// L4 (ADR 06 §2): Base UI's ScrollAreaThumb.test.tsx on both render modes (docs/components/scroll-area.md §6.2, §13).
// Titles verbatim but for the two React `flushSync` titles. Base UI's L2-only title (the error outside a Scrollbar) is in
// the component tests. jsdom's mocked metrics become the fixture's real layout; `fireEvent` and `user.pointer` become real
// mouse input, and CDP touch where Base UI fires `pointerCancel` or a second pointer, which a mouse cannot make; stubbed
// pointer capture becomes the browser's own, read with `hasPointerCapture` and a pass-through spy.
import { expect, test, testWithTouch, type Page } from './helpers';
import {
  collectPageErrors,
  gotoParts,
  hasPointerCapture,
  holdClock,
  inlineStyle,
  pointIn,
  recordAttributes,
  recordWindowEvents,
  releaseCalls,
  sawAttribute,
  scrollOf,
  scrollViewport,
  settle,
  spyReleasePointerCapture,
  touchInput,
  windowEvents,
} from './scroll-area.helpers';

const SCROLL_TIMEOUT = 500;

/** The vertical tree of Base UI's pointer-cancel and snap titles: a 200 × 200 px root over 200 × 1,000 px of content. */
const VERTICAL = { content: false, bars: 'vertical', keepMounted: true, width: 200, height: 1000 } as const;

/** The horizontal tree: 1,000 × 200 px of content under a 200 × 10 px flex scrollbar (Base UI's `renderHorizontal`). */
const HORIZONTAL = {
  content: false,
  bars: 'horizontal',
  keepMounted: true,
  width: 1000,
  height: 200,
  hStyle: 'display: flex; width: 200px; height: 10px',
} as const;

/** The id the browser gave the first recorded `pointerdown`. */
async function firstPointerId(page: Page): Promise<number> {
  await expect.poll(async () => (await windowEvents(page)).length).toBeGreaterThan(0);
  return (await windowEvents(page))[0]!.pointerId!;
}

