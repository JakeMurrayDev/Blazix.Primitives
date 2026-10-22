// L3 (ADR 06 §1): the `form` module in jsdom against a fake twin. `getDefaultFormSubmitter` keeps the five titles of
// Base UI's getDefaultFormSubmitter.test.ts (FFI-ALG-10, FFI-EDGE-04); the port's own titles cover the field half's
// reports, the ownership of custom validity and form-owner eligibility (field-and-form-infrastructure.md, FFI).
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  applyOwnCustomValidity,
  attach,
  discoverLabel,
  getControlById,
  getDefaultFormSubmitter,
  isEligibleInput,
  readNativeValidity,
  type FormControlOptions,
  type FormControlReport,
  type FormLabelOptions,
} from './form';
import type { DotNetTwin, TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin, type FakeTwinOptions } from './test-support/fake-twin';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.innerHTML = '';
  vi.useRealTimers();
});

function controlJson(control: FormControlOptions = {}): string {
  return JSON.stringify({ control: { reportEvents: true, ...control } });
}

function labelJson(label: FormLabelOptions = {}): string {
  return JSON.stringify({ label: { native: true, ...label } });
}

function attachControl(element: HTMLElement, control: FormControlOptions = {}, twin: FakeTwinOptions = {}): { handle: TwinHandle; fake: FakeTwin } {
  const fake = createFakeTwin(twin);
  const handle = attach(element, controlJson(control), fake.twin);
  handles.push(handle);
  return { handle, fake };
}

function attachLabel(element: HTMLElement, label: FormLabelOptions = {}, twin: FakeTwinOptions = {}): { handle: TwinHandle; fake: FakeTwin } {
  const fake = createFakeTwin(twin);
  const handle = attach(element, labelJson(label), fake.twin);
  handles.push(handle);
  return { handle, fake };
}

function input(html = '<input id="control" />'): HTMLInputElement {
  document.body.innerHTML = html;
  return document.querySelector<HTMLInputElement>('#control')!;
}

function type(element: HTMLInputElement, value: string): void {
  element.value = value;
  element.dispatchEvent(new Event('input', { bubbles: true }));
}

describe('getDefaultFormSubmitter', () => {
  function setup(html: string) {
    document.body.innerHTML = html;
    return document.querySelector<HTMLFormElement>('#test-form')!;
  }

  it('returns the first submit button associated with the form', () => {
    const form = setup(`
      <button id="external-before" form="test-form" type="submit">External before</button>
      <form id="test-form">
        <input type="checkbox" />
        <button id="internal" type="submit">Internal</button>
      </form>
    `);

    expect(getDefaultFormSubmitter(form)).toBe(document.querySelector('#external-before'));
  });

  it('returns a disabled submit button when it is first in form.elements', () => {
    const form = setup(`
      <form id="test-form">
        <button id="disabled" type="submit" disabled>Disabled</button>
        <button id="enabled" type="submit">Enabled</button>
      </form>
    `);

    expect(getDefaultFormSubmitter(form)).toBe(document.querySelector('#disabled'));
  });

  it('supports buttons with the default submit type', () => {
    const form = setup(`
      <form id="test-form">
        <button id="default">Default</button>
        <button id="explicit" type="submit">Explicit</button>
      </form>
    `);

    expect(getDefaultFormSubmitter(form)).toBe(document.querySelector('#default'));
  });

  it('supports input submitters from form.elements and ignores non-submit controls', () => {
    const form = setup(`
      <form id="test-form">
        <button id="button" type="button">Button</button>
        <input id="reset" type="reset" />
        <input id="submit" type="submit" />
      </form>
    `);

    expect(getDefaultFormSubmitter(form)).toBe(document.querySelector('#submit'));
  });

  it('returns null when there is no default submitter', () => {
    const form = setup(`
      <form id="test-form">
        <input type="checkbox" />
        <button type="button">Button</button>
      </form>
    `);

    expect(getDefaultFormSubmitter(form)).toBe(null);
  });
});

