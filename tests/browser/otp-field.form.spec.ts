import { test, expect, type Page } from './helpers';
import { autofill, change, gotoOtp, slotValues } from './otp-field.helpers';

// OTP (docs/components/otp-field.md) at L4 for Base UI's `OTPFieldRoot.test.tsx` Field, Form, autofill and autoSubmit
// titles (issue 278, M6 track 7), per render mode. The hidden input is the `form` module's control with ReportAutofill: an
// autofill sets its value and dispatches `change` (the `list-values.spec.ts` precedent). AutoSubmit goes through the
// module's submit revision (design point 7 (a), chosen by the user 2026-11-19).

const submits = (page: Page) => page.evaluate(() => (window as unknown as { otpSubmits?: string[] }).otpSubmits ?? []);

test.describe('OtpField Field and Form', () => {
  test('validates the latest value only after focus leaves the OTP field in onBlur mode', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=onblur', 'otp-field-field');
    await field.slot(0).focus();
    await page.keyboard.type('1');
    await expect(field.slot(1)).toBeFocused();
    await field.slot(2).focus();
    await expect(field.fixture).toHaveAttribute('data-validations', '');

    await field.outside.focus();

    await expect(field.fixture).toHaveAttribute('data-validations', '1');
  });

  test('blocks form submission while the code is incomplete', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'form&name=otp&required&value=123');

    expect(await field.fixture.getByTestId('form').evaluate((form) => (form as HTMLFormElement).checkValidity())).toBe(false);
  });

  test('allows form submission when the code is complete', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'form&name=otp&required&value=123456');

    expect(await field.fixture.getByTestId('form').evaluate((form) => (form as HTMLFormElement).checkValidity())).toBe(true);
  });

  test('redirects hidden validation input focus to the first visible slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'name=otp');

    await field.hidden.focus();

    await expect(field.slot(0)).toBeFocused();
  });

  test('accepts valid hidden-input autofill and preserves focus when autofill is cleared', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'name=otp');

    await autofill(field.hidden, '123456');
    await expect.poll(() => slotValues(field)).toBe('123456');
    await expect(field.slot(5)).toBeFocused();
    await autofill(field.hidden, '');

    await expect.poll(() => slotValues(field)).toBe('______');
    await expect(field.slot(5)).toBeFocused();
    await expect(field.fixture).toHaveAttribute('data-changes', '123456:input-change _:input-change');
    await expect(field.fixture).toHaveAttribute('data-invalids', '');
  });

  test('handles password manager autofill through the hidden input', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'name=otp');

    await autofill(field.hidden, '12a34b56');

    await expect.poll(() => slotValues(field)).toBe('123456');
    await expect(field.slot(5)).toBeFocused();
    await expect(field.fixture).toHaveAttribute('data-changes', '123456:input-change');
    await expect(field.fixture).toHaveAttribute('data-invalids', '12a34b56:input-change');
    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-change');
  });

  test('composes validation and custom normalization during hidden input autofill', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=upper&type=alphanumeric', 'otp-field-normalize');

    await autofill(field.hidden, 'ab-12 cd!');

    await expect.poll(() => slotValues(field)).toBe('AB12CD');
    await expect(field.slot(5)).toBeFocused();
    await expect(field.fixture).toHaveAttribute('data-changes', 'AB12CD:input-change');
    await expect(field.fixture).toHaveAttribute('data-invalids', 'ab-12 cd!:input-change');
    await expect(field.fixture).toHaveAttribute('data-completes', 'AB12CD:input-change');
  });

  for (const [lock, ariaInvalid] of [
    ['readOnly', 'true'],
    ['disabled', null],
  ] as const) {
    test(`ignores hidden-input autofill when ${lock} inside Field`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, `case=lock-${lock === 'readOnly' ? 'readonly' : 'disabled'}`, 'otp-field-field');
      await expect(field.fixture.getByTestId('error')).toHaveText('test');
      for (let index = 0; index < 6; index += 1) {
        if (ariaInvalid === null) {
          await expect(field.slot(index)).not.toHaveAttribute('aria-invalid');
        } else {
          await expect(field.slot(index)).toHaveAttribute('aria-invalid', ariaInvalid);
        }
      }

      await autofill(field.hidden, '12a34b56');
      // The next render of the field shows nothing changed: the error stays and no recorder moved.
      await expect(field.fixture.getByTestId('error')).toHaveText('test');

      expect(await slotValues(field)).toBe('______');
      await expect(field.fixture).toHaveAttribute('data-changes', '');
      await expect(field.fixture).toHaveAttribute('data-invalids', '');
      await expect(field.fixture).toHaveAttribute('data-completes', '');
    });

    test(`ignores hidden-input autofill when ${lock} outside Field`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, `name=otp&${lock === 'readOnly' ? 'readonly' : 'disabled'}`);
      for (let index = 0; index < 6; index += 1) {
        await expect(field.slot(index)).not.toHaveAttribute('aria-invalid');
      }

      await autofill(field.hidden, '12a34b56');

      expect(await slotValues(field)).toBe('______');
      await expect(field.fixture).toHaveAttribute('data-changes', '');
      await expect(field.fixture).toHaveAttribute('data-invalids', '');
      await expect(field.fixture).toHaveAttribute('data-completes', '');
    });
  }
});

