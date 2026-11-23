// The `scroll-area` component module (ADR 04 §1; its scope in ADR 04 Amendments 2026-11-18): ScrollArea's measurement,
// thumb drag, track press, wheel, hover and scrolling flags, all in JavaScript (ADR 01 decision 2, constraint C1). The
// contract is docs/components/scroll-area.md; this section holds the pure transcriptions of its §4.6 and §6.2, which the
// handles below run on live metrics.
import { clamp, contains, getTarget, normalizeScrollOffset, observeResize, styleWriter, type StyleLayer } from './dom';
import { platform } from './platform';
import { getFiniteAnimations } from './transitions';
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

/** The shortest thumb, in pixels (scroll-area/constants.ts:2). */
export const MIN_THUMB_SIZE = 16;

/** How long an axis keeps its scrolling flag after the last movement, in milliseconds (scroll-area/constants.ts:1). */
export const SCROLL_TIMEOUT = 500;

/** How long after the last `scroll` event a scroll counts as programmatic again, in milliseconds. */
export const PROGRAMMATIC_RESTORE_DELAY = 100;

// ---------------------------------------------------------------------------------------------------------------
// The measurement (scroll-area.md §4.6; scroll-area/viewport/ScrollAreaViewport.tsx:118-281, utils/getOffset.ts)
// ---------------------------------------------------------------------------------------------------------------

/**
 * Reads the padding or margin of an element along an axis from its computed style.
 * @param element The element, or `null`.
 * @param prop The box edge to read.
 * @param axis `x` for the inline axis, `y` for the block axis.
 * @returns The start plus the end; for the inline margin, twice the start, because Safari misreports the inline end
 * margin in right-to-left content; 0 without an element.
 */
export function getOffset(element: Element | null, prop: 'margin' | 'padding', axis: 'x' | 'y'): number {
  if (!element) {
    return 0;
  }

  const styles = getComputedStyle(element);
  const key = `${prop}-${axis === 'x' ? 'inline' : 'block'}`;
  const start = parseFloat(styles.getPropertyValue(`${key}-start`));

  // SCR-I18N-05, §16 Q5: the source assumes symmetrical inline margins.
  if (axis === 'x' && prop === 'margin') {
    return start * 2;
  }

  return start + parseFloat(styles.getPropertyValue(`${key}-end`));
}

/** A scrollbar's box as the measurement reads it. */
export interface ScrollbarBox {
  offsetWidth: number;
  offsetHeight: number;
  /** The scrollbar's padding along its own axis (`getOffset(scrollbar, 'padding', axis)`). */
  padding: number;
}

/** A thumb's box as the measurement reads it. */
export interface ThumbBox {
  /** The thumb's margin along its scrollbar's axis (`getOffset(thumb, 'margin', axis)`). */
  margin: number;
}

/** The four normalised overflow edge thresholds. */
export interface Thresholds {
  xStart: number;
  xEnd: number;
  yStart: number;
  yEnd: number;
}

/** A width and a height, in pixels. */
export interface Size {
  width: number;
  height: number;
}

/** What one run of the measurement reads. */
export interface MeasurementInput {
  scrollHeight: number;
  scrollWidth: number;
  clientHeight: number;
  clientWidth: number;
  scrollTop: number;
  scrollLeft: number;
  rtl: boolean;
  thresholds: Thresholds;
  scrollbarX: ScrollbarBox | null;
  scrollbarY: ScrollbarBox | null;
  thumbX: ThumbBox | null;
  thumbY: ThumbBox | null;
  /** The corner size the last measurement stored. */
  cornerSize: Size;
}

/** Where a thumb goes, and its overscroll length while the viewport is out of range. */
export interface ThumbPlacement {
  /** The thumb's offset along its axis, in pixels, as its `translate3d` writes it (negated for a right-to-left horizontal thumb). */
  offset: number;
  /** The shrunk length while overscrolling, or `null` to remove the override. */
  overscrollSize: number | null;
}

/** The axes without overflow. */
export interface HiddenState {
  x: boolean;
  y: boolean;
  corner: boolean;
}

/** Whether the viewport is scrolled past each edge's threshold. */
export interface OverflowEdges {
  xStart: boolean;
  xEnd: boolean;
  yStart: boolean;
  yEnd: boolean;
}

/** What one run of the measurement produces. */
export interface MeasurementResult {
  hidden: HiddenState;
  thumbSize: Size;
  cornerSize: Size;
  /** The distances from the inline start, inline end, top and bottom edges, in pixels. */
  overflowMetrics: [number, number, number, number];
  edges: OverflowEdges;
  /** The vertical thumb's placement, when the vertical scrollbar and its thumb exist. */
  thumbY: ThumbPlacement | null;
  /** The horizontal thumb's placement, when the horizontal scrollbar and its thumb exist. */
  thumbX: ThumbPlacement | null;
}

/**
 * Runs the measurement on read metrics.
 * @param input The viewport's metrics, the reading direction, the thresholds, the scrollbar and thumb boxes and the
 * stored corner size.
 * @returns The derived state, or `null` when the content has no size (the viewport or an ancestor is not laid out).
 */
