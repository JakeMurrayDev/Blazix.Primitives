// The `dom` mechanism module (ADR 04 §1 as amended 2026-10-18, 2026-10-20 and 2026-10-22): the platform flags, owner
// and shadow-DOM helpers, rect and computed-style reads, ResizeObserver and IntersectionObserver subscriptions, and the
// primitives a part drives through `DomTwin`: the `checked` and `indeterminate` properties of a hidden input
// (SWT-DOM-08, CHK-DOM-13), the live-region marker (UTL-A11Y-01..04) and the image-load probe with its convergent write
// (AVA-ALG-01/02, ADR 04 Amendments 2026-10-18).
// Transcribed from Base UI v1.9.0: packages/utils/src/{owner,shadowDom}.ts; packages/react/src/utils/{getCssDimensions,
// getElementTransform,getPseudoElementBounds,getElementAtPoint,scrollable,scrollEdges,valueToPercent}.ts;
// packages/utils/src/clamp.ts; internals/useInitialLiveRegionTextMutation.ts; avatar/image/useImageLoadingStatus.ts and
// AvatarImage.tsx.
// Requirements: shared-utilities.md §4.10 (UTL-DOM-18..22), §4.11 (UTL-DOM-23..31), §4.13, §4.16, UTL-ALG-11..14.
// The Floating UI DOM predicates Base UI imports are re-implemented here with the same semantics, so this module and
// the button module stay out of `js/positioning.js`, which bundles the engine (ADR 04 §4, Amendments 2026-10-27).
import { platform } from './platform';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinHandle,
  type TwinOptions,
  type TwinCaptures,
  type TwinReferences,
} from './twin';

export { detectPlatform, platform, type NavigatorData, type Platform } from './platform';

const noop = (): void => {};

// ---------------------------------------------------------------------------------------------------------------
// Owner and shadow-DOM helpers (UTL-DOM-18..22)
// ---------------------------------------------------------------------------------------------------------------

/**
 * Returns the document a node belongs to.
 * @param node The node, or `null`.
 * @returns The node's owner document, or the global document for `null` and for a document itself.
 */
export function ownerDocument(node: Node | null | undefined): Document {
  return node?.ownerDocument || document;
}

/**
 * Returns the window a node belongs to.
 * @param node The node, or `null`.
 * @returns The window of the node's owner document, or the global window.
 */
export function ownerWindow(node: Node | null | undefined): Window & typeof globalThis {
  return (node?.ownerDocument?.defaultView as (Window & typeof globalThis) | null) || window;
}

function isNode(value: unknown): value is Node {
  return typeof value === 'object' && value !== null && typeof (value as Node).nodeType === 'number';
}

/**
 * Determines whether a value is an element of any window.
 * @param value The value to test.
 * @returns `true` for an `Element` of this window or of the node's own window.
 */
export function isElement(value: unknown): value is Element {
  return isNode(value) && (value instanceof Element || value instanceof ownerWindow(value).Element);
}

/**
 * Determines whether a value is an HTML element of any window.
 * @param value The value to test.
 * @returns `true` for an `HTMLElement` of this window or of the node's own window.
 */
export function isHTMLElement(value: unknown): value is HTMLElement {
  return isNode(value) && (value instanceof HTMLElement || value instanceof ownerWindow(value).HTMLElement);
}

/**
 * Determines whether a value is a shadow root of any window.
 * @param value The value to test.
 * @returns `true` for a `ShadowRoot` of this window or of the node's own window.
 */
export function isShadowRoot(value: unknown): value is ShadowRoot {
  return (
    isNode(value) &&
    typeof ShadowRoot !== 'undefined' &&
    (value instanceof ShadowRoot || value instanceof ownerWindow(value).ShadowRoot)
  );
}

// ADR 04 Amendments 2026-10-27: `@floating-ui/utils/dom`'s isOverflowElement, re-implemented with its semantics so the
// scroll lock stays out of the positioning chunk (UTL-DOM-04; floating-ui.utils.dom.mjs:45-53).
/**
 * Determines whether an element establishes an overflow context: its computed `overflow`, `overflow-x` or `overflow-y`
 * is `auto`, `scroll`, `overlay`, `hidden` or `clip`, and its `display` is neither `inline` nor `contents`.
 * @param element The element.
 * @returns `true` for an overflow element.
 */
export function isOverflowElement(element: Element): boolean {
  const { overflow, overflowX, overflowY, display } = ownerWindow(element).getComputedStyle(element);
  return /auto|scroll|overlay|hidden|clip/.test(overflow + overflowY + overflowX) && display !== 'inline' && display !== 'contents';
}

/**
 * Returns the focused element, descending into open shadow roots.
 * @param doc The document to start from.
 * @returns The deepest focused element, or `null`.
 */
export function activeElement(doc: Document): Element | null {
  let element = doc.activeElement;
  while (element?.shadowRoot?.activeElement != null) {
    element = element.shadowRoot.activeElement;
  }
  return element;
}

/**
 * Determines whether `parent` contains `child`, crossing shadow boundaries.
 * @param parent The candidate ancestor.
 * @param child The candidate descendant.
 * @returns `false` when either is missing; otherwise whether `child` is `parent` or inside it in the composed tree.
 */
export function contains(parent?: Element | null, child?: Element | null): boolean {
  if (!parent || !child) {
    return false;
  }

  const rootNode = child.getRootNode?.();

  if (parent.contains(child)) {
    return true;
  }

  if (rootNode && isShadowRoot(rootNode)) {
    let next: Node | null = child;
    while (next) {
      if (parent === next) {
        return true;
      }
      next = next.parentNode || (isShadowRoot(next) ? next.host : null);
    }
  }

  return false;
}

/**
 * Finds the closest element matching a selector in the composed tree, crossing slots and shadow roots. `:scope` is
 * not supported.
 * @param node The node to start from, which may be a text node.
 * @param selector The selector to match; an invalid selector throws the native error.
 * @returns The matching element, or `null` (never the document element of a detached subtree).
 */
