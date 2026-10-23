import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/radio-group.md, the Radio-owned rows its titles prove (radio.md §13), and the composite
// rows a public root proves now (CMP-FOC-01/02, CMP-KEY-10/11/16, CMP-EDGE-18), on RadioGroup over the `composite`
// module and RadioRoot over the `button` module's hidden-input bridge, the `dom` primitive's `checked` and the `form`
// module. Titles are Base UI's (radio-group/RadioGroup.test.tsx); "port:" titles assert the per-mode rows of ADR 01's
// relaxation table, the re-cited rows and the design points of M2 tracks 7-8. The fixtures record OnValueChange as data
// attributes; every page waits for `data-attached` (every twin attached) and uses the sync button as a barrier.

type FixtureName =
  | 'radio-group-basic'
  | 'radio-group-controlled'
  | 'radio-group-rtl'
  | 'radio-group-field'
  | 'radio-group-fieldset'
  | 'radio-group-form'
  | 'radio-group-edit-form'
  | 'radio-indicator'
  | 'radio-group-allocation';

/** Opens a RadioGroup fixture and waits until every group, radio and listed indicator has its behaviors attached. */
async function gotoRadios(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
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

function radioCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    group: container.locator('[role="radiogroup"]').first(),
    radio: (value: string) => container.locator(`[data-radio][data-value="${value}"]`),
    radios: container.locator('[data-radio]'),
    input: (value: string) => container.locator(`[data-radio][data-value="${value}"] + input[type="radio"]`),
    inputs: container.locator('input[type="radio"]'),
    before: container.locator('[data-before]'),
    after: container.locator('[data-after]'),
    labels: container.locator('[data-label]'),
    toggle: container.locator('[data-toggle]'),
    field: container.locator('[data-field]').first(),
    error: container.locator('[data-error]'),
    submit: container.locator('[data-submit]'),
    form: container.locator('[data-form]').first(),
    values: container.locator('[data-values]'),
  };
}

/** Clicks the hidden input directly, as a script or a label does: it is visually hidden, so it is not hit-testable. */
async function clickInput(input: Locator): Promise<void> {
  await input.evaluate((element) => (element as HTMLInputElement).click());
}

/** Clicks a label wrapping a radio on its text, at its right edge, away from the radio it wraps. */
async function clickLabelText(label: Locator): Promise<void> {
  const box = await label.boundingBox();
  await label.click({ position: { x: box!.width - 2, y: box!.height / 2 } });
}

/** Moves focus as a script does, without a pointer: no click, so no selection. */
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

/** The values a native form submits under a name, read in the page. */
async function formData(form: Locator, name: string): Promise<string[]> {
  return form.evaluate((element, key) => new FormData(element as HTMLFormElement).getAll(key).map(String), name);
}

