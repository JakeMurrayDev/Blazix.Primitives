import { expect, test, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// The [L4] rows of docs/components/drawer.md (DRW), both render modes, Server first (ADR 04 §6). Titles are Base UI's
// (drawer/**/*.test.tsx at v1.9.0) unless marked "port:". M6 track 8a (issue 279): the rows that need only the C# parts and
// Dialog's modules; the gesture rows come with the `drawer` module (8b).

const popupCase = (page: Page, mode: RenderMode, name: string) => gotoPopupFixture(page, mode, 'drawer-popup', { case: name });

test.describe('<Drawer.Popup />', () => {
  // DRW-API-07, FOC-01: with no InitialFocus the popup itself takes focus, not its first input.
  test('defaults initial focus to the popup element', async ({ page, mode }) => {
    await popupCase(page, mode, 'focus');

    await page.getByRole('button', { name: 'Open' }).click();

    await expect(page.getByTestId('popup')).toBeFocused();
    await expect(page.getByTestId('popup-input')).not.toBeFocused();
  });

  // DRW-API-07, FOC-02.
  test('leaves focus on the trigger when initial focus is disabled', async ({ page, mode }) => {
    await popupCase(page, mode, 'focus-disabled');
    const trigger = page.getByRole('button', { name: 'Open' });

    await trigger.click();

    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(trigger).toBeFocused();
  });

  // DRW-KEY-01 (the DLG-KEY-09 port of issue 123): Base UI's wrapper `onKeyDown` sees the portaled popup's keydowns through
  // React's tree; here the portal's container is inside the wrapper, so its native listener sees them through the DOM,
  // and real key presses stand for Base UI's fireEvent.keyDown (DrawerPopup.test.tsx:145-168).
  test('stops composite navigation keys from escaping the popup', async ({ page, mode }) => {
    await popupCase(page, mode, 'composite');
    const wrapper = page.getByTestId('wrapper');
    await wrapper.evaluate((element) => {
      (window as unknown as { drawerKeys: string[] }).drawerKeys = [];
      element.addEventListener('keydown', (event) => (window as unknown as { drawerKeys: string[] }).drawerKeys.push((event as KeyboardEvent).key));
    });
    const field = page.getByRole('textbox', { name: 'Field' });

    await field.focus();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('a');

    expect(await page.evaluate(() => (window as unknown as { drawerKeys: string[] }).drawerKeys)).toEqual(['a']);
  });
});
