import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { center, holdClock, moveTo, parkPointer, recordSelectors, recorded, selectorSeen, waitMs, withClock } from './menu.helpers';
import { gotoPopupFixture, openChangeCompletes, openChanges, pressControl, settle, type PopupFixtureOptions } from './suites/popupFixture';

// The L4 titles of MenuRoot.test.tsx's nested popups, MenuPopupEnterTransition.test.tsx, MenuPopupTransitionState.test.tsx,
// the indicators' exit titles, MenuRoot.triggerless.test.tsx, MenuRoot.detached-triggers.test.tsx's hover hand-offs and
// MenuPopup.test.tsx's toolbar keys (M5 track 6, issue 172; docs/components/menu.md), titles verbatim, on both render modes.

function item(page: Page, name: string): Locator {
  return page.getByRole('menuitem', { name, exact: true });
}

test.describe('<Menu.Root /> nested popups', () => {
  async function render(page: Page, mode: RenderMode, name: string): Promise<Locator> {
    await parkPointer(page);
    return gotoPopupFixture(page, mode, 'menu-dialog', { case: name });
  }

  test('keeps the menu and dialog open when pressing Shift+Tab inside a nested dialog', async ({ page, mode }) => {
    const fixture = await render(page, mode, 'dialog');
    await page.getByTestId('menu-trigger').click();
    await expect(page.getByTestId('menu-popup')).toBeVisible();
    // The fixture renders again here, and sees the substituted trigger the opening render mounted.
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-item-attached', 'true');

    await page.getByTestId('dialog-trigger').click();
    await expect(page.getByTestId('dialog-popup')).toBeVisible();

    const dialogButton = page.getByTestId('dialog-button');
    await dialogButton.focus();
    await expect(dialogButton).toBeFocused();

    // Shift+Tab inside the dialog should NOT close the menu or the dialog.
    await page.keyboard.press('Shift+Tab');
    await settle(fixture);

    await expect(page.getByTestId('menu-popup')).toBeAttached();
    await expect(page.getByTestId('dialog-popup')).toBeAttached();
  });

  test('keeps focus in a nested alert dialog popup when the pointer leaves the triggering menu item', async ({ page, mode }) => {
    const fixture = await render(page, mode, 'alert-dialog');
    await page.getByTestId('menu-trigger').click();
    await expect(page.getByTestId('alert-dialog-trigger')).toBeVisible();
    // The fixture renders again here, and sees the substituted trigger the opening render mounted.
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-item-attached', 'true');

    await page.getByTestId('alert-dialog-trigger').click();

    const alertDialogPopup = page.getByTestId('alert-dialog-popup');
    await expect(alertDialogPopup).toBeVisible();
    await expect.poll(() => alertDialogPopup.evaluate((element) => element.contains(document.activeElement))).toBe(true);

    await parkPointer(page);
    await settle(fixture);

    await expect.poll(() => alertDialogPopup.evaluate((element) => element.contains(document.activeElement))).toBe(true);
    expect(await page.getByTestId('menu-popup').evaluate((element) => element.contains(document.activeElement))).toBe(false);
  });

  test('keeps pending focus in a nested dialog when the pointer leaves the triggering menu item', async ({ page, mode }) => {
    const fixture = await render(page, mode, 'dialog-div');
    await page.getByTestId('menu-trigger').click();
    const dialogTrigger = page.getByTestId('dialog-trigger');
    await expect(dialogTrigger).toBeVisible();
    // The fixture renders again here, and sees the substituted trigger the opening render mounted.
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-item-attached', 'true');
    const point = await center(dialogTrigger);
    await page.mouse.move(point.x, point.y);

    // Base UI holds the frames between the click and the leave; the page's clock holds them here.
    await withClock(page, async () => {
      await holdClock(page);
      await page.mouse.click(point.x, point.y);
      await parkPointer(page);
      await page.clock.resume();

      await expect(page.getByTestId('dialog-close')).toBeFocused();
      expect(await page.getByTestId('menu-popup').evaluate((element) => element.contains(document.activeElement))).toBe(false);
    });
  });

  test.describe('opening a dialog from an item', () => {
    test('keeps the dialog open after a press-drag-release activation', async ({ page, mode }) => {
      await page.addInitScript(() => {
        const clicks: boolean[] = [];
        (window as unknown as { documentClicks: boolean[] }).documentClicks = clicks;
        document.addEventListener('click', (event) => clicks.push(event.isTrusted), true);
      });
      await withClock(page, async () => {
        const fixture = await render(page, mode, 'press-release');
        const start = await center(page.getByTestId('menu-trigger'));
        await page.mouse.move(start.x, start.y);
        await page.mouse.down();
        await expect(page.getByRole('menu')).toBeVisible();

        // Exceed the impatient-click threshold so releasing over the item activates it.
        await page.clock.runFor(200);
        await page.clock.resume();

        await moveTo(page, page.getByTestId('item'));
        await page.mouse.up();

        // The item activates on release: the menu closes and the dialog opens.
        await expect(page.getByRole('menu')).toHaveCount(0);
        await expect(page.getByTestId('dialog-popup')).toBeVisible();

        // The browser fires the gesture's native click on the common ancestor of the press and release targets after the
        // dialog is open. It must not be treated as an intentional outside press on the dialog.
        await expect.poll(() => page.evaluate(() => (window as unknown as { documentClicks: boolean[] }).documentClicks.includes(true))).toBe(true);
        await settle(fixture);

        await expect(page.getByTestId('dialog-popup')).toBeVisible();
        expect(await recorded(fixture, 'data-dialog-open-changes')).not.toContain('false:outside-press');
      });
    });
  });
});

