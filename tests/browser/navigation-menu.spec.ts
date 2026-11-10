import { type Locator, type Route } from '@playwright/test';
import { delayCircuit, expect, Fixture, fixtureUrl, test, type Page, type RenderMode } from './helpers';
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

/** An element's inline `pointer-events`, the lock's write (NAV-PTR-10). */
function inlinePointerEvents(locator: Locator): Promise<string> {
  return locator.evaluate((element: HTMLElement) => element.style.pointerEvents);
}

/** An element's computed `pointer-events`: a locked list's other children inherit `none`. */
function computedPointerEvents(locator: Locator): Promise<string> {
  return locator.evaluate((element) => getComputedStyle(element).pointerEvents);
}

/**
 * Base UI's `fireEvent` for a step a real pointer cannot take: a press with no release (a real one always clicks), or an
 * enter on a trigger the lock has given `pointer-events: none`. `mouseenter` and `mouseleave` come with React's
 * `mouseover` and `mouseout`, as Testing Library's do.
 */
async function fire(locator: Locator, type: 'mouseenter' | 'mouseleave' | 'mousemove' | 'pointerdown', point?: { x: number; y: number }): Promise<void> {
  await locator.evaluate((element, [eventType, at]) => {
    const init = { bubbles: true, cancelable: true, composed: true, view: window, clientX: at?.x ?? 0, clientY: at?.y ?? 0 };
    if (eventType === 'pointerdown') {
      element.dispatchEvent(new PointerEvent(eventType, { ...init, pointerType: 'mouse', isPrimary: true, button: 0, buttons: 1 }));
    } else if (eventType === 'mousemove') {
      element.dispatchEvent(new MouseEvent(eventType, init));
    } else {
      element.dispatchEvent(new MouseEvent(eventType, { ...init, bubbles: false, cancelable: false }));
      element.dispatchEvent(new MouseEvent(eventType === 'mouseenter' ? 'mouseover' : 'mouseout', init));
    }
  }, [type, point] as const);
}

/**
 * Waits until a nested Content sits inside its inline Viewport. On Server its portal handle moves it there a round trip
 * or two after the open's render, where Base UI's portal places it at the commit; until then it is not between the
 * Viewport's guards.
 */
async function nestedPanelMoved(page: Page, testId: string): Promise<void> {
  await expect(page.getByTestId('nested-viewport').getByTestId(testId)).toHaveCount(1);
}

/** Base UI's `fireEvent.mouseMove(document, { clientX, clientY })`: a move the safe polygon reads, at a point. */
async function fireDocumentMove(page: Page, point: { x: number; y: number }): Promise<void> {
  await page.evaluate((at) => {
    document.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true, composed: true, view: window, clientX: at.x, clientY: at.y }));
  }, point);
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

test.describe('<NavigationMenu.Link /> pointerdown', () => {
  test('closes after pointerdown on a link in a hover-open popup when pointer leaves', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();

    const link = page.getByTestId('link-1-a');
    await hoverElement(page, link);
    await fire(link, 'pointerdown');
    await leaveMenu(page);
    await page.clock.runFor(CLOSE_DELAY + 10);

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
  });
});

