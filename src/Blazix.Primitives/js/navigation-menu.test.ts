// L3 (ADR 06 §1): the `navigation-menu` component module in jsdom against the fake twin. Each case builds the DOM C#
// renders (the Root, the List and its Items with Triggers, the Positioner, Popup and Viewport with the Contents the
// portal module moved into it), attaches the handles as the twins would, and plays C#'s batch with `commit` (the
// Root's `data-open`, the Triggers' `data-popup-open` and guards, the Contents' `data-open`/`data-closed`/`hidden`, the
// Root's `mounted`). The size titles are Base UI's (NavigationMenuRoot.test.tsx, NavigationMenuTrigger.test.tsx) with
// its stubs (offset sizes, `getAnimations`, `ResizeObserver` removed); the rest are port titles. Where Server and
// WebAssembly take different paths (the activation's report or synchronous invocation), the titles run on both.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { acquireSession, releaseSession } from './interactions';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';
import { createPopupHarness } from './test-support/interactions-harness';
import type { DotNetTwin, TwinHandle } from './twin';

const flags = vi.hoisted(() => ({ voiceOver: false, webkit: false }));

vi.mock('./platform', async (importOriginal) => {
  const original = await importOriginal<typeof import('./platform')>();
  return {
    ...original,
    platform: {
      ...original.platform,
      engine: {
        ...original.platform.engine,
        get webkit() {
          return flags.webkit;
        },
      },
      screenReader: {
        get voiceOver() {
          return flags.voiceOver;
        },
      },
    },
  };
});

const {
  attach,
  computeActivationDirection,
  getPhysicalSide,
  isListGuardKey,
  isOpenKey,
  ACTIVATE_REPORT,
  ARROW_PART,
  FOCUS_OUT_REPORT,
  NAVIGATE_POINT,
  RESIZE_REPORT,
  VIEWPORT_EXIT_REPORT,
  VIEWPORT_INERT_REPORT,
} = await import('./navigation-menu');
const { resetFrameScheduler } = await import('./transitions');
const { styleWriter } = await import('./dom');
type ActivationReport = import('./navigation-menu').ActivationReport;

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
  flags.voiceOver = false;
  flags.webkit = false;
});

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  resetFrameScheduler();
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

async function flushMicrotasks(): Promise<void> {
  for (let i = 0; i < 8; i++) {
    await Promise.resolve();
  }
}

// Microtasks, then frames, as often as asked: the frames a pass requests and the microtasks they queue.
async function settle(rounds = 4): Promise<void> {
  for (let i = 0; i < rounds; i++) {
    await flushMicrotasks();
    flushFrames();
  }
  await flushMicrotasks();
}

// NavigationMenuRoot.test.tsx:696-737.
function mockAnimations(element: HTMLElement) {
  type MockAnimation = { finished: Promise<void>; resolveFinished: (() => void) | null };
  const createAnimation = (): MockAnimation => {
    let resolveFinished: (() => void) | null = null;
    const finished = new Promise<void>((resolve) => {
      resolveFinished = resolve;
    });
    return { finished, resolveFinished };
  };
  let current = createAnimation();
  let active: MockAnimation[] = [];
  Object.defineProperty(element, 'getAnimations', {
    configurable: true,
    value: () => active.map((animation) => ({ finished: animation.finished })),
  });
  return {
    start() {
      current = createAnimation();
      active.push(current);
      return current;
    },
    finish(animation: MockAnimation = current) {
      const finished = animation.finished;
      animation.resolveFinished?.();
      animation.resolveFinished = null;
      active = active.filter((item) => item !== animation);
      return finished;
    },
  };
}

function mockOffsetSize(element: HTMLElement, size: () => { width: number; height: number }): void {
  mockOffset(element, () => size().width, () => size().height);
}

function mockOffset(element: HTMLElement, width: () => number, height: () => number): void {
  Object.defineProperty(element, 'offsetWidth', { configurable: true, get: width });
  Object.defineProperty(element, 'offsetHeight', { configurable: true, get: height });
}

function mockRect(element: Element, rect: { x: number; y: number; width: number; height: number }): void {
  Object.defineProperty(element, 'getBoundingClientRect', {
    configurable: true,
    value: () => ({ ...rect, top: rect.y, left: rect.x, right: rect.x + rect.width, bottom: rect.y + rect.height, toJSON: () => ({}) }),
  });
}

function getPopupSizeVars(popup: HTMLElement, positioner: HTMLElement) {
  return {
    popupWidth: popup.style.getPropertyValue('--popup-width'),
    popupHeight: popup.style.getPropertyValue('--popup-height'),
    positionerWidth: positioner.style.getPropertyValue('--positioner-width'),
    positionerHeight: positioner.style.getPropertyValue('--positioner-height'),
  };
}

// Base UI's `primeOpenPopupSize` (NavigationMenuRoot.test.tsx:754-764), through the style writer that owns both styles.
function primeSize(popup: HTMLElement, positioner: HTMLElement, popupSize: string | number, width: number, height: number): void {
  const popupLayer = styleWriter(popup).layer('auto-resize');
  const positionerLayer = styleWriter(positioner).layer('auto-resize');
  popupLayer.set('--popup-width', typeof popupSize === 'number' ? `${popupSize}px` : popupSize);
  popupLayer.set('--popup-height', typeof popupSize === 'number' ? `${height}px` : popupSize);
  positionerLayer.set('--positioner-width', `${width}px`);
  positionerLayer.set('--positioner-height', `${height}px`);
}

interface Item {
  value: string;
  li: HTMLElement;
  trigger: HTMLButtonElement;
  content: HTMLElement | null;
}

interface RenderOptions {
  values?: string[];
  keepMounted?: boolean;
  positioner?: boolean;
  nested?: boolean;
  orientation?: 'horizontal' | 'vertical';
  rtl?: boolean;
  synchronous?: boolean;
  sessionToken?: string;
  open?: string | null;
  verdict?: 'run' | 'stop';
  /** `false` leaves the Positioner's and Popup's handles unattached, as on Server before their attach arrives. */
  popupHandles?: boolean;
}

interface Rendered {
  root: HTMLElement;
  list: HTMLElement;
  items: Item[];
  positioner: HTMLElement | null;
  popup: HTMLElement | null;
  viewport: HTMLElement;
  target: HTMLElement;
  viewportGuards: { before: HTMLElement; after: HTMLElement } | null;
  value: string | null;
  mounted: boolean;
  closeReason: string | null;
  answered: number;
  twin: FakeTwin;
  dotnet: DotNetTwin;
  activations: ActivationReport[];
  /** What C# does with a synchronous activation; `commit` by default. */
  onActivate: (payload: ActivationReport, item: Item) => void;
  item(value: string): Item;
  commit(value: string | null, options?: { mounted?: boolean; closeReason?: string | null }): void;
  updateRoot(): void;
}

