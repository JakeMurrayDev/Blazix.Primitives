// L3 for the `select` component module (M5 track 9, issue 173; ADR 06 §1, test-support/fake-twin.ts). The scroll-arrow
// titles are select/scroll-arrow/SelectScrollArrow.test.tsx's, verbatim (SEL-STA-29, SEL-PTR-15..17); the module's
// handles are attached to a DOM built as the parts render it (a positioner around a popup, a list and items with
// `role="option"`, the arrows), with the geometry stubbed as Base UI's test stubs it.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { styleWriter } from './dom';
import { attach, getArrowVisibility, getTargetScrollTop, POSITIONER_PART, SCROLL_ARROW_PART } from './select';
import type { TwinHandle } from './twin';
import { createFakeTwin } from './test-support/fake-twin';

// The `positioning` handle of the positioner is not attached here: `setAligned` answers as a live handle would, running
// the aligned pass at once.
vi.mock('./positioning', async (importOriginal) => ({
  ...(await importOriginal<typeof import('./positioning')>()),
  setAligned: vi.fn((_element: Element, aligned: boolean, onPositioned?: () => void) => {
    if (aligned) {
      onPositioned?.();
    }
    return true;
  }),
}));

const DEFAULT_ITEM_OFFSETS = [0, 40, 80, 120, 160, 200, 240, 280, 320, 360];

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  vi.useRealTimers();
});

function stubScroller(
  node: HTMLElement,
  geometry: { scrollHeight: number; clientHeight: number },
  readScrollTop: () => number,
  onScrollTopChange: (value: number) => void,
): void {
  Object.defineProperty(node, 'scrollTop', { configurable: true, get: readScrollTop, set: onScrollTopChange });
  Object.defineProperty(node, 'scrollHeight', { value: geometry.scrollHeight, configurable: true });
  Object.defineProperty(node, 'clientHeight', { value: geometry.clientHeight, configurable: true });
}

function stubItem(node: HTMLElement, offsetTop: number, offsetHeight: number): void {
  Object.defineProperty(node, 'offsetTop', { value: offsetTop, configurable: true });
  Object.defineProperty(node, 'offsetHeight', { value: offsetHeight, configurable: true });
}

function hoverArrow(arrow: HTMLElement, direction: 'up' | 'down'): void {
  arrow.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, movementX: 0, movementY: direction === 'down' ? 1 : -1 } as MouseEventInit));
}

function mouseMove(arrow: HTMLElement, movementX: number, movementY: number): void {
  arrow.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, movementX, movementY } as MouseEventInit));
}

function advanceTimers(ms: number): void {
  vi.advanceTimersByTime(ms);
}

interface Rendered {
  upArrow: HTMLElement;
  downArrow: HTMLElement;
  getScrollTop(): number;
  getScrollWrites(): number;
}

// The parts' DOM: an open, kept-mounted select placed against its trigger (`alignItemWithTrigger={false}`), the
// positioner's handle first, then each arrow's handle naming the positioner (the order the parts attach in).
function renderSelect(options: {
  withList: boolean;
  withItems?: boolean;
  initialScrollTop: number;
  scrollHeight?: number;
  clientHeight?: number;
  itemOffsets?: number[];
  itemHeight?: number;
}): Rendered {
  const { withList, withItems = true, initialScrollTop, scrollHeight = 400, clientHeight = 200, itemOffsets = DEFAULT_ITEM_OFFSETS, itemHeight = 40 } = options;
  let scrollTop = initialScrollTop;
  let scrollWrites = 0;

  const trigger = document.createElement('button');
  const positioner = document.createElement('div');
  const popup = document.createElement('div');
  const upArrow = document.createElement('div');
  const downArrow = document.createElement('div');
  const list = withList ? document.createElement('div') : null;
  const scroller = list ?? popup;
  stubScroller(scroller, { scrollHeight, clientHeight }, () => scrollTop, (value) => {
    scrollWrites += 1;
    scrollTop = value;
  });
  popup.append(upArrow);
  if (list) {
    popup.append(list);
  }
  if (withItems) {
    itemOffsets.forEach((offsetTop, index) => {
      const item = document.createElement('div');
      item.setAttribute('role', 'option');
      item.id = `item-${index}`;
      item.textContent = `Item ${index}`;
      stubItem(item, offsetTop, itemHeight);
      scroller.append(item);
    });
  }
  popup.append(downArrow);
  positioner.append(popup);
  document.body.append(trigger, positioner);
  // collapseArrows: the arrows' own height does not shift the visible edge.
  Object.defineProperty(downArrow, 'offsetHeight', { value: 0, configurable: true });
  Object.defineProperty(upArrow, 'offsetHeight', { value: 0, configurable: true });

  const references = list ? [trigger, popup, list] : [trigger, popup];
  handles.push(
    attach(
      positioner,
      JSON.stringify({ part: POSITIONER_PART, open: true, mounted: true, trigger: 0, popup: 1, list: list ? 2 : null }),
      createFakeTwin().twin,
      references,
    ),
  );
  handles.push(attach(upArrow, JSON.stringify({ part: SCROLL_ARROW_PART, direction: 'up', positioner: 0 }), createFakeTwin().twin, [positioner]));
  handles.push(attach(downArrow, JSON.stringify({ part: SCROLL_ARROW_PART, direction: 'down', positioner: 0 }), createFakeTwin().twin, [positioner]));
  return { upArrow, downArrow, getScrollTop: () => scrollTop, getScrollWrites: () => scrollWrites };
}

