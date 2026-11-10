// L4 for docs/components/select.md (M5 track 9, issue 173; L4 agent press): the press-and-release, drag-to-select, keyboard
// and focus titles of Base UI's select/item/SelectItem.test.tsx, select/trigger/SelectTrigger.test.tsx and
// select/item-indicator/SelectItemIndicator.test.tsx, run on Server then WebAssembly with real Playwright input.
//
// The jsdom-only "quick selection" describe of SelectItem.test.tsx fires `mouseDown`, `pointerMove` with a synthetic
// `movementY` and `mouseUp` under fake timers. Here the gesture is a real mouse: the button goes down on the trigger (the
// popup opens on the mousedown), the pointer enters an item from the popup's side padding (no vertical movement is counted
// over an item on the way in: the select module sums the `movementY` of the `pointermove` events over items), moves down by
// the title's `movementY` in real steps, and the release is a real `mouseup`. Base UI's fake-timer ticks (`clock.tickAsync`)
// are `page.clock.runFor` on a frozen page clock, which moves the select module's 400 ms selected-delay timer (SEL-PTR-05).
import { type Locator } from '@playwright/test';
import { expect, test, type Page } from './helpers';
import { gotoPopupFixture, pressControl, settle } from './suites/popupFixture';

// The clock belongs to the worker's browser context, which every page of the worker shares: a title that froze it resumes it,
// or the next page's WebAssembly boot waits on its timers.
test.afterEach(async ({ page }) => {
  await page.clock.resume().catch(() => undefined);
});

const FREEZE_MARGIN = 50;

/**
 * Pauses the page's clock: its timers and frames run only when the title runs it forward. The clock runs on while the
 * target travels to the browser, so the target sits a margin ahead, read again if the clock passed it on a slow runner.
 */
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

const option = (page: Page, name: string): Locator => page.getByRole('option', { name, exact: true });

