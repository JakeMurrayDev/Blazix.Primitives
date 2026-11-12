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

// MenuFilterRoot.initialHighlight.test.tsx on `menu-filter-initial` (MNU-STA-22, KEY-22, FOC-*; M5 track 12), titles
// verbatim unless marked "port:". The keyboard opens a filterable menu with its input focused and the first enabled
// item highlighted; a pointer or hover open highlights nothing.
test.describe('filterable menu initial highlight', () => {
  async function open(page: Page, mode: RenderMode, hover = false): Promise<Locator> {
    await page.goto(`/${mode}/menu-filter-initial${hover ? '?hover=true' : ''}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('menu-filter-initial'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    return fixture;
  }

  function trigger(page: Page): Locator {
    return page.getByRole('button', { name: 'Actions' });
  }

  async function idOf(locator: Locator): Promise<string> {
    return (await locator.getAttribute('id')) ?? 'missing';
  }

  test('highlights the first enabled action and keeps the input in the arrow loop', async ({ page, mode }) => {
    await open(page, mode);
    await trigger(page).focus();
    await page.keyboard.press('Enter');

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));

    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Delete')));
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    await expect(input(page)).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('ArrowUp');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Delete')));
    await page.keyboard.press('ArrowUp');
    await page.keyboard.press('ArrowUp');
    await page.keyboard.press('ArrowUp');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    await expect(input(page)).toBeFocused();
  });

  test('clears the initial highlight when typing and permits an empty result', async ({ page, mode }) => {
    await open(page, mode);
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(input(page)).toBeFocused();

    await page.keyboard.type('Del');
    await expect(input(page)).toHaveValue('Del');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    await expect(item(page, 'Rename')).toHaveCount(0);

    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Delete')));

    await page.keyboard.type('x');
    await expect(input(page)).toHaveValue('Delx');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    await expect(page.getByText('No actions found.')).toBeVisible();
    await expect(input(page)).toBeFocused();
  });

  // Base UI's it.each over mouse, touch and pen; the mouse row, the one that focuses the input.
  test('handles opening with mouse', async ({ page, mode }) => {
    await open(page, mode);

    await trigger(page).click();

    await expect(input(page)).toBeFocused();
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
  });

  test('does not focus the input when opened on hover', async ({ page, mode }) => {
    await open(page, mode, true);

    await trigger(page).hover();

    await expect(input(page)).toBeVisible();
    await expect(input(page)).not.toBeFocused();
  });

  test('highlights the first action again when reopening', async ({ page, mode }) => {
    await open(page, mode);
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Escape');
    await expect(trigger(page)).toBeFocused();
    await page.keyboard.press('Enter');

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
  });
});

// MenuFilterRoot.test.tsx's autoHighlight titles (MNU-STA-19, API-55) on `menu-filter-root` with `auto`, titles verbatim:
// the typed query's first match is highlighted. The fixture's items are Rename, Delete and Duplicate, so the first match of
// "d" is Delete where Base UI's list (Rename, Duplicate, Delete) gives Duplicate.
test.describe('autoHighlight', () => {
  async function renderAuto(page: Page, mode: RenderMode, auto: 'true' | 'always'): Promise<Locator> {
    await page.goto(`/${mode}/menu-filter-root?auto=${auto}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('menu-filter-root'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    return fixture;
  }

  async function idOf(locator: Locator): Promise<string> {
    return (await locator.getAttribute('id')) ?? 'missing';
  }

  test('automatically highlights the first match after the user types', async ({ page, mode }) => {
    await renderAuto(page, mode, 'true');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    await input(page).focus();

    await page.keyboard.type('d');

    await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Delete')));
  });

  test('always highlights the first item when autoHighlight is "always"', async ({ page, mode }) => {
    await renderAuto(page, mode, 'always');

    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
  });

  test('clears the automatic highlight when a typed query is cleared', async ({ page, mode }) => {
    await renderAuto(page, mode, 'true');
    await input(page).focus();
    await page.keyboard.type('del');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', /.+/);

    await input(page).fill('');

    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
  });

  test('restores the first item when an always-highlighted query is cleared', async ({ page, mode }) => {
    await renderAuto(page, mode, 'always');
    await input(page).focus();
    await page.keyboard.type('del');
    await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');

    await input(page).fill('');

    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
  });
});

// The List's hand-back (FilterDropdownList.tsx:33-43, MenuFilterList.tsx:43-80; MNU-FOC-*, KEY-22; M5 track 12) on
// `menu-filter-root`: real focus that lands on the list or an item while the input owns the keyboard goes back to the
// input. MenuFilterPopup.test.tsx's titles verbatim, and a port title for the key replay.
test.describe('<Menu.Popup /> filter input focus', () => {
  function list(page: Page): Locator {
    return page.getByRole('menu');
  }

  test('returns focus to the input when the list takes it from the input', async ({ page, mode }) => {
    await render(page, mode);
    await input(page).focus();

    await list(page).focus();

    await expect(input(page)).toBeFocused();
  });

  test('leaves focus on the list when the input did not have it', async ({ page, mode }) => {
    await render(page, mode);
    await input(page).focus();
    await input(page).blur();

    await list(page).focus();

    await expect(list(page)).toBeFocused();
  });

  // MenuFilterList.tsx:72-79: a screen reader following aria-activedescendant focuses the highlighted item; the next key
  // returns focus to the input and moves the highlight as the input would.
  test('port: a key pressed on a focused item returns focus to the input and is routed as the input\'s', async ({ page, mode }) => {
    await render(page, mode);
    await input(page).focus();
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
    await item(page, 'Rename').focus();

    await page.keyboard.press('ArrowDown');

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Delete').getAttribute('id')) ?? 'missing');
  });
});