function createGuard(): HTMLElement {
  const guard = document.createElement('span');
  guard.tabIndex = 0;
  guard.setAttribute('data-base-ui-focus-guard', '');
  guard.setAttribute('aria-hidden', 'true');
  return guard;
}

function renderMenu(options: RenderOptions = {}): Rendered {
  const values = options.values ?? ['item-1', 'item-2'];
  const hasPositioner = options.positioner ?? true;
  const root = document.createElement(options.nested ? 'div' : 'nav');
  const list = document.createElement('ul');
  root.appendChild(list);
  document.body.appendChild(root);

  const after = document.createElement('button');
  after.textContent = 'after';
  after.setAttribute('data-testid', 'after');
  document.body.appendChild(after);

  let positioner: HTMLElement | null = null;
  let popup: HTMLElement | null = null;
  const viewport = document.createElement('div');
  viewport.id = 'viewport-1';
  let target = viewport;
  let viewportGuards: Rendered['viewportGuards'] = null;
  if (hasPositioner) {
    positioner = document.createElement('div');
    positioner.setAttribute('data-testid', 'positioner');
    popup = document.createElement('nav');
    popup.id = 'popup-1';
    popup.tabIndex = -1;
    popup.setAttribute('data-testid', 'popup-root');
    viewportGuards = { before: createGuard(), after: createGuard() };
    popup.append(viewportGuards.before, viewport, viewportGuards.after);
    positioner.appendChild(popup);
    document.body.appendChild(positioner);
  } else {
    target = document.createElement('div');
    viewport.appendChild(target);
    list.parentElement!.appendChild(viewport);
  }

  const items: Item[] = values.map((value) => {
    const li = document.createElement('li');
    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.textContent = value;
    trigger.setAttribute('data-testid', `trigger-${value}`);
    trigger.setAttribute('data-base-ui-navigation-menu-trigger', '');
    trigger.setAttribute('aria-expanded', 'false');
    li.appendChild(trigger);
    list.appendChild(li);
    let content: HTMLElement | null = null;
    if (options.keepMounted) {
      content = createContent(value);
      content.hidden = true;
      content.setAttribute('data-closed', '');
      target.appendChild(content);
    }
    return { value, li, trigger, content };
  });

  const fake = createFakeTwin({ synchronous: options.synchronous ?? false, verdict: options.verdict ?? 'run' });
  const activations: ActivationReport[] = [];
  const rendered: Rendered = {
    root,
    list,
    items,
    positioner,
    popup,
    viewport,
    target,
    viewportGuards,
    value: null,
    mounted: false,
    closeReason: null,
    answered: 0,
    twin: fake,
    dotnet: undefined as unknown as DotNetTwin,
    activations,
    onActivate(payload, item) {
      if (payload.reason !== null) {
        rendered.commit(item.value);
      }
    },
    item(value) {
      return items.find((candidate) => candidate.value === value)!;
    },
    commit(value, commitOptions = {}) {
      rendered.value = value;
      root.toggleAttribute('data-open', value !== null);
      popup?.toggleAttribute('data-open', value !== null);
      popup?.toggleAttribute('data-closed', value === null);
      for (const item of items) {
        const active = item.value === value;
        if (item.trigger.hasAttribute('data-popup-open') !== active) {
          item.trigger.toggleAttribute('data-popup-open', active);
          item.trigger.setAttribute('aria-expanded', String(active));
          if (active) {
            const owner = document.createElement('span');
            owner.setAttribute('aria-owns', viewport.id);
            item.trigger.after(createGuard(), owner, createGuard());
          } else {
            while (item.trigger.nextElementSibling !== null && item.trigger.nextElementSibling.tagName === 'SPAN') {
              item.trigger.nextElementSibling.remove();
            }
          }
        }
        if (active && item.content === null) {
          item.content = createContent(item.value);
          target.appendChild(item.content);
        }
        if (item.content !== null) {
          item.content.toggleAttribute('data-open', active);
          item.content.toggleAttribute('data-closed', !active);
          if (active) {
            item.content.hidden = false;
          } else if (options.keepMounted) {
            item.content.hidden = true;
          }
        }
      }
      rendered.mounted = commitOptions.mounted ?? (value !== null || rendered.mounted);
      if (commitOptions.closeReason !== undefined) {
        rendered.closeReason = commitOptions.closeReason;
      }
      rendered.updateRoot();
    },
    updateRoot() {
      rootHandle.update(rootOptions());
    },
  };

  rendered.dotnet = {
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      if (methodName === ACTIVATE_REPORT) {
        const payload = JSON.parse(args[0] as string) as ActivationReport;
        activations.push(payload);
        const item = items.find((candidate) => candidate.trigger === activating)!;
        rendered.onActivate(payload, item);
        return undefined as T;
      }
      return fake.twin.invokeMethod<T>(methodName, ...args);
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      return fake.twin.invokeMethodAsync<T>(methodName, ...args);
    },
  };

  // The Trigger whose handle is dispatching: the synchronous invocation carries no element, as C#'s twin knows its own.
  let activating: HTMLElement | null = null;
  for (const item of items) {
    item.trigger.addEventListener('mouseover', () => (activating = item.trigger), true);
    item.trigger.addEventListener('click', () => (activating = item.trigger), true);
    item.trigger.addEventListener('keydown', () => (activating = item.trigger), true);
  }

  const rootOptions = () =>
    JSON.stringify({
      part: 'root',
      orientation: options.orientation ?? 'horizontal',
      rtl: options.rtl ?? false,
      nested: options.nested ?? false,
      mounted: rendered.mounted,
      closeReason: rendered.closeReason,
      sessionToken: options.sessionToken ?? null,
      answered: rendered.answered,
    });
  const part = (name: string, extra: object = {}) => JSON.stringify({ part: name, rootRef: 0, ...extra });

  const rootHandle = attach(root, rootOptions(), rendered.dotnet);
  handles.push(rootHandle);
  for (const item of items) {
    handles.push(attach(item.trigger, part('trigger'), rendered.dotnet, [root]));
  }
  if (!options.nested) {
    handles.push(attach(list, part('list'), rendered.dotnet, [root]));
  }
  if (positioner !== null && popup !== null && options.popupHandles !== false) {
    handles.push(attach(positioner, part('positioner'), rendered.dotnet, [root]));
    handles.push(attach(popup, part('popup'), rendered.dotnet, [root]));
  }
  handles.push(attach(viewport, part('viewport', { hasPositioner }), rendered.dotnet, [root]));

  if (options.open != null) {
    rendered.commit(options.open);
  }
  return rendered;
}

