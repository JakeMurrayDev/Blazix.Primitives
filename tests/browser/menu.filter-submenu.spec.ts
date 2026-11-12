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

// More of MenuFilterRoot.test.tsx's and MenuFilterRoot.scoping.test.tsx's submenu titles, and
// MenuFilterSubmenuRoot.pointerFocus.test.tsx, on the same fixture. Base UI's `firePointer` presses are dispatched pointer,
// mouse and click events here too.
test.describe('filterable submenu navigation, focus and pointer', () => {
  const parentInput = (page: Page): Locator => page.getByRole('searchbox', { name: 'Filter actions' });

  function mouseMove(locator: Locator): Promise<boolean> {
    return locator.evaluate((element) => element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true })));
  }

  async function idOf(locator: Locator): Promise<string> {
    return (await locator.getAttribute('id')) ?? 'missing';
  }

  // MNU-KEY-32. ParentNavigationMenu: a plain parent whose filterable submenu trigger "Move to folder" is followed by an
  // `aria-disabled` item.
  test('keeps disabled parent items reachable during main-axis navigation', async ({ page, mode }) => {
    await render(page, mode, { ...plain, before: '', label: 'Move to folder', after: 'Disabled,Next', disabled: 'Disabled' });
    await item(page, 'Move to folder').focus();

    await page.keyboard.press('ArrowDown');

    // The parent's own navigation lands on aria-disabled neighbours; arrowing off the trigger must match.
    await expect(item(page, 'Disabled')).toBeFocused();
  });

  // MNU-KEY-32.
  test('wraps parent navigation when loopFocus is enabled', async ({ page, mode }) => {
    await render(page, mode, { ...plain, before: 'Disabled,Next', label: 'Move to folder', after: '', disabled: 'Disabled' });
    await item(page, 'Move to folder').focus();

    await page.keyboard.press('ArrowDown');

    // The trigger is last, so this wraps to the first item, which is the disabled one.
    await expect(item(page, 'Disabled')).toBeFocused();
  });

  // MNU-FOC-15. FilterableSubmenu: a plain parent with Rename and the filterable "Move to folder" (Documents, Downloads).
  // Base UI's open plain menu focuses its popup on mount; the port focuses it first.
  test('focuses the input when a filterable submenu opens with the keyboard', async ({ page, mode }) => {
    await render(page, mode, { ...plain, label: 'Move to folder', after: '', children: 'Documents,Downloads', delay: 0 });
    await page.getByTestId('popup').focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowRight');

    await expect(submenuInput(page)).toBeFocused();
    await expect(submenuInput(page)).not.toHaveAttribute('data-highlighted');
    await expect(submenuInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Documents')));

    await item(page, 'Documents').hover();
    await expect(submenuInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Documents')));
    await expect(submenuInput(page)).toHaveAttribute('data-highlighted', '');

    await submenuInput(page).hover();
    await expect(submenuInput(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(submenuInput(page)).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('ArrowDown');
    await expect(submenuInput(page)).not.toHaveAttribute('data-highlighted');

    await page.keyboard.press('ArrowUp');
    await expect(submenuInput(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(submenuInput(page)).toHaveAttribute('data-highlighted', '');
  });

  // MNU-FOC-15.
  test('closes a hover-opened submenu from a virtually focused parent', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Move to folder', after: '', children: 'Documents', delay: 0 });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    const submenuTrigger = item(page, 'Move to folder');
    await submenuTrigger.hover();
    await expect(submenuInput(page)).toBeVisible();
    await mouseMove(submenuInput(page));
    await expect(submenuInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(submenuTrigger));

    await page.keyboard.press('ArrowLeft');

    await expect(submenuInput(page)).toHaveCount(0);
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(submenuTrigger));
  });

  // MNU-FOC-15.
  test('focuses the first item when entering a hover-opened submenu from a filterable menu', async ({ page, mode }) => {
    await render(page, mode, { child: 'plain', before: '', label: 'Move to folder', after: '', children: 'Documents', delay: 0 });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await item(page, 'Move to folder').hover();
    const firstItem = item(page, 'Documents');
    await expect(firstItem).toBeVisible();
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    await expect(parentInput(page)).toBeFocused();

    await page.keyboard.press('ArrowRight');

    await expect(firstItem).toBeFocused();
  });

  // MNU-KEY-30.
  for (const autoHighlight of [false, true]) {
    test(`keeps cross-axis keys as caret keys while the submenu query is not empty (autoHighlight: ${autoHighlight})`, async ({ page, mode }) => {
      await render(page, mode, { before: '', label: 'Move to folder', after: '', children: 'Documents', ...(autoHighlight ? { auto: 'true' } : {}) });
      await parentInput(page).focus();

      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowRight');
      await expect(submenuInput(page)).toBeFocused();
      await page.keyboard.type('doc');
      await expect(submenuInput(page)).toHaveValue('doc');

      // Editing the query must not leave the submenu; the caret moves instead.
      await page.keyboard.press('ArrowLeft');

      await expect(submenuInput(page)).toBeFocused();
      expect(await submenuInput(page).evaluate((element: HTMLInputElement) => element.selectionStart)).toBe(2);
      await expect(submenuInput(page)).toHaveValue('doc');
    });
  }

  // MNU-KEY-30. A plain submenu "Share" (Email) in a filterable parent.
  test('does not open a highlighted submenu with a modified cross-axis key from the input', async ({ page, mode }) => {
    await render(page, mode, { child: 'plain', before: '', label: 'Share', after: '', children: 'Email', delay: 0 });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Share')).toHaveAttribute('data-highlighted', '');
    await dispatchMetaArrowRight(parentInput(page));
    await expect(page.getByTestId('submenu')).toHaveCount(0);

    await page.keyboard.press('ArrowRight');

    await expect(page.getByTestId('submenu')).toBeVisible();
  });

  async function dispatchMetaArrowRight(locator: Locator): Promise<void> {
    await locator.evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', metaKey: true, bubbles: true, cancelable: true })));
  }

  // MNU-KEY-31, EVT-19.
  test('does not forward submenu navigation keys while composing text', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Move to folder', after: '', children: 'Documents', hover: false });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(item(page, 'Move to folder')));
    await dispatchKey(parentInput(page), { key: 'ArrowRight', keyCode: 229 });

    await expect(submenuInput(page)).toHaveCount(0);
  });

  // MNU-KEY-29 (MenuFilterRoot.scoping.test.tsx). A plain submenu "Share" (Email, Copy link) after Rename.
  test('enters and leaves a plain submenu with the cross-axis keys from the input', async ({ page, mode }) => {
    await render(page, mode, { child: 'plain', label: 'Share', after: '', children: 'Email,Copy link' });
    await parentInput(page).focus();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    const trigger = item(page, 'Share');
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(trigger));

    // A plain submenu roves real focus, so entering it hands focus to its first item.
    await page.keyboard.press('ArrowRight');
    await expect(item(page, 'Email')).toBeFocused();
    // Only the focused submenu item is highlighted, as when a roving trigger blurs.
    await expect(trigger).not.toHaveAttribute('data-highlighted');

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 'Copy link')).toBeFocused();

    // Leaving returns focus to the input and highlights the trigger again.
    await page.keyboard.press('ArrowLeft');
    await expect(page.getByTestId('submenu')).toHaveCount(0);
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('aria-activedescendant', await idOf(trigger));
  });

  // MNU-KEY-29, FOC-15.
  for (const filterSubmenu of [false, true]) {
    test.describe(`with a filtered submenu: ${filterSubmenu}`, () => {
      const cases: Array<[string, string]> = [
        ['[ArrowRight]', '[ArrowLeft]'],
        ['[ArrowRight]', '{Shift>}[Tab]{/Shift}'],
        ['[Enter]', '[Escape]'],
      ];
      for (const [openKey, closeKey] of cases) {
        test(`restores input focus after opening with ${openKey} and closing with ${closeKey}`, async ({ page, mode }) => {
          await render(page, mode, { child: filterSubmenu ? 'filter' : 'plain', before: '', label: 'More', after: 'Rename', children: 'Child action', hover: false });
          await parentInput(page).focus();

          await page.keyboard.press('ArrowDown');
          await page.keyboard.press(openKey.slice(1, -1));
          const child = filterSubmenu ? submenuInput(page) : item(page, 'Child action');
          await expect(child).toBeFocused();
          await page.keyboard.press(closeKey.startsWith('{Shift') ? 'Shift+Tab' : closeKey.slice(1, -1));

          await expect(item(page, 'Child action')).toHaveCount(0);
          await expect(parentInput(page)).toBeFocused();
          await page.keyboard.type('ren');
          await expect(parentInput(page)).toHaveValue('ren');
          await expect(item(page, 'Rename')).toBeVisible();
        });
      }
    });
  }

  // MNU-PTR-22.
  test('returns focus to the parent input when a mouse enters the trigger after a tap', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Move to folder', after: '', children: 'Documents', delay: 0, closeDelay: 1000 });
    const submenuTrigger = item(page, 'Move to folder');
    await submenuTrigger.hover();
    await expect(submenuInput(page)).toBeVisible();
    // The root popup's initial focus runs a frame after mounting; let it land so it can't steal focus from the tap.
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

    await submenuInput(page).evaluate((element: HTMLInputElement) => {
      element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerType: 'touch', isPrimary: true }));
      element.focus();
    });
    await expect(submenuInput(page)).toBeFocused();
    // A mouse entering fires pointerover and mouseover before any pointermove.
    await submenuTrigger.evaluate((element) => {
      element.dispatchEvent(new PointerEvent('pointerover', { bubbles: true, pointerType: 'mouse' }));
      element.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
    });

    await expect(parentInput(page)).toBeFocused();
  });

  // MNU-PTR-22. A plain submenu "Share" (Email) first in a filterable parent.
  test('restores the parent input highlight when the pointer returns from a plain submenu', async ({ page, mode }) => {
    await render(page, mode, { child: 'plain', before: '', label: 'Share', after: '', children: 'Email', delay: 0 });
    const trigger = item(page, 'Share');
    await trigger.hover();
    const email = item(page, 'Email');
    await expect(email).toBeVisible();
    await mouseMove(email);
    await expect(email).toBeFocused();
    await expect(parentInput(page)).not.toHaveAttribute('data-highlighted');

    await trigger.evaluate((element) => element.dispatchEvent(new MouseEvent('mouseover', { bubbles: true })));

    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('data-highlighted', '');
  });

  // MNU-FOC-15.
  test('returns focus to the parent input once a pointer-opened submenu unmounts', async ({ page, mode }) => {
    await render(page, mode, { label: 'Move to folder', after: '', children: 'Documents', delay: 0, closeDelay: 0 });
    await item(page, 'Move to folder').hover();
    await expect(submenuInput(page)).toBeVisible();
    await mouseMove(submenuInput(page));
    await expect(submenuInput(page)).toBeFocused();

    await item(page, 'Rename').hover();

    await expect(submenuInput(page)).toHaveCount(0);
    await expect(parentInput(page)).toBeFocused();
    await expect(parentInput(page)).toHaveAttribute('data-highlighted', '');
  });

  // MNU-PTR-22. SiblingSubmenus: two filterable submenus, the second opening only after the first unmounted; the port's
  // inputs read "Filter folders" and "Filter second".
  test('moves focus back to the parent input while the pointer moves between submenu triggers', async ({ page, mode }) => {
    await render(page, mode, { before: '', label: 'Move to folder', after: '', children: 'Option', delay: 0, closeDelay: 10, second: 'Share', secondChildren: 'Option', secondDelay: 50 });
    await item(page, 'Move to folder').hover();
    const firstInput = submenuInput(page);
    await expect(firstInput).toBeVisible();
    await mouseMove(firstInput);
    await expect(firstInput).toBeFocused();

    await item(page, 'Share').hover();
    await expect(parentInput(page)).toBeFocused();
    const secondInput = page.getByRole('searchbox', { name: 'Filter second' });
    await expect(secondInput).toBeVisible();
    // Let the second submenu's open settle without moving focus.
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    await expect(parentInput(page)).toBeFocused();

    await mouseMove(secondInput);

    await expect(secondInput).toBeFocused();
  });
});

