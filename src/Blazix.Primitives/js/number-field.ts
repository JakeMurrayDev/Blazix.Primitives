// The `number-field` component module (ADR 04 §1 and Amendments 2026-11-18, the `number-field` scope: the input role and the
// scrub role). 6a ships the input role's minimal surface (issue 277, design point 2 (b), settled by the orchestrator
// 2026-11-18): the visible input's `value` handover (C# renders the first text, the module owns it from attach and writes
// C#'s answers, design point 7 (a): every input report carries a revision that `update` echoes, and an update older than
// the last report is not applied, ADR 04 Amendments 2026-11-18's revision rule widened to typing), and the focus and blur
// reports with Base UI's `defaultPrevented` checks (number-field/input/NumberFieldInput.tsx:131-150). A C# consumer's veto
// is the `input-focus` / `input-blur` decision point, fallback run (design point 1 (a), names chosen by the user
// 2026-11-18). 6b adds the input role's keydown gate, change, paste and wheel (NumberFieldInput.tsx:225-418, the wheel of
// NumberFieldRoot.tsx) over a C#-computed allowed set and locale symbols (decision 7 (a); C# re-validates and parses every
// text), the hidden input's focus redirect and autofill (design point 6), the `input-key`, `input-paste` and `input-change`
// decision points (fallback run), and then the scrub role.
// Requirements: docs/components/number-field.md (NUM-STA-05/06, NUM-EVT-06..08, NUM-EVT-13..16, NUM-FOC-01/02/05,
// NUM-KEY-01..12, NUM-PTR-07..09, NUM-FRM-05/07, NUM-EDGE-03/06).
import { decide } from './decide';
import { activeElement, ownerDocument, setCaret, writeTextValue } from './dom';
import { eventInfo, type EventInfo } from './event-info';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinCaptures,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

/** The options C# sends with `attach` and `update`. */
export interface NumberFieldOptions extends TwinOptions {
  /** The text the visible input shows, C#'s answer. */
  value: string;
  /** The revision of the last input report C# applied, which the module compares with its own. */
  revision: number;
  /** The input role's configuration; an update without it keeps the last one, so a step's text update stays small. */
  input?: NumberFieldInputOptions | null;
}

/** The input role's configuration, which C# sends at attach and again only when it changes. */
export interface NumberFieldInputOptions {
  /** The allowed non-numeric characters, C#'s NUM-KEY-07 set for the locale and format. */
  allowed?: string | null;
  /** The locale's decimal separator, currency symbol and percent sign, each typed at most once (NUM-KEY-09). */
  decimal?: string | null;
  currency?: string | null;
  percent?: string | null;
  /** Whether `min` is defined, so Home steps to it (NUM-KEY-02). */
  min?: boolean | null;
  /** Whether `max` is defined, so End steps to it (NUM-KEY-02). */
  max?: boolean | null;
  /** Whether the wheel steps the focused input: `allowWheelScrub` on an enabled, editable root (NUM-PTR-07, NUM-PTR-09). */
  wheel?: boolean | null;
  /** Whether the root is disabled or read-only, when the input role does nothing (NUM-KEY-11, NUM-EVT-16, NUM-FRM-05). */
  disabled?: boolean | null;
  readOnly?: boolean | null;
  /** Whether development warnings are on (NUM-EDGE-03). */
  developmentChecks?: boolean | null;
}

/** The payload of `OnKeyStep`: a step key, the text it steps from when dirty, and this report's revision. */
export interface NumberFieldKeyStepReport {
  key: 'ArrowUp' | 'ArrowDown' | 'Home' | 'End';
  text: string;
  revision: number;
  event: EventInfo;
}

/** The payload of `OnInputChange`: the text the input shows after an allowed edit, and this report's revision. */
export interface NumberFieldChangeReport {
  text: string;
  revision: number;
  event: EventInfo;
}

/** The payload of `OnInputPaste`: the display text with the paste spliced in, and this report's revision. */
export interface NumberFieldPasteReport {
  text: string;
  revision: number;
  event: EventInfo;
}