describe('<Select.ScrollArrow />', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  it.each([
    {
      name: 'down arrow snaps hover scrolling to the true bottom when the remaining space is fractional',
      direction: 'down' as const,
      initialScrollTop: 19.5,
      scrollHeight: 100.5,
      clientHeight: 60,
      itemOffsets: [0, 40, 80],
      itemHeight: 20,
      expected: 40.5,
    },
    {
      name: 'down arrow keeps advancing when the next item bottom is fractionally within the visible bottom',
      direction: 'down' as const,
      initialScrollTop: 71.81818389892578,
      scrollHeight: 598,
      clientHeight: 440,
      itemOffsets: [32, 64, 96, 128, 160, 192, 224, 256, 336, 368, 400, 432, 448, 480, 512, 544],
      itemHeight: 32,
      expected: 104,
    },
    {
      name: 'down arrow scrolls to the bottom when trailing content extends past the last item',
      direction: 'down' as const,
      initialScrollTop: 390,
      scrollHeight: 600,
      clientHeight: 200,
      itemOffsets: DEFAULT_ITEM_OFFSETS,
      itemHeight: 40,
      expected: 400,
    },
    {
      name: 'up arrow keeps advancing when the previous item top is fractionally within the visible top',
      direction: 'up' as const,
      initialScrollTop: 72.18181610107422,
      scrollHeight: 598,
      clientHeight: 440,
      itemOffsets: [32, 71.5, 110, 142],
      itemHeight: 32,
      expected: 32,
    },
    {
      name: 'up arrow scrolls to the very top when no earlier item remains to land on',
      direction: 'up' as const,
      initialScrollTop: 100,
      scrollHeight: 600,
      clientHeight: 200,
      itemOffsets: [300, 340, 380],
      itemHeight: 40,
      expected: 0,
    },
  ])('$name', ({ direction, expected, ...geometry }) => {
    const { downArrow, upArrow, getScrollTop } = renderSelect({ withList: true, ...geometry });

    hoverArrow(direction === 'down' ? downArrow : upArrow, direction);
    advanceTimers(40);

    expect(getScrollTop()).toBe(expected);
  });

  it('does not start auto-scrolling for a mouse move that did not move the pointer', () => {
    const { downArrow, getScrollTop } = renderSelect({ withList: true, initialScrollTop: 0 });

    mouseMove(downArrow, 0, 0);
    advanceTimers(400);

    expect(getScrollTop()).toBe(0);
  });

  it('does not let continuous pointer movement postpone the scheduled scroll', () => {
    const { downArrow, getScrollTop } = renderSelect({ withList: true, initialScrollTop: 0 });

    mouseMove(downArrow, 0, 1);
    advanceTimers(30);
    mouseMove(downArrow, 1, 1);
    advanceTimers(15);

    expect(getScrollTop()).toBe(40);
  });

  it('stops auto-scrolling once the pointer leaves the arrow', () => {
    const { downArrow, getScrollTop } = renderSelect({ withList: true, initialScrollTop: 0 });

    hoverArrow(downArrow, 'down');
    advanceTimers(40);
    expect(getScrollTop()).toBe(40);

    downArrow.dispatchEvent(new MouseEvent('mouseleave'));
    advanceTimers(400);
    expect(getScrollTop()).toBe(40);
  });

  it('snaps a sub-pixel offset to the exact top edge and then stops scrolling', () => {
    const { upArrow, getScrollTop, getScrollWrites } = renderSelect({ withList: true, initialScrollTop: 0.4 });

    hoverArrow(upArrow, 'up');
    advanceTimers(40);
    expect(getScrollTop()).toBe(0);

    const writesAtEdge = getScrollWrites();
    advanceTimers(400);
    expect(getScrollTop()).toBe(0);
    expect(getScrollWrites()).toBe(writesAtEdge);
  });

  it('scrolls the popup itself when no Select.List is rendered', () => {
    const { downArrow, getScrollTop } = renderSelect({ withList: false, initialScrollTop: 0 });

    hoverArrow(downArrow, 'down');
    advanceTimers(40);

    expect(getScrollTop()).toBe(40);
  });

  it.each(['up' as const, 'down' as const])('reflects scrollability on the %s arrow when the popup has no registered items', (direction) => {
    const { downArrow, upArrow } = renderSelect({ withList: false, withItems: false, initialScrollTop: 100 });
    const arrow = direction === 'down' ? downArrow : upArrow;

    hoverArrow(arrow, direction);
    advanceTimers(40);

    expect(arrow.hasAttribute('data-visible')).toBe(true);
  });

  it('hides the arrow when an item-less popup is already scrolled to its edge', () => {
    const { downArrow } = renderSelect({ withList: false, withItems: false, initialScrollTop: 200 });

    hoverArrow(downArrow, 'down');
    advanceTimers(40);

    expect(downArrow.hasAttribute('data-visible')).toBe(false);
  });

  it('ignores pointer interaction when the arrow has no scrollable popup', () => {
    const positioner = document.createElement('div');
    const downArrow = document.createElement('div');
    positioner.append(downArrow);
    document.body.append(positioner);
    handles.push(attach(positioner, JSON.stringify({ part: POSITIONER_PART, open: true, mounted: true }), createFakeTwin().twin));
    handles.push(attach(downArrow, JSON.stringify({ part: SCROLL_ARROW_PART, direction: 'down', positioner: 0 }), createFakeTwin().twin, [positioner]));

    hoverArrow(downArrow, 'down');

    expect(() => advanceTimers(400)).not.toThrow();
  });
});