test.describe('<NavigationMenu.Root /> pointer-events lock', () => {
  const list = (page: Page) => page.getByTestId('list');
  const body = (page: Page) => page.locator('body');

  async function hoverOpen(page: Page, n: number): Promise<void> {
    await hoverTrigger(page, n);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, n)).toBeVisible();
  }

  test('blocks pointer events on the list while traversing from a top-level trigger to the popup', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await hoverOpen(page, 1);

    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');
    expect(await inlinePointerEvents(body(page))).toBe('');
    expect(await computedPointerEvents(trigger(page, 2))).toBe('none');

    await hoverElement(page, page.getByTestId('positioner'));

    await expect.poll(() => inlinePointerEvents(list(page))).toBe('');
  });

  test('reapplies top-level safePolygon pointer events after returning from the popup and switching triggers', async ({ page, mode }) => {
    // Gap G1 of the review of PR 168 (PR 184): on Server the hover switch's commit cleared the new trigger's lock.
    await gotoMenu(page, mode, { topLink: true });
    const topLink = page.getByTestId('top-link');
    const positioner = page.getByTestId('positioner');
    await hoverOpen(page, 1);

    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');
    expect(await inlinePointerEvents(body(page))).toBe('');
    expect(await computedPointerEvents(topLink)).toBe('none');

    await hoverElement(page, positioner);
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('');

    await hoverTrigger(page, 1);
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');
    expect(await inlinePointerEvents(body(page))).toBe('');
    expect(await computedPointerEvents(topLink)).toBe('none');

    await hoverElement(page, positioner);
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('');

    await hoverTrigger(page, 2);
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(content(page, 2)).toBeVisible();
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');
    expect(await inlinePointerEvents(body(page))).toBe('');
    expect(await computedPointerEvents(topLink)).toBe('none');
  });

  test('keeps top-level safePolygon pointer events active when switching directly to a different trigger', async ({ page, mode }) => {
    // Gap G1 of the review of PR 168 (PR 184): on Server the hover switch's commit cleared the new trigger's lock.
    await gotoMenu(page, mode, { topLink: true });
    await fire(trigger(page, 1), 'mouseenter');
    await fire(trigger(page, 1), 'mousemove');
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();

    // The lock gives trigger 2 `pointer-events: none`, so a real pointer cannot enter it from trigger 1.
    await fire(trigger(page, 2), 'mouseenter');
    await fire(trigger(page, 2), 'mousemove');

    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(content(page, 2)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');
    expect(await inlinePointerEvents(body(page))).toBe('');
    expect(await computedPointerEvents(trigger(page, 1))).toBe('none');
    expect(await computedPointerEvents(page.getByTestId('top-link'))).toBe('none');
  });

  test('clears top-level safePolygon pointer events on trigger pointerdown', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await hoverOpen(page, 1);
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');

    await fire(trigger(page, 1), 'pointerdown');

    await expect.poll(() => inlinePointerEvents(list(page))).toBe('');
  });

  test('scopes safePolygon pointer events to the document for a custom list element', async ({ page, mode }) => {
    // Gap G1 of the review of PR 168 (PR 184): on Server the hover switch's commit cleared the new trigger's lock.
    await gotoMenu(page, mode, { customList: true });
    await hoverOpen(page, 1);

    await expect.poll(() => inlinePointerEvents(body(page))).toBe('none');
    expect(await inlinePointerEvents(page.getByTestId('custom-list'))).toBe('');

    await fire(trigger(page, 1), 'pointerdown');
    await expect.poll(() => inlinePointerEvents(body(page))).toBe('');

    await hoverTrigger(page, 2);
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect.poll(() => inlinePointerEvents(body(page))).toBe('none');

    await fire(trigger(page, 2), 'pointerdown');
    await expect.poll(() => inlinePointerEvents(body(page))).toBe('');
  });

  test('blocks pointer events on sibling top-level triggers when opened through real hover', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await hoverOpen(page, 1);

    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');
    expect(await computedPointerEvents(trigger(page, 2))).toBe('none');
  });

  test('releases the pointer-events lock on the list when the open trigger unmounts', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { value: 'none' });
    await hoverOpen(page, 1);
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('none');

    // One update removes trigger 1 and closes the menu while the pointer rests on the trigger, so none of the trigger's
    // own release paths runs before it unmounts.
    await pressControl(fixture.locator('[data-drop-first]'));

    await expect(trigger(page, 1)).toHaveCount(0);
    await expect.poll(() => inlinePointerEvents(list(page))).toBe('');
  });

  test('releases the pointer-events lock when the pointer sweeps across a trigger without the menu opening', async ({ page, mode }) => {
    await gotoMenu(page, mode, { keepMounted: true });
    await freezeClock(page);

    // The kept Portal's floating element exists while closed, so the enter locks the list at once; the leave comes
    // before the rest delay.
    await hoverTrigger(page, 1);
    await leaveMenu(page);

    await expect.poll(() => inlinePointerEvents(list(page))).toBe('');
    await expect(page.getByTestId('link-1-a')).toHaveCount(0);
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

  touchTest('restores hover open after touching a nested submenu trigger and closing outside', async ({ touchPage: page, mode }) => {
    await gotoMenu(page, mode, { nested: 'inline', nestedDefault: 'nested-1' });
    await trigger(page, 1).tap();
    await expect(content(page, 1)).toBeVisible();

    await page.getByTestId('nested-trigger-2').tap();
    await expect(page.getByTestId('nested-content-2')).toBeVisible();

    await page.getByTestId('after').click();
    await expect(page.getByTestId('popup-root')).toHaveCount(0);

    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();
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

  test('keeps the opt-out when focus leaves the trigger during a manual exit', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { defaultValue: 'item-1', retain: true });
    await trigger(page, 1).focus();

    await pressControl(fixture.locator('[data-close]'));
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('popup-root')).toHaveCount(1);

    // The blur to outside asks a redundant focus-out close, which must not discard the recorded opt-out.
    await page.getByTestId('after').click();
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

    // A real pointer crosses the nested panel first, which releases the inline lock on the nested List (NAV-PTR-13).
    await hoverElement(page, nestedContent(page, 2));
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

  // An inline nested Root (no Positioner) is a node of the floating tree without a context in Base UI (only `useFloating`
  // sets one, `useFloating.ts:115-117`), so it is never an open child and the parent's safe polygon closes it.
  test('closes inline nested viewport when parent menu closes', async ({ page, mode }) => {
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

  test('propagates a nested close with a falsy open value to the parent root', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'falsy' });
    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await page.getByTestId('nested-link-1').click();

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
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
    await expect(content(page, 1)).toBeVisible();
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

  test('treats a falsy value (false) as open for inline nested trigger interactions', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { nested: 'falsy' });
    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 1)).toBeVisible();

    await fireClick(nestedTrigger(page, 1));
    await settle(fixture);

    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 1)).toBeVisible();
  });

  test('port: Escape closes the nested portaled menu first', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'portal' });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await nestedTrigger(page, 1).click();
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(page.getByTestId('nested-popup-root')).toBeVisible();

    await page.keyboard.press('Escape');

    await expect(page.getByTestId('nested-popup-root')).toHaveCount(0);
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

    await page.keyboard.press('Escape');

    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
  });
});

