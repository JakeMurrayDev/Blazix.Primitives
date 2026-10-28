// The `popup` mechanism module (ADR 04 Amendments 2026-10-27, the `popup` row, chosen by the user 2026-10-27; M4 track 5,
// issue 125): the DOM half of Base UI's popup utilities that no other module owns, attached by the parts of every popup
// family. One handle per part, one role per handle (the `interactions` precedent). Design points 1-3 and 5-8 of track 5
// (the placement, the `[data-previous]` container, the physical side, the surface, the anchored lock, the trigger-change
// report, the keydown stop) are built as recommended, settled by the orchestrator 2026-10-28 (ADR 04 Amendments
// 2026-10-28).
// - The Viewport (utils/usePopupViewport.tsx, utils/usePopupAutoResize.ts; PSH-ALG-13..15, PSH-DOM-07..12, PSH-ANI-05..13,
//   PSH-I18N-01): a `MutationObserver` on the Viewport sees the keyed `[data-current]` C# remounts in the applied batch,
//   before paint on both modes. On a move between triggers while open, the removed `[data-current]` is cloned into a
//   `[data-previous inert]` container the module inserts before the new one (design point 2 (a)); the direction from the
//   previous trigger's center to the new one's (5 px tolerance) and `data-transitioning` are written on the Viewport and
//   reported for the C# mirror; the starting-style choreography runs over a frame and the previous container leaves once
//   the current one's animations finish. Auto-resize measures the popup with the measurement overrides restored and
//   writes `--popup-width/height` and the anchoring styles on the popup and `--positioner-width/height` on the
//   positioner, in the `auto-resize` layer of the `dom` style writer.
// - The Positioner (utils/InternalBackdrop.tsx, utils/useAnchoredPopupScrollLock.ts, internals/useTriggerSwitchTransition.ts;
//   PSH-ALG-17, PSH-ALG-21, PSH-PTR-05..07): the internal backdrop's `clip-path` cut-out of the active trigger, re-measured
//   on attach, on update, on each positioning pass (a `MutationObserver` on the positioner's `style`), on scroll and on
//   resize; the anchored scroll lock with its 20 px width test for a touch open; the watch that reports a move between
//   triggers once the positioner's animations finish (POP-ANI-07; C# writes `trigger-change`).
// - The Popup (POP-KEY-12, DLG-KEY-09): the composite keys' `keydown` stops at the popup when the option says so.
import { getCssDimensions, isShadowRoot, ownerDocument, ownerWindow, styleWriter, type Dimensions, type StyleLayer } from './dom';
import { eventInfo, type EventInfo } from './event-info';
import { getTabbableNearElement, isOutsideEvent } from './focus-manager';
import { acquireSession, getSession, nameRelayTrigger, releaseSession, type InteractionSession } from './interactions';
import { platform } from './platform';
import { readPlacement } from './positioning';
import { acquireScrollLock } from './scroll-lock';
import { createAnimationsWatcher, createFrameSlot, type AnimationsWatcher } from './transitions';
import { createTwinChannel, parseOptions, resolveElement, type DotNetTwin, type TwinCaptures, type TwinChannel, type TwinHandle, type TwinOptions, type TwinReferences } from './twin';

/** The report of the Viewport's direction and transitioning flag. */
export const VIEWPORT_REPORT = 'OnViewportChange';

/** The report that a move between triggers has finished animating. */
export const TRIGGER_CHANGE_REPORT = 'OnTriggerChangeSettled';

/** The decision point a focused trigger guard asks on WebAssembly; C# closes the popup inside it. */
export const GUARD_POINT = 'guard';

/** The report a focused trigger guard sends on Server, after the module moved focus. */
export const GUARD_REPORT = 'OnGuardFocus';

/** The synchronous handover of the focused guard before {@link GUARD_POINT} on WebAssembly. */
export const GUARD_HANDOVER = 'OnGuardFocusing';

/** The options of a Viewport's handle; mirrors `PopupViewportOptions` in C#. */
export interface PopupViewportOptions {
  /** Whether the popup is open; a closed popup resets the measurement. */
  open?: boolean;
  /** The popup's rendered id, which the active trigger names. */
  popupId?: string | null;
  /** The payload's version, whose change re-measures the popup. */
  contentVersion?: number;
}

