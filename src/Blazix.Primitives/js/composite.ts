// The `composite` mechanism module (ADR 04 §1 composite row; contract docs/architecture/composite-and-list-navigation.md,
// CMP): the roving group. M2 ships what RadioGroup reaches (ADR 12 Amendments 2026-10-22 item 3): the index helpers
// (CMP-ALG-01..06, 15, 16, 19), the item registry with move detection (CMP-ALG-17, CMP-STA-01..18), the root's key
// resolution without the grid branch (CMP-ALG-14), the publication adjustments (CMP-ALG-18, CMP-STA-22..30), the
// pointer and focus handling of the items (CMP-PTR-01/02, CMP-KEY-17), and RadioGroup's options (the pending-navigation
// flag, the focus-driven selection and the leaving blur; ADR 04 Amendments 2026-10-20, RDG-STA-06, RAD-FOC-05,
// RDG-FOC-04/05). M3 track 1 (ADR 04 Amendments 2026-10-26, chosen by the user) adds the items' `disabled` and
// `focusableWhenDisabled` flags with the indices derived from them (CMP-STA-08, CMP-STA-29, CMP-KEY-08) and the items
// report, `onMapChange` as ids in index order (CMP-EVT-03), and a registration-only mode for a root-less list that keeps
// the registry and the report and nothing else (CMP-STR-03), and TabsList's highlight that follows the item marked active
// (CMP-STA-22, CMP-EDGE-09). Not built: grid, `onLoop`, the keyed registry (CMP §10 Q1), the keyboard relay
// (CMP-KEY-14), a consumer-owned highlight beyond the follow, list navigation and typeahead (M5), labels and `guess` (M5).
// Transcribed from Base UI v1.9.0: internals/composite/{composite,constants}.ts, root/useCompositeRoot.ts,
// root/CompositeRoot.tsx, item/useCompositeItem.ts, list/CompositeList.tsx, list/useCompositeListItem.ts;
// floating-ui-react/utils/composite.ts; radio-group/RadioGroup.tsx:205-245 and radio/root/RadioRoot.tsx:156-166.
// From `attach` the module is the only writer of every item's `tabindex` (ADR 04 Amendments 2026-10-22): C# renders
// "-1" (CMP-STA-15, the handover at attach) and the module writes "0" or "-1" in the task that moves the highlight
// (CMP-DOM-01), except on an item whose registration carries the consumer's `tabindex` (RAD-DOM-16) or none at all, a
// navigation menu link (NAV-DOM-21; M5 track 1).
// Decision points (ADR 04 §3, fallback run for both; names chosen by the user 2026-10-22, ADR 04 Amendments 2026-10-22):
// `navigate`, the consumer's onKeyDown veto against key handling (EVD-EVT-26, CMP-EVT-12, RDG-API-12), and `select`,
// the focus-driven selection (ADR 04 Amendments 2026-10-20).
import { dispatchClickWithModifiers, findHiddenInput, isElementDisabled } from './button';
import { decide } from './decide';
import { activeElement, closest, contains, getTarget, isElement, isElementVisible, isHiddenByStyles, isHTMLElement, ownerDocument, platform } from './dom';
import { eventInfo, type EventInfo } from './event-info';
import { enqueueFocus } from './focus-manager';
import {
  acquireSession,
  getSession,
  isTypeableElement,
  isVirtualClick,
  isVirtualPointerEvent,
  LIST_OPEN_FOCUS_KEY,
  releaseSession,
  type InteractionSession,
  type ListOpenFocus,
  type SessionOpenChangeDetails,
} from './interactions';
import { createFrameSlot, type FrameSlot } from './transitions';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  resolveReference,
  type DotNetTwin,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
  type TwinCaptures,
  type TwinReferences,
} from './twin';

// ---------------------------------------------------------------------------------------------------------------
// Constants (CMP §6.11)
// ---------------------------------------------------------------------------------------------------------------

/** The attribute a component puts on the item that takes the initial tab stop, such as the checked radio. */
export const ACTIVE_COMPOSITE_ITEM = 'data-composite-item-active';

/** The name of the decision point asked before the module handles a navigation key. */
export const NAVIGATE_POINT = 'navigate';

/** The name of the decision point asked before the module selects a focused item by clicking its input. */
export const SELECT_POINT = 'select';

// ADR 04 Amendments 2026-11-03 (3), chosen by the user: the Shift+Tab close of a list (CMP-KEY-24, CMP-EVT-08).
/** The name of the decision point asked before the module returns the focus after a list's Shift+Tab close. */
export const LEAVE_POINT = 'leave';

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

// CMP-ALG-03: `isHiddenByStyles` and `isElementVisible` live in `dom` (design point 6 (a) of M5 track 1, settled by the
// orchestrator 2026-11-04: `focus-manager` imports them from there, so this module imports `focus-manager` without a
// cycle, ADR 04 §1); re-exported for this module's callers.
export { isElementVisible, isHiddenByStyles };

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

// CMP-ALG-07 (M5 track 1, the popup grid; floating-ui-react/utils/composite.ts:11-13)
/**
 * Determines whether an index lies in another grid row than a row number.
 * @param index The index.
 * @param cols The number of columns.
 * @param prevRow The row number.
 * @returns `true` when `floor(index / cols)` differs from `prevRow`.
 */
export function isDifferentGridRow(index: number, cols: number, prevRow: number): boolean {
  return Math.floor(index / cols) !== prevRow;
}

/** The options of one grid step (`getGridNavigatedIndex`). */
export interface GridNavigationOptions {
  /** The keydown. */
  event: KeyboardEvent;
  /** The arrow keys the grid moves on horizontally: only `both` moves within a row. */
  orientation: Orientation;
  /** Whether a step past either end wraps. */
  loopFocus: boolean;
  /** Called when a step wraps, with the destination it may replace; absent for the popup grid. */
  onLoop?: ((event: KeyboardEvent, prevIndex: number, nextIndex: number) => number) | undefined;
  /** Whether the reading direction is right to left. */
  rtl: boolean;
  /** The number of columns. */
  cols: number;
  /** The indices to skip. */
  disabledIndices: DisabledIndices | undefined;
  /** The first navigable index. */
  minIndex: number;
  /** The last navigable index. */
  maxIndex: number;
  /** The index the step starts from; -1 for none. */
  prevIndex: number;
  /** Whether a key the grid handles is prevented and stopped. */
  stopEvent?: boolean | undefined;
}

// CMP-ALG-08 (M5 track 1, the popup grid; floating-ui-react/utils/composite.ts:111-432): vertical moves follow
// `[role="row"]` rows when the DOM has rows of another width than `cols`, and inferred rows of that width across a
// virtualized list's gaps; horizontal moves stay within the row for `both`.
/**
 * Computes the index an arrow key moves a grid's highlight to.
 * @param list The elements array; a virtualized list leaves the unrendered indices empty.
 * @param options The step's options.
 * @returns The index, `prevIndex` when the key does not move it.
 */
