import { type Locator } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

// The [L4] rows of docs/components/number-field.md that the steppers prove with a real mouse (issue 277, 6a): NUM-PTR-01..06,
// NUM-FOC-03, NUM-EDGE-13, over the `button` module's press-and-hold repeater (UBT-ALG-07) and the hold surface of decision
// 16 (a). Titles are Base UI's (number-field/root/useNumberFieldStepperButton.test.tsx, increment/NumberFieldIncrement.test.tsx),
// run once per stepper where Base UI runs them with describe.each. Base UI dispatches the events under fake timers; here the
// real mouse presses and moves, and the page's clock is paused and run forward (ADR 06 §2). The titles that type into the
// input, the touch and pen titles (track 1's CDP touch helper) and the commit titles (track 1's ValueCommittedEventArgs)
// follow in later commits.

const START_AUTO_CHANGE_DELAY = 400;
const CHANGE_VALUE_TICK_DELAY = 60;
const FREEZE_MARGIN = 50;

// The clock belongs to the worker's browser context: a title that paused it resumes it, or the next page's WebAssembly boot
// waits on its timers (tests/browser/CONTEXT.md).
test.afterEach(async ({ page }) => {
  await page.clock.resume();
});

/** Opens the hold fixture on a case and waits until both steppers have the button module attached. */
async function gotoHold(page: Page, mode: RenderMode, holdCase = 'basic'): Promise<Locator> {
  await page.goto(`/${mode}/number-field-hold?case=${holdCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-hold'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** Pauses the page's clock a margin ahead of the page's now, as tooltip.spec.ts's freezeClock does. */
async function freezeClock(page: Page): Promise<void> {
  for (let attempt = 1; ; attempt += 1) {
    const now = await page.evaluate(() => Date.now());
    try {
      await page.clock.pauseAt(now + FREEZE_MARGIN);
      return;
    } catch (error) {
      if (attempt === 3 || !String(error).includes('Cannot fast-forward to the past')) {
        throw error;
      }
    }
  }
}

/** Base UI's holdForThreeTicks: the start delay, then three repeats. */
async function holdForThreeTicks(page: Page): Promise<void> {
  await page.clock.runFor(START_AUTO_CHANGE_DELAY);
  await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
  await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
  await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
}

/** Moves the mouse to the middle of an element. */
async function hover(page: Page, element: Locator): Promise<void> {
  const box = (await element.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
}

/** Moves the mouse below the fixture's elements, outside every stepper. */
async function leave(page: Page, element: Locator): Promise<void> {
  const box = (await element.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height + 60);
}

for (const stepper of [
  { name: 'NumberField.Increment', testId: 'increment', steps: { 1: '1', 2: '2', 4: '4', 5: '5' }, boundaryCase: 'boundary', boundary: '10' },
  { name: 'NumberField.Decrement', testId: 'decrement', steps: { 1: '-1', 2: '-2', 4: '-4', 5: '-5' }, boundaryCase: 'boundary-down', boundary: '-10' },
] as const) {
  test.describe(`<${stepper.name} />`, () => {
    test('steps continuously when holding pointerdown', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await page.mouse.up();
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[4]);
      await expect(fixture).toHaveAttribute('data-changes', new RegExp(`/${stepper.testId}-press/${stepper.testId === 'increment' ? '1' : '-1'}$`));
    });

    test('does not step twice with pointerdown and click', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');

      await button.click();

      await expect(input).toHaveValue(stepper.steps[1]);
      await expect(fixture).toHaveAttribute('data-changes', new RegExp(`^${stepper.steps[1]}/${stepper.testId}-press/-?1$`));
    });

    test('stops calling onValueChange once the boundary is reached', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode, stepper.boundaryCase);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.boundary);
      await expect(fixture).toHaveAttribute('data-changes', `${stepper.boundary}/${stepper.testId}-press/${stepper.testId === 'increment' ? '1' : '-1'}`);

      await page.clock.runFor(START_AUTO_CHANGE_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.boundary);
      await expect(fixture).toHaveAttribute('data-changes', `${stepper.boundary}/${stepper.testId}-press/${stepper.testId === 'increment' ? '1' : '-1'}`);
      await expect(button).toBeDisabled();

      await page.mouse.up();
    });

    test('stops stepping after mouseleave', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await leave(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[4]);
      await page.mouse.up();
    });

    test('starts stepping again after mouseleave then mouseenter', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await leave(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await expect(input).toHaveValue(stepper.steps[4]);

      // Base UI ticks the clock 60 ms after the mouseenter; the re-entry's tick is immediate, so the value is one step on.
      await hover(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[5]);
      await page.mouse.up();
    });

    test('does not start stepping again after mouseleave then mouseenter after pointerup', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await page.mouse.up();
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await expect(input).toHaveValue(stepper.steps[4]);

      await leave(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await expect(input).toHaveValue(stepper.steps[4]);

      await hover(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[4]);
      await expect(fixture).toHaveAttribute('data-changes', /^(\S+ ){3}\S+$/);
    });

    test('cancels an active mouse press-and-hold interaction when disabled', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      // Base UI's setProps({ disabled: true }); a key on the fixture's control, so the mouse stays pressed on the button.
      await fixture.locator('[data-set-disabled]').dispatchEvent('keydown', { key: 'Shift' });
      await expect(input).toBeDisabled();

      await page.clock.runFor(START_AUTO_CHANGE_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[1]);
      await expect(fixture).toHaveAttribute('data-changes', /^\S+$/);
      await page.mouse.up();
    });

    test('does not step when readOnly', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode, 'readonly');
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await expect(button).toHaveAttribute('aria-disabled', 'true');

      // A real press: Playwright waits for an `aria-disabled` element to enable, so the click is forced.
      await button.click({ force: true });

      await expect(input).toHaveValue('');
      await expect(fixture).toHaveAttribute('data-changes', '');
    });

    test('does not step when root is disabled', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode, 'disabled');
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await expect(button).toBeDisabled();

      await button.dispatchEvent('click');

      await expect(input).toHaveValue('0');
      await expect(fixture).toHaveAttribute('data-changes', '');
    });
  });
}

test('places the caret at the end of the input when a mouse press focuses it', async ({ page, mode }) => {
  const fixture = await gotoHold(page, mode);
  const button = fixture.getByTestId('increment');
  const input = fixture.getByTestId('input');

  await hover(page, button);
  await page.mouse.down();

  await expect(input).toBeFocused();
  const { value, start, end } = await input.evaluate((element) => {
    const field = element as HTMLInputElement;
    return { value: field.value, start: field.selectionStart, end: field.selectionEnd };
  });
  expect(start).toBe(value.length);
  expect(end).toBe(value.length);
  await page.mouse.up();
});