/** The pointer position at the center of an element. */
async function centerOf(target: Locator): Promise<{ x: number; y: number }> {
  const box = (await target.boundingBox())!;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/**
 * Presses and holds the real mouse on the trigger. The press opens the popup on the next frame (SEL-PTR-01), so the frozen
 * clock runs two frames; the barrier then shows the Positioner's handle holds the open state, which starts the 400 ms timer
 * of the selected-delay gates (SEL-PTR-05) at the frozen time. Every `runFor` after this counts toward the 400 ms.
 */
async function holdTrigger(page: Page, fixture: Locator): Promise<void> {
  const { x, y } = await centerOf(page.getByTestId('trigger'));
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.clock.runFor(32);
  await expect(page.getByRole('listbox')).toBeVisible();
  await settle(fixture);
  await page.clock.runFor(16);
}

/**
 * Moves the held pointer onto an item from the popup's side padding, which is no item: the vertical movement counted over
 * items (SEL-PTR-06) is then only the steps. `from` is the pointer's vertical offset inside the item; every step moves the
 * pointer down by that many pixels in one real `mousemove`.
 */
async function dragOnto(page: Page, item: Locator, steps: number[], from = 8): Promise<void> {
  const popup = (await page.getByTestId('popup').boundingBox())!;
  const box = (await item.boundingBox())!;
  const x = box.x + 10;
  let y = box.y + from;
  await page.mouse.move(popup.x + 2, y);
  await page.mouse.move(x, y);
  for (const step of steps) {
    y += step;
    await page.mouse.move(x, y);
  }
}

test.describe('<Select.Item />', () => {
  // SEL-PTR-08 (SEL-DOM-18 and SEL-STR-08 are L2). Base UI fires `click` events on the trigger and the item (detail 0); a
  // real mouse click is the same activation with a pointerdown on the item.
  test('should select the item and close popup when clicked', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one', placeholder: '' });
    const value = page.getByTestId('value');
    await expect(value).toHaveText('');

    await page.getByTestId('trigger').click();
    await option(page, 'one').click();

    await expect(value).toHaveText('one');
    await expect(page.getByTestId('positioner')).not.toBeVisible();
  });

  // SEL-ARIA-14, SEL-FOC-02, SEL-FOC-03, SEL-KEY-06. Base UI opens with `fireEvent.click(trigger)` (a detail 0 click): the
  // real input of a detail 0 click on the trigger is Enter on the focused button (SEL-KEY-03).
  test('navigating with keyboard should focus item', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two|three', placeholder: '' });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');

    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(page.getByText('one', { exact: true })).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(page.getByText('two', { exact: true })).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(page.getByText('three', { exact: true })).toBeFocused();
  });

  // SEL-API-43, SEL-KEY-08 (same opening as above: Enter on the focused trigger stands for the detail 0 click).
  test('should select item when Enter key is pressed', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', placeholder: '' });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');
    await expect(option(page, 'one')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'two')).toBeFocused();
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('value')).toHaveText('two');
  });

  // SEL-API-41, SEL-KEY-01, SEL-KEY-06, SEL-KEY-20.
  test('should focus disabled items', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', disabledItems: 'two', placeholder: '' });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('ArrowDown');

    await expect(option(page, 'one')).toBeFocused();

    await page.keyboard.press('ArrowDown');

    await expect(option(page, 'two')).toBeFocused();
  });

  // SEL-API-41, SEL-KEY-08, SEL-PTR-08, SEL-PTR-10. An `aria-disabled` item is clicked with `force` (Playwright waits for it
  // to be enabled); the click is still a real mouse click at the item.
  test('should not select disabled item', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', disabledItems: 'two', placeholder: '' });
    await page.getByTestId('trigger').click();
    await option(page, 'two').click({ force: true });
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-value-changes', '');
    await expect(page.getByTestId('value')).toHaveText('');
    await expect(page.getByRole('listbox')).toBeVisible();
  });

  // SEL-API-41, SEL-KEY-08.
  test('should not select disabled item when Enter is pressed', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', disabledItems: 'two', placeholder: '' });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'one')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'two')).toBeFocused();

    await page.keyboard.press('Enter');
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-value-changes', '');
    await expect(page.getByTestId('value')).toHaveText('');
  });

  // SEL-API-41, SEL-EDGE-04, SEL-PTR-10 (SEL-API-13 and SEL-DOM-35 are L2).
  test('inherits the disabled state from the root', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-item', {
      open: true,
      disabled: true,
      items: 'one',
      clickItems: 'one',
      placeholder: '',
    });
    const item = option(page, 'one');
    await expect(item).toHaveAttribute('data-disabled');

    await item.click({ force: true });
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-item-clicks', '');
    await expect(page.getByTestId('value')).toHaveText('');
  });

  // SEL-PTR-08, SEL-PTR-12. Base UI's `fireEvent.click` on the trigger and the item are real clicks here.
  test('should call onClick exactly once for a regular click', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', clickItems: 'one', placeholder: '' });

    await page.getByTestId('trigger').click();
    await option(page, 'one').click();

    await expect(page.getByTestId('value')).toHaveText('one');
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-item-clicks', 'one');
    await expect(fixture).toHaveAttribute('data-value-changes', 'one:item-press');
  });

  // SEL-PTR-08. Base UI fires pointerDown, mouseDown, mouseUp and a `click` (detail 1) on the item: Playwright's click on the
  // item is that sequence from a real mouse.
  test('should select an unhighlighted item with the mouse', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-item', { defaultOpen: true, hover: false });
    const item = option(page, 'two');
    await expect(item).toBeVisible();
    await expect(item).not.toHaveAttribute('data-highlighted');

    await item.click();

    await expect(page.getByTestId('value')).toHaveText('two');
  });

  // SEL-PTR-08. Base UI's pointer and mouse events on the trigger and the item are a real click on the trigger, a real hover
  // and a real click.
  test('should highlight a hovered item and commit it with a mouse click', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two|three' });

    await page.getByTestId('trigger').click();
    await expect(page.getByRole('listbox')).toBeVisible();
    const item = option(page, 'two');

    await item.hover();
    await expect(item).toHaveAttribute('data-highlighted');

    await item.click();

    await expect(page.getByTestId('value')).toHaveText('two');
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-value-changes', 'two:item-press');
  });

  // SEL-PTR-08. A `click` with `detail: 0` and no pointer data on an item that is not highlighted: no real input produces
  // it (Enter and Space click the focused, so highlighted, item; a pointer click carries a pointerdown), so the click is
  // the DOM's own `element.click()` (a `MouseEvent` with `detail: 0` and no `pointerType`), as Base UI's `fireEvent.click`.
  test('should ignore an unhighlighted item with a generic virtual click', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { defaultOpen: true, hover: false, placeholder: '' });
    const item = option(page, 'two');
    await expect(item).toBeVisible();
    await expect(item).not.toHaveAttribute('data-highlighted');

    await item.evaluate((element) => element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, detail: 0 })));
    await settle(fixture);

    await expect(page.getByTestId('value')).toHaveText('');
    await expect(fixture).toHaveAttribute('data-value-changes', '');
  });

  // SEL-FOC-02. Base UI's `fireEvent.click` on the trigger and `user.click` on the item are real clicks.
  test('should focus the selected item upon opening the popup', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two|three' });
    const trigger = page.getByTestId('trigger');

    await trigger.click();
    await option(page, 'three').click();
    await expect(page.getByRole('listbox')).toHaveCount(0);

    await trigger.click();

    await expect(option(page, 'three')).toBeFocused();
  });

  // SEL-KEY-09, SEL-STA-32.
  test('should allow pointer click after a typeahead sequence that ends with Space', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-item', { defaultOpen: true, items: 'one:Item One|two:Item Two' });
    const first = page.getByRole('option').first();
    const second = page.getByRole('option').nth(1);
    await expect(second).toBeVisible();

    await first.focus();
    await page.keyboard.type('item t ');

    await expect(second).toBeFocused();

    await second.click();

    await expect(page.getByTestId('value')).toHaveText('two');
  });

  // SEL-PTR-05..09, SEL-PTR-11, SEL-PTR-12, SEL-PTR-22: Base UI's jsdom-only 'quick selection' describe (fake timers).
  test.describe('quick selection', () => {
    // SEL-PTR-05. The click is a real click after the clock ran 500 ms past the open (the gates are open, so the release over
    // the item could select on its own: the click must still activate once).
    test('calls onClick once for a click made after the opening delay', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { align: false, items: 'a|b', clickItems: 'b', placeholder: '' });
      const trigger = page.getByTestId('trigger');

      await trigger.click();
      const optionB = option(page, 'b');
      await expect(optionB).toBeVisible();
      await settle(fixture);
      await page.clock.runFor(500);
      await optionB.click();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-item-clicks', 'b');
    });

    // SEL-PTR-01. Base UI: `fireEvent.mouseDown(trigger)`, `fireEvent.mouseMove(option)` (no pointer event, so no drag), tick
    // 250, `fireEvent.mouseUp(option)`. Here: a held real mouse enters the item with no vertical movement, 250 ms, a real release.
    test('should not select an item on quick mouseup when showing a placeholder (no null item)', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', {
        rootItems: true,
        items: 'sans:Sans-serif|serif:Serif|mono:Monospace|cursive:Cursive',
      });
      const value = page.getByTestId('value');
      await expect(value).toHaveText('Select font');
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'Sans-serif'), []);
      await page.clock.runFor(250);
      await page.mouse.up();
      await settle(fixture);

      await expect(value).toHaveText('Select font');
      await expect(fixture).toHaveAttribute('data-value-changes', '');
    });

    // SEL-PTR-05, SEL-PTR-22. Aligned (the positioner's default); the released item is the unselected one.
    test('should not select an unselected item within the selected delay when aligned with the trigger', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { defaultValue: 'one', items: 'one|two' });
      const value = page.getByTestId('value');
      await expect(value).toHaveText('one');
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'two'), []);
      await page.clock.runFor(250);
      await page.mouse.up();
      await settle(fixture);

      await expect(value).toHaveText('one');
      await expect(fixture).toHaveAttribute('data-value-changes', '');
    });

    // SEL-PTR-06. Base UI: pointerEnter and four `pointerMove` with `movementY: 2` over the item, tick 100, mouseUp. Here:
    // the held pointer enters the item and moves down 2 px four times (the select module sums 8 px: 64 >= 64).
    test('should call onClick when selecting via drag-to-select (mousedown on trigger, mouseup on item)', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', clickItems: 'one' });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'one'), [2, 2, 2, 2]);
      await page.clock.runFor(100);
      await page.mouse.up();

      await expect(page.getByTestId('value')).toHaveText('one');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-item-clicks', 'one');
    });

    // SEL-PTR-07, SEL-PTR-10.
    test('does not fire click handlers on a disabled item during drag-to-select', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', disabledItems: 'one', clickItems: 'one' });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'one'), [2, 2, 2, 2]);
      await page.clock.runFor(100);
      await page.mouse.up();
      await settle(fixture);

      // Releasing over a disabled item must not synthesize a click on it at all.
      await expect(fixture).toHaveAttribute('data-item-clicks', '');
    });

    // SEL-PTR-07.
    test('should not select a disabled item via drag-to-select', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', disabledItems: 'one' });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'one'), [2, 2, 2, 2]);
      await page.clock.runFor(100);
      await page.mouse.up();
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-value-changes', '');
      await expect(page.getByTestId('value')).toHaveText('Select font');
    });

    // SEL-PTR-07, SEL-PTR-09. Base UI: `fireEvent.pointerEnter(option, { pointerType: 'touch' })`, tick 500, `fireEvent.mouseUp`.
    // Here: with the mouse button still held from the trigger, a real touch (CDP `Input.dispatchTouchEvent`) lands on the item
    // and drags off it (no compatibility mouse events follow a drag), so the item's last pointer type is `touch`; then the held
    // mouse button is released over the item, a real `mouseup` that follows a touch interaction.
    test('should not select on mouseup that follows a touch interaction', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two' });
      const cdp = await page.context().newCDPSession(page);
      await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 1 });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      const item = option(page, 'one');
      await dragOnto(page, item, []);
      const { x, y } = await centerOf(item);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x, y: y + 24 }] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
      await page.clock.runFor(500);
      await page.mouse.up();
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-value-changes', '');
      await expect(page.getByTestId('value')).toHaveText('Select font');
    });

    // SEL-PTR-06. Base UI: pointer moves of `movementY: 4` over items two and three, tick 100, `mouseUp(optionThree)`. Here:
    // the held pointer enters item two near its bottom edge, moves down 4 px inside it, then 4 px into item three.
    test('should accumulate drag-to-select movement across items', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { defaultValue: 'one', items: 'one|two|three' });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      const two = option(page, 'two');
      const height = (await two.boundingBox())!.height;
      await dragOnto(page, two, [4, 4], height - 6);
      await page.clock.runFor(100);
      await page.mouse.up();

      await expect(page.getByTestId('value')).toHaveText('three');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-value-changes', 'three:item-press');
    });

    // SEL-PTR-06. Base UI: one `pointerMove` of `movementY: 8`, tick 500, mouseUp. Here: one real 8 px step down.
    test('should select via drag-to-select when hover highlighting is disabled', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { hover: false, items: 'one|two' });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      const item = option(page, 'two');
      await dragOnto(page, item, [8]);
      await expect(item).not.toHaveAttribute('data-highlighted');
      await page.clock.runFor(500);
      await page.mouse.up();

      await expect(page.getByTestId('value')).toHaveText('two');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-value-changes', 'two:item-press');
    });

    // SEL-PTR-06. Base UI: one `pointerMove` of `movementY: 2`, tick 100, mouseUp. Here: one real 2 px step down.
    test('should not treat small pointer movement as drag-to-select', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { defaultValue: 'one', items: 'one|two' });
      const value = page.getByTestId('value');
      await expect(value).toHaveText('one');
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'two'), [2]);
      await page.clock.runFor(100);
      await page.mouse.up();
      await settle(fixture);

      await expect(value).toHaveText('one');
      await expect(fixture).toHaveAttribute('data-value-changes', '');
    });

    // SEL-PTR-05. Base UI: one `pointerMove` of `movementY: 2`, tick 500, mouseUp.
    test('should allow small pointer movement to select after the opening delay', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { defaultValue: 'one', items: 'one|two' });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'two'), [2]);
      await page.clock.runFor(500);
      await page.mouse.up();

      await expect(page.getByTestId('value')).toHaveText('two');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-value-changes', 'two:item-press');
    });

    // SEL-PTR-08, SEL-PTR-22. Base UI: pointerDown and mouseDown on the trigger, then `mouseUp(option)` and a `click` with
    // detail 1 dispatched on the option. A real release over the item is dispatched to the item as a `mouseup`, but Chromium
    // sends the `click` of a press that began on the trigger to the common ancestor (the item gets none): the title's own
    // `click` on the item is dispatched after the real release, as Base UI's `fireEvent.click`.
    test('should ignore an opening click that did not start on the item', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { hover: false, items: 'one|two' });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      const item = option(page, 'one');
      await dragOnto(page, item, []);
      await page.mouse.up();
      await item.evaluate((element) => element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, detail: 1 })));
      await settle(fixture);

      await expect(page.getByTestId('value')).toHaveText('Select font');
      await expect(fixture).toHaveAttribute('data-value-changes', '');
    });

    // SEL-PTR-07, SEL-PTR-11. Base UI: one `pointerMove` of `movementY: 8`, tick 500, mouseUp. The consumer's onclick calls
    // PreventPartHandler() (Base UI's `preventBaseUIHandler()`): the item is not selected, the popup stays open.
    test('should not select item when onClick calls preventBaseUIHandler during drag-to-select', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-item', { items: 'one|two', clickItems: 'one', prevent: true });
      await freezeClock(page);

      await holdTrigger(page, fixture);
      await dragOnto(page, option(page, 'one'), [8]);
      await page.clock.runFor(500);
      await page.mouse.up();
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-item-clicks', 'one');
      await expect(page.getByTestId('value')).toHaveText('Select font');
      await expect(page.getByRole('listbox')).toBeVisible();
    });
  });
});

