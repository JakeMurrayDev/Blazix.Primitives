import { type Locator } from '@playwright/test';
import { test, type Page } from './helpers';

// Shared by the `menu.*.spec.ts` files (M5 track 6, issue 172). Named `menu.helpers.ts` so the L4 scope classifier
// (.github/scripts/classify-paths.mjs, the part before the first dot) counts it as the family's.

/** The centre of an element, in page coordinates. */
export async function center(locator: Locator): Promise<{ x: number; y: number }> {
  const box = (await locator.boundingBox())!;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/** Moves the real pointer onto an element, in steps, so it enters it and moves over it. */
export async function moveTo(page: Page, locator: Locator): Promise<void> {
  const point = await center(locator);
  await page.mouse.move(point.x, point.y, { steps: 4 });
}

/** Base UI's `resetBrowserPointer`: the worker's page keeps the pointer where the last title left it. */
export async function parkPointer(page: Page): Promise<void> {
  await page.mouse.move(0, 0);
}

/** A real click at an element's centre, whatever covers it: an open modal menu's internal backdrop takes the press. */
export async function clickThrough(page: Page, locator: Locator): Promise<void> {
  const point = await center(locator);
  await page.mouse.click(point.x, point.y);
}

/** A fixture's recorded list attribute, split on its separator. */
export async function recorded(fixture: Locator, attribute: string, separator: ' ' | '|' = ' '): Promise<string[]> {
  return ((await fixture.getAttribute(attribute)) ?? '').split(separator).filter(Boolean);
}

/**
 * Runs a timed title with `page.clock`: time flows after `install` until a `runFor`, which leaves it held, so a title
 * resumes it once its timers have run (C#'s renders reach the page through timers too), and again here because the clock
 * belongs to the worker's context (tests/browser/CONTEXT.md).
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

/** Holds the page's clock from now on, until `page.clock.resume`: no timer of the page runs, and no window closes. */
export async function holdClock(page: Page): Promise<void> {
  await page.clock.pauseAt((await page.evaluate(() => Date.now())) + 100);
}

/** Waits out a stretch of the page's time (Base UI's `waitMs`), never the test's: timers of the page run, then time flows again. */
export async function waitMs(page: Page, ms: number): Promise<void> {
  await withClock(page, async () => {
    await page.clock.runFor(ms);
  });
}

/**
 * Records whether each selector ever matches, from the page's first script on, for a state that may last one batch
 * (MenuPopupEnterTransition.test.tsx's `trackSelector`, which polls per frame; ADR 06 §2 takes a mutation record). The DOM
 * is sampled in a microtask queued from the observer, after the modules' own observers of the batch (tests/browser/CONTEXT.md).
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
      const Constructor = pointer ? PointerEvent : MouseEvent;
      element.dispatchEvent(new Constructor(name, { bubbles: true, cancelable: true, composed: true, ...(options as MouseEventInit) }));
    },
    { type, init },
  );
}

/** Presses a key the keyboard layout lacks (`ą`) as a trusted key event: Playwright's `type` inserts such text with no keydown. */
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
