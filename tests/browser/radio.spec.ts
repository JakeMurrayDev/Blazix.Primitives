import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/radio.md that RadioRoot.test.tsx and RadioIndicator.test.tsx title, and the L4 clauses
// of its L2 rows, on RadioRoot over the `button` module's hidden-input bridge and the `dom` primitive's `checked`, and on
// RadioIndicator over the `transitions` module. The RadioGroup.test.tsx titles that prove Radio rows are in
// radio-group.spec.ts. "port:" titles assert the per-mode rows of ADR 01's relaxation table (the C#-origin consumer vetoes)
// and the native-button clauses (RAD-KEY-02, RAD-EDGE-09). The fixtures record OnValueChange and the consumer's clicks as
// data attributes. Animated cases open with `?animations=1` (ADR 06 §2) and witness transient attributes by recorded
// markers, never by frame polls.

type FixtureName = 'radio-standalone' | 'radio-native-button' | 'radio-indicator';

/** Opens a Radio fixture and waits until every radio, group and listed indicator has its behaviors attached. */
async function gotoRadios(page: Page, mode: RenderMode, name: FixtureName, options: { animations?: boolean } = {}): Promise<Locator> {
  const fixture = options.animations ? await gotoAnimated(page, mode, name) : await gotoFixture(page, mode, name);
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

async function gotoAnimated(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
  await page.goto(`${fixtureUrl(mode, name)}?animations=1`, { waitUntil: 'commit' });
  await page.locator('html[data-blazix-started]').waitFor({ state: 'attached' });
  await page.locator(`[data-fixture="${name}"][data-interactive="true"]`).first().waitFor({ state: 'attached' });
  return page.locator(`[data-fixture="${name}"]`);
}

/**
 * A barrier: clicks the fixture's sync button and waits for C# to count it. Server processes one circuit's events in
 * order (ADR-04/1) and WebAssembly synchronously, so every event dispatched before has reached C# afterwards.
 */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

function radioCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    radio: container.locator('[data-radio]').first(),
    radioOf: (value: string) => container.locator(`[data-radio][data-value="${value}"]`),
    input: container.locator('input[type="radio"]').first(),
    ancestor: container.locator('[data-ancestor]'),
    label: container.locator('[data-label]'),
    toggle: container.locator('[data-toggle]'),
    select: container.locator('[data-select]'),
    field: container.locator('[data-field]'),
    indicator: (name: string) => container.locator(`[data-indicator="${name}"]`),
  };
}

/** Counts in the page the clicks an element observes, in its `data-observed-clicks`. */
async function countClicks(element: Locator): Promise<void> {
  await element.evaluate((node) => {
    node.addEventListener('click', () => {
      const dataset = (node as HTMLElement).dataset;
      dataset.observedClicks = String(Number(dataset.observedClicks ?? '0') + 1);
    });
  });
}

/** Moves focus as a script does, without a pointer. */
async function focus(element: Locator): Promise<void> {
  await element.evaluate((node) => (node as HTMLElement).focus());
}

