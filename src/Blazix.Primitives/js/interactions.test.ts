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

  it('port: with nestedTooltipTriggers a focus inside an enabled nested tooltip trigger is left to it, and a focus on the trigger opens', () => {
    const popup = harness();
    popup.attachSession();
    const outer = element('div');
    outer.tabIndex = 0;
    outer.setAttribute('data-base-ui-tooltip-trigger', '');
    const inner = document.createElement('button');
    inner.setAttribute('data-base-ui-tooltip-trigger', '');
    outer.append(inner);
    popup.attachTrigger(outer, { trigger: { focus: {}, hover: { nestedTooltipTriggers: true } } });
    popup.setReference(outer);

    inner.focus();
    expect(popup.changes).toEqual([]);

    outer.focus();
    expect(popup.open).toBe(true);
  });

  it('port: a focus open of a delay group member while another member is open carries the instant phase', async () => {
    const token = 'focus-group';
    attach(null, JSON.stringify({ provider: { token, delayOpen: 600, delayClose: 0 } }), createFakeTwin().twin);
    const first = harness();
    first.attachSession();
    const firstTrigger = element('button');
    first.attachTrigger(firstTrigger, { trigger: { hover: { delayGroupToken: token } } });
    const second = harness();
    second.attachSession();
    const secondTrigger = element('button');
    second.attachTrigger(secondTrigger, { trigger: { focus: {}, hover: { delayGroupToken: token } } });

    first.setOpen(true, 'trigger-hover', firstTrigger.id, 'mouseenter');
    await Promise.resolve();
    secondTrigger.focus();

    expect(second.changes.at(-1)?.open).toBe(true);
    expect(second.changes.at(-1)?.details.instant).toBe(true);
  });
});

// #region click tests

// ---------------------------------------------------------------------------------------------------------------
// useClick (useClick.test.tsx; FLT-PTR-01..06, FLT-ALG-16, FLT-STA-01, FLT-STA-03, FLT-STA-04, FLT-API-06, FLT-EVT-04)
// and the decision point `press` (ADR 04 Amendments 2026-10-27). Every title runs in both modes: Server (the press is
// reported and committed by the harness as C# would, a round trip later) and WebAssembly (handed over, then `press`).
// ---------------------------------------------------------------------------------------------------------------

