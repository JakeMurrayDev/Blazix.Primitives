import { type Locator } from '@playwright/test';
import { type Route } from '@playwright/test';
import { delayCircuit, expect, gotoFixture, test, type Page, type RenderMode } from './helpers';
import { center, clickThrough, fire, holdClock, moveTo, parkPointer, recordSelectors, recorded, selectorSeen, touchTest, waitFrames, waitMs, withClock } from './menu.helpers';
import { gotoPopupFixture, openChanges, pressInPage, settle, type PopupFixtureOptions } from './suites/popupFixture';

// The L4 titles of MenuTrigger.test.tsx, MenuBackdrop.test.tsx, MenuSubmenuTrigger.test.tsx,
// MenuSubmenuTrigger.screenReaderPress.test.tsx, MenuSubmenuTrigger.voiceOver.test.tsx, MenuPopup.test.tsx, MenuList.test.tsx's
// plain block, MenuItem.test.tsx, MenuCheckboxItem.test.tsx, MenuRadioItem.test.tsx, MenuLinkItem.test.tsx, MenuPositioner.test.tsx
// and MenuRoot.detached-triggers.test.tsx's nested menus (M5 track 6, issue 172; docs/components/menu.md) on `menu-root`, titles
// verbatim, every title on both render modes.

async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
  await parkPointer(page);
  return gotoPopupFixture(page, mode, 'menu-root', options);
}

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

