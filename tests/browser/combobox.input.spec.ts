import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { center, gotoCombobox } from './combobox.helpers';
import { settle } from './suites/popupFixture';

// Base UI's ComboboxInput.test.tsx, ComboboxInput.android.test.tsx, ComboboxInput.gecko.test.tsx, ComboboxInputGroup.test.tsx
// and utils/handleInputPress.test.ts at L4 (M5 track 10, issue 174), titles verbatim, on the `combobox-input` fixture
// (case = the rendering of the Base UI title). Callbacks are read from the container's recorded attributes.

async function goto(page: Page, mode: RenderMode, name: string): Promise<Locator> {
  return gotoCombobox(page, mode, 'combobox-input', { case: name });
}

function input(page: Page): Locator {
  return page.getByTestId('input');
}

function listbox(page: Page): Locator {
  return page.getByRole('listbox');
}

/**
 * Waits for the chips container's handle to hold all its chips' ids (the chips register one by one, so the last chip is only known once all are) (js/combobox.ts `chipIdsKey`, set when the handle attaches):
 * `data-attached` covers the root's session and the input, not the Chips part, and the input's key toward the chips reads
 * those ids.
 */
async function chipsReady(page: Page, count: number): Promise<void> {
  const chips = input(page).locator('xpath=..');
  await expect
    .poll(() =>
      chips.evaluate((element) => {
        const key = Object.getOwnPropertySymbols(element).find((symbol) => symbol.description === 'blazix.combobox.chipIds');
        const ids = key ? ((element as unknown as Record<symbol, readonly string[]>)[key] ?? []) : [];
        return ids.length;
      }),
    )
    .toBe(count);
}

/** The input's caret, polled: the key's handler may run after the key event reached the page. */
function caret(field: Locator): Promise<number | null> {
  return field.evaluate((element: HTMLInputElement) => element.selectionStart);
}

/** Counts the keydowns that bubble to the fixture's wrapper (Base UI's `onKeyDown` on the wrapping div). */
async function countOuterKeyDowns(page: Page): Promise<void> {
  await page.getByTestId('outer').evaluate((element) => {
    const holder = window as unknown as { outerKeyDowns: number };
    holder.outerKeyDowns = 0;
    element.addEventListener('keydown', () => {
      holder.outerKeyDowns += 1;
    });
  });
}

function outerKeyDowns(page: Page): Promise<number> {
  return page.evaluate(() => (window as unknown as { outerKeyDowns: number }).outerKeyDowns);
}

/**
 * Base UI's `fireEvent.compositionStart(input)` then `fireEvent.change(input, { target: { value: '' } })`.
 * Deviation (2026-11-10): a real IME cannot be driven to an empty composing update (CDP's imeSetComposition ends a
 * composition on empty text), so the composition events are dispatched at the real input.
 */
async function composeEmpty(field: Locator): Promise<void> {
  await field.evaluate((element: HTMLInputElement) => {
    element.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(element, '');
    element.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertCompositionText', data: '' }));
  });
}

/** The single entry a recorded list holds (one callback call), by pattern. */
async function expectSingleChange(fixture: Locator, attribute: string, entry: RegExp): Promise<void> {
  await expect(fixture).toHaveAttribute(attribute, entry);
}