test.describe('Menu enter transition', () => {
  const submenuStarting = '[data-testid="submenu-popup"][data-starting-style]';
  const menuStarting = '[data-testid="menu-popup"][data-starting-style]';

  async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
    await parkPointer(page);
    return gotoPopupFixture(page, mode, 'menu-transition', options);
  }

  test('plays the enter transition for a submenu that is open when its parent opens', async ({ page, mode }) => {
    await recordSelectors(page, [submenuStarting]);
    await render(page, mode, { animations: 1, subDefaultOpen: true });

    await page.getByTestId('trigger').click();

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect.poll(() => selectorSeen(page, submenuStarting)).toBe(true);
  });

  test('plays the enter transition for a controlled-open submenu when its parent opens', async ({ page, mode }) => {
    await recordSelectors(page, [submenuStarting]);
    await render(page, mode, { animations: 1, subOpen: true });

    await page.getByTestId('trigger').click();

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect.poll(() => selectorSeen(page, submenuStarting)).toBe(true);
  });

  test('does not play the enter transition for a menu that is open on the first render', async ({ page, mode }) => {
    await recordSelectors(page, [menuStarting]);
    await render(page, mode, { animations: 1, defaultOpen: true, noSubmenu: true });

    await expect(page.getByTestId('menu-popup')).toBeAttached();
    await waitMs(page, 100);

    expect(await selectorSeen(page, menuStarting)).toBe(false);
  });

  test('does not play the enter transition for a submenu inside a menu that is open on the first render', async ({ page, mode }) => {
    await recordSelectors(page, [menuStarting, submenuStarting]);
    await render(page, mode, { animations: 1, defaultOpen: true, subDefaultOpen: true });

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await waitMs(page, 100);

    expect(await selectorSeen(page, menuStarting)).toBe(false);
    expect(await selectorSeen(page, submenuStarting)).toBe(false);
  });

  test('does not play the enter transition for a submenu when the parent popup is kept mounted', async ({ page, mode }) => {
    await recordSelectors(page, [submenuStarting]);
    await render(page, mode, { animations: 1, keepMounted: true, subDefaultOpen: true });

    await page.getByTestId('trigger').click();

    await expect(page.getByTestId('menu-popup')).toBeVisible();
    await waitMs(page, 100);

    expect(await selectorSeen(page, submenuStarting)).toBe(false);
  });

  test('plays the enter transition for a submenu opened by the user', async ({ page, mode }) => {
    await recordSelectors(page, [submenuStarting]);
    const fixture = await render(page, mode, { animations: 1, defaultOpen: true });
    await settle(fixture);

    await item(page, 'Submenu').click();

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect.poll(() => selectorSeen(page, submenuStarting)).toBe(true);
  });

  test('plays the enter transition for a submenu whose parent opened instantly', async ({ page, mode }) => {
    await recordSelectors(page, [submenuStarting]);
    await render(page, mode, { animations: 1, subDefaultOpen: true });

    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect.poll(() => selectorSeen(page, submenuStarting)).toBe(true);
  });

  test('marks an initially open submenu as instant when its parent opened instantly', async ({ page, mode }) => {
    const instant = '[data-testid="submenu-popup"][data-instant="click"]';
    await recordSelectors(page, [instant]);
    await render(page, mode, { animations: 1, subDefaultOpen: true });

    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect(page.getByTestId('menu-popup')).toHaveAttribute('data-instant', 'click');
    // The submenu carries the inherited value during its enter frames only: it is cleared once the initial reveal settles,
    // so it cannot suppress later transitions.
    await expect.poll(() => selectorSeen(page, instant)).toBe(true);
    await expect(page.getByTestId('submenu-popup')).not.toHaveAttribute('data-instant', /.*/);
  });

  test('clears the inherited instant type after the initial open', async ({ page, mode }) => {
    const fixture = await render(page, mode, { subOpen: true });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect(page.getByTestId('submenu-popup')).not.toHaveAttribute('data-instant', /.*/);

    await pressControl(fixture.locator('[data-set-sub-open="false"]'));
    await expect(page.getByTestId('submenu-popup')).toHaveCount(0);

    await pressControl(fixture.locator('[data-set-sub-open="true"]'));
    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect(page.getByTestId('submenu-popup')).not.toHaveAttribute('data-instant', /.*/);
  });

  test('clears the inherited instant type when a controlled close interrupts the entry', async ({ page, mode }) => {
    const fixture = await render(page, mode, { animations: 1, lateStyle: true, subOpen: true });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('submenu-popup')).toBeAttached();

    // Close before the 300ms enter transition finishes, then reopen.
    await pressControl(fixture.locator('[data-set-sub-open="false"]'));
    await expect(page.getByTestId('submenu-popup')).toHaveCount(0);

    await pressControl(fixture.locator('[data-set-sub-open="true"]'));
    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect(page.getByTestId('submenu-popup')).not.toHaveAttribute('data-instant', /.*/);
  });

  test('clears the inherited instant type when the popup is not initially rendered', async ({ page, mode }) => {
    const fixture = await render(page, mode, { subDefaultOpen: true, hideSubPopup: true });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('menu-popup')).toHaveAttribute('data-instant', 'click');
    // Give the reveal time to settle while the submenu popup is absent.
    await waitMs(page, 100);

    await pressControl(fixture.locator('[data-show-sub-popup]'));
    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect(page.getByTestId('submenu-popup')).not.toHaveAttribute('data-instant', /.*/);
  });

  test('does not mark a closed submenu as instant when it is later opened programmatically', async ({ page, mode }) => {
    const fixture = await render(page, mode, { subOpen: false, subDefaultOpen: true });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('menu-popup')).toBeAttached();
    await expect(page.getByTestId('menu-popup')).toHaveAttribute('data-instant', 'click');

    await pressControl(fixture.locator('[data-set-sub-open="true"]'));

    await expect(page.getByTestId('submenu-popup')).toBeAttached();
    await expect(page.getByTestId('submenu-popup')).not.toHaveAttribute('data-instant', /.*/);
  });
});

