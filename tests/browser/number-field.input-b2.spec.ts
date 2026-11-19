import { type Locator } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

// The input role's [L4] rows of NumberFieldRoot.test.tsx's wheel, form, hidden-input, paste and key titles (issue 277, 6b;
// NUM-PTR-07..09, NUM-API-10, NUM-FRM-01..03, NUM-FOC-01/02, NUM-KEY-02..06, NUM-EVT-16, NUM-EDGE-06), on the
// `number-field-input` fixture's `b2-` cases and the `number-field-form` fixture, Server then WebAssembly.
// Base UI fires synthetic events and reads `fireEvent`'s return value (false when the default was prevented); here:
// - a wheel is an in-page `WheelEvent` (bubbling, cancelable), whose `dispatchEvent` return is the same signal;
// - a key whose default the title checks is an in-page `KeyboardEvent`, as `fireEvent.keyDown`, except the IME title's
//   composition keydown, a trusted CDP key event with keyCode 229 read by a listener the page adds after the module's;
// - a paste is a real Ctrl/Cmd+V of the clipboard the page wrote (the `combobox.root-b.spec.ts` precedent), where Base UI's
//   `pasteText` dispatches a `paste` event carrying `text/plain`;
// - typing is real key presses, where Base UI's `fireEvent.change` sets the text.
// A title whose Base UI assertions are only absences (no change, not prevented) ends with a positive `port:` step on the
// same field (the lock lifted, a key the gate does block, a wheel that does scrub), so the title proves the gate rather
// than an inert input.

