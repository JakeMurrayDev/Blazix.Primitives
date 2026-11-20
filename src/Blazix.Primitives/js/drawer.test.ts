// L3 (ADR 06 §1): the `drawer` component module in jsdom, against the fake twin. Slice 1 of M6 track 8b (issue 279):
// the Viewport handle's mouse and pen dismiss gesture without snap points (DRW-PTR-01…05, 07…10, 12…14, the
// Backdrop half of PTR-16), driven through the `swipe` tracker. Base UI's titles for these rows are browser titles
// (DrawerViewport.test.tsx, DrawerRoot.test.tsx) and run at L4 in tests/browser/drawer.spec.ts; these port titles pin
// the module's own contract: its writes, its reports and its `dismiss` decision point.
import { afterEach, describe, expect, it } from 'vitest';
import { attach, type DrawerOptions } from './drawer';
import { styleWriter } from './dom';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];
const elements: HTMLElement[] = [];
let pointTarget: Element | null = null;

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  for (const element of elements.splice(0)) {
    element.remove();
  }
  pointTarget = null;
  document.getSelection()?.removeAllRanges();
});

// jsdom has no hit testing: the element under the point is the target the test names.
Object.defineProperty(document, 'elementFromPoint', { value: () => pointTarget, configurable: true });

const defaults: DrawerOptions = {
  role: 'viewport',
  open: true,
  mounted: true,
  nested: false,
  nestedOpenDrawerCount: 0,
  swipeDirection: 'down',
  answeredDismiss: 0,
  popup: 0,
  backdrop: 1,
};

interface Mounted {
  viewport: HTMLElement;
  popup: HTMLElement;
  backdrop: HTMLElement;
  content: HTMLElement;
  fake: FakeTwin;
  handle: TwinHandle;
  update(options: Partial<DrawerOptions>): void;
}

function mount(options: Partial<DrawerOptions> = {}, synchronous = false, verdict: 'run' | 'stop' = 'run'): Mounted {
  const viewport = document.createElement('div');
  const backdrop = document.createElement('div');
  const popup = document.createElement('div');
  const content = document.createElement('div');
  content.setAttribute('data-drawer-content', '');
  popup.append(content);
  viewport.append(popup);
  document.body.append(backdrop, viewport);
  elements.push(backdrop, viewport);
  Object.defineProperty(popup, 'offsetHeight', { value: 200, configurable: true });
  Object.defineProperty(popup, 'offsetWidth', { value: 300, configurable: true });

  const fake = createFakeTwin({ synchronous, verdict });
  const handle = attach(viewport, JSON.stringify({ ...defaults, ...options }), fake.twin, [popup, backdrop]);
  handles.push(handle);
  pointTarget = popup;
  return {
    viewport,
    popup,
    backdrop,
    content,
    fake,
    handle,
    update: (next) => handle.update(JSON.stringify({ ...defaults, ...options, ...next }), [popup, backdrop]),
  };
}

let stamp = 1000;

function pointer(type: 'pointerdown' | 'pointermove' | 'pointerup', target: Element, init: PointerEventInit & { at?: number } = {}): void {
  const { at, ...rest } = init;
  const event = new PointerEvent(type, {
    bubbles: true,
    cancelable: true,
    button: 0,
    buttons: type === 'pointerup' ? 0 : 1,
    pointerId: 1,
    pointerType: 'mouse',
    ...rest,
  });
  stamp = at ?? stamp + 100;
  Object.defineProperty(event, 'timeStamp', { value: stamp });
  target.dispatchEvent(event);
}

/** A slow mouse drag along the y axis: press, a baseline move, the drag, the release. */
function drag(target: Element, toY: number): void {
  pointer('pointerdown', target, { clientX: 10, clientY: 10 });
  pointer('pointermove', target, { clientX: 10, clientY: 10 });
  pointer('pointermove', target, { clientX: 10, clientY: 10 + toY });
  pointer('pointerup', target, { clientX: 10, clientY: 10 + toY });
}

