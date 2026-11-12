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

import { dispatchClickWithModifiers } from './button';
import {
  highlightListEdge,
  highlightListItem,
  isCrossOrientationCloseKey,
  isCrossOrientationOpenKey,
  isMainOrientationKey,
  moveListHighlightFrom,
  skipListNavigation,
} from './composite';
import { eventInfo } from './event-info';
import { platform } from './platform';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinCaptures,
  type TwinChannel,
  type DecisionPolicy,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

/** The part names, as `MenuFilterTwin`'s constants. */
export type MenuFilterPart = 'input' | 'trigger' | 'list' | 'popup' | 'clear' | 'submenu-trigger';

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
  /** The automatic highlight mode an item-set change follows, `true` or `always`; the List's. */
  autoHighlight?: 'true' | 'always' | null;
  /** The parent menu's session token, whose input a filterable submenu returns to; a submenu's trigger and popup. */
  parentKey?: string | null;
  /** The parent menu's orientation; a submenu's trigger. */
  parentOrientation?: 'horizontal' | 'vertical' | null;
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

/** The decision point asked before the module handles a key: the consumer's onKeyDown veto (design point 6 (a)). */
export const NAVIGATE_POINT = 'navigate';

function isStopVerdict(verdict: DecisionPolicy): boolean {
  return verdict === 'stop' || verdict === 'prevent-and-stop';
}

interface MenuFilterElement extends HTMLElement {
  [handleKey]?: TwinHandle;
}

// The Inputs by menu key, the focus owners the List and the Popup hand focus back to (filter-dropdown/utils/
// refocusOwner.ts), with the Input's current options (its orientation and direction).
interface FocusOwner {
  input: HTMLInputElement;
  options: () => MenuFilterOptions;
}

const owners = new Map<string, FocusOwner>();

// The keys the Input keeps for its caret (the router's `edit`), which Base UI stops from reaching the popup
// (useMenuFilterKeyDown.ts:138-140); a submenu popup's close key ignores them.
const inputOwnedKeys = new WeakSet<KeyboardEvent>();

// MenuFilterSubmenuRoot.tsx:41-49, 172-207 (MNU-KEY-31..33): leaving a filterable submenu returns focus to the parent's
// input and highlights the submenu's trigger there; a plain parent gets the trigger focused. A close key that is also
// the parent's main-axis key then moves on in the parent from the trigger.
function exitSubmenu(trigger: HTMLElement, parentKey: string | null | undefined, parentOrientation: 'horizontal' | 'vertical', event: KeyboardEvent): void {
  const parent = parentKey ? owners.get(parentKey) : undefined;
  if (parent === undefined) {
    trigger.focus({ preventScroll: true });
    return;
  }
  refocusOwner(parent.input);
  highlightListItem(parent.input, trigger, event);
  if (isMainOrientationKey(event.key, parentOrientation)) {
    moveListHighlightFrom(parent.input, trigger, event);
  }
}

// isTypeableElement (floating-ui-react/utils): a key on a text field is the field's.
function isTypeable(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || target instanceof HTMLTextAreaElement || (target instanceof HTMLInputElement && !['button', 'checkbox', 'radio', 'submit', 'reset', 'image', 'range', 'color', 'file'].includes(target.type)));
}

// isRefocusingOwner (filter-dropdown/utils/refocusOwner.ts): focus handlers that run during the call treat the move as a
// continuation, not the user's.
let refocusing = false;

function refocusOwner(owner: HTMLElement): void {
  refocusing = true;
  try {
    owner.focus({ preventScroll: true });
  } finally {
    refocusing = false;
  }
}

