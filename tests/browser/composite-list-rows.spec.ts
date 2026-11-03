import { type Locator, type Page, type Route } from '@playwright/test';
import { expect, Fixture, fixtureUrl, test, type RenderMode } from './helpers';
import { gotoPopupFixture, openChanges, settle, type PopupFixtureOptions } from './suites/popupFixture';

// List navigation's L4 rows outside the popup-list suite (M5 track 1, issue 159; tests/browser/composite-list.spec.ts runs
// the suite): the non-suite [L4] rows of composite-and-list-navigation.md (CMP), the "(L4 for …)" clauses the suite does
// not already prove, the re-tags' port titles, and the stalled-load attach-order titles (Deliverable 4, the standing
// rule of CI run 40461905812). Base UI's titles verbatim on the test-only list hosts; a port title carries `port:`.
// useListNavigation.test.tsx's App is `composite-list-dom` with three items; its nested menus (test/floating-ui-tests/
// Menu.tsx's Main, MenuOrientation.tsx's HorizontalMenu) are `composite-list-nested`; ListboxFocus.tsx is
// `composite-list-selected`; the relay's menubar is `composite-list-relay`.

const APP_ITEMS = 'one|two|three';

const menuItem = (page: Page, name: string) => page.getByRole('menuitem', { name, exact: true, includeHidden: true });

/** Settles the events so far: the barrier (each has reached C#), then two frames. */
async function settled(page: Page, fixture: Locator): Promise<void> {
  await settle(fixture);
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
}

/** Records the keydowns that reach the document, and the last keydown's `defaultPrevented`, from a window capture listener. */
async function recordKeys(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as { __reached: string[]; __last: KeyboardEvent | null };
    w.__reached = [];
    w.__last = null;
    window.addEventListener('keydown', (event) => (w.__last = event), true);
    document.addEventListener('keydown', (event) => w.__reached.push(event.key));
  });
}

/** The last keydown: its key, whether its default was prevented, and whether it reached the document. */
function lastKey(page: Page): Promise<{ key: string; prevented: boolean; reached: boolean }> {
  return page.evaluate(() => {
    const w = window as unknown as { __reached: string[]; __last: KeyboardEvent };
    return { key: w.__last.key, prevented: w.__last.defaultPrevented, reached: w.__reached.includes(w.__last.key) };
  });
}

async function openApp(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
  return gotoPopupFixture(page, mode, 'composite-list-dom', { items: APP_ITEMS, recordHighlights: true, ...options });
}

async function highlights(fixture: Locator): Promise<string[]> {
  const value = await fixture.getAttribute('data-highlights');
  return value ? value.split(' ') : [];
}

