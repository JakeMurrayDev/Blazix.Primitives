import { type Locator } from '@playwright/test';
import { expect, test, waitForPositioned, type Page, type RenderMode } from './helpers';
import { center, clickThrough, fire, gotoCombobox, moveTo, recorded, touchTest, waitMs, type FixtureOptions } from './combobox.helpers';
import { pressControl, settle } from './suites/popupFixture';

// The titles of ComboboxClear, ComboboxItem, ComboboxItemIndicator, ComboboxList, ComboboxPopup, ComboboxPositioner, createItems
// (the "serializes" ones) and parts.test.ts, ported at L4 (M5 track 10, issue 174): Base UI's titles verbatim, on the
// `combobox-parts` fixture (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxPartsL4Fixture.razor), every title on
// both render modes. The fixture's `case` picks the test's rendering; its callbacks are recorded as the container's `data-*`
// attributes.
//
// Not ported (track 11's, they render Autocomplete.*): ComboboxClear.test.tsx "renders and clears the typed value in no-selection mode".

const FIXTURE = 'combobox-parts';

function gotoParts(page: Page, mode: RenderMode, options: FixtureOptions = {}): Promise<Locator> {
  return gotoCombobox(page, mode, FIXTURE, options);
}

const input = (page: Page) => page.getByTestId('input');
const option = (page: Page, name: string) => page.getByRole('option', { name, exact: true });

/** Dispatches Base UI's `new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })` on an element; whether it was prevented. */
function dispatchEnter(target: Locator): Promise<boolean> {
  return target.evaluate((element) => {
    const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    element.dispatchEvent(event);
    return event.defaultPrevented;
  });
}

/** Records, from the page's first script on, the `transitionend` events of the element with `data-testid="clear"` (Base UI's `onTransitionEnd`). */
async function recordClearTransitions(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const ends: string[] = [];
    (window as unknown as { clearTransitionEnds: string[] }).clearTransitionEnds = ends;
    document.addEventListener(
      'transitionend',
      (event) => {
        if ((event.target as HTMLElement).dataset.testid === 'clear') {
          ends.push((event as TransitionEvent).propertyName);
        }
      },
      true,
    );
  });
}

function clearTransitionEnds(page: Page): Promise<number> {
  return page.evaluate(() => (window as unknown as { clearTransitionEnds: string[] }).clearTransitionEnds.length);
}

