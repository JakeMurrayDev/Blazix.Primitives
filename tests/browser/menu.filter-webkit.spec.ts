import { type Locator } from '@playwright/test';
import { expect, Fixture, test, type Page, type RenderMode } from './helpers';

// MenuFilterRoot.webkit.test.tsx (MNU-ARIA-15, A11Y-02) on the `menu-filter-webkit` fixture.
// Deviation (2026-11-12): Base UI's file mocks `@base-ui/utils/platform` so `engine.webkit` holds in its Chromium run; the port
// reads the engine once, when js/platform.ts loads (WebKit is `CSS.supports('-webkit-backdrop-filter:none')`), so an init
// script answers that check yes before any script runs, the same flag in the same Chromium.

async function render(page: Page, mode: RenderMode, name = 'basic'): Promise<Locator> {
  await page.addInitScript(() => {
    const supports = CSS.supports.bind(CSS);
    CSS.supports = ((...args: unknown[]) => (String(args[0]).includes('-webkit-backdrop-filter') ? true : (supports as (...a: unknown[]) => boolean)(...args))) as typeof CSS.supports;
  });
  await page.goto(`/${mode}/menu-filter-webkit?case=${name}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('menu-filter-webkit'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

function input(page: Page, name = 'Filter actions'): Locator {
  return page.getByRole('searchbox', { name });
}

function item(page: Page, name: string, role: 'menuitem' | 'menuitemcheckbox' | 'menuitemradio' = 'menuitem'): Locator {
  return page.getByRole(role, { name, exact: true });
}

// A click dispatched on a page button inside the popup runs its handler without moving focus off the input.
function press(page: Page, name: string): Promise<void> {
  return page.getByRole('button', { name }).evaluate((element) => {
    element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
  });
}

test.describe('<Menu.FilterProvider><Menu.Root/></Menu.FilterProvider> (WebKit)', () => {
  test('marks only the highlighted item while its input is focused in WebKit', async ({ page, mode }) => {
    await render(page, mode);
    await expect(input(page)).toBeFocused();
    const apple = item(page, 'Apple');
    const banana = item(page, 'Banana');
    await expect(apple).not.toHaveAttribute('aria-selected');

    await page.keyboard.press('ArrowDown');

    await expect(apple).toHaveAttribute('aria-selected', 'true');
    await expect(banana).not.toHaveAttribute('aria-selected');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await apple.getAttribute('id')) ?? 'missing');

    await page.keyboard.press('ArrowDown');

    await expect(apple).not.toHaveAttribute('aria-selected');
    await expect(banana).toHaveAttribute('aria-selected', 'true');

    await input(page).evaluate((element: HTMLInputElement) => element.blur());
    await expect(banana).not.toHaveAttribute('aria-selected');

    await input(page).focus();
    await expect(banana).toHaveAttribute('aria-selected', 'true');
  });

  // Base UI's "keeps selection state for an auto-focused input in Strict Mode" is not portable: Blazor has no Strict
  // Mode's doubled effects, and the port has no autoFocus (the open menu's initial focus lands on the input, as above).

  // Base UI's input focuses itself through autoFocus; the port's has none, so the test focuses the replacement.
  test('keeps selection state when a focused input is replaced', async ({ page, mode }) => {
    await render(page, mode);
    await expect(input(page)).toBeFocused();
    const first = await input(page).elementHandle();

    await press(page, 'replace input');
    await expect.poll(() => input(page).evaluate((element, old) => element !== old, first)).toBe(true);
    await input(page).focus();
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('ArrowDown');

    await expect(item(page, 'Apple')).toHaveAttribute('aria-selected', 'true');
  });

  test('keeps selection in sync when a focused input is replaced without autofocus', async ({ page, mode }) => {
    await render(page, mode);
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    const apple = item(page, 'Apple');
    await expect(apple).toHaveAttribute('aria-selected', 'true');
    const first = await input(page).elementHandle();

    await press(page, 'replace input');
    await expect.poll(() => input(page).evaluate((element, old) => element !== old, first)).toBe(true);
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

    // Engines differ in whether they restore focus after removing the old input.
    const focused = await input(page).evaluate((element) => element.matches(':focus'));
    if (focused) {
      await expect(apple).toHaveAttribute('aria-selected', 'true');
    } else {
      await expect(apple).not.toHaveAttribute('aria-selected');
    }
  });

  test('clears selection when the focused input unmounts while the menu stays open', async ({ page, mode }) => {
    await render(page, mode);
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    const apple = item(page, 'Apple');
    await expect(apple).toHaveAttribute('aria-selected', 'true');

    await press(page, 'hide input');

    await expect(input(page)).toHaveCount(0);
    await expect(apple).toBeAttached();
    await expect(apple).not.toHaveAttribute('aria-selected');
  });

  test('does not restore selection after a focused input unmounts and the menu reopens on hover', async ({ page, mode }) => {
    await render(page, mode, 'hover');
    await expect(input(page)).toBeFocused();
    await expect(item(page, 'Apple')).toHaveAttribute('aria-selected', 'true');

    await press(page, 'close');
    await expect(input(page)).toHaveCount(0);

    await page.getByRole('button', { name: 'Actions' }).hover();

    await expect(input(page)).toBeVisible();
    await expect(input(page)).not.toBeFocused();
    await expect(item(page, 'Apple')).not.toHaveAttribute('aria-selected');
  });

  test('preserves checked state on checkbox and radio items in WebKit', async ({ page, mode }) => {
    await render(page, mode, 'kinds');
    const checkbox = item(page, 'Details', 'menuitemcheckbox');
    const radio = item(page, 'Date', 'menuitemradio');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(checkbox).toHaveAttribute('aria-selected', 'true');
    await expect(checkbox).toHaveAttribute('aria-checked', 'true');

    await page.keyboard.press('ArrowDown');
    await expect(radio).toHaveAttribute('aria-selected', 'true');
    await expect(radio).toHaveAttribute('aria-checked', 'true');
  });

  test('marks highlighted links and submenu triggers while their input is focused in WebKit', async ({ page, mode }) => {
    await render(page, mode, 'links');
    const link = item(page, 'Documentation');
    const submenuTrigger = item(page, 'More actions');
    const plainSubmenuTrigger = item(page, 'Plain submenu');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(link).toHaveAttribute('aria-selected', 'true');
    await expect(submenuTrigger).not.toHaveAttribute('aria-selected');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await link.getAttribute('id')) ?? 'missing');

    await page.keyboard.press('ArrowDown');
    await expect(link).not.toHaveAttribute('aria-selected');
    await expect(submenuTrigger).toHaveAttribute('aria-selected', 'true');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await submenuTrigger.getAttribute('id')) ?? 'missing');

    await expect(plainSubmenuTrigger).not.toHaveAttribute('aria-selected');
    await page.keyboard.press('ArrowDown');

    await expect(submenuTrigger).not.toHaveAttribute('aria-selected');
    await expect(plainSubmenuTrigger).toHaveAttribute('aria-selected', 'true');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await plainSubmenuTrigger.getAttribute('id')) ?? 'missing');
  });

  test('marks items inside an opened submenu while its input is focused', async ({ page, mode }) => {
    await render(page, mode, 'submenu');
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');

    const submenuInput = input(page, 'Filter more actions');
    await expect(submenuInput).toBeFocused();
    const share = item(page, 'Share');
    const submenuTrigger = item(page, 'More actions');
    await expect(submenuTrigger).not.toHaveAttribute('aria-selected');
    await expect(share).toHaveAttribute('aria-selected', 'true');

    await page.keyboard.press('ArrowUp');
    await expect(share).not.toHaveAttribute('aria-selected');

    await page.keyboard.press('ArrowDown');

    await expect(share).toHaveAttribute('aria-selected', 'true');
    await expect(submenuInput).toHaveAttribute('aria-activedescendant', (await share.getAttribute('id')) ?? 'missing');
  });

  test('leaves plain menu items alone, which navigate with real focus', async ({ page, mode }) => {
    await render(page, mode, 'plain');
    const apple = item(page, 'Apple');
    await expect(apple).toBeVisible();
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

    await expect(apple).not.toHaveAttribute('aria-selected');
  });
});