function createContent(value: string): HTMLElement {
  const content = document.createElement('div');
  content.setAttribute('data-testid', `content-${value}`);
  const link = document.createElement('a');
  link.href = `#${value}`;
  link.textContent = `${value} link`;
  content.appendChild(link);
  return content;
}

/** A pointer entering an element, as the browser dispatches it: `mouseover` (bubbling) and then `mouseenter`. */
function mouseEnter(element: HTMLElement): void {
  element.dispatchEvent(new MouseEvent('mouseover', { bubbles: true, relatedTarget: null }));
  element.dispatchEvent(new MouseEvent('mouseenter', { bubbles: false }));
}

function click(element: HTMLElement): void {
  element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
}

function keyDown(element: HTMLElement, key: string): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
  element.dispatchEvent(event);
  return event;
}

// The batch C# sends after an activation report on Server.
async function answer(rendered: Rendered, value: string): Promise<void> {
  rendered.commit(value);
  await flushMicrotasks();
}

// ---------------------------------------------------------------------------------------------------------------
// Pure helpers
// ---------------------------------------------------------------------------------------------------------------

describe('activation direction', () => {
  const box = (x: number, y: number) => ({ left: x, top: y }) as DOMRect;

  it.each([
    ['right', box(0, 0), box(100, 0), 'horizontal'],
    ['left', box(100, 0), box(0, 0), 'horizontal'],
    ['down', box(0, 0), box(0, 40), 'vertical'],
    ['up', box(0, 40), box(0, 0), 'vertical'],
  ] as const)('reports %s activation direction when switching in either direction', (expected, previous, next, orientation) => {
    expect(computeActivationDirection(previous, next, orientation)).toBe(expected);
  });

  it('keeps the direction when the compared coordinate is equal, and reports none without a previous trigger', () => {
    expect(computeActivationDirection(box(0, 0), box(0, 40), 'horizontal')).toBe(null);
    expect(computeActivationDirection(null, box(0, 40), 'vertical')).toBe(null);
  });
});

describe('keys', () => {
  it('opens with ArrowDown when horizontal and with the mirrored arrow when vertical in RTL mode', () => {
    expect(isOpenKey('ArrowDown', 'horizontal', false)).toBe(true);
    expect(isOpenKey('ArrowRight', 'vertical', false)).toBe(true);
    expect(isOpenKey('ArrowLeft', 'vertical', true)).toBe(true);
    expect(isOpenKey('ArrowRight', 'horizontal', false)).toBe(false);
    expect(isOpenKey('ArrowDown', 'vertical', false)).toBe(false);
  });

  it("guards the List's axis arrows only", () => {
    expect(isListGuardKey('ArrowLeft', 'horizontal')).toBe(true);
    expect(isListGuardKey('ArrowDown', 'vertical')).toBe(true);
    expect(isListGuardKey('ArrowDown', 'horizontal')).toBe(false);
    expect(isListGuardKey('PageDown', 'vertical')).toBe(false);
  });
});

