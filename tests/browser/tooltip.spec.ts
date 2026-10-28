import { expect, readDiagnostics, test, type Page, type RenderMode } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { gotoPopupFixture, openChanges, settle, type PopupFixtureOptions } from './suites/popupFixture';
import { popupConformanceTests } from './suites/popupConformanceTests';

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
