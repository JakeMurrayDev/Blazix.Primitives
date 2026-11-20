import { test, expect } from './helpers';
import { change, gotoOtp, paste, slotValues } from './otp-field.helpers';

// OTP (docs/components/otp-field.md) at L4 for Base UI's `OTPFieldRoot.test.tsx` interaction titles (issue 278, M6 track
// 7), per render mode: value handling with typing, NormalizeValue, onValueChange, onValueInvalid, onValueComplete, the
// controlled focus and completion, disabled and read-only, typing, paste and keys, and the standalone root state. Base UI's
// `fireEvent.change` is `change()` (the slot's whole text, one `input` event); a paste is a dispatched `ClipboardEvent`.

test.describe('OtpFieldRoot value handling', () => {
  test('supports typing alphabetic values when set to `alpha`', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'type=alpha');

    await change(field.slot(0), '1a2b3C');

    await expect.poll(() => slotValues(field)).toBe('abC___');
  });

  test('supports custom normalization when `validationType` is `none`', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=strip-upper&type=none', 'otp-field-normalize');

    await change(field.slot(0), 'ab-12 cd');

    await expect.poll(() => slotValues(field)).toBe('AB12CD');
  });

  test('composes with built-in validation and advances focus', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=upper&type=alphanumeric', 'otp-field-normalize');
    await field.slot(0).focus();

    await change(field.slot(0), 'a!');

    await expect.poll(() => slotValues(field)).toBe('A_____');
    await expect(field.slot(1)).toBeFocused();
  });

  test('composes built-in validation and custom normalization for pasted values', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=upper&type=alphanumeric', 'otp-field-normalize');
    await field.slot(0).focus();

    await paste(field.slot(0), 'ab-12 cd!');

    await expect.poll(() => slotValues(field)).toBe('AB12CD');
  });

  test('composes built-in validation and custom normalization from a non-first slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=upper&type=alphanumeric&value=12', 'otp-field-normalize');
    await field.slot(2).focus();

    await change(field.slot(2), 'a!');

    await expect.poll(() => slotValues(field)).toBe('12A___');
    await expect(field.slot(3)).toBeFocused();
  });

  test('fires `input-change` when typing', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);

    await change(field.slot(0), '1');

    await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change');
  });

  test('fires `input-clear` when clearing a slot by input', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1');

    await change(field.slot(0), '');

    await expect(field.fixture).toHaveAttribute('data-changes', '_:input-clear');
  });

  test('fires when typing is normalized before the OTP value updates', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);

    await change(field.slot(0), '1a');

    await expect(field.fixture).toHaveAttribute('data-invalids', '1a:input-change');
    await expect.poll(() => slotValues(field)).toBe('1_____');
  });

  for (const [title, query] of [
    ['fires when custom normalization removes characters', 'fn=zero-three&type=none&inputmode=numeric'],
    ['fires when custom normalization removes characters after built-in validation', 'fn=zero-three'],
  ] as const) {
    test(title, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, query, 'otp-field-normalize');

      await change(field.slot(0), '1209');

      await expect(field.fixture).toHaveAttribute('data-invalids', '1209:input-change');
      await expect.poll(() => slotValues(field)).toBe('120___');
    });
  }

  test('fires when built-in validation removes characters before custom normalization expands the value', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=expand', 'otp-field-normalize');

    await change(field.slot(0), '1a');

    await expect(field.fixture).toHaveAttribute('data-invalids', '1a:input-change');
    await expect.poll(() => slotValues(field)).toBe('12____');
  });

  test('fires when custom normalization removes all characters after built-in validation', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=empty', 'otp-field-normalize');

    await change(field.slot(0), '1');

    await expect(field.fixture).toHaveAttribute('data-invalids', '1:input-change');
    await expect.poll(() => slotValues(field)).toBe('______');
  });

  test('fires `input-paste` when pasted text is normalized before the OTP value updates', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await paste(field.slot(0), '12a34');

    await expect(field.fixture).toHaveAttribute('data-invalids', '12a34:input-paste');
    await expect.poll(() => slotValues(field)).toBe('1234__');
  });

  test('fires `input-paste` when custom normalization removes characters after built-in validation', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'fn=zero-three', 'otp-field-normalize');
    await field.slot(0).focus();

    await paste(field.slot(0), '1209');

    await expect(field.fixture).toHaveAttribute('data-invalids', '1209:input-paste');
    await expect.poll(() => slotValues(field)).toBe('120___');
  });

  test('fires when typing completes the OTP (the whole code at once)', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);

    await change(field.slot(0), '123456');

    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-change');
  });

  test('fires `input-paste` when pasting completes the OTP', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await paste(field.slot(0), '123456');

    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-paste');
  });

  test('fires `input-paste` when a complete paste replaces a complete OTP', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await paste(field.slot(0), '123456');
    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-paste');
    await paste(field.slot(0), '654321');
    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-paste 654321:input-paste');
    await paste(field.slot(0), '654321');

    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-paste 654321:input-paste 654321:input-paste');
  });

  test('fires `input-paste` when pasting into a middle slot completes the OTP', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');
    await field.slot(2).focus();

    await paste(field.slot(2), '3456');

    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-paste');
  });

  test('does not fire when a completion-making paste is canceled', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'cancel=all');
    await field.slot(0).focus();

    await paste(field.slot(0), '123456');

    await expect(field.fixture).toHaveAttribute('data-changes', '123456:input-paste');
    await expect(field.fixture).toHaveAttribute('data-completes', '');
    expect(await slotValues(field)).toBe('______');
  });

  test('does not fire before the OTP becomes complete', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);

    await change(field.slot(0), '12345');

    await expect(field.fixture).toHaveAttribute('data-changes', '12345:input-change');
    await expect(field.fixture).toHaveAttribute('data-completes', '');
  });

  test('does not fire later for a stale controlled completion attempt', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, '', 'otp-field-controlled');
    await change(field.slot(0), '123456');
    await expect(field.fixture).toHaveAttribute('data-changes', '123456:input-change');

    await field.fixture.getByTestId('apply').click();

    await expect.poll(() => slotValues(field)).toBe('654321');
    await expect(field.fixture).toHaveAttribute('data-completes', '');
  });

  test('fires after an asynchronously accepted controlled completion', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'mode=defer', 'otp-field-controlled');
    await change(field.slot(0), '123456');
    await expect(field.fixture).toHaveAttribute('data-pending', '123456');

    await field.fixture.getByTestId('accept').click();

    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-change');
  });

  test('does not fire again when a controlled value changes from one complete value to another', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=123456', 'otp-field-controlled');

    await field.fixture.getByTestId('apply').click();

    await expect.poll(() => slotValues(field)).toBe('654321');
    await expect(field.fixture).toHaveAttribute('data-completes', '');
  });
});

