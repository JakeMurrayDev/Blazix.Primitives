import { type Locator } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

// Batch B1 of the input role's [L4] rows (issue 277, 6b; NUM-FRM-03, NUM-FRM-04, NUM-KEY-03, NUM-KEY-07, NUM-KEY-09,
// NUM-PTR-01, NUM-PTR-02, NUM-PTR-05): NumberFieldRoot.test.tsx's titles whose assertion is native (constraint validation,
// a keydown the gate lets through) and useNumberFieldStepperButton.test.tsx's titles that type before stepping, on the
// `number-field-input` fixture's `b1-` cases, Server then WebAssembly. Base UI's fireEvent.change is Playwright's fill (the
// value set, then an input event, no keydown); its keyDown a real key; its pointerDown a real mouse press. Two dispatches
// stay synthetic, each said where it is.

const TOUCH_TIMEOUT_MARGIN = 50;

// The clock belongs to the worker's browser context: a title that paused it resumes it (tests/browser/CONTEXT.md).
test.afterEach(async ({ page }) => {
  await page.clock.resume();
});

async function gotoInput(page: Page, mode: RenderMode, inputCase: string) {
  await page.goto(`/${mode}/number-field-input?case=${inputCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-input'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  const input = fixture.getByRole('textbox');
  return { fixture, input, hidden: fixture.locator('input[type="number"][name="quantity"]') };
}

/** Records whether the next keydown of the input reached the document with its default prevented. */
async function recordKeyDown(input: Locator): Promise<void> {
  await input.evaluate((element) => {
    const record = (event: Event) => {
      if (event.target === element) {
        document.body.dataset.keydownPrevented = String(event.defaultPrevented);
      }
    };
    element.ownerDocument.addEventListener('keydown', record);
  });
}

async function keyDownPrevented(page: Page): Promise<string | undefined> {
  return page.evaluate(() => document.body.dataset.keydownPrevented);
}

async function validity(hidden: Locator) {
  return hidden.evaluate((element: HTMLInputElement) => ({
    stepMismatch: element.validity.stepMismatch,
    rangeOverflow: element.validity.rangeOverflow,
    formValid: element.form!.checkValidity(),
  }));
}

/** Pauses the page's clock a margin ahead of the page's now, as number-field.spec.ts's freezeClock does. */
async function freezeClock(page: Page): Promise<void> {
  for (let attempt = 1; ; attempt += 1) {
    const now = await page.evaluate(() => Date.now());
    try {
      await page.clock.pauseAt(now + TOUCH_TIMEOUT_MARGIN);
      return;
    } catch (error) {
      if (attempt === 3 || !String(error).includes('Cannot fast-forward to the past')) {
        throw error;
      }
    }
  }
}

async function hover(page: Page, element: Locator): Promise<void> {
  const box = (await element.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
}

test('blocks submission when step mismatch occurs', async ({ page, mode }) => {
  const { fixture, input, hidden } = await gotoInput(page, mode, 'b1-step');

  await input.fill('0.11');
  await expect(fixture).toHaveAttribute('data-changes', '0.11/input-change');

  await expect(hidden).toHaveValue('0.11');
  expect(await validity(hidden)).toMatchObject({ stepMismatch: true, formValid: false });
});

test('does not block submission when step="any"', async ({ page, mode }) => {
  const { fixture, input, hidden } = await gotoInput(page, mode, 'b1-step-any');

  await input.fill('0.11');
  await expect(fixture).toHaveAttribute('data-changes', '0.11/input-change');

  await expect(hidden).toHaveValue('0.11');
  expect(await validity(hidden)).toMatchObject({ stepMismatch: false, formValid: true });
});

test('allows typing a decimal while replacing a selection', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'b1-decimal');
  await input.focus();
  await expect(input).toHaveValue('12.3');

  const decimalIndex = (await input.inputValue()).indexOf('.');
  expect(decimalIndex).toBeGreaterThan(-1);
  await input.evaluate((element: HTMLInputElement, end) => element.setSelectionRange(1, end), decimalIndex + 2);
  await recordKeyDown(input);

  await page.keyboard.press('.');

  expect(await keyDownPrevented(page)).toBe('false');
  // The selection "2.3" is replaced by the typed separator.
  await expect(input).toHaveValue('1.');
  await expect(fixture).toHaveAttribute('data-changes', '1/input-change');
});

// .NET formats the currency (NUM-I18N-12's relaxation), so the typed text extends the input's own text.
test('accepts multi-character currency symbols while typing (e.g. pt-BR BRL)', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'b1-brl');
  const formatted = await input.inputValue();
  expect(formatted).toContain('R$');

  await input.fill(`${formatted}7`);

  await expect(fixture).toHaveAttribute('data-changes', '1234.567/input-change');
  await expect(input).toHaveValue(`${formatted}7`);
});

test('accepts exponent separators while typing (scientific notation)', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'b1-scientific');

  await input.fill('1.5E3');

  await expect(fixture).toHaveAttribute('data-changes', '1500/input-change');
  await expect(input).toHaveValue('1.5E3');
});

test('allows backspace to remove trailing currency symbol that follows a locale literal', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'b1-eur');
  const formatted = await input.inputValue();
  expect(formatted).toMatch(/€$/);
  await input.focus();
  await input.evaluate((element: HTMLInputElement) => element.setSelectionRange(element.value.length, element.value.length));
  await recordKeyDown(input);

  await page.keyboard.press('Backspace');

  expect(await keyDownPrevented(page)).toBe('false');
  await expect(fixture).toHaveAttribute('data-changes', '12.34/input-change');
  await expect(input).toHaveValue(formatted.slice(0, -1));
});

test('allows typing a negative value via keyboard when min is 0', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'b1-min0');
  await input.focus();
  await recordKeyDown(input);

  await page.keyboard.press('-');

  expect(await keyDownPrevented(page)).toBe('false');
  await expect(input).toHaveValue('-');
});

test('allows range overflow validation when true', async ({ page, mode }) => {
  const { fixture, input, hidden } = await gotoInput(page, mode, 'b1-overflow');

  await input.fill('6');
  await expect(fixture).toHaveAttribute('data-changes', '6/input-change');

  await expect(hidden).toHaveValue('6');
  expect(await validity(hidden)).toMatchObject({ rangeOverflow: true, formValid: false });
});

test('still clamps step interactions when true', async ({ page, mode }) => {
  const { fixture, input, hidden } = await gotoInput(page, mode, 'b1-overflow-step');

  const increase = fixture.getByLabel('Increase');
  // At max the stepper is disabled (NUM-STA-20), so a real click never lands; Base UI's fireEvent.click is dispatched.
  await expect(increase).toBeDisabled();
  await increase.dispatchEvent('click');

  await expect(input).toHaveValue('5');
  await expect(fixture).toHaveAttribute('data-changes', '');
  await expect(hidden).toHaveValue('5');
  expect(await validity(hidden)).toMatchObject({ rangeOverflow: false, formValid: true });
});

test('clamps to range when false', async ({ page, mode }) => {
  const { fixture, input, hidden } = await gotoInput(page, mode, 'b1-clamp');

  await input.fill('6');
  await expect(fixture).toHaveAttribute('data-changes', '5/input-change');

  await expect(hidden).toHaveValue('5');
  expect(await validity(hidden)).toMatchObject({ rangeOverflow: false, formValid: true });
});

for (const stepper of [
  { name: 'NumberField.Increment', testId: 'increment', reason: 'increment-press', steps: { 1: '1', 2: '2' }, fromDirty100: '101' },
  { name: 'NumberField.Decrement', testId: 'decrement', reason: 'decrement-press', steps: { 1: '-1', 2: '-2' }, fromDirty100: '99' },
] as const) {
  test.describe(`<${stepper.name} />`, () => {
    // Base UI's fireEvent.click is a keyboard or assistive-technology activation (detail 0); a real one would move the
    // focus to the button and blur the input first, so the click is dispatched to keep the input focused and dirty.
    test('steps when input is dirty but not blurred (click)', async ({ page, mode }) => {
      const { fixture, input } = await gotoInput(page, mode, 'b1-stepper');

      await input.fill('100');
      await expect(fixture).toHaveAttribute('data-changes', '100/input-change');
      await fixture.getByTestId(stepper.testId).dispatchEvent('click');

      await expect(input).toHaveValue(stepper.fromDirty100);
      await expect(input).toBeFocused();
    });

    test('steps when input is dirty but not blurred (pointerdown)', async ({ page, mode }) => {
      const { fixture, input } = await gotoInput(page, mode, 'b1-stepper');

      await input.fill('100');
      await expect(fixture).toHaveAttribute('data-changes', '100/input-change');
      await hover(page, fixture.getByTestId(stepper.testId));
      // The paused clock keeps the hold from repeating before the first step's answer arrives.
      await freezeClock(page);
      await page.mouse.down();

      await expect(input).toHaveValue(stepper.fromDirty100);
      await page.mouse.up();
    });

    test('ignores non-primary pointer buttons', async ({ page, mode }) => {
      // Controlled and pinned to 0, so the typed text stays out of sync with the stored value and any dirty-text sync from
      // the press would be reported.
      const { fixture, input } = await gotoInput(page, mode, 'b1-pinned');
      const button = fixture.getByTestId(stepper.testId);

      await input.fill('100');
      await expect(fixture).toHaveAttribute('data-changes', '100/input-change');
      // Base UI's fireEvent.pointerDown with button 1, dispatched: a real middle press is not prevented, so the browser
      // moves the focus to the button and the input's blur commits the dirty text, which the title does not exercise.
      await button.dispatchEvent('pointerdown', { button: 1, pointerType: 'mouse', isPrimary: true });

      await expect(input).toHaveValue('100');
      await expect(input).toBeFocused();
      // A keyboard click then syncs the dirty text once and steps from it: the middle press synced nothing before it.
      await button.dispatchEvent('click');
      await expect(fixture).toHaveAttribute('data-changes', `100/input-change 100/${stepper.reason} ${stepper.fromDirty100}/${stepper.reason}`);
    });

    // Base UI dispatches the tap's events itself, and one order (the click before touchend) is not one a touchscreen
    // produces; the events are dispatched in the page the same way, with the clock paused so the touch never settles into
    // a hold (the title's "before TOUCH_TIMEOUT").
    for (const { order, clickFirst } of [
      { order: 'click before touchend', clickFirst: true },
      { order: 'touchend before click', clickFirst: false },
    ]) {
      test(`always steps on quick touch (${order}, before TOUCH_TIMEOUT)`, async ({ page, mode }) => {
        const { fixture, input } = await gotoInput(page, mode, 'b1-stepper');
        const button = fixture.getByTestId(stepper.testId);
        await freezeClock(page);

        const tap = (enter: boolean) =>
          button.evaluate(
            (element, { first, enter: withEnter }) => {
              const touch = (type: string) =>
                element.dispatchEvent(typeof TouchEvent === 'function' ? new TouchEvent(type, { bubbles: true, cancelable: true }) : new Event(type, { bubbles: true, cancelable: true }));
              const click = () => element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, detail: 1 }));
              touch('touchstart');
              if (withEnter) {
                element.dispatchEvent(new MouseEvent('mouseenter', { bubbles: false }));
              }
              element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerType: 'touch', button: 0, isPrimary: true }));
              if (first) {
                click();
                touch('touchend');
              } else {
                touch('touchend');
                click();
              }
            },
            { first: clickFirst, enter },
          );

        await tap(true);
        await expect(input).toHaveValue(stepper.steps[1]);

        // No mouseenter occurs after the first focus.
        await tap(false);
        await expect(input).toHaveValue(stepper.steps[2]);
      });
    }
  });
}
