// L4 for docs/components/select.md (M5 track 9, issue 173): SelectRoot.test.tsx's `prop: disabled`, `prop: readOnly`, `prop: id`,
// `with Field.Root parent`, `Form` and `Field` describes (lines 2854-4469), Server then WebAssembly. Titles are Base UI's.
import { type Locator } from '@playwright/test';
import { expect, test } from './helpers';
import { waitForPositioned } from './helpers/positioner';
import { gotoPopupFixture, openChanges, pressControl, settle } from './suites/popupFixture';

const valueChanges = async (fixture: Locator): Promise<string[]> => ((await fixture.getAttribute('data-value-changes')) ?? '').split(' ').filter(Boolean);

/** Adds a recording submit listener to a form; it stops the navigation of every submit event, as the form spec's does. */
async function recordSubmits(form: Locator): Promise<() => Promise<{ prevented: boolean; entries: [string, string][] }[]>> {
  await form.evaluate((element) => {
    const records: { prevented: boolean; entries: [string, string][] }[] = [];
    (window as unknown as { recordedSubmits: typeof records }).recordedSubmits = records;
    element.addEventListener('submit', (event) => {
      const entries = [...new FormData(element as HTMLFormElement).entries()].map(([key, value]) => [key, String(value)] as [string, string]);
      records.push({ prevented: event.defaultPrevented, entries });
      event.preventDefault();
    });
  });
  return () => form.page().evaluate(() => [...(window as unknown as { recordedSubmits: { prevented: boolean; entries: [string, string][] }[] }).recordedSubmits]);
}

test.describe('prop: disabled', () => {
  // SEL-EDGE-04, SEL-KEY-19, SEL-PTR-21: a native `disabled` button is out of the tab order and takes no press.
  test('sets the disabled state', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { disabled: true, defaultValue: 'b' });
    const trigger = page.getByRole('combobox');
    await expect(trigger).toHaveAttribute('disabled');
    await expect(trigger).toHaveAttribute('data-disabled');

    await page.keyboard.press('Tab');
    await expect(trigger).not.toBeFocused();

    // Playwright's click waits for an enabled element; the press is a real one on the disabled button.
    await trigger.click({ force: true });
    await settle(fixture);
    expect(await openChanges(fixture)).toHaveLength(0);
  });

  // SEL-API-13 (L2); the Tab halves are the browser's, named by SelectRootStateTests.razor. Not mapped to an L4 row.
  test('updates the disabled state when the disabled prop changes', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { disabled: true, toggle: true, defaultValue: 'b' });
    const trigger = page.getByRole('combobox');
    await expect(trigger).toHaveAttribute('disabled');
    await expect(trigger).toHaveAttribute('data-disabled');

    await page.keyboard.press('Tab');
    await expect(trigger).not.toBeFocused();

    await trigger.click({ force: true });
    await settle(fixture);
    expect(await openChanges(fixture)).toHaveLength(0);

    await page.getByRole('button', { name: 'toggle' }).click();

    await expect(trigger).not.toHaveAttribute('disabled');
    await expect(trigger).not.toHaveAttribute('data-disabled');

    await page.keyboard.press('Tab');
    await expect(trigger).toBeFocused();

    await trigger.click();
    await expect.poll(() => openChanges(fixture)).toHaveLength(1);
  });
});

