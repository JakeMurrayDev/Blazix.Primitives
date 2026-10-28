import { type Locator, type Route } from '@playwright/test';
import { expect, Fixture, fixtureUrl, test, type Page, type RenderMode } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPopupFixture, openChanges, pressControl, settle, type PopupFixtureOptions } from './suites/popupFixture';
import { popupFocusPropsTests } from './suites/popupFocusPropsTests';
import { positionerConformanceTests } from './suites/positioner-conformance';
import { viewportConformanceTests } from './suites/viewportConformanceTests';

// The [L4] rows of docs/components/popover.md (M4 track 5, issue 125) on the `popover-*` fixtures, every title on both
// render modes. The shared suites run with Popover's values (PopoverRoot.test.tsx and
// PopoverRoot.detached-triggers.test.tsx): a trigger pressed by click through the `interactions` module's `press`, role
// and aria-haspopup `dialog`, a positioner, a viewport and a close.

popupConformanceTests({
  fixture: 'popover-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'dialog',
});

detachedTriggersConformanceTests({
  fixture: 'popover-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: true,
  close: true,
  positioner: true,
  viewport: true,
});

// The first run of viewportConformanceTests (track 1 wrote it for the families): PopoverViewport.test.tsx's config.
viewportConformanceTests({
  fixture: 'popover-viewport',
  openOn: 'click',
});

// PopoverPositioner.test.tsx's positionerConformanceTests, with Popover's Viewport (POP-POS rows; FLT's suite on a family).
positionerConformanceTests({
  fixture: 'popover-positioner',
  viewport: true,
});

// The first run of popupFocusPropsTests (track 1 wrote it for the families): PopoverPopup.test.tsx's config.
popupFocusPropsTests({
  fixture: 'popover-focus',
});

// POP-KEY-12 (PopoverPopup.tsx:95-99), PopoverPopup.test.tsx's two titles verbatim on `toolbar-popover` with `inside`: the
// portal's container is inside the toolbar root, so a keydown in the popup bubbles to the toolbar's composite as React's
// portaled events do in Base UI's test (a popup portaled to the body would pass without the stop).
test.describe('<Popover.Popup /> inside a toolbar', () => {
  test('does not relay composite keys from the popup to the toolbar', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'toolbar-popover', { inside: 'button' });

    // The toolbar itself still navigates with the same key, so a passing assertion below can't come from an inert toolbar.
    await page.keyboard.press('Tab');
    await expect(page.getByRole('button', { name: 'First' })).toBeFocused();
    await page.keyboard.press('ArrowRight');
    const trigger = page.getByTestId('trigger');
    await expect(trigger).toBeFocused();

    await page.keyboard.press('Enter');
    const insideButton = page.getByRole('button', { name: 'Inside' });
    await expect(insideButton).toBeFocused();

    await page.keyboard.press('ArrowRight');
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => resolve(undefined))));

    await expect(insideButton).toBeFocused();
    await expect(page.getByRole('button', { name: 'Last' })).not.toBeFocused();
  });

  test('keeps composite keys working inside the popup content', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'toolbar-popover', { inside: 'input' });

    await page.getByTestId('trigger').click();

    const input = page.getByRole('textbox');
    await expect(input).toBeFocused();

    await input.evaluate((element) => (element as HTMLInputElement).setSelectionRange(0, 0));
    await page.keyboard.press('ArrowRight');

    expect(await input.evaluate((element) => (element as HTMLInputElement).selectionStart)).toBe(1);
    await expect(page.getByRole('button', { name: 'Last' })).not.toBeFocused();
  });
});

