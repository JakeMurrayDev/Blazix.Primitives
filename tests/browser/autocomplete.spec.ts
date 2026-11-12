import { type Locator, type Route } from '@playwright/test';
import { expect, fixtureUrl, gotoFixture, test, type Page, type RenderMode } from './helpers';
import { center, gotoCombobox, recorded } from './combobox.helpers';
import { openChangeCompletes, openChanges, pressControl, settle } from './suites/popupFixture';

// Base UI's AutocompleteRoot.test.tsx at L4 (M5 track 11, issue 196), titles verbatim where the title is Base UI's, on the
// `autocomplete-root` fixture (tests/Blazix.Primitives.Fixtures.Client/Autocomplete/L4/AutocompleteRootL4Fixture.razor): the
// root's items are apple, banana and cherry. Callbacks are read from the container's recorded attributes. The inline
// completion is computed in C# (decision 11 (a)), so on Server the Input shows it a round trip after the arrow: the titles
// wait for the value, never for a frame.

async function goto(page: Page, mode: RenderMode, options: Record<string, string | boolean> = {}): Promise<Locator> {
  return gotoCombobox(page, mode, 'autocomplete-root', options);
}

function input(page: Page): Locator {
  return page.getByTestId('input');
}

function options(page: Page): Locator {
  return page.getByRole('option');
}

async function watchSubmits(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { submitted: string[] }).submitted = [];
    document.querySelector('[data-testid=form]')!.addEventListener('submit', (event) => {
      (window as unknown as { submitted: string[] }).submitted.push(String(new FormData(event.target as HTMLFormElement).get('tag')));
    });
  });
}

function submitted(page: Page): Promise<string[]> {
  return page.evaluate(() => (window as unknown as { submitted: string[] }).submitted);
}

async function openWithArrow(page: Page): Promise<void> {
  await input(page).focus();
  await input(page).press('ArrowDown');
  await expect(options(page)).toHaveCount(3);
}

async function typeText(page: Page, text: string): Promise<void> {
  await input(page).click();
  await input(page).pressSequentially(text);
}

