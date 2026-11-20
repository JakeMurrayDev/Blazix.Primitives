// L3 (ADR 06 §1): the `drawer` module's snap points, M6 track 8b slice 3 (issue 279). The resolution rows DRW-STA-05/06/07
// moved from C# to the module (the module measures the viewport, the popup and the root font size), so Base UI's
// DrawerSnapPoints.test.tsx titles run here against the module's resolvers; the release (DRW-PTR-11, ALG-01), the resting
// progress (DRW-STA-17, ALG-05), the offset variable (DRW-DOM-11) and the `snap-point` decision point run against a handle.
import { afterEach, describe, expect, it } from 'vitest';
import { attach, resolveActiveSnapPointOffset, resolveSnapPoints, type DrawerOptions } from './drawer';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];
const elements: HTMLElement[] = [];
let pointTarget: Element | null = null;

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  for (const element of elements.splice(0)) {
    element.remove();
  }
  pointTarget = null;
});

// jsdom has no hit testing: the element under the point is the target the test names.
Object.defineProperty(document, 'elementFromPoint', { value: () => pointTarget, configurable: true });

const defaults: DrawerOptions = {
  role: 'viewport',
  open: true,
  mounted: true,
  nested: false,
  nestedOpenDrawerCount: 0,
  swipeDirection: 'down',
  answeredDismiss: 0,
  popup: 0,
  backdrop: 1,
};

interface Mounted {
  viewport: HTMLElement;
  popup: HTMLElement;
  backdrop: HTMLElement;
  content: HTMLElement;
  fake: FakeTwin;
  update(options: Partial<DrawerOptions>): void;
}

/** A drawer whose viewport is 400 px tall and whose popup is 300 px tall, as in Base UI's snap point cases. */
function mount(options: Partial<DrawerOptions> = {}, synchronous = false, verdict: 'run' | 'stop' = 'run'): Mounted {
  const viewport = document.createElement('div');
  const backdrop = document.createElement('div');
  const popup = document.createElement('div');
  const content = document.createElement('div');
  content.setAttribute('data-drawer-content', '');
  popup.append(content);
  viewport.append(popup);
  document.body.append(backdrop, viewport);
  elements.push(backdrop, viewport);
  Object.defineProperty(viewport, 'offsetHeight', { value: 400, configurable: true });
  Object.defineProperty(popup, 'offsetHeight', { value: 300, configurable: true });
  Object.defineProperty(popup, 'offsetWidth', { value: 300, configurable: true });

  const fake = createFakeTwin({ synchronous, verdict });
  const handle = attach(viewport, JSON.stringify({ ...defaults, ...options }), fake.twin, [popup, backdrop]);
  handles.push(handle);
  pointTarget = popup;
  return {
    viewport,
    popup,
    backdrop,
    content,
    fake,
    update: (next) => handle.update(JSON.stringify({ ...defaults, ...options, ...next }), [popup, backdrop]),
  };
}

let stamp = 1000;

function pointer(type: 'pointerdown' | 'pointermove' | 'pointerup', target: Element, clientY: number, gap = 100): void {
  const event = new PointerEvent(type, {
    bubbles: true,
    cancelable: true,
    button: 0,
    buttons: type === 'pointerup' ? 0 : 1,
    pointerId: 1,
    pointerType: 'mouse',
    clientX: 10,
    clientY,
  });
  stamp += gap;
  Object.defineProperty(event, 'timeStamp', { value: stamp });
  target.dispatchEvent(event);
}

/** A mouse drag along y from 10 by `toY`, in `steps` moves `gap` ms apart: slow at 20 px per 100 ms, fast in one 16 ms move. */
function drag(target: Element, toY: number, steps: number, gap: number): void {
  pointer('pointerdown', target, 10);
  pointer('pointermove', target, 10, gap);
  for (let step = 1; step <= steps; step++) {
    pointer('pointermove', target, 10 + (toY * step) / steps, gap);
  }
  pointer('pointerup', target, 10 + toY, gap);
}
const slowDrag = (target: Element, toY: number) => drag(target, toY, Math.ceil(Math.abs(toY) / 20), 100);
const flick = (target: Element, toY: number) => drag(target, toY, 1, 16);

const snapOffset = (popup: HTMLElement) => popup.style.getPropertyValue('--drawer-snap-point-offset');

