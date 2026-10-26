// L3 (ADR 06 §1): the `tabs` component module in jsdom against a fake twin, in its Server form and its `synchronous: true`
// (WebAssembly) form. Base UI tests this module's behaviour only in a browser (the "activation direction" and "rendering"
// blocks are `skipIf(isJSDOM)`), so the titles here are the port's: the activation and its decision point, the pressing
// state, the direction rules of the applied batch, the ownership of nested parts, the Indicator's arithmetic with stubbed
// layout (jsdom has none: `getBoundingClientRect`, `offset*`, `client*`, `scroll*` and the computed style are stubbed per
// element), its writes and its change-only report. The browser-only titles run at L4 (tests/browser/tabs.spec.ts).
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ACTIVE_ATTRIBUTE,
  DIRECTION_ATTRIBUTE,
  SELECT_POINT,
  attach,
  composeIndicatorStyle,
  computeActivationDirection,
  findActiveTab,
  findOwnPanels,
  findOwnTab,
  findOwnTabs,
  findRoot,
  getLayoutOffset,
  measureActiveTab,
  resolveTranslateLength,
  type TabsOptions,
} from './tabs';
import type { DecisionPolicy, DotNetTwin, TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];
const computed = new Map<Element, Record<string, string>>();

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  computed.clear();
  document.body.replaceChildren();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

/** Resolves after the microtasks queued so far: the `MutationObserver` callback of a batch runs in one of them. */
function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => queueMicrotask(resolve));
}

function listOptions(overrides: Partial<TabsOptions> = {}): string {
  return JSON.stringify({
    part: 'list',
    orientation: 'horizontal',
    activateOnFocus: false,
    controlled: false,
    reportClicks: false,
    reportFocus: false,
    ...overrides,
  } satisfies TabsOptions);
}

function indicatorOptions(overrides: Partial<TabsOptions> = {}): string {
  return JSON.stringify({
    part: 'indicator',
    orientation: 'horizontal',
    activateOnFocus: false,
    controlled: false,
    reportClicks: false,
    reportFocus: false,
    ...overrides,
  } satisfies TabsOptions);
}

interface SyncTwin extends FakeTwin {
  handedOver: { tabId: string; direction: string }[];
  calls: string[];
}

// The WebAssembly form: the fake twin answers `Ping` and `Decide`, and this wrapper records the hand-over before `select`.
function createSyncTwin(verdict: DecisionPolicy = 'run'): SyncTwin {
  const fake = createFakeTwin({ synchronous: true, verdict });
  const handedOver: { tabId: string; direction: string }[] = [];
  const calls: string[] = [];
  const twin: DotNetTwin = {
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      calls.push(methodName);
      if (methodName === 'OnSelecting') {
        handedOver.push(JSON.parse(args[0] as string) as { tabId: string; direction: string });
        return undefined as T;
      }
      return fake.twin.invokeMethod<T>(methodName, ...args);
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      return fake.twin.invokeMethodAsync<T>(methodName, ...args);
    },
  };
  return { ...fake, twin, handedOver, calls };
}

function rect(left: number, top: number, width: number, height: number): DOMRect {
  return { left, top, width, height, right: left + width, bottom: top + height, x: left, y: top, toJSON: () => ({}) } as DOMRect;
}

interface Layout {
  rect?: DOMRect;
  offsetLeft?: number;
  offsetTop?: number;
  offsetWidth?: number;
  offsetHeight?: number;
  offsetParent?: Element | null;
  clientLeft?: number;
  clientTop?: number;
  scrollLeft?: number;
  scrollTop?: number;
  scrollWidth?: number;
  scrollHeight?: number;
  style?: Record<string, string>;
}

