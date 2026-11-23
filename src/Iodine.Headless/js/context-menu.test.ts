// L3 (ADR 06 §1): the `context-menu` component module in jsdom against a fake twin. Each case builds what C# renders (the
// Trigger, and for the Root's references the internal backdrop, a consumer Backdrop and the root Positioner holding a
// popup with `data-rootownerid`), takes a reference on the menu's interactions session as the session's own handle
// would, and attaches the Root's handle by token and the Trigger's on its element. The release slot and the position
// reference are read back from the session. Base UI's titles keep their wording (ContextMenuTrigger.test.tsx: PTR-07's
// five, EDGE-04's unmount); the rest are port titles. Where Server and WebAssembly take different paths (the consumer's
// handlers, run through `press` or reported), the titles run on both.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { acquireSession, getSession, releaseSession, type InteractionSession } from './interactions';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';
import type { DecisionPolicy, DotNetTwin, TwinHandle } from './twin';

const flags = vi.hoisted(() => ({ mac: false }));

vi.mock('./platform', async (importOriginal) => {
  const original = await importOriginal<typeof import('./platform')>();
  return {
    ...original,
    platform: {
      ...original.platform,
      os: {
        ...original.platform.os,
        get mac() {
          return flags.mac;
        },
      },
    },
  };
});

const {
  attach,
  CANCEL_OPEN_REPORT,
  LONG_PRESS_DELAY,
  OPEN_REPORT,
  PRESS_POINT,
  PRESS_REPORT,
  PRESSING_HANDOVER,
  ROOT_PART,
  TRIGGER_PART,
} = await import('./context-menu');
type OpenReport = import('./context-menu').ContextMenuOpenReport;
type PressReport = import('./context-menu').ContextMenuPress;

const RELEASE_KEY = 'blazix.interactions.menuRelease';
const ROOT_ID = 'base-ui-root-1';

interface ReleaseSlot {
  armed: boolean;
  rules: { button: number | null; skipButton: number | null; slop: number; initialPoint: { x: number; y: number } | null } | null;
  initialPoint: { x: number; y: number } | null;
}

interface TouchPoint {
  identifier: number;
  target: EventTarget;
  clientX: number;
  clientY: number;
}

interface Fixture {
  token: string;
  trigger: HTMLElement;
  positioner: HTMLElement;
  popup: HTMLElement;
  backdrop: HTMLElement;
  internalBackdrop: HTMLElement;
  outside: HTMLElement;
  rootTwin: FakeTwin;
  triggerTwin: FakeTwin;
  pressings: PressReport[];
  root: TwinHandle | null;
  triggerHandle: TwinHandle | null;
  session: InteractionSession;
  attachRoot(): TwinHandle;
  attachTrigger(options?: { disabled?: boolean; handlers?: string[] }): TwinHandle;
  slot(): ReleaseSlot | undefined;
  anchor(): { x: number; y: number; width: number; height: number } | null;
  opens(): OpenReport[];
}

interface SetupOptions {
  synchronous?: boolean;
  verdict?: DecisionPolicy;
  disabled?: boolean;
  handlers?: string[];
  attachRoot?: boolean;
  attachTrigger?: boolean;
}

let counter = 0;
const cleanups: (() => void)[] = [];

beforeEach(() => {
  flags.mac = false;
  vi.useFakeTimers();
});

afterEach(() => {
  for (const cleanup of cleanups.splice(0).reverse()) {
    cleanup();
  }
  vi.useRealTimers();
  document.body.replaceChildren();
});

function element(id: string, parent: Node = document.body): HTMLElement {
  const node = document.createElement('div');
  node.id = id;
  parent.appendChild(node);
  return node;
}

// The twin as Blazor revives it, with the synchronous `OnPressing` handover the fake twin does not answer.
function withHandover(fake: FakeTwin, pressings: PressReport[]): DotNetTwin {
  return {
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      if (methodName === PRESSING_HANDOVER) {
        pressings.push(JSON.parse(args[0] as string) as PressReport);
        return undefined as T;
      }
      return fake.twin.invokeMethod<T>(methodName, ...args);
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      return fake.twin.invokeMethodAsync<T>(methodName, ...args);
    },
  };
}

