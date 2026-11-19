import type { Locator } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';
import { delayCircuit, holdCircuit } from './helpers/circuit';

// The scrub role's [L4] rows (issue 277, 6b; NUM-STR-06, NUM-DOM-13/14, NUM-PTR-10, 13, 14, 17, 19, NUM-FOC-04,
// NUM-EDGE-09): NumberFieldScrubArea.test.tsx's and NumberFieldScrubAreaCursor.test.tsx's titles on the
// `number-field-scrub` fixture, Server then WebAssembly.
// Pointer lock (design point 10 (a), chosen by the user 2026-11-19; the probe of 2026-11-19): only new headless Chromium
// grants `requestPointerLock()`, so this file alone runs on `channel: 'chromium'`; every other spec stays on the headless
// shell. The Gecko 20 ms release and the WebKit no-lock branch are L3 over a stubbed `platform` (number-field.scrub.test.ts).
// Moves: a real mouse where Base UI's title needs only some movement; where it needs exact `movementX`/`movementY` values
// (NumberFieldScrubArea.test.tsx:14-36), the move is dispatched in the page as Base UI dispatches it, reaching the module's
// window capture listener the same way (the tests/browser/CONTEXT.md note on in-page PointerEvent dispatches).
test.use({ channel: 'chromium' });

