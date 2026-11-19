import { test, expect, advanceTime, testWithTouch, touchDrag, type Page } from './helpers';
import type { Locator } from '@playwright/test';
import { addToast, centreOf, openToast, recorded } from './toast.helpers';

// Toast.Viewport's [L4] rows and the re-tagged hover rows (docs/components/toast.md §6.1, §6.2, §6.3; issue 280, M6 track
// 9b): F6, the guards, Tab and Shift+Tab, the focus hand-off, hover, touch and the timers, on the `toast-basic` fixture.
// Titles are Base UI's from ToastViewport.test.tsx; the jsdom-only titles run here with real focus (the brief's
// Deliverable 7). The timers are C#'s, moved by `advanceTime` (never `page.clock`). Base UI's `fireEvent.mouseEnter` and
// `mouseLeave` are the real mouse; its `fireEvent.focus(guard, { relatedTarget: viewport })` is a real Tab from the
// focused viewport, and its `fireEvent.pointerDown(..., { pointerType: 'touch' })` real CDP touch.

/** Whether the toast with the id is still in the list; a sync round trip first, so C#'s timers have been applied. */
async function present(page: Page, fixture: Locator, id = 't1'): Promise<boolean> {
  const sync = Number(await fixture.getAttribute('data-sync'));
  await page.locator('[data-sync-button]').dispatchEvent('click');
  await expect(fixture).toHaveAttribute('data-sync', String(sync + 1));
  return (await page.locator(`[data-toast-id="${id}"]`).count()) === 1;
}

async function hover(page: Page, locator: Locator): Promise<void> {
  const { x, y } = await centreOf(locator);
  await page.mouse.move(x, y, { steps: 2 });
}

async function leave(page: Page): Promise<void> {
  await page.mouse.move(2, 2, { steps: 2 });
}

