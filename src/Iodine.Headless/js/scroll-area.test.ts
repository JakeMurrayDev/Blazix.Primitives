// L3 (ADR 06 §1): the `scroll-area` component module's pure helpers, the transcriptions of scroll-area.md §4.6 and §6.2, on
// mocked metrics (jsdom has no layout; Base UI's jsdom runs mock `scrollHeight`, `clientHeight`, `offsetHeight` and the
// computed style the same way). The titles are Base UI's where the row keeps its arithmetic at L3 (Deliverable 6 of the
// brief: the browser half of each runs at L4 in tests/browser/scroll-area*.spec.ts), and the port's elsewhere. The second
// half attaches the handles against a fake twin: the reports once per change, the scrolling timers, the programmatic
// marker, the bands of the style writes, hover and modality, the scroll-snap bookkeeping, the attach order and the cleanup.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  MIN_THUMB_SIZE,
  PROGRAMMATIC_RESTORE_DELAY,
  SCROLL_TIMEOUT,
  attach,
  computeMeasurement,
  dragScroll,
  getOffset,
  overscrollThumb,
  trackPressScroll,
  wheelScroll,
  type MeasurementInput,
  type OverflowReport,
  type ScrollAreaOptions,
} from './scroll-area';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';


/** A 200 × 200 viewport over 1,000 × 1,000 content at the origin, with no scrollbar, thumb or corner. */
function input(overrides: Partial<MeasurementInput> = {}): MeasurementInput {
  return {
    scrollHeight: 1000,
    scrollWidth: 1000,
    clientHeight: 200,
    clientWidth: 200,
    scrollTop: 0,
    scrollLeft: 0,
    rtl: false,
    thresholds: { xStart: 0, xEnd: 0, yStart: 0, yEnd: 0 },
    scrollbarX: null,
    scrollbarY: null,
    thumbX: null,
    thumbY: null,
    cornerSize: { width: 0, height: 0 },
    ...overrides,
  };
}

function element(style: Partial<CSSStyleDeclaration>): HTMLElement {
  const div = document.createElement('div');
  Object.assign(div.style, style);
  document.body.append(div);
  return div;
}

describe('getOffset', () => {
  it('accounts for scrollbar padding', () => {
    // `paddingInline: 8` → 16 on x; `paddingBlock: 8` → 16 on y (SCR-I18N-05: padding is start + end on both axes).
    const scrollbar = element({ paddingInlineStart: '8px', paddingInlineEnd: '8px', paddingBlockStart: '8px', paddingBlockEnd: '8px' });

    expect(getOffset(scrollbar, 'padding', 'x')).toBe(16);
    expect(getOffset(scrollbar, 'padding', 'y')).toBe(16);
  });

  it('accounts for thumb margin', () => {
    // SCR-I18N-05, §16 Q5: the horizontal margin is twice margin-inline-start; the end is never read.
    const thumb = element({ marginInlineStart: '8px', marginInlineEnd: '30px', marginBlockStart: '3px', marginBlockEnd: '5px' });

    expect(getOffset(thumb, 'margin', 'x')).toBe(16);
    expect(getOffset(thumb, 'margin', 'y')).toBe(8);
  });

  it('port: a missing element has no offset', () => {
    expect(getOffset(null, 'padding', 'x')).toBe(0);
    expect(getOffset(element({ paddingBlockStart: '4px' }), 'padding', 'y')).toBe(4);
  });
});

