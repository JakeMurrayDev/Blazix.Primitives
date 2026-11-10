import { type Locator } from '@playwright/test';
import { delayCircuit, expect, test, waitForPositioned, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// Shared by the `combobox.*.spec.ts` files that port the Base UI titles of Combobox's [L4] rows (M5 track 10, issue 174).
// Named `combobox.helpers.ts` so the L4 scope classifier (.github/scripts/classify-paths.mjs, the part before the first
// dot) counts it as the family's. The generic helpers repeat menu.helpers.ts (track 6), which this branch does not have.

/** Query options a fixture reads (`PopupSuiteFixtureBase.Query`); `undefined` leaves an option out. */
export type FixtureOptions = Record<string, string | number | boolean | undefined>;

/**
 * Opens a Combobox fixture and waits for its `data-attached` marker (the session's and the parts' handles), so the first
 * gesture is never lost. With `BLAZIX_DELAY_CIRCUIT_MS` set, a Server page's circuit is delayed by that many milliseconds
 * each way before the page loads, so one run takes every title through a slow round trip (the orchestrator's rule for
 * track 10's pointer and keyboard titles, 2026-11-10).
 */
export async function gotoCombobox(page: Page, mode: RenderMode, fixture: string, options: FixtureOptions = {}): Promise<Locator> {
  const delay = Number(process.env.BLAZIX_DELAY_CIRCUIT_MS ?? 0);
  if (mode === 'server' && delay > 0) {
    await delayCircuit(page, delay);
  }

  const query: Record<string, string | boolean | undefined> = {};
  for (const [key, value] of Object.entries(options)) {
    query[key] = typeof value === 'number' ? String(value) : value;
  }

  const container = await gotoPopupFixture(page, mode, fixture, query);
  await expect(container).toHaveAttribute('data-attached', 'true');
  return container;
}

/** The Positioner an element sits in, when it sits in one: its outermost ancestor carrying `data-side`. */
export function positionerOf(locator: Locator): Locator {
  return locator.locator('xpath=ancestor-or-self::*[@data-side][last()]');
}

/**
 * The centre of an element, in page coordinates, once the popup it sits in (if any) has its position. An inline list or a
 * popup that is never positioned passes `unanchored`.
 */
export async function center(locator: Locator, options: { unanchored?: boolean } = {}): Promise<{ x: number; y: number }> {
  const positioner = positionerOf(locator);
  if (!options.unanchored && (await positioner.count()) > 0) {
    await waitForPositioned(positioner);
  }
  const box = (await locator.boundingBox())!;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/** Moves the real pointer onto an element, in steps, so it enters it and moves over it. */
export async function moveTo(page: Page, locator: Locator, options: { unanchored?: boolean } = {}): Promise<void> {
  const point = await center(locator, options);
  await page.mouse.move(point.x, point.y, { steps: 4 });
}

/** Base UI's `resetBrowserPointer`: the worker's page keeps the pointer where the last title left it. */
export async function parkPointer(page: Page): Promise<void> {
  await page.mouse.move(0, 0);
}

/** A real click at an element's centre, whatever covers it (an open modal popup's internal backdrop takes the press). */
export async function clickThrough(page: Page, locator: Locator, options: { unanchored?: boolean } = {}): Promise<void> {
  const point = await center(locator, options);
  await page.mouse.click(point.x, point.y);
}

/** A fixture's recorded list attribute, split on its separator. */
export async function recorded(fixture: Locator, attribute: string, separator: ' ' | '|' = ' '): Promise<string[]> {
  return ((await fixture.getAttribute(attribute)) ?? '').split(separator).filter(Boolean);
}

/**
 * Runs a timed title with `page.clock`: time flows after `install` until a `runFor`, which leaves it held, so a title
 * resumes it once its timers have run, and again here because the clock belongs to the worker's context
 * (tests/browser/CONTEXT.md). On WebAssembly a held clock also holds .NET's continuations (they run on `setTimeout`), so a
 * title that waits for C# while the clock is held steps it with `runClockUntil`.
 */
export async function withClock(page: Page, body: () => Promise<void>): Promise<void> {
  await page.clock.install();
  try {
    await body();
  } finally {
    await page.clock.resume();
  }
}

/** Base UI's `waitForFrames`: two of the page's animation frames. */
export function waitFrames(page: Page): Promise<void> {
  return page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
}

/** Holds the page's clock from now on, until `page.clock.resume`: no timer of the page runs. */
export async function holdClock(page: Page): Promise<void> {
  await page.clock.pauseAt((await page.evaluate(() => Date.now())) + 100);
}

/**
 * Runs a held clock in 10 ms steps until `shown` holds, at most `limitMs` of the page's time, and leaves it held (M's
 * `runClockUntil`, track 6): a frozen clock stalls WebAssembly's module writes, so a held title steps it instead.
 */
export async function runClockUntil(page: Page, shown: () => Promise<boolean>, limitMs = 400): Promise<void> {
  for (let ran = 0; ran < limitMs && !(await shown()); ran += 10) {
    await page.clock.runFor(10);
  }
}

/** Waits out a stretch of the page's time (Base UI's `waitMs`), never the test's: timers of the page run, then time flows again. */
export async function waitMs(page: Page, ms: number): Promise<void> {
  await withClock(page, async () => {
    await page.clock.runFor(ms);
  });
}

/**
 * Records whether each selector ever matches, from the page's first script on, for a state that may last one batch (ADR 06
 * §2 takes a mutation record). The DOM is sampled in a microtask queued from the observer.
 */
export async function recordSelectors(page: Page, selectors: readonly string[]): Promise<void> {
  await page.addInitScript((list) => {
    const seen = new Set<string>();
    (window as unknown as { seenSelectors: Set<string> }).seenSelectors = seen;
    const sample = () => {
      for (const selector of list) {
        if (document.querySelector(selector)) {
          seen.add(selector);
        }
      }
    };
    new MutationObserver(() => queueMicrotask(sample)).observe(document, { attributes: true, childList: true, subtree: true });
  }, selectors);
}

/** Whether a selector given to recordSelectors has matched. */
export function selectorSeen(page: Page, selector: string): Promise<boolean> {
  return page.evaluate((name) => (window as unknown as { seenSelectors: Set<string> }).seenSelectors.has(name), selector);
}

/** Dispatches a DOM event Base UI fires with `fireEvent`, where a real input cannot take the same path. */
export function fire(locator: Locator, type: string, init: Record<string, unknown> = {}): Promise<void> {
  return locator.evaluate(
    (element, { type: name, init: options }) => {
      const pointer = name.startsWith('pointer');
      const Constructor = pointer ? PointerEvent : name.startsWith('key') ? KeyboardEvent : MouseEvent;
      element.dispatchEvent(new Constructor(name, { bubbles: true, cancelable: true, composed: true, ...(options as MouseEventInit) }));
    },
    { type, init },
  );
}

/** Presses a key the keyboard layout lacks as a trusted key event: Playwright's `type` inserts such text with no keydown. */
export async function pressCharacter(page: Page, character: string): Promise<void> {
  const session = await page.context().newCDPSession(page);
  await session.send('Input.dispatchKeyEvent', { type: 'keyDown', key: character, text: character, unmodifiedText: character });
  await session.send('Input.dispatchKeyEvent', { type: 'keyUp', key: character });
  await session.detach();
}

// Touch titles: Playwright taps only in a context made with `hasTouch`, and the worker's shared context is not, so each
// touch title opens its own (on `wasm` it downloads the runtime again), as popover.spec.ts does.
export const touchTest = test.extend<{ touchPage: Page }>({
  touchPage: async ({ browser }, use) => {
    const context = await browser.newContext({ hasTouch: true });
    const page = await context.newPage();
    await page.clock.install();
    await use(page);
    await context.close();
  },
});
