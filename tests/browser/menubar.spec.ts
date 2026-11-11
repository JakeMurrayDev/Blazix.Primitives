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
/**
 * Base UI's `user.click`: user-event's sequence dispatched to the element (the presses, the focus, the releases, the click),
 * which hit-tests nothing and leaves the browser's focus-visible heuristic without a pointer interaction.
 */
async function userClick(target: Locator): Promise<void> {
  await target.evaluate((element: HTMLElement) => {
    const init = { bubbles: true, cancelable: true, composed: true, button: 0, pointerType: 'mouse', isPrimary: true };
    element.dispatchEvent(new PointerEvent('pointerdown', init));
    element.dispatchEvent(new MouseEvent('mousedown', init));
    element.focus();
    element.dispatchEvent(new PointerEvent('pointerup', init));
    element.dispatchEvent(new MouseEvent('mouseup', init));
    element.dispatchEvent(new MouseEvent('click', { ...init, detail: 1 }));
  });
}

/** Base UI's `fireEvent(element, new PointerEvent('click', { bubbles: true, cancelable: true, pointerType: 'touch' }))`. */
async function touchClick(target: Locator): Promise<void> {
  await target.evaluate((element: HTMLElement) => {
    element.dispatchEvent(new PointerEvent('click', { bubbles: true, cancelable: true, pointerType: 'touch' }));
  });
}

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

  // Menubar.test.tsx:51-117 (MBR-PTR-08, MBR-FOC-03; MNU-STA-05's bar clause). File opens through user-event's dispatched
  // sequence, as Base UI's `user.click` does, so the programmatic focus that follows is focus-visible and opens Edit; the
  // focus goes to Edit once File's popup has taken its initial focus (ADR 01's popup-element row), else it takes it back. The
  // guard is the menu store's, under its TimeProvider (MNU-STA-05): its 300 ms window runs in real time here, so the first
  // touch click lands inside it (its dropped close is recorded by the consumer's callback, which runs first, as Base UI's
  // onOpenChange does), and the title waits Base UI's fake-timer step of 310 ms for the second.
  test('ignores a delayed touch click immediately after focus opens another menu', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await userClick(page.getByTestId('file-trigger'));
    await expect(page.getByTestId('file-menu')).toBeVisible();
    await focusInside(page, 'file-menu');

    const editTrigger = page.getByTestId('edit-trigger');
    await editTrigger.focus();
    await expect(page.getByTestId('edit-menu')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-edit-changes', /(^| )true:trigger-focus$/);

    await touchClick(editTrigger);
    await expect(fixture).toHaveAttribute('data-edit-changes', /true:trigger-focus false:trigger-press$/);
    await expect(page.getByTestId('edit-menu')).toBeVisible();

    await page.waitForTimeout(310);
    await touchClick(editTrigger);
    await expect(page.getByTestId('edit-menu')).toHaveCount(0);
  });

  test('closes on a touch item click immediately after focus opens another menu', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await userClick(page.getByTestId('file-trigger'));
    await expect(page.getByTestId('file-menu')).toBeVisible();
    await focusInside(page, 'file-menu');

    await page.getByTestId('edit-trigger').focus();
    await expect(page.getByTestId('edit-menu')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-edit-changes', /(^| )true:trigger-focus$/);

    await touchClick(page.getByTestId('edit-item-1'));
    await expect(page.getByTestId('edit-menu')).toHaveCount(0);
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
    await userClick(outside);

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
      // roving tabindex the highlight leaves. On Server the bar's hover highlight is the composite's option, sent after the
      // flag's render, and File's popup takes its initial focus a round trip after its open: a hover right after File is
      // visible met neither (MBR-PTR-06's Server row, ADR 01's popup-element row), so the title waits for the focus inside.
      const fixture = await render(page, mode);
      await moveTo(page, page.getByTestId('edit-trigger'));
      await settle(fixture);
      await expect(page.getByTestId('edit-trigger')).toHaveAttribute('tabindex', '-1');
      await expect(page.getByTestId('file-trigger')).toHaveAttribute('tabindex', '0');

      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await focusInside(page, 'file-menu');
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

    test('port: a focus-opened bar menu closes when its trigger blurs to an element outside it', async ({ page, mode }) => {
      // MBR-EVT-03 (FLT-FOC-04; MNU-FOC-16's bar clause). A bar menu takes its initial focus (MenuPopup.tsx:84-85), so the
      // title focuses the trigger again before the blur; the open is the focus switch of the touch titles above. The close's
      // reason differs by mode, open by ID (design point 7 (a) of track 8, settled 2026-11-11): the focus manager's focusout on the trigger
      // closes with `focus-out` in a microtask (FloatingFocusManager.tsx:446-566, :587), before the trigger's 0 ms blur
      // timer's `trigger-focus` (useFocus.ts:185-233), so Base UI likely records `focus-out`, as the port does on Server;
      // WebAssembly records `trigger-focus`. The title asserts the close.
      const fixture = await render(page, mode);
      await userClick(page.getByTestId('file-trigger'));
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await focusInside(page, 'file-menu');
      const editTrigger = page.getByTestId('edit-trigger');
      await editTrigger.focus();
      await expect(page.getByTestId('edit-menu')).toBeVisible();
      await expect(fixture).toHaveAttribute('data-edit-changes', /(^| )true:trigger-focus$/);
      await focusInside(page, 'edit-menu');

      await editTrigger.focus();
      await expect(editTrigger).toBeFocused();
      await page.getByTestId('before').focus();
      await expect(page.getByTestId('edit-menu')).toHaveCount(0);
      await expect(fixture).toHaveAttribute('data-edit-changes', /(^| )false:(trigger-focus|focus-out)$/);
    });

    test('port: focusable content inside the bar that is no trigger keeps its own tab stop and is skipped by the bar\'s arrow keys', async ({ page, mode }) => {
      // MBR-EDGE-04.
      await render(page, mode, { case: 'button' });
      await page.getByTestId('file-trigger').focus();
      await page.keyboard.press('ArrowRight');
      await expect(page.getByTestId('edit-trigger')).toBeFocused();

      await page.getByTestId('file-trigger').focus();
      await page.keyboard.press('Tab');
      await expect(page.getByTestId('bar-button')).toBeFocused();
    });

    test('port: a Button inside the bar infers composite mode: no tabindex of its own, and Space activates on keydown', async ({ page, mode }) => {
      // BTN-EDGE-02's Menubar example (UBT-STA-01, UBT-DOM-05, UBT-KEY-12): Base UI's useButton reads the composite root's
      // context (useButton.ts:25-26). Red on both modes (2026-11-11): the port's Button passes `composite: false`
      // (Button/Button.cs, "composite inference is M2's composite module"), so it renders tabindex="0" under any composite
      // root. Open by ID, a Button-family gap, not the bar's: issue 238.
      test.info().annotations.push({ type: 'open', description: 'BTN-EDGE-02 (issue 238): the Button never infers composite mode under a composite root' });
      test.fixme(true, 'BTN-EDGE-02 (issue 238): the Button never infers composite mode under a composite root');
      const fixture = await render(page, mode, { case: 'button' });
      const button = page.getByTestId('bar-button');
      await expect(button).not.toHaveAttribute('tabindex');

      await button.focus();
      await page.keyboard.down(' ');
      await expect(fixture).toHaveAttribute('data-item-clicks', 'button');
      await page.keyboard.up(' ');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-item-clicks', 'button');
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

    test('port: with animations, a switch between bar menus runs the outgoing exit and the incoming enter apart, and keeps the flag', async ({ page, mode }) => {
      // MBR-ANI-02: each bar menu has its own popup and transition status, and the `sibling-open` close keeps the flag.
      await render(page, mode, { animations: 1, animated: true });
      const bar = page.getByRole('menubar');
      // A CSS locator: a closing popup leaves the accessibility tree, which a role locator reads.
      const filePopup = page.locator('[data-testid="file-menu"] [role="menu"]');
      await page.getByTestId('file-trigger').click();
      await expect(filePopup).toHaveAttribute('data-open', '');

      // Recorded in the page, so the 500 ms exit cannot pass between two polls: at some mutation the outgoing popup is in its
      // ending phase while the incoming one is open, with the flag set.
      await page.evaluate(() => {
        const state = window as unknown as { menubarOverlap?: boolean };
        state.menubarOverlap = false;
        const check = () => {
          const outgoing = document.querySelector('[data-testid="file-menu"] [role="menu"]');
          const incoming = document.querySelector('[data-testid="edit-menu"] [role="menu"]');
          if (outgoing?.hasAttribute('data-ending-style') && !outgoing.hasAttribute('data-open') && incoming?.hasAttribute('data-open')
            && document.querySelector('[role="menubar"]')?.hasAttribute('data-has-submenu-open')) {
            state.menubarOverlap = true;
          }
        };
        new MutationObserver(check).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-open', 'data-ending-style'] });
      });
      await moveTo(page, page.getByTestId('edit-trigger'));
      await expect(page.getByTestId('edit-menu').getByRole('menu')).toHaveAttribute('data-open', '');
      await expect.poll(() => page.evaluate(() => (window as unknown as { menubarOverlap?: boolean }).menubarOverlap)).toBe(true);

      await expect(page.getByTestId('file-menu')).toHaveCount(0);
      await expect(page.getByTestId('edit-menu').getByRole('menu')).toHaveAttribute('data-open', '');
      await expect(bar).toHaveAttribute('data-has-submenu-open', '');
    });

    test('port: after a list-navigation close that opens no other menu the flag stays, so a hover still opens a menu', async ({ page, mode }) => {
      // MBR-EDGE-03 (with §16 Q4 replicated: the closed menu's trigger keeps the focus and the flag, and File reopens with
      // `trigger-focus`). On Server the reopened menu's backdrop lost its cut-out while the closing menu's focus manager had
      // removed its inert marker (issue 237), so the hover on Edit hit the backdrop; the positioner now keeps its backdrop.
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
        // On the shared-Root setups the one positioner moves from File's anchor to View's, so the hovers wait for a
        // stable element as Base UI's `user.hover` acts on it where it is (Playwright's actionability). On Server the
        // payload switch's Layout trigger was hovered before its handle attached; the `interactions` record of the last
        // entry replays it at the attach (design point 5 of track 8, settled 2026-11-11).
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

// ADR 01's Server rows for the bar (M5 track 8's brief, "Server rows"): the time from an input to its effect in the page's
// clock, the median of five fresh renders per mode, Server under the spec's 40 ms one-way circuit delay. An in-page
// listener records the input event and a mutation observer the effect, so Playwright's own timing is not in the figure.
// Each title also asserts that its effect came.
interface BarFigure {
  row: string;
  title: string;
  options?: PopupFixtureOptions;
  /** The event that starts the clock, and the element it must reach. */
  start: { type: string; selector: string };
  /** The effect: a selector that appears, or (absent) disappears; `scrollLock` waits for the page's scroll lock. */
  effect: { selector?: string; absent?: boolean; scrollLock?: boolean };
  prepare?: (page: Page, fixture: Locator) => Promise<void>;
  act: (page: Page, cdp: CDPSession | null) => Promise<void>;
  touch?: boolean;
}

const EDIT_OPEN = '[data-testid="edit-menu"] [role="menu"][data-open]';

const BAR_FIGURES: BarFigure[] = [
  {
    row: 'MBR-PTR-01',
    title: 'the press on a closed trigger: from the mousedown to the open popup',
    start: { type: 'mousedown', selector: '[data-testid="file-trigger"]' },
    effect: { selector: '[data-testid="file-menu"] [role="menu"][data-open]' },
    act: async (page) => {
      await page.getByTestId('file-trigger').click();
    },
  },
  {
    row: 'MBR-PTR-04',
    title: 'the hover switch: from the mouseover of another trigger to its open popup',
    start: { type: 'mouseover', selector: '[data-testid="edit-trigger"]' },
    effect: { selector: EDIT_OPEN },
    prepare: async (page) => {
      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await focusInside(page, 'file-menu');
    },
    act: async (page) => {
      await moveTo(page, page.getByTestId('edit-trigger'));
    },
  },
  {
    row: 'MBR-FOC-03',
    title: 'the focus switch: from the focus of another trigger to its open popup',
    start: { type: 'focusin', selector: '[data-testid="edit-trigger"]' },
    effect: { selector: EDIT_OPEN },
    prepare: async (page) => {
      await userClick(page.getByTestId('file-trigger'));
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await focusInside(page, 'file-menu');
    },
    act: async (page) => {
      await page.getByTestId('edit-trigger').focus();
    },
  },
  {
    row: 'MBR-KEY-08',
    title: 'the keyboard switch: from ArrowRight in a bar menu to the next menu\'s open popup',
    start: { type: 'keydown', selector: '[data-testid="file-menu"]' },
    effect: { selector: EDIT_OPEN },
    prepare: async (page) => {
      await page.getByTestId('file-trigger').focus();
      await page.keyboard.press('Enter');
      await expect(page.getByTestId('file-item-1')).toBeFocused();
    },
    act: async (page) => {
      await page.keyboard.press('ArrowRight');
    },
  },
  {
    row: 'MBR-EVT-03',
    title: 'the blur close: from the focus leaving a focus-opened menu\'s trigger to the popup\'s removal',
    start: { type: 'focusin', selector: '[data-testid="before"]' },
    effect: { selector: '[data-testid="edit-menu"]', absent: true },
    prepare: async (page) => {
      await userClick(page.getByTestId('file-trigger'));
      await expect(page.getByTestId('file-menu')).toBeVisible();
      await focusInside(page, 'file-menu');
      await page.getByTestId('edit-trigger').focus();
      await expect(page.getByTestId('edit-menu')).toBeVisible();
      await focusInside(page, 'edit-menu');
      await page.getByTestId('edit-trigger').focus();
    },
    act: async (page) => {
      await page.getByTestId('before').focus();
    },
  },
  {
    row: 'MBR-DIS-05',
    title: 'the touch lock: from the touch on a trigger to the page\'s scroll lock',
    options: { case: 'scroll-lock' },
    start: { type: 'pointerdown', selector: '[data-testid="file-trigger"]' },
    effect: { scrollLock: true },
    touch: true,
    act: async (page, cdp) => {
      await tap(cdp!, page.getByTestId('file-trigger'));
    },
  },
];

test.describe('port: the bar\'s Server timings (ADR 01)', () => {
  for (const figure of BAR_FIGURES) {
    test(`port: ${figure.title} (${figure.row})`, async ({ page, mode }) => {
      const times: number[] = [];
      for (let run = 0; run < 5; run += 1) {
        const fixture = await renderFixture(page, mode, figure.options ?? {});
        const cdp = figure.touch ? await touchSession(page) : null;
        await figure.prepare?.(page, fixture);
        await page.evaluate(({ start, effect }) => {
          const record = { t0: -1, t1: -1 };
          (window as unknown as { barFigure: typeof record }).barFigure = record;
          const met = () =>
            effect.scrollLock
              ? document.documentElement.style.overflow === 'hidden' || document.documentElement.hasAttribute('data-base-ui-scroll-locked') || document.body.style.overflow === 'hidden'
              : (document.querySelector(effect.selector!) !== null) !== !!effect.absent;
          document.addEventListener(start.type, (event) => {
            if (record.t0 < 0 && (event.target as Element | null)?.closest?.(start.selector)) {
              record.t0 = performance.now();
            }
          }, true);
          const check = () => {
            if (record.t0 >= 0 && record.t1 < 0 && met()) {
              record.t1 = performance.now();
            }
          };
          new MutationObserver(check).observe(document.documentElement, { subtree: true, childList: true, attributes: true });
        }, { start: figure.start, effect: figure.effect });
        await figure.act(page, cdp);
        await expect.poll(() => page.evaluate(() => (window as unknown as { barFigure: { t1: number } }).barFigure.t1)).toBeGreaterThanOrEqual(0);
        times.push(await page.evaluate(() => {
          const record = (window as unknown as { barFigure: { t0: number; t1: number } }).barFigure;
          return record.t1 - record.t0;
        }));
      }
      times.sort((a, b) => a - b);
      const description = `${figure.row} ${mode}: median ${times[2]!.toFixed(0)} ms (${times.map((t) => t.toFixed(0)).join(', ')})`;
      test.info().annotations.push({ type: 'figure', description });
      console.log(`[figure] ${description}`);
    });
  }
});