test.describe('OtpFieldRoot props and interactions', () => {
  test('disables every slot and prevents value changes', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'disabled');

    await expect(field.slot(0)).toBeDisabled();
    await expect(field.root).toHaveAttribute('data-disabled', '');
    await change(field.slot(0), '1');

    await expect.poll(() => slotValues(field)).toBe('______');
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });

  test('marks every slot as readonly and prevents value changes', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'readonly');

    await expect(field.slot(0)).toHaveAttribute('readonly', '');
    await expect(field.root).toHaveAttribute('data-readonly', '');
    await change(field.slot(0), '1');

    await expect.poll(() => slotValues(field)).toBe('______');
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });

  test('tracks focus state when a readonly slot receives focus', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'readonly');

    await field.slot(0).focus();

    await expect(field.root).toHaveAttribute('data-focused', '');
  });

  test('fills consecutive slots when typing multiple characters into the first input', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);

    await change(field.slot(0), '123456');

    await expect.poll(() => slotValues(field)).toBe('123456');
  });

  test('replaces consecutive slots when typing multiple characters into a later input', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=123456');

    await change(field.slot(2), '99');

    await expect.poll(() => slotValues(field)).toBe('129956');
  });

  test('fills consecutive slots when pasting a code', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await paste(field.slot(0), '123456');

    await expect.poll(() => slotValues(field)).toBe('123456');
    await expect(field.fixture).toHaveAttribute('data-changes', '123456:input-paste');
  });

  test('removes a character and moves focus to the previous slot with backspace', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234');
    await field.slot(1).focus();

    await page.keyboard.press('Backspace');

    await expect.poll(() => slotValues(field)).toBe('134___');
    await expect(field.slot(0)).toBeFocused();
    await expect(field.fixture).toHaveAttribute('data-changes', '134:keyboard');
  });

  test('does not fire `onValueChange` for Delete on an empty slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1');
    await field.slot(1).focus();

    await page.keyboard.press('Delete');
    // A later edit that does change the value marks that the Delete was handled first.
    await page.keyboard.type('2');

    await expect(field.fixture).toHaveAttribute('data-changes', '12:input-change');
    expect(await slotValues(field)).toBe('12____');
  });

  test('does not fire `onValueChange` for Backspace on an already-empty first slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await page.keyboard.press('Backspace');
    await page.keyboard.type('3');

    await expect(field.fixture).toHaveAttribute('data-changes', '3:input-change');
    await expect(field.slot(1)).toBeFocused();
  });

  test('does not move focus later for a stale controlled change', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'apply=9', 'otp-field-controlled');
    await field.slot(0).focus();
    await change(field.slot(0), '1');
    await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change');

    await field.slot(0).evaluate((input) => {
      // The button takes no focus, so the slot keeps it unless the field moves it.
      (document.querySelector('[data-testid="apply"]') as HTMLButtonElement).click();
      void input;
    });

    await expect.poll(() => slotValues(field)).toBe('9_____');
    await expect(field.slot(0)).toBeFocused();
  });

  test('moves focus after an asynchronously accepted controlled change', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'mode=defer', 'otp-field-controlled');
    await field.slot(0).focus();
    await change(field.slot(0), '1');
    await expect(field.fixture).toHaveAttribute('data-pending', '1');

    await field.slot(0).evaluate(() => (document.querySelector('[data-testid="accept"]') as HTMLButtonElement).click());

    await expect(field.slot(1)).toBeFocused();
  });

  test('updates standalone filled and focused state on the root', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await expect(field.root).not.toHaveAttribute('data-filled');
    await expect(field.root).not.toHaveAttribute('data-focused');

    await field.slot(0).focus();
    await expect(field.root).toHaveAttribute('data-focused', '');
    await page.keyboard.type('1');
    await expect(field.root).toHaveAttribute('data-filled', '');
    await field.outside.focus();

    await expect(field.root).not.toHaveAttribute('data-focused');
  });
});
