import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/checkbox.md that render neither Field, Form nor CheckboxGroup, and the L4 clauses of
// its L2 rows, on CheckboxRoot over the `button` module's hidden-input bridge and the `dom` primitive's `checked` and
// `indeterminate`, and on CheckboxIndicator over the `transitions` module. Titles are Base UI's (checkbox/root/
// CheckboxRoot.test.tsx, checkbox/indicator/CheckboxIndicator.test.tsx); "port:" titles assert the per-mode rows of ADR
// 01's relaxation table (CHK-PTR-09, CHK-KEY-11, the cancelled-toggle row of 2026-10-22, TRN-ANI-03/10, the mirrored
// transition status). The fixtures record OnCheckedChange and the consumer's clicks as data attributes. Animated cases
// open with `?animations=1` (ADR 06 §2) and witness transient attributes by recorded markers, never by frame polls.

type FixtureName = 'checkbox-basic' | 'checkbox-labels' | 'checkbox-form' | 'checkbox-indicator' | 'checkbox-field' | 'checkbox-enter' | 'checkbox-names' | 'checkbox-base-ui-form';

/** Opens a Checkbox fixture and waits until every box and listed indicator has its behaviors attached. */
async function gotoCheckboxes(page: Page, mode: RenderMode, name: FixtureName, options: { animations?: boolean } = {}): Promise<Locator> {
  const fixture = options.animations
    ? await gotoAnimated(page, mode, name)
    : await gotoFixture(page, mode, name);
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

function checkboxCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    root: container.locator('[data-checkbox]').first(),
    input: container.locator('input[type="checkbox"]').first(),
    label: container.locator('[data-label]'),
    ancestor: container.locator('[data-ancestor]'),
    before: container.locator('[data-before]'),
    toggle: container.locator('[data-toggle]'),
    indicator: container.locator('[data-indicator]').first(),
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

test.describe('<Checkbox.Root />', () => {
  test.describe('interactions', () => {
    test('tolerates imperative interaction in its ref callback before the hidden input mounts', async ({ page, mode }) => {
      const errors: string[] = [];
      page.on('pageerror', (error) => errors.push(error.message));
      // The box's element is focused, blurred and clicked the moment it is inserted, before its behaviors attach.
      await page.addInitScript(() => {
        new MutationObserver((_records, observer) => {
          const root = document.querySelector<HTMLElement>('[data-case="cancel"] [data-checkbox]');
          if (root) {
            observer.disconnect();
            root.focus();
            root.blur();
            root.click();
            document.documentElement.dataset.imperative = 'done';
          }
        }).observe(document, { childList: true, subtree: true });
      });

      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      await settle(fixture);

      await expect(page.locator('html')).toHaveAttribute('data-imperative', 'done');
      const { root, input } = checkboxCase(fixture, 'cancel');
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
      expect(errors).toEqual([]);
    });

    test('should change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input } = checkboxCase(fixture, 'uncontrolled');
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);

      await root.click();

      await expect(root).toHaveAttribute('aria-checked', 'true');
      await expect(input).toHaveJSProperty('checked', true);
      await expect(container).toHaveAttribute('data-changes', '1');
      // The change event is the click dispatched on the input: detail 0, carrying no pointer type.
      await expect(container).toHaveAttribute('data-last-change', 'value=true type=click detail=0 shift=false reason=none');

      await root.click();

      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
    });

    test('should report keyboard modifier event properties when calling onCheckedChange', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root } = checkboxCase(fixture, 'uncontrolled');

      await root.click({ modifiers: ['Shift'] });

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-last-change', 'value=true type=click detail=0 shift=true reason=none');
    });

    test('ignores a hidden input click canceled before React handles it', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input } = checkboxCase(fixture, 'uncontrolled');

      await input.evaluate((element) => {
        const event = new MouseEvent('click', { bubbles: true, cancelable: true });
        event.preventDefault();
        element.dispatchEvent(event);
      });
      await settle(fixture);

      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(root).toHaveAttribute('aria-checked', 'false');
    });

    test('can be activated with Space key', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, before } = checkboxCase(fixture, 'uncontrolled');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await before.focus();
      await page.keyboard.press('Tab');
      await expect(root).toBeFocused();
      await page.keyboard.press('Space', { delay: 0 });

      await expect(root).toHaveAttribute('aria-checked', 'true');
      await expect(container).toHaveAttribute('data-changes', '1');
    });

    test('does not activate with Enter key', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, before } = checkboxCase(fixture, 'uncontrolled');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await before.focus();
      await page.keyboard.press('Tab');
      await expect(root).toBeFocused();
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(container).toHaveAttribute('data-changes', '0');
    });
  });

  test.describe('prop: onCheckedChange', () => {
    test('port: a canceled toggle keeps the state and leaves the hidden input unchecked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input } = checkboxCase(fixture, 'cancel');

      await root.click();
      await clickInput(input);
      await settle(fixture);

      await expect(container).toHaveAttribute('data-changes', '2');
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
    });

    test('port: the hidden input follows a controlled box, and is restored when the consumer ignores a toggle', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const controlled = checkboxCase(fixture, 'controlled');
      const ignored = checkboxCase(fixture, 'ignored');

      await controlled.toggle.click();
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
      // ADR 01 relaxation table, the row of 2026-10-22 (CHK-EVT-04, CHK-STA-05): the `toggle` decision reverts the box
      // inside the dispatch on WebAssembly; on Server the box stays as the click left it until the restoring update.
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { root, input } = checkboxCase(fixture, 'cancel');
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
    test('port: the consumer onClick runs once per click, before the toggle on WebAssembly and after it on Server', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root } = checkboxCase(fixture, 'uncontrolled');

      await root.click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      // CHK-PTR-09: the consumer's onClick runs before the toggle on WebAssembly (inside the dispatch); on Server the
      // module's report precedes Blazor's dispatch of the same click (ADR 01 row "EVD-EVT-26 against composite/button key
      // handling").
      await expect(container).toHaveAttribute('data-log', mode === 'wasm' ? 'click change' : 'change click');
    });

    for (const nativeButton of [false, true]) {
      const name = nativeButton ? 'native' : 'uncontrolled';

      test(`propagates a single click event to ancestors per user click (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
        const { root, ancestor } = checkboxCase(fixture, name);
        await countClicks(ancestor);

        await root.click();

        await expect(ancestor).toHaveAttribute('data-observed-clicks', '1');
        await expect(root).toHaveAttribute('aria-checked', 'true');
      });

      test(`does not propagate to ancestors when stopPropagation() is called (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
        const { root, ancestor } = checkboxCase(fixture, name);
        await countClicks(ancestor);
        await root.evaluate((element) => element.addEventListener('click', (event) => event.stopPropagation()));

        await root.click();

        await expect(root).toHaveAttribute('aria-checked', 'true');
        await expect(ancestor).not.toHaveAttribute('data-observed-clicks');
      });
    }

    test('port: a C# onClick veto stops the toggle on WebAssembly and is too late on Server', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root } = checkboxCase(fixture, 'vetoed');

      await root.click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      await expect(root).toHaveAttribute('aria-checked', mode === 'wasm' ? 'false' : 'true');
      await expect(container).toHaveAttribute('data-changes', mode === 'wasm' ? '0' : '1');
    });

    test('port: a C# onKeyUp veto of Space stops the toggle on WebAssembly and is too late on Server; a JavaScript preventDefault() stops it on both', async ({ page, mode }) => {
      // CHK-KEY-11: the rows "EVD-EVT-26 against composite/button key handling" and "UBT-KEY-03/04".
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const vetoed = checkboxCase(fixture, 'vetoed');
      const uncontrolled = checkboxCase(fixture, 'uncontrolled');

      await vetoed.root.focus();
      await page.keyboard.press('Space');
      await settle(fixture);
      await expect(vetoed.root).toHaveAttribute('aria-checked', mode === 'wasm' ? 'false' : 'true');

      // A JavaScript listener that runs before the module's, as Base UI's consumer handler runs before its own.
      await uncontrolled.container.evaluate((element) => element.addEventListener('keyup', (event) => event.preventDefault(), { capture: true }));
      await uncontrolled.root.focus();
      await page.keyboard.press('Space');
      await settle(fixture);
      await expect(uncontrolled.root).toHaveAttribute('aria-checked', 'false');
    });
  });

  test.describe('prop: disabled', () => {
    test('should not change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root } = checkboxCase(fixture, 'disabled');
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
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input } = checkboxCase(fixture, 'readonly');

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
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input, label } = checkboxCase(fixture, 'readonly');

      await label.evaluate((element) => (element as HTMLElement).click());
      await settle(fixture);

      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('port: Space on a read-only box changes nothing', async ({ page, mode }) => {
      // CHK-KEY-12: the click is still dispatched on the root, which ignores it.
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input } = checkboxCase(fixture, 'readonly');

      await root.focus();
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(root).toHaveAttribute('aria-checked', 'false');
      await expect(input).toHaveJSProperty('checked', false);
      await expect(container).toHaveAttribute('data-changes', '0');
    });
  });

  test.describe('prop: indeterminate', () => {
    test('should not change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input } = checkboxCase(fixture, 'indeterminate');
      await expect(root).toHaveAttribute('aria-checked', 'mixed');

      await root.click();

      // CHK-PTR-12: the underlying value flips and is reported; the rendering stays mixed.
      await expect(container).toHaveAttribute('data-last-change', 'value=true type=click detail=0 shift=false reason=none');
      await expect(input).toHaveJSProperty('checked', true);
      await expect(root).toHaveAttribute('aria-checked', 'mixed');
    });

    test('sets the native input state when indeterminate', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { input } = checkboxCase(fixture, 'indeterminate');

      await expect(input).toHaveJSProperty('indeterminate', true);
    });

    test('keeps the native input state when checked changes while indeterminate remains', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
      const { container, root, input } = checkboxCase(fixture, 'indeterminate');

      // Clicking the hidden input natively clears `indeterminate` before toggling; the dom module re-asserts it inside
      // the click, so no listener after it sees it cleared.
      const seen = await root.evaluate((element) => {
        const target = element.parentElement!.querySelector('input[type="checkbox"]') as HTMLInputElement;
        let observed: boolean | null = null;
        target.addEventListener('click', () => {
          observed = target.indeterminate;
        }, { once: true });
        (element as HTMLElement).click();
        return observed;
      });

      expect(seen).toBe(true);
      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(input).toHaveJSProperty('checked', true);
      await expect(input).toHaveJSProperty('indeterminate', true);
      await expect(root).toHaveAttribute('aria-checked', 'mixed');
    });
  });

  test.describe('with native <label>', () => {
    test('should toggle the checkbox when a wrapping <label> is clicked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-labels');
      const { container, root, label } = checkboxCase(fixture, 'wrapping');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await label.click();

      await expect(root).toHaveAttribute('aria-checked', 'true');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '1');
    });

    test('should change state when clicking the checkbox if it has a wrapping label', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-labels');
      const { container, root } = checkboxCase(fixture, 'wrapping');

      // The root's click is prevented, so the label does not click the input a second time.
      await root.click();
      await expect(root).toHaveAttribute('aria-checked', 'true');
      await root.click();
      await expect(root).toHaveAttribute('aria-checked', 'false');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '2');
    });

    test('should toggle the checkbox when a explicitly linked <label> is clicked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-labels');
      const { container, root, input, label } = checkboxCase(fixture, 'explicit');
      await expect(input).toHaveAttribute('id', 'myCheckbox');
      await expect(root).toHaveAttribute('aria-checked', 'false');

      await label.click();

      await expect(root).toHaveAttribute('aria-checked', 'true');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '1');
    });

    test('should associate `id` with the native button when `nativeButton=true`', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-labels');
      const { root, input, label } = checkboxCase(fixture, 'native');
      await expect(root).toHaveAttribute('id', 'myNativeCheckbox');
      await expect(input).not.toHaveAttribute('id', 'myNativeCheckbox');
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

    function named(entries: [string, string][], name: string): [string, string][] {
      return entries.filter(([key]) => key === name);
    }

    test('should include the checkbox value in form submission, matching native checkbox behavior', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const box = fixture.locator('[data-checkbox="plain"]');

      expect(named(await submitEntries(fixture), 'plain')).toEqual([]);
      await box.click();
      await expect(box).toHaveAttribute('aria-checked', 'true');
      expect(named(await submitEntries(fixture), 'plain')).toEqual([['plain', 'on']]);
    });

    test('should include the custom checkbox value in form submission, matching native checkbox behavior', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const box = fixture.locator('[data-checkbox="custom"]');

      await box.click();
      await expect(box).toHaveAttribute('aria-checked', 'true');
      expect(named(await submitEntries(fixture), 'custom')).toEqual([['custom', 'custom-value']]);
    });

    test('matches native checkbox form submission behavior', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const box = fixture.locator('[data-checkbox="plain"]');

      await box.click();
      await expect(box).toHaveAttribute('aria-checked', 'true');
      expect(named(await submitEntries(fixture), 'plain')).toEqual([['plain', 'on']]);
      await box.click();
      await expect(box).toHaveAttribute('aria-checked', 'false');
      expect(named(await submitEntries(fixture), 'plain')).toEqual([]);
    });

    test('should submit uncheckedValue when checkbox is unchecked and uncheckedValue is specified', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const cycle = fixture.locator('[data-checkbox="cycle"]');

      expect(named(await submitEntries(fixture), 'cycle')).toEqual([['cycle', 'no']]);
      await cycle.click();
      await expect(cycle).toHaveAttribute('aria-checked', 'true');
      expect(named(await submitEntries(fixture), 'cycle')).toEqual([['cycle', 'yes']]);
    });

    test('should submit custom uncheckedValue when checkbox is unchecked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const cycle = fixture.locator('[data-checkbox="cycle"]');

      await cycle.click();
      await expect(cycle).toHaveAttribute('aria-checked', 'true');
      await cycle.click();
      await expect(cycle).toHaveAttribute('aria-checked', 'false');
      expect(named(await submitEntries(fixture), 'cycle')).toEqual([['cycle', 'no']]);
    });

    test('does not submit uncheckedValue when disabled', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');

      const entries = await submitEntries(fixture);

      expect(entries.filter(([name]) => name.startsWith('disabled'))).toEqual([]);
    });

    test('submits to an external form when `form` is provided', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const external = fixture.locator('[data-checkbox="external"]');
      await external.click();
      await expect(external).toHaveAttribute('aria-checked', 'true');

      expect(await submitEntries(fixture)).toContainEqual(['external', 'on']);
    });

    test('submits uncheckedValue to an external form when unchecked', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const externalOff = fixture.locator('[data-checkbox="external-off"]');

      expect(await submitEntries(fixture)).toContainEqual(['external-off', 'off']);

      await externalOff.click();
      await expect(externalOff).toHaveAttribute('aria-checked', 'true');
      const entries = await submitEntries(fixture);
      expect(named(entries, 'external-off')).toEqual([['external-off', 'on']]);
    });

    test('port: a parent box is never submitted, ticked or not', async ({ page, mode }) => {
      // CHK-API-05, CHK-EDGE-11: no name on the input, no unchecked-value input.
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const parent = fixture.locator('[data-checkbox="parent"]');

      expect(named(await submitEntries(fixture), 'parent')).toEqual([]);
      await parent.click();
      await expect(parent).toHaveAttribute('aria-checked', 'false');
      expect(named(await submitEntries(fixture), 'parent')).toEqual([]);
    });

    test('port: a native form reset keeps the state and re-syncs the hidden input, so the next submit matches it', async ({ page, mode }) => {
      // checkbox.md §16 Q7 as switch.md §16 Q1 (b), chosen by the user 2026-10-22; CHK-FRM-15's state clause.
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-form');
      const cycle = fixture.locator('[data-checkbox="cycle"]');
      const input = cycle.locator('xpath=following-sibling::input[@type="checkbox"][1]');
      await cycle.click();
      await expect(cycle).toHaveAttribute('aria-checked', 'true');

      await fixture.locator('[data-reset]').click();

      await expect(input).toHaveJSProperty('checked', true);
      await expect(cycle).toHaveAttribute('aria-checked', 'true');
      expect(named(await submitEntries(fixture), 'cycle')).toEqual([['cycle', 'yes']]);
    });
  });

  test.describe('Field', () => {
    test.describe('[data-focused] without a blur event', () => {
      test('is removed when the focused checkbox becomes disabled', async ({ page, mode }) => {
        const fixture = await gotoCheckboxes(page, mode, 'checkbox-field');
        const { container, root } = checkboxCase(fixture, 'focus');
        const field = container.locator('[data-root]');

        await root.focus();
        await expect(field).toHaveAttribute('data-focused', '');

        await container.locator('[data-disable]').click();

        await expect(root).toHaveAttribute('aria-disabled', 'true');
        await expect(field).not.toHaveAttribute('data-focused');
        await expect(root).not.toHaveAttribute('data-focused');
      });

      test('is removed when the focused checkbox unmounts', async ({ page, mode }) => {
        const fixture = await gotoCheckboxes(page, mode, 'checkbox-field');
        const { container, root } = checkboxCase(fixture, 'focus');
        const field = container.locator('[data-root]');

        await root.focus();
        await expect(field).toHaveAttribute('data-focused', '');

        await container.locator('[data-unmount]').evaluate((element) => (element as HTMLElement).click());

        await expect(root).toHaveCount(0);
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('port: a root that is already focused when its tracking starts acquires the focused state', async ({ page, mode }) => {
        // "is kept when the checkbox is focused during mount in StrictMode" as its port property (ADR 06 Amendments
        // 2026-10-18): the root is focused the moment it is inserted, before the form module attaches to it.
        await page.addInitScript(() => {
          new MutationObserver((_records, observer) => {
            const root = document.querySelector<HTMLElement>('[data-case="focus"] [data-checkbox]');
            if (root) {
              observer.disconnect();
              root.focus();
            }
          }).observe(document, { childList: true, subtree: true });
        });

        const fixture = await gotoCheckboxes(page, mode, 'checkbox-field');
        const { container, root } = checkboxCase(fixture, 'focus');

        await expect(root).toBeFocused();
        await expect(container.locator('[data-root]')).toHaveAttribute('data-focused', '');
      });
    });

    test.describe('Field.Label', () => {
      test('when label and checkbox are siblings', async ({ page, mode }) => {
        const fixture = await gotoCheckboxes(page, mode, 'checkbox-field');
        const { root, input, label } = checkboxCase(fixture, 'sibling');
        await expect(label).toHaveAttribute('for', (await input.getAttribute('id'))!);
        await expect(root).toHaveAttribute('aria-labelledby', (await label.getAttribute('id'))!);
        await expect(root).toHaveAttribute('aria-checked', 'false');

        await label.click();

        await expect(root).toHaveAttribute('aria-checked', 'true');
      });

      test('sets `for` on the label', async ({ page, mode }) => {
        const fixture = await gotoCheckboxes(page, mode, 'checkbox-field');
        const { root, input, label } = checkboxCase(fixture, 'wrapping');
        await expect(label).toHaveAttribute('for', (await input.getAttribute('id'))!);
        await expect(root).toHaveAttribute('aria-labelledby', (await label.getAttribute('id'))!);
        await expect(root).toHaveAttribute('aria-checked', 'false');

        await label.getByText('OK').click();

        await expect(root).toHaveAttribute('aria-checked', 'true');
      });
    });

    for (const nativeButton of [false, true]) {
      test(`defers an explicit id until hydration but keeps Field.Label associated during SSR (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        // CHK-EDGE-10, CHK-FRM-16: the explicit id is a claim registered after the render (FFI registration row), so the
        // static response associates the label and the control through the field's generated id.
        const name = nativeButton ? 'native' : 'input';
        const explicit = nativeButton ? 'explicit-native' : 'explicit-input';
        const html = await (await page.request.get(fixtureUrl(mode, 'checkbox-prerender'))).text();
        const section = html.slice(html.indexOf(`data-case="${name}"`));
        const control = nativeButton
          ? section.match(/<button[^>]*data-checkbox[^>]*>/)?.[0]
          : section.match(/<input[^>]*type="checkbox"[^>]*>/)?.[0];
        const controlId = control?.match(/ id="([^"]+)"/)?.[1];
        const labelFor = section.match(/<label[^>]*data-label[^>]*>/)?.[0].match(/ for="([^"]+)"/)?.[1];
        expect(controlId).toBeTruthy();
        expect(controlId).not.toBe(explicit);
        expect(labelFor).toBe(controlId);

        const fixture = await gotoFixture(page, mode, 'checkbox-prerender');
        const container = fixture.locator(`[data-case="${name}"]`);
        const labelled = nativeButton ? container.locator('[data-checkbox]') : container.locator('input[type="checkbox"]');
        await expect(labelled).toHaveAttribute('id', explicit);
        await expect(container.locator('[data-label]')).toHaveAttribute('for', explicit);
      });
    }
  });

  test.describe('Enter', () => {
    // CHK-KEY-03..07 on native forms (design point C2 of M2 track 5, settled by the orchestrator 2026-10-23): the form module owns the path.
    interface SubmitRecord {
      submits: number;
      submitter: string | null;
      value: string | null;
      submitterClicks: number;
    }

    /** Records the submits of the case's form (prevented), their submitter and value, and the submit button's clicks. */
    async function recordSubmits(container: Locator): Promise<void> {
      await container.evaluate((node) => {
        const record = { submits: 0, submitter: null as string | null, value: null as string | null, submitterClicks: 0 };
        (node as unknown as { submitRecord: unknown }).submitRecord = record;
        const form = node.querySelector('form')!;
        form.addEventListener('submit', (event) => {
          event.preventDefault();
          record.submits += 1;
          record.submitter = (event as SubmitEvent).submitter?.textContent ?? null;
          record.value = new FormData(form).get('test-checkbox') as string | null;
        });
        node.querySelector('[data-submit]')?.addEventListener('click', () => {
          record.submitterClicks += 1;
        });
      });
    }

    async function submitRecord(container: Locator): Promise<SubmitRecord> {
      return container.evaluate((node) => (node as unknown as { submitRecord: SubmitRecord }).submitRecord);
    }

    async function pressEnterOn(page: Page, container: Locator): Promise<void> {
      await container.locator('[data-before]').focus();
      await page.keyboard.press('Tab');
      await expect(container.locator('[data-checkbox]')).toBeFocused();
      await page.keyboard.press('Enter');
    }

    test('submits the form when Enter is pressed while focused', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="submit"]');
      await recordSubmits(container);

      await pressEnterOn(page, container);

      await expect.poll(async () => (await submitRecord(container)).submits).toBe(1);
      expect(await submitRecord(container)).toEqual({ submits: 1, submitter: 'Submit', value: null, submitterClicks: 1 });
      await settle(fixture);
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-checked', 'false');
    });

    test('does not submit the form with Enter when the consumer prevents default', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="consumer"]');
      await recordSubmits(container);
      await container.locator('[data-checkbox]').evaluate((element) => element.addEventListener('keydown', (event) => event.preventDefault()));

      await pressEnterOn(page, container);
      await settle(fixture);

      expect((await submitRecord(container)).submits).toBe(0);
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-checked', 'false');
    });

    test('does not submit the form with Enter when an ancestor prevents default', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="ancestor"]');
      await recordSubmits(container);
      await container.locator('[data-ancestor]').evaluate((element) => {
        element.addEventListener('keydown', (event) => {
          (element as HTMLElement).dataset.sawPrevented = String(event.defaultPrevented);
          event.preventDefault();
        });
      });

      await pressEnterOn(page, container);
      await settle(fixture);

      // Port (dated note on CHK-KEY-03): a JavaScript ancestor sees the native default already cancelled.
      await expect(container.locator('[data-ancestor]')).toHaveAttribute('data-saw-prevented', 'true');
      expect((await submitRecord(container)).submits).toBe(0);
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-checked', 'false');
    });

    test('submits the form with Enter when readOnly', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="readonly"]');
      await recordSubmits(container);

      await pressEnterOn(page, container);

      await expect.poll(async () => (await submitRecord(container)).submits).toBe(1);
      expect((await submitRecord(container)).submitter).toBe('Submit');
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-checked', 'false');
    });

    test('submits the form once with Enter when rendered as a native button', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="native"]');
      await recordSubmits(container);

      await pressEnterOn(page, container);

      await expect.poll(async () => (await submitRecord(container)).submits).toBe(1);
      await settle(fixture);
      const record = await submitRecord(container);
      expect(record.submits).toBe(1);
      expect(record.submitter).toBe('Submit');
      expect(record.submitterClicks).toBe(1);
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-checked', 'false');
    });

    test('does not submit the form with Enter when there is no submit button', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="none"]');
      await recordSubmits(container);

      await pressEnterOn(page, container);
      await settle(fixture);

      expect((await submitRecord(container)).submits).toBe(0);
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-checked', 'false');
    });

    test('does not submit the form with Enter when the default button is disabled', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="disabled-submitter"]');
      await recordSubmits(container);

      await pressEnterOn(page, container);
      await settle(fixture);

      expect((await submitRecord(container)).submits).toBe(0);
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-checked', 'false');
    });

    test('submits to an external form with Enter when `form` is provided', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-enter');
      const container = fixture.locator('[data-case="external"]');
      await recordSubmits(container);

      await pressEnterOn(page, container);

      await expect.poll(async () => (await submitRecord(container)).submits).toBe(1);
      expect((await submitRecord(container)).submitter).toBe('Submit');
    });
  });

  test.describe('accessible name', () => {
    // CHK-ARIA-06..08, CHK-DOM-22, CHK-EDGE-07/13: the form module's native-label fallback (design point C4 of M2 track 5,
    // settled by the orchestrator 2026-10-23) after the consumer's aria-labelledby, a non-blank aria-label and the Field.Label.
    test('sets `aria-labelledby` from a sibling label associated with the hidden input', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-names');
      const container = fixture.locator('[data-case="sibling"]');
      const label = container.getByText('Label');

      await expect(label).toHaveAttribute('id', /.+/);
      await expect(container.locator('[data-checkbox]')).toHaveAttribute('aria-labelledby', (await label.getAttribute('id'))!);
      await expect(container.getByRole('checkbox')).toHaveAccessibleName('Label');
    });

    test('updates fallback `aria-labelledby` when the hidden input id changes', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-names');
      const container = fixture.locator('[data-case="id-change"]');
      const box = container.locator('[data-checkbox]');
      const labelA = container.getByText('Label A');
      await expect(labelA).toHaveAttribute('id', /.+/);
      const labelAId = (await labelA.getAttribute('id'))!;
      await expect(box).toHaveAttribute('aria-labelledby', labelAId);

      await container.locator('[data-toggle]').click();

      const labelB = container.getByText('Label B');
      await expect(labelB).toHaveAttribute('id', /.+/);
      const labelBId = (await labelB.getAttribute('id'))!;
      await expect(box).toHaveAttribute('aria-labelledby', labelBId);
      expect(labelBId).not.toBe(labelAId);
      // CHK-DOM-22: the label id assigned before stays in place.
      await expect(labelA).toHaveAttribute('id', labelAId);
    });

    test('prefers `aria-label` over an associated label', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-names');
      const container = fixture.locator('[data-case="preferred"]');
      const nativeLabelled = container.locator('[data-checkbox="native"]');
      const fieldLabelled = container.locator('[data-checkbox="field"]');

      await expect(nativeLabelled).not.toHaveAttribute('aria-labelledby');
      await expect(nativeLabelled).toHaveAccessibleName('lease.pdf');
      await expect(fieldLabelled).not.toHaveAttribute('aria-labelledby');
      await expect(fieldLabelled).toHaveAccessibleName('notes.txt');
    });

    for (const [index, ariaLabel] of ['', ' ', '\n\t '].entries()) {
      test(`ignores a blank \`aria-label\` (${JSON.stringify(ariaLabel)})`, async ({ page, mode }) => {
        const fixture = await gotoCheckboxes(page, mode, 'checkbox-names');
        const container = fixture.locator(`[data-case="blank-${index}"]`);

        await expect(container.locator('[data-checkbox="native"]')).toHaveAccessibleName('Native label');
        await expect(container.locator('[data-checkbox="field"]')).toHaveAccessibleName('Field label');
        await expect(container.locator('[data-checkbox="sibling"]')).toHaveAccessibleName('Sibling label');
      });
    }

    for (const labelType of ['native', 'Field.Label']) {
      const name = labelType === 'native' ? 'dynamic-native' : 'dynamic-field';

      test.describe(`dynamic ${labelType} label`, () => {
        test('updates the accessible name when `aria-label` is added, cleared, or removed', async ({ page, mode }) => {
          const fixture = await gotoCheckboxes(page, mode, 'checkbox-names');
          const container = fixture.locator(`[data-case="${name}"]`);
          const box = container.locator('[data-checkbox]');
          await expect(box).toHaveAccessibleName('Associated label');

          await container.locator('[data-name]').click();
          await expect(box).toHaveAccessibleName('Custom name');
          await container.locator('[data-blank]').click();
          await expect(box).toHaveAccessibleName('Associated label');
          await container.locator('[data-name]').click();
          await expect(box).toHaveAccessibleName('Custom name');
          await container.locator('[data-unname]').click();
          await expect(box).toHaveAccessibleName('Associated label');
        });

        test('updates the accessible name when the associated label mounts and unmounts', async ({ page, mode }) => {
          const fixture = await gotoCheckboxes(page, mode, 'checkbox-names');
          const container = fixture.locator(`[data-case="${name}"]`);
          const box = container.locator('[data-checkbox]');
          await container.locator('[data-hide]').click();
          await expect(box).toHaveAccessibleName('');

          await container.locator('[data-show]').click();
          await expect(box).toHaveAccessibleName('Associated label');
          await container.locator('[data-hide]').click();
          await expect(box).toHaveAccessibleName('');
          await expect(box).not.toHaveAttribute('aria-labelledby');
          await container.locator('[data-name]').click();
          await expect(box).toHaveAccessibleName('Custom name');
          await container.locator('[data-show]').click();
          await expect(box).toHaveAccessibleName('Custom name');
          await container.locator('[data-hide]').click();
          await expect(box).toHaveAccessibleName('Custom name');
          await container.locator('[data-show]').click();
          await container.locator('[data-unname]').click();
          await expect(box).toHaveAccessibleName('Associated label');
        });
      });
    }
  });

  test('can render a native button', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-basic');
    const { container, root, before } = checkboxCase(fixture, 'native');
    await expect(root).toHaveJSProperty('tagName', 'BUTTON');
    await expect(root).toHaveAttribute('aria-checked', 'false');

    await before.focus();
    await page.keyboard.press('Tab');
    await expect(root).toBeFocused();
    await page.keyboard.press('Enter');
    await settle(fixture);
    await expect(root).toHaveAttribute('aria-checked', 'false');
    await page.keyboard.press('Space');
    await expect(root).toHaveAttribute('aria-checked', 'true');
    await root.click();
    await expect(root).toHaveAttribute('aria-checked', 'false');
    await settle(fixture);
    await expect(container).toHaveAttribute('data-changes', '2');
  });
});

