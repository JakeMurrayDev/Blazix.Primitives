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
// The scrub role (6b): one handle per ScrubArea (NUM-STR-05) and one per ScrubAreaCursor, told apart by the options' `part`
// (the ScrollArea precedent); the input role's options have none. The scrub area's pointerdown, pointer lock on the owning
// body, the window move accumulator, the release with its synthetic click and Gecko's 20 ms delay, and the touch scroll
// suppression port number-field/scrub-area/NumberFieldScrubArea.tsx:127-356; C# steps and commits from the reports. The
// cursor's `transform` is written in the style writer's `'component'` band (ADR 04 Amendments 2026-11-18, decision 5 as
// revised: Base UI writes it imperatively, NumberFieldScrubArea.tsx:76-81); its base declarations are C#'s, merged under
// the consumer's style and sent as the consumer string. A scrub step takes the input handle's next revision, so a text
// answer older than the step is not applied (decision 6 (a)). The consumer's `onpointerdown` veto is the `scrub-start`
// point, fallback start (design point 1 (a)).
// Requirements: docs/components/number-field.md (NUM-STA-05/06, NUM-EVT-06..08, NUM-EVT-13..16, NUM-FOC-01/02/04/05,
// NUM-KEY-01..12, NUM-PTR-07..19, NUM-FRM-05/07, NUM-EDGE-03/06/09/10/15, NUM-STR-05/06, NUM-DOM-13/14).
import { decide } from './decide';
import { activeElement, ownerDocument, ownerWindow, setCaret, styleWriter, writeTextValue } from './dom';
import { eventInfo, type EventInfo } from './event-info';
import { platform } from './platform';
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

/** The options of a ScrubArea's or a ScrubAreaCursor's handle. */
export interface NumberFieldScrubOptions extends TwinOptions {
  /** Which part the handle belongs to. */
  part: 'scrub' | 'cursor';
  /** Whether the scrub area steps on vertical movement (upward increases); horizontal when absent. */
  vertical?: boolean | null;
  /** The pixels the pointer must move before the value steps; `2` when absent. */
  pixelSensitivity?: number | null;
  /** The distance from the scrub area's edges at which the virtual cursor wraps; the visual viewport when absent. */
  teleportDistance?: number | null;
  /** Whether the root is disabled or read-only, when the scrub area does nothing. */
  disabled?: boolean | null;
  readOnly?: boolean | null;
}

/** The payload of `OnScrubStart`: whether the pointer can have a virtual cursor (not touch, not WebKit). */
export interface NumberFieldScrubStartReport {
  cursor: boolean;
  event: EventInfo;
}

/** The payload of `OnScrubLock`: the pointer-lock request's outcome, reported when it differs from the last one. */
export interface NumberFieldScrubLockReport {
  denied: boolean;
}

/** The payload of `OnScrubStep`: the move's delta on the scrub axis (upward positive when vertical), and a revision. */
export interface NumberFieldScrubStepReport {
  delta: number;
  revision: number;
  event: EventInfo;
}

/** The payload of `OnScrubEnd`: the release. */
export interface NumberFieldScrubEndReport {
  event: EventInfo;
}

/** The bounds the virtual cursor wraps within, as edge coordinates. */
export interface ViewportRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

/** The decision point a scrub area's pointerdown asks C# about before a scrub starts. */
export const SCRUB_START_POINT = 'scrub-start';

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

// The input handle's revision counter, on the input element, so a scrub step takes the next revision of the same line.
const inputRevision = Symbol('blazix.numberField.inputRevision');
type RevisionedInput = HTMLInputElement & { [inputRevision]?: () => number };

