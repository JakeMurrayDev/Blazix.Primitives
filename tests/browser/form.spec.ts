import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, Fixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/form.md that render only Form and Field parts (PR 2 of M2, issue 78), those that
// render Field.Error (PR 1c), and the relaxation of FRM-EVT-01 asserted on both render modes: the form module prevents
// every native submit and re-issues it with requestSubmit(submitter) once C# answers. Titles are Base UI's
// (Form.test.tsx) except the port's own, which name their row. A submission is observed through a submit listener the spec
// adds to the form, after the module's: it records the submitter, defaultPrevented and the native form data of every
// submit event and stops the navigation of a re-issued one itself.

type FixtureName = 'form-submit' | 'form-blocked' | 'form-native-validation' | 'form-external-errors' | 'form-errors' | 'form-registration';

interface RecordedSubmit {
  prevented: boolean;
  submitter: string | null;
}

interface RecordedFormData {
  prevented: boolean;
  entries: [string, string][];
}

/** Opens a Form fixture and waits until every form and control has the form module attached. */
async function gotoForm(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
  await page.goto(fixtureUrl(mode, name), { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** A barrier: clicks the fixture's sync button and waits for C# to count it (one circuit's messages run in order). */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

/** Adds the recording submit listener to a form; returns a reader of what it recorded. */
async function recordSubmits(form: Locator): Promise<() => Promise<RecordedSubmit[]>> {
  await form.evaluate((element) => {
    const records: RecordedSubmit[] = [];
    (element as HTMLElement & { recordedSubmits?: RecordedSubmit[] }).recordedSubmits = records;
    element.addEventListener('submit', (event) => {
      records.push({ prevented: event.defaultPrevented, submitter: (event as SubmitEvent).submitter?.id ?? null });
      event.preventDefault();
    });
  });
  return () => form.evaluate((element) => [...((element as HTMLElement & { recordedSubmits?: RecordedSubmit[] }).recordedSubmits ?? [])]);
}

/** Adds a submit listener that records the native form data of every submit event (FRM-FRM-08); returns its reader. */
async function recordFormData(form: Locator): Promise<() => Promise<RecordedFormData[]>> {
  await form.evaluate((element) => {
    const records: RecordedFormData[] = [];
    (element as HTMLElement & { recordedFormData?: RecordedFormData[] }).recordedFormData = records;
    element.addEventListener('submit', (event) => {
      const entries = [...new FormData(element as HTMLFormElement).entries()].map(([key, value]) => [key, String(value)] as [string, string]);
      records.push({ prevented: event.defaultPrevented, entries });
      event.preventDefault();
    });
  });
  return () => form.evaluate((element) => [...((element as HTMLElement & { recordedFormData?: RecordedFormData[] }).recordedFormData ?? [])]);
}

test.describe('Form', () => {
  // FRM-EVT-01, FRM-EVT-02 (§9: the native submit is always prevented, on both modes): the module prevents the submit,
  // C# validates and runs OnSubmit, then the module re-issues the submission once with the same submitter.
  test('prevents the native submit and re-issues it with the same submitter once C# answers', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-submit');
    const scope = fixture.locator('[data-case="onsubmit"]');
    const recorded = await recordSubmits(scope.locator('[data-testid="form"]'));

    await scope.locator('#second-submitter').click();

    await expect(scope).toHaveAttribute('data-submits', '1');
    await expect.poll(recorded).toEqual([
      { prevented: true, submitter: 'second-submitter' },
      { prevented: false, submitter: 'second-submitter' },
    ]);
  });

  test('does not re-issue the submission when OnSubmit calls PreventNativeSubmit', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-submit');
    const scope = fixture.locator('[data-case="vetoed"]');
    const recorded = await recordSubmits(scope.locator('[data-testid="form"]'));

    await scope.locator('#vetoed-submitter').click();
    await expect(scope).toHaveAttribute('data-submits', '1');
    await settle(fixture);

    expect(await recorded()).toEqual([{ prevented: true, submitter: 'vetoed-submitter' }]);
  });

  // FRM-API-03, FRM-EVT-03: OnFormSubmit receives the values, and the browser never submits.
  test('passes the values to OnFormSubmit and never lets the browser submit', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-submit');
    const scope = fixture.locator('[data-case="form-submit"]');
    const recorded = await recordSubmits(scope.locator('[data-testid="form"]'));

    await scope.locator('#form-submitter').click();
    await expect(scope).toHaveAttribute('data-values', 'username=alice132');
    await settle(fixture);

    expect(await recorded()).toEqual([{ prevented: true, submitter: 'form-submitter' }]);
  });

  // FRM-FOC-01 (§9: the focus lands when C# answers, on both modes), FRM-FRM-01, FRM-EVT-05.
  test('blocks submit and focuses the first invalid field across custom and native validation', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-blocked');
    const scope = fixture.locator('[data-case="custom-native"]');
    await page.evaluate(() => {
      const select = HTMLInputElement.prototype.select;
      (window as Window & { selectCalls?: number }).selectCalls = 0;
      HTMLInputElement.prototype.select = function (this: HTMLInputElement) {
        (window as Window & { selectCalls?: number }).selectCalls! += 1;
        select.call(this);
      };
    });

    await scope.getByRole('button', { name: 'Submit' }).click();

    await expect(scope.locator('[data-testid="custom"]')).toBeFocused();
    expect(await page.evaluate(() => (window as Window & { selectCalls?: number }).selectCalls)).toBe(1);
    await settle(fixture);
    await expect(scope).toHaveAttribute('data-form-submits', '0');
    await expect(scope.locator('[data-testid="native"]')).toHaveAttribute('aria-invalid', 'true');
  });

  // FRM-FOC-01's document order (the port's title; Base UI's renders Checkbox): keyed fields reversed keep their
  // registrations, so registration order and document order differ.
  test('focuses the first invalid control by document order after the fields are reordered', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-blocked');
    const scope = fixture.locator('[data-case="reordered"]');

    await scope.locator('[data-reverse]').click();
    await expect(scope.locator('input').first()).toHaveAttribute('data-testid', 'second');
    await scope.getByRole('button', { name: 'Submit' }).click();

    await expect(scope.locator('[data-testid="second"]')).toBeFocused();
  });

  // FRM-API-06, FRM-DOM-01: with NoValidate false the browser validates first, and no submit event reaches the form.
  test('lets the browser validate first when NoValidate is false', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-native-validation');
    const form = fixture.locator('[data-testid="form"]');
    const recorded = await recordSubmits(form);
    await expect(form).not.toHaveAttribute('novalidate');

    await fixture.getByRole('button', { name: 'Submit' }).click();
    await settle(fixture);

    expect(await recorded()).toEqual([]);
    await expect(fixture).toHaveAttribute('data-submits', '0');
    expect(await fixture.locator('[data-testid="control"]').evaluate((input) => (input as HTMLInputElement).validity.valueMissing)).toBe(true);
    await expect(fixture.locator('[data-testid="control"]')).toBeFocused();
  });

  // FRM-FOC-02, FFI-FOC-03 (the port's title; Base UI's render Field.Error and land with PR 1c): after a passed
  // submission, the first change of the errors focuses the first invalid field, once, after the render.
  test('focuses the first invalid field when errors arrive after a passed submission', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-external-errors');
    await recordSubmits(fixture.locator('[data-testid="form"]'));

    await fixture.getByRole('button', { name: 'Submit' }).click();
    await expect(fixture).toHaveAttribute('data-form-submits', '1');
    await fixture.locator('[data-set-errors]').click();

    await expect(fixture.locator('[data-testid="second"]')).toBeFocused();
    await expect(fixture.locator('[data-testid="second"]')).toHaveAttribute('aria-invalid', 'true');
  });

  // FRM-KEY-01: a click on a button with no type submits through the same pipeline.
  test('does not submit if there are errors', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-errors');
    const scope = fixture.locator('[data-case="required"]');

    await scope.getByRole('button', { name: 'Submit' }).click();

    await expect(scope.locator('[data-testid="error"]')).toBeVisible();
    await settle(fixture);
    await expect(scope).toHaveAttribute('data-submits', '0');
  });

  // FRM-FOC-02, FFI-FOC-03: errors set after a passed submission focus the first invalid field once, after the render.
  test('focuses asynchronously replaced external errors and clears only the changed own property', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-errors');
    const scope = fixture.locator('[data-case="async-replaced"]');

    await scope.getByRole('button', { name: 'Submit' }).click();

    await expect(scope.locator('[data-testid="first"]')).toBeFocused();
    await expect(scope.locator('[data-testid="first-error"]')).toHaveText('First error');
    await expect(scope.locator('[data-testid="second-error"]')).toHaveText('Second error');

    await scope.locator('[data-testid="first"]').pressSequentially('a');

    await expect(scope.locator('[data-testid="first-error"]')).toHaveCount(0);
    await expect(scope.locator('[data-testid="second-error"]')).toHaveText('Second error');
  });

  // FRM-FOC-02 (and FRM-FOC-01 for the blocked submission); FRM-FRM-08: the native form data holds the controls' entries.
  test('focuses the first invalid field only on submit', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-errors');
    const scope = fixture.locator('[data-case="app"]');
    const formData = await recordFormData(scope.locator('[data-testid="form"]'));
    const submit = scope.getByRole('button', { name: 'Submit' });
    const name = scope.locator('[data-testid="name"]');
    const age = scope.locator('[data-testid="age"]');

    await submit.click();
    await expect(name).toBeFocused();
    expect((await formData())[0]?.entries).toEqual([['name', ''], ['age', '']]);

    await name.fill('John');
    await settle(fixture);
    await expect(age).not.toBeFocused();

    await submit.click();
    await expect(age).toBeFocused();

    await age.fill('42');
    await submit.click();
    await settle(fixture);
    await expect(scope.locator('[data-testid="age-error"]')).toHaveCount(0);
    await expect(age).not.toBeFocused();
  });

  test('does not swap focus immediately on change after two submissions', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-errors');
    const scope = fixture.locator('[data-case="app"]');
    await recordFormData(scope.locator('[data-testid="form"]'));
    const submit = scope.getByRole('button', { name: 'Submit' });
    const name = scope.locator('[data-testid="name"]');
    const age = scope.locator('[data-testid="age"]');

    await submit.click();
    await expect(name).toBeFocused();

    await submit.click();
    await settle(fixture);
    await name.fill('John');
    await settle(fixture);

    await expect(age).not.toBeFocused();
  });

  // FRM-FRM-06, FRM-STA-03; FRM-FRM-08: the submission's native form data is the typed value.
  test('runs field validation on first change after Form error is set', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-errors');
    const scope = fixture.locator('[data-case="first-change"]');
    const formData = await recordFormData(scope.locator('[data-testid="form"]'));
    const input = scope.locator('[data-testid="name"]');

    await input.click();
    await page.keyboard.type('abcde');
    await scope.getByRole('button', { name: 'Submit' }).click();
    await expect(scope.locator('[data-testid="name-error"]')).toHaveText('submit error');
    expect((await formData())[0]?.entries).toEqual([['name', 'abcde']]);
    const before = Number(await scope.getAttribute('data-calls'));

    await input.click();
    await page.keyboard.press('Backspace');

    await expect(scope.locator('[data-testid="name-error"]')).toHaveText('field error');
    await expect(scope).toHaveAttribute('data-calls', String(before + 1));
  });

  // FFI-FRM-17: the registry follows mounting, a fieldset's disabled state and the control's.
  test('unmounted fields should be removed from the form', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-registration');
    const scope = fixture.locator('[data-case="unmounted"]');
    const submit = scope.getByRole('button', { name: 'Submit' });

    await submit.click();
    await expect(scope.locator('[data-testid="email"]')).toHaveAttribute('aria-invalid', 'true');
    await settle(fixture);
    await expect(scope).toHaveAttribute('data-submits', '0');

    await scope.getByRole('checkbox').click();
    await submit.click();
    await expect(scope).toHaveAttribute('data-submits', '1');
  });

  test('excludes disabled fieldset fields from validation and onFormSubmit values', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-registration');
    const scope = fixture.locator('[data-case="fieldset"]');

    await scope.getByRole('button', { name: 'Submit' }).click();

    await expect(scope).toHaveAttribute('data-submits', '1');
    await expect(scope).toHaveAttribute('data-values', 'enabled=sent');
    await expect(scope.locator('[data-testid="disabled"]')).not.toHaveAttribute('aria-invalid');
  });

  test('clears invalid attributes when a field control becomes disabled', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-registration');
    const scope = fixture.locator('[data-case="control-disabled"]');
    const control = scope.locator('[data-testid="control"]');

    await scope.getByRole('button', { name: 'Submit' }).click();
    await expect(control).toHaveAttribute('aria-invalid', 'true');

    await scope.getByRole('button', { name: 'Disable' }).click();
    await expect(control).toBeDisabled();
    await expect(control).not.toHaveAttribute('aria-invalid');

    await scope.getByRole('button', { name: 'Submit' }).click();
    await expect(scope).toHaveAttribute('data-submits', '1');
    await expect(scope).toHaveAttribute('data-values', '');
  });

  test('re-registers field controls when they become enabled again', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'form-registration');
    const scope = fixture.locator('[data-case="control-disabled"]');
    const control = scope.locator('[data-testid="control"]');
    const submit = scope.getByRole('button', { name: 'Submit' });

    await submit.click();
    await expect(control).toHaveAttribute('aria-invalid', 'true');
    await settle(fixture);
    await expect(scope).toHaveAttribute('data-submits', '0');

    await scope.getByRole('button', { name: 'Disable' }).click();
    await submit.click();
    await expect(scope).toHaveAttribute('data-submits', '1');
    await expect(scope).toHaveAttribute('data-values', '');

    await scope.getByRole('button', { name: 'Enable' }).click();
    await submit.click();
    await expect(control).toHaveAttribute('aria-invalid', 'true');
    // FRM-FOC-01 (§9): the blocked submission focuses and selects the control when C# answers; typing waits for it.
    await expect(control).toBeFocused();
    await settle(fixture);
    await expect(scope).toHaveAttribute('data-submits', '1');

    await control.pressSequentially('sent');
    await submit.click();
    await expect(scope).toHaveAttribute('data-submits', '2');
    await expect(scope).toHaveAttribute('data-values', 'control=sent');
  });
});
