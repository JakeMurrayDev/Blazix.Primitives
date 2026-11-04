// L4 for docs/components/select.md (M5 track 9, issue 173): Server then WebAssembly.
import { expect, test } from './helpers';
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
    const value = await page.getByTestId('value').boundingBox();
    const text = await page.getByRole('option', { name: 'Fuji' }).locator('div').last().boundingBox();
    expect(Math.abs((text!.y + text!.height / 2) - (value!.y + value!.height / 2))).toBeLessThan(2);
    expect(Math.abs(text!.x - value!.x)).toBeLessThan(2);
  });
});
