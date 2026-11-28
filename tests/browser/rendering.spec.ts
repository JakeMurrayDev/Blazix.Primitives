import { test, expect, gotoFixture, type Page } from './helpers';

// The render pipeline's own L4 titles (src/Iodine.Headless/Rendering). Issue 87: a part's created handler is on its
// element only in a render that adds it, so on Server a key press reaches C# only when a consumer handler needs it
// (ADR 16 rule 6). The `button-custom` fixture's `enabled` Button has no consumer key handler; its `veto` Button has a
// consumer keydown and keyup.

/** The SignalR messages of one binary WebSocket frame, each with its VarInt length prefix (helpers/circuit.ts's split). */
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

/** Records the browser events the page dispatches to the circuit (`DispatchEventAsync`), by event name, delaying none. */
async function recordDispatchedEvents(page: Page): Promise<string[]> {
  const events: string[] = [];
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((frame) => {
      for (const message of typeof frame === 'string' ? [frame] : splitMessages(frame)) {
        const text = typeof message === 'string' ? message : message.toString('latin1');
        if (text.includes('DispatchEventAsync')) {
          events.push(/"eventName":"(\w+)"/.exec(text)?.[1] ?? 'unknown');
        }
      }
      server.send(frame);
    });
    server.onMessage((frame) => socket.send(frame));
  });
  return events;
}

test.describe('Rendering', () => {
  test('a key press reaches C# from a button with a consumer key handler, and none from a button without one', async ({ page, mode }) => {
    test.skip(mode === 'wasm', 'WebAssembly dispatches in process; the L2 title covers the absent handler.');
    const events = await recordDispatchedEvents(page);
    const fixture = await gotoFixture(page, mode, 'button-custom');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const settle = async () => {
      const before = Number(await fixture.getAttribute('data-sync'));
      await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
      await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
    };
    await settle();

    let mark = events.length;
    await fixture.locator('[data-case="veto"] [data-button]').press('a');
    await settle();
    expect(events.slice(mark).filter((name) => name === 'keydown' || name === 'keyup')).toEqual(['keydown', 'keyup']);

    mark = events.length;
    const plain = fixture.locator('[data-case="enabled"] [data-button]');
    await plain.press('a');
    await plain.press('Shift');
    await settle();
    expect(events.slice(mark).filter((name) => name === 'keydown' || name === 'keyup')).toEqual([]);
  });
});
