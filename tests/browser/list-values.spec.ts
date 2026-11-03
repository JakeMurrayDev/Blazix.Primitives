import { expect, gotoFixture, test } from './helpers';

// M5 track 4 (issue 162): the shared list value layer's L4 titles on its test-only fixtures (`list-values-*`), both render
// modes unless a title is about one. Select, Combobox, Autocomplete and Menu's filter mode re-run the family rows on their
// own parts (ADR 13 Amendments 2026-10-22).

test.describe('collation', () => {
  // UTL-I18N-09..11 and ADR 01 decision 2's note of 2026-11-04: a subset of the L1 collation table
  // (tests/Blazix.Primitives.Tests/ListValues/Data/collation-table.json) run by the collator filter in the page's runtime, as
  // `contains,startsWith,endsWith`. The expected answers are the L1 table's: Intl.Collator's, except full-width letters and
  // kana, which CompareInfo's default options compare exactly.
  const subset: Record<string, string> = {
    'en-US|Résumé|resume': 'true,true,true',
    'en-US|Résumé|resume': 'false,false,false',
    'en-US|Jean-Luc|jean luc': 'true,true,true',
    'de-DE|Straße|strasse': 'false,true,false',
    'en-US|Æther|aether': 'false,true,false',
    'tr-TR|İstanbul|istanbul': 'true,true,true',
    'tr-TR|ISPARTA|isparta': 'false,false,false',
    'en-US|ＡＢＣ|abc': 'false,false,false',
    'ja-JP|カタカナ|かたかな': 'false,false,false',
  };

  // WebAssembly loads the ICU data of the application's culture group (icudt_EFIGS.dat for en-US: English, French, Italian,
  // German and Spanish), so tr-TR and ja-JP are not predefined there; Server runs the host's ICU, which has them.
  const loaded = (mode: string, key: string) => mode === 'server' || !/^(tr-TR|ja-JP)\|/.test(key);

  test('port: the collator filter answers the collation table on both modes for every culture the runtime has data for', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'list-values-collation');

    for (const [key, expected] of Object.entries(subset).filter(([key]) => loaded(mode, key))) {
      await expect(fixture.locator(`[data-row="${key}"]`)).toHaveText(expected);
    }
  });

  // A culture outside the loaded ICU data is not predefined on WebAssembly: `new CultureInfo("tr-TR")` succeeds and compares
  // under the root collation, so the dotless capital I matches `i` there, as under en-US (ADR 01's note: load the culture's
  // data, for example with BlazorWebAssemblyLoadAllGlobalizationData, to keep its tailoring).
  test('port: on WebAssembly a culture outside the loaded ICU data compares under the root collation', async ({ page, mode }) => {
    test.skip(mode !== 'wasm', 'the ICU data of WebAssembly; Server runs the host ICU');
    const fixture = await gotoFixture(page, mode, 'list-values-collation');

    await expect(fixture.locator('[data-row="tr-TR|ISPARTA|isparta"]')).toHaveText('true,true,true (not predefined)');
    await expect(fixture.locator('[data-row="ja-JP|カタカナ|かたかな"]')).toHaveText('false,false,false (not predefined)');
  });
});

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
