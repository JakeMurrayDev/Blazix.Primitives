import { expect, pointOf, swipeOnTimeline, test, testWithTouch, touchDrag, type Page, type RenderMode } from './helpers';
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

/** The centre of the popup's free area under its Content (the popup is 300 px tall from top 100). */
const popupPoint = { x: 100, y: 370 };

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