describe('computeMeasurement', () => {
  it('should correctly set thumb height and width based on scrollable content', () => {
    // (200 / 1000) × 200 = 40.
    const result = computeMeasurement(input());

    expect(result?.thumbSize).toEqual({ width: 40, height: 40 });
  });

  it('accounts for scrollbar padding', () => {
    // (200 − 16) × 0.2 on each axis.
    const result = computeMeasurement(input({
      scrollbarY: { offsetWidth: 10, offsetHeight: 200, padding: 16 },
      scrollbarX: { offsetWidth: 200, offsetHeight: 10, padding: 16 },
    }));

    expect(result?.thumbSize.height).toBeCloseTo((200 - 16) * 0.2);
    expect(result?.thumbSize.width).toBeCloseTo((200 - 16) * 0.2);
  });

  it('accounts for scrollbar margin', () => {
    // A 390 px viewport over 1,000 px of content whose scrollbars have 11 px margins, so their boxes are 368 px:
    // (390 − 22) × 0.39 on each axis.
    const result = computeMeasurement(input({
      clientHeight: 390,
      clientWidth: 390,
      scrollbarY: { offsetWidth: 10, offsetHeight: 368, padding: 0 },
      scrollbarX: { offsetWidth: 368, offsetHeight: 10, padding: 0 },
      cornerSize: { width: 10, height: 10 },
    }));

    expect(result?.thumbSize.height).toBeCloseTo((390 - 22) * 0.39);
    expect(result?.thumbSize.width).toBeCloseTo((390 - 22) * 0.39);
  });

  it('port: a thumb margin shortens the track', () => {
    const result = computeMeasurement(input({
      scrollbarY: { offsetWidth: 10, offsetHeight: 200, padding: 0 },
      thumbY: { margin: 16 },
    }));

    expect(result?.thumbSize.height).toBeCloseTo((200 - 16) * 0.2);
  });

  it('port: the thumb is never shorter than 16px', () => {
    const result = computeMeasurement(input({ scrollHeight: 100_000 }));

    expect(result?.thumbSize.height).toBe(MIN_THUMB_SIZE);
  });

  it('does not add state attributes when content does not overflow', () => {
    const result = computeMeasurement(input({ scrollHeight: 200, scrollWidth: 200 }));

    expect(result?.hidden).toEqual({ x: true, y: true, corner: true });
    expect(result?.edges).toEqual({ xStart: false, xEnd: false, yStart: false, yEnd: false });
    expect(result?.overflowMetrics).toEqual([0, 0, 0, 0]);
  });

  it('applies data attributes on root, viewport and scrollbars based on overflow and edges', () => {
    // At the start only the end edges; in the middle all four; at the end only the start edges.
    expect(computeMeasurement(input())?.edges).toEqual({ xStart: false, xEnd: true, yStart: false, yEnd: true });
    expect(computeMeasurement(input({ scrollTop: 400, scrollLeft: 400 }))?.edges).toEqual({ xStart: true, xEnd: true, yStart: true, yEnd: true });
    expect(computeMeasurement(input({ scrollTop: 800, scrollLeft: 800 }))?.edges).toEqual({ xStart: true, xEnd: false, yStart: true, yEnd: false });
  });

  it('applies numeric overflowEdgeThreshold to every edge', () => {
    const thresholds = { xStart: 20, xEnd: 20, yStart: 20, yEnd: 20 };

    expect(computeMeasurement(input({ thresholds, scrollTop: 15, scrollLeft: 15 }))?.edges).toEqual({ xStart: false, xEnd: true, yStart: false, yEnd: true });
    expect(computeMeasurement(input({ thresholds, scrollTop: 800 - 15, scrollLeft: 800 - 15 }))?.edges).toEqual({ xStart: true, xEnd: false, yStart: true, yEnd: false });
  });

  it('respects overflowEdgeThreshold and exposes scroll metrics', () => {
    const thresholds = { xStart: 20, xEnd: 0, yStart: 5, yEnd: 0 };

    expect(computeMeasurement(input({ thresholds, scrollLeft: 15, scrollTop: 7 }))?.edges).toMatchObject({ xStart: false, yStart: true });
    const later = computeMeasurement(input({ thresholds, scrollLeft: 35, scrollTop: 7 }));
    expect(later?.edges.xStart).toBe(true);
    expect(later?.overflowMetrics[0]).toBe(35);
  });

  it('treats near-edge scroll offsets as fully scrolled', () => {
    const result = computeMeasurement(input({ scrollTop: 800 - 0.5, scrollLeft: 800 - 0.5 }));

    expect(result?.edges).toEqual({ xStart: true, xEnd: false, yStart: true, yEnd: false });
    expect(result?.overflowMetrics).toEqual([800, 0, 800, 0]);
  });

  it('correctly handles RTL', () => {
    // SCR-I18N-03: the inline start distance is −scrollLeft, so 0 shows only x-end and −max only x-start.
    expect(computeMeasurement(input({ rtl: true, scrollLeft: 0 }))?.edges).toMatchObject({ xStart: false, xEnd: true });
    const end = computeMeasurement(input({ rtl: true, scrollLeft: -800 }));
    expect(end?.edges).toMatchObject({ xStart: true, xEnd: false });
    expect(end?.overflowMetrics.slice(0, 2)).toEqual([800, 0]);
  });

  it('recomputes corner size when content starts overflowing', () => {
    // SCR-STA-07, SCR-STA-18: half size, no corner; full size, the vertical scrollbar's width × the horizontal one's height.
    const scrollbars = {
      scrollbarY: { offsetWidth: 11, offsetHeight: 200, padding: 0 },
      scrollbarX: { offsetWidth: 200, offsetHeight: 13, padding: 0 },
    };
    expect(computeMeasurement(input({ ...scrollbars, scrollWidth: 200 }))?.cornerSize).toEqual({ width: 0, height: 0 });
    const both = computeMeasurement(input(scrollbars));

    expect(both?.hidden.corner).toBe(false);
    expect(both?.cornerSize).toEqual({ width: 11, height: 13 });
  });

  it('port: an unsized corner shortens the scrollbars it is about to occupy, and a sized one does not', () => {
    const scrollbars = {
      scrollbarY: { offsetWidth: 10, offsetHeight: 200, padding: 0 },
      scrollbarX: { offsetWidth: 200, offsetHeight: 10, padding: 0 },
    };

    expect(computeMeasurement(input(scrollbars))?.thumbSize).toEqual({ width: (200 - 10) * 0.2, height: (200 - 10) * 0.2 });
    expect(computeMeasurement(input({ ...scrollbars, cornerSize: { width: 10, height: 10 } }))?.thumbSize).toEqual({ width: 40, height: 40 });
  });

  it('clears corner, overflow attributes, and metrics when content stops overflowing', () => {
    const result = computeMeasurement(input({
      scrollHeight: 200,
      scrollWidth: 200,
      scrollbarY: { offsetWidth: 11, offsetHeight: 200, padding: 0 },
      scrollbarX: { offsetWidth: 200, offsetHeight: 13, padding: 0 },
    }));

    expect(result?.cornerSize).toEqual({ width: 0, height: 0 });
    expect(result?.hidden).toEqual({ x: true, y: true, corner: true });
    expect(result?.overflowMetrics).toEqual([0, 0, 0, 0]);
  });

  it('recomputes thumb size when becoming visible without requiring scroll', () => {
    // SCR-ANI-08: a zero-size viewport measures nothing.
    expect(computeMeasurement(input({ scrollHeight: 0, scrollWidth: 0, clientHeight: 0, clientWidth: 0 }))).toBeNull();
    expect(computeMeasurement(input())?.thumbSize).toEqual({ width: 40, height: 40 });
  });

  it('port: the vertical thumb offset is the scroll ratio of the free track', () => {
    // maxThumbOffset = 200 − 40 = 160; scrollTop 400 of 800 → 80.
    const result = computeMeasurement(input({
      scrollTop: 400,
      scrollbarY: { offsetWidth: 10, offsetHeight: 200, padding: 0 },
      thumbY: { margin: 0 },
      cornerSize: { width: 10, height: 10 },
    }));

    expect(result?.thumbY).toEqual({ offset: 80, overscrollSize: null });
  });

  it('port: the horizontal thumb offset is negated in RTL', () => {
    // SCR-I18N-02: −scrollLeft 400 of 800 → 80, applied leftward.
    const result = computeMeasurement(input({
      rtl: true,
      scrollLeft: -400,
      scrollbarX: { offsetWidth: 200, offsetHeight: 10, padding: 0 },
      thumbX: { margin: 0 },
      cornerSize: { width: 10, height: 10 },
    }));

    expect(result?.thumbX).toEqual({ offset: -80, overscrollSize: null });
  });
});

