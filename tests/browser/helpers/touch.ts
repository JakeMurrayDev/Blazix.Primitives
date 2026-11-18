import { type Locator, type Page } from '@playwright/test';
import { test } from './test';

// M6 track 1 (the M6 plan's decisions 3 (a) and 22 (a), chosen by the user 2026-11-18): the shared touch context and
// the CDP touch drag for Slider's touch titles, Drawer's swipe, ScrollArea and Toast. The per-spec contexts that
// predate them (menu.helpers.ts `touchTest`, combobox.helpers.ts) stay as they are.

/**
 * The `test` extension for touch titles: `touchPage` is a page in a context of its own made with `hasTouch: true`, with
 * `page.clock.install()` as every shared page has (ADR 06 §2). The worker's shared context has no touch, so each title
 * opens its own context, one per title, and on `wasm` the runtime downloads and boots again. The runner traces the
 * context as it does any context the `browser` fixture makes.
 */
export const testWithTouch = test.extend<{ touchPage: Page }>({
  touchPage: async ({ browser }, use) => {
    const context = await browser.newContext({ hasTouch: true });
    const page = await context.newPage();
    await page.clock.install();
    await use(page);
    await context.close();
  },
});

/** A point in CSS pixels of the viewport. */
export interface TouchPoint {
  x: number;
  y: number;
}

export interface TouchDragOptions {
  /** The moves between two waypoints of a path, the last landing on the waypoint; 1 by default. */
  steps?: number;
  /**
   * Runs after each `touchMove` is sent, with the move's number from 1, for a title that waits on a recorded marker
   * before the next move. CDP's send resolves before the page has dispatched the move, so the hook may run before the
   * page sees it. The helper never sleeps.
   */
  afterMove?: (move: number) => Promise<void>;
  /** How the touches leave: `end` (`touchEnd`, the default) or `cancel` (`touchCancel`). */
  release?: 'end' | 'cancel';
  /** Runs after the last move and before the release. */
  beforeRelease?: () => Promise<void>;
}

/**
 * Drags one or more touch points through their waypoints with real CDP touch input (`Input.dispatchTouchEvent`), which
 * the browser turns into touch `pointerdown`/`pointermove`/`pointerup`, the touch events and, for a tap, the
 * compatibility mouse events. Each path is one finger, all paths of the same length: `touchStart` with every finger at
 * its first waypoint, `touchMove` per step with every finger, then `touchEnd` or `touchCancel`. Chromium sent no
 * `touchmove` for a first move of 10 px in M6 track 1's probe (its touch slop), though the `pointermove` arrived. Use
 * it on a page whose context has touch (`testWithTouch`). An element without `touch-action: none` lets the browser pan,
 * which ends the pointers with `pointercancel` after the first move, as on a device.
 *
 * Timing: the events carry the browser's stamps, not a timeline. M6 track 1's probe found that Chromium honours
 * `Input.dispatchTouchEvent`'s and `Input.dispatchMouseEvent`'s `timestamp` on a page without `page.clock` (16 ms apart
 * requested, 16.0 ± 0.1 ms seen), but every fixture page installs the clock, whose patched `Event.prototype.timeStamp`
 * replaces it; under a paused clock advanced 16 ms between sends, touch moves were seen 16, 32, 16 and 0 ms apart,
 * because the browser delivers them aligned to frames. A velocity title therefore uses `swipeOnTimeline`.
 * @param page The page.
 * @param paths One array of waypoints per finger.
 * @param options Steps, the release and the hooks.
 */
export async function touchDrag(
  page: Page,
  paths: ReadonlyArray<ReadonlyArray<TouchPoint>>,
  options: TouchDragOptions = {},
): Promise<void> {
  const { steps = 1, release = 'end', afterMove, beforeRelease } = options;
  const length = paths[0]?.length ?? 0;
  if (length === 0 || paths.some((path) => path.length !== length)) {
    throw new Error('touchDrag: give at least one path, every path with the same number of waypoints.');
  }

  const cdp = await page.context().newCDPSession(page);
  const send = async (type: 'touchStart' | 'touchMove' | 'touchEnd' | 'touchCancel', points: TouchPoint[]) => {
    const touchPoints = points.map((point, id) => ({ x: point.x, y: point.y, id }));
    await cdp.send('Input.dispatchTouchEvent', { type, touchPoints });
  };

  let moves = 0;
  try {
    await send('touchStart', paths.map((path) => path[0]!));
    for (let waypoint = 1; waypoint < length; waypoint++) {
      for (let step = 1; step <= steps; step++) {
        const t = step / steps;
        await send(
          'touchMove',
          paths.map((path) => {
            const from = path[waypoint - 1]!;
            const to = path[waypoint]!;
            return { x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t };
          }),
        );
        await afterMove?.(++moves);
      }
    }
    await beforeRelease?.();
    await send(release === 'end' ? 'touchEnd' : 'touchCancel', []);
  } finally {
    await cdp.detach();
  }
}

/**
 * The point of a locator's box: its centre, or an offset from its top-left corner.
 * @param target The element.
 * @param offset The offset from the top-left corner, in CSS pixels; the centre when omitted.
 * @returns The point in viewport coordinates.
 */
export async function pointOf(target: Locator, offset?: TouchPoint): Promise<TouchPoint> {
  const box = await target.boundingBox();
  if (!box) {
    throw new Error('pointOf: the element has no box.');
  }
  if (offset) {
    return { x: box.x + offset.x, y: box.y + offset.y };
  }
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}
