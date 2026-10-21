import { test, expect, gotoFixture, fixtureUrl } from './helpers';

// docs/components/meter.md has no [L4] row: every row is C#-owned and tested at L2. This spec is the one browser clause the
// document names (MTR-DOM-03 cites MeterIndicator.test.tsx "sets positioning styles" and "sets zero width when value is 0",
// "browser: left: 0px, width: 33px in a 100 px root", computed pixels only a browser resolves), and the port's own check on
// both render modes of what C# writes for a re-render and for the label association (the MTR-ARIA-07 row: ADR 13 §1 asserts
// every relaxation row per mode). Titles are Base UI's. Readiness markers, no sleeps.
test.describe('Meter', () => {
  test('sets positioning styles', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'meter-basic');

    const indicator = fixture.locator('[data-case="positioning"] [data-testid="indicator"]');

    await expect(indicator).toHaveCSS('left', '0px');
    await expect(indicator).toHaveCSS('width', '33px');
  });

  test('sets zero width when value is 0', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'meter-basic');

    const indicator = fixture.locator('[data-case="zero"] [data-testid="indicator"]');

    await expect(indicator).toHaveCSS('inset-inline-start', '0px');
    await expect(indicator).toHaveCSS('width', '0px');
  });

  test('refreshes aria-valuenow, aria-valuetext, the value text, and the indicator when value changes', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'meter-basic');
    const meter = fixture.locator('[data-case="value"] [data-testid="meter"]');
    const value = fixture.locator('[data-case="value"] [data-testid="value"]');
    const indicator = fixture.locator('[data-case="value"] [data-testid="indicator"]');

    await expect(meter).toHaveAttribute('aria-valuenow', '50');
    await expect(meter).toHaveAttribute('aria-valuetext', '50%');
    await expect(value).toHaveText('50%');
    await expect(indicator).toHaveAttribute('style', /width: 50%/);

    await fixture.locator('[data-case="value"] [data-set-value]').click();

    await expect(meter).toHaveAttribute('aria-valuenow', '77');
    await expect(meter).toHaveAttribute('aria-valuetext', '77%');
    await expect(value).toHaveText('77%');
    await expect(indicator).toHaveAttribute('style', /width: 77%/);
  });

  // MTR-ARIA-07 (FFI-ARIA-08, the "FFI registration" row of ADR 01): the port registers the label while it initializes, in
  // the render batch that creates it, so Server and WebAssembly meet the row alike: the change reaches the root's
  // attribute in the same batch as the label's own.
  test('updates and clears the meter label association', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'meter-basic');
    const meter = fixture.locator('[data-case="label"] [data-testid="meter"]');

    await expect(meter).toHaveAttribute('aria-labelledby', 'label-a');
    await expect(fixture.locator('[data-case="label"] [data-testid="label"]')).toHaveAttribute('id', 'label-a');

    await fixture.locator('[data-case="label"] [data-change-id]').click();
    await expect(meter).toHaveAttribute('aria-labelledby', 'label-b');
    await expect(fixture.locator('[data-case="label"] [data-testid="label"]')).toHaveAttribute('id', 'label-b');

    await fixture.locator('[data-case="label"] [data-remove-label]').click();
    await expect(meter).not.toHaveAttribute('aria-labelledby');
  });

  // MTR-EDGE-06 as the port reads it (docs §14): the label publishes while it initializes, in the render that creates it,
  // so the static response of a prerendered page already carries the root's aria-labelledby, and the page keeps it after
  // the runtime takes over.
  test('carries aria-labelledby in the server-rendered markup', async ({ page, mode }) => {
    const html = await (await page.request.get(fixtureUrl(mode, 'meter-prerender'))).text();

    expect(html).toContain('role="meter"');
    expect(html).toContain('aria-labelledby="battery-label"');
    expect(html).toContain('aria-valuetext="42%"');

    const fixture = await gotoFixture(page, mode, 'meter-prerender');
    await expect(fixture.locator('[data-testid="meter"]')).toHaveAttribute('aria-labelledby', 'battery-label');
  });
});
