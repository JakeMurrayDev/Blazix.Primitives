import { type Locator } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

// Batch C of 6b's L4 titles (issue 277; NUM-API-06, NUM-API-15, NUM-API-17, NUM-FRM-02, NUM-FRM-03, NUM-FRM-08,
// NUM-FRM-12, NUM-DOM-01, NUM-DOM-05, NUM-DOM-11): NumberFieldRoot.test.tsx's titles whose assertion is native (constraint
// validation of the hidden input, a native form submission, the `form` attribute) on the `number-field-input` fixture's `c-`
// cases and the `number-field-form` fixture's `c-` cases, Server then WebAssembly. Base UI's fireEvent.change is Playwright's
// fill (the value set, then an input event, no keydown), its fireEvent.blur the locator's blur, its click a real click. A
// submission is witnessed by a capture listener on the form that reads its FormData at the submit event.

async function gotoInput(page: Page, mode: RenderMode, inputCase: string) {
  await page.goto(`/${mode}/number-field-input?case=${inputCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-input'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  const input = fixture.getByRole('textbox');
  return { fixture, input, hidden: fixture.locator('input[type="number"][name="quantity"]') };
}

async function gotoForm(page: Page, mode: RenderMode, formCase: string): Promise<Locator> {
  await page.goto(`/${mode}/number-field-form?case=${formCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-form'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** Appends `[value]` of the form's `name` entry to the form's data-submits at each submit event that fires. */
async function recordSubmits(form: Locator, name: string): Promise<void> {
  await form.evaluate((element, entry) => {
    element.addEventListener(
      'submit',
      () => {
        const value = String(new FormData(element as HTMLFormElement).get(entry));
        element.setAttribute('data-submits', `${element.getAttribute('data-submits') ?? ''}[${value}]`);
      },
      { capture: true },
    );
  }, name);
}

async function validity(hidden: Locator) {
  return hidden.evaluate((element: HTMLInputElement) => ({
    stepMismatch: element.validity.stepMismatch,
    formValid: element.form!.checkValidity(),
  }));
}

test('blocks submission when step mismatch occurs with default step', async ({ page, mode }) => {
  const { fixture, input, hidden } = await gotoInput(page, mode, 'c-step-default');

  await input.fill('0.11');
  await expect(fixture).toHaveAttribute('data-changes', '0.11/input-change');

  await expect(hidden).toHaveValue('0.11');
  expect(await validity(hidden)).toMatchObject({ stepMismatch: true, formValid: false });
});

// Base UI runs this title under jsdom only, because Chromium's validation bubble blocked its browser run; Playwright's click
// is not blocked by the bubble, so the port runs it here once, in the browser, on both render modes. The second submission's
// entry alone in data-submits proves the first click submitted nothing.
test('prevents form submission when the value does not match the step', async ({ page, mode }) => {
  const { fixture, input, hidden } = await gotoInput(page, mode, 'c-step-submit');
  const form = fixture.getByTestId('form');
  await recordSubmits(form, 'quantity');

  await input.focus();

  await input.fill('0.11');
  await expect(hidden).toHaveValue('0.11');
  await fixture.getByText('submit').click();

  await input.fill('0.1');
  await expect(hidden).toHaveValue('0.1');
  await fixture.getByText('submit').click();

  await expect(form).toHaveAttribute('data-submits', '[0.1]');
});

test.describe('Form', () => {
  // Open (design point, batch C of 6b): the hidden input of a root with no `min` is stepMismatch at 54.5 in the port, so the
  // browser blocks the submission. Base UI's React input syncs the `value` content attribute, which anchors the step grid
  // at the value when there is no `min` (react issue 12334, NumberFieldRoot.tsx:530-532); Blazor sets only the `value`
  // property, so the grid is anchored at 0 and the default step of 1 rejects 54.5.
  const externalFormOpen = 'NUM-API-17, NUM-FRM-02, NUM-FRM-03: without `min`, the hidden input\'s step grid is anchored at 0, not at the value as React\'s synced `value` attribute anchors it, so 54.5 is a step mismatch';

  test('submits to an external form when `form` is provided', async ({ page, mode }) => {
    test.info().annotations.push({ type: 'open', description: externalFormOpen });
    test.fixme(true, externalFormOpen);
    const fixture = await gotoForm(page, mode, 'c-external');
    const form = fixture.locator('#external-form');
    await recordSubmits(form, 'test');

    await fixture.getByText('Submit').click();

    await expect(form).toHaveAttribute('data-submits', '[54.5]');
  });

  test('triggers native HTML validation on submit', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'c-required');
    const submit = fixture.getByText('Submit');

    await expect(fixture.getByTestId('error')).toHaveCount(0);

    await submit.click();

    await expect(fixture.getByTestId('error')).toHaveText(['required']);
  });
});

test.describe('prop: validationMode', () => {
  // Each step waits for the hidden input to carry the typed value (C#'s answer, a leg after Base UI's synchronous change)
  // before it reads the field's state; an error is matched as the only element with the test id, so an exiting error counts.
  test('onSubmit', async ({ page, mode }) => {
    const fixture = await gotoForm(page, mode, 'c-on-submit');
    const input = fixture.getByTestId('input');
    const root = fixture.getByTestId('root');
    const hidden = fixture.locator('input[type=number]');
    const error = fixture.getByTestId('error');
    await expect(input).not.toHaveAttribute('aria-invalid');

    await input.fill('1');
    await input.blur();
    await expect(hidden).toHaveValue('1');
    await expect(input).not.toHaveAttribute('aria-invalid');
    await expect(error).toHaveCount(0);

    await input.fill('');
    await input.blur();
    await expect(hidden).toHaveValue('');
    await expect(input).not.toHaveAttribute('aria-invalid');
    await expect(error).toHaveCount(0);

    await fixture.getByText('submit').click();
    await expect(input).toHaveAttribute('aria-invalid', 'true');
    await expect(error).toHaveText(['valueMissing error']);
    await expect(root).toHaveAttribute('data-invalid');
    await expect(input).toHaveAttribute('data-invalid');

    await input.fill('2');
    await expect(hidden).toHaveValue('2');
    await expect(input).not.toHaveAttribute('aria-invalid');
    await expect(error).toHaveCount(0);
    await expect(root).not.toHaveAttribute('data-invalid');
    await expect(input).not.toHaveAttribute('data-invalid');
    await expect(root).toHaveAttribute('data-valid');
    await expect(input).toHaveAttribute('data-valid');
    // re-invalidate the field value
    await input.fill('1');
    await expect(input).toHaveAttribute('aria-invalid', 'true');
    await expect(error).toHaveText(['custom error']);

    await input.fill('3');
    await expect(hidden).toHaveValue('3');
    await expect(input).not.toHaveAttribute('aria-invalid');
    await expect(error).toHaveCount(0);

    await input.fill('');
    await expect(input).toHaveAttribute('aria-invalid', 'true');
    await expect(error).toHaveText(['valueMissing error']);
  });
});
