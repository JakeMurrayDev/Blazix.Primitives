import { type Locator, type Route } from '@playwright/test';
import { expect, Fixture, fixtureUrl, test, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture, openChangeCompletes, pressControl, settle, type PopupFixtureOptions } from './suites/popupFixture';

// The [L4] rows of docs/components/navigation-menu.md (M5 track 5, issue 163) on the `navigation-menu-*` fixtures, every
// title on both render modes: Base UI's titles (NavigationMenuRoot.test.tsx, NavigationMenuTrigger.test.tsx,
// NavigationMenuContent.test.tsx, NavigationMenuList.test.tsx) with real input, and port titles for the Content move (the
// probe's cases on the real parts, ADR 04 Amendments 2026-11-03 (4)) and the attach order.

const FIXTURE = 'navigation-menu-basic';

async function gotoMenu(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
  return gotoPopupFixture(page, mode, FIXTURE, options);
}

/** The Root's recorded value changes, as `value:reason` entries. */
async function valueChanges(fixture: Locator): Promise<string[]> {
  const list = (await fixture.getAttribute('data-value-changes')) ?? '';
  return list.split(' ').filter((entry) => entry.length > 0);
}

const trigger = (page: Page, n: number) => page.getByTestId(`trigger-${n}`);
const content = (page: Page, n: number) => page.getByTestId(`content-${n}`);

/**
 * Base UI's `fireEvent.click`: a click with no pointer movement. A real pointer reaches another trigger before it clicks,
 * so the hover switches first (NAV-PTR-18) and the click then toggles the now-active item closed; the titles that click
 * a different trigger while open use this.
 */
async function fireClick(locator: Locator): Promise<void> {
  await locator.dispatchEvent('click');
}

// navigation-menu/utils/constants.ts (OPEN_DELAY, CLOSE_DELAY) and the Trigger's PATIENT_CLICK_THRESHOLD.
const OPEN_DELAY = 50;
const CLOSE_DELAY = 50;
const PATIENT_CLICK_THRESHOLD = 500;

/** Rests a real mouse on an element: two moves inside it, so a trigger's rest timer starts (NAV-PTR-01). */
async function hoverElement(page: Page, locator: Locator): Promise<void> {
  const box = (await locator.boundingBox())!;
  await page.mouse.move(box.x + 4, box.y + 4);
  await page.mouse.move(box.x + 10, box.y + 8);
}

/** Rests a real mouse on a top-level trigger. */
async function hoverTrigger(page: Page, n: number): Promise<void> {
  await hoverElement(page, trigger(page, n));
}

/** Moves the mouse in one step to an empty corner of the page and once more inside it, away from the triggers and the popup. */
async function leaveMenu(page: Page): Promise<void> {
  const size = page.viewportSize()!;
  await page.mouse.move(size.width - 20, size.height - 20);
  await page.mouse.move(size.width - 10, size.height - 10);
}

const FREEZE_MARGIN = 50;
const frozenPages = new WeakSet<Page>();

/**
 * Pauses the page's clock (the Tooltip spec's helper): its timers fire only when the test runs it forward. The page's clock
 * runs on while the target travels to the browser, so the target sits a margin ahead, read again if the clock passed it
 * on a slow runner. A title that asserts a timer has not fired yet freezes first: a running clock counts the real time a
 * Server round trip takes.
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

// The clock is the browser context's, which the worker's pages share: a paused one would leave the next title's page
// frozen, and a WebAssembly page then never starts.
test.afterEach(async ({ page }) => {
  if (frozenPages.has(page)) {
    await page.clock.resume();
  }
});

/** Presses and releases the real pointer where it rests: Base UI's titles dispatch the click alone, with no move before it. */
async function clickInPlace(page: Page): Promise<void> {
  await page.mouse.down();
  await page.mouse.up();
}

interface AnimationState {
  name: string;
  testId: string | null;
  endingStyle: boolean;
  activationDirection: string | null;
  sizes: Record<string, string>;
}

/**
 * Records every `animationstart` from now on with the animated element's state and the four size variables at that
 * moment: a state that lasts an exit animation is witnessed by a record, never by a poll of the live style (the popup
 * suites' rule, tests/browser/CONTEXT.md).
 */
async function recordAnimationStates(page: Page): Promise<void> {
  await page.evaluate(() => {
    const records: unknown[] = [];
    (window as unknown as { navAnimationStates: unknown[] }).navAnimationStates = records;
    const read = (testId: string, name: string) => (document.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null)?.style.getPropertyValue(name) ?? '';
    document.addEventListener(
      'animationstart',
      (event) => {
        const target = event.target as HTMLElement;
        records.push({
          name: (event as AnimationEvent).animationName,
          testId: target.getAttribute('data-testid'),
          endingStyle: target.hasAttribute('data-ending-style'),
          activationDirection: target.getAttribute('data-activation-direction'),
          sizes: {
            popupWidth: read('popup-root', '--popup-width'),
            popupHeight: read('popup-root', '--popup-height'),
            positionerWidth: read('positioner', '--positioner-width'),
            positionerHeight: read('positioner', '--positioner-height'),
          },
        });
      },
      true,
    );
  });
}

