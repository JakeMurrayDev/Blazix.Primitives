// The `number-field` component module (ADR 04 §1 and Amendments 2026-11-18, the `number-field` scope: the input role and the
// scrub role). 6a ships the input role's minimal surface (issue 277, design point 2 (b), settled by the orchestrator
// 2026-11-18): the visible input's `value` handover (C# renders the first text, the module owns it from attach and writes
// C#'s answers, design point 7 (a): every input report carries a revision that `update` echoes, and an update older than
// the last report is not applied, ADR 04 Amendments 2026-11-18's revision rule widened to typing), and the focus and blur
// reports with Base UI's `defaultPrevented` checks (number-field/input/NumberFieldInput.tsx:131-150). A C# consumer's veto
// is the `input-focus` / `input-blur` decision point, fallback run (design point 1 (a), names chosen by the user
// 2026-11-18). 6b adds the keydown and `beforeinput` gate, paste, the wheel, the touch scrub and the scrub role.
// Requirements: docs/components/number-field.md (NUM-STA-05/06, NUM-EVT-06..08, NUM-FOC-05, NUM-FRM-07).
import { decide } from './decide';
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

/**
 * Attaches the input role to the visible input of a number field.
 * @param elementOrId The input, or its id when the part has no element reference.
 * @param optionsJson The `NumberFieldOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param _references The references, which this module names none of.
 * @param captures The capture ids C# sends beside the element, which find it inside a shadow root.
 * @returns The handle with `update(optionsJson)` and `dispose()`.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  _references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const input = resolveElement(elementOrId, captures?.[0]) as HTMLInputElement;
  const twin = createTwinChannel(twinReference);
  let options = parseOptions<NumberFieldOptions>(optionsJson);
  let revision = 0;
  let disposed = false;

  // The handover at attach (ADR 04 Amendments 2026-10-18): C# rendered the first text; from now on the module writes it.
  const write = (next: NumberFieldOptions) => {
    if (next.revision >= revision && input.value !== next.value) {
      input.value = next.value;
    }
  };
  write(options);

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

  input.addEventListener('focus', onFocus);
  input.addEventListener('blur', onBlur);

  return {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<NumberFieldOptions>(nextOptionsJson);
      write(options);
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      input.removeEventListener('focus', onFocus);
      input.removeEventListener('blur', onBlur);
    },
  };
}