/**
 * Attaches the input role to the visible input of a number field, or the scrub role to a ScrubArea or ScrubAreaCursor
 * when the options name its `part`.
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
  const parsed = parseOptions<NumberFieldOptions | NumberFieldScrubOptions>(optionsJson);
  if ('part' in parsed && parsed.part === 'scrub') {
    return attachScrubArea(resolveElement(elementOrId, captures?.[0]) as HTMLElement, parsed, twinReference, references);
  }
  if ('part' in parsed && parsed.part === 'cursor') {
    return attachCursor(resolveElement(elementOrId, captures?.[0]) as HTMLElement, parsed, references);
  }

  const input = resolveElement(elementOrId, captures?.[0]) as RevisionedInput;
  const twin = createTwinChannel(twinReference);
  let options = parsed as NumberFieldOptions;
  let gate: NumberFieldInputOptions = options.input ?? {};
  let revision = 0;
  let disposed = false;
  let text = input.value;
  let pendingCaret: { revision: number; text: string; caret: number } | null = null;
  const hidden = references?.[0] instanceof HTMLInputElement ? references[0] : null;
  const nextRevision = () => ++revision;
  input[inputRevision] = nextRevision;

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
    // NumberFieldInput.tsx:233-236: a prevented change is ignored, and Base UI's controlled input keeps the text it rendered.
    if (event.defaultPrevented) {
      writeTextValue(input, text);
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
  // A capture listener runs before the `form` module's own redirect on the same input (FFI-FOC-05), which would focus the
  // visible input without the caret; that one then finds the focus already moved (NumberFieldRoot.tsx:347-359, 501-504).
  hidden?.addEventListener('focus', onHiddenFocus, true);
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
      if (input[inputRevision] === nextRevision) {
        delete input[inputRevision];
      }
      input.removeEventListener('focus', onFocus);
      input.removeEventListener('blur', onBlur);
      input.removeEventListener('keydown', onKeyDown);
      input.removeEventListener('input', onInput);
      input.removeEventListener('paste', onPaste);
      input.removeEventListener('wheel', onWheel);
      hidden?.removeEventListener('focus', onHiddenFocus, true);
      hidden?.removeEventListener('change', onHiddenChange);
    },
  };
}

// number-field/utils/getViewportRect.ts (NUM-PTR-16, §16 item 13): the scrub area padded by half the teleport distance on
// every side, else the visual viewport, else the document element's client box.
/**
 * Calculates the bounds the virtual cursor wraps within.
 * @param teleportDistance The scrub area's teleport distance, or `undefined` for the viewport.
 * @param scrubArea The scrub area.
 * @returns The bounds as edge coordinates.
 */
export function getViewportRect(teleportDistance: number | undefined, scrubArea: HTMLElement): ViewportRect {
  const win = ownerWindow(scrubArea);

  if (teleportDistance != null) {
    const rect = scrubArea.getBoundingClientRect();
    return {
      left: rect.left - teleportDistance / 2,
      top: rect.top - teleportDistance / 2,
      right: rect.right + teleportDistance / 2,
      bottom: rect.bottom + teleportDistance / 2,
    };
  }

  const viewport = win.visualViewport;
  if (viewport) {
    return {
      left: viewport.offsetLeft,
      top: viewport.offsetTop,
      right: viewport.offsetLeft + viewport.width,
      bottom: viewport.offsetTop + viewport.height,
    };
  }

  return {
    left: 0,
    top: 0,
    right: win.document.documentElement.clientWidth,
    bottom: win.document.documentElement.clientHeight,
  };
}

/** What a scrub area shares with its cursor's handle, kept on the scrub area element. */
interface ScrubState {
  setCursor(cursor: HTMLElement | null): void;
}

const scrubState = Symbol('blazix.numberField.scrub');
const pendingCursor = Symbol('blazix.numberField.pendingCursor');
type ScrubElement = HTMLElement & { [scrubState]?: ScrubState; [pendingCursor]?: HTMLElement | null };

// NumberFieldScrubArea.tsx:76-81 (NUM-DOM-13): the cursor keeps the OS cursor's size under pinch-zoom.
function writeCursor(cursor: HTMLElement, x: number, y: number): void {
  const scale = ownerWindow(cursor).visualViewport?.scale ?? 1;
  styleWriter(cursor).layer('component').set('transform', `translate3d(${x}px,${y}px,0) scale(${1 / scale})`);
}

