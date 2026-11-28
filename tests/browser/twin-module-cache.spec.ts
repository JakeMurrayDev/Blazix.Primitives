import { expect, test, type Page } from './helpers';
import { gotoPopupFixture, settle } from './suites/popupFixture';

// Issue 179 (design point 10-179a (a), chosen by the user 2026-11-28): a module is imported once per runtime, so a part
// created after its module was first imported in the circuit attaches without the import's round trip. The case is ADR 01's
// NAV-PTR-02/NAV-PTR-18/NAV-ANI-04 row: a NavigationMenu hover switch mounts the new Content, whose own `portal` handle
// moves it into the Viewport from `OnAfterRender` before the held size pass can run; that handle's module, and every other
// module the Content's parts use, was already imported by the first Content. The title counts the `import` calls the circuit
// carries (each names its module's URL, the call's only argument) and records the switch's timings at 250 ms each way, the
// row's figure. Server only: WebAssembly has no circuit to count on (L2 `TwinModuleCacheTests` covers the runtime cache).

const ONE_WAY = 250;
const OPEN_DELAY = 50;
const MODULE = /_content\/Iodine\.Headless\/js\/([a-z-]+)(?:\.[0-9a-z]+)?\.js/g;

/** Delays the circuit by `ONE_WAY` each way and records every module the server asks the page to import, in order. */
async function recordImports(page: Page): Promise<string[]> {
  const imports: string[] = [];
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), ONE_WAY);
    });
    server.onMessage((message) => {
      const text = typeof message === 'string' ? message : message.toString('latin1');
      for (const match of text.matchAll(MODULE)) {
        imports.push(match[1]!);
      }
      setTimeout(() => socket.send(message), ONE_WAY);
    });
  });
  return imports;
}

async function hoverElement(page: Page, testId: string): Promise<void> {
  const box = (await page.getByTestId(testId).boundingBox())!;
  await page.mouse.move(box.x + 4, box.y + 4);
  await page.mouse.move(box.x + 10, box.y + 8);
}

test('port: a Content the hover switch mounts imports no module the circuit already imported, so the new size lands a round trip sooner on Server', async ({ page, mode }) => {
  test.skip(mode !== 'server', 'counts the import calls the circuit carries; WebAssembly has no circuit');
  const imports = await recordImports(page);
  const fixture = await gotoPopupFixture(page, mode, 'navigation-menu-basic', { sized: true });
  const positioner = page.getByTestId('positioner');
  const width = () => positioner.evaluate((element: HTMLElement) => parseFloat(element.style.getPropertyValue('--positioner-width')));

  await hoverElement(page, 'trigger-1');
  await page.clock.runFor(OPEN_DELAY + 10);
  await expect(page.getByTestId('content-1')).toBeVisible();
  await expect.poll(width).toBeCloseTo(400, 0);
  await settle(fixture);
  const before = new Set(imports);
  const countBefore = imports.length;

  await page.evaluate(() => {
    const record = { hover: -1, content: -1, size: -1 };
    (window as unknown as { switchFigure: typeof record }).switchFigure = record;
    const trigger = document.querySelector('[data-testid="trigger-2"]')!;
    const positionerElement = document.querySelector('[data-testid="positioner"]') as HTMLElement;
    trigger.addEventListener('pointermove', () => {
      if (record.hover < 0) {
        record.hover = performance.now();
      }
    }, true);
    new MutationObserver(() => {
      if (record.hover >= 0 && record.content < 0 && trigger.getAttribute('aria-expanded') === 'true') {
        record.content = performance.now();
      }
    }).observe(trigger, { attributes: true, attributeFilter: ['aria-expanded'] });
    new MutationObserver(() => {
      if (record.hover >= 0 && record.size < 0 && parseFloat(positionerElement.style.getPropertyValue('--positioner-width')) === 300) {
        record.size = performance.now();
      }
    }).observe(positionerElement, { attributes: true, attributeFilter: ['style'] });
  });
  await hoverElement(page, 'positioner');
  await hoverElement(page, 'trigger-2');
  await expect(page.getByTestId('content-2')).toBeVisible();
  await expect.poll(width).toBeCloseTo(300, 0);
  await settle(fixture);

  const record = await page.evaluate(() => (window as unknown as { switchFigure: { hover: number; content: number; size: number } }).switchFigure);
  const figure = `Server at ${ONE_WAY} ms each way: aria-expanded ${Math.round(record.content - record.hover)} ms, the new size ${Math.round(record.size - record.hover)} ms after the hover; imports ${countBefore} before the switch, ${imports.length - countBefore} during it`;
  test.info().annotations.push({ type: 'figure', description: figure });
  console.log(`[figure] ${figure}`);

  const switchImports = imports.slice(countBefore);
  expect(switchImports.filter((module) => before.has(module))).toEqual([]);
});
