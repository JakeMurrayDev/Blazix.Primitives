import { type Locator } from '@playwright/test';
import { expect, Fixture, test, type Page, type RenderMode } from './helpers';

// useMenuFilterItem.test.tsx and MenuFilterRoot.test.tsx's item-text and item-press titles (MNU-STA-17, PTR-20; M5 track
// 12, issue 197; docs/components/menu.md) on `menu-filter-items` (its cases are in the fixture's header), titles verbatim
// unless marked "port:", every title on both render modes. Base UI's controlled `value` and `fireEvent.change` are the
// input's own typing and `fill` here.

async function render(page: Page, mode: RenderMode, query: Record<string, string>): Promise<Locator> {
  await page.goto(`/${mode}/menu-filter-items?${new URLSearchParams(query).toString()}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('menu-filter-items'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

function input(page: Page, name = 'Filter actions'): Locator {
  return page.getByRole('searchbox', { name });
}

function item(page: Page, name: string): Locator {
  return page.getByRole('menuitem', { name, exact: true });
}

test.describe('filtered Menu items', () => {
  // MNU-PTR-20.
  test('keeps focus on the input when items are pressed', async ({ page, mode }) => {
    const fixture = await render(page, mode, { case: 'kinds' });
    await input(page).click();

    await item(page, 'Rename').click();
    await expect(input(page)).toBeFocused();
    await expect(fixture).toHaveAttribute('data-presses', 'Rename');

    await item(page, 'Open details').click();
    await expect(input(page)).toBeFocused();

    const checkboxItem = page.getByRole('menuitemcheckbox', { name: 'Show details' });
    await checkboxItem.click();
    await expect(input(page)).toBeFocused();
    await expect(checkboxItem).toHaveAttribute('aria-checked', 'true');

    const radioItem = page.getByRole('menuitemradio', { name: 'Sort by date' });
    await radioItem.click();
    await expect(input(page)).toBeFocused();
    await expect(radioItem).toHaveAttribute('aria-checked', 'true');
  });

  test.describe('item text resolution', () => {
    // MNU-STA-17.
    test('matches text that a descendant rendered after the item registered', async ({ page, mode }) => {
      await render(page, mode, { case: 'async' });

      await page.getByRole('button', { name: 'set label' }).click();
      await expect(item(page, 'Rename')).toBeVisible();
      await input(page).focus();
      await page.keyboard.type('rename');

      await expect(item(page, 'Delete')).toHaveCount(0);
      await expect(item(page, 'Rename')).toBeVisible();
    });

    // MNU-STA-17.
    test('stops matching text that a descendant no longer renders', async ({ page, mode }) => {
      await render(page, mode, { case: 'async', text: 'Rename', next: '' });

      await page.getByRole('button', { name: 'set label' }).click();
      await expect(page.getByTestId('async-item')).toHaveText('');
      await input(page).focus();
      await page.keyboard.type('rename');

      await expect(page.getByTestId('async-item')).toHaveCount(0);
    });

    // MNU-STA-17.
    test('matches an item whose children are an array while it is filtered out', async ({ page, mode }) => {
      await render(page, mode, { case: 'array' });
      await input(page).focus();

      // Hide it, so its DOM node goes away and only the children remain as a text source.
      await page.keyboard.type('del');
      await expect(item(page, 'Rename')).toHaveCount(0);
      await input(page).fill('rena');

      await expect(item(page, 'Rename')).toBeVisible();
      await expect(item(page, 'Delete')).toHaveCount(0);
    });

    // MNU-STA-17.
    test('matches the updated text of an item rendered through its render element', async ({ page, mode }) => {
      await render(page, mode, { case: 'render' });

      await page.getByRole('button', { name: 'set label' }).click();
      await expect(item(page, 'Two')).toBeVisible();
      await input(page).fill('two');

      await expect(item(page, 'Two')).toBeVisible();
    });

    // MNU-STA-17.
    test('keeps the rendered text of a hidden item whose children render different text', async ({ page, mode }) => {
      const fixture = await render(page, mode, { case: 'translate' });
      await input(page).fill('usu');
      await expect(item(page, 'Zmień nazwę')).toHaveCount(0);

      // Re-render the hidden item with the same children.
      await page.getByRole('button', { name: 'tick' }).click();
      await expect(fixture).toHaveAttribute('data-tick', '1');
      await input(page).fill('zmie');

      await expect(item(page, 'Zmień nazwę')).toBeVisible();
      await expect(item(page, 'Usuń')).toHaveCount(0);
    });
  });

  // MNU-STA-17 (MenuFilterRoot.test.tsx).
  test('keeps component-rendered item text after the item is filtered out', async ({ page, mode }) => {
    await render(page, mode, { case: 'label' });
    await input(page).focus();

    await page.keyboard.type('del');
    await expect(item(page, 'Rename')).toHaveCount(0);
    await input(page).fill('ren');

    await expect(item(page, 'Rename')).toBeVisible();
  });

  // MNU-STA-17's deviation (i) (2026-11-11, design point 2): Blazor cannot read a hidden item's unrendered content, so a
  // hidden item matches by its cached text, and a text changed while it is hidden counts once it shows again. Base UI's
  // "re-filters an item whose rendered text changes while it is hidden" is restated on that rule.
  test('port: matches an item whose text changed while it was hidden by the new text once it shows again', async ({ page, mode }) => {
    await render(page, mode, { case: 'async', text: 'Archive', next: 'Rename' });
    await input(page).focus();
    await page.keyboard.type('rename');
    await expect(item(page, 'Archive')).toHaveCount(0);

    // Hidden, the item still matches by "Archive"; its new text is read once a query shows it again.
    await page.getByRole('button', { name: 'set label' }).click();
    await expect(item(page, 'Rename')).toHaveCount(0);
    await input(page).fill('');
    await expect(item(page, 'Rename')).toBeVisible();
    await input(page).fill('rename');

    await expect(item(page, 'Rename')).toBeVisible();
    await expect(item(page, 'Delete')).toHaveCount(0);
  });
});

// MenuFilterRoot.test.tsx's query titles over a "Fruit" menu (MNU-STA-18, STA-21, EVT-16, EVT-18, API-58); the rows tag
// them [L2], and their typing and arrow steps need a browser (ADR 13 §3 re-tag notes). Base UI's `setProps({ value })`
// is the page's "query" buttons.
test.describe('the filter query over a fruit menu', () => {
  const fruitInput = (page: Page): Locator => input(page, 'Filter fruit');

  async function changes(fixture: Locator): Promise<string[]> {
    const value = (await fixture.getAttribute('data-value-changes')) ?? '';
    return value === '' ? [] : value.split('|');
  }

  // MNU-STA-21, EVT-18.
  test('resets the input value once when the popup closes', async ({ page, mode }) => {
    const fixture = await render(page, mode, { case: 'closing' });
    await fruitInput(page).focus();
    await page.keyboard.type('app');
    await expect(fixture).toHaveAttribute('data-value-changes', /app:input-change$/);
    const before = (await changes(fixture)).length;

    await page.getByRole('button', { name: 'Close' }).click();

    await expect.poll(async () => (await changes(fixture)).slice(before)).toEqual([':popup-close']);
  });

  // MNU-EVT-16, API-58.
  test('leaves the uncontrolled query and visible items unchanged when a change is canceled', async ({ page, mode }) => {
    await render(page, mode, { case: 'cancel' });
    const filter = fruitInput(page);
    await filter.focus();
    await page.keyboard.press('ArrowDown');
    const appleId = (await item(page, 'Apple').getAttribute('id')) ?? 'missing';
    await expect(filter).toHaveAttribute('aria-activedescendant', appleId);
    await expect(filter).toHaveValue('app');
    await expect(item(page, 'Apple')).toBeVisible();
    await expect(item(page, 'Banana')).toHaveCount(0);

    // Typing is rejected.
    await page.keyboard.type('x');
    await expect(filter).toHaveValue('app');
    await expect(item(page, 'Apple')).toBeVisible();
    await expect(item(page, 'Banana')).toHaveCount(0);
    await expect(filter).toHaveAttribute('aria-activedescendant', appleId);

    // Clearing is rejected.
    await page.getByLabel('Clear filter').click();
    await expect(filter).toHaveValue('app');
    await expect(item(page, 'Apple')).toBeVisible();
    await expect(item(page, 'Banana')).toHaveCount(0);
    await expect(filter).toHaveAttribute('aria-activedescendant', appleId);
  });

  // MNU-STA-18, FOC-12.
  test('clears a positional highlight when a controlled query replaces the visible item', async ({ page, mode }) => {
    const fixture = await render(page, mode, { case: 'controlled', query: 'ban' });
    const filter = fruitInput(page);
    await filter.focus();
    await page.keyboard.press('ArrowDown');
    await expect(filter).toHaveAttribute('aria-activedescendant', (await item(page, 'Banana').getAttribute('id')) ?? 'missing');

    await page.getByRole('button', { name: 'query app' }).click();
    await filter.focus();

    await expect(item(page, 'Apple')).toBeVisible();
    await expect(item(page, 'Banana')).toHaveCount(0);
    await expect(filter).not.toHaveAttribute('aria-activedescendant', /.+/);
    await page.keyboard.press('Enter');
    await expect(fixture).not.toHaveAttribute('data-presses', /Apple/);
  });
});

// MenuFilterRoot.scoping.test.tsx's AppendMenu (MNU-STA-20, EVT-13); Base UI's setProps is the popup's "append" button,
// clicked in the page so focus stays on the input.
test.describe('items appended while filtering', () => {
  test('keeps the highlight when an item the query filters out is appended', async ({ page, mode }) => {
    const fixture = await render(page, mode, { case: 'append' });
    const filter = input(page, 'Filter reports');
    await filter.focus();
    await page.keyboard.type('rep');
    // On Server the query's highlight reset is C#'s and lands a leg later (ADR 01); the arrows wait for the filtered list.
    await expect(item(page, 'Other')).toHaveCount(0);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    const reportB = item(page, 'Report B');
    await expect(filter).toHaveAttribute('aria-activedescendant', (await reportB.getAttribute('id')) ?? 'missing');
    const before = (await fixture.getAttribute('data-highlights')) ?? '';

    await page.getByRole('button', { name: 'append' }).evaluate((element) => element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true })));
    await expect(page.getByRole('menuitem', { name: 'Zebra', includeHidden: true })).toHaveCount(0);
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

    await expect(item(page, 'Zebra')).toHaveCount(0);
    await expect(filter).toHaveAttribute('aria-activedescendant', (await reportB.getAttribute('id')) ?? 'missing');
    await expect(fixture).toHaveAttribute('data-highlights', before);
  });
});
