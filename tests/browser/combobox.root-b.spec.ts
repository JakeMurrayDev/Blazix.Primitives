import { type Locator } from '@playwright/test';
import { expect, isScrollLocked, test, type Page, type RenderMode } from './helpers';
import { center, gotoCombobox, moveTo, recorded, touchTest, type FixtureOptions } from './combobox.helpers';
import { settle } from './suites/popupFixture';

// ComboboxRoot.test.tsx, lines 4000-9999, ported at L4 (M5 track 10, issue 174): Base UI's titles verbatim, on the `combobox-root-b`
// fixture (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxRootBL4Fixture.razor), every title on both render modes.
// The fixture's `case` and flags pick the test's rendering; its callbacks are recorded as the container's `data-*` attributes.

const FIXTURE = 'combobox-root-b';

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

/** The value of the last `highlights` entry (`value:reason:index`), Base UI's `onItemHighlighted.mock.lastCall?.[0]`. */
async function lastHighlight(fixture: Locator): Promise<string | undefined> {
  return (await recorded(fixture, 'data-highlights')).at(-1)?.split(':')[0];
}

function scrollTopOf(target: Locator): Promise<number> {
  return target.evaluate((element) => element.scrollTop);
}

function setScrollTop(target: Locator, top: number | 'end'): Promise<void> {
  return target.evaluate((element, value) => {
    element.scrollTop = value === 'end' ? element.scrollHeight : value;
  }, top);
}

function selection(field: Locator): Promise<{ start: number | null; end: number | null }> {
  return field.evaluate((element) => ({ start: (element as HTMLInputElement).selectionStart, end: (element as HTMLInputElement).selectionEnd }));
}

/** An IME composition, as the browser reports it: Chrome's composition events and input events, from the DevTools protocol. */
async function composeText(page: Page, steps: readonly string[], commit: string): Promise<void> {
  const session = await page.context().newCDPSession(page);
  for (const text of steps) {
    await session.send('Input.imeSetComposition', { text, selectionStart: text.length, selectionEnd: text.length });
  }
  await session.send('Input.insertText', { text: commit });
  await session.detach();
}

const ARROW_ROWS = '1,2,3|4,5,6';
const UNEVEN_ROWS = '1,2,3|4,5|6,7,8,9,10';

