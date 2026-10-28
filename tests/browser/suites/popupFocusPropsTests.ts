import { type Locator, type Page } from '@playwright/test';
import { expect, test, type RenderMode } from '../helpers';
import { gotoPopupFixture, settle } from './popupFixture';

// Base UI's popupFocusPropsTests (packages/react/test/popupFocusPropsTests.tsx at v1.9.0), titles verbatim, as a
// Playwright helper (ADR 09 Amendments 2026-10-27; ADR 06 Amendments 2026-10-27, one fixture base). The suite's Test
// component is the fixture page, inheriting PopupSuiteFixtureBase: `render(<Test popupProps={…} withPopupInputs />)`
// becomes the query (`initialFocus`, `finalFocus`, `popupInputs`). Base UI's refs become the elements the fixture renders,
// and its `vi.fn` spies become the calls of the function props, recorded on the fixture's container. Both describes
// repeat some titles, so `open` and `only` take a title (both describes) or `<describe> > <title>` (one). A title listed
// in `open` is skipped with its reason.

/** Base UI's InteractionType literal; `''` is a pointer type the browser did not report (the port's `Unknown`). */
type InteractionTypeLiteral = 'mouse' | 'touch' | 'pen' | 'keyboard' | '';

/**
 * A focus prop's value, as the fixture's `initialFocus` or `finalFocus` query option (Base UI's PopupFocusPropFactory).
 * The prop's target is `inside-2` for initialFocus and `outside-target` for finalFocus.
 * - `false`: the value `false`;
 * - `ref`: the target element (Base UI passes a ref to it);
 * - `fn-element`, `fn-true`, `fn-null`: a function returning the target element, `true`, or Base UI's `null` (the
 *   default target);
 * - `fn-keyboard`: a function returning the target element for a `keyboard` interaction and otherwise `undefined`
 *   (initialFocus: focus is not moved) or `true` (finalFocus).
 */
type FocusPropOption = 'false' | 'ref' | 'fn-element' | 'fn-true' | 'fn-null' | 'fn-keyboard';

type FocusFixtureOptions = {
  initialFocus?: FocusPropOption;
  finalFocus?: FocusPropOption;
  popupInputs?: boolean;
};

/** The options of popupFocusPropsTests (Base UI's PopupFocusPropsTestsConfig, the rendering half replaced by a fixture). */
export interface PopupFocusPropsConfig {
  /**
   * The fixture page: an input `outside-before`, the popup tree, an input `outside-target` (each a `data-testid`). The tree
   * renders a trigger labeled "Open" (or the page's one combobox) and a popup that takes the query's `initialFocus` and
   * `finalFocus` (FocusPropOption) and whose children are, with `popupInputs=true`, inputs `inside-1` and `inside-2`,
   * then a control labeled "Close" that closes the popup on click (no other element's text is "Close"). The container
   * renders each call of a function prop, in order, as `data-initial-focus-calls` and `data-final-focus-calls`:
   * space-separated interaction types, `mouse`, `touch`, `pen`, `keyboard`, or `unknown` for Base UI's `''`.
   */
  fixture: string;
  /** Whether the popup accepts `initialFocus` (default true). */
  initialFocus?: boolean;
  /** The role of the trigger (default `button`). A `button` trigger is labeled "Open"; there is only one `combobox`. */
  triggerRole?: 'button' | 'combobox';
  /** Whether the popup stays mounted (inaccessible) after it closes (default false). */
  alwaysMounted?: boolean;
  /**
   * The close type a `finalFocus` function receives when "Close" is clicked with a mouse (default `mouse`). Only set it
   * to document a popup whose close control doesn't report the pointer type.
   */
  mouseCloseType?: InteractionTypeLiteral;
  /** Titles listed open, by title or `<describe> > <title>`, each with its row and the track or milestone that proves it. */
  open?: Readonly<Record<string, string>>;
  /** When given, only these titles (or `<describe> > <title>` paths) are declared. */
  only?: readonly string[];
}

