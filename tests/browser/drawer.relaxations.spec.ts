import { test, expect, delayCircuit, type Page, type RenderMode } from './helpers';
import { gotoPopupFixture } from './suites/popupFixture';

// DRW §9's Server relaxations (docs/components/drawer.md) against ADR 01's relaxation table, measured per render mode on
// the Drawer fixtures (M6 track 8b slice 5, issue 279, added 2026-11-21): each row at 0, 50 and 150 ms of one-way delay
// added to the Server circuit (`delayCircuit`, before the first navigation; 0 ms is the bare localhost circuit), and once
// on WebAssembly. Each act is dispatched in the page, and its witness is a `MutationObserver` the page adds; the figure is
// the page's `performance.now()` from the act's last event to the witness, logged as a `figure` annotation. The assertions
// do not depend on the runner's speed (issue 114's rule): on Server they assert the floor the added delay puts under the
// legs a row needs, and on WebAssembly that the witness held inside the act's own task where the row is met there.

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

function figure(row: string, mode: RenderMode, delay: number, values: Record<string, number | boolean>): void {
  test.info().annotations.push({ type: 'figure', description: `${row} ${mode} +${delay} ${JSON.stringify(values)}` });
}

interface Timed {
  /** Milliseconds from the act's last event to the witness. */
  ms: number;
  /** Whether the witness held before a 0 ms timer queued at the act's last event ran (inside its task). */
  inTask: boolean;
}

