// The `slider` component module (ADR 04 §1; the placement rule of ADR 04 Amendments 2026-10-18): the Slider family's
// browser half. The seven value algorithms of SLD §4.1 live only here (ADR 01 decision 2); C# never re-derives them.
import { activeElement, clamp, contains, getTarget, isElement, observeResize, ownerDocument, ownerWindow, styleWriter, valueToPercent } from './dom';
import type { StyleLayer } from './dom';
import { isTouchLikePointerType } from './button';
import { decide } from './decide';
import { eventInfo } from './event-info';
import type { EventInfo } from './event-info';
import { matchesFocusVisible } from './interactions';
import { createFrameSlot } from './transitions';
import { attachTarget, createTwinChannel, parseOptions, resolveReference } from './twin';
import type { AttachTarget, DotNetTwin, TwinCaptures, TwinHandle, TwinOptions, TwinReferences } from './twin';

// ---------------------------------------------------------------------------------------------------------------
// The value algorithms (SLD-STA-13..19), as `slider/utils/*.ts` and `SliderThumb.tsx:63-81` write them
// ---------------------------------------------------------------------------------------------------------------

/** The collision behavior of a dragged range thumb; mirrors `SliderThumbCollisionBehavior` in C#. */
export type ThumbCollisionBehavior = 'push' | 'swap' | 'none';

/** The result of `resolveThumbCollision`: the candidate, the thumb it addresses and whether the pressed thumb swapped. */
export interface ResolveThumbCollisionResult {
  value: number | number[];
  thumbIndex: number;
  didSwap: boolean;
}

/** Ascending numeric order, for `Array.prototype.sort`. */
export function asc(a: number, b: number): number {
  return a - b;
}

// SLD-STA-13 (`slider/utils/roundValueToStep.ts:1-16`).
/**
 * Counts the decimal digits of a number, reading an exponential form for magnitudes below one so `1e-8` keeps its digits.
 * @param num The number.
 * @returns The number of fraction digits.
 */
export function getDecimalPrecision(num: number): number {
  if (num === 0) {
    return 0;
  }
  if (Math.abs(num) < 1) {
    const parts = num.toExponential().split('e-');
    const mantissaDecimalPart = parts[0]!.split('.')[1];
    return (mantissaDecimalPart ? mantissaDecimalPart.length : 0) + parseInt(parts[1]!, 10);
  }
  const decimalPart = num.toString().split('.')[1];
  return decimalPart ? decimalPart.length : 0;
}

// SLD-STA-14 (`slider/utils/roundValueToStep.ts:18-21`).
/**
 * Snaps a value to the step grid anchored at `min`, at the combined decimal precision of `step` and `min`. Not clamped.
 * @param value The value.
 * @param step The grid's step.
 * @param min The grid's origin.
 * @returns The nearest grid value.
 */
export function roundValueToStep(value: number, step: number, min: number): number {
  const nearest = Math.round((value - min) / step) * step + min;
  return Number(nearest.toFixed(Math.max(getDecimalPrecision(step), getDecimalPrecision(min))));
}

// SLD-STA-15 (`slider/thumb/SliderThumb.tsx:63-81`).
/**
 * The keyboard step: adds `increment × direction` at the combined decimal precision, then clamps to the bounds.
 * @param thumbValue The thumb's value, already rounded to the step grid.
 * @param increment The step or the large step.
 * @param direction `1` or `-1`.
 * @param min The lower bound.
 * @param max The upper bound.
 * @returns The candidate value.
 */
export function getNewValue(thumbValue: number, increment: number, direction: number, min: number, max: number): number {
  const value = thumbValue + increment * direction;
  const roundedValue = Number(
    value.toFixed(Math.max(getDecimalPrecision(thumbValue), getDecimalPrecision(increment), getDecimalPrecision(min))),
  );
  return clamp(roundedValue, min, max);
}

// SLD-STA-16 (`slider/utils/getSliderValue.ts:4-22`).
/**
 * The keyboard and native-input candidate: the value clamped to the bounds and, in range mode, to the thumb's neighbors.
 * @param valueInput The requested value.
 * @param index The thumb's index.
 * @param min The lower bound.
 * @param max The upper bound.
 * @param range Whether the slider is in range mode.
 * @param values The normalized values.
 * @returns A number in single mode, a new sorted array in range mode.
 */
export function getSliderValue(
  valueInput: number,
  index: number,
  min: number,
  max: number,
  range: boolean,
  values: readonly number[],
): number | number[] {
  const clamped = clamp(valueInput, min, max);
  if (!range) {
    return clamped;
  }
  const output = values.slice();
  output[index] = clamp(clamped, values[index - 1] ?? -Infinity, values[index + 1] ?? Infinity);
  return output.sort(asc);
}

// SLD-STA-17 (`slider/utils/validateMinimumDistance.ts:1-17`).
/**
 * Whether every adjacent pair of a range candidate is at least `step × minStepsBetweenValues` apart; a number passes.
 * @param values The candidate.
 * @param step The step.
 * @param minStepsBetweenValues The minimum number of steps between adjacent values.
 * @returns `true` when the candidate may be applied.
 */
export function validateMinimumDistance(values: number | readonly number[], step: number, minStepsBetweenValues: number): boolean {
  if (!Array.isArray(values)) {
    return true;
  }
  const minDistance = step * minStepsBetweenValues;
  for (let i = 0; i < values.length - 1; i += 1) {
    if (!(Math.abs(values[i]! - values[i + 1]!) >= minDistance)) {
      return false;
    }
  }
  return true;
}

// SLD-STA-18 (`slider/utils/getPushedThumbValues.ts:10-60`).
/**
 * Moves the thumb at `index` and pushes its neighbors to keep the minimum distance, letting a pushed thumb return toward
 * its value at the start of the gesture.
 * @param values The values the move starts from.
 * @param index The moved thumb.
 * @param nextValue The requested value.
 * @param min The lower bound.
 * @param max The upper bound.
 * @param step The step.
 * @param minStepsBetweenValues The minimum number of steps between adjacent values.
 * @param initialValues The values at the start of the gesture; `values` when absent.
 * @returns A new array.
 */
