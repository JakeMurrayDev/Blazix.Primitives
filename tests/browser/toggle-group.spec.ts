import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/toggle-group.md and the L4 clauses of its [L2] rows, with the Toggle-owned rows their
// titles prove (toggle.md §13: TGL-KEY-02, TGL-KEY-03, TGL-DOM-05, TGL-FOC-02), on ToggleGroup over the `composite`
// module and Toggle over the `button` module. Titles are Base UI's (toggle-group/ToggleGroup.test.tsx); "port:" titles
// assert the per-mode rows of ADR 01's relaxation table, the Test: none rows a browser observes and the design points of
// M3 tracks 2-3. The rows that need a Toolbar (TGG-KEY-10, TGG-FOC-05, the "nested in Toolbar.Group" variant) are M3
// track 4's. The fixtures record the callbacks as data attributes; every page waits for `data-attached` (every twin
// attached) and uses the sync button as a barrier.

type FixtureName = 'toggle-group-orientation' | 'toggle-group-basic';

/** Opens a ToggleGroup fixture and waits until every group and toggle has its behaviors attached. */
async function gotoToggles(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
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

function toggleCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    group: container.locator('[data-group]').first(),
    toggle: (value: string) => container.locator(`[data-toggle][data-value="${value}"]`),
    toggles: container.locator('[data-toggle]'),
    before: container.locator('[data-before]'),
    after: container.locator('[data-after]'),
    flip: container.locator('[data-flip]'),
    reorder: container.locator('[data-reorder]'),
  };
}

