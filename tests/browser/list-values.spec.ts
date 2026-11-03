import { expect, gotoFixture, test } from './helpers';

// M5 track 4 (issue 162): the shared list value layer's L4 titles on its test-only fixtures (`list-values-*`), both render
// modes unless a title is about one. Select, Combobox, Autocomplete and Menu's filter mode re-run the family rows on their
// own parts (ADR 13 Amendments 2026-10-22).

test.describe('allocation', () => {
  // ADR 16 rule 7, ADR 05 Amendments 2026-10-20 (the M5 re-measure of Is<T> on the WebAssembly interpreter): the WebAssembly
  // half of the item equality's and the collator filter's allocation probe (tests/Blazix.Primitives.Tests/ListValues/ holds
  // the CoreCLR half, 0 bytes for every type). A scenario is one operation over 300 items, `<type>/<operation>`; the filter's
  // round is its three predicates over 1,000 item texts.
  const operations = ['compare', 'find-single', 'find-multiple', 'includes', 'dirty'];

  test('port: the item equality and the collator filter allocate nothing for an int, a string, an enum and a record class on the interpreter', async ({ page, mode }) => {
    test.skip(mode !== 'wasm', 'the probe is about the WebAssembly interpreter; Server runs CoreCLR');
    const probe = await gotoFixture(page, mode, 'list-values-allocation');

    await probe.locator('[data-measure]').click();

    await expect(probe).toHaveAttribute('data-rounds', /^[1-9]\d*$/, { timeout: 120_000 });
    test.info().annotations.push({ type: 'bytes per round', description: (await probe.getAttribute('data-scenarios')) ?? '' });
    for (const type of ['int', 'string', 'enum', 'record-class']) {
      for (const operation of operations) {
        await expect(probe.locator(`[data-scenario="${type}/${operation}"]`)).toHaveText('0');
      }
    }
    await expect(probe.locator('[data-scenario="filter"]')).toHaveText('0');
  });

  // A record struct still compares through EqualityComparer<T> (ADR 05 Amendments 2026-10-20, "Is<T> on the WebAssembly
  // interpreter", as amended 2026-10-23): measured 2026-11-04, 192 bytes per comparison pair and 19,232 per single lookup
  // over 300 items. The title pins the profile, so a change of the platform turns it red.
  test('port: the item equality boxes a record struct on the interpreter', async ({ page, mode }) => {
    test.skip(mode !== 'wasm', 'the probe is about the WebAssembly interpreter; Server runs CoreCLR');
    const probe = await gotoFixture(page, mode, 'list-values-allocation');

    await probe.locator('[data-measure]').click();

    await expect(probe).toHaveAttribute('data-rounds', /^[1-9]\d*$/, { timeout: 120_000 });
    await expect(probe.locator('[data-scenario="record-struct/find-single"]')).not.toHaveText('0');
  });
});
