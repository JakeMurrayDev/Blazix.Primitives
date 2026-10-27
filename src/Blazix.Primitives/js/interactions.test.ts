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
// #endregion pointer tests