// Design point 2 of M4 track 5 (settled by the orchestrator 2026-10-28): JavaScript inserts `[data-previous]` before C#'s
// keyed `[data-current]`. The issue-8-style proof on both modes: mid-switch, a C# re-render keeps the inserted container
// and Blazor's own nodes in place, and disposing the popup removes it with nothing left behind (the suite's lagging-payload
// title is the C# remount).
test.describe('<Popover.Viewport /> mid-switch', () => {
  test('port: a C# re-render keeps [data-previous] before [data-current], and a dispose removes both', async ({ page, mode }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    const fixture = await gotoPopupFixture(page, mode, 'popover-viewport', { animations: 1 });
    await page.addStyleTag({
      content: '@keyframes popover-hold { from { opacity: 0.5; } } [data-testid="viewport"] [data-current] { animation: popover-hold 10s; }',
    });
    const viewport = page.getByTestId('viewport');

    await page.getByTestId('trigger-1').click();
    await expect(viewport.locator('[data-current]')).toHaveText('Content 1');
    await page.getByTestId('trigger-2').click();
    await expect(viewport.locator('[data-previous]')).toHaveText('Content 1');

    await settle(fixture);

    await expect(viewport.locator(':scope > [data-previous] + [data-current]')).toHaveText('Content 2');
    await expect(viewport.locator('[data-previous]')).toHaveCount(1);

    await pressControl(fixture.locator('[data-unmount]'));

    await expect(page.locator('[data-previous]')).toHaveCount(0);
    await expect(page.getByTestId('viewport')).toHaveCount(0);
    expect(errors).toEqual([]);
  });
});

