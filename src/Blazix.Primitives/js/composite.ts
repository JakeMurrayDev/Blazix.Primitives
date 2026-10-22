// The `composite` mechanism module (ADR 04 §1 composite row; contract docs/architecture/composite-and-list-navigation.md,
// CMP): the roving group. M2 ships what RadioGroup reaches (ADR 12 Amendments 2026-10-22 item 3): the index helpers
// (CMP-ALG-01..06, 15, 16, 19), the item registry with move detection (CMP-ALG-17, CMP-STA-01..18), the root's key
// resolution without the grid branch (CMP-ALG-14), the publication adjustments (CMP-ALG-18, CMP-STA-22..30), the
// pointer and focus handling of the items (CMP-PTR-01/02, CMP-KEY-17), and RadioGroup's options (the pending-navigation
// flag, the focus-driven selection and the leaving blur; ADR 04 Amendments 2026-10-20, RDG-STA-06, RAD-FOC-05,
// RDG-FOC-04/05). Not built: grid, `onLoop`, the keyed registry (CMP §10 Q1), the keyboard relay (CMP-KEY-14), a
// consumer-owned highlight and `onMapChange` (M3), list navigation and typeahead (M5), labels and `guess` (M5).
// Transcribed from Base UI v1.9.0: internals/composite/{composite,constants}.ts, root/useCompositeRoot.ts,
// root/CompositeRoot.tsx, item/useCompositeItem.ts, list/CompositeList.tsx, list/useCompositeListItem.ts;
// floating-ui-react/utils/composite.ts; radio-group/RadioGroup.tsx:205-245 and radio/root/RadioRoot.tsx:156-166.
// From `attach` the module is the only writer of every item's `tabindex` (ADR 04 Amendments 2026-10-22): C# renders
// "-1" (CMP-STA-15, the handover at attach) and the module writes "0" or "-1" in the task that moves the highlight
// (CMP-DOM-01), except on an item whose registration carries the consumer's `tabindex` (RAD-DOM-16).
// Decision points (ADR 04 §3, fallback run for both; names chosen by the user 2026-10-22, ADR 04 Amendments 2026-10-22):
// `navigate`, the consumer's onKeyDown veto against key handling (EVD-EVT-26, CMP-EVT-12, RDG-API-12), and `select`,
// the focus-driven selection (ADR 04 Amendments 2026-10-20).
import { dispatchClickWithModifiers, isElementDisabled } from './button';
import { decide } from './decide';
import { contains, getTarget, isElement, isHTMLElement, ownerDocument } from './dom';
import { eventInfo } from './event-info';
import { createTwinChannel, parseOptions, resolveElement, type DotNetTwin, type TwinHandle, type TwinOptions } from './twin';

// ---------------------------------------------------------------------------------------------------------------
// Constants (CMP §6.11)
// ---------------------------------------------------------------------------------------------------------------

/** The attribute a component puts on the item that takes the initial tab stop, such as the checked radio. */
export const ACTIVE_COMPOSITE_ITEM = 'data-composite-item-active';

/** The name of the decision point asked before the module handles a navigation key. */
export const NAVIGATE_POINT = 'navigate';

/** The name of the decision point asked before the module selects a focused item by clicking its input. */
export const SELECT_POINT = 'select';

const ARROW_UP = 'ArrowUp';
const ARROW_DOWN = 'ArrowDown';
const ARROW_LEFT = 'ArrowLeft';
const ARROW_RIGHT = 'ArrowRight';
const HOME = 'Home';
const END = 'End';

/** The keys the root handles: the four arrows, and Home and End when enabled. */
export const COMPOSITE_KEYS: ReadonlySet<string> = new Set([ARROW_UP, ARROW_DOWN, ARROW_LEFT, ARROW_RIGHT, HOME, END]);

/** The modifier keys that block navigation unless allowed. */
export const MODIFIER_KEYS = ['Shift', 'Control', 'Alt', 'Meta'] as const;

/** One of the modifier keys that block navigation unless allowed. */
export type ModifierKey = (typeof MODIFIER_KEYS)[number];

/** The arrow keys that move the highlight. */
export type Orientation = 'horizontal' | 'vertical' | 'both';

/** The reading direction, which swaps the horizontal arrow keys. */
export type TextDirection = 'ltr' | 'rtl';

/** The indices navigation skips: an array, or a predicate over the index. */
export type DisabledIndices = ReadonlyArray<number> | ((index: number) => boolean);

const NO_MODIFIERS = { shiftKey: false, ctrlKey: false, altKey: false, metaKey: false };

// ---------------------------------------------------------------------------------------------------------------
// Index helpers (CMP §5.1, ALG-01..06; floating-ui-react/utils/composite.ts)
// ---------------------------------------------------------------------------------------------------------------

// CMP-ALG-01
/**
 * Determines whether an index lies outside a list.
 * @param list The elements array.
 * @param index The index.
 * @returns `true` below zero or at the length and beyond.
 */
export function isIndexOutOfListBounds(list: ReadonlyArray<HTMLElement | null | undefined>, index: number): boolean {
  return index < 0 || index >= list.length;
}

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

// CMP-ALG-02, CMP-ARIA-03
/**
 * Determines whether navigation skips the item at an index.
 * @param list The elements array.
 * @param index The index.
 * @param disabledIndices The indices to skip. When absent, the item's `disabled` attribute and `aria-disabled="true"`
 * disable it; when present, only the listed indices do. A hidden or natively disabled item is skipped either way.
 * @returns `true` when navigation skips the item.
 */
