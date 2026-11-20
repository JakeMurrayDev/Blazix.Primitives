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
  /** The snap points as the consumer wrote them: fractions of the viewport height, pixels, or `px`/`rem` strings. */
  snapPoints?: Array<number | string> | null;
  /** The active snap point, one of `snapPoints` or any other value, or `null` for none. */
  activeSnapPoint?: number | string | null;
  /** Whether a release moves at most one snap point at a time. */
  snapToSequentialPoints?: boolean;
  /** The number of the last snap point request C# has answered. */
  answeredSnap?: number;
  /** The number of the last request for no snap point that C# canceled; its dismissal is dropped. */
  canceledSnap?: number;
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
const SNAP_POINT_OFFSET = '--drawer-snap-point-offset';

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

// Slice 3 (M6 track 8b, issue 279, added 2026-11-20): the snap points. DRW-STA-05/06/07 resolve them from the viewport
// height, the popup height and the root font size, which the module measures (useDrawerSnapPoints.ts:21-195).

/** A snap point the drawer can rest on: the consumer's value, its height in pixels, and the popup's offset from fully open. */
export interface SnapPointEntry {
  value: number | string;
  height: number;
  offset: number;
}

// DRW-STA-05.
function resolveSnapPointValue(value: number | string, viewportHeight: number, rootFontSize: number): number | null {
  if (!Number.isFinite(viewportHeight) || viewportHeight <= 0) {
    return null;
  }
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) {
      return null;
    }
    return value <= 1 ? clamp(value, 0, 1) * viewportHeight : value;
  }
  const trimmed = value.trim();
  if (trimmed.endsWith('px')) {
    const pixels = Number.parseFloat(trimmed);
    return Number.isFinite(pixels) ? pixels : null;
  }
  if (trimmed.endsWith('rem')) {
    const rems = Number.parseFloat(trimmed);
    return Number.isFinite(rems) ? rems * rootFontSize : null;
  }
  return null;
}

/**
 * Resolves snap points to heights and offsets, in input order, keeping the last of any two within 1 px of each other.
 * @param values The snap points as the consumer wrote them.
 * @param viewportHeight The viewport height, in pixels.
 * @param popupHeight The popup height, in pixels.
 * @param rootFontSize The root font size, in pixels.
 * @returns The resolved snap points; empty when nothing resolves or a height is not positive.
 */
export function resolveSnapPoints(
  values: ReadonlyArray<number | string>,
  viewportHeight: number,
  popupHeight: number,
  rootFontSize: number,
): SnapPointEntry[] {
  // DRW-STA-06.
  if (values.length === 0 || viewportHeight <= 0 || popupHeight <= 0) {
    return [];
  }
  const maxHeight = Math.min(popupHeight, viewportHeight);
  const resolved: SnapPointEntry[] = [];
  for (const value of values) {
    const height = resolveSnapPointValue(value, viewportHeight, rootFontSize);
    if (height === null) {
      continue;
    }
    const clamped = clamp(height, 0, maxHeight);
    resolved.push({ value, height: clamped, offset: Math.max(0, popupHeight - clamped) });
  }
  if (resolved.length <= 1) {
    return resolved;
  }
  const kept: SnapPointEntry[] = [];
  for (let index = resolved.length - 1; index >= 0; index -= 1) {
    const entry = resolved[index]!;
    if (!kept.some((other) => Math.abs(other.height - entry.height) <= 1)) {
      kept.unshift(entry);
    }
  }
  return kept;
}

/**
 * Finds the popup offset of the active snap point.
 * @param active The active snap point, or `null`.
 * @param resolved The resolved snap points.
 * @param viewportHeight The viewport height, in pixels.
 * @param popupHeight The popup height, in pixels.
 * @param rootFontSize The root font size, in pixels.
 * @returns The offset of the equal entry, else of the entry nearest the value's own height, else `null`.
 */
export function resolveActiveSnapPointOffset(
  active: number | string | null,
  resolved: readonly SnapPointEntry[],
  viewportHeight: number,
  popupHeight: number,
  rootFontSize: number,
): number | null {
  // DRW-STA-07.
  if (active === null) {
    return null;
  }
  const exact = resolved.find((entry) => Object.is(entry.value, active));
  if (exact) {
    return exact.offset;
  }
  const height = resolveSnapPointValue(active, viewportHeight, rootFontSize);
  if (height === null || resolved.length === 0) {
    return null;
  }
  const target = clamp(height, 0, Math.min(popupHeight, viewportHeight));
  let closest = resolved[0]!;
  for (const entry of resolved) {
    if (Math.abs(entry.height - target) < Math.abs(closest.height - target)) {
      closest = entry;
    }
  }
  return closest.offset;
}

