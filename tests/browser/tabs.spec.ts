import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/tabs.md, on TabsList over the `composite` module, TabsTab over the `button` module,
// TabsPanel over the `transitions` module and the `tabs` component module's two handles (ADR 04 Amendments 2026-10-26).
// Titles are Base UI's (tabs/root/TabsRoot.test.tsx, list/TabsList.test.tsx, tab/TabsTab.test.tsx, panel/TabsPanel.test.tsx,
// indicator/TabsIndicator.test.tsx); "port:" titles assert the per-mode rows of ADR 01's relaxation table and the module's
// port points. The fixtures record OnValueChange and the consumer's handlers as data attributes; every page waits for
// `data-attached` (every twin attached) and uses the sync button as a barrier. Transient states are witnessed by recorded
// markers (mutation records, `transitionrun`, `animationstart`), never by frame polls.

type FixtureName = 'tabs-keyboard' | 'tabs-list' | 'tabs-activation' | 'tabs-direction' | 'tabs-panels' | 'tabs-indicator' | 'tabs-focus';

/** Opens a Tabs fixture with a query and waits until every list, tab, panel and indicator has its behaviors attached. */
async function gotoTabs(page: Page, mode: RenderMode, name: FixtureName, query: Record<string, string | number | boolean> = {}): Promise<Locator> {
  const search = new URLSearchParams(Object.entries(query).map(([key, value]) => [key, String(value)])).toString();
  await page.goto(`${fixtureUrl(mode, name)}${search ? `?${search}` : ''}`, { waitUntil: 'commit' });
  await page.locator('html[data-blazix-started]').waitFor({ state: 'attached' });
  await page.locator(`[data-fixture="${name}"][data-interactive="true"]`).first().waitFor({ state: 'attached' });
  const fixture = page.locator(`[data-fixture="${name}"]`);
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

function tabsCase(fixture: Locator, name = 'main') {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    root: container.locator('[data-root]').first(),
    list: container.locator('[role="tablist"]').first(),
    tabs: container.locator('[role="tab"]'),
    tab: (value: string | number) => container.locator(`[data-tab="${value}"]`),
    panels: container.locator('[role="tabpanel"]'),
    panel: (value: string | number) => container.locator(`[data-panel="${value}"]`),
    indicator: (name = 'indicator') => container.locator(`[data-indicator="${name}"]`),
    before: container.locator('[data-before]'),
    after: container.locator('[data-after]'),
    button: (name: string) => container.locator(`[data-action="${name}"]`),
  };
}

/** Moves focus as a script does, without a pointer. */
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

/** Records each keydown reaching the list: its key and whether its default was prevented, read after the module's listener. */
async function recordKeys(list: Locator): Promise<void> {
  await list.evaluate((element) => {
    const records: { key: string; defaultPrevented: boolean }[] = [];
    (element as unknown as { keys: typeof records }).keys = records;
    // Added after the composite module's listener on the same element, so it reads the module's decision.
    element.addEventListener('keydown', (event) => {
      records.push({ key: (event as KeyboardEvent).key, defaultPrevented: event.defaultPrevented });
    });
  });
}

async function keyRecords(list: Locator): Promise<{ key: string; defaultPrevented: boolean }[]> {
  return list.evaluate((element) => (element as unknown as { keys: { key: string; defaultPrevented: boolean }[] }).keys);
}

interface MoveRecord {
  kind: 'tab-stop' | 'focus';
  tabStop: string | null;
  focused: string | null;
}

/**
 * Records, as markers the page keeps (ADR 06 §2), each write of `tabindex="0"` inside the List (a mutation record,
 * delivered in the microtask after the write) and each focus arriving inside it, with where the other one was at that
 * moment, as composite.spec.ts does.
 */
