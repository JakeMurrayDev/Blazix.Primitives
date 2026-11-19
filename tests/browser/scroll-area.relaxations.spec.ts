import { test, expect, delayCircuit, Fixture, fixtureUrl, type Page, type RenderMode } from './helpers';
import type { Locator } from '@playwright/test';

// SCR §9's Server relaxations (docs/components/scroll-area.md) against ADR 01's relaxation table, measured per render mode
// on the `scroll-area-relaxations` fixture: each row at 0, 50 and 150 ms of one-way delay added to the Server circuit
// (`delayCircuit`, before the first navigation; 0 ms is the bare localhost circuit), and once on WebAssembly. Each title
// logs its figures (ms after the input, the page's `performance.now()`) as a `figure` annotation. The witness is a
// `MutationObserver` the page adds, which reads the state in a microtask queued from its callback (the recorder rule of
// tests/browser/CONTEXT.md). A marker records whether it first held inside the input's own task (`inTask`: from a capture
// listener of the trusted input, or the start of an in-page input, until a `MessageChannel` task it posts, which the page's
// clock does not fake) and inside the task of the first `scroll` event after the input (`inScroll`, the same way). The
// assertions do not depend on the runner's speed (issue 114's rule): they assert the order of the markers, whether a
// marker held inside a task, and on Server the floor the added delay puts under the legs between them.
//
// The fixture renders nothing from the root's reports but the parts themselves (its header), so the figures are the
// library's; its sync barrier re-renders only itself. The message and byte figures (Deliverable 14 of the ScrollArea
// brief, issue 291) count the circuit's SignalR messages on Server at 0 ms through a counting `page.routeWebSocket`.

const SERVER_DELAYS = [0, 50, 150] as const;
const NAME = 'scroll-area-relaxations';
const FIXTURE = `[data-fixture="${NAME}"]`;

/** The least time `count` delayed legs take as the page sees it (the route's timers may fire a millisecond early). */
const legsFloor = (delay: number, count: number): number => (delay === 0 ? 0 : count * delay - 2 * count);

type Query = Record<string, string>;

/** One title per delay; a WebAssembly project runs the 0 ms title only. */
function perDelay(title: string, body: (args: { page: Page; mode: RenderMode; delay: number }) => Promise<void>): void {
  for (const delay of SERVER_DELAYS) {
    test(`${title} (${delay} ms added on Server)`, async ({ page, mode }) => {
      test.skip(mode === 'wasm' && delay !== 0, 'WebAssembly has no circuit to delay; its title runs once.');
      if (mode === 'server' && delay > 0) {
        await delayCircuit(page, delay);
      }
      await body({ page, mode, delay });
    });
  }
}

function urlOf(mode: RenderMode, query: Query): string {
  const search = new URLSearchParams(query).toString();
  return `${fixtureUrl(mode, NAME)}${search ? `?${search}` : ''}`;
}

