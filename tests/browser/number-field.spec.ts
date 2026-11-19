import { type Locator, type Route } from '@playwright/test';
import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';
import { pointOf, testWithTouch, touchDrag } from './helpers/touch';
import { delayCircuit } from './helpers/circuit';

// The [L4] rows of docs/components/number-field.md that the steppers prove with a real mouse (issue 277, 6a): NUM-PTR-01..06,
// NUM-FOC-03, NUM-EDGE-13, over the `button` module's press-and-hold repeater (UBT-ALG-07) and the hold surface of decision
// 16 (a). Titles are Base UI's (number-field/root/useNumberFieldStepperButton.test.tsx, increment/NumberFieldIncrement.test.tsx),
// run once per stepper where Base UI runs them with describe.each. Base UI dispatches the events under fake timers; here the
// real mouse presses and moves, and the page's clock is paused and run forward (ADR 06 §2). The titles that type into the
// input follow in 6b. The touch and pen titles press with real CDP input (track 1's touchDrag, Input.dispatchMouseEvent's
// pointerType pen), so the browser's own compatibility mouse events and click follow the touch.

const START_AUTO_CHANGE_DELAY = 400;
const CHANGE_VALUE_TICK_DELAY = 60;
const FREEZE_MARGIN = 50;
// Mirrors the touch settle delay in usePressAndHold (NumberFieldIncrement.test.tsx's TOUCH_TIMEOUT).
const TOUCH_TIMEOUT = 50;

// The clock belongs to the worker's browser context: a title that paused it resumes it, or the next page's WebAssembly boot
// waits on its timers (tests/browser/CONTEXT.md).
test.afterEach(async ({ page }) => {
  await page.clock.resume();
});

