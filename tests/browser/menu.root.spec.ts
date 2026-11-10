import { type Locator } from '@playwright/test';
import { expect, isScrollLocked, test, waitForPositioned, type Page, type RenderMode } from './helpers';
import { center, clickThrough, fire, holdClock, moveTo, positionerOf, parkPointer, pressCharacter, recorded, runClockUntil, touchTest, withClock } from './menu.helpers';
import { gotoPopupFixture, openChanges, settle, type PopupFixtureOptions } from './suites/popupFixture';

// MenuRoot.test.tsx's L4 titles (M5 track 6, issue 172; docs/components/menu.md) on `menu-root`, titles verbatim, every
// title on both render modes. `describe.for` runs the block twice, for contained and detached triggers. Real keyboard
// and pointer input; where Base UI's `fireEvent` takes a path a real input cannot, the title says so and dispatches it.

async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
  await parkPointer(page);
  return gotoPopupFixture(page, mode, 'menu-root', options);
}

/** Renders a menu that starts open and waits until its popup attached, so a first key or focus has a list to reach. */
async function renderOpen(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
  const fixture = await render(page, mode, { open: true, ...options });
  await expect(fixture).toHaveAttribute('data-popup-attached', 'true');
  return fixture;
}

function item(page: Page, name: string): Locator {
  return page.getByRole('menuitem', { name, exact: true });
}

function trigger(page: Page): Locator {
  return page.getByTestId('trigger');
}

function popup(page: Page): Locator {
  return page.getByTestId('menu');
}

function submenu(page: Page): Locator {
  return page.getByTestId('submenu');
}

async function arrowTo(page: Page, keys: readonly string[]): Promise<void> {
  for (const key of keys) {
    await page.keyboard.press(key);
  }
}

