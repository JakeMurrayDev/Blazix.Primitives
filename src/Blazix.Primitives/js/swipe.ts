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

// PSH-EVT-09.
/** What a release reports before its decision. Velocities are in pixels per millisecond. */
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

// The port's style sink (M6 track 8b): the Drawer Popup's `style` belongs to dom's style writer.
/** Where a tracker writes its drag styles instead of the element's inline style: a band of a shared style writer. */
export interface SwipeStyleSink {
  /** Sets a declaration. */
  set(property: string, value: string): void;
  /** Removes a declaration the tracker set, handing the property back to whatever else declares it. */
  remove(property: string): void;
}

// PSH §6.1's swipe tracker rows.
/** The options of a swipe tracker. */
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
  /**
   * Gates the start of a gesture; evaluated on the press and on each later move while the press is pending. The position
   * and details objects are reused between calls, so read them at once.
   */
  canStart?: ((position: SwipePoint, details: { nativeEvent: PointerEvent | TouchEvent; direction: SwipeDirection | undefined }) => boolean) | undefined;
  /** Whether a gesture refuses to start inside a scrollable ancestor of the target on the gesture axis; `false` by default. */
  ignoreScrollableAncestors?: boolean | undefined;
  /** Whether touch input also refuses to start on the ignored interactive targets; `true` by default. */
  ignoreSelectorWhenTouch?: boolean | undefined;
  /** Whether the tracker writes the drag transform and movement variables to the element; `true` by default. */
  trackDrag?: boolean | undefined;
  /**
   * Where the drag styles go: a band of the shared style writer of the tracked element, which the owning module passes.
   * It receives `transition: none`, the transform and the movement variables while swiping, and at the release or cancel
   * the removal of the first two (its counterpart of restoring the inline values) and the final movement.
   */
  style: SwipeStyleSink;
  onSwipeStart?: ((event: PointerEvent | TouchEvent) => void) | undefined;
  /** Called when the progress or its details change; the details object is reused between calls, so read it at once. */
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

// PSH-PTR-28.
/** A swipe tracker. */
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
  // PSH-DOM-17.
  /** The drag styles a render of the element carries: property name to value. */
  getDragStyles(): Record<string, string>;
  // PSH-PTR-27.
  /** Returns the tracker to its initial state and restores the element's inline styles. */
  reset(): void;
}

// Design point 8 (a) of the Drawer track (chosen by the user 2026-11-19, ADR 04 Amendments 2026-11-19): the tracker
// allocates no object per move. Base UI's refs of `{ x, y }` objects are scalar fields here, the progress details and
// the `canStart` arguments are one reused object each, and the handlers take the event's fields as arguments rather
// than a wrapper. Allocation remains at a press, a release, a cancel and a `getDragStyles()` read; a move builds only the
// CSS strings the drag styles write (PSH-DOM-17).

function getValidTimeStamp(timeStamp: number): number | null {
  return Number.isFinite(timeStamp) && timeStamp > 0 ? timeStamp : null;
}

function getDragTransform(x: number, y: number, scale: number): string {
  return `translate3d(${x}px,${y}px,0) scale(${scale})`;
}