describe('isEligibleInput', () => {
  it('excludes a disabled input and one inside a disabled fieldset', () => {
    document.body.innerHTML = `
      <form id="form">
        <input id="disabled" disabled />
        <fieldset disabled><input id="fieldset" /></fieldset>
        <input id="enabled" />
      </form>
    `;
    const form = document.querySelector<HTMLFormElement>('#form');

    expect(isEligibleInput(document.querySelector('#disabled')!, form)).toBe(false);
    expect(isEligibleInput(document.querySelector('#fieldset')!, form)).toBe(false);
    expect(isEligibleInput(document.querySelector('#enabled')!, form)).toBe(true);
  });

  it('excludes an input owned by another form and keeps one owned by no form', () => {
    document.body.innerHTML = `
      <form id="form"></form>
      <form id="other"></form>
      <input id="foreign" form="other" />
      <input id="missing" form="nowhere" />
      <div><input id="loose" /></div>
    `;
    const form = document.querySelector<HTMLFormElement>('#form');

    expect(isEligibleInput(document.querySelector('#foreign')!, form)).toBe(false);
    expect(isEligibleInput(document.querySelector('#missing')!, form)).toBe(false);
    expect(isEligibleInput(document.querySelector('#loose')!, form)).toBe(true);
    expect(isEligibleInput(document.querySelector('#foreign')!, null)).toBe(true);
  });
});

describe('custom validity ownership', () => {
  it('installs its own message and reads the base state with that message read out', () => {
    const element = input('<input id="control" required />');

    applyOwnCustomValidity(element, 'custom error');
    expect(element.validationMessage).toBe('custom error');

    const validity = readNativeValidity(element, null);
    expect(validity.customError).toBe(false);
    expect(validity.valueMissing).toBe(true);
    expect(validity.ownMessageInstalled).toBe(true);
    expect(element.validationMessage).toBe('custom error');
  });

  it('normalizes carriage returns in its own message', () => {
    const element = input();

    applyOwnCustomValidity(element, 'first\r\nsecond\rthird');

    expect(element.validationMessage).toBe('first\nsecond\nthird');
  });

  it('restores a message its own one displaced', () => {
    const element = input();
    element.setCustomValidity('external error');

    applyOwnCustomValidity(element, 'custom error');
    expect(readNativeValidity(element, null).validationMessage).toBe('external error');

    applyOwnCustomValidity(element, null);
    expect(element.validationMessage).toBe('external error');
  });

  it('does not adopt a native message as the message it displaced', () => {
    const element = input('<input id="control" type="email" />');
    element.value = 'bad';

    applyOwnCustomValidity(element, 'custom error');
    expect(element.validationMessage).toBe('custom error');

    element.value = 'a@b.co';
    applyOwnCustomValidity(element, null);

    expect(element.validity.customError).toBe(false);
    expect(element.validationMessage).toBe('');
  });

  it('keeps a message set outside the field that replaced its own', () => {
    const element = input();
    applyOwnCustomValidity(element, 'custom error');

    element.setCustomValidity('external error');
    expect(readNativeValidity(element, null).ownMessageInstalled).toBe(false);
    applyOwnCustomValidity(element, null);

    expect(element.validationMessage).toBe('external error');
  });

  it('does not write its own message to a barred control', () => {
    const element = input('<input id="control" readonly />');

    applyOwnCustomValidity(element, 'custom error');

    expect(element.validity.customError).toBe(false);
    expect(readNativeValidity(element, null).willValidate).toBe(false);
  });
});

