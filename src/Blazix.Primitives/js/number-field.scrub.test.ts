// L3 for the `number-field` module's scrub role (issue 277, 6b; ADR 04 Amendments 2026-11-18, the `number-field` scope):
// the scrub area's pointerdown, pointer lock, the window move accumulator, the release and its synthetic click, the touch
// scroll suppression and the virtual cursor's transform in the `'component'` band. getViewportRect.test.ts's titles
// verbatim; NUM-PTR-11, 12, 15, 16, 18 and NUM-EDGE-15 at L3 as their rows say; NumberFieldScrubArea.gecko.test.tsx's
// title over a stubbed `platform` (Chromium cannot run Gecko's branch; the probe of 2026-11-19, design point 10 (a)).
// Moves are dispatched as Base UI's tests dispatch them (NumberFieldScrubArea.test.tsx:14-36): on the scrub area, reaching
// the window's capture listener.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  attach,
  getViewportRect,
  type NumberFieldOptions,
  type NumberFieldScrubOptions,
  type NumberFieldScrubStartReport,
  type NumberFieldScrubStepReport,
} from './number-field';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin, type FakeTwinOptions } from './test-support/fake-twin';

const platformState = vi.hoisted(() => ({ gecko: false, webkit: false }));

vi.mock('./platform', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./platform')>();
  return {
    ...actual,
    platform: {
      ...actual.platform,
      engine: {
        ...actual.platform.engine,
        get gecko() {
          return platformState.gecko;
        },
        get webkit() {
          return platformState.webkit;
        },
      },
    },
  };
});

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  platformState.gecko = false;
  platformState.webkit = false;
  vi.useRealTimers();
  vi.restoreAllMocks();
  delete (document.body as { requestPointerLock?: unknown }).requestPointerLock;
  delete (document as { exitPointerLock?: unknown }).exitPointerLock;
  delete (window as { visualViewport?: unknown }).visualViewport;
});

// jsdom has no visualViewport: an own property stands in for the browser's.
function setVisualViewport(read: () => Partial<VisualViewport> | null): void {
  Object.defineProperty(window, 'visualViewport', { configurable: true, get: read });
}

let position = { clientX: 0, clientY: 0 };

function pointerDown(element: Element, init: PointerEventInit = {}): PointerEvent {
  const box = element.getBoundingClientRect();
  position = { clientX: box.left + box.width / 2, clientY: box.top + box.height / 2 };
  const event = new PointerEvent('pointerdown', { bubbles: true, cancelable: true, ...position, ...init });
  element.dispatchEvent(event);
  return event;
}

function pointerMove(element: EventTarget, { movementX = 0, movementY = 0, ...init }: PointerEventInit = {}): PointerEvent {
  position = { clientX: position.clientX + movementX, clientY: position.clientY + movementY };
  const event = new PointerEvent('pointermove', { bubbles: true, cancelable: true, ...position, movementX, movementY, ...init });
  element.dispatchEvent(event);
  return event;
}

function pointerUp(): PointerEvent {
  const event = new PointerEvent('pointerup', { bubbles: true, cancelable: true });
  window.dispatchEvent(event);
  return event;
}

interface Scrub {
  input: HTMLInputElement;
  area: HTMLSpanElement;
  handle: TwinHandle;
  fake: FakeTwin;
  steps: () => number[];
}

function mountScrub(options: Partial<NumberFieldScrubOptions> = {}, twin: FakeTwinOptions = {}): Scrub {
  const input = document.createElement('input');
  input.type = 'text';
  const area = document.createElement('span');
  document.body.append(input, area);
  const fake = createFakeTwin(twin);
  const json: NumberFieldScrubOptions = { part: 'scrub', ...options };
  const handle = attach(area, JSON.stringify(json), fake.twin, [input]);
  handles.push(handle);
  const steps = () =>
    fake.reports.filter((r) => r.method === 'OnScrubStep').map((r) => (r.payload as NumberFieldScrubStepReport).delta);
  return { input, area, handle, fake, steps };
}

