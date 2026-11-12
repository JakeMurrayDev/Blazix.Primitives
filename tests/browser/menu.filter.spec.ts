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
  async function open(page: Page, mode: RenderMode, hover = false, plain = false): Promise<Locator> {
    const query = [hover ? 'hover=true' : '', plain ? 'plain=true' : ''].filter((part) => part !== '').join('&');
    await page.goto(`/${mode}/menu-filter-initial${query === '' ? '' : `?${query}`}`, { waitUntil: 'commit' });
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

  // MNU-STA-22, FOC-01 (MenuFilterRoot.test.tsx). Base UI's menu holds Rename alone; the fixture's `plain` list holds
  // Rename and Delete.
  test('highlights the first item while keeping input focus when opened with the keyboard', async ({ page, mode }) => {
    await open(page, mode, false, true);
    await trigger(page).focus();

    await page.keyboard.press('Enter');

    await expect(input(page)).toBeFocused();
    await expect(input(page)).not.toHaveAttribute('data-highlighted');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
  });

  // MNU-STA-18 (MenuFilterRoot.test.tsx's KeyboardNavigationMenu: Rename, Duplicate, Delete). On the fixture's `plain`
  // list (Rename, Delete) the arrow moves to Delete, and "e" matches both.
  test('resets the highlight when the query changes and rebuilds it from the first match', async ({ page, mode }) => {
    await open(page, mode, false, true);
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');

    await page.keyboard.type('e');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
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

  // iOS VoiceOver presses with a touch whose contact is under a pixel and carries no pressure; Base UI's firePointer and
  // fireEvent calls are dispatched in the page.
  async function dispatchPress(locator: Locator, init: { pointerType: string; width?: number; height?: number; pressure?: number }): Promise<void> {
    await locator.evaluate((element, { pointerType, width = 1, height = 1, pressure = 0.5 }) => {
      const pointer = { bubbles: true, cancelable: true, isPrimary: true, pointerType, width, height, pressure };
      element.dispatchEvent(new PointerEvent('pointerdown', pointer));
      element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, detail: 1 }));
      element.dispatchEvent(new PointerEvent('pointerup', pointer));
      element.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, detail: 1 }));
      element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, detail: 1 }));
    }, init);
  }

  // MNU-API-59, EVT-14, EVT-15 (MenuFilterRoot.test.tsx): Base UI's fireEvent.keyDown is a dispatched keydown.
  // Base UI's title then presses Escape, which must reach the popup's handler once. The port's dismissal stops Escape on
  // the floating element before Blazor dispatches it, so no C# popup handler hears it (open by ID, reported 2026-11-12);
  // this port title keeps the title's other steps.
  test('port: runs a consumer handler once per interaction on the trigger and the input, and keeps an editing key from the popup', async ({ page, mode }) => {
    const fixture = page.locator(Fixture.interactive('menu-filter-initial'));
    await page.goto(`/${mode}/menu-filter-initial?count=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(fixture).toHaveAttribute('data-attached', 'true');

    await trigger(page).click();
    await expect(fixture).toHaveAttribute('data-trigger-clicks', '1');
    await expect(input(page)).toBeFocused();

    await input(page).evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { key: 'r', bubbles: true, cancelable: true })));
    // A printable key stays on the input: it must not reach the popup's handler.
    await expect(fixture).toHaveAttribute('data-input-keydowns', '1');
    await expect(fixture).toHaveAttribute('data-popup-keydowns', '0');

  });

  // MNU-STA-01, EVT-02 (MenuFilterRoot.test.tsx).
  test('stays closed when onOpenChange is canceled', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-initial?cancelOpen=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-attached', 'true');

    await trigger(page).click();
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

    await expect(page.getByRole('menu')).toHaveCount(0);
  });

  // MNU-KEY-23 (MenuFilterRoot.test.tsx's KeyboardNavigationMenu: Rename, Duplicate, Delete). On the fixture's `plain` list
  // (Rename, Delete) one ArrowDown reaches the last item where Base UI's tree takes two.
  test('walks the list with the arrow keys and releases the highlight at the end', async ({ page, mode }) => {
    await open(page, mode, false, true);
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(input(page)).toBeFocused();
    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');

    // Past the last item the highlight is released back to the input, matching Combobox.
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
  });

  // MNU-EDGE-14 (MenuFilterRoot.test.tsx's KeyboardNavigationMenu), on the fixture's `plain` list (Rename, Delete).
  test('starts navigation from the top again after closing and reopening', async ({ page, mode }) => {
    await open(page, mode, false, true);
    await trigger(page).focus();
    await page.keyboard.press('Enter');
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Delete')).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('Escape');
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(trigger(page)).toBeFocused();
    await page.keyboard.press('Enter');

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
    await expect(item(page, 'Delete')).not.toHaveAttribute('data-highlighted');
  });

  // MNU-STA-21, EVT-18 (MenuFilterRoot.test.tsx): a kept-mounted popup, on the fixture's `plain` list (Rename, Delete).
  test('resets the query and highlight across a close and reopen', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-initial?plain=true&keepMounted=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-attached', 'true');
    await trigger(page).click();
    await expect(input(page)).toBeFocused();
    await page.keyboard.type('ren');
    await expect(item(page, 'Delete')).toHaveCount(0);
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));

    await page.keyboard.press('Escape');
    await trigger(page).click();

    // The parts stay mounted, so the query has to be cleared explicitly on close.
    await expect(input(page)).toHaveValue('');
    await expect(item(page, 'Delete')).toBeVisible();
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
  });

  // MNU-PTR-19.
  test('focuses the input when a screen reader press opens the menu', async ({ page, mode }) => {
    await open(page, mode);
    await trigger(page).focus();

    await dispatchPress(trigger(page), { pointerType: 'touch', width: 0.333, height: 0.333, pressure: 0 });

    await expect(input(page)).toBeFocused();
  });

  // MNU-PTR-19. The fixture records no input focus handler; the input not holding focus after the tap is the check.
  test('does not focus the input when an item is tapped after a touch open', async ({ page, mode }) => {
    await open(page, mode);

    await dispatchPress(trigger(page), { pointerType: 'touch' });
    const rename = item(page, 'Rename');
    await expect(rename).toBeVisible();
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(resolve)));
    // A tap fires compatibility mouse events, including a mousemove, before its mousedown.
    await rename.evaluate((element) => {
      element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, isPrimary: true, pointerType: 'touch' }));
      element.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, cancelable: true, isPrimary: true, pointerType: 'touch' }));
      element.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
      element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true }));
    });
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(resolve)));

    await expect(input(page)).not.toBeFocused();
  });

  test('does not focus the input when opened on hover', async ({ page, mode }) => {
    await open(page, mode, true);

    await trigger(page).hover();

    await expect(input(page)).toBeVisible();
    await expect(input(page)).not.toBeFocused();
  });

  // MNU-PTR-19, STA-22 (FilterDropdownRoot.tsx:49-58): a pointer open focuses the input, which shows its focus ring.
  test('marks the input focus-visible when the menu is opened with a pointer', async ({ page, mode }) => {
    await open(page, mode);

    await trigger(page).click();

    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('data-highlighted', '');
  });

  // MNU-PTR-19, FOC-* (MenuFilterRoot.scoping.test.tsx): Menu's focus manager is modal only under a context menu, so an
  // open by hover or by an assistive technology's click hides nothing outside the popup.
  async function hiddenFromAssistiveTechnology(locator: Locator): Promise<boolean> {
    return locator.evaluate((element) => element.closest('[aria-hidden="true"], [inert]') !== null);
  }

  test('does not trap a hover-opened popup', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-initial?hover=true&after=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-attached', 'true');

    await trigger(page).hover();

    await expect(page.getByTestId('popup')).toBeVisible();
    expect(await hiddenFromAssistiveTechnology(page.getByTestId('after'))).toBe(false);
  });

  // An assistive technology activation reports no pointer type; the events are dispatched as Base UI's fireEvent does.
  test('does not trap a popup opened by an assistive technology click', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-initial?after=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-attached', 'true');

    await trigger(page).evaluate((element) => {
      element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerType: '' }));
      element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
      element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, detail: 1 }));
    });

    await expect(page.getByTestId('popup')).toBeVisible();
    expect(await hiddenFromAssistiveTechnology(page.getByTestId('after'))).toBe(false);
  });

  // Base UI's tree puts the input "after" before the trigger; the fixture's follows the menu.
  test('does not trap a menu opened by touch', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-initial?after=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-attached', 'true');

    await trigger(page).evaluate((element) => {
      element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerType: 'touch' }));
      element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    });

    await expect(page.getByTestId('popup')).toBeVisible();
    expect(await hiddenFromAssistiveTechnology(page.getByTestId('after'))).toBe(false);
  });

  // MNU-FOC-16 (MenuFilterRoot.test.tsx): Safari focuses the trigger on mousedown before the closing click lands.
  test('does not seed a highlight when the trigger regains focus while open', async ({ page, mode }) => {
    await open(page, mode);
    await trigger(page).click();
    await expect(input(page)).toBeFocused();

    await trigger(page).focus();

    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(trigger(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(item(page, 'Rename')).not.toHaveAttribute('data-highlighted');
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

  // MNU-API-55, PTR-13: Base UI's unhover is the mouse moving off the item.
  test('clears a pointer highlight on pointer leave by default', async ({ page, mode }) => {
    await render(page, mode);
    const rename = item(page, 'Rename');
    await rename.hover();
    await expect(rename).toHaveAttribute('data-highlighted', '');

    await page.mouse.move(0, 0);

    // The mirror of the autoHighlight="always" case, which retains the highlight.
    await expect(rename).not.toHaveAttribute('data-highlighted');
  });

  // MNU-API-55, PTR-13.
  test('preserves a pointer highlight when autoHighlight is "always"', async ({ page, mode }) => {
    await renderAuto(page, mode, 'always');
    const deleteItem = item(page, 'Delete');
    await deleteItem.hover();
    await expect(deleteItem).toHaveAttribute('data-highlighted', '');

    await page.mouse.move(0, 0);
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

    await expect(deleteItem).toHaveAttribute('data-highlighted', '');
  });

  // MNU-STA-18.
  test('clears the highlight when no items match', async ({ page, mode }) => {
    await renderAuto(page, mode, 'true');
    await input(page).focus();

    await page.keyboard.type('missing');

    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(page.getByRole('menuitem')).toHaveCount(0);
  });

  test('always highlights the first item when autoHighlight is "always"', async ({ page, mode }) => {
    await renderAuto(page, mode, 'always');

    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
  });

  // MNU-KEY-22, STA-19: the fixture's "el" leaves only Delete, which moves to the top away from the resting pointer. The
  // typed query's automatic highlight is awaited before the hover (ADR 01: a C#-origin highlight lands one leg later on
  // Server).
  test('keeps the automatic highlight when typing moves it out from under the pointer', async ({ page, mode }) => {
    await renderAuto(page, mode, 'true');
    await input(page).focus();
    await page.keyboard.type('e');
    await expect(item(page, 'Rename')).toHaveAttribute('data-highlighted', '');
    const deleteItem = item(page, 'Delete');
    await deleteItem.hover();
    const deleteId = (await deleteItem.getAttribute('id')) ?? 'missing';
    await expect(input(page)).toHaveAttribute('aria-activedescendant', deleteId);

    await page.keyboard.type('l');
    await expect(item(page, 'Rename')).toHaveCount(0);
    await deleteItem.evaluate((element) => element.dispatchEvent(new PointerEvent('pointerleave', { pointerType: 'mouse' })));

    await expect(input(page)).toHaveAttribute('aria-activedescendant', deleteId);
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
  // The popup's initial focus can land on the input just after the fixture reports attached; a title that moves focus
  // waits for it, or the initial focus takes focus back from the list it moved to.
  async function renderOpen(page: Page, mode: RenderMode): Promise<Locator> {
    const fixture = await render(page, mode);
    await expect(input(page)).toBeFocused();
    return fixture;
  }

  function list(page: Page): Locator {
    return page.getByRole('menu');
  }

  test('returns focus to the input when the list takes it from the input', async ({ page, mode }) => {
    await renderOpen(page, mode);
    await input(page).focus();

    await list(page).focus();

    await expect(input(page)).toBeFocused();
  });

  test('leaves focus on the list when the input did not have it', async ({ page, mode }) => {
    await renderOpen(page, mode);
    await input(page).focus();
    await input(page).blur();

    await list(page).focus();

    await expect(list(page)).toBeFocused();
  });

  // MNU-FOC-14 (MenuList.test.tsx): a screen reader parks real focus on the item, then the user moves back to the input.
  test('clears the highlight when focus returns to the input from an item', async ({ page, mode }) => {
    await renderOpen(page, mode);
    await page.keyboard.press('ArrowDown');
    const rename = item(page, 'Rename');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await rename.getAttribute('id')) ?? 'missing');

    await rename.focus();
    await input(page).focus();

    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(rename).not.toHaveAttribute('data-highlighted');
  });

  // MNU-FOC-14 (MenuList.test.tsx).
  test('keeps the highlight when focus returns to the input with autoHighlight="always"', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-root?auto=always`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-root'))).toHaveAttribute('data-attached', 'true');
    const rename = item(page, 'Rename');
    const renameId = (await rename.getAttribute('id')) ?? 'missing';
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);

    await rename.focus();
    await input(page).focus();

    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
  });

  // MNU-KEY-27 (MenuList.test.tsx, titles verbatim): a screen reader following aria-activedescendant focuses the item.
  test('replays keys from an item that holds real focus and activates on Enter', async ({ page, mode }) => {
    const fixture = await renderOpen(page, mode);
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
      await renderOpen(page, mode);
      await input(page).focus();
      await focusList(page);

      const scrolls = await keyDown(page, 'ArrowDown');

      expect(scrolls).toBe(false);
      await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');
      await expect(input(page)).toBeFocused();
    });

    test('commits the highlighted item when Enter lands on the list', async ({ page, mode }) => {
      const fixture = await renderOpen(page, mode);
      await input(page).focus();
      await page.keyboard.press('ArrowDown');
      await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');

      await focusList(page);
      await keyDown(page, 'Enter');

      await expect(fixture).toHaveAttribute('data-presses', 'Rename');
    });

    test('still closes the menu with Escape', async ({ page, mode }) => {
      await renderOpen(page, mode);
      await input(page).focus();
      await focusList(page);

      await keyDown(page, 'Escape');

      await expect(page.getByRole('dialog')).toHaveCount(0);
    });

    test('hands typing keys back to the input', async ({ page, mode }) => {
      await renderOpen(page, mode);
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

  // MNU-KEY-33 (MenuFilterRoot.test.tsx): Base UI's tree has an "Auxiliary action" button where the fixture's reads "Extra
  // action".
  test('returns focus to the input with the RTL submenu close key', async ({ page, mode }) => {
    await renderWith(page, mode, 'extra=true&dir=rtl');
    await extra(page).focus();

    await extra(page).evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true })));

    await expect(input(page)).toBeFocused();
  });

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

    // The open renders the items a round trip later on Server; read the id once the input is there.
    await expect(input(page)).toBeVisible();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));
  });
});

