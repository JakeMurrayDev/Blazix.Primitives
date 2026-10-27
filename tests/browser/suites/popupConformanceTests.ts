import { type Locator, type Page } from '@playwright/test';
import { expect, test, type RenderMode } from '../helpers';
import { animationStarts, gotoPopupFixture, openChangeCompletes, openChanges, recordAnimationStarts, setRootOpen, settle, type PopupFixtureOptions } from './popupFixture';

// Base UI's popupConformanceTests (packages/react/test/popupConformanceTests.tsx at v1.9.0), titles verbatim, as a
// Playwright helper (ADR 09 Amendments 2026-10-27; design point 7 of M4 track 1). A family's spec calls it with its fixture,
// whose page inherits PopupSuiteFixtureBase: the suite's `render(...)` and `rerender(...)` with root props become the
// fixture's query and its `[data-set-open]` buttons. A title the host cannot run yet is listed in `open` with the row and
// the track that proves it; it is skipped with that reason.

/** The options of popupConformanceTests (Base UI's PopupTestConfig, the rendering half replaced by a fixture). */
export interface PopupConformanceConfig {
  /**
   * The fixture page: `?open=` makes its root controlled, and `defaultOpen`, `cancelOpen`, `popupId`, `keepMounted` and
   * `animated` set the rest; `animated` gives the popup an enter and an exit keyframe animation whose names start with
   * `<fixture>-enter` and `<fixture>-exit`.
   */
  fixture: string;
  triggerMouseAction: 'click' | 'hover';
  expectedPopupRole?: string;
  expectedAriaHasPopupValue?: string;
  alwaysMounted?: boolean;
  combobox?: boolean;
  exitAnimation?: boolean;
  openReason?: string;
  /** Titles listed open, each with its row and the track or milestone that proves it. */
  open?: Readonly<Record<string, string>>;
}

