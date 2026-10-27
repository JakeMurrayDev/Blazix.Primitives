import { type Locator, type Page } from '@playwright/test';
import { expect, Fixture, fixtureUrl, test } from './helpers';
import { gotoPopupFixture, openChangeCompletes, settle } from './suites/popupFixture';

// The `portal` module's [L4] titles on the test-only FixturePortal (FLT-API-13 and FLT-DOM-11, whose jsdom titles are at
// L3 in portal.test.ts; settled by the orchestrator 2026-10-27), issue 8's verification item ADR-04/3 with the wrapper
// shape of ADR 04 Amendments 2026-10-27, and design point 3's order of two handles: a portaled popup's transitions handle
// attaches only after the portal module moved its node. Every test runs on both render modes.

/** Collects the page's uncaught errors and console errors, where a failed Blazor DOM edit surfaces on either mode. */
function watchErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') {
      errors.push(message.text());
    }
  });
  return errors;
}

/** The children of the portal's logical parent, by what they are: a sibling's text or `wrapper`. */
function logicalOrder(fixture: Locator): Promise<string[]> {
  return fixture.locator('[data-logical-parent]').evaluate((parent) =>
    Array.from(parent.children).map((child) => (child.hasAttribute('data-sibling') ? (child.textContent ?? '') : 'wrapper')),
  );
}

/** What the portal node's parent is: `body`, a target's attribute, `wrapper` or `none`. */
function nodeParent(page: Page, testId: string): Promise<string> {
  return page.evaluate((id) => {
    const node = document.querySelector(`[data-testid="${id}"]`);
    const parent = node?.parentElement;
    if (!node || !parent) {
      return 'none';
    }

    if (parent === document.body) {
      return 'body';
    }

    if (parent.id === 'portal-target') {
      return 'selector';
    }

    if (parent.hasAttribute('data-ref-target')) {
      return 'ref';
    }

    return parent.style.display === 'contents' ? 'wrapper' : parent.getAttribute('data-testid') ?? parent.tagName.toLowerCase();
  }, testId);
}