describe('overscrollThumb', () => {
  it('shrinks and pins the thumb to the start edge while overscrolling past the top', () => {
    // 50 px past the top of 1,000 px of content: 40 × 1000 / 1050, at offset 0.
    expect(overscrollThumb(-50, 800, 1000, 40, 160)).toEqual({ offset: 0, overscrollSize: (40 * 1000) / 1050 });
  });

  it('shrinks and pins the thumb to the end edge while overscrolling past the bottom', () => {
    const size = (40 * 1000) / 1050;

    expect(overscrollThumb(850, 800, 1000, 40, 160)).toEqual({ offset: 160 + 40 - size, overscrollSize: size });
  });

  it('restores the resting thumb size once the viewport settles back into range', () => {
    expect(overscrollThumb(400, 800, 1000, 40, 160)).toEqual({ offset: 80, overscrollSize: null });
  });

  it('port: the shrunk thumb is never shorter than 16px', () => {
    expect(overscrollThumb(-100_000, 800, 1000, 40, 160).overscrollSize).toBe(MIN_THUMB_SIZE);
  });

  it('port: no scroll range places the thumb at the start', () => {
    expect(overscrollThumb(0, 0, 200, 200, 0)).toEqual({ offset: 0, overscrollSize: null });
  });
});

describe('dragScroll', () => {
  it('port: maps the pointer displacement onto the scroll range', () => {
    // SCR-PTR-05: 40 px of 160 px of free track → a quarter of the 800 px range.
    expect(dragScroll({ start: 0, delta: 40, trackLength: 200, thumbLength: 40, padding: 0, margin: 0, scrollSize: 1000, clientSize: 200 })).toBe(200);
  });

  it('does not jump the scroll when dragging a thumb that fills the track', () => {
    expect(dragScroll({ start: 120, delta: 30, trackLength: 16, thumbLength: 16, padding: 0, margin: 0, scrollSize: 1000, clientSize: 200 })).toBe(120);
  });

  it('does not jump the scroll when dragging a thumb taller than the track', () => {
    expect(dragScroll({ start: 120, delta: -30, trackLength: 10, thumbLength: 16, padding: 0, margin: 0, scrollSize: 1000, clientSize: 200 })).toBe(120);
  });

  it('uses the negative RTL range and clears scrolling on pointer cancel', () => {
    // SCR-PTR-13: the client-space delta applies directly, so a leftward drag from 0 lands in the negative range.
    expect(dragScroll({ start: 0, delta: -40, trackLength: 200, thumbLength: 40, padding: 0, margin: 0, scrollSize: 1000, clientSize: 200 })).toBe(-200);
  });
});