export function popupConformanceTests(config: PopupConformanceConfig): void {
  const {
    fixture: fixtureName,
    triggerMouseAction,
    expectedPopupRole,
    expectedAriaHasPopupValue = expectedPopupRole,
    alwaysMounted = false,
    combobox = false,
    exitAnimation = true,
    openReason = triggerMouseAction === 'click' ? 'trigger-press' : 'trigger-hover',
    open: listedOpen = {},
  } = config;

  const getTrigger = (page: Page) => page.locator('[data-testid="trigger"]');
  const getPopup = (page: Page) => page.locator('[data-testid="popup"]');

  async function render(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
    return gotoPopupFixture(page, mode, fixtureName, options);
  }

  async function openWithTrigger(page: Page): Promise<void> {
    if (triggerMouseAction === 'click') {
      await getTrigger(page).click();
    } else {
      await getTrigger(page).hover();
    }
  }

  // Popups that stay mounted after opening are hidden instead of removed when they close.
  async function expectPopupClosed(page: Page): Promise<void> {
    if (alwaysMounted) {
      await expect(getPopup(page)).toBeHidden();
    } else {
      await expect(getPopup(page)).toHaveCount(0);
    }
  }

  /** Declares a title; one listed open is skipped with its reason. */
  function it(title: string, body: (args: { page: Page; mode: RenderMode }) => Promise<void>): void {
    test(title, async ({ page, mode }) => {
      test.skip(title in listedOpen, listedOpen[title]);
      await body({ page, mode });
    });
  }

  test.describe('Popup conformance', () => {
    test.describe('controlled mode', () => {
      it('opens the popup with the `open` prop', async ({ page, mode }) => {
        const fixture = await render(page, mode, { open: false });
        await expect(getPopup(page)).toHaveCount(0);

        await setRootOpen(fixture, true);
        await expect(getPopup(page)).toHaveCount(1);
      });
    });

    test.describe('prop: defaultOpen', () => {
      it('opens initially and remains uncontrolled', async ({ page, mode }) => {
        await render(page, mode, { defaultOpen: true });
        await expect(getPopup(page)).toBeVisible();

        await page.keyboard.press('Escape');
        await expectPopupClosed(page);
      });

      it('is ignored when `open={false}` is passed', async ({ page, mode }) => {
        const fixture = await render(page, mode, { defaultOpen: true, open: false });
        await settle(fixture);
        await expect(getPopup(page)).toHaveCount(0);
      });

      it('is ignored when `open={true}` is passed, so Escape cannot close', async ({ page, mode }) => {
        const fixture = await render(page, mode, { defaultOpen: true, open: true });
        await expect(getPopup(page)).toBeVisible();

        await page.keyboard.press('Escape');
        await settle(fixture);
        await expect(getPopup(page)).toBeVisible();
      });
    });

    test.describe('prop: onOpenChange', () => {
      it('is called with the new open state and the change details', async ({ page, mode }) => {
        const fixture = await render(page, mode);
        expect(await openChanges(fixture)).toEqual([]);

        await openWithTrigger(page);
        await expect(fixture).toHaveAttribute('data-open-changes', `true:${openReason}`);
        await expect(getPopup(page)).toBeVisible();

        await page.keyboard.press('Escape');
        await expectPopupClosed(page);
        await expect(fixture).toHaveAttribute('data-open-changes', `true:${openReason} false:escape-key`);
      });

      it('keeps the popup closed when the open change is canceled', async ({ page, mode }) => {
        const fixture = await render(page, mode, { cancelOpen: true });

        await openWithTrigger(page);
        await expect(fixture).toHaveAttribute('data-open-changes', `true:${openReason}`);
        await settle(fixture);

        // The open request was made, so a missing popup proves `cancel()` prevented it.
        expect(await openChanges(fixture)).toEqual([`true:${openReason}`]);
        await expectPopupClosed(page);
        await expect(getTrigger(page)).not.toHaveAttribute('data-popup-open');
      });
    });

    if (expectedPopupRole || triggerMouseAction === 'click') {
      test.describe('ARIA attributes', () => {
        if (expectedPopupRole) {
          it(`has the ${expectedPopupRole} role on the popup`, async ({ page, mode }) => {
            await render(page, mode, { open: true });
            await expect(getPopup(page)).toHaveAttribute('role', expectedPopupRole);
          });
        }

        if (triggerMouseAction === 'click') {
          it('has the `aria-controls` attribute on the trigger', async ({ page, mode }) => {
            await render(page, mode, { open: true });
            const id = await getPopup(page).getAttribute('id');
            await expect(getTrigger(page)).toHaveAttribute('aria-controls', id ?? '');
          });

          it('opens on trigger click and sets `aria-expanded` on the trigger', async ({ page, mode }) => {
            await render(page, mode);
            await expect(getPopup(page)).toHaveCount(0);
            await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'false');
            await getTrigger(page).click();
            // A combobox popup is the listbox itself; other popups mark the open state.
            const [openAttribute, openValue] = combobox ? ['role', 'listbox'] : ['data-open', ''];
            await expect(getPopup(page)).toHaveAttribute(openAttribute, openValue);
            await expect(getTrigger(page)).toHaveAttribute('aria-expanded', 'true');
          });

          if (expectedAriaHasPopupValue) {
            it('has the `aria-haspopup` attribute on the trigger', async ({ page, mode }) => {
              const fixture = await render(page, mode, { open: false });
              await expect(getTrigger(page)).toHaveAttribute('aria-haspopup', expectedAriaHasPopupValue);

              await setRootOpen(fixture, true);
              await expect(getPopup(page)).toHaveCount(1);
              await expect(getTrigger(page)).toHaveAttribute('aria-haspopup', expectedAriaHasPopupValue);
            });
          }

          it('allows a custom `id` prop', async ({ page, mode }) => {
            await render(page, mode, { open: true, popupId: 'TestId' });
            await expect(getPopup(page)).toHaveAttribute('id', 'TestId');
            await expect(getTrigger(page)).toHaveAttribute('aria-controls', 'TestId');
          });
        }
      });
    }

    test.describe('prop: onOpenChangeComplete', () => {
      it('is not called on mount when closed', async ({ page, mode }) => {
        const fixture = await render(page, mode);
        await settle(fixture);
        expect(await openChangeCompletes(fixture)).toEqual([]);
      });

      it('is called on open when there is no enter animation', async ({ page, mode }) => {
        const fixture = await render(page, mode, { open: false });

        await setRootOpen(fixture, true);
        await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
        await settle(fixture);
        expect(await openChangeCompletes(fixture)).toEqual(['true']);
      });

      it('is called on close when there is no exit animation', async ({ page, mode }) => {
        const fixture = await render(page, mode, { open: true });
        await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');

        await setRootOpen(fixture, false);
        await expectPopupClosed(page);
        await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
      });

      if (exitAnimation) {
        it('is called on open once the enter animation finishes', async ({ page, mode }) => {
          const fixture = await render(page, mode, { open: false, animated: true, animations: 1 });
          await recordAnimationStarts(page, fixtureName);

          await setRootOpen(fixture, true);
          // When the enter animation started, the completion had not been called.
          await expect
            .poll(() => animationStarts(page))
            .toContainEqual(expect.objectContaining({ name: `${fixtureName}-enter`, completes: '' }));

          await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
        });

        it('is called on close once the exit animation finishes', async ({ page, mode }) => {
          const fixture = await render(page, mode, { open: true, animated: true, animations: 1 });
          await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');

          await recordAnimationStarts(page, fixtureName);

          await setRootOpen(fixture, false);
          // The exit animation started under data-ending-style, before the completion for the close.
          await expect
            .poll(() => animationStarts(page))
            .toContainEqual(expect.objectContaining({ name: `${fixtureName}-exit`, endingStyle: true, completes: 'true' }));

          await expectPopupClosed(page);
          await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
        });
      }
    });

    test.describe('animations', () => {
      it('removes the popup when there is no exit animation defined', async ({ page, mode }) => {
        const fixture = await render(page, mode, { open: true, animations: 1 });
        await expect(getPopup(page)).toHaveCount(1);

        await setRootOpen(fixture, false);
        await expectPopupClosed(page);
      });

      if (exitAnimation) {
        it('hides the kept-mounted popup once the exit animation finishes', async ({ page, mode }) => {
          const fixture = await render(page, mode, { open: true, animated: true, keepMounted: !alwaysMounted, animations: 1 });
          await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');

          await recordAnimationStarts(page, fixtureName);

          await setRootOpen(fixture, false);

          // The popup stays visible while the exit animation runs...
          await expect
            .poll(() => animationStarts(page))
            .toContainEqual(expect.objectContaining({ name: `${fixtureName}-exit`, endingStyle: true, visible: true }));

          // ...and is hidden once it finishes.
          await expect(getPopup(page)).toBeHidden();
        });
      }
    });
  });
}