// DRW-DOM-12's snap point exception (useDrawerSnapPoints.ts:21-28): past fully open, the movement is damped.
function getSnapPointSwipeMovement(baseOffset: number, movement: number): number {
  const nextOffset = baseOffset + movement;
  return nextOffset >= 0 ? movement : -Math.sqrt(-nextOffset) - baseOffset;
}

// The index of the value nearest `target`; the first wins on ties.
function closestIndex(values: readonly number[], target: number): number {
  let best = 0;
  for (let index = 1; index < values.length; index += 1) {
    if (Math.abs(values[index]! - target) < Math.abs(values[best]! - target)) {
      best = index;
    }
  }
  return best;
}

// DRW-ALG-02 (DrawerViewport.tsx:236-293).
function resolveSwipeStrength(
  popup: HTMLElement,
  direction: SwipeDirection,
  baseOffset: number,
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
  const remainingDistance = Math.max(0, size - (baseOffset + getDisplacement(direction, deltaX, deltaY)));
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

  // Slice 3: the snap point request still unanswered on Blazor Server, shown until C# answers it (the revision rule).
  let pendingSnap: { request: number; index: number | null } | null = null;
  let snapRequestCount = 0;
  let swiping = false;
  // DRW-PTR-15 (Base UI's `pendingSwipeCloseSnapPointRef`): the snap point a release closed from, and the release, asked for
  // again when the dismissal leaves the drawer open.
  let closedFrom: { index: number | null; event: PointerEvent | TouchEvent } | null = null;

  // The touch path's state (slice 2): the current touch, whether it is ignored, and the pen mark of DRW-PTR-19 (1).
  let touchState: TouchScrollState | null = null;
  let ignoreTouchSwipe = false;
  let lastPointerType = '';
  let ignoreNextTouchStartFromPen = false;
  let removeTouchMove: (() => void) | null = null;

  const scrollAxis = (): ScrollAxis => (options.swipeDirection === 'left' || options.swipeDirection === 'right' ? 'horizontal' : 'vertical');

  // DRW-API-02: snap points apply to a vertical drawer with a non-empty list.
  const snapValues = (): Array<number | string> => options.snapPoints ?? [];
  const usesSnapPoints = () => snapValues().length > 0 && scrollAxis() === 'vertical';
  // The viewport and root font size as Base UI measures them (useDrawerSnapPoints.ts:92-103).
  const measureSnap = () => {
    const html = ownerDocument(viewport ?? popup).documentElement;
    const fontSize = Number.parseFloat(getComputedStyle(html).fontSize);
    return {
      viewportHeight: viewport ? viewport.offsetHeight : html.clientHeight,
      popupHeight: popup?.offsetHeight ?? 0,
      rootFontSize: Number.isFinite(fontSize) ? fontSize : 16,
    };
  };
  const activeSnapValue = (): number | string | null => {
    if (pendingSnap) {
      return pendingSnap.index === null ? null : (snapValues()[pendingSnap.index] ?? null);
    }
    return options.activeSnapPoint ?? null;
  };
  const snapState = () => {
    const { viewportHeight, popupHeight, rootFontSize } = measureSnap();
    const resolved = resolveSnapPoints(snapValues(), viewportHeight, popupHeight, rootFontSize);
    const activeOffset = resolveActiveSnapPointOffset(activeSnapValue(), resolved, viewportHeight, popupHeight, rootFontSize);
    // DRW-STA-17.
    let range: { minOffset: number; range: number } | null = null;
    if (snapValues().length >= 2 && resolved.length >= 2 && scrollAxis() === 'vertical') {
      const offsets = resolved.map((entry) => entry.offset).sort((a, b) => a - b);
      range = { minOffset: offsets[0]!, range: offsets[1]! - offsets[0]! };
    }
    const resting = range && activeOffset !== null ? clamp((activeOffset - range.minOffset) / range.range, 0, 1) : null;
    return { popupHeight, resolved, activeOffset, range, resting };
  };

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

  // DRW-DOM-11 and the resting half of DRW-PTR-16: the offset variable, and the resting progress while no drag runs.
  const refreshSnap = () => {
    const { activeOffset, resting } = snapState();
    const offset = usesSnapPoints() && activeOffset !== null ? activeOffset : 0;
    popupBand?.set(SNAP_POINT_OFFSET, `${options.swipeDirection === 'up' && offset !== 0 ? -offset : offset}px`);
    if (!swiping && resting !== null) {
      applySwipeProgress(resting);
    }
  };

  // DRW-STA-03 for a release: asks for a snap point by its index in `snapPoints`, or `null` for none, and says whether
  // C# canceled it. On WebAssembly the `snap-point:<index>` point runs C#'s request inside the dispatch; on Blazor Server
  // the request is reported and shown at once, until C#'s answer arrives in an update.
  const requestSnapPoint = (index: number | null, event: PointerEvent | TouchEvent): boolean => {
    const verdict = decide(twin, `snap-point:${index ?? 'none'}`, event, 'run');
    if (verdict === 'stop') {
      return true;
    }
    if (!twin.lastDecisionWasSynchronous) {
      reportSnapPoint(index, event);
    }
    return false;
  };

  // The numbered request, shown until C# answers it: Blazor Server's release, and DRW-PTR-15's restore on both modes.
  const reportSnapPoint = (index: number | null, event: PointerEvent | TouchEvent) => {
    snapRequestCount += 1;
    pendingSnap = { request: snapRequestCount, index };
    twin.report('OnSnapPointRequest', { request: snapRequestCount, index, event: eventInfo(event) });
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
    // DRW-PTR-15: the snap point the release closed from comes back, with reason `swipe` and the release.
    if (closedFrom) {
      const { index, event } = closedFrom;
      closedFrom = null;
      reportSnapPoint(index, event);
      refreshSnap();
    }
  };

  // DRW-PTR-11 and DRW-ALG-01 (DrawerViewport.tsx:502-637): the release of a drawer with snap points.
  const releaseOnSnapPoints = (
    element: HTMLElement,
    details: {
      event: PointerEvent | TouchEvent;
      direction: SwipeDirection | undefined;
      deltaX: number;
      deltaY: number;
      velocityX: number;
      velocityY: number;
      releaseVelocityX: number;
      releaseVelocityY: number;
    },
  ): boolean | undefined => {
    if (scrollAxis() !== 'vertical') {
      clearSwipeRelease();
      return undefined;
    }
    const state = snapState();
    if (!state.popupHeight) {
      clearSwipeRelease();
      return false;
    }
    if (state.resolved.length === 0) {
      clearSwipeRelease();
      return undefined;
    }
    const popupHeight = state.popupHeight;
    const sign = options.swipeDirection === 'down' ? 1 : -1;
    const dragDelta = sign * details.deltaY;
    const dragDirection = Math.sign(dragDelta);
    let velocity = sign * details.releaseVelocityY;
    // Touch reversals at the release are ignored.
    if (dragDirection !== 0 && Math.abs(dragDelta) >= MIN_SWIPE_THRESHOLD && Math.sign(velocity) !== 0 && Math.sign(velocity) !== dragDirection) {
      velocity = sign * details.velocityY;
    }
    const current = state.activeOffset ?? 0;
    const dragTarget = clamp(current + dragDelta, 0, popupHeight);
    const velocityOffset = Math.abs(velocity) >= FAST_SWIPE_VELOCITY ? clamp(velocity, -MAX_SWIPE_RELEASE_VELOCITY, MAX_SWIPE_RELEASE_VELOCITY) * 300 : 0;
    const target = options.snapToSequentialPoints ? dragTarget : clamp(dragTarget + velocityOffset, 0, popupHeight);
    const indexOf = (entry: SnapPointEntry) => snapValues().findIndex((value) => Object.is(value, entry.value));

    const settleInPlace = () => {
      applySwipeProgress(0);
      clearSwipeRelease();
      refreshSnap();
      return false;
    };
    const settle = (entry: SnapPointEntry) => {
      requestSnapPoint(indexOf(entry), details.event);
      return settleInPlace();
    };
    const close = (fallback: SnapPointEntry) => {
      // An unattributed gesture never dismisses.
      if (!details.direction) {
        return settle(fallback);
      }
      const left = activeSnapValue();
      if (requestSnapPoint(null, details.event)) {
        return settleInPlace();
      }
      // A value the list no longer holds has no index to ask for; it is not restored.
      const leftIndex = left === null ? null : snapValues().findIndex((value) => Object.is(value, left));
      closedFrom = leftIndex === -1 ? null : { index: leftIndex, event: details.event };
      refreshSnap();
      startSwipeRelease(
        resolveSwipeStrength(
          element,
          options.swipeDirection,
          current,
          details.deltaX,
          details.deltaY,
          details.velocityX,
          details.velocityY,
          details.releaseVelocityX,
          details.releaseVelocityY,
        ),
      );
      return true;
    };

    if (options.snapToSequentialPoints) {
      const ordered = [...state.resolved].sort((a, b) => a.offset - b.offset);
      const offsets = ordered.map((entry) => entry.offset);
      const currentIndex = closestIndex(offsets, current);
      let chosen = ordered[closestIndex(offsets, target)]!;
      let effective = target;
      if (dragDirection !== 0 && Math.sign(velocity) === dragDirection && Math.abs(velocity) >= FAST_SWIPE_VELOCITY) {
        const adjacent = clamp(currentIndex + dragDirection, 0, ordered.length - 1);
        if (adjacent !== currentIndex) {
          const next = ordered[adjacent]!;
          if ((dragDirection > 0 && target < next.offset) || (dragDirection < 0 && target > next.offset)) {
            chosen = next;
            effective = next.offset;
          }
        } else if (dragDirection > 0) {
          // A flick past the last snap point.
          return close(chosen);
        }
      }
      return Math.abs(effective - popupHeight) < Math.abs(effective - chosen.offset) ? close(chosen) : settle(chosen);
    }
    const nearest = state.resolved[closestIndex(state.resolved.map((entry) => entry.offset), target)]!;
    if (velocity >= FAST_SWIPE_VELOCITY && dragDelta > 0) {
      return close(nearest);
    }
    return Math.abs(target - popupHeight) < Math.abs(target - nearest.offset) ? close(nearest) : settle(nearest);
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

  // Base UI's `swipeDirections`: a vertical drawer with snap points is dragged both ways.
  const swipeDirections = (): SwipeDirection[] =>
    usesSnapPoints() ? (options.swipeDirection === 'down' ? ['down', 'up'] : ['up', 'down']) : [options.swipeDirection];

  const trackerOptions = () => ({
    enabled: options.mounted && options.nestedOpenDrawerCount === 0,
    directions: swipeDirections(),
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
    onSwipingChange: (next: boolean) => {
      swiping = next;
      popup?.toggleAttribute(SWIPING, next);
      backdrop?.toggleAttribute(SWIPING, next);
      twin.report('OnSwipingChange', { swiping: next });
    },
    // DRW-PTR-16 with DRW-ALG-05 and DRW-DOM-12's snap point exception (DrawerViewport.tsx:386-425).
    onProgress: (progress: number, details?: { deltaY: number }) => {
      const snaps = usesSnapPoints();
      const state = snaps ? snapState() : null;
      if (swiping && state && options.swipeDirection === 'down' && details) {
        popupBand?.remove('transform');
        popupBand?.set(MOVEMENT_Y, `${getSnapPointSwipeMovement(state.activeOffset ?? 0, details.deltaY)}px`);
      }
      let resolved = progress;
      if (state?.range && state.popupHeight > 0) {
        const { minOffset, range } = state.range;
        // Outside a drag the tracker still reports the last deltas; the drawer rests on its snap point then.
        if (swiping && details && Number.isFinite(details.deltaY)) {
          resolved = clamp((clamp((state.activeOffset ?? minOffset) + details.deltaY, 0, state.popupHeight) - minOffset) / range, 0, 1);
        } else if (state.resting !== null) {
          resolved = state.resting;
        }
      }
      applySwipeProgress(resolved);
    },
    // DRW-PTR-10 (DrawerViewport.tsx:475-500).
    onRelease: (details: {
      event: PointerEvent | TouchEvent;
      direction: SwipeDirection | undefined;
      deltaX: number;
      deltaY: number;
      velocityX: number;
      velocityY: number;
      releaseVelocityX: number;
      releaseVelocityY: number;
    }) => {
      const { direction, deltaX, deltaY, velocityX, velocityY, releaseVelocityX, releaseVelocityY } = details;
      if (popup && snapValues().length > 0) {
        return releaseOnSnapPoints(popup, details);
      }
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
        startSwipeRelease(resolveSwipeStrength(popup, direction, 0, deltaX, deltaY, velocityX, velocityY, releaseVelocityX, releaseVelocityY));
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

  refreshSnap();

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
      // DRW-ALG-01 on Blazor Server: a canceled change to no snap point drops its dismissal, and Base UI settles that
      // release in place with no snap point asked for again.
      if (pendingSnap && pendingSnap.index === null && (options.canceledSnap ?? 0) >= pendingSnap.request) {
        closedFrom = null;
      }
      // The answer to a dismissal: the drawer still open means a canceled request or an owner that kept it open.
      if (pendingDismiss !== 0 && options.answeredDismiss >= pendingDismiss) {
        if (options.open) {
          rollBack();
        } else {
          pendingDismiss = 0;
          closedFrom = null;
        }
      }
      // An open after a close clears a release left from a kept-mounted dismissal (DrawerViewport.tsx:856-867).
      if (options.open && !previous.open) {
        tracker.reset();
        clearSwipeRelease();
      }
      // C#'s answer to a snap point request: from here its own active snap point stands, so a canceled one rolls back.
      if (pendingSnap && (options.answeredSnap ?? 0) >= pendingSnap.request) {
        pendingSnap = null;
      }
      refreshSnap();
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