describe('select module: the scroll arrows geometry (port titles)', () => {
  it('reports both arrows visible in the middle of a scroller and snaps within 1px of an edge (SEL-STA-29)', () => {
    const scroller = document.createElement('div');
    let scrollTop = 100;
    stubScroller(scroller, { scrollHeight: 400, clientHeight: 200 }, () => scrollTop, (value) => {
      scrollTop = value;
    });
    expect(getArrowVisibility(scroller)).toEqual({ up: true, down: true });
    scrollTop = 199.5;
    expect(getArrowVisibility(scroller)).toEqual({ up: true, down: false });
    scrollTop = 0.5;
    expect(getArrowVisibility(scroller)).toEqual({ up: false, down: true });
  });

  it('steps one item down past the last visible one, and to the maximum when none follows (SEL-PTR-17)', () => {
    const items = DEFAULT_ITEM_OFFSETS.map((offsetTop) => {
      const item = document.createElement('div');
      stubItem(item, offsetTop, 40);
      return item;
    });
    expect(getTargetScrollTop(items, false, 0, 200, 0, 200)).toBe(40);
    expect(getTargetScrollTop(items, true, 80, 200, 0, 200)).toBe(40);
    expect(getTargetScrollTop(items.slice(0, 1), false, 0, 200, 0, 200)).toBe(200);
  });
});

describe('select module: aligned placement (port titles)', () => {
  // DP1 (a) of M5 track 9 (settled by the orchestrator 2026-11-04): the `positioning` layer stands down to
  // `position: fixed` (positioning.test.ts); the aligned band holds only the placement the steps compute.
  it('writes only the placement in the aligned band, over the anchored layer, and clears it after (SEL-POS-07)', () => {
    Object.defineProperty(document.documentElement, 'clientHeight', { value: 800, configurable: true });
    Object.defineProperty(document.documentElement, 'clientWidth', { value: 1000, configurable: true });
    const trigger = document.createElement('button');
    const positioner = document.createElement('div');
    const popup = document.createElement('div');
    popup.style.border = '0px solid';
    positioner.append(popup);
    document.body.append(trigger, positioner);
    trigger.getBoundingClientRect = () => new DOMRect(100, 200, 160, 40);
    positioner.getBoundingClientRect = () => new DOMRect(110, 240, 180, 120);
    // The `positioning` layer as it stands while told aligned.
    const anchored = styleWriter(positioner).layer('positioning');
    anchored.set('position', 'fixed');

    const options = { part: POSITIONER_PART, open: true, mounted: true, alignItemWithTrigger: true, trigger: 0, popup: 1 };
    const handle = attach(positioner, JSON.stringify(options), createFakeTwin().twin, [trigger, popup]);
    handles.push(handle);

    // A list that does not scroll is top-positioned: the remaining viewport below the trigger's top sets its top.
    expect(positioner.style.position).toBe('fixed');
    expect(positioner.style.transform).toBe('');
    expect(positioner.style.left).toBe('100px');
    expect(positioner.style.top).toBe('190px');
    expect(positioner.style.bottom).toBe('auto');

    handle.update(JSON.stringify({ ...options, open: false, mounted: false }), [trigger, popup]);
    expect(positioner.style.position).toBe('fixed');
    expect(positioner.style.left).toBe('');
    expect(positioner.style.top).toBe('');
    expect(positioner.style.bottom).toBe('');
  });
});
