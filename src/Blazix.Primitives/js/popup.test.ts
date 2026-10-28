// L3 (ADR 06 §1): the `popup` module in jsdom against the fake twin (M4 track 5, issue 125). usePopupViewport.test.tsx's
// three titles verbatim (PSH-ALG-13, PSH-DOM-07); port titles for the Viewport's clone and its choreography, auto-resize
// with stubbed sizes (the Tabs `layout()` precedent), the internal backdrop's cut-out, the anchored lock's width test, the
// trigger-change report and the composite keys' stop. Each case builds the DOM C# renders and attaches as the twin would.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

// `positioning`'s reader stands in for an attached Positioner: the rendered physical side is the attribute the test sets.
const placementReads = vi.hoisted(() => ({ count: 0 }));
const named = vi.hoisted(() => ({ calls: [] as { token: string; element: Element }[] }));
// The interactions session the Viewport reads its active trigger from (design point 13.13): one stub for every token.
const session = vi.hoisted(() => ({ token: 'session', domReferenceElement: null as Element | null, references: 0 }));
vi.mock('./interactions', () => ({
  getSession: (token: string) => ({ token }),
  acquireSession: () => {
    session.references += 1;
    return session;
  },
  releaseSession: () => {
    session.references -= 1;
  },
  nameRelayTrigger: (target: { token: string }, element: Element) => named.calls.push({ token: target.token, element }),
}));
vi.mock('./positioning', () => ({
  readPlacement: (element: Element) => {
    placementReads.count += 1;
    return element.hasAttribute('data-unattached') ? null : { physicalSide: element.getAttribute('data-physical-side') ?? 'bottom', isPositioned: true };
  },
}));

const {
  TRIGGER_CHANGE_REPORT,
  VIEWPORT_REPORT,
  attach,
  calculateRelativePosition,
  cutoutPolygon,
  getActivationDirection,
  getPopupAnchoringStyles,
  touchOpenLocksScroll,
} = await import('./popup');
import type { Offset, PopupGuardFocus, PopupOptions, PopupViewportReport } from './popup';
import { resetScrollLockForTest, scrollLockCount } from './scroll-lock';
import { resetFrameScheduler } from './transitions';
import type { TwinHandle } from './twin';

let frames: FrameRequestCallback[] = [];
const handles: TwinHandle[] = [];

beforeEach(() => {
  frames = [];
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    frames.push(callback);
    return frames.length;
  });
  vi.stubGlobal('cancelAnimationFrame', () => {});
  globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
});

afterEach(() => {
  session.domReferenceElement = null;
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  resetFrameScheduler();
  resetScrollLockForTest();
  vi.unstubAllGlobals();
  document.body.replaceChildren();
});

function flushFrames(): void {
  const pending = frames;
  frames = [];
  for (const frame of pending) {
    frame(performance.now());
  }
}