test.describe('prop: readOnly', () => {
  // SEL-EDGE-05: the trigger stays in the tab order.
  test('sets the readOnly state', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true, defaultValue: 'b' });
    const trigger = page.getByRole('combobox');
    await expect(trigger).toHaveAttribute('aria-readonly', 'true');
    await expect(trigger).toHaveAttribute('data-readonly');

    await page.keyboard.press('Tab');
    await expect(trigger).toBeFocused();
  });

  // SEL-EDGE-05: a read-only select opens and can be browsed.
  test('opens the popup when clicked', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true });
    await page.getByTestId('trigger').click();

    const listbox = page.getByRole('listbox');
    await expect(listbox).toHaveAttribute('aria-readonly', 'true');
    await settle(fixture);
    expect(await openChanges(fixture)).toHaveLength(1);
  });

  // SEL-EDGE-05: the listbox role moves to the list, and `aria-readonly` with it.
  test('marks the listbox as readonly when rendered with Select.List', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true, list: true });
    await page.getByTestId('trigger').click();

    await expect(page.getByRole('listbox')).toHaveAttribute('aria-readonly', 'true');
  });

  // SEL-EDGE-05, SEL-KEY-*: `it.each` of Base UI keeps its names; the keys are real key presses on the focused trigger.
  for (const { name, key } of [
    { name: 'ArrowDown', key: 'ArrowDown' },
    { name: 'Enter', key: 'Enter' },
    { name: 'Space', key: 'Space' },
  ]) {
    test(`opens the popup with ${name}`, async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true });
      await page.getByTestId('trigger').focus();

      await page.keyboard.press(key);

      await expect(page.getByRole('listbox')).toBeVisible();
      await settle(fixture);
      expect(await openChanges(fixture)).toHaveLength(1);
    });
  }

  // SEL-EDGE-05
  test('does not commit a value when an item is clicked in a popup the user opened', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true });
    await page.getByTestId('trigger').click();

    const optionB = page.getByRole('option', { name: 'b', exact: true });
    await optionB.click();
    await settle(fixture);

    expect(await valueChanges(fixture)).toHaveLength(0);
    await expect(optionB).not.toHaveAttribute('data-selected');
    await expect(page.getByTestId('value')).toHaveText('');
  });

  // SEL-EDGE-05, SEL-KEY-08, SEL-KEY-18: ArrowDown on the focused trigger opens the popup and highlights the first item; the
  // Enter is pressed once the highlight shows (Base UI's user-event keystrokes are synchronous with React's focus move).
  test('does not commit a value with Enter on a highlighted item', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true });
    await page.getByTestId('trigger').focus();

    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(page.getByRole('option', { name: 'a', exact: true })).toHaveAttribute('data-highlighted');
    await page.keyboard.press('Enter');
    await settle(fixture);

    expect(await valueChanges(fixture)).toHaveLength(0);
    await expect(page.getByTestId('value')).toHaveText('');
  });

  // SEL-EDGE-05, SEL-KEY-04
  test('does not commit a value with typeahead on a closed trigger', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true, fruit: true });
    await page.getByTestId('trigger').focus();

    await page.keyboard.press('b');
    await settle(fixture);

    expect(await valueChanges(fixture)).toHaveLength(0);
    await expect(page.getByTestId('value')).toHaveText('');
  });

  // SEL-EDGE-05, SEL-KEY-12, SEL-KEY-18: Base UI runs this title in a real browser only (`it.skipIf(isJSDOM)`).
  test('moves the highlight with typeahead while the popup is open', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { readOnly: true, fruit: true, defaultOpen: true });
    const apple = page.getByRole('option', { name: 'apple' });
    const banana = page.getByRole('option', { name: 'banana' });
    await expect(apple).toBeVisible();

    await apple.focus();
    await page.keyboard.press('b');

    await expect(banana).toHaveAttribute('data-highlighted');
    await settle(fixture);
    expect(await valueChanges(fixture)).toHaveLength(0);
    await expect(page.getByTestId('value')).toHaveText('');
  });
});

test.describe('Form', () => {
  // SEL-FRM-13: Base UI runs this title in a real browser only (`it.skipIf(isJSDOM)`). The click is Playwright's on the native
  // Submit button; a submit listener the spec adds reads the FormData the browser builds from the `form` attribute.
  test('submits to an external form when `form` is provided', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-external');
    const submitted = await recordSubmits(page.getByTestId('external-form'));

    await page.getByRole('button', { name: 'Submit' }).click();

    await expect.poll(async () => (await submitted()).length).toBe(1);
    expect((await submitted())[0]!.entries.filter(([key]) => key === 'country')).toEqual([['country', 'US']]);
  });

  // SEL-FRM-13: Base UI runs this title in a real browser only (`it.skipIf(isJSDOM)`).
  test('submits multiple values to an external form when `form` is provided', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-external', { multiple: true });
    const submitted = await recordSubmits(page.getByTestId('external-form'));

    await page.getByRole('button', { name: 'Submit' }).click();

    await expect.poll(async () => (await submitted()).length).toBe(1);
    expect((await submitted())[0]!.entries.filter(([key]) => key === 'countries')).toEqual([
      ['countries', 'US'],
      ['countries', 'CA'],
    ]);
  });

  // SEL-FRM-08: the hidden input reports valueMissing, so the submission is blocked, the error shows, the trigger is invalid and
  // focused. The submit listener records every submit event of the form: only the module's prevented one, no re-issued one.
  test('triggers native HTML validation on submit', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-submit', { case: 'required' });
    const submitted = await recordSubmits(fixture.locator('form'));
    const trigger = page.getByTestId('trigger');

    await expect(page.getByTestId('error')).toHaveCount(0);

    await page.getByTestId('submit').click();

    await expect(page.getByTestId('error')).toHaveText('required');
    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
    await expect(trigger).toBeFocused();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-form-submits', '0');
    expect((await submitted()).map((record) => record.prevented)).toEqual([true]);
  });

  // SEL-FRM-01, SEL-FRM-06, SEL-FRM-08: the value is chosen with the pointer once the invalid submission put focus on the trigger.
  test('revalidates immediately after form submission errors', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-submit', { case: 'required' });
    const trigger = page.getByTestId('trigger');

    await page.getByTestId('submit').click();

    await expect(page.getByTestId('error')).toHaveText('required');
    await expect(trigger).toHaveAttribute('aria-invalid', 'true');

    await trigger.click();
    await page.getByRole('option', { name: 'b', exact: true }).click();

    await expect(page.getByTestId('error')).toHaveCount(0);
    await expect(trigger).not.toHaveAttribute('aria-invalid');
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-form-submits', '0');
  });
});

