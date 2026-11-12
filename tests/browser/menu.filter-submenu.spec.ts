import { type Locator } from '@playwright/test';
import { expect, Fixture, test, type Page, type RenderMode } from './helpers';

// MenuFilterSubmenuRoot.test.tsx and the filterable-submenu titles of MenuFilterRoot.test.tsx (M5 track 12, issue 197;
// docs/components/menu.md), on `menu-filter-submenu` (its parameters are in the fixture's header), titles verbatim unless
// marked "port:", every title on both render modes. Base UI's `fireEvent.keyDown` is a dispatched keydown, read back for
// `defaultPrevented`; `user.keyboard` is Playwright's.

type Query = Record<string, string | number | boolean>;

async function render(page: Page, mode: RenderMode, query: Query = {}): Promise<Locator> {
  const search = new URLSearchParams(Object.entries(query).map(([key, value]) => [key, String(value)])).toString();
  await page.goto(`/${mode}/menu-filter-submenu${search === '' ? '' : `?${search}`}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('menu-filter-submenu'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

const plain = { parent: 'plain' } as const;

function trigger(page: Page): Locator {
  return page.getByTestId('submenu-trigger');
}

function submenuInput(page: Page): Locator {
  return page.getByRole('searchbox', { name: 'Filter folders' });
}

function item(page: Page, name: string): Locator {
  return page.getByRole('menuitem', { name, exact: true });
}

function dispatchKey(locator: Locator, init: { key: string; keyCode?: number }): Promise<boolean> {
  return locator.evaluate((element, options) => element.dispatchEvent(new KeyboardEvent('keydown', { ...options, bubbles: true, cancelable: true })), init);
}

test.describe('<Menu.FilterProvider><Menu.SubmenuRoot/></Menu.FilterProvider>', () => {
  // MenuFilterSubmenuRoot.test.tsx's PlainParentMenu: a filterable submenu whose parent is a plain, roving-focus menu.
  test.describe('inside a plain parent menu', () => {
    // MNU-KEY-33.
    test('closes with Escape and returns focus to the highlighted trigger', async ({ page, mode }) => {
      await render(page, mode, plain);
      await trigger(page).focus();
      await page.keyboard.press('ArrowRight');
      await expect(submenuInput(page)).toBeFocused();

      await page.keyboard.press('Escape');

      await expect(page.getByTestId('submenu-list')).toHaveCount(0);
      await expect(trigger(page)).toBeFocused();
      await expect(trigger(page)).toHaveAttribute('data-highlighted', '');
      await expect(item(page, 'Rename')).toHaveCount(1);
    });

    // MNU-FOC-15.
    test('opens with the cross-axis key and focuses the submenu input', async ({ page, mode }) => {
      await render(page, mode, plain);
      await trigger(page).focus();

      await page.keyboard.press('ArrowRight');

      await expect(submenuInput(page)).toBeFocused();
    });

    // MNU-KEY-33.
    test('closes with the cross-axis close key and returns focus to the trigger', async ({ page, mode }) => {
      await render(page, mode, plain);
      await trigger(page).focus();
      await page.keyboard.press('ArrowRight');
      await expect(submenuInput(page)).toBeFocused();

      await page.keyboard.press('ArrowLeft');

      await expect(page.getByTestId('submenu-list')).toHaveCount(0);
      await expect(trigger(page)).toBeFocused();
    });

    // MNU-KEY-15, KEY-29.
    test('hands the cursor back to the submenu input when the open key repeats', async ({ page, mode }) => {
      await render(page, mode, plain);
      await trigger(page).focus();
      await page.keyboard.press('ArrowRight');
      await expect(submenuInput(page)).toBeFocused();

      // Real focus back on the trigger, then the open key again while the submenu is open: it re-enters, not reopens.
      await trigger(page).focus();
      await page.keyboard.press('ArrowRight');

      await expect(submenuInput(page)).toBeFocused();
      await expect(page.getByTestId('submenu-list')).toHaveCount(1);
    });

    // MNU-FOC-15.
    test('keeps the automatic highlight when the keyboard enters a hover-opened submenu', async ({ page, mode }) => {
      await render(page, mode, { ...plain, auto: 'always', delay: 0 });

      await trigger(page).hover();
      const projectsId = (await item(page, 'Projects').getAttribute('id')) ?? 'missing';
      await expect(submenuInput(page)).toHaveAttribute('aria-activedescendant', projectsId);
      await expect(trigger(page)).toBeFocused();

      await page.keyboard.press('ArrowRight');

      await expect(submenuInput(page)).toBeFocused();
      await expect(submenuInput(page)).toHaveAttribute('aria-activedescendant', projectsId);
    });

    // MNU-KEY-18.
    test('moves along the parent list with the main-axis keys', async ({ page, mode }) => {
      await render(page, mode, plain);
      await trigger(page).focus();

      await page.keyboard.press('ArrowDown');

      await expect(item(page, 'Delete')).toBeFocused();
    });

    // MNU-KEY-29, KEY-33.
    test('closes when the close key arrives while the trigger holds focus', async ({ page, mode }) => {
      await render(page, mode, plain);
      await trigger(page).focus();
      await page.keyboard.press('ArrowRight');
      await expect(submenuInput(page)).toBeVisible();

      // Real focus back on the trigger, then the close key there rather than in the submenu popup.
      await trigger(page).focus();
      await page.keyboard.press('ArrowLeft');

      await expect(page.getByTestId('submenu-list')).toHaveCount(0);
    });

    // MNU-PTR-22.
    test('keeps focus in the submenu input when the pointer crosses the trigger again', async ({ page, mode }) => {
      await render(page, mode, { ...plain, delay: 0 });
      await trigger(page).hover();
      await expect(submenuInput(page)).toBeVisible();

      // The pointer enters the submenu and typing starts.
      await submenuInput(page).evaluate((element) => element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true })));
      await expect(submenuInput(page)).toBeFocused();
      await page.keyboard.type('ar');
      await expect(submenuInput(page)).toHaveValue('ar');

      // Crossing the trigger must not interrupt typing in the submenu.
      await trigger(page).evaluate((element) => element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true })));
      await expect(submenuInput(page)).toBeFocused();
      await page.keyboard.type('c');

      await expect(submenuInput(page)).toHaveValue('arc');
      await expect(item(page, 'Archive')).toBeVisible();
    });

    // MNU-KEY-18.
    test('leaves a key that is neither an axis nor an activation key to the parent', async ({ page, mode }) => {
      await render(page, mode, plain);
      await trigger(page).focus();

      await page.keyboard.press('Home');

      // The submenu must not open, and the parent's own Home handling still runs.
      await expect(item(page, 'Rename')).toBeFocused();
      await expect(page.getByTestId('submenu-list')).toHaveCount(0);
    });
  });
});

test.describe('filtered submenu trigger navigation', () => {
  // MNU-KEY-32: the filterable submenu "More" first in a plain parent, then Next.
  test('prevents native scrolling when moving along the parent menu', async ({ page, mode }) => {
    await render(page, mode, { ...plain, before: '', label: 'More', after: 'Next', children: 'Child', hover: false });
    const more = item(page, 'More');
    await more.focus();

    expect(await dispatchKey(more, { key: 'ArrowDown' })).toBe(false);

    await expect(item(page, 'Next')).toBeFocused();
  });
});

test.describe('closing a filtered submenu from the keyboard', () => {
  // MNU-KEY-31.
  test('ignores a close key that composes text in the submenu input', async ({ page, mode }) => {
    await render(page, mode, { ...plain, before: '', label: 'More', after: '', children: 'Child', hover: false });
    await item(page, 'More').focus();
    await page.keyboard.press('ArrowRight');
    await expect(submenuInput(page)).toBeFocused();

    await dispatchKey(submenuInput(page), { key: 'ArrowLeft', keyCode: 229 });

    // The popup can stay mounted through an exit animation, so check the open state itself.
    await expect(item(page, 'More')).toHaveAttribute('data-popup-open', '');
    await expect(submenuInput(page)).toBeFocused();
  });

  const cases = [
    { parentFilterable: false, submenuFilterable: true },
    { parentFilterable: true, submenuFilterable: false },
    { parentFilterable: true, submenuFilterable: true },
  ];
  for (const { parentFilterable, submenuFilterable } of cases) {
    // MNU-KEY-16, KEY-32.
    test(`moves a horizontal parent on when a vertical submenu closes on its main-axis key (filterable parent: ${parentFilterable}, filterable submenu: ${submenuFilterable})`, async ({ page, mode }) => {
      await render(page, mode, {
        parent: parentFilterable ? 'filter' : 'plain',
        child: submenuFilterable ? 'filter' : 'plain',
        closed: true,
        orientation: 'horizontal',
        before: 'First',
        label: 'More',
        after: 'Last',
        children: 'Child',
      });

      await page.getByRole('button', { name: 'Actions' }).focus();
      await page.keyboard.press('Enter');
      const initialFocus = parentFilterable ? page.getByRole('searchbox', { name: 'Filter actions' }) : item(page, 'First');
      await expect(initialFocus).toBeFocused();
      await page.keyboard.press('ArrowRight');
      const more = item(page, 'More');
      await expect(more).toHaveAttribute('data-highlighted', '');

      await page.keyboard.press('ArrowDown');
      await expect(page.getByTestId('submenu')).toBeVisible();
      // Base UI's submenu takes focus in the commit that mounts it; the port's focus follows the mount, so the close key
      // waits for it.
      await expect(submenuFilterable ? submenuInput(page) : item(page, 'Child')).toBeFocused();
      await page.keyboard.press('ArrowLeft');

      await expect(page.getByTestId('submenu')).toHaveCount(0);
      await expect(item(page, 'First')).toHaveAttribute('data-highlighted', '');
      await expect(more).not.toHaveAttribute('data-highlighted');
    });
  }

  for (const submenuFilterable of [false, true]) {
    // MNU-KEY-32.
    test(`does not wrap a filterable parent when closing from its first item (filterable submenu: ${submenuFilterable})`, async ({ page, mode }) => {
      await render(page, mode, {
        child: submenuFilterable ? 'filter' : 'plain',
        closed: true,
        orientation: 'horizontal',
        before: '',
        label: 'More',
        after: 'Middle,Last',
        children: 'Child',
      });

      await page.getByRole('button', { name: 'Actions' }).focus();
      await page.keyboard.press('Enter');
      const input = page.getByRole('searchbox', { name: 'Filter actions' });
      const more = item(page, 'More');
      await expect(input).toHaveAttribute('aria-activedescendant', (await more.getAttribute('id')) ?? 'missing');

      await page.keyboard.press('ArrowDown');
      await expect(page.getByTestId('submenu')).toBeVisible();
      await expect(submenuFilterable ? submenuInput(page) : item(page, 'Child')).toBeFocused();
      await page.keyboard.press('ArrowLeft');

      await expect(page.getByTestId('submenu')).toHaveCount(0);
      await expect(input).toBeFocused();
      await expect(input).toHaveAttribute('aria-activedescendant', (await more.getAttribute('id')) ?? 'missing');
      await expect(item(page, 'Last')).not.toHaveAttribute('data-highlighted');
    });
  }
});

// MenuFilterRoot.test.tsx's filterable submenus under a filterable parent. Base UI's tree names its submenu input after
// the submenu ("Filter sharing options"); the fixture's reads "Filter folders" (and "Filter second" for a second one).
// Base UI's open menu focuses its input on mount; the port focuses it first.
test.describe('<Menu.FilterProvider><Menu.Root/></Menu.FilterProvider> with a filterable submenu', () => {
  const parentInput = (page: Page): Locator => page.getByRole('searchbox', { name: 'Filter actions' });

  function mouseMove(locator: Locator): Promise<boolean> {
    return locator.evaluate((element) => element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true })));
  }

  async function idOf(locator: Locator): Promise<string> {
    return (await locator.getAttribute('id')) ?? 'missing';
  }

  // MNU-KEY-29.
  test('uses RTL cross-axis keys to enter and leave a filterable submenu', async ({ page, mode }) => {
    await render(page, mode, { dir: 'rtl', before: '', label: 'Move to folder', after: '', children: 'Documents' });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowLeft');

    await expect(submenuInput(page)).toBeVisible();
    await mouseMove(submenuInput(page));
    await expect(submenuInput(page)).toBeFocused();

    await page.keyboard.press('ArrowRight');

    await expect(submenuInput(page)).toHaveCount(0);
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Move to folder')));
  });

  // MNU-KEY-29.
  test('returns virtual focus to the parent input on Escape and resumes filtering', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Move to folder', after: 'Rename', children: 'Documents' });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');
    await expect(submenuInput(page)).toBeVisible();
    await mouseMove(submenuInput(page));
    await expect(submenuInput(page)).toBeFocused();

    await page.keyboard.press('Escape');

    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Move to folder')));

    await page.keyboard.type('ren');

    await expect(parentInput(page)).toHaveValue('ren');
    await expect(item(page, 'Rename')).toBeVisible();
    await expect(item(page, 'Move to folder')).toHaveCount(0);
  });

  // MNU-KEY-29.
  test('opens a submenu from a filterable submenu input', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Move to folder', after: '', children: '', nested: 'More folders' });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');
    const submenuTrigger = item(page, 'Move to folder');
    await expect(submenuInput(page)).toBeVisible();
    await mouseMove(submenuInput(page));
    await expect(submenuInput(page)).toBeFocused();
    const nestedTrigger = item(page, 'More folders');
    await expect(submenuInput(page)).toHaveAttribute('aria-activedescendant', await idOf(nestedTrigger));

    await page.keyboard.press('ArrowRight');
    await expect(item(page, 'Archive')).toBeFocused();

    await page.keyboard.press('ArrowLeft');
    await expect(submenuInput(page)).toBeFocused();
    await expect(submenuInput(page)).toHaveAttribute('aria-activedescendant', await idOf(nestedTrigger));

    await page.keyboard.press('ArrowLeft');
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(submenuTrigger));
  });

  // MNU-KEY-29.
  test('opens a virtually focused submenu with Enter from the input', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Share', after: 'Delete', children: 'Email' });
    const submenuTrigger = item(page, 'Share');
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(submenuTrigger));

    await page.keyboard.press('Enter');

    await expect(submenuInput(page)).toBeFocused();
    await expect(parentInput(page)).not.toHaveAttribute('aria-activedescendant', /.+/);

    await page.keyboard.press('ArrowLeft');
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(submenuTrigger));

    await page.keyboard.press('ArrowDown');
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Delete')));
  });

  // MNU-KEY-29.
  test('does not reopen a stale submenu with a cross-axis key', async ({ page, mode }) => {
    await render(page, mode, { before: 'Rename', label: 'Share', after: '', children: 'Email', second: 'Export' });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');
    await expect(submenuInput(page)).toBeFocused();

    await page.keyboard.press('Escape');
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Share')));

    await page.keyboard.press('ArrowUp');
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Rename')));

    await page.keyboard.press('ArrowRight');
    await expect(submenuInput(page)).toHaveCount(0);

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');
    await expect(page.getByRole('searchbox', { name: 'Filter second' })).toBeFocused();
    await expect(submenuInput(page)).toHaveCount(0);
  });

  // MNU-FOC-15.
  test('moves focus into the submenu input once the pointer enters it', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Move to folder', after: '', children: 'Documents', delay: 0 });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    const submenuTrigger = item(page, 'Move to folder');
    await submenuTrigger.hover();

    // Hovering only opens the submenu; the parent input keeps focus until the pointer enters.
    await expect(submenuInput(page)).toBeVisible();
    await expect(parentInput(page)).toBeFocused();
    await mouseMove(submenuInput(page));
    await expect(submenuInput(page)).toBeFocused();
    await expect(submenuInput(page)).toHaveAttribute('data-highlighted', '');
    await expect(submenuInput(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    // The parent keeps the trigger highlighted as the submenu's origin.
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(submenuTrigger));
  });

  // MNU-EVT-12, KEY-29. The fixture records each highlight report as `label:reason:key`.
  test('passes the keydown events when entering and leaving a filterable submenu', async ({ page, mode }) => {
    const fixture = await render(page, mode, { before: '', label: 'Share', after: '', children: 'Email' });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');
    await expect(submenuInput(page)).toBeFocused();

    await expect(fixture).toHaveAttribute('data-highlights', /none:keyboard:ArrowRight$/);

    await page.keyboard.press('ArrowLeft');

    await expect(fixture).toHaveAttribute('data-highlights', /Share:keyboard:ArrowLeft$/);
  });

  // MNU-KEY-29.
  test('reports no keyboard reason when Enter opens a filterable submenu', async ({ page, mode }) => {
    const fixture = await render(page, mode, { before: '', label: 'Share', after: '', children: 'Email' });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(submenuInput(page)).toBeFocused();

    await expect(fixture).toHaveAttribute('data-highlights', /none:none:[^|]*$/);
  });
});