test.describe('useListNavigation', () => {
  // CMP-FOC-03, CMP-STA-35 (L4 for the focus clause).
  test('navigates down on ArrowDown', async ({ page, mode }) => {
    await openApp(page, mode);
    await page.getByTestId('trigger').focus();

    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('menu')).toBeVisible();
    await expect(menuItem(page, 'one')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'two')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'three')).toBeFocused();

    // Reached the end of the list.
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'three')).toBeFocused();
  });

  test.describe('prop: selectedIndex', () => {
    // CMP-FOC-04, CMP-STA-33: Base UI spies on scrollIntoView; so does the page.
    test('scrolls the selected item into view on open', async ({ page, mode }) => {
      const fixture = await openApp(page, mode, { selectable: true, role: 'option', defaultValue: 'one' });
      await page.evaluate(() => {
        const w = window as unknown as { __scrolled: string[] };
        w.__scrolled = [];
        const original = Element.prototype.scrollIntoView;
        Element.prototype.scrollIntoView = function (this: Element, options?: boolean | ScrollIntoViewOptions) {
          w.__scrolled.push(this.textContent ?? '');
          original.call(this, options);
        };
      });

      await page.getByTestId('trigger').click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await settled(page, fixture);

      await expect.poll(() => page.evaluate(() => (window as unknown as { __scrolled: string[] }).__scrolled)).toContain('one');
    });
  });

  test.describe('prop: focusItemOnHover', () => {
    // CMP-FOC-03, CMP-PTR-05, CMP-ALG-31: Base UI holds the frame callbacks with a requestAnimationFrame spy; the page
    // does the same (the frame scheduler reads requestAnimationFrame at each request), so the open's deferred item focus
    // is pending when the pointer leaves. The list host's focus manager takes no initial focus (`initialFocus=false`),
    // as the App has none.
    test('cancels pending item focus when the pointer leaves before focus lands', async ({ page, mode }) => {
      const fixture = await openApp(page, mode, { focusItemOnOpen: true, initialFocus: false });
      await page.evaluate(() => {
        const w = window as unknown as { __frames: Map<number, FrameRequestCallback>; __flushFrames: () => void; __restoreFrames: () => void };
        const request = window.requestAnimationFrame;
        const cancel = window.cancelAnimationFrame;
        let frameId = 0;
        w.__frames = new Map();
        window.requestAnimationFrame = (callback) => {
          frameId += 1;
          w.__frames.set(frameId, callback);
          return frameId;
        };
        window.cancelAnimationFrame = (id) => {
          w.__frames.delete(id);
        };
        w.__flushFrames = () => {
          const callbacks = Array.from(w.__frames.values());
          w.__frames.clear();
          callbacks.forEach((callback) => callback(performance.now()));
        };
        w.__restoreFrames = () => {
          window.requestAnimationFrame = request;
          window.cancelAnimationFrame = cancel;
        };
      });

      try {
        await page.getByTestId('trigger').click();
        const menu = page.getByRole('menu');
        const item = menuItem(page, 'one');

        await expect(item).toHaveAttribute('data-highlighted', '');
        await expect(item).not.toBeFocused();

        await item.evaluate((element) =>
          element.dispatchEvent(new PointerEvent('pointerleave', { pointerType: 'mouse', relatedTarget: document.body })),
        );

        await page.evaluate(() => (window as unknown as { __flushFrames: () => void }).__flushFrames());

        await expect(item).not.toBeFocused();
        await expect(menu).not.toBeFocused();
        await expect(item).not.toHaveAttribute('data-highlighted');
        await settle(fixture);
        expect((await highlights(fixture)).at(-1)).toBe('null');
      } finally {
        await page.evaluate(() => (window as unknown as { __restoreFrames: () => void }).__restoreFrames());
      }
    });

    // CMP-API-03 (L4 for the focus clause), CMP-PTR-04: Base UI's mouseMove with a movement is Playwright's real hover;
    // its pointerLeave is dispatched as Base UI's fireEvent does.
    test('true - focuses item on hover and syncs the active index', async ({ page, mode }) => {
      const fixture = await openApp(page, mode);
      await page.getByTestId('trigger').click();
      await expect(page.getByRole('menu')).toBeFocused();

      await menuItem(page, 'two').hover();
      await expect(menuItem(page, 'two')).toBeFocused();
      await menuItem(page, 'two').evaluate((element) => element.dispatchEvent(new PointerEvent('pointerleave', { pointerType: 'mouse' })));
      await expect(page.getByRole('menu')).toBeFocused();
      await settle(fixture);
      expect(await highlights(fixture)).toContain('1');
    });

    test('false - does not focus item on hover and does not sync the active index', async ({ page, mode }) => {
      const fixture = await openApp(page, mode, { focusItemOnOpen: false, focusItemOnHover: false });
      await page.getByTestId('trigger').click();
      await expect(page.getByRole('menu')).toBeFocused();

      await menuItem(page, 'two').hover();
      await settled(page, fixture);

      await expect(menuItem(page, 'two')).not.toBeFocused();
      await expect(menuItem(page, 'two')).not.toHaveAttribute('data-highlighted');
      expect(await highlights(fixture)).toEqual([]);
    });
  });

  // CMP-STA-40.
  test('selectedIndex changing does not steal focus', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'composite-list-selected');

    await expect.poll(() => page.evaluate(() => document.activeElement?.getAttribute('role'))).toBe('option');

    await page.getByTestId('reference').click();

    await expect(page.getByTestId('reference')).toBeFocused();
    // The selected index reached the module while the list is open, and nothing moved the focus back.
    await expect(fixture).toHaveAttribute('data-value-changes', 'Blueberry');
    await expect(page.getByRole('option', { name: 'Blueberry' })).toHaveAttribute('aria-selected', 'true');
    await settled(page, fixture);
    await expect(page.getByTestId('reference')).toBeFocused();
    await expect(page.getByRole('listbox')).toBeVisible();
  });

  // CMP-KEY-28 (L4 for the consumption clause): the list host's virtual page is the typeable combobox reference (an
  // input under virtual focus); Base UI's App focuses its items, the port's keeps the focus on the input and moves
  // `aria-activedescendant`, so the highlight is read from `data-highlighted`.
  test('Home or End key press is ignored for typeable combobox reference', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'composite-list-virtual', { items: APP_ITEMS, role: 'option' });
    await recordKeys(page);
    const input = page.getByRole('combobox');
    await input.focus();

    await page.keyboard.press('ArrowDown');

    await expect(page.getByRole('option', { name: 'one' })).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('End');

    await expect(page.getByRole('option', { name: 'one' })).toHaveAttribute('data-highlighted', '');
    expect(await lastKey(page)).toEqual({ key: 'End', prevented: false, reached: true });

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Home');

    await expect(page.getByRole('option', { name: 'two' })).toHaveAttribute('data-highlighted', '');
    expect(await lastKey(page)).toEqual({ key: 'Home', prevented: false, reached: true });
    await expect(input).toBeFocused();
  });

  // In JSDOM it will not focus the first item, but will in the browser. Base UI runs the three nested titles in jsdom
  // only; the port's host opens by a press with the focus on the list element and no highlight, as jsdom's does, so
  // the same keys reach the same items.
  test.describe('nested lists', () => {
    async function openEdit(page: Page, mode: RenderMode, orientation: 'vertical' | 'horizontal'): Promise<Locator> {
      const fixture = await gotoPopupFixture(page, mode, 'composite-list-nested', { orientation });
      await page.getByRole('button', { name: 'Edit' }).click();
      await expect(page.locator('#edit-list')).toBeFocused();
      // The submenus' roots render inside the Edit menu's popup.
      await expect(fixture).toHaveAttribute('data-roots-attached', '3');
      return fixture;
    }

    test('focus management in nested lists', async ({ page, mode }) => {
      await openEdit(page, mode, 'vertical');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowRight');

      await expect(menuItem(page, 'Text')).toBeFocused();
    });

    // CMP-KEY-27, CMP-DIS-01, CMP-KEY-23.
    test('keyboard navigation in nested menus lists', async ({ page, mode }) => {
      const fixture = await openEdit(page, mode, 'vertical');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowRight'); // opens first submenu
      await expect(menuItem(page, 'Text')).toBeFocused();
      await expect(fixture).toHaveAttribute('data-roots-attached', '4');

      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowRight'); // opens second submenu

      await expect(menuItem(page, '.png')).toBeFocused();

      // it navigation with orientation = 'both'
      await page.keyboard.press('ArrowRight');
      await expect(menuItem(page, '.jpg')).toBeFocused();

      await page.keyboard.press('ArrowDown');
      await expect(menuItem(page, '.gif')).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(menuItem(page, '.svg')).toBeFocused();

      await page.keyboard.press('ArrowUp');
      await expect(menuItem(page, '.png')).toBeFocused();

      // escape closes the submenu
      await page.keyboard.press('Escape');
      await expect(menuItem(page, 'Image')).toBeFocused();
    });

    // CMP-KEY-27, CMP-KEY-23.
    test('keyboard navigation in nested menus with different orientation', async ({ page, mode }) => {
      await openEdit(page, mode, 'horizontal');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowDown'); // opens the Copy as submenu
      await expect(menuItem(page, 'Text')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowDown'); // opens the Share submenu

      await expect(menuItem(page, 'Mail')).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(menuItem(page, 'Copy as')).toBeFocused();
    });

    // The re-tags' port titles (CMP-KEY-27 and CMP-DIS-01's focus return across the portal; CMP-KEY-23's consumption
    // and focus clauses): React bubbles a portaled child's keydown through its parent popup, the port's child popup sits
    // in its own portal container, so the return focus and the consumption cross the portal.
    test('port: the cross-orientation open key on a submenu trigger is consumed and opens the submenu on its first item', async ({ page, mode }) => {
      const fixture = await openEdit(page, mode, 'vertical');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await expect(menuItem(page, 'Copy as')).toBeFocused();
      await recordKeys(page);

      await page.keyboard.press('ArrowRight');

      expect(await lastKey(page)).toEqual({ key: 'ArrowRight', prevented: true, reached: false });
      await expect(menuItem(page, 'Text')).toBeFocused();
      await expect(menuItem(page, 'Text')).toHaveAttribute('data-highlighted', '');
      await settle(fixture);
      expect(await openChanges(fixture)).toEqual(['edit:true:trigger-press', 'copy:true:list-navigation']);
    });

    test("port: a nested list's close key returns the focus across the portal to its submenu trigger, consumed when it is not the parent's main key", async ({ page, mode }) => {
      const fixture = await openEdit(page, mode, 'vertical');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowRight');
      await expect(menuItem(page, 'Text')).toBeFocused();
      // The submenu's popup is in its own portal container, outside the Edit menu's popup.
      expect(await page.getByTestId('popup').evaluate((popup) => popup.contains(document.querySelector('[data-testid="copy-popup"]')))).toBe(false);
      await recordKeys(page);

      await page.keyboard.press('ArrowLeft');

      expect(await lastKey(page)).toEqual({ key: 'ArrowLeft', prevented: true, reached: false });
      await expect(page.getByTestId('copy-popup')).toHaveCount(0);
      await expect(menuItem(page, 'Copy as')).toBeFocused();
      await expect(menuItem(page, 'Copy as')).toHaveAttribute('data-highlighted', '');
      await expect(page.getByTestId('popup')).toBeVisible();
      await settle(fixture);
      expect((await openChanges(fixture)).slice(0, 3)).toEqual(['edit:true:trigger-press', 'copy:true:list-navigation', 'copy:false:list-navigation']);
    });

    test("port: a nested list's close key that is the parent's main key closes the submenu and moves the parent list", async ({ page, mode }) => {
      const fixture = await openEdit(page, mode, 'horizontal');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      await expect(menuItem(page, 'Share')).toBeFocused();
      await page.keyboard.press('ArrowDown');
      await expect(menuItem(page, 'Mail')).toBeFocused();
      await recordKeys(page);

      await page.keyboard.press('ArrowLeft');

      // Not consumed by the submenu: the parent list took it, and stopped it there.
      expect(await lastKey(page)).toEqual({ key: 'ArrowLeft', prevented: true, reached: false });
      await expect(page.getByTestId('share-popup')).toHaveCount(0);
      await expect(menuItem(page, 'Copy as')).toBeFocused();
      await expect(menuItem(page, 'Copy as')).toHaveAttribute('data-highlighted', '');
      await settle(fixture);
      // On Server the focus manager's focus-out close of the closing submenu may follow the list's own close.
      expect((await openChanges(fixture)).filter((change) => change.startsWith('share:')).slice(0, 2)).toEqual(['share:true:list-navigation', 'share:false:list-navigation']);
    });
  });
});