test.describe('<Combobox.Clear />', () => {
  test('renders in single mode when a value is selected', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'clear-single', defaultValue: 'a' });

    await expect(page.getByTestId('clear')).toHaveCount(1);
  });

  test('does not render without a value by default', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { case: 'clear-single' });
    await settle(fixture);

    await expect(page.getByTestId('clear')).toHaveCount(0);
  });

  test('renders and clears chips in multiple mode', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'clear-chips' });

    await expect(page.getByText('apple', { exact: true })).toHaveCount(1);

    await page.getByTestId('clear').click();

    await expect(page.getByText('apple', { exact: true })).toHaveCount(0);
  });

  test('click clears selected value and focuses input', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'clear-list', defaultValue: 'a', items: 'a' });

    await page.getByTestId('clear').click();

    await expect(page.getByTestId('clear')).toHaveCount(0);
    await expect(input(page)).toBeFocused();
  });

  test('clears after pointer interaction marks keyboard navigation inactive', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'clear-single', defaultValue: 'a' });

    // Base UI's fireEvent.pointerMove(input): a real pointer moving over the input.
    await moveTo(page, input(page), { unanchored: true });
    await page.getByTestId('clear').click();

    await expect(page.getByTestId('clear')).toHaveCount(0);
  });

  test('does not dismiss the popup on click (outsidePress is blocked)', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'clear-list', defaultOpen: true, defaultValue: 'a', items: 'a|b' });

    await expect(page.getByRole('listbox')).toBeVisible();

    await page.getByTestId('clear').click();

    await expect(page.getByTestId('clear')).toHaveCount(0);
    await expect(page.getByRole('listbox')).toBeVisible();
  });

  test('does not dismiss a popup input when the clear button is rendered outside it', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'clear-trigger', defaultValue: 'a', items: 'a|b' });

    await page.getByRole('combobox').click();
    await expect(page.getByTestId('input')).toBeFocused();

    await page.getByTestId('clear').click();

    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByTestId('input')).toBeFocused();
    await expect(option(page, 'a')).toHaveAttribute('aria-selected', 'false');
  });

  test('is disabled when root disabled and does nothing on click', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { case: 'clear-single', defaultValue: 'a', disabled: true });

    const clear = page.getByTestId('clear');
    await expect(clear).toHaveAttribute('disabled');

    // Deviation (2026-11-10): a real click never reaches a disabled button, so the click is dispatched as Base UI's fireEvent.click does.
    await fire(clear, 'click');
    await settle(fixture);
    await expect(page.getByTestId('clear')).toHaveCount(1);
  });

  test('when root is readOnly it does nothing on click', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { case: 'clear-single', defaultValue: 'a', readOnly: true });

    await page.getByTestId('clear').click();
    await settle(fixture);

    await expect(page.getByTestId('clear')).toHaveCount(1);
  });

  test('clears selection without closing and restores popup input focus', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'clear-keepmounted', defaultValue: 'a' });

    await page.getByTestId('trigger').click();

    const clear = page.getByTestId('clear');
    await expect(clear).toBeAttached();

    await expect(page.getByTestId('positioner')).toHaveAttribute('data-open', '');
    await expect(clear).toHaveClass(/\bvisible\b/);
    await expect(clear).toHaveAttribute('data-visible', '');

    await clickThrough(page, clear);

    await expect(clear).toHaveClass(/\bhidden\b/);
    await expect(clear).not.toHaveAttribute('data-visible');
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByTestId('input')).toBeFocused();
    await expect(page.getByTestId('trigger')).toHaveText('None');
    await expect(option(page, 'a')).toHaveAttribute('aria-selected', 'false');
  });

  test.describe('animations', () => {
    test('triggers enter animation via data-starting-style when becoming visible', async ({ page, mode }) => {
      await recordClearTransitions(page);
      await gotoParts(page, mode, { case: 'clear-animated', animations: 1 });

      await expect(page.getByTestId('clear')).toHaveCount(0);

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await clickThrough(page, option(page, 'a'));

      await expect.poll(() => clearTransitionEnds(page)).toBeGreaterThan(0);
      await expect(page.getByTestId('clear')).toHaveCount(1);
    });

    test('triggers exit animation via data-ending-style before unmount', async ({ page, mode }) => {
      await recordClearTransitions(page);
      await gotoParts(page, mode, { case: 'clear-animated', animations: 1, keepMounted: true, defaultValue: 'a' });

      const clear = page.getByTestId('clear');
      await expect(clear).toHaveCount(1);

      await clear.click();

      await expect.poll(() => clearTransitionEnds(page)).toBeGreaterThan(0);
    });
  });
});

