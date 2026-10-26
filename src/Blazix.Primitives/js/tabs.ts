// The `tabs` component module (ADR 04 §1's component row; §2's `Tabs.Indicator` row; Amendments 2026-10-26, chosen by the
// user 2026-10-26; contract docs/components/tabs.md). Two kinds of handle, one per part that owns a twin:
// - The List's handle: the Tabs' click and focus (TAB-PTR-01..07, TAB-KEY-10), the pressing state of TAB-PTR-05/06, the
//   two Tab rects measured on activation (TAB-STA-15, TAB-I18N-02) and the decision point `select` (fallback run),
//   `data-activation-direction` on the Root, the List, the Tabs, the Panels and the Indicators from attach (written when
//   the applied batch moves `[data-active]`, TAB-STA-14..18), and the `ResizeObserver` subscriptions (through `dom`) its
//   Indicators re-measure on (TAB-DOM-30/33, TAB-EDGE-08, TAB-EDGE-11).
// - The Indicator's handle: the measurement of TAB-DOM-25..29, `hidden` (TAB-DOM-23) and the element's whole `style`
//   (the six `--active-tab-*` variables under the consumer's string, TAB-DOM-24/32), one report per change.
// Transcribed from Base UI v1.9.0: tabs/root/TabsRoot.tsx:374-406 (computeActivationDirection), tabs/tab/TabsTab.tsx:
// 119-177 (the activation and the pressing state), tabs/list/TabsList.tsx:37-82 (the observer), tabs/indicator/
// TabsIndicator.tsx:58-137,204-290 (the measurement).
// Port points pending the user (design points of the Tabs track, 2026-10-26): the module finds its parts by ownership
// rather than by id (4); it reports a click or focus that activates nothing only while a consumer handler exists (5); it
// hands the Tab and the direction over with the synchronous `OnSelecting` before `select`, probing the mode itself (6);
// a change it did not start takes `none` in an uncontrolled root and the positional direction in a controlled one (7).
import {
  getCssDimensions,
  getElementTransform,
  getTarget,
  isElement,
  isHTMLElement,
  observeResize,
  ownerDocument,
  ownerWindow,
} from './dom';
import { decide } from './decide';
import { eventInfo } from './event-info';
import { createTwinChannel, parseOptions, resolveElement, type DotNetTwin, type TwinHandle, type TwinOptions } from './twin';

// ---------------------------------------------------------------------------------------------------------------
// Constants and types
// ---------------------------------------------------------------------------------------------------------------

/** The name of the decision point asked before the module activates the Tab a gesture points at. */
export const SELECT_POINT = 'select';

/** The part literal of a List's handle. */
export const LIST_PART = 'list';

/** The part literal of an Indicator's handle. */
export const INDICATOR_PART = 'indicator';

/** The attribute a Tab carries while it is the active one. */
export const ACTIVE_ATTRIBUTE = 'data-active';

/** The attribute the module writes on every part. */
export const DIRECTION_ATTRIBUTE = 'data-activation-direction';

// The tolerance for offsetLeft/offsetTop rounding when comparing the two offsets (tabs/indicator/TabsIndicator.tsx:27).
const MAX_LAYOUT_ROUNDING_ERROR = 2;

/** The direction from the previously active Tab to the active one. */
export type ActivationDirection = 'left' | 'right' | 'up' | 'down' | 'none';

/** The axis along which the Tabs are laid out. */
export type TabsOrientation = 'horizontal' | 'vertical';

/** The options of a `tabs` handle; mirrors `TabsOptions` in C#. */
export interface TabsOptions extends TwinOptions {
  /** `list` or `indicator`. */
  part: typeof LIST_PART | typeof INDICATOR_PART;
  /** The axis of the activation direction. */
  orientation: TabsOrientation;
  /** Whether a Tab that receives focus is activated. */
  activateOnFocus: boolean;
  /** Whether the consumer owns the Root's value. */
  controlled: boolean;
  /** Whether a click on an enabled Tab that activates nothing is reported. */
  reportClicks: boolean;
  /** Whether a focus on a Tab that activates nothing is reported. */
  reportFocus: boolean;
  /** The consumer's `hidden` on an Indicator, which the module never writes over; `null` when it writes its own. */
  hidden?: boolean | null;
}