/** Opens the hold fixture on a case and waits until both steppers have the button module attached. */
async function gotoHold(page: Page, mode: RenderMode, holdCase = 'basic'): Promise<Locator> {
  await page.goto(`/${mode}/number-field-hold?case=${holdCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-hold'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** Pauses the page's clock a margin ahead of the page's now, as tooltip.spec.ts's freezeClock does. */
async function freezeClock(page: Page): Promise<void> {
  for (let attempt = 1; ; attempt += 1) {
    const now = await page.evaluate(() => Date.now());
    try {
      await page.clock.pauseAt(now + FREEZE_MARGIN);
      return;
    } catch (error) {
      if (attempt === 3 || !String(error).includes('Cannot fast-forward to the past')) {
        throw error;
      }
    }
  }
}

/** Base UI's holdForThreeTicks: the start delay, then three repeats. */
async function holdForThreeTicks(page: Page): Promise<void> {
  await page.clock.runFor(START_AUTO_CHANGE_DELAY);
  await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
  await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
  await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
}

/** Moves the mouse to the middle of an element. */
async function hover(page: Page, element: Locator): Promise<void> {
  const box = (await element.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
}

/** Moves the mouse below the fixture's elements, outside every stepper. */
async function leave(page: Page, element: Locator): Promise<void> {
  const box = (await element.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height + 60);
}

for (const stepper of [
  { name: 'NumberField.Increment', testId: 'increment', steps: { 1: '1', 2: '2', 4: '4', 5: '5' }, boundaryCase: 'boundary', boundary: '10' },
  { name: 'NumberField.Decrement', testId: 'decrement', steps: { 1: '-1', 2: '-2', 4: '-4', 5: '-5' }, boundaryCase: 'boundary-down', boundary: '-10' },
] as const) {
  test.describe(`<${stepper.name} />`, () => {
    test('steps continuously when holding pointerdown', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await page.mouse.up();
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[4]);
      await expect(fixture).toHaveAttribute('data-changes', new RegExp(`/${stepper.testId}-press/${stepper.testId === 'increment' ? '1' : '-1'}$`));
    });

    test('does not step twice with pointerdown and click', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');

      await button.click();

      await expect(input).toHaveValue(stepper.steps[1]);
      await expect(fixture).toHaveAttribute('data-changes', new RegExp(`^${stepper.steps[1]}/${stepper.testId}-press/-?1$`));
    });

    test('stops calling onValueChange once the boundary is reached', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode, stepper.boundaryCase);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.boundary);
      await expect(fixture).toHaveAttribute('data-changes', `${stepper.boundary}/${stepper.testId}-press/${stepper.testId === 'increment' ? '1' : '-1'}`);

      await page.clock.runFor(START_AUTO_CHANGE_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.boundary);
      await expect(fixture).toHaveAttribute('data-changes', `${stepper.boundary}/${stepper.testId}-press/${stepper.testId === 'increment' ? '1' : '-1'}`);
      await expect(button).toBeDisabled();

      await page.mouse.up();
    });

    test('stops stepping after mouseleave', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await leave(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[4]);
      await page.mouse.up();
    });

    test('starts stepping again after mouseleave then mouseenter', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await leave(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await expect(input).toHaveValue(stepper.steps[4]);

      // Base UI ticks the clock 60 ms after the mouseenter; the re-entry's tick is immediate, so the value is one step on.
      await hover(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[5]);
      await page.mouse.up();
    });

    test('does not start stepping again after mouseleave then mouseenter after pointerup', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      await holdForThreeTicks(page);
      await expect(input).toHaveValue(stepper.steps[4]);

      await page.mouse.up();
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await expect(input).toHaveValue(stepper.steps[4]);

      await leave(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await expect(input).toHaveValue(stepper.steps[4]);

      await hover(page, button);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[4]);
      await expect(fixture).toHaveAttribute('data-changes', /^(\S+ ){3}\S+$/);
    });

    test('cancels an active mouse press-and-hold interaction when disabled', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await hover(page, button);
      await freezeClock(page);

      await page.mouse.down();
      await expect(input).toHaveValue(stepper.steps[1]);

      // Base UI's setProps({ disabled: true }); a key on the fixture's control, so the mouse stays pressed on the button.
      await fixture.locator('[data-set-disabled]').dispatchEvent('keydown', { key: 'Shift' });
      await expect(input).toBeDisabled();

      await page.clock.runFor(START_AUTO_CHANGE_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[1]);
      await expect(fixture).toHaveAttribute('data-changes', /^\S+$/);
      await page.mouse.up();
    });

    test('does not step when readOnly', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode, 'readonly');
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await expect(button).toHaveAttribute('aria-disabled', 'true');

      // A real press: Playwright waits for an `aria-disabled` element to enable, so the click is forced.
      await button.click({ force: true });

      await expect(input).toHaveValue('');
      await expect(fixture).toHaveAttribute('data-changes', '');
    });

    test('does not step when root is disabled', async ({ page, mode }) => {
      const fixture = await gotoHold(page, mode, 'disabled');
      const button = fixture.getByTestId(stepper.testId);
      const input = fixture.getByTestId('input');
      await expect(button).toBeDisabled();

      await button.dispatchEvent('click');

      await expect(input).toHaveValue('0');
      await expect(fixture).toHaveAttribute('data-changes', '');
    });
  });
}

test('places the caret at the end of the input when a mouse press focuses it', async ({ page, mode }) => {
  const fixture = await gotoHold(page, mode);
  const button = fixture.getByTestId('increment');
  const input = fixture.getByTestId('input');

  await hover(page, button);
  await page.mouse.down();

  await expect(input).toBeFocused();
  const { value, start, end } = await input.evaluate((element) => {
    const field = element as HTMLInputElement;
    return { value: field.value, start: field.selectionStart, end: field.selectionEnd };
  });
  expect(start).toBe(value.length);
  expect(end).toBe(value.length);
  await page.mouse.up();
});

// The touch and pen titles (NUM-PTR-01, NUM-PTR-02, NUM-PTR-06; UBT-PTR-16..19): useNumberFieldStepperButton.test.tsx
// "touch" and "press and hold", NumberFieldIncrement.test.tsx "press and hold". Each title opens a context with touch.
for (const stepper of [
  { name: 'NumberField.Increment', testId: 'increment', steps: { 1: '1', 2: '2', 3: '3' }, reason: 'increment-press' },
  { name: 'NumberField.Decrement', testId: 'decrement', steps: { 1: '-1', 2: '-2', 3: '-3' }, reason: 'decrement-press' },
] as const) {
  testWithTouch.describe(`<${stepper.name} /> touch`, () => {
    testWithTouch('treats pen pointer as touch-like', async ({ touchPage: page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const point = await pointOf(fixture.getByTestId(stepper.testId));
      await freezeClock(page);
      const cdp = await page.context().newCDPSession(page);
      try {
        await cdp.send('Input.dispatchMouseEvent', { type: 'mousePressed', ...point, button: 'left', clickCount: 1, pointerType: 'pen' });

        // A mouse press focuses the input; touch-like presses leave focus alone. Base UI's jsdom press focuses nothing, so
        // it reads document.body; a real press focuses the button natively, so here the input is not focused.
        await expect(fixture.getByTestId('input')).not.toBeFocused();
        await cdp.send('Input.dispatchMouseEvent', { type: 'mouseReleased', ...point, button: 'left', clickCount: 1, pointerType: 'pen' });
        await expect(fixture.getByTestId('input')).not.toBeFocused();
      } finally {
        await cdp.detach();
      }
    });

    // Base UI runs it twice, with the click before and after touchend (it.each); a real tap has the browser's one order.
    testWithTouch("port: always steps on quick touch, in the browser's order of touchend and click (before TOUCH_TIMEOUT)", async ({ touchPage: page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const point = await pointOf(fixture.getByTestId(stepper.testId));
      const input = fixture.getByTestId('input');
      await freezeClock(page);

      await touchDrag(page, [[point]]);
      await expect(input).toHaveValue(stepper.steps[1]);

      await touchDrag(page, [[point]]);
      await expect(input).toHaveValue(stepper.steps[2]);
      await expect(fixture).toHaveAttribute('data-changes', /^\S+ \S+$/);
    });

    testWithTouch('fires onValueCommitted once on first soft tap (touch)', async ({ touchPage: page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const point = await pointOf(fixture.getByTestId(stepper.testId));
      await freezeClock(page);

      await touchDrag(page, [[point]]);

      await expect(fixture.getByTestId('input')).toHaveValue(stepper.steps[1]);
      await expect(fixture).toHaveAttribute('data-commits', `${stepper.steps[1]}/${stepper.reason}`);
    });

    testWithTouch('starts the hold once a touch press settles and ignores the compatibility click', async ({ touchPage: page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const point = await pointOf(fixture.getByTestId(stepper.testId));
      const input = fixture.getByTestId('input');
      await freezeClock(page);

      await touchDrag(page, [[point]], {
        beforeRelease: async () => {
          await expect(input).toHaveValue('0');

          await page.clock.runFor(TOUCH_TIMEOUT);
          await expect(input).toHaveValue(stepper.steps[1]);

          await page.clock.runFor(START_AUTO_CHANGE_DELAY);
          await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
          await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
          await expect(input).toHaveValue(stepper.steps[3]);
        },
      });
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue(stepper.steps[3]);
      await expect(fixture).toHaveAttribute('data-changes', /^(\S+ ){2}\S+$/);
    });

    testWithTouch('does not start the hold when the touch moves like a scroll', async ({ touchPage: page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const point = await pointOf(fixture.getByTestId(stepper.testId), { x: 4, y: 4 });
      const input = fixture.getByTestId('input');
      await freezeClock(page);

      await touchDrag(page, [[point, { x: point.x, y: point.y + 20 }]], {
        beforeRelease: async () => {
          await page.clock.runFor(TOUCH_TIMEOUT);
          await page.clock.runFor(START_AUTO_CHANGE_DELAY);
          await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);
          await expect(input).toHaveValue('0');
        },
      });

      await expect(input).toHaveValue('0');
      await expect(fixture).toHaveAttribute('data-changes', '');
    });

    testWithTouch('treats a touch press with several small moves as a tap', async ({ touchPage: page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const point = await pointOf(fixture.getByTestId(stepper.testId), { x: 4, y: 4 });
      const input = fixture.getByTestId('input');
      await freezeClock(page);

      const moves = [1, 2, 3].map((dx) => ({ x: point.x + dx, y: point.y }));
      await touchDrag(page, [[point, ...moves]], {
        beforeRelease: async () => {
          await page.clock.runFor(TOUCH_TIMEOUT);
          await page.clock.runFor(START_AUTO_CHANGE_DELAY);
          await expect(input).toHaveValue('0');
        },
      });

      await expect(input).toHaveValue(stepper.steps[1]);
    });

    testWithTouch('cancels the compatibility click from a touch press when disabled', async ({ touchPage: page, mode }) => {
      const fixture = await gotoHold(page, mode);
      const point = await pointOf(fixture.getByTestId(stepper.testId));
      const input = fixture.getByTestId('input');
      await freezeClock(page);

      await touchDrag(page, [[point]], {
        beforeRelease: async () => {
          // Base UI's setProps({ disabled: true }) then setProps({ disabled: false }): keys on the fixture's control.
          await fixture.locator('[data-set-disabled]').dispatchEvent('keydown', { key: 'Shift' });
          await expect(input).toBeDisabled();
          await fixture.locator('[data-set-disabled]').dispatchEvent('keydown', { key: 'Control' });
          await expect(input).toBeEnabled();
        },
      });
      await page.clock.runFor(CHANGE_VALUE_TICK_DELAY);

      await expect(input).toHaveValue('0');
      await expect(fixture).toHaveAttribute('data-changes', '');
    });
  });
}

// NUM-STA-19's iOS inputmode under ADR 01's platform-flags row ("MNU-A11Y-01 and any attribute a part renders from the
// platform flags"; ADR 04 Amendments 2026-11-18, NumberField point 4 (a)): the root reads PlatformFlags from its first
// interactive render, so the input renders `numeric` until the interactions module answers. The module computes iOS from
// `navigator.platform` when it loads, so the init script sets it before any module runs (interactions.spec.ts's pattern).

/** Holds every request for a module until `release` is called; WebAssembly requests the fingerprinted file name. */
async function stall(page: Page, module: string): Promise<() => Promise<void>> {
  const held: Route[] = [];
  let released = false;
  const url = new RegExp(`/_content/Blazix\\.Primitives/js/${module}(\\.[a-z0-9]+)?\\.js$`);
  await page.route(url, (route) => (released ? route.continue() : held.push(route)));
  return async () => {
    released = true;
    for (const route of held.splice(0)) {
      await route.continue();
    }
  };
}

test.describe('platform flags (NUM-STA-19)', () => {
  for (const [platformName, expected] of [
    ['iPhone', 'text'],
    ['Win32', 'numeric'],
  ] as const) {
    test(`port: the input renders inputmode numeric until the platform flags are read, then ${expected} on ${platformName}`, async ({ page, mode }) => {
      await page.addInitScript((value) => {
        Object.defineProperty(Navigator.prototype, 'platform', { get: () => value, configurable: true });
        Object.defineProperty(Navigator.prototype, 'maxTouchPoints', { get: () => 0, configurable: true });
      }, platformName);
      const release = await stall(page, 'interactions');
      const fixture = await gotoHold(page, mode);
      const input = fixture.getByTestId('input');

      await expect(input).toHaveAttribute('inputmode', 'numeric');

      await release();
      await expect(input).toHaveAttribute('inputmode', expected);
    });
  }
});

// NUM-PTR-02 under ADR 01's row "C#-origin consumer vetoes" (ADR 04 Amendments 2026-11-18, NumberField point 1 (a), the
// `hold-start` point): a consumer onpointerdown that calls PreventPartHandler() runs inside the decision on WebAssembly,
// so the press does not step; on Server the decision falls back to run and the veto lands a round trip late, so the first
// step happens. The `veto` case puts that handler on the increment button.
test('port: a C# consumer veto of the stepper pointerdown keeps the press from stepping on WebAssembly and is late on Server', async ({ page, mode }) => {
  const fixture = await gotoHold(page, mode, 'veto');
  const button = fixture.getByTestId('increment');
  const input = fixture.getByTestId('input');
  await hover(page, button);
  await freezeClock(page);

  await page.mouse.down();
  await expect(fixture).toHaveAttribute('data-vetoes', '1');

  await expect(input).toHaveValue(mode === 'wasm' ? '0' : '1');
  await expect(fixture).toHaveAttribute('data-changes', mode === 'wasm' ? '' : /^1\/increment-press\/1$/);
  await page.mouse.up();
});

// The consumer-veto row's figures for NUM-PTR-02 (ADR 01, decision 8 of the M6 plan): from the stepper's `pointerdown` to
// the C# veto's record (`data-vetoes`) and, on Server, to the step it could not stop (`data-changes`), in the page's clock
// by a capture listener and a mutation observer. The median of 5 fresh renders per delay; Server under a 0, 50 and 150 ms
// one-way circuit delay, WebAssembly once (no circuit). Each figure is a test annotation and a `[figure]` log line.
for (const delay of [0, 50, 150]) {
  test(`port: the hold-start veto's timings at ${delay} ms one-way (NUM-PTR-02, ADR 01's consumer-veto row)`, async ({ page, mode }) => {
    test.skip(mode === 'wasm' && delay !== 0, 'No circuit to delay on WebAssembly');
    if (mode === 'server' && delay > 0) {
      await delayCircuit(page, delay);
    }

    const vetoes: number[] = [];
    const steps: number[] = [];
    for (let run = 0; run < 5; run += 1) {
      await page.clock.resume();
      const fixture = await gotoHold(page, mode, 'veto');
      const button = fixture.getByTestId('increment');
      await page.evaluate(() => {
        const record = { t0: -1, veto: -1, step: -1 };
        (window as unknown as { vetoFigure: typeof record }).vetoFigure = record;
        const root = document.querySelector('[data-fixture="number-field-hold"][data-interactive="true"]')!;
        document.addEventListener('pointerdown', (event) => {
          if (record.t0 < 0 && (event.target as Element | null)?.closest?.('[data-testid="increment"]')) {
            record.t0 = performance.now();
          }
        }, true);
        new MutationObserver(() => {
          if (record.t0 < 0) {
            return;
          }
          if (record.veto < 0 && root.getAttribute('data-vetoes') === '1') {
            record.veto = performance.now();
          }
          if (record.step < 0 && (root.getAttribute('data-changes') ?? '') !== '') {
            record.step = performance.now();
          }
        }).observe(root, { attributes: true });
      });
      await hover(page, button);
      await page.mouse.down();
      await expect(fixture).toHaveAttribute('data-vetoes', '1');
      if (mode === 'server') {
        await expect(fixture).toHaveAttribute('data-changes', /^1\/increment-press\/1$/);
      }
      await page.mouse.up();
      const record = await page.evaluate(() => (window as unknown as { vetoFigure: { t0: number; veto: number; step: number } }).vetoFigure);
      vetoes.push(record.veto - record.t0);
      if (mode === 'server') {
        steps.push(record.step - record.t0);
      }
    }

    const describe = (name: string, times: number[]) => {
      const sorted = [...times].sort((a, b) => a - b);
      return `${name} median ${sorted[2]!.toFixed(0)} ms (${sorted.map((t) => t.toFixed(0)).join(', ')})`;
    };
    const parts = [describe('veto', vetoes), ...(mode === 'server' ? [describe('step', steps)] : [])];
    const description = `NUM-PTR-02 ${mode} ${delay} ms: ${parts.join('; ')}`;
    test.info().annotations.push({ type: 'figure', description });
    console.log(`[figure] ${description}`);
  });
}
