import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { gotoCombobox, recorded } from './combobox.helpers';

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
});
