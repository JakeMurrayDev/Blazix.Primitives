import { expect, swipeOnTimeline, test, type Page, type RenderMode } from './helpers';
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

// M6 track 8b (issue 279), slice 1: the Viewport's mouse and pen dismiss gesture. The module's own contract is pinned at L3
// (js/drawer.test.ts) and its C# half at L2 (DrawerViewportGestureTests); these titles are Base UI's browser titles.
const swipeCase = (page: Page, mode: RenderMode, name: string, options: Record<string, string> = {}) =>
  gotoPopupFixture(page, mode, 'drawer-swipe', { case: name, ...options });

test.describe('<Drawer.Root /> swipe', () => {
  // DRW-PTR-03, PTR-10 (DrawerRoot.test.tsx:600-633): with a 200 px wide popup the threshold is 100 px; 90 px keeps the
  // drawer open and 120 px closes it. Both drags run on a 200 ms step, slower than the 0.5 px/ms fast path, as Base UI's
  // timed swipes are, and from the popup's point (Base UI stubs elementFromPoint with the popup).
  test('uses a size-based swipe threshold', async ({ page, mode }) => {
    const fixture = await swipeCase(page, mode, 'right');
    const popup = page.getByTestId('popup');

    await swipeOnTimeline(popup, { x: 100, y: 100 }, { x: 190, y: 100 }, { startTimeMs: 1000, timeStepMs: 200 });
    await expect(popup).toBeVisible();
    await expect(fixture).toHaveAttribute('data-open-changes', '');

    await swipeOnTimeline(popup, { x: 100, y: 100 }, { x: 220, y: 100 }, { startTimeMs: 2000, timeStepMs: 200 });
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });
});