describe('drawer module: snap point resolution (DrawerSnapPoints.test.tsx)', () => {
  // DRW-STA-05, STA-06, STA-07, DOM-11 (DrawerSnapPoints.test.tsx:56-95): ph 300, vh 400, a 20 px root font size.
  it('resolves supported units, filters invalid values, clamps heights, and keeps the last duplicate', () => {
    const values = [Number.NaN, 'invalid', 'invalidpx', 'invalidrem', -1, '2rem', '100px', 200, 200.5, 800];
    const resolved = resolveSnapPoints(values, 400, 300, 20);

    expect(resolved).toEqual([
      { value: -1, height: 0, offset: 300 },
      { value: '2rem', height: 40, offset: 260 },
      { value: '100px', height: 100, offset: 200 },
      { value: 200.5, height: 200.5, offset: 99.5 },
      { value: 800, height: 300, offset: 0 },
    ]);
    expect(resolveActiveSnapPointOffset(200, resolved, 400, 300, 20)).toBe(99.5);

    const previousFontSize = document.documentElement.style.fontSize;
    document.documentElement.style.fontSize = '20px';
    try {
      const { popup } = mount({ snapPoints: values.slice(1), activeSnapPoint: 200 });
      expect(snapOffset(popup)).toBe('99.5px');
    } finally {
      document.documentElement.style.fontSize = previousFontSize;
    }
  });

  // DRW-STA-05, STA-07, DOM-11 (DrawerSnapPoints.test.tsx:97-114).
  it('uses fractional and pixel snap points and resolves null or invalid active values as closed', () => {
    const resolved = resolveSnapPoints([0.5, 200, '2rem'], 400, 300, 16);
    expect(resolveActiveSnapPointOffset(200, resolved, 400, 300, 16)).toBe(100);
    expect(resolveActiveSnapPointOffset(null, resolved, 400, 300, 16)).toBeNull();
    expect(resolveActiveSnapPointOffset('invalid', resolved, 400, 300, 16)).toBeNull();

    const { popup, update } = mount({ snapPoints: [0.5, 200, '2rem'], activeSnapPoint: 200 });
    expect(snapOffset(popup)).toBe('100px');
    update({ snapPoints: [0.5, 200, '2rem'], activeSnapPoint: null });
    expect(snapOffset(popup)).toBe('0px');
  });

  // DRW-STA-06 (DrawerSnapPoints.test.tsx:116-121).
  it('produces no resolved snap point when the list is empty', () => {
    const resolved = resolveSnapPoints([], 400, 300, 16);
    expect(resolved).toEqual([]);
    expect(resolveActiveSnapPointOffset(null, resolved, 400, 300, 16)).toBeNull();
  });

  // DRW-STA-06, STA-07 (DrawerSnapPoints.test.tsx:123-130).
  it('resolves a single snap point without a deduplication pass', () => {
    const resolved = resolveSnapPoints(['100px'], 400, 300, 16);
    expect(resolved).toEqual([{ value: '100px', height: 100, offset: 200 }]);
    expect(resolveActiveSnapPointOffset('100px', resolved, 400, 300, 16)).toBe(200);
  });

  it('port: the offset variable is negative for an up drawer and 0px without snap points', () => {
    expect(snapOffset(mount({ swipeDirection: 'up', snapPoints: ['100px', '200px'], activeSnapPoint: '100px' }).popup)).toBe('-200px');
    expect(snapOffset(mount().popup)).toBe('0px');
  });
});