test.describe('<Checkbox.Indicator />', () => {
  /** One mutation of an indicator: added, removed, or one of its attributes changed. */
  interface IndicatorRecord {
    kind: 'added' | 'removed' | 'attribute';
    name: string | null;
    present: boolean;
    batch: number;
  }

  /**
   * Records, from now on, every insertion and removal of an indicator in the case and every change of its transition
   * attributes, `hidden` and `class`, with the number of the observer callback that delivered it (one callback per
   * microtask checkpoint that follows a DOM change, so two records with one batch number landed before the same paint).
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
                records.push({ kind: 'added', name: null, present: true, batch });
              }
            });
            mutation.removedNodes.forEach((removed) => {
              if (removed instanceof Element && removed.matches('[data-indicator]')) {
                records.push({ kind: 'removed', name: null, present: false, batch });
              }
            });
          } else if (mutation.target instanceof Element && mutation.target.matches('[data-indicator]')) {
            records.push({ kind: 'attribute', name: mutation.attributeName, present: mutation.target.hasAttribute(mutation.attributeName!), batch });
          }
        }
      }).observe(node, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ['data-starting-style', 'data-ending-style', 'hidden', 'class'],
      });
    });
  }

  async function indicatorRecords(container: Locator): Promise<IndicatorRecord[]> {
    return container.evaluate((node) => (node as unknown as { indicatorRecords: IndicatorRecord[] }).indicatorRecords);
  }

  test('should not render indicator by default', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });

    await expect(checkboxCase(fixture, 'transition').container.locator('[data-indicator]')).toHaveCount(0);
  });

  test('should render indicator when checked', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { indicator } = checkboxCase(fixture, 'none');

    await expect(indicator).toHaveCount(1);
    await expect(indicator).not.toHaveAttribute('data-starting-style');
  });

  test('should keep indicator mounted when unchecked', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { container, root, toggle, indicator } = checkboxCase(fixture, 'kept');

    await toggle.click();
    await expect(root).toHaveAttribute('aria-checked', 'false');
    await expect(indicator).toHaveAttribute('data-unchecked', '');
    await expect(indicator).not.toHaveAttribute('data-ending-style');

    // CHK-ANI-07: never hidden, never styled inline.
    await expect(container.locator('[data-indicator]')).toHaveCount(1);
    await expect(indicator).not.toHaveAttribute('hidden');
    await expect(indicator).not.toHaveAttribute('style');
  });

  test('should remove the indicator when there is no exit animation defined', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { container, toggle } = checkboxCase(fixture, 'none');
    await expect(container.locator('[data-indicator]')).toHaveCount(1);

    await toggle.click();

    await expect(container.locator('[data-indicator]')).toHaveCount(0);
  });

  test('should remove the indicator when the animation finishes', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { container, toggle, indicator } = checkboxCase(fixture, 'kept');
    await indicator.evaluate((element) => {
      element.addEventListener('animationend', () => {
        (element as HTMLElement).dataset.animationEnded = 'true';
      });
    });
    await recordIndicators(container);

    await toggle.click();

    await expect(indicator).toHaveAttribute('data-animation-ended', 'true');
    // Kept mounted: it carried data-ending-style for its exit, which is cleared once the exit finished.
    await expect(indicator).not.toHaveAttribute('data-ending-style');
    const records = await indicatorRecords(container);
    expect(records).toContainEqual(expect.objectContaining({ kind: 'attribute', name: 'data-ending-style', present: true }));
    expect(records.filter((record) => record.kind === 'removed')).toEqual([]);
  });

  test('triggers enter animation via data-starting-style when mounting', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { container, toggle, indicator } = checkboxCase(fixture, 'transition');
    await container.evaluate((node) => {
      node.addEventListener('transitionend', () => {
        (node as HTMLElement).dataset.transitionEnded = 'true';
      });
    });
    await recordIndicators(container);

    await toggle.click();

    await expect(container).toHaveAttribute('data-transition-ended', 'true');
    await expect(indicator).toHaveCount(1);
    await expect(indicator).not.toHaveAttribute('data-starting-style');
    // CHK-ANI-03: the element appeared with data-starting-style (rendered by C#, the handover), which was taken off later.
    const records = await indicatorRecords(container);
    const added = records.find((record) => record.kind === 'added');
    const cleared = records.find((record) => record.kind === 'attribute' && record.name === 'data-starting-style' && !record.present);
    expect(added).toBeDefined();
    expect(cleared).toBeDefined();
    expect(cleared!.batch).toBeGreaterThan(added!.batch);
  });

  test('applies data-ending-style before unmount', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { container, toggle } = checkboxCase(fixture, 'keyframes');
    await recordIndicators(container);

    await toggle.click();

    await expect(container.locator('[data-indicator]')).toHaveCount(0);
    const records = await indicatorRecords(container);
    const ending = records.findIndex((record) => record.kind === 'attribute' && record.name === 'data-ending-style' && record.present);
    const removed = records.findIndex((record) => record.kind === 'removed');
    expect(ending).toBeGreaterThanOrEqual(0);
    expect(removed).toBeGreaterThan(ending);
  });

  test('removes all indicators in a single commit when multiple checkboxes are unchecked', async ({ page, mode }) => {
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { container, toggle } = checkboxCase(fixture, 'many');
    await expect(container.locator('[data-indicator]')).toHaveCount(10);
    await recordIndicators(container);

    await toggle.click();

    await expect(container.locator('[data-indicator]')).toHaveCount(0);
    const records = await indicatorRecords(container);
    const removalBatches = new Set(records.filter((record) => record.kind === 'removed').map((record) => record.batch));
    const hiddenBatches = new Set(records.filter((record) => record.kind === 'attribute' && record.name === 'hidden' && record.present).map((record) => record.batch));
    expect(records.filter((record) => record.kind === 'removed')).toHaveLength(10);
    if (mode === 'wasm') {
      // CHK-ANI-05: every completion's removal lands before the same paint.
      expect(removalBatches.size).toBe(1);
    } else {
      // ADR 01's TRN-ANI-10 row: the removals land in the render batches after the completion messages; the convergent
      // `hidden` the module writes at exit end hides all ten before the same paint meanwhile.
      expect(hiddenBatches.size).toBe(1);
    }
  });

  test('port: a box checked again during the exit keeps its indicator and clears the ending style', async ({ page, mode }) => {
    // CHK-ANI-08: the exit is aborted, no 'starting' phase, data-ending-style cleared on the next frame (TRN-STA-09 row).
    const fixture = await gotoCheckboxes(page, mode, 'checkbox-indicator', { animations: true });
    const { container, toggle, indicator } = checkboxCase(fixture, 'kept');
    await page.evaluate(() => {
      const style = document.createElement('style');
      style.textContent = '[data-case="kept"] .animation-test-indicator[data-ending-style] { animation-duration: 10s; }';
      document.head.append(style);
    });
    await recordIndicators(container);

    await toggle.click();
    await expect(indicator).toHaveAttribute('data-ending-style', '');
    await toggle.click();

    await expect(indicator).not.toHaveAttribute('data-ending-style');
    await expect(indicator).toHaveAttribute('data-checked', '');
    const records = await indicatorRecords(container);
    expect(records.filter((record) => record.kind === 'added' || record.kind === 'removed')).toEqual([]);
    expect(records).not.toContainEqual(expect.objectContaining({ name: 'data-starting-style', present: true }));
  });
  test.describe('Form (track 2)', () => {
    // The submission is the form module's: it reads the hidden input in the submit task (FFI-FRM-04), so the field sees
    // the native valueMissing of a box nobody touched.
    test('prop: validationMode=onSubmit', async ({ page, mode }) => {
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-base-ui-form');
      const { container, root } = checkboxCase(fixture, 'on-submit');
      await expect(root).not.toHaveAttribute('aria-invalid');

      await root.click();
      await expect(root).toHaveAttribute('data-checked', '');
      await root.click();
      await expect(root).toHaveAttribute('data-unchecked', '');
      await expect(root).not.toHaveAttribute('aria-invalid');

      await container.locator('[data-submit]').click();
      await expect(root).toHaveAttribute('aria-invalid', 'true');

      await root.click();
      await expect(root).toHaveAttribute('data-checked', '');
      await expect(root).not.toHaveAttribute('aria-invalid');

      await root.click();
      await expect(root).toHaveAttribute('data-unchecked', '');
      await expect(root).toHaveAttribute('aria-invalid', 'true');

      await root.click();
      await expect(root).toHaveAttribute('data-checked', '');
      await expect(root).not.toHaveAttribute('aria-invalid');
    });

    test('port: a required box blocks the submit of its form, which focuses the root', async ({ page, mode }) => {
      // "triggers native HTML validation on submit" without its Field.Error clause (PR 1c), with the focus of CHK-FOC-06.
      const fixture = await gotoCheckboxes(page, mode, 'checkbox-base-ui-form');
      const { container, root } = checkboxCase(fixture, 'blocked');

      await container.locator('[data-submit]').click();

      await expect(root).toHaveAttribute('aria-invalid', 'true');
      await expect(root).toBeFocused();
      await expect(container).toHaveAttribute('data-submits', '0');

      await root.click();
      await expect(root).not.toHaveAttribute('aria-invalid');
      await container.locator('[data-submit]').click();
      await expect(container).toHaveAttribute('data-submits', '1');
    });
  });
});