// MenuFilterSubmenuRoot.pointerFocus.test.tsx: a non-modal root, plain or filterable, whose list holds the filterable
// submenu "More" (Share; the port's input reads "Filter folders").
for (const filteredParent of [false, true]) {
  test.describe(`submenu pointer focus with filteredParent=${filteredParent}`, () => {
    const query = (extra: Query = {}): Query => ({ ...(filteredParent ? {} : { parent: 'plain', list: true }), modal: false, before: '', label: 'More', after: '', children: 'Share', delay: 0, ...extra });

    async function waitForParentFocus(page: Page): Promise<void> {
      // Base UI's open menu focuses its owner on mount; the port focuses it first.
      const owner = filteredParent ? page.getByRole('searchbox', { name: 'Filter actions' }) : page.getByTestId('parent-list');
      await owner.focus();
      await expect(owner).toBeFocused();
    }

    async function press(trigger: Locator, pointerType: string): Promise<void> {
      await trigger.evaluate((element, type) => element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerType: type, isPrimary: true, width: 10, height: 10, pressure: 0.5 })), pointerType);
      await trigger.evaluate((element) => element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, detail: 1 })));
      await trigger.page().evaluate(() => new Promise((resolve) => requestAnimationFrame(resolve)));
      await trigger.evaluate((element, type) => {
        element.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, cancelable: true, pointerType: type, isPrimary: true }));
        element.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, detail: 1 }));
        element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, detail: 1 }));
      }, pointerType);
    }

    const frame = (page: Page): Promise<unknown> => page.evaluate(() => new Promise((resolve) => requestAnimationFrame(resolve)));

    for (const pointerType of ['mouse', 'touch', 'pen']) {
      // MNU-FOC-15.
      test(`handles a ${pointerType} press without hover opening`, async ({ page, mode }) => {
        await render(page, mode, query({ hover: false }));
        await waitForParentFocus(page);

        await press(item(page, 'More'), pointerType);

        await expect(submenuInput(page)).toBeVisible();
        await frame(page);
        if (pointerType === 'mouse') {
          await expect(submenuInput(page)).toBeFocused();
        } else {
          await expect(submenuInput(page)).not.toBeFocused();
        }
      });
    }

    // MNU-FOC-15.
    test('does not focus the input after a touch press with hover opening enabled', async ({ page, mode }) => {
      await render(page, mode, query());
      await waitForParentFocus(page);

      await press(item(page, 'More'), 'touch');

      await expect(submenuInput(page)).toBeVisible();
      await frame(page);
      await expect(submenuInput(page)).not.toBeFocused();
    });

    for (const pointerType of ['mouse', 'touch', 'pen']) {
      // MNU-FOC-15.
      test(`does not focus a hover-opened input on a ${pointerType} press`, async ({ page, mode }) => {
        await render(page, mode, query());
        await waitForParentFocus(page);
        const trigger = item(page, 'More');
        await trigger.hover();
        await expect(submenuInput(page)).toBeVisible();
        await frame(page);
        await expect(submenuInput(page)).not.toBeFocused();

        await press(trigger, pointerType);

        await frame(page);
        await expect(submenuInput(page)).not.toBeFocused();
      });
    }

    // MNU-FOC-15.
    test('lets keyboard activation enter a hover-opened submenu', async ({ page, mode }) => {
      await render(page, mode, query());
      await waitForParentFocus(page);
      const trigger = item(page, 'More');
      await trigger.hover();
      await expect(submenuInput(page)).toBeVisible();
      await frame(page);
      await expect(submenuInput(page)).not.toBeFocused();

      await trigger.evaluate((element) => element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, detail: 0 })));

      await expect(submenuInput(page)).toBeFocused();
    });
  });
}