test.describe('<Autocomplete.Root />', () => {
  test('does not highlight on open via click or when pressing arrow keys initially', async ({ page, mode }) => {
    await goto(page, mode);
    await input(page).click();
    await expect(page.getByRole('listbox')).toHaveCount(0);

    await input(page).press('ArrowDown');
    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(options(page)).toHaveCount(3);
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(page.locator('[role=option][data-highlighted]')).toHaveCount(0);
  });

  test('highlights the first item when opening via ArrowDown', async ({ page, mode }) => {
    await goto(page, mode, { autoHighlight: 'true' });
    await input(page).focus();

    await input(page).press('ArrowDown');
    await expect(options(page)).toHaveCount(3);
    await expect(page.getByTestId('item-apple')).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await page.getByTestId('item-apple').getAttribute('id'))!);
  });

  test('highlights the first item when typing and keeps it during filtering', async ({ page, mode }) => {
    await goto(page, mode, { autoHighlight: 'true' });
    await typeText(page, 'a');
    await expect(options(page)).toHaveCount(2);
    await expect(page.getByTestId('item-apple')).toHaveAttribute('data-highlighted', '');

    await input(page).pressSequentially('n');
    await expect(options(page)).toHaveCount(1);
    await expect(page.getByTestId('item-banana')).toHaveAttribute('data-highlighted', '');
  });

  test('mode="both": inline overlay + autocomplete handles filtering', async ({ page, mode }) => {
    const fixture = await goto(page, mode, { mode: 'both' });
    await typeText(page, 'a');
    await expect(options(page)).toHaveCount(2);

    await input(page).press('ArrowDown');
    await expect(input(page)).toHaveValue('apple');
    await expect(options(page)).toHaveCount(2);
    await input(page).press('ArrowDown');
    await expect(input(page)).toHaveValue('banana');
    await expect(page.getByTestId('item-banana')).toHaveAttribute('data-highlighted', '');
    expect(await recorded(fixture, 'data-value-changes')).toEqual(['a:input-change']);
  });

  test('mode="list" (default): no inline overlay, consumer handles filtering', async ({ page, mode }) => {
    const fixture = await goto(page, mode);
    await typeText(page, 'a');
    await expect(options(page)).toHaveCount(2);

    await input(page).press('ArrowDown');
    await expect(page.getByTestId('item-apple')).toHaveAttribute('data-highlighted', '');
    await expect.poll(async () => (await recorded(fixture, 'data-highlights')).length).toBeGreaterThan(0);
    await expect(input(page)).toHaveValue('a');
  });

  test('mode="inline": static items with inline overlay', async ({ page, mode }) => {
    const fixture = await goto(page, mode, { mode: 'inline', openOnInputClick: true });
    await input(page).click();
    await expect(options(page)).toHaveCount(3);

    await input(page).press('ArrowDown');
    await expect(input(page)).toHaveValue('apple');
    await input(page).press('End');
    await input(page).press('b');
    await expect(input(page)).toHaveValue('appleb');
    await expect.poll(() => recorded(fixture, 'data-value-changes')).toEqual(['appleb:input-change']);
    await expect(options(page)).toHaveCount(3);
  });

  test('mode="both": hovering items should not change the inline overlay (preserve temporary value)', async ({ page, mode }) => {
    await goto(page, mode, { mode: 'both' });
    await typeText(page, 'a');
    // The typed open is a round trip on Server (Combobox's typing relaxation, adopted): the arrow follows the open list.
    await expect(options(page)).toHaveCount(2);
    await input(page).press('ArrowDown');
    await expect(input(page)).toHaveValue('apple');

    await page.getByTestId('item-banana').hover();
    await expect(page.getByTestId('item-banana')).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveValue('apple');
  });

  test('preserves the typed value when the popup is dismissed with Escape', async ({ page, mode }) => {
    const fixture = await goto(page, mode);
    await typeText(page, 'ap');
    await expect(page.getByRole('listbox')).toBeVisible();

    await input(page).press('Escape');
    await expect(page.getByRole('listbox')).toHaveCount(0);
    await expect(input(page)).toHaveValue('ap');
    expect(await recorded(fixture, 'data-value-changes')).toEqual(['a:input-change', 'ap:input-change']);
  });

  test('port: the closed Escape after an activation clears the text and stops the keydown, and the next closed Escape does not', async ({ page, mode }) => {
    const fixture = await goto(page, mode);
    await typeText(page, 'ban');
    await page.getByTestId('item-banana').click();
    await expect(input(page)).toHaveValue('banana');
    await expect(page.getByRole('listbox')).toHaveCount(0);
    await page.evaluate(() => {
      (window as unknown as { escapes: number }).escapes = 0;
      window.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
          (window as unknown as { escapes: number }).escapes++;
        }
      });
    });

    await input(page).press('Escape');
    await expect(input(page)).toHaveValue('');
    await expect.poll(() => recorded(fixture, 'data-value-changes')).toContain(':escape-key');
    expect(await page.evaluate(() => (window as unknown as { escapes: number }).escapes)).toBe(0);

    await input(page).press('Escape');
    await expect.poll(() => page.evaluate(() => (window as unknown as { escapes: number }).escapes)).toBe(1);
  });

  test('submits the typed input value when name is provided on Autocomplete.Root', async ({ page, mode }) => {
    await goto(page, mode);
    await typeText(page, 'xyz');
    await input(page).press('Escape');

    const entries = await page.evaluate(() => new FormData(document.querySelector('[data-testid=form]') as HTMLFormElement).getAll('tag').map(String));
    expect(entries).toEqual(['xyz']);
  });

  test('when true, clicking with pointer submits the owning form', async ({ page, mode }) => {
    await goto(page, mode, { submit: true });
    await page.evaluate(() => {
      (window as unknown as { submitted: string[] }).submitted = [];
      document.querySelector('[data-testid=form]')!.addEventListener('submit', (event) => {
        (window as unknown as { submitted: string[] }).submitted.push(String(new FormData(event.target as HTMLFormElement).get('tag')));
      });
    });
    await typeText(page, 'ch');

    await page.getByTestId('item-cherry').click();

    await expect.poll(() => page.evaluate(() => (window as unknown as { submitted: string[] }).submitted)).toEqual(['cherry']);
    await expect(input(page)).toHaveValue('cherry');
  });

  test('keeps the current highlight when the pointer leaves the list', async ({ page, mode }) => {
    await goto(page, mode, { keepHighlight: true });
    await openWithArrow(page);
    await page.getByTestId('item-banana').hover();
    await expect(page.getByTestId('item-banana')).toHaveAttribute('data-highlighted', '');

    await page.mouse.move(1, 1);
    await expect(page.getByTestId('item-banana')).toHaveAttribute('data-highlighted', '');
  });

  test('clears the highlight when the pointer leaves the list without keepHighlight', async ({ page, mode }) => {
    await goto(page, mode);
    await openWithArrow(page);
    await page.getByTestId('item-banana').hover();
    await expect(page.getByTestId('item-banana')).toHaveAttribute('data-highlighted', '');

    await page.mouse.move(1, 1);
    await expect(page.locator('[role=option][data-highlighted]')).toHaveCount(0);
  });

  test('continues keyboard navigation from the kept highlight after pointer leave', async ({ page, mode }) => {
    await goto(page, mode, { keepHighlight: true });
    await openWithArrow(page);
    await page.getByTestId('item-banana').hover();
    await expect(page.getByTestId('item-banana')).toHaveAttribute('data-highlighted', '');
    await page.mouse.move(1, 1);

    await input(page).press('ArrowDown');
    await expect(page.getByTestId('item-cherry')).toHaveAttribute('data-highlighted', '');
  });

  test('passes native mouse and pointer events for pointer highlights', async ({ page, mode }) => {
    const fixture = await goto(page, mode);
    await openWithArrow(page);

    await page.getByTestId('item-banana').hover();
    await expect.poll(() => recorded(fixture, 'data-highlight-events')).toContain('mousemove');
    await page.mouse.move(1, 1);
    await expect.poll(() => recorded(fixture, 'data-highlight-events')).toContain('pointerleave');
  });

  test('prevents submit on Enter when an item is highlighted by default (false)', async ({ page, mode }) => {
    await goto(page, mode);
    await watchSubmits(page);
    await typeText(page, 'a');
    await expect(options(page)).toHaveCount(2);
    await input(page).press('ArrowDown');
    await expect(page.getByTestId('item-apple')).toHaveAttribute('data-highlighted', '');

    await input(page).press('Enter');
    await expect(input(page)).toHaveValue('apple');
    await expect(page.getByRole('listbox')).toHaveCount(0);
    expect(await submitted(page)).toEqual([]);
  });

  test('when true, pressing Enter in the Input submits the owning form when an item is highlighted', async ({ page, mode }) => {
    await goto(page, mode, { submit: true });
    await watchSubmits(page);
    await typeText(page, 'a');
    await expect(options(page)).toHaveCount(2);
    await input(page).press('ArrowDown');
    await expect(page.getByTestId('item-apple')).toHaveAttribute('data-highlighted', '');

    await input(page).press('Enter');
    await expect.poll(() => submitted(page)).toEqual(['apple']);
  });

  test('Enter submits when no item is highlighted', async ({ page, mode }) => {
    await goto(page, mode, { openOnInputClick: true });
    await watchSubmits(page);
    await input(page).click();
    await expect(options(page)).toHaveCount(3);
    await input(page).pressSequentially('zz');
    await expect(input(page)).toHaveValue('zz');

    await input(page).press('Enter');
    await expect.poll(() => submitted(page)).toEqual(['zz']);
  });

  test('closes popup on Tab after selecting with Enter and typing again', async ({ page, mode }) => {
    const fixture = await goto(page, mode);
    await typeText(page, 'a');
    await expect(options(page)).toHaveCount(2);
    await input(page).press('ArrowDown');
    await expect(page.getByTestId('item-apple')).toHaveAttribute('data-highlighted', '');
    await input(page).press('Enter');
    await expect(page.getByRole('listbox')).toHaveCount(0);

    await input(page).press('Backspace');
    await expect(page.getByRole('listbox')).toBeVisible();
    // On Server the session's update follows the reopen's render, and a focus-out before it is lost (with the circuit delayed
    // 150 ms, 3 of 3; PR 193's STA-21 window in the interactions module): the title waits for a round trip first.
    await settle(fixture);
    await input(page).press('Tab');
    await expect(page.getByRole('listbox')).toHaveCount(0);
    await expect(input(page)).toHaveValue('appl');
  });

  test('opens the list with the arrow keys but does not commit on item press when readOnly', async ({ page, mode }) => {
    const fixture = await goto(page, mode, { readOnly: true, defaultValue: 'a' });
    await input(page).focus();
    await input(page).press('ArrowDown');
    await expect(page.getByRole('listbox')).toBeVisible();

    await page.getByTestId('item-apple').click();
    await expect(input(page)).toHaveValue('a');
    expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
  });

  test('does not fill the input with the highlighted item when readOnly', async ({ page, mode }) => {
    await goto(page, mode, { readOnly: true, mode: 'both', defaultValue: 'a' });
    await input(page).focus();
    await input(page).press('ArrowDown');
    await expect(page.getByRole('listbox')).toBeVisible();
    await input(page).press('ArrowDown');
    await expect(page.locator('[role=option][data-highlighted]')).toHaveCount(1);

    await expect(input(page)).toHaveValue('a');
    await expect(input(page)).toHaveAttribute('aria-autocomplete', 'none');
  });

  test('calls onClick when clicked with a pointer', async ({ page, mode }) => {
    const fixture = await goto(page, mode);
    await openWithArrow(page);

    await page.getByTestId('item-banana').click();
    await expect.poll(() => recorded(fixture, 'data-item-clicks')).toEqual(['banana']);
    await expect(input(page)).toHaveValue('banana');
  });

  test('commits a keyboard selection, restores trigger focus, and preserves it on reopen', async ({ page, mode }) => {
    await goto(page, mode, { insidePopup: true });
    const trigger = page.getByTestId('trigger');
    await trigger.click();
    await expect(input(page)).toBeFocused();
    await input(page).pressSequentially('ch');
    await expect(options(page)).toHaveCount(1);
    await input(page).press('ArrowDown');
    await expect(page.getByTestId('item-cherry')).toHaveAttribute('data-highlighted', '');

    await input(page).press('Enter');
    await expect(page.getByRole('listbox')).toHaveCount(0);
    await expect(trigger).toBeFocused();
    await expect(trigger).toHaveText('cherry');

    await trigger.click();
    await expect(input(page)).toHaveValue('cherry');
  });

  test('resets the list scroll position to the top when typing', async ({ page, mode }) => {
    await goto(page, mode, { many: true });
    await typeText(page, 'item');
    const list = page.getByTestId('list');
    await expect(options(page)).toHaveCount(50);
    await list.evaluate((element) => {
      element.scrollTop = 40;
    });
    expect(await list.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);

    await input(page).pressSequentially(' 1');
    await expect.poll(() => list.evaluate((element) => element.scrollTop)).toBe(0);
  });

  test('mode="both": inline navigation does not reset the scroll to the top', async ({ page, mode }) => {
    const fixture = await goto(page, mode, { many: true, mode: 'both' });
    await typeText(page, 'item');
    await expect(options(page)).toHaveCount(50);
    // Every item matches the first keystroke, so the list is full before the typing settles: on Server the arrows wait for
    // the last typed change and a round trip (Combobox's typing relaxation, adopted).
    await expect.poll(() => recorded(fixture, 'data-value-changes')).toContain('item:input-change');
    await settle(fixture);

    for (let i = 0; i < 20; i += 1) {
      await input(page).press('ArrowDown');
    }
    await expect(input(page)).toHaveValue('item 19');
    await expect.poll(() => page.getByTestId('list').evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  });
});

