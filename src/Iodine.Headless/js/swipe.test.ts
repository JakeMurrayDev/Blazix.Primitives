// L3 (ADR 06 §1): the `swipe` mechanism module in jsdom. The shared constants, selectors and displacement come first;
// Base UI tests none of them directly (they are module constants of utils/useSwipeDismiss.ts and
// internals/constants.ts), so those titles are the port's own: PSH §4.7.5's constants and displacement, and
// UTL-EDGE-19's swipe-ignore clause. The tracker's titles follow (M6 track 8, PR 8b): utils/useSwipeDismiss.test.tsx,
// transcribed for a listener-bound tracker (PSH-PTR-09..28, PSH-ALG-07..12, PSH-DOM-17, PSH-EVT-09, PSH-EDGE-10..13).
import { afterEach, describe, expect, it, vi } from 'vitest';
import { firePointer } from './test-support/pointer';
import { styleWriter } from './dom';
import {
  BASE_UI_SWIPE_IGNORE_ATTRIBUTE,
  BASE_UI_SWIPE_IGNORE_SELECTOR,
  DEFAULT_IGNORE_SELECTOR,
  DEFAULT_SWIPE_THRESHOLD,
  LEGACY_SWIPE_IGNORE_ATTRIBUTE,
  LEGACY_SWIPE_IGNORE_SELECTOR,
  MAX_RELEASE_VELOCITY_AGE_MS,
  MIN_RELEASE_VELOCITY_DURATION_MS,
  MIN_VELOCITY_DURATION_MS,
  MIN_VELOCITY_SAMPLE_DISTANCE,
  REVERSE_CANCEL_THRESHOLD,
  bindSwipeHandlers,
  createSwipeTracker,
  getDisplacement,
  type SwipeDirection,
  type SwipeTracker,
  type SwipeTrackerOptions,
} from './swipe';

function fragment(html: string): HTMLElement {
  const host = document.createElement('div');
  host.innerHTML = html;
  return host;
}

// PSH §4.7.5 (useSwipeDismiss.ts:41-54).
describe('getDisplacement', () => {
  it('port: measures the displacement along each direction, positive toward it', () => {
    expect(getDisplacement('up', 3, -20)).toBe(20);
    expect(getDisplacement('up', 3, 20)).toBe(-20);
    expect(getDisplacement('down', 3, 20)).toBe(20);
    expect(getDisplacement('down', 3, -20)).toBe(-20);
    expect(getDisplacement('left', -15, 4)).toBe(15);
    expect(getDisplacement('left', 15, 4)).toBe(-15);
    expect(getDisplacement('right', 15, 4)).toBe(15);
    expect(getDisplacement('right', -15, 4)).toBe(-15);
  });

  it('port: measures zero along an unknown direction', () => {
    expect(getDisplacement('right', 15, 4)).toBe(15);
    expect(getDisplacement('diagonal' as SwipeDirection, 15, 4)).toBe(0);
    expect(getDisplacement(undefined as unknown as SwipeDirection, 15, 4)).toBe(0);
  });
});

// PSH §4.7.5 (useSwipeDismiss.ts:32-39).
describe('constants', () => {
  it("port: keeps the tracker's thresholds and durations", () => {
    expect(DEFAULT_SWIPE_THRESHOLD).toBe(40);
    expect(REVERSE_CANCEL_THRESHOLD).toBe(10);
    expect(MIN_VELOCITY_DURATION_MS).toBe(50);
    expect(MIN_RELEASE_VELOCITY_DURATION_MS).toBe(4);
    expect(MAX_RELEASE_VELOCITY_AGE_MS).toBe(80);
    expect(MIN_VELOCITY_SAMPLE_DISTANCE).toBe(1);
  });

  it('port: the default ignored-target selector matches each interactive element it lists, and not a plain element', () => {
    const host = fragment(
      '<button></button><a></a><input><select></select><textarea></textarea><label></label><div role="button"></div><div id="plain"></div><span role="link"></span>',
    );
    const matched = [...host.children].filter((element) => element.matches(DEFAULT_IGNORE_SELECTOR));
    expect(matched.map((element) => element.tagName.toLowerCase() + (element.getAttribute('role') ?? ''))).toEqual([
      'button',
      'a',
      'input',
      'select',
      'textarea',
      'label',
      'divbutton',
    ]);
    expect(DEFAULT_IGNORE_SELECTOR).toBe('button,a,input,select,textarea,label,[role="button"]');
  });
});

