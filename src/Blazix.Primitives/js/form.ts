// The `form` mechanism module: the browser half of the field and form infrastructure (docs/architecture/
// field-and-form-infrastructure.md). ADR 04 §1, the `form` row (Amendments 2026-10-18): validity reads and custom validity
// messages, disabled and form-owner eligibility, native label lookup, the default submitter, focus redirect; the module
// owns a control's native input, change, blur and keydown listeners and reports value and validity in one message per
// event (M2 plan, settled; ADR 16 rule 6). The scopes, the registry and the validation pipeline are C#.
import { findHiddenInput } from './button';
import { activeElement, closest, getTarget, ownerDocument, ownerWindow } from './dom';
import { decide } from './decide';
import { eventInfo, type EventInfo } from './event-info';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
  type TwinCaptures,
  type TwinReferences,
} from './twin';

/** The options of an attached control; mirrors `FormControlOptions` in C#. */
export interface FormControlOptions {
  formId?: string | null;
  customValidity?: string | null;
  controlled?: boolean;
  value?: string | null;
  defaultValue?: string | null;
  revision?: number;
  reportKeys?: boolean;
  disabled?: boolean;
  reportEvents?: boolean;
  focusTarget?: string | null;
  autoFocus?: boolean;
  enterSubmit?: boolean;
  labelFallback?: boolean;
}

/** The options of an attached field label; mirrors `FormLabelOptions` in C#. */
export interface FormLabelOptions {
  native?: boolean;
  controlId?: string | null;
  decides?: boolean;
  developmentChecks?: boolean;
}

/** The options of an attached form element; mirrors `FormSubmitOptions` in C#. */
export interface FormSubmitOptions {
  token?: string | null;
  focusRevision?: number;
  focus?: string[] | null;
}

/** The options of the module; mirrors `FormOptions` in C#. Exactly one role is set per attached element. */
export interface FormOptions extends TwinOptions {
  control?: FormControlOptions | null;
  label?: FormLabelOptions | null;
  submit?: FormSubmitOptions | null;
}

/** The state of one control enrolled on a form, read when the form submits; mirrors `FormControlState` in C#. */
export interface FormControlState {
  id: string;
  value: string | null;
  validity: NativeValidity;
}

/** One submission of a form; mirrors `FormSubmitRequest` in C#. */
export interface FormSubmitRequest {
  event: EventInfo;
  submitterId: string | null;
  controls: FormControlState[];
}

/** C#'s answer to a submission; mirrors `FormSubmitAnswer` in C#. */
export interface FormSubmitAnswer {
  verdict: 'submit' | 'blocked' | 'handled';
  focus?: string[] | null;
}

/** The native validity of an input with the field's own custom message read out; mirrors `NativeValidity` in C#. */
export interface NativeValidity {
  badInput: boolean;
  customError: boolean;
  patternMismatch: boolean;
  rangeOverflow: boolean;
  rangeUnderflow: boolean;
  stepMismatch: boolean;
  tooLong: boolean;
  tooShort: boolean;
  typeMismatch: boolean;
  valid: boolean;
  valueMissing: boolean;
  validationMessage: string;
  willValidate: boolean;
  eligible: boolean;
  ownMessageInstalled: boolean;
}

/** One report of a control; mirrors `FormControlReport` in C#. */
export interface FormControlReport {
  type: 'input' | 'blur' | 'keydown' | 'enter' | 'sync' | 'focus' | 'label';
  value: string | null;
  validity: NativeValidity | null;
  event: EventInfo | null;
  sameForm: boolean;
  revision: number;
}

/** An element with the constraint validation API: an input, a textarea, a select, a button. */
type ValidatableElement = HTMLElement & {
  validity: ValidityState;
  validationMessage: string;
  willValidate: boolean;
  setCustomValidity(message: string): void;
  form?: HTMLFormElement | null;
};

