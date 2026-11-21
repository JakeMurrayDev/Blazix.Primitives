// L3 (ADR 06 §1): the `drawer` module's keyboard role on the Viewport handle, M6 track 8c (issue 279): Base UI's
// DrawerVirtualKeyboardProvider.test.tsx titles (DRW-FOC-03…FOC-11, DOM-23, and EDGE-05's lift point through a shadow root).
// Base UI runs them in a browser; here jsdom stands in, with the geometry the titles stub themselves plus what the browser
// gave them for free: a layout viewport 800 px tall, a `100svh` probe as tall, `isContentEditable`, `Element.scrollTo`,
// and a hit test the title names. A drawer under a `Drawer.VirtualKeyboardProvider` is a Viewport handle with
// `keyboard: true`; React's re-renders (a close, a nested drawer opening) are `update`s of the handle.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { attach, type DrawerOptions } from './drawer';
import type { TwinHandle } from './twin';
import { createFakeTwin } from './test-support/fake-twin';

// The browser runner's layout viewport, which Base UI's titles assume when they do not mock it.
const LAYOUT_HEIGHT = 800;
Object.defineProperty(window, 'innerHeight', { configurable: true, writable: true, value: LAYOUT_HEIGHT });

// jsdom has no layout: the `100svh` probe measures the layout viewport's full height, as a browser's does.
const nativeOffsetHeight = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'offsetHeight')!;
Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
  configurable: true,
  get(this: HTMLElement) {
    return this.style.height === '100svh' ? LAYOUT_HEIGHT : nativeOffsetHeight.get!.call(this);
  },
});

// jsdom has no `isContentEditable`.
if (!('isContentEditable' in HTMLElement.prototype)) {
  Object.defineProperty(HTMLElement.prototype, 'isContentEditable', {
    configurable: true,
    get(this: HTMLElement) {
      for (let node: HTMLElement | null = this; node; node = node.parentElement) {
        const value = node.getAttribute('contenteditable');
        if (value !== null) {
          return value !== 'false';
        }
      }
      return false;
    },
  });
}

// jsdom has no `Element.scrollTo`; the browser's moves the scroll position.
if (typeof Element.prototype.scrollTo !== 'function') {
  Element.prototype.scrollTo = function scrollTo(this: Element, options?: ScrollToOptions | number) {
    if (typeof options === 'object' && options !== null && options.top !== undefined) {
      this.scrollTop = options.top;
    }
  } as typeof Element.prototype.scrollTo;
}
// jsdom's window.scrollTo only reports that it is not implemented.
window.scrollTo = (() => {}) as typeof window.scrollTo;

// vitest's jsdom window is a proxy that jsdom's event constructors reject as `view`; a browser's window is accepted. The
// provider's redispatched click passes `view: window`, as Base UI's does.
for (const name of ['MouseEvent', 'PointerEvent'] as const) {
  const Native = window[name] as typeof MouseEvent;
  class WithoutView extends Native {
    constructor(type: string, init: MouseEventInit = {}) {
      const { view: _view, ...rest } = init;
      super(type, rest);
    }
  }
  Object.defineProperty(window, name, { configurable: true, writable: true, value: WithoutView });
}

const defaultElementFromPoint = () => null;
Object.defineProperty(document, 'elementFromPoint', { configurable: true, writable: true, value: defaultElementFromPoint });

const handles: TwinHandle[] = [];
const elements: Element[] = [];
const restores: Array<() => void> = [];

afterEach(() => {
  vi.useRealTimers();
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  for (const element of elements.splice(0)) {
    element.remove();
  }
  for (const restore of restores.splice(0).reverse()) {
    restore();
  }
  (document.activeElement as HTMLElement | null)?.blur?.();
  document.elementFromPoint = defaultElementFromPoint;
  vi.restoreAllMocks();
});

const KEYBOARD: DrawerOptions = {
  role: 'viewport',
  open: true,
  mounted: true,
  nested: false,
  nestedOpenDrawerCount: 0,
  swipeDirection: 'down',
  answeredDismiss: 0,
  popup: 0,
  backdrop: 1,
  keyboard: true,
  modal: false,
  nestedOpenDialogCount: 0,
};

interface Mounted {
  viewport: HTMLElement;
  popup: HTMLElement;
  backdrop: HTMLElement;
  get<T extends HTMLElement = HTMLElement>(id: string): T;
  update(options: Partial<DrawerOptions>): void;
}

// A drawer whose popup holds `html`: Drawer.Root > [Drawer.VirtualKeyboardProvider] > Drawer.Portal > Backdrop + Viewport
// > Popup. `container` is the portal's container.
function mount(html: string, options: Partial<DrawerOptions> = {}, container: ParentNode = document.body, before?: (root: Mounted) => void): Mounted {
  const backdrop = document.createElement('div');
  backdrop.setAttribute('data-testid', 'backdrop');
  const viewport = document.createElement('div');
  viewport.setAttribute('data-testid', 'viewport');
  const popup = document.createElement('div');
  popup.setAttribute('data-testid', 'popup');
  popup.innerHTML = html;
  viewport.append(popup);
  container.append(backdrop, viewport);
  elements.push(backdrop, viewport);
  Object.defineProperty(popup, 'offsetHeight', { value: 200, configurable: true });
  Object.defineProperty(popup, 'offsetWidth', { value: 300, configurable: true });
  const current = { ...KEYBOARD, ...options };
  const root: Mounted = {
    viewport,
    popup,
    backdrop,
    get: <T extends HTMLElement>(id: string) => {
      const found = (container as Element | Document | ShadowRoot).querySelector<T>(`[data-testid="${id}"]`);
      if (!found) {
        throw new Error(`no element ${id}`);
      }
      return found;
    },
    update: (next) => {
      Object.assign(current, next);
      handle.update(JSON.stringify(current), [popup, backdrop]);
    },
  };
  before?.(root);
  const fake = createFakeTwin();
  const handle = attach(viewport, JSON.stringify(current), fake.twin, [popup, backdrop]);
  handles.push(handle);
  return root;
}

function mockVisualViewport(height: number) {
  const originalDescriptor = Object.getOwnPropertyDescriptor(window, 'visualViewport');
  const listeners = new Map<string, Set<EventListener>>();
  const visualViewport = {
    height,
    offsetTop: 0,
    scale: 1,
    addEventListener(type: string, listener: EventListener) {
      if (!listeners.has(type)) {
        listeners.set(type, new Set());
      }
      listeners.get(type)?.add(listener);
    },
    removeEventListener(type: string, listener: EventListener) {
      listeners.get(type)?.delete(listener);
    },
  };
  Object.defineProperty(window, 'visualViewport', { configurable: true, value: visualViewport });
  const emit = (type: string) => listeners.get(type)?.forEach((listener) => listener(new Event(type)));
  restores.push(() => {
    if (originalDescriptor) {
      Object.defineProperty(window, 'visualViewport', originalDescriptor);
    } else {
      Object.defineProperty(window, 'visualViewport', { configurable: true, value: undefined });
    }
  });
  return {
    resize(nextHeight: number) {
      visualViewport.height = nextHeight;
      emit('resize');
    },
    setScale(nextScale: number) {
      visualViewport.scale = nextScale;
      emit('resize');
    },
    scroll(nextOffsetTop: number, nextHeight?: number) {
      visualViewport.offsetTop = nextOffsetTop;
      if (nextHeight !== undefined) {
        visualViewport.height = nextHeight;
      }
      emit('scroll');
    },
  };
}

function mockWindowInnerHeight(innerHeight: number) {
  const originalDescriptor = Object.getOwnPropertyDescriptor(window, 'innerHeight')!;
  Object.defineProperty(window, 'innerHeight', { configurable: true, value: innerHeight });
  restores.push(() => Object.defineProperty(window, 'innerHeight', originalDescriptor));
}

const rect = (top: number, height: number, left = 0, width = 320) => new DOMRect(left, top, width, height);

function setRect(element: Element, measure: () => DOMRect) {
  element.getBoundingClientRect = measure;
}

function setScrollSize(element: Element, clientHeight: number, scrollHeight: number) {
  Object.defineProperties(element, {
    clientHeight: { configurable: true, value: clientHeight },
    scrollHeight: { configurable: true, value: scrollHeight },
  });
}

