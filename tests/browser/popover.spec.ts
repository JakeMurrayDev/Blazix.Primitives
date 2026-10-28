import { type Locator, type Route } from '@playwright/test';
import { expect, Fixture, fixtureUrl, isScrollLocked, test, type Page, type RenderMode } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPopupFixture, openChangeCompletes, openChanges, pressControl, settle, type PopupFixtureOptions } from './suites/popupFixture';
import { popupFocusPropsTests } from './suites/popupFocusPropsTests';
import { positionerConformanceTests } from './suites/positioner-conformance';
import { viewportConformanceTests } from './suites/viewportConformanceTests';

// Touch titles: Playwright taps only in a context made with `hasTouch`, and the worker's shared context is not, so each
// touch title opens its own (on `wasm` it downloads the runtime again).
const touchTest = test.extend<{ touchPage: Page }>({
  touchPage: async ({ browser }, use) => {
    const context = await browser.newContext({ hasTouch: true });
    const page = await context.newPage();
    await page.clock.install();
    await use(page);
    await context.close();
  },
});

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

  test('should have the data-popup-open but not the data-pressed attribute when open by hover', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { delay: 0 });
    const trigger = page.getByTestId('trigger-1');

    await trigger.hover();

    await expect(trigger).toHaveAttribute('data-popup-open');
    await expect(trigger).not.toHaveAttribute('data-pressed');
  });

  test('should toggle closed with Enter or Space when rendering a <div>', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { openOnHover: false, nativeButton: false });
    const trigger = page.getByTestId('trigger-1');
    const content = page.getByText('Content', { exact: true });

    // Each Shift+Tab leaves the popup, so it waits for the open's initial focus to land there first.
    const focusInPopup = () => expect.poll(() => page.evaluate(() => document.activeElement?.closest('[data-testid="popup"]') != null)).toBe(true);

    await trigger.focus();
    await page.keyboard.press('Enter');
    await expect(content).toBeVisible();

    await focusInPopup();
    await page.keyboard.press('Shift+Tab');
    await expect(trigger).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(content).toHaveCount(0);

    await page.keyboard.press('Enter');
    await expect(content).toBeVisible();
    await focusInPopup();
    await page.keyboard.press('Shift+Tab');
    await expect(trigger).toBeFocused();
    await page.keyboard.press(' ');
    await expect(content).toHaveCount(0);

    await page.keyboard.press(' ');
    await expect(content).toBeVisible();
    await focusInPopup();
    await page.keyboard.press('Shift+Tab');
    await expect(trigger).toBeFocused();
    await page.keyboard.press(' ');
    await expect(content).toHaveCount(0);
  });

  // PopoverTrigger.test.tsx's MultiTriggerPopover: two hover triggers (delay and closeDelay 0) whose payload the popup shows.
  // A real mouse reaches the trigger before pressing it, so the mouse case's press lands on a hover-opened popup.
  test('hands ownership to a hovered sibling trigger when opened by mouse', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { triggerCount: 2, delay: 0, closeDelay: 0 });

    await page.getByTestId('trigger-1').click();
    await expect(page.getByTestId('payload')).toHaveText('1');

    await page.getByTestId('trigger-2').hover();

    await expect(page.getByTestId('payload')).toHaveText('2');
    await expect(page.getByTestId('trigger-2')).toHaveAttribute('aria-expanded', 'true');
  });

  touchTest('keeps ownership on the tapped trigger when a sibling trigger is hovered', async ({ touchPage: page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { triggerCount: 2, delay: 0, closeDelay: 0 });

    await page.getByTestId('trigger-1').tap();
    await expect(page.getByTestId('payload')).toHaveText('1');

    await page.getByTestId('trigger-2').hover();
    await settle(fixture);

    await expect(page.getByTestId('payload')).toHaveText('1');
    await expect(page.getByTestId('trigger-2')).toHaveAttribute('aria-expanded', 'false');
  });

  test('does not move focus to the popover when opened with hover', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { delay: 0 });
    const trigger = page.getByTestId('trigger-1');

    await trigger.focus();
    await trigger.hover();
    await settle(fixture);

    const close = page.getByRole('button', { name: 'Close' });
    await expect(close).toBeVisible();
    await expect(close).not.toBeFocused();
  });

  test('does not change focus when opened with hover and closed', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { delay: 0, closeDelay: 0, inputs: true, popupClass: 'fade' });
    const lastInput = page.getByTestId('last-input');

    await lastInput.focus();
    await page.getByTestId('trigger-1').hover();
    await expect(page.getByRole('dialog')).toBeVisible();

    await page.getByTestId('first-input').hover();

    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(lastInput).toBeFocused();
  });

  test('returns focus to the trigger if opened by click before the hover delay completes', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { delay: 300 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');

    await trigger.hover();
    await page.clock.runFor(100);
    await trigger.click();
    await expect(page.getByRole('button', { name: 'Close' })).toBeVisible();

    await page.clock.runFor(1000);
    await page.clock.resume();
    await page.getByRole('button', { name: 'Close' }).click();

    await expect(trigger).toBeFocused();
  });

  test('enables modal behavior after a hover-open is clicked', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { modal: true, delay: 0 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');
    const backdrop = page.getByTestId('positioner').locator('xpath=preceding-sibling::*[1]');

    await trigger.hover();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(backdrop).toHaveCount(0);

    await page.clock.runFor(patientClickThreshold - 1);
    await trigger.click();

    await expect(backdrop).toHaveAttribute('role', 'presentation');
  });

  // Base UI clicks the trigger while the pointer is in the positioner; a real pointer cannot, so the click comes from Enter on
  // the focused trigger, which leaves the pointer where it is.
  test('cleans up the safe polygon handler after a hover-opened popup becomes click-sticky', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { modal: false, delay: 0, closeDelay: 0 });
    await pausedClock(page);
    await page.evaluate(() => {
      const moves: { added: unknown[]; removed: unknown[] } = { added: [], removed: [] };
      (window as unknown as { moves: typeof moves }).moves = moves;
      const add = document.addEventListener.bind(document);
      const remove = document.removeEventListener.bind(document);
      document.addEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) => {
        if (type === 'mousemove') moves.added.push(listener);
        add(type, listener, options);
      };
      document.removeEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) => {
        if (type === 'mousemove') moves.removed.push(listener);
        remove(type, listener, options);
      };
    });
    const trigger = page.getByTestId('trigger-1');
    const moves = () => page.evaluate(() => {
      const { added, removed } = (window as unknown as { moves: { added: unknown[]; removed: unknown[] } }).moves;
      return { added: added.length, lastRemoved: added.length > 0 && removed.includes(added.at(-1)) };
    });

    await trigger.focus();
    await trigger.hover();
    await expect(page.getByRole('dialog')).toBeVisible();
    // The paused clock holds the positioner's first frame: let it place the popup, well inside the patient-click window.
    await page.clock.runFor(100);
    await expect(page.getByTestId('positioner')).toHaveAttribute('style', /translate/);
    const box = (await page.getByTestId('popup').boundingBox())!;
    await page.mouse.move(box.x + 10, box.y + 10, { steps: 4 });
    await expect.poll(async () => (await moves()).added).toBeGreaterThan(0);
    expect((await moves()).lastRemoved).toBe(false);

    await page.keyboard.press('Enter');
    await settle(fixture);
    await page.mouse.move(5, 5);
    await settle(fixture);

    await expect(page.getByRole('dialog')).toBeVisible();
    await expect.poll(async () => (await moves()).lastRemoved).toBe(true);
  });

  test('reopens immediately when re-hovering the trigger during a hover close transition', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-hover', { keepMounted: true, delay: 1, popupClass: 'reopen', animations: 1 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');
    const popup = page.getByTestId('popup');

    await trigger.hover();
    await page.clock.runFor(1);
    await expect(popup).toHaveAttribute('data-open');

    await page.mouse.move(5, 5);
    await page.clock.runFor(50);
    await expect(popup).toHaveAttribute('data-ending-style');

    // The clock stays paused, so the rest delay cannot reopen it: only the close-transition fast path on mouseenter can.
    await trigger.hover();

    await expect(popup).toHaveAttribute('data-open');
    await expect(popup).not.toHaveAttribute('data-closed');
  });

  test('keeps the popup open when the last hovered trigger is pressed', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-hover', { open: false, triggerCount: 2, hoverTriggers: 1, delay: 0 });
    await pausedClock(page);
    const trigger = page.getByTestId('trigger-1');
    const popup = page.getByTestId('popup');

    await trigger.hover();
    await expect(popup).toBeVisible();
    // The hover's patient-click window ends, so only the close-time reset keeps the click sticky.
    await page.clock.runFor(patientClickThreshold);

    await pressControl(fixture.locator('[data-set-open="false"]'));
    await expect(popup).toHaveCount(0);
    await pressControl(fixture.locator('[data-set-open="true"]'));
    await expect(popup).toBeVisible();

    await trigger.click();
    await settle(fixture);
    await expect(popup).toBeVisible();
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


// A click on the page itself (Base UI's `document.body`): the viewport's bottom-right corner, which the fixtures leave empty.
async function clickEmptyPage(page: Page): Promise<void> {
  const { width, height } = page.viewportSize()!;
  const point = [width - 4, height - 4] as const;
  expect(await page.evaluate(([x, y]) => {
    const element = document.elementFromPoint(x, y);
    return element === document.body || element === document.documentElement;
  }, point)).toBe(true);
  await page.mouse.click(...point);
}

// A press on a backdrop covering the viewport, at the bottom-right corner: pressed first, released after the checks.
async function pressBackdropCorner(page: Page, backdrop: Locator): Promise<() => Promise<void>> {
  const { width, height } = page.viewportSize()!;
  const point = [width - 4, height - 4] as const;
  expect(await backdrop.evaluate((element, [x, y]) => document.elementFromPoint(x, y) === element, point)).toBe(true);
  await page.mouse.move(...point);
  await page.mouse.down();
  return () => page.mouse.up();
}

// The focus guards a key press visits, recorded by a capture `focusin` listener (PopoverRoot.test.tsx's trackFocusedGuards):
// `unique` is false once one guard took focus twice.
async function trackFocusedGuards(page: Page): Promise<() => Promise<{ count: number; unique: boolean }>> {
  await page.evaluate(() => {
    const guards: Element[] = [];
    (window as unknown as { focusedGuards: Element[] }).focusedGuards = guards;
    document.addEventListener('focusin', (event) => {
      const target = event.target as Element;
      if (target.hasAttribute('data-base-ui-focus-guard')) {
        guards.push(target);
      }
    }, true);
  });
  return () => page.evaluate(() => {
    const guards = (window as unknown as { focusedGuards: Element[] }).focusedGuards;
    return { count: guards.length, unique: new Set(guards).size === guards.length };
  });
}

// PopoverRoot.test.tsx's focus, modal and non-modal focus titles on `popover-root`, once per trigger layout as Base UI's
// describe.for runs them (POP-KEY, POP-FOC and POP-DIS rows).
for (const [variant, name] of [['contained', 'contained triggers'], ['detached', 'detached triggers'], ['multiple', 'multiple detached triggers']] as const) {
  test.describe(`<Popover.Root /> when using ${name}`, () => {
    const goto = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) =>
      gotoPopupFixture(page, mode, 'popover-root', { variant, ...options });
    // Base UI's titles focus an element inside a default-open popup with act(), after the open's initial focus has landed
    // on it; here the titles wait for that initial focus instead, so it cannot land after the test's own keys.

    test('should close when the anchor is clicked twice', async ({ page, mode }) => {
      await goto(page, mode);
      const anchor = page.getByTestId('trigger');

      await anchor.click();
      await expect(page.getByText('Content', { exact: true })).toBeVisible();

      await anchor.click();
      await expect(page.getByText('Content', { exact: true })).toHaveCount(0);
    });

    test('rewires dismiss interactions after closing and reopening', async ({ page, mode }) => {
      await goto(page, mode, { modal: false, inside: 'close' });
      const trigger = page.getByTestId('trigger');

      await trigger.click();
      await expect(page.getByRole('dialog')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toHaveCount(0);

      await trigger.click();
      await expect(page.getByRole('dialog')).toBeVisible();
      await clickEmptyPage(page);
      await expect(page.getByRole('dialog')).toHaveCount(0);
    });

    test('remains uncontrolled, so a trigger click closes it', async ({ page, mode }) => {
      await goto(page, mode, { defaultOpen: true });
      await expect(page.getByText('Content', { exact: true })).toBeVisible();

      await page.getByTestId('trigger').click();

      await expect(page.getByText('Content', { exact: true })).toHaveCount(0);
    });

    test.describe('outside press event with backdrops', () => {
      test('uses intentional outside press with user backdrop (mouse): closes on click, not on mousedown', async ({ page, mode }) => {
        const fixture = await goto(page, mode, { defaultOpen: true, backdrop: true });
        await expect(page.getByRole('dialog')).toBeVisible();

        const release = await pressBackdropCorner(page, page.getByTestId('backdrop'));
        await settle(fixture);
        await expect(page.getByRole('dialog')).toBeVisible();
        expect(await openChanges(fixture)).toEqual([]);

        await release();
        await expect(page.getByRole('dialog')).toHaveCount(0);
        expect(await openChanges(fixture)).toHaveLength(1);
      });

      test('uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown', async ({ page, mode }) => {
        const fixture = await goto(page, mode, { defaultOpen: true, modal: true });
        await expect(page.getByTestId('popover-popup')).toBeVisible();
        // Base UI's document.querySelector('[role="presentation"]'): the backdrop, before the positioner (which has the role too).
        const internalBackdrop = page.locator('[role="presentation"]').first();

        const release = await pressBackdropCorner(page, internalBackdrop);
        await settle(fixture);
        await expect(page.getByTestId('popover-popup')).toBeVisible();
        expect(await openChanges(fixture)).toEqual([]);

        await release();
        await expect(page.getByTestId('popover-popup')).toHaveCount(0);
        expect(await openChanges(fixture)).toHaveLength(1);
      });

      test('closing via outside press: works when clicking another element inside the same shadow root', async ({ page, mode }) => {
        const fixture = await goto(page, mode, { defaultOpen: true, container: 'shadow' });
        await expect(page.getByRole('dialog')).toBeVisible();
        // Base UI renders its own outside button into the shadow root; Blazor renders into the document, so the spec adds it.
        // Base UI's fireEvent.click moves no focus, so the button's mousedown keeps focus where it is: a real press that
        // moved focus out of the popup would close it with focus-out first (and, on Server, twice: gap 14 below).
        await page.locator('[data-shadow-host]').evaluate((host) => {
          const outside = Object.assign(document.createElement('button'), { textContent: 'Outside' });
          outside.setAttribute('data-testid', 'outside');
          outside.style.cssText = 'position: fixed; left: 0; bottom: 0';
          outside.addEventListener('mousedown', (event) => event.preventDefault());
          host.shadowRoot!.append(outside);
        });

        await page.getByTestId('outside').click();

        await expect(page.getByRole('dialog')).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });

      test('closing via outside press: works when clicking outside the shadow root', async ({ page, mode }) => {
        const fixture = await goto(page, mode, { defaultOpen: true, container: 'shadow' });
        await expect(page.getByRole('dialog')).toBeVisible();

        await clickEmptyPage(page);

        await expect(page.getByRole('dialog')).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });
    });

    test('does not repeat closing completion when `unmount` is called after the automatic unmount', async ({ page, mode }) => {
      const fixture = await goto(page, mode);
      const trigger = page.getByTestId('trigger');
      const closings = async () => (await openChangeCompletes(fixture)).filter((open) => open === 'false');

      await trigger.click();
      await expect(page.getByRole('dialog')).toBeVisible();
      await trigger.click();
      await expect(page.getByRole('dialog')).toHaveCount(0);
      await expect.poll(closings).toHaveLength(1);

      await pressControl(fixture.locator('[data-unmount]'));
      await settle(fixture);
      expect(await closings()).toHaveLength(1);
    });

    test('should render an internal backdrop when `true`', async ({ page, mode }) => {
      await goto(page, mode, { modal: true, outside: 'after' });

      await page.getByTestId('trigger').click();
      await expect(page.getByTestId('popover-popup')).toBeVisible();

      await expect(page.getByTestId('positioner').locator('xpath=preceding-sibling::*[1]')).toHaveAttribute('role', 'presentation');
    });

    test('should close child popover when clicking parent popover', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'popover-nested', { case: 'child', variant });
      await expect(page.getByTestId('parent-popup')).toHaveCount(0);
      await expect(page.getByTestId('child-popup')).toHaveCount(0);

      await page.getByTestId('parent-trigger').click();
      const parentPopup = page.getByTestId('parent-popup');
      await expect(parentPopup).toBeVisible();
      await expect(page.getByTestId('child-popup')).toHaveCount(0);

      await expect(fixture).toHaveAttribute('data-child-attached', 'true');
      await page.getByTestId('child-trigger').click();
      await expect(parentPopup).toBeVisible();
      await expect(page.getByTestId('child-popup')).toHaveCount(1);

      await parentPopup.click({ position: { x: 4, y: 4 } });
      await settle(fixture);

      await expect(parentPopup).toBeVisible();
      await expect(page.getByTestId('child-popup')).toHaveCount(0);
    });

    test('focuses the trigger after the popover is closed but not unmounted', async ({ page, mode }) => {
      await goto(page, mode, { before: 'plain', after: 'plain', keepMounted: true, inside: 'close' });
      const toggle = page.getByTestId('trigger');

      await toggle.click();
      await page.getByRole('button', { name: 'Close' }).click();

      await expect(toggle).toBeFocused({ timeout: 1500 });
    });

    test('restores temporarily disabled focus before focusing a reopened keepMounted popover', async ({ page, mode }) => {
      await goto(page, mode, { before: 'plain', keepMounted: true, inside: 'button', afterTrigger: 'after' });
      const trigger = page.getByTestId('trigger');
      const inside = page.getByTestId('inside');

      await trigger.click();
      await expect(inside).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(page.getByTestId('after')).toBeFocused();
      await expect(page.getByTestId('popover-popup')).not.toHaveAttribute('data-open');

      await trigger.click();
      await expect(inside).toBeFocused();
    });

    test('moves focus to the element following the trigger, excluding the popup, when tabbing forward from the open popup', async ({ page, mode }) => {
      await goto(page, mode, { before: 'plain', after: 'plain', defaultOpen: true, inside: 'input', afterTrigger: 'focus-target' });
      await expect(page.getByTestId('input-inside')).toBeFocused();

      await page.keyboard.press('Tab');

      await expect(page.getByTestId('focus-target')).toBeFocused();
      await expect(page.getByTestId('popover-popup')).toHaveCount(0);
    });

    test('moves focus to the trigger when tabbing backward from the open popup then to the popup when tabbing forward', async ({ page, mode }) => {
      await goto(page, mode, { before: 'plain', after: 'plain', defaultOpen: true, inside: 'input' });
      await expect(page.getByTestId('input-inside')).toBeFocused();

      await page.waitForTimeout(50);
      await page.keyboard.press('Shift+Tab');
      await expect(page.getByTestId('trigger')).toBeFocused();
      await expect(page.getByTestId('popover-popup')).toBeVisible();

      await page.waitForTimeout(50);
      await page.keyboard.press('Tab');
      await expect(page.getByTestId('input-inside')).toBeFocused();
    });

    test('closes and moves focus before the trigger when tabbing backward from the focused trigger', async ({ page, mode }) => {
      await goto(page, mode, { before: 'before', after: 'plain', defaultOpen: true, inside: 'input' });
      // The open's initial focus lands first, as Base UI's act() flushes it before the test focuses the trigger.
      await expect(page.getByTestId('input-inside')).toBeFocused();
      await page.getByTestId('trigger').focus();

      await page.keyboard.press('Shift+Tab');

      await expect(page.getByTestId('before')).toBeFocused();
      await expect(page.getByTestId('popover-popup')).toHaveCount(0);
    });

    test('moves focus to the element following the trigger when tabbing forward from the open popup', async ({ page, mode }) => {
      await goto(page, mode, { before: 'plain', after: 'plain', defaultOpen: true, inside: 'input', afterTrigger: 'focus-target' });
      await expect(page.getByTestId('input-inside')).toBeFocused();

      await page.keyboard.press('Tab');

      await expect(page.getByTestId('focus-target')).toBeFocused();
      await expect(page.getByTestId('popover-popup')).toHaveCount(0);
    });

    test('moves focus to the element preceding the trigger when tabbing backward from the trigger while open', async ({ page, mode }) => {
      await goto(page, mode, { before: 'focus-target', afterTrigger: 'plain', after: 'plain', defaultOpen: true, inside: 'input', animations: 1 });
      await expect(page.getByTestId('input-inside')).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(page.getByTestId('trigger')).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(page.getByTestId('focus-target')).toBeFocused();
      await expect(page.getByTestId('popover-popup')).toHaveCount(0);
    });

    test('moves focus to the element following the trigger when tabbing forward from the trigger', async ({ page, mode }) => {
      await goto(page, mode, { afterTrigger: 'focus-target', inside: 'input', popupClass: 'fade', animations: 1 });
      const trigger = page.getByTestId('trigger');
      const popup = page.getByTestId('popover-popup');

      await trigger.click();
      await expect(popup).toBeVisible();
      await trigger.click();
      await expect(popup).toHaveAttribute('data-ending-style');
      await expect(trigger).toBeFocused();

      await page.keyboard.press('Tab');

      await expect(page.getByTestId('focus-target')).toBeFocused();
      await expect(popup).toHaveCount(0);
      await expect(page.getByTestId('focus-target')).toBeFocused();
    });

    // Base UI's 'with no other tabbable element on the page' (skipped for multiple detached triggers, whose second trigger
    // is one): a Tab out of the popup has nowhere to go but the trigger.
    test.describe('with no other tabbable element on the page', () => {
      test.skip(variant === 'multiple', 'Base UI skips it for multiple detached triggers');

      test('does not loop focus between guards when tabbing out of the popup', async ({ page, mode }) => {
        await goto(page, mode, { animations: 1 });
        await page.getByTestId('trigger').click();
        await expect(page.getByRole('dialog')).toBeFocused();

        const focusedGuards = await trackFocusedGuards(page);
        await page.keyboard.press('Tab');

        await expect(page.getByRole('dialog')).toHaveCount(0);
        expect((await focusedGuards()).unique).toBe(true);
        await expect(page.getByTestId('trigger')).toBeFocused();
      });

      test('does not loop focus between guards when shift-tabbing out of the trigger', async ({ page, mode }) => {
        await goto(page, mode, { animations: 1 });
        const trigger = page.getByTestId('trigger');
        await trigger.click();
        await expect(page.getByRole('dialog')).toBeFocused();
        await trigger.focus();

        const focusedGuards = await trackFocusedGuards(page);
        await page.keyboard.press('Shift+Tab');

        await expect(page.getByRole('dialog')).toHaveCount(0);
        expect((await focusedGuards()).unique).toBe(true);
        await expect(trigger).toBeFocused();
      });
    });

    test('closes as soon as focus leaves the popup on pointer down outside', async ({ page, mode }) => {
      await goto(page, mode, { defaultOpen: true, inside: 'button', outside: 'after' });
      await expect(page.getByTestId('inside')).toBeFocused();

      await page.getByTestId('outside').hover();
      await page.mouse.down();

      await expect(page.getByRole('dialog')).toHaveCount(0);
      await page.mouse.up();
    });

    test('moves focus to the popup when a focused child is removed on pointerdown and outside press still dismisses', async ({ page, mode }) => {
      await goto(page, mode, { defaultOpen: true, modal: 'trap-focus', inside: 'remove' });
      await expect(page.getByTestId('remove')).toBeFocused();

      await page.getByTestId('remove').hover();
      await page.mouse.down();
      await expect(page.getByTestId('popover-popup')).toBeFocused();
      await page.mouse.up();

      await page.mouse.click(5, 5);

      await expect(page.getByRole('dialog')).toHaveCount(0);
    });

    test.describe('prop: modal', () => {
      test('should only render focus guards inside the popup when `true`', async ({ page, mode }) => {
        await goto(page, mode, { modal: true, inside: 'close' });
        const trigger = page.getByTestId('trigger');

        await trigger.click();
        await expect(page.getByRole('dialog')).toBeVisible();

        const siblings = await trigger.evaluate((element) => [element.previousElementSibling, element.nextElementSibling]
          .map((sibling) => sibling?.hasAttribute('data-base-ui-focus-guard') ?? false));
        expect(siblings).toEqual([false, false]);
        await expect(page.locator('[data-base-ui-focus-guard][data-type="inside"]')).toHaveCount(2);
      });

      test('should keep trigger focus guards when `true` without a close part', async ({ page, mode }) => {
        await goto(page, mode, { defaultOpen: true, modal: true, inside: 'input', afterTrigger: 'focus-target' });
        const trigger = page.getByTestId('trigger');
        await expect(page.getByTestId('popover-popup')).toBeVisible();
        const siblings = await trigger.evaluate((element) => [element.previousElementSibling, element.nextElementSibling]
          .map((sibling) => sibling?.hasAttribute('data-base-ui-focus-guard') ?? false));
        expect(siblings).toEqual([true, true]);

        await expect(page.getByTestId('input-inside')).toBeFocused();
        await page.keyboard.press('Tab');

        await expect(page.getByTestId('focus-target')).toBeFocused();
        await expect(page.getByTestId('popover-popup')).toHaveCount(0);
      });
    });
  });
}