test.describe('Menu popup transition state', () => {
  interface PopupState {
    open: boolean;
    transitionStatus?: string;
  }

  for (const controlled of [false, true]) {
    for (const keepMounted of [false, true]) {
      for (const defaultOpen of [false, true]) {
        test(`starts each opening once (controlled=${controlled}, keepMounted=${keepMounted}, defaultOpen=${defaultOpen})`, async ({ page, mode }) => {
          // The recorder observes from the page's first script on, and reads each record in a microtask queued from its
          // callback, as Popover's title does (tests/browser/popover.spec.ts).
          await page.addInitScript(() => {
            const states: { open: boolean; transitionStatus?: string }[] = [];
            (window as unknown as { popupStates: typeof states }).popupStates = states;
            const record = (popup: Element) => {
              // Before the portal's move the popup is `display: none` (no frame paints it): the title's states are the shown ones.
              if (!popup.checkVisibility()) {
                return;
              }
              const state: { open: boolean; transitionStatus?: string } = { open: popup.hasAttribute('data-open') };
              if (popup.hasAttribute('data-starting-style')) {
                state.transitionStatus = 'starting';
              } else if (popup.hasAttribute('data-ending-style')) {
                state.transitionStatus = 'ending';
              }
              const previous = states.at(-1);
              if (previous?.open !== state.open || previous?.transitionStatus !== state.transitionStatus) {
                states.push(state);
              }
            };
            new MutationObserver((records) => {
              for (const entry of records) {
                for (const node of [entry.target, ...entry.addedNodes]) {
                  const popup = node instanceof Element ? (node.matches('[data-testid="menu-popup"]') ? node : node.querySelector('[data-testid="menu-popup"]')) : null;
                  if (popup) {
                    queueMicrotask(() => record(popup));
                  }
                }
              }
            }).observe(document, { attributes: true, childList: true, subtree: true, attributeFilter: ['data-open', 'data-closed', 'data-starting-style', 'data-ending-style'] });
          });
          await parkPointer(page);
          const fixture = await gotoPopupFixture(page, mode, 'menu-transition', {
            animations: 1,
            popupClass: 'transition-state',
            noSubmenu: true,
            open: controlled ? defaultOpen : undefined,
            followOpen: controlled || undefined,
            defaultOpen: controlled ? undefined : defaultOpen || undefined,
            keepMounted: keepMounted || undefined,
          });
          const popup = page.getByTestId('menu-popup');
          const trigger = page.getByTestId('trigger');
          const states = () => page.evaluate(() => (window as unknown as { popupStates: PopupState[] }).popupStates);
          const clearStates = () => page.evaluate(() => {
            (window as unknown as { popupStates: unknown[] }).popupStates.length = 0;
          });
          const animations = () => popup.evaluate((element) => element.getAnimations().length);
          // The popup may have gone by the next poll, so this does not wait for it.
          const finishAnimations = () =>
            page.evaluate(() => document.querySelector('[data-testid="menu-popup"]')?.getAnimations().forEach((animation) => animation.finish()));
          // The transition outlasts every phase, so a phase is finished explicitly instead of waited out.
          const finishPhase = async (completesBefore: number, phase: string) => {
            await expect.poll(animations).toBeGreaterThan(0);
            expect(await openChangeCompletes(fixture)).toHaveLength(completesBefore);
            await expect
              .poll(async () => {
                await finishAnimations();
                return (await openChangeCompletes(fixture)).slice(completesBefore);
              })
              .toEqual([phase]);
          };

          const openMenu = async () => {
            await clearStates();
            const before = (await openChangeCompletes(fixture)).length;
            await trigger.click({ force: true });
            await expect(popup).toBeVisible();
            await finishPhase(before, 'true');
            expect(await animations()).toBe(0);
            expect((await states()).filter((state) => state.open)).toEqual([{ open: true, transitionStatus: 'starting' }, { open: true }]);
          };

          if (defaultOpen) {
            await expect.poll(() => openChangeCompletes(fixture)).toEqual(['true']);
            expect(await states()).toEqual([{ open: true }]);
          } else {
            await openMenu();
          }

          const closeAndReopen = async () => {
            const before = (await openChangeCompletes(fixture)).length;
            const handle = await popup.elementHandle();
            await page.keyboard.press('Escape');
            await finishPhase(before, 'false');
            expect(await handle!.evaluate((element) => element.isConnected)).toBe(keepMounted);
            await openMenu();
          };

          await closeAndReopen();
          if (defaultOpen && keepMounted) {
            // Initial-open state must not affect later opening cycles.
            await closeAndReopen();
          }

          // A reversed exit keeps the lifecycle mounted. It must not start a new entry phase or report completion for the
          // canceled close.
          const before = (await openChangeCompletes(fixture)).length;
          const handle = await popup.elementHandle();
          await page.keyboard.press('Escape');
          await expect.poll(animations).toBeGreaterThan(0);
          expect(await openChangeCompletes(fixture)).toHaveLength(before);
          await clearStates();
          await trigger.click({ force: true });
          // The reopen handler can run after the click resolves. Do not finish the exit first.
          await expect(popup).toHaveAttribute('data-open', '');
          await expect
            .poll(async () => {
              await finishAnimations();
              return (await openChangeCompletes(fixture)).slice(before);
            })
            .toEqual(['true']);
          expect(await handle!.evaluate((element) => element === document.querySelector('[data-testid="menu-popup"]'))).toBe(true);
          expect((await states()).some((state) => state.transitionStatus === 'starting')).toBe(false);
          expect(await animations()).toBe(0);
        });
      }
    }
  }
});

