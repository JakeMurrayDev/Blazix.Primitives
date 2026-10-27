import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/toolbar.md and the L4 clauses of its [L2] rows, on ToolbarRoot over the `composite`
// module and ToolbarButton and ToolbarInput over the `button` module. Titles are Base UI's (toolbar/*/*.test.tsx);
// "port:" titles assert the per-mode rows of ADR 01's relaxation table, the Test: none rows a browser observes and the
// design points of M3 track 4. The rows that need the derived disabled indices or the outer registration (track 1's E3
// and E4: the focusableWhenDisabled layouts, the Toggle and ToggleGroup block, the hero shape) follow once PR 108 is on
// main. The fixtures record the consumer's callbacks as data attributes; every page waits for `data-attached` (every twin
// attached) and uses the sync button as a barrier.

type FixtureName = 'toolbar-orientation' | 'toolbar-keys' | 'toolbar-input' | 'toolbar-button' | 'toolbar-switch';

/** Opens a Toolbar fixture and waits until every toolbar and item has its behaviors attached. */
async function gotoToolbar(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
  const fixture = await gotoFixture(page, mode, name);
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
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

function toolbarCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    root: container.locator('[data-root]').first(),
    item: (label: string) => container.locator(`[data-item="${label}"]`),
    before: container.locator('[data-before]'),
    after: container.locator('[data-after]'),
    flip: container.locator('[data-flip]'),
  };
}

/** Moves focus as a script does, without a pointer: no click, so no activation. */
async function focus(element: Locator): Promise<void> {
  await element.evaluate((node) => (node as HTMLElement).focus());
}

/** Enters the toolbar of a case with Tab from the native button before it. */
async function tabIn(page: Page, before: Locator): Promise<void> {
  await focus(before);
  await page.keyboard.press('Tab');
}

