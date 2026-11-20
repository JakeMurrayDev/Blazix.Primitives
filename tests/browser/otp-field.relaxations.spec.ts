import { test, expect, delayCircuit, type Page } from './helpers';
import { change, gotoOtp, paste, slotValues } from './otp-field.helpers';

// OTP §9 and ADR 01's relaxation table for OtpField (issue 278, M6 track 7; decision 8 of the M6 plan): the C#-origin
// consumer vetoes (OTP-KEY-01, KEY-13, KEY-19, PTR-02, FOC-08, EVT-16) per mode, the JavaScript-listener vetoes met on both,
// and the figures of the edit loop. A C# handler that calls PreventPartHandler() runs inside the decision on WebAssembly;
// on Server the decision falls back to run, the act happens and the veto lands a round trip late. Figures: the median of 5
// fresh renders per delay, Server under 0, 50 and 150 ms one-way (`delayCircuit`), WebAssembly once, in the page's clock
// (a capture listener for the start; a focus listener, a value setter, a mutation observer or a submit listener for the
// end); each is a test annotation and a `[figure]` log line.

test.describe('OtpField consumer vetoes', () => {
  test('port: a C# onkeydown veto keeps ArrowRight from moving focus on WebAssembly and is late on Server (OTP-KEY-01)', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234&veto=keydown');
    await field.slot(1).focus();

    await page.keyboard.press('ArrowRight');

    await expect(field.fixture).toHaveAttribute('data-vetoes', 'keydown:ArrowRight');
    await expect(field.slot(mode === 'wasm' ? 1 : 2)).toBeFocused();
  });

  // PreventPartHandler() vetoes the part's handling, not the browser's (ADR 02 Amendments 2026-10-21): the key is left to
  // the browser (OTP-KEY-01), whose own deletion of the selected character then reaches the module as an input that
  // clears the slot. Base UI's veto is preventDefault(), which also stops the browser.
  test('port: a C# onkeydown veto keeps Backspace from running its keyboard edit on WebAssembly and is late on Server (OTP-EVT-16)', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234&veto=keydown');
    await field.slot(1).focus();

    await page.keyboard.press('Backspace');

    await expect(field.fixture).toHaveAttribute('data-vetoes', 'keydown:Backspace');
    await expect(field.fixture).toHaveAttribute('data-changes', mode === 'wasm' ? '134:input-clear' : '134:keyboard');
  });

  test('port: a C# oninput veto restores the slot on WebAssembly and is late on Server (OTP-KEY-13)', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'veto=input');
    await field.slot(0).focus();

    await page.keyboard.type('1');

    await expect(field.fixture).toHaveAttribute('data-vetoes', 'input');
    if (mode === 'wasm') {
      await expect(field.slot(0)).toHaveValue('');
      await expect(field.slot(0)).toBeFocused();
      await expect(field.fixture).toHaveAttribute('data-changes', '');
    } else {
      await expect(field.fixture).toHaveAttribute('data-changes', '1:input-change');
    }
  });

  test('port: a C# onpaste veto keeps the paste from distributing on WebAssembly and is late on Server (OTP-KEY-19)', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'veto=paste');
    await field.slot(0).focus();

    await paste(field.slot(0), '1234');

    await expect(field.fixture).toHaveAttribute('data-vetoes', 'paste');
    if (mode === 'wasm') {
      expect(await slotValues(field)).toBe('______');
      await expect(field.fixture).toHaveAttribute('data-changes', '');
    } else {
      await expect(field.fixture).toHaveAttribute('data-changes', '1234:input-paste');
    }
  });

  // OTP-KEY-01, KEY-13, KEY-19, PTR-02: met on both modes from a consumer JavaScript listener, which the module sees as
  // `defaultPrevented` (the module listens at the root node, after the slot's own listeners).
  test('a consumer JavaScript listener that prevents a key, a paste or a mousedown leaves it to the browser on both modes', async ({ page, mode }) => {
    const field = await gotoOtp(page, mode, 'value=1234');
    await field.slot(1).evaluate((slot) => {
      for (const type of ['keydown', 'paste', 'mousedown']) {
        slot.addEventListener(type, (event) => event.preventDefault());
      }
    });
    await field.slot(0).focus();
    await field.slot(1).focus();

    await page.keyboard.press('ArrowRight');
    await expect(field.slot(1)).toBeFocused();
    await paste(field.slot(1), '99');
    await field.slot(0).focus();
    await field.slot(1).dispatchEvent('mousedown');

    await expect(field.slot(0)).toBeFocused();
    expect(await slotValues(field)).toBe('1234__');
    await expect(field.fixture).toHaveAttribute('data-changes', '');
  });
});

type Until = { focus: string } | { write: [string, string] } | { attribute: [string, string] } | { submit: true };

async function arm(page: Page, start: string, until: Until): Promise<void> {
  await page.evaluate(
    ([startType, end]) => {
      const record = { t0: -1, t1: -1 };
      (window as unknown as { otpFigure: typeof record }).otpFigure = record;
      const fixture = document.querySelector('[data-interactive="true"]')!;
      const done = () => {
        if (record.t0 >= 0 && record.t1 < 0) {
          record.t1 = performance.now();
        }
      };
      document.addEventListener(
        startType as string,
        () => {
          if (record.t0 < 0) {
            record.t0 = performance.now();
          }
        },
        true,
      );
      const target = end as Until;
      if ('focus' in target) {
        fixture.querySelector(`[data-testid="${target.focus}"]`)!.addEventListener('focus', done);
      } else if ('write' in target) {
        const [testId, value] = target.write;
        const input = fixture.querySelector(`[data-testid="${testId}"]`) as HTMLInputElement;
        const native = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!;
        Object.defineProperty(input, 'value', {
          configurable: true,
          get() {
            return native.get!.call(this);
          },
          set(next: string) {
            native.set!.call(this, next);
            if (next === value) {
              done();
            }
          },
        });
      } else if ('attribute' in target) {
        const [name, value] = target.attribute;
        new MutationObserver(() => {
          if (fixture.getAttribute(name) === value) {
            done();
          }
        }).observe(fixture, { attributes: true });
      } else {
        document.addEventListener('submit', done, true);
      }
    },
    [start, until] as const,
  );
}