for (const { part, kind } of [
  { part: '<Menu.CheckboxItemIndicator />', kind: 'checkbox-indicator' },
  { part: '<Menu.RadioItemIndicator />', kind: 'radio-indicator' },
]) {
  test.describe(part, () => {
    test('should remove the indicator when there is no exit animation defined', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'menu-transition', { case: kind });
      await expect(page.getByTestId('indicator')).toBeAttached();

      await pressControl(fixture.locator('[data-uncheck]'));

      await expect(page.getByTestId('indicator')).toHaveCount(0);
    });

    test('should remove the indicator when the animation finishes', async ({ page, mode }) => {
      await page.addInitScript(() => {
        (window as unknown as { animationEnds: string[] }).animationEnds = [];
        document.addEventListener('animationend', (event) => (window as unknown as { animationEnds: string[] }).animationEnds.push(event.animationName), true);
      });
      const fixture = await gotoPopupFixture(page, mode, 'menu-transition', { case: kind, animations: 1, indicatorKeepMounted: true, indicatorClass: 'animation-test-indicator' });
      await expect(page.getByTestId('indicator')).not.toHaveAttribute('hidden', /.*/);

      await pressControl(fixture.locator('[data-uncheck]'));

      await expect.poll(() => page.evaluate(() => (window as unknown as { animationEnds: string[] }).animationEnds)).toContain('menu-transition-test-anim');
    });

    test('keeps the indicator mounted to play its exit animation when unchecked without keepMounted', async ({ page, mode }) => {
      const ending = '[data-testid="indicator"][data-ending-style]';
      await recordSelectors(page, [ending]);
      const fixture = await gotoPopupFixture(page, mode, 'menu-transition', { case: kind, animations: 1, indicatorClass: 'animation-test-indicator' });
      await expect(page.getByTestId('indicator')).toBeAttached();

      await pressControl(fixture.locator('[data-uncheck]'));

      await expect.poll(() => selectorSeen(page, ending)).toBe(true);
      await expect(page.getByTestId('indicator')).toHaveCount(0);
    });
  });
}