test.describe('<Menu.Trigger />', () => {
  test.describe('tabbing backward from the open trigger', () => {
    for (const modal of [true, false]) {
      test(`focuses the preceding element when modal=${modal}`, async ({ page, mode }) => {
        await render(page, mode, { animations: 1, beforeButton: true, modal, content: 'items', items: 'Item' });
        await trigger(page).click();
        await expect(page.getByRole('menu')).toBeFocused();

        // Menu normally closes on Shift+Tab from its content. Focus the trigger while it stays open.
        await trigger(page).focus();
        await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');

        await page.keyboard.press('Shift+Tab');

        await expect(page.getByTestId('before-button')).toBeFocused();
        await expect(page.getByRole('menu')).toHaveCount(0);
      });
    }
  });

  test.describe('prop: disabled', () => {
    test('should not open the menu when clicked', async ({ page, mode }) => {
      const fixture = await render(page, mode, { triggerDisabled: true, content: 'items', items: '' });

      await trigger(page).click({ force: true });
      await settle(fixture);

      await expect(page.getByRole('menu')).toHaveCount(0);
      expect(await openChanges(fixture)).toEqual([]);
    });
  });

  test('removes the hover mouseup listener when unmounted before mouseup', async ({ page, mode }) => {
    await page.addInitScript(() => {
      const added: unknown[] = [];
      const removed: unknown[] = [];
      (window as unknown as { mouseupListeners: { added: unknown[]; removed: unknown[] } }).mouseupListeners = { added, removed };
      const add = document.addEventListener;
      const remove = document.removeEventListener;
      document.addEventListener = function (this: Document, type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) {
        if (type === 'mouseup') {
          added.push(listener);
        }
        add.call(this, type, listener, options);
      } as typeof document.addEventListener;
      document.removeEventListener = function (this: Document, type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) {
        if (type === 'mouseup') {
          removed.push(listener);
        }
        remove.call(this, type, listener, options);
      } as typeof document.removeEventListener;
    });
    const fixture = await render(page, mode, { openOnHover: true, delay: 0, content: 'items', items: '' });
    await page.evaluate(() => {
      (window as unknown as { mouseupListeners: { added: unknown[] } }).mouseupListeners.added.length = 0;
    });

    await moveTo(page, trigger(page));
    await expect(page.getByRole('menu')).toBeAttached();
    expect(await page.evaluate(() => (window as unknown as { mouseupListeners: { added: unknown[] } }).mouseupListeners.added.length)).toBeGreaterThan(0);

    await pressInPage(fixture.locator('[data-toggle-root]'));
    await expect(trigger(page)).toHaveCount(0);

    await expect
      .poll(() =>
        page.evaluate(() => {
          const { added, removed } = (window as unknown as { mouseupListeners: { added: unknown[]; removed: unknown[] } }).mouseupListeners;
          return removed.includes(added[0]);
        }),
      )
      .toBe(true);
  });

  test.describe('keyboard navigation', () => {
    for (const { buttonType, key } of [
      { buttonType: 'native', key: 'ArrowUp' },
      { buttonType: 'native', key: 'ArrowDown' },
      { buttonType: 'non-native', key: 'ArrowUp' },
      { buttonType: 'non-native', key: 'ArrowDown' },
      { buttonType: 'non-native', key: 'Enter' },
      { buttonType: 'non-native', key: 'Space' },
    ]) {
      test(`opens the menu when pressing ${key} on a ${buttonType} button`, async ({ page, mode }) => {
        await render(page, mode, { triggerNonNative: buttonType !== 'native' || undefined, content: 'items', items: '1' });
        const button = page.getByRole('button', { name: 'Toggle', exact: true });
        expect(await button.evaluate((element) => element.tagName)).toBe(buttonType === 'native' ? 'BUTTON' : 'SPAN');
        await button.focus();

        await page.keyboard.press(key);

        await expect(page.getByRole('menu')).toBeVisible();
      });
    }
  });

  test.describe('style hooks', () => {
    test('should have the data-popup-open and data-pressed attributes when open', async ({ page, mode }) => {
      await render(page, mode, { noPortal: true });

      await trigger(page).evaluate((element: HTMLElement) => element.click());

      await expect(trigger(page)).toHaveAttribute('data-popup-open', '');
      await expect(trigger(page)).toHaveAttribute('data-pressed', '');
    });

    test('keeps the data-popup-open attribute and handle.isOpen when a controlled close is vetoed', async ({ page, mode }) => {
      const fixture = await render(page, mode, { handle: true, open: false, followOpenOnly: true, content: 'items', items: 'Item' });

      await trigger(page).click();
      await expect(page.getByRole('menu')).toBeVisible();
      await expect(trigger(page)).toHaveAttribute('data-popup-open', '');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-handle-open', 'true');

      // A real press lands on the modal menu's internal backdrop above the button (MenuTrigger.test.tsx:253).
      await clickThrough(page, page.getByTestId('outside'));
      await expect.poll(() => openChanges(fixture)).toContain('false:outside-press');
      await settle(fixture);

      await expect(page.getByRole('menu')).toHaveAttribute('data-open', '');
      await expect(trigger(page)).toHaveAttribute('data-popup-open', '');
      await expect(fixture).toHaveAttribute('data-handle-open', 'true');
    });
  });

  test.describe('impatient clicks with `openOnHover=true`', () => {
    test('does not close the menu if the user clicks too quickly', async ({ page, mode }) => {
      const fixture = await render(page, mode, { delay: 0, openOnHover: true, noPortal: true });
      await withClock(page, async () => {
        await moveTo(page, trigger(page));
        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');

        // Base UI ticks to PATIENT_CLICK_THRESHOLD - 1; the open's commit takes real time here, so the clock is held from
        // the moment the page shows it, and the press lands inside the window.
        await holdClock(page);
        await clickThrough(page, trigger(page));
        await page.clock.resume();
        await settle(fixture);

        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');
      });
    });

    test('closes the menu if the user clicks patiently', async ({ page, mode }) => {
      const fixture = await render(page, mode, { delay: 0, openOnHover: true, content: 'items', items: '' });
      await withClock(page, async () => {
        await moveTo(page, trigger(page));
        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');
        await page.clock.runFor(500);
        await page.clock.resume();

        await clickThrough(page, trigger(page));
        await expect(trigger(page)).not.toHaveAttribute('data-popup-open', /.*/);

        // Leaving the trigger afterwards doesn't reopen it.
        await parkPointer(page);
        await settle(fixture);

        await expect(trigger(page)).not.toHaveAttribute('data-popup-open', /.*/);
      });
    });

    test('sticks if the user clicks impatiently', async ({ page, mode }) => {
      const fixture = await render(page, mode, { delay: 0, openOnHover: true, noPortal: true });
      await withClock(page, async () => {
        await moveTo(page, trigger(page));
        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');

        await holdClock(page);
        await clickThrough(page, trigger(page));
        await parkPointer(page);
        await page.clock.resume();
        await settle(fixture);
        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');

        await waitMs(page, 1);
        await settle(fixture);

        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');
      });
    });

    test('sticks when clicked before the hover delay completes', async ({ page, mode }) => {
      const fixture = await render(page, mode, { openOnHover: true, delay: 300, content: 'items', items: 'Content' });
      await withClock(page, async () => {
        await moveTo(page, trigger(page));
        await page.clock.runFor(100);

        // User clicks impatiently to open.
        await clickThrough(page, trigger(page));
        await page.clock.resume();
        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');

        await parkPointer(page);
        await settle(fixture);

        await expect(trigger(page)).toHaveAttribute('data-popup-open', '');
      });
    });

    test('should keep the menu open when re-hovered and clicked within the patient threshold', async ({ page, mode }) => {
      const fixture = await render(page, mode, { openOnHover: true, delay: 100, content: 'items', items: 'Content' });
      await withClock(page, async () => {
        await moveTo(page, trigger(page));
        await page.clock.runFor(100);
        await page.clock.resume();
        await expect(page.getByTestId('item-Content')).toBeVisible();

        await page.clock.runFor(500);
        await page.clock.resume();

        // A real pointer closes on the way out, then opens again by the press inside the new hover's delay.
        await parkPointer(page);
        await moveTo(page, trigger(page));
        await clickThrough(page, trigger(page));
        await settle(fixture);

        await expect(page.getByTestId('item-Content')).toBeVisible();
      });
    });
  });

  // MNU-PTR-06 on Server: the windows between a hover open's request and its commit, from M's probe (G4), which the bare
  // titles above cannot reach. Under `delayCircuit` the press lands before C# committed the hover open, or the leave before
  // C# committed the press. Each keeps the menu open: the press's stick holds on both orders.
  test.describe('MNU-PTR-06 under circuit latency', () => {
    test('port: a press before the hover open commits keeps the menu open when the pointer leaves', async ({ page, mode }) => {
      test.skip(mode !== 'server', 'Server only: the window between the hover request and its commit');
      await parkPointer(page);
      await delayCircuit(page, 150);
      await withClock(page, async () => {
        const fixture = await gotoPopupFixture(page, mode, 'menu-root', { openOnHover: true, content: 'items', items: 'Item' });
        await moveTo(page, trigger(page));
        await page.clock.runFor(110);
        await page.mouse.down();
        await page.mouse.up();
        await page.clock.resume();
        await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
        await settle(fixture);
        await waitMs(page, 600);

        await parkPointer(page);
        await waitMs(page, 300);
        await settle(fixture);

        await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
        expect((await openChanges(fixture)).filter((change) => change.startsWith('false'))).toEqual([]);
      });
    });

    test('port: a leave between the hover commit and the press commit keeps the menu open', async ({ page, mode }) => {
      test.skip(mode !== 'server', 'Server only: the window between the hover request and its commit');
      await parkPointer(page);
      await delayCircuit(page, 150);
      await withClock(page, async () => {
        const fixture = await gotoPopupFixture(page, mode, 'menu-root', { openOnHover: true, content: 'items', items: 'Item' });
        await moveTo(page, trigger(page));
        await page.clock.runFor(110);
        await page.clock.resume();
        await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');

        await page.mouse.down();
        await page.mouse.up();
        await page.mouse.move(1, 1, { steps: 2 });
        await settle(fixture);
        await waitMs(page, 300);
        await settle(fixture);

        // The menu ends open; the leave can report a `false:trigger-hover` the press's stick then answers (G4b, the row's note).
        await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
      });
    });
  });

  // The attach order of the trigger's handles on one element (the brief's attach-order rule, PR 168's B1: two click listeners
  // in an unpinned order). A route holds one module; the fixture stays unattached until it arrives, and the press then
  // toggles exactly once.
  test.describe('attach order', () => {
    async function stall(page: Page, module: string): Promise<() => Promise<void>> {
      const held: Route[] = [];
      let released = false;
      const url = new RegExp(`/_content/Blazix\\.Primitives/js/${module}(\\.[a-z0-9]+)?\\.js$`);
      await page.route(url, (route) => (released ? route.continue() : held.push(route)));
      return async () => {
        released = true;
        for (const route of held.splice(0)) {
          await route.continue();
        }
      };
    }

    for (const module of ['interactions', 'button', 'composite']) {
      test(`port: with js/${module}.js stalled the trigger's handles wait, and a press toggles once when the module arrives`, async ({ page, mode }) => {
        const release = await stall(page, module);
        await parkPointer(page);
        const fixture = await gotoFixture(page, mode, 'menu-root');
        await expect(fixture).toHaveAttribute('data-attached', 'false');

        await release();
        await expect(fixture).toHaveAttribute('data-attached', 'true');

        await trigger(page).click();
        await expect(page.getByTestId('menu')).toBeVisible();
        await trigger(page).click({ force: true });
        await expect(page.getByTestId('menu')).toHaveCount(0);

        expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:trigger-press']);
      });
    }
  });

  test('keeps a hover-opened menu open when mouseup lands outside the trigger DOM but within its bounds', async ({ page, mode }) => {
    const fixture = await render(page, mode, { openOnHover: true, delay: 0, triggerStyle: 'width: 120px; height: 40px; display: block', content: 'items', items: '' });
    await moveTo(page, trigger(page));
    await expect(page.getByRole('menu')).toBeAttached();

    // As Base UI (MenuTrigger.test.tsx:422): fired on the body, since a real release over the trigger targets the trigger.
    const point = await center(trigger(page));
    await fire(page.locator('body'), 'mouseup', { clientX: point.x, clientY: point.y });
    await settle(fixture);

    await expect(page.getByRole('menu')).toBeAttached();
  });

  test.describe('preventBaseUIHandler', () => {
    test('prevents opening the menu with a mouse when `preventBaseUIHandler` is called in onMouseDown', async ({ page, mode }) => {
      const fixture = await render(page, mode, { triggerVeto: 'mousedown', content: 'items', items: '' });

      await trigger(page).click();
      await settle(fixture);

      await expect(page.getByRole('menu')).toHaveCount(0);
    });

    test('prevents opening the menu with keyboard when `preventBaseUIHandler` is called in onClick', async ({ page, mode }) => {
      const fixture = await render(page, mode, { triggerVeto: 'click', content: 'items', items: '' });
      await trigger(page).focus();

      await page.keyboard.press('Enter');
      await settle(fixture);

      await expect(page.getByRole('menu')).toHaveCount(0);
    });
  });
});

