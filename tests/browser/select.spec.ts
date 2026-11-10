// L4 for docs/components/select.md (M5 track 9, issue 173): Server then WebAssembly.
import { delayCircuit, expect, test, throttleCpu } from './helpers';
import { waitForPositioned } from './helpers/positioner';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPopupFixture } from './suites/popupFixture';
import { popupFocusPropsTests } from './suites/popupFocusPropsTests';
import { popupListConformanceTests } from './suites/popupListConformanceTests';
import { positionerConformanceTests } from './suites/positioner-conformance';

// The shared suites with Select's options: SelectRoot.test.tsx:367 (popupConformanceTests), :389 (popupListConformanceTests),
// SelectPopup.test.tsx:1728 (popupFocusPropsTests) and SelectPositioner.test.tsx:36 (positionerConformanceTests).
popupConformanceTests({
  fixture: 'select-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'listbox',
  alwaysMounted: true,
});

test.describe('Select', () => {
  popupListConformanceTests({
    fixture: 'select-list',
    itemRole: 'option',
    selectable: true,
    listEnd: 'stop',
  });
});

popupFocusPropsTests({
  fixture: 'select-focus',
  initialFocus: false,
  triggerRole: 'combobox',
  alwaysMounted: true,
  // Base UI passes `mouseCloseType: ''` for its known gap (an item press closes through the store's own setOpen, which
  // skips the event that records the close type). The port's item press closes through the open request with the
  // click's pointer type, so finalFocus receives `mouse` (design point N3 of M5 track 9, pending the user).
});

test.describe('Select.Positioner', () => {
  positionerConformanceTests({ fixture: 'select-positioner' });
});

test.describe('Select: smoke', () => {
  test('opens on a trigger press, selects an item with a click and closes', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-basic', { align: 'false' });
    const trigger = page.getByTestId('trigger');
    await expect(page.getByTestId('value')).toHaveText('Select apple');
    await trigger.click();
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await page.getByRole('option', { name: 'Fuji' }).click();
    await expect(fixture).toHaveAttribute('data-value-changes', 'fuji:item-press');
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('value')).toHaveText('Fuji');
  });

  // SEL-PTR-08 (port title, M5 track 9): the Positioner's `select` handle classifies the items' clicks, so it attaches as
  // soon as the force-mounted Popup renders, not after the `positioning` handle that only an open attaches. With a slow
  // circuit a click right after the open otherwise found no handle (final review F4's change, reverted in 4113a016).
  test('port: selects an item clicked right after the open on a slow circuit', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'the circuit delay is a Server round trip');
    await delayCircuit(page, 60);
    const fixture = await gotoPopupFixture(page, mode, 'select-basic', { align: 'false' });
    const trigger = page.getByTestId('trigger');
    await trigger.click();
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await page.getByRole('option', { name: 'Fuji' }).click();
    await expect(fixture).toHaveAttribute('data-value-changes', 'fuji:item-press');
  });

  test('places the popup over the trigger by default and aligns the selected text with the value', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-basic', { defaultValue: 'fuji' });
    const trigger = page.getByTestId('trigger');
    await page.locator('body').evaluate((body) => {
      body.style.paddingTop = '200px';
    });
    await trigger.click();
    const positioner = page.getByTestId('positioner');
    await expect(positioner).toHaveAttribute('data-side', 'none');
    await expect(page.getByTestId('popup')).toHaveAttribute('data-side', 'none');
    await waitForPositioned(positioner);
    const value = await page.getByTestId('value').boundingBox();
    const text = await page.getByRole('option', { name: 'Fuji' }).locator('div').last().boundingBox();
    expect(Math.abs((text!.y + text!.height / 2) - (value!.y + value!.height / 2))).toBeLessThan(2);
    expect(Math.abs(text!.x - value!.x)).toBeLessThan(2);
  });
});

test.describe('Select: keyboard open', () => {
  // SEL-KEY-01, SEL-STR-06 (port title, M5 track 9): focusing the trigger force-mounts the popup on a 0 ms timer
  // (SelectTrigger.tsx's onFocus), and an ArrowDown right after it opens the list; on a slow page the force-mount's
  // render lands between the key and the open. The list still opens on the first item, as Base UI's effects order it.
  test('port: a keyboard open racing the trigger\'s force-mount still opens on the first item', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-list', { itemStyle: 'height: 30px', scrollerStyle: 'height: 100px; overflow-y: auto' });
    await throttleCpu(page, 6);
    const trigger = page.getByTestId('trigger');
    await trigger.focus();
    await page.keyboard.press('ArrowDown');

    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await expect(page.getByRole('option', { name: 'Item 0' })).toHaveAttribute('data-highlighted', '');
    await expect(page.getByRole('option', { name: 'Item 0' })).toBeFocused();
  });
});