describe('trackPressScroll', () => {
  it('port: centers the thumb under the pointer on a vertical track', () => {
    // click = 140 − 20 = 120 of 160 → 0.75 of 800.
    expect(trackPressScroll({ pointer: 140, trackStart: 0, thumbLength: 40, padding: 0, margin: 0, trackLength: 200, scrollSize: 1000, clientSize: 200, horizontalRtl: false })).toBe(600);
  });

  it('scrolls into the negative RTL range when clicking a horizontal RTL track', () => {
    // SCR-PTR-18: ratio 0.25 from the left edge → −(1 − 0.25) × 800.
    expect(trackPressScroll({ pointer: 60, trackStart: 0, thumbLength: 40, padding: 0, margin: 0, trackLength: 200, scrollSize: 1000, clientSize: 200, horizontalRtl: true })).toBe(-600);
  });

  it('does not jump the scroll when clicking a track whose thumb fills the track', () => {
    expect(trackPressScroll({ pointer: 8, trackStart: 0, thumbLength: 16, padding: 0, margin: 0, trackLength: 16, scrollSize: 1000, clientSize: 200, horizontalRtl: false })).toBeNull();
  });

  it('does not jump the scroll when clicking a track whose thumb is taller than the track', () => {
    expect(trackPressScroll({ pointer: 4, trackStart: 0, thumbLength: 16, padding: 0, margin: 0, trackLength: 10, scrollSize: 1000, clientSize: 200, horizontalRtl: false })).toBeNull();
  });

  it('port: the ratio is not clamped near an edge', () => {
    // §16 Q8: a press within half a thumb of the start gives a negative offset; the browser clamps the assignment.
    expect(trackPressScroll({ pointer: 4, trackStart: 0, thumbLength: 40, padding: 0, margin: 0, trackLength: 200, scrollSize: 1000, clientSize: 200, horizontalRtl: false })).toBe(-80);
  });
});

describe('wheelScroll', () => {
  it('port: scrolls by the delta and clamps at the end', () => {
    expect(wheelScroll({ current: 790, delta: 50, maxScroll: 800, negativeRange: false })).toBe(800);
    expect(wheelScroll({ current: 100, delta: -30, maxScroll: 800, negativeRange: false })).toBe(70);
  });

  it('port: chains at an edge and ignores a zero delta', () => {
    expect(wheelScroll({ current: 800, delta: 20, maxScroll: 800, negativeRange: false })).toBeNull();
    expect(wheelScroll({ current: 0, delta: -20, maxScroll: 800, negativeRange: false })).toBeNull();
    expect(wheelScroll({ current: 400, delta: 0, maxScroll: 800, negativeRange: false })).toBeNull();
  });

  it('allows horizontal scrolling away from the RTL start edge', () => {
    expect(wheelScroll({ current: 0, delta: -50, maxScroll: 800, negativeRange: true })).toBe(-50);
  });

  it('clamps horizontal RTL wheel scrolling at both edges', () => {
    expect(wheelScroll({ current: -790, delta: -50, maxScroll: 800, negativeRange: true })).toBe(-800);
    expect(wheelScroll({ current: -10, delta: 50, maxScroll: 800, negativeRange: true })).toBe(0);
    expect(wheelScroll({ current: -800, delta: -50, maxScroll: 800, negativeRange: true })).toBeNull();
    expect(wheelScroll({ current: 0, delta: 50, maxScroll: 800, negativeRange: true })).toBeNull();
  });
});

