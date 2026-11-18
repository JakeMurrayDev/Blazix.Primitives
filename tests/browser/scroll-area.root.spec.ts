// L4 (ADR 06 §2): ScrollArea's Root and its measurement, on both render modes (docs/components/scroll-area.md §4, §9).
import { expect, holdCircuit, test, type Page } from './helpers';
import {
  attributeFlags,
  computed,
  gotoParts,
  gotoScrollArea,
  holdClock,
  mockResizeObserver,
  notifyResizeObservers,
  scrollViewport,
  settle,
  viewportMetrics,
} from './scroll-area.helpers';

// Base UI's ScrollAreaRoot.test.tsx constants.
const VIEWPORT_SIZE = 200;
const SCROLLABLE_CONTENT_SIZE = 1000;
const SCROLL_TIMEOUT = 500;

const ALL_ATTRIBUTES = [
  'data-has-overflow-x',
  'data-has-overflow-y',
  'data-overflow-x-start',
  'data-overflow-x-end',
  'data-overflow-y-start',
  'data-overflow-y-end',
] as const;
const Y_ATTRIBUTES = ['data-has-overflow-y', 'data-overflow-y-start', 'data-overflow-y-end'] as const;
const X_ATTRIBUTES = ['data-has-overflow-x', 'data-overflow-x-start', 'data-overflow-x-end'] as const;

/** The flags Base UI's "applies data attributes on root, viewport and scrollbars" compares, read in one evaluation per part. */
async function partFlags(page: Page) {
  return {
    root: await attributeFlags(page.getByTestId('root'), ALL_ATTRIBUTES),
    viewport: await attributeFlags(page.getByTestId('viewport'), ALL_ATTRIBUTES),
    content: await attributeFlags(page.getByTestId('content'), ALL_ATTRIBUTES),
    vScrollbar: await attributeFlags(page.getByTestId('scrollbar-vertical'), Y_ATTRIBUTES),
    hScrollbar: await attributeFlags(page.getByTestId('scrollbar-horizontal'), X_ATTRIBUTES),
  };
}

/** Moves the real mouse onto a point of the viewport: Base UI's `fireEvent.pointerEnter(viewport)`, which marks user interaction. */
async function pointerIntoViewport(page: Page, offset: number): Promise<void> {
  const box = (await page.getByTestId('viewport').boundingBox())!;
  await page.mouse.move(box.x + 50 + offset, box.y + 50 + offset);
}

/** The inline value of a viewport overflow variable (Base UI reads `viewport.style.getPropertyValue`). */
function viewportVariable(page: Page, name: string): Promise<string> {
  return page.getByTestId('viewport').evaluate((element, property) => (element as HTMLElement).style.getPropertyValue(property), name);
}