async function gotoScrub(page: Page, mode: RenderMode, scrubCase = 'basic') {
  await page.goto(`/${mode}/number-field-scrub?case=${scrubCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-scrub'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return {
    fixture,
    root: fixture.getByTestId('root'),
    input: fixture.getByRole('textbox'),
    area: fixture.getByTestId('scrub-area'),
    cursor: page.getByTestId('scrub-area-cursor'),
  };
}

// A real press at the area's centre; the lock is granted inside it (the probe), so later moves report exact deltas. CDP
// positions are absolute: the returned point is the press, from which a title moves.
async function press(page: Page, area: Locator, button: 'left' | 'middle' = 'left') {
  const box = (await area.boundingBox())!;
  const at = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
  await page.mouse.move(at.x, at.y);
  await page.mouse.down({ button });
  return at;
}

async function waitForLock(page: Page) {
  await page.waitForFunction(() => document.pointerLockElement === document.body);
}

// NumberFieldScrubArea.test.tsx:25-36: a pointermove with exact movement, bubbling to the window's capture listener.
async function dispatchMove(area: Locator, movementX: number, movementY = 0) {
  await area.evaluate((element, [x, y]) => {
    element.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, cancelable: true, movementX: x, movementY: y }));
  }, [movementX, movementY] as const);
}

// NumberFieldScrubArea.test.tsx:14-23: Base UI's synthetic pointerdown (no pointerType), as its exact-movement titles use.
async function dispatchDown(area: Locator, init: { button?: number; pointerType?: string } = {}) {
  await area.evaluate((element, extra) => {
    const box = element.getBoundingClientRect();
    element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, clientX: box.left + box.width / 2, clientY: box.top + box.height / 2, ...extra }));
  }, init);
}

async function dispatchUp(page: Page) {
  await page.evaluate(() => window.dispatchEvent(new PointerEvent('pointerup', { bubbles: true })));
}

// The first red L4 title of 6b (decision 6 (a), ADR 04 Amendments 2026-11-18's revision rule): C#'s answers to the first
// scrub step are held while the user scrubs again and releases; the input ends at the last step with no snap-back.
test('port: holds the circuit after a scrub step, scrubs again and releases, and ends at the last step (decision 6)', async ({ page, mode }) => {
  const hold = await holdCircuit(page, {
    armOn: (text) => text.includes('OnScrubStep'),
    from: () => true,
    until: (text) => text.includes('OnScrubEnd'),
  });
  const { fixture, input } = await gotoScrub(page, mode);
  const area = fixture.getByTestId('scrub-area');

  await press(page, area);
  await waitForLock(page);
  hold.arm();
  await dispatchMove(area, 10);
  await dispatchMove(area, 10);
  await page.mouse.up();
  if (mode === 'server') {
    await hold.released();
  }

  await expect(fixture).toHaveAttribute('data-commits', '20/scrub');
  await expect(fixture).toHaveAttribute('data-changes', '10/scrub 20/scrub');
  await expect(input).toHaveValue('20');
});

test('should increment or decrement the value when scrubbing with the pointer', async ({ page, mode }) => {
  const { area, input } = await gotoScrub(page, mode);

  const at = await press(page, area);
  await waitForLock(page);
  // Under the lock the position is virtual: Chromium reports the delta between the CDP positions as the movement.
  await page.mouse.move(at.x - 10, at.y);
  await expect(input).toHaveValue('-10');
  await page.mouse.move(at.x - 5, at.y);
  await expect(input).toHaveValue('-5');
  await page.mouse.move(at.x - 7, at.y);
  await expect(input).toHaveValue('-7');
  await page.mouse.up();
});

test('calls onValueChange while scrubbing and onValueCommitted on pointerup', async ({ page, mode }) => {
  const { fixture, area } = await gotoScrub(page, mode);

  const at = await press(page, area);
  await waitForLock(page);
  await page.mouse.move(at.x + 3, at.y);
  await expect(fixture).toHaveAttribute('data-changes', /\/scrub$/);
  await page.mouse.up();

  await expect(fixture).toHaveAttribute('data-commits', /^-?\d+\/scrub$/);
  const changes = (await fixture.getAttribute('data-changes'))!.split(' ');
  const commit = (await fixture.getAttribute('data-commits'))!;
  expect(commit).toBe(changes[changes.length - 1]);
});

test('ignores pointer movement once the scrub has ended', async ({ page, mode }) => {
  const { fixture, area, input } = await gotoScrub(page, mode);

  await dispatchDown(area);
  await dispatchMove(area, 10);
  await expect(input).toHaveValue('10');
  await dispatchUp(page);
  await dispatchMove(area, 10);

  await expect(fixture).toHaveAttribute('data-commits', '10/scrub');
  await expect(input).toHaveValue('10');
  await expect(fixture).toHaveAttribute('data-changes', '10/scrub');
});

test('does not change the value for a zero-distance move when sensitivity is 0', async ({ page, mode }) => {
  const { fixture, area, input } = await gotoScrub(page, mode, 'zero');

  await dispatchDown(area);
  await dispatchMove(area, 0);
  await dispatchUp(page);

  // The release's commit is the marker that C# has handled the move before it.
  await expect(fixture).toHaveAttribute('data-commits', '3.5/scrub');
  await expect(input).toHaveValue('3.5');
  await expect(fixture).toHaveAttribute('data-changes', '');
});

test('should only increment if the pointer movement was greater than or equal to the value', async ({ page, mode }) => {
  const { fixture, area, input } = await gotoScrub(page, mode, 'sensitivity');

  await dispatchDown(area);
  await dispatchMove(area, -2);
  await dispatchMove(area, 2);
  for (let i = 0; i < 4; i += 1) {
    await dispatchMove(area, 1);
  }
  await dispatchMove(area, 1);
  await expect(input).toHaveValue('1');
  await dispatchMove(area, 5);
  await expect(input).toHaveValue('6');
  await dispatchMove(area, -4);
  await dispatchMove(area, -1);
  await expect(input).toHaveValue('5');
  await dispatchMove(area, 5);
  await expect(input).toHaveValue('10');
  await expect(fixture).toHaveAttribute('data-changes', '1/scrub 6/scrub 5/scrub 10/scrub');
  await dispatchUp(page);
});

test('wraps the virtual cursor around the allowed bounds', async ({ page, mode }) => {
  const { area, cursor } = await gotoScrub(page, mode, 'teleport');

  await press(page, area);
  await waitForLock(page);
  // On Server the cursor exists a round trip after the press (design point 8 (a)).
  await expect(cursor).toHaveCount(1);
  await expect(cursor).toHaveCSS('transform', /matrix/);
  // The bare cursor has no size, so its wrap coordinates are the bounds themselves.
  expect(await cursor.evaluate((element) => (element as HTMLElement).offsetWidth)).toBe(0);

  // Past the right bound (120 + 100 / 2): the cursor reappears at the left one.
  await dispatchMove(area, 5000);
  await expect.poll(() => cursor.evaluate((element) => (element as HTMLElement).style.transform)).toContain('translate3d(50px,');
  // And symmetrically past the left bound (100 - 100 / 2).
  await dispatchMove(area, -5000);
  await expect.poll(() => cursor.evaluate((element) => (element as HTMLElement).style.transform)).toContain('translate3d(170px,');
  await page.mouse.up();
});

test('scrubs on vertical movement when direction is vertical', async ({ page, mode }) => {
  const { fixture, area, input } = await gotoScrub(page, mode, 'vertical');

  await dispatchDown(area);
  await dispatchMove(area, 10, 0);
  // Upward movement (negative Y) increases the value.
  await dispatchMove(area, 0, -10);
  await expect(input).toHaveValue('10');
  await dispatchMove(area, 0, 4);
  await expect(input).toHaveValue('6');
  await expect(fixture).toHaveAttribute('data-changes', '10/scrub 6/scrub');
  await dispatchUp(page);
});

test('should only scrub if the pointer moved in the given direction', async ({ page, mode }) => {
  const { fixture, area, input } = await gotoScrub(page, mode);

  await dispatchDown(area);
  await dispatchMove(area, 10);
  await expect(input).toHaveValue('10');
  await dispatchMove(area, 0, 10);
  await dispatchUp(page);

  await expect(fixture).toHaveAttribute('data-commits', '10/scrub');
  await expect(input).toHaveValue('10');
});

test('should fire onClick once when clicked without scrubbing', async ({ page, mode }) => {
  const { fixture, area } = await gotoScrub(page, mode, 'click');
  await area.evaluate((element) => {
    (window as unknown as { areaClicks: number }).areaClicks = 0;
    element.addEventListener('click', () => {
      (window as unknown as { areaClicks: number }).areaClicks += 1;
    });
  });

  await area.click();
  // The synthesized click is deferred by one task; 50 ms in the page, as Base UI waits.
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 50)));

  expect(await page.evaluate(() => (window as unknown as { areaClicks: number }).areaClicks)).toBe(1);
  await expect(fixture).toHaveAttribute('data-clicks', 'area');
});

test('should fire onClick on child elements', async ({ page, mode }) => {
  const { fixture } = await gotoScrub(page, mode, 'click');

  await fixture.getByText('Amount').click();

  await expect(fixture).toHaveAttribute('data-clicks', 'label area');
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 50)));
  await expect(fixture).toHaveAttribute('data-clicks', 'label area');
});

test('ignores non-primary pointer buttons', async ({ page, mode }) => {
  const { fixture, area, root, input } = await gotoScrub(page, mode);

  await dispatchDown(area, { button: 1 });
  await dispatchMove(area, 10);
  await dispatchUp(page);
  // A primary press after it does scrub (the red check's positive half).
  await dispatchDown(area);
  await expect(root).toHaveAttribute('data-scrubbing', '');
  await dispatchUp(page);

  await expect(fixture).toHaveAttribute('data-commits', '0/scrub');
  await expect(input).toHaveValue('0');
});

test('does not start scrubbing when the field is read-only', async ({ page, mode }) => {
  const { fixture, area, root } = await gotoScrub(page, mode, 'readonly');

  await press(page, area);
  await page.mouse.up();
  await page.mouse.click(1, 1);

  await expect(root).not.toHaveAttribute('data-scrubbing');
  await expect(root).toHaveAttribute('data-readonly', '');
  await expect(fixture).toHaveAttribute('data-commits', '');
  expect(await page.evaluate(() => document.pointerLockElement)).toBeNull();
});

test('keeps a selection the consumer sets in onFocus when a mouse press focuses the input', async ({ page, mode }) => {
  const { area, input } = await gotoScrub(page, mode, 'select');
  await input.evaluate((element) => element.addEventListener('focus', (event) => (event.currentTarget as HTMLInputElement).select()));

  await press(page, area);

  await expect(input).toBeFocused();
  expect(await input.evaluate((element: HTMLInputElement) => [element.selectionStart, element.selectionEnd, element.value.length])).toEqual([0, 3, 3]);
  await page.mouse.up();
});

test('port: a mouse press focuses the input with the caret at the end and never focuses the scrub area (NUM-FOC-04)', async ({ page, mode }) => {
  const { area, input } = await gotoScrub(page, mode, 'select');

  await press(page, area);

  await expect(input).toBeFocused();
  expect(await input.evaluate((element: HTMLInputElement) => [element.selectionStart, element.selectionEnd])).toEqual([3, 3]);
  await page.mouse.up();
});

test('tracks visual viewport scale and removes scrub listeners after pointer lock exits', async ({ page, mode }) => {
  const { area, cursor, root } = await gotoScrub(page, mode);
  const session = await page.context().newCDPSession(page);
  await session.send('Emulation.setPageScaleFactor', { pageScaleFactor: 2 });
  await page.waitForFunction(() => window.visualViewport?.scale === 2);

  await press(page, area);
  await expect(cursor).toHaveCount(1);
  await expect.poll(() => cursor.evaluate((element) => (element as HTMLElement).style.transform)).toContain('scale(0.5)');

  // Base UI stubs the scale getter to 4; Chromium's page scale is set here, back to 1.
  await session.send('Emulation.setPageScaleFactor', { pageScaleFactor: 1 });
  await page.waitForFunction(() => window.visualViewport?.scale === 1);
  await dispatchMove(area, 4);
  await expect.poll(() => cursor.evaluate((element) => (element as HTMLElement).style.transform)).toContain('scale(1)');

  await page.mouse.up();
  await expect(root).not.toHaveAttribute('data-scrubbing');
  await expect(cursor).toHaveCount(0);
  // The listeners' removal is L3's (number-field.scrub.test.ts, NUM-PTR-18).
});

test('clears the root scrubbing state when the scrub area unmounts mid-scrub', async ({ page, mode }) => {
  const { fixture, area, root } = await gotoScrub(page, mode, 'toggle');

  await dispatchDown(area);
  await dispatchMove(area, -10);
  await expect(root).toHaveAttribute('data-scrubbing', '');

  // Unmount the scrub area before pointerup; the root must not stay stuck in the scrubbing state.
  await fixture.getByTestId('unmount').dispatchEvent('click');

  await expect(fixture.getByTestId('scrub-area')).toHaveCount(0);
  await expect(root).not.toHaveAttribute('data-scrubbing');
  await expect(fixture).toHaveAttribute('data-commits', '');
});

// NumberFieldScrubArea.test.tsx:391's React.Activity block, ported (Deliverable 5): re-keying the scrub area tears its
// handle down and attaches a new one mid-scrub, without a release.
test('port: does not resume scrubbing when a re-mounted scrub area sees a bare move (NUM-PTR-12, NUM-EDGE-15)', async ({ page, mode }) => {
  const { fixture, area, input } = await gotoScrub(page, mode, 'toggle');

  await dispatchDown(area);
  await dispatchMove(area, 10);
  await expect(input).toHaveValue('10');

  await fixture.getByTestId('rekey').dispatchEvent('click');
  await expect(fixture.getByTestId('root')).not.toHaveAttribute('data-scrubbing');
  // No pointer is down anymore, so a bare mouse move must not change the value.
  await page.evaluate(() => window.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, movementX: 10 })));
  await dispatchDown(fixture.getByTestId('scrub-area'));
  await dispatchUp(page);

  await expect(fixture).toHaveAttribute('data-commits', '10/scrub');
  await expect(input).toHaveValue('10');
});

test('syncs the visible input value when scrubbing after pasting', async ({ page, mode }) => {
  const { fixture, area, input } = await gotoScrub(page, mode, 'paste');

  await input.focus();
  await input.evaluate((element: HTMLInputElement) => {
    element.select();
    const data = new DataTransfer();
    data.setData('text/plain', '20');
    element.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data }));
  });
  await expect(input).toHaveValue('20');
  await expect(fixture).toHaveAttribute('data-changes', '20/input-paste');

  await dispatchDown(area);
  await dispatchMove(area, 2);

  await expect(fixture).toHaveAttribute('data-changes', '20/input-paste 22/scrub');
  await expect(input).toHaveValue('22');
  await dispatchUp(page);
});

test('renders when using mouse input', async ({ page, mode }) => {
  const { area, cursor, root } = await gotoScrub(page, mode);
  await expect(cursor).toHaveCount(0);

  await press(page, area);

  await expect(cursor).toHaveCount(1);
  await expect(cursor).toHaveAttribute('role', 'presentation');
  // NUM-DOM-14: in a portal node appended to the body, outside the component's subtree.
  expect(await cursor.evaluate((element) => [element.parentElement!.hasAttribute('data-base-ui-portal'), element.parentElement!.parentElement === document.body])).toEqual([true, true]);
  await expect(cursor).toHaveCSS('position', 'fixed');
  await expect(cursor).toHaveCSS('pointer-events', 'none');
  await page.mouse.up();
  await expect(root).not.toHaveAttribute('data-scrubbing');
  await expect(cursor).toHaveCount(0);
});

test('only renders a cursor for the active scrub area', async ({ page, mode }) => {
  const { fixture, area, cursor } = await gotoScrub(page, mode, 'two');

  await press(page, area);

  await expect(cursor).toHaveCount(1);
  await expect(fixture.getByTestId('scrub-area-2')).toBeVisible();
  await page.mouse.up();
});

test('does not render when using touch input', async ({ page, mode }) => {
  const { area, cursor, root } = await gotoScrub(page, mode);

  await dispatchDown(area, { pointerType: 'touch' });

  await expect(root).toHaveAttribute('data-scrubbing', '');
  await dispatchUp(page);
  await expect(root).not.toHaveAttribute('data-scrubbing');
  await expect(cursor).toHaveCount(0);
  expect(await page.evaluate(() => document.pointerLockElement)).toBeNull();
});

test('handles pointer lock denial through requestPointerLock API', async ({ page, mode }) => {
  const { fixture, area, cursor, input } = await gotoScrub(page, mode);
  await page.evaluate(() => {
    (window as unknown as { lockRequests: number }).lockRequests = 0;
    HTMLElement.prototype.requestPointerLock = function requestPointerLock() {
      (window as unknown as { lockRequests: number }).lockRequests += 1;
      throw new Error('User denied pointer lock');
    };
  });

  await dispatchDown(area, { pointerType: 'mouse' });
  await dispatchMove(area, 4);
  await expect(input).toHaveValue('4');
  await dispatchUp(page);

  await expect(fixture).toHaveAttribute('data-commits', '4/scrub');
  await expect(cursor).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { lockRequests: number }).lockRequests)).toBe(1);
});

test('does not render after a quick tap when pointer lock resolves later', async ({ page, mode }) => {
  const { fixture, area, cursor } = await gotoScrub(page, mode);
  await page.evaluate(() => {
    HTMLElement.prototype.requestPointerLock = () => new Promise((resolve) => setTimeout(resolve, 30)) as unknown as Promise<void>;
  });

  await dispatchDown(area, { pointerType: 'mouse' });
  await dispatchUp(page);
  await expect(fixture).toHaveAttribute('data-commits', '0/scrub');
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 60)));

  await expect(cursor).toHaveCount(0);
});

// ADR 01's rows for NUM-STA-15, NUM-STR-06 and NUM-DOM-13/14 (decision 8 of the M6 plan): Base UI flushes the scrubbing
// state and the cursor inside the pointerdown; here C# renders them a leg later. From the press to the root's
// `data-scrubbing` (STA-15) and to the cursor's first transform (STR-06, DOM-13/14), in the page's clock; the median of 5
// fresh renders per delay, Server under 0, 50 and 150 ms one-way, WebAssembly once.
for (const delay of [0, 50, 150]) {
  test(`port: the scrub's start timings at ${delay} ms one-way (ADR 01, NUM-STA-15, NUM-STR-06, NUM-DOM-13)`, async ({ page, mode }) => {
    test.skip(mode === 'wasm' && delay !== 0, 'No circuit to delay on WebAssembly');
    if (mode === 'server' && delay > 0) {
      await delayCircuit(page, delay);
    }

    const scrubbing: number[] = [];
    const cursors: number[] = [];
    for (let run = 0; run < 5; run += 1) {
      const { area, cursor } = await gotoScrub(page, mode);
      await page.evaluate(() => {
        const record = { t0: -1, scrubbing: -1, cursor: -1 };
        (window as unknown as { scrubFigure: typeof record }).scrubFigure = record;
        const root = document.querySelector('[data-testid="root"]')!;
        document.addEventListener('pointerdown', () => {
          if (record.t0 < 0) {
            record.t0 = performance.now();
          }
        }, true);
        new MutationObserver(() => {
          if (record.t0 >= 0 && record.scrubbing < 0 && root.hasAttribute('data-scrubbing')) {
            record.scrubbing = performance.now();
          }
          const node = document.querySelector<HTMLElement>('[data-testid="scrub-area-cursor"]');
          if (record.t0 >= 0 && record.cursor < 0 && node !== null && node.style.transform !== '') {
            record.cursor = performance.now();
          }
        }).observe(document.body, { attributes: true, subtree: true, childList: true });
      });
      await press(page, area);
      await expect(cursor).toHaveCount(1);
      await expect.poll(() => page.evaluate(() => (window as unknown as { scrubFigure: { cursor: number } }).scrubFigure.cursor)).toBeGreaterThan(0);
      const record = await page.evaluate(() => (window as unknown as { scrubFigure: { t0: number; scrubbing: number; cursor: number } }).scrubFigure);
      scrubbing.push(record.scrubbing - record.t0);
      cursors.push(record.cursor - record.t0);
      await page.mouse.up();
    }

    const median = (values: number[]) => [...values].sort((a, b) => a - b)[2]!.toFixed(0);
    const description = `NUM-STA-15/STR-06/DOM-13 ${mode} ${delay} ms: data-scrubbing median ${median(scrubbing)} ms (${scrubbing.map((t) => t.toFixed(0)).join(', ')}); cursor transform median ${median(cursors)} ms (${cursors.map((t) => t.toFixed(0)).join(', ')})`;
    test.info().annotations.push({ type: 'figure', description });
    console.log(`[figure] ${description}`);
  });
}
