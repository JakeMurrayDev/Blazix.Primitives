import { type Locator, type Route } from '@playwright/test';
import { expect, Fixture, fixtureUrl, test, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture, openChangeCompletes, settle, type PopupFixtureOptions } from './suites/popupFixture';

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
    const box = (await trigger(page, 1).boundingBox())!;

    await page.mouse.move(box.x + 4, box.y + 4);
    await page.mouse.move(box.x + 10, box.y + 8);
    await page.clock.runFor(60);

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

  test('reports right activation direction when switching in either direction', async ({ page, mode }) => {
    await gotoMenu(page, mode);
    await trigger(page, 1).click();
    await expect(content(page, 1)).toBeVisible();

    await fireClick(trigger(page, 2));
    await expect(content(page, 2)).toHaveAttribute('data-activation-direction', 'right');

    await fireClick(trigger(page, 1));
    await expect(content(page, 1)).toHaveAttribute('data-activation-direction', 'left');
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

