import { test, expect, holdCircuit } from './helpers';
import { gotoOtp, slotValues } from './otp-field.helpers';

// OTP (docs/components/otp-field.md) at L4, per render mode (issue 278, M6 track 7): typing, the optimistic advance of
// variant (ii) under the revision rule (ADR 04 Amendments 2026-11-18, decision 6 (a)), and the first callbacks.
test.describe('OtpField', () => {
  // The family's first red L4 title (decision 6 (a)): the second digit is typed while the first edit's update is held,
  // and nothing snaps back when it is let go. The edit report marks the arm; the module's update opens the gate.
  test('port: a held circuit does not snap the slots back after a second digit is typed', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Only the Server circuit can be held.');
    const hold = await holdCircuit(page, {
      armOn: (text) => text.includes('OnEdit'),
      // The otp-field module's update only: the form module's update also carries a revision.
      from: (text) => text.includes('update') && text.includes('slotPattern'),
    });
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    hold.arm();
    await page.keyboard.type('1');
    await expect(field.slot(1)).toBeFocused();
    // The form module's update of the hidden input comes just before the held otp-field update of the first edit.
    await expect(field.hidden).toHaveValue('1');
    await page.keyboard.type('2');
    await expect(field.slot(2)).toBeFocused();
    // Every write of slot 1's value from here on: a stale update would write it back to empty before the next one.
    await field.slot(1).evaluate((input) => {
      const log: string[] = [];
      (window as unknown as { slotWrites: string[] }).slotWrites = log;
      const value = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!;
      Object.defineProperty(input, 'value', {
        configurable: true,
        get() {
          return value.get!.call(this);
        },
        set(next: string) {
          log.push(next);
          value.set!.call(this, next);
        },
      });
    });
    await hold.release();

    await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change 12:input-change');
    await expect(field.slot(2)).toBeFocused();
    expect(await slotValues(field)).toBe('12____');
    await expect(field.hidden).toHaveValue('12');
    expect(await page.evaluate(() => (window as unknown as { slotWrites: string[] }).slotWrites)).not.toContain('');
  });

  test('fires `input-change` when typing', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await page.keyboard.type('1');

    await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change');
    await expect(field.slot(1)).toBeFocused();
  });

  test('fires when typing completes the OTP', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12345');
    await field.slot(5).focus();

    await page.keyboard.type('6');

    await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-change');
    await expect(field.slot(5)).toBeFocused();
    await expect(field.root).toHaveAttribute('data-complete', '');
  });
});