test.describe('<Menu.Root />', () => {
  test('returns focus on Escape after a controlled root ignores a Shift+Tab close', async ({ page, mode }) => {
    await render(page, mode, { modal: false, open: false, followOpen: true, ignoreFocusOut: true, content: 'items', items: 'Item' });
    await trigger(page).click();
    await expect(page.getByRole('menu')).toBeFocused();

    await page.keyboard.press('Shift+Tab');
    await expect(trigger(page)).toBeFocused();
    await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');

    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('menuitem')).toBeFocused();

    await page.keyboard.press('Escape');
    await expect(popup(page)).toBeHidden();
    await expect(trigger(page)).toBeFocused();
  });

  test('keeps focus in the menu when a Shift+Tab close is canceled', async ({ page, mode }) => {
    const fixture = await renderOpen(page, mode, { open: undefined, defaultOpen: true, cancelClose: true, content: 'items', items: 'Item' });
    await expect(popup(page)).toBeFocused();

    await page.keyboard.press('Shift+Tab');

    // ADR 01's "C#-origin consumer vetoes" row (`leave`, MNU-KEY-11): the cancel lands inside the keydown on WebAssembly, and a
    // leg after the browser moved the focus on Server, where it still moves.
    await expect.poll(() => openChanges(fixture)).toEqual(['false:focus-out']);
    if (mode === 'wasm') {
      await expect(popup(page)).toBeFocused();
    } else {
      await expect(trigger(page)).toBeFocused();
    }

    await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
  });

  test.describe('hover opening', () => {
    test('stays open while the pointer crosses the gap from the trigger to the popup', async ({ page, mode }) => {
      await render(page, mode, { modal: false, openOnHover: true, delay: 0, sideOffset: 24, content: 'items', items: 'One' });
      await moveTo(page, trigger(page));
      await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
      await expect(page.getByTestId('item-One')).toBeVisible();

      await waitForPositioned(positionerOf(page.getByTestId('item-One')));
      const triggerBox = (await trigger(page).boundingBox())!;
      const itemBox = (await page.getByTestId('item-One').boundingBox())!;
      expect(itemBox.y - (triggerBox.y + triggerBox.height)).toBeGreaterThanOrEqual(20);
      const x = Math.min(triggerBox.x + triggerBox.width, itemBox.x + itemBox.width) - 5;

      await page.mouse.move(x, triggerBox.y + triggerBox.height - 1);
      for (let y = triggerBox.y + triggerBox.height + 3; y < itemBox.y; y += 4) {
        await page.mouse.move(x, y);
      }
      await page.mouse.move(x, itemBox.y + 2);
      await settle(page.locator('[data-fixture="menu-root"]'));

      await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
      await expect(popup(page)).toBeVisible();
    });
  });

  test.describe('opening', () => {
    test('highlights the first item when the items render a microtask after a keyboard opening', async ({ page, mode }) => {
      await render(page, mode, { content: 'deferred' });
      await trigger(page).focus();
      await page.keyboard.press('ArrowDown');

      await expect(item(page, 'One')).toBeFocused();
    });

    for (const { name, init } of [
      { name: 'a zero-size press', init: { width: 0, height: 0 } },
      { name: "Chrome's 1x1 pressureless press", init: { width: 1, height: 1, pressure: 0 } },
    ]) {
      test(`moves focus to the first item after a screen reader ${name}`, async ({ page, mode }) => {
        await render(page, mode, { content: 'items', items: 'One|Two' });

        // Screen readers synthesize the press, so Base UI's own sequence is dispatched; the click itself can still report `detail: 1`.
        await fire(trigger(page), 'pointerdown', { pointerType: 'mouse', buttons: 0, ...init });
        await fire(trigger(page), 'mousedown', { detail: 1 });
        await fire(trigger(page), 'pointerup', { pointerType: 'mouse', ...init });
        await fire(trigger(page), 'mouseup', { detail: 1 });
        await fire(trigger(page), 'click', { detail: 1 });

        await expect(item(page, 'One')).toBeFocused();
      });
    }
  });

  for (const { name, detached } of [
    { name: 'contained triggers', detached: false },
    { name: 'detached triggers', detached: true },
  ]) {
    test.describe(`when using ${name}`, () => {
      const base: PopupFixtureOptions = detached ? { detached: true } : {};

      test.describe('keyboard navigation', () => {
        test('changes the highlighted item using the arrow keys', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).focus();
          await page.keyboard.press('Enter');

          await expect(page.getByTestId('item-1')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-2')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-3')).toBeFocused();
          await page.keyboard.press('ArrowUp');
          await expect(page.getByTestId('item-2')).toBeFocused();
        });

        test('navigates across grouped items with arrow keys and text', async ({ page, mode }) => {
          await render(page, mode, { ...base, content: 'grouped' });
          await trigger(page).focus();
          await page.keyboard.press('Enter');

          await expect(item(page, 'Apple')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(item(page, 'Banana')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(item(page, 'Cherry')).toBeFocused();

          await item(page, 'Apple').focus();
          await page.keyboard.press('c');
          await expect(item(page, 'Cherry')).toBeFocused();
        });

        test('closes with a `detail === 0` click event on keyboard item activation', async ({ page, mode }) => {
          const fixture = await render(page, mode, base);
          await trigger(page).focus();
          await page.keyboard.press('Enter');
          await expect(page.getByTestId('item-1')).toBeFocused();

          await page.keyboard.press('Enter');

          await expect(trigger(page)).toHaveAttribute('aria-expanded', 'false');
          expect((await openChanges(fixture)).at(-1)).toBe('false:item-press');
          await expect(fixture).toHaveAttribute('data-item-clicks', 'Item 1:0');
        });

        test('changes the highlighted item using the Home and End keys', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).focus();
          await page.keyboard.press('Enter');
          await expect(page.getByTestId('item-1')).toBeFocused();

          await page.keyboard.press('End');
          await expect(page.getByTestId('item-5')).toBeFocused();
          await page.keyboard.press('Home');
          await expect(page.getByTestId('item-1')).toBeFocused();
        });

        test('includes disabled items during keyboard navigation', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).focus();
          await page.keyboard.press('Enter');
          await expect(page.getByTestId('item-1')).toBeFocused();

          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-2')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-3')).toBeFocused();
          await expect(page.getByTestId('item-3')).toHaveAttribute('aria-disabled', 'true');
        });

        test('skips items hidden with CSS during keyboard navigation', async ({ page, mode }) => {
          await render(page, mode, { ...base, content: 'items', items: 'Item 1|Item 2|Item 3', hiddenItems: 'Item 1' });
          await trigger(page).focus();
          await page.keyboard.press('Enter');

          await expect(page.getByTestId('item-Item 2')).toBeFocused();
          await expect(page.getByTestId('item-Item 1')).toHaveAttribute('tabindex', '-1');
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-Item 3')).toBeFocused();
          await page.keyboard.press('ArrowUp');
          await expect(page.getByTestId('item-Item 2')).toBeFocused();
        });

        test.describe('text navigation', () => {
          test('changes the highlighted item', async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, content: 'items', items: 'Aa|Ba|Bb|Ca|Cb|Cd' });
            await item(page, 'Aa').focus();

            await page.keyboard.press('c');
            await expect(item(page, 'Ca')).toBeFocused();
            await expect(item(page, 'Ca')).toHaveAttribute('tabindex', '0');

            await page.keyboard.press('d');
            await expect(item(page, 'Cd')).toBeFocused();
            await expect(item(page, 'Cd')).toHaveAttribute('tabindex', '0');
          });

          test('skips items hidden with CSS in text navigation', async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, content: 'items', items: 'Apple|Apricot|Banana', hiddenItems: 'Apple' });
            await item(page, 'Banana').focus();

            await page.keyboard.press('a');
            await expect(item(page, 'Apricot')).toBeFocused();
            await expect(page.getByTestId('item-Apple')).toHaveAttribute('tabindex', '-1');
          });

          test('skips natively disabled items in text navigation', async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, content: 'items', items: 'Apple|Banana|Blueberry', nativeDisabledItems: 'Banana' });
            await item(page, 'Apple').focus();

            await page.keyboard.press('b');
            await expect(item(page, 'Blueberry')).toBeFocused();
            await expect(page.getByTestId('item-Banana')).toHaveAttribute('tabindex', '-1');
            await expect(page.getByTestId('item-Banana')).not.toHaveAttribute('data-highlighted', /.*/);
          });

          test('changes the highlighted item using text navigation on label prop', async ({ page, mode }) => {
            await render(page, mode, { ...base, content: 'items', items: '1|2|3|4', itemLabels: '1=Aa|2=Ba|3=Bb|4=Ca' });
            await trigger(page).click();
            await expect(popup(page)).toBeFocused();
            const items = page.getByRole('menuitem');

            await page.keyboard.press('b');
            await expect(items.nth(1)).toBeFocused();
            await expect(items.nth(1)).toHaveAttribute('tabindex', '0');

            await page.keyboard.press('b');
            await expect(items.nth(2)).toBeFocused();
            await expect(items.nth(2)).toHaveAttribute('tabindex', '0');

            await page.keyboard.press('b');
            await expect(items.nth(2)).toBeFocused();
            await expect(items.nth(2)).toHaveAttribute('tabindex', '0');
          });

          test('skips the non-stringifiable items', async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, content: 'non-string' });
            await item(page, 'Aa').focus();

            await page.keyboard.press('b');
            await expect(item(page, 'Ba')).toBeFocused();
            await expect(item(page, 'Ba')).toHaveAttribute('tabindex', '0');

            await page.keyboard.press('c');
            await expect(item(page, 'Bc')).toBeFocused();
            await expect(item(page, 'Bc')).toHaveAttribute('tabindex', '0');
          });

          test('navigate to options with diacritic characters', async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, content: 'items', items: 'Aa|Ba|Bb|Bą' });
            await item(page, 'Aa').focus();

            await page.keyboard.press('b');
            await expect(item(page, 'Ba')).toBeFocused();
            await expect(item(page, 'Ba')).toHaveAttribute('tabindex', '0');

            await pressCharacter(page, 'ą');
            await expect(item(page, 'Bą')).toBeFocused();
            await expect(item(page, 'Bą')).toHaveAttribute('tabindex', '0');
          });

          test('navigate to next options that begin with diacritic characters', async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, content: 'items', items: 'Aa|ąa|ąb|ąc' });
            await item(page, 'Aa').focus();

            await pressCharacter(page, 'ą');
            await expect(item(page, 'ąa')).toBeFocused();
            await expect(item(page, 'ąa')).toHaveAttribute('tabindex', '0');
          });

          test('does not trigger the onClick event when Space is pressed during text navigation', async ({ page, mode }) => {
            const fixture = await renderOpen(page, mode, { ...base, content: 'items', items: 'Item One|Item Two|Item Three' });
            await item(page, 'Item One').focus();

            await page.keyboard.type('Item T');

            await expect(item(page, 'Item Two')).toBeFocused();
            await settle(fixture);
            await expect(fixture).toHaveAttribute('data-item-clicks', '');
          });

          test('does not open a submenu when pressing Space during a typeahead session', async ({ page, mode }) => {
            const fixture = await renderOpen(page, mode, { ...base, content: 'typeahead-submenu' });
            const submenuTrigger = page.getByTestId('submenu-trigger');
            await submenuTrigger.focus();

            await page.keyboard.type('Add to p');
            await expect(submenuTrigger).toBeFocused();

            await page.keyboard.press('Space');
            await settle(fixture);
            await expect(submenu(page)).toHaveCount(0);

            await page.keyboard.press('Space');
            await settle(fixture);
            await expect(submenu(page)).toHaveCount(0);
          });

          test('opens a focused submenu trigger with Space when not typing', async ({ page, mode }) => {
            await renderOpen(page, mode, base);
            await page.getByTestId('submenu-trigger').focus();

            await page.keyboard.press('Space');

            await expect(submenu(page)).toBeAttached();
          });

          test('matches "Item 2" after "Item " currently matches "Item 1"', async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, content: 'items', items: 'Item 1|Item 2|Item 3' });
            await item(page, 'Item 1').focus();

            await page.keyboard.type('Item 2');

            await expect(item(page, 'Item 2')).toBeFocused();
          });

          test('matches a submenu trigger label after a space + numeric suffix', async ({ page, mode }) => {
            const fixture = await renderOpen(page, mode, { ...base, content: 'item-submenu' });
            await item(page, 'Item 1').focus();

            await page.keyboard.type('Item 2');

            await expect(page.getByTestId('submenu-trigger')).toBeFocused();
            await settle(fixture);
            await expect(submenu(page)).toHaveCount(0);
          });
        });
      });

      test.describe('nested menus', () => {
        for (const { orientation, direction, openKey, closeKey } of [
          { orientation: 'vertical', direction: 'ltr', openKey: 'ArrowRight', closeKey: 'ArrowLeft' },
          { orientation: 'vertical', direction: 'rtl', openKey: 'ArrowLeft', closeKey: 'ArrowRight' },
          { orientation: 'horizontal', direction: 'ltr', openKey: 'ArrowDown', closeKey: 'ArrowUp' },
          { orientation: 'horizontal', direction: 'rtl', openKey: 'ArrowDown', closeKey: 'ArrowUp' },
        ]) {
          test(`opens a nested menu of a ${orientation} ${direction} menu with ${openKey} key and closes it with ${closeKey}`, async ({ page, mode }) => {
            await renderOpen(page, mode, { ...base, orientation, subOrientation: orientation, dir: direction });
            const submenuTrigger = page.getByTestId('submenu-trigger');
            await submenuTrigger.focus();
            await expect(submenuTrigger).toBeFocused();

            await page.keyboard.press(openKey);
            await expect(submenu(page)).toBeAttached();
            await expect(page.getByTestId('item-4_1')).toBeFocused();

            await page.keyboard.press(closeKey);
            await expect(submenu(page)).toHaveCount(0);
            await expect(submenuTrigger).toBeFocused();
          });
        }

        test('opens submenu on click when openOnHover is false', async ({ page, mode }) => {
          await render(page, mode, { ...base, subOpenOnHover: false });
          await trigger(page).click();
          await expect(popup(page)).toBeVisible();
          await expect(submenu(page)).toHaveCount(0);

          await page.getByTestId('submenu-trigger').click();

          await expect(popup(page)).toBeVisible();
          await expect(page.getByTestId('item-4_1')).toHaveText('Item 4.1');
        });

        test('keeps the root menu open when a submenu opens and the trigger `render` element has a custom id', async ({ page, mode }) => {
          const fixture = await render(page, mode, { ...base, triggerRender: 'custom-id', subOpenOnHover: false });
          await trigger(page).click();
          await expect(popup(page)).toBeVisible();

          await page.getByTestId('submenu-trigger').click();
          await expect(submenu(page)).toBeVisible();

          await expect(popup(page)).toBeVisible();
          await settle(fixture);
          expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
        });

        test('closes submenus when focus is lost by shift-tabbing from a nested menu', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).click();
          await expect(popup(page)).toBeVisible();
          await expect(submenu(page)).toHaveCount(0);

          const submenuTrigger = page.getByTestId('submenu-trigger');
          await moveTo(page, submenuTrigger);
          await expect(submenu(page)).toBeVisible();

          const submenuItem = page.getByTestId('item-4_1');
          await submenuItem.focus();
          await expect(submenuItem).toBeFocused();

          await page.keyboard.press('Shift+Tab');

          await expect(submenu(page)).toHaveCount(0);
          await expect(submenuTrigger).toBeFocused();
          await expect(popup(page)).toBeVisible();
        });

        test('closes the entire tree when clicking outside the deepest submenu', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).click();
          await expect(popup(page)).toBeFocused();

          await arrowTo(page, ['ArrowDown', 'ArrowDown', 'ArrowDown', 'ArrowDown']);
          await expect(page.getByTestId('submenu-trigger')).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('item-4_1')).toBeFocused();

          await arrowTo(page, ['ArrowDown', 'ArrowDown']);
          await expect(page.getByTestId('nested-submenu-trigger')).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('nested-submenu')).toBeAttached();

          // Deviation (2026-11-05): `user.click` in Base UI dispatches on the button; a real press lands on the modal menu's
          // internal backdrop above it (MenuRoot.test.tsx:1208).
          await clickThrough(page, page.getByTestId('outside'));

          await expect(popup(page)).toHaveCount(0);
          await expect(submenu(page)).toHaveCount(0);
          await expect(page.getByTestId('nested-submenu')).toHaveCount(0);
        });

        test('calls onOpenChange with false exactly once per menu when a submenu item is clicked', async ({ page, mode }) => {
          const fixture = await render(page, mode, { ...base, subDelay: 0, subCloseDelay: 50 });
          await trigger(page).click();
          await expect(popup(page)).toBeVisible();

          await moveTo(page, page.getByTestId('submenu-trigger'));
          await expect(submenu(page)).toBeVisible();

          // Leaving the trigger for the item schedules the delayed hover close; the click comes before it fires.
          await moveTo(page, page.getByTestId('item-4_1'));
          await page.mouse.down();
          await page.mouse.up();
          await expect(popup(page)).toHaveCount(0);

          // Wait out the pending hover close timers; they must not refire a close.
          await withClock(page, async () => {
            await page.clock.runFor(100);
          });
          await settle(fixture);
          expect((await openChanges(fixture)).filter((change) => change.startsWith('false'))).toHaveLength(1);
          const closes = (await recorded(fixture, 'data-submenu-open-changes')).filter((change) => change.startsWith('false'));
          expect(closes).toHaveLength(1);
          expect(closes.filter((change) => change === 'false:trigger-hover')).toHaveLength(0);
        });

        test('returns focus to submenu triggers when closing nested menus', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).click();
          await expect(popup(page)).toBeFocused();

          await arrowTo(page, ['ArrowDown', 'ArrowDown', 'ArrowDown', 'ArrowDown']);
          const submenuTrigger = page.getByTestId('submenu-trigger');
          await expect(submenuTrigger).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('item-4_1')).toBeFocused();
          await arrowTo(page, ['ArrowDown', 'ArrowDown']);
          const nestedSubmenuTrigger = page.getByTestId('nested-submenu-trigger');
          await expect(nestedSubmenuTrigger).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('item-4_3_1')).toBeFocused();

          await page.keyboard.press('ArrowLeft');
          await expect(page.getByTestId('nested-submenu')).toHaveCount(0);
          await expect(nestedSubmenuTrigger).toBeFocused();

          await page.keyboard.press('ArrowLeft');
          await expect(submenu(page)).toHaveCount(0);
          await expect(submenuTrigger).toBeFocused();
        });
      });

      test.describe('focus management', () => {
        test('focuses the first item after the menu is opened by keyboard', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).focus();
          await page.keyboard.press('Enter');

          const items = page.getByRole('menuitem');
          await expect(items.first()).toHaveJSProperty('tabIndex', 0);
          const count = await items.count();
          for (let index = 1; index < count; index += 1) {
            await expect(items.nth(index)).toHaveJSProperty('tabIndex', -1);
          }
        });

        test('focuses the first item when down arrow key opens the menu', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).focus();
          await page.keyboard.press('ArrowDown');

          const items = page.getByRole('menuitem');
          await expect(items.first()).toBeFocused();
          await expect(items.first()).toHaveJSProperty('tabIndex', 0);
          const count = await items.count();
          for (let index = 1; index < count; index += 1) {
            await expect(items.nth(index)).toHaveJSProperty('tabIndex', -1);
          }
        });

        test('focuses the last item when up arrow key opens the menu', async ({ page, mode }) => {
          await render(page, mode, base);
          await trigger(page).focus();
          await page.keyboard.press('ArrowUp');

          const items = page.getByRole('menuitem');
          await expect(items.nth(4)).toBeFocused();
          await expect(items.nth(4)).toHaveJSProperty('tabIndex', 0);
          for (const index of [0, 1, 2, 3]) {
            await expect(items.nth(index)).toHaveJSProperty('tabIndex', -1);
          }
        });

        test('focuses the trigger after the menu is closed', async ({ page, mode }) => {
          await render(page, mode, { ...base, inputs: true });
          await trigger(page).click();

          await page.getByRole('menuitem').first().click();

          await expect(trigger(page)).toBeFocused();
        });

        test('focuses the trigger after Escape when the closing menu receives mouseleave', async ({ page, mode }) => {
          await render(page, mode, { ...base, animations: 1, popupClass: 'transition-test-indicator' });
          await trigger(page).click();

          const firstItem = page.getByTestId('item-1');
          await expect(firstItem).toBeVisible();
          await moveTo(page, firstItem);
          await expect(firstItem).toBeFocused();

          await page.keyboard.press('Escape');

          // During the exit transition the positioner receives a mouseleave: here a real one, the pointer moving off it
          // (MenuRoot.test.tsx:1661 fires it without moving the pointer).
          await parkPointer(page);

          await expect(page.getByRole('menu')).toHaveCount(0);
          await expect(trigger(page)).toBeFocused();
        });

        test('focuses the trigger after the menu is closed but not unmounted', async ({ page, mode }) => {
          await render(page, mode, { ...base, inputs: true, keepMounted: true });
          await trigger(page).click();

          await page.getByRole('menuitem').first().click();

          await expect(trigger(page)).toBeFocused();
        });
      });

      test.describe('focus guards', () => {
        test('closes the menu and moves focus to the next element when tabbing forward from the open menu', async ({ page, mode }) => {
          await render(page, mode, { ...base, inputs: true, modal: false });
          await trigger(page).click();
          await expect(popup(page)).toBeVisible();

          await page.getByTestId('item-1').focus();
          await page.keyboard.press('Tab');

          await expect(page.getByTestId('after')).toBeFocused();
          await expect(popup(page)).toHaveCount(0);
        });

        test('closes the menu and moves focus to the trigger when shift-tabbing from the open menu', async ({ page, mode }) => {
          await render(page, mode, { ...base, inputs: true });
          await trigger(page).click();
          await expect(popup(page)).toBeVisible();

          await page.getByTestId('item-1').focus();
          await page.keyboard.press('Shift+Tab');

          await expect(trigger(page)).toBeFocused();
          await expect(popup(page)).toHaveCount(0);
        });
      });

      test.describe('prop: closeParentOnEsc', () => {
        async function openToSubmenu(page: Page): Promise<void> {
          await trigger(page).focus();
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-1')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-2')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('item-3')).toBeFocused();
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('submenu-trigger')).toBeFocused();
          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('item-4_1')).toBeFocused();
        }

        test('does not close the parent menu when the Escape key is pressed by default', async ({ page, mode }) => {
          await render(page, mode, base);
          await openToSubmenu(page);

          await page.keyboard.press('Escape');

          await expect(submenu(page)).toHaveCount(0);
          await expect(page.getByRole('menu')).toHaveCount(1);
          await expect(page.getByRole('menu')).toHaveAttribute('data-testid', 'menu');
        });

        test('closes the parent menu when the Escape key is pressed  if `closeParentOnEsc=true`', async ({ page, mode }) => {
          await render(page, mode, { ...base, closeParentOnEsc: true });
          await openToSubmenu(page);

          await page.keyboard.press('Escape');

          await expect(page.getByRole('menu')).toHaveCount(0);
        });
      });

      test.describe('prop: modal', () => {
        test('should render an internal backdrop when `true`', async ({ page, mode }) => {
          await render(page, mode, { ...base, modal: true });
          await trigger(page).click();
          await expect(page.getByRole('menu')).toBeVisible();

          await expect.poll(() => page.getByTestId('menu-positioner').evaluate((element) => element.previousElementSibling?.getAttribute('role') ?? null)).toBe('presentation');
        });

        test('should not render an internal backdrop when `false`', async ({ page, mode }) => {
          await render(page, mode, { ...base, modal: false });
          await trigger(page).click();
          await expect(page.getByRole('menu')).toBeVisible();

          expect(await page.getByTestId('menu-positioner').evaluate((element) => element.previousElementSibling?.getAttribute('role') ?? null)).toBe(null);
        });
      });

      test.describe('hover close', () => {
        test('does not close after hovering out of a popup opened without trigger hover', async ({ page, mode }) => {
          const fixture = await renderOpen(page, mode, { ...base, open: undefined, defaultOpen: true });
          await expect(page.getByRole('menu')).toBeVisible();

          await moveTo(page, page.getByTestId('menu-positioner'));
          await parkPointer(page);
          await settle(fixture);

          await expect(page.getByRole('menu')).toBeVisible();
          expect(await openChanges(fixture)).toEqual([]);
        });
      });

      test.describe('controlled open interactions', () => {
        test('returns focus to the opener when a menu is opened programmatically', async ({ page, mode }) => {
          await render(page, mode, {
            ...base,
            open: false,
            followOpen: true,
            opener: true,
            triggerId: 'menu-trigger',
            triggerDomId: 'menu-trigger',
            content: 'items',
            items: 'Close menu',
          });

          const opener = page.getByTestId('opener');
          await opener.click();
          await expect(page.getByRole('menu')).toBeVisible();

          await page.getByRole('menuitem', { name: 'Close menu' }).click();

          await expect(page.getByRole('menu')).toHaveCount(0);
          await expect(opener).toBeFocused();
        });

        test('does not close after hovering out of a popup opened externally', async ({ page, mode }) => {
          const fixture = await render(page, mode, { ...base, open: false, followOpen: true, opener: true });
          await page.getByTestId('opener').click();
          await expect(page.getByRole('menu')).toBeVisible();

          await moveTo(page, page.getByTestId('menu-positioner'));
          await parkPointer(page);
          await settle(fixture);

          await expect(page.getByRole('menu')).toBeVisible();
        });

        test('closes after hovering out of a popup opened by its trigger', async ({ page, mode }) => {
          await render(page, mode, { ...base, open: false, followOpen: true, modal: false, openOnHover: true, delay: 0 });

          await moveTo(page, trigger(page));
          await expect(page.getByRole('menu')).toBeVisible();

          await moveTo(page, page.getByTestId('menu-positioner'));
          await parkPointer(page);

          await expect(page.getByRole('menu')).toHaveCount(0);
        });
      });

      test.describe('scroll locking', () => {
        test.describe('interaction type tracking (openMethod)', () => {
          touchTest('should not apply scroll lock when opened via touch', async ({ touchPage, mode }) => {
            await gotoPopupFixture(touchPage, mode, 'menu-root', { ...base, modal: true });
            const box = (await trigger(touchPage).boundingBox())!;
            await touchPage.touchscreen.tap(box.x + box.width / 2, box.y + box.height / 2);

            await expect(touchPage.getByRole('menu')).toBeVisible();
            expect(await isScrollLocked(touchPage)).toBe(false);
          });

          test('should apply scroll lock when opened via mouse', async ({ page, mode }) => {
            await render(page, mode, { ...base, modal: true });
            await trigger(page).click();
            await expect(page.getByRole('menu')).toBeVisible();

            await expect.poll(() => isScrollLocked(page)).toBe(true);
          });
        });

        test.describe('touch scroll lock', () => {
          touchTest('should apply scroll lock when a touch-opened popup covers the viewport width', async ({ touchPage, mode }) => {
            await gotoPopupFixture(touchPage, mode, 'menu-root', { ...base, modal: true, content: 'items', items: '1', positionerStyle: 'width: calc(100vw - 10px)' });
            const box = (await trigger(touchPage).boundingBox())!;
            await touchPage.touchscreen.tap(box.x + box.width / 2, box.y + box.height / 2);

            await expect(touchPage.getByRole('menu')).toBeVisible();
            await expect.poll(() => isScrollLocked(touchPage)).toBe(true);
          });

          touchTest('should not apply scroll lock when a touch-opened popup is narrower than the viewport', async ({ touchPage, mode }) => {
            await gotoPopupFixture(touchPage, mode, 'menu-root', { ...base, modal: true, content: 'items', items: '1', positionerStyle: 'width: 240px' });
            const box = (await trigger(touchPage).boundingBox())!;
            await touchPage.touchscreen.tap(box.x + box.width / 2, box.y + box.height / 2);

            await expect(touchPage.getByRole('menu')).toBeVisible();
            await touchPage.evaluate(() => new Promise((resolve) => requestAnimationFrame(resolve)));
            expect(await isScrollLocked(touchPage)).toBe(false);
          });
        });
      });

      test.describe('prop: actionsRef', () => {
        test('unmounts the menu when the `unmount` method is called', async ({ page, mode }) => {
          const fixture = await render(page, mode, { ...base, retainEveryClose: true });
          await trigger(page).focus();
          await page.keyboard.press('Enter');
          await expect(page.getByTestId('menu')).toBeVisible();

          await trigger(page).click();
          await expect(trigger(page)).toHaveAttribute('aria-expanded', 'false');
          await expect(page.getByTestId('menu')).toBeAttached();

          await fixture.locator('[data-unmount-action]').evaluate((control) => control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })));

          await expect(page.getByTestId('menu')).toHaveCount(0);
        });
      });

      test.describe('prop: openOnHover', () => {
        test('opens when the trigger is hovered and closes when it is no longer hovered', async ({ page, mode }) => {
          await render(page, mode, { ...base, openOnHover: true, delay: 0 });
          await trigger(page).focus();

          await moveTo(page, trigger(page));
          await expect(page.getByRole('menu')).toBeVisible();

          await parkPointer(page);
          await expect(page.getByRole('menu')).toHaveCount(0);
        });

        test('opens the submenu on hover with zero delay', async ({ page, mode }) => {
          await renderOpen(page, mode, { open: undefined, defaultOpen: true, subDelay: 0 });

          await moveTo(page, page.getByTestId('submenu-trigger'));

          await expect(submenu(page)).toBeAttached();
        });

        test('does not clear body pointer-events styles when closing a scoped submenu', async ({ page, mode }) => {
          await renderOpen(page, mode, { ...base, open: undefined, defaultOpen: true, subDelay: 0, subCloseDelay: 0 });
          await moveTo(page, page.getByTestId('submenu-trigger'));
          await expect(submenu(page)).toBeVisible();

          await page.evaluate(() => {
            document.body.style.pointerEvents = 'none';
          });

          // As Base UI (MenuRoot.test.tsx:2204): fired, since the safe polygon's `pointer-events: none` stops a real pointer here.
          await fire(page.getByTestId('item-2'), 'mousemove');

          await expect(submenu(page)).toHaveCount(0);
          expect(await page.evaluate(() => document.body.style.pointerEvents)).toBe('none');
        });

        test('scopes submenu safePolygon pointer events to the parent menu with keepMounted portal', async ({ page, mode }) => {
          await renderOpen(page, mode, { ...base, open: undefined, defaultOpen: true, content: 'keep-mounted-submenu' });
          await moveTo(page, page.getByTestId('submenu-trigger'));
          await expect(submenu(page)).toBeVisible();

          await expect.poll(() => popup(page).evaluate((element: HTMLElement) => element.style.pointerEvents)).toBe('none');
          expect(await page.getByTestId('submenu-positioner').evaluate((element: HTMLElement) => element.style.pointerEvents)).toBe('auto');
          expect(await page.getByTestId('item-3').evaluate((element: HTMLElement) => element.style.pointerEvents)).toBe('');
        });

        test('should not close when submenu is hovered after root menu is hovered', async ({ page, mode }) => {
          await render(page, mode, { ...base, openOnHover: true, delay: 0, subDelay: 0 });
          await trigger(page).focus();

          await moveTo(page, trigger(page));
          await expect(popup(page)).toBeVisible();
          await moveTo(page, popup(page));
          await moveTo(page, item(page, 'Item 4'));
          await expect(submenu(page)).toBeVisible();

          // A real sweep from the trigger item into the submenu, through the safe polygon.
          await moveTo(page, submenu(page));
          await settle(page.locator('[data-fixture="menu-root"]'));

          await expect(popup(page)).toBeVisible();
          await expect(submenu(page)).toBeVisible();
        });

        test('keeps the parent submenu open after a third-level submenu closes due to sibling hover', async ({ page, mode }) => {
          await render(page, mode, { openOnHover: true, delay: 0, subDelay: 0 });
          await trigger(page).focus();
          await moveTo(page, trigger(page));
          await expect(popup(page)).toBeVisible();

          await moveTo(page, item(page, 'Item 4'));
          await expect(submenu(page)).toBeVisible();

          await moveTo(page, item(page, 'Item 4.3'));
          await expect(page.getByTestId('nested-submenu')).toBeVisible();

          // As Base UI (MenuRoot.test.tsx:2347): fired, since the safe polygon's `pointer-events: none` stops a real pointer here.
          await fire(item(page, 'Item 4.2'), 'mousemove');
          await expect(page.getByTestId('nested-submenu')).toHaveCount(0);

          await fire(submenu(page), 'mouseleave');
          await settle(page.locator('[data-fixture="menu-root"]'));

          await expect(submenu(page)).toBeVisible();
        });

        test.describe('modal behavior', () => {
          test('treats hover-opened menus as modal after a click', async ({ page, mode }) => {
            await render(page, mode, { ...base, openOnHover: true, delay: 0, content: 'items', items: 'Item 1' });
            const positioner = page.getByTestId('menu-positioner');
            const previousRole = () => positioner.evaluate((element) => element.previousElementSibling?.getAttribute('role') ?? null);
            await withClock(page, async () => {
              // As in "reopens on hover after an impatient click closes via item press": the press lands inside the window.
              await holdClock(page);
              await moveTo(page, trigger(page));
              await runClockUntil(page, () => page.getByRole('menu').isVisible());
              await expect(page.getByRole('menu')).toBeVisible();
              expect(await previousRole()).toBe(null);

              await clickThrough(page, trigger(page));
              await page.clock.resume();

              await expect.poll(previousRole).toBe('presentation');
            });
          });

          test('reopens on hover after an impatient click closes via item press', async ({ page, mode }) => {
            await render(page, mode, { ...base, openOnHover: true, delay: 100 });
            await withClock(page, async () => {
              // Base UI ticks to PATIENT_CLICK_THRESHOLD - 1; the open's commit takes real time here, so the clock is held from
              // before the hover and runs only until the page shows the open, and the press lands inside the window.
              await holdClock(page);
              await moveTo(page, trigger(page));
              await runClockUntil(page, () => page.getByRole('menu').isVisible());
              await expect(page.getByRole('menu')).toBeVisible();

              await clickThrough(page, trigger(page));
              await page.clock.resume();
              await page.getByTestId('item-1').click();
              await expect(page.getByRole('menu')).toHaveCount(0);

              // Re-enter the trigger with the real pointer.
              await moveTo(page, trigger(page));
              await page.clock.runFor(100);
              await page.clock.resume();

              await expect(page.getByRole('menu')).toBeVisible();
            });
          });
        });
      });

      test.describe('prop: closeDelay', () => {
        test('should close after delay', async ({ page, mode }) => {
          await render(page, mode, { ...base, openOnHover: true, delay: 0, closeDelay: 100 });
          await moveTo(page, trigger(page));
          await expect(page.getByTestId('item-1')).toBeVisible();

          await withClock(page, async () => {
            await parkPointer(page);
            await page.clock.runFor(50);
            await expect(page.getByTestId('item-1')).toBeVisible();

            await page.clock.runFor(60);
            await page.clock.resume();
            await expect(page.getByTestId('item-1')).toHaveCount(0);
          });
        });
      });

      // MNU-PTR-11, L4 open (settled by the orchestrator 2026-11-04; menu.md's port note): in a real browser the parent item's
      // hover moves DOM focus and the hover-opened submenu closes with `focus-out` before its close delay. Dispatched as Base
      // UI does (MenuRoot.test.tsx:2496), which the safe polygon's `pointer-events: none` also asks for.
      test.describe('prop: closeDelay, a sibling item', () => {
        const openRow = 'MNU-PTR-11, L4 open: the submenu closes with `focus-out` before its close delay (settled 2026-11-04, a real-browser probe of Base UI pending)';

        test('should close submenu after delay when hovering a sibling item', async ({ page, mode }) => {
          test.skip(true, openRow);
          await render(page, mode, { ...base, openOnHover: true, delay: 0, subDelay: 0, subCloseDelay: 100 });
          await moveTo(page, trigger(page));
          await expect(popup(page)).toBeVisible();
          await moveTo(page, item(page, 'Item 4'));
          await expect(submenu(page)).toBeVisible();

          await withClock(page, async () => {
            await fire(item(page, 'Item 1'), 'mousemove');

            // Submenu should still be open after partial delay.
            await page.clock.runFor(50);
            await expect(submenu(page)).toBeAttached();

            // Submenu should close after the full delay.
            await page.clock.runFor(60);
            await page.clock.resume();
            await expect(submenu(page)).toHaveCount(0);
          });
        });

        test('should not restart closeDelay on repeated mousemove over sibling items', async ({ page, mode }) => {
          test.skip(true, openRow);
          await render(page, mode, { ...base, openOnHover: true, delay: 0, subDelay: 0, subCloseDelay: 100 });
          await moveTo(page, trigger(page));
          await expect(popup(page)).toBeVisible();
          await moveTo(page, item(page, 'Item 4'));
          await expect(submenu(page)).toBeVisible();

          await withClock(page, async () => {
            await fire(item(page, 'Item 1'), 'mousemove');

            // Most of the delay, then move again over the sibling.
            await page.clock.runFor(80);
            await expect(submenu(page)).toBeAttached();

            // Move again: this should NOT restart the timer.
            await fire(item(page, 'Item 1'), 'mousemove');

            // 100 ms from the first move, the submenu should close.
            await page.clock.runFor(30);
            await page.clock.resume();
            await expect(submenu(page)).toHaveCount(0);
          });
        });
      });

      test.describe('submenu hover open', () => {
        test('opens a submenu after a plain delay without waiting for the pointer to rest', async ({ page, mode }) => {
          await renderOpen(page, mode, { ...base, subDelay: 100 });
          const point = await center(page.getByTestId('submenu-trigger'));

          await withClock(page, async () => {
            await page.mouse.move(point.x, point.y, { steps: 2 });
            await page.clock.runFor(40);
            await page.mouse.move(point.x + 2, point.y);
            await page.clock.runFor(40);
            await page.mouse.move(point.x + 4, point.y);
            await page.clock.runFor(40);
            await page.clock.resume();

            await expect(submenu(page)).toBeAttached();
          });
        });

        test('cancels a pending submenu hover-open when the pointer leaves via mouseout', async ({ page, mode }) => {
          const fixture = await renderOpen(page, mode, { ...base, subDelay: 100 });
          const submenuTrigger = page.getByTestId('submenu-trigger');
          const otherItem = await page.getByTestId('item-1').elementHandle();

          await withClock(page, async () => {
            // A real sweep delivers `mouseleave` too; the title is the `mouseout` alone that Chrome can leave behind, so it is
            // fired, as Base UI does (MenuRoot.test.tsx:2597), after a real move arms the delayed open.
            await moveTo(page, submenuTrigger);
            await submenuTrigger.evaluate((element, relatedTarget) => {
              element.dispatchEvent(new MouseEvent('mouseout', { bubbles: true, relatedTarget }));
            }, otherItem);

            await page.clock.runFor(200);
            await page.clock.resume();
            await settle(fixture);

            await expect(submenu(page)).toHaveCount(0);
          });
        });

        test('keeps a pending submenu hover-open when mouseout stays within the trigger', async ({ page, mode }) => {
          await renderOpen(page, mode, { ...base, subDelay: 100 });
          const submenuTrigger = page.getByTestId('submenu-trigger');

          await withClock(page, async () => {
            await moveTo(page, submenuTrigger);
            await submenuTrigger.evaluate((element) => {
              element.dispatchEvent(new MouseEvent('mouseout', { bubbles: true, relatedTarget: element }));
            });

            await page.clock.runFor(200);
            await page.clock.resume();

            await expect(submenu(page)).toBeAttached();
          });
        });
      });

      test.describe('mouse interaction', () => {
        test('triggers a menu item and closes the menu on click, drag, release', async ({ page, mode }) => {
          await withClock(page, async () => {
            const fixture = await render(page, mode, { ...base, content: 'items', items: '1|2|3' });
            const start = await center(trigger(page));
            await page.mouse.move(start.x, start.y);
            await page.mouse.down();
            await expect(popup(page)).toBeVisible();
            await page.clock.runFor(200);
            await page.clock.resume();

            await moveTo(page, page.getByTestId('item-2'));
            await page.mouse.up();

            await expect(popup(page)).toHaveCount(0);
            await expect(fixture).toHaveAttribute('data-item-clicks', '2:1');
            expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:item-press']);
          });
        });

        test('closes the menu on click, drag outside, release', async ({ page, mode }) => {
          await withClock(page, async () => {
            const fixture = await render(page, mode, { ...base, content: 'items', items: '1|2|3' });
            const start = await center(trigger(page));
            await page.mouse.move(start.x, start.y);
            await page.mouse.down();
            await expect(popup(page)).toBeVisible();
            await page.clock.runFor(200);
            await page.clock.resume();

            await page.mouse.move(start.x + 400, start.y + 300, { steps: 4 });
            await page.mouse.up();

            await expect(popup(page)).toHaveCount(0);
            expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:cancel-open']);
          });
        });
      });
    });
  }

  for (const { name, detached } of [
    { name: 'contained triggers', detached: undefined },
    { name: 'detached triggers', detached: true },
  ]) {
    test.describe(`when using ${name}, BaseUIChangeEventDetails`, () => {
      test('unmounts on a later normal close after a preventUnmountOnClose cycle and reopen', async ({ page, mode }) => {
        await render(page, mode, { detached, retainFirstClose: true });

        await trigger(page).click();
        await expect(page.getByRole('menu')).toBeAttached();

        await trigger(page).click({ force: true });
        await expect(trigger(page)).not.toHaveAttribute('data-popup-open', /.*/);
        await expect(popup(page)).toBeAttached();

        await trigger(page).click({ force: true });
        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');

        await trigger(page).click({ force: true });
        await expect(popup(page)).toHaveCount(0);
      });
    });
  }

  test('does not leave a tabbable item after tabbing out of a keepMounted menu before close', async ({ page, mode }) => {
    const fixture = await render(page, mode, { detached: true, modal: false, keepMounted: true, inputs: true });
    await trigger(page).focus();
    await page.keyboard.press('Enter');

    const menuItem = page.getByTestId('item-1');
    await expect(menuItem).toBeFocused();
    await expect(menuItem).toHaveAttribute('tabindex', '0');

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('after')).toBeFocused();

    await fixture.locator('[data-close-action]').evaluate((control) => control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })));

    await expect(popup(page)).not.toHaveAttribute('data-open', /.*/);
    await expect(menuItem).toHaveAttribute('tabindex', '-1');
  });

  test.describe('prop: highlightItemOnHover', () => {
    test('highlights an item on mouse move by default', async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode, { noTrigger: true, content: 'items', items: 'Item 1|Item 2|Item 3' });
      const item2 = page.getByTestId('item-Item 2');

      await moveTo(page, item2, { unanchored: true });

      await expect(item2).toBeFocused();
      await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).at(-1)).toBe('Item 2:pointer:mousemove:');
      expect((await recorded(fixture, 'data-highlight-ids', '|')).at(-1)).toBe(await item2.getAttribute('id'));
    });

    test('does not highlight items from mouse movement when disabled', async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode, { noTrigger: true, highlightItemOnHover: false, content: 'items', items: 'Item 1|Item 2|Item 3' });
      const item2 = page.getByTestId('item-Item 2');

      await moveTo(page, item2, { unanchored: true });
      await settle(fixture);

      await expect(item2).not.toBeFocused();
      await expect(item2).not.toHaveAttribute('data-highlighted', /.*/);
    });

    // Quarantined (ADR 11 §4) until 2026-11-25, issue 221: on WebAssembly the submenu trigger can take `data-highlighted`
    // from the mouse enter (5 of 60 and 6 of 60 in two local sets, 2026-11-11); its cause is being traced.
    test.fixme('does not highlight submenu triggers from mouse enter when disabled', {
      tag: '@quarantine',
      annotation: { type: 'issue', description: 'https://github.com/JakeMurrayDev/Blazix.Primitives/issues/221' },
    }, async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode, { noTrigger: true, highlightItemOnHover: false, content: 'lone-submenu' });
      const submenuTrigger = page.getByTestId('submenu-trigger');

      await moveTo(page, submenuTrigger, { unanchored: true });
      await settle(fixture);

      await expect(submenuTrigger).not.toHaveAttribute('data-highlighted', /.*/);
      await expect(submenuTrigger).not.toBeFocused();
    });
  });

  test.describe('prop: disabled', () => {
    test('marks items as disabled when controlled open', async ({ page, mode }) => {
      await renderOpen(page, mode, { noTrigger: true, disabled: true, content: 'disabled-kinds' });

      await expect(page.getByTestId('item')).toHaveAttribute('data-disabled', '');
      await expect(page.getByTestId('checkbox-item')).toHaveAttribute('data-disabled', '');
      await expect(page.getByTestId('radio-item')).toHaveAttribute('data-disabled', '');
    });

    test('does not highlight items with text navigation when controlled open', async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode, { noTrigger: true, disabled: true, content: 'items', items: 'Alpha|Beta' });
      const alpha = page.getByTestId('item-Alpha');
      const beta = page.getByTestId('item-Beta');
      await alpha.focus();

      await page.keyboard.press('b');
      await settle(fixture);

      await expect(beta).not.toHaveAttribute('data-highlighted', /.*/);
      await expect(beta).not.toBeFocused();
    });

    test('disables submenu triggers when controlled open', async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode, { noTrigger: true, disabled: true, subOpenOnHover: false, content: 'lone-submenu' });
      const submenuTrigger = page.getByTestId('submenu-trigger');

      await expect(submenuTrigger).toHaveAttribute('data-disabled', '');

      await submenuTrigger.click({ force: true });
      await settle(fixture);

      await expect(page.getByTestId('submenu-popup')).toHaveCount(0);
    });

    test('does not close or activate items when controlled open', async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode, { noTrigger: true, disabled: true, content: 'items', items: 'Item' });

      await page.getByTestId('item-Item').click({ force: true });
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-item-clicks', '');
      expect(await openChanges(fixture)).toEqual([]);
    });
  });

  test.describe('dynamic items', () => {
    test('skips null items when navigating', async ({ page, mode }) => {
      await render(page, mode, { content: 'dynamic' });
      await trigger(page).focus();

      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('menu')).toBeVisible();
      await expect(page.getByRole('menuitem')).toHaveCount(3);
      await expect(item(page, 'Add to Library')).toBeFocused();

      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown'); // Share
      await page.keyboard.press('ArrowDown'); // loops back to Add to Library

      await expect(item(page, 'Add to Library')).toBeFocused();
    });
  });

  test.describe('trigger render cost', () => {
    test('does not re-render the trigger while navigating the list', async ({ page, mode }) => {
      const fixture = await render(page, mode, { unobserved: true, triggerRender: 'count', content: 'items', items: 'Item 0|Item 1|Item 2|Item 3|Item 4|Item 5|Item 6|Item 7|Item 8|Item 9' });
      await trigger(page).click();
      await expect(page.getByRole('menu')).toBeFocused();

      // The barrier reads the count before it re-renders the fixture, so the barrier's own render shows in the next read only.
      await settle(fixture);
      await settle(fixture);
      const before = Number(await fixture.getAttribute('data-trigger-renders'));

      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await expect(item(page, 'Item 2')).toBeFocused();
      await settle(fixture);

      expect(Number(await fixture.getAttribute('data-trigger-renders'))).toBe(before + 1);
    });
  });

  test.describe('prop: onItemHighlighted', () => {
    async function openWithKeyboard(page: Page): Promise<void> {
      await trigger(page).focus();
      await page.keyboard.press('Enter');
      await expect(item(page, 'One')).toBeFocused();
    }

    test('passes the keydown event for keyboard highlights', async ({ page, mode }) => {
      const fixture = await render(page, mode, { content: 'highlight' });
      await openWithKeyboard(page);

      await page.keyboard.press('ArrowDown');
      await expect(item(page, 'Two')).toBeFocused();

      await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).at(-1)).toBe('Two:keyboard:keydown:ArrowDown');
    });

    test('passes the keydown event for typeahead highlights', async ({ page, mode }) => {
      const fixture = await render(page, mode, { content: 'highlight' });
      await openWithKeyboard(page);

      await page.keyboard.press('t');
      await expect(item(page, 'Two')).toBeFocused();

      await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).at(-1)).toBe('Two:keyboard:keydown:t');
    });

    test('passes native mouse and pointer events for pointer highlights', async ({ page, mode }) => {
      const fixture = await render(page, mode, { content: 'highlight' });
      await trigger(page).click();
      const two = item(page, 'Two');
      await expect(two).toBeVisible();

      await moveTo(page, two);
      await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).at(-1)).toBe('Two:pointer:mousemove:');
      expect((await recorded(fixture, 'data-highlight-ids', '|')).at(-1)).toBe(await two.getAttribute('id'));

      // Deviation (2026-11-05): Base UI dispatches `pointerout` because React derives its onPointerLeave from it
      // (MenuRoot.test.tsx:3357); the module listens for the native `pointerleave`, which a real pointer sends with it.
      await parkPointer(page);
      await expect.poll(async () => (await fixture.getAttribute('data-highlights'))?.split('|').at(-1)).toBe(':pointer:pointerleave:');
      expect(((await fixture.getAttribute('data-highlight-ids')) ?? '').split('|').at(-1)).toBe('');
    });

    test('passes the mouse event when a submenu trigger is hovered', async ({ page, mode }) => {
      const fixture = await render(page, mode, { content: 'highlight' });
      await trigger(page).click();
      const submenuTrigger = item(page, 'Three');
      await expect(submenuTrigger).toBeVisible();

      await moveTo(page, submenuTrigger);

      await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).at(-1)).toMatch(/^Three:pointer:mouse(enter|move):$/);
      expect((await recorded(fixture, 'data-highlight-ids', '|')).at(-1)).toBe(await submenuTrigger.getAttribute('id'));
    });

    test.describe('reporting after the callback or the list changes', () => {
      test('does not report a stale highlight reason for a later list change', async ({ page, mode }) => {
        const fixture = await render(page, mode, { content: 'reporting' });
        await openWithKeyboard(page);
        await page.keyboard.press('ArrowDown');
        await expect(item(page, 'Two')).toBeFocused();
        await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).at(-1)).toBe('Two:keyboard:keydown:ArrowDown');
        const before = (await recorded(fixture, 'data-highlights', '|')).length;

        await fixture.locator('[data-insert-item]').evaluate((control) => control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })));

        await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).length).toBeGreaterThan(before);
        expect((await recorded(fixture, 'data-highlights', '|')).at(-1)).toMatch(/:none:/);
      });

      test('reports highlights correctly after onItemHighlighted is removed and added back', async ({ page, mode }) => {
        const fixture = await render(page, mode, { content: 'items', items: 'One|Two' });
        const toggle = fixture.locator('[data-observe-highlights]');
        await trigger(page).focus();
        await page.keyboard.press('Enter');
        await expect(page.getByTestId('item-One')).toBeFocused();

        // Moving while unobserved, then back once observed again, reports the keyboard move.
        await toggle.evaluate((control) => control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })));
        await settle(fixture);
        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('item-Two')).toBeFocused();
        await toggle.evaluate((control) => control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })));
        await settle(fixture);

        const before = (await recorded(fixture, 'data-highlights', '|')).length;
        await page.keyboard.press('ArrowUp');

        await expect.poll(async () => (await recorded(fixture, 'data-highlights', '|')).length).toBeGreaterThan(before);
        expect((await recorded(fixture, 'data-highlights', '|')).at(-1)).toBe('One:keyboard:keydown:ArrowUp');
        expect((await recorded(fixture, 'data-highlight-ids', '|')).at(-1)).toBe(await page.getByTestId('item-One').getAttribute('id'));
      });

      test('reports clearing a highlight made before onItemHighlighted was added', async ({ page, mode }) => {
        const fixture = await render(page, mode, { unobserved: true, content: 'items', items: 'One|Two' });
        await trigger(page).click();
        const one = page.getByTestId('item-One');
        await expect(one).toBeVisible();

        await moveTo(page, one);
        await expect(one).toHaveAttribute('data-highlighted', '');

        await fixture.locator('[data-observe-highlights]').evaluate((control) => control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })));
        await settle(fixture);
        await parkPointer(page);

        await expect.poll(async () => (await fixture.getAttribute('data-highlights'))?.split('|').at(-1)).toMatch(/^:pointer:/);
      });
    });
  });
});
