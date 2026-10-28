import { type Browser } from '@playwright/test';
import { expect, fixtureUrl, test, type Page, type RenderMode } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { gotoPopupFixture, openChanges, settle, type PopupFixtureOptions } from './suites/popupFixture';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPositionerCase, positionerConformanceTests } from './suites/positioner-conformance';

// The [L4] rows of docs/components/tooltip.md (M4 track 6, issue 121) on the Tooltip fixtures, both render modes, Server
// first. Base UI's titles verbatim; port titles start with "port:". The shared suites run with Tooltip's options
// (TooltipRoot.test.tsx:27-40, TooltipRoot.detached-triggers.test.tsx:175-189).

popupConformanceTests({
  fixture: 'tooltip-conformance',
  triggerMouseAction: 'hover',
});

detachedTriggersConformanceTests({
  fixture: 'tooltip-triggers',
  openInteractions: ['hover', 'focus'],
  ariaExpanded: false,
  throwOnMissingTrigger: true,
  close: false,
  positioner: true,
  open: {
    // Run 40707988387 (172f72f), browser-server (2), reproduced at 4x CPU throttling: the root attaches its store to the
    // handle after its render (PSH-STR-03), so a trigger declared after it renders on the fallback store and its
    // `interactions` handle attaches under no session; the module keeps that binding when the session token arrives, and
    // the hover open closes at once. Open until PR 136 (track 5) lands with its fix of `Floating/InteractionsTriggerBehavior`.
    'registers a detached trigger declared after the root':
      'PSH-STR-03 with a late session token: open until PR 136 lands the session rebinding of InteractionsTriggerBehavior',
  },
});

// ---------------------------------------------------------------------------------------------------------------------
// TooltipRoot.test.tsx's `describe.for` trio (:79-1017) on `tooltip-root`: one root with a contained trigger, one detached
// trigger, and two detached triggers. Base UI dispatches the events under fake timers; here the real pointer and keyboard
// drive the trigger, and a title that needs frozen time pauses the page's clock and runs it forward (ADR 06 §2).
// ---------------------------------------------------------------------------------------------------------------------

const OPEN_DELAY = 600;

// The clock belongs to the worker's browser context, which every page of the worker shares: a title that paused it
// resumes it, or the next page's WebAssembly boot waits on its timers.
test.afterEach(async ({ page }) => {
  await page.clock.resume();
});

const frozenPages = new WeakSet<Page>();

const FREEZE_MARGIN = 50;

/**
 * Pauses the page's clock: its timers fire only when the test runs it forward. The page's clock runs on while the target
 * travels to the browser, so the target sits a margin ahead, read again if the clock passed it on a slow runner; the
 * titles freeze right after the fixture attached, when no timer a title watches is pending.
 */
async function freezeClock(page: Page): Promise<void> {
  for (let attempt = 1; ; attempt += 1) {
    const now = await page.evaluate(() => Date.now());
    try {
      await page.clock.pauseAt(now + FREEZE_MARGIN);
      break;
    } catch (error) {
      if (attempt === 3 || !String(error).includes('Cannot fast-forward to the past')) {
        throw error;
      }
    }
  }

  frozenPages.add(page);
}

/** Runs one frame of a paused clock: a closed popup with no exit animation leaves on the next frame (TIP-ANI-01). */
async function runFrame(page: Page): Promise<void> {
  await page.clock.runFor(16);
}

/**
 * Presses and releases the real pointer where it rests, a click with no move before it: Playwright's `click()` moves to the
 * element first, and that move restarts a pending rest timer, where Base UI's titles dispatch the click alone.
 */
async function clickInPlace(page: Page): Promise<void> {
  await page.mouse.down();
  await page.mouse.up();
}

/**
 * Moves the real pointer away from the trigger, below it, which leaves it from the side opposite a top popup. The safe
 * polygon decides on the leave only once the session holds the floating element and its placement; on Server they
 * arrive after C#'s render, and until then it waits for a later move. The first positioning pass removes the
 * positioner's `opacity: 0` and publishes the placement in one step (FLT-STA-08, FLT-ANI-02), after the root's session
 * update and the floating element's attach, so no open positioner may still carry it. Under a paused clock the frames
 * run first.
 */
async function moveAway(page: Page): Promise<void> {
  if (frozenPages.has(page)) {
    await page.clock.runFor(32);
  }

  await expect(page.locator('[role="presentation"][data-open][style*="opacity: 0"]')).toHaveCount(0);
  await page.mouse.move(400, 600);
}

const trio = [
  { name: 'contained triggers', case: 'contained' },
  { name: 'detached triggers', case: 'detached' },
  { name: 'multiple detached triggers', case: 'multiple' },
] as const;