test.describe('<Radio.Root />', () => {
  test('port: a standalone radio whose value is the empty string is checked, and a click keeps it checked', async ({ page, mode }) => {
    // RAD-STA-01: outside a group the radio is checked when its value is the empty string, Base UI's context default;
    // RAD-STA-02: interaction changes nothing and the hidden input is re-synchronized.
    const fixture = await gotoRadios(page, mode, 'radio-standalone');
    const { radio, input } = radioCase(fixture, 'empty');
    await expect(radio).toHaveAttribute('aria-checked', 'true');
    await expect(input).toHaveJSProperty('checked', true);

    await radio.click();
    await settle(fixture);

    await expect(radio).toHaveAttribute('aria-checked', 'true');
    await expect(input).toHaveJSProperty('checked', true);
  });

  test('associates `id` with the native button when `nativeButton=true`', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-native-button');
    const { container, label, radioOf } = radioCase(fixture, 'native');
    const radioA = radioOf('a');

    await expect(radioA).toHaveAttribute('id', 'myRadio');
    const sibling = radioA.locator('xpath=following-sibling::*[1]');
    await expect(sibling).toHaveJSProperty('tagName', 'INPUT');
    await expect(sibling).not.toHaveAttribute('id', 'myRadio');

    await expect(radioA).toHaveAttribute('aria-checked', 'false');
    await label.click();
    await expect(radioA).toHaveAttribute('aria-checked', 'true');
    await expect(container).toHaveAttribute('data-changes', '1');
  });

  test('port: Enter on a native-button radio does not select it, and Space does', async ({ page, mode }) => {
    // RAD-KEY-02 (native clause: the prevented keydown cancels native activation), RAD-KEY-03 (Space is the button's).
    const fixture = await gotoRadios(page, mode, 'radio-native-button');
    const { container, radioOf } = radioCase(fixture, 'native');
    const radioA = radioOf('a');

    await focus(radioA);
    await page.keyboard.press('Enter');
    await settle(fixture);
    await expect(radioA).toHaveAttribute('aria-checked', 'false');
    await expect(container).toHaveAttribute('data-changes', '0');

    await page.keyboard.press('Space');
    await expect(radioA).toHaveAttribute('aria-checked', 'true');
    await expect(container).toHaveAttribute('data-changes', '1');
  });

  test('port: a native-button radio wrapped in a label by a render callback is selected by a click on the label', async ({ page, mode }) => {
    // RAD-EDGE-09 (the label-click clause): the hidden input stays outside the label; the label activates the button.
    // Design point R9 of M2 tracks 7-8 (pending the user): the `button` module finds the input as the element's next
    // sibling (ADR 04 Amendments 2026-10-22), and here it is the label's, so the bridge finds none.
    test.fail(true, 'design point R9 (pending the user): the hidden-input bridge looks only at the next sibling of the element');
    const fixture = await gotoRadios(page, mode, 'radio-native-button');
    const { container, radio } = radioCase(fixture, 'wrapped');
    const label = container.locator('[data-wrapping-label]');

    await expect(label.locator('input')).toHaveCount(0);
    await expect(container.locator('input[type="radio"]')).toHaveCount(1);

    await label.click({ position: { x: (await label.boundingBox())!.width - 2, y: 4 } });

    await expect(radio).toHaveAttribute('aria-checked', 'true');
    await expect(container.locator('input[type="radio"]')).toHaveJSProperty('checked', true);
    await expect(container).toHaveAttribute('data-changes', '1');
  });

  test('sets `aria-labelledby` from a sibling label associated with the hidden input', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-standalone');
    const { radio, label } = radioCase(fixture, 'labelled');

    await expect(label).toHaveAttribute('id', /.+/);
    const labelId = await label.getAttribute('id');
    await expect(radio).toHaveAttribute('aria-labelledby', labelId!);
    await expect(radio).toHaveAccessibleName('Label');
  });

  test('updates fallback `aria-labelledby` when the hidden input id changes', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-standalone');
    const { container, radio, toggle } = radioCase(fixture, 'id-change');
    const labelA = container.locator('[data-label-a]');
    const labelB = container.locator('[data-label-b]');

    await expect(labelA).toHaveAttribute('id', /.+/);
    const labelAId = await labelA.getAttribute('id');
    await expect(radio).toHaveAttribute('aria-labelledby', labelAId!);

    await toggle.click();

    await expect(labelB).toHaveAttribute('id', /.+/);
    const labelBId = await labelB.getAttribute('id');
    expect(labelBId).not.toBe(labelAId);
    await expect(radio).toHaveAttribute('aria-labelledby', labelBId!);
  });

  test('prefers `aria-label` over an associated label', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-standalone');
    const { radio } = radioCase(fixture, 'aria-label');

    await settle(fixture);
    await expect(radio).not.toHaveAttribute('aria-labelledby');
    await expect(radio).toHaveAccessibleName('Apple');
  });

  test.describe('prop: onClick', () => {
    for (const nativeButton of [false, true]) {
      const name = nativeButton ? 'propagate-native' : 'propagate';

      test(`propagates a single click event to ancestors per user click (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-standalone');
        const { container, radio, ancestor } = radioCase(fixture, name);
        await countClicks(ancestor);

        await radio.click();

        await expect(radio).toHaveAttribute('aria-checked', 'true');
        await settle(fixture);
        // RAD-PTR-03: the hidden input's click, which selects, never reaches the ancestor.
        await expect(ancestor).toHaveAttribute('data-observed-clicks', '1');
        await expect(container).toHaveAttribute('data-clicks', '1');
        await expect(container).toHaveAttribute('data-changes', '1');
      });

      test(`does not propagate to ancestors when stopPropagation() is called (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-standalone');
        const { radio, ancestor } = radioCase(fixture, name);
        await countClicks(ancestor);
        await radio.evaluate((element) => element.addEventListener('click', (event) => event.stopPropagation()));

        await radio.click();

        await expect(radio).toHaveAttribute('aria-checked', 'true');
        await settle(fixture);
        await expect(ancestor).not.toHaveAttribute('data-observed-clicks');
      });
    }

    test('does not propagate a click to ancestors when selecting with arrow keys', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-standalone');
      const { container, radioOf, ancestor } = radioCase(fixture, 'arrows');

      await radioOf('a').click();
      await countClicks(ancestor);

      await page.keyboard.press('ArrowDown');

      await expect(radioOf('b')).toHaveAttribute('aria-checked', 'true');
      await expect(container).toHaveAttribute('data-changes', '1');
      await settle(fixture);
      await expect(ancestor).not.toHaveAttribute('data-observed-clicks');
    });

    test('port: a C# onClick veto stops the selection on WebAssembly and is too late on Server', async ({ page, mode }) => {
      // RAD-PTR-09, ADR 01's row "C#-origin consumer vetoes".
      const fixture = await gotoRadios(page, mode, 'radio-standalone');
      const { container, radio } = radioCase(fixture, 'vetoed');

      await radio.click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      await expect(radio).toHaveAttribute('aria-checked', mode === 'wasm' ? 'false' : 'true');
      await expect(container).toHaveAttribute('data-changes', mode === 'wasm' ? '0' : '1');
    });

    test('port: a C# onKeyUp veto of Space stops the selection on WebAssembly and is too late on Server', async ({ page, mode }) => {
      // RAD-KEY-01, the rows "EVD-EVT-26 against composite/button key handling" and "UBT-KEY-03/04".
      const fixture = await gotoRadios(page, mode, 'radio-standalone');
      const { container, radio } = radioCase(fixture, 'vetoed');

      await focus(radio);
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(radio).toHaveAttribute('aria-checked', mode === 'wasm' ? 'false' : 'true');
      await expect(container).toHaveAttribute('data-changes', mode === 'wasm' ? '0' : '1');
    });
  });

  test.describe('prop: disabled', () => {
    test('does not acquire the field focused state when focused programmatically', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-standalone');
      const { radio, field } = radioCase(fixture, 'disabled-field');

      await focus(radio);
      await settle(fixture);

      await expect(radio).toBeFocused();
      await expect(field).not.toHaveAttribute('data-focused');
    });
  });

  test.describe('prop: readOnly', () => {
    test('acquires the field focused state on focus', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-standalone');
      const { radio, field } = radioCase(fixture, 'readonly-field');

      await focus(radio);

      await expect(field).toHaveAttribute('data-focused', '');
    });

    test('port: Space on a read-only radio changes nothing', async ({ page, mode }) => {
      // RAD-KEY-06's read-only clause: the keyup still dispatches the root click, which the root ignores (RAD-PTR-07).
      const fixture = await gotoRadios(page, mode, 'radio-standalone');
      const { radio, input } = radioCase(fixture, 'readonly-field');

      await focus(radio);
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(radio).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
    });
  });
});