export function getPushedThumbValues(
  values: readonly number[],
  index: number,
  nextValue: number,
  min: number,
  max: number,
  step: number,
  minStepsBetweenValues: number,
  initialValues?: readonly number[],
): number[] {
  const nextValues = values.slice();
  const minValueDifference = step * minStepsBetweenValues;
  const lastIndex = nextValues.length - 1;
  const baseInitialValues = initialValues ?? values;

  const indexMin = min + index * minValueDifference;
  const indexMax = max - (lastIndex - index) * minValueDifference;
  nextValues[index] = clamp(nextValue, indexMin, indexMax);

  for (let i = index + 1; i <= lastIndex; i += 1) {
    const minAllowed = nextValues[i - 1]! + minValueDifference;
    const maxAllowed = max - (lastIndex - i) * minValueDifference;
    const initialValue = baseInitialValues[i]!;
    let candidate = Math.max(nextValues[i]!, minAllowed);
    if (initialValue < candidate) {
      candidate = Math.max(initialValue, minAllowed);
    }
    nextValues[i] = clamp(candidate, minAllowed, maxAllowed);
  }

  for (let i = index - 1; i >= 0; i -= 1) {
    const maxAllowed = nextValues[i + 1]! - minValueDifference;
    const minAllowed = min + i * minValueDifference;
    const initialValue = baseInitialValues[i]!;
    let candidate = Math.min(nextValues[i]!, maxAllowed);
    if (initialValue > candidate) {
      candidate = Math.min(initialValue, maxAllowed);
    }
    nextValues[i] = clamp(candidate, minAllowed, maxAllowed);
  }

  for (let i = 0; i <= lastIndex; i += 1) {
    nextValues[i] = Number(nextValues[i]!.toFixed(12));
  }
  return nextValues;
}

// SLD-STA-19 (`slider/utils/resolveThumbCollision.ts:15-154`).
/**
 * Resolves a dragged range thumb against its neighbors under the collision behavior.
 * @param behavior `push`, `swap` or `none`.
 * @param values The values when the move was computed.
 * @param currentValues The values of the gesture's last applied change; `values` when absent.
 * @param initialValues The values at the start of the gesture; `values` when absent.
 * @param pressedIndex The pressed thumb.
 * @param nextValue The requested value.
 * @param min The lower bound.
 * @param max The upper bound.
 * @param step The step.
 * @param minStepsBetweenValues The minimum number of steps between adjacent values.
 * @returns The candidate, the thumb it addresses and whether the pressed thumb swapped.
 */
export function resolveThumbCollision(
  behavior: ThumbCollisionBehavior,
  values: readonly number[],
  currentValues: readonly number[] | null | undefined,
  initialValues: readonly number[] | null | undefined,
  pressedIndex: number,
  nextValue: number,
  min: number,
  max: number,
  step: number,
  minStepsBetweenValues: number,
): ResolveThumbCollisionResult {
  const activeValues = currentValues ?? values;
  const baselineValues = initialValues ?? values;
  if (activeValues.length <= 1) {
    return { value: nextValue, thumbIndex: 0, didSwap: false };
  }

  const minValueDifference = step * minStepsBetweenValues;
  if (behavior === 'push') {
    return {
      value: getPushedThumbValues(activeValues, pressedIndex, nextValue, min, max, step, minStepsBetweenValues),
      thumbIndex: pressedIndex,
      didSwap: false,
    };
  }

  const candidateValues = activeValues.slice();
  const previousNeighbor = candidateValues[pressedIndex - 1];
  const nextNeighbor = candidateValues[pressedIndex + 1];
  const lowerBound = previousNeighbor != null ? previousNeighbor + minValueDifference : min;
  const upperBound = nextNeighbor != null ? nextNeighbor - minValueDifference : max;
  const pressedValueAfterClamp = Number(clamp(nextValue, lowerBound, upperBound).toFixed(12));
  candidateValues[pressedIndex] = pressedValueAfterClamp;

  if (behavior !== 'swap') {
    return { value: candidateValues, thumbIndex: pressedIndex, didSwap: false };
  }

  const pressedInitialValue = activeValues[pressedIndex]!;
  const epsilon = 1e-7;
  const shouldSwapForward = nextValue > pressedInitialValue && nextNeighbor != null && nextValue >= nextNeighbor - epsilon;
  const shouldSwapBackward = nextValue < pressedInitialValue && previousNeighbor != null && nextValue <= previousNeighbor + epsilon;
  if (!shouldSwapForward && !shouldSwapBackward) {
    return { value: candidateValues, thumbIndex: pressedIndex, didSwap: false };
  }

  const targetIndex = shouldSwapForward ? pressedIndex + 1 : pressedIndex - 1;
  const initialValuesForPush = candidateValues.map((_, index) => {
    if (index === pressedIndex) {
      return pressedValueAfterClamp;
    }
    const baseline = baselineValues[index];
    return baseline != null ? baseline : activeValues[index]!;
  });
  const nextValueForTarget = shouldSwapForward
    ? Math.max(nextValue, candidateValues[targetIndex]!)
    : Math.min(nextValue, candidateValues[targetIndex]!);
  const adjustedValues = getPushedThumbValues(
    candidateValues,
    targetIndex,
    nextValueForTarget,
    min,
    max,
    step,
    minStepsBetweenValues,
    initialValuesForPush,
  );

  const neighborIndex = shouldSwapForward ? targetIndex - 1 : targetIndex + 1;
  const previousValue = adjustedValues[neighborIndex - 1];
  const nextValueAfter = adjustedValues[neighborIndex + 1];
  let neighborLowerBound = previousValue != null ? previousValue + minValueDifference : min;
  neighborLowerBound = Math.max(neighborLowerBound, min + neighborIndex * minValueDifference);
  let neighborUpperBound = nextValueAfter != null ? nextValueAfter - minValueDifference : max;
  neighborUpperBound = Math.min(neighborUpperBound, max - (adjustedValues.length - 1 - neighborIndex) * minValueDifference);
  adjustedValues[neighborIndex] = Number(clamp(pressedValueAfterClamp, neighborLowerBound, neighborUpperBound).toFixed(12));

  return { value: adjustedValues, thumbIndex: targetIndex, didSwap: true };
}

