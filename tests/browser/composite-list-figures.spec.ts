import { type Locator, type Page } from '@playwright/test';
import { expect, test, type RenderMode } from './helpers';
import { gotoPopupFixture, openChanges, pressInPage, settle, type PopupFixtureOptions } from './suites/popupFixture';

// List navigation's figures (M5 track 1, issue 159) on `composite-list-measure`, the test-only list host with N items
// whose elements are `Render` substitutes:
// - the measurement fixture (Deliverable 13; ADR 14 Amendments 2026-11-03): the messages and bytes per held arrow key
//   (20 repeats) and per typeahead burst (6 characters) at N = 100 and 500, the input to ADR 16 §B's `[JSImport]`
//   re-check. Server: the circuit's WebSocket frames, both ways, through `page.routeWebSocket`; WebAssembly: the
//   JavaScript-to-.NET interop calls (Blazor's `DotNet.DotNetObject` methods and `DotNet`'s static ones, wrapped in the
//   page; .NET-to-JavaScript calls are bound at startup, so the page cannot count them, and render batches are read from
//   the runtime's memory). Each figure is the run minus a barrier measured the same way.
// - CMP's ADR 01 rows (Deliverable 14): each relaxation per mode, Server under a 75 ms one-way delay (a 150 ms round
//   trip, the M4 popup rows' figure), each a titled test that asserts an upper bound and logs its figure. Times are the
//   page's `performance.now()`, witnessed by mutation records and capture listeners, never by frame polls.

const ONE_WAY_MS = 75;

interface Frame {
  up: boolean;
  bytes: number;
  kinds: string[];
}

const KINDS = [
  'OnRenderCompleted',
  'EndInvokeJSFromDotNet',
  'DispatchEventAsync',
  'OnListHighlight',
  'OnKeyHandled',
  'OnItemsChange',
  'OnListMatch',
  'OnListOpenRequest',
  'OnInteractionTypeChange',
  'BeginInvokeDotNetFromJS',
  'JS.RenderBatch',
  'JS.EndInvokeDotNet',
  'JS.BeginInvokeJS',
];

function kindOf(text: string): string {
  return KINDS.find((kind) => text.includes(kind)) ?? (text.length <= 3 ? 'ping' : 'other');
}

// One WebSocket frame may carry several SignalR messages: the MessagePack hub protocol (Blazor's `blazorpack`) prefixes
// each with its length as a variable-length integer; the JSON protocol ends each with the record separator.
function messagesOf(message: string | Buffer): string[] {
  if (typeof message === 'string') {
    return message.split('\u001e').filter((part) => part.length > 0);
  }
  const parts: string[] = [];
  let offset = 0;
  while (offset < message.length) {
    let length = 0;
    let shift = 0;
    let byte: number;
    do {
      byte = message[offset]!;
      offset += 1;
      length |= (byte & 0x7f) << shift;
      shift += 7;
    } while (byte & 0x80 && offset < message.length);
    parts.push(message.subarray(offset, offset + length).toString('latin1'));
    offset += length;
  }
  return parts;
}

/** Counts the circuit's frames and messages both ways, optionally delaying each one-way leg, from before the page loads. */
async function watchCircuit(page: Page, oneWayMs = 0): Promise<Frame[]> {
  const frames: Frame[] = [];
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    const forward = (message: string | Buffer, up: boolean, send: (message: string | Buffer) => void) => {
      frames.push({ up, bytes: typeof message === 'string' ? Buffer.byteLength(message) : message.length, kinds: messagesOf(message).map(kindOf) });
      if (oneWayMs > 0) {
        setTimeout(() => send(message), oneWayMs);
      } else {
        send(message);
      }
    };
    socket.onMessage((message) => forward(message, true, (m) => server.send(m)));
    server.onMessage((message) => forward(message, false, (m) => socket.send(m)));
  });
  return frames;
}

