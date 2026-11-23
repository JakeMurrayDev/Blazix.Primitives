// L4 probes (ADR 06 §2), ScrollArea inside other families' popups: design point 12 (a) of the ScrollArea track, chosen by
// the user. Base UI composes ScrollArea inside its popups (the `scroll-area/inside-select.tsx` and `inside-menu.tsx`
// experiments, Dialog's `inside-scroll` demo, the NavigationMenu page's advice); no requirement row covers it, so each
// title is a port title. Fixtures: tests/Iodine.Headless.Fixtures.Client/ScrollArea/ScrollAreaProbe*Fixture.razor.
import { type Locator } from '@playwright/test';
import { expect, test, waitForPositioned, wheelOver, type Page } from './helpers';
import { gotoPopupFixture, settle } from './suites/popupFixture';

/** Two animation frames. */
const frames = (page: Page) =>
  page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));

/** The viewport's scroll offset. */
const scrollTop = (viewport: Locator) => viewport.evaluate((element) => element.scrollTop);

/** The thumb's offset from the top of its scrollbar, in pixels. */
const thumbOffset = (page: Page) =>
  page.getByTestId('thumb').evaluate((thumb) => thumb.getBoundingClientRect().top - thumb.parentElement!.getBoundingClientRect().top);

/** Whether an element's box lies wholly inside the viewport's box (a pixel of tolerance for subpixel layout). */
async function insideViewport(viewport: Locator, item: Locator): Promise<boolean> {
  return item.evaluate((element, container) => {
    const box = element.getBoundingClientRect();
    const clip = container!.getBoundingClientRect();
    return box.top >= clip.top - 1 && box.bottom <= clip.bottom + 1;
  }, await viewport.elementHandle());
}

/** Waits until the thumb is measured and visible inside its scrollbar. */
async function thumbMeasured(page: Page): Promise<void> {
  await expect.poll(() => page.getByTestId('thumb').evaluate((thumb) => getComputedStyle(thumb).visibility)).toBe('visible');
  await expect.poll(async () => (await page.getByTestId('thumb').boundingBox())?.height ?? 0).toBeGreaterThan(0);
}