async function settle(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

function withRect(element: HTMLElement, left: number, top: number, width: number, height: number): HTMLElement {
  element.getBoundingClientRect = () => ({ x: left, y: top, left, top, width, height, right: left + width, bottom: top + height, toJSON: () => ({}) });
  return element;
}

function withSize(element: HTMLElement, size: { width: number; height: number }): void {
  Object.defineProperty(element, 'offsetWidth', { configurable: true, get: () => size.width });
  Object.defineProperty(element, 'offsetHeight', { configurable: true, get: () => size.height });
}

function attachTo(element: HTMLElement, options: PopupOptions, twin: FakeTwin): TwinHandle {
  const handle = attach(element, JSON.stringify(options), twin.twin);
  handles.push(handle);
  return handle;
}

interface RenderedViewport {
  positioner: HTMLElement;
  popup: HTMLElement;
  viewport: HTMLElement;
  triggers: HTMLElement[];
  size: { width: number; height: number };
}

/** Renders what C# renders: two triggers, the positioner, the popup and the viewport over `[data-current]`. */
function renderViewport(): RenderedViewport {
  const triggers = [withRect(document.createElement('button'), 0, 0, 100, 50), withRect(document.createElement('button'), 200, 100, 100, 50)];
  triggers.forEach((trigger, index) => {
    trigger.id = `trigger-${index + 1}`;
    document.body.appendChild(trigger);
  });
  const positioner = document.createElement('div');
  positioner.setAttribute('data-open', '');
  positioner.setAttribute('data-side', 'bottom');
  const popup = document.createElement('div');
  popup.id = 'popup';
  popup.setAttribute('data-open', '');
  const viewport = document.createElement('div');
  viewport.appendChild(current('One'));
  popup.appendChild(viewport);
  positioner.appendChild(popup);
  document.body.appendChild(positioner);
  const size = { width: 120, height: 40 };
  withSize(popup, size);
  return { positioner, popup, viewport, triggers, size };
}

function current(text: string): HTMLElement {
  const container = document.createElement('div');
  container.setAttribute('data-current', '');
  const span = document.createElement('span');
  span.textContent = text;
  container.appendChild(span);
  return container;
}

/** Opens the popup on a trigger as C# renders it: `aria-expanded` and `aria-controls` on the active trigger only, and the session's reference. */
function activate(rendered: RenderedViewport, index: number): void {
  session.domReferenceElement = rendered.triggers[index] ?? null;
  rendered.triggers.forEach((trigger, i) => {
    if (i === index) {
      trigger.setAttribute('data-popup-open', '');
      trigger.setAttribute('aria-expanded', 'true');
      trigger.setAttribute('aria-controls', 'popup');
    } else {
      trigger.removeAttribute('data-popup-open');
      trigger.setAttribute('aria-expanded', 'false');
      trigger.removeAttribute('aria-controls');
    }
  });
}

/** C#'s keyed remount of `[data-current]`, applied in one batch with the triggers' attributes. */
function remount(rendered: RenderedViewport, text: string): HTMLElement {
  const next = current(text);
  rendered.viewport.querySelector('[data-current]')!.remove();
  rendered.viewport.appendChild(next);
  return next;
}

const openViewport = { sessionToken: 'session', viewport: { open: true, popupId: 'popup' } };

describe('getActivationDirection', () => {
  it('returns undefined without an offset', () => {
    expect(getActivationDirection(null)).toBe(undefined);
  });

  it.each([
    { offset: { horizontal: 100, vertical: 100 }, expected: ['right', 'down'] },
    { offset: { horizontal: -100, vertical: -100 }, expected: ['left', 'up'] },
    { offset: { horizontal: 100, vertical: -100 }, expected: ['right', 'up'] },
    { offset: { horizontal: -100, vertical: 100 }, expected: ['left', 'down'] },
    { offset: { horizontal: 100, vertical: 0 }, expected: ['right'] },
    { offset: { horizontal: -100, vertical: 0 }, expected: ['left'] },
    { offset: { horizontal: 0, vertical: 100 }, expected: ['down'] },
    { offset: { horizontal: 0, vertical: -100 }, expected: ['up'] },
    { offset: { horizontal: 0, vertical: 0 }, expected: [] },
    { offset: { horizontal: 5, vertical: 5 }, expected: [] },
    { offset: { horizontal: -5, vertical: -5 }, expected: [] },
    { offset: { horizontal: 5.5, vertical: -5.5 }, expected: ['right', 'up'] },
    { offset: { horizontal: 6, vertical: 6 }, expected: ['right', 'down'] },
    { offset: { horizontal: -6, vertical: -6 }, expected: ['left', 'up'] },
  ] satisfies Array<{ offset: Offset; expected: string[] }>)('returns [$expected] for $offset.horizontal, $offset.vertical', ({ offset, expected }) => {
    expect(getActivationDirection(offset)?.split(' ').filter(Boolean)).toEqual(expected);
  });
});

describe('calculateRelativePosition', () => {
  function element([left, top, width, height]: readonly [number, number, number, number]): HTMLElement {
    return withRect(document.createElement('div'), left, top, width, height);
  }

  it.each([
    { name: 'measures from the previous trigger to the new one', from: [0, 0, 100, 50], to: [200, 100, 100, 50], expected: { horizontal: 200, vertical: 100 } },
    { name: 'is negative when the new trigger is up and to the left', from: [200, 100, 100, 50], to: [0, 0, 100, 50], expected: { horizontal: -200, vertical: -100 } },
    { name: 'compares centers, not corners, of differently sized triggers', from: [0, 0, 100, 50], to: [10, 0, 20, 10], expected: { horizontal: -30, vertical: -20 } },
    { name: 'is zero for triggers that share a center', from: [0, 0, 100, 100], to: [25, 25, 50, 50], expected: { horizontal: 0, vertical: 0 } },
  ] as const)('$name', ({ from, to, expected }) => {
    expect(calculateRelativePosition(element(from), element(to))).toEqual(expected);
  });
});

describe.each([false, true])('the viewport (synchronous: %s)', (synchronous) => {
  it('port: a move between triggers while open clones the removed content into [data-previous] and reports the direction', async () => {
    const rendered = renderViewport();
    activate(rendered, 0);
    const twin = createFakeTwin({ synchronous });
    attachTo(rendered.viewport, openViewport, twin);

    activate(rendered, 1);
    const next = remount(rendered, 'Two');
    await settle();

    const previous = rendered.viewport.firstElementChild as HTMLElement;
    expect(previous.hasAttribute('data-previous')).toBe(true);
    expect(previous.hasAttribute('inert')).toBe(true);
    expect(previous.style.position).toBe('absolute');
    expect(previous.textContent).toBe('One');
    expect(rendered.viewport.lastElementChild).toBe(next);
    expect(rendered.viewport.getAttribute('data-activation-direction')).toBe('right down');
    expect(rendered.viewport.hasAttribute('data-transitioning')).toBe(true);
    expect(twin.reported<PopupViewportReport>(VIEWPORT_REPORT).at(-1)).toEqual({ activationDirection: 'right down', transitioning: true });
  });

  it('port: a Viewport whose triggers render no aria-controls still reports the activation direction', async () => {
    const rendered = renderViewport();
    session.domReferenceElement = rendered.triggers[0]!;
    const twin = createFakeTwin({ synchronous });
    attachTo(rendered.viewport, openViewport, twin);

    // Tooltip's and PreviewCard's triggers carry neither aria-expanded nor aria-controls (TIP-ARIA-02/05, PRV-ARIA-02).
    session.domReferenceElement = rendered.triggers[1]!;
    remount(rendered, 'Two');
    await settle();

    expect(rendered.viewport.querySelector('[data-previous]')?.textContent).toBe('One');
    expect(twin.reported<PopupViewportReport>(VIEWPORT_REPORT).at(-1)).toEqual({ activationDirection: 'right down', transitioning: true });
  });

  it('port: a move between triggers inside a shadow root clones the removed content', async () => {
    const rendered = renderViewport();
    const host = document.createElement('div');
    document.body.appendChild(host);
    host.attachShadow({ mode: 'open' }).append(...rendered.triggers, rendered.positioner);
    activate(rendered, 0);
    attachTo(rendered.viewport, openViewport, createFakeTwin({ synchronous }));

    activate(rendered, 1);
    remount(rendered, 'Two');
    await settle();

    expect(rendered.viewport.querySelector('[data-previous]')?.textContent).toBe('One');
  });

  it('port: an update that arrives before the mutation records (WebAssembly) still clones the removed content', async () => {
    const rendered = renderViewport();
    activate(rendered, 0);
    const twin = createFakeTwin({ synchronous });
    const handle = attachTo(rendered.viewport, openViewport, twin);

    // WebAssembly applies the render batches and C#'s after-render update in one task, before the observer's microtask.
    activate(rendered, 1);
    remount(rendered, 'Two');
    handle.update(JSON.stringify({ viewport: { open: true, popupId: 'popup', contentVersion: 1 } }));
    await settle();

    expect(rendered.viewport.querySelector('[data-previous]')?.textContent).toBe('One');
  });

  it('port: the starting style moves to an ending style over a frame, and the previous content leaves once the current one settles', async () => {
    const rendered = renderViewport();
    activate(rendered, 0);
    const twin = createFakeTwin({ synchronous });
    attachTo(rendered.viewport, openViewport, twin);

    activate(rendered, 1);
    const next = remount(rendered, 'Two');
    await settle();
    const previous = rendered.viewport.querySelector('[data-previous]')!;
    expect(next.hasAttribute('data-starting-style')).toBe(true);
    expect(previous.hasAttribute('data-ending-style')).toBe(false);

    flushFrames();
    expect(next.hasAttribute('data-starting-style')).toBe(false);
    expect(previous.hasAttribute('data-ending-style')).toBe(true);

    // Animations are disabled, so the watcher settles at once.
    flushFrames();
    await settle();
    expect(rendered.viewport.querySelector('[data-previous]')).toBe(null);
    expect(rendered.viewport.hasAttribute('data-transitioning')).toBe(false);
    expect(twin.reported<PopupViewportReport>(VIEWPORT_REPORT).at(-1)).toEqual({ activationDirection: 'right down', transitioning: false });
  });

  it('port: a remount without a move (a lagging payload) keeps the previous content first and restarts the choreography', async () => {
    const rendered = renderViewport();
    activate(rendered, 0);
    const twin = createFakeTwin({ synchronous });
    attachTo(rendered.viewport, openViewport, twin);
    activate(rendered, 1);
    remount(rendered, 'Two, stale');
    await settle();

    const late = remount(rendered, 'Two');
    await settle();

    const previous = rendered.viewport.firstElementChild!;
    expect(previous.hasAttribute('data-previous')).toBe(true);
    expect(previous.textContent).toBe('One');
    expect(rendered.viewport.lastElementChild).toBe(late);
    expect(late.hasAttribute('data-starting-style')).toBe(true);
  });

  it('port: a reopen on another trigger starts no transition and becomes the trigger the next move starts from', async () => {
    const rendered = renderViewport();
    activate(rendered, 0);
    const twin = createFakeTwin({ synchronous });
    const handle = attachTo(rendered.viewport, openViewport, twin);

    handle.update(JSON.stringify({ viewport: { ...openViewport.viewport, open: false } }));
    activate(rendered, 1);
    rendered.triggers[1]!.setAttribute('aria-expanded', 'false');
    remount(rendered, 'Two');
    await settle();
    handle.update(JSON.stringify(openViewport));
    activate(rendered, 1);
    remount(rendered, 'Two again');
    await settle();

    expect(rendered.viewport.querySelector('[data-previous]')).toBe(null);
    expect(rendered.viewport.hasAttribute('data-activation-direction')).toBe(false);

    // The reopened trigger is the one the next move starts from: its content is cloned, the direction is from it.
    activate(rendered, 0);
    remount(rendered, 'One again');
    await settle();

    expect(rendered.viewport.querySelector('[data-previous]')?.textContent).toBe('Two again');
    expect(rendered.viewport.getAttribute('data-activation-direction')).toBe('left up');
  });
});

describe('auto-resize', () => {
  it('port: the first measurement sizes the positioner, leaves the popup at auto, and restores every override', () => {
    const rendered = renderViewport();
    rendered.popup.style.transform = 'scale(0.9)';
    const twin = createFakeTwin();
    attachTo(rendered.viewport, openViewport, twin);

    expect(rendered.positioner.style.getPropertyValue('--positioner-width')).toBe('120px');
    expect(rendered.positioner.style.getPropertyValue('--positioner-height')).toBe('40px');
    expect(rendered.popup.style.getPropertyValue('--popup-width')).toBe('auto');
    expect(rendered.popup.style.getPropertyValue('--popup-height')).toBe('auto');
    expect(rendered.popup.style.transform).toBe('scale(0.9)');
    expect(rendered.popup.style.getPropertyValue('scale')).toBe('');
    expect(rendered.popup.style.position).toBe('');
    expect(rendered.positioner.style.getPropertyValue('--available-width')).toBe('');
  });

  it('port: a content change animates from the previous size to the new one, then returns to auto', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderViewport();
    rendered.popup.getAnimations = () => [];
    activate(rendered, 0);
    const twin = createFakeTwin();
    attachTo(rendered.viewport, openViewport, twin);

    rendered.size.width = 200;
    rendered.size.height = 80;
    activate(rendered, 1);
    remount(rendered, 'Two');
    await settle();

    expect(rendered.popup.style.getPropertyValue('--popup-width')).toBe('120px');
    expect(rendered.positioner.style.getPropertyValue('--positioner-width')).toBe('200px');
    const previous = rendered.viewport.querySelector<HTMLElement>('[data-previous]')!;
    expect(previous.style.getPropertyValue('--popup-width')).toBe('120px');
    expect(previous.style.getPropertyValue('--popup-height')).toBe('40px');

    flushFrames();
    expect(rendered.popup.style.getPropertyValue('--popup-width')).toBe('200px');
    expect(rendered.popup.style.getPropertyValue('--popup-height')).toBe('80px');
    flushFrames();
    await settle();
    await settle();
    expect(rendered.popup.style.getPropertyValue('--popup-width')).toBe('auto');
    expect(rendered.popup.style.getPropertyValue('scale')).toBe('');
  });

  it.each([
    { side: 'top', expected: { position: 'absolute', bottom: '0px', left: '0px' } },
    { side: 'left', expected: { position: 'absolute', top: '0px', right: '0px' } },
    { side: 'right', expected: {} },
    { side: 'bottom', expected: {} },
  ] as const)('port: anchors the popup on the rendered physical $side side', ({ side, expected }) => {
    const rendered = renderViewport();
    rendered.positioner.setAttribute('data-physical-side', side);
    attachTo(rendered.viewport, openViewport, createFakeTwin());

    for (const property of ['position', 'top', 'bottom', 'left', 'right']) {
      expect(rendered.popup.style.getPropertyValue(property)).toBe((expected as Record<string, string>)[property] ?? '');
    }
  });

  it('port: the anchoring styles follow a flip of the side', async () => {
    const rendered = renderViewport();
    attachTo(rendered.viewport, openViewport, createFakeTwin());
    expect(rendered.popup.style.position).toBe('');

    rendered.positioner.setAttribute('data-physical-side', 'top');
    rendered.positioner.setAttribute('data-side', 'top');
    await settle();

    expect(rendered.popup.style.position).toBe('absolute');
    expect(rendered.popup.style.bottom).toBe('0px');
  });

  it('port: a positioner whose handle attaches after the viewport measured re-anchors the popup on its first write', async () => {
    const rendered = renderViewport();
    rendered.positioner.setAttribute('data-unattached', '');
    attachTo(rendered.viewport, openViewport, createFakeTwin());
    expect(rendered.popup.style.position).toBe('');

    rendered.positioner.removeAttribute('data-unattached');
    rendered.positioner.setAttribute('data-physical-side', 'left');
    rendered.positioner.style.setProperty('--anchor-width', '72px');
    await settle();

    expect(rendered.popup.style.position).toBe('absolute');
    expect(rendered.popup.style.right).toBe('0px');
  });

  it("port: the measurement's own writes on a positioner that reads nothing never re-measure it", async () => {
    const rendered = renderViewport();
    rendered.positioner.setAttribute('data-unattached', '');
    attachTo(rendered.viewport, openViewport, createFakeTwin());
    placementReads.count = 0;

    await settle();
    await settle();

    expect(placementReads.count).toBe(0);
  });

  it('port: the anchoring styles of a side, as Base UI derives them', () => {
    expect(getPopupAnchoringStyles('top')).toEqual({ position: 'absolute', bottom: '0', left: '0' });
    expect(getPopupAnchoringStyles('left')).toEqual({ position: 'absolute', top: '0', right: '0' });
    expect(getPopupAnchoringStyles('right')).toEqual({});
  });
});