/** The offsets of the active Tab from the List's edges, in CSS pixels. */
export interface TabPosition {
  left: number;
  right: number;
  top: number;
  bottom: number;
}

/** The size of the active Tab, in CSS pixels. */
export interface TabSize {
  width: number;
  height: number;
}

/** What an Indicator measures, or `null` while no active Tab is measurable. */
export interface IndicatorMeasurement {
  position: TabPosition;
  size: TabSize;
}

// ---------------------------------------------------------------------------------------------------------------
// The activation direction (TAB-STA-15, TAB-I18N-02)
// ---------------------------------------------------------------------------------------------------------------

/**
 * Computes the direction from one Tab to another from their on-screen positions: `left` against `left` when
 * horizontal, `top` against `top` when vertical. Positions are visual, so under rtl the next Tab in reading order is
 * `left`.
 * @param from The previously active Tab, or `null`.
 * @param to The newly active Tab, or `null`.
 * @param orientation The List's orientation.
 * @returns `none` when either is missing or the positions are equal.
 */
export function computeActivationDirection(from: Element | null, to: Element | null, orientation: TabsOrientation): ActivationDirection {
  if (from === null || to === null) {
    return 'none';
  }
  const horizontal = orientation === 'horizontal';
  const side = horizontal ? 'left' : 'top';
  const previous = from.getBoundingClientRect()[side];
  const next = to.getBoundingClientRect()[side];
  if (next < previous) {
    return horizontal ? 'left' : 'up';
  }
  if (next > previous) {
    return horizontal ? 'right' : 'down';
  }
  return 'none';
}

// ---------------------------------------------------------------------------------------------------------------
// The Indicator's measurement (TAB-DOM-25..29, TAB-EDGE-14)
// ---------------------------------------------------------------------------------------------------------------

function isShadowRoot(value: unknown): value is ShadowRoot {
  return typeof ShadowRoot !== 'undefined' && value instanceof ShadowRoot;
}

// The composed-tree parent (Floating UI's getParentNode, which `dom` keeps private): a slot, then the parent, then a
// shadow root's host.
function composedParent(node: Node): Node | null {
  if ((node as Element).assignedSlot) {
    return (node as Element).assignedSlot;
  }
  if (node.parentNode) {
    return isShadowRoot(node.parentNode) ? node.parentNode.host : node.parentNode;
  }
  return isShadowRoot(node) ? node.host : null;
}

function isLastTraversableNode(node: Node): boolean {
  return node.nodeName === 'HTML' || node.nodeName === 'BODY' || node.nodeName === '#document';
}

function getCumulativeOffset(element: HTMLElement): { left: number; top: number } {
  let left = 0;
  let top = 0;
  let current: HTMLElement | null = element;
  while (current !== null) {
    left += current.offsetLeft;
    top += current.offsetTop;
    const offsetParent = current.offsetParent as HTMLElement | null;
    if (offsetParent !== null) {
      left += offsetParent.clientLeft;
      top += offsetParent.clientTop;
    }
    current = offsetParent;
  }
  return { left, top };
}

/**
 * The transform-immune layout offset of a Tab inside its List (TAB-DOM-26): the cumulative offsets, less the List's
 * border, less the scroll of every container between the two (the List's own scroll excluded).
 * @param tab The active Tab.
 * @param list The List.
 * @returns The offset in CSS pixels.
 */
export function getLayoutOffset(tab: HTMLElement, list: HTMLElement): { left: number; top: number } {
  const tabOffset = getCumulativeOffset(tab);
  const listOffset = getCumulativeOffset(list);
  let left = tabOffset.left - listOffset.left - list.clientLeft;
  let top = tabOffset.top - listOffset.top - list.clientTop;
  let node = composedParent(tab);
  while (isHTMLElement(node) && node !== list && !isLastTraversableNode(node)) {
    left -= node.scrollLeft;
    top -= node.scrollTop;
    node = composedParent(node);
  }
  return { left, top };
}

/**
 * Resolves one component of the `translate` longhand to pixels (TAB-DOM-29): a number, or a percentage of the reference
 * size; anything else (`calc(...)`) or nothing is 0.
 * @param value The component, or `undefined`.
 * @param referenceSize The Tab's border-box size on that axis.
 * @returns The translation in CSS pixels.
 */