describe.each([false, true])('useClick (synchronous: %s)', (synchronous) => {
  let frames: FrameRequestCallback[] = [];

  beforeEach(() => {
    frames = [];
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
      frames.push(callback);
      return frames.length;
    });
  });

  function runFrames(): void {
    const pending = frames;
    frames = [];
    pending.forEach((callback) => callback(0));
  }

  function createMouseEvent(type: string, options: MouseEventInit = {}) {
    return new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, ...options });
  }

  function createPointerEvent(type: string, pointerType: string) {
    const event = createMouseEvent(type) as PointerEvent;
    Object.defineProperty(event, 'pointerType', { value: pointerType });
    return event;
  }

  function click(target: Element) {
    target.dispatchEvent(createMouseEvent('click'));
  }

  function pressMouse(target: Element) {
    target.dispatchEvent(createPointerEvent('pointerdown', 'mouse'));
    target.dispatchEvent(createMouseEvent('mousedown'));
    target.dispatchEvent(createMouseEvent('click'));
    runFrames();
  }

  function App(props: { initialOpen?: boolean; typeable?: boolean; click?: Record<string, unknown>; onTriggerClick?: () => boolean | void } = {}) {
    const popup = harness({ synchronous, open: props.initialOpen ?? false, onTriggerClick: props.onTriggerClick });
    popup.attachSession();
    const reference = props.typeable ? element('input') : element('button');
    popup.attachTrigger(reference, { trigger: { click: props.click ?? {} } });
    popup.setReference(reference);
    return { popup, reference };
  }

  it('opens and closes on repeated clicks', () => {
    const { popup, reference } = App();
    expect(popup.open).toBe(false);

    click(reference);
    expect(popup.open).toBe(true);

    click(reference);
    expect(popup.open).toBe(false);
  });

  it('keeps open on repeated clicks when toggle is false', () => {
    const { popup, reference } = App({ click: { toggle: false } });

    click(reference);
    click(reference);

    expect(popup.open).toBe(true);
  });

  it('opens from the mousedown event path', () => {
    const { popup, reference } = App({ click: { event: 'mousedown' } });

    pressMouse(reference);
    expect(popup.open).toBe(true);
  });

  it('closes from the mousedown event path', () => {
    const { popup, reference } = App({ click: { event: 'mousedown' }, initialOpen: true });

    pressMouse(reference);
    expect(popup.changes.at(-1)?.open).toBe(false);
    expect(popup.open).toBe(false);
  });

  it('ignores the click event after mousedown when event is mousedown-only', () => {
    const { popup, reference } = App({ click: { event: 'mousedown-only' } });

    pressMouse(reference);
    expect(popup.open).toBe(true);

    click(reference);
    expect(popup.open).toBe(true);
  });

  it('ignores mouse input when ignoreMouse is true', () => {
    const { popup, reference } = App({ click: { ignoreMouse: true } });

    reference.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'mouse' }));
    click(reference);
    expect(popup.open).toBe(false);

    // Port: a touch press still toggles.
    reference.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch' }));
    click(reference);
    expect(popup.open).toBe(true);
  });

  it('delays touch opening when touchOpenDelay is set', async () => {
    vi.useFakeTimers();
    const { popup, reference } = App({ click: { touchOpenDelay: 100 } });

    reference.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch' }));
    click(reference);

    expect(popup.open).toBe(false);

    await vi.advanceTimersByTimeAsync(100);

    expect(popup.open).toBe(true);
  });

  it('delays touch opening from the deferred mousedown event path', async () => {
    vi.useFakeTimers();
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
      frames.push(callback);
      return frames.length;
    });
    const { popup, reference } = App({ click: { event: 'mousedown', touchOpenDelay: 100 } });

    reference.dispatchEvent(createPointerEvent('pointerdown', 'touch'));
    reference.dispatchEvent(createMouseEvent('mousedown'));
    reference.dispatchEvent(createMouseEvent('click'));

    expect(popup.open).toBe(false);

    runFrames();

    expect(popup.open).toBe(false);

    await vi.advanceTimersByTimeAsync(100);

    expect(popup.open).toBe(true);
  });

  it('does not delay touch closing', () => {
    vi.useFakeTimers();
    const { popup, reference } = App({ initialOpen: true, click: { touchOpenDelay: 100 } });

    reference.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch' }));
    click(reference);

    expect(popup.changes.at(-1)?.open).toBe(false);
    expect(popup.open).toBe(false);
  });

  it('uses the configured reason', () => {
    const { popup, reference } = App({ click: { reason: 'input-press' }, typeable: true });

    click(reference);

    expect(popup.changes[0]?.open).toBe(true);
    expect(popup.changes[0]?.details.reason).toBe('input-press');
  });

  it('stickIfOpen true keeps a hover-opened popup open on first click', () => {
    const { popup, reference } = App({ click: { stickIfOpen: true } });

    // The hover open, committed by C# with its `mouseenter` (the hover region's request is proven in its own block).
    popup.setOpen(true, 'trigger-hover', reference.id, 'mouseenter');
    click(reference);

    expect(popup.open).toBe(true);
    expect(getSession(popup.token)?.dataRef.current.openEvent?.type).toBe('click');
  });

  it('stickIfOpen false closes a hover-opened popup on first click', () => {
    const { popup, reference } = App({ click: { stickIfOpen: false } });

    popup.setOpen(true, 'trigger-hover', reference.id, 'mouseenter');
    expect(popup.open).toBe(true);
    click(reference);

    expect(popup.open).toBe(false);
  });

  it('port: the press is handed over and asked through press inside the dispatch on WebAssembly, and reported with its interaction type on Server', () => {
    const { popup, reference } = App();

    reference.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'pen' }));
    reference.dispatchEvent(createMouseEvent('click', { detail: 1 }));
    click(reference);

    if (synchronous) {
      expect(popup.pressings).toEqual([
        { open: true, reason: 'trigger-press', triggerId: reference.id, interactionType: 'pen' },
        { open: false, reason: 'trigger-press', triggerId: reference.id, interactionType: 'keyboard' },
      ]);
      expect(popup.decisions.map((decision) => decision.point)).toEqual(['press', 'press']);
      expect(popup.requests.filter((request) => request.method === 'OnPress')).toEqual([]);
    } else {
      const presses = popup.requests.filter((request) => request.method === 'OnPress').map((request) => request.payload);
      expect(presses.map(({ open, interactionType, triggerId }) => ({ open, interactionType, triggerId }))).toEqual([
        { open: true, interactionType: 'pen', triggerId: reference.id },
        { open: false, interactionType: 'keyboard', triggerId: reference.id },
      ]);
      expect((presses[0]?.event as { type: string }).type).toBe('click');
      expect(popup.pressings).toEqual([]);
    }
    expect(popup.open).toBe(false);
  });

  it('port: a consumer veto of the press holds on both modes', () => {
    let veto = true;
    const { popup, reference } = App({ onTriggerClick: () => (veto ? false : undefined) });

    click(reference);
    expect(popup.open).toBe(false);
    expect(popup.changes).toEqual([]);

    veto = false;
    click(reference);
    expect(popup.open).toBe(true);
  });

  it('port: with patientClick a hover open sticks for 500 ms after its commit, then a press closes it', async () => {
    vi.useFakeTimers();
    const { popup, reference } = App({ click: { patientClick: true } });

    popup.setOpen(true, 'trigger-hover', reference.id, 'mouseenter');
    click(reference);
    expect(popup.open).toBe(true);

    popup.setOpen(false, 'trigger-hover', null, 'mouseleave');
    popup.setOpen(true, 'trigger-hover', reference.id, 'mouseenter');
    await vi.advanceTimersByTimeAsync(500);
    click(reference);
    expect(popup.open).toBe(false);
  });

  it('port: a press on another registered trigger of an open popup keeps it open and moves the anchor', () => {
    const { popup, reference } = App();
    const other = element('button');
    popup.attachTrigger(other, { trigger: { click: {} } });

    click(reference);
    click(other);

    expect(popup.open).toBe(true);
    expect(popup.activeTriggerId).toBe(other.id);
  });
});
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
        const { outer, inner } = renderNestedDialogs({ bubblesOutsidePress: false }, { bubblesOutsidePress: false });

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
        const { outer, inner } = renderNestedDialogs({ bubblesOutsidePress: true }, { bubblesOutsidePress: false });

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
          const bubbles = { bubblesEscapeKey: true, bubblesOutsidePress: true };
          const { outer, inner, testButton } = renderNestedDialogs(bubbles, bubbles, synchronous);

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
            { bubblesEscapeKey: false },
            { bubblesEscapeKey: false },
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
            { bubblesEscapeKey: true },
            { bubblesEscapeKey: false },
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
          popup.attachSession({ session: { dismiss: { bubblesEscapeKey: true } } });
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

    it('port: a cancelled Escape close leaves the session armed, by the verdict on WebAssembly and by C#\'s answer on Server, so the next Escape asks again', async () => {
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

        expect(popup.open).toBe(false);
        expect(popup.changes).toHaveLength(2);
      });
    });

    it('port: on Server a session that requested a close stays disarmed until C# answers, so a second Escape before the answer requests nothing', async () => {
      const popup = harness({ open: true, commit: false });
      popup.attachSession({ session: { dismiss: {} } });
      const floating = element('div');
      popup.attachFloating(floating);

      keyDown(floating);
      keyDown(floating);
      expect(popup.requests.filter((request) => request.method === 'OnRequest')).toHaveLength(1);

      // C# answers (a cancel): the commit-path update moves the answered counter, and the next Escape asks again.
      popup.updateSession({ session: { answered: 1 } });
      keyDown(floating);
      expect(popup.requests.filter((request) => request.method === 'OnRequest')).toHaveLength(2);
    });

    it('port: a close and a reopen committed in one batch are replayed, the close with its reason, so a press seen before it does not leak into the reopened session', async () => {
      const popup = harness({ open: true });
      popup.attachSession({ session: { dismiss: { outsidePressEvent: intentional } } });
      popup.attachFloating(element('div'));
      const session = getSession(popup.token)!;
      const seen: [boolean, string][] = [];
      session.events.on('openchange', (details: SessionOpenChangeDetails) => seen.push([details.open, details.reason]));

      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      mouse(document.body, 'mousedown');
      popup.coalesce(() => {
        popup.setOpen(false, 'focus-out');
        popup.setOpen(true, 'trigger-hover', null, 'mouseenter');
      });

      expect(seen).toEqual([
        [false, 'focus-out'],
        [true, 'trigger-hover'],
      ]);
      expect(session.dataRef.current.openEvent?.type).toBe('mouseenter');
      mouse(document.body, 'click', { detail: 1 });
      expect(popup.open).toBe(true);
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
      popup.attachSession({ session: { dismiss: { bubblesEscapeKey: true, bubblesOutsidePress: false } } });
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
// ---------------------------------------------------------------------------------------------------------------
// useHoverReferenceInteraction.test.tsx, useHoverFloatingInteraction.test.tsx and safePolygon.test.ts (FLT-PTR-11..25,
// FLT-API-03/04, FLT-STA-05/06, FLT-STR-08, FLT-DOM-19, FLT-EVT-02/03, FLT-EDGE-10/13, FLT-ALG-15/24). React's prop-bag
// handlers on the trigger and the hook's native listeners are both listeners on the trigger handle's element. A delegated
// wrapper is a trigger handle on the wrapper that takes the button's id as its trigger id, so the button's handle,
// attached after it, replaces it in the trigger map (Base UI never registers the wrapper). The floating element is
// rendered throughout and `harness.open` stands in for `getByRole('tooltip')`. A title whose Base UI assertions are all
// absences gains a port contrast that the behaviour must produce.
// ---------------------------------------------------------------------------------------------------------------

// The statically imported module instance, read once at collection (before any test resets the module registry).
const { safePolygon } = await import('./interactions');
type HoverTestOptions = import('./interactions').HoverOptions;
type HoverTestTree = import('./interactions').SessionTree;
type HoverTestCloseContext = import('./interactions').HandleCloseContext;

function hoverMouse(target: EventTarget, type: string, init: MouseEventInit = {}): MouseEvent {
  const bubbles = type !== 'mouseenter' && type !== 'mouseleave';
  const event = new MouseEvent(type, { bubbles, cancelable: bubbles, composed: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function hoverPointer(target: EventTarget, type: string, pointerType: string): PointerEvent {
  const bubbles = type !== 'pointerenter' && type !== 'pointerleave';
  const event = new PointerEvent(type, { bubbles, cancelable: bubbles, composed: true, pointerType });
  target.dispatchEvent(event);
  return event;
}

function hoverRect(left: number, top: number, width: number, height: number): DOMRect {
  return {
    x: left,
    y: top,
    left,
    top,
    width,
    height,
    right: left + width,
    bottom: top + height,
    toJSON() {
      return this;
    },
  } as DOMRect;
}

function hoverSharedState(token: string): { handleCloseOptions?: { blockPointerEvents?: boolean } } | undefined {
  return getSession(token)?.dataRef.current.hoverInteractionState as { handleCloseOptions?: { blockPointerEvents?: boolean } } | undefined;
}

// A delegated wrapper around a button: the hover handle on the wrapper gives way to the button's own handle in the map.
function hoverWrapper(popup: PopupHarness, hover: HoverTestOptions) {
  const wrapper = element('div', `${popup.token}-wrapper`);
  const trigger = document.createElement('button');
  trigger.id = `${popup.token}-trigger`;
  wrapper.append(trigger);
  popup.attachTrigger(wrapper, { trigger: { triggerId: trigger.id, hover } });
  popup.attachTrigger(trigger);
  popup.setReference(trigger);
  popup.attachFloating(element('div'), { floating: { hover: null } });
  return { wrapper, trigger };
}

describe('useHoverReferenceInteraction', () => {
  afterEach(() => {
    document.body.style.removeProperty('pointer-events');
  });

  it('updates the handleClose options during render', () => {
    const popup = harness();
    popup.attachSession();
    const trigger = popup.attachTrigger(element('button'), { trigger: { hover: { safePolygon: true, blockPointerEvents: false } } });
    expect(hoverSharedState(popup.token)?.handleCloseOptions?.blockPointerEvents).toBe(false);

    trigger.update(JSON.stringify({ sessionToken: popup.token, trigger: { hover: { safePolygon: true, blockPointerEvents: true } } }));
    expect(hoverSharedState(popup.token)?.handleCloseOptions?.blockPointerEvents).toBe(true);
  });

  it('does not treat child target as inactive when handlers are on a wrapper', async () => {
    const popup = harness({ open: true });
    popup.attachSession();
    const { wrapper, trigger } = hoverWrapper(popup, { mouseOnly: true, restMs: 100, delayClose: 0, move: false });

    hoverPointer(wrapper, 'pointerenter', 'mouse');
    hoverMouse(wrapper, 'mouseenter');
    hoverMouse(trigger, 'mousemove', { movementX: 10, movementY: 0 });

    await Promise.resolve();

    // Moving over the active trigger should not emit a redundant openchange.
    expect(popup.changes).toHaveLength(0);
    expect(popup.open).toBe(true);

    // Port: the contrast, a move over another registered trigger inside the wrapper is over an inactive trigger.
    const other = document.createElement('button');
    wrapper.append(other);
    popup.attachTrigger(other);
    hoverMouse(other, 'mousemove', { movementX: 10, movementY: 0 });
    expect(popup.changes).toHaveLength(1);
    expect(popup.changes[0]?.details.reason).toBe('trigger-hover');
  });

  it('does not treat a synthetic child target as inactive when the native path differs', async () => {
    const popup = harness({ open: true });
    popup.attachSession();
    const wrapper = element('div', 'wrapper');
    const trigger = document.createElement('button');
    trigger.id = 'trigger';
    const child = document.createElement('span');
    trigger.append(child);
    wrapper.append(trigger);
    const otherTrigger = element('button', 'other-trigger');
    popup.attachTrigger(wrapper, { trigger: { triggerId: 'trigger', hover: { mouseOnly: true, restMs: 100, delayClose: 0, move: false } } });
    popup.attachTrigger(trigger);
    popup.attachTrigger(otherTrigger);
    popup.setReference(trigger);
    popup.attachFloating(element('div'), { floating: { hover: null } });

    hoverPointer(wrapper, 'pointerenter', 'mouse');
    // Entering the active trigger clears `blockMouseMove`, so a misclassified move would reopen. Port: the hook's
    // `mouseenter` listener is on the wrapper, the handle's element.
    hoverMouse(wrapper, 'mouseenter');

    const event = new MouseEvent('mousemove', { bubbles: true });
    Object.defineProperties(event, {
      // Deliberately skew the native path so `getTarget(nativeEvent)` resolves to another
      // registered trigger while the event's `target` remains `child`.
      composedPath: {
        configurable: true,
        value: () => [otherTrigger, wrapper],
      },
      movementX: {
        configurable: true,
        value: 10,
      },
      movementY: {
        configurable: true,
        value: 0,
      },
    });

    child.dispatchEvent(event);

    await Promise.resolve();

    expect(popup.changes).toHaveLength(0);
    expect(popup.open).toBe(true);

    // Port: the contrast, a move whose target is inside another registered trigger within the wrapper is inactive.
    wrapper.append(otherTrigger);
    hoverMouse(otherTrigger, 'mousemove', { movementX: 10, movementY: 0 });
    expect(popup.changes).toHaveLength(1);
  });

  it('treats disabled child trigger as inactive in wrapper fallback mode', async () => {
    const popup = harness({ open: true });
    popup.attachSession();
    const activeTrigger = element('button', 'active-trigger');
    const wrapper = element('div', 'inactive-wrapper');
    const disabledTrigger = document.createElement('button');
    disabledTrigger.id = 'disabled-trigger';
    disabledTrigger.setAttribute('data-trigger-disabled', '');
    wrapper.append(disabledTrigger);
    const hover: HoverTestOptions = { mouseOnly: true, restMs: 100, delayClose: 0, move: false };
    // Port: Base UI's hook listens on the active trigger and spreads its prop bag on the wrapper; each is a handle here.
    popup.attachTrigger(activeTrigger, { trigger: { hover } });
    popup.attachTrigger(wrapper, { trigger: { triggerId: 'disabled-trigger', hover } });
    popup.attachTrigger(disabledTrigger);
    popup.setReference(activeTrigger);
    popup.attachFloating(element('div'), { floating: { hover: null } });

    hoverPointer(activeTrigger, 'pointerenter', 'mouse');
    hoverMouse(activeTrigger, 'mouseenter');
    hoverPointer(wrapper, 'pointerenter', 'mouse');
    hoverMouse(disabledTrigger, 'mousemove', { movementX: 10, movementY: 0 });

    await Promise.resolve();

    expect(popup.changes).toHaveLength(1);
    expect(popup.open).toBe(true);
  });

  it('reopens immediately for same trigger in delegated wrapper mode during close transition', async () => {
    const popup = harness({ open: true });
    popup.attachSession();
    const { wrapper } = hoverWrapper(popup, { mouseOnly: true, move: false, delayOpen: 500, delayClose: 0 });
    await Promise.resolve();

    // Port: C# commits the hover close, then renders the closing transition (`transitionStatus === 'ending'`).
    popup.setOpen(false, 'trigger-hover');
    popup.updateSession({ session: { ending: true } });

    await Promise.resolve();
    expect(popup.open).toBe(false);

    hoverPointer(wrapper, 'pointerenter', 'mouse');
    hoverMouse(wrapper, 'mouseenter');

    await Promise.resolve();

    // Close from hover + immediate reopen without waiting open delay.
    expect(popup.changes).toHaveLength(2);
    expect(popup.changes[1]?.open).toBe(true);
    expect(popup.open).toBe(true);
  });

  it('reopens immediately when close transition state is provided externally', async () => {
    // Port: `isClosing` has no counterpart; the closing transition is C#'s `ending` either way, sent here before the
    // hover close is relayed.
    const popup = harness({ open: true });
    popup.attachSession();
    const { wrapper } = hoverWrapper(popup, { mouseOnly: true, move: false, delayOpen: 500, delayClose: 0 });
    await Promise.resolve();

    popup.updateSession({ session: { ending: true } });
    popup.setOpen(false, 'trigger-hover');

    await Promise.resolve();
    expect(popup.open).toBe(false);

    hoverPointer(wrapper, 'pointerenter', 'mouse');
    hoverMouse(wrapper, 'mouseenter');

    await Promise.resolve();

    expect(popup.changes).toHaveLength(2);
    expect(popup.changes[1]?.open).toBe(true);
    expect(popup.open).toBe(true);
  });

  describe('hover semantics', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    function render(hover: HoverTestOptions = {}) {
      const popup = harness();
      popup.attachSession();
      const button = element('button');
      popup.attachTrigger(button, { trigger: { hover } });
      popup.setReference(button);
      const tooltip = element('div');
      tooltip.setAttribute('role', 'tooltip');
      popup.attachFloating(tooltip, { floating: { hover: {} } });
      return { popup, button, tooltip };
    }

    it('opens on mouseenter', async () => {
      const { popup, button } = render();

      hoverMouse(button, 'mouseenter');

      expect(popup.open).toBe(true);
      await vi.advanceTimersByTimeAsync(0);
    });

    it('closes on mouseleave', () => {
      const { popup, button } = render();

      hoverMouse(button, 'mouseenter');
      expect(popup.open).toBe(true);
      hoverMouse(button, 'mouseleave');

      expect(popup.open).toBe(false);
    });

    it('closes when the pointer moves onto the floating element without safePolygon', async () => {
      const { popup, button, tooltip } = render();

      hoverMouse(button, 'mouseenter');
      await vi.advanceTimersByTimeAsync(0);
      expect(popup.open).toBe(true);

      hoverMouse(button, 'mouseleave', { relatedTarget: tooltip });

      expect(popup.open).toBe(false);
    });

    describe('prop: delay', () => {
      it('symmetric number', async () => {
        const { popup, button } = render({ delayOpen: 1000, delayClose: 1000 });

        hoverMouse(button, 'mouseenter');

        await vi.advanceTimersByTimeAsync(999);

        expect(popup.open).toBe(false);

        await vi.advanceTimersByTimeAsync(1);

        expect(popup.open).toBe(true);
      });

      it('open', async () => {
        const { popup, button } = render({ delayOpen: 500 });

        hoverMouse(button, 'mouseenter');

        await vi.advanceTimersByTimeAsync(499);

        expect(popup.open).toBe(false);

        await vi.advanceTimersByTimeAsync(1);

        expect(popup.open).toBe(true);
      });

      it('close', async () => {
        const { popup, button } = render({ delayClose: 500 });

        hoverMouse(button, 'mouseenter');
        hoverMouse(button, 'mouseleave');

        await vi.advanceTimersByTimeAsync(499);

        expect(popup.open).toBe(true);

        await vi.advanceTimersByTimeAsync(1);

        expect(popup.open).toBe(false);
      });

      it('open with close 0', async () => {
        const { popup, button } = render({ delayOpen: 500 });

        hoverMouse(button, 'mouseenter');

        await vi.advanceTimersByTimeAsync(499);

        hoverMouse(button, 'mouseleave');

        await vi.advanceTimersByTimeAsync(1);

        expect(popup.open).toBe(false);

        // Port: the contrast, without the leave the open timer fires.
        hoverMouse(button, 'mouseenter');
        await vi.advanceTimersByTimeAsync(500);
        expect(popup.open).toBe(true);
      });

      it('restMs + nullish open delay should respect restMs', async () => {
        const { popup, button } = render({ restMs: 100, delayClose: 100 });

        hoverMouse(button, 'mouseenter');

        await vi.advanceTimersByTimeAsync(99);

        expect(popup.open).toBe(false);

        // Port: the contrast, a move starts the rest timer, which opens after `restMs`.
        hoverMouse(button, 'mousemove', { movementX: 10, movementY: 10 });
        await vi.advanceTimersByTimeAsync(100);
        expect(popup.open).toBe(true);
      });
    });

    it('restMs', async () => {
      const { popup, button } = render({ restMs: 100 });
      const move = { movementX: 10, movementY: 10 };

      hoverMouse(button, 'mousemove', move);

      await vi.advanceTimersByTimeAsync(99);

      hoverMouse(button, 'mousemove', move);

      await vi.advanceTimersByTimeAsync(1);

      expect(popup.open).toBe(false);

      hoverMouse(button, 'mousemove', move);

      await vi.advanceTimersByTimeAsync(100);

      expect(popup.open).toBe(true);
    });

    it('restMs does not reset timer for minor mouse movement', async () => {
      const { popup, button } = render({ restMs: 100 });
      const move = { movementX: 1, movementY: 0 };

      hoverMouse(button, 'mousemove', move);

      await vi.advanceTimersByTimeAsync(99);

      hoverMouse(button, 'mousemove', move);

      await vi.advanceTimersByTimeAsync(1);

      expect(popup.open).toBe(true);
    });

    it('reports the hover reason', async () => {
      const popup = harness();
      popup.attachSession();
      const button = element('button');
      popup.attachTrigger(button, { trigger: { hover: {} } });
      popup.setReference(button);
      popup.attachFloating(element('div'), { floating: { hover: null } });

      hoverMouse(button, 'mouseenter');
      await vi.advanceTimersByTimeAsync(0);
      hoverMouse(button, 'mouseleave');

      expect(popup.changes.map((change) => change.details.reason)).toEqual(['trigger-hover', 'trigger-hover']);
    });

    it('port: a trigger mouseleave while the popup is closed cancels the pending open and requests no close', async () => {
      const { popup, button } = render({ delayOpen: 500 });

      hoverMouse(button, 'mouseenter');
      await vi.advanceTimersByTimeAsync(499);
      hoverMouse(button, 'mouseleave');
      await vi.advanceTimersByTimeAsync(1);

      expect(popup.open).toBe(false);
      expect(popup.requests).toEqual([]);

      // The same leave once the popup is open requests the hover close.
      hoverMouse(button, 'mouseenter');
      await vi.advanceTimersByTimeAsync(500);
      hoverMouse(button, 'mouseleave');
      expect(popup.requests.map((request) => [request.payload.open, request.payload.reason])).toEqual([
        [true, 'trigger-hover'],
        [false, 'trigger-hover'],
      ]);
    });

    it('port: a hover close emits floating.closed on the tree with its mouse event after the close request', async () => {
      const { popup, button } = render({ delayClose: 100 });
      const seen: { event: unknown; requests: number; open: boolean }[] = [];
      getSession(popup.token)!.tree.events.on('floating.closed', (event: unknown) => {
        seen.push({ event, requests: popup.requests.length, open: popup.open });
      });

      hoverMouse(button, 'mouseenter');
      const leave = hoverMouse(button, 'mouseleave');
      expect(seen).toEqual([]);

      await vi.advanceTimersByTimeAsync(100);
      expect(seen).toEqual([{ event: leave, requests: 2, open: false }]);
    });

    it('port: with mouseOnly, a touch pointer recorded on the trigger blocks the hover open until a close resets it', () => {
      const { popup, button } = render({ mouseOnly: true });

      hoverPointer(button, 'pointerenter', 'touch');
      hoverMouse(button, 'mouseenter');
      expect(popup.open).toBe(false);

      hoverPointer(button, 'pointerdown', 'mouse');
      hoverMouse(button, 'mouseenter');
      expect(popup.open).toBe(true);

      // A touch pointer recorded while open is reset to `undefined` (mouse-like) when the popup closes.
      hoverPointer(button, 'pointerdown', 'touch');
      popup.setOpen(false);
      hoverMouse(button, 'mouseenter');
      expect(popup.open).toBe(true);
    });

    it('port: with guardStaleOpen, a mouseout leaving the trigger cancels the pending open, and one within its subtree does not', async () => {
      const { popup, button } = render({ delayOpen: 100, guardStaleOpen: true });
      const icon = document.createElement('span');
      button.append(icon);

      hoverMouse(button, 'mouseenter');
      hoverMouse(button, 'mouseout', { relatedTarget: icon });
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(true);

      popup.setOpen(false);
      hoverMouse(button, 'mouseenter');
      hoverMouse(button, 'mouseout', { relatedTarget: document.body });
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(false);
    });

    it('port: with move (the default), the first mousemove over the trigger opens it like a mouseenter; without move it does not', () => {
      const moving = render();
      hoverMouse(moving.button, 'mousemove');
      expect(moving.popup.open).toBe(true);
      expect(moving.popup.changes[0]?.details.event?.type).toBe('mousemove');

      const still = render({ move: false });
      hoverMouse(still.button, 'mousemove');
      expect(still.popup.open).toBe(false);
    });

    it('port: with safePolygon, leaving the trigger toward the popup keeps it open and moving away closes it', () => {
      const { popup, button, tooltip } = render({ safePolygon: true });
      button.getBoundingClientRect = () => hoverRect(0, 0, 100, 100);
      tooltip.getBoundingClientRect = () => hoverRect(0, 120, 100, 100);
      getSession(popup.token)!.setPlacement('bottom');

      hoverMouse(button, 'mouseenter');
      expect(popup.open).toBe(true);

      hoverMouse(button, 'mouseleave', { clientX: 50, clientY: 100 });
      hoverMouse(document.body, 'mousemove', { clientX: 50, clientY: 110 });
      expect(popup.open).toBe(true);

      hoverMouse(document.body, 'mousemove', { clientX: 300, clientY: 50 });
      expect(popup.open).toBe(false);
      expect(popup.changes.at(-1)?.details.reason).toBe('trigger-hover');
    });

    it('port: a hover open with blockPointerEvents blocks pointer events outside the trigger and the floating element until the close', () => {
      const { popup, button, tooltip } = render({ safePolygon: true, blockPointerEvents: true });

      hoverMouse(button, 'mouseenter');
      expect(popup.open).toBe(true);
      expect([document.body.style.pointerEvents, button.style.pointerEvents, tooltip.style.pointerEvents]).toEqual(['none', 'auto', 'auto']);

      popup.setOpen(false);
      expect([document.body.style.pointerEvents, button.style.pointerEvents, tooltip.style.pointerEvents]).toEqual(['', '', '']);
    });

    it('port: a trigger that attaches after the hover open was relayed gets the blockPointerEvents mutation once it is the anchor', () => {
      const popup = harness();
      popup.attachSession();
      const tooltip = element('div');
      popup.attachFloating(tooltip, { floating: { hover: {} } });
      // A detached trigger whose open C# committed before its handle attached (on Server, a round trip earlier).
      popup.setOpen(true, 'trigger-hover', 'late', 'mouseenter');
      expect(document.body.style.pointerEvents).toBe('');

      const button = element('button', 'late');
      popup.attachTrigger(button, { trigger: { hover: { safePolygon: true, blockPointerEvents: true } } });

      expect([document.body.style.pointerEvents, button.style.pointerEvents, tooltip.style.pointerEvents]).toEqual(['none', 'auto', 'auto']);
    });

    it("port: a newer owner of the scope element takes the mutation over, and the older owner's clear leaves it intact", () => {
      const first = render({ safePolygon: true, blockPointerEvents: true });
      const second = render({ safePolygon: true, blockPointerEvents: true });

      hoverMouse(first.button, 'mouseenter');
      hoverMouse(second.button, 'mouseenter');
      expect([first.button.style.pointerEvents, first.tooltip.style.pointerEvents]).toEqual(['', '']);
      expect([document.body.style.pointerEvents, second.button.style.pointerEvents, second.tooltip.style.pointerEvents]).toEqual(['none', 'auto', 'auto']);

      first.popup.setOpen(false);
      expect([document.body.style.pointerEvents, second.button.style.pointerEvents, second.tooltip.style.pointerEvents]).toEqual(['none', 'auto', 'auto']);

      second.popup.setOpen(false);
      expect(document.body.style.pointerEvents).toBe('');
    });

    it('port: a mousemove over an inactive trigger of an open popup with blockPointerEvents moves the mutation to that trigger', () => {
      const popup = harness();
      popup.attachSession();
      const hover: HoverTestOptions = { safePolygon: true, blockPointerEvents: true, move: false };
      const first = element('button');
      const second = element('button');
      popup.attachTrigger(first, { trigger: { hover } });
      popup.attachTrigger(second, { trigger: { hover } });
      const tooltip = element('div');
      popup.attachFloating(tooltip, { floating: { hover: {} } });

      // Not a hover open, so the floating element applies no mutation of its own.
      popup.setOpen(true, 'none', first.id);
      expect(document.body.style.pointerEvents).toBe('');

      hoverMouse(second, 'mousemove');
      expect([document.body.style.pointerEvents, second.style.pointerEvents, tooltip.style.pointerEvents, first.style.pointerEvents]).toEqual([
        'none',
        'auto',
        'auto',
        '',
      ]);
    });

    it('port: a pointerdown on an interactive element with aria-haspopup inside the floating element makes the open click-like, so leaving the trigger keeps it open', () => {
      const { popup, button, tooltip } = render();
      const menuTrigger = document.createElement('button');
      menuTrigger.setAttribute('aria-haspopup', 'menu');
      const text = document.createElement('p');
      tooltip.append(menuTrigger, text);

      hoverMouse(button, 'mouseenter');
      hoverPointer(menuTrigger, 'pointerdown', 'mouse');
      hoverMouse(button, 'mouseleave');
      expect(popup.open).toBe(true);

      hoverPointer(text, 'pointerdown', 'mouse');
      hoverMouse(button, 'mouseleave');
      expect(popup.open).toBe(false);
    });

    it('port: with nestedTooltipTriggers, a mouseover on a nested tooltip trigger cancels the pending open and vetoes hover opens until the pointer is back on the outer trigger', async () => {
      const { popup, outer, label, nested } = nestedTriggers({ restMs: 100 });

      hoverPointer(outer, 'pointerenter', 'mouse');
      hoverMouse(outer, 'mouseenter');
      hoverMouse(label, 'mousemove', { movementX: 10 });
      await vi.advanceTimersByTimeAsync(50);
      hoverMouse(nested, 'mouseover');
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(false);

      hoverMouse(nested, 'mousemove', { movementX: 10 });
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(false);

      hoverMouse(label, 'mouseover');
      await vi.advanceTimersByTimeAsync(99);
      expect(popup.open).toBe(false);
      await vi.advanceTimersByTimeAsync(1);
      expect(popup.open).toBe(true);
      expect(popup.changes[0]?.details.triggerId).toBe(outer.id);
    });

    it('port: a mouseover on a nested tooltip trigger closes a popup last opened by hover, and leaves one opened otherwise', () => {
      const { popup, outer, label, nested } = nestedTriggers({});

      hoverMouse(outer, 'mouseenter');
      expect(popup.open).toBe(true);
      hoverMouse(nested, 'mouseover');
      expect(popup.open).toBe(false);
      expect(popup.changes.at(-1)?.details.reason).toBe('trigger-hover');

      // Back on the outer trigger with no open delay, it reopens at once.
      hoverMouse(label, 'mouseover');
      expect(popup.open).toBe(true);

      popup.setOpen(false);
      popup.setOpen(true, 'trigger-focus', outer.id);
      hoverMouse(nested, 'mouseover');
      expect(popup.open).toBe(true);
    });

    it('port: a mouseleave of the outer trigger cancels the scheduled reopen and clears the nested state', async () => {
      const { popup, outer, label, nested } = nestedTriggers({ restMs: 100 });

      hoverMouse(outer, 'mouseenter');
      hoverMouse(nested, 'mouseover');
      hoverMouse(label, 'mouseover');
      hoverMouse(outer, 'mouseleave');
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(false);

      // Leaving straight from the nested trigger lifts the veto: the next rest on the outer trigger opens it.
      hoverMouse(outer, 'mouseenter');
      hoverMouse(nested, 'mouseover');
      hoverMouse(outer, 'mouseleave');
      hoverMouse(outer, 'mouseenter');
      hoverMouse(label, 'mousemove', { movementX: 10 });
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(true);
    });

    it("port: with cancelPendingOpenOnPress, a pointerdown or click on a closed popup's trigger emits a trigger-press openchange on the bus alone, which cancels the pending hover open", async () => {
      const popup = harness();
      popup.attachSession();
      const button = element('button');
      popup.attachTrigger(button, { trigger: { cancelPendingOpenOnPress: true, hover: { delayOpen: 100 } } });
      popup.setReference(button);
      popup.attachFloating(element('div'), { floating: { hover: {} } });
      const seen: SessionOpenChangeDetails[] = [];
      getSession(popup.token)!.events.on('openchange', (details: SessionOpenChangeDetails) => seen.push(details));

      hoverMouse(button, 'mouseenter');
      const down = hoverPointer(button, 'pointerdown', 'mouse');
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(false);
      expect(seen).toEqual([{ open: false, reason: 'trigger-press', nativeEvent: down, nested: false, triggerElement: undefined }]);

      hoverMouse(button, 'mouseenter');
      const click = hoverMouse(button, 'click');
      await vi.advanceTimersByTimeAsync(100);
      expect(popup.open).toBe(false);
      expect(seen.at(-1)?.nativeEvent).toBe(click);
      expect(popup.requests).toEqual([]);

      // While open, a press emits nothing.
      popup.setOpen(true, 'trigger-hover', button.id);
      hoverPointer(button, 'pointerdown', 'mouse');
      expect(seen.map((details) => details.reason)).toEqual(['trigger-press', 'trigger-press', 'trigger-hover']);
    });

    it('port: a trigger of a delay group without a live provider opens after its own delays and requests no instant phase', async () => {
      const { popup, button } = render({ delayOpen: 100, delayClose: 50, delayGroupToken: 'no-provider' });

      hoverMouse(button, 'mouseenter');
      await vi.advanceTimersByTimeAsync(99);
      expect(popup.open).toBe(false);
      await vi.advanceTimersByTimeAsync(1);
      expect(popup.open).toBe(true);
      expect(popup.requests[0]?.payload).not.toHaveProperty('instant');

      hoverMouse(button, 'mouseleave');
      await vi.advanceTimersByTimeAsync(49);
      expect(popup.open).toBe(true);
      await vi.advanceTimersByTimeAsync(1);
      expect(popup.open).toBe(false);
    });

    // Tooltip's nested triggers: an outer trigger (with the marker C# renders) holding a label and a nested trigger.
    function nestedTriggers(hover: HoverTestOptions) {
      const popup = harness();
      popup.attachSession();
      const outer = element('div');
      outer.setAttribute('data-base-ui-tooltip-trigger', '');
      const label = document.createElement('span');
      const nested = document.createElement('span');
      nested.setAttribute('data-base-ui-tooltip-trigger', '');
      outer.append(label, nested);
      popup.attachTrigger(outer, { trigger: { hover: { mouseOnly: true, move: false, nestedTooltipTriggers: true, ...hover } } });
      popup.setReference(outer);
      popup.attachFloating(element('div'), { floating: { hover: {} } });
      return { popup, outer, label, nested };
    }
  });
});

describe('useHoverFloatingInteraction', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  function render(closeDelay?: number) {
    const popup = harness();
    popup.attachSession();
    const button = element('button');
    popup.attachTrigger(button, { trigger: { hover: { delayClose: closeDelay ?? null } } });
    popup.setReference(button);
    const tooltip = element('div');
    tooltip.setAttribute('role', 'tooltip');
    popup.attachFloating(tooltip, { floating: { hover: { closeDelay: closeDelay ?? null } } });
    return { popup, button, tooltip };
  }

  it('closes after the close delay when the pointer leaves the floating element', async () => {
    const { popup, button, tooltip } = render(100);

    hoverMouse(button, 'mouseenter');
    await vi.advanceTimersByTimeAsync(0);
    expect(popup.open).toBe(true);

    hoverMouse(button, 'mouseleave', { relatedTarget: tooltip });
    hoverMouse(tooltip, 'mouseenter');
    hoverMouse(tooltip, 'mouseleave');

    await vi.advanceTimersByTimeAsync(99);

    expect(popup.open).toBe(true);

    await vi.advanceTimersByTimeAsync(1);

    expect(popup.open).toBe(false);
  });

  it('keeps the popup open when the floating element is entered before closeDelay elapses', async () => {
    const { popup, button, tooltip } = render(500);

    hoverMouse(button, 'mouseenter');
    await vi.advanceTimersByTimeAsync(0);
    expect(popup.open).toBe(true);

    hoverMouse(button, 'mouseleave', { relatedTarget: tooltip });

    await vi.advanceTimersByTimeAsync(499);

    hoverMouse(tooltip, 'mouseenter');

    await vi.advanceTimersByTimeAsync(500);

    expect(popup.open).toBe(true);

    hoverMouse(tooltip, 'mouseleave');

    await vi.advanceTimersByTimeAsync(499);

    expect(popup.open).toBe(true);

    await vi.advanceTimersByTimeAsync(1);

    expect(popup.open).toBe(false);
  });

  it('port: a mouseenter or mousemove open is a hover open that leaving the floating element closes, and a focus open is not', () => {
    const popup = harness();
    popup.attachSession();
    const button = element('button');
    popup.attachTrigger(button, { trigger: { hover: {}, focus: {} } });
    popup.setReference(button);
    const tooltip = element('div');
    popup.attachFloating(tooltip, { floating: { hover: {} } });

    hoverMouse(button, 'mouseenter');
    hoverMouse(tooltip, 'mouseleave');
    expect(popup.open).toBe(false);

    hoverMouse(button, 'mousemove');
    expect(popup.changes.at(-1)?.details.event?.type).toBe('mousemove');
    hoverMouse(tooltip, 'mouseleave');
    expect(popup.open).toBe(false);

    button.focus();
    expect(popup.changes.at(-1)?.details.reason).toBe('trigger-focus');
    hoverMouse(tooltip, 'mouseleave');
    expect(popup.open).toBe(true);
  });

  it('port: leaving the floating element onto an enabled trigger or into a descendant popup does not close it', () => {
    const { popup, button, tooltip } = render(0);
    const other = element('button');
    popup.attachTrigger(other);
    const child = harness();
    child.attachSession({ session: { parentToken: popup.token } });
    const childFloating = element('div');
    const inside = document.createElement('span');
    childFloating.append(inside);
    child.attachFloating(childFloating);

    hoverMouse(button, 'mouseenter');
    expect(popup.open).toBe(true);

    hoverMouse(tooltip, 'mouseleave', { relatedTarget: other });
    hoverMouse(tooltip, 'mouseleave', { relatedTarget: inside });
    expect(popup.open).toBe(true);

    hoverMouse(tooltip, 'mouseleave', { relatedTarget: document.body });
    expect(popup.open).toBe(false);
  });

  it('port: while a safe polygon is active, the floating element passes its leave to it, so leaving back into the trough keeps the popup open', () => {
    const popup = harness();
    popup.attachSession();
    const button = element('button');
    popup.attachTrigger(button, { trigger: { hover: { safePolygon: true } } });
    popup.setReference(button);
    const tooltip = element('div');
    popup.attachFloating(tooltip, { floating: { hover: {} } });
    button.getBoundingClientRect = () => hoverRect(0, 0, 100, 100);
    tooltip.getBoundingClientRect = () => hoverRect(0, 120, 100, 100);
    getSession(popup.token)!.setPlacement('bottom');

    hoverMouse(button, 'mouseenter');
    hoverMouse(button, 'mouseleave', { clientX: 50, clientY: 100, relatedTarget: tooltip });
    hoverMouse(tooltip, 'mouseenter');
    hoverMouse(tooltip, 'mouseleave', { clientX: 50, clientY: 110 });
    expect(popup.open).toBe(true);

    hoverMouse(tooltip, 'mouseleave', { clientX: 300, clientY: 170 });
    expect(popup.open).toBe(false);
  });

  it('port: leaving a nested popup while its parent has open children waits for floating.closed, then requests a hover close after 0 ms and emits it again', async () => {
    const parent = harness({ open: true });
    parent.attachSession();
    parent.attachFloating(element('div'));
    const nested = harness();
    nested.attachSession({ session: { parentToken: parent.token } });
    const nestedTrigger = element('button');
    nested.attachTrigger(nestedTrigger, { trigger: { hover: {} } });
    const nestedFloating = element('div');
    nested.attachFloating(nestedFloating, { floating: { hover: {} } });
    const tree = getSession(nested.token)!.tree;
    const closed: unknown[] = [];
    tree.events.on('floating.closed', (event: unknown) => closed.push(event));

    hoverMouse(nestedTrigger, 'mouseenter');
    hoverMouse(nestedFloating, 'mouseleave');
    expect(nested.open).toBe(true);
    expect(nested.requests).toHaveLength(1);

    // The parent's last open child closes by other means; another popup's hover close then emits on the tree.
    nested.setOpen(false);
    const other = new MouseEvent('mouseleave');
    tree.events.emit('floating.closed', other);
    expect(nested.requests).toHaveLength(1);
    await vi.advanceTimersByTimeAsync(0);
    expect(nested.requests.at(-1)?.payload).toMatchObject({ open: false, reason: 'trigger-hover' });
    expect(closed).toEqual([other, other]);

    // A mouseenter that brings the pointer back before the 0 ms elapse cancels it.
    hoverMouse(nestedTrigger, 'mouseenter');
    hoverMouse(nestedFloating, 'mouseleave');
    nested.setOpen(false);
    tree.events.emit('floating.closed', other);
    hoverMouse(nestedFloating, 'mouseenter');
    await vi.advanceTimersByTimeAsync(0);
    expect(nested.requests).toHaveLength(3);
  });
});

describe('safePolygon', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  function createMouseMoveEvent(clientX: number, clientY: number, target: EventTarget | null = null): MouseEvent {
    return {
      type: 'mousemove',
      clientX,
      clientY,
      relatedTarget: null,
      composedPath: () => [target],
    } as unknown as MouseEvent;
  }

  // Port: Base UI's FloatingTreeStore, shaped as the session tree.
  function createTree(nodes: SessionNode[] = []): HoverTestTree {
    return { nodesRef: { current: nodes }, events: createEventEmitter() };
  }

  function createHandleCloseContext({
    domReference,
    floating,
    onClose,
    tree,
    placement = 'right',
    x = 2,
    y = 0,
    nodeId = 'root',
  }: {
    domReference: Element;
    floating: HTMLElement;
    onClose: () => void;
    tree: HoverTestTree;
    placement?: 'top' | 'bottom' | 'left' | 'right';
    x?: number;
    y?: number;
    nodeId?: string;
  }): HoverTestCloseContext {
    return {
      x,
      y,
      placement,
      elements: { domReference, floating },
      nodeId,
      onClose,
      tree,
    };
  }

  function createPlacementScenario(placement: 'top' | 'bottom' | 'left' | 'right') {
    const referenceRect = hoverRect(0, 0, 100, 100);

    switch (placement) {
      case 'top':
        return {
          referenceRect,
          floatingRect: hoverRect(0, -120, 100, 100),
          leavePoint: [50, 0] as const,
          troughPoint: [50, -10] as const,
          outsidePoint: [50, 150] as const,
        };
      case 'bottom':
        return {
          referenceRect,
          floatingRect: hoverRect(0, 120, 100, 100),
          leavePoint: [50, 100] as const,
          troughPoint: [50, 110] as const,
          outsidePoint: [50, -50] as const,
        };
      case 'left':
        return {
          referenceRect,
          floatingRect: hoverRect(-120, 0, 100, 100),
          leavePoint: [0, 50] as const,
          troughPoint: [-10, 50] as const,
          outsidePoint: [150, 50] as const,
        };
      case 'right':
      default:
        return {
          referenceRect,
          floatingRect: hoverRect(120, 0, 100, 100),
          leavePoint: [100, 50] as const,
          troughPoint: [110, 50] as const,
          outsidePoint: [-50, 50] as const,
        };
    }
  }

  // Port: the tree of the first and third titles is the session forest, where an attached child session publishes its
  // context on its node.
  function sessionTree(childOpen: boolean) {
    const root = harness();
    root.attachSession();
    const child = harness();
    child.attachSession({ session: { parentToken: root.token } });
    child.attachFloating(element('div'));
    if (childOpen) {
      child.setOpen(true);
    }
    return { tree: getSession(root.token)!.tree, nodeId: root.token, child };
  }

  it('does not close when a nested child is open', async () => {
    const domReference = document.createElement('button');
    const floating = document.createElement('div');
    domReference.getBoundingClientRect = () => hoverRect(0, 0, 100, 100);
    floating.getBoundingClientRect = () => hoverRect(120, 0, 100, 100);

    const { tree, nodeId, child } = sessionTree(true);
    const onClose = vi.fn();
    const context = createHandleCloseContext({
      domReference,
      floating,
      onClose,
      tree,
      nodeId,
    });

    const handler = safePolygon()(context);
    handler(createMouseMoveEvent(3, -1));

    await vi.advanceTimersByTimeAsync(50);

    expect(onClose).toHaveBeenCalledTimes(0);

    // Port: the contrast, once the child closes the same move closes after the intent timeout.
    child.setOpen(false);
    handler(createMouseMoveEvent(3, -1));
    await vi.advanceTimersByTimeAsync(50);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('does not close when an open nested child is behind a contextless intermediary node', async () => {
    const domReference = document.createElement('button');
    const floating = document.createElement('div');
    domReference.getBoundingClientRect = () => hoverRect(0, 0, 100, 100);
    floating.getBoundingClientRect = () => hoverRect(120, 0, 100, 100);

    // Port: the session forest never holds a contextless node, so this tree is a literal one.
    const nodes: SessionNode[] = [
      { id: 'inline-root', parentId: 'root' },
      { id: 'child', parentId: 'inline-root', context: { open: true } },
    ];
    const tree = createTree(nodes);
    const onClose = vi.fn();
    const context = createHandleCloseContext({
      domReference,
      floating,
      onClose,
      tree,
    });

    const handler = safePolygon()(context);
    handler(createMouseMoveEvent(3, -1));

    await vi.advanceTimersByTimeAsync(50);

    expect(onClose).toHaveBeenCalledTimes(0);

    // Port: the contrast, once the child behind the intermediary closes the same move closes.
    nodes[1] = { id: 'child', parentId: 'inline-root', context: { open: false } };
    handler(createMouseMoveEvent(3, -1));
    await vi.advanceTimersByTimeAsync(50);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('closes after intent timeout when no nested child is open', async () => {
    const domReference = document.createElement('button');
    const floating = document.createElement('div');
    domReference.getBoundingClientRect = () => hoverRect(0, 0, 100, 100);
    floating.getBoundingClientRect = () => hoverRect(120, 0, 100, 100);

    const { tree, nodeId } = sessionTree(false);
    const onClose = vi.fn();
    const context = createHandleCloseContext({
      domReference,
      floating,
      onClose,
      tree,
      nodeId,
    });

    const handler = safePolygon()(context);
    handler(createMouseMoveEvent(3, -1));

    await vi.advanceTimersByTimeAsync(50);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it.each(['top', 'bottom', 'left', 'right'] as const)('keeps open while moving through the trough on %s placement', (placement) => {
    const domReference = document.createElement('button');
    const floating = document.createElement('div');
    const scenario = createPlacementScenario(placement);

    domReference.getBoundingClientRect = () => scenario.referenceRect;
    floating.getBoundingClientRect = () => scenario.floatingRect;

    const tree = createTree();
    const onClose = vi.fn();
    const context = createHandleCloseContext({
      domReference,
      floating,
      onClose,
      tree,
      placement,
      x: scenario.leavePoint[0],
      y: scenario.leavePoint[1],
    });

    const handler = safePolygon()(context);
    handler(createMouseMoveEvent(scenario.troughPoint[0], scenario.troughPoint[1]));

    expect(onClose).toHaveBeenCalledTimes(0);

    // Port: the contrast, the same handler closes on a move away from the corridor.
    handler(createMouseMoveEvent(scenario.outsidePoint[0], scenario.outsidePoint[1]));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it.each(['top', 'bottom', 'left', 'right'] as const)('closes when moving away from corridor on %s placement', (placement) => {
    const domReference = document.createElement('button');
    const floating = document.createElement('div');
    const scenario = createPlacementScenario(placement);

    domReference.getBoundingClientRect = () => scenario.referenceRect;
    floating.getBoundingClientRect = () => scenario.floatingRect;

    const tree = createTree();
    const onClose = vi.fn();
    const context = createHandleCloseContext({
      domReference,
      floating,
      onClose,
      tree,
      placement,
      x: scenario.leavePoint[0],
      y: scenario.leavePoint[1],
    });

    const handler = safePolygon()(context);
    handler(createMouseMoveEvent(scenario.outsidePoint[0], scenario.outsidePoint[1]));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('resets traversal state for a new handler invocation', () => {
    const domReference = document.createElement('button');
    const floating = document.createElement('div');
    const scenario = createPlacementScenario('right');

    domReference.getBoundingClientRect = () => scenario.referenceRect;
    floating.getBoundingClientRect = () => scenario.floatingRect;

    const tree = createTree();
    const onClose = vi.fn();
    const context = createHandleCloseContext({
      domReference,
      floating,
      onClose,
      tree,
      placement: 'right',
      x: scenario.leavePoint[0],
      y: scenario.leavePoint[1],
    });

    const handleClose = safePolygon();
    const firstHandler = handleClose(context);
    firstHandler(createMouseMoveEvent(130, 50, floating));

    const secondHandler = handleClose(context);
    secondHandler(createMouseMoveEvent(scenario.troughPoint[0], scenario.troughPoint[1]));

    expect(onClose).toHaveBeenCalledTimes(0);

    // Port: the contrast. Past the trough, inside the corridor, the landed first handler closes at once while a fresh
    // one only starts the intent timer.
    const freshHandler = handleClose(context);
    freshHandler(createMouseMoveEvent(125, 50));
    expect(onClose).toHaveBeenCalledTimes(0);
    firstHandler(createMouseMoveEvent(125, 50));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
// #endregion hover tests

// #region pointer tests (useClientPoint, the delay group, the hovered line)
// The pointer regions' exports; an import declaration is hoisted, so it stands in the block that uses it.
import { delayGroupInstantPhase, readDelayGroup, readHoveredLine, updateHoveredLine, type InteractionsOptions } from './interactions';
import type { TwinHandle } from './twin';

/**
 * Lets the pointer regions' deferred effects run: they re-read the session a microtask after the relay or the event
 * that changed their inputs, as React runs a hook's effects after its commit.
 */
async function flushPointerEffects(): Promise<void> {
  for (let i = 0; i < 5; i += 1) {
    await Promise.resolve();
  }
}

// ---------------------------------------------------------------------------------------------------------------
// useClientPoint (useClientPoint.test.tsx; FLT-PTR-07..10, FLT-API-14, FLT-ALG-25, FLT-EDGE-09, FLT-STR-02, FLT-STR-11).
// Base UI reads `elements.reference.getBoundingClientRect()`, the root store's selected reference (FLT-STR-02); this
// module does no positioning, so the titles read the session's `positionReference`, else its DOM reference. Base UI's
// `fireEvent` runs React's effects before it returns; here the effects a relay or an event schedules run a microtask
// later, so a title flushes them where Base UI's `act` would.
// ---------------------------------------------------------------------------------------------------------------

describe('useClientPoint', () => {
  type Axis = 'x' | 'y' | 'both';

  interface ClientPointApp {
    readonly popup: PopupHarness;
    readonly reference: HTMLElement;
    readonly trigger: TwinHandle;
    floating: HTMLElement | null;
    /** The App's button: flips `open` with no event, so no open event is recorded (a C#-origin commit). */
    toggle(): Promise<void>;
    /** A re-render with `enabled` (Tooltip's `trackCursorAxis` turned off is `clientPoint: null`). */
    setEnabled(enabled: boolean): void;
  }

  // The App of useClientPoint.test.tsx: a reference carrying the hook's props (`getReferenceProps` and `getTriggerProps`
  // are the same listeners on the trigger handle here), and a floating element rendered while open.
  function app(options: { axis?: Axis; setReference?: boolean } = {}): ClientPointApp {
    const popup = harness();
    popup.attachSession();
    const reference = element('div', 'reference');
    const clientPoint = options.axis === undefined ? {} : { axis: options.axis };
    const trigger = popup.attachTrigger(reference, { trigger: { clientPoint } });
    if (options.setReference !== false) {
      popup.setReference(reference);
    }
    let floatingHandle: TwinHandle | null = null;
    const state: ClientPointApp = {
      popup,
      reference,
      trigger,
      floating: null,
      async toggle() {
        if (!popup.open) {
          popup.setOpen(true);
          state.floating = element('div', 'floating');
          floatingHandle = popup.attachFloating(state.floating);
        } else {
          popup.setOpen(false);
          floatingHandle?.dispose();
          state.floating?.remove();
          floatingHandle = null;
          state.floating = null;
        }
        await flushPointerEffects();
      },
      setEnabled(enabled) {
        trigger.update(JSON.stringify({ sessionToken: popup.token, trigger: { clientPoint: enabled ? clientPoint : null } }));
      },
    };
    return state;
  }

  function referenceRect(popup: PopupHarness) {
    const session = getSession(popup.token)!;
    return (session.positionReference ?? session.domReferenceElement)?.getBoundingClientRect();
  }

  function expectLocation(popup: PopupHarness, { x, y }: { x: number; y: number }) {
    const rect = referenceRect(popup);
    expect(rect?.x).toBe(x);
    expect(rect?.y).toBe(y);
    expect(rect?.width).toBe(0);
    expect(rect?.height).toBe(0);
  }

  function expectRect(popup: PopupHarness, { x, y, width, height }: { x: number; y: number; width: number; height: number }) {
    const rect = referenceRect(popup);
    expect(rect?.x).toBe(x);
    expect(rect?.y).toBe(y);
    expect(rect?.width).toBe(width);
    expect(rect?.height).toBe(height);
  }

  function mouseMove(target: EventTarget, clientX: number, clientY: number) {
    target.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, clientX, clientY }));
  }

  function rectOf(x: number, y: number, width: number, height: number): DOMRect {
    return { x, y, width, height, top: y, right: x + width, bottom: y + height, left: x, toJSON: () => {} };
  }

  // The test's `store.set('positionReference', …)`: the module's setter is internal, so the title writes the field.
  function writePositionReference(popup: PopupHarness, value: unknown) {
    (getSession(popup.token) as unknown as { positionReference: unknown }).positionReference = value;
  }

  it('updates position from trigger props', async () => {
    // Port: the test shim's element sync (FLT-STR-11's standalone root store, M5's) is the session's `setReference`.
    const { popup, reference } = app();

    await flushPointerEffects();

    mouseMove(reference, 400, 200);

    await flushPointerEffects();

    expectLocation(popup, { x: 400, y: 200 });
  });

  it('uses trigger element when dom reference is missing', async () => {
    // Port: no DOM reference is set at all, so the virtual reference can only come from the handler's element.
    const { popup, reference } = app({ axis: 'x', setReference: false });
    reference.getBoundingClientRect = () => rectOf(10, 50, 0, 0);
    expect(getSession(popup.token)!.domReferenceElement).toBeNull();

    await flushPointerEffects();

    mouseMove(reference, 200, 300);

    await flushPointerEffects();

    expectLocation(popup, { x: 200, y: 50 });
  });

  it('renders at mouse event coords', async () => {
    const { popup, reference, toggle } = app();

    await flushPointerEffects();

    mouseMove(reference, 500, 500);

    await flushPointerEffects();

    expectLocation(popup, { x: 500, y: 500 });

    mouseMove(reference, 1000, 1000);

    await flushPointerEffects();

    expectLocation(popup, { x: 1000, y: 1000 });

    // Window listener isn't registered unless the floating element is open.
    mouseMove(window, 700, 700);

    await flushPointerEffects();

    expectLocation(popup, { x: 1000, y: 1000 });

    await toggle();

    mouseMove(reference, 700, 700);
    await flushPointerEffects();

    expectLocation(popup, { x: 700, y: 700 });

    mouseMove(document.body, 0, 0);
    await flushPointerEffects();

    expectLocation(popup, { x: 0, y: 0 });
  });

  it('cleans up window listener when closing or disabling', async () => {
    const { popup, reference, toggle, setEnabled } = app();

    await toggle();

    mouseMove(reference, 500, 500);
    await flushPointerEffects();

    await toggle();

    mouseMove(document.body, 0, 0);
    await flushPointerEffects();

    expectLocation(popup, { x: 500, y: 500 });

    await toggle();

    mouseMove(document.body, 500, 500);
    await flushPointerEffects();

    expectLocation(popup, { x: 500, y: 500 });

    setEnabled(false);

    mouseMove(document.body, 0, 0);
    await flushPointerEffects();

    expectRect(popup, { x: 0, y: 0, width: 0, height: 0 });
  });

  it('clears virtual references on unmount without retaining a stale DOM reference', async () => {
    const popup = harness();
    popup.attachSession();
    const reference = element('button', 'reference');
    popup.attachFloating(element('div'));
    const virtualReference = {
      getBoundingClientRect: () => reference.getBoundingClientRect(),
    };

    writePositionReference(popup, virtualReference);

    const trigger = popup.attachTrigger(reference, { trigger: { clientPoint: {} } });
    popup.setReference(reference);

    trigger.update(JSON.stringify({ sessionToken: popup.token, trigger: { clientPoint: null } }));
    await flushPointerEffects();

    expect(getSession(popup.token)!.positionReference).toBe(reference);

    writePositionReference(popup, virtualReference);
    trigger.dispose();

    expect(getSession(popup.token)!.positionReference).toBe(null);
  });

  it('axis x', async () => {
    const { popup, reference, toggle } = app({ axis: 'x' });

    await toggle();

    mouseMove(reference, 500, 500);
    await flushPointerEffects();

    expectLocation(popup, { x: 500, y: 0 });
  });

  it('axis y', async () => {
    const { popup, reference, toggle } = app({ axis: 'y' });

    await toggle();

    mouseMove(reference, 500, 500);
    await flushPointerEffects();

    expectLocation(popup, { x: 0, y: 500 });
  });

  it.for([
    {
      axis: 'x',
      atCursor: { x: 500, y: 20, width: 0, height: 40 },
      afterReferenceMoves: { x: 550, y: 70, width: 0, height: 40 },
    },
    {
      axis: 'y',
      atCursor: { x: 10, y: 500, width: 30, height: 0 },
      afterReferenceMoves: { x: 60, y: 550, width: 30, height: 0 },
    },
  ] as const)('keeps the reference size on the untracked axis when tracking $axis', async ({ axis, atCursor, afterReferenceMoves }) => {
    const { popup, reference, toggle } = app({ axis });
    let referenceRectValue = { x: 10, y: 20, width: 30, height: 40 };

    reference.getBoundingClientRect = () => {
      const { x, y, width, height } = referenceRectValue;
      return rectOf(x, y, width, height);
    };

    mouseMove(reference, 500, 500);
    await flushPointerEffects();

    expectRect(popup, atCursor);

    // Opening re-reads the same virtual element as an auto-update. The open wasn't a hover,
    // so the cursor point keeps its offset from the reference instead of snapping back.
    referenceRectValue = { x: 60, y: 70, width: 30, height: 40 };

    await toggle();

    expectRect(popup, afterReferenceMoves);
  });

  it('removes window listener when cursor lands on floating element', async () => {
    const state = app();

    await state.toggle();

    mouseMove(state.reference, 500, 500);

    mouseMove(state.floating!, 500, 500);

    await flushPointerEffects();

    mouseMove(document.body, 0, 0);

    expectLocation(state.popup, { x: 500, y: 500 });
  });

  it('reattaches window listener after cursor returns from floating element to reference', async () => {
    const state = app();

    await state.toggle();

    mouseMove(state.reference, 500, 500);

    mouseMove(state.floating!, 500, 500);

    await flushPointerEffects();

    mouseMove(document.body, 0, 0);

    expectLocation(state.popup, { x: 500, y: 500 });

    mouseMove(state.reference, 600, 700);
    await flushPointerEffects();

    // Reapply deterministic coordinates after the effect attaches the window listener.
    // A real browser cursor can emit an unrelated move while the effect is flushing.
    mouseMove(state.reference, 600, 700);

    expectLocation(state.popup, { x: 600, y: 700 });

    mouseMove(document.body, 100, 200);

    await flushPointerEffects();
    expectLocation(state.popup, { x: 100, y: 200 });
  });

  // Port: the App's button calls `setOpen` with a focus event; here the trigger's own focus hook requests the open with
  // its `focusin`, which C# commits and the relay pairs with the request, so the session records it (FLT-STA-03).
  function focusApp() {
    const popup = harness();
    popup.attachSession();
    const reference = element('button', 'reference');
    popup.attachTrigger(reference, { trigger: { clientPoint: {}, focus: {} } });
    popup.setReference(reference);
    reference.getBoundingClientRect = () => rectOf(10, 20, 30, 40);
    return {
      popup,
      reference,
      async openWithFocusEvent() {
        reference.focus();
        popup.attachFloating(element('div', 'floating'));
        await flushPointerEffects();
        expect(popup.open).toBe(true);
        expect(getSession(popup.token)!.dataRef.current.openEvent?.type).toBe('focusin');
      },
    };
  }

  it('port: a floating element that attaches after the commit is relayed resets a virtual reference left by an earlier move to the DOM reference', async () => {
    const { popup, reference } = focusApp();
    mouseMove(reference, 500, 500);
    await flushPointerEffects();
    expectLocation(popup, { x: 500, y: 500 });

    // The focus open commits and is relayed; the Positioner attaches later (on Server, a round trip after the commit).
    reference.focus();
    await flushPointerEffects();
    popup.attachFloating(element('div', 'floating'));

    expectRect(popup, { x: 10, y: 20, width: 30, height: 40 });
  });

  it('restores the DOM reference when opened by a non-mouse event', async () => {
    const { popup, reference, openWithFocusEvent } = focusApp();

    mouseMove(reference, 500, 500);
    await flushPointerEffects();

    expectLocation(popup, { x: 500, y: 500 });

    await openWithFocusEvent();

    expectRect(popup, { x: 10, y: 20, width: 30, height: 40 });

    mouseMove(document.body, 100, 200);

    mouseMove(reference, 300, 400);
    await flushPointerEffects();

    expectRect(popup, { x: 10, y: 20, width: 30, height: 40 });
  });

  it('port: a move on the reference after a non-mouse open installs no virtual reference, even before the effect re-runs', async () => {
    const { popup, reference, openWithFocusEvent } = focusApp();
    await openWithFocusEvent();
    expect(getSession(popup.token)!.positionReference).toBe(reference);

    mouseMove(reference, 300, 400);

    expect(getSession(popup.token)!.positionReference).toBe(reference);
  });

  it('port: with a mouse the cursor is followed while the floating element stays attached after the close; with a touch only while open', async () => {
    const { popup, reference } = app();
    popup.setOpen(true);
    popup.attachFloating(element('div', 'floating'));
    reference.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'mouse' }));
    await flushPointerEffects();

    // The floating element stays attached through its exit transition.
    popup.setOpen(false);
    await flushPointerEffects();
    mouseMove(document.body, 300, 300);
    expectLocation(popup, { x: 300, y: 300 });

    reference.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch' }));
    await flushPointerEffects();
    mouseMove(document.body, 100, 100);
    expectLocation(popup, { x: 300, y: 300 });
  });

  it('port: disabling while open ignores cursor updates after re-enabling until the floating element unmounts', async () => {
    const state = app();
    await state.toggle();

    state.setEnabled(false);
    state.setEnabled(true);
    await flushPointerEffects();
    mouseMove(state.reference, 500, 500);
    expect(getSession(state.popup.token)!.positionReference).toBe(state.reference);

    await state.toggle();
    mouseMove(state.reference, 500, 500);
    expectLocation(state.popup, { x: 500, y: 500 });
  });

  it('port: a trigger that never enables cursor anchoring leaves the position reference alone, attached or disposed', async () => {
    const popup = harness();
    popup.attachSession();
    const virtualReference = { getBoundingClientRect: () => rectOf(1, 2, 0, 0) };
    const anchored = popup.attachTrigger(element('button'), { trigger: { clientPoint: {} } });
    writePositionReference(popup, virtualReference);
    anchored.dispose();
    expect(getSession(popup.token)!.positionReference).toBeNull();

    writePositionReference(popup, virtualReference);
    const reference = element('button');
    const plain = popup.attachTrigger(reference);
    popup.setOpen(true, 'none', reference.id);
    await flushPointerEffects();
    mouseMove(reference, 50, 60);
    plain.dispose();

    expect(getSession(popup.token)!.positionReference).toBe(virtualReference);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// FloatingDelayGroup (FloatingDelayGroup.test.tsx; FLT-API-16, FLT-ALG-23, FLT-EVT-07, FLT-ANI-01, FLT-EDGE-11/12).
// The Tooltip of the upstream file is a session with one trigger handle that names the provider token; the provider is
// an elementless `provider` handle. Its `useHover` with `delay: () => delayRef.current` is the hover region's, built in
// parallel: `mouseEnter` and `mouseLeave` below stand in for it (they read the group at the event as the hover region
// does, start the timer, and commit the open or close as C# would), until the hover region's merge replaces them with
// `mouseenter` and `mouseleave` events. The group's own half (its readings, the previous member's close, the instant
// phase an open carries) is the module's.
// ---------------------------------------------------------------------------------------------------------------

describe('FloatingDelayGroup', () => {
  const token = 'delay-group';

  beforeEach(() => {
    vi.useFakeTimers();
  });

  interface GroupTooltip {
    readonly popup: PopupHarness;
    readonly reference: HTMLElement;
    trigger: TwinHandle;
    timer: ReturnType<typeof setTimeout> | undefined;
    /** The instant phase each open carried, read when the open was requested. */
    readonly instants: boolean[];
  }

  function provider(delay: { open: number; close: number }, timeoutMs?: number): TwinHandle {
    return attach(
      null,
      JSON.stringify({ provider: { token, delayOpen: delay.open, delayClose: delay.close, timeout: timeoutMs ?? null } }),
      createFakeTwin().twin,
    );
  }

  function memberOptions(): Partial<InteractionsOptions> {
    return { trigger: { hover: { delayGroupToken: token } } };
  }

  function tooltip(label: string): GroupTooltip {
    const popup = harness();
    popup.attachSession();
    const reference = element('button', `reference-${label}`);
    const trigger = popup.attachTrigger(reference, memberOptions());
    return { popup, reference, trigger, timer: undefined, instants: [] };
  }

  const recordOf = (tip: GroupTooltip) => getSession(tip.popup.token) as Parameters<typeof delayGroupInstantPhase>[0];

  function mouseEnter(tip: GroupTooltip) {
    clearTimeout(tip.timer);
    const open = () => {
      tip.instants.push(delayGroupInstantPhase(recordOf(tip), token));
      tip.popup.setOpen(true, 'trigger-hover', tip.reference.id);
    };
    const delay = readDelayGroup(token)?.delayOpen ?? 0;
    if (delay) {
      tip.timer = setTimeout(open, delay);
    } else {
      open();
    }
  }

  function mouseLeave(tip: GroupTooltip) {
    clearTimeout(tip.timer);
    const close = () => tip.popup.setOpen(false, 'trigger-hover');
    const delay = readDelayGroup(token)?.delayClose ?? 0;
    if (delay) {
      tip.timer = setTimeout(close, delay);
    } else {
      close();
    }
  }

  async function advance(ms: number) {
    await vi.advanceTimersByTimeAsync(ms);
    await flushPointerEffects();
  }

  it('groups delays correctly', async () => {
    provider({ open: 1000, close: 200 });
    const one = tooltip('one');
    const two = tooltip('two');
    const three = tooltip('three');

    mouseEnter(one);

    await advance(1);

    expect(one.popup.open).toBe(false);

    await advance(999);

    expect(one.popup.open).toBe(true);

    mouseEnter(two);

    await advance(1);

    expect(one.popup.open).toBe(false);
    expect(two.popup.open).toBe(true);

    mouseEnter(three);

    await advance(1);

    expect(two.popup.open).toBe(false);
    expect(three.popup.open).toBe(true);

    mouseLeave(three);

    await advance(1);

    expect(three.popup.open).toBe(true);

    await advance(199);

    expect(three.popup.open).toBe(false);

    // Port (FLT-EVT-07, FLT-ANI-01): each previous member was closed through its Root's twin, reason `none`, no event,
    // with the instant phase; the opens that took over carried it, the first did not.
    for (const previous of [one, two]) {
      expect(previous.popup.requests.map((request) => request.payload)).toEqual([
        { open: false, reason: 'none', event: null, triggerId: null, instant: true },
      ]);
    }
    expect([...one.instants, ...two.instants, ...three.instants]).toEqual([false, true, true]);
  });

  it('timeoutMs', async () => {
    provider({ open: 1000, close: 100 }, 500);
    const one = tooltip('one');
    const two = tooltip('two');
    const three = tooltip('three');

    mouseEnter(one);

    await advance(1000);

    mouseLeave(one);

    expect(one.popup.open).toBe(true);

    await advance(499);

    expect(one.popup.open).toBe(false);

    mouseEnter(two);

    await advance(1);

    expect(two.popup.open).toBe(true);

    mouseEnter(three);

    await advance(1);

    expect(two.popup.open).toBe(false);
    expect(three.popup.open).toBe(true);

    mouseLeave(three);

    await advance(1);

    expect(three.popup.open).toBe(true);

    await advance(99);

    expect(three.popup.open).toBe(false);
  });

  it('resets the instant phase after Strict Mode replays the lifecycle effects', async () => {
    // Restated (ADR 06 Amendments 2026-10-18 rule 2): React's replay of the mount effects is each member's trigger handle
    // disposed twice and attached again to the same element before the hover. Base UI's `data-instant-phase` on the
    // second trigger is the instant phase its open carried; after its close, the trigger's own phase is C#'s (from the
    // hover region's close request), and the group's half is the phase every member's next open carries.
    provider({ open: 1000, close: 0 }, 50);
    const one = tooltip('one');
    const two = tooltip('two');
    for (const tip of [one, two]) {
      tip.trigger.dispose();
      tip.trigger.dispose();
      tip.trigger = tip.popup.attachTrigger(tip.reference, memberOptions());
    }

    mouseEnter(one);
    await advance(1000);

    mouseEnter(two);
    await advance(1);

    expect(two.instants).toEqual([true]);

    mouseLeave(two);
    await flushPointerEffects();
    expect(delayGroupInstantPhase(recordOf(one), token)).toBe(true);
    await advance(50);

    expect(delayGroupInstantPhase(recordOf(one), token)).toBe(false);
    expect(readDelayGroup(token)).toEqual({ delayOpen: 1000, delayClose: 0 });
  });

  it('keeps the active context when an inactive consumer unmounts', async () => {
    provider({ open: 1000, close: 100 }, 500);
    const one = tooltip('one');
    const two = tooltip('two');
    const three = tooltip('three');

    mouseEnter(one);

    await advance(1000);

    expect(one.popup.open).toBe(true);

    two.popup.dispose();
    expect(getSession(two.popup.token)).toBeUndefined();

    mouseEnter(three);

    await advance(1);

    expect(one.popup.open).toBe(false);
    expect(three.popup.open).toBe(true);
  });

  it('keeps the timeout active when the last closed consumer unmounts', async () => {
    provider({ open: 1000, close: 100 }, 500);
    const one = tooltip('one');
    const two = tooltip('two');

    mouseEnter(one);

    await advance(1000);

    mouseLeave(one);

    await advance(100);

    expect(one.popup.open).toBe(false);

    one.popup.dispose();
    expect(getSession(one.popup.token)).toBeUndefined();
    // Port: the group's half, the warm delay the next hover reads (FLT-EDGE-11).
    expect(readDelayGroup(token)).toEqual({ delayOpen: 0, delayClose: 100 });

    mouseEnter(two);

    await advance(1);

    expect(two.popup.open).toBe(true);
  });

  it('does not re-render unrelated consumers', async () => {
    provider({ open: 1000, close: 100 }, 500);
    const one = tooltip('one');
    const two = tooltip('two');
    const three = tooltip('three');

    mouseEnter(one);

    await advance(1000);

    mouseLeave(one);

    expect(one.popup.open).toBe(true);

    await advance(499);

    expect(one.popup.open).toBe(false);

    mouseEnter(two);

    await advance(1);

    expect(two.popup.open).toBe(true);
    // Port: the render counts are React's (FLT-ALG-23's note); a member's state lives in its controller and the group,
    // and nothing reaches the twin of a member the change does not concern.
    expect(three.popup.requests).toEqual([]);
  });

  it('port: a provider holds its group from its first attach to its last dispose, and a trigger without one reads none', () => {
    const first = provider({ open: 1000, close: 200 });
    const second = provider({ open: 1000, close: 200 });

    expect(readDelayGroup(token)).toEqual({ delayOpen: 1000, delayClose: 200 });
    expect(readDelayGroup('another-provider')).toBeNull();
    expect(readDelayGroup(null)).toBeNull();

    first.dispose();
    first.dispose();
    expect(readDelayGroup(token)).toEqual({ delayOpen: 1000, delayClose: 200 });

    second.dispose();
    expect(readDelayGroup(token)).toBeNull();
  });

  it('port: the group resets to the provider delay timeoutMs after its last member closed', async () => {
    provider({ open: 1000, close: 100 }, 500);
    const one = tooltip('one');
    const two = tooltip('two');

    mouseEnter(one);
    await advance(1000);
    mouseLeave(one);
    await advance(100);

    expect(readDelayGroup(token)).toEqual({ delayOpen: 0, delayClose: 100 });
    expect(delayGroupInstantPhase(recordOf(two), token)).toBe(true);
    expect(delayGroupInstantPhase(recordOf(one), token)).toBe(false);

    await advance(499);
    expect(readDelayGroup(token)?.delayOpen).toBe(0);

    await advance(1);
    expect(readDelayGroup(token)).toEqual({ delayOpen: 1000, delayClose: 100 });
    expect(delayGroupInstantPhase(recordOf(two), token)).toBe(false);
  });

  it('port: a provider delay change keeps the open delay at 0 while a member is current and adopts the new close delay; with none it replaces the delay', async () => {
    const handle = provider({ open: 1000, close: 100 });
    const one = tooltip('one');
    const update = (open: number, close: number) =>
      handle.update(JSON.stringify({ provider: { token, delayOpen: open, delayClose: close, timeout: null } }));

    update(800, 50);
    expect(readDelayGroup(token)).toEqual({ delayOpen: 800, delayClose: 50 });

    mouseEnter(one);
    await advance(800);
    expect(one.popup.open).toBe(true);

    update(600, 300);
    expect(readDelayGroup(token)).toEqual({ delayOpen: 0, delayClose: 300 });

    mouseLeave(one);
    await advance(300);
    expect(one.popup.open).toBe(false);
    expect(readDelayGroup(token)).toEqual({ delayOpen: 600, delayClose: 300 });
  });

  it('port: disposing the current member while its popup is open clears the group at once', async () => {
    provider({ open: 1000, close: 100 }, 500);
    const one = tooltip('one');
    const two = tooltip('two');

    mouseEnter(one);
    await advance(1000);
    expect(readDelayGroup(token)?.delayOpen).toBe(0);
    expect(delayGroupInstantPhase(recordOf(two), token)).toBe(true);

    one.trigger.dispose();

    expect(readDelayGroup(token)).toEqual({ delayOpen: 1000, delayClose: 100 });
    expect(delayGroupInstantPhase(recordOf(two), token)).toBe(false);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The hovered line (utils/popups/inlineRect.test.ts, its first six titles; PSH-PTR-08, PSH-PTR-29). Upstream calls the
// trigger props' handlers with a fake event over a ref; here a PreviewCard trigger handle with `hoveredLine` receives
// the events, the record is the session's, read through `readHoveredLine`, and `updateHoveredLine` is
// `updateInlineRectCoords`. The open the record is recomputed before (`recordHoveredLineBeforeOpen`) is the hover
// region's, proven when it is merged.
// ---------------------------------------------------------------------------------------------------------------

describe('inlineRect', () => {
  type RectLike = { left: number; top: number; right: number; bottom: number; width: number; height: number };

  const twoLines: RectLike[] = [
    { left: 0, top: 0, right: 10, bottom: 10, width: 10, height: 10 },
    { left: 0, top: 20, right: 10, bottom: 30, width: 10, height: 10 },
  ];

  function popupWithSession(): PopupHarness {
    const popup = harness();
    popup.attachSession();
    return popup;
  }

  function createTrigger(popup: PopupHarness, rects: RectLike[] | (() => RectLike[]), hoveredLine = true): HTMLElement {
    const trigger = element('span');
    Object.defineProperty(trigger, 'getClientRects', {
      value: () => (typeof rects === 'function' ? rects() : rects),
    });
    popup.attachTrigger(trigger, { trigger: { hoveredLine } });
    return trigger;
  }

  function mouse(trigger: Element, type: 'mouseenter' | 'mousemove', clientX: number, clientY: number) {
    trigger.dispatchEvent(new MouseEvent(type, { bubbles: type === 'mousemove', clientX, clientY }));
  }

  const hoveredLine = (popup: PopupHarness) => readHoveredLine(getSession(popup.token)!);

  it('clears stored coords on focus', () => {
    const popup = popupWithSession();
    const trigger = createTrigger(popup, twoLines);
    mouse(trigger, 'mousemove', 2, 3);
    expect(hoveredLine(popup)).toEqual({ x: 2, y: 3, lineIndex: 0, element: trigger });

    trigger.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));

    expect(hoveredLine(popup)).toBeUndefined();
  });

  it('stores undefined when the trigger does not wrap', () => {
    const popup = popupWithSession();
    let rects = twoLines;
    const trigger = createTrigger(popup, () => rects);
    mouse(trigger, 'mousemove', 1, 1);
    expect(hoveredLine(popup)).toEqual({ x: 1, y: 1, lineIndex: 0, element: trigger });

    rects = [{ left: 0, top: 0, right: 10, bottom: 10, width: 10, height: 10 }];
    mouse(trigger, 'mousemove', 5, 5);

    expect(hoveredLine(popup)).toBeUndefined();
  });

  it('updates stored coords on mouse move while closed', () => {
    const popup = popupWithSession();
    const trigger = createTrigger(popup, twoLines);

    mouse(trigger, 'mousemove', 5, 25);

    expect(hoveredLine(popup)).toEqual({ x: 5, y: 25, lineIndex: 1, element: trigger });
  });

  it('does not update stored coords on mouse move while open', () => {
    const popup = popupWithSession();
    const trigger = createTrigger(popup, twoLines);
    mouse(trigger, 'mousemove', 1, 1);
    popup.setOpen(true, 'trigger-hover', trigger.id);

    mouse(trigger, 'mousemove', 5, 25);

    expect(hoveredLine(popup)).toEqual({ x: 1, y: 1, lineIndex: 0, element: trigger });
  });

  it('does not update stored coords on mouse enter while open', () => {
    const popup = popupWithSession();
    const trigger = createTrigger(popup, twoLines);
    mouse(trigger, 'mouseenter', 1, 1);
    expect(hoveredLine(popup)).toEqual({ x: 1, y: 1, lineIndex: 0, element: trigger });
    popup.setOpen(true, 'trigger-hover', trigger.id);

    mouse(trigger, 'mouseenter', 5, 25);

    expect(hoveredLine(popup)).toEqual({ x: 1, y: 1, lineIndex: 0, element: trigger });
  });

  it('updates stored coords from a native mouse event while open', () => {
    const popup = popupWithSession();
    const trigger = createTrigger(popup, twoLines);
    mouse(trigger, 'mousemove', 1, 1);
    popup.setOpen(true, 'trigger-hover', trigger.id);

    updateHoveredLine(getSession(popup.token)!, trigger, 5, 25);

    expect(hoveredLine(popup)).toEqual({ x: 5, y: 25, lineIndex: 1, element: trigger });
  });

  it('port: the record is the session\'s: a trigger records only with hoveredLine, and moves on a trigger the popup is not open by replace it', () => {
    const popup = popupWithSession();
    const first = createTrigger(popup, twoLines);
    const second = createTrigger(popup, twoLines);
    const plain = createTrigger(popup, twoLines, false);
    mouse(first, 'mousemove', 5, 5);
    expect(hoveredLine(popup)).toEqual({ x: 5, y: 5, lineIndex: 0, element: first });

    popup.setOpen(true, 'trigger-hover', first.id);
    mouse(second, 'mousemove', 5, 25);
    expect(hoveredLine(popup)).toEqual({ x: 5, y: 25, lineIndex: 1, element: second });

    mouse(plain, 'mousemove', 1, 1);
    expect(hoveredLine(popup)).toEqual({ x: 5, y: 25, lineIndex: 1, element: second });
  });
});
// #endregion pointer tests
