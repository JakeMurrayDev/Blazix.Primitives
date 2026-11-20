import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';
import { delayCircuit } from './helpers/circuit';

// ADR 01's rows for the input role's C#-origin consumer vetoes (NUM-KEY-11, NUM-EVT-14; ADR 04 Amendments 2026-11-18,
// NumberField point 1 (a), the `input-key` and `input-blur` points, fallback run) and for the paste's text after C#'s
// parse (NUM-EVT-16), on the `number-field-input` fixture, Server then WebAssembly (issue 277, 6b; decision 8 of the M6
// plan). A consumer onkeydown or onblur that calls PreventPartHandler() runs inside the decision on WebAssembly, so the
// key does not step and the blur is not the field's; on Server the decision falls back to run, the act happens and the
// veto lands a round trip late. Figures: the median of 5 fresh renders per delay, Server under 0, 50 and 150 ms one-way,
// WebAssembly once, in the page's clock (a capture listener and a mutation observer); each is a test annotation and a
// `[figure]` log line.

async function gotoInput(page: Page, mode: RenderMode, inputCase: string) {
  await page.goto(`/${mode}/number-field-input?case=${inputCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-input'));
  // The circuit may take longer than the expect timeout to become interactive under load (PR 308's run 44452252075).
  await fixture.waitFor({ state: 'attached' });
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  const input = fixture.getByRole('textbox');
  await input.focus();
  return { fixture, input };
}

test('port: a C# consumer veto of the keydown keeps ArrowUp from stepping on WebAssembly and is late on Server (NUM-KEY-11)', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'veto');

  await page.keyboard.press('ArrowUp');

  await expect(fixture).toHaveAttribute('data-vetoes', '1');
  await expect(input).toHaveValue(mode === 'wasm' ? '5' : '6');
  await expect(fixture).toHaveAttribute('data-changes', mode === 'wasm' ? '' : '6/keyboard');
});

test('port: a C# consumer veto of the blur keeps it from committing on WebAssembly and is late on Server (NUM-EVT-14)', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'veto');
  await input.fill('7');
  await expect(fixture).toHaveAttribute('data-changes', '7/input-change');

  await input.blur();

  await expect(fixture).toHaveAttribute('data-vetoes', '1');
  await expect(fixture).toHaveAttribute('data-commits', mode === 'wasm' ? '' : '7/input-blur');
});

const figure = (name: string, times: number[]) => {
  const sorted = [...times].sort((a, b) => a - b);
  return `${name} median ${sorted[2]!.toFixed(0)} ms (${sorted.map((t) => t.toFixed(0)).join(', ')})`;
};

for (const act of ['keydown', 'blur', 'paste'] as const) {
  for (const delay of [0, 50, 150]) {
    const id = act === 'keydown' ? 'NUM-KEY-11' : act === 'blur' ? 'NUM-EVT-14' : 'NUM-EVT-16';
    test(`port: the ${act} timings at ${delay} ms one-way (${id}, ADR 01)`, async ({ page, mode }) => {
      test.skip(mode === 'wasm' && delay !== 0, 'No circuit to delay on WebAssembly');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }

      const vetoes: number[] = [];
      const acts: number[] = [];
      for (let run = 0; run < 5; run += 1) {
        const { fixture, input } = await gotoInput(page, mode, act === 'paste' ? 'basic' : 'veto');
        if (act === 'blur') {
          await input.fill('7');
          await expect(fixture).toHaveAttribute('data-changes', '7/input-change');
        }
        await page.evaluate((eventType) => {
          const record = { t0: -1, veto: -1, act: -1 };
          (window as unknown as { actFigure: typeof record }).actFigure = record;
          const root = document.querySelector('[data-fixture="number-field-input"][data-interactive="true"]')!;
          const input = root.querySelector('input[type="text"]') as HTMLInputElement;
          const started = () => {
            if (record.t0 < 0) {
              record.t0 = performance.now();
            }
          };
          document.addEventListener(eventType === 'blur' ? 'focusout' : eventType, started, true);
          new MutationObserver(() => {
            if (record.t0 < 0) {
              return;
            }
            if (record.veto < 0 && root.getAttribute('data-vetoes') === '1') {
              record.veto = performance.now();
            }
            const acted = eventType === 'keydown' ? (root.getAttribute('data-changes') ?? '') !== ''
              : eventType === 'blur' ? (root.getAttribute('data-commits') ?? '') !== ''
              : input.value === '42';
            if (record.act < 0 && acted) {
              record.act = performance.now();
            }
          }).observe(root, { attributes: true, subtree: true, characterData: true });
          // The module writes the input's value as a property, which no mutation reports: an own setter on the input
          // records the write of the parsed text.
          if (eventType === 'paste') {
            const native = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!;
            Object.defineProperty(input, 'value', {
              configurable: true,
              get() {
                return native.get!.call(this);
              },
              set(next: string) {
                native.set!.call(this, next);
                if (record.t0 >= 0 && record.act < 0 && next === '42') {
                  record.act = performance.now();
                }
              },
            });
          }
        }, act);
        if (act === 'keydown') {
          await page.keyboard.press('ArrowUp');
          await expect(fixture).toHaveAttribute('data-vetoes', '1');
          if (mode === 'server') {
            await expect(fixture).toHaveAttribute('data-changes', '6/keyboard');
          }
        } else if (act === 'blur') {
          await input.blur();
          await expect(fixture).toHaveAttribute('data-vetoes', '1');
          if (mode === 'server') {
            await expect(fixture).toHaveAttribute('data-commits', '7/input-blur');
          }
        } else {
          await input.evaluate((element: HTMLInputElement) => {
            element.select();
            const data = new DataTransfer();
            data.setData('text/plain', '42');
            element.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data }));
          });
          await expect(input).toHaveValue('42');
        }
        if (act === 'paste' || mode === 'server') {
          await expect.poll(() => page.evaluate(() => (window as unknown as { actFigure: { act: number } }).actFigure.act)).toBeGreaterThan(0);
        }
        const record = await page.evaluate(() => (window as unknown as { actFigure: { t0: number; veto: number; act: number } }).actFigure);
        if (act !== 'paste') {
          vetoes.push(record.veto - record.t0);
        }
        if (act === 'paste' || mode === 'server') {
          acts.push(record.act - record.t0);
        }
      }

      const parts = [
        ...(act !== 'paste' ? [figure('veto', vetoes)] : []),
        ...(act === 'paste' || mode === 'server' ? [figure(act === 'paste' ? 'text' : act === 'blur' ? 'commit' : 'step', acts)] : []),
      ];
      const description = `${id} ${mode} ${delay} ms: ${parts.join('; ')}`;
      test.info().annotations.push({ type: 'figure', description });
      console.log(`[figure] ${description}`);
    });
  }
}