test.describe('Field', () => {
  // SEL-FRM-03: blur with no related target outside the positioner marks the field touched.
  test('[data-touched]', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-field');
    const trigger = page.getByTestId('trigger');

    await expect(trigger).not.toHaveAttribute('data-dirty');

    await trigger.focus();
    await trigger.blur();

    await expect(trigger).toHaveAttribute('data-touched', '');
  });

  // SEL-FOC-10
  test('[data-focused]', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-field');
    const trigger = page.getByTestId('trigger');

    await expect(trigger).not.toHaveAttribute('data-focused');

    await trigger.focus();

    await expect(trigger).toHaveAttribute('data-focused', '');

    await trigger.blur();

    await expect(trigger).not.toHaveAttribute('data-focused');
  });

  test.describe('[data-focused] without a blur event', () => {
    // SEL-FOC-10, SEL-FRM-20: the disable press is a Shift keydown on a control that moves no focus, so the trigger keeps focus
    // and no blur event arrives, as with Base UI's `setProps`.
    test('is removed when the focused trigger becomes disabled', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-form-field', { case: 'focus-removal' });
      const trigger = page.getByTestId('first');
      const field = page.getByTestId('field');

      await trigger.focus();

      await expect(field).toHaveAttribute('data-focused', '');

      await pressControl(fixture.locator('[data-set-disabled]'));

      await expect(field).not.toHaveAttribute('data-focused');
      await expect(trigger).not.toHaveAttribute('data-focused');
    });

    // SEL-FOC-10, SEL-FRM-20
    test('is removed when the focused trigger unmounts', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-form-field', { case: 'focus-removal' });
      const field = page.getByTestId('field');

      await page.getByTestId('first').focus();

      await expect(field).toHaveAttribute('data-focused', '');

      await pressControl(fixture.locator('[data-unmount]'));

      await expect(field).not.toHaveAttribute('data-focused');
    });
  });

  // SEL-FOC-10, SEL-FRM-03: Base UI synthesizes `blur` (relatedTarget: listbox) and `focus`; here the trigger is clicked, so the
  // browser moves focus from the trigger into the popup and the module reports the blur with the popup as its target.
  test('does not mark as touched when focus moves into the popup', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-validation');
    const trigger = page.getByTestId('trigger');

    await trigger.focus();
    await trigger.click();

    await expect(page.getByRole('listbox')).toBeFocused();
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-validate-calls', '0');
    await expect(trigger).toHaveAttribute('data-focused', '');
    await expect(trigger).not.toHaveAttribute('data-touched');
    await expect(trigger).not.toHaveAttribute('aria-invalid');
  });

  // SEL-DIS-03, SEL-FOC-10, SEL-FOC-12, SEL-FRM-03, SEL-FRM-07: Base UI synthesizes the popup's `blur` (relatedTarget: outside) and the
  // outside element's `focus`; here a real Tab press moves focus from the popup to the Outside button that follows the field.
  test('validates when the popup is blurred', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-validation');
    const trigger = page.getByTestId('trigger');

    await trigger.focus();
    await trigger.click();
    await expect(page.getByRole('listbox')).toBeFocused();

    await page.keyboard.press('Tab');

    await expect(fixture).toHaveAttribute('data-validate-calls', '1');
    await expect(trigger).toHaveAttribute('data-touched', '');
    await expect(trigger).not.toHaveAttribute('data-focused');
    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
  });

  // SEL-FRM-07, SEL-FRM-08, SEL-KEY-08, SEL-KEY-17, SEL-ARIA-07, SEL-FRM-17
  test('prop: validate', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-validation');
    const trigger = page.getByTestId('trigger');

    await expect(trigger).not.toHaveAttribute('aria-invalid');

    await trigger.focus();
    await trigger.blur();

    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
  });

  // SEL-FRM-07: the validator receives the committed item (the very instance), not its `itemToStringValue` text.
  test('passes raw value to validate when itemToStringValue is provided', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-validation', { case: 'raw' });
    const trigger = page.getByTestId('trigger');

    await trigger.focus();
    await trigger.blur();

    await expect(fixture).toHaveAttribute('data-validate-calls', '1');
    await expect(fixture).toHaveAttribute('data-raw-same', 'true');
    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
  });

  // SEL-FRM-08, SEL-KEY-08, SEL-KEY-17: ArrowDown and Enter are real key presses. Focus lands on the trigger when the invalid
  // submission has been answered, so each key waits for the state it acts on.
  test('prop: validateMode=onSubmit', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-submit', { case: 'validate' });
    const trigger = page.getByRole('combobox');
    const listbox = page.getByRole('listbox');
    await expect(trigger).not.toHaveAttribute('aria-invalid');

    await page.getByText('Submit').click();
    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
    await expect(trigger).toBeFocused();

    // Arrow Down to focus Option 1 (valid). Enter acts on the focused element, so it waits for the focus, not only the
    // highlight: on Server the highlight can show a moment before focus reaches the item, and an Enter on the trigger closes.
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('option', { name: 'Option 1' })).toHaveAttribute('data-highlighted');
    await expect(page.getByRole('option', { name: 'Option 1' })).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(trigger).not.toHaveAttribute('aria-invalid');
    await expect(listbox).toHaveCount(0);

    // The popup opens on the selected item (Option 1), which takes the highlight and the focus.
    await trigger.click();
    await expect(page.getByRole('option', { name: 'Option 1' })).toBeFocused();
    // Arrow Down to focus Option 2 (invalid)
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('option', { name: 'Option 2' })).toHaveAttribute('data-highlighted');
    await page.keyboard.press('Enter');
    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
    await expect(listbox).toHaveCount(0);

    await trigger.click();
    await expect(page.getByRole('option', { name: 'Option 2' })).toBeFocused();
    // Arrow Up to focus Option 1 (valid)
    await page.keyboard.press('ArrowUp');
    await expect(page.getByRole('option', { name: 'Option 1' })).toHaveAttribute('data-highlighted');
    await page.keyboard.press('Enter');
    await expect(trigger).not.toHaveAttribute('aria-invalid');
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-form-submits', '0');
  });

  // SEL-FRM-06 (L2 in the map; Base UI runs it in jsdom only, "flaky in real browser"); ported for the real browser as the brief's
  // scope names the validation modes.
  test('prop: validationMode=onChange', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-validation', { case: 'onchange' });
    const trigger = page.getByTestId('trigger');

    await expect(trigger).not.toHaveAttribute('aria-invalid');

    await trigger.click();
    await expect(page.getByRole('listbox')).toBeFocused();

    // Arrow Down to focus the Option 1
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('option', { name: 'Option 1' })).toHaveAttribute('data-highlighted');
    await page.keyboard.press('Enter');

    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
  });

  // SEL-FRM-07: Base UI runs it in jsdom only ("flaky in real browser"). `fireEvent.blur(trigger)` is `locator.blur()`.
  test('prop: validationMode=onBlur', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-validation', { one: true });
    const trigger = page.getByTestId('trigger');

    await expect(trigger).not.toHaveAttribute('aria-invalid');

    await trigger.click();
    await expect(page.getByRole('listbox')).toBeFocused();

    // Arrow Down to focus the Option 1
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('option', { name: 'Option 1' })).toHaveAttribute('data-highlighted');
    await page.keyboard.press('Enter');
    await expect(trigger).toBeFocused();

    await trigger.blur();

    await expect(trigger).toHaveAttribute('aria-invalid', 'true');
  });

  // SEL-API-26, SEL-ARIA-05, SEL-FRM-14, SEL-PTR-24
  test('Field.Label', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-label', { case: 'field-label-non-native' });

    const labelId = await page.getByTestId('label').getAttribute('id');
    expect(labelId).toBeTruthy();
    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-labelledby', labelId!);
  });

  // SEL-API-10, SEL-ARIA-05, SEL-DOM-14, SEL-DOM-15, SEL-FRM-15, SEL-PTR-24
  test('Select.Label', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-label', { case: 'select-label' });

    const labelId = await page.getByTestId('label').getAttribute('id');
    expect(labelId).toBeTruthy();
    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-labelledby', labelId!);
  });

  // SEL-DOM-15, SEL-PTR-24: the label is a non-native label: a real click on it focuses the trigger and opens nothing.
  test('Select.Label focuses trigger without opening', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-form-label', { case: 'select-label-click' });

    await page.getByTestId('label').click();

    await expect(page.getByTestId('trigger')).toBeFocused();
    await expect(page.getByRole('listbox')).toHaveCount(0);
    await settle(fixture);
    expect(await openChanges(fixture)).toHaveLength(0);
    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'false');
  });

  // SEL-FRM-14, SEL-PTR-24: the native label's click is forwarded by the browser to the trigger button, which opens the popup
  // and moves focus to the listbox.
  test('Field.Label links to trigger and focuses it', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-label', { case: 'field-label' });

    const label = page.getByTestId('label');
    const trigger = page.getByTestId('trigger');
    const triggerId = await trigger.getAttribute('id');
    expect(triggerId).toBeTruthy();
    await expect(label).toHaveAttribute('for', triggerId!);
    expect(await label.evaluate((element) => (element as HTMLLabelElement).htmlFor)).toBe(triggerId);

    await label.click();

    await expect(page.getByRole('listbox')).toBeFocused();
  });

  // SEL-API-26, SEL-FRM-14, SEL-PTR-24
  test('Field.Label links to trigger when trigger has an explicit id', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-form-label', { case: 'field-label', triggerId: 'x-id' });

    const label = page.getByTestId('label');
    const trigger = page.getByTestId('trigger');

    await expect(trigger).toHaveAttribute('id', 'x-id');
    await expect(label).toHaveAttribute('for', 'x-id');
    expect(await label.evaluate((element) => (element as HTMLLabelElement).htmlFor)).toBe('x-id');

    await label.click();

    await expect(page.getByRole('listbox')).toBeFocused();
  });
});