/** Opens the fixture and waits for its handles (root, viewport, content) to attach. */
async function open(page: Page, mode: RenderMode, query: Query = {}): Promise<Locator> {
  await page.goto(urlOf(mode, query), { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(NAME)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(FIXTURE);
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/**
 * Opens the fixture with both axes overflowing and waits for the second measurement pass (the corner sized, the thumb
 * sized). With a corner the thumb ends at 38 px whatever order the parts attach in: a corner-size change measures again
 * (the fix of 2026-11-19, found by these figures, when a Corner sized before the horizontal scrollbar left the thumb at 40 px).
 */
async function openMeasured(page: Page, mode: RenderMode, query: Query = {}): Promise<Locator> {
  const fixture = await open(page, mode, query);
  await expect(page.getByTestId('root')).toHaveAttribute('data-has-overflow-y');
  if (query.corner !== 'false') {
    await expect.poll(() => inline(page, 'corner', 'height')).toBe('10px');
    await expect.poll(() => inline(page, 'corner', 'width')).toBe('10px');
  }
  await expect.poll(() => inline(page, 'scrollbar-vertical', '--scroll-area-thumb-height')).toMatch(query.corner === 'false' ? /^(38|40)px$/ : /^38px$/);
  await resizeObserved(page);
  return fixture;
}

/** The sync barrier: a DOM click on the barrier's button, answered once C# has handled every earlier message. */
async function settle(page: Page): Promise<void> {
  const barrier = page.locator(`${FIXTURE} [data-sync-barrier]`);
  const before = Number(await barrier.getAttribute('data-sync'));
  await barrier.locator('[data-sync-button]').evaluate((button) => (button as HTMLButtonElement).click());
  await expect(barrier).toHaveAttribute('data-sync', String(before + 1));
}

function inline(page: Page, testId: string, property: string): Promise<string> {
  return page.getByTestId(testId).evaluate((element, name) => (element as HTMLElement).style.getPropertyValue(name), property);
}

/**
 * Waits until a `ResizeObserver` created now has had its first notification: every earlier observer's pending first
 * notification (the module's, which it may skip) has then been delivered, so a later resize is measured.
 */
async function resizeObserved(page: Page): Promise<void> {
  await page.getByTestId('inner').evaluate(
    (element) =>
      new Promise<void>((resolve) => {
        const observer = new ResizeObserver(() => {
          observer.disconnect();
          resolve();
        });
        observer.observe(element);
      }),
  );
}

/** A marker: the first time its condition holds after the witness starts (after `after`'s marker, when given). */
interface Condition {
  name: string;
  selector: string;
  /** The element exists. */
  exists?: boolean;
  /** The attribute equals `value`, or is present when `value` is absent (`absent`: is not present). */
  attribute?: string;
  /** The inline style property equals `value` (`nonZero`: is set and not `0px`; `changed`: differs from its start). */
  style?: string;
  /** The computed style property equals `value`. */
  computed?: string;
  /** The class list contains this class. */
  className?: string;
  value?: string;
  absent?: boolean;
  nonZero?: boolean;
  changed?: boolean;
  after?: string;
}

interface WitnessOptions {
  conditions: Condition[];
  /** The marker the time origin is (`input` by default). */
  origin?: string;
  /** Records `scrolled` from a bubbling window listener of these events once the viewport's scrollTop has moved. */
  scrollProbe?: string[];
  /** Records, inside the first resize notification of the inner element after the input, which markers already held. */
  resizeProbe?: boolean;
}

/**
 * The page's witness, self-contained so it runs both as an init script (before the module's observers, for the first
 * measurement) and in a page evaluation. `__t[name]` is a marker's time, `__inTask`/`__inScroll` whether it held inside the
 * input's task or the first scroll event's task.
 */
function witness(options: WitnessOptions): void {
  const w = window as unknown as {
    __t: Record<string, number>;
    __inTask: Record<string, boolean>;
    __inScroll: Record<string, boolean>;
    __atResize: Record<string, boolean>;
    __task: boolean;
    __scrollTask: boolean;
    __stopWitness?: () => void;
    __startInput: () => void;
  };
  w.__stopWitness?.();
  w.__t = {};
  w.__inTask = {};
  w.__inScroll = {};
  w.__atResize = {};
  w.__task = false;
  w.__scrollTask = false;
  const list = options.conditions;
  const initial = new Map<string, string | null>();
  const read = (condition: Condition, element: HTMLElement): string | null => {
    if (condition.style) {
      return element.style.getPropertyValue(condition.style);
    }
    if (condition.attribute) {
      return element.getAttribute(condition.attribute);
    }
    return null;
  };
  for (const condition of list) {
    if (condition.changed) {
      const element = document.querySelector(condition.selector) as HTMLElement | null;
      initial.set(condition.name, element ? read(condition, element) : null);
    }
  }
  const holds = (condition: Condition): boolean => {
    const element = document.querySelector(condition.selector) as HTMLElement | null;
    if (!element) {
      return false;
    }
    if (condition.exists) {
      return true;
    }
    if (condition.className) {
      return element.classList.contains(condition.className);
    }
    if (condition.computed) {
      return getComputedStyle(element).getPropertyValue(condition.computed) === condition.value;
    }
    const value = read(condition, element);
    if (condition.changed) {
      return value !== initial.get(condition.name);
    }
    if (condition.style) {
      if (condition.nonZero) {
        return !!value && value !== '0px';
      }
      return value === condition.value;
    }
    if (condition.absent) {
      return value === null;
    }
    return condition.value === undefined ? value !== null : value === condition.value;
  };
  const check = () => {
    const now = performance.now();
    for (const condition of list) {
      if (w.__t[condition.name] !== undefined || (condition.after && w.__t[condition.after] === undefined)) {
        continue;
      }
      if (holds(condition)) {
        w.__t[condition.name] = now;
        w.__inTask[condition.name] = w.__task;
        w.__inScroll[condition.name] = w.__scrollTask;
      }
    }
  };
  // The end of a task: a MessageChannel message, which the page's clock does not fake.
  const afterTask = (done: () => void) => {
    const channel = new MessageChannel();
    channel.port1.onmessage = () => {
      channel.port1.close();
      done();
    };
    channel.port2.postMessage(null);
  };
  const observer = new MutationObserver(() => queueMicrotask(check));
  observer.observe(document, { subtree: true, childList: true, attributes: true, characterData: true });
  w.__startInput = () => {
    if (w.__t.input === undefined) {
      w.__t.input = performance.now();
    }
    w.__task = true;
    afterTask(() => {
      w.__task = false;
    });
  };
  const onInput = (event: Event) => {
    if (event.isTrusted) {
      w.__startInput();
    }
  };
  const inputTypes = ['keydown', 'pointerdown', 'pointermove', 'pointerover', 'wheel'];
  for (const type of inputTypes) {
    window.addEventListener(type, onInput, true);
  }
  const onScroll = () => {
    if (w.__t.input === undefined || w.__t.scroll !== undefined) {
      return;
    }
    w.__t.scroll = performance.now();
    w.__scrollTask = true;
    afterTask(() => {
      w.__scrollTask = false;
    });
  };
  document.addEventListener('scroll', onScroll, true);
  const viewport = document.querySelector('[data-testid="viewport"]') as HTMLElement | null;
  const startTop = viewport?.scrollTop ?? 0;
  const probe = () => {
    if (w.__t.scrolled === undefined && viewport && viewport.scrollTop !== startTop) {
      w.__t.scrolled = performance.now();
      w.__inTask.scrolled = w.__task;
    }
  };
  for (const type of options.scrollProbe ?? []) {
    window.addEventListener(type, probe);
  }
  let resizeObserver: ResizeObserver | null = null;
  if (options.resizeProbe) {
    (window as unknown as { __resizeReady: boolean }).__resizeReady = false;
    let first = true;
    resizeObserver = new ResizeObserver(() => {
      // The observer's own first notification, at observe, is not the resize.
      if (first) {
        first = false;
        (window as unknown as { __resizeReady: boolean }).__resizeReady = true;
        return;
      }
      if (w.__t.resize !== undefined) {
        return;
      }
      w.__t.resize = performance.now();
      for (const condition of list) {
        w.__atResize[condition.name] = holds(condition);
      }
    });
    resizeObserver.observe(document.querySelector('[data-testid="inner"]')!);
  }
  w.__stopWitness = () => {
    observer.disconnect();
    resizeObserver?.disconnect();
    for (const type of inputTypes) {
      window.removeEventListener(type, onInput, true);
    }
    for (const type of options.scrollProbe ?? []) {
      window.removeEventListener(type, probe);
    }
    document.removeEventListener('scroll', onScroll, true);
  };
}

async function startWitness(page: Page, options: WitnessOptions): Promise<void> {
  await page.evaluate(witness, options);
  if (options.resizeProbe) {
    // The probe's own first notification, so the next one is the resize.
    await expect.poll(() => page.evaluate(() => (window as unknown as { __resizeReady?: boolean }).__resizeReady === true)).toBe(true);
  }
}

interface Record3 {
  t: Record<string, number>;
  inTask: Record<string, boolean>;
  inScroll: Record<string, boolean>;
  atResize: Record<string, boolean>;
}

async function records(page: Page): Promise<Record3> {
  return page.evaluate(() => {
    const w = window as unknown as {
      __t: Record<string, number>;
      __inTask: Record<string, boolean>;
      __inScroll: Record<string, boolean>;
      __atResize: Record<string, boolean>;
    };
    return { t: { ...w.__t }, inTask: { ...w.__inTask }, inScroll: { ...w.__inScroll }, atResize: { ...w.__atResize } };
  });
}

async function waitForMarkers(page: Page, names: readonly string[]): Promise<Record3> {
  await expect
    .poll(async () => {
      const { t } = await records(page);
      return names.filter((name) => t[name] === undefined);
    }, { message: `the markers ${names.join(', ')} were recorded` })
    .toEqual([]);
  return records(page);
}

/** Each marker's time after the origin, rounded to 0.1 ms. */
function since(t: Record<string, number>, origin = 'input'): Record<string, number> {
  const result: Record<string, number> = {};
  for (const [name, time] of Object.entries(t)) {
    if (name !== origin) {
      result[name] = Math.round((time - t[origin]!) * 10) / 10;
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

/** Starts an in-page input: the witness's input time, then `action` in the same task. */
async function inputInPage(page: Page, action: string, arg?: unknown): Promise<void> {
  await page.evaluate(
    ({ action, arg }) => {
      (window as unknown as { __startInput: () => void }).__startInput();
      const viewport = document.querySelector('[data-testid="viewport"]') as HTMLElement;
      const inner = document.querySelector('[data-testid="inner"]') as HTMLElement;
      switch (action) {
        case 'scrollTop':
          viewport.scrollTop = arg as number;
          break;
        case 'innerHeight':
          inner.style.height = `${arg as number}px`;
          break;
        case 'resizeAndWiden': {
          // The content grows and the vertical scrollbar widens, in one task (a stylesheet rule, so the module's style
          // writer keeps the element's own style).
          const style = document.createElement('style');
          style.textContent = '[data-testid="scrollbar-vertical"] { width: 14px !important; }';
          document.head.append(style);
          inner.style.height = `${arg as number}px`;
          break;
        }
        case 'click':
          (document.querySelector(arg as string) as HTMLElement).click();
          break;
      }
    },
    { action, arg },
  );
}

const ROOT = '[data-testid="root"]';
const VIEWPORT = '[data-testid="viewport"]';
const BAR_Y = '[data-testid="scrollbar-vertical"]';
const BAR_X = '[data-testid="scrollbar-horizontal"]';
const THUMB_Y = '[data-testid="thumb-vertical"]';
const CORNER = '[data-testid="corner"]';

test.describe('ScrollArea: ADR 01 relaxation rows', () => {
  // SCR-ANI-01, SCR-ANI-03, SCR-ANI-09, SCR-STA-19, SCR-DOM-15, SCR-DOM-19, SCR-DOM-23, SCR-DOM-26: the first measurement
  // runs in the module once the viewport's handle attaches; C# renders the overflow attributes and mounts the scrollbars
  // and the Corner from its report; their handles' attach runs the second pass that sizes the thumb and the Corner.
  for (const keepMounted of [false, true]) {
    const variant = keepMounted ? 'kept-mounted scrollbars' : 'scrollbars that are not kept mounted';
    perDelay(`port: the first-measurement chain with ${variant} and a Corner`, async ({ page, mode, delay }) => {
      const conditions: Condition[] = [
        { name: 'interactive', selector: Fixture.interactive(NAME), exists: true },
        { name: 'jsMeasured', selector: VIEWPORT, style: '--scroll-area-overflow-y-end', nonZero: true },
        { name: 'hasOverflowY', selector: ROOT, attribute: 'data-has-overflow-y' },
        { name: 'tabindex', selector: VIEWPORT, attribute: 'tabindex', value: '0' },
        { name: 'scrollbarY', selector: BAR_Y, exists: true },
        { name: 'scrollbarX', selector: BAR_X, exists: true },
        { name: 'thumbVisible', selector: THUMB_Y, computed: 'visibility', value: 'visible' },
        { name: 'corner', selector: CORNER, exists: true },
        { name: 'thumbSized', selector: BAR_Y, style: '--scroll-area-thumb-height', value: '38px' },
        { name: 'cornerSized', selector: CORNER, style: 'width', value: '10px' },
        { name: 'attached', selector: `${FIXTURE}[data-attached="true"]`, exists: true },
      ];
      await page.addInitScript(witness, { conditions, origin: 'interactive' });
      await page.goto(urlOf(mode, { keepMounted: String(keepMounted) }), { waitUntil: 'commit' });
      const names = conditions.map((condition) => condition.name);
      const { t } = await waitForMarkers(page, names);
      logFigure(`first-measurement chain (${keepMounted ? 'keepMounted' : 'not kept mounted'}), ${mode} +${delay} ms`, {
        interactiveSinceNavigation: Math.round(t.interactive! * 10) / 10,
        ...since(t, 'interactive'),
      });
      expectOrder(t, ['interactive', 'jsMeasured', 'hasOverflowY', 'thumbVisible']);
      expectOrder(t, ['jsMeasured', 'tabindex']);
      expectOrder(t, ['jsMeasured', 'corner', 'cornerSized']);
      expectOrder(t, ['jsMeasured', 'thumbSized']);
      if (keepMounted) {
        // The kept-mounted scrollbars render with the first interactive render (one mutation batch); their thumbs show
        // once C# renders the measured flag, which the module writes from the style the update carries.
        expect(t.scrollbarY).toBe(t.interactive);
        expect(t.scrollbarX).toBe(t.interactive);
      } else {
        // Mounted from the report.
        expectOrder(t, ['jsMeasured', 'scrollbarY', 'thumbSized']);
        expectOrder(t, ['jsMeasured', 'scrollbarX']);
      }
      if (mode === 'server') {
        // Leg floors. The interactive render's acknowledgement reaches C#; each new part's twin imports the module (a round
        // trip) and then attaches (a call): four legs before the module measures. The report reaches C# and its render the
        // page: two legs. The parts that render rendered from the report attach the same way: four more legs before the
        // second pass sizes the Corner. A kept-mounted thumb shows from an update: the acknowledgement and the call.
        expect(t.jsMeasured! - t.interactive!).toBeGreaterThanOrEqual(legsFloor(delay, 4));
        for (const name of ['hasOverflowY', 'tabindex', 'corner', ...(keepMounted ? [] : ['scrollbarY', 'scrollbarX', 'thumbVisible'])]) {
          expect(t[name]! - t.jsMeasured!, `${name} trails the first measurement by the report and its render`).toBeGreaterThanOrEqual(legsFloor(delay, 2));
        }
        expect(t.cornerSized! - t.corner!).toBeGreaterThanOrEqual(legsFloor(delay, 4));
        if (keepMounted) {
          expect(t.thumbVisible! - t.hasOverflowY!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
        } else {
          expect(t.thumbSized! - t.scrollbarY!).toBeGreaterThanOrEqual(legsFloor(delay, 4));
        }
      }
    });
  }

  // SCR-DOM-03, SCR-DOM-10, SCR-DOM-14, SCR-DOM-16: the module writes the overflow variables in the scroll event; C#
  // projects the edge attributes from the report.
  perDelay('port: a scroll away from the top writes the variables in the scroll event and C# projects data-overflow-y-start from the report', async ({ page, mode, delay }) => {
    await openMeasured(page, mode);
    await startWitness(page, {
      conditions: [
        { name: 'jsVar', selector: VIEWPORT, style: '--scroll-area-overflow-y-start', value: '100px' },
        { name: 'rootAttr', selector: ROOT, attribute: 'data-overflow-y-start' },
        { name: 'viewportAttr', selector: VIEWPORT, attribute: 'data-overflow-y-start' },
        { name: 'scrollbarAttr', selector: BAR_Y, attribute: 'data-overflow-y-start' },
      ],
    });
    await inputInPage(page, 'scrollTop', 100);
    const { t, inScroll } = await waitForMarkers(page, ['scroll', 'jsVar', 'rootAttr', 'viewportAttr', 'scrollbarAttr']);
    logFigure(`edge attribute after a scroll, ${mode} +${delay} ms`, { ...since(t), inScroll });
    expectOrder(t, ['input', 'scroll', 'jsVar', 'rootAttr']);
    expect(inScroll.jsVar).toBe(true);
    expectOrder(t, ['jsVar', 'viewportAttr']);
    expectOrder(t, ['jsVar', 'scrollbarAttr']);
    if (mode === 'wasm') {
      expect(inScroll).toMatchObject({ rootAttr: true, viewportAttr: true, scrollbarAttr: true });
    } else {
      expect(inScroll).toMatchObject({ rootAttr: false, viewportAttr: false, scrollbarAttr: false });
      for (const name of ['rootAttr', 'viewportAttr', 'scrollbarAttr']) {
        expect(t[name]! - t.jsVar!, name).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      }
    }
  });

  // SCR-DOM-08, SCR-DOM-15: content that grows past the viewport is measured in its resize notification; C# renders the
  // viewport's tabindex, the overflow attributes and the scrollbars from the report.
  perDelay('port: content growing past the viewport is measured at once and C# renders tabindex="0" from the report', async ({ page, mode, delay }) => {
    await open(page, mode, { width: '100', height: '100' });
    await expect(page.getByTestId('viewport')).toHaveAttribute('tabindex', '-1');
    await expect.poll(() => inline(page, 'viewport', '--scroll-area-overflow-y-end')).toBe('0px');
    await resizeObserved(page);
    await startWitness(page, {
      conditions: [
        { name: 'jsVar', selector: VIEWPORT, style: '--scroll-area-overflow-y-end', nonZero: true },
        { name: 'tabindex', selector: VIEWPORT, attribute: 'tabindex', value: '0' },
        { name: 'hasOverflowY', selector: ROOT, attribute: 'data-has-overflow-y' },
        { name: 'scrollbarY', selector: BAR_Y, exists: true },
      ],
      resizeProbe: true,
    });
    await inputInPage(page, 'innerHeight', 1000);
    const { t, atResize } = await waitForMarkers(page, ['resize', 'jsVar', 'tabindex', 'hasOverflowY', 'scrollbarY']);
    logFigure(`tabindex after content growth, ${mode} +${delay} ms`, { ...since(t), atResize });
    // The module measured in the resize notification, before the witness's own notification of the same delivery.
    expect(atResize.jsVar).toBe(true);
    expectOrder(t, ['input', 'jsVar', 'tabindex']);
    expectOrder(t, ['jsVar', 'hasOverflowY']);
    expectOrder(t, ['jsVar', 'scrollbarY']);
    if (mode === 'wasm') {
      expect(atResize).toMatchObject({ tabindex: true, hasOverflowY: true, scrollbarY: true });
    } else {
      expect(atResize).toMatchObject({ tabindex: false, hasOverflowY: false, scrollbarY: false });
      for (const name of ['tabindex', 'hasOverflowY', 'scrollbarY']) {
        expect(t[name]! - t.jsVar!, name).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      }
    }
  });

  // SCR-DOM-02, SCR-DOM-20, SCR-DOM-27: the thumb-size and corner-size cells are the module's own writes once attached,
  // so they land in the resize notification on both modes.
  perDelay('port: a content resize rewrites the thumb-size and corner-size variables in the resize notification on both modes', async ({ page, mode, delay }) => {
    await openMeasured(page, mode);
    await startWitness(page, {
      conditions: [
        { name: 'thumbHeight', selector: BAR_Y, style: '--scroll-area-thumb-height', value: '19px' },
        { name: 'cornerWidth', selector: CORNER, style: 'width', value: '14px' },
        { name: 'rootCornerWidth', selector: ROOT, style: '--scroll-area-corner-width', value: '14px' },
      ],
      resizeProbe: true,
    });
    await inputInPage(page, 'resizeAndWiden', 2000);
    const { t, atResize } = await waitForMarkers(page, ['resize', 'thumbHeight', 'cornerWidth', 'rootCornerWidth']);
    logFigure(`thumb and corner size after a content resize, ${mode} +${delay} ms`, { ...since(t), atResize });
    expect(atResize).toEqual({ thumbHeight: true, cornerWidth: true, rootCornerWidth: true });
    expectOrder(t, ['input', 'thumbHeight']);
  });

  // SCR-I18N-01, SCR-ANI-02: a direction change reaches the module with the root's update, which measures again; C#
  // projects the flipped edges from the report.
  perDelay('port: a direction change measures again on the root update and C# projects the flipped horizontal edges', async ({ page, mode, delay }) => {
    await open(page, mode, { bars: 'horizontal', corner: 'false', height: '200' });
    const root = page.getByTestId('root');
    await expect(root).toHaveAttribute('data-has-overflow-x');
    await page.getByTestId('viewport').evaluate((element) => {
      element.scrollLeft = element.scrollWidth - element.clientWidth;
    });
    await expect(root).toHaveAttribute('data-overflow-x-start');
    await expect(root).not.toHaveAttribute('data-overflow-x-end');
    await startWitness(page, {
      conditions: [
        { name: 'dirApplied', selector: ROOT, style: 'direction', value: 'rtl' },
        { name: 'jsVar', selector: VIEWPORT, style: '--scroll-area-overflow-x-start', value: '0px' },
        { name: 'startCleared', selector: ROOT, attribute: 'data-overflow-x-start', absent: true },
        { name: 'endSet', selector: ROOT, attribute: 'data-overflow-x-end' },
      ],
    });
    await inputInPage(page, 'click', '[data-testid="toggle-dir"]');
    const { t, inTask } = await waitForMarkers(page, ['dirApplied', 'jsVar', 'startCleared', 'endSet']);
    logFigure(`direction change, ${mode} +${delay} ms`, { ...since(t), inTask });
    expectOrder(t, ['input', 'dirApplied', 'jsVar', 'startCleared']);
    expectOrder(t, ['jsVar', 'endSet']);
    if (mode === 'wasm') {
      expect(inTask.dirApplied).toBe(true);
      expect(inTask.jsVar).toBe(true);
    } else {
      expect(inTask.dirApplied).toBe(false);
      // The click reaches C#, the render reaches the page and is acknowledged, the update reaches the module.
      expect(t.dirApplied! - t.input!).toBeGreaterThanOrEqual(legsFloor(delay, 4));
      expect(t.startCleared! - t.jsVar!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      expect(t.endSet! - t.jsVar!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // SCR-STA-01, SCR-STA-03, RND-STA-01: data-scrolling is the module's, written in the wheel's task on both modes; the
  // C# mirror (a ClassValue reading Scrolling) follows the report.
  perDelay("port: data-scrolling is written in the wheel's task and a ClassValue reading Scrolling follows the report", async ({ page, mode, delay }) => {
    await openMeasured(page, mode, { mirror: 'true' });
    const bar = page.getByTestId('scrollbar-vertical');
    const box = (await bar.boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await expect(bar).toHaveClass(/mirror-hovering/);
    await settle(page);
    await startWitness(page, {
      conditions: [
        { name: 'scrolling', selector: ROOT, attribute: 'data-scrolling' },
        { name: 'barScrolling', selector: BAR_Y, attribute: 'data-scrolling' },
        { name: 'mirror', selector: ROOT, className: 'mirror-scrolling' },
        { name: 'cleared', selector: ROOT, attribute: 'data-scrolling', absent: true, after: 'scrolling' },
        { name: 'mirrorCleared', selector: `${ROOT}:not(.mirror-scrolling)`, exists: true, after: 'mirror' },
      ],
    });
    await page.mouse.wheel(0, 100);
    const { t, inTask } = await waitForMarkers(page, ['scrolling', 'barScrolling', 'mirror', 'cleared', 'mirrorCleared']);
    logFigure(`mirrored Scrolling, ${mode} +${delay} ms`, { ...since(t), inTask });
    expect(inTask.scrolling).toBe(true);
    expect(inTask.barScrolling).toBe(true);
    expectOrder(t, ['input', 'scrolling', 'mirror']);
    expectOrder(t, ['cleared', 'mirrorCleared']);
    if (mode === 'wasm') {
      expect(inTask.mirror).toBe(true);
    } else {
      expect(inTask.mirror).toBe(false);
      expect(t.mirror! - t.scrolling!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
      expect(t.mirrorCleared! - t.cleared!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // SCR-STA-04, RND-STA-01: data-hovering is the module's, written in the pointer's task; the mirror follows the report.
  perDelay("port: data-hovering is written in the pointer's task and a ClassValue reading Hovering follows the report", async ({ page, mode, delay }) => {
    await openMeasured(page, mode, { mirror: 'true' });
    const box = (await page.getByTestId('root').boundingBox())!;
    await page.mouse.move(box.x + box.width + 100, box.y + 100);
    await startWitness(page, {
      conditions: [
        { name: 'hovering', selector: BAR_Y, attribute: 'data-hovering' },
        { name: 'mirror', selector: BAR_Y, className: 'mirror-hovering' },
      ],
    });
    await page.mouse.move(box.x + 100, box.y + 100);
    const { t, inTask } = await waitForMarkers(page, ['hovering', 'mirror']);
    logFigure(`mirrored Hovering, ${mode} +${delay} ms`, { ...since(t), inTask });
    expect(inTask.hovering).toBe(true);
    expectOrder(t, ['input', 'hovering', 'mirror']);
    if (mode === 'wasm') {
      expect(inTask.mirror).toBe(true);
    } else {
      expect(inTask.mirror).toBe(false);
      expect(t.mirror! - t.hovering!).toBeGreaterThanOrEqual(legsFloor(delay, 2));
    }
  });

  // ADR 01's frame-loop row, SCR-PTR-05: the drag, the wheel over a scrollbar and the track press move the viewport in the
  // input's task, and the thumb follows in the scroll event, on both modes and at any delay.
  perDelay('port: a thumb drag, a wheel over the scrollbar and a track press move the viewport in the task and the thumb in the scroll event', async ({ page, mode, delay }) => {
    await openMeasured(page, mode);
    const viewport = page.getByTestId('viewport');
    const conditions: Condition[] = [
      { name: 'transform', selector: THUMB_Y, style: 'transform', changed: true },
      { name: 'scrolling', selector: ROOT, attribute: 'data-scrolling' },
    ];
    const figures: Record<string, unknown> = {};
    const check = async (name: string, markers: readonly string[]) => {
      const { t, inTask, inScroll } = await waitForMarkers(page, ['scrolled', 'scroll', ...markers]);
      figures[name] = { ...since(t), inTask, inScroll };
      expect(inTask.scrolled, `${name}: the viewport moved in the input's task`).toBe(true);
      expect(inScroll.transform, `${name}: the thumb moved in the scroll event's task`).toBe(true);
      expectOrder(t, ['input', 'scrolled', 'scroll', 'transform']);
      if (markers.includes('scrolling')) {
        expect(inTask.scrolling, `${name}: data-scrolling in the input's task`).toBe(true);
      }
    };

    // The drag: press the thumb, then move.
    const thumb = (await page.getByTestId('thumb-vertical').boundingBox())!;
    await page.mouse.move(thumb.x + thumb.width / 2, thumb.y + 10);
    await page.mouse.down();
    await startWitness(page, { conditions, scrollProbe: ['pointermove'] });
    await page.mouse.move(thumb.x + thumb.width / 2, thumb.y + 40);
    await check('drag', ['transform', 'scrolling']);
    await page.mouse.up();
    await expect(page.getByTestId('root')).not.toHaveAttribute('data-scrolling');

    // The wheel over the scrollbar (the pointer already rests on it).
    await startWitness(page, { conditions, scrollProbe: ['wheel'] });
    await page.mouse.wheel(0, 100);
    await check('wheel', ['transform', 'scrolling']);
    await expect(page.getByTestId('root')).not.toHaveAttribute('data-scrolling');

    // The track press below the thumb.
    await viewport.evaluate((element) => {
      element.scrollTop = 0;
    });
    await expect(page.getByTestId('root')).not.toHaveAttribute('data-overflow-y-start');
    const bar = (await page.getByTestId('scrollbar-vertical').boundingBox())!;
    await page.mouse.move(bar.x + bar.width / 2, bar.y + 150);
    await startWitness(page, { conditions, scrollProbe: ['pointerdown'] });
    await page.mouse.down();
    await check('track press', ['transform', 'scrolling']);
    await page.mouse.up();

    logFigure(`frame loops, ${mode} +${delay} ms`, figures);
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// Deliverable 14 (issue 291): the circuit's messages and bytes per interaction, on Server at 0 ms.

interface CircuitMessage {
  direction: 'up' | 'down';
  kind: string;
  bytes: number;
}

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

const KINDS = [
  'JS.RenderBatch',
  'JS.BeginInvokeJS',
  'JS.EndInvokeDotNet',
  'JS.EndLocationChanging',
  'BeginInvokeDotNetFromJS',
  'EndInvokeJSFromDotNet',
  'OnRenderCompleted',
  'DispatchBrowserEvent',
  'UpdateRootComponents',
] as const;

function kindOf(message: Buffer | string): string {
  const text = typeof message === 'string' ? message : message.toString('latin1');
  const named = KINDS.find((kind) => text.includes(kind));
  if (named) {
    return named;
  }
  // blazorpack's ping is the one-element array [6].
  if (typeof message !== 'string' && message.length <= 4 && message.includes(0x06)) {
    return 'ping';
  }
  return 'other';
}

/** Records every SignalR message of the circuit, both ways, without delaying any. */
async function countCircuit(page: Page): Promise<CircuitMessage[]> {
  const log: CircuitMessage[] = [];
  const record = (direction: 'up' | 'down', frame: string | Buffer) => {
    for (const message of typeof frame === 'string' ? [frame] : splitMessages(frame)) {
      log.push({ direction, kind: kindOf(message), bytes: typeof message === 'string' ? Buffer.byteLength(message) : message.length });
    }
  };
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      record('up', message);
      server.send(message);
    });
    server.onMessage((message) => {
      record('down', message);
      socket.send(message);
    });
  });
  return log;
}

interface Side {
  messages: number;
  bytes: number;
  /** The messages of each kind. */
  kinds: Record<string, number>;
  /** The bytes of each kind, each message with its length prefix. */
  kindBytes: Record<string, number>;
}

interface Tally {
  up: Side;
  down: Side;
  renderBatches: number;
  pings: number;
}

/** The messages of a slice of the log, pings counted apart. */
function tally(messages: readonly CircuitMessage[]): Tally {
  const result: Tally = {
    up: { messages: 0, bytes: 0, kinds: {}, kindBytes: {} },
    down: { messages: 0, bytes: 0, kinds: {}, kindBytes: {} },
    renderBatches: 0,
    pings: 0,
  };
  for (const message of messages) {
    if (message.kind === 'ping') {
      result.pings += 1;
      continue;
    }
    const side = result[message.direction];
    side.messages += 1;
    side.bytes += message.bytes;
    side.kinds[message.kind] = (side.kinds[message.kind] ?? 0) + 1;
    side.kindBytes[message.kind] = (side.kindBytes[message.kind] ?? 0) + message.bytes;
    if (message.kind === 'JS.RenderBatch') {
      result.renderBatches += 1;
    }
  }
  return result;
}

/** The interaction's tally: its messages up to the barrier's end, minus the barrier's own (measured alone before). */
function minus(total: Tally, barrier: Tally): Tally {
  // A kind whose count the barrier accounts for entirely is left out, with its bytes.
  const side = (a: Side, b: Side): Side => {
    const kinds: Record<string, number> = {};
    const kindBytes: Record<string, number> = {};
    for (const kind of new Set([...Object.keys(a.kinds), ...Object.keys(b.kinds)])) {
      const count = (a.kinds[kind] ?? 0) - (b.kinds[kind] ?? 0);
      if (count !== 0) {
        kinds[kind] = count;
        kindBytes[kind] = (a.kindBytes[kind] ?? 0) - (b.kindBytes[kind] ?? 0);
      }
    }
    return { messages: a.messages - b.messages, bytes: a.bytes - b.bytes, kinds, kindBytes };
  };
  return {
    up: side(total.up, barrier.up),
    down: side(total.down, barrier.down),
    renderBatches: total.renderBatches - barrier.renderBatches,
    pings: total.pings,
  };
}

test.describe('ScrollArea: Server circuit figures', () => {
  for (const mirror of [false, true]) {
    const variant = mirror ? ', with ClassValue functions reading Scrolling and Hovering' : '';
    test(`port: figures${variant}`, async ({ page, mode }) => {
      test.skip(mode === 'wasm', 'WebAssembly has no circuit.');
      const log = await countCircuit(page);
      await openMeasured(page, mode, { mirror: String(mirror) });
      const root = page.getByTestId('root');
      const bar = page.getByTestId('scrollbar-vertical');
      const viewport = page.getByTestId('viewport');

      // The barrier alone, twice: its cost is subtracted from each figure, and the second shows the page is quiet.
      await settle(page);
      let mark = log.length;
      await settle(page);
      const barrier = tally(log.slice(mark));
      mark = log.length;
      await settle(page);
      expect(tally(log.slice(mark))).toEqual({ ...barrier, pings: tally(log.slice(mark)).pings });

      const measure = async (name: string, action: () => Promise<void>): Promise<Tally> => {
        const start = log.length;
        await action();
        await settle(page);
        const figure = minus(tally(log.slice(start)), barrier);
        logFigure(`${name}${mirror ? ' (mirror)' : ''}`, figure);
        return figure;
      };

      // A programmatic scroll across the top edge: one overflow report, and the render that projects the edge.
      const crossing = await measure('scroll crossing the top edge', async () => {
        await viewport.evaluate((element) => {
          element.scrollTop = 100;
        });
        await expect(root).toHaveAttribute('data-overflow-y-start');
      });
      expect(crossing.up.kinds.BeginInvokeDotNetFromJS).toBe(1);
      expect(crossing.renderBatches).toBeGreaterThanOrEqual(1);

      // A programmatic scroll through the middle: the module writes the variables and the thumb, and reports nothing.
      const middle = await measure('scroll through the middle', async () => {
        await viewport.evaluate((element) => {
          element.scrollTop = 300;
        });
        await expect.poll(() => inline(page, 'viewport', '--scroll-area-overflow-y-start')).toBe('300px');
      });
      expect(middle.up.messages).toBe(0);
      expect(middle.down.messages).toBe(0);

      // A hover enter and leave: one report each.
      const box = (await root.boundingBox())!;
      await page.mouse.move(box.x + box.width + 100, box.y + 100);
      const hover = await measure('hover enter and leave', async () => {
        await page.mouse.move(box.x + 100, box.y + 100);
        await expect(bar).toHaveAttribute('data-hovering');
        await page.mouse.move(box.x + box.width + 100, box.y + 100);
        await expect(bar).not.toHaveAttribute('data-hovering');
        if (mirror) {
          await expect(bar).not.toHaveClass(/mirror-hovering/);
        }
      });
      expect(hover.up.kinds.BeginInvokeDotNetFromJS).toBe(2);
      if (!mirror) {
        expect(hover.renderBatches).toBe(0);
      }

      // A user scroll (the wheel over the scrollbar, in the middle) that starts and clears after 500 ms: one report each.
      const barBox = (await bar.boundingBox())!;
      await page.mouse.move(barBox.x + barBox.width / 2, barBox.y + barBox.height / 2);
      await expect(bar).toHaveAttribute('data-hovering');
      await settle(page);
      const scrolling = await measure('scroll that starts and clears after 500 ms', async () => {
        await page.mouse.wheel(0, 50);
        await expect(root).toHaveAttribute('data-scrolling');
        await expect(root).not.toHaveAttribute('data-scrolling');
        if (mirror) {
          await expect(root).not.toHaveClass(/mirror-scrolling/);
        }
      });
      expect(scrolling.up.kinds.BeginInvokeDotNetFromJS).toBe(2);
      if (!mirror) {
        expect(scrolling.renderBatches).toBe(0);
      }
      logFigure(`barrier${mirror ? ' (mirror)' : ''}`, barrier);
    });
  }
});