/** The animation starts recorded since recordAnimationStates with the given name. */
async function animationStates(page: Page, name: string): Promise<AnimationState[]> {
  const records = await page.evaluate(() => (window as unknown as { navAnimationStates?: AnimationState[] }).navAnimationStates ?? []);
  return records.filter((record) => record.name === name);
}

/** A pixel length of an element's inline custom property, as a number. */
function pixels(locator: Locator, property: string): Promise<number> {
  return locator.evaluate((element: HTMLElement, name) => parseFloat(element.style.getPropertyValue(name)), property);
}

// Touch titles: Playwright taps only in a context made with `hasTouch`, and the worker's shared context is not, so each
// touch title opens its own (on `wasm` it downloads the runtime again); the Popover spec's precedent.
const touchTest = test.extend<{ touchPage: Page }>({
  touchPage: async ({ browser }, use) => {
    const context = await browser.newContext({ hasTouch: true });
    const page = await context.newPage();
    await page.clock.install();
    await use(page);
    await context.close();
  },
});

test.describe('<NavigationMenu.Root /> interactions', () => {
  test('opens on click with mouse input', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);

    await trigger(page, 1).click();

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(content(page, 1)).toBeVisible();
    await settle(fixture);
    expect(await valueChanges(fixture)).toEqual(['item-1:trigger-press']);
  });

  test('opens on hover with mouse input', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);

    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await settle(fixture);
    expect(await valueChanges(fixture)).toEqual(['item-1:trigger-hover']);
  });

  test('does not close menu when clicking a different trigger with mouse', async ({ page, mode }) => {
    await gotoMenu(page, mode);

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await fireClick(trigger(page, 2));

    await expect(content(page, 1)).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(content(page, 2)).toBeVisible();
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
  });

  test('respects focus outside when clicking menu', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toBeFocused();

    await page.getByTestId('after').click();

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(page.getByTestId('after')).toBeFocused();
  });

  test('does not open via keyboard when the trigger is disabled', async ({ page, mode }) => {
    await gotoMenu(page, mode, { disabled: 1 });

    await trigger(page, 1).focus();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');

    await expect(trigger(page, 1)).toBeFocused();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });
});

test.describe('<NavigationMenu.Root /> delays and the patient click', () => {
  test('respects custom delay value', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { delay: 100 });
    await freezeClock(page);

    await hoverTrigger(page, 1);
    await page.clock.runFor(75);
    await settle(fixture);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');

    await page.clock.runFor(50);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(content(page, 1)).toBeVisible();
  });

  test('respects custom closeDelay value', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { closeDelay: 100 });
    await freezeClock(page);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();

    await leaveMenu(page);
    await page.clock.runFor(75);
    await settle(fixture);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await page.clock.runFor(50);
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });

  test('stays open if hovered then clicked within the patient threshold', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();

    await clickInPlace(page);
    await settle(fixture);

    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });

  test('closes if hovered then clicked after the patient threshold', async ({ page, mode }) => {
    // Open on Server, a gap reported to the orchestrator 2026-11-04: the Trigger's click role is off between the open
    // and the Positioner's registration a batch later (Base UI's interactionsEnabled), and the click role's last
    // uninstall clears the session's patient timer, so stickIfOpen stays on. Base UI keeps that timer on the Trigger.
    test.fixme(mode === 'server', 'the patient timer is cleared while the click role is off (gap to track 2)');
    const fixture = await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();
    // The patient window starts when C#'s relay of the hover open reaches the session, after the render that shows it.
    await settle(fixture);

    await page.clock.runFor(PATIENT_CLICK_THRESHOLD);
    await clickInPlace(page);

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });

  test('restores hover open after a quick click then trigger switch', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();

    await clickInPlace(page);
    await settle(fixture);
    await hoverTrigger(page, 2);
    await expect(content(page, 2)).toBeVisible();

    await leaveMenu(page);
    await page.clock.runFor(CLOSE_DELAY + 10);
    await expect(page.getByTestId('popup-root')).toHaveCount(0);

    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();
  });

  test('does not restore focus to the trigger when closed via hover', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();

    await leaveMenu(page);
    await page.clock.runFor(CLOSE_DELAY + 10);

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(trigger(page, 1)).not.toBeFocused();
  });
});

test.describe('<NavigationMenu.Root /> value', () => {
  test('should be controlled by value prop', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { value: 'item-1' });
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await pressControl(fixture.locator('[data-set-value="item-2"]'));
    await hoverTrigger(page, 2);

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
  });

  test('should call onValueChange when value changes', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await settle(fixture);
    expect((await valueChanges(fixture)).map((entry) => entry.split(':')[0])).toEqual(['item-1']);

    await fireClick(trigger(page, 2));
    await expect(content(page, 2)).toBeVisible();
    await settle(fixture);
    expect((await valueChanges(fixture)).map((entry) => entry.split(':')[0])).toEqual(['item-1', 'item-2']);
  });

  test('closes through the `close` action so `onValueChange` can opt out', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { defaultValue: 'item-1' });
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await pressControl(fixture.locator('[data-close]'));

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect(await valueChanges(fixture)).toEqual(['null:imperative-action']);
  });

  test('closes the menu when clicking a link when true', async ({ page, mode }) => {
    await gotoMenu(page, mode, { closeOnClick: true });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await page.getByTestId('link-1-a').click();

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });
});