function setup(options: SetupOptions = {}): Fixture {
  counter += 1;
  const token = `ctx-${counter}`;
  const trigger = element('trigger');
  trigger.textContent = 'Right click me';
  const portal = element('portal');
  const backdrop = element('backdrop', portal);
  const internalBackdrop = element('internal-backdrop', portal);
  const positioner = element('positioner', portal);
  const popup = element('popup', positioner);
  popup.setAttribute('data-rootownerid', ROOT_ID);
  const outside = element('outside');
  const twinOptions = { synchronous: options.synchronous ?? false, verdict: options.verdict ?? 'run' } as const;
  const rootTwin = createFakeTwin(twinOptions);
  const triggerTwin = createFakeTwin(twinOptions);
  const pressings: PressReport[] = [];
  // The interactions session's own reference, as its handle takes at attach.
  const session = acquireSession(token);
  cleanups.push(() => releaseSession(session));
  const fixture: Fixture = {
    token,
    trigger,
    positioner,
    popup,
    backdrop,
    internalBackdrop,
    outside,
    rootTwin,
    triggerTwin,
    pressings,
    root: null,
    triggerHandle: null,
    session,
    attachRoot() {
      const handle = attach(
        null,
        JSON.stringify({ part: ROOT_PART, token, backdrop: 0, internalBackdrop: 1, positioner: 2 }),
        rootTwin.twin,
        [backdrop, internalBackdrop, positioner],
      );
      fixture.root = handle;
      cleanups.push(() => handle.dispose());
      return handle;
    },
    attachTrigger(trigger_ = {}) {
      const handle = attach(
        trigger,
        JSON.stringify({
          part: TRIGGER_PART,
          token,
          rootId: ROOT_ID,
          disabled: trigger_.disabled ?? options.disabled ?? false,
          handlers: trigger_.handlers ?? options.handlers ?? [],
        }),
        withHandover(triggerTwin, pressings),
      );
      fixture.triggerHandle = handle;
      cleanups.push(() => handle.dispose());
      return handle;
    },
    slot() {
      return getSession(token)?.dataRef.current[RELEASE_KEY] as ReleaseSlot | undefined;
    },
    anchor() {
      const reference = getSession(token)?.positionReference;
      if (!reference) {
        return null;
      }
      const { x, y, width, height } = reference.getBoundingClientRect();
      return { x, y, width, height };
    },
    opens() {
      return triggerTwin.reported<OpenReport>(OPEN_REPORT);
    },
  };
  if (options.attachRoot !== false) {
    fixture.attachRoot();
  }
  if (options.attachTrigger !== false) {
    fixture.attachTrigger();
  }
  return fixture;
}

function contextMenu(target: EventTarget, clientX = 50, clientY = 60): MouseEvent {
  const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, composed: true, button: 2, buttons: 2, clientX, clientY });
  target.dispatchEvent(event);
  return event;
}

function mouseUp(target: EventTarget, clientX = 0, clientY = 0, button = 2): MouseEvent {
  const event = new MouseEvent('mouseup', { bubbles: true, cancelable: true, composed: true, button, clientX, clientY });
  target.dispatchEvent(event);
  return event;
}

// jsdom's TouchEvent takes no touch lists in its init, so they are defined on the event, as `fireEvent` does.
function touch(target: EventTarget, type: string, touches: TouchPoint[] = []): TouchEvent {
  const event = new TouchEvent(type, { bubbles: true, cancelable: true, composed: true });
  Object.defineProperty(event, 'touches', { value: touches });
  Object.defineProperty(event, 'changedTouches', { value: touches });
  target.dispatchEvent(event);
  return event;
}

function point(target: EventTarget, clientX: number, clientY: number, identifier = 0): TouchPoint {
  return { identifier, target, clientX, clientY };
}

// Whether an event reached a listener on the body after the trigger saw it.
function watchBubbling(type: string): () => boolean {
  let reached = false;
  const listener = () => {
    reached = true;
  };
  document.body.addEventListener(type, listener);
  cleanups.push(() => document.body.removeEventListener(type, listener));
  return () => {
    const value = reached;
    reached = false;
    return value;
  };
}

