import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { settle } from './suites/popupFixture';
import { fire, gotoCombobox, moveTo, recorded, type FixtureOptions } from './combobox.helpers';

// Base UI's ComboboxChip.test.tsx, ComboboxChipRemove.test.tsx, ComboboxChips.test.tsx and parts.test.ts (M5 track 10, issue 174)
// on the `combobox-chips` fixture, titles verbatim, every title on both render modes. The fixture renders the page of each test
// from its query (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxChipsL4Fixture.razor). `settle` is the barrier a
// negative assertion waits behind: every event dispatched before it has reached C#.

/** Opens the fixture and waits behind the barrier, so the chips' own handles (they report no marker of theirs) are attached. */
async function open(page: Page, mode: RenderMode, options: FixtureOptions = {}): Promise<Locator> {
  const fixture = await gotoCombobox(page, mode, 'combobox-chips', options);
  await settle(fixture);
  // The Chips part reports its chips' ids to the combobox module after a render of its own, and the module keeps them on the element
  // under a symbol key (js/combobox.ts `chipIdsKey`): the key navigation sees a chip only once that list has it, so the title waits
  // until the list is as long as the rendered chips.
  if (options.disabled) {
    return fixture; // A disabled root navigates nothing; on WebAssembly its Chips report no ids.
  }
  await expect
    .poll(() =>
      page.getByTestId('chips').evaluate((element) => {
        const key = Object.getOwnPropertySymbols(element).find((symbol) => symbol.description === 'blazix.combobox.chipIds');
        const ids = key ? ((element as unknown as Record<symbol, string[]>)[key] ?? []) : [];
        const chips = element.querySelectorAll('[data-testid^="chip-"]').length;
        return ids.length === chips ? 'ready' : `ids ${ids.length} chips ${chips} key ${Boolean(key)}`;
      }),
    )
    .toBe('ready');
  return fixture;
}

/** A press in the Chips container's padding: the area that is no chip and no input. */
async function pressChipsArea(chips: Locator): Promise<void> {
  await chips.click({ position: { x: 4, y: 4 } });
}

const listbox = (page: Page) => page.getByRole('listbox');