// CMP-KEY-22 (L4 for the consumption clause), CMP-KEY-37 (L4 for the consumption clause), CMP-KEY-28 (L4 for the
// consumption clause, DOM focus).
test.describe('consumption', () => {
  test('port: an arrow key on the closed trigger is consumed: its default is prevented and it does not propagate', async ({ page, mode }) => {
    await openApp(page, mode);
    await recordKeys(page);
    await page.getByTestId('trigger').focus();

    await page.keyboard.press('ArrowDown');

    expect(await lastKey(page)).toEqual({ key: 'ArrowDown', prevented: true, reached: false });
    await expect(menuItem(page, 'one')).toBeFocused();
  });

  test('port: Home and End on the open list are consumed and move to the first and last items', async ({ page, mode }) => {
    await openApp(page, mode);
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'one')).toBeFocused();
    await recordKeys(page);

    await page.keyboard.press('End');
    expect(await lastKey(page)).toEqual({ key: 'End', prevented: true, reached: false });
    await expect(menuItem(page, 'three')).toBeFocused();

    await page.keyboard.press('Home');
    expect(await lastKey(page)).toEqual({ key: 'Home', prevented: true, reached: false });
    await expect(menuItem(page, 'one')).toBeFocused();
  });

  test('port: a typeahead character on the open list is consumed', async ({ page, mode }) => {
    const fixture = await openApp(page, mode, { items: 'Alpha|Bravo|Charlie', typeahead: true });
    await page.getByTestId('trigger').click();
    await expect(page.getByRole('menu')).toBeFocused();
    await recordKeys(page);

    await page.keyboard.press('b');

    expect(await lastKey(page)).toEqual({ key: 'b', prevented: true, reached: false });
    await expect(menuItem(page, 'Bravo')).toBeFocused();
    await settle(fixture);
    expect(await highlights(fixture)).toContain('1');
  });
});