// PopoverClose.test.tsx's modal titles and PopoverPopup.test.tsx's display:none title, on `popover-root`'s contained layout.
test.describe('<Popover.Close /> modal focus management', () => {
  const isElementOrAncestorInert = (locator: Locator) => locator.evaluate((element) => {
    for (let current: Element | null = element; current; current = current.parentElement) {
      if (current.getAttribute('aria-hidden') === 'true' || current.hasAttribute('inert')) {
        return true;
      }
    }
    return false;
  });

  for (const modal of [true, 'trap-focus'] as const) {
    test(`enables modal focus management when \`modal=${modal}\` and close is rendered`, async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'popover-root', { outside: 'before', defaultOpen: true, modal, inside: 'close-labeled' });

      await expect.poll(() => isElementOrAncestorInert(page.getByTestId('outside'))).toBe(true);
    });
  }

  test('does not enable modal focus management when close is not rendered', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-root', { outside: 'before', defaultOpen: true, modal: true });
    await expect(page.getByText('Content', { exact: true })).toBeVisible();
    await settle(fixture);

    expect(await isElementOrAncestorInert(page.getByTestId('outside'))).toBe(false);
  });
});

test.describe('<Popover.Popup /> focus', () => {
  test('focuses the popup when the active element becomes display:none', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-root', { defaultOpen: true, inside: 'hide' });
    await expect(page.getByTestId('hide-button')).toBeFocused();

    await page.getByTestId('hide-button').click();

    await expect(page.getByTestId('popover-popup')).toBeFocused();
  });
});

