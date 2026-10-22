import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/architecture/composite-and-list-navigation.md (CMP) that M2 reaches, on the test-only composite root
// and items (`composite-*` fixtures): Base UI's CompositeRoot is internal, and RadioGroup (track 8) is the first public
// root, whose own spec proves the same rows through a public part. Titles are CompositeRoot.test.tsx's, verbatim, where a
// Base UI test observes the row; port titles cover the re-cited rows, the per-mode clauses of ADR 01's relaxation table
// (CMP-STA-10; EVD-EVT-26 against composite key handling, CMP-EVT-12) and the RadioGroup options. Real keyboard, no
// sleeps: a state inside one dispatch is witnessed by a listener the page adds after the module's, a later one by
// `expect`, and C#'s view by the fixture's data attributes after a sync barrier.

type FixtureName = 'composite-list' | 'composite-rtl' | 'composite-reorder' | 'composite-selection';

/** Opens a composite fixture and waits until every root has its behavior attached. */
async function gotoComposite(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
  const fixture = await gotoFixture(page, mode, name);
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/**
 * A barrier: clicks the fixture's sync button and waits for C# to count it. Server processes one circuit's events in order
 * (ADR-04/1) and WebAssembly synchronously, so every event dispatched before has reached C# afterwards.
 */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

function group(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    root: container.locator('[data-root]'),
    item: (label: string) => container.locator(`[data-item="${label}"]`),
  };
}

/** What a listener added after the module's on the root saw of one keydown. */
interface KeyRecord {
  key: string;
  defaultPrevented: boolean;
  /** Whether the event reached a listener on the document (it was not stopped). */
  reachedDocument: boolean;
}

/** Records every keydown that reaches the root, from a listener that runs after the module's. */
async function recordKeys(root: Locator): Promise<void> {
  await root.evaluate((element) => {
    const records: KeyRecord[] = [];
    (element as unknown as { keys: KeyRecord[] }).keys = records;
    element.addEventListener('keydown', (event) => {
      const record: KeyRecord = { key: (event as KeyboardEvent).key, defaultPrevented: event.defaultPrevented, reachedDocument: false };
      records.push(record);
      document.addEventListener(
        'keydown',
        (reached) => {
          if (reached === event) {
            record.reachedDocument = true;
          }
        },
        { once: true },
      );
    });
  });
}

async function keyRecords(root: Locator): Promise<KeyRecord[]> {
  return root.evaluate((element) => (element as unknown as { keys: KeyRecord[] }).keys);
}

/** One move of the tab stop or of the focus inside a root, with the other one's position at that moment. */
interface MoveRecord {
  kind: 'tab-stop' | 'focus';
  tabStop: string | null;
  focused: string | null;
}

/**
 * Records, as markers the page keeps (ADR 06 §2), each write of `tabindex="0"` inside the root (a mutation record, delivered
 * in the microtask after the write) and each focus arriving inside it, with where the other one was at that moment. In a
 * browser a microtask checkpoint follows every listener, so the order of these records is what shows that the tab stop moved
 * inside the keydown's own task, before the focus.
 */
async function recordMoves(root: Locator): Promise<void> {
  await root.evaluate((element) => {
    const records: MoveRecord[] = [];
    (element as unknown as { moves: MoveRecord[] }).moves = records;
    const label = (node: Element | null | undefined) => node?.getAttribute('data-item') ?? null;
    const tabStop = () => label(element.querySelector('[data-item][tabindex="0"]'));
    new MutationObserver((mutations) => {
      if (mutations.some((mutation) => (mutation.target as Element).getAttribute('tabindex') === '0')) {
        records.push({ kind: 'tab-stop', tabStop: tabStop(), focused: label(document.activeElement) });
      }
    }).observe(element, { subtree: true, attributes: true, attributeFilter: ['tabindex'] });
    element.addEventListener('focusin', (event) => {
      records.push({ kind: 'focus', tabStop: tabStop(), focused: label(event.target as Element) });
    });
  });
}