test.describe('<NavigationMenu.Trigger /> disabled', () => {
  test('does not open on hover when the trigger is disabled', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { disabled: 1 });

    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await settle(fixture);

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    expect(await valueChanges(fixture)).toEqual([]);
  });

  test('does not open on click when the trigger is disabled', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { disabled: 1 });

    // The disabled trigger stays focusable (aria-disabled), which Playwright's actionability check reads as disabled.
    await trigger(page, 1).click({ force: true });
    await settle(fixture);

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    expect(await valueChanges(fixture)).toEqual([]);
  });

  touchTest('does not open on touch when the trigger is disabled', async ({ touchPage: page, mode }) => {
    const fixture = await gotoMenu(page, mode, { disabled: 1 });

    await trigger(page, 1).tap({ force: true });
    await settle(fixture);

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    expect(await valueChanges(fixture)).toEqual([]);
  });

  test('applies the data-disabled style hook only when disabled', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { disabled: 1 });
    await expect(trigger(page, 1)).toHaveAttribute('data-disabled', '');

    await pressControl(fixture.locator('[data-enable]'));

    await expect(trigger(page, 1)).not.toHaveAttribute('data-disabled');
  });
});

test.describe('<NavigationMenu.Root /> touch', () => {
  test('does not open on hover with touch input', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);

    // A real tap always clicks; Base UI's title dispatches a touch pointerenter and the mouse events without one.
    await trigger(page, 1).dispatchEvent('pointerenter', { pointerType: 'touch' });
    await trigger(page, 1).dispatchEvent('mouseenter');
    await trigger(page, 1).dispatchEvent('mousemove');
    await page.clock.runFor(OPEN_DELAY + 10);
    await settle(fixture);

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });

  touchTest('opens on click with touch input', async ({ touchPage: page, mode }) => {
    await gotoMenu(page, mode);

    await trigger(page, 1).tap();

    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });

  touchTest('does not close menu when clicking a different trigger on touch', async ({ touchPage: page, mode }) => {
    await gotoMenu(page, mode);
    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();

    await trigger(page, 2).tap();

    await expect(content(page, 1)).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(content(page, 2)).toBeVisible();
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
  });

  touchTest('restores hover open after a touch click closes outside', async ({ touchPage: page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).tap();
    await expect(content(page, 1)).toBeVisible();

    await page.getByTestId('after').click();
    await expect(page.getByTestId('popup-root')).toHaveCount(0);

    await hoverTrigger(page, 2);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 2)).toBeVisible();
  });
});

test.describe('<NavigationMenu.Trigger /> direction', () => {
  test('opens a vertical menu with the mirrored arrow key in RTL mode', async ({ page, mode }) => {
    await gotoMenu(page, mode, { orientation: 'vertical', rtl: true });

    await trigger(page, 1).focus();
    await page.keyboard.press('ArrowLeft');

    await expect(page.getByTestId('link-1-a')).toBeVisible();
  });
});

test.describe('<NavigationMenu.Root /> actions', () => {
  test('ignores `unmount` while the popup is open', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { defaultValue: 'item-1' });
    const popup = await page.getByTestId('popup-root').elementHandle();

    await pressControl(fixture.locator('[data-unmount]'));
    await settle(fixture);

    expect(await popup!.evaluate((element) => element.isConnected)).toBe(true);
    expect(await openChangeCompletes(fixture)).not.toContain('false');
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });

  test('automatically unmounts with an actions ref and completes closing once', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { value: 'item-1' });
    await expect(page.getByTestId('popup-root')).toHaveCount(1);

    await pressControl(fixture.locator('[data-set-value="none"]'));
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await settle(fixture);
    expect((await openChangeCompletes(fixture)).filter((open) => open === 'false')).toHaveLength(1);

    // Calling the action after the automatic unmount must not repeat the completion.
    await pressControl(fixture.locator('[data-unmount]'));
    await settle(fixture);
    expect((await openChangeCompletes(fixture)).filter((open) => open === 'false')).toHaveLength(1);
  });

  test('keeps the popup mounted until the unmount action completes closing', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { value: 'item-1', fixed: true, retain: true });
    await expect(page.getByTestId('popup-root')).toHaveCount(1);

    // The close action runs onValueChange, which opts out; the page keeps its value, so the menu stays open.
    await pressControl(fixture.locator('[data-close]'));
    await settle(fixture);
    await expect(page.getByTestId('popup-root')).toHaveCount(1);

    await pressControl(fixture.locator('[data-set-value="none"]'));
    await settle(fixture);
    await expect(page.getByTestId('popup-root')).toHaveCount(1);
    expect(await openChangeCompletes(fixture)).not.toContain('false');

    await pressControl(fixture.locator('[data-unmount]'));

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await settle(fixture);
    expect((await openChangeCompletes(fixture)).filter((open) => open === 'false')).toHaveLength(1);
  });

  test('clears activation direction when controlled value closes externally after switching triggers', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { value: 'item-1', contentFade: true, fade: true, animations: 1 });
    await expect(content(page, 1)).toBeVisible();

    await fireClick(trigger(page, 2));
    await expect(content(page, 2)).toHaveAttribute('data-activation-direction', 'right');
    await recordAnimationStates(page);

    await pressControl(fixture.locator('[data-set-value="none"]'));

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    const exits = (await animationStates(page, 'nav-content-out')).filter((record) => record.testId === 'content-2');
    expect(exits).toHaveLength(1);
    expect(exits[0]).toMatchObject({ endingStyle: true, activationDirection: null });
  });
});