export function closest<E extends Element = Element>(node: Node | null | undefined, selector: string): E | null {
  let current: Node | null | undefined = node;
  while (current) {
    if (isElement(current) && current.matches(selector)) {
      return current as E;
    }
    current = (current as Element).assignedSlot ?? current.parentNode ?? (isShadowRoot(current) ? current.host : null);
  }
  return null;
}

/**
 * Returns the real target of an event: the first node of its composed path while it is dispatched, which lies inside
 * shadow roots, and `event.target` once the dispatch has completed.
 * @param event The event.
 * @returns The target.
 */
export function getTarget(event: Event): EventTarget | null {
  if ('composedPath' in event) {
    return event.composedPath()[0] ?? event.target;
  }
  return (event as Event).target;
}

// ---------------------------------------------------------------------------------------------------------------
// Measurement and scroll helpers (UTL-DOM-23..31, UTL-ALG-12..14)
// ---------------------------------------------------------------------------------------------------------------

/** A width and a height in CSS pixels. */
export interface Dimensions {
  width: number;
  height: number;
}

/**
 * Reads an element's CSS size, falling back to its offset size when the rounded CSS size differs from it (transforms,
 * subpixel layout). A non-HTML element (SVG) has no offset size and keeps its CSS size.
 * @param element The element to measure.
 * @returns The size; a non-numeric CSS size reads as 0.
 */
export function getCssDimensions(element: Element): Dimensions {
  const css = ownerWindow(element).getComputedStyle(element);
  let width = parseFloat(css.width) || 0;
  let height = parseFloat(css.height) || 0;
  const hasOffset = isHTMLElement(element);
  const offsetWidth = hasOffset ? element.offsetWidth : width;
  const offsetHeight = hasOffset ? element.offsetHeight : height;

  if (Math.round(width) !== offsetWidth || Math.round(height) !== offsetHeight) {
    width = offsetWidth;
    height = offsetHeight;
  }

  return { width, height };
}

/** A 2D translation and a uniform scale. */
export interface ElementTransform {
  x: number;
  y: number;
  scale: number;
}

/**
 * Extracts the translation and scale of an element's computed `transform` matrix. The separate `translate`, `rotate`
 * and `scale` properties are not reflected in it.
 * @param element The element.
 * @param computedStyle The element's computed style when the caller already has it.
 * @returns The translation and scale; the identity for `none`.
 */
export function getElementTransform(element: Element, computedStyle?: CSSStyleDeclaration): ElementTransform {
  const transform = (computedStyle ?? ownerWindow(element).getComputedStyle(element)).transform;
  let x = 0;
  let y = 0;
  let scale = 1;

  if (transform && transform !== 'none') {
    const matrix = transform.match(/matrix(?:3d)?\(([^)]+)\)/);
    if (matrix) {
      const values = matrix[1]!.split(', ').map(parseFloat);
      if (values.length === 6) {
        x = values[4]!;
        y = values[5]!;
        scale = Math.sqrt(values[0]! * values[0]! + values[1]! * values[1]!);
      } else if (values.length === 16) {
        x = values[12]!;
        y = values[13]!;
        scale = values[0]!;
      }
    }
  }

  return { x, y, scale };
}

/** The four edges of a box in client coordinates. */
export interface Bounds {
  left: number;
  right: number;
  top: number;
  bottom: number;
}

// Tolerance around the element bounds so a fast click whose pointer drifts slightly during press-release is not
// mistaken for a drag-off-and-release cancellation.
const BOUNDARY_OFFSET = 5;

/**
 * Returns an element's bounding box, extended symmetrically to cover its `::before` and `::after` pseudo-elements. In
 * jsdom the pseudo-element styles are not read.
 * @param element The element.
 * @returns The bounds.
 */
export function getPseudoElementBounds(element: HTMLElement): Bounds {
  const rect = element.getBoundingClientRect();

  if (platform.env.jsdom) {
    return rect;
  }

  const win = ownerWindow(element);
  const before = win.getComputedStyle(element, '::before');
  const after = win.getComputedStyle(element, '::after');

  if (before.content === 'none' && after.content === 'none') {
    return rect;
  }

  const totalWidth = Math.max(rect.width, parseFloat(before.width) || 0, parseFloat(after.width) || 0);
  const totalHeight = Math.max(rect.height, parseFloat(before.height) || 0, parseFloat(after.height) || 0);
  const widthDiff = totalWidth - rect.width;
  const heightDiff = totalHeight - rect.height;

  return {
    left: rect.left - widthDiff / 2,
    right: rect.right + widthDiff / 2,
    top: rect.top - heightDiff / 2,
    bottom: rect.bottom + heightDiff / 2,
  };
}

/**
 * Determines whether a pointer position lies within an element and its pseudo-elements, with 5px of tolerance on
 * every side.
 * @param event The pointer position.
 * @param element The element.
 * @returns `true` when the position is within the tolerance, edges included.
 */
export function isMouseWithinBounds(event: { clientX: number; clientY: number }, element: HTMLElement): boolean {
  const bounds = getPseudoElementBounds(element);
  return (
    event.clientX >= bounds.left - BOUNDARY_OFFSET &&
    event.clientX <= bounds.right + BOUNDARY_OFFSET &&
    event.clientY >= bounds.top - BOUNDARY_OFFSET &&
    event.clientY <= bounds.bottom + BOUNDARY_OFFSET
  );
}

/**
 * Returns the topmost element at a point of a document or shadow root. Pass the popup's root node rather than its
 * document, so shadow content is not retargeted to its host.
 * @param root A document or shadow root.
 * @param x The client x coordinate.
 * @param y The client y coordinate.
 * @returns The element, or `null` when the root cannot answer.
 */
export function getElementAtPoint(root: Node | null | undefined, x: number, y: number): Element | null {
  const candidate = root as (Node & Partial<Pick<Document, 'elementFromPoint'>>) | null | undefined;
  return typeof candidate?.elementFromPoint === 'function' ? candidate.elementFromPoint(x, y) : null;
}

/** A scroll axis. */
export type ScrollAxis = 'horizontal' | 'vertical';

/**
 * Determines whether an element scrolls vertically.
 * @param element The element.
 * @param allowOverflowIntent When `true`, a container with room on the axis counts even before its content overflows.
 * @returns `true` when `overflow-y` is `auto` or `scroll` and the content overflows (or there is room, with intent).
 */
