// L3 (ADR 06 §1): the `composite` module in jsdom against a fake twin. Titles are Base UI's where a Base UI test observes
// what this module does (floating-ui-react/utils/composite.test.ts, internals/composite/composite.test.ts,
// CompositeList.test.tsx, CompositeRoot.test.tsx), kept verbatim, "outside React" included; the port's own titles cover
// the RadioGroup options (the pending-navigation flag, the focus-driven selection, the leaving blur), both decision
// points per render mode, the reports, the tabindex the module owns from attach (ADR 04 Amendments 2026-10-22) and the
// rows whose cited title needs a later shape (a consumer-owned highlight, `onLoop`, grid; CMP §10 Q1).
//
// Two drivers, as the module has two halves:
// - the item registry (CMP-ALG-17) through `createCompositeRegistry`, keyed by element, which carries the explicit
//   indexes and metadata that exist on the JavaScript side only (CMP-STA-01/04/08);
// - the root through `attach` and `update`, as C# drives it: items by element id, the options of the latest `update`.
// A Base UI test that renders React components is restated over the same DOM, with the module doing what the React
// hooks did; where the restatement changes what the title observes, the test says so (dated notes in CMP).
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ACTIVE_COMPOSITE_ITEM,
  NAVIGATE_POINT,
  SELECT_POINT,
  attach,
  createCompositeRegistry,
  findNonDisabledListIndex,
  getFallbackIndex,
  getMaxListIndex,
  getMinListIndex,
  getNextListIndex,
  isElementVisible,
  isHiddenByStyles,
  isIndexOutOfListBounds,
  isListIndexDisabled,
  isModifierKeySet,
  scrollIntoViewIfNeeded,
  type CompositeItemRegistration,
  type CompositeMetadata,
  type CompositeOptions,
  type CompositeRegistry,
  type ListStepOptions,
} from './composite';
import type { DecisionPolicy, DotNetTwin, TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin, type FakeTwinOptions } from './test-support/fake-twin';

const isJSDOM = /jsdom/.test(window.navigator.userAgent);

const handles: TwinHandle[] = [];
const registries: CompositeRegistry[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  for (const registry of registries.splice(0)) {
    registry.dispose();
  }
  document.body.replaceChildren();
  vi.restoreAllMocks();
});

/** Resolves after the microtasks queued so far, as `flushMicrotasks` of Base UI's test utilities. */
function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => queueMicrotask(resolve));
}

/** Resolves on the next task, after every microtask (and `MutationObserver` callback) queued so far. */
function nextTask(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function keyDown(target: EventTarget, key: string, init: KeyboardEventInit = {}): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, composed: true, ...init });
  target.dispatchEvent(event);
  return event;
}

// --- the root through attach ----------------------------------------------------------------------------------------

const defaults: CompositeOptions = {
  orientation: 'both',
  loopFocus: true,
  enableHomeAndEndKeys: false,
  stopEventPropagation: true,
  highlightItemOnHover: false,
  modifierKeys: [],
  direction: 'ltr',
  items: [],
  selectOnNavigation: false,
  reportFocusLeave: false,
  reportHighlight: false,
};

let nextId = 0;

/** An item element as a composite item part renders it: an id and the initial `tabindex="-1"` (CMP-STA-15). */
function item(testId: string, attributes: Record<string, string> = {}, tag: keyof HTMLElementTagNameMap = 'div'): HTMLElement {
  const element = document.createElement(tag);
  element.id = `composite-item-${(nextId += 1)}`;
  element.setAttribute('data-testid', testId);
  element.setAttribute('tabindex', '-1');
  element.textContent = testId;
  for (const [name, value] of Object.entries(attributes)) {
    element.setAttribute(name, value);
  }
  return element;
}

function byTestId(testId: string): HTMLElement {
  const element = document.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  if (!element) {
    throw new Error(`no element with data-testid "${testId}"`);
  }
  return element;
}

/** The registrations C# sends: one per item element, by id, in the order given. */
function registrations(elements: HTMLElement[], tabIndexes: Record<string, string> = {}): CompositeOptions['items'] {
  return elements.map((element) => ({ id: element.id, tabIndex: tabIndexes[element.id] ?? null }));
}

/** The items currently under the root, in document order, as C# would register them. */
function itemsUnder(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>('[data-testid]')).filter((element) => element.id.startsWith('composite-item-'));
}

interface Group {
  root: HTMLElement;
  handle: TwinHandle;
  fake: FakeTwin;
  options: CompositeOptions;
  /** Sends `update` with the options changed by `overrides`; the items default to those under the root. */
  update(overrides?: Partial<CompositeOptions>): void;
}

/** Twin options of `createFakeTwin`, plus a verdict per decision point for the synchronous (WebAssembly) path. */
interface GroupTwin extends FakeTwinOptions {
  verdicts?: Partial<Record<string, DecisionPolicy>>;
}

function withVerdicts(fake: FakeTwin, verdicts: Partial<Record<string, DecisionPolicy>>): DotNetTwin {
  return {
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      const verdict = fake.twin.invokeMethod<T>(methodName, ...args);
      return methodName === 'Decide' ? ((verdicts[args[0] as string] ?? verdict) as T) : verdict;
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      return fake.twin.invokeMethodAsync<T>(methodName, ...args);
    },
  };
}

/**
 * Renders a root element holding `content` (item elements and anything else), attaches the module with the items found
 * under the root, as C# does after the root's first render.
 */
function mountGroup(content: Node[], overrides: Partial<CompositeOptions> = {}, twin: GroupTwin = {}, parent: Node = document.body): Group {
  const root = document.createElement('div');
  root.id = `composite-root-${(nextId += 1)}`;
  root.append(...content);
  parent.appendChild(root);
  const fake = createFakeTwin(twin);
  const group: Group = {
    root,
    fake,
    options: { ...defaults, items: registrations(itemsUnder(root)), ...overrides },
    handle: undefined as unknown as TwinHandle,
    update(changes = {}) {
      group.options = { ...group.options, items: registrations(itemsUnder(root)), ...changes };
      group.handle.update(JSON.stringify(group.options));
    },
  };
  group.handle = attach(root, JSON.stringify(group.options), twin.verdicts ? withVerdicts(fake, twin.verdicts) : fake.twin);
  handles.push(group.handle);
  return group;
}

function mountItems(testIds: string[], overrides: Partial<CompositeOptions> = {}, twin: GroupTwin = {}): Group {
  return mountGroup(
    testIds.map((testId) => item(testId)),
    overrides,
    twin,
  );
}

// --- floating-ui-react/utils/composite.test.ts (CMP-ALG-01..06, CMP-ARIA-03) ------------------------------------------

describe('floating-ui-react/utils/composite', () => {
  it('treats hidden visibility styles as hidden', () => {
    const hidden = document.createElement('div');
    const collapsed = document.createElement('div');
    const visible = document.createElement('div');

    hidden.style.visibility = 'hidden';
    collapsed.style.visibility = 'collapse';

    document.body.append(hidden, collapsed, visible);

    expect(isHiddenByStyles(getComputedStyle(hidden))).toBe(true);
    expect(isHiddenByStyles(getComputedStyle(collapsed))).toBe(true);
    expect(isHiddenByStyles(getComputedStyle(visible))).toBe(false);
  });

  it('uses CSS visibility fallbacks when checkVisibility is unavailable', () => {
    const visible = document.createElement('button');
    const displayHidden = document.createElement('button');
    const displayContents = document.createElement('button');
    const contentHidden = document.createElement('button');

    displayHidden.style.display = 'none';
    displayContents.style.display = 'contents';
    contentHidden.style.setProperty('content-visibility', 'hidden');
    Object.defineProperty(displayContents, 'checkVisibility', {
      configurable: true,
      value: undefined,
    });
    Object.defineProperty(contentHidden, 'checkVisibility', {
      configurable: true,
      value: undefined,
    });
    document.body.append(visible, displayHidden, displayContents, contentHidden);

    expect(isElementVisible(visible)).toBe(true);
    expect(isElementVisible(displayHidden)).toBe(false);
    expect(isElementVisible(displayContents)).toBe(false);
    expect(isElementVisible(contentHidden)).toBe(true);
  });

  it('always treats natively disabled elements as disabled, unlike aria-disabled ones', () => {
    const nativeDisabled = document.createElement('button');
    nativeDisabled.disabled = true;
    const ariaDisabled = document.createElement('button');
    ariaDisabled.setAttribute('aria-disabled', 'true');

    document.body.append(nativeDisabled, ariaDisabled);
    const list = [nativeDisabled, ariaDisabled];

    expect(isListIndexDisabled(list, 0)).toBe(true);
    expect(isListIndexDisabled(list, 1)).toBe(true);

    // An empty `disabledIndices` marks every item as enabled, but natively
    // disabled elements can never receive focus, so they stay disabled.
    expect(isListIndexDisabled(list, 0, [])).toBe(true);
    expect(isListIndexDisabled(list, 1, [])).toBe(false);

    expect(isListIndexDisabled(list, 0, () => false)).toBe(true);
    expect(isListIndexDisabled(list, 1, () => false)).toBe(false);
  });

  describe('getNextListIndex', () => {
    function items(count: number) {
      return Array.from({ length: count }, () => document.body.appendChild(document.createElement('div')));
    }

    const step = { loopFocus: true, allowEscape: false };

    function nextIndex(list: HTMLElement[], currentIndex: number, options: Omit<ListStepOptions, 'minIndex' | 'maxIndex'>) {
      return getNextListIndex(list, currentIndex, {
        ...options,
        minIndex: getMinListIndex(list, options.disabledIndices),
        maxIndex: getMaxListIndex(list, options.disabledIndices),
      });
    }

    it('steps to the adjacent index', () => {
      expect(nextIndex(items(3), 0, { ...step, decrement: false })).toEqual({
        index: 1,
        wrapped: false,
      });
      expect(nextIndex(items(3), 1, { ...step, decrement: true })).toEqual({
        index: 0,
        wrapped: false,
      });
    });

    it('wraps at either end when looping', () => {
      expect(nextIndex(items(3), 2, { ...step, decrement: false })).toEqual({
        index: 0,
        wrapped: true,
      });
      expect(nextIndex(items(3), 0, { ...step, decrement: true })).toEqual({
        index: 2,
        wrapped: true,
      });
    });

    it('stays at either end without looping', () => {
      const options = { loopFocus: false, allowEscape: false };
      expect(nextIndex(items(3), 2, { ...options, decrement: false }).index).toBe(2);
      expect(nextIndex(items(3), 0, { ...options, decrement: true }).index).toBe(0);
    });

    it('leaves the list at either end when escaping is allowed', () => {
      const options = { loopFocus: true, allowEscape: true };
      expect(nextIndex(items(3), 2, { ...options, decrement: false }).index).toBe(-1);
      expect(nextIndex(items(3), 0, { ...options, decrement: true }).index).toBe(-1);
    });

    it('enters the list at the far end from outside it', () => {
      const options = { loopFocus: true, allowEscape: true };
      expect(nextIndex(items(3), -1, { ...options, decrement: false }).index).toBe(0);
      expect(nextIndex(items(3), -1, { ...options, decrement: true }).index).toBe(2);
    });

    it('skips disabled indices', () => {
      expect(nextIndex(items(4), 0, { ...step, decrement: false, disabledIndices: [1, 2] }).index).toBe(3);
    });
  });

  // Port titles: the helpers CMP §5.1 and §5.3 transcribe that no Base UI test calls directly.
  it('reports an index out of bounds below zero and at the length (CMP-ALG-01)', () => {
    const list = [document.createElement('div'), document.createElement('div')];
    expect([-1, 0, 1, 2].map((index) => isIndexOutOfListBounds(list, index))).toEqual([true, false, false, true]);
  });

  it('returns out-of-bounds min and max when every item is disabled, and 0 and -1 for an empty list (CMP-ALG-05)', () => {
    const disabled = [document.createElement('button'), document.createElement('button')];
    for (const element of disabled) {
      element.disabled = true;
      document.body.append(element);
    }

    expect([getMinListIndex(disabled), getMaxListIndex(disabled)]).toEqual([2, -1]);
    expect([getMinListIndex([]), getMaxListIndex([])]).toEqual([0, -1]);
    expect(findNonDisabledListIndex(disabled, { startingIndex: 0, decrement: true })).toBe(-1);
  });

  it('reads each of the four modifiers unless it is allowed (CMP-ALG-16)', () => {
    for (const [init, modifier] of [
      [{ shiftKey: true }, 'Shift'],
      [{ ctrlKey: true }, 'Control'],
      [{ altKey: true }, 'Alt'],
      [{ metaKey: true }, 'Meta'],
    ] as const) {
      const event = new KeyboardEvent('keydown', { key: 'ArrowDown', ...init });
      expect(isModifierKeySet(event, [])).toBe(true);
      expect(isModifierKeySet(event, [modifier])).toBe(false);
    }
    expect(isModifierKeySet(new KeyboardEvent('keydown', { key: 'ArrowDown' }), [])).toBe(false);
  });

  it('falls back to the first enabled marked item, then the first enabled item, then 0 (CMP-ALG-15)', () => {
    const plain = document.createElement('div');
    const marked = document.createElement('div');
    marked.setAttribute(ACTIVE_COMPOSITE_ITEM, '');
    const disabledMarked = document.createElement('div');
    disabledMarked.setAttribute(ACTIVE_COMPOSITE_ITEM, '');
    disabledMarked.setAttribute('aria-disabled', 'true');
    const disabled = document.createElement('div');
    disabled.setAttribute('aria-disabled', 'true');
    document.body.append(plain, marked, disabledMarked, disabled);

    expect(getFallbackIndex([plain, marked])).toBe(1);
    expect(getFallbackIndex([disabledMarked, plain])).toBe(1);
    expect(getFallbackIndex([disabled, disabledMarked])).toBe(0);
    expect(getFallbackIndex([plain, marked], [1])).toBe(0);
  });
});