// ---------------------------------------------------------------------------------------------------------------
// The handle (ADR 04 §1-§3; Amendments 2026-10-18, 2026-10-20 and 2026-11-18)
// ---------------------------------------------------------------------------------------------------------------

/** The options of one thumb; mirrors `SliderThumbOptions` in C#. */
export interface SliderThumbOptions {
  inputId?: string | null;
  disabled: boolean;
  style?: string | null;
}

/** The options of one indicator; mirrors `SliderIndicatorOptions` in C#. */
export interface SliderIndicatorOptions {
  ref: number;
  style?: string | null;
}

/** The options of the `slider` module; mirrors `SliderOptions` in C#. */
export interface SliderOptions extends TwinOptions {
  values: number[];
  range: boolean;
  min: number;
  max: number;
  step: number;
  largeStep: number;
  minStepsBetweenValues: number;
  vertical: boolean;
  rtl: boolean;
  disabled: boolean;
  inset: boolean;
  collision: ThumbCollisionBehavior;
  thumbs: SliderThumbOptions[];
  controlRef?: number | null;
  indicators: SliderIndicatorOptions[];
  revision: number;
  rollback: boolean;
  keyHandlers: boolean;
  pressHandler: boolean;
  rawDiffers?: boolean;
}

/** The decision point asked before a key on a thumb input is handled (ADR 04 §3: fallback `run`). */
export const KEY_POINT = 'key';

/** The decision point asked before a press on the control starts a gesture (ADR 04 §3: fallback `start`). */
export const PRESS_POINT = 'press';

/** The literal of a value request's reason. */
export type SliderReason = 'input-change' | 'keyboard' | 'track-press' | 'drag';

// SliderControl.tsx:29: a touch gesture shows as a drag from its third move.
const INTENTIONAL_DRAG_COUNT_THRESHOLD = 2;

// SliderThumb.tsx:44 (COMPOSITE_KEYS plus the Page keys); internals/composite/composite.ts.
const COMPOSITE_KEYS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End']);
const ALL_KEYS = new Set([...COMPOSITE_KEYS, 'PageUp', 'PageDown']);

// UTL-DOM-13 (packages/utils/src/visuallyHidden.ts): a thumb whose percent is not finite.
const VISUALLY_HIDDEN: ReadonlyArray<readonly [string, string]> = [
  ['clip-path', 'inset(50%)'],
  ['overflow', 'hidden'],
  ['white-space', 'nowrap'],
  ['border', '0'],
  ['padding', '0'],
  ['width', '1px'],
  ['height', '1px'],
  ['margin', '0'],
  ['position', 'fixed'],
  ['top', '0'],
  ['left', '0'],
];

interface Coords {
  x: number;
  y: number;
}

interface FingerState {
  value: number | number[];
  thumbIndex: number;
  didSwap: boolean;
}

/** One element whose style the module owns: its writer's `component-under` layer and the properties written last. */
interface OwnedStyle {
  layer: StyleLayer;
  written: Map<string, string>;
}

const attachedHandle = Symbol('blazix.slider.handle');

interface SliderRootElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

// slider/utils/getMidpoint.ts.
function getMidpoint(element: Element, vertical: boolean): number {
  const rect = element.getBoundingClientRect();
  return vertical ? (rect.top + rect.bottom) / 2 : (rect.left + rect.right) / 2;
}

// SliderControl.tsx:31-51: the control's border and padding on each side of the axis.
function getControlOffset(styles: CSSStyleDeclaration | null, vertical: boolean): { start: number; end: number } {
  if (!styles) {
    return { start: 0, end: 0 };
  }
  const parseSize = (value: string | null | undefined) => {
    const parsed = value != null ? parseFloat(value) : 0;
    return Number.isNaN(parsed) ? 0 : parsed;
  };
  const start = !vertical ? 'InlineStart' : 'Top';
  const end = !vertical ? 'InlineEnd' : 'Bottom';
  const read = (name: string) => (styles as unknown as Record<string, string>)[name];
  return {
    start: parseSize(read(`border${start}Width`)) + parseSize(read(`padding${start}`)),
    end: parseSize(read(`border${end}Width`)) + parseSize(read(`padding${end}`)),
  };
}

function sameValues(a: readonly number[], b: readonly number[]): boolean {
  if (a.length !== b.length) {
    return false;
  }
  for (let i = 0; i < a.length; i += 1) {
    if (!Object.is(a[i], b[i])) {
      return false;
    }
  }
  return true;
}

/**
 * Attaches the `slider` module to a slider's root element.
 * @param elementOrId The root element, or its id under a substitute callback.
 * @param optionsJson The JSON of a {@link SliderOptions}.
 * @param dotNetRef The twin's reference.
 * @param references The control and the indicators, which the options name by index.
 * @param captures The capture ids of the elements.
 * @returns The handle.
 */