for (const variant of trio) {
  test.describe(`when using ${variant.name}`, () => {
    const open = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) =>
      gotoPopupFixture(page, mode, 'tooltip-root', { case: variant.case, ...options });
    const trigger = (page: Page) => page.locator('[data-testid="trigger"]');
    const popup = (page: Page) => page.locator('[data-testid="popup"]');
    const positioner = (page: Page) => page.locator('[data-testid="positioner"]');

    test.describe('uncontrolled open', () => {
      test('should open when the trigger is hovered', async ({ page, mode }) => {
        await open(page, mode);
        await freezeClock(page);

        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);

        await expect(popup(page)).toHaveText('Content');
      });

      test('does not open when a touch pointer hovers the trigger', async ({ browser, mode }) => {
        await withTouchPage(browser, async (page) => {
          const fixture = await open(page, mode);
          await freezeClock(page);

          // A real tap: Chromium follows the touch with compatibility mouse events, which the hover reads as touch.
          const box = (await trigger(page).boundingBox())!;
          await page.touchscreen.tap(box.x + box.width / 2, box.y + box.height / 2);
          await page.clock.runFor(OPEN_DELAY);
          await settle(fixture);

          await expect(popup(page)).toHaveCount(0);
          await page.clock.resume();
        });
      });

      test('should close when the trigger is unhovered', async ({ page, mode }) => {
        const fixture = await open(page, mode);
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        await moveAway(page);

        await expect(popup(page)).toHaveCount(0);
        await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-hover false:trigger-hover');
      });

      test('should open when the trigger is focused', async ({ page, mode }) => {
        await open(page, mode);

        await trigger(page).focus();

        await expect(popup(page)).toHaveText('Content');
      });

      test('should close when the trigger is blurred', async ({ page, mode }) => {
        await open(page, mode);
        await trigger(page).focus();
        await expect(popup(page)).toBeVisible();

        await trigger(page).blur();

        await expect(popup(page)).toHaveCount(0);
      });
    });

    test.describe('controlled open', () => {
      test('should call onOpenChange when the open state changes', async ({ page, mode }) => {
        const fixture = await open(page, mode, { open: false, delay: 0 });
        await expect(popup(page)).toHaveCount(0);

        await trigger(page).hover();

        await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-hover');
        await expect(popup(page)).toBeVisible();
      });

      test('does not call onOpenChange again when the trigger is re-hovered while open', async ({ page, mode }) => {
        const fixture = await open(page, mode, { open: false });
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        // Hovering the already-open trigger again doesn't request another open.
        const box = (await trigger(page).boundingBox())!;
        await page.mouse.move(box.x + 2, box.y + 2);
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await page.clock.runFor(OPEN_DELAY);
        await settle(fixture);

        await expect(popup(page)).toBeVisible();
        expect(await openChanges(fixture)).toEqual(['true:trigger-hover']);
      });
    });

    test.describe('prop: defaultOpen', () => {
      test('closes when the trigger is unhovered after opening by default', async ({ page, mode }) => {
        await open(page, mode, { defaultOpen: true });
        await expect(popup(page)).toHaveText('Content');

        await trigger(page).hover();
        await moveAway(page);

        await expect(popup(page)).toHaveCount(0);
      });
    });

    test.describe('prop: delay', () => {
      test('should open after rest delay', async ({ page, mode }) => {
        const fixture = await open(page, mode, { delay: 100 });
        await freezeClock(page);

        await trigger(page).hover();
        await page.clock.runFor(99);
        await settle(fixture);
        await expect(popup(page)).toHaveCount(0);

        await page.clock.runFor(1);

        await expect(popup(page)).toHaveText('Content');
      });
    });

    test.describe('prop: closeDelay', () => {
      test('should close after delay', async ({ page, mode }) => {
        const fixture = await open(page, mode, { closeDelay: 100 });
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        await moveAway(page);
        await page.clock.runFor(99);
        await settle(fixture);
        await expect(popup(page)).toBeVisible();

        await page.clock.runFor(1);

        await expect(popup(page)).toHaveCount(0);
      });
    });

    test.describe('preventUnmountOnClose()', () => {
      test('does not prevent unmounting on later closes', async ({ page, mode }) => {
        const fixture = await open(page, mode, { delay: 0, preventUnmount: true });
        await trigger(page).hover();
        await expect(popup(page)).toBeVisible();

        await moveAway(page);
        await expect(trigger(page)).not.toHaveAttribute('data-popup-open');
        await settle(fixture);
        await expect(popup(page)).toHaveCount(1);
        await fixture.locator('[data-unmount]').evaluate((element) => (element as HTMLElement).click());
        await expect(popup(page)).toHaveCount(0);

        await trigger(page).hover();
        await expect(popup(page)).toBeVisible();
        await moveAway(page);

        await expect(popup(page)).toHaveCount(0);
      });
    });

    test.describe('prop: actionsRef', () => {
      test('unmounts the tooltip when the `unmount` method is called', async ({ page, mode }) => {
        const fixture = await open(page, mode, { delay: 0, preventUnmount: true });
        await trigger(page).hover();
        await expect(popup(page)).toBeVisible();
        await moveAway(page);
        await settle(fixture);
        await expect(popup(page)).toHaveCount(1);

        await fixture.locator('[data-unmount]').evaluate((element) => (element as HTMLElement).click());

        await expect(popup(page)).toHaveCount(0);
        await expect(fixture).toHaveAttribute('data-open-change-completes', /false$/);
      });

      test('closes the tooltip when the `close` method is called', async ({ page, mode }) => {
        const fixture = await open(page, mode, { defaultOpen: true });
        await expect(popup(page)).toBeVisible();
        await expect(trigger(page)).toHaveAttribute('data-popup-open');

        await fixture.locator('[data-close]').evaluate((element) => (element as HTMLElement).click());

        await expect(popup(page)).toHaveCount(0);
        await expect(trigger(page)).not.toHaveAttribute('data-popup-open');
        await expect(fixture).toHaveAttribute('data-open-changes', 'false:imperative-action');
      });
    });

    test.describe('prop: disabled', () => {
      test('should not open when disabled', async ({ page, mode }) => {
        const fixture = await open(page, mode, { disabled: true, delay: 0 });

        await trigger(page).hover();
        await trigger(page).focus();
        await settle(fixture);

        await expect(popup(page)).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual([]);
      });

      test('should not open on focus when the trigger is disabled', async ({ page, mode }) => {
        const fixture = await open(page, mode, { triggerDisabled: true, delay: 0 });

        await trigger(page).focus();
        await settle(fixture);

        await expect(popup(page)).toHaveCount(0);
      });

      test('should close if open when becoming disabled', async ({ page, mode }) => {
        const fixture = await open(page, mode, { defaultOpen: true, delay: 0 });
        await expect(popup(page)).toBeVisible();

        await fixture.locator('[data-toggle-disabled]').evaluate((element) => (element as HTMLElement).click());

        await expect(popup(page)).toHaveCount(0);
        await expect(fixture).toHaveAttribute('data-open-changes', 'false:disabled');
      });

      test('does not throw error when combined with defaultOpen', async ({ page, mode }) => {
        const fixture = await open(page, mode, { defaultOpen: true, disabled: true });
        await settle(fixture);

        await expect(popup(page)).toHaveCount(0);
      });

      test('marks the trigger as disabled when the root is disabled', async ({ page, mode }) => {
        await open(page, mode, { disabled: true });

        await expect(trigger(page)).toHaveAttribute('data-trigger-disabled', '');
      });

      test('keeps the tooltip disabled when the root is disabled and the trigger opts back in', async ({ page, mode }) => {
        const fixture = await open(page, mode, { disabled: true, triggerDisabled: false, delay: 0 });
        await expect(trigger(page)).not.toHaveAttribute('data-trigger-disabled');

        await trigger(page).hover();
        await settle(fixture);

        await expect(popup(page)).toHaveCount(0);
      });
    });

    test.describe('prop: disableHoverablePopup', () => {
      test('applies pointer-events: none to the positioner when `disableHoverablePopup = true`', async ({ page, mode }) => {
        await open(page, mode, { disableHoverablePopup: true, delay: 0 });

        await trigger(page).hover();

        await expect(positioner(page)).toHaveCSS('pointer-events', 'none');
        expect(await positioner(page).evaluate((element) => (element as HTMLElement).style.pointerEvents)).toBe('none');
      });

      test('does not apply pointer-events: none to the positioner when `disableHoverablePopup = false`', async ({ page, mode }) => {
        await open(page, mode, { delay: 0 });

        await trigger(page).hover();

        await expect(popup(page)).toBeVisible();
        await expect.poll(() => positioner(page).evaluate((element) => (element as HTMLElement).style.pointerEvents)).toBe('');
      });
    });

    test.describe('prop: trackCursorAxis', () => {
      test('makes the positioner inert when tracking both axes', async ({ page, mode }) => {
        await open(page, mode, { trackCursorAxis: 'both', delay: 0 });

        await trigger(page).hover();

        await expect(popup(page)).toBeVisible();
        await expect.poll(() => positioner(page).evaluate((element) => (element as HTMLElement).style.pointerEvents)).toBe('none');
      });

      test('keeps the positioner hoverable when tracking a single axis', async ({ page, mode }) => {
        await open(page, mode, { trackCursorAxis: 'x', delay: 0 });

        await trigger(page).hover();

        await expect(popup(page)).toBeVisible();
        await expect.poll(() => positioner(page).evaluate((element) => (element as HTMLElement).style.pointerEvents)).toBe('');
      });
    });

    test.describe('instant animations', () => {
      test('marks the popup as instant when opened by focus, but not when opened by hover', async ({ page, mode }) => {
        await open(page, mode, { delay: 0, closeDelay: 0 });

        await trigger(page).focus();
        await expect(popup(page)).toHaveAttribute('data-instant', 'focus');
        await trigger(page).blur();
        await expect(popup(page)).toHaveCount(0);

        await trigger(page).hover();

        await expect(popup(page)).toBeVisible();
        await expect(popup(page)).not.toHaveAttribute('data-instant');
      });
    });

    test.describe('BaseUIChangeEventDetails', () => {
      test('allowPropagation() prevents stopPropagation on Escape while still closing', async ({ page, mode }) => {
        await open(page, mode, { defaultOpen: true, delay: 0, allowPropagation: true });
        await expect(popup(page)).toHaveText('Content');
        await recordEscape(page);

        await page.keyboard.press('Escape');

        await expect(popup(page)).toHaveCount(0);
        // On Server the consumer's allowPropagation() arrives after the dispatch, so the fallback stops the innermost
        // popup's key (ADR 01 row "EVD-EVT-11, EVD-DIS-01, FLT-KEY-01"; TIP-EVT-10's relaxation).
        expect(await escapeReachedWindow(page)).toBe(mode === 'wasm');
      });
    });

    test.describe('dismissal', () => {
      test('should close when Escape is pressed', async ({ page, mode }) => {
        const fixture = await open(page, mode, { defaultOpen: true, delay: 0 });
        await expect(popup(page)).toHaveText('Content');

        await page.keyboard.press('Escape');

        await expect(popup(page)).toHaveCount(0);
        await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key');
      });

      test('should not open when the trigger was clicked before delay duration', async ({ page, mode }) => {
        const fixture = await open(page, mode);
        await freezeClock(page);

        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY / 2);
        await clickInPlace(page);
        await page.clock.runFor(OPEN_DELAY / 2);
        await settle(fixture);

        await expect(popup(page)).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual([]);
      });

      test('should not open when the trigger receives pointerdown before delay duration', async ({ page, mode }) => {
        const fixture = await open(page, mode);
        await freezeClock(page);

        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY / 2);
        await page.mouse.down();
        await page.clock.runFor(OPEN_DELAY / 2);
        await settle(fixture);
        await page.mouse.up();

        await expect(popup(page)).toHaveCount(0);
      });

      test('should open when the trigger was clicked before delay duration and closeOnClick is false', async ({ page, mode }) => {
        await open(page, mode, { closeOnClick: false });
        await freezeClock(page);

        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY / 2);
        await clickInPlace(page);
        await page.clock.runFor(OPEN_DELAY / 2);

        await expect(popup(page)).toHaveText('Content');
      });

      test('should close when the trigger is clicked after delay duration', async ({ page, mode }) => {
        const fixture = await open(page, mode);
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        await clickInPlace(page);
        // Base UI's jsdom removes the popup in the click's commit; a browser removes it on the next frame (TIP-ANI-01),
        // which the paused clock runs here.
        await runFrame(page);

        await expect(popup(page)).toHaveCount(0);
        // The pointerdown closes; on Server the click that follows reaches the module before the commit, as one during an
        // exit does in Base UI, so the same reason may be requested again.
        await expect(fixture).toHaveAttribute('data-open-changes', /^true:trigger-hover false:trigger-press/);
      });

      test('should not close when the trigger is clicked after delay duration and closeOnClick is false', async ({ page, mode }) => {
        const fixture = await open(page, mode, { closeOnClick: false });
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        await clickInPlace(page);
        await settle(fixture);

        await expect(popup(page)).toBeVisible();
      });

      test('reopens on hover after the trigger is clicked closed', async ({ page, mode }) => {
        // The clock runs: the title needs no frozen time, only the order of the hover, the press and the second hover.
        const fixture = await open(page, mode, { delay: 100 });
        await trigger(page).hover();
        await expect(popup(page)).toBeVisible();
        await clickInPlace(page);
        await expect(popup(page)).toHaveCount(0);
        // The press's pointerdown and click each request the close; on Server the second one's relay must arrive before
        // the next hover, whose pending open it would otherwise cancel (TIP-PTR-13's port note).
        await settle(fixture);

        await moveAway(page);
        await trigger(page).hover();

        await expect(popup(page)).toBeVisible();
      });
    });
  });
}