function canSwipeOnAxis(delta: number, scrollOffset: number, maxScrollOffset: number, allowTowardStart: boolean, allowTowardEnd: boolean): boolean {
  return (delta > 0 && scrollOffset <= 0 && allowTowardStart) || (delta < 0 && scrollOffset >= Math.max(0, maxScrollOffset) && allowTowardEnd);
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
  // The port (the orchestrator's review of 2026-11-19): every module writes `style` through dom's writer, so the drag
  // styles go only through the sink the module passes.
  if (!initialOptions.style) {
    throw new Error('createSwipeTracker: a style sink is required.');
  }
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

  let dragStartX = 0;
  let dragStartY = 0;
  let dragOffsetX = 0;
  let dragOffsetY = 0;
  let hasLastMove = false;
  let lastMoveX = 0;
  let lastMoveY = 0;
  let initialX = 0;
  let initialY = 0;
  let initialScale = 1;
  let intendedSwipeDirection: SwipeDirection | undefined;
  let maxSwipeDisplacement = 0;
  let cancelledSwipe = false;
  let baselineX = 0;
  let baselineY = 0;
  let isFirstPointerMove = false;
  let pendingSwipe = false;
  let hasPendingStart = false;
  let pendingStartX = 0;
  let pendingStartY = 0;
  let swipeFromScrollable = false;
  let sawPrimaryButtonsOnMove = false;
  let elementWidth = 0;
  let elementHeight = 0;
  let swipeProgress = 0;
  let swipeThreshold = swipeThresholdDefault;
  let swipeThresholdFunction: ((details: { element: HTMLElement; direction: SwipeDirection }) => number) | null = null;
  let swipeStartTime: number | null = null;
  let hasSample = false;
  let sampleX = 0;
  let sampleY = 0;
  let sampleTime = 0;
  let hasStationarySample = false;
  let velocityX = 0;
  let velocityY = 0;
  let hasLastDetails = false;
  // Whether the sink holds this gesture's `transition` and `transform`.
  let sinkDragging = false;

  // The reused objects: the progress details and the `canStart` arguments.
  const progressDetails: SwipeProgressDetails = { deltaX: 0, deltaY: 0, direction: undefined };
  const startPoint: SwipePoint = { x: 0, y: 0 };
  const startDetails: { nativeEvent: PointerEvent | TouchEvent; direction: SwipeDirection | undefined } = {
    nativeEvent: undefined as unknown as PointerEvent,
    direction: undefined,
  };

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

  // PSH-ALG-09's report. `withDetails` false is Base UI's call without details.
  function updateSwipeProgress(progress: number, withDetails: boolean, deltaX = 0, deltaY = 0, direction?: SwipeDirection): void {
    const nextProgress = Number.isFinite(progress) ? clamp(progress, 0, 1) : 0;
    const progressChanged = nextProgress !== swipeProgress;
    const detailsChanged =
      withDetails &&
      (!hasLastDetails || progressDetails.deltaX !== deltaX || progressDetails.deltaY !== deltaY || progressDetails.direction !== direction);

    if (!progressChanged && !detailsChanged) {
      return;
    }

    swipeProgress = nextProgress;
    if (withDetails) {
      hasLastDetails = true;
      progressDetails.deltaX = deltaX;
      progressDetails.deltaY = deltaY;
      progressDetails.direction = direction;
      options.onProgress?.(nextProgress, progressDetails);
    } else {
      hasLastDetails = false;
      options.onProgress?.(nextProgress, undefined);
    }
  }

  function allowedToStart(nativeEvent: PointerEvent | TouchEvent, x: number, y: number): boolean {
    if (!options.canStart) {
      return true;
    }
    startPoint.x = x;
    startPoint.y = y;
    startDetails.nativeEvent = nativeEvent;
    startDetails.direction = primaryDirection;
    return options.canStart(startPoint, startDetails);
  }

  // PSH-DOM-17's imperative writer.
  function syncDragStyles(swiping: boolean): void {
    if (!(options.trackDrag ?? true)) {
      return;
    }

    const sink = options.style;
    if (swiping) {
      sinkDragging = true;
      sink.set('transition', 'none');
      sink.set('transform', getDragTransform(dragOffsetX, dragOffsetY, initialScale));
    } else if (sinkDragging) {
      sinkDragging = false;
      sink.remove('transition');
      sink.remove('transform');
    }
    sink.set(options.movementCssVars.x, `${dragOffsetX - initialX}px`);
    sink.set(options.movementCssVars.y, `${dragOffsetY - initialY}px`);
  }

  // PSH-ALG-10's sampling.
  function recordDragSample(x: number, y: number, timeStamp: number | null): void {
    if (timeStamp === null) {
      return;
    }

    if (hasSample && timeStamp > sampleTime) {
      // Some Android devices emit one effectively stationary move immediately before release. The last moving sample
      // and its timestamp are kept; repeated stationary moves still clear the velocity, and a release after a hold
      // still expires the last moving sample.
      const stationary = Math.abs(x - sampleX) < MIN_VELOCITY_SAMPLE_DISTANCE && Math.abs(y - sampleY) < MIN_VELOCITY_SAMPLE_DISTANCE;
      const skipSample = stationary && !hasStationarySample;
      hasStationarySample = stationary;
      if (skipSample) {
        return;
      }

      const durationMs = Math.max(timeStamp - sampleTime, MIN_RELEASE_VELOCITY_DURATION_MS);
      velocityX = (x - sampleX) / durationMs;
      velocityY = (y - sampleY) / durationMs;
    }

    hasSample = true;
    sampleX = x;
    sampleY = y;
    sampleTime = timeStamp;
  }

  // PSH-PTR-27.
  function reset(): void {
    currentSwipeDirection = undefined;
    setSwiping(false);
    dragDismissed = false;
    updateSwipeProgress(0, false);

    swipeThreshold = swipeThresholdDefault;
    swipeThresholdFunction = null;
    dragStartX = dragStartY = 0;
    dragOffsetX = dragOffsetY = 0;
    initialX = initialY = 0;
    initialScale = 1;
    intendedSwipeDirection = undefined;
    maxSwipeDisplacement = 0;
    cancelledSwipe = false;
    baselineX = baselineY = 0;
    isFirstPointerMove = false;
    hasLastMove = false;
    pendingSwipe = false;
    hasPendingStart = false;
    swipeFromScrollable = false;
    sawPrimaryButtonsOnMove = false;
    elementWidth = elementHeight = 0;
    swipeStartTime = null;
    hasSample = false;
    hasStationarySample = false;
    velocityX = velocityY = 0;
    hasLastDetails = false;
    syncDragStyles(false);
  }

  function isTouchLike(nativeEvent: PointerEvent | TouchEvent, touch: boolean): boolean {
    return touch || (nativeEvent as PointerEvent).pointerType === 'touch';
  }

  function getTargetAtPoint(x: number, y: number, nativeEvent: Event): HTMLElement | null {
    const root = options.element()?.getRootNode();
    return (getElementAtPoint(root, x, y) ?? getTarget(nativeEvent)) as HTMLElement | null;
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
    nativeEvent: PointerEvent | TouchEvent,
    touch: boolean,
    x: number,
    y: number,
    ignoreScrollableTarget = false,
    ignoreScrollableAncestorsOnStart = false,
  ): boolean {
    swipeFromScrollable = false;
    const touchLike = isTouchLike(nativeEvent, touch);
    const target = getTargetAtPoint(x, y, nativeEvent);

    const element = options.element();
    const body = ownerDocument(element).body;

    const scrollableTarget = touchLike && body ? findGestureScrollableTouchTarget(target, body) : null;
    if (scrollableTarget && !ignoreScrollableTarget) {
      return false;
    }
    swipeFromScrollable = Boolean(scrollableTarget && ignoreScrollableTarget);

    const isInteractiveElement = closest(target, ignoreSelector);
    if (isInteractiveElement && (!touchLike || (options.ignoreSelectorWhenTouch ?? true))) {
      return false;
    }

    if ((options.ignoreScrollableAncestors ?? false) && element && target) {
      if (!ignoreScrollableAncestorsOnStart && findScrollableAncestor(target, element, scrollAxis) != null) {
        return false;
      }
    }

    cancelledSwipe = false;
    intendedSwipeDirection = undefined;
    maxSwipeDisplacement = 0;

    dragStartX = x;
    dragStartY = y;
    swipeStartTime = getValidTimeStamp(nativeEvent.timeStamp);
    hasSample = false;
    hasStationarySample = false;
    velocityX = velocityY = 0;
    baselineX = x;
    baselineY = y;
    hasLastMove = true;
    lastMoveX = x;
    lastMoveY = y;
    swipeThreshold = swipeThresholdDefault;
    swipeThresholdFunction = typeof options.swipeThreshold === 'function' ? options.swipeThreshold : null;

    if (element) {
      elementWidth = element.offsetWidth;
      elementHeight = element.offsetHeight;
      resolveSwipeThreshold(primaryDirection);
      const transform = getElementTransform(element);

      initialX = transform.x;
      initialY = transform.y;
      initialScale = transform.scale;
      dragOffsetX = transform.x;
      dragOffsetY = transform.y;
      recordDragSample(transform.x, transform.y, swipeStartTime);

      if (!touch) {
        safelyChangePointerCapture(element, (nativeEvent as PointerEvent).pointerId, 'setPointerCapture');
      }
    }

    options.onSwipeStart?.(nativeEvent);

    setSwiping(true);
    isFirstPointerMove = true;
    updateSwipeProgress(0, false);
    syncDragStyles(true);

    return true;
  }

  function clearPendingSwipeStartState(): void {
    pendingSwipe = false;
    hasPendingStart = false;
  }

  function resetPendingSwipeState(): void {
    clearPendingSwipeStartState();
    swipeFromScrollable = false;
    hasLastMove = false;
  }

  // PSH-PTR-20.
  function cancelSwipeInteraction(event: PointerEvent): void {
    resetPendingSwipeState();

    if (!isSwiping) {
      return;
    }

    setSwiping(false);

    dragOffsetX = initialX;
    dragOffsetY = initialY;
    currentSwipeDirection = undefined;
    sawPrimaryButtonsOnMove = false;
    syncDragStyles(false);

    const element = options.element();
    if (element) {
      safelyChangePointerCapture(element, event.pointerId, 'releasePointerCapture');
    }

    updateSwipeProgress(0, true, 0, 0, undefined);

    options.onCancel?.(event);
  }

  // PSH-ALG-08: one axis of the directional damping.
  function damp(delta: number, axisAllowed: boolean, allowNegative: boolean, allowPositive: boolean): number {
    if (!axisAllowed || (!allowNegative && delta < 0) || (!allowPositive && delta > 0)) {
      return Math.sign(delta) * Math.abs(delta) ** 0.5;
    }
    return delta;
  }

  // PSH-ALG-12's edge test: toward the axis start edge (down/right) is allowed when scrolled to the start, toward the
  // end edge (up/left) when scrolled to the end.
  function canSwipeFromScrollEdgeOnPendingMove(scrollTarget: HTMLElement, deltaX: number, deltaY: number): boolean | null {
    if (hasVertical && deltaY !== 0) {
      return canSwipeOnAxis(deltaY, scrollTarget.scrollTop, scrollTarget.scrollHeight - scrollTarget.clientHeight, allowDown, allowUp);
    }

    if (hasHorizontal && deltaX !== 0) {
      return canSwipeOnAxis(deltaX, scrollTarget.scrollLeft, scrollTarget.scrollWidth - scrollTarget.clientWidth, allowRight, allowLeft);
    }

    return null;
  }

  // PSH-PTR-09.
  function handleStart(nativeEvent: PointerEvent | TouchEvent, touch: boolean): void {
    if (!options.enabled || nativeEvent.defaultPrevented) {
      return;
    }

    if (!touch && (nativeEvent as PointerEvent).button !== 0) {
      return;
    }

    let x: number;
    let y: number;
    if (touch) {
      // PSH-EDGE-12.
      const first = (nativeEvent as TouchEvent).touches?.[0];
      if (!first) {
        return;
      }
      x = first.clientX;
      y = first.clientY;
    } else {
      x = (nativeEvent as PointerEvent).clientX;
      y = (nativeEvent as PointerEvent).clientY;
    }

    pendingSwipe = true;
    hasPendingStart = true;
    pendingStartX = x;
    pendingStartY = y;
    swipeFromScrollable = false;
    sawPrimaryButtonsOnMove = !touch;

    if (!allowedToStart(nativeEvent, x, y)) {
      return;
    }

    if (startSwipeAtPosition(nativeEvent, touch, x, y)) {
      clearPendingSwipeStartState();
    }
  }

  // PSH-ALG-07.
  function handleMoveCore(
    nativeEvent: PointerEvent | TouchEvent,
    touch: boolean,
    currentTarget: HTMLElement | null,
    clientX: number,
    clientY: number,
    movementX: number,
    movementY: number,
  ): void {
    if (!options.enabled || !isSwiping) {
      return;
    }

    // PSH-PTR-19.
    if (isTouchLike(nativeEvent, touch) && !swipeFromScrollable && currentTarget) {
      if (findGestureScrollableTouchTarget(getTarget(nativeEvent), currentTarget)) {
        return;
      }
    }

    // PSH-PTR-18: prevents text selection on Safari; touch moves are never prevented.
    if (!touch) {
      nativeEvent.preventDefault();
    }

    // PSH-PTR-13: the first move re-anchors the origin to absorb the gap between the press and the first move (notably
    // on iOS touch); without drag tracking (the swipe area) the press position is kept so a quick flick whose whole
    // travel lands in this one move still registers.
    if (isFirstPointerMove) {
      isFirstPointerMove = false;
      if (options.trackDrag ?? true) {
        dragStartX = clientX;
        dragStartY = clientY;
        const moveTime = getValidTimeStamp(nativeEvent.timeStamp);
        if (moveTime !== null) {
          swipeStartTime = moveTime;
        }
        hasSample = false;
        hasStationarySample = false;
      }
    }

    if ((movementY < 0 && clientY > baselineY) || (movementY > 0 && clientY < baselineY)) {
      baselineY = clientY;
    }

    if ((movementX < 0 && clientX > baselineX) || (movementX > 0 && clientX < baselineX)) {
      baselineX = clientX;
    }

    const deltaX = clientX - dragStartX;
    const deltaY = clientY - dragStartY;

    // PSH-PTR-14 and PSH-PTR-15.
    if (!intendedSwipeDirection) {
      const candidate: SwipeDirection =
        Math.abs(deltaX) >= Math.abs(deltaY) ? (deltaX > 0 ? 'right' : 'left') : deltaY > 0 ? 'down' : 'up';

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
      const currentDisplacement = getDisplacement(direction, clientX - baselineX, clientY - baselineY);
      if (currentDisplacement > swipeThreshold) {
        cancelledSwipe = false;
        currentSwipeDirection = direction;
      } else if (!(allowLeft && allowRight) && !(allowUp && allowDown) && maxSwipeDisplacement - currentDisplacement >= REVERSE_CANCEL_THRESHOLD) {
        // A change of mind.
        cancelledSwipe = true;
      }
    }

    // PSH-PTR-16.
    const newOffsetX = hasHorizontal ? initialX + damp(deltaX, hasHorizontal, allowLeft, allowRight) : initialX;
    const newOffsetY = hasVertical ? initialY + damp(deltaY, hasVertical, allowUp, allowDown) : initialY;

    // The drag styles are rewritten only when the offset changed: they carry the raw frozen transform, and a consumer's
    // progress listener overwrites them with damped styles, but an unchanged report is deduplicated and never reaches
    // it (useSwipeDismiss.ts:676-682).
    const offsetChanged = newOffsetX !== dragOffsetX || newOffsetY !== dragOffsetY;

    dragOffsetX = newOffsetX;
    dragOffsetY = newOffsetY;
    if (offsetChanged) {
      syncDragStyles(true);
    }
    recordDragSample(newOffsetX, newOffsetY, getValidTimeStamp(nativeEvent.timeStamp));
    const dragDeltaX = newOffsetX - initialX;
    const dragDeltaY = newOffsetY - initialY;

    // PSH-PTR-17.
    let progress = 0;
    const progressDirection = primaryDirection ?? intendedSwipeDirection;
    if (progressDirection) {
      const size = progressDirection === 'left' || progressDirection === 'right' ? elementWidth : elementHeight;
      const scale = initialScale || 1;
      const progressDisplacement = getDisplacement(progressDirection, dragDeltaX, dragDeltaY);
      if (size > 0 && scale > 0 && progressDisplacement > 0) {
        progress = progressDisplacement / (size * scale);
      }
    }

    updateSwipeProgress(progress, true, dragDeltaX, dragDeltaY, intendedSwipeDirection);
  }

  // PSH-PTR-23..26 and PSH-ALG-11.
  function handleEnd(nativeEvent: PointerEvent | TouchEvent, touch: boolean): void {
    if (!options.enabled) {
      return;
    }

    const releaseDeltaX = dragOffsetX - initialX;
    const releaseDeltaY = dragOffsetY - initialY;
    const releaseDirection = intendedSwipeDirection;

    if (!isSwiping) {
      resetPendingSwipeState();
      updateSwipeProgress(0, true, releaseDeltaX, releaseDeltaY, releaseDirection);
      return;
    }

    setSwiping(false);
    resetPendingSwipeState();
    sawPrimaryButtonsOnMove = false;

    const element = options.element();
    if (element && !touch) {
      safelyChangePointerCapture(element, (nativeEvent as PointerEvent).pointerId, 'releasePointerCapture');
    }

    const startTime = swipeStartTime;
    const endTime = getValidTimeStamp(nativeEvent.timeStamp);
    const durationMs = startTime !== null && endTime !== null && endTime > startTime ? endTime - startTime : 0;
    const velocityDurationMs = durationMs > 0 ? Math.max(durationMs, MIN_VELOCITY_DURATION_MS) : 0;
    let releaseVelocityX = velocityX;
    let releaseVelocityY = velocityY;
    if (hasSample && endTime !== null && endTime >= sampleTime) {
      const ageMs = endTime - sampleTime;
      if (ageMs <= MAX_RELEASE_VELOCITY_AGE_MS) {
        const sampleDurationMs = Math.max(ageMs, MIN_RELEASE_VELOCITY_DURATION_MS);
        const deltaFromSampleX = dragOffsetX - sampleX;
        const deltaFromSampleY = dragOffsetY - sampleY;
        if (Math.abs(deltaFromSampleX) >= MIN_VELOCITY_SAMPLE_DISTANCE) {
          releaseVelocityX = deltaFromSampleX / sampleDurationMs;
        }
        if (Math.abs(deltaFromSampleY) >= MIN_VELOCITY_SAMPLE_DISTANCE) {
          releaseVelocityY = deltaFromSampleY / sampleDurationMs;
        }
      } else {
        releaseVelocityX = 0;
        releaseVelocityY = 0;
      }
    }

    const releaseDecision = options.onRelease?.({
      event: nativeEvent,
      direction: releaseDirection,
      deltaX: releaseDeltaX,
      deltaY: releaseDeltaY,
      velocityX: velocityDurationMs > 0 ? releaseDeltaX / velocityDurationMs : 0,
      velocityY: velocityDurationMs > 0 ? releaseDeltaY / velocityDurationMs : 0,
      releaseVelocityX,
      releaseVelocityY,
    });
    const hasReleaseDecision = typeof releaseDecision === 'boolean';

    const snapBack = () => {
      dragOffsetX = initialX;
      dragOffsetY = initialY;
      currentSwipeDirection = undefined;
      syncDragStyles(false);
      updateSwipeProgress(0, true, releaseDeltaX, releaseDeltaY, releaseDirection);
    };

    if (cancelledSwipe && !hasReleaseDecision) {
      snapBack();
      return;
    }

    let shouldClose = false;
    let dismissDirection: SwipeDirection | undefined;

    if (hasReleaseDecision) {
      shouldClose = releaseDecision;
      dismissDirection = releaseDirection ?? primaryDirection;
    } else {
      for (const direction of options.directions) {
        if (getDisplacement(direction, releaseDeltaX, releaseDeltaY) > swipeThreshold) {
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
      options.onDismiss?.(nativeEvent, { direction: dismissDirection });
    } else {
      snapBack();
    }
  }

  function handleMove(nativeEvent: PointerEvent | TouchEvent, touch: boolean, currentTarget: HTMLElement | null): void {
    let x: number;
    let y: number;
    if (touch) {
      // PSH-EDGE-12.
      const first = (nativeEvent as TouchEvent).touches?.[0];
      if (!first) {
        return;
      }
      x = first.clientX;
      y = first.clientY;
    } else {
      x = (nativeEvent as PointerEvent).clientX;
      y = (nativeEvent as PointerEvent).clientY;
    }

    let endAfterMove = false;

    if (!touch) {
      const event = nativeEvent as PointerEvent;
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
          handleEnd(nativeEvent, touch);
          return;
        }
        // The release move can carry the threshold-crossing displacement and the peak velocity, so it is applied
        // before the release is committed.
        endAfterMove = true;
      }
    }

    if (!isSwiping && pendingSwipe) {
      const touchLike = isTouchLike(nativeEvent, touch);
      if (!touchLike && nativeEvent.defaultPrevented) {
        resetPendingSwipeState();
        return;
      }

      if (allowedToStart(nativeEvent, x, y)) {
        let ignoreScrollableOnStart = false;
        if (touchLike) {
          const element = options.element();
          if (hasPendingStart && element) {
            const target = getTargetAtPoint(x, y, nativeEvent);
            const body = ownerDocument(element).body;
            const scrollTarget = body ? findGestureScrollableTouchTarget(target, body) : null;

            if (scrollTarget && (contains(element, scrollTarget) || contains(scrollTarget, element))) {
              const canSwipeFromEdge = canSwipeFromScrollEdgeOnPendingMove(scrollTarget, x - pendingStartX, y - pendingStartY);

              if (canSwipeFromEdge === false) {
                return;
              }

              if (canSwipeFromEdge === true) {
                ignoreScrollableOnStart = true;
              }
            }
          }
        }

        const hadPendingStart = hasPendingStart;
        const fromX = pendingStartX;
        const fromY = pendingStartY;
        if (startSwipeAtPosition(nativeEvent, touch, x, y, ignoreScrollableOnStart, ignoreScrollableOnStart)) {
          clearPendingSwipeStartState();
          if (hadPendingStart && ignoreScrollableOnStart) {
            // The displacement between the press and the move that activates a swipe from a scroll edge is kept, so
            // quick flicks can dismiss.
            dragStartX = baselineX = lastMoveX = fromX;
            dragStartY = baselineY = lastMoveY = fromY;
            hasLastMove = true;
            isFirstPointerMove = false;
          } else {
            // The swipe starts from the current in-bounds position without dropping the next move's displacement,
            // which avoids a jump when the pointer enters from outside the element.
            swipeFromScrollable = false;
          }
        }
      }
    }

    const movementX = hasLastMove ? x - lastMoveX : 0;
    const movementY = hasLastMove ? y - lastMoveY : 0;

    hasLastMove = true;
    lastMoveX = x;
    lastMoveY = y;
    handleMoveCore(nativeEvent, touch, currentTarget, x, y, movementX, movementY);

    if (endAfterMove) {
      handleEnd(nativeEvent, touch);
    }
  }

  const onPointerDown = (event: PointerEvent) => handleStart(event, false);
  const onPointerMove = (event: PointerEvent) => handleMove(event, false, event.currentTarget as HTMLElement | null);
  const onPointerEnd = (event: PointerEvent) => handleEnd(event, false);
  const onTouchStart = (event: TouchEvent) => handleStart(event, true);
  const onTouchMove = (event: TouchEvent) => handleMove(event, true, event.currentTarget as HTMLElement | null);
  const onTouchEnd = (event: TouchEvent) => handleEnd(event, true);
  const pointerProps: SwipePointerHandlers = { pointerdown: onPointerDown, pointermove: onPointerMove, pointerup: onPointerEnd, pointercancel: onPointerEnd };
  const touchProps: SwipeTouchHandlers = { touchstart: onTouchStart, touchmove: onTouchMove, touchend: onTouchEnd, touchcancel: onTouchEnd };

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
      return options.enabled ? pointerProps : {};
    },
    getTouchProps() {
      return options.enabled ? touchProps : {};
    },
    moveNative(event, currentTarget) {
      handleMove(event, true, currentTarget);
    },
    // PSH-DOM-17's render values: the live gesture, never a stale flag, so a render that commits mid-gesture keeps the
    // transform the imperative writer set.
    getDragStyles() {
      const { x, y } = options.movementCssVars;
      const deltaX = dragOffsetX - initialX;
      const deltaY = dragOffsetY - initialY;

      if (!isSwiping && deltaX === 0 && deltaY === 0 && !dragDismissed) {
        return { [x]: '0px', [y]: '0px' };
      }

      const styles: Record<string, string> = {};
      if (isSwiping) {
        // While swiping, the element is frozen at its current visual transform so it does not snap to the end position.
        styles.transition = 'none';
        styles.transform = getDragTransform(dragOffsetX, dragOffsetY, initialScale);
      }
      styles[x] = `${deltaX}px`;
      styles[y] = `${deltaY}px`;
      return styles;
    },
    reset,
  };
}
