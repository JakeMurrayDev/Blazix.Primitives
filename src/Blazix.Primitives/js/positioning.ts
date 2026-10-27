// The `positioning` mechanism module (ADR 04 §1: "engine assembly, Base UI middleware, anchor tracking, output writes";
// ADR 03): one handle per Positioner over the bundled Floating UI engine, which only this module imports, through the
// wrapper that carries its licence banner (js/floating-ui.ts; ADR 04 and ADR 07 Amendments 2026-10-27).
// Transcribed from Base UI v1.9.0: packages/react/src/internals/useAnchorPositioning.ts (the pipeline assembly, offsets,
// collision normalization, the transformOrigin middleware, lazy flip, the kept-mounted gating, anchor resolution, the
// outputs), floating-ui-react/hooks/useFloating.ts (the wrapped position reference), floating-ui-react/middleware/arrow.ts
// (the arrow fork), utils/hideMiddleware.ts, utils/adaptiveOriginMiddleware.ts and adaptiveOriginConstants.ts,
// utils/usePositioner.tsx (pointer-events while closed, the disabled-mount transition), and the parts of
// @floating-ui/react-dom's useFloating that Base UI relies on (the device-pixel rounding, `will-change`, `isPositioned`
// and its reset, a pass committed only when its data changed).
// Requirements: floating-and-positioning.md FLT-STA-07, FLT-STA-09, FLT-API-08..12, FLT-API-17, FLT-DOM-01..10,
// FLT-ANI-02..05, FLT-POS-01..17, FLT-I18N-01..03, FLT-ALG-09..14. FLT-ALG-01..08 are the engine's (ADR 03).
//
// The module asks no decision point. C# sends the options in `update`; the open and mounted inputs are read from the
// applied render batch (the Positioner's open attribute and `hidden`, design point 7 settled by the orchestrator
// 2026-10-27); one report per change of the mirrored side, align, anchorHidden and uncentered, never one per pass.
import {
  autoUpdate,
  clamp,
  computePosition,
  evaluate,
  flip,
  getAlignment,
  getAlignmentAxis,
  getAxisLength,
  getPaddingObject,
  getSide,
  getSideAxis,
  limitShift,
  offset,
  shift as floatingShift,
  size,
  type AutoUpdateOptions,
  type Boundary,
  type Derivable,
  type Middleware,
  type MiddlewareData,
  type MiddlewareState,
  type Padding,
  type Placement,
  type ReferenceElement,
  type Side as PhysicalSide,
  type SideObject,
  type Strategy,
  type VirtualElement,
} from './floating-ui';
import { ownerDocument, ownerWindow, styleWriter, type StyleLayer } from './dom';
import { observeOpenInput } from './transitions';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  resolveReference,
  type DotNetTwin,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

/** A side of the anchor, physical or logical; Base UI's `Side`. */
export type Side = 'top' | 'bottom' | 'left' | 'right' | 'inline-end' | 'inline-start';

/** An alignment along the side; Base UI's `Align`. */
export type Align = 'start' | 'center' | 'end';

/** How an axis avoids collisions; Base UI's `SideFlipMode`/`SideShiftMode` literals. */
export type CollisionAvoidanceMode = 'flip' | 'shift' | 'none';

/** The callback C# receives once per change of the mirrored values. */
export const PLACEMENT_REPORT = 'OnPlacementChange';

/** What `PLACEMENT_REPORT` carries; mirrors `PositioningPlacement` in C#. */
export interface PlacementReport {
  side: Side;
  align: Align;
  anchorHidden: boolean;
  uncentered: boolean;
}

/** A rectangle in viewport coordinates; mirrors `PositioningRect` in C#. */
export interface PositioningRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

// Traceability of the fields: sessionToken ADR 04 Amendments 2026-10-27 (the session, track 3); transitionStatus FLT-ANI-05;
// triggerId FLT-POS-13, PSH; anchorId, anchorRef and virtualAnchor FLT-API-11, FLT-STA-07; boundaryRefs FLT-API-09 (the
// element cases as references, ADR 04 Amendments 2026-10-27); the offsets and their function forms
// FLT-API-12 (design point 2); sticky FLT-POS-14; disableAnchorTracking FLT-POS-11; collisionAvoidance FLT-API-10; shift
// FLT-API-17; lazyFlip FLT-ANI-04; adaptiveOrigin FLT-DOM-04, PSH-POS-02; direction FLT-I18N-01; arrowId and arrowStyle
// FLT-DOM-09/10 (design point 6).
/** The options of a `positioning` handle; mirrors `PositioningOptions` in C#. */
export interface PositioningOptions extends TwinOptions {
  /** The popup's session token; looked up through the `interactions` export once it exists. */
  sessionToken?: string | null;
  /** The selector the Positioner matches while C# renders it open; `[data-open]` when absent. Mounted is `:not([hidden])`. */
  openSelector?: string | null;
  /** Whether the popup stays in the document while closed: tracking runs only while mounted and `isPositioned` resets on unmount. */
  keepMounted?: boolean;
  /** The popup's transition status; `starting` adds `transition: none`. */
  transitionStatus?: 'starting' | 'ending' | 'idle' | null;
  /** The id of the active trigger: the position reference when no anchor is given. */
  triggerId?: string | null;
  /** The id of a custom anchor element. */
  anchorId?: string | null;
  /** The index of a custom anchor element in the references that follow the options. */
  anchorRef?: number | null;
  /** A virtual anchor in viewport coordinates, re-sent on change. */
  virtualAnchor?: PositioningRect | null;
  /** `absolute`, the default, or `fixed`. */
  positionMethod?: Strategy;
  /** The requested side; `bottom` when absent. */
  side?: Side;
  /** The requested alignment; `center` when absent. */
  align?: Align;
  /** The distance from the anchor in pixels; 0 when absent. */
  sideOffset?: number;
  /** The declarative form of Base UI's side-offset function; when set, it is evaluated on every pass instead of `sideOffset`. */
  sideOffsetFunction?: OffsetFunction | null;
  /** The distance along the alignment axis in pixels; 0 when absent. */
  alignOffset?: number;
  /** The declarative form of Base UI's align-offset function; when set, it is evaluated on every pass instead of `alignOffset`. */
  alignOffsetFunction?: OffsetFunction | null;
  /** `clipping-ancestors`, the default when no other boundary is given. */
  collisionBoundary?: 'clipping-ancestors' | null;
  /** A boundary rectangle in viewport coordinates. */
  boundaryRect?: PositioningRect | null;
  /** The indexes of the boundary elements in the references that follow the options. */
  boundaryRefs?: readonly number[] | null;
  /** One padding for every side; 5 when neither padding is given. */
  collisionPadding?: number | null;
  /** A padding per side, a missing side being 0. */
  collisionPaddingSides?: Partial<SideObject> | null;
  /** Whether the popup stays inside the boundary when the anchor leaves it. */
  sticky?: boolean;
  /** The arrow's distance from the popup's edges; 5 when absent. */
  arrowPadding?: number;
  /** Whether anchor tracking is off. */
  disableAnchorTracking?: boolean;
  /** How each axis avoids collisions; every key defaults individually. */
  collisionAvoidance?: { side?: CollisionAvoidanceMode | null; align?: CollisionAvoidanceMode | null; fallbackAxisSide?: 'start' | 'end' | 'none' | null } | null;
  /** Base UI's private `shift` option: Context Menu and Navigation Menu pass it. */
  shift?: { crossAxis?: boolean; rootBoundary?: 'layoutViewport' } | null;
  /** Base UI's private `lazyFlip`: `side` for `true`, `placement` to lock the alignment too. */
  lazyFlip?: 'side' | 'placement' | null;
  /** Whether a Viewport part is present, which adds the adaptive-origin middleware. */
  adaptiveOrigin?: boolean;
  /** The reading direction from the nearest DirectionProvider. */
  direction?: 'ltr' | 'rtl';
  /** The id of the registered Arrow part, or none for the detached stand-in. */
  arrowId?: string | null;
  /** The Arrow's merged consumer style, composed under the arrow styles from the arrow's handover on. */
  arrowStyle?: string | null;
}