test.describe('Toast.Viewport', () => {
  test('sets the measured frontmost toast height CSS variable', async ({ page, mode }) => {
    await openToast(page, mode);
    const root = await addToast(page);

    const height = await root.getAttribute('data-height');
    await expect(page.getByTestId('viewport')).toHaveCSS('--toast-frontmost-height', `${height}px`);
  });

  test('gets focused when F6 is pressed', async ({ page, mode }) => {
    await openToast(page, mode);
    await addToast(page);

    await page.keyboard.press('F6');

    await expect(page.getByTestId('viewport')).toBeFocused();
  });

  test('focuses first toast upon tab after viewport is focused', async ({ page, mode }) => {
    await openToast(page, mode);
    await addToast(page);

    await page.keyboard.press('F6');
    await page.keyboard.press('Tab');

    await expect(page.getByTestId('root')).toBeFocused();
  });

  // Quarantined (ADR 11 §4) on Server until 2026-12-03, issue 318: failed twice in CI on Server (PR 305's run 44463962534;
  // PR 325's run 44468298715, browser-server 2). WebAssembly runs it.
  test('returns focus to previous element when pressing shift+Tab on first toast', {
    tag: '@quarantine',
    annotation: { type: 'open', description: 'https://github.com/JakeMurrayDev/Blazix.Primitives/issues/318 (expires 2026-12-03)' },
  }, async ({ page, mode }) => {
    test.fixme(mode === 'server', 'Quarantined on Server until 2026-12-03 (issue 318)');
    await openToast(page, mode);
    await addToast(page);

    await page.keyboard.press('F6');
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('root')).toBeFocused();
    await page.keyboard.press('Shift+Tab');

    await expect(page.getByTestId('add')).toBeFocused();
  });

  test('returns focus to previous element when pressing shift+Tab on last toast', async ({ page, mode }) => {
    await openToast(page, mode);
    await addToast(page);
    await addToast(page);

    await page.keyboard.press('F6');
    await page.keyboard.press('Tab'); // first toast
    await expect(page.locator('[data-toast-id="t2"]')).toBeFocused();
    await page.keyboard.press('Tab'); // first toast close button
    await page.keyboard.press('Tab'); // first toast action button
    await page.keyboard.press('Tab'); // last toast
    await expect(page.locator('[data-toast-id="t1"]')).toBeFocused();
    await page.keyboard.press('Tab'); // last toast close button
    await page.keyboard.press('Tab'); // last toast action button
    await page.keyboard.press('Tab');

    await expect(page.getByTestId('add')).toBeFocused();
  });

  test('port: a real hover expands the stack and pauses the timers, and leaving collapses and resumes them', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    const root = await addToast(page);
    const viewport = page.getByTestId('viewport');

    await hover(page, root);
    await expect(viewport).toHaveAttribute('data-expanded', '');
    await expect(root).toHaveAttribute('data-expanded', '');
    await advanceTime(page, fixture, 5001);
    expect(await present(page, fixture)).toBe(true);

    await leave(page);
    await expect(viewport).not.toHaveAttribute('data-expanded');
    await advanceTime(page, fixture, 4999);
    expect(await present(page, fixture)).toBe(true);
    await advanceTime(page, fixture, 2);
    expect(await present(page, fixture)).toBe(false);
    expect(await recorded(fixture, 'data-reports')).toEqual(expect.arrayContaining(['enter', 'leave']));
  });

  test('pauses timers when the viewport is focused', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    await addToast(page);

    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('f6');
    await advanceTime(page, fixture, 5001);

    expect(await present(page, fixture)).toBe(true);
  });

  test('restores focus and resumes timers on shift+Tab out of the focused viewport', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    await addToast(page);
    const button = page.getByTestId('add');

    await page.keyboard.press('F6');
    const viewport = page.getByTestId('viewport');
    await expect(viewport).toBeFocused();
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('f6');
    await advanceTime(page, fixture, 5001);
    expect(await present(page, fixture)).toBe(true);

    await page.keyboard.press('Shift+Tab');
    await expect(button).toBeFocused();
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('blur');
    await advanceTime(page, fixture, 5001);

    expect(await present(page, fixture)).toBe(false);
  });

  test('keeps timers paused when shift+Tab returns focus inside the viewport', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    await addToast(page);
    // Pressing F6 from a control inside the toast makes the restore target itself live inside the viewport.
    const close = page.getByLabel('close-press');
    await close.focus();

    await page.keyboard.press('F6');
    const viewport = page.getByTestId('viewport');
    await expect(viewport).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(close).toBeFocused();
    await advanceTime(page, fixture, 5001);

    // Focus never left the viewport, so the toast must stay put.
    expect(await present(page, fixture)).toBe(true);
  });

  test('keeps the viewport focused when Tab is pressed without shift', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    await addToast(page);

    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
    await page.keyboard.press('Tab');

    // Forward Tab moves into the toasts, so the viewport must not hand focus back.
    await expect(page.getByTestId('root')).toBeFocused();
    await expect(page.getByTestId('add')).not.toBeFocused();
    await advanceTime(page, fixture, 5001);
    expect(await present(page, fixture)).toBe(true);
  });

  test('ignores a mouse pointerdown outside the viewport', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    const root = await addToast(page);

    await hover(page, root);
    await expect(page.getByTestId('viewport')).toHaveAttribute('data-expanded', '');
    // Only touch activity ends the paused interaction; a mouse press outside is followed by a `mouseleave`, which handles
    // the collapse instead. The press is a real one, through CDP, with the pointer still over the toast.
    const cdp = await page.context().newCDPSession(page);
    await cdp.send('Input.dispatchMouseEvent', { type: 'mousePressed', x: 2, y: 2, button: 'left', clickCount: 1 });
    await cdp.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: 2, y: 2, button: 'left', clickCount: 1 });
    await hover(page, root);

    await expect(page.getByTestId('viewport')).toHaveAttribute('data-expanded', '');
    await advanceTime(page, fixture, 5001);
    expect(await present(page, fixture)).toBe(true);
    expect(await recorded(fixture, 'data-reports')).not.toContain('touch-outside');
  });

  test('resumes timers when the viewport is blurred', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    await addToast(page);

    await page.keyboard.press('F6');
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('f6');
    await advanceTime(page, fixture, 5001);
    await page.getByTestId('add').focus();
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('blur');
    await advanceTime(page, fixture, 5001);

    expect(await present(page, fixture)).toBe(false);
  });

  // DP7 of the Toast track (chosen by the user 2026-11-19), (a) probed first: in headless Chromium a second page brought to
  // the front fires no `blur` or `focus` on the fixture's window and `document.hasFocus()` stays true on both pages (probe of
  // 2026-11-19, both modes). So (b): the window's own events are dispatched in the page, the dated ADR 06 §1 exception beside
  // the in-page `TouchEvent` one; Base UI's titles call the captured listeners with a `composedPath` of `[window]`, which is
  // what a window-targeted event gives.
  test('resumes timers when the window regains focus', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    await addToast(page);

    await advanceTime(page, fixture, 1000);
    await page.evaluate(() => window.dispatchEvent(new FocusEvent('blur')));
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('window-blur');
    await advanceTime(page, fixture, 5000);
    expect(await present(page, fixture)).toBe(true);

    await page.evaluate(() => window.dispatchEvent(new FocusEvent('focus')));
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('window-focused');
    await advanceTime(page, fixture, 3999);
    expect(await present(page, fixture)).toBe(true);
    await advanceTime(page, fixture, 2);
    expect(await present(page, fixture)).toBe(false);
  });

  test('keeps timers paused on mouseleave while the window is blurred', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    const root = await addToast(page);

    // Hovering pauses the timer.
    await hover(page, root);
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('enter');
    await advanceTime(page, fixture, 1000);
    // The window loses focus while still hovering.
    await page.evaluate(() => window.dispatchEvent(new FocusEvent('blur')));
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('window-blur');
    // Leaving with the pointer must not resume the timer while the window is blurred, otherwise the toast could expire
    // off-screen.
    await leave(page);
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('leave');
    await advanceTime(page, fixture, 10000);
    expect(await present(page, fixture)).toBe(true);
  });

  // DP8 of the Toast track (chosen by the user 2026-11-19), (a) probed first (2026-11-19, both modes): a Viewport moved into
  // a same-origin iframe keeps being rendered by Blazor, but Blazor revives an element reference only against the host
  // document, so every handle inside the iframe fails to attach ("Blazix: no element for the reference"; on Server the
  // circuit ends). So (b): the iframe clauses of TST-STR-13 and TST-EDGE-08 stay open for track 10, issue 316 (the user's
  // choice of 2026-11-19); the once-per-cycle binding (TST-FOC-14, TST-EDGE-07) is the port title below, on the host
  // document.
  test('rebinds owner-document listeners once across empty store cycles', async () => {
    test.info().annotations.push({
      type: 'open',
      description: 'TST-STR-13, TST-EDGE-08: a Viewport inside an iframe cannot attach its handles (Blazor revives element references only against the host document); open for track 10, issue 316 (DP8 (b), chosen by the user 2026-11-19, "Keep open for track 10").',
    });
    test.fixme(true, 'Open: a Viewport in an iframe cannot attach; issue 316, track 10.');
  });

  test('port: binds the window and document listeners while toasts exist, once per cycle', async ({ page, mode }) => {
    await page.addInitScript(() => {
      const calls: string[] = [];
      (window as unknown as { listenerCalls: string[] }).listenerCalls = calls;
      for (const [target, name] of [[window, 'window'], [document, 'document']] as const) {
        const add = target.addEventListener.bind(target);
        const remove = target.removeEventListener.bind(target);
        target.addEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) => {
          calls.push(`+${name}:${type}`);
          add(type, listener, options);
        };
        target.removeEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) => {
          calls.push(`-${name}:${type}`);
          remove(type, listener, options);
        };
      }
    });
    const fixture = await openToast(page, mode);
    const calls = async (sign: '+' | '-', key: string) =>
      (await page.evaluate(() => (window as unknown as { listenerCalls: string[] }).listenerCalls)).filter((call) => call === `${sign}${key}`).length;
    const baseline = await calls('+', 'window:keydown');

    // While the list is empty, F6 does nothing.
    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).not.toBeFocused();

    await addToast(page);
    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
    expect(await calls('+', 'window:keydown')).toBe(baseline + 1);
    expect(await calls('+', 'window:blur')).toBeGreaterThanOrEqual(1);
    expect(await calls('+', 'document:pointerdown')).toBeGreaterThanOrEqual(1);

    await page.locator('[data-testid="close-all"]').dispatchEvent('click');
    await expect(page.locator('[data-toast-id]')).toHaveCount(0);
    await expect.poll(() => calls('-', 'window:keydown')).toBeGreaterThanOrEqual(1);
    await page.getByTestId('add').focus();
    await page.keyboard.press('F6');
    await expect(page.getByTestId('add')).toBeFocused();

    await addToast(page);
    await expect.poll(() => calls('+', 'window:keydown')).toBe(baseline + 2);
    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
    expect(await recorded(fixture, 'data-reports')).toEqual(expect.arrayContaining(['f6']));
  });

  // TST §16 Q3, answered by track 11's real-browser probe of Base UI (5 of 5): with the previous focus before the viewport,
  // the outer guard sends a forward Tab from it straight back, so the viewport is reachable only by F6. Replicated under the
  // M6 plan's decision 23 (a) (chosen by the user 2026-11-18).
  test('port: a forward Tab from a previous focus before the viewport bounces back to it (TST §16 Q3)', async ({ page, mode }) => {
    await openToast(page, mode, { before: '1' });
    const button = page.getByTestId('add');
    await addToast(page);

    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(button).toBeFocused();
    await expect(page.locator('[data-base-ui-focus-guard]')).toHaveCount(3);

    await page.keyboard.press('Tab');
    await expect(button).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(button).toBeFocused();
    await expect(page.getByTestId('root')).not.toBeFocused();
  });

  // DP4 (a) of the Toast track (chosen by the user 2026-11-19), useToastManager.test.tsx's "high priority toasts in dialogs
  // have correct accessibility structure" with real focus management: the modal dialog marks the outside, and the viewport
  // (a polite live region) and the alert region stay reachable (TST-EDGE-12, TST-A11Y-01).
  test('high priority toasts in dialogs have correct accessibility structure', async ({ page, mode }) => {
    await openToast(page, mode, {}, 'toast-dialog');
    await expect(page.getByTestId('dialog')).toBeVisible();

    await page.getByTestId('add').click();

    const toastRoot = page.getByTestId('toast-root');
    await expect(toastRoot).toHaveAttribute('aria-hidden', 'true');
    await expect(page.getByRole('alert')).toHaveCount(1);
    await expect(page.getByRole('alert')).toContainText('High priority toast');
    await expect(page.getByRole('region', { name: 'Notifications' })).toBeAttached();
    expect(await page.getByTestId('viewport').evaluate((element) => element.closest('[aria-hidden="true"], [inert]') === null)).toBe(true);
  });

  // TST-STR-09, TST-DOM-20, TST-API-17 (the L4 clause, the container append): the lite portal's node is appended to the
  // body, carries a generated id and `data-base-ui-portal`, and the viewport and its module work inside it.
  test('port: the portal appends its node to the body, and the viewport works inside it', async ({ page, mode }) => {
    await openToast(page, mode, {}, 'toast-portal');
    const portal = page.getByTestId('portal');

    expect(await portal.evaluate((element) => element.parentElement === document.body)).toBe(true);
    await expect(portal).toHaveAttribute('data-base-ui-portal', '');
    await expect(portal).toHaveAttribute('id', /.+/);
    await expect(portal).toHaveClass('toast-portal');
    await addToast(page);
    await expect(portal.locator('[data-toast-id="t1"]')).toBeAttached();
    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
  });

  test.describe('focus management', () => {
    test('skips toasts animating out when tabbing into the viewport', async ({ page, mode }) => {
      await openToast(page, mode, { animated: '1', animations: '1' });
      await addToast(page); // oldest toast
      await addToast(page); // newest toast (toasts[0])
      const newest = page.locator('[data-toast-id="t2"]');
      const survivor = page.locator('[data-toast-id="t1"]');

      // Close the newest toast: it enters `ending` and stays mounted for its 10 s exit.
      await page.getByTestId('close-newest').click();
      await expect(newest).toHaveAttribute('data-ending-style', '');
      // F6 focuses the viewport and renders the focus guards; Tab lands on the first inner guard from the viewport.
      await page.keyboard.press('F6');
      await expect(page.locator('[data-base-ui-focus-guard]')).toHaveCount(3);
      await page.keyboard.press('Tab');

      await expect(survivor).toBeFocused();
      await expect(newest).not.toBeFocused();
    });

    test('returns focus when no toast can receive focus', async ({ page, mode }) => {
      await openToast(page, mode, { limit: '0' });
      const button = page.getByTestId('add');
      await addToast(page);

      await page.keyboard.press('F6');
      await expect(page.getByTestId('viewport')).toBeFocused();
      await expect(page.locator('[data-base-ui-focus-guard]')).toHaveCount(3);
      await page.keyboard.press('Tab');

      await expect(button).toBeFocused();
    });

    test('returns focus to the trigger when every toast is closed', async ({ page, mode }) => {
      await openToast(page, mode, { timeout: '0' });
      const button = page.getByTestId('add');
      await addToast(page);

      await page.keyboard.press('F6');
      await page.keyboard.press('Tab');
      await expect(page.getByTestId('root')).toBeFocused();

      // Closing everything leaves no toast to hand focus to. The close is the manager's, from a key the page sends to a
      // control outside the toasts' tab order, so the focus stays on the toast until the close.
      await page.evaluate(() => (document.querySelector('[data-testid="close-all"]') as HTMLButtonElement).click());

      await expect(button).toBeFocused();
    });

    test('moves focus past toasts animating out when one is closed', async ({ page, mode }) => {
      await openToast(page, mode, { timeout: '0', animated: '1', animations: '1', content: '1' });
      await page.getByTestId('add').focus();
      await page.getByTestId('add-two').click();
      await page.getByTestId('add-one').click();
      await addToast(page, 3);
      const [newest, middle, oldest] = [page.locator('[data-toast-id="t1"]'), page.locator('[data-toast-id="one"]'), page.locator('[data-toast-id="two"]')];

      await page.keyboard.press('F6');
      await page.keyboard.press('Tab');
      await expect(newest).toBeFocused();

      // Dismissing both in one go leaves the middle toast animating out while the focused toast closes, so focus has to
      // skip over it.
      await page.evaluate(() => {
        (document.querySelector('[data-testid="close-one"]') as HTMLButtonElement).click();
        (document.querySelector('[data-testid="close-newest"]') as HTMLButtonElement).click();
      });

      await expect(middle).toHaveAttribute('data-ending-style', '');
      await expect(oldest).toBeFocused();
    });

    test('leaves focus alone when it is outside the viewport', async ({ page, mode }) => {
      await openToast(page, mode, { timeout: '0' });
      const button = page.getByTestId('add');
      await addToast(page);
      await addToast(page);
      await expect(button).toBeFocused();

      // Focus never entered the viewport, so closing a toast must not steal it.
      await page.keyboard.press('Tab'); // close newest
      await expect(page.getByTestId('close-newest')).toBeFocused();
      await page.keyboard.press('Tab'); // close all
      await page.keyboard.press('Enter');

      await expect(page.locator('[data-toast-id]')).toHaveCount(0);
      await expect(page.getByTestId('close-all')).toBeFocused();
    });
  });
});

