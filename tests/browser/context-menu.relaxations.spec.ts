import { type Locator } from '@playwright/test';
import { delayCircuit, expect, test, type Page, type RenderMode } from './helpers';
import { center, parkPointer } from './menu.helpers';
import { gotoPopupFixture, openChanges, type PopupFixtureOptions } from './suites/popupFixture';

// CTX §9's three Server measurements (M6 track 11, issue 259; decision 8 of the M6 plan), against ADR 01's relaxation table,
// on the `context-menu-root` fixture: each row at 0, 50 and 150 ms of one-way delay added to the Server circuit
// (`delayCircuit`, before the first navigation; 0 ms is the bare localhost circuit), and once on WebAssembly. Each title logs
// its figures (ms after the gesture's event, the page's `performance.now()`) as a `figure` annotation. The witness is a
// `MutationObserver` and a `focusin` listener the page adds, which read the state in a microtask queued from their callbacks
// (the recorder rule of tests/browser/CONTEXT.md); the gesture's time is a capture listener on the window. The assertions
// do not depend on the runner's speed (issue 114's rule): every marker holds, in order, and on Server no marker C# writes
// lands before the two legs a round trip takes.

const FIXTURE = 'context-menu-root';
const SERVER_DELAYS = [0, 50, 150] as const;

/** The least time `count` delayed legs take as the page sees it (the route's timers may fire a millisecond early). */
const legsFloor = (delay: number, count: number): number => (delay === 0 ? 0 : count * delay - 2 * count);

type Marks = Record<string, number>;

/** One title per delay; a WebAssembly project runs the 0 ms title only. */
function perDelay(title: string, body: (args: { page: Page; mode: RenderMode; delay: number }) => Promise<void>): void {
  for (const delay of SERVER_DELAYS) {
    test(`${title} (${delay} ms added on Server)`, async ({ page, mode }) => {
      test.skip(mode === 'wasm' && delay > 0, 'WebAssembly has no circuit to delay; its title runs once.');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }
      await body({ page, mode, delay });
    });
  }
}

async function open(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}, mac = false): Promise<Locator> {
  await page.addInitScript((value) => {
    Object.defineProperty(Navigator.prototype, 'platform', { configurable: true, get: () => value });
  }, mac ? 'MacIntel' : 'Linux x86_64');
  await parkPointer(page);
  return gotoPopupFixture(page, mode, FIXTURE, options);
}

/**
 * Starts the witness: `act` is the time of the first right-button `event` of the window (capture) after the start; every
 * marker is the first time its state holds after `act`, in ms after it:
 * - `popup`, `gone`: the popup is in the page, or not;
 * - `focus`: the active element is inside the popup;
 * - `aria-hidden`: the outside button is under an `aria-hidden="true"` or `inert` ancestor;
 * - `scroll-lock`: the document's scroll lock (the `isScrollLocked` helper's three marks);
 * - `click`: the fixture's `data-item-clicks` names the item "Second".
 */
async function startWitness(page: Page, event: 'contextmenu' | 'mouseup'): Promise<void> {
  await page.evaluate((type) => {
    const w = window as unknown as { __marks: Record<string, number>; __act?: number };
    w.__marks = {};
    delete w.__act;
    const mark = (name: string) => {
      if (w.__act !== undefined && !(name in w.__marks)) {
        w.__marks[name] = Math.round((performance.now() - w.__act) * 10) / 10;
      }
    };
    const check = () => {
      const popup = document.querySelector('[data-testid="popup"]');
      mark(popup ? 'popup' : 'gone');
      if (popup && popup.contains(document.activeElement)) mark('focus');
      if (document.querySelector('[data-testid="outside"]')?.closest('[aria-hidden="true"], [inert]')) mark('aria-hidden');
      const html = document.documentElement;
      if (html.style.overflow === 'hidden' || html.hasAttribute('data-base-ui-scroll-locked') || document.body.style.overflow === 'hidden') {
        mark('scroll-lock');
      }
      if ((document.querySelector('[data-fixture="context-menu-root"]')?.getAttribute('data-item-clicks') ?? '').includes('Second')) mark('click');
    };
    window.addEventListener(type, (e) => {
      if (w.__act === undefined && (e as MouseEvent).button === 2) w.__act = performance.now();
    }, true);
    const queue = () => queueMicrotask(check);
    new MutationObserver(queue).observe(document, { subtree: true, childList: true, attributes: true });
    document.addEventListener('focusin', queue, true);
  }, event);
}