// CMP-KEY-14 (Deliverable 7; ADR 04 Amendments 2026-11-03 (4)): the L3 relay titles on a test-only menubar whose Edit
// list is portaled outside it.
test.describe('keyboard relay', () => {
  async function openEditFromBar(page: Page, mode: RenderMode): Promise<Locator> {
    const fixture = await gotoPopupFixture(page, mode, 'composite-list-relay');
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'Undo')).toBeFocused();
    return fixture;
  }

  test('port: a keydown the portaled list does not stop is relayed to the composite root, which moves to its next item and stops it', async ({ page, mode }) => {
    const fixture = await openEditFromBar(page, mode);
    expect(await page.getByTestId('menubar').evaluate((bar) => bar.contains(document.querySelector('[data-testid="popup"]')))).toBe(false);

    // The list handles ArrowDown: nothing reaches the bar.
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'Redo')).toBeFocused();
    await expect(page.getByTestId('trigger')).toHaveAttribute('tabindex', '0');

    // ArrowRight is no key of the vertical list: the bar moves to the next item, and the event stops there.
    await recordKeys(page);
    await page.keyboard.press('ArrowRight');

    expect(await lastKey(page)).toEqual({ key: 'ArrowRight', prevented: true, reached: false });
    await expect(page.getByTestId('view')).toBeFocused();
    await expect(page.getByTestId('view')).toHaveAttribute('tabindex', '0');
    await settle(fixture);
  });

  test("port: a nested close key that is the bar's main key closes the menu and is relayed, moving the bar to the previous item", async ({ page, mode }) => {
    const fixture = await openEditFromBar(page, mode);
    await recordKeys(page);

    await page.keyboard.press('ArrowLeft');

    expect(await lastKey(page)).toEqual({ key: 'ArrowLeft', prevented: true, reached: false });
    await expect(page.getByTestId('file')).toBeFocused();
    await expect(page.getByTestId('file')).toHaveAttribute('tabindex', '0');
    await expect(page.getByTestId('popup')).toHaveCount(0);
    await settle(fixture);
    // On Server the focus manager's focus-out close of the closing menu may follow the list's own close.
    expect((await openChanges(fixture)).slice(0, 2)).toEqual(['true:list-navigation', 'false:list-navigation']);
  });
});