describe('the positioner', () => {
  function renderPositioner(): { positioner: HTMLElement; backdrop: HTMLElement; trigger: HTMLElement } {
    const trigger = withRect(document.createElement('button'), 10, 20, 30, 40);
    trigger.id = 'trigger';
    document.body.appendChild(trigger);
    const backdrop = document.createElement('div');
    backdrop.setAttribute('role', 'presentation');
    backdrop.setAttribute('data-base-ui-inert', '');
    backdrop.setAttribute('style', 'position: fixed; inset: 0');
    const positioner = document.createElement('div');
    document.body.append(backdrop, positioner);
    return { positioner, backdrop, trigger };
  }

  it('port: cuts the active trigger out of the internal backdrop, and follows each positioning pass', async () => {
    const { positioner, backdrop, trigger } = renderPositioner();
    attachTo(positioner, { positioner: { watchTriggerChange: true, cutout: true, activeTriggerId: 'trigger' } }, createFakeTwin());

    expect(backdrop.style.clipPath).toBe(cutoutPolygon({ left: 10, top: 20, right: 40, bottom: 60 }));
    expect(backdrop.style.position).toBe('fixed');

    withRect(trigger, 15, 25, 30, 40);
    positioner.style.setProperty('transform', 'translate(1px, 2px)');
    await settle();
    expect(backdrop.style.clipPath).toBe(cutoutPolygon({ left: 15, top: 25, right: 45, bottom: 65 }));
  });

  it('port: a trigger inside a shadow root is found by its id and cut out of the backdrop', () => {
    const { positioner, backdrop, trigger } = renderPositioner();
    const host = document.createElement('div');
    document.body.appendChild(host);
    host.attachShadow({ mode: 'open' }).append(trigger, backdrop, positioner);
    attachTo(positioner, { positioner: { watchTriggerChange: true, cutout: true, activeTriggerId: 'trigger' } }, createFakeTwin());

    expect(backdrop.style.clipPath).toBe(cutoutPolygon({ left: 10, top: 20, right: 40, bottom: 60 }));
  });

  it('port: a backdrop inserted after attach is cut out in the same batch', async () => {
    const { positioner, backdrop, trigger } = renderPositioner();
    backdrop.remove();
    attachTo(positioner, { positioner: { watchTriggerChange: true, cutout: true, activeTriggerId: trigger.id } }, createFakeTwin());

    positioner.before(backdrop);
    await settle();

    expect(backdrop.style.clipPath).toBe(cutoutPolygon({ left: 10, top: 20, right: 40, bottom: 60 }));
  });

  it('port: the cut-out polygon is the backdrop with a hole at the trigger', () => {
    expect(cutoutPolygon({ left: 1, top: 2, right: 3, bottom: 4 })).toBe('polygon(0% 0%,100% 0%,100% 100%,0% 100%,0% 0%,1px 2px,1px 4px,3px 4px,3px 2px,1px 2px)');
  });

  it.each([
    { viewport: 400, popup: 380, expected: true },
    { viewport: 400, popup: 379, expected: false },
    { viewport: 0, popup: 0, expected: false },
    { viewport: 400, popup: 0, expected: false },
  ])('port: a touch-opened popup $popup px wide in a $viewport px viewport locks: $expected', ({ viewport, popup, expected }) => {
    expect(touchOpenLocksScroll(viewport, popup)).toBe(expected);
  });

  it('port: the anchored lock holds while enabled, and for a touch open only when the popup spans the viewport', () => {
    const { positioner, trigger } = renderPositioner();
    Object.defineProperty(document.documentElement, 'clientWidth', { configurable: true, get: () => 400 });
    const size = { width: 300, height: 10 };
    withSize(positioner, size);
    const handle = attachTo(positioner, { positioner: { watchTriggerChange: true, scrollLock: true, activeTriggerId: trigger.id } }, createFakeTwin());
    expect(scrollLockCount()).toBe(1);

    handle.update(JSON.stringify({ positioner: { watchTriggerChange: true, scrollLock: true, touchOpen: true, activeTriggerId: trigger.id } }));
    expect(scrollLockCount()).toBe(0);

    size.width = 390;
    handle.update(JSON.stringify({ positioner: { watchTriggerChange: true, scrollLock: true, touchOpen: true, activeTriggerId: trigger.id } }));
    expect(scrollLockCount()).toBe(1);

    handle.update(JSON.stringify({ positioner: { watchTriggerChange: false, activeTriggerId: trigger.id } }));
    expect(scrollLockCount()).toBe(0);
  });

  it('port: a move between triggers while open is reported once the positioner has settled, and a close drops it', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const { positioner } = renderPositioner();
    positioner.getAnimations = () => [];
    const twin = createFakeTwin();
    const handle = attachTo(positioner, { positioner: { watchTriggerChange: true, activeTriggerId: 'a' } }, twin);

    handle.update(JSON.stringify({ positioner: { watchTriggerChange: true, activeTriggerId: 'b' } }));
    expect(twin.reported(TRIGGER_CHANGE_REPORT)).toHaveLength(0);
    flushFrames();
    await settle();
    expect(twin.reported(TRIGGER_CHANGE_REPORT)).toHaveLength(1);

    handle.update(JSON.stringify({ positioner: { watchTriggerChange: true, activeTriggerId: 'a' } }));
    handle.update(JSON.stringify({ positioner: { watchTriggerChange: false, activeTriggerId: 'a' } }));
    flushFrames();
    await settle();
    expect(twin.reported(TRIGGER_CHANGE_REPORT)).toHaveLength(1);
  });
});