/**
 * Runs a title on a page of its own touch-enabled context, for a real touch input (`page.touchscreen`): the worker's
 * context has no touch. The clock is installed running, as on every page (ADR 06 §2).
 */
async function withTouchPage(browser: Browser, body: (page: Page) => Promise<void>): Promise<void> {
  const context = await browser.newContext({ hasTouch: true });
  try {
    const page = await context.newPage();
    await page.clock.install();
    await body(page);
  } finally {
    await context.close();
  }
}

/** Records whether the next Escape keydown reaches the window, from a listener the page adds last. */
async function recordEscape(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { escapeReached: boolean }).escapeReached = false;
    window.addEventListener('keydown', (event) => {
      if (event.key === 'Escape') {
        (window as unknown as { escapeReached: boolean }).escapeReached = true;
      }
    });
  });
}

function escapeReachedWindow(page: Page): Promise<boolean> {
  return page.evaluate(() => (window as unknown as { escapeReached: boolean }).escapeReached);
}

// ---------------------------------------------------------------------------------------------------------------------
// TooltipProvider.test.tsx and the two animation titles of TooltipRoot.test.tsx (:456-590) on `tooltip-provider`: the
// delay group is the `interactions` module's (ADR 04 Amendments 2026-10-27), keyed by the provider's token.
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<Tooltip.Provider />', () => {
  const open = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) => gotoPopupFixture(page, mode, 'tooltip-provider', options);
  const trigger = (page: Page, number: number) => page.locator(`[data-testid="trigger-${number}"]`);
  const popup = (page: Page, number: number) => page.locator(`[data-testid="popup-${number}"]`);

  test.describe('prop: delay', () => {
    test('waits for the delay before showing the tooltip', async ({ page, mode }) => {
      const fixture = await open(page, mode, { count: 1, delay: 10_000 });
      await freezeClock(page);

      await trigger(page, 1).hover();
      await page.clock.runFor(1_000);
      await settle(fixture);
      await expect(popup(page, 1)).toHaveCount(0);

      await page.clock.runFor(9_000);

      await expect(popup(page, 1)).toHaveText('Content One');
    });

    test('respects delay=0', async ({ page, mode }) => {
      await open(page, mode, { count: 1, delay: 0 });
      await freezeClock(page);

      await trigger(page, 1).hover();

      await expect(popup(page, 1)).toHaveText('Content One');
    });

    test('respects trigger delay prop over provider delay prop', async ({ page, mode }) => {
      const fixture = await open(page, mode, { count: 1, delay: 10, triggerDelay: 100 });
      await freezeClock(page);

      await trigger(page, 1).hover();
      await page.clock.runFor(99);
      await settle(fixture);
      await expect(popup(page, 1)).toHaveCount(0);

      await page.clock.runFor(1);

      await expect(popup(page, 1)).toBeVisible();
    });
  });

  test.describe('prop: closeDelay', () => {
    test('waits for the closeDelay before hiding the tooltip', async ({ page, mode }) => {
      const fixture = await open(page, mode, { count: 1, closeDelay: 400 });
      await freezeClock(page);
      await trigger(page, 1).hover();
      await page.clock.runFor(OPEN_DELAY);
      await expect(popup(page, 1)).toBeVisible();

      await moveAway(page);
      await page.clock.runFor(300);
      await settle(fixture);
      await expect(popup(page, 1)).toBeVisible();

      await page.clock.runFor(300);

      await expect(popup(page, 1)).toHaveCount(0);
    });

    test('uses the latest closeDelay after the prop updates', async ({ page, mode }) => {
      const fixture = await open(page, mode, { count: 1, closeDelay: 400 });
      await freezeClock(page);
      await trigger(page, 1).hover();
      await page.clock.runFor(OPEN_DELAY);
      await expect(popup(page, 1)).toBeVisible();

      await fixture.locator('[data-set-close-delay]').evaluate((element) => (element as HTMLElement).click());
      await settle(fixture);
      await moveAway(page);
      await page.clock.runFor(999);
      await settle(fixture);
      await expect(popup(page, 1)).toBeVisible();

      await page.clock.runFor(1);

      await expect(popup(page, 1)).toHaveCount(0);
    });
  });

  test.describe('prop: timeout', () => {
    test('opens an adjacent tooltip instantly while the group is active', async ({ page, mode }) => {
      await open(page, mode, { timeout: 400, delay: 100 });
      await freezeClock(page);
      await trigger(page, 1).hover();
      await page.clock.runFor(100);
      await expect(popup(page, 1)).toHaveText('Content One');

      await trigger(page, 2).hover();
      // Base UI's `tick(0)`; the first popup's removal is on the next frame (TIP-ANI-01).
      await runFrame(page);

      await expect(popup(page, 2)).toHaveText('Content Two');
      await expect(popup(page, 1)).toHaveCount(0);
    });

    test('respects a trigger delay over delay=0 outside the instant phase', async ({ page, mode }) => {
      const fixture = await open(page, mode, { timeout: 400, delay: 0, triggerDelay: 100 });
      await freezeClock(page);

      await trigger(page, 1).hover();
      await page.clock.runFor(99);
      await settle(fixture);
      await expect(popup(page, 1)).toHaveCount(0);
      await page.clock.runFor(1);
      await expect(popup(page, 1)).toBeVisible();

      await trigger(page, 2).hover();
      // Base UI's `tick(0)`: the first trigger's leave closes it through a zero-delay timer.
      await page.clock.runFor(0);
      await expect(popup(page, 2)).toBeVisible();
      await expect(popup(page, 1)).toHaveCount(0);

      await moveAway(page);
      await expect(popup(page, 2)).toHaveCount(0);
      await page.clock.runFor(400);
      await settle(fixture);

      await trigger(page, 1).hover();
      await page.clock.runFor(99);
      await settle(fixture);
      await expect(popup(page, 1)).toHaveCount(0);
      await page.clock.runFor(1);
      await expect(popup(page, 1)).toBeVisible();
    });

    test('requires the full delay again once the timeout elapses', async ({ page, mode }) => {
      const fixture = await open(page, mode, { timeout: 400, delay: 100 });
      await freezeClock(page);
      await trigger(page, 1).hover();
      await page.clock.runFor(100);
      await expect(popup(page, 1)).toBeVisible();

      await moveAway(page);
      await expect(popup(page, 1)).toHaveCount(0);
      await page.clock.runFor(400);
      await settle(fixture);
      await trigger(page, 2).hover();
      await settle(fixture);
      await expect(popup(page, 2)).toHaveCount(0);

      await page.clock.runFor(100);

      await expect(popup(page, 2)).toBeVisible();
    });
  });
});