test.describe('OtpField autoSubmit', () => {
  test('does not auto-submit the owning form when the OTP becomes complete by default', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=default', 'otp-field-autosubmit');

    await change(field.slot(0), '123456');

    await expect.poll(() => slotValues(field)).toBe('123456');
    expect(await submits(page)).toEqual([]);
  });

  test('submits the completed named OTP value from the owning form when enabled', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=named', 'otp-field-autosubmit');

    await change(field.slot(0), '123456');

    await expect.poll(() => submits(page)).toEqual(['123456']);
    expect(await slotValues(field)).toBe('123456');
  });

  test('keeps the completed value when the owning form has no requestSubmit method', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=named', 'otp-field-autosubmit');
    await field.fixture.getByTestId('form').evaluate((form) => {
      Object.defineProperty(form, 'requestSubmit', { configurable: true, value: undefined });
    });

    await change(field.slot(0), '123456');

    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-change');
    expect(await slotValues(field)).toBe('123456');
    expect(await submits(page)).toEqual([]);
  });

  // EVT-14's flushSync titles become "no exception, one submit" (the brief, Deliverable 2): Blazor has no flushSync.
  test('port: auto-submits a Base UI Form once without an exception (Base UI: "does not call flushSync inside a layout effect")', async ({ page, mode }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    const field = await gotoOtp(page, mode, 'case=blazix', 'otp-field-autosubmit');

    await change(field.slot(0), '123456');

    await expect(field.fixture).toHaveAttribute('data-submits', '1');
    expect(await slotValues(field)).toBe('123456');
    expect(errors).toEqual([]);
  });

  test('port: blocks an invalid Base UI Form auto-submit without an exception (Base UI: "... without calling flushSync inside a layout effect")', async ({ page, mode }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    const field = await gotoOtp(page, mode, 'case=blazix-invalid', 'otp-field-autosubmit');

    await change(field.slot(0), '123456');

    await expect(field.fixture.getByTestId('error')).toHaveText('Invalid OTP');
    await expect(field.fixture).toHaveAttribute('data-submits', '0');
    expect(errors).toEqual([]);
  });

  test('keeps focus on the first invalid field when auto-submit is blocked', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=first-invalid', 'otp-field-autosubmit');
    const email = field.fixture.getByTestId('email');

    await change(field.slot(0), '123456');

    await expect(email).toHaveAttribute('aria-invalid', 'true');
    await expect(email).toBeFocused();
  });

  test('does not submit the owning form before the OTP becomes complete when enabled', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=named', 'otp-field-autosubmit');

    await change(field.slot(0), '12345');

    await expect.poll(() => slotValues(field)).toBe('12345_');
    expect(await submits(page)).toEqual([]);
  });

  test('submits an associated external form when used with `autoSubmit`', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=external', 'otp-field-autosubmit');

    await change(field.slot(0), '123456');

    await expect.poll(() => submits(page)).toEqual(['123456']);
  });

  test('does not submit an ancestor form when the explicit form id is not a form', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=not-form', 'otp-field-autosubmit');

    await change(field.slot(0), '123456');

    await expect.poll(() => slotValues(field)).toBe('123456');
    expect(await submits(page)).toEqual([]);
  });
});

test.describe('OtpField focus state in a Field', () => {
  test('is removed when the focused field becomes disabled', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=focus', 'otp-field-field');
    const fieldRoot = field.fixture.getByTestId('field');
    await field.slot(0).focus();
    await expect(fieldRoot).toHaveAttribute('data-focused', '');
    await expect(field.fixture.getByTestId('first')).toHaveAttribute('data-focused', '');

    await field.fixture.getByTestId('disable').click();

    await expect(fieldRoot).not.toHaveAttribute('data-focused');
    await expect(field.fixture.getByTestId('first')).not.toHaveAttribute('data-focused');
  });

  test('is removed when the focused field unmounts', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=focus', 'otp-field-field');
    const fieldRoot = field.fixture.getByTestId('field');
    await field.slot(0).focus();
    await expect(fieldRoot).toHaveAttribute('data-focused', '');

    await field.fixture.getByTestId('unmount').click();

    await expect(fieldRoot).not.toHaveAttribute('data-focused');
  });

  test('is kept when a previously focused slot unmounts after focus moves to a sibling', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=sibling', 'otp-field-field');
    const second = field.fixture.getByTestId('slot-second');
    await field.fixture.getByTestId('slot-first').focus();
    await second.focus();
    await expect(second).toBeFocused();
    await expect(field.fixture.getByTestId('field')).toHaveAttribute('data-focused', '');

    // The button does not take focus from the slot, as Base UI's setProps does not.
    await second.evaluate(() => (document.querySelector('[data-testid="unmount"]') as HTMLButtonElement).click());

    await expect(field.fixture.getByTestId('slot-first')).toHaveCount(0);
    await expect(second).toBeFocused();
    await expect(field.fixture.getByTestId('field')).toHaveAttribute('data-focused', '');
    await expect(field.root).toHaveAttribute('data-focused', '');
  });

  test('is removed when the focused slot unmounts but its field remains', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=remains', 'otp-field-field');
    await field.fixture.getByTestId('slot-first').focus();
    await expect(field.root).toHaveAttribute('data-focused', '');

    await field.fixture.getByTestId('slot-first').evaluate(() => (document.querySelector('[data-testid="unmount"]') as HTMLButtonElement).click());

    await expect(field.fixture.getByTestId('field')).not.toHaveAttribute('data-focused');
    await expect(field.root).not.toHaveAttribute('data-focused');
  });
});