describe('context-menu: the right-click open', () => {
  it('port: the root arms the release with the context menu rules and anchors the origin rect at attach', () => {
    const fixture = setup({ attachTrigger: false });

    expect(fixture.slot()).toEqual({
      armed: true,
      rules: { button: 2, skipButton: 2, slop: 1, initialPoint: null },
      initialPoint: null,
    });
    expect(fixture.anchor()).toEqual({ x: 0, y: 0, width: 0, height: 0 });
  });

  it('port: on macOS the rules keep the right-button release that a non-macOS gesture skips', () => {
    flags.mac = true;
    const fixture = setup();

    expect(fixture.slot()?.rules).toEqual({ button: 2, skipButton: null, slop: 1, initialPoint: null });

    contextMenu(fixture.trigger, 30, 40);
    expect(fixture.slot()?.rules).toEqual({ button: 2, skipButton: null, slop: 1, initialPoint: { x: 30, y: 40 } });
  });

  it('port: a right click arms the release, records the point, anchors a 0 x 0 rect there and reports the open with its event', () => {
    const fixture = setup();
    const bubbled = watchBubbling('contextmenu');
    fixture.slot()!.armed = false;

    const event = contextMenu(fixture.trigger, 120, 80);

    expect(event.defaultPrevented).toBe(true);
    expect(bubbled()).toBe(false);
    expect(fixture.slot()).toEqual({
      armed: true,
      rules: { button: 2, skipButton: 2, slop: 1, initialPoint: { x: 120, y: 80 } },
      initialPoint: { x: 120, y: 80 },
    });
    expect(fixture.anchor()).toEqual({ x: 120, y: 80, width: 0, height: 0 });
    expect(fixture.opens()).toEqual([
      { x: 120, y: 80, pointerType: 'mouse', event: expect.objectContaining({ type: 'contextmenu', kind: 'MouseEvent' }) },
    ]);
  });

  it('port: a right click on a descendant of the trigger opens too', () => {
    const fixture = setup();
    const child = element('child', fixture.trigger);

    contextMenu(child, 10, 20);

    expect(fixture.opens()).toHaveLength(1);
    expect(fixture.anchor()).toEqual({ x: 10, y: 20, width: 0, height: 0 });
  });

  it('port: the root keeps a gesture made before its attach', () => {
    const fixture = setup({ attachRoot: false });

    contextMenu(fixture.trigger, 70, 90);
    fixture.attachRoot();

    expect(fixture.anchor()).toEqual({ x: 70, y: 90, width: 0, height: 0 });
    expect(fixture.slot()).toMatchObject({ armed: true, initialPoint: { x: 70, y: 90 } });
  });

  it('port: the trigger and the root take their own reference on the session, so the attach order does not matter', () => {
    counter += 1;
    const token = `ctx-${counter}`;
    const trigger = element('trigger');
    const twin = createFakeTwin();
    const triggerHandle = attach(trigger, JSON.stringify({ part: TRIGGER_PART, token, rootId: ROOT_ID }), twin.twin);
    const rootHandle = attach(null, JSON.stringify({ part: ROOT_PART, token }), createFakeTwin().twin);
    cleanups.push(() => rootHandle.dispose());
    cleanups.push(() => triggerHandle.dispose());

    expect(getSession(token)).toBeDefined();
    contextMenu(trigger, 5, 6);
    expect(getSession(token)?.positionReference?.getBoundingClientRect()).toMatchObject({ x: 5, y: 6, width: 0, height: 0 });

    // The interactions session attaches last and finds the module's anchor and arm.
    const session = acquireSession(token);
    cleanups.push(() => releaseSession(session));
    expect(session.positionReference?.getBoundingClientRect()).toMatchObject({ x: 5, y: 6 });
    expect((session.dataRef.current[RELEASE_KEY] as ReleaseSlot).armed).toBe(true);
  });

  it('port: a disabled root neither opens on a right click nor prevents the native menu', () => {
    const fixture = setup({ disabled: true });
    const bubbled = watchBubbling('contextmenu');

    const event = contextMenu(fixture.trigger);

    expect(event.defaultPrevented).toBe(false);
    expect(bubbled()).toBe(true);
    expect(fixture.opens()).toEqual([]);
    expect(fixture.anchor()).toEqual({ x: 0, y: 0, width: 0, height: 0 });
  });

  it('port: an update that enables the trigger opens on the next right click', () => {
    const fixture = setup({ disabled: true });
    contextMenu(fixture.trigger);
    expect(fixture.opens()).toEqual([]);

    fixture.triggerHandle!.update(JSON.stringify({ part: TRIGGER_PART, token: fixture.token, rootId: ROOT_ID, disabled: false }));
    contextMenu(fixture.trigger);

    expect(fixture.opens()).toHaveLength(1);
  });
});

