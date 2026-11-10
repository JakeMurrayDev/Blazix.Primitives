// L4 for docs/components/select.md (M5 track 9, issue 173), the navigation half of SelectRoot.test.tsx (4470 to the end),
// SelectPositioner.test.tsx's kept-mounted title and SelectScrollArrow.test.tsx's L4 title: Server then WebAssembly.
// Fixtures: tests/Blazix.Primitives.Fixtures.Client/Select/SelectNav*Fixture.razor (and `select-positioner`).
import { type Locator, type Page } from '@playwright/test';
import { expect, test } from './helpers';
import { waitForPositioned } from './helpers/positioner';
import { gotoPopupFixture, pressControl, settle } from './suites/popupFixture';

const trigger = (page: Page) => page.getByTestId('trigger');
const option = (page: Page, name: string) => page.getByRole('option', { name, exact: true });

/** Two animation frames: what Base UI's `settleFrames` (two `waitSingleFrame`s) waits for. */
const frames = (page: Page) =>
  page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));

/** Settles the events so far (every one has reached C#), then two frames. */
async function settled(page: Page, fixture: Locator): Promise<void> {
  await settle(fixture);
  await frames(page);
}

/**
 * Focuses the trigger and waits for its 0 ms force-mount to register the closed list (SEL-FOC-11), the state a closed-trigger
 * typeahead keystroke needs: the hidden items exist in the DOM.
 */
async function focusTriggerMounted(page: Page, fixture: Locator, itemCount: number): Promise<void> {
  await trigger(page).focus();
  await expect(page.getByRole('option', { includeHidden: true })).toHaveCount(itemCount);
  // The DOM is not the registry: the list's labels reach the module a round trip and a frame later.
  await settled(page, fixture);
}

/**
 * Focuses the trigger and settles, so a key pressed next does not race the trigger's 0 ms force-mount (the race the port title
 * in select.spec.ts, "a keyboard open racing the trigger's force-mount", pins on its own).
 */
async function focusTrigger(page: Page, fixture: Locator): Promise<void> {
  await trigger(page).focus();
  await settled(page, fixture);
}

/** Waits until the element stops moving: two successive frames report the same box. */
async function stable(page: Page, target: Locator): Promise<void> {
  let previous = '';
  await expect
    .poll(async () => {
      await frames(page);
      const now = JSON.stringify(await target.boundingBox());
      const same = now === previous;
      previous = now;
      return same;
    })
    .toBe(true);
}

/**
 * Waits for the first pass of the Positioner around `target` before a title reads its box (issue 191): until then the
 * Positioner holds `opacity: 0` over the unpositioned layout.
 */
async function positioned(target: Locator): Promise<void> {
  // The target first: right after the press that opens the popup it may not be in the page yet.
  await target.waitFor({ state: 'attached' });
  const positioner = target.locator('xpath=ancestor::*[@data-testid="positioner"]').last();
  if ((await positioner.count()) > 0) {
    await waitForPositioned(positioner);
  }
}

/** Moves the real mouse onto a point of the element's visible part (the part inside `within`), arriving with a movement. */
async function moveOver(page: Page, target: Locator, within?: Locator): Promise<void> {
  await positioned(target);
  const box = (await target.boundingBox())!;
  let top = box.y;
  let bottom = box.y + box.height;
  if (within) {
    const clip = (await within.boundingBox())!;
    top = Math.max(top, clip.y);
    bottom = Math.min(bottom, clip.y + clip.height);
  }
  const x = box.x + box.width / 2;
  const y = (top + bottom) / 2;
  await page.mouse.move(x - 6, y);
  await page.mouse.move(x, y);
}

const verticalCenter = (box: { y: number; height: number }) => box.y + box.height / 2;