test.describe('<Menu.Root /> without a trigger', () => {
  async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<Locator> {
    await parkPointer(page);
    const fixture = await gotoPopupFixture(page, mode, 'menu-triggerless', options);
    return fixture;
  }

  async function focusItem(page: Page, name: string): Promise<Locator> {
    const menuItem = item(page, name);
    await expect(menuItem).toBeVisible();
    await menuItem.focus();
    return menuItem;
  }

  for (const direction of ['ltr', 'rtl']) {
    test.describe(direction, () => {
      for (const keepMounted of [false, true]) {
        test(`keeps nested Escape and focus local, keepMounted=${keepMounted}`, async ({ page, mode }) => {
          const fixture = await render(page, mode, { case: 'nested', dir: direction, keepMounted: keepMounted || undefined });
          await expect(fixture).toHaveAttribute('data-popup-attached', 'true');
          const more = await focusItem(page, 'More');
          const root = page.getByTestId('menu');
          const handle = await root.elementHandle();

          await page.keyboard.press(direction === 'rtl' ? 'ArrowLeft' : 'ArrowRight');
          await expect(item(page, 'Leaf')).toBeFocused();
          await settle(fixture);
          expect(await openChanges(fixture)).toEqual([]);
          await expect(root).toHaveAttribute('data-open', '');

          await page.keyboard.press('Escape');
          await expect(item(page, 'Leaf')).toHaveCount(0);
          await expect(more).toBeFocused();
          await settle(fixture);
          expect(await openChanges(fixture)).toEqual([]);

          await page.keyboard.press('Escape');
          await expect(page.getByRole('menu')).toHaveCount(0);
          expect(await openChanges(fixture)).toEqual(['false:escape-key']);
          expect(await handle!.evaluate((element) => element.isConnected)).toBe(keepMounted);
        });
      }
    });
  }

  for (const keepMounted of [false, true]) {
    test(`supports an external anchor across reopen, keepMounted=${keepMounted}`, async ({ page, mode }) => {
      const fixture = await render(page, mode, { case: 'anchor', keepMounted: keepMounted || undefined });
      const anchor = page.getByTestId('open-anchor');

      const openAndSelect = async (cycle: number) => {
        await anchor.click();
        // A pointer open focuses the popup once it attached; the first key needs its list.
        await expect(page.getByRole('menu')).toBeFocused();
        await focusItem(page, 'More');
        await page.keyboard.press('ArrowRight');
        const nested = item(page, 'Nested');
        await expect(nested).toBeFocused();
        await settle(fixture);
        expect((await openChanges(fixture)).slice(cycle)).toEqual([]);

        await nested.click();

        await expect(page.getByRole('menu')).toHaveCount(0);
        expect((await openChanges(fixture)).slice(cycle)).toEqual(['false:item-press']);
        await expect(anchor).toBeFocused();
      };

      // Base UI holds the frames so the submenu opens before the parent's deferred focus; a real run keeps their order.
      await openAndSelect(0);
      await openAndSelect(1);
    });
  }

  for (const method of ['click', 'hover'] as const) {
    test(`closes a sibling submenu without closing the root on ${method}`, async ({ page, mode }) => {
      const fixture = await render(page, mode, { case: 'siblings', method });
      await expect(fixture).toHaveAttribute('data-popup-attached', 'true');
      const act = async (locator: Locator) => {
        if (method === 'click') {
          await locator.click();
        } else {
          await moveTo(page, locator);
        }
      };

      await act(await focusItem(page, 'First'));
      await expect(item(page, 'First item')).toBeVisible();
      const before = (await recorded(fixture, 'data-first-open-changes')).length;

      await act(item(page, 'Second'));
      await expect(item(page, 'Second item')).toBeVisible();
      await expect(item(page, 'First item')).toHaveCount(0);
      await settle(fixture);

      const changes = (await recorded(fixture, 'data-first-open-changes')).slice(before);
      expect(changes).toHaveLength(1);
      expect(changes[0]).toMatch(method === 'hover' ? /^false:(trigger-hover|sibling-open)$/ : /^false:sibling-open$/);
      expect(await openChanges(fixture)).toEqual([]);
    });
  }

  test('keeps an initially open submenu attached to the root', async ({ page, mode }) => {
    const fixture = await render(page, mode, { case: 'initial' });

    await expect.poll(() => recorded(fixture, 'data-submenu-open-change-completes')).toContain('true');

    await expect(page.getByRole('menu')).toHaveCount(2);
    expect(await openChanges(fixture)).toEqual([]);
  });

  for (const initialTrigger of [false, true]) {
    // Base UI also compares the store's floating node ids before and after the trigger mounts; they are internal here, so the
    // title reads what follows from them: the tree stays one, the nested Escape stays local, and focus returns to the trigger.
    test(`preserves real trigger registration, initially present=${initialTrigger}`, async ({ page, mode }) => {
      const fixture = await render(page, mode, { case: 'registration', initialTrigger: initialTrigger || undefined });
      await expect(fixture).toHaveAttribute('data-popup-attached', 'true');
      await focusItem(page, 'More');

      await page.keyboard.press('F2');
      const trigger = page.getByTestId('trigger');
      await expect(trigger).toBeAttached();

      await page.keyboard.press('ArrowRight');
      await expect(item(page, 'Nested')).toBeFocused();
      await settle(fixture);
      expect(await openChanges(fixture)).toEqual([]);

      await page.keyboard.press('Escape');
      await expect(item(page, 'Nested')).toHaveCount(0);

      await page.keyboard.press('Escape');
      await expect(page.getByRole('menu')).toHaveCount(0);
      await expect(trigger).toBeFocused();
    });
  }
});