const VALIDITY_KEYS = [
  'badInput',
  'customError',
  'patternMismatch',
  'rangeOverflow',
  'rangeUnderflow',
  'stepMismatch',
  'tooLong',
  'tooShort',
  'typeMismatch',
  'valid',
  'valueMissing',
] as const;

// FFI-FRM-09: the field's own message and the message it displaced, kept on the element so that the form half (track 2)
// reads the same base state as the control's handle.
const ownership = Symbol('blazix.form.ownership');
const liveHandle = Symbol('blazix.form.handle');
// FRM-DOM-02/03, design point F6 (settled by the orchestrator 2026-10-23; ADR 04 §3's one-way contract): the forms of the
// page by the token C# draws and never renders, each with the controls enrolled on it. The one module-level registry of
// the module (js/CONTEXT.md): a control and its form attach in either order, so a control enrols by token, and resolves its
// form at each use.
interface FormRecord {
  element: HTMLElement | null;
  controls: Set<EnrolledControl>;
}

const forms = new Map<string, FormRecord>();

function formRecord(token: string): FormRecord {
  let record = forms.get(token);
  if (!record) {
    record = { element: null, controls: new Set() };
    forms.set(token, record);
  }
  return record;
}

function releaseFormRecord(token: string, record: FormRecord): void {
  if (record.element === null && record.controls.size === 0 && forms.get(token) === record) {
    forms.delete(token);
  }
}

interface OwnershipRecord {
  owned: string | null;
  displaced: string;
}

type OwnedElement = ValidatableElement & { [ownership]?: OwnershipRecord };
type HandleHost = HTMLElement & { [liveHandle]?: TwinHandle };

interface EnrolledControl {
  element: HTMLElement;
  read(): FormControlState;
}

// ---------------------------------------------------------------------------------------------------------------
// Pure helpers (exported for the form half and for the tests)
// ---------------------------------------------------------------------------------------------------------------

/**
 * Returns the default button a browser uses for implicit form submission: the first submit button or input of
 * `form.elements`, including one associated through `form="…"` and a disabled one; `input type="image"` is never one.
 * @param form The form, or `null`.
 * @returns The default submitter, or `null`.
 */
export function getDefaultFormSubmitter(form: HTMLFormElement | null): HTMLButtonElement | HTMLInputElement | null {
  if (!form) {
    return null;
  }

  for (const candidate of Array.from(form.elements)) {
    const tagName = candidate.tagName;
    if (tagName === 'BUTTON' || tagName === 'INPUT') {
      const button = candidate as HTMLButtonElement | HTMLInputElement;
      if (button.type === 'submit') {
        return button;
      }
    }
  }

  return null;
}

// FFI-ALG-04.
/**
 * Whether an input takes part in the enclosing form's validation: not `:disabled`, and owned by that form or
 * by no form without a `form` attribute.
 * @param input The input.
 * @param formElement The enclosing form's element, or `null` outside a form.
 * @returns The eligibility.
 */
export function isEligibleInput(input: Element, formElement: HTMLFormElement | null): boolean {
  if (input.matches(':disabled')) {
    return false;
  }

  const owner = (input as ValidatableElement).form ?? null;
  if (!formElement || owner === formElement) {
    return true;
  }

  return owner === null && !input.hasAttribute('form');
}

// FFI-ALG-02, FFI-FRM-09.
/**
 * Reads an input's native validity with the field's own custom message read out: while the
 * message is still installed, the read sees the message it displaced, and the element is left as it was.
 * @param element The input.
 * @param formElement The enclosing form's element, or `null`.
 * @returns The validity; an element without the constraint validation API reports a barred, valid state.
 */