async function recordMoves(list: Locator): Promise<void> {
  await list.evaluate((element) => {
    const records: MoveRecord[] = [];
    (element as unknown as { moves: MoveRecord[] }).moves = records;
    const label = (node: Element | null | undefined) => node?.getAttribute('data-tab') ?? null;
    const tabStop = () => label(element.querySelector('[data-tab][tabindex="0"]'));
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

async function moveRecords(list: Locator): Promise<MoveRecord[]> {
  return list.evaluate((element) => (element as unknown as { moves: MoveRecord[] }).moves);
}

async function tabIndexes(tabs: Locator): Promise<(string | null)[]> {
  return tabs.evaluateAll((elements) => elements.map((element) => element.getAttribute('tabindex')));
}

const keyboardCases = [
  { name: 'horizontal ltr', orientation: 'horizontal', direction: 'ltr', previousItemKey: 'ArrowLeft', nextItemKey: 'ArrowRight' },
  { name: 'horizontal rtl', orientation: 'horizontal', direction: 'rtl', previousItemKey: 'ArrowRight', nextItemKey: 'ArrowLeft' },
  { name: 'vertical', orientation: 'vertical', direction: 'ltr', previousItemKey: 'ArrowUp', nextItemKey: 'ArrowDown' },
] as const;

test.describe('<Tabs.Root />', () => {
  test.describe('keyboard navigation when focus is on a tab', () => {
    for (const { name, orientation, direction, previousItemKey, nextItemKey } of keyboardCases) {
      test.describe(`when focus is on a tab element in a ${name} tablist`, () => {
        const base = { orientation, direction };

        /** Presses one key on a focused tab and asserts the focus, the List's key handler and the key's default. */
        async function press(page: Page, mode: RenderMode, query: Record<string, string | number | boolean>, from: number, key: string, to: number) {
          const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { ...base, ...query });
          const { container, list, tab } = tabsCase(fixture);
          await recordKeys(list);
          await focus(tab(from));
          await page.keyboard.press(key);
          await expect(tab(to)).toBeFocused();
          // TAB-KEY-09: the List's consumer onkeydown runs once for the key (inside `navigate` on WebAssembly, from the
          // handled-key report a round trip later on Server: ADR 01's EVD-EVT-26 row) and the key's default is prevented.
          await expect(container).toHaveAttribute('data-list-keydowns', '1');
          expect(await keyRecords(list)).toEqual([{ key, defaultPrevented: true }]);
          return { fixture, container, tab };
        }

        test.describe(previousItemKey, () => {
          test.describe('with `activateOnFocus = false`', () => {
            test('moves focus to the last tab without activating it if focus is on the first tab', async ({ page, mode }) => {
              const { fixture, container } = await press(page, mode, { value: 0 }, 0, previousItemKey, 2);
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
            });

            test('moves focus to the previous tab without activating it', async ({ page, mode }) => {
              const { fixture, container } = await press(page, mode, { value: 1 }, 1, previousItemKey, 0);
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
            });

            test('moves focus to a disabled tab without activating it', async ({ page, mode }) => {
              const { fixture, container, tab } = await press(page, mode, { value: 2, disabled: '1' }, 2, previousItemKey, 1);
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
              await expect(tab(1)).toHaveAttribute('aria-disabled', 'true');
            });
          });

          test.describe('with `activateOnFocus = true`', () => {
            test('moves focus to the last tab while activating it if focus is on the first tab', async ({ page, mode }) => {
              const { container, tab } = await press(page, mode, { value: 0, activateOnFocus: true }, 0, previousItemKey, 2);
              await expect(container).toHaveAttribute('data-changes', '1');
              await expect(container).toHaveAttribute('data-log', /change:2/);
              // TAB-KEY-14: the controlled value is not applied by the parent, so the DOM selection is unchanged.
              await expect(tab(0)).toHaveAttribute('aria-selected', 'true');
              await expect(tab(2)).toHaveAttribute('aria-selected', 'false');
            });

            test('moves focus to the previous tab while activating it', async ({ page, mode }) => {
              const { container } = await press(page, mode, { value: 1, activateOnFocus: true }, 1, previousItemKey, 0);
              await expect(container).toHaveAttribute('data-changes', '1');
              await expect(container).toHaveAttribute('data-last-change', /^value=0 reason=none .* type=focusin kind=FocusEvent/);
            });
          });

          test('moves focus to a disabled tab without activating it', async ({ page, mode }) => {
            const { fixture, container } = await press(page, mode, { value: 2, disabled: '1', activateOnFocus: true }, 2, previousItemKey, 1);
            await settle(fixture);
            await expect(container).toHaveAttribute('data-changes', '0');
          });
        });

        test.describe(nextItemKey, () => {
          test.describe('with `activateOnFocus = false`', () => {
            test('moves focus to the first tab without activating it if focus is on the last tab', async ({ page, mode }) => {
              const { fixture, container } = await press(page, mode, { value: 2 }, 2, nextItemKey, 0);
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
            });

            test('moves focus to the next tab without activating it', async ({ page, mode }) => {
              const { fixture, container } = await press(page, mode, { value: 1 }, 1, nextItemKey, 2);
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
            });

            test('moves focus to a disabled tab without activating it', async ({ page, mode }) => {
              const { fixture, container, tab } = await press(page, mode, { value: 0, disabled: '1' }, 0, nextItemKey, 1);
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
              // TAB-KEY-15: the disabled tab's own key prevention does not stop the next key from reaching the List.
              await page.keyboard.press(nextItemKey);
              await expect(tab(2)).toBeFocused();
            });
          });

          test.describe('with `activateOnFocus = true`', () => {
            test('moves focus to the first tab while activating it if focus is on the last tab', async ({ page, mode }) => {
              const { container } = await press(page, mode, { value: 2, activateOnFocus: true }, 2, nextItemKey, 0);
              await expect(container).toHaveAttribute('data-changes', '1');
              await expect(container).toHaveAttribute('data-log', /change:0/);
            });

            test('moves focus to the next tab while activating it', async ({ page, mode }) => {
              const { container, tab } = await press(page, mode, { value: 1, activateOnFocus: true }, 1, nextItemKey, 2);
              await expect(container).toHaveAttribute('data-changes', '1');
              await expect(container).toHaveAttribute('data-log', /change:2/);
              await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
            });
          });

          test('moves focus to a disabled tab without activating it', async ({ page, mode }) => {
            const { fixture, container, tab } = await press(page, mode, { value: 0, disabled: '1', activateOnFocus: true }, 0, nextItemKey, 1);
            await settle(fixture);
            await expect(container).toHaveAttribute('data-changes', '0');
            await page.keyboard.press(nextItemKey);
            await expect(tab(2)).toBeFocused();
          });
        });

        test.describe('modifier keys', () => {
          for (const modifierKey of ['Shift', 'Control', 'Alt', 'Meta'] as const) {
            test(`does not move focus when modifier key: ${modifierKey} is pressed`, async ({ page, mode }) => {
              const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { ...base, value: 0 });
              const { container, list, tab, before } = tabsCase(fixture);
              await recordKeys(list);

              // TAB-KEY-13: Tab lands on the List's one tab stop.
              await focus(before);
              await page.keyboard.press('Tab');
              await expect(tab(0)).toBeFocused();

              await page.keyboard.down(modifierKey);
              await page.keyboard.press(nextItemKey);
              await page.keyboard.up(modifierKey);
              await expect(tab(0)).toBeFocused();
              await expect(container).toHaveAttribute('data-list-keydowns', '2');

              await page.keyboard.down(modifierKey);
              await page.keyboard.press(previousItemKey);
              await page.keyboard.up(modifierKey);
              await expect(tab(0)).toBeFocused();
              await expect(container).toHaveAttribute('data-list-keydowns', '4');
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
              // TAB-KEY-08: the navigation keys pressed with a modifier are not default-prevented.
              const arrows = (await keyRecords(list)).filter((record) => record.key === nextItemKey || record.key === previousItemKey);
              expect(arrows).toEqual([
                { key: nextItemKey, defaultPrevented: false },
                { key: previousItemKey, defaultPrevented: false },
              ]);
            });
          }
        });
      });
    }

    test.describe('when focus is on a tab regardless of orientation', () => {
      for (const { key, from, to, disabledTarget } of [
        { key: 'Home', from: 2, to: 0, disabledTarget: 0 },
        { key: 'End', from: 0, to: 2, disabledTarget: 2 },
      ] as const) {
        const first = key === 'Home' ? 'first' : 'last';
        test.describe(key, () => {
          test(`when \`activateOnFocus = false\`, moves focus to the ${first} tab without activating it`, async ({ page, mode }) => {
            const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { value: from });
            const { container, list, tab } = tabsCase(fixture);
            await recordKeys(list);
            await focus(tab(from));
            await page.keyboard.press(key);
            await expect(tab(to)).toBeFocused();
            await expect(container).toHaveAttribute('data-list-keydowns', '1');
            await settle(fixture);
            await expect(container).toHaveAttribute('data-changes', '0');
            expect(await keyRecords(list)).toEqual([{ key, defaultPrevented: true }]);
          });

          test(`when \`activateOnFocus = true\`, moves focus to the ${first} tab while activating it`, async ({ page, mode }) => {
            const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { value: from, activateOnFocus: true });
            const { container, list, tab } = tabsCase(fixture);
            await recordKeys(list);
            await focus(tab(from));
            await page.keyboard.press(key);
            await expect(tab(to)).toBeFocused();
            await expect(container).toHaveAttribute('data-changes', '1');
            await expect(container).toHaveAttribute('data-log', new RegExp(`change:${to}`));
            await expect(container).toHaveAttribute('data-list-keydowns', '1');
            expect(await keyRecords(list)).toEqual([{ key, defaultPrevented: true }]);
          });

          for (const activateOnFocus of [false, true]) {
            test(`when \`activateOnFocus = ${activateOnFocus}\`, moves focus to a disabled tab without activating it`, async ({ page, mode }) => {
              const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { value: from, activateOnFocus, disabled: String(disabledTarget) });
              const { container, list, tab } = tabsCase(fixture);
              await recordKeys(list);
              await focus(tab(from));
              await page.keyboard.press(key);
              await expect(tab(disabledTarget)).toBeFocused();
              await expect(container).toHaveAttribute('data-list-keydowns', '1');
              await settle(fixture);
              await expect(container).toHaveAttribute('data-changes', '0');
              expect(await keyRecords(list)).toEqual([{ key, defaultPrevented: true }]);
            });
          }
        });
      }
    });

    test('should allow to focus first tab when there are no active tabs', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { default: 0 });
      const { tab } = tabsCase(fixture);

      // The composite writes the tab stop from attach (ADR 04 Amendments 2026-10-22).
      await expect(tab(0)).toHaveAttribute('tabindex', '0');
      await expect(tab(1)).toHaveAttribute('tabindex', '-1');
      await expect(tab(2)).toHaveAttribute('tabindex', '-1');
    });
  });
});

test.describe('<Tabs.List />', () => {
  test.describe('keyboard navigation', () => {
    test('moves focus to a tab disabled with the `disabled` prop', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-list');
      const { container, tab } = tabsCase(fixture, 'disabled-prop');

      await focus(tab(0));
      await page.keyboard.press('ArrowRight');

      await expect(tab(1)).toBeFocused();
      await expect(tab(1)).toHaveAttribute('aria-disabled', 'true');
      await expect(tab(1)).not.toHaveAttribute('disabled');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('skips a natively disabled tab in a single keypress', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-list');
      const { tab } = tabsCase(fixture, 'native-disabled');
      // TAB-DOM-11: the substitute's own `disabled` stays while the Tab's `Disabled` is false.
      await expect(tab(1)).toHaveAttribute('disabled', '');

      await focus(tab(0));
      await page.keyboard.press('ArrowRight');

      await expect(tab(2)).toBeFocused();
      await page.keyboard.press('ArrowLeft');
      await expect(tab(0)).toBeFocused();
    });
  });

  test.describe('prop: loopFocus', () => {
    test('does not wrap focus past the first tab when `loopFocus` is false', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-list');
      const { tab } = tabsCase(fixture, 'loop-first');

      await focus(tab(0));
      await page.keyboard.press('ArrowLeft');

      await expect(tab(0)).toBeFocused();
      await expect(tab(2)).not.toBeFocused();
    });

    test('does not wrap focus past the last tab when `loopFocus` is false', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-list');
      const { tab } = tabsCase(fixture, 'loop-last');

      await focus(tab(2));
      await page.keyboard.press('ArrowRight');

      await expect(tab(2)).toBeFocused();
      await expect(tab(0)).not.toBeFocused();
    });
  });

  test.describe('roving focus after tab removal', () => {
    test('moves the tab stop off a hidden successor when the highlighted tab is removed', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-list');
      const { tab, button } = tabsCase(fixture, 'hidden-successor');

      await focus(tab(0));
      await page.keyboard.press('ArrowRight');
      await expect(tab(1)).toBeFocused();
      await button('remove-middle').click();

      await expect(tab(1)).toHaveCount(0);
      await expect(tab(0)).toHaveAttribute('tabindex', '0');
      await expect(tab(2)).toHaveAttribute('tabindex', '-1');
      await expect(tab(3)).toHaveAttribute('tabindex', '-1');
    });

    test('can fall back to a tab that is focusable when disabled', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-list');
      const { tab, button } = tabsCase(fixture, 'disabled-fallback');

      await button('remove-selected').click();

      await expect(tab(2)).toHaveCount(0);
      await expect(tab(0)).toHaveAttribute('tabindex', '0');
      await expect(tab(1)).toHaveAttribute('tabindex', '-1');
    });

    test('keeps tracking a successor through subsequent removals', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-list');
      const { tab, button } = tabsCase(fixture, 'successors');

      await button('remove-selected').click();
      await expect(tab(1)).toHaveCount(0);
      await expect(tab(2)).toHaveAttribute('tabindex', '0');

      await button('remove-first').click();
      await expect(tab(0)).toHaveCount(0);
      await expect(tab(2)).toHaveAttribute('tabindex', '0');
      await expect(tab(3)).toHaveAttribute('tabindex', '-1');
    });
  });
});

