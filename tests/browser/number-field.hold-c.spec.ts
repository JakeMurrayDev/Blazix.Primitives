import { type Locator } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

// Batch C's press-and-hold titles (issue 277, 6b; NUM-PTR-04): NumberFieldIncrement.test.tsx's and
// NumberFieldRoot.test.tsx's holds on the `number-field-hold` fixture's `basic` case (defaultValue 0), Server then
// WebAssembly, with a real mouse and the page's clock paused and run forward, as number-field.spec.ts does (ADR 06 §2).

const START_AUTO_CHANGE_DELAY = 400;
const CHANGE_VALUE_TICK_DELAY = 60;
const FREEZE_MARGIN = 50;

// The clock belongs to the worker's browser context: a title that paused it resumes it (tests/browser/CONTEXT.md).
test.afterEach(async ({ page }) => {
  await page.clock.resume();
});

async function gotoHold(page: Page, mode: RenderMode): Promise<Locator> {
  await page.goto(`/${mode}/number-field-hold?case=basic`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-hold'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** Pauses the page's clock a margin ahead of the page's now, as number-field.spec.ts's freezeClock does. */
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

async function hover(page: Page, element: Locator): Promise<void> {
  const box = (await element.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
}

async function leave(page: Page, element: Locator): Promise<void> {
  const box = (await element.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height + 60);
}

test.describe('<NumberField.Increment />', () => {
  // Base UI wraps the root in a <div onPointerUp={(event) => event.stopPropagation()}>; the fixture's container is the
  // root's ancestor here, and a native listener on it stops the pointerup before it reaches the window. The real mouse's
  // release is Base UI's fireEvent.pointerUp; its fireEvent.mouseUp stays a synthetic dispatch on the button, because
  // Chromium fires no compatibility mouseup after a pointerdown whose default the hold prevented (measured: a mouseup
  // listener on the button never ran), so in a browser the button's mouseup fallback, Base UI's and the port's alike,
  // is reached only by a dispatched mouseup.
  // As in Base UI, the button's own mouseup ends the hold and nothing commits (the commit is the window pointerup's). An
  // ArrowDown on the input (which the press focused) after the ticks is a barrier: every report reaches C# in order, so a
  // tick the hold had made would be recorded before the keyboard change.
  test('stops the hold on release when an ancestor stops pointerup propagation', async ({ page, mode }) => {
    const fixture = await gotoHold(page, mode);
    await fixture.evaluate((element) => element.addEventListener('pointerup', (event) => event.stopPropagation()));
    const button = fixture.getByTestId('increment');
    const input = fixture.getByTestId('input');
    await hover(page, button);
    await freezeClock(page);

    await page.mouse.down();
    await expect(input).toHaveValue('1');

    await page.mouse.up();
    await button.dispatchEvent('mouseup');

    await page.clock.runFor(START_AUTO_CHANGE_DELAY);
    await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
    await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

    await expect(input).toHaveValue('1');
    await expect(input).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(fixture).toHaveAttribute('data-changes', /^1\/increment-press\/1 0\/keyboard\/\S+$/);
    await expect(input).toHaveValue('0');
  });
});

test.describe('prop: onValueCommitted', () => {
  // Base UI counts the commits right after the pointerup. Here every report reaches C# in order, so an ArrowUp on the input
  // (which the press focused) after the release is a barrier: its keyboard commit is recorded after every commit the hold
  // made, and the hold's commits are exactly those before it.
  test('fires once per press-release even after mouseleave/mouseenter during a hold', async ({ page, mode }) => {
    const fixture = await gotoHold(page, mode);
    const increment = fixture.getByTestId('increment');
    const input = fixture.getByTestId('input');
    await hover(page, increment);
    await freezeClock(page);

    await page.mouse.down();
    await expect(input).toHaveValue('1');
    await leave(page, increment);
    await hover(page, increment);
    await page.mouse.up();

    await expect(input).toBeFocused();
    await page.keyboard.press('ArrowUp');

    await expect(fixture).toHaveAttribute('data-commits', /^\S+\/increment-press \S+\/keyboard$/);
  });
});
