// The `toast` component module (ADR 04 §1; its scope in ADR 04 Amendments 2026-11-18, the `toast` cell; the 9b design
// points of 2026-11-19, chosen by the user): Toast's own swipe gesture and its movement styles, the Root's measurement and
// the Content observers, the Viewport's hover, focus, F6, window and touch listeners, the routing of the focus guards C#
// renders, and the focus hand-off C# computes at close. One `ToastTwin` per part, three part literals (DP1 (a)); the
// contract is docs/components/toast.md (§5.2, §6, §9). Transcribed from Base UI v1.9.0: toast/root/ToastRoot.tsx,
// toast/viewport/ToastViewport.tsx, toast/content/ToastContent.tsx and the focus half of toast/store.ts.
//
// The store, its timers and every derived flag are C#'s (ADR 01 decision 4; the M6 plan's decision 19 (a)): the module
// reports one message per state change (ADR 04 §3), never per move or per frame, and C# applies each report to the store.
// Every report carries a revision, and each options record carries the revision of the last report C# applied; an update
// older than the module's last report is ignored unless it carries a new lifecycle, the one rollback (ADR 04 Amendments
// 2026-11-18, decision 6 (a): Toast's store never refuses a close, so a reinitialised Root is the only reset).
import {
  activeElement,
  closest,
  contains,
  getElementTransform,
  getTarget,
  isHTMLElement,
  ownerDocument,
  ownerWindow,
  styleWriter,
  type StyleLayer,
} from './dom';
import { matchesFocusVisible } from './interactions';
import {
  BASE_UI_SWIPE_IGNORE_SELECTOR,
  DEFAULT_SWIPE_THRESHOLD,
  LEGACY_SWIPE_IGNORE_SELECTOR,
  REVERSE_CANCEL_THRESHOLD,
  getDisplacement,
  type SwipeDirection,
} from './swipe';
import {
  attachTarget,
  createTwinChannel,
  parseOptions,
  resolveReference,
  type AttachTarget,
  type DotNetTwin,
  type TwinCaptures,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

// ---------------------------------------------------------------------------------------------------------------
// Constants (toast/root/ToastRoot.tsx:35-39)
// ---------------------------------------------------------------------------------------------------------------

// TST-PTR-16.
/** The damping exponent of a drag component in a direction that does not dismiss. */
export const OPPOSITE_DIRECTION_DAMPING_FACTOR = 0.5;

// TST-PTR-13.
/** The drag, in pixels, after which a press becomes a swipe. */
export const MIN_DRAG_THRESHOLD = 1;

// TST-PTR-08: Toast's interactive list joined with both ignore selectors (UTL-EDGE-19; DRW-PTR-24 keeps the legacy one for
// Toast only).
/** The targets a toast's swipe never starts on. */
export const TOAST_SWIPE_IGNORE_SELECTOR = `button,a,input,textarea,[role="button"],${BASE_UI_SWIPE_IGNORE_SELECTOR},${LEGACY_SWIPE_IGNORE_SELECTOR}`;

// The guard marker PSH-STR-08 renders (utils/FocusGuard.tsx:39); the `portal` module keeps its own constant private.
const FOCUS_GUARD_ATTRIBUTE = 'data-base-ui-focus-guard';

const SWIPING_ATTRIBUTE = 'data-swiping';
const SWIPE_DIRECTION_ATTRIBUTE = 'data-swipe-direction';
const ENDING_STYLE_ATTRIBUTE = 'data-ending-style';
const MOVEMENT_X = '--toast-swipe-movement-x';
const MOVEMENT_Y = '--toast-swipe-movement-y';

// ADR 04 Amendments 2026-11-18, the bands paragraph (decision 5 as revised, chosen by the user): Base UI passes the drag
// styles through the Root's merged `style` prop, where the consumer's wins (ToastRoot.tsx:445-459,476-482), and writes the
// measurement's `height` on `element.style` after render (ToastRoot.tsx:124-129).
const DRAG_BAND = 'component-under';
const MEASUREMENT_BAND = 'component';

const NOOP = () => {};

// ---------------------------------------------------------------------------------------------------------------
// Options and reports
// ---------------------------------------------------------------------------------------------------------------

/** The part a handle serves; mirrors the part literals of `ToastTwin` in C#. */
export type ToastPart = 'viewport' | 'root' | 'content';

// TST-FOC-07, TST-FOC-08.
/** A focus hand-off C# computed at a close. */
export interface ToastHandOff {
  /** Increases with each close; the module acts on a revision once. */
  revision: number;
  /** Whether focus returns to the element focused before F6 rather than to a toast. */
  toPrevious: boolean;
  /** The index, among the references, of the Root to focus; `null` with `toPrevious` or when it has no element. */
  targetRef?: number | null;
}

/** The options of one part's handle; mirrors `ToastOptions` in C#. */
export interface ToastOptions extends TwinOptions {
  part: ToastPart;
  /** Viewport and Root: the revision of the last report C# applied. */
  revision?: number;
  // TST-FOC-14.
  /** Viewport: whether the list is empty, so the window and document listeners are unbound. */
  empty?: boolean;
  /** Viewport: the store's `hovering`. */
  hovering?: boolean;
  /** Viewport: the store's `focused`. */
  focused?: boolean;
  // TST-PTR-03.
  /** Viewport: whether some toast is ending, which defers a `mouseleave`. */
  hasEnding?: boolean;
  // TST-FOC-02.
  /** Viewport: the index, among the references, of the first toast that is neither ending nor limited. */
  focusTargetRef?: number | null;
  /** Viewport: the hand-off of the latest close. */
  handOff?: ToastHandOff | null;
  // TST-PTR-22.
  /** Root: the dismiss directions, in the order a release tries them; empty for an anchored toast. */
  directions?: SwipeDirection[];
  // TST-ANI-08/11.
  /** Root: increases when the Root starts a new lifecycle (a new toast id, or a re-add): the rollback. */
  lifecycle?: number;
  /** Content: the index, among the references, of its Root's element. */
  rootRef?: number | null;
}

/** What a Viewport reports; C# applies each to the store. */
export type ToastViewportReportKind =
  | 'enter'
  | 'leave'
  | 'focus'
  | 'blur'
  | 'f6'
  | 'window-blur'
  | 'window-focus'
  | 'window-focused'
  | 'touch-outside';

/** What a Root reports; C# applies each to the store or the Root's mirrored state. */
export type ToastRootReportKind = 'height' | 'swipe-start' | 'direction' | 'release' | 'escape' | 'touch-press';

/** One report; mirrors `ToastReport` in C#. */
export interface ToastReport {
  revision: number;
  kind: ToastViewportReportKind | ToastRootReportKind;
  /** `height`: the measured height in pixels. */
  height?: number;
  /** `height`: the lifecycle it was measured in. */
  lifecycle?: number;
  /** `direction` and `release`: the armed or dismissing direction; `null` on a release that snaps back. */
  direction?: SwipeDirection | null;
  /** `release`: whether the release dismisses the toast. */
  dismiss?: boolean;
  // TST-FOC-12.
  /** `window-focus`: whether the timers resume at once. */
  resume?: boolean;
}

const REPORT_METHOD = 'OnReport';

// ---------------------------------------------------------------------------------------------------------------
// The gesture's arithmetic (TST-PTR-12..17; ToastRoot.tsx:180-197,294-407), pure so L3 can drive it
// ---------------------------------------------------------------------------------------------------------------

// TST-PTR-13.
/** The axis a two-axis gesture is locked to once it becomes a swipe. */
export type SwipeAxisLock = 'horizontal' | 'vertical' | null;

// TST-PTR-16.
/**
 * Damps a drag component that points in a direction that does not dismiss.
 * @param directions The dismiss directions.
 * @param deltaX The horizontal drag.
 * @param deltaY The vertical drag.
 * @returns The drag, each component not in an allowed direction replaced by `sign(d)·|d|^0.5`.
 */
export function applyDirectionalDamping(directions: readonly SwipeDirection[], deltaX: number, deltaY: number): { x: number; y: number } {
  const damp = (delta: number) =>
    delta > 0 ? delta ** OPPOSITE_DIRECTION_DAMPING_FACTOR : -(Math.abs(delta) ** OPPOSITE_DIRECTION_DAMPING_FACTOR);
  const dampX = (deltaX > 0 && !directions.includes('right')) || (deltaX < 0 && !directions.includes('left'));
  const dampY = (deltaY > 0 && !directions.includes('down')) || (deltaY < 0 && !directions.includes('up'));
  return { x: dampX ? damp(deltaX) : deltaX, y: dampY ? damp(deltaY) : deltaY };
}

// TST-PTR-14.
/**
 * The direction a move would arm while none is armed.
 * @param lock The axis lock.
 * @param deltaX The horizontal drag.
 * @param deltaY The vertical drag.
 * @returns The candidate; `undefined` when a locked axis has no movement.
 */
export function armingCandidate(lock: SwipeAxisLock, deltaX: number, deltaY: number): SwipeDirection | undefined {
  if (lock === 'vertical') {
    return deltaY > 0 ? 'down' : deltaY < 0 ? 'up' : undefined;
  }
  if (lock === 'horizontal') {
    return deltaX > 0 ? 'right' : deltaX < 0 ? 'left' : undefined;
  }
  if (Math.abs(deltaX) >= Math.abs(deltaY)) {
    return deltaX > 0 ? 'right' : 'left';
  }
  return deltaY > 0 ? 'down' : 'up';
}

// TST-PTR-17.
/**
 * The direction a release dismisses in.
 * @param directions The dismiss directions, in order.
 * @param deltaX The drag from the initial transform.
 * @param deltaY The drag from the initial transform.
 * @returns The first direction whose displacement exceeds the threshold, or `undefined`.
 */
export function dismissDirection(directions: readonly SwipeDirection[], deltaX: number, deltaY: number): SwipeDirection | undefined {
  for (const direction of directions) {
    if (getDisplacement(direction, deltaX, deltaY) > DEFAULT_SWIPE_THRESHOLD) {
      return direction;
    }
  }
  return undefined;
}

// ---------------------------------------------------------------------------------------------------------------
// Shared helpers
// ---------------------------------------------------------------------------------------------------------------

function setFlag(element: Element, name: string, value: string | null): void {
  if (value === null) {
    if (element.hasAttribute(name)) {
      element.removeAttribute(name);
    }
  } else if (element.getAttribute(name) !== value) {
    element.setAttribute(name, value);
  }
}

// The handover (ADR 04 Amendments 2026-10-18): C# froze the rendered style at the attach; from here the writer composes the
// consumer's merged string with the module's bands.
const takenOver = Symbol('iodine.toast.takenOver');

function takeOver(element: HTMLElement, style: string | null | undefined): void {
  const marked = element as unknown as Record<symbol, boolean>;
  if (!marked[takenOver]) {
    marked[takenOver] = true;
    element.style.cssText = '';
  }
  styleWriter(element).setConsumer(style ?? null);
}

// PR 385's verifier nit: `isHTMLElement` also recognises a guard of a same-origin iframe's realm (issue 316).
function isGuard(element: unknown): element is HTMLElement {
  return isHTMLElement(element) && element.hasAttribute(FOCUS_GUARD_ATTRIBUTE);
}

// The Root's measurement, published on its element so the Content's handle finds it without a module-level registry.
const rootMeasure = Symbol('iodine.toast.measure');

interface MeasuredRoot extends HTMLElement {
  [rootMeasure]?: () => void;
}

// ---------------------------------------------------------------------------------------------------------------
// attach
// ---------------------------------------------------------------------------------------------------------------

/**
 * Attaches one Toast part's handle.
 * @param elementOrId The part's element or its id.
 * @param optionsJson The JSON of a `ToastOptions`.
 * @param dotNetRef The part's twin.
 * @param references The elements the options name by index.
 * @param captures The capture ids of the elements that crossed.
 * @returns The handle.
 */
export function attach(
  elementOrId: AttachTarget,
  optionsJson: string,
  dotNetRef: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const element = attachTarget(elementOrId, captures);
  const options = parseOptions<ToastOptions>(optionsJson);
  if (!element) {
    return { update: NOOP, dispose: NOOP };
  }

  switch (options.part) {
    case 'viewport':
      return attachViewport(element, options, createTwinChannel(dotNetRef), references, captures);
    case 'root':
      return attachRoot(element, options, createTwinChannel(dotNetRef));
    case 'content':
      return attachContent(element, options, references, captures);
    default:
      return { update: NOOP, dispose: NOOP };
  }
}

// ---------------------------------------------------------------------------------------------------------------
// The Viewport (TST-KEY-01..04, TST-FOC-01..14, TST-PTR-01..07, TST-PTR-25, TST-EDGE-07/08; ToastViewport.tsx:45-227)
// ---------------------------------------------------------------------------------------------------------------

function attachViewport(
  viewport: HTMLElement,
  initial: ToastOptions,
  channel: TwinChannel,
  initialReferences: TwinReferences,
  initialCaptures: TwinCaptures,
): TwinHandle {
  let options = initial;
  let references = initialReferences;
  let captures = initialCaptures;
  let revision = 0;
  // The store's flags as the module last learnt them, moved ahead by its own reports (the optimistic view).
  const view = { hovering: options.hovering ?? false, focused: options.focused ?? false };
  // A report that may have resumed the timers while the pointer stays over the viewport; the next move pauses them again,
  // as Base UI's every `mousemove` does (ToastViewport.tsx:153-157,218-219).
  let pausePending = false;
  let previousFocus: HTMLElement | null = null;
  let handlingFocusGuard = false;
  let markedReadyForMouseLeave = false;
  let touchActive = false;
  let windowFocusTimer: ReturnType<typeof setTimeout> | undefined;
  let windowBlurred = false;
  let unbindGlobal: (() => void) | null = null;
  let handledHandOff = options.handOff?.revision ?? 0;
  let disposed = false;

  const win = ownerWindow(viewport);
  const doc = ownerDocument(viewport);

  function report(kind: ToastViewportReportKind, extra?: Partial<ToastReport>): void {
    revision++;
    channel.report(REPORT_METHOD, { revision, kind, ...extra } satisfies ToastReport);
  }

  // TST-FOC-04 (store.ts:398-400).
  function restoreFocusToPrevElement(): void {
    previousFocus?.focus({ preventScroll: true });
  }

  // TST-PTR-02..04 (flushMouseLeave, ToastViewport.tsx:136-151).
  function flushMouseLeave(): void {
    if (options.hasEnding || touchActive || !markedReadyForMouseLeave) {
      return;
    }
    markedReadyForMouseLeave = false;
    view.hovering = false;
    report('leave');
  }

  // TST-PTR-01.
  function onMouseEnter(): void {
    markedReadyForMouseLeave = false;
    if (!view.hovering || pausePending) {
      view.hovering = true;
      pausePending = false;
      report('enter');
    }
  }

  function onMouseLeave(): void {
    markedReadyForMouseLeave = true;
    flushMouseLeave();
  }

  // TST-PTR-04.
  function onPointerDown(event: PointerEvent): void {
    if (event.pointerType === 'touch') {
      touchActive = true;
    }
  }

  function onPointerEnd(event: PointerEvent): void {
    if (event.pointerType !== 'touch') {
      return;
    }
    touchActive = false;
    flushMouseLeave();
  }

  // TST-FOC-05, TST-FOC-15, TST-PTR-25 (handleFocus, ToastViewport.tsx:181-198).
  function handleFocus(): void {
    if (handlingFocusGuard) {
      handlingFocusGuard = false;
      return;
    }
    if (view.focused) {
      return;
    }
    if (matchesFocusVisible(activeElement(doc))) {
      view.focused = true;
      report('focus');
    }
  }

  // TST-FOC-02, TST-FOC-03, TST-FOC-10 (handleFocusGuard, ToastViewport.tsx:104-119): C# names the first toast that is
  // neither ending nor limited.
  function handleFocusGuard(event: FocusEvent): void {
    handlingFocusGuard = true;
    const target =
      event.relatedTarget === viewport ? resolveReference<HTMLElement>(references, options.focusTargetRef, captures) : null;
    if (target) {
      target.focus();
    } else {
      restoreFocusToPrevElement();
    }
  }

  function isInnerGuard(target: EventTarget | null): boolean {
    return isGuard(target) && (target === viewport.firstElementChild || target === viewport.lastElementChild);
  }

  // A React `onFocus`: the inner guards' focus bubbles here, the guard's handler first.
  function onFocusIn(event: FocusEvent): void {
    if (isInnerGuard(getTarget(event))) {
      handleFocusGuard(event);
    }
    handleFocus();
  }

  // TST-FOC-06 (handleBlur, ToastViewport.tsx:200-209).
  function onFocusOut(event: FocusEvent): void {
    if (!view.focused || contains(viewport, event.relatedTarget as Element | null)) {
      return;
    }
    view.focused = false;
    report('blur');
  }

  // TST-KEY-03, TST-KEY-04 (ToastViewport.tsx:121-134): the timers resume through the blur, not here.
  function onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Tab' && event.shiftKey && getTarget(event) === viewport) {
      event.preventDefault();
      restoreFocusToPrevElement();
    }
  }

  // TST-KEY-01, TST-KEY-02, TST-FOC-01.
  function onGlobalKeyDown(event: KeyboardEvent): void {
    if (event.key !== 'F6' || getTarget(event) === viewport) {
      return;
    }
    event.preventDefault();
    previousFocus = activeElement(doc) as HTMLElement | null;
    // The viewport's own focus event finds the stack focused already, so the F6 is one report.
    view.focused = true;
    viewport.focus({ preventScroll: true });
    report('f6');
  }

  // A focus or blur event's target is the window or a node; jsdom hands listeners a wrapper of its window, so the window
  // is told apart as the target that is not a node.
  function isWindow(target: EventTarget | null): boolean {
    return target === win || (target !== null && !('nodeType' in target));
  }

  // TST-FOC-11.
  function onWindowBlur(event: FocusEvent): void {
    if (!isWindow(getTarget(event))) {
      return;
    }
    windowBlurred = true;
    report('window-blur');
  }

  // TST-FOC-12 (ToastViewport.tsx:77-94): every focus in the window without a related target (the capture phase sees each
  // element's), resuming the timers unless focus-visible focus landed inside the viewport; the window flag waits a macrotask
  // so that such a focus reports first. Only the reports that change the store are sent: a resume, and the flag after a
  // window blur.
  function onWindowFocus(event: FocusEvent): void {
    if (event.relatedTarget) {
      return;
    }
    const target = getTarget(event);
    const resume =
      isWindow(target) || !contains(viewport, target as Element | null) || !matchesFocusVisible(activeElement(doc));
    if (resume) {
      pausePending = true;
      report('window-focus', { resume });
    }
    clearTimeout(windowFocusTimer);
    windowFocusTimer = setTimeout(() => {
      if (!disposed && windowBlurred) {
        windowBlurred = false;
        report('window-focused');
      }
    }, 0);
  }

  // TST-PTR-05..07 (handleDocumentPointerDown, store.ts:402-416).
  function onDocumentPointerDown(event: PointerEvent): void {
    if (event.pointerType !== 'touch' || contains(viewport, getTarget(event) as Element | null)) {
      return;
    }
    view.hovering = false;
    view.focused = false;
    report('touch-outside');
  }

  // The outer guard sits before the viewport, outside it, so its focus is read on the document while toasts exist.
  function onDocumentFocusIn(event: FocusEvent): void {
    const target = getTarget(event);
    if (isGuard(target) && target === viewport.previousElementSibling) {
      handleFocusGuard(event);
    }
  }

  // TST-FOC-14, TST-EDGE-07, TST-EDGE-08: bound on the viewport's own window and document while the list is non-empty.
  function syncGlobalListeners(): void {
    if (!options.empty && unbindGlobal === null) {
      win.addEventListener('keydown', onGlobalKeyDown);
      win.addEventListener('blur', onWindowBlur, true);
      win.addEventListener('focus', onWindowFocus, true);
      doc.addEventListener('pointerdown', onDocumentPointerDown, true);
      doc.addEventListener('focusin', onDocumentFocusIn);
      unbindGlobal = () => {
        win.removeEventListener('keydown', onGlobalKeyDown);
        win.removeEventListener('blur', onWindowBlur, true);
        win.removeEventListener('focus', onWindowFocus, true);
        doc.removeEventListener('pointerdown', onDocumentPointerDown, true);
        doc.removeEventListener('focusin', onDocumentFocusIn);
      };
    } else if (options.empty && unbindGlobal !== null) {
      unbindGlobal();
      unbindGlobal = null;
    }
  }

  // TST-FOC-07..09 (handleFocusManagement, store.ts:480-515): C# named the target at the close; the module focuses it once,
  // and only while focus-visible focus is inside the viewport, so a stale decision never moves focus.
  function syncHandOff(): void {
    const handOff = options.handOff;
    if (!handOff || handOff.revision <= handledHandOff) {
      return;
    }
    handledHandOff = handOff.revision;
    const active = activeElement(doc);
    if (!contains(viewport, active) || !matchesFocusVisible(active)) {
      return;
    }
    if (handOff.toPrevious) {
      restoreFocusToPrevElement();
      return;
    }
    const target = resolveReference<HTMLElement>(references, handOff.targetRef, captures);
    if (target && target.isConnected && !target.hasAttribute(ENDING_STYLE_ATTRIBUTE) && !target.hasAttribute('inert')) {
      target.focus();
    }
  }

  viewport.addEventListener('mouseenter', onMouseEnter);
  viewport.addEventListener('mousemove', onMouseEnter);
  viewport.addEventListener('mouseleave', onMouseLeave);
  viewport.addEventListener('focusin', onFocusIn);
  viewport.addEventListener('focusout', onFocusOut);
  viewport.addEventListener('keydown', onKeyDown);
  viewport.addEventListener('click', handleFocus);
  viewport.addEventListener('pointerdown', onPointerDown);
  viewport.addEventListener('pointerup', onPointerEnd);
  viewport.addEventListener('pointercancel', onPointerEnd);
  syncGlobalListeners();

  return {
    update(optionsJson, nextReferences, nextCaptures) {
      if (disposed) {
        return;
      }
      const next = parseOptions<ToastOptions>(optionsJson);
      const endingChanged = (next.hasEnding ?? false) !== (options.hasEnding ?? false);
      options = next;
      references = nextReferences;
      captures = nextCaptures;
      // The store's flags replace the optimistic view only once C# has applied every report sent.
      if ((next.revision ?? 0) >= revision) {
        view.hovering = next.hovering ?? false;
        view.focused = next.focused ?? false;
      }
      syncGlobalListeners();
      // TST-PTR-03: the effect on `hasTransitioningToasts` (ToastViewport.tsx:151).
      if (endingChanged) {
        flushMouseLeave();
      }
      syncHandOff();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      clearTimeout(windowFocusTimer);
      unbindGlobal?.();
      unbindGlobal = null;
      viewport.removeEventListener('mouseenter', onMouseEnter);
      viewport.removeEventListener('mousemove', onMouseEnter);
      viewport.removeEventListener('mouseleave', onMouseLeave);
      viewport.removeEventListener('focusin', onFocusIn);
      viewport.removeEventListener('focusout', onFocusOut);
      viewport.removeEventListener('keydown', onKeyDown);
      viewport.removeEventListener('click', handleFocus);
      viewport.removeEventListener('pointerdown', onPointerDown);
      viewport.removeEventListener('pointerup', onPointerEnd);
      viewport.removeEventListener('pointercancel', onPointerEnd);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The Root (TST-PTR-08..24, TST-KEY-05/06, TST-ANI-02/08/11, TST-DOM-06/13/14; ToastRoot.tsx:118-459)
// ---------------------------------------------------------------------------------------------------------------

function attachRoot(root: HTMLElement, initial: ToastOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let directions: readonly SwipeDirection[] = options.directions ?? [];
  let lifecycle = options.lifecycle ?? 0;
  let revision = 0;
  let disposed = false;

  // The gesture (ToastRoot.tsx:71-96).
  let activePointerId: number | null = null;
  let dragStart = { x: 0, y: 0 };
  let cancelBaseline = { x: 0, y: 0 };
  let initialTransform = { x: 0, y: 0, scale: 1 };
  let dragOffset = { x: 0, y: 0 };
  let intendedDirection: SwipeDirection | undefined;
  let maxSwipeDisplacement = 0;
  let cancelled = false;
  let firstMove = false;
  let realSwipe = false;
  let lock: SwipeAxisLock = null;
  let swiping = false;
  let direction: SwipeDirection | undefined;
  let unbindDocument: (() => void) | null = null;
  // The release freeze (the M6 plan's decision 19 (a)): a dismissing release keeps the drag styles until the
  // `transitions` module writes data-ending-style, so the exit transition starts from the drag position.
  let frozen = false;
  let endingObserver: MutationObserver | null = null;

  // The measurement's last report, per lifecycle (TST-ANI-02).
  let measured = false;
  let lastHeight = 0;

  takeOver(root, options.style);
  const dragBand: StyleLayer = styleWriter(root).layer(DRAG_BAND);
  const measurementBand: StyleLayer = styleWriter(root).layer(MEASUREMENT_BAND);

  function report(kind: ToastRootReportKind, extra?: Partial<ToastReport>): void {
    revision++;
    channel.report(REPORT_METHOD, { revision, kind, ...extra } satisfies ToastReport);
  }

  // TST-DOM-13, TST-DOM-14 (getDragStyles, ToastRoot.tsx:445-459).
  function writeDrag(): void {
    dragBand.set(MOVEMENT_X, `${dragOffset.x - initialTransform.x}px`);
    dragBand.set(MOVEMENT_Y, `${dragOffset.y - initialTransform.y}px`);
    if (swiping || frozen) {
      dragBand.set('transition', 'none');
      dragBand.set('transform', `translateX(${dragOffset.x}px) translateY(${dragOffset.y}px) scale(${initialTransform.scale})`);
    } else {
      dragBand.remove('transition');
      dragBand.remove('transform');
    }
  }

  function setDirection(next: SwipeDirection | undefined): boolean {
    if (next === direction) {
      return false;
    }
    direction = next;
    setFlag(root, SWIPE_DIRECTION_ATTRIBUTE, next ?? null);
    return true;
  }

  function stopFreeze(): void {
    frozen = false;
    endingObserver?.disconnect();
    endingObserver = null;
  }

  function watchEnding(): void {
    if (root.hasAttribute(ENDING_STYLE_ATTRIBUTE)) {
      stopFreeze();
      writeDrag();
      return;
    }
    if (typeof MutationObserver !== 'function') {
      return;
    }
    endingObserver = new MutationObserver(() => {
      if (root.hasAttribute(ENDING_STYLE_ATTRIBUTE)) {
        stopFreeze();
        writeDrag();
      }
    });
    endingObserver.observe(root, { attributes: true, attributeFilter: [ENDING_STYLE_ATTRIBUTE] });
  }

  // TST-ANI-02 (recalculateHeight, ToastRoot.tsx:118-144): the transient `auto` above the consumer's height, removed in the
  // same task, so the consumer's own height returns.
  function measure(): void {
    if (disposed) {
      return;
    }
    measurementBand.set('height', 'auto');
    const height = root.offsetHeight;
    measurementBand.remove('height');
    if (measured && height === lastHeight) {
      return;
    }
    measured = true;
    lastHeight = height;
    report('height', { height, lifecycle });
  }

  // TST-ANI-11 (ToastRoot.tsx:157-163), and the drag state of TST-PTR-24.
  function resetSwipeState(): void {
    unbindDocument?.();
    unbindDocument = null;
    activePointerId = null;
    swiping = false;
    realSwipe = false;
    lock = null;
    stopFreeze();
    setFlag(root, SWIPING_ATTRIBUTE, null);
    setDirection(undefined);
    initialTransform = { x: 0, y: 0, scale: 1 };
    dragOffset = { x: 0, y: 0 };
    writeDrag();
  }

  // TST-PTR-17..20 (handleSwipeEnd, ToastRoot.tsx:199-238).
  function onSwipeEnd(event: PointerEvent): void {
    if (event.pointerId !== activePointerId) {
      return;
    }
    activePointerId = null;
    unbindDocument?.();
    unbindDocument = null;
    swiping = false;
    realSwipe = false;
    lock = null;
    setFlag(root, SWIPING_ATTRIBUTE, null);

    if (event.type === 'pointercancel' || cancelled) {
      snapBack();
      return;
    }

    const dismiss = dismissDirection(directions, dragOffset.x - initialTransform.x, dragOffset.y - initialTransform.y);
    if (dismiss) {
      setDirection(dismiss);
      frozen = true;
      writeDrag();
      watchEnding();
      report('release', { direction: dismiss, dismiss: true });
    } else {
      snapBack();
    }
  }

  function snapBack(): void {
    dragOffset = { x: initialTransform.x, y: initialTransform.y };
    setDirection(undefined);
    writeDrag();
    report('release', { direction: null, dismiss: false });
  }

  // TST-PTR-08..10 (handlePointerDown, ToastRoot.tsx:240-292).
  function onPointerDown(event: PointerEvent): void {
    if (directions.length === 0 || event.button !== 0) {
      return;
    }
    // TST-PTR-09: before the target check.
    if (event.pointerType === 'touch') {
      report('touch-press');
    }
    if (closest(getTarget(event) as Node | null, TOAST_SWIPE_IGNORE_SELECTOR)) {
      return;
    }

    stopFreeze();
    cancelled = false;
    intendedDirection = undefined;
    maxSwipeDisplacement = 0;
    activePointerId = event.pointerId;
    dragStart = { x: event.clientX, y: event.clientY };
    cancelBaseline = dragStart;
    const transform = getElementTransform(root);
    initialTransform = transform;
    dragOffset = { x: transform.x, y: transform.y };
    swiping = true;
    realSwipe = false;
    lock = null;
    firstMove = true;
    setFlag(root, SWIPING_ATTRIBUTE, '');
    writeDrag();
    report('swipe-start');

    unbindDocument?.();
    const doc = ownerDocument(root);
    doc.addEventListener('pointerup', onSwipeEnd);
    doc.addEventListener('pointercancel', onSwipeEnd);
    unbindDocument = () => {
      doc.removeEventListener('pointerup', onSwipeEnd);
      doc.removeEventListener('pointercancel', onSwipeEnd);
    };

    try {
      root.setPointerCapture?.(event.pointerId);
    } catch {
      // A pointer the browser no longer tracks cannot be captured; the document listeners still end the gesture.
    }
  }

  // TST-PTR-11..16, TST-PTR-23 (handlePointerMove, ToastRoot.tsx:294-408).
  function onPointerMove(event: PointerEvent): void {
    if (event.pointerId !== activePointerId) {
      return;
    }
    event.preventDefault();

    if (firstMove) {
      dragStart = { x: event.clientX, y: event.clientY };
      firstMove = false;
    }

    const { clientX, clientY, movementX, movementY } = event;
    if ((movementY < 0 && clientY > cancelBaseline.y) || (movementY > 0 && clientY < cancelBaseline.y)) {
      cancelBaseline = { x: cancelBaseline.x, y: clientY };
    }
    if ((movementX < 0 && clientX > cancelBaseline.x) || (movementX > 0 && clientX < cancelBaseline.x)) {
      cancelBaseline = { x: clientX, y: cancelBaseline.y };
    }

    const deltaX = clientX - dragStart.x;
    const deltaY = clientY - dragStart.y;
    const cancelDeltaX = clientX - cancelBaseline.x;
    const cancelDeltaY = clientY - cancelBaseline.y;
    const hasHorizontal = directions.includes('left') || directions.includes('right');
    const hasVertical = directions.includes('up') || directions.includes('down');

    if (!realSwipe && Math.sqrt(deltaX * deltaX + deltaY * deltaY) >= MIN_DRAG_THRESHOLD) {
      realSwipe = true;
      if (hasHorizontal && hasVertical) {
        lock = Math.abs(deltaX) > Math.abs(deltaY) ? 'horizontal' : 'vertical';
      }
    }

    if (!intendedDirection) {
      const candidate = armingCandidate(lock, deltaX, deltaY);
      if (candidate && directions.includes(candidate)) {
        intendedDirection = candidate;
        maxSwipeDisplacement = getDisplacement(candidate, deltaX, deltaY);
        if (setDirection(candidate)) {
          report('direction', { direction: candidate });
        }
      }
    } else {
      const current = getDisplacement(intendedDirection, cancelDeltaX, cancelDeltaY);
      if (current > DEFAULT_SWIPE_THRESHOLD) {
        cancelled = false;
        if (setDirection(intendedDirection)) {
          report('direction', { direction: intendedDirection });
        }
      } else if (
        !(directions.includes('left') && directions.includes('right')) &&
        !(directions.includes('up') && directions.includes('down')) &&
        maxSwipeDisplacement - current >= REVERSE_CANCEL_THRESHOLD
      ) {
        cancelled = true;
      }
    }

    const damped = applyDirectionalDamping(directions, deltaX, deltaY);
    let x = initialTransform.x;
    let y = initialTransform.y;
    if (lock !== 'vertical' && hasHorizontal) {
      x += damped.x;
    }
    if (lock !== 'horizontal' && hasVertical) {
      y += damped.y;
    }
    dragOffset = { x, y };
    writeDrag();
  }

  // TST-KEY-05, TST-KEY-06 (handleKeyDown, ToastRoot.tsx:410-421).
  function onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && contains(root, activeElement(ownerDocument(root)))) {
      report('escape');
    }
  }

  // TST-PTR-21 (ToastRoot.tsx:423-443): only during a gesture.
  function onTouchMove(event: TouchEvent): void {
    if (activePointerId === null || directions.length === 0 || !contains(root, getTarget(event) as Element | null)) {
      return;
    }
    event.preventDefault();
  }

  root.addEventListener('pointerdown', onPointerDown);
  root.addEventListener('pointermove', onPointerMove);
  root.addEventListener('pointerup', onSwipeEnd);
  root.addEventListener('pointercancel', onSwipeEnd);
  root.addEventListener('keydown', onKeyDown);
  root.addEventListener('touchmove', onTouchMove, { passive: false });
  (root as MeasuredRoot)[rootMeasure] = measure;

  writeDrag();
  measure();

  return {
    update(optionsJson) {
      if (disposed) {
        return;
      }
      const next = parseOptions<ToastOptions>(optionsJson);
      const nextLifecycle = next.lifecycle ?? 0;
      const rollback = nextLifecycle !== lifecycle;
      // Decision 6 (a): an update older than the module's last report is ignored unless it is the rollback.
      if (!rollback && (next.revision ?? 0) < revision) {
        return;
      }
      options = next;
      directions = next.directions ?? [];
      takeOver(root, next.style);
      if (rollback) {
        // TST-ANI-07/08/11: a new lifecycle resets the swipe state and measures again.
        lifecycle = nextLifecycle;
        resetSwipeState();
        measured = false;
        measure();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      unbindDocument?.();
      unbindDocument = null;
      stopFreeze();
      root.removeEventListener('pointerdown', onPointerDown);
      root.removeEventListener('pointermove', onPointerMove);
      root.removeEventListener('pointerup', onSwipeEnd);
      root.removeEventListener('pointercancel', onSwipeEnd);
      root.removeEventListener('keydown', onKeyDown);
      root.removeEventListener('touchmove', onTouchMove);
      if ((root as MeasuredRoot)[rootMeasure] === measure) {
        delete (root as MeasuredRoot)[rootMeasure];
      }
      dragBand.clear();
      measurementBand.clear();
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The Content (TST-ANI-03; ToastContent.tsx:22-42)
// ---------------------------------------------------------------------------------------------------------------

function attachContent(content: HTMLElement, initial: ToastOptions, references: TwinReferences, captures: TwinCaptures): TwinHandle {
  let root = resolveReference<HTMLElement>(references, initial.rootRef, captures);
  let disposed = false;

  // The Root's own measurement, so one handle reports every height.
  const remeasure = () => {
    if (!disposed && root) {
      (root as MeasuredRoot)[rootMeasure]?.();
    }
  };

  const observers: Array<() => void> = [];
  if (typeof ResizeObserver === 'function' && typeof MutationObserver === 'function') {
    const resizeObserver = new ResizeObserver(remeasure);
    const mutationObserver = new MutationObserver(remeasure);
    resizeObserver.observe(content);
    mutationObserver.observe(content, { childList: true, subtree: true, characterData: true });
    observers.push(
      () => resizeObserver.disconnect(),
      () => mutationObserver.disconnect(),
    );
  }

  remeasure();

  return {
    update(optionsJson, nextReferences, nextCaptures) {
      const next = parseOptions<ToastOptions>(optionsJson);
      root = resolveReference<HTMLElement>(nextReferences, next.rootRef, nextCaptures);
    },
    dispose() {
      disposed = true;
      for (const disconnect of observers) {
        disconnect();
      }
    },
  };
}