test.describe('<NavigationMenu.Link /> and the guards', () => {
  test('keeps the menu open when a link loses focus without a related target', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);
    await trigger(page, 1).click();
    const link = page.getByTestId('link-1-a');
    await expect(link).toBeVisible();

    await link.focus();
    await link.evaluate((element: HTMLElement) => element.blur());
    await settle(fixture);

    await expect(link).toBeAttached();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });

  test('returns focus to the trigger when no viewport focus guard is rendered', async ({ page, mode }) => {
    await gotoMenu(page, mode, { noViewport: true });
    await trigger(page, 1).focus();
    await trigger(page, 1).click();
    await expect(page.getByTestId('popup-root')).toBeVisible();

    const guards = page.locator('li:has(> [data-testid="trigger-1"]) > [data-base-ui-focus-guard]');
    await expect(guards).toHaveCount(2);
    await guards.nth(1).focus();

    await expect(trigger(page, 1)).toBeFocused();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });
});

test.describe('<NavigationMenu.Popup /> anchoring', () => {
  for (const [side, rtl] of [['left', false], ['inline-start', false], ['inline-end', true]] as const) {
    test(`pins the popup to the right edge when side is ${side}${rtl ? ' in RTL' : ''} so it grows leftward`, async ({ page, mode }) => {
      await gotoMenu(page, mode, { side, rtl: rtl || undefined });
      await trigger(page, 1).click();
      const popup = page.getByTestId('popup-root');
      await expect(popup).toBeVisible();

      await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', side);
      expect(await popup.evaluate((element: HTMLElement) => [element.style.position, element.style.top, element.style.right, element.style.left, element.style.bottom])).toEqual(['absolute', '0px', '0px', '', '']);
    });
  }

  test('port: pins the popup to the bottom edge when side is top so it grows upward', async ({ page, mode }) => {
    await gotoMenu(page, mode, { side: 'top' });
    await trigger(page, 1).click();
    const popup = page.getByTestId('popup-root');
    await expect(popup).toBeVisible();

    await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'top');
    expect(await popup.evaluate((element: HTMLElement) => [element.style.position, element.style.bottom, element.style.left, element.style.top, element.style.right])).toEqual(['absolute', '0px', '0px', '', '']);
  });

  test('disables popup and arrow transitions while a kept portal opens', async ({ page, mode }) => {
    await gotoMenu(page, mode, { keepMounted: true, arrow: true });
    await freezeClock(page);
    const popup = page.getByTestId('popup-root');
    const arrowPart = page.getByTestId('arrow');

    await trigger(page, 1).click();

    await expect.poll(() => popup.evaluate((element: HTMLElement) => element.style.transition)).toBe('none');
    await expect.poll(() => arrowPart.evaluate((element: HTMLElement) => element.style.transition)).toBe('none');

    await page.clock.runFor(64);
    await expect.poll(() => popup.evaluate((element: HTMLElement) => element.style.transition)).not.toBe('none');
    expect(await arrowPart.evaluate((element: HTMLElement) => element.style.transition)).not.toBe('none');
  });
});

