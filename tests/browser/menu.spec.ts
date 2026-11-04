import { type Locator } from '@playwright/test';
import { expect, test, type Page } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { popupFocusPropsTests } from './suites/popupFocusPropsTests';
import { gotoPopupFixture, openChanges, settle } from './suites/popupFixture';
import { popupListConformanceTests } from './suites/popupListConformanceTests';
import { positionerConformanceTests } from './suites/positioner-conformance';
import { viewportConformanceTests } from './suites/viewportConformanceTests';

// The [L4] rows of docs/components/menu.md (M5 track 6, issue 172) on the `menu-*` fixtures, every title on both render
// modes. The shared popup suites run with Menu's values (MenuRoot.test.tsx:127-143, MenuRoot.detached-triggers.test.tsx:14-26,
// MenuViewport.test.tsx:25-46, MenuPopup.test.tsx:199-215, MenuPositioner.test.tsx:633-648): a trigger pressed by click
// (the `interactions` module toggles on its `mousedown`), the popup's role and the trigger's aria-haspopup `menu`, no close
// part, a positioner and a viewport.

popupConformanceTests({
  fixture: 'menu-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'menu',
});

detachedTriggersConformanceTests({
  fixture: 'menu-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: true,
  close: false,
  positioner: true,
  viewport: true,
});

viewportConformanceTests({
  fixture: 'menu-viewport',
  openOn: 'click',
});

positionerConformanceTests({
  fixture: 'menu-positioner',
  viewport: true,
});

popupFocusPropsTests({
  fixture: 'menu-focus',
  initialFocus: false,
});

// MenuRoot.test.tsx:267-294: the popup-list suite with Menu's options, on Menu's own parts (track 1 ran it on its test-only
// host, composite-list.spec.ts:13).
popupListConformanceTests({
  fixture: 'menu-list',
  itemRole: 'menuitem',
  listEnd: 'wrap',
});

// Menu's own titles on `menu-nested` (MenuRoot.test.tsx, MenuItem.test.tsx, MenuCheckboxItem.test.tsx,
// MenuRadioItem.test.tsx, MenuSubmenuTrigger.test.tsx), with real pointer presses, moves and releases. The sibling
// close titles (MNU-PTR-11) are not here yet: in a browser the parent item's hover moves focus, and the submenu closes with
// `focus-out` before its close delay. Timed titles use page.clock: time flows after `install` until a
// `runFor`, which leaves it held, so a title resumes it once its timers have run (C#'s renders reach the page through
// timers too) and again in `finally`, because the clock belongs to the worker's context.

async function center(locator: Locator): Promise<{ x: number; y: number }> {
  const box = (await locator.boundingBox())!;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

async function moveTo(page: Page, locator: Locator): Promise<void> {
  const point = await center(locator);
  await page.mouse.move(point.x, point.y, { steps: 4 });
}

async function submenuChanges(fixture: Locator): Promise<string[]> {
  return ((await fixture.getAttribute('data-submenu-open-changes')) ?? '').split(' ').filter(Boolean);
}

// The worker's page keeps the pointer where the last title left it; a pointer resting over an item of a newly opened
// popup would take the highlight, so the keyboard titles park it in the corner first.
async function openByKeyboard(page: Page): Promise<void> {
  await page.mouse.move(0, 0);
  await page.getByTestId('trigger').focus();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByTestId('item-one')).toBeFocused();
}

async function openSubmenuByHover(page: Page, fixture: Locator): Promise<void> {
  await page.getByTestId('trigger').click();
  await expect(page.getByTestId('popup')).toBeVisible();
  await moveTo(page, page.getByTestId('submenu-trigger'));
  await expect(page.getByTestId('submenu-trigger')).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByTestId('submenu-popup')).toBeVisible();
  expect(await submenuChanges(fixture)).toEqual(['true:trigger-hover']);
}

