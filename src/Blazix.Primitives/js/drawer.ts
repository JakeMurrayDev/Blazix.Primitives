// The `drawer` component module (ADR 04 §1's `drawer` row; docs/components/drawer.md §6.2, §6.5): the Drawer's
// gestures and the writes they make on the Popup and the Backdrop. M6 track 8b (issue 279, session D), built in slices
// (the 8b design note in the track's notes folder):
// - slice 1: the Viewport handle's mouse and pen dismiss gesture without snap points (DRW-PTR-01…05, 07…10, 12…14, the
//   Backdrop half of PTR-16), the `dismiss` decision point and its rollback through `update`;
// - slice 2: the touch path (PTR-06's touch clauses, PTR-19…24, ALG-03); the VirtualKeyboardProvider notifications
//   (DRW-FOC-10) come with 8c's keyboard role;
// - later slices: snap points (PTR-11, 15, ALG-01/02/05), the Provider visual state and the nested-swipe report
//   (PTR-16…18), the SwipeArea handle (PTR-25…32).
// Transcribed from Base UI v1.9.0: packages/react/src/drawer/viewport/DrawerViewport.tsx.
//
// One writer per value (ADR 04 §2): the Popup's and Backdrop's style declarations go through dom's style writer, in the
// `component` band (Base UI writes them with `style.setProperty` after render); `data-ending-style` through
// `transitions.forceEnding`; `data-swiping` and `data-swipe-dismiss` are this module's attributes. C# mirrors
// `data-swiping` from `OnSwipingChange` (the Popup's and Backdrop's `[MirroredField] Swiping`).
//
// The dismissal (ADR 04 Amendments 2026-11-18, the `dismiss` point; design point 10 (a), chosen by the user 2026-11-19):
// on WebAssembly the release asks `dismiss` inside the dispatch, where C# requests the close with reason `swipe`; `stop`
// is a canceled request and rolls back at once. On Server the point falls back to `run` and the release is reported
// (`OnDismissRequest`, numbered); the module proceeds optimistically and rolls back when an `update` answers the request
// (`answeredDismiss`) while the drawer is still open: a canceled request, or a controlled owner that kept it open
// (DRW-PTR-14, PTR-15).

