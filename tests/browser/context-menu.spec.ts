import { type Locator } from '@playwright/test';
import { delayCircuit, expect, test, waitForPositioned, type Page, type RenderMode } from './helpers';
import { center, holdClock, parkPointer, positionerOf, withClock } from './menu.helpers';
import { gotoPopupFixture, openChanges, pressControl, settle, type PopupFixtureOptions } from './suites/popupFixture';

// The L4 titles of ContextMenuRoot.test.tsx, ContextMenuRoot.non-mac.test.tsx and ContextMenuTrigger.test.tsx (M5 track 7,
// issue 194; docs/components/context-menu.md), titles verbatim, and the port titles of the `none` rows, on both render
// modes. Every Server title runs with a one-way circuit delay (wave 2's lessons: the window between a C# render and a JS
// handle). Gestures are real Playwright input: right-button presses and releases through `page.mouse`, so Chromium fires
// `contextmenu` on the press; touch titles dispatch `TouchEvent`s in the page (Base UI's `fireEvent.touch*`, as
// dialog.spec.ts does), and the platform is pinned by `navigator.platform` before the first load (Base UI mocks
// `platform.os.mac`: true in ContextMenuRoot.test.tsx, the real jsdom value, not macOS, elsewhere). Timed titles hold the
// page's clock and step it (`page.clock`), the open's own timers included, as `runClockUntil` does for Menu.

const FIXTURE = 'context-menu-root';
const SERVER_DELAY_MS = 40;

type Platform = 'mac' | 'other';

async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}, platform: Platform = 'other'): Promise<Locator> {
  await page.addInitScript((value) => {
    Object.defineProperty(Navigator.prototype, 'platform', { configurable: true, get: () => value });
  }, platform === 'mac' ? 'MacIntel' : 'Linux x86_64');
  if (mode === 'server') {
    await delayCircuit(page, SERVER_DELAY_MS);
  }
  await parkPointer(page);
  return gotoPopupFixture(page, mode, FIXTURE, options);
}

/** The recorded event types of the root's open changes. */
async function openEvents(fixture: Locator): Promise<string[]> {
  return ((await fixture.getAttribute('data-open-events')) ?? '').split(' ').filter(Boolean);
}

/** Presses the right button over a point of an element (the pointer moves there first): Chromium fires `contextmenu`. */
async function rightDown(page: Page, target: Locator, offset?: { x: number; y: number }): Promise<{ x: number; y: number }> {
  const box = (await target.boundingBox())!;
  const point = offset ? { x: box.x + offset.x, y: box.y + offset.y } : { x: box.x + box.width / 2, y: box.y + box.height / 2 };
  await page.mouse.move(point.x, point.y);
  await page.mouse.down({ button: 'right' });
  return point;
}

/** A right click at an element's point: the press, Chromium's `contextmenu`, the release. */
async function rightClick(page: Page, target: Locator, offset?: { x: number; y: number }): Promise<{ x: number; y: number }> {
  const point = await rightDown(page, target, offset);
  await page.mouse.up({ button: 'right' });
  return point;
}

/**
 * Steps a held clock in 10 ms steps until `shown` holds, at most `limitMs`, and returns the page time it ran: the open's
 * render reaches the page through timers on both modes, so a title that counts 500 ms from the gesture subtracts it.
 */
async function runUntil(page: Page, shown: () => Promise<boolean>, limitMs = 400): Promise<number> {
  let ran = 0;
  while (ran < limitMs && !(await shown())) {
    await page.clock.runFor(10);
    ran += 10;
  }
  return ran;
}

/**
 * Waits, with the page's clock held since before the gesture, until an element shows, and returns the page time it ran.
 * On Server the open's render arrives over the (delayed) circuit in real time, which a held clock does not stop, so the
 * title waits in real time and spends none of the page's; on WebAssembly the runtime's continuations are page timers (the
 * frozen-clock stall), so the clock is stepped and the time it ran is returned for the title to subtract.
 */