test.describe('<Menu.Root /> press, drag and release', () => {
  test('triggers a menu item and closes the menu on click, drag, release', async ({ page, mode }) => {
    await page.clock.install();
    try {
      const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
      const trigger = page.getByTestId('trigger');
      const start = await center(trigger);
      await page.mouse.move(start.x, start.y);
      await page.mouse.down();
      await expect(trigger).toHaveAttribute('aria-expanded', 'true');
      await expect(page.getByTestId('popup')).toBeVisible();
      await page.clock.runFor(200);
      await page.clock.resume();

      await moveTo(page, page.getByTestId('item-two'));
      await page.mouse.up();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(fixture).toHaveAttribute('data-item-clicks', 'Two:1');
      expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:item-press']);
    } finally {
      await page.clock.resume();
    }
  });

  test('closes the menu on click, drag outside, release', async ({ page, mode }) => {
    await page.clock.install();
    try {
      const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
      const trigger = page.getByTestId('trigger');
      const start = await center(trigger);
      await page.mouse.move(start.x, start.y);
      await page.mouse.down();
      await expect(page.getByTestId('popup')).toBeVisible();
      await page.clock.runFor(200);
      await page.clock.resume();

      await page.mouse.move(start.x + 400, start.y + 300, { steps: 4 });
      await page.mouse.up();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:cancel-open']);
      await expect(fixture).toHaveAttribute('data-item-clicks', '');
    } finally {
      await page.clock.resume();
    }
  });

  test('selects nested items with click, drag, release', async ({ page, mode }) => {
    await page.clock.install();
    try {
      const fixture = await gotoPopupFixture(page, mode, 'menu-nested', { submenuDelay: 0 });
      const trigger = page.getByTestId('trigger');
      const start = await center(trigger);
      await page.mouse.move(start.x, start.y);
      await page.mouse.down();
      await expect(page.getByTestId('popup')).toBeVisible();
      await page.clock.runFor(200);
      await page.clock.resume();

      await moveTo(page, page.getByTestId('submenu-trigger'));
      await expect(page.getByTestId('submenu-popup')).toBeVisible();
      await moveTo(page, page.getByTestId('sub-two'));
      await page.mouse.up();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(fixture).toHaveAttribute('data-item-clicks', 'Sub two:1');
      expect(await submenuChanges(fixture)).toEqual(['true:trigger-hover', 'false:item-press']);
    } finally {
      await page.clock.resume();
    }
  });
});

test.describe('<Menu.Item />', () => {
  test('closes the menu when the item is clicked by default', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
    await page.getByTestId('trigger').click();
    await page.getByTestId('item-one').click();

    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'false');
    await expect(fixture).toHaveAttribute('data-item-clicks', 'One:1');
    expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:item-press']);
  });

  test('closes with a `detail === 0` click event on keyboard item activation', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
    await openByKeyboard(page);
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'false');
    await expect(fixture).toHaveAttribute('data-item-clicks', 'One:0');
    expect(await openChanges(fixture)).toEqual(['true:list-navigation', 'false:item-press']);
    await expect(page.getByTestId('trigger')).toBeFocused();
  });
});

test.describe('<Menu.CheckboxItem /> and <Menu.RadioItem />', () => {
  test('toggles the checked state when clicked', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
    await page.getByTestId('trigger').click();
    const item = page.getByTestId('checkbox');

    await item.click();
    await expect(fixture).toHaveAttribute('data-checked', 'true');
    await expect(item).toHaveAttribute('aria-checked', 'true');
    await item.click();
    await expect(fixture).toHaveAttribute('data-checked', 'false');
    await expect(item).toHaveAttribute('aria-checked', 'false');
    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'true');
  });

  test('calls `onValueChange` when the item is clicked', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
    await page.getByTestId('trigger').click();

    await page.getByTestId('radio-beta').click();

    await expect(fixture).toHaveAttribute('data-radio', 'beta');
    await expect(page.getByTestId('radio-beta')).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByTestId('radio-alpha')).toHaveAttribute('aria-checked', 'false');
  });
});