/** Records each keydown reaching the root: its key, whether its default was prevented and whether it reached the document. */
async function recordKeys(root: Locator): Promise<void> {
  await root.evaluate((element) => {
    const records: { key: string; defaultPrevented: boolean; reachedDocument: boolean }[] = [];
    (element as unknown as { keys: typeof records }).keys = records;
    // Added after the composite module's listener on the same element, so it reads the module's decision.
    element.addEventListener('keydown', (event) => {
      const record = { key: (event as KeyboardEvent).key, defaultPrevented: event.defaultPrevented, reachedDocument: false };
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

/** The count of each entry of a case's `data-log`. */
async function logCounts(container: Locator): Promise<Record<string, number>> {
  const log = (await container.getAttribute('data-log')) ?? '';
  const counts: Record<string, number> = {};
  for (const entry of log.split(' ').filter(Boolean)) {
    counts[entry] = (counts[entry] ?? 0) + 1;
  }
  return counts;
}

test.describe('<Toolbar.Root />', () => {
  test.describe('keyboard navigation', () => {
    // TLB-STR-01, TLB-STR-03, TLB-API-02, TLB-API-03, TLB-API-05 (`dir` forwarded), TLB-KEY-01, TLB-KEY-02, TLB-FOC-01,
    // TLB-DOM-02, TLB-EDGE-06 (each toolbar carries the opposite `dir`, which is not read).
    for (const { direction, orientation, nextKey, prevKey } of [
      { direction: 'ltr', orientation: 'horizontal', nextKey: 'ArrowRight', prevKey: 'ArrowLeft' },
      { direction: 'ltr', orientation: 'vertical', nextKey: 'ArrowDown', prevKey: 'ArrowUp' },
      { direction: 'rtl', orientation: 'horizontal', nextKey: 'ArrowLeft', prevKey: 'ArrowRight' },
      { direction: 'rtl', orientation: 'vertical', nextKey: 'ArrowDown', prevKey: 'ArrowUp' },
    ]) {
      test(`${direction}, orientation: ${orientation}`, async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-orientation');
        const { root, item, before } = toolbarCase(fixture, `${direction}-${orientation}`);
        await expect(root).toHaveAttribute('dir', direction === 'ltr' ? 'rtl' : 'ltr');
        await expect(root).not.toHaveAttribute('tabindex');

        await tabIn(page, before);
        await expect(item('button1')).toBeFocused();

        for (const label of ['link', 'grouped1', 'grouped2', 'input', 'button1']) {
          await page.keyboard.press(nextKey);
          await expect(item(label)).toBeFocused();
        }

        await page.keyboard.press(prevKey);
        await expect(item('input')).toBeFocused();

        await page.keyboard.press(prevKey);
        await expect(item('grouped2')).toBeFocused();
      });
    }

    // TLB-API-03, TLB-KEY-05.
    test('does not wrap focus when loopFocus is false', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-orientation');
      const { item, before } = toolbarCase(fixture, 'no-loop');

      await tabIn(page, before);
      await expect(item('first')).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(item('first')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(item('last')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(item('last')).toBeFocused();
    });
  });

  test.describe('port', () => {
    // TLB-KEY-03, TLB-KEY-04, TLB-KEY-06, TLB-KEY-07, CMP-KEY-10: a handled arrow is default-prevented and stopped; the
    // cross-axis arrows, Home and End (not the toolbar's) and a modified arrow are neither, and move nothing.
    test('port: a handled arrow is default-prevented and stopped; the cross-axis arrows, Home, End and a modified arrow are neither', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-keys');
      const { root, item } = toolbarCase(fixture, 'keys');
      await recordKeys(root);

      await focus(item('one'));
      await page.keyboard.press('ArrowRight');
      await expect(item('two')).toBeFocused();
      for (const key of ['ArrowDown', 'ArrowUp', 'Home', 'End', 'Shift+ArrowRight', 'Control+ArrowRight']) {
        await page.keyboard.press(key);
      }
      await settle(fixture);

      await expect(item('two')).toBeFocused();
      await expect(item('two')).toHaveAttribute('tabindex', '0');
      // The modifier presses' own keydowns (Shift, Control) are not the subject.
      const records = await root.evaluate((element) => (element as unknown as { keys: { key: string }[] }).keys);
      const [arrow, ...others] = records.filter((record) => record.key !== 'Shift' && record.key !== 'Control');
      expect(arrow).toMatchObject({ key: 'ArrowRight', defaultPrevented: true, reachedDocument: false });
      expect(others).toHaveLength(6);
      for (const other of others) {
        expect(other).toMatchObject({ defaultPrevented: false, reachedDocument: true });
      }
    });

    // TLB-FOC-01, TLB-KEY-14: one tab stop; Tab enters on the item holding it and leaves in one press, and Shift+Tab
    // returns to it.
    test('port: Tab enters the toolbar on the item holding the tab stop and leaves it in one press', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-keys');
      const { container, item, before, after } = toolbarCase(fixture, 'keys');
      await expect(container.locator('[data-item][tabindex="0"]')).toHaveCount(1);

      await tabIn(page, before);
      await page.keyboard.press('ArrowRight');
      await expect(item('two')).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(after).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(item('two')).toBeFocused();
      await expect(container.locator('[data-item][tabindex="0"]')).toHaveCount(1);
    });

    // TLB-PTR-03, CMP-PTR-01: the toolbar does not highlight on hover.
    test('port: moving the pointer over an item neither focuses it nor moves the tab stop', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-keys');
      const { item } = toolbarCase(fixture, 'keys');
      await expect(item('one')).toHaveAttribute('tabindex', '0');

      await item('three').hover();
      await settle(fixture);

      await expect(item('three')).not.toBeFocused();
      await expect(item('one')).toHaveAttribute('tabindex', '0');
      await expect(item('three')).toHaveAttribute('tabindex', '-1');
    });

    // TLB-PTR-04, CMP-PTR-02: an item that receives focus from a pointer press becomes the tab stop; JavaScript writes
    // the tabindex in the focus task, so on both modes (§9 narrowed, M3 track 4).
    test('port: a pointer press on an item makes it the tab stop', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-keys');
      const { container, item } = toolbarCase(fixture, 'keys');

      await item('three').click();

      await expect(item('three')).toBeFocused();
      await expect(item('three')).toHaveAttribute('tabindex', '0');
      await expect(container.locator('[data-item][tabindex="0"]')).toHaveCount(1);
    });

    // TLB-API-05 (L4 clause), ADR 01's row "… EVD-EVT-26 against composite/button key handling …" with option (i): the
    // root's C# onkeydown runs inside `navigate` on WebAssembly, where PreventPartHandler() vetoes the move; on Server it
    // runs once from the handled-key report, a round trip late, and the move has happened.
    test("port: the root's C# onkeydown vetoes a handled arrow on WebAssembly and runs a round trip late on Server", async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-keys');
      const { container, item } = toolbarCase(fixture, 'root-keydown');

      await focus(item('one'));
      await page.keyboard.press('ArrowRight');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-log', 'root:ArrowRight');
      await expect(item(mode === 'wasm' ? 'one' : 'two')).toBeFocused();
    });

    // TLB-API-16, ADR 01 and ADR 04 Amendments 2026-10-26 (option (i)'s precision): a ToolbarGroup's C# onkeydown never
    // runs for a key the module handles, and runs for one it does not.
    test("port: a ToolbarGroup's C# onkeydown runs for an unhandled key and not for a handled arrow", async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-keys');
      const { container, item } = toolbarCase(fixture, 'group-keydown');

      await focus(item('one'));
      await page.keyboard.press('ArrowRight');
      await expect(item('two')).toBeFocused();
      await page.keyboard.press('a');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-log', 'group:a');
    });
  });
});