test.describe('<Menu.Backdrop />', () => {
  for (const controlled of [false, true]) {
    test(`does not start an entry phase when initially open (controlled=${controlled})`, async ({ page, mode }) => {
      await recordSelectors(page, ['[data-testid="backdrop"][data-starting-style]']);
      const fixture = await renderOpen(page, mode, {
        open: controlled ? true : undefined,
        defaultOpen: controlled ? undefined : true,
        noTrigger: true,
        backdrop: true,
        content: 'items',
        items: 'Item',
      });

      await expect(page.getByTestId('backdrop')).toBeAttached();
      await settle(fixture);

      expect(await selectorSeen(page, '[data-testid="backdrop"][data-starting-style]')).toBe(false);
    });
  }

  test('sets `pointer-events: none` style on backdrop if opened by hover', async ({ page, mode }) => {
    await render(page, mode, { delay: 0, openOnHover: true, backdrop: true, content: 'items', items: '' });

    await moveTo(page, trigger(page));

    await expect(page.getByTestId('backdrop')).toBeAttached();
    await expect.poll(() => page.getByTestId('backdrop').evaluate((element: HTMLElement) => element.style.pointerEvents)).toBe('none');
  });

  test('does not set `pointer-events: none` style on backdrop if opened by click', async ({ page, mode }) => {
    await render(page, mode, { delay: 0, backdrop: true, content: 'items', items: '' });

    await trigger(page).click();

    await expect(page.getByTestId('backdrop')).toBeAttached();
    expect(await page.getByTestId('backdrop').evaluate((element: HTMLElement) => element.style.pointerEvents)).not.toBe('none');
  });
});

