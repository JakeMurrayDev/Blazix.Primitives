import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, readDiagnostics, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture, openChanges, settle as settlePopup } from './suites/popupFixture';

// The [L4] rows of docs/components/toolbar.md and the L4 clauses of its [L2] rows, on ToolbarRoot over the `composite`
// module and ToolbarButton and ToolbarInput over the `button` module. Titles are Base UI's (toolbar/*/*.test.tsx);
// "port:" titles assert the per-mode rows of ADR 01's relaxation table, the Test: none rows a browser observes and the
// design points of M3 track 4. The disabled indices are the ones the `composite` module derives from the items' flags and
// a substituted toggle's registration is the toolbar button's (M3 track 1's E3 and E4). The fixtures record the consumer's
// callbacks as data attributes; every page waits for `data-attached` (every twin attached) and uses the sync button as a
// barrier.

const MISMATCH_MESSAGE =
  'Blazix: A component that acts as a button expected a native `<button>` because the `NativeButton` parameter is `true`.';

type FixtureName = 'toolbar-orientation' | 'toolbar-keys' | 'toolbar-disabled' | 'toolbar-input' | 'toolbar-button' | 'toolbar-switch' | 'toolbar-toggle' | 'toolbar-dialog';

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

/** Adds latency to the Server circuit in both directions (CDP network emulation does not delay the WebSocket). */
async function delayCircuit(page: Page, delay: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), delay);
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), delay);
    });
  });
}