export function readNativeValidity(element: Element, formElement: HTMLFormElement | null): NativeValidity {
  const input = element as OwnedElement;
  const eligible = isEligibleInput(element, formElement);
  if (!('validity' in input) || input.validity == null) {
    return { ...emptyValidity(), willValidate: false, eligible, ownMessageInstalled: false };
  }

  const record = input[ownership];
  const installed = record?.owned != null && input.willValidate && input.validationMessage === record.owned;
  if (record?.owned != null && !installed && input.willValidate) {
    // Replaced or withdrawn by someone else: the field no longer owns a message here.
    record.owned = null;
    record.displaced = '';
  }

  if (installed) {
    input.setCustomValidity(record!.displaced);
  }

  const result = { ...emptyValidity(), eligible, ownMessageInstalled: installed } as NativeValidity;
  for (const key of VALIDITY_KEYS) {
    result[key] = input.validity[key];
  }
  result.validationMessage = input.validationMessage;
  result.willValidate = input.willValidate;

  if (installed) {
    input.setCustomValidity(record!.owned!);
  }

  return result;
}

// FFI-FRM-08..10.
/**
 * Installs or withdraws the field's own custom message: the previous own message is withdrawn only while
 * it is still installed, restoring what it displaced; a new message records the custom message it displaces (never a
 * native one) and is not written to a barred input. `\r\n` and `\r` become `\n`.
 * @param element The input.
 * @param message The message, or `null` to withdraw the field's own message.
 */
export function applyOwnCustomValidity(element: Element, message: string | null | undefined): void {
  const input = element as OwnedElement;
  if (!('setCustomValidity' in input)) {
    return;
  }

  const record = (input[ownership] ??= { owned: null, displaced: '' });
  const normalized = message == null ? null : message.replace(/\r\n?/g, '\n');
  if (record.owned != null) {
    // Replacement transfers ownership; a barred control hides its message.
    if (!input.willValidate || input.validationMessage === record.owned) {
      input.setCustomValidity(record.displaced);
    }
    record.owned = null;
    record.displaced = '';
  }

  if (normalized != null && normalized !== '' && input.willValidate) {
    record.displaced = input.validity.customError ? input.validationMessage : '';
    input.setCustomValidity(normalized);
    record.owned = normalized;
  }
}

// FFI-ALG-08 (control lookup), FFI-PTR-04.
/**
 * Finds the control a label points at: from the label's root node outward through every
 * enclosing shadow root's host, then the document.
 * @param label The label element.
 * @param id The control id.
 * @returns The first element with that id, or `null`.
 */
export function getControlById(label: Element, id: string): Element | null {
  let root: Node = label.getRootNode();
  while (typeof ShadowRoot !== 'undefined' && root instanceof ShadowRoot) {
    const element = root.getElementById(id);
    if (element) {
      return element;
    }
    root = root.host.getRootNode();
  }
  return ownerDocument(label).getElementById(id);
}

// FFI-ALG-08 `discover`, FFI-ARIA-06.
/**
 * Discovers the native label of a form-participation input: its parent label, a next
 * sibling label whose `for` is the input's id, or its first associated label; an id-less label gets `generatedLabelId`.
 * @param input The input.
 * @param generatedLabelId The id to give an id-less label (`${controlId}-label`), or `null`.
 * @returns The label's id, or `null` when no label is found or it has no id.
 */
export function discoverLabel(input: HTMLInputElement, generatedLabelId: string | null): string | null {
  let label: HTMLLabelElement | null = null;
  if (input.parentElement instanceof HTMLLabelElement) {
    label = input.parentElement;
  } else if (input.id && input.nextElementSibling instanceof HTMLLabelElement && input.nextElementSibling.htmlFor === input.id) {
    label = input.nextElementSibling;
  } else {
    label = input.labels?.[0] ?? null;
  }

  if (!label) {
    return null;
  }

  if (label.id === '' && generatedLabelId) {
    label.id = generatedLabelId;
  }

  return label.id || null;
}

function emptyValidity(): NativeValidity {
  return {
    badInput: false,
    customError: false,
    patternMismatch: false,
    rangeOverflow: false,
    rangeUnderflow: false,
    stepMismatch: false,
    tooLong: false,
    tooShort: false,
    typeMismatch: false,
    valid: true,
    valueMissing: false,
    validationMessage: '',
    willValidate: true,
    eligible: true,
    ownMessageInstalled: false,
  };
}

