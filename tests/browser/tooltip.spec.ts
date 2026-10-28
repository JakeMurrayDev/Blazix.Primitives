import { type Browser } from '@playwright/test';
import { expect, readDiagnostics, test, type Page, type RenderMode } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { gotoPopupFixture, openChanges, settle, type PopupFixtureOptions } from './suites/popupFixture';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPositionerCase, positionerConformanceTests } from './suites/positioner-conformance';

// The [L4] rows of docs/components/tooltip.md (M4 track 6, issue 121) on the Tooltip fixtures, both render modes, Server
// first. Base UI's titles verbatim; port titles start with "port:". The shared suites run with Tooltip's options
// (TooltipRoot.test.tsx:27-40, TooltipRoot.detached-triggers.test.tsx:175-189).

const OVERLAP_TITLE = 'resolves a trigger still registered to the previous root during a transient overlap';

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
    // Base UI changes the phase with `setProps`; the suite helper clicks the fixture's button with the real pointer, which
    // is a press outside an open tooltip (TIP-PTR-14). The port title below runs the same steps with a click that has no
    // pointer press.
    [OVERLAP_TITLE]: 'TIP-PTR-14: the phase button is an outside press; restated as the port title of this spec',
  },
});

test.describe('detached triggers', () => {
  test(`port: ${OVERLAP_TITLE}, with the phase changed by a click with no pointer press`, async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'tooltip-triggers', { case: 'overlap' });
    const setPhase = (phase: string) => fixture.locator(`[data-set-phase="${phase}"]`).evaluate((element) => (element as HTMLElement).click());
    const trigger = (number: number) => page.locator(`[data-testid="trigger-${number}"]`);

    await setPhase('overlap');
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
    await expect(fixture).not.toHaveAttribute('data-handle-error', /./);
    await expect(trigger(2)).toHaveAttribute('data-popup-open');
    await expect(trigger(1)).not.toHaveAttribute('data-popup-open');
    expect((await readDiagnostics(page, mode, fixture)).filter((entry) => entry.message.includes('No trigger found'))).toHaveLength(0);

    await setPhase('incoming');
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
    await expect(trigger(2)).toHaveAttribute('data-popup-open');
    await expect(trigger(1)).not.toHaveAttribute('data-popup-open');
  });
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

/** Pauses the page's clock: its timers fire only when the test runs it forward. */
async function freezeClock(page: Page): Promise<void> {
  const now = await page.evaluate(() => Date.now());
  await page.clock.pauseAt(now + 1);
  frozenPages.add(page);
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
 * Moves the real pointer away from the trigger, below it, which leaves it from the side opposite a top popup. Under a
 * paused clock the frames run first, so the positioning pass has published the placement the safe polygon reads.
 */
async function moveAway(page: Page): Promise<void> {
  if (frozenPages.has(page)) {
    await page.clock.runFor(32);
  }

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
});

// ---------------------------------------------------------------------------------------------------------------------
// The titles that measure or move between sized elements (TooltipRoot.test.tsx:42-77, 592-638, 1104-1390) on
// `tooltip-layout`. Cursor anchoring is the `interactions` module's virtual anchor, which `positioning` follows through
// the session (FLT-STA-08, PR 135).
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<Tooltip.Root /> layout', () => {
  const open = (page: Page, mode: RenderMode, options: PopupFixtureOptions) => gotoPopupFixture(page, mode, 'tooltip-layout', options);
  const byTestId = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`);

  async function centerX(page: Page, id: string): Promise<number> {
    const box = (await byTestId(page, id).boundingBox())!;
    return box.x + box.width / 2;
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
