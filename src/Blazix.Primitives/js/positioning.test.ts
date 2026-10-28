// L3 (ADR 06 §1): the `positioning` mechanism module in jsdom against a fake twin. Base UI's jsdom titles are kept
// verbatim: `utils/hideMiddleware.test.ts` (against the engine's own `hide()`, ADR 03 Consequences) and the two shift
// titles of `internals/useAnchorPositioning.test.tsx`, whose `vi.mock('../floating-ui-react')` shift spy becomes a spy on
// the wrapper `./floating-ui`. The other titles are the port's: the pipeline assembly (FLT-POS-02..05, FLT-ALG-14), the
// anchor (FLT-STA-07, FLT-POS-13, FLT-API-09, FLT-API-11), the tracking gate (FLT-STA-09, FLT-POS-11/12), the `.fn(state)`
// of the Base UI middleware over synthetic states (FLT-ALG-09, FLT-ALG-11, FLT-ALG-12, FLT-I18N-03; the `hide` test's
// technique), the change-only report and the handle's lifecycle. jsdom has no layout: rects and the viewport are stubbed.
// The browser-only titles (the conformance suite, the transform-origin block, the lazy flips) run at L4.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { hide as nativeHide } from '@floating-ui/dom';
import type { MiddlewareState, Rect, SideObject } from '@floating-ui/dom';
import {
  adaptiveOrigin,
  arrow,
  attach,
  evaluateOffset,
  getLogicalSide,
  hide,
  readPlacement,
  transformOrigin,
  update as updatePosition,
  type OffsetData,
  type PositioningOptions,
} from './positioning';
import { styleWriter } from './dom';
import { acquireSession, getSession, releaseSession, resetInteractionsForTest, type SessionVirtualElement } from './interactions';
import type { TwinHandle, TwinReferences } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const spies = vi.hoisted(() => ({
  offset: vi.fn(),
  flip: vi.fn(),
  shift: vi.fn(),
  limitShift: vi.fn(),
  size: vi.fn(),
  autoUpdate: vi.fn(),
  autoUpdateCleanup: vi.fn(),
  computePosition: vi.fn(),
}));

vi.mock('./floating-ui', async () => {
  const actual = await vi.importActual<typeof import('./floating-ui')>('./floating-ui');
  return {
    ...actual,
    offset: ((...args: Parameters<typeof actual.offset>) => {
      spies.offset(...args);
      return actual.offset(...args);
    }) satisfies typeof actual.offset,
    flip: ((...args: Parameters<typeof actual.flip>) => {
      spies.flip(...args);
      return actual.flip(...args);
    }) satisfies typeof actual.flip,
    shift: ((...args: Parameters<typeof actual.shift>) => {
      spies.shift(...args);
      return actual.shift(...args);
    }) satisfies typeof actual.shift,
    limitShift: ((...args: Parameters<typeof actual.limitShift>) => {
      spies.limitShift(...args);
      return actual.limitShift(...args);
    }) satisfies typeof actual.limitShift,
    size: ((...args: Parameters<typeof actual.size>) => {
      spies.size(...args);
      return actual.size(...args);
    }) satisfies typeof actual.size,
    autoUpdate: ((...args: Parameters<typeof actual.autoUpdate>) => {
      spies.autoUpdate(...args);
      const cleanup = actual.autoUpdate(...args);
      return () => {
        spies.autoUpdateCleanup();
        cleanup();
      };
    }) satisfies typeof actual.autoUpdate,
    computePosition: ((...args: Parameters<typeof actual.computePosition>) => {
      spies.computePosition(...args);
      return actual.computePosition(...args);
    }) satisfies typeof actual.computePosition,
  };
});

const handles: TwinHandle[] = [];

beforeEach(() => {
  for (const spy of Object.values(spies)) {
    spy.mockClear();
  }
});

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

/** Resolves after the engine's promise chain and the `MutationObserver` microtasks queued so far have run. */
async function settle(): Promise<void> {
  for (let i = 0; i < 10; i++) {
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  }
}

function domRect(x: number, y: number, width: number, height: number): DOMRect {
  return { x, y, width, height, top: y, left: x, right: x + width, bottom: y + height, toJSON: () => ({}) } as DOMRect;
}

function stubRect(element: Element, x: number, y: number, width: number, height: number): void {
  element.getBoundingClientRect = () => domRect(x, y, width, height);
  element.getClientRects = () => [domRect(x, y, width, height)] as unknown as DOMRectList;
}

/** A 1000×800 layout viewport (jsdom's is 0×0, which would clip every anchor). */
function stubViewport(): void {
  Object.defineProperty(document.documentElement, 'clientWidth', { configurable: true, value: 1000 });
  Object.defineProperty(document.documentElement, 'clientHeight', { configurable: true, value: 800 });
}

interface Setup {
  fake: FakeTwin;
  trigger: HTMLElement;
  positioner: HTMLElement;
  handle: TwinHandle;
  update(overrides: Partial<PositioningOptions>, references?: TwinReferences): void;
}

function options(overrides: Partial<PositioningOptions> = {}): PositioningOptions {
  return { triggerId: 'trigger', ...overrides };
}

function setup(
  overrides: Partial<PositioningOptions> = {},
  init: { synchronous?: boolean; hidden?: boolean; open?: boolean; references?: TwinReferences } = {},
): Setup {
  const trigger = document.createElement('button');
  trigger.id = 'trigger';
  stubRect(trigger, 100, 100, 72, 36);
  const positioner = document.createElement('div');
  positioner.setAttribute('role', 'presentation');
  if (init.open ?? true) {
    positioner.setAttribute('data-open', '');
  }
  if (init.hidden) {
    positioner.hidden = true;
  }
  document.body.append(trigger, positioner);
  const fake = createFakeTwin({ synchronous: init.synchronous ?? false });
  let current = options(overrides);
  const handle = attach(positioner, JSON.stringify(current), fake.twin, init.references);
  handles.push(handle);
  return {
    fake,
    trigger,
    positioner,
    handle,
    update(next, references) {
      current = { ...current, ...next };
      handle.update(JSON.stringify(current), references);
    },
  };
}