test.describe('<Tabs.Root /> (tab order)', () => {
  // TAB-DOM-09, TAB-FOC-01, TAB-FOC-02 and TAB-DOM-08's clause, re-tagged to L4: the composite module writes the roving
  // `tabindex` from attach (ADR 04 Amendments 2026-10-22). The tab stop following a programmatic selection is TAB-STA-19's
  // follow clause (track 1's E5).
  test('puts the selected child in tab order', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-list');
    const { tabs, button } = tabsCase(fixture, 'tab-order');

    await expect.poll(() => tabIndexes(tabs)).toEqual(['-1', '0']);

    await button('select-0').click();

    await expect.poll(() => tabIndexes(tabs)).toEqual(['0', '-1']);
  });

  test('does not set tabIndex=0 on disabled tabs when they are programmatically selected', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-list');
    const { list, tab, button } = tabsCase(fixture, 'disabled-selected');
    await expect(tab(1)).toHaveAttribute('tabindex', '0');
    await expect(tab(0)).toHaveAttribute('tabindex', '-1');
    await expect(tab(2)).toHaveAttribute('tabindex', '-1');
    await recordMoves(list);

    await button('select-0').click();

    await expect(tab(0)).toHaveAttribute('aria-selected', 'true');
    await settle(fixture);
    await expect(tab(0)).toHaveAttribute('tabindex', '-1');
    await expect(tab(1)).toHaveAttribute('tabindex', '0');
    // The tab stop never moved, not even for a frame.
    expect(await moveRecords(list)).toEqual([]);
  });

  test('port: focus moved by a navigation key lands one microtask after the keydown, after the tab stop', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { value: 0 });
    const { list, tab } = tabsCase(fixture);
    await focus(tab(0));
    await recordMoves(list);

    await page.keyboard.press('ArrowRight');

    // TAB-FOC-10's microtask clause over CMP-FOC-02 (ADR 12 Amendments 2026-10-26 item 3): the tab stop moves inside the
    // keydown's dispatch, the focus a microtask later, onto the Tab that already holds the tab stop. A focus inside the
    // listener would dispatch `focusin` before the observer's record of the `tabindex` write.
    await expect(tab(1)).toBeFocused();
    expect(await moveRecords(list)).toEqual([
      { kind: 'tab-stop', tabStop: '1', focused: '0' },
      { kind: 'focus', tabStop: '1', focused: '1' },
    ]);
  });
});

// Group E: the tab stop follows the active Tab while focus is outside the List (track 1's E5, the `composite` module's
// follow of the active marker) and stays with focus inside it; a keyboard step is never pulled back.
test.describe('<Tabs.Root /> (highlight synchronization on external value change relative to focus)', () => {
  for (const activateOnFocus of [true, false]) {
    test(`keeps controlled async activation and focus aligned with activateOnFocus=${activateOnFocus}`, async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-focus');
      const { container, tab } = tabsCase(fixture, activateOnFocus ? 'async-focus' : 'async-click');

      await focus(tab(0));
      await page.keyboard.press('ArrowRight');
      await expect(tab(1)).toBeFocused();
      await expect(tab(0)).toHaveAttribute('aria-selected', 'true');

      await page.keyboard.press('ArrowRight');
      await expect(tab(2)).toBeFocused();

      // Without activateOnFocus, focusing the tab must not select it until Enter is pressed.
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', activateOnFocus ? '1' : '0');
      if (!activateOnFocus) {
        await page.keyboard.press('Enter');
      }

      await expect(tab(2)).toHaveAttribute('aria-selected', 'true');
      await expect(tab(2)).toBeFocused();
      await expect(tab(2)).toHaveAttribute('tabindex', '0');
      await expect(container).toHaveAttribute('data-last-change', /^value=2 /);
    });
  }

  test('when focus is outside the tablist, highlight follows the new active tab (tabIndex=0 moves)', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-focus');
    const { tabs, button } = tabsCase(fixture, 'outside');
    await expect.poll(() => tabIndexes(tabs)).toEqual(['0', '-1', '-1']);

    await button('select-2').click();

    await expect.poll(() => tabIndexes(tabs)).toEqual(['-1', '-1', '0']);

    await button('select-1').click();

    await expect.poll(() => tabIndexes(tabs)).toEqual(['-1', '0', '-1']);
  });

  test('when focus is inside the tablist, highlight stays put on external change and arrow keys continue from the focused tab', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-focus');
    const { tab, tabs, button } = tabsCase(fixture, 'inside');
    await focus(tab(0));
    await expect(tab(0)).toHaveAttribute('tabindex', '0');

    // From script, so focus stays on the Tab, as the test's setProps does.
    await button('select-2').evaluate((element) => (element as HTMLElement).click());
    await expect(tab(2)).toHaveAttribute('aria-selected', 'true');
    await settle(fixture);

    // Highlight should not change (still on first tab), but selection did
    expect(await tabIndexes(tabs)).toEqual(['0', '-1', '-1']);
    await expect(tab(0)).toHaveAttribute('aria-selected', 'false');

    // Arrow navigation should continue from the highlighted tab
    await page.keyboard.press('ArrowRight');
    await expect(tab(1)).toBeFocused();
    // Selection remains the externally-set tab since activateOnFocus=false
    await expect(tab(2)).toHaveAttribute('aria-selected', 'true');
    await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
  });

  test('keeps the tab stop on the selected tab when focus is inside the list', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-focus');
    const { tab, button } = tabsCase(fixture, 'list-inside');
    await focus(tab(2));

    await button('remove-first').evaluate((element) => (element as HTMLElement).click());

    await expect(tab(0)).toHaveCount(0);
    await settle(fixture);
    await expect(tab(1)).toHaveAttribute('tabindex', '-1');
    await expect(tab(2)).toHaveAttribute('tabindex', '0');
  });
});

// The automatic selection of an uncontrolled Root (track 1's E2, the `composite` module's report of the Tabs' order):
// TAB-STA-04's registry follows a Tab's `disabled` and its removal.
test.describe('<Tabs.Root /> (automatic selection)', () => {
  test('calls onValueChange when the selected tab becomes disabled', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-focus');
    const { container, tab, button } = tabsCase(fixture, 'becomes-disabled');

    await button('disable-first').click();

    await expect(container).toHaveAttribute('data-changes', '1');
    await expect(container).toHaveAttribute('data-last-change', /^value=1 reason=disabled direction=none /);
    await expect(tab(0)).toHaveAttribute('aria-selected', 'false');
    await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
  });

  test('keeps activation direction none after automatic disabled fallback', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-focus');
    const { root, tab, button } = tabsCase(fixture, 'becomes-disabled');

    await button('disable-first').click();

    await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
    await settle(fixture);
    await expect(root).toHaveAttribute('data-activation-direction', 'none');
  });

  test('calls onValueChange when the selected tab is removed', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-focus');
    const { container, tab, tabs, button } = tabsCase(fixture, 'removed');

    await button('remove-first').click();

    await expect(container).toHaveAttribute('data-changes', '1');
    await expect(container).toHaveAttribute('data-last-change', /^value=1 reason=missing /);
    await expect(tabs.first()).toHaveAttribute('aria-selected', 'true');
    await expect(tabs.first()).toHaveText('Tab 1');
    await expect(tab(1)).toHaveAttribute('tabindex', '0');
  });
});

test.describe('<Tabs.Root /> (roving focus)', () => {
  test('keeps a roving focus entry point when a controlled selected tab is removed', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-list');
    const { container, tab, button } = tabsCase(fixture, 'entry-point');

    await button('remove-last').click();

    await expect(tab(2)).toHaveCount(0);
    await expect(tab(0)).toHaveAttribute('aria-selected', 'false');
    await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
    await expect(tab(0)).toHaveAttribute('tabindex', '0');
    await expect(tab(1)).toHaveAttribute('tabindex', '-1');

    // The removal button is the element before the List: Tab from it lands on the List's one tab stop.
    await focus(button('remove-last'));
    await page.keyboard.press('Tab');
    await expect(tab(0)).toBeFocused();

    await page.keyboard.press('ArrowRight');
    await expect(tab(1)).toBeFocused();
    await settle(fixture);
    await expect(container).toHaveAttribute('data-changes', '0');
  });
});

