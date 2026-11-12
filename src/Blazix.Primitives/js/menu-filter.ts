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
  isCrossOrientationCloseKey,
  isCrossOrientationOpenKey,
  isMainOrientationKey,
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

// The Inputs by menu key, the focus owners the List and the Popup hand focus back to (filter-dropdown/utils/
// refocusOwner.ts), with the Input's current options (its orientation and direction).
interface FocusOwner {
  input: HTMLInputElement;
  options: () => MenuFilterOptions;
}

const owners = new Map<string, FocusOwner>();

// isTypeableElement (floating-ui-react/utils): a key on a text field is the field's.
function isTypeable(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || target instanceof HTMLTextAreaElement || (target instanceof HTMLInputElement && !['button', 'checkbox', 'radio', 'submit', 'reset', 'image', 'range', 'color', 'file'].includes(target.type)));
}

function refocusOwner(owner: HTMLElement): void {
  owner.focus({ preventScroll: true });
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

  const observer = new MutationObserver(reportTexts);
  observer.observe(list, { subtree: true, childList: true, characterData: true });
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

// The Trigger (MenuFilterDropdown.tsx:124-150, MNU-KEY-28): while the popup is open the input holds real focus, so the
// vertical arrows typed on the trigger move to the input and run there, a typed character's default action follows focus
// into the input, and the cross-axis arrows are left out of the list navigation (they drive submenus). The trigger's own
// list handle skips each key it relays. Off in a menubar, whose trigger keys move between triggers (MBR-KEY-14).
function attachTrigger(trigger: HTMLElement, initial: MenuFilterOptions): TwinHandle {
  let options = initial;
  let disposed = false;

  const onKeyDown = (event: KeyboardEvent): void => {
    const owner = options.menuKey ? owners.get(options.menuKey) : undefined;
    if (options.open !== true || options.inBar === true || owner === undefined) {
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

// The Popup (useMenuFilterPopup.ts): the input holds real focus while the menu filters. A press on the popup's own
// background keeps it there (PTR-22); a mouse moving over the popup returns it to the input when it is outside the popup
// (FOC-14); focus landing on the popup itself, the focus manager's fallback, moves on to the input; and the cross-axis
// close key on another control returns focus to the input (KEY-33). A nested popup is portaled outside this one in the
// DOM, so its events never reach this handle.
function attachPopup(popup: HTMLElement, initial: MenuFilterOptions): TwinHandle {
  let options = initial;
  let disposed = false;
  // A tap fires compatibility mouse events, which must not focus the input and raise the on-screen keyboard.
  let pointerType = 'mouse';

  const ownerOf = (): FocusOwner | undefined => (options.menuKey ? owners.get(options.menuKey) : undefined);

  const onMouseDown = (event: MouseEvent): void => {
    if (event.target === popup) {
      event.preventDefault();
    }
  };
  const trackPointerType = (event: PointerEvent): void => {
    pointerType = event.pointerType || 'mouse';
  };
  const onMouseMove = (): void => {
    const owner = ownerOf();
    if (pointerType !== 'mouse' || options.open !== true || owner === undefined) {
      return;
    }
    const active = popup.ownerDocument.activeElement;
    if (active === owner.input || (active !== null && popup.contains(active))) {
      return;
    }
    refocusOwner(owner.input);
  };
  const onFocusIn = (event: FocusEvent): void => {
    const owner = ownerOf();
    if (options.open === true && owner !== undefined && event.target === popup) {
      refocusOwner(owner.input);
    }
  };
  const onKeyDown = (event: KeyboardEvent): void => {
    const owner = ownerOf();
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
  popup.addEventListener('focusin', onFocusIn);
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
      popup.removeEventListener('focusin', onFocusIn);
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
      inner = attachPopup(element, options);
      break;
    case 'trigger':
      inner = attachTrigger(element, options);
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