test.describe('<Menu.SubmenuTrigger />', () => {
  for (const key of ['Enter', 'Space']) {
    test(`keeps a hover-opened submenu open when pressing ${key}`, async ({ page, mode }) => {
      const fixture = await render(page, mode, { content: 'more' });
      await trigger(page).click();
      const submenuTrigger = item(page, 'More');
      await expect(submenuTrigger).toBeVisible();

      await moveTo(page, submenuTrigger);
      await expect(submenu(page)).toBeVisible();
      await expect(submenuTrigger).toBeFocused();

      await page.keyboard.press(key);
      await settle(fixture);

      await expect(submenu(page)).toBeVisible();
      await expect(popup(page)).toBeVisible();
    });
  }

  touchTest('closes a touch-opened submenu on a second tap', async ({ touchPage, mode }) => {
    const fixture = await gotoPopupFixture(touchPage, mode, 'menu-root', { defaultOpen: true, noTrigger: true, content: 'more' });
    await expect(fixture).toHaveAttribute('data-popup-attached', 'true');
    const submenuTrigger = touchPage.getByRole('menuitem', { name: 'More', exact: true });
    const point = await center(submenuTrigger);

    await touchPage.touchscreen.tap(point.x, point.y);
    await expect(touchPage.getByTestId('submenu')).toBeVisible();

    await touchPage.touchscreen.tap(point.x, point.y);

    await expect(touchPage.getByTestId('submenu')).toHaveCount(0);
    await expect(touchPage.getByTestId('menu')).toBeVisible();
  });

  async function openSubmenuByKeyboard(page: Page): Promise<void> {
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(item(page, 'More')).toBeFocused();
    await page.keyboard.press('ArrowRight');
    await expect(item(page, 'Alpha')).toBeFocused();
  }

  test('keeps a submenu open on direct trigger focus but closes it on guard return', async ({ page, mode }) => {
    const fixture = await render(page, mode, { content: 'more', playNext: true });
    await openSubmenuByKeyboard(page);

    await item(page, 'More').focus();
    await settle(fixture);
    await expect(submenu(page)).toBeAttached();

    await item(page, 'Alpha').focus();

    const guard = submenu(page).locator('xpath=..').locator('[data-base-ui-focus-guard][data-type="inside"]').first();
    await expect(guard).toBeAttached();
    await guard.evaluate((element: HTMLElement) => element.focus());

    await expect(item(page, 'More')).toBeFocused();
    await expect(submenu(page)).toHaveCount(0);
    await expect(popup(page)).toBeAttached();

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Play Next')).toBeFocused();
  });

  test('closes a submenu when its focus guard is in a shadow-root portal', async ({ page, mode }) => {
    const fixture = await render(page, mode, { content: 'more', playNext: true, container: 'shadow' });
    await expect.poll(() => page.locator('[data-shadow-host]').evaluate((host) => host.shadowRoot !== null)).toBe(true);
    await settle(fixture);
    await openSubmenuByKeyboard(page);

    await item(page, 'More').focus();
    await settle(fixture);
    await expect(submenu(page)).toBeAttached();

    await item(page, 'Alpha').focus();
    const guard = page.locator('[data-shadow-host] [data-base-ui-focus-guard][data-type="inside"]').first();
    await expect(guard).toBeAttached();
    await guard.evaluate((element: HTMLElement) => element.focus());

    await expect(item(page, 'More')).toBeFocused();
    await expect(submenu(page)).toHaveCount(0);
    await expect(popup(page)).toBeAttached();

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Play Next')).toBeFocused();
  });

  // Base UI reads the store's trigger registry (MenuSubmenuTrigger.test.tsx:307-313); the registry is C#'s, so the title reads
  // what follows from it: the open submenu's popup names the renamed trigger.
  test('follows a submenu trigger id change', async ({ page, mode }) => {
    const fixture = await renderOpen(page, mode, { content: 'more', subTriggerId: 'first' });
    await expect(item(page, 'More')).toHaveAttribute('id', 'first');

    await pressInPage(fixture.locator('[data-rename-sub-trigger]'));
    await expect(item(page, 'More')).toHaveAttribute('id', 'second');

    await item(page, 'More').focus();
    await page.keyboard.press('ArrowRight');

    await expect(submenu(page)).toHaveAttribute('aria-labelledby', 'second');
  });

  for (const { direction, openKey } of [
    { direction: 'ltr', openKey: 'ArrowRight' },
    { direction: 'rtl', openKey: 'ArrowLeft' },
  ]) {
    test(`opens the submenu with ${openKey} and highlights a single item in ${direction} direction`, async ({ page, mode }) => {
      await renderOpen(page, mode, { dir: direction, content: 'numbered' });
      await item(page, '2').focus();

      await page.keyboard.press(openKey);

      await expect(item(page, '2.1')).toBeFocused();
      // Only the first submenu item is highlighted; no other submenu or parent menu item is.
      await expect(page.locator('[role="menuitem"][data-highlighted]')).toHaveCount(1);
      await expect(page.locator('[role="menuitem"][data-highlighted]')).toHaveText('2.1');
    });
  }

  test('sets tabIndex to 0 on the submenu trigger after opening the submenu with a keydown event', async ({ page, mode }) => {
    await renderOpen(page, mode, { content: 'numbered' });
    await item(page, '2').focus();

    await page.keyboard.press('ArrowRight');

    await expect(item(page, '2')).toHaveAttribute('tabindex', '0');
  });

  test('uses the label prop for text navigation', async ({ page, mode }) => {
    await renderOpen(page, mode, { content: 'label' });
    await item(page, 'Alpha').focus();

    await page.keyboard.press('r');

    await expect(page.getByTestId('submenu-trigger')).toBeFocused();
  });

  test.describe('prop: disabled', () => {
    test('does not open on hover when disabled', async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode, { content: 'disabled-submenu' });

      await moveTo(page, item(page, 'Open submenu'));
      await settle(fixture);

      await expect(page.getByTestId('submenu-popup')).toHaveCount(0);
    });
  });
});

