import { test, expect, delayCircuit } from './helpers';
import { parkPointer } from './menu.helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// ADR 01's row of 2026-11-25 (issue 367; design point 10-363a (1) of M6 track 10, chosen by the user 2026-11-25): a
// click-opened bar menu is visible, and cut out of its backdrop at insertion, before its popup takes its initial focus;
// in that window the trigger keeps the focus, and a blur of it closes the menu with `trigger-focus`. Measured on
// `menubar-root` at 0, 50 and 150 ms of one-way delay added to the Server circuit (`delayCircuit`, before the first
// navigation), and once on WebAssembly: the figure is the page's `performance.now()` from a `MutationObserver` seeing
// File's positioner enter the document to the popup's `focusin`, logged as a `figure` annotation. A coverage title (the
// base has the same window); the assertion is only the floor the added delay puts under the legs (issue 114's rule).

const SERVER_DELAYS = [0, 50, 150] as const;

/** The least time `count` delayed legs take as the page sees it (the route's timers may fire a millisecond early). */
const legsFloor = (delay: number, count: number): number => (delay === 0 ? 0 : count * delay - 2 * count);

test.describe('Menubar: ADR 01 relaxation rows', () => {
  for (const delay of SERVER_DELAYS) {
    test(`a click-opened bar menu's popup takes its initial focus after the menu shows (${delay} ms added on Server)`, { tag: '@coverage' }, async ({ page, mode }) => {
      test.skip(mode !== 'server' && delay !== 0, 'WebAssembly has no circuit to delay; its title runs once.');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }
      await parkPointer(page);
      await page.addInitScript(() => {
        const marks = window as unknown as { menubarShownAt?: number; menubarFocusedAt?: number };
        new MutationObserver(() => {
          if (marks.menubarShownAt === undefined && document.querySelector('[data-testid="file-menu"]') !== null) {
            marks.menubarShownAt = performance.now();
          }
        }).observe(document, { subtree: true, childList: true });
        document.addEventListener('focusin', (event) => {
          const target = event.target as Element;
          if (marks.menubarFocusedAt === undefined && target.getAttribute('role') === 'menu' && target.closest('[data-testid="file-menu"]') !== null) {
            marks.menubarFocusedAt = performance.now();
          }
        }, true);
      });
      await gotoPopupFixture(page, mode, 'menubar-root');
      await page.getByTestId('file-trigger').click();
      await expect(page.getByTestId('file-menu').getByRole('menu')).toBeFocused({ timeout: 10_000 });

      const ms = await page.evaluate(() => {
        const marks = window as unknown as { menubarShownAt: number; menubarFocusedAt: number };
        return Math.round(marks.menubarFocusedAt - marks.menubarShownAt);
      });
      test.info().annotations.push({ type: 'figure', description: `bar menu initial focus ${mode} +${delay} ${JSON.stringify({ ms })}` });
      expect(ms).toBeGreaterThanOrEqual(legsFloor(mode === 'server' ? delay : 0, 4));
    });
  }
});
