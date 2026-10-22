import { test, expect, fixtureUrl, gotoFixture } from './helpers';

// The [L4] rows of docs/components/fieldset.md and FFI-ARIA-08/FFI-FRM-34 on Fieldset parts (PR 1a). Titles are Base UI's
// (FieldsetRoot.test.tsx, FieldsetLegend.test.tsx, useRegisteredLabelId.test.tsx). The legend publishes its id while it
// initializes, so the association lands in the legend's own render batch on both modes and is in prerendered markup
// (ADR 01 Amendments 2026-10-22, the port note on FST-DOM-08).
test.describe('Fieldset', () => {
  // FST-FRM-02: the browser disables every native control inside a disabled <fieldset>.
  test('sets the native disabled attribute', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'fieldset-basic');
    const scope = fixture.locator('[data-case="native"]');

    await expect(scope.locator('[data-testid="fieldset"]')).toHaveAttribute('disabled', '');
    await expect(scope.locator('[data-testid="input"]')).toBeDisabled();
  });

  test('should set aria-labelledby on the fieldset automatically', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'fieldset-basic');
    const scope = fixture.locator('[data-case="auto"]');

    const id = await scope.locator('[data-testid="legend"]').getAttribute('id');
    expect(id).toBeTruthy();
    await expect(scope.getByRole('group')).toHaveAttribute('aria-labelledby', id!);
  });

  test('should set aria-labelledby on the fieldset with custom id', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'fieldset-basic');

    await expect(fixture.locator('[data-case="custom"]').getByRole('group')).toHaveAttribute('aria-labelledby', 'legend-id');
  });

  test('updates and clears the legend association', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'fieldset-basic');
    const scope = fixture.locator('[data-case="association"]');
    const group = scope.getByRole('group');

    await expect(group).toHaveAttribute('aria-labelledby', 'legend-a');
    await scope.locator('[data-change-id]').click();
    await expect(group).toHaveAttribute('aria-labelledby', 'legend-b');
    await scope.locator('[data-remove-legend]').click();
    await expect(group).not.toHaveAttribute('aria-labelledby');
  });

  // FFI-ARIA-08, FST-EDGE-03: the last publication wins, and the older legend's withdrawal leaves the newer id.
  test('does not let an older label cleanup clear a newer label', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'fieldset-basic');
    const scope = fixture.locator('[data-case="two-legends"]');
    const group = scope.getByRole('group');

    await expect(group).toHaveAttribute('aria-labelledby', 'old-label');
    await scope.locator('[data-both]').click();
    await expect(group).toHaveAttribute('aria-labelledby', 'new-label');
    await scope.locator('[data-new]').click();
    await expect(scope.getByText('Old')).toHaveCount(0);
    await expect(group).toHaveAttribute('aria-labelledby', 'new-label');
  });

  test('does not set `aria-labelledby` during SSR when legend is absent', async ({ page, mode }) => {
    const html = await (await page.request.get(fixtureUrl(mode, 'fieldset-prerender'))).text();
    const absent = html.match(/<fieldset[^>]*data-testid="absent"[^>]*>/)?.[0];

    expect(absent).toBeTruthy();
    expect(absent).not.toContain('aria-labelledby');
  });

  // FST-DOM-05 (the generated id is the same in the server markup and after the runtime takes over) and FST-DOM-08 as
  // the port reads it: the association is already in the static response.
  test('sets `aria-labelledby` after hydration without a custom legend id', async ({ page, mode }) => {
    const html = await (await page.request.get(fixtureUrl(mode, 'fieldset-prerender'))).text();
    const legendId = html.match(/<div[^>]*data-testid="legend"[^>]*>/)?.[0].match(/id="([^"]+)"/)?.[1];
    expect(legendId).toBeTruthy();
    expect(html).toMatch(new RegExp(`<fieldset[^>]*aria-labelledby="${legendId}"`));

    const fixture = await gotoFixture(page, mode, 'fieldset-prerender');
    await expect(fixture.locator('[data-testid="legend"]')).toHaveAttribute('id', legendId!);
    await expect(fixture.locator('[data-testid="fieldset"]')).toHaveAttribute('aria-labelledby', legendId!);
  });
});
