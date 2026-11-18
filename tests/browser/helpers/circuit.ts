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

/**
 * Holds the Server circuit's messages to the page, in order, from the first one `from` matches after `arm()` until the page
 * sends a message `until` matches, or for at most `maxMs`: a server slower than the user's next input (issue 261).
 * - `armOn`: the gate is armed only once, after `arm()`, the page has sent a message `armOn` matches (the input that starts
 *   the server's work).
 * - `holdMatch`: whether the matching message itself is held (the default) or sent, holding only the messages after it.
 *
 * The server's frames are split into their messages, so a message the server flushed together with the one before it is held
 * as if it had been flushed alone. Messages to the server are not delayed. Call it before the page's first navigation; on
 * WebAssembly there is no circuit and `arm()` does nothing.
 */
export async function holdCircuit(
  page: Page,
  options: {
    from: (text: string) => boolean;
    armOn?: (text: string) => boolean;
    holdMatch?: boolean;
    until?: (text: string) => boolean;
    maxMs?: number;
  },
): Promise<{ arm: () => void; released: () => Promise<void> }> {
  const maxMs = options.maxMs ?? 1_000;
  let sent: Promise<void> = Promise.resolve();
  let requested = false;
  let armed = false;
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
      if (release !== null && options.until?.(text)) {
        release();
      }
      server.send(message);
    });
    server.onMessage((frame) => {
      for (const message of typeof frame === 'string' ? [frame] : splitMessages(frame)) {
        let wait = gate;
        if (armed && options.from(textOf(message))) {
          requested = false;
          armed = false;
          gate = new Promise<void>((resolve) => {
            const timer = setTimeout(() => release?.(), maxMs);
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
        chain = chain.then(() => wait).then(() => socket.send(message));
        sent = chain;
      }
    });
  });
  return {
    arm: () => {
      requested = true;
      armed = options.armOn === undefined;
    },
    // Resolves once the gate has opened and every message held so far has been handed to the page, then lets the page run
    // the tasks those messages queued (a page task after them).
    released: async () => {
      await gate;
      await sent;
      await page.evaluate(() => new Promise<void>((resolve) => setTimeout(resolve, 0)));
    },
  };
}