test.describe('<Combobox.Chip />', () => {
  test.describe('prop: disabled', () => {
    test('should prevent keyboard navigation when disabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, { disabled: true });
      const chipApple = page.getByTestId('chip-apple');

      // Focus the chip manually (simulating navigation)
      await chipApple.focus();
      // Try to navigate with arrow keys
      await page.keyboard.press('ArrowRight');
      await settle(fixture);

      // The focus should remain on the same chip when disabled
      await expect(chipApple).toBeFocused();
    });

    test('should prevent deletion when disabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, { disabled: true });

      // Focus the chip manually
      await page.getByTestId('chip-apple').focus();
      // Try to delete with backspace
      await page.keyboard.press('Backspace');
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(page.getByTestId('chip-apple')).toBeVisible();
    });

    test('should prevent mouse interactions when disabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, { disabled: true, values: 'apple' });

      // Chip should not focus input on mouse down when disabled
      await page.getByTestId('chip-apple').click();
      await settle(fixture);

      await expect(page.getByTestId('input')).not.toBeFocused();
    });

    test('should prevent focus when disabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, { disabled: true, values: 'apple', input: 'none' });

      const chip = page.getByTestId('chip-apple');
      await chip.click();
      await settle(fixture);

      await expect(chip).not.toBeFocused();
    });
  });

  test.describe('prop: readOnly', () => {
    test('should prevent deletion when readOnly', async ({ page, mode }) => {
      const fixture = await open(page, mode, { readOnly: true });

      // Focus the chip manually
      await page.getByTestId('chip-apple').focus();
      // Try to delete with backspace
      await page.keyboard.press('Backspace');
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(page.getByTestId('chip-apple')).toBeVisible();
    });

    test('should prevent navigation when readOnly and also prevent deletion', async ({ page, mode }) => {
      const fixture = await open(page, mode, { readOnly: true });
      const chipApple = page.getByTestId('chip-apple');

      // Focus the first chip
      await chipApple.focus();

      // Navigation should be blocked
      await page.keyboard.press('ArrowRight');
      await settle(fixture);
      await expect(chipApple).toBeFocused();

      // Deletion should be blocked
      await page.keyboard.press('Delete');
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(page.getByTestId('chip-banana')).toBeVisible();
    });

    test('moves focus to the input when a chip is pressed while readOnly', async ({ page, mode }) => {
      await open(page, mode, { readOnly: true, values: 'apple', input: 'inside' });

      await page.getByTestId('chip-apple').click();

      await expect(page.getByTestId('input')).toBeFocused();
    });
  });

  test.describe('interaction behavior', () => {
    test('does not dismiss the popup when clicking a chip (outsidePress is blocked)', async ({ page, mode }) => {
      const fixture = await open(page, mode, { popup: true, defaultOpen: true, items: 'a|b' });

      await expect(listbox(page)).toBeVisible();

      await page.getByTestId('chip-apple').click();
      await settle(fixture);

      await expect(listbox(page)).toBeVisible();
    });

    test('closes the popup when a chip receives focus', async ({ page, mode }) => {
      const fixture = await open(page, mode, { popup: true, defaultOpen: true, items: 'a|b' });

      const input = page.getByTestId('input');
      await expect(listbox(page)).toBeVisible();

      await input.click();
      // Readiness: the click's open request has reached C# (a focus-out right behind it can be missed on Server, see the report).
      await settle(fixture);
      await page.getByTestId('chip-apple').focus();

      await expect(listbox(page)).toHaveCount(0);
    });

    test('should handle keyboard navigation when enabled', async ({ page, mode }) => {
      await open(page, mode, { values: 'apple,banana,cherry' });
      const chipApple = page.getByTestId('chip-apple');
      const input = page.getByTestId('input');

      // Focus the first chip
      await chipApple.focus();

      // Navigate right
      await page.keyboard.press('ArrowRight');
      await expect(page.getByTestId('chip-banana')).toBeFocused();

      // Navigate right again
      await page.keyboard.press('ArrowRight');
      await expect(page.getByTestId('chip-cherry')).toBeFocused();

      // Navigate right at the end should focus input
      await page.keyboard.press('ArrowRight');
      await expect(input).toBeFocused();

      // Navigate left from first chip should also focus input
      await chipApple.focus();
      await page.keyboard.press('ArrowLeft');
      await expect(input).toBeFocused();
    });

    test('returns focus to the input for activation, vertical navigation, and text entry', async ({ page, mode }) => {
      await open(page, mode, { values: 'apple', popup: true, items: 'banana' });
      const chip = page.getByTestId('chip-apple');
      const input = page.getByTestId('input');

      await chip.focus();
      await page.keyboard.press('Enter');
      await expect(input).toBeFocused();

      await chip.focus();
      await page.keyboard.press(' ');
      await expect(input).toBeFocused();

      await chip.focus();
      await page.keyboard.press('ArrowDown');
      await expect(input).toBeFocused();
      await expect(listbox(page)).toBeVisible();

      await page.keyboard.press('Escape');
      // The Escape closes the popup (a round trip on Server) before the chip takes the focus again.
      await expect(listbox(page)).toHaveCount(0);
      await chip.focus();
      await page.keyboard.press('ArrowUp');
      await expect(input).toBeFocused();
      await expect(listbox(page)).toBeVisible();

      await page.keyboard.press('Escape');
      await expect(listbox(page)).toHaveCount(0);
      await chip.focus();
      await page.keyboard.press('a');
      await expect(input).toBeFocused();
    });

    for (const key of ['ArrowDown', 'ArrowUp']) {
      test(`focuses a conditional input when reopening from a chip with ${key}`, async ({ page, mode }) => {
        const fixture = await open(page, mode, { values: 'apple', input: 'conditional', controlled: true, defaultOpen: true, popup: true, items: 'banana' });
        const chip = page.getByTestId('chip-apple');

        await page.getByRole('combobox').click();
        await settle(fixture);
        await page.keyboard.press('ArrowLeft');

        await expect(page.getByRole('combobox')).toHaveCount(0);
        await expect(chip).toBeFocused();

        await page.keyboard.press(key);

        await expect(page.getByRole('combobox')).toBeFocused();
      });
    }

    test('leaves focus on a chip for modified printable keys', async ({ page, mode }) => {
      const fixture = await open(page, mode, { values: 'apple' });
      const chip = page.getByTestId('chip-apple');

      await chip.focus();
      await page.keyboard.press('Control+a');
      await settle(fixture);
      await expect(chip).toBeFocused();

      await page.keyboard.press('Meta+a');
      await settle(fixture);
      await expect(chip).toBeFocused();

      await page.keyboard.press('Alt+a');
      await settle(fixture);
      await expect(chip).toBeFocused();
    });

    test('handles Delete as a chip removal key', async ({ page, mode }) => {
      const fixture = await open(page, mode);

      await page.getByTestId('chip-apple').focus();
      await page.keyboard.press('Delete');

      await expect(fixture).toHaveAttribute('data-value-changes', 'banana:none');
    });

    test('focuses the next chip after removing a chip rendered from the value', async ({ page, mode }) => {
      await open(page, mode, { values: 'apple,banana,cherry', fromValue: true, input: 'inside' });

      await page.getByTestId('chip-banana').focus();
      await page.keyboard.press('Delete');

      await expect(page.getByTestId('chip-cherry')).toBeFocused();
    });

    test('mirrors chip keyboard navigation in RTL mode', async ({ page, mode }) => {
      await open(page, mode, { rtl: true, input: 'inside' });
      const chipApple = page.getByTestId('chip-apple');
      const chipBanana = page.getByTestId('chip-banana');
      const input = page.getByTestId('input');

      await input.focus();
      await input.evaluate((element: HTMLInputElement) => element.setSelectionRange(0, 0));

      await page.keyboard.press('ArrowRight');
      await expect(chipBanana).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(chipApple).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(input).toBeFocused();

      await chipApple.focus();

      await page.keyboard.press('ArrowLeft');
      await expect(chipBanana).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(input).toBeFocused();
    });

    test('should navigate through only rendered chips', async ({ page, mode }) => {
      await open(page, mode, { values: 'apple,banana,cherry', fromValue: true, slice: 2, input: 'inside' });
      const chipBanana = page.getByTestId('chip-banana');
      const input = page.getByTestId('input');

      await input.focus();
      await input.evaluate((element: HTMLInputElement) => element.setSelectionRange(0, 0));

      await page.keyboard.press('ArrowLeft');
      await expect(chipBanana).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(input).toBeFocused();

      await chipBanana.focus();

      await page.keyboard.press('ArrowRight');
      await expect(input).toBeFocused();
    });

    test('should handle deletion when enabled', async ({ page, mode }) => {
      const fixture = await open(page, mode);

      // Focus the chip and delete it
      await page.getByTestId('chip-apple').focus();
      await page.keyboard.press('Backspace');
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual(['banana:none']);
    });

    test('should focus input on mouse down when enabled', async ({ page, mode }) => {
      await open(page, mode, { values: 'apple' });

      await page.getByTestId('chip-apple').click();

      await expect(page.getByTestId('input')).toBeFocused();
    });
  });
});