async function moveRecords(root: Locator): Promise<MoveRecord[]> {
  return root.evaluate((element) => (element as unknown as { moves: MoveRecord[] }).moves);
}

test.describe('Composite', () => {
  test.describe('list', () => {
    // CMP-KEY-10, CMP-KEY-11, CMP-EDGE-18: default prevented and propagation stopped exactly when the index changes.
    for (const { orientation, key, prevented } of [
      { orientation: 'horizontal', key: 'ArrowRight', prevented: true },
      { orientation: 'horizontal', key: 'ArrowDown', prevented: false },
      { orientation: 'vertical', key: 'ArrowDown', prevented: true },
      { orientation: 'vertical', key: 'ArrowRight', prevented: false },
      { orientation: 'both', key: 'ArrowRight', prevented: true },
      { orientation: 'both', key: 'ArrowDown', prevented: true },
    ]) {
      test(`sets default prevention to ${prevented} for ${key} with ${orientation} orientation`, async ({ page, mode }) => {
        const fixture = await gotoComposite(page, mode, 'composite-list');
        const { root, item } = group(fixture, orientation);
        await recordKeys(root);

        await item('1').focus();
        await page.keyboard.press(key);

        const [record] = await keyRecords(root);
        expect(record).toMatchObject({ key, defaultPrevented: prevented, reachedDocument: !prevented });
        await expect(item(prevented ? '2' : '1')).toBeFocused();
      });
    }

    // CMP-KEY-16 and CMP-FOC-02, re-cited from "controlled mode" (a consumer-owned highlight is M3; ADR 13 Amendments
    // 2026-10-22): the tab stop moves inside the dispatch, the focus one microtask later.
    test('uncontrolled mode', async ({ page, mode }) => {
      const fixture = await gotoComposite(page, mode, 'composite-list');
      const { root, item } = group(fixture, 'uncontrolled');

      await item('1').focus();
      await recordMoves(root);

      await page.keyboard.press('ArrowDown');
      await expect(item('2')).toHaveAttribute('tabindex', '0');
      await expect(item('2')).toBeFocused();

      await page.keyboard.press('ArrowDown');
      await expect(item('3')).toHaveAttribute('tabindex', '0');
      await expect(item('3')).toBeFocused();

      await page.keyboard.press('ArrowUp');
      await expect(item('2')).toHaveAttribute('tabindex', '0');
      await expect(item('2')).toBeFocused();

      await page.keyboard.press('ArrowUp');
      await expect(item('1')).toHaveAttribute('tabindex', '0');
      await expect(item('1')).toBeFocused();

      // Each move: the tab stop first, while the old item still has focus, then the focus, onto the item that already
      // holds the tab stop.
      const move = (from: string, to: string): MoveRecord[] => [
        { kind: 'tab-stop', tabStop: to, focused: from },
        { kind: 'focus', tabStop: to, focused: to },
      ];
      expect(await moveRecords(root)).toEqual([...move('1', '2'), ...move('2', '3'), ...move('3', '2'), ...move('2', '1')]);
    });

    // CMP-FOC-01, re-cited from "keeps outer metadata and focus navigation when items share a DOM node" (two registrations
    // on one element is M3; ADR 13 Amendments 2026-10-22).
    test('Tab lands on the item holding the tab stop and Tab leaves the group', async ({ page, mode }) => {
      const fixture = await gotoComposite(page, mode, 'composite-list');
      const { container, item } = group(fixture, 'uncontrolled');

      await container.locator('[data-before]').focus();
      await page.keyboard.press('Tab');
      await expect(item('1')).toBeFocused();

      await page.keyboard.press('ArrowDown');
      await expect(item('2')).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(container.locator('[data-before]')).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(item('2')).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(container.locator('[data-after]')).toBeFocused();
    });

    test('prevents arrow key navigation when any modifier key is pressed by default', async ({ page, mode }) => {
      const fixture = await gotoComposite(page, mode, 'composite-list');
      const { item } = group(fixture, 'uncontrolled');

      await item('1').focus();
      for (const modifier of ['Shift', 'Control', 'Alt', 'Meta']) {
        await page.keyboard.press(`${modifier}+ArrowDown`);
        await expect(item('1')).toBeFocused();
      }
    });

    test('specifies allowed modifier keys that do not prevent arrow key navigation when pressed', async ({ page, mode }) => {
      const fixture = await gotoComposite(page, mode, 'composite-list');
      const { item } = group(fixture, 'modifiers');

      await item('1').focus();
      await page.keyboard.press('Shift+ArrowDown');
      await expect(item('1')).toBeFocused();
      await page.keyboard.press('Control+ArrowDown');
      await expect(item('1')).toBeFocused();
      await page.keyboard.press('Alt+ArrowDown');
      await expect(item('2')).toBeFocused();
      await page.keyboard.press('Meta+ArrowDown');
      await expect(item('3')).toBeFocused();
    });

    test('updates the order of items when their containers are reordered', async ({ page, mode }) => {
      const fixture = await gotoComposite(page, mode, 'composite-reorder');
      const itemA = fixture.locator('[data-item="a"]');
      const itemB = fixture.locator('[data-item="b"]');

      await fixture.locator('[data-reorder]').click();

      // The re-sort commits asynchronously (MutationObserver); once it does, the roving tab stop (highlighted index 0)
      // belongs to the first item in the new DOM order.
      await expect(itemB).toHaveAttribute('tabindex', '0');

      await itemB.focus();
      await page.keyboard.press('ArrowDown');
      await expect(itemA).toBeFocused();
    });

    test.describe('rtl', () => {
      test('horizontal orientation', async ({ page, mode }) => {
        const fixture = await gotoComposite(page, mode, 'composite-rtl');
        const { item } = group(fixture, 'horizontal');

        await item('1').focus();

        await page.keyboard.press('ArrowDown');
        await expect(item('1')).toBeFocused();

        await page.keyboard.press('ArrowLeft');
        await expect(item('2')).toHaveAttribute('tabindex', '0');
        await expect(item('2')).toBeFocused();

        await page.keyboard.press('ArrowLeft');
        await expect(item('3')).toHaveAttribute('tabindex', '0');
        await expect(item('3')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('2')).toHaveAttribute('tabindex', '0');
        await expect(item('2')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('1')).toHaveAttribute('tabindex', '0');
        await expect(item('1')).toBeFocused();

        // loop backward
        await page.keyboard.press('ArrowRight');
        await expect(item('3')).toHaveAttribute('tabindex', '0');
        await expect(item('3')).toBeFocused();
      });

      test('both horizontal and vertical orientation', async ({ page, mode }) => {
        const fixture = await gotoComposite(page, mode, 'composite-rtl');
        const { item } = group(fixture, 'both');

        await item('1').focus();

        await page.keyboard.press('ArrowLeft');
        await expect(item('2')).toHaveAttribute('tabindex', '0');
        await expect(item('2')).toBeFocused();

        await page.keyboard.press('ArrowLeft');
        await expect(item('3')).toHaveAttribute('tabindex', '0');
        await expect(item('3')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('2')).toHaveAttribute('tabindex', '0');
        await expect(item('2')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('1')).toHaveAttribute('tabindex', '0');
        await expect(item('1')).toBeFocused();

        await page.keyboard.press('ArrowDown');
        await expect(item('2')).toHaveAttribute('tabindex', '0');
        await expect(item('2')).toBeFocused();

        await page.keyboard.press('ArrowDown');
        await expect(item('3')).toHaveAttribute('tabindex', '0');
        await expect(item('3')).toBeFocused();
      });
    });
  });

  // CMP-STA-10 and ADR 01's row "EVD-STA-07, CMP-STA-10, FFI registration": the registry flush is C#'s after-render. On
  // WebAssembly it runs before paint, so the item's insertion and the module's tabindex write share one task (one batch
  // of mutation records); on Server the write follows the insertion a round trip later.
  test('a new item takes the tab stop in the task that inserts it on WebAssembly, and within a round trip on Server', async ({ page, mode }) => {
    const fixture = await gotoComposite(page, mode, 'composite-list');
    const { container, root } = group(fixture, 'late');
    await root.evaluate((element) => {
      const batches: string[][] = [];
      (element as unknown as { batches: string[][] }).batches = batches;
      new MutationObserver((records) => {
        batches.push(
          records.map((record) =>
            record.type === 'attributes'
              ? `tabindex=${(record.target as Element).getAttribute('tabindex')}`
              : `inserted:${Array.from(record.addedNodes, (node) => (node as Element).getAttribute?.('data-item')).join(',')}`,
          ),
        );
      }).observe(element, { childList: true, subtree: true, attributes: true, attributeFilter: ['tabindex'] });
    });

    await container.locator('[data-add]').click();
    await expect(container.locator('[data-item="1"]')).toHaveAttribute('tabindex', '0');

    const batches = await root.evaluate((element) => (element as unknown as { batches: string[][] }).batches);
    const insertion = batches.findIndex((batch) => batch.includes('inserted:1'));
    const tabStop = batches.findIndex((batch) => batch.includes('tabindex=0'));
    expect(insertion).toBeGreaterThanOrEqual(0);
    if (mode === 'wasm') {
      expect(tabStop).toBe(insertion);
    } else {
      expect(tabStop).toBeGreaterThan(insertion);
    }
  });
});