function attachScrubArea(area: ScrubElement, initial: NumberFieldScrubOptions, twinReference: DotNetTwin, references?: TwinReferences): TwinHandle {
  const twin = createTwinChannel(twinReference);
  const input = references?.[0] instanceof HTMLInputElement ? (references[0] as RevisionedInput) : null;
  let options = initial;
  let disposed = false;
  // NumberFieldScrubArea.tsx:59-64: the scrub's refs.
  let pointerDown = false;
  let didMove = false;
  let pointerDownTarget: EventTarget | null = null;
  let nativeClickReceived = false;
  let lockDenied = false;
  let cumulativeDelta = 0;
  let coords = { x: 0, y: 0 };
  let press = { clientX: 0, clientY: 0 };
  let cursor: HTMLElement | null = null;
  let listening = false;
  let releaseTimer: ReturnType<typeof setTimeout> | undefined;
  let clickTimer: ReturnType<typeof setTimeout> | undefined;

  const inactive = () => options.disabled === true || options.readOnly === true;
  const win = () => ownerWindow(input ?? area);
  const exitLock = () => {
    try {
      ownerDocument(area).exitPointerLock();
    } catch {
      // Ignore errors.
    }
  };

  // NumberFieldScrubArea.tsx:126-145: the cursor starts centred on the press.
  const placeCursor = () => {
    if (cursor === null) {
      return;
    }
    coords = { x: press.clientX - cursor.offsetWidth / 2, y: press.clientY - cursor.offsetHeight / 2 };
    writeCursor(cursor, coords.x, coords.y);
  };

  // NumberFieldScrubArea.tsx:83-123 (NUM-PTR-16): the virtual cursor follows the movement and wraps at the bounds.
  const moveCursor = (event: PointerEvent) => {
    if (cursor === null) {
      return;
    }
    const rect = getViewportRect(options.teleportDistance ?? undefined, area);
    const wrap = (coord: number, halfSize: number, low: number, high: number) => {
      if (coord + halfSize < low) {
        return high - halfSize;
      }
      if (coord + halfSize > high) {
        return low - halfSize;
      }
      return coord;
    };
    coords = {
      x: wrap(Math.round(coords.x + event.movementX), cursor.offsetWidth / 2, rect.left, rect.right),
      y: wrap(Math.round(coords.y + event.movementY), cursor.offsetHeight / 2, rect.top, rect.bottom),
    };
    writeCursor(cursor, coords.x, coords.y);
  };

  const state: ScrubState = {
    setCursor(next) {
      cursor = next;
      if (next !== null && pointerDown) {
        placeCursor();
      }
    },
  };
  area[scrubState] = state;
  if (area[pendingCursor]) {
    state.setCursor(area[pendingCursor]!);
    area[pendingCursor] = null;
  }

  // NumberFieldScrubArea.tsx:226-260 (NUM-PTR-11, NUM-PTR-12).
  const onMove = (event: PointerEvent) => {
    // The flag, not the listeners' presence, says whether a pointer is down (NUM-EDGE-15).
    if (!pointerDown) {
      return;
    }
    // Prevent text selection.
    event.preventDefault();
    moveCursor(event);
    const vertical = options.vertical === true;
    cumulativeDelta += vertical ? event.movementY : event.movementX;
    if (Math.abs(cumulativeDelta) >= (options.pixelSensitivity ?? 2)) {
      cumulativeDelta = 0;
      didMove = true;
      const delta = vertical ? -event.movementY : event.movementX;
      if (delta !== 0) {
        const revision = input?.[inputRevision]?.() ?? 0;
        twin.report('OnScrubStep', { delta, revision, event: eventInfo(event) } satisfies NumberFieldScrubStepReport);
      }
    }
  };

  // NumberFieldScrubArea.tsx:165-224 (NUM-PTR-13, NUM-PTR-14).
  const onUp = (event: PointerEvent) => {
    const handler = () => {
      releaseTimer = undefined;
      exitLock();
      pointerDown = false;
      stopListening();
      twin.report('OnScrubEnd', { event: eventInfo(event) } satisfies NumberFieldScrubEndReport);

      // While the pointer is locked the browser targets its click at the lock element; without a lock the native click
      // arrives after pointerup in the same task, so wait one task and click only if it did not reach the scrub area.
      const target = pointerDownTarget;
      if (!didMove && target !== null && input !== null) {
        const MouseEventConstructor = ownerWindow(input).MouseEvent;
        clearTimeout(clickTimer);
        clickTimer = setTimeout(() => {
          clickTimer = undefined;
          if (nativeClickReceived) {
            return;
          }
          target.dispatchEvent(new MouseEventConstructor('click', { bubbles: true, cancelable: true }));
        }, 0);
      }
      didMove = false;
      pointerDownTarget = null;
    };

    if (releaseTimer !== undefined) {
      return;
    }
    if (platform.engine.gecko) {
      // Firefox needs a small delay here when soft-clicking, as the pointer lock will not release otherwise.
      releaseTimer = setTimeout(handler, 20);
    } else {
      handler();
    }
  };

  // NUM-PTR-18: the window listeners exist only while a scrub runs, in the capture phase.
  const startListening = () => {
    if (listening || inactive()) {
      return;
    }
    listening = true;
    cumulativeDelta = 0;
    win().addEventListener('pointerup', onUp, true);
    win().addEventListener('pointermove', onMove, true);
  };
  const stopListening = () => {
    if (!listening) {
      return;
    }
    listening = false;
    clearTimeout(releaseTimer);
    releaseTimer = undefined;
    win().removeEventListener('pointerup', onUp, true);
    win().removeEventListener('pointermove', onMove, true);
  };

  const onClickCapture = () => {
    nativeClickReceived = true;
  };

  // NumberFieldScrubArea.tsx:304-349 (NUM-PTR-10, NUM-PTR-19, NUM-FOC-04).
  const onPointerDown = (event: PointerEvent) => {
    if (event.defaultPrevented || inactive() || event.button) {
      return;
    }
    const verdict = decide(twin, SCRUB_START_POINT, event, 'start');
    if (verdict === 'stop' || verdict === 'prevent-and-stop') {
      return;
    }

    const isTouch = event.pointerType === 'touch';
    if (event.pointerType === 'mouse') {
      event.preventDefault();
      // NumberFieldRoot.tsx:351-359 (focusInput).
      if (input !== null) {
        setCaret(input, input.value.length);
        input.focus();
      }
    }

    pointerDown = true;
    didMove = false;
    nativeClickReceived = false;
    clearTimeout(clickTimer);
    clickTimer = undefined;
    pointerDownTarget = event.target;
    press = { clientX: event.clientX, clientY: event.clientY };
    startListening();
    placeCursor();
    const lockable = !isTouch && !platform.engine.webkit;
    twin.report('OnScrubStart', { cursor: lockable, event: eventInfo(event) } satisfies NumberFieldScrubStartReport);

    // WebKit causes significant layout shift with the native message, so it never locks.
    if (lockable) {
      const settle = (denied: boolean) => {
        if (disposed || denied === lockDenied) {
          return;
        }
        lockDenied = denied;
        twin.report('OnScrubLock', { denied } satisfies NumberFieldScrubLockReport);
      };
      try {
        Promise.resolve(ownerDocument(area).body.requestPointerLock()).then(
          () => settle(false),
          () => settle(true),
        );
      } catch {
        settle(true);
      }
    }
  };

  // NumberFieldScrubArea.tsx:279-298 (NUM-PTR-15): one finger scrubs; pinch-zoom stays the browser's.
  const onTouchStart = (event: TouchEvent) => {
    if (!inactive() && event.touches.length === 1) {
      event.preventDefault();
    }
  };

  area.addEventListener('pointerdown', onPointerDown);
  area.addEventListener('click', onClickCapture, true);
  area.addEventListener('touchstart', onTouchStart, { passive: false });

  return {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<NumberFieldScrubOptions>(nextOptionsJson);
      // NumberFieldScrubArea.tsx:262-276: becoming disabled or read-only mid-scrub tears the listeners down.
      if (inactive()) {
        stopListening();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      // NumberFieldScrubArea.tsx:262-276 (NUM-PTR-17): an unmount mid-scrub releases the lock; C# clears the state.
      if (pointerDown) {
        pointerDown = false;
        exitLock();
      }
      stopListening();
      clearTimeout(clickTimer);
      if (area[scrubState] === state) {
        delete area[scrubState];
      }
      area.removeEventListener('pointerdown', onPointerDown);
      area.removeEventListener('click', onClickCapture, true);
      area.removeEventListener('touchstart', onTouchStart);
    },
  };
}

