import type { Page } from '@playwright/test';

/**
 * Adds a one-way delay to the Blazor Server circuit's WebSocket in each direction (ADR 06 §2: CDP network emulation
 * does not delay it). Messages keep their order in each direction.
 */
export async function delayCircuit(page: Page, oneWayMs: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), oneWayMs);
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), oneWayMs);
    });
  });
}

/**
 * `delayCircuit` with an extra delay on the client's messages that contain `marker` (a circuit method's name, such as a
 * Combobox input's `OnInputChange`), so the server handles them that much later while the rest of the circuit (render
 * acknowledgements, interop results) flows. The marked messages keep their order among themselves, as do the others. Call
 * it before the page's first navigation.
 */
export async function delayCircuitMessages(page: Page, oneWayMs: number, marker: string, extraMs: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      const text = typeof message === 'string' ? message : message.toString('latin1');
      setTimeout(() => server.send(message), oneWayMs + (text.includes(marker) ? extraMs : 0));
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), oneWayMs);
    });
  });
}

/** The SignalR messages of one binary WebSocket frame, each with its VarInt length prefix. */
function splitMessages(frame: Buffer): Buffer[] {
  const messages: Buffer[] = [];
  let offset = 0;
  while (offset < frame.length) {
    let length = 0;
    let shift = 0;
    let cursor = offset;
    let byte: number;
    do {
      byte = frame[cursor++]!;
      length |= (byte & 0x7f) << shift;
      shift += 7;
    } while (byte & 0x80 && cursor < frame.length);
    messages.push(frame.subarray(offset, cursor + length));
    offset = cursor + length;
  }
  return messages;
}

const textOf = (message: string | Buffer): string => (typeof message === 'string' ? message : message.toString('latin1'));

/** A hold on the Server circuit's messages to the page (`holdCircuit`). */
export interface CircuitHold {
  /** Arms the gate: the next message `from` matches (after `armOn`'s, when given) opens it. */
  arm(): void;
  /**
   * Releases the gate now, as a server that answers at this point would, and waits as `released` does. A title that waits
   * for the server's answer before its next input calls it first.
   */
  release(): Promise<void>;
  /**
   * Waits until the gate has released every message it held and the page has run the tasks they queued (a page task after
   * them). Fails when the gate never opened, or when nothing released it (`until`, `release()`) within `maxMs` of the gate.
   */
  released(): Promise<void>;
}

/**
 * Holds the Server circuit's messages to the page, in order, from the first one `from` matches after `arm()` until the page
 * sends a message `until` matches or the title calls `release()`: a server slower than the user's next input (issue 261).
 * A hold always ends one of those two ways; there is no release on a timer.
 * - `armOn`: the gate is armed only once, after `arm()`, the page has sent a message `armOn` matches (the input that starts
 *   the server's work).
 * - `holdMatch`: whether the matching message itself is held (the default) or sent, holding only the messages after it.
 * - `until`: the page's message that ends the hold, when the input the hold races reaches C#. A key a module stops before
 *   Blazor's event delegation sends nothing; the title then ends the hold with `release()` once it has pressed the key.
 * - `maxMs`: a ceiling, never a wait. When nothing ends the hold within it, the messages are let go so the page can finish,
 *   and `released()` (or `release()`) fails the title: a hold that ended on the ceiling proves nothing.
 *
 * An `until` match that comes before the gate opens means the input already beat the server, and the gate then holds
 * nothing. The server's frames are split into their messages, so a message the server flushed together with the one before
 * it is held as if it had been flushed alone. Messages to the server are not delayed. Call it before the page's first
 * navigation; on WebAssembly there is no circuit and the gate never opens, so a title calls `released()` on Server only.
 */
export async function holdCircuit(
  page: Page,
  options: {
    from: (text: string) => boolean;
    until?: (text: string) => boolean;
    armOn?: (text: string) => boolean;
    holdMatch?: boolean;
    maxMs?: number;
  },
): Promise<CircuitHold> {
  const maxMs = options.maxMs ?? 5_000;
  let requested = false;
  let armed = false;
  let opened = false;
  let untilBeforeGate = false;
  let failure: Error | null = null;
  let release: (() => void) | null = null;
  let gate: Promise<void> = Promise.resolve();
  let chain: Promise<void> = Promise.resolve();
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      const text = textOf(message);
      if (requested && !armed && options.armOn?.(text)) {
        armed = true;
      }
      if (options.until?.(text)) {
        if (release !== null) {
          release();
        } else if (armed) {
          untilBeforeGate = true;
        }
      }
      server.send(message);
    });
    server.onMessage((frame) => {
      for (const message of typeof frame === 'string' ? [frame] : splitMessages(frame)) {
        let wait = gate;
        if (armed && options.from(textOf(message))) {
          requested = false;
          armed = false;
          opened = true;
          if (!untilBeforeGate) {
            gate = new Promise<void>((resolve) => {
              const timer = setTimeout(() => {
                failure = new Error(`holdCircuit: nothing ended the hold (\`until\`, \`release()\`) within ${maxMs} ms of the gate`);
                release?.();
              }, maxMs);
              release = () => {
                clearTimeout(timer);
                release = null;
                resolve();
              };
            });
            if (options.holdMatch !== false) {
              wait = gate;
            }
          }
        }
        chain = chain.then(() => wait).then(() => socket.send(message));
      }
    });
  });
  const released = async (): Promise<void> => {
    if (!opened) {
      throw new Error('holdCircuit: the gate never opened (no message to the page matched `from` after `arm()`)');
    }
    await gate;
    await chain;
    if (failure) {
      throw failure;
    }
    await page.evaluate(() => new Promise<void>((resolve) => setTimeout(resolve, 0)));
  };
  return {
    arm: () => {
      requested = true;
      armed = options.armOn === undefined;
    },
    release: async () => {
      release?.();
      await released();
    },
    released,
  };
}