test.describe('dynamic items', () => {
  // SEL-EDGE-10, SEL-KEY-01.
  test('skips null items when navigating', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-dynamic', { case: 'null-items' });
    await focusTrigger(page, fixture);
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('listbox')).toBeVisible();
    // Three items leave the list in a 0 ms task after the open; the keys below run on the list that is left.
    await expect(fixture).toHaveAttribute('data-filtered', 'true');
    await expect(page.getByRole('option')).toHaveCount(3);
    await expect(option(page, 'Add to Library')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown'); // Share
    await page.keyboard.press('ArrowDown'); // Share still

    await expect(option(page, 'Share')).toBeFocused();
  });

  // SEL-EDGE-10, SEL-STA-12 (L2).
  test('unselects the selected item if removed', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-dynamic', { case: 'remove' });
    await focusTrigger(page, fixture);
    await page.keyboard.press('ArrowDown');

    await expect(option(page, 'a')).toHaveAttribute('data-selected', '');
    await expect(page.getByTestId('value')).toHaveText('a');

    await pressControl(fixture.locator('[data-remove]'));
    await expect(option(page, 'b')).not.toHaveAttribute('data-selected');
    await expect(option(page, 'a')).toHaveCount(0);
    // Base UI's `fireEvent.click` flushes the reconciliation of the removed value before the next line; on Server it follows
    // the registry's publication by a round trip (SEL-STA-12, relaxed), so the value is waited for before the item returns.
    await expect(page.getByTestId('value')).toHaveText('');

    await pressControl(fixture.locator('[data-add]'));
    await expect(option(page, 'a')).toBeVisible();
    await expect(option(page, 'a')).not.toHaveAttribute('data-selected');
  });

  /** The body the three "resets aligned positioning ..." titles share. */
  async function resetsAlignedPositioning(page: Page, mode: 'server' | 'wasm', caseName: string, button: string, opensItself: boolean) {
    await gotoPopupFixture(page, mode, 'select-nav-dynamic', { case: caseName });
    const dropdown = trigger(page);

    await dropdown.click();
    await option(page, 'a-35').click();
    await expect(dropdown).toHaveAttribute('aria-expanded', 'false');
    await page.getByTestId(button).click();
    if (!opensItself) {
      await dropdown.click();
    }

    const listbox = page.getByRole('listbox');
    await expect(listbox).toBeVisible();
    const positioner = page.getByTestId('positioner');
    const firstOption = option(page, 'b-0');
    await expect(dropdown).toHaveAttribute('aria-expanded', 'true');
    await expect.poll(() => listbox.evaluate((node) => node.scrollTop)).toBe(0);
    await expect
      .poll(async () => (await firstOption.boundingBox())!.y - (await positioner.boundingBox())!.y)
      .toBeGreaterThanOrEqual(0);
  }

  // SEL-POS-07, SEL-POS-13.
  test('resets aligned positioning after controlled value reset and option replacement', async ({ page, mode }) => {
    await resetsAlignedPositioning(page, mode, 'replace-options', 'replace-options', false);
  });

  // SEL-POS-09.
  test('resets aligned positioning when value reset, option replacement, and controlled open happen together', async ({ page, mode }) => {
    await resetsAlignedPositioning(page, mode, 'replace-open', 'replace-options-and-open', true);
  });

  // SEL-POS-09 (the title has no row of its own in the map: the same placement with the value left stale).
  test('resets aligned positioning when a stale controlled value, option replacement, and controlled open happen together', async ({ page, mode }) => {
    await resetsAlignedPositioning(page, mode, 'stale-open', 'replace-options-and-open', true);
  });
});