async function awaitShown(page: Page, mode: RenderMode, target: Locator): Promise<number> {
  if (mode === 'server') {
    await expect(target).toBeVisible();
    return 0;
  }
  return runUntil(page, () => target.isVisible());
}

/** Waits until the Positioner holding an element has its position, so the popup's handles are attached on both modes. */
async function positioned(target: Locator, _mode?: RenderMode, clockFrozen = false): Promise<void> {
  await waitForPositioned(positionerOf(target), { clockFrozen });
}

/** The centre of an element once its Positioner has its position, with the page's clock held. */
async function heldCenter(target: Locator): Promise<{ x: number; y: number }> {
  await positioned(target, undefined, true);
  const box = (await target.boundingBox())!;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

type TouchPoint = { x: number; y: number; id?: number };

/** Base UI's `fireEvent.touch*`: a `TouchEvent` built in the page on the element, with its touch list. */
async function touch(target: Locator, type: 'touchstart' | 'touchmove' | 'touchend' | 'touchcancel', touches: TouchPoint[] = []): Promise<void> {
  await target.evaluate(
    (element, { type: eventType, points }) => {
      const list = points.map((p) => new Touch({ identifier: p.id ?? 0, target: element, clientX: p.x, clientY: p.y }));
      element.dispatchEvent(new TouchEvent(eventType, { bubbles: true, cancelable: true, composed: true, touches: list, targetTouches: list, changedTouches: list }));
    },
    { type, points: touches },
  );
}

/** Dispatches a synthetic `contextmenu` on an element and returns `defaultPrevented` after the dispatch (Base UI's form). */
async function dispatchContextMenu(target: Locator): Promise<boolean> {
  return target.evaluate((element) => {
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, composed: true, button: 2 });
    element.dispatchEvent(event);
    return event.defaultPrevented;
  });
}