describe('drawer module: the Viewport dismiss gesture (mouse and pen)', () => {
  it('port: a mouse drag on the popup moves it through the component band and marks the popup and backdrop as swiping', () => {
    const { popup, backdrop, fake } = mount();

    pointer('pointerdown', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 50 });

    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('40px');
    expect(popup.style.getPropertyValue('transition')).toBe('none');
    expect(popup.hasAttribute('data-swiping')).toBe(true);
    expect(backdrop.hasAttribute('data-swiping')).toBe(true);
    expect(fake.reported('OnSwipingChange')).toEqual([{ swiping: true }]);

    // The drag's declarations sit in the popup's component band: clearing it leaves nothing behind.
    styleWriter(popup).layer('component').clear();
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('');
  });

  it('port: a release past half the popup height requests the close with reason swipe and marks the swipe dismissal', () => {
    const { popup, backdrop, fake } = mount();

    drag(popup, 120);

    expect(fake.reported('OnSwipingChange')).toEqual([{ swiping: true }, { swiping: false }]);
    expect(popup.hasAttribute('data-swiping')).toBe(false);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);
    expect(backdrop.hasAttribute('data-swipe-dismiss')).toBe(true);
    expect(backdrop.style.getPropertyValue('--drawer-swipe-progress')).toBe('0');
    const [request] = fake.reported<{ request: number; event: { type: string } }>('OnDismissRequest');
    expect(request).toMatchObject({ request: 1, event: { type: 'pointerup' } });
  });

  it('port: a release under the threshold snaps back and requests nothing', () => {
    const { popup, fake } = mount();

    drag(popup, 90);

    expect(fake.reported('OnDismissRequest')).toEqual([]);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('0px');
  });

  it('port: a fast release dismisses before the threshold', () => {
    const { popup, fake } = mount();

    pointer('pointerdown', popup, { clientX: 10, clientY: 10, at: 5000 });
    pointer('pointermove', popup, { clientX: 10, clientY: 10, at: 5010 });
    pointer('pointermove', popup, { clientX: 10, clientY: 50, at: 5040 });
    pointer('pointerup', popup, { clientX: 10, clientY: 50, at: 5050 });

    // 40 px over 50 ms (the velocity floor) is 0.8 px/ms, past 0.5.
    expect(fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: a dismissal that C# answers while the drawer stays open rolls back', () => {
    const { popup, backdrop, fake, update } = mount();

    drag(popup, 120);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);

    // An update that does not answer the request yet changes nothing.
    update({ open: true, answeredDismiss: 0 });
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);

    update({ open: true, answeredDismiss: 1 });
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(backdrop.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('0px');
    expect(fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: an accepted dismissal keeps the swipe dismissal marked', () => {
    const { popup, update } = mount();

    drag(popup, 120);
    update({ open: false, answeredDismiss: 1 });

    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);
  });

  it('port: on WebAssembly the dismissal asks the dismiss point, and a canceled one rolls back at once without a report', () => {
    const { popup, fake } = mount({}, true, 'stop');

    drag(popup, 120);

    expect(fake.decisions.map((decision) => decision.point)).toEqual(['dismiss']);
    expect(fake.decisions[0]!.event.type).toBe('pointerup');
    expect(fake.reported('OnDismissRequest')).toEqual([]);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('0px');
  });

  it('port: on WebAssembly an accepted dismissal is not reported again', () => {
    const { popup, fake } = mount({}, true, 'run');

    drag(popup, 120);

    expect(fake.decisions).toHaveLength(1);
    expect(fake.reported('OnDismissRequest')).toEqual([]);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);
  });

  it('port: no gesture starts while closed, while a nested drawer is open, from Drawer.Content, from a swipe-ignore target, or from a touch pointer', () => {
    const closed = mount({ open: false });
    drag(closed.popup, 120);
    expect(closed.fake.reports).toEqual([]);

    const nested = mount({ nestedOpenDrawerCount: 1 });
    drag(nested.popup, 120);
    expect(nested.fake.reports).toEqual([]);

    const content = mount();
    pointTarget = content.content;
    drag(content.content, 120);
    expect(content.fake.reports).toEqual([]);

    const ignored = mount();
    const ignore = document.createElement('div');
    ignore.setAttribute('data-base-ui-swipe-ignore', 'y');
    ignored.popup.append(ignore);
    pointTarget = ignore;
    drag(ignore, 120);
    expect(ignored.fake.reports).toEqual([]);

    const touch = mount();
    pointer('pointerdown', touch.popup, { clientX: 10, clientY: 10, pointerType: 'touch' });
    pointer('pointermove', touch.popup, { clientX: 10, clientY: 130, pointerType: 'touch' });
    pointer('pointerup', touch.popup, { clientX: 10, clientY: 130, pointerType: 'touch' });
    expect(touch.fake.reports).toEqual([]);
  });

  // ADR 04 Amendments 2026-11-18, design point 2 as revised 2026-11-19 (the opt-in absorber, chosen by the user
  // 2026-11-19): C# renders the popup's `--nested-drawers` in the commit path, so a re-render rewrites the `style`
  // attribute; the module opts the popup's writer in, and the drag's declarations survive the rewrite.
  it('port: a C# re-render of the popup style during a drag keeps the drag declarations and takes the rendered ones', async () => {
    const { popup } = mount();

    pointer('pointerdown', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 50 });
    popup.setAttribute('style', '--nested-drawers: 1');
    await new Promise<void>((resolve) => setTimeout(resolve, 0));

    expect(popup.style.getPropertyValue('--nested-drawers')).toBe('1');
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('40px');
    expect(popup.style.getPropertyValue('transition')).toBe('none');
  });

  it('port: a mouse swipe start clears a selection inside the popup and keeps one outside it', () => {
    const inside = mount();
    const text = document.createTextNode('selectable');
    inside.content.append(text);
    const outside = document.createElement('p');
    outside.textContent = 'outside';
    document.body.append(outside);
    elements.push(outside);
    const selection = document.getSelection()!;

    selection.setBaseAndExtent(outside.firstChild!, 0, outside.firstChild!, 3);
    pointer('pointerdown', inside.popup, { clientX: 10, clientY: 10 });
    expect(selection.isCollapsed).toBe(false);
    pointer('pointerup', inside.popup, { clientX: 10, clientY: 10 });

    selection.setBaseAndExtent(text, 0, text, 4);
    pointer('pointerdown', inside.popup, { clientX: 10, clientY: 10 });
    expect(selection.rangeCount).toBe(0);
  });
});