// The inline nested lock (NAV-PTR-13): Base UI places the nested Viewport by mocked rects; the fixture's `nestedSide` lays
// it out 20 px from nested trigger 1, and a real pointer leaves through that edge. The clock is frozen, so a close delay
// fires only when a title runs it.
test.describe('<NavigationMenu.Root /> inline nested lock', () => {
  const nestedTrigger = (page: Page, n: number) => page.getByTestId(`nested-trigger-${n}`);
  const nestedContent = (page: Page, n: number) => page.getByTestId(`nested-content-${n}`);
  const nestedList = (page: Page) => page.getByTestId('nested-list');
  const nestedViewport = (page: Page) => page.getByTestId('nested-viewport');

  async function hoverOpen(page: Page, locator: Locator): Promise<void> {
    await hoverElement(page, locator);
    await page.clock.runFor(OPEN_DELAY + 10);
  }

  async function openNested(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<void> {
    await gotoMenu(page, mode, { nested: 'inline', ...options });
    await freezeClock(page);
    await hoverOpen(page, trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    // Issue 191: the Positioner stays transparent until its first pass (FLT-STA-09, Base UI's `opacity: 0` while not
    // positioned), and toBeVisible ignores opacity, so a title read the nested trigger's box before the pass moved the
    // popup and moved the pointer to a stale point. The titles read no geometry before the pass. This is a readiness check
    // on a stable end state, the pass having landed (the opacity stays once set), not a poll over a transient style, so it
    // stays within ADR 06 §2. The clock steps, since the pass may wait for a frame and the clock is frozen.
    await expect.poll(async () => {
      await page.clock.runFor(16);
      return page.getByTestId('positioner').evaluate((element) => (element as HTMLElement).style.opacity);
    }).not.toBe('0');
  }

  type Side = 'right' | 'left' | 'bottom' | 'top';

  /**
   * Leaves nested trigger 1 through the edge that faces `side`: Base UI's leave point is on the edge, and a real pointer's
   * leave lands 1 px past it, the safe polygon's apex; the move after it is 10 px past the edge, into the gap.
   */
  async function leaveToward(page: Page, side: Side): Promise<void> {
    const box = (await nestedTrigger(page, 1).boundingBox())!;
    const x = box.x + box.width / 2;
    const y = box.y + box.height / 2;
    const points: Record<Side, [number, number][]> = {
      right: [[box.x + box.width - 1, y], [box.x + box.width + 1, y], [box.x + box.width + 10, y]],
      left: [[box.x + 1, y], [box.x - 1, y], [box.x - 10, y]],
      bottom: [[x, box.y + box.height - 1], [x, box.y + box.height + 1], [x, box.y + box.height + 10]],
      top: [[x, box.y + 1], [x, box.y - 1], [x, box.y - 10]],
    };
    for (const [pointX, pointY] of points[side]) {
      await page.mouse.move(pointX, pointY);
    }
  }

  test('scopes inline safePolygon pointer events to the submenu list while traversing to the viewport', async ({ page, mode }) => {
    await openNested(page, mode, { nestedDefault: 'nested-1', nestedSide: 'right' });

    await hoverElement(page, nestedTrigger(page, 1));
    await expect.poll(() => inlinePointerEvents(nestedList(page))).toBe('none');

    await leaveToward(page, 'right');

    expect(await inlinePointerEvents(nestedList(page))).toBe('none');
    expect(await inlinePointerEvents(page.locator('body'))).toBe('');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 1)).toBeVisible();

    await hoverElement(page, nestedViewport(page));

    await expect.poll(() => inlinePointerEvents(nestedList(page))).toBe('');
  });

  for (const side of ['right', 'left', 'bottom', 'top'] as const) {
    test(`keeps an inline submenu open while traversing toward a viewport on the ${side}`, async ({ page, mode }) => {
      await openNested(page, mode, { nestedSide: side });

      await hoverOpen(page, nestedTrigger(page, 1));
      await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
      await expect.poll(() => inlinePointerEvents(nestedList(page))).toBe('none');

      await leaveToward(page, side);
      expect(await inlinePointerEvents(nestedList(page))).toBe('none');
      await page.clock.runFor(CLOSE_DELAY + 10);

      await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
      await expect(nestedContent(page, 1)).toBeVisible();
    });
  }

  // Gap G3 of the review of PR 168 (PR 184): the inline nested Trigger's leave built no safe polygon.
  test('clears inline safePolygon pointer events when the pointer leaves the traversal path', async ({ page, mode }) => {
    await openNested(page, mode, { nestedDefault: 'nested-1', nestedSide: 'right' });

    await hoverElement(page, nestedTrigger(page, 1));
    await expect.poll(() => inlinePointerEvents(nestedList(page))).toBe('none');
    const box = (await nestedTrigger(page, 1).boundingBox())!;
    await page.mouse.move(box.x + box.width - 1, box.y + box.height / 2);
    await page.mouse.move(box.x + box.width + 1, box.y + box.height / 2);
    expect(await inlinePointerEvents(nestedList(page))).toBe('none');

    // Away from the Viewport on the right, and still inside content 1: its first Link.
    const away = (await page.getByTestId('link-1-a').boundingBox())!;
    await page.mouse.move(away.x + 4, away.y + 4);

    await expect.poll(() => inlinePointerEvents(nestedList(page))).toBe('');
    await expect(nestedTrigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedContent(page, 1)).toBeVisible();
  });

  // Base UI's sequence event for event (NavigationMenuRoot.test.tsx:3059-3104), dispatched as its fireEvent calls are, with
  // the points taken from the fixture's layout where Base UI mocks the rects: its leave is 2 px inside nested trigger 1's
  // edge that faces the Viewport, and its move is in the gap toward the Viewport. A real pointer would add the leaves of
  // the Viewport and of trigger 2 that Base UI's title never sends, and their close timers.
  test('keeps inline safePolygon pointer events when returning to the original trigger before traversing again', async ({ page, mode }) => {
    await openNested(page, mode, { nestedDefault: 'nested-1', nestedSide: 'right' });
    const first = nestedTrigger(page, 1);
    const box = (await first.boundingBox())!;
    const viewportBox = (await nestedViewport(page).boundingBox())!;
    const leave = { x: box.x + box.width - 2, y: box.y + box.height / 2 };
    const gap = { x: (box.x + box.width + viewportBox.x) / 2, y: box.y + box.height / 2 };

    await fire(first, 'mouseenter');
    await fire(first, 'mouseleave', leave);
    await fireDocumentMove(page, gap);
    await fire(nestedViewport(page), 'mouseenter');

    await fire(nestedTrigger(page, 2), 'mouseenter');
    await fire(nestedTrigger(page, 2), 'mousemove');
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'true');

    await fire(first, 'mouseenter');
    await fire(first, 'mousemove');
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(first).toHaveAttribute('aria-expanded', 'true');
    await expect.poll(() => inlinePointerEvents(nestedList(page))).toBe('none');

    await fire(first, 'mouseleave', leave);
    await fireDocumentMove(page, gap);

    expect(await inlinePointerEvents(nestedList(page))).toBe('none');
    await expect(first).toHaveAttribute('aria-expanded', 'true');
    await expect(nestedTrigger(page, 2)).toHaveAttribute('aria-expanded', 'false');
    await expect(nestedContent(page, 1)).toBeVisible();
    await expect(nestedContent(page, 2)).toHaveCount(0);
  });
});