/** The options of a Positioner's handle; mirrors `PopupPositionerOptions` in C#. */
export interface PopupPositionerOptions {
  /** Whether a move between triggers is watched: the popup is open. */
  watchTriggerChange?: boolean;
  /** Whether the internal backdrop cuts out the active trigger (it is rendered). */
  cutout?: boolean;
  /** Whether the anchored scroll lock is enabled. */
  scrollLock?: boolean;
  /** Whether a touch opened the popup. */
  touchOpen?: boolean;
  /** The id of the active trigger. */
  activeTriggerId?: string | null;
}

/** The options of a Popup's handle; mirrors `PopupElementOptions` in C#. */
export interface PopupElementOptions {
  /** Whether the composite keys' keydown stops at the popup. */
  stopCompositeKeys?: boolean;
}

/** The options of the trigger role; mirrors `PopupTriggerOptions` in C#. */
export interface PopupTriggerOptions {
  /** Whether the trigger's focus guards render beside it (it owns the open popup outside focus-manager-modal mode). */
  guards?: boolean;
  /** The popup's id; its parent is the positioner the guards' destinations skip. */
  popupId?: string | null;
}

/** The options of a `popup` handle; mirrors `PopupOptions` in C#. Exactly one role is set. */
export interface PopupOptions extends TwinOptions {
  sessionToken?: string | null;
  viewport?: PopupViewportOptions | null;
  positioner?: PopupPositionerOptions | null;
  trigger?: PopupTriggerOptions | null;
  popup?: PopupElementOptions | null;
}

/** The payload of {@link GUARD_REPORT}; mirrors `PopupGuardFocus` in C#. */
export interface PopupGuardFocus {
  guard: 'before' | 'after';
  event: EventInfo;
}

/** The payload of {@link VIEWPORT_REPORT}; mirrors `PopupViewportReport` in C#. */
export interface PopupViewportReport {
  activationDirection: string | null;
  transitioning: boolean;
}

/** A relative position between two trigger centers. */
export interface Offset {
  horizontal: number;
  vertical: number;
}

/** A physical side of the anchor. */
export type PhysicalSide = 'top' | 'right' | 'bottom' | 'left';

// usePopupViewport.tsx:296-343.
/**
 * Returns the direction a popup moved, as space-separated tokens (`right down`, `left`, ``): each axis beyond 5 px
 * contributes its token.
 * @param offset The offset from the previous trigger's center to the new one's, or `null`.
 * @returns The direction, or `undefined` without an offset.
 */
export function getActivationDirection(offset: Offset | null): string | undefined {
  if (!offset) {
    return undefined;
  }
  return `${withTolerance(offset.horizontal, 'right', 'left')} ${withTolerance(offset.vertical, 'down', 'up')}`;
}

function withTolerance(value: number, positive: string, negative: string): string {
  if (value > 5) {
    return positive;
  }
  return value < -5 ? negative : '';
}

/**
 * Calculates the offset between the centers of two elements.
 * @param from The previous trigger.
 * @param to The new trigger.
 * @returns The horizontal and vertical offset from `from`'s center to `to`'s.
 */
export function calculateRelativePosition(from: Element, to: Element): Offset {
  const fromRect = from.getBoundingClientRect();
  const toRect = to.getBoundingClientRect();
  return {
    horizontal: toRect.left + toRect.width / 2 - (fromRect.left + fromRect.width / 2),
    vertical: toRect.top + toRect.height / 2 - (fromRect.top + fromRect.height / 2),
  };
}

// usePopupAutoResize.ts:156-173: the popup grows away from the anchor, so it is pinned to the edge facing it.
/**
 * Returns the anchoring styles of a popup on a physical side: pinned to the bottom when above the anchor, to the right
 * when left of it.
 * @param side The physical side.
 * @returns The declarations, empty for the bottom and right sides.
 */
export function getPopupAnchoringStyles(side: PhysicalSide): Record<string, string> {
  const top = side === 'top';
  const left = side === 'left';
  if (!top && !left) {
    return {};
  }
  return { position: 'absolute', [top ? 'bottom' : 'top']: '0', [left ? 'right' : 'left']: '0' };
}

// InternalBackdrop.tsx:13-16.
/**
 * Returns the `clip-path` that cuts a rectangle out of a full-viewport backdrop.
 * @param rect The rectangle, in viewport coordinates.
 * @returns The polygon.
 */