// The attach order (the brief's L4 plan; the lesson of CI run 40461905812): the Input's `combobox` and list handles and the
// root's session each wait for their module, and a gesture after it arrives runs once. A route holds the module, matched
// with or without WebAssembly's fingerprint.
test.describe('<Autocomplete.Root /> attach order', () => {
  async function stall(page: Page, module: string): Promise<{ release: () => Promise<void>; held: () => number }> {
    const held: Route[] = [];
    let heldCount = 0;
    let released = false;
    const url = new RegExp(`/_content/Blazix\\.Primitives/js/${module}(\\.[a-z0-9]+)?\\.js$`);
    await page.route(url, (route) => {
      if (released) {
        return route.continue();
      }
      heldCount += 1;
      held.push(route);
      return undefined;
    });
    return {
      release: async () => {
        released = true;
        for (const route of held.splice(0)) {
          await route.continue();
        }
      },
      held: () => heldCount,
    };
  }

  for (const module of ['combobox', 'composite', 'interactions']) {
    test(`port: with js/${module}.js stalled the parts wait, and typing and a press run once the module arrives`, async ({ page, mode }) => {
      const { release, held } = await stall(page, module);
      const fixture = await gotoFixture(page, mode, 'autocomplete-root');
      // The module's request is held (on WebAssembly the fixture's marker can read attached before its parts' handles
      // start, so the hold itself is the witness).
      await expect.poll(held).toBeGreaterThan(0);

      await release();
      await expect(fixture).toHaveAttribute('data-attached', 'true');

      await typeText(page, 'ch');
      await expect(options(page)).toHaveCount(1);
      await page.getByTestId('item-cherry').click();
      await expect(input(page)).toHaveValue('cherry');
      await expect.poll(() => recorded(fixture, 'data-value-changes')).toEqual(['c:input-change', 'ch:input-change', 'cherry:item-press']);
    });
  }
});