test.describe('<Menu.SubmenuTrigger /> with a screen reader press', () => {
  // Chrome performs a screen reader activation as a synthetic mouse press: a zero-pressure 1x1 `pointerdown`, then
  // `mousedown` and a `detail: 0` click. No real input takes this path, so Base UI's sequence is dispatched.
  async function fireScreenReaderMouseDown(locator: Locator): Promise<void> {
    await fire(locator, 'pointerdown', { pointerType: 'mouse', width: 1, height: 1, pressure: 0, detail: 0, buttons: 0 });
    await fire(locator, 'mousedown', { detail: 0 });
  }

  async function fireScreenReaderPress(locator: Locator): Promise<void> {
    await fireScreenReaderMouseDown(locator);
    await fire(locator, 'click', { detail: 0 });
  }

  async function openRoot(page: Page): Promise<Locator> {
    await trigger(page).click();
    const submenuTrigger = page.getByTestId('submenu-trigger');
    await expect(submenuTrigger).toBeVisible();
    return submenuTrigger;
  }

  test('toggles the submenu on repeated screen reader activation with the default `openOnHover`', async ({ page, mode }) => {
    const fixture = await render(page, mode, { content: 'sr' });
    const submenuTrigger = await openRoot(page);

    await fireScreenReaderPress(submenuTrigger);
    // Let opening focus settle before activating again.
    await expect(submenu(page)).toBeAttached();
    await waitFrames(page);
    await settle(fixture);

    await fireScreenReaderPress(submenuTrigger);
    await expect(submenu(page)).toHaveCount(0);
    await expect(popup(page)).toBeAttached();
    await waitFrames(page);
    await settle(fixture);

    await fireScreenReaderPress(submenuTrigger);
    await expect(submenu(page)).toBeAttached();
    await waitFrames(page);
    await settle(fixture);
    await expect(popup(page)).toBeAttached();
  });

  test('does not close the submenu on the trailing click of a screen reader press with `openOnHover={false}`', async ({ page, mode }) => {
    const fixture = await render(page, mode, { content: 'sr', subOpenOnHover: false });
    const submenuTrigger = await openRoot(page);

    await fireScreenReaderMouseDown(submenuTrigger);
    await expect(submenu(page)).toBeAttached();

    // The trailing click must not toggle the freshly opened submenu closed.
    await fire(submenuTrigger, 'click', { detail: 0 });
    await settle(fixture);

    await expect(submenu(page)).toBeAttached();
  });

  test('opens on a screen reader press without waiting a frame', async ({ page, mode }) => {
    await render(page, mode, { content: 'sr' });
    const submenuTrigger = await openRoot(page);

    // The reader re-syncs focus to its cursor right after the press, so the open cannot wait for a frame. With the page's
    // frames held, the open arrives: inside the dispatch on WebAssembly, a round trip later on Server (ADR 01's row).
    await withClock(page, async () => {
      await holdClock(page);
      await fireScreenReaderMouseDown(submenuTrigger);
      await expect(submenuTrigger).toHaveAttribute('data-popup-open', '');
    });
  });

  test('reports the trigger, not a pressed descendant, as the opener', async ({ page, mode }) => {
    const fixture = await render(page, mode, { content: 'icon-submenu' });
    const submenuTrigger = await openRoot(page);

    await fireScreenReaderPress(page.getByTestId('icon'));

    await expect(submenu(page)).toBeAttached();
    expect((await recorded(fixture, 'data-submenu-open-changes')).filter((entry) => entry.startsWith('trigger:'))[0]).toBe(`trigger:${await submenuTrigger.getAttribute('id')}`);
  });

  test('ignores an ordinary mouse press with the default `openOnHover`', async ({ page, mode }) => {
    const fixture = await render(page, mode, { content: 'sr' });
    const submenuTrigger = await openRoot(page);

    // A real mouse press has a pressed button even when it reports no pressure: fired, since a real pointer would also hover.
    await fire(submenuTrigger, 'pointerdown', { pointerType: 'mouse', width: 1, height: 1, pressure: 0, detail: 0, buttons: 1 });
    await fire(submenuTrigger, 'mousedown');
    await fire(submenuTrigger, 'click', { detail: 1 });
    await settle(fixture);

    await expect(submenu(page)).toHaveCount(0);
  });
});

