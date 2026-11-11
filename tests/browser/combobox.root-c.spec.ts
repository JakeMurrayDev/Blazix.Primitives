import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { center, fire, gotoCombobox, moveTo, recorded, type FixtureOptions } from './combobox.helpers';
import { pressControl, settle } from './suites/popupFixture';

// ComboboxRoot.test.tsx, lines 10000 on, ported at L4 (M5 track 10, issue 174): Base UI's titles verbatim, on the `combobox-root-c`
// fixture (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxRootCL4Fixture.razor), every title on both render modes.
// The fixture's `case` picks the test's rendering; its callbacks are recorded as the container's `data-*` attributes.
//
// Not ported (render `Autocomplete.*`, track 11's): "does not request form submission when no form owns the autocomplete",
// "does not request form submission when requestSubmit is unavailable", "validates the autocomplete input when focus leaves its popup".

const FIXTURE = 'combobox-root-c';

function gotoRoot(page: Page, mode: RenderMode, options: FixtureOptions = {}): Promise<Locator> {
  return gotoCombobox(page, mode, FIXTURE, options);
}

function input(page: Page): Locator {
  return page.getByTestId('input');
}

/** Base UI's `user.clear`: select all, then delete. */
async function clearInput(field: Locator): Promise<void> {
  await field.press('ControlOrMeta+a');
  await field.press('Backspace');
}

/** Presses an item with the real pointer once its popup is positioned. */
async function pressOption(option: Locator): Promise<void> {
  const point = await center(option);
  await option.page().mouse.click(point.x, point.y);
}

/** Base UI's `expect(input).toHaveAttribute('aria-activedescendant', option.id)`: an item's id is read on each poll, as the item may render again. */
async function expectActive(field: Locator, option: Locator): Promise<void> {
  await expect
    .poll(async () => {
      const active = await field.getAttribute('aria-activedescendant');
      return active !== null && active === (await option.getAttribute('id'));
    })
    .toBe(true);
}

interface Highlight {
  value: string;
  reason: string;
  index: number;
}

/** The fixture's recorded `onItemHighlighted` calls, in order (`null` is Base UI's `undefined`). */
async function highlights(fixture: Locator): Promise<Highlight[]> {
  return (await recorded(fixture, 'data-highlights')).map((entry) => {
    const [value, reason, index] = entry.split(':') as [string, string, string];
    return { value, reason, index: Number(index) };
  });
}

/** Base UI's `expect(onItemHighlighted.mock.lastCall)`, polled: the last call, or `undefined` before the first. */
function lastHighlight(fixture: Locator): Promise<Highlight | undefined> {
  return highlights(fixture).then((list) => list.at(-1));
}

