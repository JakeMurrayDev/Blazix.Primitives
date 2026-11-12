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

  // PTR-21 (FilterDropdownClear.tsx:46-49): the press's default is prevented, so focus never leaves the input.
  test('port: a press on Clear is prevented, so focus stays on the input', async ({ page, mode }) => {
    await render(page, mode);
    await input(page).focus();
    await page.keyboard.type('r');
    await expect(clear(page)).toBeVisible();

    const prevented = await clear(page).evaluate((element) => !element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true })));

    expect(prevented).toBe(true);
    await expect(input(page)).toBeFocused();
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

// MenuList.test.tsx's "horizontal list without autoHighlight" (MNU-KEY-24, KEY-22; M5 track 12), titles verbatim, on
// `menu-filter-root` and `menu-filter-initial` with `orientation=horizontal`. The fixture's items are Rename, Delete and
// Duplicate: "l" keeps Delete and Duplicate, where Base UI's "p" keeps Alpha and Apple of Alpha, Apple and Beta.
test.describe('horizontal list without autoHighlight', () => {
  async function renderHorizontal(page: Page, mode: RenderMode, fixtureName: string): Promise<void> {
    await page.goto(`/${mode}/${fixtureName}?orientation=horizontal`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive(fixtureName))).toHaveAttribute('data-attached', 'true');
  }

  async function idOf(locator: Locator): Promise<string> {
    return (await locator.getAttribute('id')) ?? 'missing';
  }

  test('enters the results with the vertical arrows after typing', async ({ page, mode }) => {
    await renderHorizontal(page, mode, 'menu-filter-root');
    await input(page).focus();

    await page.keyboard.type('l');
    await expect(item(page, 'Rename')).toHaveCount(0);

    // Left and Right keep moving the caret while nothing is highlighted.
    await page.keyboard.press('ArrowLeft');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);

    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Delete')));
    await page.keyboard.press('ArrowRight');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Duplicate')));
    await page.keyboard.press('ArrowLeft');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Delete')));
  });

  test('enters the results from the end with ArrowUp', async ({ page, mode }) => {
    await renderHorizontal(page, mode, 'menu-filter-root');
    await input(page).focus();

    await page.keyboard.press('ArrowUp');

    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Duplicate')));
  });

  // The fixture's first item is disabled, so the first item highlighted is Rename.
  test('highlights the first item when opened from the trigger with ArrowDown', async ({ page, mode }) => {
    await renderHorizontal(page, mode, 'menu-filter-initial');
    await page.getByRole('button', { name: 'Actions' }).focus();

    await page.keyboard.press('ArrowDown');

    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
  });
});

