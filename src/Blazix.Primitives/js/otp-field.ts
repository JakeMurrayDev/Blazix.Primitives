// The `otp-field` component module (ADR 04 §1; Amendments 2026-11-18, the scope cell, `edit` and variant (ii); Amendments
// 2026-11-19, design points 1–7 of M6 track 7, chosen by the user 2026-11-19; issue 278). One handle on the Root element
// (design point 6 (a)); the slots are found by the ids C# registered, in index order. Port of
// `otp-field/input/OTPFieldInput.tsx` (the per-slot handlers) and the DOM half of `root/OTPFieldRoot.tsx` (`focusInput`,
// `handleInputFocus`, `requestSubmit`). C# owns normalization and distribution: the module reports each edit once and
// writes the slot values C# answers.
import { decide } from './decide';
import { contains, ownerWindow, platform, selectAll, trackComposition, writeTextValue } from './dom';
import type { CompositionTracker } from './dom';
import { eventInfo } from './event-info';
import type { EventInfo } from './event-info';
import { createTwinChannel, parseOptions, resolveElement } from './twin';
import type { DotNetTwin, TwinCaptures, TwinHandle, TwinOptions, TwinReferences } from './twin';

/** The decision point asked before an edit runs (fallback `run`); C#'s chain runs the request. */
export const EDIT_POINT = 'edit';
/** The decision point asked before a navigation key moves focus (fallback `run`). */
export const KEY_POINT = 'key';
/** The decision point asked before a slot's `mousedown` focuses it (fallback `run`). */
export const PRESS_POINT = 'press';
/** The decision point asked before a focus is reported (fallback `run`). */
export const FOCUS_POINT = 'focus';
/** The decision point asked before a blur is reported (fallback `run`). */
export const BLUR_POINT = 'blur';

/** A focus move C# asks for: focus and select a slot, once per revision. Mirrors `OtpFieldFocusRequest`. */
export interface OtpFieldFocusRequest {
  index: number;
  revision: number;
}

/** The options of the handle on a one-time code field's root. Mirrors `OtpFieldOptions` in C#. */
export interface OtpFieldOptions extends TwinOptions {
  /** The slots' ids, in index order. */
  slots: string[];
  /** The number of slots. */
  length: number;
  /** The committed, normalized value; slot i shows its code unit i. */
  value: string;
  /** The revision of the last edit report C# applied. */
  revision: number;
  /** An update that rolls back an optimistic act, applied even when older than the module's last act. */
  rollback?: boolean;
  /** The slot in the tab order. */
  activeIndex: number;
  /** A focus move to perform once. */
  focus?: OtpFieldFocusRequest;
  disabled?: boolean;
  readOnly?: boolean;
  rtl?: boolean;
  /** Whether a typed character the slot pattern accepts is written and advanced at once (variant (ii)). */
  optimistic?: boolean;
  /** The validation type's single-character pattern. */
  slotPattern?: string;
  /** Each increase submits the owning form once. */
  submitRevision: number;
  /** The id of the form that owns the field. */
  form?: string;
  /** The decision points C# answers besides `edit`. */
  points?: string[];
  /** Whether the development warnings need the module's reports. */
  developmentChecks?: boolean;
}

/** The kinds of edit the module reports. */
export type OtpFieldEditKind = 'input' | 'paste' | 'backspace' | 'delete' | 'clear';

/** The payload of `OnEdit` and `OnEditHandover`. Mirrors `OtpFieldEditReport`. */
export interface OtpFieldEditReport {
  index: number;
  kind: OtpFieldEditKind;
  raw: string;
  advanced: boolean;
  revision: number;
  event: EventInfo;
}

/** The payload of `OnFocus`. */
export interface OtpFieldFocusReport {
  index: number;
  event: EventInfo;
}

/** The payload of `OnBlur`. */
export interface OtpFieldBlurReport {
  index: number;
  outside: boolean;
  event: EventInfo;
}

/** The payload of `OnPasteUnreadable`. */
export interface OtpFieldPasteUnreadableReport {
  index: number;
}

/** A platform test seam for the Android composition path (OTP-KEY-22, OTP-KEY-23). */
export interface OtpFieldEnvironment {
  android: boolean;
}