export function computeMeasurement(input: MeasurementInput): MeasurementResult | null {
  const { scrollHeight: contentHeight, scrollWidth: contentWidth, clientHeight: viewHeight, clientWidth: viewWidth } = input;
  const { scrollTop, scrollLeft, rtl, thresholds, scrollbarX, scrollbarY, thumbX, thumbY, cornerSize } = input;

  // SCR-ANI-08.
  if (contentHeight === 0 || contentWidth === 0) {
    return null;
  }

  // SCR-STA-06, SCR-STA-07.
  const hiddenY = viewHeight >= contentHeight;
  const hiddenX = viewWidth >= contentWidth;
  const hidden: HiddenState = { x: hiddenX, y: hiddenY, corner: hiddenY || hiddenX };
  const ratioX = viewWidth / contentWidth;
  const ratioY = viewHeight / contentHeight;
  const maxScrollLeft = Math.max(0, contentWidth - viewWidth);
  const maxScrollTop = Math.max(0, contentHeight - viewHeight);

  // SCR-STA-10, SCR-I18N-03.
  let fromStartX = 0;
  let fromEndX = 0;
  if (!hiddenX) {
    fromStartX = normalizeScrollOffset(rtl ? -scrollLeft : scrollLeft, maxScrollLeft);
    fromEndX = maxScrollLeft - fromStartX;
  }
  const fromStartY = hiddenY ? 0 : normalizeScrollOffset(scrollTop, maxScrollTop);
  const fromEndY = hiddenY ? 0 : maxScrollTop - fromStartY;
  const trackWidth = hiddenX ? 0 : viewWidth;
  const trackHeight = hiddenY ? 0 : viewHeight;

  // SCR-STA-18: the corner is the vertical scrollbar's width by the horizontal scrollbar's height.
  let cornerWidth = 0;
  let cornerHeight = 0;
  if (!hiddenX && !hiddenY) {
    cornerWidth = scrollbarY?.offsetWidth || 0;
    cornerHeight = scrollbarX?.offsetHeight || 0;
  }

  // Until the corner is sized, the scrollbars still span the corner they are about to give up.
  const cornerUnsized = cornerSize.width === 0 && cornerSize.height === 0;
  const cornerWidthOffset = cornerUnsized ? cornerWidth : 0;
  const cornerHeightOffset = cornerUnsized ? cornerHeight : 0;

  // SCR-STA-17.
  const paddingX = scrollbarX?.padding ?? 0;
  const paddingY = scrollbarY?.padding ?? 0;
  const marginX = thumbX?.margin ?? 0;
  const marginY = thumbY?.margin ?? 0;
  const idealWidth = trackWidth - paddingX - marginX;
  const idealHeight = trackHeight - paddingY - marginY;
  const maxWidth = scrollbarX ? Math.min(scrollbarX.offsetWidth - cornerWidthOffset, idealWidth) : idealWidth;
  const maxHeight = scrollbarY ? Math.min(scrollbarY.offsetHeight - cornerHeightOffset, idealHeight) : idealHeight;
  const thumbWidth = Math.max(MIN_THUMB_SIZE, maxWidth * ratioX);
  const thumbHeight = Math.max(MIN_THUMB_SIZE, maxHeight * ratioY);

  // SCR-DOM-24, SCR-DOM-25, SCR-I18N-02.
  let placementY: ThumbPlacement | null = null;
  if (scrollbarY && thumbY) {
    const maxThumbOffsetY = scrollbarY.offsetHeight - thumbHeight - paddingY - marginY;
    placementY = overscrollThumb(scrollTop, maxScrollTop, contentHeight, thumbHeight, maxThumbOffsetY);
  }
  let placementX: ThumbPlacement | null = null;
  if (scrollbarX && thumbX) {
    const maxThumbOffsetX = scrollbarX.offsetWidth - thumbWidth - paddingX - marginX;
    const placed = overscrollThumb(rtl ? -scrollLeft : scrollLeft, maxScrollLeft, contentWidth, thumbWidth, maxThumbOffsetX);
    placementX = rtl ? { offset: -placed.offset, overscrollSize: placed.overscrollSize } : placed;
  }

  // SCR-STA-09: strict comparisons, and no edge flag on an axis without overflow.
  const edges: OverflowEdges = {
    xStart: !hiddenX && fromStartX > thresholds.xStart,
    xEnd: !hiddenX && fromEndX > thresholds.xEnd,
    yStart: !hiddenY && fromStartY > thresholds.yStart,
    yEnd: !hiddenY && fromEndY > thresholds.yEnd,
  };

  return {
    hidden,
    thumbSize: { width: thumbWidth, height: thumbHeight },
    cornerSize: { width: cornerWidth, height: cornerHeight },
    overflowMetrics: [fromStartX, fromEndX, fromStartY, fromEndY],
    edges,
    thumbY: placementY,
    thumbX: placementX,
  };
}

/**
 * Places a thumb and, while the viewport is scrolled out of range (WebKit's rubber-banding), shrinks it against the
 * pinned edge, damped by `content / (content + overscroll)`.
 * @param scrollFromStart The scroll distance from the axis's start edge, possibly out of range.
 * @param maxScroll The largest in-range distance.
 * @param content The content's length.
 * @param size The resting thumb length.
 * @param maxThumbOffset The free track length.
 * @returns The thumb's offset from the start and its shrunk length, or `null` for the resting length.
 */
