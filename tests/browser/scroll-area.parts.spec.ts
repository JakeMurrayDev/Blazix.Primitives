// L4 (ADR 06 §2): Base UI's ScrollAreaCorner.test.tsx and ScrollAreaContent.test.tsx on both render modes
// (docs/components/scroll-area.md §5.4, §5.7, §13). Titles verbatim. Their L2-only titles (aria-hidden on the Corner, the
// error outside a Viewport, the ref-less Content renderer) are in the component tests.
import { expect, test } from './helpers';
import { computed, gotoParts } from './scroll-area.helpers';

test.describe('ScrollArea.Corner', () => {
  test.describe('interactions', () => {
    test('should apply correct corner size when both scrollbars are present', async ({ page, mode }) => {
      await gotoParts(page, mode, { content: false, vStyle: 'width: 10px', hStyle: 'height: 10px', thumb: false, corner: true });
      const corner = page.getByTestId('corner');

      await expect
        .poll(async () => ({
          width: await computed(corner, '--scroll-area-corner-width'),
          height: await computed(corner, '--scroll-area-corner-height'),
        }))
        .toEqual({ width: '10px', height: '10px' });
    });
  });
});

test.describe('ScrollArea.Content', () => {
  // `rerender(renderArea(1000))` is the fixture's resize button; the content's own box grows with the element it wraps.
  test('recomputes overflow when observed content resizes', async ({ page, mode }) => {
    await gotoParts(page, mode, { rootWidth: 100, rootHeight: 100, bars: 'none', width: 50, height: 50, resizeHeight: 1000 });
    const root = page.getByTestId('root');

    await expect(root).not.toHaveAttribute('data-has-overflow-y');

    await page.getByTestId('resize').click();

    await expect(root).toHaveAttribute('data-has-overflow-y');
  });
});