describe('the popup', () => {
  it.each(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End'])('port: with the stop, %s keydown stays inside the popup without being prevented', (key) => {
    const toolbar = document.createElement('div');
    const popup = document.createElement('div');
    const item = document.createElement('button');
    popup.appendChild(item);
    toolbar.appendChild(popup);
    document.body.appendChild(toolbar);
    const reached = vi.fn();
    toolbar.addEventListener('keydown', reached);
    attachTo(popup, { popup: { stopCompositeKeys: true } }, createFakeTwin());

    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
    item.dispatchEvent(event);

    expect(reached).not.toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(false);
  });

  it('port: the stop follows its option on update and leaves every other key to the toolbar', () => {
    const toolbar = document.createElement('div');
    const popup = document.createElement('div');
    toolbar.appendChild(popup);
    document.body.appendChild(toolbar);
    const reached = vi.fn();
    toolbar.addEventListener('keydown', reached);
    const handle = attachTo(popup, { popup: { stopCompositeKeys: false } }, createFakeTwin());

    popup.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(reached).toHaveBeenCalledTimes(1);

    handle.update(JSON.stringify({ popup: { stopCompositeKeys: true } }));
    popup.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    popup.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));

    expect(reached).toHaveBeenCalledTimes(2);
    expect(reached.mock.calls[1]![0].key).toBe('Tab');
  });
});