// UTL-EDGE-19, the swipe-ignore clause (internals/constants.ts:8-12).
describe('swipe-ignore selectors', () => {
  it("port: each swipe-ignore selector matches its own attribute and not the other's", () => {
    const host = fragment('<div data-base-ui-swipe-ignore></div><div data-swipe-ignore></div><div data-base-ui-swipe-ignore="x"></div>');
    const [current, legacy, axis] = [...host.children];

    expect(BASE_UI_SWIPE_IGNORE_ATTRIBUTE).toBe('data-base-ui-swipe-ignore');
    expect(LEGACY_SWIPE_IGNORE_ATTRIBUTE).toBe('data-swipe-ignore');
    expect(BASE_UI_SWIPE_IGNORE_SELECTOR).toBe('[data-base-ui-swipe-ignore]');
    expect(LEGACY_SWIPE_IGNORE_SELECTOR).toBe('[data-swipe-ignore]');

    expect(current!.matches(BASE_UI_SWIPE_IGNORE_SELECTOR)).toBe(true);
    expect(axis!.matches(BASE_UI_SWIPE_IGNORE_SELECTOR)).toBe(true);
    expect(legacy!.matches(LEGACY_SWIPE_IGNORE_SELECTOR)).toBe(true);
    expect(current!.matches(LEGACY_SWIPE_IGNORE_SELECTOR)).toBe(false);
    expect(legacy!.matches(BASE_UI_SWIPE_IGNORE_SELECTOR)).toBe(false);
  });
});

// The tracker (utils/useSwipeDismiss.test.tsx). Base UI renders a component whose element spreads `getDragStyles()`
// into its `style` and `getPointerProps()` (or `getTouchProps()`) into its props; here `mountTracker` binds the same
// handlers with `bindSwipeHandlers` and writes `getDragStyles()` once, the consumer's first render. Base UI's
// `fireEvent` calls become dispatched events: pointer events carry `pointerType: 'mouse'`, touch events are plain
// events with a `touches` (and `changedTouches`) list, since jsdom has no `Touch`. `flushMicrotasks` has no
// counterpart: the tracker reacts inside the dispatch.

const mounted: HTMLElement[] = [];

afterEach(() => {
  for (const element of mounted.splice(0)) {
    element.remove();
  }
  document.body.style.cssText = '';
  document.documentElement.style.cssText = '';
});

interface Mounted {
  element: HTMLElement;
  tracker: SwipeTracker;
}

type TrackerInit = Partial<SwipeTrackerOptions> & Pick<SwipeTrackerOptions, 'directions'>;

function mountTracker(init: TrackerInit, options: { html?: string; input?: 'pointer' | 'touch'; style?: string } = {}): Mounted {
  const element = document.createElement('div');
  element.innerHTML = options.html ?? '';
  document.body.append(element);
  mounted.push(element);

  // The port's harness (the review of 2026-11-19): the element's `style` belongs to dom's writer, as on a part. The
  // rendered style (the tracker's drag styles, then the test's own) is the consumer's, and the drag styles go to the
  // `component` band, as a module's sink sends them.
  const writer = styleWriter(element);
  const tracker = createSwipeTracker({
    enabled: true,
    element: () => element,
    movementCssVars: { x: '--x', y: '--y' },
    style: writer.layer('component'),
    ...init,
  });

  const styles = tracker.getDragStyles();
  const rendered = Object.keys(styles).map((property) => `${property}: ${styles[property]!};`);
  writer.setConsumer([...rendered, options.style ?? ''].join(' ').trim());

  bindSwipeHandlers(element, options.input === 'touch' ? tracker.getTouchProps() : tracker.getPointerProps());
  return { element, tracker };
}

function child(element: HTMLElement, selector: string): HTMLElement {
  return element.querySelector<HTMLElement>(selector)!;
}

