import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';
import { delayCircuit } from './helpers/circuit';

// The input role's [L4] rows with real keys and a real wheel (issue 277, 6b; NUM-KEY-01, 02, 07, 09, 10; NUM-PTR-07):
// NumberFieldInput.test.tsx's and NumberFieldRoot.test.tsx's titles on the `number-field-input` fixture, Server then
// WebAssembly. The `number-field` module gates and prevents in the dispatch; C# steps and answers the text.

async function gotoInput(page: Page, mode: RenderMode, inputCase: string) {
  await page.goto(`/${mode}/number-field-input?case=${inputCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-input'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  const input = fixture.getByRole('textbox');
  await input.focus();
  return { fixture, input };
}

test('should not allow non-numeric characters on keydown', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'empty');

  await page.keyboard.press('a');

  await expect(input).toHaveValue('');
});

test('should increment on keydown ArrowUp', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'basic');

  await page.keyboard.press('ArrowUp');

  await expect(input).toHaveValue('1');
});

test('should decrement on keydown ArrowDown', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'basic');

  await page.keyboard.press('ArrowDown');

  await expect(input).toHaveValue('-1');
});

test('should set the value to min on keydown Home', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'bounds');

  await page.keyboard.press('Home');

  await expect(input).toHaveValue('-10');
});

test('should set the value to max on keydown End', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'bounds');

  await page.keyboard.press('End');

  await expect(input).toHaveValue('10');
});

// Base UI dispatches keydowns and a change; here the keys are typed, so the browser inserts what the gate lets through.
test('applies locale-aware decimal/group gating (de-DE)', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'de');

  await page.keyboard.type('1,');
  await expect(input).toHaveValue('1,');

  await page.keyboard.press(',');
  await expect(input).toHaveValue('1,');

  await page.keyboard.type('..');
  await expect(input).toHaveValue('1,..');
});

test('should allow the user to scrub the input value with the mouse wheel', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'wheel');
  const box = (await input.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);

  await page.mouse.wheel(0, 10);
  await expect(input).toHaveValue('4');

  await page.mouse.wheel(0, -10);
  await expect(input).toHaveValue('5');
  await expect(fixture).toHaveAttribute('data-changes', '4/wheel 5/wheel');
});

// ADR 01's rows for NUM-KEY-01 and NUM-PTR-07 (decision 8 of the M6 plan): Base UI commits a key or wheel step inside the
// event; here the module prevents in the dispatch and C# commits a leg later. From the keydown or wheel to the commit's
// record (`data-commits`), in the page's clock; the median of 5 fresh renders per delay, Server under 0, 50 and 150 ms
// one-way, WebAssembly once. Each figure is a test annotation and a `[figure]` log line.
for (const [label, act] of [
  ['ArrowUp', async (page: Page) => page.keyboard.press('ArrowUp')],
  ['wheel', async (page: Page) => page.mouse.wheel(0, -10)],
] as const) {
  for (const delay of [0, 50, 150]) {
    test(`port: the ${label} step's commit timing at ${delay} ms one-way (ADR 01, NUM-${label === 'wheel' ? 'PTR-07' : 'KEY-01'})`, async ({ page, mode }) => {
      test.skip(mode === 'wasm' && delay !== 0, 'No circuit to delay on WebAssembly');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }

      const times: number[] = [];
      for (let run = 0; run < 5; run += 1) {
        const { fixture, input } = await gotoInput(page, mode, 'wheel');
        const box = (await input.boundingBox())!;
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await page.evaluate((eventType) => {
          const record = { t0: -1, t1: -1 };
          (window as unknown as { stepFigure: typeof record }).stepFigure = record;
          const root = document.querySelector('[data-fixture="number-field-input"][data-interactive="true"]')!;
          document.addEventListener(eventType, () => {
            if (record.t0 < 0) {
              record.t0 = performance.now();
            }
          }, true);
          new MutationObserver(() => {
            if (record.t0 >= 0 && record.t1 < 0 && (root.getAttribute('data-commits') ?? '') !== '') {
              record.t1 = performance.now();
            }
          }).observe(root, { attributes: true });
        }, label === 'wheel' ? 'wheel' : 'keydown');
        await act(page);
        await expect(fixture).toHaveAttribute('data-commits', /^6\//);
        const record = await page.evaluate(() => (window as unknown as { stepFigure: { t0: number; t1: number } }).stepFigure);
        times.push(record.t1 - record.t0);
      }

      const sorted = [...times].sort((a, b) => a - b);
      const description = `NUM-${label === 'wheel' ? 'PTR-07' : 'KEY-01'} ${mode} ${delay} ms: commit median ${sorted[2]!.toFixed(0)} ms (${sorted.map((t) => t.toFixed(0)).join(', ')})`;
      test.info().annotations.push({ type: 'figure', description });
      console.log(`[figure] ${description}`);
    });
  }
}
