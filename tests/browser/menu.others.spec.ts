import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { parkPointer, recorded } from './menu.helpers';
import { gotoPopupFixture, openChanges, settle, type PopupFixtureOptions } from './suites/popupFixture';

// Menu's perf titles and the Menu titles of other families' test files (M5 track 6, issue 172; the brief's "other families' Menu
// titles" and "useTriggerFocusGuards' Menu half"), titles verbatim, on both render modes.

test.describe('perf', () => {
  for (const kind of ['item', 'checkbox', 'radio']) {
    test.describe(`${kind} items`, () => {
      test('perf: does not rerender menu items unnecessarily', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, 'menu-perf', { kind });
        await expect(fixture).toHaveAttribute('data-popup-attached', 'true');
        await page.locator('#item-1').focus();
        await expect(page.locator('#item-1')).toHaveAttribute('data-highlighted', '');

        // The barrier reads the counts before it re-renders the fixture, and that render adds one to each item; so the second
        // read is the baseline, and the barrier's own render shows as one more in the read after the key.
        await settle(fixture);
        await settle(fixture);
        const before = (await recorded(fixture, 'data-renders')).map(Number);

        await page.keyboard.press('ArrowDown'); // highlights '2'
        await expect(page.locator('#item-2')).toBeFocused();
        await settle(fixture);
        const after = (await recorded(fixture, 'data-renders')).map(Number);

        // '1' re-renders as it loses the highlight, and '2' as it receives it; neither the highlighted nor the selected state
        // of '3' and '4' changed.
        expect(after.map((count, index) => count - (before[index] ?? 0))).toEqual([2, 2, 1, 1]);
      });
    });
  }
});

test.describe('useTriggerFocusGuards', () => {
  async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<Locator> {
    await parkPointer(page);
    return gotoPopupFixture(page, mode, 'menu-guards', { animations: 1, ...options });
  }

  // Menu moves focus to the popup itself.
  async function openPopup(page: Page): Promise<Locator> {
    const trigger = page.getByTestId('trigger');
    await trigger.click();
    await expect(page.getByTestId('popup')).toBeFocused();
    return trigger;
  }

  test.describe('Menu', () => {
    for (const direction of ['forward', 'backward'] as const) {
      test.describe(`tabbing ${direction}`, () => {
        const tabKey = direction === 'backward' ? 'Shift+Tab' : 'Tab';

        // Puts focus on the element the upcoming Tab should leave from. Menu closes on Shift+Tab from its content, so the
        // trigger is focused directly.
        async function focusTabOrigin(page: Page, trigger: Locator): Promise<void> {
          await (direction === 'backward' ? trigger : page.getByTestId('inside')).focus();
        }

        for (const [finalFocus, tabIndex] of [
          ['ref', 0],
          ['function', 0],
          ['ref', -1],
          ['function', -1],
        ] as const) {
          test(`preserves the tab destination after the exit transition with finalFocus as a ${finalFocus} and trigger tabIndex=${tabIndex}`, async ({ page, mode }) => {
            await render(page, mode, { scenario: 'final', finalFocus, tabIndex });
            const trigger = await openPopup(page);
            await focusTabOrigin(page, trigger);
            await expect(direction === 'backward' ? trigger : page.getByTestId('inside')).toBeFocused();
            await expect(trigger).toHaveAttribute('aria-expanded', 'true');

            await page.keyboard.press(tabKey);

            const destination = page.getByTestId(direction === 'backward' ? 'before' : 'after');
            await expect(destination).toBeFocused();
            await expect(page.getByTestId('popup')).toHaveCount(0);
            await expect(destination).toBeFocused();
            await expect(trigger).toHaveAttribute('tabindex', String(tabIndex));
          });
        }

        for (const change of ['disabled', 'tabIndex', 'hidden', 'removed', 'inserted', 'reordered']) {
          test(`uses the current tab order when an adjacent control is ${change} during close`, async ({ page, mode }) => {
            // Deviation (2026-11-05): the fixture's controls change in C#, when its OnOpenChange runs. On Server that is a round
            // trip after the browser took the Tab, so the tab order read is the one before the change (ADR 01's general row
            // for a consumer's C# state); Base UI's commit lands inside the close.
            test.skip(mode === 'server', 'Server: a consumer re-render caused by the close lands a round trip after the Tab');
            await render(page, mode, { scenario: 'order', change, direction, tabIndex: -1 });
            await focusTabOrigin(page, await openPopup(page));

            await page.keyboard.press(tabKey);

            await expect(page.getByTestId('target')).toBeFocused();
            await expect(page.getByTestId('popup')).toHaveCount(0);
            await expect(page.getByTestId('target')).toBeFocused();
          });
        }

        test('preserves the surrounding modal dialog focus trap', async ({ page, mode }) => {
          await render(page, mode, { scenario: 'dialog', direction, tabIndex: -1 });
          const destination = page.getByRole('button', { name: 'Inside dialog' });
          await expect(destination).toBeFocused();

          await focusTabOrigin(page, await openPopup(page));
          await page.keyboard.press(tabKey);

          await expect(page.getByTestId('popup')).toHaveCount(0);
          await expect(destination).toBeFocused();
        });

        for (const tabIndex of [0, -1]) {
          test(`returns focus to the trigger when no outside element is tabbable and trigger tabIndex=${tabIndex}`, async ({ page, mode }) => {
            await render(page, mode, { scenario: 'alone', tabIndex });
            const trigger = await openPopup(page);
            await focusTabOrigin(page, trigger);

            await page.keyboard.press(tabKey);

            await expect(trigger).toBeFocused();
            await expect(page.getByTestId('popup')).toHaveCount(0);
            await expect(trigger).toBeFocused();
            await expect(trigger).toHaveAttribute('tabindex', String(tabIndex));
          });
        }
      });
    }
  });
});

