import { type Locator, type Page } from '@playwright/test';
import { delayCircuit, expect, readDiagnostics, test, waitForPositioned, type RenderMode } from '../helpers';
import { gotoPopupFixture, openChanges, pressControl, settle, type PopupFixtureOptions } from './popupFixture';

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
  /**
   * Whether the handle's open throws for an unregistered trigger id instead of opening unassociated with a warning; the
   * fixture's handle follows its `throws=false` query option.
   */
  throwOnMissingTrigger: boolean;
  /** Whether the fixture's popup renders a close ("Close popup"), which click-opened popups are closed with. */
  close: boolean;
  /** Whether the fixture renders a positioner (`data-testid="positioner"`). */
  positioner?: boolean;
  /** Whether the fixture renders a viewport (enables the inline scale title). */
  viewport?: boolean;
  /** Titles listed open, each with its row and the track or milestone that proves it. */
  open?: Readonly<Record<string, string>>;
  /**
   * Titles settled open as a `test.fixme` with an `open` annotation (tests/browser/CONTEXT.md), each with its row and the
   * issue that proves it, such as the click families' reparent marker title (issue 306).
   */
  fixme?: Readonly<Record<string, string>>;
  /** When given, only these titles are declared (a second run of one branch, such as `throwOnMissingTrigger: false`). */
  only?: readonly string[];
}

// The circuit's one-way delay of the titles that leave a hover-opened popup, on Server (issue 177): the Positioner's first
// pass lands a round trip after the popup shows, so a leave that does not wait for it meets the unpositioned popup.
const HOVER_LEAVE_LATENCY = 100;