describe('the Popup anchoring side', () => {
  it.each([
    ['top', false, 'top'],
    ['left', false, 'left'],
    ['inline-start', false, 'left'],
    ['inline-end', true, 'left'],
    ['inline-end', false, 'bottom'],
    ['bottom', false, 'bottom'],
  ] as const)('pins the popup for side %s (rtl %s) to the %s edge', (side, rtl, expected) => {
    expect(getPhysicalSide(side, rtl)).toBe(expected);
  });

  it('pins the popup to the right edge when side is left so it grows leftward', async () => {
    const rendered = renderMenu();
    rendered.positioner!.setAttribute('data-side', 'left');
    await settle();
    expect(rendered.popup!.style.position).toBe('absolute');
    expect(rendered.popup!.style.top).toBe('0px');
    expect(rendered.popup!.style.right).toBe('0px');
    rendered.positioner!.setAttribute('data-side', 'bottom');
    await settle();
    expect(rendered.popup!.style.position).toBe('');
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The activation path, on both modes
// ---------------------------------------------------------------------------------------------------------------

describe.each([false, true])('the activation (synchronous: %s)', (synchronous) => {
  it('requests a hover switch with trigger-hover and the direction from the previous trigger', async () => {
    const rendered = renderMenu({ synchronous, open: 'item-1' });
    mockRect(rendered.item('item-1').trigger, { x: 0, y: 0, width: 80, height: 30 });
    mockRect(rendered.item('item-2').trigger, { x: 100, y: 0, width: 80, height: 30 });
    await settle();

    mouseEnter(rendered.item('item-2').trigger);
    const reports = synchronous ? rendered.activations : rendered.twin.reported<ActivationReport>(ACTIVATE_REPORT);
    expect(reports).toHaveLength(1);
    expect(reports[0]).toMatchObject({ reason: 'trigger-hover', direction: 'right' });
    expect(reports[0]!.event.type).toBe('mouseover');
  });

  it("activates on the trigger's mouseover, before the entry's mouseenter listeners run", async () => {
    const rendered = renderMenu({ synchronous, open: 'item-1' });
    mockRect(rendered.item('item-1').trigger, { x: 0, y: 0, width: 80, height: 30 });
    mockRect(rendered.item('item-2').trigger, { x: 100, y: 0, width: 80, height: 30 });
    await settle();
    const reports = () => (synchronous ? rendered.activations : rendered.twin.reported<ActivationReport>(ACTIVATE_REPORT));
    let reportedBeforeEnter = -1;
    // Stands in for the hover interaction's listener, attached before this handle, which on WebAssembly switches the
    // menu inside the dispatch: a capture listener runs before every listener on the target.
    const target = rendered.item('item-2').trigger;
    const onEnter = (event: Event) => {
      if (event.target === target) {
        reportedBeforeEnter = reports().length;
      }
    };
    document.addEventListener('mouseenter', onEnter, true);

    mouseEnter(target);
    document.removeEventListener('mouseenter', onEnter, true);

    expect(reportedBeforeEnter).toBe(1);
    expect(reports()[0]).toMatchObject({ reason: 'trigger-hover', direction: 'right' });
  });

  it("resets the session's recorded open event on a hover switch, and keeps it on a click switch", async () => {
    const session = acquireSession('nav-session');
    try {
      const rendered = renderMenu({ synchronous, open: 'item-1', sessionToken: 'nav-session' });
      await settle();
      const opened = new MouseEvent('click');
      session.dataRef.current.openEvent = opened;

      click(rendered.item('item-2').trigger);
      expect(session.dataRef.current.openEvent).toBe(opened);

      mouseEnter(rendered.item('item-1').trigger);
      expect(session.dataRef.current.openEvent).toBeUndefined();
    } finally {
      releaseSession(session);
    }
  });

  it('requests a click switch with trigger-press and nothing for a closed menu', async () => {
    const rendered = renderMenu({ synchronous });
    await settle();
    click(rendered.item('item-1').trigger);
    expect(synchronous ? rendered.activations : rendered.twin.reported(ACTIVATE_REPORT)).toHaveLength(0);

    rendered.commit('item-1');
    await settle();
    click(rendered.item('item-2').trigger);
    const reports = synchronous ? rendered.activations : rendered.twin.reported<ActivationReport>(ACTIVATE_REPORT);
    expect(reports).toHaveLength(1);
    expect(reports[0]!.reason).toBe('trigger-press');
  });

  it('opens with the open key once, with list-navigation, preventing and stopping it', async () => {
    const rendered = renderMenu({ synchronous });
    await settle();
    const outer = vi.fn();
    rendered.root.addEventListener('keydown', outer);

    const event = keyDown(rendered.item('item-1').trigger, 'ArrowDown');

    const reports = synchronous ? rendered.activations : rendered.twin.reported<ActivationReport>(ACTIVATE_REPORT);
    expect(reports).toHaveLength(1);
    expect(reports[0]!.reason).toBe('list-navigation');
    expect(event.defaultPrevented).toBe(true);
    expect(outer).not.toHaveBeenCalled();
  });

  it('runs the size transition with the size measured before the switch once the new Content is in place', async () => {
    const rendered = renderMenu({ synchronous, keepMounted: true });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    let size = { width: 675, height: 220 };
    mockOffsetSize(popup, () => size);
    rendered.commit('item-1');
    await settle();
    primeSize(popup, positioner, 'auto', 675, 220);
    // C#'s batch swaps the Content, which changes the Popup's size.
    rendered.onActivate = (_payload, item) => {
      size = { width: 500, height: 180 };
      rendered.commit(item.value);
    };

    click(rendered.item('item-2').trigger);
    if (!synchronous) {
      // Server: nothing moves until the batch shows the new Content.
      expect(positioner.style.getPropertyValue('--positioner-width')).toBe('675px');
      size = { width: 500, height: 180 };
      await answer(rendered, 'item-2');
    }

    expect(getPopupSizeVars(popup, positioner)).toEqual({
      popupWidth: '675px',
      popupHeight: '220px',
      positionerWidth: '500px',
      positionerHeight: '180px',
    });
    // Next frame: the measured size, then the auto-size reset, at once with animations disabled.
    flushFrames();
    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: 'auto', popupHeight: 'auto', positionerWidth: '500px', positionerHeight: '180px' });
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The size pass (NAV §9.2–9.4), Base UI's titles
// ---------------------------------------------------------------------------------------------------------------

describe('size', () => {
  it('updates popup sizing immediately when switching to a keepMounted trigger', async () => {
    globalThis.ResizeObserver = class {
      observe() {}
      unobserve() {}
      disconnect() {}
    } as unknown as typeof ResizeObserver;
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ keepMounted: true, synchronous: true });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    const animations = mockAnimations(popup);
    let size = { width: 675, height: 220 };
    mockOffsetSize(popup, () => size);
    rendered.commit('item-1');
    await settle();
    primeSize(popup, positioner, 'auto', 675, 220);

    size = { width: 500, height: 180 };
    animations.start();
    click(rendered.item('item-2').trigger);
    await flushMicrotasks();

    expect(positioner.style.getPropertyValue('--positioner-width')).toBe('500px');
    expect(positioner.style.getPropertyValue('--positioner-height')).toBe('180px');

    animations.finish();
    await settle();
    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: 'auto', popupHeight: 'auto', positionerWidth: '500px', positionerHeight: '180px' });
  });

  it('ignores the initial open size reset once a trigger switch has started', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ keepMounted: true, synchronous: true });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    const animations = mockAnimations(popup);
    let size = { width: 675, height: 220 };
    mockOffsetSize(popup, () => size);

    const openAnimation = animations.start();
    rendered.commit('item-1');
    await settle();

    size = { width: 500, height: 180 };
    const switchAnimation = animations.start();
    click(rendered.item('item-2').trigger);
    await settle();
    expect(getPopupSizeVars(popup, positioner)).toMatchObject({ positionerWidth: '500px', positionerHeight: '180px' });

    animations.finish(openAnimation);
    await settle();
    expect(popup.style.getPropertyValue('--popup-width')).toBe('500px');
    expect(popup.style.getPropertyValue('--popup-height')).toBe('180px');

    animations.finish(switchAnimation);
    await settle();
    expect(getPopupSizeVars(popup, positioner)).toMatchObject({ popupWidth: 'auto', popupHeight: 'auto' });
  });

  it('does not let a previously hovered trigger reapply popup sizes after a later switch', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ values: ['item-1', 'item-2', 'item-3'], keepMounted: true, synchronous: true });
    const popup = rendered.popup!;
    const animations = mockAnimations(popup);
    let size = { width: 600, height: 200 };
    mockOffsetSize(popup, () => size);
    rendered.commit('item-1');
    await settle();

    size = { width: 700, height: 240 };
    const first = animations.start();
    mouseEnter(rendered.item('item-2').trigger);
    await settle();
    expect(popup.style.getPropertyValue('--popup-width')).toBe('700px');

    size = { width: 500, height: 160 };
    const second = animations.start();
    mouseEnter(rendered.item('item-3').trigger);
    await settle();
    expect(popup.style.getPropertyValue('--popup-width')).toBe('500px');

    // The second trigger's reset was aborted by the third's activation.
    animations.finish(first);
    await settle();
    expect(popup.style.getPropertyValue('--popup-width')).toBe('500px');

    animations.finish(second);
    await settle();
    expect(popup.style.getPropertyValue('--popup-width')).toBe('auto');
  });

  it('does not let interrupted mutation resizing reapply popup sizes after a later switch', async () => {
    vi.stubGlobal('ResizeObserver', undefined);
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ synchronous: true, open: 'item-1' });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    await settle();
    const animations = mockAnimations(popup);
    let size = { width: 700, height: 420 };
    mockOffsetSize(popup, () => size);
    primeSize(popup, positioner, 700, 700, 420);
    const setProperty = vi.spyOn(positioner.style, 'setProperty');
    const widths = (from = 0) => setProperty.mock.calls.slice(from).filter((call) => call[0] === '--positioner-width').map((call) => call[1]);

    // "Expand Product": the active Content grows while the popup's transition runs.
    size = { width: 760, height: 480 };
    animations.start();
    rendered.item('item-1').content!.appendChild(document.createElement('div'));
    await settle();
    flushFrames();
    flushFrames();
    flushFrames();
    expect(widths()).toContain('760px');

    // The hover onto "Solutions" switches before the mutation's steps have finished.
    size = { width: 500, height: 300 };
    const before = setProperty.mock.calls.length;
    mouseEnter(rendered.item('item-2').trigger);
    await settle();
    for (let i = 0; i < 4; i += 1) {
      flushFrames();
      await settle();
    }

    const after = widths(before);
    expect(after).toContain('500px');
    expect(positioner.style.getPropertyValue('--positioner-width')).toBe('500px');
    expect(after.slice(after.indexOf('500px') + 1)).not.toContain('760px');
    setProperty.mockRestore();
  });

  it('updates popup sizing when inline nested content is inserted while active', async () => {
    vi.stubGlobal('ResizeObserver', undefined);
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu();
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    rendered.commit('item-1');
    await settle();

    const animations = mockAnimations(popup);
    let size = { width: 250, height: 120 };
    mockOffsetSize(popup, () => size);

    size = { width: 250, height: 220 };
    animations.start();
    const extra = document.createElement('div');
    extra.setAttribute('data-testid', 'extra-content');
    rendered.item('item-1').content!.appendChild(extra);
    await settle();

    expect(parseInt(positioner.style.getPropertyValue('--positioner-height'), 10)).toBe(220);

    animations.finish();
    await settle();
    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: 'auto', popupHeight: 'auto', positionerWidth: '250px', positionerHeight: '220px' });
  });

  it('updates popup sizing when switching kept inline nested content', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ keepMounted: true });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    rendered.commit('item-1');
    const nested = [1, 2].map((n) => {
      const panel = document.createElement('div');
      panel.setAttribute('data-testid', `nested-popup-${n}`);
      panel.hidden = n === 2;
      rendered.item('item-1').content!.appendChild(panel);
      return panel;
    });
    await settle();
    const animations = mockAnimations(popup);
    let size = { width: 250, height: 220 };
    mockOffsetSize(popup, () => size);
    primeSize(popup, positioner, 'auto', 250, 220);

    // The nested Root's switch, as its render writes it: the second kept panel shows and the first hides.
    size = { width: 250, height: 300 };
    animations.start();
    nested[1]!.hidden = false;
    nested[0]!.hidden = true;
    await settle();
    expect(parseInt(positioner.style.getPropertyValue('--positioner-height'), 10)).toBe(300);

    animations.finish();
    await settle();
    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: 'auto', popupHeight: 'auto', positionerWidth: '250px', positionerHeight: '300px' });
  });

  it('updates popup sizing when a kept nested content hidden attribute changes', async () => {
    vi.stubGlobal('ResizeObserver', undefined);
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ keepMounted: true });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    rendered.commit('item-1');
    const nested = document.createElement('div');
    nested.setAttribute('data-testid', 'nested-popup-1');
    rendered.item('item-1').content!.appendChild(nested);
    await settle();
    let size = { width: 250, height: 220 };
    mockOffsetSize(popup, () => size);
    primeSize(popup, positioner, 'auto', 250, 220);

    // The MutationObserver path, as Base UI's contract-level check: the kept nested content hides.
    size = { width: 250, height: 300 };
    nested.setAttribute('hidden', '');
    await settle();

    expect(getPopupSizeVars(popup, positioner)).toMatchObject({ positionerWidth: '250px', positionerHeight: '300px' });
  });

  it('keeps inline mutation resize interruptible when content updates again mid-transition', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu();
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    rendered.commit('item-1');
    await settle();

    const animations = mockAnimations(popup);
    const heights = [190, 260];
    let height = 260;
    mockOffset(
      popup,
      () => 250,
      () => {
        const next = heights.shift();
        if (next != null) {
          height = next;
        }
        return height;
      },
    );
    primeSize(popup, positioner, 250, 250, 220);
    const written: string[] = [];
    const setProperty = positioner.style.setProperty.bind(positioner.style);
    vi.spyOn(positioner.style, 'setProperty').mockImplementation((property, value, priority) => {
      if (property === '--positioner-height') {
        written.push(String(value));
      }
      setProperty(property, value, priority);
    });

    animations.start();
    rendered.item('item-1').content!.appendChild(document.createElement('div'));
    await settle();
    expect(written).toContain('190px');

    animations.finish();
    await settle();
    expect(getPopupSizeVars(popup, positioner)).toMatchObject({ popupHeight: 'auto', positionerHeight: '260px' });
  });

  it('preserves the current size when an interrupted mutation temporarily measures zero', async () => {
    vi.stubGlobal('ResizeObserver', undefined);
    const rendered = renderMenu();
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    rendered.commit('item-1');
    await settle();

    const widths = [250, 0];
    const heights = [220, 0];
    mockOffset(
      popup,
      () => widths.shift() ?? 0,
      () => heights.shift() ?? 0,
    );
    primeSize(popup, positioner, 250, 250, 220);

    rendered.item('item-1').content!.appendChild(document.createElement('div'));
    await settle();

    expect(popup.style.getPropertyValue('--popup-width')).toBe('auto');
    expect(popup.style.getPropertyValue('--popup-height')).toBe('auto');
    expect(positioner.style.getPropertyValue('--positioner-width')).toBe('250px');
    expect(positioner.style.getPropertyValue('--positioner-height')).toBe('220px');
  });

  it('does not animate popup sizing when nested default content mounts during opening', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu();
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    mockOffsetSize(popup, () => ({ width: 250, height: 220 }));
    // The opening batch: the Popup is starting when the nested default content arrives.
    popup.setAttribute('data-starting-style', '');
    rendered.commit('item-1');
    await flushMicrotasks();
    rendered.item('item-1').content!.appendChild(document.createElement('div'));
    await flushMicrotasks();
    popup.removeAttribute('data-starting-style');
    await settle();

    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: 'auto', popupHeight: 'auto', positionerWidth: '250px', positionerHeight: '220px' });
  });

  it('does not animate popup sizing when kept nested default content first moves into the portal', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ keepMounted: true });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    // Base UI's order: the click's commit runs the opening before the test mocks the measurements.
    popup.setAttribute('data-starting-style', '');
    rendered.commit('item-1');
    await flushMicrotasks();
    const setProperty = vi.spyOn(popup.style, 'setProperty');
    const heights = [120, 220];
    let height = 220;
    mockOffset(popup, () => 250, () => {
      height = heights.shift() ?? height;
      return height;
    });

    // The kept content's nested default content first moves into the portal.
    rendered.item('item-1').content!.appendChild(document.createElement('div'));
    await flushMicrotasks();
    popup.removeAttribute('data-starting-style');
    await settle();

    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: 'auto', popupHeight: 'auto', positionerWidth: '250px', positionerHeight: '220px' });
    const fixedHeights = setProperty.mock.calls.filter((call) => call[0] === '--popup-height').map((call) => call[1]).filter((value) => value !== 'auto' && value !== '0px');
    expect(fixedHeights.length).toBeGreaterThan(0);
    expect(fixedHeights.every((value) => value === '220px')).toBe(true);
    setProperty.mockRestore();
  });

  it('updates popup sizing when the window is resized while the popup is open', async () => {
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    const rendered = renderMenu({ keepMounted: true });
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    let size = { width: 675, height: 220 };
    mockOffsetSize(popup, () => size);
    rendered.commit('item-1');
    await settle();
    primeSize(popup, positioner, 'auto', 675, 220);

    size = { width: 500, height: 180 };
    window.dispatchEvent(new Event('resize'));
    expect(rendered.twin.reported(RESIZE_REPORT)).toHaveLength(1);
    await settle();

    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: 'auto', popupHeight: 'auto', positionerWidth: '500px', positionerHeight: '180px' });
  });

  it('preserves popup size when controlled value closes externally', async () => {
    const rendered = renderMenu();
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    rendered.commit('item-1');
    await settle();
    primeSize(popup, positioner, 'auto', 675, 220);
    mockOffsetSize(popup, () => ({ width: 0, height: 0 }));

    rendered.commit(null);
    await settle();

    expect(getPopupSizeVars(popup, positioner)).toEqual({ popupWidth: '675px', popupHeight: '220px', positionerWidth: '675px', positionerHeight: '220px' });
  });

  it('does not collapse auto-sized popup to zero on close if measurement temporarily returns 0', async () => {
    const rendered = renderMenu();
    const popup = rendered.popup!;
    const positioner = rendered.positioner!;
    mockOffsetSize(popup, () => ({ width: 300, height: 140 }));
    rendered.commit('item-1');
    await settle();
    mockOffsetSize(popup, () => ({ width: 0, height: 0 }));

    rendered.commit(null);
    await settle();

    expect(popup.style.getPropertyValue('--popup-width')).not.toBe('0px');
    expect(positioner.style.getPropertyValue('--positioner-width')).toBe('300px');
    expect(positioner.style.getPropertyValue('--positioner-height')).toBe('140px');
  });

  it('seeds the popup width from the exiting panel when reopening after hovering a top-level link', async () => {
    const observers: Array<() => void> = [];
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: () => void) {
          observers.push(callback);
        }
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    const rendered = renderMenu();
    const popup = rendered.popup!;
    let size = { width: 675, height: 220 };
    mockOffsetSize(popup, () => size);
    rendered.commit('item-1');
    await settle();
    for (const notify of observers) {
      notify();
    }

    // Closing while mounted, then reopening on another item: the remembered 675px seeds the transition's start.
    rendered.commit(null);
    await settle();
    size = { width: 0, height: 0 };
    rendered.commit('item-2');
    await flushMicrotasks();

    expect(popup.style.getPropertyValue('--popup-width')).toBe('675px');
  });
});