// The attach order of two handles on one element (Deliverable 4; the standing rule of CI run 40461905812; design point
// 1's two handles): the trigger carries the `interactions` trigger handle and the list's reference handle, and the list
// handles wait for the session's attach (ListNavigationBehavior.SessionReady), so whichever module arrives last the list
// still opens on ArrowDown with its first item highlighted. And the focus manager re-picks its default initial focus when
// the list handle writes a `tabindex` inside the open popup (M4 track 4's re-pick).
test.describe('attach order', () => {
  async function stall(page: Page, module: string): Promise<{ hits: () => number; release: () => Promise<void> }> {
    const held: Route[] = [];
    let hits = 0;
    let released = false;
    // WebAssembly requests the fingerprinted URL (`<module>.<hash>.js`).
    await page.route(new RegExp(`/js/${module}(\\.[\\w-]+)?\\.js(\\?.*)?$`), (route) => {
      hits += 1;
      return released ? route.continue() : void held.push(route);
    });
    return {
      hits: () => hits,
      release: async () => {
        released = true;
        for (const route of held.splice(0)) {
          await route.continue();
        }
      },
    };
  }

  async function gotoUnready(page: Page, mode: RenderMode, name: string, options: PopupFixtureOptions = {}): Promise<Locator> {
    const query = new URLSearchParams(Object.entries(options).map(([key, value]) => [key, String(value)]));
    const search = query.toString();
    await page.goto(search ? `${fixtureUrl(mode, name)}?${search}` : fixtureUrl(mode, name), { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
    return page.locator(Fixture.container(name));
  }

  for (const module of ['composite', 'interactions']) {
    test(`port: with js/${module}.js stalled the list opens on ArrowDown with its first item highlighted once both modules load`, async ({ page, mode }) => {
      const stalled = await stall(page, module);
      const fixture = await gotoUnready(page, mode, 'composite-list-dom', { items: APP_ITEMS });
      await expect.poll(stalled.hits).toBeGreaterThan(0);
      await expect(fixture).toHaveAttribute('data-attached', 'false');

      await stalled.release();
      await expect(fixture).toHaveAttribute('data-attached', 'true');
      await page.getByTestId('trigger').focus();
      await page.keyboard.press('ArrowDown');

      await expect(page.getByRole('menu')).toBeVisible();
      await expect(menuItem(page, 'one')).toHaveAttribute('data-highlighted', '');
      await expect(menuItem(page, 'one')).toBeFocused();
      await settle(fixture);
      expect(await openChanges(fixture)).toEqual(['true:list-navigation']);
    });
  }

  // The ordering rule's own case: a list open from the start emits no `openchange`, so a list handle that attached before
  // the session's handle (held back with js/interactions.js) would read a closed session and never run the open's sync.
  test('port: with js/interactions.js stalled a list open from the start highlights its first item once the session attaches', async ({ page, mode }) => {
    const stalled = await stall(page, 'interactions');
    const fixture = await gotoUnready(page, mode, 'composite-list-dom', { items: APP_ITEMS, defaultOpen: true, focusItemOnOpen: true });
    await expect.poll(stalled.hits).toBeGreaterThan(0);
    await expect(page.getByRole('menu')).toBeVisible();
    await expect(menuItem(page, 'one')).not.toHaveAttribute('data-highlighted');

    await stalled.release();

    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await expect(menuItem(page, 'one')).toHaveAttribute('data-highlighted', '');
    await expect(menuItem(page, 'one')).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'two')).toBeFocused();
  });

  // Every list item renders tabindex="-1" until js/composite.js attaches; the focus manager's default initial focus
  // (`initialFocusOnList=false`) first finds no tabbable item and moves once onto the item the list handle writes
  // tabindex="0" on (`focusItemOnOpen=true` highlights the first item on a press).
  test('port: with js/composite.js stalled the focus manager re-picks its initial focus onto the item the list handle writes tabindex="0" on', async ({ page, mode }) => {
    const stalled = await stall(page, 'composite');
    const fixture = await gotoUnready(page, mode, 'composite-list-dom', { items: APP_ITEMS, focusItemOnOpen: true, initialFocusOnList: false });
    await expect.poll(stalled.hits).toBeGreaterThan(0);
    // The interactions handle attaches without the composite module, so a press opens the popup.
    await expect(page.getByTestId('trigger')).toHaveAttribute('data-interactions-attached', '');
    await page.getByTestId('trigger').click();
    await expect(page.getByRole('menu')).toBeVisible();
    await expect(menuItem(page, 'one')).toHaveAttribute('tabindex', '-1');

    await stalled.release();

    await expect(menuItem(page, 'one')).toHaveAttribute('tabindex', '0');
    await expect(menuItem(page, 'one')).toBeFocused();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'two')).toBeFocused();
  });

  test('port: with js/focus-manager.js stalled the list opens on ArrowDown on its first item, and the focus stays there once the focus manager attaches', async ({ page, mode }) => {
    const stalled = await stall(page, 'focus-manager');
    const fixture = await gotoPopupFixture(page, mode, 'composite-list-dom', { items: APP_ITEMS, initialFocusOnList: false });
    await page.getByTestId('trigger').focus();
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'one')).toBeFocused();
    await expect.poll(stalled.hits).toBeGreaterThan(0);

    await stalled.release();
    await expect(page.locator('[data-handle-attached]')).toHaveCount(1);
    await settled(page, fixture);

    await expect(menuItem(page, 'one')).toBeFocused();
    await expect(menuItem(page, 'one')).toHaveAttribute('tabindex', '0');
    await page.keyboard.press('ArrowDown');
    await expect(menuItem(page, 'two')).toBeFocused();
  });
});
