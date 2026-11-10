import { type Locator, type Page } from '@playwright/test';
import { expect, test, waitForPositioned, type RenderMode } from '../helpers';
import { gotoPopupFixture, settle as barrier, type PopupFixtureOptions } from './popupFixture';

// Base UI's popupListConformanceTests (packages/react/test/popupListConformanceTests.tsx at v1.9.0), titles verbatim, as a
// Playwright helper (M5 track 1, issue 159, Deliverable 13; the M4 suites' convention). A family's spec calls it with its
// fixture, whose page inherits PopupSuiteFixtureBase: the suite's `createComponent` props and root props become the
// fixture's query (tests/browser/CONTEXT.md):
// - `items` (`|`-separated labels; absent for the suite's twelve, empty for none), `disabledItems` (`|`-separated),
//   `itemStyle` and `scrollerStyle` (the suite's CSS), `onItemClick` (the items' click handler records each activated
//   item's text as the container's `data-item-clicks`, `|`-separated);
// - `role` (the config's `itemRole`), `selectable` (the root's `onValueChange` records `data-value-changes`,
//   `|`-separated), `defaultValue`, `modal`, and `wrap` (`form`: a form with a submit button whose submits are counted as
//   `data-submits`; `dialog`: inside an open Dialog's popup, `data-testid="dialog"`);
// - the After button's clicks as `data-outside-clicks`.
// The family's own list options (Menu's loop and typeahead, Select's, Combobox's escape) are the config's `query`.
// `it.skipIf(isJSDOM)` titles run here; `user.*` is Playwright's real input, except Base UI's own `fireEvent` calls (the
// touch scroll's pointer events and the navigation keys' default), which dispatch in the page as Base UI does. A title the
// host cannot run yet is listed in `open` with its row and the track that proves it; it is skipped with that reason.

const ITEMS = Array.from({ length: 12 }, (_, index) => `Item ${index}`);
const ITEM_HEIGHT = 30;
const SCROLLER_HEIGHT = 100;
const SCROLLER_STYLE = `height: ${SCROLLER_HEIGHT}px; max-height: none; overflow-y: auto; padding: 0`;
const ITEM_STYLE = `height: ${ITEM_HEIGHT}px; box-sizing: border-box; margin: 0`;

/** The options of popupListConformanceTests (Base UI's PopupListTestConfig, the rendering half replaced by a fixture). */
export interface PopupListConformanceConfig {
  /** The fixture page (see the header for its query contract). */
  fixture: string;
  /** The family's list options the fixture takes, sent with every page. */
  query?: PopupFixtureOptions;
  itemRole: 'option' | 'menuitem';
  focusModel?: 'dom' | 'virtual';
  homeEnd?: boolean;
  selectable?: boolean;
  spaceActivates?: boolean;
  enterSpaceOpen?: boolean;
  typeahead?: boolean;
  modal?: boolean;
  triggerClickCloses?: boolean;
  listEnd: 'wrap' | 'stop' | 'escape';
  /** Titles listed open, each with its row and the track or milestone that proves it. */
  open?: Readonly<Record<string, string>>;
}

interface RenderListOptions {
  root?: { defaultValue?: string };
  items?: readonly string[];
  disabledItems?: readonly string[];
  onItemClick?: boolean;
  wrap?: 'form' | 'dialog';
}

interface Rendered {
  page: Page;
  fixture: Locator;
}

const KEYS: Record<string, string> = { '{Enter}': 'Enter', ' ': 'Space', '{ArrowDown}': 'ArrowDown', '{ArrowUp}': 'ArrowUp' };

