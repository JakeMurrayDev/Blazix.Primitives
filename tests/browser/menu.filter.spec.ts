import { type Locator } from '@playwright/test';
import { delayCircuit, expect, Fixture, test, type Page, type RenderMode } from './helpers';

// The titles of MenuFilterRoot.test.tsx and MenuFilterRoot.scoping.test.tsx that need the items' rendered text, which
// only a browser renders (design point 2 (b′): the menu-filter List handle reports it), and do not depend on virtual
// focus (M5 track 12, issue 197; docs/components/menu.md), on `menu-filter-root`, titles verbatim unless marked "port:",
// every title on both render modes. The query is set with `fill`, which inserts the text without keydown events: the
// input's keys reach the list navigation correctly only under virtual focus, which lands with the held branches.

async function render(page: Page, mode: RenderMode, query = ''): Promise<Locator> {
  const search = query === '' ? '' : `?query=${encodeURIComponent(query)}`;
  await page.goto(`/${mode}/menu-filter-root${search}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('menu-filter-root'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

function input(page: Page): Locator {
  return page.getByRole('searchbox', { name: 'Filter actions' });
}

function item(page: Page, name: string): Locator {
  return page.getByRole('menuitem', { name, exact: true });
}

function clear(page: Page): Locator {
  return page.locator('[aria-label="Clear filter"]');
}

test.describe('<Menu.FilterProvider><Menu.Root/></Menu.FilterProvider>', () => {
  // MNU-STA-15, STA-23, ARIA-16 (MenuFilterRoot.scoping.test.tsx).
  test('filters items by text and shows the empty state', async ({ page, mode }) => {
    await render(page, mode);

    await input(page).fill('del');

    await expect(item(page, 'Rename')).toHaveCount(0);
    await expect(item(page, 'Delete')).toBeVisible();
    await expect(page.getByRole('status')).toHaveCount(0);

    await input(page).fill('');
    await input(page).fill('zzz');

    await expect(page.getByRole('menuitem')).toHaveCount(0);
    await expect(page.getByRole('status')).toHaveText('No results');
  });

  // MNU-API-61, STA-23 (MenuFilterRoot.test.tsx).
  test('removes Empty once the query matches again', async ({ page, mode }) => {
    await render(page, mode);
    await expect(page.getByText('No results')).toHaveCount(0);

    await input(page).fill('zzz');
    await expect(page.getByText('No results')).toBeVisible();

    await input(page).fill('ren');

    await expect(page.getByText('No results')).toHaveCount(0);
    await expect(item(page, 'Rename')).toBeVisible();
  });

  // MNU-STA-16: a matching item is the same element before and after a query change; a filtered-out one renders none.
  test('port: keeps the matching items mounted', async ({ page, mode }) => {
    await render(page, mode);
    for (const name of ['Rename', 'Delete', 'Duplicate']) {
      await item(page, name).evaluate((element) => element.setAttribute('data-marked', ''));
    }

    await input(page).fill('e');
    await expect(page.locator('[role=menuitem][data-marked]')).toHaveCount(3);

    await input(page).fill('del');

    await expect(item(page, 'Rename')).toHaveCount(0);
    await expect(item(page, 'Delete')).toHaveAttribute('data-marked', '');
  });

  test.describe('input value change reasons', () => {
    // MNU-EVT-16, API-58.
    test('reports input-change while typing', async ({ page, mode }) => {
      const fixture = await render(page, mode);

      await input(page).fill('r');

      await expect(fixture).toHaveAttribute('data-value-changes', 'r:input-change');
    });

    // MNU-EVT-16, API-58.
    test('reports input-clear when the field is emptied', async ({ page, mode }) => {
      const fixture = await render(page, mode);

      await input(page).fill('r');
      await expect(fixture).toHaveAttribute('data-value-changes', 'r:input-change');
      await input(page).fill('');

      await expect(fixture).toHaveAttribute('data-value-changes', 'r:input-change|:input-clear');
    });

    // MNU-EVT-17, PTR-21, API-60.
    test('reports clear-press when the clear button is used', async ({ page, mode }) => {
      const fixture = await render(page, mode);

      await input(page).fill('r');
      await expect(clear(page)).toBeVisible();
      await clear(page).click();

      await expect(fixture).toHaveAttribute('data-value-changes', 'r:input-change|:clear-press');
      await expect(clear(page)).toHaveCount(0);
      await expect(page.getByRole('menuitem')).toHaveCount(3);
    });
  });

  // The Server substitute of the titles above (M5 track 12's rule: every pointer and keyboard title runs on Server under
  // `delayCircuit` at least once): an edit and a Clear press reach the provider in order across a slow circuit, and the
  // input ends empty.
  test('port: under a delayed circuit, an edit then a Clear press reach the provider in order', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Server only: the circuit between the browser and C#');
    await delayCircuit(page, 150);
    const fixture = await render(page, mode);

    await input(page).fill('re');
    await expect(clear(page)).toBeVisible();
    await clear(page).click();

    await expect(fixture).toHaveAttribute('data-value-changes', 're:input-change|:clear-press');
    await expect(input(page)).toHaveValue('');
    await expect(page.getByRole('menuitem')).toHaveCount(3);
  });
});

// Virtual focus (MNU-STA-22, KEY-22; MenuRoot.tsx:516-557, 622-638): the input holds real focus and is the list's
// reference, so the arrow keys move the highlight through aria-activedescendant and typed text stays in the input.
test.describe('virtual focus', () => {
  // MNU-STA-22, KEY-22: Base UI's "navigates the list from the input with aria-activedescendant" without its Enter step,
  // which is the key router's `activate`.
  test('port: navigates the list from the input with aria-activedescendant', async ({ page, mode }) => {
    await render(page, mode);
    await input(page).focus();

    await page.keyboard.press('ArrowDown');

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
  });

  // MNU-KEY-22, EVT-16: typing is the input's, not the list's typeahead.
  test('port: typing in the input filters the list while the input keeps focus', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await input(page).focus();

    await page.keyboard.type('del');

    await expect(input(page)).toHaveValue('del');
    await expect(input(page)).toBeFocused();
    await expect(item(page, 'Rename')).toHaveCount(0);
    await expect(item(page, 'Delete')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-value-changes', 'd:input-change|de:input-change|del:input-change');
  });

  // The Server substitute: each keystroke's commit arrives after the next keystroke, and none overwrites a later one.
  test('port: under a delayed circuit, typed text is never overwritten by an earlier commit', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Server only: the circuit between the browser and C#');
    await delayCircuit(page, 150);
    const fixture = await render(page, mode);
    await input(page).focus();

    await page.keyboard.type('del');

    await expect(fixture).toHaveAttribute('data-value-changes', 'd:input-change|de:input-change|del:input-change');
    await expect(input(page)).toHaveValue('del');
    await expect(item(page, 'Delete')).toBeVisible();
  });
});
