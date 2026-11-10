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
import { type DotNetTwin, type TwinCaptures, type TwinHandle, type TwinOptions, type TwinReferences } from './twin';

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

/**
 * Attaches a `menu-filter` handle to a part's element.
 * @param elementOrId The part's element, or its id.
 * @param optionsJson The `MenuFilterOptions` JSON.
 * @param twinReference The part's `MenuFilterTwin`.
 * @param references The elements the options name by index.
 * @param captures The captured element references.
 * @returns The handle.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  void elementOrId;
  void optionsJson;
  void twinReference;
  void references;
  void captures;
  throw new Error('menu-filter: not implemented');
}