// The cursor's handle (NUM-STR-06, NUM-DOM-13/14): C# renders it while the scrub area's conditions hold and the portal
// moves it to the body; this handle takes its style over and hands the element to its scrub area, which writes the
// transform. A cursor whose scrub area has not attached yet waits on the scrub area element.
function attachCursor(cursor: HTMLElement, initial: NumberFieldScrubOptions, references?: TwinReferences): TwinHandle {
  const area = references?.[0] instanceof HTMLElement ? (references[0] as ScrubElement) : null;
  let disposed = false;
  const writer = styleWriter(cursor);
  // The handover (ADR 04 Amendments 2026-10-18): from attach the element's whole style is the writer's.
  cursor.style.cssText = '';
  writer.setConsumer(initial.style ?? null);

  if (area !== null) {
    if (area[scrubState]) {
      area[scrubState]!.setCursor(cursor);
    } else {
      area[pendingCursor] = cursor;
    }
  }

  return {
    update(nextOptionsJson: string) {
      if (!disposed) {
        writer.setConsumer(parseOptions<NumberFieldScrubOptions>(nextOptionsJson).style ?? null);
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      writer.layer('component').clear();
      if (area !== null) {
        if (area[pendingCursor] === cursor) {
          area[pendingCursor] = null;
        }
        area[scrubState]?.setCursor(null);
      }
    },
  };
}
