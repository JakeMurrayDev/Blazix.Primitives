// L4 (ADR 06 §2): Base UI's ScrollAreaViewport.test.tsx on both render modes (docs/components/scroll-area.md §6.2, §9, §13).
// Titles verbatim but for the React `flushSync` title. Base UI's L2-only titles (the presentational role, the error outside
// a Root) are in the component tests. `fireEvent.pointerEnter`/`pointerDown` become real mouse and CDP touch input, fake
// timers become `page.clock`, and the ResizeObserver and `getAnimations` spies become init scripts.
import { expect, test, testWithTouch, wheelOver, type Page } from './helpers';
import {
  attributeFlags,
  collectPageErrors,
  gotoParts,
  holdClock,
  pointIn,
  recordAttributes,
  sawAttribute,
  scrollViewport,
  settle,
  touchInput,
} from './scroll-area.helpers';

const SCROLL_TIMEOUT = 500;

/** A programmatic scroll, resolved once its `scroll` event reached a listener added after the module's. */
async function scrollAndMeasure(page: Page, top: number): Promise<void> {
  await page.getByTestId('viewport').evaluate(
    (element, offset) =>
      new Promise<void>((resolve) => {
        element.addEventListener('scroll', () => resolve(), { once: true });
        element.scrollTop = offset;
      }),
    top,
  );
}

/**
 * Waits for one real rendering update of the page: a new element's first ResizeObserver notification, which the browser
 * delivers in the frame's rendering steps (after it updated the animations). `page.clock` fakes `requestAnimationFrame`.
 */
function renderingUpdate(page: Page): Promise<void> {
  return page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        const probe = document.createElement('div');
        document.body.append(probe);
        const observer = new ResizeObserver(() => {
          observer.disconnect();
          probe.remove();
          resolve();
        });
        observer.observe(probe);
      }),
  );
}

/**
 * Replaces `getAnimations` on the viewport (Base UI defines it on the node): a subtree read returns one animation whose
 * `finished` the title resolves with `window.resolveAnimation()`, plus, for `infinite`, one with infinite timing that never
 * finishes. `window.animationReads` counts the reads. Optionally stubs `ResizeObserver.prototype.observe`, as Base UI's
 * `vi.spyOn(...).mockImplementation(() => {})`. Call before navigating.
 */
async function stubViewportAnimations(page: Page, options: { infinite?: { iterations?: number; duration?: number } | undefined; stubResizeObserver?: boolean }) {
  await page.addInitScript(({ infinite, stubResizeObserver }) => {
    const state = window as unknown as { animationReads: number; resolveAnimation: () => void };
    state.animationReads = 0;
    let resolve: () => void = () => {};
    const finished = new Promise<void>((done) => {
      resolve = done;
    });
    state.resolveAnimation = () => resolve();
    if (stubResizeObserver) {
      ResizeObserver.prototype.observe = () => {};
    }
    const timing = infinite ? { ...infinite, ...(infinite.iterations === -1 ? { iterations: Infinity } : {}), ...(infinite.duration === -1 ? { duration: Infinity } : {}) } : null;
    const original = Element.prototype.getAnimations;
    Element.prototype.getAnimations = function getAnimations(this: Element, getOptions?: GetAnimationsOptions) {
      if (this.getAttribute('data-testid') === 'viewport' && getOptions?.subtree) {
        state.animationReads += 1;
        return [
          { finished },
          ...(timing ? [{ finished: new Promise<void>(() => {}), effect: { getTiming: () => timing } }] : []),
        ] as unknown as Animation[];
      }
      return original.call(this, getOptions);
    };
  }, options);
}