test.describe('<ContextMenu.Root />', () => {
  test.describe('interactions', () => {
    test('closes nested submenus when releasing the context menu pointer over an item', async ({ page, mode }) => {
      const fixture = await render(page, mode, { submenu: 'open' }, 'mac');
      await rightDown(page, page.getByTestId('trigger'), { x: 20, y: 20 });
      await expect(page.getByTestId('popup')).toBeVisible();
      await expect(page.getByTestId('submenu-popup')).toBeVisible();

      const target = await center(page.getByTestId('submenu-item'));
      await page.mouse.move(target.x, target.y, { steps: 4 });
      await page.mouse.up({ button: 'right' });

      await expect(page.getByTestId('submenu-popup')).toBeHidden();
      await expect(page.getByTestId('popup')).toBeHidden();
      expect((await fixture.getAttribute('data-submenu-open-changes'))!.split(' ').at(-1)).toBe('false:item-press');
      expect((await openChanges(fixture)).at(-1)).toBe('false:item-press');
    });

    test('does not activate a submenu trigger when releasing the context menu pointer over it', async ({ page, mode }) => {
      const fixture = await render(page, mode, { submenu: 'closed' }, 'mac');
      await rightDown(page, page.getByTestId('trigger'), { x: 20, y: 20 });
      await expect(page.getByTestId('popup')).toBeVisible();

      const target = await center(page.getByTestId('submenu-trigger'), { unanchored: false });
      await page.mouse.move(target.x, target.y, { steps: 2 });
      await page.mouse.up({ button: 'right' });
      await settle(fixture);

      await expect(page.getByTestId('submenu-popup')).toHaveCount(0);
      expect(await fixture.getAttribute('data-submenu-open-changes')).toBe('');
    });

    test('ignores mouseup directly under the cursor when the context menu spawns there', async ({ page, mode }) => {
      const fixture = await render(page, mode, { alignOffset: 0 }, 'mac');
      await rightDown(page, page.getByTestId('trigger'), { x: 12, y: 12 });
      await expect(page.getByTestId('popup')).toBeVisible();
      // The popup opened under the cursor: the release over its item is the gesture's own.
      await positioned(page.getByTestId('item'), mode);
      await page.mouse.up({ button: 'right' });
      await settle(fixture);

      await expect(page.getByTestId('popup')).toBeVisible();
      expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    });

    test('ignores mouseup directly under the cursor when alignOffset is negative', async ({ page, mode }) => {
      const fixture = await render(page, mode, { alignOffset: -5 }, 'mac');
      await rightDown(page, page.getByTestId('trigger'), { x: 18, y: 18 });
      await positioned(page.getByTestId('item'), mode);
      await page.mouse.up({ button: 'right' });
      await settle(fixture);

      await expect(page.getByTestId('popup')).toBeVisible();
      expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    });

    test('allows mouseup after leaving the initial cursor point', async ({ page, mode }) => {
      const fixture = await render(page, mode, { alignOffset: 0 }, 'mac');
      const point = await rightDown(page, page.getByTestId('trigger'), { x: 20, y: 20 });
      await positioned(page.getByTestId('item'), mode);
      await page.mouse.move(point.x + 4, point.y + 4);
      await page.mouse.up({ button: 'right' });

      await expect(page.getByTestId('popup')).toBeHidden();
      expect((await openChanges(fixture)).at(-1)).toBe('false:item-press');
      expect(await fixture.getAttribute('data-item-clicks')).toBe('Action');
    });

    test('does not open when disabled', async ({ page, mode }) => {
      const fixture = await render(page, mode, { disabled: true }, 'mac');
      await rightClick(page, page.getByTestId('trigger'), { x: 10, y: 10 });
      await settle(fixture);

      await expect(page.getByTestId('popup')).toHaveCount(0);
      expect(await openChanges(fixture)).toEqual([]);
    });
  });

  test('returns focus to the focused surface when closing with Shift+Tab', async ({ page, mode }) => {
    const fixture = await render(page, mode, { button: true }, 'mac');
    const surface = page.getByRole('button', { name: 'Right click me' });
    await surface.focus();
    await expect(surface).toBeFocused();
    // CTX-KEY-02: a `contextmenu` the platform dispatches for a keyboard request (Shift+F10) on the focused surface.
    await dispatchContextMenu(surface);
    const menu = page.getByRole('menu');
    await expect(menu).toBeFocused();

    await page.keyboard.press('Shift+Tab');
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(surface).toBeFocused();
    expect((await openChanges(fixture)).at(-1)).toBe('false:focus-out');
  });

  test.describe('prop: collisionAvoidance', () => {
    test('flips to the opposite side when side: flip is set and there is no space', async ({ page, mode }) => {
      const height = page.viewportSize()!.height;
      await render(page, mode, { case: 'flip', anchorY: height - 20 }, 'mac');

      await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'top');
    });
  });
});

test.describe('<ContextMenu.Root /> (non-Mac)', () => {
  test('ignores context menu mouseup on non-Mac platforms', async ({ page, mode }) => {
    const fixture = await render(page, mode, { alignOffset: 0 }, 'other');
    const point = await rightDown(page, page.getByTestId('trigger'), { x: 12, y: 12 });
    await positioned(page.getByTestId('item'), mode);
    await page.mouse.move(point.x + 12, point.y + 12);
    await page.mouse.up({ button: 'right' });
    await settle(fixture);

    await expect(page.getByTestId('popup')).toBeVisible();
    expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
  });
});

