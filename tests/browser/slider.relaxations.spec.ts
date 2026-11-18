import { test, expect, delayCircuit, Fixture, fixtureUrl, type Page, type RenderMode } from './helpers';
import type { Locator } from '@playwright/test';

// SLD §9's Server relaxations (docs/components/slider.md) against ADR 01's relaxation table, measured per render mode on
// the `slider-relaxations` fixture: each row at 0, 50 and 150 ms of one-way delay added to the Server circuit
// (`delayCircuit`, before the first navigation; 0 ms is the bare localhost circuit), and once on WebAssembly, where the
// strict timing is asserted. Each title logs its figures (ms after the input, the page's `performance.now()`) as a
// `figure` annotation. The witness is a `MutationObserver` the page adds, which reads the state in a microtask queued from
// its callback (the recorder rule of tests/browser/CONTEXT.md), plus a capture `focusout` listener; a marker records
// whether it first held inside the input's own task (before a 0 ms timer the input's capture listener starts). Two
// update-carried values (Step, the direction) change no DOM the module writes, so their new behavior is probed: a
// synthetic ArrowRight on the thumb input once per animation frame from the click, whose step the module writes into
// the input's value in the same dispatch. The assertions do not depend on the runner's speed (issue 114's rule): they
// assert the order of the markers, whether a marker held inside the input's task, and on Server the floor the added
// delay puts under the legs between them.

const SERVER_DELAYS = [0, 50, 150] as const;

/** The least time `count` delayed legs take as the page sees it (the route's timers may fire a millisecond early). */
const legsFloor = (delay: number, count: number): number => (delay === 0 ? 0 : count * delay - 2 * count);

type Query = Record<string, string>;

/** The delays a title runs at in a mode: the three Server delays, or WebAssembly once. */
function delaysFor(mode: RenderMode): readonly number[] {
  return mode === 'server' ? SERVER_DELAYS : [0];
}

/** One title per delay; a WebAssembly project runs the 0 ms title only. */
function perDelay(title: string, body: (args: { page: Page; mode: RenderMode; delay: number }) => Promise<void>): void {
  for (const delay of SERVER_DELAYS) {
    test(`${title} (${delay} ms added on Server)`, async ({ page, mode }) => {
      test.skip(!delaysFor(mode).includes(delay), 'WebAssembly has no circuit to delay; its title runs once.');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }
      await body({ page, mode, delay });
    });
  }
}