describe('context-menu: the document mouseup', () => {
  it('port: one gesture installs one document mouseup, removing the one a previous gesture left', () => {
    const fixture = setup();
    const added = vi.spyOn(document, 'addEventListener');
    const removed = vi.spyOn(document, 'removeEventListener');

    contextMenu(fixture.trigger);
    contextMenu(fixture.trigger);

    const mouseUps = added.mock.calls.filter(([type]) => type === 'mouseup');
    expect(mouseUps).toHaveLength(2);
    expect(removed.mock.calls.filter(([type, listener]) => type === 'mouseup' && listener === mouseUps[0]![1])).toHaveLength(1);

    vi.advanceTimersByTime(LONG_PRESS_DELAY + 1);
    mouseUp(fixture.outside);
    mouseUp(fixture.outside);
    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toHaveLength(1);
  });

  it('port: the document mouseup disarms the release and does nothing else within 500 ms of the gesture', () => {
    const fixture = setup();
    contextMenu(fixture.trigger);

    vi.advanceTimersByTime(LONG_PRESS_DELAY - 1);
    mouseUp(fixture.outside);

    expect(fixture.slot()?.armed).toBe(false);
    expect(fixture.slot()?.rules).toMatchObject({ button: 2 });
    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toEqual([]);

    // The listener was one-shot.
    vi.advanceTimersByTime(10);
    mouseUp(fixture.outside);
    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toEqual([]);
  });

  it('port: a document mouseup outside the tree after 500 ms reports cancel-open with its event', () => {
    const fixture = setup();
    contextMenu(fixture.trigger);

    vi.advanceTimersByTime(LONG_PRESS_DELAY);
    mouseUp(fixture.outside, 300, 400);

    expect(fixture.slot()?.armed).toBe(false);
    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toEqual([
      { event: expect.objectContaining({ type: 'mouseup', kind: 'MouseEvent' }) },
    ]);
  });

  it('port: a mouseup inside the root positioner does not cancel', () => {
    const fixture = setup();
    const inside = element('inside', fixture.positioner);
    contextMenu(fixture.trigger);

    vi.advanceTimersByTime(LONG_PRESS_DELAY + 1);
    mouseUp(inside);

    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toEqual([]);
  });

  it('port: a mouseup in a popup of the same root, portaled elsewhere and inside a shadow root, does not cancel', () => {
    const fixture = setup();
    const submenu = element('submenu-popup');
    submenu.setAttribute('data-rootownerid', ROOT_ID);
    const host = element('host', submenu);
    const shadow = host.attachShadow({ mode: 'open' });
    const item = element('item', shadow);
    contextMenu(fixture.trigger);

    vi.advanceTimersByTime(LONG_PRESS_DELAY + 1);
    mouseUp(item);

    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toEqual([]);
  });

  it('port: a mouseup in a popup of another root cancels', () => {
    const fixture = setup();
    const other = element('other-popup');
    other.setAttribute('data-rootownerid', 'base-ui-root-2');
    contextMenu(fixture.trigger);

    vi.advanceTimersByTime(LONG_PRESS_DELAY + 1);
    mouseUp(other);

    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toHaveLength(1);
  });

  it('aborts the pending document mouseup listener when the trigger unmounts', () => {
    const fixture = setup();
    contextMenu(fixture.trigger);
    expect(fixture.opens()).toHaveLength(1);

    fixture.triggerHandle!.dispose();
    fixture.trigger.remove();
    vi.advanceTimersByTime(LONG_PRESS_DELAY + 1);
    mouseUp(document.body);

    expect(fixture.triggerTwin.reported(CANCEL_OPEN_REPORT)).toEqual([]);
  });
});