test.describe('<Tabs.Root /> (nesting)', () => {
  test('keeps a nested root independent from the one hosting its panel', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-activation');
    const { tab, panel } = tabsCase(fixture, 'nested');

    // Each Root wires only its own Tabs and Panels.
    await expect(tab('outer-1')).toHaveAttribute('aria-controls', (await panel('outer-1').getAttribute('id'))!);
    await expect(tab('inner-1')).toHaveAttribute('aria-controls', (await panel('inner-1').getAttribute('id'))!);

    // Arrow keys inside the nested List move only within it, and loop there.
    await focus(tab('inner-1'));
    await page.keyboard.press('ArrowRight');
    await expect(tab('inner-2')).toBeFocused();
    await page.keyboard.press('ArrowRight');
    await expect(tab('inner-1')).toBeFocused();
    await page.keyboard.press('ArrowLeft');
    await expect(tab('inner-2')).toBeFocused();

    // Activating a nested Tab does not change the outer selection.
    await tab('inner-2').click();
    await expect(tab('inner-2')).toHaveAttribute('aria-selected', 'true');
    await expect(panel('inner-2')).toBeVisible();
    await expect(tab('outer-1')).toHaveAttribute('aria-selected', 'true');
    await expect(panel('outer-1')).toBeVisible();

    // Selecting another outer Tab unmounts the nested Root with its Panel.
    await tab('outer-2').click();
    await expect(panel('outer-2')).toBeVisible();
    await expect(tab('inner-1')).toHaveCount(0);
    await expect(panel('outer-1')).toHaveCount(0);
  });
});

test.describe('<Tabs.Root /> (activation)', () => {
  test.describe('prop: onValueChange', () => {
    test('when `activateOnFocus = true` should call onValueChange on pointerdown', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'focus-on');

      await tab(1).hover();
      await page.mouse.down();

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-last-change', /^value=1 reason=none .* type=focusin kind=FocusEvent/);
      // TAB-PTR-09: the consumer's onpointerdown runs once.
      await expect(container).toHaveAttribute('data-pointerdowns', '1');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '1');
    });

    test('should call onValueChange when clicking', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'controlled');

      await tab(1).click();

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-last-change', /^value=1 reason=none direction=right type=click kind=(Pointer|Mouse)Event trigger=none cancelable=true$/);
    });

    test('should not call onValueChange on non-main button clicks', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'controlled');

      // TAB-PTR-04 (§16 Q12): a synthetic click whose button is 2, the click React filters out.
      await tab(1).evaluate((element) => element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 2 })));

      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('should not call onValueChange when already active', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'controlled');

      await tab(0).click();

      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('when `activateOnFocus = true` should call onValueChange if an unactive tab gets focused', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'focus-on');

      await focus(tab(0));
      await page.keyboard.press('ArrowRight');

      await expect(tab(1)).toBeFocused();
      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-log', 'change:1');
    });

    test('when `activateOnFocus = false` should not call onValueChange if an unactive tab gets focused', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'focus-off');

      await focus(tab(0));

      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('does not move an uncontrolled selection when a user-initiated change is canceled', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab, panel } = tabsCase(fixture, 'cancel');
      await expect(tab(0)).toHaveAttribute('aria-selected', 'true');

      await tab(1).click();

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-last-change', /reason=none/);
      await settle(fixture);
      // The canceled click must not commit the new value in an uncontrolled root (TAB-EVT-03).
      await expect(tab(0)).toHaveAttribute('aria-selected', 'true');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
      await expect(panel(1)).toHaveAttribute('hidden', '');
    });

    test('does not call onValueChange on initial render when defaultValue is provided', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container } = tabsCase(fixture, 'uncontrolled');

      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('port: the selection commits inside the click on WebAssembly and a round trip later on Server', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { tab } = tabsCase(fixture, 'uncontrolled');

      // TAB-PTR-01's "synchronously after the click" (design point 8 of the Tabs track, settled by the orchestrator
      // 2026-10-26: a row of ADR 01's relaxation table): the request runs inside the `select` decision on WebAssembly;
      // on Server the module reports the activation and C# commits a round trip later.
      const selectedAfterClick = await tab(1).evaluate((element) => {
        (element as HTMLElement).click();
        return element.getAttribute('aria-selected');
      });

      expect(selectedAfterClick).toBe(mode === 'wasm' ? 'true' : 'false');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
    });

    test('port: a focus activation commits inside the focus on WebAssembly and a round trip later on Server', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { tab } = tabsCase(fixture, 'pointer');

      // TAB-KEY-10 with ActivateOnFocus under the same ADR 01 row as TAB-PTR-01 (design point 8 of the Tabs track,
      // settled by the orchestrator 2026-10-26).
      const selectedAfterFocus = await tab(1).evaluate((element) => {
        (element as HTMLElement).focus();
        return element.getAttribute('aria-selected');
      });

      expect(selectedAfterFocus).toBe(mode === 'wasm' ? 'true' : 'false');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
    });
  });

  test.describe('pointer navigation', () => {
    test('selects the clicked tab', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { tab, panel } = tabsCase(fixture, 'uncontrolled');

      await tab(1).click();

      await expect(panel(0)).toHaveAttribute('hidden', '');
      await expect(panel(1)).not.toHaveAttribute('hidden');
      await expect(panel(2)).toHaveAttribute('hidden', '');
    });

    test('does not select the clicked disabled tab', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab, panel } = tabsCase(fixture, 'disabled');

      // Playwright waits for an aria-disabled element to be enabled; the click is forced.
      await tab(1).click({ force: true });

      await settle(fixture);
      await expect(panel(0)).not.toHaveAttribute('hidden');
      await expect(panel(1)).toHaveAttribute('hidden', '');
      await expect(panel(2)).toHaveAttribute('hidden', '');
      // TAB-PTR-03: the consumer's onClick is not invoked on a disabled Tab.
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(container).toHaveAttribute('data-clicks', '0');
    });

    test('sets the aria-selected attribute on the active tab', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { tab } = tabsCase(fixture, 'uncontrolled');
      const selection = () => Promise.all([0, 1, 2].map((index) => tab(index).getAttribute('aria-selected')));

      expect(await selection()).toEqual(['true', 'false', 'false']);

      for (const [index, expected] of [
        [1, ['false', 'true', 'false']],
        [2, ['false', 'false', 'true']],
        [0, ['true', 'false', 'false']],
      ] as const) {
        await tab(index).evaluate((element) => (element as HTMLElement).click());
        await expect(tab(index)).toHaveAttribute('aria-selected', 'true');
        expect(await selection()).toEqual(expected);
      }
    });
  });
});