function valueOf(element: Element): string | null {
  return 'value' in element && typeof (element as HTMLInputElement).value === 'string' ? (element as HTMLInputElement).value : null;
}

// FRM-DOM-03: the form element registered under the token, or null when the form is not attached.
function formById(_element: Element, formId: string | null | undefined): HTMLFormElement | null {
  const form = formId ? (forms.get(formId)?.element ?? null) : null;
  return form instanceof HTMLFormElement ? form : null;
}

// CHK-KEY-03..07, FFI-KEY-02, FFI-ALG-10 (design point C2 of M2 track 5, settled by the orchestrator 2026-10-23). The Enter path of a
// checkbox's root. Base UI cancels the native default on the native event only, so that React ancestors still see
// `defaultPrevented === false`, and decides in a microtask after React's dispatch; the port has one event, and a
// microtask queued by a listener runs before the next listener of a user event. So the listener runs in the capture
// phase at the root (before the `button` module's Enter suppression and any bubbling listener), cancels the default at
// once, records a later `preventDefault()` by shadowing it on the event, and decides in a window listener added during
// the dispatch, which runs after every ancestor (a 0 ms task when an ancestor stopped the propagation). A JavaScript
// ancestor therefore sees `defaultPrevented === true`, a dated note on CHK-KEY-03.
function createEnterSubmission(element: HTMLElement, isEnabled: () => boolean): () => void {
  const onKeyDown = (event: KeyboardEvent): void => {
    if (event.key !== 'Enter' || !isEnabled()) {
      return;
    }

    const preventedBefore = event.defaultPrevented;
    Event.prototype.preventDefault.call(event);
    let preventedLater = false;
    Object.defineProperty(event, 'preventDefault', {
      configurable: true,
      value: () => {
        preventedLater = true;
      },
    });

    const win = ownerWindow(element);
    let decided = false;
    const decide = (): void => {
      if (decided) {
        return;
      }
      decided = true;
      win.removeEventListener('keydown', onTail);
      win.clearTimeout(fallback);
      if (preventedBefore || preventedLater) {
        return;
      }
      win.queueMicrotask(() => {
        const submitter = getDefaultFormSubmitter(findHiddenInput(element)?.form ?? null);
        if (submitter && !submitter.disabled) {
          submitter.click();
        }
      });
    };
    const onTail = (tail: Event): void => {
      if (tail === event) {
        decide();
      }
    };
    win.addEventListener('keydown', onTail);
    const fallback = win.setTimeout(decide, 0);
  };

  element.addEventListener('keydown', onKeyDown, { capture: true });
  return () => element.removeEventListener('keydown', onKeyDown, { capture: true });
}

// CHK-ARIA-08, CHK-EDGE-13 (design point C4 of M2 track 5, settled by the orchestrator 2026-10-23): Base UI looks for the label after every
// commit of the box; a Blazor component whose parameters did not change does not render again when a label elsewhere
// mounts, so the module watches the document instead. One observer per document serves every input that looks for its
// label; its callbacks run once per mutation batch, after the renderer's or a script's change of a label or an id.
const labelWatchers = new WeakMap<Document, { observer: MutationObserver; callbacks: Set<() => void> }>();

function watchLabels(element: Element, callback: () => void): () => void {
  const doc = ownerDocument(element);
  let watcher = labelWatchers.get(doc);
  if (!watcher) {
    const callbacks = new Set<() => void>();
    const observer = new (ownerWindow(element).MutationObserver)(() => {
      for (const run of [...callbacks]) {
        run();
      }
    });
    observer.observe(doc, { childList: true, subtree: true, attributes: true, attributeFilter: ['id', 'for'] });
    watcher = { observer, callbacks };
    labelWatchers.set(doc, watcher);
  }
  const current = watcher;
  current.callbacks.add(callback);
  return () => {
    current.callbacks.delete(callback);
    if (current.callbacks.size === 0) {
      current.observer.disconnect();
      labelWatchers.delete(doc);
    }
  };
}

