import { type Locator, type Page } from '@playwright/test';
import { expect, test, type RenderMode } from '../helpers';
import { gotoPopupFixture, openChanges, settle, type PopupFixtureOptions } from './popupFixture';

// Base UI's detachedTriggersConformanceTests (packages/react/test/detachedTriggersConformanceTests.tsx at v1.9.0), titles
// verbatim, as a Playwright helper (ADR 09 Amendments 2026-10-27; design point 7 of M4 track 1). The suite's Fixture is a
// page inheriting PopupSuiteFixtureBase: `mode`, `triggerCount`, `rootFirst`, `nesting`, `open`, `triggerId`,
// `defaultOpen` and `defaultTriggerId` in its query; `case=controlled` for the programmatic case; buttons for the root's
// mount (`[data-toggle-root]`), the nesting (`[data-set-nesting]`), a new handle (`[data-recreate-handle]`) and the
// handle's imperative calls (`[data-handle-open]`, `[data-handle-close]`), whose state renders as
// `data-handle-open-state` and `data-handle-error`. Triggers `trigger-1`.. carry their number as payload; the root's
// payload template renders `root-payload` and the popup with its `content`. A title listed in `open` is skipped with
// its reason.

type OpenInteraction = 'click' | 'hover' | 'focus';

/** The options of detachedTriggersConformanceTests (Base UI's DetachedTriggersTestConfig, the parts replaced by a fixture). */
export interface DetachedTriggersConfig {
  fixture: string;
  /** Interactions that open the popup from a trigger. The first one is the primary interaction. */
  openInteractions: readonly [OpenInteraction, ...OpenInteraction[]];
  /** Whether triggers expose `aria-expanded` and `aria-controls`. */
  ariaExpanded: boolean;
  /** Whether the handle's open throws for an unregistered trigger id instead of opening unassociated with a warning. */
  throwOnMissingTrigger: boolean;
  /** Whether the fixture's popup renders a close ("Close popup"), which click-opened popups are closed with. */
  close: boolean;
  /** Whether the fixture renders a positioner (`data-testid="positioner"`). */
  positioner?: boolean;
  /** Whether the fixture renders a viewport (enables the inline scale title). */
  viewport?: boolean;
  /** Titles listed open, each with its row and the track or milestone that proves it. */
  open?: Readonly<Record<string, string>>;
}