// MenuFilterRoot.scoping.test.tsx's "trigger key relay" (MNU-KEY-28; MenuFilterDropdown.tsx:124-150; M5 track 12) on
// `menu-filter-initial` with `hover=true&plain=true`, titles verbatim: a hover open leaves focus on the trigger, and the
// trigger relays the vertical arrows and typed characters to the input. "lets a consumer onKeyDown cancel the relay"
// needs the `navigate` decision point (design point 6), which is not built.
test.describe('trigger key relay', () => {
  async function openByHover(page: Page, mode: RenderMode): Promise<Locator> {
    await page.goto(`/${mode}/menu-filter-initial?hover=true&plain=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-attached', 'true');
    const trigger = page.getByRole('button', { name: 'Actions' });
    await trigger.focus();
    await trigger.hover();
    await expect(input(page)).toBeVisible();
    // A hover open shows the popup without moving focus off the trigger.
    await expect(trigger).toBeFocused();
    return trigger;
  }

  test('moves ArrowDown from the trigger into the input and highlights the first item', async ({ page, mode }) => {
    await openByHover(page, mode);

    await page.keyboard.press('ArrowDown');

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
  });

  test('moves a typed character from the trigger into the input', async ({ page, mode }) => {
    await openByHover(page, mode);

    await page.keyboard.press('r');

    await expect(input(page)).toBeFocused();
  });

  // Ctrl+R is dispatched, as user-event does, since a real one reloads the page.
  test('keeps focus on the trigger for modified keys and cross-axis arrows', async ({ page, mode }) => {
    const trigger = await openByHover(page, mode);

    await trigger.evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { key: 'r', ctrlKey: true, bubbles: true, cancelable: true })));
    await expect(trigger).toBeFocused();

    await page.keyboard.press('ArrowRight');
    await expect(trigger).toBeFocused();
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
  });
});

// MenuFilterRoot.test.tsx's "focus ownership inside the popup" with a filterable submenu (MNU-PTR-22, FOC-14; M5 track
// 12) on `menu-filter-root` with `submenu=true`, titles verbatim, mouse moves dispatched as Base UI's fireEvent does.
test.describe('focus ownership inside the popup', () => {
  async function renderNested(page: Page, mode: RenderMode): Promise<void> {
    await page.goto(`/${mode}/menu-filter-root?submenu=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-root'))).toHaveAttribute('data-attached', 'true');
  }

  function mouseMove(locator: Locator): Promise<boolean> {
    return locator.evaluate((element) => element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true })));
  }

  const submenuInput = (page: Page): Locator => page.getByRole('searchbox', { name: 'Filter folders' });
  const submenuTrigger = (page: Page): Locator => page.getByRole('menuitem', { name: 'Move to folder' });

  test('returns focus to the parent input when the pointer moves back over the parent popup', async ({ page, mode }) => {
    await renderNested(page, mode);
    await submenuTrigger(page).hover();
    await expect(submenuInput(page)).toBeVisible();
    await mouseMove(submenuInput(page));
    await expect(submenuInput(page)).toHaveAttribute('data-highlighted', '');
    await submenuInput(page).click();
    await expect(submenuInput(page)).toBeFocused();

    // Back over the still-open submenu's trigger.
    await mouseMove(submenuTrigger(page));

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('data-highlighted', '');
    await expect(submenuInput(page)).not.toHaveAttribute('data-highlighted');
    await expect(submenuInput(page)).toBeVisible();
  });

  // useMenuFilterPopup.ts:25-28, 80-84: focus that entered a nested popup by keyboard stays there while the pointer
  // crosses the parent popup.
  test('port: focus a keyboard moved into a submenu stays there while the pointer crosses the parent popup', async ({ page, mode }) => {
    await renderNested(page, mode);
    await input(page).focus();
    for (let i = 0; i < 4; i += 1) {
      await page.keyboard.press('ArrowDown');
    }
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await submenuTrigger(page).getAttribute('id')) ?? 'missing');
    // Enter clicks the highlighted trigger (the router's activate), a keyboard open that focuses the submenu's input.
    await page.keyboard.press('Enter');
    await expect(submenuInput(page)).toBeFocused();

    await mouseMove(page.getByTestId('popup'));

    await expect(submenuInput(page)).toBeFocused();
  });

  test('opens a hovered submenu without moving focus until the pointer enters it', async ({ page, mode }) => {
    await renderNested(page, mode);
    await expect(input(page)).toBeFocused();

    await submenuTrigger(page).hover();
    await expect(submenuInput(page)).toBeVisible();
    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('data-highlighted', '');

    await mouseMove(page.getByTestId('submenu-popup'));

    await expect(submenuInput(page)).toBeFocused();
    await expect(submenuInput(page)).toHaveAttribute('data-highlighted', '');
  });
});

