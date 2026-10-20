import { test, expect, gotoFixture } from './helpers';

// The browser-only Base UI tests of the `dom` module's measurement helpers (UTL-DOM-23 and UTL-DOM-25; skipped in
// jsdom at L3). The page imports the module the library ships and runs the helper on real layout. The render mode
// plays no part; the probe fixture only provides a page that serves the library's static assets.
const domModule = '/_content/Blazix.Primitives/js/dom.js';

test.describe('getCssDimensions', () => {
  // Parts that accept a `render` prop can be SVG elements, which have no `offsetWidth`/`offsetHeight`.
  test('reads the CSS size of an SVG element', async ({ page, mode }) => {
    await gotoFixture(page, mode, 'probe');

    const size = await page.evaluate(async (path) => {
      const { getCssDimensions } = await import(path);
      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('width', '80');
      svg.setAttribute('height', '24');
      document.body.appendChild(svg);
      try {
        return getCssDimensions(svg);
      } finally {
        svg.remove();
      }
    }, domModule);

    expect(size).toEqual({ width: 80, height: 24 });
  });
});

test.describe('getPseudoElementBounds', () => {
  test('includes pseudo-element bounds in browsers', async ({ page, mode }) => {
    await gotoFixture(page, mode, 'probe');

    const bounds = await page.evaluate(async (path) => {
      const { getPseudoElementBounds } = await import(path);
      const style = document.createElement('style');
      style.textContent = `
        .pseudo-element-bounds-test {
          position: absolute;
          left: 100px;
          top: 50px;
          width: 20px;
          height: 10px;
        }

        .pseudo-element-bounds-test::before {
          content: "";
          display: block;
          width: 40px;
          height: 30px;
        }
      `;
      const element = document.createElement('div');
      element.className = 'pseudo-element-bounds-test';
      document.head.appendChild(style);
      document.body.appendChild(element);
      try {
        const { left, right, top, bottom } = getPseudoElementBounds(element);
        return { left, right, top, bottom };
      } finally {
        element.remove();
        style.remove();
      }
    }, domModule);

    expect(bounds).toEqual({ left: 90, right: 130, top: 40, bottom: 70 });
  });
});
