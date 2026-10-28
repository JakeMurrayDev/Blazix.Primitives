import { expect, test } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPopupFixture } from './suites/popupFixture';
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