/** The names of the middleware of the last `computePosition` call, in order. */
function lastPipeline(): string[] {
  const config = spies.computePosition.mock.lastCall?.[2] as { middleware: Array<{ name: string } | null | false> };
  return config.middleware.filter((entry): entry is { name: string } => Boolean(entry)).map((entry) => entry.name);
}

// ---------------------------------------------------------------------------------------------------------------
// utils/hideMiddleware.test.ts, verbatim (FLT-ALG-10, FLT-POS-09; FLT-ALG-03 through the engine's hide())
// ---------------------------------------------------------------------------------------------------------------

const floatingRect: Rect = { x: 0, y: 0, width: 5, height: 5 };

function createState(reference: Rect, overflow: SideObject): MiddlewareState {
  const referenceElement = document.createElement('button');
  const floatingElement = document.createElement('div');

  return {
    x: 0,
    y: 0,
    initialPlacement: 'bottom',
    placement: 'bottom',
    strategy: 'absolute',
    middlewareData: {},
    elements: {
      reference: referenceElement,
      floating: floatingElement,
    },
    rects: {
      reference,
      floating: floatingRect,
    },
    platform: {
      detectOverflow: vi.fn(async () => overflow),
      getElementRects: vi.fn(async () => ({ reference, floating: floatingRect })),
      getClippingRect: vi.fn(async () => reference),
      getDimensions: vi.fn(async () => ({ width: 5, height: 5 })),
    },
  } as MiddlewareState;
}