describe('control', () => {
  it('reports the value and the validity once per change of the value', () => {
    const element = input('<input id="control" required />');
    const { fake } = attachControl(element);

    type(element, 'a');
    element.dispatchEvent(new Event('change', { bubbles: true }));

    const reports = fake.reported<FormControlReport>('OnControlReport');
    expect(reports).toHaveLength(1);
    const [first] = reports;
    expect(first).toMatchObject({ type: 'input', value: 'a', sameForm: false });
    expect(first?.validity).toMatchObject({ valid: true, valueMissing: false, eligible: true });
    expect(first?.event).toMatchObject({ kind: 'Event', type: 'input' });
  });

  it('reports a blur with the current validity', () => {
    const element = input('<input id="control" required />');
    const { fake } = attachControl(element);

    element.dispatchEvent(new FocusEvent('blur'));

    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([
      expect.objectContaining({ type: 'blur', value: '', validity: expect.objectContaining({ valueMissing: true, valid: false }) }),
    ]);
  });

  it('reports keys other than Enter only while the consumer handles keydown', () => {
    const element = input();
    const { fake, handle } = attachControl(element);

    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'a', bubbles: true }));
    expect(fake.reported('OnControlReport')).toHaveLength(0);

    handle.update(controlJson({ reportKeys: true }));
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'a', bubbles: true }));
    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'keydown' })]);
  });

  it('reports the value Enter commits after the implicit submission, in the enclosing form only', () => {
    vi.useFakeTimers();
    document.body.innerHTML = '<form id="form"><input id="control" /></form>';
    const element = document.querySelector<HTMLInputElement>('#control')!;
    const { fake } = attachControl(element, { formId: 'form' });

    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    element.value = 'rewritten';
    vi.advanceTimersByTime(0);

    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([
      expect.objectContaining({ type: 'keydown', sameForm: true }),
      expect.objectContaining({ type: 'enter', value: 'rewritten', sameForm: true }),
    ]);
  });

  it('reports Enter outside a form without a deferred commit', () => {
    vi.useFakeTimers();
    const element = input();
    const { fake } = attachControl(element);

    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    vi.advanceTimersByTime(0);

    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'keydown', sameForm: false })]);
  });

  it('does not defer a commit for a prevented Enter, and dispose drops a pending one', () => {
    vi.useFakeTimers();
    document.body.innerHTML = '<form id="form"><input id="control" /></form>';
    const element = document.querySelector<HTMLInputElement>('#control')!;
    element.addEventListener('keydown', (event) => event.preventDefault(), { once: true });
    const { fake, handle } = attachControl(element, { formId: 'form' });

    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    handle.dispose();
    vi.advanceTimersByTime(0);

    expect(fake.reported<FormControlReport>('OnControlReport').map((r) => r.type)).toEqual(['keydown', 'keydown']);
  });

  it('shows the consumer value and reverts an edit the consumer did not apply', () => {
    const element = input('<input id="control" value="kept" />');
    const { handle, fake } = attachControl(element, { controlled: true, value: 'kept' });
    expect(element.value).toBe('kept');

    type(element, 'typed');
    handle.update(controlJson({ controlled: true, value: 'kept' }));

    expect(element.value).toBe('kept');
    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'input', value: 'typed' })]);
  });

  // The handover hands over the element's state: a value that differs at attach from the one C# rendered is the user's
  // edit, made before the module listened, and is reported rather than overwritten.
  it('reports an edit made before attach instead of overwriting it', () => {
    const controlled = input('<input id="control" value="kept" />');
    controlled.value = 'early';
    const first = attachControl(controlled, { controlled: true, value: 'kept' });

    expect(controlled.value).toBe('early');
    expect(first.fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'input', value: 'early', event: null })]);
    first.handle.dispose();

    const uncontrolled = input('<input id="control" value="start" />');
    uncontrolled.value = 'edited';
    const second = attachControl(uncontrolled, { defaultValue: 'start' });

    expect(uncontrolled.value).toBe('edited');
    expect(uncontrolled.defaultValue).toBe('start');
    expect(second.fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'input', value: 'edited' })]);
  });

  it('reports nothing at attach when the element shows the value C# rendered', () => {
    const element = input('<input id="control" value="kept" />');
    const { fake } = attachControl(element, { controlled: true, value: 'kept' });

    expect(fake.reported('OnControlReport')).toHaveLength(0);
  });

  it('sets the default value and follows a changed one only while the value is unedited', () => {
    const element = input('<input id="control" value="first" />');
    const { handle } = attachControl(element, { defaultValue: 'first' });
    expect(element.defaultValue).toBe('first');
    expect(element.value).toBe('first');

    handle.update(controlJson({ defaultValue: 'second' }));
    expect(element.value).toBe('second');

    type(element, 'edited');
    handle.update(controlJson({ defaultValue: 'third' }));
    expect(element.defaultValue).toBe('third');
    expect(element.value).toBe('edited');
  });

  it('keeps a value a script wrote when the default value changes', () => {
    const element = input('<input id="control" value="foo" />');
    const { handle } = attachControl(element, { defaultValue: 'foo' });

    element.value = 'scripted';
    handle.update(controlJson({ defaultValue: 'bar' }));

    expect(element.value).toBe('scripted');
  });

  it('installs and withdraws the custom validity the options carry', () => {
    const element = input();
    const { handle } = attachControl(element, { customValidity: 'custom error' });
    expect(element.validationMessage).toBe('custom error');

    handle.update(controlJson({ customValidity: null }));

    expect(element.validationMessage).toBe('');
  });

  it('answers a revision with a sync report', () => {
    const element = input();
    const { handle, fake } = attachControl(element, { revision: 0 });

    handle.update(controlJson({ revision: 0 }));
    handle.update(controlJson({ revision: 1 }));

    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'sync', revision: 1 })]);
  });

  it('reports focus when the element is already focused at attach or when re-enabled', () => {
    const element = input();
    element.focus();
    const { handle, fake } = attachControl(element, { disabled: true });
    expect(fake.reported('OnControlReport')).toHaveLength(0);

    handle.update(controlJson({ disabled: false }));

    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'focus' })]);
  });

  // Blazor replaces a prerendered element when the page becomes interactive, so the browser's autofocus is lost; the
  // module focuses an autofocus control at attach unless something else already has focus (React's own autoFocus).
  it('focuses an autofocus control at attach when nothing has focus', () => {
    const element = input();
    const { fake } = attachControl(element, { autoFocus: true });

    expect(document.activeElement).toBe(element);
    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'focus' })]);
  });

  it('does not take focus from another element for an autofocus control', () => {
    document.body.innerHTML = '<button id="other">Other</button><input id="control" />';
    const other = document.querySelector<HTMLButtonElement>('#other')!;
    other.focus();
    const { fake } = attachControl(document.querySelector<HTMLInputElement>('#control')!, { autoFocus: true });

    expect(document.activeElement).toBe(other);
    expect(fake.reported('OnControlReport')).toHaveLength(0);
  });

  it('does not focus a disabled autofocus control, nor on a later update', () => {
    const element = input('<input id="control" disabled />');
    const { handle } = attachControl(element, { autoFocus: true, disabled: true });
    element.disabled = false;
    handle.update(controlJson({ autoFocus: true, disabled: false }));

    expect(document.activeElement).toBe(document.body);
  });

  it('hands focus to the focus target', () => {
    document.body.innerHTML = '<input id="control" /><button id="visible"></button>';
    const element = document.querySelector<HTMLInputElement>('#control')!;
    attachControl(element, { focusTarget: 'visible', reportEvents: false });

    element.focus();

    expect(document.activeElement).toBe(document.querySelector('#visible'));
  });

  it('returns the live handle for a repeated attach of the same element', () => {
    const element = input();
    const first = attachControl(element);
    const second = attach(element, controlJson(), createFakeTwin().twin);

    type(element, 'a');

    expect(first.fake.reported('OnControlReport')).toHaveLength(1);
    expect(second.update).toBe(first.handle.update);
  });

  it('stops reporting after dispose', () => {
    const element = input();
    const { handle, fake } = attachControl(element);

    handle.dispose();
    type(element, 'a');

    expect(fake.reported('OnControlReport')).toHaveLength(0);
  });
});