let environment: OtpFieldEnvironment = { android: platform.os.android };

/**
 * Replaces the platform flags the module reads; test-only.
 * @param next The flags, or `undefined` to restore the detected ones.
 */
export function setEnvironmentForTesting(next: OtpFieldEnvironment | undefined): void {
  environment = next ?? { android: platform.os.android };
}

/**
 * Attaches the module to a one-time code field's root.
 * @param elementOrId The root element, or its id.
 * @param optionsJson The JSON of the options.
 * @param twinReference The root's twin.
 * @param _references Unused.
 * @param captures The capture id of a root rendered by a substitute.
 * @returns The handle.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  _references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const root = resolveElement(elementOrId, captures?.[0]);
  const twin = createTwinChannel(twinReference);
  const listenRoot = root.getRootNode() as Document | ShadowRoot;
  let options = parseOptions<OtpFieldOptions>(optionsJson);
  let disposed = false;

  // What the module shows: the committed value C# sent last, or the value an optimistic act wrote ahead of it.
  let value = options.value;
  // The revision rule (ADR 04 Amendments 2026-11-18, decision 6): every edit report carries the next revision, and an
  // update older than the last act is ignored unless it rolls back (design point 4 (a)).
  let revision = options.revision;
  let lastAct = options.revision;
  let appliedFocus = options.focus?.revision ?? 0;
  let appliedSubmit = options.submitRevision;
  let focusedIndex = -1;
  let reportedFocus = false;
  let slotPattern = compilePattern(options.slotPattern);

  // OTP-KEY-21..23 (OTPFieldInput.tsx:73-76,189-199): one tracker per slot from `dom`; a slot composes only off Android.
  const trackers = new Map<HTMLInputElement, CompositionTracker>();

  const lookup = (id: string): HTMLInputElement | null => {
    const found = (listenRoot as Document).getElementById?.(id) ?? null;
    return found instanceof ownerWindow(root).HTMLInputElement ? found : null;
  };
  const slots = (): HTMLInputElement[] => options.slots.map(lookup).filter((slot): slot is HTMLInputElement => slot !== null);
  const indexOf = (target: EventTarget | null): number => {
    if (!(target instanceof ownerWindow(root).HTMLInputElement) || !contains(root, target)) {
      return -1;
    }
    return options.slots.indexOf(target.id);
  };
  const slotValue = (index: number) => value[index] ?? '';
  const lastIndex = () => Math.max(options.length - 1, 0);
  const asks = (point: string) => options.points?.includes(point) === true;
  const composing = (slot: HTMLInputElement) => !environment.android && trackers.get(slot)?.composing() === true;

  const syncTrackers = () => {
    const current = new Set(slots());
    for (const [slot, tracker] of trackers) {
      if (!current.has(slot)) {
        tracker.dispose();
        trackers.delete(slot);
      }
    }
    for (const slot of current) {
      if (!trackers.has(slot)) {
        trackers.set(slot, trackComposition(slot));
      }
    }
  };

  // OTP-FOC-01, OTP-FOC-02, design point 5 (a): the roving tab stop, written with every focus move after attach.
  const writeTabStops = () => {
    const all = slots();
    const active = focusedIndex >= 0 ? Math.min(focusedIndex, lastIndex()) : Math.min(value.length, options.length - 1);
    all.forEach((slot, index) => {
      slot.tabIndex = index === active ? 0 : -1;
    });
  };

  // OTP-DOM-08, OTP-KEY-17: slot i shows the value's code unit i; an ignored or cancelled edit is restored here, the empty
  // slot included. A composing slot keeps the IME's text.
  const writeSlots = () => {
    slots().forEach((slot, index) => {
      if (!composing(slot)) {
        writeTextValue(slot, slotValue(index));
      }
    });
  };

  // OTPFieldRoot.tsx:164-169 `focusInput`: clamped to the rendered slots (§16 item 11), then focused and selected.
  const focusInput = (index: number) => {
    const all = slots();
    const target = all[Math.min(Math.max(index, 0), Math.max(all.length - 1, 0))];
    if (target) {
      target.focus();
      selectAll(target);
    }
  };

  // OTPFieldRoot.tsx:176-199 (OTP-EVT-13, design point 7 (a)): the `Form` id when it names a form, else the hidden input's
  // owner, else slot 0's; `requestSubmit()` only where the form has it.
  const hiddenInput = (): HTMLInputElement | null => {
    const sibling = root.nextElementSibling;
    return sibling instanceof ownerWindow(root).HTMLInputElement ? sibling : null;
  };
  const owningForm = (): HTMLFormElement | null => {
    let form = hiddenInput()?.form ?? slots()[0]?.form ?? null;
    if (options.form) {
      const named = (listenRoot as Document).getElementById?.(options.form) ?? null;
      if (named?.tagName === 'FORM') {
        form = named as HTMLFormElement;
      }
    }
    return form;
  };
  const requestSubmit = () => {
    const form = owningForm();
    if (form && typeof form.requestSubmit === 'function') {
      form.requestSubmit();
    }
  };

  const apply = (next: OtpFieldOptions, initial: boolean) => {
    options = next;
    slotPattern = compilePattern(next.slotPattern);
    syncTrackers();
    if (next.revision >= lastAct || next.rollback === true) {
      value = next.value;
      lastAct = Math.max(lastAct, next.revision);
      writeSlots();
      writeTabStops();
      // OTP-FOC-10 (OTPFieldRoot.tsx:207-215): the queued focus, once per revision.
      if (next.focus && next.focus.revision > appliedFocus) {
        appliedFocus = next.focus.revision;
        focusInput(next.focus.index);
      }
    }
    if (!initial && next.submitRevision > appliedSubmit) {
      appliedSubmit = next.submitRevision;
      requestSubmit();
    }
  };

  // One message per edit (ADR 04 §3). On WebAssembly the edit is handed over, then `edit` runs C#'s chain inside the
  // dispatch, whose update may arrive before `decide` returns (design point 2 (a)); elsewhere the fallback runs and the
  // edit is reported. Returns whether the edit ran.
  const edit = (index: number, kind: OtpFieldEditKind, raw: string, event: Event, advanced: boolean): boolean => {
    const previousAct = lastAct;
    revision += 1;
    lastAct = revision;
    const report: OtpFieldEditReport = { index, kind, raw, advanced, revision, event: eventInfo(event) };
    if (twin.synchronous) {
      twinReference.invokeMethod<void>('OnEditHandover', JSON.stringify(report));
      if (decide(twin, EDIT_POINT, event, 'run') === 'run') {
        return true;
      }
      // A vetoed edit reaches no request, so C# never echoes its revision: the last act is the one before it.
      lastAct = previousAct;
      return false;
    }
    decide(twin, EDIT_POINT, event, 'run');
    twin.report('OnEdit', report);
    return true;
  };

  // OTPFieldInput.tsx:101-133 `commitValue`, minus normalization: C# normalizes, distributes and answers by update.
  const commitText = (slot: HTMLInputElement, index: number, raw: string, kind: 'input' | 'paste', event: Event) => {
    // Variant (ii) (decision 6 (a); design point 3 (a)): a single typed character the slot pattern accepts is written and
    // focus advances before C# answers. On WebAssembly C# answers inside the dispatch, so nothing runs ahead of it.
    const optimistic =
      kind === 'input' && options.optimistic === true && !twin.synchronous && raw.length === 1 && slotPattern?.test(raw) === true;
    if (optimistic) {
      value = value.slice(0, index) + raw + value.slice(index + 1);
    }
    const ran = edit(index, kind, raw, event, optimistic);
    if (!ran) {
      // A consumer vetoed the edit: the slot shows its committed character again (OTP-KEY-13, OTP-KEY-17).
      writeTextValue(slot, slotValue(index));
      return;
    }
    if (optimistic) {
      focusInput(Math.min(index + 1, lastIndex()));
    }
  };

  const onInput = (event: Event) => {
    const index = indexOf(event.target);
    if (index < 0 || disposed) {
      return;
    }
    const slot = event.target as HTMLInputElement;
    if (event.defaultPrevented || options.disabled || options.readOnly) {
      writeTextValue(slot, slotValue(index));
      return;
    }
    // OTPFieldInput.tsx:212-215: the IME's text stays buffered in the slot until `compositionend`.
    if (composing(slot)) {
      return;
    }
    commitText(slot, index, slot.value, 'input', event);
  };

  // A capture listener runs before the slot's own tracker sees `compositionend`, so `composing` still says it composed.
  const onCompositionEnd = (event: Event) => {
    const index = indexOf(event.target);
    if (index < 0 || disposed) {
      return;
    }
    const slot = event.target as HTMLInputElement;
    if (!composing(slot)) {
      return;
    }
    if (options.disabled || options.readOnly) {
      // OTPFieldInput.tsx:201-205: the buffer is dropped and the slot shows its committed character again.
      writeTextValue(slot, slotValue(index));
      return;
    }
    commitText(slot, index, slot.value, 'input', event);
  };

  const onPaste = (event: Event) => {
    const index = indexOf(event.target);
    if (index < 0 || disposed || event.defaultPrevented || options.disabled || options.readOnly) {
      return;
    }
    let raw = '';
    try {
      raw = (event as ClipboardEvent).clipboardData?.getData('text/plain') ?? '';
    } catch {
      // OTP-KEY-20, OTP-EDGE-05: the paste is abandoned; the warning is C#'s.
      if (options.developmentChecks) {
        twin.report('OnPasteUnreadable', { index } satisfies OtpFieldPasteUnreadableReport);
      }
      return;
    }
    if (edit(index, 'paste', raw, event, false)) {
      event.preventDefault();
    }
  };

  // OTPFieldInput.tsx:219-304.
  const onKeyDown = (event: KeyboardEvent) => {
    const index = indexOf(event.target);
    if (index < 0 || disposed) {
      return;
    }
    const slot = event.target as HTMLInputElement;
    if (event.defaultPrevented || options.disabled || composing(slot)) {
      return;
    }
    const first = 0;
    const last = lastIndex();
    const endTarget = Math.min(value.length, last);
    const boundary = (event.ctrlKey || event.metaKey) && !event.altKey;
    const previousKey = options.rtl ? 'ArrowRight' : 'ArrowLeft';
    const nextKey = options.rtl ? 'ArrowLeft' : 'ArrowRight';

    const navigate = (target: number) => {
      if (asks(KEY_POINT) && decide(twin, KEY_POINT, event, 'run') !== 'run') {
        return;
      }
      stopEvent(event);
      focusInput(target);
    };

    if (event.key === previousKey) {
      navigate(boundary ? first : Math.max(first, index - 1));
      return;
    }
    if (event.key === nextKey) {
      navigate(boundary ? endTarget : Math.min(last, index + 1));
      return;
    }
    if (event.key === 'Home' || event.key === 'ArrowUp') {
      navigate(first);
      return;
    }
    if (event.key === 'End' || event.key === 'ArrowDown') {
      navigate(endTarget);
      return;
    }
    if (options.readOnly) {
      return;
    }

    const editKey = (kind: OtpFieldEditKind) => {
      if (edit(index, kind, '', event, false)) {
        stopEvent(event);
      }
    };

    if (event.key === 'Backspace' && boundary) {
      editKey('clear');
      return;
    }
    if (event.key === 'Delete') {
      editKey('delete');
      return;
    }
    // OTP-KEY-09, §16 item 5 (replicated, filed upstream): no modifier check.
    const fullSelection = slot.selectionStart === 0 && slot.selectionEnd === slot.value.length;
    if (event.key.length === 1 && fullSelection && slotValue(index) === event.key) {
      if (asks(KEY_POINT) && decide(twin, KEY_POINT, event, 'run') !== 'run') {
        return;
      }
      stopEvent(event);
      if (index < options.length - 1) {
        focusInput(index + 1);
      }
      return;
    }
    if (event.key === 'Backspace') {
      editKey('backspace');
    }
  };

  // OTP-PTR-01, OTP-PTR-02 (OTPFieldInput.tsx:164-170).
  const onMouseDown = (event: Event) => {
    const index = indexOf(event.target);
    if (index < 0 || disposed || event.defaultPrevented || options.disabled) {
      return;
    }
    if (asks(PRESS_POINT) && decide(twin, PRESS_POINT, event, 'run') !== 'run') {
      return;
    }
    event.preventDefault();
    focusInput(index);
  };

  // OTP-FOC-03..05, OTP-FOC-07 (OTPFieldInput.tsx:171-187; OTPFieldRoot.tsx:274-283): a slot past the value's end sends
  // focus to the end target; otherwise the slot is selected. The focus is reported once when it enters the root.
  const onFocusIn = (event: FocusEvent) => {
    const index = indexOf(event.target);
    if (index < 0 || disposed || event.defaultPrevented || options.disabled) {
      return;
    }
    if (asks(FOCUS_POINT) && decide(twin, FOCUS_POINT, event, 'run') !== 'run') {
      return;
    }
    if (!reportedFocus) {
      reportedFocus = true;
      twin.report('OnFocus', { index, event: eventInfo(event) } satisfies OtpFieldFocusReport);
    }
    if (index > value.length) {
      focusInput(Math.min(value.length, options.length - 1));
      return;
    }
    focusedIndex = index;
    writeTabStops();
    selectAll(event.target as HTMLInputElement);
  };

  // OTP-FOC-07, OTP-FRM-09, OTP-FRM-10 (OTPFieldRoot.tsx:286-300): reported once when focus leaves the root.
  const onFocusOut = (event: FocusEvent) => {
    const index = indexOf(event.target);
    if (index < 0 || disposed || event.defaultPrevented) {
      return;
    }
    if (asks(BLUR_POINT) && decide(twin, BLUR_POINT, event, 'run') !== 'run') {
      return;
    }
    const related = event.relatedTarget;
    if (related instanceof ownerWindow(root).Element && contains(root, related)) {
      return;
    }
    focusedIndex = -1;
    writeTabStops();
    if (reportedFocus) {
      reportedFocus = false;
      twin.report('OnBlur', { index, outside: true, event: eventInfo(event) } satisfies OtpFieldBlurReport);
    }
  };

  // OTP-FRM-14, §16 item 9: a native reset puts the slots and the hidden input back to their attributes; the module
  // re-writes them from the value one task later, after the reset algorithm ran (the `dom` module's precedent).
  const onReset = (event: Event) => {
    if (disposed || event.target !== owningForm()) {
      return;
    }
    ownerWindow(root).setTimeout(() => {
      if (disposed) {
        return;
      }
      writeSlots();
      const hidden = hiddenInput();
      if (hidden) {
        writeTextValue(hidden, value);
      }
    }, 0);
  };

  // Delegated from the root node so Blazor's own dispatch of a consumer's handler is never cut off by `stopEvent`.
  listenRoot.addEventListener('keydown', onKeyDown as EventListener);
  listenRoot.addEventListener('input', onInput);
  listenRoot.addEventListener('paste', onPaste);
  listenRoot.addEventListener('mousedown', onMouseDown);
  listenRoot.addEventListener('focusin', onFocusIn as EventListener);
  listenRoot.addEventListener('focusout', onFocusOut as EventListener);
  listenRoot.addEventListener('compositionend', onCompositionEnd, true);
  listenRoot.addEventListener('reset', onReset);

  apply(options, true);

  return {
    update(nextJson: string): void {
      if (!disposed) {
        apply(parseOptions<OtpFieldOptions>(nextJson), false);
      }
    },
    dispose(): void {
      disposed = true;
      listenRoot.removeEventListener('keydown', onKeyDown as EventListener);
      listenRoot.removeEventListener('input', onInput);
      listenRoot.removeEventListener('paste', onPaste);
      listenRoot.removeEventListener('mousedown', onMouseDown);
      listenRoot.removeEventListener('focusin', onFocusIn as EventListener);
      listenRoot.removeEventListener('focusout', onFocusOut as EventListener);
      listenRoot.removeEventListener('compositionend', onCompositionEnd, true);
      listenRoot.removeEventListener('reset', onReset);
      for (const tracker of trackers.values()) {
        tracker.dispose();
      }
      trackers.clear();
    },
  };
}

// The slot pattern is a single-character class (OTP-STA-05) such as `\d` or `[a-zA-Z]`, matched against one character.
function compilePattern(pattern: string | undefined): RegExp | null {
  return pattern ? new RegExp(`^(?:${pattern})$`) : null;
}

// `stopEvent` (floating-ui-react/utils/event.ts): the event's own preventDefault and stopPropagation.
function stopEvent(event: Event): void {
  event.preventDefault();
  event.stopPropagation();
}
