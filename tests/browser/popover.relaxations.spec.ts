import { test, expect, delayCircuit, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// ADR 01's row of 2026-11-24 (issue 268, design point 3 (a) of M6 track 10, chosen by the user 2026-11-24): outside press
// arms only once the popup's floating handle attaches, so on Server no outside dismissal happens between the open's render
// and that attach. Measured on `popover-root` (a non-modal popover) at 0, 50 and 150 ms of one-way delay added to the
// Server circuit (`delayCircuit`, before the first navigation), and once on WebAssembly. The witnesses are the page's: a
// `MutationObserver` for the open popup in the document, and the document's first passive capture `touchstart` listener
// added after it (the outside-press arming adds it with the others), recorded by an init script. The figure is the page's
// `performance.now()` between them, logged as a `figure` annotation; the assertion is only the floor the added delay puts
// under the two legs (the render's acknowledgement and the attach), not the runner's speed (issue 114's rule).

const SERVER_DELAYS = [0, 50, 150] as const;

/** The least time `count` delayed legs take as the page sees it (the route's timers may fire a millisecond early). */
const legsFloor = (delay: number, count: number): number => (delay === 0 ? 0 : count * delay - 2 * count);

function perDelay(title: string, body: (args: { page: Page; mode: RenderMode; delay: number }) => Promise<void>): void {
  for (const delay of SERVER_DELAYS) {
    test(`${title} (${delay} ms added on Server)`, async ({ page, mode }) => {
      test.skip(mode !== 'server' && delay !== 0, 'WebAssembly has no circuit to delay; its title runs once.');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }
      await body({ page, mode, delay });
    });
  }
}

test.describe('Popover: ADR 01 relaxation rows', () => {
  perDelay('outside press arms once the floating handle attaches after the open', async ({ page, mode, delay }) => {
    await page.addInitScript(() => {
      const times: number[] = [];
      (window as unknown as { __captureArms: number[] }).__captureArms = times;
      const original = Document.prototype.addEventListener;
      Document.prototype.addEventListener = function (this: Document, type: string, listener: EventListenerOrEventListenerObject | null, options?: boolean | AddEventListenerOptions) {
        // The outside-press arming adds its capture listeners together, `touchstart` among them as passive (the bundle is
        // minified, so the listener's name cannot tell it apart).
        if (type === 'touchstart' && typeof options === 'object' && options?.capture === true && options.passive === true) {
          times.push(performance.now());
        }
        return original.call(this, type, listener as EventListenerOrEventListenerObject, options);
      } as typeof Document.prototype.addEventListener;
    });
    await gotoPopupFixture(page, mode, 'popover-root', {});

    const rendered = page.evaluate(() => new Promise<number>((resolve) => {
      const observer = new MutationObserver(() => {
        if (document.querySelector('[data-testid="popover-popup"]') !== null) {
          observer.disconnect();
          resolve(performance.now());
        }
      });
      observer.observe(document.body, { childList: true, subtree: true });
    }));
    await page.getByTestId('trigger').click();
    const renderedAt = await rendered;
    const armedAt = await page.waitForFunction((after) => {
      const times = (window as unknown as { __captureArms: number[] }).__captureArms;
      return times.find((time) => time >= after) ?? null;
    }, renderedAt).then((handle) => handle.jsonValue() as Promise<number>);

    const ms = Math.round(armedAt - renderedAt);
    test.info().annotations.push({ type: 'figure', description: `outside-press arming ${mode} +${delay} ${JSON.stringify({ ms })}` });
    expect(ms).toBeGreaterThanOrEqual(mode === 'server' ? legsFloor(delay, 2) : 0);
  });
});
