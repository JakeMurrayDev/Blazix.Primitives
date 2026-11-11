// The `menu-filter` component module (M5 track 12's design point 1 (a), settled by the orchestrator 2026-11-11): Menu's filter mode in the
// browser, for docs/components/menu.md §6.1.1 and the filter rows of §5.8 and §6.2. One handle per part that owns a
// MenuFilterTwin, joined to its menu by `menuKey`:
// - the Input: composition (EVT-19, KEY-31), the committed-value writes (C# -> JS, the Input/Field.Control precedent of
//   ADR 04 Amendments 2026-10-22), the key router over Base UI's getMenuFilterKeyAction table (KEY-22..33), the consumer's
//   `navigate` veto (design point 6 (a)), data-highlighted (STA-22, design point 5 (a)) and the WebKit aria-selected
//   (ARIA-15, design point 5 (c));
// - the Trigger: the relay of the vertical arrows while the popup is open (KEY-28), off in a menubar (MBR-KEY-14);
// - the List: the mousedown default of the List and its items (PTR-20), a key on the List replayed onto the Input (KEY-27)
//   and the items' rendered texts (STA-17, design point 2 (b′));
// - the Popup: the background mousedown, the pointer's refocus of the Input (PTR-22) and the cross-axis close key (KEY-33).
// It imports `composite`'s exported predicates and reads the session through `interactions`' exports; it edits neither.
// Interface commit: the option and report names; every acting body throws until its red-first commit.

import { isCrossOrientationCloseKey, isCrossOrientationOpenKey, isMainOrientationKey } from './composite';
import { eventInfo } from './event-info';
import { platform } from './platform';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinCaptures,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

/** The part names, as `MenuFilterTwin`'s constants. */
export type MenuFilterPart = 'input' | 'trigger' | 'list' | 'popup';

/** The options of a `menu-filter` handle, as `MenuFilterOptions` serializes them. */
export interface MenuFilterOptions extends TwinOptions {
  /** The part the handle is attached to. */
  part: MenuFilterPart;
  /** The key that joins the handles of one filterable menu. */
  menuKey?: string | null;
  /** The committed query C# last sent; the Input's. */
  value?: string | null;
  /** The count of committed values C# has sent; the Input's. */
  valueRevision?: number | null;
  /** Whether the menu is open; the Input's and the Trigger's. */
  open?: boolean | null;
  /** Whether the menu opened from the keyboard, which seeds the focus ring; the Input's. */
  keyboardOpen?: boolean | null;
  /** The axis of the menu's list; the Input's. */
  orientation?: 'horizontal' | 'vertical' | null;
  /** Whether the reading direction is right to left; the Input's. */
  rtl?: boolean | null;
  /** Whether the menu is a submenu; the Input's. */
  submenu?: boolean | null;
  /** Whether the menu belongs to a menubar, where the Trigger's relay is off; the Trigger's. */
  inBar?: boolean | null;
  /** Whether C# holds a consumer key handler the `navigate` point asks; the Input's and the Trigger's. */
  decidesKeyDown?: boolean | null;
  /** The ids of the registered items whose rendered text C# needs; the List's. */
  textIds?: readonly string[] | null;
}

/**
 * What a key does while the filter input owns the keyboard: Base UI's `MenuFilterKeyAction`
 * (`menu/filter-root/useMenuFilterKeyDown.ts:22-35`).
 */
export type MenuFilterKeyAction = 'edit' | 'navigate' | 'enter-list' | 'submenu' | 'activate' | 'close' | 'ignore';

/** The menu's state a key's action depends on: Base UI's `MenuFilterKeyContext`. */
export interface MenuFilterKeyContext {
  orientation: 'horizontal' | 'vertical';
  rtl: boolean;
  /** Whether an item is highlighted. */
  hasActiveItem: boolean;
  /** Whether the highlighted item opens a submenu. */
  activeItemOpensSubmenu: boolean;
  /** Whether the input has text. */
  hasValue: boolean;
}

/** The fields of a keydown the table reads. */
export interface MenuFilterKey {
  key: string;
  which?: number;
  shiftKey: boolean;
  ctrlKey: boolean;
  altKey: boolean;
  metaKey: boolean;
}

/**
 * Decides what a key does while the filter input owns the keyboard (KEY-22, KEY-30; `getMenuFilterKeyAction`).
 * @param event The keydown.
 * @param context The menu's state.
 * @returns The key's action.
 */