const handles: TwinHandle[] = [];

beforeEach(() => {
  // jsdom implements neither Web Animations nor pointer capture.
  Element.prototype.getAnimations ??= function getAnimations() {
    return [];
  };
  const captured = new WeakMap<Element, Set<number>>();
  Element.prototype.setPointerCapture = function setPointerCapture(id: number) {
    (captured.get(this) ?? captured.set(this, new Set()).get(this)!).add(id);
  };
  Element.prototype.releasePointerCapture = function releasePointerCapture(id: number) {
    captured.get(this)?.delete(id);
  };
  Element.prototype.hasPointerCapture = function hasPointerCapture(id: number) {
    return captured.get(this)?.has(id) ?? false;
  };
});

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

function metrics(element: HTMLElement, values: Record<string, number>): void {
  for (const [name, value] of Object.entries(values)) {
    Object.defineProperty(element, name, { configurable: true, get: () => value });
  }
}

interface Tree {
  root: HTMLElement;
  viewport: HTMLElement;
  content: HTMLElement;
  scrollbar: HTMLElement;
  thumb: HTMLElement;
  fake: FakeTwin;
}

/** A 200 × 200 viewport over 1,000 px of content, with a vertical scrollbar of 200 px and its thumb, all attached. */
function tree({ rootFirst = true }: { rootFirst?: boolean } = {}): Tree {
  const root = document.createElement('div');
  const viewport = document.createElement('div');
  const content = document.createElement('div');
  const scrollbar = document.createElement('div');
  const thumb = document.createElement('div');
  scrollbar.setAttribute('data-orientation', 'vertical');
  thumb.setAttribute('data-orientation', 'vertical');
  viewport.append(content);
  scrollbar.append(thumb);
  root.append(viewport, scrollbar);
  document.body.append(root);
  metrics(viewport, { scrollHeight: 1000, scrollWidth: 200, clientHeight: 200, clientWidth: 200 });
  metrics(scrollbar, { offsetHeight: 200, offsetWidth: 10 });
  metrics(thumb, { offsetHeight: 40, offsetWidth: 10 });
  const fake = createFakeTwin();
  const attachRoot = () => handles.push(attach(root, json({ part: 'root', rtl: false, thresholds: { xStart: 0, xEnd: 0, yStart: 0, yEnd: 0 }, style: 'position: relative' }), fake.twin));
  if (rootFirst) {
    attachRoot();
  }
  handles.push(attach(viewport, json({ part: 'viewport', rootRef: 0, style: 'overflow: scroll' }), createFakeTwin().twin, [root]));
  handles.push(attach(content, json({ part: 'content', rootRef: 0 }), createFakeTwin().twin, [root]));
  handles.push(attach(scrollbar, json({ part: 'scrollbar', rootRef: 0, orientation: 'vertical', style: 'position: absolute' }), createFakeTwin().twin, [root]));
  handles.push(attach(thumb, json({ part: 'thumb', rootRef: 0, orientation: 'vertical', style: 'height: var(--scroll-area-thumb-height)' }), createFakeTwin().twin, [root]));
  if (!rootFirst) {
    attachRoot();
  }
  return { root, viewport, content, scrollbar, thumb, fake };
}

function json(options: ScrollAreaOptions): string {
  return JSON.stringify(options);
}