/** Wraps the page's JavaScript-to-.NET interop calls (WebAssembly) and records each with its string arguments' size. */
async function watchInterop(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as { __interop: { method: string; bytes: number }[]; DotNet: Record<string, unknown> & { DotNetObject: { prototype: Record<string, unknown> } } };
    w.__interop = [];
    const wrap = (owner: Record<string, unknown>, name: string, methodArgument: number) => {
      const original = owner[name] as (...args: unknown[]) => unknown;
      owner[name] = function (this: unknown, ...args: unknown[]) {
        const bytes = args.reduce<number>((sum, arg) => sum + (typeof arg === 'string' ? new TextEncoder().encode(arg).length : 0), 0);
        w.__interop.push({ method: String(args[methodArgument]), bytes });
        return original.apply(this, args);
      };
    };
    wrap(w.DotNet.DotNetObject.prototype, 'invokeMethodAsync', 0);
    wrap(w.DotNet.DotNetObject.prototype, 'invokeMethod', 0);
    wrap(w.DotNet, 'invokeMethodAsync', 1);
    wrap(w.DotNet, 'invokeMethod', 1);
  });
}

interface Tally {
  /** Messages and their bytes, JavaScript to .NET (frames on Server, interop calls on WebAssembly). */
  up: number;
  upBytes: number;
  /** Messages and their bytes, .NET to JavaScript (Server only). */
  down: number;
  downBytes: number;
  /** WebSocket frames each way (Server only): SignalR batches messages into frames. */
  upFrames: number;
  downFrames: number;
  kinds: Record<string, number>;
}

const emptyTally = (): Tally => ({ up: 0, upBytes: 0, down: 0, downBytes: 0, upFrames: 0, downFrames: 0, kinds: {} });

function tallyFrames(frames: readonly Frame[]): Tally {
  const tally = emptyTally();
  for (const frame of frames) {
    const kinds = frame.kinds.filter((kind) => kind !== 'ping');
    if (kinds.length === 0) {
      continue;
    }
    if (frame.up) {
      tally.upFrames += 1;
      tally.up += kinds.length;
      tally.upBytes += frame.bytes;
    } else {
      tally.downFrames += 1;
      tally.down += kinds.length;
      tally.downBytes += frame.bytes;
    }
    for (const kind of kinds) {
      const key = `${frame.up ? 'up' : 'down'} ${kind}`;
      tally.kinds[key] = (tally.kinds[key] ?? 0) + 1;
    }
  }
  return tally;
}

async function tallyInterop(page: Page): Promise<Tally> {
  const calls = await page.evaluate(() => (window as unknown as { __interop: { method: string; bytes: number }[] }).__interop.splice(0));
  const tally = emptyTally();
  for (const call of calls) {
    tally.up += 1;
    tally.upBytes += call.bytes;
    tally.kinds[`up ${call.method}`] = (tally.kinds[`up ${call.method}`] ?? 0) + 1;
  }
  return tally;
}

function minus(run: Tally, barrier: Tally): Tally {
  const kinds: Record<string, number> = { ...run.kinds };
  for (const [kind, count] of Object.entries(barrier.kinds)) {
    kinds[kind] = (kinds[kind] ?? 0) - count;
    if (kinds[kind] === 0) {
      delete kinds[kind];
    }
  }
  return {
    up: run.up - barrier.up,
    upBytes: run.upBytes - barrier.upBytes,
    down: run.down - barrier.down,
    downBytes: run.downBytes - barrier.downBytes,
    upFrames: run.upFrames - barrier.upFrames,
    downFrames: run.downFrames - barrier.downFrames,
    kinds,
  };
}

/** Measures what `action` sends, up to `until`, minus a barrier measured the same way. */
async function measure(page: Page, mode: RenderMode, fixture: Locator, frames: Frame[], action: () => Promise<void>, until: () => Promise<void>): Promise<Tally> {
  const read = async (): Promise<Tally> => (mode === 'server' ? tallyFrames(frames.splice(0)) : tallyInterop(page));
  await settle(fixture);
  await read();
  await settle(fixture);
  const barrier = await read();
  await action();
  await until();
  await settle(fixture);
  return minus(await read(), barrier);
}

const item = (page: Page, index: number) => page.getByRole('menuitem', { name: `Item ${index}`, exact: true, includeHidden: true });