/** Moves focus as a script does, without a pointer: no click, so no activation. */
async function focus(element: Locator): Promise<void> {
  await element.evaluate((node) => (node as HTMLElement).focus());
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

/** One move of the tab stop or of the focus inside a group, with the other one's position at that moment. */
interface MoveRecord {
  kind: 'tab-stop' | 'focus';
  tabStop: string | null;
  focused: string | null;
}

/**
 * Records, as markers the page keeps (ADR 06 §2), each write of `tabindex="0"` inside the group (a mutation record) and each
 * focus arriving inside it, with where the other one was at that moment.
 */
async function recordMoves(group: Locator): Promise<void> {
  await group.evaluate((element) => {
    const records: MoveRecord[] = [];
    (element as unknown as { moves: MoveRecord[] }).moves = records;
    const label = (node: Element | null | undefined) => node?.getAttribute('data-value') ?? null;
    const tabStop = () => label(element.querySelector('[data-toggle][tabindex="0"]'));
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

async function moveRecords(group: Locator): Promise<MoveRecord[]> {
  return group.evaluate((element) => (element as unknown as { moves: MoveRecord[] }).moves);
}

/** Records each keydown reaching the group: its key, whether its default was prevented and whether it reached the document. */
async function recordKeys(group: Locator): Promise<void> {
  await group.evaluate((element) => {
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

test.describe('<ToggleGroup />', () => {
  test.describe('prop: multiple transitions', () => {
    // TGG-STR-02, TGG-STA-05, TGG-DOM-04, TGG-DOM-06, TGG-PTR-01, TGG-FOC-03, TGL-FOC-02. The "nested in Toolbar.Group"
    // variant is M3 track 4's.
    test('preserves selection and roving focus when standalone', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { group, toggle, before, flip } = toggleCase(fixture, 'multiple');

      await expect(group).not.toHaveAttribute('data-multiple');
      await expect(toggle('one')).toHaveAttribute('aria-pressed', 'true');
      await expect(toggle('two')).toHaveAttribute('aria-pressed', 'false');

      await focus(before);
      await page.keyboard.press('Tab');
      await page.keyboard.press('ArrowRight');
      await expect(toggle('two')).toBeFocused();

      await toggle('two').click();
      await expect(toggle('one')).toHaveAttribute('aria-pressed', 'false');
      await expect(toggle('two')).toHaveAttribute('aria-pressed', 'true');

      await flip.click();
      await expect(group).toHaveAttribute('data-multiple', '');

      await toggle('one').click();
      await expect(toggle('one')).toHaveAttribute('aria-pressed', 'true');
      await expect(toggle('two')).toHaveAttribute('aria-pressed', 'true');

      await toggle('two').click();
      await expect(toggle('one')).toHaveAttribute('aria-pressed', 'true');
      await expect(toggle('two')).toHaveAttribute('aria-pressed', 'false');

      await flip.click();
      await expect(group).not.toHaveAttribute('data-multiple');

      await toggle('two').click();
      await expect(toggle('one')).toHaveAttribute('aria-pressed', 'false');
      await expect(toggle('two')).toHaveAttribute('aria-pressed', 'true');

      await page.keyboard.press('ArrowLeft');
      await expect(toggle('one')).toBeFocused();
    });
  });

  test.describe('keyboard interactions', () => {
    const arrowKeyCases = [
      { direction: 'ltr', orientation: 'horizontal', nextKey: 'ArrowRight', prevKey: 'ArrowLeft', ignoredNextKey: 'ArrowDown', ignoredPrevKey: 'ArrowUp' },
      { direction: 'ltr', orientation: 'vertical', nextKey: 'ArrowDown', prevKey: 'ArrowUp', ignoredNextKey: 'ArrowRight', ignoredPrevKey: 'ArrowLeft' },
      { direction: 'rtl', orientation: 'horizontal', nextKey: 'ArrowLeft', prevKey: 'ArrowRight', ignoredNextKey: 'ArrowDown', ignoredPrevKey: 'ArrowUp' },
      { direction: 'rtl', orientation: 'vertical', nextKey: 'ArrowDown', prevKey: 'ArrowUp', ignoredNextKey: 'ArrowLeft', ignoredPrevKey: 'ArrowRight' },
    ] as const;

    // TGG-KEY-01/02/03, TGG-API-05/06, TGG-DOM-05, TGG-FOC-01/02/03, TGG-STA-08, TGG-EDGE-08 (each group also carries the
    // opposite `dir` attribute); TGL-KEY-03, TGL-DOM-05.
    for (const { direction, orientation, nextKey, prevKey, ignoredNextKey, ignoredPrevKey } of arrowKeyCases) {
      test(`${direction}, orientation: ${orientation} moves focus with arrow keys`, async ({ page, mode }) => {
        const fixture = await gotoToggles(page, mode, 'toggle-group-orientation');
        const { group, toggle, before } = toggleCase(fixture, `${direction}-${orientation}`);
        const [button1, button2, button3] = [toggle('one'), toggle('two'), toggle('three')];
        await expect(group).not.toHaveAttribute('tabindex');

        await focus(before);
        await page.keyboard.press('Tab');

        await expect(button1).toHaveAttribute('tabindex', '0');
        await expect(button1).toBeFocused();

        await page.keyboard.press(nextKey);

        await expect(button2).toHaveAttribute('tabindex', '0');
        await expect(button2).toBeFocused();

        await page.keyboard.press(nextKey);

        await expect(button3).toHaveAttribute('tabindex', '0');
        await expect(button3).toBeFocused();

        // loop to the beginning
        await page.keyboard.press(nextKey);

        await expect(button1).toHaveAttribute('tabindex', '0');
        await expect(button1).toBeFocused();

        await page.keyboard.press(prevKey);

        await expect(button3).toHaveAttribute('tabindex', '0');
        await expect(button3).toBeFocused();

        await page.keyboard.press(prevKey);

        await expect(button2).toHaveAttribute('tabindex', '0');
        await expect(button2).toBeFocused();

        // keys from the other axis should not move focus
        await page.keyboard.press(ignoredNextKey);

        await expect(button2).toHaveAttribute('tabindex', '0');
        await expect(button2).toBeFocused();

        await page.keyboard.press(ignoredPrevKey);

        await expect(button2).toHaveAttribute('tabindex', '0');
        await expect(button2).toBeFocused();
      });
    }

    // TGG-KEY-04, TGG-FOC-01.
    test('Home key moves focus to the first item', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { toggle, before } = toggleCase(fixture, 'home-end');
      const [button1, button2, button3] = [toggle('one'), toggle('two'), toggle('three')];

      await focus(before);
      await page.keyboard.press('Tab');
      await expect(button1).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      await expect(button3).toBeFocused();

      await page.keyboard.press('Home');
      await expect(button1).toHaveAttribute('tabindex', '0');
      await expect(button1).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(button2).toBeFocused();

      await page.keyboard.press('Home');
      await expect(button1).toHaveAttribute('tabindex', '0');
      await expect(button1).toBeFocused();
    });

    test('End key moves focus to the last item', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { toggle, before } = toggleCase(fixture, 'home-end');
      const [button1, button2, button3] = [toggle('one'), toggle('two'), toggle('three')];

      await focus(before);
      await page.keyboard.press('Tab');
      await expect(button1).toBeFocused();

      await page.keyboard.press('End');
      await expect(button3).toHaveAttribute('tabindex', '0');
      await expect(button3).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(button2).toBeFocused();

      await page.keyboard.press('End');
      await expect(button3).toHaveAttribute('tabindex', '0');
      await expect(button3).toBeFocused();
    });

    // TGG-KEY-09, TGG-FOC-03 (programmatic focus); TGL-KEY-02: Space activates a grouped toggle on keydown.
    for (const key of ['Enter', 'Space']) {
      test(`key: ${key} toggles the pressed state`, async ({ page, mode }) => {
        const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
        const { toggle } = toggleCase(fixture, 'keys');
        const button1 = toggle('one');

        await expect(button1).toHaveAttribute('aria-pressed', 'false');

        await focus(button1);

        await page.keyboard.press(key);

        await expect(button1).toHaveAttribute('aria-pressed', 'true');

        await page.keyboard.press(key);

        await expect(button1).toHaveAttribute('aria-pressed', 'false');
      });
    }
  });

  test.describe('prop: onValueChange', () => {
    // TGG-EVT-06, TGG-KEY-09; TGL-KEY-02, TGL-EVT-06 (one call per activation).
    for (const key of ['Enter', 'Space']) {
      test(`fires when the ${key} is pressed`, async ({ page, mode }) => {
        const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
        const { container, toggle } = toggleCase(fixture, 'keys');
        const [button1, button2] = [toggle('one'), toggle('two')];

        await expect(container).toHaveAttribute('data-changes', '0');

        await focus(button1);

        await page.keyboard.press(key);

        await expect(container).toHaveAttribute('data-changes', '1');
        await expect(container).toHaveAttribute('data-last-change', 'one');

        await focus(button2);

        await page.keyboard.press(key);

        await expect(container).toHaveAttribute('data-changes', '2');
        await expect(container).toHaveAttribute('data-last-change', 'two');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-presses', '2');
      });
    }
  });

  test.describe('port', () => {
    // TGG-API-06 (L4 clause), TGG-KEY-05: no wrap at either end, the key not consumed.
    test('port: with LoopFocus false the arrows stop at either end', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { group, toggle } = toggleCase(fixture, 'no-loop');
      await recordKeys(group);

      await focus(toggle('one'));
      await page.keyboard.press('ArrowLeft');
      await expect(toggle('one')).toBeFocused();

      await page.keyboard.press('End');
      await expect(toggle('three')).toBeFocused();
      await page.keyboard.press('ArrowRight');
      await expect(toggle('three')).toBeFocused();
      await settle(fixture);

      const keys = await group.evaluate((element) => (element as unknown as { keys: { key: string; defaultPrevented: boolean }[] }).keys);
      expect(keys.map(({ key, defaultPrevented }) => `${key}:${defaultPrevented}`)).toEqual(['ArrowLeft:false', 'End:true', 'ArrowRight:false']);
    });

    // TGG-FOC-01 (L4 clause), TGG-DOM-05: one tab stop; Tab enters on it and leaves in one press.
    test('port: Tab enters the group on the toggle holding the tab stop and leaves it in one press', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { group, toggle, before, after } = toggleCase(fixture, 'home-end');
      await expect(group).not.toHaveAttribute('tabindex');

      await focus(before);
      await page.keyboard.press('Tab');
      await expect(toggle('one')).toBeFocused();
      await page.keyboard.press('ArrowRight');
      await expect(toggle('two')).toHaveAttribute('tabindex', '0');

      await page.keyboard.press('Tab');
      await expect(after).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(toggle('two')).toBeFocused();
    });

    // TGG-FOC-02 (L4 clause), CMP-STA-24: a disabled native toggle is not focusable, and the first publication's
    // re-validation moves the initial tab stop to the first enabled toggle (Server: after paint plus a round trip, the
    // CMP-STA-10 row).
    test('port: with the first toggle disabled, the first enabled toggle takes the initial tab stop', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { toggle, before } = toggleCase(fixture, 'disabled-first');

      await expect(toggle('one')).toBeDisabled();
      await expect(toggle('two')).toHaveAttribute('tabindex', '0');
      await expect(toggle('one')).toHaveAttribute('tabindex', '-1');

      await focus(before);
      await page.keyboard.press('Tab');
      await expect(toggle('two')).toBeFocused();
    });

    // TGG-KEY-08, TGL-FOC-03 (the standalone clause): a disabled native toggle carries `disabled` and is skipped.
    test('port: the arrow keys skip a disabled toggle', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { toggle } = toggleCase(fixture, 'disabled-middle');

      await focus(toggle('one'));
      await page.keyboard.press('ArrowRight');
      await expect(toggle('three')).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(toggle('one')).toBeFocused();
    });

    // TGG-FOC-04, CMP-STA-26/27: the tab stop follows the toggle that held it (Server: the registry update lands a round
    // trip after the reorder, the CMP-STA-10 row).
    test('port: the tab stop follows its toggle when the toggles are reordered', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { toggle, toggles, reorder } = toggleCase(fixture, 'reorder');

      await focus(toggle('one'));
      await expect(toggle('one')).toHaveAttribute('tabindex', '0');

      await reorder.click();
      await expect(toggles.first()).toHaveAttribute('data-value', 'three');

      await expect(toggle('one')).toHaveAttribute('tabindex', '0');
      await expect(toggle('three')).toHaveAttribute('tabindex', '-1');
      await expect(toggle('two')).toHaveAttribute('tabindex', '-1');
    });

    // TGG-FOC-03's key-move clause, CMP-FOC-02, CMP-KEY-16 (ADR 04 Amendments 2026-10-22): the composite writes the tab
    // stop inside the keydown's task and focuses a microtask later, so the two stay together on both modes; Server latency
    // is added to show that no round trip is involved.
    test('port: after an arrow key the focus and the tab stop move in the same task, on both modes', async ({ page, mode }) => {
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { group, toggle } = toggleCase(fixture, 'home-end');

      await focus(toggle('one'));
      await expect(toggle('one')).toHaveAttribute('tabindex', '0');
      await recordMoves(group);

      await page.keyboard.press('ArrowRight');
      await expect(toggle('two')).toBeFocused();
      await expect(toggle('two')).toHaveAttribute('tabindex', '0');

      const moves = await moveRecords(group);
      expect(moves.slice(0, 2)).toEqual([
        { kind: 'tab-stop', tabStop: 'two', focused: 'one' },
        { kind: 'focus', tabStop: 'two', focused: 'two' },
      ]);
    });

    // TGG-EDGE-09, TGG-FOC-02, TGL-EDGE-08, TGL-DOM-05 (the CMP-STA-10 row of ADR 01's relaxation table): C# renders
    // tabindex="-1" on every toggle and the first publication moves the tab stop to the first one; on Server that lands a
    // round trip after the render, so at least one frame is painted without a tab stop. Recorded per animation frame.
    test('port: grouped toggles render tabindex -1 until the first publication, a round trip later on Server', async ({ page, mode }) => {
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      await page.addInitScript(() => {
        const frames: (string | null)[] = [];
        (window as unknown as { toggleFrames: (string | null)[] }).toggleFrames = frames;
        const tick = () => {
          const element = document.querySelector('[data-case="home-end"] [data-toggle]');
          if (element) {
            frames.push(element.getAttribute('tabindex'));
          }
          if (frames.at(-1) !== '0' && frames.length < 5000) {
            requestAnimationFrame(tick);
          }
        };
        requestAnimationFrame(tick);
      });
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { toggle } = toggleCase(fixture, 'home-end');
      await expect(toggle('one')).toHaveAttribute('tabindex', '0');

      const frames = await page.evaluate(() => (window as unknown as { toggleFrames: (string | null)[] }).toggleFrames);
      expect(frames.at(-1)).toBe('0');
      expect(frames.slice(0, -1).every((value) => value === '-1')).toBe(true);
      if (mode === 'server') {
        expect(frames.length).toBeGreaterThan(1);
      }
    });

    // TGG-API-09 (L4 clause), ADR 01's row "… EVD-EVT-26 against composite/button key handling …" with option (i): the
    // group's C# onkeydown runs inside `navigate` on WebAssembly, where PreventPartHandler() vetoes the move; on Server it
    // runs once from the handled-key report, a round trip late, and the move has happened.
    test("port: the group's C# onkeydown vetoes a handled arrow on WebAssembly and runs a round trip late on Server", async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { container, toggle } = toggleCase(fixture, 'group-keydown');

      await focus(toggle('one'));
      await page.keyboard.press('ArrowRight');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-log', 'group:ArrowRight');
      await expect(toggle(mode === 'wasm' ? 'one' : 'two')).toBeFocused();
    });

    // TGG-DOM-06, ADR 01 and ADR 04 Amendments 2026-10-26 (option (i)'s precision): a C# onkeydown on an element between a
    // toggle and its group never runs for a key the module handles, and runs for one it does not.
    test('port: a C# onkeydown on a wrapper between the toggles and the group runs for an unhandled key and not for a handled arrow', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { container, toggle } = toggleCase(fixture, 'wrapper-keydown');

      await focus(toggle('one'));
      await page.keyboard.press('ArrowRight');
      await expect(toggle('two')).toBeFocused();
      await page.keyboard.press('a');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-log', 'wrapper:a');
    });

    // TGG-KEY-06, CMP-KEY-10: a handled key is default-prevented and stopped; Enter, which the group does not handle, is neither.
    test('port: a handled arrow is default-prevented and stopped, and Enter is neither', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { group, toggle } = toggleCase(fixture, 'home-end');
      await recordKeys(group);

      await focus(toggle('one'));
      await page.keyboard.press('ArrowRight');
      await expect(toggle('two')).toBeFocused();
      await page.keyboard.press('Enter');
      await settle(fixture);

      const [arrow, enter] = await group.evaluate((element) => (element as unknown as { keys: unknown[] }).keys);
      expect(arrow).toMatchObject({ key: 'ArrowRight', defaultPrevented: true, reachedDocument: false });
      expect(enter).toMatchObject({ key: 'Enter', defaultPrevented: false, reachedDocument: true });
    });

    // TGG-PTR-03, TGL-FOC-04: the group does not highlight on hover.
    test('port: moving the pointer over a toggle neither focuses it nor moves the tab stop', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { toggle } = toggleCase(fixture, 'home-end');
      await expect(toggle('one')).toHaveAttribute('tabindex', '0');

      await toggle('three').hover();
      await settle(fixture);

      await expect(toggle('three')).not.toBeFocused();
      await expect(toggle('three')).toHaveAttribute('tabindex', '-1');
      await expect(toggle('one')).toHaveAttribute('tabindex', '0');
    });
  });
});