// A Dialog or a Popover inside a Content is the menu's child through the floating-node cascade (NAV-DIS-07, NAV-FOC-12,
// NAV-PTR-16; ADR 04 Amendments 2026-11-03 (5)).
test.describe('<NavigationMenu.Content /> nested popups', () => {
  test('keeps a hover-open menu open when pointerdown happens on a nested dialog trigger', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nestedPopup: 'dialog' });
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();
    const dialogTrigger = page.getByTestId('dialog-trigger');

    await fire(dialogTrigger, 'pointerdown');
    await fire(content(page, 1), 'mouseleave');
    await dialogTrigger.click();

    await expect(page.getByTestId('dialog-popup')).toBeVisible();
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });

  for (const [kind, name] of [['dialog', 'dialog'], ['popover', 'popover']] as const) {
    test(`keeps the menu open when interacting with a nested ${name}`, async ({ page, mode }) => {
      await gotoMenu(page, mode, { nestedPopup: kind });
      await trigger(page, 1).click();
      await expect(content(page, 1)).toBeVisible();
      await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');

      await page.getByTestId(`${kind}-trigger`).click();
      await expect(page.getByTestId(`${kind}-popup`)).toBeVisible();

      await page.getByTestId(`${kind}-button`).click();

      await expect(content(page, 1)).toBeVisible();
      await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    });
  }
});