// MenuFilterRoot.scoping.test.tsx's "trigger key relay" (MNU-KEY-28; MenuFilterDropdown.tsx:124-150; M5 track 12) on
// `menu-filter-initial` with `hover=true&plain=true`, titles verbatim: a hover open leaves focus on the trigger, and the
// trigger relays the vertical arrows and typed characters to the input.
test.describe('trigger key relay', () => {
  async function openByHover(page: Page, mode: RenderMode, extra = ''): Promise<Locator> {
    await page.goto(`/${mode}/menu-filter-initial?hover=true&plain=true${extra}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-attached', 'true');
    const trigger = page.getByRole('button', { name: 'Actions' });
    await trigger.focus();
    await trigger.hover();
    await expect(input(page)).toBeVisible();
    // The trigger relays only to an attached input, which on Server attaches a leg after the popup shows.
    await expect(page.locator(Fixture.interactive('menu-filter-initial'))).toHaveAttribute('data-input-attached', 'true');
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

  // Design point 6 (a): the consumer's onKeyDown vetoes through the `navigate` decision point, asked once per keydown.
  // ADR 01's row "C#-origin consumer vetoes": synchronous on WebAssembly, a round trip late on Server (fallback "run").
  test('lets a consumer onKeyDown cancel the relay', async ({ page, mode }) => {
    test.skip(mode === 'server', "ADR 01: a C# consumer's veto is a round trip late on Server; the port title below asserts it");
    const trigger = await openByHover(page, mode, '&cancelArrow=true');

    await page.keyboard.press('ArrowDown');

    await expect(trigger).toBeFocused();
  });

  test("port: on Server the trigger relays the key before the consumer's C# onKeyDown can cancel it", async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Server only: the circuit between the browser and C#');
    await openByHover(page, mode, '&cancelArrow=true');

    await page.keyboard.press('ArrowDown');

    await expect(input(page)).toBeFocused();
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

  // MNU-KEY-29, KEY-31 (MenuFilterSubmenuRoot.tsx:60-90, 230-270). The fixture's trigger is the list's fourth item and its
  // last, so the parent's next item after the trigger is reached with ArrowUp (Duplicate) where Base UI's tree has Delete
  // after it.
  test('opens a virtually focused submenu with the keyboard', async ({ page, mode }) => {
    await renderNested(page, mode);
    await input(page).focus();
    for (let i = 0; i < 4; i += 1) {
      await page.keyboard.press('ArrowDown');
    }
    const triggerId = (await submenuTrigger(page).getAttribute('id')) ?? 'missing';
    await expect(input(page)).toHaveAttribute('aria-activedescendant', triggerId);

    await page.keyboard.press('ArrowRight');

    await expect(submenuInput(page)).toBeFocused();
    await expect(submenuInput(page)).not.toHaveAttribute('data-highlighted');
    await expect(submenuInput(page)).toHaveAttribute('aria-activedescendant', (await page.getByRole('menuitem', { name: 'Documents' }).getAttribute('id')) ?? 'missing');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);

    await page.keyboard.press('ArrowLeft');
    await expect(input(page)).toBeFocused();
    await expect(input(page)).toHaveAttribute('aria-activedescendant', triggerId);

    await page.keyboard.press('ArrowUp');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Duplicate').getAttribute('id')) ?? 'missing');
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

    const dispatch = (init: { key: string; shiftKey?: boolean; altKey?: boolean; metaKey?: boolean }): Promise<boolean> =>
      input(page).evaluate((element, options) => element.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...options })), init);

    expect(await dispatch({ key: 'ArrowDown', shiftKey: true })).toBe(true);
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
    expect(await dispatch({ key: 'ArrowLeft', shiftKey: true })).toBe(true);
    expect(await dispatch({ key: 'ArrowRight', altKey: true })).toBe(true);
    expect(await dispatch({ key: 'Home', metaKey: true })).toBe(true);
    expect(await dispatch({ key: 'End', shiftKey: true })).toBe(true);
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
  });

  // MNU-EVT-19, STA-21 (FilterDropdownInput.tsx:84-103): composition text is not committed until the composition ends.
  // Base UI composes 「にほ」 then commits 「日本」 over a 日本語 item; here "zz" (which would empty the list) then "del". The events
  // are dispatched as Base UI's fireEvent does.
  test('filters on the committed text, not the composition in progress', async ({ page, mode }) => {
    const fixture = await renderWith(page, mode);
    const compose = (value: string, end: boolean): Promise<void> =>
      input(page).evaluate((element, args) => {
        const field = element as HTMLInputElement;
        if (!args.end) {
          field.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
        }
        field.value = args.value;
        field.dispatchEvent(new Event('input', { bubbles: true }));
        if (args.end) {
          field.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true, data: args.value }));
        }
      }, { value, end });

    await compose('zz', false);
    await expect(input(page)).toHaveValue('zz');
    await expect(fixture).toHaveAttribute('data-value-changes', '');
    await expect(item(page, 'Rename')).toBeVisible();
    await expect(page.getByText('No results')).toHaveCount(0);

    await compose('del', true);

    await expect(fixture).toHaveAttribute('data-value-changes', 'del:input-change');
    await expect(item(page, 'Rename')).toHaveCount(0);
    await expect(item(page, 'Delete')).toBeVisible();
  });

  // MNU-KEY-25, EVT-19 (useMenuFilterKeyDown.ts:58-66): a keydown of an IME composition (keyCode 229) is the input's; the
  // keydown is dispatched as Base UI's fireEvent does.
  test('does not activate the highlighted item when Enter commits an IME composition', async ({ page, mode }) => {
    const fixture = await renderWith(page, mode);
    await expect(input(page)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await item(page, 'Rename').getAttribute('id')) ?? 'missing');

    await input(page).evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', keyCode: 229, which: 229, bubbles: true, cancelable: true } as KeyboardEventInit)));

    await expect(page.getByRole('menu')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-presses', '');
  });

  // MNU-KEY-25 (useMenuFilterKeyDown.ts:173-176): the fixture's link item has no href, so the click stays on the page.
  test('preserves modifiers when the input activates a link item', async ({ page, mode }) => {
    const fixture = await renderWith(page, mode, '?link=true');
    await expect(input(page)).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await page.getByRole('menuitem', { name: 'Docs' }).getAttribute('id')) ?? 'missing');
    await page.keyboard.press('Control+Enter');

    await expect(fixture).toHaveAttribute('data-presses', 'Docs:ctrl');
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

// MenuFilterRoot.test.tsx's consumer key handler on the input (MNU-API-59, EVT-15; design point 6 (a)) on
// `menu-filter-root` with `veto=true`. Base UI's fireEvent.keyDown is a dispatched keydown. ADR 01's row "C#-origin
// consumer vetoes": the veto holds on WebAssembly; on Server the handler runs a round trip after the key, asserted by a
// port title.
test.describe('a consumer key handler on the input', () => {
  async function renderVeto(page: Page, mode: RenderMode): Promise<Locator> {
    await page.goto(`/${mode}/menu-filter-root?veto=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    const fixture = page.locator(Fixture.interactive('menu-filter-root'));
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await expect(input(page)).toBeFocused();
    return fixture;
  }

  function keyDown(page: Page, key: string): Promise<boolean> {
    return input(page).evaluate((element, pressed) => element.dispatchEvent(new KeyboardEvent('keydown', { key: pressed, bubbles: true, cancelable: true })), key);
  }

  test('lets a consumer key handler on the input opt out of list navigation and activation', async ({ page, mode }) => {
    test.skip(mode === 'server', "ADR 01: a C# consumer's veto is a round trip late on Server; the port title below asserts it");
    const fixture = await renderVeto(page, mode);
    const renameId = (await item(page, 'Rename').getAttribute('id')) ?? 'missing';

    await keyDown(page, 'ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
    await keyDown(page, 'ArrowUp');
    await keyDown(page, 'Enter');

    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
    await expect(fixture).not.toHaveAttribute('data-presses', /Rename/);
  });

  test("port: on Server the input's keys run before the consumer's C# onKeyDown can opt out", async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Server only: the circuit between the browser and C#');
    await renderVeto(page, mode);
    const renameId = (await item(page, 'Rename').getAttribute('id')) ?? 'missing';

    await keyDown(page, 'ArrowDown');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
    await keyDown(page, 'ArrowUp');

    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', renameId);
  });
});

// MenuFilterRoot.scoping.test.tsx (MNU-DOM-22, KEY-10) on `menu-filter-root` with a query and the "After" input; the
// row tags it [L2], and Tab is the browser's (ADR 13 §3 re-tag note). Base UI's Clear reads "Clear query"; the fixture's
// reads "Clear filter".
test.describe('tabbing out of a filterable popup', () => {
  test('skips Clear when tabbing out of the popup', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-root?query=re&after=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-root'))).toHaveAttribute('data-attached', 'true');
    await expect(input(page)).toBeFocused();
    await expect(clear(page)).toHaveAttribute('tabindex', '-1');

    await page.keyboard.press('Tab');

    await expect(page.getByTestId('after')).toBeFocused();
    await expect(page.getByRole('menu')).toHaveCount(0);
  });

  // MNU-FOC-10, KEY-10 (MenuFilterRoot.scoping.test.tsx's FocusMenu with modal={false}).
  test('lets Tab leave the popup with modal={false}', async ({ page, mode }) => {
    await page.goto(`/${mode}/menu-filter-root?modal=false&after=true`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-root'))).toHaveAttribute('data-attached', 'true');
    await expect(input(page)).toBeFocused();
    expect(await page.getByTestId('after').evaluate((element) => element.closest('[aria-hidden="true"], [inert]') !== null)).toBe(false);

    await page.keyboard.press('Tab');

    await expect(page.getByTestId('after')).toBeFocused();
  });
});

// MenuFilterRoot.test.tsx's caret-key titles (MNU-KEY-23, KEY-30) on `menu-filter-root` (Rename, Delete, Duplicate),
// titles verbatim. Base UI's trees hold Rename and Remove; the fixture's queries "ren" and "re" each match Rename alone.
test.describe('the input keeps its caret keys', () => {
  async function renderWith(page: Page, mode: RenderMode, query: string): Promise<void> {
    await page.goto(`/${mode}/menu-filter-root${query === '' ? '' : `?${query}`}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await expect(page.locator(Fixture.interactive('menu-filter-root'))).toHaveAttribute('data-attached', 'true');
  }

  const caret = (page: Page): Promise<number | null> => input(page).evaluate((element: HTMLInputElement) => element.selectionStart);

  test('keeps Home and End as caret keys until an item is highlighted', async ({ page, mode }) => {
    await renderWith(page, mode, 'query=rename');
    await input(page).click();
    await input(page).evaluate((element: HTMLInputElement) => element.setSelectionRange(3, 3));

    await page.keyboard.press('Home');
    expect(await caret(page)).toBe(0);
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);

    await page.keyboard.press('End');
    expect(await caret(page)).toBe('rename'.length);
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
  });

  test('keeps Home and End as caret keys while the input is empty', async ({ page, mode }) => {
    await renderWith(page, mode, '');
    await input(page).click();

    await page.keyboard.press('End');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await page.keyboard.press('Home');
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
  });

  test('keeps the caret keys while a query is highlighted automatically', async ({ page, mode }) => {
    await renderWith(page, mode, 'auto=true');
    await input(page).click();
    await page.keyboard.type('ren');
    const renameId = (await item(page, 'Rename').getAttribute('id')) ?? 'missing';
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);

    await page.keyboard.press('ArrowLeft');
    expect(await caret(page)).toBe(2);
    await page.keyboard.press('Home');
    expect(await caret(page)).toBe(0);
    await page.keyboard.press('ArrowRight');
    expect(await caret(page)).toBe(1);
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
  });

  test('keeps the caret keys after navigating to an item while the query is not empty', async ({ page, mode }) => {
    await renderWith(page, mode, '');
    await input(page).click();
    await page.keyboard.type('re');
    // On Server the query's highlight reset lands a leg later (ADR 01); the arrow waits for the filtered list.
    await expect(item(page, 'Delete')).toHaveCount(0);
    await page.keyboard.press('ArrowDown');
    const renameId = (await item(page, 'Rename').getAttribute('id')) ?? 'missing';
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);

    await page.keyboard.press('ArrowLeft');
    expect(await caret(page)).toBe(1);
    await page.keyboard.press('End');
    expect(await caret(page)).toBe(2);
    await expect(input(page)).toHaveAttribute('aria-activedescendant', renameId);
  });
});