function mountCursor(area: Element, style = 'position: fixed; top: 0px; left: 0px; pointer-events: none'): { cursor: HTMLSpanElement; handle: TwinHandle } {
  const cursor = document.createElement('span');
  cursor.setAttribute('style', style);
  document.body.append(cursor);
  const json: NumberFieldScrubOptions = { part: 'cursor', style };
  const handle = attach(cursor, JSON.stringify(json), createFakeTwin().twin, [area]);
  handles.push(handle);
  return { cursor, handle };
}

function stubLock(result: 'grant' | 'deny' | 'throw' = 'grant'): ReturnType<typeof vi.fn> {
  const request = vi.fn(() => {
    if (result === 'throw') {
      throw new Error('User denied pointer lock');
    }
    return result === 'grant' ? Promise.resolve() : Promise.reject(new Error('WrongDocumentError'));
  });
  (document.body as { requestPointerLock?: unknown }).requestPointerLock = request;
  (document as { exitPointerLock?: unknown }).exitPointerLock = vi.fn();
  return request;
}

describe('getViewportRect', () => {
  let element: HTMLElement;

  beforeEach(() => {
    element = document.createElement('div');
    element.style.cssText = 'position:fixed;left:100px;top:100px;width:20px;height:20px';
    document.body.appendChild(element);
    // jsdom does no layout: the rect the browser would measure for the styles above.
    vi.spyOn(element, 'getBoundingClientRect').mockReturnValue(new DOMRect(100, 100, 20, 20));
  });

  it('pads the element rect by half the teleport distance', () => {
    const rect = element.getBoundingClientRect();

    expect(getViewportRect(100, element)).toEqual({
      left: rect.left - 50,
      top: rect.top - 50,
      right: rect.right + 50,
      bottom: rect.bottom + 50,
    });
  });

  it('collapses to the element rect for a zero teleport distance', () => {
    const rect = element.getBoundingClientRect();

    expect(getViewportRect(0, element)).toEqual({
      left: rect.left,
      top: rect.top,
      right: rect.right,
      bottom: rect.bottom,
    });
  });

  it('uses the visual viewport when no teleport distance is set', () => {
    // jsdom has no visualViewport; a pinch-zoomed one, offset from the layout viewport and smaller than it.
    setVisualViewport(() => ({ offsetLeft: 7, offsetTop: 11, width: 300, height: 400, scale: 2 }));

    expect(getViewportRect(undefined, element)).toEqual({ left: 7, top: 11, right: 307, bottom: 411 });
  });

  it('falls back to the document element without a visual viewport', () => {
    expect(window.visualViewport).toBe(undefined);

    expect(getViewportRect(undefined, element)).toEqual({
      left: 0,
      top: 0,
      right: document.documentElement.clientWidth,
      bottom: document.documentElement.clientHeight,
    });
  });
});