// SelectRoot.test.tsx:1577-1675, the locked roots at L4 (final review B1; SEL-EDGE-04, SEL-EDGE-05; at L2 in
// SelectRootAutofillTests.razor). Base UI's `fireEvent.change` on the hidden input is the same event in the page: the
// browser's autofill writes the value and fires `change`.
test.describe('locked roots', () => {
  for (const { lockState, label, withField } of [
    { lockState: 'readOnly', label: 'inside Field', withField: true },
    { lockState: 'disabled', label: 'inside Field', withField: true },
    { lockState: 'readOnly', label: 'outside Field', withField: false },
    { lockState: 'disabled', label: 'outside Field', withField: false },
  ] as const) {
    // SEL-DOM-47, SEL-FRM-12, SEL-FRM-19, SEL-EDGE-04.
    test(`ignores hidden-input autofill when ${lockState} ${label}`, async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { [lockState]: true, ...(withField ? { field: true } : { name: 'select' }) });
      const input = page.locator('input[name="select"]');
      await expect(input).toHaveCount(1);
      const expectedError = withField ? 'test' : null;
      const error = page.getByTestId('error');
      if (expectedError === null) {
        await expect(error).toHaveCount(0);
      } else {
        await expect(error).toHaveText(expectedError);
      }

      await input.evaluate((element) => {
        (element as HTMLInputElement).value = 'b';
        element.dispatchEvent(new Event('change', { bubbles: true }));
      });
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-value-changes', '');
      await expect(input).toHaveValue('');
      if (expectedError === null) {
        await expect(error).toHaveCount(0);
      } else {
        await expect(error).toHaveText(expectedError);
      }
    });
  }

  for (const lockState of ['disabled', 'readOnly'] as const) {
    // SEL-EDGE-04, SEL-EDGE-05, SEL-EDGE-06. An `aria-disabled` item is clicked with `force` (Playwright waits for it to be
    // enabled); the click is still a real mouse click at the item.
    test(`does not commit item selection when root is ${lockState} and forced open`, async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-form-state', { [lockState]: true, open: true });
      const optionB = page.getByRole('option', { name: 'b', exact: true });
      await expect(optionB).toBeVisible();
      // The box is read after the Positioner's first pass (issue 191's class); the Positioner is the outer `data-side`.
      await waitForPositioned(page.locator('[role="presentation"][data-side]').first());

      await optionB.click({ force: true });
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-value-changes', '');
      await expect(optionB).not.toHaveAttribute('data-selected');
    });
  }
});