function focusWithVisible(element: HTMLElement): void {
  // focusVisible: Chrome 144+, Safari and Firefox.
  element.focus({ focusVisible: true } as FocusOptions);
}

// ---------------------------------------------------------------------------------------------------------------
// attach
// ---------------------------------------------------------------------------------------------------------------

/**
 * Attaches the module to a control, a control's validation input or a field label.
 * @param elementOrId The element, or its id.
 * @param optionsJson The options.
 * @param twinReference The twin's object reference.
 * @param _references The references, which this module names none of.
 * @param captures The capture ids C# sends beside the element and the references, which find them inside a shadow root.
 * @returns The handle with `update` and `dispose`; a repeated attach of the same element returns the live handle.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  _references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const element = resolveElement(elementOrId, captures?.[0]) as HandleHost;
  const existing = element[liveHandle];
  if (existing) {
    return existing;
  }

  const channel = createTwinChannel(twinReference);
  const options = parseOptions<FormOptions>(optionsJson);
  const handle = options.submit
    ? attachForm(element, options.submit, twinReference)
    : options.label
      ? attachLabel(element, options.label, channel)
      : attachControl(element, options.control ?? {}, channel);
  element[liveHandle] = handle;
  return {
    update: handle.update,
    dispose() {
      if (element[liveHandle] === handle) {
        delete element[liveHandle];
      }
      handle.dispose();
    },
  };
}

// FLD-EVT-01..07, FLD-FOC-01..04, FLD-KEY-01, FFI-ALG-16, FFI-STA-09, FFI-FOC-05, FFI-EDGE-06/07, FLD-API-15/16.
function attachControl(element: HTMLElement, initial: FormControlOptions, channel: TwinChannel): TwinHandle {
  let options = initial;
  let tracked = valueOf(element);
  // The value the module or the renderer last put in the element: a default value follows it only while it is unchanged.
  let expected = tracked;
  let enterTimer: ReturnType<typeof setTimeout> | undefined;

  function report(type: FormControlReport['type'], event: Event | undefined, extra?: Partial<FormControlReport>): void {
    channel.report('OnControlReport', {
      type,
      value: valueOf(element),
      validity: readNativeValidity(element, formById(element, options.formId)),
      event: event ? eventInfo(event) : null,
      sameForm: false,
      revision: options.revision ?? 0,
      ...extra,
    } satisfies FormControlReport);
  }

  function setValue(value: string): void {
    (element as HTMLInputElement).value = value;
    expected = value;
    tracked = value;
  }

  function apply(next: FormControlOptions, previous: FormControlOptions | null, keepLiveValue = false): void {
    if (next.controlled) {
      // FLD-API-15: the element shows the consumer's value; an edit it did not apply is reverted.
      if (!keepLiveValue && next.value != null && valueOf(element) !== next.value) {
        setValue(next.value);
      }
    } else if ('defaultValue' in element && (previous === null || previous.defaultValue !== next.defaultValue)) {
      // FLD-API-16, FFI-EDGE-06/07: a default value is the element's default; the live value follows it only while
      // nothing (the user or a script) changed the value since the module or the renderer last set it.
      const input = element as HTMLInputElement;
      if (next.defaultValue != null || previous !== null) {
        input.defaultValue = next.defaultValue ?? '';
      }
      if (previous !== null && input.value === expected) {
        setValue(next.defaultValue ?? '');
      }
    }

    applyOwnCustomValidity(element, next.customValidity);
  }

  function onValue(event: Event): void {
    // React's value tracker: one report per actual change of the value, whatever event carried it.
    const current = valueOf(element);
    if (current === tracked) {
      return;
    }
    tracked = current;
    report('input', event);
  }

  function onBlur(event: Event): void {
    report('blur', event);
  }

  function onKeyDown(event: KeyboardEvent): void {
    const enterOnInput = event.key === 'Enter' && element.tagName === 'INPUT';
    if (!enterOnInput && !options.reportKeys) {
      return;
    }

    const form = (element as HTMLInputElement).form ?? null;
    const sameForm = form !== null && form === formById(element, options.formId);
    report('keydown', event, { sameForm });
    if (enterOnInput && sameForm && !event.defaultPrevented) {
      // FFI-ALG-16: implicit submission runs after the keydown; the deferred commit reports the value it then has.
      clearTimeout(enterTimer);
      enterTimer = setTimeout(() => {
        enterTimer = undefined;
        report('enter', undefined, { sameForm });
      }, 0);
    }
  }

  // FFI-FOC-05: a form-participation input hands focus to its visible control.
  function onFocus(): void {
    if (!options.focusTarget) {
      return;
    }
    const target = ownerDocument(element).getElementById(options.focusTarget);
    if (target instanceof HTMLElement && target !== element) {
      target.focus();
    }
  }

  function reportFocusIfActive(): void {
    // FFI-STA-09, FLD-FOC-04: a control that already has focus when it mounts or becomes enabled acquires the flag.
    const root = element.getRootNode() as Document | ShadowRoot;
    const active = 'activeElement' in root ? root.activeElement : activeElement(ownerDocument(element));
    if (active === element) {
      report('focus', undefined);
    }
  }

  // CHK-ARIA-07/08 (design point C4 of M2 track 5, settled by the orchestrator 2026-10-23): while the part asks for it, the native label of a
  // form-participation input is discovered on attach, after every update and after every change of the document's labels
  // and ids, and reported when it changed.
  let discoveredLabel: string | null | undefined;
  let stopWatchingLabels: (() => void) | null = null;
  function discover(): void {
    if (!options.labelFallback) {
      discoveredLabel = undefined;
      stopWatchingLabels?.();
      stopWatchingLabels = null;
      return;
    }
    stopWatchingLabels ??= watchLabels(element, discover);
    const labelId = discoverLabel(element as HTMLInputElement, element.id ? `${element.id}-label` : null);
    if (labelId !== discoveredLabel) {
      discoveredLabel = labelId;
      // The label's id travels as the report's value (one payload shape for every report).
      report('label', undefined, { value: labelId });
    }
  }

  const reports = options.reportEvents !== false;
  // The handover at attach hands over the element's state: a live value that differs from the one C# rendered is the
  // user's edit, made before the module listened, so it is kept and reported rather than overwritten (settled by the
  // orchestrator 2026-10-22).
  const rendered = (options.controlled ? options.value : options.defaultValue) ?? '';
  const editedBeforeAttach = reports && tracked !== null && tracked !== rendered;
  if (editedBeforeAttach) {
    expected = rendered;
  }
  apply(options, null, editedBeforeAttach);

  // FFI-FRM-04, FFI-EDGE-07: the control's state is read when its form submits, in the submit task.
  const enrollment: EnrolledControl = {
    element,
    read: () => ({ id: element.id, value: valueOf(element), validity: readNativeValidity(element, formById(element, options.formId)) }),
  };
  let enrolledToken: string | null = null;
  function withdraw(): void {
    if (enrolledToken !== null) {
      const record = forms.get(enrolledToken);
      if (record) {
        record.controls.delete(enrollment);
        releaseFormRecord(enrolledToken, record);
      }
      enrolledToken = null;
    }
  }
  function enroll(): void {
    const token = options.formId || null;
    if (token === enrolledToken) {
      return;
    }
    withdraw();
    if (token !== null) {
      formRecord(token).controls.add(enrollment);
      enrolledToken = token;
    }
  }
  enroll();
  if (reports) {
    element.addEventListener('input', onValue);
    element.addEventListener('change', onValue);
    element.addEventListener('blur', onBlur);
    element.addEventListener('keydown', onKeyDown);
  }
  element.addEventListener('focus', onFocus);
  const removeEnterSubmission = createEnterSubmission(element, () => options.enterSubmit === true && !options.disabled);
  if (options.autoFocus && !options.disabled) {
    // FLD-FOC-04: the renderer replaces a prerendered element when the page becomes interactive, which loses the focus
    // the browser's autofocus gave it; the module focuses the control once, at attach, unless something else has focus.
    const doc = ownerDocument(element);
    const active = activeElement(doc);
    if (active === null || active === doc.body) {
      element.focus();
    }
  }
  if (editedBeforeAttach) {
    report('input', undefined);
  }
  if (reports && !options.disabled) {
    reportFocusIfActive();
  }
  discover();

  return {
    update(optionsJson: string) {
      const previous = options;
      options = parseOptions<FormOptions>(optionsJson).control ?? {};
      enroll();
      apply(options, previous);
      if ((options.revision ?? 0) > (previous.revision ?? 0)) {
        report('sync', undefined);
      }
      if (reports && previous.disabled && !options.disabled) {
        reportFocusIfActive();
      }
      discover();
    },
    dispose() {
      withdraw();
      clearTimeout(enterTimer);
      element.removeEventListener('input', onValue);
      element.removeEventListener('change', onValue);
      element.removeEventListener('blur', onBlur);
      element.removeEventListener('keydown', onKeyDown);
      element.removeEventListener('focus', onFocus);
      removeEnterSubmission();
      stopWatchingLabels?.();
      stopWatchingLabels = null;
    },
  };
}

// FFI-PTR-01..04, FLD-PTR-01..04, FFI-EDGE-02. A consumer's C# pointer handler runs first inside the decision point
// `label` (fallback run) when the options say the consumer has one.
function attachLabel(element: HTMLElement, initial: FormLabelOptions, channel: TwinChannel): TwinHandle {
  let options = initial;

  function vetoed(event: Event): boolean {
    return options.decides === true && decide(channel, 'label', event, 'run') === 'stop';
  }

  function isNestedControl(event: Event): boolean {
    const target = getTarget(event) as Node | null;
    return closest(target, 'button,input,select,textarea') !== null;
  }

  // useLabel's handleInteraction: the double-click guard, then (non-native) the click-to-focus.
  function onMouseDown(event: MouseEvent): void {
    if (options.native === false || isNestedControl(event) || vetoed(event)) {
      return;
    }
    if (!event.defaultPrevented && event.detail > 1) {
      event.preventDefault();
    }
  }

  function onClick(event: MouseEvent): void {
    if (options.native !== false || isNestedControl(event) || vetoed(event)) {
      return;
    }
    if (!event.defaultPrevented && event.detail > 1) {
      event.preventDefault();
    }
    if (!options.controlId) {
      return;
    }
    const control = getControlById(element, options.controlId);
    if (control instanceof HTMLElement) {
      focusWithVisible(control);
    }
  }

  function onPointerDown(event: PointerEvent): void {
    if (options.native !== false || vetoed(event)) {
      return;
    }
    event.preventDefault();
  }

  function checkElement(): void {
    if (!options.developmentChecks) {
      return;
    }
    const isLabel = element.tagName === 'LABEL';
    const native = options.native !== false;
    if (native !== isLabel) {
      channel.report('OnLabelMismatch', { expectedNative: native });
    }
  }

  element.addEventListener('mousedown', onMouseDown);
  element.addEventListener('click', onClick);
  element.addEventListener('pointerdown', onPointerDown);
  checkElement();

  return {
    update(optionsJson: string) {
      const previous = options;
      options = parseOptions<FormOptions>(optionsJson).label ?? {};
      if ((previous.native !== false) !== (options.native !== false)) {
        checkElement();
      }
    },
    dispose() {
      element.removeEventListener('mousedown', onMouseDown);
      element.removeEventListener('click', onClick);
      element.removeEventListener('pointerdown', onPointerDown);
    },
  };
}

// FRM-EVT-01, FFI-ALG-05, FFI-FOC-01/02, FRM-FOC-01/02 (ADR 04 Amendments 2026-10-18, the form row; the submit half of
// M2 track 2). The native submit is always prevented and reported with the state of every enrolled control; C#'s answer
// re-issues it with requestSubmit(submitter) behind a one-shot pass flag, focuses the first invalid control, or does
// nothing (design point F1: the answer is the result of the OnSubmitRequest invocation).
function attachForm(element: HTMLElement, initial: FormSubmitOptions, twinReference: DotNetTwin): TwinHandle {
  let options = initial;
  let pass = false;
  const token = initial.token || null;
  const record = token !== null ? formRecord(token) : null;
  if (record) {
    record.element = element;
  }

  function onSubmit(event: Event): void {
    if (pass) {
      // The re-issued submission: the browser submits it. The flag serves this one event.
      pass = false;
      return;
    }
    event.preventDefault();
    const submitter = (event as SubmitEvent).submitter ?? null;
    const request: FormSubmitRequest = {
      event: eventInfo(event),
      submitterId: submitter?.id || null,
      controls: Array.from(record?.controls ?? [], (control) => control.read()),
    };
    twinReference.invokeMethodAsync<string>('OnSubmitRequest', JSON.stringify(request)).then(
      (answerJson) => act(JSON.parse(answerJson) as FormSubmitAnswer, submitter),
      () => {
        // The circuit is gone: nothing to submit.
      },
    );
  }

  function act(answer: FormSubmitAnswer, submitter: HTMLElement | null): void {
    if (answer.verdict === 'blocked') {
      focusFirstInvalid(element, record, answer.focus ?? []);
      return;
    }
    if (answer.verdict !== 'submit' || !(element instanceof HTMLFormElement) || !element.isConnected) {
      return;
    }
    const button = submitter && submitter.isConnected && (submitter as HTMLButtonElement).form === element ? (submitter as HTMLButtonElement) : undefined;
    pass = true;
    try {
      element.requestSubmit(button);
    } finally {
      pass = false;
    }
  }

  element.addEventListener('submit', onSubmit);

  return {
    update(optionsJson: string) {
      const previous = options;
      options = parseOptions<FormOptions>(optionsJson).submit ?? {};
      // FRM-FOC-02: after a passed submission, the next change of the error map focuses the first invalid control once.
      if ((options.focusRevision ?? 0) > (previous.focusRevision ?? 0)) {
        focusFirstInvalid(element, record, options.focus ?? []);
      }
    },
    dispose() {
      element.removeEventListener('submit', onSubmit);
      if (record && token !== null && record.element === element) {
        record.element = null;
        releaseFormRecord(token, record);
      }
    },
  };
}

// FFI-FOC-01/02, FFI-ALG-05: the first control by document order; registration order where the trees are disconnected
// (separate shadow roots). An id without a usable control is skipped. An enrolled control is found inside a shadow root.
function focusFirstInvalid(form: HTMLElement, record: FormRecord | null, ids: readonly string[]): void {
  const controls = record?.controls;
  let first: HTMLElement | null = null;
  for (const id of ids) {
    let control: Element | null = null;
    if (controls) {
      for (const candidate of controls) {
        if (candidate.element.id === id && candidate.element.isConnected) {
          control = candidate.element;
          break;
        }
      }
    }
    control ??= getControlById(form, id);
    if (control instanceof HTMLElement && (!first || comesBeforeInSameTree(control, first))) {
      first = control;
    }
  }
  if (!first) {
    return;
  }
  first.focus();
  if (first.tagName === 'INPUT') {
    (first as HTMLInputElement).select();
  }
}

function comesBeforeInSameTree(element: Node, reference: Node): boolean {
  const position = element.compareDocumentPosition(reference);
  return (position & Node.DOCUMENT_POSITION_DISCONNECTED) === 0 && (position & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
}
