// One-family helpers for tests/browser/scroll-area*.spec.ts (the brief's "Touch only": not tests/browser/helpers/).
import { type Locator } from '@playwright/test';
import { expect, fixtureUrl, type Page, type RenderMode } from './helpers';

/** Opens a ScrollArea fixture with a query and waits until its parts' handles attached and the first measurement reached C#. */
export async function gotoScrollArea(
  page: Page,
  mode: RenderMode,
  name: string,
  query: Record<string, string | number | boolean> = {},
  { measured = true }: { measured?: boolean } = {},
): Promise<Locator> {
  const search = new URLSearchParams(Object.entries(query).map(([key, value]) => [key, String(value)])).toString();
  await page.goto(`${fixtureUrl(mode, name)}${search ? `?${search}` : ''}`, { waitUntil: 'commit' });
  await page.locator('html[data-blazix-started]').waitFor({ state: 'attached' });
  await page.locator(`[data-fixture="${name}"][data-interactive="true"]`).first().waitFor({ state: 'attached' });
  const fixture = page.locator(`[data-fixture="${name}"]`);
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  if (measured) {
    await expect(fixture).toHaveAttribute('data-measured', 'true');
  }
  return fixture;
}

/** A barrier: clicks the fixture's sync button and waits for C# to count it. */
export async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

/** The computed value of a property on an element. */
export function computed(locator: Locator, property: string): Promise<string> {
  return locator.evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property);
}

/** Opens the configurable `scroll-area-parts` fixture (its query is documented in ScrollAreaPartsFixture.razor). */
export function gotoParts(
  page: Page,
  mode: RenderMode,
  query: Record<string, string | number | boolean> = {},
  options: { measured?: boolean } = {},
): Promise<Locator> {
  return gotoScrollArea(page, mode, 'scroll-area-parts', query, options);
}

/** One sample of the attribute recorder: an element (by test id), an attribute, and whether it was present. */
export interface AttributeSample {
  id: string;
  name: string;
  present: boolean;
}

/**
 * Records every change of the named attributes on elements with a test id, from the page's first script on (an init
 * script, so its observer runs before the module's; tests/browser/CONTEXT.md's recorder rule): the state is read in a
 * microtask queued from the observer's callback, never in the callback. Call before navigating.
 */
export async function recordAttributes(page: Page, names: readonly string[]): Promise<void> {
  await page.addInitScript((list) => {
    const samples: Array<{ id: string; name: string; present: boolean }> = [];
    (window as unknown as { scrollAreaSamples: typeof samples }).scrollAreaSamples = samples;
    const observer = new MutationObserver((records) => {
      const changed = records.map((record) => [record.target as Element, record.attributeName!] as const);
      queueMicrotask(() => {
        for (const [element, name] of changed) {
          const id = element.getAttribute('data-testid');
          if (id) {
            samples.push({ id, name, present: element.hasAttribute(name) });
          }
        }
      });
    });
    observer.observe(document, { attributes: true, subtree: true, attributeFilter: [...list] });
  }, names);
}

/** The recorder's samples so far. */
export function attributeSamples(page: Page): Promise<AttributeSample[]> {
  return page.evaluate(() => (window as unknown as { scrollAreaSamples: AttributeSample[] }).scrollAreaSamples.slice());
}

/** Whether the recorder saw an attribute present on an element, among the samples from `from` on. */
export async function sawAttribute(page: Page, id: string, name: string, from = 0): Promise<boolean> {
  return (await attributeSamples(page)).slice(from).some((sample) => sample.id === id && sample.name === name && sample.present);
}

/** The number of samples so far, to read later samples from. */
export async function sampleMark(page: Page): Promise<number> {
  return (await attributeSamples(page)).length;
}

/**
 * Holds the page's clock from now on: no timer of the page runs until the title runs it (`page.clock.runFor`) or resumes it
 * (tests/browser/CONTEXT.md: a title that needs frozen time pauses it itself, and the clock belongs to the worker's
 * context, so the title resumes it in a `finally`). The clock is held at the current time plus 50 ms, so timers due by then run.
 */
export async function holdClock(page: Page): Promise<void> {
  // The running clock moves on between the read and the pause, and `pauseAt` cannot go back: read it again and retry.
  for (let attempt = 0; ; attempt++) {
    try {
      await page.clock.pauseAt((await page.evaluate(() => Date.now())) + 50);
      return;
    } catch (error) {
      if (attempt >= 4 || !String(error).includes('to the past')) {
        throw error;
      }
    }
  }
}

