import { expect, pointOf, swipeOnTimeline, test, testWithTouch, touchDrag, type Page, type RenderMode } from './helpers';
import type { Locator } from '@playwright/test';
import { gotoPopupFixture, settle } from './suites/popupFixture';

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

// Slice 1's remaining browser titles (M6 track 8b, issue 279, added 2026-11-19), on the `down` case of `drawer-swipe`.
// Base UI fires these with `fireEvent` on the Viewport and stubs `elementFromPoint` with the Popup. Here a real mouse
// stands in where one can make the event. Elsewhere (a pointercancel, a selection a real press would collapse) a
// PointerEvent is dispatched in the page on the Viewport at a point inside the Popup, so the real hit test finds the
// Popup (ADR 04 Amendments 2026-11-19, the ScrollArea track's point 14: in-page dispatch where no real device makes
// the event, each with this note).
const downCase = (page: Page, mode: RenderMode, options: Record<string, string> = {}) =>
  gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'down', ...options });

interface PointerInit {
  x: number;
  y: number;
  buttons?: number;
  button?: number;
  pointerType?: string;
  pointerId?: number;
}

/** Dispatches one PointerEvent on the element with the test id, at a viewport point. */
const dispatchPointer = (page: Page, testId: string, type: string, init: PointerInit) =>
  page.getByTestId(testId).evaluate(
    (element, { type, init }) => {
      element.dispatchEvent(
        new PointerEvent(type, {
          bubbles: true,
          cancelable: true,
          composed: true,
          isPrimary: true,
          pointerId: init.pointerId ?? 1,
          pointerType: init.pointerType ?? 'mouse',
          button: init.button ?? (type === 'pointermove' ? -1 : 0),
          buttons: init.buttons ?? (type === 'pointerup' || type === 'pointercancel' ? 0 : 1),
          clientX: init.x,
          clientY: init.y,
        }),
      );
    },
    { type, init },
  );

/** A point in the popup's free area under its regions (the popup is 300 px tall from top 100). */
const popupPoint = { x: 100, y: 390 };

test.describe('<Drawer.Viewport /> swipe', () => {
  // DRW-PTR-01 (DrawerViewport.test.tsx:3534-3590).
  test('toggles data-swiping on the backdrop while swiping', async ({ page, mode }) => {
    await downCase(page, mode);
    const backdrop = page.getByTestId('backdrop');

    await page.mouse.move(popupPoint.x, popupPoint.y);
    await page.mouse.down();
    await page.mouse.move(popupPoint.x, popupPoint.y + 8, { steps: 2 });
    await expect(backdrop).toHaveAttribute('data-swiping', '');

    await page.mouse.up();
    await expect(backdrop).not.toHaveAttribute('data-swiping');
  });

  // DRW-PTR-01 (DrawerViewport.test.tsx:4622-4692). A move whose `buttons` lacks the primary button is a chord: the right
  // button pressed, then the left released. Base UI fires `buttons: 2`; no real device makes that move alone.
  test('ends swipe drag when the primary mouse button is released mid-gesture', async ({ page, mode }) => {
    await downCase(page, mode);
    const backdrop = page.getByTestId('backdrop');

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y });
    await dispatchPointer(page, 'viewport', 'pointermove', { x: popupPoint.x, y: popupPoint.y + 8 });
    await expect(backdrop).toHaveAttribute('data-swiping', '');

    await dispatchPointer(page, 'viewport', 'pointermove', { x: popupPoint.x, y: popupPoint.y + 12, buttons: 2 });
    await expect(backdrop).not.toHaveAttribute('data-swiping');

    await dispatchPointer(page, 'viewport', 'pointermove', { x: popupPoint.x, y: popupPoint.y + 30, buttons: 0 });
    await expect(backdrop).not.toHaveAttribute('data-swiping');
  });

  // DRW-PTR-01, PTR-05 (DrawerViewport.test.tsx:3592-3642): no real device makes a pointercancel.
  test('cancels pointer drags and ignores compatibility touch pointer cancellation', async ({ page, mode }) => {
    await downCase(page, mode);
    const backdrop = page.getByTestId('backdrop');

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y });
    await dispatchPointer(page, 'viewport', 'pointermove', { x: popupPoint.x, y: popupPoint.y + 20 });
    await expect(backdrop).toHaveAttribute('data-swiping', '');

    await dispatchPointer(page, 'viewport', 'pointercancel', { x: popupPoint.x, y: popupPoint.y + 20 });
    await expect(backdrop).not.toHaveAttribute('data-swiping');

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y, pointerId: 2, pointerType: 'touch' });
    await dispatchPointer(page, 'viewport', 'pointercancel', { x: popupPoint.x, y: popupPoint.y, pointerId: 2, pointerType: 'touch' });
    await expect(backdrop).not.toHaveAttribute('data-swiping');
  });

  // DRW-PTR-04 (DrawerViewport.test.tsx:1216-1254). Base UI checks the press alone; the drag past the threshold here
  // also shows nothing starts.
  test('does not start non-touch swipes from Drawer.Content', async ({ page, mode }) => {
    const fixture = await downCase(page, mode);
    const box = (await page.getByTestId('content-target').boundingBox())!;

    await page.mouse.move(box.x + 50, box.y + 50);
    await page.mouse.down();
    await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
    await page.mouse.move(box.x + 50, box.y + 250, { steps: 4 });
    await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
    await page.mouse.up();

    // The barrier: on Server a close the release requested would have reached C# by now.
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-changes', '');
  });

  // DRW-PTR-04, PTR-09 (DrawerViewport.test.tsx:4807-4866). Folded with its positive half (the review of 2026-11-19):
  // the same press first starts a swipe on the open drawer, so the absence after the close shows the open gate. A planted
  // fault that drops the gate alone leaves the absence standing, because the closed popup is hidden from the hit test.
  test('does not start pointer swipes while a closed drawer remains mounted', async ({ page, mode }) => {
    const fixture = await downCase(page, mode, { retainEveryClose: 'true' });
    const popup = page.getByTestId('popup');
    const backdrop = page.getByTestId('backdrop');

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y });
    await dispatchPointer(page, 'viewport', 'pointermove', { x: popupPoint.x, y: popupPoint.y + 20 });
    await expect(backdrop).toHaveAttribute('data-swiping', '');
    await dispatchPointer(page, 'viewport', 'pointerup', { x: popupPoint.x, y: popupPoint.y + 20 });
    await expect(backdrop).not.toHaveAttribute('data-swiping');

    await page.getByRole('button', { name: 'Close' }).click();
    await expect(popup).toHaveAttribute('data-closed', '');
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:close-press');

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y });
    await dispatchPointer(page, 'viewport', 'pointermove', { x: popupPoint.x, y: popupPoint.y + 100 });
    await expect(backdrop).not.toHaveAttribute('data-swiping');
    await dispatchPointer(page, 'viewport', 'pointerup', { x: popupPoint.x, y: popupPoint.y + 100 });
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:close-press');
  });

  // DRW-PTR-09 (DrawerViewport.test.tsx:4694-4736).
  test('ignores swipe input until a popup is mounted', async ({ page, mode }) => {
    const fixture = await downCase(page, mode, { noPopup: 'true' });

    await page.mouse.move(10, 10);
    await page.mouse.down();
    await page.mouse.move(10, 110, { steps: 4 });
    await page.mouse.up();

    // The barrier: on Server a close the release requested would have reached C# by now.
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-changes', '');
  });

  // DRW-PTR-08 (DrawerViewport.test.tsx:101-152): dispatched, because a real press collapses the selection itself.
  test('clears text selection on swipe start', async ({ page, mode }) => {
    await downCase(page, mode);
    await page.getByTestId('text').evaluate((text) => {
      const range = document.createRange();
      range.setStart(text.firstChild!, 0);
      range.setEnd(text.firstChild!, 5);
      document.getSelection()!.removeAllRanges();
      document.getSelection()!.addRange(range);
    });

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y });

    expect(await page.evaluate(() => document.getSelection()!.rangeCount)).toBe(0);
  });

  // DRW-PTR-08 (DrawerViewport.test.tsx:154-202).
  test('preserves text selection outside the popup when a swipe starts', async ({ page, mode }) => {
    await downCase(page, mode);
    await page.getByTestId('outside-text').evaluate((text) => {
      const range = document.createRange();
      range.setStart(text.firstChild!, 0);
      range.setEnd(text.firstChild!, 7);
      document.getSelection()!.removeAllRanges();
      document.getSelection()!.addRange(range);
    });

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y });

    expect(await page.evaluate(() => [document.getSelection()!.rangeCount, document.getSelection()!.toString()])).toEqual([1, 'Outside']);
  });

  // DRW-PTR-08 (DrawerViewport.test.tsx:4868-4904). Base UI mocks the selection with the popup as both endpoints; here
  // the real selection is set on the popup element itself.
  test('clears a selection whose endpoints are popup elements', async ({ page, mode }) => {
    await downCase(page, mode);
    await page.getByTestId('popup').evaluate((popup) => {
      document.getSelection()!.setBaseAndExtent(popup, 0, popup, popup.childNodes.length);
    });
    expect(await page.evaluate(() => document.getSelection()!.isCollapsed)).toBe(false);

    await dispatchPointer(page, 'viewport', 'pointerdown', { x: popupPoint.x, y: popupPoint.y });

    expect(await page.evaluate(() => document.getSelection()!.rangeCount)).toBe(0);
  });
});