test.describe('<Tabs.Tab />', () => {
  test.describe('prop: nativeButton', () => {
    test('renders as an anchor and toggles selection when `nativeButton` is false', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { tab } = tabsCase(fixture, 'anchor');

      expect(await tab('overview').evaluate((element) => element.tagName)).toBe('A');
      await expect(tab('overview')).toHaveAttribute('aria-selected', 'true');
      await expect(tab('details')).toHaveAttribute('aria-selected', 'false');

      await tab('details').click();

      await expect(tab('overview')).toHaveAttribute('aria-selected', 'false');
      await expect(tab('details')).toHaveAttribute('aria-selected', 'true');
      // TAB-KEY-16: the click activates the Tab and follows the link.
      expect(new URL(page.url()).hash).toBe('#details');
    });

    // TabsTab.test.tsx "renders a react-router Link" re-cited to an `<a href>` substitute (ADR 13 Amendments 2026-10-22).
    test('port: an `<a href>` substitute keeps its href and is activated by Space and by Enter', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { tab } = tabsCase(fixture, 'anchor');
      await expect(tab('overview')).toHaveAttribute('href', /#overview$/);

      await focus(tab('overview'));
      await page.keyboard.press('ArrowRight');
      await expect(tab('details')).toBeFocused();
      await page.keyboard.press(' ');
      await expect(tab('details')).toHaveAttribute('aria-selected', 'true');

      await page.keyboard.press('ArrowLeft');
      await expect(tab('overview')).toBeFocused();
      await page.keyboard.press('Enter');
      await expect(tab('overview')).toHaveAttribute('aria-selected', 'true');
      expect(new URL(page.url()).hash).toBe('#overview');
    });
  });

  test.describe('pointer interaction', () => {
    test('does not re-commit the value when the active tab is pressed', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'pointer');

      await tab(0).click();

      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(tab(0)).toHaveAttribute('aria-selected', 'true');
    });

    test('does not activate a disabled tab that is pressed and focused', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'pointer-disabled');

      await tab(1).click({ force: true });
      // Disabled tabs stay focusable, and `activateOnFocus` must not select them.
      await focus(tab(1));

      await expect(tab(1)).toBeFocused();
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(tab(0)).toHaveAttribute('aria-selected', 'true');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
      // TAB-PTR-07: the disabled Tab's pointerdown and click reach no consumer handler.
      await expect(container).toHaveAttribute('data-pointerdowns', '0');
      await expect(container).toHaveAttribute('data-clicks', '0');
    });

    test('does not activate a tab focused by a held secondary-button press', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'pointer');

      await tab(1).hover();
      await page.mouse.down({ button: 'right' });
      await focus(tab(1));

      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
      await page.mouse.up({ button: 'right' });
    });

    test('activates on focus again once a secondary-button press has ended', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'pointer');

      await tab(1).click({ button: 'right' });
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');

      await focus(tab(0));
      await page.keyboard.press('ArrowRight');

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
    });

    test('activates on focus again once a secondary-button press is cancelled', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'pointer');

      await tab(1).evaluate((element) => {
        element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, button: 2 }));
        element.dispatchEvent(new PointerEvent('pointercancel', { bubbles: true }));
      });
      await focus(tab(0));
      await page.keyboard.press('ArrowRight');

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
    });
  });

  test.describe('keyboard activation', () => {
    for (const [label, key] of [
      ['Enter', 'Enter'],
      ['Space', ' '],
    ] as const) {
      test(`activates the focused tab with ${label} when \`activateOnFocus\` is false`, async ({ page, mode }) => {
        const fixture = await gotoTabs(page, mode, 'tabs-activation');
        const { tab } = tabsCase(fixture, 'keys');

        await focus(tab(0));
        await page.keyboard.press('ArrowRight');
        await expect(tab(1)).toBeFocused();
        await expect(tab(1)).toHaveAttribute('aria-selected', 'false');

        await page.keyboard.press(key);

        await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
        await expect(tab(0)).toHaveAttribute('aria-selected', 'false');
      });
    }
  });

  test.describe('element replacement', () => {
    // TAB-DOM-09 and TAB-EDGE-11's "settles when the rendered component recreates its merged ref", restated under ADR 06
    // Amendments 2026-10-18 rule 2 as an element replacement.
    test('port: a Tab whose element is replaced keeps its registration, its activation and its tabindex', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'fixed-tabs' });
      const { container, tab, button } = tabsCase(fixture);

      await button('swap').click();
      await expect.poll(() => tab(1).evaluate((element) => element.tagName)).toBe('A');
      await expect(tab(1)).toHaveAttribute('tabindex', '-1');
      await expect(tab(2)).toHaveAttribute('tabindex', '0');

      await focus(tab(2));
      await page.keyboard.press('ArrowLeft');
      await expect(tab(1)).toBeFocused();
      await expect(tab(1)).toHaveAttribute('tabindex', '0');

      // The case's Root is controlled with a value its parent never changes: the request is what the click reaches.
      await tab(1).click();
      await expect(container).toHaveAttribute('data-last-change', /^value=1 reason=none /);
    });
  });

  test.describe('consumer vetoes', () => {
    // TAB-PTR-09 with ADR 01's "C#-origin consumer vetoes" row: on WebAssembly the consumer's onclick runs inside the
    // `select` decision; on Server the module reports the activation and C# runs the consumer's handler before the request,
    // a round trip late, so the veto holds on both modes.
    test("port: PreventPartHandler() in a C# onclick vetoes the activation on both modes", async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation');
      const { container, tab } = tabsCase(fixture, 'veto');

      await tab(1).click();

      await expect(container).toHaveAttribute('data-clicks', '1');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
    });

    test("port: PreventPartHandler() in a C# onfocus vetoes the focus activation on both modes", async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation', { vetoFocus: true });
      const { container, tab } = tabsCase(fixture, 'veto');

      await focus(tab(0));
      await page.keyboard.press('ArrowRight');

      await expect(tab(1)).toBeFocused();
      await expect(container).toHaveAttribute('data-focuses', '1');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(tab(1)).toHaveAttribute('aria-selected', 'false');
    });

    test('port: a consumer onclick that does not veto runs once, before the request', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-activation', { veto: false });
      const { container, tab } = tabsCase(fixture, 'veto');

      await tab(1).click();

      await expect(tab(1)).toHaveAttribute('aria-selected', 'true');
      // The press focuses the Tab first: its onfocus runs, then its onclick, then the request.
      await expect(container).toHaveAttribute('data-log', 'focus click change:1');
      // A click on the now active Tab still reaches the consumer's handler, and activates nothing.
      await tab(1).click();
      await expect(container).toHaveAttribute('data-log', 'focus click change:1 click');
    });
  });
});

/** One Panel element the page saw created: its value, and its direction attribute and class in the batch that created it. */
interface CreatedPanel {
  panel: string | null;
  direction: string | null;
  className: string;
}

/**
 * Records, as markers the page keeps (ADR 06 §2), each Panel element a batch creates in a case, with the
 * `data-activation-direction` and class it carries in that batch's mutation callback: the `tabs` module's observer was
 * created first, at attach, so this one runs after it, before paint.
 */
async function recordCreatedPanels(container: Locator): Promise<void> {
  await container.evaluate((element) => {
    const records: CreatedPanel[] = [];
    (element as unknown as { created: CreatedPanel[] }).created = records;
    new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        for (const node of mutation.addedNodes) {
          if (node instanceof HTMLElement && node.getAttribute('role') === 'tabpanel') {
            records.push({
              panel: node.getAttribute('data-panel'),
              direction: node.getAttribute('data-activation-direction'),
              className: node.className,
            });
          }
        }
      }
    }).observe(element, { subtree: true, childList: true });
  });
}

async function createdPanels(container: Locator): Promise<CreatedPanel[]> {
  return container.evaluate((element) => (element as unknown as { created: CreatedPanel[] }).created);
}

test.describe('<Tabs.Root /> (activation direction)', () => {
  for (const { orientation, forward, backward } of [
    { orientation: 'horizontal', forward: 'right', backward: 'left' },
    { orientation: 'vertical', forward: 'down', backward: 'up' },
  ] as const) {
    test(`should set the \`data-activation-direction\` attribute on the tabs root with orientation=${orientation}`, async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-direction');
      const { container, root, tab, panel } = tabsCase(fixture, orientation);
      await recordCreatedPanels(container);
      await expect(root).toHaveAttribute('data-activation-direction', 'none');

      await tab(1).click();

      await expect(root).toHaveAttribute('data-activation-direction', forward);
      // TAB-STA-14, TAB-ANI-01: the first open render of the Panel already carries the new direction, before paint.
      await expect.poll(() => createdPanels(container)).toEqual([expect.objectContaining({ panel: '1', direction: forward })]);
      await expect(container).toHaveAttribute('data-last-change', new RegExp(`direction=${forward} `));
      await expect(panel(0)).toHaveCount(0);

      await tab(0).click();

      await expect(root).toHaveAttribute('data-activation-direction', backward);
      await expect.poll(async () => (await createdPanels(container)).at(-1)).toEqual(expect.objectContaining({ panel: '0', direction: backward }));
      // The C# mirror (ADR 01's RND-STA-01 row): the Panel's class function sees the direction once the report arrives.
      await expect(panel(0)).toHaveClass(`dir-${backward}`);
    });

    test(`should update \`data-activation-direction\` on programmatic value changes with orientation=${orientation}`, async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-direction');
      const { container, root, tab, button } = tabsCase(fixture, `programmatic-${orientation}`);
      await recordCreatedPanels(container);
      await expect(root).toHaveAttribute('data-activation-direction', 'none');
      await expect(tab(0)).toHaveAttribute('data-activation-direction', 'none');

      await button('select-1').click();

      await expect(root).toHaveAttribute('data-activation-direction', forward);
      await expect(tab(1)).toHaveAttribute('data-activation-direction', forward);
      await expect.poll(() => createdPanels(container)).toEqual([expect.objectContaining({ panel: '1', direction: forward })]);

      await button('select-0').click();

      await expect(root).toHaveAttribute('data-activation-direction', backward);
      await expect(tab(0)).toHaveAttribute('data-activation-direction', backward);
      await expect.poll(async () => (await createdPanels(container)).at(-1)).toEqual(expect.objectContaining({ panel: '0', direction: backward }));
    });
  }

  test('resets `data-activation-direction` when the selection is cleared and restored', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-direction');
    const { root, indicator, button } = tabsCase(fixture, 'cleared');

    await button('select-1').click();
    await expect(root).toHaveAttribute('data-activation-direction', 'right');

    // Clearing the selection is not a directional transition.
    await button('clear').click();
    await expect(root).toHaveAttribute('data-activation-direction', 'none');
    await expect(indicator()).toHaveCount(0);

    // Neither is selecting a tab again from a cleared state.
    await button('select-0').click();
    await expect(indicator()).toHaveCount(1);
    await settle(fixture);
    await expect(root).toHaveAttribute('data-activation-direction', 'none');
  });

  test('should update `data-activation-direction` on programmatic change after a canceled click', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-direction');
    const { container, root, tab, button } = tabsCase(fixture, 'canceled');

    // Click is canceled: the value stays at 0.
    await tab(1).click();
    await expect(container).toHaveAttribute('data-changes', '1');
    await settle(fixture);
    await expect(root).toHaveAttribute('data-activation-direction', 'none');

    // A later programmatic change still computes the direction correctly.
    await button('select-1').click();
    await expect(root).toHaveAttribute('data-activation-direction', 'right');
  });

  test('should update `data-activation-direction` on programmatic change after a controlled parent ignores click', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-direction');
    const { container, root, tab, button } = tabsCase(fixture, 'ignored');

    // The click calls OnValueChange but the parent does not update the value.
    await tab(1).click();
    await expect(container).toHaveAttribute('data-changes', '1');
    await settle(fixture);
    await expect(root).toHaveAttribute('data-activation-direction', 'none');

    await button('select-1').click();
    await expect(root).toHaveAttribute('data-activation-direction', 'right');
  });

  test('should compute correct direction when adding and selecting a new tab in one controlled update', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-direction');
    const { container, root, button, panel } = tabsCase(fixture, 'add-number');
    await recordCreatedPanels(container);
    await expect(root).toHaveAttribute('data-activation-direction', 'none');

    await button('add').click();

    await expect(root).toHaveAttribute('data-activation-direction', 'right');
    await expect.poll(() => createdPanels(container)).toEqual([expect.objectContaining({ panel: '2', direction: 'right' })]);
    await expect(panel(2)).toHaveClass('dir-right');
  });

  test('should compute correct direction on final render when adding and selecting a new tab in one controlled update with out of order string values', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-direction');
    const { container, root, button, panel } = tabsCase(fixture, 'add-string');
    await recordCreatedPanels(container);
    await expect(root).toHaveAttribute('data-activation-direction', 'none');

    await button('add').click();

    // TAB-STA-16/17 (design point 7, settled by the orchestrator 2026-10-26): the new Tab is in the batch the module
    // reads, so the positional direction is the first one shown; the provisional value-based guess ('Account' <
    // 'Overview') never appears.
    await expect(root).toHaveAttribute('data-activation-direction', 'right');
    await expect.poll(() => createdPanels(container)).toEqual([expect.objectContaining({ panel: 'Account', direction: 'right' })]);
    await expect(panel('Account')).toHaveClass('dir-right');
  });
});