async function gotoInput(page: Page, mode: RenderMode, inputCase: string, { focus = true } = {}) {
  await page.goto(`/${mode}/number-field-input?case=${inputCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-input'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  const input = fixture.getByRole('textbox');
  if (focus) {
    await input.focus();
  }
  return { fixture, input };
}

/** Dispatches a wheel event on the input, as `fireEvent.wheel`; returns false when its default was prevented. */
function wheel(input: Locator, init: { deltaX?: number; deltaY: number; shiftKey?: boolean; ctrlKey?: boolean }): Promise<boolean> {
  return input.evaluate(
    (element, eventInit) => element.dispatchEvent(new WheelEvent('wheel', { bubbles: true, cancelable: true, ...eventInit })),
    init,
  );
}

/** Dispatches a keydown on the input, as `fireEvent.keyDown`; returns false when its default was prevented. */
function keyDown(input: Locator, key: string): Promise<boolean> {
  return input.evaluate((element, k) => element.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true })), key);
}

/** Writes the clipboard and pastes it into the focused input with a real Ctrl/Cmd+V. */
async function paste(page: Page, text: string) {
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.evaluate((t) => navigator.clipboard.writeText(t), text);
  await page.keyboard.press('ControlOrMeta+v');
}

/** Sets the input's selection, as `input.setSelectionRange` / `input.select()`. */
function select(input: Locator, start: number, end: number) {
  return input.evaluate((element, [s, e]) => (element as HTMLInputElement).setSelectionRange(s!, e!), [start, end]);
}

function selection(input: Locator) {
  return input.evaluate((element) => [(element as HTMLInputElement).selectionStart, (element as HTMLInputElement).selectionEnd]);
}

/** Lifts the fixture's lock, focuses the input, and wheels until the module's newly attached listener prevents one. */
async function unlockAndWheel(fixture: Locator, input: Locator) {
  await fixture.getByTestId('b2-unlock').click();
  await expect(input).not.toBeDisabled();
  await input.focus();
  // The listener attaches when the module receives the unlocked options, a leg after the render; until then a wheel is
  // not prevented and changes nothing, so exactly one wheel scrubs.
  await expect.poll(() => wheel(input, { deltaY: 1 })).toBe(false);
}

test.describe('prop: allowWheelScrub', () => {
  test('does not scrub on wheel when disabled', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-wheel-disabled', { focus: false });

    expect(await wheel(input, { deltaY: 1 })).toBe(true);
    await expect(input).toHaveValue('5');
    await expect(fixture).toHaveAttribute('data-changes', '');

    // port: the same wheel scrubs once the field is enabled (NUM-PTR-09's listener attaches with it).
    await unlockAndWheel(fixture, input);
    await expect(input).toHaveValue('4');
    await expect(fixture).toHaveAttribute('data-changes', '4/wheel');
  });

  test('does not scrub on wheel when readOnly', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-wheel-readonly');

    expect(await wheel(input, { deltaY: 1 })).toBe(true);
    await expect(input).toHaveValue('5');
    await expect(fixture).toHaveAttribute('data-changes', '');

    // port: the same wheel scrubs once the field is editable.
    await unlockAndWheel(fixture, input);
    await expect(input).toHaveValue('4');
    await expect(fixture).toHaveAttribute('data-changes', '4/wheel');
  });

  test('should not allow the user to scrub the input value with the mouse wheel if `allowWheelScrub` is `false`', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-wheel-off');

    expect(await wheel(input, { deltaY: 1 })).toBe(true);
    await expect(input).toHaveValue('5');
    expect(await wheel(input, { deltaY: -5 })).toBe(true);
    await expect(input).toHaveValue('5');

    // port: the same wheel scrubs once allowWheelScrub is true.
    await unlockAndWheel(fixture, input);
    await expect(input).toHaveValue('4');
    await expect(fixture).toHaveAttribute('data-changes', '4/wheel');
  });

  test('does not scrub on wheel while pinch-zooming (ctrlKey)', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'wheel');

    expect(await wheel(input, { deltaY: 1, ctrlKey: true })).toBe(true);

    // port: the same wheel without ctrlKey scrubs, and is the only change.
    expect(await wheel(input, { deltaY: 1 })).toBe(false);
    await expect(input).toHaveValue('4');
    await expect(fixture).toHaveAttribute('data-changes', '4/wheel');
  });

  test('does not scrub on wheel when the input is not focused', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'wheel', { focus: false });

    expect(await wheel(input, { deltaY: 1 })).toBe(true);

    // port: the same wheel scrubs once the input is focused, and is the only change.
    await input.focus();
    expect(await wheel(input, { deltaY: 1 })).toBe(false);
    await expect(input).toHaveValue('4');
    await expect(fixture).toHaveAttribute('data-changes', '4/wheel');
  });

  test('does not scrub on a horizontal wheel event, and lets it scroll the page', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'wheel');

    expect(await wheel(input, { deltaY: 0, deltaX: 100 })).toBe(true);
    expect(await wheel(input, { deltaY: 0, deltaX: -100 })).toBe(true);
    // A precision touchpad emits sub-pixel noise on the cross axis during a sideways swipe.
    expect(await wheel(input, { deltaY: -0.5, deltaX: 100 })).toBe(true);
    expect(await wheel(input, { deltaY: 0.5, deltaX: -100 })).toBe(true);
    // An event with no movement at all.
    expect(await wheel(input, { deltaY: 0, deltaX: 0 })).toBe(true);
    await expect(input).toHaveValue('5');

    // port: a vertical wheel scrubs and commits, and is the only change and the only commit.
    expect(await wheel(input, { deltaY: 1 })).toBe(false);
    await expect(input).toHaveValue('4');
    await expect(fixture).toHaveAttribute('data-changes', '4/wheel');
    await expect(fixture).toHaveAttribute('data-commits', '4/wheel');
  });

  test('scrubs on a vertical wheel event that carries horizontal noise', async ({ page, mode }) => {
    const { input } = await gotoInput(page, mode, 'wheel');

    expect(await wheel(input, { deltaY: 1, deltaX: -0.5 })).toBe(false);
    await expect(input).toHaveValue('4');

    expect(await wheel(input, { deltaY: -1, deltaX: 0.5 })).toBe(false);
    await expect(input).toHaveValue('5');

    expect(await wheel(input, { deltaY: 1 })).toBe(false);
    await expect(input).toHaveValue('4');
  });

  test('uses largeStep when shift is held and the browser swaps the wheel axis', async ({ page, mode }) => {
    const { input } = await gotoInput(page, mode, 'b2-wheel-zero');

    // Chromium delivers shift + wheel as a horizontal event, so the horizontal delta carries the intended direction:
    // positive is "down", which steps the value down.
    expect(await wheel(input, { deltaY: 0, deltaX: -100, shiftKey: true })).toBe(false);
    await expect(input).toHaveValue('10');

    expect(await wheel(input, { deltaY: 0, deltaX: 100, shiftKey: true })).toBe(false);
    await expect(input).toHaveValue('0');

    // Cross-axis noise must not flip the direction of the same physical gesture.
    expect(await wheel(input, { deltaY: 0.5, deltaX: -100, shiftKey: true })).toBe(false);
    await expect(input).toHaveValue('10');

    expect(await wheel(input, { deltaY: -0.5, deltaX: 100, shiftKey: true })).toBe(false);
    await expect(input).toHaveValue('0');
  });

  test('uses largeStep when shift is held during wheel', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-wheel-zero');

    await wheel(input, { deltaY: -1, shiftKey: true });

    await expect(fixture).toHaveAttribute('data-changes', '10/wheel');
  });

  test('does not commit when a wheel step is a no-op at the boundary', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-wheel-max');

    await wheel(input, { deltaY: -1 });
    await expect(input).toHaveValue('5');

    // port: a wheel down steps and commits, and is the only change and the only commit (the barrier for the no-op).
    await wheel(input, { deltaY: 1 });
    await expect(fixture).toHaveAttribute('data-changes', '4/wheel');
    await expect(fixture).toHaveAttribute('data-commits', '4/wheel');
    await expect(input).toHaveValue('4');
  });

  test('syncs the visible input value when using the mouse wheel after pasting', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-wheel-ten');

    // Select the existing value so the paste replaces it rather than inserting at the caret.
    await input.selectText();
    await paste(page, '20');

    await expect(input).toHaveValue('20');
    await expect(fixture).toHaveAttribute('data-changes', '20/input-paste');

    await wheel(input, { deltaY: -1 });

    await expect(fixture).toHaveAttribute('data-changes', '20/input-paste 21/wheel');
    await expect(input).toHaveValue('21');
  });
});

test.describe('Form', () => {
  // NUM-FRM-01/02 (Base UI skips it under jsdom, which has no FormData): a capture listener reads the form's FormData at the
  // submit event, as "should not include formatting in the submitted value" does in number-field.spec.ts.
  test('should include the input value in the form submission', async ({ page, mode }) => {
    await page.goto(`/${mode}/number-field-form?case=b2-submit`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('number-field-form'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const form = fixture.locator('form');
    await form.evaluate((element) => {
      element.addEventListener('submit', () => element.setAttribute('data-submitted', String(new FormData(element as HTMLFormElement).get('test'))), { capture: true });
    });

    await fixture.getByText('Submit').click();
    await expect(form).toHaveAttribute('data-submitted', '');

    await fixture.getByRole('textbox').focus();
    await page.keyboard.type('50');
    // Base UI's fireEvent.change updates the hidden input before the click; here C#'s answer does, a leg later.
    await expect(fixture.locator('input[type=number]')).toHaveValue('50');
    await fixture.getByText('Submit').click();
    await expect(form).toHaveAttribute('data-submitted', '50');
  });

  test('prevents Form/Field submission when the value does not match the step', async ({ page, mode }) => {
    await page.goto(`/${mode}/number-field-form?case=b2-step`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('number-field-form'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const input = fixture.getByTestId('input');
    const hidden = fixture.locator('input[type=number]');

    await input.focus();
    await expect(fixture.getByTestId('error')).toHaveCount(0);

    await input.selectText();
    await page.keyboard.type('0.11');
    await expect(hidden).toHaveValue('0.11');
    await fixture.getByText('submit').click();

    await expect(fixture.getByTestId('error')).toHaveText('step mismatch');
    await expect(fixture).toHaveAttribute('data-form-submits', '');

    await input.selectText();
    await page.keyboard.type('0.1');
    await expect(hidden).toHaveValue('0.1');
    await fixture.getByText('submit').click();

    await expect(fixture).toHaveAttribute('data-form-submits', '0.1');
  });
});

test.describe('hidden input', () => {
  const hiddenInput = (fixture: Locator) => fixture.locator('input[aria-hidden][type=number]');

  test('forwards focus to the visible input', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-five', { focus: false });

    await hiddenInput(fixture).evaluate((element) => (element as HTMLInputElement).focus());

    await expect(input).toBeFocused();
  });

  test('places the caret at the end of the visible input when forwarding focus', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-hundred', { focus: false });
    await select(input, 0, 0);

    await hiddenInput(fixture).evaluate((element) => (element as HTMLInputElement).focus());

    await expect(input).toBeFocused();
    const length = (await input.inputValue()).length;
    expect(await selection(input)).toEqual([length, length]);
  });

  // Base UI's consumer onFocus is a React handler that calls `event.currentTarget.select()` in the dispatch; a C# @onfocus
  // runs a leg later on Server, so the consumer here is a focus listener the page adds to the input (after the module's).
  test('keeps a selection the consumer sets in onFocus when forwarding focus', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-hundred', { focus: false });
    await input.evaluate((element) => element.addEventListener('focus', (event) => (event.currentTarget as HTMLInputElement).select()));

    await hiddenInput(fixture).evaluate((element) => (element as HTMLInputElement).focus());

    await expect(input).toBeFocused();
    const length = (await input.inputValue()).length;
    expect(await selection(input)).toEqual([0, length]);
  });
});

test.describe('integration: exotic inputs and IME', () => {
  // Persian digits are not on Playwright's US layout (keyboard.type would insert them without a keydown), so each is a
  // trusted CDP key event carrying the digit as its key and text: a keydown through the gate, then the browser's insertion,
  // as user.type sends them.
  test('accepts Persian digit keyboard input', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'empty');

    const session = await page.context().newCDPSession(page);
    for (const digit of ['۱', '۲', '۳']) {
      await session.send('Input.dispatchKeyEvent', { type: 'keyDown', key: digit, text: digit, unmodifiedText: digit });
      await session.send('Input.dispatchKeyEvent', { type: 'keyUp', key: digit });
    }
    await session.detach();

    await expect(input).toHaveValue('۱۲۳');
    await expect(fixture).toHaveAttribute('data-changes', /(^| )123\/input-change$/);
  });

  for (const [label, text, value] of [
    ['Persian', '۱۲۳', 123],
    ['Arabic-Indic', '١٢٣', 123],
    ['fullwidth', '１２３', 123],
    ['Han', '一二三', 123],
  ] as const) {
    test(`pastes ${label} numerals through the input contract`, async ({ page, mode }) => {
      const { fixture, input } = await gotoInput(page, mode, 'basic');

      await input.selectText();
      await paste(page, text);

      await expect(input).toHaveValue(text);
      await expect(fixture).toHaveAttribute('data-changes', `${value}/input-paste`);
    });
  }

  test('rejects invalid pasted characters without changing the value contract', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-twelve');

    await input.selectText();
    await paste(page, 'abc');
    await expect(input).toHaveValue('12');

    // port: a valid paste over the same selection is the first change (the barrier for the rejected one).
    await paste(page, '7');
    await expect(fixture).toHaveAttribute('data-changes', '7/input-paste');
    await expect(input).toHaveValue('7');
  });

  test('allows composition key events (IME) without preventing default', async ({ page, mode }) => {
    const { input } = await gotoInput(page, mode, 'empty');
    await input.evaluate((element) => {
      const records: { keyCode: number; prevented: boolean }[] = [];
      (window as unknown as { keyRecords: typeof records }).keyRecords = records;
      // Read at the window, after the whole dispatch, as fireEvent's return value reports it: the module decides at the
      // input's root node, after the input's own listeners.
      window.addEventListener('keydown', (event) => {
        if (event.target === element) {
          records.push({ keyCode: (event as KeyboardEvent).keyCode, prevented: event.defaultPrevented });
        }
      });
    });

    // keyCode 229 marks a composition keydown. A single-character key that would otherwise be blocked, so only the
    // composition bypass lets it through; port: the same key without 229 is blocked.
    const session = await page.context().newCDPSession(page);
    for (const keyCode of [229, 65]) {
      await session.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'a', code: 'KeyA', windowsVirtualKeyCode: keyCode, nativeVirtualKeyCode: keyCode });
      await session.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'a', code: 'KeyA', windowsVirtualKeyCode: keyCode, nativeVirtualKeyCode: keyCode });
    }
    await session.detach();

    const records = await page.evaluate(() => (window as unknown as { keyRecords: { keyCode: number; prevented: boolean }[] }).keyRecords);
    expect(records).toEqual([
      { keyCode: 229, prevented: false },
      { keyCode: 65, prevented: true },
    ]);
  });
});

test.describe('pasting', () => {
  test('should not allow pasting an invalid number', async ({ page, mode }) => {
    const { input } = await gotoInput(page, mode, 'empty');

    await paste(page, 'abc');
    // Base UI's fireEvent.change after the paste: the text set from script and an input event, which the module reverts.
    await input.evaluate((element) => {
      const field = element as HTMLInputElement;
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, 'abc');
      field.dispatchEvent(new Event('input', { bubbles: true }));
    });
    await expect(input).toHaveValue('');
    await input.blur();
    await expect(input).toHaveValue('');
  });
});

test.describe('pasting at the caret', () => {
  test('does not paste into a readOnly field', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-readonly');

    await input.selectText();
    await paste(page, '9');
    await expect(input).toHaveValue('1');

    // port: the same paste replaces the selection once the field is editable, and is the only change.
    await fixture.getByTestId('b2-unlock').click();
    await expect(input).not.toHaveAttribute('readonly');
    await input.selectText();
    await paste(page, '9');
    await expect(input).toHaveValue('9');
    await expect(fixture).toHaveAttribute('data-changes', '9/input-paste');
  });

  test('inserts pasted text at the caret instead of replacing the whole value', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-123');

    await select(input, 3, 3);
    await paste(page, '5');

    await expect(input).toHaveValue('1235');
    await expect(fixture).toHaveAttribute('data-changes', '1235/input-paste');
  });

  // port: the change record shows the module's paste, not the browser's.
  test('replaces the selected range when pasting over a selection', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-123');

    await select(input, 1, 2);
    await paste(page, '9');

    await expect(input).toHaveValue('193');
    await expect(fixture).toHaveAttribute('data-changes', '193/input-paste');
  });

  test('keeps the caret just after the pasted text', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'b2-123');

    await select(input, 1, 2);
    await paste(page, '9');

    await expect(input).toHaveValue('193');
    // port: C#'s answer has arrived, so the caret is the one the module set after it.
    await expect(fixture).toHaveAttribute('data-changes', '193/input-paste');
    expect(await selection(input)).toEqual([2, 2]);
  });
});

test.describe('keys', () => {
  test('should allow navigation keys and not prevent their default behavior', async ({ page, mode }) => {
    const { input } = await gotoInput(page, mode, 'empty');
    await page.keyboard.type('123');

    for (const key of ['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'Tab', 'Enter']) {
      expect(await keyDown(input, key), key).toBe(true);
    }

    // port: a character the gate blocks is prevented.
    expect(await keyDown(input, 'a')).toBe(false);
  });

  test('does not prevent native caret movement for Home/End without min/max', async ({ page, mode }) => {
    const { input } = await gotoInput(page, mode, 'b2-five');

    for (const key of ['Home', 'End']) {
      expect(await keyDown(input, key), key).toBe(true);
    }
    await expect(input).toHaveValue('5');

    // port: a step key is prevented and steps.
    expect(await keyDown(input, 'ArrowUp')).toBe(false);
    await expect(input).toHaveValue('6');
  });

  test('does not swallow non-printing keys it does not handle', async ({ page, mode }) => {
    const { input } = await gotoInput(page, mode, 'b2-five');

    for (const key of ['PageUp', 'PageDown', 'Insert', 'F5']) {
      expect(await keyDown(input, key), key).toBe(true);
    }

    // port: a character the gate blocks is prevented.
    expect(await keyDown(input, 'a')).toBe(false);
  });
});
