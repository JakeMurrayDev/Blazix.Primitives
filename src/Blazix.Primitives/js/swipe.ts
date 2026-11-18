// The `swipe` mechanism module (ADR 04 §1's `swipe` row; PSH §4.7.5): the values Drawer's and Toast's gestures share and
// the swipe tracker Drawer's Viewport and SwipeArea drive.
// Built by M6 track 1 (the M6 plan's decision 3 (a), chosen by the user 2026-11-18): the literal direction type, the
// displacement along a direction, the tracker's constants and the swipe-ignore selectors, exported so `toast` and the
// tracker import one copy (toast.md: "Same values").
// The tracker (PSH-PTR-09..28, PSH-ALG-07..12, PSH-DOM-17/18, PSH-EVT-09, PSH-I18N-02, PSH-EDGE-10..13): M6 track 8, PR 8b
// (session D). No part attaches this module, so it has no twin, no entry and no JSON context row; esbuild bundles it into
// the `drawer` output (ADR 04 Amendments 2026-11-19, design point 3).
// Transcribed from Base UI v1.9.0: packages/react/src/utils/useSwipeDismiss.ts; internals/constants.ts:8-12.

import {
  clamp,
  closest,
  contains,
  findScrollableAncestor,
  findScrollableTouchTarget,
  getElementAtPoint,
  getElementTransform,
  getTarget,
  ownerDocument,
  type ScrollAxis,
} from './dom';

/** A physical swipe direction; never mirrored in a right-to-left layout. */
export type SwipeDirection = 'up' | 'down' | 'left' | 'right';

// PSH §4.7.5: the tracker's constants (useSwipeDismiss.ts:32-39).

/** The displacement, in pixels, a swipe must exceed to dismiss when no threshold is given. */
export const DEFAULT_SWIPE_THRESHOLD = 40;

/** The movement back, in pixels, that cancels a swipe once it has moved toward a direction. */
export const REVERSE_CANCEL_THRESHOLD = 10;

/** The shortest gesture duration, in milliseconds, over which the overall velocity is measured. */
export const MIN_VELOCITY_DURATION_MS = 50;

/** The shortest time between two samples, in milliseconds, over which a release velocity is measured. */
export const MIN_RELEASE_VELOCITY_DURATION_MS = 4;

/** The greatest age, in milliseconds, of the last sample a release velocity is measured from. */
export const MAX_RELEASE_VELOCITY_AGE_MS = 80;

/** The smallest movement, in pixels, recorded as a velocity sample. */
export const MIN_VELOCITY_SAMPLE_DISTANCE = 1;

/** The targets a swipe does not start on when no selector is given: the interactive elements. */
export const DEFAULT_IGNORE_SELECTOR = 'button,a,input,select,textarea,label,[role="button"]';

// UTL-EDGE-19, the swipe-ignore clause (internals/constants.ts:8-12). Toast ignores both selectors, joined with its own
// interactive list (ToastRoot.tsx:39,253: `button,a,input,textarea,[role="button"]` and both selectors); Drawer's
// Viewport ignores only BASE_UI_SWIPE_IGNORE_SELECTOR (DrawerViewport.tsx:1095), so `data-swipe-ignore` stops a toast's
// swipe and not a drawer's. Each consumer composes its selector from these.

/** The attribute that marks an element whose subtree does not start a swipe. */
export const BASE_UI_SWIPE_IGNORE_ATTRIBUTE = 'data-base-ui-swipe-ignore';

/** The earlier attribute with the same meaning, still honored where Base UI honors it. */
export const LEGACY_SWIPE_IGNORE_ATTRIBUTE = 'data-swipe-ignore';

/** Matches an element that carries {@link BASE_UI_SWIPE_IGNORE_ATTRIBUTE}, whatever its value. */
export const BASE_UI_SWIPE_IGNORE_SELECTOR = `[${BASE_UI_SWIPE_IGNORE_ATTRIBUTE}]`;

/** Matches an element that carries {@link LEGACY_SWIPE_IGNORE_ATTRIBUTE}. */
export const LEGACY_SWIPE_IGNORE_SELECTOR = `[${LEGACY_SWIPE_IGNORE_ATTRIBUTE}]`;

// PSH §4.7.5: displacement along a direction (useSwipeDismiss.ts:41-54).
/**
 * Measures a movement along a swipe direction.
 * @param direction The direction.
 * @param deltaX The horizontal movement, in pixels, positive to the right.
 * @param deltaY The vertical movement, in pixels, positive downward.
 * @returns The movement toward `direction` (`up`: `-deltaY`, `down`: `deltaY`, `left`: `-deltaX`, `right`: `deltaX`);
 *   0 for any other value.
 */
export function getDisplacement(direction: SwipeDirection, deltaX: number, deltaY: number): number {
  switch (direction) {
    case 'up':
      return -deltaY;
    case 'down':
      return deltaY;
    case 'left':
      return -deltaX;
    case 'right':
      return deltaX;
    default:
      return 0;
  }
}

// PSH §4.7: the swipe tracker (useSwipeDismiss.ts:56-1126). Base UI's hook keeps its gesture in refs and three pieces of
// React state; here one closure holds them all, and the three state values are getters on the returned tracker. Base UI
// spreads `getPointerProps()`/`getTouchProps()` onto the element; here they are listener maps keyed by DOM event type,
// bound with `bindSwipeHandlers`.