test.describe('Drawer: ADR 01 relaxation rows', () => {
  // DRW-PTR-14, PTR-15: a canceled swipe dismissal rolls back. WebAssembly decides it inside the release (`dismiss` point);
  // Server proceeds and rolls back when the answering update arrives.
  perDelay('a canceled swipe dismissal rolls back', async ({ page, mode, delay }) => {
    await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'right', cancelClose: 'true' });
    const timed: Timed = await page.getByTestId('popup').evaluate((popup) => new Promise<Timed>((resolve) => {
      const fire = (type: string, x: number, time: number) => {
        const event = new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, isPrimary: true, pointerId: 1, pointerType: 'mouse', button: type === 'pointermove' ? -1 : 0, buttons: type === 'pointerup' ? 0 : 1, clientX: x, clientY: 10 });
        Object.defineProperty(event, 'timeStamp', { value: time });
        (document.elementFromPoint(x, 10) ?? popup).dispatchEvent(event);
      };
      fire('pointerdown', 10, 1000);
      fire('pointermove', 11, 1016);
      fire('pointermove', 160, 1032);
      let tick = false;
      const start = performance.now();
      setTimeout(() => (tick = true), 0);
      const done = () => !popup.hasAttribute('data-swipe-dismiss');
      fire('pointerup', 160, 1048);
      const check = () => {
        if (done()) {
          observer.disconnect();
          resolve({ ms: performance.now() - start, inTask: !tick });
        }
      };
      const observer = new MutationObserver(() => queueMicrotask(check));
      observer.observe(popup, { attributes: true });
      queueMicrotask(check);
    }));
    figure('PTR-14/15 rollback', mode, delay, { ...timed });
    if (mode === 'wasm') {
      expect(timed.inTask).toBe(true);
    } else {
      expect(timed.ms).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // DRW-ALG-01: a canceled change to no snap point. WebAssembly settles in place inside the release (`snap-point:none`);
  // Server shows the exit styles until the answer, as recorded in drawer.md's ALG-01 note.
  perDelay('a canceled change to no snap point leaves the drawer at rest', async ({ page, mode, delay }) => {
    await gotoPopupFixture(page, mode, 'drawer-swipe', { case: 'snap', snapPoints: '100px,300px', defaultSnap: '100px', cancelSnap: 'null' });
    const timed = await page.getByTestId('popup').evaluate((popup) => new Promise<Timed & { endingSeen: boolean }>((resolve) => {
      let endingSeen = false;
      const fire = (type: string, y: number, time: number) => {
        const event = new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, isPrimary: true, pointerId: 1, pointerType: 'mouse', button: type === 'pointermove' ? -1 : 0, buttons: type === 'pointerup' ? 0 : 1, clientX: 10, clientY: y });
        Object.defineProperty(event, 'timeStamp', { value: time });
        (document.elementFromPoint(10, y) ?? popup).dispatchEvent(event);
      };
      const watcher = new MutationObserver(() => {
        endingSeen ||= popup.hasAttribute('data-ending-style') || popup.hasAttribute('data-swipe-dismiss');
      });
      watcher.observe(popup, { attributes: true });
      fire('pointerdown', 50, 1000);
      fire('pointermove', 51, 1016);
      fire('pointermove', 250, 1024);
      let tick = false;
      const start = performance.now();
      setTimeout(() => (tick = true), 0);
      fire('pointerup', 250, 1032);
      endingSeen ||= popup.hasAttribute('data-ending-style') || popup.hasAttribute('data-swipe-dismiss');
      const check = () => {
        if (!popup.hasAttribute('data-ending-style') && !popup.hasAttribute('data-swipe-dismiss')) {
          observer.disconnect();
          watcher.disconnect();
          resolve({ ms: performance.now() - start, inTask: !tick, endingSeen });
        }
      };
      const observer = new MutationObserver(() => queueMicrotask(check));
      observer.observe(popup, { attributes: true });
      setTimeout(check, 0);
    }));
    figure('ALG-01 canceled close', mode, delay, { ...timed });
    if (mode === 'wasm') {
      expect(timed.endingSeen).toBe(false);
    } else {
      expect(timed.ms).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // DRW-STA-10, STA-11, DOM-10: the Popup's measurement reaches C#, whose frontmost height comes back rendered.
  perDelay('a nested drawer\'s height reaches its parent\'s frontmost height', async ({ page, mode, delay }) => {
    await gotoPopupFixture(page, mode, 'drawer-stack', { case: 'stretch' });
    const timed = await page.getByTestId('parent-popup').evaluate((parent) => new Promise<Timed>((resolve) => {
      const trigger = Array.from(document.querySelectorAll('button')).find((button) => button.textContent === 'Open nested drawer')!;
      let tick = false;
      const start = performance.now();
      setTimeout(() => (tick = true), 0);
      trigger.click();
      const check = () => {
        if ((parent as HTMLElement).style.getPropertyValue('--drawer-frontmost-height') === '204px') {
          observer.disconnect();
          resolve({ ms: performance.now() - start, inTask: !tick });
        }
      };
      const observer = new MutationObserver(() => queueMicrotask(check));
      observer.observe(parent, { attributes: true });
    }));
    figure('STA-10/11 frontmost height', mode, delay, { ...timed });
    if (mode === 'server') {
      expect(timed.ms).toBeGreaterThanOrEqual(legsFloor(delay, 4));
    }
  });

  // DRW-PTR-17, STA-12, DOM-04: a nested drawer's swiping reaches its parent's `data-nested-drawer-swiping` (RND-STA-01).
  perDelay('a nested drawer\'s swiping marks its parent', async ({ page, mode, delay }) => {
    await gotoPopupFixture(page, mode, 'drawer-stack', { case: 'nested' });
    const timed = await page.getByTestId('parent-popup').evaluate((parent) => new Promise<Timed>((resolve) => {
      const child = document.querySelector('[data-testid=child-popup]')!;
      const fire = (type: string, y: number, time: number) => {
        const event = new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, isPrimary: true, pointerId: 1, pointerType: 'mouse', button: type === 'pointermove' ? -1 : 0, buttons: type === 'pointerup' ? 0 : 1, clientX: 100, clientY: y });
        Object.defineProperty(event, 'timeStamp', { value: time });
        (document.elementFromPoint(100, y) ?? child).dispatchEvent(event);
      };
      fire('pointerdown', 60, 1000);
      fire('pointermove', 61, 1016);
      let tick = false;
      const start = performance.now();
      setTimeout(() => (tick = true), 0);
      fire('pointermove', 90, 1032);
      const check = () => {
        if (parent.hasAttribute('data-nested-drawer-swiping')) {
          observer.disconnect();
          fire('pointerup', 90, 5000);
          resolve({ ms: performance.now() - start, inTask: !tick });
        }
      };
      const observer = new MutationObserver(() => queueMicrotask(check));
      observer.observe(parent, { attributes: true });
      queueMicrotask(check);
    }));
    figure('PTR-17 nested swiping', mode, delay, { ...timed });
    if (mode === 'server') {
      expect(timed.ms).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // DRW-PTR-29: the swipe area opens the drawer, and the popup is moved in the mutation that renders it open, so no frame
  // paints it at rest (sampled in each frame before its paint); the open itself is a round trip away on Server.
  perDelay('the swipe area moves the popup before it is first painted', async ({ page, mode, delay }) => {
    await gotoPopupFixture(page, mode, 'drawer-swipe-area', { parts: 'true' });
    const result = await page.getByTestId('swipe-area').evaluate((area) => new Promise<{ openMs: number; gapMs: number; restSeen: boolean }>((resolve) => {
      const fire = (type: string, y: number, time: number) => {
        const event = new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, isPrimary: true, pointerId: 1, pointerType: 'mouse', button: type === 'pointermove' ? -1 : 0, buttons: type === 'pointerup' ? 0 : 1, clientX: 10, clientY: y });
        Object.defineProperty(event, 'timeStamp', { value: time });
        area.dispatchEvent(event);
      };
      let openedAt = -1;
      let restSeen = false;
      const start = performance.now();
      // A frame's callbacks run after the task's microtasks and before its paint: a popup sampled open and unmoved there
      // is a popup painted at rest.
      const sample = () => {
        // Before the portal places it the popup has no box (no height), so nothing of it is painted.
        const popup = document.querySelector<HTMLElement>('[data-testid=popup][data-open]');
        if (popup && popup.offsetHeight > 0) {
          if (openedAt < 0) {
            openedAt = performance.now();
          }
          const moved = popup.style.getPropertyValue('--drawer-swipe-movement-y');
          if (moved === '' || moved === '0px') {
            restSeen = true;
          } else {
            fire('pointerup', 40, 5000);
            resolve({ openMs: openedAt - start, gapMs: performance.now() - openedAt, restSeen });
            return;
          }
        }
        requestAnimationFrame(sample);
      };
      requestAnimationFrame(sample);
      fire('pointerdown', 120, 1000);
      fire('pointermove', 119, 1016);
      fire('pointermove', 60, 1032);
    }));
    figure('PTR-29 first paint', mode, delay, result);
    expect(result.restSeen).toBe(false);
    if (mode === 'server') {
      expect(result.openMs).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });
});