// Slice 2 (M6 track 8b, issue 279, 2026-11-19): the Viewport's touch path, DRW-PTR-06, PTR-19…24 and ALG-03.
type TouchPoint = { clientX: number; clientY: number };

function touch(type: 'touchstart' | 'touchmove' | 'touchend' | 'touchcancel', target: Element, points: TouchPoint[], cancelable = true): Event {
  const event = new Event(type, { bubbles: true, cancelable, composed: true });
  Object.defineProperty(event, 'touches', { value: type === 'touchend' || type === 'touchcancel' ? [] : points });
  Object.defineProperty(event, 'changedTouches', { value: points });
  stamp += 100;
  Object.defineProperty(event, 'timeStamp', { value: stamp });
  target.dispatchEvent(event);
  return event;
}

/** A slow touch drag along the y axis from a target: the start, a baseline move, the drag, the end. */
function touchDrag(target: Element, toY: number): Event[] {
  return [
    touch('touchstart', target, [{ clientX: 10, clientY: 10 }]),
    touch('touchmove', target, [{ clientX: 10, clientY: 10 }]),
    touch('touchmove', target, [{ clientX: 10, clientY: 10 + toY }]),
    touch('touchend', target, [{ clientX: 10, clientY: 10 + toY }]),
  ];
}

