import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The L4 rows and clauses of docs/components/toggle.md that are Toggle's own: the default-prevented disabled click
// (TGL-PTR-02), keyboard activation once per press (TGL-EVT-06, TGL-KEY-01), the consumer vetoes per mode (TGL-PTR-03, design
// point 12.2), Home and End through the item's gate (design point 12.3), the bare toggle under a composite root
// (TGL-EDGE-09) and TGL-DOM-05 as re-tagged ("should pass composite props" on the DOM attribute). The grouped titles of
// ToggleGroup.test.tsx that toggle.md cites (TGL-KEY-02, TGL-KEY-03, TGL-FOC-02) run in toggle-group.spec.ts, where Base
// UI has them; the toolbar rows (TGL-KEY-05, TGL-FOC-03 and the toolbar clauses) are M3 track 4's. "port:" titles assert
// the per-mode rows of ADR 01's relaxation table and the Test: none rows a browser observes.

type FixtureName = 'toggle-standalone' | 'toggle-composite-root' | 'toggle-group-basic';

/** Opens a Toggle fixture and waits until every toggle and root has its behaviors attached. */
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
    toggle: container.locator('[data-toggle]').first(),
    byValue: (value: string) => container.locator(`[data-toggle][data-value="${value}"]`),
    item: (label: string) => container.locator(`[data-item="${label}"]`),
    bound: container.locator('[data-bound]'),
  };
}

/** Moves focus as a script does, without a pointer: no click, so no activation. */
async function focus(element: Locator): Promise<void> {
  await element.evaluate((node) => (node as HTMLElement).focus());
}

/** Records, from a bubble listener on the case, whether each click and pointerdown reaching it was default-prevented. */
async function recordDefaults(container: Locator): Promise<void> {
  await container.evaluate((element) => {
    const records: string[] = [];
    (element as unknown as { defaults: string[] }).defaults = records;
    for (const type of ['click', 'pointerdown']) {
      element.addEventListener(type, (event) => records.push(`${type}:${event.defaultPrevented}`));
    }
  });
}

async function defaults(container: Locator): Promise<string[]> {
  return container.evaluate((element) => (element as unknown as { defaults: string[] }).defaults);
}