export function isScrollableY(element: HTMLElement, allowOverflowIntent = false): boolean {
  const { overflowY } = ownerWindow(element).getComputedStyle(element);
  if (overflowY !== 'auto' && overflowY !== 'scroll') {
    return false;
  }
  return allowOverflowIntent ? element.clientHeight > 0 : element.scrollHeight > element.clientHeight;
}

/**
 * Determines whether an element scrolls horizontally.
 * @param element The element.
 * @param allowOverflowIntent When `true`, a container with room on the axis counts even before its content overflows.
 * @returns `true` when `overflow-x` is `auto` or `scroll` and the content overflows (or there is room, with intent).
 */
export function isScrollableX(element: HTMLElement, allowOverflowIntent = false): boolean {
  const { overflowX } = ownerWindow(element).getComputedStyle(element);
  if (overflowX !== 'auto' && overflowX !== 'scroll') {
    return false;
  }
  return allowOverflowIntent ? element.clientWidth > 0 : element.scrollWidth > element.clientWidth;
}

/**
 * Determines whether an element scrolls on an axis.
 * @param element The element.
 * @param axis The axis.
 * @param allowOverflowIntent When `true`, a container with room on the axis counts even before its content overflows.
 * @returns Whether the element scrolls on `axis`.
 */
export function isScrollable(element: HTMLElement, axis: ScrollAxis, allowOverflowIntent = false): boolean {
  return axis === 'vertical' ? isScrollableY(element, allowOverflowIntent) : isScrollableX(element, allowOverflowIntent);
}

function nodeName(node: Node): string {
  return (node.nodeName || '').toLowerCase();
}

function isLastTraversableNode(node: Node): boolean {
  return ['html', 'body', '#document'].includes(nodeName(node));
}

function getParentNode(node: Node): Node {
  if (nodeName(node) === 'html') {
    return node;
  }
  const result: Node =
    (node as Element).assignedSlot ||
    node.parentNode ||
    (isShadowRoot(node) ? node.host : null) ||
    ownerDocument(node).documentElement;
  return isShadowRoot(result) ? result.host : result;
}

/**
 * Walks up the composed tree from a target to the first HTML element that scrolls on an axis, crossing shadow
 * boundaries and slots and stopping before `root` and before `html`, `body` and the document.
 * @param target The node to start from; non-HTML nodes are skipped but their ancestors are visited.
 * @param root The element the walk stops at.
 * @param axis The axis.
 * @param allowOverflowIntent See {@link isScrollable}.
 * @returns The scrollable ancestor, or `null`.
 */
export function findScrollableAncestor(
  target: EventTarget | null,
  root: HTMLElement,
  axis: ScrollAxis,
  allowOverflowIntent = false,
): HTMLElement | null {
  let node: unknown = target;
  while (isElement(node) && node !== root && !isLastTraversableNode(node)) {
    if (isHTMLElement(node) && isScrollable(node, axis, allowOverflowIntent)) {
      return node;
    }
    node = getParentNode(node);
  }
  return null;
}

/**
 * Finds the element a touch on `target` would scroll: a scrollable ancestor below `root`, else `root` itself when it
 * scrolls.
 * @param target The touch target.
 * @param root The element the walk stops at.
 * @param axis The axis, vertical by default.
 * @param allowOverflowIntent See {@link isScrollable}.
 * @returns The element, or `null`.
 */
export function findScrollableTouchTarget(
  target: EventTarget | null,
  root: HTMLElement,
  axis: ScrollAxis = 'vertical',
  allowOverflowIntent = false,
): HTMLElement | null {
  return findScrollableAncestor(target, root, axis, allowOverflowIntent) ?? (isScrollable(root, axis, allowOverflowIntent) ? root : null);
}

/** The distance in pixels within which a scroll offset snaps to its edge. */
export const SCROLL_EDGE_TOLERANCE_PX = 1;

/**
 * Returns the largest scroll offset of a scroll container.
 * @param scrollSize The scroll width or height.
 * @param clientSize The client width or height.
 * @returns The overflow, never negative.
 */
export function getMaxScrollOffset(scrollSize: number, clientSize: number): number {
  return Math.max(0, scrollSize - clientSize);
}

/**
 * Clamps a scroll offset to its range and snaps it to an edge within 1px; when both edges are that close, the nearer
 * one wins and a tie goes to the start.
 * @param value The offset.
 * @param max The largest offset.
 * @returns The normalised offset; 0 when `max` is not positive.
 */
export function normalizeScrollOffset(value: number, max: number): number {
  if (max <= 0) {
    return 0;
  }

  const clamped = Math.min(Math.max(value, 0), max);
  const startDistance = clamped;
  const endDistance = max - clamped;
  const withinStartTolerance = startDistance <= SCROLL_EDGE_TOLERANCE_PX;
  const withinEndTolerance = endDistance <= SCROLL_EDGE_TOLERANCE_PX;

  if (withinStartTolerance && withinEndTolerance) {
    return startDistance <= endDistance ? 0 : max;
  }
  if (withinStartTolerance) {
    return 0;
  }
  if (withinEndTolerance) {
    return max;
  }
  return clamped;
}

// UTL-EDGE-14: the loops' clamp, beside C#'s RangeMath.Clamp (ADR 01 decision 2, one implementation per loop; the
// UTL-DOM-31 precedent). `floating-ui.ts`'s re-export of the engine's clamp is the positioning module's alone.
/**
 * Clamps a value to a range.
 * @param value The value.
 * @param min The lower bound; `Number.MIN_SAFE_INTEGER` when omitted.
 * @param max The upper bound; `Number.MAX_SAFE_INTEGER` when omitted.
 * @returns `Math.max(min, Math.min(value, max))`.
 */
export function clamp(
  value: number,
  min: number = Number.MIN_SAFE_INTEGER,
  max: number = Number.MAX_SAFE_INTEGER,
): number {
  return Math.max(min, Math.min(value, max));
}

/**
 * Maps a value onto a percentage of a range, without clamping.
 * @param value The value.
 * @param min The start of the range.
 * @param max The end of the range.
 * @returns `(value - min) * 100 / (max - min)`.
 */
