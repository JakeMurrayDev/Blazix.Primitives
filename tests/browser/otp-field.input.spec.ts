import type { Locator } from '@playwright/test';
import { test, expect, readDiagnostics, type Page } from './helpers';
import { change, gotoOtp, paste, slotValues } from './otp-field.helpers';

// OTP (docs/components/otp-field.md) at L4 for Base UI's `OTPFieldInput.test.tsx` (issue 278, M6 track 7), per render
// mode. Base UI fires synthetic events; here keys are real (`page.keyboard`), typing a slot's whole text is `fill()` (one
// `input` event, as `fireEvent.change` sets it), a paste is a dispatched `ClipboardEvent` (tests/browser/CONTEXT.md), and a
// composition is CDP's `Input.imeSetComposition` (the `combobox.input.spec.ts` precedent). Titles marked `port:` restate a
// Base UI title the port cannot host as written; each says why.

async function selection(slot: Locator): Promise<[number | null, number | null]> {
  return slot.evaluate((input) => [(input as HTMLInputElement).selectionStart, (input as HTMLInputElement).selectionEnd]);
}

// A listener above the document: the module's stopPropagation() at the root node hides a handled key from it (design point
// 9 of N3, pending the user: ancestor element listeners still see it).
async function countWindowKeydowns(page: Page): Promise<void> {
  await page.evaluate(() => {
    const record = { count: 0 };
    (window as unknown as { keydowns: typeof record }).keydowns = record;
    window.addEventListener('keydown', () => {
      record.count += 1;
    });
  });
}

const windowKeydowns = (page: Page) => page.evaluate(() => (window as unknown as { keydowns: { count: number } }).keydowns.count);

async function compose(page: Page, text: string): Promise<void> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Input.imeSetComposition', { text, selectionStart: text.length, selectionEnd: text.length });
}

async function commitComposition(page: Page, text: string): Promise<void> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Input.insertText', { text });
}

