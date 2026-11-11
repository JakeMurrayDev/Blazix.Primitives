import { type CDPSession, type Locator } from '@playwright/test';
import { delayCircuit, expect, isScrollLocked, test, type Page, type RenderMode } from './helpers';
import { center, clickThrough, moveTo, parkPointer } from './menu.helpers';
import { gotoPopupFixture, settle, type PopupFixtureOptions } from './suites/popupFixture';

// The L4 titles of Menubar.test.tsx (M5 track 8, issue 195; docs/components/menubar.md), titles verbatim, on both render
// modes, Server under a one-way circuit delay. Real Playwright input: presses, hovers and keys. The first batch runs on
// the three setups of Base UI's `describe.for` (contained, detached and multiple contained triggers, `?setup=`).

const FIXTURE = 'menubar-root';
const SERVER_DELAY_MS = 40;

async function renderFixture(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
  if (mode === 'server') {
    await delayCircuit(page, SERVER_DELAY_MS);
  }
  await parkPointer(page);
  return gotoPopupFixture(page, mode, FIXTURE, options);
}

const SETUPS = [
  { name: 'contained triggers', setup: 'contained' },
  { name: 'detached triggers', setup: 'detached' },
  { name: 'multiple contained triggers', setup: 'multiple' },
] as const;

/** Opens the fixture outside the setups (the contained bar, or a `case`). */
const render = renderFixture;

// Base UI's `pressWithTouch` fires `pointerdown` (touch) and `mousedown`. Here a touch is a real tap, as in
// select-lifecycle.spec.ts: CDP touch emulation and `Input.dispatchTouchEvent` at the element's center once its popup has
// its position, which the browser turns into a touch `pointerdown`, the `touchend` and the compatibility mouse events.
async function touchSession(page: Page): Promise<CDPSession> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 1 });
  return cdp;
}