/** A point in client coordinates. */
export interface SwipePoint {
  x: number;
  y: number;
}

/** The movement a progress report carries: the drag offset from the initial translation, and the intended direction. */
export interface SwipeProgressDetails {
  deltaX: number;
  deltaY: number;
  direction: SwipeDirection | undefined;
}

/** What a release reports before its decision (PSH-EVT-09). Velocities are in pixels per millisecond. */
export interface SwipeReleaseDetails {
  event: PointerEvent | TouchEvent;
  direction: SwipeDirection | undefined;
  deltaX: number;
  deltaY: number;
  velocityX: number;
  velocityY: number;
  releaseVelocityX: number;
  releaseVelocityY: number;
}

/** The options of a swipe tracker (PSH §6.1's swipe tracker rows). */
export interface SwipeTrackerOptions {
  /** Whether the tracker reacts to input; its listener maps are empty when it does not. */
  enabled: boolean;
  /** The directions the gesture may dismiss toward, on a single axis. */
  directions: readonly SwipeDirection[];
  /** The tracked element, read at each event. */
  element: () => HTMLElement | null;
  /** The two CSS variables the drag offset is written to. */
  movementCssVars: { x: string; y: string };
  /** The distance, in pixels, a release must exceed to dismiss: a number, or a function read per gesture; 40 by default. */
  swipeThreshold?: number | ((details: { element: HTMLElement; direction: SwipeDirection }) => number) | undefined;
  /** Gates the start of a gesture; evaluated on the press and on each later move while the press is pending. */
  canStart?: ((position: SwipePoint, details: { nativeEvent: PointerEvent | TouchEvent; direction: SwipeDirection | undefined }) => boolean) | undefined;
  /** Whether a gesture refuses to start inside a scrollable ancestor of the target on the gesture axis; `false` by default. */
  ignoreScrollableAncestors?: boolean | undefined;
  /** Whether touch input also refuses to start on the ignored interactive targets; `true` by default. */
  ignoreSelectorWhenTouch?: boolean | undefined;
  /** Whether the tracker writes the drag transform and movement variables to the element; `true` by default. */
  trackDrag?: boolean | undefined;
  onSwipeStart?: ((event: PointerEvent | TouchEvent) => void) | undefined;
  onProgress?: ((progress: number, details?: SwipeProgressDetails) => void) | undefined;
  onCancel?: ((event: PointerEvent | TouchEvent) => void) | undefined;
  onSwipingChange?: ((swiping: boolean) => void) | undefined;
  /** Called at each release; a boolean return overrides the dismissal decision. */
  onRelease?: ((details: SwipeReleaseDetails) => boolean | void) | undefined;
  onDismiss?: ((event: PointerEvent | TouchEvent, details: { direction: SwipeDirection }) => void) | undefined;
}

/** A listener map for pointer input, keyed by DOM event type. */
export type SwipePointerHandlers = Partial<Record<'pointerdown' | 'pointermove' | 'pointerup' | 'pointercancel', (event: PointerEvent) => void>>;

/** A listener map for touch input, keyed by DOM event type. */
export type SwipeTouchHandlers = Partial<Record<'touchstart' | 'touchmove' | 'touchend' | 'touchcancel', (event: TouchEvent) => void>>;

/** A swipe tracker (PSH-PTR-28). */
export interface SwipeTracker {
  /** Whether a gesture is active. */
  readonly swiping: boolean;
  /** The direction of the gesture, or of its dismissal once dismissed. */
  readonly swipeDirection: SwipeDirection | undefined;
  /** Whether the last release dismissed. */
  readonly dragDismissed: boolean;
  /** Replaces the options; a gesture in progress keeps the threshold it started with. */
  setOptions(options: SwipeTrackerOptions): void;
  /** The pointer listeners; empty while disabled. */
  getPointerProps(): SwipePointerHandlers;
  /** The touch listeners; empty while disabled. */
  getTouchProps(): SwipeTouchHandlers;
  /** Feeds a native `touchmove` captured elsewhere into the same pipeline, `currentTarget` standing for its listener. */
  moveNative(event: TouchEvent, currentTarget: HTMLElement): void;
  /** The drag styles a render of the element carries: property name to value (PSH-DOM-17). */
  getDragStyles(): Record<string, string>;
  /** Returns the tracker to its initial state and restores the element's inline styles (PSH-PTR-27). */
  reset(): void;
}

// The input a handler normalises: a pointer event, a touch event, or a native touchmove fed through `moveNative`.
interface SwipeInput {
  readonly touch: boolean;
  readonly nativeEvent: PointerEvent | TouchEvent;
  readonly currentTarget: HTMLElement | null;
}

function pointerInput(event: PointerEvent): SwipeInput {
  return { touch: false, nativeEvent: event, currentTarget: event.currentTarget as HTMLElement | null };
}

function touchInput(event: TouchEvent, currentTarget?: HTMLElement): SwipeInput {
  return { touch: true, nativeEvent: event, currentTarget: currentTarget ?? (event.currentTarget as HTMLElement | null) };
}

function getValidTimeStamp(timeStamp: number): number | null {
  return Number.isFinite(timeStamp) && timeStamp > 0 ? timeStamp : null;
}

function getDragTransform(dragOffset: SwipePoint, scale: number): string {
  return `translate3d(${dragOffset.x}px,${dragOffset.y}px,0) scale(${scale})`;
}