test.describe('<Dialog.Root /> with a nested menu', () => {
  test('should not dismiss the dialog when dismissing outside a nested modal menu', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nesting', { case: 'dialog' });
    await page.getByTestId('dialog-trigger').click();
    await expect(page.getByRole('dialog')).toBeVisible();

    await page.getByTestId('menu-trigger').click();
    await expect(page.getByRole('menu')).toBeVisible();

    // A real press at the page's corner lands on the topmost internal backdrop: the menu's, then the dialog's.
    await page.mouse.click(2, 2);
    await expect(page.getByRole('menu')).toHaveCount(0);
    await settle(fixture);
    await expect(page.getByRole('dialog')).toBeVisible();

    await page.mouse.click(2, 2);
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });

  test('should not close the parent menu when Escape is pressed in a nested dialog', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-dialog', { case: 'dialog' });
    await page.getByTestId('menu-trigger').click();
    await expect(page.getByRole('menu')).toBeVisible();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-item-attached', 'true');

    await page.getByRole('menuitem', { name: 'Open Dialog' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();

    await page.keyboard.press('Escape');

    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(page.getByTestId('menu-popup')).toBeVisible();
  });
});

test.describe('<Toolbar.Button /> as a menu trigger', () => {
  test('renders a menu trigger', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'menu-nesting', { case: 'toolbar' });

    await expect(page.getByTestId('button')).toHaveAttribute('aria-haspopup', 'menu');
  });

  test('handles interactions', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nesting', { case: 'toolbar' });
    await expect(page.getByRole('menu')).toHaveCount(0);
    const trigger = page.getByRole('button', { name: 'Toggle' });

    await page.keyboard.press('Tab');
    await expect(trigger).toBeFocused();

    await page.keyboard.press('Enter');
    await expect(page.getByRole('menu')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-button-clicks', '1');
    expect(await openChanges(fixture)).toHaveLength(1);

    await expect(page.getByTestId('item-1')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('item-2')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('item-3')).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(page.getByTestId('item-2')).toBeFocused();

    await page.keyboard.press('Escape');
    await expect(page.getByRole('menu')).toHaveCount(0);
    expect(await openChanges(fixture)).toHaveLength(2);
    await expect(trigger).toBeFocused();
  });

  test('disabled state', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nesting', { case: 'toolbar', disabled: true });
    const trigger = page.getByRole('button', { name: 'Toggle' });
    await expect(trigger).not.toHaveAttribute('disabled', /.*/);
    await expect(trigger).toHaveAttribute('data-disabled', '');
    await expect(trigger).toHaveAttribute('aria-disabled', 'true');
    await expect(page.getByRole('menu')).toHaveCount(0);

    await page.keyboard.press('Tab');
    await expect(trigger).toBeFocused();

    await page.keyboard.press('Enter');
    await page.keyboard.press('Space');
    await page.keyboard.press('ArrowUp');
    await settle(fixture);

    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-button-clicks', '0');
  });
});

test.describe('<Popover.Root /> with a nested menu', () => {
  test('moves focus to the next element when tabbing out of a nested menu inside the popover', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'menu-nesting', { case: 'popover' });
    await page.getByTestId('menu-trigger').click();

    await expect(page.getByRole('menu')).toBeFocused();

    await page.keyboard.press('Tab');

    await expect(page.getByTestId('after')).toBeFocused();
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(page.getByTestId('popover-popup')).toBeVisible();
  });
});