describe('label', () => {
  function labelled(html: string): { label: HTMLElement; control: HTMLInputElement } {
    document.body.innerHTML = html;
    return { label: document.querySelector<HTMLElement>('#label')!, control: document.querySelector<HTMLInputElement>('#control')! };
  }

  it('focuses the control on click when it is not a native label', () => {
    const { label, control } = labelled('<div id="label">Label</div><input id="control" />');
    attachLabel(label, { native: false, controlId: 'control' });

    label.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));

    expect(document.activeElement).toBe(control);
  });

  it('prevents the default of a pointerdown on a non-native label', () => {
    const { label } = labelled('<div id="label">Label</div><input id="control" />');
    attachLabel(label, { native: false, controlId: 'control' });
    const event = new MouseEvent('pointerdown', { bubbles: true, cancelable: true });

    label.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
  });

  it('prevents text selection on a double click of a native label', () => {
    const { label } = labelled('<label id="label" for="control">Label</label><input id="control" />');
    attachLabel(label, { controlId: 'control' });
    const single = new MouseEvent('mousedown', { bubbles: true, cancelable: true, detail: 1 });
    const double = new MouseEvent('mousedown', { bubbles: true, cancelable: true, detail: 2 });

    label.dispatchEvent(single);
    label.dispatchEvent(double);

    expect(single.defaultPrevented).toBe(false);
    expect(double.defaultPrevented).toBe(true);
  });

  it('ignores a click on a control nested in the label', () => {
    const { label } = labelled('<div id="label"><button id="nested">Nested</button></div><input id="control" />');
    attachLabel(label, { native: false, controlId: 'control' });

    document.querySelector<HTMLButtonElement>('#nested')!.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));

    expect(document.activeElement).toBe(document.body);
  });

  it('asks the label decision point when the consumer has a pointer handler, and a veto skips the focus', () => {
    const { label } = labelled('<div id="label">Label</div><input id="control" />');
    const { fake } = attachLabel(label, { native: false, controlId: 'control', decides: true }, { synchronous: true, verdict: 'stop' });

    label.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));

    expect(fake.decisions.map((d) => d.point)).toEqual(['label']);
    expect(document.activeElement).toBe(document.body);
  });

  it('runs its own handling on the fallback where C# cannot decide inside the dispatch', () => {
    const { label, control } = labelled('<div id="label">Label</div><input id="control" />');
    const { fake } = attachLabel(label, { native: false, controlId: 'control', decides: true });

    label.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));

    expect(fake.decisions).toHaveLength(0);
    expect(document.activeElement).toBe(control);
  });

  it('reports a mismatch between the element and nativeLabel only with the development checks on', () => {
    const { label } = labelled('<div id="label">Label</div><input id="control" />');
    const quiet = attachLabel(label, { native: true, developmentChecks: false });
    expect(quiet.fake.reported('OnLabelMismatch')).toHaveLength(0);
    quiet.handle.dispose();

    const checked = attachLabel(label, { native: true, developmentChecks: true });

    expect(checked.fake.reported('OnLabelMismatch')).toEqual([{ expectedNative: true }]);
  });

  it('finds the control inside the label\'s shadow root before the document', () => {
    document.body.innerHTML = '<input id="control" /><div id="host"></div>';
    const shadow = document.querySelector('#host')!.attachShadow({ mode: 'open' });
    shadow.innerHTML = '<div id="label">Label</div><input id="control" />';

    expect(getControlById(shadow.querySelector('#label')!, 'control')).toBe(shadow.querySelector('#control'));
  });

  it('discovers a parent label and gives an id-less one the generated id', () => {
    document.body.innerHTML = '<label><input id="control" /></label>';

    expect(discoverLabel(document.querySelector<HTMLInputElement>('#control')!, 'control-label')).toBe('control-label');
    expect(document.querySelector('label')!.id).toBe('control-label');
  });
});