test.describe('<Combobox.Root />', () => {
  test.describe('prop: autoHighlight', () => {
    test('clears active highlight when removing the highlighted chip item', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'chips' });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await page.keyboard.press('ArrowDown');

      await expect(field).toHaveAttribute('aria-activedescendant', /.+/);

      const highlightedId = (await field.getAttribute('aria-activedescendant'))!;
      const highlightedLabel = await page.locator(`[id="${highlightedId}"]`).textContent();
      expect(highlightedLabel).not.toBeNull();

      await page.getByRole('button', { name: `Remove ${highlightedLabel as string}`, includeHidden: true }).click();

      await expect(field).not.toHaveAttribute('aria-activedescendant');
    });

    test('keeps highlight in sync after selecting then backspacing to a single match', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'alpha|beta|gamma|delta|epsilon', autoHighlight: true });

      const field = page.getByRole('combobox');
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();

      // Select index 4
      await pressOption(page.getByRole('option', { name: 'epsilon' }));
      await expect(page.getByRole('listbox')).toHaveCount(0);

      // Reopen and press Backspace to narrow to a single match
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();

      // Backspace once: from 'epsilon' -> 'epsilo', which should still only match 'epsilon'
      await page.keyboard.press('Backspace');
      const epsilon = page.getByRole('option', { name: 'epsilon' });
      await expect(epsilon).toBeVisible();
      // With autoHighlight, the first (and only) item should be highlighted
      await expectActive(field, epsilon);
    });

    test('navigates on first ArrowDown after editing selection to a new matching query', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'Apple|Grape|Grapefruit', autoHighlight: true });

      const field = input(page);

      // Open and select Apple
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await pressOption(page.getByRole('option', { name: 'Apple', exact: true }));

      // Edit input to "Ape" (matches Grape and Grapefruit)
      await field.click();
      await clearInput(field);
      await field.pressSequentially('Ape');
      await expect(page.getByRole('listbox')).toBeVisible();

      const grape = page.getByRole('option', { name: 'Grape', exact: true });
      const grapefruit = page.getByRole('option', { name: 'Grapefruit', exact: true });

      // With autoHighlight, first match is highlighted immediately
      await expectActive(field, grape);

      // One ArrowDown should move to the next match (no double keypress needed)
      await page.keyboard.press('ArrowDown');
      await expectActive(field, grapefruit);
    });

    test('fires a single clearing highlight on Enter selection', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'Apple|Apricot|Banana', autoHighlight: true });

      const field = page.getByRole('combobox');

      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await field.pressSequentially('app');
      await expect(field).toHaveValue('app');
      await expect(field).toHaveAttribute('aria-activedescendant', /.+/);
      await settle(fixture);

      // Reset history to focus on close events only.
      const before = (await highlights(fixture)).length;
      await page.keyboard.press('Enter');
      await expect(page.getByRole('listbox')).toHaveCount(0);
      await settle(fixture);

      const calls = (await highlights(fixture)).slice(before);
      const clearingCalls = calls.filter((call) => call.value === 'null');
      expect(clearingCalls).toHaveLength(1);
      const postClearCalls = calls.slice(calls.indexOf(clearingCalls[0]!) + 1);
      expect(postClearCalls.every((call) => call.value === 'null')).toBe(true);
    });
  });

  test.describe('prop: onItemHighlighted', () => {
    test('fires on keyboard navigation', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b|c' });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await page.keyboard.press('ArrowDown');

      await expect.poll(async () => (await highlights(fixture)).length).toBeGreaterThan(0);
      const last = (await lastHighlight(fixture))!;
      expect(last.value).toBe('a');
      expect(last.reason).toBe('keyboard');
      expect(last.index).toBe(0);
      // The event is a KeyboardEvent whose key is ArrowDown.
      expect((await recorded(fixture, 'data-highlight-events')).at(-1)).toBe('keydown:KeyboardEvent:ArrowDown:-');
    });

    test('passes native mouse and pointer events for pointer highlights', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b|c' });

      await input(page).click();
      const option = page.getByRole('option', { name: 'b', exact: true });
      await expect(option).toBeVisible();
      // Deviation (2026-11-10): Base UI dispatches a bare `mousemove` MouseEvent on the option; the real pointer's move onto it
      // produces the same `mousemove` MouseEvent (with `pointermove` beside it), and the leave is the real `pointerout`.
      await moveTo(page, option);

      await expect.poll(() => lastHighlight(fixture)).toMatchObject({ value: 'b', reason: 'pointer' });
      // A MouseEvent that is not a PointerEvent.
      expect((await recorded(fixture, 'data-highlight-events')).at(-1)).toBe('mousemove:MouseEvent:-:mouse');

      await page.mouse.move(0, 0);
      await expect.poll(() => lastHighlight(fixture)).toMatchObject({ value: 'null', reason: 'pointer' });
      // The leave event is a PointerEvent (CMB-PTR-06); jsdom's `pointerout` instance cannot be compared with a real pointer's.
      expect((await recorded(fixture, 'data-highlight-events')).at(-1)).toMatch(/^pointer(out|leave):PointerEvent:-:mouse$/);
    });

    async function highlightFirstWithKeyboard(page: Page, fixture: Locator): Promise<void> {
      await input(page).click();
      // Base UI awaits the popup it presses the key into (`findByRole`); an arrow pressed inside the open's round trip can be lost on Server.
      await expect(page.getByRole('listbox')).toBeVisible();
      await page.keyboard.press('ArrowDown');
      await expect.poll(() => lastHighlight(fixture)).toMatchObject({ value: 'apple' });
    }

    test('reports a pointer highlight after keyboard navigation', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'apple|apricot|banana', clear: true });
      await highlightFirstWithKeyboard(page, fixture);

      await moveTo(page, page.getByRole('option', { name: 'banana' }));
      await expect.poll(() => lastHighlight(fixture)).toMatchObject({ value: 'banana', reason: 'pointer' });
    });

    test('reports no keyboard reason when typing clears the highlight', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'apple|apricot|banana', clear: true });
      await highlightFirstWithKeyboard(page, fixture);

      await page.keyboard.press('a');
      await expect.poll(() => lastHighlight(fixture)).toMatchObject({ value: 'null', reason: 'none' });
    });

    test('reports no keyboard reason when the clear button clears the highlight', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'apple|apricot|banana', clear: true });
      await highlightFirstWithKeyboard(page, fixture);

      // Deviation (2026-11-10): the kept-mounted Clear is hidden while the field is empty, so a real click cannot reach it; Base UI's
      // `fireEvent.click` dispatches the click on the hidden button.
      await fire(page.getByTestId('clear'), 'click');
      await expect.poll(() => lastHighlight(fixture)).toMatchObject({ value: 'null', reason: 'none' });
      expect((await recorded(fixture, 'data-highlight-events')).at(-1)).toMatch(/^click:/);
    });
  });

  test.describe('prop: open', () => {
    test('keeps filtered content stable when a controlled close is deferred', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'deferred-close', animations: 1 });

      await input(page).pressSequentially('ap');
      await pressOption(page.getByRole('option', { name: 'Apple' }));

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await expect(input(page)).toHaveValue('Apple');
      await expect(page.getByRole('option', { name: 'Apple' })).toHaveCount(1);
      await expect(page.getByRole('option', { name: 'Apricot' })).toHaveCount(1);
    });
  });

  test.describe('dialog pattern', () => {
    test('clears the highlight when an inline list in an unbound dialog unmounts', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'dialog-inline' });

      const trigger = page.getByRole('button', { name: 'Choose fruit' });
      await trigger.click();
      const field = page.getByRole('combobox');
      await expect(field).toBeFocused();
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'Banana' })).toHaveAttribute('data-highlighted', '');

      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toHaveCount(0);

      await trigger.click();
      await expect(field).toBeFocused();
      await expect(field).not.toHaveAttribute('aria-activedescendant');
      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'Apple' })).toHaveAttribute('data-highlighted', '');
    });

    test('does not let pending dialog initial focus steal focus from an async-enabled combobox', async ({ page, mode }) => {
      // Keep the dialog's initial-focus rAF pending until after the combobox click.
      // The GitHub repro depends on this timing; otherwise the rAF can fire before the click.
      await page.addInitScript(() => {
        const holder = window as unknown as {
          heldFrames: Map<number, FrameRequestCallback>;
          releaseFrames: () => void;
        };
        const callbacks = new Map<number, FrameRequestCallback>();
        let frameId = 0;
        holder.heldFrames = callbacks;
        window.requestAnimationFrame = (callback) => {
          frameId += 1;
          callbacks.set(frameId, callback);
          return frameId;
        };
        window.cancelAnimationFrame = (id) => {
          callbacks.delete(id);
        };
        holder.releaseFrames = () => {
          const pending = Array.from(callbacks.values());
          callbacks.clear();
          pending.forEach((callback) => callback(performance.now()));
        };
      });
      await gotoRoot(page, mode, { case: 'dialog-async' });

      await expect(page.getByLabel('Fruit', { exact: true })).toBeEnabled();
      await page.getByLabel('Fruit', { exact: true }).click();

      await expect(page.getByRole('listbox')).toBeVisible();

      // The dialog's initial-focus frame is still pending (a frame held before the click).
      expect(await page.evaluate(() => (window as unknown as { heldFrames: Map<number, unknown> }).heldFrames.size)).toBeGreaterThan(0);
      await page.evaluate(() => (window as unknown as { releaseFrames: () => void }).releaseFrames());

      await expect(page.getByLabel('Fruit', { exact: true })).toBeFocused();
      await expect(page.getByRole('listbox')).toBeVisible();
    });

    test.describe('single', () => {
      test('closes the dialog after selecting an item and updates the trigger value', async ({ page, mode }) => {
        await gotoRoot(page, mode, { case: 'dialog-single' });

        const trigger = page.getByTestId('dialog-trigger');
        await trigger.click();

        await expect(page.getByRole('dialog', { name: 'Fruit chooser' })).toBeVisible();
        const field = page.getByTestId('dialog-input');
        await expect(field).toBeVisible();

        await field.click();
        await field.pressSequentially('ap');
        await page.getByRole('option', { name: 'Apple', exact: true }).click();

        await expect(page.getByRole('dialog', { name: 'Fruit chooser' })).toHaveCount(0);

        await expect(trigger).toHaveText('Apple');
      });
    });
  });

  test.describe('Form', () => {
    /** Base UI's `submitSpy`: the submit listener records the form's data and prevents the navigation. */
    async function recordSubmits(page: Page, name: string): Promise<void> {
      await page.evaluate((field) => {
        const form = document.getElementById('external-form') as HTMLFormElement;
        const holder = window as unknown as { submitted: (string | null)[][] };
        holder.submitted = [];
        form.addEventListener('submit', (event) => {
          event.preventDefault();
          holder.submitted.push(new FormData(form).getAll(field) as string[]);
        });
      }, name);
    }

    function submitted(page: Page): Promise<(string | null)[][]> {
      return page.evaluate(() => (window as unknown as { submitted: (string | null)[][] }).submitted);
    }

    test('submits to an external form when form is provided', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'form-external' });
      await recordSubmits(page, 'country');

      await page.getByRole('button', { name: 'Submit' }).click();

      expect(await submitted(page)).toEqual([['US']]);
    });

    test('submits multiple values to an external form when form is provided', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'form-external', multiple: true });
      await recordSubmits(page, 'countries');

      await page.getByRole('button', { name: 'Submit' }).click();

      expect(await submitted(page)).toEqual([['US', 'AU']]);
    });

    test('triggers native HTML validation on submit', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'form-required' });

      const submit = page.getByText('Submit', { exact: true });

      await expect(page.getByTestId('error')).toHaveCount(0);

      await submit.click();

      await expect(page.getByTestId('error')).toHaveText('required');
    });

    test('focuses trigger and surfaces errors when input is inside popup', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'form-popup-input' });

      await expect(page.getByTestId('error')).toHaveCount(0);

      await page.getByText('Submit', { exact: true }).click();

      const trigger = page.getByTestId('trigger');

      await expect(trigger).toBeFocused();
      await expect(trigger).toHaveAttribute('data-invalid', '');

      await expect(page.getByTestId('error')).toHaveText('required');
      await settle(fixture);
      expect(await recorded(fixture, 'data-submits')).toEqual([]);

      await trigger.click();

      await expect(input(page)).toBeVisible();
      await expect(input(page)).not.toHaveAttribute('data-invalid');
    });

    test('submits on Enter when no item is highlighted (does not prevent)', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'form-enter', items: 'apple|banana' });

      const field = page.getByRole('combobox');
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();
      // No navigation, so nothing highlighted
      await page.keyboard.press('Enter');

      await settle(fixture);
      expect(await recorded(fixture, 'data-submits')).toHaveLength(1);
    });

    test('prevents submit on Enter when an item is highlighted', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'form-enter', items: 'alpha|beta' });

      const field = page.getByRole('combobox');
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await page.keyboard.press('ArrowDown');
      await expect(field).toHaveAttribute('aria-activedescendant', /.+/);
      await page.keyboard.press('Enter');

      await expect(page.getByRole('listbox')).toHaveCount(0);
      await settle(fixture);
      expect(await recorded(fixture, 'data-submits')).toEqual([]);
    });
  });

  test.describe('Field', () => {
    test('[data-focused]', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'field-focus' });

      const field = page.getByTestId('input');
      const trigger = page.getByTestId('trigger');

      await expect(field).not.toHaveAttribute('data-focused');
      await expect(trigger).not.toHaveAttribute('data-focused');

      // Deviation (2026-11-10): Base UI's `fireEvent.focus` dispatches a focus event on the input; the real `focus()` moves focus there.
      await field.focus();

      await expect(field).toHaveAttribute('data-focused', '');
      await expect(trigger).toHaveAttribute('data-focused', '');

      await field.evaluate((element) => element.blur());

      await expect(field).not.toHaveAttribute('data-focused');
      await expect(trigger).not.toHaveAttribute('data-focused');
    });

    test.describe('[data-focused] without a blur event', () => {
      test('is removed when the focused input becomes disabled', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'field-inputs' });

        const field = page.getByTestId('first');
        await field.focus();

        await expect(page.getByTestId('field')).toHaveAttribute('data-focused', '');

        await pressControl(fixture.locator('[data-toggle-disabled]'));
        await expect(field).toBeDisabled();
        await settle(fixture);

        await expect(page.getByTestId('field')).not.toHaveAttribute('data-focused');
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is removed when the focused input unmounts', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'field-inputs' });

        await page.getByTestId('first').focus();

        await expect(page.getByTestId('field')).toHaveAttribute('data-focused', '');

        await pressControl(fixture.locator('[data-toggle-unmount]'));
        await expect(page.getByTestId('first')).toHaveCount(0);
        await settle(fixture);

        await expect(page.getByTestId('field')).not.toHaveAttribute('data-focused');
      });

      test('is removed when the focused trigger becomes disabled', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'field-triggers' });

        const trigger = page.getByTestId('trigger');
        await trigger.focus();

        await expect(page.getByTestId('field')).toHaveAttribute('data-focused', '');

        await pressControl(fixture.locator('[data-toggle-disabled]'));
        await expect(trigger).toBeDisabled();
        await settle(fixture);

        await expect(page.getByTestId('field')).not.toHaveAttribute('data-focused');
        await expect(trigger).not.toHaveAttribute('data-focused');
      });

      test('is removed when the focused trigger unmounts', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'field-triggers' });

        await page.getByTestId('trigger').focus();

        await expect(page.getByTestId('field')).toHaveAttribute('data-focused', '');

        await pressControl(fixture.locator('[data-toggle-unmount]'));
        await expect(page.getByTestId('trigger')).toHaveCount(0);
        await settle(fixture);

        await expect(page.getByTestId('field')).not.toHaveAttribute('data-focused');
      });
    });

    test('does not mark as touched when focus moves into the popup', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'field-popup' });

      const trigger = page.getByTestId('trigger');

      // Deviation (2026-11-10): Base UI fires `focus`, `click`, then a `blur` with the popup as `relatedTarget` and a `focus` on the
      // popup; the real click focuses the trigger and the open moves focus to the popup, which is the same sequence.
      await trigger.click();

      const popup = page.getByRole('dialog');
      await expect(popup).toBeFocused();
      await settle(fixture);

      expect(await recorded(fixture, 'data-validate-calls')).toHaveLength(0);
      await expect(trigger).toHaveAttribute('data-focused', '');
      await expect(trigger).not.toHaveAttribute('data-touched');
      await expect(trigger).not.toHaveAttribute('aria-invalid');
    });

    test('validates when the popup is blurred', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'field-popup' });

      const trigger = page.getByTestId('trigger');
      const outside = page.getByTestId('outside');

      await trigger.click();

      const popup = page.getByRole('dialog');
      await expect(popup).toBeFocused();

      // Deviation (2026-11-11): Base UI fires a bare `blur` with the outside button as `relatedTarget`, which closes the popup only under
      // jsdom; in a browser the portalled popup's focus manager closes on focus-out only after a pointerdown outside
      // (`FloatingFocusManager.tsx:571-595`), so the outside button is pressed for real (pointerdown, then the click that focuses it).
      await outside.click();

      await expect.poll(async () => (await recorded(fixture, 'data-validate-calls')).length).toBe(1);

      await expect(trigger).toHaveAttribute('data-touched', '');
      await expect(trigger).not.toHaveAttribute('data-focused');
      await expect(trigger).toHaveAttribute('aria-invalid', 'true');
    });
  });

  test.describe('prop: highlightItemOnHover', () => {
    test('highlights an item on mouse move by default', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry' });

      const field = page.getByRole('combobox');
      await field.click();

      const banana = page.getByRole('option', { name: 'banana' });
      // Deviation (2026-11-10): Base UI's `fireEvent.mouseMove(banana, { pointerType: 'mouse' })` is the real pointer's move onto the item.
      await moveTo(page, banana);

      await expect(banana).toHaveAttribute('data-highlighted', '');
      expect(await field.getAttribute('aria-activedescendant')).toBe(await banana.getAttribute('id'));
    });

    test('does not highlight items from mouse movement when disabled', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'apple|banana|cherry', highlightItemOnHover: false });

      const field = page.getByRole('combobox');
      await field.click();

      const banana = page.getByRole('option', { name: 'banana' });
      await moveTo(page, banana);
      await settle(fixture);

      await expect(field).not.toHaveAttribute('aria-activedescendant');
      await expect(banana).not.toHaveAttribute('data-highlighted');
    });
  });

  test.describe('prop: loopFocus', () => {
    /** Focuses the input, presses the first arrow (which opens), and waits for the options and the highlight Base UI awaits. */
    async function openWith(page: Page, key: 'ArrowUp' | 'ArrowDown', index: number): Promise<{ field: Locator; options: Locator }> {
      const field = page.getByRole('combobox');
      await field.focus();

      // ArrowUp opens and focuses last item; ArrowDown opens and focuses first item
      await page.keyboard.press(key);

      await expect(page.getByRole('listbox')).toBeVisible();

      const options = page.getByRole('option');
      await expectActive(field, options.nth(index));
      return { field, options };
    }

    test('loops focus from last to first item with ArrowDown by default', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry' });
      const { field, options } = await openWith(page, 'ArrowUp', 2);

      // Loop cycles through input
      await page.keyboard.press('ArrowDown');

      await expect(field).not.toHaveAttribute('aria-activedescendant');

      // Then to first item
      await page.keyboard.press('ArrowDown');

      await expectActive(field, options.nth(0));
    });

    test('loops focus from first to last item with ArrowUp by default', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry' });
      const { field, options } = await openWith(page, 'ArrowDown', 0);

      // Loop cycles through input
      await page.keyboard.press('ArrowUp');

      await expect(field).not.toHaveAttribute('aria-activedescendant');

      // Then to last item
      await page.keyboard.press('ArrowUp');

      await expectActive(field, options.nth(2));
    });

    test('does not loop focus from last to first with ArrowDown when loopFocus={false}', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'apple|banana|cherry', loopFocus: false });
      const { field, options } = await openWith(page, 'ArrowUp', 2);

      // Should stay at last item (no loop)
      await page.keyboard.press('ArrowDown');
      await settle(fixture);

      await expectActive(field, options.nth(2));
    });

    test('does not loop focus from first to last with ArrowUp when loopFocus={false}', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'apple|banana|cherry', loopFocus: false });
      const { field, options } = await openWith(page, 'ArrowDown', 0);

      // Should stay at first item (no loop)
      await page.keyboard.press('ArrowUp');
      await settle(fixture);

      await expectActive(field, options.nth(0));
    });
  });

  test.describe('coverage edge cases', () => {
    /** Base UI's `getHiddenControl`: the aria-hidden text input the root renders (the form-value `hidden` inputs are not textboxes). */
    function hiddenControl(page: Page): Locator {
      return page.locator('input[aria-hidden="true"]:not([type="hidden"])');
    }

    test('moves focus from the hidden control to an external input', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'hidden-input' });
      const visibleInput = page.getByTestId('visible-input');

      // Deviation (2026-11-10): `fireEvent.focus(hiddenInput)` is the real `focus()` of the hidden control.
      await hiddenControl(page).focus();

      await expect(visibleInput).toBeFocused();
    });

    test('falls back to the trigger when the hidden control is focused', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'hidden-trigger' });

      await hiddenControl(page).focus();

      await expect(page.getByTestId('trigger')).toBeFocused();
    });

    test('falls back to the trigger after an external input unmounts', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'hidden-swap' });

      await pressControl(fixture.locator('[data-swap]'));
      await expect(page.getByTestId('trigger')).toBeVisible();
      await settle(fixture);

      await hiddenControl(page).focus();

      await expect(page.getByTestId('trigger')).toBeFocused();
    });

    test('moves hidden-control focus to the trigger when the input is inside the popup', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'hidden-popup-input' });
      await expect(page.getByRole('combobox', { includeHidden: true }).first()).toBeAttached();

      await hiddenControl(page).focus();

      await expect(page.getByTestId('trigger')).toBeFocused();
    });

    test('safely handles hidden-control focus without a visible control', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'hidden-none' });

      // Deviation (2026-11-10): Base UI's `fireEvent.focus` dispatches the event without moving focus, so `document.body` keeps it; a
      // real `focus()` would leave the hidden control focused, which no behavior of the root changes (CMB-FOC-05: nothing happens).
      await fire(hiddenControl(page), 'focus', { bubbles: false });

      await expect.poll(() => page.evaluate(() => document.activeElement === document.body)).toBe(true);
    });
  });

  test.describe('within Composite', () => {
    test('should navigate between combobox and composite items', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'composite' });

      const field = page.getByRole('combobox');
      await field.click();

      await page.keyboard.press('ArrowLeft');
      const button2 = page.getByRole('button', { name: 'Item 2' });
      await expect(button2).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(field).toBeFocused();
    });
  });
});
