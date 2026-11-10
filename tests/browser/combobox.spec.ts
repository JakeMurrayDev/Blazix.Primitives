import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// The [L4] rows of docs/components/combobox.md (M5 track 10, issue 174) on the `combobox-*` fixtures, every title on both
// render modes. A title waits on the page's `data-attached` (the session's and the input's handles) before its first
// gesture, and reads the root's recorded callbacks from the container's attributes.

async function gotoCombobox(page: Page, mode: RenderMode, options: Record<string, string | boolean | undefined> = {}): Promise<Locator> {
  const fixture = await gotoPopupFixture(page, mode, 'combobox-root', options);
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

function input(page: Page): Locator {
  return page.getByTestId('input');
}

test.describe('Combobox typing and the list', () => {
  test('port: typing a query opens the popup and filters the list', async ({ page, mode }) => {
    const fixture = await gotoCombobox(page, mode);
    await input(page).click();
    await input(page).pressSequentially('an');

    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(page.getByRole('option')).toHaveText(['banana']);
    await expect(input(page)).toHaveValue('an');
    await expect(fixture).toHaveAttribute('data-input-changes', /an:input-change/);
  });

  test('port: ArrowDown highlights the first item through aria-activedescendant while focus stays on the input', async ({ page, mode }) => {
    await gotoCombobox(page, mode);
    await input(page).click();
    // On Server the click's open lands a round trip after the click, so the arrow key waits for the open list, as the
    // Enter title's waits for the filtered one.
    await expect(page.getByRole('listbox')).toBeVisible();
    await input(page).press('ArrowDown');

    const first = page.getByRole('option', { name: 'apple' });
    await expect(first).toHaveAttribute('data-highlighted', '');
    await expect(input(page)).toHaveAttribute('aria-activedescendant', (await first.getAttribute('id'))!);
    await expect(input(page)).toBeFocused();
  });

  test('port: Enter selects the highlighted item, fills the input and closes the popup', async ({ page, mode }) => {
    const fixture = await gotoCombobox(page, mode);
    await input(page).click();
    await input(page).pressSequentially('ch');
    // On Server the typed open and the filter arrive a round trip after the keystrokes (CMB §9's typing row), so the
    // arrow key waits for the filtered list.
    await expect(page.getByRole('option')).toHaveText(['cherry']);
    await input(page).press('ArrowDown');
    await expect(page.getByRole('option', { name: 'cherry' })).toHaveAttribute('data-highlighted', '');
    await input(page).press('Enter');

    await expect(page.getByRole('listbox')).toBeHidden();
    await expect(input(page)).toHaveValue('cherry');
    await expect(fixture).toHaveAttribute('data-value-changes', 'cherry:item-press');
  });

  test('port: a click on an item selects it and keeps the focus on the input', async ({ page, mode }) => {
    const fixture = await gotoCombobox(page, mode);
    await input(page).click();
    await page.getByRole('option', { name: 'date' }).click();

    await expect(input(page)).toHaveValue('date');
    await expect(fixture).toHaveAttribute('data-value-changes', 'date:item-press');
    await expect(input(page)).toBeFocused();
  });

  test('port: Escape closes the open popup, and a second Escape clears the selection', async ({ page, mode }) => {
    const fixture = await gotoCombobox(page, mode, { defaultValue: 'banana' });
    await expect(input(page)).toHaveValue('banana');
    await input(page).click();
    await expect(page.getByRole('listbox')).toBeVisible();
    await input(page).press('Escape');
    await expect(page.getByRole('listbox')).toBeHidden();
    await input(page).press('Escape');

    await expect(input(page)).toHaveValue('');
    await expect(fixture).toHaveAttribute('data-value-changes', 'null:escape-key');
  });
});