test.describe('<Drawer.Root /> swipe close', () => {
  // DRW-PTR-03, PTR-10 (DrawerRoot.test.tsx:1726-1748): 149 px over a second, past half the 300 px popup only with a
  // longer drag; here 200 px over a second.
  test('closes an uncontrolled drawer after a slow long drag', async ({ page, mode }) => {
    await downCase(page, mode);
    const popup = page.getByTestId('popup');

    await swipeOnTimeline(popup, popupPoint, { x: popupPoint.x, y: popupPoint.y + 200 }, { startTimeMs: 1000, timeStepMs: 333 });

    await expect(popup).toHaveCount(0);
  });

  // DRW-PTR-14 (DrawerRoot.test.tsx:1109-1135): a fast swipe past the threshold whose close the parent cancels.
  test('clears swipe-dismiss styles when swipe close is canceled', async ({ page, mode }) => {
    const fixture = await downCase(page, mode, { cancelClose: 'true' });
    const popup = page.getByTestId('popup');
    const backdrop = page.getByTestId('backdrop');

    await swipeOnTimeline(popup, popupPoint, { x: popupPoint.x, y: popupPoint.y + 200 }, { startTimeMs: 1000, timeStepMs: 10 });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
    await expect(popup).not.toHaveAttribute('data-swipe-dismiss');
    await expect(backdrop).not.toHaveAttribute('data-swipe-dismiss');
    await expect(popup).not.toHaveAttribute('data-ending-style');
    await expect(backdrop).not.toHaveAttribute('data-swiping');
    await expect(popup).toHaveAttribute('data-open', '');
    await expect.poll(() => popup.evaluate((element) => element.style.getPropertyValue('--drawer-swipe-movement-y'))).toBe('0px');
  });

  // DRW-PTR-15 (DrawerRoot.test.tsx:1136-1170).
  test('does not dismiss a controlled drawer via swipe when open is always true', async ({ page, mode }) => {
    const fixture = await downCase(page, mode, { alwaysOpen: 'true' });
    const popup = page.getByTestId('popup');
    const backdrop = page.getByTestId('backdrop');

    await swipeOnTimeline(popup, popupPoint, { x: popupPoint.x, y: popupPoint.y + 200 }, { startTimeMs: 1000, timeStepMs: 10 });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
    await expect(popup).not.toHaveAttribute('data-swipe-dismiss');
    await expect(backdrop).not.toHaveAttribute('data-swipe-dismiss');
    await expect(popup).not.toHaveAttribute('data-ending-style');
    await expect(popup).toHaveAttribute('data-open', '');
  });
});

// Slice 2 (M6 track 8b, issue 279, added 2026-11-19): the Viewport's touch path, with real CDP touch (`touchDrag`) on a
// page whose context has touch. Chromium sends no touchmove for a first move under its 10 px slop, so every drag moves in
// steps of more than that.
test.describe('<Drawer.Viewport /> touch swipe', () => {
  // DRW-PTR-20 (DrawerViewport.test.tsx:55-99). Base UI spies on the document's listeners; here an init script records
  // the non-passive touchmove registrations the page makes.
  testWithTouch('registers the non-passive document touchmove listener only while open', async ({ touchPage: page, mode }) => {
    await page.addInitScript(() => {
      const log: string[] = [];
      (window as unknown as { touchMoveLog: string[] }).touchMoveLog = log;
      const add = Document.prototype.addEventListener;
      const remove = Document.prototype.removeEventListener;
      // Only non-passive registrations: other modules add passive touchmove listeners of their own.
      const nonPassive = new Set<EventListenerOrEventListenerObject>();
      Document.prototype.addEventListener = function (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) {
        if (type === 'touchmove' && typeof options === 'object' && options.passive === false) {
          nonPassive.add(listener);
          log.push(`add passive=${String(options.passive)} capture=${String(options.capture)}`);
        }
        add.call(this, type, listener, options);
      };
      Document.prototype.removeEventListener = function (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) {
        if (type === 'touchmove' && nonPassive.delete(listener)) {
          log.push('remove');
        }
        remove.call(this, type, listener, options);
      };
    });
    await downCase(page, mode);
    const log = () => page.evaluate(() => (window as unknown as { touchMoveLog: string[] }).touchMoveLog);
    await expect.poll(log).toEqual(['add passive=false capture=true']);

    await page.getByRole('button', { name: 'Close' }).click();
    await expect(page.getByTestId('popup')).toHaveCount(0);
    await expect.poll(log).toEqual(['add passive=false capture=true', 'remove']);
  });

  // DRW-PTR-07 (DrawerViewport.test.tsx:257-295): the ignore selector does not apply to touch.
  testWithTouch('starts touch swipes from interactive elements', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const start = await pointOf(page.getByRole('button', { name: 'Close' }));

    await touchDrag(page, [[start, { x: start.x, y: start.y + 250 }]], { steps: 10 });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });

  // DRW-PTR-19 (DrawerViewport.test.tsx:745-799). Folded with its positive half: the same drag from the popup's free area
  // dismisses.
  testWithTouch('does not start touch swipes from elements with data-base-ui-swipe-ignore', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const ignored = await pointOf(page.getByTestId('ignored'));

    await touchDrag(page, [[ignored, { x: ignored.x, y: ignored.y + 250 }]], { steps: 10 });
    await settle(fixture);
    await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
    await expect(fixture).toHaveAttribute('data-open-changes', '');

    await touchDrag(page, [[popupPoint, { x: popupPoint.x, y: popupPoint.y + 250 }]], { steps: 10 });
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });

  // DRW-PTR-24 (DrawerViewport.test.tsx:1154-1214).
  testWithTouch('still allows touch swipes from elements with legacy data-swipe-ignore', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const legacy = await pointOf(page.getByTestId('legacy'));

    await touchDrag(page, [[legacy, { x: legacy.x, y: legacy.y + 250 }]], { steps: 10 });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });

  // DRW-PTR-06 (DrawerViewport.test.tsx:1306-1375): the touch starts on the viewport above the popup.
  testWithTouch('dismisses when touch starts outside the popup, then continues swiping down inside it', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);

    await touchDrag(page, [[{ x: 100, y: 50 }, { x: 100, y: 500 }]], { steps: 15 });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });

  // DRW-PTR-21 (d) (DrawerViewport.test.tsx:1839-1884): a window capture listener sees each move first and reads its
  // defaultPrevented after the module's document listener ran.
  testWithTouch('prevents touchmove when there is no scroll container', async ({ touchPage: page, mode }) => {
    await downCase(page, mode);
    await page.evaluate(() => {
      const moves: TouchEvent[] = [];
      (window as unknown as { drawerMoves: TouchEvent[] }).drawerMoves = moves;
      window.addEventListener('touchmove', (event) => moves.push(event), { capture: true, passive: true });
    });

    await touchDrag(page, [[popupPoint, { x: popupPoint.x, y: popupPoint.y + 60 }]], { steps: 4 });

    const prevented = await page.evaluate(() => (window as unknown as { drawerMoves: TouchEvent[] }).drawerMoves.map((event) => event.defaultPrevented));
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.every(Boolean)).toBe(true);
  });
});

