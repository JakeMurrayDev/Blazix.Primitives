import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, Fixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/architecture/field-and-form-infrastructure.md (FFI) and docs/components/field.md (FLD): the titles
// that render only Field parts (PR 1a, the landing rule of issue 77) and those that render Form, Field.Error or
// Field.Validity (PR 1c). Titles are Base UI's (FieldRoot.test.tsx, FieldControl.test.tsx, FieldLabel.test.tsx,
// FieldDescription.test.tsx, FieldError.test.tsx, useLabel.test.tsx) except the port's own, which name their row. Every
// test runs on both render modes; the label, description, legend and error-id rows are met on both (ADR 01 Amendments
// 2026-10-22). Readiness markers and the sync barrier, no sleeps; a transient attribute is asserted from the mutation
// records the page keeps (the transitions hand-over of M1).

type FixtureName =
  | 'field-basic'
  | 'field-validation'
  | 'field-label-non-native'
  | 'field-autofocus'
  | 'field-edit-form'
  | 'field-error'
  | 'field-custom-validity'
  | 'field-form';

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

/** Sets a custom validity message on a control from outside the field, as Base UI's tests call setCustomValidity(). */
async function setCustomValidity(control: Locator, message: string): Promise<void> {
  await control.evaluate((input, text) => (input as HTMLInputElement).setCustomValidity(text), message);
}

/** The control's validationMessage. */
function validationMessage(control: Locator): Promise<string> {
  return control.evaluate((input) => (input as HTMLInputElement).validationMessage);
}

/** Changes a control's value as a script does: the value, then an input event (a read-only control takes no typing). */
async function changeValue(control: Locator, value: string): Promise<void> {
  await control.evaluate((input, text) => {
    (input as HTMLInputElement).value = text;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }, value);
}

interface ErrorSnapshot {
  scope: string;
  present: boolean;
  hidden: boolean;
  starting: boolean;
  ending: boolean;
}

declare global {
  interface Window {
    errorRecords: ErrorSnapshot[];
    errorTransitionEnds: string[];
  }
}

// Installed before the page's scripts: a snapshot of each case's error after every mutation that changes it, and the
// transitionend events of the errors, by case.
function installErrorRecorder(): void {
  window.errorRecords = [];
  window.errorTransitionEnds = [];
  const last = new Map<string, string>();
  const take = () => {
    document.querySelectorAll('[data-case]').forEach((scope) => {
      const error = scope.querySelector('[data-testid=error]');
      const snapshot: ErrorSnapshot = {
        scope: scope.getAttribute('data-case') ?? '',
        present: error !== null,
        hidden: error?.hasAttribute('hidden') ?? false,
        starting: error?.hasAttribute('data-starting-style') ?? false,
        ending: error?.hasAttribute('data-ending-style') ?? false,
      };
      const key = JSON.stringify(snapshot);
      if (last.get(snapshot.scope) !== key) {
        last.set(snapshot.scope, key);
        window.errorRecords.push(snapshot);
      }
    });
  };
  new MutationObserver(take).observe(document, { subtree: true, childList: true, attributes: true });
  document.addEventListener(
    'transitionend',
    (event) => {
      const target = event.target as Element;
      if (target.matches?.('[data-testid=error]')) {
        window.errorTransitionEnds.push(target.closest('[data-case]')?.getAttribute('data-case') ?? '');
      }
    },
    true,
  );
}

