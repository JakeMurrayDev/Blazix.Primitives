import { type Locator } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

// NumberFieldInput.test.tsx's [L4] titles (issue 277, 6b; NUM-KEY-03, 05..08, 12; NUM-PTR-05; NUM-FOC-02; NUM-EVT-16) on
// the `number-field-input` fixture's `a-` cases, Server then WebAssembly. Where Base UI dispatches a keydown and reads
// dispatchEvent's result (the gate's verdict), the keydown is dispatched in the page the same way; where it fires keys,
// changes and blurs to drive C#, the keys are real, `fill` is the change, and a blur is the element's blur().

async function gotoInput(page: Page, mode: RenderMode, inputCase: string, focus = true) {
  await page.goto(`/${mode}/number-field-input?case=${inputCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-input'));
  // The circuit may take longer than the expect timeout to become interactive under load (PR 308's run 44452252075).
  await fixture.waitFor({ state: 'attached' });
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  const input = fixture.getByRole('textbox');
  if (focus) {
    await input.focus();
  }
  return { fixture, input };
}

function dispatchKey(input: Locator, key: string): Promise<boolean> {
  return input.evaluate((el, k) => el.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true })), key);
}

function pressMinus(input: Locator, start: number, end: number | 'all'): Promise<boolean> {
  return input.evaluate(
    (el: HTMLInputElement, [s, e]) => {
      el.setSelectionRange(s as number, e === 'all' ? el.value.length : (e as number));
      return el.dispatchEvent(new KeyboardEvent('keydown', { key: '-', bubbles: true, cancelable: true }));
    },
    [start, end] as const,
  );
}

test('uses smallStep and snapOnStep when holding Alt with ArrowUp/ArrowDown', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'a-alt');

  await page.keyboard.press('Alt+ArrowUp');
  await expect(input).toHaveValue('0.3');

  await page.keyboard.press('Alt+ArrowDown');
  await expect(input).toHaveValue('0.2');
});

test('decrements with keyboard from numeric state, not rounded display text', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-precise');
  await expect(input).toHaveValue('1.235');

  // ArrowDown steps from the full-precision numeric value (0.23456), not the rounded display (0.235).
  await page.keyboard.press('ArrowDown');

  await expect(fixture).toHaveAttribute('data-changes', '0.23456/keyboard');
});

test('increments with keyboard from numeric state after a no-edit blur cycle', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-mirror');

  await page.keyboard.type('1.23456');
  await expect(fixture).toHaveAttribute('data-changes', / 1\.23456\/input-change$/);
  await input.blur();
  await expect(input).toHaveValue('1.235');

  await input.focus();
  await page.keyboard.press('ArrowUp');

  await expect(fixture).toHaveAttribute('data-changes', / 2\.23456\/keyboard$/);
});

test('does not commit a stale value when a synced keyboard step is canceled after an external change', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-cancel');

  // A prior committed keyboard step commits 1.
  await page.keyboard.press('ArrowUp');
  await expect(fixture).toHaveAttribute('data-commits', '1/keyboard');

  // The controlled value changes externally to 10; Base UI's fireEvent.click does not move the focus.
  await fixture.getByRole('button', { name: 'external' }).dispatchEvent('click');
  await expect(input).toHaveValue('10');

  // Canceling the next keyboard step must not commit the stale earlier value (1).
  await fixture.getByRole('button', { name: 'cancel' }).dispatchEvent('click');
  await expect(fixture.getByRole('button', { name: 'armed' })).toBeVisible();
  await page.keyboard.press('ArrowUp');

  await expect(fixture).toHaveAttribute('data-changes', '1/keyboard 11/keyboard');
  await expect(fixture).toHaveAttribute('data-commits', '1/keyboard');
  await expect(input).toHaveValue('10');
});

test('keeps dirty-input authority after a non-mutating key so the next keyboard step uses dirty text', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-lag');

  // Dirty text the parent never mirrors back into `value` (still 0).
  await input.fill('1.5');
  await expect(fixture).toHaveAttribute('data-changes', '1.5/input-change');

  // A non-mutating navigation key must not mark the input as synced, so ArrowUp steps from 1.5.
  await page.keyboard.press('ArrowLeft');
  await page.keyboard.press('ArrowUp');

  await expect(fixture).toHaveAttribute('data-changes', '1.5/input-change 2.5/keyboard');
});

test('keeps dirty-input authority after a non-mutating key so a button step uses dirty text', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-lag-inc');

  await input.fill('1.5');
  await expect(fixture).toHaveAttribute('data-changes', '1.5/input-change');
  await page.keyboard.press('ArrowLeft');

  // The button commits the dirty text before stepping, so it steps from 1.5, not 0.
  await fixture.getByRole('button', { name: 'Increase' }).click();

  await expect(fixture).toHaveAttribute('data-changes', /(^| )2\.5\/[a-z-]+$/);
});

test('commits dirty text on blur after a cursor key when the controlled value does not mirror it', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-lag');

  await input.fill('5');
  await expect(fixture).toHaveAttribute('data-changes', '5/input-change');

  // A cursor key must not clear dirty authority, so blur commits the typed text (5), not the stale numeric state (0).
  await page.keyboard.press('ArrowLeft');
  await input.blur();

  await expect(fixture).toHaveAttribute('data-changes', '5/input-change 5/input-blur');
});

test('allows unicode plus/minus, permille and fullwidth digits on keydown when formatted as percent', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'a-percent');

  expect(await dispatchKey(input, '−')).toBe(true); // MINUS SIGN U+2212
  expect(await dispatchKey(input, '＋')).toBe(true); // FULLWIDTH PLUS SIGN U+FF0B
  expect(await dispatchKey(input, '‰')).toBe(true);
  expect(await dispatchKey(input, '１')).toBe(true);
});

test('blocks percent and permille symbols on keydown when not formatted as percent', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'empty');

  expect(await dispatchKey(input, '%')).toBe(false);
  expect(await dispatchKey(input, '‰')).toBe(false);
});

test('allows space key when locale uses space-like grouping (pl-PL)', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-pl');

  // pl-PL grouping is a space-like character; a plain space from the keyboard is allowed.
  expect(await dispatchKey(input, ' ')).toBe(true);

  // A typical user value with a regular space as the group.
  await input.fill('1 234');
  await expect(fixture).toHaveAttribute('data-changes', '1234/input-change');
  await expect(input).toHaveValue('1 234');
});

test.describe('single sign character', () => {
  async function renderSigned(page: Page, mode: RenderMode) {
    const { input } = await gotoInput(page, mode, 'a-signed');
    await expect(input).toHaveValue('-5');
    return input;
  }

  test('blocks a second minus sign when the existing one is not selected', async ({ page, mode }) => {
    const input = await renderSigned(page, mode);
    expect(await pressMinus(input, 2, 2)).toBe(false);
  });

  test('allows a minus sign that replaces the selected existing sign', async ({ page, mode }) => {
    const input = await renderSigned(page, mode);
    expect(await pressMinus(input, 0, 1)).toBe(true);
  });

  test('allows a minus sign when the whole value is selected', async ({ page, mode }) => {
    const input = await renderSigned(page, mode);
    expect(await pressMinus(input, 0, 'all')).toBe(true);
  });
});

test.describe('focus selection', () => {
  // The field's data-focused is rendered once C# has applied the focus report, so the selection is read after it.
  test('keeps the selection the browser set when focus moves into the input', async ({ page, mode }) => {
    const { fixture, input } = await gotoInput(page, mode, 'a-hundred', false);

    // Tabbing into an input natively selects the whole value before the focus event fires.
    await input.evaluate((el: HTMLInputElement) => {
      el.setSelectionRange(0, el.value.length);
      el.focus();
    });
    await expect(fixture.getByTestId('field')).toHaveAttribute('data-focused', '');

    expect(await input.evaluate((el: HTMLInputElement) => [el.selectionStart, el.selectionEnd, el.value.length])).toEqual([0, 3, 3]);
  });
});

test('reports an unchanged value once when a paste event carries no clipboard data', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'a-twelve');

  await input.evaluate((el) => {
    const pasteEvent = new Event('paste', { bubbles: true, cancelable: true });
    Object.defineProperty(pasteEvent, 'clipboardData', { value: null });
    el.dispatchEvent(pasteEvent);
  });

  // Missing clipboard text splices an empty string in, so the text is unchanged; the paste still marks the input dirty,
  // and direct-entry reasons report even when the number is unchanged: exactly one `input-paste` change with 12.
  await expect(fixture).toHaveAttribute('data-changes', '12/input-paste');
  await expect(input).toHaveValue('12');
});