// Slice 2's scroll-edge titles (DRW-PTR-21 (f), ALG-03's edges), real CDP touch on the fixture's 60 px scroll container.
// The first move stays inside the container (25 px; Chromium swallowed every move up to 12 px): the tracker starts a swipe
// from a scroll edge only when the move's point is still over the scroller, as Base UI's jsdom titles pin the hit test to
// it.
// Base UI reads `fireEvent`'s result and an `onTouchMove` on the container; here a window capture listener reads each
// move's defaultPrevented and a listener on the container counts the moves that reach it.
const recordScrollMoves = (page: Page) =>
  page.evaluate(() => {
    const record = { prevented: [] as boolean[], reached: 0 };
    (window as unknown as { scrollMoves: typeof record }).scrollMoves = record;
    const moves: TouchEvent[] = [];
    window.addEventListener('touchmove', (event) => moves.push(event), { capture: true, passive: true });
    document.querySelector('[data-testid="scroll"]')!.addEventListener('touchmove', () => (record.reached += 1), { passive: true });
    // The page reads defaultPrevented after every listener ran.
    window.addEventListener('touchend', () => (record.prevented = moves.map((event) => event.defaultPrevented)), { capture: true });
  });
const scrollMoves = (page: Page) => page.evaluate(() => (window as unknown as { scrollMoves: { prevented: boolean[]; reached: number } }).scrollMoves);

test.describe('<Drawer.Viewport /> touch swipe over a scroll container', () => {
  // DRW-PTR-21 (DrawerViewport.test.tsx:1604-1650): at the top, a drag toward the dismiss direction is the drawer's.
  testWithTouch('prevents touchmove at scroll top when swiping down on scrollable content', async ({ touchPage: page, mode }) => {
    await downCase(page, mode);
    await recordScrollMoves(page);
    const start = await pointOf(page.getByTestId('scroll'));

    await touchDrag(page, [[start, { x: start.x, y: start.y + 25 }, { x: start.x, y: start.y + 60 }]], { steps: 1 });

    const { prevented, reached } = await scrollMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.every(Boolean)).toBe(true);
    expect(reached).toBe(0);
  });

  // DRW-PTR-21 (DrawerViewport.test.tsx:2214-2260): at the top, a drag the other way scrolls the content.
  testWithTouch('allows touchmove when scrolling down from scroll top', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    await recordScrollMoves(page);
    const scroll = page.getByTestId('scroll');
    const start = await pointOf(scroll);

    await touchDrag(page, [[start, { x: start.x, y: start.y - 60 }]], { steps: 4 });

    const { prevented, reached } = await scrollMoves(page);
    expect(prevented.some(Boolean)).toBe(false);
    expect(reached).toBeGreaterThan(0);
    await expect.poll(() => scroll.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-changes', '');
  });

  // DRW-PTR-21, PTR-10 (DrawerViewport.test.tsx:2513-2582).
  testWithTouch('dismisses from a top-edge scroll container with a touch swipe down', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const start = await pointOf(page.getByTestId('scroll'));

    await touchDrag(page, [[start, { x: start.x, y: start.y + 25 }, { x: start.x, y: start.y + 250 }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });
});

// Slice 2's other scroll edges (added 2026-11-20): the fixture's `case=scroll` popup sits at left 300, top 300 (200 by
// 300 px) with a vertical scroller (60 px over 120, y 300-360) and a horizontal one (120 px over 360, x 300-420, y
// 360-400); `direction` is the swipe direction. Base UI stubs the scroll offsets; here they are set before the drag.
const scrollCase = async (page: Page, mode: RenderMode, direction: string, offsets: { scrollTop?: number | 'max'; scrollLeft?: number | 'max' } = {}) => {
  const fixture = await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'scroll', direction });
  await page.evaluate((offsets) => {
    const scroll = document.querySelector<HTMLElement>('[data-testid="scroll"]')!;
    const hscroll = document.querySelector<HTMLElement>('[data-testid="hscroll"]')!;
    if (offsets.scrollTop !== undefined) {
      scroll.scrollTop = offsets.scrollTop === 'max' ? scroll.scrollHeight - scroll.clientHeight : offsets.scrollTop;
    }
    if (offsets.scrollLeft !== undefined) {
      hscroll.scrollLeft = offsets.scrollLeft === 'max' ? hscroll.scrollWidth - hscroll.clientWidth : offsets.scrollLeft;
    }
  }, offsets);
  return fixture;
};
const scrollCenter = { x: 400, y: 330 };
const hscrollCenter = { x: 360, y: 380 };