test.describe('OtpFieldInput', () => {
  test('moves focus with arrow keys', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');
    await field.slot(1).focus();

    await page.keyboard.press('ArrowRight');
    await expect(field.slot(2)).toBeFocused();
    await page.keyboard.press('ArrowLeft');
    await expect(field.slot(1)).toBeFocused();
  });

  test('moves focus with arrow keys in RTL', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12', 'otp-field-rtl');
    await field.slot(1).focus();

    await page.keyboard.press('ArrowLeft');
    await expect(field.slot(2)).toBeFocused();
    await page.keyboard.press('ArrowRight');
    await expect(field.slot(1)).toBeFocused();
  });

  test('redirects focus to the first empty slot when a later empty slot is focused', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');

    await field.slot(4).focus();

    await expect(field.slot(2)).toBeFocused();
  });

  test('moves focus to the next slot after typing', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await page.keyboard.type('1');

    await expect(field.slot(1)).toBeFocused();
  });

  test('selects the last slot after typing into it for the first time', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12345');
    await field.slot(5).focus();

    await page.keyboard.type('6');

    await expect(field.fixture).toHaveAttribute('data-changes', '123456:input-change');
    await expect(field.slot(5)).toBeFocused();
    await expect.poll(() => selection(field.slot(5))).toEqual([0, 1]);
  });

  // ADR 05 Amendments 2026-11-18 item 1 (decision 9 (a)): typing commits regardless of Cancel(), which keeps only the focus
  // where it was (and, under the optimistic advance, rolls it back: design point 4 (a)). Base UI also keeps the slot empty.
  test('port: keeps focus in place when typing is canceled (the typed character commits, decision 9 (a))', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'cancel=input-change');
    await field.slot(0).focus();

    await page.keyboard.type('1');

    await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change');
    await expect(field.slot(0)).toBeFocused();
    expect(await slotValues(field)).toBe('1_____');
  });

  test('keeps the filled slot selected when typing an invalid character', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1');
    await field.slot(0).focus();

    await page.keyboard.type('a');

    await expect(field.fixture).toHaveAttribute('data-invalids', 'a:input-change');
    await expect(field.slot(0)).toHaveValue('1');
    await expect(field.slot(0)).toBeFocused();
    await expect.poll(() => selection(field.slot(0))).toEqual([0, 1]);
  });

  test('commits an IME composition once on compositionend instead of per intermediate change', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'type=alphanumeric');
    await field.slot(0).focus();

    await compose(page, 'd');
    await compose(page, 'dd');
    await compose(page, 'ddd');
    await expect(field.slot(0)).toHaveValue('ddd');
    await commitComposition(page, 'ddd');

    await expect(field.fixture).toHaveAttribute('data-changes', 'ddd:input-change');
    await expect.poll(() => slotValues(field)).toBe('ddd___');
    await expect(field.slot(3)).toBeFocused();
  });

  test('reports characters rejected from a committed IME composition', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();

    await compose(page, '1a');
    await expect(field.fixture).toHaveAttribute('data-invalids', '');
    await commitComposition(page, '1a');

    await expect(field.fixture).toHaveAttribute('data-invalids', '1a:input-change');
    await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change');
    await expect.poll(() => slotValues(field)).toBe('1_____');
    await expect(field.slot(1)).toBeFocused();
  });

  test('ignores keyboard commands while a composition is buffered', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'type=alphanumeric&value=12');
    await field.slot(2).focus();

    await compose(page, 'a');
    await page.keyboard.press('Backspace');
    await expect(field.slot(2)).toBeFocused();
    await commitComposition(page, '');

    await expect.poll(() => slotValues(field)).toBe('12____');
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });

  // Base UI's `setProps` is a fixture button pressed with a DOM click, so focus and the composition stay in the slot; the
  // composition's events are dispatched as Base UI fires them.
  for (const prop of ['disabled', 'readonly'] as const) {
    test(`does not commit a composition that ends after the field becomes ${prop === 'disabled' ? 'disabled' : 'readOnly'}`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, 'type=alphanumeric');
      await field.slot(0).focus();
      await field.slot(0).dispatchEvent('compositionstart');
      await change(field.slot(0), 'abc');

      await field.fixture.getByTestId(`make-${prop}`).evaluate((button) => (button as HTMLButtonElement).click());
      await expect(field.root).toHaveAttribute(`data-${prop}`, '');
      await field.slot(0).dispatchEvent('compositionend');

      await expect.poll(() => slotValues(field)).toBe('______');
      await expect(field.fixture).toHaveAttribute('data-changes', '');
    });
  }

  test('selects the slot value on mousedown', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1');

    await field.slot(0).dispatchEvent('mousedown');

    await expect(field.slot(0)).toBeFocused();
    expect(await selection(field.slot(0))).toEqual([0, 1]);
  });

  // OTP-PTR-02, ADR 01's consumer-veto row: the slot's onmousedown calls PreventPartHandler(). On WebAssembly the `press`
  // decision runs it inside the dispatch; on Server the decision falls back to run and the veto lands a round trip late.
  test('allows a composed mousedown handler to prevent focus', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'veto=mousedown');

    await field.slot(0).dispatchEvent('mousedown');

    await expect(field.fixture).toHaveAttribute('data-vetoes', 'mousedown');
    if (mode === 'wasm') {
      await expect(field.slot(0)).not.toBeFocused();
    } else {
      await expect(field.slot(0)).toBeFocused();
    }
  });

  test('allows a composed focus handler to prevent internal focus state', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'veto=focus');

    await field.slot(0).focus();

    await expect(field.fixture).toHaveAttribute('data-vetoes', 'focus');
    await expect(field.slot(0)).toBeFocused();
    if (mode === 'wasm') {
      await expect(field.root).not.toHaveAttribute('data-focused');
    } else {
      await expect(field.root).toHaveAttribute('data-focused', '');
    }
  });

  test('allows a composed blur handler to preserve internal focus state', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'veto=blur');
    await field.slot(0).focus();
    await expect(field.root).toHaveAttribute('data-focused', '');

    await field.outside.focus();

    await expect(field.fixture).toHaveAttribute('data-vetoes', 'blur');
    await expect(field.outside).toBeFocused();
    if (mode === 'wasm') {
      await expect(field.root).toHaveAttribute('data-focused', '');
    } else {
      // Server: relaxed (OTP §9, OTP-FOC-08): the blur is reported before the veto runs.
      await expect(field.root).not.toHaveAttribute('data-focused');
    }
  });

  test('moves focus to the next slot when typing the same character into a filled slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');
    await field.slot(1).focus();

    await page.keyboard.type('2');

    await expect(field.slot(2)).toBeFocused();
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });

  for (const [key, target] of [
    ['ArrowUp', 0],
    ['ArrowDown', 4],
  ] as const) {
    test(`moves focus to the field boundary with ${key}`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, 'value=1234');
      await field.slot(1).focus();

      await page.keyboard.press(key);

      await expect(field.slot(target)).toBeFocused();
    });
  }

  // OTP-KEY-05; design point 9 of N3 (pending the user, recommendation (a)): the module listens at the root node, so its
  // stopPropagation() hides the key from listeners above the document, not from ancestor elements.
  test('port: stops propagation when ArrowDown moves focus to the empty end slot (a window listener)', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234');
    await countWindowKeydowns(page);
    await field.slot(1).focus();

    await page.keyboard.press('ArrowDown');

    await expect(field.slot(4)).toBeFocused();
    expect(await windowKeydowns(page)).toBe(0);
  });

  test('keeps focus on the empty end slot with ArrowDown', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');
    await field.slot(2).focus();
    await countWindowKeydowns(page);

    await page.keyboard.press('ArrowDown');

    await expect(field.slot(2)).toBeFocused();
    expect(await windowKeydowns(page)).toBe(0);
  });

  test('keeps focus on the final slot with ArrowDown when the value is complete', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=123456');
    await field.slot(5).focus();

    await page.keyboard.press('ArrowDown');

    await expect(field.slot(5)).toBeFocused();
  });

  // Base UI spies on `select()`; the module selects through `setSelectionRange` (the `dom` helper), so that is the spy.
  test('does not reselect the final slot when typing the same character', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=123456');
    await field.slot(5).focus();
    await field.slot(5).evaluate((input) => {
      const record = { count: 0 };
      (window as unknown as { selects: typeof record }).selects = record;
      const original = (input as HTMLInputElement).setSelectionRange.bind(input);
      (input as HTMLInputElement).setSelectionRange = (...args: Parameters<HTMLInputElement['setSelectionRange']>) => {
        record.count += 1;
        original(...args);
      };
      (input as HTMLInputElement).select = () => {
        record.count += 1;
      };
    });

    await page.keyboard.type('6');

    await expect(field.slot(5)).toBeFocused();
    expect(await page.evaluate(() => (window as unknown as { selects: { count: number } }).selects.count)).toBe(0);
  });

  test('moves focus to the first slot with Home', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234');
    await field.slot(3).focus();

    await page.keyboard.press('Home');

    await expect(field.slot(0)).toBeFocused();
  });

  test('moves focus to the empty end slot with End', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234');
    await field.slot(0).focus();

    await page.keyboard.press('End');

    await expect(field.slot(4)).toBeFocused();
  });

  for (const [name, modifier] of [
    ['Ctrl', 'Control'],
    ['Cmd', 'Meta'],
  ] as const) {
    test(`moves focus to the field boundaries with ${name} + arrow keys`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, 'value=1234');
      await field.slot(2).focus();

      await page.keyboard.press(`${modifier}+ArrowLeft`);
      await expect(field.slot(0)).toBeFocused();
      await page.keyboard.press(`${modifier}+ArrowRight`);
      await expect(field.slot(4)).toBeFocused();
    });

    test(`moves focus to the field boundaries with ${name} + arrow keys in RTL`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, 'value=1234', 'otp-field-rtl');
      await field.slot(2).focus();

      await page.keyboard.press(`${modifier}+ArrowLeft`);
      await expect(field.slot(4)).toBeFocused();
      await page.keyboard.press(`${modifier}+ArrowRight`);
      await expect(field.slot(0)).toBeFocused();
    });

    test(`clears all slots with ${name} + Backspace`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, 'value=1234');
      await field.slot(2).focus();

      await page.keyboard.press(`${modifier}+Backspace`);

      await expect(field.fixture).toHaveAttribute('data-changes', '_:keyboard');
      await expect.poll(() => slotValues(field)).toBe('______');
      await expect(field.slot(0)).toBeFocused();
    });
  }

  test('keeps arrow and home/end navigation working in readonly mode', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234&readonly');
    await field.slot(1).focus();

    await page.keyboard.press('ArrowRight');
    await expect(field.slot(2)).toBeFocused();
    await page.keyboard.press('Home');
    await expect(field.slot(0)).toBeFocused();
    await page.keyboard.press('End');
    await expect(field.slot(4)).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(field.slot(0)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(field.slot(4)).toBeFocused();
  });

  test('leaves vertical arrow navigation unhandled in disabled mode', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12&disabled');
    await countWindowKeydowns(page);

    const results = await field.slot(1).evaluate((input) => [
      input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'ArrowUp' })),
      input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'ArrowDown' })),
    ]);

    expect(results).toEqual([true, true]);
    expect(await windowKeydowns(page)).toBe(2);
  });

  test('blocks Delete and Backspace from changing the value in readonly mode', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234&readonly');
    await field.slot(1).focus();

    await page.keyboard.press('Delete');
    await page.keyboard.press('Backspace');

    await expect(field.slot(1)).toBeFocused();
    expect(await slotValues(field)).toBe('1234__');
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });

  test('blocks paste from changing the value in readonly mode', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234&readonly');
    await field.slot(1).focus();

    await paste(field.slot(1), '99');

    await expect(field.slot(1)).toBeFocused();
    expect(await slotValues(field)).toBe('1234__');
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });

  test('warns in development when clipboard text cannot be read during paste handling', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');
    await field.slot(1).focus();

    await paste(field.slot(1), '', true);

    await expect
      .poll(async () => (await readDiagnostics(page, mode, field.fixture)).filter((d) => d.message.includes('could not read clipboard text')).length)
      .toBe(1);
    expect(await slotValues(field)).toBe('12____');
  });

  test('ignores a paste event when clipboard text is unavailable', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');

    const notCanceled = await field.slot(1).evaluate((input) => input.dispatchEvent(new Event('paste', { bubbles: true, cancelable: true })));

    expect(notCanceled).toBe(false);
    expect(await slotValues(field)).toBe('12____');
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });

  test('allows tabbing out of the field from the active slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode);
    await field.slot(0).focus();
    await page.keyboard.type('1');
    await expect(field.slot(1)).toBeFocused();

    await page.keyboard.press('Tab');

    await expect(field.outside).toBeFocused();
  });

  test('deletes the current character and moves focus to the previous slot on backspace', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234');
    await field.slot(1).focus();

    await page.keyboard.press('Backspace');

    await expect.poll(() => slotValues(field)).toBe('134___');
    await expect(field.slot(0)).toBeFocused();
  });

  test('deletes the previous filled slot when backspacing on an empty non-first slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');
    await field.slot(2).focus();

    await page.keyboard.press('Backspace');

    await expect.poll(() => slotValues(field)).toBe('1_____');
    await expect(field.slot(1)).toBeFocused();
  });

  test('keeps focus in place when backspace is canceled', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234&cancel=keyboard');
    await field.slot(1).focus();

    await page.keyboard.press('Backspace');

    await expect(field.fixture).toHaveAttribute('data-changes', '134:keyboard');
    expect(await slotValues(field)).toBe('1234__');
    await expect(field.slot(1)).toBeFocused();
  });

  test('deletes the current character with Delete without moving focus', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234');
    await field.slot(1).focus();

    await page.keyboard.press('Delete');

    await expect.poll(() => slotValues(field)).toBe('134___');
    await expect(field.slot(1)).toBeFocused();
    await expect.poll(() => selection(field.slot(1))).toEqual([0, 1]);
  });

  test('selects the previous slot value after backspacing into the first slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=12');
    await field.slot(1).focus();

    await page.keyboard.press('Backspace');

    await expect(field.slot(0)).toBeFocused();
    await expect.poll(() => selection(field.slot(0))).toEqual([0, 1]);
  });

  test('keeps focus in place when paste is canceled', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'cancel=input-paste');
    await field.slot(0).focus();

    await paste(field.slot(0), '1234');

    await expect(field.fixture).toHaveAttribute('data-changes', '1234:input-paste');
    expect(await slotValues(field)).toBe('______');
    await expect(field.slot(0)).toBeFocused();
  });

  test('replaces values from the middle when pasting into a later slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=123456');
    await field.slot(2).focus();

    await paste(field.slot(2), '99');

    await expect.poll(() => slotValues(field)).toBe('129956');
    await expect(field.slot(4)).toBeFocused();
  });

  test('applies a native label to every slot', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=native', 'otp-field-labels');

    for (let index = 0; index < 6; index += 1) {
      await expect(field.slot(index)).toHaveAccessibleName('Verification code');
    }
  });

  test('keeps the shared label on the first slot even if an aria-label is provided', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=shared', 'otp-field-labels');

    await expect(field.slot(0)).toHaveAccessibleName('Verification code');
    await expect(field.slot(0)).not.toHaveAttribute('aria-label', 'Character 1 of 6');
    await expect(field.slot(1)).toHaveAttribute('aria-label', 'Character 2 of 6');
  });

  test('warns when aria-label is provided on the first slot without an associated label', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'case=warn', 'otp-field-labels');

    await expect
      .poll(async () => (await readDiagnostics(page, mode, field.fixture)).filter((d) => d.message.includes('ignores `aria-label` on the first input')).length)
      .toBe(1);
  });

  for (const labelCase of ['shared', 'field-label'] as const) {
    test(`does not warn for a first-slot aria-label when ${labelCase === 'shared' ? 'a native label' : 'Field.Label'} is associated`, async ({ page, mode }) => {
      const field = await gotoOtp(page, mode, `case=${labelCase}`, 'otp-field-labels');
      await expect(field.slot(0)).toHaveAccessibleName('Verification code');

      const diagnostics = await readDiagnostics(page, mode, field.fixture);

      expect(diagnostics.filter((d) => d.message.includes('ignores `aria-label`'))).toEqual([]);
    });
  }
});