test.describe('<ContextMenu.Trigger />', () => {
  test('should open menu on right click (context menu event)', async ({ page, mode }) => {
    await render(page, mode);
    await rightClick(page, page.getByTestId('trigger'));

    await expect(page.getByRole('menu')).toBeVisible();
  });

  test('adds open state attributes', async ({ page, mode }) => {
    await render(page, mode, { defaultOpen: true });
    const trigger = page.getByTestId('trigger');
    await expect(trigger).toHaveAttribute('data-popup-open', '');
    await expect(page.getByRole('menu')).toBeFocused();

    await page.keyboard.press('Escape');
    await expect(trigger).not.toHaveAttribute('data-popup-open');
    await expect(trigger).not.toHaveAttribute('data-pressed');
  });

  test('should call onOpenChange when menu is opened via right click', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await rightClick(page, page.getByTestId('trigger'));
    await expect(page.getByRole('menu')).toBeVisible();

    expect((await openChanges(fixture)).at(-1)).toBe('true:trigger-press');
    expect(await openEvents(fixture)).toEqual(['contextmenu']);
  });

  test('does not cancel opening menu on mouseup after mousedown outside before 500ms', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await withClock(page, async () => {
      await holdClock(page);
      await rightDown(page, page.getByTestId('trigger'));
      const ran = await awaitShown(page, mode, page.getByRole('menu'));
      await page.mouse.move(5, 5);
      await page.clock.runFor(499 - ran);

      expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
      await page.mouse.up({ button: 'right' });
      await page.clock.runFor(1);
    });
    await settle(fixture);

    expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    await expect(page.getByRole('menu')).toBeVisible();
  });

  test('cancels opening menu on mouseup after mousedown outside after 500ms', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await withClock(page, async () => {
      await holdClock(page);
      await rightDown(page, page.getByTestId('trigger'));
      const ran = await awaitShown(page, mode, page.getByRole('menu'));
      await page.mouse.move(5, 5);
      await page.clock.runFor(501 - ran);
      await page.mouse.up({ button: 'right' });
    });

    await expect(page.getByRole('menu')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:cancel-open']);
    expect(await openEvents(fixture)).toEqual(['contextmenu', 'mouseup']);
  });

  test('keeps the menu open when the context-menu gesture ends inside its positioner', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await withClock(page, async () => {
      await holdClock(page);
      await rightDown(page, page.getByTestId('trigger'));
      const ran = await awaitShown(page, mode, page.getByRole('menu'));
      const inside = await heldCenter(page.getByTestId('second'));
      await page.mouse.move(inside.x, inside.y);
      await page.clock.runFor(Math.max(0, 501 - ran));
      await page.mouse.up({ button: 'right' });
    });
    await settle(fixture);

    expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    await expect(page.getByRole('menu')).toBeVisible();
  });

  test('keeps the root menu open when the context-menu gesture ends in a portaled submenu', async ({ page, mode }) => {
    const fixture = await render(page, mode, { submenu: 'open' });
    await withClock(page, async () => {
      await holdClock(page);
      await rightDown(page, page.getByTestId('trigger'));
      const ran = await awaitShown(page, mode, page.getByTestId('submenu-popup'));
      const inside = await heldCenter(page.getByTestId('submenu-item'));
      await page.mouse.move(inside.x, inside.y, { steps: 4 });
      await page.clock.runFor(Math.max(0, 501 - ran));
      await page.mouse.up({ button: 'right' });
    });
    await settle(fixture);

    expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    await expect(page.getByTestId('submenu-popup')).toBeVisible();
  });

  test('aborts the pending document mouseup listener when the trigger unmounts', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await rightDown(page, page.getByTestId('trigger'));
    await expect(page.getByRole('menu')).toBeVisible();

    await pressControl(page.getByTestId('toggle-trigger'));
    await expect(page.getByTestId('trigger')).toHaveCount(0);
    await withClock(page, async () => {
      await page.clock.runFor(501);
      await page.mouse.move(5, 5);
      await page.mouse.up({ button: 'right' });
    });
    await settle(fixture);

    expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    await expect(page.getByRole('menu')).toBeVisible();
  });

  test.describe('prop: disabled', () => {
    test('does not open on right-click when disabled', async ({ page, mode }) => {
      const fixture = await render(page, mode, { disabled: true });
      await rightClick(page, page.getByTestId('trigger'));
      await settle(fixture);

      await expect(page.getByTestId('popup')).toHaveCount(0);
      expect(await openChanges(fixture)).toEqual([]);
    });

    test('does not block the native context menu when disabled', async ({ page, mode }) => {
      await render(page, mode, { disabled: true });

      expect(await dispatchContextMenu(page.getByTestId('trigger'))).toBe(false);
    });
  });

  test('blocks native context menus on both internal and external backdrops', async ({ page, mode }) => {
    await render(page, mode, { defaultOpen: true, backdrop: true });
    // Base UI's selector, `[data-base-ui-portal] > [data-base-ui-inert][role="presentation"]`, also matches the consumer
    // Backdrop once the modal focus manager marks it outside the popup; the internal one is the Positioner's previous sibling.
    const internalBackdrop = page.getByTestId('positioner').locator('xpath=preceding-sibling::*[1]');
    await expect(internalBackdrop).toHaveAttribute('role', 'presentation');
    await expect(internalBackdrop).toHaveAttribute('id', /-backdrop$/);
    await expect(page.getByTestId('backdrop')).toBeAttached();

    await expect.poll(() => dispatchContextMenu(internalBackdrop)).toBe(true);
    await expect.poll(() => dispatchContextMenu(page.getByTestId('backdrop'))).toBe(true);
    expect(await dispatchContextMenu(page.locator('body'))).toBe(false);
  });

  test('blocks native context menus in a portal mounted inside the trigger DOM subtree', async ({ page, mode }) => {
    await render(page, mode, { defaultOpen: true, container: true });
    const popup = page.getByTestId('popup');
    await expect(popup).toBeAttached();
    expect(await popup.evaluate((element) => !!element.closest('[data-testid="trigger"]'))).toBe(true);

    expect(await dispatchContextMenu(popup)).toBe(true);
  });

  test('blocks the native context menu when onContextMenu skips the Base UI handler', async ({ page, mode }) => {
    const fixture = await render(page, mode, { veto: true });

    expect(await dispatchContextMenu(page.getByTestId('trigger'))).toBe(true);
    await settle(fixture);
    expect(await openChanges(fixture)).toEqual([]);
    await expect(page.getByTestId('popup')).toHaveCount(0);
  });

  test.describe('long press', () => {
    test('should open menu on long press on touchscreen devices', async ({ page, mode }) => {
      const fixture = await render(page, mode);
      await withClock(page, async () => {
        await holdClock(page);
        await touch(page.getByTestId('trigger'), 'touchstart', [{ x: 100, y: 100 }]);
        await page.clock.runFor(500);
        await runUntil(page, () => page.getByRole('menu').isVisible());
      });

      await expect(page.getByRole('menu')).toBeVisible();
      expect(await openEvents(fixture)).toEqual(['touchstart']);
    });

    test('delays outside-press dismissal after opening from a long press', async ({ page, mode }) => {
      const fixture = await render(page, mode);
      await withClock(page, async () => {
        await holdClock(page);
        await touch(page.getByTestId('trigger'), 'touchstart', [{ x: 100, y: 100 }]);
        await page.clock.runFor(500);
        // The grace counts from the open the session applies, which the render brings.
        await runUntil(page, () => page.getByRole('menu').isVisible());

        await page.mouse.move(5, 5);
        await page.mouse.down();
        await page.mouse.up();
        await settle(fixture);
        await expect(page.getByRole('menu')).toBeVisible();

        await page.clock.runFor(500);
        await page.mouse.down();
        await page.mouse.up();
      });

      await expect(page.getByRole('menu')).toHaveCount(0);
      expect((await openChanges(fixture)).at(-1)).toBe('false:outside-press');
    });
  });

  test('should handle nested context menus correctly', async ({ page, mode }) => {
    const fixture = await render(page, mode, { case: 'nested' });

    await rightClick(page, page.getByTestId('inner-trigger'));
    await expect(page.getByTestId('inner-menu')).toBeVisible();
    await expect(page.getByTestId('outer-menu')).toHaveCount(0);

    await page.mouse.move(5, 5);
    await page.mouse.down();
    await page.mouse.up();
    await expect(page.getByTestId('inner-menu')).toHaveCount(0);

    await rightClick(page, page.getByTestId('outer-trigger'), { x: 300, y: 150 });
    await expect(page.getByTestId('outer-menu')).toBeVisible();
    await expect(page.getByTestId('inner-menu')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
  });
});