test.describe('<Drawer.Viewport /> touch swipe from other scroll edges', () => {
  // DRW-PTR-21 (DrawerViewport.test.tsx:1652-1702): at the bottom, a drag toward an up drawer's dismiss direction is the
  // drawer's.
  testWithTouch('prevents touchmove at scroll bottom when swiping up on scrollable content', async ({ touchPage: page, mode }) => {
    await scrollCase(page, mode, 'up', { scrollTop: 'max' });
    await recordScrollMoves(page);
    const { x, y } = scrollCenter;

    await touchDrag(page, [[{ x, y }, { x, y: y - 25 }, { x, y: y - 60 }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    const { prevented, reached } = await scrollMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.every(Boolean)).toBe(true);
    expect(reached).toBe(0);
  });

  // "starts swipe-to-dismiss after a scrollable container reaches the dismiss edge" (DRW-PTR-21, ALG-03;
  // DrawerViewport.test.tsx:2445-2511) is ported at L3 (js/drawer.test.ts), 2026-11-20: here the drag from scrollTop 20
  // scrolled the content to 0, and Chromium sent every move after the first as non-cancelable (cancelable, y, scrollTop:
  // [true,330,20], [false,343,0], [false,355,0], [false,403,0], [false,450,0]), so Base UI's own condition, a cancelable
  // move toward the dismiss edge, never holds in one real gesture.

  // DRW-PTR-21, PTR-10 (DrawerViewport.test.tsx:2584-2653).
  testWithTouch('dismisses from a bottom-edge scroll container with a touch swipe up', async ({ touchPage: page, mode }) => {
    const fixture = await scrollCase(page, mode, 'up', { scrollTop: 'max' });
    const { x, y } = scrollCenter;

    await touchDrag(page, [[{ x, y }, { x, y: y - 25 }, { x, y: y - 250 }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });

  // DRW-PTR-21, PTR-10 (DrawerViewport.test.tsx:2655-2724).
  testWithTouch('dismisses from a left-edge horizontal scroll container with a touch swipe right', async ({ touchPage: page, mode }) => {
    const fixture = await scrollCase(page, mode, 'right', { scrollLeft: 0 });
    const { x, y } = hscrollCenter;

    await touchDrag(page, [[{ x, y }, { x: x + 25, y }, { x: x + 250, y }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });

  // DRW-PTR-21, PTR-10 (DrawerViewport.test.tsx:2726-2795).
  testWithTouch('dismisses from a right-edge horizontal scroll container with a touch swipe left', async ({ touchPage: page, mode }) => {
    const fixture = await scrollCase(page, mode, 'left', { scrollLeft: 'max' });
    const { x, y } = hscrollCenter;

    await touchDrag(page, [[{ x, y }, { x: x - 25, y }, { x: x - 250, y }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });

  // DRW-PTR-21, PTR-22 (DrawerViewport.test.tsx:2898-2948): the vertical scroller is the cross axis of a right drawer.
  testWithTouch('allows horizontal swipe dismiss from a vertical scroll container', async ({ touchPage: page, mode }) => {
    await scrollCase(page, mode, 'right', { scrollTop: 20 });
    const { x, y } = scrollCenter;

    await touchDrag(page, [[{ x, y }, { x: x + 25, y }, { x: x + 80, y }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });
  });

  // DRW-PTR-21 (a) (DrawerViewport.test.tsx:5421-5451). Folded with its positive half: the first finger's drag alone
  // moves the popup. Base UI's jsdom title also finds no data-swiping, because jsdom's hit test misses the popup at the
  // touch start; in a browser the touch on the popup starts the swipe at once, so here the title asserts what the
  // browser keeps: the moves are not prevented and the popup does not follow the fingers. The root is always open, so a
  // dismissal attempt leaves the popup in place.
  testWithTouch('leaves pinch-zoom touchmove to the browser', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode, { alwaysOpen: 'true' });
    const popup = page.getByTestId('popup');
    const top = () => popup.evaluate((element) => Math.round(element.getBoundingClientRect().top));
    const first = [popupPoint, { x: popupPoint.x, y: popupPoint.y + 30 }, { x: popupPoint.x, y: popupPoint.y + 60 }];

    await touchDrag(page, [first], { steps: 1, beforeRelease: () => expect.poll(top).toBeGreaterThan(100) });
    await settle(fixture);
    await expect.poll(top).toBe(100);

    await recordScrollMoves(page);
    const second = [{ x: 150, y: popupPoint.y }, { x: 150, y: popupPoint.y - 30 }, { x: 150, y: popupPoint.y - 60 }];
    await touchDrag(page, [first, second], {
      steps: 1,
      beforeRelease: async () => {
        await settle(fixture);
        expect(await top()).toBe(100);
      },
    });

    const { prevented } = await scrollMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.some(Boolean)).toBe(false);
  });
});

// Slice 2's cross-axis arbitration (DRW-PTR-22, ALG-03; added 2026-11-20): a right drawer over the fixture's vertical
// scroller. Chromium dispatches no move under its touch slop (12 px was swallowed), so the first move these titles send
// is already past the port's 6 px slop on one axis; the sub-slop and non-cancelable paths are held at L3.
const movementX = (page: Page) => page.getByTestId('popup').evaluate((element) => (element as HTMLElement).style.getPropertyValue('--drawer-swipe-movement-x'));

test.describe('<Drawer.Viewport /> touch swipe over a cross-axis scroll container', () => {
  // DRW-PTR-22 (DrawerViewport.test.tsx:3001-3047). Base UI's sub-slop first move never reaches the page here; the
  // L3 port holds it.
  testWithTouch('claims the gesture once the drawer axis passes the slop over cross-axis scrollable content', async ({ touchPage: page, mode }) => {
    await scrollCase(page, mode, 'right');
    await recordScrollMoves(page);
    const { x, y } = scrollCenter;

    await touchDrag(page, [[{ x, y }, { x: x + 25, y: y + 1 }, { x: x + 40, y: y + 1 }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    const { prevented } = await scrollMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.every(Boolean)).toBe(true);
    expect(await page.getByTestId('scroll').evaluate((element) => element.scrollTop)).toBe(0);
  });

  // DRW-PTR-22 (DrawerViewport.test.tsx:3049-3104). Base UI commits the gesture with a non-cancelable sub-slop move; here
  // the first move wins on the cross axis and the browser scrolls, and the decisive drawer-axis moves after it are still
  // not claimed. Folded with its positive half: the content scrolled.
  testWithTouch('yields the gesture when the browser commits to a native cross-axis scroll', async ({ touchPage: page, mode }) => {
    const fixture = await scrollCase(page, mode, 'right');
    await recordScrollMoves(page);
    const { x } = scrollCenter;

    // Several vertical moves first: Chromium may begin its scroll only on a later move, and a scroll that begins on a
    // horizontal one never scrolls this content.
    await touchDrag(page, [[{ x, y: 357 }, { x, y: 305 }, { x: x + 80, y: 305 }]], {
      steps: 4,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
      },
    });

    const { prevented } = await scrollMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.some(Boolean)).toBe(false);
    // The compositor applies the native scroll in a later frame.
    await expect.poll(() => page.getByTestId('scroll').evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
    await expect(fixture).toHaveAttribute('data-open-changes', '');
  });

  // DRW-PTR-22 (DrawerViewport.test.tsx:3106-3162).
  testWithTouch('keeps tracking the finger when a claimed drag returns inside the slop', async ({ touchPage: page, mode }) => {
    await scrollCase(page, mode, 'right');
    await recordScrollMoves(page);
    const { x, y } = scrollCenter;

    await touchDrag(page, [[{ x, y }, { x: x + 20, y }, { x: x + 50, y }, { x: x + 3, y }]], {
      steps: 1,
      afterMove: async (move) => {
        if (move === 2) {
          expect(await movementX(page)).toBe('30px');
        }
      },
      beforeRelease: async () => expect(Number.parseFloat(await movementX(page))).toBeLessThan(0),
    });

    const { prevented } = await scrollMoves(page);
    expect(prevented.length).toBe(3);
    expect(prevented.every(Boolean)).toBe(true);
  });
});

// Slice 2's selection handles and range inputs (DRW-PTR-19, PTR-21 (b); added 2026-11-20), on the fixture's
// `case=controls` down drawer: its controls fill the popup's top 164 px, and the drags start in the free part below.
const controlsCase = (page: Page, mode: RenderMode) => gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'controls' });
const freePoint = { x: 100, y: 350 };
// A window capture listener reads each move's defaultPrevented once every listener ran.
const recordMoves = (page: Page) =>
  page.evaluate(() => {
    const record = { prevented: [] as boolean[] };
    (window as unknown as { drawerMoves: typeof record }).drawerMoves = record;
    const moves: TouchEvent[] = [];
    window.addEventListener('touchmove', (event) => moves.push(event), { capture: true, passive: true });
    window.addEventListener('touchend', () => (record.prevented = moves.map((event) => event.defaultPrevented)), { capture: true });
  });
const preventedMoves = (page: Page) => page.evaluate(() => (window as unknown as { drawerMoves: { prevented: boolean[] } }).drawerMoves.prevented);

// Each selection title selects "Selec" in a control, drags down from the free area, and finds the moves left to the
// browser and no swipe. Folded with its positive half: once the selection collapses, the same drag swipes.
const selectionTitle = (title: string, select: (page: Page) => Promise<void>, collapse: (page: Page) => Promise<void>) =>
  testWithTouch(title, async ({ touchPage: page, mode }) => {
    const fixture = await controlsCase(page, mode);
    const backdrop = page.getByTestId('backdrop');
    const drag = [freePoint, { x: freePoint.x, y: freePoint.y + 30 }, { x: freePoint.x, y: freePoint.y + 40 }];
    await select(page);
    await recordMoves(page);

    await touchDrag(page, [drag], {
      steps: 1,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(backdrop).not.toHaveAttribute('data-swiping');
      },
    });
    const prevented = await preventedMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.some(Boolean)).toBe(false);

    await collapse(page);
    await touchDrag(page, [drag], { steps: 1, beforeRelease: () => expect(backdrop).toHaveAttribute('data-swiping', '') });
  });

const selectInControl = (testId: string) => (page: Page) =>
  page.getByTestId(testId).evaluate((element) => {
    const control = element as HTMLInputElement | HTMLTextAreaElement;
    control.focus();
    control.setSelectionRange(0, 5);
  });
const collapseInControl = (testId: string) => (page: Page) =>
  page.getByTestId(testId).evaluate((element) => (element as HTMLInputElement | HTMLTextAreaElement).setSelectionRange(0, 0));
const selectText = (testId: string, focus: boolean) => (page: Page) =>
  page.getByTestId(testId).evaluate((element, focus) => {
    if (focus) {
      (element as HTMLElement).focus();
    }
    const range = document.createRange();
    range.setStart(element.firstChild!, 0);
    range.setEnd(element.firstChild!, 5);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
  }, focus);
const collapseText = (page: Page) => page.evaluate(() => window.getSelection()!.collapseToStart());

test.describe('<Drawer.Viewport /> touch swipe over selections and range inputs', () => {
  // DRW-PTR-21 (b) (DrawerViewport.test.tsx:1975-2027).
  selectionTitle('does not start swiping when adjusting input selection handles', selectInControl('input'), collapseInControl('input'));

  // DRW-PTR-21 (b) (DrawerViewport.test.tsx:2029-2081).
  selectionTitle('does not start swiping when adjusting textarea selection handles', selectInControl('textarea'), collapseInControl('textarea'));

  // DRW-PTR-21 (b) (DrawerViewport.test.tsx:2083-2148).
  selectionTitle('does not start swiping when adjusting contenteditable selection handles', selectText('editable', true), collapseText);

  // DRW-PTR-21 (b) (DrawerViewport.test.tsx:2150-2212).
  selectionTitle('does not start swiping when adjusting regular text selection handles', selectText('selectable', false), collapseText);

  // DRW-PTR-19 (4) (DrawerViewport.test.tsx:1886-1925). Folded with its positive half: the drag moves the range's value.
  testWithTouch('does not block touchmove on native range inputs', async ({ touchPage: page, mode }) => {
    const fixture = await controlsCase(page, mode);
    const range = page.getByTestId('range');
    const start = await pointOf(range);
    await recordMoves(page);

    await touchDrag(page, [[start, { x: start.x + 20, y: start.y }, { x: start.x + 40, y: start.y }]], {
      steps: 2,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
      },
    });

    const prevented = await preventedMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.some(Boolean)).toBe(false);
    await expect.poll(() => range.evaluate((element) => Number((element as HTMLInputElement).value))).toBeGreaterThan(50);
  });

  // DRW-PTR-19 (4) (DrawerViewport.test.tsx:1927-1973). The Slider's range input is visually hidden inside its thumb, so a
  // real touch lands on the thumb element and never has the input in its path (measured 2026-11-20: the hit target is
  // the thumb `div`, and the drawer swipes as Base UI's identical check does). The title's touch on the input is input
  // no real device makes, so it is dispatched in the page (ADR 04, Amendments 2026-11-19, point 14). Folded with its
  // positive half: the same touch on the thumb element is claimed.
  testWithTouch('does not block touchmove on slider thumb range inputs', async ({ touchPage: page, mode }) => {
    const fixture = await controlsCase(page, mode);
    const backdrop = page.getByTestId('backdrop');
    const start = await pointOf(page.getByTestId('slider-thumb'));
    const dispatchTouch = (target: Locator, type: string, x: number) =>
      target.evaluate(
        (element, { type, x, y }: { type: string; x: number; y: number }) => {
          const touch = new Touch({ identifier: 1, target: element, clientX: x, clientY: y });
          const event = new TouchEvent(type, {
            bubbles: true,
            cancelable: true,
            composed: true,
            touches: type === 'touchend' ? [] : [touch],
            changedTouches: [touch],
          });
          element.dispatchEvent(event);
          return event.defaultPrevented;
        },
        { type, x, y: start.y },
      );

    const input = page.getByRole('slider', { name: 'Volume' });
    await dispatchTouch(input, 'touchstart', start.x);
    expect(await dispatchTouch(input, 'touchmove', start.x + 20)).toBe(false);
    await settle(fixture);
    await expect(backdrop).not.toHaveAttribute('data-swiping');
    await dispatchTouch(input, 'touchend', start.x + 20);

    const thumb = page.getByTestId('slider-thumb');
    await dispatchTouch(thumb, 'touchstart', start.x);
    expect(await dispatchTouch(thumb, 'touchmove', start.x + 20)).toBe(true);
    await expect(backdrop).toHaveAttribute('data-swiping', '');
  });
});

// Slice 2's `data-base-ui-swipe-ignore` axis values (DRW-PTR-19 (7), PTR-22; added 2026-11-20), on the fixture's
// `case=axis`: rows marked `y` (y 100-140) and `x` (y 140-180), then a valueless ancestor around a row marked `x` (y
// 180-220); the rest of the popup is free. Base UI's first move of each cross-axis drag is under the slop, which Chromium
// does not dispatch; here the first move is past it, and the L3 port title holds the sub-slop move.
const axisCase = (page: Page, mode: RenderMode, direction: 'down' | 'right') => gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'axis', direction });
type Point = { x: number; y: number };
// Records whether each move was prevented, and how many moves reached a test id's element.
const recordAxisMoves = (page: Page, testId: string) =>
  page.evaluate((testId) => {
    const record = { prevented: [] as boolean[], reached: 0 };
    (window as unknown as { axisMoves: typeof record }).axisMoves = record;
    const moves: TouchEvent[] = [];
    window.addEventListener('touchmove', (event) => moves.push(event), { capture: true, passive: true });
    document.querySelector(`[data-testid="${testId}"]`)!.addEventListener('touchmove', () => (record.reached += 1), { passive: true });
    window.addEventListener('touchend', () => (record.prevented = moves.map((event) => event.defaultPrevented)), { capture: true });
  }, testId);
const axisMoves = (page: Page) => page.evaluate(() => (window as unknown as { axisMoves: { prevented: boolean[]; reached: number } }).axisMoves);
const along = (direction: 'down' | 'right', from: Point, distance: number): Point =>
  direction === 'down' ? { x: from.x, y: from.y + distance } : { x: from.x + distance, y: from.y };
const across = (direction: 'down' | 'right', from: Point, distance: number): Point =>
  direction === 'down' ? { x: from.x + distance, y: from.y + 1 } : { x: from.x + 1, y: from.y + distance };

test.describe('<Drawer.Viewport /> touch swipe over swipe-ignore axis values', () => {
  // DRW-PTR-19 (7) (DrawerViewport.test.tsx:836-859). Folded with its positive half: the same drag from the free area
  // dismisses.
  for (const [direction, axis] of [['down', 'y'], ['right', 'x']] as const) {
    testWithTouch(`ignores ${direction} touch swipes from elements with the drawer axis value "${axis}"`, async ({ touchPage: page, mode }) => {
      const fixture = await axisCase(page, mode, direction);
      const start = await pointOf(page.getByTestId(`axis-${axis}`));
      await recordAxisMoves(page, `axis-${axis}`);

      await touchDrag(page, [[start, along(direction, start, 25), along(direction, start, 250)]], {
        steps: 1,
        beforeRelease: async () => {
          await settle(fixture);
          await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
        },
      });
      const { prevented } = await axisMoves(page);
      expect(prevented.length).toBeGreaterThan(0);
      expect(prevented.some(Boolean)).toBe(false);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-open-changes', '');

      const free = { x: 100, y: 350 };
      await touchDrag(page, [[free, along(direction, free, 25), along(direction, free, 250)]], { steps: 1 });
      await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
    });
  }

  // DRW-PTR-22 (DrawerViewport.test.tsx:860-889): the target gets the cross-axis drag and the popup does not move.
  for (const [direction, axis] of [['down', 'x'], ['right', 'y']] as const) {
    testWithTouch(`hands cross-axis drags in ${direction} drawers to elements with the value "${axis}"`, async ({ touchPage: page, mode }) => {
      const fixture = await axisCase(page, mode, direction);
      const popup = page.getByTestId('popup');
      const start = await pointOf(page.getByTestId(`axis-${axis}`));
      await recordAxisMoves(page, `axis-${axis}`);

      await touchDrag(page, [[start, across(direction, start, 14), across(direction, start, 18)]], {
        steps: 1,
        beforeRelease: async () => {
          await settle(fixture);
          await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
          const movement = await popup.evaluate((element) => [
            (element as HTMLElement).style.getPropertyValue('--drawer-swipe-movement-x'),
            (element as HTMLElement).style.getPropertyValue('--drawer-swipe-movement-y'),
          ]);
          expect(movement.map((value) => Number.parseFloat(value || '0'))).toEqual([0, 0]);
        },
      });

      const { prevented, reached } = await axisMoves(page);
      expect(reached).toBe(prevented.length);
      expect(reached).toBeGreaterThan(0);
      expect(prevented.some(Boolean)).toBe(false);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-open-changes', '');
    });
  }

  // DRW-PTR-19 (7), PTR-22 (DrawerViewport.test.tsx:927-981).
  for (const [direction, axis] of [['down', 'x'], ['right', 'y']] as const) {
    testWithTouch(`still swipes ${direction} drawers from elements with the cross-axis value "${axis}"`, async ({ touchPage: page, mode }) => {
      const fixture = await axisCase(page, mode, direction);
      const start = await pointOf(page.getByTestId(`axis-${axis}`));
      await recordAxisMoves(page, `axis-${axis}`);

      await touchDrag(page, [[start, along(direction, start, 25), along(direction, start, 250)]], { steps: 1 });

      await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
      const { prevented } = await axisMoves(page);
      expect(prevented.length).toBeGreaterThan(0);
      expect(prevented.every(Boolean)).toBe(true);
    });
  }

  // DRW-PTR-19 (7) (DrawerViewport.test.tsx:982-1003). Folded with its positive half: the same drag from the free area
  // dismisses.
  testWithTouch('keeps an ancestor that ignores all directions in effect over a nested axis value', async ({ touchPage: page, mode }) => {
    const fixture = await axisCase(page, mode, 'down');
    const start = await pointOf(page.getByTestId('nested-x'));

    await touchDrag(page, [[start, along('down', start, 25), along('down', start, 250)]], {
      steps: 1,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
      },
    });
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-changes', '');

    const free = { x: 100, y: 350 };
    await touchDrag(page, [[free, along('down', free, 25), along('down', free, 250)]], { steps: 1 });
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });
});

// Slice 2's shadow roots (DRW-PTR-19 (7), PTR-06; added 2026-11-20), on the fixture's `case=shadow`: the portal renders
// into an open shadow root, so the document's hit test retargets every point in the popup to the shadow host, and only
// the root node's hit test finds the element under the touch. Locators pierce the open root.
test.describe('<Drawer.Viewport /> touch swipe in a shadow root', () => {
  // DRW-PTR-19 (7) (DrawerViewport.test.tsx:297-351). Base UI stubs both hit tests; here they are real. The ignored row
  // stops the swipe only if the shadow root's hit test finds it, and the target starts one only if it does.
  testWithTouch('uses shadow-root hit testing for touch swipe targets', async ({ touchPage: page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'shadow' });
    const backdrop = page.getByTestId('backdrop');
    const ignored = await pointOf(page.getByTestId('ignored'));
    const target = await pointOf(page.getByTestId('target'));

    await touchDrag(page, [[ignored, { x: ignored.x, y: ignored.y + 25 }, { x: ignored.x, y: ignored.y + 40 }]], {
      steps: 1,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(backdrop).not.toHaveAttribute('data-swiping');
      },
    });

    await touchDrag(page, [[target, { x: target.x, y: target.y + 25 }, { x: target.x, y: target.y + 40 }]], {
      steps: 1,
      beforeRelease: () => expect(backdrop).toHaveAttribute('data-swiping', ''),
    });
  });

  // DRW-PTR-06 (DrawerViewport.test.tsx:352-413, a browser-only title in Base UI).
  testWithTouch('starts a swipe inside a shadow root using real hit testing', async ({ touchPage: page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'shadow' });

    await touchDrag(page, [[popupPoint, { x: popupPoint.x, y: popupPoint.y + 25 }, { x: popupPoint.x, y: popupPoint.y + 250 }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:swipe');
  });
});

// Slice 2's touches without a usual target (DRW-PTR-19 (3) and (5), PTR-21; added 2026-11-20), on the fixture's down
// drawer. A touchstart with no touches and one whose composed path holds no element are input no real device makes, so
// they are dispatched in the page (ADR 04, Amendments 2026-11-19, point 14).
const dispatchTouchAt = (page: Page, testId: string, type: string, point: Point | null, path: 'window' | null = null) =>
  page.getByTestId(testId).evaluate(
    (element, { type, point, path }: { type: string; point: Point | null; path: 'window' | null }) => {
      const touches = point ? [new Touch({ identifier: 1, target: element, clientX: point.x, clientY: point.y })] : [];
      const event = new TouchEvent(type, { bubbles: true, cancelable: true, composed: true, touches, changedTouches: touches });
      if (path === 'window') {
        Object.defineProperty(event, 'composedPath', { value: () => [window] });
      }
      element.dispatchEvent(event);
      return event.defaultPrevented;
    },
    { type, point, path },
  );

test.describe('<Drawer.Viewport /> touch swipe without a usual target', () => {
  // DRW-PTR-19 (3) (DrawerViewport.test.tsx:3644-3658). Folded with its positive half: a real touch drag after it swipes.
  testWithTouch('ignores touchstart when there is no active touch', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const backdrop = page.getByTestId('backdrop');

    await dispatchTouchAt(page, 'viewport', 'touchstart', null);
    await settle(fixture);
    await expect(backdrop).not.toHaveAttribute('data-swiping');

    await touchDrag(page, [[popupPoint, { x: popupPoint.x, y: popupPoint.y + 25 }, { x: popupPoint.x, y: popupPoint.y + 40 }]], {
      steps: 1,
      beforeRelease: () => expect(backdrop).toHaveAttribute('data-swiping', ''),
    });
  });

  // DRW-PTR-19 (5) (DrawerViewport.test.tsx:4947-4981): the touchstart's composed path holds only the window.
  testWithTouch('falls back to the viewport when a touch event has no element target', async ({ touchPage: page, mode }) => {
    await downCase(page, mode);

    await dispatchTouchAt(page, 'popup', 'touchstart', popupPoint, 'window');
    await dispatchTouchAt(page, 'popup', 'touchmove', { x: popupPoint.x, y: popupPoint.y + 40 });

    await expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', '');
  });

  // DRW-PTR-21 (e) (DrawerViewport.test.tsx:5453-5494). The container overflows at the touchstart and stops overflowing
  // before the first move: a page listener after the Viewport's shrinks its content. Base UI's last, non-cancelable move
  // cannot be prevented by anyone, and Chromium sends none here.
  testWithTouch('prevents page scrolling for a non-overflowing touch scroll container', async ({ touchPage: page, mode }) => {
    await downCase(page, mode);
    await recordScrollMoves(page);
    await page.evaluate(() =>
      document.addEventListener(
        'touchstart',
        () => {
          // Clipped too: the scroller's height follows its content, and overflowing text would keep it scrollable.
          (document.querySelector('[data-testid="scroll"]')!.firstElementChild as HTMLElement).style.cssText = 'height: 10px; overflow: hidden;';
        },
        { once: true },
      ),
    );
    const start = await pointOf(page.getByTestId('scroll'));

    await touchDrag(page, [[start, { x: start.x, y: start.y - 25 }, { x: start.x, y: start.y - 40 }]], { steps: 1 });

    const { prevented } = await scrollMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.every(Boolean)).toBe(true);
  });
});