/** The payload of `OnWheelStep`: `1` for up, `-1` for down. */
export interface NumberFieldWheelReport {
  direction: number;
  event: EventInfo;
}

/** The payload of `OnAutofill`: the hidden input's `valueAsNumber`, or `null` when it is not a number. */
export interface NumberFieldAutofillReport {
  value: number | null;
  event: EventInfo;
}

/** The payload of `OnInputFocus`. */
export interface NumberFieldFocusReport {
  event: EventInfo;
}

/** The payload of `OnInputBlur`: the text the input showed and the revision of this report. */
export interface NumberFieldBlurReport {
  text: string;
  revision: number;
  event: EventInfo;
}

/** The decision point a focus of the visible input asks C# about. */
export const INPUT_FOCUS_POINT = 'input-focus';

/** The decision point a blur of the visible input asks C# about. */
export const INPUT_BLUR_POINT = 'input-blur';

/** The decision point a keydown asks C# about before it steps or blocks a character. */
export const INPUT_KEY_POINT = 'input-key';

/** The decision point a paste asks C# about before it prevents the browser's paste. */
export const INPUT_PASTE_POINT = 'input-paste';

/** The decision point an edit asks C# about before it is reported or reverted. */
export const INPUT_CHANGE_POINT = 'input-change';

// parse.ts:5-11 and 61-64 (NUM-KEY-06, NUM-KEY-08): the numeral systems and the sign variants, constant here (decision 7).
const NUMERAL_RE = /[0-9٠-٩۰-۹０-９零〇一二三四五六七八九]/;
const ANY_MINUS_RE = /[-−－‒–—﹣]/;
const ANY_PLUS_RE = /[+＋﹢]/;
const FORMAT_CONTROL_RE = /\p{Cf}/u;