/** One change the page saw on a Panel of a case, with the number of the observer callback that delivered it. */
interface PanelRecord {
  kind: 'added' | 'removed' | 'attribute';
  panel: string | null;
  name: string | null;
  present: boolean;
  batch: number;
  /** For `data-ending-style` appearing: the `aria-controls` of the Tab with the Panel's value, and the Panel's id. */
  controls?: string | null;
  id?: string;
}

/**
 * Records, from now on, every insertion and removal of a Panel in a case and every change of its transition attributes,
 * `inert` and `hidden`, with the number of the observer callback that delivered it (one callback per microtask checkpoint
 * after a DOM change, so two records with one batch number landed before the same paint).
 */
async function recordPanels(container: Locator): Promise<void> {
  await container.evaluate((node) => {
    const records: unknown[] = [];
    let batch = 0;
    (node as unknown as { panelRecords: unknown[] }).panelRecords = records;
    new MutationObserver((mutations) => {
      batch++;
      for (const mutation of mutations) {
        if (mutation.type === 'childList') {
          mutation.addedNodes.forEach((added) => {
            if (added instanceof Element && added.matches('[data-panel]')) {
              records.push({ kind: 'added', panel: added.getAttribute('data-panel'), name: null, present: true, batch });
            }
          });
          mutation.removedNodes.forEach((removed) => {
            if (removed instanceof Element && removed.matches('[data-panel]')) {
              records.push({ kind: 'removed', panel: removed.getAttribute('data-panel'), name: null, present: false, batch });
            }
          });
        } else if (mutation.target instanceof Element && mutation.target.matches('[data-panel]')) {
          const panel = mutation.target.getAttribute('data-panel');
          const present = mutation.target.hasAttribute(mutation.attributeName!);
          const record: Record<string, unknown> = { kind: 'attribute', panel, name: mutation.attributeName, present, batch };
          if (mutation.attributeName === 'data-ending-style' && present) {
            record.controls = node.querySelector(`[data-tab="${panel}"]`)?.getAttribute('aria-controls') ?? null;
            record.id = mutation.target.id;
          }
          records.push(record);
        }
      }
    }).observe(node, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['data-starting-style', 'data-ending-style', 'inert', 'hidden'],
    });
  });
}

async function panelRecords(container: Locator): Promise<PanelRecord[]> {
  return container.evaluate((node) => (node as unknown as { panelRecords: PanelRecord[] }).panelRecords);
}

/** Opens a fixture with animations on (ADR 06 §2: the host turns them off unless `?animations=1`). */
async function gotoAnimated(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
  return gotoTabs(page, mode, name, { animations: 1 });
}

test.describe('<Tabs.Panel />', () => {
  test.describe('animations', () => {
    test('triggers enter animation via data-starting-style when mounting', async ({ page, mode }) => {
      const fixture = await gotoAnimated(page, mode, 'tabs-panels');
      const { container, tab, panel } = tabsCase(fixture, 'enter');
      await expect(panel('two')).toHaveCount(0);
      await container.evaluate((node) => {
        node.addEventListener('transitionend', (event) => {
          if ((event.target as Element).matches('[data-panel="two"]')) {
            (node as HTMLElement).dataset.transitionEnded = 'true';
          }
        });
      });
      await recordPanels(container);
      await recordCreatedPanels(container);

      await tab('two').click();

      await expect(container).toHaveAttribute('data-transition-ended', 'true');
      await expect(panel('two')).toHaveCount(1);
      await expect(panel('two')).not.toHaveAttribute('data-starting-style');
      // TAB-ANI-01: the element appeared with data-starting-style (C#'s handover) and the new direction, which were
      // there before the frame that cleared the starting style.
      const records = await panelRecords(container);
      const added = records.find((record) => record.kind === 'added' && record.panel === 'two');
      const cleared = records.find((record) => record.panel === 'two' && record.name === 'data-starting-style' && !record.present);
      expect(added).toBeDefined();
      expect(cleared).toBeDefined();
      expect(cleared!.batch).toBeGreaterThan(added!.batch);
      expect(await createdPanels(container)).toEqual([{ panel: 'two', direction: 'right', className: 'animation-test-panel' }]);
    });

    test('applies data-ending-style before unmount', async ({ page, mode }) => {
      const fixture = await gotoAnimated(page, mode, 'tabs-panels');
      const { container, tab, panel } = tabsCase(fixture, 'exit');
      await expect(panel('one')).toHaveCount(1);
      await recordPanels(container);

      await tab('two').click();

      await expect(panel('one')).toHaveCount(0);
      const records = await panelRecords(container);
      const ending = records.findIndex((record) => record.panel === 'one' && record.name === 'data-ending-style' && record.present);
      const removed = records.findIndex((record) => record.kind === 'removed' && record.panel === 'one');
      expect(ending).toBeGreaterThanOrEqual(0);
      expect(removed).toBeGreaterThan(ending);
      // TAB-ANI-05: the closing Panel keeps its registration through its exit, so its Tab still names it.
      expect(records[ending]!.controls).toBe(records[ending]!.id);
      // TAB-ANI-03 with design point 1 (OpenSelector ":not([inert])", settled by the orchestrator 2026-10-26):
      // data-ending-style and inert appear in the same batch, before paint, on both modes.
      const inert = records.find((record) => record.panel === 'one' && record.name === 'inert' && record.present);
      expect(inert?.batch).toBe(records[ending]!.batch);
    });

    test('port: a kept-mounted panel plays its exit, is hidden at its end, and starts from data-starting-style when reopened', async ({ page, mode }) => {
      const fixture = await gotoAnimated(page, mode, 'tabs-panels');
      const { container, tab, panel } = tabsCase(fixture, 'kept');
      await expect(panel('two')).toHaveAttribute('hidden', '');
      await recordPanels(container);

      await tab('two').click();

      // The closing Panel: data-ending-style for its exit, then hidden and data-hidden (TAB-DOM-14, TAB-ANI-03).
      await expect(panel('one')).toHaveAttribute('hidden', '');
      await expect(panel('one')).toHaveAttribute('data-hidden', '');
      await expect(panel('one')).not.toHaveAttribute('data-ending-style');
      // The opening Panel: no longer hidden, its starting style written and cleared by the module.
      await expect(panel('two')).not.toHaveAttribute('hidden');
      await expect(panel('two')).not.toHaveAttribute('data-starting-style');
      const records = await panelRecords(container);
      expect(records).toContainEqual(expect.objectContaining({ panel: 'one', name: 'data-ending-style', present: true }));
      expect(records).toContainEqual(expect.objectContaining({ panel: 'two', name: 'data-starting-style', present: true }));
      expect(records.filter((record) => record.kind === 'removed')).toEqual([]);
    });
  });

  test('port: with animations disabled a closed panel unmounts at once', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-panels');
    const { container, tab, panel } = tabsCase(fixture, 'exit');
    await recordPanels(container);

    await tab('two').click();

    // TAB-ANI-06 (TRN-EDGE-01): the exit completes when the completion watcher arms; data-ending-style lasts at most one
    // batch before the element is removed.
    await expect(panel('one')).toHaveCount(0);
    await expect(panel('two')).toHaveCount(1);
    const records = await panelRecords(container);
    const ending = records.filter((record) => record.panel === 'one' && record.name === 'data-ending-style' && record.present);
    expect(ending.length).toBeLessThanOrEqual(1);
  });
});