test.describe('<NavigationMenu.Root /> nested', () => {
  const nestedTrigger = (page: Page, n: number) => page.getByTestId(`nested-trigger-${n}`);
  const nestedContent = (page: Page, n: number) => page.getByTestId(`nested-content-${n}`);

  async function hoverOpen(page: Page, locator: Locator): Promise<void> {
    await hoverElement(page, locator);
    await page.clock.runFor(OPEN_DELAY + 10);
  }

  test('handles inline nested menu without positioner/popup correctly', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'inline', nestedDefault: 'nested-1' });
    await hoverOpen(page, trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await hoverOpen(page, nestedTrigger(page, 1));
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await hoverElement(page, nestedContent(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await hoverOpen(page, nestedTrigger(page, 2));
    await expect(nestedContent(page, 2)).toBeVisible();
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });

  test('keeps parent menu open when hovering inline nested triggers without defaultValue', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'inline' });
    await hoverOpen(page, trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'false');

    await hoverOpen(page, nestedTrigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    // A real pointer crosses the nested panel first, which releases the inline lock on the nested List (NAV-PTR-13).
    await hoverElement(page, nestedContent(page, 1));
    await hoverOpen(page, nestedTrigger(page, 2));
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 2)).toBeVisible();
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
  });

  test('switches content in viewport when hovering different nested triggers', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'inline', nestedDefault: 'nested-1' });
    await hoverOpen(page, trigger(page, 1));
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 1)).toBeVisible();

    await hoverOpen(page, nestedTrigger(page, 2));
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(nestedContent(page, 2)).toBeVisible();
    await expect(nestedContent(page, 1)).toHaveCount(0);

    await hoverOpen(page, nestedTrigger(page, 1));
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'false');
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(nestedContent(page, 2)).toHaveCount(0);
  });

  test('maintains inline viewport state when hovering between triggers and content', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { nested: 'inline', nestedDefault: 'nested-1' });
    await hoverOpen(page, trigger(page, 1));
    await expect(nestedContent(page, 1)).toBeVisible();
    await hoverOpen(page, nestedTrigger(page, 2));
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 2)).toBeVisible();

    await hoverElement(page, nestedContent(page, 2));
    await settle(fixture);
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 2)).toBeVisible();

    await hoverElement(page, nestedTrigger(page, 2));
    await settle(fixture);
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 2)).toBeVisible();
  });

  // Open, under investigation (M5 track 5, 2026-11-04): with the inline nested menu open by default, leaving the trigger
  // and the popup keeps the parent open on both modes, with real input and with Base UI's dispatched leaves alike. The
  // parent's safe polygon aborts while a child node is open (safePolygon.ts:177); which path closes it in Base UI's run
  // is not traced yet.
  test.fixme('closes inline nested viewport when parent menu closes', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'inline', nestedDefault: 'nested-1' });
    await hoverOpen(page, trigger(page, 1));
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 1)).toBeVisible();

    // Base UI leaves the trigger and the popup; the real pointer crosses the popup on its way out.
    await hoverElement(page, page.getByTestId('link-1-a'));
    await leaveMenu(page);
    await page.clock.runFor(CLOSE_DELAY + 10);

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(nestedContent(page, 1)).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });

  test('handles click interactions on inline nested menu triggers', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'inline', nestedDefault: 'nested-1' });
    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await fireClick(nestedTrigger(page, 2));
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(nestedContent(page, 2)).toBeVisible();
    await expect(nestedContent(page, 1)).toHaveCount(0);

    await fireClick(nestedTrigger(page, 2));
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(nestedContent(page, 2)).toBeVisible();
    await expect(nestedContent(page, 1)).toHaveCount(0);
  });

  test('allows arrow key navigation to submenu triggers', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'inline', nestedDefault: 'nested-1' });
    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    // Base UI's Content has one Link before the nested Root; this one has two, so the walk starts from the second.
    const link = page.getByTestId('link-1-b');
    await link.focus();

    await page.keyboard.press('ArrowDown');
    await expect(nestedTrigger(page, 1)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(nestedTrigger(page, 2)).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(nestedTrigger(page, 1)).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(link).toBeFocused();
  });

  test('allows arrow key navigation with 3+ levels of nesting', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'deep' });
    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    const level2Link = page.getByTestId('level2-link-1');
    await expect(page.getByTestId('level3-content-1')).toBeVisible();

    // Base UI's Content has one Link before the nested Root; this one has two, so the walk starts from the second.
    const link = page.getByTestId('link-1-b');
    await link.focus();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('level2-trigger-1')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('level2-trigger-2')).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(page.getByTestId('level2-trigger-1')).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(link).toBeFocused();

    await level2Link.focus();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('level3-trigger-1')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByTestId('level3-trigger-2')).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(page.getByTestId('level3-trigger-1')).toBeFocused();
    await page.keyboard.press('ArrowUp');
    await expect(level2Link).toBeFocused();
  });

  test('closes all levels when a deeply nested link with closeOnClick is clicked', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { nested: 'deep', closeOnClick: true });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await expect(page.getByTestId('level2-content-1')).toBeVisible();
    await expect(page.getByTestId('level3-content-1')).toBeVisible();

    await page.getByTestId('level3-link-1').click();

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(page.getByTestId('level2-content-1')).toHaveCount(0);
    await expect(page.getByTestId('level3-content-1')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect((await valueChanges(fixture)).at(-1)).toBe('null:link-press');
  });

  test('opens nested menu on hover and stays open when hovering over nested popup', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'portal' });
    await hoverOpen(page, trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await hoverOpen(page, nestedTrigger(page, 1));
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await hoverElement(page, page.getByTestId('nested-popup-root'));
    await page.clock.runFor(CLOSE_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });

  test('closes the parent menu after a nested submenu closes on delayed hover-out', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'portal', closeDelay: 200 });
    await hoverOpen(page, trigger(page, 1));
    await hoverOpen(page, nestedTrigger(page, 1));
    await expect(nestedContent(page, 1)).toBeVisible();

    await leaveMenu(page);
    await page.clock.runFor(210);

    await expect(nestedContent(page, 1)).toHaveCount(0);
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });

  test('closes the parent menu when a nested link with closeOnClick is clicked', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'portal', closeOnClick: true });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await hoverOpen(page, nestedTrigger(page, 1));
    await expect(nestedContent(page, 1)).toBeVisible();

    await page.getByTestId('nested-link-1').click();

    await expect(nestedContent(page, 1)).toHaveCount(0);
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });

  test('calls onValueChange on the parent root when nested closeOnClick link is clicked', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { nested: 'portal', closeOnClick: true });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await hoverOpen(page, nestedTrigger(page, 1));
    await expect(nestedContent(page, 1)).toBeVisible();

    await page.getByTestId('nested-link-1').click();

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await settle(fixture);
    expect((await valueChanges(fixture)).at(-1)).toBe('null:link-press');
  });
});