// NAV-API-12's server-markup titles (Base UI's `renderToString` ones) on the prerendered `navigation-menu-prerender`: the
// static markup is read with its scripts removed, then the interactive page.
test.describe('<NavigationMenu.Content /> prerendered', () => {
  async function staticMarkup(page: Page, mode: RenderMode, query: string): Promise<void> {
    const response = await page.request.get(`${fixtureUrl(mode, 'navigation-menu-prerender')}${query}`);
    await page.setContent((await response.text()).replace(/<script[\s\S]*?<\/script>/g, ''));
  }

  test('keeps the content mounted (hidden) in the DOM when keepMounted is true', async ({ page, mode }) => {
    await staticMarkup(page, mode, '?keepMountedContent=true');

    await expect(page.locator('[data-testid="content-1"]')).toHaveCount(1);
    await expect(page.locator('[data-testid="content-1"]')).toBeHidden();
  });

  test('does not keep the content mounted in the DOM when keepMounted is false', async ({ page, mode }) => {
    await staticMarkup(page, mode, '');

    await expect(page.locator('[data-testid="trigger-1"]')).toHaveCount(1);
    await expect(page.locator('[data-testid="content-1"]')).toHaveCount(0);
  });

  test('does not keep the content mounted post-hydration when keepMounted is false', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'navigation-menu-prerender');

    await expect(page.getByTestId('content-1')).toHaveCount(0);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
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

  test('moves focus through the menu correctly', async ({ page, mode }) => {
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

  test('closes the menu when tabbing forward out', async ({ page, mode }) => {
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

  test('does not restore focus to the trigger when focus moves outside', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).focus();

    await trigger(page, 1).click();
    // Base UI's click commits at once; here the open render, which brings the guards after the trigger, is waited for.
    await expect(content(page, 1)).toBeVisible();
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

  test('closes after tabbing out of arbitrary tabbable content', async ({ page, mode }) => {
    await gotoMenu(page, mode, { actionContent: true });
    await trigger(page, 2).focus();
    await fireClick(trigger(page, 2));
    await expect(content(page, 2)).toBeVisible();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('action')).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('after')).toBeFocused();
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'false');
  });

  test('returns to the last submenu item when shift+tabbing after tabbing out of it', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).focus();
    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('link-1-a')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('link-1-b')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(trigger(page, 2)).toBeFocused();

    await page.keyboard.press('Shift+Tab');
    await expect(page.getByTestId('link-1-b')).toBeFocused();
  });

  test('tabs from the last link of the last nested panel to the next top-level trigger', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'tabBoundary' });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await nestedPanelMoved(page, 'nested-content-2');
    const lastLink = page.getByTestId('nested-last-link');
    await lastLink.focus();
    await expect(lastLink).toBeFocused();

    await page.keyboard.press('Tab');

    await expect(trigger(page, 2)).toBeFocused();
    await expect(page.getByTestId('nested-content-2')).toHaveCount(1);
    await expect(page.getByTestId('nested-trigger-2')).toHaveAttribute('aria-expanded', 'true');
  });

  // Gap G2 of the review of PR 168 (PR 184): a Shift+Tab out of an inline nested panel left its Links at tabindex -1.
  test('tabs between nested triggers and links without opening inactive panels', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'tabFlow' });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await nestedPanelMoved(page, 'nested-content-developers');
    const developers = page.getByTestId('nested-trigger-developers');
    await developers.focus();
    await expect(developers).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-link-get-started')).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(developers).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-link-get-started')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-link-composition')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-trigger-design-systems')).toBeFocused();
    await expect(page.getByTestId('nested-content-design-systems')).toHaveCount(0);
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-trigger-engineering-leads')).toBeFocused();
  });

  // Gap G2 of the review of PR 168 (PR 184): a Shift+Tab out of an inline nested panel left its Links at tabindex -1.
  test('returns to the last inline submenu item when shift+tabbing after leaving it', async ({ page, mode }) => {
    await gotoMenu(page, mode, { nested: 'tabFlow' });
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await nestedPanelMoved(page, 'nested-content-developers');

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-trigger-developers')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-link-get-started')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-link-composition')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nested-trigger-design-systems')).toBeFocused();

    await page.keyboard.press('Shift+Tab');
    await expect(page.getByTestId('nested-link-composition')).toBeFocused();
  });
});