test.describe('<Toolbar.Input />', () => {
  test.describe('pointer interactions', () => {
    // TLB-PTR-02, TLB-PTR-04, TLB-FOC-08.
    test('does not steal focus while disabled and becomes pointer-focusable when enabled', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-input');
      const { item, before, flip } = toolbarCase(fixture, 'pointer');

      await tabIn(page, before);
      await expect(item('button')).toBeFocused();

      await item('input').click({ force: true });
      await expect(item('button')).toBeFocused();

      await flip.click();
      await expect(item('input')).not.toHaveAttribute('aria-disabled');
      await settle(fixture);
      await item('input').click();
      await expect(item('input')).toBeFocused();
    });

    // TLB-PTR-02, TLB-EDGE-11, TLB-API-14 (a consumer type forwarded).
    test('prevents click default actions while disabled', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-input');
      const { item, flip } = toolbarCase(fixture, 'checkbox');
      const input = item('input');
      await expect(input).toHaveAttribute('type', 'checkbox');

      await input.click({ force: true });
      await expect(input).not.toBeChecked();

      await flip.click();
      await expect(input).not.toHaveAttribute('aria-disabled');
      await settle(fixture);
      await input.click();
      await expect(input).toBeChecked();
    });
  });

  test.describe('keyboard navigation', () => {
    // TLB-KEY-09, TLB-KEY-07 (Shift held), TLB-KEY-02.
    for (const [direction, nextKey, previousKey] of [
      ['ltr', 'ArrowRight', 'ArrowLeft'],
      ['rtl', 'ArrowLeft', 'ArrowRight'],
    ] as const) {
      test(`respects caret and selection boundaries in horizontal ${direction} toolbars`, async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-input');
        const { item, before } = toolbarCase(fixture, `caret-${direction}`);
        const input = item('input');
        const select = (start: number, end: number) =>
          input.evaluate((element, [s, e]) => (element as HTMLInputElement).setSelectionRange(s, e), [start, end] as const);

        await tabIn(page, before);
        await page.keyboard.press(nextKey);
        await expect(input).toBeFocused();

        await select(1, 3);
        await page.keyboard.press(nextKey);
        await expect(input).toBeFocused();

        await select(2, 2);
        await page.keyboard.press(`Shift+${nextKey}`);
        await expect(input).toBeFocused();

        // The forward key leaves the input at the end of its value in both directions, the backward key at the start.
        await select(4, 4);
        await page.keyboard.press(nextKey);
        await expect(item('after')).toBeFocused();

        await page.keyboard.press(previousKey);
        await expect(input).toBeFocused();
        await select(0, 0);
        await page.keyboard.press(previousKey);
        await expect(item('before')).toBeFocused();
      });
    }

    // TLB-KEY-09, TLB-KEY-10, TLB-FOC-05.
    for (const { orientation, nextKey, prevKey } of [
      { orientation: 'horizontal', nextKey: 'ArrowRight', prevKey: 'ArrowLeft' },
      { orientation: 'vertical', nextKey: 'ArrowDown', prevKey: 'ArrowUp' },
    ]) {
      test(`orientation: ${orientation}`, async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-input');
        const { item, before } = toolbarCase(fixture, `orientation-${orientation}`);
        const input = item('input');

        await tabIn(page, before);
        await expect(item('button1')).toBeFocused();

        await page.keyboard.press(nextKey);
        await expect(input).toBeFocused();
        expect(await input.evaluate((element) => [(element as HTMLInputElement).selectionStart, (element as HTMLInputElement).selectionEnd])).toEqual([0, 4]);

        await page.keyboard.press('ArrowRight');
        await page.keyboard.press(nextKey);
        await expect(item('button2')).toBeFocused();

        await page.keyboard.press(prevKey);
        await expect(input).toBeFocused();

        await page.keyboard.press('ArrowLeft');
        await page.keyboard.press(prevKey);
        await expect(item('button1')).toBeFocused();
      });
    }
  });
});