// ---------------------------------------------------------------------------------------------------------------
// Keys, guards and focus
// ---------------------------------------------------------------------------------------------------------------

describe('the open key', () => {
  it('does not open a nested or disabled trigger with the open key', async () => {
    const nested = renderMenu({ nested: true });
    await settle();
    expect(keyDown(nested.item('item-1').trigger, 'ArrowDown').defaultPrevented).toBe(false);
    document.body.replaceChildren();

    const rendered = renderMenu();
    rendered.item('item-1').trigger.setAttribute('data-disabled', '');
    await settle();
    expect(keyDown(rendered.item('item-1').trigger, 'ArrowDown').defaultPrevented).toBe(false);
    keyDown(rendered.item('item-2').trigger, 'ArrowDown');
    expect(nested.twin.reported(ACTIVATE_REPORT)).toHaveLength(0);
    expect(rendered.twin.reported<ActivationReport>(ACTIVATE_REPORT).map((report) => report.reason)).toEqual(['list-navigation']);
  });

  it('asks navigate before it prevents the open key, and a stop leaves the key and the menu alone', async () => {
    const rendered = renderMenu({ synchronous: true, verdict: 'stop' });
    await settle();

    const event = keyDown(rendered.item('item-1').trigger, 'ArrowDown');

    expect(rendered.twin.decisions.map((decision) => decision.point)).toEqual([NAVIGATE_POINT]);
    expect(rendered.twin.decisions[0]!.event.key).toBe('ArrowDown');
    expect(event.defaultPrevented).toBe(false);
    expect(rendered.activations).toHaveLength(0);
  });

  it('opens a vertical menu with the mirrored arrow key in RTL mode', async () => {
    const rendered = renderMenu({ orientation: 'vertical', rtl: true });
    await settle();
    expect(keyDown(rendered.item('item-1').trigger, 'ArrowRight').defaultPrevented).toBe(false);
    expect(keyDown(rendered.item('item-1').trigger, 'ArrowLeft').defaultPrevented).toBe(true);
    expect(rendered.twin.reported(ACTIVATE_REPORT)).toHaveLength(1);
  });
});