export function isListIndexDisabled(
  list: ReadonlyArray<HTMLElement | null | undefined>,
  index: number,
  disabledIndices?: DisabledIndices | null,
): boolean {
  const isExplicitlyDisabled =
    typeof disabledIndices === 'function' ? disabledIndices(index) : (disabledIndices?.includes(index) ?? false);

  if (isExplicitlyDisabled) {
    return true;
  }

  const element = list[index];
  if (!element) {
    return false;
  }

  if (!isElementVisible(element)) {
    return true;
  }

  // A natively disabled element can never receive focus, so it is always skipped, even when `disabledIndices` marks
  // it as enabled. Only `aria-disabled` items can be focusable while disabled.
  if (element.matches(':disabled')) {
    return true;
  }

  return !disabledIndices && (element.hasAttribute('disabled') || element.getAttribute('aria-disabled') === 'true');
}

// CMP-ALG-04
/**
 * Steps through a list from an index until it reaches an item navigation does not skip.
 * @param list The elements array.
 * @param options `startingIndex` (-1 by default), `decrement` (`false`), `disabledIndices`, and `amount` (1), the
 * size of each step.
 * @returns The index reached, which is out of bounds when no such item exists in that direction.
 */
export function findNonDisabledListIndex(
  list: ReadonlyArray<HTMLElement | null | undefined>,
  {
    startingIndex = -1,
    decrement = false,
    disabledIndices,
    amount = 1,
  }: {
    startingIndex?: number | undefined;
    decrement?: boolean | undefined;
    disabledIndices?: DisabledIndices | null | undefined;
    amount?: number | undefined;
  } = {},
): number {
  let index = startingIndex;
  do {
    index += decrement ? -amount : amount;
  } while (index >= 0 && index <= list.length - 1 && isListIndexDisabled(list, index, disabledIndices));

  return index;
}

// CMP-ALG-05
/**
 * Finds the first item navigation does not skip.
 * @param list The elements array.
 * @param disabledIndices The indices to skip.
 * @returns The index, or the length when every item is skipped (0 for an empty list).
 */
export function getMinListIndex(list: ReadonlyArray<HTMLElement | null | undefined>, disabledIndices?: DisabledIndices | null): number {
  return findNonDisabledListIndex(list, { disabledIndices });
}

// CMP-ALG-05
/**
 * Finds the last item navigation does not skip.
 * @param list The elements array.
 * @param disabledIndices The indices to skip.
 * @returns The index, or -1 when every item is skipped or the list is empty.
 */
export function getMaxListIndex(list: ReadonlyArray<HTMLElement | null | undefined>, disabledIndices?: DisabledIndices | null): number {
  return findNonDisabledListIndex(list, {
    decrement: true,
    startingIndex: list.length,
    disabledIndices,
  });
}

/** The options of one step through a list. */
export interface ListStepOptions {
  /** Whether the step moves toward the start of the list. */
  decrement: boolean;
  /** Whether stepping past either end wraps to the other end. */
  loopFocus: boolean;
  /** Whether stepping past either end leaves the list instead of wrapping. */
  allowEscape: boolean;
  /** The indices to skip. */
  disabledIndices?: DisabledIndices | undefined;
  /** The first navigable index. */
  minIndex: number;
  /** The last navigable index. */
  maxIndex: number;
}

// CMP-ALG-06 (implemented early: its first consumer is list navigation, M5; the CMP-ALG-01/04/05 titles call it)
/**
 * Computes the index one arrow step moves the highlight to.
 * @param list The elements array.
 * @param currentIndex The index the step starts from; -1 or the length is outside the list.
 * @param options The step's options.
 * @returns The index, or -1 when the step leaves the list, and whether it wrapped from one end to the other.
 */
export function getNextListIndex(
  list: ReadonlyArray<HTMLElement | null | undefined>,
  currentIndex: number,
  options: ListStepOptions,
): { index: number; wrapped: boolean } {
  const { decrement, loopFocus, allowEscape, disabledIndices, minIndex, maxIndex } = options;
  const step = () => findNonDisabledListIndex(list, { startingIndex: currentIndex, decrement, disabledIndices });

  let index: number;
  let wrapped = false;

  if (!loopFocus) {
    index = decrement ? Math.max(minIndex, step()) : Math.min(maxIndex, step());
  } else if (decrement ? currentIndex <= minIndex : currentIndex >= maxIndex) {
    // Escaping is only possible from inside the list: from outside it, a step enters at the far end.
    const outside = decrement ? -1 : list.length;
    if (allowEscape && currentIndex !== outside) {
      index = -1;
    } else {
      index = decrement ? maxIndex : minIndex;
      wrapped = true;
    }
  } else {
    index = step();
  }

  return { index: isIndexOutOfListBounds(list, index) ? -1 : index, wrapped };
}

// CMP-ALG-16
/**
 * Determines whether a key event holds a modifier that blocks navigation.
 * @param event The key event.
 * @param allowedModifiers The modifiers that do not block navigation.
 * @returns `true` when Shift, Control, Alt or Meta is held and not allowed.
 */
export function isModifierKeySet(event: KeyboardEvent, allowedModifiers: ReadonlyArray<string>): boolean {
  for (const key of MODIFIER_KEYS) {
    if (allowedModifiers.includes(key)) {
      continue;
    }
    if (event.getModifierState(key)) {
      return true;
    }
  }
  return false;
}