// --- internals/composite/composite.test.ts (CMP-ALG-19, CMP-I18N-06) --------------------------------------------------

describe('Composite', () => {
  describe('scrollIntoViewIfNeeded', () => {
    it('uses the left scroll margin when checking left overflow in RTL', () => {
      const scrollContainer = document.createElement('div');
      const element = document.createElement('div');
      const scrollTo = vi.fn();

      element.style.scrollMarginLeft = '20px';
      scrollContainer.appendChild(element);
      // `getComputedStyle` only resolves styles for elements attached to the document.
      document.body.appendChild(scrollContainer);

      Object.defineProperties(scrollContainer, {
        clientWidth: { value: 100 },
        scrollWidth: { value: 200 },
        scrollLeft: { value: 0 },
        scrollTop: { value: 0 },
        scrollTo: { value: scrollTo },
      });
      Object.defineProperties(element, {
        offsetLeft: { value: 10 },
        offsetWidth: { value: 10 },
        offsetParent: { value: scrollContainer },
        scrollTo: { value: vi.fn() },
      });

      scrollIntoViewIfNeeded(scrollContainer, element, 'rtl', 'horizontal');

      expect(scrollTo).toHaveBeenCalledWith({
        left: -10,
        top: 0,
        behavior: 'auto',
      });

      scrollContainer.remove();
    });
  });
});

// --- CompositeList.test.tsx through the registry (CMP-ALG-17, CMP-STA-01..18, CMP-EDGE-12..14) -----------------------

/** Element ids, in the order of the elements array, for readable failures. */
function testIds(elements: ReadonlyArray<Element | null | undefined>): Array<string | null> {
  return Array.from(elements, (element) => element?.getAttribute('data-testid') ?? null);
}

interface Registry {
  registry: CompositeRegistry;
  published: Array<Map<Element, CompositeMetadata>>;
}

function createRegistry(): Registry {
  const registry = createCompositeRegistry();
  registries.push(registry);
  const published: Array<Map<Element, CompositeMetadata>> = [];
  registry.subscribe((map) => published.push(map));
  return { registry, published };
}

/** A registry item element, as `useCompositeListItem` renders it in Base UI's tests. */
function listItem(label: string): HTMLElement {
  const element = document.createElement('div');
  element.setAttribute('data-testid', label);
  element.textContent = label;
  return element;
}