/**
 * Replaces `ResizeObserver` with Base UI's `withMockResizeObserver` (ScrollAreaRoot.test.tsx:19-55): observers never
 * notify by themselves; `window.notifyResizeObservers()` calls each with one entry per observed element (the module's
 * `observeResize` iterates the entries, so an empty list would call nothing). Call before navigating.
 */
export async function mockResizeObserver(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const observers = new Set<{ callback: ResizeObserverCallback; targets: Set<Element>; self: ResizeObserver }>();
    class ResizeObserverMock {
      private readonly record: { callback: ResizeObserverCallback; targets: Set<Element>; self: ResizeObserver };

      constructor(callback: ResizeObserverCallback) {
        this.record = { callback, targets: new Set(), self: this as unknown as ResizeObserver };
      }

      observe(target: Element) {
        this.record.targets.add(target);
        observers.add(this.record);
      }

      unobserve(target: Element) {
        this.record.targets.delete(target);
      }

      disconnect() {
        observers.delete(this.record);
      }

      takeRecords() {
        return [];
      }
    }
    (window as unknown as { ResizeObserver: unknown }).ResizeObserver = ResizeObserverMock;
    (window as unknown as { notifyResizeObservers: () => number }).notifyResizeObservers = () => {
      for (const observer of observers) {
        const entries = [...observer.targets].map((target) => ({ target, contentRect: target.getBoundingClientRect() }));
        observer.callback(entries as unknown as ResizeObserverEntry[], observer.self);
      }
      return observers.size;
    };
  });
}

/** Calls every mocked observer (`mockResizeObserver`) and returns how many there were. */
export function notifyResizeObservers(page: Page): Promise<number> {
  return page.evaluate(() => (window as unknown as { notifyResizeObservers: () => number }).notifyResizeObservers());
}

/** Collects the page's uncaught errors, for a title whose Base UI form asserts `not.toThrow()`. */
export function collectPageErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  return errors;
}

/** Scrolls the viewport programmatically; the browser's own `scroll` event follows. */
export async function scrollViewport(page: Page, position: { top?: number; left?: number }): Promise<void> {
  await page.getByTestId('viewport').evaluate((element, { top, left }) => {
    if (top !== undefined) {
      element.scrollTop = top;
    }
    if (left !== undefined) {
      element.scrollLeft = left;
    }
  }, position);
}

/** The viewport's scroll offsets and ranges. */
export function viewportMetrics(page: Page): Promise<{ top: number; left: number; maxTop: number; maxLeft: number }> {
  return page.getByTestId('viewport').evaluate((element) => ({
    top: element.scrollTop,
    left: element.scrollLeft,
    maxTop: element.scrollHeight - element.clientHeight,
    maxLeft: element.scrollWidth - element.clientWidth,
  }));
}

/** Whether each named attribute is present on an element (Base UI's `getAttributeFlags`). */
export function attributeFlags(locator: Locator, names: readonly string[]): Promise<Record<string, boolean>> {
  return locator.evaluate((element, list) => Object.fromEntries(list.map((name) => [name, element.hasAttribute(name)])), names);
}

/** A touch point's id and position, for `TouchInput`. */
export interface TouchContact {
  id: number;
  x: number;
  y: number;
}

/**
 * Step-by-step CDP touch (`Input.dispatchTouchEvent`), for titles that start, move, end and cancel contacts one at a time
 * (a second contact while the first is down, a cancel): `touchDrag` sends whole gestures. Each call sends the contacts
 * still down after it; use it on a `testWithTouch` page.
 */
export async function touchInput(page: Page) {
  const cdp = await page.context().newCDPSession(page);
  const down = new Map<number, TouchContact>();
  const points = () => [...down.values()].map(({ id, x, y }) => ({ id, x, y }));
  return {
    async start(contact: TouchContact) {
      down.set(contact.id, contact);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: points() });
    },
    async move(contact: TouchContact) {
      down.set(contact.id, contact);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: points() });
    },
    // Chromium lifts the contacts a `touchEnd` lists (a probe lifted the first contact when `touchEnd` listed the one
    // still down), and every contact for an empty list.
    async end(id: number) {
      const contact = down.get(id)!;
      down.delete(id);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [{ id: contact.id, x: contact.x, y: contact.y }] });
    },
    async cancel() {
      down.clear();
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchCancel', touchPoints: [] });
    },
    detach: () => cdp.detach(),
  };
}

/**
 * Records the pointer events of the given types that reach the window (a bubbling listener, so after the parts' own), with
 * their pointer id and type, buttons, and whether a listener cancelled them.
 */