describe('drawer module: the snap point release (DRW-PTR-11, ALG-01)', () => {
  // DRW-STA-17, ALG-05 (DrawerViewport.test.tsx:4439-4483): `100px` rests at offset 200, the larger of the two offsets.
  it('keeps the resting snap point progress when a press never starts a swipe', () => {
    const { backdrop, content } = mount({ snapPoints: ['100px', '200px'], activeSnapPoint: '100px' });
    expect(backdrop.style.getPropertyValue('--drawer-swipe-progress')).toBe('1');

    pointTarget = content;
    pointer('pointerdown', content, 100);
    pointer('pointerup', content, 100);

    expect(backdrop.style.getPropertyValue('--drawer-swipe-progress')).toBe('1');
  });

  // Offsets: `100px` at 200, `200px` at 100, `300px` at 0.
  const three = { snapPoints: ['100px', '200px', '300px'] };

  it('port: a slow drag settles on the nearest snap point and requests it with reason swipe on Blazor Server', () => {
    const { popup, fake } = mount({ ...three, activeSnapPoint: '300px' });

    slowDrag(popup, 80);

    expect(fake.reported('OnSnapPointRequest')).toMatchObject([{ request: 1, index: 1 }]);
    expect(fake.reported('OnDismissRequest')).toEqual([]);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    // Optimistic until C# answers.
    expect(snapOffset(popup)).toBe('100px');
  });

  it('port: a slow drag that ends closer to closed than to the nearest snap point dismisses', () => {
    const { popup, fake } = mount({ ...three, activeSnapPoint: '100px' });

    slowDrag(popup, 80);

    expect(fake.reported('OnSnapPointRequest')).toMatchObject([{ request: 1, index: null }]);
    expect(fake.reported('OnDismissRequest')).toHaveLength(1);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);
  });

  it('port: a fast downward flick dismisses from the nearest snap point', () => {
    const { popup, fake } = mount({ ...three, activeSnapPoint: '300px' });

    flick(popup, 30);

    expect(fake.reported('OnSnapPointRequest')).toMatchObject([{ index: null }]);
    expect(fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: with snapToSequentialPoints a fast flick advances one snap point instead of dismissing', () => {
    const { popup, fake } = mount({ ...three, activeSnapPoint: '300px', snapToSequentialPoints: true });

    flick(popup, 30);

    expect(fake.reported('OnSnapPointRequest')).toMatchObject([{ index: 1 }]);
    expect(fake.reported('OnDismissRequest')).toEqual([]);
  });

  it('port: an upward drag from a lower snap point expands to the next one', () => {
    const { popup, fake } = mount({ ...three, activeSnapPoint: '100px' });

    slowDrag(popup, -90);

    expect(fake.reported('OnSnapPointRequest')).toMatchObject([{ index: 1 }]);
    expect(fake.reported('OnDismissRequest')).toEqual([]);
  });

  it('port: on Blazor Server the next update settles the requested snap point, and a canceled request rolls back', () => {
    const { popup, update } = mount({ ...three, activeSnapPoint: '300px' });

    slowDrag(popup, 80);
    expect(snapOffset(popup)).toBe('100px');

    update({ activeSnapPoint: '300px', answeredSnap: 1 });
    expect(snapOffset(popup)).toBe('0px');
  });

  it('port: on WebAssembly a snap request asks the snap-point point with the index, and a canceled close settles in place', () => {
    const settled = mount({ ...three, activeSnapPoint: '300px' }, true);
    slowDrag(settled.popup, 80);
    expect(settled.fake.decisions.map((decision) => decision.point)).toEqual(['snap-point:1']);
    expect(settled.fake.reported('OnSnapPointRequest')).toEqual([]);

    const canceled = mount({ ...three, activeSnapPoint: '100px' }, true, 'stop');
    slowDrag(canceled.popup, 80);
    expect(canceled.fake.decisions.map((decision) => decision.point)).toEqual(['snap-point:none']);
    expect(canceled.popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(canceled.popup.hasAttribute('data-ending-style')).toBe(false);
    expect(canceled.fake.reported('OnDismissRequest')).toEqual([]);
  });

  it('port: a horizontal drawer with snap points, or a list that resolves to nothing, uses regular dismissal', () => {
    const horizontal = mount({ swipeDirection: 'right', snapPoints: ['100px', '200px'], activeSnapPoint: '100px' });
    horizontal.popup.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, button: 0, buttons: 1, pointerId: 1, clientX: 10, clientY: 10 }));
    for (const x of [10, 100, 200]) {
      horizontal.popup.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, buttons: 1, pointerId: 1, clientX: x, clientY: 10 }));
    }
    horizontal.popup.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerId: 1, clientX: 200, clientY: 10 }));
    expect(horizontal.fake.reported('OnSnapPointRequest')).toEqual([]);
    expect(horizontal.fake.reported('OnDismissRequest')).toHaveLength(1);

    const invalid = mount({ snapPoints: ['invalid'], activeSnapPoint: null });
    slowDrag(invalid.popup, 200);
    expect(invalid.fake.reported('OnSnapPointRequest')).toEqual([]);
    expect(invalid.fake.reported('OnDismissRequest')).toHaveLength(1);
  });
});