test.describe('<Tooltip.Root /> animations', () => {
  const trigger = (page: Page, number: number) => page.locator(`[data-testid="trigger-${number}"]`);
  const popup = (page: Page, number: number) => page.locator(`[data-testid="popup-${number}"]`);

  test('toggles instant animations for adjacent tooltips only while opening', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'tooltip-provider', { css: 'instant', triggerDelay: 0, animations: 1 });

    await trigger(page, 1).hover();
    await expect(popup(page, 1)).toBeVisible();
    await expect(popup(page, 1)).not.toHaveAttribute('data-instant');

    await moveAway(page);
    await trigger(page, 2).hover();

    await expect(popup(page, 2)).toHaveAttribute('data-instant', 'delay');
    expect(await popup(page, 2).evaluate((element) => element.getAnimations().length)).toBe(0);
    // Closing from the starting style would exit from `opacity: 0` and never start a transition.
    await expect(popup(page, 2)).not.toHaveAttribute('data-starting-style');

    await moveAway(page);

    await expect(popup(page, 2)).toHaveAttribute('data-ending-style', '');
    await expect(popup(page, 2)).not.toHaveAttribute('data-instant');
    expect(await popup(page, 2).evaluate((element) => element.getAnimations().length)).toBe(1);
  });

  test('unmounts an exiting tooltip when another tooltip opens', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'tooltip-provider', { css: 'exit', triggerDelay: 0, timeout: 30_000, animations: 1 });

    await trigger(page, 1).hover();
    await expect(popup(page, 1)).toBeVisible();
    await moveAway(page);
    await expect.poll(() => popup(page, 1).evaluate((element) => element.getAnimations().length)).toBe(1);

    await trigger(page, 2).hover();
    await expect(popup(page, 2)).toBeVisible();

    await expect(popup(page, 1)).toHaveCount(0);
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// Nested tooltips (TooltipRoot.test.tsx:1392-2700) on `tooltip-nested`: the nested-trigger detection is the
// `interactions` module's (`Hover.NestedTooltipTriggers`, ADR 04 Amendments 2026-10-18 item 4), reached here with the real
// pointer and keyboard.
// ---------------------------------------------------------------------------------------------------------------------