// CMP-ALG-15
/**
 * Finds the item that takes the tab stop when the highlighted item is gone: the first enabled item carrying the
 * active marker, else the first enabled item.
 * @param elements The elements array.
 * @param disabledIndices The indices to skip.
 * @returns The index, or 0 when no item can hold the tab stop, so the index stays in range.
 */
export function getFallbackIndex(elements: ReadonlyArray<HTMLElement | null | undefined>, disabledIndices?: DisabledIndices | null): number {
  let fallbackIndex = -1;

  for (let index = 0; index < elements.length; index += 1) {
    const element = elements[index];

    if (!element || isListIndexDisabled(elements, index, disabledIndices)) {
      continue;
    }

    if (element.hasAttribute(ACTIVE_COMPOSITE_ITEM)) {
      return index;
    }

    if (fallbackIndex === -1) {
      fallbackIndex = index;
    }
  }

  return Math.max(fallbackIndex, 0);
}

/**
 * Determines whether an event target is a text field whose caret the arrow keys move.
 * @param element The event target.
 * @returns `true` for an `input` that exposes a selection range and for a `textarea`.
 */
export function isNativeInput(element: EventTarget | null): element is HTMLInputElement | HTMLTextAreaElement {
  if (!isHTMLElement(element)) {
    return false;
  }
  if (element.tagName === 'INPUT' && (element as HTMLInputElement).selectionStart != null) {
    return true;
  }
  return element.tagName === 'TEXTAREA';
}

// CMP-ALG-19, CMP-I18N-06
/**
 * Scrolls a container so that an item inside it is fully visible along the root's orientation, checking the start
 * edge first under right-to-left.
 * @param scrollContainer The scrolling container, or `null`.
 * @param element The item, or `null`.
 * @param direction The reading direction.
 * @param orientation The axes the container scrolls along.
 */
export function scrollIntoViewIfNeeded(
  scrollContainer: HTMLElement | null,
  element: HTMLElement | null,
  direction: TextDirection,
  orientation: Orientation,
): void {
  if (!scrollContainer || !element || !element.scrollTo) {
    return;
  }

  let targetX = scrollContainer.scrollLeft;
  let targetY = scrollContainer.scrollTop;

  const isOverflowingX = scrollContainer.clientWidth < scrollContainer.scrollWidth;
  const isOverflowingY = scrollContainer.clientHeight < scrollContainer.scrollHeight;

  if (isOverflowingX && orientation !== 'vertical') {
    const elementOffsetLeft = getOffset(scrollContainer, element, 'left');
    const containerStyles = getScrollStyles(scrollContainer);
    const elementStyles = getScrollStyles(element);
    const overflowsRight =
      elementOffsetLeft + element.offsetWidth + elementStyles.scrollMarginRight >
      scrollContainer.scrollLeft + scrollContainer.clientWidth - containerStyles.scrollPaddingRight;
    const overflowsLeft =
      elementOffsetLeft - elementStyles.scrollMarginLeft < scrollContainer.scrollLeft + containerStyles.scrollPaddingLeft;
    const alignRight =
      elementOffsetLeft + element.offsetWidth + elementStyles.scrollMarginRight - scrollContainer.clientWidth + containerStyles.scrollPaddingRight;
    const alignLeft = elementOffsetLeft - elementStyles.scrollMarginLeft - containerStyles.scrollPaddingLeft;

    if (direction === 'ltr') {
      if (overflowsRight) {
        targetX = alignRight;
      } else if (overflowsLeft) {
        targetX = alignLeft;
      }
    }

    if (direction === 'rtl') {
      if (overflowsLeft) {
        targetX = alignLeft;
      } else if (overflowsRight) {
        targetX = alignRight;
      }
    }
  }

  if (isOverflowingY && orientation !== 'horizontal') {
    const elementOffsetTop = getOffset(scrollContainer, element, 'top');
    const containerStyles = getScrollStyles(scrollContainer);
    const elementStyles = getScrollStyles(element);

    if (elementOffsetTop - elementStyles.scrollMarginTop < scrollContainer.scrollTop + containerStyles.scrollPaddingTop) {
      targetY = elementOffsetTop - elementStyles.scrollMarginTop - containerStyles.scrollPaddingTop;
    } else if (
      elementOffsetTop + element.offsetHeight + elementStyles.scrollMarginBottom >
      scrollContainer.scrollTop + scrollContainer.clientHeight - containerStyles.scrollPaddingBottom
    ) {
      targetY =
        elementOffsetTop + element.offsetHeight + elementStyles.scrollMarginBottom - scrollContainer.clientHeight + containerStyles.scrollPaddingBottom;
    }
  }

  scrollContainer.scrollTo({
    left: targetX,
    top: targetY,
    behavior: 'auto',
  });
}

function getOffset(ancestor: HTMLElement, element: HTMLElement, side: 'left' | 'top'): number {
  const propName = side === 'left' ? 'offsetLeft' : 'offsetTop';

  let result = 0;
  let current = element;

  while (current.offsetParent) {
    result += current[propName];
    if (current.offsetParent === ancestor) {
      break;
    }
    current = current.offsetParent as HTMLElement;
  }

  return result;
}