export function valueToPercent(value: number, min: number, max: number): number {
  return ((value - min) * 100) / (max - min);
}

// ---------------------------------------------------------------------------------------------------------------
// Observers
// ---------------------------------------------------------------------------------------------------------------

/**
 * Subscribes to an element's size changes. Does nothing where `ResizeObserver` does not exist.
 * @param element The element to observe.
 * @param callback Called with each entry the observer delivers.
 * @param options The box to observe.
 * @returns The function that disconnects the observer.
 */
export function observeResize(
  element: Element,
  callback: (entry: ResizeObserverEntry) => void,
  options?: ResizeObserverOptions,
): () => void {
  if (typeof ResizeObserver === 'undefined') {
    return noop;
  }
  const observer = new ResizeObserver((entries) => {
    for (const entry of entries) {
      callback(entry);
    }
  });
  observer.observe(element, options);
  return () => observer.disconnect();
}

/**
 * Subscribes to an element's intersection changes. Does nothing where `IntersectionObserver` does not exist.
 * @param element The element to observe.
 * @param callback Called with each entry the observer delivers.
 * @param options The root, margin and thresholds.
 * @returns The function that disconnects the observer.
 */
export function observeIntersection(
  element: Element,
  callback: (entry: IntersectionObserverEntry) => void,
  options?: IntersectionObserverInit,
): () => void {
  if (typeof IntersectionObserver === 'undefined') {
    return noop;
  }
  const observer = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      callback(entry);
    }
  }, options);
  observer.observe(element);
  return () => observer.disconnect();
}

// ---------------------------------------------------------------------------------------------------------------
// The layered-style writer (ADR 04 Amendments 2026-10-27, Decision 2)
// ---------------------------------------------------------------------------------------------------------------

/**
 * A named layer of declarations a module writes on an element, in rank order, a higher rank winning:
 * - `positioning` and `transitions` lose to the consumer's style (Base UI merges the positioner styles, then
 *   `transition: none`, then the consumer's props);
 * - `component-under` holds a value a component passes through a part's merged `style`, which the consumer's style
 *   overrides, so it ranks just under the consumer;
 * - the consumer's style;
 * - `positioning-measured` and `auto-resize` win over it (Base UI writes those variables with `setProperty` after
 *   render), and `aligned` wins over those (Select's aligned placement writes the positioner's coordinates and size
 *   after them);
 * - `component` holds a value a component writes with `style.setProperty` or `element.style` after render, which
 *   overrides the consumer, so it ranks last.
 *
 * A property may hold a value in several bands; the highest rank shows, and removing it there hands the property back
 * to the next band that holds it.
 */
export type StyleLayerName =
  | 'positioning'
  | 'transitions'
  | 'component-under'
  | 'positioning-measured'
  | 'auto-resize'
  | 'aligned'
  | 'component';

// SEL-DOM-20, SEL-POS-07 (M5 track 9, granted by the orchestrator 2026-11-04; design point 1 (a), settled by the
// orchestrator 2026-11-04): the aligned band is over the other positioning bands. ADR 04 Amendments 2026-11-18, the
// bands paragraph (decision 5 as revised, chosen by the user 2026-11-18): `component-under` right under the consumer,
// `component` last. The consumer's rank is the number of bands under it.
const underConsumer: readonly StyleLayerName[] = ['positioning', 'transitions', 'component-under'];
const overConsumer: readonly StyleLayerName[] = ['positioning-measured', 'auto-resize', 'aligned', 'component'];
const consumerRank = underConsumer.length;
const styleLayerRanks: ReadonlyMap<StyleLayerName, number> = new Map([
  ...underConsumer.map((name, index) => [name, index] as const),
  ...overConsumer.map((name, index) => [name, consumerRank + 1 + index] as const),
]);

/** One module's declarations on an element. */
export interface StyleLayer {
  /** Sets a declaration; a shorthand is held as the longhands the browser expands it to. */
  set(property: string, value: string): void;
  /** Removes a declaration this layer set. */
  remove(property: string): void;
  /** Removes every declaration of this layer; a module clears its layers when its handle is disposed. */
  clear(): void;
}

/** The one writer of an element's `style` attribute once a module has taken it over. */
export interface StyleWriter {
  /** Sets the consumer's merged style string; `null` for none. */
  setConsumer(cssText: string | null | undefined): void;
  /** Returns the named layer. */
  layer(name: StyleLayerName): StyleLayer;
  /**
   * Makes the writer take a write to the `style` attribute it did not make, such as a re-render that renders
   * declarations of its own, as the consumer's style, writing every layer's declarations again over it. Only an element
   * whose `style` another renderer rewrites asks for this: modules that override a declaration directly for a
   * measurement and restore it later would otherwise have their overrides taken as the consumer's.
   */
  absorbForeignWrites(): void;
}

interface Declaration {
  value: string;
  priority: string;
}

// A symbol on the element, as every module's handle is kept: the modules that write one element share its writer.
const attachedStyleWriter = Symbol('iodine.dom.styleWriter');

interface StyledElement extends HTMLElement {
  [attachedStyleWriter]?: StyleWriter;
}

function readDeclarations(scratch: CSSStyleDeclaration, cssText: string): Map<string, Declaration> {
  const parsed = new Map<string, Declaration>();
  scratch.cssText = cssText;
  for (let i = 0; i < scratch.length; i++) {
    const name = scratch[i]!;
    parsed.set(name, { value: scratch.getPropertyValue(name), priority: scratch.getPropertyPriority(name) });
  }
  return parsed;
}

/**
 * Returns the element's style writer, created on first use and shared by every module that writes the element: the
 * element's whole `style` is the consumer's string plus the named layers in their fixed order, and each write touches
 * only the properties whose winning value changed. C# freezes the element's rendered `style` from the handover on, so
 * this is its only writer.
 * @param element The element.
 * @returns The writer.
 */
