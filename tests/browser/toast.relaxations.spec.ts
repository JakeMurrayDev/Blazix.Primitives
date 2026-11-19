import { test, expect, delayCircuit, type Page, type RenderMode } from './helpers';
import { addToast, centreOf, mouseSwipe, openToast, type Query } from './toast.helpers';

// TST §9's Server relaxations (docs/components/toast.md) against ADR 01's relaxation table (issue 280, Deliverable 9; the M6
// plan's decision 8 (a), chosen by the user 2026-11-18): each candidate row measured at 0, 50 and 150 ms of one-way delay
// added to the Server circuit (`delayCircuit`, before the first navigation; 0 ms is the bare localhost circuit), and once on
// WebAssembly. Each title logs its figure (ms between two markers, the page's `performance.now()`) as a `figure` annotation.
// The witness is in the page: a capture listener stamps the input, and a `MutationObserver` reads the state in a microtask
// queued from its callback (the recorder rule of tests/browser/CONTEXT.md). The assertions do not depend on the runner's
// speed (issue 114's rule): on Server they assert the floor the added delay puts under the legs a row crosses.

const SERVER_DELAYS = [0, 50, 150] as const;

/** The least time `legs` delayed one-way legs take as the page sees it (the route's timers may fire a millisecond early). */
const legsFloor = (delay: number, legs: number): number => (delay === 0 ? 0 : legs * delay - 2 * legs);

function perDelay(title: string, body: (args: { page: Page; mode: RenderMode; delay: number }) => Promise<void>): void {
  for (const delay of SERVER_DELAYS) {
    test(`${title} (${delay} ms added on Server)`, async ({ page, mode }) => {
      test.skip(mode === 'wasm' && delay !== 0, 'WebAssembly has no circuit to delay; its title runs once.');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }
      await body({ page, mode, delay });
    });
  }
}

/** What ends the measurement: the first state in which the element matches. */
interface Until {
  selector: string;
  attribute?: string;
  value?: string;
  absent?: boolean;
  gone?: boolean;
  style?: string;
  changed?: boolean;
}

/** What starts it: the first event of a type (in the capture phase on the window), or the first mutation inside an element. */
type From = { event: string; within?: string } | { mutationIn: string };

/** Arms the page's witness; `measured()` then resolves to the ms between the two markers. */
async function arm(page: Page, from: From, until: Until): Promise<() => Promise<number>> {
  await page.evaluate(
    ([start, end]) => {
      const w = window as unknown as { __in?: number; __out?: number };
      delete w.__in;
      delete w.__out;
      const initialStyle = end.style ? (document.querySelector(end.selector) as HTMLElement | null)?.style.getPropertyValue(end.style) : undefined;
      const holds = (): boolean => {
        const element = document.querySelector(end.selector) as HTMLElement | null;
        if (end.gone) {
          return element === null;
        }
        if (!element) {
          return false;
        }
        if (end.style) {
          const value = element.style.getPropertyValue(end.style);
          return end.changed ? value !== initialStyle && value !== '' : value === end.value;
        }
        if (end.attribute) {
          return end.absent ? !element.hasAttribute(end.attribute) : end.value === undefined ? element.hasAttribute(end.attribute) : element.getAttribute(end.attribute) === end.value;
        }
        return true;
      };
      const observer = new MutationObserver((records) => {
        queueMicrotask(() => {
          if ('mutationIn' in start && w.__in === undefined && records.some((record) => (record.target as Element).closest?.(start.mutationIn) ?? record.target.parentElement?.closest(start.mutationIn))) {
            w.__in = performance.now();
          }
          if (w.__in !== undefined && w.__out === undefined && holds()) {
            w.__out = performance.now();
            observer.disconnect();
          }
        });
      });
      observer.observe(document.body, { attributes: true, childList: true, characterData: true, subtree: true });
      if ('event' in start) {
        const listener = (event: Event) => {
          if (start.within && !(event.target instanceof Element && event.target.closest(start.within))) {
            return;
          }
          if (w.__in === undefined) {
            w.__in = performance.now();
          }
          window.removeEventListener(start.event, listener, true);
        };
        window.addEventListener(start.event, listener, true);
      }
    },
    [from, until] as const,
  );
  return async () => {
    await expect.poll(() => page.evaluate(() => (window as unknown as { __out?: number }).__out !== undefined), { timeout: 15_000 }).toBe(true);
    return page.evaluate(() => {
      const w = window as unknown as { __in: number; __out: number };
      return Math.round(w.__out - w.__in);
    });
  };
}

function record(row: string, mode: RenderMode, delay: number, figure: number): void {
  test.info().annotations.push({ type: 'figure', description: `${row}: ${mode}${mode === 'server' ? ` +${delay} ms` : ''}: ${figure} ms` });
}