export function resolveTranslateLength(value: string | undefined, referenceSize: number): number {
  if (!value) {
    return 0;
  }
  const numeric = parseFloat(value);
  if (!Number.isFinite(numeric)) {
    return 0;
  }
  return value.endsWith('%') ? (numeric / 100) * referenceSize : numeric;
}

/**
 * The active Tab's own 2D translation (TAB-DOM-29): the computed `transform` matrix's translation plus the `translate`
 * longhand.
 * @param tab The active Tab.
 * @returns The translation in CSS pixels.
 */
export function getActiveTabTranslation(tab: HTMLElement): { x: number; y: number } {
  const computedStyle = ownerWindow(tab).getComputedStyle(tab);
  const { x, y } = getElementTransform(tab, computedStyle);
  let translateX = x;
  let translateY = y;
  const { translate } = computedStyle;
  if (translate && translate !== 'none') {
    const parts = translate.split(' ');
    translateX += resolveTranslateLength(parts[0], tab.offsetWidth);
    translateY += resolveTranslateLength(parts[1], tab.offsetHeight);
  }
  return { x: translateX, y: translateY };
}

/**
 * Measures the active Tab against its List (TAB-DOM-25..29): the CSS size, the sub-pixel rect offset when it agrees with
 * the layout offset within 2 px once the Tab's own translation is removed, else the layout offset; `right` and `bottom`
 * against the List's scrollable content.
 * @param tab The active Tab.
 * @param list The List.
 * @returns The position and size in CSS pixels, unrounded.
 */
export function measureActiveTab(tab: HTMLElement, list: HTMLElement): IndicatorMeasurement {
  const { width, height } = getCssDimensions(tab);
  const { width: listWidth, height: listHeight } = getCssDimensions(list);
  const tabRect = tab.getBoundingClientRect();
  const listRect = list.getBoundingClientRect();
  const scaleX = listWidth > 0 ? listRect.width / listWidth : 1;
  const scaleY = listHeight > 0 ? listRect.height / listHeight : 1;

  const layout = getLayoutOffset(tab, list);
  let left = layout.left;
  let top = layout.top;

  const rectLeft = (tabRect.left - listRect.left) / scaleX + list.scrollLeft - list.clientLeft;
  const rectTop = (tabRect.top - listRect.top) / scaleY + list.scrollTop - list.clientTop;

  // A scale of zero divides to NaN or Infinity and fails the comparison, which keeps the layout offset (TAB-EDGE-14).
  const translation = getActiveTabTranslation(tab);
  if (
    Math.abs(rectLeft - translation.x - left) <= MAX_LAYOUT_ROUNDING_ERROR &&
    Math.abs(rectTop - translation.y - top) <= MAX_LAYOUT_ROUNDING_ERROR
  ) {
    left = rectLeft;
    top = rectTop;
  }

  return {
    position: { left, right: list.scrollWidth - left - width, top, bottom: list.scrollHeight - top - height },
    size: { width, height },
  };
}

/**
 * Composes an Indicator's whole inline style (TAB-DOM-24/32): the six variables, then the consumer's string, whose
 * declarations win.
 * @param measurement The measurement, or `null` for no variables.
 * @param consumerStyle The consumer's merged style string.
 * @returns The `cssText` to write.
 */
export function composeIndicatorStyle(measurement: IndicatorMeasurement | null, consumerStyle: string | null | undefined): string {
  const consumer = consumerStyle ?? '';
  if (measurement === null) {
    return consumer;
  }
  const { position, size } = measurement;
  const variables =
    `--active-tab-left: ${position.left}px; --active-tab-right: ${position.right}px; ` +
    `--active-tab-top: ${position.top}px; --active-tab-bottom: ${position.bottom}px; ` +
    `--active-tab-width: ${size.width}px; --active-tab-height: ${size.height}px;`;
  return consumer === '' ? variables : `${variables} ${consumer}`;
}

