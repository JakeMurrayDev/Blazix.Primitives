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

      // TAB-PTR-01's "synchronously after the click" (design point 8 of the Tabs track, pending the user): the request
      // runs inside the `select` decision on WebAssembly; on Server the module reports the activation and C# commits a
      // round trip later.
      const selectedAfterClick = await tab(1).evaluate((element) => {
        (element as HTMLElement).click();
        return element.getAttribute('aria-selected');
      });

      expect(selectedAfterClick).toBe(mode === 'wasm' ? 'true' : 'false');
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

    // TAB-STA-16/17 (design point 7, pending the user): the new Tab is in the batch the module reads, so the positional
    // direction is the first one shown; the provisional value-based guess ('Account' < 'Overview') never appears.
    await expect(root).toHaveAttribute('data-activation-direction', 'right');
    await expect.poll(() => createdPanels(container)).toEqual([expect.objectContaining({ panel: 'Account', direction: 'right' })]);
    await expect(panel('Account')).toHaveClass('dir-right');
  });
});