describe('<CompositeList />', () => {
  describe('prop: elementsRef', () => {
    // Port: the registry's teardown is `dispose`, which empties the elements array in place (CMP-STA-14). Labels are not
    // built in M2 (CMP-STA-06/07, M5), so the labels clause is not asserted.
    it('cleans up refs on unmount', () => {
      const { registry } = createRegistry();
      const elements = ['a', 'b', 'c'].map((label) => document.body.appendChild(listItem(label)));
      for (const element of elements) {
        registry.register(element);
      }
      registry.flush();

      expect(registry.elements).toHaveLength(3);

      registry.dispose();
      expect(registry.elements).toHaveLength(0);
    });

    // Port, 2026-10-22: the guess (CMP-STA-16) is M5 and React's Strict Mode replay has no counterpart. What the title
    // guards remains: a rebuild whose items are already in place keeps the elements array populated, here a rebuild
    // after every item re-registered unchanged, as when C# sends an `update` that resolves the same elements again.
    it('keeps refs populated for items whose guessed index is already correct', () => {
      const { registry, published } = createRegistry();
      const elements = ['a', 'b', 'c'].map((label) => document.body.appendChild(listItem(label)));
      for (const element of elements) {
        registry.register(element);
      }
      registry.flush();
      for (const element of elements) {
        registry.register(element);
      }
      registry.flush();

      expect(testIds(registry.elements)).toEqual(['a', 'b', 'c']);
      expect(published).toHaveLength(1);
    });

    it('only publishes maps that are aligned with the element registry', () => {
      const { registry } = createRegistry();
      const snapshots: Array<{ elements: Array<HTMLElement | null>; mapElements: Element[] }> = [];
      registry.subscribe((map) => {
        snapshots.push({ elements: [...registry.elements], mapElements: Array.from(map.keys()) });
      });
      const list = document.body.appendChild(document.createElement('div'));
      for (const label of ['a', 'b', 'c']) {
        registry.register(list.appendChild(listItem(label)));
      }
      registry.flush();

      expect(snapshots).toHaveLength(1);

      registry.register(list.appendChild(listItem('d')));
      registry.flush();

      expect(snapshots).toHaveLength(2);
      snapshots.forEach((snapshot) => {
        expect(snapshot.elements).toEqual(snapshot.mapElements);
      });
    });

    it('registers explicitly indexed items in their index-addressed slots', () => {
      const { registry, published } = createRegistry();
      const two = document.body.appendChild(listItem('two'));
      const zero = document.body.appendChild(listItem('zero'));
      const one = document.body.appendChild(listItem('one'));
      registry.register(two, { index: 2 });
      registry.register(zero, { index: 0 });
      registry.register(one, { index: 1 });
      registry.flush();

      const map = published.at(-1)!;
      expect(Array.from(map.values(), (metadata) => metadata.index)).toEqual([0, 1, 2]);
      expect(registry.elements).toEqual([zero, one, two]);
    });

    it('reserves explicit slots when assigning automatic indexes', () => {
      const { registry } = createRegistry();
      const automaticOne = document.body.appendChild(listItem('automatic one'));
      const explicitZero = document.body.appendChild(listItem('explicit zero'));
      const automaticTwo = document.body.appendChild(listItem('automatic two'));
      registry.register(automaticOne);
      registry.register(explicitZero, { index: 0 });
      registry.register(automaticTwo);
      registry.flush();

      expect(registry.elements).toEqual([explicitZero, automaticOne, automaticTwo]);
      expect(registry.indexOf(automaticOne)).toBe(1);
      expect(registry.indexOf(automaticTwo)).toBe(2);
    });

    // Port, 2026-10-22: React replaced the `elementsRef` object; the registry owns its one elements array, so the
    // restatement is a rebuild that changes nothing: the array is re-synced in place and nothing is published.
    it('syncs replacement refs without publishing an unchanged map', () => {
      const { registry, published } = createRegistry();
      const element = document.body.appendChild(listItem('item'));
      registry.register(element);
      registry.flush();
      expect(registry.elements).toEqual([element]);
      published.length = 0;

      registry.register(element);
      registry.flush();

      expect(registry.elements).toEqual([element]);
      expect(published).toHaveLength(0);
    });

    // CMP-STA-04: implemented early (ALG-17 as written); first consumer M5.
    it('does not register negative explicit indexes', () => {
      const { registry } = createRegistry();
      registry.register(document.body.appendChild(listItem('item')), { index: -1 });
      registry.flush();

      expect(registry.elements).toHaveLength(0);
      expect(Object.hasOwn(registry.elements, '-1')).toBe(false);
    });

    // Port, 2026-10-22: what the React test watched through ref callbacks is the registration itself. An index shift
    // republishes the tracked item at its new index from its original registration, which is never replaced.
    it('does not detach item refs when an index shifts', () => {
      const { registry, published } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      const tracked = list.appendChild(listItem('tracked'));
      const metadata = { kind: 'tracked' };
      registry.register(tracked, { metadata });
      registry.flush();
      expect(published.at(-1)!.get(tracked)).toEqual({ kind: 'tracked', index: 0 });

      registry.register(list.insertBefore(listItem('before'), tracked));
      registry.flush();

      expect(registry.indexOf(tracked)).toBe(1);
      expect(published.at(-1)!.get(tracked)).toEqual({ kind: 'tracked', index: 1 });
    });

    it('excludes items detached outside React from the registry', () => {
      const { registry, published } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      for (const label of ['a', 'b', 'c']) {
        registry.register(list.appendChild(listItem(label)));
      }
      registry.flush();

      // Detaching a registered item outside React never fires its ref callback, so it
      // stays registered while disconnected. `compareDocumentPosition` is meaningless
      // for it, and leaving it in would scramble the order of every other item.
      const detached = byTestId('b');
      detached.remove();

      registry.register(document.body.appendChild(listItem('d')));
      registry.flush();

      expect(registry.elements).toEqual([byTestId('a'), byTestId('c'), byTestId('d')]);
      const map = published.at(-1)!;
      expect(Array.from(map.keys())).toEqual([byTestId('a'), byTestId('c'), byTestId('d')]);
    });

    it('skips detached items while verifying order after a move', async () => {
      const { registry } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      list.setAttribute('data-testid', 'list');
      for (const label of ['a', 'b', 'c']) {
        registry.register(list.appendChild(listItem(label)));
      }
      registry.flush();

      const a = byTestId('a');
      const c = byTestId('c');

      // One batch that both detaches a registered item and moves another. The move makes the
      // observer verify order, and the verification walks the detached item, which no longer
      // has a meaningful document position.
      byTestId('b').remove();
      list.insertBefore(c, a);
      await nextTask();

      expect(registry.indexOf(byTestId('c'))).toBe(0);
      expect(registry.indexOf(byTestId('a'))).toBe(1);
    });

    it('updates the registry when a mounted item stops rendering an element', () => {
      const { registry } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      const tail = list.appendChild(listItem('tail'));
      registry.register(tail);
      registry.flush();

      expect(registry.elements).toEqual([tail]);

      const vanishing = list.insertBefore(listItem('vanishing'), tail);
      registry.register(vanishing);
      registry.flush();
      expect(registry.elements).toHaveLength(2);

      // The item stays mounted while its element detaches, so the next publication must leave its node out.
      registry.unregister(vanishing);
      vanishing.remove();
      registry.flush();

      expect(registry.elements).toEqual([tail]);
      expect(registry.indexOf(tail)).toBe(0);
    });

    it('updates indexes when a leaf item moves outside React', async () => {
      const { registry } = createRegistry();
      const container = document.body.appendChild(document.createElement('div'));
      for (const label of ['a', 'b', 'c']) {
        registry.register(container.appendChild(listItem(label)));
      }
      registry.flush();

      expect(registry.indexOf(byTestId('a'))).toBe(0);

      container.appendChild(byTestId('a'));
      await nextTask();

      expect(registry.indexOf(byTestId('a'))).toBe(2);
      expect(registry.indexOf(byTestId('b'))).toBe(0);
      expect(registry.indexOf(byTestId('c'))).toBe(1);
      expect(registry.elements).toEqual([byTestId('b'), byTestId('c'), byTestId('a')]);
    });

    it('observes each shared mutation root once', () => {
      const observe = vi.spyOn(MutationObserver.prototype, 'observe');
      const { registry } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      list.setAttribute('data-testid', 'list');
      for (const label of ['a', 'b', 'c']) {
        registry.register(list.appendChild(listItem(label)));
      }
      registry.flush();

      const observedRoots = observe.mock.calls.map(([root]) => root);
      observe.mockRestore();
      expect(observedRoots).toEqual([byTestId('list')]);
    });

    // Blazor reorders keyed elements by moving them, as React does; the move is the DOM mutation the observer sees.
    it('updates indexes when keyed groups reorder', async () => {
      const { registry } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      const sections = ['a', 'b'].map((label) => {
        const section = list.appendChild(document.createElement('section'));
        registry.register(section.appendChild(listItem(label)));
        return section;
      });
      registry.flush();

      expect(registry.indexOf(byTestId('a'))).toBe(0);
      expect(registry.indexOf(byTestId('b'))).toBe(1);

      list.insertBefore(sections[1]!, sections[0]!);
      await nextTask();

      expect(registry.indexOf(byTestId('b'))).toBe(0);
      expect(registry.indexOf(byTestId('a'))).toBe(1);
    });

    it('observes reorders after the list grows from one item', async () => {
      const { registry } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      const a = list.appendChild(listItem('a'));
      registry.register(a);
      registry.flush();
      const b = list.appendChild(listItem('b'));
      registry.register(b);
      registry.flush();

      list.insertBefore(b, a);
      await nextTask();

      expect(registry.indexOf(b)).toBe(0);
      expect(registry.indexOf(a)).toBe(1);
    });

    it('updates indexes when grouped items reorder alongside unrelated mutations', async () => {
      const { registry } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      list.setAttribute('data-testid', 'list');
      for (const label of ['a', 'b']) {
        const group = list.appendChild(document.createElement('section'));
        group.setAttribute('data-testid', `group-${label}`);
        registry.register(group.appendChild(listItem(label)));
      }
      registry.flush();

      expect(registry.indexOf(byTestId('a'))).toBe(0);
      expect(registry.indexOf(byTestId('b'))).toBe(1);

      const groupA = byTestId('group-a');
      const groupB = byTestId('group-b');
      const badge = list.ownerDocument.createElement('span');
      badge.setAttribute('data-testid', 'badge');

      list.insertBefore(groupB, groupA);
      list.appendChild(badge);
      await nextTask();

      expect(registry.indexOf(byTestId('b'))).toBe(0);
      expect(registry.indexOf(byTestId('a'))).toBe(1);
      expect(byTestId('badge').isConnected).toBe(true);
    });

    it('ignores mutations for unrelated leaf nodes', async () => {
      const { registry, published } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      registry.register(list.appendChild(listItem('a')));
      const badge = list.appendChild(document.createElement('span'));
      badge.setAttribute('data-testid', 'badge');
      registry.register(list.appendChild(listItem('b')));
      registry.flush();

      expect(registry.indexOf(byTestId('b'))).toBe(1);
      published.length = 0;

      badge.remove();
      await nextTask();

      expect(document.querySelector('[data-testid="badge"]')).toBe(null);
      expect(published).toHaveLength(0);
    });

    it('registers items that sit across a shadow boundary', () => {
      const host = document.createElement('div');
      document.body.appendChild(host);
      const shadowContainer = document.createElement('div');
      host.attachShadow({ mode: 'open' }).appendChild(shadowContainer);
      const { registry } = createRegistry();

      // No ancestor can `contain` both items, so the pair has no common root to observe.
      // Registration still has to hold.
      const light = document.body.appendChild(listItem('light'));
      const shadowItem = shadowContainer.appendChild(listItem('shadow'));
      registry.register(light);
      registry.register(shadowItem);
      registry.flush();

      expect(registry.elements).toHaveLength(2);
      expect(registry.elements).toContain(light);
      expect(registry.elements).toContain(shadowItem);

      registry.dispose();
      expect(registry.elements).toHaveLength(0);
    });
  });

  describe('prop: onMapChange', () => {
    it('publishes item metadata alongside the index', () => {
      const { registry, published } = createRegistry();
      const first = document.body.appendChild(listItem('first'));
      const second = document.body.appendChild(listItem('second'));
      registry.register(first, { metadata: { kind: 'alpha' } });
      registry.register(second, { metadata: { kind: 'beta' } });
      registry.flush();

      const map = published.at(-1)!;
      expect(map.get(first)).toEqual({ kind: 'alpha', index: 0 });
      expect(map.get(second)).toEqual({ kind: 'beta', index: 1 });
    });
  });

  describe('Suspense integration', () => {
    // Port, 2026-10-22: Blazor has no Suspense; the restatement is a subtree withheld from the document and restored,
    // twice, while its items stay registered. The registry rebuilds only when C# sends an `update` that changes the
    // items or when the observer sees a move, so nothing is published while the items are away, and after each
    // restoration the elements array and the published indexes hold every item in order.
    it('does not publish an empty registry when an outer boundary repeatedly suspends', async () => {
      const { registry, published } = createRegistry();
      const boundary = document.body.appendChild(document.createElement('div'));
      const list = boundary.appendChild(document.createElement('div'));
      for (const label of ['a', 'b', 'c']) {
        registry.register(list.appendChild(listItem(label)));
      }
      registry.flush();
      expect(published).toHaveLength(1);

      for (let attempt = 0; attempt < 2; attempt += 1) {
        boundary.remove();
        await nextTask();
        document.body.appendChild(boundary);
        await nextTask();

        expect(registry.elements).toEqual([byTestId('a'), byTestId('b'), byTestId('c')]);
        expect(['a', 'b', 'c'].map((label) => registry.indexOf(byTestId(label)))).toEqual([0, 1, 2]);
      }

      expect(published.length).toBeGreaterThan(0);
      published.forEach((map) => {
        expect(map.size).toBe(3);
      });
    });
  });

  // CMP-EDGE-13: Test: none in the document; a port title for the row.
  it('still registers and orders items where MutationObserver does not exist (CMP-EDGE-13)', () => {
    vi.stubGlobal('MutationObserver', undefined);
    try {
      const { registry } = createRegistry();
      const list = document.body.appendChild(document.createElement('div'));
      const b = list.appendChild(listItem('b'));
      const a = list.insertBefore(listItem('a'), b);
      registry.register(b);
      registry.register(a);
      registry.flush();

      expect(registry.elements).toEqual([a, b]);
    } finally {
      vi.unstubAllGlobals();
    }
  });
});

// --- CompositeRoot.test.tsx through attach (CMP-ALG-14/15/18, CMP-STA-22..30, CMP-KEY-*, CMP-DOM-01/02) --------------