test.describe('<Toggle />', () => {
  test.describe('prop: render', () => {
    // TGL-DOM-05 as re-tagged 2026-10-26 (ADR 04 Amendments 2026-10-22): Toggle.test.tsx reads `tabIndex: 0` from the
    // render callback's props; the port's callback receives C#'s `-1` and the composite writes the roving value on the
    // element from attach, so the title is restated on the DOM attribute.
    test('port: should pass composite props (the toggle a render function renders holds the tab stop)', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { byValue } = toggleCase(fixture, 'render');

      await expect(byValue('left')).toHaveAttribute('tabindex', '0');
      await expect(byValue('left')).toHaveAttribute('aria-pressed', 'true');
      await expect(byValue('left')).toHaveAttribute('data-pressed-state', 'true');
      await expect(byValue('left')).toHaveAttribute('id', /^base-ui-/);
    });
  });

  test.describe('prop: disabled', () => {
    // TGL-PTR-02's default-prevention clause, TGL-PTR-04, TGL-EVT-08 (UBT-PTR-01..03): a script's click on a disabled
    // non-native toggle is default-prevented and runs neither the toggle nor the consumer's onclick; its pointerdown is
    // default-prevented and the consumer's onpointerdown does not run. A disabled native button receives no click at all.
    test("port: a disabled toggle's click is default-prevented and runs neither the toggle nor the consumer's handlers", async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-standalone');
      const nonNative = toggleCase(fixture, 'disabled-non-native');
      const native = toggleCase(fixture, 'disabled-native');
      await recordDefaults(nonNative.container);
      await recordDefaults(native.container);

      await nonNative.toggle.evaluate((element) => (element as HTMLElement).click());
      await nonNative.toggle.dispatchEvent('pointerdown', { bubbles: true, cancelable: true });
      await native.toggle.evaluate((element) => (element as HTMLElement).click());
      await settle(fixture);

      expect(await defaults(nonNative.container)).toEqual(['click:true', 'pointerdown:true']);
      expect(await defaults(native.container)).toEqual([]);
      for (const { container, toggle } of [nonNative, native]) {
        await expect(container).toHaveAttribute('data-presses', '0');
        await expect(container).toHaveAttribute('data-clicks', '0');
        await expect(container).toHaveAttribute('data-pointerdowns', '0');
        await expect(toggle).toHaveAttribute('aria-pressed', 'false');
      }
    });
  });

  test.describe('keyboard', () => {
    // TGL-EVT-06's keyboard clause, TGL-KEY-01 (UBT-KEY-01/02/09): standalone, a native toggle is clicked by the browser
    // (Enter on keydown, Space on keyup), a non-native one by the module (Enter on keydown, Space on keyup); one
    // OnPressedChange per press either way.
    test('port: Enter and Space activate a standalone toggle once per press, native and non-native', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-standalone');

      for (const name of ['native', 'non-native']) {
        const { container, toggle } = toggleCase(fixture, name);
        await focus(toggle);

        await page.keyboard.press('Enter');
        await expect(container).toHaveAttribute('data-presses', '1');
        await expect(toggle).toHaveAttribute('aria-pressed', 'true');

        await page.keyboard.down(' ');
        await settle(fixture);
        await expect(container).toHaveAttribute('data-presses', '1');

        await page.keyboard.up(' ');
        await expect(container).toHaveAttribute('data-presses', '2');
        await expect(toggle).toHaveAttribute('aria-pressed', 'false');
        await expect(container).toHaveAttribute('data-last-press', 'pressed=false type=click detail=0 reason=none trigger=none');
      }
    });

    // TGL-API-01/TGL-STA-05 through @bind-Pressed in a browser: the consumer's state follows each activation.
    test('port: a toggle bound with @bind-Pressed follows its activations', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-standalone');
      const { container, toggle, bound } = toggleCase(fixture, 'controlled');

      await toggle.click();
      await expect(bound).toHaveAttribute('data-bound', 'true');
      await expect(toggle).toHaveAttribute('aria-pressed', 'true');

      await toggle.click();
      await expect(bound).toHaveAttribute('data-bound', 'false');
      await expect(toggle).toHaveAttribute('aria-pressed', 'false');
      await expect(container).toHaveAttribute('data-presses', '2');
    });
  });

  test.describe('vetoes', () => {
    // TGL-PTR-03, design point 12.2 (a), settled by the orchestrator 2026-10-26: the consumer's C# onclick is not declined, so it runs before the
    // toggle's in the same Blazor dispatch and its PreventPartHandler() keeps the toggle unpressed on both modes.
    test('port: a C# onclick that calls PreventPartHandler() keeps the toggle unpressed, on both modes', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-standalone');
      const { container, toggle } = toggleCase(fixture, 'veto-click');

      await toggle.click();
      await focus(toggle);
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '2');
      await expect(container).toHaveAttribute('data-presses', '0');
      await expect(toggle).toHaveAttribute('aria-pressed', 'false');
    });

    // ADR 01's row "C#-origin consumer vetoes" (UBT-KEY-03/04), design point 12.2: a C# onkeydown's PreventPartHandler()
    // answers the `activate` decision inside the dispatch on WebAssembly, so Enter does not activate a non-native toggle;
    // on Server the decision falls back to run, the toggle activates, and the handler runs once, a round trip late.
    test('port: a C# onkeydown that calls PreventPartHandler() vetoes keyboard activation on WebAssembly and is too late on Server', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-standalone');
      const { container, toggle } = toggleCase(fixture, 'veto-key');

      await focus(toggle);
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-keydowns', '1');
      await expect(container).toHaveAttribute('data-presses', mode === 'wasm' ? '0' : '1');
      await expect(toggle).toHaveAttribute('aria-pressed', mode === 'wasm' ? 'false' : 'true');
    });

    // Design point 12.3 (a), settled by the orchestrator 2026-10-26: in a group, Home and End take the item's gate as the arrows do, so a
    // consumer's veto of Home inside `navigate` on WebAssembly leaves the one-shot marker where Blazor's dispatch of the
    // key reads it and the handler runs once; on Server the module moves focus and the handled-key report runs it once.
    test("port: a vetoed Home runs the toggle's C# onkeydown once", async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-group-basic');
      const { container, byValue } = toggleCase(fixture, 'toggle-keydown');

      await focus(byValue('three'));
      await page.keyboard.press('Home');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-log', 'toggle:Home');
      await expect(byValue(mode === 'wasm' ? 'three' : 'one')).toBeFocused();
    });
  });

  test.describe('composite button mode', () => {
    // TGL-EDGE-09 (toggle §16 Q1, chosen by the user 2026-10-26), UBT-STA-01: under a composite root without a group the
    // toggle infers composite mode (Space on keydown, no tabindex of its own) and is not an item: the root's arrows skip
    // it and a native button keeps its sequential tab stop.
    test('port: a bare toggle under a composite root activates Space on keydown and is skipped by the root\'s arrows', async ({ page, mode }) => {
      const fixture = await gotoToggles(page, mode, 'toggle-composite-root');
      const { container, toggle, item } = toggleCase(fixture, 'bare');
      await expect(toggle).not.toHaveAttribute('tabindex');
      await expect(toggle).toHaveAttribute('aria-disabled', 'false');

      await focus(item('1'));
      await page.keyboard.press('ArrowRight');
      await expect(item('2')).toBeFocused();

      await focus(item('1'));
      await page.keyboard.press('Tab');
      await expect(toggle).toBeFocused();

      await page.keyboard.down(' ');
      await expect(toggle).toHaveAttribute('aria-pressed', 'true');
      await page.keyboard.up(' ');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-presses', '1');
      await expect(toggle).toHaveAttribute('aria-pressed', 'true');
    });
  });
});