test.describe('<Combobox.Root />', () => {
  test.describe('keyboard interaction', () => {
    test('Escape closes the popup without committing when nothing highlighted', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'a|b', defaultOpen: true });

      await expect(page.getByRole('listbox')).toBeVisible();

      await page.keyboard.press('Escape');
      await expect(page.getByRole('listbox')).toHaveCount(0);
      await expect(input(page)).toHaveValue('');
    });

    test('bubbles Escape key when rendered inline without Positioner/Popup', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b', inline: true, defaultOpen: true, outer: true });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await page.keyboard.press('Escape');
      await settle(fixture);

      expect(await recorded(fixture, 'data-outer-escapes')).toHaveLength(1);
    });

    test('keeps input value on Enter when inline and no item is highlighted', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'Apple|Banana', inline: true });

      const field = input(page);
      await field.click();
      await field.pressSequentially('Ba');

      await expect(field).toHaveValue('Ba');
      await expect(field).not.toHaveAttribute('aria-activedescendant');

      await field.press('Enter');

      await expect(field).toHaveValue('Ba');
    });

    test('bubbles Escape key when list is empty and popup hidden with CSS', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { emptyItems: true, defaultOpen: true, outer: true });

      const positioner = page.getByTestId('positioner');
      await expect(positioner).toBeAttached();
      await positioner.evaluate((element) => {
        element.style.display = 'none';
      });

      await page.getByRole('combobox').click();
      await page.keyboard.press('Escape');
      await settle(fixture);

      expect(await recorded(fixture, 'data-outer-escapes')).toHaveLength(1);
    });

    test('does not bubble Escape key when Empty component is present', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { emptyItems: true, defaultOpen: true, outer: true, empty: true });

      const positioner = page.getByTestId('positioner');
      await expect(positioner).toBeAttached();
      await positioner.evaluate((element) => {
        element.style.display = 'none';
      });

      await page.getByRole('combobox').click();
      await page.keyboard.press('Escape');
      await settle(fixture);

      expect(await recorded(fixture, 'data-outer-escapes')).toHaveLength(0);
    });
  });

  test.describe('scroll locking', () => {
    test.describe('touch scroll lock', () => {
      touchTest('applies scroll lock when a touch-opened popup covers the viewport width', async ({ touchPage: page, mode }) => {
        await gotoRoot(page, mode, { items: 'Apple', modal: true, trigger: true, positionerWidth: 'full' });

        await page.getByTestId('trigger').tap();
        await expect(page.getByRole('listbox')).toBeVisible();

        await expect.poll(() => isScrollLocked(page)).toBe(true);
      });

      touchTest('does not apply scroll lock when a touch-opened popup is narrower than the viewport', async ({ touchPage: page, mode }) => {
        const fixture = await gotoRoot(page, mode, { items: 'Apple', modal: true, trigger: true, positionerWidth: '240px' });

        await page.getByTestId('trigger').tap();
        await expect(page.getByRole('listbox')).toBeVisible();
        await settle(fixture);
        await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));

        expect(await isScrollLocked(page)).toBe(false);
      });
    });
  });

  test('does not open on programmatic input events', async ({ page, mode }) => {
    const fixture = await gotoRoot(page, mode, { items: 'Darlinghurst|Sydney' });

    // Deviation (2026-11-11): Base UI's `fireEvent.change` sets the value from script and dispatches the event; a real input is never programmatic.
    await input(page).evaluate((element) => {
      const field = element as HTMLInputElement;
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, 'Darlinghurst');
      field.dispatchEvent(new Event('input', { bubbles: true }));
    });
    await settle(fixture);

    await expect(page.getByRole('listbox')).toHaveCount(0);
  });

  test('opens on paste input events', async ({ page, mode }) => {
    await gotoRoot(page, mode, { items: 'Darlinghurst|Sydney' });

    await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.evaluate(() => navigator.clipboard.writeText('Darlinghurst'));
    await input(page).focus();
    await page.keyboard.press('ControlOrMeta+v');

    await expect(page.getByRole('listbox')).toBeVisible();
  });

  test.describe('prop: disabled', () => {
    test('should prevent keyboard interactions when disabled', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b', disabled: true });

      // `user.type` clicks the element and types into whatever has focus; a disabled input takes neither.
      await input(page).click({ force: true });
      await page.keyboard.type('a');
      await settle(fixture);

      await expect(page.getByRole('listbox')).toHaveCount(0);
    });
  });

  test.describe('prop: readOnly', () => {
    test('should render readOnly state on the input', async ({ page, mode }) => {
      await gotoRoot(page, mode, { readOnly: true });

      await expect(input(page)).toHaveAttribute('aria-readonly', 'true');
      await expect(input(page)).toHaveAttribute('readonly');
    });

    test('should expose aria-autocomplete="none" when readOnly', async ({ page, mode }) => {
      await gotoRoot(page, mode, { readOnly: true });

      await expect(input(page)).toHaveAttribute('aria-autocomplete', 'none');
    });

    test('should open the popup from the input and the trigger when readOnly', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'a|b', readOnly: true, trigger: true });

      await input(page).click();
      await expect(page.getByRole('listbox')).toHaveAttribute('aria-readonly', 'true');

      await page.keyboard.press('Escape');
      await expect(page.getByRole('listbox')).toHaveCount(0);

      await page.getByTestId('trigger').click();
      await expect(page.getByRole('listbox')).toBeVisible();
    });

    test('should browse the items with the arrow keys when readOnly', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'a|b', readOnly: true });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'a', exact: true })).toHaveAttribute('data-highlighted', '');

      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'b', exact: true })).toHaveAttribute('data-highlighted', '');
    });

    test('should prevent value changes when readOnly with items', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b', readOnly: true, defaultOpen: true });

      await pressOption(page.getByRole('option', { name: 'a', exact: true }));
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
    });

    test('should not submit the form with Enter on a highlighted item when readOnly', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b', readOnly: true, form: true });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'a', exact: true })).toHaveAttribute('data-highlighted', '');

      await page.keyboard.press('Enter');
      await settle(fixture);

      expect(await recorded(fixture, 'data-submits')).toEqual([]);
    });

    test('should report arrow-key highlights as keyboard-driven when readOnly', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b', readOnly: true });

      // Opening by pointer marks pointer activity, which the arrow key must clear.
      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await settle(fixture);
      const before = (await recorded(fixture, 'data-highlights')).length;

      await page.keyboard.press('ArrowDown');

      await expect
        .poll(async () => (await recorded(fixture, 'data-highlights')).slice(before))
        .toContain('a:keyboard:0');
    });

    test('should not commit a value with Enter on a highlighted item when readOnly', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b', readOnly: true });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'a', exact: true })).toHaveAttribute('data-highlighted', '');

      await page.keyboard.press('Enter');
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(input(page)).toHaveValue('');
    });
  });

  test.describe('prop: grid', () => {
    test('arrow keys navigate across rows and columns in grid mode', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: ARROW_ROWS, defaultOpen: true });

      await input(page).click();
      await expect(page.getByRole('grid')).toBeVisible();

      const steps: [string, string][] = [
        ['ArrowDown', '1'],
        ['ArrowRight', '2'],
        ['ArrowRight', '3'],
        ['ArrowDown', '6'],
        ['ArrowLeft', '5'],
        ['ArrowUp', '2'],
      ];
      for (const [key, expected] of steps) {
        await page.keyboard.press(key);
        await expect.poll(() => lastHighlight(fixture)).toBe(expected);
      }
    });

    test('does not re-report the highlight on keys the grid does not handle', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: '1,2|3,4', defaultOpen: true });

      await input(page).click();
      await expect(page.getByRole('grid')).toBeVisible();

      await page.keyboard.press('ArrowDown');
      await expect.poll(() => lastHighlight(fixture)).toBe('1');
      const before = (await recorded(fixture, 'data-highlights')).length;

      // A modifier alone is not navigation; the grid navigator returns the unchanged index.
      await page.keyboard.press('Shift');
      await settle(fixture);
      expect((await recorded(fixture, 'data-highlights')).length).toBe(before);

      // A horizontal move is handled by the main-orientation path, once.
      await page.keyboard.press('ArrowRight');
      await expect.poll(() => lastHighlight(fixture)).toBe('2');
      expect((await recorded(fixture, 'data-highlights')).length).toBe(before + 1);
    });

    // https://github.com/mui/base-ui/issues/4947
    test('moves the input caret on ArrowLeft when no item is highlighted in grid mode', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: '1,2', defaultOpen: true });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('grid')).toBeVisible();

      await field.pressSequentially('abc');

      await expect(field).toHaveValue('abc');
      expect((await selection(field)).start).toBe(3);

      await page.keyboard.press('ArrowLeft');
      await settle(fixture);

      expect(await selection(field)).toEqual({ start: 2, end: 2 });
      expect(await recorded(fixture, 'data-highlights')).toEqual([]);
    });

    // https://github.com/mui/base-ui/issues/4947
    test('moves the input caret on ArrowRight when no item is highlighted in grid mode', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: '1,2', defaultOpen: true });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('grid')).toBeVisible();

      await field.pressSequentially('abc');
      await expect(field).toHaveValue('abc');
      await settle(fixture);
      await field.evaluate((element) => (element as HTMLInputElement).setSelectionRange(1, 1));

      expect((await selection(field)).start).toBe(1);

      await page.keyboard.press('ArrowRight');
      await settle(fixture);

      expect(await selection(field)).toEqual({ start: 2, end: 2 });
      expect(await recorded(fixture, 'data-highlights')).toEqual([]);
    });

    // https://github.com/mui/base-ui/issues/4947
    test('keeps grid navigation when autoHighlight surfaces an item before typing arrow keys', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: '1,2', defaultOpen: true, autoHighlight: true });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('grid')).toBeVisible();

      await field.pressSequentially('a');
      await expect.poll(() => lastHighlight(fixture)).toBe('1');

      await page.keyboard.press('ArrowRight');
      await expect.poll(() => lastHighlight(fixture)).toBe('2');
    });

    test('mirrors horizontal grid navigation in RTL mode', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: ARROW_ROWS, defaultOpen: true, rtl: true });

      await input(page).click();
      await expect(page.getByRole('grid')).toBeVisible();

      const steps: [string, string][] = [
        ['ArrowDown', '1'],
        ['ArrowLeft', '2'],
        ['ArrowLeft', '3'],
        ['ArrowRight', '2'],
      ];
      for (const [key, expected] of steps) {
        await page.keyboard.press(key);
        await expect.poll(() => lastHighlight(fixture)).toBe(expected);
      }
    });

    test('supports uneven rows navigation', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: UNEVEN_ROWS, defaultOpen: true });

      await input(page).click();
      await expect(page.getByRole('grid')).toBeVisible();

      // Down from the last column (3) to a shorter row clamps to its last item (5); up from the clamped item (5) returns to the same column
      // in the previous row (2); from 2, down to 5 (same column), then down to 7 in the longer row; left within the last row goes to 6,
      // then up to the first column in the previous row (4).
      const steps: [string, string][] = [
        ['ArrowDown', '1'],
        ['ArrowRight', '2'],
        ['ArrowRight', '3'],
        ['ArrowDown', '5'],
        ['ArrowUp', '2'],
        ['ArrowDown', '5'],
        ['ArrowDown', '7'],
        ['ArrowLeft', '6'],
        ['ArrowUp', '4'],
      ];
      for (const [key, expected] of steps) {
        await page.keyboard.press(key);
        await expect.poll(() => lastHighlight(fixture)).toBe(expected);
      }
    });

    test('supports uneven rows navigation within groups', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'grid', rows: UNEVEN_ROWS, groups: true, defaultOpen: true });

      await input(page).click();
      await expect(page.getByRole('grid')).toBeVisible();

      const steps: [string, string][] = [
        ['ArrowDown', '1'],
        ['ArrowRight', '2'],
        ['ArrowRight', '3'],
        ['ArrowDown', '5'],
        ['ArrowUp', '2'],
        ['ArrowDown', '5'],
        ['ArrowDown', '7'],
      ];
      for (const [key, expected] of steps) {
        await page.keyboard.press(key);
        await expect.poll(() => lastHighlight(fixture)).toBe(expected);
      }
    });
  });

  test.describe('prop: multiple', () => {
    test('keeps the filter text when a drag-select release commits the selection', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'apple|apricot|banana', multiple: true, defaultOpen: true, popupInput: true, cancelItemPress: true });

      const field = input(page);
      await field.click();
      await field.pressSequentially('app');
      const apple = page.getByRole('option', { name: 'apple' });
      await expect(apple).toBeAttached();

      // Press starts outside the item (drag-select), so the commit happens on mouseup. The popup has no anchor (the input is inside it and
      // no trigger exists), so it is never positioned.
      const from = await center(field, { unanchored: true });
      const to = await center(apple, { unanchored: true });
      await page.mouse.move(from.x, from.y);
      await page.mouse.down();
      await page.mouse.move(to.x, to.y, { steps: 4 });
      await expect(apple).toHaveAttribute('data-highlighted', '');
      await page.mouse.up();
      await settle(fixture);

      await expect(field).toHaveValue('app');
      await expect(apple).toHaveAttribute('aria-selected', 'true');
      expect((await recorded(fixture, 'data-input-item-presses')).at(-1)).toBe('true');
    });

    test('keeps the popup input focused through keyboard selection and restores the last selection', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|apricot|banana', multiple: true, trigger: true, triggerValue: true, popupInput: true });

      const trigger = page.getByTestId('trigger');
      await trigger.click();

      const field = input(page);
      await expect(field).toBeVisible();
      await field.pressSequentially('ban');
      await expect(page.getByRole('option')).toHaveCount(1);
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('Enter');

      await expect(page.getByRole('dialog')).toBeVisible();
      await expect(field).toBeFocused();
      await expect(field).toHaveValue('');
      await expect(trigger).toHaveText('banana');
      await expect(page.getByRole('option', { name: 'banana' })).toHaveAttribute('aria-selected', 'true');

      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toHaveCount(0);
      await expect(trigger).toBeFocused();

      await trigger.click();

      const selectedOption = page.getByRole('option', { name: 'banana' });
      await expect(selectedOption).toBeVisible();
      await expect(input(page)).toHaveValue('');
      await expect(selectedOption).toHaveAttribute('aria-selected', 'true');
      await expect(selectedOption).toHaveAttribute('data-highlighted', '');
    });

    test('keeps filtered popup content stable while closing with input inside popup', async ({ page, mode }) => {
      await gotoRoot(page, mode, {
        items: 'apple|apricot|banana',
        multiple: true,
        trigger: true,
        triggerValue: true,
        popupInput: true,
        empty: true,
        animated: true,
        label: 'Fruits',
        animations: 1,
      });

      const trigger = page.getByTestId('trigger');
      await trigger.click();

      const field = input(page);
      await expect(field).toBeVisible();
      await field.pressSequentially('zz');

      await expect(page.getByRole('status')).toHaveText('No matches');
      await expect(page.getByText('apple')).toHaveCount(0);

      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await expect(page.getByRole('status')).toHaveText('No matches');
      await expect(page.getByText('apple')).toHaveCount(0);

      // The close animation is long so a slow run can't finish it before the checks above.
      await popup.evaluate((element) => element.getAnimations().forEach((animation) => animation.finish()));
      await expect(page.getByTestId('popup')).toHaveCount(0);

      await trigger.click();

      await expect(input(page)).toBeVisible();
      await expect(input(page)).toHaveValue('');
      await expect(page.getByText('apple')).toBeVisible();
    });

    test('keeps filtered popup content stable when input changes during the close animation', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|apricot|banana', multiple: true, trigger: true, popupInput: true, animated: true, animations: 1 });

      await page.getByTestId('trigger').click();
      const field = input(page);
      await expect(field).toBeVisible();
      await field.pressSequentially('ap');
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await clearInput(field);

      await expect(page.getByText('apple')).toBeVisible();
      await expect(page.getByText('apricot')).toBeVisible();
      await expect(page.getByText('banana')).toHaveCount(0);
    });

    test('clears the deferred popup input when reopening during close animation', async ({ page, mode }) => {
      await gotoRoot(page, mode, {
        items: 'apple|apricot|banana',
        multiple: true,
        trigger: true,
        triggerValue: true,
        popupInput: true,
        empty: true,
        animated: true,
        label: 'Fruits',
        animations: 1,
      });

      const trigger = page.getByTestId('trigger');
      await trigger.click();

      const field = input(page);
      await expect(field).toBeVisible();
      await field.pressSequentially('zz');

      await expect(page.getByRole('status')).toHaveText('No matches');

      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await trigger.click();

      await expect(popup).not.toHaveAttribute('data-ending-style');

      await expect(input(page)).toHaveValue('');
      await expect(page.getByText('apple')).toBeVisible();
      await expect(page.getByText('banana')).toBeVisible();
    });
  });

  test.describe('prop: openOnInputClick', () => {
    test('keeps the popup open on a second input click by default', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'a' });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      // Click input again should not toggle closed automatically
      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();
    });

    test('does not open on input click when false, but opens on typing', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a', openOnInputClick: false });

      await input(page).click();
      await settle(fixture);
      await expect(page.getByRole('listbox')).toHaveCount(0);

      await input(page).pressSequentially('a');
      await expect(page.getByRole('listbox')).toBeVisible();
    });
  });

  test.describe('scroll reset on input value change', () => {
    const manyItems = Array.from({ length: 50 }, (_, index) => `item-${index}`).join('|');
    const filteringItems = Array.from({ length: 50 }, (_, index) => (index < 25 ? `alpha-${index}` : `beta-${index - 25}`)).join('|');
    const SCROLLING_LIST = 'max-height: 100px; overflow-y: auto';

    test('resets the list scroll position to the top when the query changes', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: filteringItems, listStyle: SCROLLING_LIST });

      const field = input(page);
      await field.click();

      const list = page.getByRole('listbox');
      await expect(list).toBeVisible();
      await setScrollTop(list, 40);
      expect(await scrollTopOf(list)).toBeGreaterThan(0);

      // Remove the current first item while keeping enough matches for the list to scroll.
      await field.pressSequentially('b');

      await expect.poll(() => scrollTopOf(list)).toBe(0);
    });

    test('resets manually filtered children without deferring the reset until hover', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'manual', items: filteringItems, listStyle: SCROLLING_LIST });

      const field = input(page);
      await field.click();

      const list = page.getByRole('listbox');
      await expect(list).toBeVisible();
      await setScrollTop(list, 40);
      expect(await scrollTopOf(list)).toBeGreaterThan(0);

      await field.pressSequentially('b');

      await expect.poll(() => scrollTopOf(list)).toBe(0);

      // A later highlight must not consume a typed-input reset that should already be cleared.
      await setScrollTop(list, 40);
      const visibleOption = page.getByRole('option', { name: 'beta-5' });
      await moveTo(page, visibleOption);
      await expect(visibleOption).toHaveAttribute('data-highlighted', '');
      expect(await scrollTopOf(list)).toBe(40);
    });

    test('resets the scroll position of a scrollable wrapper around the list', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: manyItems, viewport: 'scroll', listStyle: 'overflow-y: auto' });

      const field = input(page);
      await field.click();

      const viewport = page.getByTestId('viewport');
      await expect(viewport).toBeVisible();
      await setScrollTop(viewport, 40);
      expect(await scrollTopOf(viewport)).toBeGreaterThan(0);

      await field.pressSequentially('item-1');

      await expect.poll(() => scrollTopOf(viewport)).toBe(0);
    });

    test('skips clipping ancestors when finding the scroll container', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: manyItems, viewport: 'clip' });

      const field = input(page);
      await field.click();

      const viewport = page.getByTestId('viewport');
      await expect(viewport).toBeVisible();
      await setScrollTop(viewport, 40);
      expect(await scrollTopOf(viewport)).toBeGreaterThan(0);

      await field.pressSequentially('item-1');

      await expect.poll(() => scrollTopOf(viewport)).toBe(0);
    });

    test('keeps the auto-highlighted first item in view after the query changes', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: manyItems, autoHighlight: true, listStyle: SCROLLING_LIST });

      const field = input(page);
      await field.click();

      const list = page.getByRole('listbox');
      await expect(list).toBeVisible();
      await setScrollTop(list, 40);

      await field.pressSequentially('item-1');

      await expect.poll(() => scrollTopOf(list)).toBe(0);
    });

    test('resets the scroll container when filtering reorders retained items', async ({ page, mode }) => {
      const items = Array.from({ length: 10 }, (_, index) => `item-${index}`).join('|');
      await gotoRoot(page, mode, { case: 'reorder', items, listStyle: 'max-height: 60px; overflow-y: auto', itemStyle: 'height: 24px' });

      const field = input(page);
      await field.click();

      const list = page.getByRole('listbox');
      await expect(list).toBeVisible();
      await setScrollTop(list, 40);
      expect(await scrollTopOf(list)).toBeGreaterThan(0);

      await field.pressSequentially('x');

      await expect(page.getByRole('option').first()).toHaveText('item-9');
      await expect.poll(() => scrollTopOf(list)).toBe(0);
    });

    test('resets only the nearest scrollable wrapper when composed in a scrollable dialog', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'dialog-inline', items: manyItems });

      const field = input(page);
      const dialog = page.getByTestId('dialog');
      const viewport = page.getByTestId('viewport');
      await field.click();
      await setScrollTop(dialog, 150);
      await setScrollTop(viewport, 40);
      const dialogScrollTop = await scrollTopOf(dialog);
      expect(dialogScrollTop).toBeGreaterThan(0);
      expect(await scrollTopOf(viewport)).toBeGreaterThan(0);

      // Deviation (2026-11-11): Base UI's `user.keyboard` fires synthetic key and input events, which never scroll. Chrome scrolls the caret of a real
      // keystroke into view (here the input sits above the dialog's scrolled viewport: 141 becomes 103 with a plain <input>), so the input events are
      // dispatched as the browser reports typed text, with no scrolling.
      await field.evaluate((element) => {
        const target = element as HTMLInputElement;
        const setValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
        for (const character of 'item-1') {
          setValue.call(target, target.value + character);
          target.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: character }));
        }
      });

      await expect(field).toHaveValue('item-1');
      await expect.poll(() => scrollTopOf(viewport)).toBe(0);
      expect(await scrollTopOf(dialog)).toBe(dialogScrollTop);
    });

    test('does not reset a surrounding dialog', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'dialog-portal', items: manyItems });

      const field = input(page);
      const dialog = page.getByTestId('dialog');
      await field.click();
      await setScrollTop(dialog, 'end');
      const dialogScrollTop = await scrollTopOf(dialog);
      expect(dialogScrollTop).toBeGreaterThan(0);

      await page.keyboard.type('item-1');
      await expect(field).toHaveValue('item-1');
      await settle(fixture);

      expect(await scrollTopOf(dialog)).toBe(dialogScrollTop);
    });
  });

  test.describe('prop: autoHighlight', () => {
    test('highlights the first matching item after typing (single mode)', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry', autoHighlight: true });

      const field = page.getByRole('combobox');
      await field.click();
      await field.pressSequentially('ch');

      const cherry = page.getByRole('option', { name: 'cherry' });
      await expect(cherry).toBeVisible();
      await expect(field).toHaveAttribute('aria-activedescendant', (await cherry.getAttribute('id'))!);
    });

    test('highlights the first matching item after IME composition', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry', autoHighlight: true, openOnInputClick: false });

      const field = page.getByRole('combobox');
      await field.focus();
      await composeText(page, ['c', 'ch'], 'ch');

      await expect(page.getByRole('listbox')).toBeVisible();
      const cherry = page.getByRole('option', { name: 'cherry' });
      await expect(cherry).toBeVisible();
      await expect(field).toHaveAttribute('aria-activedescendant', (await cherry.getAttribute('id'))!);
    });

    test('keeps gridcell typeahead active across Space in row mode without selecting', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, {
        case: 'grid',
        rows: 'new-york~new york,new-jersey~new jersey|old-town~old town,other',
        autoHighlight: true,
      });

      const field = page.getByRole('combobox');
      await field.click();
      await field.pressSequentially('new');

      const newYork = page.getByRole('gridcell', { name: 'new york' });
      await expect(newYork).toHaveAttribute('data-highlighted', '');
      await expect(field).toHaveAttribute('aria-activedescendant', (await newYork.getAttribute('id'))!);

      await field.click();
      await field.pressSequentially(' ');
      await expect(field).toHaveValue('new ');
      await expect(newYork).toHaveAttribute('data-highlighted', '');
      await expect(field).toHaveAttribute('aria-activedescendant', (await newYork.getAttribute('id'))!);
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
    });

    test('clears highlight after removing the highlighted chip while filtering (multiple mode)', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'chips', items: 'apple|banana|cherry', autoHighlight: true, defaultOpen: true, defaultValues: 'apple' });

      const field = input(page);
      await expect(page.getByRole('listbox')).toBeVisible();

      await field.click();
      await field.pressSequentially('a');
      await expect(field).toHaveAttribute('aria-activedescendant');

      await page.getByRole('button', { name: 'Remove apple', includeHidden: true }).click();

      await expect(field).not.toHaveAttribute('aria-activedescendant');
    });

    test('keeps the active item highlighted after clearing the last selected value', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'JavaScript|TypeScript|Python|Ruby', multiple: true });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await pressOption(page.getByRole('option', { name: 'JavaScript' }));
      await pressOption(page.getByRole('option', { name: 'TypeScript' }));

      await field.click();
      await field.pressSequentially('pyth');
      await expect(page.getByRole('option')).toHaveCount(1);
      await pressOption(page.getByRole('option', { name: 'Python' }));

      await expect(page.getByRole('listbox')).toHaveCount(0);

      await field.focus();
      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('listbox')).toBeVisible();

      const javaScript = page.getByRole('option', { name: 'JavaScript' });
      await moveTo(page, javaScript);
      await pressOption(javaScript);
      const typeScript = page.getByRole('option', { name: 'TypeScript' });
      await moveTo(page, typeScript);
      await pressOption(typeScript);

      const pythonOption = page.getByRole('option', { name: 'Python' });
      await moveTo(page, pythonOption);
      await pressOption(pythonOption);

      await expect(field).toHaveAttribute('aria-activedescendant', (await pythonOption.getAttribute('id'))!);

      await page.keyboard.press('ArrowDown');
      const rubyOption = page.getByRole('option', { name: 'Ruby' });
      await expect(field).toHaveAttribute('aria-activedescendant', (await rubyOption.getAttribute('id'))!);
    });

    test('does not shift highlight to the previous selected item on Enter deselect', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'JavaScript|TypeScript|Python', multiple: true, defaultValues: 'JavaScript,TypeScript' });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();

      const typeScriptOption = page.getByRole('option', { name: 'TypeScript' });
      await moveTo(page, typeScriptOption);
      await expect(field).toHaveAttribute('aria-activedescendant', (await typeScriptOption.getAttribute('id'))!);

      await expect(typeScriptOption).toHaveAttribute('aria-selected', 'true');

      await page.keyboard.press('Enter');

      await expect(typeScriptOption).toHaveAttribute('aria-selected', 'false');
      await expect(field).toHaveAttribute('aria-activedescendant', (await typeScriptOption.getAttribute('id'))!);
    });

    test('continues ArrowDown navigation from the Enter-selected item (multiple mode)', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'chips', items: 'JavaScript|TypeScript|Python' });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();

      const typeScriptOption = page.getByRole('option', { name: 'TypeScript' });
      await moveTo(page, typeScriptOption);
      await expect(field).toHaveAttribute('aria-activedescendant', (await typeScriptOption.getAttribute('id'))!);

      await page.keyboard.press('Enter');
      await expect(field).toHaveAttribute('aria-activedescendant', (await typeScriptOption.getAttribute('id'))!);

      await page.keyboard.press('ArrowDown');

      const pythonOption = page.getByRole('option', { name: 'Python' });
      await expect(field).toHaveAttribute('aria-activedescendant', (await pythonOption.getAttribute('id'))!);
    });
  });
});