export function getMenuFilterKeyAction(event: MenuFilterKey, context: MenuFilterKeyContext): MenuFilterKeyAction {
  const { orientation, rtl, hasActiveItem, activeItemOpensSubmenu, hasValue } = context;
  const { key } = event;
  // useMenuFilterKeyDown.ts:58-61: the Enter that commits an IME composition belongs to the input, not the list.
  if (event.which === 229) {
    return 'ignore';
  }
  if (key === 'Tab') {
    return event.shiftKey ? 'close' : 'ignore';
  }
  if (key === 'Enter') {
    return hasActiveItem ? 'activate' : 'navigate';
  }

  const isArrow = key.startsWith('Arrow');
  const isBoundary = key === 'Home' || key === 'End';

  // :72-76: modified arrows and boundary keys are text-editing commands, like a modified character.
  if (event.shiftKey || event.ctrlKey || event.altKey || event.metaKey) {
    return isArrow || isBoundary || key.length === 1 ? 'edit' : 'navigate';
  }
  // :77-80: characters stay out of parent handlers such as typeahead.
  if (key.length === 1) {
    return 'edit';
  }
  // :81-85: Home and End move the caret while the input has text, and jump to the ends of the list otherwise once an
  // item is highlighted.
  if (isBoundary) {
    return hasActiveItem && !hasValue ? 'navigate' : 'edit';
  }
  if (!isArrow) {
    return 'navigate';
  }

  // :90-93: Left and Right move the caret of a non-empty input while nothing is highlighted.
  if (isMainOrientationKey(key, orientation)) {
    return orientation === 'horizontal' && !hasActiveItem && hasValue ? 'edit' : 'navigate';
  }

  // :95-102: a highlighted submenu trigger takes the cross-axis keys to open or close its submenu.
  if (activeItemOpensSubmenu && (isCrossOrientationOpenKey(key, orientation, rtl) || isCrossOrientationCloseKey(key, orientation, rtl, false))) {
    return 'submenu';
  }
  if (orientation === 'horizontal' && !hasActiveItem) {
    return 'enter-list';
  }
  // :106-108: otherwise the cross-axis keys move the caret; on an empty input they fall through so they can still close a
  // submenu.
  return hasValue ? 'edit' : 'navigate';
}

const handleKey = Symbol('blazix.menu-filter.handle');

interface MenuFilterElement extends HTMLElement {
  [handleKey]?: TwinHandle;
}

const HIGHLIGHTED = 'data-highlighted';

// The Input (filter-dropdown/input/FilterDropdownInput.tsx; menu/input/MenuInput.tsx).
function attachInput(input: HTMLInputElement, initial: MenuFilterOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let disposed = false;
  // EVT-19, KEY-31 (FilterDropdownInput.tsx:84-103): IME text is not committed until the composition ends.
  let composing = false;
  // STA-22 (FilterDropdownRoot.tsx:49-58): both seeded from a keyboard open on every open.
  let focusVisible = input.ownerDocument.activeElement === input || options.keyboardOpen === true;
  let keyboardModality = options.keyboardOpen === true;
  // C# renders the input without data-highlighted (MenuInputState.Highlighted starts false), so only a change is reported.
  let highlighted = false;
  let lastRevision = options.valueRevision ?? null;

  // FilterDropdownInput.tsx:45-53: an edit made while the popup animates out would land after the close reset the query.
  const commit = (event: Event): void => {
    if (options.open !== true) {
      return;
    }
    const value = input.value;
    channel.report('OnValueChange', { value, reason: value === '' ? 'input-clear' : 'input-change', event: eventInfo(event) });
  };

  const syncHighlighted = (): void => {
    const next = focusVisible && (!keyboardModality || !input.hasAttribute('aria-activedescendant'));
    if (next === highlighted) {
      return;
    }
    highlighted = next;
    input.toggleAttribute(HIGHLIGHTED, next);
    channel.report('OnHighlightedChange', { highlighted: next });
  };

  // C# -> JS (ADR 04 Amendments 2026-10-22, the Input/Field.Control precedent): a committed value is written when its
  // revision moves and the live text differs, which also re-asserts a controlled value the consumer did not apply.
  const writeValue = (): void => {
    const revision = options.valueRevision ?? null;
    if (revision === lastRevision) {
      return;
    }
    lastRevision = revision;
    const value = options.value ?? '';
    if (!composing && input.value !== value) {
      input.value = value;
    }
  };

  const onInput = (event: Event): void => {
    if (composing) {
      return;
    }
    commit(event);
  };
  const onCompositionStart = (): void => {
    // Some Android keyboards treat all typing as one composition.
    if (platform.os.android) {
      return;
    }
    composing = true;
  };
  const onCompositionEnd = (event: Event): void => {
    if (!composing) {
      return;
    }
    composing = false;
    commit(event);
  };
  const onFocus = (): void => {
    focusVisible = true;
    syncHighlighted();
  };
  const onBlur = (): void => {
    focusVisible = false;
    syncHighlighted();
  };
  const onKeyDown = (): void => {
    keyboardModality = true;
    syncHighlighted();
  };
  const onPointer = (): void => {
    keyboardModality = false;
    syncHighlighted();
  };
  // ADR 04 :504: composite writes aria-activedescendant on its reference, the Input; the focus ring follows it.
  const observer = new MutationObserver(syncHighlighted);
  observer.observe(input, { attributes: true, attributeFilter: ['aria-activedescendant'] });

  input.addEventListener('input', onInput);
  input.addEventListener('compositionstart', onCompositionStart);
  input.addEventListener('compositionend', onCompositionEnd);
  input.addEventListener('focus', onFocus);
  input.addEventListener('blur', onBlur);
  input.addEventListener('keydown', onKeyDown);
  input.addEventListener('pointerdown', onPointer);
  input.addEventListener('mouseenter', onPointer);
  syncHighlighted();

  return {
    update(optionsJson: string): void {
      if (disposed) {
        return;
      }
      const wasOpen = options.open === true;
      options = parseOptions<MenuFilterOptions>(optionsJson);
      if (!wasOpen && options.open === true) {
        focusVisible = input.ownerDocument.activeElement === input || options.keyboardOpen === true;
        keyboardModality = options.keyboardOpen === true;
      }
      writeValue();
      syncHighlighted();
    },
    dispose(): void {
      if (disposed) {
        return;
      }
      disposed = true;
      observer.disconnect();
      input.removeEventListener('input', onInput);
      input.removeEventListener('compositionstart', onCompositionStart);
      input.removeEventListener('compositionend', onCompositionEnd);
      input.removeEventListener('focus', onFocus);
      input.removeEventListener('blur', onBlur);
      input.removeEventListener('keydown', onKeyDown);
      input.removeEventListener('pointerdown', onPointer);
      input.removeEventListener('mouseenter', onPointer);
    },
  };
}