describe('context-menu: the native menu suppressor', () => {
  it('port: the document suppressor prevents the native menu over the trigger and both backdrops, and nowhere else', () => {
    const fixture = setup();

    expect(contextMenu(fixture.internalBackdrop).defaultPrevented).toBe(true);
    expect(contextMenu(fixture.backdrop).defaultPrevented).toBe(true);
    expect(contextMenu(fixture.outside).defaultPrevented).toBe(false);
    expect(contextMenu(fixture.popup).defaultPrevented).toBe(false);

    // A portal container placed inside the trigger is inside it.
    const container = element('container', fixture.trigger);
    const portaled = element('portaled', container);
    expect(contextMenu(portaled).defaultPrevented).toBe(true);

    // The backdrops it checks follow the root's updates.
    const replacement = element('backdrop-2');
    fixture.root!.update(JSON.stringify({ part: ROOT_PART, token: fixture.token, backdrop: 0, internalBackdrop: null, positioner: null }), [replacement]);
    expect(contextMenu(replacement).defaultPrevented).toBe(true);
    expect(contextMenu(fixture.internalBackdrop).defaultPrevented).toBe(false);
  });

  it('port: the suppressor stops with the trigger and while the root is disabled', () => {
    const fixture = setup();
    fixture.triggerHandle!.update(JSON.stringify({ part: TRIGGER_PART, token: fixture.token, rootId: ROOT_ID, disabled: true }));
    expect(contextMenu(fixture.backdrop).defaultPrevented).toBe(false);

    fixture.triggerHandle!.update(JSON.stringify({ part: TRIGGER_PART, token: fixture.token, rootId: ROOT_ID, disabled: false }));
    expect(contextMenu(fixture.backdrop).defaultPrevented).toBe(true);

    fixture.triggerHandle!.dispose();
    expect(contextMenu(fixture.backdrop).defaultPrevented).toBe(false);
  });
});

describe('context-menu: the long press', () => {
  it('port: a long press anchors a 10 x 10 rect, reports the open with the touchstart and installs no mouseup', () => {
    const fixture = setup();
    const bubbled = watchBubbling('touchstart');
    const added = vi.spyOn(document, 'addEventListener');

    const start = touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 110)]);
    expect(bubbled()).toBe(false);
    expect(start.defaultPrevented).toBe(false);
    expect(fixture.slot()?.armed).toBe(false);

    vi.advanceTimersByTime(LONG_PRESS_DELAY - 1);
    expect(fixture.opens()).toEqual([]);
    vi.advanceTimersByTime(1);

    expect(fixture.opens()).toEqual([
      { x: 100, y: 110, pointerType: 'touch', event: expect.objectContaining({ type: 'touchstart', kind: 'TouchEvent' }) },
    ]);
    expect(fixture.anchor()).toEqual({ x: 100, y: 110, width: 10, height: 10 });
    expect(fixture.slot()).toMatchObject({ armed: false, initialPoint: { x: 100, y: 110 } });
    expect(added.mock.calls.filter(([type]) => type === 'mouseup')).toEqual([]);
  });

  it('port: every touchstart on an enabled trigger disarms the release, whatever its touch count', () => {
    const fixture = setup();
    const bubbled = watchBubbling('touchstart');

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 1, 1), point(fixture.trigger, 2, 2, 1)]);

    expect(fixture.slot()?.armed).toBe(false);
    // A multi-touch start cancels before the propagation is stopped.
    expect(bubbled()).toBe(true);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);
    expect(fixture.opens()).toEqual([]);
  });

  it('should cancel long press when touch moves beyond threshold', () => {
    const fixture = setup();

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    touch(fixture.trigger, 'touchmove', [point(fixture.trigger, 120, 100)]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);

    expect(fixture.opens()).toEqual([]);
  });

  it('keeps a pending long press when touch movement stays within the threshold', () => {
    const fixture = setup();

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    touch(fixture.trigger, 'touchmove', [point(fixture.trigger, 105, 105)]);
    touch(fixture.trigger, 'touchmove', [point(fixture.trigger, 110, 90)]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);

    expect(fixture.opens()).toHaveLength(1);
  });

  it('cancels a pending long press when the touch ends', () => {
    const fixture = setup();

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    touch(fixture.trigger, 'touchend');
    vi.advanceTimersByTime(LONG_PRESS_DELAY);
    expect(fixture.opens()).toEqual([]);

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    touch(fixture.trigger, 'touchcancel');
    vi.advanceTimersByTime(LONG_PRESS_DELAY);
    expect(fixture.opens()).toEqual([]);
  });

  it('cancels a pending long press when the gesture becomes multi-touch', () => {
    const fixture = setup();
    const first = point(fixture.trigger, 100, 100);
    const both = [first, point(fixture.trigger, 120, 100, 1)];

    touch(fixture.trigger, 'touchstart', [first]);
    touch(fixture.trigger, 'touchmove', both);
    touch(fixture.trigger, 'touchmove', [first]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);
    expect(fixture.opens()).toEqual([]);

    touch(fixture.trigger, 'touchend');
    touch(fixture.trigger, 'touchstart', [first]);
    touch(fixture.trigger, 'touchstart', both);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);
    expect(fixture.opens()).toEqual([]);
  });

  it('does not open on long press when disabled', () => {
    const fixture = setup({ disabled: true });

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);

    expect(fixture.opens()).toEqual([]);
    expect(fixture.slot()?.armed).toBe(true);
  });

  it('port: a disabled touchstart cancels a long press that started while enabled', () => {
    const fixture = setup();

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    fixture.triggerHandle!.update(JSON.stringify({ part: TRIGGER_PART, token: fixture.token, rootId: ROOT_ID, disabled: true }));
    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);

    expect(fixture.opens()).toEqual([]);
  });

  it('port: a long press on a browser that also fires contextmenu sends two open requests', () => {
    const fixture = setup();

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);
    contextMenu(fixture.trigger, 100, 100);

    expect(fixture.opens().map((report) => report.pointerType)).toEqual(['touch', 'mouse']);
  });

  it('port: the pending long press is dropped with the trigger', () => {
    const fixture = setup();

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    fixture.triggerHandle!.dispose();
    vi.advanceTimersByTime(LONG_PRESS_DELAY);

    expect(fixture.opens()).toEqual([]);
  });
});