function hasPrimaryMouseButton(buttons: number): boolean {
  return buttons % 2 === 1;
}

// PSH-EDGE-10.
function safelyChangePointerCapture(element: HTMLElement, pointerId: number, method: 'setPointerCapture' | 'releasePointerCapture'): void {
  const pointerCaptureMethod = element[method] as ((pointerId: number) => void) | undefined;
  if (typeof pointerCaptureMethod !== 'function') {
    return;
  }

  try {
    pointerCaptureMethod.call(element, pointerId);
  } catch (error) {
    if (error && typeof error === 'object' && 'name' in error && error.name === 'NotFoundError') {
      return;
    }
    throw error;
  }
}

/**
 * Binds a tracker's listener map to an element.
 * @param target The element.
 * @param handlers The map from `getPointerProps()` or `getTouchProps()`.
 * @returns A function that removes the listeners.
 */
export function bindSwipeHandlers(target: EventTarget, handlers: SwipePointerHandlers | SwipeTouchHandlers): () => void {
  const entries = Object.entries(handlers) as [string, EventListener][];
  for (const [type, listener] of entries) {
    target.addEventListener(type, listener);
  }
  return () => {
    for (const [type, listener] of entries) {
      target.removeEventListener(type, listener);
    }
  };
}

/**
 * Creates a swipe tracker for one element.
 * @param initialOptions The options; replace them with `setOptions`.
 * @returns The tracker.
 */