// jsdom has no layout: each property is defined on the instance, and the computed style comes from `computed`.
function layout(element: HTMLElement, values: Layout): void {
  if (values.rect) {
    const box = values.rect;
    element.getBoundingClientRect = () => box;
  }
  for (const key of ['offsetLeft', 'offsetTop', 'offsetWidth', 'offsetHeight', 'offsetParent', 'clientLeft', 'clientTop', 'scrollLeft', 'scrollTop', 'scrollWidth', 'scrollHeight'] as const) {
    if (values[key] !== undefined) {
      Object.defineProperty(element, key, { configurable: true, value: values[key] });
    }
  }
  if (values.style) {
    computed.set(element, values.style);
  }
}

function stubComputedStyle(): void {
  const original = window.getComputedStyle.bind(window);
  vi.spyOn(window, 'getComputedStyle').mockImplementation((element: Element) => {
    const values = computed.get(element);
    if (!values) {
      return original(element);
    }
    return { transform: 'none', translate: 'none', ...values } as unknown as CSSStyleDeclaration;
  });
}

interface Tabs {
  root: HTMLElement;
  list: HTMLElement;
  tabs: HTMLElement[];
  panels: HTMLElement[];
}

// The DOM C# renders: a Root, a List, Tabs with ids, `role="tab"`, `data-active` on the active one, and kept Panels.
function renderTabs(count = 3, active = 0, options: { disabled?: number[] } = {}): Tabs {
  const root = document.createElement('div');
  root.setAttribute(DIRECTION_ATTRIBUTE, 'none');
  const list = document.createElement('div');
  list.setAttribute('role', 'tablist');
  list.setAttribute(DIRECTION_ATTRIBUTE, 'none');
  root.append(list);
  const tabs: HTMLElement[] = [];
  const panels: HTMLElement[] = [];
  for (let index = 0; index < count; index++) {
    const tab = document.createElement('button');
    tab.id = `tab-${index}`;
    tab.setAttribute('role', 'tab');
    tab.setAttribute(DIRECTION_ATTRIBUTE, 'none');
    tab.textContent = `Tab ${index}`;
    if (index === active) {
      tab.setAttribute(ACTIVE_ATTRIBUTE, '');
    }
    if (options.disabled?.includes(index)) {
      tab.setAttribute('data-disabled', '');
      tab.setAttribute('aria-disabled', 'true');
    }
    tab.getBoundingClientRect = () => rect(index * 100, 0, 100, 40);
    list.append(tab);
    tabs.push(tab);
    const panel = document.createElement('div');
    panel.setAttribute('role', 'tabpanel');
    panel.setAttribute(DIRECTION_ATTRIBUTE, 'none');
    root.append(panel);
    panels.push(panel);
  }
  document.body.append(root);
  return { root, list, tabs, panels };
}

function attachList(list: HTMLElement, twin: DotNetTwin, overrides: Partial<TabsOptions> = {}): TwinHandle {
  const handle = attach(list, listOptions(overrides), twin);
  handles.push(handle);
  return handle;
}

function attachIndicator(element: HTMLElement, twin: DotNetTwin, overrides: Partial<TabsOptions> = {}): TwinHandle {
  const handle = attach(element, indicatorOptions(overrides), twin);
  handles.push(handle);
  return handle;
}

// What C# renders when a Tab becomes active: the marker moves in one batch.
function commit(tabs: HTMLElement[], active: number | null): void {
  for (const [index, tab] of tabs.entries()) {
    tab.toggleAttribute(ACTIVE_ATTRIBUTE, index === active);
  }
}

function click(target: Element, init: MouseEventInit = {}): void {
  target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ...init }));
}

function focusIn(target: Element): void {
  target.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
}

function pointer(type: string, target: EventTarget, button = 0): void {
  // jsdom has no PointerEvent constructor in every version; a MouseEvent of the pointer type carries `button`.
  target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button }));
}

