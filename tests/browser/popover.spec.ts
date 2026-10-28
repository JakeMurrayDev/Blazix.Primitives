import { expect, test } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPopupFixture, pressControl, settle } from './suites/popupFixture';
import { popupFocusPropsTests } from './suites/popupFocusPropsTests';
import { viewportConformanceTests } from './suites/viewportConformanceTests';

// The [L4] rows of docs/components/popover.md (M4 track 5, issue 125) on the `popover-*` fixtures, every title on both
// render modes. The shared suites run with Popover's values (PopoverRoot.test.tsx and
// PopoverRoot.detached-triggers.test.tsx): a trigger pressed by click through the `interactions` module's `press`, role
// and aria-haspopup `dialog`, a positioner, a viewport and a close.

popupConformanceTests({
  fixture: 'popover-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'dialog',
});

detachedTriggersConformanceTests({
  fixture: 'popover-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: true,
  close: true,
  positioner: true,
  viewport: true,
  open: {
    'allows controlling the open state and active trigger programmatically':
      'POP-API-07, POP-FOC-05: with the focus manager wired it passes on Server; on WebAssembly focus does not return to the ' +
      'external "Open Trigger 2" after a programmatic switch and close (the return target the focus-manager module records ' +
      'on WebAssembly\'s synchronous update), under investigation in track 5',
  },
});

// The first run of viewportConformanceTests (track 1 wrote it for the families): PopoverViewport.test.tsx's config.
viewportConformanceTests({
  fixture: 'popover-viewport',
  openOn: 'click',
});

// The first run of popupFocusPropsTests (track 1 wrote it for the families): PopoverPopup.test.tsx's config.
popupFocusPropsTests({
  fixture: 'popover-focus',
});

// POP-KEY-12 (PopoverPopup.tsx:95-99), PopoverPopup.test.tsx's two titles verbatim on `toolbar-popover` with `inside`: the
// portal's container is inside the toolbar root, so a keydown in the popup bubbles to the toolbar's composite as React's
// portaled events do in Base UI's test (a popup portaled to the body would pass without the stop).
test.describe('<Popover.Popup /> inside a toolbar', () => {
  test('does not relay composite keys from the popup to the toolbar', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'toolbar-popover', { inside: 'button' });

    // The toolbar itself still navigates with the same key, so a passing assertion below can't come from an inert toolbar.
    await page.keyboard.press('Tab');
    await expect(page.getByRole('button', { name: 'First' })).toBeFocused();
    await page.keyboard.press('ArrowRight');
    const trigger = page.getByTestId('trigger');
    await expect(trigger).toBeFocused();

    await page.keyboard.press('Enter');
    const insideButton = page.getByRole('button', { name: 'Inside' });
    await expect(insideButton).toBeFocused();

    await page.keyboard.press('ArrowRight');
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => resolve(undefined))));

    await expect(insideButton).toBeFocused();
    await expect(page.getByRole('button', { name: 'Last' })).not.toBeFocused();
  });

  test('keeps composite keys working inside the popup content', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'toolbar-popover', { inside: 'input' });

    await page.getByTestId('trigger').click();

    const input = page.getByRole('textbox');
    await expect(input).toBeFocused();

    await input.evaluate((element) => (element as HTMLInputElement).setSelectionRange(0, 0));
    await page.keyboard.press('ArrowRight');

    expect(await input.evaluate((element) => (element as HTMLInputElement).selectionStart)).toBe(1);
    await expect(page.getByRole('button', { name: 'Last' })).not.toBeFocused();
  });
});

// Design point 2 of M4 track 5 (settled by the orchestrator 2026-10-28): JavaScript inserts `[data-previous]` before C#'s
// keyed `[data-current]`. The issue-8-style proof on both modes: mid-switch, a C# re-render keeps the inserted container
// and Blazor's own nodes in place, and disposing the popup removes it with nothing left behind (the suite's lagging-payload
// title is the C# remount).
test.describe('<Popover.Viewport /> mid-switch', () => {
  test('port: a C# re-render keeps [data-previous] before [data-current], and a dispose removes both', async ({ page, mode }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    const fixture = await gotoPopupFixture(page, mode, 'popover-viewport', { animations: 1 });
    await page.addStyleTag({
      content: '@keyframes popover-hold { from { opacity: 0.5; } } [data-testid="viewport"] [data-current] { animation: popover-hold 10s; }',
    });
    const viewport = page.getByTestId('viewport');

    await page.getByTestId('trigger-1').click();
    await expect(viewport.locator('[data-current]')).toHaveText('Content 1');
    await page.getByTestId('trigger-2').click();
    await expect(viewport.locator('[data-previous]')).toHaveText('Content 1');

    await settle(fixture);

    await expect(viewport.locator(':scope > [data-previous] + [data-current]')).toHaveText('Content 2');
    await expect(viewport.locator('[data-previous]')).toHaveCount(1);

    await pressControl(fixture.locator('[data-unmount]'));

    await expect(page.locator('[data-previous]')).toHaveCount(0);
    await expect(page.getByTestId('viewport')).toHaveCount(0);
    expect(errors).toEqual([]);
  });
});

