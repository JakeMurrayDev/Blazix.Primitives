// The `combobox` component module (ADR 04 §1's component row, written by M5 track 10 under Amendments 2026-11-03 (4); the
// `value` writer of (6); the `clear` and `remove` points of (3); contract docs/components/combobox.md). One handle per part
// element, composing `composite` (list navigation, the highlight's writes), `interactions` (presses, dismissal) and `dom`,
// with no copy of their logic and no page-global state:
// - The input's handle: composition (CMB-KEY-14; not on Android; `which === 229`), the `inputType` classification, typed
//   against autofill-like (ComboboxInput.tsx:239-247; KEY-17, STA-21), the scroll reset on a typed change (PTR-12; none when
//   virtualized), Home/End's caret and `scrollLeft` with Gecko's RTL swap (KEY-06), Enter's synthetic click on the
//   highlighted item, prevented and stopped, the keydown recorded (KEY-04, API-60, FRM-07), the closed Escape's `clear`
//   (KEY-05, EVT-09; fallback "stop when there is something to clear and the root is not inline"), Backspace into an empty
//   input toward the chips (KEY-08), the start dismiss button's press, and the `value` writer: a committed value from
//   `update` is written when it is not the echo of a change this handle reported, so a lagging commit never overwrites
//   newer typing and a controlled value the consumer did not apply is re-asserted.
// - The popup's handle: the focus redirect to the input (ComboboxPopup.tsx:90-98) and the end dismiss button's press.
// - The list's handle: the items' pointerdown and mousedown defaults and the drag-select release (PTR-03..05).
// - The trigger's handle: the press's focus move and its one-shot mouseup (`cancel-open`, PTR-02, 03). The input group's
//   and the chips' handles: the input press (PTR-08, handleInputPress.ts); the chips' also the chip keys (KEY-07..10). The
//   clear's: the press default; the chip removes': the press default and the `remove` point.
// Transcribed from Base UI v1.9.0: combobox/input/ComboboxInput.tsx:212-430, combobox/root/AriaCombobox.tsx:681-707,
// combobox/popup/ComboboxPopup.tsx:85-99, combobox/item/ComboboxItem.tsx:146-185, combobox/utils/parts.ts:46-58.
import { closest, contains, getTarget, isHTMLElement, isMouseWithinBounds, isScrollableY, platform as runtimePlatform } from './dom';
import { eventInfo } from './event-info';
import { isInteractiveElement } from './interactions';
import type { Platform } from './platform';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  resolveReference,
  type DecisionPolicy,
  type DotNetTwin,
  type TwinCaptures,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

// ---------------------------------------------------------------------------------------------------------------
// Constants and types
// ---------------------------------------------------------------------------------------------------------------

/** The decision point asked before the closed Escape clears. */
export const CLEAR_POINT = 'clear';

/** The decision point asked before a chip's removal. */
export const REMOVE_POINT = 'remove';

/** The decision point the input asks before its own keys while C# holds the consumer's `onKeyDown` (ADR 04 Amendments 2026-11-10). */
export const NAVIGATE_POINT = 'navigate';

/** The decision point the chips and the input group ask before a press while C# holds the consumer's `onMouseDown`. */
export const PRESS_POINT = 'press';

/** The part literals; mirror `ComboboxTwin`'s constants. */
export const INPUT_PART = 'input';
export const TRIGGER_PART = 'trigger';
export const CLEAR_PART = 'clear';
export const INPUT_GROUP_PART = 'input-group';
export const CHIPS_PART = 'chips';
export const CHIP_REMOVE_PART = 'chip-remove';
export const LIST_PART = 'list';
export const POPUP_PART = 'popup';

/** The options of a `combobox` handle; mirrors `ComboboxOptions` in C#. */
export interface ComboboxOptions extends TwinOptions {
  part?: string;
  insidePopup?: boolean;
  inline?: boolean;
  disabled?: boolean;
  readOnly?: boolean;
  multiple?: boolean;
  hasValue?: boolean;
  virtualized?: boolean;
  chipsId?: string | null;
  listId?: string | null;
  popupId?: string | null;
  value?: string | null;
  valueRevision?: number;
  open?: boolean;
  mounted?: boolean;
  rtl?: boolean;
  inputId?: string | null;
  touchOpened?: boolean;
  chipIds?: readonly string[] | null;
  index?: number;
  dismissId?: string | null;
  submitRevision?: number | null;
  submitFormElement?: number | null;
  decidesKeyDown?: boolean;
  decidesPress?: boolean;
  focusOnAttach?: boolean;
}