describe('the activation', () => {
  it('port: a click on an inactive tab reports the activation with the measured direction on Server', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    attachList(list, fake.twin);

    click(tabs[2]!);

    const [selection] = fake.reported<{ tabId: string; direction: string; event: { type: string; kind: string } }>('OnSelect');
    expect(fake.reported('OnSelect')).toHaveLength(1);
    expect(selection).toMatchObject({ tabId: 'tab-2', direction: 'right', event: { type: 'click', kind: 'MouseEvent' } });
    expect(fake.decisions).toEqual([]);
  });

  it('port: a click on an inactive tab hands the activation over and asks select inside the dispatch on WebAssembly', () => {
    const { list, tabs } = renderTabs(3, 2);
    const sync = createSyncTwin();
    attachList(list, sync.twin);

    click(tabs[0]!);

    expect(sync.handedOver).toEqual([{ tabId: 'tab-0', direction: 'left' }]);
    expect(sync.calls.filter((call) => call !== 'Ping')).toEqual(['OnSelecting', 'Decide']);
    expect(sync.decisions.map((decision) => [decision.point, decision.event.type])).toEqual([[SELECT_POINT, 'click']]);
    expect(sync.reported('OnSelect')).toEqual([]);
  });

  it('port: a click whose button is not the primary one activates nothing', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    attachList(list, fake.twin, { reportClicks: true });

    click(tabs[1]!, { button: 2 });
    click(tabs[1]!, { button: 1 });

    expect(fake.reports).toEqual([]);
  });

  it('port: a click on the active tab or a disabled tab asks nothing', () => {
    const { list, tabs } = renderTabs(3, 0, { disabled: [1] });
    const sync = createSyncTwin();
    attachList(list, sync.twin, { reportClicks: true });

    click(tabs[0]!);
    click(tabs[1]!);

    expect(sync.decisions).toEqual([]);
    expect(sync.handedOver).toEqual([]);
    // The active tab's click reaches the consumer's handler; the disabled tab's reaches nobody.
    expect(sync.reported<{ tabId: string }>('OnTabEvent').map((report) => report.tabId)).toEqual(['tab-0']);
  });

  it('port: a click on a child of a tab activates that tab', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    attachList(list, fake.twin);
    const label = document.createElement('span');
    tabs[1]!.append(label);

    click(label);

    expect(fake.reported<{ tabId: string }>('OnSelect').map((report) => report.tabId)).toEqual(['tab-1']);
  });

  it('port: a focus activates an inactive tab only with activateOnFocus', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    const handle = attachList(list, fake.twin);

    focusIn(tabs[1]!);
    expect(fake.reported('OnSelect')).toEqual([]);

    handle.update(listOptions({ activateOnFocus: true }));
    focusIn(tabs[1]!);

    const [selection] = fake.reported<{ tabId: string; event: { type: string; kind: string } }>('OnSelect');
    expect(selection).toMatchObject({ tabId: 'tab-1', event: { type: 'focusin', kind: 'FocusEvent' } });
  });

  it('port: a held non-primary press keeps its focus from activating until a pointerup on the document', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    attachList(list, fake.twin, { activateOnFocus: true });

    pointer('pointerdown', tabs[1]!, 2);
    focusIn(tabs[1]!);
    expect(fake.reported('OnSelect')).toEqual([]);

    pointer('pointerup', document);
    focusIn(tabs[1]!);
    expect(fake.reported<{ tabId: string }>('OnSelect').map((report) => report.tabId)).toEqual(['tab-1']);
  });

  it('port: a cancelled non-primary press releases the pressing state', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    attachList(list, fake.twin, { activateOnFocus: true });

    pointer('pointerdown', tabs[2]!, 1);
    pointer('pointercancel', tabs[2]!);
    focusIn(tabs[2]!);

    expect(fake.reported<{ tabId: string }>('OnSelect').map((report) => report.tabId)).toEqual(['tab-2']);
  });

  it('port: a primary press lets its focus activate the tab', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    attachList(list, fake.twin, { activateOnFocus: true });

    pointer('pointerdown', tabs[1]!, 0);
    focusIn(tabs[1]!);

    expect(fake.reported<{ tabId: string }>('OnSelect').map((report) => report.tabId)).toEqual(['tab-1']);
  });

  it('port: a focus that activates nothing is reported only while asked, a disabled tab included', () => {
    const { list, tabs } = renderTabs(3, 0, { disabled: [2] });
    const fake = createFakeTwin();
    const handle = attachList(list, fake.twin, { activateOnFocus: true });

    focusIn(tabs[0]!);
    focusIn(tabs[2]!);
    expect(fake.reports).toEqual([]);

    handle.update(listOptions({ activateOnFocus: true, reportFocus: true }));
    focusIn(tabs[0]!);
    focusIn(tabs[2]!);

    expect(fake.reported<{ tabId: string; event: { type: string } }>('OnTabEvent')).toMatchObject([
      { tabId: 'tab-0', event: { type: 'focusin' } },
      { tabId: 'tab-2', event: { type: 'focusin' } },
    ]);
    expect(fake.reported('OnSelect')).toEqual([]);
  });

  it('port: a nested list owns its own tabs', () => {
    const outer = renderTabs(2, 0);
    const nested = document.createElement('div');
    nested.setAttribute('role', 'tablist');
    const nestedTab = document.createElement('button');
    nestedTab.setAttribute('role', 'tab');
    nestedTab.id = 'nested-tab';
    nested.append(nestedTab);
    outer.list.append(nested);
    const fake = createFakeTwin();
    attachList(outer.list, fake.twin, { reportClicks: true });

    click(nestedTab);

    expect(fake.reports).toEqual([]);
    expect(findOwnTab(outer.list, nestedTab)).toBeNull();
    expect(findOwnTabs(outer.list).map((tab) => tab.id)).toEqual(['tab-0', 'tab-1']);
  });

  it('port: a repeated attach of the same element returns the live handle, and dispose is idempotent', () => {
    const { list, tabs } = renderTabs();
    const fake = createFakeTwin();
    const handle = attachList(list, fake.twin);

    expect(attach(list, listOptions(), fake.twin)).toBe(handle);
    handle.dispose();
    handle.dispose();
    click(tabs[1]!);

    expect(fake.reports).toEqual([]);
  });
});