// Slice 2's selections, pens and compatibility pointers on the touch path (DRW-PTR-05, PTR-08, PTR-19 (1), PTR-21 (b);
// added 2026-11-20), on the fixture's down drawer.
const selectPopupText = (page: Page, anchor: 'text' | 'outside-text') =>
  page.evaluate((anchor) => {
    const anchorNode = document.querySelector(`[data-testid="${anchor}"]`)!.firstChild!;
    const focusNode = document.querySelector('[data-testid="text"]')!.firstChild!;
    window.getSelection()!.setBaseAndExtent(anchorNode, 0, focusNode, 5);
  }, anchor);
const selectionState = (page: Page) =>
  page.evaluate(() => {
    const selection = window.getSelection()!;
    return { rangeCount: selection.rangeCount, collapsed: selection.isCollapsed };
  });

test.describe('<Drawer.Viewport /> touch swipe with selections, pens and compatibility pointers', () => {
  // DRW-PTR-08 (DrawerViewport.test.tsx:204-255). The touch is cancelled, not lifted: a lifted touch is a tap, and the
  // browser's tap moves the selection.
  testWithTouch('does not clear text selection on touch swipe start', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    await selectPopupText(page, 'text');

    await touchDrag(page, [[popupPoint, { x: popupPoint.x, y: popupPoint.y + 25 }]], {
      steps: 1,
      release: 'cancel',
      beforeRelease: async () => {
        await settle(fixture);
        expect(await selectionState(page)).toEqual({ rangeCount: 1, collapsed: false });
      },
    });
  });

  // DRW-PTR-21 (b) (DrawerViewport.test.tsx:4906-4945): a real selection from the text outside the drawer into the
  // popup. Folded with its positive half: once the selection collapses, the same drag swipes.
  testWithTouch('preserves an expanded touch selection whose focus endpoint is in the popup', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const backdrop = page.getByTestId('backdrop');
    const drag = [popupPoint, { x: popupPoint.x, y: popupPoint.y + 25 }, { x: popupPoint.x, y: popupPoint.y + 40 }];
    await selectPopupText(page, 'outside-text');

    await touchDrag(page, [drag], {
      steps: 1,
      release: 'cancel',
      beforeRelease: async () => {
        await settle(fixture);
        await expect(backdrop).not.toHaveAttribute('data-swiping');
        expect(await selectionState(page)).toEqual({ rangeCount: 1, collapsed: false });
      },
    });

    await page.evaluate(() => window.getSelection()!.collapseToEnd());
    await touchDrag(page, [drag], { steps: 1, beforeRelease: () => expect(backdrop).toHaveAttribute('data-swiping', '') });
  });

  // DRW-PTR-19 (1), PTR-04 (DrawerViewport.test.tsx:1377-1444): a real pen press (CDP) on Drawer.Content, then the
  // touch events a pen's platform sends for the same contact. CDP's touch sends its own touch pointerdown first, which a
  // pen's touch events never follow, so the touch events are dispatched in the page (ADR 04, Amendments 2026-11-19,
  // point 14). Folded with its positive half: a pen drag from the popup's free area swipes, as a non-touch drag.
  testWithTouch('treats pen interactions on Drawer.Content as non-touch swipes', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const backdrop = page.getByTestId('backdrop');
    const cdp = await page.context().newCDPSession(page);
    const pen = (type: 'mousePressed' | 'mouseMoved' | 'mouseReleased', { x, y }: Point) =>
      cdp.send('Input.dispatchMouseEvent', {
        type,
        x,
        y,
        button: type === 'mouseMoved' ? 'none' : 'left',
        buttons: type === 'mouseReleased' ? 0 : 1,
        clickCount: type === 'mouseMoved' ? 0 : 1,
        pointerType: 'pen',
      });
    const content = await pointOf(page.getByTestId('content-target'));

    await pen('mousePressed', content);
    await dispatchTouchAt(page, 'content-target', 'touchstart', content);
    await settle(fixture);
    await expect(backdrop).not.toHaveAttribute('data-swiping');
    expect(await dispatchTouchAt(page, 'content-target', 'touchmove', { x: content.x, y: content.y + 25 })).toBe(false);
    await dispatchTouchAt(page, 'content-target', 'touchend', null);
    await pen('mouseReleased', content);
    await settle(fixture);
    await expect(backdrop).not.toHaveAttribute('data-swiping');

    await pen('mousePressed', popupPoint);
    await pen('mouseMoved', { x: popupPoint.x, y: popupPoint.y + 20 });
    await expect(backdrop).toHaveAttribute('data-swiping', '');
    await pen('mouseReleased', { x: popupPoint.x, y: popupPoint.y + 20 });
    await cdp.detach();
  });

  // DRW-PTR-05 (DrawerViewport.test.tsx:4983-5031): touch pointer events with no touch events beside them are input no
  // real device makes, so they are dispatched in the page (ADR 04, Amendments 2026-11-19, point 14). Folded with its
  // positive half: the same sequence as a mouse swipes.
  test('ignores compatibility touch pointer gestures with real displacement', async ({ page, mode }) => {
    const fixture = await downCase(page, mode);
    const backdrop = page.getByTestId('backdrop');
    const sequence = async (pointerType: string, pointerId: number, end: boolean) => {
      const { x, y } = popupPoint;
      await dispatchPointer(page, 'viewport', 'pointerdown', { x, y, pointerType, pointerId });
      await dispatchPointer(page, 'viewport', 'pointermove', { x, y: y + 1, pointerType, pointerId });
      await dispatchPointer(page, 'viewport', 'pointermove', { x, y: y + 100, pointerType, pointerId });
      if (end) {
        await dispatchPointer(page, 'viewport', 'pointerup', { x, y: y + 100, pointerType, pointerId });
        await dispatchPointer(page, 'viewport', 'pointercancel', { x, y: y + 100, pointerType, pointerId });
      }
    };

    await sequence('touch', 1, true);
    await settle(fixture);
    await expect(backdrop).not.toHaveAttribute('data-swiping');
    await expect(fixture).toHaveAttribute('data-open-changes', '');

    await sequence('mouse', 2, false);
    await expect(backdrop).toHaveAttribute('data-swiping', '');
  });
});