// PopoverRoot.test.tsx's touch scroll-lock titles (POP-PTR-14/19, POP-EDGE-14): a modal root of their own, here
// `popover-root`'s contained layout, opened by a real tap in a context with touch.
test.describe('<Popover.Root /> prop: modal, touch', () => {
  touchTest('applies scroll lock when a touch-opened popup covers the viewport width', async ({ touchPage: page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-root', { modal: true, positionerWidth: 'full' });

    await page.getByTestId('trigger').tap();
    await expect(page.getByTestId('popover-popup')).toBeVisible();

    await expect.poll(() => isScrollLocked(page)).toBe(true);
  });

  touchTest('does not apply scroll lock when a touch-opened popup is narrower than the viewport', async ({ touchPage: page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popover-root', { modal: true, positionerWidth: '240px' });

    await page.getByTestId('trigger').tap();
    await expect(page.getByTestId('popover-popup')).toBeVisible();
    await settle(fixture);
    await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));

    expect(await isScrollLocked(page)).toBe(false);
  });
});

// Gap 14 (M4 track 5, raised 2026-10-29): with focus in a non-modal popup, a real press on a focusable outside element
// moves focus first, so the focus manager closes with focus-out and the press's outside-press finds the popup closed, one
// OnOpenChange as in Base UI (WebAssembly). On Server both close requests leave before the first commit returns, and
// OnOpenChange runs again, with outside-press, for a popover already closed.
test.describe('<Popover.Root /> a press outside that moves focus', () => {
  test('port: a real press on a focusable outside element closes once, with focus-out', async ({ page, mode }) => {
    test.skip(mode === 'server', 'gap 14: on Server the outside-press request reaches OnOpenChange after the focus-out close');
    const fixture = await gotoPopupFixture(page, mode, 'popover-root', { defaultOpen: true, inside: 'button', outside: 'after' });
    await expect(page.getByTestId('inside')).toBeFocused();

    await page.getByTestId('outside').click();

    await expect(page.getByRole('dialog')).toHaveCount(0);
    await settle(fixture);
    expect(await openChanges(fixture)).toEqual(['false:focus-out']);
  });
});