test.describe('<NavigationMenu.List />', () => {
  test('navigates from the focused trigger after an earlier item is removed', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { topLink: true });
    await trigger(page, 1).focus();
    await page.keyboard.press('ArrowRight');
    await expect(trigger(page, 2)).toBeFocused();
    await page.keyboard.press('ArrowRight');
    const last = page.getByTestId('top-link');
    await expect(last).toBeFocused();

    await pressControl(fixture.locator('[data-remove-first]'));
    await expect(trigger(page, 1)).toHaveCount(0);
    await settle(fixture);
    await page.keyboard.press('ArrowLeft');

    await expect(trigger(page, 2)).toBeFocused();
  });

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

  test('handles positioner width correctly', async ({ page, mode }) => {
    await gotoMenu(page, mode, { emptyItem: true });

    // Base UI's pointer: onto the trigger with content, onto the trigger without, and back.
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await hoverElement(page, page.getByTestId('trigger-empty'));
    await page.clock.runFor(OPEN_DELAY + 10);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);

    await expect(page.getByTestId('link-1-a')).toBeVisible();
    const positioner = page.getByTestId('positioner');
    const contentWidth = await content(page, 1).evaluate((element) => element.getBoundingClientRect().width);
    await expect.poll(async () => Math.abs((await pixels(positioner, '--positioner-width')) - contentWidth)).toBeLessThanOrEqual(1);
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

  for (const keepMounted of [false, true]) {
    test(`preserves popup size when controlled value closes externally${keepMounted ? ' with keepMounted portal' : ''}`, async ({ page, mode }) => {
      const fixture = await gotoMenu(page, mode, { value: 'item-1', sized: true, fade: true, animations: 1, keepMounted });
      await expect(content(page, 1)).toBeVisible();
      await expect.poll(() => pixels(page.getByTestId('positioner'), '--positioner-width')).toBeCloseTo(400, 0);
      await recordAnimationStates(page);

      await pressControl(fixture.locator('[data-set-value="none"]'));

      // A kept Portal keeps the Popup, hidden, once its exit ends.
      await expect(page.getByTestId('popup-root')).toHaveCount(keepMounted ? 1 : 0);
      await expect(page.getByTestId('popup-root')).toBeHidden();
      const exits = await animationStates(page, 'nav-popup-out');
      expect(exits).toHaveLength(1);
      expect(exits[0]).toMatchObject({ endingStyle: true, sizes: { popupWidth: '400px', popupHeight: '200px', positionerWidth: '400px', positionerHeight: '200px' } });
    });
  }

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
// Port titles for rows Base UI tests nowhere (`Test: none`; the review of PR 168, fix 7), where the browser is the layer
// that can fail.
test.describe('port rows', () => {
  test('port: the Positioner has pointer-events none while the menu is not open, and none of its own while open', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode, { keepMounted: true });
    const positioner = page.getByTestId('positioner');
    expect(await inlinePointerEvents(positioner)).toBe('none');

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await expect.poll(() => inlinePointerEvents(positioner)).toBe('');

    await pressControl(fixture.locator('[data-close]'));
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await expect.poll(() => inlinePointerEvents(positioner)).toBe('none');
  });

  test('port: the Positioner carries the anchor and available sizes and the transform origin from the positioning pass', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    const positioner = page.getByTestId('positioner');

    for (const name of ['--anchor-width', '--anchor-height', '--available-width', '--available-height']) {
      await expect.poll(() => positioner.evaluate((element: HTMLElement, property) => element.style.getPropertyValue(property), name)).toMatch(/^\d+(\.\d+)?px$/);
    }
    expect(await positioner.evaluate((element: HTMLElement) => element.style.getPropertyValue('--transform-origin'))).not.toBe('');
  });

  test('port: in RTL the List mirrors its horizontal keys: ArrowLeft moves forward and ArrowRight back', async ({ page, mode }) => {
    await gotoMenu(page, mode, { rtl: true });
    await trigger(page, 1).focus();

    await page.keyboard.press('ArrowLeft');
    await expect(trigger(page, 2)).toBeFocused();

    await page.keyboard.press('ArrowRight');
    await expect(trigger(page, 1)).toBeFocused();
  });

  test('port: a scroll and a window blur dismiss nothing', async ({ page, mode }) => {
    const fixture = await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();

    await page.evaluate(() => {
      document.dispatchEvent(new Event('scroll'));
      window.dispatchEvent(new Event('scroll'));
      window.dispatchEvent(new FocusEvent('blur'));
    });
    await settle(fixture);

    await expect(content(page, 1)).toBeVisible();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
  });
});