// NumberFieldInput.tsx:35-43 (NUM-KEY-03).
const NAVIGATE_KEYS = new Set(['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'Tab', 'Enter', 'Escape']);

// NUM-EDGE-03, UTL-EDGE-11/12: one warning per message for the page.
const warned = new Set<string>();

function warnOnce(message: string): void {
  if (warned.has(message)) {
    return;
  }
  warned.add(message);
  console.warn(message);
}

/**
 * Attaches the input role to the visible input of a number field.
 * @param elementOrId The input, or its id when the part has no element reference.
 * @param optionsJson The `NumberFieldOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param references The hidden `<input type="number">` at index 0, whose focus moves to the visible input and whose
 * `change` is an autofill.
 * @param captures The capture ids C# sends beside the element, which find it inside a shadow root.
 * @returns The handle with `update(optionsJson)` and `dispose()`.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const input = resolveElement(elementOrId, captures?.[0]) as HTMLInputElement;
  const twin = createTwinChannel(twinReference);
  let options = parseOptions<NumberFieldOptions>(optionsJson);
  let gate: NumberFieldInputOptions = options.input ?? {};
  let revision = 0;
  let disposed = false;
  let text = input.value;
  let pendingCaret: { revision: number; text: string; caret: number } | null = null;
  const hidden = references?.[0] instanceof HTMLInputElement ? references[0] : null;

  // The handover at attach (ADR 04 Amendments 2026-10-18): C# rendered the first text; from now on the module writes it.
  // An answer older than the module's last report is not applied (design point 7 (a)).
  const write = (next: NumberFieldOptions) => {
    if (next.revision < revision) {
      return;
    }
    writeTextValue(input, next.value);
    text = next.value;
    // NUM-EVT-16: the caret goes just after the pasted text once C# accepted it.
    if (pendingCaret && next.revision >= pendingCaret.revision) {
      if (next.value === pendingCaret.text) {
        setCaret(input, pendingCaret.caret);
      }
      pendingCaret = null;
    }
  };
  write(options);

  const inactive = () => gate.disabled === true || gate.readOnly === true;
  const allowedSet = () => gate.allowed ?? '';

  // NumberFieldInput.tsx:131-136: a focus whose default a consumer prevented is not the field's.
  const onFocus = (event: FocusEvent) => {
    if (event.defaultPrevented || decide(twin, INPUT_FOCUS_POINT, event, 'run') !== 'run') {
      return;
    }
    twin.report('OnInputFocus', { event: eventInfo(event) } satisfies NumberFieldFocusReport);
  };

  // NumberFieldInput.tsx:137-150: the blur carries the text the input shows, which C# parses and answers.
  const onBlur = (event: FocusEvent) => {
    if (event.defaultPrevented || decide(twin, INPUT_BLUR_POINT, event, 'run') !== 'run') {
      return;
    }
    revision += 1;
    twin.report('OnInputBlur', { text: input.value, revision, event: eventInfo(event) } satisfies NumberFieldBlurReport);
  };

  // NumberFieldInput.tsx:280-405 (NUM-KEY-01..11): the gate decides in the dispatch; C# steps.
  const onKeyDown = (event: KeyboardEvent) => {
    if (event.defaultPrevented || inactive()) {
      return;
    }

    const current = input.value;
    const allowed = allowedSet();
    let isAllowed = event.key.length === 1 && allowed.includes(event.key);
    const start = input.selectionStart;
    const end = input.selectionEnd;
    const allSelected = start === 0 && end === current.length;
    const covers = (index: number) => start != null && end != null && index >= start && index < end;

    // NUM-KEY-08: one sign of either kind.
    for (const sign of [ANY_MINUS_RE, ANY_PLUS_RE]) {
      if (sign.test(event.key) && Array.from(allowed).some((ch) => sign.test(ch))) {
        const existing = current.search(new RegExp(sign.source, 'u'));
        isAllowed = !(ANY_MINUS_RE.test(current) || ANY_PLUS_RE.test(current)) || allSelected || (existing !== -1 && covers(existing));
      }
    }

    // NUM-KEY-09: one of each symbol; group separators are not limited.
    for (const symbol of [gate.decimal, gate.currency, gate.percent]) {
      if (symbol && event.key === symbol) {
        const index = current.indexOf(symbol);
        isAllowed = index === -1 || allSelected || covers(index);
      }
    }

    const isStepKey = event.key === 'ArrowUp' || event.key === 'ArrowDown';
    if (
      (event as KeyboardEvent & { which?: number }).which === 229 ||
      (event.altKey && !isStepKey) ||
      event.ctrlKey ||
      event.metaKey ||
      isAllowed ||
      NUMERAL_RE.test(event.key) ||
      NAVIGATE_KEYS.has(event.key)
    ) {
      return;
    }

    const isBound = (event.key === 'Home' && gate.min === true) || (event.key === 'End' && gate.max === true);
    if (event.key.length > 1 && !isStepKey && !isBound) {
      return;
    }

    if (decide(twin, INPUT_KEY_POINT, event, 'run') !== 'run') {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (isStepKey || isBound) {
      revision += 1;
      twin.report('OnKeyStep', {
        key: event.key as NumberFieldKeyStepReport['key'],
        text: current,
        revision,
        event: eventInfo(event),
      } satisfies NumberFieldKeyStepReport);
    }
  };

  // NumberFieldInput.tsx:225-262 (NUM-EVT-15, NUM-EDGE-06): an allowed edit is reported; any other is reverted.
  const onInput = (event: Event) => {
    if (event.defaultPrevented) {
      return;
    }
    const next = input.value;
    const allowed = allowedSet();
    const valid =
      next.trim() === '' ||
      Array.from(next).every(
        (ch) => NUMERAL_RE.test(ch) || ANY_MINUS_RE.test(ch) || allowed.includes(ch) || FORMAT_CONTROL_RE.test(ch),
      );
    if (!valid) {
      writeTextValue(input, text);
      return;
    }
    if (decide(twin, INPUT_CHANGE_POINT, event, 'run') !== 'run') {
      return;
    }
    text = next;
    revision += 1;
    twin.report('OnInputChange', { text: next, revision, event: eventInfo(event) } satisfies NumberFieldChangeReport);
  };

  // NumberFieldInput.tsx:406-445 (NUM-EVT-16, NUM-EDGE-03): the paste is spliced at the selection; C# parses it.
  const onPaste = (event: ClipboardEvent) => {
    if (event.defaultPrevented || inactive()) {
      return;
    }
    let pasted: string;
    try {
      pasted = event.clipboardData?.getData('text/plain') ?? '';
    } catch {
      if (gate.developmentChecks === true) {
        warnOnce('Blazix: <NumberFieldInput> could not read clipboard text during paste handling. ');
      }
      return;
    }
    if (decide(twin, INPUT_PASTE_POINT, event, 'run') !== 'run') {
      return;
    }
    event.preventDefault();
    const current = input.value;
    const start = input.selectionStart ?? current.length;
    const end = input.selectionEnd ?? current.length;
    const next = current.slice(0, start) + pasted + current.slice(end);
    revision += 1;
    pendingCaret = { revision, text: next, caret: start + pasted.length };
    twin.report('OnInputPaste', { text: next, revision, event: eventInfo(event) } satisfies NumberFieldPasteReport);
  };

  // NumberFieldRoot.tsx's wheel (NUM-PTR-07..09): a non-passive listener while the options allow it.
  const onWheel = (event: WheelEvent) => {
    if (event.ctrlKey || activeElement(ownerDocument(input)) !== input) {
      return;
    }
    const horizontal = Math.abs(event.deltaX) > Math.abs(event.deltaY);
    if (horizontal && !event.shiftKey) {
      return;
    }
    const delta = event.shiftKey && horizontal ? event.deltaX : event.deltaY;
    if (delta === 0) {
      return;
    }
    event.preventDefault();
    twin.report('OnWheelStep', { direction: delta > 0 ? -1 : 1, event: eventInfo(event) } satisfies NumberFieldWheelReport);
  };
  let wheelAttached = false;
  const syncWheel = () => {
    const want = gate.wheel === true && !inactive();
    if (want && !wheelAttached) {
      input.addEventListener('wheel', onWheel, { passive: false });
    } else if (!want && wheelAttached) {
      input.removeEventListener('wheel', onWheel);
    }
    wheelAttached = want;
  };
  syncWheel();

  // NUM-FOC-01/02 (design point 6): the hidden input never keeps the focus; the caret goes to the end first.
  const onHiddenFocus = () => {
    setCaret(input, input.value.length);
    input.focus();
  };

  // NUM-FRM-05 (design point 6): a browser autofill of the hidden input; C# checks disabled and read-only.
  const onHiddenChange = (event: Event) => {
    if (event.defaultPrevented || hidden === null) {
      return;
    }
    const value = hidden.valueAsNumber;
    twin.report('OnAutofill', { value: Number.isNaN(value) ? null : value, event: eventInfo(event) } satisfies NumberFieldAutofillReport);
  };

  input.addEventListener('focus', onFocus);
  input.addEventListener('blur', onBlur);
  input.addEventListener('keydown', onKeyDown);
  input.addEventListener('input', onInput);
  input.addEventListener('paste', onPaste);
  hidden?.addEventListener('focus', onHiddenFocus);
  hidden?.addEventListener('change', onHiddenChange);

  return {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<NumberFieldOptions>(nextOptionsJson);
      gate = options.input ?? gate;
      write(options);
      syncWheel();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      input.removeEventListener('focus', onFocus);
      input.removeEventListener('blur', onBlur);
      input.removeEventListener('keydown', onKeyDown);
      input.removeEventListener('input', onInput);
      input.removeEventListener('paste', onPaste);
      input.removeEventListener('wheel', onWheel);
      hidden?.removeEventListener('focus', onHiddenFocus);
      hidden?.removeEventListener('change', onHiddenChange);
    },
  };
}