import {
  activeElement,
  closest,
  contains,
  findScrollableTouchTarget,
  getElementAtPoint,
  getTarget,
  isElement,
  ownerDocument,
  ownerWindow,
  styleWriter,
  type ScrollAxis,
  type StyleLayer,
} from './dom';
import { decide } from './decide';
import { eventInfo } from './event-info';
import {
  BASE_UI_SWIPE_IGNORE_ATTRIBUTE,
  BASE_UI_SWIPE_IGNORE_SELECTOR,
  createSwipeTracker,
  getDisplacement,
  type SwipeDirection,
  type SwipeTracker,
} from './swipe';
import { forceEnding } from './transitions';
import {
  attachTarget,
  createTwinChannel,
  parseOptions,
  resolveReference,
  type DotNetTwin,
  type TwinCaptures,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

/** The options of a drawer handle; mirrors `DrawerOptions` in C#. */
export interface DrawerOptions extends TwinOptions {
  /** The part the handle serves. */
  role: 'viewport';
  /** Whether the drawer is open. */
  open: boolean;
  /** Whether the drawer's popup is mounted. */
  mounted: boolean;
  /** Whether the drawer is nested in another drawer or dialog. */
  nested: boolean;
  /** How many drawers nested in this one are open. */
  nestedOpenDrawerCount: number;
  /** The direction the drawer is swiped to dismiss it. */
  swipeDirection: SwipeDirection;
  /** The number of the last dismiss request C# has answered. */
  answeredDismiss: number;
  /** The index of the Popup element among the references, or `null` while none is rendered. */
  popup?: number | null;
  /** The index of the Backdrop element among the references, or `null` while none is rendered. */
  backdrop?: number | null;
}

// DRW-PTR-03, PTR-10 (DrawerViewport.tsx:45-55).
const MIN_SWIPE_THRESHOLD = 10;
const FAST_SWIPE_VELOCITY = 0.5;
const MIN_SWIPE_RELEASE_VELOCITY = 0.2;
const MAX_SWIPE_RELEASE_VELOCITY = 4;
const MIN_SWIPE_RELEASE_DURATION_MS = 80;
const MAX_SWIPE_RELEASE_DURATION_MS = 360;
const MIN_SWIPE_RELEASE_SCALAR = 0.1;
const MAX_SWIPE_RELEASE_SCALAR = 1;

const AXIS_LOCK_SLOP = 6;
const AXIS_LOCK_BIAS = 2;
const DRAWER_CONTENT_SELECTOR = '[data-drawer-content]';
const AXIS_SWIPE_IGNORE_SELECTORS: Record<ScrollAxis, string> = {
  horizontal: `[${BASE_UI_SWIPE_IGNORE_ATTRIBUTE}="x"]`,
  vertical: `[${BASE_UI_SWIPE_IGNORE_ATTRIBUTE}="y"]`,
};
const SWIPING = 'data-swiping';
const SWIPE_DISMISS = 'data-swipe-dismiss';
const MOVEMENT_X = '--drawer-swipe-movement-x';
const MOVEMENT_Y = '--drawer-swipe-movement-y';
const SWIPE_STRENGTH = '--drawer-swipe-strength';
const SWIPE_PROGRESS = '--drawer-swipe-progress';
const DRAWER_HEIGHT = '--drawer-height';

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function getBaseSwipeSize(element: HTMLElement, direction: SwipeDirection): number {
  return direction === 'left' || direction === 'right' ? element.offsetWidth : element.offsetHeight;
}

// DRW-PTR-03.
function getBaseSwipeThreshold(element: HTMLElement, direction: SwipeDirection): number {
  return Math.max(getBaseSwipeSize(element, direction) * 0.5, MIN_SWIPE_THRESHOLD);
}

// DRW-ALG-02 (DrawerViewport.tsx:236-293), without the snap point base offset (slice 3).
function resolveSwipeStrength(
  popup: HTMLElement,
  direction: SwipeDirection,
  deltaX: number,
  deltaY: number,
  velocityX: number,
  velocityY: number,
  releaseVelocityX: number,
  releaseVelocityY: number,
): number | null {
  const size = getBaseSwipeSize(popup, direction);
  if (size <= 0) {
    return null;
  }
  const remainingDistance = Math.max(0, size - getDisplacement(direction, deltaX, deltaY));
  if (remainingDistance <= 0) {
    return null;
  }
  const releaseVelocity = getDisplacement(direction, releaseVelocityX, releaseVelocityY);
  const directionalVelocity = Math.abs(releaseVelocity) > 0 ? releaseVelocity : getDisplacement(direction, velocityX, velocityY);
  if (directionalVelocity <= MIN_SWIPE_RELEASE_VELOCITY) {
    return null;
  }
  const clampedVelocity = clamp(directionalVelocity, MIN_SWIPE_RELEASE_VELOCITY, MAX_SWIPE_RELEASE_VELOCITY);
  const durationMs = clamp(remainingDistance / clampedVelocity, MIN_SWIPE_RELEASE_DURATION_MS, MAX_SWIPE_RELEASE_DURATION_MS);
  const normalizedDuration = (durationMs - MIN_SWIPE_RELEASE_DURATION_MS) / (MAX_SWIPE_RELEASE_DURATION_MS - MIN_SWIPE_RELEASE_DURATION_MS);
  return MIN_SWIPE_RELEASE_SCALAR + normalizedDuration * (MAX_SWIPE_RELEASE_SCALAR - MIN_SWIPE_RELEASE_SCALAR);
}

// DRW-PTR-21 (b), PTR-06 (DrawerViewport.tsx:1115-1155).
function isTextSelectionControl(target: Element): target is HTMLInputElement | HTMLTextAreaElement {
  return target.tagName === 'INPUT' || target.tagName === 'TEXTAREA';
}

function hasExpandedSelectionWithinTarget(selection: Selection, target: Element): boolean {
  const anchor = isElement(selection.anchorNode) ? selection.anchorNode : selection.anchorNode?.parentElement;
  const focus = isElement(selection.focusNode) ? selection.focusNode : selection.focusNode?.parentElement;
  return selection.containsNode(target, true) || contains(target, anchor) || contains(target, focus);
}

function shouldIgnoreSwipeForTextSelection(doc: Document, root: HTMLElement): boolean {
  const active = activeElement(doc);
  if (active && contains(root, active) && isTextSelectionControl(active)) {
    const { selectionStart, selectionEnd } = active;
    if (selectionStart != null && selectionEnd != null && selectionStart < selectionEnd) {
      return true;
    }
  }
  const selection = doc.getSelection?.();
  if (!selection || selection.isCollapsed) {
    return false;
  }
  return hasExpandedSelectionWithinTarget(selection, root);
}

// DRW-PTR-19 (4).
function isEventOnRangeInput(event: TouchEvent, win: Window & typeof globalThis): boolean {
  return event.composedPath().some((target) => target instanceof win.HTMLInputElement && target.type === 'range');
}

// The per-touch state (Base UI's `TouchScrollState`, DrawerViewport.tsx:64-75).
interface TouchScrollState {
  startX: number;
  startY: number;
  lastX: number;
  lastY: number;
  startedInPopup: boolean;
  scrollTarget: HTMLElement | null;
  hasCrossAxisGestureTarget: boolean;
  allowSwipe: boolean | null;
  preserveNativeCrossAxisScroll: boolean;
  drawerAxisAttributed: boolean;
}

// DRW-ALG-03's `yield(move)` (DrawerViewport.tsx:1172-1227): true leaves the move to the browser or the cross-axis target.
function shouldYieldTouchMove(state: TouchScrollState, event: TouchEvent, touch: Touch, vertical: boolean): boolean {
  if (state.preserveNativeCrossAxisScroll) {
    return true;
  }
  if (state.drawerAxisAttributed || state.allowSwipe === true || !state.hasCrossAxisGestureTarget) {
    return false;
  }
  if (!event.cancelable) {
    state.preserveNativeCrossAxisScroll = true;
    return true;
  }
  const drawerAxis = Math.abs(vertical ? touch.clientY - state.startY : touch.clientX - state.startX);
  const crossAxis = Math.abs(vertical ? touch.clientX - state.startX : touch.clientY - state.startY);
  if (crossAxis >= AXIS_LOCK_SLOP && crossAxis > drawerAxis + AXIS_LOCK_BIAS) {
    state.preserveNativeCrossAxisScroll = true;
    return true;
  }
  if (drawerAxis >= AXIS_LOCK_SLOP) {
    state.drawerAxisAttributed = true;
    return false;
  }
  return true;
}

// DRW-ALG-03's scroll edges (DrawerViewport.tsx:1229-1270).
function scrollMax(target: HTMLElement, axis: ScrollAxis): number {
  return axis === 'vertical'
    ? Math.max(0, target.scrollHeight - target.clientHeight)
    : Math.max(0, target.scrollWidth - target.clientWidth);
}

function scrollOffset(target: HTMLElement, axis: ScrollAxis): number {
  return axis === 'vertical' ? target.scrollTop : target.scrollLeft;
}

function dismissesFromStartEdge(direction: SwipeDirection, axis: ScrollAxis): boolean {
  return axis === 'vertical' ? direction === 'down' : direction === 'right';
}

function isAtSwipeStartEdge(target: HTMLElement, axis: ScrollAxis, direction: SwipeDirection): boolean {
  return dismissesFromStartEdge(direction, axis) ? scrollOffset(target, axis) <= 0 : scrollOffset(target, axis) >= scrollMax(target, axis);
}

function canSwipeFromScrollEdgeOnMove(target: HTMLElement, axis: ScrollAxis, direction: SwipeDirection, delta: number): boolean {
  const towardDismiss = dismissesFromStartEdge(direction, axis) ? delta > 0 : delta < 0;
  return towardDismiss && isAtSwipeStartEdge(target, axis, direction);
}

/**
 * Attaches a drawer handle.
 * @param elementOrId The Viewport element, or its id.
 * @param optionsJson The `DrawerOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param references The elements the options name by index (`popup`, `backdrop`).
 * @param captures The capture ids C# sends beside the element and the references.
 * @returns The handle.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const viewport = attachTarget(elementOrId, captures);
  const twin = createTwinChannel(twinReference);
  let options = parseOptions<DrawerOptions>(optionsJson);
  let popup: HTMLElement | null = null;
  let backdrop: HTMLElement | null = null;
  let popupBand: StyleLayer | null = null;
  let backdropBand: StyleLayer | null = null;
  let disposed = false;

  // The numbered dismiss request still unanswered, or 0.
  let pendingDismiss = 0;
  let dismissCount = 0;

  // The touch path's state (slice 2): the current touch, whether it is ignored, and the pen mark of DRW-PTR-19 (1).
  let touchState: TouchScrollState | null = null;
  let ignoreTouchSwipe = false;
  let lastPointerType = '';
  let ignoreNextTouchStartFromPen = false;
  let removeTouchMove: (() => void) | null = null;

  const scrollAxis = (): ScrollAxis => (options.swipeDirection === 'left' || options.swipeDirection === 'right' ? 'horizontal' : 'vertical');

  const readReferences = (refs: TwinReferences, refCaptures?: TwinCaptures) => {
    const nextPopup = resolveReference<HTMLElement>(refs, options.popup, refCaptures);
    const nextBackdrop = resolveReference<HTMLElement>(refs, options.backdrop, refCaptures);
    if (nextPopup !== popup) {
      popup = nextPopup;
      // ADR 04 Amendments 2026-11-18, design point 2 as revised 2026-11-19 (the opt-in absorber, chosen by the user
      // 2026-11-19): C# renders the Popup's `--nested-drawers` and `--drawer-frontmost-height` in the commit path, so
      // its writer takes a re-rendered `style` as the consumer's and writes the bands again over it.
      const writer = popup ? styleWriter(popup) : null;
      writer?.absorbForeignWrites();
      popupBand = writer ? writer.layer('component') : null;
    }
    if (nextBackdrop !== backdrop) {
      backdrop = nextBackdrop;
      backdropBand = backdrop ? styleWriter(backdrop).layer('component') : null;
    }
  };
  readReferences(references, captures);

  const setSwipeDismissed = (dismissed: boolean) => {
    popup?.toggleAttribute(SWIPE_DISMISS, dismissed);
    backdrop?.toggleAttribute(SWIPE_DISMISS, dismissed);
  };

  // Base UI's `clearSwipeRelease`.
  const clearSwipeRelease = () => {
    setSwipeDismissed(false);
    if (popup) {
      forceEnding(popup, false);
    }
    popupBand?.remove(SWIPE_STRENGTH);
  };

  // DRW-PTR-16's Backdrop half; the Provider visual state and the parent's progress are slice 3.
  const applySwipeProgress = (progress: number) => {
    const active = options.open && !options.nested;
    const shown = active && progress > 0 ? progress : 0;
    backdropBand?.set(SWIPE_PROGRESS, `${shown}`);
    backdropBand?.remove(DRAWER_HEIGHT);
  };

  // DRW-PTR-12: the release's synchronous styles, before the close request.
  const startSwipeRelease = (strength: number | null) => {
    if (!popup) {
      return;
    }
    setSwipeDismissed(true);
    popupBand?.remove('transition');
    forceEnding(popup);
    if (strength !== null && Number.isFinite(strength) && strength > 0) {
      popupBand?.set(SWIPE_STRENGTH, `${strength}`);
    } else {
      popupBand?.remove(SWIPE_STRENGTH);
    }
  };

  const rollBack = () => {
    pendingDismiss = 0;
    tracker.reset();
    clearSwipeRelease();
  };

  const tracker: SwipeTracker = createSwipeTracker({
    enabled: false,
    directions: [options.swipeDirection],
    element: () => popup,
    movementCssVars: { x: MOVEMENT_X, y: MOVEMENT_Y },
    style: {
      set: (property, value) => popupBand?.set(property, value),
      remove: (property) => popupBand?.remove(property),
    },
  });

  const trackerOptions = () => ({
    enabled: options.mounted && options.nestedOpenDrawerCount === 0,
    directions: [options.swipeDirection] as SwipeDirection[],
    element: () => popup,
    movementCssVars: { x: MOVEMENT_X, y: MOVEMENT_Y },
    style: {
      set: (property: string, value: string) => popupBand?.set(property, value),
      remove: (property: string) => popupBand?.remove(property),
    },
    ignoreSelectorWhenTouch: false,
    ignoreScrollableAncestors: true,
    // DRW-PTR-03.
    swipeThreshold: ({ element, direction }: { element: HTMLElement; direction: SwipeDirection }) => getBaseSwipeThreshold(element, direction),
    // DRW-PTR-06.
    canStart: (position: { x: number; y: number }, details: { nativeEvent: PointerEvent | TouchEvent }) => {
      if (!popup) {
        return false;
      }
      const nativeEvent = details.nativeEvent;
      const touchLike = 'touches' in nativeEvent || nativeEvent.pointerType === 'touch';
      const state = touchLike ? touchState : null;
      // A touch that started inside the popup may activate after leaving it.
      if (!state?.startedInPopup && !contains(popup, getElementAtPoint(popup.getRootNode(), position.x, position.y))) {
        return false;
      }
      if (touchLike && shouldIgnoreSwipeForTextSelection(ownerDocument(popup), popup)) {
        return false;
      }
      // Over a cross-axis gesture target, stay pending until the drawer axis wins (DRW-ALG-03).
      return !(state?.hasCrossAxisGestureTarget && !state.drawerAxisAttributed && state.allowSwipe !== true);
    },
    // DRW-PTR-08.
    onSwipeStart: (event: PointerEvent | TouchEvent) => {
      if ('touches' in event || (event as PointerEvent).pointerType === 'touch' || !popup) {
        return;
      }
      const selection = ownerDocument(popup).getSelection?.();
      if (!selection || selection.isCollapsed) {
        return;
      }
      const anchor = isElement(selection.anchorNode) ? selection.anchorNode : selection.anchorNode?.parentElement;
      const focus = isElement(selection.focusNode) ? selection.focusNode : selection.focusNode?.parentElement;
      if (contains(popup, anchor) || contains(popup, focus)) {
        selection.removeAllRanges();
      }
    },
    onSwipingChange: (swiping: boolean) => {
      popup?.toggleAttribute(SWIPING, swiping);
      backdrop?.toggleAttribute(SWIPING, swiping);
      twin.report('OnSwipingChange', { swiping });
    },
    onProgress: (progress: number) => applySwipeProgress(progress),
    // DRW-PTR-10 (DrawerViewport.tsx:475-500).
    onRelease: (details: {
      direction: SwipeDirection | undefined;
      deltaX: number;
      deltaY: number;
      velocityX: number;
      velocityY: number;
      releaseVelocityX: number;
      releaseVelocityY: number;
    }) => {
      const { direction, deltaX, deltaY, velocityX, velocityY, releaseVelocityX, releaseVelocityY } = details;
      if (!popup || !direction) {
        clearSwipeRelease();
        return undefined;
      }
      const directionalDelta = getDisplacement(direction, deltaX, deltaY);
      if (directionalDelta <= 0) {
        clearSwipeRelease();
        return false;
      }
      const shouldClose =
        getDisplacement(direction, velocityX, velocityY) >= FAST_SWIPE_VELOCITY || directionalDelta > getBaseSwipeThreshold(popup, direction);
      if (shouldClose) {
        startSwipeRelease(resolveSwipeStrength(popup, direction, deltaX, deltaY, velocityX, velocityY, releaseVelocityX, releaseVelocityY));
      } else {
        clearSwipeRelease();
      }
      return shouldClose;
    },
    // DRW-PTR-14.
    onDismiss: (event: PointerEvent | TouchEvent) => {
      backdropBand?.set(SWIPE_PROGRESS, '0');
      backdropBand?.remove(DRAWER_HEIGHT);
      // The `dismiss` point: on WebAssembly C# requests the close inside the dispatch.
      const verdict = decide(twin, 'dismiss', event, 'run');
      if (verdict === 'stop') {
        rollBack();
        return;
      }
      setSwipeDismissed(true);
      dismissCount += 1;
      pendingDismiss = dismissCount;
      if (!twin.lastDecisionWasSynchronous) {
        twin.report('OnDismissRequest', { request: dismissCount, event: eventInfo(event) });
      }
    },
  });
  tracker.setOptions(trackerOptions());

  const pointerHandlers = tracker.getPointerProps;
  // DRW-PTR-04, PTR-05 (DrawerViewport.tsx:920-961).
  const onPointerDown = (event: PointerEvent) => {
    lastPointerType = event.pointerType;
    ignoreNextTouchStartFromPen = event.pointerType === 'pen';
    if (!options.open || !options.mounted || options.nestedOpenDrawerCount > 0) {
      return;
    }
    const elementAtPoint = getElementAtPoint(viewport?.getRootNode(), event.clientX, event.clientY);
    if (closest(elementAtPoint, BASE_UI_SWIPE_IGNORE_SELECTOR) || closest(elementAtPoint, DRAWER_CONTENT_SELECTOR)) {
      return;
    }
    if (event.pointerType === 'touch') {
      return;
    }
    pointerHandlers().pointerdown?.(event);
  };
  const onPointerMove = (event: PointerEvent) => {
    if (event.pointerType !== 'touch') {
      pointerHandlers().pointermove?.(event);
    }
  };
  const onPointerEnd = (event: PointerEvent) => {
    lastPointerType = '';
    if (event.pointerType !== 'touch') {
      (event.type === 'pointerup' ? pointerHandlers().pointerup : pointerHandlers().pointercancel)?.(event);
    }
  };

  // DRW-PTR-19 (DrawerViewport.tsx:962-1041).
  const resetTouchSwipeState = (ignore: boolean) => {
    ignoreTouchSwipe = ignore;
    touchState = null;
  };
  const touchHandlers = tracker.getTouchProps;
  const onTouchStart = (event: TouchEvent) => {
    if (lastPointerType === 'pen' && ignoreNextTouchStartFromPen) {
      ignoreNextTouchStartFromPen = false;
      resetTouchSwipeState(false);
      return;
    }
    if (!options.open || !options.mounted || options.nestedOpenDrawerCount > 0 || !viewport) {
      resetTouchSwipeState(false);
      return;
    }
    const touch = event.touches[0];
    if (!touch) {
      return;
    }
    if (isEventOnRangeInput(event, ownerWindow(viewport))) {
      resetTouchSwipeState(false);
      return;
    }
    const elementAtPoint = getElementAtPoint(viewport.getRootNode(), touch.clientX, touch.clientY);
    const eventTarget = getTarget(event);
    const target = isElement(eventTarget) ? eventTarget : viewport;
    if (!contains(viewport, target)) {
      resetTouchSwipeState(true);
      return;
    }
    // DRW-FOC-10's touchstart notification comes with 8c's keyboard role.
    const axis = scrollAxis();
    const crossAxis: ScrollAxis = axis === 'vertical' ? 'horizontal' : 'vertical';
    // `x`/`y` hand touch drags along that axis to the element; any other value ignores the swipe outright.
    if (closest(elementAtPoint, `${BASE_UI_SWIPE_IGNORE_SELECTOR}:not(${AXIS_SWIPE_IGNORE_SELECTORS[crossAxis]})`)) {
      resetTouchSwipeState(true);
      return;
    }
    ignoreTouchSwipe = false;
    const scrollTarget = findScrollableTouchTarget(target, viewport, axis);
    const hasCrossAxisGestureTarget =
      findScrollableTouchTarget(target, viewport, crossAxis) != null || closest(elementAtPoint, AXIS_SWIPE_IGNORE_SELECTORS[crossAxis]) != null;
    touchState = {
      startX: touch.clientX,
      startY: touch.clientY,
      lastX: touch.clientX,
      lastY: touch.clientY,
      startedInPopup: contains(popup, elementAtPoint),
      scrollTarget,
      hasCrossAxisGestureTarget,
      allowSwipe: scrollTarget && !isAtSwipeStartEdge(scrollTarget, axis, options.swipeDirection) ? false : null,
      preserveNativeCrossAxisScroll: false,
      drawerAxisAttributed: false,
    };
    touchHandlers().touchstart?.(event);
  };
  // DRW-PTR-23.
  const onTouchEnd = (event: TouchEvent) => {
    resetTouchSwipeState(false);
    lastPointerType = '';
    ignoreNextTouchStartFromPen = false;
    (event.type === 'touchend' ? touchHandlers().touchend : touchHandlers().touchcancel)?.(event);
  };

  // DRW-PTR-21 (DrawerViewport.tsx:717-804).
  const processTouchMove = (event: TouchEvent, state: TouchScrollState, touch: Touch, root: HTMLElement) => {
    const axis = scrollAxis();
    const vertical = axis === 'vertical';
    const drawerAxisDelta = vertical ? touch.clientY - state.lastY : touch.clientX - state.lastX;
    if (event.touches.length === 2) {
      return;
    }
    const doc = ownerDocument(root);
    if (shouldIgnoreSwipeForTextSelection(doc, root) || options.nestedOpenDrawerCount > 0) {
      return;
    }
    if (shouldYieldTouchMove(state, event, touch, vertical)) {
      return;
    }
    const scrollTarget = state.scrollTarget;
    if (!scrollTarget || scrollTarget === doc.documentElement || scrollTarget === doc.body) {
      if (event.cancelable) {
        event.preventDefault();
      }
      event.stopPropagation();
      tracker.moveNative(event, root);
      return;
    }
    if (scrollMax(scrollTarget, axis) <= 0) {
      if (event.cancelable) {
        event.preventDefault();
      }
      event.stopPropagation();
      return;
    }
    if (drawerAxisDelta !== 0) {
      const fromEdge = canSwipeFromScrollEdgeOnMove(scrollTarget, axis, options.swipeDirection, drawerAxisDelta);
      if (!state.allowSwipe) {
        if (event.cancelable && fromEdge) {
          state.allowSwipe = true;
          event.preventDefault();
        } else {
          state.allowSwipe = false;
        }
      } else if (event.cancelable) {
        event.preventDefault();
      }
    }
    if (state.allowSwipe === true) {
      event.stopPropagation();
      tracker.moveNative(event, root);
    }
  };
  const onDocumentTouchMove = (event: TouchEvent) => {
    // DRW-FOC-10's touchmove notification comes with 8c's keyboard role.
    if (ignoreTouchSwipe) {
      return;
    }
    const state = touchState;
    const touch = event.touches[0];
    const root = viewport ?? popup;
    if (!touch || !state || !root) {
      return;
    }
    processTouchMove(event, state, touch, root);
    state.lastX = touch.clientX;
    state.lastY = touch.clientY;
  };
  // DRW-PTR-20: the document listener exists only while open and mounted, so a closed drawer adds no non-passive touch
  // listener.
  const syncTouchMoveListener = () => {
    const wanted = !disposed && options.open && options.mounted && (viewport ?? popup) !== null;
    if (wanted && removeTouchMove === null) {
      const doc = ownerDocument(viewport ?? popup);
      const listenerOptions: AddEventListenerOptions = { passive: false, capture: true };
      doc.addEventListener('touchmove', onDocumentTouchMove, listenerOptions);
      removeTouchMove = () => doc.removeEventListener('touchmove', onDocumentTouchMove, listenerOptions);
    } else if (!wanted && removeTouchMove !== null) {
      removeTouchMove();
      removeTouchMove = null;
    }
  };

  viewport?.addEventListener('pointerdown', onPointerDown);
  viewport?.addEventListener('pointermove', onPointerMove);
  viewport?.addEventListener('pointerup', onPointerEnd);
  viewport?.addEventListener('pointercancel', onPointerEnd);
  viewport?.addEventListener('touchstart', onTouchStart);
  viewport?.addEventListener('touchend', onTouchEnd);
  viewport?.addEventListener('touchcancel', onTouchEnd);
  syncTouchMoveListener();

  return {
    update(nextJson, nextReferences, nextCaptures) {
      if (disposed) {
        return;
      }
      const previous = options;
      options = parseOptions<DrawerOptions>(nextJson);
      if (nextReferences !== undefined) {
        readReferences(nextReferences, nextCaptures);
      }
      tracker.setOptions(trackerOptions());
      syncTouchMoveListener();
      // The answer to a dismissal: the drawer still open means a canceled request or an owner that kept it open.
      if (pendingDismiss !== 0 && options.answeredDismiss >= pendingDismiss) {
        if (options.open) {
          rollBack();
        } else {
          pendingDismiss = 0;
        }
      }
      // An open after a close clears a release left from a kept-mounted dismissal (DrawerViewport.tsx:856-867).
      if (options.open && !previous.open) {
        tracker.reset();
        clearSwipeRelease();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      viewport?.removeEventListener('pointerdown', onPointerDown);
      viewport?.removeEventListener('pointermove', onPointerMove);
      viewport?.removeEventListener('pointerup', onPointerEnd);
      viewport?.removeEventListener('pointercancel', onPointerEnd);
      viewport?.removeEventListener('touchstart', onTouchStart);
      viewport?.removeEventListener('touchend', onTouchEnd);
      viewport?.removeEventListener('touchcancel', onTouchEnd);
      syncTouchMoveListener();
      backdrop?.toggleAttribute(SWIPING, false);
      popupBand?.clear();
      backdropBand?.clear();
    },
  };
}