// Routes a key that landed elsewhere as the input's: a copy is dispatched on the input, and the original is prevented when
// the copy was.
function replayOnOwner(owner: HTMLInputElement, event: KeyboardEvent): void {
  const KeyboardEventConstructor = owner.ownerDocument.defaultView?.KeyboardEvent ?? KeyboardEvent;
  const replay = new KeyboardEventConstructor('keydown', {
    key: event.key,
    code: event.code,
    location: event.location,
    repeat: event.repeat,
    shiftKey: event.shiftKey,
    ctrlKey: event.ctrlKey,
    altKey: event.altKey,
    metaKey: event.metaKey,
    bubbles: true,
    cancelable: true,
    composed: true,
  });
  if (!owner.dispatchEvent(replay)) {
    event.preventDefault();
  }
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

  // ARIA-15 (MenuStore.ts:107-111, useMenuItemCommonProps.ts:63-68; design point 5 (c)): WebKit only, while the input has
  // focus, the item the composite's aria-activedescendant names is marked aria-selected="true".
  let selectedItem: Element | null = null;
  const syncSelected = (): void => {
    if (!platform.engine.webkit) {
      return;
    }
    const id = input.getAttribute('aria-activedescendant');
    const next = !disposed && id && input.ownerDocument.activeElement === input ? input.ownerDocument.getElementById(id) : null;
    if (next === selectedItem) {
      return;
    }
    selectedItem?.removeAttribute('aria-selected');
    next?.setAttribute('aria-selected', 'true');
    selectedItem = next;
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
    syncSelected();
  };
  const onBlur = (): void => {
    focusVisible = false;
    syncHighlighted();
    syncSelected();
  };
  const onKeyDown = (): void => {
    keyboardModality = true;
    syncHighlighted();
  };
  // KEY-22, KEY-24, KEY-25, KEY-26, KEY-29, KEY-30 (useMenuFilterKeyDown.ts:103-192): the router. A key the table gives to the
  // input (`edit`) or to no one (`ignore`) is marked before composite's reference handle runs, so it moves the caret, not
  // the highlight; `enter-list` highlights the list's edge, an `activate` key clicks the highlighted item, a `submenu` key
  // is dispatched on it, and `close` reports a focus-out.
  const onKeyDownCapture = (event: KeyboardEvent): void => {
    if (options.open !== true) {
      return;
    }
    // Design point 6 (a): while C# holds the consumer's onKeyDown, it runs first and may veto the router and the list
    // navigation (Base UI's preventBaseUIHandler). On Server the point falls back to `run`.
    if (options.decidesKeyDown === true && isStopVerdict(channel.decide(NAVIGATE_POINT, event, 'run'))) {
      skipListNavigation(event);
      return;
    }
    const activeId = input.getAttribute('aria-activedescendant');
    const activeItem = activeId ? input.ownerDocument.getElementById(activeId) : null;
    const action = getMenuFilterKeyAction(event, {
      orientation: options.orientation ?? 'vertical',
      rtl: options.rtl === true,
      hasActiveItem: activeItem !== null,
      activeItemOpensSubmenu: activeItem?.hasAttribute('aria-haspopup') ?? false,
      hasValue: input.value !== '',
    });
    switch (action) {
      case 'edit':
        inputOwnedKeys.add(event);
        skipListNavigation(event);
        break;
      case 'ignore':
        skipListNavigation(event);
        break;
      // KEY-24 (:155-167): in a horizontal menu with nothing highlighted, ArrowDown highlights the first item and ArrowUp the
      // last, and the key is prevented only when an item was highlighted.
      case 'enter-list':
        skipListNavigation(event);
        if (highlightListEdge(input, event.key === 'ArrowDown' ? 'first' : 'last', event)) {
          event.preventDefault();
        }
        break;
      // KEY-25 (:173-176): Enter on a highlighted item clicks it, with the key's modifiers.
      case 'activate':
        skipListNavigation(event);
        event.preventDefault();
        dispatchClickWithModifiers(activeItem!, event);
        break;
      // KEY-29 (:161-169): the highlighted submenu trigger's own handlers decide, since opening depends on the item and on
      // both menus' orientations, so the key is dispatched on it.
      case 'submenu': {
        skipListNavigation(event);
        event.preventDefault();
        const KeyboardEventConstructor = input.ownerDocument.defaultView?.KeyboardEvent ?? KeyboardEvent;
        activeItem!.dispatchEvent(new KeyboardEventConstructor(event.type, event));
        break;
      }
      // KEY-26 (:179-192): Shift+Tab closes with focus-out; C# requests the close and the focus manager returns focus.
      case 'close':
        skipListNavigation(event);
        event.preventDefault();
        channel.report('OnFocusOut', { event: eventInfo(event) });
        break;
      default:
        break;
    }
  };
  const onPointer = (): void => {
    keyboardModality = false;
    syncHighlighted();
  };
  // ADR 04 :504: composite writes aria-activedescendant on its reference, the Input; the focus ring follows it.
  const observer = new MutationObserver(() => {
    syncHighlighted();
    syncSelected();
  });
  observer.observe(input, { attributes: true, attributeFilter: ['aria-activedescendant'] });

  input.addEventListener('input', onInput);
  input.addEventListener('compositionstart', onCompositionStart);
  input.addEventListener('compositionend', onCompositionEnd);
  input.addEventListener('focus', onFocus);
  input.addEventListener('blur', onBlur);
  input.addEventListener('keydown', onKeyDownCapture, true);
  input.addEventListener('keydown', onKeyDown);
  input.addEventListener('pointerdown', onPointer);
  input.addEventListener('mouseenter', onPointer);
  const ownerKey = options.menuKey ?? null;
  const owner: FocusOwner = { input, options: () => options };
  if (ownerKey !== null) {
    owners.set(ownerKey, owner);
  }
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
      syncSelected();
      input.removeEventListener('input', onInput);
      input.removeEventListener('compositionstart', onCompositionStart);
      input.removeEventListener('compositionend', onCompositionEnd);
      input.removeEventListener('focus', onFocus);
      input.removeEventListener('blur', onBlur);
      input.removeEventListener('keydown', onKeyDownCapture, true);
      input.removeEventListener('keydown', onKeyDown);
      input.removeEventListener('pointerdown', onPointer);
      input.removeEventListener('mouseenter', onPointer);
      if (ownerKey !== null && owners.get(ownerKey) === owner) {
        owners.delete(ownerKey);
      }
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

  const ownerOf = (): HTMLInputElement | undefined => (options.menuKey ? owners.get(options.menuKey)?.input : undefined);

  // FilterDropdownList.tsx:33-43: a press on the list's own content (its background, a group, a group label) focuses the
  // list; focus that came from the input goes back to it.
  const onFocus = (event: FocusEvent): void => {
    const owner = ownerOf();
    if (owner !== undefined && event.relatedTarget === owner && event.target === list) {
      refocusOwner(owner);
    }
  };

  // KEY-22 (MenuFilterList.tsx:43-80): real focus inside the list while the input owns the keyboard (a screen reader
  // following aria-activedescendant focuses the item) goes back to the input, and the key is replayed there, so it is
  // routed as the input's; the list's own handles skip the original. A key an item already acted on stays the item's.
  const onKeyDown = (event: KeyboardEvent): void => {
    const owner = ownerOf();
    const target = event.target;
    if (owner === undefined || event.defaultPrevented || !(target instanceof Node) || !list.contains(target)) {
      return;
    }
    if (owner.ownerDocument.activeElement === owner) {
      return;
    }
    skipListNavigation(event);
    refocusOwner(owner);
    replayOnOwner(owner, event);
  };

  // STA-20 (FilterDropdownRoot.tsx:109-125, MenuFilterList.tsx:101-117): a positional highlight must not silently move to
  // another item. When the item set changes and a different item now holds the highlighted index, the highlight goes to the
  // first item under the automatic highlight, else it is cleared; an item set that leaves the highlighted item in place
  // keeps it.
  let previousItems: Element[] | null = null;
  const itemsOf = (): Element[] => Array.from(list.querySelectorAll('[role="menuitem"], [role="menuitemcheckbox"], [role="menuitemradio"]'));
  const onItemsChange = (): void => {
    const items = itemsOf();
    const previous = previousItems;
    previousItems = items;
    if (previous === null || (previous.length === items.length && items.every((item, index) => item === previous[index]))) {
      return;
    }
    const owner = ownerOf();
    if (owner === undefined) {
      return;
    }
    const activeId = owner.getAttribute('aria-activedescendant');
    const active = activeId ? owner.ownerDocument.getElementById(activeId) : null;
    const activeIndex = active === null ? -1 : previous.indexOf(active);
    if (activeIndex !== -1 && items[activeIndex] === previous[activeIndex]) {
      return;
    }
    // FilterDropdownRoot.tsx:89-90: from the query the input holds now.
    const autoHighlight = options.autoHighlight === 'always' || (options.autoHighlight === 'true' && owner.value.trim() !== '');
    if (activeIndex === -1 && !autoHighlight) {
      return;
    }
    highlightListEdge(owner, autoHighlight && items.length > 0 ? 'first' : null, undefined);
  };

  const observer = new MutationObserver(() => {
    reportTexts();
    onItemsChange();
  });
  observer.observe(list, { subtree: true, childList: true, characterData: true });
  previousItems = itemsOf();
  list.addEventListener('mousedown', onMouseDown);
  list.addEventListener('focus', onFocus);
  list.addEventListener('keydown', onKeyDown);
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
      list.removeEventListener('focus', onFocus);
      list.removeEventListener('keydown', onKeyDown);
    },
  };
}