export function createSwipeTracker(initialOptions: SwipeTrackerOptions): SwipeTracker {
  let options = initialOptions;

  // The values Base UI derives at each render (useSwipeDismiss.ts:107-123), derived again at each `setOptions`.
  let primaryDirection: SwipeDirection | undefined;
  let swipeThresholdDefault = DEFAULT_SWIPE_THRESHOLD;
  let allowLeft = false;
  let allowRight = false;
  let allowUp = false;
  let allowDown = false;
  let hasHorizontal = false;
  let hasVertical = false;
  let scrollAxis: ScrollAxis = 'vertical';

  function derive(): void {
    const { directions, swipeThreshold } = options;
    primaryDirection = directions.length === 1 ? directions[0] : undefined;
    swipeThresholdDefault = Math.max(0, typeof swipeThreshold === 'number' ? swipeThreshold : DEFAULT_SWIPE_THRESHOLD);
    allowLeft = directions.includes('left');
    allowRight = directions.includes('right');
    allowUp = directions.includes('up');
    allowDown = directions.includes('down');
    hasHorizontal = allowLeft || allowRight;
    hasVertical = allowUp || allowDown;
    // Consumers only pass directions on a single axis.
    scrollAxis = hasHorizontal ? 'horizontal' : 'vertical';
  }
  derive();

  const ignoreSelector = DEFAULT_IGNORE_SELECTOR;

  let currentSwipeDirection: SwipeDirection | undefined;
  let dragDismissed = false;
  let isSwiping = false;

  let dragStartPos: SwipePoint = { x: 0, y: 0 };
  let dragOffset: SwipePoint = { x: 0, y: 0 };
  let lastMovePos: SwipePoint | null = null;
  let initialTransform = { x: 0, y: 0, scale: 1 };
  let intendedSwipeDirection: SwipeDirection | undefined;
  let maxSwipeDisplacement = 0;
  let cancelledSwipe = false;
  let swipeCancelBaseline: SwipePoint = { x: 0, y: 0 };
  let isFirstPointerMove = false;
  let pendingSwipe = false;
  let pendingSwipeStartPos: SwipePoint | null = null;
  let swipeFromScrollable = false;
  let sawPrimaryButtonsOnMove = false;
  let elementSize = { width: 0, height: 0 };
  let swipeProgress = 0;
  let swipeThreshold = swipeThresholdDefault;
  let swipeThresholdFunction: ((details: { element: HTMLElement; direction: SwipeDirection }) => number) | null = null;
  let swipeStartTime: number | null = null;
  let lastDragSample: { x: number; y: number; time: number } | null = null;
  let hasStationarySample = false;
  let lastDragVelocity: SwipePoint = { x: 0, y: 0 };
  let lastProgressDetails: SwipeProgressDetails | null = null;
  let dragStyleSnapshot: [string, string] | null = null;

  function setSwiping(nextSwiping: boolean): void {
    if (isSwiping === nextSwiping) {
      return;
    }
    isSwiping = nextSwiping;
    options.onSwipingChange?.(nextSwiping);
  }

  // PSH-PTR-12.
  function resolveSwipeThreshold(direction: SwipeDirection | undefined): void {
    if (!direction) {
      return;
    }
    const element = options.element();
    if (!element || !swipeThresholdFunction) {
      return;
    }
    swipeThreshold = Math.max(0, swipeThresholdFunction({ element, direction }));
  }

  // PSH-ALG-09's report.
  function updateSwipeProgress(progress: number, details?: SwipeProgressDetails): void {
    const nextProgress = Number.isFinite(progress) ? clamp(progress, 0, 1) : 0;
    const progressChanged = nextProgress !== swipeProgress;
    let detailsChanged = false;

    if (details) {
      const lastDetails = lastProgressDetails;
      detailsChanged =
        !lastDetails ||
        lastDetails.deltaX !== details.deltaX ||
        lastDetails.deltaY !== details.deltaY ||
        lastDetails.direction !== details.direction;
    }

    if (!progressChanged && !detailsChanged) {
      return;
    }

    swipeProgress = nextProgress;
    if (details) {
      lastProgressDetails = details;
    } else if (progressChanged) {
      lastProgressDetails = null;
    }
    options.onProgress?.(nextProgress, details);
  }

  // PSH-DOM-17's imperative writer.
  function syncDragStyles(swiping: boolean): void {
    const element = options.element();
    if (!(options.trackDrag ?? true) || !element) {
      if (!swiping) {
        dragStyleSnapshot = null;
      }
      return;
    }

    const style = element.style;
    if (swiping) {
      if (!dragStyleSnapshot) {
        dragStyleSnapshot = [style.transition, style.transform];
      }
      style.transition = 'none';
    } else if (dragStyleSnapshot) {
      [style.transition, style.transform] = dragStyleSnapshot;
      dragStyleSnapshot = null;
    }

    const deltaX = dragOffset.x - initialTransform.x;
    const deltaY = dragOffset.y - initialTransform.y;

    if (swiping) {
      style.transform = getDragTransform(dragOffset, initialTransform.scale);
    }

    style.setProperty(options.movementCssVars.x, `${deltaX}px`);
    style.setProperty(options.movementCssVars.y, `${deltaY}px`);
  }

  // PSH-ALG-10's sampling.
  function recordDragSample(offset: SwipePoint, timeStamp: number | null): void {
    if (timeStamp === null) {
      return;
    }

    const lastSample = lastDragSample;
    if (lastSample && timeStamp > lastSample.time) {
      // Some Android devices emit one effectively stationary move immediately before release. The last moving sample
      // and its timestamp are kept; repeated stationary moves still clear the velocity, and a release after a hold
      // still expires the last moving sample.
      const stationary =
        Math.abs(offset.x - lastSample.x) < MIN_VELOCITY_SAMPLE_DISTANCE && Math.abs(offset.y - lastSample.y) < MIN_VELOCITY_SAMPLE_DISTANCE;
      const skipSample = stationary && !hasStationarySample;
      hasStationarySample = stationary;
      if (skipSample) {
        return;
      }

      const durationMs = Math.max(timeStamp - lastSample.time, MIN_RELEASE_VELOCITY_DURATION_MS);
      lastDragVelocity = { x: (offset.x - lastSample.x) / durationMs, y: (offset.y - lastSample.y) / durationMs };
    }

    lastDragSample = { x: offset.x, y: offset.y, time: timeStamp };
  }

  // PSH-PTR-27.
  function reset(): void {
    currentSwipeDirection = undefined;
    setSwiping(false);
    dragDismissed = false;
    updateSwipeProgress(0);

    swipeThreshold = swipeThresholdDefault;
    swipeThresholdFunction = null;
    dragStartPos = { x: 0, y: 0 };
    dragOffset = { x: 0, y: 0 };
    initialTransform = { x: 0, y: 0, scale: 1 };
    intendedSwipeDirection = undefined;
    maxSwipeDisplacement = 0;
    cancelledSwipe = false;
    swipeCancelBaseline = { x: 0, y: 0 };
    isFirstPointerMove = false;
    lastMovePos = null;
    pendingSwipe = false;
    pendingSwipeStartPos = null;
    swipeFromScrollable = false;
    sawPrimaryButtonsOnMove = false;
    elementSize = { width: 0, height: 0 };
    swipeStartTime = null;
    lastDragSample = null;
    hasStationarySample = false;
    lastDragVelocity = { x: 0, y: 0 };
    lastProgressDetails = null;
    syncDragStyles(false);
  }

  // PSH-EDGE-12: a touch event without a primary touch point has no position.
  function getPrimaryPointerPosition(input: SwipeInput): SwipePoint | null {
    if (input.touch) {
      const touch = (input.nativeEvent as TouchEvent).touches?.[0];
      return touch ? { x: touch.clientX, y: touch.clientY } : null;
    }
    const event = input.nativeEvent as PointerEvent;
    return { x: event.clientX, y: event.clientY };
  }

  function isTouchLike(input: SwipeInput): boolean {
    return input.touch || (input.nativeEvent as PointerEvent).pointerType === 'touch';
  }

  function getTargetAtPoint(position: SwipePoint, nativeEvent: Event): HTMLElement | null {
    const root = options.element()?.getRootNode();
    const elementAtPoint = getElementAtPoint(root, position.x, position.y);
    return (elementAtPoint ?? getTarget(nativeEvent)) as HTMLElement | null;
  }

  // PSH-EDGE-13: the swiped element is positioned relative to the viewport, so the page scroller never gates the
  // gesture (a reset like `html, body { height: 100%; overflow: auto }` makes `body` a real scroll container).
  function findGestureScrollableTouchTarget(target: EventTarget | null, root: HTMLElement): HTMLElement | null {
    const scrollTarget = findScrollableTouchTarget(target, root, scrollAxis);
    const doc = ownerDocument(scrollTarget);
    return scrollTarget === doc.body || scrollTarget === doc.documentElement ? null : scrollTarget;
  }

  // PSH-PTR-10 and PSH-PTR-11.
  function startSwipeAtPosition(
    input: SwipeInput,
    position: SwipePoint,
    startOptions?: { ignoreScrollableTarget?: boolean; ignoreScrollableAncestors?: boolean },
  ): boolean {
    swipeFromScrollable = false;
    const touchLike = isTouchLike(input);
    const target = getTargetAtPoint(position, input.nativeEvent);

    const element = options.element();
    const body = ownerDocument(element).body;

    const scrollableTarget = touchLike && body ? findGestureScrollableTouchTarget(target, body) : null;
    const ignoreScrollableTarget = startOptions?.ignoreScrollableTarget ?? false;
    if (scrollableTarget && !ignoreScrollableTarget) {
      return false;
    }
    swipeFromScrollable = Boolean(scrollableTarget && ignoreScrollableTarget);

    const isInteractiveElement = closest(target, ignoreSelector);
    if (isInteractiveElement && (!touchLike || (options.ignoreSelectorWhenTouch ?? true))) {
      return false;
    }

    if ((options.ignoreScrollableAncestors ?? false) && element && target) {
      const ignoreAncestors = startOptions?.ignoreScrollableAncestors ?? false;
      if (!ignoreAncestors && findScrollableAncestor(target, element, scrollAxis) != null) {
        return false;
      }
    }

    cancelledSwipe = false;
    intendedSwipeDirection = undefined;
    maxSwipeDisplacement = 0;

    dragStartPos = position;
    swipeStartTime = getValidTimeStamp(input.nativeEvent.timeStamp);
    lastDragSample = null;
    hasStationarySample = false;
    lastDragVelocity = { x: 0, y: 0 };
    swipeCancelBaseline = position;
    lastMovePos = position;
    swipeThreshold = swipeThresholdDefault;
    swipeThresholdFunction = typeof options.swipeThreshold === 'function' ? options.swipeThreshold : null;

    if (element) {
      elementSize = { width: element.offsetWidth, height: element.offsetHeight };
      resolveSwipeThreshold(primaryDirection);
      const transform = getElementTransform(element);

      initialTransform = transform;
      dragOffset = { x: transform.x, y: transform.y };
      recordDragSample({ x: transform.x, y: transform.y }, swipeStartTime);

      if (!input.touch) {
        safelyChangePointerCapture(element, (input.nativeEvent as PointerEvent).pointerId, 'setPointerCapture');
      }
    }

    options.onSwipeStart?.(input.nativeEvent);

    setSwiping(true);
    isFirstPointerMove = true;
    updateSwipeProgress(0);
    syncDragStyles(true);

    return true;
  }

  function clearPendingSwipeStartState(): void {
    pendingSwipe = false;
    pendingSwipeStartPos = null;
  }

  function resetPendingSwipeState(): void {
    clearPendingSwipeStartState();
    swipeFromScrollable = false;
    lastMovePos = null;
  }

  // PSH-PTR-20.
  function cancelSwipeInteraction(event: PointerEvent): void {
    resetPendingSwipeState();

    if (!isSwiping) {
      return;
    }

    setSwiping(false);

    dragOffset = { x: initialTransform.x, y: initialTransform.y };
    currentSwipeDirection = undefined;
    sawPrimaryButtonsOnMove = false;
    syncDragStyles(false);

    const element = options.element();
    if (element) {
      safelyChangePointerCapture(element, event.pointerId, 'releasePointerCapture');
    }

    updateSwipeProgress(0, { deltaX: 0, deltaY: 0, direction: undefined });

    options.onCancel?.(event);
  }

  // PSH-ALG-08.
  function applyDirectionalDamping(deltaX: number, deltaY: number): SwipePoint {
    const exponent = (value: number) => Math.sign(value) * Math.abs(value) ** 0.5;
    const dampAxis = (delta: number, allowNegative: boolean, allowPositive: boolean) =>
      (!allowNegative && delta < 0) || (!allowPositive && delta > 0) ? exponent(delta) : delta;

    return {
      x: hasHorizontal ? dampAxis(deltaX, allowLeft, allowRight) : exponent(deltaX),
      y: hasVertical ? dampAxis(deltaY, allowUp, allowDown) : exponent(deltaY),
    };
  }

  // PSH-ALG-12's edge test: toward the axis start edge (down/right) is allowed when scrolled to the start, toward the
  // end edge (up/left) when scrolled to the end.
  function canSwipeFromScrollEdgeOnPendingMove(scrollTarget: HTMLElement, deltaX: number, deltaY: number): boolean | null {
    const canSwipeOnAxis = (delta: number, scrollOffset: number, maxScrollOffset: number, allowTowardStart: boolean, allowTowardEnd: boolean) =>
      (delta > 0 && scrollOffset <= 0 && allowTowardStart) || (delta < 0 && scrollOffset >= Math.max(0, maxScrollOffset) && allowTowardEnd);

    if (hasVertical && deltaY !== 0) {
      return canSwipeOnAxis(deltaY, scrollTarget.scrollTop, scrollTarget.scrollHeight - scrollTarget.clientHeight, allowDown, allowUp);
    }

    if (hasHorizontal && deltaX !== 0) {
      return canSwipeOnAxis(deltaX, scrollTarget.scrollLeft, scrollTarget.scrollWidth - scrollTarget.clientWidth, allowRight, allowLeft);
    }

    return null;
  }

  // PSH-PTR-09.
  function handleStart(input: SwipeInput): void {
    if (!options.enabled) {
      return;
    }

    if (input.nativeEvent.defaultPrevented) {
      return;
    }

    if (!input.touch && (input.nativeEvent as PointerEvent).button !== 0) {
      return;
    }

    const startPos = getPrimaryPointerPosition(input);
    if (!startPos) {
      return;
    }

    pendingSwipe = true;
    pendingSwipeStartPos = startPos;
    swipeFromScrollable = false;
    sawPrimaryButtonsOnMove = !input.touch;

    const allowedToStart = options.canStart
      ? options.canStart(startPos, { nativeEvent: input.nativeEvent, direction: primaryDirection })
      : true;
    if (!allowedToStart) {
      return;
    }

    if (startSwipeAtPosition(input, startPos)) {
      clearPendingSwipeStartState();
    }
  }

  // PSH-ALG-07.
  function handleMoveCore(input: SwipeInput, position: SwipePoint, movement: SwipePoint): void {
    if (!options.enabled || !isSwiping) {
      return;
    }

    // PSH-PTR-19.
    const target = getTarget(input.nativeEvent) as HTMLElement | null;
    if (isTouchLike(input) && !swipeFromScrollable && input.currentTarget) {
      if (findGestureScrollableTouchTarget(target, input.currentTarget)) {
        return;
      }
    }

    // PSH-PTR-18: prevents text selection on Safari; touch moves are never prevented.
    if (!input.touch) {
      input.nativeEvent.preventDefault();
    }

    // PSH-PTR-13: the first move re-anchors the origin to absorb the gap between the press and the first move (notably
    // on iOS touch); without drag tracking (the swipe area) the press position is kept so a quick flick whose whole
    // travel lands in this one move still registers.
    if (isFirstPointerMove) {
      isFirstPointerMove = false;
      if (options.trackDrag ?? true) {
        dragStartPos = position;
        const moveTime = getValidTimeStamp(input.nativeEvent.timeStamp);
        if (moveTime !== null) {
          swipeStartTime = moveTime;
        }
        lastDragSample = null;
        hasStationarySample = false;
      }
    }

    const clientX = position.x;
    const clientY = position.y;

    if ((movement.y < 0 && clientY > swipeCancelBaseline.y) || (movement.y > 0 && clientY < swipeCancelBaseline.y)) {
      swipeCancelBaseline = { x: swipeCancelBaseline.x, y: clientY };
    }

    if ((movement.x < 0 && clientX > swipeCancelBaseline.x) || (movement.x > 0 && clientX < swipeCancelBaseline.x)) {
      swipeCancelBaseline = { x: clientX, y: swipeCancelBaseline.y };
    }

    const deltaX = clientX - dragStartPos.x;
    const deltaY = clientY - dragStartPos.y;
    const cancelDeltaY = clientY - swipeCancelBaseline.y;
    const cancelDeltaX = clientX - swipeCancelBaseline.x;

    // PSH-PTR-14 and PSH-PTR-15.
    if (!intendedSwipeDirection) {
      let candidate: SwipeDirection;
      if (Math.abs(deltaX) >= Math.abs(deltaY)) {
        candidate = deltaX > 0 ? 'right' : 'left';
      } else {
        candidate = deltaY > 0 ? 'down' : 'up';
      }

      const isAllowed =
        (candidate === 'left' && allowLeft) ||
        (candidate === 'right' && allowRight) ||
        (candidate === 'up' && allowUp) ||
        (candidate === 'down' && allowDown);
      if (isAllowed) {
        intendedSwipeDirection = candidate;
        maxSwipeDisplacement = getDisplacement(candidate, deltaX, deltaY);
        currentSwipeDirection = candidate;
        resolveSwipeThreshold(candidate);
      }
    } else {
      const direction = intendedSwipeDirection;
      const currentDisplacement = getDisplacement(direction, cancelDeltaX, cancelDeltaY);
      if (currentDisplacement > swipeThreshold) {
        cancelledSwipe = false;
        currentSwipeDirection = direction;
      } else if (!(allowLeft && allowRight) && !(allowUp && allowDown) && maxSwipeDisplacement - currentDisplacement >= REVERSE_CANCEL_THRESHOLD) {
        // A change of mind.
        cancelledSwipe = true;
      }
    }

    // PSH-PTR-16.
    const dampedDelta = applyDirectionalDamping(deltaX, deltaY);
    let newOffsetX = initialTransform.x;
    let newOffsetY = initialTransform.y;

    if (hasHorizontal) {
      newOffsetX += dampedDelta.x;
    }
    if (hasVertical) {
      newOffsetY += dampedDelta.y;
    }

    // The drag styles are rewritten only when the offset changed: they carry the raw frozen transform, and a consumer's
    // progress listener overwrites them with damped styles, but an unchanged report is deduplicated and never reaches
    // it (useSwipeDismiss.ts:676-682).
    const offsetChanged = newOffsetX !== dragOffset.x || newOffsetY !== dragOffset.y;

    dragOffset = { x: newOffsetX, y: newOffsetY };
    if (offsetChanged) {
      syncDragStyles(true);
    }
    recordDragSample({ x: newOffsetX, y: newOffsetY }, getValidTimeStamp(input.nativeEvent.timeStamp));
    const dragDeltaX = newOffsetX - initialTransform.x;
    const dragDeltaY = newOffsetY - initialTransform.y;

    // PSH-PTR-17.
    let progress = 0;
    const progressDirection = primaryDirection ?? intendedSwipeDirection;
    if (progressDirection) {
      const size = progressDirection === 'left' || progressDirection === 'right' ? elementSize.width : elementSize.height;
      const scale = initialTransform.scale || 1;
      const progressDisplacement = getDisplacement(progressDirection, dragDeltaX, dragDeltaY);
      if (size > 0 && scale > 0 && progressDisplacement > 0) {
        progress = progressDisplacement / (size * scale);
      }
    }

    updateSwipeProgress(progress, { deltaX: dragDeltaX, deltaY: dragDeltaY, direction: intendedSwipeDirection });
  }

  // PSH-PTR-23..26 and PSH-ALG-11.
  function handleEnd(input: SwipeInput): void {
    if (!options.enabled) {
      return;
    }

    const resolvedDragOffset = dragOffset;
    const resolvedInitialTransform = initialTransform;
    const releaseDeltaX = resolvedDragOffset.x - resolvedInitialTransform.x;
    const releaseDeltaY = resolvedDragOffset.y - resolvedInitialTransform.y;
    const progressDetails: SwipeProgressDetails = { deltaX: releaseDeltaX, deltaY: releaseDeltaY, direction: intendedSwipeDirection };

    if (!isSwiping) {
      resetPendingSwipeState();
      updateSwipeProgress(0, progressDetails);
      return;
    }

    setSwiping(false);
    resetPendingSwipeState();
    sawPrimaryButtonsOnMove = false;

    const element = options.element();
    if (element && !input.touch) {
      safelyChangePointerCapture(element, (input.nativeEvent as PointerEvent).pointerId, 'releasePointerCapture');
    }

    const deltaX = releaseDeltaX;
    const deltaY = releaseDeltaY;
    const startTime = swipeStartTime;
    const endTime = getValidTimeStamp(input.nativeEvent.timeStamp);
    const durationMs = startTime !== null && endTime !== null && endTime > startTime ? endTime - startTime : 0;
    const velocityDurationMs = durationMs > 0 ? Math.max(durationMs, MIN_VELOCITY_DURATION_MS) : 0;
    const velocityX = velocityDurationMs > 0 ? deltaX / velocityDurationMs : 0;
    const velocityY = velocityDurationMs > 0 ? deltaY / velocityDurationMs : 0;
    let releaseVelocityX = lastDragVelocity.x;
    let releaseVelocityY = lastDragVelocity.y;
    const lastSample = lastDragSample;
    if (lastSample && endTime !== null && endTime >= lastSample.time) {
      const ageMs = endTime - lastSample.time;
      if (ageMs <= MAX_RELEASE_VELOCITY_AGE_MS) {
        const sampleDurationMs = Math.max(ageMs, MIN_RELEASE_VELOCITY_DURATION_MS);
        const deltaFromLastSampleX = resolvedDragOffset.x - lastSample.x;
        const deltaFromLastSampleY = resolvedDragOffset.y - lastSample.y;
        if (Math.abs(deltaFromLastSampleX) >= MIN_VELOCITY_SAMPLE_DISTANCE) {
          releaseVelocityX = deltaFromLastSampleX / sampleDurationMs;
        }
        if (Math.abs(deltaFromLastSampleY) >= MIN_VELOCITY_SAMPLE_DISTANCE) {
          releaseVelocityY = deltaFromLastSampleY / sampleDurationMs;
        }
      } else {
        releaseVelocityX = 0;
        releaseVelocityY = 0;
      }
    }

    const releaseDecision = options.onRelease?.({
      event: input.nativeEvent,
      direction: intendedSwipeDirection,
      deltaX,
      deltaY,
      velocityX,
      velocityY,
      releaseVelocityX,
      releaseVelocityY,
    });
    const hasReleaseDecision = typeof releaseDecision === 'boolean';

    if (cancelledSwipe && !hasReleaseDecision) {
      dragOffset = { x: resolvedInitialTransform.x, y: resolvedInitialTransform.y };
      currentSwipeDirection = undefined;
      syncDragStyles(false);
      updateSwipeProgress(0, progressDetails);
      return;
    }

    let shouldClose = false;
    let dismissDirection: SwipeDirection | undefined;

    if (hasReleaseDecision) {
      shouldClose = releaseDecision;
      dismissDirection = intendedSwipeDirection ?? primaryDirection;
    } else {
      for (const direction of options.directions) {
        if (getDisplacement(direction, deltaX, deltaY) > swipeThreshold) {
          shouldClose = true;
          dismissDirection = direction;
          break;
        }
      }
    }

    // PSH-PTR-25.
    if (shouldClose && dismissDirection) {
      currentSwipeDirection = dismissDirection;
      dragDismissed = true;
      syncDragStyles(false);
      options.onDismiss?.(input.nativeEvent, { direction: dismissDirection });
    } else {
      dragOffset = { x: resolvedInitialTransform.x, y: resolvedInitialTransform.y };
      currentSwipeDirection = undefined;
      syncDragStyles(false);
      updateSwipeProgress(0, progressDetails);
    }
  }

  function handleMove(input: SwipeInput): void {
    const currentPos = getPrimaryPointerPosition(input);
    if (!currentPos) {
      return;
    }

    let endAfterMove = false;

    if (!input.touch) {
      const event = input.nativeEvent as PointerEvent;
      const hasPrimaryButton = hasPrimaryMouseButton(event.buttons);
      if (hasPrimaryButton) {
        sawPrimaryButtonsOnMove = true;
      }

      // PSH-PTR-20: a non-primary button taking over (a right click during a drag) cancels.
      if (event.buttons !== 0 && !hasPrimaryButton) {
        cancelSwipeInteraction(event);
        return;
      }

      // PSH-PTR-21: a `buttons: 0` move means the primary button was already released, even if no pointerup reached
      // the element; on fast trackpad flicks it arrives just before pointerup and is the release.
      if (event.buttons === 0 && sawPrimaryButtonsOnMove) {
        if (!isSwiping) {
          // The gesture never activated: discard it.
          handleEnd(input);
          return;
        }
        // The release move can carry the threshold-crossing displacement and the peak velocity, so it is applied
        // before the release is committed.
        endAfterMove = true;
      }
    }

    if (!isSwiping && pendingSwipe) {
      if (!isTouchLike(input) && input.nativeEvent.defaultPrevented) {
        resetPendingSwipeState();
        return;
      }

      const allowedToStart = options.canStart
        ? options.canStart(currentPos, { nativeEvent: input.nativeEvent, direction: primaryDirection })
        : true;

      if (allowedToStart) {
        const pendingStartPos = pendingSwipeStartPos;
        let ignoreScrollableOnStart = false;
        if (isTouchLike(input)) {
          const element = options.element();
          if (pendingStartPos && element) {
            const target = getTargetAtPoint(currentPos, input.nativeEvent);
            const body = ownerDocument(element).body;
            const scrollTarget = body ? findGestureScrollableTouchTarget(target, body) : null;

            if (scrollTarget && (contains(element, scrollTarget) || contains(scrollTarget, element))) {
              const canSwipeFromEdge = canSwipeFromScrollEdgeOnPendingMove(
                scrollTarget,
                currentPos.x - pendingStartPos.x,
                currentPos.y - pendingStartPos.y,
              );

              if (canSwipeFromEdge === false) {
                return;
              }

              if (canSwipeFromEdge === true) {
                ignoreScrollableOnStart = true;
              }
            }
          }
        }

        const started = startSwipeAtPosition(input, currentPos, {
          ignoreScrollableTarget: ignoreScrollableOnStart,
          ignoreScrollableAncestors: ignoreScrollableOnStart,
        });
        if (started) {
          clearPendingSwipeStartState();
          if (pendingStartPos && ignoreScrollableOnStart) {
            // The displacement between the press and the move that activates a swipe from a scroll edge is kept, so
            // quick flicks can dismiss.
            dragStartPos = pendingStartPos;
            swipeCancelBaseline = pendingStartPos;
            lastMovePos = pendingStartPos;
            isFirstPointerMove = false;
          } else {
            // The swipe starts from the current in-bounds position without dropping the next move's displacement,
            // which avoids a jump when the pointer enters from outside the element.
            swipeFromScrollable = false;
          }
        }
      }
    }

    const previousPos = lastMovePos;
    const movement = previousPos === null ? { x: 0, y: 0 } : { x: currentPos.x - previousPos.x, y: currentPos.y - previousPos.y };

    lastMovePos = currentPos;
    handleMoveCore(input, currentPos, movement);

    if (endAfterMove) {
      handleEnd(input);
    }
  }

  const onPointerDown = (event: PointerEvent) => handleStart(pointerInput(event));
  const onPointerMove = (event: PointerEvent) => handleMove(pointerInput(event));
  const onPointerEnd = (event: PointerEvent) => handleEnd(pointerInput(event));
  const onTouchStart = (event: TouchEvent) => handleStart(touchInput(event));
  const onTouchMove = (event: TouchEvent) => handleMove(touchInput(event));
  const onTouchEnd = (event: TouchEvent) => handleEnd(touchInput(event));

  return {
    get swiping() {
      return isSwiping;
    },
    get swipeDirection() {
      return currentSwipeDirection;
    },
    get dragDismissed() {
      return dragDismissed;
    },
    setOptions(next) {
      options = next;
      derive();
    },
    getPointerProps() {
      return options.enabled
        ? { pointerdown: onPointerDown, pointermove: onPointerMove, pointerup: onPointerEnd, pointercancel: onPointerEnd }
        : {};
    },
    getTouchProps() {
      return options.enabled ? { touchstart: onTouchStart, touchmove: onTouchMove, touchend: onTouchEnd, touchcancel: onTouchEnd } : {};
    },
    moveNative(event, currentTarget) {
      handleMove(touchInput(event, currentTarget));
    },
    // PSH-DOM-17's render values: the live gesture, never a stale flag, so a render that commits mid-gesture keeps the
    // transform the imperative writer set.
    getDragStyles() {
      const { x, y } = options.movementCssVars;
      const deltaX = dragOffset.x - initialTransform.x;
      const deltaY = dragOffset.y - initialTransform.y;

      if (!isSwiping && deltaX === 0 && deltaY === 0 && !dragDismissed) {
        return { [x]: '0px', [y]: '0px' };
      }

      const styles: Record<string, string> = {};
      if (isSwiping) {
        // While swiping, the element is frozen at its current visual transform so it does not snap to the end position.
        styles.transition = 'none';
        styles.transform = getDragTransform(dragOffset, initialTransform.scale);
      }
      styles[x] = `${deltaX}px`;
      styles[y] = `${deltaY}px`;
      return styles;
    },
    reset,
  };
}