test.describe('<Combobox.Item />', () => {
  test('selects item and closes in single mode', async ({ page, mode }) => {
    await gotoParts(page, mode, { defaultOpen: true, static: true });

    await expect(option(page, 'two')).toBeVisible();
    await clickThrough(page, option(page, 'two'));

    await expect(input(page)).toHaveValue('two');
    await expect(page.getByRole('listbox')).toHaveCount(0);
  });

  test.describe('prop: onClick', () => {
    test('calls onClick when clicked with a pointer', async ({ page, mode }) => {
      const fixture = await gotoParts(page, mode, { items: 'apple|banana', openOnInputClick: true, clicks: true });

      await input(page).click();
      await expect(page.getByRole('listbox')).toBeVisible();

      await clickThrough(page, option(page, 'banana'));
      await settle(fixture);

      expect(await recorded(fixture, 'data-item-clicks')).toEqual(['banana']);
    });
  });

  test('does not select disabled item', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { defaultOpen: true, static: true, disabledItem: 'two' });

    await expect(option(page, 'two')).toBeVisible();
    await clickThrough(page, option(page, 'two'));
    await settle(fixture);

    await expect(input(page)).toHaveValue('');
  });

  test('Enter selects highlighted item', async ({ page, mode }) => {
    await gotoParts(page, mode, { static: true });

    await input(page).click();
    await expect(page.getByRole('listbox')).toBeVisible();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');

    await expect(input(page)).toHaveValue('one');
    await expect(page.getByRole('listbox')).toHaveCount(0);
  });

  touchTest('keeps the input focused after selecting an item with touch', async ({ touchPage: page, mode }) => {
    await gotoParts(page, mode, { defaultOpen: true, static: true });

    await input(page).click();
    await expect(input(page)).toBeFocused();

    await expect(option(page, 'two')).toBeVisible();
    await waitForPositioned(page.getByRole('listbox').locator('xpath=ancestor-or-self::*[@data-side][last()]'));
    await option(page, 'two').tap();

    await expect(input(page)).toHaveValue('two');
    await expect(input(page)).toBeFocused();
  });

  test('prevents default on mousedown so pointer selection does not steal input focus', async ({ page, mode }) => {
    await gotoParts(page, mode, { defaultOpen: true, static: true });

    await expect(option(page, 'two')).toBeVisible();
    await center(option(page, 'two'));
    // Deviation (2026-11-10): a real press prevents the default of its pointerdown (CMB-PTR-03), which suppresses the compatibility mousedown,
    // so the mousedown Base UI's test creates is dispatched on the item (a cancelable MouseEvent, button 0) and its cancelation read.
    const prevented = await option(page, 'two').evaluate((element) => {
      const mouseDown = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 });
      element.dispatchEvent(mouseDown);
      return mouseDown.defaultPrevented;
    });

    expect(prevented).toBe(true);
  });

  test.describe('prop: index', () => {
    test('updates the highlight on hover in a non-virtualized list', async ({ page, mode }) => {
      await gotoParts(page, mode, { items: 'apple|banana|cherry', static: true, rootItems: true, indexed: true, defaultOpen: true, autoHighlight: true });

      await expect(page.getByRole('listbox')).toBeVisible();

      const banana = option(page, 'banana');
      await moveTo(page, banana);
      await expect(banana).toHaveAttribute('data-highlighted', '');

      const cherry = option(page, 'cherry');
      await moveTo(page, cherry);
      await expect(cherry).toHaveAttribute('data-highlighted', '');
      await expect(banana).not.toHaveAttribute('data-highlighted');
    });
  });
});

test.describe('<Combobox.ItemIndicator />', () => {
  test('updates a mounted indicator when its item becomes unselected', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'indicator-open' });

    await expect(page.getByTestId('apple-indicator')).toHaveAttribute('data-selected', '');

    await clickThrough(page, option(page, 'banana'));

    await expect(page.getByTestId('apple-indicator')).not.toHaveAttribute('data-selected');
  });

  test('mounts only while selected when keepMounted is false', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { case: 'indicator' });
    const setValue = (value: string) => pressControl(fixture.locator(`[data-set-value="${value}"]`));

    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(page.getByTestId('indicator')).toHaveCount(0);

    await setValue('a');

    const indicator = page.getByTestId('indicator');
    await expect(indicator).toHaveAttribute('data-selected', '');
    await expect(indicator).toHaveAttribute('aria-hidden', 'true');
    await expect(indicator).not.toHaveAttribute('selected');

    await setValue('b');

    await expect(page.getByTestId('indicator')).toHaveCount(0);

    await setValue('a');

    await expect(page.getByTestId('indicator')).toHaveAttribute('data-selected', '');
  });

  test('preserves the kept-mounted element across selection changes', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { case: 'indicator', keepMounted: true });
    const setValue = (value: string) => pressControl(fixture.locator(`[data-set-value="${value}"]`));

    await expect(page.getByRole('listbox')).toBeVisible();
    const indicator = page.getByTestId('indicator');
    const handle = await indicator.elementHandle();
    const isSameElement = () => page.getByTestId('indicator').evaluate((element, original) => element === original, handle);

    await expect(indicator).not.toHaveAttribute('data-selected');

    await setValue('a');

    await expect(indicator).toHaveAttribute('data-selected', '');
    expect(await isSameElement()).toBe(true);

    await setValue('b');

    await expect(indicator).not.toHaveAttribute('data-selected');
    expect(await isSameElement()).toBe(true);
    await expect(indicator).not.toHaveAttribute('data-ending-style');

    await setValue('a');

    await expect(indicator).toHaveAttribute('data-selected', '');
    expect(await isSameElement()).toBe(true);
  });
});