test.describe('<NavigationMenu.Content /> move', () => {
  test('moves content into the popup and keeps it there when switching triggers', async ({ page, mode }) => {
    await gotoMenu(page, mode);

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    const first = await content(page, 1).elementHandle();
    expect(await page.getByTestId('viewport').evaluate((viewport, element) => viewport.contains(element), first)).toBe(true);
    expect(await page.getByTestId('list').evaluate((list, element) => list.contains(element), first)).toBe(false);

    await fireClick(trigger(page, 2));
    await expect(content(page, 2)).toBeVisible();
    expect(await page.getByTestId('viewport').evaluate((viewport) => viewport.contains(document.querySelector('[data-testid="content-2"]')))).toBe(true);

    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    expect(await page.getByTestId('viewport').evaluate((viewport) => viewport.contains(document.querySelector('[data-testid="content-1"]')))).toBe(true);
  });

  test('keeps content mounted inside the popup when closed if the portal is kept mounted', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { keepMounted: true, keepMountedContent: true });

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await page.keyboard.press('Escape');

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(content(page, 1)).toBeAttached();
    await expect(content(page, 1)).toBeHidden();
    expect(await page.getByTestId('viewport').evaluate((viewport) => viewport.contains(document.querySelector('[data-testid="content-1"]')))).toBe(true);
    await settle(fixture);
    expect(await openChangeCompletes(fixture)).toEqual(['false']);
  });

  test('keeps the content mounted (hidden) post-hydration when keepMounted is true', async ({ page, mode }) => {
    await gotoMenu(page, mode, { keepMountedContent: true });

    // No Viewport target before the first open: the kept content renders inline in its Item, hidden.
    await expect(content(page, 1)).toBeAttached();
    await expect(content(page, 1)).toBeHidden();
    expect(await page.getByTestId('list').evaluate((list) => list.contains(document.querySelector('[data-testid="content-1"]')))).toBe(true);

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    expect(await page.getByTestId('viewport').evaluate((viewport) => viewport.contains(document.querySelector('[data-testid="content-1"]')))).toBe(true);
  });

  test('port: a latched kept content follows a portal that unmounts and mounts again, and leaves nothing in the body', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { keepMountedContent: true });

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await settle(fixture);

    // The target left with the portal: the latched content renders nothing, and nothing was moved into the body.
    await expect(content(page, 1)).toHaveCount(0);
    expect(await page.evaluate(() => [...document.body.children].some((child) => child.matches('[data-testid^="content-"]')))).toBe(false);

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    expect(await page.getByTestId('viewport').evaluate((viewport) => viewport.contains(document.querySelector('[data-testid="content-1"]')))).toBe(true);
  });
});

test.describe('<NavigationMenu.Trigger /> keyboard and focus', () => {
  for (const key of ['Enter', ' ', 'ArrowDown']) {
    test(`keeps focus on the trigger when opened with ${key === ' ' ? 'Space' : key}`, async ({ page, mode }) => {
      await gotoMenu(page, mode);

      await trigger(page, 1).focus();
      await page.keyboard.press(key);

      await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
      await expect(content(page, 1)).toBeVisible();
      await expect(trigger(page, 1)).toBeFocused();
    });
  }

  // Design point 4 (chosen by the user 2026-11-04): the open key asks `navigate` first. On WebAssembly the consumer's
  // onkeydown runs inside the point and its PreventPartHandler() stops the open; on Server the point falls back to `run`
  // (ADR 01, "C#-origin consumer vetoes"), and the consumer's handler still runs.
  test('port: a consumer onkeydown that calls PreventPartHandler() keeps the open key from opening on WebAssembly; on Server the key opens', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { vetoOpenKey: true });

    await trigger(page, 1).focus();
    await page.keyboard.press('ArrowDown');
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-veto-calls', '1');
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', mode === 'wasm' ? 'false' : 'true');
    await expect(trigger(page, 1)).toBeFocused();
  });

  test('does not emit a duplicate onValueChange when switching items via keyboard', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);

    await trigger(page, 1).focus();
    await page.keyboard.press('ArrowDown');
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await page.keyboard.press('ArrowRight');
    await expect(trigger(page, 2)).toBeFocused();
    await page.keyboard.press('ArrowDown');

    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await settle(fixture);
    expect(await valueChanges(fixture)).toEqual(['item-1:list-navigation', 'item-2:list-navigation']);
  });

  test('returns focus to trigger when closing menu', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await page.getByTestId('link-1-a').focus();

    await page.keyboard.press('Escape');

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toBeFocused();
  });

  // Open until track 1's Link registration without a tabindex lands (NAV-DOM-21, NAV-FOC-03): the Links render the
  // composite's roving tabindex=-1, so Tab skips them.
  test.fixme('moves focus through the menu correctly', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('link-1-a')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('link-1-b')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(trigger(page, 2)).toBeFocused();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await page.keyboard.press('Shift+Tab');
    await expect(page.getByTestId('link-1-b')).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(page.getByTestId('link-1-a')).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(trigger(page, 1)).toBeFocused();
  });

  // Open until track 1's Link registration without a tabindex lands (NAV-DOM-21, NAV-FOC-03): the Links render the
  // composite's roving tabindex=-1, so Tab skips them.
  test.fixme('closes the menu when tabbing forward out', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);
    await trigger(page, 2).click();
    await expect(content(page, 2)).toBeVisible();

    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    await expect(page.getByTestId('after')).toBeFocused();
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect(await valueChanges(fixture)).toContain('null:focus-out');
  });

  // Open until track 1's Link registration without a tabindex lands (NAV-DOM-21, NAV-FOC-03): the Links render the
  // composite's roving tabindex=-1, so Tab skips them.
  test.fixme('does not restore focus to the trigger when focus moves outside', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).focus();

    await trigger(page, 1).click();
    for (let i = 0; i < 4; i += 1) {
      await page.keyboard.press('Tab');
    }

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(page.getByTestId('after')).toBeFocused();
    await expect(trigger(page, 1)).not.toBeFocused();
  });

  test('closes the menu when tabbing back out', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();

    await page.keyboard.press('Shift+Tab');

    await expect(page.getByTestId('before')).toBeFocused();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect(await valueChanges(fixture)).toContain('null:focus-out');
  });
});

