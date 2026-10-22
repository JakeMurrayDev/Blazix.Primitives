import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, Fixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/architecture/field-and-form-infrastructure.md (FFI) and docs/components/field.md (FLD) whose
// titles render only Field parts (PR 1a, the landing rule of issue 77); the titles that render Form, Field.Error or
// Field.Validity land in PR 1c. Titles are Base UI's (FieldRoot.test.tsx, FieldControl.test.tsx, FieldLabel.test.tsx,
// FieldDescription.test.tsx, useLabel.test.tsx) except the port's own, which name their row. Every test runs on both
// render modes; the label, description and legend rows are met on both (ADR 01 Amendments 2026-10-22). Readiness markers
// and the sync barrier, no sleeps.

type FixtureName = 'field-basic' | 'field-validation' | 'field-label-non-native' | 'field-autofocus' | 'field-edit-form';

/** Opens a Field fixture and waits until every control and label has the form module attached. */
async function gotoField(page: Page, mode: RenderMode, name: FixtureName, query = ''): Promise<Locator> {
  await page.goto(`${fixtureUrl(mode, name)}${query}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/**
 * A barrier: clicks the fixture's sync button and waits for C# to count it. Server processes one circuit's events and
 * the form module's reports in order (ADR-04/1) and WebAssembly synchronously, so everything dispatched before has
 * reached C# afterwards.
 */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

test.describe('Field', () => {
  test.describe('Field.Label', () => {
    // FFI-PTR-01, FLD-PTR-03: the click-to-focus runs in the form module inside the dispatch, on both modes.
    test('when nativeLabel={false}, clicking focuses the associated control', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-label-non-native');
      const scope = fixture.locator('[data-case="non-native"]');

      await expect(scope.locator('[data-testid="label"]')).not.toHaveAttribute('for');
      await scope.locator('[data-testid="label"]').click();

      await expect(scope.locator('[data-testid="control"]')).toBeFocused();
    });

    // FFI-PTR-02. Blazor renders into the document, so the spec moves the rendered field into a shadow root first: the
    // native label keeps its `for` association inside the shadow tree, and the module does not interfere.
    test('preserves native label activation inside a shadow root', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-label-non-native');
      const source = fixture.locator('[data-case="shadow"] [data-shadow-source]');
      await source.evaluate((element) => {
        const host = document.createElement('div');
        host.setAttribute('data-shadow-host-of', 'shadow');
        element.parentElement!.appendChild(host);
        host.attachShadow({ mode: 'open' }).appendChild(element);
      });

      await page.locator('[data-shadow-host-of="shadow"]').getByText('Name').click();

      const focused = await page.locator('[data-shadow-host-of="shadow"]').evaluate((host) => {
        const active = host.shadowRoot!.activeElement;
        return active?.getAttribute('data-testid') ?? null;
      });
      expect(focused).toBe('control');
    });

    // FFI-PTR-03: the nested-control guard walks out of the shadow root the click came from.
    test('does not focus the control when a composed click originates inside a nested button', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-label-non-native');
      const scope = fixture.locator('[data-case="nested"]');

      await scope.locator('[data-shadow-host]').evaluate((host) => {
        const target = document.createElement('span');
        target.setAttribute('data-testid', 'shadow-target');
        host.attachShadow({ mode: 'open' }).appendChild(target);
        target.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true }));
      });

      await expect(scope.locator('[data-testid="control"]')).not.toBeFocused();
    });
  });

  test.describe('Field.Description', () => {
    // FLD-ARIA-04: published while the description initializes, so the attribute lands with it on both modes.
    test('should set aria-describedby on the control automatically', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="description"]');

      const id = await scope.locator('[data-testid="description"]').getAttribute('id');
      expect(id).toBeTruthy();
      await expect(scope.locator('[data-testid="control"]')).toHaveAttribute('aria-describedby', id!);
    });
  });

  test.describe('prop: validationMode', () => {
    // FFI-ALG-02, FFI-FRM-06: the browser's own ValidityState, read by the module at the blur.
    test('should not mark invalid if `valueMissing` is the only error and not yet dirtied', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-validation');
      const control = fixture.locator('[data-case="required"] [data-testid="control"]');

      await control.focus();
      await control.blur();
      await settle(fixture);

      await expect(control).not.toHaveAttribute('data-invalid');
      await expect(control).not.toHaveAttribute('aria-invalid');
    });

    test('should not mark field as invalid for valueMissing if not dirty', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-validation');
      const control = fixture.locator('[data-case="required"] [data-testid="control"]');

      await control.focus();
      await control.blur();
      await settle(fixture);

      await expect(control).not.toHaveAttribute('data-invalid');
      await expect(control).not.toHaveAttribute('aria-invalid');
    });

    test('should mark invalid if `valueMissing` is the only error and dirtied', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-validation');
      const control = fixture.locator('[data-case="required"] [data-testid="control"]');

      await control.focus();
      await control.fill('a');
      await control.fill('');
      await control.blur();

      await expect(control).toHaveAttribute('data-invalid', '');
      await expect(control).toHaveAttribute('aria-invalid', 'true');
    });

    test('should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-validation');
      const control = fixture.locator('[data-case="type-mismatch"] [data-testid="control"]');

      await control.focus();
      await control.blur();

      await expect(control).toHaveAttribute('data-invalid', '');
      await expect(control).toHaveAttribute('aria-invalid', 'true');
    });
  });

  test.describe('custom validity ownership', () => {
    // FFI-FRM-09: the field withdraws its own message from an input that became disabled (barred from validation).
    test('clears its own message on an input that became disabled', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-validation');
      const scope = fixture.locator('[data-case="disabled-message"]');
      const control = scope.locator('[data-testid="control"]');

      await scope.locator('[data-validate]').click();
      await expect.poll(() => control.evaluate((input) => (input as HTMLInputElement).validationMessage)).toBe('custom error');

      await scope.locator('[data-toggle-disabled]').click();
      await scope.locator('[data-pass]').click();
      await scope.locator('[data-validate]').click();
      await scope.locator('[data-toggle-disabled]').click();
      await settle(fixture);

      await expect.poll(() => control.evaluate((input) => (input as HTMLInputElement).validationMessage)).toBe('');
    });

    test('does not adopt a native message as the message it displaced', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-validation');
      const control = fixture.locator('[data-case="native-message"] [data-testid="control"]');

      await control.fill('bad');
      await expect.poll(() => control.evaluate((input) => (input as HTMLInputElement).validationMessage)).toBe('custom error');

      await control.fill('a@b.co');
      await settle(fixture);

      expect(await control.evaluate((input) => (input as HTMLInputElement).validity.customError)).toBe(false);
      await expect.poll(() => control.evaluate((input) => (input as HTMLInputElement).validationMessage)).toBe('');
    });
  });

  test.describe('defaultValue behavior', () => {
    // FFI-EDGE-07, FLD-EDGE-12: the module owns the value from attach and never re-applies the default on focus.
    test('should not reset to defaultValue when input value is programmatically changed and then focused', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const input = fixture.locator('[data-case="default-value"] [data-testid="input"]');

      await expect(input).toHaveValue('foo');
      await input.evaluate((element) => {
        (element as HTMLInputElement).value = '';
      });
      await expect(input).toHaveValue('');

      await input.focus();
      await settle(fixture);

      await expect(input).toHaveValue('');
    });

    test('should not reset to defaultValue when input value is programmatically changed to non-empty value and then focused', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const input = fixture.locator('[data-case="default-value"] [data-testid="input"]');

      await expect(input).toHaveValue('foo');
      await input.evaluate((element) => {
        (element as HTMLInputElement).value = 'abc';
      });
      await expect(input).toHaveValue('abc');

      await input.focus();
      await settle(fixture);

      await expect(input).toHaveValue('abc');
    });

    // FFI-EDGE-06, FLD-API-16 (the port's title): a changed DefaultValue becomes the element's default and keeps the
    // user's text, which Blazor's renderer, writing only the `value` property, could not do.
    test('keeps the user\'s text when the default value changes', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="default-change"]');
      const control = scope.locator('[data-testid="control"]');
      await expect(control).toHaveValue('first');

      await control.fill('edited');
      await scope.locator('[data-change-default]').click();
      await settle(fixture);

      await expect(control).toHaveValue('edited');
      expect(await control.evaluate((input) => (input as HTMLInputElement).defaultValue)).toBe('second');
    });
  });

  test.describe('controlled value', () => {
    // FLD-API-15 (the port's title): the module re-applies the consumer's value after an edit it did not take, on Server
    // one round trip later (ADR 01 relaxation row added 2026-10-22).
    test('reverts an edit the consumer does not apply', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="ignored"]');
      const control = scope.locator('[data-testid="control"]');
      await expect(control).toHaveValue('kept');

      await control.pressSequentially('x');

      await expect(scope).toHaveAttribute('data-changes', '1');
      await expect(control).toHaveValue('kept');
    });

    // FLD-FRM-01 (registration latency on Server): the remounted control registers after its render, and the field keeps
    // the baseline it took at the first registration.
    test('keeps the original baseline when a controlled control remounts', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="remount"]');
      const root = scope.locator('[data-testid="root"]');

      await scope.locator('[data-testid="control"]').fill('b');
      await expect(root).toHaveAttribute('data-dirty', '');

      await scope.locator('[data-toggle]').click();
      await scope.locator('[data-toggle]').click();
      await expect(scope.locator('[data-testid="control"]')).toHaveValue('b');

      await expect(root).toHaveAttribute('data-dirty', '');

      await scope.locator('[data-testid="control"]').fill('a');
      await expect(root).not.toHaveAttribute('data-dirty');
    });
  });

  test.describe('[data-focused]', () => {
    // FLD-FOC-04: the module reports a control that is already focused when it becomes enabled again only if it still
    // has focus; browsers move focus off a disabled control.
    test('can be re-acquired after the control is re-enabled', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="focus"]');
      const root = scope.locator('[data-testid="root"]');
      const control = scope.locator('[data-testid="first"]');

      await control.focus();
      await expect(root).toHaveAttribute('data-focused', '');

      await scope.locator('[data-toggle-disabled]').click();
      await expect(root).not.toHaveAttribute('data-focused');

      await scope.locator('[data-testid="outside"]').focus();
      await scope.locator('[data-toggle-disabled]').click();
      await settle(fixture);
      await expect(root).not.toHaveAttribute('data-focused');

      await control.focus();
      await expect(root).toHaveAttribute('data-focused', '');
    });

    // FLD-FRM-01: the unregistration of a removed control clears the focused state it owned.
    test('is removed when the focused control unmounts', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="focus"]');
      const root = scope.locator('[data-testid="root"]');

      await scope.locator('[data-testid="first"]').focus();
      await expect(root).toHaveAttribute('data-focused', '');

      await scope.locator('[data-toggle-mounted]').evaluate((element) => (element as HTMLElement).click());

      await expect(scope.locator('[data-testid="first"]')).toHaveCount(0);
      await expect(root).not.toHaveAttribute('data-focused');
      await expect(scope.locator('[data-testid="label"]')).not.toHaveAttribute('data-focused');
    });

    // FLD-FOC-04: the browser focuses the prerendered autofocus control before the runtime starts; the module reports it
    // focused when it attaches.
    test('should sync focused state when autoFocus is used with SSR', async ({ page, mode }) => {
      const html = await (await page.request.get(fixtureUrl(mode, 'field-autofocus'))).text();
      expect(html).toMatch(/<input[^>]*autofocus/);

      const fixture = await gotoField(page, mode, 'field-autofocus');
      const control = fixture.locator('[data-testid="control"]');

      await expect(control).toHaveAttribute('autofocus', '');
      await expect(control).toBeFocused();
      await expect(fixture.locator('[data-testid="root"]')).toHaveAttribute('data-focused', '');
      await expect(control).toHaveAttribute('data-focused', '');
      await expect(fixture.getByText('Name')).toHaveAttribute('data-focused', '');
    });
  });

  test.describe('keyboard', () => {
    // FFI-KEY-01, FLD-KEY-01: outside a form the Enter commit runs at the keydown.
    test('validates when Enter is pressed outside a form', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="enter"]');
      const control = scope.locator('[data-testid="control"]');

      await control.focus();
      await control.press('Enter');

      await expect(scope).toHaveAttribute('data-validations', '1');
    });
  });

  // ADR 05 Amendments 2026-10-22, verification item 5: inside Blazor's EditForm the control notifies the EditContext,
  // takes the property's name, and the field shows the DataAnnotations messages as its invalid state.
  test('@verifies ADR-05/5 participates in a cascaded EditContext', async ({ page, mode }) => {
    const fixture = await gotoField(page, mode, 'field-edit-form');
    const control = fixture.locator('[data-testid="control"]');
    const root = fixture.locator('[data-testid="root"]');

    await expect(control).toHaveAttribute('name', 'Email');

    await fixture.getByRole('button', { name: 'Submit' }).click();
    await expect(fixture).toHaveAttribute('data-invalid-submits', '1');
    await expect(fixture.getByText('The email is required.')).toBeVisible();
    await expect(root).toHaveAttribute('data-invalid', '');
    await expect(control).toHaveAttribute('aria-invalid', 'true');

    await control.fill('not an email');
    await expect(fixture.getByText('The email is not valid.')).toBeVisible();
    await expect(root).toHaveAttribute('data-invalid', '');

    await control.fill('a@b.co');
    await expect(fixture.getByText('The email is not valid.')).toHaveCount(0);
    await expect(root).not.toHaveAttribute('data-invalid');

    await fixture.getByRole('button', { name: 'Submit' }).click();
    await expect(fixture).toHaveAttribute('data-valid-submits', '1');
  });
});