// AUT-ANI-01, EVT-03, DIS-01: AutocompleteRoot.test.tsx's "manual unmount lifecycle" on `autocomplete-unmount`, the titles
// Combobox ports at L4 (combobox.root-a.spec.ts), each the same engine path under the Autocomplete parts. The other five of
// Base UI's twelve are L2 titles (AutocompleteLifecycleTests).
test.describe('<Autocomplete.Root /> manual unmount lifecycle', () => {
  // ADR 01 relaxation (CMB-STA-29, Server, adopted): a popup held mounted by `preventUnmountOnClose` carries the convergent
  // `hidden`, so Base UI's `queryByRole('listbox')` is a presence check that includes hidden elements.
  const mountedListbox = (target: Page) => target.getByRole('listbox', { includeHidden: true });
  const completedClosings = async (fixture: Locator) => (await openChangeCompletes(fixture)).filter((open) => open === 'false');
  const unmountFixture = (page: Page, mode: RenderMode, options: Record<string, string | boolean> = {}) =>
    gotoCombobox(page, mode, 'autocomplete-unmount', options);

  async function pressOption(option: Locator): Promise<void> {
    const point = await center(option);
    await option.page().mouse.click(point.x, point.y);
  }

  test('automatically unmounts with an actions ref and completes closing once', async ({ page, mode }) => {
    const fixture = await unmountFixture(page, mode);

    expect(await openChangeCompletes(fixture)).toEqual([]);
    await page.getByRole('combobox').click();
    await expect(page.getByRole('listbox')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(mountedListbox(page)).toHaveCount(0);
    await settle(fixture);
    expect(await completedClosings(fixture)).toHaveLength(1);

    await pressControl(fixture.locator('[data-unmount]'));
    await settle(fixture);
    expect(await completedClosings(fixture)).toHaveLength(1);
  });

  test('keeps the popup mounted until the unmount action completes closing', async ({ page, mode }) => {
    const fixture = await unmountFixture(page, mode, { defaultOpen: true, retainEveryClose: true });

    await pressOption(page.getByRole('option'));
    await settle(fixture);
    await expect(mountedListbox(page)).toHaveCount(1);
    expect(await openChangeCompletes(fixture)).not.toContain('false');
    await pressControl(fixture.locator('[data-unmount]'));
    await expect(mountedListbox(page)).toHaveCount(0);
    await settle(fixture);
    expect(await completedClosings(fixture)).toHaveLength(1);

    await pressControl(fixture.locator('[data-set-open="true"]'));
    await expect(page.getByRole('listbox')).toBeVisible();
    await pressControl(fixture.locator('[data-set-open="false"]'));
    await expect(mountedListbox(page)).toHaveCount(0);
    await settle(fixture);
    expect(await completedClosings(fixture)).toHaveLength(2);
  });

  test('clears the opt-out when a controlled reopen interrupts a pending unmount', async ({ page, mode }) => {
    const fixture = await unmountFixture(page, mode, { defaultOpen: true, retainEveryClose: true });

    await pressOption(page.getByRole('option'));
    await settle(fixture);
    await expect(mountedListbox(page)).toHaveCount(1);
    expect(await openChangeCompletes(fixture)).not.toContain('false');
    await pressControl(fixture.locator('[data-set-open="true"]'));
    await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'true');
    await pressControl(fixture.locator('[data-set-open="false"]'));
    await expect(mountedListbox(page)).toHaveCount(0);
    await settle(fixture);
    expect(await completedClosings(fixture)).toHaveLength(1);
  });

  test('ignores an opt-out on a canceled close', async ({ page, mode }) => {
    const fixture = await unmountFixture(page, mode, { defaultOpen: true, cancelFirst: true });

    await pressOption(page.getByRole('option'));
    await settle(fixture);
    await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'true');
    await expect(mountedListbox(page)).toHaveCount(1);

    await pressControl(fixture.locator('[data-set-cancel="false"]'));
    await pressOption(page.getByRole('option'));
    await expect(mountedListbox(page)).toHaveCount(0);
  });

  // Deviation (Combobox's, 2026-11-10): Base UI applies the controlled close in React.startTransition; Blazor has no
  // transitions, so the fixture applies it in the callback.
  test('keeps the opt-out when a controlled close is applied in a transition', async ({ page, mode }) => {
    const fixture = await unmountFixture(page, mode, { defaultOpen: true, retainEveryClose: true });

    await pressOption(page.getByRole('option'));
    await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    await expect(mountedListbox(page)).toHaveCount(1);
    expect(await openChangeCompletes(fixture)).not.toContain('false');

    await pressControl(fixture.locator('[data-unmount]'));
    await expect(mountedListbox(page)).toHaveCount(0);
    await settle(fixture);
    expect(await completedClosings(fixture)).toHaveLength(1);
  });

  test('closes through the `close` action so `onOpenChange` can opt out', async ({ page, mode }) => {
    const fixture = await unmountFixture(page, mode, { defaultOpen: true, retainEveryClose: true });
    await expect(page.getByRole('listbox')).toBeVisible();

    await pressControl(fixture.locator('[data-close]'));
    await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'false');
    await settle(fixture);
    expect(await openChanges(fixture)).toEqual(['false:imperative-action']);
    await expect(mountedListbox(page)).toHaveCount(1);

    await pressControl(fixture.locator('[data-unmount]'));
    await expect(mountedListbox(page)).toHaveCount(0);
  });

  test('completes closing once when `unmount` is called twice in one batch', async ({ page, mode }) => {
    const fixture = await unmountFixture(page, mode, { defaultOpen: true, retainEveryClose: true });

    await pressOption(page.getByRole('option'));
    await settle(fixture);
    await expect(mountedListbox(page)).toHaveCount(1);

    await pressControl(fixture.locator('[data-unmount-twice]'));
    await expect(mountedListbox(page)).toHaveCount(0);
    await settle(fixture);
    expect(await completedClosings(fixture)).toHaveLength(1);
  });
});