test.describe('<Combobox.ChipRemove />', () => {
  test.describe('prop: disabled', () => {
    test('should not remove chip when disabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, { disabled: true, remove: true });

      // Deviation (2026-11-10): Playwright waits for a button with `aria-disabled` to be enabled, so the click is forced; it is still
      // a real pointer press at the button's centre.
      await page.getByTestId('remove-apple').click({ force: true });
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(page.getByTestId('chip-apple')).toBeVisible();
    });
  });

  test.describe('prop: readOnly', () => {
    test('should not remove chip when readOnly', async ({ page, mode }) => {
      const fixture = await open(page, mode, { readOnly: true, remove: true });

      // The read-only remove button is `aria-disabled` too: the click is forced as above.
      await page.getByTestId('remove-apple').click({ force: true });
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(page.getByTestId('chip-apple')).toBeVisible();
    });

    test('should be focusable but not functional when readOnly', async ({ page, mode }) => {
      const fixture = await open(page, mode, { readOnly: true, remove: true, values: 'apple', input: 'none' });

      const remove = page.getByTestId('remove-apple');

      // Should be focusable
      await remove.focus();
      await expect(remove).toBeFocused();

      // But should not trigger action
      await page.keyboard.press('Enter');
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      await expect(remove).toBeVisible();
    });
  });

  test.describe('interaction behavior', () => {
    test('should remove chip on click when enabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, { remove: true });

      await page.getByTestId('remove-apple').click();
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual(['banana:chip-remove-press']);
    });

    test('should focus input after removing chip', async ({ page, mode }) => {
      await open(page, mode, { remove: true, values: 'apple' });

      await page.getByTestId('remove-apple').click();

      await expect(page.getByTestId('input')).toBeFocused();
    });

    test('should keep the popup open while removing a chip', async ({ page, mode }) => {
      const fixture = await open(page, mode, { popup: true, items: 'apple|banana', trigger: true, remove: true, values: 'apple' });

      await page.getByRole('button', { name: 'Open' }).click();
      await expect(listbox(page)).toBeVisible();

      await page.getByTestId('remove-apple').click();
      await settle(fixture);

      await expect(listbox(page)).toBeVisible();
      await expect(page.getByTestId('input')).toBeFocused();
    });

    test('keeps a popup input focused when removing a chip rendered outside the popup', async ({ page, mode }) => {
      await open(page, mode, { popup: true, items: 'apple|banana', trigger: true, remove: true, values: 'apple', fromValue: true, input: 'popup' });

      await page.getByRole('combobox').click();
      const input = page.getByTestId('input');
      await expect(input).toBeVisible();
      await expect(input).toBeFocused();

      await page.getByTestId('remove-apple').click();

      await expect(page.getByTestId('remove-apple')).toHaveCount(0);
      await expect(page.getByRole('dialog')).toBeVisible();
      await expect(input).toBeFocused();
      await expect(page.getByRole('option', { name: 'apple' })).toHaveAttribute('aria-selected', 'false');
    });

    test('should prevent event propagation', async ({ page, mode }) => {
      const fixture = await open(page, mode, { clicks: true, remove: true, values: 'apple' });

      await page.getByTestId('remove-apple').click();
      await settle(fixture);

      // Remove click should happen but not propagate to chip
      expect(await recorded(fixture, 'data-remove-clicks')).toHaveLength(1);
      expect(await recorded(fixture, 'data-chip-clicks')).toHaveLength(0);
    });

    test('allows click and keyboard events to propagate when requested', async ({ page, mode }) => {
      const fixture = await open(page, mode, { clicks: true, allowPropagation: true, remove: true, values: 'apple' });

      const remove = page.getByTestId('remove-apple');
      await remove.click();
      if (mode === 'server') {
        // ADR 01 relaxation (CMB-EVT-09's ChipRemove clause, KEY-10, PTR-10; §9, 2026-11-11): on Server the consumer's
        // allowPropagation() arrives after the dispatch, so the remove point's fallback stops the click and the keydown.
        await expect(fixture).toHaveAttribute('data-value-changes', ':chip-remove-press');
        await remove.focus();
        await page.keyboard.press('Enter');
        await expect(fixture).toHaveAttribute('data-value-changes', ':chip-remove-press :chip-remove-press');
        await settle(fixture);
        expect(await recorded(fixture, 'data-chip-clicks')).toEqual([]);
        expect(await recorded(fixture, 'data-chip-keydowns')).toEqual([]);
        return;
      }
      await expect(fixture).toHaveAttribute('data-chip-clicks', 'x');
      await settle(fixture);
      expect(await recorded(fixture, 'data-chip-clicks')).toHaveLength(1);

      await remove.focus();
      await page.keyboard.press('Enter');
      await expect(fixture).toHaveAttribute('data-chip-keydowns', /Enter/);
    });

    for (const rootProps of [{ disabled: true }, { readOnly: true }]) {
      test(`ignores click and keyboard activation on a non-native button: ${rootProps.disabled ? '{ disabled: true }' : '{ readOnly: true }'}`, async ({ page, mode }) => {
        const fixture = await open(page, mode, { ...rootProps, nonNative: true, remove: true, values: 'apple', input: 'none' });

        // Deviation (2026-11-10): Base UI dispatches a bare `click` and a bare `keydown`; the click here is a real pointer press
        // (forced, since the part is `aria-disabled`) and the key a real Enter on the focused part, which reach every handler the
        // two dispatched events do.
        const remove = page.getByTestId('remove-apple');
        await remove.click({ force: true });
        await remove.focus();
        await page.keyboard.press('Enter');
        await settle(fixture);

        expect(await recorded(fixture, 'data-value-changes')).toEqual([]);
      });
    }

    test('removes a chip with Space but ignores unrelated keys', async ({ page, mode }) => {
      const fixture = await open(page, mode, { remove: true, input: 'none' });

      const remove = page.getByTestId('remove-apple');
      await remove.focus();
      await page.keyboard.press('ArrowDown');
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toEqual([]);

      await page.keyboard.press(' ');
      await expect(fixture).toHaveAttribute('data-value-changes', 'banana:chip-remove-press');
      await settle(fixture);
      expect(await recorded(fixture, 'data-value-changes')).toEqual(['banana:chip-remove-press']);
    });

    test('keeps a different active item highlighted when removing a chip', async ({ page, mode }) => {
      const fixture = await open(page, mode, { popup: true, defaultOpen: true, remove: true, optionSuffix: true, items: 'apple|banana' });

      const apple = page.getByRole('option', { name: 'apple option' });
      await expect(apple).toBeVisible();
      await moveTo(page, apple);
      await expect(apple).toHaveAttribute('data-highlighted', '');
      // Deviation (2026-11-10): Base UI's `fireEvent.click` moves no pointer. A real click moves the pointer off the item, and the item's
      // pointer leave clears its highlight itself, so the click is dispatched where the pointer rests.
      await fire(page.getByTestId('remove-banana'), 'click');
      await settle(fixture);

      await expect(apple).toHaveAttribute('data-highlighted', '');
    });

    test('removes a filtered-out chip without clearing the visible highlight', async ({ page, mode }) => {
      const fixture = await open(page, mode, { popup: true, defaultOpen: true, remove: true, optionSuffix: true, items: 'banana' });

      const banana = page.getByRole('option', { name: 'banana option' });
      await expect(banana).toBeVisible();
      await moveTo(page, banana);
      await expect(banana).toHaveAttribute('data-highlighted', '');
      // Deviation (2026-11-10): as above, the click is dispatched where the pointer rests.
      await fire(page.getByTestId('remove-apple'), 'click');
      await settle(fixture);

      await expect(banana).toHaveAttribute('data-highlighted', '');
    });

    test('records pointer-origin removal when clearing the highlighted item', async ({ page, mode }) => {
      await open(page, mode, { popup: true, defaultOpen: true, remove: true, optionSuffix: true, items: 'apple', values: 'apple', input: 'none' });

      const apple = page.getByRole('option', { name: 'apple option' });
      await expect(apple).toBeVisible();
      await moveTo(page, apple, { unanchored: true });
      await expect(apple).toHaveAttribute('data-highlighted', '');
      await page.getByTestId('remove-apple').click();

      await expect(apple).not.toHaveAttribute('data-highlighted', '');
    });

    test('should handle keyboard activation', async ({ page, mode }) => {
      const fixture = await open(page, mode, { remove: true });

      await page.getByTestId('remove-apple').focus();
      await page.keyboard.press('Enter');
      await settle(fixture);

      expect(await recorded(fixture, 'data-value-changes')).toEqual(['banana:chip-remove-press']);
    });
  });
});

