// L3 (ADR 06 §1): the `interactions` mechanism module in jsdom, against the fake twin and the session harness
// (`test-support/interactions-harness.ts`), which plays C#'s part: it commits a reported request as an uncontrolled popup
// store would, answers `press` and `escape` in its WebAssembly form, and sends the relay in the session's `update`.
// Base UI's titles are kept verbatim (js/CONTEXT.md); each is jsdom-only upstream (`describe.skipIf(!isJSDOM)`). Titles
// that start with `port:` have no Base UI counterpart. Sources of the translated titles: floating-ui-react/utils/
// nodes.test.ts, utils/event.test.ts, hooks/useFloating.test.tsx (FLT-STR-03), and the hook tests named per block.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  acquireSession,
  attach,
  createEventEmitter,
  getNodeAncestors,
  getNodeChildren,
  getSession,
  isTopmostOpenSession,
  releaseSession,
  resetInteractionsForTest,
  type SessionNode,
  type SessionOpenChangeDetails,
} from './interactions';
import { resetFrameScheduler } from './transitions';
import { createFakeTwin } from './test-support/fake-twin';
import { createPopupHarness, type PopupHarness } from './test-support/interactions-harness';

const harnesses: PopupHarness[] = [];

function harness(options: Parameters<typeof createPopupHarness>[0] = {}): PopupHarness {
  const created = createPopupHarness(options);
  harnesses.push(created);
  return created;
}

function element<K extends keyof HTMLElementTagNameMap>(tag: K, id?: string): HTMLElementTagNameMap[K] {
  const created = document.createElement(tag);
  if (id) {
    created.id = id;
  }
  document.body.append(created);
  return created;
}

beforeEach(() => {
  vi.useRealTimers();
});