/**
 * Base UI's expectBubblePositionVariables, in the page: the six variables of an Indicator against the active Tab's
 * measurement (CSS size, the rect offset over the List's scale, scroll and border, `right`/`bottom` against the List's
 * scrollable content), within 0.01 px. Returns the mismatches, empty when they agree.
 */
async function positionMismatches(indicator: Locator, list: Locator, tab: Locator): Promise<string[]> {
  const tabHandle = await tab.elementHandle();
  const listHandle = await list.elementHandle();
  return indicator.evaluate(
    (bubble, [activeTab, tabList]) => {
      const dimensions = (element: Element) => {
        const css = getComputedStyle(element);
        let width = parseFloat(css.width) || 0;
        let height = parseFloat(css.height) || 0;
        const html = element as HTMLElement;
        if (Math.round(width) !== html.offsetWidth || Math.round(height) !== html.offsetHeight) {
          width = html.offsetWidth;
          height = html.offsetHeight;
        }
        return { width, height };
      };
      const tabElement = activeTab as HTMLElement;
      const listElement = tabList as HTMLElement;
      const tabRect = tabElement.getBoundingClientRect();
      const listRect = listElement.getBoundingClientRect();
      const tabSize = dimensions(tabElement);
      const listSize = dimensions(listElement);
      const scaleX = listSize.width > 0 ? listRect.width / listSize.width : 1;
      const scaleY = listSize.height > 0 ? listRect.height / listSize.height : 1;
      const left = (tabRect.left - listRect.left) / scaleX + listElement.scrollLeft - listElement.clientLeft;
      const top = (tabRect.top - listRect.top) / scaleY + listElement.scrollTop - listElement.clientTop;
      const expected: Record<string, number> = {
        '--active-tab-left': left,
        '--active-tab-right': listElement.scrollWidth - left - tabSize.width,
        '--active-tab-top': top,
        '--active-tab-bottom': listElement.scrollHeight - top - tabSize.height,
        '--active-tab-width': tabSize.width,
        '--active-tab-height': tabSize.height,
      };
      const style = getComputedStyle(bubble);
      return Object.entries(expected)
        .filter(([name, value]) => !(Math.abs(parseFloat(style.getPropertyValue(name)) - value) <= 0.01))
        .map(([name, value]) => `${name}: ${style.getPropertyValue(name)} != ${value}`);
    },
    [tabHandle, listHandle] as const,
  );
}

/** Base UI's waitForBubbleToOverlapActiveTab: the Indicator's on-screen box coincides with the active Tab's within 1 px. */
async function overlapMismatches(indicator: Locator, tab: Locator): Promise<string[]> {
  const tabHandle = await tab.elementHandle();
  return indicator.evaluate((bubble, activeTab) => {
    const bubbleRect = bubble.getBoundingClientRect();
    const tabRect = (activeTab as Element).getBoundingClientRect();
    return (['left', 'top', 'right', 'bottom'] as const)
      .filter((edge) => !(Math.abs(bubbleRect[edge] - tabRect[edge]) <= 1))
      .map((edge) => `${edge}: ${bubbleRect[edge]} != ${tabRect[edge]}`);
  }, tabHandle);
}

async function cssVariable(indicator: Locator, name: string): Promise<number> {
  return indicator.evaluate((element, variable) => parseFloat(getComputedStyle(element).getPropertyValue(variable)), name);
}