/** Overrides layout values jsdom reports as 0, the way Base UI's tests do when the runner has no layout. */
function stubLayout(element: HTMLElement, values: Record<string, number>): void {
  for (const [name, value] of Object.entries(values)) {
    Object.defineProperty(element, name, { value, configurable: true });
  }
}

function pointerDown(target: Element, init: PointerEventInit = {}): boolean {
  return target.dispatchEvent(
    new PointerEvent('pointerdown', { bubbles: true, cancelable: true, button: 0, buttons: 1, pointerId: 1, pointerType: 'mouse', ...init }),
  );
}

function pointerMove(target: Element, init: PointerEventInit = {}): boolean {
  return target.dispatchEvent(
    new PointerEvent('pointermove', { bubbles: true, cancelable: true, buttons: 1, pointerId: 1, pointerType: 'mouse', ...init }),
  );
}

function pointerUp(target: Element, init: PointerEventInit = {}): boolean {
  return target.dispatchEvent(
    new PointerEvent('pointerup', { bubbles: true, cancelable: true, button: 0, buttons: 0, pointerId: 1, pointerType: 'mouse', ...init }),
  );
}

type Point = { clientX: number; clientY: number };

function fireTouch(type: 'touchstart' | 'touchmove' | 'touchend', target: Element, touches: Point[], changedTouches: Point[] = touches): Event {
  const event = new Event(type, { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'touches', { value: touches });
  Object.defineProperty(event, 'changedTouches', { value: changedTouches });
  target.dispatchEvent(event);
  return event;
}

const timed = { pointerId: 1, pointerType: 'mouse', bubbles: true };

describe('createSwipeTracker', () => {
  it('does not start swiping within a scrollable element when ignoreScrollableAncestors is true', () => {
    const onSwipeStart = vi.fn();
    const { element: root } = mountTracker(
      { directions: ['down'], ignoreScrollableAncestors: true, onSwipeStart },
      { html: '<div data-testid="scroll" style="overflow-y: auto; height: 100px"><div style="height: 200px"></div></div>' },
    );
    const scroll = child(root, '[data-testid="scroll"]');
    stubLayout(scroll, { scrollHeight: 200, clientHeight: 100 });

    const elementFromPoint = Object.getOwnPropertyDescriptor(document, 'elementFromPoint');
    Object.defineProperty(document, 'elementFromPoint', { value: () => scroll, configurable: true });
    try {
      pointerDown(scroll, { clientX: 0, clientY: 100 });
      pointerMove(scroll, { clientX: 0, clientY: 150 });

      expect(onSwipeStart).not.toHaveBeenCalled();
      expect(root.style.getPropertyValue('--y')).toBe('0px');
    } finally {
      if (elementFromPoint) {
        Object.defineProperty(document, 'elementFromPoint', elementFromPoint);
      } else {
        delete (document as { elementFromPoint?: unknown }).elementFromPoint;
      }
    }
  });

  // Base UI skips this title and the next in jsdom (`it.skipIf(isJSDOM)`) because they need real layout; the port runs
  // them here with the page scroller's and the descendant's sizes stubbed, since `swipe` has no L4 host (ADR 04
  // Amendments 2026-11-19, design point 3).
  it('starts a touch swipe when the page scroller is `body`', () => {
    const onSwipeStart = vi.fn();
    const { body } = document;
    document.documentElement.style.cssText = 'height: 100%; overflow-y: auto';
    body.style.cssText = 'height: 100%; overflow-y: auto';
    stubLayout(body, { scrollHeight: 5000, clientHeight: 600, scrollTop: 0 });
    try {
      const { element } = mountTracker({ directions: ['down'], onSwipeStart }, { input: 'touch', style: 'position: fixed; inset: 0;' });

      expect(body.scrollTop).toBe(0);
      expect(body.scrollHeight).toBeGreaterThan(body.clientHeight);

      fireTouch('touchstart', element, [{ clientX: 50, clientY: 300 }]);
      fireTouch('touchmove', element, [{ clientX: 50, clientY: 260 }]);

      expect(onSwipeStart).toHaveBeenCalledTimes(1);
    } finally {
      for (const name of ['scrollHeight', 'clientHeight', 'scrollTop']) {
        delete (body as unknown as Record<string, unknown>)[name];
      }
    }
  });

  it('keeps gating on a scrollable descendant of the other axis when `body` scrolls the page', () => {
    const onSwipeStart = vi.fn();
    const { body } = document;
    document.documentElement.style.cssText = 'height: 100%; overflow-y: auto';
    body.style.cssText = 'height: 100%; overflow-y: auto';
    stubLayout(body, { scrollHeight: 5000, clientHeight: 600 });
    try {
      const { element } = mountTracker(
        { directions: ['right'], onSwipeStart },
        {
          input: 'touch',
          style: 'position: fixed; inset: 0;',
          html: '<div data-testid="scroll" style="overflow-x: auto; overflow-y: hidden; width: 100%; height: 100%"><div style="width: 5000px; height: 20px"></div></div>',
        },
      );
      const scroll = child(element, '[data-testid="scroll"]');
      stubLayout(scroll, { scrollWidth: 5000, clientWidth: 400, scrollLeft: 20 });

      expect(body.scrollHeight).toBeGreaterThan(body.clientHeight);
      expect(scroll.scrollWidth).toBeGreaterThan(scroll.clientWidth);
      expect(scroll.scrollLeft).toBeGreaterThan(0);

      fireTouch('touchstart', scroll, [{ clientX: 50, clientY: 300 }]);
      fireTouch('touchmove', scroll, [{ clientX: 90, clientY: 300 }]);

      expect(onSwipeStart).not.toHaveBeenCalled();
    } finally {
      for (const name of ['scrollHeight', 'clientHeight']) {
        delete (body as unknown as Record<string, unknown>)[name];
      }
    }
  });

  it('does not prevent touch scrolling during swipe interactions', () => {
    const { element } = mountTracker({ directions: ['down'] });

    pointerDown(element, { clientX: 0, clientY: 100 });
    // The first move establishes the baseline.
    pointerMove(element, { clientX: 0, clientY: 100 });
    // A move up (unsupported) does not block the default touch scroll.
    pointerMove(element, { clientX: 0, clientY: 50 });

    const touchMoveBefore = new Event('touchmove', { bubbles: true, cancelable: true });
    element.dispatchEvent(touchMoveBefore);
    expect(touchMoveBefore.defaultPrevented).toBe(false);

    // Once a supported direction is detected, touchmove is still not prevented.
    pointerMove(element, { clientX: 0, clientY: 150 });

    const touchMoveAfter = new Event('touchmove', { bubbles: true, cancelable: true });
    element.dispatchEvent(touchMoveAfter);
    expect(touchMoveAfter.defaultPrevented).toBe(false);
  });

  it('fires onProgress relative to the element size', () => {
    const onProgress = vi.fn();
    const { element } = mountTracker({ directions: ['right'], onProgress });
    stubLayout(element, { offsetWidth: 200 });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 50, clientY: 0 });

    expect(onProgress.mock.calls.at(-1)?.[0]).toBeCloseTo(0.25, 2);
  });

  it('continues firing onProgress when swipe progress is clamped', () => {
    const onProgress = vi.fn();
    const { element } = mountTracker({ directions: ['right'], onProgress });
    stubLayout(element, { offsetWidth: 200 });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 50, clientY: 0 });
    const callsAfterForward = onProgress.mock.calls.length;

    // Past the starting point in the opposite direction; progress is clamped to 0.
    pointerMove(element, { clientX: -10, clientY: 0 });
    const callsAfterReverse = onProgress.mock.calls.length;
    expect(callsAfterReverse).toBeGreaterThan(callsAfterForward);

    pointerMove(element, { clientX: -20, clientY: 0 });
    expect(onProgress.mock.calls.length).toBeGreaterThan(callsAfterReverse);
    expect(onProgress.mock.calls.at(-1)?.[0]).toBe(0);
  });

  it('applies exponential damping for opposite-direction movement', () => {
    const { element } = mountTracker({ directions: ['down'] });

    pointerDown(element, { clientX: 0, clientY: 100 });
    expect(element.style.transition).toBe('none');

    pointerMove(element, { clientX: 0, clientY: 100 });
    pointerMove(element, { clientX: 0, clientY: 50 });

    expect(element.style.getPropertyValue('--y')).not.toBe('0px');
  });

  it('preserves inline transform and transition when resetting drag styles', () => {
    const { element, tracker } = mountTracker({ directions: ['down'] }, { style: 'transform: scale(0.9); transition: opacity 200ms ease;' });
    const initialTransform = element.style.transform;
    const initialTransition = element.style.transition;

    expect(initialTransform).not.toBe('');
    expect(initialTransition).not.toBe('');

    tracker.reset();

    expect(element.style.transform).toBe(initialTransform);
    expect(element.style.transition).toBe(initialTransition);

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 12 });
    pointerMove(element, { clientX: 0, clientY: 16 });
    pointerUp(element, { clientX: 0, clientY: 16 });

    expect(element.style.transform).toBe(initialTransform);
    expect(element.style.transition).toBe(initialTransition);
  });

  it('applies the drag transform imperatively while swiping', () => {
    const { element } = mountTracker({ directions: ['down'] });

    pointerDown(element, { clientX: 0, clientY: 0 });
    // The first move only establishes the drag baseline.
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 40 });

    expect(element.style.transition).toBe('none');
    expect(element.style.transform).toMatch(/translate3d\(0px, ?40px, ?0(?:px)?\)/);
    expect(element.style.getPropertyValue('--y')).toBe('40px');
  });

  // Base UI captures `getDragStyles` from a render whose `isSwiping` state still lags behind the gesture; the port has
  // no lagging render, so its analogue is a consumer that reads `getDragStyles()` mid-gesture (the drawer module's
  // update path): the read mirrors the live gesture, not a stale flag.
  it('keeps the drag transform on a render that lags behind the swiping state', () => {
    const { element, tracker } = mountTracker({ directions: ['down'] });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 40 });

    expect(element.style.transition).toBe('none');

    const laggingStyles = tracker.getDragStyles();
    expect(laggingStyles.transition).toBe('none');
    expect(laggingStyles.transform).toMatch(/translate3d\(0px, ?40px, ?0(?:px)?\)/);
  });

  it('respects custom swipeThreshold', () => {
    const onDismiss = vi.fn();
    const { element } = mountTracker({ directions: ['down'], swipeThreshold: 10, onDismiss });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    // Beyond the custom 10px threshold.
    pointerMove(element, { clientX: 0, clientY: 20 });
    pointerUp(element, { clientX: 0, clientY: 20 });

    expect(onDismiss).toHaveBeenCalled();
  });

  // Base UI runs this title twice, with and without React's StrictMode (`strict: %s`); the port has no StrictMode, so
  // one title stands for both rows. Base UI's `setProps` is `setOptions`.
  it('snapshots swipeThreshold for the active gesture', () => {
    const onDismiss = vi.fn();
    const init: TrackerInit = { directions: ['down'], swipeThreshold: 50, onDismiss };
    const { element, tracker } = mountTracker(init);

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });

    tracker.setOptions({ enabled: true, element: () => element, movementCssVars: { x: '--x', y: '--y' }, style: styleWriter(element).layer('component'), ...init, swipeThreshold: 10 });

    pointerMove(element, { clientX: 0, clientY: 20 });
    pointerUp(element, { clientX: 0, clientY: 20 });

    expect(onDismiss).not.toHaveBeenCalled();

    pointerDown(element, { pointerId: 2, clientX: 0, clientY: 0 });
    pointerMove(element, { pointerId: 2, clientX: 0, clientY: 0 });
    pointerMove(element, { pointerId: 2, clientX: 0, clientY: 20 });
    pointerUp(element, { pointerId: 2, clientX: 0, clientY: 20 });

    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('fires onSwipingChange on start and end', () => {
    const onSwipingChange = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onSwipingChange });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerUp(element, { clientX: 0, clientY: 0 });

    expect(onSwipingChange).toHaveBeenCalledTimes(2);
    expect(onSwipingChange).toHaveBeenNthCalledWith(1, true);
    expect(onSwipingChange).toHaveBeenLastCalledWith(false);
  });

  it('cancels pointer swipe when the primary mouse button is released without pointerup', () => {
    const onDismiss = vi.fn();
    const onSwipingChange = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onDismiss, onSwipingChange });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 12 });

    expect(onSwipingChange).toHaveBeenCalledWith(true);
    expect(element.style.getPropertyValue('--y')).not.toBe('0px');

    pointerMove(element, { buttons: 0, clientX: 0, clientY: 16 });

    expect(onSwipingChange).toHaveBeenLastCalledWith(false);
    expect(element.style.getPropertyValue('--y')).toBe('0px');
    expect(onDismiss).not.toHaveBeenCalled();

    pointerMove(element, { buttons: 0, clientX: 0, clientY: 40 });

    expect(element.style.getPropertyValue('--y')).toBe('0px');
    expect(onDismiss).not.toHaveBeenCalled();
  });

  it('commits the swipe when the primary button is released past the threshold without pointerup', () => {
    const onDismiss = vi.fn();
    const onCancel = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onDismiss, onCancel });

    pointerDown(element, { clientX: 0, clientY: 0 });
    // The first move establishes the start position; the second provides the displacement.
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 100 });
    // A fast trackpad flick releases the click just before pointerup: a trailing `buttons: 0` move commits the gesture.
    pointerMove(element, { buttons: 0, clientX: 0, clientY: 100 });

    expect(onDismiss).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('uses the final buttons:0 release move to decide dismissal when it crosses the threshold', () => {
    const onDismiss = vi.fn();
    const onCancel = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onDismiss, onCancel });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    // The last pressed move stays just below the 40px threshold.
    pointerMove(element, { clientX: 0, clientY: 35 });
    // The trailing release move carries the threshold-crossing displacement and is applied before the decision.
    pointerMove(element, { buttons: 0, clientX: 0, clientY: 100 });

    expect(onDismiss).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('resets swiping when touch ends over a scrollable descendant', () => {
    const onSwipingChange = vi.fn();
    const { element: root } = mountTracker(
      { directions: ['down'], onSwipingChange },
      { input: 'touch', html: '<div data-testid="touch-scroll" style="overflow-y: auto; max-height: 40px"><div style="height: 120px"></div></div>' },
    );
    const scroll = child(root, '[data-testid="touch-scroll"]');
    stubLayout(scroll, { scrollHeight: 120, clientHeight: 40 });

    fireTouch('touchstart', root, [{ clientX: 0, clientY: 0 }]);
    fireTouch('touchmove', root, [{ clientX: 0, clientY: 20 }]);
    fireTouch('touchend', scroll, [], [{ clientX: 0, clientY: 20 }]);

    expect(onSwipingChange).toHaveBeenCalledTimes(2);
    expect(onSwipingChange).toHaveBeenNthCalledWith(1, true);
    expect(onSwipingChange).toHaveBeenLastCalledWith(false);
    expect(root.style.getPropertyValue('--y')).toBe('0px');
  });

  it('allows onRelease to override dismissal', () => {
    const onDismiss = vi.fn();
    const onRelease = vi.fn(() => false);
    const { element } = mountTracker({ directions: ['down'], swipeThreshold: 10, onDismiss, onRelease });

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 20 });
    pointerUp(element, { clientX: 0, clientY: 20 });

    expect(onRelease).toHaveBeenCalled();
    expect(onDismiss).not.toHaveBeenCalled();
  });

  // The velocity titles follow Base UI's fixed timelines through `firePointer` (Base UI also switches to fake timers,
  // which the tracker never reads: it measures `event.timeStamp`).
  it('provides swipe velocity on release', () => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['right'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1100 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 50, clientY: 0, timeStamp: 1200 });
    firePointer.up(element, { ...timed, clientX: 50, clientY: 0, timeStamp: 1300 });

    const details = onRelease.mock.calls[0]?.[0];
    expect(details?.velocityX).toBeCloseTo(0.25, 2);
    expect(details?.velocityY).toBeCloseTo(0, 2);
  });

  it('provides release velocity from the latest swipe movement', () => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['right'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1100 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 50, clientY: 0, timeStamp: 1200 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 70, clientY: 0, timeStamp: 1216 });
    firePointer.up(element, { ...timed, clientX: 70, clientY: 0, timeStamp: 1224 });

    const details = onRelease.mock.calls[0]?.[0];
    expect(details?.releaseVelocityX).toBeCloseTo(1.25, 2);
    expect(details?.releaseVelocityY).toBeCloseTo(0, 2);
  });

  it.each([
    { trailingTime: 1040, releaseTime: 1048, expectedVelocity: 2.5 },
    { trailingTime: 1040, releaseTime: 1113, expectedVelocity: 0 },
    { trailingTime: 1113, releaseTime: 1121, expectedVelocity: 0 },
  ])('handles a stationary move at $trailingTime and release at $releaseTime', ({ trailingTime, releaseTime, expectedVelocity }) => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    for (const [timeStamp, clientY] of [
      [1008, 20],
      [1016, 40],
      [1024, 60],
      [1032, 80],
    ] as const) {
      firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY, timeStamp });
    }
    // Chrome on Android emits an effectively stationary sample right before `pointerup`.
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 80.5, timeStamp: trailingTime });
    firePointer.up(element, { ...timed, clientX: 0, clientY: 80.5, timeStamp: releaseTime });

    // The last moving sample (20px / 8ms) is kept, unless it is older than 80ms.
    const details = onRelease.mock.calls[0]?.[0];
    expect(details?.releaseVelocityY).toBeCloseTo(expectedVelocity, 4);
    expect(details?.releaseVelocityX).toBeCloseTo(0, 2);
  });

  it.each([
    {
      name: '60 Hz input',
      moves: [
        [1016, 14.4],
        [1032, 28.8],
        [1048, 43.2],
        [1064, 57.6],
      ],
      release: [1080, 57.6],
      expected: 0.9,
    },
    {
      name: '120 Hz input',
      moves: [
        [1008, 7.2],
        [1016, 14.4],
        [1024, 21.6],
        [1032, 28.8],
      ],
      release: [1040, 28.8],
      expected: 0.9,
    },
    {
      name: 'a slower final step',
      moves: [
        [1008, 10],
        [1016, 20],
        [1024, 30],
        [1032, 31],
      ],
      release: [1040, 31],
      expected: 0.125,
    },
    {
      name: 'a flick after a pause',
      moves: [
        [1008, 10],
        [1016, 20],
        [1216, 30],
        [1224, 40],
      ],
      release: [1232, 40],
      expected: 1.25,
    },
    {
      // 8px over 2ms is floored to 4ms.
      name: 'near-simultaneous samples',
      moves: [
        [1008, 10],
        [1010, 18],
      ],
      release: [1018, 18],
      expected: 2,
    },
  ])('measures release velocity from the latest movement ($name)', ({ moves, release, expected }) => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    for (const [timeStamp, clientY] of moves) {
      firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: clientY!, timeStamp: timeStamp! });
    }
    firePointer.up(element, { ...timed, clientX: 0, clientY: release[1]!, timeStamp: release[0]! });

    expect(onRelease.mock.calls[0]?.[0]?.releaseVelocityY).toBeCloseTo(expected, 4);
  });

  it('keeps release velocity when moves arrive sparsely', () => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 20, timeStamp: 1060 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 200, timeStamp: 1120 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 200.5, timeStamp: 1128 });
    firePointer.up(element, { ...timed, clientX: 0, clientY: 200.5, timeStamp: 1136 });

    // The last moving sample: 180px over 60ms.
    expect(onRelease.mock.calls[0]?.[0]?.releaseVelocityY).toBeCloseTo(3, 4);
  });

  it('measures release velocity from the first move rather than the press', () => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    // The first move arrives 200ms after the press and re-anchors the drag origin.
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 5, timeStamp: 1200 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 15, timeStamp: 1208 });
    firePointer.up(element, { ...timed, clientX: 0, clientY: 15, timeStamp: 1216 });

    // 10px over 8ms; measuring from the press would report 0.05.
    expect(onRelease.mock.calls[0]?.[0]?.releaseVelocityY).toBeCloseTo(1.25, 4);
  });

  it('decays release velocity while the pointer stays pinned', () => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 20, timeStamp: 1016 });
    // An off-screen mouse keeps emitting moves at the pinned coordinates.
    for (const timeStamp of [1032, 1048, 1064]) {
      firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 20, timeStamp });
    }
    firePointer.up(element, { ...timed, clientX: 0, clientY: 20, timeStamp: 1072 });

    // Repeated stationary samples clear the velocity from the last movement.
    expect(onRelease.mock.calls[0]?.[0]?.releaseVelocityY).toBe(0);
  });

  it('does not report a previous gesture velocity for a release without movement', () => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['down'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 40, timeStamp: 1008 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 120, timeStamp: 1016 });
    firePointer.up(element, { ...timed, clientX: 0, clientY: 120, timeStamp: 1024 });

    expect(onRelease.mock.calls[0]?.[0]?.releaseVelocityY).toBeGreaterThan(1);

    firePointer.down(element, { ...timed, pointerId: 2, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 5000 });
    firePointer.up(element, { ...timed, pointerId: 2, clientX: 0, clientY: 0, timeStamp: 5016 });

    expect(onRelease.mock.calls[1]?.[0]?.releaseVelocityY).toBe(0);
  });

  it('clamps short swipe durations when computing velocity', () => {
    const onRelease = vi.fn();
    const { element } = mountTracker({ directions: ['right'], onRelease });

    firePointer.down(element, { ...timed, button: 0, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1000 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 0, clientY: 0, timeStamp: 1005 });
    firePointer.move(element, { ...timed, buttons: 1, clientX: 30, clientY: 0, timeStamp: 1010 });
    firePointer.up(element, { ...timed, clientX: 30, clientY: 0, timeStamp: 1015 });

    const details = onRelease.mock.calls[0]?.[0];
    expect(details?.velocityX).toBeCloseTo(0.6, 2);
    expect(details?.velocityY).toBeCloseTo(0, 2);
  });

  // The port's style sink (M6 track 8b): the Drawer Popup's `style` belongs to dom's style writer, so the drawer module
  // hands the tracker a band of it. Setting the drag styles there and removing them at the release is Base UI's
  // snapshot and restore of the inline `transition` and `transform` (useSwipeDismiss.ts:214-247).
  it('port: a tracker given a style sink writes the drag styles through it and hands them back at the release', () => {
    const writes: string[] = [];
    const sink = {
      set: (property: string, value: string) => writes.push(`set ${property} ${value}`),
      remove: (property: string) => writes.push(`remove ${property}`),
    };
    const { element } = mountTracker({ directions: ['down'], style: sink });
    const inline = element.style.cssText;

    pointerDown(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 0 });
    pointerMove(element, { clientX: 0, clientY: 40 });
    expect(writes).toEqual([
      'set transition none',
      'set transform translate3d(0px,0px,0) scale(1)',
      'set --x 0px',
      'set --y 0px',
      'set transition none',
      'set transform translate3d(0px,40px,0) scale(1)',
      'set --x 0px',
      'set --y 40px',
    ]);

    writes.length = 0;
    pointerUp(element, { clientX: 0, clientY: 40 });
    expect(writes).toEqual(['remove transition', 'remove transform', 'set --x 0px', 'set --y 0px']);
    expect(element.style.cssText).toBe(inline);
  });

  // The port (the review of 2026-11-19): every module writes `style` through dom's writer, so a tracker writes its drag
  // styles only through the sink its module passes, and one created without a sink is refused.
  it('port: a tracker created without a style sink is refused', () => {
    const element = document.createElement('div');

    expect(() =>
      createSwipeTracker({ enabled: true, element: () => element, movementCssVars: { x: '--x', y: '--y' }, directions: ['down'] } as unknown as SwipeTrackerOptions),
    ).toThrow(/style sink/);
  });

  it('ignores pointer interactions that were default prevented', () => {
    const onSwipeStart = vi.fn();
    const { element: root } = mountTracker({ directions: ['down'], onSwipeStart }, { html: '<div data-testid="child"></div>' });
    const target = child(root, '[data-testid="child"]');
    target.addEventListener('pointerdown', (event) => event.preventDefault());

    pointerDown(target, { clientX: 0, clientY: 100 });
    pointerMove(target, { clientX: 0, clientY: 150 });

    expect(onSwipeStart).not.toHaveBeenCalled();
    expect(root.style.getPropertyValue('--y')).toBe('0px');
  });
});
