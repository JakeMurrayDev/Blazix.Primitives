import { expect, fixtureUrl, Fixture, type Page, type RenderMode } from './helpers';
import type { Locator } from '@playwright/test';

// One-family helpers of the Toast specs (issue 280, M6 track 9b), beside them as the brief asks. The `toast-basic` fixture
// is Base UI's `List` and `Button` (toast/utils/test-utils.tsx); its query switches are documented in
// tests/Blazix.Primitives.Fixtures.Client/Toast/ToastBasicFixture.razor. `animations=1` is the host's switch for Base UI's
// `BASE_UI_ANIMATIONS_DISABLED = false`.

export type Query = Record<string, string>;

/** Opens a Toast fixture with a query and waits for the viewport's handle to attach. */
export async function openToast(page: Page, mode: RenderMode, query: Query = {}, name = 'toast-basic'): Promise<Locator> {
  const search = new URLSearchParams(query).toString();
  await page.goto(`${fixtureUrl(mode, name)}${search ? `?${search}` : ''}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/**
 * Clicks the fixture's `add` button (Base UI's `Button`) and waits until the new toast is measured: the Root's `height`
 * report reached C#, so its handle has attached (the height is rendered as the root's `data-height`).
 */
export async function addToast(page: Page, count?: number): Promise<Locator> {
  const before = await page.locator('[data-toast-id]').count();
  await page.getByTestId('add').click();
  const expected = count ?? before + 1;
  await expect(page.locator('[data-toast-id]')).toHaveCount(expected);
  await expect(page.locator('[data-toast-id]:not([data-height])')).toHaveCount(0);
  return page.locator('[data-toast-id]').first();
}

/** The fixture's toast ids in the order of the given attribute (`data-closed`, `data-removed`). */
export async function recorded(fixture: Locator, attribute: 'data-closed' | 'data-removed' | 'data-reports'): Promise<string[]> {
  return ((await fixture.getAttribute(attribute)) ?? '').split(' ').filter(Boolean);
}

/** The centre of an element in CSS pixels of the viewport. */
export async function centreOf(locator: Locator): Promise<{ x: number; y: number }> {
  const box = await locator.boundingBox();
  if (!box) {
    throw new Error('The element has no box');
  }
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/**
 * Drags with the real mouse the way Base UI's `simulateSwipe` does (ToastRoot.test.tsx:15-76): a press at `start`, a move
 * one pixel toward the end (the gesture's first move re-anchors), the move to the end in `steps`, and the release, on the
 * element under the pointer.
 */
export async function mouseSwipe(
  page: Page,
  start: { x: number; y: number },
  delta: { x: number; y: number },
  options: { steps?: number; beforeRelease?: () => Promise<void> } = {},
): Promise<void> {
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(start.x + Math.sign(delta.x), start.y + Math.sign(delta.y));
  await page.mouse.move(start.x + delta.x, start.y + delta.y, { steps: options.steps ?? 4 });
  await options.beforeRelease?.();
  await page.mouse.up();
}