test.describe('<Tabs.Indicator />', () => {
  test('exposes null active tab state when the selected value has no matching tab', async ({ page, mode }) => {
    const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'missing' });
    const { indicator } = tabsCase(fixture);

    // The render function ran more than once (the module's report renders it again), and its state has no position.
    await expect(indicator()).toHaveAttribute('data-states', /^[2-9]|[1-9][0-9]+$/);
    await expect(indicator()).toHaveAttribute('data-position', 'null');
    await expect(indicator()).toHaveAttribute('data-size', 'null');
    await expect(indicator()).toHaveAttribute('hidden', '');
  });

  test.describe('rendering', () => {
    test('should set CSS variables corresponding to the active tab', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'plain' });
      const { indicator, list, tab } = tabsCase(fixture);

      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);
      await expect(indicator()).not.toHaveAttribute('hidden');
    });

    test('should update the position and movement variables when the active tab changes', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'plain' });
      const { indicator, list, tab, button } = tabsCase(fixture);

      await button('select-3').click();
      await expect.poll(() => positionMismatches(indicator(), list, tab(3))).toEqual([]);

      await button('select-1').click();
      await expect.poll(() => positionMismatches(indicator(), list, tab(1))).toEqual([]);
    });

    test('should update the position variables when the tab list is resized', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'resize-list' });
      const { indicator, list, tab, button } = tabsCase(fixture);
      await expect.poll(() => positionMismatches(indicator(), list, tab(1))).toEqual([]);
      const before = await cssVariable(indicator(), '--active-tab-width');

      await button('widen').click();

      await expect.poll(() => cssVariable(indicator(), '--active-tab-width')).toBeGreaterThan(before);
      await expect.poll(() => positionMismatches(indicator(), list, tab(1))).toEqual([]);
    });

    test('should account for scroll and border when the tab list is transformed', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'scaled' });
      const { indicator, list, tab } = tabsCase(fixture);

      await list.evaluate((element) => {
        element.scrollLeft = 80;
      });
      // Scrolling alone does not re-measure (TAB-DOM-30, §16 Q11); Base UI's test relies on the first resize notification
      // arriving after the scroll, so the active tab is nudged to bring one.
      await tab(3).evaluate((element) => {
        (element as HTMLElement).style.flex = '0 0 121px';
      });

      await expect.poll(() => positionMismatches(indicator(), list, tab(3))).toEqual([]);
    });

    test('accounts for a scrolled container between the tab list and the active tab', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'scroller' });
      const { container, indicator, tab } = tabsCase(fixture);

      await container.locator('[data-scroller]').evaluate((element) => {
        element.scrollLeft = 40;
      });
      // Scrolling on its own doesn't notify the indicator, only tab resizes do: the active tab is nudged.
      await tab(3).evaluate((element) => {
        (element as HTMLElement).style.width = '84px';
      });

      await expect.poll(() => overlapMismatches(indicator(), tab(3))).toEqual([]);
    });

    for (const [title, wrapper] of [
      ['overlays the active tab when an ancestor has a 2D rotation', 'transform: rotate(40deg)'],
      ['overlays the active tab when an ancestor has a size-preserving flip', 'transform: scaleX(-1)'],
      ['overlays the active tab when an ancestor uses the rotate longhand', 'rotate: 40deg'],
      ['overlays the active tab when an ancestor uses a flipping scale longhand', 'scale: -1 1'],
      ['overlays the active tab when an ancestor has a 3D rotation (#4837)', 'transform: perspective(600px) rotateY(35deg)'],
    ] as const) {
      test(title, async ({ page, mode }) => {
        const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'transformed', wrapper });
        const { container, indicator, tab } = tabsCase(fixture);
        // The page applied the ancestor's transform, so an untransformed overlay cannot pass for it.
        const applied = await container.locator('.styled-indicator').evaluate((element) => {
          const style = getComputedStyle(element);
          return [style.transform, style.rotate, style.scale].some((value) => value !== 'none');
        });
        expect(applied).toBe(true);

        await expect.poll(() => overlapMismatches(indicator(), tab(3))).toEqual([]);
        await expect(indicator()).not.toHaveAttribute('hidden');
      });
    }

    test('sets transformed offsets relative to the tab list when the list is not the offset parent', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', {
        case: 'transformed',
        wrapper: 'position: relative; transform: rotate(40deg)',
        list: 'display: flex; margin-left: 40px',
      });
      const { indicator } = tabsCase(fixture);

      await expect.poll(() => cssVariable(indicator(), '--active-tab-left')).toBeCloseTo(160, 2);
      await expect.poll(() => cssVariable(indicator(), '--active-tab-top')).toBeCloseTo(0, 2);
    });

    for (const [title, tabStyle] of [
      ['follows the active tab when it has its own transform translation', 'transform: translateX(12px) translateY(4px)'],
      ['follows the active tab when it uses the translate longhand', 'translate: 12px 4px'],
      // 50% of 80px = 40px across, 25% of 32px = 8px down.
      ['follows the active tab when the translate longhand uses percentages', 'translate: 50% 25%'],
    ] as const) {
      test(title, async ({ page, mode }) => {
        const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'translated', tab: tabStyle });
        const { indicator, tab } = tabsCase(fixture);
        // The page applied the Tab's own translation, so the Indicator must follow it off its layout slot.
        const translated = await tab(3).evaluate((element) => {
          const style = getComputedStyle(element);
          return style.transform !== 'none' || style.translate !== 'none';
        });
        expect(translated).toBe(true);

        await expect.poll(() => overlapMismatches(indicator(), tab(3))).toEqual([]);
      });
    }

    test('updates position when a different tab resizes', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'fixed-tabs' });
      const { indicator, list, tab } = tabsCase(fixture);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);

      await tab(1).evaluate((element) => element.setAttribute('style', 'width: 140px; flex-shrink: 0;'));

      await expect.poll(() => cssVariable(indicator(), '--active-tab-left')).toBeCloseTo(140, 0);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);
    });

    test('keeps observing a tab whose rendered element type changes', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'fixed-tabs' });
      const { indicator, list, tab, button } = tabsCase(fixture);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);

      await button('swap').click();
      await expect.poll(() => tab(1).evaluate((element) => element.tagName)).toBe('A');

      await tab(1).evaluate((element) => element.setAttribute('style', 'width: 160px; flex-shrink: 0;'));

      await expect.poll(() => cssVariable(indicator(), '--active-tab-left')).toBeCloseTo(160, 0);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);
    });

    test('falls back to offset positions when the tab list is scaled to zero', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'scale-zero' });
      const { indicator, tab } = tabsCase(fixture);
      const offsets = await tab(2).evaluate((element) => ({ left: (element as HTMLElement).offsetLeft, top: (element as HTMLElement).offsetTop }));

      // The collapsed bounding rects can't be divided by, so the indicator uses the untransformed offsets.
      await expect.poll(() => cssVariable(indicator(), '--active-tab-left')).toBeCloseTo(offsets.left, 2);
      expect(await cssVariable(indicator(), '--active-tab-top')).toBeCloseTo(offsets.top, 2);
      expect(await cssVariable(indicator(), '--active-tab-width')).toBeCloseTo(100, 2);
      await expect(indicator()).not.toHaveAttribute('hidden');
    });

    test('updates position when a new tab is inserted and then resized', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'fixed-tabs' });
      const { indicator, list, tab, button } = tabsCase(fixture);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);

      await button('insert').click();
      await expect(tab(0)).toHaveCount(1);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);

      await tab(0).evaluate((element) => element.setAttribute('style', 'width: 120px; flex-shrink: 0;'));

      await expect.poll(() => cssVariable(indicator(), '--active-tab-left')).toBeCloseTo(220, 0);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);
    });

    test('port: a consumer style overrides the CSS variables the module writes', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'consumer-style' });
      const { indicator, list, tab } = tabsCase(fixture);

      // TAB-DOM-32: the variables first, then the consumer's style, which wins on the property both set; the other
      // five variables are the module's measurement.
      await expect
        .poll(async () => (await positionMismatches(indicator(), list, tab(2))).filter((entry) => !entry.startsWith('--active-tab-left:')))
        .toEqual([]);
      await expect.poll(() => cssVariable(indicator(), '--active-tab-left')).toBe(7);
      await expect(indicator()).toHaveCSS('color', 'rgb(255, 0, 0)');
    });

    test('updates all indicators when a different tab resizes', async ({ page, mode }) => {
      const fixture = await gotoTabs(page, mode, 'tabs-indicator', { case: 'fixed-tabs', second: true });
      const { indicator, list, tab } = tabsCase(fixture);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);
      await expect.poll(() => positionMismatches(indicator('second'), list, tab(2))).toEqual([]);

      await tab(1).evaluate((element) => element.setAttribute('style', 'width: 140px; flex-shrink: 0;'));

      await expect.poll(() => cssVariable(indicator(), '--active-tab-left')).toBeCloseTo(140, 0);
      await expect.poll(() => positionMismatches(indicator(), list, tab(2))).toEqual([]);
      await expect.poll(() => positionMismatches(indicator('second'), list, tab(2))).toEqual([]);
    });
  });
});

/** Records, from now on, every change of `aria-selected` on the Tabs of a case, as the attribute's new value. */
async function recordSelections(container: Locator): Promise<void> {
  await container.evaluate((element) => {
    const records: string[] = [];
    (element as unknown as { selections: string[] }).selections = records;
    new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        const tab = mutation.target as Element;
        records.push(`${tab.getAttribute('data-tab')}:${tab.getAttribute('aria-selected')}`);
      }
    }).observe(element, { subtree: true, attributes: true, attributeFilter: ['aria-selected'] });
  });
}

async function selections(container: Locator): Promise<string[]> {
  return container.evaluate((element) => (element as unknown as { selections: string[] }).selections);
}

// The rows of ADR 01's relaxation table §9 names for Tabs, asserted per mode; on Server the circuit is slowed in both
// directions so that "a round trip late" is a gap the page records, not a race.
test.describe('<Tabs /> (relaxations per mode)', () => {
  const latency = 200;

  test('port: a List onkeydown veto suppresses the navigation on WebAssembly; on Server the key is handled and the handler runs a round trip late', async ({ page, mode }) => {
    if (mode === 'server') await delayCircuit(page, latency);
    const fixture = await gotoTabs(page, mode, 'tabs-keyboard', { value: 0, vetoKeys: true });
    const { container, tab } = tabsCase(fixture);
    // The List's recorded key count at the moment focus reaches Tab 1, if it does.
    await tab(1).evaluate((element, owner) => {
      element.addEventListener('focus', () => element.setAttribute('data-keydowns-at-focus', owner!.getAttribute('data-list-keydowns') ?? ''));
    }, await container.elementHandle());

    await focus(tab(0));
    await page.keyboard.press('ArrowRight');

    // TAB-KEY-09 with the "EVD-EVT-26 against composite/button key handling" row.
    await expect(container).toHaveAttribute('data-list-keydowns', '1');
    await settle(fixture);
    if (mode === 'wasm') {
      await expect(tab(0)).toBeFocused();
      await expect(tab(1)).not.toHaveAttribute('data-keydowns-at-focus');
    } else {
      await expect(tab(1)).toBeFocused();
      await expect(tab(1)).toHaveAttribute('data-keydowns-at-focus', '0');
    }
    await expect(container).toHaveAttribute('data-changes', '0');
  });

  test('port: a C# onclick veto holds on both modes, with no transient selection under Server latency', async ({ page, mode }) => {
    if (mode === 'server') await delayCircuit(page, latency);
    const fixture = await gotoTabs(page, mode, 'tabs-activation');
    const { container, tab } = tabsCase(fixture, 'veto');
    await recordSelections(container);

    await tab(1).click();

    // TAB-PTR-09 with the "C#-origin consumer vetoes" row: the module selects nothing on its own; C# runs the consumer's
    // handler before the request (inside `select` on WebAssembly, from the report on Server).
    await expect(container).toHaveAttribute('data-clicks', '1');
    await settle(fixture);
    expect(await selections(container)).toEqual([]);
    await expect(container).toHaveAttribute('data-changes', '0');
  });

  test('port: the C# mirror of the activation direction follows the attribute the module writes', async ({ page, mode }) => {
    if (mode === 'server') await delayCircuit(page, latency);
    const fixture = await gotoTabs(page, mode, 'tabs-direction');
    const { container, tab, panel } = tabsCase(fixture, 'horizontal');
    await recordCreatedPanels(container);

    await tab(1).click();

    // The "RND-STA-01 for mirrored fields" row (TAB-STA-14's class clause, TAB-STA-20). The Panel's first render carries
    // the module's attribute. On WebAssembly the module's report runs .NET inside its own observer callback, so the
    // mirror's render lands before this later observer reads the Panel, before paint; on Server the first render keeps
    // the class of the mirror's previous value and the class follows a round trip later.
    const className = mode === 'wasm' ? 'dir-right' : 'dir-none';
    await expect.poll(() => createdPanels(container)).toEqual([{ panel: '1', direction: 'right', className }]);
    await expect(panel(1)).toHaveClass('dir-right');
    await expect(panel(1)).toHaveAttribute('data-activation-direction', 'right');
  });
});
