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

  // MNU-KEY-27 (MenuList.test.tsx, titles verbatim): a screen reader following aria-activedescendant focuses the item.
  test('replays keys from an item that holds real focus and activates on Enter', async ({ page, mode }) => {
    const fixture = await render(page, mode);
    await input(page).focus();
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');

    await item(page, 'Rename').focus();
    await page.keyboard.press('ArrowDown');

    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Delete').getAttribute('id')) ?? 'missing');
    await expect(input(page)).toBeFocused();

    await item(page, 'Delete').focus();
    await page.keyboard.press('Enter');

    // Base UI's last step reads the input's empty value under jsdom, where the closing popup is still mounted; here the
    // press closes the menu and the popup unmounts, so the Enter typed nothing and was the item's once.
    await expect(fixture).toHaveAttribute('data-presses', 'Delete');
    await expect(input(page)).toHaveCount(0);
  });

  test.describe('keys on a focused list', () => {
    // Focus the list takes from the input goes straight back to it, so start from focus that reached the list another
    // way, such as a screen reader or a touch press. The keys are dispatched on the list, as Base UI's fireEvent does.
    async function focusList(page: Page): Promise<void> {
      await input(page).blur();
      await list(page).focus();
      await expect(list(page)).toBeFocused();
    }

    function keyDown(page: Page, key: string): Promise<boolean> {
      return list(page).evaluate((element, pressed) => element.dispatchEvent(new KeyboardEvent('keydown', { key: pressed, bubbles: true, cancelable: true })), key);
    }

    test('replays navigation keys on the input instead of scrolling', async ({ page, mode }) => {
      await render(page, mode);
      await input(page).focus();
      await focusList(page);

      const scrolls = await keyDown(page, 'ArrowDown');

      expect(scrolls).toBe(false);
      await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
      await expect(input(page)).toBeFocused();
    });

    test('commits the highlighted item when Enter lands on the list', async ({ page, mode }) => {
      const fixture = await render(page, mode);
      await input(page).focus();
      await page.keyboard.press('ArrowDown');
      await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');

      await focusList(page);
      await keyDown(page, 'Enter');

      await expect(fixture).toHaveAttribute('data-presses', 'Rename');
    });

    test('still closes the menu with Escape', async ({ page, mode }) => {
      await render(page, mode);
      await input(page).focus();
      await focusList(page);

      await keyDown(page, 'Escape');

      await expect(page.getByRole('dialog')).toHaveCount(0);
    });

    test('hands typing keys back to the input', async ({ page, mode }) => {
      await render(page, mode);
      await input(page).focus();
      await focusList(page);

      const types = await keyDown(page, 'z');

      expect(types).toBe(true);
      await expect(input(page)).toBeFocused();
    });
  });
});

// The Popup's refocus (useMenuFilterPopup.ts:50-143; MNU-KEY-33, FOC-14, PTR-22; M5 track 12) on `menu-filter-root`
// with `extra`, `orientation` and `dir`. MenuFilterPopup.test.tsx's and MenuFilterRoot.test.tsx's titles verbatim, keys
// and pointer moves dispatched as Base UI's fireEvent does, and port titles for the background press and a real pointer.
test.describe('the filterable popup\'s refocus', () => {
  async function renderWith(page: Page, mode: RenderMode, search: string): Promise<Locator> {
    await page.goto(`/${mode}/menu-filter-root?${search}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('menu-filter-root'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    return fixture;
  }

  function extra(page: Page): Locator {
    return page.getByRole('button', { name: 'Extra action' });
  }

  for (const [orientation, direction, key] of [
    ['vertical', 'ltr', 'ArrowLeft'],
    ['vertical', 'rtl', 'ArrowRight'],
    ['horizontal', 'ltr', 'ArrowUp'],
    ['horizontal', 'rtl', 'ArrowUp'],
  ] as const) {
    test(`returns focus with the ${orientation} ${direction} close key`, async ({ page, mode }) => {
      await renderWith(page, mode, `extra=true&orientation=${orientation}&dir=${direction}`);
      await extra(page).focus();
      await expect(extra(page)).toBeFocused();

      await extra(page).evaluate((element, pressed) => element.dispatchEvent(new KeyboardEvent('keydown', { key: pressed, bubbles: true, cancelable: true })), key);

      await expect(input(page)).toBeFocused();
    });
  }

  test('leaves focus on another control in the popup when the pointer moves', async ({ page, mode }) => {
    await renderWith(page, mode, 'extra=true');
    await extra(page).focus();

    await page.getByTestId('popup').evaluate((element) => element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true })));

    await expect(extra(page)).toBeFocused();
  });

  // PTR-22 (useMenuFilterPopup.ts:100-105): a press on the popup's own background keeps focus on the input.
  test('port: a press on the popup background is prevented, so the input keeps focus', async ({ page, mode }) => {
    await renderWith(page, mode, 'extra=true');
    await input(page).focus();

    const kept = await page.getByTestId('popup').evaluate((element) => !element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true })));

    expect(kept).toBe(true);
    await expect(input(page)).toBeFocused();
  });

  // FOC-14 (useMenuFilterPopup.ts:48-95): a mouse moving over the popup, while focus is outside it, returns focus to the input.
  test('port: a mouse moving over the popup returns focus to the input after focus left the popup', async ({ page, mode }) => {
    await renderWith(page, mode, 'extra=true');
    await input(page).focus();
    await input(page).blur();
    await expect(input(page)).not.toBeFocused();

    const box = (await page.getByTestId('popup').boundingBox())!;
    await page.mouse.move(box.x + 4, box.y + 4);
    await page.mouse.move(box.x + 8, box.y + 6);

    await expect(input(page)).toBeFocused();
  });
});

// Menu.Clear's press (FilterDropdownClear.tsx:46-58; MNU-PTR-21; M5 track 12) on `menu-filter-root`: the press keeps
// focus on the input, and the clear resets the highlight per autoHighlight.
test.describe('Menu.Clear', () => {
  test('clears a navigated highlight when Clear is pressed', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-root?auto=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-root'))).toHaveAttribute('data-attached', 'true');
    await input(page).focus();
    await page.keyboard.type('e');
    // The typed query's automatic highlight is C#'s, which reaches the module one leg later on Server (ADR 01's
    // "JS-owned values that originate in C# state" row); Base UI applies it before the next key.
    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');

    await clear(page).click();

    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    await expect(item(page, 'Delete')).not.toHaveAttribute('data-highlighted');
  });

  // PTR-21: the press does not take focus from the input, and the click returns it there.
  test('port: a press on Clear keeps focus on the input', async ({ page, mode }) => {
    await render(page, mode);
    await input(page).focus();
    await page.keyboard.type('r');
    await expect(clear(page)).toBeVisible();

    await clear(page).click();

    await expect(input(page)).toHaveValue('');
    await expect(input(page)).toBeFocused();
  });
});