function sameMeasurement(a: IndicatorMeasurement | null, b: IndicatorMeasurement | null): boolean {
  if (a === null || b === null) {
    return a === b;
  }
  return (
    Object.is(a.position.left, b.position.left) &&
    Object.is(a.position.right, b.position.right) &&
    Object.is(a.position.top, b.position.top) &&
    Object.is(a.position.bottom, b.position.bottom) &&
    Object.is(a.size.width, b.size.width) &&
    Object.is(a.size.height, b.size.height)
  );
}

// ---------------------------------------------------------------------------------------------------------------
// Ownership (design point 4 of the Tabs track, pending the user)
// ---------------------------------------------------------------------------------------------------------------

// Symbols on the elements, not a module registry: a repeated attach of the same element returns the live handle, and a
// part finds its List by walking up to the element that carries a List's handle.
const listHandle = Symbol('blazix.tabs.list');
const indicatorHandle = Symbol('blazix.tabs.indicator');

interface ListController {
  readonly element: HTMLElement;
  readonly handle: TwinHandle;
  activeTab(): HTMLElement | null;
  addIndicator(indicator: IndicatorController): void;
  removeIndicator(indicator: IndicatorController): void;
}

interface IndicatorController {
  readonly element: HTMLElement;
  readonly handle: TwinHandle;
  setList(list: ListController | null): void;
  measure(): void;
}

interface TabsElement extends HTMLElement {
  [listHandle]?: ListController;
  [indicatorHandle]?: IndicatorController;
}

function isOtherList(node: Element, list: HTMLElement): boolean {
  return node !== list && ((node as TabsElement)[listHandle] !== undefined || node.getAttribute('role') === 'tablist');
}

/**
 * Finds the Tab of a List an event target lies in: the nearest `[role="tab"]` on the way up to the List, unless a nested
 * List (another List's handle, or another `tablist`) comes first.
 * @param list The List.
 * @param target The event's target.
 * @returns The Tab, or `null`.
 */
export function findOwnTab(list: HTMLElement, target: EventTarget | null): HTMLElement | null {
  let node: Node | null = isElement(target) ? target : ((target as Node | null)?.parentNode ?? null);
  let tab: HTMLElement | null = null;
  while (node !== null && node !== list) {
    if (isElement(node)) {
      if (isOtherList(node, list)) {
        return null;
      }
      if (tab === null && isHTMLElement(node) && node.getAttribute('role') === 'tab') {
        tab = node;
      }
    }
    node = composedParent(node);
  }
  return node === list ? tab : null;
}

/**
 * The Tabs of a List in document order, a nested List's excluded.
 * @param list The List.
 * @returns The Tabs.
 */
export function findOwnTabs(list: HTMLElement): HTMLElement[] {
  return Array.from(list.querySelectorAll<HTMLElement>('[role="tab"]')).filter((tab) => findOwnTab(list, tab) === tab);
}

/**
 * The active Tab of a List (design point 4 (b)): the first `[data-active]` Tab the List owns.
 * @param list The List.
 * @returns The Tab, or `null`.
 */
export function findActiveTab(list: HTMLElement): HTMLElement | null {
  for (const candidate of list.querySelectorAll<HTMLElement>(`[${ACTIVE_ATTRIBUTE}]`)) {
    if (findOwnTab(list, candidate) === candidate) {
      return candidate;
    }
  }
  return null;
}

// The nearest composed ancestor (the element itself excluded) that carries the direction attribute every part renders.
function nearestPartAncestor(element: Element): HTMLElement | null {
  let node = composedParent(element);
  while (node !== null) {
    if (isHTMLElement(node) && node.hasAttribute(DIRECTION_ATTRIBUTE)) {
      return node;
    }
    node = composedParent(node);
  }
  return null;
}

/**
 * The Root of a List: its nearest ancestor that renders `data-activation-direction`.
 * @param list The List.
 * @returns The Root, or `null` when none is found (a substituted Root without the attribute).
 */
export function findRoot(list: HTMLElement): HTMLElement | null {
  return nearestPartAncestor(list);
}

/**
 * The Panels of a Root, a nested Root's excluded: the `[role="tabpanel"]` elements whose nearest part ancestor is the Root.
 * @param root The Root.
 * @returns The Panels.
 */
export function findOwnPanels(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>('[role="tabpanel"]')).filter((panel) => nearestPartAncestor(panel) === root);
}