export function styleWriter(element: HTMLElement): StyleWriter {
  const styled = element as StyledElement;
  const existing = styled[attachedStyleWriter];
  if (existing) {
    return existing;
  }

  // The under layers, then the consumer at `consumerRank`, then the over layers; a higher rank wins.
  const sources: Array<Map<string, Declaration>> = Array.from(
    { length: underConsumer.length + 1 + overConsumer.length },
    () => new Map<string, Declaration>(),
  );
  const applied = new Map<string, string>();

  const apply = (properties: Iterable<string>) => {
    for (const property of properties) {
      let winner: Declaration | undefined;
      for (let rank = sources.length - 1; rank >= 0 && winner === undefined; rank--) {
        winner = sources[rank]!.get(property);
      }
      const key = winner === undefined ? undefined : `${winner.value}!${winner.priority}`;
      if (key === applied.get(property)) {
        continue;
      }
      if (winner === undefined) {
        applied.delete(property);
        element.style.removeProperty(property);
      } else {
        applied.set(property, key!);
        element.style.setProperty(property, winner.value, winner.priority);
      }
    }
  };

  const replace = (rank: number, next: Map<string, Declaration>) => {
    const previous = sources[rank]!;
    sources[rank] = next;
    apply(new Set([...previous.keys(), ...next.keys()]));
  };

  const layers = new Map<StyleLayerName, StyleLayer>();
  let consumerText: string | null = null;
  let scratch: CSSStyleDeclaration | null = null;
  // A custom property is its own longhand; anything else goes through a detached declaration, which expands shorthands
  // the way the browser does.
  const parse = (cssText: string): Map<string, Declaration> =>
    readDeclarations((scratch ??= ownerDocument(element).createElement('div').style), cssText);
  const longhands = (property: string, value: string): Map<string, Declaration> =>
    property.startsWith('--') ? new Map([[property, { value, priority: '' }]]) : parse(`${property}: ${value}`);

  // ADR 04 Amendments 2026-11-18, design point 2 of the Drawer track, as revised 2026-11-19 (the opt-in form, chosen by
  // the user 2026-11-19; granted to session D, M6 track 8b): on a writer that opts in (`absorbForeignWrites`, the Drawer
  // Popup, whose C# re-render renders declarations in its commit path), a write to the `style` attribute the writer did
  // not make is absorbed. The attribute's text becomes the consumer's, the cache is rebuilt from the element and every
  // band's winners are written again. Foreign is judged by declaration against the cache, not by text. The observer,
  // which runs before paint, absorbs any difference. A writer call absorbs first only when a declaration it applied was
  // removed or changed. Declarations merely added inside one task are left to the observer, as Select's measurement
  // does (select.ts `unsetTransformStyles`). Opt-in, not installed at the first layer write as first settled: popup.ts
  // `override` writes a measurement declaration directly and restores it in a later task, and an observer on every
  // writer took it, and the bands present with it, as the consumer's (6 L4 titles failed in the sample of 2026-11-19).
  let written = false;
  let observer: MutationObserver | null = null;

  const differsFromCache = (additions: boolean) => {
    const style = element.style;
    for (const [property, key] of applied) {
      if (`${style.getPropertyValue(property)}!${style.getPropertyPriority(property)}` !== key) {
        return true;
      }
    }
    if (additions) {
      for (let i = 0; i < style.length; i++) {
        if (!applied.has(style[i]!)) {
          return true;
        }
      }
    }
    return false;
  };

  const absorbForeignWrite = (additions: boolean) => {
    if (observer === null || !written || !differsFromCache(additions)) {
      return;
    }
    const current = element.getAttribute('style');
    consumerText = current || null;
    sources[consumerRank] = consumerText === null ? new Map() : parse(consumerText);
    applied.clear();
    const style = element.style;
    for (let i = 0; i < style.length; i++) {
      const name = style[i]!;
      applied.set(name, `${style.getPropertyValue(name)}!${style.getPropertyPriority(name)}`);
    }
    const properties = new Set(applied.keys());
    for (const source of sources) {
      for (const property of source.keys()) {
        properties.add(property);
      }
    }
    apply(properties);
  };

  // Runs one writer call, absorbing first a foreign write that removed or changed one of the writer's declarations.
  const write = (change: () => void) => {
    absorbForeignWrite(false);
    change();
    written = true;
  };

  const observe = () => {
    if (observer !== null) {
      return;
    }
    const View = ownerWindow(element).MutationObserver;
    if (typeof View !== 'function') {
      return;
    }
    observer = new View(() => absorbForeignWrite(true));
    observer.observe(element, { attributes: true, attributeFilter: ['style'] });
  };

  const writer: StyleWriter = {
    absorbForeignWrites: observe,
    setConsumer(cssText) {
      write(() => {
        const text = cssText || null;
        if (text === consumerText) {
          return;
        }
        consumerText = text;
        replace(consumerRank, text === null ? new Map() : parse(text));
      });
    },
    layer(name) {
      const known = layers.get(name);
      if (known) {
        return known;
      }
      const rank = styleLayerRanks.get(name)!;
      const layer: StyleLayer = {
        set(property, value) {
          write(() => {
            const declarations = longhands(property, value);
            const own = sources[rank]!;
            for (const [longhand, declaration] of declarations) {
              own.set(longhand, declaration);
            }
            apply(declarations.keys());
          });
        },
        remove(property) {
          write(() => {
            const removed = [...longhands(property, 'initial').keys(), property];
            const own = sources[rank]!;
            for (const longhand of removed) {
              own.delete(longhand);
            }
            apply(removed);
          });
        },
        clear() {
          write(() => replace(rank, new Map()));
        },
      };
      layers.set(name, layer);
      return layer;
    },
  };
  styled[attachedStyleWriter] = writer;
  return writer;
}

// ---------------------------------------------------------------------------------------------------------------
// Text-control helpers (M6 track 6, issue 277, 6b; design point 9, granted by the orchestrator 2026-11-19)
// ---------------------------------------------------------------------------------------------------------------

/**
 * Collapses a text control's selection at a position, clamped to its text. An input type without a selection API (such
 * as `number`) is left as it is.
 * @param input The text input or textarea.
 * @param position The caret's index.
 */
export function setCaret(input: HTMLInputElement | HTMLTextAreaElement, position: number): void {
  const index = Math.max(0, Math.min(position, input.value.length));
  try {
    input.setSelectionRange(index, index);
  } catch {
    // The input type has no selection.
  }
}