// MenuFilterRoot.test.tsx's detached triggers.
test.describe('detached triggers of a filterable menu', () => {
  // MNU-FOC-16. Base UI's menu holds Rename and Delete; the fixture's holds Rename, the submenu trigger "Move to" and
  // Delete, none of which a trigger's focus may highlight.
  test('does not highlight an item when another detached trigger receives focus', async ({ page, mode }) => {
    const fixture = await render(page, mode, { detached: true, closed: true });
    await page.getByRole('button', { name: 'First trigger' }).click();
    const input = page.getByRole('searchbox', { name: 'Filter actions' });
    await expect(input).toBeFocused();
    const before = (await fixture.getAttribute('data-highlights')) ?? '';

    await page.getByRole('button', { name: 'Second trigger' }).focus();

    await expect(input).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(item(page, 'Rename')).not.toHaveAttribute('data-highlighted');
    await expect(fixture).toHaveAttribute('data-highlights', before);
  });
});

test.describe('closing a filtered submenu portaled into a shadow root', () => {
  // MNU-KEY-32. Playwright's locators pierce the open shadow root.
  test('moves a filterable parent on when a submenu portaled into a shadow root closes', async ({ page, mode }) => {
    await render(page, mode, { child: 'plain', closed: true, orientation: 'horizontal', before: 'First', label: 'More', after: 'Last', children: 'Child', shadow: true });

    await page.getByRole('button', { name: 'Actions' }).focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('searchbox', { name: 'Filter actions' })).toBeFocused();
    await page.keyboard.press('ArrowRight');
    const more = item(page, 'More');
    await expect(more).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('ArrowDown');
    const submenu = page.locator('[data-shadow-host]').getByTestId('submenu');
    await expect(submenu).toBeVisible();
    await expect(item(page, 'Child')).toBeFocused();
    await page.keyboard.press('ArrowLeft');

    await expect(submenu).toHaveCount(0);
    await expect(item(page, 'First')).toHaveAttribute('data-highlighted', '');
  });
});

// MenuFilterRoot.scoping.test.tsx's "leaving a plain submenu while it animates out": a plain submenu "Share" (Email) after
// Rename in a filterable parent, both popups with a 300 ms exit, animations on.
test.describe('leaving a plain submenu while it animates out', () => {
  for (const key of ['ArrowLeft', 'Escape']) {
    // MNU-KEY-29.
    test(`highlights the trigger as soon as ${key} closes the submenu`, async ({ page, mode }) => {
      await render(page, mode, { animations: 1, exiting: true, child: 'plain', label: 'Share', after: '', children: 'Email' });
      const input = page.getByRole('searchbox', { name: 'Filter actions' });
      await input.focus();

      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowRight');
      await expect(item(page, 'Email')).toBeFocused();

      await page.keyboard.press(key);

      await expect(page.getByTestId('submenu')).toHaveAttribute('data-ending-style', '');
      await expect(input).toBeFocused();
      await expect(item(page, 'Share')).toHaveAttribute('data-highlighted', '');
      await expect(input).not.toHaveAttribute('data-highlighted');
    });
  }
});