afterEach(() => {
  for (const created of harnesses.splice(0)) {
    created.dispose();
  }
  resetInteractionsForTest();
  resetFrameScheduler();
  document.body.replaceChildren();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

// ---------------------------------------------------------------------------------------------------------------
// utils/nodes.test.ts (FLT-STR-06, FLT-ALG-20)
// ---------------------------------------------------------------------------------------------------------------

describe('nodes', () => {
  const contextOpen = { open: true };
  const contextClosed = { open: false };

  it('getNodeChildren returns an array of children, ignoring closed ones when onlyOpenChildren=true', () => {
    expect(
      getNodeChildren(
        [
          { id: '0', parentId: null, context: contextOpen },
          { id: '1', parentId: '0', context: contextOpen },
          { id: '2', parentId: '1', context: contextOpen },
          { id: '3', parentId: '1', context: contextOpen },
          { id: '4', parentId: '1', context: contextClosed },
        ],
        '0',
        true,
      ),
    ).toEqual([
      { id: '1', parentId: '0', context: contextOpen },
      { id: '2', parentId: '1', context: contextOpen },
      { id: '3', parentId: '1', context: contextOpen },
    ]);
  });

  it('getNodeChildren returns an array of children, including closed ones when onlyOpenChildren=false', () => {
    expect(
      getNodeChildren(
        [
          { id: '0', parentId: null, context: contextOpen },
          { id: '1', parentId: '0', context: contextOpen },
          { id: '2', parentId: '1', context: contextOpen },
          { id: '3', parentId: '1', context: contextOpen },
          { id: '4', parentId: '1', context: contextClosed },
        ],
        '0',
        false,
      ),
    ).toEqual([
      { id: '1', parentId: '0', context: contextOpen },
      { id: '2', parentId: '1', context: contextOpen },
      { id: '3', parentId: '1', context: contextOpen },
      { id: '4', parentId: '1', context: contextClosed },
    ]);
  });

  it('getNodeChildren handles deep parent structures correctly (onlyOpenChildren=true)', () => {
    const nodes: SessionNode[] = [
      { id: '0', parentId: null, context: contextOpen },
      { id: '1', parentId: '0', context: contextOpen },
      { id: '2', parentId: '1', context: contextClosed },
      { id: '3', parentId: '2', context: contextOpen },
      { id: '4', parentId: '2', context: contextClosed },
      { id: '5', parentId: '0', context: contextOpen },
      { id: '6', parentId: '5', context: contextOpen },
    ];

    expect(getNodeChildren(nodes, '0', true)).toEqual([
      { id: '1', parentId: '0', context: contextOpen },
      { id: '3', parentId: '2', context: contextOpen },
      { id: '5', parentId: '0', context: contextOpen },
      { id: '6', parentId: '5', context: contextOpen },
    ]);
  });

  it('getNodeChildren includes open descendants behind contextless intermediary nodes', () => {
    const nodes: SessionNode[] = [
      { id: '0', parentId: null, context: contextOpen },
      { id: '1', parentId: '0' },
      { id: '2', parentId: '1', context: contextOpen },
    ];

    expect(getNodeChildren(nodes, '0', true)).toEqual([{ id: '2', parentId: '1', context: contextOpen }]);
  });

  it('getNodeChildren handles deep parent structures correctly (onlyOpenChildren=false)', () => {
    const nodes: SessionNode[] = [
      { id: '0', parentId: null, context: contextOpen },
      { id: '1', parentId: '0', context: contextOpen },
      { id: '2', parentId: '1', context: contextClosed },
      { id: '3', parentId: '2', context: contextOpen },
      { id: '4', parentId: '2', context: contextClosed },
      { id: '5', parentId: '0', context: contextOpen },
      { id: '6', parentId: '5', context: contextOpen },
    ];

    expect(getNodeChildren(nodes, '0', false)).toEqual([
      { id: '1', parentId: '0', context: contextOpen },
      { id: '2', parentId: '1', context: contextClosed },
      { id: '3', parentId: '2', context: contextOpen },
      { id: '4', parentId: '2', context: contextClosed },
      { id: '5', parentId: '0', context: contextOpen },
      { id: '6', parentId: '5', context: contextOpen },
    ]);
  });

  it('getNodeAncestors returns an array of ancestors', () => {
    expect(
      getNodeAncestors(
        [
          { id: '0', parentId: null },
          { id: '1', parentId: '0' },
          { id: '2', parentId: '1' },
        ],
        '2',
      ),
    ).toEqual([
      { id: '1', parentId: '0' },
      { id: '0', parentId: null },
    ]);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// utils/event.test.ts (FLT-ALG-26, FLT-A11Y-01). Upstream mocks the platform flags with jsdom off; the port re-imports the
// module over a mutable copy of `./platform` (the dom.test.ts precedent).
// ---------------------------------------------------------------------------------------------------------------

describe('isVirtualPointerEvent', () => {
  const flags = { android: false };

  async function load() {
    vi.resetModules();
    vi.doMock('./platform', async (importOriginal) => {
      const actual = await importOriginal<typeof import('./platform')>();
      return {
        ...actual,
        platform: {
          ...actual.platform,
          env: { ...actual.platform.env, jsdom: false },
          os: {
            ...actual.platform.os,
            get android() {
              return flags.android;
            },
          },
        },
      };
    });
    const module = await import('./interactions');
    vi.doUnmock('./platform');
    return module.isVirtualPointerEvent;
  }

  function pointerEvent(overrides: Partial<PointerEvent>): PointerEvent {
    return {
      type: 'pointerdown',
      width: 1,
      height: 1,
      pressure: 0,
      detail: 0,
      buttons: 0,
      pointerType: 'mouse',
      ...overrides,
    } as PointerEvent;
  }

  it('recognizes a pressureless 1x1 mouse pointerdown as a screen reader press', async () => {
    const isVirtualPointerEvent = await load();
    expect(isVirtualPointerEvent(pointerEvent({}))).toBe(true);
  });

  it('ignores a pressed mouse', async () => {
    const isVirtualPointerEvent = await load();
    expect(isVirtualPointerEvent(pointerEvent({ pressure: 0.5 }))).toBe(false);
    expect(isVirtualPointerEvent(pointerEvent({ pressure: 0, buttons: 1 }))).toBe(false);
  });

  it('recognizes a TalkBack press with a pressed button on Android', async () => {
    const isVirtualPointerEvent = await load();
    flags.android = true;
    try {
      expect(isVirtualPointerEvent(pointerEvent({ buttons: 1 }))).toBe(true);
      expect(isVirtualPointerEvent(pointerEvent({ buttons: 1, pressure: 0.5 }))).toBe(false);
    } finally {
      flags.android = false;
    }
  });

  it('ignores the same shape on other pointer types', async () => {
    const isVirtualPointerEvent = await load();
    expect(isVirtualPointerEvent(pointerEvent({ pointerType: 'touch' }))).toBe(false);
    expect(isVirtualPointerEvent(pointerEvent({ pointerType: 'pen' }))).toBe(false);
  });

  it('ignores the same shape on events other than pointerdown', async () => {
    const isVirtualPointerEvent = await load();
    expect(isVirtualPointerEvent(pointerEvent({ type: 'pointerup' }))).toBe(false);
    expect(isVirtualPointerEvent(pointerEvent({ type: 'pointermove' }))).toBe(false);
  });

  it('recognizes an iOS VoiceOver touch', async () => {
    const isVirtualPointerEvent = await load();
    expect(isVirtualPointerEvent(pointerEvent({ pointerType: 'touch', width: 0.333, height: 0.333 }))).toBe(true);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The session (ADR 04 Amendments 2026-10-27, decision 3; FLT-STR-03..09, FLT-STA-02/03, FLT-EVT-02)
// ---------------------------------------------------------------------------------------------------------------

describe('the session', () => {
  it('port: the bus calls every listener of a name in registration order, a second registration is a no-op, and off removes one', () => {
    const events = createEventEmitter();
    const calls: string[] = [];
    const first = (data: unknown) => calls.push(`first:${String(data)}`);
    const second = (data: unknown) => calls.push(`second:${String(data)}`);
    events.on('openchange', first);
    events.on('openchange', second);
    events.on('openchange', first);
    events.on('other', second);

    events.emit('openchange', 1);
    events.off('openchange', first);
    events.emit('openchange', 2);

    expect(calls).toEqual(['first:1', 'second:1', 'second:2']);
  });

  it('uses the supplied root store while preserving DOM and position references', () => {
    const popup = harness();
    const referenceElement = element('button', 'reference');
    const floatingElement = element('div');
    popup.attachSession();
    popup.attachTrigger(referenceElement);
    popup.attachFloating(floatingElement);
    popup.setOpen(true, 'none', 'reference');

    const session = getSession(popup.token)!;
    const context = session.dataRef.current.floatingContext!;

    expect(session.node.context).toBe(context);
    expect(context.dataRef).toBe(session.dataRef);
    expect(context.events).toBe(session.events);
    expect(context.elements.floating).toBe(floatingElement);
    expect(context.elements.domReference).toBe(referenceElement);
    expect(session.domReferenceElement).toBe(referenceElement);
    expect(session.positionReference).toBeNull();
  });

  it('port: the first handle that names a token creates its session in any attach order, and the last dispose deletes it', () => {
    const popup = harness();
    const trigger = popup.attachTrigger(element('button', 'trigger'));
    const created = getSession(popup.token);
    expect(created?.triggerElements.getById('trigger')).toBe(document.getElementById('trigger'));

    const root = popup.attachSession();
    const floating = popup.attachFloating(element('div'));
    expect(getSession(popup.token)).toBe(created);

    trigger.dispose();
    root.dispose();
    expect(getSession(popup.token)).toBe(created);
    expect(created?.triggerElements.size).toBe(0);

    floating.dispose();
    floating.dispose();
    expect(getSession(popup.token)).toBeUndefined();
  });

  it('port: another module holds a session through acquireSession until it releases it, and the reset clears the map', () => {
    const held = acquireSession('positioned');
    const popup = harness({ token: 'positioned' });
    popup.attachSession().dispose();
    expect(getSession('positioned')).toBe(held);

    releaseSession(held);
    expect(getSession('positioned')).toBeUndefined();

    acquireSession('kept');
    resetInteractionsForTest();
    expect(getSession('kept')).toBeUndefined();
  });

  it('port: the token form attaches an elementless session from the options token and never resolves an element', () => {
    const fake = createFakeTwin();
    const handle = attach(null, JSON.stringify({ sessionToken: 'no-element', session: { open: true } }), fake.twin);

    expect(getSession('no-element')?.open).toBe(true);
    handle.dispose();
    expect(getSession('no-element')).toBeUndefined();
  });

  it('port: the session emits openchange once per relayed revision, after recording the open event, and none for an update that does not move it', () => {
    const popup = harness();
    popup.attachSession();
    popup.attachTrigger(element('button', 'trigger'));
    const session = getSession(popup.token)!;
    const seen: { details: SessionOpenChangeDetails; openEvent: Event | undefined; open: boolean }[] = [];
    session.events.on('openchange', (details: SessionOpenChangeDetails) => {
      seen.push({ details, openEvent: session.dataRef.current.openEvent, open: session.open });
    });

    popup.setOpen(true, 'trigger-press', 'trigger');
    popup.updateSession({});
    popup.setOpen(false, 'outside-press');

    expect(seen.map((entry) => [entry.details.open, entry.details.reason, entry.open])).toEqual([
      [true, 'trigger-press', false],
      [false, 'outside-press', true],
    ]);
    expect(seen[0]?.details.triggerElement).toBe(document.getElementById('trigger'));
    expect(seen[0]?.details.nested).toBe(false);
    expect(session.open).toBe(false);
    expect(session.dataRef.current.openEvent).toBeUndefined();
  });

  it('port: a cancelled request moves no revision and emits nothing, and a committed one carries its own event', () => {
    let cancel = true;
    const popup = harness({
      onOpenChange: (_open, details) => {
        if (cancel) {
          details.cancel();
        }
      },
    });
    popup.attachSession();
    const session = getSession(popup.token)!;
    const reasons: (string | undefined)[] = [];
    const events: (Event | undefined)[] = [];
    session.events.on('openchange', (details: SessionOpenChangeDetails) => {
      reasons.push(details.reason);
      events.push(details.nativeEvent);
    });
    const fake = createFakeTwin();
    const request = attach(element('button', 'other'), JSON.stringify({ sessionToken: popup.token, trigger: {} }), fake.twin);

    popup.setOpen(true, 'trigger-press');
    expect(popup.revision).toBe(0);
    expect(reasons).toEqual([]);

    cancel = false;
    popup.setOpen(true, 'trigger-press', 'other');
    expect(popup.revision).toBe(1);
    expect(reasons).toEqual(['trigger-press']);
    expect(events[0]).toBeUndefined();
    request.dispose();
  });

  it('port: a nested session joins its parent through the cascaded token; the tree sees the open child and the topmost gate the later open', () => {
    const outer = harness();
    const inner = harness();
    outer.attachSession();
    outer.attachFloating(element('div'));
    inner.attachSession({ session: { parentToken: outer.token } });
    inner.attachFloating(element('div'));
    const outerSession = getSession(outer.token)!;
    const innerSession = getSession(inner.token)!;
    const nested: boolean[] = [];
    innerSession.events.on('openchange', (details: SessionOpenChangeDetails) => nested.push(details.nested));

    outer.setOpen(true);
    inner.setOpen(true);

    expect(innerSession.nested).toBe(true);
    expect(nested).toEqual([true]);
    expect(getNodeChildren(outerSession.tree.nodesRef.current, outer.token).map((node) => node.id)).toEqual([inner.token]);
    expect(getNodeAncestors(innerSession.tree.nodesRef.current, inner.token).map((node) => node.id)).toEqual([outer.token]);
    expect(innerSession.tree.events).toBe(outerSession.tree.events);
    expect(isTopmostOpenSession(innerSession)).toBe(true);
    expect(isTopmostOpenSession(outerSession)).toBe(false);

    inner.setOpen(false);
    expect(getNodeChildren(outerSession.tree.nodesRef.current, outer.token)).toEqual([]);
    expect(isTopmostOpenSession(outerSession)).toBe(true);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// useFocus (useFocus.test.tsx; FLT-FOC-01..04, FLT-API-15, FLT-STA-02)
// ---------------------------------------------------------------------------------------------------------------

describe('useFocus', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  function setup(delay?: number) {
    const popup = harness();
    popup.attachSession();
    const button = element('button');
    popup.attachTrigger(button, { trigger: { focus: delay === undefined ? {} : { delay } } });
    popup.setReference(button);
    const floating = element('div');
    popup.attachFloating(floating);
    return { popup, button, floating };
  }

  it('does not reopen when focus is restored after leaving the tab', async () => {
    const { popup, button } = setup(100);

    button.focus();

    window.dispatchEvent(new Event('blur'));
    button.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));

    await vi.advanceTimersByTimeAsync(200);

    expect(popup.open).toBe(false);

    // Port: the block lasts until the pointer leaves the reference; the next focus then opens after the delay.
    button.blur();
    button.dispatchEvent(new MouseEvent('mouseleave'));
    button.focus();
    await vi.advanceTimersByTimeAsync(100);
    expect(popup.open).toBe(true);
  });

  it('port: a focus opens with reason trigger-focus at once without a delay, and after the delay with one', async () => {
    const immediate = setup();
    immediate.button.focus();
    expect(immediate.popup.open).toBe(true);
    expect(immediate.popup.changes[0]?.details.reason).toBe('trigger-focus');
    expect(immediate.popup.changes[0]?.details.triggerId).toBe(immediate.button.id);
    immediate.button.blur();
    await vi.advanceTimersByTimeAsync(0);

    const delayed = setup(100);
    delayed.button.focus();
    expect(delayed.popup.open).toBe(false);
    await vi.advanceTimersByTimeAsync(100);
    expect(delayed.popup.open).toBe(true);
  });

  it('port: a press or Escape close relayed to the session blocks the next focus on that reference until the pointer leaves it', () => {
    const { popup, button } = setup();
    popup.setOpen(true, 'trigger-press', button.id);
    popup.setOpen(false, 'escape-key');

    button.focus();
    expect(popup.open).toBe(false);

    button.blur();
    button.dispatchEvent(new MouseEvent('mouseleave'));
    button.focus();
    expect(popup.open).toBe(true);
  });

  it('port: a blur closes after 0 ms unless focus moved into the floating element, onto an outside focus guard or onto another enabled trigger', async () => {
    const { popup, button, floating } = setup();
    const inside = document.createElement('button');
    floating.append(inside);
    const guard = element('span');
    guard.tabIndex = 0;
    guard.setAttribute('data-base-ui-focus-guard', '');
    guard.setAttribute('data-type', 'outside');
    const other = element('button');
    popup.attachTrigger(other);
    const outside = element('button');

    button.focus();
    expect(popup.open).toBe(true);

    inside.focus();
    await vi.advanceTimersByTimeAsync(0);
    expect(popup.open).toBe(true);

    button.focus();
    guard.focus();
    await vi.advanceTimersByTimeAsync(0);
    expect(popup.open).toBe(true);

    button.focus();
    other.focus();
    await vi.advanceTimersByTimeAsync(0);
    expect(popup.open).toBe(true);

    button.focus();
    outside.focus();
    expect(popup.open).toBe(true);
    await vi.advanceTimersByTimeAsync(0);
    expect(popup.open).toBe(false);
    expect(popup.changes.at(-1)?.details.reason).toBe('trigger-focus');
  });

  it('port: a focus moving between two enabled triggers of an open popup opens at once despite the delay', () => {
    const { popup, button } = setup(100);
    const other = element('button');
    popup.attachTrigger(other, { trigger: { focus: { delay: 100 } } });
    popup.setOpen(true, 'trigger-hover', button.id);

    button.focus();
    other.focus();

    expect(popup.changes.at(-1)?.details.triggerId).toBe(other.id);
    expect(popup.activeTriggerId).toBe(other.id);
  });
});

// #region click tests
// #endregion click tests

// #region dismiss tests
// ---------------------------------------------------------------------------------------------------------------
// useDismiss (useDismiss.test.tsx; FLT-DIS-01..06, FLT-DIS-08..16, FLT-KEY-02/03, FLT-PTR-26..30, FLT-API-01/02/07,
// FLT-A11Y-02/05, FLT-ALG-17, FLT-STR-07, FLT-FOC-01, FLT-EVT-01) and the `escape` point (FLT-KEY-01, FLT-EVT-05; the
// port titles at the end). The translation:
// - An `App` renders a reference button, a floating element and the hooks: a harness plays the popup, the button is a
//   trigger handle made the anchor with `setReference` (the test shim's `refs.setReference`), and "not in the document"
//   reads `open` false, since the port keeps a closed popup's element. A consumer's `onClick`/`onPointerDown` that sets
//   state is a listener that calls `setOpen`, as the store write would.
// - `useClick` (the click region) is stood in by the `setOpen` its press would request.
// - `FloatingPortal` places the floating element in `document.body` (or the container given); a nested popup is a
//   harness whose `session.parentToken` is the enclosing token: the `FloatingTree`/`FloatingNode` relation, and the
//   React ancestry a portal keeps.
// - `FloatingFocusManager` (track 4) is stood in by what it leaves that dismissal reads: the `data-base-ui-inert` marker
//   on the elements outside the popup at open, and the element focused when an Escape is pressed.
// - Base UI's `outsidePress` function becomes the Dialog predicate set of the port (`primaryButtonOnly`, `singleTouch`,
//   `ownBackdropIds`, `topmost`); each title names the predicate that answers as the function did.
// - Titles an Escape closes on run in both modes (`inBothModes`), since the `escape` point is on their path.
// - An absence-only title ends with a `// Port:` step that needs the behaviour, so each title fails without it.
// - Left out: "dismisses with escape key", "calls preventDefault on escape key dismiss", "does not call preventDefault on
//   escape key if close is canceled" (FLT-KEY-01, FLT-EVT-05: L4, the worker's; the verdicts are proven here by port
//   titles); "dismisses when clicking outside a shared shadow root while focus is managed" (the focus manager, track 4;
//   without it the title is the one before it). FLT-DIS-07 (the scrollbar press) needs layout and has no title.
// ---------------------------------------------------------------------------------------------------------------
import { normalizeBubbles as normalizeDismissBubbles, type DismissOptions as DismissProps } from './interactions';

describe('useDismiss', () => {
  const sloppy = { mouse: 'sloppy', touch: 'sloppy' } as const;
  const intentional = { mouse: 'intentional', touch: 'intentional' } as const;
  // FLT-EVT-01: the App helper asserts the reason of every change (useDismiss.test.tsx:41-55). A mismatch is recorded
  // and failed after the test, since a throw inside the harness's commit would land in a DOM listener.
  const reasonErrors: string[] = [];

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    expect(reasonErrors.splice(0)).toEqual([]);
  });

  function dispatch<E extends Event>(target: EventTarget, event: E): E {
    target.dispatchEvent(event);
    return event;
  }

  // `fireEvent`'s defaults: the events bubble, are cancelable and composed.
  const pointer = (target: EventTarget, type: string, init: PointerEventInit = {}) =>
    dispatch(target, new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, ...init }));
  const mouse = (target: EventTarget, type: string, init: MouseEventInit = {}) =>
    dispatch(target, new MouseEvent(type, { bubbles: true, cancelable: true, composed: true, ...init }));
  const keyDown = (target: EventTarget, init: KeyboardEventInit = {}) =>
    dispatch(target, new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true, composed: true, ...init }));
  const composition = (target: EventTarget, type: 'compositionstart' | 'compositionend') =>
    dispatch(target, new CompositionEvent(type, { bubbles: true, cancelable: true, composed: true }));

  interface TouchPoint {
    identifier: number;
    target: EventTarget;
    clientX: number;
    clientY: number;
  }

  // jsdom's TouchEvent takes no touch lists in its init, so they are defined on the event, as `fireEvent` does.
  const touch = (target: EventTarget, type: string, touches: TouchPoint[], changedTouches: TouchPoint[] = touches) => {
    const event = new TouchEvent(type, { bubbles: true, cancelable: true, composed: true });
    Object.defineProperty(event, 'touches', { value: touches });
    Object.defineProperty(event, 'changedTouches', { value: changedTouches });
    return dispatch(target, event);
  };

  // user-event's primary mouse click: the pointer, mouse and click events of one press.
  const userClick = (target: EventTarget) => {
    pointer(target, 'pointerdown', { pointerType: 'mouse', button: 0, buttons: 1 });
    mouse(target, 'mousedown', { button: 0, buttons: 1, detail: 1 });
    pointer(target, 'pointerup', { pointerType: 'mouse', button: 0 });
    mouse(target, 'mouseup', { button: 0, detail: 1 });
    pointer(target, 'click', { pointerType: 'mouse', button: 0, detail: 1 });
  };

  // user-event's `{Escape}`: a keydown and a keyup on the focused element.
  const userEscape = () => {
    const target = document.activeElement ?? document.body;
    const event = keyDown(target);
    dispatch(target, new KeyboardEvent('keyup', { key: 'Escape', bubbles: true, cancelable: true, composed: true }));
    return event;
  };

  const allowPropagation = (details: { allowPropagation(): void }) => details.allowPropagation();
  const cancel = (details: { cancel(): void }) => details.cancel();
  const cancelAndAllowPropagation = (details: { cancel(): void; allowPropagation(): void }) => {
    details.cancel();
    details.allowPropagation();
  };

  // Runs a body on Blazor Server, then on WebAssembly, with a clean page in between.
  async function inBothModes(body: (synchronous: boolean) => void | Promise<void>): Promise<void> {
    for (const synchronous of [false, true]) {
      try {
        await body(synchronous);
      } catch (error) {
        if (error instanceof Error) {
          error.message = `[synchronous: ${synchronous}] ${error.message}`;
        }
        throw error;
      }
      for (const created of harnesses.splice(0)) {
        created.dispose();
      }
      resetInteractionsForTest();
      document.body.replaceChildren();
      vi.clearAllTimers();
    }
  }

  interface AppProps extends DismissProps {
    referencePress?: boolean;
    onClose?: () => void;
    synchronous?: boolean;
  }

  // useDismiss.test.tsx:34-71: an open popup, its reference button and a floating element with an input.
  function renderApp(props: AppProps = {}) {
    const { referencePress = false, onClose, synchronous = false, ...dismiss } = props;
    const popup = harness({
      open: true,
      synchronous,
      onOpenChange(open, details) {
        const expected = dismiss.outsidePress ? 'outside-press' : dismiss.escapeKey ? 'escape-key' : referencePress ? 'trigger-press' : null;
        if (expected !== null && details.reason !== expected) {
          reasonErrors.push(`expected the reason ${expected}, got ${details.reason}`);
        }
        if (!dismiss.outsidePress && dismiss.escapeKey && !open) {
          onClose?.();
        }
      },
    });
    popup.attachSession({ session: { dismiss } });
    const reference = element('button');
    popup.attachTrigger(reference, { trigger: { referencePress } });
    popup.setReference(reference);
    const floating = element('div');
    floating.setAttribute('role', 'tooltip');
    const input = document.createElement('input');
    floating.append(input);
    popup.attachFloating(floating);
    return { popup, reference, floating, input };
  }

  // PersistentDismiss: the hook stays mounted while closed; the open and close buttons set the state directly.
  function renderPersistentDismiss(synchronous: boolean, withInput = false) {
    const popup = harness({ open: true, synchronous });
    popup.attachSession({ session: { dismiss: {} } });
    const reference = element('button');
    popup.attachTrigger(reference);
    popup.setReference(reference);
    const openButton = element('button');
    openButton.textContent = 'open';
    openButton.addEventListener('click', () => popup.setOpen(true));
    const closeButton = element('button');
    closeButton.textContent = 'close';
    closeButton.addEventListener('click', () => popup.setOpen(false));
    const tooltip = element('div');
    tooltip.setAttribute('role', 'tooltip');
    const input = document.createElement('input');
    if (withInput) {
      tooltip.append(input);
    }
    popup.attachFloating(tooltip);
    return { popup, openButton, closeButton, input };
  }

  describe('default options', () => {
    it('registers outside press touch listeners as passive', () => {
      const addEventListenerSpy = vi.spyOn(document, 'addEventListener');

      renderApp();

      for (const eventName of ['touchstart', 'touchmove', 'touchend']) {
        expect(addEventListenerSpy).toHaveBeenCalledWith(eventName, expect.any(Function), { capture: true, passive: true });
      }
    });

    it('does not dismiss with escape key if IME is active', async () => {
      await inBothModes(async (synchronous) => {
        const onClose = vi.fn();
        const { input } = renderApp({ onClose, escapeKey: true, synchronous });

        input.focus();

        // "あ" is entered and Escape is pressed to cancel the IME composition.
        input.value = 'あ';
        input.dispatchEvent(new Event('change', { bubbles: true }));
        composition(input, 'compositionstart');
        keyDown(input);
        composition(input, 'compositionend');

        // The compositionend timeout tick (Safari).
        await vi.advanceTimersByTimeAsync(0);

        expect(onClose).toHaveBeenCalledTimes(0);

        keyDown(input);

        expect(onClose).toHaveBeenCalledTimes(1);
      });
    });

    it('does not dismiss with escape key while a composition spans a reopen', async () => {
      await inBothModes(async (synchronous) => {
        const { popup, openButton, closeButton } = renderPersistentDismiss(synchronous);

        composition(document.body, 'compositionstart');
        mouse(closeButton, 'click');
        mouse(openButton, 'click');
        expect(popup.open).toBe(true);

        keyDown(document.body, { isComposing: true });
        await Promise.resolve();
        expect(popup.open).toBe(true);

        // Port: once that composition ends and settles, Escape closes.
        composition(document.body, 'compositionend');
        await vi.advanceTimersByTimeAsync(0);
        keyDown(document.body);
        expect(popup.open).toBe(false);
      });
    });

    it('does not dismiss with the escape key that ends a composition spanning a reopen', async () => {
      await inBothModes(async (synchronous) => {
        const { popup, openButton, closeButton } = renderPersistentDismiss(synchronous);

        composition(document.body, 'compositionstart');
        mouse(closeButton, 'click');
        mouse(openButton, 'click');

        // Safari fires `compositionend` before the `keydown` that ends the composition.
        composition(document.body, 'compositionend');
        keyDown(document.body);
        await Promise.resolve();
        expect(popup.open).toBe(true);

        // Port: the next Escape, once the composition settled, closes.
        await vi.advanceTimersByTimeAsync(0);
        keyDown(document.body);
        expect(popup.open).toBe(false);
      });
    });

    it('dismisses with escape key after closing during IME composition', async () => {
      await inBothModes((synchronous) => {
        const { popup, openButton, closeButton, input } = renderPersistentDismiss(synchronous, true);

        // The hook stays mounted while closed, so the composition that ends after closing must not keep blocking
        // Escape once it reopens.
        composition(input, 'compositionstart');
        mouse(closeButton, 'click');
        expect(popup.open).toBe(false);
        composition(document.body, 'compositionend');

        mouse(openButton, 'click');
        expect(popup.open).toBe(true);

        keyDown(document.body);
        expect(popup.open).toBe(false);
      });
    });

    it.each([6, 12])('does not dismiss a reopened popup when an old touch moves %s pixels and ends', (distance) => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: {} } });
      const button = element('button');
      popup.attachTrigger(button);
      popup.setReference(button);
      button.addEventListener('click', () => popup.setOpen(!popup.open, 'trigger-press', button.id));
      const tooltip = element('div');
      tooltip.setAttribute('role', 'tooltip');
      popup.attachFloating(tooltip);

      const point = { identifier: 1, target: document.body, clientX: 10, clientY: 10 };
      touch(document.body, 'touchstart', [point]);

      mouse(button, 'click');
      expect(popup.open).toBe(false);
      mouse(button, 'click');
      expect(popup.open).toBe(true);

      // The deferred touchend dismissal and the immediate touchmove dismissal both belong to the previous session.
      const movedTouch = { ...point, clientX: point.clientX + distance };
      touch(document.body, 'touchmove', [movedTouch]);
      touch(document.body, 'touchend', [], [movedTouch]);
      expect(popup.open).toBe(true);

      // A new gesture in this session still dismisses.
      touch(document.body, 'touchstart', [point]);
      touch(document.body, 'touchmove', [{ ...point, clientX: 22 }]);
      expect(popup.open).toBe(false);
    });

    it('dismisses with outside pointer press', () => {
      const { popup } = renderApp();
      userClick(document.body);
      expect(popup.open).toBe(false);
    });

    it.each([false, true])('clears the inside marker when the interaction owner unmounts (strict: %s)', (strict) => {
      // Restated: the interaction owner is the session handle's dismissal half (the Root's `useDismiss`); the root store
      // (the session) outlives it because the floating handle holds the session. Strict Mode's mount, unmount and
      // remount is an attach, a dispose and an attach, and a second dispose is a no-op.
      const popup = harness({ open: true });
      const tooltip = element('div');
      tooltip.setAttribute('role', 'tooltip');
      const button = document.createElement('button');
      button.type = 'button';
      tooltip.append(button);
      popup.attachFloating(tooltip);
      const mountInteraction = (outsidePress: boolean) =>
        popup.attachSession({ session: { dismiss: { outsidePress, outsidePressEvent: sloppy } } });

      let interaction = mountInteraction(false);
      if (strict) {
        interaction.dispose();
        interaction = mountInteraction(false);
      }

      mouse(button, 'click');
      interaction.dispose();
      if (strict) {
        interaction.dispose();
      }
      mountInteraction(true);
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });

      expect(popup.open).toBe(false);
    });

    it('dismisses with reference press', () => {
      const { popup, reference } = renderApp({ referencePress: true });
      pointer(reference, 'pointerdown');
      expect(popup.open).toBe(false);
    });

    it('dismisses with native click', () => {
      const { popup, reference } = renderApp({ referencePress: true });
      mouse(reference, 'click');
      expect(popup.open).toBe(false);
    });

    it('outsidePress function guard', () => {
      // `outsidePress={() => false}`: `topmost` vetoes the press while a popup opened later is open.
      const { popup } = renderApp({ outsidePress: true, topmost: true });
      const later = harness();
      later.attachSession();
      later.setOpen(true);

      userClick(document.body);
      expect(popup.open).toBe(true);

      // Port: once the later popup closes, the guard passes and the press closes.
      later.setOpen(false);
      userClick(document.body);
      expect(popup.open).toBe(false);
    });

    it('outsidePress ignored for third party elements', () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: {} } });
      const container = element('div');
      const reference = document.createElement('button');
      const dialog = document.createElement('div');
      dialog.setAttribute('role', 'dialog');
      container.append(reference, dialog);
      popup.attachTrigger(reference);
      popup.setReference(reference);
      popup.attachFloating(dialog);
      const presentAtOpen = element('div');
      // The modal FloatingFocusManager marks what lies outside the floating element at open.
      reference.setAttribute('data-base-ui-inert', '');
      presentAtOpen.setAttribute('data-base-ui-inert', '');

      const thirdParty = document.createElement('div');
      thirdParty.setAttribute('data-testid', 'third-party');
      document.body.append(thirdParty);
      userClick(thirdParty);
      expect(popup.open).toBe(true);
      thirdParty.remove();

      // Port: a press on an element marked at open closes.
      userClick(presentAtOpen);
      expect(popup.open).toBe(false);
    });

    it('dismisses when clicking outside a shared shadow root', () => {
      const host = element('div');
      const shadowRoot = host.attachShadow({ mode: 'open' });
      const container = document.createElement('div');
      shadowRoot.appendChild(container);
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: {} } });
      const reference = document.createElement('button');
      container.append(reference);
      popup.attachTrigger(reference);
      popup.setReference(reference);
      // FloatingPortal container={shadowRoot}.
      const portalNode = document.createElement('div');
      shadowRoot.append(portalNode);
      const dialog = document.createElement('div');
      dialog.setAttribute('role', 'dialog');
      portalNode.append(dialog);
      popup.attachFloating(dialog);

      userClick(document.body);

      expect(popup.open).toBe(false);
      host.remove();
    });

    it('outsidePress not ignored for nested floating elements', () => {
      function renderNested(modal: readonly [boolean, boolean] | null) {
        const popover = (id: string, container: HTMLElement) => {
          const popup = harness({ open: true });
          popup.attachSession({ session: { dismiss: {} } });
          const reference = document.createElement('button');
          const dialog = document.createElement('div');
          dialog.setAttribute('role', 'dialog');
          dialog.setAttribute('data-testid', id);
          container.append(reference, dialog);
          popup.attachTrigger(reference);
          popup.setReference(reference);
          popup.attachFloating(dialog);
          return { popup, reference, dialog };
        };
        // No FloatingTree: the second popover renders inside the first one's floating element, with no tree relation.
        const popover1 = popover('popover-1', element('div'));
        const popover2 = popover('popover-2', popover1.dialog);
        // A modal FloatingFocusManager marks what lies outside its floating element at open.
        if (modal ? modal[0] : true) {
          popover1.reference.setAttribute('data-base-ui-inert', '');
        }
        if (modal?.[1]) {
          popover1.reference.setAttribute('data-base-ui-inert', '');
          popover2.reference.setAttribute('data-base-ui-inert', '');
        }
        return { popover1, popover2 };
      }

      for (const modal of [[true, true], [true, false], [false, true], null] as const) {
        const { popover1, popover2 } = renderNested(modal);

        userClick(popover2.dialog);
        expect(popover1.popup.open).toBe(true);
        expect(popover2.popup.open).toBe(true);
        userClick(popover1.dialog);
        expect(popover2.popup.open).toBe(false);

        popover1.popup.dispose();
        popover2.popup.dispose();
        document.body.replaceChildren();
      }
    });
  });

  describe('options set to false', () => {
    it('does not dismiss with escape key', async () => {
      const { popup } = renderApp({ escapeKey: false });
      keyDown(document.body);
      expect(popup.open).toBe(true);
      await Promise.resolve();

      // Port: the dismissal is live: an outside press closes.
      userClick(document.body);
      expect(popup.open).toBe(false);
    });

    it('does not dismiss with outside press', async () => {
      await inBothModes((synchronous) => {
        const { popup } = renderApp({ outsidePress: false, synchronous });
        userClick(document.body);
        expect(popup.open).toBe(true);

        // Port: the dismissal is live: Escape closes.
        keyDown(document.body);
        expect(popup.open).toBe(false);
      });
    });

    it('does not dismiss with reference pointer down', () => {
      const { popup, reference } = renderApp({ referencePress: false });
      userClick(reference);
      expect(popup.open).toBe(true);

      // Port: the dismissal is live: an outside press closes.
      userClick(document.body);
      expect(popup.open).toBe(false);
    });

    it('does not dismiss when clicking portaled children', async () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: {} } });
      const reference = element('button');
      popup.attachTrigger(reference);
      popup.setReference(reference);
      popup.attachFloating(element('div'));
      // FloatingPortal inside the floating element: its node lies in document.body, outside the floating element in the
      // DOM and inside it in the React tree; a session whose parent is the popup's stands in for that ancestry.
      const portal = harness();
      portal.attachSession({ session: { parentToken: popup.token } });
      const portalNode = element('div');
      portal.attachFloating(portalNode);
      const portaledButton = document.createElement('button');
      portaledButton.setAttribute('data-testid', 'portaled-button');
      portalNode.append(portaledButton);

      pointer(portaledButton, 'pointerdown');
      await Promise.resolve();

      expect(popup.open).toBe(true);

      // Port: a press outside both closes.
      pointer(document.body, 'pointerdown');
      expect(popup.open).toBe(false);
    });

    it('outsidePress function guard', () => {
      // `outsidePress={() => true}`: `primaryButtonOnly` passes this primary press.
      const { popup } = renderApp({ outsidePress: true, primaryButtonOnly: true });
      userClick(document.body);
      expect(popup.open).toBe(false);
    });
  });

  describe('prop: bubbles', () => {
    // Dialog with a FloatingNode inside one FloatingTree: the nested dialog is a harness whose parent token is the
    // enclosing one's, rendered (without a portal) inside the enclosing floating element.
    function renderDialog(
      testId: string,
      props: DismissProps,
      options: { parent?: PopupHarness; container?: HTMLElement; synchronous?: boolean } = {},
    ) {
      const popup = harness({ open: true, synchronous: options.synchronous ?? false });
      popup.attachSession({ session: { dismiss: props, parentToken: options.parent?.token ?? null } });
      const reference = document.createElement('button');
      const floating = document.createElement('div');
      floating.setAttribute('data-testid', testId);
      (options.container ?? document.body).append(reference, floating);
      popup.attachTrigger(reference);
      popup.setReference(reference);
      popup.attachFloating(floating);
      return { popup, reference, floating };
    }

    function renderNestedDialogs(outerProps: DismissProps, innerProps: DismissProps, synchronous = false) {
      const outer = renderDialog('outer', outerProps, { synchronous });
      const inner = renderDialog('inner', innerProps, { parent: outer.popup, container: outer.floating, synchronous });
      const testButton = document.createElement('button');
      testButton.textContent = 'test button';
      inner.floating.append(testButton);
      return { outer, inner, testButton };
    }

    describe('normalizeProp', () => {
      it('undefined', () => {
        const { escapeKey: escapeKeyBubbles, outsidePress: outsidePressBubbles } = normalizeDismissBubbles();

        expect(escapeKeyBubbles).toBe(false);
        expect(outsidePressBubbles).toBe(true);
      });

      it('when false', () => {
        const { escapeKey: escapeKeyBubbles, outsidePress: outsidePressBubbles } = normalizeDismissBubbles(false);

        expect(escapeKeyBubbles).toBe(false);
        expect(outsidePressBubbles).toBe(false);
      });

      it('{}', () => {
        const { escapeKey: escapeKeyBubbles, outsidePress: outsidePressBubbles } = normalizeDismissBubbles({});

        expect(escapeKeyBubbles).toBe(false);
        expect(outsidePressBubbles).toBe(true);
      });

      it('{ escapeKey: false }', () => {
        const { escapeKey: escapeKeyBubbles, outsidePress: outsidePressBubbles } = normalizeDismissBubbles({ escapeKey: false });

        expect(escapeKeyBubbles).toBe(false);
        expect(outsidePressBubbles).toBe(true);
      });

      it('{ outsidePress: false }', () => {
        const { escapeKey: escapeKeyBubbles, outsidePress: outsidePressBubbles } = normalizeDismissBubbles({ outsidePress: false });

        expect(escapeKeyBubbles).toBe(false);
        expect(outsidePressBubbles).toBe(false);
      });
    });

    describe('prop: bubbles.outsidePress', () => {
      it('when true', () => {
        const { outer, inner } = renderNestedDialogs({}, {});

        expect(outer.popup.open).toBe(true);
        expect(inner.popup.open).toBe(true);

        pointer(document.body, 'pointerdown');

        expect(outer.popup.open).toBe(false);
        expect(inner.popup.open).toBe(false);
      });

      it('when false', () => {
        const { outer, inner } = renderNestedDialogs({ bubbles: { outsidePress: false } }, { bubbles: { outsidePress: false } });

        expect(outer.popup.open).toBe(true);
        expect(inner.popup.open).toBe(true);

        pointer(document.body, 'pointerdown');

        expect(outer.popup.open).toBe(true);
        expect(inner.popup.open).toBe(false);

        pointer(document.body, 'pointerdown');

        expect(outer.popup.open).toBe(false);
        expect(inner.popup.open).toBe(false);
      });

      it('mixed', () => {
        const { outer, inner } = renderNestedDialogs({ bubbles: { outsidePress: true } }, { bubbles: { outsidePress: false } });

        expect(outer.popup.open).toBe(true);
        expect(inner.popup.open).toBe(true);

        pointer(document.body, 'pointerdown');

        expect(outer.popup.open).toBe(true);
        expect(inner.popup.open).toBe(false);

        pointer(document.body, 'pointerdown');

        expect(outer.popup.open).toBe(false);
        expect(inner.popup.open).toBe(false);
      });
    });

    describe('prop: bubbles.escapeKey', () => {
      it('without FloatingTree', async () => {
        await inBothModes((synchronous) => {
          // Two popups with no tree: the tooltip's reference lies inside the popover's floating element, and focus opens
          // the tooltip (useFocus on its trigger).
          const popover = harness({ open: true, synchronous });
          popover.attachSession({ session: { dismiss: {} } });
          const popoverReference = element('button');
          popover.attachTrigger(popoverReference);
          popover.setReference(popoverReference);
          const dialog = element('div');
          dialog.setAttribute('role', 'dialog');
          popover.attachFloating(dialog);
          const tooltip = harness({ synchronous });
          tooltip.attachSession({ session: { dismiss: {} } });
          const focusButton = document.createElement('button');
          focusButton.setAttribute('data-testid', 'focus-button');
          dialog.append(focusButton);
          tooltip.attachTrigger(focusButton, { trigger: { focus: {} } });
          tooltip.setReference(focusButton);
          const tooltipElement = element('div');
          tooltipElement.setAttribute('role', 'tooltip');
          tooltip.attachFloating(tooltipElement);

          focusButton.focus();
          expect(tooltip.open).toBe(true);

          userEscape();

          expect(tooltip.open).toBe(false);
          expect(popover.open).toBe(true);
        });
      });

      it('when true', async () => {
        await inBothModes((synchronous) => {
          const bubbles = { escapeKey: true, outsidePress: true };
          const { outer, inner, testButton } = renderNestedDialogs({ bubbles }, { bubbles }, synchronous);

          expect(outer.popup.open).toBe(true);
          expect(inner.popup.open).toBe(true);

          // The focus manager's initial focus: the inner dialog's first tabbable element.
          testButton.focus();
          userEscape();

          expect(outer.popup.open).toBe(false);
          expect(inner.popup.open).toBe(false);
        });
      });

      it('when false', async () => {
        await inBothModes((synchronous) => {
          const { outer, inner, testButton } = renderNestedDialogs(
            { bubbles: { escapeKey: false } },
            { bubbles: { escapeKey: false } },
            synchronous,
          );

          expect(outer.popup.open).toBe(true);
          expect(inner.popup.open).toBe(true);

          testButton.focus();
          userEscape();

          expect(outer.popup.open).toBe(true);
          expect(inner.popup.open).toBe(false);

          // The focus manager returns focus to the inner dialog's reference, inside the outer dialog.
          inner.reference.focus();
          userEscape();

          expect(outer.popup.open).toBe(false);
          expect(inner.popup.open).toBe(false);
        });
      });

      it('mixed', async () => {
        await inBothModes((synchronous) => {
          const { outer, inner, testButton } = renderNestedDialogs(
            { bubbles: { escapeKey: true } },
            { bubbles: { escapeKey: false } },
            synchronous,
          );

          expect(outer.popup.open).toBe(true);
          expect(inner.popup.open).toBe(true);

          testButton.focus();
          userEscape();

          expect(outer.popup.open).toBe(true);
          expect(inner.popup.open).toBe(false);

          inner.reference.focus();
          userEscape();

          expect(outer.popup.open).toBe(false);
          expect(inner.popup.open).toBe(false);
        });
      });
    });
  });

  // FLT §10 Q14: 1.9.0 has no `capture` option; the titles are kept verbatim and run under the default `bubbles`.
  describe('prop: capture', () => {
    describe('normalizeProp', () => {
      it('undefined', () => {
        const { escapeKey: escapeKeyCapture, outsidePress: outsidePressCapture } = normalizeDismissBubbles();

        expect(escapeKeyCapture).toBe(false);
        expect(outsidePressCapture).toBe(true);
      });

      it('{}', () => {
        const { escapeKey: escapeKeyCapture, outsidePress: outsidePressCapture } = normalizeDismissBubbles({});

        expect(escapeKeyCapture).toBe(false);
        expect(outsidePressCapture).toBe(true);
      });

      it('when true', () => {
        const { escapeKey: escapeKeyCapture, outsidePress: outsidePressCapture } = normalizeDismissBubbles(true);

        expect(escapeKeyCapture).toBe(true);
        expect(outsidePressCapture).toBe(true);
      });

      it('when false', () => {
        const { escapeKey: escapeKeyCapture, outsidePress: outsidePressCapture } = normalizeDismissBubbles(false);

        expect(escapeKeyCapture).toBe(false);
        expect(outsidePressCapture).toBe(false);
      });

      it('{ escapeKey: true }', () => {
        const { escapeKey: escapeKeyCapture, outsidePress: outsidePressCapture } = normalizeDismissBubbles({ escapeKey: true });

        expect(escapeKeyCapture).toBe(true);
        expect(outsidePressCapture).toBe(true);
      });

      it('{ outsidePress: false }', () => {
        const { escapeKey: escapeKeyCapture, outsidePress: outsidePressCapture } = normalizeDismissBubbles({ outsidePress: false });

        expect(escapeKeyCapture).toBe(false);
        expect(outsidePressCapture).toBe(false);
      });
    });

    // Overlay, and NestedDialog with FloatingPortal: the floating elements lie in document.body; the references render
    // where the dialogs do (the outer one in the overlay, the inner one in the outer floating element).
    function renderOverlay(synchronous = false) {
      const overlay = element('div');
      overlay.addEventListener('pointerdown', (event) => event.stopPropagation());
      overlay.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
          event.stopPropagation();
        }
      });
      const outside = document.createElement('span');
      outside.textContent = 'outside';
      overlay.append(outside);
      const portalDialog = (id: string, container: HTMLElement, parent?: PopupHarness) => {
        const popup = harness({ open: true, synchronous });
        popup.attachSession({ session: { dismiss: {}, parentToken: parent?.token ?? null } });
        const reference = document.createElement('button');
        container.append(reference);
        popup.attachTrigger(reference);
        popup.setReference(reference);
        const floating = element('div');
        floating.tabIndex = -1;
        const label = document.createElement('span');
        label.textContent = id;
        floating.append(label);
        popup.attachFloating(floating);
        return { popup, reference, floating, label };
      };
      const outer = portalDialog('outer', overlay);
      const inner = portalDialog('inner', outer.floating, outer.popup);
      return { outside, outer, inner };
    }

    describe('prop: capture.outsidePress', () => {
      it('when true', () => {
        const { outside, outer, inner } = renderOverlay();

        expect(outer.popup.open).toBe(true);
        expect(inner.popup.open).toBe(true);

        userClick(outer.label);

        expect(outer.popup.open).toBe(true);
        expect(inner.popup.open).toBe(false);

        userClick(outside);

        expect(outer.popup.open).toBe(false);
        expect(inner.popup.open).toBe(false);
      });
    });

    describe('prop: capture.escapeKey', () => {
      it('when false', async () => {
        await inBothModes((synchronous) => {
          const { outer, inner } = renderOverlay(synchronous);

          expect(outer.popup.open).toBe(true);
          expect(inner.popup.open).toBe(true);

          // The focus manager focuses the inner floating element, which has no tabbable content.
          inner.floating.focus();
          userEscape();

          expect(outer.popup.open).toBe(true);
          expect(inner.popup.open).toBe(false);

          // It returns focus to the inner dialog's reference, inside the outer floating element.
          inner.reference.focus();
          userEscape();

          expect(outer.popup.open).toBe(false);
          expect(inner.popup.open).toBe(false);
        });
      });
    });
  });

  describe('outsidePressEvent: intentional', () => {
    // AppWithPreventedPressStart: a scrubber inside the floating element prevents its pointerdown.
    function renderPreventedPressStart() {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { outsidePressEvent: intentional } } });
      const reference = element('button');
      popup.attachTrigger(reference);
      popup.setReference(reference);
      const tooltip = element('div');
      tooltip.setAttribute('role', 'tooltip');
      const scrubber = document.createElement('div');
      scrubber.setAttribute('data-testid', 'scrubber');
      scrubber.addEventListener('pointerdown', (event) => event.preventDefault());
      tooltip.append(scrubber);
      popup.attachFloating(tooltip);
      return { popup, scrubber };
    }

    it('dragging outside the floating element does not close', () => {
      const { popup, floating } = renderApp({ outsidePressEvent: intentional });
      mouse(floating, 'mousedown');
      mouse(document.body, 'mouseup');
      expect(popup.open).toBe(true);

      // Port: the drag suppresses exactly the next outside click; the one after it closes.
      mouse(document.body, 'click');
      expect(popup.open).toBe(true);
      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });

    it('dragging inside the floating element does not close', () => {
      const { popup, floating } = renderApp({ outsidePressEvent: intentional });
      mouse(document.body, 'mousedown');
      mouse(floating, 'mouseup');
      // The browser fires the gesture's click on the common ancestor of the mousedown and mouseup targets; the mouseup
      // inside the floating element marks the React tree so this click must not dismiss.
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // Port: a press observed while open, then its click, closes.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('dragging outside the floating element then clicking outside closes', () => {
      const { popup, floating } = renderApp({ outsidePressEvent: intentional });
      mouse(floating, 'mousedown');
      mouse(document.body, 'mouseup');
      // A click event will have fired before the proper outside click.
      mouse(document.body, 'click');
      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });

    it('dragging outside the floating element then clicking outside closes with mouse clicks', () => {
      const { popup, floating } = renderApp({ outsidePressEvent: intentional });
      pointer(floating, 'pointerdown', { pointerType: 'mouse' });
      mouse(floating, 'mousedown');
      mouse(document.body, 'mouseup');

      // Real mouse clicks carry `detail: 1`: this one passes the press-observed guard and is consumed by the one-shot
      // drag suppression.
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // The next press-backed mouse click closes.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('mouse click whose press started before open does not close', () => {
      const { popup } = renderApp({ outsidePressEvent: intentional });

      // The trailing click of a press that began before open.
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // A press observed while open still closes.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('compatibility events whose pointerdown opened the floating element do not count as a new press', () => {
      const popup = harness();
      popup.attachSession({ session: { dismiss: { outsidePressEvent: intentional } } });
      const openButton = element('button');
      openButton.textContent = 'Open';
      openButton.addEventListener('pointerdown', () => popup.setOpen(true));
      const tooltip = element('div');
      tooltip.setAttribute('role', 'tooltip');
      popup.attachFloating(tooltip);

      pointer(openButton, 'pointerdown', { pointerType: 'mouse' });
      expect(popup.open).toBe(true);

      // The pointerdown happened before the floating element opened; its compatibility events arrive after opening
      // but belong to the same press.
      mouse(openButton, 'mousedown');
      mouse(document.body, 'mouseup');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // A new pointer press that begins while open still dismisses.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('keyboard-generated outside click without a prior press closes', () => {
      const { popup } = renderApp({ outsidePressEvent: intentional });

      // Keyboard activations produce `detail: 0` clicks with no press.
      mouse(document.body, 'click', { detail: 0 });
      expect(popup.open).toBe(false);
    });

    it('press-less outside click reporting a pointer type closes', () => {
      const { popup } = renderApp({ outsidePressEvent: intentional });

      // Android assistive technology reports `pointerType: 'mouse'` with no press behind it.
      const click = new MouseEvent('click', { bubbles: true, detail: 0 });
      Object.defineProperty(click, 'pointerType', { value: 'mouse' });
      document.body.dispatchEvent(click);
      expect(popup.open).toBe(false);
    });

    it('press seen in a previous open session does not leak into a reopen', () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { outsidePressEvent: intentional } } });
      const reference = element('button');
      popup.attachTrigger(reference);
      popup.setReference(reference);
      reference.addEventListener('click', () => popup.setOpen(true, 'trigger-press', reference.id));
      const tooltip = element('div');
      tooltip.setAttribute('role', 'tooltip');
      popup.attachFloating(tooltip);

      // A genuine outside press closes and leaves a press on record.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);

      mouse(reference, 'click');
      expect(popup.open).toBe(true);

      // The reopened session must not inherit the previous session's press.
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // A press observed in the new session still closes.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('press seen before a same-batch close and reopen does not leak into the new session', () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { outsidePressEvent: intentional } } });
      const reference = element('button');
      popup.attachTrigger(reference);
      popup.setReference(reference);
      popup.attachFloating(element('div'));

      // A press lands while the first session is open.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');

      // Only `openchange` observes the session boundary.
      popup.setOpen(false, 'none');
      popup.setOpen(true, 'none');
      expect(popup.open).toBe(true);

      // The gesture's trailing click belongs to the previous session.
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // A press observed in the new session still closes.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('press survives a redundant open dispatch while already open', () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { outsidePressEvent: intentional } } });
      const reference = element('button');
      popup.attachTrigger(reference);
      popup.setReference(reference);
      popup.attachFloating(element('div'));

      // A genuine outside press lands while open.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');

      // A redundant open dispatch mid-gesture does not end the session.
      popup.setOpen(true, 'none');
      expect(popup.open).toBe(true);

      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('press survives listener re-attachment while open', () => {
      const { popup } = renderApp({ outsidePressEvent: intentional });

      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');

      // Changing an option mid-gesture re-attaches the document listeners; the observed press survives.
      popup.updateSession({ session: { dismiss: { outsidePressEvent: intentional, escapeKey: false } } });

      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('pointerdown-only press while open allows the outside click to close', () => {
      const { popup } = renderApp({ outsidePressEvent: intentional });

      // A pointerdown without a compatibility mousedown counts as an observed press on its own.
      pointer(document.body, 'pointerdown');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('non-primary-button press does not count as an outside press', () => {
      const { popup } = renderApp({ outsidePressEvent: intentional });

      // A right-button press produces `contextmenu`, not `click`.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse', button: 2 });
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // A primary-button press still closes.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('cancelled press does not count as an outside press', () => {
      const { popup } = renderApp({ outsidePressEvent: intentional });

      // A cancelled gesture produces no click.
      pointer(document.body, 'pointerdown', { pointerType: 'touch' });
      pointer(document.body, 'pointercancel');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);

      // A completed press still closes.
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(false);
    });

    it('inside click then programmatic outside click closes', () => {
      const { popup, input } = renderApp({ outsidePressEvent: intentional });

      mouse(input, 'mousedown');
      mouse(input, 'mouseup');
      mouse(input, 'click');
      expect(popup.open).toBe(true);

      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });

    it('inside click after drag does not cause immediate close on first outside click', () => {
      const { popup, floating, input } = renderApp({ outsidePressEvent: intentional });

      mouse(floating, 'mousedown');
      mouse(document.body, 'mouseup');

      // Inside clicks never dismiss, and do not consume the one-shot suppression of the drag that started inside.
      mouse(input, 'click');
      expect(popup.open).toBe(true);

      // The first true outside click after that drag is still ignored once.
      mouse(document.body, 'click');
      expect(popup.open).toBe(true);

      // The next outside click is a deliberate outside press and dismisses.
      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });

    it('drag ending on outsidePress-ignored target does not consume next outside click', () => {
      // `outsidePress={(event) => !event.target.closest('[data-testid="ignore"]')}`: with `ownBackdropIds`, a press on an
      // element that is neither an own backdrop nor an ancestor of the popup is ignored, one on the body is not.
      const { popup, floating } = renderApp({ outsidePressEvent: intentional, outsidePress: true, ownBackdropIds: ['backdrop'] });
      const ignored = document.createElement('div');
      ignored.setAttribute('data-testid', 'ignore');
      document.body.append(ignored);

      mouse(floating, 'mousedown');
      mouse(ignored, 'mouseup');

      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });

    it('press start prevented inside does not require double outside click', async () => {
      const { popup, scrubber } = renderPreventedPressStart();

      pointer(scrubber, 'pointerdown', { pointerType: 'mouse', button: 0 });
      mouse(scrubber, 'mousedown', { button: 0 });
      pointer(document.body, 'pointerup', { pointerType: 'mouse', button: 0 });
      mouse(document.body, 'mouseup', { button: 0 });

      // A tick with no immediate synthetic click: the next user click still dismisses.
      await vi.advanceTimersByTimeAsync(0);

      pointer(document.body, 'pointerdown', { pointerType: 'mouse', button: 0 });
      mouse(document.body, 'mousedown', { button: 0 });
      pointer(document.body, 'pointerup', { pointerType: 'mouse', button: 0 });
      mouse(document.body, 'mouseup', { button: 0 });
      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });

    it('press start prevented inside suppresses only immediate outside click', async () => {
      const { popup, scrubber } = renderPreventedPressStart();

      pointer(scrubber, 'pointerdown', { pointerType: 'mouse', button: 0 });
      mouse(scrubber, 'mousedown', { button: 0 });
      pointer(document.body, 'pointerup', { pointerType: 'mouse', button: 0 });
      mouse(document.body, 'mouseup', { button: 0 });

      mouse(document.body, 'click');
      expect(popup.open).toBe(true);

      await vi.advanceTimersByTimeAsync(0);

      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });

    it('pointercancel after prevented press start suppresses immediate outside click', async () => {
      const { popup, scrubber } = renderPreventedPressStart();

      pointer(scrubber, 'pointerdown', { pointerType: 'mouse', button: 0 });
      mouse(scrubber, 'mousedown', { button: 0 });
      pointer(document.body, 'pointercancel', { pointerType: 'mouse' });

      mouse(document.body, 'click');
      expect(popup.open).toBe(true);

      await vi.advanceTimersByTimeAsync(0);

      mouse(document.body, 'click');
      expect(popup.open).toBe(false);
    });
  });

  it('nested floating elements with different portal containers', async () => {
    // ButtonWithFloating: useClick is stood in by the toggle its click requests; FloatingPortal places the first
    // floating element in document.body and the second in the other container; the non-modal focus manager is not
    // part of the L3 title. The second popup renders inside the first one's floating element in the React tree.
    function buttonWithFloating(triggerText: string, container: HTMLElement, portalContainer: HTMLElement, parent?: PopupHarness) {
      const popup = harness();
      popup.attachSession({ session: { dismiss: {}, parentToken: parent?.token ?? null } });
      const trigger = document.createElement('button');
      trigger.textContent = triggerText;
      container.append(trigger);
      popup.attachTrigger(trigger);
      popup.setReference(trigger);
      trigger.addEventListener('click', () => popup.setOpen(!popup.open, 'trigger-press', trigger.id));
      const floating = document.createElement('div');
      portalContainer.append(floating);
      popup.attachFloating(floating);
      return { popup, trigger, floating };
    }

    const app = element('div');
    const otherContainer = document.createElement('div');
    const first = buttonWithFloating('open 1', app, document.body);
    const second = buttonWithFloating('open 2', first.floating, otherContainer, first.popup);
    const nested = document.createElement('button');
    nested.textContent = 'nested';
    second.floating.append(nested);
    app.append(otherContainer);

    userClick(first.trigger);
    expect(first.popup.open).toBe(true);

    userClick(second.trigger);
    await Promise.resolve();

    expect(first.popup.open).toBe(true);
    expect(second.popup.open).toBe(true);

    // Port: a press outside both closes both.
    pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
    expect(first.popup.open).toBe(false);
    expect(second.popup.open).toBe(false);
  });

  // -------------------------------------------------------------------------------------------------------------
  // Port titles: the `escape` point and its verdicts (FLT-KEY-01, FLT-EVT-05; ADR 04 Amendments 2026-10-27), one
  // close per keydown (FLT §10 Q11, POP §16 Q7), the disarm, Dialog's predicates, the forest's inside marks.
  // -------------------------------------------------------------------------------------------------------------

  describe('the escape point', () => {
    it('port: an accepted close is prevented and stopped, prevented only with allowPropagation(), stopped only when cancelled, and neither when cancelled with allowPropagation(); on Server every close takes the prevent-and-stop fallback and is reported', async () => {
      // [name, consumer, defaultPrevented, reached a later document listener] on WebAssembly.
      const consumers: [string, (details: { cancel(): void; allowPropagation(): void }) => void, boolean, boolean][] = [
        ['accepted', () => {}, true, false],
        ['accepted with allowPropagation()', allowPropagation, true, true],
        ['cancelled', cancel, false, false],
        ['cancelled with allowPropagation()', cancelAndAllowPropagation, false, true],
      ];
      await inBothModes((synchronous) => {
        for (const [name, consumer, prevented, propagated] of consumers) {
          const popup = harness({ open: true, synchronous, onOpenChange: (_open, details) => consumer(details) });
          popup.attachSession({ session: { dismiss: {} } });
          const floating = element('div');
          popup.attachFloating(floating);
          const reachedDocument = vi.fn();
          document.addEventListener('keydown', reachedDocument);

          const event = keyDown(floating);
          document.removeEventListener('keydown', reachedDocument);

          const accepted = name.startsWith('accepted');
          expect([name, event.defaultPrevented, reachedDocument.mock.calls.length > 0, popup.open]).toEqual(
            synchronous ? [name, prevented, propagated, !accepted] : [name, true, false, !accepted],
          );
          expect(popup.changes.map((change) => change.details.reason)).toEqual(['escape-key']);
          expect(popup.decisions.map((decision) => decision.point)).toEqual(synchronous ? ['escape'] : []);
          expect(popup.requests.map((request) => request.payload.reason)).toEqual(synchronous ? [] : ['escape-key']);
          popup.dispose();
        }
      });
    });

    it('port: bubbles.escapeKey drops the stop half of the verdict, so the keydown reaches the document whatever the consumer decides', async () => {
      const consumers: [string, (details: { cancel(): void }) => void, boolean][] = [
        ['accepted', () => {}, true],
        ['cancelled', cancel, false],
      ];
      await inBothModes((synchronous) => {
        for (const [name, consumer, prevented] of consumers) {
          const popup = harness({ open: true, synchronous, onOpenChange: (_open, details) => consumer(details) });
          popup.attachSession({ session: { dismiss: { bubbles: { escapeKey: true } } } });
          const floating = element('div');
          popup.attachFloating(floating);
          const reachedDocument = vi.fn();
          document.addEventListener('keydown', reachedDocument);

          const event = keyDown(floating);
          document.removeEventListener('keydown', reachedDocument);

          expect([name, event.defaultPrevented, reachedDocument.mock.calls.length]).toEqual([name, synchronous ? prevented : true, 1]);
          expect(popup.changes).toHaveLength(1);
          popup.dispose();
        }
      });
    });

    it('port: with nested sessions an Escape on the document is the innermost open popup\'s alone: the outer popup asks nothing, reports nothing and closes on the next Escape', async () => {
      await inBothModes((synchronous) => {
        const outer = harness({ open: true, synchronous });
        outer.attachSession({ session: { dismiss: {} } });
        outer.attachFloating(element('div'));
        const inner = harness({ open: true, synchronous });
        inner.attachSession({ session: { dismiss: {}, parentToken: outer.token } });
        inner.attachFloating(element('div'));

        const event = keyDown(document.body);

        expect(event.defaultPrevented).toBe(true);
        expect(inner.open).toBe(false);
        expect(outer.open).toBe(true);
        expect(inner.decisions.map((decision) => decision.point)).toEqual(synchronous ? ['escape'] : []);
        expect(inner.requests.map((request) => request.payload.reason)).toEqual(synchronous ? [] : ['escape-key']);
        expect(outer.decisions).toEqual([]);
        expect(outer.requests).toEqual([]);
        expect(outer.changes).toEqual([]);

        keyDown(document.body);
        expect(outer.open).toBe(false);
      });
    });

    it('port: one close per keydown: a script-dispatched Escape that the floating element lets propagate reaches the session\'s document listener, which asks nothing more', async () => {
      const consumers: [string, (details: { cancel(): void; allowPropagation(): void }) => void][] = [
        ['accepted with allowPropagation()', allowPropagation],
        ['cancelled with allowPropagation()', cancelAndAllowPropagation],
      ];
      await inBothModes((synchronous) => {
        for (const [name, consumer] of consumers) {
          // `commit: false` keeps the popup open through the dispatch, as a commit that has not arrived yet does.
          const popup = harness({ open: true, synchronous, commit: false, onOpenChange: (_open, details) => consumer(details) });
          popup.attachSession({ session: { dismiss: {} } });
          const floating = element('div');
          popup.attachFloating(floating);
          const reachedDocument = vi.fn();
          document.addEventListener('keydown', reachedDocument);

          keyDown(floating);
          document.removeEventListener('keydown', reachedDocument);

          // On Server the fallback stops the keydown; on WebAssembly the verdict lets it reach the document.
          expect([name, reachedDocument.mock.calls.length, popup.changes.length]).toEqual([name, synchronous ? 1 : 0, 1]);
          popup.dispose();
        }
      });
    });

    it('port: a session that requested a close asks nothing for a second script-dispatched Escape until the relay brings the next open', async () => {
      await inBothModes((synchronous) => {
        const options = { open: true, synchronous, commit: false };
        const popup = harness(options);
        popup.attachSession({ session: { dismiss: {} } });
        const floating = element('div');
        popup.attachFloating(floating);

        keyDown(floating);
        const second = keyDown(floating);

        expect(popup.changes).toHaveLength(1);
        expect(second.defaultPrevented).toBe(false);

        options.commit = true;
        popup.setOpen(true);
        keyDown(floating);

        expect(popup.open).toBe(false);
        expect(popup.changes.map((change) => [change.open, change.details.reason])).toEqual([
          [false, 'escape-key'],
          [true, 'none'],
          [false, 'escape-key'],
        ]);
      });
    });

    it('port: a cancelled Escape close leaves the session armed on WebAssembly, where the verdict reports the cancel, so the next Escape asks again; on Server the session stays disarmed until the next open', async () => {
      await inBothModes((synchronous) => {
        let cancelNext = true;
        const popup = harness({
          open: true,
          synchronous,
          onOpenChange: (_open, details) => {
            if (cancelNext) {
              details.cancel();
            }
          },
        });
        popup.attachSession({ session: { dismiss: {} } });
        const floating = element('div');
        popup.attachFloating(floating);

        keyDown(floating);
        expect(popup.open).toBe(true);

        cancelNext = false;
        keyDown(floating);

        expect(popup.open).toBe(!synchronous);
        expect(popup.changes).toHaveLength(synchronous ? 2 : 1);
      });
    });

    it('port: an Escape on an inactive trigger of an open popup, inside another open popup, closes that popup alone', async () => {
      // FLT §10 Q10, as the port observes it: the trigger's listener is on the focused element and runs first.
      await inBothModes((synchronous) => {
        const popover = harness({ open: true, synchronous });
        popover.attachSession({ session: { dismiss: {} } });
        const popoverPopup = element('div');
        popover.attachFloating(popoverPopup);
        const dialog = harness({ open: true, synchronous });
        dialog.attachSession({ session: { dismiss: {} } });
        const activeTrigger = element('button');
        dialog.attachTrigger(activeTrigger);
        dialog.setReference(activeTrigger);
        const inactiveTrigger = document.createElement('button');
        popoverPopup.append(inactiveTrigger);
        dialog.attachTrigger(inactiveTrigger);
        dialog.attachFloating(element('div'));

        inactiveTrigger.focus();
        userEscape();

        expect(dialog.open).toBe(false);
        expect(popover.open).toBe(true);
      });
    });

    it('port: with dismissal off nothing listens and the trigger and floating bags are empty; turning it on installs them', async () => {
      await inBothModes((synchronous) => {
        const popup = harness({ open: true, synchronous });
        popup.attachSession();
        const trigger = element('button');
        popup.attachTrigger(trigger, { trigger: { referencePress: true } });
        popup.setReference(trigger);
        const floating = element('div');
        popup.attachFloating(floating);

        keyDown(floating);
        keyDown(trigger);
        keyDown(document.body);
        userClick(document.body);
        pointer(trigger, 'pointerdown');

        expect(popup.open).toBe(true);
        expect(popup.changes).toEqual([]);
        expect(popup.decisions).toEqual([]);

        popup.updateSession({ session: { dismiss: {} } });
        keyDown(floating);

        expect(popup.open).toBe(false);
      });
    });
  });

  describe('the outside press', () => {
    it('port: an outside press that requested a close requests nothing for its compatibility mousedown, nor for another press, until the relay brings the next open', () => {
      const options = { open: true, commit: false };
      const popup = harness(options);
      popup.attachSession({ session: { dismiss: {} } });
      popup.attachFloating(element('div'));

      userClick(document.body);
      userClick(document.body);

      expect(popup.requests.map((request) => request.payload.reason)).toEqual(['outside-press']);

      options.commit = true;
      popup.setOpen(true);
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });

      expect(popup.open).toBe(false);
    });

    it('port: primaryButtonOnly: a non-primary outside press does not dismiss and a primary one does', () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { primaryButtonOnly: true } } });
      popup.attachFloating(element('div'));

      pointer(document.body, 'pointerdown', { pointerType: 'mouse', button: 2 });
      mouse(document.body, 'mousedown', { button: 2 });
      expect(popup.open).toBe(true);

      pointer(document.body, 'pointerdown', { pointerType: 'mouse', button: 0 });
      expect(popup.open).toBe(false);
    });

    it('port: singleTouch: a multi-touch outside press does not dismiss and a single-touch one does', () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { singleTouch: true } } });
      popup.attachFloating(element('div'));
      const first = { identifier: 1, target: document.body, clientX: 10, clientY: 10 };
      const second = { identifier: 2, target: document.body, clientX: 40, clientY: 10 };

      // Two fingers moving 12 px: a single finger would dismiss on that touchmove.
      touch(document.body, 'touchstart', [first, second]);
      touch(document.body, 'touchmove', [{ ...first, clientX: 22 }, second]);
      expect(popup.open).toBe(true);

      // A finger lifted after a 6 px move while another stays down.
      touch(document.body, 'touchstart', [first]);
      touch(document.body, 'touchmove', [{ ...first, clientX: 16 }]);
      touch(document.body, 'touchend', [second], [{ ...first, clientX: 16 }]);
      expect(popup.open).toBe(true);

      // One finger: a 6 px move, then its touchend, dismisses.
      touch(document.body, 'touchstart', [first]);
      touch(document.body, 'touchmove', [{ ...first, clientX: 16 }]);
      touch(document.body, 'touchend', [], [{ ...first, clientX: 16 }]);
      expect(popup.open).toBe(false);
    });

    it('port: ownBackdropIds: a press on an own backdrop, or on an ancestor of the popup other than its portal node, dismisses; a press on another outside element does not', () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { ownBackdropIds: ['backdrop'] } } });
      const backdrop = element('div', 'backdrop');
      const other = element('div');
      const portalNode = element('div');
      portalNode.setAttribute('data-base-ui-portal', '');
      const popupElement = document.createElement('div');
      portalNode.append(popupElement);
      popup.attachFloating(popupElement);

      pointer(other, 'pointerdown', { pointerType: 'mouse' });
      pointer(portalNode, 'pointerdown', { pointerType: 'mouse' });
      expect(popup.open).toBe(true);

      pointer(backdrop, 'pointerdown', { pointerType: 'mouse' });
      expect(popup.open).toBe(false);

      popup.setOpen(true);
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      expect(popup.open).toBe(false);
    });

    it('port: topmost: only the topmost open popup dismisses on an outside press', () => {
      const first = harness({ open: true });
      first.attachSession({ session: { dismiss: { topmost: true } } });
      first.attachFloating(element('div'));
      const second = harness();
      second.attachSession({ session: { dismiss: { topmost: true } } });
      second.attachFloating(element('div'));
      second.setOpen(true);

      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      expect(second.open).toBe(false);
      expect(first.open).toBe(true);

      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      expect(first.open).toBe(false);
    });

    it('port: a press inside a popup nested two levels down, each in its own portal and with no tree node, marks every enclosing popup inside', () => {
      const outer = harness({ open: true });
      outer.attachSession({ session: { dismiss: {} } });
      outer.attachFloating(element('div'));
      const middle = harness({ open: true });
      middle.attachSession({ session: { parentToken: outer.token } });
      middle.attachFloating(element('div'), { floating: { node: false } });
      const inner = harness({ open: true });
      inner.attachSession({ session: { parentToken: middle.token } });
      const innerFloating = element('div');
      inner.attachFloating(innerFloating, { floating: { node: false } });
      const button = document.createElement('button');
      innerFloating.append(button);

      pointer(button, 'pointerdown', { pointerType: 'mouse' });
      expect(outer.open).toBe(true);

      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      expect(outer.open).toBe(false);
    });

    it('port: the session publishes its bubbles flags into the shared data while open and dismissal is on', () => {
      const popup = harness();
      popup.attachSession({ session: { dismiss: { bubbles: { escapeKey: true, outsidePress: false } } } });
      const data = getSession(popup.token)!.dataRef.current;
      expect(data.__escapeKeyBubbles).toBeUndefined();

      popup.setOpen(true);

      expect(data.__escapeKeyBubbles).toBe(true);
      expect(data.__outsidePressBubbles).toBe(false);
    });
  });
});
// #endregion dismiss tests

// #region hover tests
// #endregion hover tests

// #region pointer tests (useClientPoint, the delay group, the hovered line)
// #endregion pointer tests