testWithTouch.describe('Toast.Viewport with touch', () => {
  // Base UI fires touch `pointerdown`/`pointermove`/`pointerup` on the root and `mouseLeave` on the viewport; here the touch
  // is real CDP touch on a root with `touch-action: none` (so Chromium does not pan and cancel), and the leave is the real
  // mouse, which entered the viewport first so that it can leave it.
  testWithTouch('keeps expanded during an active touch swipe even if mouseleave fires', async ({ touchPage: page, mode }) => {
    await openToast(page, mode, { directions: 'down', touch: '1' });
    const root = await addToast(page);
    const viewport = page.getByTestId('viewport');
    const start = await centreOf(root);
    await hover(page, root);

    await touchDrag(page, [[start, { x: start.x, y: start.y + 20 }]], {
      beforeRelease: async () => {
        await expect(root).toHaveAttribute('data-swiping', '');
        await expect(viewport).toHaveAttribute('data-expanded', '');
        await leave(page);
        await page.locator('[data-sync-button]').dispatchEvent('click');
        await expect(page.locator('[data-fixture]')).toHaveAttribute('data-sync', '1');
        await expect(viewport).toHaveAttribute('data-expanded', '');
      },
    });

    await expect(viewport).not.toHaveAttribute('data-expanded');
  });

  testWithTouch('keeps expanded when a touch swipe is canceled without leaving the viewport', async ({ touchPage: page, mode }) => {
    await openToast(page, mode, { directions: 'down', touch: '1' });
    const root = await addToast(page);
    const viewport = page.getByTestId('viewport');
    const start = await centreOf(root);

    await touchDrag(page, [[start, { x: start.x, y: start.y + 20 }]], {
      release: 'cancel',
      beforeRelease: async () => {
        await expect(root).toHaveAttribute('data-swiping', '');
        await expect(viewport).toHaveAttribute('data-expanded', '');
      },
    });

    await expect(root).not.toHaveAttribute('data-swiping');
    await page.locator('[data-sync-button]').dispatchEvent('click');
    await expect(page.locator('[data-fixture]')).toHaveAttribute('data-sync', '1');
    await expect(viewport).toHaveAttribute('data-expanded', '');
  });

  testWithTouch('collapses after a touch swipe is canceled if mouseleave already fired', async ({ touchPage: page, mode }) => {
    await openToast(page, mode, { directions: 'down', touch: '1' });
    const root = await addToast(page);
    const viewport = page.getByTestId('viewport');
    const start = await centreOf(root);
    await hover(page, root);

    await touchDrag(page, [[start, { x: start.x, y: start.y + 20 }]], {
      release: 'cancel',
      beforeRelease: async () => {
        await expect(root).toHaveAttribute('data-swiping', '');
        await leave(page);
      },
    });

    await expect(root).not.toHaveAttribute('data-swiping');
    await expect(viewport).not.toHaveAttribute('data-expanded');
  });

  testWithTouch('collapses and resumes timers on a touch outside the viewport', async ({ touchPage: page, mode }) => {
    const fixture = await openToast(page, mode);
    const root = await addToast(page);
    const viewport = page.getByTestId('viewport');
    await hover(page, root);
    await expect(viewport).toHaveAttribute('data-expanded', '');
    await advanceTime(page, fixture, 5001);
    expect(await present(page, fixture)).toBe(true);

    await page.touchscreen.tap(2, 2);

    await expect(viewport).not.toHaveAttribute('data-expanded');
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('touch-outside');
    // TST-DIS-01: the touch outside closes nothing; the timer does.
    expect(await recorded(fixture, 'data-closed')).toEqual([]);
    await advanceTime(page, fixture, 5001);
    expect(await present(page, fixture)).toBe(false);
  });

  testWithTouch('stays expanded on a touch inside the viewport', async ({ touchPage: page, mode }) => {
    const fixture = await openToast(page, mode);
    const root = await addToast(page);
    const viewport = page.getByTestId('viewport');
    await hover(page, root);
    await expect(viewport).toHaveAttribute('data-expanded', '');

    const { x, y } = await centreOf(page.getByTestId('description'));
    await page.touchscreen.tap(x, y);

    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('touch-press');
    expect(await recorded(fixture, 'data-reports')).not.toContain('touch-outside');
    await expect(viewport).toHaveAttribute('data-expanded', '');
    await advanceTime(page, fixture, 5001);
    expect(await present(page, fixture)).toBe(true);
  });
});

export {};