export function detachedTriggersConformanceTests(config: DetachedTriggersConfig): void {
  const { fixture: fixtureName, openInteractions, ariaExpanded, throwOnMissingTrigger, close, positioner = false, viewport = false, open: listedOpen = {}, fixme: listedFixme = {}, only } = config;
  const primaryInteraction = openInteractions[0];
  // Switching the active trigger must keep the popup mounted. Hover-opened popups close on `mouseleave` before the next
  // trigger is entered, so they switch with focus instead.
  const switchInteraction: OpenInteraction = openInteractions.includes('focus') ? 'focus' : primaryInteraction;

  const getTrigger = (page: Page, number: number) => page.locator(`[data-testid="trigger-${number}"]`);
  const getPopup = (page: Page) => page.locator('[data-testid="popup"]');

  function render(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<Locator> {
    return gotoPopupFixture(page, mode, fixtureName, options);
  }

  /**
   * Moves the real pointer to the page's origin, out of the triggers. With a positioner it first waits for the positioner's
   * first pass (`waitForPositioned`): until then the popup sits unpositioned at the origin, and from a second opening on the
   * session still holds the previous placement, so the safe polygon finds the pointer inside the popup's box and waits for
   * a later move, which never comes (issue 177; issue 191's class).
   */
  async function leaveTriggers(page: Page): Promise<void> {
    if (positioner) {
      await waitForPositioned(page.locator('[data-testid="positioner"]'));
    }
    await page.mouse.move(0, 0);
  }

  /** Delays the Server circuit before the first navigation of a title that closes a hover-opened popup by leaving it. */
  async function delayHoverLeave(page: Page, mode: RenderMode, interaction: OpenInteraction): Promise<void> {
    if (interaction === 'hover' && mode === 'server') {
      await delayCircuit(page, HOVER_LEAVE_LATENCY);
    }
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
      await leaveTriggers(page);
    } else {
      await trigger.blur();
    }
  }

  async function switchTrigger(interaction: OpenInteraction, page: Page, to: Locator): Promise<void> {
    if (interaction === 'hover') {
      await leaveTriggers(page);
      await to.hover();
    } else {
      await openWith(interaction, to);
    }
  }

  const waitForPopupOpen = (page: Page) => expect(getPopup(page)).toBeVisible();
  const waitForPopupClosed = (page: Page) => expect(getPopup(page)).toHaveCount(0);
  // The content outside a Viewport's `[data-previous]` clone: during a switch the clone holds the outgoing content, which
  // Base UI's `waitFor` retries past and Playwright's strict locator would not.
  const currentContent = (page: Page) => page.locator('[data-testid="content"]:not([data-previous] *)');
  const waitForContent = (page: Page, text: string) => expect(currentContent(page)).toHaveText(text);

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

  /**
   * Declares a title; one listed open is skipped with its reason, and one listed in `fixme` is a `test.fixme`. `tag` is
   * Playwright's (`@coverage` marks a coverage title for the red check, ADR 11 Amendments 2026-11-19).
   */
  function it(title: string, body: (args: { page: Page; mode: RenderMode }) => Promise<void>, { tag }: { tag?: string } = {}): void {
    if (only && !only.includes(title)) {
      return;
    }

    const openRow = listedFixme[title];
    if (openRow) {
      test.fixme(title, { ...(tag ? { tag } : {}), annotation: { type: 'open', description: openRow } }, async ({ page, mode }) => {
        await body({ page, mode });
      });
      return;
    }

    test(title, tag ? { tag } : {}, async ({ page, mode }) => {
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
              await delayHoverLeave(page, mode, interaction);
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
            await expect(currentContent(page)).toHaveText('2');
            await expectActiveTrigger(page, 2);
          });

          it('opens initially with the payload of a non-first `defaultTriggerId`', async ({ page, mode }) => {
            await render(page, mode, { mode: triggerMode, triggerCount: 3, defaultOpen: true, defaultTriggerId: 'trigger-2' });

            await waitForContent(page, '2');
            await expectActiveTrigger(page, 2, 3);
          });

          it('allows controlling the open state and active trigger programmatically', async ({ page, mode }) => {
            await render(page, mode, { mode: triggerMode, case: 'controlled' });

            // A modal popup hides the page (`aria-hidden`), and these locators resolve on each use where Base UI queried
            // the buttons up front (track 8).
            const openTrigger1 = page.getByRole('button', { name: 'Open Trigger 1', includeHidden: true });
            const openTrigger2 = page.getByRole('button', { name: 'Open Trigger 2', includeHidden: true });
            const closeExternally = page.getByRole('button', { name: 'Close externally', includeHidden: true });

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

          await pressControl(fixture.locator('[data-handle-open="trigger-2"]'));
          await waitForContent(page, '2');
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
          await expectActiveTrigger(page, 2);

          await pressControl(fixture.locator('[data-handle-close]'));
          await waitForPopupClosed(page);
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');
          await expectActiveTrigger(page, null);
        });

        if (throwOnMissingTrigger) {
          it('throws when opened with an unregistered trigger id', async ({ page, mode }) => {
            const fixture = await render(page, mode, { mode: 'detached' });

            await pressControl(fixture.locator('[data-handle-open="missing"]'));
            await expect(fixture).toHaveAttribute(
              'data-handle-error',
              /was called with the trigger id "missing", but no matching trigger is registered/,
            );
            await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');
          });
        } else {
          it('opens unassociated with a warning when opened with an unregistered trigger id', async ({ page, mode }) => {
            const fixture = await render(page, mode, { mode: 'detached', throws: false });

            await pressControl(fixture.locator('[data-handle-open="missing"]'));
            await waitForPopupOpen(page);
            const warnings = (await readDiagnostics(page, mode, fixture)).filter((entry) => entry.message.includes('No trigger found with id "missing"'));
            expect(warnings).toHaveLength(1);
            await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
            await expectActiveTrigger(page, null);
          });
        }
      });

      test.describe('root ownership', () => {
        it('ignores imperative calls while no root is attached and follows the root across remounts', async ({ page, mode }) => {
          const fixture = await render(page, mode, { mode: 'detached', rootMounted: false });
          const detachedWarnings = async () =>
            (await readDiagnostics(page, mode, fixture)).filter((entry) => entry.message.includes('no root using this handle is mounted')).length;

          // Before any root attached the handle has no logger, so its two warnings are dropped (design point 6 of M4 track
          // 1, settled by the orchestrator 2026-10-27); Base UI counts two here.
          await pressControl(fixture.locator('[data-handle-open="trigger-1"]'));
          await pressControl(fixture.locator('[data-handle-close]'));
          await settle(fixture);
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');
          expect(await detachedWarnings()).toBe(0);

          await pressControl(fixture.locator('[data-toggle-root]'));
          await expect(page.locator('[data-testid="root-payload"]')).toHaveText('No payload');
          await expect(getPopup(page)).toHaveCount(0);

          await pressControl(fixture.locator('[data-handle-open="trigger-1"]'));
          await waitForContent(page, '1');
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');

          // Unmounting the root while open detaches it from the handle.
          await pressControl(fixture.locator('[data-toggle-root]'));
          await settle(fixture);
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');
          await expect(getPopup(page)).toHaveCount(0);

          await pressControl(fixture.locator('[data-handle-open="trigger-1"]'));
          await pressControl(fixture.locator('[data-handle-close]'));
          await settle(fixture);
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'false');
          expect(await detachedWarnings()).toBe(2);

          // A remounted root starts closed, without the previous payload, and is controllable.
          await pressControl(fixture.locator('[data-toggle-root]'));
          await expect(page.locator('[data-testid="root-payload"]')).toHaveText('No payload');
          await expect(getPopup(page)).toHaveCount(0);

          await pressControl(fixture.locator('[data-handle-open="trigger-2"]'));
          await waitForContent(page, '2');
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
        });

        it('registers a detached trigger declared after the root', async ({ page, mode }) => {
          await render(page, mode, { mode: 'detached', rootFirst: true });

          await openWith(primaryInteraction, getTrigger(page, 2));
          await waitForContent(page, '2');
          await expectActiveTrigger(page, 2);
        });

        it('resolves a trigger still registered to the previous root during a transient overlap', async ({ page, mode }) => {
          // The detached trigger settles into the outgoing root's store. The incoming root then attaches while the outgoing
          // one is still mounted, and its batch opens by trigger id before the trigger has migrated to the incoming store.
          const fixture = await render(page, mode, { case: 'overlap' });
          await pressControl(fixture.locator('[data-set-phase="overlap"]'));
          await settle(fixture);

          await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
          await expect(fixture).not.toHaveAttribute('data-handle-error', /./);
          await expect(getTrigger(page, 2)).toHaveAttribute('data-popup-open');
          await expect(getTrigger(page, 1)).not.toHaveAttribute('data-popup-open');
          expect((await readDiagnostics(page, mode, fixture)).filter((entry) => entry.message.includes('No trigger found'))).toHaveLength(0);

          // Completing the handoff (the outgoing root unmounts) keeps the popup open and associated.
          await pressControl(fixture.locator('[data-set-phase="incoming"]'));
          await settle(fixture);
          await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
          await expect(getTrigger(page, 2)).toHaveAttribute('data-popup-open');
          await expect(getTrigger(page, 1)).not.toHaveAttribute('data-popup-open');
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
            await delayHoverLeave(page, mode, primaryInteraction);
            // `rearmAttached`: the fixture's readiness marker also waits for the reparented trigger's attach (issue 270).
            const fixture = await render(page, mode, { mode: 'detached', triggerCount: 1, nesting: initialNesting, rearmAttached: true });

            await expectOpensAndCloses(page);

            for (const nesting of nextNestings) {
              await pressControl(fixture.locator(`[data-set-nesting="${nesting}"]`));
              if (recreateHandle) {
                await pressControl(fixture.locator('[data-recreate-handle]'));
              }

              await settle(fixture);
              await expect(fixture).toHaveAttribute('data-attached', 'true');
              await expectOpensAndCloses(page);
            }
          });
        }

        // Issue 270 (M6 track 11): a reparent gives the trigger a new instance whose handles attach after the render that
        // moves it, so the readiness marker the reparent titles wait on drops until they have, rather than keeping the first
        // render's value. The drop is recorded as it happens (ADR 06 §2). Every family's fixture lists its triggers' attach
        // tasks in `Attachments`, each through its own reference capture (issue 306).
        it('port: a reparented trigger takes the readiness marker down until its handles attach again', async ({ page, mode }) => {
          const fixture = await render(page, mode, { mode: 'detached', triggerCount: 1, nesting: 0, rearmAttached: true });
          await expect(fixture).toHaveAttribute('data-attached', 'true');
          await fixture.evaluate((element) => {
            const values: string[] = [];
            (window as unknown as { attachedValues: string[] }).attachedValues = values;
            // One callback can carry both mutations when Server's two render batches land in one task, so each record's value
            // is the next record's old value, and the last record's is the current one.
            new MutationObserver((records) =>
              records.forEach((_, index) => {
                const next = records[index + 1];
                values.push((next ? next.oldValue : element.getAttribute('data-attached')) ?? '');
              }),
            ).observe(element, {
              attributes: true,
              attributeOldValue: true,
              attributeFilter: ['data-attached'],
            });
          });

          await pressControl(fixture.locator('[data-set-nesting="2"]'));
          await settle(fixture);

          await expect(fixture).toHaveAttribute('data-attached', 'true');
          expect(await page.evaluate(() => (window as unknown as { attachedValues: string[] }).attachedValues)).toEqual(['false', 'true']);
          // A coverage title (ADR 11 Amendments 2026-11-19, fixture-side fixes): the fix is in PopupSuiteFixtureBase.cs, so the
          // title passes against the base's src/; its red is its run against the old fixture base (PR 307).
        }, { tag: '@coverage' });
      });
    });
  });
}