test.describe('<Radio.Indicator />', () => {
  /** One mutation of an indicator: added, removed, or one of its attributes changed. */
  interface IndicatorRecord {
    kind: 'added' | 'removed' | 'attribute';
    indicator: string | null;
    name: string | null;
    present: boolean;
    batch: number;
  }

  /**
   * Records, from now on, every insertion and removal of an indicator in the case and every change of its transition
   * attributes and `hidden`, with the number of the observer callback that delivered it (one callback per microtask
   * checkpoint that follows a DOM change, so two records with one batch number landed before the same paint).
   */
  async function recordIndicators(container: Locator): Promise<void> {
    await container.evaluate((node) => {
      const records: unknown[] = [];
      let batch = 0;
      (node as unknown as { indicatorRecords: unknown[] }).indicatorRecords = records;
      new MutationObserver((mutations) => {
        batch++;
        for (const mutation of mutations) {
          if (mutation.type === 'childList') {
            mutation.addedNodes.forEach((added) => {
              if (added instanceof Element && added.matches('[data-indicator]')) {
                records.push({ kind: 'added', indicator: added.getAttribute('data-indicator'), name: null, present: true, batch });
              }
            });
            mutation.removedNodes.forEach((removed) => {
              if (removed instanceof Element && removed.matches('[data-indicator]')) {
                records.push({ kind: 'removed', indicator: removed.getAttribute('data-indicator'), name: null, present: false, batch });
              }
            });
          } else if (mutation.target instanceof Element && mutation.target.matches('[data-indicator]')) {
            records.push({
              kind: 'attribute',
              indicator: mutation.target.getAttribute('data-indicator'),
              name: mutation.attributeName,
              present: mutation.target.hasAttribute(mutation.attributeName!),
              batch,
            });
          }
        }
      }).observe(node, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ['data-starting-style', 'data-ending-style', 'hidden'],
      });
    });
  }

  async function indicatorRecords(container: Locator): Promise<IndicatorRecord[]> {
    return container.evaluate((node) => (node as unknown as { indicatorRecords: IndicatorRecord[] }).indicatorRecords);
  }

  test('should remove the indicator when there is no exit animation defined', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-indicator');
    const { container, select, indicator } = radioCase(fixture, 'no-exit');
    // RAD-EDGE-10: both radios of value "a" are checked, and each indicator is mounted.
    await expect(indicator('a')).toHaveCount(1);
    await expect(indicator('a-twin')).toHaveCount(1);

    await select.click();

    await expect(indicator('a')).toHaveCount(0);
    await expect(indicator('a-twin')).toHaveCount(0);
    await expect(container.locator('[data-indicator]')).toHaveCount(0);
  });

  test('should remove the indicator when the animation finishes', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-indicator', { animations: true });
    const { container, select, indicator } = radioCase(fixture, 'kept');
    await indicator('a').evaluate((element) => {
      element.addEventListener('animationend', () => {
        (element as HTMLElement).dataset.animationEnded = 'true';
      });
    });
    await recordIndicators(container);

    await select.click();

    await expect(indicator('a')).toHaveAttribute('data-animation-ended', 'true');
    // Kept mounted: it carried data-ending-style for its exit, which is cleared once the exit finished.
    await expect(indicator('a')).not.toHaveAttribute('data-ending-style');
    await expect(indicator('a')).toHaveAttribute('data-unchecked', '');
    const records = await indicatorRecords(container);
    expect(records).toContainEqual(expect.objectContaining({ kind: 'attribute', indicator: 'a', name: 'data-ending-style', present: true }));
    expect(records.filter((record) => record.kind === 'removed')).toEqual([]);
  });

  test('triggers enter animation via data-starting-style when mounting', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-indicator', { animations: true });
    const { container, select, indicator } = radioCase(fixture, 'enter');
    await expect(indicator('a')).toHaveCount(0);
    await container.evaluate((node) => {
      node.addEventListener('transitionend', (event) => {
        if ((event.target as Element).matches('[data-indicator="a"]')) {
          (node as HTMLElement).dataset.transitionEnded = 'true';
        }
      });
    });
    await recordIndicators(container);

    await select.click();

    await expect(container).toHaveAttribute('data-transition-ended', 'true');
    await expect(indicator('a')).toHaveCount(1);
    await expect(indicator('a')).not.toHaveAttribute('data-starting-style');
    // RAD-ANI-02: the element appeared with data-starting-style (rendered by C#, the handover), which was taken off later.
    const records = await indicatorRecords(container);
    const added = records.find((record) => record.kind === 'added' && record.indicator === 'a');
    const cleared = records.find((record) => record.indicator === 'a' && record.name === 'data-starting-style' && !record.present);
    expect(added).toBeDefined();
    expect(cleared).toBeDefined();
    expect(cleared!.batch).toBeGreaterThan(added!.batch);
  });

  test('applies data-ending-style before unmount', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-indicator', { animations: true });
    const { container, select, indicator } = radioCase(fixture, 'exit');
    await expect(indicator('a')).toHaveCount(1);
    await recordIndicators(container);

    await select.click();

    await expect(indicator('a')).toHaveCount(0);
    const records = await indicatorRecords(container);
    const ending = records.findIndex((record) => record.indicator === 'a' && record.name === 'data-ending-style' && record.present);
    const removed = records.findIndex((record) => record.kind === 'removed' && record.indicator === 'a');
    expect(ending).toBeGreaterThanOrEqual(0);
    expect(removed).toBeGreaterThan(ending);
  });
});