describe('the List', () => {
  it('stops vertical navigation keys from escaping the list', async () => {
    const rendered = renderMenu({ orientation: 'vertical' });
    await settle();
    const outer = vi.fn();
    document.body.addEventListener('keydown', outer);
    keyDown(rendered.item('item-1').trigger, 'ArrowUp');
    keyDown(rendered.item('item-1').trigger, 'ArrowDown');
    keyDown(rendered.item('item-1').trigger, 'PageDown');
    expect(outer.mock.calls.map(([event]) => (event as KeyboardEvent).key)).toEqual(['PageDown']);
    document.body.removeEventListener('keydown', outer);
  });
});

describe('focus guards', () => {
  it('moves focus from the active trigger into the popup and back through the guards', async () => {
    const rendered = renderMenu({ open: 'item-1' });
    await settle();
    const trigger = rendered.item('item-1').trigger;
    const leading = trigger.nextElementSibling as HTMLElement;
    const link = rendered.item('item-1').content!.querySelector('a')!;

    // Tab from the trigger reaches its leading guard from outside the floating element, which forwards to the
    // Viewport's leading guard, which (again from outside) focuses the first tabbable inside the Viewport.
    trigger.focus();
    leading.focus();
    expect(document.activeElement).toBe(link);

    // Shift+Tab from the first tabbable: the Viewport's leading guard from inside focuses the trigger's leading guard,
    // which from inside the floating element focuses the previous tabbable, the trigger.
    rendered.viewportGuards!.before.focus();
    expect(document.activeElement).toBe(trigger);
  });

  it('closes the menu when tabbing forward out', async () => {
    const rendered = renderMenu({ open: 'item-1', values: ['item-1'] });
    await settle();
    const trigger = rendered.item('item-1').trigger;
    const trailing = trigger.nextElementSibling!.nextElementSibling!.nextElementSibling as HTMLElement;
    const link = rendered.item('item-1').content!.querySelector('a')!;

    // Tab from the last tabbable inside the popup reaches the trigger's trailing guard from inside, which focuses the
    // next tabbable after the menu and closes it.
    link.focus();
    trailing.focus();

    expect(document.activeElement?.getAttribute('data-testid')).toBe('after');
    expect(rendered.twin.reported(FOCUS_OUT_REPORT)).toHaveLength(1);
  });

  it('routes the trailing guard after an owner that carries no aria-owns, as rendered without a Viewport', async () => {
    const rendered = renderMenu({ open: 'item-1', values: ['item-1'] });
    await settle();
    const trigger = rendered.item('item-1').trigger;
    const owner = trigger.nextElementSibling!.nextElementSibling as HTMLElement;
    owner.removeAttribute('aria-owns');
    const trailing = owner.nextElementSibling as HTMLElement;

    // From the trigger, outside the floating element: the trailing guard forwards to the Viewport's trailing guard.
    trigger.focus();
    trailing.focus();

    expect(document.activeElement).not.toBe(trailing);
  });

  it('swaps the guards for VoiceOver on WebKit', async () => {
    flags.voiceOver = true;
    flags.webkit = true;
    const rendered = renderMenu();
    rendered.commit('item-1');
    await settle();
    const trigger = rendered.item('item-1').trigger;
    const guard = trigger.nextElementSibling as HTMLElement;
    expect(guard.getAttribute('role')).toBe('button');
    expect(guard.hasAttribute('aria-hidden')).toBe(false);
    expect(rendered.viewportGuards!.before.getAttribute('role')).toBe('button');
  });
});