export function cutoutPolygon(rect: { left: number; top: number; right: number; bottom: number }): string {
  const { left, top, right, bottom } = rect;
  return `polygon(0% 0%,100% 0%,100% 100%,0% 100%,0% 0%,${left}px ${top}px,${left}px ${bottom}px,${right}px ${bottom}px,${right}px ${top}px,${left}px ${top}px)`;
}

// useAnchoredPopupScrollLock.ts:6-10: up to 20 px of total gutter still counts as full width.
/**
 * Determines whether a touch-opened popup is wide enough to lock the page's scroll.
 * @param viewportWidth The document element's client width.
 * @param popupWidth The positioner's offset width.
 * @returns `true` when both are positive and the popup leaves at most 20 px.
 */
export function touchOpenLocksScroll(viewportWidth: number, popupWidth: number): boolean {
  return viewportWidth > 0 && popupWidth > 0 && popupWidth >= viewportWidth - 20;
}

const COMPOSITE_KEYS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End']);
const PREVIOUS = 'data-previous';
const CURRENT = 'data-current';
const STARTING = 'data-starting-style';
const ENDING = 'data-ending-style';
const DIRECTION = 'data-activation-direction';
const TRANSITIONING = 'data-transitioning';

interface RoleHandle {
  update(options: PopupOptions, references: TwinReferences): void;
  dispose(): void;
}

// A symbol on the element, as every module's handle is kept: a repeated attach returns the live handle.
const attachedHandle = Symbol('blazix.popup.handle');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

