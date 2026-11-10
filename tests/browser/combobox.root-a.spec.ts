import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { center, clickThrough, gotoCombobox, recorded, type FixtureOptions } from './combobox.helpers';
import { openChangeCompletes, openChanges, pressControl, settle } from './suites/popupFixture';

// ComboboxRoot.test.tsx, lines 1-3999, ported at L4 (M5 track 10, issue 174): Base UI's titles verbatim, on the `combobox-root-a`
// fixture (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxRootAL4Fixture.razor), every title on both render modes.
// The fixture's `case` picks the test's rendering; its callbacks are recorded as the container's `data-*` attributes.

const FIXTURE = 'combobox-root-a';

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

/**
 * A keydown that Chrome reports for a key an IME handles: `keyCode` and `which` 229. Playwright's keyboard cannot set them, and its
 * real IME is not available, so this sends a trusted key event through the DevTools protocol.
 */
async function pressImeKey(page: Page, key: string, code: string): Promise<void> {
  const session = await page.context().newCDPSession(page);
  const init = { key, code, windowsVirtualKeyCode: 229, nativeVirtualKeyCode: 229 };
  await session.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', ...init });
  await session.send('Input.dispatchKeyEvent', { type: 'keyUp', ...init });
  await session.detach();
}

/** Records every `scrollIntoView` call (Base UI's `vi.spyOn(HTMLElement.prototype, 'scrollIntoView')`), calling through. */
async function spyScrollIntoView(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const calls: { id: string; options: unknown }[] = [];
    (window as unknown as { scrollCalls: typeof calls }).scrollCalls = calls;
    const original = HTMLElement.prototype.scrollIntoView;
    HTMLElement.prototype.scrollIntoView = function (this: HTMLElement, ...args: unknown[]) {
      calls.push({ id: this.id, options: args[0] });
      return (original as (...parameters: unknown[]) => void).apply(this, args);
    };
  });
}

function scrollCalls(page: Page): Promise<{ id: string; options: unknown }[]> {
  return page.evaluate(() => (window as unknown as { scrollCalls: { id: string; options: unknown }[] }).scrollCalls.slice());
}

function clearScrollCalls(page: Page): Promise<void> {
  return page.evaluate(() => {
    (window as unknown as { scrollCalls: unknown[] }).scrollCalls.length = 0;
  });
}

const NEAREST = { block: 'nearest', inline: 'nearest' };