test.describe('<Menu.Root /> multiple detached triggers', () => {
  // The hover hand-off's records: each `data-instant` the popup carries while it runs its exit, and while a close is
  // requested but not committed, from a recorder in the page (Base UI records them in the popup's render function).
  async function recordInstants(page: Page): Promise<void> {
    await page.addInitScript(() => {
      const records = { ending: [] as (string | null)[], pending: [] as (string | null)[] };
      (window as unknown as { instants: typeof records }).instants = records;
      const sample = () => {
        const popup = document.querySelector('[data-testid="popup"]');
        if (!popup) {
          return;
        }
        if (popup.hasAttribute('data-ending-style')) {
          records.ending.push(popup.getAttribute('data-instant'));
        } else if (document.querySelector('[data-fixture="menu-transition"]')?.getAttribute('data-open-changes')?.includes('false:')) {
          records.pending.push(popup.getAttribute('data-instant'));
        }
      };
      new MutationObserver(() => queueMicrotask(sample)).observe(document, { attributes: true, subtree: true, attributeFilter: ['data-instant', 'data-ending-style', 'data-open-changes'] });
    });
  }

  const instants = (page: Page) => page.evaluate(() => (window as unknown as { instants: { ending: (string | null)[]; pending: (string | null)[] } }).instants);
  const clearInstants = (page: Page) =>
    page.evaluate(() => {
      const { ending, pending } = (window as unknown as { instants: { ending: unknown[]; pending: unknown[] } }).instants;
      ending.length = 0;
      pending.length = 0;
    });

  async function handOff(page: Page, mode: RenderMode, settleTriggerChange: boolean): Promise<void> {
    await parkPointer(page);
    await gotoPopupFixture(page, mode, 'menu-transition', { case: 'hover-triggers', animations: 1 });
    await moveTo(page, page.getByTestId('trigger-1'));
    await expect(page.getByTestId('content')).toHaveText('1');

    await moveTo(page, page.getByTestId('trigger-2'));
    await expect(page.getByTestId('content')).toHaveText('2');

    if (settleTriggerChange) {
      await expect(page.getByTestId('popup')).toHaveAttribute('data-instant', 'trigger-change');
    }

    // The handoff itself legitimately renders `trigger-change` while closing the previous trigger's popup. Only the close
    // that follows matters.
    await clearInstants(page);
  }

  test('does not apply the trigger-change instant to a hover close after switching triggers', async ({ page, mode }) => {
    await recordInstants(page);
    await handOff(page, mode, true);

    await parkPointer(page);
    await expect(page.getByTestId('popup')).toHaveAttribute('data-ending-style', '');

    expect((await instants(page)).ending).not.toContain('trigger-change');
  });

  test('does not restore the trigger-change instant when a controlled close commits late', async ({ page, mode }) => {
    await recordInstants(page);
    await parkPointer(page);
    const fixture = await gotoPopupFixture(page, mode, 'menu-transition', { case: 'late-commit', animations: 1 });

    await moveTo(page, page.getByTestId('trigger-1'));
    await expect(page.getByTestId('popup')).toBeAttached();

    // Let the first open settle so moving to trigger 2 is a real switch.
    await waitMs(page, 100);

    await moveTo(page, page.getByTestId('trigger-2'));
    await expect.poll(() => recorded(fixture, 'data-opened-triggers')).toEqual(['trigger-1', 'trigger-2']);
    // Request the close while the switch is still animating.
    await parkPointer(page);

    await expect.poll(async () => (await instants(page)).ending.length, { timeout: 3000 }).toBeGreaterThan(0);

    expect((await instants(page)).pending).toContain('trigger-change');
    expect((await instants(page)).ending).not.toContain('trigger-change');
  });

  test('does not restore the trigger-change instant after a hover close has started', async ({ page, mode }) => {
    await handOffRecording(page, mode);
    const popup = page.getByTestId('popup');
    const positioner = page.getByTestId('positioner');
    await expect.poll(() => positioner.evaluate((element) => element.getAnimations().length)).toBeGreaterThan(0);

    await parkPointer(page);
    await expect(popup).toHaveAttribute('data-ending-style', '');

    await positioner.evaluate((element) => Promise.all(element.getAnimations().map((animation) => animation.finished)));

    // Still mid-exit: the stale callback must not have marked it instant and collapsed the transition.
    await expect(popup).toHaveAttribute('data-ending-style', '');
    await expect(popup).not.toHaveAttribute('data-instant', /.*/);
  });

  async function handOffRecording(page: Page, mode: RenderMode): Promise<void> {
    await recordInstants(page);
    await handOff(page, mode, false);
  }
});