async function gotoMeasure(page: Page, mode: RenderMode, options: PopupFixtureOptions, oneWayMs = 0): Promise<{ fixture: Locator; frames: Frame[] }> {
  const frames = mode === 'server' ? await watchCircuit(page, oneWayMs) : [];
  const fixture = await gotoPopupFixture(page, mode, 'composite-list-measure', options);
  if (mode === 'wasm') {
    await watchInterop(page);
  }
  return { fixture, frames };
}

/** Opens the list by ArrowDown and moves once, so the first key's one-time reports are out of the figures. */
async function openByKeyboard(page: Page): Promise<void> {
  await page.getByTestId('trigger').focus();
  await page.keyboard.press('ArrowDown');
  await expect(item(page, 0)).toHaveAttribute('data-mirrored-highlighted', '');
  // The first item takes the focus once the floating handle attached after the portal's move.
  await expect(item(page, 0)).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(item(page, 1)).toHaveAttribute('data-mirrored-highlighted', '');
  await expect(item(page, 1)).toBeFocused();
}

function perUnit(tally: Tally, units: number): { up: number; upBytes: number; down: number; downBytes: number } {
  const round = (value: number) => Math.round((value / units) * 10) / 10;
  return { up: round(tally.up), upBytes: round(tally.upBytes), down: round(tally.down), downBytes: round(tally.downBytes) };
}

function logFigure(name: string, figure: unknown): void {
  const text = JSON.stringify(figure);
  test.info().annotations.push({ type: 'figure', description: `${name}: ${text}` });
  console.log(`[figure] ${test.info().project.name} ${name}: ${text}`);
}

test.describe('measurement fixture', () => {
  for (const n of [100, 500]) {
    test(`port: the messages and bytes per held arrow key and per typeahead burst at ${n} items`, async ({ page, mode }) => {
      const { fixture, frames } = await gotoMeasure(page, mode, { n });
      await openByKeyboard(page);

      // A held key: Playwright's down on a pressed key is a repeat keydown.
      const held = await measure(page, mode, fixture, frames, async () => {
        for (let i = 0; i < 20; i += 1) {
          await page.keyboard.down('ArrowDown');
        }
        await page.keyboard.up('ArrowDown');
      }, async () => {
        await expect(item(page, 21)).toHaveAttribute('data-mirrored-highlighted', '');
      });

      // A burst: six characters typed into the open list. From "Item 21", "I" moves to "Item 22", which the next four keep,
      // and "Item 9" matches "Item 90", the next label with that prefix.
      const burst = await measure(page, mode, fixture, frames, async () => {
        await page.keyboard.type('Item 9');
      }, async () => {
        await expect(item(page, 90)).toHaveAttribute('data-mirrored-highlighted', '');
      });

      logFigure(`held ArrowDown at ${n} items, per key`, { ...perUnit(held, 20), total: held });
      logFigure(`typeahead burst at ${n} items, per burst`, burst);
      expect(perUnit(held, 20).up).toBeLessThanOrEqual(8);
      expect(perUnit(held, 20).down).toBeLessThanOrEqual(8);
    });
  }
});

/** A condition the page witnesses: an element (by selector and index) with an attribute, an attribute value, focus or a count. */
interface Condition {
  name: string;
  selector: string;
  index?: number;
  attribute?: string;
  value?: string;
  includes?: string;
  focused?: boolean;
  count?: number;
  /** The element exists outside this ancestor (a portaled node after the portal's move). */
  outside?: string;
}

const menuItemAt = (index: number, attribute: string): Omit<Condition, 'name'> => ({ selector: '[role="menuitem"]', index, attribute });
const container = '[data-fixture="composite-list-measure"]';