/** A disabled item that holds focus: `data-disabled`, `aria-disabled="true"` and focused. */
async function expectFocusedWhenDisabled(element: Locator): Promise<void> {
  await expect(element).toHaveAttribute('data-disabled');
  await expect(element).toHaveAttribute('aria-disabled', 'true');
  await expect(element).toBeFocused();
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

  test.describe('prop: focusableWhenDisabled', () => {
    // TLB-API-07, TLB-API-13, TLB-DOM-04, TLB-DOM-05, TLB-DOM-07, TLB-KEY-06, TLB-KEY-08, TLB-KEY-13, TLB-FOC-03.
    test('toolbar items can be focused when disabled by default', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-disabled');
      const { container, item, before } = toolbarCase(fixture, 'default');
      for (const label of ['button1', 'grouped1', 'grouped2', 'input']) {
        await expect(item(label)).not.toHaveAttribute('disabled');
      }
      await expect(container.locator('[data-item][tabindex="0"]')).toHaveCount(1);

      await tabIn(page, before);
      await expect(item('button1')).toBeFocused();

      for (const label of ['grouped1', 'grouped2', 'input']) {
        await page.keyboard.press('ArrowRight');
        await expectFocusedWhenDisabled(item(label));
      }

      // loop to the beginning
      await page.keyboard.press('ArrowRight');
      await expect(item('button1')).toHaveAttribute('tabindex', '0');

      await page.keyboard.press('ArrowLeft');
      await expectFocusedWhenDisabled(item('input'));

      await page.keyboard.press('ArrowLeft');
      await expectFocusedWhenDisabled(item('grouped2'));
    });

    // TLB-API-07, TLB-STA-02, TLB-DOM-03, TLB-DOM-04, TLB-KEY-08, TLB-FOC-04.
    test('toolbar items can individually disable focusableWhenDisabled', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-disabled');
      const { container, item, before } = toolbarCase(fixture, 'individual');
      for (const element of await container.locator('[data-focusable]').all()) {
        await expect(element).not.toHaveAttribute('disabled');
      }
      await expect(item('input')).not.toHaveAttribute('disabled');
      await expect(item('grouped2')).toHaveAttribute('disabled');

      await tabIn(page, before);
      await expect(item('button1')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expectFocusedWhenDisabled(item('grouped1'));

      await page.keyboard.press('ArrowRight');
      await expectFocusedWhenDisabled(item('input'));

      // loop to the beginning
      await page.keyboard.press('ArrowRight');
      await expect(item('button1')).toHaveAttribute('tabindex', '0');

      await page.keyboard.press('ArrowLeft');
      await expectFocusedWhenDisabled(item('input'));

      await page.keyboard.press('ArrowLeft');
      await expectFocusedWhenDisabled(item('grouped1'));
    });

    // TLB-STA-04, TLB-FOC-02, TLB-DOM-05, TLB-KEY-01.
    test('moves the initial tab stop off a disabled, non-focusable first item', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-disabled');
      const { item, before } = toolbarCase(fixture, 'first-non-focusable');

      // a natively disabled first item cannot hold the single roving tab stop
      await expect(item('button1')).toHaveAttribute('disabled');
      await expect(item('button2')).toHaveAttribute('tabindex', '0');
      await expect(item('button1')).not.toHaveAttribute('tabindex', '0');

      await tabIn(page, before);
      await expect(item('button2')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(item('button3')).toBeFocused();

      // looping back skips the disabled first item
      await page.keyboard.press('ArrowRight');
      await expect(item('button2')).toBeFocused();
    });

    // TLB-KEY-08, TLB-FOC-04.
    test('keeps an enabled item with focusableWhenDisabled={false} navigable', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-disabled');
      const { item, before } = toolbarCase(fixture, 'enabled-non-focusable');
      await expect(item('button2')).not.toHaveAttribute('disabled');

      await tabIn(page, before);
      await expect(item('button1')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(item('button2')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(item('button3')).toBeFocused();
    });

    // TLB-API-13, TLB-STA-02, TLB-FOC-04.
    test('skips a disabled Toolbar.Input with focusableWhenDisabled={false}', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-disabled');
      const { item, before } = toolbarCase(fixture, 'input-non-focusable');

      await tabIn(page, before);
      await expect(item('button1')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(item('input')).not.toBeFocused();
      await expect(item('button2')).toBeFocused();
    });

    // TLB-STA-06, TLB-STA-04: an item that becomes disabled and not focusable while it holds the tab stop sends its new
    // flags, and the derived indices move the tab stop to the next navigable item.
    test('port: an item that becomes disabled and not focusable while it holds the tab stop gives it to the next item', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-disabled');
      const { container, item, flip } = toolbarCase(fixture, 'runtime');
      await expect(item('button1')).toHaveAttribute('tabindex', '0');

      await flip.evaluate((element) => (element as HTMLElement).click());
      await expect(item('button1')).toHaveAttribute('disabled');

      await expect(item('button2')).toHaveAttribute('tabindex', '0');
      await expect(container.locator('[data-item][tabindex="0"]')).toHaveCount(1);
    });

    // TLB-STA-04, TLB-FOC-02, TLB-EDGE-07 (the CMP-STA-10 row of ADR 01's relaxation table): C# renders tabindex="-1" on
    // every item, and the first publication, which carries the flags, puts the tab stop on the first navigable item; on
    // Server that lands a round trip after the render, so at least one frame is painted without a tab stop. The disabled,
    // non-focusable first item never holds it. Recorded per animation frame.
    test('port: the first publication puts the tab stop on the first navigable item, a round trip later on Server', async ({ page, mode }) => {
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      await page.addInitScript(() => {
        const frames: (string | null)[][] = [];
        (window as unknown as { toolbarFrames: (string | null)[][] }).toolbarFrames = frames;
        const tick = () => {
          const items = [...document.querySelectorAll('[data-case="first-non-focusable"] [data-item]')];
          if (items.length === 3) {
            frames.push(items.map((element) => element.getAttribute('tabindex')));
          }
          if (frames.at(-1)?.[1] !== '0' && frames.length < 5000) {
            requestAnimationFrame(tick);
          }
        };
        requestAnimationFrame(tick);
      });
      const fixture = await gotoToolbar(page, mode, 'toolbar-disabled');
      const { item } = toolbarCase(fixture, 'first-non-focusable');
      await expect(item('button2')).toHaveAttribute('tabindex', '0');

      const frames = await page.evaluate(() => (window as unknown as { toolbarFrames: (string | null)[][] }).toolbarFrames);
      expect(frames.at(-1)).toEqual(['-1', '0', '-1']);
      expect(frames.slice(0, -1).every((tabIndexes) => tabIndexes.every((value) => value === '-1'))).toBe(true);
      if (mode === 'server') {
        expect(frames.length).toBeGreaterThan(1);
      }
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

  test.describe('disabled', () => {
    // TLB-KEY-14, TLB-FOC-03, TLB-FOC-08.
    test('does not trap keyboard focus when disabled', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-input');
      const { item, before, after } = toolbarCase(fixture, 'disabled-trap');

      await tabIn(page, before);
      await expect(item('button')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(item('input')).toBeFocused();

      // Tab must leave the toolbar instead of being trapped on the disabled input
      await page.keyboard.press('Tab');
      await expect(after).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(item('input')).toBeFocused();
    });

    // TLB-KEY-11, TLB-KEY-08.
    test('does not block vertical roving focus when disabled', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-input');
      const { item, before } = toolbarCase(fixture, 'disabled-vertical');

      await tabIn(page, before);
      await expect(item('button1')).toBeFocused();

      await page.keyboard.press('ArrowDown');
      await expect(item('input')).toBeFocused();

      // ArrowDown must move roving focus past the disabled input
      await page.keyboard.press('ArrowDown');
      await expect(item('button2')).toBeFocused();

      await page.keyboard.press('ArrowUp');
      await expect(item('input')).toBeFocused();

      await page.keyboard.press('ArrowUp');
      await expect(item('button1')).toBeFocused();
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

    // TLB-KEY-09, TLB-KEY-02, §16 Q8 (settled by the orchestrator 2026-10-27: Base UI cannot simulate it): with real
    // right-to-left text in an `rtl` input, the browser moves the caret logically, so the forward key (ArrowLeft) advances
    // selectionStart; the module decides by selectionStart only, leaving the input at its logical ends.
    test('port: with right-to-left text the arrows move the caret logically inside the input and leave it at its logical ends', async ({ page, mode }) => {
      const fixture = await gotoToolbar(page, mode, 'toolbar-input');
      const { item, before } = toolbarCase(fixture, 'caret-rtl-text');
      const input = item('input');
      const select = (start: number, end: number) =>
        input.evaluate((element, [s, e]) => (element as HTMLInputElement).setSelectionRange(s, e), [start, end] as const);
      const caret = () => input.evaluate((element) => (element as HTMLInputElement).selectionStart);

      await tabIn(page, before);
      await page.keyboard.press('ArrowLeft');
      await expect(input).toBeFocused();

      await select(2, 2);
      await page.keyboard.press('ArrowLeft');
      await expect(input).toBeFocused();
      expect(await caret()).toBe(3);

      await page.keyboard.press('ArrowRight');
      await expect(input).toBeFocused();
      expect(await caret()).toBe(2);

      await select(4, 4);
      await page.keyboard.press('ArrowLeft');
      await expect(item('after')).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(input).toBeFocused();
      await select(0, 0);
      await page.keyboard.press('ArrowRight');
      await expect(item('before')).toBeFocused();
    });

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

    // TLB-DOM-08, UBT-ALG-04, design point 14.1 (a), settled by the orchestrator 2026-10-27: the outer button's handle
    // clears the DOM disabled property the inner Toggle rendered, after the first render and after each flip, so the
    // element stays focusable.
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

  test.describe('rendering other Base UI components', () => {
    // Each substituted toggle records its OnPressedChange (design point 14.8, settled by the orchestrator 2026-10-27: the
    // pipeline keeps a substitute's typed callback typed): one entry per activation (TGL-EVT-06).
    // ToolbarButton.test.tsx's Popover block (M4 track 5; issue 115): a PopoverTrigger substituted into the button.
    test.describe('Popover', () => {
      test('handles interactions', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, 'toolbar-popover');
        const trigger = page.getByTestId('trigger');
        await expect(page.getByText('Content')).toHaveCount(0);

        await page.keyboard.press('Tab');
        await expect(trigger).toBeFocused();
        expect(await openChanges(fixture)).toEqual([]);

        await page.keyboard.press('Enter');
        await expect(page.getByText('Content')).toBeVisible();
        await expect.poll(() => openChanges(fixture)).toEqual(['true:trigger-press']);

        await page.keyboard.press('Escape');
        await expect.poll(() => openChanges(fixture)).toEqual(['true:trigger-press', 'false:escape-key']);
        await expect(trigger).toBeFocused();
      });

      test('disabled state', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, 'toolbar-popover', { disabled: true });
        const trigger = page.getByTestId('trigger');
        await expect(page.getByText('Content')).toHaveCount(0);
        // The toolbar button's handle clears the DOM `disabled` its substitute rendered (design point 14.2 (a)).
        await expect(trigger).not.toHaveAttribute('disabled');
        await expect(trigger).toHaveAttribute('data-disabled');
        await expect(trigger).toHaveAttribute('aria-disabled', 'true');

        await page.keyboard.press('Tab');
        await expect(trigger).toBeFocused();
        for (const key of ['Enter', 'Space', 'ArrowUp', 'ArrowDown']) {
          await page.keyboard.press(key);
        }
        await page.getByText('Sync').evaluate((element) => element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })));
        await expect(fixture).toHaveAttribute('data-sync', '1');
        expect(await openChanges(fixture)).toEqual([]);
      });
    });

    test.describe('Toggle and ToggleGroup', () => {
      // TLB-STR-05, TLB-ARIA-09, TLB-EDGE-12.
      test('renders toggle and toggle group', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { root } = toolbarCase(fixture, 'block');

        await expect(root.getByRole('button')).toHaveCount(3);
        for (const button of await root.getByRole('button').all()) {
          await expect(button).toHaveAttribute('aria-pressed');
        }
      });

      // TLB-KEY-12, TLB-KEY-17, TLB-FOC-01, TLB-EDGE-12, TGL-KEY-02, TGL-EVT-06.
      test('handles interactions', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { container, item, before } = toolbarCase(fixture, 'block');
        for (const label of ['apple', 'one', 'two']) {
          await expect(item(label)).toHaveAttribute('aria-pressed', 'false');
        }

        await tabIn(page, before);
        await expect(item('apple')).toBeFocused();

        await page.keyboard.press('Enter');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-changes', '1');
        await expect(item('apple')).toHaveAttribute('aria-pressed', 'true');

        await page.keyboard.press('ArrowRight');
        await expect(item('one')).toBeFocused();

        await page.keyboard.press('Space');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-changes', '2');
        await expect(item('one')).toHaveAttribute('aria-pressed', 'true');

        await page.keyboard.press('ArrowRight');
        await expect(item('two')).toBeFocused();

        await page.keyboard.press('Enter');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-changes', '3');
        await expect(item('two')).toHaveAttribute('aria-pressed', 'true');
        await expect(container).toHaveAttribute('data-log', 'press:true press:true press:true');
      });

      // TLB-STA-03, TLB-DOM-08, TLB-KEY-13, TLB-KEY-17, TLB-EDGE-12, TGL-KEY-05: the outer registration is focusable when
      // disabled (E4), and the `button` module clears the DOM disabled property each substituted toggle rendered (14.1).
      test('disabled state', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { container, item, before } = toolbarCase(fixture, 'block-disabled');
        for (const label of ['apple', 'one', 'two']) {
          await expect(item(label)).toHaveAttribute('aria-pressed', 'false');
          await expect(item(label)).not.toHaveAttribute('disabled');
          await expect(item(label)).toHaveAttribute('data-disabled');
          await expect(item(label)).toHaveAttribute('aria-disabled', 'true');
        }

        await tabIn(page, before);
        await expect(item('apple')).toBeFocused();
        await page.keyboard.press('Enter');
        await page.keyboard.press('Space');

        await page.keyboard.press('ArrowRight');
        await expect(item('one')).toBeFocused();
        await page.keyboard.press('Enter');
        await page.keyboard.press('Space');

        await page.keyboard.press('ArrowRight');
        await expect(item('two')).toBeFocused();
        await page.keyboard.press('Enter');
        await page.keyboard.press('Space');
        await settle(fixture);

        await expect(container).toHaveAttribute('data-changes', '0');
      });

      // TLB-STR-01, TLB-STR-04, TLB-KEY-17, TLB-FOC-01, TGG-KEY-10, TGG-FOC-05.
      test('navigates and selects direct ToggleGroup > Toggle children', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { container, item, before } = toolbarCase(fixture, 'direct');
        await expect(item('one')).toHaveAttribute('aria-pressed', 'true');

        await tabIn(page, before);
        await expect(item('one')).toBeFocused();

        // toggles past the first must be reachable (previously treated as disabled)
        await page.keyboard.press('ArrowRight');
        await expect(item('two')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('three')).toBeFocused();

        await page.keyboard.press('Enter');
        await settle(fixture);
        // exclusive selection replaces the previous value
        await expect(container).toHaveAttribute('data-log', 'value:three');
        await expect(item('one')).toHaveAttribute('aria-pressed', 'false');
        await expect(item('three')).toHaveAttribute('aria-pressed', 'true');
      });

      // TLB-STA-02, TLB-STA-03, TLB-KEY-08, TLB-KEY-17, TLB-EDGE-12, TGL-FOC-03.
      test('skips disabled direct ToggleGroup > Toggle children', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { item, before } = toolbarCase(fixture, 'direct-disabled');
        await expect(item('two')).toBeDisabled();

        await tabIn(page, before);
        await expect(item('one')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('three')).toBeFocused();
        await expect(item('two')).not.toHaveAttribute('tabindex', '0');
      });

      // TLB-STR-04, TLB-KEY-17.
      test('supports multiple selection for direct ToggleGroup > Toggle children', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { container, item, before } = toolbarCase(fixture, 'direct-multiple');

        await tabIn(page, before);
        await expect(item('one')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('two')).toBeFocused();

        await page.keyboard.press('Enter');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-log', 'value:one,two');
        await expect(item('one')).toHaveAttribute('aria-pressed', 'true');
        await expect(item('two')).toHaveAttribute('aria-pressed', 'true');
      });

      // TLB-STR-04, TGG-API-01.
      test('supports a controlled ToggleGroup value', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { item, before } = toolbarCase(fixture, 'direct-controlled');
        await expect(item('one')).toHaveAttribute('aria-pressed', 'false');

        await tabIn(page, before);
        await expect(item('one')).toBeFocused();

        await page.keyboard.press('Enter');
        await expect(item('one')).toHaveAttribute('aria-pressed', 'true');
      });

      // TLB-API-15, TLB-STA-05, TLB-STA-02, TGG-EDGE-07.
      test('disables direct ToggleGroup children when Toolbar.Group is disabled', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { item, before } = toolbarCase(fixture, 'direct-group-disabled');
        for (const label of ['one', 'two']) {
          await expect(item(label)).toBeDisabled();
          await expect(item(label)).toHaveAttribute('data-disabled');
        }

        await tabIn(page, before);
        await expect(item('before')).toBeFocused();

        await page.keyboard.press('ArrowRight');
        await expect(item('after')).toBeFocused();
        await expect(item('one')).not.toHaveAttribute('tabindex', '0');
        await expect(item('two')).not.toHaveAttribute('tabindex', '0');
      });

      // TLB-STA-06, TLB-STA-02, TGL-FOC-03.
      test('skips a direct Toggle that becomes disabled at runtime', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { item, before, flip } = toolbarCase(fixture, 'direct-runtime');

        await tabIn(page, before);
        await expect(item('one')).toBeFocused();

        // A script click, so focus stays on the first toggle.
        await flip.evaluate((element) => (element as HTMLElement).click());
        await expect(item('two')).toBeDisabled();
        await settle(fixture);

        await page.keyboard.press('ArrowRight');
        await expect(item('three')).toBeFocused();
      });

      // TLB-STA-04, TLB-FOC-02, TLB-DOM-08: the toolbar's first publication waits for its substituted buttons' handles,
      // which clear the DOM disabled property of a substituted toggle; otherwise that property, rendered by the toggle
      // until its handle attaches, makes the module move the initial tab stop off it. The `button` module is held back
      // so its handles attach last, the order a slow network gives (Base UI corrects the property in a layout effect,
      // before its item map is published).
      test('port: a disabled toggle substituted into the first toolbar button keeps the initial tab stop when the button handles attach last', async ({ page, mode }) => {
        await page.route('**/js/button.js', (route) => {
          setTimeout(() => void route.continue(), 1000);
        });
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { item, before } = toolbarCase(fixture, 'first-disabled-substitute');
        await expect(item('first')).not.toHaveAttribute('disabled');

        await expect(item('first')).toHaveAttribute('tabindex', '0');
        await tabIn(page, before);
        await expect(item('first')).toBeFocused();
      });

      // TLB-EDGE-12, TLB-STR-05 (the Toggle clause), TLB-FOC-01, CMP-STR-04 (E4): the docs' hero, two toolbar buttons
      // substituting toggles in a toggle group, then a group of two buttons and a link; the tab stop moves onto the
      // substituted toggles and each activation toggles once.
      test("port: in the docs' hero shape the substituted toggles are toolbar items, each toggles once, and the arrows reach every item", async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-toggle');
        const { container, item, before } = toolbarCase(fixture, 'hero');
        await expect(container.locator('[data-item][tabindex="0"]')).toHaveCount(1);

        await tabIn(page, before);
        await expect(item('align-left')).toBeFocused();

        await page.keyboard.press('Enter');
        await settle(fixture);
        await expect(item('align-left')).toHaveAttribute('aria-pressed', 'true');

        await page.keyboard.press('ArrowRight');
        await expect(item('align-right')).toBeFocused();
        await expect(item('align-right')).toHaveAttribute('tabindex', '0');

        await page.keyboard.press('Space');
        await settle(fixture);
        await expect(item('align-right')).toHaveAttribute('aria-pressed', 'true');
        await expect(item('align-left')).toHaveAttribute('aria-pressed', 'false');
        await expect(container).toHaveAttribute('data-log', 'value:align-left value:align-right');

        for (const label of ['currency', 'percent', 'link', 'align-left']) {
          await page.keyboard.press('ArrowRight');
          await expect(item(label)).toBeFocused();
        }
        await expect(container.locator('[data-item][tabindex="0"]')).toHaveCount(1);
      });
    });

    test.describe('Dialog', () => {
      // TLB-STR-05, API-09, DOM-08, KEY-12, KEY-13, PTR-01, KEY-15, FOC-06: the Dialog clauses (M4 track 8; ADR 12
      // Amendments 2026-10-27 item 4), on the `toolbar-dialog` fixture: a ToolbarButton whose Render is a DialogTrigger,
      // in a non-modal DialogRoot. Dialog reads no toolbar context.
      const dialogCase = (page: Page, mode: RenderMode, name: string) => gotoPopupFixture(page, mode, 'toolbar-dialog', { case: name });

      test('renders a dialog trigger', async ({ page, mode }) => {
        await dialogCase(page, mode, 'renders');

        await expect(page.getByRole('button', { name: /^(Before|Sync)$/ })).toHaveCount(2);
        const trigger = page.getByTestId('trigger');
        await expect(trigger).toHaveCount(1);
        await expect(page.locator('[data-root] button')).toHaveCount(1);
        await expect(page.locator('[data-root] button')).toHaveAttribute('data-testid', 'trigger');
      });

      test('handles interactions', async ({ page, mode }) => {
        const fixture = await dialogCase(page, mode, 'interactions');
        await expect(page.getByText('title text')).toHaveCount(0);
        const trigger = page.getByTestId('trigger');

        await tabIn(page, fixture.locator('[data-before]'));
        await expect(trigger).toBeFocused();
        expect(await openChanges(fixture)).toEqual([]);

        await page.keyboard.press('Enter');
        await expect(page.getByText('title text')).toHaveCount(1);
        await expect.poll(() => openChanges(fixture)).toEqual(['true:trigger-press']);

        await page.keyboard.press('Escape');
        await expect(page.getByText('title text')).toHaveCount(0);
        await expect.poll(() => openChanges(fixture)).toEqual(['true:trigger-press', 'false:escape-key']);
        await expect(trigger).toBeFocused();
      });

      test('disabled state', async ({ page, mode }) => {
        const fixture = await dialogCase(page, mode, 'disabled');
        await expect(page.getByText('title text')).toHaveCount(0);
        const trigger = page.getByTestId('trigger');

        await expect(trigger).not.toHaveAttribute('disabled');
        await expect(trigger).toHaveAttribute('data-disabled');
        await expect(trigger).toHaveAttribute('aria-disabled', 'true');
        await tabIn(page, fixture.locator('[data-before]'));
        await expect(trigger).toBeFocused();

        for (const key of ['Enter', 'Space', 'ArrowUp', 'ArrowDown']) {
          await page.keyboard.press(key);
        }
        await settlePopup(fixture);
        expect(await openChanges(fixture)).toEqual([]);
      });

      test('prevents composite keydowns from escaping', async ({ page, mode }) => {
        await dialogCase(page, mode, 'composite');
        await expect(page.getByRole('dialog')).toHaveCount(0);
        const trigger = page.getByRole('button', { name: 'dialog', exact: true });

        await trigger.click();
        await expect(page.getByRole('dialog')).toBeFocused();
        await page.keyboard.press('ArrowRight');

        await expect(page.getByRole('dialog')).toBeFocused();
        await expect(page.getByRole('button', { name: 'empty', includeHidden: true })).not.toBeFocused();
      });

      // DLG-KEY-09, the port title the stop needs (issue 123): with the popup portaled into a container inside the
      // ToolbarRoot its keydowns bubble into the toolbar, so only the popup's own stop (the popup module's
      // StopCompositeKeys) keeps the toolbar's focus where it is.
      test('port: a dialog popup portaled into a container inside the toolbar stops the composite keydowns', async ({ page, mode }) => {
        await dialogCase(page, mode, 'composite-inside');
        const trigger = page.getByRole('button', { name: 'dialog', exact: true });

        await trigger.click();
        const popup = page.locator('[data-portal-target] [role="dialog"]');
        await expect(popup).toBeFocused();
        await page.keyboard.press('ArrowRight');
        await page.keyboard.press('End');

        await expect(popup).toBeFocused();
        await expect(page.getByRole('button', { name: 'empty', includeHidden: true })).not.toBeFocused();
      });
    });

    test.describe('Switch', () => {
      // TLB-STR-05, TLB-ARIA-09. Deviation (design point 14.7, option (c), chosen by the user 2026-10-27; the
      // non-native shape is open to issue 115): the switch is native, `NativeButton="true"` over a `<button>`, because
      // a non-native one puts the id it receives on its hidden input; so the switch element is the toolbar's item, and
      // the declared-mode check, which Base UI's span shape trips once, reports nothing (TLB-EDGE-02 is asserted at
      // L2).
      test('renders a switch', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-switch');
        const { item } = toolbarCase(fixture, 'renders');

        await expect(item('button')).toHaveAttribute('role', 'switch');
        await expect(item('button')).toHaveAttribute('tabindex', '0');
        await settle(fixture);
        const mismatches = (await readDiagnostics(page, mode, fixture)).filter((d) => d.message.startsWith(MISMATCH_MESSAGE));
        expect(mismatches).toHaveLength(0);
      });

      // TLB-KEY-12, TLB-PTR-01, TLB-DOM-05. Over the native switch, Enter is the browser's activation and Space the toolbar
      // button's keydown click (composite mode); the switch's own handle (composite: false on main, issue 107) leaves
      // both to them, so one click per press.
      test('handles interactions', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-switch');
        const { container, item, before } = toolbarCase(fixture, 'interactions');
        const switchElement = item('button');
        await expect(switchElement).toHaveAttribute('data-unchecked');

        await tabIn(page, before);
        await expect(switchElement).toBeFocused();
        await expect(switchElement).toHaveAttribute('tabindex', '0');

        await switchElement.click();
        await settle(fixture);
        await expect(container).toHaveAttribute('data-changes', '1');
        await expect(container).toHaveAttribute('data-clicks', '1');
        await expect(switchElement).toHaveAttribute('data-checked');

        await page.keyboard.press('Enter');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-changes', '2');
        await expect(container).toHaveAttribute('data-clicks', '2');
        await expect(switchElement).toHaveAttribute('data-unchecked');

        await page.keyboard.press('Space');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-changes', '3');
        await expect(container).toHaveAttribute('data-clicks', '3');
        await expect(switchElement).toHaveAttribute('data-checked');
      });

      // TLB-DOM-08, TLB-KEY-13, TLB-PTR-01, TLB-FOC-03: the native switch renders `disabled`, which the toolbar button's
      // handle clears while it is focusable when disabled (design point 14.1).
      test('disabled state', async ({ page, mode }) => {
        const fixture = await gotoToolbar(page, mode, 'toolbar-switch');
        const { container, item, before } = toolbarCase(fixture, 'disabled');
        const switchElement = item('button');

        await expect(switchElement).not.toHaveAttribute('disabled');
        await expect(switchElement).toHaveAttribute('data-disabled');
        await expect(switchElement).toHaveAttribute('aria-disabled', 'true');

        await tabIn(page, before);
        await expect(switchElement).toBeFocused();
        await expect(switchElement).toHaveAttribute('tabindex', '0');

        await page.keyboard.press('Enter');
        await page.keyboard.press('Space');
        await switchElement.click({ force: true });
        await settle(fixture);

        await expect(container).toHaveAttribute('data-changes', '0');
        await expect(container).toHaveAttribute('data-clicks', '0');
      });
    });
  });
});