// The Clear (FilterDropdownClear.tsx:46-58, MNU-PTR-21): a press does not take focus from the input, and a click returns
// focus to it.
function attachClear(clear: HTMLElement, initial: MenuFilterOptions): TwinHandle {
  let options = initial;
  let disposed = false;

  const onMouseDown = (event: MouseEvent): void => {
    event.preventDefault();
  };
  const onClick = (): void => {
    const owner = options.menuKey ? owners.get(options.menuKey) : undefined;
    if (owner !== undefined) {
      refocusOwner(owner.input);
    }
  };

  clear.addEventListener('mousedown', onMouseDown);
  clear.addEventListener('click', onClick);

  return {
    update(optionsJson: string): void {
      if (!disposed) {
        options = parseOptions<MenuFilterOptions>(optionsJson);
      }
    },
    dispose(): void {
      if (!disposed) {
        disposed = true;
        clear.removeEventListener('mousedown', onMouseDown);
        clear.removeEventListener('click', onClick);
      }
    },
  };
}

// The Trigger (MenuFilterDropdown.tsx:124-150, MNU-KEY-28): while the popup is open the input holds real focus, so the
// vertical arrows typed on the trigger move to the input and run there, a typed character's default action follows focus
// into the input, and the cross-axis arrows are left out of the list navigation (they drive submenus). The trigger's own
// list handle skips each key it relays. Off in a menubar, whose trigger keys move between triggers (MBR-KEY-14).
function attachTrigger(trigger: HTMLElement, initial: MenuFilterOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let disposed = false;

  const onKeyDown = (event: KeyboardEvent): void => {
    const owner = options.menuKey ? owners.get(options.menuKey) : undefined;
    if (options.open !== true || options.inBar === true || owner === undefined) {
      return;
    }
    // Design point 6 (a): the consumer's onKeyDown may keep the key on the trigger (MenuFilterDropdown.tsx:131-150 runs
    // after the consumer's handler, which preventBaseUIHandler() skips).
    const relays = event.key === 'ArrowUp' || event.key === 'ArrowDown' || (event.key.length === 1 && event.key !== ' ' && !event.ctrlKey && !event.metaKey && !event.altKey);
    if (relays && options.decidesKeyDown === true && isStopVerdict(channel.decide(NAVIGATE_POINT, event, 'run'))) {
      return;
    }
    if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
      skipListNavigation(event);
      event.preventDefault();
      refocusOwner(owner.input);
      replayOnOwner(owner.input, event);
    } else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      skipListNavigation(event);
    } else if (event.key.length === 1 && event.key !== ' ' && !event.ctrlKey && !event.metaKey && !event.altKey) {
      refocusOwner(owner.input);
    }
  };

  // Capture, so the key is marked before the trigger's own handles run.
  trigger.addEventListener('keydown', onKeyDown, true);

  return {
    update(optionsJson: string): void {
      if (!disposed) {
        options = parseOptions<MenuFilterOptions>(optionsJson);
      }
    },
    dispose(): void {
      if (!disposed) {
        disposed = true;
        trigger.removeEventListener('keydown', onKeyDown, true);
      }
    },
  };
}