test.describe('<Select.Trigger />', () => {
  // SEL-DIS-09, SEL-FOC-07, SEL-POS-03. Base UI's `fireEvent.focus(trigger)` is the trigger's real focus (`focus()`).
  test('closes an aligned popup when focus lands on the trigger so it is not obscured', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'aligned-open' });
    const trigger = page.getByTestId('trigger');

    // `data-side="none"` proves the popup is still item-aligned over the trigger.
    await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'none');

    await trigger.focus();

    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:none');
  });

  // SEL-FOC-07.
  test('keeps a non-aligned popup open when focus lands on the trigger', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'non-aligned-open' });
    const trigger = page.getByTestId('trigger');

    await trigger.focus();
    await settle(fixture);

    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await expect(fixture).toHaveAttribute('data-open-changes', '');
  });

  test.describe('releasing the opening press', () => {
    // The trigger sits at left 100 px, top 100 px, 100 × 40 px (the bounds Base UI pins on `getBoundingClientRect`). The press
    // is held "long enough for the trigger to start watching its release": the 0 ms macrotask of SEL-PTR-04 runs under the
    // frozen clock.
    async function pressTrigger(page: Page, fixture: Locator): Promise<Locator> {
      const trigger = page.getByTestId('trigger');
      expect(await trigger.boundingBox()).toMatchObject({ x: 100, y: 100, width: 100, height: 40 });
      await freezeClock(page);
      await page.mouse.move(150, 120);
      await page.mouse.down();
      await page.clock.runFor(32);
      await expect(trigger).toHaveAttribute('aria-expanded', 'true');
      await settle(fixture);
      await page.clock.runFor(20);
      return trigger;
    }

    // SEL-PTR-04. Base UI releases with `fireEvent.mouseUp(document.body, { clientX: 96, clientY: 120 })`: a real release at
    // that point (the page's internal backdrop is under it).
    test('keeps the popup open when the press slips up to 5px off the trigger', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'release' });
      const trigger = await pressTrigger(page, fixture);

      await page.mouse.move(96, 120);
      await page.mouse.up();
      await page.clock.runFor(20);
      await settle(fixture);

      await expect(trigger).toHaveAttribute('aria-expanded', 'true');
      await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press');
    });

    // SEL-DIS-05, SEL-PTR-04.
    test('closes the popup when the press is dragged off the trigger and released', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'release' });
      const trigger = await pressTrigger(page, fixture);

      await page.mouse.move(94, 120);
      await page.mouse.up();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press false:cancel-open');
    });
  });

  test.describe('disabled state', () => {
    // SEL-KEY-19 (SEL-API-24 and SEL-DOM-07 are L2). Base UI presses Tab once: a disabled native button is not focusable.
    test('cannot be focused when disabled', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'disabled' });
      const trigger = page.getByTestId('trigger');
      await expect(trigger).toHaveAttribute('data-disabled');

      await page.keyboard.press('Tab');

      await expect(trigger).not.toBeFocused();
    });

    // SEL-PTR-21 (SEL-API-24 is L2). Base UI's `fireEvent.click` is a real mouse click at the disabled button's position
    // (Playwright's `click` waits for a button to be enabled).
    test('does not toggle the popup when disabled', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'disabled' });
      const { x, y } = await centerOf(page.getByTestId('trigger'));

      await page.mouse.click(x, y);
      await settle(fixture);

      await expect(page.getByRole('listbox')).toHaveCount(0);
      await expect(fixture).toHaveAttribute('data-open-changes', '');
    });
  });

  test.describe('style hooks', () => {
    // SEL-DIS-02, SEL-POS-01, SEL-STA-20 (SEL-DOM-05 and SEL-STA-28 are L2). Base UI's `user.click(document.body)` is a real
    // click on the page, which the internal backdrop receives.
    test('sets data-popup-side to the current popup side', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'side-right' });
      const trigger = page.getByTestId('trigger');
      await expect(trigger).not.toHaveAttribute('data-popup-side');

      await trigger.click();

      await expect(page.getByRole('listbox')).toBeVisible();
      await expect(trigger).toHaveAttribute('data-popup-side', 'right');

      await page.mouse.click(5, 5);

      await expect(page.getByRole('listbox')).toHaveCount(0);
      await expect(trigger).not.toHaveAttribute('data-popup-side');
    });

    // SEL-POS-15 (SEL-DOM-05 and SEL-STA-28 are L2).
    test('sets data-popup-side to the resolved side when alignItemWithTrigger is active', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-press-trigger', { case: 'resolved-side' });
      const trigger = page.getByTestId('trigger');

      await trigger.click();

      await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'none');
      await expect(trigger).toHaveAttribute('data-popup-side', 'bottom');
    });
  });
});