/** Records, from the page, the first time each condition held: a mutation record or a capture listener witnesses it. */
async function startWitness(page: Page, conditions: readonly Condition[]): Promise<void> {
  await page.evaluate((list) => {
    const w = window as unknown as { __t: Record<string, number>; __witness?: MutationObserver };
    w.__t = {};
    const holds = (condition: (typeof list)[number]): boolean => {
      const all = document.querySelectorAll(condition.selector);
      if (condition.count !== undefined) {
        return all.length === condition.count;
      }
      const element = all[condition.index ?? 0];
      if (!element) {
        return false;
      }
      if (condition.outside !== undefined) {
        return !element.closest(condition.outside);
      }
      if (condition.focused) {
        return document.activeElement === element;
      }
      const value = element.getAttribute(condition.attribute!);
      if (condition.includes !== undefined) {
        return (value ?? '').includes(condition.includes);
      }
      return condition.value === undefined ? value !== null : value === condition.value;
    };
    const check = () => {
      const now = performance.now();
      for (const condition of list) {
        if (w.__t[condition.name] === undefined && holds(condition)) {
          w.__t[condition.name] = now;
        }
      }
    };
    w.__witness?.disconnect();
    w.__witness = new MutationObserver(check);
    w.__witness.observe(document, { subtree: true, childList: true, attributes: true, characterData: true });
    document.addEventListener('focusin', check, true);
    window.addEventListener('keydown', () => (w.__t.keydown ??= performance.now()), true);
  }, conditions as Condition[]);
}

async function times(page: Page): Promise<Record<string, number>> {
  return page.evaluate(() => (window as unknown as { __t: Record<string, number> }).__t);
}

async function markStart(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as { __t: Record<string, number> };
    w.__t.start = performance.now();
  });
}

const since = (t: Record<string, number>, from: string, to: string) => Math.round(t[to]! - t[from]!);