test.describe('<NavigationMenu.List />', () => {
  test('stops vertical navigation keys from escaping the list', async ({ page, mode }) => {
    await gotoMenu(page, mode, { orientation: 'vertical' });
    await page.evaluate(() => {
      const seen: string[] = [];
      (window as unknown as { escaped: string[] }).escaped = seen;
      document.addEventListener('keydown', (event) => seen.push(event.key));
    });

    await trigger(page, 1).focus();
    await page.keyboard.press('ArrowUp');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('PageDown');

    expect(await page.evaluate(() => (window as unknown as { escaped: string[] }).escaped)).toEqual(['PageDown']);
  });
});

test.describe('size', () => {
  test('handles focus and positioner height', async ({ page, mode }) => {
    await gotoMenu(page, mode, { sized: true });

    await trigger(page, 1).focus();
    await page.keyboard.press('ArrowDown');
    await expect(content(page, 1)).toBeVisible();

    const positioner = page.getByTestId('positioner');
    await expect.poll(() => positioner.evaluate((element) => parseFloat(element.style.getPropertyValue('--positioner-height')))).toBeCloseTo(200, 0);

    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('ArrowDown');
    await expect(content(page, 2)).toBeVisible();
    await expect.poll(() => positioner.evaluate((element) => parseFloat(element.style.getPropertyValue('--positioner-height')))).toBeCloseTo(150, 0);
  });

  for (const [orientation, forward, backward] of [['horizontal', 'right', 'left'], ['vertical', 'down', 'up']] as const) {
    test(`reports ${forward} activation direction when switching in either direction (${orientation})`, async ({ page, mode }) => {
      await gotoMenu(page, mode, orientation === 'vertical' ? { orientation } : {});
      await trigger(page, 1).click();
      await expect(content(page, 1)).toBeVisible();

      await fireClick(trigger(page, 2));
      await expect(content(page, 2)).toHaveAttribute('data-activation-direction', forward);

      await fireClick(trigger(page, 1));
      await expect(content(page, 1)).toHaveAttribute('data-activation-direction', backward);
    });
  }

  test('port: reports the activation direction when switching by hover', async ({ page, mode }) => {
    await gotoMenu(page, mode, { animations: 1 });
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(page.getByTestId('link-1-a')).toBeVisible();

    // Across the panel, which releases the List's lock, to the other trigger: on WebAssembly the hover interaction
    // switches inside the same dispatch, after the activation measured the direction.
    const link = (await page.getByTestId('link-1-b').boundingBox())!;
    await page.mouse.move(link.x + 2, link.y + 2);
    const next = (await trigger(page, 2).boundingBox())!;
    await page.mouse.move(next.x + 4, next.y + next.height - 3);
    await page.mouse.move(next.x + 10, next.y + 8);

    await expect(content(page, 2)).toHaveAttribute('data-activation-direction', 'right');
  });

  test('repositions the positioner when switching triggers via hover', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(page.getByTestId('link-1-a')).toBeVisible();
    const positioner = page.getByTestId('positioner');
    const firstLeft = (await positioner.boundingBox())!.x;

    // Base UI's pointer skips the pointer-events check; a real one crosses the panel first, which releases the
    // safe-polygon lock on the List (NAV-PTR-11), and then reaches the other trigger.
    const link = (await page.getByTestId('link-1-b').boundingBox())!;
    await page.mouse.move(link.x + 2, link.y + 2);
    await page.mouse.move(link.x + 4, link.y + 4);
    const next = (await trigger(page, 2).boundingBox())!;
    await page.mouse.move(next.x + 4, next.y + next.height - 3);
    await page.mouse.move(next.x + 10, next.y + 8);
    await expect(page.getByTestId('link-2-a')).toBeVisible();

    await expect.poll(async () => Math.abs((await positioner.boundingBox())!.x - firstLeft)).toBeGreaterThan(20);
  });

  test('updates popup sizing immediately when switching to a keepMounted trigger', async ({ page, mode }) => {
    await gotoMenu(page, mode, { sized: true, keepMountedContent: true });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    const positioner = page.getByTestId('positioner');
    await expect.poll(() => pixels(positioner, '--positioner-height')).toBeCloseTo(200, 0);

    await fireClick(trigger(page, 2));

    await expect(content(page, 2)).toBeVisible();
    await expect.poll(() => pixels(positioner, '--positioner-height')).toBeCloseTo(150, 0);
    await expect.poll(() => pixels(positioner, '--positioner-width')).toBeCloseTo(300, 0);
  });

  test('closes on the short exit path after switching content', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { sized: true, scopedExit: true, animations: 1 });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await fireClick(trigger(page, 2));
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    const popup = page.getByTestId('popup-root');
    await expect.poll(() => popup.evaluate((element) => element.getAnimations().some((animation) => animation.playState !== 'finished'))).toBe(false);
    await settle(fixture);
    expect(await openChangeCompletes(fixture)).toEqual([]);
    await trigger(page, 2).focus();
    // The close's duration, from the Escape to the recorded completion, measured in the page.
    await fixture.evaluate((container) => {
      const record = { start: 0, end: 0 };
      (window as unknown as { shortExit: typeof record }).shortExit = record;
      document.addEventListener('keydown', () => (record.start ||= performance.now()), true);
      new MutationObserver(() => {
        if (container.getAttribute('data-open-change-completes')?.includes('false')) {
          record.end ||= performance.now();
        }
      }).observe(container, { attributes: true, attributeFilter: ['data-open-change-completes'] });
    });

    await page.keyboard.press('Escape');

    await expect.poll(() => openChangeCompletes(fixture)).toEqual(['false']);
    // Base UI's bound (NavigationMenuRoot.test.tsx:4310): the scoped exit is 150 ms, and the 350 ms size transitions,
    // which the ending style drops, would exceed it. Measured in the page, from the Escape's keydown to the completion's
    // attribute write, so the test runner's speed does not count.
    const { start, end } = await page.evaluate(() => (window as unknown as { shortExit: { start: number; end: number } }).shortExit);
    expect(end - start).toBeLessThan(325);
  });

  test('preserves popup size when controlled value closes externally', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { value: 'item-1', sized: true, fade: true, animations: 1 });
    await expect(content(page, 1)).toBeVisible();
    await expect.poll(() => pixels(page.getByTestId('positioner'), '--positioner-width')).toBeCloseTo(400, 0);
    await recordAnimationStates(page);

    await pressControl(fixture.locator('[data-set-value="none"]'));

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    const exits = await animationStates(page, 'nav-popup-out');
    expect(exits).toHaveLength(1);
    expect(exits[0]).toMatchObject({ endingStyle: true, sizes: { popupWidth: '400px', popupHeight: '200px', positionerWidth: '400px', positionerHeight: '200px' } });
  });

  test('updates popup sizing when the window is resized while the popup is open', async ({ page, mode }) => {
    await gotoMenu(page, mode, { fluid: true, keepMountedContent: true });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    const positioner = page.getByTestId('positioner');
    const viewport = page.viewportSize()!;
    await expect.poll(() => pixels(positioner, '--positioner-width')).toBeCloseTo(viewport.width * 0.4, 0);
    await positioner.evaluate((element) => {
      const record = { instant: false };
      (window as unknown as { instantSeen: typeof record }).instantSeen = record;
      new MutationObserver(() => {
        record.instant ||= element.hasAttribute('data-instant');
      }).observe(element, { attributes: true, attributeFilter: ['data-instant'] });
    });

    await page.setViewportSize({ width: 1000, height: viewport.height });

    await expect.poll(() => page.evaluate(() => (window as unknown as { instantSeen: { instant: boolean } }).instantSeen.instant)).toBe(true);
    await expect(positioner).not.toHaveAttribute('data-instant');
    await expect.poll(() => pixels(positioner, '--positioner-width')).toBeCloseTo(400, 0);
  });
});

// The attach order (the lesson of CI run 40461905812): the Trigger's four handles, the button, composite, interactions and
// navigation-menu ones, attach in the same render; the navigation-menu module's late load leaves the click role working,
// and its handle joins the Root's menu once it arrives.
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

  test('port: with js/navigation-menu.js stalled the trigger opens on click, and the size pass runs once the module arrives', async ({ page, mode }) => {
    const stalled = await stall(page, 'navigation-menu');
    await page.goto(`${fixtureUrl(mode, FIXTURE)}?sized=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await page.locator(Fixture.interactive(FIXTURE)).first().waitFor({ state: 'attached' });
    await expect.poll(stalled.hits).toBeGreaterThan(0);

    await trigger(page, 1).click();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(content(page, 1)).toBeVisible();

    await stalled.release();
    await expect(page.locator(Fixture.container(FIXTURE))).toHaveAttribute('data-attached', 'true');
    await fireClick(trigger(page, 2));
    await expect(content(page, 2)).toBeVisible();
    const positioner = page.getByTestId('positioner');
    await expect.poll(() => positioner.evaluate((element) => parseFloat(element.style.getPropertyValue('--positioner-height')))).toBeCloseTo(150, 0);
  });
});