function findListController(element: Element): ListController | null {
  let node = composedParent(element);
  while (node !== null) {
    const controller = (node as TabsElement)[listHandle];
    if (controller !== undefined) {
      return controller;
    }
    node = composedParent(node);
  }
  return null;
}

function isDisabledTab(tab: HTMLElement): boolean {
  return tab.hasAttribute('data-disabled') || tab.getAttribute('aria-disabled') === 'true';
}

function isActiveTab(tab: HTMLElement): boolean {
  return tab.hasAttribute(ACTIVE_ATTRIBUTE);
}

// ---------------------------------------------------------------------------------------------------------------
// The List's handle
// ---------------------------------------------------------------------------------------------------------------

interface PendingActivation {
  from: HTMLElement | null;
  to: HTMLElement;
  direction: ActivationDirection;
}

function attachList(element: TabsElement, initialOptions: TabsOptions, twinReference: DotNetTwin): TwinHandle {
  const twin = createTwinChannel(twinReference);
  const doc = ownerDocument(element);
  let options = initialOptions;
  let disposed = false;
  let synchronous: boolean | undefined;
  let lastActive: HTMLElement | null = findActiveTab(element);
  let direction: ActivationDirection = 'none';
  let pending: PendingActivation | null = null;
  const presses = new WeakMap<Element, { main: boolean }>();
  const indicators = new Set<IndicatorController>();
  const observed = new Map<Element, () => void>();

  // Design point 6 (pending the user): the mode, probed once per handle as the channel does, so the activation can be
  // handed over before `select` is asked.
  const isSynchronous = (): boolean => {
    if (synchronous === undefined) {
      try {
        synchronous = twinReference.invokeMethod<boolean>('Ping') === true;
      } catch {
        synchronous = false;
      }
    }
    return synchronous;
  };

  const report = (tab: HTMLElement, event: Event) => twin.report('OnTabEvent', { tabId: tab.id, event: eventInfo(event) });

  // TAB-STA-03, TAB-EVT-02: the direction from the active Tab to the one the gesture points at, measured in the dispatch,
  // then `select` (fallback run). On WebAssembly C# runs the consumer's handler and the request inside it; elsewhere the
  // activation is reported and C# runs them a round trip later.
  const activate = (tab: HTMLElement, event: Event) => {
    const from = findActiveTab(element);
    const record: PendingActivation = { from, to: tab, direction: computeActivationDirection(from, tab, options.orientation) };
    pending = record;
    if (isSynchronous()) {
      twinReference.invokeMethod<void>('OnSelecting', JSON.stringify({ tabId: tab.id, direction: record.direction }));
      if (decide(twin, SELECT_POINT, event, 'run') !== 'run' && pending === record) {
        // A consumer handler vetoed it: nothing changes (TAB-STA-18).
        pending = null;
      }
      return;
    }
    decide(twin, SELECT_POINT, event, 'run');
    twin.report('OnSelect', { tabId: tab.id, direction: record.direction, event: eventInfo(event) });
  };

  // TAB-PTR-01..04, TAB-PTR-09: an enabled Tab's click activates it unless it is active; a secondary or middle click is
  // ignored (TAB-PTR-04, §16 Q12: React's filter, made explicit); a disabled Tab's click reaches nobody (UBT-PTR-01).
  const onClick = (event: MouseEvent) => {
    const tab = findOwnTab(element, getTarget(event));
    if (tab === null || event.button !== 0 || isDisabledTab(tab)) {
      return;
    }
    if (isActiveTab(tab)) {
      if (options.reportClicks) {
        report(tab, event);
      }
      return;
    }
    activate(tab, event);
  };

  // TAB-KEY-10, TAB-PTR-05/06: with activateOnFocus, a focus activates an enabled, inactive Tab unless a non-primary
  // press of that Tab is held. The consumer's onfocus runs for every focus, a disabled Tab's included.
  const onFocusIn = (event: FocusEvent) => {
    const tab = findOwnTab(element, getTarget(event));
    if (tab === null) {
      return;
    }
    const press = presses.get(tab);
    if (options.activateOnFocus && !isDisabledTab(tab) && !isActiveTab(tab) && (press === undefined || press.main)) {
      activate(tab, event);
      return;
    }
    if (options.reportFocus) {
      report(tab, event);
    }
  };

  // TAB-PTR-05..07: a press on an enabled, inactive Tab records whether it is the main button until the next pointerup or
  // pointercancel anywhere in the document.
  const onPointerDown = (event: PointerEvent) => {
    const tab = findOwnTab(element, getTarget(event));
    if (tab === null || isDisabledTab(tab) || isActiveTab(tab)) {
      return;
    }
    presses.set(tab, { main: event.button === 0 });
    const end = () => {
      presses.delete(tab);
      doc.removeEventListener('pointerup', end);
      doc.removeEventListener('pointercancel', end);
    };
    doc.addEventListener('pointerup', end);
    doc.addEventListener('pointercancel', end);
  };

  // TAB-STA-14..18 and design point 7 (pending the user): the direction of a change of the active Tab. A change the module
  // started keeps the direction measured in its dispatch; any other is `none` in an uncontrolled Root (an automatic
  // fallback) and positional in a controlled one (a programmatic change); `none` when either side is empty or the old
  // Tab has left the document.
  const directionOf = (from: HTMLElement | null, to: HTMLElement | null): ActivationDirection => {
    if (from === null || to === null) {
      return 'none';
    }
    if (pending !== null && pending.to === to && pending.from === from) {
      return pending.direction;
    }
    if (!options.controlled || !from.isConnected) {
      return 'none';
    }
    return computeActivationDirection(from, to, options.orientation);
  };

  const parts = (): HTMLElement[] => {
    const root = findRoot(element);
    const found: HTMLElement[] = [element, ...findOwnTabs(element)];
    if (root !== null) {
      found.push(root, ...findOwnPanels(root));
    }
    for (const indicator of indicators) {
      found.push(indicator.element);
    }
    return found;
  };

  const writeDirection = () => {
    for (const part of parts()) {
      if (part.getAttribute(DIRECTION_ATTRIBUTE) !== direction) {
        part.setAttribute(DIRECTION_ATTRIBUTE, direction);
      }
    }
  };

  const onResize = () => {
    for (const indicator of indicators) {
      indicator.measure();
    }
  };

  // TAB-DOM-30, TAB-EDGE-11: the List and every Tab it owns, the ones mounted later and a swapped element included.
  const syncObserved = () => {
    const wanted = new Set<Element>([element, ...findOwnTabs(element)]);
    for (const [target, stop] of observed) {
      if (!wanted.has(target)) {
        stop();
        observed.delete(target);
      }
    }
    for (const target of wanted) {
      if (!observed.has(target)) {
        observed.set(target, observeResize(target, onResize));
      }
    }
  };

  // The applied batch (ADR 04 Amendments 2026-10-26, the openSelector precedent): pre-paint on both modes.
  const onBatch = () => {
    if (disposed) {
      return;
    }
    const active = findActiveTab(element);
    if (active !== lastActive) {
      const next = directionOf(lastActive, active);
      lastActive = active;
      pending = null;
      if (next !== direction) {
        direction = next;
        twin.report('OnDirectionChange', { direction });
      }
    }
    writeDirection();
    syncObserved();
    for (const indicator of indicators) {
      indicator.measure();
    }
  };

  const observer = new MutationObserver(onBatch);
  const observeBatches = () => {
    observer.disconnect();
    observer.observe(findRoot(element) ?? element, {
      subtree: true,
      childList: true,
      attributes: true,
      attributeFilter: [ACTIVE_ATTRIBUTE, 'role', 'data-disabled'],
    });
  };

  const controller: ListController = {
    element,
    handle: undefined as unknown as TwinHandle,
    activeTab: () => (disposed ? null : findActiveTab(element)),
    addIndicator(indicator) {
      indicators.add(indicator);
      if (indicator.element.getAttribute(DIRECTION_ATTRIBUTE) !== direction) {
        indicator.element.setAttribute(DIRECTION_ATTRIBUTE, direction);
      }
    },
    removeIndicator(indicator) {
      indicators.delete(indicator);
    },
  };

  const handle: TwinHandle = {
    update(optionsJson: string) {
      if (!disposed) {
        options = parseOptions<TabsOptions>(optionsJson);
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete element[listHandle];
      observer.disconnect();
      element.removeEventListener('click', onClick);
      element.removeEventListener('focusin', onFocusIn);
      element.removeEventListener('pointerdown', onPointerDown);
      for (const stop of observed.values()) {
        stop();
      }
      observed.clear();
      for (const indicator of indicators) {
        indicator.setList(null);
      }
      indicators.clear();
    },
  };
  (controller as { handle: TwinHandle }).handle = handle;
  element[listHandle] = controller;

  element.addEventListener('click', onClick);
  element.addEventListener('focusin', onFocusIn);
  element.addEventListener('pointerdown', onPointerDown);
  observeBatches();
  syncObserved();
  writeDirection();

  // Indicators attached before this List adopt it now.
  for (const candidate of element.querySelectorAll('*')) {
    const indicator = (candidate as TabsElement)[indicatorHandle];
    if (indicator !== undefined && findListController(candidate) === controller) {
      indicator.setList(controller);
    }
  }

  return handle;
}

// ---------------------------------------------------------------------------------------------------------------
// The Indicator's handle
// ---------------------------------------------------------------------------------------------------------------

function attachIndicator(element: TabsElement, initialOptions: TabsOptions, twinReference: DotNetTwin): TwinHandle {
  const twin = createTwinChannel(twinReference);
  let options = initialOptions;
  let disposed = false;
  let list: ListController | null = null;
  let reported: IndicatorMeasurement | null = null;
  let writtenStyle: string | null = null;

  // TAB-DOM-20/23/24: measured when the List and an active Tab exist; hidden unless both sizes are positive, unless the
  // consumer set `hidden`. Scroll alone measures nothing (TAB-DOM-30, §16 Q11).
  const measure = () => {
    if (disposed) {
      return;
    }
    const tab = list?.activeTab() ?? null;
    const measurement = list !== null && tab !== null ? measureActiveTab(tab, list.element) : null;
    const style = composeIndicatorStyle(measurement, options.style);
    if (style !== writtenStyle) {
      writtenStyle = style;
      element.style.cssText = style;
    }
    const hidden = options.hidden ?? !(measurement !== null && measurement.size.width > 0 && measurement.size.height > 0);
    if (element.hasAttribute('hidden') !== hidden) {
      element.toggleAttribute('hidden', hidden);
    }
    if (!sameMeasurement(measurement, reported)) {
      reported = measurement;
      twin.report('OnMeasure', measurement === null ? { position: null, size: null } : measurement);
    }
  };

  const controller: IndicatorController = {
    element,
    handle: undefined as unknown as TwinHandle,
    setList(next) {
      if (next === list) {
        return;
      }
      list?.removeIndicator(controller);
      list = next;
      list?.addIndicator(controller);
      measure();
    },
    measure,
  };

  const handle: TwinHandle = {
    update(optionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<TabsOptions>(optionsJson);
      measure();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete element[indicatorHandle];
      list?.removeIndicator(controller);
      list = null;
    },
  };
  (controller as { handle: TwinHandle }).handle = handle;
  element[indicatorHandle] = controller;

  const found = findListController(element);
  if (found !== null) {
    controller.setList(found);
  } else {
    measure();
  }
  return handle;
}

// ---------------------------------------------------------------------------------------------------------------
// The twin-facing entry
// ---------------------------------------------------------------------------------------------------------------

/**
 * Attaches a List's or an Indicator's handle. A List's handle owns the activation of its Tabs (the decision point
 * `select`; `OnSelect`, `OnTabEvent`), `data-activation-direction` on every part (`OnDirectionChange`) and the resize
 * observation; an Indicator's handle owns its `hidden` and `style` (`OnMeasure`).
 * @param elementOrId The element, or its id when the part has no element reference.
 * @param optionsJson The `TabsOptions` JSON.
 * @param twinReference The twin's object reference.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin): TwinHandle {
  const element = resolveElement(elementOrId) as TabsElement;
  const options = parseOptions<TabsOptions>(optionsJson);
  if (options.part === INDICATOR_PART) {
    return element[indicatorHandle]?.handle ?? attachIndicator(element, options, twinReference);
  }
  return element[listHandle]?.handle ?? attachList(element, options, twinReference);
}

