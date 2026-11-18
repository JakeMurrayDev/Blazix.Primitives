// L4 (ADR 06 §2): Base UI's ScrollAreaScrollbar.test.tsx on both render modes (docs/components/scroll-area.md §6.2, §13).
// Titles verbatim. Base UI's L2-only titles (aria-hidden, the orientation attribute, the ref-less renderer) are in the
// component tests. jsdom's mocked metrics become the fixture's real layout (a 200 × 200 px root over 1,000 px of content,
// 10 px scrollbars), `fireEvent` becomes real mouse, wheel and CDP touch input, and fake timers become `page.clock`.
import { expect, test, testWithTouch, wheelOver, type Page } from './helpers';
import {
  attributeFlags,
  collectPageErrors,
  computed,
  gotoParts,
  hasPointerCapture,
  holdClock,
  inlineStyle,
  pointIn,
  moveRootIntoShadowRoot,
  recordAttributes,
  recordWindowEvents,
  sampleMark,
  sawAttribute,
  scrollOf,
  scrollViewport,
  settle,
  touchInput,
  viewportMetrics,
  windowEvents,
} from './scroll-area.helpers';

const SCROLL_TIMEOUT = 500;

test.describe('ScrollArea.Scrollbar', () => {
  test.describe('data-scrolling attribute', () => {
    // Fake timers: the page's clock is held from before the first scroll. `fireEvent.pointerEnter` is a real mouse move onto
    // the viewport; `fireEvent.scroll` a programmatic scroll whose `scroll` event the browser sends.
    test('adds [data-scrolling] attribute when viewport is scrolled in the correct direction', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, keepMounted: true, thumb: false, corner: true });
      const verticalScrollbar = page.getByTestId('scrollbar-vertical');
      const horizontalScrollbar = page.getByTestId('scrollbar-horizontal');

      await holdClock(page);
      try {
        await expect(verticalScrollbar).not.toHaveAttribute('data-scrolling');
        await expect(horizontalScrollbar).not.toHaveAttribute('data-scrolling');

        const first = await pointIn(page, 'viewport', 50, 50);
        await page.mouse.move(first.x, first.y);
        await scrollViewport(page, { top: 1 });

        await expect(verticalScrollbar).toHaveAttribute('data-scrolling', '');
        await expect(horizontalScrollbar).not.toHaveAttribute('data-scrolling', '');

        await page.clock.runFor(SCROLL_TIMEOUT - 1);

        await expect(verticalScrollbar).toHaveAttribute('data-scrolling', '');
        await expect(horizontalScrollbar).not.toHaveAttribute('data-scrolling', '');

        await page.mouse.move(first.x + 10, first.y + 10);
        await scrollViewport(page, { left: 1 });
        await expect(horizontalScrollbar).toHaveAttribute('data-scrolling');

        await page.clock.runFor(1); // vertical just finished

        await expect(verticalScrollbar).not.toHaveAttribute('data-scrolling');
        await expect(horizontalScrollbar).toHaveAttribute('data-scrolling');

        await page.clock.runFor(SCROLL_TIMEOUT - 2); // already ticked 1ms above

        await expect(verticalScrollbar).not.toHaveAttribute('data-scrolling');
        await expect(horizontalScrollbar).toHaveAttribute('data-scrolling');

        await page.clock.runFor(1);

        await expect(verticalScrollbar).not.toHaveAttribute('data-scrolling');
        await expect(horizontalScrollbar).not.toHaveAttribute('data-scrolling');
      } finally {
        await page.clock.resume();
      }
    });
  });

  test.describe('data-hovering attribute', () => {
    // Base UI spies `matches(':hover')` to return true for the viewport at mount. Here the real mouse rests where the
    // viewport will mount, the root mounts under it from a fixture button pressed in the page (so no pointer event reaches
    // the new elements), and the browser's own `:hover` stands in for the spy. Chromium updates the hover state of content
    // that appeared under a stationary pointer in a later rendering update, so a route wraps the module and holds the
    // viewport's attach until the browser reports the viewport hovered (the title's premise, asserted), at most 30 updates.
    test('detects a viewport that is already hovered on mount', async ({ page, mode }) => {
      await page.route(/\/js\/scroll-area(\.[\w-]+)?\.js(\?.*)?$/, async (route) => {
        const url = new URL(route.request().url());
        if (url.search.includes('original')) {
          await route.continue();
          return;
        }
        const original = `${url.pathname}?original`;
        await route.fulfill({
          contentType: 'text/javascript',
          body: `
            import * as original from '${original}';
            export * from '${original}';
            const frame = () => new Promise((resolve) => {
              const probe = document.createElement('div');
              document.body.append(probe);
              const observer = new ResizeObserver(() => { observer.disconnect(); probe.remove(); resolve(); });
              observer.observe(probe);
            });
            const hovered = () => !!document.querySelector('[data-testid="viewport"]')?.matches(':hover');
            export async function attach(element, optionsJson, ...rest) {
              if (JSON.parse(optionsJson).part === 'viewport') {
                for (let i = 0; i < 30 && !hovered(); i++) {
                  await frame();
                }
                window.viewportHoveredAtAttach = hovered();
              }
              return original.attach(element, optionsJson, ...rest);
            }`,
        });
      });
      const fixture = await gotoParts(page, mode, { deferRoot: true, bars: 'vertical', keepMounted: true }, { measured: false });
      const slot = (await fixture.locator(':scope > div').nth(1).boundingBox())!;
      await page.mouse.move(slot.x + 100, slot.y + 100);

      await page.getByTestId('mount-root').evaluate((element) => (element as HTMLElement).click());

      await expect(page.getByTestId('scrollbar-vertical')).toHaveAttribute('data-hovering');
      expect(await page.evaluate(() => (window as unknown as { viewportHoveredAtAttach: boolean }).viewportHoveredAtAttach)).toBe(true);
    });

    testWithTouch('does not enter hover state for touch pointers', async ({ touchPage: page, mode }) => {
      await recordAttributes(page, ['data-hovering']);
      const fixture = await gotoParts(page, mode, { bars: 'vertical', keepMounted: true });
      const point = await pointIn(page, 'viewport', 50, 50);

      const touch = await touchInput(page);
      try {
        await touch.start({ id: 0, ...point });
        await touch.end(0);
      } finally {
        await touch.detach();
      }
      await settle(fixture);

      expect(await sawAttribute(page, 'scrollbar-vertical', 'data-hovering')).toBe(false);
      await expect(page.getByTestId('scrollbar-vertical')).not.toHaveAttribute('data-hovering');
    });

    // React's synthetic `target` (the viewport) and the native composed path (`[body, viewport]`) have no DOM counterpart:
    // the DOM's divergence between an event's target and its composed path is shadow retargeting. The Root is moved into a
    // shadow root, and the real mouse enters and leaves the viewport there.
    test('port: adds [data-hovering] when the pointer target is inside a shadow root', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'vertical', keepMounted: true });
      await moveRootIntoShadowRoot(page);
      const verticalScrollbar = page.getByTestId('scrollbar-vertical');

      await page.mouse.move(0, 0);
      await expect(verticalScrollbar).not.toHaveAttribute('data-hovering');

      const point = await pointIn(page, 'viewport', 50, 50);
      await page.mouse.move(point.x, point.y);

      await expect(verticalScrollbar).toHaveAttribute('data-hovering', '');

      await page.mouse.move(0, 0);

      await expect(verticalScrollbar).not.toHaveAttribute('data-hovering');
    });
  });

  test.describe('track pointer down', () => {
    test('ignores non-primary pointer presses', async ({ page, mode }) => {
      await gotoParts(page, mode, {
        content: false,
        bars: 'vertical',
        keepMounted: true,
        viewportStyle: 'scroll-snap-type: y mandatory',
      });
      const viewport = page.getByTestId('viewport');

      const point = await pointIn(page, 'scrollbar-vertical', 5, 100);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down({ button: 'right' });

      expect(await scrollOf(page, 'top')).toBe(0);
      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
      await page.mouse.up({ button: 'right' });
    });

    test('handles a track press when no viewport is mounted', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      const errors = collectPageErrors(page);
      const fixture = await gotoParts(page, mode, { viewport: false, bars: 'vertical', keepMounted: true }, { measured: false });
      const scrollbar = page.getByTestId('scrollbar-vertical');

      const point = await pointIn(page, 'scrollbar-vertical', 5, 100);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();
      await page.mouse.up();
      await settle(fixture);

      expect(await sawAttribute(page, 'scrollbar-vertical', 'data-scrolling')).toBe(false);
      await expect(scrollbar).not.toHaveAttribute('data-scrolling');
      expect(errors).toEqual([]);
    });

    test('does not start a track gesture without a thumb', async ({ page, mode }) => {
      await gotoParts(page, mode, {
        content: false,
        bars: 'vertical',
        keepMounted: true,
        thumb: false,
        viewportStyle: 'scroll-snap-type: y mandatory',
      });
      const viewport = page.getByTestId('viewport');

      const point = await pointIn(page, 'scrollbar-vertical', 5, 100);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();

      expect(await scrollOf(page, 'top')).toBe(0);
      expect(await inlineStyle(viewport, 'scroll-snap-type')).toBe('y mandatory');
      await page.mouse.up();
    });

    // React's synthetic target (the scrollbar) against a native path that starts at the thumb has no DOM counterpart; the
    // DOM's divergence between target and composed path is shadow retargeting. The Root is moved into a shadow root and the
    // real mouse presses the thumb there: the scrollbar's track handler must see the press as the thumb's and not jump.
    test('port: ignores thumb clicks when the press is inside a shadow root', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'vertical', keepMounted: true });
      await moveRootIntoShadowRoot(page);

      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();

      expect(await scrollOf(page, 'top')).toBe(0);
      await page.mouse.up();
      expect(await scrollOf(page, 'top')).toBe(0);
    });

    test('marks the scroll area as scrolling when pressing the track', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      await gotoParts(page, mode, { content: false, bars: 'vertical', keepMounted: true });
      await recordWindowEvents(page, ['pointerdown']);

      const point = await pointIn(page, 'scrollbar-vertical', 5, 160);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();

      expect(await scrollOf(page, 'top')).not.toBe(0);
      const [press] = await windowEvents(page);
      // Base UI stubs `setPointerCapture` on the thumb; the press really captures the pointer there.
      expect(await hasPointerCapture(page.getByTestId('thumb-vertical'), press!.pointerId!)).toBe(true);
      await expect.poll(() => sawAttribute(page, 'scrollbar-vertical', 'data-scrolling')).toBe(true);
      await page.mouse.up();
    });

    // `fireEvent.pointerCancel` is a real CDP `touchCancel` of a touch that pressed the track. Base UI's next
    // `pointerMove` keeps the cancelled pointer's id with a button down, which no real input can send after the cancel, so
    // that one move is dispatched in the page (the only synthetic event of this title).
    testWithTouch('clears track drag state on pointer cancel', async ({ touchPage: page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'vertical', keepMounted: true });
      await recordWindowEvents(page, ['pointerdown']);

      const point = await pointIn(page, 'scrollbar-vertical', 5, 160);
      const touch = await touchInput(page);
      try {
        await touch.start({ id: 0, ...point });
        await expect.poll(() => scrollOf(page, 'top')).not.toBe(0);
        const scrollTopAfterTrackPress = await scrollOf(page, 'top');
        const [press] = await windowEvents(page);

        await touch.cancel();
        await page.getByTestId('thumb-vertical').evaluate(
          (thumb, { pointerId, clientX, clientY }) => {
            thumb.dispatchEvent(
              new PointerEvent('pointermove', { bubbles: true, cancelable: true, pointerId, pointerType: 'touch', buttons: 1, clientX, clientY }),
            );
          },
          { pointerId: press!.pointerId!, clientX: point.x, clientY: point.y + 20 },
        );

        expect(await scrollOf(page, 'top')).toBe(scrollTopAfterTrackPress);
      } finally {
        await touch.detach();
      }
    });
  });

  // Base UI dispatches a cancelable `mousedown` and reads `defaultPrevented`, because jsdom has no focus side of a press; a
  // real press here is read by a window listener, and the focused viewport keeps focus.
  test.describe('track mouse down', () => {
    const presses = [
      { name: 'primary', button: 'left' },
      { name: 'middle', button: 'middle' },
      { name: 'secondary', button: 'right' },
    ] as const;

    for (const { name, button } of presses) {
      test(`cancels a ${name} press on the track so focus stays on the active element`, async ({ page, mode }) => {
        await gotoParts(page, mode, { bars: 'vertical', keepMounted: true });
        await recordWindowEvents(page, ['mousedown']);
        const viewport = page.getByTestId('viewport');
        await viewport.focus();

        const point = await pointIn(page, 'scrollbar-vertical', 5, 120);
        await page.mouse.move(point.x, point.y);
        await page.mouse.down({ button });

        const [mousedown] = await windowEvents(page);
        expect(mousedown!.target).toBe('scrollbar-vertical');
        expect(mousedown!.defaultPrevented).toBe(true);
        await expect(viewport).toBeFocused();
        await page.mouse.up({ button });
      });
    }

    test('cancels the press on the thumb so focus stays on the active element', async ({ page, mode }) => {
      await gotoParts(page, mode, { bars: 'vertical', keepMounted: true });
      await recordWindowEvents(page, ['mousedown']);
      const viewport = page.getByTestId('viewport');
      await viewport.focus();

      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();

      const [mousedown] = await windowEvents(page);
      expect(mousedown!.target).toBe('thumb-vertical');
      expect(mousedown!.defaultPrevented).toBe(true);
      await expect(viewport).toBeFocused();
      await page.mouse.up();
    });
  });

  test.describe('track click by axis', () => {
    async function renderAxisTrack(page: Page, mode: 'server' | 'wasm', orientation: 'horizontal' | 'vertical', direction: 'ltr' | 'rtl') {
      await gotoParts(page, mode, { content: false, bars: orientation, keepMounted: true, dir: direction });
      const thumb = page.getByTestId(`thumb-${orientation}`);
      await expect.poll(() => thumb.evaluate((element) => (element as HTMLElement).offsetWidth + (element as HTMLElement).offsetHeight)).toBeGreaterThan(0);
      return (await page.getByTestId(`scrollbar-${orientation}`).boundingBox())!;
    }

    test('scrolls down when clicking below the thumb on a vertical track', async ({ page, mode }) => {
      const rect = await renderAxisTrack(page, mode, 'vertical', 'ltr');

      await page.mouse.move(rect.x + rect.width / 2, rect.y + rect.height - 5);
      await page.mouse.down();

      expect(await scrollOf(page, 'top')).toBeGreaterThan(0);
      await page.mouse.up();
    });

    test('scrolls right when clicking the end of a horizontal LTR track', async ({ page, mode }) => {
      const rect = await renderAxisTrack(page, mode, 'horizontal', 'ltr');

      await page.mouse.move(rect.x + rect.width - 5, rect.y + rect.height / 2);
      await page.mouse.down();

      expect(await scrollOf(page, 'left')).toBeGreaterThan(0);
      await page.mouse.up();
    });

    test('scrolls into the negative RTL range when clicking a horizontal RTL track', async ({ page, mode }) => {
      const rect = await renderAxisTrack(page, mode, 'horizontal', 'rtl');

      await page.mouse.move(rect.x + 5, rect.y + rect.height / 2);
      await page.mouse.down();

      expect(await scrollOf(page, 'left')).toBeLessThan(0);
      await page.mouse.up();
    });
  });

  test.describe('scroll snap on track press', () => {
    test('does not snap the initial jump-to-click position', async ({ page, mode }) => {
      await gotoParts(page, mode, {
        content: false,
        rootWidth: 400,
        bars: 'horizontal',
        keepMounted: true,
        items: 10,
        viewportStyle: 'scroll-snap-type: x mandatory',
      });
      const thumb = page.getByTestId('thumb-horizontal');
      await expect.poll(() => thumb.evaluate((element) => (element as HTMLElement).offsetWidth)).toBeGreaterThan(0);

      // Aim mid-way between the 800 and 1000 snap points (200px items).
      const targetScroll = 900;
      const { maxLeft: maxScroll } = await viewportMetrics(page);
      const { trackWidth, thumbWidth } = await page.getByTestId('scrollbar-horizontal').evaluate((scrollbar) => ({
        trackWidth: (scrollbar as HTMLElement).offsetWidth,
        thumbWidth: (scrollbar.firstElementChild as HTMLElement).offsetWidth,
      }));
      const rect = (await page.getByTestId('scrollbar-horizontal').boundingBox())!;
      const clickX = rect.x + (targetScroll / maxScroll) * (trackWidth - thumbWidth) + thumbWidth / 2;

      await page.mouse.move(clickX, rect.y + rect.height / 2);
      await page.mouse.down();

      expect(Math.abs((await scrollOf(page, 'left')) - targetScroll)).toBeLessThanOrEqual(1);

      // Releasing restores snapping, which re-snaps to the nearest snap point.
      await page.mouse.up();
      await expect.poll(async () => (await scrollOf(page, 'left')) % 200).toBe(0);
    });
  });

  // The thumb is half the scrollbar's width here, so a real press on the scrollbar's other half reaches the track (Base UI
  // dispatches on the scrollbar element under a thumb that covers it).
  test.describe('non-positive thumb offset', () => {
    async function renderShortTrack(page: Page, mode: 'server' | 'wasm', trackHeight: number) {
      await gotoParts(page, mode, {
        content: false,
        bars: 'vertical',
        keepMounted: true,
        vStyle: `height: ${trackHeight}px; bottom: auto; width: 12px`,
        vThumbStyle: 'width: 50%',
      });
      const thumb = page.getByTestId('thumb-vertical');
      await expect.poll(() => thumb.evaluate((element) => (element as HTMLElement).offsetHeight)).toBeGreaterThan(0);

      // Park the scroll mid-range so an erroneous jump to an edge is detectable; the measurement of that scroll has run
      // once the viewport's variable shows it.
      await scrollViewport(page, { top: 400 });
      await expect.poll(() => inlineStyle(page.getByTestId('viewport'), '--scroll-area-overflow-y-start')).toBe('400px');
    }

    async function dragThumb(page: Page) {
      const point = await pointIn(page, 'thumb-vertical', (width) => width / 2, (height) => height / 2);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();
      await page.mouse.move(point.x, point.y + 5);
    }

    async function pressTrack(page: Page) {
      const point = await pointIn(page, 'scrollbar-vertical', 9, 5);
      await page.mouse.move(point.x, point.y);
      await page.mouse.down();
    }

    test('does not jump the scroll when dragging a thumb that fills the track', async ({ page, mode }) => {
      await renderShortTrack(page, mode, 16);

      await dragThumb(page);

      expect(await scrollOf(page, 'top')).toBe(400);
      await page.mouse.up();
    });

    test('does not jump the scroll when dragging a thumb taller than the track', async ({ page, mode }) => {
      await renderShortTrack(page, mode, 10);

      await dragThumb(page);

      expect(await scrollOf(page, 'top')).toBe(400);
      await page.mouse.up();
    });

    test('does not jump the scroll when clicking a track whose thumb fills the track', async ({ page, mode }) => {
      await renderShortTrack(page, mode, 16);

      await pressTrack(page);

      expect(await scrollOf(page, 'top')).toBe(400);
      await page.mouse.up();
    });

    test('does not jump the scroll when clicking a track whose thumb is taller than the track', async ({ page, mode }) => {
      await renderShortTrack(page, mode, 10);

      await pressTrack(page);

      expect(await scrollOf(page, 'top')).toBe(400);
      await page.mouse.up();
    });
  });

  // `fireEvent.wheel` is a real wheel over the scrollbar (`wheelOver`); the viewport's offsets are real and clamped by the
  // browser, where Base UI's mock stores whatever is assigned.
  test.describe('wheel', () => {
    async function renderWheelTest(
      page: Page,
      mode: 'server' | 'wasm',
      props: { direction?: 'ltr' | 'rtl'; orientation?: 'horizontal' | 'vertical'; scrollLeft?: number; scrollTop?: number },
    ) {
      const { direction = 'ltr', orientation = 'horizontal', scrollLeft = 0, scrollTop = 0 } = props;
      const fixture = await gotoParts(page, mode, { content: false, dir: direction, bars: orientation, keepMounted: true, thumb: false });
      if (scrollLeft !== 0 || scrollTop !== 0) {
        await setScroll(page, { left: scrollLeft, top: scrollTop });
      }
      return { fixture, scrollbar: page.getByTestId(`scrollbar-${orientation}`) };
    }

    // A programmatic scroll whose measurement has run (the viewport's variables are written in its `scroll` handler).
    async function setScroll(page: Page, position: { left?: number; top?: number }) {
      await scrollViewport(page, position);
      await expect
        .poll(async () => {
          const metrics = await viewportMetrics(page);
          return (position.left === undefined || metrics.left === position.left) && (position.top === undefined || metrics.top === position.top);
        })
        .toBe(true);
    }

    test('allows horizontal scrolling away from the RTL start edge', async ({ page, mode }) => {
      const { scrollbar } = await renderWheelTest(page, mode, { direction: 'rtl' });

      await wheelOver(scrollbar, -50, 0);

      await expect.poll(() => scrollOf(page, 'left')).toBe(-50);
    });

    test('clamps horizontal LTR wheel scrolling at both edges', async ({ page, mode }) => {
      const { scrollbar } = await renderWheelTest(page, mode, { direction: 'ltr' });

      await wheelOver(scrollbar, -50, 0);
      expect(await scrollOf(page, 'left')).toBe(0);

      await setScroll(page, { left: 790 });
      await wheelOver(scrollbar, 50, 0);
      await expect.poll(() => scrollOf(page, 'left')).toBe(800);

      await wheelOver(scrollbar, 50, 0);
      expect(await scrollOf(page, 'left')).toBe(800);
    });

    test('clamps horizontal RTL wheel scrolling at both edges', async ({ page, mode }) => {
      const { scrollbar } = await renderWheelTest(page, mode, { direction: 'rtl' });

      await wheelOver(scrollbar, 50, 0);
      expect(await scrollOf(page, 'left')).toBe(0);

      await setScroll(page, { left: -100 });
      await wheelOver(scrollbar, 50, 0);
      await expect.poll(() => scrollOf(page, 'left')).toBe(-50);

      await setScroll(page, { left: -790 });
      await wheelOver(scrollbar, -50, 0);
      await expect.poll(() => scrollOf(page, 'left')).toBe(-800);

      await wheelOver(scrollbar, -50, 0);
      expect(await scrollOf(page, 'left')).toBe(-800);

      await setScroll(page, { left: -10 });
      await wheelOver(scrollbar, 50, 0);
      await expect.poll(() => scrollOf(page, 'left')).toBe(0);
    });

    test('clamps vertical wheel scrolling at both edges', async ({ page, mode }) => {
      const { scrollbar } = await renderWheelTest(page, mode, { orientation: 'vertical' });

      await wheelOver(scrollbar, 0, -50);
      expect(await scrollOf(page, 'top')).toBe(0);

      await setScroll(page, { top: 790 });
      await wheelOver(scrollbar, 0, 50);
      await expect.poll(() => scrollOf(page, 'top')).toBe(800);

      await wheelOver(scrollbar, 0, 50);
      expect(await scrollOf(page, 'top')).toBe(800);
    });

    // `fireEvent.wheel`'s return value is `!defaultPrevented`; a window listener reads it from each real wheel.
    test('preventDefaults only when it consumes the scroll, allowing chaining at edges', async ({ page, mode }) => {
      const { scrollbar } = await renderWheelTest(page, mode, { orientation: 'vertical' });
      await recordWindowEvents(page, ['wheel']);

      // Mid-range: the wheel scroll is consumed, so the event is cancelled.
      await setScroll(page, { top: 400 });
      await wheelOver(scrollbar, 0, 50);

      // At the end edge scrolling further: not consumed, so the event chains to the parent/page.
      await setScroll(page, { top: 800 });
      await wheelOver(scrollbar, 0, 50);

      // At the start edge scrolling further backward, the event chains too.
      await setScroll(page, { top: 0 });
      await wheelOver(scrollbar, 0, -50);

      await expect.poll(async () => (await windowEvents(page)).map((event) => event.defaultPrevented)).toEqual([true, false, false]);
    });

    // A zero-delta wheel is no input a real wheel makes: CDP's `Input.dispatchMouseEvent` sends one.
    test('ignores zero-delta wheel events', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      const { fixture, scrollbar } = await renderWheelTest(page, mode, { orientation: 'vertical', scrollTop: 400 });
      await recordWindowEvents(page, ['wheel']);

      const point = await pointIn(page, 'scrollbar-vertical', 5, 100);
      await page.mouse.move(point.x, point.y);
      const cdp = await page.context().newCDPSession(page);
      try {
        await cdp.send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: point.x, y: point.y, deltaX: 0, deltaY: 0 });
      } finally {
        await cdp.detach();
      }
      await expect.poll(async () => (await windowEvents(page)).length).toBe(1);
      await settle(fixture);

      expect((await windowEvents(page))[0]!.defaultPrevented).toBe(false);
      expect(await scrollOf(page, 'top')).toBe(400);
      expect(await sawAttribute(page, 'scrollbar-vertical', 'data-scrolling')).toBe(false);
      await expect(scrollbar).not.toHaveAttribute('data-scrolling');
    });

    // The Ctrl modifier is sent through CDP. A context of its own (the touch context, unused for touch here) keeps a zoom the
    // browser may apply away from the worker's shared context.
    testWithTouch('does not intercept browser zoom gestures', async ({ touchPage: page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      const { fixture, scrollbar } = await renderWheelTest(page, mode, { orientation: 'vertical', scrollTop: 400 });
      await recordWindowEvents(page, ['wheel']);

      const point = await pointIn(page, 'scrollbar-vertical', 5, 100);
      await page.mouse.move(point.x, point.y);
      const cdp = await page.context().newCDPSession(page);
      try {
        await cdp.send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: point.x, y: point.y, deltaX: 0, deltaY: 50, modifiers: 2 });
      } finally {
        await cdp.detach();
      }
      await expect.poll(async () => (await windowEvents(page)).length).toBe(1);
      await settle(fixture);

      expect((await windowEvents(page))[0]!.defaultPrevented).toBe(false);
      expect(await scrollOf(page, 'top')).toBe(400);
      expect(await sawAttribute(page, 'scrollbar-vertical', 'data-scrolling')).toBe(false);
      await expect(scrollbar).not.toHaveAttribute('data-scrolling');
    });

    test('marks the scroll area as scrolling when wheeling over the scrollbar', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      const { scrollbar } = await renderWheelTest(page, mode, { orientation: 'vertical' });

      await wheelOver(scrollbar, 0, 50);

      await expect.poll(() => sawAttribute(page, 'scrollbar-vertical', 'data-scrolling')).toBe(true);
    });

    test('marks the scroll area as scrolling when wheeling over the horizontal scrollbar', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      const { scrollbar } = await renderWheelTest(page, mode, { orientation: 'horizontal' });

      await wheelOver(scrollbar, 50, 0);

      await expect.poll(() => sawAttribute(page, 'scrollbar-horizontal', 'data-scrolling')).toBe(true);
    });

    test('does not mark the scroll area as scrolling when chaining at an edge', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      const { fixture, scrollbar } = await renderWheelTest(page, mode, { orientation: 'vertical' });
      await recordWindowEvents(page, ['wheel']);

      // At the end edge scrolling further chains to the page without consuming the
      // scroll, so the area must not be marked as scrolling.
      await setScroll(page, { top: 800 });
      const mark = await sampleMark(page);
      await wheelOver(scrollbar, 0, 50);
      await expect.poll(async () => (await windowEvents(page)).length).toBe(1);
      await settle(fixture);

      expect(await sawAttribute(page, 'scrollbar-vertical', 'data-scrolling', mark)).toBe(false);
      await expect(scrollbar).not.toHaveAttribute('data-scrolling');
    });

    // The scrollbar mounts with the overflow; its handle has attached once its thumb-size variable holds the measured size.
    test('registers after the horizontal scrollbar becomes visible', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, dir: 'rtl', bars: 'horizontal', width: 1000, height: 200 });
      const horizontalScrollbar = page.getByTestId('scrollbar-horizontal');

      await expect(horizontalScrollbar).toHaveAttribute('data-has-overflow-x');
      await expect.poll(() => computed(horizontalScrollbar, '--scroll-area-thumb-width')).not.toBe('0px');

      await wheelOver(horizontalScrollbar, -50, 0);

      await expect.poll(() => scrollOf(page, 'left')).toBe(-50);
    });
  });

  test.describe('data overflow attributes (scrollbars)', () => {
    test('applies data attributes on vertical and horizontal scrollbars', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false });
      const vScrollbar = page.getByTestId('scrollbar-vertical');
      const hScrollbar = page.getByTestId('scrollbar-horizontal');
      const flags = async () => ({
        ...(await attributeFlags(vScrollbar, ['data-has-overflow-y', 'data-overflow-y-start', 'data-overflow-y-end'])),
        ...(await attributeFlags(hScrollbar, ['data-has-overflow-x', 'data-overflow-x-start', 'data-overflow-x-end'])),
      });

      await expect.poll(flags).toEqual({
        'data-has-overflow-y': true,
        'data-overflow-y-start': false,
        'data-overflow-y-end': true,
        'data-has-overflow-x': true,
        'data-overflow-x-start': false,
        'data-overflow-x-end': true,
      });

      // Scroll to middle
      const { maxTop, maxLeft } = await viewportMetrics(page);
      await scrollViewport(page, { top: maxTop / 2, left: maxLeft / 2 });

      await expect(vScrollbar).toHaveAttribute('data-overflow-y-start');
      await expect(vScrollbar).toHaveAttribute('data-overflow-y-end');
      await expect(hScrollbar).toHaveAttribute('data-overflow-x-start');
      await expect(hScrollbar).toHaveAttribute('data-overflow-x-end');
    });
  });
});