export function getGridNavigatedIndex(list: ReadonlyArray<HTMLElement | null | undefined>, options: GridNavigationOptions): number {
  const { event, orientation, loopFocus, onLoop, rtl, cols, disabledIndices, minIndex, maxIndex, prevIndex, stopEvent: stop = false } = options;
  let nextIndex = prevIndex;

  let verticalDirection: 'up' | 'down' | undefined;
  if (event.key === ARROW_UP) {
    verticalDirection = 'up';
  } else if (event.key === ARROW_DOWN) {
    verticalDirection = 'down';
  }

  if (verticalDirection) {
    // Detect row structure only when handling vertical navigation. This keeps the non-vertical key paths free from row
    // inference work.
    const rows: number[][] = [];
    const rowIndexMap: number[] = [];
    let hasRoleRow = false;
    let visibleItemCount = 0;
    {
      let currentRowEl: Element | null = null;
      let currentRowIndex = -1;

      list.forEach((el, idx) => {
        if (el == null) {
          return;
        }

        visibleItemCount += 1;

        const rowEl = closest(el, '[role="row"]');
        if (rowEl) {
          hasRoleRow = true;
        }

        if (rowEl !== currentRowEl || currentRowIndex === -1) {
          currentRowEl = rowEl;
          currentRowIndex += 1;
          rows[currentRowIndex] = [];
        }
        rows[currentRowIndex]!.push(idx);
        rowIndexMap[idx] = currentRowIndex;
      });
    }

    let hasDomRows = false;
    let inferredDomCols = 0;

    if (hasRoleRow) {
      for (const row of rows) {
        const rowLength = row.length;

        if (rowLength > inferredDomCols) {
          inferredDomCols = rowLength;
        }

        if (rowLength !== cols) {
          hasDomRows = true;
        }
      }
    }

    const hasVirtualizedGaps = hasDomRows && visibleItemCount < list.length;
    const verticalCols = inferredDomCols || cols;

    const navigateVertically = (direction: 'up' | 'down'): number | undefined => {
      if (!hasDomRows || prevIndex === -1) {
        return undefined;
      }

      const currentRow = rowIndexMap[prevIndex];
      if (currentRow == null) {
        return undefined;
      }

      const colInRow = rows[currentRow]!.indexOf(prevIndex);
      const step = direction === 'up' ? -1 : 1;

      for (let nextRow = currentRow + step, i = 0; i < rows.length; i += 1, nextRow += step) {
        if (nextRow < 0 || nextRow >= rows.length) {
          if (!loopFocus || hasVirtualizedGaps) {
            return undefined;
          }
          nextRow = nextRow < 0 ? rows.length - 1 : 0;
          if (onLoop) {
            const clampedCol = Math.min(colInRow, rows[nextRow]!.length - 1);
            const targetItemIndex = rows[nextRow]![clampedCol] ?? rows[nextRow]![0]!;
            const returnedItemIndex = onLoop(event, prevIndex, targetItemIndex);
            nextRow = rowIndexMap[returnedItemIndex] ?? nextRow;
          }
        }

        const targetRow = rows[nextRow]!;
        for (let col = Math.min(colInRow, targetRow.length - 1); col >= 0; col -= 1) {
          const candidate = targetRow[col]!;
          if (!isListIndexDisabled(list, candidate, disabledIndices)) {
            return candidate;
          }
        }
      }

      return undefined;
    };

    const navigateVerticallyWithInferredRows = (direction: 'up' | 'down'): number | undefined => {
      if (!hasVirtualizedGaps || prevIndex === -1) {
        return undefined;
      }

      const colInRow = prevIndex % verticalCols;
      const rowStep = direction === 'up' ? -verticalCols : verticalCols;
      const lastRowStart = maxIndex - (maxIndex % verticalCols);
      const rowCount = Math.floor(maxIndex / verticalCols) + 1;

      for (let rowStart = prevIndex - colInRow + rowStep, i = 0; i < rowCount; i += 1, rowStart += rowStep) {
        if (rowStart < 0 || rowStart > maxIndex) {
          if (!loopFocus) {
            return undefined;
          }
          rowStart = rowStart < 0 ? lastRowStart : 0;
        }

        const rowEnd = Math.min(rowStart + verticalCols - 1, maxIndex);
        for (let candidate = Math.min(rowStart + colInRow, rowEnd); candidate >= rowStart; candidate -= 1) {
          if (!isListIndexDisabled(list, candidate, disabledIndices)) {
            return candidate;
          }
        }
      }

      return undefined;
    };

    if (stop) {
      stopEvent(event);
    }

    const verticalCandidate = navigateVertically(verticalDirection) ?? navigateVerticallyWithInferredRows(verticalDirection);

    if (verticalCandidate !== undefined) {
      nextIndex = verticalCandidate;
    } else if (prevIndex === -1) {
      // CMP-EDGE-16
      nextIndex = verticalDirection === 'up' ? maxIndex : minIndex;
    } else {
      nextIndex = findNonDisabledListIndex(list, {
        startingIndex: prevIndex,
        amount: verticalCols,
        decrement: verticalDirection === 'up',
        disabledIndices,
      });

      if (loopFocus) {
        if (verticalDirection === 'up' && (prevIndex - verticalCols < minIndex || nextIndex < 0)) {
          const col = prevIndex % verticalCols;
          const maxCol = maxIndex % verticalCols;
          const offset = maxIndex - (maxCol - col);

          if (maxCol === col) {
            nextIndex = maxIndex;
          } else {
            nextIndex = maxCol > col ? offset : offset - verticalCols;
          }
          if (onLoop) {
            nextIndex = onLoop(event, prevIndex, nextIndex);
          }
        }

        if (verticalDirection === 'down' && prevIndex + verticalCols > maxIndex) {
          nextIndex = findNonDisabledListIndex(list, {
            startingIndex: (prevIndex % verticalCols) - verticalCols,
            amount: verticalCols,
            disabledIndices,
          });
          if (onLoop) {
            nextIndex = onLoop(event, prevIndex, nextIndex);
          }
        }
      }
    }

    if (isIndexOutOfListBounds(list, nextIndex)) {
      nextIndex = prevIndex;
    }
  }

  // Remains on the same row/column (CMP-ALG-07).
  if (orientation === 'both') {
    const prevRow = Math.floor(prevIndex / cols);

    if (event.key === (rtl ? ARROW_LEFT : ARROW_RIGHT)) {
      if (stop) {
        stopEvent(event);
      }

      if (prevIndex % cols !== cols - 1) {
        nextIndex = findNonDisabledListIndex(list, {
          startingIndex: prevIndex,
          disabledIndices,
        });

        if (loopFocus && isDifferentGridRow(nextIndex, cols, prevRow)) {
          nextIndex = findNonDisabledListIndex(list, {
            startingIndex: prevIndex - (prevIndex % cols) - 1,
            disabledIndices,
          });
          if (onLoop) {
            nextIndex = onLoop(event, prevIndex, nextIndex);
          }
        }
      } else if (loopFocus) {
        nextIndex = findNonDisabledListIndex(list, {
          startingIndex: prevIndex - (prevIndex % cols) - 1,
          disabledIndices,
        });
        if (onLoop) {
          nextIndex = onLoop(event, prevIndex, nextIndex);
        }
      }

      if (isDifferentGridRow(nextIndex, cols, prevRow)) {
        nextIndex = prevIndex;
      }
    }

    if (event.key === (rtl ? ARROW_RIGHT : ARROW_LEFT)) {
      if (stop) {
        stopEvent(event);
      }

      if (prevIndex % cols !== 0) {
        nextIndex = findNonDisabledListIndex(list, {
          startingIndex: prevIndex,
          decrement: true,
          disabledIndices,
        });

        if (loopFocus && isDifferentGridRow(nextIndex, cols, prevRow)) {
          nextIndex = findNonDisabledListIndex(list, {
            startingIndex: prevIndex + (cols - (prevIndex % cols)),
            decrement: true,
            disabledIndices,
          });
          if (onLoop) {
            nextIndex = onLoop(event, prevIndex, nextIndex);
          }
        }
      } else if (loopFocus) {
        nextIndex = findNonDisabledListIndex(list, {
          startingIndex: prevIndex + (cols - (prevIndex % cols)),
          decrement: true,
          disabledIndices,
        });
        if (onLoop) {
          nextIndex = onLoop(event, prevIndex, nextIndex);
        }
      }

      if (isDifferentGridRow(nextIndex, cols, prevRow)) {
        nextIndex = prevIndex;
      }
    }

    const lastRow = Math.floor(maxIndex / cols) === prevRow;

    if (isIndexOutOfListBounds(list, nextIndex)) {
      if (loopFocus && lastRow) {
        nextIndex =
          event.key === (rtl ? ARROW_RIGHT : ARROW_LEFT)
            ? maxIndex
            : findNonDisabledListIndex(list, {
                startingIndex: prevIndex - (prevIndex % cols) - 1,
                disabledIndices,
              });
        if (onLoop) {
          nextIndex = onLoop(event, prevIndex, nextIndex);
        }
      } else {
        nextIndex = prevIndex;
      }
    }
  }

  return nextIndex;
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

/** What an item registers with: an explicit index, metadata and its label's source, all optional. */
export interface CompositeRegistration {
  /** The index the item occupies; automatic, in document order, when absent. A negative index is ignored. */
  index?: number | null | undefined;
  /** The value published with the item's index; compared by identity. */
  metadata?: object | null | undefined;
  /** The item's label; `null` for no label, which never falls back; absent to read `textElement`'s or the item's text. */
  label?: string | null | undefined;
  /** The element whose text is the label when `label` is absent. */
  textElement?: Element | null | undefined;
}

/** What the registry publishes for one item: its metadata with its index. */
export type CompositeMetadata = { index: number } & Record<string, unknown>;

/** The item registry of one root: elements by index, in document order, with move detection. */
export interface CompositeRegistry {
  /** The registered, connected elements at their indexes; explicit indexes may leave empty slots. */
  readonly elements: Array<HTMLElement | null>;
  // CMP-STA-06, CMP-STA-07.
  /** The items' labels, aligned with `elements`, resolved on every rebuild. */
  readonly labels: Array<string | null>;
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
  label: string | null | undefined;
  textElement: Element | null;
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
  const labels: Array<string | null> = [];
  const listeners = new Set<(map: Map<Element, CompositeMetadata>) => void>();
  let dirty = true;
  let disposed = false;
  let previousItems: readonly RegistryItem[] | null = null;
  let latest = new Map<Element, CompositeMetadata>();
  let mutationObserver: MutationObserver | null = null;

  // CMP-STA-05: the elements and labels arrays are rewritten in place. CMP-STA-06/07 (M5 track 1): each label is
  // resolved again on every rebuild, the explicit label first (an explicit `null` never falls back), then the text
  // element's text, then the item element's.
  const syncRefs = (items: readonly RegistryItem[]): Map<Element, CompositeMetadata> => {
    const nextMap = new Map<Element, CompositeMetadata>();
    elements.length = 0;
    labels.length = 0;
    for (const item of items) {
      const { registration } = item;
      nextMap.set(item.element, { ...((registration.metadata ?? {}) as Record<string, unknown>), index: item.index });
      elements[item.index] = item.element;
      labels[item.index] =
        registration.label !== undefined ? registration.label : (registration.textElement?.textContent ?? item.element.textContent);
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
    labels,
    register(element, registration = {}) {
      if (disposed) {
        return;
      }
      map.set(element, {
        index: registration.index ?? null,
        metadata: registration.metadata ?? null,
        label: registration.label,
        textElement: registration.textElement ?? null,
      });
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
      labels.length = 0;
      map.clear();
      listeners.clear();
      latest = new Map();
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The order report (CMP-EVT-03 as ids, M3 track 1's E2; design point 5 (b) of M5 track 1, chosen by the user
// 2026-11-04: chunked, so no JavaScript → C# message reaches Server's default 32 KB receive limit)
// ---------------------------------------------------------------------------------------------------------------

/**
 * The largest order report payload sent whole, in characters of JSON: a longer one goes in parts. Escaping doubles a
 * quote and a non-ASCII id takes up to three bytes per character, so a part stays below 25 KB on the wire.
 */
export const ORDER_REPORT_CHUNK_CHARS = 8192;

// Characters a part's envelope adds beyond its ids (`{"ids":[`, `],"publication":N,"part":N,"parts":N}`), with room.
const ORDER_REPORT_ENVELOPE_CHARS = 96;

/**
 * Sends one publication's order: whole when its payload fits `ORDER_REPORT_CHUNK_CHARS`, else in parts, each with the
 * publication's number, its position and the number of parts, which C# joins in order before acting on the order.
 * @param channel The twin channel.
 * @param ids The ids in index order.
 * @param publication The publication's number, which grows with each report of the handle.
 */
function reportItemOrder(channel: TwinChannel, ids: string[], publication: number): void {
  if (JSON.stringify({ ids }).length <= ORDER_REPORT_CHUNK_CHARS) {
    channel.report('OnItemsChange', { ids });
    return;
  }
  const parts: string[][] = [];
  let part: string[] = [];
  let length = ORDER_REPORT_ENVELOPE_CHARS;
  for (const id of ids) {
    const added = JSON.stringify(id).length + 1;
    if (part.length > 0 && length + added > ORDER_REPORT_CHUNK_CHARS) {
      parts.push(part);
      part = [];
      length = ORDER_REPORT_ENVELOPE_CHARS;
    }
    part.push(id);
    length += added;
  }
  parts.push(part);
  parts.forEach((chunk, index) => channel.report('OnItemsChange', { ids: chunk, publication, part: index, parts: parts.length }));
}

// ---------------------------------------------------------------------------------------------------------------
// The root (attach): CompositeTwin's handle
// ---------------------------------------------------------------------------------------------------------------

/**
 * One item as C# registers it: the id of its element, the consumer's `tabindex`, which the module never overrides, and
 * the item's flags, from which the module derives the indices navigation skips when the root asks it to.
 */
export interface CompositeItemRegistration {
  /** The id of the item's element, resolved from the root's root node. */
  id: string;
  /** The consumer's `tabindex`, or `null` when the module writes the roving value. */
  tabIndex?: string | null | undefined;
  /** Whether the item is disabled; absent for `false`. */
  disabled?: boolean | undefined;
  /** Whether the item stays navigable and can hold the tab stop while disabled; absent for `false`. */
  focusableWhenDisabled?: boolean | undefined;
  // CMP-STA-06.
  /**
   * The item's explicit label; the empty string for an item with no label that must not fall back to its text; absent to
   * read the text of `textId`'s element or of the item's element.
   */
  label?: string | undefined;
  /** The id of the element whose text is the label when there is no explicit label; absent for the item's element. */
  textId?: string | undefined;
  // CMP-STA-04, CMP-STA-17.
  /** The item's explicit index; absent for an automatic index in document order. */
  index?: number | undefined;
  /** Whether the item renders no `tabindex` and the module writes none; absent for `false`. */
  omitTabIndex?: boolean | undefined;
}

// ---------------------------------------------------------------------------------------------------------------
// The list handle's options (ADR 04 Amendments 2026-11-03 (1), chosen by the user; CMP §6.8, §6.9; M5 track 1's names,
// design points of 2026-11-04). Each is absent when it has Base UI's default (useListNavigation.ts:244-267,
// useTypeahead.ts:73-83).
// ---------------------------------------------------------------------------------------------------------------

/** A highlight C# asks the list for: an index, or `null` to clear, applied once per revision. */
export interface CompositeHighlightRequest {
  /** The index to highlight; absent or `null` to clear the highlight. */
  index?: number | null | undefined;
  /** The request's number, which grows with each request. */
  revision: number;
}

/** The typeahead of a list. */
export interface CompositeTypeaheadOptions {
  /** The time in milliseconds after the last key at which a new search starts. */
  resetMs: number;
  /**
   * The items a search skips: `element` for a hidden or natively disabled element (Menu), `attribute` for
   * `isElementDisabled` (Select); absent for none.
   */
  skip?: 'element' | 'attribute' | undefined;
  /** The labels by index, sent by C# (Combobox's `items`); absent for the registered items' labels. */
  labels?: ReadonlyArray<string | null> | undefined;
}

/** The list navigation options every handle of one list carries. */
export interface CompositeListOptions {
  /** `false` to turn the handle's navigation off; absent for `true`. */
  enabled?: boolean | undefined;
  /** Whether a step past either end wraps; absent for `false`. */
  loopFocus?: boolean | undefined;
  /** The arrow keys that move the highlight; absent for `vertical`. */
  orientation?: Orientation | undefined;
  /** The arrow keys that open the list from its trigger; absent for `orientation`. */
  triggerOrientation?: Orientation | undefined;
  /** Whether the reading direction is right to left; absent for `false`. */
  rtl?: boolean | undefined;
  /** Whether the highlight moves without moving the focus; absent for `false`. */
  virtual?: boolean | undefined;
  /** Whether the list is nested in another list; absent for `false`. */
  nested?: boolean | undefined;
  /** The parent list's orientation; absent to read what the parent popup's list published. */
  parentOrientation?: Orientation | undefined;
  /** Whether an item is highlighted on open; absent for `'auto'` (a keyboard open only). */
  focusItemOnOpen?: boolean | undefined;
  /** Whether a step past either end clears the highlight under virtual focus; absent for `false`. */
  allowEscape?: boolean | undefined;
  /** `false` to keep the closed list closed on an arrow key; absent for `true`. */
  openOnArrowKeyDown?: boolean | undefined;
  /** `false` to ignore the pointer moving over an item; absent for `true`. */
  focusItemOnHover?: boolean | undefined;
  /** `false` to keep the highlight when the pointer leaves an item; absent for `true`. */
  resetOnPointerLeave?: boolean | undefined;
  /** The indices navigation skips; empty for none; absent to read the items' disabled state from the DOM. */
  disabledIndices?: number[] | undefined;
  /** Whether the skipped indices are derived from the items' flags; absent for `false`. */
  deriveDisabledIndices?: boolean | undefined;
  /** The selected item's index; absent when none is selected. */
  selectedIndex?: number | undefined;
  /** Whether the items are a grid; absent for `false`. */
  grid?: boolean | undefined;
  // CMP-ALG-13.
  /**
   * The grid's number of columns; absent for 2, the navigator Combobox passes unwrapped; C#'s `Cols`.
   */
  cols?: number | undefined;
  /**
   * The list's length when the rendered items are a window of it (an externally virtualized list, Base UI's
   * `listRef.current.length`); absent for the registered items' extent; C#'s `ItemCount`.
   */
  itemCount?: number | undefined;
  /** Whether a nested list that is its menu's root stays open on the nested close key; absent for `false`. */
  keepOpenOnCrossOrientationKey?: boolean | undefined;
  /** The references index of the element that takes the focus when a nested list closes; absent for the reference. */
  nestedReturnFocusElement?: number | undefined;
  /** The references index of the composite root that unstopped keydowns are relayed to; absent for none. */
  relayElement?: number | undefined;
  /** The latest highlight C# asked for; absent before any. */
  highlight?: CompositeHighlightRequest | undefined;
  /** The typeahead; absent for none. */
  typeahead?: CompositeTypeaheadOptions | undefined;
}

/** The reference role: the active trigger, or the input under virtual focus, which carries `aria-activedescendant`. */
export type CompositeReferenceOptions = Record<string, never>;

/** The trigger role: a trigger that opens the list but is not its reference. */
export type CompositeTriggerOptions = Record<string, never>;

/** The floating role: the list's element; its items are the handle's `items`, reported in order with `reportItems`. */
export type CompositeFloatingOptions = Record<string, never>;

/** The report of a list's highlight, once per change. */
export interface CompositeListHighlightReport {
  /** The highlighted index, or `null`. */
  index: number | null;
  /** The highlighted item's element id, or `null`. */
  itemId: string | null;
  /** The event that moved the highlight, or `null`. */
  event: EventInfo | null;
  /** The revision of the last C# highlight request applied; 0 before any. */
  revision: number;
  // Design point 9 (a) of M5 track 6 (settled by the orchestrator 2026-11-04): Menu's onItemHighlighted label.
  /** The highlighted item's label, its registered label else its text; absent when no item is highlighted or it has none. */
  label?: string;
}

/** The request to open or close a list's popup. */
export interface CompositeListOpenRequestReport {
  /** Whether the popup is to open. */
  open: boolean;
  /** `list-navigation` or `focus-out`. */
  reason: 'list-navigation' | 'focus-out';
  /** The keydown. */
  event: EventInfo;
  /** The trigger's element id for an open, or `null`. */
  triggerId: string | null;
}

/** The report of a typeahead match while the list is closed. */
export interface CompositeListMatchReport {
  /** The matching index. */
  index: number;
  /** The keydown. */
  event: EventInfo;
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
  /**
   * Whether the indices navigation skips are derived from the items' flags, those that are disabled and not focusable
   * when disabled, in place of `disabledIndices`.
   */
  deriveDisabledIndices?: boolean | undefined;
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
  /** Whether the module reports the items' ids in index order on each publication that changed them. */
  reportItems?: boolean | undefined;
  /**
   * Whether the root is a list without a roving group: read once at `attach`. The module keeps the registry and reports
   * the order, and installs no listener, writes no `tabindex` and keeps no highlight.
   */
  registrationOnly?: boolean | undefined;
  /**
   * Whether the highlight follows the item marked active when focus is outside the root and the item is not
   * `aria-disabled="true"`, except once after a keydown on that item; the re-validation against the disabled indices does
   * not run.
   */
  followActiveItem?: boolean | undefined;
  /** The token of the popup session a list handle joins; absent for a roving group. */
  sessionToken?: string | undefined;
  /** The list navigation options of a list handle; absent for a roving group. */
  list?: CompositeListOptions | undefined;
  /** The reference role; absent for another role. */
  reference?: CompositeReferenceOptions | undefined;
  /** The trigger role; absent for another role. */
  trigger?: CompositeTriggerOptions | undefined;
  /** The floating role; absent for another role. */
  floating?: CompositeFloatingOptions | undefined;
}

// A symbol on the element, not a module registry: a repeated attach of the same element returns the live handle.
const attachedHandle = Symbol('blazix.composite.handle');

// CMP-KEY-14 (M5 track 1, Deliverable 7; design point 3 (a), settled by the orchestrator 2026-11-04): a roving root
// publishes its key resolution on its element, Base UI's `relayKeyboardEvent` (useCompositeRoot.ts:338), so no module
// registry is needed (ADR 04 §3).
const keyboardRelay = Symbol('blazix.composite.relay');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
  [keyboardRelay]?: (event: KeyboardEvent) => boolean;
}

/**
 * Runs a composite root's key resolution for a keydown that did not bubble through the root element, such as one from a
 * menu popup portaled outside a menubar, and stops the event when the root handles it.
 * @param rootElement The root's element; an element with no attached root handles nothing.
 * @param event The keydown.
 * @returns `true` when the root moved its highlight for the key.
 */
export function relayKeyboardEvent(rootElement: Element | null | undefined, event: KeyboardEvent): boolean {
  const relay = (rootElement as AttachedElement | null | undefined)?.[keyboardRelay];
  if (!relay) {
    return false;
  }
  const handled = relay(event);
  if (handled) {
    event.stopPropagation();
  }
  return handled;
}

/** What the follow last saw of one item: the dependencies of its evaluation. */
interface FollowState {
  active: boolean;
  index: number;
  disabled: boolean;
}

interface AttachedItem {
  id: string;
  element: HTMLElement;
  tabIndex: string | null;
  disabled: boolean;
  focusableWhenDisabled: boolean;
  omitTabIndex: boolean;
  label: string | null | undefined;
  textId: string | null;
  index: number | null;
}

// The registry's part of an item registration: a change re-registers the item (CMP-STA-07, CMP-STA-17).
function sameRegistryFields(a: AttachedItem | undefined, b: AttachedItem): boolean {
  return a !== undefined && a.element === b.element && a.label === b.label && a.textId === b.textId && a.index === b.index;
}

// CMP-STA-06 at the C# boundary (M5 track 1): C# sends an explicit "no label" as the empty string.
function registeredLabel(label: string | undefined): string | null | undefined {
  return label === '' ? null : label;
}

function sameIndices(a: ReadonlyArray<number> | null | undefined, b: ReadonlyArray<number> | null | undefined): boolean {
  if (a == null || b == null) {
    return a == null && b == null;
  }
  return a.length === b.length && a.every((value, index) => value === b[index]);
}

/**
 * Attaches a roving group to a root element, or, when the options carry `list`, a list handle in its role. From here on
 * the module resolves the items C# registers, keeps their order, owns their `tabindex` (except where the consumer set
 * one or the item takes none), handles the navigation keys, follows focus and, when asked, the pointer, and reports
 * highlight changes, focus leaving the root and the items' order. With `registrationOnly` it only resolves the items,
 * keeps their order and reports it. A list handle joins its popup's interactions session; see `attachList`.
 * @param elementOrId The root element, or its id when the part has no element reference.
 * @param optionsJson The `CompositeOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param references The references: a list handle's return-focus element and relay root, by the indices its options name.
 * @param captures The capture ids C# sends beside the element and the references, which find them inside a shadow root.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const options = parseOptions<CompositeOptions>(optionsJson);
  if (options.list) {
    return attachList(elementOrId, options, twinReference, references, captures);
  }
  return attachGroup(elementOrId, options, twinReference, captures);
}

function attachGroup(
  elementOrId: Element | string | null,
  initialOptions: CompositeOptions,
  twinReference: DotNetTwin,
  captures?: TwinCaptures,
): TwinHandle {
  const root = resolveElement(elementOrId, captures?.[0]) as AttachedElement;
  const existing = root[attachedHandle];
  if (existing) {
    return existing;
  }

  const twin = createTwinChannel(twinReference);
  let options = initialOptions;
  // CMP-STR-03 (M3 track 1, E1): a root-less list, such as TabsRoot's panels and AccordionRoot's items.
  const registrationOnly = options.registrationOnly === true;
  let disposed = false;
  const registry = createCompositeRegistry();
  let items = new Map<string, AttachedItem>();
  // Elements whose `tabindex` belongs to the consumer, or that take none (NAV-DOM-21).
  let consumerTabIndexes = new Set<Element>();
  // The registered items by element, for their flags.
  let itemsByElement = new Map<Element, AttachedItem>();
  // CMP-STA-08, TLB-STA-02 (M3 track 1, E3): the indices derived from the items' flags, in the registry's order.
  let derivedDisabledIndices: number[] = [];
  // CMP-EVT-03 (M3 track 1, E2): the id sequence last reported, so a publication that keeps it reports nothing.
  let reportedIds: string[] | null = null;
  let publications = 0;
  // CMP-STA-22's consumer-owned clause as TabsList uses it (M3 track 1, E5): what each item's last evaluation saw, the
  // highlight it saw, the items a keydown reached since their last evaluation (TabsTab.tsx's `isNavigatingRef`), and the
  // observer of the marker.
  let followStates = new Map<Element, FollowState>();
  let followedHighlight: number | null = null;
  const navigating = new Set<Element>();
  let markerObserver: MutationObserver | null = null;
  let following = false;
  let followAgain = false;
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

  // ToolbarRoot.tsx:32-46: the items that are disabled and not focusable when disabled, by index.
  const deriveDisabledIndices = (): number[] => {
    const derived: number[] = [];
    registry.elements.forEach((element, index) => {
      const attached = element ? itemsByElement.get(element) : undefined;
      if (attached?.disabled && !attached.focusableWhenDisabled) {
        derived.push(index);
      }
    });
    return derived;
  };

  // The indices navigation skips under a set of options: derived from the flags, or the root's own.
  const disabledIndicesOf = (current: CompositeOptions): ReadonlyArray<number> | null | undefined =>
    current.deriveDisabledIndices ? derivedDisabledIndices : current.disabledIndices;

  // CMP-DOM-01: "0" on the highlighted index, "-1" elsewhere, never over a consumer's value (RAD-DOM-16). While an item
  // carries a non-modal portal's `data-tabindex` stash (FLT-DOM-18), the value goes into the stash, which restores it
  // (ADR 04 Amendments 2026-10-27, the composite's precedence; the focus-manager track's design point 4).
  const writeTabIndexes = (): void => {
    registry.elements.forEach((element, index) => {
      if (!element || consumerTabIndexes.has(element)) {
        return;
      }
      const value = index === highlightedIndex ? '0' : '-1';
      const attribute = element.hasAttribute('data-tabindex') ? 'data-tabindex' : 'tabindex';
      if (element.getAttribute(attribute) !== value) {
        element.setAttribute(attribute, value);
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
    const disabledIndices = disabledIndicesOf(options);
    // CMP-EDGE-09: a highlight that follows the active item is the consumer's, so it is not re-validated.
    if (disabledIndices == null || !defaultIndexSet || options.followActiveItem) {
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

  // TabsTab.tsx:83-109, run for each item whose dependencies changed since its last run: the marker, its index, the
  // highlight, its `aria-disabled`. A keydown on the item since then only clears that mark. Otherwise a marked item whose
  // index differs from the highlight takes it, without scrolling, when focus is outside the root (across shadow roots)
  // and it is not `aria-disabled="true"`, read from the applied batch: a disabled Tab renders it and never DOM
  // `disabled` (TAB-DOM-10; design point 5, option (a), settled by the orchestrator 2026-10-26). A highlight change made
  // here runs the items again, as Base UI's effects run again on the new highlighted index.
  function evaluateFollow(): void {
    if (!options.followActiveItem || registrationOnly || !defaultIndexSet) {
      return;
    }
    if (following) {
      followAgain = true;
      return;
    }
    following = true;
    try {
      do {
        followAgain = false;
        followPass();
      } while (followAgain);
    } finally {
      following = false;
    }
  }

  function followPass(): void {
    const highlightChanged = followedHighlight !== highlightedIndex;
    followedHighlight = highlightedIndex;
    const next = new Map<Element, FollowState>();
    registry.elements.forEach((element, index) => {
      if (!element) {
        return;
      }
      const state: FollowState = {
        active: element.hasAttribute(ACTIVE_COMPOSITE_ITEM),
        index,
        disabled: element.getAttribute('aria-disabled') === 'true',
      };
      next.set(element, state);
      const previous = followStates.get(element);
      if (!highlightChanged && previous && previous.active === state.active && previous.index === index && previous.disabled === state.disabled) {
        return;
      }
      if (navigating.delete(element)) {
        return;
      }
      if (!state.active || index === highlightedIndex) {
        return;
      }
      const focused = activeElement(ownerDocument(root));
      if (focused && contains(root, focused)) {
        return;
      }
      if (!state.disabled) {
        setHighlightedIndex(index, false);
      }
    });
    followStates = next;
  }

  // Design point 4, option (a), settled by the orchestrator 2026-10-26: a change of the marker, or of an item's
  // `aria-disabled`, in the applied batch, pre-paint on both modes.
  const syncMarkerObserver = (): void => {
    const wanted = options.followActiveItem === true && !registrationOnly && typeof MutationObserver === 'function';
    if (wanted && !markerObserver) {
      markerObserver = new MutationObserver(() => evaluateFollow());
      markerObserver.observe(root, { subtree: true, attributes: true, attributeFilter: [ACTIVE_COMPOSITE_ITEM, 'aria-disabled'] });
    } else if (!wanted && markerObserver) {
      markerObserver.disconnect();
      markerObserver = null;
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
      evaluateFollow();
    }
  }

  // CMP-EVT-03 as a report (M3 track 1, E2): the ids in index order, on each publication that changed them, only when
  // asked (ADR 16 rule 6). Subscribed first, so it runs before the root's adjustment of the same publication, as
  // CompositeRoot.tsx:88-91 calls `onMapChange` before its own handler.
  registry.subscribe(() => {
    if (!options.reportItems && !registrationOnly) {
      return;
    }
    const ids: string[] = [];
    for (const element of registry.elements) {
      const attached = element ? itemsByElement.get(element) : undefined;
      if (attached) {
        ids.push(attached.id);
      }
    }
    if (reportedIds && ids.length === reportedIds.length && ids.every((id, index) => id === reportedIds![index])) {
      return;
    }
    reportedIds = ids;
    publications += 1;
    reportItemOrder(twin, ids, publications);
  });

  // The root's listener on each publication; a registration-only list has no highlight to adjust.
  registry.subscribe((map) => {
    if (registrationOnly) {
      return;
    }

    // E3: derived in the same pass as the publication, before the adjustments read it (TLB §14).
    if (options.deriveDisabledIndices) {
      derivedDisabledIndices = deriveDisabledIndices();
    }

    if (map.size === 0) {
      return;
    }

    adjustHighlight(map);

    // CMP-STA-29: Base UI's derived array is new after every publication, so its re-validation effect runs again.
    if (options.deriveDisabledIndices) {
      revalidateHighlight();
    }

    // E5: a publication changes the items' indexes, and may register a marked item.
    evaluateFollow();
  });

  // CMP-ALG-18, CMP-STA-23..28.
  function adjustHighlight(map: Map<Element, CompositeMetadata>): void {
    const elements = registry.elements;
    const disabledIndices = disabledIndicesOf(options);

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
  }

  // Resolves the registered ids; registers new and replaced elements and unregisters removed ones. Returns whether the
  // registry changed, so one `update` rebuilds once (CMP-STA-10).
  const syncItems = (registrations: ReadonlyArray<CompositeItemRegistration>): boolean => {
    const next = new Map<string, AttachedItem>();
    for (const registration of registrations) {
      const element = resolveItem(registration.id);
      if (element) {
        next.set(registration.id, {
          id: registration.id,
          element,
          tabIndex: registration.tabIndex ?? null,
          disabled: registration.disabled ?? false,
          focusableWhenDisabled: registration.focusableWhenDisabled ?? false,
          omitTabIndex: registration.omitTabIndex ?? false,
          label: registeredLabel(registration.label),
          textId: registration.textId ?? null,
          index: registration.index ?? null,
        });
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
      if (!sameRegistryFields(items.get(id), attached)) {
        // CMP-STA-04, CMP-STA-06, CMP-STA-17 (M5 track 1): the explicit index and the label's source from C#.
        registry.register(attached.element, {
          index: attached.index,
          label: attached.label,
          textElement: attached.textId === null ? null : resolveItem(attached.textId),
        });
        changed = true;
      }
    });

    items = next;
    consumerTabIndexes = new Set();
    itemsByElement = new Map();
    next.forEach((attached) => {
      itemsByElement.set(attached.element, attached);
      // NAV-DOM-21 (M5 track 1, Deliverable 10): an item registered without a tabindex keeps its native focusability.
      if (attached.tabIndex != null || attached.omitTabIndex) {
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
    // E5: TabsTab.tsx's capture-phase `isNavigatingRef`, set by any keydown that reaches the item.
    if (options.followActiveItem) {
      const item = itemOf(event);
      if (item) {
        navigating.add(item);
      }
    }
  };

  // CMP-ALG-14 without the grid branch (no consumer, CMP §10 Q1). Returns whether it acted, for the relay (CMP-KEY-14).
  const onKeyDown = (event: KeyboardEvent): boolean => {
    const current = options;
    const isHomeOrEnd = event.key === HOME || event.key === END;
    if (!COMPOSITE_KEYS.has(event.key) || (!current.enableHomeAndEndKeys && isHomeOrEnd)) {
      return false;
    }

    if (isModifierKeySet(event, current.modifierKeys)) {
      return false;
    }

    // CMP-EDGE-08
    if (!root.isConnected) {
      return false;
    }

    const isRtl = current.direction === 'rtl';
    const { orientation } = current;
    const disabledIndices = disabledIndicesOf(current);

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
        return false;
      }
      // Moving forward and not at the last caret position.
      if (event.key !== backwardKey && selectionStart < textContent.length) {
        return false;
      }
      // Moving backward and not at the first caret position.
      if (event.key !== forwardKey && selectionStart > 0) {
        return false;
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
      return false;
    }

    // The consumer's veto, asked only where the module is about to act (EVD-EVT-26; Server: run).
    if (decide(twin, NAVIGATE_POINT, event, 'run') !== 'run') {
      return false;
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
    return true;
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

    // The item's hidden input, found as the `button` module's bridge finds it (RAD-EDGE-09: under a render callback it may
    // follow an element that contains the item).
    const input = findHiddenInput(item);
    if (input) {
      const { disabled, readOnly } = input;
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
    ['keydown', ((event: KeyboardEvent) => void onKeyDown(event)) as EventListener, false],
    ['focusin', onFocusIn as EventListener, false],
    ['focusout', onFocusOut as EventListener, false],
    ['mousemove', onMouseMove as EventListener, false],
  ];

  if (!registrationOnly) {
    for (const [type, listener, capture] of listeners) {
      root.addEventListener(type, listener, capture);
    }
    // CMP-KEY-14: the relay runs the same key resolution, with the options of the latest `update` and the root's twin, so
    // a relayed key asks the root's `navigate` point and sends the root's handled-key report (option (i)).
    root[keyboardRelay] = onKeyDown;
  }

  // The first publication (CMP-STA-23..25, ALG-18).
  syncItems(options.items);
  registry.flush();
  if (!registrationOnly) {
    writeTabIndexes();
  }
  syncMarkerObserver();

  const handle: TwinHandle = {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      const previousDisabledIndices = disabledIndicesOf(options);
      options = parseOptions<CompositeOptions>(nextOptionsJson);
      if (syncItems(options.items)) {
        registry.flush();
      }
      if (registrationOnly) {
        return;
      }
      // E3: a flag change re-derives without a publication.
      if (options.deriveDisabledIndices) {
        derivedDisabledIndices = deriveDisabledIndices();
      }
      writeTabIndexes();
      if (!sameIndices(previousDisabledIndices, disabledIndicesOf(options))) {
        revalidateHighlight();
      }
      // E5: an update may turn the option on or off, and may register a marked item.
      syncMarkerObserver();
      evaluateFollow();
      reportHighlightChange();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete root[attachedHandle];
      if (root[keyboardRelay] === onKeyDown) {
        delete root[keyboardRelay];
      }
      for (const [type, listener, capture] of listeners) {
        root.removeEventListener(type, listener, capture);
      }
      markerObserver?.disconnect();
      markerObserver = null;
      registry.dispose();
      items = new Map();
    },
  };
  root[attachedHandle] = handle;
  return handle;
}

// ---------------------------------------------------------------------------------------------------------------
// List navigation (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1)-(3), chosen by the user; CMP §4.2.4,
// §4.2.5, §4.6-§4.9, §5.4-§5.6; useListNavigation.ts and useTypeahead.ts). One handle per element role (design point
// 1): the reference (the active trigger, or the input under virtual focus), each other trigger, and the floating
// element, which owns the registry and the items' listeners by delegation; items attach nothing. The refs Base UI's
// hooks keep in one closure are the list state, kept in the popup session's `dataRef` under a key this module owns, so
// every handle of one list shares them whatever their attach order. The module writes the highlight (the items'
// `tabindex` and `data-highlighted`, and under virtual focus `aria-activedescendant`) in the task that moves the focus,
// and reports it once per change; C# mirrors it and asks for its own highlights by revision (CMP-STA-32).
// ---------------------------------------------------------------------------------------------------------------

/** The `dataRef` key that holds a list's shared state (the `interactions` session data a module owns by its key). */
const LIST_STATE = 'blazix.composite.list';

const TAB = 'Tab';
const ESCAPE = 'Escape';

/** The report of a list's highlight. */
export const LIST_HIGHLIGHT_REPORT = 'OnListHighlight';

/** The report of a list's open or close request. */
export const LIST_OPEN_REQUEST_REPORT = 'OnListOpenRequest';

/** The report of a typeahead match while the list is closed. */
export const LIST_MATCH_REPORT = 'OnListMatch';

function doSwitch(orientation: Orientation | undefined, vertical: boolean, horizontal: boolean): boolean {
  switch (orientation) {
    case 'vertical':
      return vertical;
    case 'horizontal':
      return horizontal;
    default:
      return vertical || horizontal;
  }
}

// CMP-ALG-20 (useListNavigation.ts:59-100), exported for Menu's filter keydown (fact sheet A contradiction 8).
/**
 * Determines whether a key moves along a list's orientation.
 * @param key The key.
 * @param orientation The orientation; absent for both axes.
 * @returns `true` for the arrow keys of the orientation's axis.
 */
export function isMainOrientationKey(key: string, orientation: Orientation | undefined): boolean {
  const vertical = key === ARROW_UP || key === ARROW_DOWN;
  const horizontal = key === ARROW_LEFT || key === ARROW_RIGHT;
  return doSwitch(orientation, vertical, horizontal);
}

/**
 * Determines whether a key moves towards a list's end, or is Enter, Space or the empty key.
 * @param key The key.
 * @param orientation The orientation; absent for both axes.
 * @param rtl Whether the reading direction is right to left.
 * @returns `true` for ArrowDown, the forward horizontal arrow, Enter, Space and `''`.
 */
export function isMainOrientationToEndKey(key: string, orientation: Orientation | undefined, rtl: boolean): boolean {
  const vertical = key === ARROW_DOWN;
  const horizontal = rtl ? key === ARROW_LEFT : key === ARROW_RIGHT;
  return doSwitch(orientation, vertical, horizontal) || key === 'Enter' || key === ' ' || key === '';
}

/**
 * Determines whether a key opens a nested list across its parent's orientation.
 * @param key The key.
 * @param orientation The parent's orientation; absent for both axes.
 * @param rtl Whether the reading direction is right to left.
 * @returns `true` for the forward horizontal arrow under a vertical parent, ArrowDown under a horizontal one.
 */
export function isCrossOrientationOpenKey(key: string, orientation: Orientation | undefined, rtl: boolean): boolean {
  const vertical = rtl ? key === ARROW_LEFT : key === ARROW_RIGHT;
  const horizontal = key === ARROW_DOWN;
  return doSwitch(orientation, vertical, horizontal);
}

/**
 * Determines whether a key closes a nested list across its own orientation.
 * @param key The key.
 * @param orientation The nested list's orientation; absent for both axes.
 * @param rtl Whether the reading direction is right to left.
 * @param grid Whether the list is a grid.
 * @returns `true` for the backward horizontal arrow in a vertical list, ArrowUp in a horizontal one, and Escape in a
 * list of both axes or a horizontal grid.
 */
export function isCrossOrientationCloseKey(key: string, orientation: Orientation | undefined, rtl: boolean, grid: boolean): boolean {
  const vertical = rtl ? key === ARROW_RIGHT : key === ARROW_LEFT;
  const horizontal = key === ARROW_UP;
  if (orientation === 'both' || (orientation === 'horizontal' && grid)) {
    return key === ESCAPE;
  }
  return doSwitch(orientation, vertical, horizontal);
}

// The event's own preventDefault and stopPropagation (floating-ui-react/utils/event.ts `stopEvent`).
function stopEvent(event: Event): void {
  event.preventDefault();
  event.stopPropagation();
}

// useListNavigation.ts:36-38: WebKit fires zero-delta moves when the list scrolls under a still pointer (CMP-ALG-29).
function isStationaryWebKitPointer(event: MouseEvent): boolean {
  return platform.engine.webkit && event.movementX === 0 && event.movementY === 0;
}

// floating-ui-react/utils/element.ts `isTypeableCombobox`.
function isTypeableCombobox(element: Element | null | undefined): boolean {
  return element != null && element.getAttribute('role') === 'combobox' && isTypeableElement(element);
}

function setAttribute(element: Element, name: string, value: string | null): void {
  if (value === null) {
    if (element.hasAttribute(name)) {
      element.removeAttribute(name);
    }
  } else if (element.getAttribute(name) !== value) {
    element.setAttribute(name, value);
  }
}

function resolveById(scopeElement: Element, id: string): HTMLElement | null {
  const scope = scopeElement.getRootNode();
  const found =
    'getElementById' in scope && typeof (scope as Document | ShadowRoot).getElementById === 'function'
      ? (scope as Document | ShadowRoot).getElementById(id)
      : null;
  const element = found ?? ownerDocument(scopeElement).getElementById(id);
  return isHTMLElement(element) ? element : null;
}

/** The floating handle's half of a list: its element, its registry and its items. */
interface ListFloating {
  readonly element: HTMLElement;
  readonly registry: CompositeRegistry;
  readonly channel: TwinChannel;
  /** Whether the module writes the element's `tabindex` (not a consumer's, not an item registered without one). */
  ownsTabIndex(element: Element): boolean;
  /** The registered id of an item element. */
  idOf(element: Element | null | undefined): string | null;
  /** The indices derived from the items' flags, in the registry's order. */
  derivedDisabledIndices(): number[];
  // CMP-KEY-27.
  /** Runs the floating handle's keydown handling for a keydown a portaled child list handed up. */
  keyDown(event: KeyboardEvent): void;
}

/** A reference or trigger handle's half of a list. */
interface ListReference {
  readonly element: HTMLElement;
  /** Whether the handle is the reference, which carries `aria-activedescendant` under virtual focus. */
  readonly isReference: boolean;
}

/** The typeahead's refs (useTypeahead.ts:57-60) and the typing flag Base UI reports through `onTyping`. */
interface TypeaheadState {
  text: string;
  prevIndex: number | null;
  matchIndex: number | null;
  timer: ReturnType<typeof setTimeout> | null;
  typing: boolean;
}

/** The refs of one list, shared by its handles. */
interface ListState {
  /** `indexRef`, the cursor; -1 for none. */
  index: number;
  /** Base UI's `activeIndex`: the highlight the module wrote and last reported. */
  activeIndex: number | null;
  /** `keyRef`, the navigation key recorded on the trigger. */
  key: string | null;
  pointerModality: boolean;
  forceSyncFocus: boolean;
  forceScrollIntoView: boolean;
  /** `focusItemOnOpenRef`. */
  focusItemOnOpen: boolean | 'auto';
  /** The open state an `openchange` carries while it is emitted, before the session applies it; `null` otherwise. */
  emittingOpen: boolean | null;
  previousOpen: boolean;
  previousMounted: boolean;
  cancelQueuedFocus: (() => void) | null;
  readonly focusFrame: FrameSlot;
  readonly waitFrame: FrameSlot;
  floating: ListFloating | null;
  references: Set<ListReference>;
  /** Every live handle of the list, the most recent last. */
  handles: Set<ListHandle>;
  /** The revision of the last C# highlight request applied. */
  appliedRevision: number;
  /** The highlight last reported, so a report goes out once per change; `undefined` before any. */
  reportedIndex: number | null | undefined;
  /** The elements that carry the `aria-activedescendant` the module wrote under virtual focus. */
  readonly descendantTargets: Set<Element>;
  readonly typeahead: TypeaheadState;
  readonly onOpenChange: (details: SessionOpenChangeDetails) => void;
}

/** One list handle as the shared logic sees it: its element, its channel and its current options. */
interface ListHandle {
  readonly element: HTMLElement;
  readonly channel: TwinChannel;
  readonly session: InteractionSession;
  options(): CompositeOptions;
  list(): CompositeListOptions;
  returnFocusElement(): HTMLElement | null;
}

type ListData = { [LIST_STATE]?: ListState };

// CMP §8: read at event time.
/** Base UI's `open`: the session's committed state, or the one its `openchange` is emitting, read at event time. */
function isOpen(state: ListState, session: InteractionSession): boolean {
  return state.emittingOpen ?? session.open;
}

function listStateOf(session: InteractionSession): ListState | undefined {
  return (session.dataRef.current as ListData)[LIST_STATE];
}

/** The list options of any live handle: every handle of one list carries the same options. */
function currentHandle(state: ListState): ListHandle | null {
  let last: ListHandle | null = null;
  for (const handle of state.handles) {
    last = handle;
  }
  return last;
}

function enabledOf(list: CompositeListOptions): boolean {
  return list.enabled !== false;
}

function orientationOf(list: CompositeListOptions): Orientation {
  return list.orientation ?? 'vertical';
}

function selectedIndexOf(list: CompositeListOptions): number | null {
  return list.selectedIndex ?? null;
}

function focusItemOnOpenOf(list: CompositeListOptions): boolean | 'auto' {
  return list.focusItemOnOpen ?? 'auto';
}

function elementsOf(state: ListState): Array<HTMLElement | null> {
  const elements = state.floating?.registry.elements ?? [];
  // A virtualized list's unrendered indices are empty slots of its full length (CMP-ALG-08's virtualized gaps).
  const itemCount = currentHandle(state)?.list().itemCount;
  if (itemCount != null && itemCount > elements.length) {
    return Array.from({ length: itemCount }, (_, index) => elements[index] ?? null);
  }
  return elements;
}

// CMP-ALG-13 (M5 track 1; floating-ui-react/hooks/gridNavigation.ts:17-47): the popup grid's navigator, uniform 1x1
// cells, `cols` 2 unless the caller binds another count.
/**
 * Computes the index a grid key moves a popup list's highlight to.
 * @param event The keydown; a key the grid handles is prevented and stopped.
 * @param prevIndex The cursor.
 * @param list The elements array.
 * @param orientation The list's orientation.
 * @param loopFocus Whether a step past either end wraps.
 * @param rtl Whether the reading direction is right to left.
 * @param disabledIndices The indices to skip.
 * @param minIndex The first navigable index.
 * @param maxIndex The last navigable index.
 * @param cols The number of columns; 2 by default.
 * @returns The index, or `undefined` for none (an out-of-bounds result).
 */
export function gridNavigation(
  event: KeyboardEvent,
  prevIndex: number,
  list: ReadonlyArray<HTMLElement | null | undefined>,
  orientation: Orientation,
  loopFocus: boolean,
  rtl: boolean,
  disabledIndices: DisabledIndices | undefined,
  minIndex: number,
  maxIndex: number,
  cols = 2,
): number | undefined {
  const nextIndex = getGridNavigatedIndex(list, {
    event,
    orientation,
    loopFocus,
    rtl,
    cols,
    disabledIndices,
    minIndex,
    maxIndex,
    // CMP-EDGE-17: an out-of-range previous index falls back to the first enabled item.
    prevIndex: prevIndex > maxIndex ? minIndex : prevIndex,
    stopEvent: true,
  });

  // `getGridNavigatedIndex` can return an out-of-bounds sentinel (e.g. `-1` when there is no previous item to move from);
  // surface that as `undefined` so the caller treats it as "no navigation" rather than highlighting index `-1`.
  return isIndexOutOfListBounds(list, nextIndex) ? undefined : nextIndex;
}

function disabledIndicesOfList(state: ListState, list: CompositeListOptions): ReadonlyArray<number> | undefined {
  if (list.deriveDisabledIndices) {
    return state.floating?.derivedDisabledIndices() ?? [];
  }
  return list.disabledIndices;
}

/** The reference: the reference handle's element, else the session's reference element. */
function referenceElementOf(state: ListState, session: InteractionSession): Element | null {
  for (const reference of state.references) {
    if (reference.isReference) {
      return reference.element;
    }
  }
  return session.domReferenceElement;
}

// The floating half of the list in the parent popup's session, when that popup has one.
function parentListFloatingOf(session: InteractionSession): ListFloating | null {
  const parentId = session.node.parentId;
  const parent = parentId ? getSession(parentId) : undefined;
  return parent ? (listStateOf(parent)?.floating ?? null) : null;
}

// CMP-STA-41: the parent popup's published orientation, when the options do not name it.
function parentOrientationOf(handle: ListHandle): Orientation | undefined {
  const list = handle.list();
  if (list.parentOrientation) {
    return list.parentOrientation;
  }
  const parentId = handle.session.node.parentId;
  const parent = parentId ? getSession(parentId) : undefined;
  return parent?.dataRef.current.orientation as Orientation | undefined;
}

// The writer of the highlight (ADR 04 Amendments 2026-11-03 (2)): each item's `data-highlighted`, and its `tabindex`
// (`0` on the highlighted item of an open list under DOM focus, `-1` elsewhere; into a non-modal portal's `data-tabindex`
// stash while it carries one), and under virtual focus `aria-activedescendant` from the active item's own id on the
// reference and, unless the reference is a typeable combobox, on the floating element (CMP-ARIA-01, CMP-ARIA-02).
function writeHighlight(state: ListState, handle: ListHandle): void {
  const list = handle.list();
  const virtual = list.virtual === true;
  const enabled = enabledOf(list);
  const floating = state.floating;
  if (floating) {
    floating.registry.elements.forEach((element, index) => {
      if (!element) {
        return;
      }
      const highlighted = enabled && index === state.activeIndex;
      setAttribute(element, 'data-highlighted', highlighted ? '' : null);
      if (!floating.ownsTabIndex(element)) {
        return;
      }
      const value = !virtual && isOpen(state, handle.session) && highlighted ? '0' : '-1';
      setAttribute(element, element.hasAttribute('data-tabindex') ? 'data-tabindex' : 'tabindex', value);
    });
  }
  // CMP-ARIA-01, CMP-ARIA-02: the attribute exists only under virtual focus. React drops the prop when it no longer
  // applies, so the module removes what it wrote from an element it no longer targets (DOM focus, another reference, an
  // unmounted floating element), and never touches the attribute of an element it did not write.
  const targets = new Map<Element, string | null>();
  if (virtual) {
    const active = state.activeIndex != null ? elementsOf(state)[state.activeIndex] : null;
    const descendant = enabled && isOpen(state, handle.session) && active?.id ? active.id : null;
    const reference = referenceElementOf(state, handle.session);
    if (reference) {
      targets.set(reference, descendant);
    }
    if (floating) {
      targets.set(floating.element, isTypeableCombobox(reference) ? null : descendant);
    }
  }
  for (const element of state.descendantTargets) {
    if (!targets.has(element)) {
      setAttribute(element, 'aria-activedescendant', null);
      state.descendantTargets.delete(element);
    }
  }
  targets.forEach((value, element) => {
    setAttribute(element, 'aria-activedescendant', value);
    if (value === null) {
      state.descendantTargets.delete(element);
    } else {
      state.descendantTargets.add(element);
    }
  });
}

// CMP-EVT-04 as a report: once per change of the highlight, with the C# revision applied.
function reportHighlight(state: ListState, handle: ListHandle, event: Event | undefined): void {
  if (state.activeIndex === state.reportedIndex) {
    return;
  }
  state.reportedIndex = state.activeIndex;
  const element = state.activeIndex != null ? elementsOf(state)[state.activeIndex] : null;
  // Design point 9 (a) of M5 track 6 (settled by the orchestrator 2026-11-04): the registry's label, resolved on every
  // rebuild (CMP-STA-06/07), sent only for a highlighted item that has one.
  const label = state.activeIndex != null ? state.floating?.registry.labels[state.activeIndex] : null;
  const payload: CompositeListHighlightReport = {
    index: state.activeIndex,
    itemId: state.floating?.idOf(element) ?? null,
    event: event ? eventInfo(event) : null,
    revision: state.appliedRevision,
    ...(label != null ? { label } : {}),
  };
  (state.floating?.channel ?? handle.channel).report(LIST_HIGHLIGHT_REPORT, payload);
}

// F1 of PR 183 (settled by the orchestrator 2026-11-04 as (b)): while open, the item the focus moves to and the element
// focus came from, for a focus manager whose handle attaches after this focus moved focus in (FLT-FOC-20's dated note).
function markOpenFocus(state: ListState, handle: ListHandle, item: HTMLElement): void {
  if (!isOpen(state, handle.session)) {
    return;
  }
  const data = handle.session.dataRef.current;
  const previous = data[LIST_OPEN_FOCUS_KEY] as ListOpenFocus | undefined;
  let origin = previous?.origin ?? null;
  if (previous === undefined) {
    const doc = ownerDocument(item);
    const active = activeElement(doc);
    const floating = state.floating?.element ?? handle.session.floatingElement;
    origin = active === null || active === doc.body || contains(floating, active) ? null : active;
  }
  const mark: ListOpenFocus = { element: item, origin };
  data[LIST_OPEN_FOCUS_KEY] = mark;
}

// useListNavigation.ts:330-371 `focusItem`: the queued focus of CMP-ALG-23/ALG-31 and the scroll of CMP-STA-34.
function focusItem(state: ListState, handle: ListHandle): void {
  state.focusFrame.cancel();
  const virtual = handle.list().virtual === true;
  const runFocus = (item: HTMLElement): void => {
    if (!virtual) {
      markOpenFocus(state, handle, item);
      state.cancelQueuedFocus = enqueueFocus(item, { sync: state.forceSyncFocus, preventScroll: true });
    }
  };
  const initialItem = elementsOf(state)[state.index];
  const forceScrollIntoView = state.forceScrollIntoView;
  if (initialItem) {
    runFocus(initialItem);
  }
  const run = (): void => {
    const waitedItem = elementsOf(state)[state.index] || initialItem;
    if (!waitedItem) {
      return;
    }
    if (!initialItem) {
      runFocus(waitedItem);
    }
    if (forceScrollIntoView || !state.pointerModality) {
      waitedItem.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
    }
  };
  if (state.forceSyncFocus) {
    run();
  } else {
    state.focusFrame.request(run);
  }
}

// The consumer's `activeIndex` update and the effect that follows it (useListNavigation.ts:405-477): the module is
// Base UI's consumer, so a change of the highlight is written, reported and synchronized with the focus in one task.
function commitHighlight(state: ListState, handle: ListHandle, next: number | null, event: Event | undefined): void {
  if (next === state.activeIndex) {
    return;
  }
  state.activeIndex = next;
  writeHighlight(state, handle);
  reportHighlight(state, handle, event);
  syncActiveIndex(state, handle, next);
}

// `onNavigate`: the cursor proposed as the highlight.
function navigateTo(state: ListState, handle: ListHandle, event?: Event): void {
  commitHighlight(state, handle, state.index === -1 ? null : state.index, event);
}

// useListNavigation.ts:407-477 for one value of `activeIndex`: the item focus while open, and the initial sync.
function syncActiveIndex(state: ListState, handle: ListHandle, activeIndex: number | null): void {
  const list = handle.list();
  if (!enabledOf(list)) {
    return;
  }
  if (!isOpen(state, handle.session)) {
    state.forceSyncFocus = false;
    return;
  }
  if (!state.floating) {
    return;
  }
  const elements = elementsOf(state);
  if (activeIndex == null) {
    state.forceSyncFocus = false;
    if (selectedIndexOf(list) != null) {
      return;
    }
    // Reset while the floating element was open (the list changed).
    if (state.previousMounted) {
      state.index = -1;
      focusItem(state, handle);
    }
    // CMP-STA-37, CMP-ALG-24: the initial sync, after the list has a first element.
    const focusOnOpen = state.focusItemOnOpen;
    if ((!state.previousOpen || !state.previousMounted) && focusOnOpen && (state.key != null || (focusOnOpen === true && state.key == null))) {
      let runs = 0;
      const waitForListPopulated = (): void => {
        if (elementsOf(state)[0] == null) {
          // Before paint if possible, then one frame; no more than that (CMP-FOC-09: give up silently).
          if (runs < 2) {
            if (runs) {
              state.waitFrame.request(waitForListPopulated);
            } else {
              queueMicrotask(waitForListPopulated);
            }
          }
          runs += 1;
          return;
        }
        const current = handle.list();
        const rtl = current.rtl === true;
        // `disabledIndices` is deliberately left out, so DOM-disabled items are skipped on open (mui/base-ui#2604).
        state.index =
          state.key == null || isMainOrientationToEndKey(state.key, current.triggerOrientation ?? orientationOf(current), rtl) || current.nested
            ? getMinListIndex(elementsOf(state))
            : getMaxListIndex(elementsOf(state));
        state.key = null;
        navigateTo(state, handle);
      };
      waitForListPopulated();
    }
  } else if (!isIndexOutOfListBounds(elements, activeIndex)) {
    state.index = activeIndex;
    focusItem(state, handle);
    state.forceScrollIntoView = false;
  }
}

// The effects that follow an open change or the floating element's mount or unmount (useListNavigation.ts:374-547).
function syncOpenAndMount(state: ListState, handle: ListHandle): void {
  const list = handle.list();
  const open = isOpen(state, handle.session);
  const mounted = state.floating !== null;
  const renderedActiveIndex = state.activeIndex;
  // :374-376, CMP-STA-41
  handle.session.dataRef.current.orientation = orientationOf(list);
  // :378-385, CMP-STA-38
  if (!open) {
    state.key = null;
    delete handle.session.dataRef.current[LIST_OPEN_FOCUS_KEY];
  }
  const focusItemOnOpen = focusItemOnOpenOf(list);
  if (!open || focusItemOnOpen !== 'auto') {
    state.focusItemOnOpen = focusItemOnOpen;
  }
  if (enabledOf(list)) {
    // :389-403, CMP-STA-33
    const selectedIndex = selectedIndexOf(list);
    if (open && mounted) {
      state.index = selectedIndex ?? -1;
      if (state.focusItemOnOpen && selectedIndex != null) {
        state.forceScrollIntoView = true;
        navigateTo(state, handle);
      }
    } else if (state.previousMounted) {
      state.index = -1;
      navigateTo(state, handle);
    }
    // :407-477 with the highlight of the render that opened or mounted the list.
    syncActiveIndex(state, handle, renderedActiveIndex);
  }
  // :481-503, CMP-FOC-08: a nested child whose floating element unmounted focuses its parent's, so the arrow keys work
  // after the pointer leaves the child.
  if (enabledOf(list) && !mounted && list.virtual !== true && state.previousMounted) {
    const parentId = handle.session.node.parentId;
    const parent = parentId ? getSession(parentId)?.floatingElement : null;
    const nodes = handle.session.tree.nodesRef.current;
    const focused = activeElement(ownerDocument(handle.session.domReferenceElement ?? parent ?? null));
    const treeContainsFocus = nodes.some((node) => node.context?.elements?.floating != null && contains(node.context.elements.floating, focused));
    if (parent && !treeContainsFocus && state.pointerModality) {
      parent.focus({ preventScroll: true });
    }
  }
  // :505-508
  state.previousOpen = open;
  state.previousMounted = mounted;
  writeHighlight(state, handle);
  typeaheadOnOpenOrSelectedChange(state, handle);
}

function createListState(session: InteractionSession, list: CompositeListOptions): ListState {
  const state: ListState = {
    index: selectedIndexOf(list) ?? -1,
    activeIndex: null,
    key: null,
    pointerModality: true,
    forceSyncFocus: false,
    forceScrollIntoView: false,
    focusItemOnOpen: focusItemOnOpenOf(list),
    emittingOpen: null,
    previousOpen: session.open,
    previousMounted: false,
    cancelQueuedFocus: null,
    focusFrame: createFrameSlot(),
    waitFrame: createFrameSlot(),
    floating: null,
    references: new Set(),
    handles: new Set(),
    appliedRevision: 0,
    reportedIndex: null,
    descendantTargets: new Set(),
    typeahead: { text: '', prevIndex: selectedIndexOf(list) ?? -1, matchIndex: null, timer: null, typing: false },
    // FLT-STA-02: the session emits before it applies the state, so the effects read the emitted value.
    onOpenChange: (details) => {
      const handle = currentHandle(state);
      if (!handle) {
        return;
      }
      state.emittingOpen = details.open;
      try {
        syncOpenAndMount(state, handle);
      } finally {
        state.emittingOpen = null;
      }
    },
  };
  session.events.on('openchange', state.onOpenChange);
  (session.dataRef.current as ListData)[LIST_STATE] = state;
  return state;
}

function releaseListState(state: ListState, session: InteractionSession): void {
  if (state.handles.size > 0) {
    return;
  }
  session.events.off('openchange', state.onOpenChange);
  state.focusFrame.cancel();
  state.waitFrame.cancel();
  if (state.typeahead.timer !== null) {
    clearTimeout(state.typeahead.timer);
  }
  state.descendantTargets.clear();
  if ((session.dataRef.current as ListData)[LIST_STATE] === state) {
    delete (session.dataRef.current as ListData)[LIST_STATE];
  }
}

// A C# highlight request (ADR 04 Amendments 2026-11-03 (2)): applied once per revision, as a consumer's `activeIndex`.
function applyHighlightRequest(state: ListState, handle: ListHandle): void {
  const request = handle.list().highlight;
  if (!request || request.revision <= state.appliedRevision) {
    return;
  }
  state.appliedRevision = request.revision;
  const next = request.index ?? null;
  if (next === state.activeIndex) {
    // The revision moved: report it, so C# knows its request was applied.
    state.reportedIndex = undefined;
    reportHighlight(state, handle, undefined);
    return;
  }
  commitHighlight(state, handle, next, undefined);
}

// ---------------------------------------------------------------------------------------------------------------
// Typeahead (CMP §4.2.5, §4.9, §5.5; useTypeahead.ts), wholly in the module: the buffer, the reset timer, the
// matching and the typing flag, which the floating handle's capture listener reads to prevent Space (design point 2).
// ---------------------------------------------------------------------------------------------------------------

function setTyping(state: ListState, typing: boolean): void {
  state.typeahead.typing = typing;
}

// useTypeahead.ts:223-235, CMP-STA-44: a reset on open, and on a close that has no selected index.
function typeaheadOnOpenOrSelectedChange(state: ListState, handle: ListHandle): void {
  const typeahead = handle.list().typeahead;
  const t = state.typeahead;
  if (!typeahead) {
    return;
  }
  if (!isOpen(state, handle.session) && selectedIndexOf(handle.list()) !== null) {
    return;
  }
  if (t.timer !== null) {
    clearTimeout(t.timer);
    t.timer = null;
  }
  t.matchIndex = null;
  if (t.text !== '') {
    t.text = '';
    setTyping(state, false);
  }
}

// useTypeahead.ts:84-222 (CMP-ALG-25, ALG-26; KEY-35..43; EVT-10, EVT-11).
function typeaheadKeyDown(state: ListState, handle: ListHandle, event: KeyboardEvent): void {
  const list = handle.list();
  const options = list.typeahead;
  if (!options) {
    return;
  }
  const t = state.typeahead;
  const elements = state.floating?.registry.elements;
  const listContent: ReadonlyArray<string | null> = options.labels ?? state.floating?.registry.labels ?? [];

  const isItemAvailable = (index: number): boolean => {
    if (options.skip === 'element') {
      const element = elements?.[index];
      return !((element && !isElementVisible(element)) || element?.matches(':disabled'));
    }
    if (options.skip === 'attribute') {
      return !isElementDisabled(elements?.[index] ?? null);
    }
    return true;
  };

  const getMatchingIndex = (content: ReadonlyArray<string | null>, text: string, startIndex = 0): number => {
    if (content.length === 0) {
      return -1;
    }
    const normalizedStartIndex = ((startIndex % content.length) + content.length) % content.length;
    const lowerString = text.toLowerCase();
    for (let offset = 0; offset < content.length; offset += 1) {
      const index = (normalizedStartIndex + offset) % content.length;
      const label = content[index];
      if (!label?.toLowerCase().startsWith(lowerString) || !isItemAvailable(index)) {
        continue;
      }
      return index;
    }
    return -1;
  };

  if (t.text.length > 0 && event.key === ' ') {
    // Space continues the typing session.
    stopEvent(event);
    setTyping(state, true);
  }

  if (t.text.length > 0 && t.text[0] !== ' ') {
    if (getMatchingIndex(listContent, t.text) === -1 && event.key !== ' ') {
      setTyping(state, false);
    }
  }

  if (event.key.length !== 1 || event.ctrlKey || event.metaKey || event.altKey) {
    return;
  }

  if (isOpen(state, handle.session) && event.key !== ' ') {
    stopEvent(event);
    setTyping(state, true);
  }

  const selectedIndex = selectedIndexOf(list);
  const isNewSession = t.text === '';
  if (isNewSession) {
    t.prevIndex = selectedIndex ?? state.activeIndex ?? -1;
  }

  // Rapid succession of one first letter cycles through its items, unless a label repeats its first letter.
  const allowRapidSuccessionOfFirstLetter = listContent.every((text, index) =>
    text && isItemAvailable(index) ? text[0]?.toLowerCase() !== text[1]?.toLowerCase() : true,
  );
  if (allowRapidSuccessionOfFirstLetter && t.text === event.key) {
    t.text = '';
    t.prevIndex = t.matchIndex;
  }

  t.text += event.key;
  if (t.timer !== null) {
    clearTimeout(t.timer);
  }
  t.timer = setTimeout(() => {
    t.timer = null;
    t.text = '';
    t.prevIndex = t.matchIndex;
    setTyping(state, false);
  }, options.resetMs);

  const prevIndex = isNewSession ? (selectedIndex ?? state.activeIndex ?? -1) : t.prevIndex;
  const startIndex = (prevIndex ?? 0) + 1;
  const index = getMatchingIndex(listContent, t.text, startIndex);

  if (index !== -1) {
    onTypeaheadMatch(state, handle, index, event);
    t.matchIndex = index;
  } else if (event.key !== ' ') {
    t.text = '';
    setTyping(state, false);
  }
}

// `onMatch` (CMP-EVT-10): while open the match is a keyboard highlight (MenuRoot.tsx:562-566, SelectRoot.tsx:401-403);
// on a closed list it is the family's value request (SelectRoot.tsx:404-407, ComboboxTrigger.tsx:116-121).
function onTypeaheadMatch(state: ListState, handle: ListHandle, index: number, event: KeyboardEvent): void {
  if (isOpen(state, handle.session)) {
    if (index !== state.activeIndex) {
      commitHighlight(state, handle, index, event);
    }
    return;
  }
  const payload: CompositeListMatchReport = { index, event: eventInfo(event) };
  handle.channel.report(LIST_MATCH_REPORT, payload);
}

// useTypeahead.ts:205-221: focus leaving the reference and the floating element ends the session.
function typeaheadFocusOut(state: ListState, handle: ListHandle, event: FocusEvent): void {
  if (!handle.list().typeahead) {
    return;
  }
  const next = isElement(event.relatedTarget) ? event.relatedTarget : null;
  const reference = referenceElementOf(state, handle.session);
  const floating = state.floating?.element ?? handle.session.floatingElement;
  if (contains(reference, next) || contains(floating, next)) {
    return;
  }
  const t = state.typeahead;
  if (t.timer !== null) {
    clearTimeout(t.timer);
    t.timer = null;
  }
  t.text = '';
  t.prevIndex = t.matchIndex;
  setTyping(state, false);
}

// ---------------------------------------------------------------------------------------------------------------
// The list handle's key handling (useListNavigation.ts:550-912)
// ---------------------------------------------------------------------------------------------------------------

// EVD-EVT-26.
/** One keydown as a handle handles it: the consumer's veto, asked once before the first effect. */
interface KeyDispatch {
  readonly event: KeyboardEvent;
  vetoed: boolean | null;
  stopped: boolean;
}

function proceed(dispatch: KeyDispatch, handle: ListHandle): boolean {
  if (dispatch.vetoed === null) {
    dispatch.vetoed = decide(handle.channel, NAVIGATE_POINT, dispatch.event, 'run') !== 'run';
  }
  return !dispatch.vetoed;
}

function stopDispatch(dispatch: KeyDispatch): void {
  dispatch.stopped = true;
  stopEvent(dispatch.event);
}

function requestOpen(handle: ListHandle, open: boolean, reason: 'list-navigation' | 'focus-out', event: KeyboardEvent, trigger: Element | null): void {
  const payload: CompositeListOpenRequestReport = {
    open,
    reason,
    event: eventInfo(event),
    triggerId: trigger?.id || null,
  };
  handle.channel.report(LIST_OPEN_REQUEST_REPORT, payload);
}

// `commonOnKeyDown` (useListNavigation.ts:550-683).
function commonKeyDown(state: ListState, handle: ListHandle, dispatch: KeyDispatch, currentTarget: Element): void {
  const event = dispatch.event;
  const list = handle.list();
  state.pointerModality = false;
  state.forceSyncFocus = true;

  // CMP-KEY-25: Chrome fires ArrowDown twice while composing.
  if ((event as KeyboardEvent & { which?: number }).which === 229) {
    return;
  }

  // An animating-out floating element ignores navigation.
  if (!isOpen(state, handle.session) && currentTarget === state.floating?.element) {
    return;
  }

  const orientation = orientationOf(list);
  const rtl = list.rtl === true;
  const isGrid = list.grid === true;

  // CMP-KEY-27, CMP-DIS-01, CMP-EVT-07: the nested close across the list's orientation.
  if (list.nested && !list.keepOpenOnCrossOrientationKey && isCrossOrientationCloseKey(event.key, orientation, rtl, isGrid)) {
    if (!proceed(dispatch, handle)) {
      return;
    }
    // The parent navigates with the key when it is its main key; otherwise the key stops here.
    if (!isMainOrientationKey(event.key, parentOrientationOf(handle))) {
      stopDispatch(dispatch);
    }
    requestOpen(handle, false, 'list-navigation', event, null);
    const returnElement = handle.returnFocusElement() ?? referenceElementOf(state, handle.session);
    if (isHTMLElement(returnElement)) {
      returnElement.focus();
    }
    return;
  }

  const elements = elementsOf(state);
  // CMP-STA-32: the cursor follows a highlight C# set since the last key.
  if (state.activeIndex != null && state.activeIndex !== state.index && !isIndexOutOfListBounds(elements, state.activeIndex)) {
    state.index = state.activeIndex;
  }

  const currentIndex = state.index;
  const disabledIndices = disabledIndicesOfList(state, list);
  const minIndex = getMinListIndex(elements, disabledIndices);
  const maxIndex = getMaxListIndex(elements, disabledIndices);

  // CMP-KEY-28's Home and End, except on a typeable combobox reference.
  if (!isTypeableCombobox(referenceElementOf(state, handle.session))) {
    if (event.key === HOME || event.key === END) {
      if (!proceed(dispatch, handle)) {
        return;
      }
      stopDispatch(dispatch);
      state.index = event.key === HOME ? minIndex : maxIndex;
      navigateTo(state, handle, event);
    }
  }

  // CMP-KEY-29, CMP-ALG-13: grid navigation runs before the main-key rule. The navigator consumes the vertical arrows,
  // and the horizontal ones for `both`, so the veto is asked before it runs for those keys.
  if (isGrid) {
    const consumed =
      event.key === ARROW_UP || event.key === ARROW_DOWN || (orientation === 'both' && (event.key === ARROW_LEFT || event.key === ARROW_RIGHT));
    if (consumed && !proceed(dispatch, handle)) {
      return;
    }
    const index = gridNavigation(event, state.index, elements, orientation, list.loopFocus === true, rtl, disabledIndices, minIndex, maxIndex, list.cols ?? 2);
    if (consumed) {
      dispatch.stopped = true;
    }

    // The grid navigator returns the unchanged index for keys it does not handle, and reporting that as a navigation
    // would re-emit the current highlight on every keydown.
    if (index != null && index !== state.index) {
      state.index = index;
      navigateTo(state, handle, event);
    }

    if (orientation === 'both') {
      return;
    }
  }

  if (isMainOrientationKey(event.key, orientation)) {
    if (!proceed(dispatch, handle)) {
      return;
    }
    stopDispatch(dispatch);

    // No item focused while focus is inside the popup: start at the boundary (CMP-KEY-31).
    const focused = activeElement(ownerDocument(currentTarget));
    if (
      isOpen(state, handle.session) &&
      list.virtual !== true &&
      contains(currentTarget, focused) &&
      !elements.some((item) => item != null && contains(item, focused))
    ) {
      state.index = isMainOrientationToEndKey(event.key, orientation, rtl) ? minIndex : maxIndex;
      const before = state.activeIndex;
      navigateTo(state, handle, event);
      // The boundary item may already be the highlight, so no change would move the focus.
      if (before === state.index) {
        focusItem(state, handle);
      }
      return;
    }

    const { index, wrapped } = getNextListIndex(elements, currentIndex, {
      decrement: !isMainOrientationToEndKey(event.key, orientation, rtl),
      loopFocus: list.loopFocus === true,
      allowEscape: list.allowEscape === true,
      disabledIndices,
      minIndex,
      maxIndex,
    });
    if (wrapped) {
      // Give time for virtualizers to update the list.
      state.forceSyncFocus = false;
    }
    state.index = index;
    navigateTo(state, handle, event);
  }
}

// The trigger's keydown (useListNavigation.ts:836-912), on the reference and on every trigger.
function triggerKeyDown(state: ListState, handle: ListHandle, dispatch: KeyDispatch): void {
  const event = dispatch.event;
  const list = handle.list();
  const currentTarget = handle.element;
  const currentOpen = isOpen(state, handle.session);
  state.pointerModality = false;

  const orientation = orientationOf(list);
  const triggerOrientation = list.triggerOrientation ?? orientation;
  const rtl = list.rtl === true;
  const parentOrientation = parentOrientationOf(handle);
  const isArrowKey = event.key.startsWith('Arrow');
  const isParentCrossOpenKey = isCrossOrientationOpenKey(event.key, parentOrientation, rtl);
  const isMainKey = isMainOrientationKey(event.key, currentOpen ? orientation : triggerOrientation);
  const isNavigationKey = (list.nested ? isParentCrossOpenKey : isMainKey) || event.key === 'Enter' || event.key.trim() === '';

  if (list.virtual && currentOpen && (!list.nested || isTypeableElement(currentTarget))) {
    commonKeyDown(state, handle, dispatch, currentTarget);
    return;
  }

  // CMP-KEY-19: a list that does not open on an arrow key keeps its highlight unset while closed.
  if (!currentOpen && list.openOnArrowKeyDown === false && isArrowKey) {
    return;
  }

  // CMP-KEY-21: the key the open's initial highlight reads.
  if (isNavigationKey) {
    const isParentMainKey = isMainOrientationKey(event.key, parentOrientation);
    state.key = list.nested && isParentMainKey ? null : event.key;
  }

  if (list.nested) {
    if (isParentCrossOpenKey) {
      if (!proceed(dispatch, handle)) {
        return;
      }
      stopDispatch(dispatch);
      if (currentOpen) {
        state.index = getMinListIndex(elementsOf(state), disabledIndicesOfList(state, list));
        navigateTo(state, handle, event);
        if (list.virtual) {
          state.floating?.element.focus();
        }
      } else {
        // CMP-EVT-06
        requestOpen(handle, true, 'list-navigation', event, currentTarget);
      }
    }
    return;
  }

  if (isMainKey) {
    if (!proceed(dispatch, handle)) {
      return;
    }
    const selectedIndex = selectedIndexOf(list);
    if (selectedIndex != null) {
      state.index = selectedIndex;
    }
    stopDispatch(dispatch);
    if (!currentOpen && list.openOnArrowKeyDown !== false) {
      // CMP-EVT-06
      requestOpen(handle, true, 'list-navigation', event, currentTarget);
    } else {
      commonKeyDown(state, handle, dispatch, currentTarget);
    }
    if (currentOpen) {
      navigateTo(state, handle, event);
    }
  }
}

// The floating element's keydown (useListNavigation.ts:769-797): Shift+Tab's `leave`, then the common handling.
function floatingKeyDown(state: ListState, handle: ListHandle, dispatch: KeyDispatch): void {
  const event = dispatch.event;
  const list = handle.list();
  if (event.key === TAB && event.shiftKey && isOpen(state, handle.session) && list.virtual !== true) {
    // A keydown from inside a nested element with its own focus management, such as a dialog, is not handled.
    const target = getTarget(event);
    if (isElement(target) && !contains(handle.element, target)) {
      return;
    }
    if (!proceed(dispatch, handle)) {
      return;
    }
    stopDispatch(dispatch);
    // CMP-KEY-24, CMP-EVT-08 (ADR 04 Amendments 2026-11-03 (3)): on WebAssembly C# runs the close inside `leave` and
    // answers whether it was canceled; on Server the close is requested and the focus returns (fallback run).
    const verdict = decide(handle.channel, LEAVE_POINT, event, 'run');
    if (!handle.channel.lastDecisionWasSynchronous) {
      requestOpen(handle, false, 'focus-out', event, null);
    }
    const returnElement = handle.returnFocusElement() ?? referenceElementOf(state, handle.session);
    if (verdict === 'run' && isHTMLElement(returnElement)) {
      returnElement.focus();
    }
    return;
  }
  commonKeyDown(state, handle, dispatch, handle.element);
}

// The trigger's focus, pointer and mouse handlers (useListNavigation.ts:812-834, :913-926).
function triggerFocus(state: ListState, handle: ListHandle, event: FocusEvent): void {
  if (event.target !== handle.element) {
    return;
  }
  if (isOpen(state, handle.session) && handle.list().virtual !== true) {
    state.index = -1;
    navigateTo(state, handle, event);
  }
}

function checkVirtualMouse(state: ListState, handle: ListHandle, event: MouseEvent): void {
  const list = handle.list();
  if (focusItemOnOpenOf(list) === 'auto' && isVirtualClick(event)) {
    state.focusItemOnOpen = list.virtual !== true;
  }
}

function checkVirtualPointer(state: ListState, handle: ListHandle, event: PointerEvent): void {
  const list = handle.list();
  const focusItemOnOpen = focusItemOnOpenOf(list);
  state.focusItemOnOpen = focusItemOnOpen;
  if (focusItemOnOpen === 'auto' && isVirtualPointerEvent(event)) {
    state.focusItemOnOpen = true;
  }
}

// ---------------------------------------------------------------------------------------------------------------
// The items' handlers, delegated on the floating element (useListNavigation.ts:685-750)
// ---------------------------------------------------------------------------------------------------------------

// The registered item an event's composed path reaches inside the floating element, innermost first.
function itemOfEvent(state: ListState, event: Event): HTMLElement | null {
  const floating = state.floating;
  if (!floating) {
    return null;
  }
  for (const node of event.composedPath()) {
    if (node === floating.element) {
      break;
    }
    if (isHTMLElement(node) && floating.registry.indexOf(node) !== -1) {
      return node;
    }
  }
  return null;
}

// `syncCurrentTarget` (useListNavigation.ts:515-525).
function syncCurrentTarget(state: ListState, handle: ListHandle, item: HTMLElement, event: Event): void {
  if (!isOpen(state, handle.session)) {
    return;
  }
  const index = state.floating?.registry.indexOf(item) ?? -1;
  if (index !== -1 && (state.index !== index || state.activeIndex !== index)) {
    state.index = index;
    navigateTo(state, handle, event);
  }
}

function itemFocus(state: ListState, handle: ListHandle, event: FocusEvent): void {
  const item = itemOfEvent(state, event);
  if (!item) {
    return;
  }
  state.forceSyncFocus = true;
  syncCurrentTarget(state, handle, item, event);
}

function itemClick(state: ListState, handle: ListHandle, event: MouseEvent): void {
  const item = itemOfEvent(state, event);
  // Safari; skipped under virtual focus, which keeps the real focus on the reference.
  if (item && handle.list().virtual !== true) {
    item.focus({ preventScroll: true });
  }
}

function itemMouseMove(state: ListState, handle: ListHandle, event: MouseEvent): void {
  const item = itemOfEvent(state, event);
  if (!item || isStationaryWebKitPointer(event)) {
    return;
  }
  state.forceSyncFocus = true;
  state.forceScrollIntoView = false;
  if (handle.list().focusItemOnHover !== false) {
    syncCurrentTarget(state, handle, item, event);
  }
}

// An item's `pointerleave` (React's per-element onPointerLeave): `pointerleave` does not bubble, so the floating handle
// hears it in the capture phase, where its target is the element the pointer left.
function itemPointerLeave(state: ListState, handle: ListHandle, event: PointerEvent): void {
  const item = event.target;
  if (!isHTMLElement(item) || (state.floating?.registry.indexOf(item) ?? -1) === -1) {
    return;
  }
  const list = handle.list();
  if (!isOpen(state, handle.session) || !state.pointerModality || event.pointerType === 'touch') {
    return;
  }
  state.forceSyncFocus = true;
  const relatedTarget = event.relatedTarget as HTMLElement | null;
  if (list.focusItemOnHover === false || elementsOf(state).includes(relatedTarget)) {
    return;
  }
  if (list.resetOnPointerLeave === false) {
    return;
  }
  state.cancelQueuedFocus?.();
  state.cancelQueuedFocus = null;
  state.index = -1;
  navigateTo(state, handle, event);
  if (list.virtual !== true) {
    const floatingElement = state.floating?.element ?? null;
    const focused = activeElement(ownerDocument(floatingElement));
    if (floatingElement && contains(floatingElement, focused)) {
      floatingElement.focus({ preventScroll: true });
    }
  }
}

// ---------------------------------------------------------------------------------------------------------------
// attachList: a list handle in its role
// ---------------------------------------------------------------------------------------------------------------

// The development warnings of useListNavigation.ts:272-287 (CMP-EDGE-03..05), with the `Blazix: ` prefix.
function warnListOptions(list: CompositeListOptions): void {
  if (list.allowEscape) {
    if (!list.loopFocus) {
      console.warn('Blazix: `useListNavigation` looping must be enabled to allow escaping.');
    }
    if (!list.virtual) {
      console.warn('Blazix: `useListNavigation` must be virtual to allow escaping.');
    }
  }
  if (orientationOf(list) === 'vertical' && list.grid) {
    console.warn('Blazix: In grid list navigation mode, the `orientation` should', 'be either "horizontal" or "both".');
  }
}

// Base UI warns on every render; the port warns where the options are set: at attach, and on an `update` that changed
// an option the warnings read.
function sameWarningInputs(a: CompositeListOptions, b: CompositeListOptions): boolean {
  return (
    a.allowEscape === b.allowEscape &&
    a.loopFocus === b.loopFocus &&
    a.virtual === b.virtual &&
    a.grid === b.grid &&
    orientationOf(a) === orientationOf(b)
  );
}

// ADR 04 Amendments 2026-11-03 (1).
/**
 * Attaches a list handle: it joins the popup's interactions session by token and shares the list's state with the
 * list's other handles. The floating handle resolves and orders the items, writes the
 * highlight and handles the list's keys and the items' pointer and focus events; a reference or trigger handle opens
 * the list from its keys. Every handle runs the typeahead its options carry.
 */
function attachList(
  elementOrId: Element | string | null,
  initialOptions: CompositeOptions,
  twinReference: DotNetTwin,
  initialReferences: TwinReferences,
  captures: TwinCaptures,
): TwinHandle {
  const element = resolveElement(elementOrId, captures?.[0]) as AttachedElement;
  const existing = element[attachedHandle];
  if (existing) {
    return existing;
  }

  const channel = createTwinChannel(twinReference);
  let options = initialOptions;
  let references = initialReferences;
  let referenceCaptures = captures;
  let disposed = false;
  const session = acquireSession(options.sessionToken ?? '');
  const isFloating = options.floating != null;
  const state = listStateOf(session) ?? createListState(session, options.list!);

  const handle: ListHandle = {
    element,
    channel,
    session,
    options: () => options,
    list: () => options.list ?? {},
    returnFocusElement: () => {
      const index = options.list?.nestedReturnFocusElement;
      const found = index == null ? null : resolveReference<Element>(references, index, referenceCaptures);
      return isHTMLElement(found) ? found : null;
    },
  };
  state.handles.add(handle);
  warnListOptions(handle.list());

  // The floating half: the registry and the items, resolved by id from C#'s registrations.
  const registry = isFloating ? createCompositeRegistry() : null;
  let items = new Map<string, AttachedItem>();
  let itemsByElement = new Map<Element, AttachedItem>();
  let reportedIds: string[] | null = null;
  let publications = 0;
  let derived: number[] = [];

  const deriveDisabled = (): number[] => {
    const result: number[] = [];
    registry?.elements.forEach((item, index) => {
      const attached = item ? itemsByElement.get(item) : undefined;
      if (attached?.disabled && !attached.focusableWhenDisabled) {
        result.push(index);
      }
    });
    return result;
  };

  const syncListItems = (registrations: ReadonlyArray<CompositeItemRegistration>): boolean => {
    if (!registry) {
      return false;
    }
    const next = new Map<string, AttachedItem>();
    for (const registration of registrations) {
      const found = resolveById(element, registration.id);
      if (found) {
        next.set(registration.id, {
          id: registration.id,
          element: found,
          tabIndex: registration.tabIndex ?? null,
          disabled: registration.disabled ?? false,
          focusableWhenDisabled: registration.focusableWhenDisabled ?? false,
          omitTabIndex: registration.omitTabIndex ?? false,
          label: registeredLabel(registration.label),
          textId: registration.textId ?? null,
          index: registration.index ?? null,
        });
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
      if (!sameRegistryFields(items.get(id), attached)) {
        registry.register(attached.element, {
          index: attached.index,
          label: attached.label,
          textElement: attached.textId === null ? null : resolveById(element, attached.textId),
        });
        changed = true;
      }
    });
    items = next;
    itemsByElement = new Map();
    next.forEach((attached) => itemsByElement.set(attached.element, attached));
    return changed;
  };

  let floatingPart: ListFloating | null = null;
  let referencePart: ListReference | null = null;

  if (registry) {
    floatingPart = {
      element,
      registry,
      channel,
      ownsTabIndex: (item) => {
        const attached = itemsByElement.get(item);
        return attached !== undefined && attached.tabIndex == null && !attached.omitTabIndex;
      },
      idOf: (item) => (item ? (itemsByElement.get(item)?.id ?? null) : null),
      derivedDisabledIndices: () => derived,
      keyDown: (event) => onKeyDown(event),
    };
    // CMP-EVT-03 (E2): the ids in index order on each publication that changed them, when asked.
    registry.subscribe(() => {
      derived = deriveDisabled();
      if (options.reportItems) {
        const ids: string[] = [];
        for (const item of registry.elements) {
          const attached = item ? itemsByElement.get(item) : undefined;
          if (attached) {
            ids.push(attached.id);
          }
        }
        if (!(reportedIds && ids.length === reportedIds.length && ids.every((id, index) => id === reportedIds![index]))) {
          reportedIds = ids;
          publications += 1;
          reportItemOrder(channel, ids, publications);
        }
      }
      writeHighlight(state, handle);
    });
  } else {
    referencePart = { element, isReference: options.reference != null };
    state.references.add(referencePart);
  }

  // Listeners. One keydown per handle runs the list navigation, then the typeahead, after the consumer's veto.
  const onKeyDown = (event: KeyboardEvent): void => {
    const list = handle.list();
    const dispatch: KeyDispatch = { event, vetoed: null, stopped: false };
    if (enabledOf(list)) {
      if (isFloating) {
        floatingKeyDown(state, handle, dispatch);
      } else {
        triggerKeyDown(state, handle, dispatch);
      }
    }
    if (list.typeahead && typeaheadProceeds(dispatch, handle, event)) {
      const stoppedBefore = event.defaultPrevented;
      typeaheadKeyDown(state, handle, event);
      if (!stoppedBefore && event.defaultPrevented) {
        dispatch.stopped = true;
      }
    }
    // Option (i) (ADR 04 Amendments 2026-10-22, 2026-11-03 (3)): a key stopped here never reaches Blazor's dispatcher.
    if (dispatch.stopped && options.reportHandledKeys && !channel.lastDecisionWasSynchronous) {
      channel.report('OnKeyHandled', { event: eventInfo(event) });
    }
    // CMP-KEY-14 (Deliverable 7; MenuRoot.tsx:692-699): a menubar's root does not hear the keydowns of a menu portaled
    // outside it, so the floating handle relays one no listener stopped to the root its `relayElement` names; the root
    // stops what it handles.
    const relayElement = list.relayElement;
    if (isFloating && relayElement != null && !event.cancelBubble) {
      relayKeyboardEvent(resolveReference<Element>(references, relayElement, referenceCaptures), event);
    }
    // CMP-KEY-27, CMP-DIS-01 (Deliverable 6): React bubbles a portaled child popup's keydown through the React tree to its
    // parent popup's handlers, so a nested list's close key that is also the parent's main key moves the parent. The
    // port's child popup is outside the parent popup's DOM: a keydown no listener stopped is handed to the parent list's
    // floating handle, unless its path already reaches that element.
    if (isFloating && !event.cancelBubble) {
      const parentFloating = parentListFloatingOf(session);
      if (parentFloating && !event.composedPath().includes(parentFloating.element)) {
        parentFloating.keyDown(event);
      }
    }
  };

  // Design point 2 (c): an item's Space while typing is prevented before the item's own `button` listener runs, as
  // Base UI's item handler does (useMenuItemCommonProps.ts:85-88, SelectItem.tsx:161-169); `button` skips a prevented
  // Space on a `menuitem*`, `option` or `gridcell`.
  const onKeyDownCapture = (event: KeyboardEvent): void => {
    if (event.key === ' ' && state.typeahead.typing && itemOfEvent(state, event)) {
      event.preventDefault();
    }
  };

  const onFocusOut = (event: FocusEvent): void => typeaheadFocusOut(state, handle, event);

  const listeners: [string, EventListener, boolean][] = isFloating
    ? [
        ['keydown', onKeyDownCapture as EventListener, true],
        ['keydown', onKeyDown as EventListener, false],
        ['focusout', onFocusOut as EventListener, false],
        ['focusin', ((event: FocusEvent) => enabledOf(handle.list()) && itemFocus(state, handle, event)) as EventListener, false],
        ['click', ((event: MouseEvent) => enabledOf(handle.list()) && itemClick(state, handle, event)) as EventListener, false],
        ['mousemove', ((event: MouseEvent) => enabledOf(handle.list()) && itemMouseMove(state, handle, event)) as EventListener, false],
        ['pointerleave', ((event: PointerEvent) => enabledOf(handle.list()) && itemPointerLeave(state, handle, event)) as EventListener, true],
        [
          'pointermove',
          ((event: PointerEvent) => {
            if (enabledOf(handle.list()) && !isStationaryWebKitPointer(event)) {
              state.pointerModality = true;
            }
          }) as EventListener,
          false,
        ],
      ]
    : [
        ['keydown', onKeyDown as EventListener, false],
        ['focusout', onFocusOut as EventListener, false],
        ['focus', ((event: FocusEvent) => enabledOf(handle.list()) && triggerFocus(state, handle, event)) as EventListener, false],
        ['pointerdown', ((event: PointerEvent) => enabledOf(handle.list()) && checkVirtualPointer(state, handle, event)) as EventListener, false],
        ['pointerenter', ((event: PointerEvent) => enabledOf(handle.list()) && checkVirtualPointer(state, handle, event)) as EventListener, false],
        ['mousedown', ((event: MouseEvent) => enabledOf(handle.list()) && checkVirtualMouse(state, handle, event)) as EventListener, false],
        ['click', ((event: MouseEvent) => enabledOf(handle.list()) && checkVirtualMouse(state, handle, event)) as EventListener, false],
      ];
  for (const [type, listener, capture] of listeners) {
    element.addEventListener(type, listener, capture);
  }

  // The first publication, then the open sync of a floating element mounted open (CMP-STA-37; ADR 01 Amendments
  // 2026-10-27: a handle the opening render creates attaches from OnAfterRender).
  if (registry && floatingPart) {
    syncListItems(options.items);
    registry.flush();
    derived = deriveDisabled();
    state.floating = floatingPart;
  }
  applyHighlightRequest(state, handle);
  if (registry) {
    syncOpenAndMount(state, handle);
  } else {
    session.dataRef.current.orientation = orientationOf(handle.list());
    writeHighlight(state, handle);
  }

  const twinHandle: TwinHandle = {
    update(nextOptionsJson: string, nextReferences?: TwinReferences, nextCaptures?: TwinCaptures) {
      if (disposed) {
        return;
      }
      const previous = handle.list();
      options = parseOptions<CompositeOptions>(nextOptionsJson);
      if (nextReferences !== undefined) {
        references = nextReferences;
        referenceCaptures = nextCaptures;
      }
      const list = handle.list();
      if (!sameWarningInputs(previous, list)) {
        warnListOptions(list);
      }
      if (registry && syncListItems(options.items)) {
        registry.flush();
      }
      if (registry) {
        derived = deriveDisabled();
      }
      session.dataRef.current.orientation = orientationOf(list);
      if (focusItemOnOpenOf(list) !== focusItemOnOpenOf(previous) && (!isOpen(state, session) || focusItemOnOpenOf(list) !== 'auto')) {
        state.focusItemOnOpen = focusItemOnOpenOf(list);
      }
      if (selectedIndexOf(list) !== selectedIndexOf(previous)) {
        onSelectedIndexChange(state, handle);
      }
      applyHighlightRequest(state, handle);
      writeHighlight(state, handle);
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete element[attachedHandle];
      for (const [type, listener, capture] of listeners) {
        element.removeEventListener(type, listener, capture);
      }
      state.handles.delete(handle);
      if (referencePart) {
        state.references.delete(referencePart);
      }
      if (registry && state.floating === floatingPart) {
        state.floating = null;
        // The unmount's effects (CMP-STA-33's reset, CMP-FOC-08), with the options of the handle that unmounted.
        syncOpenAndMount(state, handle);
        registry.dispose();
      }
      releaseListState(state, session);
      releaseSession(session);
    },
  };
  element[attachedHandle] = twinHandle;
  return twinHandle;
}

// The typeahead runs after the list navigation of the same keydown under the same veto (mergeProps runs the consumer's
// handler first, and its preventBaseUIHandler() skips both); a key it does not act on asks nothing.
function typeaheadProceeds(dispatch: KeyDispatch, handle: ListHandle, event: KeyboardEvent): boolean {
  if (dispatch.vetoed !== null) {
    return !dispatch.vetoed;
  }
  const actsOnKey = event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey;
  return !actsOnKey || proceed(dispatch, handle);
}

// useListNavigation.ts:389-403 for a change of `selectedIndex` while the list is open and mounted; useTypeahead's reset.
function onSelectedIndexChange(state: ListState, handle: ListHandle): void {
  const list = handle.list();
  if (enabledOf(list) && isOpen(state, handle.session) && state.floating) {
    const selectedIndex = selectedIndexOf(list);
    state.index = selectedIndex ?? -1;
    if (state.focusItemOnOpen && selectedIndex != null) {
      state.forceScrollIntoView = true;
      navigateTo(state, handle);
    }
  }
  typeaheadOnOpenOrSelectedChange(state, handle);
}