test.describe('<Combobox.List />', () => {
  test('does not prevent Enter when no item is highlighted', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { defaultOpen: true, static: true, items: 'a' });

    const list = page.getByRole('listbox');
    await expect(list).toBeVisible();
    // Deviation (2026-11-10): the keydown targets the List itself, as Base UI's test dispatches it; a real Enter reaches the Input.
    const prevented = await dispatchEnter(list);
    await settle(fixture);

    expect(prevented).toBe(false);
  });

  test('selects the highlighted item with Enter', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { defaultOpen: true, static: true, items: 'a' });

    await page.getByRole('combobox').click();
    await expect(page.getByRole('listbox')).toBeVisible();
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'a')).toHaveAttribute('data-highlighted', '');

    // Deviation (2026-11-10): the keydown targets the List itself, as Base UI's test dispatches it; a real Enter reaches the Input.
    await dispatchEnter(page.getByRole('listbox'));
    await settle(fixture);

    expect((await recorded(fixture, 'data-value-changes')).map((entry) => entry.split(':')[0])).toEqual(['a']);
  });

  for (const flag of ['disabled', 'readOnly']) {
    test(`ignores Enter when interaction is disabled: { ${flag}: true }`, async ({ page, mode }) => {
      const fixture = await gotoParts(page, mode, { defaultOpen: true, static: true, items: 'a', noInput: true, [flag]: true });

      const list = page.getByRole('listbox');
      await expect(list).toBeVisible();
      // Deviation (2026-11-10): the keydown targets the List itself, as Base UI's test dispatches it (this root has no Input).
      await dispatchEnter(list);
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
    });
  }
});

test.describe('<Combobox.Popup />', () => {
  touchTest('focuses the popup instead of its input when opened by touch', async ({ touchPage: page, mode }) => {
    await gotoParts(page, mode, { case: 'popup-trigger' });

    await page.getByTestId('trigger').tap();

    await expect(page.getByTestId('popup')).toBeFocused();
    await expect(page.getByTestId('input')).not.toBeFocused();
  });

  test('honors initialFocus={false}', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { case: 'popup-trigger', initialFocus: 'false' });

    const trigger = page.getByTestId('trigger');
    await trigger.focus();
    await trigger.click();

    await expect(page.getByTestId('input')).toBeAttached();
    await settle(fixture);
    await expect(trigger).toBeFocused();
  });

  test('returns focus to an explicitly provided element when the popup closes', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'popup-final' });

    await expect(page.getByRole('listbox')).toBeVisible();
    await page.keyboard.press('Escape');

    await expect(page.getByRole('button', { name: 'final focus' })).toBeFocused();
  });
});

test.describe('<Combobox.Positioner />', () => {
  test('should not lock body scroll when controlled value={[]} triggers a re-render', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'pos-scroll' });

    // Base UI waits 500 ms for the mount, the re-render and the scroll locker's timeout; the page's time runs that long.
    await waitMs(page, 500);

    const overflow = await page.evaluate(() => ({
      bodyX: document.body.style.overflowX,
      bodyY: document.body.style.overflowY,
      htmlX: document.documentElement.style.overflowX,
      htmlY: document.documentElement.style.overflowY,
    }));

    expect(overflow.bodyX).not.toBe('hidden');
    expect(overflow.bodyY).not.toBe('hidden');
    expect(overflow.htmlX).not.toBe('hidden');
    expect(overflow.htmlY).not.toBe('hidden');
  });

  test('keeps the popup on the preferred side when its capped height fits below tall content', async ({ page, mode }) => {
    await gotoParts(page, mode, { case: 'pos-side' });

    await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'bottom');
  });

  test.describe('default anchor', () => {
    // The Positioner's `sideOffset` callback reads `data.anchor.width`; the fixture's offset is that width, so the gap between the
    // anchor's bottom edge and the Positioner's top edge is the width of the anchor the Positioner used.
    async function offsetFromAnchor(page: Page, anchor: Locator): Promise<number> {
      const positioner = page.getByTestId('positioner');
      await waitForPositioned(positioner);
      const anchorBox = (await anchor.boundingBox())!;
      const positionerBox = (await positioner.boundingBox())!;
      return positionerBox.y - (anchorBox.y + anchorBox.height);
    }

    test('uses the input when input group is absent', async ({ page, mode }) => {
      await gotoParts(page, mode, { case: 'pos-anchor-input' });

      const inputWidth = (await input(page).boundingBox())!.width;
      const triggerWidth = (await page.getByTestId('trigger').boundingBox())!.width;

      await expect.poll(() => offsetFromAnchor(page, input(page))).toBeCloseTo(inputWidth, 0);
      expect(await offsetFromAnchor(page, input(page))).not.toBeCloseTo(triggerWidth, 0);
    });

    test('uses the input group when present', async ({ page, mode }) => {
      await gotoParts(page, mode, { case: 'pos-anchor-group' });

      const groupWidth = (await page.getByTestId('group').boundingBox())!.width;

      await expect.poll(() => offsetFromAnchor(page, page.getByTestId('group'))).toBeCloseTo(groupWidth, 0);
    });
  });
});