test.describe('<Combobox.Chips />', () => {
  test('focuses the input when clicking anywhere in the Chips area', async ({ page, mode }) => {
    await open(page, mode, { values: 'apple', input: 'inside' });

    const chips = page.getByTestId('chips');
    const chip = page.getByTestId('chip-apple');
    const input = page.getByTestId('input');

    await expect(input).not.toBeFocused();

    await pressChipsArea(chips);
    await expect(input).toBeFocused();

    // Blur and click on a chip: input should still receive focus.
    await input.evaluate((element: HTMLInputElement) => element.blur());
    await expect(input).not.toBeFocused();
    await chip.click();
    await expect(input).toBeFocused();
  });

  test('lets onMouseDown prevent the built-in focus and open behavior', async ({ page, mode }) => {
    const fixture = await open(page, mode, { chipsMouseDown: true, popup: true, items: 'apple|banana', values: 'apple', input: 'inside' });

    await pressChipsArea(page.getByTestId('chips'));
    await settle(fixture);

    expect(await recorded(fixture, 'data-chips-mousedowns')).toHaveLength(1);
    if (mode === 'server') {
      // ADR 01 Amendments 2026-11-10 (press): on Server the veto falls back to run. The press focuses and opens, and the handler still ran once.
      await expect(page.getByTestId('input')).toBeFocused();
      await expect(listbox(page)).toBeVisible();
      return;
    }
    await expect(page.getByTestId('input')).not.toBeFocused();
    await expect(listbox(page)).toHaveCount(0);
  });

  test('lets nested chip onMouseDown prevent the built-in focus and open behavior', async ({ page, mode }) => {
    const fixture = await open(page, mode, { chipMouseDown: true, popup: true, items: 'apple|banana', values: 'apple', input: 'inside' });

    await page.getByTestId('chip-apple').click();
    await settle(fixture);

    expect(await recorded(fixture, 'data-chip-mousedowns')).toHaveLength(1);
    await expect(page.getByTestId('input')).not.toBeFocused();
    await expect(listbox(page)).toHaveCount(0);
  });

  test('lets nested chip onContextMenu call preventBaseUIHandler', async ({ page, mode }) => {
    const fixture = await open(page, mode, { chipContextMenu: true, values: 'apple', input: 'inside' });

    // Deviation (2026-11-10): a real right click also presses (`mousedown`, button 2) before the `contextmenu`; Base UI fires only
    // the `contextmenu`. The assertion (the consumer's handler runs once and nothing throws) is Base UI's.
    await page.getByTestId('chip-apple').click({ button: 'right' });
    await settle(fixture);

    expect(await recorded(fixture, 'data-chip-contextmenus')).toHaveLength(1);
  });

  test('does not treat chip remove presses as chips-area presses', async ({ page, mode }) => {
    const fixture = await open(page, mode, { remove: true, popup: true, items: 'apple|banana', values: 'apple', input: 'inside' });

    const remove = page.getByTestId('remove-apple');
    await remove.hover();
    await page.mouse.down();
    try {
      await settle(fixture);
      await expect(listbox(page)).toHaveCount(0);
    } finally {
      await page.mouse.up();
    }
  });

  test('does not focus or open when disabled by Field.Root', async ({ page, mode }) => {
    const fixture = await open(page, mode, { field: true, popup: true, items: 'apple|banana', values: 'apple', input: 'inside' });

    await pressChipsArea(page.getByTestId('chips'));
    await settle(fixture);

    await expect(page.getByTestId('input')).not.toBeFocused();
    await expect(listbox(page)).toHaveCount(0);
  });

  test('focuses the input and opens when readOnly', async ({ page, mode }) => {
    await open(page, mode, { readOnly: true, popup: true, items: 'apple|banana', values: 'apple', input: 'inside' });

    await pressChipsArea(page.getByTestId('chips'));

    await expect(page.getByTestId('input')).toBeFocused();
    await expect(listbox(page)).toBeVisible();
  });

  test('opens and focuses an input rendered inside the popup when the chips area is pressed', async ({ page, mode }) => {
    await open(page, mode, { popup: true, items: 'apple|banana', trigger: true, values: 'apple', input: 'popup' });

    await pressChipsArea(page.getByTestId('chips'));

    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByTestId('input')).toBeFocused();
  });

  test('clears the highlighted chip when the popup opens', async ({ page, mode }) => {
    await open(page, mode, { popup: true, items: 'banana', values: 'apple', input: 'inside' });

    const input = page.getByTestId('input');
    await input.focus();
    await input.evaluate((element: HTMLInputElement) => element.setSelectionRange(0, 0));
    await page.keyboard.press('ArrowLeft');
    await expect(page.getByTestId('chip-apple')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(listbox(page)).toBeVisible();

    await input.focus();
    await input.evaluate((element: HTMLInputElement) => element.setSelectionRange(0, 0));
    await page.keyboard.press('ArrowLeft');
    await expect(page.getByTestId('chip-apple')).toBeFocused();
  });
});

test.describe('Combobox part utilities', () => {
  // parts.test.ts is a unit test of `getIndexAfterChipRemoval`; the browser test is the same behaviour through the real parts:
  // removing the only chip (a chip rendered from the value) leaves no chip to focus, so the focus goes to the input.
  test('returns no index after removing the only chip', async ({ page, mode }) => {
    const fixture = await open(page, mode, { values: 'apple', fromValue: true, input: 'inside' });

    await page.getByTestId('chip-apple').focus();
    await page.keyboard.press('Backspace');

    await expect(fixture).toHaveAttribute('data-value-changes', ':none');
    await expect(page.getByTestId('chip-apple')).toHaveCount(0);
    await expect(page.getByTestId('input')).toBeFocused();
  });
});