test.describe('<Menu.SubmenuTrigger /> with VoiceOver', () => {
  // Base UI mocks the platform module's flag; the port's flag reads `navigator.platform` when the module loads
  // (tests/browser/interactions.spec.ts, "platform flags"), so the init script sets it before any module runs.
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      Object.defineProperty(Navigator.prototype, 'platform', { get: () => 'MacIntel', configurable: true });
      Object.defineProperty(Navigator.prototype, 'maxTouchPoints', { get: () => 0, configurable: true });
    });
  });

  test('omits the expanded state when the submenu is opened with ArrowRight', async ({ page, mode }) => {
    await render(page, mode, { content: 'sr' });
    await trigger(page).focus();
    await page.keyboard.press('Enter');

    const submenuTrigger = item(page, 'More');
    await expect(submenuTrigger).toBeFocused();
    await expect(submenuTrigger).toHaveAttribute('aria-expanded', 'false');

    await page.keyboard.press('ArrowRight');

    await expect(submenu(page)).toBeAttached();
    // Focus moves into the submenu; this is the announcement VoiceOver must not talk over.
    await expect(item(page, 'Alpha')).toBeFocused();
    await expect(page.getByTestId('submenu-trigger')).not.toHaveAttribute('aria-expanded', /.*/);
    // The submenu is still discoverable through `aria-haspopup`.
    await expect(page.getByTestId('submenu-trigger')).toHaveAttribute('aria-haspopup', 'menu');
  });

  test('omits the expanded state when the submenu is opened with Enter', async ({ page, mode }) => {
    await render(page, mode, { content: 'sr' });
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(item(page, 'More')).toBeFocused();

    await page.keyboard.press('Enter');

    await expect(submenu(page)).toBeAttached();
    await expect(item(page, 'Alpha')).toBeFocused();
    await expect(page.getByTestId('submenu-trigger')).not.toHaveAttribute('aria-expanded', /.*/);
  });

  test('keeps the expanded state when the submenu is opened with a pointer', async ({ page, mode }) => {
    await render(page, mode, { content: 'sr' });
    await trigger(page).click();

    const submenuTrigger = item(page, 'More');
    await submenuTrigger.click();

    await expect(submenu(page)).toBeAttached();
    // Focus stays on the trigger, so there is no item announcement to talk over.
    await expect(submenuTrigger).toHaveAttribute('aria-expanded', 'true');
  });
});

test.describe('<Menu.Popup />', () => {
  test('enters the items at the list boundary when an arrow key leaves custom popup content', async ({ page, mode }) => {
    await render(page, mode, { content: 'custom-button' });
    await trigger(page).click();
    await expect(item(page, 'One')).toBeVisible();

    await item(page, 'One').focus();
    await page.keyboard.press('ArrowDown');
    // Mid-list, so continuing from the highlight would reach Three instead.
    await expect(item(page, 'Two')).toBeFocused();

    await page.getByRole('button', { name: 'Custom' }).focus();
    await page.keyboard.press('ArrowDown');

    await expect(item(page, 'One')).toBeFocused();
  });

  test('moves focus into the items when the boundary item is already highlighted', async ({ page, mode }) => {
    await render(page, mode, { content: 'custom-link' });
    await trigger(page).focus();
    await page.keyboard.press('Enter');

    await expect(item(page, 'One')).toHaveAttribute('data-highlighted', '');
    await page.getByRole('link', { name: 'Profile' }).focus();
    await expect(item(page, 'One')).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'One')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Two')).toBeFocused();
  });

  test('moves focus into the items when the last item is already highlighted', async ({ page, mode }) => {
    await render(page, mode, { content: 'custom-link' });
    await trigger(page).focus();
    await page.keyboard.press('ArrowUp');

    await expect(item(page, 'Three')).toHaveAttribute('data-highlighted', '');
    await page.getByRole('link', { name: 'Profile' }).focus();
    await expect(item(page, 'Three')).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('ArrowUp');
    await expect(item(page, 'Three')).toBeFocused();

    await page.keyboard.press('ArrowUp');
    await expect(item(page, 'Two')).toBeFocused();
  });

  test.describe('prop: finalFocus', () => {
    test('receives the interaction type of the item press that closed the menu', async ({ page, mode }) => {
      const fixture = await render(page, mode, { finalFocusLog: true, content: 'items', items: 'Close' });

      await trigger(page).click();
      await item(page, 'Close').click();
      await expect(trigger(page)).toBeFocused();
      await expect.poll(async () => (await recorded(fixture, 'data-final-focus-calls')).at(-1)).toBe('mouse');

      await page.keyboard.press('Enter');
      await expect(item(page, 'Close')).toBeFocused();
      await page.keyboard.press('Enter');
      await expect(trigger(page)).toBeFocused();
      await expect.poll(async () => (await recorded(fixture, 'data-final-focus-calls')).at(-1)).toBe('keyboard');
    });

    test('receives the mouse interaction type after a drag-release item selection', async ({ page, mode }) => {
      await withClock(page, async () => {
        const fixture = await render(page, mode, { finalFocusLog: true, content: 'items', items: 'Close' });
        const start = await center(trigger(page));
        await page.mouse.move(start.x, start.y);
        await page.mouse.down();
        await expect(item(page, 'Close')).toBeVisible();

        // Drag-release selection is only armed 200ms after the trigger press.
        await page.clock.runFor(200);
        await page.clock.resume();
        await moveTo(page, item(page, 'Close'));
        await page.mouse.up();

        await expect(item(page, 'Close')).toHaveCount(0);
        await expect.poll(async () => (await recorded(fixture, 'data-final-focus-calls')).at(-1)).toBe('mouse');
      });
    });
  });
});