test.describe('ScrollArea.Root', () => {
  test('port: smoke — the first measurement sizes the thumb and projects the overflow attributes', async ({ page, mode }) => {
    await gotoScrollArea(page, mode, 'scroll-area-basic');

    const root = page.getByTestId('root');
    await expect(root).toHaveAttribute('data-has-overflow-y', '');
    await expect(root).toHaveAttribute('data-overflow-y-end', '');
    await expect(page.getByTestId('viewport')).toHaveAttribute('tabindex', '0');
    const thumb = page.getByTestId('thumb');
    await expect.poll(() => computed(thumb, 'height')).toBe('40px');
    await expect.poll(() => computed(thumb, 'visibility')).toBe('visible');
  });

  // The family's first red L4 title (standing rules; the brief's "Tests first"). ScrollArea moves nothing optimistically
  // and C# echoes no module value back, so decision 6's revision field is not needed: the reports reach C# in order while
  // its renders are held, and the attributes end at the latest measurement.
  test('port: a held circuit does not leave the overflow attributes at a stale measurement', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Only the Server circuit can be held.');
    const hold = await holdCircuit(page, {
      armOn: (text) => text.includes('OnOverflow'),
      from: (text) => text.includes('RenderBatch'),
    });
    await gotoScrollArea(page, mode, 'scroll-area-basic');
    const root = page.getByTestId('root');
    const viewport = page.getByTestId('viewport');
    await viewport.evaluate((element) => {
      element.scrollTop = 400;
    });
    await expect(root).toHaveAttribute('data-overflow-y-start', '');
    await expect(page.getByTestId('scrollbar')).toBeVisible();

    hold.arm();
    // Back to the top, then the content shrinks below the viewport: two reports while the renders are held.
    await viewport.evaluate((element) => {
      element.scrollTop = 0;
    });
    await expect.poll(() => page.evaluate(() => document.querySelector('[data-testid=viewport]')!.scrollTop)).toBe(0);
    await page.getByTestId('content').locator('div').evaluate((element) => {
      (element as HTMLElement).style.height = '50px';
    });
    await expect.poll(() => viewport.evaluate((element) => getComputedStyle(element).getPropertyValue('--scroll-area-overflow-y-end'))).toBe('0px');
    await hold.release();

    await expect(root).not.toHaveAttribute('data-has-overflow-y');
    await expect(root).not.toHaveAttribute('data-overflow-y-start');
    await expect(root).not.toHaveAttribute('data-overflow-y-end');
    await expect(viewport).toHaveAttribute('tabindex', '-1');
    await expect(page.getByTestId('scrollbar')).toHaveCount(0);
  });

  // The attach-order rule (standing rules; ADR 04 Amendments 2026-10-29, the attach-overtaken rule): the parts' handles can
  // attach before the root's. The route serves the module through a wrapper whose `attach` holds the root's handle back
  // until its parts attached; the root's state is created by whichever handle comes first.
  test('port: parts whose handles attach before the root\'s are measured once the root attaches', async ({ page, mode }) => {
    // The served name may carry a fingerprint (`scroll-area.<hash>.js`); the wrapper imports the same URL with `?original`,
    // which passes through.
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
          const order = (window.scrollAreaAttachOrder ??= []);
          export function attach(element, optionsJson, ...rest) {
            const part = JSON.parse(optionsJson).part;
            if (part !== 'root') {
              order.push(part);
              return original.attach(element, optionsJson, ...rest);
            }
            return new Promise((resolve) => {
              const wait = () => {
                if (order.includes('viewport') && order.includes('content')) {
                  order.push('root');
                  resolve(original.attach(element, optionsJson, ...rest));
                } else {
                  setTimeout(wait, 10);
                }
              };
              wait();
            });
          }`,
      });
    });

    await gotoScrollArea(page, mode, 'scroll-area-basic');

    // The viewport's and the content's handles attach in either order (both from their own OnAfterRenderAsync); the root's
    // comes after both.
    const order = await page.evaluate(() => (window as unknown as { scrollAreaAttachOrder: string[] }).scrollAreaAttachOrder.slice(0, 3));
    expect([...order.slice(0, 2)].sort()).toEqual(['content', 'viewport']);
    expect(order[2]).toBe('root');
    await expect(page.getByTestId('root')).toHaveAttribute('data-has-overflow-y', '');
    await expect.poll(() => computed(page.getByTestId('thumb'), 'height')).toBe('40px');
  });

  test.describe('data-scrolling attribute', () => {
    // Fake timers: the page's clock is held from before the gesture, so the 500 ms timer runs only when the title runs it.
    // `fireEvent.pointerEnter` is a real mouse move onto the viewport; `fireEvent.scroll` is a programmatic scroll whose
    // `scroll` event the browser sends.
    test('adds [data-scrolling] attribute when viewport is scrolled', async ({ page, mode }) => {
      await gotoParts(page, mode, { bars: 'none', content: false });
      const root = page.getByTestId('root');
      await holdClock(page);
      try {
        await expect(root).not.toHaveAttribute('data-scrolling');

        await pointerIntoViewport(page, 0);
        await scrollViewport(page, { top: 1 });
        await expect(root).toHaveAttribute('data-scrolling', '');

        await page.clock.runFor(SCROLL_TIMEOUT);
        await expect(root).not.toHaveAttribute('data-scrolling');

        // Test horizontal scrolling
        await pointerIntoViewport(page, 10);
        await scrollViewport(page, { left: 1 });
        await expect(root).toHaveAttribute('data-scrolling', '');

        await page.clock.runFor(SCROLL_TIMEOUT);
        await expect(root).not.toHaveAttribute('data-scrolling');
      } finally {
        await page.clock.resume();
      }
    });
  });

  test.describe('sizing', () => {
    test('recomputes thumb size when becoming visible without requiring scroll', async ({ page, mode }) => {
      await gotoParts(page, mode, { hidden: true, bars: 'vertical', content: false, vStyle: 'display: flex', vThumbStyle: 'padding-block: 8px' });

      await page.getByTestId('show').click();

      const verticalThumb = page.getByTestId('thumb-vertical');
      await expect.poll(() => computed(verticalThumb, '--scroll-area-thumb-height')).not.toBe('0px');
      expect(await computed(verticalThumb, '--scroll-area-thumb-height')).not.toBe('');
    });

    test('shows scrollbars after mount compute before the first ResizeObserver measurement', async ({ page, mode }) => {
      await mockResizeObserver(page);
      await gotoParts(page, mode, { bars: 'vertical', content: false, vStyle: '' });

      const verticalScrollbar = page.getByTestId('scrollbar-vertical');
      await expect.poll(() => verticalScrollbar.evaluate((element) => getComputedStyle(element).visibility)).toBe('visible');

      expect(await notifyResizeObservers(page)).toBeGreaterThan(0);

      await expect.poll(() => verticalScrollbar.evaluate((element) => getComputedStyle(element).visibility)).toBe('visible');
    });

    test('shows keepMounted scrollbar track and thumb after mount compute', async ({ page, mode }) => {
      await mockResizeObserver(page);
      await gotoParts(page, mode, { bars: 'vertical', content: false, vStyle: '', keepMounted: true });

      const verticalScrollbar = page.getByTestId('scrollbar-vertical');
      const verticalThumb = page.getByTestId('thumb-vertical');
      const visibility = (locator: typeof verticalThumb) => locator.evaluate((element) => getComputedStyle(element).visibility);
      await expect.poll(() => visibility(verticalScrollbar)).toBe('visible');
      await expect.poll(() => visibility(verticalThumb)).toBe('visible');

      expect(await notifyResizeObservers(page)).toBeGreaterThan(0);

      await expect.poll(() => visibility(verticalScrollbar)).toBe('visible');
      await expect.poll(() => visibility(verticalThumb)).toBe('visible');
    });

    // `rerender` with the larger content is the fixture's resize button; the title waits for the new size to reach the DOM
    // before it notifies the observers, as `rerender` resolves after React's commit.
    test('recomputes corner size when content starts overflowing', async ({ page, mode }) => {
      await mockResizeObserver(page);
      const fixture = await gotoParts(page, mode, {
        content: false,
        width: VIEWPORT_SIZE / 2,
        height: VIEWPORT_SIZE / 2,
        resizeWidth: SCROLLABLE_CONTENT_SIZE,
        resizeHeight: SCROLLABLE_CONTENT_SIZE,
        vStyle: 'width: 11px',
        hStyle: 'height: 13px',
        corner: true,
      });

      expect(await notifyResizeObservers(page)).toBeGreaterThan(0);
      await settle(fixture);
      await expect(page.getByTestId('corner')).toHaveCount(0);

      await page.getByTestId('resize').click();
      await expect(page.getByTestId('inner')).toHaveCSS('width', `${SCROLLABLE_CONTENT_SIZE}px`);

      await notifyResizeObservers(page);

      const corner = page.getByTestId('corner');
      await expect.poll(() => corner.evaluate((element) => (element as HTMLElement).style.width)).toBe('11px');
      await expect.poll(() => corner.evaluate((element) => (element as HTMLElement).style.height)).toBe('13px');
    });

    test('clears corner, overflow attributes, and metrics when content stops overflowing', async ({ page, mode }) => {
      await mockResizeObserver(page);
      await gotoParts(page, mode, {
        content: false,
        keepMounted: true,
        corner: true,
        resizeWidth: VIEWPORT_SIZE / 2,
        resizeHeight: VIEWPORT_SIZE / 2,
      });
      const root = page.getByTestId('root');

      await expect(root).toHaveAttribute('data-has-overflow-x');
      await expect(root).toHaveAttribute('data-has-overflow-y');
      await expect(page.getByTestId('corner')).toBeAttached();
      expect(await viewportVariable(page, '--scroll-area-overflow-x-end')).not.toBe('0px');
      expect(await viewportVariable(page, '--scroll-area-overflow-y-end')).not.toBe('0px');

      await page.getByTestId('resize').click();
      await expect(page.getByTestId('inner')).toHaveCSS('width', `${VIEWPORT_SIZE / 2}px`);
      await notifyResizeObservers(page);

      await expect(root).not.toHaveAttribute('data-has-overflow-x');
      await expect(root).not.toHaveAttribute('data-has-overflow-y');
      await expect(page.getByTestId('corner')).toHaveCount(0);
      expect(await viewportVariable(page, '--scroll-area-overflow-x-start')).toBe('0px');
      expect(await viewportVariable(page, '--scroll-area-overflow-x-end')).toBe('0px');
      expect(await viewportVariable(page, '--scroll-area-overflow-y-start')).toBe('0px');
      expect(await viewportVariable(page, '--scroll-area-overflow-y-end')).toBe('0px');
    });

    test('should correctly set thumb height and width based on scrollable content', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, vStyle: '', hStyle: '' });

      const expectedThumbSize = `${(VIEWPORT_SIZE / SCROLLABLE_CONTENT_SIZE) * VIEWPORT_SIZE}px`;

      await expect.poll(() => computed(page.getByTestId('thumb-vertical'), '--scroll-area-thumb-height')).toBe(expectedThumbSize);
      await expect.poll(() => computed(page.getByTestId('thumb-horizontal'), '--scroll-area-thumb-width')).toBe(expectedThumbSize);
    });

    test('should not add padding for overlay scrollbars', async ({ page, mode }) => {
      await gotoParts(page, mode, {
        content: false,
        thumb: false,
        vStyle: 'width: 10px; height: 100%',
        hStyle: 'height: 10px; width: 100%',
      });

      const style = await page.getByTestId('viewport').evaluate((element) => {
        const computedStyle = getComputedStyle(element.firstElementChild!);
        return { left: computedStyle.paddingLeft, right: computedStyle.paddingRight, bottom: computedStyle.paddingBottom };
      });

      expect(style).toEqual({ left: '0px', right: '0px', bottom: '0px' });
    });

    test('accounts for scrollbar padding', async ({ page, mode }) => {
      const PADDING = 8;
      await gotoParts(page, mode, { content: false, vStyle: `padding-block: ${PADDING}px`, hStyle: `padding-inline: ${PADDING}px` });

      const expectedThumbSize = `${(VIEWPORT_SIZE - PADDING * 2) * (VIEWPORT_SIZE / SCROLLABLE_CONTENT_SIZE)}px`;

      await expect.poll(() => computed(page.getByTestId('thumb-vertical'), '--scroll-area-thumb-height')).toBe(expectedThumbSize);
      await expect.poll(() => computed(page.getByTestId('thumb-horizontal'), '--scroll-area-thumb-width')).toBe(expectedThumbSize);
    });

    test('accounts for scrollbar margin', async ({ page, mode }) => {
      const margin = 11;
      const viewportSize = 390;
      await gotoParts(page, mode, {
        content: false,
        rootWidth: viewportSize,
        rootHeight: viewportSize,
        vStyle: `margin-block: ${margin}px`,
        hStyle: `margin-inline: ${margin}px`,
      });

      const expectedThumbSize = `${(viewportSize - margin * 2) * (viewportSize / SCROLLABLE_CONTENT_SIZE)}px`;

      await expect.poll(() => computed(page.getByTestId('thumb-vertical'), '--scroll-area-thumb-height')).toBe(expectedThumbSize);
      await expect.poll(() => computed(page.getByTestId('thumb-horizontal'), '--scroll-area-thumb-width')).toBe(expectedThumbSize);
    });

    test('accounts for thumb margin', async ({ page, mode }) => {
      const MARGIN = 8;
      await gotoParts(page, mode, {
        content: false,
        vStyle: '',
        hStyle: '',
        vThumbStyle: `margin-block: ${MARGIN}px`,
        hThumbStyle: `margin-inline: ${MARGIN}px`,
      });

      const expectedThumbSize = `${(VIEWPORT_SIZE - MARGIN * 2) * (VIEWPORT_SIZE / SCROLLABLE_CONTENT_SIZE)}px`;

      await expect.poll(() => computed(page.getByTestId('thumb-vertical'), '--scroll-area-thumb-height')).toBe(expectedThumbSize);
      await expect.poll(() => computed(page.getByTestId('thumb-horizontal'), '--scroll-area-thumb-width')).toBe(expectedThumbSize);
    });
  });

  test.describe('overflow data attributes', () => {
    // `rerender(renderArea('rtl'))` is the fixture's direction toggle (the DirectionProvider and the root's `direction`).
    test('recomputes horizontal overflow edges when direction changes', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, bars: 'horizontal', width: SCROLLABLE_CONTENT_SIZE, height: VIEWPORT_SIZE });
      const root = page.getByTestId('root');

      await expect(root).toHaveAttribute('data-has-overflow-x');

      const { maxLeft: maxScrollLeft } = await viewportMetrics(page);
      await scrollViewport(page, { left: maxScrollLeft / 2 });

      const xEdges = ['data-overflow-x-start', 'data-overflow-x-end'];
      await expect.poll(() => attributeFlags(root, xEdges)).toEqual({ 'data-overflow-x-start': true, 'data-overflow-x-end': true });

      await page.getByTestId('toggle-dir').click();
      await expect(root).toHaveCSS('direction', 'rtl');

      await scrollViewport(page, { left: -maxScrollLeft });

      await expect.poll(() => attributeFlags(root, xEdges)).toEqual({ 'data-overflow-x-start': true, 'data-overflow-x-end': false });
    });

    test('measures content mounted after the viewport initial measurement', async ({ page, mode }) => {
      await gotoParts(page, mode, { bars: 'vertical', lateContent: true });
      const root = page.getByTestId('root');
      const viewport = page.getByTestId('viewport');

      // Empty viewport: no overflow and kept out of tab order.
      await expect(root).not.toHaveAttribute('data-has-overflow-y');
      await expect(viewport).toHaveAttribute('tabindex', '-1');

      await page.getByTestId('show-content').click();

      // Once oversized content mounts, overflow state and tab order must update.
      await expect(root).toHaveAttribute('data-has-overflow-x');
      await expect(root).toHaveAttribute('data-has-overflow-y');
      await expect(viewport).toHaveAttribute('tabindex', '0');
    });

    test('applies data attributes on root, viewport and scrollbars based on overflow and edges', async ({ page, mode }) => {
      await gotoParts(page, mode, {});
      const atStart = {
        'data-has-overflow-x': true,
        'data-has-overflow-y': true,
        'data-overflow-x-start': false,
        'data-overflow-x-end': true,
        'data-overflow-y-start': false,
        'data-overflow-y-end': true,
      };

      // Initial: at start (top/left)
      await expect.poll(() => partFlags(page)).toEqual({
        root: atStart,
        viewport: atStart,
        content: atStart,
        vScrollbar: { 'data-has-overflow-y': true, 'data-overflow-y-start': false, 'data-overflow-y-end': true },
        hScrollbar: { 'data-has-overflow-x': true, 'data-overflow-x-start': false, 'data-overflow-x-end': true },
      });

      // Scroll to middle
      const { maxTop, maxLeft } = await viewportMetrics(page);
      await scrollViewport(page, { top: maxTop / 2, left: maxLeft / 2 });
      const middle = {
        'data-has-overflow-x': true,
        'data-has-overflow-y': true,
        'data-overflow-x-start': true,
        'data-overflow-x-end': true,
        'data-overflow-y-start': true,
        'data-overflow-y-end': true,
      };
      await expect.poll(() => partFlags(page)).toEqual({
        root: middle,
        viewport: middle,
        content: middle,
        vScrollbar: { 'data-has-overflow-y': true, 'data-overflow-y-start': true, 'data-overflow-y-end': true },
        hScrollbar: { 'data-has-overflow-x': true, 'data-overflow-x-start': true, 'data-overflow-x-end': true },
      });

      // Scroll to end
      await scrollViewport(page, { top: maxTop, left: maxLeft });
      const atEnd = {
        'data-has-overflow-x': true,
        'data-has-overflow-y': true,
        'data-overflow-x-start': true,
        'data-overflow-x-end': false,
        'data-overflow-y-start': true,
        'data-overflow-y-end': false,
      };
      await expect.poll(() => partFlags(page)).toEqual({
        root: atEnd,
        viewport: atEnd,
        content: atEnd,
        vScrollbar: { 'data-has-overflow-y': true, 'data-overflow-y-start': true, 'data-overflow-y-end': false },
        hScrollbar: { 'data-has-overflow-x': true, 'data-overflow-x-start': true, 'data-overflow-x-end': false },
      });
    });

    test('treats near-edge scroll offsets as fully scrolled', async ({ page, mode }) => {
      await gotoParts(page, mode, {});
      const root = page.getByTestId('root');
      const { maxTop, maxLeft } = await viewportMetrics(page);

      await scrollViewport(page, { top: maxTop - 0.5, left: maxLeft - 0.5 });

      await expect(root).toHaveAttribute('data-overflow-y-start');
      await expect(root).toHaveAttribute('data-overflow-x-start');
      await expect(root).not.toHaveAttribute('data-overflow-y-end');
      await expect(root).not.toHaveAttribute('data-overflow-x-end');
    });

    test('respects overflowEdgeThreshold and exposes scroll metrics', async ({ page, mode }) => {
      const fixture = await gotoParts(page, mode, { threshold: 'xStart:20,yStart:5' });
      const viewport = page.getByTestId('viewport');

      await scrollViewport(page, { left: 15, top: 7 });

      await expect(viewport).toHaveAttribute('data-overflow-y-start');
      await settle(fixture);
      await expect(viewport).not.toHaveAttribute('data-overflow-x-start');

      await scrollViewport(page, { left: 35, top: 7 });

      await expect(viewport).toHaveAttribute('data-overflow-x-start');

      expect(await viewportVariable(page, '--scroll-area-overflow-x-start')).toBe('35px');
      const horizontalEndPx = await viewportVariable(page, '--scroll-area-overflow-x-end');
      expect(horizontalEndPx).not.toBe('');
      expect(horizontalEndPx).not.toBe('0px');
    });

    // `fireEvent.scroll` asserts synchronously; here the module writes the viewport's variables in the same measurement as
    // the report, so the title waits for the variable, then for the sync barrier, before it reads the attributes.
    test('applies numeric overflowEdgeThreshold to every edge', async ({ page, mode }) => {
      const fixture = await gotoParts(page, mode, { threshold: 20 });
      const viewport = page.getByTestId('viewport');

      await expect(viewport).toHaveAttribute('data-has-overflow-x');

      await scrollViewport(page, { left: 15, top: 15 });
      await expect.poll(() => viewportVariable(page, '--scroll-area-overflow-y-start')).toBe('15px');
      await settle(fixture);

      await expect(viewport).not.toHaveAttribute('data-overflow-x-start');
      await expect(viewport).not.toHaveAttribute('data-overflow-y-start');
      await expect(viewport).toHaveAttribute('data-overflow-x-end');
      await expect(viewport).toHaveAttribute('data-overflow-y-end');

      const { maxTop, maxLeft } = await viewportMetrics(page);
      await scrollViewport(page, { left: maxLeft - 15, top: maxTop - 15 });
      await expect.poll(() => viewportVariable(page, '--scroll-area-overflow-y-end')).toBe('15px');
      await settle(fixture);

      await expect(viewport).toHaveAttribute('data-overflow-x-start');
      await expect(viewport).toHaveAttribute('data-overflow-y-start');
      await expect(viewport).not.toHaveAttribute('data-overflow-x-end');
      await expect(viewport).not.toHaveAttribute('data-overflow-y-end');
    });

    // `rerender(renderArea(20))` is the fixture's threshold button (`thresholdAfter`).
    test('recomputes overflow edges when overflowEdgeThreshold changes', async ({ page, mode }) => {
      await gotoParts(page, mode, { bars: 'vertical', threshold: 'yStart:5', thresholdAfter: 'yStart:20' });
      const viewport = page.getByTestId('viewport');

      await scrollViewport(page, { top: 10 });
      await expect(viewport).toHaveAttribute('data-overflow-y-start');

      // Raising the threshold above the current offset must clear the edge without a new scroll.
      await page.getByTestId('set-threshold').click();
      await expect(viewport).not.toHaveAttribute('data-overflow-y-start');
    });

    test('does not add state attributes when content does not overflow', async ({ page, mode }) => {
      const fixture = await gotoParts(page, mode, { width: VIEWPORT_SIZE / 2, height: VIEWPORT_SIZE / 2, keepMounted: true });
      await settle(fixture);

      const none = (names: readonly string[]) => Object.fromEntries(names.map((name) => [name, false]));
      expect(await attributeFlags(page.getByTestId('root'), ALL_ATTRIBUTES)).toEqual(none(ALL_ATTRIBUTES));
      expect(await attributeFlags(page.getByTestId('viewport'), ALL_ATTRIBUTES.slice(2))).toEqual(none(ALL_ATTRIBUTES.slice(2)));
      expect(await attributeFlags(page.getByTestId('content'), ALL_ATTRIBUTES.slice(2))).toEqual(none(ALL_ATTRIBUTES.slice(2)));
      expect(await attributeFlags(page.getByTestId('scrollbar-vertical'), Y_ATTRIBUTES.slice(1))).toEqual(none(Y_ATTRIBUTES.slice(1)));
      expect(await attributeFlags(page.getByTestId('scrollbar-horizontal'), X_ATTRIBUTES.slice(1))).toEqual(none(X_ATTRIBUTES.slice(1)));
    });

    test('correctly handles RTL', async ({ page, mode }) => {
      await gotoParts(page, mode, { dir: 'rtl', content: false, bars: 'horizontal', width: SCROLLABLE_CONTENT_SIZE, height: 200 });
      const root = page.getByTestId('root');
      const { maxLeft: maxScrollLeft } = await viewportMetrics(page);

      await scrollViewport(page, { left: 0 });

      await expect(root).toHaveAttribute('data-has-overflow-x');
      await expect(root).not.toHaveAttribute('data-overflow-x-start');
      await expect(root).toHaveAttribute('data-overflow-x-end');

      await scrollViewport(page, { left: -maxScrollLeft / 2 });

      await expect(root).toHaveAttribute('data-overflow-x-start');
      await expect(root).toHaveAttribute('data-overflow-x-end');

      await scrollViewport(page, { left: -maxScrollLeft });

      await expect(root).toHaveAttribute('data-overflow-x-start');
      await expect(root).not.toHaveAttribute('data-overflow-x-end');
    });
  });

  test.describe('context stability', () => {
    // React's committed-render count becomes C#'s: `data-reports` counts every change of the root's shared state C# applied,
    // which is what re-renders the parts. A `scroll` that moves nothing cannot come from real input, so the three are
    // dispatched in the page, as Base UI's `fireEvent.scroll` does; the sync barrier then proves C# had the chance to apply
    // anything they reported.
    test('does not re-render parts on scroll when the corner size is unchanged', async ({ page, mode }) => {
      const fixture = await gotoParts(page, mode, { content: false, vStyle: 'width: 10px', hStyle: 'height: 10px', corner: true });

      // Wait until both scrollbars are visible and the corner has been measured,
      // which is the precondition for the corner-size setter to run on scroll.
      await expect.poll(() => page.getByTestId('corner').evaluate((element) => (element as HTMLElement).style.width)).toBe('10px');
      await settle(fixture);

      const countBeforeScroll = await fixture.getAttribute('data-reports');

      await page.getByTestId('viewport').evaluate((element) => {
        for (let i = 0; i < 3; i += 1) {
          element.scrollTop = 0;
          element.scrollLeft = 0;
          element.dispatchEvent(new Event('scroll'));
        }
      });
      await settle(fixture);

      expect(await fixture.getAttribute('data-reports')).toBe(countBeforeScroll);
    });
  });
});