const figureOf = (page: Page) => page.evaluate(() => (window as unknown as { otpFigure: { t0: number; t1: number } }).otpFigure);

interface Act {
  name: string;
  id: string;
  query: string;
  fixture?: string;
  start: string;
  until: Until;
  run(page: Page, field: Awaited<ReturnType<typeof gotoOtp>>): Promise<void>;
}

const acts: Act[] = [
  {
    name: 'typing on a built-in field (the optimistic advance)',
    id: 'OTP-FOC-10',
    query: '',
    start: 'keydown',
    until: { focus: 'slot-1' },
    run: async (page, field) => {
      await field.slot(0).focus();
      await page.keyboard.type('1');
    },
  },
  {
    name: 'typing on a custom-normalized field (focus follows the committed value)',
    id: 'OTP-FOC-10',
    query: 'fn=upper&type=alphanumeric',
    fixture: 'otp-field-normalize',
    start: 'keydown',
    until: { focus: 'slot-1' },
    run: async (page, field) => {
      await field.slot(0).focus();
      await page.keyboard.type('a');
    },
  },
  {
    name: 'paste distribution',
    id: 'OTP-KEY-18',
    query: '',
    start: 'paste',
    until: { write: ['slot-3', '4'] },
    run: async (_page, field) => {
      await field.slot(0).focus();
      await paste(field.slot(0), '1234');
    },
  },
  {
    name: 'Backspace distribution',
    id: 'OTP-KEY-15',
    query: 'value=1234',
    start: 'keydown',
    until: { write: ['slot-3', ''] },
    run: async (page, field) => {
      await field.slot(1).focus();
      await page.keyboard.press('Backspace');
    },
  },
  {
    name: 'onValueComplete',
    id: 'OTP-EVT-08',
    query: 'value=12345',
    start: 'keydown',
    until: { attribute: ['data-completes', '123456:input-change'] },
    run: async (page, field) => {
      await field.slot(5).focus();
      await page.keyboard.type('6');
    },
  },
  {
    name: 'autoSubmit',
    id: 'OTP-EVT-13',
    query: 'case=named',
    fixture: 'otp-field-autosubmit',
    start: 'input',
    until: { submit: true },
    run: async (_page, field) => {
      await change(field.slot(0), '123456');
    },
  },
];

const median = (times: number[]) => [...times].sort((a, b) => a - b)[2]!;

for (const act of acts) {
  for (const delay of [0, 50, 150]) {
    test(`port: the ${act.name} timings at ${delay} ms one-way (${act.id}, ADR 01)`, async ({ page, mode }) => {
      test.skip(mode === 'wasm' && delay !== 0, 'No circuit to delay on WebAssembly');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }

      const times: number[] = [];
      for (let run = 0; run < 5; run += 1) {
        const field = await gotoOtp(page, mode, act.query, act.fixture);
        await arm(page, act.start, act.until);
        await act.run(page, field);
        await expect.poll(async () => (await figureOf(page)).t1).toBeGreaterThan(0);
        const figure = await figureOf(page);
        times.push(figure.t1 - figure.t0);
      }

      const line = `${act.name} at ${delay} ms: median ${median(times).toFixed(0)} ms (${times.map((t) => t.toFixed(0)).join(', ')})`;
      test.info().annotations.push({ type: 'figure', description: `${mode} ${line}` });
      console.log(`[figure] ${mode} ${line}`);
    });
  }
}

// Decision 26 (a): the messages and bytes a typed six-digit code costs on Server, counted on the circuit's WebSocket from
// the first key to the completion's record; nothing is coalesced.
test('port: the messages and bytes of a typed six-digit code on Server (decision 26)', async ({ page, mode }) => {
  test.skip(mode !== 'server', 'Only Server has a circuit.');
  const counts = { counting: false, sent: 0, sentBytes: 0, received: 0, receivedBytes: 0 };
  page.on('websocket', (socket) => {
    socket.on('framesent', (frame) => {
      if (counts.counting) {
        counts.sent += 1;
        counts.sentBytes += typeof frame.payload === 'string' ? Buffer.byteLength(frame.payload) : frame.payload.length;
      }
    });
    socket.on('framereceived', (frame) => {
      if (counts.counting) {
        counts.received += 1;
        counts.receivedBytes += typeof frame.payload === 'string' ? Buffer.byteLength(frame.payload) : frame.payload.length;
      }
    });
  });
  const field = await gotoOtp(page, mode);
  await field.slot(0).focus();

  counts.counting = true;
  await page.keyboard.type('123456');
  await expect(field.fixture).toHaveAttribute('data-completes', '123456:input-change');
  await expect(field.hidden).toHaveValue('123456');
  counts.counting = false;

  const line = `a typed six-digit code: ${counts.sent} frames sent (${counts.sentBytes} bytes), ${counts.received} received (${counts.receivedBytes} bytes)`;
  test.info().annotations.push({ type: 'figure', description: line });
  console.log(`[figure] server ${line}`);
  expect(counts.sent).toBeGreaterThan(0);
});