describe('Composite', () => {
  describe('list', () => {
    // CMP-DOM-03: the C# half is the L2 title of the same name; the module writes nothing on the root either.
    it('does not add aria-orientation when orientation is set', () => {
      const { root } = mountItems(['1', '2'], { orientation: 'horizontal' });

      expect(root.hasAttribute('aria-orientation')).toBe(false);
      expect(root.getAttributeNames().sort()).toEqual(['id']);
    });

    // 'controlled mode' needs a consumer-owned highlight (CMP-STA-22's controlled clause, M3); its focus and tab-stop
    // clauses (CMP-KEY-16, CMP-FOC-02) are re-cited to this title (ADR 13 Amendments 2026-10-22).
    it('uncontrolled mode', async () => {
      mountItems(['1', '2', '3']);

      const item1 = byTestId('1');
      const item2 = byTestId('2');
      const item3 = byTestId('3');

      item1.focus();

      keyDown(item1, 'ArrowDown');
      await flushMicrotasks();

      expect(item2.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item2);

      keyDown(item2, 'ArrowDown');
      await flushMicrotasks();

      expect(item3.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item3);

      keyDown(item3, 'ArrowUp');
      await flushMicrotasks();

      expect(item2.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item2);

      keyDown(item2, 'ArrowUp');
      await flushMicrotasks();

      expect(item1.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item1);
    });

    // Port, 2026-10-22: an explicit index exists on the JavaScript side only (CMP-ALG-17's partial row), so the marked
    // item's registry index is its document position here; the mirror C# reads is the highlight report.
    it('uses an active item explicit index as the initial highlighted index', () => {
      const group = mountGroup([item('zero'), item('one'), item('two', { [ACTIVE_COMPOSITE_ITEM]: '' })], { reportHighlight: true });

      expect(group.fake.reported('OnHighlightChange')).toEqual([{ index: 2 }]);
      expect(byTestId('two').getAttribute('tabindex')).toBe('0');
    });

    // CMP-KEY-04 and CMP-KEY-17 (M3, implemented early) and CMP-EDGE-12 (the composed target).
    it('keeps native input behavior when the native target differs from the synthetic target', async () => {
      mountGroup([item('1'), Object.assign(document.createElement('div'), { id: 'host' }), item('2')], { orientation: 'horizontal' });

      const item1 = byTestId('1');
      const item2 = byTestId('2');
      const host = document.getElementById('host')!;
      const input = document.createElement('input');

      input.type = 'text';
      input.value = 'abcd';
      input.setSelectionRange(2, 2);

      const focusEvent = new FocusEvent('focusin', { bubbles: true });
      Object.defineProperty(focusEvent, 'composedPath', {
        configurable: true,
        value: () => [input, host],
      });

      host.dispatchEvent(focusEvent);

      // Focusing a native input within a composite selects the whole value so
      // the first arrow key returns control to the textbox before moving focus.
      expect(input.selectionStart).toBe(0);
      expect(input.selectionEnd).toBe(4);

      item1.focus();

      input.setSelectionRange(1, 1);

      const keyDownEvent = new KeyboardEvent('keydown', {
        key: 'ArrowRight',
        bubbles: true,
        cancelable: true,
      });
      Object.defineProperty(keyDownEvent, 'composedPath', {
        configurable: true,
        value: () => [input, host],
      });

      host.dispatchEvent(keyDownEvent);
      await flushMicrotasks();

      expect(document.activeElement).toBe(item1);
      expect(document.activeElement).not.toBe(item2);
    });

    it.each([
      { orientation: 'horizontal' as const, key: 'ArrowRight', prevented: true },
      { orientation: 'horizontal' as const, key: 'ArrowDown', prevented: false },
      { orientation: 'vertical' as const, key: 'ArrowDown', prevented: true },
      { orientation: 'vertical' as const, key: 'ArrowRight', prevented: false },
      { orientation: 'both' as const, key: 'ArrowRight', prevented: true },
      { orientation: 'both' as const, key: 'ArrowDown', prevented: true },
    ])('sets default prevention to $prevented for $key with $orientation orientation', async ({ orientation, key, prevented }) => {
      mountItems(['1', '2'], { orientation });

      const item1 = byTestId('1');
      item1.focus();

      const allowed = item1.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
      await flushMicrotasks();

      expect(allowed).toBe(!prevented);
    });

    describe('Home and End keys', () => {
      it('Home key moves focus to the first item', async () => {
        mountItems(['1', '2', '3'], { enableHomeAndEndKeys: true });

        const item1 = byTestId('1');
        const item3 = byTestId('3');

        item3.focus();

        keyDown(item3, 'Home');
        await flushMicrotasks();

        expect(item1.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item1);
      });

      it('End key moves focus to the last item', async () => {
        mountItems(['1', '2', '3'], { enableHomeAndEndKeys: true });

        const item1 = byTestId('1');
        const item3 = byTestId('3');

        item1.focus();

        keyDown(item1, 'End');
        await flushMicrotasks();

        expect(item3.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item3);
      });
    });

    // 'calls onLoop and uses its return value' needs `onLoop` (deferred, CMP §10 Q1); CMP-KEY-06's forward wrap without
    // a loop hook is this port title.
    it('wraps a forward key from the last item to the first when looping (CMP-KEY-06)', async () => {
      mountItems(['1', '2', '3']);

      byTestId('3').focus();
      keyDown(byTestId('3'), 'ArrowDown');
      await flushMicrotasks();

      expect(byTestId('1').getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(byTestId('1'));
    });

    // The `onLoop` clause is vacuous: the hook is deferred (CMP §10 Q1).
    it('does not loop or call onLoop when loopFocus is disabled', async () => {
      mountItems(['1', '2', '3', '4', '5', '6', '7', '8', '9'], { loopFocus: false });

      byTestId('9').focus();

      keyDown(byTestId('9'), 'ArrowDown');
      await flushMicrotasks();

      expect(byTestId('9').getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(byTestId('9'));
    });

    // Browser-only in Base UI (`describe.skipIf(isJSDOM)`); they run at L4 on the `composite-rtl` fixture.
    describe.skipIf(isJSDOM)('rtl', () => {
      function mountRtl(orientation: CompositeOptions['orientation']) {
        const wrapper = document.body.appendChild(document.createElement('div'));
        wrapper.dir = 'rtl';
        mountGroup([item('1'), item('2'), item('3')], { orientation, direction: 'rtl' }, {}, wrapper);
      }

      it('horizontal orientation', async () => {
        mountRtl('horizontal');

        const item1 = byTestId('1');
        const item2 = byTestId('2');
        const item3 = byTestId('3');

        item1.focus();

        keyDown(item1, 'ArrowDown');
        await flushMicrotasks();

        keyDown(item1, 'ArrowLeft');
        await flushMicrotasks();

        expect(item2.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item2);

        keyDown(item2, 'ArrowLeft');
        await flushMicrotasks();

        expect(item3.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item3);

        keyDown(item3, 'ArrowRight');
        await flushMicrotasks();

        expect(item2.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item2);

        keyDown(item2, 'ArrowRight');
        await flushMicrotasks();

        expect(item1.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item1);

        // loop backward
        keyDown(item1, 'ArrowRight');
        await flushMicrotasks();

        expect(item3.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item3);
      });

      it('both horizontal and vertical orientation', async () => {
        mountRtl('both');

        const item1 = byTestId('1');
        const item2 = byTestId('2');
        const item3 = byTestId('3');

        item1.focus();

        keyDown(item1, 'ArrowLeft');
        await flushMicrotasks();

        expect(item2.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item2);

        keyDown(item2, 'ArrowLeft');
        await flushMicrotasks();

        expect(item3.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item3);

        keyDown(item3, 'ArrowRight');
        await flushMicrotasks();

        expect(item2.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item2);

        keyDown(item2, 'ArrowRight');
        await flushMicrotasks();

        expect(item1.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item1);

        keyDown(item1, 'ArrowDown');
        await flushMicrotasks();

        expect(item2.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item2);

        keyDown(item2, 'ArrowDown');
        await flushMicrotasks();

        expect(item3.getAttribute('tabindex')).toBe('0');
        expect(document.activeElement).toBe(item3);
      });
    });

    // CMP-I18N-03: its cited title is grid rtl (no consumer); this port title covers the list clause in jsdom, where
    // the module reads `direction` from its options and never from the DOM (UTL-I18N-03).
    it('leaves ArrowUp, ArrowDown, Home and End unchanged under rtl (CMP-I18N-03)', async () => {
      mountItems(['1', '2', '3'], { direction: 'rtl', enableHomeAndEndKeys: true });

      byTestId('1').focus();
      keyDown(byTestId('1'), 'ArrowDown');
      await flushMicrotasks();
      expect(document.activeElement).toBe(byTestId('2'));

      keyDown(byTestId('2'), 'End');
      await flushMicrotasks();
      expect(document.activeElement).toBe(byTestId('3'));

      keyDown(byTestId('3'), 'ArrowUp');
      await flushMicrotasks();
      expect(document.activeElement).toBe(byTestId('2'));

      keyDown(byTestId('2'), 'Home');
      await flushMicrotasks();
      expect(document.activeElement).toBe(byTestId('1'));

      // The horizontal pair is swapped: ArrowLeft is forward.
      keyDown(byTestId('1'), 'ArrowLeft');
      await flushMicrotasks();
      expect(document.activeElement).toBe(byTestId('2'));
    });
  });

  describe('prop: disabledIndices', () => {
    it('moves the initial tab stop to the first enabled item when the default item is disabled', () => {
      mountItems(['1', '2', '3'], { disabledIndices: [0] });

      expect(byTestId('1').getAttribute('tabindex')).toBe('-1');
      expect(byTestId('2').getAttribute('tabindex')).toBe('0');
      expect(byTestId('3').getAttribute('tabindex')).toBe('-1');
    });

    it('keeps the initial tab stop when all items are disabled', () => {
      mountItems(['1', '2'], { disabledIndices: [0, 1] });

      expect(byTestId('1').getAttribute('tabindex')).toBe('0');
      expect(byTestId('2').getAttribute('tabindex')).toBe('-1');
    });

    it('disables navigating item when their index is included', async () => {
      mountItems(['1', '2', '3'], { disabledIndices: [1] });

      const item1 = byTestId('1');
      const item3 = byTestId('3');

      item1.focus();

      keyDown(item1, 'ArrowDown');
      await flushMicrotasks();

      expect(item3.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item3);

      keyDown(item3, 'ArrowUp');
      await flushMicrotasks();

      expect(item1.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item1);
    });

    it('allows navigating items disabled in the DOM when their index is excluded', async () => {
      const disabled = { 'data-disabled': '', 'aria-disabled': 'true', disabled: '' };
      mountGroup([item('1', disabled, 'span'), item('2', disabled, 'span'), item('3', disabled, 'span')], { disabledIndices: [] });

      const item1 = byTestId('1');
      const item2 = byTestId('2');
      const item3 = byTestId('3');

      item1.focus();

      keyDown(item1, 'ArrowDown');
      await flushMicrotasks();

      expect(item2.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item2);

      keyDown(item2, 'ArrowDown');
      await flushMicrotasks();

      expect(item3.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item3);

      keyDown(item3, 'ArrowDown');
      await flushMicrotasks();

      expect(item1.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowUp');
      await flushMicrotasks();

      expect(item3.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(item3);
    });

    // CMP-STA-29 (M3, implemented early): its cited title needs two registrations on one element (M3).
    it('moves a root-owned highlight off an index that a changed disabledIndices disables (CMP-STA-29)', () => {
      const group = mountItems(['1', '2', '3'], { disabledIndices: [] });
      expect(byTestId('1').getAttribute('tabindex')).toBe('0');

      group.update({ disabledIndices: [0] });

      expect(byTestId('1').getAttribute('tabindex')).toBe('-1');
      expect(byTestId('2').getAttribute('tabindex')).toBe('0');
    });

    // CMP-STA-29: Base UI re-validates whenever the highlighted index changes too, so focus landing on an item that
    // disabledIndices disables leaves the tab stop on the first enabled item.
    it('moves the tab stop off an item that disabledIndices disables when focus lands on it (CMP-STA-29)', () => {
      mountItems(['1', '2', '3'], { disabledIndices: [1] });

      byTestId('2').focus();

      expect(document.activeElement).toBe(byTestId('2'));
      expect(byTestId('1').getAttribute('tabindex')).toBe('0');
      expect(byTestId('2').getAttribute('tabindex')).toBe('-1');
    });
  });

  // Each removal is what C# does: the render batch removes the element, then the root's `update` drops its id.
  describe('item removal', () => {
    function remove(group: Group, testId: string) {
      byTestId(testId).remove();
      group.update();
    }

    it('keeps the tab stop on the highlighted item when an earlier item is removed', async () => {
      const group = mountItems(['1', '2', '3', '4']);

      const item1 = byTestId('1');
      item1.focus();

      keyDown(item1, 'ArrowDown');
      await flushMicrotasks();

      keyDown(byTestId('2'), 'ArrowDown');
      await flushMicrotasks();

      const item3 = byTestId('3');
      expect(item3.getAttribute('tabindex')).toBe('0');

      remove(group, '1');

      expect(item3.getAttribute('tabindex')).toBe('0');
      expect(byTestId('2').getAttribute('tabindex')).toBe('-1');
      expect(byTestId('4').getAttribute('tabindex')).toBe('-1');

      // navigation continues from the item that holds the tab stop
      keyDown(item3, 'ArrowDown');
      await flushMicrotasks();

      expect(document.activeElement).toBe(byTestId('4'));
    });

    it('moves the tab stop back into range when the highlighted item is removed', async () => {
      const group = mountItems(['1', '2', '3']);

      const item1 = byTestId('1');
      item1.focus();

      keyDown(item1, 'ArrowDown');
      await flushMicrotasks();

      keyDown(byTestId('2'), 'ArrowDown');
      await flushMicrotasks();

      expect(byTestId('3').getAttribute('tabindex')).toBe('0');

      remove(group, '3');

      expect(byTestId('1').getAttribute('tabindex')).toBe('0');
      expect(byTestId('2').getAttribute('tabindex')).toBe('-1');
    });

    it('moves the tab stop to the active item when the highlighted item is removed', async () => {
      const group = mountGroup([item('1'), item('2'), item('3', { [ACTIVE_COMPOSITE_ITEM]: '' }), item('4')]);

      const item3 = byTestId('3');
      expect(item3.getAttribute('tabindex')).toBe('0');

      item3.focus();

      keyDown(item3, 'ArrowDown');
      await flushMicrotasks();

      expect(byTestId('4').getAttribute('tabindex')).toBe('0');

      remove(group, '4');

      expect(byTestId('1').getAttribute('tabindex')).toBe('-1');
      expect(item3.getAttribute('tabindex')).toBe('0');
    });

    it('skips items that cannot hold the tab stop', async () => {
      const group = mountGroup([item('1', { style: 'display: none' }), item('2', { 'aria-disabled': 'true' }), item('3'), item('4')]);

      const item3 = byTestId('3');
      expect(item3.getAttribute('tabindex')).toBe('0');

      item3.focus();

      keyDown(item3, 'ArrowDown');
      await flushMicrotasks();

      expect(byTestId('4').getAttribute('tabindex')).toBe('0');

      remove(group, '4');

      expect(byTestId('1').getAttribute('tabindex')).toBe('-1');
      expect(byTestId('2').getAttribute('tabindex')).toBe('-1');
      expect(item3.getAttribute('tabindex')).toBe('0');
    });

    it('keeps the tab stop in range when no item can hold it', () => {
      const group = mountGroup([item('1', { 'aria-disabled': 'true' }), item('2', { 'aria-disabled': 'true' }), item('3')]);

      expect(byTestId('3').getAttribute('tabindex')).toBe('0');

      remove(group, '3');

      expect(byTestId('1').getAttribute('tabindex')).toBe('0');
      expect(byTestId('2').getAttribute('tabindex')).toBe('-1');
    });

    // CMP-STA-28: Test: none in the document; a port title for the row.
    it('ignores a publication of an empty item list (CMP-STA-28)', async () => {
      const group = mountItems(['1', '2', '3'], { reportHighlight: true });
      byTestId('1').focus();
      keyDown(byTestId('1'), 'ArrowDown');
      await flushMicrotasks();
      expect(group.fake.reported('OnHighlightChange')).toEqual([{ index: 1 }]);

      const items = itemsUnder(group.root);
      for (const element of items) {
        element.remove();
      }
      group.update();
      group.root.append(...items);
      group.update();

      // The empty list changed nothing; when the items return, the highlighted element is found again at index 1.
      expect(group.fake.reported('OnHighlightChange')).toEqual([{ index: 1 }]);
      expect(byTestId('2').getAttribute('tabindex')).toBe('0');
    });
  });

  describe('prop: modifierKeys', () => {
    it('prevents arrow key navigation when any modifier key is pressed by default', async () => {
      mountItems(['1', '2']);

      const item1 = byTestId('1');

      item1.focus();

      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowDown', { shiftKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowDown', { ctrlKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowDown', { altKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowDown', { metaKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item1);
    });

    it('specifies allowed modifier keys that do not prevent arrow key navigation when pressed', async () => {
      mountItems(['1', '2', '3'], { modifierKeys: ['Alt', 'Meta'] });

      const item1 = byTestId('1');
      const item2 = byTestId('2');
      const item3 = byTestId('3');

      item1.focus();

      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowDown', { shiftKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowDown', { ctrlKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item1);

      keyDown(item1, 'ArrowDown', { altKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item2);

      keyDown(item2, 'ArrowDown', { metaKey: true });
      await flushMicrotasks();
      expect(document.activeElement).toBe(item3);
    });
  });
});

// --- port titles: the root the module owns from attach ---------------------------------------------------------------

describe('composite root (port)', () => {
  // CMP-DOM-01 under the handover (ADR 04 Amendments 2026-10-22): C# renders "-1" on every item (CMP-STA-15) and the
  // module's first publication writes the roving value.
  it('writes the roving tabindex on every item from attach: "0" on the highlighted item, "-1" elsewhere', () => {
    mountItems(['1', '2', '3']);

    expect(['1', '2', '3'].map((testId) => byTestId(testId).getAttribute('tabindex'))).toEqual(['0', '-1', '-1']);
  });

  // CMP-KEY-10, CMP-FOC-02: the tab stop moves in the dispatch's own task, the focus one microtask later.
  it('writes the tabindex in the same task as the highlight change and moves the focus a microtask later', async () => {
    mountItems(['1', '2']);
    const item1 = byTestId('1');
    const item2 = byTestId('2');
    item1.focus();

    keyDown(item1, 'ArrowDown');

    expect(item2.getAttribute('tabindex')).toBe('0');
    expect(item1.getAttribute('tabindex')).toBe('-1');
    expect(document.activeElement).toBe(item1);

    await flushMicrotasks();
    expect(document.activeElement).toBe(item2);
  });

  // RAD-DOM-16: a consumer tabindex travels in the item's registration and is never written over.
  it('never writes the tabindex of an item whose registration carries a consumer tabindex', async () => {
    const group = mountItems(['1', '2', '3']);
    const item2 = byTestId('2');
    item2.setAttribute('tabindex', '5');
    group.update({ items: registrations(itemsUnder(group.root), { [item2.id]: '5' }) });

    byTestId('1').focus();
    keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();

    expect(item2.getAttribute('tabindex')).toBe('5');
    expect(document.activeElement).toBe(item2);

    keyDown(item2, 'ArrowDown');
    await flushMicrotasks();
    expect(item2.getAttribute('tabindex')).toBe('5');
    expect(byTestId('3').getAttribute('tabindex')).toBe('0');
  });

  // CMP-STA-10 with ids: a rendered element replaced under the same id is found again on the next `update`.
  it('resolves the items again on update, so a replaced element takes over its id', () => {
    const group = mountItems(['1', '2']);
    const first = byTestId('1');
    const replacement = item('1');
    replacement.id = first.id;
    first.replaceWith(replacement);

    group.update();

    expect(replacement.getAttribute('tabindex')).toBe('0');
  });

  // CMP-STA-10 (ADR 06 Amendments 2026-10-18 rule 2): a repeated attach of the same element is a no-op.
  it('returns the live handle for a repeated attach of the same element, and dispose is idempotent', async () => {
    const group = mountItems(['1', '2']);
    const again = attach(group.root, JSON.stringify(group.options), createFakeTwin().twin);
    expect(again).toBe(group.handle);

    group.handle.dispose();
    group.handle.dispose();

    byTestId('1').focus();
    const event = keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();
    expect(event.defaultPrevented).toBe(false);
    expect(document.activeElement).toBe(byTestId('1'));

    // A new attach after dispose starts a new handle.
    const fresh = attach(group.root, JSON.stringify(group.options), createFakeTwin().twin);
    handles.push(fresh);
    expect(fresh).not.toBe(group.handle);
  });

  // CMP-EDGE-08: Test: none in the document.
  it('ignores keys while the root element is not in the document (CMP-EDGE-08)', async () => {
    const group = mountItems(['1', '2']);
    const item1 = byTestId('1');
    group.root.remove();

    const event = keyDown(item1, 'ArrowDown');
    await flushMicrotasks();

    expect(event.defaultPrevented).toBe(false);
    expect(item1.getAttribute('tabindex')).toBe('0');
  });

  // CMP §8: handlers read the options of the latest `update` at event time.
  it('reads the options of the latest update when a key is pressed', async () => {
    const group = mountItems(['1', '2']);
    group.update({ orientation: 'vertical' });

    byTestId('1').focus();
    const event = keyDown(byTestId('1'), 'ArrowRight');
    await flushMicrotasks();

    expect(event.defaultPrevented).toBe(false);
    expect(document.activeElement).toBe(byTestId('1'));
  });

  // CMP-KEY-10/11, CMP-EDGE-18: propagation is stopped only when the index changes, and only with stopEventPropagation.
  it('stops propagation only for a key that moves the highlight, and only with stopEventPropagation', async () => {
    const group = mountItems(['1', '2'], { loopFocus: false });
    const reached: string[] = [];
    document.body.addEventListener('keydown', (event) => reached.push(event.key));

    byTestId('1').focus();
    keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();
    keyDown(byTestId('2'), 'ArrowDown');
    await flushMicrotasks();
    expect(reached).toEqual(['ArrowDown']);

    group.update({ stopEventPropagation: false });
    keyDown(byTestId('2'), 'ArrowUp');
    await flushMicrotasks();
    expect(reached).toEqual(['ArrowDown', 'ArrowUp']);
    expect(document.activeElement).toBe(byTestId('1'));
  });

  // CMP-PTR-02 and CMP-STA-30 (Test: none): focus sets the highlight without scrolling; a keyboard move scrolls.
  it('scrolls the root for a keyboard move but not for a focused item (CMP-STA-30)', async () => {
    const group = mountItems(['1', '2', '3']);
    const scrollTo = vi.fn();
    Object.defineProperties(group.root, {
      clientHeight: { value: 10 },
      scrollHeight: { value: 100 },
      scrollTo: { value: scrollTo },
    });
    for (const element of itemsUnder(group.root)) {
      Object.defineProperty(element, 'scrollTo', { value: vi.fn() });
    }

    byTestId('2').focus();
    expect(byTestId('2').getAttribute('tabindex')).toBe('0');
    expect(scrollTo).not.toHaveBeenCalled();

    keyDown(byTestId('2'), 'ArrowDown');
    await flushMicrotasks();
    expect(scrollTo).toHaveBeenCalledTimes(1);
  });

  // CMP-STA-25: Test: none in the document.
  it('scrolls the marked item into view at the first publication (CMP-STA-25)', () => {
    const scrollTo = vi.fn();
    const marked = item('2', { [ACTIVE_COMPOSITE_ITEM]: '' });
    Object.defineProperty(marked, 'scrollTo', { value: vi.fn() });
    const root = document.createElement('div');
    Object.defineProperties(root, { scrollTo: { value: scrollTo } });
    root.append(item('1'), marked);
    document.body.append(root);

    handles.push(attach(root, JSON.stringify({ ...defaults, items: registrations(itemsUnder(root)) }), createFakeTwin().twin));

    expect(scrollTo).toHaveBeenCalledTimes(1);
  });

  // CMP-PTR-01, CMP-API-03 (M5, implemented early).
  it('focuses a hovered item on mousemove only with highlightItemOnHover', () => {
    const group = mountGroup([item('1'), item('2'), item('3', { 'aria-disabled': 'true' })]);
    const move = (testId: string) => byTestId(testId).dispatchEvent(new MouseEvent('mousemove', { bubbles: true }));

    move('2');
    expect(document.activeElement).not.toBe(byTestId('2'));

    group.update({ highlightItemOnHover: true });
    move('2');
    expect(document.activeElement).toBe(byTestId('2'));
    expect(byTestId('2').getAttribute('tabindex')).toBe('0');

    move('3');
    expect(document.activeElement).toBe(byTestId('2'));
  });

  // CMP-EVT-01 as a report: one message per change, and only when asked (ADR 16 rule 6).
  it('reports the highlighted index once per change, and only with reportHighlight', async () => {
    const quiet = mountItems(['a', 'b']);
    byTestId('b').focus();
    expect(quiet.fake.reported('OnHighlightChange')).toEqual([]);
    document.body.replaceChildren();

    const group = mountItems(['1', '2', '3'], { reportHighlight: true });
    expect(group.fake.reported('OnHighlightChange')).toEqual([]);

    byTestId('2').focus();
    byTestId('1').focus();
    byTestId('1').focus();
    keyDown(byTestId('1'), 'ArrowUp');
    await flushMicrotasks();

    expect(group.fake.reported('OnHighlightChange')).toEqual([{ index: 1 }, { index: 0 }, { index: 2 }]);
  });
});

// --- port titles: the RadioGroup options (RDG-STA-06, RDG-KEY-03, RAD-FOC-05, RDG-FOC-04/05) ---------------------------

describe('composite selection on navigation (port)', () => {
  /** A radio as Radio renders it: the item element, then its hidden input as the next sibling (RAD-STR-01). */
  function radio(testId: string, input: Partial<Pick<HTMLInputElement, 'disabled' | 'readOnly'>> = {}): Node[] {
    const element = item(testId, { role: 'radio' }, 'span');
    const hidden = document.createElement('input');
    hidden.type = 'radio';
    hidden.name = 'group';
    hidden.tabIndex = -1;
    hidden.setAttribute('data-input', testId);
    hidden.disabled = input.disabled ?? false;
    hidden.readOnly = input.readOnly ?? false;
    return [element, hidden];
  }

  function inputOf(testId: string): HTMLInputElement {
    return document.querySelector<HTMLInputElement>(`[data-input="${testId}"]`)!;
  }

  function countClicks(): Record<string, number> {
    const clicks: Record<string, number> = {};
    document.addEventListener('click', (event) => {
      const testId = (event.target as Element).getAttribute('data-input');
      if (testId) {
        clicks[testId] = (clicks[testId] ?? 0) + 1;
      }
    });
    return clicks;
  }

  function mountRadios(inputs: Record<string, Partial<Pick<HTMLInputElement, 'disabled' | 'readOnly'>>> = {}, overrides: Partial<CompositeOptions> = {}, twin: GroupTwin = {}): Group {
    return mountGroup(
      ['a', 'b', 'c'].flatMap((testId) => radio(testId, inputs[testId])),
      { selectOnNavigation: true, modifierKeys: ['Shift'], ...overrides },
      twin,
    );
  }

  it('clicks the hidden input of the item an arrow key focuses, once, with no modifiers', async () => {
    mountRadios();
    const clicks = countClicks();
    const events: MouseEvent[] = [];
    inputOf('b').addEventListener('click', (event) => events.push(event));

    byTestId('a').focus();
    expect(clicks).toEqual({});

    keyDown(byTestId('a'), 'ArrowDown', { shiftKey: true });
    await flushMicrotasks();

    expect(document.activeElement).toBe(byTestId('b'));
    expect(clicks).toEqual({ b: 1 });
    expect(inputOf('b').checked).toBe(true);
    expect(events[0]).toMatchObject({ shiftKey: false, ctrlKey: false, altKey: false, metaKey: false, detail: 0 });
  });

  // RAD-EDGE-09 (design point R9 of M2 tracks 7-8, settled by the orchestrator 2026-10-23): a native radio whose render
  // callback wraps its <button> in a <label>; the hidden input follows the label, not the item.
  it('clicks the hidden input that follows the label a render callback wraps the item in', async () => {
    const wrapped = (testId: string): Node[] => {
      const [element, hidden] = radio(testId);
      const label = document.createElement('label');
      label.append(element!, ` ${testId}`);
      return [label, hidden!];
    };
    mountGroup([...wrapped('a'), ...wrapped('b')], { selectOnNavigation: true, modifierKeys: ['Shift'] });
    const clicks = countClicks();
    byTestId('a').focus();

    keyDown(byTestId('a'), 'ArrowDown');
    await flushMicrotasks();

    expect(document.activeElement).toBe(byTestId('b'));
    expect(clicks).toEqual({ b: 1 });
    expect(inputOf('b').checked).toBe(true);
  });

  it('does not select an item that receives focus without a pending arrow key', () => {
    mountRadios();
    const clicks = countClicks();

    byTestId('a').focus();
    byTestId('b').focus();

    expect(clicks).toEqual({});
  });

  // RDG-STA-06: the flag is set for any key starting with "Arrow", with any modifiers, whether or not the root acts.
  it('sets the pending flag for an arrow key the root ignores, so the next focused item selects', async () => {
    mountRadios();
    const clicks = countClicks();
    byTestId('a').focus();

    keyDown(byTestId('a'), 'ArrowDown', { ctrlKey: true });
    await flushMicrotasks();
    expect(document.activeElement).toBe(byTestId('a'));
    expect(clicks).toEqual({});

    byTestId('c').focus();
    expect(clicks).toEqual({ c: 1 });

    // The flag was consumed.
    byTestId('b').focus();
    expect(clicks).toEqual({ c: 1 });
  });

  it('does not set the pending flag without selectOnNavigation', async () => {
    mountRadios({}, { selectOnNavigation: false });
    const clicks = countClicks();
    byTestId('a').focus();

    keyDown(byTestId('a'), 'ArrowDown');
    await flushMicrotasks();

    expect(document.activeElement).toBe(byTestId('b'));
    expect(clicks).toEqual({});
  });

  // RadioRoot.tsx:156-166 returns before clearing the flag.
  it('leaves the flag set when the focused item input is disabled or read-only', async () => {
    mountRadios({ b: { disabled: true }, c: { readOnly: true } });
    const clicks = countClicks();
    byTestId('a').focus();

    keyDown(byTestId('a'), 'ArrowDown', { ctrlKey: true });
    byTestId('b').focus();
    byTestId('c').focus();
    expect(clicks).toEqual({});

    byTestId('a').focus();
    expect(clicks).toEqual({ a: 1 });
  });

  // RAD §16 Q6: unreachable for a native focus event, which is not cancelable; the check is kept.
  it('does not select on a focus event that was default-prevented', async () => {
    mountRadios();
    const clicks = countClicks();
    byTestId('a').focus();
    keyDown(byTestId('a'), 'ArrowDown', { ctrlKey: true });

    byTestId('b').addEventListener('focusin', (event) => event.preventDefault(), { once: true });
    byTestId('b').dispatchEvent(new FocusEvent('focusin', { bubbles: true, cancelable: true }));
    expect(clicks).toEqual({});

    byTestId('c').focus();
    expect(clicks).toEqual({ c: 1 });
  });

  // RDG-FOC-04/05: the leaving blur clears the flag and reports once; a move between items does neither.
  it('clears the flag and reports once when focus leaves the root, not when it moves between items', async () => {
    const outside = document.body.appendChild(document.createElement('button'));
    const group = mountRadios({}, { reportFocusLeave: true });
    const clicks = countClicks();
    byTestId('a').focus();

    keyDown(byTestId('a'), 'ArrowDown', { metaKey: true });
    // Focus moves within the root through an element that is not an item: nothing leaves.
    group.root.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: byTestId('b') }));
    expect(group.fake.reported('OnFocusLeave')).toEqual([]);

    outside.focus();
    const reports = group.fake.reported<{ event: { type: string; target: { id: string } } }>('OnFocusLeave');
    expect(reports).toHaveLength(1);
    expect(reports[0]!.event.type).toBe('focusout');
    expect(reports[0]!.event.target.id).toBe(byTestId('a').id);

    byTestId('b').focus();
    expect(clicks).toEqual({});
  });

  it('treats a blur with no related target as leaving the root', () => {
    const group = mountRadios({}, { reportFocusLeave: true });
    byTestId('a').focus();

    byTestId('a').blur();

    expect(group.fake.reported('OnFocusLeave')).toHaveLength(1);
  });

  it('reports no leaving blur without reportFocusLeave', () => {
    const group = mountRadios();
    byTestId('a').focus();

    byTestId('a').blur();

    expect(group.fake.reported('OnFocusLeave')).toEqual([]);
  });
});

// --- port titles: the two decision points per render mode (ADR 04 §3; names chosen by the user 2026-10-22) ------------------------

describe('composite decision points (port)', () => {
  it('runs the key handling without asking C# on Server (fallback run)', async () => {
    const group = mountItems(['1', '2']);
    byTestId('1').focus();

    const event = keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();

    expect(group.fake.decisions).toEqual([]);
    expect(event.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(byTestId('2'));
  });

  it('asks navigate with the key event on WebAssembly and does nothing when C# answers stop', async () => {
    const group = mountItems(['1', '2'], {}, { synchronous: true, verdicts: { [NAVIGATE_POINT]: 'stop' } });
    const reached: string[] = [];
    document.body.addEventListener('keydown', (event) => reached.push(event.key));
    byTestId('1').focus();

    const event = keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();

    expect(group.fake.decisions).toHaveLength(1);
    expect(group.fake.decisions[0]).toMatchObject({ point: 'navigate', event: { type: 'keydown', key: 'ArrowDown', target: { id: byTestId('1').id } } });
    expect(event.defaultPrevented).toBe(false);
    expect(reached).toEqual(['ArrowDown']);
    expect(byTestId('1').getAttribute('tabindex')).toBe('0');
    expect(document.activeElement).toBe(byTestId('1'));
  });

  it('handles the key on WebAssembly when C# answers run', async () => {
    const group = mountItems(['1', '2'], {}, { synchronous: true, verdicts: { [NAVIGATE_POINT]: 'run' } });
    byTestId('1').focus();

    const event = keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();

    expect(group.fake.decisions.map((decision) => decision.point)).toEqual(['navigate']);
    expect(event.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(byTestId('2'));
  });

  it('asks navigate only where the module is about to act', async () => {
    const group = mountItems(['1', '2'], { loopFocus: false }, { synchronous: true, verdicts: { [NAVIGATE_POINT]: 'run' } });
    byTestId('1').focus();

    keyDown(byTestId('1'), 'Tab');
    keyDown(byTestId('1'), 'Enter');
    keyDown(byTestId('1'), 'Home');
    keyDown(byTestId('1'), 'ArrowDown', { ctrlKey: true });
    keyDown(byTestId('1'), 'ArrowUp');
    await flushMicrotasks();

    expect(group.fake.decisions).toEqual([]);
  });

  describe.each([false, true])('select (synchronous: %s)', (synchronous) => {
    function mountSelection(verdict: DecisionPolicy) {
      const element = (testId: string) => [item(testId, { role: 'radio' }, 'span'), Object.assign(document.createElement('input'), { type: 'radio', tabIndex: -1 })];
      const group = mountGroup([...element('a'), ...element('b')], { selectOnNavigation: true }, { synchronous, verdicts: { [SELECT_POINT]: verdict } });
      const clicks: string[] = [];
      for (const testId of ['a', 'b']) {
        (byTestId(testId).nextElementSibling as HTMLInputElement).addEventListener('click', () => clicks.push(testId));
      }
      return { group, clicks };
    }

    it(synchronous ? 'asks select with the focus event and skips the click when C# answers stop' : 'clicks without asking C# (fallback run)', async () => {
      const { group, clicks } = mountSelection('stop');
      byTestId('a').focus();

      keyDown(byTestId('a'), 'ArrowDown');
      await flushMicrotasks();

      expect(document.activeElement).toBe(byTestId('b'));
      const selects = group.fake.decisions.filter((decision) => decision.point === 'select');
      if (synchronous) {
        expect(selects).toHaveLength(1);
        expect(selects[0]!.event).toMatchObject({ type: 'focusin', target: { id: byTestId('b').id } });
        expect(clicks).toEqual([]);
      } else {
        expect(selects).toEqual([]);
        expect(clicks).toEqual(['b']);
      }
    });

    it('asks select only where the module is about to click', () => {
      const { group } = mountSelection('run');

      byTestId('a').focus();
      byTestId('b').focus();

      expect(group.fake.decisions).toEqual([]);
    });
  });
});

// --- port titles: the handled-key report (option (i) of ADR 04's note of 2026-10-22, chosen by the user 2026-10-23) ----------------

describe('composite handled-key report (port)', () => {
  it('reports a key it handles and stops, on Server, only when reportHandledKeys is set', async () => {
    const quiet = mountItems(['a', 'b']);
    byTestId('a').focus();
    keyDown(byTestId('a'), 'ArrowDown');
    await flushMicrotasks();
    expect(quiet.fake.reported('OnKeyHandled')).toEqual([]);
    document.body.replaceChildren();

    const group = mountItems(['1', '2'], { reportHandledKeys: true, loopFocus: false });
    byTestId('1').focus();
    keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();
    // Not handled: past the end without looping, and a key outside the composite's.
    keyDown(byTestId('2'), 'ArrowDown');
    keyDown(byTestId('2'), 'Enter');

    const reports = group.fake.reported<{ event: { type: string; key: string; target: { id: string } } }>('OnKeyHandled');
    expect(reports).toHaveLength(1);
    expect(reports[0]!.event).toMatchObject({ type: 'keydown', key: 'ArrowDown', target: { id: byTestId('1').id } });
  });

  it('does not report a key that C# decided inside the dispatch (WebAssembly)', async () => {
    const group = mountItems(['1', '2'], { reportHandledKeys: true }, { synchronous: true, verdicts: { [NAVIGATE_POINT]: 'run' } });
    byTestId('1').focus();

    keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();

    expect(document.activeElement).toBe(byTestId('2'));
    expect(group.fake.reported('OnKeyHandled')).toEqual([]);
  });

  it('does not report a key whose propagation it leaves alone', async () => {
    const group = mountItems(['1', '2'], { reportHandledKeys: true, stopEventPropagation: false });
    byTestId('1').focus();

    keyDown(byTestId('1'), 'ArrowDown');
    await flushMicrotasks();

    expect(document.activeElement).toBe(byTestId('2'));
    expect(group.fake.reported('OnKeyHandled')).toEqual([]);
  });
});

// --- port titles: the items' flags and the derived disabled indices (M3 track 1, E3) --------------------------------
// ADR 04 Amendments 2026-10-26 (chosen by the user): each registration carries the item's `disabled` and
// `focusableWhenDisabled`, and the module derives the indices navigation skips from them, as ToolbarRoot.tsx:32-46 does
// from its item map, in the same pass as each publication and on each `update` that changes a flag (TLB §14).

describe('composite item flags (port)', () => {
  type Flags = Pick<CompositeItemRegistration, 'disabled' | 'focusableWhenDisabled'>;

  /** The registrations C# sends with each item's flags, by test id. */
  function flagged(elements: HTMLElement[], flags: Record<string, Flags> = {}): CompositeOptions['items'] {
    return elements.map((element) => ({ id: element.id, tabIndex: null, ...flags[element.getAttribute('data-testid')!] }));
  }

  // CMP-STA-24 over the derived indices.
  it('moves the initial tab stop off a first item that its flags disable, in the first publication (CMP-STA-24)', () => {
    const elements = [item('1'), item('2'), item('3')];
    mountGroup(elements, { deriveDisabledIndices: true, items: flagged(elements, { '1': { disabled: true } }) });

    expect(byTestId('1').getAttribute('tabindex')).toBe('-1');
    expect(byTestId('2').getAttribute('tabindex')).toBe('0');
    expect(byTestId('3').getAttribute('tabindex')).toBe('-1');
  });

  // CMP-KEY-08 over the derived indices.
  it('skips an item that its flags disable when an arrow key moves the highlight (CMP-KEY-08)', async () => {
    const elements = [item('1'), item('2'), item('3')];
    mountGroup(elements, { deriveDisabledIndices: true, items: flagged(elements, { '2': { disabled: true } }) });
    const item1 = byTestId('1');
    const item3 = byTestId('3');
    item1.focus();

    keyDown(item1, 'ArrowDown');
    await flushMicrotasks();

    expect(document.activeElement).toBe(item3);
    expect(item3.getAttribute('tabindex')).toBe('0');

    keyDown(item3, 'ArrowUp');
    await flushMicrotasks();

    expect(document.activeElement).toBe(item1);
  });

  // CMP-STA-29 over the derived indices: Toolbar's case, a flag that changes after the first publication.
  it('moves the tab stop off the highlighted item when a flag change disables it after the first publication (CMP-STA-29)', () => {
    const elements = [item('1'), item('2'), item('3')];
    const group = mountGroup(elements, { deriveDisabledIndices: true, items: flagged(elements) });
    expect(byTestId('1').getAttribute('tabindex')).toBe('0');

    group.update({ items: flagged(elements, { '1': { disabled: true } }) });

    expect(byTestId('1').getAttribute('tabindex')).toBe('-1');
    expect(byTestId('2').getAttribute('tabindex')).toBe('0');
  });

  // TLB-STA-04's shape: a disabled item that stays focusable is not derived, so its aria-disabled does not skip it.
  it('keeps an aria-disabled item navigable when its flags make it focusable when disabled', async () => {
    const elements = [item('1'), item('2', { 'aria-disabled': 'true' }), item('3')];
    mountGroup(elements, {
      deriveDisabledIndices: true,
      items: flagged(elements, { '2': { disabled: true, focusableWhenDisabled: true } }),
    });
    const item1 = byTestId('1');
    const item2 = byTestId('2');
    item1.focus();

    keyDown(item1, 'ArrowDown');
    await flushMicrotasks();

    expect(document.activeElement).toBe(item2);
    expect(item2.getAttribute('tabindex')).toBe('0');
  });

  // TabsList's case (TAB-KEY-07): its Tabs send `disabled`, but it keeps `disabledIndices={[]}` and derives nothing.
  it('derives the indices only when asked: with deriveDisabledIndices a flagged item is skipped, under an explicit [] it is not', async () => {
    const derived = [item('a1'), item('a2'), item('a3')];
    mountGroup(derived, { deriveDisabledIndices: true, items: flagged(derived, { a2: { disabled: true } }) });
    byTestId('a1').focus();
    keyDown(byTestId('a1'), 'ArrowDown');
    await flushMicrotasks();
    expect(document.activeElement).toBe(byTestId('a3'));

    const explicit = [item('b1'), item('b2', { 'aria-disabled': 'true' }), item('b3')];
    mountGroup(explicit, { disabledIndices: [], items: flagged(explicit, { b2: { disabled: true } }) });
    byTestId('b1').focus();
    keyDown(byTestId('b1'), 'ArrowDown');
    await flushMicrotasks();
    expect(document.activeElement).toBe(byTestId('b2'));
  });
});

// --- port titles: the items report (M3 track 1, E2) --------------------------------------------------------------------
// ADR 04 Amendments 2026-10-26 (chosen by the user): on every publication whose id sequence changed, the module reports the
// items' ids in index order, before the root's own adjustment of the same publication (CMP-EVT-03, `CompositeRoot.tsx:88-91`
// calls `onMapChange` first); C# joins them to its registrations, so no value crosses. One self-contained sequence per
// report (design points 1 and 3, option (a); pending the user).

describe('composite items report (port)', () => {
  type ItemsChange = { ids: string[] };

  it('reports the ids in index order on the first publication, an addition and a removal, only with reportItems', () => {
    const quiet = mountItems(['x', 'y']);
    quiet.update();
    expect(quiet.fake.reported('OnItemsChange')).toEqual([]);
    document.body.replaceChildren();

    const group = mountItems(['a', 'b', 'c'], { reportItems: true });
    const [a, b, c] = ['a', 'b', 'c'].map(byTestId);
    expect(group.fake.reported<ItemsChange>('OnItemsChange')).toEqual([{ ids: [a!.id, b!.id, c!.id] }]);

    const d = item('d');
    group.root.insertBefore(d, b!);
    group.update();
    c!.remove();
    group.update();

    expect(group.fake.reported<ItemsChange>('OnItemsChange')).toEqual([
      { ids: [a!.id, b!.id, c!.id] },
      { ids: [a!.id, d.id, b!.id, c!.id] },
      { ids: [a!.id, d.id, b!.id] },
    ]);
  });

  it('reports an observed move, and nothing for a publication that keeps the id sequence', async () => {
    const sections = ['a', 'b'].map((label) => {
      const section = document.createElement('section');
      section.appendChild(item(label));
      return section;
    });
    const group = mountGroup(sections, { reportItems: true });
    const [a, b] = ['a', 'b'].map(byTestId);

    group.root.insertBefore(sections[1]!, sections[0]!);
    await nextTask();

    // A replaced element under the same id is a publication (CMP-STA-09) whose ids do not change.
    const replacement = item('b');
    replacement.id = b!.id;
    b!.replaceWith(replacement);
    group.update();
    group.update();

    expect(group.fake.reported<ItemsChange>('OnItemsChange')).toEqual([{ ids: [a!.id, b!.id] }, { ids: [b!.id, a!.id] }]);
  });

  it("reports the ids before the root's own adjustment of the same publication", () => {
    const elements = [item('1'), item('2', { [ACTIVE_COMPOSITE_ITEM]: '' }), item('3')];
    const group = mountGroup(elements, { reportItems: true, reportHighlight: true });

    expect(group.fake.reports.map((report) => report.method)).toEqual(['OnItemsChange', 'OnHighlightChange']);
    expect(group.fake.reported('OnHighlightChange')).toEqual([{ index: 1 }]);
  });
});

// --- port titles: the registration-only list (M3 track 1, E1) ----------------------------------------------------------
// ADR 04 Amendments 2026-10-26 (chosen by the user): a root-less `CompositeList` (TabsRoot's panels, AccordionRoot's
// items; CMP-STR-03) attaches in a registration-only mode that installs no listeners, writes no `tabindex`, keeps no
// highlight, reads no marker and asks no decision point, and keeps the registry (move detection, CMP-STA-11/12; shadow-root
// items, CMP-STA-13) with the items report (design points 1 and 2, option (a); pending the user).

describe('composite registration-only list (port)', () => {
  type ItemsChange = { ids: string[] };

  /** An item element as a list item part renders it: an id and no `tabindex`. */
  function listEntry(testId: string): HTMLElement {
    const element = item(testId);
    element.removeAttribute('tabindex');
    return element;
  }

  describe.each([false, true])('synchronous: %s', (synchronous) => {
    it('reports the order and leaves keys, focus and tabindex to the page', async () => {
      const group = mountGroup([listEntry('1'), listEntry('2'), listEntry('3')], { registrationOnly: true, reportHighlight: true }, { synchronous });
      const [one, two, three] = ['1', '2', '3'].map(byTestId);

      expect(group.fake.reported<ItemsChange>('OnItemsChange')).toEqual([{ ids: [one!.id, two!.id, three!.id] }]);

      one!.setAttribute('tabindex', '0');
      one!.focus();
      const event = keyDown(one!, 'ArrowDown');
      await flushMicrotasks();

      expect(event.defaultPrevented).toBe(false);
      expect(document.activeElement).toBe(one);
      expect([two, three].map((element) => element!.hasAttribute('tabindex'))).toEqual([false, false]);
      expect(group.fake.reported('OnHighlightChange')).toEqual([]);
      expect(group.fake.decisions).toEqual([]);
    });
  });

  it('reports a move and an update, and resolves items inside a shadow root', async () => {
    const host = document.body.appendChild(document.createElement('div'));
    const shadow = host.attachShadow({ mode: 'open' });
    const sections = ['a', 'b'].map((label) => {
      const section = document.createElement('section');
      section.appendChild(listEntry(label));
      return section;
    });
    const group = mountGroup(sections, { registrationOnly: true }, {}, shadow);
    const [a, b] = ['a', 'b'].map((label) => shadow.querySelector<HTMLElement>(`[data-testid="${label}"]`)!);

    group.root.insertBefore(sections[1]!, sections[0]!);
    await nextTask();

    const c = listEntry('c');
    group.root.appendChild(c);
    group.handle.update(JSON.stringify({ ...group.options, items: registrations([b!, a!, c]) }));

    expect(group.fake.reported<ItemsChange>('OnItemsChange')).toEqual([
      { ids: [a!.id, b!.id] },
      { ids: [b!.id, a!.id] },
      { ids: [b!.id, a!.id, c.id] },
    ]);
  });
});

// --- port titles: the highlight that follows the active item (M3 track 1, E5) ------------------------------------------
// ADR 04 Amendments 2026-10-26 (chosen by the user): TabsList's consumer-owned highlight (CMP-STA-22, TAB-STA-19) becomes
// an option of the module. Each item is evaluated as `TabsTab.tsx:83-109`'s effect is, when one of its dependencies changes
// (the marker, its index, the highlight, its `disabled` flag): a keydown on the item since its last evaluation only clears
// that mark; otherwise a marked item whose index differs from the highlight takes it, without scrolling, when focus is
// outside the root and its `disabled` flag is off. A marker change is seen by a `MutationObserver` on the root (design
// point 4, option (a)); the `disabled` flag is the registration's (design point 5, option (b)); both pending the user.

describe('composite active-item follow (port)', () => {
  type Flags = Pick<CompositeItemRegistration, 'disabled' | 'focusableWhenDisabled'>;

  function flagged(elements: HTMLElement[], flags: Record<string, Flags> = {}): CompositeOptions['items'] {
    return elements.map((element) => ({ id: element.id, tabIndex: null, ...flags[element.getAttribute('data-testid')!] }));
  }

  function outside(): HTMLElement {
    const button = document.body.appendChild(document.createElement('button'));
    button.setAttribute('data-testid', 'outside');
    return button;
  }

  function mark(testId: string): void {
    document.querySelector(`[${ACTIVE_COMPOSITE_ITEM}]`)?.removeAttribute(ACTIVE_COMPOSITE_ITEM);
    byTestId(testId).setAttribute(ACTIVE_COMPOSITE_ITEM, '');
  }

  // Restated (dated 2026-10-26, M3 track 1): the consumer-owned highlight of Base UI's title is TabsList's, which the port
  // builds as the follow; the keys move the highlight as they do in a root-owned group, and a marker moved while focus is
  // outside the root moves the tab stop.
  it('controlled mode', async () => {
    const button = outside();
    mountItems(['1', '2', '3'], { followActiveItem: true });
    const [item1, item2, item3] = ['1', '2', '3'].map(byTestId);

    item1!.focus();
    expect(item1!.getAttribute('tabindex')).toBe('0');

    keyDown(item1!, 'ArrowDown');
    await flushMicrotasks();
    expect(item2!.getAttribute('tabindex')).toBe('0');
    expect(document.activeElement).toBe(item2);

    keyDown(item2!, 'ArrowDown');
    await flushMicrotasks();
    expect(item3!.getAttribute('tabindex')).toBe('0');
    expect(document.activeElement).toBe(item3);

    keyDown(item3!, 'ArrowUp');
    await flushMicrotasks();
    expect(item2!.getAttribute('tabindex')).toBe('0');
    expect(document.activeElement).toBe(item2);

    button.focus();
    mark('1');
    await flushMicrotasks();
    expect(item1!.getAttribute('tabindex')).toBe('0');
    expect(item2!.getAttribute('tabindex')).toBe('-1');
  });

  it('moves the highlight to the marked item while focus is outside the root, and keeps it while focus is inside', async () => {
    const button = outside();
    const group = mountGroup([item('1', { [ACTIVE_COMPOSITE_ITEM]: '' }), item('2'), item('3')], { followActiveItem: true, reportHighlight: true });
    const [item1, item3] = ['1', '3'].map(byTestId);
    button.focus();

    mark('3');
    await flushMicrotasks();
    expect(item3!.getAttribute('tabindex')).toBe('0');
    expect(group.fake.reported('OnHighlightChange')).toEqual([{ index: 2 }]);

    item3!.focus();
    mark('1');
    await flushMicrotasks();
    expect(item3!.getAttribute('tabindex')).toBe('0');
    expect(item1!.getAttribute('tabindex')).toBe('-1');

    keyDown(item3!, 'ArrowRight');
    await flushMicrotasks();
    expect(document.activeElement).toBe(item1);
  });

  it('does not follow a marked item whose disabled flag is set, and follows it once the flag is cleared', async () => {
    const button = outside();
    const elements = [item('1'), item('2', { 'aria-disabled': 'true' }), item('3')];
    const group = mountGroup(elements, { followActiveItem: true, disabledIndices: [], items: flagged(elements, { '2': { disabled: true } }) });
    button.focus();

    mark('2');
    await flushMicrotasks();
    expect(byTestId('1')!.getAttribute('tabindex')).toBe('0');

    group.update({ items: flagged(elements) });
    expect(byTestId('2')!.getAttribute('tabindex')).toBe('0');
  });

  it('skips the follow once after a keydown on the marked item', async () => {
    const button = outside();
    const group = mountGroup([item('1', { [ACTIVE_COMPOSITE_ITEM]: '' }), item('2'), item('3')], { followActiveItem: true });
    const [item1, item2, item3] = ['1', '2', '3'].map(byTestId);
    item2!.focus();
    button.focus();

    // A keydown that reaches the marked item while focus is elsewhere: the highlight moves from 2 to 3, and the marked
    // item's evaluation that follows only clears its mark.
    keyDown(item1!, 'ArrowRight');
    expect(item3!.getAttribute('tabindex')).toBe('0');
    // The module focuses the item a microtask later; focus leaves the root again.
    await flushMicrotasks();
    button.focus();
    expect(item3!.getAttribute('tabindex')).toBe('0');

    item1!.removeAttribute(ACTIVE_COMPOSITE_ITEM);
    await flushMicrotasks();
    item1!.setAttribute(ACTIVE_COMPOSITE_ITEM, '');
    await flushMicrotasks();
    expect(item1!.getAttribute('tabindex')).toBe('0');
    expect(group.root.contains(document.activeElement)).toBe(false);
  });

  // On Server the batch that adds a marked item is observed before the `update` that registers it arrives.
  it('follows a marked item that registers after the marker is observed', async () => {
    const button = outside();
    const group = mountGroup([item('1', { [ACTIVE_COMPOSITE_ITEM]: '' }), item('2')], { followActiveItem: true });
    button.focus();

    byTestId('1').removeAttribute(ACTIVE_COMPOSITE_ITEM);
    group.root.appendChild(item('3', { [ACTIVE_COMPOSITE_ITEM]: '' }));
    await flushMicrotasks();
    expect(byTestId('1')!.getAttribute('tabindex')).toBe('0');

    group.update();
    expect(byTestId('3')!.getAttribute('tabindex')).toBe('0');
  });

  // CMP-EDGE-09: the consumer-owned highlight is not re-validated against disabledIndices.
  it('leaves the followed highlight on an item that a changed disabledIndices disables (CMP-EDGE-09)', async () => {
    const button = outside();
    const group = mountItems(['1', '2', '3'], { followActiveItem: true, disabledIndices: [] });
    button.focus();

    mark('2');
    await flushMicrotasks();
    expect(byTestId('2')!.getAttribute('tabindex')).toBe('0');

    group.update({ disabledIndices: [1] });
    expect(byTestId('2')!.getAttribute('tabindex')).toBe('0');
  });
});
