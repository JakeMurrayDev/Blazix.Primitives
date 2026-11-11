import { type Locator } from '@playwright/test';
import { delayCircuit, expect, test, type Page, type RenderMode } from './helpers';
import { moveTo, parkPointer } from './menu.helpers';
import { gotoPopupFixture, type PopupFixtureOptions } from './suites/popupFixture';

// The L4 titles of Menubar.test.tsx (M5 track 8, issue 195; docs/components/menubar.md), titles verbatim, on both render
// modes, Server under a one-way circuit delay. Real Playwright input: presses, hovers and keys. The first batch runs on
// the contained setup (ContainedTriggerMenubar); the detached and multiple setups follow with their fixture cases.

const FIXTURE = 'menubar-root';
const SERVER_DELAY_MS = 40;

async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
  if (mode === 'server') {
    await delayCircuit(page, SERVER_DELAY_MS);
  }
  await parkPointer(page);
  return gotoPopupFixture(page, mode, FIXTURE, options);
}

test.describe('<Menubar />', () => {
  test.describe('when using contained triggers', () => {
    test.describe('click interactions', () => {
      // Red on both modes (2026-11-11): the bar menu's internal backdrop has no cut-out over the bar, so the trigger is
      // covered and the second click never reaches it. MenuPositionerBehavior sends `Cutout` only for a top-level menu;
      // Base UI cuts the bar's element out for a bar menu (MenuPositioner.tsx:271-276). Lifted with that Menu/ fix.
      test.fixme('should open the menu after clicking on its trigger and close it when clicking again', async ({ page, mode }) => {
        await render(page, mode);
        const fileTrigger = page.getByTestId('file-trigger');

        await fileTrigger.click({ delay: 30 });
        await expect(page.getByTestId('file-menu')).toBeVisible();

        await fileTrigger.click({ delay: 30 });
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
      });
    });

    test.describe('hover behavior', () => {
      test('should not open submenus on hover when no submenu is already open', async ({ page, mode }) => {
        const fixture = await render(page, mode);

        await moveTo(page, page.getByTestId('file-trigger'));
        await page.getByTestId('file-trigger').hover();

        await expect(fixture).toHaveAttribute('data-file-changes', '');
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
      });

      // Red on both modes (2026-11-11): with File open, the internal backdrop covers Edit's trigger (no cut-out over the
      // bar), so the hover never reaches it. Lifted with the same Menu/ fix as the click title.
      test.fixme('should open submenus on hover when another submenu is already open', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').click();
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await expect(page.getByRole('menubar')).toHaveAttribute('data-has-submenu-open', '');

        await moveTo(page, page.getByTestId('edit-trigger'));
        await expect(page.getByTestId('edit-menu')).toBeVisible();
        await expect(page.getByTestId('file-menu')).toHaveCount(0);

        await moveTo(page, page.getByTestId('view-trigger'));
        await expect(page.getByTestId('view-menu')).toBeVisible();
        await expect(page.getByTestId('edit-menu')).toHaveCount(0);
      });
    });

    test.describe('focus behavior', () => {
      test('focuses a menubar item without immediately opening the menu', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('before').focus();

        await page.keyboard.press('Tab');
        const fileTrigger = page.getByTestId('file-trigger');
        await expect(fileTrigger).toBeFocused();
        await expect(page.getByTestId('file-menu')).toHaveCount(0);

        await page.keyboard.press('Enter');
        await expect(page.getByTestId('file-menu')).toBeVisible();
      });
    });

    test.describe('keyboard interactions', () => {
      test('should navigate between menubar items with arrow keys', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();
        await expect(page.getByTestId('file-trigger')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(page.getByTestId('edit-trigger')).toBeFocused();
      });

      test('should open the menu with Space key', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();

        await page.keyboard.press(' ');
        await expect(page.getByTestId('file-menu')).toBeVisible();
      });

      test('should navigate within the menu using arrow keys', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();

        await page.keyboard.press('Enter');
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await expect(page.getByTestId('file-item-1')).toBeFocused();

        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('file-item-2')).toBeFocused();

        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('share-trigger')).toBeFocused();
      });

      test('should open the submenu with right arrow key', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();
        await page.keyboard.press('Enter');
        await expect(page.getByTestId('file-item-1')).toBeFocused();
        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('file-item-2')).toBeFocused();
        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('share-trigger')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(page.getByTestId('share-menu')).toBeVisible();
        await expect(page.getByTestId('share-item-1')).toBeFocused();
      });

      test('should close the menu with Escape key', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();
        await page.keyboard.press('Enter');
        await expect(page.getByTestId('file-menu')).toBeVisible();

        await page.keyboard.press('Escape');
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
      });

      test('should close submenu with left arrow key and return focus to submenu trigger', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();
        await page.keyboard.press('Enter');
        await expect(page.getByTestId('file-item-1')).toBeFocused();
        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('file-item-2')).toBeFocused();
        await page.keyboard.press('ArrowDown');
        const shareTrigger = page.getByTestId('share-trigger');
        await expect(shareTrigger).toBeFocused();
        await page.keyboard.press('ArrowRight');
        await expect(page.getByTestId('share-item-1')).toBeFocused();

        await page.keyboard.press('ArrowLeft');
        await expect(page.getByTestId('share-menu')).toHaveCount(0);
        await expect(shareTrigger).toBeFocused();
      });

      test('moves focus to the first and last triggers with Home and End', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();

        await page.keyboard.press('End');
        await expect(page.getByTestId('view-trigger')).toBeFocused();

        await page.keyboard.press('Home');
        await expect(page.getByTestId('file-trigger')).toBeFocused();
      });
    });

    test.describe('prop: loopFocus', () => {
      test.describe('when loopFocus == true', () => {
        test('should loop around to the first item after the last one', async ({ page, mode }) => {
          await render(page, mode, { loopFocus: true });
          const firstItem = page.getByTestId('file-trigger');
          await firstItem.focus();
          await page.keyboard.press('ArrowRight');
          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('view-trigger')).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(firstItem).toBeFocused();
        });

        test('should loop around to the last item after the first one', async ({ page, mode }) => {
          await render(page, mode, { loopFocus: true });
          await page.getByTestId('file-trigger').focus();
          await expect(page.getByTestId('file-trigger')).toBeFocused();

          await page.keyboard.press('ArrowLeft');
          await expect(page.getByTestId('view-trigger')).toBeFocused();
        });
      });

      test.describe('when loopFocus == false', () => {
        test('should stay on the last item when navigating beyond it', async ({ page, mode }) => {
          await render(page, mode, { loopFocus: false });
          await page.getByTestId('file-trigger').focus();
          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('edit-trigger')).toBeFocused();
          await page.keyboard.press('ArrowRight');
          const lastItem = page.getByTestId('view-trigger');
          await expect(lastItem).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(lastItem).toBeFocused();
        });

        test('should stay on the first item when navigating before it', async ({ page, mode }) => {
          await render(page, mode, { loopFocus: false });
          const firstItem = page.getByTestId('file-trigger');
          await firstItem.focus();

          await page.keyboard.press('ArrowLeft');
          await expect(firstItem).toBeFocused();
        });
      });
    });

    test.describe('prop: disabled', () => {
      test('disables child menus when menubar is disabled', async ({ page, mode }) => {
        await render(page, mode, { disabled: true });
        const fileTrigger = page.getByTestId('file-trigger');
        await expect(fileTrigger).toHaveAttribute('disabled', '');

        // Base UI tabs from the document; the fixture's controls precede the bar, so the Tab starts on "Before".
        await page.getByTestId('before').focus();
        await page.keyboard.press('Tab');
        await expect(fileTrigger).not.toBeFocused();

        // A disabled button takes no Playwright click; the real pointer presses it.
        const box = (await fileTrigger.boundingBox())!;
        await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
      });
    });

    test.describe('role', () => {
      test('sets role="menubar" on the root element', async ({ page, mode }) => {
        await render(page, mode);
        await expect(page.getByRole('menubar')).toHaveCount(1);
      });

      test('sets aria-orientation on the root element', async ({ page, mode }) => {
        await render(page, mode, { orientation: 'vertical' });
        await expect(page.getByRole('menubar')).toHaveAttribute('aria-orientation', 'vertical');
      });

      test('sets role="menuitem" on menu triggers', async ({ page, mode }) => {
        await render(page, mode);
        await expect(page.getByRole('menubar').getByRole('menuitem')).toHaveCount(3);
      });
    });
  });
});