test.describe('<Menu.Popup /> inside a toolbar', () => {
  test('stops toolbar navigation keys without blocking ordinary key events', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'menu-toolbar');
    const popup = page.getByTestId('popup');
    await expect(popup).toBeAttached();
    await page.getByTestId('parent').evaluate((parent) => {
      (window as unknown as { parentKeys: string[] }).parentKeys = [];
      parent.addEventListener('keydown', (event) => (window as unknown as { parentKeys: string[] }).parentKeys.push((event as KeyboardEvent).key));
    });
    const parentKeys = () => page.evaluate(() => (window as unknown as { parentKeys: string[] }).parentKeys);

    await popup.evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'ArrowRight' })));
    expect(await parentKeys()).toEqual([]);

    await popup.evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: 'F1' })));
    expect(await parentKeys()).toEqual(['F1']);
  });
});

test.describe('<Menu.Positioner /> with a controlled parent', () => {
  test('closes an open submenu with a sibling reason when its controlled parent closes', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'menu-transition', { open: true, keepMounted: true, subDefaultOpen: true });
    await expect(page.getByTestId('submenu-popup')).toBeAttached();

    await pressControl(fixture.locator('[data-set-open="false"]'));

    await expect.poll(async () => (await recorded(fixture, 'data-submenu-open-changes')).at(-1)).toBe('false:sibling-open');
  });
});