// Slice 2's remaining scroll arbitration (DRW-PTR-21 (f), PTR-22, ALG-03; added 2026-11-20). Base UI's last six titles
// here are browser-only (skipped in jsdom) but still use synthetic touches; here every touch is real, and each absence
// is folded with the native scroll the drawer leaves to the browser. The case=scroll popup adds a both-ways scroller
// (x 300-420, y 400-460). Several vertical or horizontal moves begin each drag: Chromium may begin its scroll only on a
// later move.
const scrollOffsets = (page: Page, testId: string) =>
  page.getByTestId(testId).evaluate((element) => ({ top: element.scrollTop, left: element.scrollLeft }));

test.describe('<Drawer.Viewport /> touch swipe and native scrolling', () => {
  // DRW-PTR-21 (f) (DrawerViewport.test.tsx:1704-1745), on the fixture's `case=wrap`: the scroll container sits between
  // the Viewport and the popup.
  testWithTouch('prevents touchmove when a scrollable ancestor wraps the popup at the top', async ({ touchPage: page, mode }) => {
    await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'wrap' });
    await recordMoves(page);
    const start = await pointOf(page.getByTestId('item'));

    await touchDrag(page, [[start, { x: start.x, y: start.y + 25 }, { x: start.x, y: start.y + 40 }]], {
      steps: 1,
      beforeRelease: () => expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', ''),
    });

    const prevented = await preventedMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.every(Boolean)).toBe(true);
  });

  // DRW-PTR-21 (f), ALG-03 (DrawerViewport.test.tsx:2399-2443): at the right edge, a leftward drag is not the drawer's.
  // The content has nothing left to scroll that way, so the title asserts only absences; its red is a planted fault,
  // quoted in PR 305.
  testWithTouch('does not start an opposite-direction swipe from scroll right edge for right drawers', async ({ touchPage: page, mode }) => {
    const fixture = await scrollCase(page, mode, 'right', { scrollLeft: 'max' });
    await recordMoves(page);
    const { x, y } = hscrollCenter;

    await touchDrag(page, [[{ x: x + 50, y }, { x: x - 50, y }]], {
      steps: 5,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
      },
    });

    const prevented = await preventedMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.some(Boolean)).toBe(false);
  });

  // DRW-PTR-22 (DrawerViewport.test.tsx:3292-3348): the both-ways scroller at scrollLeft 40; the drag wins on the cross
  // axis.
  testWithTouch('does not hijack cross-axis gestures from mixed-axis scroll containers', async ({ touchPage: page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'scroll', direction: 'down' });
    await page.getByTestId('bscroll').evaluate((element) => (element.scrollLeft = 40));
    await recordMoves(page);
    const start = await pointOf(page.getByTestId('bscroll'));

    await touchDrag(page, [[{ x: start.x + 50, y: start.y - 20 }, { x: start.x - 40, y: start.y + 20 }]], {
      steps: 5,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
      },
    });

    const prevented = await preventedMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.some(Boolean)).toBe(false);
    await expect.poll(async () => (await scrollOffsets(page, 'bscroll')).left).toBeGreaterThan(40);
  });

  // DRW-PTR-22 (DrawerViewport.test.tsx:3350-3532): a scroller overflowing only on the drawer's cross axis keeps its
  // native scroll.
  for (const [direction, testId, axis] of [
    ['right', 'scroll', 'vertical'],
    ['left', 'scroll', 'vertical'],
    ['down', 'hscroll', 'horizontal'],
    ['up', 'hscroll', 'horizontal'],
  ] as const) {
    testWithTouch(`does not block ${axis} scrolling in ${direction} drawers when only ${axis} overflow exists`, async ({ touchPage: page, mode }) => {
      const fixture = await scrollCase(page, mode, direction, { scrollTop: 0, scrollLeft: 0 });
      await recordMoves(page);
      const { x, y } = await pointOf(page.getByTestId(testId));
      const path = axis === 'vertical' ? [{ x, y: y + 25 }, { x, y: y - 25 }] : [{ x: x + 50, y }, { x: x - 50, y }];

      await touchDrag(page, [path], {
        steps: 5,
        beforeRelease: async () => {
          await settle(fixture);
          await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
        },
      });

      const prevented = await preventedMoves(page);
      expect(prevented.length).toBeGreaterThan(0);
      expect(prevented.some(Boolean)).toBe(false);
      await expect.poll(async () => {
        const offsets = await scrollOffsets(page, testId);
        return axis === 'vertical' ? offsets.top : offsets.left;
      }).toBeGreaterThan(0);
    });
  }
});