test.describe('typeahead', () => {
  // SEL-API-42, SEL-KEY-04, SEL-KEY-12.
  test('matches an item by its label when the rendered text differs', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'label' });
    await focusTrigger(page, fixture);
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'Option 1')).toBeFocused();

    // Only the label matches; the rendered text is "Option 2".
    await page.keyboard.type('fr');

    await expect(option(page, 'Option 2')).toBeFocused();
  });

  // SEL-FOC-11, SEL-KEY-04.
  test('starts a new search after focus leaves the select', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'focus-leaves' });
    await focusTriggerMounted(page, fixture, 3);
    await page.keyboard.press('b');
    await expect(page.getByTestId('value')).toHaveText('banana');

    // Leaving and returning within the typeahead timeout must not continue the "b" search.
    await page.evaluate(() => {
      (document.querySelector('[data-testid="outside"]') as HTMLElement).focus();
      (document.querySelector('[data-testid="trigger"]') as HTMLElement).focus();
    });
    await page.keyboard.press('a');

    await expect(page.getByTestId('value')).toHaveText('avocado');
  });

  // SEL-KEY-04, SEL-KEY-09, SEL-STA-32.
  test('does not trigger selection when Space is pressed during text navigation', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'space-text' });
    const options = page.getByRole('option');
    await expect(options).toHaveCount(2);
    await options.first().focus();

    await page.keyboard.type('Item T');

    await expect(options.nth(1)).toBeFocused();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-item-clicks', '');
    await expect(fixture).toHaveAttribute('data-value-changes', '');
  });

  // SEL-KEY-04.
  test('skips disabled items and commits the next match via typeahead on a closed trigger', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'disabled-skip' });
    await focusTriggerMounted(page, fixture, 2);

    // "apricot" and "avocado" both start with "a", but "apricot" is disabled: one "a" lands on "avocado".
    await page.keyboard.press('a');

    await expect(page.getByTestId('value')).toHaveText('avocado');
  });

  // SEL-FOC-11, SEL-KEY-04.
  test('commits typeahead on a closed trigger when items are provided', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'items-provided' });
    await focusTriggerMounted(page, fixture, 2);

    // The popup is never opened; the trigger's own focus force-mount registers the list.
    await page.keyboard.press('c');

    await expect(page.getByTestId('value')).toHaveText('Cherry');
  });

  // SEL-EDGE-07, SEL-KEY-04.
  test('commits nothing when the only typeahead match is disabled (closed trigger)', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'only-disabled' });
    await focusTriggerMounted(page, fixture, 2);

    // "banana" is the only "b" item and it is disabled, so there is no selectable match.
    await page.keyboard.press('b');

    await settle(fixture);
    await expect(page.getByTestId('value')).toHaveText('');
    await expect(fixture).toHaveAttribute('data-value-changes', '');
  });

  // SEL-EDGE-07, SEL-KEY-04.
  test('does not let a disabled double-letter item block rapid cycling among enabled matches', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'double-letter' });
    await focusTriggerMounted(page, fixture, 3);

    // The disabled "aaron" must not count toward the same-letter guard: "a" twice cycles apple -> avocado.
    await page.keyboard.press('a');
    await expect(page.getByTestId('value')).toHaveText('apple');
    await page.keyboard.press('a');
    await expect(page.getByTestId('value')).toHaveText('avocado');
  });

  // SEL-KEY-04, SEL-KEY-12.
  test('skips disabled items when highlighting via typeahead on an open popup', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'open-disabled' });
    const apricot = option(page, 'apricot');
    const avocado = option(page, 'avocado');
    await expect(avocado).toBeVisible();
    await avocado.focus();

    // Open-state typeahead highlights; "a" must skip the disabled "apricot" and highlight "avocado".
    await page.keyboard.press('a');

    await expect(avocado).toHaveAttribute('data-highlighted', '');
    await expect(apricot).not.toHaveAttribute('data-highlighted');
  });

  // SEL-EDGE-07, SEL-KEY-04.
  test('starts from the first match after value reset (closed)', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'reset-closed' });
    await focusTriggerMounted(page, fixture, 2);
    await page.keyboard.press('a');
    await expect(page.getByTestId('value')).toHaveText('a1');

    await page.getByTestId('reset').click();
    await expect(page.getByTestId('value')).toHaveText('');

    await trigger(page).focus();
    await page.keyboard.press('a');
    await expect(page.getByTestId('value')).toHaveText('a1');
  });

  // SEL-KEY-04, SEL-STA-04 (L2).
  test('does not jump matches after a closed-state value reset', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-typeahead', { case: 'jump-reset' });
    await expect(page.getByTestId('value')).toHaveText('dog');

    await page.getByTestId('set-car').click();
    await expect(page.getByTestId('value')).toHaveText('car');

    await focusTriggerMounted(page, fixture, 3);
    await page.keyboard.press('c');
    await expect(page.getByTestId('value')).toHaveText('cat');

    await page.keyboard.press('a');
    await expect(page.getByTestId('value')).toHaveText('cat');
  });
});