// ---------------------------------------------------------------------------------------------------------------
// The Base UI middleware (FLT-ALG-09..12)
// ---------------------------------------------------------------------------------------------------------------

// FLT-ALG-10, FLT-POS-09.
/** Base UI's `hide` replacement: the engine's `referenceHidden` strategy, or an all-zero anchor rect. */
export const hide: Middleware = {
  name: 'hide',
  async fn(state) {
    const { width, height, x, y } = state.rects.reference;
    const anchorHidden = width === 0 && height === 0 && x === 0 && y === 0;
    // Mirrors Floating UI's `hide()` referenceHidden strategy. Floating UI injects `detectOverflow` into the middleware
    // platform before invoking middleware.
    const overflow = await state.platform.detectOverflow!(state, {
      elementContext: 'reference',
    });
    const referenceHidden =
      overflow.top - height >= 0 || overflow.right - width >= 0 || overflow.bottom - height >= 0 || overflow.left - width >= 0;

    return {
      data: {
        referenceHidden: referenceHidden || anchorHidden,
      },
    };
  },
};

/** The options of the arrow fork. */
export interface ArrowOptions {
  /** The arrow element, or `null` for none. */
  element: Element | null;
  /** The padding between the arrow and the floating element's edges. */
  padding?: Padding;
}

// FLT-ALG-09.
/**
 * Base UI's fork of the engine's `arrow` middleware, which always measures against the floating element
 * rather than the arrow's offset parent.
 * @param options The element and padding, or a function of the middleware state returning them.
 * @returns The middleware.
 */
export function arrow(options: ArrowOptions | Derivable<ArrowOptions>): Middleware {
  return {
    name: 'arrow',
    options,
    async fn(state) {
      const { x, y, placement, rects, platform, elements, middlewareData } = state;
      // Since `element` is required, we don't Partial<> the type.
      const { element, padding = 0 } = evaluate(options, state) || {};

      if (element == null) {
        return {};
      }

      const paddingObject = getPaddingObject(padding);
      const coords = { x, y };
      const axis = getAlignmentAxis(placement);
      const length = getAxisLength(axis);
      const arrowDimensions = await platform.getDimensions(element);
      const isYAxis = axis === 'y';
      const minProp = isYAxis ? 'top' : 'left';
      const maxProp = isYAxis ? 'bottom' : 'right';
      const clientProp = isYAxis ? 'clientHeight' : 'clientWidth';

      const endDiff = rects.reference[length] + rects.reference[axis] - coords[axis] - rects.floating[length];
      const startDiff = coords[axis] - rects.reference[axis];

      const clientSize = (elements.floating as HTMLElement)[clientProp] || rects.floating[length];

      const centerToReference = endDiff / 2 - startDiff / 2;

      // If the padding is large enough that it causes the arrow to no longer be centered, modify the padding so that
      // it is centered.
      const largestPossiblePadding = clientSize / 2 - arrowDimensions[length] / 2 - 1;
      const minPadding = Math.min(paddingObject[minProp], largestPossiblePadding);
      const maxPadding = Math.min(paddingObject[maxProp], largestPossiblePadding);

      // Make sure the arrow doesn't overflow the floating element if the center point is outside the floating
      // element's bounds.
      const min = minPadding;
      const max = clientSize - arrowDimensions[length] - maxPadding;
      const center = clientSize / 2 - arrowDimensions[length] / 2 + centerToReference;
      const arrowOffset = clamp(min, center, max);

      // If the reference is small enough that the arrow's padding causes it to to point to nothing for an aligned
      // placement, adjust the offset of the floating element itself. To ensure `shift()` continues to take action, a
      // single reset is performed when this is true.
      const shouldAddOffset =
        !middlewareData.arrow &&
        getAlignment(placement) != null &&
        center !== arrowOffset &&
        rects.reference[length] / 2 - (center < min ? minPadding : maxPadding) - arrowDimensions[length] / 2 < 0;
      const alignmentOffset = shouldAddOffset ? (center < min ? center - min : center - max) : 0;

      return {
        [axis]: coords[axis] + alignmentOffset,
        data: {
          [axis]: arrowOffset,
          centerOffset: center - arrowOffset - alignmentOffset,
          ...(shouldAddOffset && { alignmentOffset }),
        },
        reset: shouldAddOffset,
      };
    },
  };
}