describe('drawer module: the Viewport touch path', () => {
  it('port: the document touchmove listener is non-passive in the capture phase, and only while open and mounted', () => {
    const added: [string, AddEventListenerOptions | boolean | undefined][] = [];
    const removed: string[] = [];
    const add = document.addEventListener.bind(document);
    const remove = document.removeEventListener.bind(document);
    document.addEventListener = ((type: string, listener: EventListener, opts?: AddEventListenerOptions | boolean) => {
      added.push([type, opts]);
      add(type, listener, opts);
    }) as typeof document.addEventListener;
    document.removeEventListener = ((type: string, listener: EventListener, opts?: EventListenerOptions | boolean) => {
      removed.push(type);
      remove(type, listener, opts);
    }) as typeof document.removeEventListener;
    try {
      const { update } = mount({ open: false });
      expect(added.filter(([type]) => type === 'touchmove')).toEqual([]);

      update({ open: true });
      expect(added.filter(([type]) => type === 'touchmove')).toEqual([['touchmove', { passive: false, capture: true }]]);

      update({ open: false });
      expect(removed.filter((type) => type === 'touchmove')).toEqual(['touchmove']);
    } finally {
      document.addEventListener = add;
      document.removeEventListener = remove;
    }
  });

  it('port: a touch drag on the popup prevents its moves, moves the popup and dismisses past the threshold', () => {
    const { popup, fake } = mount();

    const [, , move] = touchDrag(popup, 120);

    expect(move!.defaultPrevented).toBe(true);
    expect(fake.reported('OnSwipingChange')).toEqual([{ swiping: true }, { swiping: false }]);
    expect(fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: a touch from Drawer.Content or an interactive element starts a swipe, as the ignore selector does not apply to touch', () => {
    const fromContent = mount();
    pointTarget = fromContent.content;
    touchDrag(fromContent.content, 120);
    expect(fromContent.fake.reported('OnDismissRequest')).toHaveLength(1);

    const fromButton = mount();
    const button = document.createElement('button');
    fromButton.popup.append(button);
    pointTarget = button;
    touchDrag(button, 120);
    expect(fromButton.fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: a touch right after a pen press, a touch on a range input, and a touch with no touches start nothing, and the next plain touch swipes', () => {
    const pen = mount();
    pointer('pointerdown', pen.popup, { clientX: 10, clientY: 10, pointerType: 'pen' });
    touchDrag(pen.popup, 120);
    expect(pen.fake.reported('OnDismissRequest')).toEqual([]);
    touchDrag(pen.popup, 120);
    expect(pen.fake.reported('OnDismissRequest')).toHaveLength(1);

    const range = mount();
    const input = document.createElement('input');
    input.type = 'range';
    range.popup.append(input);
    pointTarget = input;
    const [, , move] = touchDrag(input, 120);
    expect(move!.defaultPrevented).toBe(false);
    expect(range.fake.reported('OnDismissRequest')).toEqual([]);
    pointTarget = range.popup;
    touchDrag(range.popup, 120);
    expect(range.fake.reported('OnDismissRequest')).toHaveLength(1);

    const empty = mount();
    touch('touchstart', empty.popup, []);
    touch('touchmove', empty.popup, [{ clientX: 10, clientY: 130 }]);
    expect(empty.fake.reported('OnSwipingChange')).toEqual([]);
    touchDrag(empty.popup, 120);
    expect(empty.fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: data-base-ui-swipe-ignore stops a touch swipe unless its value is the cross axis, and the legacy attribute does nothing', () => {
    const ignored = mount();
    const all = document.createElement('div');
    all.setAttribute('data-base-ui-swipe-ignore', '');
    ignored.popup.append(all);
    pointTarget = all;
    touchDrag(all, 120);
    expect(ignored.fake.reported('OnDismissRequest')).toEqual([]);

    const sameAxis = mount();
    const y = document.createElement('div');
    y.setAttribute('data-base-ui-swipe-ignore', 'y');
    sameAxis.popup.append(y);
    pointTarget = y;
    touchDrag(y, 120);
    expect(sameAxis.fake.reported('OnDismissRequest')).toEqual([]);

    // `x` on a down drawer is the cross axis: arbitrated, and a vertical drag wins it. The sub-slop first move is left to
    // the browser, so the tracker's baseline is the first move past the slop.
    const crossAxis = mount();
    const x = document.createElement('div');
    x.setAttribute('data-base-ui-swipe-ignore', 'x');
    crossAxis.popup.append(x);
    pointTarget = x;
    touch('touchstart', x, [{ clientX: 10, clientY: 10 }]);
    touch('touchmove', x, [{ clientX: 10, clientY: 10 }]);
    touch('touchmove', x, [{ clientX: 10, clientY: 20 }]);
    touch('touchmove', x, [{ clientX: 10, clientY: 140 }]);
    touch('touchend', x, [{ clientX: 10, clientY: 140 }]);
    expect(crossAxis.fake.reported('OnDismissRequest')).toHaveLength(1);

    const legacy = mount();
    const old = document.createElement('div');
    old.setAttribute('data-swipe-ignore', '');
    legacy.popup.append(old);
    pointTarget = old;
    touchDrag(old, 120);
    expect(legacy.fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: over a cross-axis target a sub-slop move is left alone, a cross-axis win yields the gesture, and a drawer-axis win claims it', () => {
    const arbitrate = (moves: TouchPoint[], cancelable = true) => {
      const mounted = mount();
      const x = document.createElement('div');
      x.setAttribute('data-base-ui-swipe-ignore', 'x');
      mounted.popup.append(x);
      pointTarget = x;
      touch('touchstart', x, [{ clientX: 10, clientY: 10 }]);
      const events = moves.map((point) => touch('touchmove', x, [point], cancelable));
      touch('touchend', x, [moves[moves.length - 1]!]);
      return { events, fake: mounted.fake };
    };

    const subSlop = arbitrate([{ clientX: 12, clientY: 13 }]);
    expect(subSlop.events[0]!.defaultPrevented).toBe(false);

    const crossWins = arbitrate([{ clientX: 20, clientY: 12 }, { clientX: 20, clientY: 150 }]);
    expect(crossWins.events.map((event) => event.defaultPrevented)).toEqual([false, false]);
    expect(crossWins.fake.reported('OnSwipingChange')).toEqual([]);

    const drawerWins = arbitrate([{ clientX: 11, clientY: 20 }, { clientX: 11, clientY: 150 }]);
    expect(drawerWins.events.map((event) => event.defaultPrevented)).toEqual([true, true]);
    expect(drawerWins.fake.reported('OnDismissRequest')).toHaveLength(1);

    const nonCancelable = arbitrate([{ clientX: 10, clientY: 20 }, { clientX: 10, clientY: 150 }], false);
    expect(nonCancelable.fake.reported('OnSwipingChange')).toEqual([]);
  });

  it('port: a scroll container away from its dismiss edge keeps its scroll, and one at the edge hands the drag to the swipe', () => {
    const scrolled = (scrollTop: number) => {
      const mounted = mount();
      const scroller = document.createElement('div');
      scroller.style.overflowY = 'auto';
      Object.defineProperty(scroller, 'scrollHeight', { value: 400, configurable: true });
      Object.defineProperty(scroller, 'clientHeight', { value: 100, configurable: true });
      scroller.scrollTop = scrollTop;
      Object.defineProperty(scroller, 'scrollTop', { value: scrollTop, configurable: true });
      mounted.popup.append(scroller);
      pointTarget = scroller;
      return { ...mounted, events: touchDrag(scroller, 120), scroller };
    };

    const middle = scrolled(50);
    expect(middle.events[2]!.defaultPrevented).toBe(false);
    expect(middle.fake.reported('OnDismissRequest')).toEqual([]);

    const top = scrolled(0);
    expect(top.events[2]!.defaultPrevented).toBe(true);
    expect(top.fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  // DRW-PTR-21, ALG-03 (DrawerViewport.test.tsx:2445-2511), ported here rather than at L4 (2026-11-20): Chromium makes every
  // touchmove after the first non-cancelable once its native scroll starts, so one real gesture never reaches the edge
  // with a cancelable move (tests/browser/drawer.spec.ts's note on the title).
  it('starts swipe-to-dismiss after a scrollable container reaches the dismiss edge', () => {
    const { popup, backdrop } = mount();
    const scroller = document.createElement('div');
    scroller.style.overflowY = 'auto';
    Object.defineProperty(scroller, 'scrollHeight', { value: 120, configurable: true });
    Object.defineProperty(scroller, 'clientHeight', { value: 40, configurable: true });
    scroller.scrollTop = 30;
    popup.append(scroller);
    pointTarget = scroller;

    touch('touchstart', scroller, [{ clientX: 0, clientY: 30 }]);
    const first = touch('touchmove', scroller, [{ clientX: 0, clientY: 40 }]);
    expect(first.defaultPrevented).toBe(false);
    expect(backdrop.hasAttribute('data-swiping')).toBe(false);

    scroller.scrollTop = 0;
    const second = touch('touchmove', scroller, [{ clientX: 0, clientY: 50 }]);
    expect(second.defaultPrevented).toBe(true);
    expect(backdrop.getAttribute('data-swiping')).toBe('');
  });

  it('port: a touch that starts in the popup may start after leaving it, and one that starts outside moves nothing until it enters', () => {
    // The start waits for the drawer axis over a cross-axis target, and by then the touch has left the popup.
    const inside = mount();
    const x = document.createElement('div');
    x.setAttribute('data-base-ui-swipe-ignore', 'x');
    inside.popup.append(x);
    pointTarget = x;
    touch('touchstart', x, [{ clientX: 10, clientY: 10 }]);
    pointTarget = inside.viewport;
    touch('touchmove', x, [{ clientX: 10, clientY: 20 }]);
    touch('touchmove', x, [{ clientX: 10, clientY: 140 }]);
    touch('touchend', x, [{ clientX: 10, clientY: 140 }]);
    expect(inside.fake.reported('OnDismissRequest')).toHaveLength(1);

    // Base UI's "does not jump": the gesture starts where the touch enters the popup, measured from there.
    const outside = mount();
    pointTarget = outside.viewport;
    touch('touchstart', outside.viewport, [{ clientX: 10, clientY: 10 }]);
    touch('touchmove', outside.viewport, [{ clientX: 10, clientY: 40 }]);
    expect(outside.fake.reported('OnSwipingChange')).toEqual([]);
    pointTarget = outside.popup;
    touch('touchmove', outside.viewport, [{ clientX: 10, clientY: 60 }]);
    touch('touchmove', outside.viewport, [{ clientX: 10, clientY: 80 }]);
    expect(outside.fake.reported('OnSwipingChange')).toEqual([{ swiping: true }]);
    expect(outside.popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('20px');
  });

  it('port: a touch with an expanded selection in the popup, or a two-finger move, is left to the browser, and a plain one-finger drag is not', () => {
    const selected = mount();
    const text = document.createTextNode('selectable');
    selected.content.append(text);
    document.getSelection()!.setBaseAndExtent(text, 0, text, 4);
    const [, , move] = touchDrag(selected.popup, 120);
    expect(move!.defaultPrevented).toBe(false);
    expect(selected.fake.reported('OnDismissRequest')).toEqual([]);
    document.getSelection()!.removeAllRanges();
    const [, , plain] = touchDrag(selected.popup, 120);
    expect(plain!.defaultPrevented).toBe(true);
    expect(selected.fake.reported('OnDismissRequest')).toHaveLength(1);

    const pinch = mount();
    touch('touchstart', pinch.popup, [{ clientX: 10, clientY: 10 }]);
    const twoFingers = touch('touchmove', pinch.popup, [{ clientX: 10, clientY: 60 }, { clientX: 50, clientY: 60 }]);
    expect(twoFingers.defaultPrevented).toBe(false);
    const oneFinger = touch('touchmove', pinch.popup, [{ clientX: 10, clientY: 80 }]);
    expect(oneFinger.defaultPrevented).toBe(true);
  });

  it('port: a touchend clears the touch state, so the next touch inherits no eligibility', () => {
    const { popup, fake } = mount();
    touch('touchstart', popup, [{ clientX: 10, clientY: 10 }]);
    const claimed = touch('touchmove', popup, [{ clientX: 10, clientY: 30 }]);
    expect(claimed.defaultPrevented).toBe(true);
    touch('touchend', popup, [{ clientX: 10, clientY: 30 }]);

    // A move with no touchstart of its own is ignored.
    const stray = touch('touchmove', popup, [{ clientX: 10, clientY: 130 }]);
    expect(stray.defaultPrevented).toBe(false);
    expect(fake.reported('OnDismissRequest')).toEqual([]);
  });
});