test.describe('prop: multiple', () => {
  // SEL-PTR-14, SEL-STA-07.
  test('keeps the active index on a deselected item in multiple mode', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-multiple', { case: 'deselect-active' });
    await trigger(page).click();
    const optionB = option(page, 'b');
    await expect(optionB).toBeVisible();

    await optionB.click();
    await expect(optionB).toHaveAttribute('data-highlighted', '');

    await optionB.click();
    await expect(optionB).toHaveAttribute('data-highlighted', '');
  });

  // SEL-DOM-26, SEL-POS-07.
  test('aligns the first selected item with the trigger when alignItemWithTrigger is active', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-multiple', { case: 'aligned' });
    await trigger(page).click();

    const anchorItem = option(page, 'opt-10');
    await expect(anchorItem).toHaveAttribute('data-highlighted', '');
    await expect(page.getByTestId('popup')).toHaveAttribute('data-side', 'none');

    await expect
      .poll(async () => Math.abs(verticalCenter((await anchorItem.boundingBox())!) - verticalCenter((await trigger(page).boundingBox())!)))
      .toBeLessThanOrEqual(1);
  });

  // SEL-FOC-01, SEL-KEY-01.
  test('anchors to the first selected item when opened with the keyboard', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-multiple', { case: 'anchor-keyboard' });
    await page.keyboard.press('Tab');
    await expect(trigger(page)).toBeFocused();
    await settled(page, fixture);
    await page.keyboard.press('ArrowDown');

    await expect(option(page, 'b')).toHaveAttribute('data-highlighted', '');
    await expect(option(page, 'c')).not.toHaveAttribute('data-highlighted');
  });

  // SEL-KEY-06.
  test('continues arrow navigation from the anchor rather than the top of the list', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-multiple', { case: 'anchor-continue' });
    await trigger(page).click();
    const optionB = option(page, 'b');
    await expect(optionB).toHaveAttribute('data-highlighted', '');
    // Focus reaches the anchor asynchronously. Keys sent before it lands are lost.
    await expect(optionB).toBeFocused();

    // The next item after the anchor, not `a` at the top.
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'c')).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('ArrowUp');
    await expect(optionB).toHaveAttribute('data-highlighted', '');
  });

  // SEL-KEY-08, SEL-KEY-13, SEL-STA-07.
  test('keeps the highlight on an item deselected with the keyboard', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-multiple', { case: 'deselect-keyboard' });
    await trigger(page).click();
    const optionB = option(page, 'b');
    await expect(optionB).toHaveAttribute('data-highlighted', '');
    await expect(optionB).toBeFocused();

    await page.keyboard.press('Enter');

    await expect(optionB).toHaveAttribute('aria-selected', 'false');
    await expect(optionB).toHaveAttribute('data-highlighted', '');
  });

  // SEL-FOC-03, SEL-POS-24. Base UI spies on `scrollIntoView`; the spy is added before the page loads and records the text of
  // each element it is called on.
  test('scrolls the first selected item into view on open', async ({ page, mode }) => {
    await page.addInitScript(() => {
      const calls: string[] = [];
      (window as unknown as { scrolledIntoView: string[] }).scrolledIntoView = calls;
      const original = HTMLElement.prototype.scrollIntoView;
      HTMLElement.prototype.scrollIntoView = function (this: HTMLElement, ...args: Parameters<typeof original>) {
        calls.push(this.textContent ?? '');
        return original.apply(this, args);
      };
    });
    await gotoPopupFixture(page, mode, 'select-nav-multiple', { case: 'scroll-into-view' });
    await page.evaluate(() => ((window as unknown as { scrolledIntoView: string[] }).scrolledIntoView.length = 0));

    await trigger(page).click();

    const selectedItem = option(page, 'item 80');
    await expect(selectedItem).toHaveAttribute('data-highlighted', '');
    await expect
      .poll(() => page.evaluate(() => (window as unknown as { scrolledIntoView: string[] }).scrolledIntoView))
      .toContain('item 80');
  });
});

test.describe('prop: highlightItemOnHover', () => {
  // SEL-API-15, SEL-PTR-13.
  test('highlights an item on mouse move by default', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-hover', { case: 'default' });
    const optionB = option(page, 'b');
    await expect(optionB).toBeVisible();
    await positioned(optionB);

    await optionB.hover();

    await expect(optionB).toHaveAttribute('data-highlighted', '');
  });

  // SEL-API-15, SEL-POS-10. Base UI fires `pointerdown` (mouse) and `mousedown` on the trigger and never releases; the real
  // press is a mouse down with no release, since a release would land on the aligned popup's first item and select it.
  test('highlights the first item after opening when alignItemWithTrigger is active and no value is selected', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-hover', { case: 'aligned-first' });
    const box = (await trigger(page).boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();

    const optionA = option(page, 'a');
    await expect(optionA).toBeVisible();
    await expect(optionA).toHaveAttribute('data-highlighted', '');
  });

  // SEL-API-15, SEL-PTR-13.
  test('does not highlight items from mouse movement when disabled', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-hover', { case: 'disabled' });
    const optionB = option(page, 'b');
    await expect(optionB).toBeVisible();
    await positioned(optionB);

    await optionB.hover();

    await settled(page, fixture);
    await expect(optionB).not.toHaveAttribute('data-highlighted');
  });

  // SEL-API-15, SEL-FOC-02, SEL-PTR-13.
  test('does not remove highlight when mousing out of popup when disabled', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-hover', { case: 'disabled' });
    const optionA = option(page, 'a');
    await positioned(optionA);
    await optionA.hover();

    const popup = page.getByRole('listbox');
    await expect(popup).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(optionA).toHaveAttribute('data-highlighted', '');

    // `user.unhover(popup)`: the pointer leaves the popup.
    await page.mouse.move(2, 2);

    await settled(page, fixture);
    await expect(optionA).toHaveAttribute('data-highlighted', '');
  });
});

