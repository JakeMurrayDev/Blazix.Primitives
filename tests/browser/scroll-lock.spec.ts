import { expect, gotoFixture, isScrollLocked, test, type Page } from './helpers';

// The `scroll-lock` module's port titles at L4 (UTL §4.8, every row `Test: none` upstream; UTL-ALG-01/02) on the test-only
// FixtureScrollLock, which composes PopupScrollLockBehavior as the popup families do. Every test runs on both render
// modes; on Server the lock applies a round trip after the commit (UTL-DOM-12's dated note).

function scrollY(page: Page): Promise<number> {
  return page.evaluate(() => window.scrollY);
}

/** Clicks a toggle through the DOM: a pointer click would first scroll the button into view. */
async function toggle(page: Page, lock: string): Promise<void> {
  await page.locator(`[data-toggle-lock="${lock}"]`).evaluate((element) => (element as HTMLElement).click());
}

/** Where the tall content sits in the viewport: what the user sees, whichever path the lock took. */
function contentTop(page: Page): Promise<number> {
  return page.evaluate(() => Math.round(document.querySelector('[data-tall]')!.getBoundingClientRect().top));
}

test.describe('scroll lock', () => {
  test('port: enabling the lock on a scrollable page locks it without moving the page or its content, and disabling restores both', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'scroll-lock-basic');
    await page.evaluate(() => window.scrollTo(0, 400));
    const width = await page.evaluate(() => document.documentElement.clientWidth);
    const top = await contentTop(page);
    const declarations = () => page.evaluate(() => [document.documentElement.style.cssText, document.body.style.cssText]);
    const before = await declarations();

    await toggle(page, '1');
    await expect.poll(() => isScrollLocked(page)).toBe(true);
    expect(await contentTop(page)).toBe(top);
    expect(await page.evaluate(() => document.documentElement.clientWidth)).toBe(width);

    // Wheel input does not scroll a locked page.
    await page.mouse.wheel(0, 600);
    await expect(fixture.locator('[data-lock="1"]')).toHaveAttribute('data-scroll-lock-enabled', 'true');
    expect(await contentTop(page)).toBe(top);

    await toggle(page, '1');
    await expect.poll(() => isScrollLocked(page)).toBe(false);
    expect(await scrollY(page)).toBe(400);
    expect(await contentTop(page)).toBe(top);
    expect(await declarations()).toEqual(before);
  });

  test('port: two locks on one page hold one page lock until the last releases', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'scroll-lock-basic');

    await toggle(page, '1');
    await toggle(page, '2');
    await expect.poll(() => isScrollLocked(page)).toBe(true);

    await toggle(page, '1');
    await expect(fixture.locator('[data-lock="1"]')).toHaveAttribute('data-scroll-lock-enabled', 'false');
    await fixture.locator('[data-sync-button]').click();
    await expect(fixture).toHaveAttribute('data-sync', '1');
    expect(await isScrollLocked(page)).toBe(true);

    await toggle(page, '2');
    await expect.poll(() => isScrollLocked(page)).toBe(false);
  });
});
