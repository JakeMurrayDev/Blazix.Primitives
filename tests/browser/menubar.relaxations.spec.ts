import { test, expect, delayCircuit } from './helpers';
import { center, parkPointer } from './menu.helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// ADR 01's row of 2026-11-25 (issue 367; design point 10-363a (1) of M6 track 10, chosen by the user 2026-11-25): a
// click-opened bar menu is visible, and cut out of its backdrop at insertion, before its popup takes its initial focus;
// in that window the trigger keeps the focus, and a blur of it closes the menu with `trigger-focus`. Measured on
// `menubar-root` at 0, 50 and 150 ms of one-way delay added to the Server circuit (`delayCircuit`, before the first
// navigation), and once on WebAssembly: the figure is the page's `performance.now()` from a `MutationObserver` seeing
// File's positioner enter the document to the popup's `focusin`, logged as a `figure` annotation. A coverage title (the
// base has the same window); the assertion is only the floor the added delay puts under the legs (issue 114's rule).
//
// Issue 367 (design point 10-367a (1), chosen by the user 2026-11-27; ADR 01 Amendments 2026-11-27): the trigger's blur
// close in that window is held until the focus manager arms, so a press on the backdrop as soon as it covers the page
// closes the menu with `outside-press`, at the arm. The second figure is the page's `performance.now()` from that press's
// `pointerdown` to a `MutationObserver` seeing File's positioner leave the document, with whether the press came before
// the popup's `focusin` and the close's recorded reason. A coverage title too (the base closes the menu as well, with
// `trigger-focus`); the reason is proven by the `menubar.spec.ts` titles.

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

  for (const delay of SERVER_DELAYS) {
    test(`a backdrop press before a click-opened bar menu's popup takes its initial focus closes the menu (${delay} ms added on Server)`, { tag: '@coverage' }, async ({ page, mode }) => {
      test.skip(mode !== 'server' && delay !== 0, 'WebAssembly has no circuit to delay; its title runs once.');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }
      await parkPointer(page);
      await page.addInitScript(() => {
        const marks = window as unknown as { pressedAt?: number; pressedBeforeFocus?: boolean; closedAt?: number; focused?: boolean };
        let shown = false;
        new MutationObserver(() => {
          const present = document.querySelector('[data-testid="file-menu"]') !== null;
          if (present) {
            shown = true;
          } else if (shown && marks.pressedAt !== undefined && marks.closedAt === undefined) {
            marks.closedAt = performance.now();
          }
        }).observe(document, { subtree: true, childList: true });
        document.addEventListener('focusin', (event) => {
          const target = event.target as Element;
          if (target.getAttribute('role') === 'menu' && target.closest('[data-testid="file-menu"]') !== null) {
            marks.focused = true;
          }
        }, true);
        document.addEventListener('pointerdown', (event) => {
          if (shown && marks.pressedAt === undefined && !(event.target as Element).closest('[data-testid="file-trigger"]')) {
            marks.pressedAt = performance.now();
            marks.pressedBeforeFocus = marks.focused !== true;
          }
        }, true);
      });
      const fixture = await gotoPopupFixture(page, mode, 'menubar-root');
      const before = await center(page.getByTestId('before'));
      await page.getByTestId('file-trigger').click();
      // The press lands on the backdrop as soon as it covers the page; a press before that reaches the page and is ADR 01's
      // row of 2026-11-24 (no outside dismissal before the floating attach).
      const hit = () => page.evaluate(({ x, y }) => document.elementFromPoint(x, y)?.getAttribute('role') ?? '', before);
      await expect.poll(hit, { intervals: [5] }).toBe('presentation');
      await page.mouse.click(before.x, before.y);
      await expect(page.getByTestId('file-menu')).toHaveCount(0, { timeout: 10_000 });

      const marks = await page.evaluate(() => {
        const recorded = window as unknown as { pressedAt: number; pressedBeforeFocus: boolean; closedAt: number };
        return { ms: Math.round(recorded.closedAt - recorded.pressedAt), beforeFocus: recorded.pressedBeforeFocus };
      });
      const reason = ((await fixture.getAttribute('data-file-changes')) ?? '').split(' ').at(-1);
      const figure = { ...marks, reason };
      test.info().annotations.push({ type: 'figure', description: `bar menu backdrop press ${mode} +${delay} ${JSON.stringify(figure)}` });
      expect(figure.ms).toBeGreaterThanOrEqual(legsFloor(mode === 'server' ? delay : 0, 2));
    });
  }
});