test.describe('groups', () => {
  // SEL-KEY-06, SEL-KEY-07.
  test('navigates across groups without stopping on labels or separators', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-groups');
    await focusTrigger(page, fixture);
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'apple')).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'carrot')).toBeFocused();

    await page.keyboard.press('End');
    await expect(option(page, 'leek')).toBeFocused();

    await page.keyboard.press('Home');
    await expect(option(page, 'apple')).toBeFocused();
  });
});

test.describe('item-aligned popup interaction', () => {
  async function geometry(popup: Locator) {
    return popup.evaluate((node) => {
      const rect = node.getBoundingClientRect();
      return { top: rect.top, height: rect.height, scrollTop: node.scrollTop };
    });
  }

  // SEL-POS-11. Playwright's hover would scroll a partly visible item into view where Base UI's does not, so the pointer is
  // moved onto the part of the item that shows.
  test('does not move the popup or its list when hovering items at its edges', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-nav-aligned', { case: 'edges' });
    await trigger(page).click();
    await expect(option(page, 'Item 15')).toBeFocused();
    await positioned(option(page, 'Item 15'));
    await frames(page);
    const popup = page.getByTestId('popup');
    const before = await geometry(popup);

    const [topEdge, bottomEdge] = await page.evaluate(() => {
      const popupRect = document.querySelector('[data-testid="popup"]')!.getBoundingClientRect();
      const options = [...document.querySelectorAll('[role="option"]')];
      const top = options.find((item) => item.getBoundingClientRect().bottom > popupRect.top)!;
      const bottom = options.filter((item) => item.getBoundingClientRect().top < popupRect.bottom).at(-1)!;
      return [top.textContent, bottom.textContent];
    });

    for (const name of [bottomEdge, topEdge] as string[]) {
      const item = option(page, name);
      await moveOver(page, item, popup);
      await expect(item).toHaveAttribute('data-highlighted', '');
      await frames(page);
      expect(await geometry(popup)).toEqual(before);
    }
  });

  // SEL-POS-11.
  test('keeps the highlighted item visible while keyboard navigation grows the popup', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-aligned', { case: 'grow' });
    await focusTrigger(page, fixture);
    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'Item 1')).toBeFocused();
    const positioner = page.getByTestId('positioner');
    await waitForPositioned(positioner);
    await frames(page);
    const initialHeight = (await positioner.boundingBox())!.height;

    const isInsidePopup = async (name: string) => {
      const popupRect = (await page.getByTestId('popup').boundingBox())!;
      const itemRect = (await option(page, name).boundingBox())!;
      return itemRect.y >= popupRect.y - 1 && itemRect.y + itemRect.height <= popupRect.y + popupRect.height + 1;
    };

    const navigate = async (from: number, to: number) => {
      for (let step = from; step < to; step += 1) {
        await page.keyboard.press('ArrowDown');
      }
      await expect(option(page, `Item ${to}`)).toBeFocused();
      await frames(page);
    };

    // While there is room to grow, the popup grows instead of scrolling its first items away.
    await navigate(1, 9);
    expect((await positioner.boundingBox())!.height).toBeGreaterThan(initialHeight);
    expect(await isInsidePopup('Item 0')).toBe(true);
    expect(await isInsidePopup('Item 9')).toBe(true);

    await navigate(9, 25);
    expect((await positioner.boundingBox())!.height).toBeGreaterThan(initialHeight + 100);
    expect(await isInsidePopup('Item 25')).toBe(true);
  });
});