test.describe('FixturePortal', () => {
  test('port: the portal node moves to the end of the body with the consumer attributes, and its wrapper stays in place', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'portal-basic');
    const portal = page.locator('[data-testid="portal"]');

    await expect(portal).toHaveAttribute('data-base-ui-portal', '');
    await expect(portal).toHaveClass('portal-node');
    expect(await portal.getAttribute('id')).toMatch(/^base-ui-/);
    await expect.poll(() => nodeParent(page, 'portal')).toBe('body');
    expect(await page.evaluate(() => document.body.lastElementChild?.getAttribute('data-testid'))).toBe('portal');
    expect(await logicalOrder(fixture)).toEqual(['wrapper']);
    expect(await fixture.locator('[data-logical-parent] > div').evaluate((wrapper) => wrapper.children.length)).toBe(0);
    await expect(page.locator('[data-testid="portal-content"]')).toBeVisible();
  });

  test('port: a pending container renders no node until a container is supplied, then the node moves into it', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'portal-basic', { container: 'pending' });
    await settle(fixture);
    await expect(page.locator('[data-testid="portal"], [data-testid="portal-content"]')).toHaveCount(0);

    await fixture.locator('[data-set-container="selector"]').click();

    await expect.poll(() => nodeParent(page, 'portal')).toBe('selector');
    await expect(page.locator('[data-testid="portal-content"]')).toBeVisible();
  });

  test('port: a change of container moves the node into the new container: an element reference, a selector match, the body', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'portal-basic', { container: 'selector' });
    await expect.poll(() => nodeParent(page, 'portal')).toBe('selector');

    await fixture.locator('[data-set-container="ref"]').click();
    await expect.poll(() => nodeParent(page, 'portal')).toBe('ref');
    expect(await page.locator('#portal-target').evaluate((target) => target.children.length)).toBe(0);

    await fixture.locator('[data-set-container="body"]').click();
    await expect.poll(() => nodeParent(page, 'portal')).toBe('body');
    await expect(page.locator('[data-testid="portal-content"]')).toBeVisible();
  });

  test("port: a nested portal moves into the enclosing portal's node", async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'portal-basic', { nested: true });

    await expect.poll(() => nodeParent(page, 'portal')).toBe('body');
    await expect.poll(() => nodeParent(page, 'inner-portal')).toBe('portal');
    await expect(page.locator('[data-testid="inner-content"]')).toBeVisible();
  });

  test('@verifies ADR-04/3 Moving a rendered node and restoring it before disposal never produces a Blazor insertBefore/removeChild on a foreign parent when the portal node has siblings in its logical parent', async ({ page, mode }) => {
    const errors = watchErrors(page);
    const fixture = await gotoPopupFixture(page, mode, 'portal-basic');
    await expect.poll(() => nodeParent(page, 'portal')).toBe('body');

    // Blazor inserts siblings before and after the portal in its logical parent while the node is moved.
    await fixture.locator('[data-add-before]').click();
    await fixture.locator('[data-add-before]').click();
    await fixture.locator('[data-add-after]').click();
    await fixture.locator('[data-add-after]').click();
    await settle(fixture);
    expect(await logicalOrder(fixture)).toEqual(['Before 0', 'Before 1', 'wrapper', 'After 0', 'After 1']);

    // Blazor edits the node's children wherever the node is.
    for (let index = 0; index < 3; index += 1) {
      await fixture.locator('[data-add-child]').click();
    }
    await fixture.locator('[data-remove-child]').click();
    await settle(fixture);
    await expect(page.locator('[data-testid="portal"] > [data-child]')).toHaveCount(2);
    expect(await nodeParent(page, 'portal')).toBe('body');

    // Removing the portal takes its node with it; rendering it again moves a new node out.
    await fixture.locator('[data-toggle-portal]').click();
    await settle(fixture);
    await expect(page.locator('[data-base-ui-portal]')).toHaveCount(0);
    expect(await logicalOrder(fixture)).toEqual(['Before 0', 'Before 1', 'After 0', 'After 1']);

    await fixture.locator('[data-toggle-portal]').click();
    await expect.poll(() => nodeParent(page, 'portal')).toBe('body');
    await fixture.locator('[data-add-before]').click();
    await settle(fixture);
    expect(await logicalOrder(fixture)).toEqual(['Before 0', 'Before 1', 'Before 2', 'wrapper', 'After 0', 'After 1']);

    await fixture.locator('[data-remove-siblings]').click();
    await settle(fixture);
    expect(await logicalOrder(fixture)).toEqual(['wrapper']);
    await expect(page.locator('[data-base-ui-portal]')).toHaveCount(1);
    expect(errors).toEqual([]);
  });
});

test.describe('the portal and the portaled popup', () => {
  test("port: a portaled popup's transitions handle attaches only after the portal module moved its node (portal.js stalled)", async ({ page, mode }) => {
    let release!: () => void;
    const stalled = new Promise<void>((resolve) => {
      release = resolve;
    });
    // The module's URL carries the static-asset fingerprint (`js/portal.<hash>.js`).
    await page.route(/\/js\/portal(\.[0-9a-z]+)?\.js(\?|$)/, async (route) => {
      await stalled;
      await route.continue();
    });

    await page.goto(`${fixtureUrl(mode, 'popup-conformance')}?open=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await page.locator(Fixture.interactive('popup-conformance')).first().waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.container('popup-conformance'));
    await settle(fixture);

    // While the portal module is held, the node stays hidden in its wrapper and the popup's handle waits.
    expect(await page.locator('[data-base-ui-portal]').evaluate((node) => (node.parentElement as HTMLElement).style.display)).toBe('contents');
    await expect(page.locator('[data-base-ui-portal]')).toHaveCSS('display', 'none');
    await expect(fixture).toHaveAttribute('data-attached', 'false');
    expect(await openChangeCompletes(fixture)).toEqual([]);

    release();

    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await expect.poll(() => page.evaluate(() => document.querySelector('[data-base-ui-portal]')?.parentElement === document.body)).toBe(true);
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
  });
});