export function popupFocusPropsTests(config: PopupFocusPropsConfig): void {
  const {
    fixture: fixtureName,
    initialFocus: testInitialFocus = true,
    triggerRole = 'button',
    alwaysMounted = false,
    mouseCloseType = 'mouse',
    open: listedOpen = {},
    only,
  } = config;
  // The fixture records Base UI's empty literal as `unknown`.
  const mouseCloseCall = mouseCloseType === '' ? 'unknown' : mouseCloseType;

  // A combobox trigger is named by its label rather than its content, so find it by role alone.
  const getTrigger = (page: Page) =>
    // After a modal open the page is `aria-hidden`, and the locator resolves on each use (track 8).
    triggerRole === 'combobox'
      ? page.getByRole('combobox', { includeHidden: true })
      : page.getByRole('button', { name: 'Open', exact: true, includeHidden: true });
  const getClose = (page: Page) => page.getByText('Close', { exact: true });
  const getByTestId = (page: Page, testId: string) => page.locator(`[data-testid="${testId}"]`);

  function render(page: Page, mode: RenderMode, options: FocusFixtureOptions = {}): Promise<Locator> {
    return gotoPopupFixture(page, mode, fixtureName, options);
  }

  async function expectClosed(page: Page): Promise<void> {
    if (alwaysMounted) {
      await expect.poll(() => getClose(page).evaluate(isInaccessible)).toBe(true);
    } else {
      await expect(getClose(page)).toHaveCount(0);
    }
  }

  async function findClickableClose(page: Page): Promise<Locator> {
    const close = getClose(page);
    await expect(close).toHaveCount(1);
    // Positioners ignore pointer events until they have been positioned.
    await expect.poll(() => close.evaluate((element) => getComputedStyle(element).pointerEvents)).not.toBe('none');
    return close;
  }

  async function closeWithPointer(page: Page): Promise<void> {
    await (await findClickableClose(page)).click();
  }

  /** Declares a title of `group`; one listed open, by its title or its `<group> > <title>` path, is skipped with its reason. */
  function itIn(group: string) {
    return (title: string, body: (args: { page: Page; mode: RenderMode }) => Promise<void>): void => {
      const path = `${group} > ${title}`;
      if (only && !only.includes(title) && !only.includes(path)) {
        return;
      }

      test(title, async ({ page, mode }) => {
        const reason = listedOpen[path] ?? listedOpen[title];
        test.skip(reason !== undefined, reason);
        await body({ page, mode });
      });
    };
  }

  if (testInitialFocus) {
    test.describe('prop: initialFocus', () => {
      const it = itIn('prop: initialFocus');

      it('focuses the first focusable element by default', async ({ page, mode }) => {
        await render(page, mode, { popupInputs: true });

        await getTrigger(page).click();
        await expect(getByTestId(page, 'inside-1')).toBeFocused();
      });

      it('focuses the element of a ref', async ({ page, mode }) => {
        await render(page, mode, { popupInputs: true, initialFocus: 'ref' });

        await getTrigger(page).click();
        await expect(getByTestId(page, 'inside-2')).toBeFocused();
      });

      const initialCases: ReadonlyArray<{ name: string; option: FocusPropOption; focused: string }> = [
        { name: 'an element', option: 'fn-element', focused: 'inside-2' },
        // `true` and `null` keep the default target, so the recorded call proves the function was consulted.
        { name: 'true', option: 'fn-true', focused: 'inside-1' },
        { name: 'null', option: 'fn-null', focused: 'inside-1' },
      ];

      for (const { name, option, focused } of initialCases) {
        it(`focuses the right element when a function returns ${name}`, async ({ page, mode }) => {
          const fixture = await render(page, mode, { popupInputs: true, initialFocus: option });

          await getTrigger(page).click();
          await expect(getByTestId(page, focused)).toBeFocused();
          await settle(fixture);
          expect(await focusCalls(fixture, 'initial')).toHaveLength(1);
        });
      }

      it('does not move focus when `false`', async ({ page, mode }) => {
        const fixture = await render(page, mode, { popupInputs: true, initialFocus: 'false' });

        const trigger = getTrigger(page);
        await trigger.click();
        await expect(getClose(page)).toHaveCount(1);
        await settle(fixture);
        await waitSingleFrame(page);

        await expect(trigger).toBeFocused();
      });

      it('passes the interaction type to a function, and does nothing when it returns undefined', async ({ page, mode }) => {
        const fixture = await render(page, mode, { popupInputs: true, initialFocus: 'fn-keyboard' });

        const trigger = getTrigger(page);

        await trigger.click();
        await expect.poll(async () => (await focusCalls(fixture, 'initial')).at(-1)).toBe('mouse');
        await expect(trigger).toBeFocused();

        await page.keyboard.press('Escape');
        await expectClosed(page);

        await trigger.focus();
        await page.keyboard.press('Enter');
        await expect(getByTestId(page, 'inside-2')).toBeFocused();
        await expect.poll(async () => (await focusCalls(fixture, 'initial')).at(-1)).toBe('keyboard');

        await page.keyboard.press('Escape');
        await expectClosed(page);

        // Base UI dispatches the touch press itself; the worker's browser context has no touchscreen, so it is dispatched
        // the same way here.
        await trigger.dispatchEvent('pointerdown', { pointerType: 'touch' });
        await trigger.dispatchEvent('click', { detail: 1 });
        await expect.poll(async () => (await focusCalls(fixture, 'initial')).at(-1)).toBe('touch');
        await settle(fixture);
        expect(await focusCalls(fixture, 'initial')).toHaveLength(3);
      });

      it('is not called again when the popup closes', async ({ page, mode }) => {
        const fixture = await render(page, mode, { popupInputs: true, initialFocus: 'fn-element' });

        await getTrigger(page).click();
        await expect(getByTestId(page, 'inside-2')).toBeFocused();
        await settle(fixture);
        expect(await focusCalls(fixture, 'initial')).toHaveLength(1);

        await closeWithPointer(page);
        await expect(getTrigger(page)).toBeFocused();
        await settle(fixture);
        expect(await focusCalls(fixture, 'initial')).toHaveLength(1);
      });
    });
  }

  test.describe('prop: finalFocus', () => {
    const it = itIn('prop: finalFocus');

    it('focuses the trigger by default', async ({ page, mode }) => {
      await render(page, mode);

      await getTrigger(page).click();
      await closeWithPointer(page);

      await expect(getTrigger(page)).toBeFocused();
    });

    it('focuses the element of a ref', async ({ page, mode }) => {
      await render(page, mode, { finalFocus: 'ref' });

      await getTrigger(page).click();
      await closeWithPointer(page);

      await expect(getByTestId(page, 'outside-target')).toBeFocused();
    });

    const finalCases: ReadonlyArray<{ name: string; option: FocusPropOption; getFocused: (page: Page) => Locator }> = [
      { name: 'an element', option: 'fn-element', getFocused: (page) => getByTestId(page, 'outside-target') },
      // `true` and `null` keep the default target, so the recorded call proves the function was consulted.
      { name: 'true', option: 'fn-true', getFocused: getTrigger },
      { name: 'null', option: 'fn-null', getFocused: getTrigger },
    ];

    for (const { name, option, getFocused } of finalCases) {
      it(`focuses the right element when a function returns ${name}`, async ({ page, mode }) => {
        const fixture = await render(page, mode, { finalFocus: option });

        await getTrigger(page).click();
        await closeWithPointer(page);

        await expect(getFocused(page)).toBeFocused();
        await settle(fixture);
        expect(await focusCalls(fixture, 'final')).toHaveLength(1);
      });
    }

    it('does not move focus when `false`', async ({ page, mode }) => {
      const fixture = await render(page, mode, { finalFocus: 'false' });

      await getTrigger(page).click();
      await closeWithPointer(page);
      await expectClosed(page);
      await settle(fixture);
      await waitSingleFrame(page);

      await expect(getTrigger(page)).not.toBeFocused();
      if (!alwaysMounted) {
        // The focused close control was removed, so focus falls back to the body.
        await expect(page.locator('body')).toBeFocused();
      }
    });

    it('passes the close type to a function', async ({ page, mode }) => {
      const fixture = await render(page, mode, { finalFocus: 'fn-keyboard' });

      const trigger = getTrigger(page);

      await trigger.click();
      // Playwright's mouse press carries its pointer type, which user-event's synthetic clicks lack (Base UI dispatches
      // the press directly for that reason).
      await (await findClickableClose(page)).click();
      await expectClosed(page);
      await expect(trigger).toBeFocused();
      await settle(fixture);
      expect((await focusCalls(fixture, 'final'))[0]).toBe(mouseCloseCall);

      await trigger.click();
      await expect(getClose(page)).toHaveCount(1);
      await settle(fixture);
      await waitSingleFrame(page);
      await page.keyboard.press('Escape');
      await expect(getByTestId(page, 'outside-target')).toBeFocused();
      await settle(fixture);
      expect(await focusCalls(fixture, 'final')).toEqual([mouseCloseCall, 'keyboard']);
    });
  });
}

/** The recorded calls of the `initialFocus` or `finalFocus` function, as interaction types. */
async function focusCalls(fixture: Locator, prop: 'initial' | 'final'): Promise<string[]> {
  const value = await fixture.getAttribute(`data-${prop}-focus-calls`);
  return value ? value.split(' ').filter(Boolean) : [];
}

/**
 * testing-library's `isInaccessible`, run in the page: hidden by its own visibility, or by `hidden`, `aria-hidden="true"`
 * or `display: none` on it or an ancestor.
 */
function isInaccessible(element: Element): boolean {
  if (getComputedStyle(element).visibility === 'hidden') {
    return true;
  }

  for (let current: Element | null = element; current !== null; current = current.parentElement) {
    if ((current as HTMLElement).hidden === true || current.getAttribute('aria-hidden') === 'true' || getComputedStyle(current).display === 'none') {
      return true;
    }
  }

  return false;
}

/** Waits one animation frame in the page (Base UI's `waitSingleFrame`). */
async function waitSingleFrame(page: Page): Promise<void> {
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
}