describe.each([
  ['Server', false],
  ['WebAssembly', true],
] as const)('context-menu: the consumer handlers on %s', (_mode, synchronous) => {
  it('port: a supplied handler is run once per dispatch of its event, through press or a report', () => {
    const fixture = setup({ synchronous, handlers: ['contextmenu', 'touchstart', 'touchmove', 'touchend', 'touchcancel'] });

    contextMenu(fixture.trigger, 40, 50);
    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 1, 2)]);
    touch(fixture.trigger, 'touchmove', [point(fixture.trigger, 3, 4)]);
    touch(fixture.trigger, 'touchend');
    touch(fixture.trigger, 'touchcancel');

    const types = ['contextmenu', 'touchstart', 'touchmove', 'touchend', 'touchcancel'];
    if (synchronous) {
      // Design point 3 (a), settled 2026-11-11: the touch move, end and cancel are decided only while a long press is
      // pending; the touchcancel after the touchend has none, so its handler runs from a report, with no veto to give.
      const decided = types.slice(0, 4);
      expect(fixture.triggerTwin.decisions.map((decision) => [decision.point, decision.event.type])).toEqual(decided.map((type) => [PRESS_POINT, type]));
      expect(fixture.pressings.map((press) => press.event.type)).toEqual(decided);
      expect(fixture.triggerTwin.reported<PressReport>(PRESS_REPORT).map((press) => press.event.type)).toEqual(['touchcancel']);
    } else {
      expect(fixture.triggerTwin.decisions).toEqual([]);
      expect(fixture.triggerTwin.reported<PressReport>(PRESS_REPORT).map((press) => press.event.type)).toEqual(types);
      // The press precedes the open it may veto.
      expect(fixture.triggerTwin.reports.slice(0, 2).map((report) => report.method)).toEqual([PRESS_REPORT, OPEN_REPORT]);
    }
    const [press] = synchronous ? fixture.pressings : fixture.triggerTwin.reported<PressReport>(PRESS_REPORT);
    expect(press).toMatchObject({ clientX: 40, clientY: 50, button: 2, buttons: 2 });
    const [, start] = synchronous ? fixture.pressings : fixture.triggerTwin.reported<PressReport>(PRESS_REPORT);
    expect(start?.touches).toEqual([expect.objectContaining({ identifier: 0, clientX: 1, clientY: 2 })]);
  });

  it('port: a touch move, end or cancel with no pending long press runs its handler from a report, never a decision', () => {
    const fixture = setup({ synchronous, handlers: ['touchmove', 'touchend', 'touchcancel'] });

    touch(fixture.trigger, 'touchmove', [point(fixture.trigger, 3, 4)]);
    touch(fixture.trigger, 'touchend');
    touch(fixture.trigger, 'touchcancel');

    expect(fixture.triggerTwin.decisions).toEqual([]);
    expect(fixture.pressings).toEqual([]);
    expect(fixture.triggerTwin.reported<PressReport>(PRESS_REPORT).map((press) => press.event.type)).toEqual(['touchmove', 'touchend', 'touchcancel']);
  });

  it('port: a disabled trigger still runs the consumer handlers and does nothing of its own', () => {
    const fixture = setup({ synchronous, disabled: true, handlers: ['contextmenu', 'touchstart'] });

    const event = contextMenu(fixture.trigger);
    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 1, 2)]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);

    const asked = synchronous ? fixture.pressings : fixture.triggerTwin.reported<PressReport>(PRESS_REPORT);
    expect(asked.map((press) => press.event.type)).toEqual(['contextmenu', 'touchstart']);
    expect(event.defaultPrevented).toBe(false);
    expect(fixture.opens()).toEqual([]);
  });

  it('port: no handler is asked for or reported when the consumer supplied none', () => {
    const fixture = setup({ synchronous, handlers: ['touchend'] });

    contextMenu(fixture.trigger);
    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 1, 2)]);

    expect(fixture.triggerTwin.decisions).toEqual([]);
    expect(fixture.pressings).toEqual([]);
    expect(fixture.triggerTwin.reported(PRESS_REPORT)).toEqual([]);
    expect(fixture.opens()).toHaveLength(1);
  });
});