async function open(page: Page, mode: RenderMode, query: Query = {}): Promise<Locator> {
  const name = 'slider-relaxations';
  const search = new URLSearchParams(query).toString();
  await page.goto(`${fixtureUrl(mode, name)}${search ? `?${search}` : ''}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** The sync barrier: a DOM click on the fixture's Sync button, answered once C# has handled every earlier message. */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((button) => (button as HTMLButtonElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

async function origin(fixture: Locator): Promise<{ x: number; y: number }> {
  return (await fixture.getByTestId('track').boundingBox())!;
}

const THUMB = '[data-testid="thumb"]';
const INPUT = '[data-testid="thumb"] input';
const ROOT = '[data-testid="root"]';

/** A marker: the first time its condition holds after the witness starts (after `after`'s marker, when given). */
interface Condition {
  name: string;
  selector: string;
  /** The attribute equals `value`, or is present when `value` is absent (`absent`: is not present). */
  attribute?: string;
  /** The attribute differs from its value when the witness started. */
  changed?: string;
  /** The inline style property equals `value`. */
  style?: string;
  /** The element's `value` property equals `value`. */
  property?: 'value';
  /** The element's text equals `value`. */
  text?: boolean;
  /** The element is no longer the active element. */
  blurred?: boolean;
  /**
   * A mutation record of the element's `style` carries an old value that includes this text: a write that a later write
   * of the same task replaced, which no read of the live state sees.
   */
  replacedStyle?: string;
  value?: string;
  absent?: boolean;
  after?: string;
}

/**
 * Starts the witness. `__t.input` is the input's time: a capture `keydown` or `pointerdown` on the window, or the time a
 * title's in-page click starts (`clickInPage`); `__task` is true from it until a 0 ms timer it queues.
 */
async function startWitness(page: Page, conditions: Condition[]): Promise<void> {
  await page.evaluate((list) => {
    const w = window as unknown as {
      __t: Record<string, number>;
      __inTask: Record<string, boolean>;
      __task: boolean;
      __witness?: MutationObserver;
      __stopWitness?: () => void;
      __startInput: () => void;
    };
    w.__stopWitness?.();
    w.__t = {};
    w.__inTask = {};
    w.__task = false;
    const initial = new Map<string, string | null>();
    for (const condition of list) {
      if (condition.changed) {
        initial.set(condition.name, document.querySelector(condition.selector)?.getAttribute(condition.changed) ?? null);
      }
    }
    const holds = (condition: (typeof list)[number]): boolean => {
      const element = document.querySelector(condition.selector) as HTMLElement | null;
      if (!element) {
        return false;
      }
      if (condition.blurred) {
        return document.activeElement !== element;
      }
      if (condition.changed) {
        return element.getAttribute(condition.changed) !== initial.get(condition.name);
      }
      if (condition.style) {
        return element.style.getPropertyValue(condition.style) === condition.value;
      }
      if (condition.property) {
        return (element as HTMLInputElement).value === condition.value;
      }
      if (condition.text) {
        return (element.textContent ?? '').trim() === condition.value;
      }
      const value = element.getAttribute(condition.attribute!);
      if (condition.absent) {
        return value === null;
      }
      return condition.value === undefined ? value !== null : value === condition.value;
    };
    const check = () => {
      const now = performance.now();
      for (const condition of list) {
        if (condition.replacedStyle || w.__t[condition.name] !== undefined || (condition.after && w.__t[condition.after] === undefined)) {
          continue;
        }
        if (holds(condition)) {
          w.__t[condition.name] = now;
          w.__inTask[condition.name] = w.__task;
        }
      }
    };
    // The recorder rule: read in a microtask queued from the callback, after any module observer of the same batch.
    const queued = () => queueMicrotask(check);
    // A replaced write is read from the records themselves (their old values), not from the live state.
    const replaced = (records: MutationRecord[]) => {
      const now = performance.now();
      for (const condition of list) {
        if (!condition.replacedStyle || w.__t[condition.name] !== undefined) {
          continue;
        }
        const element = document.querySelector(condition.selector);
        if (records.some((record) => record.target === element && record.attributeName === 'style' && (record.oldValue ?? '').includes(condition.replacedStyle!))) {
          w.__t[condition.name] = now;
          w.__inTask[condition.name] = w.__task;
        }
      }
    };
    const observer = new MutationObserver((records) => {
      replaced(records);
      queued();
    });
    observer.observe(document, { subtree: true, childList: true, attributes: true, attributeOldValue: true, characterData: true });
    w.__startInput = () => {
      w.__t.input ??= performance.now();
      w.__task = true;
      setTimeout(() => {
        w.__task = false;
      }, 0);
    };
    // A probe's synthetic keydown is not an input.
    const onInput = (event: Event) => {
      if (event.isTrusted) {
        w.__startInput();
      }
    };
    window.addEventListener('keydown', onInput, true);
    window.addEventListener('pointerdown', onInput, true);
    document.addEventListener('focusout', queued, true);
    w.__stopWitness = () => {
      observer.disconnect();
      window.removeEventListener('keydown', onInput, true);
      window.removeEventListener('pointerdown', onInput, true);
      document.removeEventListener('focusout', queued, true);
    };
  }, conditions);
}

/** Clicks a fixture button in the page (a DOM click, so focus stays) and starts the input's time at the click. */
async function clickInPage(fixture: Locator, testId: string): Promise<void> {
  await fixture.getByTestId(testId).evaluate((button) => {
    (window as unknown as { __startInput: () => void }).__startInput();
    (button as HTMLButtonElement).click();
  });
}

/**
 * Clicks a fixture button in the page and probes the module's key handling from the click: a synthetic ArrowRight on
 * the thumb input in the click's task and then once per animation frame, until the module moves the value by other than
 * +1 (the new step or the new direction). Records `behavior` and the probes' deltas.
 */
async function clickAndProbe(fixture: Locator, testId: string): Promise<void> {
  await fixture.getByTestId(testId).evaluate((button, inputSelector) => {
    const w = window as unknown as { __t: Record<string, number>; __inTask: Record<string, boolean>; __task: boolean; __deltas: number[]; __startInput: () => void };
    const input = document.querySelector(inputSelector) as HTMLInputElement;
    w.__deltas = [];
    w.__startInput();
    (button as HTMLButtonElement).click();
    const probe = () => {
      const before = Number(input.value);
      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
      const delta = Number(input.value) - before;
      w.__deltas.push(delta);
      if (delta !== 1) {
        w.__t.behavior = performance.now();
        w.__inTask.behavior = w.__task;
        return;
      }
      if (w.__deltas.length < 120) {
        requestAnimationFrame(probe);
      }
    };
    probe();
  }, INPUT);
}

async function times(page: Page): Promise<Record<string, number>> {
  return page.evaluate(() => (window as unknown as { __t: Record<string, number> }).__t);
}

async function inTask(page: Page): Promise<Record<string, boolean>> {
  return page.evaluate(() => (window as unknown as { __inTask: Record<string, boolean> }).__inTask);
}

async function waitForMarkers(page: Page, names: readonly string[]): Promise<void> {
  await expect
    .poll(async () => {
      const t = await times(page);
      return names.filter((name) => t[name] === undefined);
    }, { message: `the markers ${names.join(', ')} were recorded` })
    .toEqual([]);
}

/** Each marker's time after the input, rounded to 0.1 ms. */
function sinceInput(t: Record<string, number>): Record<string, number> {
  const result: Record<string, number> = {};
  for (const [name, time] of Object.entries(t)) {
    if (name !== 'input') {
      result[name] = Math.round((time - t.input!) * 10) / 10;
    }
  }
  return result;
}

function logFigure(name: string, figure: unknown): void {
  const text = JSON.stringify(figure);
  test.info().annotations.push({ type: 'figure', description: `${name}: ${text}` });
  console.log(`[figure] ${test.info().project.name} ${name}: ${text}`);
}

function expectOrder(t: Record<string, number>, names: readonly string[]): void {
  for (let index = 1; index < names.length; index += 1) {
    expect(t[names[index]!]!, `"${names[index - 1]}" precedes "${names[index]}"`).toBeGreaterThanOrEqual(t[names[index - 1]!]!);
  }
}

test.describe('Slider: ADR 01 relaxation rows', () => {
  // SLD-PTR-01, SLD-PTR-16, SLD-EVT-09 (EVT-09's veto clause): the Control's C# onpointerdown calls PreventPartHandler().
  perDelay("port: a C# onpointerdown veto stops the drag on WebAssembly, and the drag starts regardless on Server", async ({ page, mode, delay }) => {
    const fixture = await open(page, mode, { pointerVeto: '1' });
    await startWitness(page, [
      { name: 'moved', selector: THUMB, style: 'inset-inline-start', value: '60%' },
      { name: 'handler', selector: '[data-fixture="slider-relaxations"]', attribute: 'data-pointer-vetoes', value: '1' },
    ]);
    const box = await origin(fixture);
    await page.mouse.move(box.x + 60, box.y + 5);
    await page.mouse.down();
    await expect(fixture).toHaveAttribute('data-pointer-vetoes', '1');
    await page.mouse.up();
    await settle(fixture);
    const t = await times(page);
    const k = await inTask(page);
    logFigure(`C# pointerdown veto, ${mode} +${delay} ms`, { ...sinceInput(t), inTask: k, changes: await fixture.getAttribute('data-changes') });
    if (mode === 'wasm') {
      expect(t.moved).toBeUndefined();
      expect(k.handler).toBe(true);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '25');
    } else {
      expect(k.moved).toBe(true);
      expect(k.handler).toBe(false);
      expectOrder(t, ['input', 'moved', 'handler']);
      expect(t.handler! - t.input!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      await expect(fixture).toHaveAttribute('data-changes', '60:track-press:0');
      await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '60');
    }
  });

  // SLD-KEY-01, SLD-API-22, SLD-EVT-09: the thumb's C# onkeydown calls PreventPartHandler(). The veto skips the module's
  // key handling only, as Base UI's preventBaseUIHandler() skips its onKeyDown: nothing prevents the range input's native
  // step, so on WebAssembly the input steps itself and the module reports that as an `input-change` (SLD-EVT-02), where
  // on Server the module handles the key (`keyboard`, its preventDefault() stopping the native step).
  perDelay('port: a C# onkeydown veto on the thumb stops the key handling on WebAssembly, and the key is handled on Server', async ({ page, mode, delay }) => {
    const fixture = await open(page, mode, { keyVeto: '1' });
    await fixture.getByRole('slider').focus();
    await startWitness(page, [
      { name: 'moved', selector: THUMB, style: 'inset-inline-start', value: '26%' },
      { name: 'handler', selector: '[data-fixture="slider-relaxations"]', attribute: 'data-key-vetoes', value: '1' },
    ]);
    await page.keyboard.press('ArrowRight');
    await expect(fixture).toHaveAttribute('data-key-vetoes', '1');
    await settle(fixture);
    const t = await times(page);
    const k = await inTask(page);
    logFigure(`C# keydown veto, ${mode} +${delay} ms`, { ...sinceInput(t), inTask: k, changes: await fixture.getAttribute('data-changes') });
    if (mode === 'wasm') {
      expect(k.handler).toBe(true);
      expectOrder(t, ['input', 'handler', 'moved']);
      await expect(fixture).toHaveAttribute('data-changes', '26:input-change:0');
      await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '26');
    } else {
      expect(k.moved).toBe(true);
      expect(k.handler).toBe(false);
      expectOrder(t, ['input', 'moved', 'handler']);
      expect(t.handler! - t.input!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      await expect(fixture).toHaveAttribute('data-changes', '26:keyboard:0');
      await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '26');
    }
  });

  // SLD-PTR-01, SLD-KEY-01 (default-prevented clauses): a JavaScript listener's preventDefault() is met on both modes.
  perDelay("port: a JavaScript listener's preventDefault() stops the key and the drag on both modes", async ({ page, mode, delay }) => {
    const fixture = await open(page, mode);
    await page.evaluate(
      ({ input, track }) => {
        document.querySelector(input)!.addEventListener('keydown', (event) => event.preventDefault());
        document.querySelector(track)!.addEventListener('pointerdown', (event) => event.preventDefault());
      },
      { input: INPUT, track: '[data-testid="track"]' },
    );
    await startWitness(page, [
      { name: 'keyMoved', selector: THUMB, style: 'inset-inline-start', value: '26%' },
      { name: 'pressMoved', selector: THUMB, style: 'inset-inline-start', value: '60%' },
    ]);
    await fixture.getByRole('slider').focus();
    await page.keyboard.press('ArrowRight');
    const box = await origin(fixture);
    await page.mouse.move(box.x + 60, box.y + 5);
    await page.mouse.down();
    await page.mouse.up();
    await settle(fixture);
    const t = await times(page);
    logFigure(`JavaScript preventDefault, ${mode} +${delay} ms`, { ...sinceInput(t), changes: await fixture.getAttribute('data-changes') });
    expect(t.keyMoved).toBeUndefined();
    expect(t.pressMoved).toBeUndefined();
    await expect(fixture).toHaveAttribute('data-changes', '');
    await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '25');
  });

  // RND-STA-01 (the mirrored-fields row as SLD §9 adopts it): after a key press the module writes the thumb's style and
  // the input's value in the keydown's task; C# renders aria-valuenow and the Value text from the report.
  perDelay('port: the module writes the thumb style and the input value in the keydown; C# renders aria-valuenow and the Value text from the report', async ({ page, mode, delay }) => {
    const fixture = await open(page, mode);
    await fixture.getByRole('slider').focus();
    await startWitness(page, [
      { name: 'style', selector: THUMB, style: 'inset-inline-start', value: '26%' },
      { name: 'inputValue', selector: INPUT, property: 'value', value: '26' },
      { name: 'ariaValueNow', selector: INPUT, attribute: 'aria-valuenow', value: '26' },
      { name: 'valueText', selector: '[data-testid="value"]', text: true, value: '26' },
    ]);
    await page.keyboard.press('ArrowRight');
    await waitForMarkers(page, ['style', 'inputValue', 'ariaValueNow', 'valueText']);
    const t = await times(page);
    const k = await inTask(page);
    logFigure(`RND-STA-01 key press, ${mode} +${delay} ms`, { ...sinceInput(t), inTask: k });
    expect(k.style).toBe(true);
    expect(k.inputValue).toBe(true);
    expectOrder(t, ['input', 'style', 'ariaValueNow']);
    expectOrder(t, ['style', 'valueText']);
    if (mode === 'wasm') {
      expect(k.ariaValueNow).toBe(true);
      expect(k.valueText).toBe(true);
    } else {
      expect(k.ariaValueNow).toBe(false);
      expect(k.valueText).toBe(false);
      expect(t.ariaValueNow! - t.style!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      expect(t.valueText! - t.style!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // RND-STA-01 for the gesture: the module moves the thumb at the press; C# renders data-dragging from the gesture report.
  perDelay('port: the module moves the thumb at the press; C# renders data-dragging from the gesture report', async ({ page, mode, delay }) => {
    const fixture = await open(page, mode);
    await startWitness(page, [
      { name: 'style', selector: THUMB, style: 'inset-inline-start', value: '60%' },
      { name: 'dragging', selector: ROOT, attribute: 'data-dragging' },
      { name: 'ariaValueNow', selector: INPUT, attribute: 'aria-valuenow', value: '60' },
    ]);
    const box = await origin(fixture);
    await page.mouse.move(box.x + 60, box.y + 5);
    await page.mouse.down();
    await waitForMarkers(page, ['style', 'dragging', 'ariaValueNow']);
    await page.mouse.up();
    const t = await times(page);
    const k = await inTask(page);
    logFigure(`RND-STA-01 press, ${mode} +${delay} ms`, { ...sinceInput(t), inTask: k });
    expect(k.style).toBe(true);
    expectOrder(t, ['input', 'style', 'dragging']);
    if (mode === 'wasm') {
      expect(k.dragging).toBe(true);
      expect(k.ariaValueNow).toBe(true);
    } else {
      expect(k.dragging).toBe(false);
      expect(t.dragging! - t.style!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      expect(t.ariaValueNow! - t.style!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
    await expect(fixture).toHaveAttribute('data-commits', '60:track-press');
  });

  // SLD-EVT-03, SLD-EVT-12: OnValueChange cancels; the module's optimistic move is rolled back by the answering update.
  perDelay('port: a cancelled key change returns the thumb within the keydown on WebAssembly, and a round trip later on Server', async ({ page, mode, delay }) => {
    const fixture = await open(page, mode, { cancel: '1' });
    await fixture.getByRole('slider').focus();
    // `replaced`: the write that replaced the optimistic 26% (the record whose old value holds it), which proves the 26% was
    // written even when no read of the live state sees it; `shown`: 26% read as the live state (absent when the return
    // replaced it in the same task); `returned`: the live 25% after it.
    await startWitness(page, [
      { name: 'replaced', selector: THUMB, replacedStyle: 'inset-inline-start: 26%' },
      { name: 'shown', selector: THUMB, style: 'inset-inline-start', value: '26%' },
      { name: 'returned', selector: THUMB, style: 'inset-inline-start', value: '25%', after: 'replaced' },
      { name: 'inputReturned', selector: INPUT, property: 'value', value: '25', after: 'replaced' },
    ]);
    await page.keyboard.press('ArrowRight');
    await waitForMarkers(page, ['replaced', 'returned', 'inputReturned']);
    await settle(fixture);
    const t = await times(page);
    const k = await inTask(page);
    logFigure(`cancelled change rollback, ${mode} +${delay} ms`, { ...sinceInput(t), inTask: k });
    expectOrder(t, ['input', 'replaced', 'returned']);
    if (mode === 'wasm') {
      expect(k.replaced).toBe(true);
      // The rollback replaces the optimistic write inside the keydown's dispatch: no microtask reads 26%, no frame paints it.
      expect(t.shown).toBeUndefined();
      expect(k.returned).toBe(true);
    } else {
      expect(k.shown).toBe(true);
      expect(k.returned).toBe(false);
      expect(t.returned! - t.shown!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
    await expect(fixture).toHaveAttribute('data-changes', '26:keyboard:0');
    await expect(fixture).toHaveAttribute('data-commits', '');
    await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '25');
  });

  // ADR 01's "JS-owned values that originate in C# state" row: every update-carried value reaches the module on update,
  // after the render that carries it (the update is sent from OnAfterRender).
  const updates: Array<{
    name: string;
    button: string;
    query?: Query;
    focus?: boolean;
    rendered: Condition;
    module?: Condition;
    probe?: boolean;
    check?: (fixture: Locator) => Promise<void>;
  }> = [
    {
      name: 'Min',
      button: 'set-min',
      rendered: { name: 'rendered', selector: INPUT, attribute: 'min', value: '20' },
      module: { name: 'behavior', selector: THUMB, style: 'inset-inline-start', value: '6.25%' },
    },
    {
      name: 'Max',
      button: 'set-max',
      rendered: { name: 'rendered', selector: INPUT, attribute: 'max', value: '50' },
      module: { name: 'behavior', selector: THUMB, style: 'inset-inline-start', value: '50%' },
    },
    {
      name: 'Step',
      button: 'set-step',
      rendered: { name: 'rendered', selector: INPUT, attribute: 'step', value: '10' },
      probe: true,
    },
    {
      // SLD-FOC-05: the module blurs the focused thumb input when the update disables the slider.
      name: 'Disabled (with its blur)',
      button: 'toggle-disabled',
      focus: true,
      rendered: { name: 'rendered', selector: ROOT, attribute: 'data-disabled' },
      module: { name: 'behavior', selector: INPUT, blurred: true },
    },
    {
      name: 'Orientation',
      button: 'toggle-orientation',
      rendered: { name: 'rendered', selector: INPUT, attribute: 'aria-orientation', value: 'vertical' },
      module: { name: 'behavior', selector: THUMB, style: 'bottom', value: '25%' },
    },
    {
      name: 'direction',
      button: 'toggle-direction',
      // Nothing a part renders changes with a horizontal slider's direction: the fixture's own wrapper marks the render.
      rendered: { name: 'rendered', selector: '[data-direction]', attribute: 'data-direction', value: 'Rtl' },
      probe: true,
    },
    {
      name: 'controlled Value',
      button: 'set-value',
      query: { controlled: '1' },
      rendered: { name: 'rendered', selector: INPUT, attribute: 'aria-valuenow', value: '70' },
      module: { name: 'behavior', selector: THUMB, style: 'inset-inline-start', value: '70%' },
      check: async (fixture) => {
        await expect(fixture.getByRole('slider')).toHaveValue('70');
      },
    },
  ];

  for (const update of updates) {
    perDelay(`port: an external ${update.name} reaches the module after the render that carries it`, async ({ page, mode, delay }) => {
      const fixture = await open(page, mode, update.query ?? {});
      if (update.focus) {
        await fixture.getByRole('slider').focus();
      }
      await startWitness(page, update.module ? [update.rendered, update.module] : [update.rendered]);
      if (update.probe) {
        await clickAndProbe(fixture, update.button);
      } else {
        await clickInPage(fixture, update.button);
      }
      await waitForMarkers(page, ['rendered', 'behavior']);
      await settle(fixture);
      const t = await times(page);
      const k = await inTask(page);
      const deltas = update.probe ? await page.evaluate(() => (window as unknown as { __deltas: number[] }).__deltas) : undefined;
      logFigure(`update-carried ${update.name}, ${mode} +${delay} ms`, {
        ...sinceInput(t),
        inTask: k,
        ...(deltas ? { probes: deltas.length, lastDelta: deltas.at(-1) } : {}),
      });
      if (mode === 'wasm') {
        // The update runs from OnAfterRender inside the click's dispatch (a probe in the click's task already sees it).
        expect(k.rendered).toBe(true);
        expect(k.behavior).toBe(true);
      } else {
        expectOrder(t, ['input', 'rendered', 'behavior']);
        expect(k.rendered).toBe(false);
        expect(k.behavior).toBe(false);
        expect(t.rendered! - t.input!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
        expect(t.behavior! - t.input!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      }
      await update.check?.(fixture);
    });
  }

  // SLD-FRM-02, SLD-FRM-04, SLD-STA-21: the field registration's dirty write follows the change's commit.
  perDelay('port: data-dirty lands within the keydown on WebAssembly, and after the commit on Server', async ({ page, mode, delay }) => {
    const fixture = await open(page, mode, { field: '1' });
    await fixture.getByRole('slider').focus();
    await startWitness(page, [
      { name: 'style', selector: THUMB, style: 'inset-inline-start', value: '26%' },
      { name: 'ariaValueNow', selector: INPUT, attribute: 'aria-valuenow', value: '26' },
      { name: 'rootDirty', selector: ROOT, attribute: 'data-dirty' },
      { name: 'fieldDirty', selector: '[data-testid="field"]', attribute: 'data-dirty' },
    ]);
    await page.keyboard.press('ArrowRight');
    await waitForMarkers(page, ['style', 'ariaValueNow', 'rootDirty', 'fieldDirty']);
    const t = await times(page);
    const k = await inTask(page);
    logFigure(`field data-dirty, ${mode} +${delay} ms`, { ...sinceInput(t), inTask: k });
    expect(k.style).toBe(true);
    expectOrder(t, ['input', 'style', 'ariaValueNow', 'rootDirty']);
    if (mode === 'wasm') {
      expect(k.rootDirty).toBe(true);
      expect(k.fieldDirty).toBe(true);
    } else {
      expect(k.rootDirty).toBe(false);
      expect(t.rootDirty! - t.style!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      expect(t.fieldDirty! - t.style!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });
});
