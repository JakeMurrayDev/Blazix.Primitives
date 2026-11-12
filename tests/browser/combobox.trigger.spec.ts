import { type Locator } from '@playwright/test';
import { expect, test, waitForPositioned, type Page } from './helpers';
import { center, fire, gotoCombobox, moveTo, positionerOf, recorded } from './combobox.helpers';
import { settle, pressInPage, pressControl } from './suites/popupFixture';

// ComboboxTrigger.test.tsx's titles (and createItems.test.tsx's closed trigger typeahead title) at L4 (M5 track 10, issue 174),
// every title on both render modes, on the `combobox-trigger` fixture. Titles are Base UI's verbatim. Not ported:
//  - "validates the no selection value when blurred" (renders Autocomplete.Root: track 11's);
//  - the titles of the file with no [L4] row.

const trigger = (page: Page): Locator => page.getByTestId('trigger');
const listbox = (page: Page): Locator => page.getByRole('listbox');

// A pointer held down on a title's trigger is released here, so the next title of the worker's page starts with the button up.
test.afterEach(async ({ page }) => {
  await page.mouse.up().catch(() => undefined);
});

/** Base UI's `fireEvent.pointerDown` + `fireEvent.mouseDown` on the trigger: a real primary press, left held down. */
async function pressTrigger(page: Page): Promise<void> {
  const point = await center(trigger(page), { unanchored: true });
  await page.mouse.move(point.x, point.y, { steps: 4 });
  await page.mouse.down();
}