// AUT-FRM-02, EDGE-03: Base UI's `renderToString` titles on the prerendered `autocomplete-prerender`, its static markup read
// with the scripts removed (the NavigationMenu precedent).
test.describe('<Autocomplete.Root /> prerendered', () => {
  async function staticMarkup(page: Page, mode: RenderMode, layout: string): Promise<void> {
    const response = await page.request.get(`${fixtureUrl(mode, 'autocomplete-prerender')}?layout=${layout}`);
    await page.setContent((await response.text()).replace(/<script[\s\S]*?<\/script>/g, ''));
  }

  function named(page: Page, value: string): Promise<number> {
    return page.evaluate(
      (expected) => [...document.querySelectorAll('input')].filter((element) => element.getAttribute('name') === 'search' && element.value === expected).length,
      value,
    );
  }

  test('server-renders only the inline input name for native FormData', async ({ page, mode }) => {
    await staticMarkup(page, mode, 'inline');

    expect(await named(page, '')).toBe(1);
    await expect(page.getByTestId('input')).toHaveAttribute('name', 'search');
  });

  test('server-renders only the outside-popup input name for native FormData', async ({ page, mode }) => {
    await staticMarkup(page, mode, 'outside');

    expect(await named(page, '')).toBe(1);
    await expect(page.getByTestId('input')).toHaveAttribute('name', 'search');
  });

  test('server-renders the default popup input value without a form name before hydration', async ({ page, mode }) => {
    await staticMarkup(page, mode, 'popup');

    expect(await named(page, 'alpha')).toBe(0);
    await expect(page.getByTestId('input')).toHaveCount(0);
  });
});