export function detachedTriggersConformanceTests(config: DetachedTriggersConfig): void {
  const { fixture: fixtureName, openInteractions, ariaExpanded, throwOnMissingTrigger, close, positioner = false, viewport = false, open: listedOpen = {} } = config;
  const primaryInteraction = openInteractions[0];
  // Switching the active trigger must keep the popup mounted. Hover-opened popups close on `mouseleave` before the next
  // trigger is entered, so they switch with focus instead.
  const switchInteraction: OpenInteraction = openInteractions.includes('focus') ? 'focus' : primaryInteraction;

  const getTrigger = (page: Page, number: number) => page.locator(`[data-testid="trigger-${number}"]`);
  const getPopup = (page: Page) => page.locator('[data-testid="popup"]');

  function render(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<Locator> {
    return gotoPopupFixture(page, mode, fixtureName, options);
  }

  async function openWith(interaction: OpenInteraction, trigger: Locator): Promise<void> {
    if (interaction === 'click') {
      await trigger.click();
    } else if (interaction === 'hover') {
      await trigger.hover();
    } else {
      await trigger.focus();
    }
  }

  async function closeWith(interaction: OpenInteraction, page: Page, trigger: Locator): Promise<void> {
    if (interaction === 'click') {
      if (close) {
        await page.getByText('Close popup').click();
      } else {
        await page.keyboard.press('Escape');
      }
    } else if (interaction === 'hover') {
      await page.mouse.move(0, 0);
    } else {
      await trigger.blur();
    }
  }

  async function switchTrigger(interaction: OpenInteraction, page: Page, to: Locator): Promise<void> {
    if (interaction === 'hover') {
      await page.mouse.move(0, 0);
      await to.hover();
    } else {
      await openWith(interaction, to);
    }
  }

  const waitForPopupOpen = (page: Page) => expect(getPopup(page)).toBeVisible();
  const waitForPopupClosed = (page: Page) => expect(getPopup(page)).toHaveCount(0);
  const waitForContent = (page: Page, text: string) => expect(page.locator('[data-testid="content"]')).toHaveText(text);

  /** Asserts that only the trigger with the given number is marked as the popup's active trigger, or that none is. */
  async function expectActiveTrigger(page: Page, activeNumber: number | null, triggerCount = 2): Promise<void> {
    const popupId = activeNumber === null ? null : await getPopup(page).getAttribute('id');
    for (let number = 1; number <= triggerCount; number += 1) {
      const trigger = getTrigger(page, number);
      const active = number === activeNumber;
      if (active) {
        await expect(trigger).toHaveAttribute('data-popup-open');
      } else {
        await expect(trigger).not.toHaveAttribute('data-popup-open');
      }

      if (ariaExpanded) {
        await expect(trigger).toHaveAttribute('aria-expanded', String(active));
        if (active) {
          await expect(trigger).toHaveAttribute('aria-controls', popupId ?? '');
        } else if (popupId !== null) {
          await expect(trigger).not.toHaveAttribute('aria-controls');
        }
      }
    }
  }

  async function expectPositionerAlignedWith(page: Page, trigger: Locator): Promise<void> {
    if (!positioner) {
      return;
    }

    await expect
      .poll(async () => Math.abs(((await page.locator('[data-testid="positioner"]').boundingBox())?.x ?? 0) - ((await trigger.boundingBox())?.x ?? 0)))
      .toBeLessThanOrEqual(1);
  }

  /** Declares a title; one listed open is skipped with its reason. */
  function it(title: string, body: (args: { page: Page; mode: RenderMode }) => Promise<void>): void {
    test(title, async ({ page, mode }) => {
      test.skip(title in listedOpen, listedOpen[title]);
      await body({ page, mode });
    });
  }

  test.describe('detached triggers conformance', () => {
    for (const triggerMode of ['contained', 'detached'] as const) {
      test.describe(`${triggerMode} triggers`, () => {
        test.describe('multiple triggers', () => {
          for (const interaction of openInteractions) {
            it(`opens with any trigger on ${interaction} and marks only that trigger as active`, async ({ page, mode }) => {
              await render(page, mode, { mode: triggerMode, triggerCount: 3 });

              await expect(getPopup(page)).toHaveCount(0);
              await expectActiveTrigger(page, null, 3);

              for (const number of [1, 2, 3]) {
                const trigger = getTrigger(page, number);

                await openWith(interaction, trigger);
                await waitForPopupOpen(page);
                await expectActiveTrigger(page, number, 3);

                await closeWith(interaction, page, trigger);
                await waitForPopupClosed(page);
                await expectActiveTrigger(page, null, 3);
              }
            });
          }

          it('sets the payload of the active trigger and renders content based on it', async ({ page, mode }) => {
            await render(page, mode, { mode: triggerMode });

            await openWith(primaryInteraction, getTrigger(page, 1));
            await waitForContent(page, '1');
            await expect(page.locator('[data-testid="root-payload"]')).toHaveText('1');

            await switchTrigger(primaryInteraction, page, getTrigger(page, 2));
            await waitForContent(page, '2');
            await expect(page.locator('[data-testid="root-payload"]')).toHaveText('2');
            await expectActiveTrigger(page, 2);
          });

          it('reuses the popup DOM nodes when switching triggers', async ({ page, mode }) => {
            await render(page, mode, { mode: triggerMode });

            await openWith(switchInteraction, getTrigger(page, 1));
            await waitForContent(page, '1');
            // Marks the nodes in the page: a replaced node loses the mark.
            await getPopup(page).evaluate((node) => node.setAttribute('data-original', ''));
            if (positioner) {
              await page.locator('[data-testid="positioner"]').evaluate((node) => node.setAttribute('data-original', ''));
            }

            await switchTrigger(switchInteraction, page, getTrigger(page, 2));
            await waitForContent(page, '2');
            await expect(getPopup(page)).toHaveAttribute('data-original', '');
            if (positioner) {
              await expect(page.locator('[data-testid="positioner"]')).toHaveAttribute('data-original', '');
            }
          });

          it('marks the trigger matching a controlled `triggerId` as active', async ({ page, mode }) => {
            await render(page, mode, { mode: triggerMode, open: true, triggerId: 'trigger-2' });

            await waitForPopupOpen(page);
            await expect(page.locator('[data-testid="content"]')).toHaveText('2');
            await expectActiveTrigger(page, 2);
          });

          it('opens initially with the payload of a non-first `defaultTriggerId`', async ({ page, mode }) => {
            await render(page, mode, { mode: triggerMode, triggerCount: 3, defaultOpen: true, defaultTriggerId: 'trigger-2' });

            await waitForContent(page, '2');
            await expectActiveTrigger(page, 2, 3);
          });

          it('allows controlling the open state and active trigger programmatically', async ({ page, mode }) => {
            await render(page, mode, { mode: triggerMode, case: 'controlled' });

            const openTrigger1 = page.getByRole('button', { name: 'Open Trigger 1' });
            const openTrigger2 = page.getByRole('button', { name: 'Open Trigger 2' });
            const closeExternally = page.getByRole('button', { name: 'Close externally' });

            await openTrigger1.click();
            await waitForContent(page, '1');
            await expectActiveTrigger(page, 1);
            await expectPositionerAlignedWith(page, getTrigger(page, 1));

            await openTrigger2.click();
            await waitForContent(page, '2');
            await expectActiveTrigger(page, 2);
            await expectPositionerAlignedWith(page, getTrigger(page, 2));

            if (close) {
              await page.getByText('Close popup').click();
              await waitForPopupClosed(page);
              // Focus returns to the element that opened the popup.
              await expect(openTrigger2).toBeFocused();
            } else {
              await closeExternally.click();
              await waitForPopupClosed(page);
            }

            await expectActiveTrigger(page, null);
          });

          if (viewport) {
            it('does not leave an inline scale style on the popup after switching triggers', async ({ page, mode }) => {
              const fixture = await render(page, mode, { mode: triggerMode, viewport: true, animations: 1 });

              await openWith(switchInteraction, getTrigger(page, 1));
              await waitForContent(page, '1');

              await switchTrigger(switchInteraction, page, getTrigger(page, 2));
              await waitForContent(page, '2');
              await settle(fixture);

              // The trigger changed while the popup stayed open, rather than closing and reopening.
              const changes = await openChanges(fixture);
              expect(changes.length).toBeGreaterThan(0);
              expect(changes.map((entry) => entry.split(':')[0])).not.toContain('false');
              // An inline scale would override CSS transitions on the popup.
              expect(await getPopup(page).evaluate((node) => (node as HTMLElement).style.scale)).toBe('');
            });
          }
        });
      });
    }

    test.describe('detached triggers', () => {
      test.describe('imperative actions on the handle', () => {
        it('opens by trigger id with its payload and closes', async ({ page, mode }) => {
          const fixture = await render(page, mode, { mode: 'detached' });

          await expect(getPopup(page)).toHaveCount(0);
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');

          await fixture.locator('[data-handle-open="trigger-2"]').click();
          await waitForContent(page, '2');
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
          await expectActiveTrigger(page, 2);

          await fixture.locator('[data-handle-close]').click();
          await waitForPopupClosed(page);
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');
          await expectActiveTrigger(page, null);
        });

        if (throwOnMissingTrigger) {
          it('throws when opened with an unregistered trigger id', async ({ page, mode }) => {
            const fixture = await render(page, mode, { mode: 'detached' });

            await fixture.locator('[data-handle-open="missing"]').click();
            await expect(fixture).toHaveAttribute(
              'data-handle-error',
              /was called with the trigger id "missing", but no matching trigger is registered/,
            );
            await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');
          });
        }
      });

      test.describe('root ownership', () => {
        it('registers a detached trigger declared after the root', async ({ page, mode }) => {
          await render(page, mode, { mode: 'detached', rootFirst: true });

          await openWith(primaryInteraction, getTrigger(page, 2));
          await waitForContent(page, '2');
          await expectActiveTrigger(page, 2);
        });
      });

      test.describe('reparenting', () => {
        async function expectOpensAndCloses(page: Page): Promise<void> {
          await openWith(primaryInteraction, getTrigger(page, 1));
          await waitForPopupOpen(page);
          await expectActiveTrigger(page, 1, 1);
          await closeWith(primaryInteraction, page, getTrigger(page, 1));
          await waitForPopupClosed(page);
        }

        const cases = [
          { name: 'when wrappers are removed', nestings: [3, 2, 1, 0], recreateHandle: false },
          { name: 'when wrappers are added', nestings: [0, 1, 2, 3], recreateHandle: false },
          { name: 'during Fast Refresh-like handle recreation', nestings: [1, 1, 1], recreateHandle: true },
          { name: 'when reparented during Fast Refresh-like handle recreation', nestings: [3, 2, 1, 0], recreateHandle: true },
        ];

        for (const { name, nestings, recreateHandle } of cases) {
          it(`keeps a detached trigger working ${name}`, async ({ page, mode }) => {
            const [initialNesting, ...nextNestings] = nestings;
            const fixture = await render(page, mode, { mode: 'detached', triggerCount: 1, nesting: initialNesting });

            await expectOpensAndCloses(page);

            for (const nesting of nextNestings) {
              await fixture.locator(`[data-set-nesting="${nesting}"]`).click();
              if (recreateHandle) {
                await fixture.locator('[data-recreate-handle]').click();
              }

              await settle(fixture);
              await expectOpensAndCloses(page);
            }
          });
        }
      });
    });
  });
}
