import { expect, readDiagnostics, test } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { gotoPopupFixture, settle } from './suites/popupFixture';
import { popupConformanceTests } from './suites/popupConformanceTests';

// The [L4] rows of docs/components/tooltip.md (M4 track 6, issue 121) on the Tooltip fixtures, both render modes, Server
// first. Base UI's titles verbatim; port titles start with "port:". The shared suites run with Tooltip's options
// (TooltipRoot.test.tsx:27-40, TooltipRoot.detached-triggers.test.tsx:175-189).

const OVERLAP_TITLE = 'resolves a trigger still registered to the previous root during a transient overlap';

popupConformanceTests({
  fixture: 'tooltip-conformance',
  triggerMouseAction: 'hover',
});

detachedTriggersConformanceTests({
  fixture: 'tooltip-triggers',
  openInteractions: ['hover', 'focus'],
  ariaExpanded: false,
  throwOnMissingTrigger: true,
  close: false,
  positioner: true,
  open: {
    // Base UI changes the phase with `setProps`; the suite helper clicks the fixture's button with the real pointer, which
    // is a press outside an open tooltip (TIP-PTR-14). The port title below runs the same steps with a click that has no
    // pointer press.
    [OVERLAP_TITLE]: 'TIP-PTR-14: the phase button is an outside press; restated as the port title of this spec',
  },
});

test.describe('detached triggers', () => {
  test(`port: ${OVERLAP_TITLE}, with the phase changed by a click with no pointer press`, async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'tooltip-triggers', { case: 'overlap' });
    const setPhase = (phase: string) => fixture.locator(`[data-set-phase="${phase}"]`).evaluate((element) => (element as HTMLElement).click());
    const trigger = (number: number) => page.locator(`[data-testid="trigger-${number}"]`);

    await setPhase('overlap');
    await settle(fixture);

    await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
    await expect(fixture).not.toHaveAttribute('data-handle-error', /./);
    await expect(trigger(2)).toHaveAttribute('data-popup-open');
    await expect(trigger(1)).not.toHaveAttribute('data-popup-open');
    expect((await readDiagnostics(page, mode, fixture)).filter((entry) => entry.message.includes('No trigger found'))).toHaveLength(0);

    await setPhase('incoming');
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
    await expect(trigger(2)).toHaveAttribute('data-popup-open');
    await expect(trigger(1)).not.toHaveAttribute('data-popup-open');
  });
});