async function tap(cdp: CDPSession, target: Locator): Promise<void> {
  const { x, y } = await center(target);
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

/**
 * Waits until the focus is inside the open menu whose Positioner has `testId`. ADR 01's "A popup element the opening
 * render creates" row: the popup takes its initial focus once its handles attach (a round trip after the open on Server,
 * after the open's render on WebAssembly), and a key pressed before then lands on the trigger. Base UI's jsdom titles
 * focus in the open's commit.
 */
async function focusInside(page: Page, testId: string): Promise<void> {
  await expect.poll(() => page.evaluate((id) => !!document.activeElement?.closest(`[data-testid="${id}"]`), testId)).toBe(true);
}

test.describe('<Menubar />', () => {
  test('renders contained top-level portal ownership as an allowed menubar child', async ({ page, mode }) => {
    await render(page, mode);
    await page.getByTestId('file-trigger').click();
    const fileMenu = page.getByTestId('file-menu');
    await expect(fileMenu).toBeVisible();

    const portalId = await fileMenu.evaluate((element) => element.closest('[data-base-ui-portal]')?.id ?? '');
    const owner = page.getByRole('menubar').locator('span[aria-owns]');
    expect(portalId).not.toBe('');
    await expect(owner).toHaveAttribute('role', 'group');
    await expect(owner).not.toHaveAttribute('aria-hidden');
    await expect(owner).toHaveAttribute('aria-owns', portalId);
  });

  test('renders detached top-level portal ownership without an accessibility role', async ({ page, mode }) => {
    await render(page, mode, { setup: 'detached' });
    await page.getByTestId('file-trigger').click();
    const fileMenu = page.getByTestId('file-menu');
    await expect(fileMenu).toBeVisible();

    const portalId = await fileMenu.evaluate((element) => element.closest('[data-base-ui-portal]')?.id ?? '');
    const owner = page.locator('span[aria-owns]');
    expect(portalId).not.toBe('');
    await expect(owner).toHaveAttribute('aria-owns', portalId);
    // The portal renders outside the menubar, so the `group` workaround would only add a stray accessible group.
    await expect(page.getByRole('menubar').locator('span[aria-owns]')).toHaveCount(0);
    await expect(owner).not.toHaveAttribute('role');
  });

  test('keeps focus on an outside target when a triggerless menubar menu closes', async ({ page, mode }) => {
    const fixture = await render(page, mode, { case: 'triggerless' });
    const outside = page.getByTestId('outside');
    // The menu is open from the first render; a settled round trip has its popup's handles attached (on Server they attach
    // a round trip after the render), so the press below meets an outside-press listener.
    await expect(page.getByTestId('triggerless-menu')).toHaveAttribute('data-open', '');
    await settle(fixture);

    // Base UI's `user.click` dispatches the press to the button itself. A real click there lands on the modal bar's
    // backdrop (MBR-DOM-08), so the port dispatches the same sequence user-event does: the pointer and mouse presses,
    // the focus, the releases and the click.
    await outside.evaluate((button: HTMLElement) => {
      const init = { bubbles: true, cancelable: true, composed: true, button: 0, pointerType: 'mouse', isPrimary: true };
      button.dispatchEvent(new PointerEvent('pointerdown', init));
      button.dispatchEvent(new MouseEvent('mousedown', init));
      button.focus();
      button.dispatchEvent(new PointerEvent('pointerup', init));
      button.dispatchEvent(new MouseEvent('mouseup', init));
      button.dispatchEvent(new MouseEvent('click', { ...init, detail: 1 }));
    });

    await expect(page.getByTestId('triggerless-menu')).not.toHaveAttribute('data-open');
    await expect(outside).toBeFocused();
  });

  // MenuPositioner.test.tsx's "Menubar parent" titles (MBR-POS-02; MNU-POS-02's bar half). Base UI reads the side its
  // `sideOffset` callback receives; the port reads the side the Positioner resolved, its `data-side`.
  test.describe('MenuPositioner: Menubar parent', () => {
    test('uses bottom as the default side when the menubar is horizontal', async ({ page, mode }) => {
      await render(page, mode, { case: 'open-side' });
      await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'bottom');
    });

    test('uses inline-end as the default side when the menubar is vertical', async ({ page, mode }) => {
      await render(page, mode, { case: 'open-side', orientation: 'vertical' });
      await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'inline-end');
    });
  });

  // Deliverable 6 of M5 track 8's brief (MBR-PTR-01, PTR-02; PSH-PTR-04, ALG-20): the mixed toggle with consumer handlers.
  test.describe('port: the mixed toggle with consumer handlers', () => {
    test('port: the opening press runs onmousedown once, the swallowed click runs onclick once and keeps the menu open, and the next click closes it', async ({ page, mode }) => {
      const fixture = await render(page, mode, { case: 'consumer' });
      const trigger = page.getByTestId('file-trigger');

      await trigger.click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await expect(fixture).toHaveAttribute('data-handler-calls', 'mousedown click');
      await settle(fixture);
      await expect(page.getByTestId('file-menu')).toBeVisible();

      await trigger.click();
      await expect(page.getByTestId('file-menu')).toHaveCount(0);
      expect(((await fixture.getAttribute('data-file-changes')) ?? '').split(' ')).toEqual(['true:trigger-press', 'false:trigger-press']);
    });

    test('port: a consumer onmousedown that calls PreventPartHandler() stops the open', async ({ page, mode }) => {
      const fixture = await render(page, mode, { case: 'consumer', veto: true });

      await page.getByTestId('file-trigger').click();
      await expect(fixture).toHaveAttribute('data-handler-calls', /mousedown/);
      await settle(fixture);

      await expect(page.getByTestId('file-menu')).toHaveCount(0);
      await expect(fixture).toHaveAttribute('data-file-changes', '');
    });
  });

  test.describe('disabled state', () => {
    test('keeps the menubar reachable when the first trigger is disabled', async ({ page, mode }) => {
      await render(page, mode, { case: 'first-disabled' });
      const fileTrigger = page.getByTestId('file-trigger');
      const editTrigger = page.getByTestId('edit-trigger');
      await expect(fileTrigger).toHaveAttribute('disabled', '');
      await expect(fileTrigger).toHaveAttribute('tabindex', '-1');
      await expect(editTrigger).toHaveAttribute('tabindex', '0');

      // Base UI tabs from the document; the fixture's controls precede the bar, so the Tab starts on "Before".
      await page.getByTestId('before').focus();
      await page.keyboard.press('Tab');
      await expect(editTrigger).toBeFocused();
    });

    test('disables menu items while the menubar is disabled', async ({ page, mode }) => {
      const fixture = await render(page, mode, { case: 'disabled-open' });
      const item = page.getByTestId('file-item');
      await expect(item).toHaveAttribute('aria-disabled', 'true');

      const box = (await item.boundingBox())!;
      await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-item-clicks', '');
    });
  });

  // Deliverable 7's port titles for the rows Base UI's tests do not cover (MBR §13 `none`), on the contained bar.
  test.describe('port: rows with no Base UI title', () => {
    test('port: every popup of the bar, submenus included, names the bar as its root owner', async ({ page, mode }) => {
      // MBR-STA-06, MBR-DOM-09.
      await render(page, mode);
      const barId = await page.getByRole('menubar').getAttribute('id');
      expect(barId).toBeTruthy();
      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await moveTo(page, page.getByTestId('share-trigger'));
      await expect(page.getByTestId('share-menu')).toBeVisible();

      await expect(page.getByTestId('file-menu').getByRole('menu').first()).toHaveAttribute('data-rootownerid', barId!);
      await expect(page.getByTestId('share-menu').getByRole('menu')).toHaveAttribute('data-rootownerid', barId!);
    });

    test('port: a bar trigger renders no focus guards, closed or open', async ({ page, mode }) => {
      // MBR-DOM-07. A trigger's guards carry no `data-type`; the open menu's portal keeps its `outside` guards around its
      // owner in the bar, as Base UI's FloatingPortal does.
      await render(page, mode);
      const triggerGuards = page.getByRole('menubar').locator('[data-base-ui-focus-guard]:not([data-type])');
      await expect(triggerGuards).toHaveCount(0);

      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await expect(page.getByRole('menubar').locator('[data-base-ui-focus-guard][data-type="outside"]')).toHaveCount(2);
      await expect(triggerGuards).toHaveCount(0);
    });

    test('port: the bar ignores its arrow keys while a modifier key is held', async ({ page, mode }) => {
      // MBR-KEY-03: no allowed modifier keys (CMP-KEY-02).
      const fixture = await render(page, mode);
      const fileTrigger = page.getByTestId('file-trigger');
      await fileTrigger.focus();

      for (const modifier of ['Shift', 'Control', 'Alt', 'Meta']) {
        await page.keyboard.press(`${modifier}+ArrowRight`);
        await page.keyboard.press(`${modifier}+End`);
      }
      await settle(fixture);
      await expect(fileTrigger).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(page.getByTestId('edit-trigger')).toBeFocused();
    });

    test('port: Shift+Tab inside an open bar menu closes it with focus-out', async ({ page, mode }) => {
      // MBR-KEY-12.
      const fixture = await render(page, mode);
      await page.getByTestId('file-trigger').focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('file-item-1')).toBeFocused();
      await expect(page.getByRole('menubar')).toHaveAttribute('data-has-submenu-open', '');

      await page.keyboard.press('Shift+Tab');
      // The close and its reason. What follows differs by mode, an open question (design point 6 of M5 track 8): the focus
      // the close returns to File's trigger reaches it while the flag is still set on Server, and the trigger's focus open
      // reopens the menu (`true:trigger-focus`); on WebAssembly the flag clears inside the keydown and the menu stays closed.
      await expect(fixture).toHaveAttribute('data-file-changes', /^true:trigger-press false:focus-out/);
    });

    test('port: the bar has no typeahead, and an open bar menu runs its own', async ({ page, mode }) => {
      // MBR-KEY-15.
      const fixture = await render(page, mode);
      const fileTrigger = page.getByTestId('file-trigger');
      await fileTrigger.focus();

      await page.keyboard.press('v');
      await settle(fixture);
      await expect(fileTrigger).toBeFocused();
      await expect(page.getByRole('menu')).toHaveCount(0);

      await page.keyboard.press('Enter');
      await expect(page.getByTestId('file-item-1')).toBeFocused();
      await page.keyboard.press('s');
      await expect(page.getByTestId('file-item-2')).toBeFocused();
    });

    test('port: while a bar menu is open the pointer moving over another trigger makes it the tab stop, and while none is open it does not', async ({ page, mode }) => {
      // MBR-PTR-06 (CMP-PTR-01 under `HighlightItemOnHover = HasSubmenuOpen`): the highlight focuses the trigger, which
      // makes it the bar's tab stop. The hovered trigger's own menu then opens and takes the focus, so the title reads the
      // roving tabindex the highlight leaves. Red on Server (2026-11-11): the hovered View trigger never becomes the tab
      // stop (tabindex stays -1, 2 of 2). Open by ID (MBR-PTR-06's Server row, brief's Server rows).
      test.fixme(mode === 'server', 'the hover highlight while a bar menu is open never reaches the roving tabindex on Server');
      const fixture = await render(page, mode);
      await moveTo(page, page.getByTestId('edit-trigger'));
      await settle(fixture);
      await expect(page.getByTestId('edit-trigger')).toHaveAttribute('tabindex', '-1');
      await expect(page.getByTestId('file-trigger')).toHaveAttribute('tabindex', '0');

      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await moveTo(page, page.getByTestId('view-trigger'));
      await expect(page.getByTestId('view-trigger')).toHaveAttribute('tabindex', '0');
      await expect(page.getByTestId('file-trigger')).toHaveAttribute('tabindex', '-1');
    });

    test('port: while a modal bar menu is open the bar is cut out of the backdrop, and a press elsewhere is an outside press', async ({ page, mode }) => {
      // MBR-PTR-09, MBR-DOM-08.
      const fixture = await render(page, mode);
      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();

      const hitAt = async (target: Locator) => {
        const { x, y } = await center(target);
        return page.evaluate((point) => {
          const hit = document.elementFromPoint(point.x, point.y);
          return hit?.getAttribute('data-testid') ?? hit?.getAttribute('role') ?? hit?.tagName ?? '';
        }, { x, y });
      };
      await expect.poll(() => hitAt(page.getByTestId('edit-trigger'))).toBe('edit-trigger');
      await expect.poll(() => hitAt(page.getByTestId('before'))).toBe('presentation');

      await clickThrough(page, page.getByTestId('before'));
      await expect(page.getByTestId('file-menu')).toHaveCount(0);
      await expect(fixture).toHaveAttribute('data-file-changes', /(^| )false:outside-press$/);
    });

    test('port: a bar menu of a modal bar marks nothing outside it', async ({ page, mode }) => {
      // MBR-FOC-06: the focus manager is non-modal (no inert or aria-hidden marking, no trap).
      await render(page, mode);
      await page.getByTestId('file-trigger').focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('file-item-1')).toBeFocused();

      // The non-modal focus manager still writes its plain marker, `data-base-ui-inert`, as Base UI's markOthers does.
      const marked = await page.getByTestId('before').evaluate((element) => !!element.closest('[aria-hidden="true"], [inert]'));
      expect(marked).toBe(false);
    });

    test('port: in a horizontal right-to-left bar ArrowLeft moves forward and ArrowRight backward, and ArrowDown still opens', async ({ page, mode }) => {
      // MBR-I18N-01.
      await render(page, mode, { dir: 'rtl' });
      await page.getByTestId('file-trigger').focus();

      await page.keyboard.press('ArrowLeft');
      await expect(page.getByTestId('edit-trigger')).toBeFocused();
      await page.keyboard.press('ArrowRight');
      await expect(page.getByTestId('file-trigger')).toBeFocused();

      await page.keyboard.press('ArrowDown');
      await expect(page.getByTestId('file-menu')).toBeVisible();
    });

    test('port: in a bar menu of a horizontal right-to-left bar ArrowLeft on a plain item moves to the next menu and ArrowRight to the previous', async ({ page, mode }) => {
      // MBR-I18N-03.
      await render(page, mode, { dir: 'rtl' });
      await page.getByTestId('edit-trigger').focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('edit-item-1')).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(page.getByTestId('view-menu')).toBeVisible();
      await expect(page.getByTestId('edit-menu')).toHaveCount(0);
      await focusInside(page, 'view-menu');

      await page.keyboard.press('ArrowRight');
      await expect(page.getByTestId('view-menu')).toHaveCount(0);
      await expect(page.getByTestId('edit-menu')).toBeVisible();
    });

    test('port: a canceled sibling-open close leaves two bar menus open', async ({ page, mode }) => {
      // MBR-EVT-01.
      await render(page, mode, { cancelSibling: true });
      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();

      await moveTo(page, page.getByTestId('edit-trigger'));
      await expect(page.getByTestId('edit-menu')).toBeVisible();
      await expect(page.getByTestId('file-menu')).toBeVisible();
    });

    test('port: a switch between bar menus is instant as a group, and a submenu in a bar menu never is', async ({ page, mode }) => {
      // MBR-ANI-01.
      await render(page, mode);
      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await moveTo(page, page.getByTestId('share-trigger'));
      await expect(page.getByTestId('share-menu')).toBeVisible();
      await expect(page.getByTestId('share-menu')).not.toHaveAttribute('data-instant', 'group');

      await moveTo(page, page.getByTestId('edit-trigger'));
      await expect(page.getByTestId('edit-menu')).toBeVisible();
      await expect(page.getByTestId('edit-menu')).toHaveAttribute('data-instant', 'group');
      await expect(page.getByTestId('edit-menu').getByRole('menu')).toHaveAttribute('data-instant', 'group');
    });

    test('port: after a list-navigation close that opens no other menu the flag stays, so a hover still opens a menu', async ({ page, mode }) => {
      // MBR-EDGE-03 (with §16 Q4 replicated: the closed menu's trigger keeps the focus and the flag, and File reopens with
      // `trigger-focus`). Red on Server (2026-11-11): after that reopen, hovering Edit opens nothing (Edit records no
      // change). Open by ID with the probe in the hand-over note.
      test.fixme(mode === 'server', 'after the §16 Q4 reopen a hover on another trigger opens nothing on Server');
      const fixture = await render(page, mode, { loopFocus: false });
      const bar = page.getByRole('menubar');
      await page.getByTestId('file-trigger').focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('file-item-1')).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(fixture).toHaveAttribute('data-file-changes', /false:list-navigation/);
      await expect(bar).toHaveAttribute('data-has-submenu-open', '');

      await moveTo(page, page.getByTestId('edit-trigger'));
      await expect(page.getByTestId('edit-menu')).toBeVisible();
    });
  });

  for (const { name, setup } of SETUPS) {
  test.describe(`when using ${name}`, () => {
    const render = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) => renderFixture(page, mode, { setup, ...options });

    test.describe('click interactions', () => {
      test('should open the menu after clicking on its trigger and close it when clicking again', async ({ page, mode }) => {
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

      test('should open submenus on hover when another submenu is already open', async ({ page, mode }) => {
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

    test.describe('hover behavior, nested', () => {
      test('should open nested submenus on hover when parent menu is open', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').click();
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await expect(page.getByRole('menubar')).toHaveAttribute('data-has-submenu-open', '');

        await moveTo(page, page.getByTestId('share-trigger'));
        await expect(page.getByTestId('share-menu')).toBeVisible();
      });

      test('should open another menu on hover when a nested submenu is open', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').click();
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await expect(page.getByRole('menubar')).toHaveAttribute('data-has-submenu-open', '');
        await moveTo(page, page.getByTestId('share-trigger'));
        await expect(page.getByTestId('share-menu')).toBeVisible();
        await moveTo(page, page.getByTestId('share-item-1'));

        await moveTo(page, page.getByTestId('edit-trigger'));
        await expect(page.getByTestId('edit-menu')).toBeVisible();
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
        await expect(page.getByTestId('share-menu')).toHaveCount(0);
      });
    });

    test.describe('closeOnClick on nested items behavior', () => {
      test('should respect closeOnClick on nested items when the menu was opened on click', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('view-trigger').click({ delay: 30 });
        await expect(page.getByTestId('view-menu')).toBeVisible();
        await moveTo(page, page.getByTestId('layout-trigger'));
        await expect(page.getByTestId('layout-menu')).toBeVisible();

        await page.getByTestId('layout-item-2').click({ delay: 30 });
        await expect(page.getByTestId('layout-menu')).toBeVisible();
      });

      test('should respect closeOnClick on nested items when the menu was opened on hover', async ({ page, mode }) => {
        // Red on the shared-Root setups, both modes (2026-11-11): the Layout submenu is not visible after the hover switch
        // from File to View. Open by ID (MBR-PTR-07's shared-Root runs), cause not yet traced.
        // On the shared-Root setups the one positioner moves from File's anchor to View's, so the hovers wait for a
        // stable element as Base UI's `user.hover` acts on it where it is (Playwright's actionability). Red on Server on
        // those setups (2026-11-11): the Layout submenu does not open; suspected, unverified, the submenu trigger the
        // payload switch mounts is hovered before its hover handle attaches a round trip later. Open by ID (MBR-PTR-07).
        test.fixme(setup !== 'contained' && mode === 'server', 'shared-Root setups on Server: the hover on a freshly mounted submenu trigger (open)');
        await render(page, mode);
        await page.getByTestId('file-trigger').click({ delay: 30 });
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await page.getByTestId('view-trigger').hover();
        await expect(page.getByTestId('view-menu')).toBeVisible();
        await page.getByTestId('layout-trigger').hover();
        await expect(page.getByTestId('layout-menu')).toBeVisible();

        await page.getByTestId('layout-item-2').click({ delay: 30 });
        await expect(page.getByTestId('layout-menu')).toBeVisible();
      });
    });

    test('correctly opens new menu on hover after clicking on its trigger and entering from hover (#2222)', async ({ page, mode }) => {
      await render(page, mode);
      const fileTrigger = page.getByTestId('file-trigger');
      const editTrigger = page.getByTestId('edit-trigger');
      const bar = page.getByRole('menubar');
      await fileTrigger.click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await expect(bar).toHaveAttribute('data-has-submenu-open', '');
      await moveTo(page, editTrigger);
      await expect(page.getByTestId('edit-menu')).toBeVisible();

      await editTrigger.click();
      await expect(page.getByTestId('edit-menu')).toHaveCount(0);
      await expect(bar).not.toHaveAttribute('data-has-submenu-open');

      await fileTrigger.click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await expect(bar).toHaveAttribute('data-has-submenu-open', '');
      await moveTo(page, editTrigger);
      await expect(page.getByTestId('edit-menu')).toBeVisible();
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

      test('closes open submenus when navigating to the next menubar item with ArrowRight', async ({ page, mode }) => {
        // Base UI renders its own contained bar here (`SpyMenubar`), in every setup. Its File menu has one item before
        // Share, the fixture's two, so the focus takes two ArrowDown presses to reach Share.
        const fixture = await renderFixture(page, mode);
        await page.getByTestId('file-trigger').focus();
        await page.keyboard.press('Enter');
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await expect(page.getByTestId('file-item-1')).toBeFocused();

        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('file-item-2')).toBeFocused();
        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('share-trigger')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(page.getByTestId('share-menu')).toBeVisible();
        await expect(page.getByTestId('share-item-1')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(page.getByTestId('edit-menu')).toBeVisible();
        await expect(page.getByTestId('share-menu')).toHaveCount(0);
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
        await expect(fixture).toHaveAttribute('data-file-changes', /(^| )false:[a-z-]+$/);
        await expect(fixture).toHaveAttribute('data-edit-changes', /(^| )true:[a-z-]+$/);
      });

      test('should navigate between menus using left/right arrow keys when menus are open', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').focus();
        await page.keyboard.press('Enter');
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await focusInside(page, 'file-menu');

        await page.keyboard.press('ArrowRight');
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
        await expect(page.getByTestId('edit-menu')).toBeVisible();
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

    test.describe('mixed mouse and keyboard interactions', () => {
      test('should allow keyboard navigation after opening a menu with mouse click', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').click();
        await expect(page.getByTestId('file-menu')).toBeVisible();

        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('file-item-1')).toBeFocused();

        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('file-item-2')).toBeFocused();
      });

      test('should allow clicking a menu trigger then navigating to another menu with keyboard', async ({ page, mode }) => {
        await render(page, mode);
        await page.getByTestId('file-trigger').click();
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await focusInside(page, 'file-menu');

        await page.keyboard.press('ArrowRight');
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
        await expect(page.getByTestId('edit-menu')).toBeVisible();
      });
    });

    test.describe('touch interactions', () => {
      test('closes the entire tree on a single outside press after opening a submenu', async ({ page, mode }) => {
        await render(page, mode);
        const cdp = await touchSession(page);

        await tap(cdp, page.getByTestId('file-trigger'));
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await tap(cdp, page.getByTestId('share-trigger'));
        await expect(page.getByTestId('share-menu')).toBeVisible();

        await tap(cdp, page.getByTestId('before'));
        // The submenu lives inside the file menu, so it is gone once the file menu closes.
        await expect(page.getByTestId('file-menu')).toHaveCount(0);
        await expect(page.getByTestId('share-menu')).toHaveCount(0);
      });
    });

    test.describe('scroll locking', () => {
      // Base UI renders its own modal bar here, in every setup.
      test('applies scroll lock when a touch-opened submenu covers the viewport width', async ({ page, mode }) => {
        await renderFixture(page, mode, { case: 'scroll-lock' });
        const cdp = await touchSession(page);

        await tap(cdp, page.getByTestId('file-trigger'));
        await expect(page.getByRole('menu')).toBeVisible();

        await expect.poll(() => isScrollLocked(page)).toBe(true);
      });

      test('does not apply scroll lock when a touch-opened submenu is narrower than the viewport', async ({ page, mode }) => {
        const fixture = await renderFixture(page, mode, { case: 'scroll-lock', fileWidth: 'narrow' });
        const cdp = await touchSession(page);

        await tap(cdp, page.getByTestId('file-trigger'));
        await expect(page.getByRole('menu')).toBeVisible();
        // The positioned popup and a settled round trip: the lock's decision has run (its control is the title above).
        await center(page.getByRole('menu'));
        await settle(fixture);

        expect(await isScrollLocked(page)).toBe(false);
      });

      test('updates scroll lock when handing off between top-level touch-opened menus', async ({ page, mode }) => {
        await renderFixture(page, mode, { case: 'scroll-lock' });
        const cdp = await touchSession(page);

        await tap(cdp, page.getByTestId('file-trigger'));
        await expect(page.getByTestId('file-menu')).toBeVisible();
        await expect.poll(() => isScrollLocked(page)).toBe(true);

        await tap(cdp, page.getByTestId('edit-trigger'));
        await expect(page.getByTestId('edit-menu')).toBeVisible();
        await expect(page.getByTestId('file-menu')).toHaveCount(0);

        await expect.poll(() => isScrollLocked(page)).toBe(false);
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
        // Base UI renders its own `<Menubar orientation="vertical" />` here, in every setup.
        await renderFixture(page, mode, { orientation: 'vertical' });
        await expect(page.getByRole('menubar')).toHaveAttribute('aria-orientation', 'vertical');
      });

      test('moves between triggers with the main-axis arrow keys when vertical', async ({ page, mode }) => {
        // Base UI renders its own contained `<Menubar orientation="vertical">` here, in every setup.
        await renderFixture(page, mode, { orientation: 'vertical' });
        await page.getByTestId('file-trigger').focus();

        await page.keyboard.press('ArrowDown');
        await expect(page.getByTestId('edit-trigger')).toBeFocused();
        await expect(page.getByRole('menu')).toHaveCount(0);
      });

      for (const { direction, openKey, oppositeKey } of [
        { direction: 'ltr', openKey: 'ArrowRight', oppositeKey: 'ArrowLeft' },
        { direction: 'rtl', openKey: 'ArrowLeft', oppositeKey: 'ArrowRight' },
      ] as const) {
        test(`opens a menu of a vertical ${direction} menubar with ${openKey}`, async ({ page, mode }) => {
          // Base UI renders its own contained vertical bar in a DirectionProvider here, in every setup.
          const fixture = await renderFixture(page, mode, { orientation: 'vertical', dir: direction });
          await page.getByTestId('file-trigger').focus();

          await page.keyboard.press(oppositeKey);
          await settle(fixture);
          await expect(page.getByRole('menu')).toHaveCount(0);

          await page.keyboard.press(openKey);
          await expect(page.getByRole('menu')).toHaveCount(1);
        });
      }

      test('sets role="menuitem" on menu triggers', async ({ page, mode }) => {
        await render(page, mode);
        await expect(page.getByRole('menubar').getByRole('menuitem')).toHaveCount(3);
      });
    });
  });
  }
});