async function open(page: Page, mode: RenderMode, query: Query = {}): Promise<void> {
  await openToast(page, mode, query);
}

test.describe('Toast relaxations', () => {
  // TST-DOM-02, TST-DOM-06, TST-DOM-15, TST-PTR-01: the hover report, then C#'s render of data-expanded (two legs). The
  // timers pause in the same handler that writes `hovering`, so this is also the pause latency (STA-21, PTR-01..04).
  perDelay('port: data-expanded and the pause follow the hover by a round trip', async ({ page, mode, delay }) => {
    await open(page, mode);
    const root = await addToast(page);
    const measured = await arm(page, { event: 'mouseover', within: '[data-testid="viewport"]' }, { selector: '[data-testid="viewport"]', attribute: 'data-expanded' });
    const { x, y } = await centreOf(root);
    await page.mouse.move(x, y);
    const figure = await measured();
    record('data-expanded / pause after hover', mode, delay, figure);
    expect(figure).toBeGreaterThanOrEqual(legsFloor(delay, 2));
  });

  // TST-ARIA-06, TST-ARIA-07: F6's report, then C#'s render of the high-priority root's and the close's aria-hidden.
  perDelay("port: the close's aria-hidden follows F6 by a round trip", async ({ page, mode, delay }) => {
    await open(page, mode, { priority: 'high' });
    await addToast(page);
    const measured = await arm(page, { event: 'keydown' }, { selector: '[aria-label="close-press"]', attribute: 'aria-hidden', value: 'false' });
    await page.keyboard.press('F6');
    const figure = await measured();
    record('close aria-hidden after F6', mode, delay, figure);
    expect(figure).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    await expect(page.getByTestId('root')).not.toHaveAttribute('aria-hidden');
  });

  // TST-PTR-17: the release report, the close's render, then the `transitions` update's data-ending-style; the release
  // freeze keeps the toast at the drag position meanwhile.
  perDelay('port: data-ending-style follows a dismissing release by a round trip, under the freeze', async ({ page, mode, delay }) => {
    await open(page, mode, { directions: 'right', animated: '1', animations: '1' });
    const root = await addToast(page);
    const measured = await arm(page, { event: 'pointerup' }, { selector: '[data-testid="root"]', attribute: 'data-ending-style' });
    await mouseSwipe(page, await centreOf(root), { x: 60, y: 0 });
    const figure = await measured();
    record('data-ending-style after a dismissing release', mode, delay, figure);
    expect(figure).toBeGreaterThanOrEqual(legsFloor(delay, 2));
  });

  // TST-ANI-03, TST-DOM-12: a content mutation, the module's measurement report, then the root's update with the new
  // --toast-height (ADR 01's row for --toast-height, now with figures).
  perDelay('port: --toast-height follows a content change by a round trip', async ({ page, mode, delay }) => {
    await open(page, mode, { content: '1', width: '30' });
    await addToast(page);
    const measured = await arm(page, { mutationIn: '[data-testid="description"]' }, { selector: '[data-testid="root"]', style: '--toast-height', changed: true });
    await page.getByTestId('update').click();
    const figure = await measured();
    record('--toast-height after a content change', mode, delay, figure);
    expect(figure).toBeGreaterThanOrEqual(legsFloor(delay, 2));
  });

  // TST-ANI-05, TST-ANI-06 (via TRN-ANI-09): from data-ending-style to the removal, with no animation: the completion
  // message, then C#'s render that removes the root. §9 says "about two round trips"; ADR 01 measured about one.
  perDelay('port: the removal follows data-ending-style by a round trip when nothing animates', async ({ page, mode, delay }) => {
    await open(page, mode);
    await addToast(page);
    const measured = await arm(page, { mutationIn: '[data-testid="root"]' }, { selector: '[data-testid="root"]', gone: true });
    await page.getByTestId('close-newest').click();
    const figure = await measured();
    record('removal after data-ending-style (no animation)', mode, delay, figure);
    expect(figure).toBeGreaterThanOrEqual(legsFloor(delay, 2));
  });

  // DP1 of the Toast track (chosen by the user 2026-11-19): the attach latency of a second toast: the add's click, C#'s
  // render with the new root, its handle's attach and measurement, the height report, and the root's update.
  perDelay("port: a second toast's handle attaches and measures within two round trips of the add", async ({ page, mode, delay }) => {
    await open(page, mode);
    await addToast(page);
    const measured = await arm(page, { event: 'click', within: '[data-testid="add"]' }, { selector: '[data-toast-id="t2"]', attribute: 'data-height' });
    await page.getByTestId('add').click();
    const figure = await measured();
    record('second toast measured after the add', mode, delay, figure);
    expect(figure).toBeGreaterThanOrEqual(legsFloor(delay, 4));
  });
});