test.describe('<Combobox.Trigger />', () => {
  test.describe('prop: disabled', () => {
    test('should not open popup when disabled', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { disabled: true });
      // Playwright waits for a button to be enabled before it clicks, so the click is forced.
      await trigger(page).click({ force: true });
      await settle(fixture);

      await expect(listbox(page)).toHaveCount(0);
      expect(await recorded(fixture, 'data-open-changes')).toEqual([]);
    });
  });

  test('validates the single selection value when blurred', async ({ page, mode }) => {
    const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { case: 'validate' });
    await trigger(page).focus();
    await expect(trigger(page)).toBeFocused();
    await trigger(page).blur();

    await expect(fixture).toHaveAttribute('data-validations', 'apple');
  });

  test('ignores a pending mouseup after the trigger unmounts', async ({ page, mode }) => {
    const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { input: 'inside', unmount: true });
    await pressTrigger(page);
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press');
    await expect(listbox(page)).toBeVisible();

    await pressInPage(page.getByTestId('hide-trigger'));
    await expect(trigger(page)).toHaveCount(0);

    await page.mouse.move(100, 100);
    await page.mouse.up();
    await settle(fixture);

    // The open change recorded before the trigger went is the only one.
    expect(await recorded(fixture, 'data-open-changes')).toEqual(['true:trigger-press']);
  });

  test('does not select when closed typeahead matches a sparse label without a value', async ({ page, mode }) => {
    // Deviation (2026-11-10): Base UI registers the sparse label through the root context's labelsRef and valuesRef, which the
    // port keeps internal; the fixture renders the same Trigger with no registered item, so no label matches the key.
    const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { portal: false });
    await trigger(page).focus();
    await page.keyboard.press('a');
    await settle(fixture);

    expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
  });

  test.describe('prop: readOnly', () => {
    test('opens the popup when readOnly', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { readOnly: true });
      await trigger(page).click();

      await expect(listbox(page)).toHaveAttribute('aria-readonly', 'true');
    });

    for (const { name, key } of [
      { name: 'ArrowDown', key: 'ArrowDown' },
      { name: 'ArrowUp', key: 'ArrowUp' },
      { name: 'Enter', key: 'Enter' },
      { name: 'Space', key: 'Space' },
    ]) {
      test(`opens on ${name} when readOnly`, async ({ page, mode }) => {
        const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { readOnly: true, input: 'inside', items: 'a' });
        await trigger(page).focus();
        await page.keyboard.press(key);

        await expect(listbox(page)).toBeVisible();
        await settle(fixture);
        expect((await recorded(fixture, 'data-open-changes')).length).toBe(1);
      });
    }

    test('does not commit a value with typeahead on a closed trigger', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { readOnly: true, value: true, items: 'apple|banana' });
      await trigger(page).focus();
      await page.keyboard.press('b');
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(trigger(page)).toHaveAttribute('data-placeholder');
    });

    test('should not toggle when readOnly=false (control)', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { items: 'a' });
      await trigger(page).click();

      await expect(listbox(page)).toBeVisible();
    });
  });

  test.describe('interaction behavior', () => {
    test('should toggle popup when enabled', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { input: 'before', items: 'a|b' });

      await trigger(page).click();
      await expect(listbox(page)).toBeVisible();

      await trigger(page).click();
      await expect(listbox(page)).toHaveCount(0);
    });

    test('should call onOpenChange when toggling', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { items: 'a|b' });
      await trigger(page).click();

      await expect(fixture).toHaveAttribute('data-open-changes', /^true:/);
      await settle(fixture);
      expect((await recorded(fixture, 'data-open-changes')).length).toBe(1);
    });

    test('opens popup when pressing ArrowDown or ArrowUp', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { input: 'after', items: '' });
      await trigger(page).focus();

      await page.keyboard.press('ArrowDown');
      await expect(listbox(page)).toBeAttached();
      await expect(page.getByTestId('input')).toBeFocused();

      await page.keyboard.press('Escape');
      await expect(listbox(page)).toHaveCount(0);

      await trigger(page).focus();
      await page.keyboard.press('ArrowUp');
      await expect(listbox(page)).toBeAttached();
      await expect(page.getByTestId('input')).toBeFocused();
    });

    test('does not open on ArrowDown/ArrowUp when reference is a textarea', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { textarea: true, portal: false });
      await trigger(page).click();
      await page.keyboard.press('ArrowDown');
      await settle(fixture);
      await expect(listbox(page)).toHaveCount(0);
      await page.keyboard.press('ArrowUp');
      await settle(fixture);
      await expect(listbox(page)).toHaveCount(0);
    });

    test('fires with reason trigger-press when Trigger is clicked', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { items: 'alpha|beta' });
      await trigger(page).click();

      await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press');
      await settle(fixture);
      expect(await recorded(fixture, 'data-open-changes')).toEqual(['true:trigger-press']);
    });
  });

  test.describe('drag selection', () => {
    test('commits selection when the input is outside the popup', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { input: 'before' });
      await pressTrigger(page);

      await expect(listbox(page)).toBeVisible();
      const option = page.getByRole('option', { name: 'Beta' });
      await moveTo(page, option);
      await expect(option).toHaveAttribute('data-highlighted');

      await page.mouse.up();

      await expect(fixture).toHaveAttribute('data-value-changes', /^beta:/);
      await settle(fixture);
      expect((await recorded(fixture, 'data-value-changes')).length).toBe(1);
    });

    test('commits selection when the input is inside the popup and the pointer is released over an item', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { input: 'inside' });
      await pressTrigger(page);

      await expect(listbox(page)).toBeVisible();
      const option = page.getByRole('option', { name: 'Beta' });
      await moveTo(page, option);
      await expect(option).toHaveAttribute('data-highlighted');

      await page.mouse.up();

      await expect(fixture).toHaveAttribute('data-value-changes', /^beta:/);
      await settle(fixture);
      expect((await recorded(fixture, 'data-value-changes')).length).toBe(1);
    });

    test('does not double-commit when a non-primary touch lands on another item', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { input: 'inside' });
      await pressTrigger(page);

      await expect(listbox(page)).toBeVisible();
      const alpha = page.getByRole('option', { name: 'Alpha' });
      const beta = page.getByRole('option', { name: 'Beta' });

      // Highlight Alpha so a stray drag-select would be able to commit it.
      await moveTo(page, alpha);
      await expect(alpha).toHaveAttribute('data-highlighted');

      // Primary touch presses Alpha; a second, non-primary touch lands on Beta. Without the `isPrimary` guard the shared ref
      // would flip to Beta, making Alpha's release read as a drag-select and commit once on `mouseup` and again on the
      // following `click`.
      // Deviation (2026-11-10): a real two-finger touch while the mouse button is held yields no pointerdown pair on two items
      // and a mouseup on Alpha; the two touch pointerdowns and the click are dispatched, the mouseup is the real release.
      await fire(alpha, 'pointerdown', { pointerType: 'touch', isPrimary: true, button: 0 });
      await fire(beta, 'pointerdown', { pointerType: 'touch', isPrimary: false, button: 0 });
      await page.mouse.up();
      await fire(alpha, 'click', { button: 0 });

      await expect(fixture).toHaveAttribute('data-value-changes', /^alpha:/);
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toHaveLength(1);
    });

    test('commits a later drag-select onto an item whose earlier pointerdown never released', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { input: 'before' });

      // Press an item, then close before any matching `mouseup` arrives. The closed-state effect must clear the shared
      // pointerdown ref, otherwise the stale entry makes the next drag-select onto Alpha read as a same-item tap and never
      // commit.
      await pressTrigger(page);
      await expect(listbox(page)).toBeVisible();
      const alpha = page.getByRole('option', { name: 'Alpha' });
      // Deviation (2026-11-10): the pointerdown is a touch one with no release, which a real mouse button held on the trigger
      // cannot also produce.
      await fire(alpha, 'pointerdown', { pointerType: 'touch', isPrimary: true, button: 0 });

      await page.keyboard.press('Escape');
      await expect(listbox(page)).toHaveCount(0);

      // The held button is released away from the trigger, so its click does not reopen the popup.
      await page.mouse.move(600, 600);
      await page.mouse.up();
      await settle(fixture);

      // Reopen and drag-select onto Alpha (gesture starts on the trigger).
      await pressTrigger(page);
      await expect(listbox(page)).toBeVisible();
      const alphaReopened = page.getByRole('option', { name: 'Alpha' });
      await moveTo(page, alphaReopened);
      await expect(alphaReopened).toHaveAttribute('data-highlighted');
      await page.mouse.up();

      await expect(fixture).toHaveAttribute('data-value-changes', /^alpha:/);
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toHaveLength(1);
    });

    test('does not commit selection if the pointer never hovers the item', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { input: 'inside' });
      await pressTrigger(page);

      await expect(listbox(page)).toBeVisible();
      const option = page.getByRole('option', { name: 'Beta' });
      await waitForPositioned(positionerOf(option));

      // Deviation (2026-11-10): a real release over an item needs the pointer to move there, which hovers it; the mouseup on
      // the item with no hover is dispatched.
      await fire(option, 'mouseup', { button: 0 });
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
    });
  });

  test.describe('cancel-open', () => {
    test('closes the popup when mouseup occurs outside the trigger bounds', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { items: 'alpha' });
      await pressTrigger(page);

      await expect(listbox(page)).toBeVisible();

      await page.mouse.move(900, 600, { steps: 4 });
      await page.mouse.up();

      await expect(listbox(page)).toHaveCount(0);
      const changes = await recorded(fixture, 'data-open-changes');
      expect(changes.at(-1)).toBe('false:cancel-open');
    });

    test('keeps the popup open when mouseup remains near the trigger bounds', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { items: 'alpha' });
      await pressTrigger(page);

      await expect(listbox(page)).toBeVisible();
      const box = (await trigger(page).boundingBox())!;

      // Within 5px of the trigger's bounds, outside the trigger itself.
      await page.mouse.move(box.x - 3, box.y + box.height / 2, { steps: 4 });
      await page.mouse.up();
      await settle(fixture);

      await expect(listbox(page)).toBeVisible();
      expect(await recorded(fixture, 'data-open-changes')).toEqual(['true:trigger-press']);
    });

    test('closes the popup when the release is more than 5px outside the trigger bounds', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { items: 'alpha' });
      await pressTrigger(page);

      await expect(listbox(page)).toBeVisible();
      const box = (await trigger(page).boundingBox())!;

      await page.mouse.move(box.x - 6, box.y + box.height / 2, { steps: 4 });
      await page.mouse.up();

      await expect(listbox(page)).toHaveCount(0);
    });
  });

  test.describe('typeahead', () => {
    test('selects item when typing on focused trigger (input inside popup)', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { case: 'typeahead-items' });

      await expect(trigger(page)).not.toContainText('apple');

      await trigger(page).focus();
      await page.keyboard.press('a');

      await expect(trigger(page)).toHaveText('apple');
      await expect(listbox(page)).toHaveCount(0);
    });

    // There is no strict mode in Blazor: both cases render the same.
    for (const strict of [false, true]) {
      test(`selects item when typing after the popup has been opened and closed (items prop, strict: ${strict})`, async ({ page, mode }) => {
        await gotoCombobox(page, mode, 'combobox-trigger', { case: 'typeahead-items' });

        // Opening mounts the list (rendered labels overwrite the derived ones) and closing unmounts it, clearing the registered
        // labels. Typeahead must still work afterwards.
        await trigger(page).click();
        await expect(listbox(page)).toBeVisible();
        await page.keyboard.press('Escape');
        await expect(listbox(page)).toHaveCount(0);
        // Focus returns to the trigger asynchronously after close.
        await expect(trigger(page)).toBeFocused();

        await page.keyboard.press('b');

        await expect(trigger(page)).toHaveText('banana');
      });
    }

    for (const keepMounted of [false, true]) {
      test(`cycles to the next matching item when typing after open/close (no items prop, keepMounted ${keepMounted})`, async ({ page, mode }) => {
        await gotoCombobox(page, mode, 'combobox-trigger', { case: 'typeahead-plain', keepMounted });

        await trigger(page).click();
        await expect(listbox(page)).toBeVisible();
        await page.keyboard.press('Escape');
        await expect(listbox(page)).toHaveCount(0);
        await expect(trigger(page)).toBeFocused();

        // Typeahead starts matching after the selected item, like a native select.
        await page.keyboard.press('a');
        await expect(trigger(page)).toHaveText('apricot');
      });
    }

    test('cycles from the selected item after reordering items while closed (no items prop)', async ({ page, mode }) => {
      const fixture = await gotoCombobox(page, mode, 'combobox-trigger', { case: 'typeahead-reorder' });

      // Open and close so the trigger path force-mounts the list.
      await trigger(page).click();
      await expect(listbox(page)).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(listbox(page)).toHaveCount(0);
      await expect(trigger(page)).toBeFocused();

      // Reorder while closed: apple moves from index 0 to index 1.
      await pressControl(page.getByTestId('reorder'));
      await settle(fixture);

      // Typeahead starts after the selected item; from apple it wraps to apricot.
      await page.keyboard.press('a');
      await expect(trigger(page)).toHaveText('apricot');
    });

    test('only matches mounted indexed item labels when typing on the focused trigger', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { case: 'typeahead-indexed' });

      await trigger(page).click();
      await expect(listbox(page)).toBeVisible();

      await pressControl(page.getByRole('button', { name: 'Remove banana' }));
      await expect(page.getByRole('option', { name: 'Banana' })).toHaveCount(0);
      await page.keyboard.press('Escape');
      await expect(listbox(page)).toHaveCount(0);

      await trigger(page).focus();
      await page.keyboard.press('b');

      await expect(trigger(page)).toHaveText('blueberry');
    });
  });

  test.describe('data state attributes', () => {
    test('sets data-popup-side to the current popup side', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { side: 'right', items: 'apple' });

      await expect(trigger(page)).not.toHaveAttribute('data-popup-side');

      await trigger(page).click();

      await expect(listbox(page)).toBeVisible();
      await waitForPositioned(positionerOf(listbox(page)));
      await expect(trigger(page)).toHaveAttribute('data-popup-side', 'right');

      await page.mouse.click(900, 650);

      await expect(listbox(page)).toHaveCount(0);
      await expect(trigger(page)).not.toHaveAttribute('data-popup-side');
    });
  });
});

test.describe('Combobox.createItems', () => {
  test.describe('integration', () => {
    test('uses derived labels for closed trigger typeahead', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-trigger', { case: 'create-items' });

      await trigger(page).focus();
      await page.keyboard.press('z');

      await expect(trigger(page)).toHaveText('Zebra');
      await expect(listbox(page)).toHaveCount(0);
    });
  });
});