// Slice 2's teardown and taps (DRW-DOM-20, ANI-05, PTR-19; added 2026-11-20), on the fixture's down drawer.
test.describe('<Drawer.Viewport /> touch swipe teardown and taps', () => {
  // DRW-DOM-20, ANI-05 (DrawerViewport.test.tsx:415-456): the fixture's remove button stops rendering the root mid-swipe, as
  // Base UI's unmount(); the detached backdrop keeps its attributes, so the check reads the cleanup.
  testWithTouch('clears the backdrop data-swiping attribute when the drawer unmounts mid-swipe', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const backdrop = await page.getByTestId('backdrop').elementHandle();

    await touchDrag(page, [[popupPoint, { x: popupPoint.x, y: popupPoint.y + 25 }]], {
      steps: 1,
      beforeRelease: async () => {
        await expect(page.getByTestId('backdrop')).toHaveAttribute('data-swiping', '');
        await fixture.locator('[data-remove-root]').evaluate((button) => (button as HTMLButtonElement).click());
        await expect(page.getByTestId('popup')).toHaveCount(0);
        expect(await backdrop!.evaluate((element) => element.isConnected)).toBe(false);
        await expect.poll(() => backdrop!.evaluate((element) => element.hasAttribute('data-swiping'))).toBe(false);
      },
    });
  });

  // DRW-PTR-19, no swipe on a tap (DrawerViewport.test.tsx:689-743): a real tap on a plain element in Drawer.Content.
  testWithTouch('allows clicks on non-interactive elements without data-base-ui-swipe-ignore', async ({ touchPage: page, mode }) => {
    const fixture = await downCase(page, mode);
    const target = page.getByTestId('content-target');
    await target.evaluate((element) => {
      (window as unknown as { contentClicks: number }).contentClicks = 0;
      element.addEventListener('click', () => ((window as unknown as { contentClicks: number }).contentClicks += 1));
    });

    await touchDrag(page, [[await pointOf(target)]]);

    await expect.poll(() => page.evaluate(() => (window as unknown as { contentClicks: number }).contentClicks)).toBe(1);
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-changes', '');
    await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
  });
});