describe('the activation direction', () => {
  it('port: horizontal tabs compare left, vertical tabs compare top, equal positions give none', () => {
    const a = document.createElement('button');
    const b = document.createElement('button');
    a.getBoundingClientRect = () => rect(0, 0, 50, 20);
    b.getBoundingClientRect = () => rect(80, 20, 50, 20);

    expect(computeActivationDirection(a, b, 'horizontal')).toBe('right');
    expect(computeActivationDirection(b, a, 'horizontal')).toBe('left');
    expect(computeActivationDirection(a, b, 'vertical')).toBe('down');
    expect(computeActivationDirection(b, a, 'vertical')).toBe('up');
    expect(computeActivationDirection(a, a, 'horizontal')).toBe('none');
    expect(computeActivationDirection(null, b, 'horizontal')).toBe('none');
    expect(computeActivationDirection(a, null, 'vertical')).toBe('none');
  });

  it('port: the direction is physical, so the next tab of an rtl list is to the left', () => {
    const { list, tabs } = renderTabs(2, 0);
    // An rtl list lays the first tab out on the right.
    tabs[0]!.getBoundingClientRect = () => rect(100, 0, 100, 40);
    tabs[1]!.getBoundingClientRect = () => rect(0, 0, 100, 40);
    const fake = createFakeTwin();
    attachList(list, fake.twin);

    click(tabs[1]!);

    expect(fake.reported<{ direction: string }>('OnSelect')[0]?.direction).toBe('left');
  });

  it('port: the batch that moves data-active writes the measured direction on every part, a panel it creates included, and reports it once', async () => {
    const { root, list, tabs, panels } = renderTabs();
    const indicator = document.createElement('span');
    indicator.setAttribute(DIRECTION_ATTRIBUTE, 'none');
    list.append(indicator);
    const fake = createFakeTwin();
    attachList(list, fake.twin);
    attachIndicator(indicator, fake.twin);

    click(tabs[1]!);
    // C#'s render of the commit, a round trip later on Server: the marker moves and a panel is created with C#'s value.
    commit(tabs, 1);
    const created = document.createElement('div');
    created.setAttribute('role', 'tabpanel');
    created.setAttribute(DIRECTION_ATTRIBUTE, 'none');
    root.append(created);
    await flushMicrotasks();

    for (const part of [root, list, ...tabs, ...panels, created, indicator]) {
      expect(part.getAttribute(DIRECTION_ATTRIBUTE)).toBe('right');
    }
    expect(fake.reported('OnDirectionChange')).toEqual([{ direction: 'right' }]);

    // A batch that does not move the marker reports nothing.
    tabs[0]!.setAttribute('data-disabled', '');
    await flushMicrotasks();
    expect(fake.reported('OnDirectionChange')).toEqual([{ direction: 'right' }]);
  });

  it('port: a change the module did not start is none in an uncontrolled root', async () => {
    const { list, tabs } = renderTabs(3, 2);
    const fake = createFakeTwin();
    attachList(list, fake.twin);

    // An automatic fallback: the selected tab was disabled, C# moved the marker to the first enabled tab.
    tabs[2]!.setAttribute('data-disabled', '');
    commit(tabs, 0);
    await flushMicrotasks();

    expect(list.getAttribute(DIRECTION_ATTRIBUTE)).toBe('none');
    expect(fake.reported('OnDirectionChange')).toEqual([]);
  });

  it('port: a change the module did not start is positional in a controlled root', async () => {
    const { list, tabs } = renderTabs(3, 0);
    const fake = createFakeTwin();
    attachList(list, fake.twin, { controlled: true });

    commit(tabs, 2);
    await flushMicrotasks();
    expect(list.getAttribute(DIRECTION_ATTRIBUTE)).toBe('right');

    commit(tabs, 1);
    await flushMicrotasks();
    expect(list.getAttribute(DIRECTION_ATTRIBUTE)).toBe('left');
    expect(fake.reported('OnDirectionChange')).toEqual([{ direction: 'right' }, { direction: 'left' }]);
  });

  it('port: a tab added and selected in one batch takes the positional direction at once', async () => {
    const { list, tabs } = renderTabs(2, 1);
    const fake = createFakeTwin();
    attachList(list, fake.twin, { controlled: true });

    const added = document.createElement('button');
    added.setAttribute('role', 'tab');
    added.id = 'tab-new';
    added.getBoundingClientRect = () => rect(200, 0, 100, 40);
    added.setAttribute(ACTIVE_ATTRIBUTE, '');
    tabs[1]!.removeAttribute(ACTIVE_ATTRIBUTE);
    list.append(added);
    await flushMicrotasks();

    expect(added.getAttribute(DIRECTION_ATTRIBUTE)).toBe('right');
    expect(fake.reported('OnDirectionChange')).toEqual([{ direction: 'right' }]);
  });

  it('port: clearing and restoring the selection is none', async () => {
    const { list, tabs } = renderTabs(3, 0);
    const fake = createFakeTwin();
    attachList(list, fake.twin, { controlled: true });

    commit(tabs, 2);
    await flushMicrotasks();
    commit(tabs, null);
    await flushMicrotasks();
    expect(list.getAttribute(DIRECTION_ATTRIBUTE)).toBe('none');

    commit(tabs, 1);
    await flushMicrotasks();
    expect(list.getAttribute(DIRECTION_ATTRIBUTE)).toBe('none');
    expect(fake.reported('OnDirectionChange')).toEqual([{ direction: 'right' }, { direction: 'none' }]);
  });

  it('port: a vetoed activation writes nothing, and a later programmatic change measures from the actual previous tab', async () => {
    const { list, tabs } = renderTabs(3, 1);
    const sync = createSyncTwin('stop');
    attachList(list, sync.twin, { controlled: true });

    click(tabs[2]!);
    await flushMicrotasks();
    expect(list.getAttribute(DIRECTION_ATTRIBUTE)).toBe('none');

    commit(tabs, 0);
    await flushMicrotasks();
    expect(list.getAttribute(DIRECTION_ATTRIBUTE)).toBe('left');
  });

  it('port: the root is the nearest part ancestor and the panels of a nested root are not its own', () => {
    const outer = renderTabs(2, 0);
    const inner = renderTabs(2, 0);
    outer.panels[0]!.append(inner.root);

    expect(findRoot(outer.list)).toBe(outer.root);
    expect(findRoot(inner.list)).toBe(inner.root);
    expect(findOwnPanels(outer.root)).toEqual(outer.panels);
    expect(findOwnPanels(inner.root)).toEqual(inner.panels);
    expect(findActiveTab(outer.list)).toBe(outer.tabs[0]);
  });
});

