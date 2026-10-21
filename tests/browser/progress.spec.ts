import { test, expect, gotoFixture, fixtureUrl } from './helpers';

// docs/components/progress.md has no [L4] row: every row is C#-owned and tested at L2. This spec is the browser clauses the
// document names (PRG-DOM-03 and PRG-DOM-04 cite ProgressIndicator.test.tsx "determinate", "sets zero width when value is 0"
// and "indeterminate", computed styles only a browser resolves), and the port's own check on both render modes of what C#
// writes along the status cycle and for the label association (the PRG-ARIA-07 row: ADR 13 §1 asserts every relaxation row
// per mode). Titles are Base UI's. Readiness markers, no sleeps.
test.describe('Progress', () => {
  test('determinate', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'progress-basic');

    const indicator = fixture.locator('[data-case="determinate"] [data-testid="indicator"]');

    // The indicator is a span (render={<span />}): the width does not apply to an inline box, so the computed value is the
    // declared one, as in Base UI's test.
    await expect(indicator).toHaveCSS('inset-inline-start', '0px');
    await expect(indicator).toHaveCSS('width', '33%');
  });

  test('sets zero width when value is 0', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'progress-basic');

    const indicator = fixture.locator('[data-case="zero"] [data-testid="indicator"]');

    await expect(indicator).toHaveCSS('inset-inline-start', '0px');
    await expect(indicator).toHaveCSS('width', '0px');
  });

  test('indeterminate', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'progress-basic');

    const indicator = fixture.locator('[data-case="indeterminate"] [data-testid="indicator"]');

    // No built-in declaration at all (PRG-DOM-04): no style attribute, and the position stays the browser's default.
    await expect(indicator).not.toHaveAttribute('style');
    await expect(indicator).toHaveCSS('inset-inline-start', 'auto');
    await expect(indicator).toHaveAttribute('data-indeterminate', '');
  });

  test('keeps every composed part synchronized through the status cycle', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'progress-basic');
    const cycle = fixture.locator('[data-case="cycle"]');
    const parts = ['root', 'label', 'value', 'track', 'indicator'].map((id) => cycle.locator(`[data-testid="${id}"]`));
    const root = cycle.locator('[data-testid="root"]');
    const value = cycle.locator('[data-testid="value"]');
    const indicator = cycle.locator('[data-testid="indicator"]');

    async function expectStatus(status: 'indeterminate' | 'progressing' | 'complete') {
      for (const part of parts) {
        for (const name of ['indeterminate', 'progressing', 'complete']) {
          if (name === status) {
            await expect(part).toHaveAttribute(`data-${name}`, '');
          } else {
            await expect(part).not.toHaveAttribute(`data-${name}`);
          }
        }
        await expect(part).not.toHaveAttribute('data-status');
      }
    }

    await expectStatus('indeterminate');
    await expect(root).not.toHaveAttribute('aria-valuenow');
    await expect(root).toHaveAttribute('aria-valuetext', 'indeterminate progress');
    await expect(value).toBeEmpty();
    await expect(indicator).not.toHaveAttribute('style');

    await cycle.locator('[data-set-50]').click();
    await expectStatus('progressing');
    await expect(root).toHaveAttribute('aria-valuenow', '50');
    await expect(value).toHaveText('50%');
    await expect(indicator).toHaveAttribute('style', /width: 50%/);

    await cycle.locator('[data-set-100]').click();
    await expectStatus('complete');
    await expect(root).toHaveAttribute('aria-valuenow', '100');
    await expect(value).toHaveText('100%');
    await expect(indicator).toHaveAttribute('style', /width: 100%/);

    await cycle.locator('[data-set-null]').click();
    await expectStatus('indeterminate');
    await expect(root).not.toHaveAttribute('aria-valuenow');
    await expect(value).toBeEmpty();
    await expect(indicator).not.toHaveAttribute('style');
  });

  // PRG-ARIA-07 (FFI-ARIA-08, the "FFI registration" row of ADR 01): the port registers the label while its parameters are
  // set, in the render batch that creates, re-ids or removes it, so Server and WebAssembly meet the row alike.
  test('updates and clears the progress bar label association', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'progress-basic');
    const root = fixture.locator('[data-case="label"] [data-testid="root"]');

    await expect(root).toHaveAttribute('aria-labelledby', 'label-a');
    await expect(fixture.locator('[data-case="label"] [data-testid="label"]')).toHaveAttribute('id', 'label-a');

    await fixture.locator('[data-case="label"] [data-change-id]').click();
    await expect(root).toHaveAttribute('aria-labelledby', 'label-b');
    await expect(fixture.locator('[data-case="label"] [data-testid="label"]')).toHaveAttribute('id', 'label-b');

    await fixture.locator('[data-case="label"] [data-remove-label]').click();
    await expect(root).not.toHaveAttribute('aria-labelledby');
  });

  // PRG-EDGE-06 as the port reads it (docs §14): the label publishes while its parameters are set, in the render that creates
  // it, so the static response of a prerendered page already carries the root's aria-labelledby, and the page keeps it
  // after the runtime takes over.
  test('carries aria-labelledby in the server-rendered markup', async ({ page, mode }) => {
    const html = await (await page.request.get(fixtureUrl(mode, 'progress-prerender'))).text();

    expect(html).toContain('role="progressbar"');
    expect(html).toContain('aria-labelledby="upload-label"');
    expect(html).toContain('aria-valuetext="42%"');
    expect(html).toContain('data-progressing=""');

    const fixture = await gotoFixture(page, mode, 'progress-prerender');
    await expect(fixture.locator('[data-testid="root"]')).toHaveAttribute('aria-labelledby', 'upload-label');
  });
});