test.describe('composite consumer key handlers (port)', () => {
  // EVD-EVT-26 against composite key handling (CMP-EVT-12; ADR 01 relaxation table, ADR 04 §3 decision point `navigate`,
  // fallback run) with option (i) of ADR 04's note of 2026-10-22, chosen by the user 2026-10-23. WebAssembly runs the focused item's and
  // the root's C# onkeydown inside the dispatch, where PreventPartHandler() stops the key. On Server the key is handled and
  // the module's handled-key report runs the same handlers a round trip later. An ancestor's handler never sees the key,
  // as in Base UI, where the root stops its propagation.
  test('a PreventPartHandler() in the root onkeydown vetoes the key on WebAssembly; Server handles it and runs the handler after (fallback run)', async ({ page, mode }) => {
    const fixture = await gotoComposite(page, mode, 'composite-list');
    const { container, item } = group(fixture, 'probe-stop');
    await container.locator('[data-veto-switch]').click();
    await expect(container).toHaveAttribute('data-veto', 'true');

    await item('1').focus();
    await page.keyboard.press('ArrowDown');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-root-keys', '1');
    if (mode === 'wasm') {
      await expect(item('1')).toBeFocused();
      await expect(item('1')).toHaveAttribute('tabindex', '0');
    } else {
      await expect(item('2')).toBeFocused();
    }
  });

  // The probe of deliverable 2 (issue 79), as option (i) answers it.
  test("with stopEventPropagation a handled key runs the item's and the root's C# onkeydown and no ancestor's; an unhandled key runs all three", async ({ page, mode }) => {
    const fixture = await gotoComposite(page, mode, 'composite-list');
    const { container, item } = group(fixture, 'probe-stop');

    await item('1').focus();
    await page.keyboard.press('ArrowDown');
    await expect(item('2')).toBeFocused();
    await settle(fixture);

    await expect(container).toHaveAttribute('data-root-keys', '1');
    await expect(container).toHaveAttribute('data-item-keys', '1');
    await expect(container).toHaveAttribute('data-outer-keys', '0');

    // ArrowRight is outside the vertical orientation: the module leaves it, and Blazor dispatches it.
    await page.keyboard.press('ArrowRight');
    await settle(fixture);
    await expect(container).toHaveAttribute('data-root-keys', '2');
    await expect(container).toHaveAttribute('data-item-keys', '2');
    await expect(container).toHaveAttribute('data-outer-keys', '1');
  });

  test('without stopEventPropagation every C# onkeydown runs once for a key the root handles', async ({ page, mode }) => {
    const fixture = await gotoComposite(page, mode, 'composite-list');
    const { container, item } = group(fixture, 'probe-propagate');

    await item('1').focus();
    await page.keyboard.press('ArrowDown');
    await expect(item('2')).toBeFocused();
    await settle(fixture);

    await expect(container).toHaveAttribute('data-root-keys', '1');
    await expect(container).toHaveAttribute('data-item-keys', '1');
    await expect(container).toHaveAttribute('data-outer-keys', '1');
  });
});