test.describe('ScrollArea probes', () => {
  test('port: probe: ScrollArea inside Select keeps the keyboard highlight in view and moves the thumb', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'scroll-area-probe-select');
    const trigger = page.getByTestId('trigger');
    await trigger.focus();
    await settle(fixture);
    await page.keyboard.press('ArrowDown');

    const viewport = page.getByTestId('viewport');
    await expect(page.getByRole('listbox')).toBeVisible();
    await waitForPositioned(page.getByTestId('positioner'));
    await expect(page.getByRole('option', { name: 'Item 1', exact: true })).toBeFocused();
    await thumbMeasured(page);
    expect(await scrollTop(viewport)).toBe(0);
    const before = await thumbOffset(page);

    // 200 px of 30 px items shows six and a part; fifteen presses reach Item 16.
    for (let i = 0; i < 15; i++) {
      await page.keyboard.press('ArrowDown');
    }
    const highlighted = page.getByRole('option', { name: 'Item 16', exact: true });
    await expect(highlighted).toBeFocused();
    await expect(highlighted).toHaveAttribute('data-highlighted', '');
    await expect.poll(() => insideViewport(viewport, highlighted)).toBe(true);
    expect(await scrollTop(viewport)).toBeGreaterThan(0);
    await expect.poll(() => thumbOffset(page)).toBeGreaterThan(before);
  });

  test('port: probe: ScrollArea inside Menu keeps the keyboard highlight in view and moves the thumb', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'scroll-area-probe-menu');
    await page.getByTestId('trigger').focus();
    await settle(fixture);
    await page.keyboard.press('ArrowDown');

    const viewport = page.getByTestId('viewport');
    await expect(page.getByRole('menu')).toBeVisible();
    await waitForPositioned(page.getByTestId('positioner'));
    await expect(page.getByRole('menuitem', { name: 'Item 1', exact: true })).toBeFocused();
    await thumbMeasured(page);
    expect(await scrollTop(viewport)).toBe(0);
    const before = await thumbOffset(page);

    for (let i = 0; i < 15; i++) {
      await page.keyboard.press('ArrowDown');
    }
    const highlighted = page.getByRole('menuitem', { name: 'Item 16', exact: true });
    await expect(highlighted).toBeFocused();
    await expect(highlighted).toHaveAttribute('data-highlighted', '');
    await expect.poll(() => insideViewport(viewport, highlighted)).toBe(true);
    expect(await scrollTop(viewport)).toBeGreaterThan(0);
    await expect.poll(() => thumbOffset(page)).toBeGreaterThan(before);
  });

  test('port: probe: ScrollArea inside Combobox keeps the keyboard highlight in view, moves the thumb and keeps filtering', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'scroll-area-probe-combobox');
    const input = page.getByTestId('input');
    await input.focus();
    await settle(fixture);
    await page.keyboard.press('ArrowDown');

    const viewport = page.getByTestId('viewport');
    await expect(page.getByRole('listbox')).toBeVisible();
    await waitForPositioned(page.getByTestId('positioner'));
    await expect(page.getByRole('option')).toHaveCount(50);
    await thumbMeasured(page);
    expect(await scrollTop(viewport)).toBe(0);
    const before = await thumbOffset(page);

    // The highlight is virtual: the focus stays in the input while the arrows move `data-highlighted`.
    const highlightedOption = page.locator('[role="option"][data-highlighted]');
    await page.keyboard.press('ArrowDown');
    await expect(highlightedOption).toHaveCount(1);
    const start = Number((await highlightedOption.textContent())!.replace('Item ', ''));
    for (let i = start; i < 16; i++) {
      await page.keyboard.press('ArrowDown');
    }
    const highlighted = page.getByRole('option', { name: 'Item 16', exact: true });
    await expect(highlighted).toHaveAttribute('data-highlighted', '');
    await expect(input).toBeFocused();
    await expect(input).toHaveAttribute('aria-activedescendant', (await highlighted.getAttribute('id'))!);
    await expect.poll(() => insideViewport(viewport, highlighted)).toBe(true);
    expect(await scrollTop(viewport)).toBeGreaterThan(0);
    await expect.poll(() => thumbOffset(page)).toBeGreaterThan(before);

    // Filtering still works inside the ScrollArea: "Item 4" and "Item 40" .. "Item 49".
    await page.keyboard.type('Item 4');
    await expect(input).toHaveValue('Item 4');
    await expect(page.getByRole('option')).toHaveCount(11);
    await expect(input).toBeFocused();
    await expect(page.getByRole('option', { name: 'Item 4', exact: true })).toBeVisible();
  });

  test('port: probe: ScrollArea inside NavigationMenu content scrolls with the wheel and keeps the panel size', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'scroll-area-probe-navigation-menu');
    await page.getByTestId('trigger').click();
    await settle(fixture);

    const popup = page.getByTestId('popup');
    const viewport = page.getByTestId('viewport');
    await expect(page.getByTestId('content')).toBeVisible();
    await waitForPositioned(page.getByTestId('positioner'));
    await thumbMeasured(page);
    await frames(page);
    const size = await popup.boundingBox();
    const root = (await page.getByTestId('root').boundingBox())!;
    expect(size!.width).toBeGreaterThanOrEqual(root.width);
    expect(size!.height).toBeGreaterThanOrEqual(root.height);
    expect(Math.round(root.height)).toBe(200);
    expect(await scrollTop(viewport)).toBe(0);
    const before = await thumbOffset(page);

    await wheelOver(viewport, 0, 120);
    await expect.poll(() => scrollTop(viewport)).toBeGreaterThan(0);
    await expect.poll(() => thumbOffset(page)).toBeGreaterThan(before);
    await expect(page.getByTestId('content')).toBeVisible();
    await frames(page);
    expect(await popup.boundingBox()).toEqual(size);
    expect(await page.getByTestId('root').boundingBox()).toEqual(root);
  });

  test('port: probe: ScrollArea inside Dialog keeps the focus trapped, reaches the viewport and scrolls with the wheel', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'scroll-area-probe-dialog');
    await page.getByTestId('trigger').click();
    await settle(fixture);

    const popup = page.getByTestId('popup');
    const viewport = page.getByTestId('viewport');
    await expect(popup).toBeVisible();
    await thumbMeasured(page);
    // The viewport is focusable once the measurement found its content overflowing.
    await expect(viewport).toHaveAttribute('tabindex', '0');

    // A Tab past the last tabbable lands on the trap's inside guard, which moves the focus on in a frame (Base UI's
    // `enqueueFocus`): read where the focus settles.
    const settledFocus = async () => {
      let now = { id: null as string | null, inside: false, guard: true };
      await expect
        .poll(async () => {
          now = await page.evaluate(() => {
            const active = document.activeElement;
            return {
              id: active?.getAttribute('data-testid') ?? active?.tagName ?? null,
              inside: !!active && !!document.querySelector('[data-testid="popup"]')?.contains(active),
              guard: !!active?.hasAttribute('data-base-ui-focus-guard'),
            };
          });
          return now.guard;
        })
        .toBe(false);
      return now;
    };
    // The initial focus goes to the popup's first tabbable, which is the viewport or Close depending on whether the
    // overflow was measured by then; either way it is inside.
    expect((await settledFocus()).inside).toBe(true);
    const forward: Array<string | null> = [];
    for (let i = 0; i < 4; i++) {
      await page.keyboard.press('Tab');
      const now = await settledFocus();
      expect(now.inside, `Tab ${i + 1} left the popup for ${now.id}`).toBe(true);
      forward.push(now.id);
    }
    // The viewport (tabindex 0 while it overflows) and Close are the popup's tabbables, and Tab loops between them.
    expect([...new Set(forward)].sort()).toEqual(['close', 'viewport']);
    expect(forward.every((id, i) => i === 0 || id !== forward[i - 1])).toBe(true);
    const backward: Array<string | null> = [];
    for (let i = 0; i < 4; i++) {
      await page.keyboard.press('Shift+Tab');
      const now = await settledFocus();
      expect(now.inside, `Shift+Tab ${i + 1} left the popup for ${now.id}`).toBe(true);
      backward.push(now.id);
    }
    expect([...new Set(backward)].sort()).toEqual(['close', 'viewport']);
    expect(backward.every((id, i) => i === 0 || id !== backward[i - 1])).toBe(true);

    expect(await scrollTop(viewport)).toBe(0);
    const before = await thumbOffset(page);
    await wheelOver(viewport, 0, 120);
    await expect.poll(() => scrollTop(viewport)).toBeGreaterThan(0);
    await expect.poll(() => thumbOffset(page)).toBeGreaterThan(before);
    await expect(popup).toBeVisible();
  });
});