export function overscrollThumb(
  scrollFromStart: number,
  maxScroll: number,
  content: number,
  size: number,
  maxThumbOffset: number,
): ThumbPlacement {
  const clamped = clamp(scrollFromStart, 0, maxScroll);
  const overscroll = scrollFromStart - clamped;
  const nextSize = Math.max(MIN_THUMB_SIZE, (size * content) / (content + Math.abs(overscroll)));
  const offset = maxScroll ? (clamped / maxScroll) * maxThumbOffset : 0;

  // An end-edge overscroll pushes the shrunk thumb down by the shrink, so it stays pinned to the end.
  return {
    offset: offset + (overscroll > 0 ? size - nextSize : 0),
    overscrollSize: overscroll ? nextSize : null,
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The pointer mappings (scroll-area.md §6.2.1-§6.2.3; ScrollAreaRoot.tsx:184-236, ScrollAreaScrollbar.tsx:81-189)
// ---------------------------------------------------------------------------------------------------------------

/** What a drag move reads. */
export interface DragInput {
  /** The viewport's scroll offset on the drag axis when the drag started. */
  start: number;
  /** The pointer's client displacement on the drag axis since the drag started. */
  delta: number;
  trackLength: number;
  thumbLength: number;
  /** The scrollbar's padding along the axis. */
  padding: number;
  /** The thumb's margin along the axis. */
  margin: number;
  scrollSize: number;
  clientSize: number;
}

// SCR-PTR-05, SCR-PTR-06, SCR-PTR-13.
/**
 * Maps a drag move onto a scroll offset.
 * @param input The drag's start, the displacement and the boxes.
 * @returns The offset to assign, which the browser clamps; the start offset when the thumb fills the track.
 */
export function dragScroll(input: DragInput): number {
  const maxThumbOffset = input.trackLength - input.thumbLength - input.padding - input.margin;
  const ratio = maxThumbOffset <= 0 ? 0 : input.delta / maxThumbOffset;
  return input.start + ratio * (input.scrollSize - input.clientSize);
}

/** What a track press reads. */
export interface TrackPressInput {
  /** The pointer's client coordinate on the scrollbar's axis. */
  pointer: number;
  /** The track's client start on that axis (its bounding rectangle's top or left). */
  trackStart: number;
  thumbLength: number;
  /** The scrollbar's padding along the axis. */
  padding: number;
  /** The thumb's margin along the axis. */
  margin: number;
  trackLength: number;
  scrollSize: number;
  clientSize: number;
  /** Whether the scrollbar is horizontal in right-to-left content. */
  horizontalRtl: boolean;
}

// SCR-PTR-14, SCR-PTR-18; §16 Q8; SCR-PTR-15.
/**
 * Maps a track press onto the offset that centers the thumb under the pointer.
 * @param input The pointer, the boxes and the direction.
 * @returns The offset to assign, unclamped, or `null` when the thumb fills the track.
 */
export function trackPressScroll(input: TrackPressInput): number | null {
  const click = input.pointer - input.trackStart - input.thumbLength / 2 - input.padding + input.margin / 2;
  const maxThumbOffset = input.trackLength - input.thumbLength - input.padding - input.margin;
  if (maxThumbOffset <= 0) {
    return null;
  }

  const ratio = click / maxThumbOffset;
  const maxScroll = input.scrollSize - input.clientSize;
  return input.horizontalRtl ? -(1 - ratio) * maxScroll : ratio * maxScroll;
}

/** What a wheel over a scrollbar reads. */
export interface WheelInput {
  /** The viewport's scroll offset on the scrollbar's axis. */
  current: number;
  /** The wheel's delta on the scrollbar's axis. */
  delta: number;
  /** `scrollSize − clientSize` on the axis. */
  maxScroll: number;
  /** Whether the range is `[−max, 0]` (a horizontal scrollbar in right-to-left content). */
  negativeRange: boolean;
}

// SCR-PTR-21..24.
/**
 * Maps a wheel over a scrollbar onto a scroll offset.
 * @param input The offset, the delta and the range.
 * @returns The clamped offset, or `null` when the wheel is not consumed: a zero delta, or the viewport is already at
 * the edge in the delta's direction, so the event chains to the ancestors.
 */
export function wheelScroll(input: WheelInput): number | null {
  const { current, delta, maxScroll, negativeRange } = input;
  if (delta === 0) {
    return null;
  }

  const min = negativeRange ? -maxScroll : 0;
  const max = negativeRange ? 0 : maxScroll;
  if ((current <= min && delta < 0) || (current >= max && delta > 0)) {
    return null;
  }

  return Math.min(max, Math.max(min, current + delta));
}

// ---------------------------------------------------------------------------------------------------------------
// The handles (ADR 04 Amendments 2026-11-18, the `scroll-area` scope cell; design point 6 (a) of the ScrollArea track:
// one handle per part)
// ---------------------------------------------------------------------------------------------------------------

/** The part a handle belongs to. */
export type ScrollAreaPart = 'root' | 'viewport' | 'content' | 'scrollbar' | 'thumb' | 'corner';

/** A scrollbar's or a thumb's axis. */
export type ScrollAreaOrientation = 'vertical' | 'horizontal';

/** The options C# sends on `attach` and every `update` of a handle. */
export interface ScrollAreaOptions extends TwinOptions {
  part: ScrollAreaPart;
  /** The index of the root element among the references, for every part but the root. */
  rootRef?: number | null;
  /** The axis of a scrollbar or a thumb. */
  orientation?: ScrollAreaOrientation;
  /** The root's reading direction. */
  rtl?: boolean;
  /** The root's four normalised overflow edge thresholds. */
  thresholds?: Thresholds;
}

/** The overflow state the root's twin receives (`OnOverflow`), once per change. */
export interface OverflowReport {
  hasOverflowX: boolean;
  hasOverflowY: boolean;
  xStart: boolean;
  xEnd: boolean;
  yStart: boolean;
  yEnd: boolean;
  measured: boolean;
}

// The bands of ADR 04 Amendments 2026-11-18 (decision 5 as revised): what Base UI writes after render goes over the
// consumer's style; what it renders through a part's merged `style` goes under it (design point 1 of the ScrollArea track).
const AFTER_RENDER_BAND = 'component';
const MEASURED_BAND = 'component-under';

const SCROLLING_ATTRIBUTE = 'data-scrolling';
const HOVERING_ATTRIBUTE = 'data-hovering';
const ORIENTATION_ATTRIBUTE = 'data-orientation';
const OVERFLOW_VARIABLES = [
  '--scroll-area-overflow-x-start',
  '--scroll-area-overflow-x-end',
  '--scroll-area-overflow-y-start',
  '--scroll-area-overflow-y-end',
] as const;
const THUMB_HEIGHT = '--scroll-area-thumb-height';
const THUMB_WIDTH = '--scroll-area-thumb-width';
const CORNER_HEIGHT = '--scroll-area-corner-height';
const CORNER_WIDTH = '--scroll-area-corner-width';
const NOOP = () => {};

// SCR-DOM-12 (ScrollAreaViewport.tsx:31-69; design point 9 (a) of the ScrollArea track): once per page, skipped on WebKit,
// where a descendant could not `inherit` a non-inheriting property, and errors (already registered) ignored.
let overflowVariablesRegistered = false;

function registerOverflowVariables(): void {
  if (overflowVariablesRegistered || platform.engine.webkit) {
    return;
  }
  overflowVariablesRegistered = true;
  if (typeof CSS === 'undefined' || typeof CSS.registerProperty !== 'function') {
    return;
  }
  for (const name of OVERFLOW_VARIABLES) {
    try {
      CSS.registerProperty({ name, syntax: '<length>', inherits: false, initialValue: '0px' });
    } catch {
      // Already registered, by another bundle or the page.
    }
  }
}

// SCR-STA-21.
/** The drag record the two scrollbars share. */
interface DragState {
  pointerId: number | null;
  startX: number;
  startY: number;
  startScrollTop: number;
  startScrollLeft: number;
  orientation: ScrollAreaOrientation;
}

// SCR §2.
/** What one root shares with the handles of its parts, kept on the root element. */
interface RootState {
  readonly root: HTMLElement;
  twin: TwinChannel | null;
  rtl: boolean;
  thresholds: Thresholds;
  viewport: HTMLElement | null;
  content: HTMLElement | null;
  scrollbarX: HTMLElement | null;
  scrollbarY: HTMLElement | null;
  thumbX: HTMLElement | null;
  thumbY: HTMLElement | null;
  corner: HTMLElement | null;
  // SCR-STA-19; §16 Q7.
  /** Set by the first measurement attempt; never reset. */
  measured: boolean;
  /** The viewport metrics the last measurement read: clientHeight, scrollHeight, clientWidth, scrollWidth. */
  lastMetrics: [number, number, number, number] | null;
  hidden: HiddenState;
  edges: OverflowEdges;
  thumbSize: Size;
  cornerSize: Size;
  reported: OverflowReport | null;
  scrollingX: boolean;
  scrollingY: boolean;
  scrollTimerX: ReturnType<typeof setTimeout> | undefined;
  scrollTimerY: ReturnType<typeof setTimeout> | undefined;
  reportedScrolling: { x: boolean; y: boolean };
  hovering: boolean;
  reportedHovering: boolean;
  // SCR-STA-16.
  /** Whether the last `pointerenter`, `pointermove` or `pointerdown` on the root came from touch. */
  touchModality: boolean;
  // SCR-STA-14.
  /** Starts true, cleared by an interaction on the viewport, restored 100 ms after the last scroll. */
  programmatic: boolean;
  // SCR-STA-11; §16 Q4.
  /** The position the scrolling flags compare against, updated by user-driven scrolls only. */
  scrollPosition: { x: number; y: number };
  drag: DragState;
  // SCR-PTR-03.
  /** Whether a drag disabled the viewport's scroll snapping. */
  snapDisabled: boolean;
  measureQueued: boolean;
  /** Runs the measurement now. */
  measure(): void;
  /** Runs the measurement in a microtask, once however often it is asked for before then. */
  queueMeasure(): void;
}

const rootStateKey = Symbol('iodine.scrollArea.root');

interface ScrollAreaRootElement extends HTMLElement {
  [rootStateKey]?: RootState;
}

// The attach-overtaken rule (ADR 04 Amendments 2026-10-29): a part's handle can attach before its root's, so whichever
// attaches first creates the state, and the root's handle fills in its twin and options.
function rootState(root: HTMLElement): RootState {
  const element = root as ScrollAreaRootElement;
  const existing = element[rootStateKey];
  if (existing) {
    return existing;
  }

  const state: RootState = {
    root,
    twin: null,
    rtl: false,
    thresholds: { xStart: 0, xEnd: 0, yStart: 0, yEnd: 0 },
    viewport: null,
    content: null,
    scrollbarX: null,
    scrollbarY: null,
    thumbX: null,
    thumbY: null,
    corner: null,
    measured: false,
    lastMetrics: null,
    hidden: { x: true, y: true, corner: true },
    edges: { xStart: false, xEnd: false, yStart: false, yEnd: false },
    thumbSize: { width: 0, height: 0 },
    cornerSize: { width: 0, height: 0 },
    reported: null,
    scrollingX: false,
    scrollingY: false,
    scrollTimerX: undefined,
    scrollTimerY: undefined,
    reportedScrolling: { x: false, y: false },
    hovering: false,
    reportedHovering: false,
    touchModality: false,
    programmatic: true,
    scrollPosition: { x: 0, y: 0 },
    drag: { pointerId: null, startX: 0, startY: 0, startScrollTop: 0, startScrollLeft: 0, orientation: 'vertical' },
    snapDisabled: false,
    measureQueued: false,
    measure: () => measure(state),
    queueMeasure: () => {
      if (state.measureQueued) {
        return;
      }
      state.measureQueued = true;
      queueMicrotask(() => {
        state.measureQueued = false;
        measure(state);
      });
    },
  };
  element[rootStateKey] = state;
  return state;
}

// The handover (ADR 04 Amendments 2026-10-18): from the first take-over the element's whole style is the writer's, C#'s
// declarations and the consumer's string (sent as `style`) plus the module's bands; C# freezes what it rendered.
function takeOver(element: HTMLElement, style: string | null | undefined): void {
  const writer = styleWriter(element);
  if (!(element as unknown as Record<symbol, boolean>)[takenOver]) {
    (element as unknown as Record<symbol, boolean>)[takenOver] = true;
    element.style.cssText = '';
  }
  writer.setConsumer(style ?? null);
}

const takenOver = Symbol('iodine.scrollArea.takenOver');

function band(element: HTMLElement, name: typeof AFTER_RENDER_BAND | typeof MEASURED_BAND): StyleLayer {
  return styleWriter(element).layer(name);
}

function px(value: number): string {
  return `${value}px`;
}

function setFlag(element: HTMLElement | null, name: string, on: boolean): void {
  if (!element || element.hasAttribute(name) === on) {
    return;
  }
  if (on) {
    element.setAttribute(name, '');
  } else {
    element.removeAttribute(name);
  }
}

// SCR-STA-11, SCR-STA-12, SCR-DOM-03/10/14/16/22: the module writes data-scrolling; C# mirrors it from one report per change.
function writeScrolling(state: RootState): void {
  const either = state.scrollingX || state.scrollingY;
  setFlag(state.root, SCROLLING_ATTRIBUTE, either);
  setFlag(state.viewport, SCROLLING_ATTRIBUTE, either);
  setFlag(state.content, SCROLLING_ATTRIBUTE, either);
  setFlag(state.scrollbarY, SCROLLING_ATTRIBUTE, state.scrollingY);
  setFlag(state.thumbY, SCROLLING_ATTRIBUTE, state.scrollingY);
  setFlag(state.scrollbarX, SCROLLING_ATTRIBUTE, state.scrollingX);
  setFlag(state.thumbX, SCROLLING_ATTRIBUTE, state.scrollingX);
  if (state.twin && (state.reportedScrolling.x !== state.scrollingX || state.reportedScrolling.y !== state.scrollingY)) {
    state.reportedScrolling = { x: state.scrollingX, y: state.scrollingY };
    state.twin.report('OnScrolling', state.reportedScrolling);
  }
}

// SCR-STA-15, SCR-DOM-16: data-hovering is the scrollbars'.
function writeHovering(state: RootState): void {
  setFlag(state.scrollbarY, HOVERING_ATTRIBUTE, state.hovering);
  setFlag(state.scrollbarX, HOVERING_ATTRIBUTE, state.hovering);
  if (state.twin && state.reportedHovering !== state.hovering) {
    state.reportedHovering = state.hovering;
    state.twin.report('OnHovering', { hovering: state.hovering });
  }
}

function setHovering(state: RootState, hovering: boolean): void {
  if (state.hovering !== hovering) {
    state.hovering = hovering;
    writeHovering(state);
  }
}

// ScrollAreaRoot.tsx:82-90: the axis's flag and its 500 ms timer, restarted by every movement.
function startScrolling(state: RootState, vertical: boolean): void {
  if (vertical) {
    clearTimeout(state.scrollTimerY);
    state.scrollingY = true;
    state.scrollTimerY = setTimeout(() => setScrolling(state, true, false), SCROLL_TIMEOUT);
  } else {
    clearTimeout(state.scrollTimerX);
    state.scrollingX = true;
    state.scrollTimerX = setTimeout(() => setScrolling(state, false, false), SCROLL_TIMEOUT);
  }
  writeScrolling(state);
}

function setScrolling(state: RootState, vertical: boolean, on: boolean): void {
  if (vertical) {
    state.scrollingY = on;
  } else {
    state.scrollingX = on;
  }
  writeScrolling(state);
}

// ScrollAreaRoot.tsx:92-105 (handleScroll): a movement on an axis sets that axis's flag.
function reportScroll(state: RootState, position: { x: number; y: number }): void {
  const offsetX = position.x - state.scrollPosition.x;
  const offsetY = position.y - state.scrollPosition.y;
  state.scrollPosition = position;
  if (offsetY !== 0) {
    startScrolling(state, true);
  }
  if (offsetX !== 0) {
    startScrolling(state, false);
  }
}

// The values Base UI renders through the parts' merged `style` from its state (SCR-DOM-02, -20, -27).
function writeThumbSize(state: RootState): void {
  if (state.scrollbarY) {
    band(state.scrollbarY, MEASURED_BAND).set(THUMB_HEIGHT, px(state.thumbSize.height));
  }
  if (state.scrollbarX) {
    band(state.scrollbarX, MEASURED_BAND).set(THUMB_WIDTH, px(state.thumbSize.width));
  }
}

function writeCornerSize(state: RootState): void {
  const root = band(state.root, MEASURED_BAND);
  root.set(CORNER_HEIGHT, px(state.cornerSize.height));
  root.set(CORNER_WIDTH, px(state.cornerSize.width));
  if (state.corner) {
    const corner = band(state.corner, MEASURED_BAND);
    corner.set('width', px(state.cornerSize.width));
    corner.set('height', px(state.cornerSize.height));
  }
}

function writeThumb(thumb: HTMLElement, sizeVariable: string, placement: ThumbPlacement, vertical: boolean): void {
  const layer = band(thumb, AFTER_RENDER_BAND);
  // SCR-DOM-25: removed when back in range, so the scrollbar's resting value applies again.
  if (placement.overscrollSize === null) {
    layer.remove(sizeVariable);
  } else {
    layer.set(sizeVariable, px(placement.overscrollSize));
  }
  // SCR-DOM-24.
  layer.set('transform', vertical ? `translate3d(0,${placement.offset}px,0)` : `translate3d(${placement.offset}px,0,0)`);
}

function reportOverflow(state: RootState): void {
  if (!state.twin) {
    return;
  }
  const next: OverflowReport = {
    hasOverflowX: !state.hidden.x,
    hasOverflowY: !state.hidden.y,
    xStart: state.edges.xStart,
    xEnd: state.edges.xEnd,
    yStart: state.edges.yStart,
    yEnd: state.edges.yEnd,
    measured: state.measured,
  };
  const last = state.reported;
  if (
    last &&
    last.hasOverflowX === next.hasOverflowX &&
    last.hasOverflowY === next.hasOverflowY &&
    last.xStart === next.xStart &&
    last.xEnd === next.xEnd &&
    last.yStart === next.yStart &&
    last.yEnd === next.yEnd &&
    last.measured === next.measured
  ) {
    return;
  }
  state.reported = next;
  state.twin.report('OnOverflow', next);
}

function scrollbarBox(scrollbar: HTMLElement | null, axis: 'x' | 'y'): ScrollbarBox | null {
  return scrollbar ? { offsetWidth: scrollbar.offsetWidth, offsetHeight: scrollbar.offsetHeight, padding: getOffset(scrollbar, 'padding', axis) } : null;
}

function thumbBox(thumb: HTMLElement | null, axis: 'x' | 'y'): ThumbBox | null {
  return thumb ? { margin: getOffset(thumb, 'margin', axis) } : null;
}

// SCR §4.6 on the live elements (ScrollAreaViewport.tsx:118-281). The first attempt sets the measured flag even when the
// content has no size (SCR-STA-19, SCR-ANI-08). A result equal to the stored state changes nothing but the viewport's
// variables and the thumbs' transforms (SCR-STA-20).
function measure(state: RootState): void {
  const viewport = state.viewport;
  if (!viewport || !state.twin) {
    return;
  }

  const first = state.lastMetrics === null;
  const result = computeMeasurement({
    scrollHeight: viewport.scrollHeight,
    scrollWidth: viewport.scrollWidth,
    clientHeight: viewport.clientHeight,
    clientWidth: viewport.clientWidth,
    scrollTop: viewport.scrollTop,
    scrollLeft: viewport.scrollLeft,
    rtl: state.rtl,
    thresholds: state.thresholds,
    scrollbarX: scrollbarBox(state.scrollbarX, 'x'),
    scrollbarY: scrollbarBox(state.scrollbarY, 'y'),
    thumbX: thumbBox(state.thumbX, 'x'),
    thumbY: thumbBox(state.thumbY, 'y'),
    cornerSize: state.cornerSize,
  });
  state.lastMetrics = [viewport.clientHeight, viewport.scrollHeight, viewport.clientWidth, viewport.scrollWidth];
  if (first) {
    state.measured = true;
  }
  if (!result) {
    reportOverflow(state);
    return;
  }

  if (result.thumbSize.width !== state.thumbSize.width || result.thumbSize.height !== state.thumbSize.height) {
    state.thumbSize = result.thumbSize;
    writeThumbSize(state);
  }
  if (result.thumbY && state.thumbY) {
    writeThumb(state.thumbY, THUMB_HEIGHT, result.thumbY, true);
  }
  if (result.thumbX && state.thumbX) {
    writeThumb(state.thumbX, THUMB_WIDTH, result.thumbX, false);
  }

  // SCR-DOM-11: rewritten on every measurement; the writer skips an unchanged value.
  const layer = band(viewport, AFTER_RENDER_BAND);
  for (let i = 0; i < OVERFLOW_VARIABLES.length; i++) {
    layer.set(OVERFLOW_VARIABLES[i]!, px(result.overflowMetrics[i]!));
  }

  // SCR-STA-18: only while a corner element is mounted. In Base UI the corner and both scrollbars mount in one commit; here
  // each part attaches on its own, so a corner sized before the other scrollbar attached changes size again later, and the
  // new size shortens the tracks: a change measures once more, which converges to Base UI's result.
  let cornerChanged = false;
  if (state.corner && (result.cornerSize.width !== state.cornerSize.width || result.cornerSize.height !== state.cornerSize.height)) {
    state.cornerSize = result.cornerSize;
    writeCornerSize(state);
    cornerChanged = true;
  }

  const hiddenChanged =
    result.hidden.x !== state.hidden.x || result.hidden.y !== state.hidden.y || result.hidden.corner !== state.hidden.corner;
  if (hiddenChanged) {
    state.hidden = result.hidden;
  }
  const edges = result.edges;
  if (
    edges.xStart !== state.edges.xStart ||
    edges.xEnd !== state.edges.xEnd ||
    edges.yStart !== state.edges.yStart ||
    edges.yEnd !== state.edges.yEnd
  ) {
    state.edges = edges;
  }
  reportOverflow(state);

  // SCR-ANI-02, SCR-ANI-03: a hidden-state change measures again once the parts it shows or removes have rendered; the
  // parts' own attach measures too, a round trip later on Server.
  if (hiddenChanged || cornerChanged) {
    state.queueMeasure();
  }
}

// ScrollAreaRoot.tsx:107-119.
function disableSnap(state: RootState): void {
  if (state.viewport && !state.snapDisabled) {
    state.snapDisabled = true;
    band(state.viewport, AFTER_RENDER_BAND).set('scroll-snap-type', 'none');
  }
}

// ScrollAreaRoot.tsx:121-155 (handlePointerDown); `element` is the element whose listener runs (the Thumb, or the
// Scrollbar for a track press), whose rendered data-orientation picks the axis (SCR-PTR-04, §16 Q6).
function dragStart(state: RootState, event: PointerEvent, element: HTMLElement): void {
  if (event.button !== 0) {
    return;
  }

  const drag = state.drag;
  if (drag.pointerId !== null) {
    const activeThumb = drag.orientation === 'vertical' ? state.thumbY : state.thumbX;
    // SCR-PTR-11: a live drag holds capture; without it the release went missing and the new pointer takes over.
    if (activeThumb?.hasPointerCapture(drag.pointerId)) {
      return;
    }
  }

  drag.pointerId = event.pointerId;
  drag.startY = event.clientY;
  drag.startX = event.clientX;
  drag.orientation = element.getAttribute(ORIENTATION_ATTRIBUTE) as ScrollAreaOrientation;

  const viewport = state.viewport;
  if (viewport) {
    drag.startScrollTop = viewport.scrollTop;
    drag.startScrollLeft = viewport.scrollLeft;
    disableSnap(state);
  }

  const thumb = drag.orientation === 'vertical' ? state.thumbY : state.thumbX;
  thumb?.setPointerCapture(event.pointerId);
}

// ScrollAreaRoot.tsx:157-182 (handlePointerUp).
function dragEnd(state: RootState, event: PointerEvent): void {
  const drag = state.drag;
  if (event.pointerId !== drag.pointerId) {
    return;
  }

  drag.pointerId = null;
  // SCR-PTR-09: the drag axis's flag clears at once, whatever its timer.
  setScrolling(state, drag.orientation === 'vertical', false);

  if (state.snapDisabled) {
    if (state.viewport) {
      band(state.viewport, AFTER_RENDER_BAND).remove('scroll-snap-type');
    }
    state.snapDisabled = false;
  }

  const thumb = drag.orientation === 'vertical' ? state.thumbY : state.thumbX;
  // A pointercancel releases capture implicitly; releasing a capture no longer held would throw.
  if (thumb?.hasPointerCapture(event.pointerId)) {
    thumb.releasePointerCapture(event.pointerId);
  }
}

// ScrollAreaRoot.tsx:184-236 (handlePointerMove), SCR §6.2.1.
function dragMove(state: RootState, event: PointerEvent): void {
  const drag = state.drag;
  if (event.pointerId !== drag.pointerId) {
    return;
  }

  // SCR-PTR-08: a move without the primary button held is the missed release.
  if (event.buttons % 2 === 0) {
    dragEnd(state, event);
    return;
  }

  const viewport = state.viewport;
  if (!viewport) {
    return;
  }

  const vertical = drag.orientation === 'vertical';
  const thumb = vertical ? state.thumbY : state.thumbX;
  const scrollbar = vertical ? state.scrollbarY : state.scrollbarX;
  if (!thumb || !scrollbar) {
    return;
  }

  const axis = vertical ? 'y' : 'x';
  const next = dragScroll({
    start: vertical ? drag.startScrollTop : drag.startScrollLeft,
    delta: vertical ? event.clientY - drag.startY : event.clientX - drag.startX,
    trackLength: vertical ? scrollbar.offsetHeight : scrollbar.offsetWidth,
    thumbLength: vertical ? thumb.offsetHeight : thumb.offsetWidth,
    padding: getOffset(scrollbar, 'padding', axis),
    margin: getOffset(thumb, 'margin', axis),
    scrollSize: vertical ? viewport.scrollHeight : viewport.scrollWidth,
    clientSize: vertical ? viewport.clientHeight : viewport.clientWidth,
  });
  if (vertical) {
    viewport.scrollTop = next;
  } else {
    viewport.scrollLeft = next;
  }
  // §16 Q9: cancelled and marked even when the thumb fills the track.
  event.preventDefault();
  startScrolling(state, vertical);
}

// ScrollAreaScrollbar.tsx:123-189 (the track press), SCR §6.2.2.
function trackPress(state: RootState, event: PointerEvent, scrollbarElement: HTMLElement, vertical: boolean): void {
  if (event.button !== 0) {
    return;
  }

  // SCR-PTR-16: the composed path's target, with a shadow-aware containment check.
  const target = getTarget(event) as Element | null;
  const thumb = vertical ? state.thumbY : state.thumbX;
  if (thumb && contains(thumb, target)) {
    return;
  }

  const viewport = state.viewport;
  if (!viewport) {
    return;
  }

  const scrollbar = vertical ? state.scrollbarY : state.scrollbarX;
  if (!thumb || !scrollbar) {
    return;
  }

  const axis = vertical ? 'y' : 'x';
  const rect = scrollbar.getBoundingClientRect();
  const next = trackPressScroll({
    pointer: vertical ? event.clientY : event.clientX,
    trackStart: vertical ? rect.top : rect.left,
    thumbLength: vertical ? thumb.offsetHeight : thumb.offsetWidth,
    padding: getOffset(scrollbar, 'padding', axis),
    margin: getOffset(thumb, 'margin', axis),
    trackLength: vertical ? scrollbar.offsetHeight : scrollbar.offsetWidth,
    scrollSize: vertical ? viewport.scrollHeight : viewport.scrollWidth,
    clientSize: vertical ? viewport.clientHeight : viewport.clientWidth,
    horizontalRtl: !vertical && state.rtl,
  });
  if (next === null) {
    return;
  }

  // SCR-PTR-17: snapping off before the jump.
  disableSnap(state);
  if (vertical) {
    viewport.scrollTop = next;
  } else {
    viewport.scrollLeft = next;
  }
  reportScroll(state, { x: viewport.scrollLeft, y: viewport.scrollTop });
  dragStart(state, event, scrollbarElement);
}

// ScrollAreaScrollbar.tsx:81-115 (the native wheel listener), SCR §6.2.3.
function wheel(state: RootState, event: WheelEvent, vertical: boolean): void {
  const viewport = state.viewport;
  if (!viewport || event.ctrlKey) {
    return;
  }

  const horizontal = !vertical;
  const maxScroll = horizontal ? viewport.scrollWidth - viewport.clientWidth : viewport.scrollHeight - viewport.clientHeight;
  const next = wheelScroll({
    current: horizontal ? viewport.scrollLeft : viewport.scrollTop,
    delta: horizontal ? event.deltaX : event.deltaY,
    maxScroll,
    negativeRange: horizontal && state.rtl,
  });
  if (next === null) {
    return;
  }

  event.preventDefault();
  if (horizontal) {
    viewport.scrollLeft = next;
  } else {
    viewport.scrollTop = next;
  }
  reportScroll(state, { x: viewport.scrollLeft, y: viewport.scrollTop });
}

/** Adds a listener and returns the function that removes it. */
function listen<K extends keyof HTMLElementEventMap>(
  element: HTMLElement,
  type: K,
  listener: (event: HTMLElementEventMap[K]) => void,
  options?: AddEventListenerOptions,
): () => void {
  element.addEventListener(type, listener as EventListener, options);
  return () => element.removeEventListener(type, listener as EventListener, options);
}

/**
 * Attaches a part's handle.
 * @param elementOrId The part's element.
 * @param optionsJson The {@link ScrollAreaOptions}, as JSON.
 * @param dotNetRef The part's twin; only the root's receives reports.
 * @param references The root element, for every part but the root.
 * @param captures The capture ids of the element and the references.
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
  let options = parseOptions<ScrollAreaOptions>(optionsJson);
  const rootElement =
    options.part === 'root' ? element : resolveReference<HTMLElement>(references, options.rootRef ?? null, captures);
  if (!element || !rootElement) {
    // SCR-EDGE-10: a substitute whose element the module cannot locate is inert.
    return { update: NOOP, dispose: NOOP };
  }

  const state = rootState(rootElement);
  const cleanups: Array<() => void> = [];
  let disposed = false;
  const part = options.part;
  // The content's style stays C#'s throughout: the module writes none there (SCR-DOM-13).
  const handsOverStyle = part !== 'content';

  if (handsOverStyle) {
    takeOver(element, options.style);
  }

  switch (part) {
    case 'root':
      attachRoot();
      break;
    case 'viewport':
      attachViewport();
      break;
    case 'content':
      attachContent();
      break;
    case 'scrollbar':
      attachScrollbar();
      break;
    case 'thumb':
      attachThumb();
      break;
    case 'corner':
      attachCorner();
      break;
  }

  function vertical(): boolean {
    return (options.orientation ?? 'vertical') === 'vertical';
  }

  function attachRoot(): void {
    state.twin = createTwinChannel(dotNetRef);
    state.rtl = options.rtl ?? false;
    state.thresholds = options.thresholds ?? state.thresholds;
    writeCornerSize(state);

    // ScrollAreaRoot.tsx:238-272: hover and modality, on the root's own listeners.
    const enterOrMove = (event: PointerEvent) => {
      state.touchModality = event.pointerType === 'touch';
      if (event.pointerType !== 'touch') {
        setHovering(state, contains(state.root, event.target as Element | null));
      }
    };
    cleanups.push(
      listen(element!, 'pointerenter', enterOrMove),
      listen(element!, 'pointermove', enterOrMove),
      listen(element!, 'pointerdown', (event) => {
        state.touchModality = event.pointerType === 'touch';
      }),
      listen(element!, 'pointerleave', () => setHovering(state, false)),
      () => {
        // SCR-ANI-07: the root's scrolling timers.
        clearTimeout(state.scrollTimerX);
        clearTimeout(state.scrollTimerY);
        state.twin = null;
      },
    );
    writeScrolling(state);
    writeHovering(state);
    if (state.viewport) {
      state.queueMeasure();
    }
  }

  function attachViewport(): void {
    state.viewport = element;
    registerOverflowVariables();
    let restoreTimer: ReturnType<typeof setTimeout> | undefined;
    let animationTimer: ReturnType<typeof setTimeout> | undefined;
    const interact = () => {
      state.programmatic = false;
    };

    // SCR-PTR-33, SCR-STA-13, SCR-STA-14.
    cleanups.push(
      listen(element!, 'scroll', () => {
        if (state.viewport !== element) {
          return;
        }
        measure(state);
        if (state.touchModality || !state.programmatic) {
          reportScroll(state, { x: element!.scrollLeft, y: element!.scrollTop });
        }
        clearTimeout(restoreTimer);
        restoreTimer = setTimeout(() => {
          state.programmatic = true;
        }, PROGRAMMATIC_RESTORE_DELAY);
      }),
      listen(element!, 'wheel', interact, { passive: true }),
      listen(element!, 'pointermove', interact),
      listen(element!, 'pointerenter', interact),
      listen(element!, 'keydown', interact),
    );

    // SCR-PTR-30: no enter event fires for an element mounted under a stationary pointer.
    if (element!.matches(':hover')) {
      setHovering(state, true);
    }

    // SCR-ANI-04: the first notification is dropped when it repeats the mount-time measurement's metrics.
    let initialized = false;
    cleanups.push(
      observeResize(element!, () => {
        if (!initialized) {
          initialized = true;
          const last = state.lastMetrics;
          if (
            last &&
            last[0] === element!.clientHeight &&
            last[1] === element!.scrollHeight &&
            last[2] === element!.clientWidth &&
            last[3] === element!.scrollWidth
          ) {
            return;
          }
        }
        measure(state);
      }),
    );

    // SCR-ANI-06: after the finite animations of the subtree settle, holding no reference to them (SCR-EDGE-07).
    animationTimer = setTimeout(() => {
      const animations = getFiniteAnimations(element!, { subtree: true });
      if (animations.length === 0) {
        return;
      }
      Promise.allSettled(animations.map((animation) => animation.finished.then(NOOP)))
        .then(() => {
          if (!disposed) {
            measure(state);
          }
        })
        .catch(NOOP);
    }, 0);

    cleanups.push(() => {
      // SCR-ANI-07.
      clearTimeout(animationTimer);
      clearTimeout(restoreTimer);
      if (state.viewport === element) {
        state.viewport = null;
      }
    });
    writeScrolling(state);
    // SCR-ANI-01: one microtask after the viewport's handle attaches.
    state.queueMeasure();
  }

  function attachContent(): void {
    state.content = element;
    // SCR-ANI-05: the first notification counts only when the content came after the first measurement.
    const computeOnInitialResize = state.measured;
    let initialized = false;
    cleanups.push(
      observeResize(element!, () => {
        if (!initialized) {
          initialized = true;
          if (!computeOnInitialResize) {
            return;
          }
        }
        measure(state);
      }),
      () => {
        if (state.content === element) {
          state.content = null;
        }
      },
    );
    writeScrolling(state);
  }

  function attachScrollbar(): void {
    const isVertical = vertical();
    if (isVertical) {
      state.scrollbarY = element;
    } else {
      state.scrollbarX = element;
    }
    cleanups.push(
      listen(element!, 'pointerdown', (event) => trackPress(state, event, element!, vertical())),
      // SCR-PTR-20: pressing a scrollbar never moves focus.
      listen(element!, 'mousedown', (event) => event.preventDefault()),
      listen(element!, 'pointerup', (event) => dragEnd(state, event)),
      listen(element!, 'pointercancel', (event) => dragEnd(state, event)),
      // SCR-PTR-26: native and not passive, so it is no merged handler and a consumer cannot veto it.
      listen(element!, 'wheel', (event) => wheel(state, event, vertical()), { passive: false }),
      () => {
        if (state.scrollbarY === element) {
          state.scrollbarY = null;
        }
        if (state.scrollbarX === element) {
          state.scrollbarX = null;
        }
      },
    );
    writeThumbSize(state);
    writeScrolling(state);
    writeHovering(state);
    state.queueMeasure();
  }

  function attachThumb(): void {
    if (vertical()) {
      state.thumbY = element;
    } else {
      state.thumbX = element;
    }
    cleanups.push(
      listen(element!, 'pointerdown', (event) => dragStart(state, event, element!)),
      listen(element!, 'pointermove', (event) => dragMove(state, event)),
      listen(element!, 'pointerup', (event) => dragEnd(state, event)),
      listen(element!, 'pointercancel', (event) => dragEnd(state, event)),
      () => {
        if (state.thumbY === element) {
          state.thumbY = null;
        }
        if (state.thumbX === element) {
          state.thumbX = null;
        }
      },
    );
    writeScrolling(state);
    state.queueMeasure();
  }

  function attachCorner(): void {
    state.corner = element;
    writeCornerSize(state);
    cleanups.push(() => {
      if (state.corner === element) {
        state.corner = null;
      }
    });
    state.queueMeasure();
  }

  return {
    update(nextJson, nextReferences, nextCaptures) {
      if (disposed) {
        return;
      }
      void nextReferences;
      void nextCaptures;
      const next = parseOptions<ScrollAreaOptions>(nextJson);
      const previous = options;
      options = next;
      if (handsOverStyle) {
        takeOver(element, next.style);
      }
      if (part === 'root') {
        const thresholds = next.thresholds ?? state.thresholds;
        const changed =
          (next.rtl ?? false) !== state.rtl ||
          thresholds.xStart !== state.thresholds.xStart ||
          thresholds.xEnd !== state.thresholds.xEnd ||
          thresholds.yStart !== state.thresholds.yStart ||
          thresholds.yEnd !== state.thresholds.yEnd;
        state.rtl = next.rtl ?? false;
        state.thresholds = thresholds;
        // SCR-ANI-02: a direction or threshold change measures without a scroll.
        if (changed) {
          state.queueMeasure();
        }
      } else if ((part === 'scrollbar' || part === 'thumb') && previous.orientation !== next.orientation) {
        // A new orientation moves the registration to the other axis.
        for (const key of ['scrollbarX', 'scrollbarY', 'thumbX', 'thumbY'] as const) {
          if (state[key] === element) {
            state[key] = null;
          }
        }
        if (part === 'scrollbar') {
          if (vertical()) {
            state.scrollbarY = element;
          } else {
            state.scrollbarX = element;
          }
        } else if (vertical()) {
          state.thumbY = element;
        } else {
          state.thumbX = element;
        }
        writeThumbSize(state);
        writeScrolling(state);
        state.queueMeasure();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      for (const cleanup of cleanups.splice(0).reverse()) {
        cleanup();
      }
      if (part !== 'root' && part !== 'viewport') {
        // SCR-ANI-02: removing a scrollbar, a thumb or the corner measures again.
        state.queueMeasure();
      }
    },
  };
}