test.describe('composite selection on navigation (port)', () => {
  // RDG-STA-06, RDG-KEY-03, RAD-FOC-05 (ADR 04 Amendments 2026-10-20): the item an arrow key focuses clicks its input.
  test('selects the item an arrow key focuses by clicking its input, once per key', async ({ page, mode }) => {
    const fixture = await gotoComposite(page, mode, 'composite-selection');
    const item = (label: string) => fixture.locator(`[data-item="${label}"]`);

    await item('a').focus();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-changes', '0');

    await page.keyboard.press('ArrowDown');
    await expect(item('b')).toBeFocused();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-selected', 'b');
    await expect(fixture).toHaveAttribute('data-changes', '1');
    await expect(fixture.locator('[data-input="b"]')).toBeChecked();

    // RDG-KEY-02: Shift is an allowed modifier.
    await page.keyboard.press('Shift+ArrowDown');
    await expect(item('c')).toBeFocused();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-selected', 'c');
    await expect(fixture).toHaveAttribute('data-changes', '2');
  });

  // RDG-STA-06 and RDG-FOC-05: an ignored arrow still sets the flag, and the leaving blur clears it.
  for (const modifier of ['Control', 'Alt', 'Meta']) {
    test(`an arrow with ${modifier} sets the pending flag and the leaving blur clears it, so a refocus does not select`, async ({ page, mode }) => {
      const fixture = await gotoComposite(page, mode, 'composite-selection');
      const item = (label: string) => fixture.locator(`[data-item="${label}"]`);

      await item('a').focus();
      await page.keyboard.press(`${modifier}+ArrowDown`);
      await expect(item('a')).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(fixture.locator('[data-after]')).toBeFocused();
      await page.keyboard.press('Shift+Tab');
      await expect(item('a')).toBeFocused();
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-changes', '0');
      await expect(fixture).toHaveAttribute('data-focus-leaves', '1');
    });
  }

  // RadioRoot.tsx:156-166: a disabled input returns before the flag is cleared.
  test('leaves the pending flag set when the focused item input is disabled', async ({ page, mode }) => {
    const fixture = await gotoComposite(page, mode, 'composite-selection');
    const item = (label: string) => fixture.locator(`[data-item="${label}"]`);
    await fixture.locator('[data-disable-b]').click();
    await expect(fixture.locator('[data-input="b"]')).toBeDisabled();

    await item('a').focus();
    await page.keyboard.press('ArrowDown');
    await expect(item('b')).toBeFocused();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-changes', '0');

    await item('c').focus();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-selected', 'c');
  });

  // RDG-FOC-04/05 and CMP-EVT-01 as reports: once per change, a move between items is not a leave.
  test('reports the highlighted index once per change and the leaving blur once', async ({ page, mode }) => {
    const fixture = await gotoComposite(page, mode, 'composite-selection');
    const item = (label: string) => fixture.locator(`[data-item="${label}"]`);

    await item('b').focus();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-highlight', '1');

    await page.keyboard.press('ArrowDown');
    await expect(item('c')).toBeFocused();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-highlight', '2');
    await expect(fixture).toHaveAttribute('data-focus-leaves', '0');

    const leaving = await item('c').getAttribute('id');
    await page.keyboard.press('Tab');
    await expect(fixture.locator('[data-after]')).toBeFocused();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-focus-leaves', '1');
    await expect(fixture).toHaveAttribute('data-last-leave', `focusout:${leaving}`);
  });
});
