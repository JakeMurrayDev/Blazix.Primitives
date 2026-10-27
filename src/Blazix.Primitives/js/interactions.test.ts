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