test.describe('<Select.ItemIndicator />', () => {
  // SEL-ANI-12. The exit's `animationstart` is recorded with the ending style the indicator carried then (a 150 ms keyframe
  // on `[data-ending-style]`, `?animations=1`), so the title shows the exit ran before it asserts the state settled.
  test('settles out of its transition state after the item is deselected', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-press-indicator', { case: 'settle', animations: 1 });
    const indicator = page.getByTestId('indicator');
    await expect(indicator).toHaveAttribute('data-selected');
    await indicator.evaluate((element) => {
      const starts: boolean[] = [];
      (window as unknown as { indicatorExits: boolean[] }).indicatorExits = starts;
      element.addEventListener('animationstart', () => starts.push(element.hasAttribute('data-ending-style')));
    });

    await option(page, 'a').click();

    await expect(indicator).not.toHaveAttribute('data-selected');
    await expect.poll(() => page.evaluate(() => (window as unknown as { indicatorExits: boolean[] }).indicatorExits)).toEqual([true]);
    // A kept-mounted indicator must not be left advertising an exit transition once the deselection has finished, or
    // `[data-ending-style]` CSS would stick permanently.
    await expect(indicator).not.toHaveAttribute('data-ending-style');
    await expect(indicator).toHaveCount(1);
  });

  // SEL-ANI-12 (SEL-ARIA-13, SEL-DOM-39 and SEL-STR-11 are L2). Base UI's `setProps` is the fixture's value buttons.
  test('mounts only while selected when keepMounted is false', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-indicator', { value: 'b' });
    const indicator = page.getByTestId('indicator');
    await expect(indicator).toHaveCount(0);

    await setValue(fixture, 'a');

    await expect(indicator).toHaveAttribute('data-selected');
    await expect(indicator).toHaveAttribute('aria-hidden', 'true');
    await expect(indicator).not.toHaveAttribute('selected');

    await setValue(fixture, 'b');

    await expect(indicator).toHaveCount(0);

    await setValue(fixture, 'a');

    await expect(indicator).toHaveAttribute('data-selected');
  });

  // SEL-ANI-12 (SEL-STR-11 is L2).
  test('preserves the kept-mounted element across selection changes', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-press-indicator', { value: 'b', keepMounted: true, animations: 1 });
    const indicator = page.getByTestId('indicator');
    const element = (await indicator.elementHandle())!;
    const same = () => element.evaluate((node) => node === document.querySelector('[data-testid="indicator"]'));
    await expect(indicator).not.toHaveAttribute('data-selected');

    await setValue(fixture, 'a');

    expect(await same()).toBe(true);
    await expect(indicator).toHaveAttribute('data-selected');

    await setValue(fixture, 'b');

    expect(await same()).toBe(true);
    await expect(indicator).not.toHaveAttribute('data-selected');
    await expect(indicator).not.toHaveAttribute('data-ending-style');

    await setValue(fixture, 'a');

    expect(await same()).toBe(true);
    await expect(indicator).toHaveAttribute('data-selected');
  });

  async function setValue(fixture: Locator, value: 'a' | 'b'): Promise<void> {
    await pressControl(fixture.locator(`[data-set-value="${value}"]`));
    await settle(fixture);
  }
});
