import { expect, test } from './helpers';
import { center, gotoCombobox } from './combobox.helpers';

// CMB-FRM-05's "prop: validationMode=onSubmit" (ComboboxRoot.test.tsx, describe 'Field'; M5 track 10, issue 174; ported by
// session S): Base UI's title verbatim, at L4 on both render modes, on the `combobox-form` fixture
// (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxFormL4Fixture.razor). Its steps are keyboard navigation through
// the list and the Clear's focus return, which bUnit cannot drive.

test.describe('<Combobox.Root />', () => {
  test.describe('Field', () => {
    // CMB-FRM-05
    test('prop: validationMode=onSubmit', async ({ page, mode }) => {
      await gotoCombobox(page, mode, 'combobox-form');
      const input = page.getByTestId('input');
      await expect(input).not.toHaveAttribute('aria-invalid');

      await page.getByText('submit').click();
      await expect(input).toHaveAttribute('aria-invalid', 'true');

      await input.click();
      // On Server the open lands a round trip after the click: the keys wait for the positioned list, as the other keyboard
      // titles do.
      await expect(page.getByRole('listbox')).toBeVisible();
      await center(page.getByRole('listbox'));

      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('Enter');

      await expect(input).not.toHaveAttribute('aria-invalid');

      const clear = page.getByTestId('clear');
      await clear.click();

      await expect(input).toBeFocused();
      await page.keyboard.press('Tab');

      await expect(input).toHaveAttribute('aria-invalid', 'true');
    });
  });
});