async function marks(page: Page, names: string[]): Promise<Marks> {
  await expect.poll(() => page.evaluate((list) => list.every((name) => name in (window as unknown as { __marks: Marks }).__marks), names)).toBe(true);
  return page.evaluate(() => (window as unknown as { __marks: Marks }).__marks);
}

function figure(mode: RenderMode, delay: number, values: Marks): void {
  const description = `${mode} ${delay} ms: ${JSON.stringify(values)}`;
  test.info().annotations.push({ type: 'figure', description });
  console.log(`figure | ${test.info().title} | ${description}`);
}

test.describe('ContextMenu: ADR 01 relaxation rows', () => {
  // CTX-PTR-05, CTX-EVT-05: the `cancel-open` close. The module recognises the release outside after 500 ms and reports it;
  // C# closes, so on Server the popup goes a round trip after the `mouseup`.
  perDelay('the cancel-open close: the popup is gone after the release', async ({ page, mode, delay }) => {
    const fixture = await open(page, mode);
    const trigger = await center(page.getByTestId('trigger'));
    await page.mouse.move(trigger.x, trigger.y);
    await page.mouse.down({ button: 'right' });
    await expect(page.getByRole('menu')).toBeVisible();
    await page.mouse.move(5, 5);
    const start = await page.evaluate(() => performance.now());
    await page.waitForFunction((from) => performance.now() - from > 520, start);
    await startWitness(page, 'mouseup');

    await page.mouse.up({ button: 'right' });

    const values = await marks(page, ['gone']);
    figure(mode, delay, { closed: values.gone! });
    expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:cancel-open']);
    if (mode === 'server') {
      expect(values.gone!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // CTX-FOC-01, CTX-FOC-02, CTX-ARIA-04, CTX-DIS-05: the modal root popup's opening batch. The popup, its initial focus, the
  // outside content's `aria-hidden` and the scroll lock, after the `contextmenu`.
  perDelay("the modal root popup's opening batch: focus, aria-hidden and the scroll lock after the popup", async ({ page, mode, delay }) => {
    await open(page, mode);
    await startWitness(page, 'contextmenu');
    const trigger = await center(page.getByTestId('trigger'));
    await page.mouse.move(trigger.x, trigger.y);
    await page.mouse.down({ button: 'right' });
    await page.mouse.up({ button: 'right' });

    const values = await marks(page, ['popup', 'focus', 'aria-hidden', 'scroll-lock']);
    figure(mode, delay, { popup: values.popup!, focus: values.focus!, 'aria-hidden': values['aria-hidden']!, 'scroll-lock': values['scroll-lock']! });
    for (const name of ['focus', 'aria-hidden', 'scroll-lock']) {
      expect(values[name]!).toBeGreaterThanOrEqual(values.popup!);
    }
    if (mode === 'server') {
      expect(values.popup!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // CTX-PTR-09, CTX-EVT-01 (`item-press`): on macOS a right-button release over an item of a DefaultOpen menu activates it.
  // The release is pointer state in `interactions`; the item's click and the `item-press` close follow.
  perDelay("a release's item-press: the item's click and the close after the release", async ({ page, mode, delay }) => {
    const fixture = await open(page, mode, { defaultOpen: true }, true);
    const item = await center(page.getByTestId('second'));
    await page.mouse.move(item.x, item.y);
    await page.mouse.down({ button: 'right' });
    await startWitness(page, 'mouseup');

    await page.mouse.up({ button: 'right' });

    const values = await marks(page, ['click', 'gone']);
    figure(mode, delay, { click: values.click!, closed: values.gone! });
    expect((await openChanges(fixture)).at(-1)).toBe('false:item-press');
    if (mode === 'server') {
      expect(values.click!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      expect(values.gone!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });
});
