import { test, type Page } from './helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// The list part measurement of ADR 14 (Amendments 2026-11-03; issues 5, 28, 25) in a browser, on `menu-measure`: the time of one
// render of a menu's items, by count and form, on both modes. The median of 25 renders, each from the click on the fixture's
// Rerender button to the batch that changes its `data-tick`. The Server figure includes the circuit's round trip to a local
// host. C#'s per-item figures in Release are `MenuListMeasureTests`'s at L2; ADR 16 records both.

async function medianRenderMs(page: Page): Promise<number> {
  return page.evaluate(async () => {
    const host = document.querySelector('[data-fixture="menu-measure"]')!;
    const button = host.querySelector<HTMLButtonElement>('[data-testid="rerender"]')!;
    const times: number[] = [];
    for (let run = 0; run < 25; run += 1) {
      const before = host.getAttribute('data-tick');
      const start = performance.now();
      await new Promise<void>((resolve) => {
        const observer = new MutationObserver(() => {
          if (host.getAttribute('data-tick') !== before) {
            observer.disconnect();
            resolve();
          }
        });
        observer.observe(host, { attributes: true, attributeFilter: ['data-tick'] });
        button.click();
      });
      times.push(performance.now() - start);
    }
    times.sort((a, b) => a - b);
    return times[Math.floor(times.length / 2)]!;
  });
}

test.describe('list part measurement', () => {
  for (const count of [100, 500]) {
    for (const form of ['plain', 'substitute']) {
      test(`port: the time of one render of a menu of ${count} items, ${form}`, async ({ page, mode }) => {
        await gotoPopupFixture(page, mode, 'menu-measure', { count, form });
        const ms = await medianRenderMs(page);
        const figure = `${count} ${form}: ${ms.toFixed(1)} ms, ${((ms * 1000) / count).toFixed(1)} us per item`;
        test.info().annotations.push({ type: 'figure', description: figure });
        console.log(`[figure] ${test.info().project.name} ${figure}`);
      });
    }
  }
});