// The List's item texts (design point 2 (b′), settled by the orchestrator 2026-11-11): C# names the items it has no label
// for, and the List reports what each renders, as useFilterDropdownItem.ts:100-140 reads it (`textContent`, empty when the
// item renders none). Only a changed text is reported; an id the list does not render is skipped, so a filtered-out item
// keeps the text C# cached. A mutation under the list re-reads the named items (an item's text can change without C#).
function attachList(list: HTMLElement, initial: MenuFilterOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let disposed = false;
  const reported = new Map<string, string>();

  const reportTexts = (): void => {
    const ids: string[] = [];
    const texts: string[] = [];
    for (const id of options.textIds ?? []) {
      const item = list.ownerDocument.getElementById(id);
      if (item === null || !list.contains(item)) {
        continue;
      }
      const text = item.textContent ?? '';
      if (reported.get(id) !== text) {
        reported.set(id, text);
        ids.push(id);
        texts.push(text);
      }
    }
    if (ids.length > 0) {
      channel.report('OnTexts', { ids, texts });
    }
  };

  // PTR-20 (useMenuItemCommonProps.ts:12-14, 84): under virtual focus pressing an item keeps focus on the input. The
  // list's own background is not an item: a scrollbar press may focus the list, which the Popup's refocus handles.
  const onMouseDown = (event: MouseEvent): void => {
    const target = event.target;
    const item = target instanceof Element ? target.closest('[role^="menuitem"]') : null;
    if (item !== null && list.contains(item)) {
      event.preventDefault();
    }
  };

  const observer = new MutationObserver(reportTexts);
  observer.observe(list, { subtree: true, childList: true, characterData: true });
  list.addEventListener('mousedown', onMouseDown);
  reportTexts();

  return {
    update(optionsJson: string): void {
      if (disposed) {
        return;
      }
      options = parseOptions<MenuFilterOptions>(optionsJson);
      reportTexts();
    },
    dispose(): void {
      if (disposed) {
        return;
      }
      disposed = true;
      observer.disconnect();
      list.removeEventListener('mousedown', onMouseDown);
    },
  };
}

/**
 * Attaches a `menu-filter` handle to a part's element.
 * @param elementOrId The part's element, or its id.
 * @param optionsJson The `MenuFilterOptions` JSON.
 * @param twinReference The part's `MenuFilterTwin`.
 * @param references The elements the options name by index.
 * @param captures The captured element references.
 * @returns The handle; a repeated attach of the same element returns the first one.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  void references;
  const element = resolveElement(elementOrId, captures?.[0]) as MenuFilterElement;
  const existing = element[handleKey];
  if (existing) {
    return existing;
  }
  const options = parseOptions<MenuFilterOptions>(optionsJson);
  const channel = createTwinChannel(twinReference);
  let inner: TwinHandle;
  switch (options.part) {
    case 'input':
      inner = attachInput(element as HTMLInputElement, options, channel);
      break;
    case 'list':
      inner = attachList(element, options, channel);
      break;
    default:
      throw new Error(`menu-filter: the ${options.part} part is not implemented`);
  }
  const handle: TwinHandle = {
    update: (json, refs, caps) => inner.update(json, refs, caps),
    dispose: () => {
      inner.dispose();
      if (element[handleKey] === handle) {
        delete element[handleKey];
      }
    },
  };
  element[handleKey] = handle;
  return handle;
}