test.describe("ADR 01's list rows", () => {
  // The highlight's writer is JavaScript's; C#'s `Highlighted` (a mirrored field) follows the report (RND-STA-01's row:
  // CMP-STA-31, CMP-DOM-04, CMP-EVT-04).
  test("port: the Highlighted mirror trails the module's data-highlighted by the report's leg and C#'s render", async ({ page, mode }) => {
    await gotoMeasure(page, mode, { n: 100 }, mode === 'server' ? ONE_WAY_MS : 0);
    await openByKeyboard(page);
    await startWitness(page, [
      { name: 'written', ...menuItemAt(2, 'data-highlighted') },
      { name: 'mirrored', ...menuItemAt(2, 'data-mirrored-highlighted') },
    ]);

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 2)).toHaveAttribute('data-mirrored-highlighted', '');

    const t = await times(page);
    const figure = { written: since(t, 'keydown', 'written'), mirrored: since(t, 'keydown', 'mirrored'), lag: since(t, 'written', 'mirrored') };
    logFigure('Highlighted mirror lag', figure);
    expect(figure.written).toBeLessThan(50);
    expect(figure.lag).toBeLessThan(mode === 'server' ? 2 * ONE_WAY_MS + 400 : 200);
  });

  // ADR 01's "JS-owned values that originate in C# state" row: a C#-origin highlight is sent at once with a revision.
  test('port: a C#-origin highlight request reaches the module one leg after C# makes it', async ({ page, mode }) => {
    const { fixture } = await gotoMeasure(page, mode, { n: 100, highlight: 5 }, mode === 'server' ? ONE_WAY_MS : 0);
    await openByKeyboard(page);
    await startWitness(page, [
      { name: 'written', ...menuItemAt(5, 'data-highlighted') },
      { name: 'mirrored', ...menuItemAt(5, 'data-mirrored-highlighted') },
    ]);

    await markStart(page);
    await pressInPage(fixture.locator('[data-highlight-from-csharp]'));
    await expect(item(page, 5)).toHaveAttribute('data-mirrored-highlighted', '');
    await expect(item(page, 5)).toBeFocused();

    const t = await times(page);
    const figure = { written: since(t, 'start', 'written'), mirrored: since(t, 'start', 'mirrored') };
    logFigure('C#-origin highlight request', figure);
    expect(figure.written).toBeLessThan(mode === 'server' ? 2 * ONE_WAY_MS + 400 : 200);
  });

  // CMP-STA-37, CMP-KEY-21: the open by an arrow key is C#'s (a round trip on Server), then the floating handle attaches
  // after the portal's move and highlights the first item.
  test('port: the open by ArrowDown highlights the first item after the open round trip and the floating attach', async ({ page, mode }) => {
    await gotoMeasure(page, mode, { n: 100 }, mode === 'server' ? ONE_WAY_MS : 0);
    await startWitness(page, [
      { name: 'expanded', selector: '[data-testid="trigger"]', attribute: 'aria-expanded', value: 'true' },
      { name: 'rendered', selector: '[role="menuitem"]', attribute: 'role' },
      { name: 'moved', selector: '[data-testid="popup"]', outside: container },
      { name: 'focusManaged', selector: '[data-handle-attached]', attribute: 'data-handle-attached' },
      { name: 'highlighted', ...menuItemAt(0, 'data-highlighted') },
      { name: 'focused', selector: '[role="menuitem"]', focused: true },
    ]);
    await page.getByTestId('trigger').focus();

    await page.keyboard.press('ArrowDown');
    await expect(item(page, 0)).toBeFocused();
    await expect(item(page, 0)).toHaveAttribute('data-highlighted', '');

    const t = await times(page);
    const figure = {
      expanded: since(t, 'keydown', 'expanded'),
      rendered: since(t, 'keydown', 'rendered'),
      moved: since(t, 'keydown', 'moved'),
      focusManaged: since(t, 'keydown', 'focusManaged'),
      highlighted: since(t, 'keydown', 'highlighted'),
      attach: since(t, 'rendered', 'highlighted'),
    };
    logFigure('open by ArrowDown to the first item highlighted', figure);
    expect(figure.highlighted).toBeLessThan(mode === 'server' ? 6 * ONE_WAY_MS + 600 : 300);
  });

  // CMP-STA-06, STA-07, STA-17 into the CMP-STA-10 row: an item C# adds registers in the render that creates it, the
  // root sends the registration from its after-render, and the module reports the new order.
  test("port: an item added by a re-render reaches the module's order report one leg after its render", async ({ page, mode }) => {
    const { fixture } = await gotoMeasure(page, mode, { n: 100, reportItems: true }, mode === 'server' ? ONE_WAY_MS : 0);
    await page.getByTestId('trigger').click();
    await expect(fixture).toHaveAttribute('data-order', '100');
    await page.evaluate(() => {
      const w = window as unknown as { __t: Record<string, number>; DotNet: { DotNetObject: { prototype: Record<string, unknown> } } };
      const prototype = w.DotNet.DotNetObject.prototype;
      const original = prototype.invokeMethodAsync as (...args: unknown[]) => unknown;
      prototype.invokeMethodAsync = function (this: unknown, ...args: unknown[]) {
        if (args[0] === 'OnItemsChange') {
          w.__t.reported ??= performance.now();
        }
        return original.apply(this, args);
      };
    });
    await startWitness(page, [
      { name: 'rendered', selector: '[role="menuitem"]', count: 101 },
      { name: 'mirrored', selector: container, attribute: 'data-order', value: '101' },
    ]);

    await markStart(page);
    await pressInPage(fixture.locator('[data-add-item]'));
    await expect(fixture).toHaveAttribute('data-order', '101');

    const t = await times(page);
    const figure = { rendered: since(t, 'start', 'rendered'), reported: since(t, 'start', 'reported'), afterRender: since(t, 'rendered', 'reported'), csharp: since(t, 'start', 'mirrored') };
    logFigure('registry change after a re-render to the order report', figure);
    expect(figure.afterRender).toBeLessThan(mode === 'server' ? 2 * ONE_WAY_MS + 400 : 200);
  });

  // A closed list's typeahead match (CMP-EVT-10): the module matches in the keydown and reports the index; the family's
  // value request starts when C# has it.
  test("port: a closed list's typeahead match reaches C# one leg after the key", async ({ page, mode }) => {
    const { fixture } = await gotoMeasure(page, mode, { n: 100, labels: true }, mode === 'server' ? ONE_WAY_MS : 0);
    await startWitness(page, [{ name: 'matched', selector: container, attribute: 'data-matches', includes: '0' }]);
    await page.getByTestId('trigger').focus();

    await page.keyboard.press('I');
    await expect(fixture).not.toHaveAttribute('data-matches', '');

    const t = await times(page);
    const figure = { csharp: since(t, 'keydown', 'matched'), index: await fixture.getAttribute('data-matches') };
    logFigure("closed list's typeahead match to C#", figure);
    await expect(page.getByTestId('trigger')).toHaveAttribute('aria-expanded', 'false');
    expect(figure.csharp).toBeLessThan(mode === 'server' ? 2 * ONE_WAY_MS + 400 : 200);
  });

  // ADR 01's "C#-origin consumer vetoes" row, the decision point `navigate` (ADR 04 Amendments 2026-10-22, option (i)):
  // on WebAssembly the consumer's `onkeydown` runs inside the keydown and its PreventPartHandler() keeps the highlight;
  // on Server the module moves first and the handler runs a round trip later, where the veto cannot apply.
  test("port: navigate: a consumer's veto of an arrow key applies on WebAssembly and cannot apply on Server, where the handler runs a leg after the move", async ({ page, mode }) => {
    const { fixture } = await gotoMeasure(page, mode, { n: 100, veto: true }, mode === 'server' ? ONE_WAY_MS : 0);
    await page.getByTestId('trigger').click();
    await expect(page.getByRole('menu')).toBeFocused();
    await page.keyboard.press('Home');
    await expect(fixture).toHaveAttribute('data-vetoes', '1');
    if (mode === 'server') {
      await expect(item(page, 0)).toHaveAttribute('data-highlighted', '');
    } else {
      await expect(page.locator('[role="menuitem"][data-highlighted]')).toHaveCount(0);
    }
    await startWitness(page, [
      { name: 'moved', ...menuItemAt(1, 'data-highlighted') },
      { name: 'vetoed', selector: container, attribute: 'data-vetoes', value: '2' },
    ]);

    await page.keyboard.press('ArrowDown');
    await expect(fixture).toHaveAttribute('data-vetoes', '2');
    await settle(fixture);

    const t = await times(page);
    if (mode === 'server') {
      await expect(item(page, 1)).toHaveAttribute('data-highlighted', '');
      const figure = { moved: since(t, 'keydown', 'moved'), handler: since(t, 'keydown', 'vetoed') };
      logFigure('navigate: the consumer handler after the move (Server)', figure);
      expect(figure.moved).toBeLessThan(50);
      expect(figure.handler).toBeLessThan(2 * ONE_WAY_MS + 400);
    } else {
      await expect(page.locator('[role="menuitem"][data-highlighted]')).toHaveCount(0);
      const figure = { handler: since(t, 'keydown', 'vetoed') };
      logFigure('navigate: the consumer handler in the keydown (WebAssembly)', figure);
      expect(t.moved).toBeUndefined();
      expect(figure.handler).toBeLessThan(200);
    }
  });

  // The decision point `leave` (ADR 04 Amendments 2026-11-03 (3)): on WebAssembly the Shift+Tab close runs inside the
  // keydown and a canceled close keeps the focus; on Server the module returns the focus at once and the cancel lands a
  // round trip later, so a canceled close still moved the focus.
  test('port: leave: a canceled Shift+Tab close keeps the focus on WebAssembly and still moves it on Server', async ({ page, mode }) => {
    const { fixture } = await gotoMeasure(page, mode, { n: 100, cancelClose: true }, mode === 'server' ? ONE_WAY_MS : 0);
    await openByKeyboard(page);
    await startWitness(page, [
      { name: 'returned', selector: '[data-testid="trigger"]', focused: true },
      { name: 'canceled', selector: container, attribute: 'data-open-changes', includes: 'false:focus-out' },
    ]);

    await page.keyboard.press('Shift+Tab');
    await expect.poll(async () => (await openChanges(fixture)).includes('false:focus-out')).toBe(true);
    await settle(fixture);

    const t = await times(page);
    await expect(page.getByRole('menu')).toBeVisible();
    if (mode === 'server') {
      await expect(page.getByTestId('trigger')).toBeFocused();
      const figure = { focusReturned: since(t, 'keydown', 'returned'), cancelInCSharp: since(t, 'keydown', 'canceled') };
      logFigure('leave: a canceled close that still moved the focus (Server)', figure);
      expect(figure.focusReturned).toBeLessThan(50);
      expect(figure.cancelInCSharp).toBeLessThan(2 * ONE_WAY_MS + 400);
    } else {
      await expect(item(page, 1)).toBeFocused();
      const figure = { cancelInCSharp: since(t, 'keydown', 'canceled') };
      logFigure('leave: the canceled close in the keydown (WebAssembly)', figure);
      expect(t.returned).toBeUndefined();
      expect(figure.cancelInCSharp).toBeLessThan(200);
    }
  });
});