/** The payload of a classified change of the input text; mirrors `ComboboxInputChange`. */
export interface ComboboxInputChange {
  text: string;
  typed: boolean;
  composing: boolean;
  event: unknown;
}

const handleKey = Symbol('blazix.combobox.handle');

// The chips container's handle keeps its chips' ids, in order, on its element, where the input's handle reads them.
const chipIdsKey = Symbol('blazix.combobox.chipIds');

interface ComboboxElement extends HTMLElement {
  [handleKey]?: TwinHandle;
  [chipIdsKey]?: readonly string[];
}

function chipIdsOf(doc: Document, chipsId: string | null | undefined): readonly string[] {
  const chips = chipsId ? (doc.getElementById(chipsId) as ComboboxElement | null) : null;
  return chips?.[chipIdsKey] ?? [];
}

// combobox/utils/parts.ts:28-32: the key toward the previous chip, then the key toward the next one.
function chipNavigationKeys(rtl: boolean): readonly [string, string] {
  return rtl ? ['ArrowRight', 'ArrowLeft'] : ['ArrowLeft', 'ArrowRight'];
}

// ---------------------------------------------------------------------------------------------------------------
// Pure helpers (exported for the L3 titles)
// ---------------------------------------------------------------------------------------------------------------

/**
 * Classifies an `input` event: typed when it carries an `inputType` other than `insertReplacementText`, or arrives during a
 * composition; otherwise autofill-like, which opens nothing (ComboboxInput.tsx:239-247).
 * @param event The `input` event.
 * @param composing Whether a composition is in progress.
 * @returns `true` for typed input.
 */
export function isTypedInput(event: Event, composing: boolean): boolean {
  if (composing || event.type === 'compositionend') {
    return true;
  }
  const inputType = (event as InputEvent).inputType;
  return inputType != null && inputType !== '' && inputType !== 'insertReplacementText';
}

/**
 * Where Home and End put the caret: Gecko swaps them in a right-to-left input (ComboboxInput.tsx:333-348).
 * @param key `Home` or `End`.
 * @param length The input text's length.
 * @param rtl Whether the input reads right to left.
 * @param gecko Whether the engine is Gecko.
 * @returns The caret offset.
 */
export function caretFor(key: 'Home' | 'End', length: number, rtl: boolean, gecko: boolean): number {
  if (key === 'Home') {
    return gecko && rtl ? length : 0;
  }
  return gecko && rtl ? 0 : length;
}

/**
 * The value writer's decision for a committed value: `null` when it echoes a change the handle reported (those are
 * consumed), else the value to write when it differs from the live text.
 * @param pending The texts the handle reported and C# has not answered yet, oldest first; mutated.
 * @param committed The committed value from `update`.
 * @param live The input's live text.
 * @returns The text to write, or `null` for none.
 */
export function resolveCommittedValue(pending: string[], committed: string, live: string): string | null {
  const echo = pending.indexOf(committed);
  if (echo !== -1) {
    pending.splice(0, echo + 1);
    return null;
  }
  pending.length = 0;
  return committed === live ? null : committed;
}

function stopEvent(event: Event): void {
  event.preventDefault();
  event.stopPropagation();
}

function isStopVerdict(verdict: DecisionPolicy): boolean {
  return verdict === 'stop' || verdict === 'prevent-and-stop';
}

// ---------------------------------------------------------------------------------------------------------------
// The input's handle
// ---------------------------------------------------------------------------------------------------------------