export function attach(
  elementOrId: AttachTarget,
  optionsJson: string,
  dotNetRef: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const root = attachTarget(elementOrId, captures) as SliderRootElement;
  const existing = root[attachedHandle];
  if (existing) {
    return existing;
  }

  const twin = createTwinChannel(dotNetRef);
  let options = parseOptions<SliderOptions>(optionsJson);
  let currentReferences = references;
  let currentCaptures = captures;
  let disposed = false;

  // The module's values: C#'s after every update it applies, the candidate after every act (the optimistic move).
  let values: number[] = options.values.slice();
  // ADR 04 Amendments 2026-11-18 (the revision rule): the revision of the module's last act.
  let actRevision = options.revision;
  // The pressed index before each swap the module applied optimistically, by the act's revision, for a rollback.
  const swaps = new Map<number, number>();

  let active = -1;
  let lastUsed = -1;
  let restoringFocusVisible = false;

  let control: HTMLElement | null = null;
  let controlStyles: CSSStyleDeclaration | null = null;
  let thumbElements: (HTMLElement | null)[] = [];
  let inputs: (HTMLInputElement | null)[] = [];
  let indicatorElements: HTMLElement[] = [];
  const owned = new Map<HTMLElement, OwnedStyle>();

  // The inset positions (SLD-ANI-01): undefined until computed.
  let positions: (number | undefined)[] = [];

  // The gesture (SliderControl.tsx:121-140, SliderRoot.tsx:126-135).
  let pressedThumbIndex = -1;
  let pressedThumbCenterOffset: number | null = null;
  let pressedValues: number[] | null = null;
  let latestValues: number[] = values;
  let touchId: number | null = null;
  let moveCount = 0;
  let insetThumbOffset = 0;
  let pointerGesture = false;
  let listening = false;
  let dragging = false;
  const focusFrame = createFrameSlot();

  let unobserveResize: (() => void)[] = [];

  // -------------------------------------------------------------------------------------------------------------
  // Elements and styles
  // -------------------------------------------------------------------------------------------------------------

  function rootNode(): Document | ShadowRoot {
    const node = root.getRootNode();
    return node instanceof ShadowRoot ? node : ownerDocument(root);
  }

  function resolve(): void {
    const nextControl = resolveReference<HTMLElement>(currentReferences, options.controlRef, currentCaptures);
    if (nextControl !== control) {
      control?.removeEventListener('pointerdown', onPointerDown);
      control?.removeEventListener('touchstart', onTouchStart);
      control = nextControl;
      controlStyles = control ? ownerWindow(control).getComputedStyle(control) : null;
      control?.addEventListener('pointerdown', onPointerDown);
      control?.addEventListener('touchstart', onTouchStart, { passive: true });
    }

    const scope = rootNode();
    inputs = options.thumbs.map((thumb) => {
      if (!thumb.inputId) {
        return null;
      }
      const found = scope.getElementById(thumb.inputId);
      return found instanceof HTMLInputElement ? found : null;
    });
    thumbElements = inputs.map((input) => input?.parentElement ?? null);

    const nextIndicators: HTMLElement[] = [];
    for (const indicator of options.indicators) {
      const element = resolveReference<HTMLElement>(currentReferences, indicator.ref, currentCaptures);
      if (element) {
        nextIndicators.push(element);
      }
    }
    indicatorElements = nextIndicators;

    // Elements no longer owned give their style back to nothing: their part left, or a render replaced them.
    const live = new Set<HTMLElement>([...thumbElements.filter((e): e is HTMLElement => e !== null), ...indicatorElements]);
    for (const [element, style] of owned) {
      if (!live.has(element)) {
        style.layer.clear();
        owned.delete(element);
      }
    }

    observeInset();
  }

  // ADR 04 Amendments 2026-10-18 (the handover) and 2026-11-18 (the bands): from the first write the element's whole style
  // is the writer's, the consumer's string with this module's declarations in `component-under`, below it.
  function own(element: HTMLElement, consumer: string | null | undefined): OwnedStyle {
    let style = owned.get(element);
    if (!style) {
      element.style.cssText = '';
      const writer = styleWriter(element);
      style = { layer: writer.layer('component-under'), written: new Map() };
      owned.set(element, style);
    }
    styleWriter(element).setConsumer(consumer ?? null);
    return style;
  }

  function write(style: OwnedStyle, declarations: ReadonlyArray<readonly [string, string]>): void {
    const next = new Map<string, string>(declarations as Array<[string, string]>);
    for (const property of style.written.keys()) {
      if (!next.has(property)) {
        style.layer.remove(property);
      }
    }
    for (const [property, value] of next) {
      if (style.written.get(property) !== value) {
        style.layer.set(property, value);
      }
    }
    style.written = next;
  }

  function percentOf(index: number): number {
    return valueToPercent(values[index]!, options.min, options.max);
  }

  // SliderThumb.tsx:276-304 (SLD-DOM-09..12).
  function thumbDeclarations(index: number): Array<readonly [string, string]> {
    const vertical = options.vertical;
    const percent = index < values.length ? percentOf(index) : Number.NaN;
    if (!options.inset && !Number.isFinite(percent)) {
      return [...VISUALLY_HIDDEN];
    }
    const declarations: Array<readonly [string, string]> = [
      ['position', 'absolute'],
      [vertical ? 'bottom' : 'inset-inline-start', options.inset ? 'var(--position)' : `${percent}%`],
      [vertical ? 'left' : 'top', '50%'],
      ['translate', `${(vertical || !options.rtl ? -1 : 1) * 50}% ${(vertical ? 1 : -1) * 50}%`],
    ];
    const range = values.length > 1;
    const safeLastUsed = lastUsed >= 0 && lastUsed < values.length ? lastUsed : -1;
    let zIndex: number | undefined;
    if (range) {
      if (active === index) {
        zIndex = 2;
      } else if (safeLastUsed === index) {
        zIndex = 1;
      }
    } else if (active === index) {
      zIndex = 1;
    }
    if (zIndex !== undefined) {
      declarations.push(['z-index', String(zIndex)]);
    }
    if (options.inset) {
      const position = positions[index];
      declarations.push(['--position', `${position ?? 0}%`]);
      if (position === undefined) {
        declarations.push(['visibility', 'hidden']);
      }
    }
    return declarations;
  }

  // SliderIndicator.tsx:11-48 (SLD-DOM-05/06).
  function indicatorDeclarations(): Array<readonly [string, string]> {
    const vertical = options.vertical;
    const range = values.length > 1;
    const inset = options.inset;
    const start = inset ? positions[0] : percentOf(0);
    const end = inset ? positions[values.length - 1] : percentOf(values.length - 1);
    const declarations: Array<readonly [string, string]> = [];
    if (inset && (start === undefined || (range && end === undefined))) {
      declarations.push(['visibility', 'hidden']);
    }
    declarations.push(['position', vertical ? 'absolute' : 'relative']);
    declarations.push([vertical ? 'width' : 'height', 'inherit']);
    let startValue = `${start ?? 0}%`;
    let sizeValue = `${(end ?? 0) - (start ?? 0)}%`;
    if (inset) {
      declarations.push(['--start-position', startValue]);
      startValue = 'var(--start-position)';
      if (range) {
        declarations.push(['--relative-size', sizeValue]);
        sizeValue = 'var(--relative-size)';
      }
    }
    declarations.push([vertical ? 'bottom' : 'inset-inline-start', range ? startValue : '0']);
    declarations.push([vertical ? 'height' : 'width', range ? sizeValue : startValue]);
    return declarations;
  }

  // SLD-DOM-16 (decision 14 (a)): the module writes each input's value, so a refused change and a native reset are repaired.
  function writeInputs(): void {
    inputs.forEach((input, index) => {
      if (!input) {
        return;
      }
      const text = index < values.length ? String(values[index]) : '';
      if (input.value !== text) {
        input.value = text;
      }
    });
  }

  function render(): void {
    thumbElements.forEach((thumb, index) => {
      if (thumb) {
        write(own(thumb, options.thumbs[index]?.style), thumbDeclarations(index));
      }
    });
    const indicatorStyle = indicatorDeclarations();
    indicatorElements.forEach((indicator, i) => {
      write(own(indicator, options.indicators[i]?.style), indicatorStyle);
    });
    writeInputs();
  }

  // SliderThumb.tsx:204-232 (SLD-ANI-01).
  function computeInsetPositions(): void {
    if (!options.inset) {
      positions = [];
      return;
    }
    const next: (number | undefined)[] = [];
    thumbElements.forEach((thumb, index) => {
      if (!control || !thumb || index >= values.length) {
        next[index] = undefined;
        return;
      }
      const side = options.vertical ? 'height' : 'width';
      const thumbRect = thumb.getBoundingClientRect();
      const controlRect = control.getBoundingClientRect();
      const controlSize = controlRect[side] - thumbRect[side];
      const offset = thumbRect[side] / 2 + (controlSize * percentOf(index)) / 100;
      const percent = (offset / controlRect[side]) * 100;
      next[index] = Number.isFinite(percent) ? percent : undefined;
    });
    positions = next;
  }

  function refresh(): void {
    computeInsetPositions();
    render();
  }

  // SLD-ANI-01: a ResizeObserver over the control and each thumb, so a slider revealed from `display: none` positions itself.
  function observeInset(): void {
    for (const stop of unobserveResize) {
      stop();
    }
    unobserveResize = [];
    if (!options.inset) {
      return;
    }
    const observe = (element: Element | null) => {
      if (element) {
        unobserveResize.push(observeResize(element, () => refresh()));
      }
    };
    observe(control);
    thumbElements.forEach(observe);
  }

  // -------------------------------------------------------------------------------------------------------------
  // Requests
  // -------------------------------------------------------------------------------------------------------------

  function snapshot(event: Event | undefined): EventInfo {
    return eventInfo(event);
  }

  // The optimistic move (ADR 04 Amendments 2026-11-18): the thumbs move and the inputs show the candidate at once; C#
  // answers with an update carrying this act's revision, or a rollback.
  function act(next: number[], reason: SliderReason, thumbIndex: number, event: Event | undefined): number {
    actRevision += 1;
    values = next;
    refresh();
    // SLD-EVT-04: the request's target is redefined as `{ value, name }`, the candidate in the value's shape.
    const name = inputs.find((input) => input !== null)?.name || null;
    const value = JSON.stringify(options.range ? next : next[0]);
    twin.report('OnValueRequest', {
      reason,
      activeThumbIndex: thumbIndex,
      values: next,
      event: eventInfo(event, { id: null, tagName: null, value, name }),
      revision: actRevision,
    });
    return actRevision;
  }

  function differs(next: number[]): boolean {
    return options.rawDiffers === true || !sameValues(next, values);
  }

  // SliderRoot.tsx:228-247 (SLD-STA-16/17, SLD-KEY-07, SLD-EVT-02): a keyboard or native-input candidate.
  function handleInputChange(valueInput: number, index: number, reason: 'keyboard' | 'input-change', event: Event): void {
    const candidate = getSliderValue(valueInput, index, options.min, options.max, options.range, values);
    if (!validateMinimumDistance(candidate, options.step, options.minStepsBetweenValues)) {
      return;
    }
    const next = Array.isArray(candidate) ? candidate : [candidate];
    if (!Array.isArray(candidate) && Number.isNaN(candidate)) {
      return;
    }
    // SLD-FRM-05 (a): an equal candidate is still reported, so C# marks the field touched; C# drops it (SLD-STA-09).
    act(next, reason, index, event);
  }

  // -------------------------------------------------------------------------------------------------------------
  // Keys and native input (SLD §6.1, SLD-EVT-02)
  // -------------------------------------------------------------------------------------------------------------

  function indexOfInput(target: EventTarget | null): number {
    return target instanceof HTMLInputElement ? inputs.indexOf(target) : -1;
  }

  function onKeyDown(event: KeyboardEvent): void {
    const index = indexOfInput(getTarget(event));
    if (index < 0) {
      return;
    }
    const input = inputs[index]!;

    // SLD-API-22, SLD-EVT-09: the consumer's C# handler first, inside the dispatch on WebAssembly.
    if (options.keyHandlers) {
      const verdict = decide(twin, KEY_POINT, event, 'run');
      if (!twin.lastDecisionWasSynchronous) {
        twin.report('OnKeyDown', { index, event: snapshot(event) });
      }
      if (verdict === 'stop') {
        return;
      }
    }

    // SLD-KEY-01, SLD-KEY-09.
    if (event.defaultPrevented || !ALL_KEYS.has(event.key) || options.thumbs[index]?.disabled) {
      return;
    }

    // SLD-KEY-08.
    if (COMPOSITE_KEYS.has(event.key)) {
      event.stopPropagation();
    }

    const range = values.length > 1;
    const thumbValue = values[index]!;
    let newValue: number | null = null;
    let direction = 0;
    let increment = event.shiftKey ? options.largeStep : options.step;
    const roundedValue = roundValueToStep(thumbValue, options.step, options.min);
    switch (event.key) {
      case 'ArrowUp':
        direction = 1;
        break;
      case 'ArrowRight':
        direction = options.rtl ? -1 : 1;
        break;
      case 'ArrowDown':
        direction = -1;
        break;
      case 'ArrowLeft':
        direction = options.rtl ? 1 : -1;
        break;
      case 'PageUp':
        increment = options.largeStep;
        direction = 1;
        break;
      case 'PageDown':
        increment = options.largeStep;
        direction = -1;
        break;
      case 'End':
        newValue =
          range && Number.isFinite(values[index + 1])
            ? values[index + 1]! - options.step * options.minStepsBetweenValues
            : options.max;
        break;
      case 'Home':
        newValue =
          range && Number.isFinite(values[index - 1])
            ? values[index - 1]! + options.step * options.minStepsBetweenValues
            : options.min;
        break;
      default:
        break;
    }
    if (direction !== 0) {
      newValue = getNewValue(roundedValue, increment, direction, options.min, options.max);
    }
    if (newValue === null) {
      return;
    }

    // SLD-KEY-11: a pointer-focused input is blurred and focused again, so `:focus-visible` shows; the pair is not reported.
    if (!matchesFocusVisible(input)) {
      restoringFocusVisible = true;
      input.blur();
      input.focus({ preventScroll: true, focusVisible: true } as FocusOptions);
      restoringFocusVisible = false;
    }

    handleInputChange(newValue, index, 'keyboard', event);
    event.preventDefault();
  }

  // SLD-EVT-02: a change of the native input by something other than the keys (assistive technology, scripts).
  function onInput(event: Event): void {
    const index = indexOfInput(getTarget(event));
    if (index < 0) {
      return;
    }
    const input = inputs[index]!;
    const valueAsNumber = input.valueAsNumber;
    if (event.type === 'change' && Object.is(valueAsNumber, values[index])) {
      return;
    }
    handleInputChange(valueAsNumber, index, 'input-change', event);
  }

  // SLD-FOC-03/04, SLD-STA-06/07; ADR 04 Amendments 2026-10-20: each blur classified once by its related target.
  function onFocusIn(event: FocusEvent): void {
    const index = indexOfInput(getTarget(event));
    if (index < 0) {
      return;
    }
    if (restoringFocusVisible) {
      event.stopPropagation();
      return;
    }
    active = index;
    lastUsed = index;
    render();
    twin.report('OnFocusChange', { index, focused: true, outside: false, event: snapshot(event) });
  }

  function onFocusOut(event: FocusEvent): void {
    const index = indexOfInput(getTarget(event));
    if (index < 0) {
      return;
    }
    if (restoringFocusVisible) {
      event.stopPropagation();
      return;
    }
    active = -1;
    render();
    const related = event.relatedTarget instanceof Element ? event.relatedTarget : null;
    const outside = !thumbElements.some((thumb) => thumb !== null && contains(thumb, related));
    twin.report('OnFocusChange', { index, focused: false, outside, event: snapshot(event) });
  }

  // -------------------------------------------------------------------------------------------------------------
  // Pointer and touch (SLD §6.2; slider/control/SliderControl.tsx)
  // -------------------------------------------------------------------------------------------------------------

  function getFingerCoords(event: TouchEvent | PointerEvent): Coords | null {
    if (touchId != null && (event as TouchEvent).changedTouches) {
      const touches = (event as TouchEvent).changedTouches;
      for (let i = 0; i < touches.length; i += 1) {
        const touch = touches[i]!;
        if (touch.identifier === touchId) {
          return { x: touch.clientX, y: touch.clientY };
        }
      }
      return null;
    }
    return { x: (event as PointerEvent).clientX, y: (event as PointerEvent).clientY };
  }

  function thumbInput(index: number): HTMLInputElement | null {
    return inputs[index] ?? null;
  }

  function updatePressedThumb(nextIndex: number): void {
    pressedThumbIndex = nextIndex;
    if (!thumbElements[nextIndex]) {
      pressedThumbCenterOffset = null;
    }
  }

  function resetPressedThumb(): void {
    pressedThumbIndex = -1;
    pressedThumbCenterOffset = null;
  }

  function thumbContaining(target: EventTarget | null): number {
    if (!isElement(target)) {
      return -1;
    }
    return thumbElements.findIndex((thumb) => thumb !== null && contains(thumb, target));
  }

  function isTargetDisabledThumb(target: EventTarget | null): boolean {
    const index = thumbContaining(target);
    return index >= 0 && thumbInput(index)?.disabled === true;
  }

  // SliderControl.tsx:173-224 (SLD-PTR-04).
  function getFingerState(coords: Coords): FingerState | null {
    const thumbIndex = pressedThumbIndex;
    if (!control || thumbIndex < 0 || thumbIndex >= values.length) {
      return null;
    }
    const vertical = options.vertical;
    const { width, height, bottom, left, right } = control.getBoundingClientRect();
    const controlOffset = getControlOffset(controlStyles, vertical);
    const controlSize = (vertical ? height : width) - controlOffset.start - controlOffset.end - insetThumbOffset * 2;
    const thumbCenterOffset = pressedThumbCenterOffset ?? 0;
    const fingerX = coords.x - thumbCenterOffset;
    const fingerY = coords.y - thumbCenterOffset;
    const valueSize = vertical
      ? bottom - fingerY - controlOffset.end
      : (options.rtl ? right - fingerX : fingerX - left) - controlOffset.start;
    const valueRescaled = clamp((valueSize - insetThumbOffset) / controlSize, 0, 1);
    let newValue = (options.max - options.min) * valueRescaled + options.min;
    newValue = roundValueToStep(newValue, options.step, options.min);
    newValue = clamp(newValue, options.min, options.max);
    if (values.length <= 1) {
      return { value: options.range ? [newValue] : newValue, thumbIndex, didSwap: false };
    }
    return resolveThumbCollision(
      options.collision,
      values,
      latestValues,
      pressedValues,
      thumbIndex,
      newValue,
      options.min,
      options.max,
      options.step,
      options.minStepsBetweenValues,
    );
  }

  // SliderControl.tsx:226-276 (SLD-PTR-03, SLD-PTR-18, SLD-PTR-24).
  function startPressing(coords: Coords): void {
    pressedValues = values.length > 1 ? values.slice() : null;
    latestValues = values;
    const pressed = pressedThumbIndex;
    let closest = pressed;
    if (pressed > -1 && pressed < values.length) {
      if (values[pressed] === options.max) {
        let candidate = pressed;
        while (candidate > 0 && values[candidate - 1] === options.max) {
          candidate -= 1;
        }
        closest = candidate;
      }
    } else {
      const axis = !options.vertical ? 'x' : 'y';
      let minDistance: number | undefined;
      closest = -1;
      for (let i = 0; i < thumbElements.length; i += 1) {
        const thumb = thumbElements[i];
        if (thumb && !thumbInput(i)?.disabled) {
          const distance = Math.abs(coords[axis] - getMidpoint(thumb, options.vertical));
          if (minDistance === undefined || distance <= minDistance) {
            closest = i;
            minDistance = distance;
          }
        }
      }
    }
    if (closest > -1 && closest !== pressed) {
      updatePressedThumb(closest);
    }
    insetThumbOffset = 0;
    if (options.inset) {
      const thumb = thumbElements[closest];
      if (thumb) {
        insetThumbOffset = thumb.getBoundingClientRect()[!options.vertical ? 'width' : 'height'] / 2;
      }
    }
  }

  function focusThumb(index: number): void {
    thumbInput(index)?.focus({ preventScroll: true, focusVisible: false } as FocusOptions);
  }

  function setDragging(next: boolean): void {
    if (dragging !== next) {
      dragging = next;
      twin.report('OnGesture', { active: true, dragging: next, commit: false });
    }
  }

  // SliderControl.tsx:293-318 (SLD-PTR-05, SLD-PTR-10).
  function setValueFromPointer(finger: FingerState, reason: 'track-press' | 'drag', event: Event): void {
    const next = Array.isArray(finger.value) ? finger.value : [finger.value];
    if (!differs(next)) {
      return;
    }
    // The swap is recorded before the report: on WebAssembly C# may answer, a rollback included, inside it.
    const before = pressedThumbIndex;
    latestValues = next;
    if (finger.didSwap) {
      swaps.set(actRevision + 1, before);
      updatePressedThumb(finger.thumbIndex);
      focusThumb(finger.thumbIndex);
    }
    act(next, reason, finger.thumbIndex, event);
  }

  function onTouchMove(event: TouchEvent | PointerEvent): void {
    const coords = getFingerCoords(event);
    if (coords == null) {
      return;
    }
    moveCount += 1;
    // SLD-PTR-09: a pointermove with no button down is a release whose pointerup was lost.
    if (event.type === 'pointermove' && (event as PointerEvent).buttons === 0) {
      onTouchEnd(event);
      return;
    }
    const finger = getFingerState(coords);
    if (finger == null) {
      return;
    }
    if (validateMinimumDistance(finger.value, options.step, options.minStepsBetweenValues)) {
      if (!dragging && moveCount > INTENTIONAL_DRAG_COUNT_THRESHOLD) {
        setDragging(true);
      }
      setValueFromPointer(finger, 'drag', event);
    }
  }

  // SliderControl.tsx:351-387 (SLD-PTR-11): C# commits the last value it applied (SLD-EVT-12).
  function onTouchEnd(event: TouchEvent | PointerEvent): void {
    if (getFingerCoords(event) == null) {
      return;
    }
    active = -1;
    dragging = false;
    pressedThumbCenterOffset = null;
    twin.report('OnGesture', { active: false, dragging: false, commit: true, event: snapshot(event) });
    if ('pointerType' in event && control?.hasPointerCapture((event as PointerEvent).pointerId)) {
      control.releasePointerCapture((event as PointerEvent).pointerId);
    }
    pressedThumbIndex = -1;
    stopListening();
    render();
  }

  function onPointerCancel(event: PointerEvent): void {
    // SLD-PTR-13: the browser cancels a touch pointer once it pans; the touch listeners keep tracking the finger.
    if (touchId != null) {
      return;
    }
    onTouchEnd(event);
  }

  // SliderControl.tsx:399-443 (SLD-PTR-12).
  function onTouchStart(event: TouchEvent): void {
    const startedByPointer = pointerGesture;
    pointerGesture = false;
    if (options.disabled) {
      return;
    }
    const target = getTarget(event);
    if (isTargetDisabledThumb(target)) {
      resetPressedThumb();
      return;
    }
    const touch = event.changedTouches[0];
    if (touch == null) {
      return;
    }
    touchId = touch.identifier;
    if (!startedByPointer) {
      const coords = { x: touch.clientX, y: touch.clientY };
      recordThumbPress(target, coords);
      startPressing(coords);
      const finger = getFingerState(coords);
      if (finger == null) {
        return;
      }
      focusThumb(finger.thumbIndex);
      if (!listening) {
        dragging = false;
        twin.report('OnGesture', { active: true, dragging: false, commit: false });
      }
      setValueFromPointer(finger, 'track-press', event);
      moveCount = 0;
    }
    listen(true);
  }

  // SliderThumb.tsx:479-489 (SLD-PTR-02): a press on an enabled thumb records it and the grab offset.
  function recordThumbPress(target: EventTarget | null, coords: Coords): void {
    const index = thumbContaining(target);
    if (index < 0 || thumbInput(index)?.disabled) {
      return;
    }
    pressedThumbIndex = index;
    const midpoint = getMidpoint(thumbElements[index]!, options.vertical);
    pressedThumbCenterOffset = (options.vertical ? coords.y : coords.x) - midpoint;
  }

  // SliderControl.tsx:491-556 (SLD-PTR-01, 05..08, 16).
  function onPointerDown(event: PointerEvent): void {
    pointerGesture = false;
    const target = getTarget(event);
    const coords = { x: event.clientX, y: event.clientY };
    recordThumbPress(target, coords);
    if (!control || options.disabled || event.defaultPrevented || !isElement(target) || event.button !== 0) {
      return;
    }
    if (isTargetDisabledThumb(target)) {
      resetPressedThumb();
      return;
    }

    // ADR 04 §3: the consumer's C# onpointerdown, inside the dispatch on WebAssembly; fallback start.
    if (options.pressHandler && decide(twin, PRESS_POINT, event, 'start') === 'stop') {
      resetPressedThumb();
      return;
    }

    startPressing(coords);
    const finger = getFingerState(coords);
    if (finger == null) {
      return;
    }
    const pressedOnFocusedThumb = contains(thumbElements[finger.thumbIndex] ?? null, activeElement(ownerDocument(control)));
    if (pressedOnFocusedThumb) {
      event.preventDefault();
    } else {
      focusFrame.request(() => focusThumb(finger.thumbIndex));
    }
    dragging = true;
    twin.report('OnGesture', { active: true, dragging: true, commit: false });

    const pressedOnAnyThumb = pressedThumbCenterOffset != null;
    if (!pressedOnAnyThumb) {
      setValueFromPointer(finger, 'track-press', event);
    }

    // SLD §16 Q4, fixed (decision 23): capture whenever the pointer id is defined, 0 included.
    if (event.pointerId !== undefined) {
      try {
        control.setPointerCapture(event.pointerId);
      } catch {
        // A pointer that is no longer active cannot be captured; the document listeners still track it.
      }
    }
    moveCount = 0;
    pointerGesture = isTouchLikePointerType(event.pointerType);
    listen(false);
  }

  function listen(touch: boolean): void {
    const doc = ownerDocument(control ?? root);
    listening = true;
    if (touch) {
      doc.addEventListener('touchmove', onTouchMove, { passive: true });
      doc.addEventListener('touchend', onTouchEnd, { passive: true });
      doc.addEventListener('touchcancel', onTouchEnd, { passive: true });
      return;
    }
    doc.addEventListener('pointermove', onTouchMove, { passive: true });
    doc.addEventListener('pointerup', onTouchEnd, { once: true });
    doc.addEventListener('pointercancel', onPointerCancel, { once: true });
  }

  // SliderControl.tsx:445-457; SLD §16 Q6, fixed (decision 23): the pressed index and the grab offset are cleared too.
  function stopListening(): void {
    const doc = ownerDocument(control ?? root);
    doc.removeEventListener('pointermove', onTouchMove);
    doc.removeEventListener('pointerup', onTouchEnd);
    doc.removeEventListener('pointercancel', onPointerCancel);
    doc.removeEventListener('touchmove', onTouchMove);
    doc.removeEventListener('touchend', onTouchEnd);
    doc.removeEventListener('touchcancel', onTouchEnd);
    touchId = null;
    pressedValues = null;
    pointerGesture = false;
    listening = false;
    resetPressedThumb();
    swaps.clear();
  }

  // SLD-PTR-17, SLD-EDGE-15: a gesture dropped by disabling commits nothing.
  function dropGesture(): void {
    if (!listening && !dragging) {
      return;
    }
    stopListening();
    dragging = false;
    twin.report('OnGesture', { active: false, dragging: false, commit: false });
  }

  // -------------------------------------------------------------------------------------------------------------
  // Updates (the revision rule) and the handle
  // -------------------------------------------------------------------------------------------------------------

  function apply(next: SliderOptions): void {
    const wasDisabled = options.disabled;
    options = next;
    // ADR 04 Amendments 2026-11-18: an update older than the last act keeps the module's values unless it rolls back.
    if (next.revision >= actRevision || next.rollback) {
      values = next.values.slice();
      if (next.rollback) {
        // A refused swap leaves the originally pressed thumb pressed (SLD-PTR-10); later moves start from C#'s values.
        // The rollback's revision is the refused act's own, so the acts it undoes start at it.
        let restored: number | undefined;
        for (const revision of swaps.keys()) {
          if (revision >= next.revision && (restored === undefined || revision < restored)) {
            restored = revision;
          }
        }
        if (restored !== undefined) {
          const swappedTo = pressedThumbIndex;
          pressedThumbIndex = swaps.get(restored)!;
          // Focus went to the swapped thumb with the optimistic move; it returns to the originally pressed one.
          if (contains(thumbElements[swappedTo] ?? null, activeElement(ownerDocument(root)))) {
            focusThumb(pressedThumbIndex);
          }
        }
        for (const revision of [...swaps.keys()]) {
          if (revision >= next.revision) {
            swaps.delete(revision);
          }
        }
        latestValues = values;
      }
      actRevision = Math.max(actRevision, next.revision);
    }
    resolve();

    // SLD-FOC-05, SLD-PTR-17: disabling drops the gesture and blurs the focused element inside the root.
    if (next.disabled && !wasDisabled) {
      dropGesture();
      const focused = activeElement(ownerDocument(root));
      if (focused instanceof HTMLElement && contains(root, focused)) {
        focused.blur();
      }
      active = -1;
    }
    refresh();
  }

  root.addEventListener('keydown', onKeyDown);
  root.addEventListener('input', onInput);
  root.addEventListener('change', onInput);
  root.addEventListener('focusin', onFocusIn);
  root.addEventListener('focusout', onFocusOut);
  resolve();
  refresh();
  // SLD-ANI-01, SLD §16 Q12 (replicated): one more inset pass a microtask after mount.
  if (options.inset) {
    queueMicrotask(() => {
      if (!disposed) {
        refresh();
      }
    });
  }

  const handle: TwinHandle = {
    update(nextJson, nextReferences, nextCaptures) {
      if (disposed) {
        return;
      }
      currentReferences = nextReferences;
      currentCaptures = nextCaptures;
      apply(parseOptions<SliderOptions>(nextJson));
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      stopListening();
      focusFrame.cancel();
      for (const stop of unobserveResize) {
        stop();
      }
      root.removeEventListener('keydown', onKeyDown);
      root.removeEventListener('input', onInput);
      root.removeEventListener('change', onInput);
      root.removeEventListener('focusin', onFocusIn);
      root.removeEventListener('focusout', onFocusOut);
      control?.removeEventListener('pointerdown', onPointerDown);
      control?.removeEventListener('touchstart', onTouchStart);
      for (const style of owned.values()) {
        style.layer.clear();
      }
      owned.clear();
      delete root[attachedHandle];
    },
  };
  root[attachedHandle] = handle;
  return handle;
}