describe('the trigger guards', () => {
  /** What C# renders: an outside button, the pre-guard, the trigger, the focus target, an after button and the portaled positioner. */
  function renderGuards() {
    const before = Object.assign(document.createElement('button'), { id: 'before', textContent: 'Before' });
    const guard = (): HTMLElement => {
      const span = document.createElement('span');
      span.tabIndex = 0;
      span.setAttribute('data-base-ui-focus-guard', '');
      span.setAttribute('aria-hidden', 'true');
      return span;
    };
    const preGuard = guard();
    const trigger = Object.assign(document.createElement('button'), { id: 'trigger', textContent: 'Trigger' });
    const focusTarget = guard();
    const after = Object.assign(document.createElement('button'), { id: 'after', textContent: 'After' });
    const positioner = document.createElement('div');
    const contentGuard = guard();
    const popup = Object.assign(document.createElement('div'), { id: 'popup' });
    const inside = Object.assign(document.createElement('button'), { id: 'inside', textContent: 'Inside' });
    popup.appendChild(inside);
    positioner.append(contentGuard, popup);
    document.body.append(before, preGuard, trigger, focusTarget, after, positioner);
    return { before, preGuard, trigger, focusTarget, after, positioner, contentGuard, inside };
  }

  const triggerOptions: PopupOptions = { sessionToken: 'session', trigger: { guards: true, popupId: 'popup' } };

  /** A WebAssembly twin: it records the handover and the decision, and C# closes inside the decision. */
  function synchronousTwin(onDecide: () => void) {
    const calls: { method: string; args: unknown[] }[] = [];
    const twin = {
      invokeMethod<T>(method: string, ...args: unknown[]): T {
        calls.push({ method, args });
        if (method === 'Decide') {
          onDecide();
          return 'run' as T;
        }
        return true as T;
      },
      invokeMethodAsync<T>(method: string, ...args: unknown[]): Promise<T> {
        calls.push({ method, args });
        return Promise.resolve(undefined as T);
      },
    };
    return { twin, calls };
  }

  beforeEach(() => {
    named.calls.length = 0;
  });

  it('port: on Server, focusing the pre-guard names it, focuses the tabbable before the trigger and reports the guard', () => {
    const rendered = renderGuards();
    const twin = createFakeTwin();
    attachTo(rendered.trigger, triggerOptions, twin);

    rendered.preGuard.focus();

    expect(document.activeElement).toBe(rendered.before);
    expect(named.calls).toEqual([{ token: 'session', element: rendered.preGuard }]);
    const report = twin.reported<PopupGuardFocus>('OnGuardFocus').at(-1);
    expect(report?.guard).toBe('before');
    expect(report?.event.type).toBe('focusin');
  });

  it('port: on WebAssembly, the guard is handed over, the close commits inside the decision, then the tabbable after the trigger takes focus', () => {
    const rendered = renderGuards();
    rendered.inside.focus();
    let decidedWhileFocused: Element | null = null;
    const { twin, calls } = synchronousTwin(() => {
      decidedWhileFocused = document.activeElement;
    });
    handles.push(attach(rendered.trigger, JSON.stringify(triggerOptions), twin));

    rendered.focusTarget.focus();

    const handover = calls.find((call) => call.method === 'OnGuardFocusing');
    expect(JSON.parse(handover!.args[0] as string)).toEqual({ guard: 'after' });
    expect(calls.some((call) => call.method === 'Decide' && call.args[0] === 'guard')).toBe(true);
    expect(decidedWhileFocused).toBe(rendered.focusTarget);
    expect(document.activeElement).toBe(rendered.after);
    expect(named.calls.map((call) => call.element)).toEqual([rendered.focusTarget]);
  });

  it('port: Tab onto the focus target from outside the popup moves into the popup content, closing nothing', () => {
    const rendered = renderGuards();
    const twin = createFakeTwin();
    attachTo(rendered.trigger, triggerOptions, twin);
    rendered.trigger.focus();

    rendered.focusTarget.focus();

    expect(document.activeElement).toBe(rendered.contentGuard);
    expect(twin.reported('OnGuardFocus')).toEqual([]);
    expect(named.calls).toEqual([]);
  });

  it('port: the guards follow the option on update: released while off, listened to again once on', () => {
    const rendered = renderGuards();
    const twin = createFakeTwin();
    const handle = attachTo(rendered.trigger, triggerOptions, twin);

    handle.update(JSON.stringify({ ...triggerOptions, trigger: { guards: false, popupId: 'popup' } }));
    rendered.preGuard.focus();
    expect(document.activeElement).toBe(rendered.preGuard);

    rendered.after.focus();
    handle.update(JSON.stringify(triggerOptions));
    rendered.preGuard.focus();

    expect(document.activeElement).toBe(rendered.before);
    expect(twin.reported('OnGuardFocus')).toHaveLength(1);
  });
});