test.describe('<Menu.SubmenuTrigger />', () => {
  test('opens the submenu on hover with zero delay', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested', { submenuDelay: 0 });
    await openSubmenuByHover(page, fixture);
  });

  test('port: the submenu opens on hover after its default 100 ms delay', async ({ page, mode }) => {
    await page.clock.install();
    try {
      const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
      await page.getByTestId('trigger').click();
      await expect(page.getByTestId('popup')).toBeVisible();

      await moveTo(page, page.getByTestId('submenu-trigger'));
      await page.clock.runFor(40);
      await expect(page.getByTestId('submenu-trigger')).toHaveAttribute('aria-expanded', 'false');
      await page.clock.runFor(100);
      await page.clock.resume();

      await expect(page.getByTestId('submenu-trigger')).toHaveAttribute('aria-expanded', 'true');
      expect(await submenuChanges(fixture)).toEqual(['true:trigger-hover']);
    } finally {
      await page.clock.resume();
    }
  });

  test('opens a nested menu of a vertical ltr menu with ArrowRight key and closes it with ArrowLeft', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
    await openByKeyboard(page);
    await page.keyboard.press('End');
    await page.keyboard.press('ArrowUp');
    await expect(page.getByTestId('submenu-trigger')).toBeFocused();

    await page.keyboard.press('ArrowRight');
    await expect(page.getByTestId('sub-one')).toBeFocused();
    await page.keyboard.press('ArrowLeft');

    await expect(page.getByTestId('submenu-trigger')).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('submenu-trigger')).toBeFocused();
    expect(await submenuChanges(fixture)).toEqual(['true:list-navigation', 'false:list-navigation']);
  });

  test('does not close the parent menu when the Escape key is pressed by default', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested');
    await openByKeyboard(page);
    await page.keyboard.press('End');
    await page.keyboard.press('ArrowUp');
    await page.keyboard.press('ArrowRight');
    await expect(page.getByTestId('sub-one')).toBeFocused();

    await page.keyboard.press('Escape');

    await expect(page.getByTestId('submenu-trigger')).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'true');
    expect(await openChanges(fixture)).toEqual(['true:list-navigation']);
  });

  test('closes the parent menu when the Escape key is pressed if `closeParentOnEsc=true`', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested', { closeParentOnEsc: true });
    await openByKeyboard(page);
    await page.keyboard.press('End');
    await page.keyboard.press('ArrowUp');
    await page.keyboard.press('ArrowRight');
    await expect(page.getByTestId('sub-one')).toBeFocused();

    await page.keyboard.press('Escape');

    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'false');
    expect(await openChanges(fixture)).toEqual(['true:list-navigation', 'false:escape-key']);
    expect(await submenuChanges(fixture)).toEqual(['true:list-navigation', 'false:escape-key']);
  });

  test('calls onOpenChange with false exactly once per menu when a submenu item is clicked', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-nested', { submenuDelay: 0 });
    await openSubmenuByHover(page, fixture);

    await page.getByTestId('sub-one').click();

    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:item-press']);
    expect(await submenuChanges(fixture)).toEqual(['true:trigger-hover', 'false:item-press']);
    await expect(fixture).toHaveAttribute('data-item-clicks', 'Sub one:1');
  });
});

test.describe('<Menu.Trigger /> hover', () => {
  test('opens when the trigger is hovered and closes when it is no longer hovered', async ({ page, mode }) => {
    await page.clock.install();
    try {
      const fixture = await gotoPopupFixture(page, mode, 'menu-nested', { openOnHover: true });
      await moveTo(page, page.getByTestId('trigger'));
      await page.clock.runFor(110);
      await page.clock.resume();
      await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'true');

      await page.mouse.move(1, 1, { steps: 4 });

      await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'false');
      expect(await openChanges(fixture)).toEqual(['true:trigger-hover', 'false:trigger-hover']);
    } finally {
      await page.clock.resume();
    }
  });
});
