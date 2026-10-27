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