/** The sides the adaptive-origin coordinates are measured from when no transition runs. */
export const DEFAULT_SIDES = { sideX: 'left', sideY: 'top' } as const;

// FLT-ALG-12, FLT-ANI-03, FLT-DOM-04.
/**
 * Base UI's `adaptiveOrigin` middleware: while the floating element has a transition, a left
 * placement is measured from the right edge and a top placement from the bottom edge, so a size transition grows away
 * from the anchor. PSH's Viewport supplies it; the module runs it last when the `adaptiveOrigin` option is on.
 */
export const adaptiveOrigin: Middleware = {
  name: 'adaptiveOrigin',
  async fn(state) {
    const {
      x: rawX,
      y: rawY,
      rects: { floating: floatRect },
      elements: { floating },
      platform,
      strategy,
      placement,
    } = state;

    const win = ownerWindow(floating);
    const styles = win.getComputedStyle(floating);
    const hasTransition = styles.transitionDuration !== '0s' && styles.transitionDuration !== '';

    if (!hasTransition) {
      return {
        x: rawX,
        y: rawY,
        data: DEFAULT_SIDES,
      };
    }

    const offsetParent = await platform.getOffsetParent?.(floating);

    let offsetDimensions = { width: 0, height: 0 };

    // For fixed strategy, prefer visualViewport if available
    if (strategy === 'fixed' && win?.visualViewport) {
      offsetDimensions = {
        width: win.visualViewport.width,
        height: win.visualViewport.height,
      };
    } else if (offsetParent === win) {
      const doc = ownerDocument(floating);
      offsetDimensions = {
        width: doc.documentElement.clientWidth,
        height: doc.documentElement.clientHeight,
      };
    } else if (await platform.isElement?.(offsetParent)) {
      offsetDimensions = await platform.getDimensions(offsetParent as Element);
    }

    const currentSide = getSide(placement);
    let x = rawX;
    let y = rawY;

    if (currentSide === 'left') {
      x = offsetDimensions.width - (rawX + floatRect.width);
    }
    if (currentSide === 'top') {
      y = offsetDimensions.height - (rawY + floatRect.height);
    }

    const sideX = currentSide === 'left' ? 'right' : DEFAULT_SIDES.sideX;
    const sideY = currentSide === 'top' ? 'bottom' : DEFAULT_SIDES.sideY;
    return {
      x,
      y,
      data: {
        sideX,
        sideY,
      },
    };
  },
};

/** What the `transformOrigin` middleware reads from the handle and where it writes. */
export interface TransformOriginOptions {
  /** The registered Arrow element, or `null` when the stand-in is used. */
  arrow(): HTMLElement | null;
  /** The side offset of the pass, resolved from the state. */
  sideOffset(state: MiddlewareState): number;
  /** The side-axis shift flag: whether the popup also shifts on the side axis. */
  crossAxisShiftEnabled: boolean;
  /** Writes the `--transform-origin` value. */
  write(value: string): void;
}

// FLT-ALG-11, FLT-DOM-07, FLT-I18N-03.
/**
 * Base UI's `transformOrigin` middleware: runs after `arrow`, before `hide`, and writes
 * `--transform-origin` from the arrow position, the alignment, the platform's direction and the shift.
 * @param options What the middleware reads and where it writes.
 * @returns The middleware.
 */