// PopoverRoot.test.tsx's nested popup interactions that render their own roots, on `popover-nested`.
test.describe('<Popover.Root /> nested popup interactions', () => {
  test('returns focus through nested programmatic popovers in close order', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'popover-nested', { case: 'programmatic' });
    const parentTrigger = page.getByRole('button', { name: 'Parent trigger' });

    await parentTrigger.click();
    await expect(page.getByTestId('parent-popup')).toBeVisible();

    const childOpener = page.getByRole('button', { name: 'Open child programmatically' });
    await childOpener.click();
    await expect(page.getByTestId('child-popup')).toBeVisible();

    await page.getByRole('button', { name: 'Close child' }).click();
    await expect(page.getByTestId('child-popup')).toHaveCount(0);
    await expect(childOpener).toBeFocused();
    await expect(page.getByTestId('parent-popup')).toBeVisible();

    await page.getByRole('button', { name: 'Close parent' }).click();
    await expect(page.getByTestId('parent-popup')).toHaveCount(0);
    await expect(parentTrigger).toBeFocused();
  });

  test('keeps the parent popover open when press starts in nested popover and ends outside', async ({ page, mode }) => {
    // Gap 15 (M4 track 5, raised 2026-10-29): the parent closes with outside-press on both modes. Base UI's child popup
    // is in the parent's React tree, so its pointerdown reaches the parent's capture handlers and sets FLT-PTR-29's
    // press-started-inside marker; the interactions module listens on the DOM, where the child's portal is outside it.
    test.skip(true, 'gap 15: a press that starts in a nested popover\'s popup is not one that started inside the parent (interactions)');
    const fixture = await gotoPopupFixture(page, mode, 'popover-nested', { case: 'press' });
    await expect(page.getByTestId('parent-popup')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-child-attached', 'true');

    await page.getByRole('button', { name: 'Child', exact: true }).click();
    const childPopup = page.getByTestId('child-popup');
    await expect(childPopup).toBeVisible();

    // A real press starts in the child popup and is released on the outside button.
    await childPopup.hover();
    await page.mouse.down();
    await page.getByTestId('outside').hover();
    await page.mouse.up();
    await settle(fixture);

    await expect(page.getByTestId('parent-popup')).toBeVisible();
    await expect(childPopup).toBeVisible();
  });
});
