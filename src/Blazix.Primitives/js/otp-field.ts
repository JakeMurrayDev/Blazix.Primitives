// The `otp-field` component module (ADR 04 §1; Amendments 2026-11-18, the scope cell, `edit` and variant (ii); Amendments
// 2026-11-19, design points 1–7 of M6 track 7, chosen by the user 2026-11-19; issue 278). The published interface: the
// options C# sends, the reports the module sends and the five decision points. The behaviour lands in the next commits.
import { createTwinChannel, parseOptions, resolveElement } from './twin';
import type { DotNetTwin, TwinCaptures, TwinHandle, TwinOptions, TwinReferences } from './twin';
import type { EventInfo } from './event-info';

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
  resolveElement(elementOrId, captures?.[0]);
  createTwinChannel(twinReference);
  let options = parseOptions<OtpFieldOptions>(optionsJson);
  void options;
  return {
    update(nextJson: string): void {
      options = parseOptions<OtpFieldOptions>(nextJson);
    },
    dispose(): void {},
  };
}