describe('hideMiddleware', () => {
  it.each([
    [{ top: 19, right: 9, bottom: 19, left: 9 }, false],
    [{ top: 20, right: 9, bottom: 19, left: 9 }, true],
    [{ top: 19, right: 10, bottom: 19, left: 9 }, true],
    [{ top: 19, right: 9, bottom: 20, left: 9 }, true],
    [{ top: 19, right: 9, bottom: 19, left: 10 }, true],
  ] satisfies Array<[SideObject, boolean]>)('matches Floating UI hide() for reference overflow %#', async (overflow, expected) => {
    const state = createState({ x: 1, y: 2, width: 10, height: 20 }, overflow);

    const nativeResult = await nativeHide().fn(state);
    const result = await hide.fn(state);

    expect(result.data?.referenceHidden).toBe(nativeResult.data?.referenceHidden);
    expect(result.data?.referenceHidden).toBe(expected);
    expect(state.platform.detectOverflow).toHaveBeenCalledTimes(2);
    expect(state.platform.detectOverflow).toHaveBeenNthCalledWith(1, state, {
      elementContext: 'reference',
    });
    expect(state.platform.detectOverflow).toHaveBeenNthCalledWith(2, state, {
      elementContext: 'reference',
    });
  });

  it('treats an all-zero reference rect as hidden', async () => {
    const state = createState({ x: 0, y: 0, width: 0, height: 0 }, { top: -1, right: -1, bottom: -1, left: -1 });

    const nativeResult = await nativeHide().fn(state);
    const result = await hide.fn(state);

    expect(nativeResult.data?.referenceHidden).toBe(false);
    expect(result.data?.referenceHidden).toBe(true);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// internals/useAnchorPositioning.test.tsx, the jsdom titles verbatim (FLT-API-11, FLT-API-17, FLT-POS-05, FLT-ALG-14)
// ---------------------------------------------------------------------------------------------------------------

describe('useAnchorPositioning', () => {
  function TestUseAnchorPositioning(props: { shift?: PositioningOptions['shift'] }) {
    // Base UI's component: an anchor through a handle, the bottom/center defaults and fallbackAxisSide 'none'.
    const anchor = document.createElement('div');
    anchor.id = 'anchor';
    document.body.append(anchor);
    return setup({ anchorId: 'anchor', collisionAvoidance: { fallbackAxisSide: 'none' }, shift: props.shift ?? null });
  }

  it('uses the visual viewport for shift by default', async () => {
    TestUseAnchorPositioning({});
    await settle();

    expect(spies.shift).toHaveBeenCalled();
    expect(spies.shift.mock.calls[0]?.[0].rootBoundary).toBe(undefined);
  });

  it.each([
    { shift: { rootBoundary: 'layoutViewport' } as const, crossAxis: false },
    { shift: { crossAxis: true, rootBoundary: 'layoutViewport' } as const, crossAxis: true },
  ])('uses the configured shift options', async ({ shift, crossAxis }) => {
    TestUseAnchorPositioning({ shift });
    await settle();

    expect(spies.shift.mock.calls[0]?.[0].rootBoundary).toBe('layoutViewport');
    expect(spies.shift.mock.calls[0]?.[0].crossAxis).toBe(crossAxis);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The pipeline assembly (FLT-POS-02..05, FLT-ALG-14, FLT-API-09)
// ---------------------------------------------------------------------------------------------------------------

describe('positioning module: the pipeline', () => {
  it('assembles offset, shift and flip, size, arrow, transformOrigin and hide in order, adaptiveOrigin last when on (FLT-POS-02)', async () => {
    const { update } = setup();
    await settle();
    expect(lastPipeline()).toEqual(['offset', 'shift', 'flip', 'size', 'arrow', 'transformOrigin', 'hide']);

    update({ adaptiveOrigin: true });
    await settle();
    expect(lastPipeline()).toEqual(['offset', 'shift', 'flip', 'size', 'arrow', 'transformOrigin', 'hide', 'adaptiveOrigin']);
  });

  it('runs flip before shift for an aligned placement unless an axis avoids collisions by shifting (FLT-POS-03)', async () => {
    const { update } = setup({ align: 'start' });
    await settle();
    expect(lastPipeline().slice(1, 3)).toEqual(['flip', 'shift']);

    update({ collisionAvoidance: { align: 'shift' } });
    await settle();
    expect(lastPipeline().slice(1, 3)).toEqual(['shift', 'flip']);

    update({ collisionAvoidance: { side: 'shift' } });
    await settle();
    expect(lastPipeline().slice(1, 3)).toEqual(['shift', 'flip']);
  });

  it('configures flip with the preferred-side bias and omits it when the side axis avoids nothing (FLT-POS-04)', async () => {
    const { update } = setup({ side: 'top', collisionPadding: 12, collisionAvoidance: { fallbackAxisSide: 'start' } });
    await settle();

    expect(spies.flip.mock.lastCall?.[0]).toMatchObject({
      boundary: 'clippingAncestors',
      padding: { top: 13, right: 13, bottom: 14, left: 13 },
      mainAxis: true,
      crossAxis: 'alignment',
      fallbackAxisSideDirection: 'start',
    });

    update({ side: 'inline-end', collisionAvoidance: { align: 'none' } });
    await settle();
    expect(spies.flip.mock.lastCall?.[0]).toMatchObject({ padding: { top: 13, right: 13, bottom: 13, left: 13 }, crossAxis: false, fallbackAxisSideDirection: 'end' });

    update({ collisionAvoidance: { side: 'none' } });
    await settle();
    expect(lastPipeline()).not.toContain('flip');
    expect(lastPipeline()).toContain('shift');
  });

  it('configures shift with the unbiased padding and a limiter, and omits it when no axis shifts (FLT-POS-05, FLT-POS-14)', async () => {
    const { update } = setup({ collisionPaddingSides: { top: 1, right: 2, bottom: 3, left: 4 } });
    await settle();

    expect(spies.shift.mock.lastCall?.[0]).toMatchObject({
      boundary: 'clippingAncestors',
      padding: { top: 1, right: 2, bottom: 3, left: 4 },
      mainAxis: true,
      crossAxis: false,
    });
    expect(spies.shift.mock.lastCall?.[0].limiter).toBeDefined();
    expect(spies.limitShift).toHaveBeenCalled();

    update({ sticky: true });
    await settle();
    expect(spies.shift.mock.lastCall?.[0]).toMatchObject({ crossAxis: true, limiter: undefined });

    update({ sticky: false, collisionAvoidance: { align: 'none' } });
    await settle();
    expect(lastPipeline()).not.toContain('shift');
    expect(lastPipeline()).toContain('flip');
  });

  it('normalizes the collision padding: a number for every side, missing sides as zero (FLT-API-08)', async () => {
    const { update } = setup({ collisionPadding: 7 });
    await settle();
    expect(spies.size.mock.lastCall?.[0].padding).toEqual({ top: 7, right: 7, bottom: 7, left: 7 });

    update({ collisionPadding: null, collisionPaddingSides: { left: 3 } });
    await settle();
    expect(spies.size.mock.lastCall?.[0].padding).toEqual({ top: 0, right: 0, bottom: 0, left: 3 });

    update({ collisionPaddingSides: null });
    await settle();
    expect(spies.size.mock.lastCall?.[0].padding).toEqual({ top: 5, right: 5, bottom: 5, left: 5 });
  });

  it('passes clipping-ancestors to the engine as clippingAncestors and a rect unchanged (FLT-API-09)', async () => {
    const { update } = setup({ collisionBoundary: 'clipping-ancestors' });
    await settle();
    expect(spies.size.mock.lastCall?.[0].boundary).toBe('clippingAncestors');

    update({ collisionBoundary: null, boundaryRect: { x: 1, y: 2, width: 300, height: 200 } });
    await settle();
    expect(spies.size.mock.lastCall?.[0].boundary).toEqual({ x: 1, y: 2, width: 300, height: 200 });
    expect(spies.flip.mock.lastCall?.[0].boundary).toEqual({ x: 1, y: 2, width: 300, height: 200 });
  });

  it('passes the boundary elements the references carry to the engine, and rebuilds when an element at the same index changes (FLT-API-09)', async () => {
    const first = document.createElement('section');
    const second = document.createElement('section');
    document.body.append(first, second);
    const { update } = setup({ boundaryRefs: [0] }, { references: [first] });
    await settle();
    expect(spies.size.mock.lastCall?.[0].boundary).toEqual([first]);
    expect(spies.size.mock.lastCall?.[0].boundary[0]).toBe(first);
    expect(spies.flip.mock.lastCall?.[0].boundary[0]).toBe(first);

    update({ boundaryRefs: [1, 2], anchorRef: 0 }, [first, first, second]);
    await settle();
    const list = spies.size.mock.lastCall?.[0].boundary as Element[];
    expect(list).toHaveLength(2);
    expect(list[0]).toBe(first);
    expect(list[1]).toBe(second);

    const built = spies.offset.mock.calls.length;
    update({ boundaryRefs: [1, 2], anchorRef: 0 }, [first, first, second]);
    await settle();
    expect(spies.offset.mock.calls.length).toBe(built);

    update({ boundaryRefs: [1, 2], anchorRef: 0 }, [first, second, first]);
    await settle();
    expect(spies.offset.mock.calls.length).toBe(built + 1);
    expect((spies.size.mock.lastCall?.[0].boundary as Element[])[0]).toBe(second);
  });

  it('configures the arrow middleware with the Arrow and its padding, or a detached stand-in with none (FLT-POS-08, FLT-DOM-10)', async () => {
    const arrowElement = document.createElement('div');
    arrowElement.id = 'arrow';
    const { positioner, update } = setup({ arrowPadding: 12 });
    positioner.append(arrowElement);
    await settle();

    const arrowOptions = () => {
      const config = spies.computePosition.mock.lastCall?.[2] as { middleware: Array<{ name: string; options?: unknown } | null> };
      const middleware = config.middleware.find((entry) => entry?.name === 'arrow')!;
      return (middleware.options as (state: MiddlewareState) => { element: Element; padding: number })(middlewareState({ elements: { reference: document.body, floating: positioner } }));
    };
    const standIn = arrowOptions();
    expect(standIn.element).not.toBe(arrowElement);
    expect(standIn.element.isConnected).toBe(false);
    expect(standIn.element.ownerDocument).toBe(positioner.ownerDocument);
    expect(standIn.padding).toBe(0);

    update({ arrowId: 'arrow' });
    await settle();
    expect(arrowOptions()).toEqual({ element: arrowElement, padding: 12 });
  });

  it('rebuilds the pipeline only when one of its inputs changes (FLT-ALG-14, FLT-POS-17)', async () => {
    const { update } = setup({ side: 'bottom' });
    await settle();
    const built = spies.offset.mock.calls.length;
    const tracked = spies.autoUpdate.mock.calls.length;

    update({});
    update({ style: 'color: red' });
    await settle();
    expect(spies.offset.mock.calls.length).toBe(built);
    expect(spies.autoUpdate.mock.calls.length).toBe(tracked);

    update({ side: 'top' });
    await settle();
    expect(spies.offset.mock.calls.length).toBe(built + 1);
    expect(spies.autoUpdate.mock.calls.length).toBe(tracked + 1);
    expect(spies.computePosition.mock.lastCall?.[2].placement).toBe('top');

    update({ direction: 'rtl' });
    await settle();
    expect(spies.offset.mock.calls.length).toBe(built + 2);
  });

  it('requests the physical placement of a logical side with its alignment (FLT-POS-01, FLT-I18N-01)', async () => {
    const { update } = setup({ side: 'inline-end', align: 'start' });
    await settle();
    expect(spies.computePosition.mock.lastCall?.[2].placement).toBe('right-start');

    update({ direction: 'rtl' });
    await settle();
    expect(spies.computePosition.mock.lastCall?.[2].placement).toBe('left-start');

    update({ side: 'inline-start', align: 'center' });
    await settle();
    expect(spies.computePosition.mock.lastCall?.[2].placement).toBe('right');

    update({ positionMethod: 'fixed' });
    await settle();
    expect(spies.computePosition.mock.lastCall?.[2].strategy).toBe('fixed');
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The anchor and the tracking gate (FLT-STA-07, FLT-POS-13, FLT-API-11, FLT-STA-09, FLT-POS-11, FLT-POS-12)
// ---------------------------------------------------------------------------------------------------------------

describe('positioning module: the anchor', () => {
  it('positions against the active trigger, or a custom anchor wrapped so that its own rect is measured (FLT-STA-07, FLT-POS-13)', async () => {
    const { trigger, update } = setup();
    await settle();
    expect(spies.computePosition.mock.lastCall?.[0]).toBe(trigger);

    const anchor = document.createElement('span');
    anchor.id = 'anchor';
    stubRect(anchor, 300, 40, 20, 10);
    document.body.append(anchor);
    update({ anchorId: 'anchor' });
    await settle();

    const reference = spies.computePosition.mock.lastCall?.[0] as { contextElement: Element; getBoundingClientRect(): DOMRect; getClientRects(): DOMRectList };
    expect(reference).not.toBe(anchor);
    expect(reference.contextElement).toBe(anchor);
    expect(reference.getBoundingClientRect()).toMatchObject({ x: 300, y: 40, width: 20, height: 10 });
    expect(reference.getClientRects()).toHaveLength(1);
  });

  it('positions against a virtual anchor as given, re-sent on change (FLT-STA-07)', async () => {
    const { update } = setup({ virtualAnchor: { x: 10, y: 20, width: 30, height: 40 } });
    await settle();

    const reference = spies.computePosition.mock.lastCall?.[0] as { contextElement?: Element; getBoundingClientRect(): DOMRect };
    expect(reference.contextElement).toBeUndefined();
    expect(reference.getBoundingClientRect()).toMatchObject({ x: 10, y: 20, width: 30, height: 40, top: 20, left: 10, right: 40, bottom: 60 });

    update({ virtualAnchor: { x: 11, y: 20, width: 30, height: 40 } });
    await settle();
    expect((spies.computePosition.mock.lastCall?.[0] as { getBoundingClientRect(): DOMRect }).getBoundingClientRect()).toMatchObject({ x: 11 });
  });

  it('registers a changed anchor only when it differs from the registered one (FLT-API-11)', async () => {
    const first = document.createElement('span');
    first.id = 'first';
    const second = document.createElement('span');
    second.id = 'second';
    document.body.append(first, second);
    const { update } = setup({ anchorId: 'first' });
    await settle();
    const subscriptions = spies.autoUpdate.mock.calls.length;

    update({ anchorId: 'first', style: 'color: blue' });
    await settle();
    expect(spies.autoUpdate.mock.calls.length).toBe(subscriptions);

    update({ anchorId: 'second' });
    await settle();
    expect(spies.autoUpdate.mock.calls.length).toBe(subscriptions + 1);
    expect((spies.autoUpdate.mock.lastCall?.[0] as { contextElement: Element }).contextElement).toBe(second);
  });

  it('positions against the anchor element the references carry, wrapped so that its own rect is measured, and registers the one a later update carries at the same index (FLT-API-11)', async () => {
    const first = document.createElement('span');
    stubRect(first, 300, 40, 20, 10);
    const second = document.createElement('span');
    stubRect(second, 500, 60, 30, 10);
    document.body.append(first, second);
    const { update } = setup({ anchorRef: 0 }, { references: [first] });
    await settle();

    const reference = spies.computePosition.mock.lastCall?.[0] as { contextElement: Element; getBoundingClientRect(): DOMRect };
    expect(reference).not.toBe(first);
    expect(reference.contextElement).toBe(first);
    expect(reference.getBoundingClientRect()).toMatchObject({ x: 300, y: 40, width: 20, height: 10 });
    const subscriptions = spies.autoUpdate.mock.calls.length;

    update({ style: 'color: blue' }, [first]);
    await settle();
    expect(spies.autoUpdate.mock.calls.length).toBe(subscriptions);

    update({}, [second]);
    await settle();
    expect(spies.autoUpdate.mock.calls.length).toBe(subscriptions + 1);
    expect((spies.autoUpdate.mock.lastCall?.[0] as { contextElement: Element }).contextElement).toBe(second);
    expect((spies.computePosition.mock.lastCall?.[0] as { getBoundingClientRect(): DOMRect }).getBoundingClientRect()).toMatchObject({ x: 500 });
  });

  it('tracks the anchor while mounted with the configured options, and a kept-mounted popup only while mounted (FLT-STA-09, FLT-POS-11, FLT-POS-12)', async () => {
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe(): void {}
        unobserve(): void {}
        disconnect(): void {}
      },
    );
    const { positioner, update } = setup({ keepMounted: true }, { hidden: true, open: false });
    await settle();
    expect(spies.autoUpdate).not.toHaveBeenCalled();
    expect(spies.computePosition).not.toHaveBeenCalled();

    positioner.hidden = false;
    positioner.setAttribute('data-open', '');
    await settle();
    expect(spies.autoUpdate).toHaveBeenCalledTimes(1);
    expect(spies.autoUpdate.mock.lastCall?.[3]).toEqual({ ancestorScroll: true, elementResize: true, layoutShift: false });
    expect(spies.computePosition).toHaveBeenCalled();

    positioner.removeAttribute('data-open');
    positioner.hidden = true;
    await settle();
    expect(spies.autoUpdateCleanup).toHaveBeenCalledTimes(1);

    positioner.hidden = false;
    update({ disableAnchorTracking: true });
    await settle();
    expect(spies.autoUpdate.mock.lastCall?.[3]).toEqual({ ancestorScroll: false, elementResize: false, layoutShift: false });
  });

  it('re-resolves a trigger or an arrow that a render replaced, by id, on the next pass', async () => {
    stubViewport();
    const arrowElement = document.createElement('div');
    arrowElement.id = 'arrow';
    const { trigger, positioner } = setup({ arrowId: 'arrow' });
    positioner.append(arrowElement);
    window.dispatchEvent(new Event('resize'));
    await settle();

    const replacement = document.createElement('button');
    replacement.id = 'trigger';
    stubRect(replacement, 400, 100, 72, 36);
    trigger.replaceWith(replacement);
    const newArrow = document.createElement('div');
    newArrow.id = 'arrow';
    arrowElement.replaceWith(newArrow);
    window.dispatchEvent(new Event('resize'));
    await settle();

    expect(spies.computePosition.mock.lastCall?.[0]).toBe(replacement);
    expect(spies.autoUpdate.mock.lastCall?.[0]).toBe(replacement);
    expect(newArrow.getAttribute('data-side')).toBe('bottom');
    expect(newArrow.style.getPropertyValue('position')).toBe('absolute');

    // A replaced Arrow alone keeps the tracking and still gets this pass's writes.
    const subscriptions = spies.autoUpdate.mock.calls.length;
    const third = document.createElement('div');
    third.id = 'arrow';
    newArrow.replaceWith(third);
    window.dispatchEvent(new Event('resize'));
    await settle();
    expect(spies.autoUpdate.mock.calls.length).toBe(subscriptions);
    expect(third.getAttribute('data-side')).toBe('bottom');
    expect(third.style.getPropertyValue('position')).toBe('absolute');
  });

  it('starts no positioning pass while the anchor does not resolve, and starts when it does (FLT-STA-09)', async () => {
    const { update } = setup({ triggerId: 'missing' });
    await settle();
    expect(spies.computePosition).not.toHaveBeenCalled();

    update({ triggerId: 'trigger' });
    await settle();
    expect(spies.computePosition).toHaveBeenCalled();
  });
});

describe('positioning module: the session (FLT-STA-08)', () => {
  afterEach(() => {
    resetInteractionsForTest();
  });

  // The session is the real `interactions` one; its position reference is written as the client point writes it.
  function setPositionReference(session: object, reference: SessionVirtualElement | null): void {
    (session as { positionReference: SessionVirtualElement | null }).positionReference = reference;
  }

  it('takes the session of its token, publishes each committed placement to it, positions against its position reference while one is set, and releases it at dispose (FLT-STA-08)', async () => {
    const session = acquireSession('base-ui-1');
    const setPlacement = vi.spyOn(session, 'setPlacement');
    const subscribe = vi.spyOn(session, 'subscribePositionReference');
    const { trigger, handle, update } = setup({ sessionToken: 'base-ui-1', side: 'top' });
    await settle();
    expect(setPlacement).toHaveBeenLastCalledWith('top');
    expect(spies.computePosition.mock.lastCall?.[0]).toBe(trigger);
    expect(subscribe).toHaveBeenCalledTimes(1);
    const notify = subscribe.mock.calls[0]![0];

    const point: SessionVirtualElement = {
      getBoundingClientRect: () => ({ x: 400, y: 300, width: 0, height: 0, top: 300, left: 400, right: 400, bottom: 300 }),
    };
    setPositionReference(session, point);
    notify();
    await settle();
    expect(spies.computePosition.mock.lastCall?.[0]).toBe(point);

    update({ side: 'bottom' });
    await settle();
    expect(setPlacement).toHaveBeenLastCalledWith('bottom');

    setPositionReference(session, null);
    notify();
    await settle();
    expect(spies.computePosition.mock.lastCall?.[0]).toBe(trigger);

    // A new token moves the handle to that session; the previous one keeps only the test's reference.
    update({ sessionToken: 'base-ui-2' });
    await settle();
    const next = getSession('base-ui-2');
    expect(next).toBeDefined();
    releaseSession(session);
    expect(getSession('base-ui-1')).toBeUndefined();

    handle.dispose();
    expect(getSession('base-ui-2')).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The Base UI middleware over synthetic states (FLT-ALG-09, FLT-ALG-11, FLT-ALG-12, FLT-I18N-02/03)
// ---------------------------------------------------------------------------------------------------------------

function middlewareState(overrides: Partial<MiddlewareState> & { reference?: Rect; floating?: Rect; isRTL?: boolean; dimensions?: { width: number; height: number } }): MiddlewareState {
  const reference = overrides.reference ?? { x: 100, y: 100, width: 72, height: 36 };
  const floating = overrides.floating ?? { x: 0, y: 0, width: 52, height: 24 };
  const floatingElement = document.createElement('div');
  return {
    x: 110,
    y: 136,
    initialPlacement: 'bottom',
    placement: 'bottom',
    strategy: 'absolute',
    middlewareData: {},
    elements: { reference: document.createElement('button'), floating: floatingElement },
    rects: { reference, floating },
    platform: {
      getElementRects: vi.fn(async () => ({ reference, floating })),
      getClippingRect: vi.fn(async () => reference),
      getDimensions: vi.fn(async () => overrides.dimensions ?? { width: 10, height: 10 }),
      // The DOM platform's isRTL is synchronous; Base UI compares its result with `=== true`.
      isRTL: vi.fn(() => overrides.isRTL ?? false),
      getOffsetParent: vi.fn(async () => window),
      isElement: vi.fn(async (value: unknown) => value instanceof Element),
    },
    ...overrides,
  } as MiddlewareState;
}

describe('positioning module: the arrow fork (FLT-ALG-09)', () => {
  it('centers the arrow on the anchor, measured against the floating element, and returns nothing without an element', async () => {
    const element = document.createElement('div');
    const result = await arrow({ element, padding: 5 }).fn(middlewareState({ x: 110, y: 136 }));

    // center = 52/2 - 10/2 + ((72 + 100 - 110 - 52)/2 - (110 - 100)/2) = 21 + 0
    expect(result).toEqual({ x: 110, data: { x: 21, centerOffset: 0 }, reset: false });
    expect(await arrow({ element: null }).fn(middlewareState({}))).toEqual({});
  });

  it('clamps the arrow to the padding and reports the offset from the center', async () => {
    const element = document.createElement('div');
    // The popup sits 40px to the right of a narrow anchor: the arrow cannot reach the anchor's center.
    const result = await arrow({ element, padding: 5 }).fn(middlewareState({ x: 150, reference: { x: 100, y: 100, width: 20, height: 36 } }));

    expect(result.data).toMatchObject({ x: 5 });
    expect((result.data as { centerOffset: number }).centerOffset).toBeLessThan(0);
  });

  it('nudges an aligned popup once so that the arrow still points at a small anchor', async () => {
    const element = document.createElement('div');
    const state = middlewareState({ x: 100, placement: 'bottom-start', reference: { x: 100, y: 100, width: 8, height: 36 }, dimensions: { width: 10, height: 10 } });
    const result = await arrow({ element, padding: 5 }).fn(state);

    expect(result.reset).toBe(true);
    expect((result.data as { alignmentOffset: number }).alignmentOffset).toBeLessThan(0);
  });
});

describe('positioning module: the transform origin (FLT-ALG-11, FLT-I18N-03)', () => {
  async function run(state: MiddlewareState, config: Partial<Parameters<typeof transformOrigin>[0]> = {}): Promise<string> {
    let written = '';
    await transformOrigin({
      arrow: () => null,
      sideOffset: () => 0,
      crossAxisShiftEnabled: false,
      write: (value) => {
        written = value;
      },
      ...config,
    }).fn(state);
    return written;
  }

  it('points to the arrow, real or stand-in, for a centered popup, and to the anchor-facing edge on the side axis', async () => {
    expect(await run(middlewareState({ middlewareData: { arrow: { x: 21, centerOffset: 0 } } }))).toBe('21px 0px');
    expect(await run(middlewareState({ placement: 'top', middlewareData: { arrow: { x: 21, centerOffset: 0 } } }), { sideOffset: () => 8 })).toBe('21px calc(100% + 8px)');
  });

  it('grows an aligned arrowless popup from its aligned edge, by the floating element direction', async () => {
    expect(await run(middlewareState({ placement: 'bottom-start' }))).toBe('0% 0px');
    expect(await run(middlewareState({ placement: 'bottom-end' }))).toBe('100% 0px');
    expect(await run(middlewareState({ placement: 'bottom-start', isRTL: true }))).toBe('100% 0px');
    expect(await run(middlewareState({ placement: 'right-start', isRTL: true }), { sideOffset: () => 4 })).toBe('-4px 0%');
  });

  it('points to the anchor center on the side axis when a cross-axis shift moves the popup over the anchor', async () => {
    const state = middlewareState({ y: 120, placement: 'bottom', middlewareData: { arrow: { x: 21, centerOffset: 0 }, shift: { x: 0, y: -16, enabled: { x: false, y: true } } } });
    // The side origin is the anchor's center relative to the popup: 100 + 36 / 2 - 120.
    expect(await run(state, { crossAxisShiftEnabled: true })).toBe('21px -2px');
    expect(await run(state, { crossAxisShiftEnabled: false })).toBe('21px 0px');
  });
});

describe('positioning module: the adaptive origin (FLT-ALG-12, FLT-ANI-03)', () => {
  it('keeps the plain coordinates while the floating element has no transition', async () => {
    const state = middlewareState({ placement: 'left' });
    expect(await adaptiveOrigin.fn(state)).toEqual({ x: 110, y: 136, data: { sideX: 'left', sideY: 'top' } });
  });

  it('measures a left or top placement from the opposite edge while the floating element has a transition', async () => {
    Object.defineProperty(document.documentElement, 'clientWidth', { configurable: true, value: 1000 });
    Object.defineProperty(document.documentElement, 'clientHeight', { configurable: true, value: 800 });
    const left = middlewareState({ placement: 'left', x: 40, y: 100 });
    left.elements.floating.style.transitionDuration = '0.2s';
    document.body.append(left.elements.floating);
    expect(await adaptiveOrigin.fn(left)).toEqual({ x: 1000 - (40 + 52), y: 100, data: { sideX: 'right', sideY: 'top' } });

    const top = middlewareState({ placement: 'top', x: 40, y: 60 });
    top.elements.floating.style.transitionDuration = '0.2s';
    document.body.append(top.elements.floating);
    expect(await adaptiveOrigin.fn(top)).toEqual({ x: 40, y: 800 - (60 + 24), data: { sideX: 'left', sideY: 'bottom' } });
  });
});

describe('positioning module: logical sides (FLT-I18N-02)', () => {
  it.each([
    ['inline-start', 'right', false, 'inline-end'],
    ['inline-start', 'left', false, 'inline-start'],
    ['inline-start', 'right', true, 'inline-start'],
    ['inline-end', 'left', true, 'inline-end'],
    ['left', 'right', false, 'right'],
    ['inline-end', 'top', false, 'top'],
  ] as const)('reports %s rendered %s (rtl: %s) as %s', (side, rendered, rtl, expected) => {
    expect(getLogicalSide(side, rendered, rtl)).toBe(expected);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The report and the handle (one message per change, ADR 04 §3; idempotent attach and dispose, ADR 06 rule 2)
// ---------------------------------------------------------------------------------------------------------------

describe.each([false, true])('positioning module: the report (synchronous: %s)', (synchronous) => {
  it('reports the rendered side, align, anchorHidden and uncentered once per change, never once per pass (FLT-POS-09, FLT-POS-10)', async () => {
    stubViewport();
    const { fake, trigger, positioner } = setup({ side: 'bottom' }, { synchronous });
    await settle();
    const first = fake.reported<{ side: string; align: string; anchorHidden: boolean; uncentered: boolean }>('OnPlacementChange');
    expect(first).toHaveLength(1);
    expect(first[0]).toMatchObject({ side: 'bottom', align: 'center', anchorHidden: false });

    window.dispatchEvent(new Event('resize'));
    window.dispatchEvent(new Event('resize'));
    await settle();
    expect(spies.computePosition.mock.calls.length).toBeGreaterThan(1);
    expect(fake.reported('OnPlacementChange')).toHaveLength(1);

    // A moved anchor changes the pass's coordinates, not the mirrored values.
    const before = positioner.style.transform;
    stubRect(trigger, 140, 100, 72, 36);
    window.dispatchEvent(new Event('resize'));
    await settle();
    expect(positioner.style.transform).not.toBe(before);
    expect(fake.reported('OnPlacementChange')).toHaveLength(1);

    stubRect(trigger, 0, 0, 0, 0);
    window.dispatchEvent(new Event('resize'));
    await settle();
    expect(fake.reported('OnPlacementChange')).toHaveLength(2);
    expect(fake.reported<{ anchorHidden: boolean }>('OnPlacementChange')[1]).toMatchObject({ anchorHidden: true });
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The offsets (FLT-API-12, FLT-POS-06): a number, or the declarative form of Base UI's offset function (design point 2
// of the positioning track, settled by the orchestrator 2026-10-27)
// ---------------------------------------------------------------------------------------------------------------

describe('positioning module: the offsets', () => {
  const data = (side: OffsetData['side'], align: OffsetData['align']): OffsetData => ({
    side,
    align,
    anchor: { width: 72, height: 36 },
    positioner: { width: 52, height: 24 },
  });

  it('evaluates a number, or a constant plus the anchor and positioner terms', () => {
    expect(evaluateOffset(7, data('bottom', 'center'))).toBe(7);
    expect(evaluateOffset(undefined, data('bottom', 'center'))).toBe(0);
    expect(evaluateOffset({ anchorWidth: 1, positionerWidth: 1 }, data('bottom', 'center'))).toBe(124);
    expect(evaluateOffset({ constant: 2, anchorHeight: 0.5, positionerHeight: -1 }, data('top', 'start'))).toBe(2 + 18 - 24);
  });

  it('applies the first override whose side and align match the evaluated placement', () => {
    const form = {
      constant: 1,
      overrides: [
        { side: 'right' as const, align: 'end' as const, constant: 30 },
        { side: 'right' as const, constant: 20 },
        { align: 'end' as const, positionerWidth: 1 },
      ],
    };
    expect(evaluateOffset(form, data('right', 'end'))).toBe(30);
    expect(evaluateOffset(form, data('right', 'start'))).toBe(20);
    expect(evaluateOffset(form, data('left', 'end'))).toBe(52);
    expect(evaluateOffset(form, data('inline-start', 'center'))).toBe(1);
  });

  it('feeds the offset middleware the logical side and align of the placement each pass evaluates (FLT-API-12, FLT-POS-06)', async () => {
    setup({
      side: 'inline-start',
      sideOffsetFunction: { overrides: [{ side: 'inline-end', constant: 9 }] },
      alignOffsetFunction: { overrides: [{ align: 'end', constant: 4 }] },
    });
    await settle();

    const produce = spies.offset.mock.lastCall?.[0] as (state: MiddlewareState) => { mainAxis: number; crossAxis: number; alignmentAxis: number };
    expect(produce(middlewareState({ placement: 'right-end' }))).toEqual({ mainAxis: 9, crossAxis: 4, alignmentAxis: 4 });
    expect(produce(middlewareState({ placement: 'left' }))).toEqual({ mainAxis: 0, crossAxis: 0, alignmentAxis: 0 });
  });

  it('rebuilds the pipeline when an offset form changes (FLT-POS-17)', async () => {
    const { update } = setup({ sideOffsetFunction: { constant: 1 } });
    await settle();
    const built = spies.offset.mock.calls.length;

    update({ sideOffsetFunction: { constant: 1 } });
    await settle();
    expect(spies.offset.mock.calls.length).toBe(built);

    update({ sideOffsetFunction: { constant: 2 } });
    await settle();
    expect(spies.offset.mock.calls.length).toBe(built + 1);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The writes through the element's style writer (FLT-DOM-01..05; ADR 04 Amendments 2026-10-27, Decision 2)
// ---------------------------------------------------------------------------------------------------------------

describe('positioning module: the style writer', () => {
  it('composes the consumer between the positioner styles and the measured variables, and clears its layers on dispose', async () => {
    stubViewport();
    const { positioner, handle } = setup({ style: 'opacity: 0.5; --available-height: 1px; color: red', transitionStatus: 'starting' });
    // Another module writing the same element composes into the same writer.
    styleWriter(positioner).layer('auto-resize').set('--positioner-width', '52px');
    await settle();

    expect(positioner.style.getPropertyValue('opacity')).toBe('0.5');
    expect(positioner.style.getPropertyValue('color')).toBe('red');
    expect(positioner.style.getPropertyValue('--available-height')).toMatch(/^\d+(\.\d+)?px$/);
    expect(positioner.style.getPropertyValue('--available-height')).not.toBe('1px');
    expect(positioner.style.getPropertyValue('--available-width')).toMatch(/px$/);
    expect(positioner.style.getPropertyValue('--anchor-width')).toBe('72px');
    expect(positioner.style.getPropertyValue('--transform-origin')).not.toBe('');
    expect(positioner.style.getPropertyValue('transition')).toBe('none');
    expect(positioner.style.getPropertyValue('--positioner-width')).toBe('52px');

    handle.dispose();
    expect(positioner.style.getPropertyValue('transform')).toBe('');
    expect(positioner.style.getPropertyValue('--anchor-width')).toBe('');
    expect(positioner.style.getPropertyValue('color')).toBe('red');
    expect(positioner.style.getPropertyValue('--positioner-width')).toBe('52px');
  });

  it('writes the arrow styles and attributes under the arrow consumer style from the update that carries its id (FLT-DOM-09, FLT-DOM-10)', async () => {
    stubViewport();
    const arrowElement = document.createElement('div');
    arrowElement.id = 'arrow';
    const { positioner, update } = setup();
    positioner.append(arrowElement);
    await settle();
    expect(arrowElement.style.cssText).toBe('');
    expect(arrowElement.hasAttribute('data-side')).toBe(false);

    update({ arrowId: 'arrow', arrowStyle: 'background: red; position: relative' });
    await settle();
    expect(arrowElement.style.getPropertyValue('background')).toBe('red');
    expect(arrowElement.style.getPropertyValue('position')).toBe('relative');
    expect(arrowElement.style.getPropertyValue('left')).toMatch(/px$/);
    expect(arrowElement.getAttribute('data-side')).toBe('bottom');
    expect(arrowElement.getAttribute('data-align')).toBe('center');
    // jsdom's 0×0 boxes centre the arrow (centerOffset 0), so the arrow is not uncentered.
    expect(arrowElement.hasAttribute('data-uncentered')).toBe(false);
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The exports for the popup module and PreviewCard (design point 3, settled by the orchestrator 2026-10-27)
// ---------------------------------------------------------------------------------------------------------------

describe('positioning module: readPlacement and update', () => {
  it('reads the rendered placement and the positioned flag of an attached Positioner (FLT-DOM-08)', async () => {
    stubViewport();
    const { positioner } = setup({ side: 'inline-end', direction: 'rtl' });
    expect(readPlacement(positioner)).toEqual({ side: 'inline-end', align: 'center', physicalSide: 'left', isPositioned: false });

    await settle();
    expect(readPlacement(positioner)).toMatchObject({ side: 'inline-end', physicalSide: 'left', isPositioned: true });
    expect(readPlacement(document.createElement('div'))).toBeNull();
  });

  it('runs one positioning pass on demand and does nothing for an element without a handle (FLT-POS-16)', async () => {
    const { positioner } = setup();
    await settle();
    const passes = spies.computePosition.mock.calls.length;

    updatePosition(positioner);
    updatePosition(document.createElement('div'));
    await settle();
    expect(spies.computePosition.mock.calls.length).toBe(passes + 1);
  });
});

describe('positioning module: the pre-position style (FLT-DOM-02, FLT-ANI-02)', () => {
  it('keeps a kept-mounted Positioner fixed at the origin and transparent until the pass of each new mount', async () => {
    stubViewport();
    const { positioner } = setup({ keepMounted: true });
    await settle();
    expect(positioner.style.opacity).toBe('');
    expect(positioner.style.transform).toMatch(/^translate\(/);
    expect(positioner.style.position).toBe('absolute');

    positioner.removeAttribute('data-open');
    positioner.hidden = true;
    await settle();
    expect(positioner.style.opacity).toBe('0');
    expect(positioner.style.position).toBe('fixed');
    expect(positioner.style.transform).toBe('');
    expect(positioner.style.top).toBe('0px');
    expect(positioner.style.pointerEvents).toBe('none');
  });
});

describe('positioning module: the attribute writes', () => {
  it('writes data-side, data-align and data-anchor-hidden only when they change', async () => {
    stubViewport();
    const { positioner, update } = setup();
    const records: string[] = [];
    new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        if (mutation.attributeName?.startsWith('data-side') || mutation.attributeName?.startsWith('data-align') || mutation.attributeName === 'data-anchor-hidden') {
          records.push(mutation.attributeName);
        }
      }
    }).observe(positioner, { attributes: true });
    await settle();
    window.dispatchEvent(new Event('resize'));
    window.dispatchEvent(new Event('resize'));
    await settle();

    // Attach wrote the requested placement; passes that render the same placement write nothing.
    expect(positioner.getAttribute('data-side')).toBe('bottom');
    expect(records).toEqual([]);

    update({ side: 'top' });
    await settle();
    expect(positioner.getAttribute('data-side')).toBe('top');
    expect(records).toEqual(['data-side']);
  });
});

describe('positioning module: the handle', () => {
  it('returns the live handle on a repeated attach and disposes idempotently, stopping the tracking and dropping a pass still in flight', async () => {
    stubViewport();
    const { positioner, handle, fake } = setup();
    await settle();
    expect(attach(positioner, JSON.stringify(options()), fake.twin)).toBe(handle);
    expect(positioner.style.transform).toMatch(/^translate\(/);
    const reports = fake.reports.length;

    handle.dispose();
    handle.dispose();
    expect(spies.autoUpdateCleanup).toHaveBeenCalledTimes(1);
    expect(positioner.style.transform).toBe('');

    // A new handle's first pass is in flight when it is disposed: its result is never applied or reported.
    const again = attach(positioner, JSON.stringify(options({ side: 'top' })), fake.twin);
    expect(again).not.toBe(handle);
    again.dispose();
    await settle();
    expect(positioner.style.transform).toBe('');
    expect(fake.reports).toHaveLength(reports);
  });
});