/** The recorded snapshots of one case's error, from the first that matches `from` on. */
async function errorRecords(page: Page, scope: string, from: (snapshot: ErrorSnapshot) => boolean): Promise<ErrorSnapshot[]> {
  const records = (await page.evaluate(() => window.errorRecords)).filter((snapshot) => snapshot.scope === scope);
  const index = records.findIndex(from);
  return index < 0 ? [] : records.slice(index);
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

    // FFI-FRM-09 (PR 1c): a message the field did not set is the field's error, and it survives the field's own passes.
    test('keeps a message set outside the field when submitting', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="submitting"]');
      const control = scope.locator('[data-testid="control"]');
      await setCustomValidity(control, 'external error');

      await scope.getByRole('button', { name: 'submit' }).click();

      await expect(scope.locator('[data-testid="error"]')).toHaveText('external error');
      await settle(fixture);
      await expect(scope).toHaveAttribute('data-form-submits', '0');
      expect(await validationMessage(control)).toBe('external error');
    });

    test('keeps a message set outside the field when validating on change', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="on-change"]');
      const control = scope.locator('[data-testid="control"]');
      await setCustomValidity(control, 'external error');

      await control.fill('abc');

      await expect(scope.locator('[data-testid="error"]')).toHaveText('external error');
      await expect(control).toHaveAttribute('data-invalid', '');
      expect(await validationMessage(control)).toBe('external error');
    });

    test('keeps a message set outside the field when revalidating on change', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="revalidating"]');
      const control = scope.locator('[data-testid="control"]');
      await control.focus();
      await control.fill('a');
      await control.fill('');
      await control.blur();
      await expect(control).toHaveAttribute('data-invalid', '');

      await setCustomValidity(control, 'external error');
      await control.fill('abc');

      await expect(scope.locator('[data-testid="error"]')).toHaveText('external error');
      await expect(control).toHaveAttribute('data-invalid', '');
      expect(await validationMessage(control)).toBe('external error');
    });

    test('keeps other native errors deferred while a message set outside the field survives', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="deferred"]');
      const control = scope.locator('[data-testid="control"]');
      await control.focus();
      await control.fill('a');
      await control.fill('');
      await control.blur();
      await expect(control).toHaveAttribute('data-invalid', '');

      await setCustomValidity(control, 'external error');
      await control.fill('abc');

      await expect(scope).toHaveAttribute('data-errors', 'external error');
      await expect(scope).toHaveAttribute('data-custom-error', 'true');
      await expect(scope).toHaveAttribute('data-type-mismatch', 'false');
      await expect(scope).toHaveAttribute('data-valid', 'false');
      await expect(scope.locator('[data-testid="type-mismatch"]')).toHaveCount(0);
    });

    // FFI-FRM-10: the module writes the field's own message, with the line break normalized by the browser.
    test('clears the message it set itself', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="clears-own"]');
      const control = scope.locator('[data-testid="control"]');

      await control.fill('bad');
      await expect.poll(() => validationMessage(control)).toBe('custom error\nmore');

      await control.fill('good');
      await expect.poll(() => validationMessage(control)).toBe('');
      await expect(scope.locator('[data-testid="error"]')).toHaveCount(0);
    });

    test('restores a message its own one displaced', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="restores"]');
      const control = scope.locator('[data-testid="control"]');
      await setCustomValidity(control, 'external error');

      await control.fill('bad');
      await expect.poll(() => validationMessage(control)).toBe('custom error');

      await control.fill('good');
      await expect.poll(() => validationMessage(control)).toBe('external error');
      await expect(scope.locator('[data-testid="error"]')).toHaveText('external error');
    });

    test('does not restore a message that was withdrawn while its own one was installed', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="withdrawn"]');
      const control = scope.locator('[data-testid="control"]');
      await setCustomValidity(control, 'external error');

      await control.fill('bad');
      await expect.poll(() => validationMessage(control)).toBe('custom error');
      await setCustomValidity(control, '');

      await control.fill('good');
      await expect.poll(() => validationMessage(control)).toBe('');
      await expect(scope.locator('[data-testid="error"]')).toHaveCount(0);
    });

    // FFI-FRM-08: a read-only control is barred from constraint validation (willValidate is false).
    test('ignores a message set outside the field on a control barred from validation', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="barred"]');
      const control = scope.locator('[data-testid="control"]');
      expect(await control.evaluate((input) => (input as HTMLInputElement).willValidate)).toBe(false);
      await setCustomValidity(control, 'external error');

      await changeValue(control, 'abc');
      await settle(fixture);

      await expect(control).not.toHaveAttribute('data-invalid');
      await expect(scope.locator('[data-testid="error"]')).toHaveCount(0);

      await scope.getByRole('button', { name: 'submit' }).click();
      await expect(scope).toHaveAttribute('data-form-submits', '1');
    });

    test('does not write its own message to a barred control', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-custom-validity');
      const scope = fixture.locator('[data-case="barred-own"]');
      const control = scope.locator('[data-testid="control"]');
      await setCustomValidity(control, 'external error');

      await changeValue(control, 'abc');
      await expect(scope.locator('[data-testid="error"]')).toHaveText('custom error');

      await scope.locator('[data-make-editable]').click();
      await expect(control).not.toHaveAttribute('readonly');
      await settle(fixture);

      expect(await validationMessage(control)).toBe('external error');
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

  // The two platform facts behind FLD-DOM-11's `C# → JS` writer (ADR 04 Amendments 2026-10-22, chosen by the user):
  // Blazor's renderer writes `value` only as a property and only when the rendered frame changed. A change here fails
  // these titles before it reaches the form module's value ownership.
  test.describe('platform: the value Blazor renders', () => {
    test('is not re-applied over the user\'s text when the frame is unchanged', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const plain = fixture.locator('[data-case="plain"] [data-testid="plain"]');
      await expect(plain).toHaveValue('first');

      await plain.fill('typed');
      await settle(fixture);

      await expect(plain).toHaveValue('typed');
    });

    test('overwrites the user\'s text when it changes, and never sets defaultValue', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const plain = fixture.locator('[data-case="plain"] [data-testid="plain"]');
      expect(await plain.evaluate((input) => (input as HTMLInputElement).defaultValue)).toBe('');

      await plain.fill('edited');
      await fixture.locator('[data-case="plain"] [data-set-plain]').click();

      await expect(plain).toHaveValue('second');
      expect(await plain.evaluate((input) => (input as HTMLInputElement).defaultValue)).toBe('');
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

    // The handover at attach (ADR 04 Amendments 2026-10-22, settled by the orchestrator): an edit made before the form
    // module attached is the user's, so the module reports it instead of showing the stale value C# rendered.
    test('keeps and reports an edit made before the control attached', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="early"]');
      await scope.evaluate((container) => {
        const observer = new MutationObserver(() => {
          const control = container.querySelector<HTMLInputElement>('[data-testid="control"]');
          if (control) {
            observer.disconnect();
            control.value = 'early';
            control.dispatchEvent(new Event('input', { bubbles: true }));
          }
        });
        observer.observe(container, { childList: true, subtree: true });
      });

      await scope.locator('[data-mount]').click();

      await expect(scope).toHaveAttribute('data-attaches', '1');
      await expect(scope).toHaveAttribute('data-value', 'early');
      await expect(scope.locator('[data-testid="control"]')).toHaveValue('early');
      await expect(scope.locator('[data-testid="root"]')).toHaveAttribute('data-dirty', '');
    });

    // FLD-FRM-01 (registration latency on Server): the remounted control registers after its render, and the field keeps
    // the baseline it took at the first registration.
    test('keeps the original baseline when a controlled control remounts', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-basic');
      const scope = fixture.locator('[data-case="remount"]');
      const root = scope.locator('[data-testid="root"]');

      await expect(scope).toHaveAttribute('data-attaches', '1');
      await scope.locator('[data-testid="control"]').fill('b');
      await expect(root).toHaveAttribute('data-dirty', '');

      await scope.locator('[data-toggle]').click();
      await scope.locator('[data-toggle]').click();
      await expect(scope.locator('[data-testid="control"]')).toHaveValue('b');
      await expect(scope).toHaveAttribute('data-attaches', '2');

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

  test.describe('Field.Error', () => {
    // FLD-API-31, FLD-ARIA-05 (met on both modes: the id is published in the render that mounts the error).
    test('should set aria-describedby on the control automatically', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-error');
      const scope = fixture.locator('[data-case="describedby"]');
      const errorId = await scope.locator('[data-testid="error"]').getAttribute('id');

      expect(errorId).toBeTruthy();
      await expect(scope.locator('[data-testid="control"]')).toHaveAttribute('aria-describedby', errorId!);
    });

    // FFI-FRM-06: the browser's own valueMissing, deferred until the submission.
    test('should show error messages by default', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-error');
      const scope = fixture.locator('[data-case="default"]');
      const control = scope.locator('[data-testid="control"]');
      const error = scope.locator('[data-testid="error"]');
      await expect(error).toHaveCount(0);

      await control.focus();
      await control.fill('a');
      await control.fill('');
      await control.blur();
      await settle(fixture);
      await expect(error).toHaveCount(0);

      await scope.getByRole('button', { name: 'submit' }).click();
      await expect(error).toHaveText('Message');
    });

    // FLD-ANI-02, FLD-DOM-18: C# creates the element with data-starting-style; the module removes it on the next frame,
    // so the transition from the starting style runs.
    test('triggers enter animation via data-starting-style when mounting', async ({ page, mode }) => {
      await page.addInitScript(installErrorRecorder);
      const fixture = await gotoField(page, mode, 'field-error', '?animations=1');
      const scope = fixture.locator('[data-case="enter"]');
      const error = scope.locator('[data-testid="error"]');
      await expect(error).toHaveCount(0);

      await scope.locator('[data-show]').click();

      await expect.poll(() => page.evaluate(() => window.errorTransitionEnds.filter((name) => name === 'enter').length)).toBeGreaterThan(0);
      await expect(error).toBeVisible();
      await expect(error).not.toHaveAttribute('data-starting-style');
      expect((await errorRecords(page, 'enter', (snapshot) => snapshot.present))[0]?.starting).toBe(true);
    });

    // FLD-STA-06, FLD-DOM-14, FLD-DOM-18, FLD-ANI-03: data-ending-style while the exit runs, then the element is removed.
    test('applies data-ending-style before unmount', async ({ page, mode }) => {
      await page.addInitScript(installErrorRecorder);
      const fixture = await gotoField(page, mode, 'field-error', '?animations=1');
      const scope = fixture.locator('[data-case="exit"]');
      const error = scope.locator('[data-testid="error"]');
      await expect(error).toBeVisible();

      await scope.locator('[data-hide]').click();

      await expect(error).toHaveCount(0);
      const exit = await errorRecords(page, 'exit', (snapshot) => snapshot.ending);
      expect(exit.length).toBeGreaterThan(0);
      expect(exit[0]!.present).toBe(true);
      if (mode === 'server') {
        // ADR 01's TRN-ANI-03 row (FLD-ANI-03's Server relaxation): the module hides the error at exit end; the removal
        // follows the completion message.
        expect(exit.some((snapshot) => snapshot.hidden)).toBe(true);
      }
    });
  });

  test.describe('in a Form or with Field.Validity', () => {
    // FFI-FRM-06: the browser reports valueMissing at the blur; an untouched value is not an error yet.
    test('does not publish errors while `valueMissing` is suppressed', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="suppressed"]');
      const control = scope.locator('[data-testid="control"]');

      await control.focus();
      await control.blur();
      await settle(fixture);

      await expect(scope).toHaveAttribute('data-valid', 'true');
      await expect(scope).toHaveAttribute('data-errors', '');
      await expect(scope).toHaveAttribute('data-error', '');
    });

    // FLD-EDGE-12: a value written by a script and then blurred is validated from the element.
    test('should apply [data-field] style hooks to field components', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="style-hooks"]');
      const control = scope.locator('[data-testid="control"]');
      const parts = ['control', 'label', 'description'].map((part) => scope.locator(`[data-testid="${part}"]`));
      for (const part of parts) {
        await expect(part).not.toHaveAttribute('data-valid');
      }
      await expect(scope.locator('[data-testid="error"]')).toHaveCount(0);

      await control.focus();
      await control.fill('a');
      await control.fill('');
      await control.blur();

      for (const part of [...parts, scope.locator('[data-testid="error"]')]) {
        await expect(part).toHaveAttribute('data-invalid', '');
      }

      await control.evaluate((input) => {
        (input as HTMLInputElement).value = 'value';
        (input as HTMLInputElement).focus();
        (input as HTMLInputElement).blur();
      });

      for (const part of parts) {
        await expect(part).toHaveAttribute('data-valid', '');
      }
      await expect(scope.locator('[data-testid="error"]')).toHaveCount(0);
    });

    // FLD-FRM-01: the values a validator receives come from the registered fields of the moment.
    test('unmounted fields are excluded from the validate fn', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="unmounted"]');

      await scope.getByRole('button', { name: 'submit' }).click();
      await expect(scope).toHaveAttribute('data-calls', 'input1=one&input2=two');

      await scope.getByRole('checkbox').click();
      await scope.getByRole('button', { name: 'submit' }).click();

      await expect(scope).toHaveAttribute('data-calls', 'input1=one&input2=two|input2=two');
      await expect(scope).toHaveAttribute('data-submits', '2');
    });

    test('uses the Field.Control name for form submission and form validation values', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="control-name"]');

      await scope.getByRole('button', { name: 'submit' }).click();

      await expect(scope).toHaveAttribute('data-values', 'email=one@example.com&confirmEmail=one@example.com');
      await expect(scope).toHaveAttribute('data-calls', /(^|\|)email=one@example\.com&confirmEmail=one@example\.com$/);
    });
  });

  test.describe('Field.Control in a Form', () => {
    // FLD-FRM-17, FFI-FRM-32 (Server: the re-commit runs after the render, a round trip later; ADR 01's FLD-FRM-17 row).
    test('validates the final controlled value when it is normalized on blur', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="normalized"]');
      const control = scope.locator('[data-testid="control"]');

      await control.fill('foo ');
      await control.blur();

      await expect(scope).toHaveAttribute('data-calls', /\|foo$/);
      await expect(scope.locator('[data-testid="error"]')).toHaveText('Invalid email');
    });

    test('keeps the final async validation when a controlled value is normalized on blur', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="normalized-async"]');
      const control = scope.locator('[data-testid="control"]');

      await control.fill('foo ');
      await control.blur();
      await expect(scope).toHaveAttribute('data-calls', 'foo |foo');

      await scope.locator('[data-resolve-final]').click();
      await expect(scope.locator('[data-testid="error"]')).toHaveText('Invalid email');

      await scope.locator('[data-resolve-stale]').click();
      await settle(fixture);
      await expect(scope.locator('[data-testid="error"]')).toHaveText('Invalid email');
    });

    test('does not validate when a controlled value is reset to the initial value on blur', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="reset-initial"]');
      const control = scope.locator('[data-testid="control"]');

      await control.fill('foo');
      await control.blur();
      await expect(control).toHaveValue('');
      await settle(fixture);

      await expect(scope.locator('[data-testid="error"]')).toHaveCount(0);
    });

    // FLD-KEY-01, FFI-KEY-01, FRM-DOM-03, FRM-KEY-01: the browser's implicit submission validates once.
    test('validates once when Enter implicitly submits a form', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="enter-submits"]');

      await scope.locator('[data-testid="control"]').press('Enter');

      await expect(scope).toHaveAttribute('data-submits', '1');
      await settle(fixture);
      await expect(scope).toHaveAttribute('data-calls', '1');
    });

    test('validates when Enter does not implicitly submit the form', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="enter-no-submit"]');

      await scope.locator('[data-testid="control"]').press('Enter');

      await expect(scope).toHaveAttribute('data-calls', '1');
      await settle(fixture);
      await expect(scope).toHaveAttribute('data-submits', '0');
    });

    test('validates when a disabled submit button blocks implicit submission', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="enter-disabled-submit"]');

      await scope.locator('[data-testid="control"]').press('Enter');

      await expect(scope).toHaveAttribute('data-calls', '1');
      await settle(fixture);
      await expect(scope).toHaveAttribute('data-submits', '0');
    });

    // FLD-KEY-04: the deferred commit reads the value the form's own keydown handler set.
    test('validates the latest value when Enter does not submit the form', async ({ page, mode }) => {
      const fixture = await gotoField(page, mode, 'field-form');
      const scope = fixture.locator('[data-case="enter-latest"]');
      const control = scope.locator('[data-testid="control"]');

      await control.focus();
      await control.press('Enter');

      await expect(scope).toHaveAttribute('data-count', '1');
      await expect(scope).toHaveAttribute('data-last', '');
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