describe('the indicator', () => {
  // A border-box List at (10, 20) of 402 x 42 with a 1 px border, laid out at offset (10, 20) in the body; the Tab at
  // index 1 is 100 x 38 at layout offset (101, 1) inside it.
  function measuredTabs(): { list: HTMLElement; tab: HTMLElement } {
    stubComputedStyle();
    const { list, tabs } = renderTabs(3, 1);
    layout(list, {
      rect: rect(10, 20, 402, 42),
      offsetLeft: 10,
      offsetTop: 20,
      offsetParent: null,
      clientLeft: 1,
      clientTop: 1,
      scrollLeft: 0,
      scrollTop: 0,
      scrollWidth: 400,
      scrollHeight: 40,
      style: { width: '402px', height: '42px' },
      offsetWidth: 402,
      offsetHeight: 42,
    });
    const tab = tabs[1]!;
    layout(tab, {
      rect: rect(111, 21, 100, 38),
      offsetLeft: 101,
      offsetTop: 1,
      offsetParent: list,
      offsetWidth: 100,
      offsetHeight: 38,
      style: { width: '100px', height: '38px' },
    });
    return { list, tab };
  }

  it('port: the rect offset is used when it agrees with the layout offset, with right and bottom against the scrollable content', () => {
    const { list, tab } = measuredTabs();
    // Layout: (101 + 1 + 10) - 10 - 1 = 101; rect: (111 - 10) / 1 + 0 - 1 = 100, which agrees within 2 px and wins.
    const measurement = measureActiveTab(tab, list);

    expect(measurement.size).toEqual({ width: 100, height: 38 });
    expect(measurement.position.left).toBe(100);
    expect(measurement.position.top).toBe(0);
    expect(measurement.position.right).toBe(400 - 100 - 100);
    expect(measurement.position.bottom).toBe(40 - 0 - 38);
  });

  it('port: a distorted rect falls back to the layout offset, as under an ancestor rotation', () => {
    const { list, tab } = measuredTabs();
    tab.getBoundingClientRect = () => rect(160, 60, 100, 38);

    const measurement = measureActiveTab(tab, list);

    expect(measurement.position.left).toBe(getLayoutOffset(tab, list).left);
    expect(measurement.position.top).toBe(getLayoutOffset(tab, list).top);
  });

  it('port: a list scaled to zero keeps the layout offset and the CSS size', () => {
    const { list, tab } = measuredTabs();
    list.getBoundingClientRect = () => rect(10, 20, 0, 0);
    tab.getBoundingClientRect = () => rect(10, 20, 0, 0);

    const measurement = measureActiveTab(tab, list);

    expect(measurement.size).toEqual({ width: 100, height: 38 });
    expect(measurement.position.left).toBe(getLayoutOffset(tab, list).left);
    expect(Number.isFinite(measurement.position.left)).toBe(true);
  });

  it("port: the tab's own translation is removed from the comparison, so the indicator follows a translated tab", () => {
    const { list, tab } = measuredTabs();
    tab.getBoundingClientRect = () => rect(123, 21, 100, 38);
    computed.set(tab, { width: '100px', height: '38px', transform: 'matrix(1, 0, 0, 1, 12, 0)' });

    expect(measureActiveTab(tab, list).position.left).toBe(112);

    computed.set(tab, { width: '100px', height: '38px', translate: '12px' });
    expect(measureActiveTab(tab, list).position.left).toBe(112);
  });

  it('port: a translate longhand resolves percentages against the border box and ignores calc()', () => {
    expect(resolveTranslateLength('10%', 200)).toBe(20);
    expect(resolveTranslateLength('-4.5px', 200)).toBe(-4.5);
    expect(resolveTranslateLength('calc(10px + 5%)', 200)).toBe(0);
    expect(resolveTranslateLength(undefined, 200)).toBe(0);
  });

  it('port: a scrolled container between the list and the tab is subtracted, the list scroll is not', () => {
    const { list, tab } = measuredTabs();
    const container = document.createElement('div');
    list.insertBefore(container, tab);
    container.append(tab);
    layout(container, { scrollLeft: 30, scrollTop: 0 });
    layout(list, { scrollLeft: 50 });

    expect(getLayoutOffset(tab, list).left).toBe(101 - 30);
  });

  it("port: the six variables come before the consumer's declarations, which win", () => {
    const measurement = { position: { left: 1.5, right: 2, top: 3, bottom: 4 }, size: { width: 50.25, height: 6 } };

    expect(composeIndicatorStyle(measurement, 'color: red; --active-tab-left: 0px')).toBe(
      '--active-tab-left: 1.5px; --active-tab-right: 2px; --active-tab-top: 3px; --active-tab-bottom: 4px; --active-tab-width: 50.25px; --active-tab-height: 6px; color: red; --active-tab-left: 0px',
    );
    expect(composeIndicatorStyle(null, 'color: red')).toBe('color: red');
    expect(composeIndicatorStyle(null, null)).toBe('');
  });

  it('port: the indicator shows itself with the variables once the active tab measures, and reports each change once', async () => {
    const { list, tab } = measuredTabs();
    const indicator = document.createElement('span');
    indicator.setAttribute('hidden', '');
    list.append(indicator);
    const fake = createFakeTwin();
    attachList(list, fake.twin);
    attachIndicator(indicator, fake.twin, { style: 'color: red' });

    expect(indicator.hasAttribute('hidden')).toBe(false);
    expect(indicator.style.getPropertyValue('--active-tab-left')).toBe('100px');
    expect(indicator.style.getPropertyValue('--active-tab-width')).toBe('100px');
    expect(indicator.style.getPropertyValue('color')).toBe('red');
    expect(fake.reported('OnMeasure')).toEqual([{ position: { left: 100, right: 200, top: 0, bottom: 2 }, size: { width: 100, height: 38 } }]);

    // An unchanged measurement is not reported again; a changed one is, once.
    list.setAttribute('data-unrelated', '');
    tab.removeAttribute(ACTIVE_ATTRIBUTE);
    tab.setAttribute(ACTIVE_ATTRIBUTE, '');
    await flushMicrotasks();
    expect(fake.reported('OnMeasure')).toHaveLength(1);

    layout(tab, { style: { width: '120px', height: '38px' }, offsetWidth: 120 });
    tab.removeAttribute(ACTIVE_ATTRIBUTE);
    tab.setAttribute(ACTIVE_ATTRIBUTE, '');
    await flushMicrotasks();
    expect(fake.reported('OnMeasure')).toHaveLength(2);
    expect(indicator.style.getPropertyValue('--active-tab-width')).toBe('120px');
  });

  it("port: the indicator stays hidden without an active tab or with a zero size, and never writes over the consumer's hidden", async () => {
    const { list, tab } = measuredTabs();
    const indicator = document.createElement('span');
    list.append(indicator);
    const fake = createFakeTwin();
    attachList(list, fake.twin);
    const handle = attachIndicator(indicator, fake.twin);

    tab.removeAttribute(ACTIVE_ATTRIBUTE);
    await flushMicrotasks();
    expect(indicator.hasAttribute('hidden')).toBe(true);
    expect(indicator.style.getPropertyValue('--active-tab-left')).toBe('');
    expect(fake.reported('OnMeasure').at(-1)).toEqual({ position: null, size: null });

    tab.setAttribute(ACTIVE_ATTRIBUTE, '');
    layout(tab, { style: { width: '0px', height: '38px' }, offsetWidth: 0 });
    await flushMicrotasks();
    expect(indicator.hasAttribute('hidden')).toBe(true);
    expect(indicator.style.getPropertyValue('--active-tab-width')).toBe('0px');

    handle.update(indicatorOptions({ hidden: false }));
    expect(indicator.hasAttribute('hidden')).toBe(false);
  });

  it('port: an indicator attached before its list adopts the list when the list attaches', () => {
    const { list } = measuredTabs();
    const indicator = document.createElement('span');
    list.append(indicator);
    const fake = createFakeTwin();

    attachIndicator(indicator, fake.twin);
    expect(indicator.hasAttribute('hidden')).toBe(true);

    attachList(list, fake.twin);
    expect(indicator.hasAttribute('hidden')).toBe(false);
    expect(indicator.getAttribute(DIRECTION_ATTRIBUTE)).toBe('none');
  });

  it('port: the list observes itself and every tab it owns, a tab inserted later included, and every indicator re-measures on a notification', async () => {
    const observed: Element[] = [];
    const callbacks: ResizeObserverCallback[] = [];
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: ResizeObserverCallback) {
          callbacks.push(callback);
        }
        observe(target: Element) {
          observed.push(target);
        }
        disconnect() {}
        unobserve() {}
      },
    );
    const { list, tab } = measuredTabs();
    const first = document.createElement('span');
    const second = document.createElement('span');
    list.append(first, second);
    const fake = createFakeTwin();
    attachList(list, fake.twin);
    attachIndicator(first, fake.twin);
    attachIndicator(second, fake.twin);

    expect(observed).toEqual([list, ...findOwnTabs(list)]);

    const inserted = document.createElement('button');
    inserted.setAttribute('role', 'tab');
    inserted.id = 'tab-inserted';
    list.append(inserted);
    await flushMicrotasks();
    expect(observed.at(-1)).toBe(inserted);

    layout(tab, { style: { width: '140px', height: '38px' }, offsetWidth: 140 });
    callbacks[0]!([{ target: list } as unknown as ResizeObserverEntry], {} as ResizeObserver);

    expect(first.style.getPropertyValue('--active-tab-width')).toBe('140px');
    expect(second.style.getPropertyValue('--active-tab-width')).toBe('140px');
  });

  it('port: without ResizeObserver the list installs no observer and still measures on its batches', async () => {
    vi.stubGlobal('ResizeObserver', undefined);
    const { list, tab } = measuredTabs();
    const indicator = document.createElement('span');
    list.append(indicator);
    const fake = createFakeTwin();
    attachList(list, fake.twin);
    attachIndicator(indicator, fake.twin);

    layout(tab, { style: { width: '130px', height: '38px' }, offsetWidth: 130 });
    tab.removeAttribute(ACTIVE_ATTRIBUTE);
    tab.setAttribute(ACTIVE_ATTRIBUTE, '');
    await flushMicrotasks();

    expect(indicator.style.getPropertyValue('--active-tab-width')).toBe('130px');
  });
});