// The attach-order rule (ADR 04 Amendments 2026-10-27, the portal's move; settled for Popover by the orchestrator
// 2026-10-28): where a handle depends on another's attach, it awaits it, pinned by a title that stalls the module that must
// go first. Three orders on Popover: (1) the trigger's button, interactions and popup handles; (2) the Positioner's session
// join before its positioning handle reads the session's reference (FLT-STA-08); (3) every portaled handle after the
// portal's move. Each title holds one module's script back (the regular-expression route and its hit assertion of issue
// 133), asserts what the page already does without it, then releases it and asserts the full outcome (PR 143's shape).
test.describe('attach order', () => {
  async function stall(page: Page, module: string): Promise<{ hits: () => number; release: () => Promise<void> }> {
    const held: Route[] = [];
    let hits = 0;
    let released = false;
    await page.route(new RegExp(`/_content/Blazix\\.Primitives/js/${module}(\\.[0-9a-z]+)?\\.js$`), (route) => {
      hits += 1;
      return released ? route.continue() : held.push(route);
    });
    return {
      hits: () => hits,
      release: async () => {
        released = true;
        for (const route of held.splice(0)) {
          await route.continue();
        }
      },
    };
  }

  async function gotoUnready(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<Locator> {
    const query = new URLSearchParams(Object.entries(options).map(([key, value]) => [key, String(value)]));
    await page.goto(`${fixtureUrl(mode, 'popover-conformance')}?${query}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await page.locator(Fixture.interactive('popover-conformance')).first().waitFor({ state: 'attached' });
    return page.locator(Fixture.container('popover-conformance'));
  }

  const inside = (page: Page) => () => page.getByRole('dialog').evaluate((element) => element.contains(document.activeElement));

  for (const module of ['button', 'interactions']) {
    test(`port: with js/${module}.js stalled the trigger attaches its other handles first, and opens the popover once when the module arrives`, async ({ page, mode }) => {
      const stalled = await stall(page, module);
      const fixture = await gotoUnready(page, mode, {});
      await expect.poll(stalled.hits).toBeGreaterThan(0);

      await stalled.release();
      await expect(fixture).toHaveAttribute('data-attached', 'true');
      await page.getByTestId('trigger').click();

      await expect(page.getByTestId('popup')).toBeVisible();
      await settle(fixture);
      expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    });
  }

  // The popup module loads with the first popup handle (the open popover's, and the trigger's once its guards render), so
  // it is held back from the first open.
  test('port: with js/popup.js stalled the popover opens and closes, and its handles attach once the module arrives', async ({ page, mode }) => {
    const popupModule = await stall(page, 'popup');
    const fixture = await gotoPopupFixture(page, mode, 'popover-conformance');
    const trigger = page.getByTestId('trigger');

    await trigger.click();
    await expect(page.getByTestId('popup')).toBeVisible();
    await expect.poll(popupModule.hits).toBeGreaterThan(0);

    await popupModule.release();
    await expect.poll(inside(page)).toBe(true);
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup')).toHaveCount(0);
    await expect(trigger).toBeFocused();
    expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:escape-key']);
  });

  test('port: with js/interactions.js stalled a default-open popover positions at its trigger once the session joins (FLT-STA-08)', async ({ page, mode }) => {
    const interactions = await stall(page, 'interactions');
    const fixture = await gotoUnready(page, mode, { defaultOpen: true });
    await expect.poll(interactions.hits).toBeGreaterThan(0);
    await expect(fixture).toHaveAttribute('data-attached', 'false');

    await interactions.release();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const trigger = (await page.getByTestId('trigger').boundingBox())!;
    await expect
      .poll(async () => (await page.getByTestId('positioner').boundingBox())?.y ?? -1)
      .toBeGreaterThanOrEqual(Math.floor(trigger.y + trigger.height));
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['false:escape-key']);
  });

  test('port: with js/focus-manager.js stalled a default-open popover attaches its other handles first, and focus moves in and returns once the module arrives', async ({ page, mode }) => {
    const focusManager = await stall(page, 'focus-manager');
    const fixture = await gotoPopupFixture(page, mode, 'popover-conformance', { defaultOpen: true });
    await expect.poll(focusManager.hits).toBeGreaterThan(0);
    await expect(page.getByTestId('popup')).toBeVisible();
    expect(await inside(page)()).toBe(false);

    await focusManager.release();
    await expect.poll(inside(page)).toBe(true);
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup')).toHaveCount(0);

    const trigger = page.getByTestId('trigger');
    await trigger.click();
    await expect.poll(inside(page)).toBe(true);
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup')).toHaveCount(0);
    await expect(trigger).toBeFocused();
    expect(await openChanges(fixture)).toEqual(['false:escape-key', 'true:trigger-press', 'false:escape-key']);
  });

  test('port: with js/portal.js stalled the portaled handles wait for the move, and focus moves in once the popup is in place', async ({ page, mode }) => {
    const portal = await stall(page, 'portal');
    const fixture = await gotoUnready(page, mode, { defaultOpen: true });
    await expect.poll(portal.hits).toBeGreaterThan(0);
    await expect.poll(() => page.evaluate(() => document.activeElement?.closest('[role=dialog]') == null)).toBe(true);

    await portal.release();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await expect(page.getByTestId('popup')).toBeVisible();
    await expect.poll(inside(page)).toBe(true);
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup')).toHaveCount(0);
  });
});

// POP-DOM-29 (TRN-DOM-05; ADR 04 Amendments 2026-10-28): from the handover on, the transitions module writes
// `transition: none` on the popup's starting frame and drops it with data-starting-style. Witnessed by a mutation record
// on a reopen, after the handle attached and C#'s style froze.
test.describe('<Popover.Popup /> starting frame', () => {
  test('port: a reopened popup carries transition: none with data-starting-style and drops both on the clearing frame', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-conformance');
    const trigger = page.getByTestId('trigger');
    await trigger.click();
    await expect(page.getByTestId('popup')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup')).toHaveCount(0);

    await page.evaluate(() => {
      const records: { starting: boolean; transition: string }[] = [];
      (window as unknown as { startingRecords: typeof records }).startingRecords = records;
      new MutationObserver(() => {
        const popup = document.querySelector<HTMLElement>('[data-testid="popup"]');
        if (popup !== null) {
          records.push({ starting: popup.hasAttribute('data-starting-style'), transition: popup.style.getPropertyValue('transition-property') });
        }
      }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['style', 'data-starting-style'] });
    });
    await trigger.click();
    await expect(page.getByTestId('popup')).toBeVisible();

    // The popup shows from C#'s render before its transitions handle attaches (on WebAssembly the visible check can pass
    // first), so the title waits for the module's starting record and then for the settled style.
    const read = () => page.evaluate(() => (window as unknown as { startingRecords: { starting: boolean; transition: string }[] }).startingRecords);
    await expect.poll(async () => (await read()).some((record) => record.starting)).toBe(true);
    await expect(page.getByTestId('popup')).not.toHaveAttribute('data-starting-style');
    await expect.poll(() => page.getByTestId('popup').evaluate((element) => (element as HTMLElement).style.getPropertyValue('transition-property'))).toBe('');

    const records = await read();
    // Before the handle attaches C# renders the declaration alone (data-starting-style is the module's attribute); from the
    // module's first starting record the two arrive and leave together.
    const from = records.findIndex((record) => record.starting);
    expect(from).toBeGreaterThanOrEqual(0);
    const owned = records.slice(from);
    expect(owned.filter((record) => record.starting).every((record) => record.transition === 'none')).toBe(true);
    expect(owned.filter((record) => !record.starting).every((record) => record.transition === '')).toBe(true);
    expect(owned.at(-1)).toEqual({ starting: false, transition: '' });
  });
});

// POP-DOM-22's clip-path clause (L4; PSH-DOM-14, PSH-ALG-21): a modal popover's internal backdrop, rendered by track 8's
// shared writer before the positioner, has the active trigger cut out of it by the popup module.
test.describe('<Popover.Positioner /> internal backdrop', () => {
  test('port: a modal popover cuts its trigger out of the internal backdrop', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-conformance', { modal: true });
    const trigger = page.getByTestId('trigger');
    await trigger.click();
    const backdrop = page.getByTestId('positioner').locator('xpath=preceding-sibling::*[1]');
    await expect(backdrop).toHaveAttribute('role', 'presentation');

    // The polygon's hole: the four pixel corners after the five percentage points of the outer rectangle.
    const box = (await trigger.boundingBox())!;
    const hole = async () => {
      const clipPath = await backdrop.evaluate((element) => (element as HTMLElement).style.clipPath);
      return [...clipPath.matchAll(/(-?[\d.]+)px (-?[\d.]+)px/g)].slice(0, 4).map((match) => [Number(match[1]), Number(match[2])]);
    };
    await expect.poll(async () => (await hole()).length).toBe(4);
    const expected = [[box.x, box.y], [box.x, box.y + box.height], [box.x + box.width, box.y + box.height], [box.x + box.width, box.y]];
    (await hole()).forEach(([x, y], index) => {
      expect(x).toBeCloseTo(expected[index]![0]!, 0);
      expect(y).toBeCloseTo(expected[index]![1]!, 0);
    });

    // The cut-out lets a press reach the trigger, which closes the popover.
    await trigger.click();
    await expect(page.getByTestId('popup')).toHaveCount(0);
  });
});

// The pointer and hover titles of PopoverRoot.test.tsx and PopoverTrigger.test.tsx on `popover-hover` (POP-PTR rows): real
// mouse input, the interactions module's hover timers driven by a paused page clock (Base UI's fake timers), which every
// title resumes after itself (the worker context's clock, tests/browser/CONTEXT.md).
test.describe('<Popover.Trigger /> hover', () => {
  test.afterEach(async ({ page }) => {
    await page.clock.resume().catch(() => undefined);
  });

  async function pausedClock(page: Page): Promise<void> {
    await page.clock.install();
    await page.clock.pauseAt(Date.now() + 1000);
  }

  test('should open after delay with rest type by default', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { delay: 100 });
    await pausedClock(page);
    const content = page.getByText('Content', { exact: true });

    await page.getByTestId('trigger-1').hover();
    await page.mouse.move(110, 110, { steps: 2 });
    await page.getByTestId('trigger-1').hover();
    await expect(content).toHaveCount(0);

    await page.clock.runFor(100);
    await expect(content).toBeVisible();
  });

  // PATIENT_CLICK_THRESHOLD (utils/constants.ts): a press within 500 ms of a hover open keeps the popover open (sticks).
  const patientClickThreshold = 500;

  test('does not open on hover when disabled', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { disabled: true, delay: 0, nativeButton: false });
    const trigger = page.getByTestId('trigger-1');
    await expect(trigger).toHaveAttribute('data-disabled');

    await trigger.hover();
    await page.mouse.move(110, 110);

    await expect(page.getByText('Content', { exact: true })).toHaveCount(0);
    await expect(trigger).not.toHaveAttribute('data-popup-open');
  });

  test('closes the popover if the user clicks patiently', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { delay: 0 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');

    await trigger.hover();
    await expect(trigger).toHaveAttribute('data-popup-open');
    await page.clock.runFor(patientClickThreshold);
    await trigger.click();
    await expect(trigger).not.toHaveAttribute('data-popup-open');

    await page.getByTestId('outside').hover();
    await settle(fixture);
    await expect(trigger).not.toHaveAttribute('data-popup-open');
  });

  test('sticks if the user clicks impatiently', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { delay: 0 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');

    await trigger.hover();
    await expect(trigger).toHaveAttribute('data-popup-open');
    await page.clock.runFor(patientClickThreshold - 1);
    await trigger.click();
    await settle(fixture);
    await expect(trigger).toHaveAttribute('data-popup-open');

    await page.getByTestId('outside').hover();
    await settle(fixture);
    await expect(trigger).toHaveAttribute('data-popup-open');
    await page.clock.runFor(1);
    await settle(fixture);
    await expect(trigger).toHaveAttribute('data-popup-open');
  });

  test('sticks when clicked before the hover delay completes', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { delay: 300 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');

    await trigger.hover();
    await page.clock.runFor(100);
    await trigger.click();
    await expect(trigger).toHaveAttribute('data-popup-open');

    await page.getByTestId('outside').hover();
    await settle(fixture);
    await expect(trigger).toHaveAttribute('data-popup-open');
  });

  test('should keep the popover open when re-hovered and clicked within the patient threshold', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { delay: 100 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');
    const content = page.getByText('Content', { exact: true });

    await trigger.hover();
    await page.clock.runFor(100);
    await expect(content).toBeVisible();
    await page.clock.runFor(patientClickThreshold);

    await page.mouse.move(5, 5);
    await trigger.hover();
    await trigger.click();
    await expect(content).toBeVisible();
  });

  test('reopens on hover after an impatient click is followed by a close button press', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { delay: 100 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');
    const dialog = page.getByRole('dialog');

    await trigger.hover();
    await page.clock.runFor(100);
    await expect(dialog).toBeVisible();
    await page.clock.runFor(patientClickThreshold - 1);
    await trigger.click();
    await page.getByRole('button', { name: 'Close' }).click();
    await expect(dialog).toHaveCount(0);

    await page.mouse.move(5, 5);
    await trigger.hover();
    await page.clock.runFor(100);
    await expect(dialog).toBeVisible();
  });

  test('closes after hovering out of a popup opened by its trigger', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { open: false, delay: 0 });
    await page.getByTestId('trigger-1').hover();
    await expect(page.getByRole('dialog')).toBeVisible();

    await page.getByTestId('popup').hover();
    await page.mouse.move(5, 5);

    await expect(page.getByRole('dialog')).toHaveCount(0);
  });

  test('does not close after hovering out of a popup opened externally', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { open: false, delay: 0 });
    await pressControl(fixture.locator('[data-set-open="true"]'));
    await expect(page.getByRole('dialog')).toBeVisible();

    await page.getByTestId('popup').hover();
    await page.mouse.move(5, 5);
    await settle(fixture);

    await expect(page.getByRole('dialog')).toBeVisible();
  });

  test('does not close after hovering out of a popup opened without trigger hover', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { defaultOpen: true });
    await expect(page.getByText('Content', { exact: true })).toBeVisible();

    await page.getByTestId('popup').hover();
    await page.mouse.move(5, 5);
    await settle(fixture);

    await expect(page.getByText('Content', { exact: true })).toBeVisible();
  });

  test('should close after delay', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { closeDelay: 100 });
    await pausedClock(page);
    const content = page.getByText('Content', { exact: true });

    await page.getByTestId('trigger-1').hover();
    await page.clock.runFor(300);
    await expect(content).toBeVisible();

    await page.getByTestId('outside').hover();
    await page.clock.runFor(50);
    await settle(fixture);
    await expect(content).toBeVisible();

    await page.clock.runFor(50);
    await expect(content).toHaveCount(0);
  });
});