// ADR 01's NavigationMenu rows, per mode (§9's Server relaxations): on Server with a one-way delay on the circuit, on
// WebAssembly as Base UI states the requirement.
test.describe('Server relaxations', () => {
  const DELAY = 150;

  // G4b (NAV-PTR-05's notes of 2026-11-05): Base UI gates the Trigger's click role on the Positioner's element
  // (NavigationMenuTrigger.tsx:128), which React sets in the commit that mounts it, before paint. Here the Positioner
  // registered in its after-render, after the open's render had taken the Trigger's click role away; on Server the role came
  // back a round trip later, so a click in between found no listener, the hover open's event was never upgraded, and the
  // leave closed the menu. On WebAssembly the Positioner registered inside the same synchronous dispatch, so the gap was
  // Server's only, and the title is red against the old order on Server alone.
  test('port: a click right after a hover open renders keeps the menu open when the pointer leaves', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'the round trip that left the click role away exists on Server only');
    // Twice the describe's delay, so the click lands inside the round trip that brought the click role back every time.
    await delayCircuit(page, 2 * DELAY);
    const fixture = await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    // The patient window runs on the page's clock: paused from the open's render, the click lands inside it however long a
    // loaded host takes.
    await freezeClock(page);

    await clickInPlace(page);
    await settle(fixture);
    await leaveMenu(page);
    await page.clock.runFor(CLOSE_DELAY + 10);
    await settle(fixture);

    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'true');
    expect(await valueChanges(fixture)).toEqual(['item-1:trigger-hover']);
  });

  test('port: a hover switch lands inside the hover on WebAssembly, and a round trip later on Server, with the size after the new Content', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, DELAY);
    }
    await gotoMenu(page, mode, { sized: true });
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();
    const positioner = page.getByTestId('positioner');
    await expect.poll(() => pixels(positioner, '--positioner-width')).toBeCloseTo(400, 0);

    // NAV-PTR-02, NAV-PTR-18: read at once after the hover; on Server the commit is still a round trip away. The pointer
    // reaches trigger 2 through the popup, which releases the lock on the List (NAV-PTR-11).
    await hoverElement(page, page.getByTestId('positioner'));
    await expect.poll(() => inlinePointerEvents(page.getByTestId('list'))).toBe('');
    await hoverTrigger(page, 2);
    expect(await trigger(page, 2).getAttribute('aria-expanded')).toBe(mode === 'wasm' ? 'true' : 'false');

    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
    await expect(content(page, 2)).toBeVisible();
    // NAV-ANI-04: the held size pass runs once the new Content is in the Viewport.
    await expect.poll(() => pixels(positioner, '--positioner-width')).toBeCloseTo(300, 0);
  });

  test('port: data-instant lands within the resize on WebAssembly, and a round trip after it on Server', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, DELAY);
    }
    const fixture = await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    // The Positioner's handle, which listens for the resize, attaches from the open's render: the module's size write and
    // a barrier after it mark it attached.
    await expect.poll(() => pixels(page.getByTestId('positioner'), '--positioner-width')).toBeGreaterThan(0);
    await settle(fixture);
    await page.getByTestId('positioner').evaluate((element) => {
      const record = window as unknown as { instantAfter?: number; resizedAt?: number };
      new MutationObserver(() => {
        if (element.hasAttribute('data-instant') && record.instantAfter === undefined && record.resizedAt !== undefined) {
          record.instantAfter = performance.now() - record.resizedAt;
        }
      }).observe(element, { attributes: true, attributeFilter: ['data-instant'] });
    });

    const size = page.viewportSize()!;
    await page.evaluate(() => {
      (window as unknown as { resizedAt?: number }).resizedAt = performance.now();
    });
    await page.setViewportSize({ width: 1000, height: size.height });

    await expect.poll(() => page.evaluate(() => (window as unknown as { instantAfter?: number }).instantAfter ?? null)).not.toBeNull();
    const elapsed = await page.evaluate(() => (window as unknown as { instantAfter: number }).instantAfter);
    // NAV-ANI-14, NAV-DOM-28: the report and C#'s render cross the circuit once each way on Server.
    if (mode === 'server') {
      expect(elapsed).toBeGreaterThanOrEqual(2 * DELAY);
    } else {
      expect(elapsed).toBeLessThan(2 * DELAY);
    }
    await expect(page.getByTestId('positioner')).not.toHaveAttribute('data-instant');
  });

  // "closes the menu when tabbing back out" failed on Server in CI run 41810354439 (issue-114 rule): the Trigger's focus-out
  // check waited for the Popup's and Positioner's handles, which attach a round trip or two after the open's render.
  // With the circuit delayed, a Shift+Tab right after the Content shows lands in that window every time.
  test('port: tabbing back out right after the open closes the menu, before the Popup\'s handle attaches', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, DELAY);
    }
    const fixture = await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();

    await page.keyboard.press('Shift+Tab');

    await expect(page.getByTestId('before')).toBeFocused();
    await expect(trigger(page, 1)).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect(await valueChanges(fixture)).toContain('null:focus-out');
  });

  // "closes the menu when tabbing forward out" failed on Server in CI run 41828715253 (issue-114 rule): the guards after
  // the active Trigger route into the Viewport through the Viewport's handle, which attaches a round trip or two after the
  // open's render. With the circuit delayed, the Tabs right after the Content shows land in that window every time.
  test('port: tabbing forward out right after the open closes the menu, before the Viewport\'s handle attaches', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, DELAY);
    }
    const fixture = await gotoMenu(page, mode);
    await trigger(page, 2).click();
    await expect(content(page, 2)).toBeVisible();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('link-2-a')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('link-2-b')).toBeFocused();
    await page.keyboard.press('Tab');

    await expect(page.getByTestId('after')).toBeFocused();
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect(await valueChanges(fixture)).toContain('null:focus-out');
  });

  test('port: a click on the trigger a hover switch just activated closes the menu on WebAssembly, and keeps it open within the round trip on Server', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, DELAY);
    }
    const fixture = await gotoMenu(page, mode);
    await hoverTrigger(page, 1);
    await page.clock.runFor(OPEN_DELAY + 10);
    await expect(content(page, 1)).toBeVisible();

    // NAV-PTR-05, NAV-PTR-07: the click comes at once after the hover that switches to trigger 2.
    await hoverTrigger(page, 2);
    await clickInPlace(page);
    await settle(fixture);

    if (mode === 'wasm') {
      await expect(page.getByTestId('popup-root')).toHaveCount(0);
      await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'false');
    } else {
      await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
      await expect(content(page, 2)).toBeVisible();
    }
  });
});

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

  /**
   * Pins the order of trigger 2's two click listeners with a stalled load, then waits for the page to attach. With
   * js/interactions.js stalled, an ArrowDown open on trigger 2 marks its navigation-menu handle attached first (the open
   * key is that handle's alone, NAV-KEY-01); with js/navigation-menu.js stalled, a click open on trigger 2 marks its
   * interactions handle attached first. Each marker is the clicked trigger's own handle, pressed once the fixture's
   * `data-handles` shows that handle attached and not the stalled one.
   */
  async function pinListenerOrder(page: Page, mode: RenderMode, first: 'navigation-menu' | 'interactions', controlled: boolean): Promise<void> {
    // On Server the circuit is delayed, so the handles attach round trips after the page is interactive every time, as on
    // the loaded runner of CI run 41815930860, where a marker press could land before the handle it marks.
    if (mode === 'server') {
      await delayCircuit(page, 150);
    }
    const stalledModule = first === 'navigation-menu' ? 'interactions' : 'navigation-menu';
    const stalled = await stall(page, stalledModule);
    await page.goto(`${fixtureUrl(mode, FIXTURE)}${controlled ? '?value=none' : ''}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await page.locator(Fixture.interactive(FIXTURE)).first().waitFor({ state: 'attached' });
    await expect.poll(stalled.hits).toBeGreaterThan(0);
    // On Server the handle attaches round trips after the page is interactive: the fixture renders it once C# has it.
    const container = page.locator(Fixture.container(FIXTURE));
    await expect(container).toHaveAttribute('data-handles', new RegExp(`(^| )trigger-2:${first}( |$)`));
    expect(await container.getAttribute('data-handles')).not.toContain(`trigger-2:${stalledModule}`);
    if (first === 'navigation-menu') {
      await trigger(page, 2).focus();
      await page.keyboard.press('ArrowDown');
    } else {
      await trigger(page, 2).click();
    }
    await expect(content(page, 2)).toBeVisible();
    await pressControl(page.locator(Fixture.container(FIXTURE)).locator('[data-close]'));
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await stalled.release();
    await expect(page.locator(Fixture.container(FIXTURE))).toHaveAttribute('data-attached', 'true');
  }

  // Base UI's click role reads the toggle of the render before the click whichever listener runs first (B1 of the review
  // of PR 168). A trusted click (Enter on the focused trigger, with no pointer moving, as Base UI's fireEvent.click) runs
  // a microtask checkpoint after each listener, which a dispatched click does not; a controlled Root re-renders the page
  // inside the dispatch, which sets the Trigger's parameters again.
  for (const controlled of [false, true]) {
    for (const first of ['navigation-menu', 'interactions'] as const) {
      for (const trusted of [false, true]) {
        const stalledModule = first === 'navigation-menu' ? 'interactions' : 'navigation-menu';
        const title = `port: ${controlled ? 'in a controlled menu, ' : ''}with js/${stalledModule}.js stalled the ${first} handle attaches first, and a ${trusted ? 'trusted ' : ''}click on another trigger of a click-opened menu switches to it`;
        test(title, async ({ page, mode }) => {
          await pinListenerOrder(page, mode, first, controlled);

          await trigger(page, 1).click();
          await expect(content(page, 1)).toBeVisible();
          if (trusted) {
            await trigger(page, 2).focus();
            await page.keyboard.press('Enter');
          } else {
            await fireClick(trigger(page, 2));
          }

          await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'true');
          await expect(content(page, 2)).toBeVisible();
        });
      }
    }
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

  // The Content's handles: its navigation-menu one attaches on mount from the loaded module, its transitions one waits
  // for js/transitions.js, which the first popup part to mount loads. The title above takes the other order.
  test('port: with js/transitions.js stalled the Content\'s navigation-menu handle attaches first, and the switch and the close complete once the module arrives', async ({ page, mode }) => {
    const stalled = await stall(page, 'transitions');
    await gotoMenu(page, mode);

    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();
    await expect.poll(stalled.hits).toBeGreaterThan(0);

    await stalled.release();
    await fireClick(trigger(page, 2));
    await expect(content(page, 1)).toHaveCount(0);
    await expect(content(page, 2)).toBeVisible();
    await expect(content(page, 2)).toHaveAttribute('data-activation-direction', 'right');

    await fireClick(trigger(page, 2));
    await expect(page.getByTestId('popup-root')).toHaveCount(0);
    await expect(trigger(page, 2)).toHaveAttribute('aria-expanded', 'false');
  });
});

