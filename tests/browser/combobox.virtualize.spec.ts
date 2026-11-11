import { expect, test } from './helpers';
import { center, gotoCombobox, recorded } from './combobox.helpers';
import { settle } from './suites/popupFixture';

// Issue 29 (ADR 14's ADR 16 item, Amendments 2026-11-03; design point 1 (a); CMB-API-22), M5 track 10: Combobox's `virtualized`
// path with Blazor's `Virtualize` on both render modes, on the `combobox-virtualize` fixture
// (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxVirtualizeL4Fixture.razor): 1,000 items, a window rendered by
// `Virtualize` over the consumer's filtered items, each an Item without `Index`, and the consumer's reveal through
// `ScrollToItemAsync` driven by `OnItemHighlighted`.

test('@verifies ADR-16/5 Combobox virtualized with Blazor Virtualize: keyboard across the window, the reveal, a selection and the form value', async ({ page, mode }, testInfo) => {
  const fixture = await gotoCombobox(page, mode, 'combobox-virtualize');
  const field = page.getByTestId('input');
  const list = page.getByTestId('list');

  await field.click();
  await expect(page.getByRole('listbox')).toBeVisible();
  await center(page.getByRole('listbox'));
  const windowSize = await page.getByRole('option').count();
  expect(windowSize).toBeLessThan(40);

  // Keyboard navigation past the rendered window: every ArrowDown moves the highlight one item, and the consumer reveals it.
  const target = windowSize + 10;
  const started = Date.now();
  for (let step = 0; step <= target; step += 1) {
    await page.keyboard.press('ArrowDown');
  }

  const label = `item ${String(target).padStart(3, '0')}`;
  const revealed = page.getByRole('option', { name: label, exact: true });
  await expect(revealed).toHaveAttribute('data-highlighted', '');
  const lag = Date.now() - started;
  await expect(field).toHaveAttribute('aria-activedescendant', (await revealed.getAttribute('id'))!);
  expect(await list.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  await settle(fixture);
  expect((await recorded(fixture, 'data-reveals')).at(-1)).toBe(String(target));
  // Server's reveal lag: each reveal is a round trip after its key, recorded rather than bounded.
  testInfo.annotations.push({ type: 'reveal-lag-ms', description: `${mode}: ${lag} ms for ${target + 1} keys` });

  await page.keyboard.press('Enter');
  await expect(field).toHaveValue(label);
  await expect(page.getByRole('listbox')).toHaveCount(0);
  await expect(page.locator('input[name="fruit"]')).toHaveValue(label);
});