test.describe('<Toolbar.Button />', () => {
  test.describe('prop: nativeButton', () => {
    // TLB-API-08, TLB-KEY-12, TLB-DOM-04 (role="button"). The capture listener is the spec's: Blazor has no capture phase.
    for (const key of ['Space', 'Enter']) {
      test(`custom element: dispatches real clicks from ${key} keyboard activation`, async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-button');
        const { container, item, before } = toolbarCase(fixture, 'custom');
        const button = item('save');
        await expect(button).toHaveAttribute('role', 'button');
        await container.locator('[data-ancestor]').evaluate((element) => {
          (element as unknown as { captured: number }).captured = 0;
          element.addEventListener('click', () => (element as unknown as { captured: number }).captured++, { capture: true });
        });

        await tabIn(page, before);
        await expect(button).toBeFocused();
        await page.keyboard.press(key);
        await settle(fixture);

        expect(await container.locator('[data-ancestor]').evaluate((element) => (element as unknown as { captured: number }).captured)).toBe(1);
        expect(await logCounts(container)).toEqual({ 'render:click': 1, 'button:click': 1, 'ancestor:click': 1 });
      });
    }
  });

  test.describe('prop: disabled', () => {
    // TLB-API-06, TLB-API-09, TLB-API-10, TLB-DOM-03, TLB-DOM-04, TLB-ARIA-03, TLB-KEY-13, TLB-PTR-01, TLB-EDGE-04.
    test('disables the button', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-button');
      const { container, item } = toolbarCase(fixture, 'disabled');
      const button = item('button');

      await expect(button).not.toHaveAttribute('disabled');
      await expect(button).toHaveAttribute('data-disabled');
      await expect(button).toHaveAttribute('aria-disabled', 'true');

      await button.click({ force: true });
      await focus(button);
      await page.keyboard.press('Space');
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '0');
      await expect(container).toHaveAttribute('data-mousedowns', '0');
      await expect(container).toHaveAttribute('data-pointerdowns', '0');
      await expect(container).toHaveAttribute('data-keydowns', '0');
    });

    // TLB-PTR-01, TLB-EDGE-04.
    test('allows hover handlers while blocking activation', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-button');
      const { container, item } = toolbarCase(fixture, 'hover');
      const button = item('button');

      await expect(button).not.toHaveAttribute('disabled');
      await expect(button).toHaveAttribute('data-disabled');
      await expect(button).toHaveAttribute('aria-disabled', 'true');

      await button.hover();
      await settle(fixture);
      expect(Number(await container.getAttribute('data-mousemoves'))).toBeGreaterThan(0);

      await button.click({ force: true });
      await settle(fixture);
      await expect(container).toHaveAttribute('data-clicks', '0');
    });

    // TLB-DOM-08, UBT-ALG-04, design point 14.1 (a), pending the user: the outer button's handle clears the DOM disabled
    // property the inner Toggle rendered, after the first render and after each flip, so the element stays focusable.
    test('port: a disabled Toggle substituted into a focusable-when-disabled button has no disabled attribute and stays focusable, also after a runtime flip', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-button');
      const { item, flip } = toolbarCase(fixture, 'toggle-substitute');
      const toggle = item('toggle');

      await expect(toggle).toHaveAttribute('aria-pressed', 'false');
      await expect(toggle).toHaveAttribute('aria-disabled', 'true');
      await expect(toggle).not.toHaveAttribute('disabled');
      await focus(toggle);
      await expect(toggle).toBeFocused();

      await flip.click();
      await expect(toggle).toHaveAttribute('aria-disabled', 'false');
      await flip.click();
      await expect(toggle).toHaveAttribute('aria-disabled', 'true');
      await settle(fixture);

      await expect(toggle).not.toHaveAttribute('disabled');
      await focus(toggle);
      await expect(toggle).toBeFocused();
    });
  });

  // ToolbarButton.test.tsx's Switch block ("renders a switch", "handles interactions", "disabled state") waits on design
  // point 14.7 of M3 track 4: a non-native SwitchRoot puts the id it receives on its hidden input (SWT-API-11), so the
  // toolbar registers and attaches to the input. The `toolbar-switch` fixture is kept for it.
});