// Scrolls synchronously, so a title's centering assertion does not wait on a smooth scroll.
function scrollInstantly(element: HTMLElement) {
  element.scrollTo = ((options?: ScrollToOptions | number) => {
    if (typeof options === 'object' && options !== null && options.top !== undefined) {
      element.scrollTop = options.top;
    }
  }) as typeof element.scrollTo;
}

function touchList(target: EventTarget, point: { clientX: number; clientY: number }) {
  return [{ identifier: 1, target, ...point }];
}

function fireTouch(target: EventTarget, type: string, touches: unknown[], changedTouches: unknown[] = touches): Event {
  const event = new Event(type, { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'touches', { value: touches, configurable: true });
  Object.defineProperty(event, 'changedTouches', { value: changedTouches, configurable: true });
  target.dispatchEvent(event);
  return event;
}

const touchStart = (target: EventTarget, clientX: number, clientY: number) => fireTouch(target, 'touchstart', touchList(target, { clientX, clientY }));
const touchMove = (target: EventTarget, clientX: number, clientY: number) => fireTouch(target, 'touchmove', touchList(target, { clientX, clientY }));

// Base UI's `createNativeTouchEnd` / `createNativeTouchCancel`: the lift point in `changedTouches`, nothing in `touches`.
function createNativeTouchEnd(target: EventTarget, point: { clientX: number; clientY: number }, type = 'touchend'): Event {
  const event = new Event(type, { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'changedTouches', { value: touchList(target, point), configurable: true });
  Object.defineProperty(event, 'touches', { value: [], configurable: true });
  return event;
}

async function flushMicrotasks() {
  for (let index = 0; index < 5; index += 1) {
    // eslint-disable-next-line no-await-in-loop
    await Promise.resolve();
  }
}

async function flushAnimationFrames(count: number) {
  for (let index = 0; index < count; index += 1) {
    // eslint-disable-next-line no-await-in-loop
    await new Promise((resolve) => {
      requestAnimationFrame(resolve);
    });
  }
}

const waitFor = (callback: () => void) => vi.waitFor(callback, { timeout: 3000, interval: 10 });
const inset = (viewport: HTMLElement) => viewport.style.getPropertyValue('--drawer-keyboard-inset');

const SCROLL_BODY = (style: string, inner: string) => `<div data-testid="scroll" style="${style}">${inner}</div>`;
const TALL = '<div style="height: 900px"></div>';
const INPUT = '<input data-testid="input" type="text">';

describe('<Drawer.VirtualKeyboardProvider /> (drawer module keyboard role)', () => {
  it('compensates for keyboard overlap without adding extra spacing and centers the focused input', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; overflow-anchor: auto; padding-bottom: 20px', TALL + INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));
    setRect(input, () => rect(650 - scroll.scrollTop, 40));
    scrollInstantly(scroll);

    const keyboardViewportHeight = 500;
    input.focus();
    scroll.scrollTop = 0;
    visualViewport.resize(keyboardViewportHeight);

    await waitFor(() => expect(scroll.style.paddingBottom).toBe('240px'));
    await waitFor(() => expect(scroll.style.scrollPaddingBottom).not.toBe(''));
    await waitFor(() => expect(scroll.style.overflowAnchor).toBe('none'));
    await waitFor(() => {
      const scrollRect = scroll.getBoundingClientRect();
      const inputRect = input.getBoundingClientRect();
      expect((inputRect.top + inputRect.bottom) / 2).toBeCloseTo((scrollRect.top + keyboardViewportHeight) / 2, 0);
    });

    input.blur();

    await waitFor(() => expect(scroll.style.paddingBottom).toBe('20px'));
    await waitFor(() => expect(scroll.style.scrollPaddingBottom).toBe(''));
    await waitFor(() => expect(scroll.style.overflowAnchor).toBe('auto'));
  });

  it('tops the scroll container padding up to the visibility margin below the overlap', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 0px', TALL + INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));

    input.focus();
    visualViewport.resize(500);

    // 220px of overlap plus the 16px margin the container's own padding does not cover.
    await waitFor(() => expect(scroll.style.paddingBottom).toBe('236px'));

    input.blur();

    await waitFor(() => expect(scroll.style.paddingBottom).toBe('0px'));
  });

  it('adds scroll slack to a light-DOM scroller for an input inside a shadow root', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; overflow-anchor: auto; padding-bottom: 20px', `${TALL}<div data-testid="host"></div>`));
    const scroll = drawer.get('scroll');
    const shadowRoot = drawer.get('host').attachShadow({ mode: 'open' });
    const input = document.createElement('input');
    input.type = 'text';
    shadowRoot.append(input);
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));
    setRect(input, () => rect(650 - scroll.scrollTop, 40));
    scrollInstantly(scroll);

    const keyboardViewportHeight = 500;
    input.focus();
    scroll.scrollTop = 0;
    visualViewport.resize(keyboardViewportHeight);

    // Slack reaches the light-DOM scroller even though the input lives in a shadow root.
    await waitFor(() => expect(Number.parseFloat(scroll.style.paddingBottom)).toBeGreaterThan(20));
    await waitFor(() => expect(scroll.style.overflowAnchor).toBe('none'));
    await waitFor(() => {
      const scrollRect = scroll.getBoundingClientRect();
      const inputRect = input.getBoundingClientRect();
      expect((inputRect.top + inputRect.bottom) / 2).toBeCloseTo((scrollRect.top + keyboardViewportHeight) / 2, 0);
    });
  });

  it('adds keyboard scroll slack to a potential scroll ancestor for textareas', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', '<div style="height: 300px"></div><textarea data-testid="textarea"></textarea>'));
    const scroll = drawer.get('scroll');
    const textarea = drawer.get('textarea');
    setScrollSize(scroll, 420, 420);
    setScrollSize(textarea, 88, 88);
    setRect(scroll, () => rect(300, 420));
    setRect(textarea, () => rect(650, 40));

    textarea.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(Number.parseFloat(scroll.style.paddingBottom)).toBeGreaterThan(20));
    await waitFor(() => expect(textarea.style.paddingBottom).toBe(''));
  });

  it('adds keyboard scroll slack to the scroll container, not an overflowing textarea', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', `<div style="height: 300px"></div><textarea data-testid="textarea">${'line\n'.repeat(40)}</textarea>`));
    const scroll = drawer.get('scroll');
    const textarea = drawer.get('textarea');
    setScrollSize(scroll, 420, 800);
    // The textarea's own content overflows, making it scrollable itself. Scrolling it cannot move its box above the
    // keyboard, so slack must go to the ancestor.
    textarea.style.overflowY = 'auto';
    setScrollSize(textarea, 88, 600);
    setRect(scroll, () => rect(300, 420));
    setRect(textarea, () => rect(650, 40));

    textarea.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(Number.parseFloat(scroll.style.paddingBottom)).toBeGreaterThan(20));
    expect(textarea.style.paddingBottom).toBe('');
  });

  it('sets the keyboard inset CSS variable for a focused input without a scroll target', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(`<div>${INPUT}</div>`);
    const input = drawer.get('input');

    input.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));
    await waitFor(() => expect(getComputedStyle(drawer.popup).getPropertyValue('--drawer-keyboard-inset')).toBe('300px'));

    input.blur();

    await waitFor(() => expect(inset(drawer.viewport)).toBe('0px'));
  });

  // DRW-DOM-23 on the port (M6 track 8c, added 2026-11-21): C# renders the Viewport's `style`, and a Blazor re-render
  // rewrites the whole attribute; the inset is a `component` band of the Viewport's style writer, which absorbs the
  // foreign write before paint (ADR 04 Amendments 2026-11-18, the Drawer's design point 2 (b)).
  it('port: keeps the keyboard inset across a re-render of the viewport style attribute', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(`<div>${INPUT}</div>`);
    const input = drawer.get('input');

    input.focus();
    visualViewport.resize(500);
    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));

    drawer.viewport.setAttribute('style', 'position: fixed; inset: 0px;');

    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));
    expect(drawer.viewport.style.getPropertyValue('position')).toBe('fixed');
  });

  it('captures focusin targets through a shadow root', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount('<div data-testid="host"></div>');
    const shadowRoot = drawer.get('host').attachShadow({ mode: 'open' });
    const input = document.createElement('input');
    input.type = 'text';
    shadowRoot.append(input);

    input.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));
  });

  it('keeps the keyboard inset when focus moves directly to another keyboard input', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount('<div><input data-testid="input-a" type="text"><input data-testid="input-b" type="text"></div>');
    const inputA = drawer.get('input-a');
    const inputB = drawer.get('input-b');

    inputA.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));

    // Focus moving straight to another keyboard input must re-align to the new target, not clear the inset. Dispatch the
    // focusout in isolation (no following focusin) so a regression that cleared on every focusout would leave nothing to
    // restore the inset.
    inputA.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: inputB }));

    await flushAnimationFrames(2);
    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));
  });

  it('does not add keyboard scroll slack by default', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    // No Drawer.VirtualKeyboardProvider.
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', TALL + INPUT), { keyboard: false });
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));

    input.focus();
    scroll.scrollTop = 0;
    visualViewport.resize(500);
    await flushAnimationFrames(3);

    await waitFor(() => expect(scroll.style.paddingBottom).toBe('20px'));
    await waitFor(() => expect(scroll.style.scrollPaddingBottom).toBe(''));
    await waitFor(() => expect(scroll.scrollTop).toBe(0));
    expect(inset(drawer.viewport)).toBe('');
  });

  it('restores keyboard scroll slack when the drawer closes', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; overflow-anchor: auto; padding-bottom: 20px', TALL + INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));
    setRect(input, () => rect(650, 40));

    input.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(Number.parseFloat(scroll.style.paddingBottom)).toBeGreaterThan(20));
    await waitFor(() => expect(scroll.style.overflowAnchor).toBe('none'));

    drawer.update({ open: false });

    await waitFor(() => expect(scroll.style.paddingBottom).toBe('20px'));
    await waitFor(() => expect(scroll.style.scrollPaddingBottom).toBe(''));
    await waitFor(() => expect(scroll.style.overflowAnchor).toBe('auto'));
  });

  it.each([false, true])('releases keyboard inset and scroll slack after focus leaves the field (pointerdown: %s)', async (pointerDown) => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', `${INPUT}${TALL}<button data-testid="item" type="button"></button>`));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    const item = drawer.get('item');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));
    setRect(input, () => rect(310 - scroll.scrollTop, 40));

    input.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(Number.parseFloat(scroll.style.paddingBottom)).toBeGreaterThan(20));

    // Scroll into the slack, where removing it would clamp the scroll position.
    scroll.scrollTop = scroll.scrollHeight;
    const scrollTop = scroll.scrollTop;
    const paddingBottom = scroll.style.paddingBottom;
    const insetBefore = inset(drawer.viewport);

    // A tap's `mousedown` moves focus, and its `mouseup` and `click` are hit-tested at the same point in the same task,
    // so nothing under the finger may move synchronously.
    if (pointerDown) {
      // A consumer can move focus before pointerdown reaches the provider.
      window.addEventListener('pointerdown', () => item.focus(), { capture: true, once: true });
      item.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerType: 'mouse', button: 0, buttons: 1 }));
    } else {
      item.focus();
    }

    expect(scroll.style.paddingBottom).toBe(paddingBottom);
    expect(scroll.scrollTop).toBe(scrollTop);
    expect(inset(drawer.viewport)).toBe(insetBefore);

    await flushAnimationFrames(1);

    expect(scroll.style.paddingBottom).toBe('20px');
    expect(inset(drawer.viewport)).toBe('0px');
  });

  it('activates an item on the first tap while the keyboard is open', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const onClick = vi.fn();
    const drawer = mount(`${INPUT}<button data-testid="item" type="button">Select item</button>`);
    drawer.get('item').addEventListener('click', onClick);

    drawer.get('input').focus();
    visualViewport.resize(500);

    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));

    const item = drawer.get('item');
    // A tap's compatibility mousedown moves focus, then mouseup and click are hit-tested at the tap point in the same task.
    // A synchronous inset reset on focus loss would move the item away before they land: jsdom has no layout to move, so the
    // inset itself is what must hold until the next frame.
    item.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    item.focus();
    expect(inset(drawer.viewport)).toBe('300px');
    item.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true }));
    item.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));

    expect(onClick).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(inset(drawer.viewport)).toBe('0px'));
  });

  it('does not add keyboard scroll slack while pinch-zoomed', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', TALL + INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));

    input.focus();
    scroll.scrollTop = 0;
    visualViewport.setScale(1.5);
    visualViewport.resize(500);
    await flushAnimationFrames(3);

    await waitFor(() => expect(scroll.style.paddingBottom).toBe('20px'));
    await waitFor(() => expect(scroll.style.scrollPaddingBottom).toBe(''));
    await waitFor(() => expect(scroll.scrollTop).toBe(0));
  });

  it('focuses an unfocused keyboard input on touchend without page scroll', async () => {
    const drawer = mount('<input data-testid="input" style="opacity: 0.5; transform: scale(1); transition: opacity 1s" type="text">');
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    const clickEvents: MouseEvent[] = [];
    input.addEventListener('click', (clickEvent) => clickEvents.push(clickEvent));
    document.elementFromPoint = () => input;

    touchStart(input, 12, 34);

    await waitFor(() => expect(drawer.backdrop.hasAttribute('data-swiping')).toBe(true));

    const touchEnd = createNativeTouchEnd(input, { clientX: 12, clientY: 34 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
    // Preventing the touchend default suppresses the native compatibility click, so the provider redispatches it.
    expect(clickEvents).toHaveLength(1);
    expect(clickEvents[0]!.clientX).toBe(12);
    expect(clickEvents[0]!.clientY).toBe(34);
    expect(clickEvents[0]!.detail).toBe(1);
    expect(input.style.opacity).toBe('0.5');
    expect(input.style.transform).toBe('scale(1)');
    expect(input.style.transition).toBe('opacity 1s');
    await waitFor(() => expect(drawer.backdrop.hasAttribute('data-swiping')).toBe(false));
  });

  it('preserves native taps on picker inputs', async () => {
    const drawer = mount('<input data-testid="input" type="date">');
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('preserves native taps that lift over a neighboring interactive element', async () => {
    const drawer = mount(`${INPUT}<button data-testid="button" type="button">Action</button>`);
    const input = drawer.get('input');
    const button = drawer.get('button');
    const focusSpy = vi.spyOn(input, 'focus');
    const inputClickEvents: MouseEvent[] = [];
    input.addEventListener('click', (clickEvent) => inputClickEvents.push(clickEvent));
    // The finger lifts over the button, but `touchend.target` stays on the touchstart input on mobile, so the lift point
    // must win over the fallback.
    document.elementFromPoint = () => button;

    touchStart(input, 0, 0);
    // The native touchend is still dispatched on the touchstart input.
    const touchEnd = createNativeTouchEnd(input, { clientX: 5, clientY: 5 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
    expect(inputClickEvents).toHaveLength(0);
  });

  it('resolves the lift point through the shadow root when the drawer renders in one', async () => {
    const host = document.body.appendChild(document.createElement('div'));
    elements.push(host);
    const shadowRoot = host.attachShadow({ mode: 'open' });
    const drawer = mount(`${INPUT}<button data-testid="button" type="button">Action</button>`, {}, shadowRoot);
    const input = drawer.get<HTMLInputElement>('input');
    const button = drawer.get<HTMLButtonElement>('button');
    const focusSpy = vi.spyOn(input, 'focus');
    // A real document hit test retargets shadow content to the host, so only the shadow root sees the button under the
    // lift point. Resolving against the document would miss it and fall back to focusing the touchstart input.
    document.elementFromPoint = () => host;
    Object.defineProperty(shadowRoot, 'elementFromPoint', { configurable: true, value: () => button });

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 5, clientY: 5 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('preserves native taps on disabled inputs', async () => {
    const drawer = mount('<input data-testid="input" type="text" disabled>');
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    const clickEvents: MouseEvent[] = [];
    input.addEventListener('click', (clickEvent) => clickEvents.push(clickEvent));
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
    expect(clickEvents).toHaveLength(0);
  });

  it('preserves native taps on disabled textareas', async () => {
    const drawer = mount('<textarea data-testid="input" disabled></textarea>');
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    const clickEvents: MouseEvent[] = [];
    input.addEventListener('click', (clickEvent) => clickEvents.push(clickEvent));
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
    expect(clickEvents).toHaveLength(0);
  });

  it('preserves native taps on a disabled labelled control', async () => {
    const drawer = mount('<label data-testid="label" for="note">Note</label><input data-testid="input" id="note" type="text" disabled>');
    const label = drawer.get('label');
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    const labelClickEvents: MouseEvent[] = [];
    label.addEventListener('click', (clickEvent) => labelClickEvents.push(clickEvent));
    document.elementFromPoint = () => label;

    touchStart(label, 24, 48);
    const touchEnd = createNativeTouchEnd(label, { clientX: 24, clientY: 48 });
    label.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
    expect(labelClickEvents).toHaveLength(0);
  });

  it('focuses a keyboard input with data-base-ui-swipe-ignore without starting drawer swipe', async () => {
    const drawer = mount('<input data-testid="input" data-base-ui-swipe-ignore type="text">');
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    await flushMicrotasks();
    expect(drawer.backdrop.hasAttribute('data-swiping')).toBe(false);

    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
    expect(drawer.backdrop.hasAttribute('data-swiping')).toBe(false);
  });

  it('does not focus a keyboard input on touchend without touchstart', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;

    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('does not focus a keyboard input after touchcancel', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    input.dispatchEvent(createNativeTouchEnd(input, { clientX: 0, clientY: 0 }, 'touchcancel'));
    await flushMicrotasks();
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('does not focus a keyboard input while a nested drawer is open', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;
    // The nested drawer opened: C# sends the Root's nested counts.
    drawer.update({ nestedOpenDialogCount: 1, nestedOpenDrawerCount: 1 });

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('preserves native taps on an already-focused keyboard input', async () => {
    mockWindowInnerHeight(800);
    mockVisualViewport(500);
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    input.focus();
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('refocuses an already-focused keyboard input when the keyboard viewport is closed', async () => {
    mockWindowInnerHeight(800);
    mockVisualViewport(800);
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    input.focus();
    const focusSpy = vi.spyOn(input, 'focus');
    const blurSpy = vi.spyOn(input, 'blur');
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(blurSpy).toHaveBeenCalledTimes(1);
    expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
  });

  it('overrides the incoming field geometry when focus moves without a tap while the keyboard is open', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const drawer = mount('<input data-testid="first" type="text"><input data-testid="second" type="text">');
    const first = drawer.get('first');
    const second = drawer.get('second');

    first.focus();
    visualViewport.resize(500);
    await flushAnimationFrames(2);

    // The provider's capture-phase focusout listener runs before this bubble-phase one, so the geometry override must
    // already be applied when it fires.
    let transformDuringFocusChange: string | null = null;
    const onFocusOut = () => {
      transformDuringFocusChange = second.style.transform;
    };
    document.addEventListener('focusout', onFocusOut);
    const blurSpy = vi.spyOn(second, 'blur');
    const focusSpy = vi.spyOn(second, 'focus');
    const onFocus = vi.fn();
    second.addEventListener('focus', onFocus);
    let optionsDuringFocusIn: FocusOptions | undefined;
    second.addEventListener('focusin', () => {
      optionsDuringFocusIn = focusSpy.mock.lastCall?.[0];
    });

    // Simulates the iOS keyboard's next-field arrow: focus moves with no touch events.
    second.focus();

    expect(transformDuringFocusChange).toContain('translateY');
    expect(second.style.transform).toBe('');
    expect(second.style.opacity).toBe('');
    expect(blurSpy).not.toHaveBeenCalled();
    // The iOS 27 native focus request is recorded between focus and focusin. Protect it during focus without emitting
    // another focus/blur cycle.
    expect(optionsDuringFocusIn).toEqual({ preventScroll: true });
    expect(focusSpy).toHaveBeenCalledTimes(2);
    expect(onFocus).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(second);
    document.removeEventListener('focusout', onFocusOut);
  });

  // The body of the scroll titles below: `first` sits at the center of the visible band above the keyboard (so the
  // initial alignment leaves the scroll position at 0), `second` 270px below it.
  function twoFieldBody(options: Partial<DrawerOptions> = {}) {
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', '<input data-testid="first" type="text"><div style="height: 900px"></div><input data-testid="second" type="text">'), options);
    const scroll = drawer.get('scroll');
    const first = drawer.get('first');
    const second = drawer.get('second');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));
    setRect(first, () => rect(380 - scroll.scrollTop, 40));
    setRect(second, () => rect(650 - scroll.scrollTop, 40));
    return { drawer, scroll, first, second };
  }

  it('centers a field focused without a tap inside the scrollable body', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const { scroll, first, second } = twoFieldBody();
    scrollInstantly(scroll);
    const keyboardViewportHeight = 500;

    first.focus();
    scroll.scrollTop = 0;
    visualViewport.resize(keyboardViewportHeight);
    // Simulates the iOS keyboard's next-field arrow: focus moves with no touch events.
    second.focus();

    await waitFor(() => {
      const scrollRect = scroll.getBoundingClientRect();
      const secondRect = second.getBoundingClientRect();
      expect((secondRect.top + secondRect.bottom) / 2).toBeCloseTo((scrollRect.top + keyboardViewportHeight) / 2, 0);
    });
  });

  it('does not restart a progressing or settled reduced-motion alignment scroll', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      value: (query: string) => ({ matches: query === '(prefers-reduced-motion: reduce)' }) as MediaQueryList,
    });
    restores.push(() => Object.defineProperty(window, 'matchMedia', { configurable: true, value: undefined }));
    vi.useFakeTimers();
    const { scroll, first, second } = twoFieldBody();
    const scrollToSpy = vi.fn();
    scroll.scrollTo = scrollToSpy as unknown as typeof scroll.scrollTo;

    first.focus();
    visualViewport.resize(500);
    scrollToSpy.mockClear();

    second.focus();
    vi.advanceTimersToNextFrame();
    vi.advanceTimersToNextFrame();

    expect(scrollToSpy).toHaveBeenCalledWith({ top: 270, behavior: 'auto' });

    scroll.scrollTop = 100;
    await vi.advanceTimersByTimeAsync(150);

    expect(scrollToSpy).toHaveBeenCalledTimes(1);

    scroll.scrollTop = 270;
    await vi.runAllTimersAsync();

    expect(scroll.scrollTop).toBe(270);
    expect(scrollToSpy).toHaveBeenCalledTimes(1);
  });

  it('re-issues the alignment scroll when a delayed realign pass finds it stalled', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    const { scroll, first, second } = twoFieldBody();
    // The mock never moves, standing in for a smooth scroll canceled by WebKit's native reveal resolution: a later realign
    // pass must re-issue it.
    const scrollToSpy = vi.fn();
    scroll.scrollTo = scrollToSpy as unknown as typeof scroll.scrollTo;

    first.focus();
    visualViewport.resize(500);
    await flushAnimationFrames(3);
    scrollToSpy.mockClear();

    second.focus();

    await waitFor(() => expect(scrollToSpy.mock.calls.length).toBeGreaterThanOrEqual(2));
    expect(scrollToSpy).toHaveBeenLastCalledWith(expect.objectContaining({ top: 270 }));
  });

  it('stops pending realignment once the user starts scrolling while geometry is changing', async () => {
    mockWindowInnerHeight(800);
    const visualViewport = mockVisualViewport(800);
    vi.useFakeTimers();
    const { scroll, first, second } = twoFieldBody();
    let scrollBottom = 380;
    setRect(scroll, () => rect(300, scrollBottom - 300));
    // The mock never moves, standing in for a smooth scroll canceled by WebKit's native reveal resolution, the exact stall
    // a delayed pass would re-issue. A user scroll stops at a stationary position the same way, so a pointer going down
    // must cancel the passes rather than let one mistake the manual scroll for a canceled reveal.
    const scrollToSpy = vi.fn();
    scroll.scrollTo = scrollToSpy as unknown as typeof scroll.scrollTo;

    first.focus();
    visualViewport.resize(500);
    scrollToSpy.mockClear();

    second.focus();
    // Let the first settle check observe the squeezed body before the user takes over.
    vi.advanceTimersToNextFrame();
    scroll.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    // The body continues expanding after pointer-down, then the destination settles. A live frame loop would eventually
    // center the field against the final geometry.
    for (const bottom of [400, 440, 480, 480]) {
      scrollBottom = bottom;
      vi.advanceTimersToNextFrame();
    }
    await vi.runAllTimersAsync();

    // Pointer-down cancels both the pending frame alignment and delayed passes.
    expect(scrollToSpy).not.toHaveBeenCalled();
  });

  it('clears keyboard state when a tapped input redirects focus away', async () => {
    mockVisualViewport(500);
    vi.useFakeTimers();
    const drawer = mount(`${INPUT}<button data-testid="other" type="button"></button>`);
    const input = drawer.get('input');
    const other = drawer.get('other');
    // A consumer redirects focus off the field the moment it focuses. Listening on `focusin` at the target matches React's
    // `onFocus`: it runs after the provider's capture-phase `focusin` has already recorded the input, so the provider must
    // reconcile when focus lands on a non-keyboard element.
    input.addEventListener('focusin', () => other.focus());
    document.elementFromPoint = () => input;

    touchStart(input, 12, 34);
    input.dispatchEvent(createNativeTouchEnd(input, { clientX: 12, clientY: 34 }));
    await flushMicrotasks();
    await vi.runAllTimersAsync();

    // Focus stays on the redirected element and the drawer left no keyboard inset behind for the abandoned input.
    expect(document.activeElement).toBe(other);
    expect(inset(drawer.viewport)).toBe('0px');
  });

  it('clears keyboard state when a tapped input blurs itself', async () => {
    mockVisualViewport(500);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; overflow-anchor: auto; padding-bottom: 20px', TALL + INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));
    setRect(input, () => rect(650, 40));
    // A consumer that opens its own picker instead of the software keyboard blurs the field from `onFocus`. The `focusout`
    // arrives before `.focus()` returns, and focus ends on the body, so no `focusin` follows to reconcile from: that
    // `focusout` is the provider's only chance to drop the field.
    input.addEventListener('focusin', () => input.blur());
    document.elementFromPoint = () => input;

    touchStart(input, 12, 34);
    input.dispatchEvent(createNativeTouchEnd(input, { clientX: 12, clientY: 34 }));
    // Let the frame-scheduled alignment run: it is what applies the inset and slack to a target the `focusout` failed to
    // clear.
    await flushAnimationFrames(3);

    expect(document.activeElement).not.toBe(input);
    expect(inset(drawer.viewport)).toBe('0px');
    expect(scroll.style.paddingBottom).toBe('20px');
    expect(scroll.style.scrollPaddingBottom).toBe('');
  });

  it('defers the alignment scroll until the destination stops moving', async () => {
    const visualViewport = mockVisualViewport(800);
    vi.useFakeTimers();
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto', TALL + INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    // The scroller's bottom edge moves after focus (like a footer resizing over a CSS transition): squeezed at first,
    // settled a frame later.
    let scrollBottom = 380;
    setRect(scroll, () => rect(300, scrollBottom - 300));
    setRect(input, () => rect(650 - scroll.scrollTop, 40));
    const scrollToSpy = vi.fn((options?: ScrollToOptions | number) => {
      if (typeof options === 'object' && options !== null && options.top !== undefined) {
        scroll.scrollTop = options.top;
      }
    });
    scroll.scrollTo = scrollToSpy as unknown as typeof scroll.scrollTo;

    visualViewport.resize(500);
    input.focus();
    // Let the first alignment check measure the squeezed geometry, then settle it.
    vi.advanceTimersToNextFrame();
    scrollBottom = 480;
    await vi.runAllTimersAsync();

    // Squeezed destination: visible band [316, 364] → center 340, input center 670 → scrollTop 330. Settled: band [316,
    // 464] → center 390 → scrollTop 280. The squeezed destination must never be scrolled to.
    expect(scroll.scrollTop).toBe(280);
    expect(scrollToSpy).toHaveBeenCalledTimes(1);
    expect(scrollToSpy).not.toHaveBeenCalledWith(expect.objectContaining({ top: 330 }));
  });

  it('eventually aligns when geometry never settles', async () => {
    mockVisualViewport(500);
    vi.useFakeTimers();
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto', INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    let geometryTick = 0;
    setRect(scroll, () => {
      geometryTick += 1;
      return rect(geometryTick % 2 === 0 ? 100 : 200, 300);
    });
    setRect(input, () => rect(650, 40));
    const scrollToSpy = vi.fn();
    scroll.scrollTo = scrollToSpy as unknown as typeof scroll.scrollTo;

    input.focus();
    for (let index = 0; index < 65; index += 1) {
      vi.advanceTimersToNextFrame();
    }

    expect(scrollToSpy).toHaveBeenCalledTimes(1);
  });

  it('realigns a tapped field in the scrollable body when the keyboard is already open for a field outside it', async () => {
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(`${SCROLL_BODY('height: 420px; overflow-y: auto', `${TALL}<textarea data-testid="textarea"></textarea>`)}<div><input data-testid="footer-input" type="text"></div>`);
    const scroll = drawer.get('scroll');
    const textarea = drawer.get('textarea');
    const footerInput = drawer.get('footer-input');
    setScrollSize(scroll, 280, 1200);
    // The scroller is squeezed while the footer field holds focus; it expands back over a CSS transition once focus moves
    // into the body.
    let scrollBottom = 380;
    setRect(scroll, () => rect(100, scrollBottom - 100));
    setRect(textarea, () => rect(650 - scroll.scrollTop, 40));
    scrollInstantly(scroll);
    const keyboardViewportHeight = 500;

    footerInput.focus();
    visualViewport.resize(keyboardViewportHeight);
    await flushAnimationFrames(2);
    document.elementFromPoint = () => textarea;

    touchStart(textarea, 24, 300);
    textarea.dispatchEvent(createNativeTouchEnd(textarea, { clientX: 24, clientY: 300 }));
    await flushMicrotasks();

    expect(document.activeElement).toBe(textarea);
    // The frame-scheduled pass centers against the squeezed geometry: visible band [116, 364] → center 240, textarea
    // center 670 → scrollTop 430.
    await waitFor(() => expect(scroll.scrollTop).toBe(430));

    // The footer's shrink transition settles; only the delayed realign passes see the expanded scroller (the keyboard stays
    // open, so no viewport events fire).
    scrollBottom = 480;

    await waitFor(() => {
      const scrollRect = scroll.getBoundingClientRect();
      const textareaRect = textarea.getBoundingClientRect();
      const visibleCenter = (scrollRect.top + Math.min(scrollRect.bottom, keyboardViewportHeight)) / 2;
      expect((textareaRect.top + textareaRect.bottom) / 2).toBeCloseTo(visibleCenter, 0);
    });
  });

  it('does not blur and re-focus for focus changes while the keyboard is closed', async () => {
    mockVisualViewport(800);
    const drawer = mount('<input data-testid="first" type="text"><input data-testid="second" type="text">');
    const first = drawer.get('first');
    const second = drawer.get('second');
    first.focus();
    const blurSpy = vi.spyOn(second, 'blur');
    const focusSpy = vi.spyOn(second, 'focus');

    second.focus();

    expect(blurSpy).not.toHaveBeenCalled();
    expect(focusSpy).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(second);
  });

  function mockScrollY(value: number) {
    const original = Object.getOwnPropertyDescriptor(window, 'scrollY');
    Object.defineProperty(window, 'scrollY', { configurable: true, value });
    restores.push(() => {
      if (original) {
        Object.defineProperty(window, 'scrollY', original);
      }
    });
  }

  it('restores the page scroll position when a modal drawer page scrolls while the keyboard is open', async () => {
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(INPUT, { modal: true });
    const input = drawer.get('input');
    input.focus();
    visualViewport.resize(500);
    const scrollToSpy = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});

    // Simulates WebKit's native reveal scroll escaping the scroll lock.
    mockScrollY(120);
    window.dispatchEvent(new Event('scroll'));

    // Must restore instantly so the following measurements read the resting page; the two-argument form would obey a
    // global `scroll-behavior: smooth` and animate.
    expect(scrollToSpy).toHaveBeenCalledWith({ left: 0, top: 0, behavior: 'instant' });
  });

  it('does not restore the page scroll position for a non-modal drawer', async () => {
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    input.focus();
    visualViewport.resize(500);
    const scrollToSpy = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});

    mockScrollY(120);
    window.dispatchEvent(new Event('scroll'));

    expect(scrollToSpy).not.toHaveBeenCalled();
  });

  it('does not treat a small visual viewport reduction as the software keyboard', async () => {
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', TALL + INPUT));
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 420, 1200);
    setRect(scroll, () => rect(300, 420));

    // A reduction of 50px is below KEYBOARD_RESIZE_THRESHOLD (60px), so it is treated as browser chrome movement rather
    // than the software keyboard: no inset, no scroll slack.
    input.focus();
    visualViewport.resize(750);

    await waitFor(() => expect(inset(drawer.viewport)).toBe('0px'));
    expect(scroll.style.paddingBottom).toBe('20px');
    expect(scroll.style.overflowAnchor).toBe('');
  });

  it('computes the keyboard inset from a non-zero visual viewport offset', async () => {
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(INPUT);
    const input = drawer.get('input');

    input.focus();
    // The visual viewport shrinks to 500 and is offset 40px down within the layout viewport.
    visualViewport.scroll(40, 500);

    // inset = innerHeight - min(innerHeight, offsetTop + height) = 800 - (40 + 500) = 260.
    await waitFor(() => expect(inset(drawer.viewport)).toBe('260px'));
  });

  it.each(['explicit', 'implicit'])('preserves preventScroll after activating an %s label with no input focused', async (association) => {
    const field = '<input data-testid="input" id="note" type="text">';
    const drawer = mount(
      association === 'explicit'
        ? `<label data-testid="label" for="note"><span data-testid="label-text">Note</span></label>${field}`
        : `<label data-testid="label"><span data-testid="label-text">Note</span>${field}</label>`,
    );
    const label = drawer.get('label');
    const input = drawer.get('input');
    const labelText = drawer.get('label-text');
    expect(document.activeElement).not.toBe(input);
    const focusSpy = vi.spyOn(input, 'focus');
    const onFocus = vi.fn();
    const onBlur = vi.fn();
    input.addEventListener('focus', onFocus);
    input.addEventListener('blur', onBlur);
    const inputClickEvents: MouseEvent[] = [];
    let focusCallsDuringActivation = 0;
    input.addEventListener('click', (clickEvent) => {
      inputClickEvents.push(clickEvent);
      focusCallsDuringActivation = focusSpy.mock.calls.length;
    });
    const labelClickEvents: MouseEvent[] = [];
    label.addEventListener('click', (clickEvent) => {
      if (clickEvent.target === labelText) {
        labelClickEvents.push(clickEvent);
      }
    });
    document.elementFromPoint = () => labelText;

    touchStart(labelText, 24, 48);
    const touchEnd = createNativeTouchEnd(labelText, { clientX: 24, clientY: 48 });
    labelText.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
    expect(labelClickEvents).toHaveLength(1);
    expect(labelClickEvents[0]!.clientX).toBe(24);
    expect(labelClickEvents[0]!.clientY).toBe(48);
    expect(labelClickEvents[0]!.detail).toBe(1);
    expect(labelClickEvents[0]!.defaultPrevented).toBe(false);
    expect(inputClickEvents).toHaveLength(1);
    // WebKit's label default action refocuses without preventScroll after forwarding the click. Reapply it after
    // activation without emitting a new focus/blur cycle.
    expect(focusCallsDuringActivation).toBe(1);
    expect(focusSpy).toHaveBeenCalledTimes(2);
    expect(focusSpy).toHaveBeenLastCalledWith({ preventScroll: true });
    expect(onFocus).toHaveBeenCalledTimes(1);
    expect(onBlur).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(input);
  });

  it('preserves a focus change from a label click handler', async () => {
    const drawer = mount('<label data-testid="label" for="note">Note</label><input data-testid="input" id="note"><button data-testid="other" type="button">Other</button>');
    const label = drawer.get('label');
    const input = drawer.get('input');
    const other = drawer.get('other');
    label.addEventListener('click', (event) => {
      event.preventDefault();
      other.focus();
    });
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => label;

    touchStart(label, 24, 48);
    label.dispatchEvent(createNativeTouchEnd(label, { clientX: 24, clientY: 48 }));
    await flushMicrotasks();

    expect(document.activeElement).toBe(other);
    expect(focusSpy).toHaveBeenCalledTimes(1);
  });

  it('focuses a contentEditable element on touchend', async () => {
    const drawer = mount('<div data-testid="editor" contenteditable="">Edit me</div>');
    const editor = drawer.get('editor');
    const focusSpy = vi.spyOn(editor, 'focus');
    document.elementFromPoint = () => editor;

    touchStart(editor, 0, 0);
    const touchEnd = createNativeTouchEnd(editor, { clientX: 0, clientY: 0 });
    editor.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
  });

  it('focuses the editing host when tapping a nested element inside a contentEditable', async () => {
    const drawer = mount('<div data-testid="editor" contenteditable=""><span data-testid="inner">Edit me</span></div>');
    const editor = drawer.get('editor');
    const inner = drawer.get('inner');
    // Taps on rich-text content land on inherited-editable descendants, which are not focusable themselves; the editing
    // host must receive focus instead.
    document.elementFromPoint = () => inner;

    touchStart(inner, 0, 0);
    const touchEnd = createNativeTouchEnd(inner, { clientX: 0, clientY: 0 });
    inner.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(editor);
  });

  it('probes nearby points to resolve a keyboard input retargeted on touchend', async () => {
    const drawer = mount(`<div data-testid="wrapper" style="padding: 20px">${INPUT}</div>`);
    const wrapper = drawer.get('wrapper');
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    // The tap point resolves to the wrapper; only a point 16px below resolves to the input, so the input can only be found
    // via the hit-slop probe (not the native event target).
    document.elementFromPoint = ((_x: number, y: number) => (y === 16 ? input : wrapper)) as typeof document.elementFromPoint;

    touchStart(wrapper, 0, 0);
    const touchEnd = createNativeTouchEnd(wrapper, { clientX: 0, clientY: 0 });
    wrapper.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
  });

  it('does not focus a keyboard input when the touch moves past the tap threshold', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;

    touchStart(input, 0, 0);
    // Moving past INPUT_TAP_MOVE_THRESHOLD (10px) marks the gesture as a scroll, not a tap.
    touchMove(input, 0, 80);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 80 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();

    touchStart(input, 0, 0);
    touchMove(input, 0, 5);
    const shortMoveTouchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 5 });
    input.dispatchEvent(shortMoveTouchEnd);
    await flushMicrotasks();

    expect(shortMoveTouchEnd.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(input);
  });

  it('restores the previous scroll slack when focus moves to a different scroll container', async () => {
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(
      '<div data-testid="scroll1" style="height: 200px; overflow-y: auto; padding-bottom: 10px"><div style="height: 600px"></div><input data-testid="input1" type="text"></div>'
        + '<div data-testid="scroll2" style="height: 200px; overflow-y: auto; padding-bottom: 10px"><div style="height: 600px"></div><input data-testid="input2" type="text"></div>',
    );
    const scroll1 = drawer.get('scroll1');
    const scroll2 = drawer.get('scroll2');
    const input1 = drawer.get('input1');
    const input2 = drawer.get('input2');
    for (const scroll of [scroll1, scroll2]) {
      setScrollSize(scroll, 200, 800);
    }
    // Both containers extend below the reduced viewport bottom (500), so each needs slack.
    setRect(scroll1, () => rect(520, 40));
    setRect(scroll2, () => rect(560, 40));
    setRect(input1, () => rect(540, 40));
    setRect(input2, () => rect(580, 40));

    input1.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(Number.parseFloat(scroll1.style.paddingBottom)).toBeGreaterThan(10));

    input2.focus();

    await waitFor(() => expect(Number.parseFloat(scroll2.style.paddingBottom)).toBeGreaterThan(10));
    expect(scroll1.style.paddingBottom).toBe('10px');
    expect(scroll1.style.overflowAnchor).toBe('');
  });

  it('does not steal a tap that lands on an interactive element near a keyboard input', async () => {
    const drawer = mount(`${INPUT}<button data-testid="clear" type="button">Clear</button>`);
    const input = drawer.get('input');
    const clear = drawer.get('clear');
    const focusSpy = vi.spyOn(input, 'focus');
    // The tap lands exactly on the button; only the hit-slop probe points resolve to the input. The probe must not steal
    // the button's tap.
    document.elementFromPoint = ((x: number, y: number) => (x === 0 && y === 0 ? clear : input)) as typeof document.elementFromPoint;

    touchStart(clear, 0, 0);
    const touchEnd = createNativeTouchEnd(clear, { clientX: 0, clientY: 0 });
    clear.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('preserves native taps on keyboard inputs while pinch-zoomed', async () => {
    const visualViewport = mockVisualViewport(800);
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    const focusSpy = vi.spyOn(input, 'focus');
    document.elementFromPoint = () => input;
    visualViewport.setScale(1.5);

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('restores slack from a removed scroll container before adjusting a new one', async () => {
    const visualViewport = mockVisualViewport(500);
    const drawer = mount(
      '<div data-testid="scroll1" style="overflow-y: auto; padding-bottom: 10px"><input data-testid="input1" type="text"></div>'
        + '<div data-testid="scroll2" style="overflow-y: auto"><input data-testid="input2" type="text"></div>',
    );
    const scroll1 = drawer.get('scroll1');
    const scroll2 = drawer.get('scroll2');
    const input1 = drawer.get('input1');
    const input2 = drawer.get('input2');
    for (const scroll of [scroll1, scroll2]) {
      setScrollSize(scroll, 200, 800);
      setRect(scroll, () => rect(300, 400));
    }
    setRect(input1, () => rect(620, 40));
    setRect(input2, () => rect(620, 40));

    input1.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(Number.parseFloat(scroll1.style.paddingBottom)).toBeGreaterThan(10));

    Object.defineProperty(scroll1, 'isConnected', { configurable: true, value: false });
    input2.focus();

    await waitFor(() => expect(Number.parseFloat(scroll2.style.paddingBottom)).toBeGreaterThan(0));
    expect(scroll1.style.paddingBottom).toBe('10px');
    expect(scroll1.style.overflowAnchor).toBe('');
  });

  it('clears inset and slack when the focused field is removed', async () => {
    const visualViewport = mockVisualViewport(500);
    const drawer = mount('<div data-testid="scroll" style="overflow-y: auto; padding-bottom: 10px"><input data-testid="input" type="text"></div>');
    const scroll = drawer.get('scroll');
    const input = drawer.get('input');
    setScrollSize(scroll, 200, 800);
    setRect(scroll, () => rect(300, 400));
    setRect(input, () => rect(620, 40));

    input.focus();
    visualViewport.resize(500);

    await waitFor(() => expect(Number.parseFloat(scroll.style.paddingBottom)).toBeGreaterThan(10));

    const nativeContains = drawer.viewport.contains.bind(drawer.viewport);
    drawer.viewport.contains = (candidate) => (candidate === input ? false : nativeContains(candidate));
    visualViewport.resize(490);
    await flushAnimationFrames(2);

    expect(inset(drawer.viewport)).toBe('0px');
    expect(scroll.style.paddingBottom).toBe('10px');
  });

  it('captures an initially focused input without attempting an unnecessary page restore', async () => {
    mockVisualViewport(500);
    const scrollToSpy = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    // `autoFocus`: the input holds focus when the drawer's handle attaches.
    const drawer = mount('<input autofocus data-testid="input" type="text">', { modal: true }, document.body, (root) => root.get('input').focus());

    expect(document.activeElement).toBe(drawer.get('input'));
    await waitFor(() => expect(inset(drawer.viewport)).toBe('300px'));
    expect(scrollToSpy).not.toHaveBeenCalled();
  });

  // Base UI drives the provider's touch handlers straight from an input under `useDrawerVirtualKeyboardContext`; the port
  // has no such context, so the touches go through the Viewport handle, which forwards them as Base UI's Viewport does.
  it('ignores a directly captured touch target while a closed drawer remains mounted', async () => {
    const drawer = mount(`${INPUT}<button type="button">Close</button>`);
    const input = drawer.get('input');
    document.elementFromPoint = () => input;
    // A Close press whose close is kept mounted (`preventUnmountOnClose()`): the drawer is closed and still mounted.
    drawer.update({ open: false, mounted: true });

    touchStart(input, 0, 0);
    fireTouch(input, 'touchmove', []);
    input.dispatchEvent(createNativeTouchEnd(input, { clientX: 0, clientY: 0 }));

    expect(document.activeElement).not.toBe(input);
  });

  it('does not focus a lift-point target outside the drawer', async () => {
    const outsideWrapper = document.createElement('div');
    outsideWrapper.innerHTML = '<input data-testid="outside" type="text">';
    document.body.append(outsideWrapper);
    elements.push(outsideWrapper);
    const drawer = mount('<input data-testid="inside" type="text">');
    const inside = drawer.get('inside');
    const outside = outsideWrapper.querySelector<HTMLElement>('[data-testid="outside"]')!;
    document.elementFromPoint = () => outside;

    touchStart(inside, 0, 0);
    inside.dispatchEvent(createNativeTouchEnd(inside, { clientX: 0, clientY: 0 }));

    expect(document.activeElement).not.toBe(inside);
    expect(document.activeElement).not.toBe(outside);
  });

  it('falls back to the touch target when point lookup misses', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    document.elementFromPoint = () => null;

    touchStart(input, 0, 0);
    const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
    input.dispatchEvent(touchEnd);

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(input);
  });

  it('uses the remaining touch and MouseEvent fallback for keyboard taps', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    const pointerEventDescriptor = Object.getOwnPropertyDescriptor(window, 'PointerEvent');
    document.elementFromPoint = () => null;

    touchStart(input, 10, 20);
    Object.defineProperty(window, 'PointerEvent', { configurable: true, value: undefined });
    restores.push(() => {
      if (pointerEventDescriptor) {
        Object.defineProperty(window, 'PointerEvent', pointerEventDescriptor);
      }
    });
    const touchEnd = new Event('touchend', { bubbles: true, cancelable: true });
    Object.defineProperties(touchEnd, {
      changedTouches: { configurable: true, value: [] },
      touches: { configurable: true, value: touchList(input, { clientX: 10, clientY: 20 }) },
    });
    input.dispatchEvent(touchEnd);

    expect(touchEnd.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(input);
  });

  it('ignores a keyboard tap whose native target is not an element', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');
    document.elementFromPoint = () => null;

    touchStart(input, 10, 20);
    const touchEnd = createNativeTouchEnd(input, { clientX: 10, clientY: 20 });
    touchEnd.composedPath = () => [window];
    input.dispatchEvent(touchEnd);

    expect(touchEnd.defaultPrevented).toBe(false);
    expect(document.activeElement).not.toBe(input);
  });

  it('ignores touchend without a lift coordinate', async () => {
    const drawer = mount(INPUT);
    const input = drawer.get('input');

    touchStart(input, 10, 20);
    const touchEnd = new Event('touchend', { bubbles: true, cancelable: true });
    Object.defineProperties(touchEnd, {
      changedTouches: { configurable: true, value: [] },
      touches: { configurable: true, value: [] },
    });
    input.dispatchEvent(touchEnd);
    await flushMicrotasks();

    expect(document.activeElement).not.toBe(input);
  });

  it('does not align inputs when visualViewport is unavailable', async () => {
    const originalDescriptor = Object.getOwnPropertyDescriptor(window, 'visualViewport');
    Object.defineProperty(window, 'visualViewport', { configurable: true, value: undefined });
    restores.push(() => {
      if (originalDescriptor) {
        Object.defineProperty(window, 'visualViewport', originalDescriptor);
      }
    });
    const drawer = mount(INPUT);

    drawer.get('input').focus();
    await flushAnimationFrames(2);

    expect(inset(drawer.viewport)).toBe('0px');
  });

  describe('layout viewport resized for the keyboard (Chrome on iOS)', () => {
    function mockResizableInnerHeight(initialHeight: number) {
      const originalDescriptor = Object.getOwnPropertyDescriptor(window, 'innerHeight')!;
      let innerHeight = initialHeight;
      Object.defineProperty(window, 'innerHeight', { configurable: true, get: () => innerHeight });
      restores.push(() => Object.defineProperty(window, 'innerHeight', originalDescriptor));
      return {
        resize(nextHeight: number) {
          innerHeight = nextHeight;
          window.dispatchEvent(new Event('resize'));
        },
      };
    }

    function renderKeyboardDrawer() {
      const drawer = mount(SCROLL_BODY('height: 420px; overflow-y: auto; padding-bottom: 20px', TALL + INPUT));
      // The browser lays the 900px body out inside the 420px scroller.
      setScrollSize(drawer.get('scroll'), 420, 1200);
      return drawer;
    }

    // New Chrome on iOS shrinks `svh` for the keyboard before resizing the layout viewport.
    function mockSmallViewportHeight(initialHeight: number) {
      const originalDescriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'offsetHeight')!;
      let smallViewportHeight = initialHeight;
      Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
        configurable: true,
        get(this: HTMLElement) {
          return this.style.height === '100svh' ? smallViewportHeight : originalDescriptor.get!.call(this);
        },
      });
      restores.push(() => Object.defineProperty(HTMLElement.prototype, 'offsetHeight', originalDescriptor));
      return {
        set(nextHeight: number) {
          smallViewportHeight = nextHeight;
        },
      };
    }

    function mockScrollGeometry(scroll: HTMLElement, input: HTMLElement) {
      setScrollSize(scroll, 380, 1200);
      // The shrunken viewport keeps the scroller above the keyboard.
      setRect(scroll, () => new DOMRect(0, 100, 320, 380));
      setRect(input, () => new DOMRect(0, 650 - scroll.scrollTop, 320, 40));
    }

    it('applies no keyboard inset or slack when the small viewport follows the keyboard', async () => {
      const innerHeight = mockResizableInnerHeight(800);
      const visualViewport = mockVisualViewport(800);
      const smallViewport = mockSmallViewportHeight(800);
      vi.useFakeTimers();
      const drawer = renderKeyboardDrawer();
      const scroll = drawer.get('scroll');
      const input = drawer.get('input');
      // The scroller lays out inside the layout viewport; against the full-height one, the keyboard would overlap its bottom
      // 220px.
      setRect(scroll, () => new DOMRect(0, window.innerHeight - 500, 320, 420));

      input.focus();
      smallViewport.set(500);
      visualViewport.resize(500);
      vi.advanceTimersToNextFrame();

      expect(inset(drawer.viewport)).toBe('0px');
      expect(scroll.style.paddingBottom).toBe('20px');

      innerHeight.resize(650);
      vi.advanceTimersToNextFrame();

      expect(inset(drawer.viewport)).toBe('0px');
      expect(scroll.style.paddingBottom).toBe('20px');
    });

    it('aligns the focused field once the layout viewport has shrunk to the visual viewport', async () => {
      const innerHeight = mockResizableInnerHeight(800);
      const visualViewport = mockVisualViewport(800);
      vi.useFakeTimers();
      const drawer = renderKeyboardDrawer();
      const scroll = drawer.get('scroll');
      const input = drawer.get('input');
      mockScrollGeometry(scroll, input);
      scrollInstantly(scroll);

      // The visual viewport shrinks first; the layout viewport follows a frame later.
      input.focus();
      scroll.scrollTop = 0;
      visualViewport.resize(500);
      vi.advanceTimersToNextFrame();

      innerHeight.resize(500);
      await vi.advanceTimersByTimeAsync(200);

      const inputRect = input.getBoundingClientRect();
      expect((inputRect.top + inputRect.bottom) / 2).toBeCloseTo((100 + 480) / 2, 0);
      expect(inset(drawer.viewport)).toBe('0px');
      expect(scroll.style.paddingBottom).toBe('20px');
    });

    it('scrolls the focused field once the layout viewport reaches the visual viewport', async () => {
      const innerHeight = mockResizableInnerHeight(800);
      const visualViewport = mockVisualViewport(800);
      const smallViewport = mockSmallViewportHeight(800);
      vi.useFakeTimers();
      const drawer = renderKeyboardDrawer();
      const scroll = drawer.get('scroll');
      const input = drawer.get('input');
      mockScrollGeometry(scroll, input);
      const scrollToSpy = vi.spyOn(scroll, 'scrollTo').mockImplementation(() => {});

      input.focus();
      smallViewport.set(500);
      visualViewport.resize(500);
      vi.advanceTimersToNextFrame();

      // The layout viewport can pause on its way down.
      innerHeight.resize(700);
      vi.advanceTimersToNextFrame();
      innerHeight.resize(600);
      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextFrame();

      expect(scrollToSpy).not.toHaveBeenCalled();

      innerHeight.resize(500);
      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextFrame();

      expect(scrollToSpy).toHaveBeenCalledTimes(1);
    });

    it('waits for the layout viewport to grow to a shorter keyboard before scrolling', async () => {
      const innerHeight = mockResizableInnerHeight(800);
      const visualViewport = mockVisualViewport(800);
      const smallViewport = mockSmallViewportHeight(800);
      vi.useFakeTimers();
      const drawer = renderKeyboardDrawer();
      const scroll = drawer.get('scroll');
      const input = drawer.get('input');
      mockScrollGeometry(scroll, input);
      const scrollToSpy = vi.spyOn(scroll, 'scrollTo').mockImplementation(() => {});

      input.focus();
      smallViewport.set(500);
      visualViewport.resize(500);
      vi.advanceTimersToNextFrame();

      innerHeight.resize(500);
      await vi.advanceTimersByTimeAsync(2000);

      expect(scrollToSpy).toHaveBeenCalledTimes(1);
      scrollToSpy.mockClear();

      // The keyboard gets shorter while staying open; the layout viewport follows later.
      smallViewport.set(540);
      visualViewport.resize(540);
      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextFrame();

      expect(scrollToSpy).not.toHaveBeenCalled();

      innerHeight.resize(540);
      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextFrame();

      expect(scrollToSpy).toHaveBeenCalledTimes(1);
    });

    it('does not treat a plain resize of both viewports as the software keyboard', async () => {
      const innerHeight = mockResizableInnerHeight(800);
      const visualViewport = mockVisualViewport(800);
      vi.useFakeTimers();
      const drawer = renderKeyboardDrawer();
      const scroll = drawer.get('scroll');
      const input = drawer.get('input');
      mockScrollGeometry(scroll, input);
      const scrollToSpy = vi.spyOn(scroll, 'scrollTo').mockImplementation(() => {});

      input.focus();
      await vi.advanceTimersByTimeAsync(100);

      // A window resize (or rotation) moves both viewports together.
      visualViewport.resize(500);
      innerHeight.resize(500);
      await vi.advanceTimersByTimeAsync(200);

      expect(scrollToSpy).not.toHaveBeenCalled();
      expect(['', '0px']).toContain(inset(drawer.viewport));
      expect(scroll.style.paddingBottom).toBe('20px');
    });

    it('clears the keyboard inset and slack once the layout viewport catches up with the visual viewport', async () => {
      const innerHeight = mockResizableInnerHeight(800);
      const visualViewport = mockVisualViewport(800);
      vi.useFakeTimers();
      const drawer = renderKeyboardDrawer();
      const scroll = drawer.get('scroll');
      const input = drawer.get('input');
      // Place the scroller so the keyboard overlaps its bottom 220px.
      let scrollBottom = 720;
      setRect(scroll, () => new DOMRect(0, scrollBottom - 420, 320, 420));

      // Let the alignment passes finish so only the resize can re-measure.
      input.focus();
      visualViewport.resize(500);
      await vi.advanceTimersByTimeAsync(2000);

      expect(inset(drawer.viewport)).toBe('300px');
      expect(scroll.style.paddingBottom).toBe('240px');

      // The layout viewport catches up and the fixed drawer lays out inside it.
      scrollBottom = 480;
      innerHeight.resize(500);
      vi.advanceTimersToNextFrame();

      expect(inset(drawer.viewport)).toBe('0px');
      expect(scroll.style.paddingBottom).toBe('20px');
    });

    it('preserves native taps while the keyboard is open and refocuses once it closes', async () => {
      const innerHeight = mockResizableInnerHeight(800);
      const visualViewport = mockVisualViewport(800);
      vi.useFakeTimers();
      const drawer = renderKeyboardDrawer();
      const input = drawer.get('input');
      const tap = () => {
        touchStart(input, 0, 0);
        const touchEnd = createNativeTouchEnd(input, { clientX: 0, clientY: 0 });
        input.dispatchEvent(touchEnd);
        return touchEnd;
      };

      input.focus();
      visualViewport.resize(500);
      vi.advanceTimersToNextFrame();
      innerHeight.resize(500);
      await vi.advanceTimersByTimeAsync(200);
      const focusSpy = vi.spyOn(input, 'focus');
      document.elementFromPoint = () => input;

      // The overlap is gone, but the keyboard is still open.
      expect(tap().defaultPrevented).toBe(false);
      expect(focusSpy).not.toHaveBeenCalled();

      visualViewport.resize(800);
      innerHeight.resize(800);
      await vi.advanceTimersByTimeAsync(200);

      expect(tap().defaultPrevented).toBe(true);
      expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
    });
  });
});