/** Records each submit of a native form: its FormData entries for a name, as `data-submitted`, and stops the navigation. */
async function recordSubmits(form: Locator, name: string): Promise<void> {
  await form.evaluate((element, key) => {
    element.addEventListener('submit', (event) => {
      event.preventDefault();
      const entries = new FormData(element as HTMLFormElement).getAll(key).map(String);
      const dataset = (element as HTMLElement).dataset;
      dataset.submitted = JSON.stringify(entries.length === 0 ? null : entries);
    });
  }, name);
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
    const tabStop = () => label(element.querySelector('[data-radio][tabindex="0"]'));
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

test.describe('<RadioGroup />', () => {
  test.describe('prop: onValueChange', () => {
    test('should call onValueChange when an item is clicked', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio, input } = radioCase(fixture, 'single');

      await radio('a').click();

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(radio('a')).toHaveAttribute('aria-checked', 'true');
      await expect(input('a')).toHaveJSProperty('checked', true);
      // RDG-EVT-02/03: reason none, the click dispatched on the input (detail 0), no trigger.
      await expect(container).toHaveAttribute('data-last-change', 'value=a type=click detail=0 shift=false reason=none trigger=none');

      // RDG-EVT-06: activating the radio that is already checked is not a change.
      await radio('a').click();
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '1');
    });

    test('should report keyboard modifier event properties when calling onCheckedChange', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio } = radioCase(fixture, 'single');

      await radio('a').click({ modifiers: ['Shift'] });

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-last-change', 'value=a type=click detail=0 shift=true reason=none trigger=none');
    });

    test('should select an item with Space on keyup', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio } = radioCase(fixture, 'single');

      await focus(radio('a'));
      await page.keyboard.down(' ');
      await settle(fixture);
      await expect(container).toHaveAttribute('data-changes', '0');

      await page.keyboard.up(' ');

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-log', 'change:a');
      await expect(radio('a')).toHaveAttribute('aria-checked', 'true');
    });

    test('should not select an item with Enter', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio } = radioCase(fixture, 'single');

      await focus(radio('a'));
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
    });

    test('does not change state when canceled via a root click', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-controlled');
      const { container, group, radio, input } = radioCase(fixture, 'cancel');

      await radio('a').click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
      await expect(input('a')).toHaveJSProperty('checked', false);
      await expect(group).not.toHaveAttribute('data-touched');
      await expect(group).not.toHaveAttribute('data-dirty');
      await expect(group).not.toHaveAttribute('data-filled');
    });

    test('does not change state when canceled via a hidden input click', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-controlled');
      const { container, group, radio, input } = radioCase(fixture, 'cancel');

      await clickInput(input('a'));
      await settle(fixture);

      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
      await expect(input('a')).toHaveJSProperty('checked', false);
      await expect(group).not.toHaveAttribute('data-touched');
      await expect(group).not.toHaveAttribute('data-dirty');
      await expect(group).not.toHaveAttribute('data-filled');
    });

    test('does not change state when canceled via arrow key navigation', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-controlled');
      const { container, group, radio, inputs } = radioCase(fixture, 'cancel');

      await focus(radio('a'));
      await page.keyboard.press('ArrowDown');

      await expect(radio('b')).toBeFocused();
      await expect(container).toHaveAttribute('data-changes', '1');
      await settle(fixture);
      await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
      await expect(radio('b')).toHaveAttribute('aria-checked', 'false');
      await expect(inputs.nth(0)).toHaveJSProperty('checked', false);
      await expect(inputs.nth(1)).toHaveJSProperty('checked', false);
      await expect(group).not.toHaveAttribute('data-touched');
      await expect(group).not.toHaveAttribute('data-dirty');
      await expect(group).not.toHaveAttribute('data-filled');
    });

    test('port: a canceled selection never checks the input on WebAssembly and is restored a round trip later on Server', async ({ page, mode }) => {
      // ADR 01 relaxation table, the "C#-origin consumer vetoes" row of 2026-10-22 (RDG-EVT-04, RDG-STA-03): the `toggle`
      // decision reverts the input inside the dispatch on WebAssembly; on Server it stays as the click left it until the
      // restoring update.
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      const fixture = await gotoRadios(page, mode, 'radio-group-controlled');
      const { radio, input } = radioCase(fixture, 'cancel');

      const checkedAfterClick = await radio('a').evaluate((element) => {
        (element as HTMLElement).click();
        return (element.nextElementSibling as HTMLInputElement).checked;
      });

      expect(checkedAfterClick).toBe(mode === 'server');
      await expect(input('a')).toHaveJSProperty('checked', false);
      await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
    });

    // Design point R2 (c) of M2 tracks 7-8 (pending the user): the browser unchecks the selected same-name radio natively;
    // a refused click restores it on both modes (natively on WebAssembly, by the group's restore on Server).
    test('port: a refused selection restores the input of the selected radio, named or not', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-controlled');

      for (const name of ['ignored-named', 'ignored-nameless']) {
        const { container, radio, input } = radioCase(fixture, name);
        await expect(input('a')).toHaveJSProperty('checked', true);

        await radio('b').click();
        await settle(fixture);

        await expect(container).toHaveAttribute('data-changes', '1');
        await expect(radio('a')).toHaveAttribute('aria-checked', 'true');
        await expect(input('a')).toHaveJSProperty('checked', true);
        await expect(input('b')).toHaveJSProperty('checked', false);
      }
    });

    test('port: a controlled group follows its value, and the hidden inputs follow it', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-controlled');
      const { container, radio, input } = radioCase(fixture, 'external');

      await container.locator('[data-select-c]').click();
      await expect(radio('c')).toHaveAttribute('aria-checked', 'true');
      await expect(input('c')).toHaveJSProperty('checked', true);
      await expect(input('a')).toHaveJSProperty('checked', false);
      // RDG-EVT-05: no callback for a change of the parameter.
      await expect(container).toHaveAttribute('data-changes', '0');

      await radio('b').click();
      await expect(radio('b')).toHaveAttribute('aria-checked', 'true');
      await expect(input('b')).toHaveJSProperty('checked', true);
      await expect(input('c')).toHaveJSProperty('checked', false);
    });
  });

  test.describe('prop: disabled', () => {
    test('should not change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio, input } = radioCase(fixture, 'disabled');
      await expect(radio('')).toHaveAttribute('aria-checked', 'false');

      // Playwright treats aria-disabled as disabled and would wait; the pointer click is forced.
      await radio('').click({ force: true });
      await clickInput(input(''));
      await settle(fixture);

      await expect(radio('')).toHaveAttribute('aria-checked', 'false');
      await expect(input('')).toHaveJSProperty('checked', false);
      // RAD-PTR-08: the consumer's click handler is not invoked while disabled.
      await expect(container).toHaveAttribute('data-changes', '0');
      await expect(container).toHaveAttribute('data-clicks', '0');
    });
  });

  test.describe('prop: readOnly', () => {
    test('should not change its state when clicked', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio, input, labels } = radioCase(fixture, 'readonly');

      await radio('a').click();
      // RAD-PTR-07: a click reaching the input (its label) is reverted.
      await clickLabelText(labels.first());
      await clickInput(input('a'));
      await focus(radio('a'));
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
      await expect(input('a')).toHaveJSProperty('checked', false);
      await expect(container).toHaveAttribute('data-changes', '0');
    });

    test('port: arrow keys still move focus in a read-only group, and the focused radio does not select', async ({ page, mode }) => {
      // RDG-EDGE-04, RAD-EDGE-04 (the focus-driven selection clause): the module skips the click on a read-only input.
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio } = radioCase(fixture, 'readonly');

      await focus(radio('a'));
      await page.keyboard.press('ArrowDown');

      await expect(radio('b')).toBeFocused();
      await expect(radio('b')).toHaveAttribute('tabindex', '0');
      await settle(fixture);
      await expect(radio('b')).toHaveAttribute('aria-checked', 'false');
      await expect(container).toHaveAttribute('data-changes', '0');
    });
  });

  test.describe('form data', () => {
    test('should return null when no radio is selected (matching native behavior)', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, submit } = radioCase(fixture, 'native');
      await recordSubmits(form, 'group');

      await submit.click();

      await expect(form).toHaveAttribute('data-submitted', 'null');
    });

    test('should return null in form data when no radio is selected', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, inputs } = radioCase(fixture, 'native');

      expect(await formData(form, 'group')).toEqual([]);
      // RDG-STR-01: the group renders no input of its own; the radios' inputs are the form's only entries.
      await expect(form.locator('input')).toHaveCount(3);
      await expect(inputs).toHaveCount(3);
    });

    test('should include selected radio value in form data', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, radio } = radioCase(fixture, 'native');

      await radio('a').click();
      await expect(radio('a')).toHaveAttribute('aria-checked', 'true');

      expect(await formData(form, 'group')).toEqual(['a']);
    });
  });

  test('should automatically select radio upon navigation', async ({ page, mode }) => {
    const fixture = await gotoRadios(page, mode, 'radio-group-controlled');
    const { group, radio } = radioCase(fixture, 'flags');

    await focus(radio('a'));
    await expect(group).not.toHaveAttribute('data-touched');
    await expect(radio('a')).toHaveAttribute('aria-checked', 'false');

    await page.keyboard.press('ArrowDown');

    await expect(radio('b')).toBeFocused();
    await expect(radio('b')).toHaveAttribute('aria-checked', 'true');
    await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
    // RAD-FRM-05: the selection touches the field without a blur; RDG-STA-05: dirty and filled follow (Server: a round trip).
    await expect(group).toHaveAttribute('data-touched', '');
    await expect(group).toHaveAttribute('data-dirty', '');
    await expect(group).toHaveAttribute('data-filled', '');
  });

  test.describe('should manage arrow key navigation', () => {
    for (const scenario of ['metaKey', 'ctrlKey', 'altKey', 'single'] as const) {
      test(`does not select on refocus after an ignored arrow (${scenario})`, async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-basic');
        const { container, radio, after } = radioCase(fixture, scenario === 'single' ? 'ignored-single' : 'ignored');
        const modifier = { metaKey: 'Meta', ctrlKey: 'Control', altKey: 'Alt', single: '' }[scenario];

        await focus(radio('a'));
        await page.keyboard.press(modifier ? `${modifier}+ArrowDown` : 'ArrowDown');

        await expect(radio('a')).toBeFocused();
        await expect(radio('a')).toHaveAttribute('aria-checked', 'false');

        await page.keyboard.press('Tab');
        await expect(after).toBeFocused();

        await page.keyboard.press('Shift+Tab');
        await expect(radio('a')).toBeFocused();
        await settle(fixture);
        await expect(radio('a')).toHaveAttribute('aria-checked', 'false');
        await expect(container).toHaveAttribute('data-changes', '0');
      });
    }

    for (const { direction, next, previous } of [
      { direction: 'ltr', next: 'ArrowRight', previous: 'ArrowLeft' },
      { direction: 'rtl', next: 'ArrowLeft', previous: 'ArrowRight' },
    ]) {
      test.describe(`direction: ${direction}`, () => {
        test('moves focus with arrow keys and Tab', async ({ page, mode }) => {
          const fixture = await gotoRadios(page, mode, 'radio-group-rtl');
          const { radio, after } = radioCase(fixture, direction);

          await focus(radio('a'));
          await expect(radio('a')).toBeFocused();

          const steps: [string, string][] = [
            ['ArrowDown', 'b'],
            ['ArrowDown', 'c'],
            ['ArrowDown', 'a'],
            ['ArrowUp', 'c'],
            ['ArrowUp', 'b'],
            ['ArrowUp', 'a'],
            [previous, 'c'],
            [next, 'a'],
          ];
          for (const [key, expected] of steps) {
            await page.keyboard.press(key);
            await expect(radio(expected)).toBeFocused();
          }

          await page.keyboard.press('Tab');
          await expect(after).toBeFocused();

          await page.keyboard.press('Shift+Tab');
          await expect(radio('a')).toBeFocused();

          await page.keyboard.press(previous);
          await expect(radio('c')).toBeFocused();

          await page.keyboard.press('Shift+Tab');
          await page.keyboard.press('Tab');
          await expect(radio('c')).toBeFocused();
        });

        test.describe('modifier keys', () => {
          test('when Shift is pressed arrow keys move focus normally', async ({ page, mode }) => {
            const fixture = await gotoRadios(page, mode, 'radio-group-rtl');
            const { radio, before } = radioCase(fixture, direction);

            await focus(before);
            await page.keyboard.press('Tab');
            await expect(radio('a')).toBeFocused();

            await page.keyboard.press(`Shift+${next}`);
            await expect(radio('b')).toBeFocused();

            await page.keyboard.press('Shift+ArrowDown');
            await expect(radio('c')).toBeFocused();
          });
        });
      });
    }
  });

  test.describe('item removal', () => {
    test('moves the tab stop to the checked radio when the highlighted radio is removed', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { radio, toggle } = radioCase(fixture, 'removal');

      await focus(radio('b'));
      await page.keyboard.press('ArrowDown');
      await expect(radio('c')).toHaveAttribute('tabindex', '0');

      await toggle.click();
      await expect(radio('c')).toHaveCount(0);

      // RDG-FOC-03 (Server: the registry rebuild lands a round trip after the removal, the CMP-STA-10 row).
      await expect(radio('a')).toHaveAttribute('tabindex', '-1');
      await expect(radio('b')).toHaveAttribute('tabindex', '0');
    });
  });

  test('sets tabIndex=0 to the correct element initially', async ({ page, mode }) => {
    // RDG-FOC-01, RDG-EDGE-06: C# renders -1 on every radio; the first publication after attach moves the tab stop to the
    // checked radio (Server: after paint plus a round trip, the CMP-STA-10 row).
    const fixture = await gotoRadios(page, mode, 'radio-group-basic');
    const { radio } = radioCase(fixture, 'initial');

    await expect(radio('a')).not.toHaveAttribute('tabindex', '0');
    await expect(radio('b')).toHaveAttribute('tabindex', '0');
  });

  test('port: with nothing checked and the first radio disabled, the first enabled radio takes the initial tab stop', async ({ page, mode }) => {
    // RDG-FOC-01's second clause (CMP-STA-24).
    const fixture = await gotoRadios(page, mode, 'radio-group-basic');
    const { radio } = radioCase(fixture, 'first-disabled');

    await expect(radio('b')).toHaveAttribute('tabindex', '0');
    await expect(radio('a')).toHaveAttribute('tabindex', '-1');
  });

  test.describe('style hooks', () => {
    test('should apply data-checked and data-unchecked to radio root and indicator', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-indicator');
      const { container, radio } = radioCase(fixture, 'hooks');
      const indicator = (value: string) => container.locator(`[data-indicator="${value}"]`);

      for (const value of ['a', 'b']) {
        await expect(radio(value)).toHaveAttribute('data-unchecked', '');
        await expect(indicator(value)).toHaveAttribute('data-unchecked', '');
      }

      const clicks: [string, string][] = [
        ['a', 'b'],
        ['b', 'a'],
        ['a', 'b'],
      ];
      for (const [clicked, other] of clicks) {
        await radio(clicked).click();
        await expect(radio(clicked)).toHaveAttribute('data-checked', '');
        await expect(indicator(clicked)).toHaveAttribute('data-checked', '');
        await expect(radio(other)).toHaveAttribute('data-unchecked', '');
        await expect(indicator(other)).toHaveAttribute('data-unchecked', '');
        // RDG-PTR-01: the click's focus moves the tab stop.
        await expect(radio(clicked)).toHaveAttribute('tabindex', '0');
      }
    });
  });

  test.describe('with native <label>', () => {
    test('associates implicitly', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, labels } = radioCase(fixture, 'implicit');

      await clickLabelText(labels.nth(0));
      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-log', 'change:apple');

      // RAD-PTR-02: one change per click, although the label would deliver a click of its own to the input.
      await clickLabelText(labels.nth(1));
      await expect(container).toHaveAttribute('data-changes', '2');
      await expect(container).toHaveAttribute('data-log', 'change:apple change:banana');
    });

    test('associates explicitly', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, labels } = radioCase(fixture, 'explicit');

      await labels.nth(0).click();
      await expect(container).toHaveAttribute('data-changes', '1');
      await expect(container).toHaveAttribute('data-log', 'change:apple');

      await labels.nth(1).click();
      await expect(container).toHaveAttribute('data-changes', '2');
      await expect(container).toHaveAttribute('data-log', 'change:apple change:banana');
    });
  });

  test.describe('Field', () => {
    test.describe('[data-focused] without a blur event', () => {
      test('is removed when the focused group becomes disabled', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field, toggle } = radioCase(fixture, 'group-disabled');

        await focus(radio('a'));
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle.click();
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is removed when the focused group unmounts', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field, toggle } = radioCase(fixture, 'group-unmount');

        await focus(radio('a'));
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle.evaluate((element) => (element as HTMLElement).click());
        await expect(radio('a')).toHaveCount(0);
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is kept when a previously focused radio unmounts after focus moves to a sibling', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field, toggle } = radioCase(fixture, 'radio-unmount');

        await focus(radio('a'));
        await focus(radio('b'));
        await expect(radio('b')).toBeFocused();
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle.evaluate((element) => (element as HTMLElement).click());
        await expect(radio('a')).toHaveCount(0);
        await settle(fixture);

        await expect(radio('b')).toBeFocused();
        await expect(field).toHaveAttribute('data-focused', '');
      });

      test('is removed when the focused radio unmounts but its group remains', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field, toggle } = radioCase(fixture, 'radio-unmount');

        await focus(radio('a'));
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle.evaluate((element) => (element as HTMLElement).click());
        await expect(radio('a')).toHaveCount(0);
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is not acquired when focus lands on a disabled radio', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field } = radioCase(fixture, 'disabled-radio');

        await focus(radio('a'));
        await settle(fixture);

        await expect(radio('a')).toBeFocused();
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is not reacquired when a radio disabled while focused is refocused', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field, toggle } = radioCase(fixture, 'toggle-disabled');

        await focus(radio('a'));
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle.evaluate((element) => (element as HTMLElement).click());
        await expect(radio('a')).toHaveAttribute('aria-disabled', 'true');
        await expect(field).not.toHaveAttribute('data-focused');

        await radio('a').evaluate((element) => (element as HTMLElement).blur());
        await focus(radio('a'));
        await settle(fixture);

        await expect(radio('a')).toBeFocused();
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is reacquired when a radio that kept focus while disabled is re-enabled', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field, toggle } = radioCase(fixture, 'toggle-disabled');

        await focus(radio('a'));
        await toggle.evaluate((element) => (element as HTMLElement).click());
        await expect(radio('a')).toBeFocused();
        await expect(field).not.toHaveAttribute('data-focused');

        // RAD-FOC-04 (Server: a round trip after the re-enabling render, the "FFI registration" row).
        await toggle.evaluate((element) => (element as HTMLElement).click());
        await expect(field).toHaveAttribute('data-focused', '');
      });

      test('is not acquired when a radio inherits disabled from Field.Item', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field } = radioCase(fixture, 'item-disabled');

        await focus(radio('a'));
        await settle(fixture);

        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is reacquired when an arrow key moves focus from a disabled radio to an enabled sibling', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { radio, field, toggle } = radioCase(fixture, 'toggle-disabled');

        await focus(radio('a'));
        await expect(field).toHaveAttribute('data-focused', '');
        await toggle.evaluate((element) => (element as HTMLElement).click());
        await expect(field).not.toHaveAttribute('data-focused');
        await focus(radio('a'));

        await page.keyboard.press('ArrowDown');

        await expect(radio('b')).toBeFocused();
        await expect(field).toHaveAttribute('data-focused', '');
      });
    });

    test.describe('Field.Label', () => {
      test('associates implicitly', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { container, labels } = radioCase(fixture, 'labels-implicit');

        await expect(labels).toHaveCount(2);
        for (const label of await labels.all()) {
          await expect(label).toHaveAttribute('for', /.+/);
        }

        await container.getByText('Apple').click();
        await expect(container).toHaveAttribute('data-changes', '1');
        await expect(container).toHaveAttribute('data-log', 'change:apple');
      });

      test('associates explicitly', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { container } = radioCase(fixture, 'labels-explicit');

        await container.getByText('Banana', { exact: true }).click();
        await expect(container).toHaveAttribute('data-changes', '1');
        await expect(container).toHaveAttribute('data-log', 'change:banana');
      });
    });

    test.describe('prop: validationMode', () => {
      test('onBlur validates only when focus leaves the group', async ({ page, mode }) => {
        const fixture = await gotoRadios(page, mode, 'radio-group-field');
        const { container, group, radio, after } = radioCase(fixture, 'on-blur');
        const validated = container.locator('[data-validated]');

        await focus(radio('a'));
        await focus(radio('b'));
        await settle(fixture);
        await expect(validated).toHaveAttribute('data-validated', '');

        await focus(after);

        await expect(validated).toHaveAttribute('data-validated', 'a');
        await expect(group).toHaveAttribute('aria-invalid', 'true');
        await expect(group).toHaveAttribute('data-touched', '');
      });
    });
  });

  test.describe('Form', () => {
    test('submits to an external form when `form` is provided', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, submit } = radioCase(fixture, 'external');
      await recordSubmits(form, 'group');

      await submit.click();

      await expect(form).toHaveAttribute('data-submitted', JSON.stringify(['b']));
    });

    test('triggers native HTML validation on submit', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { group, radio, error, submit } = radioCase(fixture, 'required');
      await expect(error).toHaveCount(0);

      await submit.click();

      await expect(error).toHaveText('required');
      await expect(group).toHaveAttribute('aria-invalid', 'true');
      // RDG-FOC-07: the form focuses the representative radio's root.
      await expect(radio('a')).toBeFocused();
    });

    test('clears required validation when a value is selected', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { group, radio, error, submit } = radioCase(fixture, 'required');

      await submit.click();
      await expect(error).toHaveText('required');
      await expect(radio('a')).toHaveAttribute('aria-invalid', 'true');
      await expect(radio('b')).toHaveAttribute('aria-invalid', 'true');

      await radio('b').click();

      await expect(error).toHaveCount(0);
      await expect(group).not.toHaveAttribute('aria-invalid', 'true');
      await expect(radio('a')).not.toHaveAttribute('aria-invalid', 'true');
      await expect(radio('b')).not.toHaveAttribute('aria-invalid', 'true');
    });

    test('excludes a disabled selected radio from onFormSubmit to match native form data', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, radio, toggle, submit, values } = radioCase(fixture, 'disable-selected');

      await toggle.click();
      await expect(radio('a')).toHaveAttribute('aria-disabled', 'true');
      expect(await formData(form, 'test')).toEqual([]);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'test=null');
    });

    test('includes a selected radio again when it is re-enabled before form submission', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, radio, toggle, submit, values } = radioCase(fixture, 'disable-selected');

      await toggle.click();
      await expect(radio('a')).toHaveAttribute('aria-disabled', 'true');
      expect(await formData(form, 'test')).toEqual([]);

      await toggle.click();
      await expect(radio('a')).not.toHaveAttribute('aria-disabled');
      expect(await formData(form, 'test')).toEqual(['a']);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'test=a');
    });

    test('excludes an initially disabled selected radio from onFormSubmit to match native form data', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, submit, values } = radioCase(fixture, 'initially-disabled');

      expect(await formData(form, 'test')).toEqual([]);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'test=null');
    });

    test('projects an enabled selected radio, matching native form data', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, submit, values } = radioCase(fixture, 'projects');

      expect(await formData(form, 'choice')).toEqual(['a']);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'choice=a');
    });

    test('excludes a radio disabled through an ancestor <fieldset disabled> to match native form data', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, radio, submit, values } = radioCase(fixture, 'fieldset-disabled');

      // RDG-EDGE-07: the input is `:disabled` but its `disabled` property is false, so the radio is not aria-disabled.
      await expect(radio('a')).not.toHaveAttribute('aria-disabled');
      expect(await formData(form, 'choice')).toEqual([]);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'choice=null');
    });

    test('includes a selected radio after its ancestor fieldset is enabled', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, toggle, submit, values } = radioCase(fixture, 'fieldset-disabled');

      expect(await formData(form, 'choice')).toEqual([]);
      await toggle.click();
      await expect(form.locator('fieldset')).not.toHaveAttribute('disabled');
      expect(await formData(form, 'choice')).toEqual(['a']);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'choice=a');
    });

    test('omits a radio associated to another form via the `form` prop', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, submit, values } = radioCase(fixture, 'other-form');

      expect(await formData(form, 'choice')).toEqual([]);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'choice=null');
    });

    test('submits null when the selected radio in a required group is disabled, matching native validity', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, error, submit, values } = radioCase(fixture, 'required-disabled-selected');

      expect(await formData(form, 'choice')).toEqual([]);

      await submit.click();

      await expect(values).toHaveAttribute('data-values', 'choice=null');
      await expect(error).toHaveCount(0);
    });

    test('focuses the first enabled radio when all radios start disabled', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { radio, toggle, submit } = radioCase(fixture, 'all-disabled');

      await toggle.click();
      await expect(radio('a')).not.toHaveAttribute('aria-disabled');

      await submit.click();

      await expect(radio('a')).toBeFocused();
    });

    test('validates and focuses the first radio after its ancestor fieldset is enabled', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-form');
      const { form, radio, error, toggle, submit } = radioCase(fixture, 'fieldset-validate');

      await toggle.click();
      await expect(form.locator('fieldset')).not.toHaveAttribute('disabled');

      await submit.click();

      await expect(error).toBeVisible();
      await expect(error).toHaveText('required');
      await expect(radio('a')).toBeFocused();
    });
  });

  test.describe('Fieldset', () => {
    test('port: a <fieldset disabled> skips a native-button radio in arrow navigation and leaves the other radios enabled', async ({ page, mode }) => {
      // RDG-EDGE-07, re-cited from "keeps inputRef available after an ancestor fieldset is enabled" (inputRef is withdrawn,
      // ADR 02 Amendments 2026-10-22): the native button matches `:disabled`, the span radios are not aria-disabled.
      const fixture = await gotoRadios(page, mode, 'radio-group-fieldset');
      const { radio } = radioCase(fixture, 'native-disabled');

      await expect(radio('a')).not.toHaveAttribute('aria-disabled');
      await expect(radio('c')).not.toHaveAttribute('aria-disabled');
      await focus(radio('a'));
      await page.keyboard.press('ArrowDown');

      await expect(radio('c')).toBeFocused();
    });

    test('labels the radio group from the fieldset legend', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-fieldset');
      const { container, group } = radioCase(fixture, 'legend');

      const legendId = await container.locator('[data-legend]').getAttribute('id');
      expect(legendId).toBeTruthy();
      await expect(group).toHaveAttribute('aria-labelledby', legendId!);
    });
  });

  test.describe('composite', () => {
    // CMP-FOC-02 and CMP-KEY-16 through the public part (ADR 04 Amendments 2026-10-22): the composite writes the tab stop
    // inside the keydown's task and moves the focus a microtask later, so the two stay together on both modes; Server
    // latency is added to show that no round trip is involved.
    test('port: after an arrow key the focus and the tab stop move in the same task, on both modes', async ({ page, mode }) => {
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
      const fixture = await gotoRadios(page, mode, 'radio-group-rtl');
      const { group, radio } = radioCase(fixture, 'ltr');

      await focus(radio('a'));
      await expect(radio('a')).toHaveAttribute('tabindex', '0');
      await recordMoves(group);

      await page.keyboard.press('ArrowDown');
      await expect(radio('b')).toBeFocused();
      await expect(radio('b')).toHaveAttribute('tabindex', '0');

      const moves = await moveRecords(group);
      expect(moves.slice(0, 2)).toEqual([
        { kind: 'tab-stop', tabStop: 'b', focused: 'a' },
        { kind: 'focus', tabStop: 'b', focused: 'b' },
      ]);
    });

    // CMP-FOC-01: Tab lands on the radio holding the tab stop, and Tab leaves the group.
    test('port: Tab enters the group on the radio holding the tab stop and leaves it in one press', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { radio, before, after } = radioCase(fixture, 'initial');
      await expect(radio('b')).toHaveAttribute('tabindex', '0');

      await focus(before);
      await page.keyboard.press('Tab');
      await expect(radio('b')).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(after).toBeFocused();
    });

    // CMP-KEY-10, CMP-KEY-11, CMP-EDGE-18: a handled arrow is default-prevented and stopped; an arrow that changes nothing
    // (no other enabled radio) is neither.
    test('port: a handled arrow is default-prevented and stopped, and an arrow with no other enabled radio is neither', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const three = radioCase(fixture, 'three');
      const single = radioCase(fixture, 'ignored-single');
      await recordKeys(three.group);
      await recordKeys(single.group);

      await focus(three.radio('a'));
      await page.keyboard.press('ArrowDown');
      await expect(three.radio('b')).toBeFocused();

      await focus(single.radio('a'));
      await page.keyboard.press('ArrowDown');
      await settle(fixture);

      const [handled] = await three.group.evaluate((element) => (element as unknown as { keys: unknown[] }).keys);
      const [ignored] = await single.group.evaluate((element) => (element as unknown as { keys: unknown[] }).keys);
      expect(handled).toMatchObject({ key: 'ArrowDown', defaultPrevented: true, reachedDocument: false });
      expect(ignored).toMatchObject({ key: 'ArrowDown', defaultPrevented: false, reachedDocument: true });
    });

    // ADR 01's row "… EVD-EVT-26 against composite/button key handling …" with option (i) (ADR 04 Amendments 2026-10-22,
    // chosen by the user 2026-10-23): a handled arrow runs the radio's C# onkeydown, then the group's, once each; inside
    // the `navigate` decision on WebAssembly, from the handled-key report on Server.
    test("port: a handled arrow runs the radio's and the group's C# onkeydown once each", async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio } = radioCase(fixture, 'keydown');

      await focus(radio('a'));
      await page.keyboard.press('ArrowDown');
      await expect(radio('b')).toBeFocused();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-log', /^keydown:ArrowDown keydown:group:ArrowDown change:b$/);
    });

    // RAD-FOC-05, ADR 04 Amendments 2026-10-20 (the decision point `select`, fallback run): a C# consumer onfocus that
    // prevents the part handler vetoes the focus-driven selection on WebAssembly; on Server it arrives after the
    // selection (the "C#-origin consumer vetoes" row).
    test('port: a C# onfocus veto stops the focus-driven selection on WebAssembly and is too late on Server', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-basic');
      const { container, radio } = radioCase(fixture, 'focus-veto');

      await focus(radio('a'));
      await page.keyboard.press('ArrowDown');
      await expect(radio('b')).toBeFocused();
      await settle(fixture);

      await expect(radio('b')).toHaveAttribute('aria-checked', mode === 'wasm' ? 'false' : 'true');
      await expect(container).toHaveAttribute('data-changes', mode === 'wasm' ? '0' : '1');
    });
  });

  test.describe('EditForm', () => {
    test('@verifies ADR-05/5 participates in a cascaded EditContext', async ({ page, mode }) => {
      const fixture = await gotoRadios(page, mode, 'radio-group-edit-form');
      const group = fixture.locator('[data-testid="group"]');
      const root = fixture.locator('[data-testid="root"]');
      const apple = fixture.locator('[data-radio][data-value="apple"]');

      await expect(apple.locator('xpath=following-sibling::input[1]')).toHaveAttribute('name', 'Fruit');

      await fixture.getByRole('button', { name: 'Submit' }).click();
      await expect(fixture).toHaveAttribute('data-invalid-submits', '1');
      await expect(fixture.locator('[data-testid="message"]')).toHaveText('Pick a fruit.');
      await expect(fixture.locator('[data-testid="error"]')).toHaveText('Pick a fruit.');
      await expect(root).toHaveAttribute('data-invalid', '');
      await expect(group).toHaveAttribute('aria-invalid', 'true');

      await apple.click();
      await expect(fixture.locator('[data-testid="message"]')).toHaveCount(0);
      await expect(root).not.toHaveAttribute('data-invalid');

      await fixture.getByRole('button', { name: 'Submit' }).click();
      await expect(fixture).toHaveAttribute('data-valid-submits', '1');
    });
  });

  test.describe('allocation', () => {
    // ADR 08 Amendments 2026-10-22 ("does not box … the primitives"), ADR 16 rule 7: the WebAssembly half of the radio's
    // allocation probe (tests/Blazix.Primitives.Tests/Radio/RadioAllocationProbeTests.cs is the CoreCLR half, 0 bytes for
    // every type). A round is what a radio and its group compare on the render path: two StrictEquals, one null test and
    // two ValueChangedObserver.Observe.
    test('port: the value comparisons of a radio allocate nothing for a string, an enum and a record class on the interpreter', async ({ page, mode }) => {
      test.skip(mode !== 'wasm', 'the probe is about the WebAssembly interpreter; Server runs CoreCLR');
      const probe = await gotoFixture(page, mode, 'radio-group-allocation');

      await probe.locator('[data-measure]').click();

      await expect(probe).toHaveAttribute('data-iterations', /^[1-9]\d*$/);
      test.info().annotations.push({ type: 'bytes per round', description: (await probe.getAttribute('data-scenarios')) ?? '' });
      for (const scenario of ['string', 'enum', 'record-class']) {
        await expect(probe.locator(`[data-scenario="${scenario}"]`)).toHaveText('0');
      }
    });

    // Design point R8 of M2 tracks 7-8 (pending the user): EqualityComparer<T>.Default boxes on the interpreter (ADR
    // 05's third verification item, measured 2026-10-20; Amendments 2026-10-20 accept the cost for the generic Is<T>).
    // Measured here 2026-10-23: 192 bytes per round for int and int?, 416 for a record struct. The title pins the
    // profile, so a change of the platform or of Store/Equality.cs turns it red.
    test('port: the value comparisons of a radio box an int, a nullable int and a record struct on the interpreter', async ({ page, mode }) => {
      test.skip(mode !== 'wasm', 'the probe is about the WebAssembly interpreter; Server runs CoreCLR');
      const probe = await gotoFixture(page, mode, 'radio-group-allocation');

      await probe.locator('[data-measure]').click();

      await expect(probe).toHaveAttribute('data-iterations', /^[1-9]\d*$/);
      for (const scenario of ['int', 'nullable-int', 'record-struct']) {
        await expect(probe.locator(`[data-scenario="${scenario}"]`)).not.toHaveText('0');
      }
    });
  });
});