async function microtasks(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

function scrollTo(viewport: HTMLElement, top: number): void {
  metrics(viewport, { scrollTop: top });
  viewport.dispatchEvent(new Event('scroll'));
}

describe('the measurement reports', () => {
  it('port: the first measurement reports the overflow state with the measured flag, once', async () => {
    const { fake, viewport } = tree();
    await microtasks();

    expect(fake.reported<OverflowReport>('OnOverflow')).toEqual([
      { hasOverflowX: false, hasOverflowY: true, xStart: false, xEnd: false, yStart: false, yEnd: true, measured: true },
    ]);

    // SCR-STA-20: past the start edge one report; scrolls through the middle that cross no edge report nothing.
    scrollTo(viewport, 100);
    scrollTo(viewport, 200);
    scrollTo(viewport, 300);
    expect(fake.reported<OverflowReport>('OnOverflow')).toHaveLength(2);
    expect(fake.reported<OverflowReport>('OnOverflow')[1]).toMatchObject({ yStart: true, yEnd: true });
  });

  it('port: a zero-size viewport is measured, so the flag reports, but nothing else changes', async () => {
    const { fake, viewport } = tree({ rootFirst: true });
    metrics(viewport, { scrollHeight: 0, scrollWidth: 0, clientHeight: 0, clientWidth: 0 });
    await microtasks();

    expect(fake.reported<OverflowReport>('OnOverflow')).toEqual([
      { hasOverflowX: false, hasOverflowY: false, xStart: false, xEnd: false, yStart: false, yEnd: false, measured: true },
    ]);
    expect(viewport.style.getPropertyValue('--scroll-area-overflow-y-end')).toBe('');
  });

  it('port: parts attached before their root are measured once the root attaches', async () => {
    const { fake } = tree({ rootFirst: false });
    await microtasks();

    expect(fake.reported<OverflowReport>('OnOverflow')).toHaveLength(1);
    expect(fake.reported<OverflowReport>('OnOverflow')[0]).toMatchObject({ hasOverflowY: true, measured: true });
  });
});

describe('the writes and their bands', () => {
  it('port: the thumb transform and the viewport variables win over the consumer, the thumb-size variable loses to it', async () => {
    const { root, viewport, scrollbar, thumb } = tree();
    handles.push(attach(thumb, json({ part: 'thumb', rootRef: 0, orientation: 'vertical', style: 'transform: none' }), createFakeTwin().twin, [root]));
    await microtasks();
    // The consumer's strings, sent as `style` on update: a transform on the thumb and a thumb-size variable on the scrollbar.
    handles[3]!.update(json({ part: 'scrollbar', rootRef: 0, orientation: 'vertical', style: '--scroll-area-thumb-height: 77px' }));
    handles[4]!.update(json({ part: 'thumb', rootRef: 0, orientation: 'vertical', style: 'transform: none' }));
    handles[1]!.update(json({ part: 'viewport', rootRef: 0, style: '--scroll-area-overflow-y-end: 1px' }));
    scrollTo(viewport, 400);

    expect(thumb.style.getPropertyValue('transform')).toBe('translate3d(0,80px,0)');
    expect(viewport.style.getPropertyValue('--scroll-area-overflow-y-end')).toBe('400px');
    expect(scrollbar.style.getPropertyValue('--scroll-area-thumb-height')).toBe('77px');
  });

  it('port: the corner variables start at 0px on the root and the thumb-size variable on the scrollbar', async () => {
    const { root, scrollbar } = tree();
    expect(root.style.getPropertyValue('--scroll-area-corner-height')).toBe('0px');
    expect(root.style.getPropertyValue('position')).toBe('relative');
    await microtasks();

    expect(scrollbar.style.getPropertyValue('--scroll-area-thumb-height')).toBe('40px');
  });

  it('port: the content keeps the style C# rendered', () => {
    const { root } = tree();
    const content = document.createElement('div');
    content.setAttribute('style', 'min-width: fit-content');
    root.querySelector('div')!.append(content);
    handles.push(attach(content, json({ part: 'content', rootRef: 0 }), createFakeTwin().twin, [root]));

    expect(content.getAttribute('style')).toBe('min-width: fit-content');
  });
});

describe('the corner', () => {
  it('port: a corner sized before the other scrollbar attaches measures again, so the thumb follows the shortened track', async () => {
    // On Server each part attaches on its own: here the corner and the vertical scrollbar attach, the corner is sized
    // 10 × 0, then the horizontal scrollbar attaches and the corner becomes 10 × 10, which shortens the vertical track
    // to 190 px through --scroll-area-corner-height. The thumb must follow: (190 / 1000) × 200 → 38 px.
    const { root, viewport, scrollbar } = tree();
    metrics(viewport, { scrollHeight: 1000, scrollWidth: 1000, clientHeight: 200, clientWidth: 200 });
    Object.defineProperty(scrollbar, 'offsetHeight', {
      configurable: true,
      get: () => 200 - parseFloat(root.style.getPropertyValue('--scroll-area-corner-height') || '0'),
    });
    const corner = document.createElement('div');
    root.append(corner);
    handles.push(attach(corner, json({ part: 'corner', rootRef: 0, style: 'position: absolute' }), createFakeTwin().twin, [root]));
    await microtasks();
    await microtasks();
    const horizontal = document.createElement('div');
    horizontal.setAttribute('data-orientation', 'horizontal');
    metrics(horizontal, { offsetHeight: 10, offsetWidth: 190 });
    root.append(horizontal);
    handles.push(attach(horizontal, json({ part: 'scrollbar', rootRef: 0, orientation: 'horizontal', style: 'position: absolute' }), createFakeTwin().twin, [root]));
    await microtasks();
    await microtasks();
    await microtasks();

    expect(root.style.getPropertyValue('--scroll-area-corner-height')).toBe('10px');
    expect(scrollbar.style.getPropertyValue('--scroll-area-thumb-height')).toBe('38px');
  });
});

describe('the scrolling flags', () => {
  it('adds [data-scrolling] attribute when viewport is scrolled', async () => {
    vi.useFakeTimers();
    const { fake, root, viewport, content, scrollbar, thumb } = tree();
    await vi.advanceTimersByTimeAsync(0);
    viewport.dispatchEvent(new Event('pointerenter'));
    scrollTo(viewport, 100);

    for (const element of [root, viewport, content, scrollbar, thumb]) {
      expect(element.hasAttribute('data-scrolling')).toBe(true);
    }
    expect(fake.reported('OnScrolling')).toEqual([{ x: false, y: true }]);

    await vi.advanceTimersByTimeAsync(SCROLL_TIMEOUT - 1);
    expect(root.hasAttribute('data-scrolling')).toBe(true);
    await vi.advanceTimersByTimeAsync(1);
    expect(root.hasAttribute('data-scrolling')).toBe(false);
    expect(thumb.hasAttribute('data-scrolling')).toBe(false);
    expect(fake.reported('OnScrolling')).toEqual([{ x: false, y: true }, { x: false, y: false }]);
  });

  it('port: a scroll that no interaction preceded is programmatic and sets no flag', async () => {
    vi.useFakeTimers();
    const { fake, root, viewport } = tree();
    await vi.advanceTimersByTimeAsync(0);

    scrollTo(viewport, 100);

    expect(root.hasAttribute('data-scrolling')).toBe(false);
    expect(fake.reported('OnScrolling')).toEqual([]);
  });

  it('port: 100 ms after the last scroll a scroll is programmatic again; a keydown marks the user', async () => {
    vi.useFakeTimers();
    const { root, viewport } = tree();
    await vi.advanceTimersByTimeAsync(0);
    viewport.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    scrollTo(viewport, 100);
    expect(root.hasAttribute('data-scrolling')).toBe(true);
    await vi.advanceTimersByTimeAsync(SCROLL_TIMEOUT);

    await vi.advanceTimersByTimeAsync(PROGRAMMATIC_RESTORE_DELAY);
    scrollTo(viewport, 200);

    expect(root.hasAttribute('data-scrolling')).toBe(false);
  });

  it('port: in touch modality every scroll is the user\'s', async () => {
    vi.useFakeTimers();
    const { root, viewport } = tree();
    await vi.advanceTimersByTimeAsync(0);
    const down = new Event('pointerdown') as Event & { pointerType: string };
    Object.defineProperty(down, 'pointerType', { value: 'touch' });
    root.dispatchEvent(down);

    scrollTo(viewport, 100);

    expect(root.hasAttribute('data-scrolling')).toBe(true);
  });

  it('port: disposing the root clears its scrolling timers', async () => {
    vi.useFakeTimers();
    const { fake, viewport } = tree();
    await vi.advanceTimersByTimeAsync(0);
    viewport.dispatchEvent(new Event('pointerenter'));
    scrollTo(viewport, 100);

    handles[0]!.dispose();
    await vi.advanceTimersByTimeAsync(SCROLL_TIMEOUT);

    expect(vi.getTimerCount()).toBe(0);
    expect(fake.reported('OnScrolling')).toEqual([{ x: false, y: true }]);
  });
});

describe('hover and modality', () => {
  function pointer(type: string, pointerType: string): Event {
    const event = new Event(type, { bubbles: type !== 'pointerenter' && type !== 'pointerleave' });
    Object.defineProperty(event, 'pointerType', { value: pointerType });
    return event;
  }

  it('port: a mouse over the root hovers the scrollbars, a leave clears them, a touch never hovers', async () => {
    const { fake, root, scrollbar } = tree();
    await microtasks();

    root.dispatchEvent(pointer('pointerenter', 'touch'));
    expect(scrollbar.hasAttribute('data-hovering')).toBe(false);
    root.dispatchEvent(pointer('pointerenter', 'mouse'));
    expect(scrollbar.hasAttribute('data-hovering')).toBe(true);
    root.dispatchEvent(pointer('pointermove', 'mouse'));
    root.dispatchEvent(pointer('pointerleave', 'mouse'));

    expect(scrollbar.hasAttribute('data-hovering')).toBe(false);
    expect(fake.reported('OnHovering')).toEqual([{ hovering: true }, { hovering: false }]);
  });
});

describe('the drag', () => {
  function press(target: HTMLElement, type: string, init: { pointerId?: number; clientY?: number; buttons?: number; button?: number }): PointerEvent {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, clientY: init.clientY ?? 0, button: init.button ?? 0, buttons: init.buttons ?? 1 }) as unknown as PointerEvent;
    Object.defineProperty(event, 'pointerId', { value: init.pointerId ?? 1 });
    target.dispatchEvent(event);
    return event;
  }

  it('disables viewport scroll snap while dragging and restores it on release', async () => {
    const { viewport, thumb } = tree();
    handles[1]!.update(json({ part: 'viewport', rootRef: 0, style: 'overflow: scroll; scroll-snap-type: y mandatory' }));
    await microtasks();
    expect(viewport.style.getPropertyValue('scroll-snap-type')).toBe('y mandatory');

    press(thumb, 'pointerdown', { clientY: 10 });
    expect(viewport.style.getPropertyValue('scroll-snap-type')).toBe('none');
    expect(thumb.hasPointerCapture(1)).toBe(true);

    press(thumb, 'pointerup', { clientY: 10, buttons: 0 });
    expect(viewport.style.getPropertyValue('scroll-snap-type')).toBe('y mandatory');
    expect(thumb.hasPointerCapture(1)).toBe(false);
  });

  it('port: a move maps the pointer onto the scroll range and cancels the event', async () => {
    const { viewport, thumb, root } = tree();
    await microtasks();
    let assigned = 0;
    Object.defineProperty(viewport, 'scrollTop', { configurable: true, get: () => assigned, set: (value: number) => { assigned = value; } });

    press(thumb, 'pointerdown', { clientY: 10 });
    const move = press(thumb, 'pointermove', { clientY: 50 });

    // 40 px of the 160 px free track → a quarter of the 800 px range.
    expect(assigned).toBe(200);
    expect(move.defaultPrevented).toBe(true);
    expect(root.hasAttribute('data-scrolling')).toBe(true);
  });

  it('port: a move without the primary button ends the drag instead of scrolling', async () => {
    const { viewport, thumb, root } = tree();
    await microtasks();
    let assigned = 0;
    Object.defineProperty(viewport, 'scrollTop', { configurable: true, get: () => assigned, set: (value: number) => { assigned = value; } });

    press(thumb, 'pointerdown', { clientY: 10 });
    press(thumb, 'pointermove', { clientY: 50, buttons: 0 });
    press(thumb, 'pointermove', { clientY: 90 });

    expect(assigned).toBe(0);
    expect(root.hasAttribute('data-scrolling')).toBe(false);
  });
});

describe('cleanup', () => {
  it('ignores an animation finishing after its viewport unmounts', async () => {
    vi.useFakeTimers();
    let finish!: () => void;
    const finished = new Promise<void>((resolve) => {
      finish = resolve;
    });
    const animation = { finished, effect: { getTiming: () => ({ duration: 100, iterations: 1 }) } } as unknown as Animation;
    vi.spyOn(Element.prototype, 'getAnimations').mockReturnValue([animation]);
    const { fake, viewport } = tree();
    await vi.advanceTimersByTimeAsync(0);
    const before = fake.reported('OnOverflow').length;

    handles[1]!.dispose();
    metrics(viewport, { scrollHeight: 200 });
    finish();
    await vi.advanceTimersByTimeAsync(0);

    expect(fake.reported('OnOverflow')).toHaveLength(before);
  });

  it('port: disposing a handle removes its listeners and its registration', async () => {
    const { fake, viewport, root } = tree();
    await microtasks();
    handles[1]!.dispose();
    viewport.dispatchEvent(new Event('pointerenter'));

    scrollTo(viewport, 400);

    expect(root.hasAttribute('data-scrolling')).toBe(false);
    expect(fake.reported('OnOverflow')).toHaveLength(1);
  });
});