describe('focus-out', () => {
  // CI run 41810354439 (issue-114 rule): on Server the Positioner's and Popup's handles attach a round trip or two after
  // the open's render, and the focus-out check waited for them. The active Trigger's `aria-controls` names the Popup from
  // the open's render on (NAV-ARIA-02).
  it('closes the menu when the trigger blurs outside before the popup handles attach, by the popup its aria-controls names', async () => {
    const rendered = renderMenu({ open: 'item-1', popupHandles: false });
    await settle();
    const trigger = rendered.item('item-1').trigger;
    trigger.setAttribute('aria-controls', rendered.popup!.id);
    const inside = rendered.item('item-1').content!.querySelector('a')!;

    trigger.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: inside }));
    expect(rendered.twin.reported(FOCUS_OUT_REPORT)).toHaveLength(0);
    trigger.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.querySelector('[data-testid="after"]') }));
    expect(rendered.twin.reported(FOCUS_OUT_REPORT)).toHaveLength(1);
  });

  it('closes the menu when the trigger blurs to an element outside the root and the popup, and only once in flight', async () => {
    const rendered = renderMenu({ open: 'item-1' });
    await settle();
    const trigger = rendered.item('item-1').trigger;
    const outside = document.querySelector<HTMLElement>('[data-testid="after"]')!;

    trigger.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: rendered.item('item-2').trigger }));
    expect(rendered.twin.reported(FOCUS_OUT_REPORT)).toHaveLength(0);
    trigger.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: outside }));
    trigger.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: null }));
    expect(rendered.twin.reported(FOCUS_OUT_REPORT)).toHaveLength(1);

    // C# answers (canceled): the next blur may close again.
    rendered.answered += 1;
    rendered.updateRoot();
    trigger.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: null }));
    expect(rendered.twin.reported(FOCUS_OUT_REPORT)).toHaveLength(2);
  });
});

describe('focus return', () => {
  it('returns focus to trigger when closing menu', async () => {
    const rendered = renderMenu({ open: 'item-1' });
    await settle();
    const link = rendered.item('item-1').content!.querySelector('a')!;
    link.focus();
    rendered.commit(null, { closeReason: 'escape-key' });
    await settle();
    rendered.commit(null, { mounted: false });
    expect(document.activeElement).toBe(rendered.item('item-1').trigger);
  });

  it('does not restore focus to the trigger when closed via hover', async () => {
    const rendered = renderMenu({ open: 'item-1' });
    await settle();
    rendered.item('item-1').content!.querySelector('a')!.focus();
    rendered.commit(null, { closeReason: 'trigger-hover' });
    await settle();
    rendered.commit(null, { mounted: false });
    expect(document.activeElement).not.toBe(rendered.item('item-1').trigger);
  });
});

describe('the Viewport', () => {
  it('reports viewportInert when focus leaves an inline Viewport for neither its inside nor the active trigger', async () => {
    const rendered = renderMenu({ positioner: false, open: 'item-1' });
    await settle();
    const link = rendered.item('item-1').content!.querySelector('a')!;
    link.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: rendered.item('item-1').trigger }));
    expect(rendered.twin.reported(VIEWPORT_INERT_REPORT)).toHaveLength(0);
    link.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.querySelector('[data-testid="after"]') }));
    expect(rendered.twin.reported(VIEWPORT_INERT_REPORT)).toEqual([{ inert: true }]);
  });

  // NavigationMenuViewport.tsx:104-118 then NavigationMenuTrigger.tsx:665-670: Base UI sets viewportInert on the
  // Viewport's blur and clears it on the active Trigger's focus, in that order. The report is asynchronous and the
  // Trigger's C# onfocus is not, so the Trigger's focus reports the clear after an outstanding set, in the set's queue.
  it('reports the clear after the set when focus leaves an inline Viewport through a guard for its active trigger', async () => {
    const rendered = renderMenu({ positioner: false, open: 'item-1' });
    await settle();
    const link = rendered.item('item-1').content!.querySelector('a')!;
    const trigger = rendered.item('item-1').trigger;
    link.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.querySelector('[data-testid="after"]') }));
    trigger.dispatchEvent(new FocusEvent('focus'));
    expect(rendered.twin.reported(VIEWPORT_INERT_REPORT)).toEqual([{ inert: true }, { inert: false }]);

    trigger.dispatchEvent(new FocusEvent('focus'));
    expect(rendered.twin.reported(VIEWPORT_INERT_REPORT)).toHaveLength(2);
  });
});

