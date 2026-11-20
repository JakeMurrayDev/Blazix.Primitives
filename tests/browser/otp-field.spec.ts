import type { Route } from '@playwright/test';
import { test, expect, holdCircuit, Fixture, type Page } from './helpers';
import { change, gotoOtp, slotValues } from './otp-field.helpers';

// Holds one module's script until `release()` (the attach-order rule; the dialog.spec.ts precedent): the title asserts the
// route was hit, then releases it and asserts the full outcome. Fingerprinted names (`otp-field.<hash>.js`) match too.
async function stall(page: Page, module: string): Promise<{ hits: () => number; release: () => Promise<void> }> {
  const held: Route[] = [];
  let hits = 0;
  let released = false;
  await page.route(new RegExp(`/js/${module}(\\.[\\w-]+)?\\.js(\\?.*)?$`), (route) => {
    hits += 1;
    return released ? route.continue() : void held.push(route);
  });
  return {
    hits: () => hits,
    release: async () => {
      released = true;
      for (const route of held.splice(0)) {
        await route.continue();
      }
    },
  };
}

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

  // FOC-10 as written for a controlled field: no optimistic advance, so focus moves only with the committed value's
  // update. Held, the slot keeps focus; let go, focus follows.
  test('port: on a controlled field focus follows the committed value, a round trip after the key (OTP-FOC-10)', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Only the Server circuit can be held.');
    const hold = await holdCircuit(page, {
      armOn: (text) => text.includes('OnEdit'),
      from: (text) => text.includes('update') && text.includes('slotPattern'),
    });
    const field = await gotoOtp(page, mode, 'mode=mirror', 'otp-field-controlled');
    await field.slot(0).focus();

    hold.arm();
    await page.keyboard.type('1');
    await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change');
    await expect(field.slot(0)).toBeFocused();
    await hold.release();

    await expect(field.slot(1)).toBeFocused();
    expect(await slotValues(field)).toBe('1_____');
  });

  // Two handles on the root element (the attach-order rule of the standing rules): the composite list's and the
  // otp-field module's. Either module may load late; the field attaches once both have, with the slots' ids and values.
  for (const module of ['otp-field', 'composite'] as const) {
    test(`port: the field attaches and edits when the ${module} module loads late (two handles on the root)`, async ({ page, mode }) => {
      const held = await stall(page, module);
      await page.goto(`/${mode}/otp-field-basic`, { waitUntil: 'commit' });
      await page.locator(Fixture.started).waitFor({ state: 'attached' });
      const fixture = page.locator(Fixture.interactive('otp-field-basic')).first();
      await fixture.waitFor({ state: 'attached' });
      await expect.poll(held.hits).toBeGreaterThan(0);
      const slot = (index: number) => fixture.getByTestId(`slot-${index}`);
      await expect(slot(1)).toHaveAttribute('id', 'otp-2');

      await held.release();
      await expect(fixture).toHaveAttribute('data-attached', 'true');
      await slot(0).focus();
      await page.keyboard.type('12');

      await expect(fixture).toHaveAttribute('data-changes', '1:input-change 12:input-change');
      await expect(slot(2)).toBeFocused();
    });
  }

  // OTP-KEY-22, OTP-KEY-23 (OTPFieldInput.android.test.tsx): Android commits each change during a composition. The
  // platform is pinned by an init script, as context-menu.spec.ts pins `navigator.platform`; the events are dispatched as
  // Base UI fires them, since a real IME would follow the focus move.
  test('commits each change during an IME composition (Android)', async ({ page, mode }) => {
    await page.addInitScript(() => {
      Object.defineProperty(Navigator.prototype, 'userAgent', {
        configurable: true,
        get: () => 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36',
      });
      Object.defineProperty(Navigator.prototype, 'platform', { configurable: true, get: () => 'Linux armv8l' });
    });
    const field = await gotoOtp(page, mode, 'length=3&type=alphanumeric');
    await field.slot(0).focus();

    await field.slot(0).dispatchEvent('compositionstart');
    await change(field.slot(0), 'a');
    await expect(field.fixture).toHaveAttribute('data-changes', 'a:input-change');
    await expect(field.slot(1)).toBeFocused();
    await field.slot(0).dispatchEvent('compositionend');

    await expect(field.fixture).toHaveAttribute('data-changes', 'a:input-change');
    expect(await slotValues(field)).toBe('a__');
  });

  // OTP-EDGE-10: prerender and the first interactive render carry each slot's id, value, tabindex and maxlength.
  test('port: the prerendered slots carry the ids, values, tab stop and maxlength of the first interactive render (OTP-EDGE-10)', async ({ page, mode }) => {
    const html = await (await page.request.get(`/${mode}/otp-field-prerender`)).text();
    const tags = [...html.matchAll(/<input[^>]*data-testid="slot-\d"[^>]*>/g)].map((match) => match[0]);
    const read = (tag: string, name: string) => tag.match(new RegExp(` ${name}="([^"]*)"`))?.[1] ?? null;
    const prerendered = tags.map((tag) => [read(tag, 'id'), read(tag, 'value') || null, read(tag, 'tabindex'), read(tag, 'maxlength')]);
    expect(prerendered).toEqual([
      ['code', '1', '-1', '6'],
      ['code-2', '2', '-1', null],
      ['code-3', null, '0', null],
      ['code-4', null, '-1', null],
      ['code-5', null, '-1', null],
      ['code-6', null, '-1', null],
    ]);

    await page.goto(`/${mode}/otp-field-prerender`, { waitUntil: 'commit' });
    const fixture = page.locator(Fixture.container('otp-field-prerender'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const interactive = await fixture.locator('[data-testid^="slot-"]').evaluateAll((inputs) =>
      inputs.map((input) => [input.id, (input as HTMLInputElement).value || null, input.getAttribute('tabindex'), input.getAttribute('maxlength')]),
    );
    expect(interactive).toEqual(prerendered);
  });

  // ADR 05 Amendments 2026-11-18 item 2: @bind-Value inside an EditForm notifies the EditContext, takes the property's
  // name and shows the validator's messages as the field's invalid state.
  test('port: @bind-Value inside an EditForm validates the code and submits once it is valid @verifies ADR-05/5', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, '', 'otp-field-editcontext');
    await expect(field.hidden).toHaveAttribute('name', 'Code');
    await field.slot(0).focus();
    await page.keyboard.type('123');

    await field.fixture.getByRole('button', { name: 'Submit' }).click();
    await expect(field.fixture).toHaveAttribute('data-invalid-submits', '1');
    await expect(field.fixture.getByTestId('message')).toHaveText('The code has six digits.');
    await expect(field.root).toHaveAttribute('data-invalid', '');

    await field.slot(3).focus();
    await page.keyboard.type('456');
    await field.fixture.getByRole('button', { name: 'Submit' }).click();

    await expect(field.fixture).toHaveAttribute('data-valid-submits', '1');
    await expect(field.fixture.getByTestId('message')).toHaveCount(0);
    await expect(field.root).not.toHaveAttribute('data-invalid');
  });

  // OTP-FRM-14 (a `none` row; §16 item 9, an explicit re-sync): a native reset puts inputs back to their default values;
  // the module writes the slots and the hidden input back from the field's value one task later.
  test('port: a native form reset leaves the slots and the hidden input showing the field value (OTP-FRM-14)', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'form&name=otp&value=123');
    await field.slot(3).focus();
    await page.keyboard.type('4');
    await expect(field.fixture).toHaveAttribute('data-changes', '1234:input-change');

    await field.fixture.getByTestId('reset').click();

    await expect.poll(() => slotValues(field)).toBe('1234__');
    await expect(field.hidden).toHaveValue('1234');
  });
});
