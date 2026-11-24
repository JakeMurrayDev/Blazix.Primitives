import { test, expect, delayCircuit, type Page, type RenderMode } from './helpers';
import { gotoCombobox } from './combobox.helpers';

// ADR 01's row of 2026-11-24 (issue 247, design point 10c-4 (b) of M6 track 10, chosen by the user 2026-11-24): a Tab
// that leaves the input before a typed reopen's focus-manager handle attaches is noted by the combobox input's module and
// replayed when the handle attaches, so the close lands later than a Tab after the attach would. Measured on
// `autocomplete-root` at 0, 50 and 150 ms of one-way delay added to the Server circuit (`delayCircuit`, before the first
// navigation), and once on WebAssembly. The Tab is pressed in the page as soon as the reopened list enters the document;
// the figure is the page's `performance.now()` from that Tab to a `MutationObserver` seeing the list leave, logged as a
// `figure` annotation. The assertion is only the floor the added delay puts under the legs (issue 114's rule).

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

test.describe('Autocomplete: ADR 01 relaxation rows', () => {
  perDelay('a Tab right after a typed reopen closes the list once the focus manager attaches', async ({ page, mode, delay }) => {
    await gotoCombobox(page, mode, 'autocomplete-root', {});
    const input = page.getByTestId('input');
    await input.click();
    await input.pressSequentially('a');
    await expect(page.getByRole('option')).toHaveCount(2);
    await input.press('ArrowDown');
    await expect(page.getByTestId('item-apple')).toHaveAttribute('data-highlighted', '');
    await input.press('Enter');
    await expect(page.getByRole('listbox')).toHaveCount(0);

    const timed = page.evaluate(() => new Promise<number>((resolve) => {
      let tabAt = -1;
      const observer = new MutationObserver(() => {
        const list = document.querySelector('[role="listbox"]');
        if (tabAt < 0 && list !== null) {
          tabAt = performance.now();
          // The Tab's focus move: the next tabbable after the input takes focus.
          const tabbables = [...document.querySelectorAll<HTMLElement>('input, button, [tabindex="0"]')].filter((element) => !element.hasAttribute('disabled'));
          const input = document.querySelector<HTMLElement>('[data-testid="input"]')!;
          tabbables[tabbables.indexOf(input) + 1]?.focus();
        } else if (tabAt >= 0 && list === null) {
          observer.disconnect();
          resolve(performance.now() - tabAt);
        }
      });
      observer.observe(document.body, { childList: true, subtree: true });
    }));
    await input.press('Backspace');
    const ms = Math.round(await timed);
    test.info().annotations.push({ type: 'figure', description: `typed-reopen tab close ${mode} +${delay} ${JSON.stringify({ ms })}` });
    expect(ms).toBeGreaterThanOrEqual(mode === 'server' ? legsFloor(delay, 2) : 0);
  });
});