// A filterable submenu's trigger (MenuFilterSubmenuRoot.tsx:240-276, MNU-KEY-29, KEY-31..33): the parent's cross-axis open
// key opens the submenu through the list-navigation reason, after clearing the parent's highlight (handleSubmenuEnter), or
// re-enters an open one; the submenu's close key closes it and returns to the parent. A key of an IME composition is the
// input's (MNU-EVT-19). The key reaches the trigger re-dispatched by the parent's router, or on its real focus in a plain
// parent; the trigger's own list handles skip it.
function attachSubmenuTrigger(trigger: HTMLElement, initial: MenuFilterOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let disposed = false;

  const onKeyDown = (event: KeyboardEvent): void => {
    if (event.isComposing || event.keyCode === 229) {
      return;
    }
    const parent = options.parentKey ? owners.get(options.parentKey) : undefined;
    const rtl = parent?.options().rtl === true;
    const parentOrientation = options.parentOrientation ?? 'vertical';
    if (options.open === true && isCrossOrientationCloseKey(event.key, options.orientation ?? 'vertical', rtl, false)) {
      skipListNavigation(event);
      event.preventDefault();
      event.stopPropagation();
      channel.report('OnSubmenuKey', { open: false, event: eventInfo(event) });
      exitSubmenu(trigger, options.parentKey, parentOrientation, event);
      return;
    }
    if (!isCrossOrientationOpenKey(event.key, parentOrientation, rtl)) {
      return;
    }
    skipListNavigation(event);
    event.preventDefault();
    event.stopPropagation();
    if (parent !== undefined) {
      highlightListEdge(parent.input, null, event);
    }
    if (options.open === true) {
      const own = options.menuKey ? owners.get(options.menuKey) : undefined;
      if (own !== undefined) {
        refocusOwner(own.input);
      }
      return;
    }
    channel.report('OnSubmenuKey', { open: true, event: eventInfo(event) });
  };

  trigger.addEventListener('keydown', onKeyDown, true);

  return {
    update(optionsJson: string): void {
      if (!disposed) {
        options = parseOptions<MenuFilterOptions>(optionsJson);
      }
    },
    dispose(): void {
      if (!disposed) {
        disposed = true;
        trigger.removeEventListener('keydown', onKeyDown, true);
      }
    },
  };
}

