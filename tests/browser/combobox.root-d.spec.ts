import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import {
  center,
  clearScrollCalls,
  fire,
  gotoCombobox,
  moveTo,
  NEAREST,
  parkPointer,
  recorded,
  scrollCalls,
  spyScrollIntoView,
  waitFrames,
  type FixtureOptions,
} from './combobox.helpers';
import { pressControl, settle } from './suites/popupFixture';

// The Base UI titles of the rows the 2026-11-11 docs pass re-tagged [L4] (CMB-STA-21, STA-25, STA-26, STA-27, STA-28, EVT-04), ported
// at L4 (M5 track 10, issue 174): ComboboxRoot.test.tsx's `skipIf(isJSDOM)` titles and one ComboboxInput.test.tsx title, verbatim, on
// the `combobox-root-d` fixture (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxRootDL4Fixture.razor), every title on
// both render modes. The fixture's `case` picks the test's rendering; its callbacks are recorded as the container's `data-*` attributes.
//
// Deviations (2026-11-11):
// - Base UI's `setProps` is a fixture control pressed with a Shift keydown (`pressControl`), which moves neither focus nor the pointer;
//   `flushMicrotasks` and an awaited `setProps` are `settle(fixture)`, a round trip that also waits for the render.
// - Negative checks (`not.toHaveAttribute`) run after `settle(fixture)`, so a late highlight cannot pass them early.
// - Base UI's `globalThis.BASE_UI_ANIMATIONS_DISABLED = false` is the host's `animations=1` query.
// - "reopening during the close animation" runs Base UI's exit animation for 10 s, not 400 ms: the reopen must land while the popup
//   is still ending, and on Server under a delayed circuit a 400 ms animation can finish first. No title waits for it to end.

const FIXTURE = 'combobox-root-d';

function gotoRoot(page: Page, mode: RenderMode, options: FixtureOptions = {}): Promise<Locator> {
  return gotoCombobox(page, mode, FIXTURE, options);
}

function input(page: Page): Locator {
  return page.getByTestId('input');
}

function option(page: Page, name: string): Locator {
  return page.getByRole('option', { name, exact: true });
}

/** Base UI's `user.type(element, text)`: a click on the element, then the keys. */
async function typeInto(field: Locator, text: string): Promise<void> {
  await field.click();
  await field.page().keyboard.type(text);
}

/** Base UI's `user.clear`: select all, then delete. */
async function clearInput(field: Locator): Promise<void> {
  await field.press('ControlOrMeta+a');
  await field.press('Backspace');
}

/** Presses an item with the real pointer once its popup is positioned (`unanchored`: a popup with no anchor is never positioned). */
async function pressOption(target: Locator, options: { unanchored?: boolean } = {}): Promise<void> {
  const point = await center(target, options);
  await target.page().mouse.click(point.x, point.y);
}

async function idOf(locator: Locator): Promise<string> {
  const id = await locator.getAttribute('id');
  expect(id).not.toBeNull();
  return id!;
}

