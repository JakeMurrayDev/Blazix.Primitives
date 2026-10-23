import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/switch.md and the L4 clauses of its L2 rows, the Field and Form ones included, on
// SwitchRoot over the `button` module's hidden-input bridge and the `dom` primitive's `checked`. Titles are Base UI's
// (switch/root/SwitchRoot.test.tsx); "port:" titles assert the per-mode rows of ADR 01's relaxation table (SWT-PTR-09,
// SWT-KEY-08, and the cancelled-toggle row of 2026-10-22: a C# veto is decided inside the dispatch on WebAssembly and a
// round trip late on Server). The fixtures record OnCheckedChange and the consumer's clicks as data attributes.

type FixtureName = 'switch-basic' | 'switch-labels' | 'switch-form' | 'switch-field' | 'switch-form-field' | 'switch-edit-form';

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

  test.describe('native label fallback', () => {
    test('sets `aria-labelledby` from a sibling label associated with the hidden input', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { root, label } = switchCase(fixture, 'explicit');

      await expect(label).not.toHaveAttribute('id', '');
      const labelId = await label.getAttribute('id');
      expect(labelId).toBe('mySwitch-label');
      await expect(root).toHaveAttribute('aria-labelledby', labelId!);
    });

    test('port: a wrapping native label names the switch', async ({ page, mode }) => {
      // SWT-ARIA-07's citation "should toggle the switch when a wrapping <label> is clicked" (name derived).
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { root, label } = switchCase(fixture, 'wrapping');

      await expect(root).toHaveAttribute('aria-labelledby', /.+/);
      await expect(label).toHaveAttribute('id', (await root.getAttribute('aria-labelledby'))!);
      await expect(root).toHaveAccessibleName('Toggle');
    });

    test('updates fallback `aria-labelledby` when the hidden input id changes', async ({ page, mode }) => {
      // SWT-ARIA-08 (row "FFI registration"): the module reports the label after the id change; C# renders it, a round
      // trip later on Server. SWT-DOM-18: the previously assigned id stays on the first label.
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { container, root } = switchCase(fixture, 'id-change');
      const labelA = container.locator('[data-label-a]');
      const labelB = container.locator('[data-label-b]');
      await expect(root).toHaveAttribute('aria-labelledby', 'switch-input-a-label');
      await expect(labelA).toHaveAttribute('id', 'switch-input-a-label');

      await container.locator('[data-toggle]').click();

      await expect(container.locator('input[type="checkbox"]')).toHaveAttribute('id', 'switch-input-b');
      await expect(root).toHaveAttribute('aria-labelledby', 'switch-input-b-label');
      await expect(labelB).toHaveAttribute('id', 'switch-input-b-label');
      await expect(labelA).toHaveAttribute('id', 'switch-input-a-label');
    });

    test('prefers `aria-label` over an associated label', async ({ page, mode }) => {
      // The native label half; the Field.Label half is SwitchFieldTests at L2.
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { root } = switchCase(fixture, 'aria-label');
      await settle(fixture);

      await expect(root).not.toHaveAttribute('aria-labelledby');
      await expect(root).toHaveAccessibleName('Wi-Fi');
    });

    test('port: focus on the hidden input moves to the switch', async ({ page, mode }) => {
      // SWT-FOC-03 (Test: none), the form module's focus redirect (FFI-FOC-05).
      const fixture = await gotoSwitches(page, mode, 'switch-labels');
      const { root, input } = switchCase(fixture, 'explicit');

      await input.evaluate((element) => (element as HTMLInputElement).focus());

      await expect(root).toBeFocused();
    });
  });

  test.describe('Field', () => {
    test.describe('[data-focused] without a blur event', () => {
      test('is removed when the focused switch becomes disabled', async ({ page, mode }) => {
        const fixture = await gotoSwitches(page, mode, 'switch-field');
        const container = fixture.locator('[data-case="focus"]');
        const root = container.locator('[data-switch]');
        await root.focus();
        await expect(container.locator('[data-root]')).toHaveAttribute('data-focused', '');
        await expect(root).toHaveAttribute('data-focused', '');

        await container.locator('[data-disable]').evaluate((element) => (element as HTMLElement).click());

        await expect(container.locator('[data-root]')).not.toHaveAttribute('data-focused');
        await expect(root).not.toHaveAttribute('data-focused');
      });

      test('is removed when the focused switch unmounts', async ({ page, mode }) => {
        const fixture = await gotoSwitches(page, mode, 'switch-field');
        const container = fixture.locator('[data-case="focus"]');
        await container.locator('[data-switch]').focus();
        await expect(container.locator('[data-root]')).toHaveAttribute('data-focused', '');

        await container.locator('[data-unmount]').evaluate((element) => (element as HTMLElement).click());

        await expect(container.locator('[data-switch]')).toHaveCount(0);
        await expect(container.locator('[data-root]')).not.toHaveAttribute('data-focused');
      });
    });

    test.describe('Field.Label', () => {
      test('sets `for` on the label', async ({ page, mode }) => {
        // SWT-FRM-16's label-click clause: the browser clicks the input the label points at.
        const fixture = await gotoSwitches(page, mode, 'switch-field');
        const container = fixture.locator('[data-case="wrapping"]');
        const label = container.locator('[data-label]');
        const root = container.locator('[data-switch]');
        await expect(label).toHaveAttribute('for', (await container.locator('input[type="checkbox"]').getAttribute('id'))!);
        await expect(root).toHaveAttribute('aria-labelledby', (await label.getAttribute('id'))!);

        await label.evaluate((element) => (element as HTMLElement).click());

        await expect(root).toHaveAttribute('aria-checked', 'true');
      });

      test('when the label is sibling to the switch', async ({ page, mode }) => {
        const fixture = await gotoSwitches(page, mode, 'switch-field');
        const container = fixture.locator('[data-case="sibling"]');
        const root = container.locator('[data-switch]');
        await expect(root).toHaveAttribute('aria-checked', 'false');

        await container.locator('[data-label]').click();

        await expect(root).toHaveAttribute('aria-checked', 'true');
      });

      test('when rendering a non-native label', async ({ page, mode }) => {
        // SWT-PTR-10: a non-native label focuses the switch and does not toggle it.
        const fixture = await gotoSwitches(page, mode, 'switch-field');
        const container = fixture.locator('[data-case="non-native-label"]');
        const label = container.locator('[data-label]');
        const root = container.locator('[data-switch]');
        await expect(label).not.toHaveAttribute('for');
        await expect(root).toHaveAttribute('aria-labelledby', (await label.getAttribute('id'))!);

        await label.evaluate((element) => (element as HTMLElement).click());
        await settle(fixture);

        await expect(root).toBeFocused();
        await expect(root).toHaveAttribute('aria-checked', 'false');
      });
    });

    test('port: the field\'s filled state lands with the toggle on WebAssembly and a round trip later on Server', async ({ page, mode }) => {
      // SWT-STA-06 and SWT-FRM-06 (ADR 01 row "FFI registration"): the field's writes run after the render that shows
      // the new value, in OnAfterRender: before paint on WebAssembly, after paint plus a round trip on Server. The page
      // records the switch's state at every animation frame from the click on, which is what a frame can paint.
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      const fixture = await gotoSwitches(page, mode, 'switch-field');
      const root = fixture.locator('[data-case="filled"] [data-switch]');
      await root.evaluate((element) => {
        const frames: string[] = [];
        (window as unknown as { filledFrames: string[] }).filledFrames = frames;
        element.addEventListener('click', () => {
          const record = () => {
            const state = `${element.getAttribute('aria-checked')}:${element.hasAttribute('data-filled')}`;
            frames.push(state);
            if (state !== 'true:true' && frames.length < 600) {
              requestAnimationFrame(record);
            }
          };
          requestAnimationFrame(record);
        });
      });

      await root.click();
      await expect(root).toHaveAttribute('data-filled', '');
      await expect.poll(() => page.evaluate(() => (window as unknown as { filledFrames: string[] }).filledFrames.at(-1))).toBe('true:true');

      const frames = await page.evaluate(() => (window as unknown as { filledFrames: string[] }).filledFrames);
      if (mode === 'wasm') {
        expect(frames).not.toContain('true:false');
      } else {
        expect(frames).toContain('true:false');
      }
    });
  });

  test.describe('Form with Field', () => {
    interface Submission {
      prevented: boolean;
      entries: [string, string][];
    }

    /** Records the FormData of every submit event of the form in the page, and stops a navigation nobody prevented. */
    async function recordSubmissions(form: Locator): Promise<() => Promise<Submission[]>> {
      await form.evaluate((element) => {
        const records: Submission[] = [];
        (element as HTMLElement & { submissions?: Submission[] }).submissions = records;
        element.addEventListener('submit', (event) => {
          const entries = [...new FormData(element as HTMLFormElement).entries()].map(([key, value]) => [key, String(value)] as [string, string]);
          records.push({ prevented: event.defaultPrevented, entries });
          event.preventDefault();
        });
      });
      return () => form.evaluate((element) => [...((element as HTMLElement & { submissions?: Submission[] }).submissions ?? [])]);
    }

    /** The values a name had in the last recorded submission. */
    function lastValues(submissions: Submission[], name: string): string[] {
      return submissions.at(-1)!.entries.filter(([key]) => key === name).map(([, value]) => value);
    }

    function formCase(fixture: Locator, name: string) {
      const container = fixture.locator(`[data-case="${name}"]`);
      return { container, form: container.locator('form'), root: container.locator('[data-switch]'), submit: container.locator('[data-submit]') };
    }

    test('triggers native HTML validation on submit', async ({ page, mode }) => {
      // SWT-FRM-08: the field is invalid with valueMissing, the submission is blocked and the switch is focused.
      const fixture = await gotoSwitches(page, mode, 'switch-form-field');
      const { container, root, submit } = formCase(fixture, 'native-validation');
      await expect(container.locator('[data-error]')).toHaveCount(0);

      await submit.click();

      await expect(container.locator('[data-error]')).toHaveText('required');
      await expect(root).toHaveAttribute('aria-invalid', 'true');
      await expect(root).toBeFocused();
      await settle(fixture);
      await expect(container).toHaveAttribute('data-submits', '0');
    });

    test('prop: validationMode=onSubmit', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form-field');
      const { container, root, submit } = formCase(fixture, 'on-submit');
      await expect(root).not.toHaveAttribute('aria-invalid');

      await submit.click();
      await expect(root).toHaveAttribute('aria-invalid', 'true');
      await expect(container.locator('[data-error]')).toHaveCount(1);

      await root.click();
      await expect(root).not.toHaveAttribute('aria-invalid');
      await expect(container.locator('[data-error]')).toHaveCount(0);

      await root.click();
      await expect(root).toHaveAttribute('aria-invalid', 'true');
      await expect(container.locator('[data-error]')).toHaveCount(1);
    });

    test('preserves Field validation props through canceled changes, submit, and reset', async ({ page, mode }) => {
      // SWT-EVT-04, SWT-FRM-08, SWT-FRM-15, SWT-FRM-17.
      const fixture = await gotoSwitches(page, mode, 'switch-form-field');
      const { container, root, submit } = formCase(fixture, 'preserves');
      const descriptionId = await container.locator('[data-description]').getAttribute('id');
      await expect(root).toHaveAttribute('aria-describedby', `external-description ${descriptionId}`);

      await root.click();
      await settle(fixture);
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(container.locator('input[type="checkbox"]')).toHaveJSProperty('checked', false);

      await submit.click();
      await expect(root).toHaveAttribute('aria-invalid', 'true');
      await expect(container.locator('[data-error]')).toHaveText('required');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-submits', '0');

      await container.locator('[data-stop-canceling]').evaluate((element) => (element as HTMLElement).click());
      await root.click();
      await expect(root).toHaveAttribute('aria-checked', 'true');
      await expect(root).not.toHaveAttribute('aria-invalid');
      await submit.click();
      await settle(fixture);
      await expect(container).toHaveAttribute('data-submits', '1');

      await container.locator('[data-reset]').click();
      await settle(fixture);
      await expect(root).toHaveAttribute('aria-checked', 'true');
      await expect(root).not.toHaveAttribute('aria-invalid');
      await expect(root).toHaveAttribute('aria-describedby', `external-description ${descriptionId}`);
    });

    test('should include the switch value in form submission, matching native checkbox behavior', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form-field');
      const { form, root, submit } = formCase(fixture, 'value');
      const submissions = await recordSubmissions(form);

      await submit.click();
      await expect.poll(async () => (await submissions()).length).toBe(1);
      expect(lastValues(await submissions(), 'test-switch')).toEqual([]);

      await root.click();
      await expect(root).toHaveAttribute('aria-checked', 'true');
      await submit.click();
      await expect.poll(async () => (await submissions()).length).toBe(2);
      expect(lastValues(await submissions(), 'test-switch')).toEqual(['on']);
    });

    test('matches native checkbox form submission behavior', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form-field');
      const native = fixture.locator('[data-case="native-checkbox"]');
      const nativeSubmissions = await recordSubmissions(native.locator('form'));
      await native.locator('[data-submit]').click();
      await native.locator('[data-native-checkbox]').click();
      await native.locator('[data-submit]').click();
      const nativeRecords = await nativeSubmissions();

      const { form, root, submit } = formCase(fixture, 'value');
      const customSubmissions = await recordSubmissions(form);
      await submit.click();
      await expect.poll(async () => (await customSubmissions()).length).toBe(1);
      await root.click();
      await expect(root).toHaveAttribute('aria-checked', 'true');
      await submit.click();
      await expect.poll(async () => (await customSubmissions()).length).toBe(2);
      const customRecords = await customSubmissions();

      expect(nativeRecords.map((r) => r.entries.filter(([k]) => k === 'native').map(([, v]) => v))).toEqual([[], ['on']]);
      expect(customRecords.map((r) => r.entries.filter(([k]) => k === 'test-switch').map(([, v]) => v))).toEqual([[], ['on']]);
    });

    for (const [title, name, off] of [
      ['should submit uncheckedValue when switch is off and uncheckedValue is specified', 'unchecked-value', 'off'],
      ['should submit custom uncheckedValue when switch is off', 'custom-unchecked-value', 'false'],
    ] as const) {
      test(title, async ({ page, mode }) => {
        const fixture = await gotoSwitches(page, mode, 'switch-form-field');
        const { form, root, submit } = formCase(fixture, name);
        const submissions = await recordSubmissions(form);

        await submit.click();
        await expect.poll(async () => (await submissions()).length).toBe(1);
        expect(lastValues(await submissions(), 'test-switch')).toEqual([off]);

        await root.click();
        await expect(root).toHaveAttribute('aria-checked', 'true');
        await submit.click();
        await expect.poll(async () => (await submissions()).length).toBe(2);
        expect(lastValues(await submissions(), 'test-switch')).toEqual(['on']);

        await root.click();
        await expect(root).toHaveAttribute('aria-checked', 'false');
        await submit.click();
        await expect.poll(async () => (await submissions()).length).toBe(3);
        expect(lastValues(await submissions(), 'test-switch')).toEqual([off]);
      });
    }

    test('submits custom value and uncheckedValue across an off/on/off cycle', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-form-field');
      const { form, root, submit } = formCase(fixture, 'cycle');
      const submissions = await recordSubmissions(form);

      for (const [expected, count] of [['no', 1], ['yes', 2], ['no', 3]] as const) {
        if (count > 1) {
          await root.click();
          await expect(root).toHaveAttribute('aria-checked', expected === 'yes' ? 'true' : 'false');
        }
        await submit.click();
        await expect.poll(async () => (await submissions()).length).toBe(count);
        expect(lastValues(await submissions(), 'test-switch')).toEqual([expected]);
      }
    });
  });

  test.describe('EditForm', () => {
    test('@verifies ADR-05/5 a switch bound in an EditForm notifies the EditContext and shows its validator\'s message', async ({ page, mode }) => {
      const fixture = await gotoSwitches(page, mode, 'switch-edit-form');
      const root = fixture.locator('[data-switch]');
      await expect(fixture.locator('input[type="checkbox"]')).toHaveAttribute('name', 'Terms');

      await fixture.getByRole('button', { name: 'Submit' }).click();
      await expect(fixture).toHaveAttribute('data-invalid-submits', '1');
      await expect(fixture.locator('[data-testid="message"]')).toHaveText('Accept the terms.');
      await expect(root).toHaveAttribute('aria-invalid', 'true');
      await expect(fixture.locator('[data-error]')).toHaveText('Accept the terms.');

      await root.click();
      await expect(root).toHaveAttribute('aria-checked', 'true');
      await expect(fixture.locator('[data-testid="message"]')).toHaveCount(0);
      await expect(root).not.toHaveAttribute('aria-invalid');
      await fixture.getByRole('button', { name: 'Submit' }).click();
      await expect(fixture).toHaveAttribute('data-valid-submits', '1');
    });
  });

});