// The Popup (useMenuFilterPopup.ts): the input holds real focus while the menu filters. A press on the popup's own
// background keeps it there (PTR-22); a mouse moving over the popup returns it to the input when it is outside the popup
// (FOC-14); focus landing on the popup itself, the focus manager's fallback, moves on to the input; and the cross-axis
// close key on another control returns focus to the input (KEY-33). A nested popup is portaled outside this one in the
// DOM, so its events never reach this handle.
function attachPopup(popup: HTMLElement, initial: MenuFilterOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let disposed = false;
  // A tap fires compatibility mouse events, which must not focus the input and raise the on-screen keyboard.
  let pointerType = 'mouse';
  // useMenuFilterPopup.ts:25-28: focus that entered a nested popup by keyboard or click stays there while the pointer
  // crosses this popup; focus that merely followed the pointer in (a refocus) follows it back out. The nested popup is
  // portaled outside this one, so its focus is seen on the document.
  let nestedFocus: Element | null = null;

  const ownerOf = (): FocusOwner | undefined => (options.menuKey ? owners.get(options.menuKey) : undefined);

  const onMouseDown = (event: MouseEvent): void => {
    if (event.target === popup) {
      event.preventDefault();
    }
  };
  const trackPointerType = (event: PointerEvent): void => {
    pointerType = event.pointerType || 'mouse';
  };
  // useMenuFilterPopup.ts:50-95: a nested popup's focus stays unless the pointer entered one of this popup's submenu
  // triggers, and not from inside that trigger.
  const restoreInputFocus = (event: MouseEvent, enteredTrigger: boolean): void => {
    const owner = ownerOf();
    if (options.open !== true || owner === undefined) {
      return;
    }
    const active = popup.ownerDocument.activeElement;
    if (active === owner.input || (active !== null && popup.contains(active))) {
      return;
    }
    const target = event.target instanceof Element ? event.target : null;
    const submenuTrigger = target?.closest('[aria-haspopup]') ?? null;
    const triggerInPopup = submenuTrigger !== null && popup.contains(submenuTrigger) ? submenuTrigger : null;
    if (nestedFocus !== null && !(enteredTrigger && triggerInPopup !== null)) {
      return;
    }
    if (enteredTrigger && triggerInPopup !== null && event.relatedTarget instanceof Node && triggerInPopup.contains(event.relatedTarget)) {
      return;
    }
    refocusOwner(owner.input);
  };
  const onMouseMove = (event: MouseEvent): void => {
    if (pointerType === 'mouse') {
      restoreInputFocus(event, false);
    }
  };
  const onMouseOver = (event: MouseEvent): void => {
    if (pointerType === 'mouse' && nestedFocus !== null) {
      restoreInputFocus(event, true);
    }
  };
  const onDocumentFocusIn = (event: FocusEvent): void => {
    const owner = ownerOf();
    const target = event.target;
    if (owner !== undefined && target === owner.input) {
      nestedFocus = null;
    } else if (target instanceof Element && !popup.contains(target) && !refocusing && target.closest('[role="dialog"], [data-rootownerid]') !== null) {
      nestedFocus = target;
    }
  };
  const onFocusIn = (event: FocusEvent): void => {
    const owner = ownerOf();
    if (options.open === true && owner !== undefined && event.target === popup) {
      refocusOwner(owner.input);
    }
  };
  const onKeyDown = (event: KeyboardEvent): void => {
    const owner = ownerOf();
    // MenuFilterSubmenuRoot.tsx:278-292 (MNU-KEY-32): a filterable submenu's popup closes on its cross-axis close key and
    // returns to the parent; a key the input keeps for its caret, or of an IME composition, does not count.
    if (options.submenu === true && owner !== undefined && !inputOwnedKeys.has(event) && !event.isComposing && event.keyCode !== 229) {
      const ownerOptions = owner.options();
      if (isCrossOrientationCloseKey(event.key, ownerOptions.orientation ?? 'vertical', ownerOptions.rtl === true, false)) {
        const trigger = popup.id ? popup.ownerDocument.querySelector<HTMLElement>(`[aria-controls="${CSS.escape(popup.id)}"]`) : null;
        skipListNavigation(event);
        event.preventDefault();
        event.stopPropagation();
        channel.report('OnSubmenuKey', { open: false, event: eventInfo(event) });
        if (trigger !== null) {
          exitSubmenu(trigger, options.parentKey, ownerOptions.orientation ?? 'vertical', event);
        }
        return;
      }
    }
    if (owner === undefined || event.target === owner.input || isTypeable(event.target)) {
      return;
    }
    const ownerOptions = owner.options();
    if (isCrossOrientationCloseKey(event.key, ownerOptions.orientation ?? 'vertical', ownerOptions.rtl === true, false)) {
      refocusOwner(owner.input);
    }
  };

  popup.addEventListener('mousedown', onMouseDown);
  popup.addEventListener('pointerdown', trackPointerType);
  popup.addEventListener('pointerover', trackPointerType);
  popup.addEventListener('pointermove', trackPointerType);
  popup.addEventListener('mousemove', onMouseMove);
  popup.addEventListener('mouseover', onMouseOver);
  popup.addEventListener('focusin', onFocusIn);
  popup.ownerDocument.addEventListener('focusin', onDocumentFocusIn, true);
  popup.addEventListener('keydown', onKeyDown);

  return {
    update(optionsJson: string): void {
      if (!disposed) {
        options = parseOptions<MenuFilterOptions>(optionsJson);
      }
    },
    dispose(): void {
      if (disposed) {
        return;
      }
      disposed = true;
      popup.removeEventListener('mousedown', onMouseDown);
      popup.removeEventListener('pointerdown', trackPointerType);
      popup.removeEventListener('pointerover', trackPointerType);
      popup.removeEventListener('pointermove', trackPointerType);
      popup.removeEventListener('mousemove', onMouseMove);
      popup.removeEventListener('mouseover', onMouseOver);
      popup.removeEventListener('focusin', onFocusIn);
      popup.ownerDocument.removeEventListener('focusin', onDocumentFocusIn, true);
      popup.removeEventListener('keydown', onKeyDown);
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
    case 'popup':
      inner = attachPopup(element, options, channel);
      break;
    case 'trigger':
      inner = attachTrigger(element, options, channel);
      break;
    case 'clear':
      inner = attachClear(element, options);
      break;
    case 'submenu-trigger':
      inner = attachSubmenuTrigger(element, options, channel);
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
