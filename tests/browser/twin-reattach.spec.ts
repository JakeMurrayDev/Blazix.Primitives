import { test, expect, gotoFixture } from './helpers';

// Issue 132 (design point 10-132a (b), chosen by the user 2026-11-28; standing rule "the attach order of two handles on one
// element is pinned by a test"): a part that re-attaches its element while the first attach is in flight gets the handle
// only once that attach completed, so whatever it signals afterwards follows the module. The case is TLB-STA-04's
// (`toolbar.spec.ts` "port: a disabled toggle substituted into the first toolbar button keeps the initial tab stop when
// the button handles attach last"): the toolbar's first publication waits for its substituted buttons' handles, which clear
// the DOM `disabled` property the substituted toggle renders. Here a render lands while the `button` module is held, so
// every toolbar button attaches a second time during its first attach; before the rule that second attach returned at
// once, the button signalled "attached", the toolbar published, and the `composite` module moved the tab stop off the
// still-disabled first item. The route matches the fingerprinted module URL WebAssembly loads.
test.describe('twin re-attach in flight', () => {
  test('port: a render while the button handles attach does not release the toolbar before they attach, so a substituted disabled toggle keeps the initial tab stop', async ({ page, mode }) => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    let requested!: () => void;
    const requestedOnce = new Promise<void>((resolve) => (requested = resolve));
    await page.route(/\/_content\/Iodine\.Headless\/js\/button(\.[0-9a-z]+)?\.js$/, async (route) => {
      requested();
      await held;
      await route.continue();
    });

    const fixture = await gotoFixture(page, mode, 'toolbar-toggle');
    await requestedOnce;

    // A barrier click: the fixture renders again, so every toolbar button runs its after-render attach a second time. The
    // barriers after it are round trips on Server (ADR-04/1: one circuit's events in order), so a toolbar released early
    // has imported and attached its `composite` handle before the `button` module is let through.
    for (let round = 0; round < 4; round += 1) {
      const before = Number(await fixture.getAttribute('data-sync'));
      await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
      await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
    }
    release();
    await expect(fixture).toHaveAttribute('data-attached', 'true');

    const container = fixture.locator('[data-case="first-disabled-substitute"]');
    const first = container.locator('[data-item="first"]');
    await expect(first).not.toHaveAttribute('disabled');
    await expect(first).toHaveAttribute('tabindex', '0');
    await container.locator('[data-before]').evaluate((node) => (node as HTMLElement).focus());
    await page.keyboard.press('Tab');
    await expect(first).toBeFocused();
  });
});