test.describe('<Combobox.Root />', () => {
  test.describe('selection behavior', () => {
    test.describe('single', () => {
      test('highlights and scrolls the selected item into view on mount when inline (items prop)', async ({ page, mode }) => {
        await spyScrollIntoView(page);
        await gotoRoot(page, mode, { items: 'apple|banana|cherry', defaultValue: 'banana' });

        const field = input(page);
        const selectedItem = option(page, 'banana');

        await expect(selectedItem).toHaveAttribute('data-highlighted', '');
        await expect(field).toHaveAttribute('aria-activedescendant', await idOf(selectedItem));
        await expect.poll(async () => (await scrollCalls(page)).map((call) => call.options)).toContainEqual(NEAREST);
      });

      test('does not restore the initial highlight after focus leaves an inline input (items prop)', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'inline-controlled' });
        const field = input(page);
        const banana = option(page, 'banana');

        await expect(banana).toHaveAttribute('data-highlighted', '');

        await field.click();
        await page.getByRole('button', { name: 'Change value' }).click();

        await expect(field).not.toHaveAttribute('aria-activedescendant');
        await settle(fixture);
        await expect(field).not.toHaveAttribute('aria-activedescendant');
        await expect(option(page, 'cherry')).not.toHaveAttribute('data-highlighted');
      });

      test('does not apply the initial highlight when a selection resolves after blur (items prop)', async ({ page, mode }) => {
        await spyScrollIntoView(page);
        const fixture = await gotoRoot(page, mode, { case: 'inline-resolve' });
        const field = input(page);

        await field.click();
        await page.getByRole('button', { name: 'Blur target' }).click();
        await page.getByRole('button', { name: 'Resolve selection' }).click();
        await settle(fixture);
        await waitFrames(page);

        const selectedItem = option(page, 'banana');
        await expect(selectedItem).not.toHaveAttribute('data-highlighted');
        await expect(field).not.toHaveAttribute('aria-activedescendant');
        expect(await scrollCalls(page)).toEqual([]);
      });

      test('does not snap the highlight to a stale index while filtering an inline list (items prop)', async ({ page, mode }) => {
        // The selected item survives filtering but shifts index: `banana` is at unfiltered index 1, but after typing `b` the visible
        // list is `[banana, blueberry]` so index 1 is `blueberry`.
        const fixture = await gotoRoot(page, mode, { items: 'apple|banana|blueberry|cherry', defaultValue: 'banana' });

        const field = input(page);
        const banana = option(page, 'banana');
        await expect(banana).toHaveAttribute('data-highlighted', '');

        await field.click();
        await page.keyboard.type('b');

        await expect(option(page, 'cherry')).toHaveCount(0);
        await settle(fixture);

        // Typing without `autoHighlight` clears the highlight; it must not be re-derived from a stale unfiltered index onto a
        // different item.
        await expect(field).not.toHaveAttribute('aria-activedescendant');
        await expect(option(page, 'blueberry')).not.toHaveAttribute('data-highlighted');
      });

      test('waits for a controlled query clear to commit before restoring the selection (items prop)', async ({ page, mode }) => {
        // Deviation (2026-11-11): Base UI's ClearActiveIndexButton calls `store.context.setIndices({ activeIndex: null })`; the
        // fixture's button calls the store's ClearHighlightAsync, which clears the highlight the same way and also forgets the selected
        // index (no step of this title reads it).
        const fixture = await gotoRoot(page, mode, { case: 'query-clear' });
        const field = input(page);
        const banana = option(page, 'banana');
        await expect(banana).toBeVisible();

        await page.getByRole('button', { name: 'Clear highlight' }).click();
        await settle(fixture);
        await expect(field).not.toHaveAttribute('aria-activedescendant');
        await expect(banana).not.toHaveAttribute('data-highlighted');

        await clearInput(field);
        await settle(fixture);
        await expect(field).toHaveValue('b');
        await expect(banana).not.toHaveAttribute('data-highlighted');

        await page.getByRole('button', { name: 'Rerender' }).click();
        await settle(fixture);
        await expect(field).not.toHaveAttribute('aria-activedescendant');
        await expect(banana).not.toHaveAttribute('data-highlighted');
      });

      test('highlights the first selected item on mount and keeps the highlight when toggling in a multiple inline list (items prop)', async ({
        page,
        mode,
      }) => {
        const fixture = await gotoRoot(page, mode, {
          items: 'apple|banana|cherry|date|elderberry',
          multiple: true,
          defaultValue: 'apple|date',
        });

        const field = input(page);
        const date = option(page, 'date');
        const apple = option(page, 'apple');

        // Initial highlight follows the first selected value in rendered order.
        await expect(apple).toHaveAttribute('data-highlighted', '');
        const appleId = await idOf(apple);
        await expect(field).toHaveAttribute('aria-activedescendant', appleId);

        // Deselecting must not jump the highlight (and scroll) to the other selected item.
        await pressOption(date);

        await expect(date).toHaveAttribute('aria-selected', 'false');
        await settle(fixture);
        await expect(apple).not.toHaveAttribute('data-highlighted');
        await expect(field).not.toHaveAttribute('aria-activedescendant', appleId);
      });

      test('does not highlight a non-selected item when an initial input value pre-filters the inline list (items prop)', async ({
        page,
        mode,
      }) => {
        // The selected value is filtered out by the initial input value, but its unfiltered index stays in range of the filtered list.
        const fixture = await gotoRoot(page, mode, { items: 'b0|sel|b1|b2|b3', defaultValue: 'sel', defaultInputValue: 'b' });

        const field = input(page);
        const b1 = option(page, 'b1');
        await expect(b1).toBeAttached();
        await expect(option(page, 'sel')).toHaveCount(0);
        await settle(fixture);
        await waitFrames(page);

        await expect(b1).not.toHaveAttribute('data-highlighted');
        await expect(field).not.toHaveAttribute('aria-activedescendant');
      });

      test('does not replace a pointer highlight when the initial selection resolves after mount (items prop)', async ({ page, mode }) => {
        await spyScrollIntoView(page);
        const fixture = await gotoRoot(page, mode, { case: 'inline-late' });

        const field = input(page);
        const apple = option(page, 'apple');
        await moveTo(page, apple);

        await expect(apple).toHaveAttribute('data-highlighted', '');

        await pressControl(page.getByTestId('set-items'));
        await expect(option(page, 'banana')).toBeAttached();
        await settle(fixture);
        await waitFrames(page);

        await expect(apple).toHaveAttribute('data-highlighted', '');
        await expect(option(page, 'banana')).not.toHaveAttribute('data-highlighted');
        await expect(field).toHaveAttribute('aria-activedescendant', await idOf(apple));
        expect(await scrollCalls(page)).toEqual([]);
      });

      test('does not restore the initial highlight after pointer navigation is cleared (items prop)', async ({ page, mode }) => {
        await spyScrollIntoView(page);
        const fixture = await gotoRoot(page, mode, { case: 'inline-late' });

        const field = input(page);
        const apple = option(page, 'apple');
        await moveTo(page, apple);

        await expect(apple).toHaveAttribute('data-highlighted', '');

        await parkPointer(page);
        await expect(apple).not.toHaveAttribute('data-highlighted');

        await clearScrollCalls(page);
        await pressControl(page.getByTestId('set-items'));
        await expect(option(page, 'banana')).toBeAttached();
        await settle(fixture);
        await waitFrames(page);

        await expect(option(page, 'banana')).not.toHaveAttribute('data-highlighted');
        await expect(field).not.toHaveAttribute('aria-activedescendant');
        expect(await scrollCalls(page)).toEqual([]);
      });

      test('restores the selected direct item after its registration index changes', async ({ page, mode }) => {
        await spyScrollIntoView(page, { callThrough: false });
        const fixture = await gotoRoot(page, mode, { case: 'direct-reorder' });
        const field = input(page);

        await typeInto(field, 'c');
        const cherry = option(page, 'cherry');
        await expect(cherry).toBeVisible();
        await expect(option(page, 'apple')).toHaveCount(0);
        await fire(cherry, 'mousemove', { pointerType: 'mouse' });
        await expect(cherry).toHaveAttribute('data-highlighted', '');
        await waitFrames(page);

        await clearScrollCalls(page);
        const before = (await recorded(fixture, 'data-highlights')).length;
        await clearInput(field);

        const banana = option(page, 'banana');
        await expect(banana).toHaveAttribute('data-highlighted', '');
        const bananaId = await idOf(banana);
        await expect(field).toHaveAttribute('aria-activedescendant', bananaId);
        // Interim registrations may scroll transiently before paint; the final scroll must land on the restored selected item.
        await expect.poll(async () => (await scrollCalls(page)).at(-1)?.id).toBe(bananaId);
        expect((await scrollCalls(page)).at(-1)?.options).toEqual(NEAREST);
        await settle(fixture);
        const highlights = (await recorded(fixture, 'data-highlights')).slice(before).map((entry) => entry.split(':'));
        expect(highlights.at(-1)?.[0]).toBe('banana');

        // The restore must never report an item at an index it does not occupy.
        const positions: Record<string, number> = { apple: 0, banana: 1, cherry: 2 };
        const inconsistent = highlights
          .filter(([item]) => item !== 'null')
          .filter(([item, , index]) => String(positions[item]) !== index)
          .map(([item, , index]) => `${item}@${index}`);
        expect(inconsistent).toEqual([]);
      });
    });
  });

  test.describe('reopening during the close animation', () => {
    test('shows every item when a controlled reopen interrupts a canceled mirrored-selection clear', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'reopen-mirror', animations: 1 });

      await page.getByTestId('open').click();
      const field = input(page);
      await expect(field).toBeVisible();
      await typeInto(field, 'ap');
      // The popup has no anchor (its input is inside it and there is no trigger), so it is never positioned.
      await pressOption(option(page, 'apple'), { unanchored: true });

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await page.getByTestId('open').click();
      await expect(popup).not.toHaveAttribute('data-ending-style');

      await expect(field).toHaveValue('apple');
      await expect(page.getByRole('option')).toHaveCount(3);
    });

    test('keeps the typed filter when items change afterwards (input outside popup)', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'refetch', animations: 1 });

      const field = input(page);
      await typeInto(field, 'app');
      // Deviation (2026-11-11): on Server the popup that typing opens renders a round trip later; Base UI's Escape lands on the open,
      // filtered popup, so the title waits for that list first.
      await expect(page.getByRole('option')).toHaveCount(1);
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await page.getByTestId('trigger').click();
      await settle(fixture);

      await expect(field).toHaveValue('app');

      // A new `items` identity must not resync the input to the selected label while the user's filter is still in it.
      await page.getByTestId('refetch').click();
      await settle(fixture);

      await expect(field).toHaveValue('app');
    });

    test('keeps filtering when typing reopens the popup (input outside popup)', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'typing-reopen', animations: 1 });

      const field = input(page);
      await expect(field).toHaveValue('apple');

      await field.click();
      await page.keyboard.press('Backspace');
      await expect(page.getByRole('option')).toHaveCount(1);

      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      // Typing reopens the popup, so the keystroke's own "query changed" flag must survive the reopen even when it restores the
      // selected label exactly.
      await page.keyboard.type('e');
      await settle(fixture);

      await expect(field).toHaveValue('apple');
      await expect(page.getByRole('option')).toHaveCount(1);
    });

    test('does not re-emit a clear when closing again without typing (multiple, input outside popup)', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'multiple-outside', animations: 1 });

      const field = input(page);
      await typeInto(field, 'app');
      // Deviation (2026-11-11): on Server the popup that typing opens renders a round trip later; Base UI's Escape lands on the open,
      // filtered popup, so the title waits for that list first.
      await expect(page.getByRole('option')).toHaveCount(1);
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      // The close path already cleared the input, so nothing survives the reopen.
      await page.getByTestId('trigger').click();
      await settle(fixture);

      await expect(field).toHaveValue('');

      const before = (await recorded(fixture, 'data-input-changes')).length;

      await page.keyboard.press('Escape');
      await settle(fixture);

      expect((await recorded(fixture, 'data-input-changes')).slice(before)).toEqual([]);
    });

    test('keeps a keystroke typed into an empty input when items refresh synchronously', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'sync-refresh', animations: 1 });

      const field = input(page);
      await expect(field).toHaveValue('apple');

      await field.click();
      for (let count = 0; count < 5; count += 1) {
        await page.keyboard.press('Backspace');
      }
      await expect(field).toHaveValue('');
      // Deviation (2026-11-11): on Server the emptied query's list renders a round trip later; Base UI's Escape lands on it, so the
      // title waits for every item first.
      await expect(page.getByRole('option')).toHaveCount(3);

      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      // `ComboboxInput` sets the input value before requesting the open, so the reopen must not judge survival from the stale empty
      // value and let the `items` sync undo the keystroke.
      await page.keyboard.type('a');
      await settle(fixture);

      await expect(field).toHaveValue('a');
    });

    test('unfreezes filtering when a controlled open ignores the close request (multiple, input outside popup)', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'ignores-close' });

      const field = input(page);
      await typeInto(field, 'ap');
      await expect(page.getByRole('option')).toHaveCount(2);

      // The close request clears the input and freezes the query for the exit animation, but the consumer keeps the popup open, so
      // the frozen query must stop filtering.
      await page.keyboard.press('Escape');
      await settle(fixture);

      await expect(field).toHaveValue('');
      await expect(page.getByRole('option')).toHaveCount(3);
    });

    test('opens blank when the input only mirrors the selection (single, input inside popup)', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'mirror-single', animations: 1 });

      await page.getByTestId('trigger').click();
      const field = input(page);
      await expect(field).toBeFocused();

      await typeInto(field, 'ap');
      await expect(page.getByRole('option')).toHaveCount(2);

      await pressOption(option(page, 'apple'));

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      // A consumer mirroring the selection into the popup input must not leave the reopened list filtered by it: an interrupted close
      // opens blank, same as a completed one.
      await page.getByTestId('trigger').click();

      await expect(input(page)).toHaveValue('');
      await expect(page.getByRole('option')).toHaveCount(3);
    });

    test('keeps an input value set in the same batch as a controlled reopen (input inside popup)', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'preset', animations: 1 });

      await page.getByTestId('trigger').click();
      const field = input(page);
      await expect(field).toBeFocused();

      await typeInto(field, 'ap');
      await expect(page.getByRole('option')).toHaveCount(2);

      await page.keyboard.press('Escape');
      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      const before = (await recorded(fixture, 'data-input-changes')).length;

      // The consumer's value never fed the frozen query, so the reopen cleanup must not mistake it for leftover filter text and
      // clear it.
      await page.getByTestId('preset').click();
      await settle(fixture);

      await expect(input(page)).toHaveValue('banana');
      expect((await recorded(fixture, 'data-input-changes')).slice(before)).not.toContain(':input-clear');
    });

    test('shows every item when selection reopens the popup', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'selection-reopen', animations: 1 });

      const field = input(page);
      await typeInto(field, 'ap');
      await pressOption(option(page, 'apple'));

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await page.getByTestId('trigger').click();

      await expect(field).toHaveValue('apple');
      await expect(page.getByRole('option')).toHaveCount(3);
    });
  });

  test.describe('prop: multiple', () => {
    test('clears the pending filter with the "input-clear" reason when reopening interrupts the close animation', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'input-clear-reopen', animations: 1 });

      const trigger = page.getByTestId('trigger');
      await trigger.click();

      const field = input(page);
      await expect(field).toBeFocused();

      await typeInto(field, 'app');
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      const before = (await recorded(fixture, 'data-input-changes')).length;
      await trigger.click();

      await expect(input(page)).toHaveValue('');
      await settle(fixture);

      const changes = (await recorded(fixture, 'data-input-changes')).slice(before);
      const cleanup = changes.findIndex((entry) => entry.startsWith(':'));
      expect(cleanup).not.toBe(-1);
      expect(changes[cleanup]).toBe(':input-clear');
      expect((await recorded(fixture, 'data-input-item-presses')).slice(before)[cleanup]).toBe('null');
      // The synthetic placeholder event proves cleanup clears never carry the reopening gesture.
      expect((await recorded(fixture, 'data-input-events')).slice(before)[cleanup]).toBe('base-ui');
    });

    test('releases the frozen query when reopening during the close animation with the input outside the popup', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'multiple-outside', animations: 1 });

      const field = input(page);
      await typeInto(field, 'app');
      // Deviation (2026-11-11): on Server the popup that typing opens renders a round trip later; Base UI's Escape lands on the open,
      // filtered popup, so the title waits for that list first.
      await expect(page.getByRole('option')).toHaveCount(1);
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await page.getByTestId('trigger').click();

      await expect(field).toHaveValue('');
      await expect(option(page, 'banana')).toBeAttached();
    });

    test('does not emit another clear when controlled input is already empty on interrupted close', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'empty-controlled', animations: 1 });

      const trigger = page.getByTestId('trigger');
      await trigger.click();
      await typeInto(input(page), 'zz');
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');
      await expect(input(page)).toHaveValue('');

      await settle(fixture);
      const before = (await recorded(fixture, 'data-input-changes')).length;

      await trigger.click();

      await expect(popup).not.toHaveAttribute('data-ending-style');
      await expect(input(page)).toHaveValue('');
      await settle(fixture);
      expect((await recorded(fixture, 'data-input-changes')).slice(before)).toEqual([]);
    });
  });

  test.describe('prop: autoHighlight', () => {
    test.describe('asynchronous items', () => {
      test('discards the request when a controlled close is interrupted', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'async', animations: 1 });
        const field = page.getByRole('combobox');
        await typeInto(field, '32');
        await page.getByTestId('popup').evaluate((element) => {
          (window as unknown as { exitAnimation: Animation }).exitAnimation = element.animate({ opacity: [1, 0] }, { duration: 10000 });
        });

        try {
          await pressControl(page.getByTestId('close'));
          await settle(fixture);
          await pressControl(page.getByTestId('reopen'));
          await settle(fixture);

          await expect(field).toHaveValue('32');
          await expect(page.getByRole('option')).toHaveCount(1);
          await expect(page.getByRole('option')).not.toHaveAttribute('data-highlighted');
        } finally {
          await page.evaluate(() => (window as unknown as { exitAnimation: Animation }).exitAnimation.cancel());
        }
      });
    });
  });
});

test.describe('<Combobox.Input />', () => {
  test.describe('interaction behavior', () => {
    test('does not restore an inline highlight after the highlighted slot is removed', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'inline-remove' });
      const field = page.getByRole('combobox');

      await field.focus();
      await page.keyboard.press('ArrowDown');
      await expect(field).toHaveAttribute('aria-activedescendant');

      await page.getByRole('button', { name: 'remove items' }).click();
      await field.click();
      await settle(fixture);

      await expect(field).not.toHaveAttribute('aria-activedescendant');
    });
  });
});