// Titles of MenuFilterRoot.test.tsx and MenuList.test.tsx that run on `menu-filter-root` as it is (M5 track 12), verbatim:
// MNU-DIS-02, KEY-14, DOM-24 and ARIA-06. The trees' items differ (Rename, Delete and Duplicate here); the keys follow them.
test.describe('filterable menu keys and semantics', () => {
  async function renderWith(page: Page, mode: RenderMode, search = ''): Promise<Locator> {
    await page.goto(`/${mode}/menu-filter-root${search}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('menu-filter-root'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    return fixture;
  }

  test('closes on Escape even with a query typed', async ({ page, mode }) => {
    await renderWith(page, mode);
    await input(page).fill('ren');
    await expect(input(page)).toHaveValue('ren');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('Escape');

    // Escape closes outright; it does not clear the query first.
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Actions' })).toBeFocused();
  });

  test('does not move the highlight while typing into the input', async ({ page, mode }) => {
    await renderWith(page, mode);
    await input(page).focus();

    await page.keyboard.type('d');

    await expect(item(page, 'Rename')).toHaveCount(0);
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
  });

  test('keeps virtual focus on the input without making the list tabbable', async ({ page, mode }) => {
    await renderWith(page, mode);
    const list = page.getByRole('menu');
    const rename = item(page, 'Rename');
    await expect(input(page)).toBeFocused();
    await expect(list).not.toHaveAttribute('aria-hidden');
    await expect(list).toHaveAttribute('tabindex', '-1');
    await expect(rename).toHaveAttribute('tabindex', '-1');
    await expect(rename).not.toHaveAttribute('aria-expanded');

    await page.keyboard.press('ArrowDown');

    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await rename.getAttribute('id')) ?? 'missing');
    await expect(input(page)).toBeFocused();
  });

  // MNU-FOC-13 (MenuFilterRoot.scoping.test.tsx): Menu's focus manager is modal only under a context menu (MenuPopup.tsx:197),
  // so a modal filterable popup hides nothing outside it, and Tab leaves it and closes it.
  test('lets Tab leave a modal filterable popup and closes it', async ({ page, mode }) => {
    await renderWith(page, mode, '?after=true');
    await expect(input(page)).toBeFocused();
    const after = page.getByTestId('after');
    await expect(after).not.toHaveAttribute('aria-hidden', 'true');
    await expect(after).not.toHaveAttribute('inert');

    await page.keyboard.press('Tab');

    await expect(after).toBeFocused();
    await expect(page.getByRole('menu')).toHaveCount(0);
  });

  // MNU-DIS-08 (MenuFilterRoot.test.tsx): the same Tab from a non-modal root.
  test('moves focus past the menu when tabbing from the input', async ({ page, mode }) => {
    await renderWith(page, mode, '?after=true&modal=false');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('Tab');

    await expect(page.getByTestId('after')).toBeFocused();
  });

  // MNU-PTR-20 (useMenuItemCommonProps.ts:12-14, 84): a press on an item keeps focus on the input.
  test('preserves input focus on item presses', async ({ page, mode }) => {
    await renderWith(page, mode);

    const prevented = await item(page, 'Rename').evaluate((element) => !element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true })));

    expect(prevented).toBe(true);
  });

  // MNU-KEY-26 (useMenuFilterKeyDown.ts:179-192): backward tabbing mirrors a plain menu.
  test('closes and returns focus to the trigger on Shift+Tab from the input', async ({ page, mode }) => {
    await renderWith(page, mode);
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('Shift+Tab');

    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Actions' })).toBeFocused();
  });

  test('keeps input focus when a Shift+Tab close is canceled', async ({ page, mode }) => {
    await renderWith(page, mode, '?cancel=true');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('Shift+Tab');

    await expect(page.getByRole('menu')).toBeVisible();
    await expect(input(page)).toBeFocused();
  });

  test('closes the popup with Escape and returns focus to the trigger', async ({ page, mode }) => {
    await renderWith(page, mode);
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('Escape');

    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Actions' })).toBeFocused();
  });

  // MNU-KEY-22, KEY-30 (useMenuFilterKeyDown.ts:84-127): the keydowns are dispatched as Base UI's fireEvent does; a handler
  // that prevented them would break the browser's own selection and word movement.
  test('leaves modified editing keys to the input while an item is highlighted', async ({ page, mode }) => {
    await renderWith(page, mode);
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    const renameId = (await item(page, 'Rename').getAttribute('id')) ?? 'missing';
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);

    const dispatch = (init: KeyboardEventInit): Promise<boolean> =>
      input(page).evaluate((element, options) => element.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...options })), init);

    expect(await dispatch({ key: 'ArrowDown', shiftKey: true })).toBe(true);
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
    expect(await dispatch({ key: 'ArrowLeft', shiftKey: true })).toBe(true);
    expect(await dispatch({ key: 'ArrowRight', altKey: true })).toBe(true);
    expect(await dispatch({ key: 'Home', metaKey: true })).toBe(true);
    expect(await dispatch({ key: 'End', shiftKey: true })).toBe(true);
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
  });

  // MNU-KEY-25 (useMenuFilterItem.test.tsx): the fixture's disabled item is Delete, the second.
  test('does not activate a highlighted disabled item with Enter', async ({ page, mode }) => {
    const fixture = await renderWith(page, mode, '?disabled=true');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Delete').getAttribute('id')) ?? 'missing');
    await page.keyboard.press('Enter');

    await expect(fixture).toHaveAttribute('data-presses', '');
    await expect(page.getByRole('menu')).toBeVisible();
  });

  // MNU-KEY-05.
  test('uses Home and End for list navigation after an item is highlighted', async ({ page, mode }) => {
    await renderWith(page, mode);
    await input(page).focus();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Delete').getAttribute('id')) ?? 'missing');

    await page.keyboard.press('Home');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
    await page.keyboard.press('End');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Duplicate').getAttribute('id')) ?? 'missing');
  });

  // MNU-STA-15, API-55: Base UI types "Google" then a space over Google Calendar and Google Chrome; here "de" then a space
  // keeps Delete.
  for (const auto of ['true', 'always'] as const) {
    test(`keeps a highlight when typing does not change the trimmed query with autoHighlight=${auto}`, async ({ page, mode }) => {
      await renderWith(page, mode, `?auto=${auto}`);
      await input(page).focus();
      await page.keyboard.type('de');
      await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');

      await page.keyboard.type(' ');

      await expect(input(page)).toHaveValue('de ');
      await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');
      await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Delete').getAttribute('id')) ?? 'missing');
    });

    // API-55, STA-19 (MenuRoot.tsx:528): the automatic highlight keeps the arrows inside the list. Base UI's list ends at
    // Delete; this one at Duplicate.
    test(`does not allow arrow navigation to escape when autoHighlight is ${auto}`, async ({ page, mode }) => {
      await renderWith(page, mode, `?auto=${auto}`);
      await input(page).focus();
      if (auto === 'true') {
        await page.keyboard.type('e');
      }
      await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');

      await page.keyboard.press('ArrowUp');
      await expect(item(page, 'Duplicate')).toHaveAttribute('data-highlighted', '');
      await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Duplicate').getAttribute('id')) ?? 'missing');

      await page.keyboard.press('ArrowDown');
      await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
      await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
    });
  }

  // MenuList.test.tsx's tree uses autoHighlight="always", so the first item is highlighted from the open.
  test('exposes horizontal navigation on the menu instead of the dialog', async ({ page, mode }) => {
    await renderWith(page, mode, '?orientation=horizontal&auto=always');
    await expect(page.getByRole('menu')).toHaveAttribute('aria-orientation', 'horizontal');
    await expect(page.getByRole('dialog')).not.toHaveAttribute('aria-orientation');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('ArrowRight');

    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Delete').getAttribute('id')) ?? 'missing');
  });
});

// MenuFilterRoot.scoping.test.tsx's "prop: onItemHighlighted" (MNU-EVT-*, STA-20; FilterDropdownRoot.tsx:109-125) on
// `menu-filter-root` with `insert=true`, titles verbatim: an item set change that moves another item under the highlighted
// index clears the highlight instead of reporting that item.
test.describe('prop: onItemHighlighted', () => {
  test('does not report the item that takes the highlighted index when an item is inserted', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-root?insert=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('menu-filter-root'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await input(page).focus();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await expect(fixture).toHaveAttribute('data-highlights', 'Rename|Delete');

    // The modal menu's backdrop covers the page, so the fixture's control is pressed programmatically, as Base UI's setProps
    // re-renders without moving focus.
    await page.getByRole('button', { name: 'Insert item' }).evaluate((element) => (element as HTMLButtonElement).click());

    await expect(item(page, 'Archive')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-highlights', 'Rename|Delete|none');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
  });
});
