import { delayCircuit, expect, gotoFixture, test, type Page } from './helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// M5 track 4 (issue 162): the shared list value layer's L4 titles on its test-only fixtures (`list-values-*`), both render
// modes unless a title is about one. Select, Combobox, Autocomplete and Menu's filter mode re-run the family rows on their
// own parts (ADR 13 Amendments 2026-10-22).

test.describe('form-participation input', () => {
  // Design point 4 of M5 track 4 (settled by the orchestrator 2026-11-04): the form module's control role with ReportAutofill
  // on the test-only host `list-values-hidden-input`. The autofill is the browser's: the page sets the value and dispatches
  // `change`, as an autofill does.
  async function autofill(page: Page, text: string): Promise<void> {
    await page.getByTestId('hidden-input').evaluate((element, value) => {
      (element as HTMLInputElement).value = value;
      element.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));
    }, text);
  }

  test('port: reports an autofilled text once and keeps the committed value when the root rejects it', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'list-values-hidden-input', { reject: 1 });
    const hidden = page.getByTestId('hidden-input');
    await expect(hidden).toHaveValue('apple');

    await autofill(page, 'Banana');

    await expect(fixture).toHaveAttribute('data-autofills', 'Banana');
    await expect(hidden).toHaveValue('apple');
    await expect(fixture).toHaveAttribute('data-committed', 'apple');
  });

  test('port: writes the matched value the root commits', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'list-values-hidden-input');
    const hidden = page.getByTestId('hidden-input');

    await autofill(page, 'Banana');

    await expect(fixture).toHaveAttribute('data-committed', 'banana');
    await expect(hidden).toHaveValue('banana');
    await expect(fixture).toHaveAttribute('data-autofills', 'Banana');
  });

  // ADR 01's relaxation row "SEL-FRM-18, CMB-EVT-11 (the autofill of a list root's hidden input matched to an item)"
  // (added 2026-11-04): the match is C#'s. On WebAssembly the report runs it synchronously, so the matched value is in the input when the change's
  // dispatch returns; on Server it lands a round trip later (the circuit delayed 75 ms each way here), and until then the
  // input holds the committed value the module put back inside the dispatch. A MutationObserver records when
  // data-committed changes (ADR 06 §2's recorded marker).
  test('port: the autofill match lands inside the change dispatch on WebAssembly and a round trip later on Server, where the input keeps the committed value until then', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, 75);
    }
    const fixture = await gotoPopupFixture(page, mode, 'list-values-hidden-input');
    const timing = await page.evaluate(() => new Promise<{ restored: string; elapsed: number }>((resolve) => {
      const container = document.querySelector('[data-fixture="list-values-hidden-input"]')!;
      const element = document.querySelector('[data-testid="hidden-input"]') as HTMLInputElement;
      let start = 0;
      let restored = '';
      new MutationObserver(() => {
        if (container.getAttribute('data-committed') === 'banana') {
          resolve({ restored, elapsed: performance.now() - start });
        }
      }).observe(container, { attributes: true, attributeFilter: ['data-committed'] });
      element.value = 'Banana';
      start = performance.now();
      element.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));
      restored = element.value;
    }));
    test.info().annotations.push({ type: 'autofill match', description: `${mode}: ${Math.round(timing.elapsed)} ms after the change` });

    if (mode === 'server') {
      expect(timing.restored).toBe('apple');
      expect(timing.elapsed).toBeGreaterThan(150);
    } else {
      expect(timing.restored).toBe('banana');
    }
    await expect(fixture).toHaveAttribute('data-committed', 'banana');
  });
});

test.describe('item indicator', () => {
  // RND-DOM-52, RND-ANI-03 (SelectItemIndicator.test.tsx; the family is incidental, ADR 13 Amendments 2026-10-22): the
  // test-only indicator over ItemIndicatorBehavior, kept mounted, with a 150 ms keyframe exit on `list-values-indicator`.
  test('settles out of its transition state after the item is deselected', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'list-values-indicator', { animations: 1 });
    const indicator = page.getByTestId('indicator');
    await expect(indicator).toHaveAttribute('data-selected');
    // The exit's animationstart, recorded with the ending style the indicator carried then (ADR 06 §2's recorded marker).
    await indicator.evaluate((element) => {
      const starts: boolean[] = [];
      (window as unknown as { indicatorExits: boolean[] }).indicatorExits = starts;
      element.addEventListener('animationstart', () => starts.push(element.hasAttribute('data-ending-style')));
    });

    await page.locator('[data-select="b"]').click();

    await expect(indicator).not.toHaveAttribute('data-selected');
    await expect.poll(() => page.evaluate(() => (window as unknown as { indicatorExits: boolean[] }).indicatorExits)).toEqual([true]);
    // A kept-mounted indicator must not be left advertising an exit transition once the deselection has finished.
    await expect(indicator).not.toHaveAttribute('data-ending-style');
    await expect(indicator).toHaveCount(1);
  });

  test('preserves the kept-mounted element across selection changes', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'list-values-indicator', { animations: 1 });
    const indicator = page.getByTestId('indicator');
    const element = await indicator.elementHandle();
    const same = () => element!.evaluate((node) => node === document.querySelector('[data-testid="indicator"]'));

    await page.locator('[data-select="b"]').click();
    await expect(indicator).not.toHaveAttribute('data-selected');
    expect(await same()).toBe(true);
    await expect(indicator).not.toHaveAttribute('data-ending-style');

    await page.locator('[data-select="a"]').click();
    await expect(indicator).toHaveAttribute('data-selected');
    expect(await same()).toBe(true);
  });
});

test.describe('collation', () => {
  // UTL-I18N-09..11 and ADR 01 decision 2's note of 2026-11-04: a subset of the L1 collation table
  // (tests/Blazix.Primitives.Tests/ListValues/Data/collation-table.json) run by the collator filter in the page's runtime, as
  // `contains,startsWith,endsWith`. The expected answers are the L1 table's, Intl.Collator's.
  const subset: Record<string, string> = {
    'en-US|Résumé|resume': 'true,true,true',
    'en-US|Résumé|resume': 'false,false,false',
    'en-US|Jean-Luc|jean luc': 'true,true,true',
    'de-DE|Straße|strasse': 'false,true,false',
    'en-US|Æther|aether': 'false,true,false',
    'tr-TR|İstanbul|istanbul': 'true,true,true',
    'tr-TR|ISPARTA|isparta': 'false,false,false',
    'en-US|ＡＢＣ|abc': 'true,true,true',
    'ja-JP|カタカナ|かたかな': 'true,true,true',
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
    await expect(fixture.locator('[data-row="ja-JP|カタカナ|かたかな"]')).toHaveText('true,true,true (not predefined)');
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