test.describe('<Menu.List /> in a plain menu', () => {
  test('navigates the items with the keyboard', async ({ page, mode }) => {
    await render(page, mode, { content: 'plain-list' });
    await trigger(page).focus();
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('item-1')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('item-2')).toBeFocused();
  });

  test('focuses the list rather than the presentational popup on a pointer open', async ({ page, mode }) => {
    await render(page, mode, { content: 'plain-list' });

    await trigger(page).click();

    await expect(page.getByTestId('list')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('item-1')).toBeFocused();
  });

  test('enters the list from the end with ArrowUp after a pointer open without looping', async ({ page, mode }) => {
    await render(page, mode, { loopFocus: false, content: 'list-three' });

    await trigger(page).click();

    await expect(page.getByTestId('list')).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(item(page, 'Delete')).toBeFocused();
  });
});

test.describe('<Menu.Item />', () => {
  test('skips a natively disabled item during keyboard navigation', async ({ page, mode }) => {
    await renderOpen(page, mode, { content: 'items', items: '1|2|3', nativeDisabledItems: '2' });
    await page.getByTestId('item-1').focus();

    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('item-3')).toBeFocused();

    await page.keyboard.press('ArrowUp');
    await expect(page.getByTestId('item-1')).toBeFocused();
  });
});

test.describe('<Menu.CheckboxItem />', () => {
  function checkbox(page: Page): Locator {
    return page.getByRole('menuitemcheckbox');
  }

  test('toggles the checked state when Space is pressed', async ({ page, mode }) => {
    await render(page, mode, { content: 'checkbox', items: 'Item' });
    await trigger(page).focus();
    await page.keyboard.press('ArrowDown');
    await expect(checkbox(page)).toBeFocused();

    await page.keyboard.press('Space');
    await expect(checkbox(page)).toHaveAttribute('data-checked', '');

    await page.keyboard.press('Space');
    await expect(checkbox(page)).toHaveAttribute('data-unchecked', '');
  });

  test('toggles with Space after closing during typeahead and reopening', async ({ page, mode }) => {
    await render(page, mode, { content: 'checkbox', items: 'Settings' });
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(checkbox(page)).toBeFocused();

    await page.keyboard.press('s');
    await page.keyboard.press('Escape');
    await expect(trigger(page)).toBeFocused();

    await page.keyboard.press('Enter');
    await expect(checkbox(page)).toBeFocused();
    await expect(checkbox(page)).toHaveAttribute('aria-checked', 'false');

    await page.keyboard.press('Space');
    await expect(checkbox(page)).toHaveAttribute('aria-checked', 'true');
  });

  test('does not toggle when Space is pressed during an active typeahead session', async ({ page, mode }) => {
    const fixture = await renderOpen(page, mode, { content: 'checkbox', items: 'Item One|Item Two' });
    const itemOne = page.getByRole('menuitemcheckbox', { name: 'Item One' });
    const itemTwo = page.getByRole('menuitemcheckbox', { name: 'Item Two' });
    await itemOne.focus();

    await page.keyboard.type('Item T');
    await expect(itemTwo).toBeFocused();

    await page.keyboard.press('Space');
    await page.keyboard.press('Space');
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-checkbox-changes', '');
    await expect(itemTwo).toHaveAttribute('aria-checked', 'false');
  });

  test('toggles the checked state when Enter is pressed', async ({ page, mode }) => {
    await render(page, mode, { content: 'checkbox', items: 'Item' });
    await trigger(page).focus();
    await page.keyboard.press('ArrowDown');
    await expect(checkbox(page)).toBeFocused();

    await page.keyboard.press('Enter');

    await expect(checkbox(page)).toHaveAttribute('data-checked', '');
  });
});

test.describe('<Menu.RadioItem />', () => {
  for (const key of ['Space', 'Enter']) {
    test(`selects the item when ${key} is pressed`, async ({ page, mode }) => {
      // Deviation (2026-11-05): Base UI's tree has no popup around the group (MenuRadioItem.test.tsx:185); here the list needs
      // the popup that every menu has.
      await render(page, mode, { content: 'radio', items: 'Item' });
      await trigger(page).focus();
      await page.keyboard.press('ArrowDown');
      const radio = page.getByRole('menuitemradio');
      await expect(radio).toBeFocused();

      await page.keyboard.press(key);

      await expect(radio).toHaveAttribute('data-checked', '');
    });
  }

  test('does not select when Space is pressed during an active typeahead session', async ({ page, mode }) => {
    const fixture = await renderOpen(page, mode, { content: 'radio', items: 'Item One|Item Two' });
    const itemOne = page.getByRole('menuitemradio', { name: 'Item One' });
    const itemTwo = page.getByRole('menuitemradio', { name: 'Item Two' });
    await itemOne.focus();

    await page.keyboard.type('Item T');
    await expect(itemTwo).toBeFocused();

    await page.keyboard.press('Space');
    await page.keyboard.press('Space');
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-radio-changes', '');
    await expect(itemTwo).toHaveAttribute('aria-checked', 'false');
  });
});