function attachInput(element: HTMLInputElement, initial: ComboboxOptions, channel: TwinChannel, platform: Platform): TwinHandle {
  let options = initial;
  let composing = false;
  let compositionText: string | null = null;
  let lastRevision = initial.valueRevision ?? 0;
  let lastSubmit = initial.submitRevision ?? 0;
  const pending: string[] = [];
  const doc = element.ownerDocument;

  const report = (text: string, typed: boolean, isComposing: boolean, event: Event): void => {
    if (!isComposing) {
      pending.push(text);
    }
    const payload: ComboboxInputChange = { text, typed, composing: isComposing, event: eventInfo(event) };
    channel.report('OnInputChange', payload);
  };

  // AriaCombobox.tsx:681-707: a typed change resets the list's scroll before paint, unless the list is virtualized.
  const resetListScroll = (): void => {
    if (options.virtualized || !options.listId) {
      return;
    }
    const list = doc.getElementById(options.listId);
    if (!list) {
      return;
    }
    const popup = options.popupId ? doc.getElementById(options.popupId) : null;
    let ancestor: Element | null = list.firstElementChild ?? list;
    while (ancestor && isHTMLElement(ancestor)) {
      if (popup ? !contains(popup, ancestor) : ancestor.getAttribute('role') === 'dialog') {
        break;
      }
      if (isScrollableY(ancestor)) {
        ancestor.scrollTop = 0;
        break;
      }
      ancestor = ancestor.parentElement;
    }
  };

  const onCompositionStart = (): void => {
    // CMB-KEY-14: Android's keyboards report every word as composing, so composition is not tracked there.
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
    compositionText = element.value;
    resetListScroll();
    report(element.value, true, false, event);
  };

  const onInput = (event: Event): void => {
    // Chromium can follow compositionend with an input of the same text; it was reported at the end of the composition.
    if (!composing && compositionText !== null && element.value === compositionText) {
      compositionText = null;
      return;
    }
    compositionText = null;
    const typed = isTypedInput(event, composing);
    // AriaCombobox.tsx:676-707 resets inside the handler, before the highlight its commit scrolls to; on WebAssembly the report
    // runs C#, and a restored highlight's scroll, before it returns, so the reset goes first (CMB-PTR-13).
    if (typed && !composing) {
      resetListScroll();
    }
    report(element.value, typed, composing, event);
  };

  const clickHighlighted = (event: KeyboardEvent): boolean => {
    const activeId = element.getAttribute('aria-activedescendant');
    const item = activeId ? doc.getElementById(activeId) : null;
    if (!item) {
      return false;
    }
    stopEvent(event);
    // parts.ts:46-58 (clickHighlightedItem): the selection is attributed to the keydown for the click only; the second report
    // ends the attribution, so a click that selected nothing leaves none for a later selection.
    channel.report('OnSelectionEvent', { event: eventInfo(event) });
    item.click();
    channel.report('OnSelectionEvent', { event: null });
    return true;
  };

  // Base UI's `mounted`: on Server the update that carries a finished close can trail the next keydown, so a popup list no
  // longer in the document counts as unmounted (an inline list stays in the document and reads the options alone).
  const isMounted = (): boolean => {
    if (!options.mounted) {
      return false;
    }
    if (options.inline || !options.listId) {
      return true;
    }
    return doc.getElementById(options.listId) !== null;
  };

  // Base UI reads `mounted` from the render, so its value when the keydown began. The session's trigger half listens on
  // this input too, and on WebAssembly its Escape commits the close inside the keydown, before this handle's listener when
  // it attached first; the document's capture phase runs before both, so the value is recorded there.
  let keyDownAtStart: KeyboardEvent | null = null;
  let mountedAtStart = false;
  const onKeyDownCapture = (event: KeyboardEvent): void => {
    keyDownAtStart = event;
    mountedAtStart = isMounted();
  };

  const handleKey = (event: KeyboardEvent): void => {
    if (event.ctrlKey || event.shiftKey || event.altKey || event.metaKey) {
      return;
    }
    if (options.disabled || options.readOnly) {
      // Browsing can highlight an item, and Enter there must not submit the form.
      if (options.readOnly && event.key === 'Enter' && options.open && element.hasAttribute('aria-activedescendant')) {
        stopEvent(event);
      }
      return;
    }
    if (event.key === 'Home' || event.key === 'End') {
      stopEvent(event);
      const rtl = options.rtl ?? false;
      const cursor = caretFor(event.key, element.value.length, rtl, platform.engine.gecko);
      element.setSelectionRange(cursor, cursor);
      const scrollAmount = element.scrollWidth - element.clientWidth;
      element.scrollLeft = event.key === 'Home' ? 0 : rtl ? -scrollAmount : scrollAmount;
      return;
    }
    if (event.key === 'Escape' && !(event === keyDownAtStart ? mountedAtStart : isMounted())) {
      // CMB-KEY-05, EVT-09: the closed Escape clears; the point's fallback stops the keydown when there is something to
      // clear and the root is not inline. On Server the clear runs from the report a round trip later.
      const fallback: DecisionPolicy = options.hasValue && !options.inline ? 'stop' : 'run';
      const verdict = channel.decide(CLEAR_POINT, event, fallback);
      if (!channel.lastDecisionWasSynchronous) {
        channel.report('OnClear', { index: -1, event: eventInfo(event) });
      }
      if (isStopVerdict(verdict)) {
        event.stopPropagation();
      }
      return;
    }
    if (event.key === 'Backspace' && element.value === '' && options.multiple && options.hasValue && options.chipsId) {
      const rendered = chipIdsOf(doc, options.chipsId).length;
      channel.report('OnRemove', { index: rendered, event: eventInfo(event) });
      return;
    }
    // CMB-KEY-07 (ComboboxInput.tsx:149-168): the key toward the previous chip at the input's start focuses the last chip.
    if (
      options.multiple &&
      options.hasValue &&
      options.chipsId &&
      event.key === chipNavigationKeys(options.rtl ?? false)[0] &&
      (element.selectionStart ?? 0) === 0
    ) {
      event.preventDefault();
      const last = chipIdsOf(doc, options.chipsId).at(-1);
      if (last !== undefined) {
        doc.getElementById(last)?.focus();
      }
    }
    // event.isComposing
    if (event.which === 229) {
      return;
    }
    if (event.key === 'Enter' && options.open) {
      if (clickHighlighted(event)) {
        return;
      }
      if (options.inline) {
        return;
      }
      // Enter without a highlight closes with `none` and leaves the keydown to the form (FRM-07).
      channel.report('OnEnterClose', { event: eventInfo(event) });
    }
  };

  // ADR 04 Amendments 2026-11-10 (design point 6, chosen by the user): Base UI merges the consumer's onKeyDown before the
  // input's own handler, so its preventBaseUIHandler() skips the input's keys. While C# holds that handler, every keydown
  // asks `navigate` first: on WebAssembly C# runs it inside the dispatch, once per event for this handle and the composite
  // reference handle; on Server the point falls back to `run`, and a key this handle stopped, which Blazor never
  // dispatches, is reported so the handler still runs, a round trip late.
  const onKeyDown = (event: KeyboardEvent): void => {
    if (options.decidesKeyDown && isStopVerdict(channel.decide(NAVIGATE_POINT, event, 'run'))) {
      return;
    }
    const stoppedBefore = event.cancelBubble;
    handleKey(event);
    if (options.decidesKeyDown && !channel.synchronous && !stoppedBefore && event.cancelBubble) {
      channel.report('OnKeyHandled', eventInfo(event));
    }
  };

  // CMB-STR-07: the start dismiss button, found by the id C# rendered it with, while the popup is open and modal.
  const onDocumentClick = (event: MouseEvent): void => {
    const target = getTarget(event) as Element | null;
    if (options.dismissId && target?.id === options.dismissId) {
      channel.report('OnDismiss', eventInfo(event));
    }
  };

  element.addEventListener('compositionstart', onCompositionStart);
  element.addEventListener('compositionend', onCompositionEnd);
  element.addEventListener('input', onInput);
  element.addEventListener('keydown', onKeyDown);
  doc.addEventListener('keydown', onKeyDownCapture, true);
  doc.addEventListener('click', onDocumentClick, true);
  // A chip's arrow key opened the popup and focused an input that had not mounted yet (ComboboxChip.tsx); it takes the focus
  // as it attaches.
  if (initial.focusOnAttach) {
    element.focus();
  }

  return {
    update(optionsJson: string, references?: TwinReferences) {
      options = parseOptions<ComboboxOptions>(optionsJson);
      // ComboboxItem.tsx:150-158, AriaCombobox.tsx:945-948: an item press of a root that submits on item click submits, once
      // its selection rendered (which this update follows), the hidden validation input's form, else the input's.
      const submit = options.submitRevision ?? 0;
      if (submit !== lastSubmit) {
        lastSubmit = submit;
        const hidden = resolveReference<HTMLInputElement>(references, options.submitFormElement);
        const form = hidden?.form ?? element.form;
        if (form && typeof form.requestSubmit === 'function') {
          form.requestSubmit();
        }
      }
      const revision = options.valueRevision ?? 0;
      if (revision !== lastRevision && options.value != null) {
        lastRevision = revision;
        const next = resolveCommittedValue(pending, options.value, element.value);
        if (next !== null) {
          element.value = next;
        }
      }
    },
    dispose() {
      element.removeEventListener('compositionstart', onCompositionStart);
      element.removeEventListener('compositionend', onCompositionEnd);
      element.removeEventListener('input', onInput);
      element.removeEventListener('keydown', onKeyDown);
      doc.removeEventListener('keydown', onKeyDownCapture, true);
      doc.removeEventListener('click', onDocumentClick, true);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The popup's and the list's handles
// ---------------------------------------------------------------------------------------------------------------

function attachPopup(element: HTMLElement, initial: ComboboxOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  const doc = element.ownerDocument;

  // ComboboxPopup.tsx:90-98: focus landing on the list or the popup itself goes back to the input, except after a touch open.
  const onFocusIn = (event: FocusEvent): void => {
    if (options.touchOpened || !options.inputId) {
      return;
    }
    const target = getTarget(event) as Element | null;
    const list = options.listId ? doc.getElementById(options.listId) : null;
    if (target === element || (list !== null && contains(list, target))) {
      doc.getElementById(options.inputId)?.focus();
    }
  };

  // CMB-STR-07: the end dismiss button, found by the id C# rendered it with.
  const onDocumentClick = (event: MouseEvent): void => {
    const target = getTarget(event) as Element | null;
    if (options.dismissId && target?.id === options.dismissId) {
      channel.report('OnDismiss', eventInfo(event));
    }
  };

  element.addEventListener('focusin', onFocusIn);
  doc.addEventListener('click', onDocumentClick, true);
  return {
    update(optionsJson: string) {
      options = parseOptions<ComboboxOptions>(optionsJson);
    },
    dispose() {
      element.removeEventListener('focusin', onFocusIn);
      doc.removeEventListener('click', onDocumentClick, true);
    },
  };
}

function itemOf(list: HTMLElement, target: EventTarget | null): HTMLElement | null {
  let node = target as Element | null;
  while (node && node !== list) {
    const role = node.getAttribute('role');
    if (role === 'option' || role === 'gridcell') {
      return node as HTMLElement;
    }
    node = node.parentElement;
  }
  return null;
}

function attachList(element: HTMLElement, initial: ComboboxOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let pointerDownItem: Element | null = null;

  // ComboboxItem.tsx:146-185: the item keeps the focus on the input (pointerdown and mousedown defaults); a mouseup on a
  // highlighted item that the press did not start on is a drag-select release, which clicks it.
  const onPointerDown = (event: PointerEvent): void => {
    const item = itemOf(element, getTarget(event));
    if (!item) {
      return;
    }
    if (event.isPrimary) {
      pointerDownItem = item;
    }
    event.preventDefault();
  };

  const onMouseDown = (event: MouseEvent): void => {
    if (itemOf(element, getTarget(event))) {
      event.preventDefault();
    }
  };

  const onMouseUp = (event: MouseEvent): void => {
    const item = itemOf(element, getTarget(event));
    if (!item) {
      return;
    }
    const startedOnItem = pointerDownItem === item;
    pointerDownItem = null;
    if (options.disabled || options.readOnly || event.button !== 0 || startedOnItem || !item.hasAttribute('data-highlighted')) {
      return;
    }
    if (item.hasAttribute('data-disabled')) {
      return;
    }
    item.click();
  };

  // CMB-PTR-05 (AriaCombobox.tsx:885-892): a press inside an `<a href>` is the link's. This listener runs before Blazor's
  // delegated one, so C# reads the press before the click's selection, and its end after the click was dispatched.
  const onClick = (event: MouseEvent): void => {
    const target = getTarget(event) as Element | null;
    if (!itemOf(element, target)) {
      return;
    }
    const href = closest<HTMLAnchorElement>(target, 'a')?.getAttribute('href');
    if (!href) {
      return;
    }
    channel.report('OnItemLinkPress', { href });
    setTimeout(() => channel.report('OnItemLinkPress', { href: null }), 0);
  };

  // ComboboxList.tsx:83-99: the list's own Enter clicks the highlighted item, attributed to the keydown as the input's is
  // (parts.ts:46-58); with no highlight the keydown is left to the form.
  const onKeyDown = (event: KeyboardEvent): void => {
    if (options.disabled || options.readOnly || event.key !== 'Enter') {
      return;
    }
    const item = element.querySelector('[role="option"][data-highlighted]');
    if (!(item instanceof HTMLElement)) {
      return;
    }
    stopEvent(event);
    channel.report('OnSelectionEvent', { event: eventInfo(event) });
    item.click();
    channel.report('OnSelectionEvent', { event: null });
  };

  element.addEventListener('pointerdown', onPointerDown, true);
  element.addEventListener('mousedown', onMouseDown);
  element.addEventListener('mouseup', onMouseUp);
  element.addEventListener('click', onClick);
  element.addEventListener('keydown', onKeyDown);
  return {
    update(optionsJson: string) {
      options = parseOptions<ComboboxOptions>(optionsJson);
      if (!options.open) {
        pointerDownItem = null;
      }
    },
    dispose() {
      element.removeEventListener('pointerdown', onPointerDown, true);
      element.removeEventListener('mousedown', onMouseDown);
      element.removeEventListener('mouseup', onMouseUp);
      element.removeEventListener('click', onClick);
      element.removeEventListener('keydown', onKeyDown);
    },
  };
}

// The trigger's handle (PTR-02, PTR-03; ComboboxTrigger.tsx:96-100,177-233): a press that is not a touch focuses the input
// and, with the input outside the popup, keeps the focus there. A press while closed with the input inside the popup waits
// for its release once: a release away from the trigger, the popup and the list, and more than 5px outside the trigger's
// bounds, cancels the open (`cancel-open`). A release after the trigger left the document is ignored.
function attachTrigger(element: HTMLElement, initial: ComboboxOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let pointerType = '';
  let pendingRelease: ((event: MouseEvent) => void) | null = null;
  const doc = element.ownerDocument;
  const stopWaiting = (): void => {
    if (pendingRelease) {
      doc.removeEventListener('mouseup', pendingRelease);
      pendingRelease = null;
    }
  };
  const trackPointer = (event: PointerEvent): void => {
    pointerType = event.pointerType;
  };
  const onMouseDown = (event: MouseEvent): void => {
    if (options.disabled) {
      return;
    }
    if (pointerType !== 'touch') {
      const input = options.inputId ? doc.getElementById(options.inputId) : null;
      input?.focus();
      if (!options.insidePopup) {
        event.preventDefault();
      }
    }
    if (options.open || !options.insidePopup) {
      return;
    }
    stopWaiting();
    const onRelease = (release: MouseEvent): void => {
      pendingRelease = null;
      if (!element.isConnected) {
        return;
      }
      const target = getTarget(release) as Element | null;
      const popup = options.popupId ? doc.getElementById(options.popupId) : null;
      const list = options.listId ? doc.getElementById(options.listId) : null;
      if (contains(element, target) || contains(popup, target) || contains(list, target) || isMouseWithinBounds(release, element)) {
        return;
      }
      channel.report('OnCancelOpen', eventInfo(release));
    };
    pendingRelease = onRelease;
    doc.addEventListener('mouseup', onRelease, { once: true });
  };
  // ComboboxTrigger.tsx:186-189: a blur into the popup is not a blur; any other is reported for the field.
  const onFocusOut = (event: FocusEvent): void => {
    const related = event.relatedTarget as Element | null;
    const popup = options.popupId ? doc.getElementById(options.popupId) : null;
    const list = options.listId ? doc.getElementById(options.listId) : null;
    if (contains(popup, related) || contains(list, related)) {
      return;
    }
    channel.report('OnBlur', eventInfo(event));
  };
  // ComboboxTrigger.tsx:247-256: ArrowDown or ArrowUp stops the key, asks for the open with list-navigation and focuses the
  // input (the trigger holds no list-navigation props; the input is the list's reference in both layouts).
  // A disabled trigger handles no keys: useButton.ts:116-119 returns first (ADR 04 Amendments 2026-11-10).
  const onKeyDown = (event: KeyboardEvent): void => {
    if (options.disabled || (event.key !== 'ArrowDown' && event.key !== 'ArrowUp')) {
      return;
    }
    stopEvent(event);
    channel.report('OnOpenRequest', eventInfo(event));
    (options.inputId ? doc.getElementById(options.inputId) : null)?.focus();
  };
  element.addEventListener('pointerdown', trackPointer);
  element.addEventListener('pointerenter', trackPointer);
  element.addEventListener('mousedown', onMouseDown);
  element.addEventListener('focusout', onFocusOut);
  element.addEventListener('keydown', onKeyDown);
  return {
    update(optionsJson: string) {
      options = parseOptions<ComboboxOptions>(optionsJson);
    },
    dispose() {
      stopWaiting();
      element.removeEventListener('pointerdown', trackPointer);
      element.removeEventListener('pointerenter', trackPointer);
      element.removeEventListener('mousedown', onMouseDown);
      element.removeEventListener('focusout', onFocusOut);
      element.removeEventListener('keydown', onKeyDown);
    },
  };
}

// The chips container's handle (KEY-07..10; ComboboxChip.tsx:39-129): the input press of its element, and the keys of its
// chips, found by id among the ids C# sends in order. The key toward the previous or next chip moves the focus there, or
// to the input past either end; Backspace and Delete report the chip's removal and focus the chip that takes its place, or
// the input; Enter, Space, a printable key and ArrowDown/ArrowUp (which also asks for the open) move the focus to the input.
function attachChips(element: ComboboxElement, initial: ComboboxOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  element[chipIdsKey] = options.chipIds ?? [];
  const press = attachInputPress(element, initial, channel, false);
  const doc = element.ownerDocument;
  const onKeyDown = (event: KeyboardEvent): void => {
    if (options.disabled || options.readOnly) {
      return;
    }
    const ids = options.chipIds ?? [];
    let node = getTarget(event) instanceof Element ? (getTarget(event) as Element) : null;
    while (node && node !== element && !ids.includes(node.id)) {
      node = node.parentElement;
    }
    if (!node || node === element) {
      return;
    }
    const index = ids.indexOf(node.id);
    const [previousKey, nextKey] = chipNavigationKeys(options.rtl ?? false);
    let next: string | undefined = ids[index];
    if (event.key === previousKey) {
      event.preventDefault();
      next = index > 0 ? ids[index - 1] : undefined;
    } else if (event.key === nextKey) {
      event.preventDefault();
      next = index < ids.length - 1 ? ids[index + 1] : undefined;
    } else if (event.key === 'Backspace' || event.key === 'Delete') {
      // getIndexAfterChipRemoval (parts.ts:37-40) in the old order: the next chip, else the previous one.
      stopEvent(event);
      channel.report('OnRemove', { index, event: eventInfo(event) });
      next = index < ids.length - 1 ? ids[index + 1] : index > 0 ? ids[index - 1] : undefined;
    } else if (event.key === 'Enter' || event.key === ' ') {
      stopEvent(event);
      next = undefined;
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      stopEvent(event);
      channel.report('OnOpenRequest', eventInfo(event));
      next = undefined;
    } else if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      next = undefined;
    }
    if (next === undefined) {
      (options.inputId ? doc.getElementById(options.inputId) : null)?.focus();
    } else {
      doc.getElementById(next)?.focus();
    }
  };
  element.addEventListener('keydown', onKeyDown);
  return {
    update(optionsJson: string) {
      options = parseOptions<ComboboxOptions>(optionsJson);
      element[chipIdsKey] = options.chipIds ?? [];
      press.update(optionsJson);
    },
    dispose() {
      element.removeEventListener('keydown', onKeyDown);
      delete element[chipIdsKey];
      press.dispose();
    },
  };
}

// A chip remove button's handle (PTR-07; ComboboxChipRemove.tsx:75-121): the press default, and a click or Enter/Space that
// asks the `remove` point (fallback stop) with the chip's index. On Server the removal runs from the report; a stop verdict
// stops the click, or prevents and stops the key. The focus then moves to the input.
function attachChipRemove(element: HTMLElement, initial: ComboboxOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  const doc = element.ownerDocument;
  const onMouseDown = (event: MouseEvent): void => {
    event.preventDefault();
  };
  const remove = (event: Event, isKey: boolean): void => {
    if (options.disabled || options.readOnly) {
      return;
    }
    const verdict = channel.decide(REMOVE_POINT, event, 'stop');
    if (!channel.lastDecisionWasSynchronous) {
      channel.report('OnRemove', { index: options.index ?? -1, event: eventInfo(event) });
    }
    if (isStopVerdict(verdict)) {
      if (isKey) {
        stopEvent(event);
      } else {
        event.stopPropagation();
      }
    }
    (options.inputId ? doc.getElementById(options.inputId) : null)?.focus();
  };
  const onClick = (event: MouseEvent): void => remove(event, false);
  const onKeyDown = (event: KeyboardEvent): void => {
    if (event.key === 'Enter' || event.key === ' ') {
      remove(event, true);
    }
  };
  element.addEventListener('mousedown', onMouseDown);
  element.addEventListener('click', onClick);
  element.addEventListener('keydown', onKeyDown);
  return {
    update(optionsJson: string) {
      options = parseOptions<ComboboxOptions>(optionsJson);
    },
    dispose() {
      element.removeEventListener('mousedown', onMouseDown);
      element.removeEventListener('click', onClick);
      element.removeEventListener('keydown', onKeyDown);
    },
  };
}

// The input group's and the chips' press (PTR-08; combobox/utils/handleInputPress.ts): a press on the element, or on a
// descendant that is not interactive (nor, for the group, inside the chips container), keeps the focus off the element,
// focuses the input and reports the press, which opens the popup when the root opens on input click.
function attachInputPress(element: HTMLElement, initial: ComboboxOptions, channel: TwinChannel, ignoreChips: boolean): TwinHandle {
  let options = initial;
  const onMouseDown = (event: MouseEvent): void => {
    // ADR 04 Amendments 2026-11-10: the consumer's onMouseDown runs first, and its preventBaseUIHandler() skips the press
    // (handleInputPress.ts:9); on Server the point falls back to `run`.
    if (options.decidesPress && isStopVerdict(channel.decide(PRESS_POINT, event, 'run'))) {
      return;
    }
    const target = getTarget(event);
    const targetElement = target instanceof Element ? target : null;
    const doc = element.ownerDocument;
    if (targetElement !== element) {
      const chips = ignoreChips && options.chipsId ? doc.getElementById(options.chipsId) : null;
      if (contains(chips, targetElement) || isInteractiveElement(targetElement)) {
        return;
      }
    }
    event.preventDefault();
    if (options.disabled) {
      return;
    }
    const input = options.inputId ? doc.getElementById(options.inputId) : null;
    input?.focus();
    channel.report('OnInputPress', eventInfo(event));
  };
  element.addEventListener('mousedown', onMouseDown);
  return {
    update(optionsJson: string) {
      options = parseOptions<ComboboxOptions>(optionsJson);
    },
    dispose() {
      element.removeEventListener('mousedown', onMouseDown);
    },
  };
}

// The clear button's press default (PTR-07): a press there keeps the focus where it is, so the input stays focused; a click
// that is not disabled or read-only moves the focus to the input (ComboboxClear.tsx:111), after C#'s clear is dispatched.
function attachPressDefault(element: HTMLElement, initial: ComboboxOptions): TwinHandle {
  let options = initial;
  const input = (): HTMLElement | null => (options.inputId ? element.ownerDocument.getElementById(options.inputId) : null);
  // ComboboxClear.tsx:98-100: every press keeps the focus where it is.
  const onMouseDown = (event: MouseEvent): void => {
    event.preventDefault();
  };
  const onClick = (): void => {
    if (options.disabled || options.readOnly) {
      return;
    }
    input()?.focus();
  };
  element.addEventListener('mousedown', onMouseDown);
  element.addEventListener('click', onClick);
  return {
    update(optionsJson: string) {
      options = parseOptions<ComboboxOptions>(optionsJson);
    },
    dispose() {
      element.removeEventListener('mousedown', onMouseDown);
      element.removeEventListener('click', onClick);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The twin-facing entry
// ---------------------------------------------------------------------------------------------------------------

/**
 * Creates the `attach` entry for a platform, so the L3 titles inject Gecko's and Android's flags.
 * @param platform The platform flags the handles read.
 * @returns The `attach` function.
 */
export function createAttach(platform: Platform): typeof attach {
  return (elementOrId, optionsJson, twinReference, _references, captures) => {
    const element = resolveElement(elementOrId, captures?.[0]) as ComboboxElement;
    const existing = element[handleKey];
    if (existing) {
      return existing;
    }
    const options = parseOptions<ComboboxOptions>(optionsJson);
    const channel = createTwinChannel(twinReference);
    let handle: TwinHandle;
    switch (options.part ?? INPUT_PART) {
      case INPUT_PART:
        handle = attachInput(element as HTMLInputElement, options, channel, platform);
        break;
      case POPUP_PART:
        handle = attachPopup(element, options, channel);
        break;
      case LIST_PART:
        handle = attachList(element, options, channel);
        break;
      case TRIGGER_PART:
        handle = attachTrigger(element, options, channel);
        break;
      case INPUT_GROUP_PART:
        handle = attachInputPress(element, options, channel, true);
        break;
      case CHIPS_PART:
        handle = attachChips(element, options, channel);
        break;
      case CHIP_REMOVE_PART:
        handle = attachChipRemove(element, options, channel);
        break;
      default:
        handle = attachPressDefault(element, options);
        break;
    }
    const owned: TwinHandle = {
      update: handle.update,
      dispose() {
        handle.dispose();
        delete element[handleKey];
      },
    };
    element[handleKey] = owned;
    return owned;
  };
}

/**
 * Attaches a combobox part's handle.
 * @param elementOrId The element, or its id when the part has no element reference.
 * @param optionsJson The `ComboboxOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param _references The references, which this module names none of.
 * @param captures The capture ids C# sends beside the element, which find it inside a shadow root.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  _references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  return createAttach(runtimePlatform)(elementOrId, optionsJson, twinReference, _references, captures);
}
