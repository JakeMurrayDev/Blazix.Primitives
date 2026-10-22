import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/switch.md that render neither Field nor Form, and the L4 clauses of its L2 rows, on
// SwitchRoot over the `button` module's hidden-input bridge and the `dom` primitive's `checked`. Titles are Base UI's
// (switch/root/SwitchRoot.test.tsx); "port:" titles assert the per-mode rows of ADR 01's relaxation table (SWT-PTR-09,
// SWT-KEY-08, and the cancelled-toggle row of 2026-10-22: a C# veto is decided inside the dispatch on WebAssembly and a
// round trip late on Server). The fixtures record OnCheckedChange and the consumer's clicks as data attributes.

type FixtureName = 'switch-basic' | 'switch-labels' | 'switch-form';

/** Opens a Switch fixture and waits until every switch has its behaviors attached. */
async function gotoSwitches(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
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

function switchCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    root: container.locator('[data-switch]'),
    input: container.locator('input[type="checkbox"]'),
    label: container.locator('[data-label]'),
    ancestor: container.locator('[data-ancestor]'),
    before: container.locator('[data-before]'),
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

/** Clicks the hidden input directly, as a script or a label does: it is visually hidden, so it is not hit-testable. */
async function clickInput(input: Locator): Promise<void> {
  await input.evaluate((element) => (element as HTMLInputElement).click());
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

test.describe('<Switch.Root />', () => {
  test.describe('interactions', () => {
    test('tolerates imperative interaction in its ref callback before the hidden input mounts', async ({ page, mode }) => {
      const errors: string[] = [];
      page.on('pageerror', (error) => errors.push(error.message));
      // The switch's element is focused, blurred and clicked the moment it is inserted, before its behaviors attach.
      await page.addInitScript(() => {
        new MutationObserver((_records, observer) => {
          const root = document.querySelector<HTMLElement>('[data-case="cancel"] [data-switch]');
          if (root) {
            observer.disconnect();
            root.focus();
            root.blur();
            root.click();
            document.documentElement.dataset.imperative = 'done';
          }
        }).observe(document, { childList: true, subtree: true });
      });

      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      await settle(fixture);

      await expect(page.locator('html')).toHaveAttribute('data-imperative', 'done');
      const { root, input } = switchCase(fixture, 'cancel');
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
      expect(errors).toEqual([]);
    });

    test('should change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root, input } = switchCase(fixture, 'uncontrolled');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await root.click();

      await expect(root).toHaveAttribute('aria-checked', 'true');
      await expect(input).toHaveJSProperty('checked', true);
      await expect(container).toHaveAttribute('data-changes', '1');
      // The change event is the click dispatched on the input: detail 0, carrying no pointer type.
      await expect(container).toHaveAttribute('data-last-change', 'value=true type=click detail=0 shift=false reason=none');
    });

    test('ignores a hidden input click canceled before React handles it', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root, input } = switchCase(fixture, 'uncontrolled');

      await input.evaluate((element) => {
        const event = new MouseEvent('click', { bubbles: true, cancelable: true });
        event.preventDefault();
        element.dispatchEvent(event);
      });
      await settle(fixture);

      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(root).toHaveAttribute('aria-checked', 'false');
    });

    for (const key of ['Enter', 'Space']) {
      test(`can be activated with ${key} key`, async ({ page, mode }) => {
        const fixture = await gotoSwitches(page, mode, 'switch-basic');
        const { container, root, before } = switchCase(fixture, 'uncontrolled');
        await expect(root).toHaveAttribute('aria-checked', 'false');

        await before.focus();
        await page.keyboard.press('Tab');
        await expect(root).toBeFocused();
        await page.keyboard.press(key, { delay: 0 });

        await expect(root).toHaveAttribute('aria-checked', 'true');
        await expect(container).toHaveAttribute('data-changes', '1');
      });
    }
  });

  test.describe('prop: onCheckedChange', () => {
    test('should report keyboard modifier event properties when calling onCheckedChange', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root } = switchCase(fixture, 'uncontrolled');

      await root.click({ modifiers: ['Shift'] });

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-last-change', 'value=true type=click detail=0 shift=true reason=none');
    });

    test('port: a canceled toggle keeps the state and leaves the hidden input unchecked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root, input } = switchCase(fixture, 'cancel');

      await root.click();
      await clickInput(input);
      await settle(fixture);

      await expect(container).toHaveAttribute('data-changes', '2');
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
    });

    test('port: the hidden input follows a controlled switch, and is restored when the consumer ignores a toggle', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const controlled = switchCase(fixture, 'controlled');
      const ignored = switchCase(fixture, 'ignored');

      await controlled.container.locator('[data-toggle]').click();
      await expect(controlled.root).toHaveAttribute('aria-checked', 'true');
      await expect(controlled.input).toHaveJSProperty('checked', true);
      await controlled.root.click();
      await expect(controlled.root).toHaveAttribute('aria-checked', 'false');
      await expect(controlled.input).toHaveJSProperty('checked', false);

      await ignored.root.click();
      await settle(fixture);
      await expect(ignored.container).toHaveAttribute('data-changes', '1');
      await expect(ignored.root).toHaveAttribute('aria-checked', 'false');
      await expect(ignored.input).toHaveJSProperty('checked', false);
    });

    test('port: a canceled toggle never flips the box on WebAssembly and is restored a round trip later on Server', async ({ page, mode }) => {
      // ADR 01 relaxation table, the row of 2026-10-22: the `toggle` decision reverts the box inside the dispatch on
      // WebAssembly; on Server the box stays as the click left it until the restoring update arrives.
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { root, input } = switchCase(fixture, 'cancel');
      await input.evaluate((element) => {
        element.addEventListener('change', () => {
          const dataset = (element as HTMLElement).dataset;
          dataset.changeEvents = String(Number(dataset.changeEvents ?? '0') + 1);
        });
      });

      const checkedAfterClick = await root.evaluate((element) => {
        (element as HTMLElement).click();
        return (element.parentElement!.querySelector('input[type="checkbox"]') as HTMLInputElement).checked;
      });

      if (mode === 'wasm') {
        expect(checkedAfterClick).toBe(false);
        await settle(fixture);
        await expect(input).not.toHaveAttribute('data-change-events');
      } else {
        expect(checkedAfterClick).toBe(true);
        await expect(input).toHaveJSProperty('checked', false);
        await expect(input).toHaveAttribute('data-change-events', '1');
      }
      await expect(root).toHaveAttribute('aria-checked', 'false');
    });
  });

  test.describe('prop: onClick', () => {
    test('should call onClick when clicked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root } = switchCase(fixture, 'uncontrolled');

      await root.click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      // SWT-PTR-09: the consumer's onClick runs before the toggle on WebAssembly (inside the dispatch); on Server the
      // module's report precedes Blazor's dispatch of the same click (ADR 01 row "EVD-EVT-26 against composite/button key
      // handling").
      await expect(container).toHaveAttribute('data-log', mode === 'wasm' ? 'click change' : 'change click');
    });

    for (const nativeButton of [false, true]) {
      const name = nativeButton ? 'native' : 'uncontrolled';

      test(`propagates a single click event to ancestors per user click (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const fixture = await gotoSwitches(page, mode, 'switch-basic');
        const { root, ancestor } = switchCase(fixture, name);
        await countClicks(ancestor);

        await root.click();

        await expect(ancestor).toHaveAttribute('data-observed-clicks', '1');
        await expect(root).toHaveAttribute('aria-checked', 'true');
      });

      test(`does not propagate to ancestors when stopPropagation() is called (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const fixture = await gotoSwitches(page, mode, 'switch-basic');
        const { root, ancestor } = switchCase(fixture, name);
        await countClicks(ancestor);
        await root.evaluate((element) => element.addEventListener('click', (event) => event.stopPropagation()));

        await root.click();

        await expect(root).toHaveAttribute('aria-checked', 'true');
        await expect(ancestor).not.toHaveAttribute('data-observed-clicks');
      });
    }

    test('port: a C# onClick veto stops the toggle on WebAssembly and is too late on Server', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root } = switchCase(fixture, 'vetoed');

      await root.click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      await expect(root).toHaveAttribute('aria-checked', mode === 'wasm' ? 'false' : 'true');
      await expect(container).toHaveAttribute('data-changes', mode === 'wasm' ? '0' : '1');
    });

    test('port: a C# onKeyDown veto of Enter stops the toggle on WebAssembly and is too late on Server; a JavaScript preventDefault() stops it on both', async ({ page, mode }) => {
      // SWT-KEY-08: the rows "EVD-EVT-26 against composite/button key handling" and "UBT-KEY-03/04".
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const vetoed = switchCase(fixture, 'vetoed');
      const uncontrolled = switchCase(fixture, 'uncontrolled');

      await vetoed.root.focus();
      await page.keyboard.press('Enter');
      await settle(fixture);
      await expect(vetoed.root).toHaveAttribute('aria-checked', mode === 'wasm' ? 'false' : 'true');

      await uncontrolled.container.evaluate((element) => element.addEventListener('keydown', (event) => event.preventDefault(), { capture: true }));
      await uncontrolled.root.focus();
      await page.keyboard.press('Enter');
      await settle(fixture);
      await expect(uncontrolled.root).toHaveAttribute('aria-checked', 'false');
    });
  });

  test.describe('prop: disabled', () => {
    test('should not change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root } = switchCase(fixture, 'disabled');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      const prevented = await root.evaluate((element) => {
        const pointerdown = new PointerEvent('pointerdown', { bubbles: true, cancelable: true });
        element.dispatchEvent(pointerdown);
        const click = new MouseEvent('click', { bubbles: true, cancelable: true, detail: 1 });
        element.dispatchEvent(click);
        return [pointerdown.defaultPrevented, click.defaultPrevented];
      });
      await settle(fixture);

      expect(prevented).toEqual([true, true]);
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(container).toHaveAttribute('data-clicks', '0');
    });
  });

  test.describe('prop: readOnly', () => {
    test('should not change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root, input } = switchCase(fixture, 'readonly');

      const prevented = await root.evaluate((element) => {
        const click = new MouseEvent('click', { bubbles: false, cancelable: true, detail: 1 });
        element.dispatchEvent(click);
        return click.defaultPrevented;
      });
      await settle(fixture);

      expect(prevented).toBe(false);
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('should not change its state when its label is clicked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-basic');
      const { container, root, input, label } = switchCase(fixture, 'readonly');

      await label.evaluate((element) => (element as HTMLElement).click());
      await settle(fixture);

      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
      await expect(container).toHaveAttribute('data-changes', '0');
    });
  });

  test.describe('with native <label>', () => {
    test('should toggle the switch when a wrapping <label> is clicked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { container, root, label } = switchCase(fixture, 'wrapping');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await label.click();

      await expect(root).toHaveAttribute('aria-checked', 'true');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '1');

      // A click on the switch inside the label toggles it once: the root's click is prevented so the label does not
      // click the input a second time.
      await root.click();
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '2');
    });

    test('should toggle the switch when a explicitly linked <label> is clicked', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { container, root, input, label } = switchCase(fixture, 'explicit');
      await expect(input).toHaveAttribute('id', 'mySwitch');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await label.click();

      await expect(root).toHaveAttribute('aria-checked', 'true');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '1');
    });

    test('should associate `id` with the native button when `nativeButton=true`', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { root, input, label } = switchCase(fixture, 'native');
      await expect(root).toHaveAttribute('id', 'myNativeSwitch');
      await expect(input).not.toHaveAttribute('id', 'myNativeSwitch');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await label.click();

      await expect(root).toHaveAttribute('aria-checked', 'true');
    });
  });

  test.describe('Form', () => {
    /** Submits the form and returns the entries of its FormData, with the navigation prevented. */
    async function submitEntries(fixture: Locator): Promise<[string, string][]> {
      await fixture.locator('[data-form]').evaluate((form) => {
        form.addEventListener('submit', (event) => {
          event.preventDefault();
          (form as HTMLElement).dataset.entries = JSON.stringify([...new FormData(form as HTMLFormElement).entries()]);
        }, { once: true });
      });
      await fixture.locator('[data-submit]').click();
      const json = await fixture.locator('[data-form]').getAttribute('data-entries');
      await fixture.locator('[data-form]').evaluate((form) => delete (form as HTMLElement).dataset.entries);
      return JSON.parse(json!) as [string, string][];
    }

    test('does not submit uncheckedValue when disabled', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form');

      const entries = await submitEntries(fixture);

      expect(entries.filter(([name]) => name.startsWith('disabled'))).toEqual([]);
    });

    test('submits to an external form when `form` is provided', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form');
      await fixture.locator('[data-switch="external"]').click();
      await expect(fixture.locator('[data-switch="external"]')).toHaveAttribute('aria-checked', 'true');

      const entries = await submitEntries(fixture);

      expect(entries).toContainEqual(['external', 'on']);
    });

    test('submits uncheckedValue to an external form when off', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form');

      expect(await submitEntries(fixture)).toContainEqual(['external-off', 'off']);

      await fixture.locator('[data-switch="external-off"]').click();
      await expect(fixture.locator('[data-switch="external-off"]')).toHaveAttribute('aria-checked', 'true');
      const entries = await submitEntries(fixture);
      expect(entries).toContainEqual(['external-off', 'on']);
      expect(entries).not.toContainEqual(['external-off', 'off']);
    });

    test('port: a native form submits the value and the unchecked value across an off/on/off cycle', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form');
      const cycle = fixture.locator('[data-switch="cycle"]');

      expect((await submitEntries(fixture)).filter(([name]) => name === 'cycle')).toEqual([['cycle', 'no']]);
      await cycle.click();
      await expect(cycle).toHaveAttribute('aria-checked', 'true');
      expect((await submitEntries(fixture)).filter(([name]) => name === 'cycle')).toEqual([['cycle', 'yes']]);
      await cycle.click();
      await expect(cycle).toHaveAttribute('aria-checked', 'false');
      expect((await submitEntries(fixture)).filter(([name]) => name === 'cycle')).toEqual([['cycle', 'no']]);
    });
  });

  test.describe('form reset', () => {
    test('port: a native form reset keeps the state and re-syncs the hidden input, so the next submit matches it', async ({ page, mode }) => {
      // switch.md §16 Q1 (b), chosen by the user 2026-10-22; SWT-FRM-15's state clause and FFI-FRM-30's dated note.
      const fixture = await gotoSwitches(page, mode, 'switch-form');
      const cycle = fixture.locator('[data-switch="cycle"]');
      const input = cycle.locator('xpath=following-sibling::input[@type="checkbox"][1]');
      await cycle.click();
      await expect(cycle).toHaveAttribute('aria-checked', 'true');

      await fixture.locator('[data-reset]').click();

      await expect(input).toHaveJSProperty('checked', true);
      await expect(cycle).toHaveAttribute('aria-checked', 'true');
      await fixture.locator('[data-form]').evaluate((form) => {
        form.addEventListener('submit', (event) => {
          event.preventDefault();
          (form as HTMLElement).dataset.entries = JSON.stringify([...new FormData(form as HTMLFormElement).entries()]);
        }, { once: true });
      });
      await fixture.locator('[data-submit]').click();
      const entries = JSON.parse((await fixture.locator('[data-form]').getAttribute('data-entries'))!) as [string, string][];
      expect(entries.filter(([name]) => name === 'cycle')).toEqual([['cycle', 'yes']]);
    });
  });

  test('can render a native button', async ({ page, mode }) => {
    const fixture = await gotoSwitches(page, mode, 'switch-basic');
    const { container, root, before } = switchCase(fixture, 'native');
    await expect(root).toHaveJSProperty('tagName', 'BUTTON');

    await before.focus();
    await page.keyboard.press('Tab');
    await expect(root).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(root).toHaveAttribute('aria-checked', 'true');
    await page.keyboard.press('Space');
    await expect(root).toHaveAttribute('aria-checked', 'false');
    await settle(fixture);
    await expect(container).toHaveAttribute('data-changes', '2');
  });
});