test.describe('<Menu.LinkItem />', () => {
  // Base UI renders react-router's Link; the port's items are plain anchors whose fragment moves, read from the location
  // (MenuLinkItem.test.tsx:30, :118).
  test('react-router <Link> activates with Enter and Space', async ({ page, mode }) => {
    await renderOpen(page, mode, { content: 'link', items: 'link 1|link 2' });

    await item(page, 'link 2').focus();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/#link-2$/);

    await item(page, 'link 1').focus();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/#link-1$/);

    await item(page, 'link 2').focus();
    await page.keyboard.press('Space');
    await expect(page).toHaveURL(/#link-2$/);

    await item(page, 'link 1').focus();
    await page.keyboard.press('Space');
    await expect(page).toHaveURL(/#link-1$/);
  });

  test('does not navigate when Space is pressed during an active typeahead session', async ({ page, mode }) => {
    const fixture = await renderOpen(page, mode, { content: 'link', items: 'Item One|Item Two' });
    await item(page, 'Item One').focus();

    await page.keyboard.type('Item T');
    await expect(item(page, 'Item Two')).toBeFocused();
    expect(new URL(page.url()).hash).toBe('');

    await page.keyboard.press('Space');
    await settle(fixture);

    expect(new URL(page.url()).hash).toBe('');
  });
});

test.describe('<Menu.Positioner />', () => {
  test.describe('prop: keepMounted', () => {
    test('when keepMounted=true, should keep the content mounted when closed', async ({ page, mode }) => {
      await render(page, mode, { modal: false, keepMounted: true, content: 'items', items: '1|2' });

      await expect(popup(page)).toBeAttached();
      await expect(page.getByRole('menu')).toHaveCount(0);

      await trigger(page).click({ delay: 20 });
      await expect(page.getByRole('menu')).toBeVisible();

      await trigger(page).click({ delay: 20 });
      await expect(popup(page)).toBeAttached();
      await expect(page.getByRole('menu')).toHaveCount(0);
    });

    test('when keepMounted=false, should unmount the content when closed', async ({ page, mode }) => {
      await render(page, mode, { modal: false, content: 'items', items: '1|2' });

      await expect(popup(page)).toHaveCount(0);

      await trigger(page).click({ delay: 20 });
      await expect(page.getByRole('menu')).toBeVisible();

      await trigger(page).click({ delay: 20 });
      await expect(popup(page)).toHaveCount(0);
    });
  });
});

test.describe('<Menu.Root /> detached triggers', () => {
  test.describe('nested menus', () => {
    for (const { kind, detached } of [
      { kind: 'contained', detached: undefined },
      { kind: 'detached', detached: true },
    ]) {
      test.describe(`with ${kind} triggers`, () => {
        test('supports keyboard navigation regardless of which trigger opened the menu', async ({ page, mode }) => {
          await render(page, mode, { detached, twoTriggers: true, content: 'two-level' });

          await page.getByTestId('trigger-1').click();
          await expect(page.getByRole('menu')).toBeFocused();

          await page.keyboard.press('ArrowDown');
          await page.keyboard.press('ArrowDown');
          const submenuTrigger = page.getByTestId('submenu-trigger');
          await expect(submenuTrigger).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(page.getByTestId('submenu-item')).toBeFocused();

          await page.keyboard.press('ArrowLeft');
          await expect(submenu(page)).toHaveCount(0);
          await expect(submenuTrigger).toBeFocused();

          await page.keyboard.press('Escape');
          await expect(popup(page)).toHaveCount(0);

          await page.getByTestId('trigger-2').click();
          await expect(popup(page)).toBeAttached();
        });

        test('opens a submenu on click when hover is disabled', async ({ page, mode }) => {
          await render(page, mode, { detached, twoTriggers: true, content: 'two-level', subOpenOnHover: false });

          await page.getByTestId('trigger-1').click();
          await expect(popup(page)).toBeAttached();
          await expect(submenu(page)).toHaveCount(0);

          await page.getByTestId('submenu-trigger').click();
          await expect(page.getByTestId('submenu-item')).toHaveText('Nested');

          await page.getByTestId('submenu-item').click();
          await expect(popup(page)).toHaveCount(0);

          await page.getByTestId('trigger-2').click();
          await expect(popup(page)).toBeAttached();
          await expect(submenu(page)).toHaveCount(0);
        });

        test('closes every level when clicking outside the deepest submenu', async ({ page, mode }) => {
          await render(page, mode, { detached, twoTriggers: true, content: 'three-level' });

          await page.getByTestId('trigger-1').click();
          await expect(page.getByRole('menu')).toBeFocused();

          await page.keyboard.press('ArrowDown');
          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('submenu-trigger-1')).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(item(page, 'Item 2')).toBeFocused();

          await page.keyboard.press('ArrowDown');
          await expect(page.getByTestId('submenu-trigger-2')).toBeFocused();

          await page.keyboard.press('ArrowRight');
          await expect(item(page, 'Deep Item')).toBeFocused();

          // Deviation (2026-11-05): `user.click` in Base UI dispatches on the button; a real press lands on the modal menu's
          // internal backdrop above it (MenuRoot.detached-triggers.test.tsx).
          await clickThrough(page, page.getByTestId('outside'));

          await expect(popup(page)).toHaveCount(0);
          await expect(page.getByTestId('level-2')).toHaveCount(0);
          await expect(page.getByTestId('level-3')).toHaveCount(0);
        });

        test('selects nested items with click, drag, release', async ({ page, mode }) => {
          await withClock(page, async () => {
            const fixture = await render(page, mode, { detached, twoTriggers: true, content: 'two-level' });
            const trigger1 = page.getByTestId('trigger-1');
            const start = await center(trigger1);
            await page.mouse.move(start.x, start.y);
            await page.mouse.down();
            await expect(popup(page)).toBeVisible();
            await page.clock.runFor(200);
            await page.clock.resume();

            await moveTo(page, page.getByTestId('submenu-trigger'));
            await expect(submenu(page)).toBeVisible();
            await moveTo(page, page.getByTestId('submenu-item'));
            await page.mouse.up();

            await expect(popup(page)).toHaveCount(0);
            expect((await recorded(fixture, 'data-submenu-open-changes')).at(-1)).toBe('false:item-press');
          });
        });
      });
    }
  });
});