// Slice 2's portaled descendants (DRW-PTR-19 (5), PTR-21, PTR-22; added 2026-11-20), on the fixture's `case=combobox`: a
// Combobox in Drawer.Content whose list portals out of the drawer. In Base UI a touch on a portaled descendant reaches the
// Viewport's handler through React's tree, and the Viewport-containment check turns it away. Here the Viewport listens on
// its element, so no such touch reaches it at all.
test.describe('<Drawer.Viewport /> touch swipe over portaled descendants', () => {
  // DRW-PTR-19 (5) (DrawerViewport.test.tsx:1005-1047). It asserts only an absence; its red is a planted fault, quoted in
  // PR 305.
  testWithTouch('does not prevent native touch scrolling in portaled descendants', async ({ touchPage: page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'combobox' });
    const listbox = page.getByTestId('listbox');
    await expect(listbox).toBeVisible();
    await recordMoves(page);
    const start = await pointOf(listbox);

    await touchDrag(page, [[{ x: start.x, y: start.y - 15 }, { x: start.x, y: start.y + 25 }]], {
      steps: 4,
      beforeRelease: () => settle(fixture),
    });

    const prevented = await preventedMoves(page);
    expect(prevented.length).toBeGreaterThan(0);
    expect(prevented.some(Boolean)).toBe(false);
  });

  // DRW-PTR-22 (DrawerViewport.test.tsx:1049-1152, browser-only in Base UI): a real touch scroll of the list, bottom to
  // top. Folded with its positive half: the list scrolls.
  testWithTouch('allows touch gestures on a portaled combobox popup without starting drawer swipe', async ({ touchPage: page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'combobox' });
    const listbox = page.getByTestId('listbox');
    await expect(listbox).toBeVisible();
    await expect.poll(() => listbox.evaluate((element) => element.scrollHeight > element.clientHeight)).toBe(true);
    const box = (await listbox.boundingBox())!;
    const x = box.x + box.width / 2;

    await touchDrag(page, [[{ x, y: box.y + box.height - 8 }, { x, y: box.y + box.height / 2 }, { x, y: box.y + 8 }]], {
      steps: 4,
      beforeRelease: async () => {
        await settle(fixture);
        await expect(page.getByTestId('backdrop')).not.toHaveAttribute('data-swiping');
      },
    });

    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-changes', '');
    await expect(listbox).toBeVisible();
    await expect.poll(() => listbox.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  });
});