test.describe('opening', () => {
  // SEL-DOM-25 (L2), SEL-FOC-02, SEL-KEY-02. A selected item claims the keyboard opening, so the ArrowUp is never consumed.
  test('does not carry an ArrowUp opening over to a later pointer opening', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-opening');
    await focusTrigger(page, fixture);
    await page.keyboard.press('ArrowUp');
    await expect(option(page, 'b')).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(trigger(page)).toHaveAttribute('aria-expanded', 'false');

    await page.getByTestId('clear').click();
    await trigger(page).click();
    await expect(page.getByRole('listbox')).toBeFocused();
    // Base UI waits 50 ms for a stale highlight to land; here the barrier and two frames pass.
    await settled(page, fixture);

    await expect(option(page, 'c')).not.toHaveAttribute('data-highlighted');
  });
});

test.describe('closing', () => {
  // SEL-ANI-01, SEL-ANI-14, SEL-DOM-26, SEL-PTR-13, SEL-STA-07. Base UI moves the pointer with `fireEvent.mouseMove` on item
  // `a`; here the real mouse moves onto it.
  test('keeps the highlight on the item while the popup animates out', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-nav-closing', { animations: 1 });
    await focusTrigger(page, fixture);
    await page.keyboard.press('ArrowDown');
    const optionA = option(page, 'a');
    const optionB = option(page, 'b');
    await expect(optionA).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(optionB).toBeFocused();

    await page.keyboard.press('Escape');

    await expect(page.getByTestId('popup')).toHaveAttribute('data-ending-style', '');
    await expect(optionB).toHaveAttribute('data-highlighted', '');

    // Items moving under the pointer while the popup fades out don't take the highlight.
    await moveOver(page, optionA);
    await settled(page, fixture);
    await expect(optionB).toHaveAttribute('data-highlighted', '');
    await expect(optionA).not.toHaveAttribute('data-highlighted');
  });
});

test.describe('Select.Positioner: kept-mounted positioner', () => {
  // SEL-ANI-02, SEL-POS-02. Rendering the full-size popup at coordinates computed for the hidden positioner can overflow the
  // layout viewport on the next open; the positioner must sit at the viewport origin until positioned.
  test('does not retain stale coordinates while closed', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-positioner', { case: 'kept-mounted' });
    const positioner = page.getByTestId('positioner');
    await expect(positioner).toBeVisible();
    await expect.poll(() => positioner.evaluate((node) => (node as HTMLElement).style.transform)).not.toBe('');

    await page.keyboard.press('Escape');

    // The portal can remount around the close, so the kept-mounted node is queried again each time.
    await expect(page.getByTestId('positioner')).toBeHidden();
    await expect(page.getByTestId('positioner')).toHaveAttribute('hidden', /.*/);
    const closed = page.getByTestId('positioner');
    await expect.poll(() => closed.evaluate((node) => (node as HTMLElement).style.position)).toBe('fixed');
    expect(await closed.evaluate((node) => (node as HTMLElement).style.transform)).toBe('');
    expect(await closed.evaluate((node) => (node as HTMLElement).style.top)).toBe('0px');
    expect(await closed.evaluate((node) => (node as HTMLElement).style.left)).toBe('0px');
  });
});

test.describe('Select.ScrollArrow', () => {
  // SEL-DOM-44, SEL-PTR-16 (L3). Base UI stubs the scroller's geometry; here the popup is its own scroller on real layout (a
  // 200 px popup over 400 px of content with no registered items), scrolled to 100 px, and the real mouse hovers the arrow.
  for (const direction of ['up', 'down'] as const) {
    test(`reflects scrollability on the ${direction} arrow when the popup has no registered items`, async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-nav-arrow');
      const popup = page.getByTestId('popup');
      await expect(popup).toBeVisible();
      // The arrows' handles are attached once the down arrow shows (the content overflows below); a hover before that starts nothing.
      await expect(page.getByTestId('down')).toHaveAttribute('data-visible', '');
      await stable(page, popup);
      await popup.evaluate((node) => (node.scrollTop = 100));
      const arrow = page.getByTestId(direction);

      await moveOver(page, arrow);
      await page.clock.runFor(40);

      // Mid-scroller with nothing to step through: the arrow still reports that more content lies beyond it.
      await expect(arrow).toHaveAttribute('data-visible', '');
    });
  }
});