// The submit half: the module prevents every native submit, reports it with the state of every control enrolled on the
// form, and acts on C#'s answer (re-issue through the pass flag, focus the first invalid control, or nothing).
describe('form submission', () => {
  interface SubmitRequest {
    event: { type: string; defaultPrevented: boolean };
    submitterId: string | null;
    controls: { id: string; value: string | null; validity: { valueMissing: boolean; valid: boolean } }[];
  }

  interface SubmitAnswer {
    verdict: 'submit' | 'blocked' | 'handled';
    focus?: string[];
  }

  /** A twin whose OnSubmitRequest records the request and answers with the next queued answer. */
  function createSubmitTwin(answers: SubmitAnswer[]): { twin: DotNetTwin; requests: SubmitRequest[]; settled: () => Promise<void> } {
    const requests: SubmitRequest[] = [];
    const pending: Promise<unknown>[] = [];
    const twin: DotNetTwin = {
      invokeMethod<T>(): T {
        throw new Error('The current dispatcher does not support synchronous calls from JS to .NET.');
      },
      invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
        if (methodName !== 'OnSubmitRequest') {
          return Promise.resolve(undefined as T);
        }
        requests.push(JSON.parse(args[0] as string) as SubmitRequest);
        const answer = Promise.resolve(JSON.stringify(answers.shift() ?? { verdict: 'handled' }) as T);
        pending.push(answer);
        return answer;
      },
    };
    return {
      twin,
      requests,
      async settled() {
        await Promise.all(pending);
        await Promise.resolve();
      },
    };
  }

  /** Records every submit event that reaches the end of the form's listeners, then stops the navigation jsdom lacks. */
  function recordSubmits(form: HTMLFormElement): { defaultPrevented: boolean; submitter: string | null }[] {
    const seen: { defaultPrevented: boolean; submitter: string | null }[] = [];
    form.addEventListener('submit', (event) => {
      seen.push({ defaultPrevented: event.defaultPrevented, submitter: (event as SubmitEvent).submitter?.id ?? null });
      event.preventDefault();
    });
    return seen;
  }

  function attachForm(form: HTMLFormElement, twin: DotNetTwin): TwinHandle {
    const handle = attach(form, JSON.stringify({ submit: {} }), twin);
    handles.push(handle);
    return handle;
  }

  it('always prevents the native submit and reports it', async () => {
    document.body.innerHTML = '<form id="form"><button id="send" type="submit">Send</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const { twin, requests, settled } = createSubmitTwin([{ verdict: 'handled' }]);
    attachForm(form, twin);
    const seen = recordSubmits(form);

    document.querySelector<HTMLButtonElement>('#send')!.click();
    await settled();

    expect(seen).toEqual([{ defaultPrevented: true, submitter: 'send' }]);
    expect(requests).toEqual([expect.objectContaining({ event: expect.objectContaining({ type: 'submit', defaultPrevented: true }), submitterId: 'send' })]);
  });

  it('re-issues the submission once with the same submitter through the pass flag', async () => {
    document.body.innerHTML = '<form id="form"><button id="first" type="submit">First</button><button id="second" type="submit">Second</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const { twin, requests, settled } = createSubmitTwin([{ verdict: 'submit' }]);
    attachForm(form, twin);
    const seen = recordSubmits(form);

    document.querySelector<HTMLButtonElement>('#second')!.click();
    await settled();

    expect(requests).toHaveLength(1);
    expect(seen).toEqual([
      { defaultPrevented: true, submitter: 'second' },
      { defaultPrevented: false, submitter: 'second' },
    ]);
  });

  it('intercepts a second submit started during the pass', async () => {
    document.body.innerHTML = '<form id="form"><button id="send" type="submit">Send</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const { twin, requests, settled } = createSubmitTwin([{ verdict: 'submit' }, { verdict: 'handled' }]);
    attachForm(form, twin);
    let nested = false;
    form.addEventListener('submit', () => {
      if (!nested && requests.length === 1) {
        nested = true;
        form.requestSubmit();
      }
    });
    const seen = recordSubmits(form);

    form.requestSubmit();
    await settled();
    await settled();

    expect(requests).toHaveLength(2);
    expect(seen.filter((s) => !s.defaultPrevented)).toHaveLength(1);
  });

  it('reads every control enrolled on the form into the report', async () => {
    document.body.innerHTML = '<form id="form" _bl_form-capture><input id="email" required /><button id="send" type="submit">Send</button></form><input id="portaled" />';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const email = document.querySelector<HTMLInputElement>('#email')!;
    const portaled = document.querySelector<HTMLInputElement>('#portaled')!;
    attachControl(email, { formId: 'form-capture' });
    attachControl(portaled, { formId: 'form-capture' });
    portaled.value = 'written by a script';
    const { twin, requests, settled } = createSubmitTwin([{ verdict: 'handled' }]);
    attachForm(form, twin);
    recordSubmits(form);

    document.querySelector<HTMLButtonElement>('#send')!.click();
    await settled();

    expect(requests[0]!.controls).toEqual([
      expect.objectContaining({ id: 'email', value: '', validity: expect.objectContaining({ valueMissing: true, valid: false }) }),
      expect.objectContaining({ id: 'portaled', value: 'written by a script' }),
    ]);
  });

  it('compares the form owner with the form found by its capture id', () => {
    vi.useFakeTimers();
    document.body.innerHTML = '<form _bl_capture-id><input id="control" /></form>';
    const element = document.querySelector<HTMLInputElement>('#control')!;
    const { fake } = attachControl(element, { formId: 'capture-id' });

    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));

    expect(fake.reported<FormControlReport>('OnControlReport')).toEqual([expect.objectContaining({ type: 'keydown', sameForm: true })]);
  });

  it('focuses the first invalid control by document order and selects an input\'s text', async () => {
    document.body.innerHTML = '<form id="form"><input id="first" value="one" /><input id="second" value="two" /><button id="send" type="submit">Send</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const select = vi.spyOn(HTMLInputElement.prototype, 'select');
    const { twin, settled } = createSubmitTwin([{ verdict: 'blocked', focus: ['second', 'first'] }]);
    attachForm(form, twin);
    recordSubmits(form);

    document.querySelector<HTMLButtonElement>('#send')!.click();
    await settled();

    expect(document.activeElement).toBe(document.querySelector('#first'));
    expect(select).toHaveBeenCalledTimes(1);
    select.mockRestore();
  });

  it('keeps registration order for controls in disconnected trees', async () => {
    document.body.innerHTML = '<form id="form"><div id="host"></div><input id="light" /><button id="send" type="submit">Send</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const shadow = document.querySelector('#host')!.attachShadow({ mode: 'open' });
    shadow.innerHTML = '<input id="shadowed" />';
    const { twin, settled } = createSubmitTwin([{ verdict: 'blocked', focus: ['light', 'shadowed'] }]);
    attachForm(form, twin);
    recordSubmits(form);

    document.querySelector<HTMLButtonElement>('#send')!.click();
    await settled();

    expect(document.activeElement).toBe(document.querySelector('#light'));
  });

  it('calls select() only on an input', async () => {
    document.body.innerHTML = '<form id="form"><textarea id="notes">text</textarea><button id="send" type="submit">Send</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const select = vi.spyOn(HTMLTextAreaElement.prototype, 'select');
    const { twin, settled } = createSubmitTwin([{ verdict: 'blocked', focus: ['notes'] }]);
    attachForm(form, twin);
    recordSubmits(form);

    document.querySelector<HTMLButtonElement>('#send')!.click();
    await settled();

    expect(document.activeElement).toBe(document.querySelector('#notes'));
    expect(select).not.toHaveBeenCalled();
    select.mockRestore();
  });

  it('does nothing more when C# answers that it handled the submission', async () => {
    document.body.innerHTML = '<form id="form"><input id="field" /><button id="send" type="submit">Send</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const { twin, settled } = createSubmitTwin([{ verdict: 'handled' }]);
    attachForm(form, twin);
    const seen = recordSubmits(form);

    document.querySelector<HTMLButtonElement>('#send')!.click();
    await settled();

    expect(seen).toEqual([{ defaultPrevented: true, submitter: 'send' }]);
    expect(document.activeElement).toBe(document.body);
  });

  it('focuses the first invalid control when an update asks for it after a passed submission', () => {
    document.body.innerHTML = '<form id="form"><input id="first" /><input id="second" /></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const { twin } = createSubmitTwin([]);
    const handle = attachForm(form, twin);

    handle.update(JSON.stringify({ submit: { focusRevision: 0, focus: ['second'] } }));
    expect(document.activeElement).toBe(document.body);

    handle.update(JSON.stringify({ submit: { focusRevision: 1, focus: ['second'] } }));
    expect(document.activeElement).toBe(document.querySelector('#second'));
  });

  it('stops intercepting after dispose', async () => {
    document.body.innerHTML = '<form id="form"><button id="send" type="submit">Send</button></form>';
    const form = document.querySelector<HTMLFormElement>('#form')!;
    const { twin, requests } = createSubmitTwin([]);
    const handle = attachForm(form, twin);
    const seen = recordSubmits(form);

    handle.dispose();
    document.querySelector<HTMLButtonElement>('#send')!.click();

    expect(requests).toHaveLength(0);
    expect(seen).toEqual([{ defaultPrevented: false, submitter: 'send' }]);
  });
});