test.describe('<Combobox.Input />', () => {
  test.describe('prop: readOnly', () => {
    test('should not change the input value when typing while readOnly', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'readonly-type');

      await input(page).click();
      await input(page).pressSequentially('a');
      await settle(fixture);

      await expect(input(page)).toHaveValue('');
      expect((await fixture.getAttribute('data-input-changes')) ?? '').toBe('');
    });

    test('allows interactions when readOnly={false}', async ({ page, mode }) => {
      await goto(page, mode, 'readonly-false');

      await input(page).click();
      await expect(listbox(page)).toBeVisible();
      await expect(page.getByRole('option', { name: 'a' })).toBeVisible();
      await page.getByRole('option', { name: 'a' }).click();

      await expect(input(page)).toHaveValue('a');
    });
  });

  test.describe('onOpenChange reason', () => {
    test('fires with reason input-press when Input is clicked', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'open-reason');

      await input(page).click();

      await expect(fixture).toHaveAttribute('data-open-changes', /(^| )true:input-press$/);
    });
  });

  test.describe('interaction behavior', () => {
    test('clears selected value when input text is cleared (single selection)', async ({ page, mode }) => {
      await goto(page, mode, 'clear-single');
      await expect(input(page)).toHaveValue('apple');

      await input(page).click();
      await input(page).press('ControlOrMeta+a');
      await input(page).press('Backspace');
      await expect(input(page)).toHaveValue('');

      await input(page).pressSequentially('a');
      await expect(listbox(page)).toBeVisible();

      const options = page.getByRole('option');
      await expect(options).not.toHaveCount(0);
      for (const option of await options.all()) {
        await expect(option).not.toHaveAttribute('aria-selected', 'true');
      }
    });

    test('should open popup on typing when enabled', async ({ page, mode }) => {
      await goto(page, mode, 'type-open');

      await input(page).click();
      await input(page).pressSequentially('a');

      await expect(listbox(page)).toBeVisible();
    });

    test('should move focus to clear button when pressing Escape and popup is closed', async ({ page, mode }) => {
      await goto(page, mode, 'escape-clear');

      await input(page).focus();
      await input(page).press('Escape');
      await expect(input(page)).toHaveValue('');

      await input(page).click();
      await input(page).pressSequentially('a');
      await expect(listbox(page)).toBeVisible();
      await page.getByRole('option', { name: 'apple' }).click();
      await expect(input(page)).toHaveValue('apple');

      await input(page).click();
      await input(page).press('End');
      await input(page).pressSequentially('a');
      // "applea" matches no item, so the list is empty and has no box: the open popup is read from the input.
      await expect(input(page)).toHaveAttribute('aria-expanded', 'true');
      await expect(listbox(page)).toBeAttached();

      await input(page).press('Escape');
      await expect(input(page)).toHaveValue('apple');
      await expect(listbox(page)).toBeHidden();

      await input(page).press('Escape');
      await expect(input(page)).toHaveValue('');
    });

    test('pressing Home moves caret to start', async ({ page, mode }) => {
      await goto(page, mode, 'plain');

      await input(page).focus();
      await input(page).pressSequentially('banana');
      await expect(input(page)).toHaveValue('banana');

      await input(page).press('Home');

      await expect.poll(() => caret(input(page))).toBe(0);
      expect(await input(page).evaluate((element: HTMLInputElement) => element.selectionEnd)).toBe(0);
    });

    test('pressing End moves caret to end', async ({ page, mode }) => {
      await goto(page, mode, 'plain');

      await input(page).focus();
      await input(page).pressSequentially('apple');
      await expect(input(page)).toHaveValue('apple');

      await input(page).press('End');

      await expect.poll(() => caret(input(page))).toBe(5);
      expect(await input(page).evaluate((element: HTMLInputElement) => element.selectionEnd)).toBe(5);
    });

    test('scrolls to the start and end when pressing Home/End on overflowing input', async ({ page, mode }) => {
      await goto(page, mode, 'plain-overflow');
      const field = input(page);
      await field.focus();

      await field.pressSequentially('this is a very long combobox value');

      const sizes = await field.evaluate((element: HTMLInputElement) => ({ scrollWidth: element.scrollWidth, clientWidth: element.clientWidth }));
      expect(sizes.scrollWidth).toBeGreaterThan(sizes.clientWidth);
      const expectedScroll = sizes.scrollWidth - sizes.clientWidth;
      expect(expectedScroll).toBeGreaterThan(0);

      await field.evaluate((element: HTMLInputElement, scroll) => {
        element.scrollLeft = scroll;
        element.setSelectionRange(element.value.length, element.value.length);
      }, expectedScroll);

      await field.press('Home');
      await expect.poll(() => caret(field)).toBe(0);
      expect(await field.evaluate((element: HTMLInputElement) => element.selectionEnd)).toBe(0);
      await expect.poll(() => field.evaluate((element: HTMLInputElement) => element.scrollLeft)).toBe(0);

      await field.press('End');
      const length = 'this is a very long combobox value'.length;
      await expect.poll(() => caret(field)).toBe(length);
      expect(await field.evaluate((element: HTMLInputElement) => element.selectionEnd)).toBe(length);
      await expect
        .poll(async () => Math.abs((await field.evaluate((element: HTMLInputElement) => element.scrollLeft)) - expectedScroll))
        .toBeLessThanOrEqual(2);
    });

    test('clears the chip highlight when focus returns to the input', async ({ page, mode }) => {
      await goto(page, mode, 'chips-focus');
      await chipsReady(page, 3);

      await input(page).focus();
      await expect(input(page)).toBeFocused();
      await input(page).press('ArrowLeft');
      await expect(page.getByTestId('chip-cherry')).toBeFocused();
      await page.keyboard.press('ArrowLeft');
      await expect(page.getByTestId('chip-banana')).toBeFocused();

      await input(page).focus();
      await expect(input(page)).toBeFocused();
      await input(page).press('ArrowLeft');
      await expect(page.getByTestId('chip-cherry')).toBeFocused();
    });

    test('removes the last chip after clicking from a chip back to the input', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'chips-click-back');
      await chipsReady(page, 3);

      await input(page).click();
      await expect(input(page)).toBeFocused();
      await input(page).press('ArrowLeft');
      await expect(page.getByTestId('chip-cherry')).toBeFocused();

      await input(page).click();
      await expect(input(page)).toBeFocused();

      await page.keyboard.press('Backspace');

      await expectSingleChange(fixture, 'data-value-changes', /^apple,banana:\S+$/);
    });

    test('removes the last chip after leaving a chip and tabbing back to the input', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'chips-tab-back');
      const outside = page.getByTestId('outside');
      await chipsReady(page, 3);

      await outside.click();
      await page.keyboard.press('Tab');
      await expect(input(page)).toBeFocused();
      await page.keyboard.press('ArrowLeft');
      await expect(page.getByTestId('chip-cherry')).toBeFocused();

      await outside.click();
      await expect(outside).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(input(page)).toBeFocused();

      await page.keyboard.press('Backspace');

      await expectSingleChange(fixture, 'data-value-changes', /^apple,banana:\S+$/);
    });

    test('keeps focus on the input when navigating toward chips but none are rendered', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'no-chips');

      await input(page).focus();
      await input(page).evaluate((element: HTMLInputElement) => element.setSelectionRange(0, 0));
      await input(page).press('ArrowLeft');
      await settle(fixture);

      await expect(input(page)).toBeFocused();
    });

    test('treats a null selectionStart as the beginning of a custom input', async ({ page, mode }) => {
      await goto(page, mode, 'number');
      await chipsReady(page, 1);

      expect(await input(page).evaluate((element: HTMLInputElement) => element.selectionStart)).toBe(null);
      await input(page).focus();
      await input(page).press('ArrowLeft');

      await expect(page.getByTestId('chip')).toBeFocused();
    });

    test('keeps the popup open after modified keyboard navigation', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'default-open');
      await expect(listbox(page)).toBeVisible();

      await input(page).focus();
      await input(page).press('Control+ArrowDown');
      await settle(fixture);

      await expect(listbox(page)).toBeVisible();
    });

    test('does not select an item for an IME keydown without a highlight', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'default-open');
      await expect(listbox(page)).toBeVisible();
      await input(page).focus();

      // Deviation (2026-11-10): Playwright's keyboard cannot set keyCode 229, so the key is dispatched through CDP as a raw
      // keydown with `windowsVirtualKeyCode: 229`, which the page receives as a trusted event (checked by `seen`).
      await input(page).evaluate((element) => {
        const holder = window as unknown as { imeKeyCodes: number[] };
        holder.imeKeyCodes = [];
        element.addEventListener('keydown', (event) => holder.imeKeyCodes.push((event as KeyboardEvent).keyCode));
      });
      const session = await page.context().newCDPSession(page);
      await session.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 229 });
      await session.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 229 });
      await session.detach();
      await expect.poll(() => page.evaluate(() => (window as unknown as { imeKeyCodes: number[] }).imeKeyCodes)).toContain(229);
      await settle(fixture);

      expect((await fixture.getAttribute('data-value-changes')) ?? '').toBe('');
    });

    test('clears a closed multiple value on Escape and stops propagation', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'escape-multiple');
      await countOuterKeyDowns(page);

      await input(page).focus();
      await input(page).press('Escape');
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-value-changes', /^:escape-key$/);
      expect(await outerKeyDowns(page)).toBe(0);
    });

    test('lets Escape propagate when a closed multiple value is already empty', async ({ page, mode }) => {
      await goto(page, mode, 'escape-multiple-empty');
      await countOuterKeyDowns(page);

      await input(page).focus();
      await input(page).press('Escape');

      expect(await outerKeyDowns(page)).toBeGreaterThan(0);
    });

    test('lets Escape propagate from a closed inline combobox', async ({ page, mode }) => {
      await goto(page, mode, 'escape-inline');
      await countOuterKeyDowns(page);

      await input(page).focus();
      await input(page).press('Escape');

      expect(await outerKeyDowns(page)).toBeGreaterThan(0);
    });

    test('closes on an empty composition update when input clicks do not open the popup', async ({ page, mode }) => {
      await goto(page, mode, 'composition-closed');
      await expect(listbox(page)).toBeVisible();

      await composeEmpty(input(page));

      await expect(listbox(page)).toBeHidden();
    });

    test('clears the highlight on an empty composition update while staying open', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'composition-open');
      await expect(listbox(page)).toBeVisible();

      await input(page).focus();
      await input(page).press('ArrowDown');
      await expect(input(page)).toHaveAttribute('aria-activedescendant', /.+/);

      await composeEmpty(input(page));
      await settle(fixture);

      await expect(listbox(page)).toBeVisible();
      await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    });

    test('removes the last rendered chip when pressing Backspace in an empty input', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'value-slice');
      await chipsReady(page, 2);

      await input(page).click();
      await expect(input(page)).toBeFocused();
      await page.keyboard.press('Backspace');

      await expectSingleChange(fixture, 'data-value-changes', /^apple,cherry:\S+$/);
    });

    test('removes the last chip after tabbing from a chip back to the input', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'value-closed');
      await chipsReady(page, 3);

      await input(page).click();
      await expect(input(page)).toBeFocused();
      await input(page).press('ArrowLeft');
      await expect(page.getByTestId('cherry')).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(input(page)).toBeFocused();

      await page.keyboard.press('Backspace');

      await expectSingleChange(fixture, 'data-value-changes', /^apple,banana:\S+$/);
    });

    test('focuses the last chip with ArrowLeft after tabbing from a chip back to the input', async ({ page, mode }) => {
      await goto(page, mode, 'value-closed');
      await chipsReady(page, 3);
      const lastChip = page.getByTestId('cherry');

      await input(page).click();
      await expect(input(page)).toBeFocused();
      await input(page).press('ArrowLeft');
      await expect(lastChip).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(input(page)).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(lastChip).toBeFocused();
    });

    test('removes the last selected value when no chips are rendered', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'value-count');

      await input(page).click();
      await expect(input(page)).toBeFocused();
      await page.keyboard.press('Backspace');

      await expectSingleChange(fixture, 'data-value-changes', /^apple:\S+$/);
    });

    test('closes the popup when tabbing out', async ({ page, mode }) => {
      await goto(page, mode, 'tab');
      const button = page.getByTestId('button');

      await input(page).click();
      await expect(listbox(page)).toBeVisible();
      await center(listbox(page)); // positioned: the popup's first positioning pass has landed

      await page.keyboard.press('Tab');

      await expect(button).toBeFocused();
      await expect(listbox(page)).toBeHidden();

      await input(page).click();
      await expect(listbox(page)).toBeVisible();
      await center(listbox(page)); // positioned: the popup's first positioning pass has landed

      await page.keyboard.press('Tab');

      await expect(button).toBeFocused();
      await expect(listbox(page)).toBeHidden();
    });

    test('preserves caret position when controlled and inserting in the middle', async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'controlled-input');
      const field = input(page);

      await field.click();
      await field.pressSequentially('abcd');
      await expect(field).toHaveValue('abcd');

      // Move caret left twice to position after "ab"
      await page.keyboard.press('ArrowLeft');
      await page.keyboard.press('ArrowLeft');
      await expect.poll(() => caret(field)).toBe(2);

      // On Server the controlled text's round trips land after the keys: each check waits until C# has seen the last change.
      await page.keyboard.type('xxx');
      await expect(fixture).toHaveAttribute('data-input-changes', /(^| )abxxxcd:input-change$/);
      await settle(fixture);
      await expect(field).toHaveValue('abxxxcd');
      await expect.poll(() => caret(field)).toBe(5);
      expect(await field.evaluate((element: HTMLInputElement) => element.selectionEnd)).toBe(5);

      await page.keyboard.type('y');
      await expect(fixture).toHaveAttribute('data-input-changes', /(^| )abxxxycd:input-change$/);
      await settle(fixture);
      await expect(field).toHaveValue('abxxxycd');
      await expect.poll(() => caret(field)).toBe(6);
      expect(await field.evaluate((element: HTMLInputElement) => element.selectionEnd)).toBe(6);
    });
  });
});