/**
 * Selects a text control's whole text. An input type without a selection API is left as it is.
 * @param input The text input or textarea.
 */
export function selectAll(input: HTMLInputElement | HTMLTextAreaElement): void {
  try {
    input.setSelectionRange(0, input.value.length);
  } catch {
    // The input type has no selection.
  }
}

/**
 * Writes a text control's `value` property only when the text differs, so an unchanged answer leaves the caret and the
 * selection where the user put them. C# renders the `value` attribute once (the handover at attach) and never diffs it
 * afterwards, so the property is the module's alone.
 * @param input The text input or textarea.
 * @param value The text.
 * @returns Whether the text was written.
 */
export function writeTextValue(input: HTMLInputElement | HTMLTextAreaElement, value: string): boolean {
  if (input.value === value) {
    return false;
  }
  input.value = value;
  return true;
}

/** Whether an IME composition is in progress in a text control, from its composition events. */
export interface CompositionTracker {
  /** Returns whether a composition has started and not yet ended. */
  composing(): boolean;
  /** Removes the listeners. */
  dispose(): void;
}

/**
 * Tracks `compositionstart` and `compositionend` on a text control.
 * @param input The text input or textarea.
 * @returns The tracker.
 */
export function trackComposition(input: HTMLInputElement | HTMLTextAreaElement): CompositionTracker {
  let active = false;
  const onStart = () => {
    active = true;
  };
  const onEnd = () => {
    active = false;
  };
  input.addEventListener('compositionstart', onStart);
  input.addEventListener('compositionend', onEnd);
  return {
    composing: () => active,
    dispose() {
      active = false;
      input.removeEventListener('compositionstart', onStart);
      input.removeEventListener('compositionend', onEnd);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// Live-region marker (UTL-A11Y-01..04)
// ---------------------------------------------------------------------------------------------------------------

// Word Joiner is invisible and zero-width, so it forces a text mutation without shifting layout.
const LIVE_REGION_MARKER = '⁠';

/** How long the live-region marker stays before the text is restored, in milliseconds. */
export const INITIAL_LIVE_REGION_TEXT_MUTATION_RESET_DELAY = 200;

/** Runs a callback after the browser has painted the current frame: one animation frame, then one task. */
function afterPaint(callback: () => void): () => void {
  let frame: number | undefined;
  let timeout: ReturnType<typeof setTimeout> | undefined;
  if (typeof requestAnimationFrame === 'function') {
    frame = requestAnimationFrame(() => {
      frame = undefined;
      timeout = setTimeout(callback, 0);
    });
  } else {
    timeout = setTimeout(callback, 0);
  }
  return () => {
    if (frame !== undefined) {
      cancelAnimationFrame(frame);
    }
    if (timeout !== undefined) {
      clearTimeout(timeout);
    }
  };
}

function findLastTextNode(root: Element): Text | null {
  const walker = ownerDocument(root).createTreeWalker(root, NodeFilter.SHOW_TEXT);
  let last: Text | null = null;
  while (walker.nextNode()) {
    const text = walker.currentNode as Text;
    if (text.nodeValue !== '') {
      last = text;
    }
  }
  return last;
}

/**
 * Makes a screen reader announce the text a live region appears with: after the next paint, an invisible word joiner
 * is appended to the region's last non-empty text node, and removed again 200 ms later if the text still carries it.
 * Does nothing on iOS or when the region has no text.
 * @param element The live region.
 * @returns The cleanup: it cancels a pending mark or restore and removes a marker still in place.
 */
export function markLiveRegion(element: Element): () => void {
  if (platform.os.ios) {
    return noop;
  }

  let marked: { node: Text; original: string; value: string } | null = null;
  let reset: ReturnType<typeof setTimeout> | undefined;

  const restore = () => {
    if (marked && marked.node.nodeValue === marked.value) {
      marked.node.nodeValue = marked.original;
    }
    marked = null;
  };

  const cancelPaint = afterPaint(() => {
    const node = findLastTextNode(element);
    if (node === null) {
      return;
    }
    const original = node.data;
    marked = { node, original, value: `${original}${LIVE_REGION_MARKER}` };
    node.nodeValue = marked.value;
    reset = setTimeout(() => {
      reset = undefined;
      restore();
    }, INITIAL_LIVE_REGION_TEXT_MUTATION_RESET_DELAY);
  });

  return () => {
    cancelPaint();
    if (reset !== undefined) {
      clearTimeout(reset);
      reset = undefined;
    }
    restore();
  };
}

// ---------------------------------------------------------------------------------------------------------------
// Image-load probe (AVA-ALG-01, AVA-ALG-02)
// ---------------------------------------------------------------------------------------------------------------

/** The status of an image load. */
export type ImageLoadingStatus = 'loading' | 'loaded' | 'error';

/** The source properties of an image, as the `<img>` element receives them. */
export interface ImageSource {
  src?: string | null;
  srcSet?: string | null;
  sizes?: string | null;
  crossOrigin?: string | null;
  referrerPolicy?: string | null;
}

/**
 * Loads an image with a detached probe and reports its status. The synchronous pass reports once: `loading`, or the
 * result straight away when the image is already complete (cached). Without `src` and `srcSet` the status is `error`
 * and no probe is made.
 * @param source The image's source properties; the probe is configured before its source is assigned, source last.
 * @param onStatus Called with each status.
 * @returns The function that supersedes the probe: its later results are ignored.
 */
export function probeImage(source: ImageSource, onStatus: (status: ImageLoadingStatus) => void): () => void {
  if (!source.src && !source.srcSet) {
    onStatus('error');
    return noop;
  }

  let current = true;
  const image = new Image();
  const settle = (status: ImageLoadingStatus) => () => {
    if (current) {
      onStatus(status);
    }
  };

  image.onload = settle('loaded');
  image.onerror = settle('error');
  if (source.referrerPolicy) {
    image.referrerPolicy = source.referrerPolicy;
  }
  image.crossOrigin = source.crossOrigin ?? null;
  if (source.sizes) {
    image.sizes = source.sizes;
  }
  if (source.srcSet) {
    image.srcset = source.srcSet;
  }
  if (source.src) {
    image.src = source.src;
  }

  onStatus(image.complete ? (image.naturalWidth > 0 ? 'loaded' : 'error') : 'loading');

  return () => {
    current = false;
  };
}

/**
 * Derives the status of a rendered `<img>`: `loading` until it is complete, then `loaded` when it has a natural width
 * and `error` otherwise (a source-less image is complete with no width).
 * @param element The rendered element.
 * @returns The status, or `null` when the element is not an image.
 */
export function deriveImageStatus(element: Element): ImageLoadingStatus | null {
  if (!(element instanceof ownerWindow(element).HTMLImageElement)) {
    return null;
  }
  if (!element.complete) {
    return 'loading';
  }
  return element.naturalWidth > 0 ? 'loaded' : 'error';
}

// ---------------------------------------------------------------------------------------------------------------
// The twin-facing entry
// ---------------------------------------------------------------------------------------------------------------

/** The image-load primitive's options; mirrors `ImageLoadOptions` in C#. */
export interface ImageLoadOptions extends ImageSource {
  /** `true` derives the status from the attached `<img>`; `false` loads a detached probe. */
  keepMounted: boolean;
  /** The ids, separated by spaces, of the elements to hide the moment the image loads (the fallbacks), ahead of the render that removes them. */
  hideWhenLoaded?: string | null;
}

/** The options of a `dom` handle; mirrors `DomOptions` in C#. Each field turns one primitive on. */
export interface DomOptions extends TwinOptions {
  /** The `indeterminate` property of the attached checkbox input; absent leaves the property alone. */
  indeterminate?: boolean | null;
  /** The `checked` property of the attached checkbox or radio input; absent leaves the property alone. */
  checked?: boolean | null;
  /** Raised by C# after a request that left `checked` different from a native click's value, so the update arrives. */
  checkedRestores?: number | null;
  /** Whether the attached element is a live region whose text must be announced when it appears. */
  liveRegion?: boolean | null;
  /** The image whose loading status the handle reports. */
  image?: ImageLoadOptions | null;
}

// The attributes of a kept-mounted `<img>` that start a new request when a render changes them (AVA-STA-12).
const IMAGE_SOURCE_ATTRIBUTES = ['src', 'srcset', 'sizes', 'crossorigin', 'referrerpolicy'];

function imageKey(image: ImageLoadOptions | null | undefined): string | null {
  return image ? JSON.stringify([image.src, image.srcSet, image.sizes, image.crossOrigin, image.referrerPolicy, image.keepMounted]) : null;
}

/**
 * Attaches the `dom` primitives to an element. The module owns no style property: the consumer's style stays with the
 * part's render output and `options.style` is ignored.
 * @param elementOrId The element, or its id when the part has no element reference.
 * @param optionsJson The `DomOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param _references The references, which this module names none of.
 * @param captures The capture ids C# sends beside the element and the references, which find them inside a shadow root.
 * @returns The handle with `update(optionsJson)` and `dispose()`.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  _references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const element = resolveElement(elementOrId, captures?.[0]);
  const twin = createTwinChannel(twinReference);
  let options = parseOptions<DomOptions>(optionsJson);
  let disposed = false;
  let unmarkLiveRegion: (() => void) | null = null;
  let supersedeProbe: () => void = noop;
  let evaluatedImage: string | null = null;
  let lastStatus: ImageLoadingStatus | null = null;
  let stopObservingSource: (() => void) | null = null;
  let evaluatedKeptMounted = false;

  const assertIndeterminate = () => {
    if (options.indeterminate != null && element instanceof ownerWindow(element).HTMLInputElement) {
      element.indeterminate = options.indeterminate;
    }
  };

  // ADR 04 Amendments 2026-10-22: `checked` is set on attach and on every update, never inside a native click. A click C#
  // accepts must stay until its commit arrives; a refused one is reverted natively (the `button` module's `toggle`
  // decision on WebAssembly, its read-only `preventDefault()`) or restored by the update that raises `checkedRestores`.
  const assertChecked = () => {
    if (!disposed && options.checked != null && element instanceof ownerWindow(element).HTMLInputElement) {
      element.checked = options.checked;
    }
  };

  // switch.md §16 Q1 (b), chosen by the user 2026-10-22 (FFI-FRM-30's dated note): a form reset puts the input back to the
  // `checked` attribute C# rendered first. The reset algorithm runs after the `reset` event's listeners, so the property is
  // re-asserted one task later. The owning form is the input's `form`, which follows its `form` attribute.
  let resetForm: HTMLFormElement | null = null;
  const onReset = () => {
    ownerWindow(element).setTimeout(assertChecked, 0);
  };
  const bindReset = () => {
    const form = !disposed && options.checked != null && element instanceof ownerWindow(element).HTMLInputElement ? element.form : null;
    if (form === resetForm) {
      return;
    }
    resetForm?.removeEventListener('reset', onReset);
    resetForm = form;
    resetForm?.addEventListener('reset', onReset);
  };

  // A native click on a checkbox clears `indeterminate` before the click is dispatched; this listener restores it
  // inside the same dispatch, before any later listener or Blazor sees the event.
  const onClick = () => assertIndeterminate();

  // One message per state change: a status equal to the last one reported is not sent again.
  const reportStatus = (status: ImageLoadingStatus) => {
    if (status === lastStatus) {
      return;
    }
    lastStatus = status;
    const hide = options.image?.hideWhenLoaded;
    if (status === 'loaded' && hide) {
      // The convergent write: the render C# makes when this status arrives removes the same elements.
      const document = ownerDocument(element);
      for (const id of hide.split(' ')) {
        if (id) {
          document.getElementById(id)?.setAttribute('hidden', '');
        }
      }
    }
    twin.report('OnImageStatus', { status });
  };

  const applyLiveRegion = () => {
    if (options.liveRegion) {
      unmarkLiveRegion ??= markLiveRegion(element);
    } else if (unmarkLiveRegion) {
      unmarkLiveRegion();
      unmarkLiveRegion = null;
    }
  };

  // The element's own load and error events set the status in C#, which this module does not see: a derivation after a
  // source change reports what it finds even when that equals the last report, and C# drops an unchanged status.
  const deriveFromElement = () => {
    const status = deriveImageStatus(element);
    if (status !== null) {
      lastStatus = null;
      reportStatus(status);
    }
  };

  const stopSourceObserver = () => {
    stopObservingSource?.();
    stopObservingSource = null;
  };

  // A render that changes the source of a kept-mounted image (a parameter, or a substitute element's own `src`)
  // re-derives the status in the batch that applies it, before paint and before `update` arrives.
  const observeSource = () => {
    const MutationObserverImpl = ownerWindow(element).MutationObserver;
    if (stopObservingSource || typeof MutationObserverImpl !== 'function') {
      return;
    }
    const observer = new MutationObserverImpl(() => {
      if (!disposed) {
        deriveFromElement();
      }
    });
    observer.observe(element, { attributes: true, attributeFilter: IMAGE_SOURCE_ATTRIBUTES });
    stopObservingSource = () => observer.disconnect();
  };

  const applyImage = () => {
    const image = options.image;
    if (!image?.keepMounted) {
      stopSourceObserver();
    }
    const key = imageKey(image);
    if (key === evaluatedImage) {
      return;
    }
    const wasKeptMounted = evaluatedKeptMounted;
    evaluatedImage = key;
    evaluatedKeptMounted = image?.keepMounted === true;
    supersedeProbe();
    supersedeProbe = noop;
    if (!image) {
      return;
    }
    if (image.keepMounted) {
      // The observer has already derived the status for a source change C# rendered; only the first evaluation of a
      // kept-mounted image derives here.
      if (!wasKeptMounted) {
        deriveFromElement();
      }
      observeSource();
      return;
    }
    supersedeProbe = probeImage(image, reportStatus);
  };

  const apply = () => {
    assertChecked();
    bindReset();
    assertIndeterminate();
    applyLiveRegion();
    applyImage();
  };

  element.addEventListener('click', onClick);
  apply();

  return {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<DomOptions>(nextOptionsJson);
      apply();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      element.removeEventListener('click', onClick);
      bindReset();
      unmarkLiveRegion?.();
      unmarkLiveRegion = null;
      supersedeProbe();
      supersedeProbe = noop;
      stopSourceObserver();
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// Visibility (CMP-ALG-03; floating-ui-react/utils/composite.ts), moved from `composite` (design point 6 (a) of M5 track 1,
// settled by the orchestrator 2026-11-04) so `composite` and `focus-manager` share it without an import cycle.
// ---------------------------------------------------------------------------------------------------------------

// CMP-ALG-03
/**
 * Determines whether computed styles hide an element through `visibility`.
 * @param styles The element's computed styles.
 * @returns `true` for `visibility: hidden` and `visibility: collapse`.
 */
export function isHiddenByStyles(styles: CSSStyleDeclaration): boolean {
  return styles.visibility === 'hidden' || styles.visibility === 'collapse';
}

// CMP-ALG-03
/**
 * Determines whether an element is rendered and visible.
 * @param element The element, or `null`.
 * @param styles The element's computed styles; read from the element when omitted.
 * @returns `false` for a missing or disconnected element and for one hidden by its styles; otherwise
 * `checkVisibility()` where it exists, else whether `display` is neither `none` nor `contents`.
 */
export function isElementVisible(
  element: Element | null | undefined,
  styles: CSSStyleDeclaration | null = element ? getComputedStyle(element) : null,
): boolean {
  if (!element || !element.isConnected || !styles || isHiddenByStyles(styles)) {
    return false;
  }

  if (typeof element.checkVisibility === 'function') {
    return element.checkVisibility();
  }

  return styles.display !== 'none' && styles.display !== 'contents';
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

// Issue 242 (design point 4 (b) of M6 track 10, chosen by the user 2026-11-24; PSH-ALG-21, MBR-DOM-08): Base UI computes the
// backdrop's cut-out during render (InternalBackdrop.tsx:13-16). The port's backdrop names its cut-out element in
// `data-iodine-cutout`, and one document-level MutationObserver cuts it in the microtask after the batch inserted it,
// before any hit test and before its positioner's handle attaches; the positioner's own cut then follows each pass.
const CUTOUT_ATTRIBUTE = 'data-iodine-cutout';
const cutoutObservers = new WeakMap<Document, MutationObserver>();

function cutAtInsertion(backdrop: HTMLElement): void {
  const id = backdrop.getAttribute(CUTOUT_ATTRIBUTE);
  if (id === null || id === '') {
    return;
  }
  const root = backdrop.getRootNode();
  const target = (isShadowRoot(root) ? root.getElementById(id) : null) ?? ownerDocument(backdrop).getElementById(id);
  if (target !== null) {
    styleWriter(backdrop).layer('positioning-measured').set('clip-path', cutoutPolygon(target.getBoundingClientRect()));
  }
}

// ADR 16 (the observer sees every insertion on the page): an added element is the backdrop itself, or holds at most one
// (a portal's subtree), so a leaf is checked by its attribute alone and a subtree by its first match.
function cutInserted(node: Node): void {
  if (!(node instanceof HTMLElement)) {
    return;
  }
  if (node.hasAttribute(CUTOUT_ATTRIBUTE)) {
    cutAtInsertion(node);
    return;
  }
  if (node.firstElementChild === null) {
    return;
  }
  const backdrop = node.querySelector<HTMLElement>(`[${CUTOUT_ATTRIBUTE}]`);
  if (backdrop !== null) {
    cutAtInsertion(backdrop);
  }
}

// Issue 363 (settled by the orchestrator 2026-11-25): the `popup` module installs the observer at its first attach, a round
// trip after the first popup's render on Server, so a menu trigger's handle installs it as well; the first menu of a page
// is then cut at insertion too.
/**
 * Installs the document's backdrop observer once, and cuts the backdrops already in it.
 * @param doc The document.
 */
export function observeBackdropInsertions(doc: Document): void {
  if (cutoutObservers.has(doc)) {
    return;
  }
  const observer = new MutationObserver((records) => {
    for (const record of records) {
      for (const node of record.addedNodes) {
        cutInserted(node);
      }
    }
  });
  observer.observe(doc.documentElement, { childList: true, subtree: true });
  cutoutObservers.set(doc, observer);
  cutInserted(doc.documentElement);
}