export async function recordWindowEvents(page: Page, types: readonly string[]): Promise<void> {
  await page.evaluate((list) => {
    const events: Array<Record<string, unknown>> = [];
    (window as unknown as { scrollAreaEvents: typeof events }).scrollAreaEvents = events;
    for (const type of list) {
      window.addEventListener(type, (event) => {
        const pointer = event as PointerEvent;
        events.push({
          type,
          target: (event.target as Element | null)?.getAttribute?.('data-testid') ?? null,
          pointerId: pointer.pointerId,
          pointerType: pointer.pointerType,
          button: pointer.button,
          buttons: pointer.buttons,
          defaultPrevented: event.defaultPrevented,
        });
      });
    }
  }, types);
}

/** A recorded window event (`recordWindowEvents`). */
export interface WindowEvent {
  type: string;
  target: string | null;
  pointerId?: number;
  pointerType?: string;
  button?: number;
  buttons?: number;
  defaultPrevented: boolean;
}

/** The events `recordWindowEvents` recorded so far. */
export function windowEvents(page: Page): Promise<WindowEvent[]> {
  return page.evaluate(() => (window as unknown as { scrollAreaEvents: WindowEvent[] }).scrollAreaEvents.slice());
}

/**
 * Moves the rendered Root, with the scrollbar-hiding style element C# renders before it, into an open shadow root of a new
 * host in its place. The port titles of Base UI's synthetic-target tests use it: React's synthetic `target` and the native
 * composed path have no DOM counterpart, and the DOM's own divergence between an event's `target` and its composed path is
 * shadow retargeting. Blazor keeps its references to the moved elements, so its later attribute writes still land.
 */
export async function moveRootIntoShadowRoot(page: Page): Promise<void> {
  await page.evaluate(() => {
    const root = document.querySelector('[data-testid="root"]')!;
    const style = root.previousElementSibling?.tagName === 'STYLE' ? root.previousElementSibling : null;
    const host = document.createElement('div');
    host.setAttribute('data-testid', 'shadow-host');
    root.parentElement!.insertBefore(host, style ?? root);
    const shadow = host.attachShadow({ mode: 'open' });
    if (style) {
      shadow.append(style);
    }
    shadow.append(root);
  });
}

/** Whether an element holds pointer capture for a pointer. */
export function hasPointerCapture(locator: Locator, pointerId: number): Promise<boolean> {
  return locator.evaluate((element, id) => element.hasPointerCapture(id), pointerId);
}

/** The inline value of a style property of an element (Base UI reads `element.style.<property>`). */
export function inlineStyle(locator: Locator, property: string): Promise<string> {
  return locator.evaluate((element, name) => (element as HTMLElement).style.getPropertyValue(name), property);
}

/** A point of an element's box, as an offset from its top-left corner. */
export async function pointIn(page: Page, testId: string, x: number | ((width: number) => number), y: number | ((height: number) => number)) {
  // A part is hit-testable once C#'s first measured render dropped its `visibility: hidden` (a round trip after the
  // measured marker), and a thumb once the measurement sized it: wait for both before aiming at it.
  await expect.poll(() => page.getByTestId(testId).evaluate((element) => getComputedStyle(element).visibility)).toBe('visible');
  if (testId.startsWith('thumb')) {
    await expect
      .poll(async () => {
        const box = await page.getByTestId(testId).boundingBox();
        return !!box && box.width > 0 && box.height > 0;
      })
      .toBe(true);
  }
  const box = (await page.getByTestId(testId).boundingBox())!;
  return {
    x: box.x + (typeof x === 'function' ? x(box.width) : x),
    y: box.y + (typeof y === 'function' ? y(box.height) : y),
  };
}

/** The scroll offset of the viewport on an axis. */
export function scrollOf(page: Page, axis: 'top' | 'left'): Promise<number> {
  return viewportMetrics(page).then((metrics) => metrics[axis]);
}

/**
 * Wraps an element's `releasePointerCapture` with a pass-through spy (Base UI spies or stubs it): each call is recorded
 * with whether the element held capture for that pointer when it was called, then the real method runs.
 */
export async function spyReleasePointerCapture(locator: Locator): Promise<void> {
  await locator.evaluate((element) => {
    const calls: Array<{ pointerId: number; held: boolean }> = [];
    (element as unknown as { releaseCalls: typeof calls }).releaseCalls = calls;
    const release = element.releasePointerCapture.bind(element);
    element.releasePointerCapture = (pointerId: number) => {
      calls.push({ pointerId, held: element.hasPointerCapture(pointerId) });
      release(pointerId);
    };
  });
}

/** The calls `spyReleasePointerCapture` recorded. */
export function releaseCalls(locator: Locator): Promise<Array<{ pointerId: number; held: boolean }>> {
  return locator.evaluate((element) => (element as unknown as { releaseCalls: Array<{ pointerId: number; held: boolean }> }).releaseCalls.slice());
}
