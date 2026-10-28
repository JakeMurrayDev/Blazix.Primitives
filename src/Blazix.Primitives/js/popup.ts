// The `popup` mechanism module (ADR 04 Amendments 2026-10-27, the `popup` row, chosen by the user 2026-10-27; M4 track 5,
// issue 125): the DOM half of Base UI's popup utilities that no other module owns, attached by the parts of every popup
// family. One handle per part, one role per handle (the `interactions` precedent). Design points 1-3 and 5-8 of track 5
// (the placement, the `[data-previous]` container, the physical side, the surface, the anchored lock, the trigger-change
// report, the keydown stop) are built as recommended, pending the user.
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
import { getCssDimensions, ownerDocument, ownerWindow, styleWriter, type Dimensions, type StyleLayer } from './dom';
import { acquireScrollLock } from './scroll-lock';
import { createAnimationsWatcher, createFrameSlot, type AnimationsWatcher } from './transitions';
import { createTwinChannel, parseOptions, resolveElement, type DotNetTwin, type TwinChannel, type TwinHandle, type TwinOptions, type TwinReferences } from './twin';

/** The report of the Viewport's direction and transitioning flag. */
export const VIEWPORT_REPORT = 'OnViewportChange';

/** The report that a move between triggers has finished animating. */
export const TRIGGER_CHANGE_REPORT = 'OnTriggerChangeSettled';

/** The options of a Viewport's handle; mirrors `PopupViewportOptions` in C#. */
export interface PopupViewportOptions {
  /** Whether the popup is open. */
  open?: boolean;
  /** Whether the popup is mounted. */
  mounted?: boolean;
  /** The popup's rendered id, which the active trigger names. */
  popupId?: string | null;
  /** The id of the active trigger, the fallback when no trigger names the popup. */
  activeTriggerId?: string | null;
  /** The payload's version, whose change re-measures the popup. */
  contentVersion?: number;
  /** The text direction C# reads from the direction provider. */
  direction?: 'ltr' | 'rtl' | null;
}

/** The options of a Positioner's handle; mirrors `PopupPositionerOptions` in C#. */
export interface PopupPositionerOptions {
  /** Whether the popup is open. */
  open?: boolean;
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

/** The options of a `popup` handle; mirrors `PopupOptions` in C#. Exactly one role is set. */
export interface PopupOptions extends TwinOptions {
  sessionToken?: string | null;
  viewport?: PopupViewportOptions | null;
  positioner?: PopupPositionerOptions | null;
  popup?: PopupElementOptions | null;
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

/**
 * Resolves the physical side of a logical side.
 * @param side The rendered side, `top`, `bottom`, `left`, `right`, `inline-start` or `inline-end`.
 * @param direction The text direction.
 * @returns The physical side.
 */
export function physicalSideOf(side: string | null, direction: 'ltr' | 'rtl'): PhysicalSide {
  switch (side) {
    case 'top':
    case 'left':
    case 'right':
      return side;
    case 'inline-start':
      return direction === 'rtl' ? 'right' : 'left';
    case 'inline-end':
      return direction === 'rtl' ? 'left' : 'right';
    default:
      return 'bottom';
  }
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
 * @returns The handle with `update(optionsJson, references)` and `dispose()`; the live handle when already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin, references?: TwinReferences): TwinHandle {
  const element = resolveElement(elementOrId) as AttachedElement;
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

  const findTrigger = (): Element | null => {
    const id = popup?.id || options.popupId;
    if (id) {
      const quoted = id.replace(/["\\]/g, '\\$&');
      const named = doc.querySelector(`[data-popup-open][aria-controls~="${quoted}"],[data-popup-open][aria-describedby~="${quoted}"]`);
      if (named !== null) {
        return named;
      }
    }
    return options.activeTriggerId ? doc.getElementById(options.activeTriggerId) : null;
  };

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

  // usePopupAutoResize.ts:42-121.
  const measure = () => {
    resizeCleanup?.abort();
    resizeFrame.cancel();
    if (!options.mounted) {
      committed = null;
      return;
    }
    if (popup === null || positioner === null || popupLayer === null || positionerLayer === null) {
      return;
    }

    const side = physicalSideOf(positioner.getAttribute('data-side'), options.direction === 'rtl' ? 'rtl' : 'ltr');
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
    if (!open || !options.mounted) {
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

  const observer = new MutationObserver((records) => {
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
  });
  observer.observe(viewport, { childList: true });

  // A flip of the side re-anchors the popup (the anchoring styles follow the side, usePopupAutoResize's effect).
  const sideObserver = new MutationObserver(() => {
    if (!disposed) {
      measure();
    }
  });
  if (positioner !== null) {
    sideObserver.observe(positioner, { attributes: true, attributeFilter: ['data-side'] });
  }

  let attached = false;
  return {
    update(next) {
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
      if (version !== contentVersion || previousOptions.mounted !== options.mounted || previousOptions.direction !== options.direction) {
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

  const trigger = (): Element | null => (options.activeTriggerId ? doc.getElementById(options.activeTriggerId) : null);

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
    if (options.open !== true) {
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