export function popupListConformanceTests(config: PopupListConformanceConfig): void {
  const {
    fixture: fixtureName,
    query = {},
    itemRole,
    focusModel = 'dom',
    homeEnd = true,
    selectable = false,
    spaceActivates = true,
    enterSpaceOpen = true,
    typeahead = true,
    modal = true,
    triggerClickCloses = true,
    listEnd,
    open: listedOpen = {},
  } = config;

  async function renderList(page: Page, mode: RenderMode, options: RenderListOptions = {}): Promise<Rendered> {
    const fixture = await gotoPopupFixture(page, mode, fixtureName, {
      ...query,
      role: itemRole,
      selectable: selectable || undefined,
      modal,
      itemStyle: ITEM_STYLE,
      scrollerStyle: SCROLLER_STYLE,
      items: options.items?.join('|'),
      disabledItems: options.disabledItems?.join('|'),
      onItemClick: options.onItemClick || undefined,
      defaultValue: options.root?.defaultValue,
      wrap: options.wrap,
    });
    return { page, fixture };
  }

  const getTrigger = (page: Page) => page.locator('[data-testid="trigger"]');
  const getItem = (page: Page, index: number) => page.getByRole(itemRole, { name: ITEMS[index]!, exact: true, includeHidden: true });

  function highlightedIndex(page: Page): Promise<number> {
    return page.evaluate(
      ({ role, items }) => {
        const highlighted = Array.from(document.querySelectorAll(`[role="${role}"]`)).filter((item) => item.hasAttribute('data-highlighted'));
        if (highlighted.length > 1) {
          throw new Error(`Popup list conformance: ${highlighted.length} items are highlighted.`);
        }
        return highlighted.length ? items.indexOf(highlighted[0]!.textContent ?? '') : -1;
      },
      { role: itemRole, items: ITEMS },
    );
  }

  function highlightedText(page: Page): Promise<string | undefined> {
    return page.evaluate(
      (role) => Array.from(document.querySelectorAll(`[role="${role}"]`)).find((item) => item.hasAttribute('data-highlighted'))?.textContent ?? undefined,
      itemRole,
    );
  }

  const expectHighlight = (page: Page, index: number) => expect.poll(() => highlightedIndex(page)).toBe(index);

  function focusOwner(page: Page): Promise<string> {
    return page.evaluate((role) => {
      const active = document.activeElement;
      if (active === document.querySelector('[data-testid="trigger"]')) {
        return 'trigger';
      }
      if (Array.from(document.querySelectorAll(`[role="${role}"]`)).some((item) => item.contains(active))) {
        return 'item';
      }
      if (document.querySelector('[data-testid="popup"]')?.contains(active)) {
        return 'popup';
      }
      return 'outside';
    }, itemRole);
  }

  function isFullyVisible(page: Page, index: number): Promise<boolean> {
    return page.evaluate(
      ({ role, label }) => {
        const item = Array.from(document.querySelectorAll(`[role="${role}"]`)).find((element) => element.textContent === label)!;
        const itemRect = item.getBoundingClientRect();
        const scrollerRect = document.querySelector('[data-testid="scroller"]')!.getBoundingClientRect();
        return itemRect.top >= scrollerRect.top - 1 && itemRect.bottom <= scrollerRect.bottom + 1;
      },
      { role: itemRole, label: ITEMS[index]! },
    );
  }

  const scrollTop = (page: Page) => page.locator('[data-testid="scroller"]').evaluate((scroller) => scroller.scrollTop);

  // Base UI's settle: two frames after the events so far. The barrier first, so every event before has reached C#.
  async function settle({ page, fixture }: Rendered): Promise<void> {
    await barrier(fixture);
    await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  }

  async function waitForOpen(rendered: Rendered): Promise<void> {
    await expect(getTrigger(rendered.page)).toHaveAttribute('aria-expanded', 'true');
    await expect(rendered.page.getByRole(itemRole, { includeHidden: true }).first()).toBeVisible();
    await settle(rendered);
  }

  async function waitForClosed(page: Page): Promise<void> {
    await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'false');
  }

  async function focusTrigger(page: Page): Promise<void> {
    await getTrigger(page).focus();
  }

  async function openWithMouse(rendered: Rendered): Promise<void> {
    await getTrigger(rendered.page).click();
    await waitForOpen(rendered);
  }

  async function openWithKeyboard(rendered: Rendered): Promise<number> {
    await focusTrigger(rendered.page);
    await rendered.page.keyboard.press('ArrowDown');
    await waitForOpen(rendered);
    await expect.poll(() => highlightedIndex(rendered.page)).not.toBe(-1);
    return highlightedIndex(rendered.page);
  }

  // Playwright's hover scrolls an item into view; Base UI's moves no scroll, so a partly visible item is hovered where
  // it shows.
  async function hoverVisiblePart(page: Page, index: number): Promise<void> {
    // A positioned instance (Menu's) reads the item's box only after the Positioner's first pass; the composite-list hosts
    // have no Positioner.
    // The Positioner is the scroller's outermost ancestor carrying `data-side`: a Menu popup that holds a List also renders
    // `role="presentation"` and `data-side`, so a role-and-attribute match finds two elements (2026-11-10, M5 track 6).
    const positioner = page.getByTestId('scroller').locator('xpath=ancestor::*[@data-side][last()]');
    if ((await positioner.count()) > 0) {
      await waitForPositioned(positioner);
    }

    const point = await page.evaluate(
      ({ role, label }) => {
        const item = Array.from(document.querySelectorAll(`[role="${role}"]`)).find((element) => element.textContent === label)!;
        const itemRect = item.getBoundingClientRect();
        const scrollerRect = document.querySelector('[data-testid="scroller"]')!.getBoundingClientRect();
        const top = Math.max(itemRect.top, scrollerRect.top);
        const bottom = Math.min(itemRect.bottom, scrollerRect.bottom);
        return { x: itemRect.left + itemRect.width / 2, y: (top + bottom) / 2 };
      },
      { role: itemRole, label: ITEMS[index]! },
    );
    await page.mouse.move(point.x, point.y);
  }

  /** Real focus moves to the highlighted item, or stays on the trigger and points at it. */
  async function expectFocusOnItem(page: Page, index: number): Promise<void> {
    const trigger = getTrigger(page);
    if (focusModel === 'virtual') {
      const id = await getItem(page, index).getAttribute('id');
      await expect(trigger).toHaveAttribute('aria-activedescendant', id!);
      await expect(trigger).toBeFocused();
    } else {
      await expect(getItem(page, index)).toBeFocused();
    }
  }

  /** Nothing is highlighted, and no element is announced as the active descendant either. */
  async function expectNoHighlight(page: Page): Promise<void> {
    await expectHighlight(page, -1);
    if (focusModel === 'virtual') {
      await expect(getTrigger(page)).not.toHaveAttribute('aria-activedescendant');
    }
  }

  /** Where the highlight lands after moving past an end of a list of `count` items. */
  function pastEnd(edge: 'first' | 'last', count: number): number {
    if (listEnd === 'escape') {
      return -1;
    }
    if (listEnd === 'wrap') {
      return edge === 'first' ? count - 1 : 0;
    }
    return edge === 'first' ? 0 : count - 1;
  }

  async function navigateTo(page: Page, from: number, to: number): Promise<void> {
    const key = to > from ? 'ArrowDown' : 'ArrowUp';
    for (let i = 0; i < Math.abs(to - from); i += 1) {
      await page.keyboard.press(key);
    }
    await expectHighlight(page, to);
  }

  const spyOptions: RenderListOptions = { onItemClick: true };

  async function expectActivated(fixture: Locator, labels: string[]): Promise<void> {
    await expect(fixture).toHaveAttribute('data-item-clicks', labels.join('|'));
    // `onValueChange` is only passed to selectable lists.
    await expect(fixture).toHaveAttribute('data-value-changes', selectable ? labels.join('|') : '');
  }

  async function expectNothingCommitted(rendered: Rendered): Promise<void> {
    await barrier(rendered.fixture);
    await expect(rendered.fixture).toHaveAttribute('data-item-clicks', '');
    await expect(rendered.fixture).toHaveAttribute('data-value-changes', '');
  }

  /** Declares a title; one listed open is skipped with its reason. */
  function it(title: string, body: (args: { page: Page; mode: RenderMode }) => Promise<void>): void {
    test(title, async ({ page, mode }) => {
      test.skip(title in listedOpen, listedOpen[title]);
      await body({ page, mode });
    });
  }

  test.describe('Popup list conformance', () => {
    test.describe('pointer', () => {
      it('highlights and focuses the hovered item', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await openWithMouse(rendered);

        await getItem(page, 2).hover();

        await expectHighlight(page, 2);
        await expectFocusOnItem(page, 2);
      });

      it('clears the highlight when the pointer leaves the list without leaving focus on an item', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, spyOptions);
        await openWithMouse(rendered);

        await getItem(page, 1).hover();
        await expectHighlight(page, 1);

        // A modal popup's backdrop covers the button; the pointer still moves over its position.
        await page.getByTestId('after').hover({ force: true });

        await expectNoHighlight(page);
        // A focused item that no longer looks highlighted would still respond to Enter.
        await expect.poll(() => focusOwner(page)).toBe(focusModel === 'virtual' ? 'trigger' : 'popup');
        await page.keyboard.press('Enter');
        await settle(rendered);
        await expectNothingCommitted(rendered);
      });

      it('continues keyboard navigation from the hovered item', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await openWithMouse(rendered);
        await getItem(page, 4).hover();
        await expectHighlight(page, 4);

        await page.keyboard.press('ArrowDown');

        await expectHighlight(page, 5);
        await expectFocusOnItem(page, 5);
      });

      it('activates the hovered item with Enter', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, spyOptions);
        await openWithMouse(rendered);
        await getItem(page, 6).hover();
        await expectFocusOnItem(page, 6);

        await page.keyboard.press('Enter');

        await waitForClosed(page);
        await expectActivated(rendered.fixture, [ITEMS[6]!]);
      });

      it('lets the pointer take the highlight back after keyboard navigation', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        const start = await openWithKeyboard(rendered);
        await navigateTo(page, start, 3);

        await getItem(page, 1).hover();

        await expectHighlight(page, 1);
        await expectFocusOnItem(page, 1);
      });

      it('keeps the highlight when a touch scroll starts on the highlighted item', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await openWithMouse(rendered);
        await getItem(page, 2).hover();
        await expectHighlight(page, 2);

        // The browser takes the touch over for scrolling, cancels the pointer and it leaves.
        await getItem(page, 2).evaluate((touched) => {
          const after = document.querySelector('[data-testid="after"]');
          touched.dispatchEvent(new PointerEvent('pointerdown', { pointerType: 'touch', bubbles: true, cancelable: true }));
          touched.dispatchEvent(new PointerEvent('pointercancel', { pointerType: 'touch', bubbles: true }));
          touched.dispatchEvent(new PointerEvent('pointerleave', { pointerType: 'touch', relatedTarget: after }));
        });
        await settle(rendered);

        expect(await highlightedIndex(page)).toBe(2);
      });

      it('does not activate a disabled item', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { disabledItems: [ITEMS[1]!], onItemClick: true });
        await openWithMouse(rendered);
        await expect(getItem(page, 1)).toHaveAttribute('aria-disabled', 'true');

        await getItem(page, 1).hover();
        await page.keyboard.press('Enter');
        await settle(rendered);

        await expectNothingCommitted(rendered);

        // `force`: Playwright waits for an `aria-disabled` element to be enabled; Base UI's click does not.
        await getItem(page, 1).click({ force: true });
        await settle(rendered);

        await expectNothingCommitted(rendered);
        await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'true');
        await expect(getItem(page, 1)).not.toHaveAttribute('data-selected');

        // An enabled item must reach the same callbacks, so disconnected handlers cannot pass.
        await getItem(page, 2).click();
        await expectActivated(rendered.fixture, [ITEMS[2]!]);
      });

      it('ignores right clicks on the trigger and on items', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, spyOptions);

        await getTrigger(page).click({ button: 'right' });
        await settle(rendered);
        await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'false');

        await openWithMouse(rendered);
        await getItem(page, 2).click({ button: 'right' });
        await settle(rendered);

        await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'true');
        await expectNothingCommitted(rendered);
      });

      it('keeps the keyboard highlight while the list scrolls under a resting pointer', async ({ page, mode }) => {
        // Chromium fires boundary events (but no `mousemove`) as items move under a still pointer, and WebKit fires a
        // zero-movement `mousemove`. Neither is the user pointing.
        const rendered = await renderList(page, mode);
        await openWithMouse(rendered);

        await getItem(page, 1).hover();
        await expectHighlight(page, 1);

        await navigateTo(page, 1, 9);
        await settle(rendered);

        expect(await highlightedIndex(page)).toBe(9);
      });

      it('does not scroll the list when hovering a partially visible item at either edge', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await openWithMouse(rendered);
        expect(await scrollTop(page)).toBe(0);

        // Item 3 spans 90-120px, so only its top 10px are visible.
        await hoverVisiblePart(page, 3);
        await expectHighlight(page, 3);
        await settle(rendered);
        expect(await scrollTop(page)).toBe(0);

        // Item 1 spans 30-60px, so only its bottom 15px are visible.
        await page.locator('[data-testid="scroller"]').evaluate(async (scroller) => {
          scroller.scrollTop = 45;
          await new Promise((resolve) => requestAnimationFrame(resolve));
        });
        await hoverVisiblePart(page, 1);
        await expectHighlight(page, 1);
        await settle(rendered);
        expect(await scrollTop(page)).toBe(45);
      });
    });

    test.describe('opening', () => {
      it('starts without a highlight after a pointer opening', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await openWithMouse(rendered);

        await expectNoHighlight(page);
        expect(await focusOwner(page)).toBe(focusModel === 'virtual' ? 'trigger' : 'popup');

        await page.keyboard.press('ArrowDown');
        await expectHighlight(page, 0);
        await expectFocusOnItem(page, 0);
      });

      it('opens on the first item with ArrowDown', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await focusTrigger(page);

        await page.keyboard.press('ArrowDown');
        await waitForOpen(rendered);

        await expectHighlight(page, 0);
        await expectFocusOnItem(page, 0);
      });

      it('opens on the last item with ArrowUp', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await focusTrigger(page);

        await page.keyboard.press('ArrowUp');
        await waitForOpen(rendered);

        await expectHighlight(page, ITEMS.length - 1);
        await expectFocusOnItem(page, ITEMS.length - 1);
      });

      it('scrolls the last item into view when opening with ArrowUp', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await focusTrigger(page);

        await page.keyboard.press('ArrowUp');
        await waitForOpen(rendered);

        await expect.poll(() => isFullyVisible(page, ITEMS.length - 1)).toBe(true);
      });

      it('opens past a disabled first item', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { disabledItems: [ITEMS[0]!] });

        const start = await openWithKeyboard(rendered);

        expect(start).toBe(1);
      });

      if (enterSpaceOpen) {
        for (const key of ['{Enter}', ' ']) {
          it(`opens with ${key} without activating an item`, async ({ page, mode }) => {
            const rendered = await renderList(page, mode, spyOptions);
            await focusTrigger(page);

            await page.keyboard.press(KEYS[key]!);
            await waitForOpen(rendered);
            await settle(rendered);

            await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'true');
            await expectNothingCommitted(rendered);
            await expectHighlight(page, 0);
            await expectFocusOnItem(page, 0);
          });
        }
      }

      it('starts fresh when reopened after Escape', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        const start = await openWithKeyboard(rendered);
        await navigateTo(page, start, 7);
        await page.keyboard.press('Escape');
        await waitForClosed(page);

        await openWithMouse(rendered);

        await expectNoHighlight(page);
      });

      if (selectable) {
        for (const key of ['{ArrowDown}', '{ArrowUp}']) {
          it(`opens on the selected item with ${key}`, async ({ page, mode }) => {
            const rendered = await renderList(page, mode, { root: { defaultValue: ITEMS[8]! } });
            await focusTrigger(page);

            await page.keyboard.press(KEYS[key]!);
            await waitForOpen(rendered);

            await expectHighlight(page, 8);
          });
        }

        it('marks only the selected item as selected', async ({ page, mode }) => {
          const rendered = await renderList(page, mode, { root: { defaultValue: ITEMS[8]! } });
          await openWithMouse(rendered);

          await expect(getItem(page, 8)).toHaveAttribute('aria-selected', 'true');
          await expect(getItem(page, 2)).toHaveAttribute('aria-selected', 'false');
        });

        it('reveals and highlights the selected item when opening', async ({ page, mode }) => {
          const rendered = await renderList(page, mode, { root: { defaultValue: ITEMS[8]! } });
          await openWithMouse(rendered);

          await expect.poll(() => isFullyVisible(page, 8)).toBe(true);
          await expectHighlight(page, 8);
        });
      }
    });

    test.describe('keyboard', () => {
      it('moves the highlight one item at a time with the arrow keys', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        const start = await openWithKeyboard(rendered);

        await page.keyboard.press('ArrowDown');
        await expectHighlight(page, start + 1);

        await page.keyboard.press('ArrowUp');
        await expectHighlight(page, start);
      });

      it('moves focus along with the highlight', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        const start = await openWithKeyboard(rendered);
        await expectFocusOnItem(page, start);

        await page.keyboard.press('ArrowDown');
        await expectFocusOnItem(page, start + 1);

        await page.keyboard.press('ArrowUp');
        await expectFocusOnItem(page, start);
      });

      for (const key of spaceActivates ? ['{Enter}', ' '] : ['{Enter}']) {
        it(`activates the highlighted item once with ${key}`, async ({ page, mode }) => {
          const rendered = await renderList(page, mode, spyOptions);
          const start = await openWithKeyboard(rendered);
          await page.keyboard.press('ArrowDown');
          await expectFocusOnItem(page, start + 1);

          await page.keyboard.press(KEYS[key]!);

          await waitForClosed(page);
          await expectActivated(rendered.fixture, [ITEMS[start + 1]!]);
          await expect(getTrigger(page)).toBeFocused();
        });
      }

      it('does not submit a surrounding form', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { wrap: 'form' });

        await openWithMouse(rendered);
        await page.keyboard.press('Escape');
        await waitForClosed(page);
        if (enterSpaceOpen) {
          await focusTrigger(page);
          await page.keyboard.press('Enter');
          await waitForOpen(rendered);
          await page.keyboard.press('Escape');
          await waitForClosed(page);
        }
        const start = await openWithKeyboard(rendered);
        await navigateTo(page, start, start + 1);
        await page.keyboard.press('Enter');
        await waitForClosed(page);

        await barrier(rendered.fixture);
        await expect(rendered.fixture).toHaveAttribute('data-submits', '0');
      });

      it('stops on a disabled item while navigating', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { disabledItems: [ITEMS[2]!] });
        const start = await openWithKeyboard(rendered);
        expect(start).toBe(0);

        await navigateTo(page, 0, 1);
        await page.keyboard.press('ArrowDown');
        await expectHighlight(page, 2);

        await page.keyboard.press('ArrowUp');
        await expectHighlight(page, 1);
      });

      if (homeEnd) {
        it('moves the highlight to the last and first items with End and Home', async ({ page, mode }) => {
          const rendered = await renderList(page, mode);
          await openWithKeyboard(rendered);

          await page.keyboard.press('End');
          await expectHighlight(page, ITEMS.length - 1);

          await page.keyboard.press('Home');
          await expectHighlight(page, 0);
        });

        it('scrolls the last and first items into view with End and Home', async ({ page, mode }) => {
          const rendered = await renderList(page, mode);
          await openWithKeyboard(rendered);

          await page.keyboard.press('End');
          await expect.poll(() => isFullyVisible(page, ITEMS.length - 1)).toBe(true);

          await page.keyboard.press('Home');
          await expect.poll(() => isFullyVisible(page, 0)).toBe(true);
        });

        it('moves to disabled ends with End and Home', async ({ page, mode }) => {
          const rendered = await renderList(page, mode, { disabledItems: [ITEMS[0]!, ITEMS[ITEMS.length - 1]!] });
          await openWithKeyboard(rendered);

          await page.keyboard.press('End');
          await expectHighlight(page, ITEMS.length - 1);

          await page.keyboard.press('Home');
          await expectHighlight(page, 0);
        });
      }

      it('scrolls the highlighted item into view in both directions, only as far as needed', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        const start = await openWithKeyboard(rendered);

        await navigateTo(page, start, 8);
        await expect.poll(() => isFullyVisible(page, 8)).toBe(true);

        // Moving to an item that is already visible doesn't scroll.
        const before = await scrollTop(page);
        await navigateTo(page, 8, 7);
        await settle(rendered);
        expect(await scrollTop(page)).toBe(before);

        // Moving one item past the edge scrolls by about one item, rather than recentering.
        await navigateTo(page, 7, 9);
        await expect.poll(() => isFullyVisible(page, 9)).toBe(true);
        expect((await scrollTop(page)) - before).toBeLessThanOrEqual(ITEM_HEIGHT + 1);

        await navigateTo(page, 9, 2);
        await expect.poll(() => isFullyVisible(page, 2)).toBe(true);
      });

      if (typeahead) {
        it('reveals an item reached by typing its label', async ({ page, mode }) => {
          const rendered = await renderList(page, mode);
          await openWithKeyboard(rendered);

          await page.keyboard.type('Item 9');

          await expectHighlight(page, 9);
          await expect.poll(() => isFullyVisible(page, 9)).toBe(true);
        });

        it('starts a new typeahead search after a pause', async ({ page, mode }) => {
          const items = ['Alpha', 'Bravo', 'Charlie'];
          const rendered = await renderList(page, mode, { items });
          await focusTrigger(page);
          await page.keyboard.press('ArrowDown');
          await waitForOpen(rendered);

          await page.keyboard.press('b');
          await expect.poll(() => highlightedText(page)).toBe('Bravo');

          // Longer than the typeahead reset delay, so `c` is a new search and not `bc`; the reset timer is the page's.
          await page.clock.runFor(1000);
          await page.keyboard.press('c');

          await expect.poll(() => highlightedText(page)).toBe('Charlie');
        });
      }

      it('prevents the default scrolling of navigation keys', async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await expectFocusOnItem(page, await openWithKeyboard(rendered));

        for (const key of homeEnd ? ['ArrowDown', 'ArrowUp', 'End', 'Home'] : ['ArrowDown', 'ArrowUp']) {
          // Base UI's `fireEvent` returns `false` when a handler prevented the default action.
          const notPrevented = await page.evaluate(
            (name) => (document.activeElement ?? document.body).dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true })),
            key,
          );
          expect(notPrevented, key).toBe(false);
        }
      });
    });

    test.describe('list boundaries', () => {
      const pastEndVerb = { wrap: 'wraps', stop: 'stops', escape: 'leaves the list' }[listEnd];

      it(`${pastEndVerb} past the first item`, async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        const start = await openWithKeyboard(rendered);
        expect(start).toBe(0);

        await page.keyboard.press('ArrowUp');

        const expected = pastEnd('first', ITEMS.length);
        if (expected === -1) {
          await expectNoHighlight(page);
        } else {
          await expectHighlight(page, expected);
        }
      });

      it(`${pastEndVerb} past the last item`, async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await focusTrigger(page);
        await page.keyboard.press('ArrowUp');
        await waitForOpen(rendered);
        await expectHighlight(page, ITEMS.length - 1);

        await page.keyboard.press('ArrowDown');

        const expected = pastEnd('last', ITEMS.length);
        if (expected === -1) {
          await expectNoHighlight(page);
        } else {
          await expectHighlight(page, expected);
        }
      });

      // Only wrapping moves the highlight to an item that may be scrolled out of view.
      if (listEnd === 'wrap') {
        it('scrolls the first item into view when wrapping past the last item', async ({ page, mode }) => {
          const rendered = await renderList(page, mode);
          await focusTrigger(page);
          await page.keyboard.press('ArrowUp');
          await waitForOpen(rendered);
          await expectHighlight(page, ITEMS.length - 1);

          await page.keyboard.press('ArrowDown');

          await expect.poll(() => isFullyVisible(page, 0)).toBe(true);
        });
      }

      it('keeps a single item reachable', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { ...spyOptions, items: [ITEMS[0]!] });
        await openWithKeyboard(rendered);

        await page.keyboard.press('ArrowDown');
        await expectHighlight(page, listEnd === 'escape' ? -1 : 0);
        if (listEnd === 'escape') {
          await page.keyboard.press('ArrowDown');
        }
        await expectHighlight(page, 0);

        await page.keyboard.press('Enter');
        await waitForClosed(page);
        await expect(rendered.fixture).toHaveAttribute('data-item-clicks', ITEMS[0]!);
      });

      it('opens and handles keys without losing focus when the list is empty', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { items: [] });
        await focusTrigger(page);

        const owner = focusModel === 'virtual' ? 'trigger' : 'popup';

        await page.keyboard.press('ArrowDown');
        await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'true');
        await expect.poll(() => focusOwner(page)).toBe(owner);

        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('ArrowUp');
        await page.keyboard.press('Enter');
        await settle(rendered);

        expect(await focusOwner(page)).toBe(owner);
      });

      it('does not activate anything when every item is disabled', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { ...spyOptions, disabledItems: ITEMS });
        await focusTrigger(page);

        await page.keyboard.press('ArrowDown');
        await waitForOpen(rendered);
        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('ArrowUp');
        await page.keyboard.press('Enter');
        await settle(rendered);

        await expectNothingCommitted(rendered);
        expect(['trigger', 'item', 'popup']).toContain(await focusOwner(page));
      });
    });

    test.describe('dismissal', () => {
      it('closes on Escape without committing, and keeps focus on the trigger', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, spyOptions);
        const start = await openWithKeyboard(rendered);
        await navigateTo(page, start, start + 1);

        await page.keyboard.press('Escape');

        await waitForClosed(page);
        await expect(getTrigger(page)).toBeFocused();
        await expect(getTrigger(page)).not.toHaveAttribute('aria-activedescendant');
        await expectNothingCommitted(rendered);
      });

      it('closes on Tab without committing, and moves focus past the trigger', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, spyOptions);
        const start = await openWithKeyboard(rendered);
        await navigateTo(page, start, start + 1);

        await page.keyboard.press('Tab');

        await waitForClosed(page);
        await expect(page.getByTestId('after')).toBeFocused();
        await expectNothingCommitted(rendered);
      });

      it('closes on Shift+Tab without committing, and moves focus to the element before the popup', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, spyOptions);
        const start = await openWithKeyboard(rendered);
        await navigateTo(page, start, start + 1);

        await page.keyboard.press('Shift+Tab');

        await waitForClosed(page);
        // The popup follows the trigger in focus order, unless focus never left the trigger.
        await expect(page.getByTestId(focusModel === 'virtual' ? 'before' : 'trigger')).toBeFocused();
        await expectNothingCommitted(rendered);
      });

      it('closes on an outside press without committing', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, spyOptions);
        await openWithMouse(rendered);
        await getItem(page, 1).hover();
        await expectHighlight(page, 1);

        // Base UI clicks the body: a point of the page that holds no control (the backdrop, when modal).
        const viewport = page.viewportSize()!;
        await page.mouse.click(viewport.width - 4, viewport.height - 4);

        await waitForClosed(page);
        await expectNothingCommitted(rendered);
      });

      it(`closes on a real outside click ${modal ? 'that the page underneath does not receive' : 'that reaches the page underneath'}`, async ({ page, mode }) => {
        const rendered = await renderList(page, mode);
        await openWithMouse(rendered);

        // `force` clicks at the button's position even when a backdrop covers it.
        await page.getByTestId('after').click({ force: true });

        await waitForClosed(page);
        await barrier(rendered.fixture);
        await expect(rendered.fixture).toHaveAttribute('data-outside-clicks', modal ? '0' : '1');
      });

      if (triggerClickCloses) {
        it('closes when the trigger is clicked again', async ({ page, mode }) => {
          const rendered = await renderList(page, mode);
          await openWithMouse(rendered);

          // `force`: a modal host's backdrop has no cut-out of the trigger (Base UI's InternalBackdrop `cutout`).
          await getTrigger(page).click({ force: modal });

          await waitForClosed(page);
          await settle(rendered);
          await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'false');
        });
      }

      it('closes only itself on Escape inside a dialog', async ({ page, mode }) => {
        const rendered = await renderList(page, mode, { wrap: 'dialog' });
        await openWithKeyboard(rendered);

        await page.keyboard.press('Escape');

        await waitForClosed(page);
        await expect(page.getByTestId('dialog')).toBeVisible();

        await page.keyboard.press('Escape');
        await expect(page.getByTestId('dialog')).toHaveCount(0);
      });
    });
  });
}