/**
 * Attaches the module to a popup part's element in the role its options name.
 * @param elementOrId The part's element, or its id.
 * @param optionsJson The `PopupOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param references The elements the options name by index, if any.
 * @param captures The capture ids C# sends: the element's first, then the references'.
 * @returns The handle with `update(optionsJson, references, captures)` and `dispose()`; the live handle when already attached.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  // ADR 04 Amendments 2026-10-28: a part inside a shadow root is revived as `null` and found by its capture id.
  const element = resolveElement(elementOrId, captures?.[0]) as AttachedElement;
  const existing = element[attachedHandle];
  if (existing) {
    return existing;
  }

  const channel = createTwinChannel(twinReference);
  const options = parseOptions<PopupOptions>(optionsJson);
  const role: RoleHandle = options.viewport
    ? attachViewport(element, options, channel)
    : options.positioner
      ? attachPositioner(element, options, channel)
      : options.trigger
        ? attachTrigger(element, options, channel, twinReference)
        : attachPopupElement(element, options);
  role.update(options, references);

  let disposed = false;
  const handle: TwinHandle = {
    update(nextOptionsJson, nextReferences) {
      if (!disposed) {
        role.update(parseOptions<PopupOptions>(nextOptionsJson), nextReferences);
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete element[attachedHandle];
      role.dispose();
    },
  };
  element[attachedHandle] = handle;
  return handle;
}

// An id resolves from the node's own root first (its shadow root, else its document), then from its document, as
// `composite` resolves its items (ADR 04 Amendments 2026-10-22 and 2026-10-26; track 5's early review item 8).
function findById(node: Node, id: string): Element | null {
  const scope = node.getRootNode();
  return (isShadowRoot(scope) ? scope.getElementById(id) : null) ?? ownerDocument(node).getElementById(id);
}

// ---------------------------------------------------------------------------------------------------------------
// The Viewport
// ---------------------------------------------------------------------------------------------------------------

function attachViewport(viewport: HTMLElement, initial: PopupOptions, channel: TwinChannel): RoleHandle {
  let options: PopupViewportOptions = initial.viewport ?? {};
  const doc = ownerDocument(viewport);
  const popup = viewport.parentElement?.closest<HTMLElement>('[data-open],[data-closed]') ?? null;
  const positioner = popup?.parentElement?.closest<HTMLElement>('[data-open],[data-closed]') ?? null;
  const popupLayer = popup === null ? null : styleWriter(popup).layer('auto-resize');
  const positionerLayer = positioner === null ? null : styleWriter(positioner).layer('auto-resize');
  const choreography = createFrameSlot();
  const resizeFrame = createFrameSlot();
  let currentContainer = findCurrent(viewport);
  let previousContainer: HTMLElement | null = null;
  let previousTrigger: Element | null = null;
  let lastHandledTrigger: Element | null = null;
  let cleanup: AbortController | null = null;
  let resizeCleanup: AbortController | null = null;
  let committed: Dimensions | null = null;
  let anchoring: Record<string, string> = {};
  let direction: string | null = null;
  let reported: PopupViewportReport | null = null;
  let contentVersion = options.contentVersion ?? 0;
  let placementPending = false;
  let disposed = false;
  const currentWatcher = createAnimationsWatcher(() => currentContainer, true);
  const popupWatcher: AnimationsWatcher = createAnimationsWatcher(popup, true);

  const report = () => {
    const next: PopupViewportReport = { activationDirection: direction, transitioning: previousContainer !== null };
    if (reported === null || reported.activationDirection !== next.activationDirection || reported.transitioning !== next.transitioning) {
      reported = next;
      channel.report(VIEWPORT_REPORT, next);
    }
  };

  // The active trigger is the `interactions` session's reference element, acquired by the token as `positioning` does
  // (design point 13.13 of M4 track 6, settled by the orchestrator 2026-10-28, ADR 04 Amendments 2026-10-28): every
  // family's trigger, including Tooltip's and PreviewCard's, which render no aria-controls (usePopupViewport reads the
  // store's activeTriggerElement).
  let session: InteractionSession | null = null;
  let sessionToken: string | null = null;
  const useSession = (token: string | null): void => {
    if (token === sessionToken) {
      return;
    }
    if (session !== null) {
      releaseSession(session);
    }
    sessionToken = token;
    session = token === null ? null : acquireSession(token);
  };
  const findTrigger = (): Element | null => session?.domReferenceElement ?? null;

  // usePopupViewport.tsx:122-156: the starting-style choreography, then the previous container leaves once the current
  // container's animations finish. Re-run when the current container remounts mid-transition.
  const runChoreography = () => {
    cleanup?.abort();
    const current = currentContainer;
    const previous = previousContainer;
    if (current === null || previous === null) {
      return;
    }
    current.setAttribute(STARTING, '');
    previous.removeAttribute(ENDING);
    choreography.request(() => {
      current.removeAttribute(STARTING);
      previous.setAttribute(ENDING, '');
      const controller = new AbortController();
      cleanup = controller;
      currentWatcher.run(() => {
        if (controller.signal.aborted) {
          return;
        }
        previousContainer?.remove();
        previousContainer = null;
        viewport.removeAttribute(TRANSITIONING);
        report();
      }, controller.signal);
    });
  };

  const startTransition = (removed: HTMLElement | null, from: Element, to: Element) => {
    const content = removed ?? currentContainer;
    if (content === null || currentContainer === null) {
      return;
    }
    const snapshot = doc.createElement('div');
    for (const child of Array.from(content.childNodes)) {
      snapshot.appendChild(child.cloneNode(true));
    }
    if (previousContainer === null) {
      previousContainer = doc.createElement('div');
      previousContainer.setAttribute(PREVIOUS, '');
      previousContainer.setAttribute('inert', '');
      previousContainer.style.position = 'absolute';
    }
    viewport.insertBefore(previousContainer, currentContainer);
    previousContainer.replaceChildren(...Array.from(snapshot.childNodes));
    direction = getActivationDirection(calculateRelativePosition(from, to)) ?? null;
    if (direction === null) {
      viewport.removeAttribute(DIRECTION);
    } else {
      viewport.setAttribute(DIRECTION, direction);
    }
    viewport.setAttribute(TRANSITIONING, '');
    report();
    runChoreography();
  };

  const resetAnchoring = () => {
    for (const property of Object.keys(anchoring)) {
      popupLayer?.remove(property);
    }
    anchoring = {};
  };

  // One measurement; its own writes on the positioner are not the positioning handle's, so the side observer drops them.
  const measure = () => {
    measureNow();
    sideObserver.takeRecords();
  };

  // usePopupAutoResize.ts:42-121.
  const measureNow = () => {
    resizeCleanup?.abort();
    resizeFrame.cancel();
    if (!options.open) {
      committed = null;
      return;
    }
    if (popup === null || positioner === null || popupLayer === null || positionerLayer === null) {
      return;
    }

    // Design point 3 (settled by the orchestrator 2026-10-28): the rendered physical side, from `positioning`'s reader. A
    // Positioner whose handle has not attached yet reads nothing; its first write re-measures (the side observer).
    const placement = readPlacement(positioner);
    placementPending = placement === null;
    const side = placement?.physicalSide ?? 'bottom';
    resetAnchoring();
    anchoring = getPopupAnchoringStyles(side);
    for (const [property, value] of Object.entries(anchoring)) {
      popupLayer.set(property, value);
    }
    setSize(popupLayer, '--popup', 'auto');

    const restore = [
      override(popup, 'position', 'static'),
      override(popup, 'transform', 'none'),
      override(popup, 'scale', '1'),
      override(positioner, '--available-width', 'max-content'),
      override(positioner, '--available-height', 'max-content'),
    ];
    const restoreMeasureLayout = [
      currentContainer === null ? null : override(currentContainer, 'animation', 'none'),
      currentContainer === null ? null : override(currentContainer, 'transition', 'none'),
      previousContainer === null ? null : override(previousContainer, 'display', 'none'),
    ];
    const restoreAll = () => {
      for (const undo of [...restore, ...restoreMeasureLayout]) {
        undo?.();
      }
    };

    setSize(positionerLayer, '--positioner', 'max-content');
    const next = getCssDimensions(popup);
    const previous = committed;
    committed = next;
    if (previous === null) {
      setSize(positionerLayer, '--positioner', next);
      restoreAll();
      return;
    }

    setSize(popupLayer, '--popup', previous);
    restoreAll();
    if (previousContainer !== null) {
      previousContainer.style.setProperty('--popup-width', `${previous.width}px`);
      previousContainer.style.setProperty('--popup-height', `${previous.height}px`);
    }
    setSize(positionerLayer, '--positioner', next);
    const controller = new AbortController();
    resizeCleanup = controller;
    resizeFrame.request(() => {
      setSize(popupLayer, '--popup', next);
      popupWatcher.run(() => {
        if (!controller.signal.aborted) {
          setSize(popupLayer, '--popup', 'auto');
        }
      }, controller.signal);
    });
  };

  // One applied batch: the remounted current container, a move between triggers, the measurement.
  const process = (removed: HTMLElement | null, remounted: boolean) => {
    currentContainer = findCurrent(viewport);
    const open = options.open === true;
    if (!open) {
      lastHandledTrigger = null;
    }
    const trigger = open ? findTrigger() : null;
    if (open && trigger !== null && previousTrigger !== null && trigger !== previousTrigger && lastHandledTrigger !== trigger) {
      lastHandledTrigger = trigger;
      startTransition(removed, previousTrigger, trigger);
    } else if (remounted && previousContainer !== null) {
      // A lagging payload remounted the current container mid-transition: keep the previous one first, re-run the
      // choreography on the new container.
      viewport.insertBefore(previousContainer, currentContainer);
      runChoreography();
    }
    previousTrigger = open ? trigger : null;
    if (remounted) {
      measure();
    }
  };

  const onMutations = (records: MutationRecord[]) => {
    if (disposed) {
      return;
    }
    let removed: HTMLElement | null = null;
    let remounted = false;
    for (const record of records) {
      for (const node of Array.from(record.removedNodes)) {
        if (node instanceof HTMLElement && node.hasAttribute(CURRENT) && removed === null) {
          removed = node;
        }
      }
      for (const node of Array.from(record.addedNodes)) {
        if (node instanceof HTMLElement && node.hasAttribute(CURRENT)) {
          remounted = true;
        }
      }
    }
    if (removed !== null || remounted) {
      process(removed, remounted);
    }
  };
  const observer = new MutationObserver(onMutations);
  observer.observe(viewport, { childList: true });

  // A flip of the side re-anchors the popup (the anchoring styles follow the side, usePopupAutoResize's effect), and so
  // does the positioning handle's first write when the reader had nothing yet.
  const sideObserver = new MutationObserver((records) => {
    if (!disposed && records.some((record) => record.attributeName === 'data-side' || placementPending)) {
      measure();
    }
  });
  if (positioner !== null) {
    sideObserver.observe(positioner, { attributes: true, attributeFilter: ['data-side', 'style'] });
  }

  let attached = false;
  return {
    update(next) {
      useSession(next.sessionToken ?? null);
      // On WebAssembly C#'s after-render update runs in the task that applied the render batch, before the observer's
      // microtask: the removed `[data-current]` is only in the pending records, so they are handled first.
      const pending = observer.takeRecords();
      if (pending.length > 0) {
        onMutations(pending);
      }
      const previousOptions = options;
      options = next.viewport ?? {};
      const version = options.contentVersion ?? 0;
      if (!attached) {
        attached = true;
        previousTrigger = options.open ? findTrigger() : null;
        measure();
        report();
        return;
      }
      if (version !== contentVersion || previousOptions.open !== options.open) {
        contentVersion = version;
        process(null, false);
        measure();
        return;
      }
      process(null, false);
    },
    dispose() {
      disposed = true;
      observer.disconnect();
      sideObserver.disconnect();
      cleanup?.abort();
      resizeCleanup?.abort();
      choreography.cancel();
      resizeFrame.cancel();
      currentWatcher.cancel();
      popupWatcher.cancel();
      previousContainer?.remove();
      previousContainer = null;
      popupLayer?.clear();
      positionerLayer?.clear();
      viewport.removeAttribute(TRANSITIONING);
      useSession(null);
    },
  };
}

function findCurrent(viewport: HTMLElement): HTMLElement | null {
  for (const child of Array.from(viewport.children)) {
    if (child instanceof HTMLElement && child.hasAttribute(CURRENT)) {
      return child;
    }
  }
  return null;
}

function setSize(layer: StyleLayer, prefix: '--popup' | '--positioner', size: Dimensions | 'auto' | 'max-content'): void {
  const width = typeof size === 'string' ? size : `${size.width}px`;
  const height = typeof size === 'string' ? size : `${size.height}px`;
  layer.set(`${prefix}-width`, width);
  layer.set(`${prefix}-height`, height);
}

// usePopupAutoResize.ts:175-182: a measurement override, restored to the exact previous value and priority.
function override(element: HTMLElement, property: string, value: string): () => void {
  const previous = element.style.getPropertyValue(property);
  const priority = element.style.getPropertyPriority(property);
  element.style.setProperty(property, value);
  return () => {
    if (previous === '') {
      element.style.removeProperty(property);
    } else {
      element.style.setProperty(property, previous, priority);
    }
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The Positioner
// ---------------------------------------------------------------------------------------------------------------

function attachPositioner(positioner: HTMLElement, initial: PopupOptions, channel: TwinChannel): RoleHandle {
  let options: PopupPositionerOptions = initial.positioner ?? {};
  const doc = ownerDocument(positioner);
  const win = ownerWindow(positioner);
  const watcher = createAnimationsWatcher(positioner);
  let switching: AbortController | null = null;
  let releaseLock: (() => void) | null = null;
  let lockedTrigger: Element | null = null;
  let backdrop: HTMLElement | null = null;
  let backdropLayer: StyleLayer | null = null;
  let disposed = false;
  let attached = false;

  const trigger = (): Element | null => (options.activeTriggerId ? findById(positioner, options.activeTriggerId) : null);

  // InternalBackdrop.tsx:13-16: the backdrop is the positioner's previous sibling, found by position.
  const cut = () => {
    const sibling = positioner.previousElementSibling;
    const next = sibling instanceof HTMLElement && sibling.hasAttribute('data-base-ui-inert') ? sibling : null;
    if (next !== backdrop) {
      backdropLayer?.clear();
      backdrop = next;
      backdropLayer = next === null ? null : styleWriter(next).layer('positioning-measured');
    }
    if (backdropLayer === null) {
      return;
    }
    const anchor = options.cutout === false ? null : trigger();
    if (anchor === null) {
      backdropLayer.remove('clip-path');
      return;
    }
    backdropLayer.set('clip-path', cutoutPolygon(anchor.getBoundingClientRect()));
  };

  // useAnchoredPopupScrollLock.ts: a touch open locks only a popup that spans the viewport's width.
  const lock = () => {
    const enabled = options.scrollLock === true;
    const wanted = enabled && (options.touchOpen !== true || touchOpenLocksScroll(doc.documentElement.clientWidth, positioner.offsetWidth));
    const reference = trigger();
    if (releaseLock !== null && (!wanted || reference !== lockedTrigger)) {
      releaseLock();
      releaseLock = null;
      lockedTrigger = null;
    }
    if (wanted && releaseLock === null) {
      lockedTrigger = reference;
      releaseLock = acquireScrollLock(reference);
    }
  };

  // useTriggerSwitchTransition.ts: a move between triggers while open reports once the positioner's animations finish.
  const watchSwitch = (previousId: string | null | undefined) => {
    if (options.watchTriggerChange !== true) {
      switching?.abort();
      switching = null;
      return;
    }
    if (!previousId || !options.activeTriggerId || previousId === options.activeTriggerId) {
      return;
    }
    switching?.abort();
    const controller = new AbortController();
    switching = controller;
    watcher.run(() => {
      if (!controller.signal.aborted) {
        channel.report(TRIGGER_CHANGE_REPORT, {});
      }
    }, controller.signal);
  };

  const styleObserver = new MutationObserver(() => {
    if (!disposed) {
      cut();
    }
  });
  styleObserver.observe(positioner, { attributes: true, attributeFilter: ['style'] });
  const siblingObserver = new MutationObserver(() => {
    if (!disposed) {
      cut();
    }
  });
  if (positioner.parentNode !== null) {
    siblingObserver.observe(positioner.parentNode, { childList: true });
  }
  const onScrollOrResize = () => {
    if (!disposed) {
      cut();
    }
  };
  win.addEventListener('scroll', onScrollOrResize, { capture: true, passive: true });
  win.addEventListener('resize', onScrollOrResize);

  return {
    update(next) {
      const previousId = options.activeTriggerId;
      options = next.positioner ?? {};
      cut();
      lock();
      if (attached) {
        watchSwitch(previousId);
      }
      attached = true;
    },
    dispose() {
      disposed = true;
      styleObserver.disconnect();
      siblingObserver.disconnect();
      win.removeEventListener('scroll', onScrollOrResize, { capture: true });
      win.removeEventListener('resize', onScrollOrResize);
      switching?.abort();
      watcher.cancel();
      backdropLayer?.clear();
      releaseLock?.();
      releaseLock = null;
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The Popup
// ---------------------------------------------------------------------------------------------------------------

function attachPopupElement(popup: HTMLElement, initial: PopupOptions): RoleHandle {
  let options: PopupElementOptions = initial.popup ?? {};

  // PopoverPopup.tsx:96-100: inside a toolbar, the composite keys never reach it (stopPropagation, no preventDefault).
  const onKeyDown = (event: KeyboardEvent) => {
    if (options.stopCompositeKeys === true && COMPOSITE_KEYS.has(event.key)) {
      event.stopPropagation();
    }
  };
  popup.addEventListener('keydown', onKeyDown);

  return {
    update(next) {
      options = next.popup ?? {};
    },
    dispose() {
      popup.removeEventListener('keydown', onKeyDown);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The Trigger
// ---------------------------------------------------------------------------------------------------------------

const FOCUS_GUARD = 'data-base-ui-focus-guard';

// Design point 5 of M4 track 5 (settled by the orchestrator 2026-10-28); useTriggerFocusGuards.ts, FocusGuard.tsx. C#
// renders the guards beside the trigger while it owns the open popup outside focus-manager-modal mode; the module listens
// on them. A focused guard names itself as the close's trigger (interactions' `nameRelayTrigger`, design point 4's
// sub-point), then: on WebAssembly it hands the guard over and asks `guard`, inside which C# closes the popup, and focuses
// the tabbable beside the trigger; on Server it focuses at once and reports. Tab onto the focus target from outside the
// popup moves into the popup's content instead.
function attachTrigger(trigger: HTMLElement, initial: PopupOptions, channel: TwinChannel, twinReference: DotNetTwin): RoleHandle {
  let options: PopupTriggerOptions = initial.trigger ?? {};
  let sessionToken = initial.sessionToken ?? null;
  let guards: (HTMLElement | null)[] = [null, null];
  const swapped = new WeakSet<Element>();

  const guardAt = (node: Element | null): HTMLElement | null => (node instanceof HTMLElement && node.hasAttribute(FOCUS_GUARD) ? node : null);

  const positioner = (): HTMLElement | null => {
    const popup = options.popupId ? findById(trigger, options.popupId) : null;
    return popup?.parentElement ?? null;
  };

  // The guards a close unmounts: the trigger's own, and the portal's outside guards around its owner (the element whose
  // aria-owns names the portal node around the positioner) and the placeholder the portal leaves after it.
  const closingGuards = (): HTMLElement[] => {
    const closing = guards.filter((element): element is HTMLElement => element !== null);
    for (let node = positioner()?.parentElement ?? null; node !== null; node = node.parentElement) {
      const owner = node.id ? node.ownerDocument.querySelector(`[aria-owns="${CSS.escape(node.id)}"]`) : null;
      if (owner !== null) {
        const placeholder = owner.nextElementSibling;
        for (const sibling of [owner.previousElementSibling, placeholder, placeholder?.nextElementSibling ?? null]) {
          const guard = guardAt(sibling);
          if (guard !== null && guard.getAttribute('data-type') === 'outside') {
            closing.push(guard);
          }
        }
        break;
      }
    }
    return closing;
  };

  // useTriggerFocusGuards.ts:42-58: the destination is resolved after the close, from the trigger if the guard left. On
  // WebAssembly the close commits inside the decision; on Server it lands after a round trip, so the destination is
  // resolved as the closed document would give it: from the trigger, with the guards the close unmounts out of the tab
  // order for the lookup only (else the next one takes focus and bounces it back into the popup).
  const closeAndFocus = (event: FocusEvent, guard: HTMLElement, name: 'before' | 'after', direction: 1 | -1): void => {
    const session = sessionToken ? getSession(sessionToken) : undefined;
    if (session) {
      nameRelayTrigger(session, guard);
    }
    if (channel.synchronous) {
      twinReference.invokeMethod<void>(GUARD_HANDOVER, JSON.stringify({ guard: name }));
      channel.decide(GUARD_POINT, event, 'run');
      getTabbableNearElement(guard.isConnected ? guard : trigger, direction, positioner())?.focus();
      return;
    }
    const closing = closingGuards().map((element) => [element, element.getAttribute('tabindex')] as const);
    let destination: ReturnType<typeof getTabbableNearElement>;
    try {
      for (const [element] of closing) {
        element.setAttribute('tabindex', '-1');
      }
      destination = getTabbableNearElement(trigger, direction, positioner());
    } finally {
      for (const [element, tabIndex] of closing) {
        if (tabIndex === null) {
          element.removeAttribute('tabindex');
        } else {
          element.setAttribute('tabindex', tabIndex);
        }
      }
    }
    destination?.focus();
    channel.report(GUARD_REPORT, { guard: name, event: eventInfo(event) } satisfies PopupGuardFocus);
  };

  const onBefore = (event: FocusEvent): void => closeAndFocus(event, event.currentTarget as HTMLElement, 'before', -1);

  const onAfter = (event: FocusEvent): void => {
    const container = positioner();
    if (container !== null && isOutsideEvent(event, container)) {
      container.querySelector<HTMLElement>(`[${FOCUS_GUARD}]`)?.focus();
      return;
    }
    closeAndFocus(event, event.currentTarget as HTMLElement, 'after', 1);
  };

  const sync = (): void => {
    const wanted = options.guards === true ? [guardAt(trigger.previousElementSibling), guardAt(trigger.nextElementSibling)] : [null, null];
    if (wanted[0] === guards[0] && wanted[1] === guards[1]) {
      return;
    }
    release();
    guards = wanted;
    // React's `onFocus` is `focusin`.
    guards[0]?.addEventListener('focusin', onBefore);
    guards[1]?.addEventListener('focusin', onAfter);
    for (const element of guards) {
      // FocusGuard.tsx:16-24: VoiceOver's virtual cursor fires focus only on a WebKit `role="button"` element.
      if (element !== null && !swapped.has(element) && platform.screenReader.voiceOver && platform.engine.webkit) {
        swapped.add(element);
        element.setAttribute('role', 'button');
        element.removeAttribute('aria-hidden');
      }
    }
  };

  const release = (): void => {
    guards[0]?.removeEventListener('focusin', onBefore);
    guards[1]?.removeEventListener('focusin', onAfter);
  };

  return {
    update(next) {
      options = next.trigger ?? {};
      sessionToken = next.sessionToken ?? null;
      sync();
    },
    dispose() {
      release();
      guards = [null, null];
    },
  };
}