describe('context-menu: the consumer veto on WebAssembly', () => {
  it('port: a vetoed right click neither opens nor anchors, and the suppressor still prevents the native menu', () => {
    const fixture = setup({ synchronous: true, verdict: 'stop', handlers: ['contextmenu'] });
    const bubbled = watchBubbling('contextmenu');
    fixture.slot()!.armed = false;

    const event = contextMenu(fixture.trigger, 30, 30);

    expect(fixture.triggerTwin.decisions.map((decision) => decision.point)).toEqual([PRESS_POINT]);
    expect(event.defaultPrevented).toBe(true);
    expect(bubbled()).toBe(true);
    expect(fixture.opens()).toEqual([]);
    expect(fixture.slot()?.armed).toBe(false);
    expect(fixture.anchor()).toEqual({ x: 0, y: 0, width: 0, height: 0 });
  });

  it('port: a vetoed touchstart starts no long press and keeps the release armed', () => {
    const fixture = setup({ synchronous: true, verdict: 'stop', handlers: ['touchstart'] });
    const bubbled = watchBubbling('touchstart');

    touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
    vi.advanceTimersByTime(LONG_PRESS_DELAY);

    expect(bubbled()).toBe(true);
    expect(fixture.slot()?.armed).toBe(true);
    expect(fixture.opens()).toEqual([]);
  });

  it('port: a vetoed touch move, end or cancel keeps the pending long press', () => {
    for (const type of ['touchmove', 'touchend', 'touchcancel']) {
      const fixture = setup({ synchronous: true, verdict: 'stop', handlers: [type] });

      touch(fixture.trigger, 'touchstart', [point(fixture.trigger, 100, 100)]);
      touch(fixture.trigger, type, type === 'touchmove' ? [point(fixture.trigger, 150, 100)] : []);
      vi.advanceTimersByTime(LONG_PRESS_DELAY);

      expect(fixture.opens(), type).toHaveLength(1);
      document.body.replaceChildren();
    }
  });
});