test.describe('nested tooltips', () => {
  const open = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) => gotoPopupFixture(page, mode, 'tooltip-nested', options);
  const byTestId = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`);

  /** Moves the real pointer to the center of an element in steps, so `mouseover`, `mouseenter` and `mousemove` fire. */
  async function moveTo(page: Page, id: string): Promise<void> {
    const box = (await byTestId(page, id).boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 4 });
  }

  test('should not open the outer tooltip when hovering over a nested tooltip trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode);
    await freezeClock(page);

    await moveTo(page, 'inner-trigger');
    await page.clock.runFor(OPEN_DELAY);

    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should not open the outer tooltip when moving between sibling nested triggers', async ({ page, mode }) => {
    const fixture = await open(page, mode, { siblings: true });
    await freezeClock(page);

    await moveTo(page, 'inner-trigger');
    await page.clock.runFor(OPEN_DELAY);
    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await moveTo(page, 'inner-trigger-2');
    await page.clock.runFor(OPEN_DELAY);

    await expect(byTestId(page, 'inner-popup-2')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should not open ancestor tooltips when hovering over a third-level nested trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { depth: 3 });
    await freezeClock(page);

    await moveTo(page, 'deep-trigger');
    await page.clock.runFor(OPEN_DELAY);

    await expect(byTestId(page, 'deep-popup')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'inner-popup')).toHaveCount(0);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should open the outer tooltip when moving from a nested trigger to the parent area with zero delay', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 0 });
    await moveTo(page, 'inner-trigger');
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);

    await moveTo(page, 'outer-area');

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
  });

  test('should not open a disabled outer tooltip when moving from a nested trigger to the parent area with zero delay', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 0, outerDisabled: true });
    await moveTo(page, 'inner-trigger');

    await moveTo(page, 'outer-area');
    await settle(fixture);

    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should reopen the delayed outer tooltip when moving from a nested trigger to the parent area', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 100 });
    await freezeClock(page);
    await moveTo(page, 'inner-trigger');
    await page.clock.runFor(100);
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);

    await moveTo(page, 'outer-area');
    await page.clock.runFor(99);
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
    await page.clock.runFor(1);

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
  });

  test('should cancel the pending parent reopen when the pointer leaves the parent trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 100 });
    await freezeClock(page);
    await moveTo(page, 'inner-trigger');
    await moveTo(page, 'outer-area');

    await moveAway(page);
    await page.clock.runFor(200);
    await settle(fixture);

    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should not reopen the outer tooltip when rapidly moving back to a nested trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 100 });
    await freezeClock(page);
    await moveTo(page, 'inner-trigger');
    await moveTo(page, 'outer-area');

    await moveTo(page, 'inner-trigger');
    await page.clock.runFor(200);
    await settle(fixture);

    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should close the outer tooltip when the pointer moves from outer area onto a nested trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 0 });
    await moveTo(page, 'outer-area');
    await expect(byTestId(page, 'outer-popup')).toBeVisible();

    await moveTo(page, 'inner-trigger');

    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-hover false:trigger-hover');
  });

  test('should keep a focus-opened outer tooltip open when hovering over a nested trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { detached: false });
    // The outer trigger is a `span`, which takes no focus by itself (TIP §16 Q10); the inner trigger's focus does not open
    // the outer one (TIP-FOC-05), so the outer is focused through a tabindex the page gives it.
    await byTestId(page, 'outer-trigger').evaluate((element) => element.setAttribute('tabindex', '0'));
    await byTestId(page, 'outer-trigger').focus();
    await expect(byTestId(page, 'outer-popup')).toBeVisible();

    await moveTo(page, 'inner-trigger');
    await settle(fixture);

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
    expect(await openChanges(fixture)).toEqual(['true:trigger-focus']);
  });

  test('should keep a controlled-open outer tooltip open when hovering over a nested trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerOpen: true });
    await expect(byTestId(page, 'outer-popup')).toBeVisible();

    await moveTo(page, 'inner-trigger');
    await settle(fixture);

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
    expect(await openChanges(fixture)).toEqual([]);
  });

  test('should not open the outer tooltip when focusing a nested tooltip trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode);

    await byTestId(page, 'inner-trigger').focus();

    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should close a focus-opened inner tooltip when the inner trigger loses focus', async ({ page, mode }) => {
    const fixture = await open(page, mode);
    await byTestId(page, 'inner-trigger').focus();
    await expect(byTestId(page, 'inner-popup')).toBeVisible();

    await byTestId(page, 'inner-trigger').blur();

    await expect(byTestId(page, 'inner-popup')).toHaveCount(0);
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should allow the parent tooltip to open when a nested trigger is disabled', async ({ page, mode }) => {
    await open(page, mode, { outerDelay: 0, innerDisabled: true });
    await expect(byTestId(page, 'inner-trigger')).not.toHaveAttribute('data-base-ui-tooltip-trigger');

    await moveTo(page, 'inner-trigger');

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
  });

  test('should open the outer tooltip when hovering over the non-nested area', async ({ page, mode }) => {
    await open(page, mode);
    await freezeClock(page);

    await moveTo(page, 'outer-area');
    await page.clock.runFor(OPEN_DELAY);

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
  });

  test('should not open the outer tooltip when the pointer moves onto a nested trigger before the delay expires', async ({ page, mode }) => {
    const fixture = await open(page, mode);
    await freezeClock(page);
    await moveTo(page, 'outer-area');
    await page.clock.runFor(OPEN_DELAY / 2);

    await moveTo(page, 'inner-trigger');
    await page.clock.runFor(OPEN_DELAY);

    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should support nested triggers with a Provider delay={0}', async ({ page, mode }) => {
    const fixture = await open(page, mode, { provider: true });

    await moveTo(page, 'inner-trigger');

    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
  });

  test('should not reopen the outer tooltip via the local reopen path for touch pointers', async ({ browser, mode }) => {
    await withTouchPage(browser, async (page) => {
      const fixture = await open(page, mode, { outerDelay: 0 });

      const inner = (await byTestId(page, 'inner-trigger').boundingBox())!;
      await page.touchscreen.tap(inner.x + inner.width / 2, inner.y + inner.height / 2);
      const area = (await byTestId(page, 'outer-area').boundingBox())!;
      await page.touchscreen.tap(area.x + area.width / 2, area.y + area.height / 2);
      await settle(fixture);

      await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
    });
  });

  test('should allow mouse hover after leaving a touch interaction', async ({ browser, mode }) => {
    await withTouchPage(browser, async (page) => {
      await open(page, mode, { outerDelay: 0 });
      const area = (await byTestId(page, 'outer-area').boundingBox())!;
      await page.touchscreen.tap(area.x + area.width / 2, area.y + area.height / 2);
      await page.mouse.move(400, 600);

      await moveTo(page, 'inner-trigger');
      await moveTo(page, 'outer-area');

      await expect(byTestId(page, 'outer-popup')).toBeVisible();
    });
  });

  test('should support nested triggers inside a detached parent trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { detached: true, outerDelay: 0 });

    await moveTo(page, 'inner-trigger');
    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);

    await moveTo(page, 'outer-area');

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
  });

  test('should restart the parent delay when moving from a nested trigger to the parent area', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 100, innerDelay: 1000 });
    await freezeClock(page);
    await moveTo(page, 'outer-area');
    await page.clock.runFor(50);

    await moveTo(page, 'inner-trigger');
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);

    await moveTo(page, 'outer-area');
    await page.clock.runFor(50);
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
    await page.clock.runFor(50);

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
  });

  // Listed open (TIP-PTR-20, tooltip.md's dated note 2026-10-28): the real pointer reaching the parent area also moves
  // over it, which starts the hover hook's rest timer (useHoverReferenceInteraction.ts:426-497; Tooltip's `restMs` is the
  // open delay). That timer's open is not gated by the open state while the popup has no active trigger
  // (`isOverInactiveTrigger` is true for no reference), so Base UI's own code announces the outside-opened tooltip again
  // in a browser; Base UI's title fires no `mousemove` on the parent area. The local reopen's own `!open` guard has no
  // title: an L3 one would pass against `main`, which ADR 11 §3's red check refuses.
  test('should not re-announce an open outer tooltip when the pending reopen fires', async ({ page, mode }) => {
    test.skip(true, 'TIP-PTR-20: the real move over the parent area starts the hover rest timer, which Base UI does not gate');
    const fixture = await open(page, mode, { providerDelay: 100, innerDelay: 1000, outerControlled: true });
    await freezeClock(page);
    await moveTo(page, 'inner-trigger');
    await moveTo(page, 'outer-area');

    await fixture.locator('[data-open-outer]').evaluate((element) => (element as HTMLElement).click());
    await expect(byTestId(page, 'outer-popup')).toBeVisible();
    await page.clock.runFor(100);
    await settle(fixture);

    await expect(byTestId(page, 'outer-popup')).toBeVisible();
    expect(await openChanges(fixture)).toEqual([]);
  });

  // React bubbles the nested popup's mouseover to the outer trigger through the portal; the DOM does not (tooltip.md §16,
  // "Portals"), so the title holds through the real pointer's path from the inner trigger to its popup.
  test('should not reopen the outer tooltip when hovering the nested tooltip popup', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 100, innerDelay: 0 });
    await moveTo(page, 'inner-trigger');
    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);

    await moveTo(page, 'inner-popup');
    await page.waitForTimeout(100);
    await settle(fixture);

    await expect(byTestId(page, 'inner-popup')).toBeVisible();
    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual([]);
  });

  test('should not open the outer tooltip when moving from the outer popup to a nested trigger', async ({ page, mode }) => {
    const fixture = await open(page, mode, { outerDelay: 0, innerDelay: OPEN_DELAY * 10 });
    await moveTo(page, 'outer-area');
    await expect(byTestId(page, 'outer-popup')).toBeVisible();
    await moveTo(page, 'outer-popup');
    await settle(fixture);
    await expect(byTestId(page, 'outer-popup')).toBeVisible();

    await moveTo(page, 'inner-trigger');

    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-hover false:trigger-hover');
  });

  test('should suppress the safePolygon-driven open while a nested trigger is hovered', async ({ page, mode }) => {
    const fixture = await open(page, mode, { innerDelay: OPEN_DELAY * 10 });
    await freezeClock(page);
    await moveTo(page, 'inner-trigger');
    const box = (await byTestId(page, 'inner-trigger').boundingBox())!;
    await page.mouse.move(box.x + box.width / 2 + 1, box.y + box.height / 2);
    await page.mouse.move(box.x + box.width / 2 + 2, box.y + box.height / 2);

    await page.clock.runFor(OPEN_DELAY);
    await settle(fixture);

    await expect(byTestId(page, 'outer-popup')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual([]);
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// The titles that measure or move between sized elements (TooltipRoot.test.tsx:42-77, 592-638, 1104-1390) on
// `tooltip-layout`. Cursor anchoring is the `interactions` module's virtual anchor, which `positioning` follows through
// the session (FLT-STA-08, PR 135).
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<Tooltip.Root /> layout', () => {
  const open = (page: Page, mode: RenderMode, options: PopupFixtureOptions) => gotoPopupFixture(page, mode, 'tooltip-layout', options);
  const byTestId = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`);

  /** The element's horizontal center, or `NaN` while it is not rendered, so an `expect.poll` over it retries. */
  async function centerX(page: Page, id: string): Promise<number> {
    const box = await byTestId(page, id).boundingBox();
    return box === null ? Number.NaN : box.x + box.width / 2;
  }

  test('does not open once the hovered trigger has unmounted', async ({ page, mode }) => {
    const fixture = await open(page, mode, { case: 'unmount' });
    await freezeClock(page);
    await byTestId(page, 'trigger').hover();
    await page.clock.runFor(1);

    await fixture.locator('[data-toggle-trigger]').evaluate((element) => (element as HTMLElement).click());
    await expect(byTestId(page, 'trigger')).toHaveCount(0);
    await page.clock.runFor(OPEN_DELAY);
    await settle(fixture);

    await expect(byTestId(page, 'popup')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual([]);
  });

  test('tracks the cursor on the first delayed hover when trackCursorAxis is x', async ({ page, mode }) => {
    await open(page, mode, { case: 'cursor' });
    const box = (await byTestId(page, 'trigger').boundingBox())!;
    const cursorX = box.x + 240;

    await page.mouse.move(cursorX, box.y + 20, { steps: 4 });

    await expect(byTestId(page, 'positioner')).toBeVisible();
    await expect.poll(async () => Math.abs((await centerX(page, 'positioner')) - cursorX)).toBeLessThanOrEqual(2);
    await expect(byTestId(page, 'positioner')).toHaveAttribute('data-instant', 'tracking-cursor');
  });

  test('stops tracking the cursor after trackCursorAxis is disabled while closed', async ({ page, mode }) => {
    const fixture = await open(page, mode, { case: 'cursor' });
    const box = (await byTestId(page, 'trigger').boundingBox())!;
    const cursorX = box.x + 240;
    await page.mouse.move(cursorX, box.y + 20, { steps: 4 });
    await expect.poll(async () => Math.abs((await centerX(page, 'positioner')) - cursorX)).toBeLessThanOrEqual(2);
    await page.mouse.move(cursorX, box.y - 60, { steps: 2 });
    await expect(byTestId(page, 'positioner')).toHaveCount(0);

    await fixture.locator('[data-disable-tracking]').evaluate((element) => (element as HTMLElement).click());
    await settle(fixture);
    await page.mouse.move(cursorX, box.y + 20, { steps: 4 });

    await expect(byTestId(page, 'positioner')).toBeVisible();
    await expect.poll(async () => Math.abs((await centerX(page, 'positioner')) - (box.x + box.width / 2))).toBeLessThanOrEqual(2);
  });

  test('updates the tracked cursor position after closing and reopening', async ({ page, mode }) => {
    await open(page, mode, { case: 'cursor' });
    const box = (await byTestId(page, 'trigger').boundingBox())!;
    const firstCursorX = box.x + 240;
    const secondCursorX = box.x + 60;
    await page.mouse.move(firstCursorX, box.y + 20, { steps: 4 });
    await expect.poll(async () => Math.abs((await centerX(page, 'positioner')) - firstCursorX)).toBeLessThanOrEqual(2);
    await page.mouse.move(firstCursorX, box.y - 60, { steps: 2 });
    await expect(byTestId(page, 'positioner')).toHaveCount(0);

    await page.mouse.move(secondCursorX, box.y + 20, { steps: 4 });

    await expect(byTestId(page, 'positioner')).toBeVisible();
    await expect.poll(async () => Math.abs((await centerX(page, 'positioner')) - secondCursorX)).toBeLessThanOrEqual(2);
  });

  test.describe('hoverable popup', () => {
    test('survives the trip from the trigger to the popup, then closes after leaving it', async ({ page, mode }) => {
      const fixture = await open(page, mode, { case: 'hoverable' });
      await byTestId(page, 'trigger').hover();
      await expect(byTestId(page, 'popup')).toBeVisible();

      await byTestId(page, 'popup').hover();
      await settle(fixture);
      await expect(byTestId(page, 'popup')).toBeVisible();

      await byTestId(page, 'outside').hover();

      await expect(byTestId(page, 'popup')).toHaveCount(0);
    });

    test('closes on that same trip when `disableHoverablePopup` is set', async ({ page, mode }) => {
      await open(page, mode, { case: 'hoverable', disableHoverablePopup: true });
      await byTestId(page, 'trigger').hover();
      await expect(byTestId(page, 'popup')).toBeVisible();

      const popupBox = (await byTestId(page, 'popup').boundingBox())!;
      await page.mouse.move(popupBox.x + popupBox.width / 2, popupBox.y + popupBox.height / 2, { steps: 4 });

      await expect(byTestId(page, 'popup')).toHaveCount(0);
    });
  });

  test('keeps the tooltip open when moving across spaced triggers without a closeDelay', async ({ page, mode }) => {
    const fixture = await open(page, mode, { case: 'spaced' });
    await byTestId(page, 'trigger-1').hover();
    await expect(byTestId(page, 'popup')).toBeVisible();

    await byTestId(page, 'trigger-2').hover();
    await expect(byTestId(page, 'popup')).toBeVisible();
    await byTestId(page, 'trigger-3').hover();
    await expect(byTestId(page, 'popup')).toBeVisible();

    // Base UI dispatches one mousemove on the body; here the pointer moves within the third trigger, which the safe polygon
    // left running from the second one must not read as a leave.
    const box = (await byTestId(page, 'trigger-3').boundingBox())!;
    await page.mouse.move(box.x + 2, box.y + 2, { steps: 3 });
    await settle(fixture);

    await expect(byTestId(page, 'popup')).toBeVisible();
    expect((await openChanges(fixture)).filter((entry) => entry.startsWith('false'))).toEqual([]);
  });

  test('inline opacity: 0 is removed before user CSS transitions run', async ({ page, mode }) => {
    await open(page, mode, { case: 'opacity', animations: 1 });

    await byTestId(page, 'trigger').hover();

    await expect(byTestId(page, 'popup')).toBeVisible();
    await expect
      .poll(() =>
        byTestId(page, 'popup').evaluate((element) => ({
          opacity: Number(getComputedStyle(element).opacity),
          opacityAnimationCount: element.getAnimations().filter((animation) => (animation as CSSTransition).transitionProperty === 'opacity').length,
        })),
      )
      .toEqual({ opacity: 1, opacityAnimationCount: 0 });
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// The hand-off titles of TooltipRoot.detached-triggers.test.tsx (:308-341, :408-456, :574-613, :665-780) on
// `tooltip-handoff`, with the real pointer and keyboard.
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<Tooltip.Root /> hand-off', () => {
  const open = (page: Page, mode: RenderMode, options: PopupFixtureOptions) => gotoPopupFixture(page, mode, 'tooltip-handoff', options);
  const byTestId = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`);

  test('closes a cursor-tracking tooltip when an inactive trigger is pressed', async ({ browser, mode }) => {
    await withTouchPage(browser, async (page) => {
      const fixture = await open(page, mode, { case: 'pressed' });
      await fixture.locator('[data-handle-open]').evaluate((element) => (element as HTMLElement).click());
      await expect(byTestId(page, 'popup')).toBeVisible();

      // A touch press, as Base UI's: no hover moves the tooltip to the pressed trigger first.
      const box = (await byTestId(page, 'trigger-2').boundingBox())!;
      await page.touchscreen.tap(box.x + box.width / 2, box.y + box.height / 2);

      await expect(byTestId(page, 'popup')).toHaveCount(0);
      await expect(fixture).toHaveAttribute('data-open-changes', /^true:imperative-action false:trigger-press/);
    });
  });

  test('hands off open state and payload to a trigger with its own DOM id while open', async ({ page, mode }) => {
    await open(page, mode, { case: 'own-id' });
    await expect(byTestId(page, 'trigger-2')).toHaveAttribute('id', 'custom-button');

    await byTestId(page, 'trigger-1').focus();
    await expect(byTestId(page, 'content')).toHaveText('1');
    await byTestId(page, 'trigger-2').focus();

    await expect(byTestId(page, 'trigger-2')).toHaveAttribute('data-popup-open', '');
    await expect(byTestId(page, 'content')).toHaveText('2');
  });

  test('should close when focusing a disabled trigger while another trigger is open', async ({ page, mode }) => {
    await open(page, mode, { case: 'disabled' });
    await byTestId(page, 'trigger-1').focus();
    await expect(byTestId(page, 'content')).toHaveText('1');

    await byTestId(page, 'trigger-2').focus();

    await expect(byTestId(page, 'content')).toHaveCount(0);
    await expect(byTestId(page, 'trigger-2')).not.toHaveAttribute('data-popup-open');
  });

  test('should close when hovering a disabled trigger while another trigger is open', async ({ page, mode }) => {
    await open(page, mode, { case: 'disabled' });
    await byTestId(page, 'trigger-1').hover();
    await expect(byTestId(page, 'content')).toHaveText('1');

    await byTestId(page, 'trigger-2').hover();

    await expect(byTestId(page, 'content')).toHaveCount(0);
    await expect(byTestId(page, 'trigger-2')).not.toHaveAttribute('data-popup-open');
  });

  test('should switch to a rendered disabled button trigger when trigger hover is enabled', async ({ page, mode }) => {
    await open(page, mode, { case: 'native-disabled' });
    await byTestId(page, 'trigger-1').hover();
    await expect(byTestId(page, 'content')).toHaveText('1');

    // A natively disabled button still receives the pointer's enter and move events (TIP-PTR-25).
    const box = (await byTestId(page, 'trigger-2').boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 4 });

    await expect(byTestId(page, 'content')).toHaveText('2');
    await expect(byTestId(page, 'trigger-2')).toHaveAttribute('data-popup-open', '');
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// TooltipPositioner.test.tsx's positionerConformanceTests (:55-75) and TooltipArrow.test.tsx (:20-60) on
// `tooltip-positioner`. Base UI runs the suite with `viewport: true`; its three Viewport titles wait for the Viewport
// (track 5's `popup` module, design point 13.2): TIP-POS-05.
// ---------------------------------------------------------------------------------------------------------------------

positionerConformanceTests({ fixture: 'tooltip-positioner' });

test.describe('<Tooltip.Arrow />', () => {
  const arrow = (page: Page) => page.locator('[data-testid="arrow"]');

  test('is hidden from assistive technology and mirrors the resolved side', async ({ page, mode }) => {
    await gotoPositionerCase(page, mode, 'tooltip-positioner', 'arrow');

    await expect(arrow(page)).toHaveAttribute('aria-hidden', 'true');
    await expect(arrow(page)).toHaveAttribute('data-side', 'bottom');
    await expect(arrow(page)).toHaveAttribute('data-open', '');
  });

  test('is marked uncentered when it cannot point at the anchor', async ({ page, mode }) => {
    await gotoPositionerCase(page, mode, 'tooltip-positioner', 'arrow-uncentered');

    await expect(arrow(page)).toHaveAttribute('data-uncentered', '');
  });

  test('is not marked uncentered when it can point at the anchor', async ({ page, mode }) => {
    await gotoPositionerCase(page, mode, 'tooltip-positioner', 'arrow-centered');

    await expect(arrow(page)).toHaveAttribute('data-side', 'bottom');
    await expect(arrow(page)).not.toHaveAttribute('data-uncentered');
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// TooltipTrigger.test.tsx's Toolbar title (:107-132) on `tooltip-toolbar`, and the attach order of the two modules'
// handles on that one element, pinned by a title that stalls one module's load (the standing rule; issue 133's
// fingerprinted route).
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<Tooltip.Trigger /> in a toolbar', () => {
  const trigger = (page: Page) => page.locator('[data-testid="trigger"]');
  const popup = (page: Page) => page.locator('[data-testid="popup"]');

  test('opens on delayed hover when rendered as a disabled toolbar button', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'tooltip-toolbar');
    await expect(trigger(page)).not.toHaveAttribute('disabled');
    await expect(trigger(page)).toHaveAttribute('aria-disabled', 'true');

    await trigger(page).hover();

    await expect(popup(page)).toBeVisible();
  });

  for (const stalled of ['interactions', 'button'] as const) {
    test(`port: the disabled toolbar button trigger opens on delayed hover when the ${stalled} module loads last`, async ({ page, mode }) => {
      let hit = false;
      await page.route(new RegExp(`/js/${stalled}(\\.[0-9a-z]+)?\\.js`), (route) => {
        hit = true;
        setTimeout(() => void route.continue(), 1000);
      });

      await gotoPopupFixture(page, mode, 'tooltip-toolbar');
      expect(hit).toBe(true);
      await expect(trigger(page)).toHaveAttribute('aria-disabled', 'true');
      await expect(trigger(page)).toHaveAttribute('data-base-ui-tooltip-trigger', '');

      await trigger(page).hover();

      await expect(popup(page)).toBeVisible();
    });
  }
});

// ---------------------------------------------------------------------------------------------------------------------
// TIP-EDGE-12 restated for Blazor (sheet D §8: Base UI's hydration titles are React SSR's) on the prerendered
// `tooltip-prerender`: the static markup's trigger shows no data-popup-open, as Base UI's server markup reads the fallback
// store; once interactive the trigger registers after the root's first render and the default-open root stays open.
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<Tooltip.Root /> prerendered', () => {
  test("port: keeps a default-open root open until its detached trigger registers, and the prerendered trigger shows no data-popup-open", async ({ page, mode }) => {
    const response = await page.request.get(fixtureUrl(mode, 'tooltip-prerender'));
    await page.setContent((await response.text()).replace(/<script[\s\S]*?<\/script>/g, ''));
    await expect(page.locator('[data-testid="trigger-1"]')).toHaveCount(1);
    await expect(page.locator('[data-testid="trigger-1"]')).not.toHaveAttribute('data-popup-open');

    const fixture = await gotoPopupFixture(page, mode, 'tooltip-prerender');

    await expect(page.locator('[data-testid="trigger-1"]')).toHaveAttribute('data-popup-open', '');
    await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
    await expect(page.locator('[data-testid="popup"]')).toBeVisible();
    expect(await openChanges(fixture)).toEqual([]);
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// The Server relaxations of tooltip.md §9 (ADR 01's relaxation table), asserted per mode with latency added to the
// circuit's WebSocket (`page.routeWebSocket`; CDP emulation does not delay it). Each witness is a record the page takes
// (a MutationObserver, a listener added last), never a poll of a short-lived state (ADR 06 §2).
// ---------------------------------------------------------------------------------------------------------------------

/** Delays every circuit message by `oneWayMs` each way (Server latency). */
async function delayCircuit(page: Page, oneWayMs: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), oneWayMs);
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), oneWayMs);
    });
  });
}

const LATENCY = 250;

test.describe('Server relaxations (tooltip.md §9)', () => {
  const byTestId = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`);

  // TIP-STA-06/07, TIP-ANI-03 (chosen by the user 2026-10-27, ADR 04 Amendments 2026-10-27): the group's phase travels on
  // the open request, so the popup is created with data-instant="delay" in the batch that renders data-open, on both modes.
  test('port: data-instant="delay" renders in the batch of data-open on both modes', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, LATENCY);
    }

    await gotoPopupFixture(page, mode, 'tooltip-provider', { triggerDelay: 0, timeout: 400 });
    await page.evaluate(() => {
      const records: { instant: string | null; open: boolean }[] = [];
      (window as unknown as { popupRecords: typeof records }).popupRecords = records;
      new MutationObserver((mutations) => {
        for (const mutation of mutations) {
          for (const node of mutation.addedNodes) {
            const popup = node instanceof HTMLElement ? (node.matches('[data-testid="popup-2"]') ? node : node.querySelector('[data-testid="popup-2"]')) : null;
            if (popup) {
              records.push({ instant: popup.getAttribute('data-instant'), open: popup.hasAttribute('data-open') });
            }
          }
        }
      }).observe(document.body, { childList: true, subtree: true });
    });
    await byTestId(page, 'trigger-1').hover();
    await expect(byTestId(page, 'popup-1')).toBeVisible();

    await byTestId(page, 'trigger-2').hover();

    await expect(byTestId(page, 'popup-2')).toBeVisible();
    const records = await page.evaluate(() => (window as unknown as { popupRecords: { instant: string | null; open: boolean }[] }).popupRecords);
    expect(records[0]).toEqual({ instant: 'delay', open: true });
  });

  // TIP-PTR-03, TIP-EVT-02 (EVD-EVT-18, PSH-ANI-03): the hover open is decided in the module and commits a round trip
  // later on Server; on WebAssembly it commits with no round trip.
  test('port: a hover open commits a round trip after the hover on Server and with none on WebAssembly', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, LATENCY);
    }

    await gotoPopupFixture(page, mode, 'tooltip-root', { case: 'contained', delay: 0 });
    await page.evaluate(() => {
      const times: { enter?: number; open?: number } = {};
      (window as unknown as { hoverTimes: typeof times }).hoverTimes = times;
      document.querySelector('[data-testid="trigger"]')!.addEventListener('mouseenter', () => {
        times.enter ??= performance.now();
      });
      new MutationObserver(() => {
        if (times.open === undefined && document.querySelector('[data-testid="popup"]')) {
          times.open = performance.now();
        }
      }).observe(document.body, { childList: true, subtree: true });
    });

    await byTestId(page, 'trigger').hover();

    await expect(byTestId(page, 'popup')).toBeVisible();
    const { enter, open } = await page.evaluate(() => (window as unknown as { hoverTimes: { enter: number; open: number } }).hoverTimes);
    if (mode === 'server') {
      expect(open - enter).toBeGreaterThanOrEqual(2 * LATENCY - 20);
    } else {
      expect(open - enter).toBeLessThan(2 * LATENCY - 20);
    }
  });

  // TIP-STA-12, TIP-STA-13, TIP-POS-07 (RND-STA-01 for mirrored fields): the module writes data-side; the side C# mirrors
  // (the positioner's class) follows a round trip later on Server, and within the module's report on WebAssembly.
  test('port: the mirrored side follows the data-side the module writes', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, LATENCY);
    }

    await gotoPopupFixture(page, mode, 'tooltip-root', { case: 'contained', delay: 0, topEdge: true });
    await page.evaluate(() => {
      const records: { side: string | null; className: string }[] = [];
      (window as unknown as { sideRecords: typeof records }).sideRecords = records;
      new MutationObserver(() => {
        const positioner = document.querySelector('[data-testid="positioner"]');
        if (positioner) {
          records.push({ side: positioner.getAttribute('data-side'), className: positioner.className });
        }
      }).observe(document.body, { attributes: true, childList: true, subtree: true, attributeFilter: ['data-side', 'class'] });
    });

    await byTestId(page, 'trigger').hover();

    await expect(byTestId(page, 'positioner')).toHaveAttribute('data-side', 'bottom');
    await expect(byTestId(page, 'positioner')).toHaveClass('mirrored-bottom');
    const records = await page.evaluate(() => (window as unknown as { sideRecords: { side: string | null; className: string }[] }).sideRecords);
    const lagging = records.some((record) => record.side === 'bottom' && record.className === 'mirrored-top');
    // On WebAssembly the report runs .NET inside the module's observer callback, so the class lands in the same task.
    expect(lagging).toBe(mode === 'server');
  });

  // TIP-KEY-01, TIP-EVT-03 (EVD-EVT-11, EVD-DIS-01, FLT-KEY-01): a consumer's cancel of an Escape close holds on both
  // modes, and the keydown stops at the document on both (no allowPropagation call). On WebAssembly the cancel is read
  // inside the keydown, so the key's default stays; on Server it arrives a round trip later and the module prevents the
  // key per the innermost-popup fallback (ADR 04 §3).
  test('port: a canceled Escape close keeps the tooltip open on both modes and keeps the key\'s default only on WebAssembly', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'tooltip-root', { case: 'contained', defaultOpen: true, cancelClose: true });
    await expect(byTestId(page, 'popup')).toBeVisible();
    await page.evaluate(() => {
      const record: { prevented?: boolean; reached: boolean } = { reached: false };
      (window as unknown as { escapeRecord: typeof record }).escapeRecord = record;
      window.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
          record.reached = true;
        }
      });
      document.addEventListener(
        'keydown',
        (event) => {
          if (event.key === 'Escape') {
            setTimeout(() => {
              record.prevented = event.defaultPrevented;
            });
          }
        },
        { capture: true },
      );
    });

    await page.keyboard.press('Escape');
    await settle(fixture);

    await expect(byTestId(page, 'popup')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key');
    const record = await page.evaluate(() => (window as unknown as { escapeRecord: { prevented: boolean; reached: boolean } }).escapeRecord);
    expect(record).toEqual({ prevented: mode === 'server', reached: false });
  });

  /**
   * Records when the popup with `testId` gets the convergent `hidden` (ADR 04 §2), when its exit animation or transition
   * ends or is canceled, when it leaves the DOM, and when the fixture's `data-open-change-completes` gains a `false`.
   */
  async function recordExit(page: Page, testId: string): Promise<void> {
    await page.evaluate((id) => {
      const times: { ended?: number; hidden?: number; removed?: number; completed?: number } = {};
      (window as unknown as { exitTimes: typeof times }).exitTimes = times;
      const isPopup = (node: Node) => node instanceof Element && node.matches(`[data-testid="${id}"]`);
      for (const type of ['animationend', 'transitionend', 'transitioncancel']) {
        document.addEventListener(
          type,
          (event) => {
            if (isPopup(event.target as Node)) {
              times.ended ??= performance.now();
            }
          },
          { capture: true },
        );
      }
      new MutationObserver((mutations) => {
        for (const mutation of mutations) {
          const target = mutation.target as Element;
          if (mutation.attributeName === 'hidden' && isPopup(target) && target.hasAttribute('hidden')) {
            times.hidden ??= performance.now();
          }
          if (mutation.attributeName === 'data-open-change-completes' && /false$/.test(target.getAttribute('data-open-change-completes') ?? '')) {
            times.completed ??= performance.now();
          }
          for (const node of mutation.removedNodes) {
            if (isPopup(node) || (node instanceof Element && node.querySelector(`[data-testid="${id}"]`))) {
              times.removed ??= performance.now();
            }
          }
        }
      }).observe(document.body, { attributes: true, childList: true, subtree: true, attributeFilter: ['hidden', 'data-open-change-completes'] });
    }, testId);
  }

  const exitTimes = (page: Page) =>
    page.evaluate(() => (window as unknown as { exitTimes: { ended?: number; hidden?: number; removed?: number; completed?: number } }).exitTimes);

  // TIP-ANI-01, TIP-EVT-05 (TRN-ANI-03, TRN-ANI-09/10): after the exit animation the module writes the convergent `hidden`
  // at once on both modes; the removal and onOpenChangeComplete(false) wait for the completion message, a round trip
  // later on Server.
  test('port: an animated close hides the popup at animation end and removes it a round trip later on Server', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, LATENCY);
    }

    const fixture = await gotoPopupFixture(page, mode, 'tooltip-root', { case: 'contained', defaultOpen: true, animated: true, animations: 1 });
    await expect(byTestId(page, 'popup')).toBeVisible();
    await expect(byTestId(page, 'popup')).not.toHaveAttribute('data-starting-style');
    await recordExit(page, 'popup');

    await page.keyboard.press('Escape');

    await expect(byTestId(page, 'popup')).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-change-completes', /false$/);
    const { ended, hidden, removed, completed } = await exitTimes(page);
    expect(hidden).toBeDefined();
    if (mode === 'server') {
      expect(Math.abs(hidden! - ended!)).toBeLessThan(LATENCY);
      expect(removed! - hidden!).toBeGreaterThanOrEqual(2 * LATENCY - 20);
      expect(completed! - hidden!).toBeGreaterThanOrEqual(2 * LATENCY - 20);
    } else {
      // TRN-ANI-09 on WebAssembly: the completion commits the removal with no round trip. Whether `animationend` is
      // dispatched before it is the browser's frame order, which run 40722564761 saw either way.
      expect(removed! - hidden!).toBeLessThan(2 * LATENCY - 20);
      expect(completed! - hidden!).toBeLessThan(2 * LATENCY - 20);
    }
  });

  // TIP-ANI-04 (TRN-ANI-09): the exiting grouped tooltip's transition is canceled by its data-instant="delay" when the next
  // one opens; the module hides it at once and it leaves the DOM a round trip later on Server.
  test('port: an exiting grouped tooltip is hidden when the next opens and leaves the DOM a round trip later on Server', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, LATENCY);
    }

    await gotoPopupFixture(page, mode, 'tooltip-provider', { css: 'exit', triggerDelay: 0, timeout: 30_000, animations: 1 });
    await byTestId(page, 'trigger-1').hover();
    await expect(byTestId(page, 'popup-1')).toBeVisible();
    await moveAway(page);
    await expect.poll(() => byTestId(page, 'popup-1').evaluate((element) => element.getAnimations().length)).toBe(1);
    await recordExit(page, 'popup-1');

    await byTestId(page, 'trigger-2').hover();

    await expect(byTestId(page, 'popup-2')).toBeVisible();
    await expect(byTestId(page, 'popup-1')).toHaveCount(0);
    const { hidden, removed } = await exitTimes(page);
    expect(hidden).toBeDefined();
    if (mode === 'server') {
      expect(removed! - hidden!).toBeGreaterThanOrEqual(2 * LATENCY - 20);
    } else {
      expect(removed! - hidden!).toBeLessThan(2 * LATENCY - 20);
    }
  });

  // ADR 01's hand-off row (chosen by the user 2026-10-27; ADR 04 Amendments 2026-10-28, design point 13.4 (1)); TIP-PTR-07,
  // TIP-FOC-04's at-once clause: the module re-anchors the popup to the new trigger as soon as it requests the hand-off;
  // the active trigger (`data-popup-open`), which C# commits, follows a round trip later on Server, and the payload a
  // further one, since the trigger forwards it from OnAfterRender (PSH-STA-30), which on Server runs after the batch's
  // acknowledgement. Measured locally at 250 ms each way: anchor 0 ms, active 510 ms, payload 950 ms; WebAssembly 0 ms.
  test('port: a hover hand-off re-anchors at once; on Server the active trigger follows a round trip later and the payload a further one', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, LATENCY);
    }

    await gotoPopupFixture(page, mode, 'tooltip-triggers', { mode: 'contained' });
    await byTestId(page, 'trigger-1').hover();
    await expect(byTestId(page, 'content')).toHaveText('1');
    await expect(page.locator('[role="presentation"][data-open][style*="opacity: 0"]')).toHaveCount(0);
    const target = (await byTestId(page, 'trigger-2').boundingBox())!;
    await page.evaluate((left) => {
      const times: { enter?: number; anchor?: number; active?: number; content?: number } = {};
      (window as unknown as { handoffTimes: typeof times }).handoffTimes = times;
      document.querySelector('[data-testid="trigger-2"]')!.addEventListener('mouseenter', () => {
        times.enter ??= performance.now();
      });
      new MutationObserver(() => {
        const now = performance.now();
        const positioner = document.querySelector('[data-testid="positioner"]');
        if (times.anchor === undefined && positioner && Math.abs(positioner.getBoundingClientRect().left - left) < 40) {
          times.anchor = now;
        }
        if (times.active === undefined && document.querySelector('[data-testid="trigger-2"]')?.hasAttribute('data-popup-open')) {
          times.active = now;
        }
        if (times.content === undefined && document.querySelector('[data-testid="content"]')?.textContent === '2') {
          times.content = now;
        }
      }).observe(document.body, { attributes: true, childList: true, characterData: true, subtree: true });
    }, target.x);

    await byTestId(page, 'trigger-2').hover();

    await expect(byTestId(page, 'trigger-2')).toHaveAttribute('data-popup-open');
    await expect(byTestId(page, 'content')).toHaveText('2');
    const { enter, anchor, active, content } = await page.evaluate(
      () => (window as unknown as { handoffTimes: { enter: number; anchor: number; active: number; content: number } }).handoffTimes,
    );
    test.info().annotations.push({ type: 'hand-off', description: `${mode}: anchor ${Math.round(anchor - enter)} ms, active ${Math.round(active - enter)} ms, payload ${Math.round(content - enter)} ms after mouseenter` });
    expect(anchor - enter).toBeLessThan(LATENCY);
    if (mode === 'server') {
      expect(active - enter).toBeGreaterThanOrEqual(2 * LATENCY - 20);
      expect(content - active).toBeGreaterThanOrEqual(LATENCY);
    } else {
      expect(active - enter).toBeLessThan(2 * LATENCY - 20);
      expect(content - enter).toBeLessThan(2 * LATENCY - 20);
    }
  });

  // TIP-DIS-05 (PSH-STA-23): the close for a lost active trigger runs at the render-batch boundary, after paint plus a
  // round trip on Server, during which the popup stays.
  test('port: the trigger-unmount close lands a round trip after the trigger leaves on Server', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, LATENCY);
    }

    const fixture = await gotoPopupFixture(page, mode, 'tooltip-root', { case: 'detached', defaultOpen: true });
    await expect(byTestId(page, 'popup')).toBeVisible();
    await expect(byTestId(page, 'trigger')).toHaveAttribute('data-popup-open', '');
    await page.evaluate(() => {
      const times: { trigger?: number; popup?: number } = {};
      (window as unknown as { removalTimes: typeof times }).removalTimes = times;
      new MutationObserver((mutations) => {
        for (const mutation of mutations) {
          for (const node of mutation.removedNodes) {
            if (node instanceof HTMLElement && node.matches('[data-testid="trigger"]')) {
              times.trigger ??= performance.now();
            }
            if (node instanceof HTMLElement && (node.matches('[data-testid="popup"]') || node.querySelector('[data-testid="popup"]'))) {
              times.popup ??= performance.now();
            }
          }
        }
      }).observe(document.body, { childList: true, subtree: true });
    });

    await fixture.locator('[data-toggle-trigger]').evaluate((element) => (element as HTMLElement).click());

    await expect(byTestId(page, 'popup')).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:none');
    const { trigger, popup } = await page.evaluate(() => (window as unknown as { removalTimes: { trigger: number; popup: number } }).removalTimes);
    if (mode === 'server') {
      expect(popup - trigger).toBeGreaterThanOrEqual(2 * LATENCY - 20);
    } else {
      expect(popup - trigger).toBeLessThan(2 * LATENCY - 20);
    }
  });
});