function getScrollStyles(element: HTMLElement) {
  const styles = getComputedStyle(element);
  return {
    scrollMarginTop: parseFloat(styles.scrollMarginTop) || 0,
    scrollMarginRight: parseFloat(styles.scrollMarginRight) || 0,
    scrollMarginBottom: parseFloat(styles.scrollMarginBottom) || 0,
    scrollMarginLeft: parseFloat(styles.scrollMarginLeft) || 0,
    scrollPaddingTop: parseFloat(styles.scrollPaddingTop) || 0,
    scrollPaddingRight: parseFloat(styles.scrollPaddingRight) || 0,
    scrollPaddingBottom: parseFloat(styles.scrollPaddingBottom) || 0,
    scrollPaddingLeft: parseFloat(styles.scrollPaddingLeft) || 0,
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The item registry (CMP-ALG-17; CompositeList.tsx). Registry in JavaScript: ADR 01 decision 2.
// ---------------------------------------------------------------------------------------------------------------

/** What an item registers with: an explicit index and metadata, both optional. */
export interface CompositeRegistration {
  /** The index the item occupies; automatic, in document order, when absent. A negative index is ignored. */
  index?: number | null | undefined;
  /** The value published with the item's index; compared by identity. */
  metadata?: object | null | undefined;
}

/** What the registry publishes for one item: its metadata with its index. */
export type CompositeMetadata = { index: number } & Record<string, unknown>;

/** The item registry of one root: elements by index, in document order, with move detection. */
export interface CompositeRegistry {
  /** The registered, connected elements at their indexes; explicit indexes may leave empty slots. */
  readonly elements: Array<HTMLElement | null>;
  /**
   * Registers an element, replacing its earlier registration.
   * @param element The item's element.
   * @param registration The explicit index and metadata.
   */
  register(element: Element, registration?: CompositeRegistration): void;
  /**
   * Removes an element's registration.
   * @param element The item's element.
   */
  unregister(element: Element): void;
  /** Rebuilds the item list once if a registration changed since the last rebuild, publishing it when it differs. */
  flush(): void;
  /**
   * Returns an element's index from the last rebuild.
   * @param element The item's element.
   * @returns The index, or -1 for an element the last rebuild did not index.
   */
  indexOf(element: Element): number;
  /**
   * Adds a listener for the item map, which is published after a rebuild that changed it.
   * @param listener Receives the map from element to metadata with index, ordered by index.
   * @returns A function that removes the listener.
   */
  subscribe(listener: (map: Map<Element, CompositeMetadata>) => void): () => void;
  /** Stops move detection, empties the elements array and forgets every registration. Idempotent. */
  dispose(): void;
}

interface NormalizedRegistration {
  index: number | null;
  metadata: object | null;
}

interface RegistryItem {
  index: number;
  element: HTMLElement;
  registration: NormalizedRegistration;
}

function sortByDocumentPosition(a: Element, b: Element): number {
  // Adjacent siblings are the common case for lists that are already in order, and `compareDocumentPosition` scans
  // siblings from the parent's first child, so sorting a long flat list would otherwise be quadratic.
  if (a.nextElementSibling === b) {
    return -1;
  }
  if (b.nextElementSibling === a) {
    return 1;
  }
  // `DOCUMENT_POSITION_CONTAINED_BY` is always reported alongside `FOLLOWING`, and `CONTAINS` alongside `PRECEDING`,
  // so testing `FOLLOWING` alone orders siblings and nested items alike.
  return a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1;
}

// CMP-STA-01..04: explicit indexes reserve their slots, automatic items fill the rest in document order, disconnected
// elements and negative indexes are left out.
function snapshot(map: Map<Element, NormalizedRegistration>): [RegistryItem[], HTMLElement[]] {
  const reservedIndices = new Set<number>();
  const items: RegistryItem[] = [];
  const automaticItems: RegistryItem[] = [];

  map.forEach((registration, node) => {
    if (!node.isConnected) {
      return;
    }

    const index = registration.index;
    const item = { index: index ?? -1, element: node as HTMLElement, registration };

    if (index === null) {
      automaticItems.push(item);
    } else if (index >= 0) {
      reservedIndices.add(index);
      items.push(item);
    }
  });

  let nextAutomaticIndex = 0;
  automaticItems.sort((a, b) => sortByDocumentPosition(a.element, b.element));

  for (const item of automaticItems) {
    while (reservedIndices.has(nextAutomaticIndex)) {
      nextAutomaticIndex += 1;
    }

    item.index = nextAutomaticIndex;
    items.push(item);
    nextAutomaticIndex += 1;
  }

  if (reservedIndices.size > 0) {
    items.sort((a, b) => a.index - b.index);
  }

  return [items, automaticItems.map((item) => item.element)];
}

function getCommonAncestor(firstNode: Element, lastNode: Element): Element | null {
  let ancestor = firstNode.parentElement;

  // The `parentElement` walk cannot cross shadow boundaries, so the native `contains` is sufficient here.
  while (ancestor && !ancestor.contains(lastNode)) {
    ancestor = ancestor.parentElement;
  }

  return ancestor;
}

function hasMovedNode(entries: MutationRecord[]): boolean {
  for (const entry of entries) {
    for (let i = 0; i < entry.removedNodes.length; i += 1) {
      if (entry.removedNodes[i]?.isConnected) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Creates the item registry of one root: elements registered by reference, rebuilt into index order on `flush`, and
 * rebuilt on its own when a registered element moves in the document.
 * @returns The registry.
 */
export function createCompositeRegistry(): CompositeRegistry {
  const map = new Map<Element, NormalizedRegistration>();
  const elements: Array<HTMLElement | null> = [];
  const listeners = new Set<(map: Map<Element, CompositeMetadata>) => void>();
  let dirty = true;
  let disposed = false;
  let previousItems: readonly RegistryItem[] | null = null;
  let latest = new Map<Element, CompositeMetadata>();
  let mutationObserver: MutationObserver | null = null;

  // CMP-STA-05: the elements array is rewritten in place.
  const syncRefs = (items: readonly RegistryItem[]): Map<Element, CompositeMetadata> => {
    const nextMap = new Map<Element, CompositeMetadata>();
    elements.length = 0;
    for (const item of items) {
      nextMap.set(item.element, { ...((item.registration.metadata ?? {}) as Record<string, unknown>), index: item.index });
      elements[item.index] = item.element;
    }
    return nextMap;
  };

  // CMP-STA-11/12: observe the lowest common ancestor of each pair of adjacent automatic items, re-installed after every
  // rebuild; without MutationObserver the registry still works (CMP-EDGE-13), and items with no common ancestor, such
  // as one across a shadow boundary, are not observed (CMP-STA-13).
  const observe = (sortedNodes: HTMLElement[]): void => {
    mutationObserver?.disconnect();
    mutationObserver = null;

    // A single item can't reorder.
    if (typeof MutationObserver !== 'function' || sortedNodes.length < 2) {
      return;
    }

    const observer = new MutationObserver((entries) => {
      // Only verify the order after a move: a node removed and re-added within one batch. Additions and removals
      // alone can't change the relative order of the remaining items.
      if (!hasMovedNode(entries)) {
        return;
      }

      let previousConnectedNode: Element | null = null;
      for (const node of sortedNodes) {
        if (!node.isConnected) {
          continue;
        }
        if (previousConnectedNode && sortByDocumentPosition(previousConnectedNode, node) > 0) {
          observer.disconnect();
          dirty = true;
          rebuild();
          return;
        }
        previousConnectedNode = node;
      }
    });

    mutationObserver = observer;

    const roots = new Set<Element>();
    for (let i = 1; i < sortedNodes.length; i += 1) {
      const root = getCommonAncestor(sortedNodes[i - 1]!, sortedNodes[i]!);
      if (root) {
        roots.add(root);
      }
    }

    roots.forEach((root) => observer.observe(root, { childList: true }));
  };

  // CMP-STA-09: publish only when the length, an index, an element, an explicit index or a metadata identity changed.
  const rebuild = (): void => {
    if (disposed) {
      return;
    }
    const [items, automaticNodes] = snapshot(map);
    const nextMap = syncRefs(items);

    const previous = previousItems;
    const changed =
      !previous ||
      previous.length !== items.length ||
      items.some((item, index) => {
        const previousItem = previous[index]!;
        return (
          item.index !== previousItem.index ||
          item.element !== previousItem.element ||
          item.registration.index !== previousItem.registration.index ||
          item.registration.metadata !== previousItem.registration.metadata
        );
      });

    observe(automaticNodes);
    previousItems = items;
    latest = nextMap;
    dirty = false;

    if (!changed) {
      return;
    }

    for (const listener of [...listeners]) {
      listener(nextMap);
    }
  };

  return {
    elements,
    register(element, registration = {}) {
      if (disposed) {
        return;
      }
      map.set(element, { index: registration.index ?? null, metadata: registration.metadata ?? null });
      dirty = true;
    },
    unregister(element) {
      if (disposed) {
        return;
      }
      map.delete(element);
      dirty = true;
    },
    flush() {
      if (dirty) {
        rebuild();
      }
    },
    indexOf(element) {
      return latest.get(element)?.index ?? -1;
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    // CMP-STA-14
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      mutationObserver?.disconnect();
      mutationObserver = null;
      elements.length = 0;
      map.clear();
      listeners.clear();
      latest = new Map();
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The root (attach): CompositeTwin's handle
// ---------------------------------------------------------------------------------------------------------------

/** One item as C# registers it: the id of its element and the consumer's `tabindex`, which the module never overrides. */
export interface CompositeItemRegistration {
  /** The id of the item's element, resolved from the root's root node. */
  id: string;
  /** The consumer's `tabindex`, or `null` when the module writes the roving value. */
  tabIndex?: string | null | undefined;
}

// Mirrors `CompositeOptions` in C#.
/** The options of the `composite` module, sent on `attach` and on every `update`. */
export interface CompositeOptions extends TwinOptions {
  /** The arrow keys that move the highlight. */
  orientation: Orientation;
  /** Whether a step past either end wraps to the other end. */
  loopFocus: boolean;
  /** Whether Home and End move the highlight to the first and last item. */
  enableHomeAndEndKeys: boolean;
  /** Whether a handled key's propagation is stopped. */
  stopEventPropagation: boolean;
  /** Whether moving the pointer over an item focuses it. */
  highlightItemOnHover: boolean;
  /** The modifiers that do not block navigation. */
  modifierKeys: ReadonlyArray<string>;
  /** The indices navigation skips; absent or `null` to read the items' disabled state from the DOM. */
  disabledIndices?: number[] | null | undefined;
  /** The reading direction. */
  direction: TextDirection;
  /** The items, by element id. */
  items: CompositeItemRegistration[];
  /** Whether an item an arrow key focuses is selected by clicking the input that follows it. */
  selectOnNavigation: boolean;
  /** Whether the module reports focus leaving the root. */
  reportFocusLeave: boolean;
  /** Whether the module reports each change of the highlighted index. */
  reportHighlight: boolean;
  /** Whether the module reports a key it handled and stopped without asking C# inside the dispatch. */
  reportHandledKeys?: boolean | undefined;
}

// A symbol on the element, not a module registry: a repeated attach of the same element returns the live handle.
const attachedHandle = Symbol('blazix.composite.handle');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

interface AttachedItem {
  element: HTMLElement;
  tabIndex: string | null;
}

function sameIndices(a: ReadonlyArray<number> | null | undefined, b: ReadonlyArray<number> | null | undefined): boolean {
  if (a == null || b == null) {
    return a == null && b == null;
  }
  return a.length === b.length && a.every((value, index) => value === b[index]);
}

/**
 * Attaches the roving group to a root element. From here on the module resolves the items C# registers, keeps their
 * order, owns their `tabindex` (except where the consumer set one), handles the navigation keys on the root, follows
 * focus and, when asked, the pointer, and reports highlight changes and focus leaving the root.
 * @param elementOrId The root element, or its id when the part has no element reference.
 * @param optionsJson The `CompositeOptions` JSON.
 * @param twinReference The twin's object reference.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin): TwinHandle {
  const root = resolveElement(elementOrId) as AttachedElement;
  const existing = root[attachedHandle];
  if (existing) {
    return existing;
  }

  const twin = createTwinChannel(twinReference);
  let options = parseOptions<CompositeOptions>(optionsJson);
  let disposed = false;
  const registry = createCompositeRegistry();
  let items = new Map<string, AttachedItem>();
  // Elements whose `tabindex` belongs to the consumer.
  let consumerTabIndexes = new Set<Element>();
  // CMP-STA-22: the root owns the highlight, initially 0, which is also the value C# mirrors before any report.
  let highlightedIndex = 0;
  let highlightedElement: HTMLElement | null = null;
  let defaultIndexSet = false;
  let reportedHighlight = 0;
  // RDG-STA-06: never state; set by an arrow key, consumed by the focus-driven selection, cleared by the leaving blur.
  let pendingNavigation = false;

  const resolveItem = (id: string): HTMLElement | null => {
    const scope = root.getRootNode();
    const found =
      'getElementById' in scope && typeof (scope as Document | ShadowRoot).getElementById === 'function'
        ? (scope as Document | ShadowRoot).getElementById(id)
        : null;
    const element = found ?? ownerDocument(root).getElementById(id);
    return isHTMLElement(element) ? element : null;
  };

  // CMP-DOM-01: "0" on the highlighted index, "-1" elsewhere, never over a consumer's value (RAD-DOM-16).
  const writeTabIndexes = (): void => {
    registry.elements.forEach((element, index) => {
      if (!element || consumerTabIndexes.has(element)) {
        return;
      }
      const value = index === highlightedIndex ? '0' : '-1';
      if (element.getAttribute('tabindex') !== value) {
        element.setAttribute('tabindex', value);
      }
    });
  };

  // CMP-EVT-01 as a report: once per change, only when asked (ADR 16 rule 6).
  const reportHighlightChange = (): void => {
    if (!options.reportHighlight || highlightedIndex === reportedHighlight) {
      return;
    }
    reportedHighlight = highlightedIndex;
    twin.report('OnHighlightChange', { index: highlightedIndex });
  };

  // CMP-STA-29 (M3, implemented early): a root-owned highlight on an index that `disabledIndices` disables moves to the
  // first enabled index, after the first publication.
  const revalidateHighlight = (): void => {
    const disabledIndices = options.disabledIndices;
    if (disabledIndices == null || !defaultIndexSet) {
      return;
    }
    const elements = registry.elements;
    if (isListIndexDisabled(elements, highlightedIndex, disabledIndices)) {
      const firstEnabledIndex = findNonDisabledListIndex(elements, { disabledIndices });
      if (!isIndexOutOfListBounds(elements, firstEnabledIndex)) {
        setHighlightedIndex(firstEnabledIndex, false);
      }
    }
  };

  // Base UI's onHighlightedIndexChange(index, shouldScrollIntoView): the tab stop moves in the same task (CMP-DOM-01).
  function setHighlightedIndex(index: number, scroll: boolean): void {
    const elements = registry.elements;
    highlightedElement = elements[index] ?? null;
    const changed = index !== highlightedIndex;
    highlightedIndex = index;
    writeTabIndexes();
    if (scroll) {
      // CMP-STA-30
      scrollIntoViewIfNeeded(root, elements[index] ?? null, options.direction, options.orientation);
    }
    reportHighlightChange();
    if (changed) {
      revalidateHighlight();
    }
  }

  // CMP-ALG-18, CMP-STA-23..28.
  registry.subscribe((map) => {
    if (map.size === 0) {
      return;
    }

    const elements = registry.elements;
    const { disabledIndices } = options;

    if (defaultIndexSet) {
      // Items added or removed around the highlighted one shift its index, so the tab stop follows the element.
      const nextIndex = highlightedElement ? elements.indexOf(highlightedElement) : -1;

      if (nextIndex === -1) {
        // A replacement at the same index can keep the tab stop. Otherwise move it to an eligible item so a missing,
        // hidden, or disabled replacement does not take the composite out of the tab order.
        const replacement = elements[highlightedIndex];
        if (!replacement || isListIndexDisabled(elements, highlightedIndex, disabledIndices)) {
          setHighlightedIndex(getFallbackIndex(elements, disabledIndices), false);
        } else {
          highlightedElement = replacement;
        }
      } else if (nextIndex !== highlightedIndex) {
        setHighlightedIndex(nextIndex, false);
      }
      writeTabIndexes();
      return;
    }

    defaultIndexSet = true;

    const sortedElements = Array.from(map.keys()) as HTMLElement[];
    const activeItem = sortedElements.find((element) => element.hasAttribute(ACTIVE_COMPOSITE_ITEM)) ?? null;
    // The map value carries the item's own index, which is not its position among the keys once explicit and
    // automatic indexes leave gaps (CMP §10 Q4: the disabled check below indexes positions, as transcribed).
    const activeIndex = activeItem ? (map.get(activeItem)?.index ?? -1) : -1;

    if (activeIndex !== -1) {
      setHighlightedIndex(activeIndex, false);
    } else if (isListIndexDisabled(sortedElements, highlightedIndex, disabledIndices)) {
      // The default highlighted item is disabled, so it does not hold the tab stop; if every item is disabled the
      // index is kept.
      const firstEnabledIndex = findNonDisabledListIndex(sortedElements, { disabledIndices });
      if (!isIndexOutOfListBounds(sortedElements, firstEnabledIndex)) {
        setHighlightedIndex(firstEnabledIndex, false);
      }
    }

    // CMP-STA-25
    scrollIntoViewIfNeeded(root, activeItem, options.direction, options.orientation);
    writeTabIndexes();
  });

  // Resolves the registered ids; registers new and replaced elements and unregisters removed ones. Returns whether the
  // registry changed, so one `update` rebuilds once (CMP-STA-10).
  const syncItems = (registrations: ReadonlyArray<CompositeItemRegistration>): boolean => {
    const next = new Map<string, AttachedItem>();
    for (const registration of registrations) {
      const element = resolveItem(registration.id);
      if (element) {
        next.set(registration.id, { element, tabIndex: registration.tabIndex ?? null });
      }
    }

    const nextElements = new Set<Element>();
    next.forEach((attached) => nextElements.add(attached.element));

    let changed = false;
    items.forEach((previous, id) => {
      if (next.get(id)?.element !== previous.element && !nextElements.has(previous.element)) {
        registry.unregister(previous.element);
        changed = true;
      }
    });
    next.forEach((attached, id) => {
      if (items.get(id)?.element !== attached.element) {
        registry.register(attached.element);
        changed = true;
      }
    });

    items = next;
    consumerTabIndexes = new Set();
    next.forEach((attached) => {
      if (attached.tabIndex != null) {
        consumerTabIndexes.add(attached.element);
      }
    });
    return changed;
  };

  // The item an event concerns: the outermost registered element on its composed path inside the root, as React's
  // focus handlers of nested items would leave it.
  const itemOf = (event: Event): HTMLElement | null => {
    let found: HTMLElement | null = null;
    for (const node of event.composedPath()) {
      if (node === root) {
        break;
      }
      if (isHTMLElement(node) && registry.indexOf(node) !== -1) {
        found = node;
      }
    }
    return found;
  };

  // RDG-STA-06, RDG-KEY-03: before the root's own handling, whatever the modifiers and whether or not it acts.
  const onKeyDownCapture = (event: KeyboardEvent): void => {
    if (options.selectOnNavigation && typeof event.key === 'string' && event.key.startsWith('Arrow')) {
      pendingNavigation = true;
    }
  };

  // CMP-ALG-14 without the grid branch (no consumer, CMP §10 Q1).
  const onKeyDown = (event: KeyboardEvent): void => {
    const current = options;
    const isHomeOrEnd = event.key === HOME || event.key === END;
    if (!COMPOSITE_KEYS.has(event.key) || (!current.enableHomeAndEndKeys && isHomeOrEnd)) {
      return;
    }

    if (isModifierKeySet(event, current.modifierKeys)) {
      return;
    }

    // CMP-EDGE-08
    if (!root.isConnected) {
      return;
    }

    const isRtl = current.direction === 'rtl';
    const { orientation, disabledIndices } = current;

    const horizontalForwardKey = isRtl ? ARROW_LEFT : ARROW_RIGHT;
    const horizontalBackwardKey = isRtl ? ARROW_RIGHT : ARROW_LEFT;
    const forwardKey = orientation === 'vertical' ? ARROW_DOWN : horizontalForwardKey;
    const backwardKey = orientation === 'vertical' ? ARROW_UP : horizontalBackwardKey;

    // CMP-KEY-04 (M3, implemented early), CMP-EDGE-12: the original target from the composed path.
    const target = getTarget(event);
    if (target != null && isNativeInput(target) && !isElementDisabled(target)) {
      const selectionStart = target.selectionStart;
      const selectionEnd = target.selectionEnd;
      const textContent = target.value;
      // Native text behavior: Shift held to select, or a selection exists.
      if (selectionStart == null || event.shiftKey || selectionStart !== selectionEnd) {
        return;
      }
      // Moving forward and not at the last caret position.
      if (event.key !== backwardKey && selectionStart < textContent.length) {
        return;
      }
      // Moving backward and not at the first caret position.
      if (event.key !== forwardKey && selectionStart > 0) {
        return;
      }
    }

    const elements = registry.elements;
    let nextIndex = highlightedIndex;
    const minIndex = getMinListIndex(elements, disabledIndices);
    const maxIndex = getMaxListIndex(elements, disabledIndices);

    const isForwardKey =
      (orientation !== 'vertical' && event.key === horizontalForwardKey) || (orientation !== 'horizontal' && event.key === ARROW_DOWN);
    const isBackwardKey =
      (orientation !== 'vertical' && event.key === horizontalBackwardKey) || (orientation !== 'horizontal' && event.key === ARROW_UP);

    // CMP-KEY-05 (M3, implemented early)
    if (current.enableHomeAndEndKeys) {
      if (event.key === HOME) {
        nextIndex = minIndex;
      } else if (event.key === END) {
        nextIndex = maxIndex;
      }
    }

    // CMP-KEY-06..09 without the loop hook (CMP §10 Q1).
    if (nextIndex === highlightedIndex && (isForwardKey || isBackwardKey)) {
      if (current.loopFocus && nextIndex === maxIndex && isForwardKey) {
        nextIndex = minIndex;
      } else if (current.loopFocus && nextIndex === minIndex && isBackwardKey) {
        nextIndex = maxIndex;
      } else {
        nextIndex = findNonDisabledListIndex(elements, {
          startingIndex: nextIndex,
          decrement: isBackwardKey,
          disabledIndices,
        });
      }
    }

    // CMP-KEY-10/11, CMP-EDGE-18: nothing is prevented or stopped unless the index changes.
    if (nextIndex === highlightedIndex || isIndexOutOfListBounds(elements, nextIndex)) {
      return;
    }

    // The consumer's veto, asked only where the module is about to act (EVD-EVT-26; Server: run).
    if (decide(twin, NAVIGATE_POINT, event, 'run') !== 'run') {
      return;
    }

    if (current.stopEventPropagation) {
      event.stopPropagation();
    }

    if (isHomeOrEnd || isForwardKey || isBackwardKey) {
      event.preventDefault();
    }
    setHighlightedIndex(nextIndex, true);

    // CMP-FOC-02: wait for a focus manager's return focus, which runs synchronously, to complete.
    queueMicrotask(() => {
      registry.elements[nextIndex]?.focus();
    });

    // Option (i) of ADR 04's note of 2026-10-22, chosen by the user 2026-10-23: a key stopped here never reaches Blazor's dispatcher,
    // so where no C# chain ran inside the dispatch (Server), C# runs the consumer's handlers from this report.
    if (current.stopEventPropagation && current.reportHandledKeys && !twin.lastDecisionWasSynchronous) {
      twin.report('OnKeyHandled', { event: eventInfo(event) });
    }
  };

  const onFocusIn = (event: FocusEvent): void => {
    const current = options;
    const item = itemOf(event);

    // CMP-PTR-02: focus from any source moves the highlight to the item, without scrolling.
    if (item) {
      setHighlightedIndex(registry.indexOf(item), false);
    }

    // CMP-KEY-17 (M3, implemented early): a focused native text input selects its value.
    const target = getTarget(event);
    if (target != null && isNativeInput(target)) {
      target.setSelectionRange(0, target.value.length);
    }

    // RAD-FOC-05: the focus-driven selection. A disabled or read-only input returns before the flag is cleared, as
    // RadioRoot.tsx:156-166 does; a focus event cannot be canceled, so the defaultPrevented check is kept for fidelity
    // only (RAD §16 Q6).
    if (!current.selectOnNavigation || !pendingNavigation || !item || event.defaultPrevented) {
      return;
    }

    const input = item.nextElementSibling;
    if (isHTMLElement(input) && input.tagName === 'INPUT') {
      const { disabled, readOnly } = input as HTMLInputElement;
      if (disabled || readOnly) {
        return;
      }
      if (decide(twin, SELECT_POINT, event, 'run') !== 'run') {
        return;
      }
      dispatchClickWithModifiers(input, NO_MODIFIERS);
    }

    pendingNavigation = false;
  };

  // RDG-FOC-04/05: focus leaving the root (a related target outside it, across shadow boundaries, or none).
  const onFocusOut = (event: FocusEvent): void => {
    const relatedTarget = event.relatedTarget;
    if (contains(root, isElement(relatedTarget) ? relatedTarget : null)) {
      return;
    }

    pendingNavigation = false;
    if (options.reportFocusLeave) {
      twin.report('OnFocusLeave', { event: eventInfo(event) });
    }
  };

  // CMP-PTR-01, CMP-API-03 (M5, implemented early).
  const onMouseMove = (event: MouseEvent): void => {
    if (!options.highlightItemOnHover) {
      return;
    }
    const item = itemOf(event);
    if (!item) {
      return;
    }
    const disabled = item.hasAttribute('disabled') || item.getAttribute('aria-disabled') === 'true';
    if (registry.indexOf(item) !== highlightedIndex && !disabled) {
      item.focus();
    }
  };

  const listeners: [string, EventListener, boolean][] = [
    ['keydown', onKeyDownCapture as EventListener, true],
    ['keydown', onKeyDown as EventListener, false],
    ['focusin', onFocusIn as EventListener, false],
    ['focusout', onFocusOut as EventListener, false],
    ['mousemove', onMouseMove as EventListener, false],
  ];

  for (const [type, listener, capture] of listeners) {
    root.addEventListener(type, listener, capture);
  }

  // The first publication (CMP-STA-23..25, ALG-18).
  syncItems(options.items);
  registry.flush();
  writeTabIndexes();

  const handle: TwinHandle = {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      const previous = options;
      options = parseOptions<CompositeOptions>(nextOptionsJson);
      if (syncItems(options.items)) {
        registry.flush();
      }
      writeTabIndexes();
      if (!sameIndices(previous.disabledIndices, options.disabledIndices)) {
        revalidateHighlight();
      }
      reportHighlightChange();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete root[attachedHandle];
      for (const [type, listener, capture] of listeners) {
        root.removeEventListener(type, listener, capture);
      }
      registry.dispose();
      items = new Map();
    },
  };
  root[attachedHandle] = handle;
  return handle;
}