test.describe('Combobox.createItems', () => {
  test.describe('integration', () => {
    test('serializes derived values in multiple mode', async ({ page, mode }) => {
      await gotoParts(page, mode, { case: 'users-multiple' });

      const hidden = page.locator('input[name="users"]');
      await expect(hidden).toHaveCount(2);
      expect(await hidden.evaluateAll((inputs) => inputs.map((element) => (element as HTMLInputElement).value))).toEqual(['1', '2']);
      expect(await page.getByTestId('form').evaluate((form) => new FormData(form as HTMLFormElement).getAll('users'))).toEqual(['1', '2']);
    });

    test('serializes a derived value in single mode', async ({ page, mode }) => {
      await gotoParts(page, mode, { case: 'users-single' });

      await expect(page.locator('input[name="user"]')).toHaveValue('2');
      expect(await page.getByTestId('form').evaluate((form) => new FormData(form as HTMLFormElement).getAll('user'))).toEqual(['2']);
    });

    test('serializes the derived value with itemToStringValue', async ({ page, mode }) => {
      await gotoParts(page, mode, { case: 'users-string' });

      await expect(page.locator('input[name="user"]')).toHaveValue('user-2');
      expect(await page.getByTestId('form').evaluate((form) => new FormData(form as HTMLFormElement).getAll('user'))).toEqual(['user-2']);
    });
  });
});

test.describe('Combobox part utilities', () => {
  // parts.test.ts unit-tests `clickHighlightedItem(store, index, nativeEvent)`, the helper a keyboard Enter on the Input runs. Each title is
  // ported as a browser test of the same behaviour through the real parts: an Enter keydown with a highlighted item clicks the rendered item
  // (the Item's `onclick` runs once, the value change carries the originating keydown), and does nothing when that item is not rendered.
  test('does nothing when the highlighted item is not rendered', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { items: 'a|b|c', skipFirst: true, clicks: true });

    await input(page).click();
    await expect(page.getByRole('listbox')).toBeVisible();
    // The list renders the items from the second on, so ArrowDown highlights the first, which is not rendered.
    await expect(option(page, 'a')).toHaveCount(0);
    await page.keyboard.press('ArrowDown');
    await expect.poll(async () => (await recorded(fixture, 'data-highlights')).map((entry) => entry.split(':')[0])).toEqual(['a']);

    await page.keyboard.press('Enter');
    await settle(fixture);

    expect(await recorded(fixture, 'data-item-clicks')).toEqual([]);
    expect(await recorded(fixture, 'data-value-events')).toEqual([]);
    expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
  });

  test('clicks the rendered highlighted item with the originating event', async ({ page, mode }) => {
    const fixture = await gotoParts(page, mode, { static: true, clicks: true });

    await input(page).click();
    await expect(page.getByRole('listbox')).toBeVisible();
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'one')).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('Enter');
    await settle(fixture);

    expect(await recorded(fixture, 'data-item-clicks')).toEqual(['one']);
    expect(await recorded(fixture, 'data-value-events')).toEqual(['keydown']);
    expect((await recorded(fixture, 'data-value-changes')).map((entry) => entry.split(':')[0])).toEqual(['one']);
  });
});