describe('the scrub role', () => {
  it('port: starts a scrub on a primary pointerdown and requests pointer lock on the body (NUM-PTR-10)', async () => {
    const request = stubLock('grant');
    const { area, fake } = mountScrub();

    pointerDown(area, { pointerType: 'mouse' });
    await Promise.resolve();

    expect(request).toHaveBeenCalledTimes(1);
    const start = fake.reports.find((r) => r.method === 'OnScrubStart')?.payload as NumberFieldScrubStartReport;
    expect(start.cursor).toBe(true);
    expect(fake.reports.some((r) => r.method === 'OnScrubLock')).toBe(false);
  });

  it('port: a mouse press prevents its default and focuses the input with the caret at the end (NUM-FOC-04)', () => {
    stubLock();
    const { area, input } = mountScrub();
    input.value = '1234';

    const event = pointerDown(area, { pointerType: 'mouse' });

    expect(event.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(input);
    expect(input.selectionStart).toBe(4);
  });

  it('port: a touch press neither focuses nor locks, and reports no virtual cursor (NUM-FOC-04, NUM-STR-06)', () => {
    const request = stubLock();
    const { area, input, fake } = mountScrub();

    const event = pointerDown(area, { pointerType: 'touch' });

    expect(event.defaultPrevented).toBe(false);
    expect(document.activeElement).not.toBe(input);
    expect(request).not.toHaveBeenCalled();
    expect((fake.reports[0]!.payload as NumberFieldScrubStartReport).cursor).toBe(false);
  });

  it('port: a denied lock is reported once and the scrub goes on (NUM-EDGE-09)', async () => {
    stubLock('deny');
    const { area, fake, steps } = mountScrub();

    pointerDown(area);
    await new Promise((resolve) => setTimeout(resolve, 0));
    pointerMove(area, { movementX: 4 });

    expect(fake.reports.filter((r) => r.method === 'OnScrubLock').map((r) => r.payload)).toEqual([{ denied: true }]);
    expect(steps()).toEqual([4]);
  });

  it('port: a thrown lock request is a denial too (NUM-EDGE-09)', async () => {
    stubLock('throw');
    const { area, fake } = mountScrub();

    pointerDown(area);
    await Promise.resolve();

    expect(fake.reports.filter((r) => r.method === 'OnScrubLock').map((r) => r.payload)).toEqual([{ denied: true }]);
  });

  it('port: WebKit never requests pointer lock and reports no virtual cursor (NUM-EDGE-10)', () => {
    platformState.webkit = true;
    const request = stubLock();
    const { area, fake } = mountScrub();

    pointerDown(area, { pointerType: 'mouse' });

    expect(request).not.toHaveBeenCalled();
    expect((fake.reports[0]!.payload as NumberFieldScrubStartReport).cursor).toBe(false);
  });

  it('port: a pointerdown that is not primary, prevented, or on an inactive field starts nothing (NUM-PTR-19)', () => {
    const request = stubLock();
    const { area, fake, handle } = mountScrub();

    pointerDown(area, { button: 1 });
    const prevented = new PointerEvent('pointerdown', { bubbles: true, cancelable: true });
    prevented.preventDefault();
    area.dispatchEvent(prevented);
    handle.update(JSON.stringify({ part: 'scrub', readOnly: true } satisfies NumberFieldScrubOptions));
    pointerDown(area);
    handle.update(JSON.stringify({ part: 'scrub', disabled: true } satisfies NumberFieldScrubOptions));
    pointerDown(area);

    expect(fake.reports).toEqual([]);
    expect(request).not.toHaveBeenCalled();

    // The same press on an enabled, editable field starts the scrub.
    handle.update(JSON.stringify({ part: 'scrub' } satisfies NumberFieldScrubOptions));
    pointerDown(area);
    expect(fake.reports.map((r) => r.method)).toEqual(['OnScrubStart']);
  });

  it('port: the scrub-start decision point stops the scrub on a C# veto (design point 1 (a), fallback start)', () => {
    stubLock();
    const { area, fake } = mountScrub({}, { synchronous: true, verdict: 'stop' });

    pointerDown(area, { pointerType: 'mouse' });

    expect(fake.decisions.map((d) => d.point)).toEqual(['scrub-start']);
    expect(fake.reports).toEqual([]);
  });

  it('port: the move accumulator steps by the move that crosses pixelSensitivity, with that move\'s delta (NUM-PTR-11)', () => {
    stubLock();
    const { area, steps } = mountScrub({ pixelSensitivity: 5 });

    pointerDown(area);
    pointerMove(area, { movementX: -2 });
    pointerMove(area, { movementX: 2 });
    for (let i = 0; i < 4; i++) {
      pointerMove(area, { movementX: 1 });
    }
    expect(steps()).toEqual([]);

    pointerMove(area, { movementX: 1 });
    pointerMove(area, { movementX: 5 });
    pointerMove(area, { movementX: -4 });
    pointerMove(area, { movementX: -1 });

    expect(steps()).toEqual([1, 5, -1]);
  });

  it('port: a right-to-left document scrubs in the pointer\'s direction, not the reading direction (NUM-PTR-20, NUM-EDGE-18)', () => {
    stubLock();
    document.documentElement.dir = 'rtl';
    try {
      const { area, steps } = mountScrub();
      area.dir = 'rtl';

      pointerDown(area);
      pointerMove(area, { movementX: 4 });
      pointerMove(area, { movementX: -6 });

      expect(steps()).toEqual([4, -6]);
    } finally {
      document.documentElement.removeAttribute('dir');
    }
  });

  it('port: vertical scrubs step on movementY, upward increasing, and ignore the other axis (NUM-PTR-11)', () => {
    stubLock();
    const { area, steps } = mountScrub({ vertical: true });

    pointerDown(area);
    pointerMove(area, { movementX: 10 });
    pointerMove(area, { movementY: -10 });
    pointerMove(area, { movementY: 4 });

    expect(steps()).toEqual([10, -4]);
  });

  it('port: a zero-distance move with sensitivity 0 reports no step (NUM-PTR-11)', () => {
    stubLock();
    const { area, steps } = mountScrub({ pixelSensitivity: 0 });

    pointerDown(area);
    pointerMove(area, { movementX: 0 });
    expect(steps()).toEqual([]);

    pointerMove(area, { movementX: 1 });
    expect(steps()).toEqual([1]);
  });

  it('port: every processed move prevents its default, and moves without a pointer down are ignored (NUM-PTR-12)', () => {
    stubLock();
    const { area, steps } = mountScrub();

    const before = pointerMove(window, { movementX: 10 });
    pointerDown(area);
    const during = pointerMove(area, { movementX: 10 });

    expect(before.defaultPrevented).toBe(false);
    expect(during.defaultPrevented).toBe(true);
    expect(steps()).toEqual([10]);
  });

  it('port: a step report carries the input\'s next revision (ADR 04 Amendments 2026-11-18, decision 6 (a))', () => {
    stubLock();
    const input = document.createElement('input');
    const area = document.createElement('span');
    document.body.append(input, area);
    const inputFake = createFakeTwin();
    const inputOptions: NumberFieldOptions = { value: '0', revision: 0 };
    const inputHandle = attach(input, JSON.stringify(inputOptions), inputFake.twin);
    handles.push(inputHandle);
    const fake = createFakeTwin();
    handles.push(attach(area, JSON.stringify({ part: 'scrub' } satisfies NumberFieldScrubOptions), fake.twin, [input]));

    pointerDown(area);
    pointerMove(area, { movementX: 4 });
    pointerMove(area, { movementX: 4 });

    expect(fake.reports.filter((r) => r.method === 'OnScrubStep').map((r) => (r.payload as NumberFieldScrubStepReport).revision)).toEqual([1, 2]);
    // An answer to the first step arriving after the second report is older than the input's revision: not applied.
    inputHandle.update(JSON.stringify({ value: '4', revision: 1 } satisfies NumberFieldOptions));
    expect(input.value).toBe('0');
    inputHandle.update(JSON.stringify({ value: '8', revision: 2 } satisfies NumberFieldOptions));
    expect(input.value).toBe('8');
  });

  it('port: the window listeners exist only while the pointer is down, in the capture phase (NUM-PTR-18)', () => {
    stubLock();
    const add = vi.spyOn(window, 'addEventListener');
    const remove = vi.spyOn(window, 'removeEventListener');
    const { area } = mountScrub();

    expect(add.mock.calls.filter(([type]) => type === 'pointermove' || type === 'pointerup')).toEqual([]);
    pointerDown(area);
    const move = add.mock.calls.find(([type]) => type === 'pointermove');
    const up = add.mock.calls.find(([type]) => type === 'pointerup');
    expect(move?.[2]).toBe(true);
    expect(up?.[2]).toBe(true);

    pointerUp();

    expect(remove).toHaveBeenCalledWith('pointermove', move?.[1], true);
    expect(remove).toHaveBeenCalledWith('pointerup', up?.[1], true);
  });

  it('port: the accumulator restarts at 0 with every scrub (NUM-PTR-18)', () => {
    stubLock();
    const { area, steps } = mountScrub({ pixelSensitivity: 5 });

    pointerDown(area);
    pointerMove(area, { movementX: 4 });
    pointerUp();
    pointerDown(area);
    pointerMove(area, { movementX: 4 });
    expect(steps()).toEqual([]);

    pointerMove(area, { movementX: 1 });
    expect(steps()).toEqual([1]);
  });

  it('port: the release exits pointer lock and reports the end once (NUM-PTR-13)', () => {
    stubLock();
    const { area, fake } = mountScrub();

    pointerDown(area);
    pointerMove(area, { movementX: 4 });
    pointerUp();
    pointerUp();

    expect(document.exitPointerLock).toHaveBeenCalledTimes(1);
    expect(fake.reports.filter((r) => r.method === 'OnScrubEnd')).toHaveLength(1);
  });

  it('delays releasing the pointer lock so soft clicks are not swallowed', () => {
    platformState.gecko = true;
    vi.useFakeTimers();
    stubLock();
    const { area, fake } = mountScrub();

    pointerDown(area);
    pointerMove(area, { movementX: 10 });
    pointerUp();

    // Firefox needs the pointer lock to outlive the release, so the scrub is still open here.
    expect(fake.reports.some((r) => r.method === 'OnScrubEnd')).toBe(false);
    expect(document.exitPointerLock).not.toHaveBeenCalled();

    vi.advanceTimersByTime(20);

    expect(fake.reports.filter((r) => r.method === 'OnScrubEnd')).toHaveLength(1);
    expect(document.exitPointerLock).toHaveBeenCalledTimes(1);
  });

  it('port: a tap with no move dispatches one synthetic click on the pressed target unless a native click arrived (NUM-PTR-14)', () => {
    vi.useFakeTimers();
    stubLock();
    const { area } = mountScrub();
    const label = document.createElement('label');
    area.append(label);
    const clicks: EventTarget[] = [];
    area.addEventListener('click', (event) => clicks.push(event.target!));

    pointerDown(label);
    pointerUp();
    vi.advanceTimersByTime(0);
    expect(clicks).toEqual([label]);

    pointerDown(label);
    pointerUp();
    label.click();
    vi.advanceTimersByTime(0);
    expect(clicks).toEqual([label, label]);

    pointerDown(label);
    pointerMove(area, { movementX: 10 });
    pointerUp();
    vi.advanceTimersByTime(0);
    expect(clicks).toEqual([label, label]);
  });

  it('blocks scrolling for a single-touch scrub', () => {
    const { area } = mountScrub();

    const event = new TouchEvent('touchstart', { bubbles: true, cancelable: true, touches: [{} as Touch] });
    const notCanceled = area.dispatchEvent(event);

    expect(notCanceled).toBe(false);
  });

  it('leaves multi-touch gestures such as pinch-zoom to the browser', () => {
    const { area } = mountScrub();
    // The listener is live: a single touch is canceled (the red check's positive half).
    expect(area.dispatchEvent(new TouchEvent('touchstart', { bubbles: true, cancelable: true, touches: [{} as Touch] }))).toBe(false);

    const event = new TouchEvent('touchstart', { bubbles: true, cancelable: true, touches: [{} as Touch, {} as Touch] });
    const notCanceled = area.dispatchEvent(event);

    expect(notCanceled).toBe(true);
  });

  it('port: touch scroll suppression is off while the field is disabled or read-only (NUM-PTR-15, NUM-PTR-19)', () => {
    const { area, handle } = mountScrub({ readOnly: true });
    const touch = () => area.dispatchEvent(new TouchEvent('touchstart', { bubbles: true, cancelable: true, touches: [{} as Touch] }));

    expect(touch()).toBe(true);
    handle.update(JSON.stringify({ part: 'scrub', disabled: true } satisfies NumberFieldScrubOptions));
    expect(touch()).toBe(true);
    handle.update(JSON.stringify({ part: 'scrub' } satisfies NumberFieldScrubOptions));
    expect(touch()).toBe(false);
  });

  it('port: a handle re-attached mid-scrub without a release ignores a bare move (NUM-EDGE-15, NUM-PTR-12)', () => {
    stubLock();
    const { area, handle, fake, input } = mountScrub();

    pointerDown(area);
    pointerMove(area, { movementX: 10 });
    handle.dispose();
    const again = attach(area, JSON.stringify({ part: 'scrub' } satisfies NumberFieldScrubOptions), fake.twin, [input]);
    handles.push(again);
    pointerMove(window, { movementX: 10 });

    expect(fake.reports.filter((r) => r.method === 'OnScrubStep')).toHaveLength(1);
    expect(fake.reports.some((r) => r.method === 'OnScrubEnd')).toBe(false);
  });

  it('port: a disposal mid-scrub exits pointer lock without reporting an end (NUM-PTR-17)', () => {
    stubLock();
    const { area, handle, fake } = mountScrub();

    pointerDown(area);
    handle.dispose();

    expect(document.exitPointerLock).toHaveBeenCalledTimes(1);
    expect(fake.reports.some((r) => r.method === 'OnScrubEnd')).toBe(false);
  });
});

describe('the virtual cursor', () => {
  it('port: the cursor starts centered on the press and wraps around the teleport bounds (NUM-PTR-16, NUM-DOM-13)', () => {
    stubLock();
    const { area } = mountScrub({ teleportDistance: 100 });
    vi.spyOn(area, 'getBoundingClientRect').mockReturnValue(new DOMRect(100, 100, 20, 20));

    pointerDown(area);
    const { cursor } = mountCursor(area);
    expect(cursor.style.transform).toBe('translate3d(110px,110px,0) scale(1)');

    // Past the right bound (120 + 100 / 2): the cursor reappears at the left one.
    pointerMove(area, { movementX: 5000 });
    expect(cursor.style.transform).toContain('translate3d(50px,');

    // And symmetrically past the left bound (100 - 100 / 2).
    pointerMove(area, { movementX: -5000 });
    expect(cursor.style.transform).toContain('translate3d(170px,');
  });

  it('port: a cursor attached before it is laid out is centred again by its size once it is (NUM-PTR-16, design point 8 (a))', () => {
    stubLock();
    const observers: Array<() => void> = [];
    vi.stubGlobal('ResizeObserver', class {
      constructor(private readonly callback: () => void) {}
      observe() {
        observers.push(this.callback);
      }
      disconnect() {}
    });
    const { area } = mountScrub();
    vi.spyOn(area, 'getBoundingClientRect').mockReturnValue(new DOMRect(100, 100, 20, 20));

    pointerDown(area);
    const { cursor } = mountCursor(area);
    expect(cursor.style.transform).toBe('translate3d(110px,110px,0) scale(1)');

    // The portal moved it: laid out at 10 by 6.
    vi.spyOn(cursor, 'getClientRects').mockReturnValue([new DOMRect(0, 0, 10, 6)] as unknown as DOMRectList);
    vi.spyOn(cursor, 'offsetWidth', 'get').mockReturnValue(10);
    vi.spyOn(cursor, 'offsetHeight', 'get').mockReturnValue(6);
    observers.forEach((callback) => callback());

    expect(cursor.style.transform).toBe('translate3d(105px,107px,0) scale(1)');
    vi.unstubAllGlobals();
  });

  it('port: the transform inverts the visual viewport scale (NUM-DOM-13)', () => {
    stubLock();
    let scale = 2;
    setVisualViewport(() => ({ offsetLeft: 0, offsetTop: 0, width: 1000, height: 1000, scale }));
    const { area } = mountScrub();

    pointerDown(area);
    const { cursor } = mountCursor(area);
    expect(cursor.style.transform).toContain('scale(0.5)');

    scale = 4;
    pointerMove(area, { movementX: 4 });
    expect(cursor.style.transform).toContain('scale(0.25)');
  });

  it('port: the cursor keeps the consumer\'s style under the transform band and clears it on disposal (NUM-DOM-13)', () => {
    stubLock();
    const { area } = mountScrub();

    pointerDown(area);
    const { cursor, handle } = mountCursor(area, 'position: fixed; top: 0px; left: 0px; pointer-events: none; transform: rotate(1deg)');

    expect(cursor.style.position).toBe('fixed');
    expect(cursor.style.pointerEvents).toBe('none');
    expect(cursor.style.transform).toBe('translate3d(0px,0px,0) scale(1)');

    handle.dispose();
    expect(cursor.style.transform).toBe('rotate(1deg)');
  });
});