test.describe('ScrollArea.Viewport', () => {
  // React's `onScroll` with `flushSync(() => setMounted(false))` has no Blazor counterpart (a C# handler runs after the
  // event, a round trip later on Server): a consumer's JavaScript `scroll` listener, at the viewport ahead of the module's,
  // removes the viewport's element in the middle of the dispatch of a real wheel's scroll.
  test('port: handles a consumer scroll listener removing the viewport', async ({ page, mode }) => {
    const errors = collectPageErrors(page);
    await gotoParts(page, mode, { content: false, bars: 'none' });
    const viewport = page.getByTestId('viewport');
    await viewport.evaluate((element) => {
      element.addEventListener('scroll', () => element.remove(), { capture: true, once: true });
    });

    await wheelOver(viewport, 0, 100);

    await expect(viewport).toHaveCount(0);
    expect(errors).toEqual([]);
  });

  test.describe('subtree animations', () => {
    // Chromium only, as Base UI's `skipIf(!platform.engine.blink)`. The two animated targets are added by an init script
    // when the viewport appears, as Base UI's ref callback appends them, so Blazor holds no reference to them; the script
    // keeps only a `WeakRef` to the first. CDP's `HeapProfiler.collectGarbage` collects it.
    test('releases a completed animation target while another animation is paused', async ({ page, mode }) => {
      await page.addInitScript(() => {
        const state = window as unknown as { targetRef?: WeakRef<HTMLDivElement>; animationsRead: boolean };
        state.animationsRead = false;
        const original = Element.prototype.getAnimations;
        Element.prototype.getAnimations = function getAnimations(this: Element, options?: GetAnimationsOptions) {
          if (this.getAttribute('data-testid') === 'viewport') {
            state.animationsRead = true;
          }
          return original.call(this, options);
        };
        const observer = new MutationObserver(() => {
          const node = document.querySelector('[data-testid="viewport"]');
          if (!node || node.querySelector('[data-animated-target]')) {
            return;
          }
          const target = document.createElement('div');
          const pausedTarget = document.createElement('div');
          target.setAttribute('data-animated-target', 'completed');
          pausedTarget.setAttribute('data-animated-target', 'paused');
          node.append(target, pausedTarget);
          state.targetRef = new WeakRef(target);
          for (const element of [target, pausedTarget]) {
            element.animate({ opacity: [0, 1] }, { duration: 1000, fill: 'forwards' }).pause();
          }
        });
        observer.observe(document, { childList: true, subtree: true });
      });
      await gotoParts(page, mode, { content: false, bars: 'none', width: 100, height: 100 });

      await expect.poll(() => page.evaluate(() => (window as unknown as { animationsRead: boolean }).animationsRead)).toBe(true);

      await page.evaluate(async () => {
        const target = document.querySelector('[data-animated-target="completed"]')!;
        target.getAnimations()[0]!.finish();
        for (let i = 0; i < 5; i++) {
          await Promise.resolve();
        }
        target.remove();
      });
      await renderingUpdate(page);

      const cdp = await page.context().newCDPSession(page);
      try {
        await cdp.send('HeapProfiler.collectGarbage');
      } finally {
        await cdp.detach();
      }

      expect(await page.evaluate(() => (window as unknown as { targetRef: WeakRef<HTMLDivElement> }).targetRef.deref() === undefined)).toBe(true);
      expect(await page.evaluate(() => document.querySelector('[data-animated-target="paused"]')!.getAnimations()[0]!.playState)).toBe('paused');
    });

    // Base UI mocks the viewport's metrics (100 px wide, then 1,000 px); here the content is 100 px wide in a 200 px viewport
    // and is widened in the page while the ResizeObserver is stubbed, so only the animation's end can measure it.
    const variants = [
      ['no infinite animation', undefined],
      ['infinite iterations', { iterations: -1 }],
      ['infinite duration', { duration: -1, iterations: 1 }],
    ] as const;
    for (const [description, infiniteTiming] of variants) {
      test(`recomputes overflow after a subtree animation finishes with ${description}`, async ({ page, mode }) => {
        await stubViewportAnimations(page, { infinite: infiniteTiming, stubResizeObserver: true });
        const fixture = await gotoParts(page, mode, { content: false, bars: 'horizontal', keepMounted: true, width: 100, height: 100 });
        const root = page.getByTestId('root');

        await expect.poll(() => page.evaluate(() => (window as unknown as { animationReads: number }).animationReads)).toBeGreaterThan(0);
        await settle(fixture);
        await expect(root).not.toHaveAttribute('data-has-overflow-x');

        await page.getByTestId('inner').evaluate((element) => {
          (element as HTMLElement).style.width = '1000px';
        });
        await page.evaluate(() => (window as unknown as { resolveAnimation: () => void }).resolveAnimation());

        await expect(root).toHaveAttribute('data-has-overflow-x');
      });
    }

    test('ignores an animation finishing after its viewport unmounts', async ({ page, mode }) => {
      const errors = collectPageErrors(page);
      await stubViewportAnimations(page, {});
      const fixture = await gotoParts(page, mode, { content: false, bars: 'none' });

      await expect.poll(() => page.evaluate(() => (window as unknown as { animationReads: number }).animationReads)).toBeGreaterThan(0);

      await page.getByTestId('unmount-viewport').click();
      await expect(page.getByTestId('viewport')).toHaveCount(0);

      await page.evaluate(async () => {
        (window as unknown as { resolveAnimation: () => void }).resolveAnimation();
        await new Promise((resolve) => setTimeout(resolve, 0));
      });
      await settle(fixture);
      expect(errors).toEqual([]);
    });
  });

  test.describe('data-scrolling attribute', () => {
    // Fake timers: the page's clock is held from before the gesture. `fireEvent.pointerEnter` is a real mouse move onto the
    // viewport; `fireEvent.scroll` a programmatic scroll whose `scroll` event the browser sends.
    test('adds [data-scrolling] attribute when viewport is scrolled', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'none' });
      const viewport = page.getByTestId('viewport');
      await holdClock(page);
      try {
        await expect(viewport).not.toHaveAttribute('data-scrolling');

        const point = await pointIn(page, 'viewport', 50, 50);
        await page.mouse.move(point.x, point.y);
        await scrollViewport(page, { top: 1 });

        await expect(viewport).toHaveAttribute('data-scrolling', '');

        await page.clock.runFor(SCROLL_TIMEOUT);

        await expect(viewport).not.toHaveAttribute('data-scrolling');

        // Test horizontal scrolling
        await page.mouse.move(point.x + 10, point.y + 10);
        await scrollViewport(page, { left: 1 });

        await expect(viewport).toHaveAttribute('data-scrolling', '');

        await page.clock.runFor(SCROLL_TIMEOUT);

        await expect(viewport).not.toHaveAttribute('data-scrolling');
      } finally {
        await page.clock.resume();
      }
    });

    test('ignores data-scrolling during programmatic scroll', async ({ page, mode }) => {
      await recordAttributes(page, ['data-scrolling']);
      const fixture = await gotoParts(page, mode, { content: false, bars: 'none', viewportStyle: 'pointer-events: none' });

      // No user interaction before the scroll event, as with `scrollTo()`.
      // `pointer-events: none` isolates this from the browser's real pointer.
      await scrollAndMeasure(page, 1);
      await settle(fixture);

      expect(await sawAttribute(page, 'viewport', 'data-scrolling')).toBe(false);
      await expect(page.getByTestId('viewport')).not.toHaveAttribute('data-scrolling');
    });

    // The touch is a real CDP tap on the viewport; the scroll that follows is programmatic, standing in for WebKit's
    // event-less momentum scroll.
    testWithTouch('adds [data-scrolling] in touch modality even when the gesture delivers no events', async ({ touchPage: page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'none' });
      const viewport = page.getByTestId('viewport');
      const point = await pointIn(page, 'viewport', 50, 50);

      await holdClock(page);
      const touch = await touchInput(page);
      try {
        // The initial touch is delivered normally and establishes touch modality.
        await touch.start({ id: 0, ...point });
        await touch.end(0);

        // A touch that catches an in-flight momentum scroll or rubber-band
        // bounce is consumed natively by WebKit: no touch/pointer events fire
        // for the whole gesture, only scroll events after an arbitrary pause.
        await page.clock.runFor(200);
        await scrollViewport(page, { top: 1 });

        await expect(viewport).toHaveAttribute('data-scrolling', '');

        await page.clock.runFor(SCROLL_TIMEOUT);

        await expect(viewport).not.toHaveAttribute('data-scrolling');
      } finally {
        await touch.detach();
        await page.clock.resume();
      }
    });

    test('keeps ignoring programmatic scrolls in mouse modality', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'none' });
      const viewport = page.getByTestId('viewport');
      const point = await pointIn(page, 'viewport', 50, 50);

      await holdClock(page);
      try {
        await page.mouse.move(point.x, point.y);
        await page.mouse.down();
        await page.mouse.up();

        await page.clock.runFor(200);
        await scrollAndMeasure(page, 1);

        await page.clock.runFor(SCROLL_TIMEOUT);

        await expect(viewport).not.toHaveAttribute('data-scrolling');
      } finally {
        await page.clock.resume();
      }
    });

    // The touch is a real CDP tap; the mouse move is real, over the vertical scrollbar, which is the root's and not the
    // viewport's (Base UI fires it on the root).
    testWithTouch('restores programmatic scroll suppression after modality flips back to mouse', async ({ touchPage: page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'vertical', keepMounted: true });
      const viewport = page.getByTestId('viewport');
      const point = await pointIn(page, 'viewport', 50, 50);
      const scrollbarPoint = await pointIn(page, 'scrollbar-vertical', 5, 150);

      await holdClock(page);
      const touch = await touchInput(page);
      try {
        await touch.start({ id: 0, ...point });
        await touch.end(0);
        await scrollViewport(page, { top: 1 });

        await expect(viewport).toHaveAttribute('data-scrolling', '');

        await page.clock.runFor(SCROLL_TIMEOUT);

        await expect(viewport).not.toHaveAttribute('data-scrolling');

        // A mouse pointermove on the root (not the viewport, whose own
        // handlers mark user interaction) switches back to mouse modality.
        await page.mouse.move(scrollbarPoint.x, scrollbarPoint.y);
        await scrollAndMeasure(page, 2);

        await expect(viewport).not.toHaveAttribute('data-scrolling');
      } finally {
        await touch.detach();
        await page.clock.resume();
      }
    });

    test('removes [data-scrolling] after timeout', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'none' });
      const viewport = page.getByTestId('viewport');
      await holdClock(page);
      try {
        // Start scrolling
        const point = await pointIn(page, 'viewport', 50, 50);
        await page.mouse.move(point.x, point.y);
        await scrollViewport(page, { top: 1 });

        await expect(viewport).toHaveAttribute('data-scrolling', '');

        // Wait less than timeout - should still be scrolling
        await page.clock.runFor(SCROLL_TIMEOUT - 1);

        await expect(viewport).toHaveAttribute('data-scrolling', '');

        // Wait for remaining timeout
        await page.clock.runFor(1);

        await expect(viewport).not.toHaveAttribute('data-scrolling');
      } finally {
        await page.clock.resume();
      }
    });
  });

  test.describe('overflow data attributes (viewport)', () => {
    test('applies data attributes on viewport', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'none' });
      const viewport = page.getByTestId('viewport');

      await expect
        .poll(() =>
          attributeFlags(viewport, [
            'data-has-overflow-x',
            'data-has-overflow-y',
            'data-overflow-x-start',
            'data-overflow-x-end',
            'data-overflow-y-start',
            'data-overflow-y-end',
          ]),
        )
        .toEqual({
          'data-has-overflow-x': true,
          'data-has-overflow-y': true,
          'data-overflow-x-start': false,
          'data-overflow-x-end': true,
          'data-overflow-y-start': false,
          'data-overflow-y-end': true,
        });
    });
  });

  // Only Safari reports an out-of-range `scrollTop`/`scrollLeft` while rubber-banding, and the
  // browser clamps the property on assignment, so the getter is mocked to emulate it against
  // real layout (Base UI's own approach), and the `scroll` event is dispatched in the page.
  test.describe('overscroll feedback', () => {
    const VIEWPORT_SIZE = 200;
    const CONTENT_SIZE = 1000;
    const MAX_SCROLL = CONTENT_SIZE - VIEWPORT_SIZE;

    async function renderScrollArea(page: Page, mode: 'server' | 'wasm', orientation: 'vertical' | 'horizontal', textDirection: 'ltr' | 'rtl' = 'ltr') {
      await gotoParts(page, mode, {
        content: false,
        dir: textDirection,
        bars: orientation,
        keepMounted: true,
        width: orientation === 'horizontal' ? CONTENT_SIZE : VIEWPORT_SIZE,
        height: orientation === 'vertical' ? CONTENT_SIZE : VIEWPORT_SIZE,
      });
      const scrollbar = page.getByTestId(`scrollbar-${orientation}`);
      const thumb = page.getByTestId(`thumb-${orientation}`);
      const axis = orientation === 'vertical' ? 'height' : 'width';
      await expect.poll(async () => (await thumb.boundingBox())?.[axis] ?? 0).toBeGreaterThan(0);
      return { scrollbar, thumb };
    }

    function overscroll(page: Page, prop: 'scrollTop' | 'scrollLeft', value: number) {
      return page.getByTestId('viewport').evaluate(
        (viewport, { name, offset }) => {
          Object.defineProperty(viewport, name, { configurable: true, get: () => offset });
          viewport.dispatchEvent(new Event('scroll'));
        },
        { name: prop, offset: value },
      );
    }

    const rect = async (locator: ReturnType<Page['getByTestId']>) => (await locator.boundingBox())!;
    const closeTo = (actual: number, expected: number) => expect(Math.abs(actual - expected)).toBeLessThan(0.5);

    test('shrinks and pins the thumb to the start edge while overscrolling past the top', async ({ page, mode }) => {
      const { scrollbar, thumb } = await renderScrollArea(page, mode, 'vertical');
      const restingHeight = (await rect(thumb)).height;

      await overscroll(page, 'scrollTop', -50);

      // Shrinks, but damped by the content length rather than subtracting the raw pixels
      // (a 1:1 subtraction of 50px would collapse this thumb to its minimum size).
      await expect.poll(async () => (await rect(thumb)).height).toBeLessThan(restingHeight);
      expect((await rect(thumb)).height).toBeGreaterThan(restingHeight * 0.9);
      // Pinned to the top edge of the track.
      closeTo((await rect(thumb)).y, (await rect(scrollbar)).y);
    });

    test('shrinks and pins the thumb to the end edge while overscrolling past the bottom', async ({ page, mode }) => {
      const { scrollbar, thumb } = await renderScrollArea(page, mode, 'vertical');
      const restingHeight = (await rect(thumb)).height;

      await overscroll(page, 'scrollTop', MAX_SCROLL + 50);

      await expect.poll(async () => (await rect(thumb)).height).toBeLessThan(restingHeight);
      expect((await rect(thumb)).height).toBeGreaterThan(restingHeight * 0.9);
      // Pinned to the bottom edge of the track.
      const thumbBox = await rect(thumb);
      const scrollbarBox = await rect(scrollbar);
      closeTo(thumbBox.y + thumbBox.height, scrollbarBox.y + scrollbarBox.height);
    });

    test('restores the resting thumb size once the viewport settles back into range', async ({ page, mode }) => {
      const { thumb } = await renderScrollArea(page, mode, 'vertical');
      const restingHeight = (await rect(thumb)).height;

      await overscroll(page, 'scrollTop', -50);
      await expect.poll(async () => (await rect(thumb)).height).toBeLessThan(restingHeight);

      await overscroll(page, 'scrollTop', 100);
      await expect.poll(async () => Math.abs((await rect(thumb)).height - restingHeight) < 0.5).toBe(true);
    });

    test('shrinks and pins the horizontal thumb to the inline start while overscrolling (LTR)', async ({ page, mode }) => {
      const { scrollbar, thumb } = await renderScrollArea(page, mode, 'horizontal');
      const restingWidth = (await rect(thumb)).width;

      await overscroll(page, 'scrollLeft', -50);

      await expect.poll(async () => (await rect(thumb)).width).toBeLessThan(restingWidth);
      expect((await rect(thumb)).width).toBeGreaterThan(restingWidth * 0.9);
      // Inline start is the left edge in LTR.
      closeTo((await rect(thumb)).x, (await rect(scrollbar)).x);
    });

    test('shrinks and pins the horizontal thumb to the inline end while overscrolling (LTR)', async ({ page, mode }) => {
      const { scrollbar, thumb } = await renderScrollArea(page, mode, 'horizontal');
      const restingWidth = (await rect(thumb)).width;

      await overscroll(page, 'scrollLeft', MAX_SCROLL + 50);

      await expect.poll(async () => (await rect(thumb)).width).toBeLessThan(restingWidth);
      expect((await rect(thumb)).width).toBeGreaterThan(restingWidth * 0.9);
      // Inline end is the right edge in LTR.
      const thumbBox = await rect(thumb);
      const scrollbarBox = await rect(scrollbar);
      closeTo(thumbBox.x + thumbBox.width, scrollbarBox.x + scrollbarBox.width);
    });

    test('shrinks and pins the horizontal thumb to the inline start while overscrolling (RTL)', async ({ page, mode }) => {
      const { scrollbar, thumb } = await renderScrollArea(page, mode, 'horizontal', 'rtl');
      const restingWidth = (await rect(thumb)).width;

      // RTL scrolls from 0 toward `-MAX_SCROLL`; overscrolling the inline start goes positive.
      await overscroll(page, 'scrollLeft', 50);

      await expect.poll(async () => (await rect(thumb)).width).toBeLessThan(restingWidth);
      expect((await rect(thumb)).width).toBeGreaterThan(restingWidth * 0.9);
      // Inline start is the right edge in RTL.
      const thumbBox = await rect(thumb);
      const scrollbarBox = await rect(scrollbar);
      closeTo(thumbBox.x + thumbBox.width, scrollbarBox.x + scrollbarBox.width);
    });

    test('shrinks and pins the horizontal thumb to the inline end while overscrolling (RTL)', async ({ page, mode }) => {
      const { scrollbar, thumb } = await renderScrollArea(page, mode, 'horizontal', 'rtl');
      const restingWidth = (await rect(thumb)).width;

      // RTL overscroll past the inline end goes beyond `-MAX_SCROLL`.
      await overscroll(page, 'scrollLeft', -(MAX_SCROLL + 50));

      await expect.poll(async () => (await rect(thumb)).width).toBeLessThan(restingWidth);
      expect((await rect(thumb)).width).toBeGreaterThan(restingWidth * 0.9);
      // Inline end is the left edge in RTL.
      closeTo((await rect(thumb)).x, (await rect(scrollbar)).x);
    });
  });
});