test.describe('<Combobox.Root />', () => {
  test.describe('IME composition', () => {
    test('does not select the highlighted item with an Enter that commits IME text', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b|c' });

      await input(page).focus();
      await page.keyboard.press('ArrowDown');
      const optionA = page.getByRole('option', { name: 'a', exact: true });
      await expect(optionA).toHaveAttribute('data-highlighted', '');

      await pressImeKey(page, 'Enter', 'Enter');
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(input(page)).toHaveAttribute('aria-expanded', 'true');
      await expect(optionA).toHaveAttribute('data-highlighted', '');
    });

    test('does not move the highlight with arrow keys that pick an IME candidate', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { items: 'a|b|c' });

      await input(page).focus();
      await page.keyboard.press('ArrowDown');
      const optionA = page.getByRole('option', { name: 'a', exact: true });
      await expect(optionA).toHaveAttribute('data-highlighted', '');

      // Chrome reports keys handled by an active IME composition with keyCode 229.
      await pressImeKey(page, 'ArrowDown', 'ArrowDown');
      await settle(fixture);

      await expect(optionA).toHaveAttribute('data-highlighted', '');
      await expect(page.getByRole('option', { name: 'b', exact: true })).not.toHaveAttribute('data-highlighted', '');
    });
  });

  test.describe('manual unmount lifecycle', () => {
    // ADR 01 relaxation (CMB-STA-29, Server): a popup held mounted by `preventUnmountOnClose` carries the convergent `hidden`, so Base UI's
    // `queryByRole('listbox')` (accessible elements only) becomes a presence check that includes hidden elements.
    const mountedListbox = (target: Page) => target.getByRole('listbox', { includeHidden: true });
    const completedClosings = async (fixture: Locator) => (await openChangeCompletes(fixture)).filter((open) => open === 'false');

    test('automatically unmounts with an actions ref and completes closing once', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'unmount' });

      expect(await openChangeCompletes(fixture)).toEqual([]);
      await page.getByRole('combobox').click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(mountedListbox(page)).toHaveCount(0);
      await settle(fixture);
      expect(await completedClosings(fixture)).toHaveLength(1);

      // Calling the action after the automatic unmount must not repeat the completion.
      await pressControl(fixture.locator('[data-unmount]'));
      await settle(fixture);
      expect(await completedClosings(fixture)).toHaveLength(1);
    });

    test('keeps the popup mounted until the unmount action completes closing', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'unmount', defaultOpen: true, retainEveryClose: true });

      await pressOption(page.getByRole('option'));
      await settle(fixture);
      await expect(mountedListbox(page)).toHaveCount(1);
      expect(await openChangeCompletes(fixture)).not.toContain('false');
      await pressControl(fixture.locator('[data-unmount]'));
      await expect(mountedListbox(page)).toHaveCount(0);
      await settle(fixture);
      expect(await completedClosings(fixture)).toHaveLength(1);

      await pressControl(fixture.locator('[data-set-open="true"]'));
      await expect(page.getByRole('listbox')).toBeVisible();
      await pressControl(fixture.locator('[data-set-open="false"]'));
      await expect(mountedListbox(page)).toHaveCount(0);
      await settle(fixture);
      expect(await completedClosings(fixture)).toHaveLength(2);
    });

    test('clears the opt-out when a controlled reopen interrupts a pending unmount', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'unmount', defaultOpen: true, retainEveryClose: true });

      await pressOption(page.getByRole('option'));
      await settle(fixture);
      await expect(mountedListbox(page)).toHaveCount(1);
      expect(await openChangeCompletes(fixture)).not.toContain('false');
      await pressControl(fixture.locator('[data-set-open="true"]'));
      await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'true');
      await pressControl(fixture.locator('[data-set-open="false"]'));
      await expect(mountedListbox(page)).toHaveCount(0);
      await settle(fixture);
      expect(await completedClosings(fixture)).toHaveLength(1);
    });

    test('ignores an opt-out on a canceled close', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'unmount', defaultOpen: true, cancelFirst: true });

      await pressOption(page.getByRole('option'));
      await settle(fixture);
      await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'true');
      await expect(mountedListbox(page)).toHaveCount(1);

      await pressControl(fixture.locator('[data-set-cancel="false"]'));
      await pressOption(page.getByRole('option'));
      await expect(mountedListbox(page)).toHaveCount(0);
    });

    // Deviation (2026-11-10): Base UI applies the controlled close in React.startTransition; Blazor has no transitions, so the
    // fixture applies it in the callback, as the other controlled titles do.
    test('keeps the opt-out when a controlled close is applied in a transition', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'unmount', defaultOpen: true, retainEveryClose: true });

      await pressOption(page.getByRole('option'));
      await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'false');
      await settle(fixture);
      await expect(mountedListbox(page)).toHaveCount(1);
      expect(await openChangeCompletes(fixture)).not.toContain('false');

      await pressControl(fixture.locator('[data-unmount]'));
      await expect(mountedListbox(page)).toHaveCount(0);
      await settle(fixture);
      expect(await completedClosings(fixture)).toHaveLength(1);
    });

    test('closes through the `close` action so `onOpenChange` can opt out', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'unmount', defaultOpen: true, retainEveryClose: true });
      await expect(page.getByRole('listbox')).toBeVisible();

      await pressControl(fixture.locator('[data-close]'));
      await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'false');
      await settle(fixture);
      expect(await openChanges(fixture)).toEqual(['false:imperative-action']);
      await expect(mountedListbox(page)).toHaveCount(1);

      await pressControl(fixture.locator('[data-unmount]'));
      await expect(mountedListbox(page)).toHaveCount(0);
    });

    test('completes closing once when `unmount` is called twice in one batch', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'unmount', defaultOpen: true, retainEveryClose: true });

      await pressOption(page.getByRole('option'));
      await settle(fixture);
      await expect(mountedListbox(page)).toHaveCount(1);

      await pressControl(fixture.locator('[data-unmount-twice]'));
      await expect(mountedListbox(page)).toHaveCount(0);
      await settle(fixture);
      expect(await completedClosings(fixture)).toHaveLength(1);
    });
  });

  test('does not focus input when closing via trigger click (input inside popup)', async ({ page, mode }) => {
    await gotoRoot(page, mode, { case: 'popup-input', items: 'One|Two|Three', label: 'Demo', inputLabel: 'combobox-input' });

    const trigger = page.getByTestId('trigger');
    await trigger.click();

    await expect(page.getByRole('listbox')).toBeVisible();

    const field = page.getByRole('combobox', { name: 'combobox-input' });
    await expect(field).toBeFocused();

    await trigger.click();
    await expect(trigger).toBeFocused();
  });

  test.describe('input inside popup composition', () => {
    test('selects with the keyboard, restores focus, and resets the query on reopen', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'popup-input', items: 'Apple|Banana|Cherry', label: 'Fruits', placeholder: 'Select+a+fruit' });

      const trigger = page.getByTestId('trigger');
      await trigger.click();

      const field = input(page);
      await expect(field).toBeFocused();

      await field.pressSequentially('ban');
      await expect(page.getByRole('option')).toHaveCount(1);
      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'Banana' })).toHaveAttribute('data-highlighted', '');
      await page.keyboard.press('Enter');

      await expect(page.getByRole('dialog')).toHaveCount(0);
      await expect(trigger).toBeFocused();
      await expect(trigger).toHaveText('Banana');

      await trigger.click();

      await expect(input(page)).toHaveValue('');

      const banana = page.getByRole('option', { name: 'Banana' });
      await expect(banana).toHaveAttribute('aria-selected', 'true');
      await expect(banana).toHaveAttribute('data-highlighted', '');
    });

    test('discards an uncommitted query on Escape and preserves the selection', async ({ page, mode }) => {
      const fixture = await gotoRoot(page, mode, { case: 'popup-input', items: 'Apple|Banana|Cherry', defaultValue: 'Apple', label: 'Fruits' });

      const trigger = page.getByTestId('trigger');
      await trigger.click();
      await input(page).pressSequentially('ban');
      await expect(page.getByRole('option')).toHaveCount(1);
      await page.keyboard.press('Escape');

      await expect(page.getByRole('dialog')).toHaveCount(0);
      await expect(trigger).toBeFocused();
      await expect(trigger).toHaveText('Apple');
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);

      await trigger.click();

      await expect(input(page)).toHaveValue('');
      await expect(page.getByRole('option', { name: 'Apple' })).toHaveAttribute('aria-selected', 'true');
      await expect(page.getByRole('option', { name: 'Banana' })).toBeVisible();
    });

    test('clears a single-select query and restores the selection when reopening during the close animation', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'popup-input', items: 'Apple|Banana', defaultValue: 'Banana', trigger: 'Open', animated: true, empty: true, animations: 1 });

      const trigger = page.getByTestId('trigger');
      await trigger.click();
      const field = input(page);
      await expect(field).toBeFocused();
      await clearInput(field);
      await field.pressSequentially('banana');
      await expect(field).toHaveValue('banana');
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await trigger.click();

      await expect(popup).not.toHaveAttribute('data-ending-style');
      // Not ported: the SelectedIndexProbe text '1' reads the store's `selectedIndex`, which the port does not expose; the
      // highlight of the selected item below is what the index drives.
      await expect(field).toHaveValue('');
      await expect(page.getByRole('option', { name: 'Apple' })).toBeVisible();
      const banana = page.getByRole('option', { name: 'Banana' });
      await expect(banana).toHaveAttribute('data-highlighted', '');
      await expect(field).toHaveAttribute('aria-activedescendant', (await banana.getAttribute('id'))!);
    });

    test('preserves a typed query when input reopens single-select during the close animation', async ({ page, mode }) => {
      await gotoRoot(page, mode, { case: 'popup-input', items: 'Apple|Banana', trigger: 'Open', animated: true, empty: true, animations: 1 });

      await page.getByTestId('trigger').click();
      const field = input(page);
      await expect(field).toBeFocused();
      await field.pressSequentially('ap');
      await expect(field).toHaveValue('ap');
      await page.keyboard.press('Escape');

      const popup = page.getByTestId('popup');
      await expect(popup).toHaveAttribute('data-ending-style', '');

      await field.focus();
      await page.keyboard.type('b');

      await expect(popup).not.toHaveAttribute('data-ending-style');
      await expect(field).toHaveValue('apb');
      await expect(page.getByRole('status')).toHaveText('No matches');
      await expect(page.getByRole('option')).toHaveCount(0);
    });
  });

  test('does not aria-hide the input group when the input is outside the popup', async ({ page, mode }) => {
    const fixture = await gotoRoot(page, mode, { case: 'group', items: 'apple|banana' });

    await input(page).click();
    await expect(page.getByRole('listbox')).toBeVisible();
    await settle(fixture);

    await expect(input(page)).toBeFocused();
    await expect(page.getByTestId('group')).not.toHaveAttribute('aria-hidden', 'true');
  });

  test('dismisses the popup when clicking a plain wrapper around the input', async ({ page, mode }) => {
    await gotoRoot(page, mode, { case: 'wrapper', items: 'apple|banana' });

    await page.getByRole('button', { name: 'Open' }).click();
    await expect(page.getByRole('listbox')).toBeVisible();

    await page.getByTestId('pad').click();

    await expect(page.getByRole('listbox')).toHaveCount(0);
  });

  test('does not dismiss when pressing portalled content inside the popup but outside the list', async ({ page, mode }) => {
    const fixture = await gotoRoot(page, mode, { case: 'portalled', defaultOpen: true, portalled: 'content' });
    await expect(page.getByRole('listbox')).toBeVisible();

    await clickThrough(page, page.getByText('Portalled content'));
    await settle(fixture);

    await expect(page.getByRole('listbox')).toBeVisible();
  });

  test('does not navigate the list with arrow keys from portalled controls inside the popup', async ({ page, mode }) => {
    const fixture = await gotoRoot(page, mode, { case: 'portalled', defaultOpen: true, portalled: 'control' });
    await expect(page.getByRole('listbox')).toBeVisible();

    const portalledControl = page.getByRole('button', { name: 'Portalled control' });
    await portalledControl.focus();
    await page.keyboard.press('ArrowDown');
    await settle(fixture);

    // Base UI reads the store's active index (`null`); the port shows it as no highlighted item, no active descendant and no highlight reported.
    await expect(page.locator('[data-highlighted]')).toHaveCount(0);
    await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
    expect(await recorded(fixture, 'data-highlights')).toEqual([]);
  });

  test('does not dismiss when pressing content inside a nested popover', async ({ page, mode }) => {
    const fixture = await gotoRoot(page, mode, { case: 'nested-popover', defaultOpen: true });
    await expect(page.getByRole('listbox')).toBeVisible();

    await page.getByRole('button', { name: 'Open nested popover' }).click();

    await clickThrough(page, page.getByRole('button', { name: 'Nested focusable content' }));
    await settle(fixture);

    await expect(page.getByRole('listbox')).toBeVisible();
  });

  test('wraps modal focus from popup controls back to the input', async ({ page, mode }) => {
    await gotoRoot(page, mode, { case: 'modal-wrap', defaultOpen: true, modal: true });
    await expect(page.getByRole('listbox', { includeHidden: true })).toBeVisible();

    const field = input(page);
    const extraControl = page.getByRole('button', { name: 'Extra control' });

    await field.focus();
    await page.keyboard.press('Tab');
    await expect(extraControl).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(field).toBeFocused();
  });

  test('hides the trigger when popup is open with input outside the popup', async ({ page, mode }) => {
    await gotoRoot(page, mode, { case: 'hide-trigger', items: 'Apple|Banana' });

    await input(page).click();

    await expect(page.getByRole('listbox')).toBeVisible();

    await expect.poll(() => isInert(page.getByTestId('outside'))).toBe(true);

    expect(await isInert(page.getByTestId('trigger'))).toBe(true);
  });

  test('renders internal dismiss buttons before the input and after the popup', async ({ page, mode }) => {
    await gotoRoot(page, mode, { case: 'dismiss', items: 'Apple|Banana', defaultOpen: true, modal: true });
    await expect(page.getByRole('listbox')).toBeVisible();

    const popup = page.getByTestId('popup');
    const field = page.getByRole('combobox', { name: 'Combobox input' });
    const dismissButtons = page.getByRole('button', { name: 'Dismiss' });
    await expect(dismissButtons).toHaveCount(2);
    const startDismissButton = dismissButtons.nth(0);
    const endDismissButton = dismissButtons.nth(1);

    expect(await field.evaluate((element, start) => element.previousElementSibling === start, await startDismissButton.elementHandle())).toBe(true);
    expect(await popup.evaluate((element, end) => element.nextElementSibling === end, await endDismissButton.elementHandle())).toBe(true);
    await expect(startDismissButton).not.toHaveAttribute('tabindex');
    await expect(endDismissButton).not.toHaveAttribute('tabindex');

    await expect.poll(() => isInert(page.getByTestId('outside'))).toBe(true);

    // Deviation (2026-11-10): the dismiss button is a 1 px element clipped to nothing (`clip-path: inset(50%)`) with no tabindex, so no real pointer or key reaches it.
    await endDismissButton.dispatchEvent('click');

    await expect(page.getByRole('listbox')).toHaveCount(0);
  });

  test.describe('selection behavior', () => {
    test.describe('single', () => {
      test('highlights and scrolls the selected item into view on first open (no items prop)', async ({ page, mode }) => {
        await spyScrollIntoView(page);
        await gotoRoot(page, mode, { static: true, defaultValue: 'banana' });

        await input(page).click();

        const selectedItem = page.getByRole('option', { name: 'banana' });
        await expect(selectedItem).toHaveAttribute('data-highlighted', '');
        await expect(input(page)).toHaveAttribute('aria-activedescendant', (await selectedItem.getAttribute('id'))!);
        const id = (await selectedItem.getAttribute('id'))!;
        await expect.poll(async () => (await scrollCalls(page)).map((call) => call.id)).toEqual([id]);
        expect((await scrollCalls(page))[0]!.options).toEqual(NEAREST);
      });

      test('emits the selected item index to onItemHighlighted on mount when inline and virtualized (items prop)', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'inline-virtualized', gen: '50|item-|0', defaultValue: 'item-30' });

        await expect.poll(() => recorded(fixture, 'data-highlights')).toContain('item-30:none:30');
      });

      test('restores the highlight and scroll to the selected item when the query is cleared', async ({ page, mode }) => {
        await spyScrollIntoView(page);
        await gotoRoot(page, mode, { case: 'popup-input', items: 'apple|banana|cherry', defaultValue: 'banana' });

        await page.getByTestId('trigger').click();
        const field = input(page);
        await expect(field).toBeFocused();

        // Move the highlight away from the selected item by filtering.
        await field.pressSequentially('cherry');
        await expect(page.getByRole('option', { name: 'banana' })).toHaveCount(0);

        await clearScrollCalls(page);

        // Clearing the query should restore the highlight to the selected item.
        await clearInput(field);

        const selectedItem = page.getByRole('option', { name: 'banana' });
        await expect(selectedItem).toHaveAttribute('data-highlighted', '');
        await expect(field).toHaveAttribute('aria-activedescendant', (await selectedItem.getAttribute('id'))!);
        await expect.poll(async () => (await scrollCalls(page)).map((call) => call.options)).toContainEqual(NEAREST);
      });

      test('does not navigate kept-mounted items while controlled closed', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { items: 'apple|banana', open: false, keepMounted: true, autoHighlight: true });

        await input(page).click();
        await input(page).pressSequentially('banana');
        await expect(input(page)).toHaveValue('banana');
        await settle(fixture);

        // Base UI reads the store's active index (`null`); the port shows it as no highlighted item, no active descendant and no highlight reported.
        await expect(page.locator('[data-highlighted]')).toHaveCount(0);
        await expect(input(page)).not.toHaveAttribute('aria-activedescendant');
        expect(await recorded(fixture, 'data-highlights')).toEqual([]);
      });

      test('scrolls to programmatic highlights after the filtered list changes', async ({ page, mode }) => {
        await gotoRoot(page, mode, {
          case: 'popup-input',
          gen: '200|item-|3',
          defaultValue: 'item-180',
          autoHighlight: true,
          listStyle: 'max-height: 100px; overflow: auto',
        });

        await page.getByTestId('trigger').click();
        const field = input(page);
        await expect(field).toBeFocused();
        const list = page.getByTestId('list');
        await list.evaluate((element) => {
          element.scrollTop = element.scrollHeight;
        });

        await field.pressSequentially('9');

        const firstMatch = page.getByRole('option', { name: 'item-009' });
        await expect(firstMatch).toHaveAttribute('data-highlighted', '');
        await expect.poll(() => list.evaluate((element) => element.scrollTop)).toBe(0);

        await list.evaluate((element) => {
          element.scrollTop = element.scrollHeight;
        });
        await clearInput(field);

        const selectedItem = page.getByRole('option', { name: 'item-180' });
        await expect(selectedItem).toHaveAttribute('data-highlighted', '');
        await expect
          .poll(async () => {
            const listBox = (await list.boundingBox())!;
            const selectedBox = (await selectedItem.boundingBox())!;
            return selectedBox.y >= listBox.y && selectedBox.y + selectedBox.height <= listBox.y + listBox.height;
          })
          .toBe(true);
      });

      // Deviation (2026-11-10): `Combobox.useFilteredItems()` is Base UI's hook; the fixture's List renders an item per filtered item
      // and mounts only the window of ten that follows the highlight, which is the same externally windowed list.
      test('restores the selected item through an externally virtualized list', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'external-virtualized', gen: '100|item-|3', defaultValue: 'item-080' });

        await page.getByTestId('trigger').click();
        const field = input(page);
        await expect(field).toBeFocused();

        await field.pressSequentially('item-000');
        await expect(page.getByRole('option', { name: 'item-000' })).toBeVisible();
        const before = (await recorded(fixture, 'data-scroll-to-index')).length;

        await clearInput(field);

        await expect.poll(async () => (await recorded(fixture, 'data-scroll-to-index')).slice(before)).toContain('80');
        const selectedItem = page.getByRole('option', { name: 'item-080' });
        await expect(selectedItem).toHaveAttribute('data-highlighted', '');
        await expect(field).toHaveAttribute('aria-activedescendant', (await selectedItem.getAttribute('id'))!);
      });

      test('fires onOpenChange once with reason item-press on mouse click', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { items: 'apple|banana' });

        await page.getByRole('combobox').click();
        await expect(page.getByRole('listbox')).toBeVisible();
        await settle(fixture);

        const before = (await openChanges(fixture)).length;
        await pressOption(page.getByRole('option', { name: 'apple' }));

        await expect(page.getByRole('listbox')).toHaveCount(0);
        await settle(fixture);

        const calls = (await openChanges(fixture)).slice(before);
        expect(calls).toEqual(['false:item-press']);
        // `event instanceof MouseEvent`: the pointer events are MouseEvents too.
        expect((await recorded(fixture, 'data-open-change-events')).at(-1)).toMatch(/^(click|mouse|pointer)/);
      });

      test('fires onOpenChange once with reason item-press on Enter selection', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { items: 'apple|banana' });

        await page.getByRole('combobox').click();
        await expect(page.getByRole('listbox')).toBeVisible();

        await page.keyboard.press('ArrowDown');
        await expect(page.getByRole('option', { name: 'apple' })).toHaveAttribute('data-highlighted', '');
        await settle(fixture);
        const before = (await openChanges(fixture)).length;
        await page.keyboard.press('Enter');

        await expect(page.getByRole('listbox')).toHaveCount(0);
        await settle(fixture);

        const calls = (await openChanges(fixture)).slice(before);
        expect(calls).toEqual(['false:item-press']);
        expect((await recorded(fixture, 'data-open-change-events')).at(-1)).toBe('keydown');
      });
    });

    test.describe('multiple', () => {
      test('anchors to the first selected item when opened with the keyboard', async ({ page, mode }) => {
        await gotoRoot(page, mode, { case: 'multi-popup', items: 'apple|banana|cherry', defaultValues: 'banana,cherry' });

        const trigger = page.getByTestId('trigger');
        await page.keyboard.press('Tab');
        await expect(trigger).toBeFocused();
        await page.keyboard.press('ArrowDown');

        const bananaItem = page.getByRole('option', { name: 'banana' });
        await expect(bananaItem).toHaveAttribute('data-highlighted', '');
        await expect(page.getByRole('option', { name: 'cherry' })).not.toHaveAttribute('data-highlighted');
      });

      test('continues arrow navigation from the anchor rather than the top of the list', async ({ page, mode }) => {
        await gotoRoot(page, mode, { case: 'multi-popup', items: 'apple|banana|cherry', defaultValues: 'banana,cherry' });

        await page.getByTestId('trigger').click();
        const field = input(page);
        // Focus reaches the input asynchronously. Keys sent before it lands are lost.
        await expect(field).toBeFocused();
        const bananaItem = page.getByRole('option', { name: 'banana' });
        await expect(bananaItem).toHaveAttribute('data-highlighted', '');

        // The next item after the anchor, not `apple` at the top.
        await page.keyboard.press('ArrowDown');
        await expect(page.getByRole('option', { name: 'cherry' })).toHaveAttribute('data-highlighted', '');

        await page.keyboard.press('ArrowUp');
        await expect(bananaItem).toHaveAttribute('data-highlighted', '');
      });

      test('scrolls the first selected item into view when clearing (input inside popup)', async ({ page, mode }) => {
        await spyScrollIntoView(page);
        const fixture = await gotoRoot(page, mode, { case: 'multi-popup', gen: '100|item |0', defaultValues: 'item 90,item 80' });

        await page.getByTestId('trigger').click();
        const field = input(page);
        await expect(field).toBeFocused();
        await field.pressSequentially('item 0');
        await expect(page.getByRole('option', { name: 'item 80' })).toHaveCount(0);

        await clearScrollCalls(page);
        await clearInput(field);

        const selectedItem = page.getByRole('option', { name: 'item 80' });
        await expect(selectedItem).toHaveAttribute('data-highlighted', '');
        // Base UI's ActiveIndexProbe reads '80': the highlight reported for the item is at index 80.
        await expect.poll(() => recorded(fixture, 'data-highlights')).toContain('item_80:none:80');
        const id = (await selectedItem.getAttribute('id'))!;
        await expect.poll(async () => (await scrollCalls(page)).map((call) => call.id)).toEqual([id]);
        expect((await scrollCalls(page))[0]!.options).toEqual(NEAREST);
      });

      // Deviation (2026-11-10): SelectedIndexProbe reads the store's `selectedIndex`, which the port does not expose. The index anchors the
      // highlight on open, so the title shows the opened list's highlight on the first selected item (index 0), and the selection cleared.
      test('resets selectedIndex when clearing all selections while open', async ({ page, mode }) => {
        const fixture = await gotoRoot(page, mode, { case: 'multi-controlled', items: 'apple|banana|cherry', defaultValues: 'apple,banana' });

        await expect(page.getByRole('listbox')).toHaveCount(0);

        await input(page).click();

        await expect(page.getByRole('listbox')).toBeVisible();
        await expect(page.getByRole('option', { name: 'apple' })).toHaveAttribute('data-highlighted', '');

        await pressControl(page.getByTestId('clear'));

        await settle(fixture);
        await expect(page.getByRole('option', { selected: true })).toHaveCount(0);
      });
    });
  });

  test.describe('keyboard interaction', () => {
    test('focuses first item on ArrowDown and last item on ArrowUp', async ({ page, mode }) => {
      await gotoRoot(page, mode, { static: true });

      const field = input(page);
      await field.focus();

      await page.keyboard.press('ArrowDown');
      const first = page.getByRole('option', { name: 'apple' });
      await expect(field).toHaveAttribute('aria-activedescendant', (await first.getAttribute('id'))!);

      await page.keyboard.press('Escape');

      await expect(page.getByRole('listbox')).toHaveCount(0);

      await page.keyboard.press('ArrowUp');

      const last = page.getByRole('option', { name: 'cherry' });
      await expect(field).toHaveAttribute('aria-activedescendant', (await last.getAttribute('id'))!);
    });

    test('opens, navigates with ArrowDown, and Enter selects', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry' });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      // Highlight first item and select it
      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'apple' })).toHaveAttribute('data-highlighted', '');
      await page.keyboard.press('Enter');

      await expect(page.getByRole('listbox')).toHaveCount(0);
      await expect(input(page)).toHaveValue('apple');
    });

    test('Enter selects with manual indices provided to items', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry', indexed: true });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await input(page).pressSequentially('c'); // filter to "cherry"
      await expect(page.getByRole('option')).toHaveCount(1);
      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'cherry' })).toHaveAttribute('data-highlighted', '');
      await page.keyboard.press('Enter');

      await expect(input(page)).toHaveValue('cherry');
    });

    test('clicking on "listbox" keeps the focus on the input', async ({ page, mode }) => {
      await gotoRoot(page, mode, { items: 'apple|banana|cherry', listStyle: 'padding: 10px' });

      const field = input(page);
      await field.click();
      await expect(page.getByRole('listbox')).toBeVisible();

      // The list's own padding is the pixel that is the listbox and no item, as Base UI's `user.click(listbox)` targets it.
      const listbox = page.getByRole('listbox');
      await center(listbox);
      await listbox.click({ position: { x: 3, y: 3 } });
      await expect(field).toBeFocused();

      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('option', { name: 'apple' })).toHaveAttribute('data-highlighted', '');
      await page.keyboard.press('Enter');

      await expect(field).toHaveValue('apple');
    });
  });
});

/** Base UI's `isElementOrAncestorInert`. */
function isInert(target: Locator): Promise<boolean> {
  return target.evaluate((element) => {
    for (let current: HTMLElement | null = element as HTMLElement; current; current = current.parentElement) {
      if (current.getAttribute('aria-hidden') === 'true' || current.hasAttribute('inert') || current.hasAttribute('data-base-ui-inert')) {
        return true;
      }
    }
    return false;
  });
}