test.describe('ScrollArea.Thumb', () => {
  test('handles a thumb gesture when no viewport is mounted', async ({ page, mode }) => {
    await recordAttributes(page, ['data-scrolling']);
    const errors = collectPageErrors(page);
    // Without a viewport nothing measures, so the thumb keeps its 0 px size variable and its `visibility: hidden` (Base UI's
    // `hasMeasuredScrollbar`): the consumer's style gives the real pointer something to press.
    const fixture = await gotoParts(
      page,
      mode,
      { viewport: false, bars: 'vertical', keepMounted: true, vThumbStyle: 'width: 100%; min-height: 20px; visibility: visible' },
      { measured: false },
    );
    const thumb = page.getByTestId('thumb-vertical');

    const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
    await page.mouse.move(point.x, point.y);
    await page.mouse.down();
    await page.mouse.move(point.x, point.y + 20);
    await settle(fixture);

    // Without a viewport there is nothing to scroll, so the drag never consumes the move.
    expect(await sawAttribute(page, 'thumb-vertical', 'data-scrolling')).toBe(false);
    expect(await inlineStyle(thumb, 'transform')).toBe('');

    await page.mouse.up();
    await settle(fixture);
    expect(await sawAttribute(page, 'thumb-vertical', 'data-scrolling')).toBe(false);
    expect(errors).toEqual([]);
  });

  // React's `onPointerMove` with `flushSync(() => setMounted(false))` has no Blazor counterpart (a C# handler runs after the
  // event, a round trip later on Server): a consumer's JavaScript listener, at the thumb ahead of the module's, removes the
  // scrollbar's element in the middle of the move's dispatch.
  test('port: handles the scrollbar removed by a consumer pointer-move listener', async ({ page, mode }) => {
    const errors = collectPageErrors(page);
    await gotoParts(page, mode, { content: false, bars: 'vertical', keepMounted: true });
    await page.getByTestId('thumb-vertical').evaluate((thumb) => {
      thumb.addEventListener('pointermove', () => thumb.closest('[data-testid="scrollbar-vertical"]')!.remove(), { capture: true, once: true });
    });

    const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
    await page.mouse.move(point.x, point.y);
    await page.mouse.down();
    await page.mouse.move(point.x, point.y + 20);

    await expect(page.getByTestId('scrollbar-vertical')).toHaveCount(0);
    expect(await scrollOf(page, 'top')).toBe(0);
    await page.mouse.up();
    expect(errors).toEqual([]);
  });

  // React's `onPointerUp` with `flushSync(() => setMounted(false))`: a consumer's JavaScript listener, at the thumb ahead of
  // the module's, removes the viewport's element in the middle of the release's dispatch.
  test('port: handles the viewport removed by a consumer pointer-up listener', async ({ page, mode }) => {
    const errors = collectPageErrors(page);
    await gotoParts(page, mode, { content: false, bars: 'vertical', keepMounted: true, viewportStyle: 'scroll-snap-type: y mandatory' });
    const viewport = page.getByTestId('viewport');
    await page.getByTestId('thumb-vertical').evaluate((thumb) => {
      thumb.addEventListener('pointerup', () => document.querySelector('[data-testid="viewport"]')!.remove(), { capture: true, once: true });
    });

    const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
    await page.mouse.move(point.x, point.y);
    await page.mouse.down();
    expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

    await page.mouse.up();
    await expect(viewport).toHaveCount(0);
    expect(errors).toEqual([]);
  });

  test.describe('horizontal dragging', () => {
    test('updates LTR scroll position and pointer capture state', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      await gotoParts(page, mode, HORIZONTAL);
      await recordWindowEvents(page, ['pointerdown']);
      const thumb = page.getByTestId('thumb-horizontal');
      await spyReleasePointerCapture(thumb);

      const point = await pointIn(page, 'thumb-horizontal', (width) => width / 2, (height) => height / 2);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();
      await page.mouse.move(point.x + 20, point.y);

      // Base UI counts `setPointerCapture` calls on a stub; the real capture is read instead.
      const pointerId = await firstPointerId(page);
      expect(await hasPointerCapture(thumb, pointerId)).toBe(true);
      expect(await scrollOf(page, 'left')).toBeGreaterThan(0);
      await expect.poll(() => sawAttribute(page, 'scrollbar-horizontal', 'data-scrolling')).toBe(true);

      await page.mouse.up();

      expect((await releaseCalls(thumb)).length).toBeGreaterThan(0);
      expect(await hasPointerCapture(thumb, pointerId)).toBe(false);
    });

    // A mouse cannot be cancelled: the drag is a CDP touch, cancelled with `touchCancel`. The clock is held so the 500 ms
    // timer cannot clear the flag in the cancel's place.
    testWithTouch('uses the negative RTL range and clears scrolling on pointer cancel', async ({ touchPage: page, mode }) => {
      const errors = collectPageErrors(page);
      await gotoParts(page, mode, { ...HORIZONTAL, dir: 'rtl' });
      const scrollbar = page.getByTestId('scrollbar-horizontal');

      const point = await pointIn(page, 'thumb-horizontal', (width) => width / 2, (height) => height / 2);
      await holdClock(page);
      const touch = await touchInput(page);
      try {
        await touch.start({ id: 0, ...point });
        await touch.move({ id: 0, x: point.x - 10, y: point.y });
        await touch.move({ id: 0, x: point.x - 20, y: point.y });

        await expect.poll(() => scrollOf(page, 'left')).toBeLessThan(0);
        await expect(scrollbar).toHaveAttribute('data-scrolling');

        await touch.cancel();

        await expect(scrollbar).not.toHaveAttribute('data-scrolling');
        expect(errors).toEqual([]);
      } finally {
        await touch.detach();
        await page.clock.resume();
      }
    });
  });

  // Base UI's stubs make capture absent and `releasePointerCapture` throw. With a real cancelled touch, the spy records
  // every release with whether capture was held at that moment: none may release a capture it does not hold.
  testWithTouch('clears scrolling state on pointer cancel without releasing stale capture', async ({ touchPage: page, mode }) => {
    const errors = collectPageErrors(page);
    await gotoParts(page, mode, VERTICAL);
    const scrollbar = page.getByTestId('scrollbar-vertical');
    const thumb = page.getByTestId('thumb-vertical');
    await spyReleasePointerCapture(thumb);

    const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
    await holdClock(page);
    const touch = await touchInput(page);
    try {
      await touch.start({ id: 0, ...point });
      await touch.move({ id: 0, x: point.x, y: point.y + 10 });
      await touch.move({ id: 0, x: point.x, y: point.y + 20 });

      await expect(scrollbar).toHaveAttribute('data-scrolling');

      await touch.cancel();

      await expect(scrollbar).not.toHaveAttribute('data-scrolling');
      expect((await releaseCalls(thumb)).every((call) => call.held)).toBe(true);
      expect(errors).toEqual([]);
    } finally {
      await touch.detach();
      await page.clock.resume();
    }
  });

  // The missed release is a CDP mouse move with `buttons: 0` while the press is still down. The second pointer that hovers
  // with no button is dispatched in the page: CDP's pen (`Input.dispatchMouseEvent` with `pointerType: 'pen'`) shares
  // Chromium's mouse state, so its buttonless move released the mouse's capture in a probe. The clock is held so only the
  // buttonless move can clear the flag.
  test('ends a drag whose release was missed instead of scrolling on hover', async ({ page, mode }) => {
    await gotoParts(page, mode, { ...VERTICAL, viewportStyle: 'scroll-snap-type: y mandatory', vStyle: 'width: 10px; height: 200px' });
    const viewport = page.getByTestId('viewport');
    const thumb = page.getByTestId('thumb-vertical');
    const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, 5);

    await holdClock(page);
    const cdp = await page.context().newCDPSession(page);
    try {
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();
      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

      await page.mouse.move(point.x, point.y + 20);
      expect(await scrollOf(page, 'top')).toBeGreaterThan(0);
      const scrolled = await scrollOf(page, 'top');

      // A different pointer hovering over the thumb must not end the active drag.
      await thumb.evaluate((element, { clientX, clientY }) => {
        element.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, cancelable: true, pointerId: 2, pointerType: 'pen', buttons: 0, clientX, clientY }));
      }, { clientX: point.x, clientY: point.y + 60 });
      expect(await scrollOf(page, 'top')).toBe(scrolled);
      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

      await page.mouse.move(point.x, point.y + 60);
      expect(await scrollOf(page, 'top')).toBeGreaterThan(scrolled);
      const continuedScroll = await scrollOf(page, 'top');
      await expect(thumb).toHaveAttribute('data-scrolling');

      // The release never arrived (e.g. pointer capture was lost mid-drag), so
      // the first buttonless move must end the drag rather than scroll. It must
      // clear the scrolling state immediately like a real release, not leave it
      // lingering until the scroll timeout fires.
      // Chromium drops the capture on a buttonless move (`lostpointercapture` first), so the move reaches the element under
      // the pointer: it stays on the thumb, which followed the pointer during the drag (Base UI dispatches on the thumb).
      await cdp.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: point.x, y: point.y + 62, button: 'none', buttons: 0 });
      expect(await scrollOf(page, 'top')).toBe(continuedScroll);
      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
      await expect(thumb).not.toHaveAttribute('data-scrolling');

      await cdp.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: point.x, y: point.y + 64, button: 'none', buttons: 0 });
      expect(await scrollOf(page, 'top')).toBe(continuedScroll);
    } finally {
      await cdp.detach();
      await page.mouse.up();
      await page.clock.resume();
    }
  });

  testWithTouch('clears horizontal scrolling state on pointer cancel', async ({ touchPage: page, mode }) => {
    const errors = collectPageErrors(page);
    await gotoParts(page, mode, { ...HORIZONTAL, hStyle: 'height: 10px' });
    const scrollbar = page.getByTestId('scrollbar-horizontal');
    const thumb = page.getByTestId('thumb-horizontal');
    await spyReleasePointerCapture(thumb);

    const point = await pointIn(page, 'thumb-horizontal', (width) => width / 2, (height) => height / 2);
    await holdClock(page);
    const touch = await touchInput(page);
    try {
      await touch.start({ id: 0, ...point });
      await touch.move({ id: 0, x: point.x + 10, y: point.y });
      await touch.move({ id: 0, x: point.x + 20, y: point.y });

      await expect(scrollbar).toHaveAttribute('data-scrolling');

      await touch.cancel();

      await expect(scrollbar).not.toHaveAttribute('data-scrolling');
      expect((await releaseCalls(thumb)).every((call) => call.held)).toBe(true);
      expect(errors).toEqual([]);
    } finally {
      await touch.detach();
      await page.clock.resume();
    }
  });

  test.describe('scroll snap', () => {
    const WITH_SNAP = { ...VERTICAL, viewportStyle: 'scroll-snap-type: y mandatory' };

    test('disables viewport scroll snap while dragging and restores it on release', async ({ page, mode }) => {
      await gotoParts(page, mode, WITH_SNAP);
      const viewport = page.getByTestId('viewport');

      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();
      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

      await page.mouse.up();
      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
    });

    testWithTouch('restores viewport scroll snap on pointer cancel', async ({ touchPage: page, mode }) => {
      await gotoParts(page, mode, WITH_SNAP);
      const viewport = page.getByTestId('viewport');

      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      const touch = await touchInput(page);
      try {
        await touch.start({ id: 0, ...point });
        await expect.poll(() => inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

        await touch.cancel();
        await expect.poll(() => inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
      } finally {
        await touch.detach();
      }
    });

    // Two real touch contacts on the thumb: the first holds capture, so the second is ignored.
    testWithTouch('ignores a second pointer while a drag is active', async ({ touchPage: page, mode }) => {
      await gotoParts(page, mode, WITH_SNAP);
      const viewport = page.getByTestId('viewport');

      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      const touch = await touchInput(page);
      try {
        await touch.start({ id: 0, x: point.x - 2, y: point.y - 5 });
        await touch.start({ id: 1, x: point.x + 2, y: point.y + 5 });
        await expect.poll(() => inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

        await touch.end(1);
        await settle(page.locator('[data-fixture="scroll-area-parts"]'));
        expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

        await touch.end(0);
        await expect.poll(() => inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
      } finally {
        await touch.detach();
      }
    });

    // The first contact's capture is dropped in the page (`releasePointerCapture`, which delivers no `pointerup` or
    // `pointercancel`), and the contact never lifts during the title; a second real contact then takes over.
    testWithTouch('lets a new pointer take over when capture was silently dropped', async ({ touchPage: page, mode }) => {
      await gotoParts(page, mode, WITH_SNAP);
      await recordWindowEvents(page, ['pointerdown']);
      const viewport = page.getByTestId('viewport');
      const thumb = page.getByTestId('thumb-vertical');

      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      const touch = await touchInput(page);
      try {
        await touch.start({ id: 0, x: point.x - 2, y: point.y - 5 });
        await expect.poll(() => inlineStyle(viewport, 'scroll-snap-type')).toBe('none');

        // The browser dropped capture without delivering `pointerup` or
        // `pointercancel`, and the contact's id never reappears (e.g. a lost
        // touch), so a new pointer must be able to take over the latched drag.
        const pointerId = await firstPointerId(page);
        await thumb.evaluate((element, id) => element.releasePointerCapture(id), pointerId);
        expect(await hasPointerCapture(thumb, pointerId)).toBe(false);

        await touch.start({ id: 1, x: point.x + 2, y: point.y + 5 });
        await touch.end(1);
        await expect.poll(() => inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
      } finally {
        await touch.cancel();
        await touch.detach();
      }
    });

    test('ignores non-primary pointer presses', async ({ page, mode }) => {
      await gotoParts(page, mode, WITH_SNAP);
      await recordWindowEvents(page, ['pointerdown']);
      const viewport = page.getByTestId('viewport');
      const thumb = page.getByTestId('thumb-vertical');

      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down({ button: 'right' });

      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
      // Base UI asserts its `setPointerCapture` spy was not called; the thumb holds no capture.
      expect(await hasPointerCapture(thumb, await firstPointerId(page))).toBe(false);
      await page.mouse.up({ button: 'right' });
    });
  });

  test.describe('data-scrolling attribute', () => {
    // Fake timers: the page's clock is held from before the first scroll; `fireEvent.pointerEnter` is a real mouse move onto
    // the viewport and `fireEvent.scroll` a programmatic scroll.
    test('adds [data-scrolling] attribute when viewport is scrolled in the correct direction', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, keepMounted: true });
      const verticalThumb = page.getByTestId('thumb-vertical');
      const horizontalThumb = page.getByTestId('thumb-horizontal');

      await holdClock(page);
      try {
        await expect(verticalThumb).not.toHaveAttribute('data-scrolling');
        await expect(horizontalThumb).not.toHaveAttribute('data-scrolling');

        const first = await pointIn(page, 'viewport', 50, 50);
        await page.mouse.move(first.x, first.y);
        await scrollViewport(page, { top: 1 });

        await expect(verticalThumb).toHaveAttribute('data-scrolling', '');
        await expect(horizontalThumb).not.toHaveAttribute('data-scrolling');

        await page.clock.runFor(SCROLL_TIMEOUT - 1);

        await expect(verticalThumb).toHaveAttribute('data-scrolling', '');
        await expect(horizontalThumb).not.toHaveAttribute('data-scrolling');

        await page.mouse.move(first.x + 10, first.y + 10);
        await scrollViewport(page, { left: 1 });
        await expect(horizontalThumb).toHaveAttribute('data-scrolling');

        await page.clock.runFor(1);

        await expect(verticalThumb).not.toHaveAttribute('data-scrolling');
        await expect(horizontalThumb).toHaveAttribute('data-scrolling');

        await page.clock.runFor(SCROLL_TIMEOUT - 2);

        await expect(verticalThumb).not.toHaveAttribute('data-scrolling');
        await expect(horizontalThumb).toHaveAttribute('data-scrolling');

        await page.clock.runFor(1);

        await expect(verticalThumb).not.toHaveAttribute('data-scrolling');
        await expect(horizontalThumb).not.toHaveAttribute('data-scrolling');
      } finally {
        await page.clock.resume();
      }
    });
  });
});