describe('the inline Viewport target', () => {
  it("reports the target's exit once its animations finish after the menu closes, and not after a reopen", async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const rendered = renderMenu({ positioner: false, open: 'item-1' });
    await settle();
    const animations = mockAnimations(rendered.target);

    const exit = animations.start();
    rendered.commit(null);
    await settle();
    expect(rendered.twin.reported(VIEWPORT_EXIT_REPORT)).toHaveLength(0);
    animations.finish(exit);
    await settle();
    expect(rendered.twin.reported(VIEWPORT_EXIT_REPORT)).toHaveLength(1);

    rendered.commit('item-1');
    await settle();
    const interrupted = animations.start();
    rendered.commit(null);
    await settle();
    rendered.commit('item-2');
    animations.finish(interrupted);
    await settle();
    expect(rendered.twin.reported(VIEWPORT_EXIT_REPORT)).toHaveLength(1);
  });
});

describe('the Positioner', () => {
  it('takes its tabbables out of the tab order when focus leaves it and restores them when it returns', async () => {
    const rendered = renderMenu({ open: 'item-1' });
    await settle();
    const positioner = rendered.positioner!;
    const link = rendered.item('item-1').content!.querySelector('a')!;
    link.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.body }));
    expect(link.getAttribute('tabindex')).toBe('-1');
    positioner.dispatchEvent(new FocusEvent('focusin', { relatedTarget: document.body }));
    expect(link.hasAttribute('tabindex')).toBe(false);
  });

  it('reports a window resize once per frame while open', async () => {
    const rendered = renderMenu({ open: 'item-1' });
    await settle();
    window.dispatchEvent(new Event('resize'));
    window.dispatchEvent(new Event('resize'));
    expect(rendered.twin.reported(RESIZE_REPORT)).toHaveLength(1);
    flushFrames();
    window.dispatchEvent(new Event('resize'));
    expect(rendered.twin.reported(RESIZE_REPORT)).toHaveLength(2);
  });
});

describe('the Arrow', () => {
  it('carries transition none while the Popup starts and drops it after, as a kept portal opens', async () => {
    const rendered = renderMenu({});
    await settle();
    const arrow = document.createElement('div');
    rendered.popup!.prepend(arrow);
    handles.push(attach(arrow, JSON.stringify({ part: ARROW_PART, rootRef: 0 }), rendered.dotnet, [rendered.root]));

    rendered.popup!.setAttribute('data-starting-style', '');
    await settle();
    expect(arrow.style.transition).toBe('none');

    rendered.popup!.removeAttribute('data-starting-style');
    await settle();
    expect(arrow.style.transition).toBe('');
  });
});

describe('the Content', () => {
  it('takes an exiting Content out of flow in the pass, before the size pass measures, and puts it back once hidden', async () => {
    const rendered = renderMenu({ keepMounted: true, open: 'item-1' });
    await settle();
    const first = rendered.item('item-1').content!;
    handles.push(attach(first, JSON.stringify({ part: 'content', rootRef: 0 }), rendered.dotnet, [rendered.root]));

    // Server: the switch's batch closes the kept Content; its `hidden` and C#'s exiting style land a moment later.
    rendered.commit('item-2');
    first.hidden = false;
    await settle();
    expect([first.style.position, first.style.top, first.style.left]).toEqual(['absolute', '0px', '0px']);

    first.hidden = true;
    await settle();
    expect(first.style.position).toBe('');
  });

  it('makes an exiting Content inert unless focus is inside it', async () => {
    const rendered = renderMenu({ open: 'item-1' });
    await settle();
    const content = rendered.item('item-1').content!;
    handles.push(attach(content, JSON.stringify({ part: 'content', rootRef: 0 }), rendered.dotnet, [rendered.root]));
    const link = content.querySelector('a')!;
    link.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));

    rendered.commit('item-2');
    await settle();
    expect(content.hasAttribute('inert')).toBe(false);

    link.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.body }));
    expect(content.hasAttribute('inert')).toBe(true);
  });
});

// NAV-EDGE-11; NavigationMenuRoot.webkit.test.tsx with Base UI's engine flag mocked (sheet F Q16 (a)): the lock the
// Trigger's hover options configure (NavigationMenuTrigger.cs: the safe polygon with blocked pointer events, the lock on
// enter, the `ul` scope) on the interactions harness, with the WebKit flag set. Neither module branches on the engine.
describe('WebKit', () => {
  const hover: import('./interactions').HoverOptions = {
    safePolygon: true,
    blockPointerEvents: true,
    blockPointerEventsOnEnter: true,
    pointerEventsScopeSelector: 'ul',
    move: false,
  };

  function mouseEnterTrigger(element: HTMLElement): void {
    element.dispatchEvent(new MouseEvent('mouseenter', { relatedTarget: null }));
  }

  it('blocks top-level pointer events for hover-open menus', () => {
    flags.webkit = true;
    const popup = createPopupHarness();
    try {
      popup.attachSession();
      const list = document.createElement('ul');
      const trigger = document.createElement('button');
      trigger.id = 'webkit-trigger';
      list.append(trigger);
      const floating = document.createElement('div');
      document.body.append(list, floating);
      popup.attachTrigger(trigger, { trigger: { hover } });
      popup.attachFloating(floating, { floating: { hover: {} } });

      mouseEnterTrigger(trigger);
      popup.setOpen(true, 'trigger-hover', trigger.id, 'mouseenter');

      expect(popup.open).toBe(true);
      expect([list.style.pointerEvents, document.body.style.pointerEvents]).toEqual(['none', '']);
    } finally {
      popup.dispose();
    }
  });

  it('keeps nested safePolygon pointer events scoped on WebKit', () => {
    flags.webkit = true;
    const popup = createPopupHarness();
    try {
      popup.attachSession();
      // A top-level List and, inside its open content, the inline nested List with its trigger and Viewport.
      const list = document.createElement('ul');
      const content = document.createElement('div');
      const nestedList = document.createElement('ul');
      const nestedTrigger = document.createElement('button');
      nestedTrigger.id = 'webkit-nested-trigger';
      const nestedViewport = document.createElement('div');
      nestedList.append(nestedTrigger);
      content.append(nestedList, nestedViewport);
      list.append(document.createElement('li'));
      document.body.append(list, content);
      popup.attachTrigger(nestedTrigger, { trigger: { hover } });
      popup.attachFloating(nestedViewport, { floating: { node: false, hover: {} } });

      mouseEnterTrigger(nestedTrigger);

      expect([nestedList.style.pointerEvents, list.style.pointerEvents, document.body.style.pointerEvents]).toEqual(['none', '', '']);
    } finally {
      popup.dispose();
    }
  });
});