test.describe('<Combobox.Input /> on Android', () => {
  test('propagates changes during Android composition', async ({ page, mode }) => {
    // Deviation (2026-11-10): the module reads the platform once, when it loads (js/platform.ts: `userAgent` containing
    // "android"), so an init script sets the user agent and platform before any script, where Base UI mocks
    // `@base-ui/utils/platform`.
    await page.addInitScript(() => {
      Object.defineProperty(navigator, 'userAgent', { get: () => 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36' });
      Object.defineProperty(navigator, 'platform', { get: () => 'Linux armv8l' });
    });
    const fixture = await goto(page, mode, 'android');
    await input(page).focus();

    // A real IME composition through CDP: compositionstart, then the composing input event.
    const session = await page.context().newCDPSession(page);
    await session.send('Input.imeSetComposition', { text: 'a', selectionStart: 1, selectionEnd: 1 });
    await session.detach();

    await expect(fixture).toHaveAttribute('data-input-changes', /(^| )a:input-change( |$)/);
  });
});

test.describe('<Combobox.Input /> in Gecko RTL', () => {
  test('uses Gecko RTL caret positions for Home and End', async ({ page, mode }) => {
    // Deviation (2026-11-10): the module reads the engine once, when it loads (js/platform.ts: Gecko is a "firefox" user agent
    // where `-webkit-backdrop-filter` is unsupported), so an init script sets the user agent and the CSS support check before
    // any script, where Base UI mocks `@base-ui/utils/platform`.
    await page.addInitScript(() => {
      Object.defineProperty(navigator, 'userAgent', { get: () => 'Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0' });
      Object.defineProperty(navigator, 'platform', { get: () => 'Linux x86_64' });
      const supports = CSS.supports.bind(CSS);
      CSS.supports = ((...args: unknown[]) => (String(args[0]).includes('-webkit-backdrop-filter') ? false : (supports as (...a: unknown[]) => boolean)(...args))) as typeof CSS.supports;
    });
    await goto(page, mode, 'gecko');
    await expect(input(page)).toHaveValue('apple');
    await input(page).focus();
    const length = 'apple'.length;

    await input(page).press('Home');
    await expect.poll(() => caret(input(page))).toBe(length);

    await input(page).press('End');
    await expect.poll(() => caret(input(page))).toBe(0);
  });
});

test.describe('<Combobox.InputGroup />', () => {
  test('should not dismiss the popup when clicking inside the input group', async ({ page, mode }) => {
    const fixture = await goto(page, mode, 'group');

    await page.getByRole('button', { name: 'Open' }).click();
    await expect(listbox(page)).toBeVisible();
      await center(listbox(page)); // positioned: the popup's first positioning pass has landed

    await page.getByTestId('pad').click();
    await settle(fixture);

    await expect(listbox(page)).toBeVisible();
      await center(listbox(page)); // positioned: the popup's first positioning pass has landed
  });

  test('focuses the input and opens when clicking input-group padding around chips', async ({ page, mode }) => {
    await goto(page, mode, 'group-chips');

    await page.getByTestId('group').click({ position: { x: 3, y: 3 } });

    await expect(input(page)).toBeFocused();
    await expect(listbox(page)).toBeVisible();
      await center(listbox(page)); // positioned: the popup's first positioning pass has landed
  });

  test('does not handle chip presses a second time when chips are nested inside the input group', async ({ page, mode }) => {
    const fixture = await goto(page, mode, 'group-chips');

    // Base UI fires only the mousedown on the chip.
    const box = (await page.getByTestId('chip').boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    try {
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-open-changes', /^true:\S+$/);
    } finally {
      await page.mouse.up();
    }
  });

  test('focuses the input without opening when openOnInputClick is false', async ({ page, mode }) => {
    const fixture = await goto(page, mode, 'group-no-open');

    await page.getByTestId('group').click({ position: { x: 3, y: 3 } });
    await settle(fixture);

    await expect(input(page)).toBeFocused();
    await expect(listbox(page)).toHaveCount(0);
  });
});

test.describe('handleInputPress', () => {
  test('handles an event whose target is not an Element', async ({ page, mode }) => {
    const fixture = await goto(page, mode, 'group-press');
    const errors: Error[] = [];
    page.on('pageerror', (error) => errors.push(error));

    // Deviation (2026-11-10): the unit test calls the helper with a stub event; here the same press, a mousedown whose target
    // is a text node ("padding" inside the group), is dispatched at the real part, since a pointer can only land on an Element.
    const result = await page.evaluate(() => {
      const text = document.querySelector('[data-testid="pad"]')!.firstChild!;
      const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
      const notPrevented = text.dispatchEvent(event);
      return { nodeType: text.nodeType, notPrevented };
    });
    await settle(fixture);

    expect(result.nodeType).toBe(3);
    expect(result.notPrevented).toBe(false);
    await expect(input(page)).toBeFocused();
    await expect(listbox(page)).toHaveCount(0);
    expect(errors).toEqual([]);
  });
});