export function transformOrigin(options: TransformOriginOptions): Middleware {
  return {
    name: 'transformOrigin',
    fn(state) {
      const {
        elements: { floating },
        middlewareData,
        placement: renderedPlacement,
        platform,
        rects,
        y,
      } = state;

      const renderedSide = getSide(renderedPlacement);
      const renderedAlign = getAlignment(renderedPlacement);
      const isVertical = getSideAxis(renderedSide) === 'y';
      const arrowEl = options.arrow();

      const sideOffsetValue = options.sideOffset(state);

      // An aligned arrowless popup grows from its aligned edge, until a shift (beyond subpixel) breaks its alignment
      // with the anchor. Everything else grows from the arrow, real or fake.
      let crossOrigin: string;
      if (!arrowEl && renderedAlign && Math.abs(isVertical ? middlewareData.shift?.x || 0 : middlewareData.shift?.y || 0) <= 1) {
        // The platform direction, not `isRtl`: it must match what Floating UI placed with.
        crossOrigin = (renderedAlign === 'start') === (isVertical && platform.isRTL?.(floating) === true) ? '100%' : '0%';
      } else {
        const arrowOffset = isVertical ? middlewareData.arrow?.x || 0 : middlewareData.arrow?.y || 0;
        const arrowSize = isVertical ? arrowEl?.clientWidth || 0 : arrowEl?.clientHeight || 0;
        crossOrigin = `${arrowOffset + arrowSize / 2}px`;
      }

      // Side axis: the anchor-facing edge, or the anchor's center when the popup overlaps it.
      let sideOrigin = renderedSide === 'top' || renderedSide === 'left' ? `calc(100% + ${sideOffsetValue}px)` : `${-sideOffsetValue}px`;
      if (options.crossAxisShiftEnabled && isVertical && Math.abs(middlewareData.shift?.y || 0) > sideOffsetValue) {
        sideOrigin = `${rects.reference.y + rects.reference.height / 2 - y}px`;
      }

      options.write(isVertical ? `${crossOrigin} ${sideOrigin}` : `${sideOrigin} ${crossOrigin}`);

      return {};
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// Sides and offsets (FLT-I18N-01/02, FLT-API-12)
// ---------------------------------------------------------------------------------------------------------------

// FLT-I18N-02.
/**
 * Returns the side reported for a rendered physical side: logical when the requested side was logical.
 * @param sideParam The requested side.
 * @param renderedSide The physical side of the rendered placement.
 * @param isRtl Whether the reading direction is right to left.
 * @returns The reported side.
 */
export function getLogicalSide(sideParam: Side, renderedSide: PhysicalSide, isRtl: boolean): Side {
  const isLogicalSideParam = sideParam === 'inline-start' || sideParam === 'inline-end';
  const logicalRight = isRtl ? 'inline-start' : 'inline-end';
  const logicalLeft = isRtl ? 'inline-end' : 'inline-start';
  return (
    {
      top: 'top',
      right: isLogicalSideParam ? logicalRight : 'right',
      bottom: 'bottom',
      left: isLogicalSideParam ? logicalLeft : 'left',
    } satisfies Record<PhysicalSide, Side>
  )[renderedSide];
}

// FLT-API-12.
/** The data an offset is evaluated with. */
export interface OffsetData {
  side: Side;
  align: Align;
  anchor: { width: number; height: number };
  positioner: { width: number; height: number };
}

// FLT-API-12.
/**
 * Returns the data of the placement a pass is evaluating: its logical side, its alignment, and the measured
 * anchor and floating sizes.
 * @param state The middleware state.
 * @param sideParam The requested side.
 * @param isRtl Whether the reading direction is right to left.
 * @returns The offset data.
 */
export function getOffsetData(state: MiddlewareState, sideParam: Side, isRtl: boolean): OffsetData {
  const { rects, placement } = state;
  return {
    side: getLogicalSide(sideParam, getSide(placement), isRtl),
    align: getAlignment(placement) || 'center',
    anchor: { width: rects.reference.width, height: rects.reference.height },
    positioner: { width: rects.floating.width, height: rects.floating.height },
  };
}

/** The terms of an offset: a constant plus multiples of the measured anchor and positioner sizes. */
export interface OffsetTerms {
  constant?: number;
  anchorWidth?: number;
  anchorHeight?: number;
  positionerWidth?: number;
  positionerHeight?: number;
}

/** Terms that replace the base terms when the evaluated placement has this side and alignment (a `null` key matches any). */
export interface OffsetOverride extends OffsetTerms {
  side?: Side | null;
  align?: Align | null;
}

// Design point 2 of the positioning track, settled by the orchestrator 2026-10-27.
/** The declarative form of Base UI's offset function; mirrors `PositioningOffset` in C#. */
export interface OffsetFunction extends OffsetTerms {
  overrides?: OffsetOverride[] | null;
}

// FLT-API-12, FLT-POS-06.
/**
 * Evaluates an offset for the placement a pass evaluates: a number as given, or the terms of the first
 * override whose side and align match (else the base terms) applied to the measured sizes.
 * @param value A number, the declarative function, or nothing for 0.
 * @param data The offset data of the pass.
 * @returns The offset in pixels.
 */
export function evaluateOffset(value: number | OffsetFunction | null | undefined, data: OffsetData): number {
  if (value == null) {
    return 0;
  }
  if (typeof value === 'number') {
    return value;
  }
  const terms =
    value.overrides?.find((entry) => (entry.side == null || entry.side === data.side) && (entry.align == null || entry.align === data.align)) ?? value;
  return (
    (terms.constant ?? 0) +
    (terms.anchorWidth ?? 0) * data.anchor.width +
    (terms.anchorHeight ?? 0) * data.anchor.height +
    (terms.positionerWidth ?? 0) * data.positioner.width +
    (terms.positionerHeight ?? 0) * data.positioner.height
  );
}

/** What `readPlacement` returns: the rendered placement and whether the current mount has been positioned. */
export interface RenderedPlacement {
  side: Side;
  align: Align;
  physicalSide: PhysicalSide;
  isPositioned: boolean;
}

const physicalSides: Record<Side, (isRtl: boolean) => PhysicalSide> = {
  top: () => 'top',
  right: () => 'right',
  bottom: () => 'bottom',
  left: () => 'left',
  'inline-end': (isRtl) => (isRtl ? 'left' : 'right'),
  'inline-start': (isRtl) => (isRtl ? 'right' : 'left'),
};

// ---------------------------------------------------------------------------------------------------------------
// The twin-facing entry
// ---------------------------------------------------------------------------------------------------------------

// A symbol on the element, not a module registry: a repeated attach of the same element returns the live handle, and
// the exports below find it.
const attachedHandle = Symbol('blazix.positioning.handle');

interface PositioningHandle extends TwinHandle {
  placement(): RenderedPlacement;
  position(): void;
}

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: PositioningHandle;
}

type Declarations = Map<string, string>;

// Writes a layer's next declarations, touching only the properties that changed since the previous write.
function writeLayer(layer: StyleLayer, previous: Declarations, next: Declarations): void {
  for (const property of previous.keys()) {
    if (!next.has(property)) {
      layer.remove(property);
    }
  }
  for (const [property, value] of next) {
    if (previous.get(property) !== value) {
      layer.set(property, value);
    }
  }
}

// FLT-DOM-08; design point 3 of the positioning track, settled by the orchestrator 2026-10-27.
/**
 * Reads the placement an attached Positioner rendered last, for the `popup` module's anchoring styles: before the first
 * pass, the requested placement.
 * @param element The Positioner element.
 * @returns The rendered placement and whether the current mount has been positioned, or `null` when no handle is attached.
 */
export function readPlacement(element: Element): RenderedPlacement | null {
  return (element as AttachedElement)[attachedHandle]?.placement() ?? null;
}

// FLT-POS-16; design point 3 of the positioning track.
/**
 * Runs one positioning pass of an attached Positioner now, as Base UI's `positioning.update()`: PreviewCard
 * calls it when its hovered line changes.
 * @param element The Positioner element.
 */
export function update(element: Element): void {
  (element as AttachedElement)[attachedHandle]?.position();
}

interface PassData {
  x: number;
  y: number;
  placement: Placement;
  strategy: Strategy;
  middlewareData: MiddlewareData;
}

function deepEqual(a: unknown, b: unknown): boolean {
  if (a === b) {
    return true;
  }
  if (typeof a !== 'object' || typeof b !== 'object' || a === null || b === null) {
    return false;
  }
  if (Array.isArray(a) !== Array.isArray(b)) {
    return false;
  }
  const keysA = Object.keys(a);
  const keysB = Object.keys(b);
  if (keysA.length !== keysB.length) {
    return false;
  }
  return keysA.every((key) => deepEqual((a as Record<string, unknown>)[key], (b as Record<string, unknown>)[key]));
}

// Writes an attribute only when it changes: every write queues a mutation record for each observer of the element.
function writeAttribute(element: Element, name: string, value: string | null): void {
  if (element.getAttribute(name) === value) {
    return;
  }
  if (value === null) {
    element.removeAttribute(name);
  } else {
    element.setAttribute(name, value);
  }
}

function roundByDpr(element: Element, value: number): number {
  const dpr = ownerWindow(element).devicePixelRatio || 1;
  return Math.round(value * dpr) / dpr;
}

function normalizePadding(options: PositioningOptions): SideObject {
  const sides = options.collisionPaddingSides;
  if (sides) {
    return { top: sides.top || 0, right: sides.right || 0, bottom: sides.bottom || 0, left: sides.left || 0 };
  }
  const all = options.collisionPadding ?? 5;
  return { top: all, right: all, bottom: all, left: all };
}

/**
 * Attaches the module to a Positioner element.
 * @param elementOrId The Positioner element, or its id.
 * @param optionsJson The `PositioningOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param initialReferences The anchor and boundary elements the options name by index, or nothing when they name none.
 * @returns The handle with `update(optionsJson, references)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin, initialReferences?: TwinReferences): TwinHandle {
  const element = resolveElement(elementOrId) as AttachedElement;
  const existing = element[attachedHandle];
  if (existing) {
    return existing;
  }

  const twin = createTwinChannel(twinReference);
  let options = parseOptions<PositioningOptions>(optionsJson);
  let references = initialReferences;
  let disposed = false;

  const writer = styleWriter(element);
  writer.setConsumer(options.style);
  const positionerLayer = writer.layer('positioning');
  const transitionLayer = writer.layer('transitions');
  const measuredLayer = writer.layer('positioning-measured');
  let positionerDeclarations: Declarations = new Map();
  let arrowElement: HTMLElement | null = null;
  let arrowLayer: StyleLayer | null = null;
  let arrowDeclarations: Declarations = new Map();

  let open = element.matches(options.openSelector || '[data-open]');
  let mounted = !element.hidden;
  let isPositioned = false;
  let data: PassData | null = null;
  // FLT-ANI-04: the rendered placement locked while mounted, when lazy flipping.
  let mountPlacement: Placement | null = null;

  let pipeline: Array<Middleware | null> = [];
  let pipelineKey: string | null = null;
  let placement: Placement = 'bottom';
  let reference: ReferenceElement | null = null;
  let referenceKey: unknown = null;
  let trackingKey: unknown = null;
  let stopTracking: (() => void) | null = null;
  let stopObserving: (() => void) | null = null;
  let observedSelector: string | null = null;

  const sideParam = (): Side => options.side ?? 'bottom';
  const isRtl = (): boolean => options.direction === 'rtl';
  let reported: PlacementReport = { side: sideParam(), align: options.align ?? 'center', anchorHidden: false, uncentered: true };

  const sideOffsetOf = (state: MiddlewareState): number =>
    evaluateOffset(options.sideOffsetFunction ?? options.sideOffset, getOffsetData(state, sideParam(), isRtl()));
  const alignOffsetOf = (state: MiddlewareState): number =>
    evaluateOffset(options.alignOffsetFunction ?? options.alignOffset, getOffsetData(state, sideParam(), isRtl()));

  // The position reference (FLT-STA-07, FLT-POS-13, FLT-API-11): a virtual anchor as given, a custom anchor element (from
  // the references, or by id) wrapped so its own rect is measured, else the active trigger itself (the root store's
  // reference element).
  const resolvePositionReference = (): { key: unknown; value: ReferenceElement | null } => {
    const doc = ownerDocument(element);
    const virtual = options.virtualAnchor;
    if (virtual) {
      const key = JSON.stringify(virtual);
      if (key === referenceKey) {
        return { key, value: reference };
      }
      const { x, y, width, height } = virtual;
      const rect = { x, y, width, height, top: y, left: x, right: x + width, bottom: y + height };
      return { key, value: { getBoundingClientRect: () => ({ ...rect, toJSON: () => rect }) as DOMRect } satisfies VirtualElement };
    }
    const anchor =
      options.anchorRef != null
        ? resolveReference<Element>(references, options.anchorRef)
        : options.anchorId
          ? doc.getElementById(options.anchorId)
          : undefined;
    if (anchor !== undefined) {
      if (anchor === null) {
        return { key: null, value: null };
      }
      if (anchor === referenceKey) {
        return { key: anchor, value: reference };
      }
      return {
        key: anchor,
        value: {
          getBoundingClientRect: () => anchor.getBoundingClientRect(),
          getClientRects: () => anchor.getClientRects(),
          contextElement: anchor,
        } satisfies VirtualElement,
      };
    }
    const trigger = options.triggerId ? doc.getElementById(options.triggerId) : null;
    return { key: trigger, value: trigger };
  };

  // The Arrow has no handle (design point 6, settled by the orchestrator 2026-10-27): its id and style travel here, and
  // it is re-resolved by id on every update; the stand-in of FLT-DOM-10 serves while it does not resolve.
  const resolveArrow = () => {
    const found = options.arrowId ? ownerDocument(element).getElementById(options.arrowId) : null;
    const next = found instanceof HTMLElement ? found : null;
    if (next !== arrowElement) {
      arrowLayer?.clear();
      arrowElement = next;
      arrowLayer = next ? styleWriter(next).layer('positioning') : null;
      arrowDeclarations = new Map();
    }
    if (arrowElement) {
      styleWriter(arrowElement).setConsumer(options.arrowStyle);
    }
  };

  // FLT-API-09: the boundary elements the references carry, passed to the engine as a list (the engine reads one element
  // and a list of it alike), else the rect, else the clipping ancestors. The pipeline key names an element by a number
  // the handle gives it, so a different element at the same index rebuilds the pipeline.
  const elementKeys = new WeakMap<Element, number>();
  let nextElementKey = 0;
  const keyOf = (target: Element): number => {
    let key = elementKeys.get(target);
    if (key === undefined) {
      key = nextElementKey++;
      elementKeys.set(target, key);
    }
    return key;
  };
  const resolveBoundary = (): Boundary => {
    if (options.boundaryRefs) {
      return options.boundaryRefs.map((index) => resolveReference<Element>(references, index)).filter((entry): entry is Element => entry !== null);
    }
    return options.boundaryRect ?? 'clippingAncestors';
  };

  // FLT-ALG-14: the pipeline, rebuilt only when one of its inputs changes.
  const assemble = () => {
    const isRtlValue = isRtl();
    const avoidance = options.collisionAvoidance ?? {};
    const collisionAvoidanceSide = avoidance.side || 'flip';
    const collisionAvoidanceAlign = avoidance.align || 'flip';
    const collisionAvoidanceFallbackAxisSide = avoidance.fallbackAxisSide || 'end';
    const shiftCrossAxis = options.shift?.crossAxis ?? false;
    const shiftRootBoundary = options.shift?.rootBoundary;
    const sticky = options.sticky ?? false;
    const arrowPadding = options.arrowPadding ?? 5;
    const align = options.align ?? 'center';
    const lockAlign = options.lazyFlip === 'placement';
    const mountSide = mountPlacement ? getSide(mountPlacement) : null;
    const mountAlign = mountPlacement && lockAlign ? getAlignment(mountPlacement) || 'center' : null;
    const side = mountSide || physicalSides[sideParam()](isRtlValue);
    const placementAlign = mountAlign || align;
    const nextPlacement = placementAlign === 'center' ? side : (`${side}-${placementAlign}` as Placement);
    const collisionPadding = normalizePadding(options);
    const boundary = resolveBoundary();

    const key = JSON.stringify([
      sideParam(),
      nextPlacement,
      options.positionMethod ?? 'absolute',
      collisionAvoidanceSide,
      collisionAvoidanceAlign,
      collisionAvoidanceFallbackAxisSide,
      shiftCrossAxis,
      shiftRootBoundary,
      sticky,
      arrowPadding,
      collisionPadding,
      Array.isArray(boundary) ? boundary.map(keyOf) : boundary,
      isRtlValue,
      options.adaptiveOrigin ?? false,
      options.sideOffsetFunction ?? options.sideOffset ?? 0,
      options.alignOffsetFunction ?? options.alignOffset ?? 0,
    ]);
    if (key === pipelineKey) {
      return;
    }
    pipelineKey = key;
    placement = nextPlacement;

    // Create a bias to the preferred side. On iOS, when the mobile software keyboard opens, the input is exactly
    // centered in the viewport, but this can cause it to flip to the top undesirably. The bias is only applied to
    // `flip()` so it doesn't shift the resting position computed by `shift()` and `size()` away from the requested
    // `collisionPadding`.
    const bias = 1;
    const biasTop = sideParam() === 'bottom' ? bias : 0;
    const biasBottom = sideParam() === 'top' ? bias : 0;
    const biasLeft = sideParam() === 'right' ? bias : 0;
    const biasRight = sideParam() === 'left' ? bias : 0;

    const commonCollisionProps = { boundary, padding: collisionPadding } as const;

    const middleware: Array<Middleware | null> = [];
    // The first slot is Base UI's `inline` middleware (FLT-POS-02), PreviewCard's hovered-line rect.

    middleware.push(
      offset((state) => {
        const sideAxis = sideOffsetOf(state);
        const alignAxis = alignOffsetOf(state);
        return {
          mainAxis: sideAxis,
          crossAxis: alignAxis,
          alignmentAxis: alignAxis,
        };
      }),
    );

    const shiftDisabled = collisionAvoidanceAlign === 'none' && collisionAvoidanceSide !== 'shift';
    const crossAxisShiftEnabled = !shiftDisabled && (sticky || shiftCrossAxis || collisionAvoidanceSide === 'shift');

    const flipMiddleware =
      collisionAvoidanceSide === 'none'
        ? null
        : flip({
            ...commonCollisionProps,
            // Ensure the popup flips if it's been limited by its --available-height and it resizes. Since the size()
            // padding is smaller than the flip() padding, flip() will take precedence.
            padding: {
              top: collisionPadding.top + bias + biasTop,
              right: collisionPadding.right + bias + biasRight,
              bottom: collisionPadding.bottom + bias + biasBottom,
              left: collisionPadding.left + bias + biasLeft,
            },
            mainAxis: !shiftCrossAxis && collisionAvoidanceSide === 'flip',
            crossAxis: collisionAvoidanceAlign === 'flip' ? 'alignment' : false,
            fallbackAxisSideDirection: collisionAvoidanceFallbackAxisSide,
          });
    const shiftMiddleware = shiftDisabled
      ? null
      : floatingShift({
          ...commonCollisionProps,
          // Use the Layout Viewport to avoid shifting around when pinch-zooming.
          rootBoundary: shiftRootBoundary,
          mainAxis: collisionAvoidanceAlign !== 'none',
          crossAxis: crossAxisShiftEnabled,
          limiter:
            sticky || shiftCrossAxis
              ? undefined
              : limitShift((limitData) => {
                  if (!arrowElement) {
                    return {};
                  }
                  const { width, height } = arrowElement.getBoundingClientRect();
                  const sideAxis = getSideAxis(getSide(limitData.placement));
                  const arrowSize = sideAxis === 'y' ? width : height;
                  const offsetAmount =
                    sideAxis === 'y' ? collisionPadding.left + collisionPadding.right : collisionPadding.top + collisionPadding.bottom;
                  return {
                    offset: arrowSize / 2 + offsetAmount / 2,
                  };
                }),
        });

    // https://floating-ui.com/docs/flip#combining-with-shift
    // Keyed on the alignment actually being requested, not the raw prop: a locked alignment can differ from `align`,
    // and the ordering has to match the placement that is asked for.
    if (collisionAvoidanceSide === 'shift' || collisionAvoidanceAlign === 'shift' || placementAlign === 'center') {
      middleware.push(shiftMiddleware, flipMiddleware);
    } else {
      middleware.push(flipMiddleware, shiftMiddleware);
    }

    middleware.push(
      size({
        ...commonCollisionProps,
        apply({ availableWidth, availableHeight, rects }) {
          if (!mounted) {
            return;
          }

          measuredLayer.set('--available-width', `${availableWidth}px`);
          measuredLayer.set('--available-height', `${availableHeight}px`);

          // Snap anchor dimensions to device pixels to ensure the popup's visual width matches the anchor's one.
          const dpr = ownerWindow(element).devicePixelRatio || 1;
          const { x, y, width, height } = rects.reference;
          const anchorWidth = (Math.round((x + width) * dpr) - Math.round(x * dpr)) / dpr;
          const anchorHeight = (Math.round((y + height) * dpr) - Math.round(y * dpr)) / dpr;

          measuredLayer.set('--anchor-width', `${anchorWidth}px`);
          measuredLayer.set('--anchor-height', `${anchorHeight}px`);
        },
      }),
      arrow((state) => ({
        // `transform-origin` calculations rely on an element existing. If the arrow hasn't been set, we'll create a
        // fake element.
        element: arrowElement || ownerDocument(state.elements.floating).createElement('div'),
        // No padding for the fake arrow: it would displace aligned popups on narrow anchors.
        padding: arrowElement ? arrowPadding : 0,
      })),
      transformOrigin({
        arrow: () => arrowElement,
        sideOffset: sideOffsetOf,
        crossAxisShiftEnabled,
        write: (value) => measuredLayer.set('--transform-origin', value),
      }),
      hide,
      options.adaptiveOrigin ? adaptiveOrigin : null,
    );

    pipeline = middleware;
  };

  const report = () => {
    if (data === null) {
      return;
    }
    const next: PlacementReport = {
      side: getLogicalSide(sideParam(), getSide(data.placement), isRtl()),
      align: getAlignment(data.placement) || 'center',
      anchorHidden: Boolean(data.middlewareData.hide?.referenceHidden),
      uncentered: data.middlewareData.arrow?.centerOffset !== 0,
    };
    if (deepEqual(next, reported)) {
      return;
    }
    reported = next;
    twin.report(PLACEMENT_REPORT, next);
  };

  // The outputs (FLT-DOM-01..04, FLT-DOM-08..10, FLT-ANI-02, FLT-ANI-05).
  const render = () => {
    const positionMethod = options.positionMethod ?? 'absolute';
    const declarations: Declarations = new Map();
    // Default to `fixed` when not positioned to prevent `autoFocus` scroll jumps. This ensures the popup is inside the
    // viewport initially before it gets positioned.
    const resolvedPosition = isPositioned ? positionMethod : 'fixed';
    declarations.set('position', resolvedPosition);
    if (!isPositioned || data === null) {
      // Until a position for the current open is computed, ignore any coordinates retained from a previous open (or
      // from a pass that measured the hidden popup as 0x0).
      declarations.set('top', '0px');
      declarations.set('left', '0px');
    } else if (options.adaptiveOrigin) {
      const sides = (data.middlewareData.adaptiveOrigin as typeof DEFAULT_SIDES | undefined) ?? DEFAULT_SIDES;
      declarations.set(sides.sideX, `${data.x}px`);
      declarations.set(sides.sideY, `${data.y}px`);
    } else {
      declarations.set('left', '0px');
      declarations.set('top', '0px');
      declarations.set('transform', `translate(${roundByDpr(element, data.x)}px, ${roundByDpr(element, data.y)}px)`);
      if ((ownerWindow(element).devicePixelRatio || 1) >= 1.5) {
        declarations.set('will-change', 'transform');
      }
    }
    // Seeded so consumer `max-height: min(x, var(--available-height))` rules resolve on the first pass; the measured
    // px values of the over layer win (FLT-DOM-05).
    declarations.set('--available-width', '100vw');
    declarations.set('--available-height', '100vh');
    if (!isPositioned) {
      declarations.set('opacity', '0');
    }
    if (!open) {
      declarations.set('pointer-events', 'none');
    }
    writeLayer(positionerLayer, positionerDeclarations, declarations);
    positionerDeclarations = declarations;
    // TRN's disabled-mount transition, merged after the positioner styles and before the consumer's (FLT-ANI-05).
    if (options.transitionStatus === 'starting') {
      transitionLayer.set('transition', 'none');
    } else {
      transitionLayer.clear();
    }

    const placementNow = data?.placement ?? placement;
    const sideAttribute = getLogicalSide(sideParam(), getSide(placementNow), isRtl());
    const alignAttribute = getAlignment(placementNow) || 'center';
    writeAttribute(element, 'data-side', sideAttribute);
    writeAttribute(element, 'data-align', alignAttribute);
    writeAttribute(element, 'data-anchor-hidden', data?.middlewareData.hide?.referenceHidden ? '' : null);

    if (arrowElement && arrowLayer) {
      const arrowData = data?.middlewareData.arrow;
      const next: Declarations = new Map([['position', 'absolute']]);
      if (arrowData?.y != null) {
        next.set('top', `${arrowData.y}px`);
      }
      if (arrowData?.x != null) {
        next.set('left', `${arrowData.x}px`);
      }
      writeLayer(arrowLayer, arrowDeclarations, next);
      arrowDeclarations = next;
      writeAttribute(arrowElement, 'data-side', sideAttribute);
      writeAttribute(arrowElement, 'data-align', alignAttribute);
      writeAttribute(arrowElement, 'data-uncentered', arrowData?.centerOffset !== 0 ? '' : null);
    }
  };

  // A render may replace the trigger, the anchor or the Arrow with a new element of the same id: the next pass finds it.
  const isDetached = (): boolean => {
    const element = reference === null ? null : 'nodeType' in reference ? reference : reference.contextElement;
    return (element != null && !element.isConnected) || (arrowElement !== null && !arrowElement.isConnected);
  };

  // One positioning pass (FLT-POS-16): committed only when its data differs from the previous pass's.
  const position = () => {
    if (disposed || !mounted || reference === null) {
      return;
    }
    // A new reference restarts the tracking, whose first update is this pass; a new Arrow alone does not.
    if (isDetached() && refresh()) {
      return;
    }
    const passReference = reference;
    void computePosition(passReference, element, { placement, strategy: options.positionMethod ?? 'absolute', middleware: pipeline }).then((result) => {
      if (disposed || passReference !== reference) {
        return;
      }
      // A pass may resolve while closed but still mounted; `isPositioned` stays false then for a kept-mounted popup
      // (the session's `open` input is `mounted`), so the next open starts hidden (FLT-ANI-02).
      const positioned = !(options.keepMounted && !mounted);
      const next: PassData = { x: result.x, y: result.y, placement: result.placement, strategy: result.strategy, middlewareData: result.middlewareData };
      if (positioned === isPositioned && deepEqual(next, data)) {
        return;
      }
      data = next;
      isPositioned = positioned;
      render();
      report();
      lockPlacement();
    });
  };

  // FLT-ANI-04: lock the flipped side (and, with `placement`, the alignment) while mounted.
  const lockPlacement = () => {
    if (!options.lazyFlip || !mounted || !isPositioned || data === null) {
      return;
    }
    const renderedSide = getSide(data.placement);
    const renderedAlign = getAlignment(data.placement) || 'center';
    const requestedSide = getSide(placement);
    const requestedAlign = getAlignment(placement) || 'center';
    if (renderedSide !== requestedSide || (options.lazyFlip === 'placement' && renderedAlign !== requestedAlign)) {
      mountPlacement = data.placement;
      refresh();
    }
  };

  // Anchor tracking (FLT-POS-11/12, FLT-ALG-13, FLT-STA-09): while mounted and the reference resolves. Returns whether
  // the tracking was restarted, which runs a pass at once.
  const syncTracking = (): boolean => {
    const tracking = !options.disableAnchorTracking;
    const autoUpdateOptions: AutoUpdateOptions = {
      ancestorScroll: tracking,
      elementResize: tracking && typeof ResizeObserver !== 'undefined',
      layoutShift: tracking && typeof IntersectionObserver !== 'undefined',
    };
    const key = mounted && reference !== null ? [reference, pipelineKey, JSON.stringify(autoUpdateOptions)] : null;
    if (key !== null && trackingKey !== null && key.every((part, i) => part === (trackingKey as unknown[])[i])) {
      return false;
    }
    stopTracking?.();
    stopTracking = null;
    trackingKey = key;
    if (key !== null && reference !== null) {
      stopTracking = autoUpdate(reference, element, position, autoUpdateOptions);
      return true;
    }
    return false;
  };

  const refresh = (): boolean => {
    if (disposed) {
      return false;
    }
    if (!mounted && mountPlacement !== null) {
      mountPlacement = null;
    }
    assemble();
    if (mounted) {
      const resolved = resolvePositionReference();
      if (resolved.key !== referenceKey || resolved.value !== reference) {
        referenceKey = resolved.key;
        reference = resolved.value;
      }
    } else {
      // FLT-STA-09: a Positioner that is not mounted positions against nothing.
      referenceKey = null;
      reference = null;
    }
    resolveArrow();
    render();
    return syncTracking();
  };

  const observe = () => {
    const selector = options.openSelector || '[data-open]';
    if (selector === observedSelector) {
      return;
    }
    stopObserving?.();
    observedSelector = selector;
    stopObserving = observeOpenInput(element, selector, (nextOpen) => {
      const nextMounted = !element.hidden;
      if (nextOpen === open && nextMounted === mounted) {
        return;
      }
      open = nextOpen;
      if (nextMounted !== mounted) {
        mounted = nextMounted;
        // The session's `open` input of a kept-mounted popup is `mounted` (FLT-POS-12, FLT-ANI-02).
        if (!mounted && options.keepMounted) {
          isPositioned = false;
        }
      }
      refresh();
    });
  };

  const handle: PositioningHandle = {
    update(nextJson: string, nextReferences?: TwinReferences) {
      if (disposed) {
        return;
      }
      options = parseOptions<PositioningOptions>(nextJson);
      references = nextReferences;
      writer.setConsumer(options.style);
      observe();
      refresh();
    },
    placement() {
      const placementNow = data?.placement ?? placement;
      return {
        side: getLogicalSide(sideParam(), getSide(placementNow), isRtl()),
        align: getAlignment(placementNow) || 'center',
        physicalSide: getSide(placementNow),
        isPositioned,
      };
    },
    position,
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      stopTracking?.();
      stopTracking = null;
      stopObserving?.();
      stopObserving = null;
      arrowLayer?.clear();
      positionerLayer.clear();
      transitionLayer.clear();
      measuredLayer.clear();
      delete element[attachedHandle];
    },
  };

  element[attachedHandle] = handle;
  observe();
  refresh();
  return handle;
}
