// L3 (ADR 06 §1): the `toast` component module against a fake twin, in jsdom. The swipe titles are Base UI's from
// ToastRoot.test.tsx's `describe.skipIf(isJSDOM)('swipe behavior')` and 'drag behavior regression' (toast.md's [L3] rows
// TST-PTR-08, 11..21), with `movementX/Y` set explicitly (toast.md §16 Q5) and the toast's removal read as the release's
// dismissal report (C# closes the toast; the removal is L4). The port's titles cover the measurement, the Content
// observers, the Viewport's reports (one per change), the bands, the release freeze and the revision rule.
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  TOAST_SWIPE_IGNORE_SELECTOR,
  applyDirectionalDamping,
  armingCandidate,
  attach,
  dismissDirection,
  type ToastOptions,
  type ToastReport,
} from './toast';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';
import { firePointer } from './test-support/pointer';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  vi.useRealTimers();
});

function options(overrides: Partial<ToastOptions>): string {
  return JSON.stringify({ revision: 0, ...overrides });
}

interface AttachedRoot {
  element: HTMLElement;
  fake: FakeTwin;
  handle: TwinHandle;
  reports(): ToastReport[];
}

function attachRoot(overrides: Partial<ToastOptions> = {}, element: HTMLElement = document.createElement('div')): AttachedRoot {
  if (!element.isConnected) {
    document.body.append(element);
  }
  const fake = createFakeTwin();
  const handle = attach(element, options({ part: 'root', directions: ['down', 'right'], lifecycle: 0, ...overrides }), fake.twin);
  handles.push(handle);
  return { element, fake, handle, reports: () => fake.reported<ToastReport>('OnReport') };
}

let stamp = 1;

function pointer(type: 'down' | 'move' | 'up', element: Element, init: PointerEventInit): boolean {
  return firePointer[type](element, { pointerId: 1, button: 0, ...init, timeStamp: stamp++ });
}

function cancel(target: EventTarget, init: PointerEventInit = {}): void {
  target.dispatchEvent(new PointerEvent('pointercancel', { bubbles: true, cancelable: true, pointerId: 1, ...init }));
}

// ToastRoot.test.tsx:15-76: a press, a one-pixel first move toward the end, the move to the end, the release.
function simulateSwipe(
  element: HTMLElement,
  startX: number,
  startY: number,
  endX: number,
  endY: number,
  releaseTarget: EventTarget = element,
  releaseType: 'pointerup' | 'pointercancel' = 'pointerup',
): void {
  pointer('down', element, { clientX: startX, clientY: startY });
  pointer('move', element, { clientX: startX + Math.sign(endX - startX), clientY: startY + Math.sign(endY - startY) });
  pointer('move', element, { clientX: endX, clientY: endY });
  if (releaseType === 'pointercancel') {
    cancel(releaseTarget, { clientX: endX, clientY: endY });
  } else {
    releaseTarget.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, cancelable: true, pointerId: 1, clientX: endX, clientY: endY }));
  }
}

function releases(root: AttachedRoot): ToastReport[] {
  return root.reports().filter((report) => report.kind === 'release');
}

function dismissed(root: AttachedRoot): boolean {
  return releases(root).some((report) => report.dismiss === true);
}

describe('the gesture arithmetic', () => {
  it('port: damps only the components in a direction that does not dismiss', () => {
    expect(applyDirectionalDamping(['down', 'right'], -16, 9)).toEqual({ x: -4, y: 9 });
    expect(applyDirectionalDamping(['up'], 25, -30)).toEqual({ x: 5, y: -30 });
  });

  it('port: arms by the locked axis, else by the larger delta, a zero delta reading as left', () => {
    expect(armingCandidate('vertical', 50, 0)).toBeUndefined();
    expect(armingCandidate('horizontal', 0, 50)).toBeUndefined();
    expect(armingCandidate(null, 0, 0)).toBe('left');
    expect(armingCandidate(null, 3, -4)).toBe('up');
  });

  it('port: dismisses in the first allowed direction past 40 px, in the order given', () => {
    expect(dismissDirection(['right', 'down'], 41, 41)).toBe('right');
    expect(dismissDirection(['down', 'right'], 41, 41)).toBe('down');
    expect(dismissDirection(['down'], 0, 40)).toBeUndefined();
  });

  it('port: ignores interactive targets and both swipe-ignore attributes', () => {
    expect(TOAST_SWIPE_IGNORE_SELECTOR).toBe('button,a,input,textarea,[role="button"],[data-base-ui-swipe-ignore],[data-swipe-ignore]');
  });
});

describe('swipe behavior', () => {
  it.each([
    ['left', -60, 0],
    ['right', 60, 0],
    ['up', 0, -60],
    ['down', 0, 60],
  ] as const)('dismisses with a real %s pointer swipe', (direction, deltaX, deltaY) => {
    const root = attachRoot({ directions: [direction] });

    simulateSwipe(root.element, 100, 100, 100 + deltaX, 100 + deltaY);

    expect(releases(root)).toEqual([expect.objectContaining({ direction, dismiss: true })]);
  });

  it('closes toast when swiping in the specified direction beyond threshold', () => {
    const root = attachRoot({ directions: ['up'] });

    simulateSwipe(root.element, 100, 100, 100, 55);

    expect(dismissed(root)).toBe(true);
  });

  it('locks to a single axis on the first move of a two-axis swipe', () => {
    const root = attachRoot({ directions: ['down', 'right'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 101, clientY: 101 });
    pointer('move', root.element, { clientX: 120, clientY: 150 });

    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('0px');
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-y')).not.toBe('0px');

    pointer('up', root.element, { clientX: 120, clientY: 150 });
  });

  it('does not close toast when swiping in the specified direction below threshold', () => {
    const root = attachRoot({ directions: ['up'] });

    simulateSwipe(root.element, 100, 100, 100, 90);

    expect(releases(root)).toEqual([expect.objectContaining({ direction: null, dismiss: false })]);
  });

  it('does not close toast when swiping in a non-specified direction', () => {
    const root = attachRoot({ directions: ['up'] });

    simulateSwipe(root.element, 100, 100, 100, 150);

    expect(releases(root)).toEqual([expect.objectContaining({ direction: null, dismiss: false })]);
  });

  describe('supports multiple swipe directions', () => {
    it('up + right', () => {
      const right = attachRoot({ directions: ['up', 'right'] });
      simulateSwipe(right.element, 100, 100, 150, 100);
      expect(releases(right)).toEqual([expect.objectContaining({ direction: 'right', dismiss: true })]);

      const up = attachRoot({ directions: ['up', 'right'] });
      simulateSwipe(up.element, 100, 100, 100, 50);
      expect(releases(up)).toEqual([expect.objectContaining({ direction: 'up', dismiss: true })]);
    });

    it('right + left', () => {
      const right = attachRoot({ directions: ['right', 'left'] });
      simulateSwipe(right.element, 100, 100, 150, 100);
      expect(releases(right)).toEqual([expect.objectContaining({ direction: 'right', dismiss: true })]);

      const left = attachRoot({ directions: ['right', 'left'] });
      simulateSwipe(left.element, 100, 100, 50, 100);
      expect(releases(left)).toEqual([expect.objectContaining({ direction: 'left', dismiss: true })]);
    });

    it('up + down', () => {
      const up = attachRoot({ directions: ['up', 'down'] });
      simulateSwipe(up.element, 100, 100, 100, 50);
      expect(releases(up)).toEqual([expect.objectContaining({ direction: 'up', dismiss: true })]);

      const down = attachRoot({ directions: ['up', 'down'] });
      simulateSwipe(down.element, 100, 100, 100, 150);
      expect(releases(down)).toEqual([expect.objectContaining({ direction: 'down', dismiss: true })]);
    });
  });

  it('cancels a vertical swipe when the pointer changes its mind', () => {
    const root = attachRoot({ directions: ['up'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 100, clientY: 99, movementY: -1 });
    pointer('move', root.element, { clientX: 100, clientY: 10, movementY: -89 });
    expect(root.element.getAttribute('data-swipe-direction')).toBe('up');

    pointer('move', root.element, { clientX: 100, clientY: 50, movementY: 40 });
    pointer('up', root.element, { clientX: 100, clientY: 50 });

    expect(releases(root)).toEqual([expect.objectContaining({ direction: null, dismiss: false })]);
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-y')).toBe('0px');
  });

  it('cancels a horizontal swipe when the pointer changes its mind', () => {
    const root = attachRoot({ directions: ['right'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 101, clientY: 100, movementX: 1 });
    pointer('move', root.element, { clientX: 190, clientY: 100, movementX: 89 });
    expect(root.element.getAttribute('data-swipe-direction')).toBe('right');

    pointer('move', root.element, { clientX: 150, clientY: 100, movementX: -40 });
    pointer('up', root.element, { clientX: 150, clientY: 100 });

    expect(releases(root)).toEqual([expect.objectContaining({ direction: null, dismiss: false })]);
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('0px');
  });

  it('locks the gesture to the horizontal axis when it starts sideways', () => {
    const root = attachRoot({ directions: ['down', 'right'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 95, clientY: 98 });
    pointer('move', root.element, { clientX: 90, clientY: 98 });
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);

    pointer('move', root.element, { clientX: 100, clientY: 98 });
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);

    pointer('move', root.element, { clientX: 110, clientY: 220 });
    expect(root.element.getAttribute('data-swipe-direction')).toBe('right');

    pointer('move', root.element, { clientX: 160, clientY: 220 });
    pointer('up', root.element, { clientX: 160, clientY: 220 });

    expect(releases(root)).toEqual([expect.objectContaining({ direction: 'right', dismiss: true })]);
  });

  it('locks the gesture to the vertical axis when it starts upright', () => {
    const root = attachRoot({ directions: ['down', 'right'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 98, clientY: 95 });
    pointer('move', root.element, { clientX: 98, clientY: 90 });
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);

    pointer('move', root.element, { clientX: 98, clientY: 100 });
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);

    pointer('move', root.element, { clientX: 220, clientY: 110 });
    expect(root.element.getAttribute('data-swipe-direction')).toBe('down');

    pointer('move', root.element, { clientX: 220, clientY: 160 });
    pointer('up', root.element, { clientX: 220, clientY: 160 });

    expect(releases(root)).toEqual([expect.objectContaining({ direction: 'down', dismiss: true })]);
  });

  it('does not start a swipe from a non-primary pointer button', () => {
    const root = attachRoot({ directions: ['right'] });

    pointer('down', root.element, { clientX: 100, clientY: 100, button: 2 });
    pointer('move', root.element, { clientX: 101, clientY: 100 });
    expect(root.reports()).toEqual([expect.objectContaining({ kind: 'height' })]);

    pointer('move', root.element, { clientX: 200, clientY: 100 });
    pointer('up', root.element, { clientX: 200, clientY: 100 });

    expect(root.element.hasAttribute('data-swiping')).toBe(false);
    expect(releases(root)).toEqual([]);
  });

  it('applies [data-swiping] attribute when swiping', () => {
    const root = attachRoot({ directions: ['up'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });

    expect(root.element.getAttribute('data-swiping')).toBe('');
    expect(root.reports().at(-1)).toEqual(expect.objectContaining({ kind: 'swipe-start' }));
  });

  it('dismisses toast when swiped down with downward swipe direction', () => {
    const root = attachRoot({ directions: ['down'] });
    simulateSwipe(root.element, 100, 100, 100, 150);
    expect(releases(root)).toEqual([expect.objectContaining({ direction: 'down', dismiss: true })]);
  });

  it('dismisses toast when swiped left with leftward swipe direction', () => {
    const root = attachRoot({ directions: ['left'] });
    simulateSwipe(root.element, 100, 100, 50, 100);
    expect(releases(root)).toEqual([expect.objectContaining({ direction: 'left', dismiss: true })]);
  });

  it('dismisses toast when swiped right with rightward swipe direction', () => {
    const root = attachRoot({ directions: ['right'] });
    simulateSwipe(root.element, 100, 100, 150, 100);
    expect(releases(root)).toEqual([expect.objectContaining({ direction: 'right', dismiss: true })]);
  });

  it('allows swiping in multiple directions when specified', () => {
    const up = attachRoot({ directions: ['up', 'right'] });
    simulateSwipe(up.element, 100, 100, 100, 50);
    expect(releases(up)).toEqual([expect.objectContaining({ direction: 'up', dismiss: true })]);

    const right = attachRoot({ directions: ['up', 'right'] });
    simulateSwipe(right.element, 100, 100, 150, 100);
    expect(releases(right)).toEqual([expect.objectContaining({ direction: 'right', dismiss: true })]);
  });

  it('does not dismiss when swiped in non-specified direction', () => {
    const root = attachRoot({ directions: ['up'] });

    simulateSwipe(root.element, 100, 100, 150, 100);
    simulateSwipe(root.element, 100, 100, 100, 150);

    expect(releases(root)).toEqual([
      expect.objectContaining({ direction: null, dismiss: false }),
      expect.objectContaining({ direction: null, dismiss: false }),
    ]);
  });

  it('does not dismiss when swipe distance is below threshold', () => {
    const root = attachRoot({ directions: ['up'] });

    simulateSwipe(root.element, 100, 100, 100, 95);

    expect(releases(root)).toEqual([expect.objectContaining({ direction: null, dismiss: false })]);
  });

  it('prevents native touchmove only during an active touch swipe', () => {
    const root = attachRoot({ directions: ['up'] });

    const beforeSwipeEvent = new Event('touchmove', { bubbles: true, cancelable: true });
    root.element.dispatchEvent(beforeSwipeEvent);
    expect(beforeSwipeEvent.defaultPrevented).toBe(false);

    pointer('down', root.element, { clientX: 100, clientY: 100, pointerType: 'touch' });

    const duringSwipeEvent = new Event('touchmove', { bubbles: true, cancelable: true });
    root.element.dispatchEvent(duringSwipeEvent);
    expect(duringSwipeEvent.defaultPrevented).toBe(true);

    cancel(root.element, { pointerType: 'touch' });

    const afterSwipeEvent = new Event('touchmove', { bubbles: true, cancelable: true });
    root.element.dispatchEvent(afterSwipeEvent);
    expect(afterSwipeEvent.defaultPrevented).toBe(false);
  });

  it('does not start swiping from elements with the data-base-ui-swipe-ignore attribute', () => {
    const root = attachRoot({ directions: ['up'] });
    const ignoreTarget = document.createElement('div');
    ignoreTarget.setAttribute('data-base-ui-swipe-ignore', '');
    root.element.append(ignoreTarget);
    const control = attachRoot({ directions: ['up'] });

    pointer('down', control.element, { clientX: 100, clientY: 100 });
    pointer('down', ignoreTarget, { clientX: 100, clientY: 100 });

    expect(control.element.getAttribute('data-swiping')).toBe('');
    expect(root.element.hasAttribute('data-swiping')).toBe(false);
  });

  it('does not start swiping from elements with the legacy data-swipe-ignore attribute', () => {
    const root = attachRoot({ directions: ['up'] });
    const ignoreTarget = document.createElement('div');
    ignoreTarget.setAttribute('data-swipe-ignore', '');
    root.element.append(ignoreTarget);
    const control = attachRoot({ directions: ['up'] });

    pointer('down', control.element, { clientX: 100, clientY: 100 });
    pointer('down', ignoreTarget, { clientX: 100, clientY: 100 });

    expect(control.element.getAttribute('data-swiping')).toBe('');
    expect(root.element.hasAttribute('data-swiping')).toBe(false);
  });

  it('port: a touch press pauses the timers even on an ignored target (TST-PTR-09)', () => {
    const root = attachRoot({ directions: ['up'] });
    const button = document.createElement('button');
    root.element.append(button);

    pointer('down', button, { clientX: 100, clientY: 100, pointerType: 'touch' });

    expect(root.reports().map((report) => report.kind)).toEqual(['height', 'touch-press']);
    expect(root.element.hasAttribute('data-swiping')).toBe(false);
  });

  it('port: an anchored toast, with no directions, never starts a swipe (TST-PTR-22)', () => {
    const anchored = attachRoot({ directions: [] });
    const control = attachRoot({ directions: ['up'] });

    simulateSwipe(control.element, 100, 100, 100, 55);
    simulateSwipe(anchored.element, 100, 100, 100, 55);

    expect(dismissed(control)).toBe(true);
    expect(anchored.reports().map((report) => report.kind)).toEqual(['height']);
    expect(anchored.element.hasAttribute('data-swiping')).toBe(false);
  });

  it('port: moves of another pointer are ignored and not prevented (TST-PTR-23)', () => {
    const root = attachRoot({ directions: ['up'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 100, clientY: 99 });
    const own = pointer('move', root.element, { clientX: 100, clientY: 97 });
    const other = firePointer.move(root.element, { pointerId: 2, clientX: 100, clientY: 10, timeStamp: stamp++ });

    expect(own).toBe(false);
    expect(other).toBe(true);
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-y')).toBe('-2px');
  });
});

describe('drag behavior regression', () => {
  it('resets drag state after releasing a far swipe even when the release lands on the document', () => {
    const root = attachRoot();

    simulateSwipe(root.element, 300, 100, -5000, 100, document);

    expect(releases(root)).toEqual([expect.objectContaining({ dismiss: false })]);
    expect(root.element.hasAttribute('data-swiping')).toBe(false);
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('0px');
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-y')).toBe('0px');
    expect(root.element.style.transition).toBe('');
    expect(root.element.style.transform).toBe('');

    simulateSwipe(root.element, 100, 100, 150, 100);

    expect(dismissed(root)).toBe(true);
  });

  it('resets drag state after a document pointercancel', () => {
    const root = attachRoot();

    pointer('down', root.element, { clientX: 300, clientY: 100 });
    pointer('move', root.element, { clientX: 299, clientY: 100 });
    expect(root.element.style.transform).not.toBe('');

    pointer('move', root.element, { clientX: -5000, clientY: 100 });
    cancel(document);

    expect(releases(root)).toEqual([expect.objectContaining({ dismiss: false })]);
    expect(root.element.hasAttribute('data-swiping')).toBe(false);
    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('0px');
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-y')).toBe('0px');
    expect(root.element.style.transition).toBe('');
    expect(root.element.style.transform).toBe('');
  });
});

describe('the drag styles and the release freeze', () => {
  it('port: freezes the element in the component-under band while swiping', () => {
    const root = attachRoot({ directions: ['right'] });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 101, clientY: 100 });
    pointer('move', root.element, { clientX: 130, clientY: 100 });

    expect(root.element.style.transition).toBe('none');
    expect(root.element.style.transform).toBe('translateX(29px) translateY(0px) scale(1)');
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('29px');
  });

  it("port: a consumer transform beats the swipe's", () => {
    const root = attachRoot({ directions: ['right'], style: 'transform: rotate(1deg)' });

    pointer('down', root.element, { clientX: 100, clientY: 100 });
    pointer('move', root.element, { clientX: 101, clientY: 100 });
    pointer('move', root.element, { clientX: 130, clientY: 100 });

    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('29px');
    expect(root.element.style.transform).toBe('rotate(1deg)');
  });

  it('port: a dismissing release keeps the drag styles until data-ending-style lands, then drops the freeze', async () => {
    const root = attachRoot({ directions: ['right'] });

    simulateSwipe(root.element, 100, 100, 160, 100);

    expect(root.element.getAttribute('data-swipe-direction')).toBe('right');
    expect(root.element.hasAttribute('data-swiping')).toBe(false);
    expect(root.element.style.transition).toBe('none');
    expect(root.element.style.transform).toBe('translateX(59px) translateY(0px) scale(1)');

    root.element.setAttribute('data-ending-style', '');
    await Promise.resolve();

    expect(root.element.style.transition).toBe('');
    expect(root.element.style.transform).toBe('');
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('59px');
    expect(root.element.getAttribute('data-swipe-direction')).toBe('right');
  });

  it('port: the movement variables rest at 0px from the attach, under the consumer style', () => {
    const root = attachRoot({ style: '--toast-index: 2; color: red' });

    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('0px');
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-y')).toBe('0px');
    expect(root.element.style.getPropertyValue('--toast-index')).toBe('2');
  });
});

describe('the revision rule', () => {
  it('port: an update older than the last report is ignored, and a newer one applies', () => {
    const root = attachRoot({ directions: ['right'], style: '--toast-index: 0' });
    simulateSwipe(root.element, 100, 100, 160, 100);
    const last = root.reports().at(-1)!.revision;

    root.handle.update(options({ part: 'root', directions: ['right'], lifecycle: 0, revision: last - 1, style: '--toast-index: 5' }));
    expect(root.element.style.getPropertyValue('--toast-index')).toBe('0');
    expect(root.element.style.transform).toBe('translateX(59px) translateY(0px) scale(1)');

    root.handle.update(options({ part: 'root', directions: ['right'], lifecycle: 0, revision: last, style: '--toast-index: 1' }));
    expect(root.element.style.getPropertyValue('--toast-index')).toBe('1');
    expect(root.element.style.transform).toBe('translateX(59px) translateY(0px) scale(1)');
  });

  it('port: a new lifecycle is the rollback: it resets the swipe state and measures again, however old its revision', () => {
    const root = attachRoot({ directions: ['right'] });
    simulateSwipe(root.element, 100, 100, 160, 100);

    root.handle.update(options({ part: 'root', directions: ['right'], lifecycle: 1, revision: 0 }));

    expect(root.element.hasAttribute('data-swipe-direction')).toBe(false);
    expect(root.element.style.getPropertyValue('--toast-swipe-movement-x')).toBe('0px');
    expect(root.element.style.transform).toBe('');
    expect(root.reports().at(-1)).toEqual(expect.objectContaining({ kind: 'height', lifecycle: 1 }));
  });
});

describe('the measurement', () => {
  function measuredRoot(natural: () => number, style?: string): AttachedRoot {
    const element = document.createElement('div');
    Object.defineProperty(element, 'offsetHeight', {
      configurable: true,
      get: () => (element.style.height === 'auto' || element.style.height === '' ? natural() : parseFloat(element.style.height)),
    });
    return attachRoot(style === undefined ? {} : { style }, element);
  }

  it('port: measures at the attach with a transient auto height, and reports the height once per change', () => {
    let natural = 48;
    const root = measuredRoot(() => natural);

    expect(root.reports()).toEqual([expect.objectContaining({ kind: 'height', height: 48, lifecycle: 0 })]);
    expect(root.element.style.height).toBe('');
    natural = 60;
    root.handle.update(options({ part: 'root', directions: ['down', 'right'], lifecycle: 1 }));
    expect(root.reports().at(-1)).toEqual(expect.objectContaining({ kind: 'height', height: 60, lifecycle: 1 }));
  });

  it("port: a consumer height loses to the transient auto and returns after it", () => {
    const root = measuredRoot(() => 48, 'height: 10px');

    expect(root.reports()).toEqual([expect.objectContaining({ kind: 'height', height: 48 })]);
    expect(root.element.style.height).toBe('10px');
  });

  it('port: the content observes its subtree and measures its root again on a change that moves the height', async () => {
    let natural = 40;
    const root = measuredRoot(() => natural);
    const content = document.createElement('div');
    root.element.append(content);
    const observed: ResizeObserverCallback[] = [];
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: ResizeObserverCallback) {
          observed.push(callback);
        }
        observe(): void {}
        disconnect(): void {}
      },
    );
    try {
      const handle = attach(content, options({ part: 'content', rootRef: 0 }), createFakeTwin().twin, [root.element]);
      handles.push(handle);
      expect(observed).toHaveLength(1);

      natural = 72;
      content.append(document.createTextNode('more'));
      await Promise.resolve();
      expect(root.reports().map((report) => report.height)).toEqual([40, 72]);

      content.append(document.createTextNode('same height'));
      await Promise.resolve();
      expect(root.reports().map((report) => report.height)).toEqual([40, 72]);
    } finally {
      vi.unstubAllGlobals();
    }
  });
});

describe('Escape', () => {
  it('port: closes when focus is inside the root, and not when it is elsewhere (TST-KEY-05, TST-KEY-06)', () => {
    const root = attachRoot();
    root.element.tabIndex = 0;
    const outside = document.createElement('button');
    document.body.append(outside);

    root.element.focus();
    root.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    outside.focus();
    root.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

    expect(root.reports().filter((report) => report.kind === 'escape')).toHaveLength(1);
  });
});

describe('the viewport', () => {
  interface AttachedViewport {
    element: HTMLElement;
    handle: TwinHandle;
    kinds(): string[];
    reports(): ToastReport[];
    update(overrides: Partial<ToastOptions>, references?: unknown[]): void;
  }

  function attachViewport(overrides: Partial<ToastOptions> = {}): AttachedViewport {
    const element = document.createElement('div');
    element.tabIndex = -1;
    document.body.append(element);
    const fake = createFakeTwin();
    let current: Partial<ToastOptions> = { part: 'viewport', empty: false, hovering: false, focused: false, hasEnding: false, ...overrides };
    const handle = attach(element, options(current), fake.twin);
    handles.push(handle);
    const reports = () => fake.reported<ToastReport>('OnReport');
    return {
      element,
      handle,
      reports,
      kinds: () => reports().map((report) => report.kind),
      update(next, references) {
        current = { ...current, ...next };
        handle.update(options(current), references);
      },
    };
  }

  it('port: hover reports once on enter, not per move, and a leave reports once', () => {
    const viewport = attachViewport();

    viewport.element.dispatchEvent(new MouseEvent('mouseenter'));
    for (let i = 0; i < 5; i++) {
      viewport.element.dispatchEvent(new MouseEvent('mousemove', { bubbles: true }));
    }
    viewport.element.dispatchEvent(new MouseEvent('mouseleave'));

    expect(viewport.kinds()).toEqual(['enter', 'leave']);
  });

  it('port: a leave while a toast is ending waits until none is (TST-PTR-03)', () => {
    const viewport = attachViewport({ hasEnding: true });

    viewport.element.dispatchEvent(new MouseEvent('mouseenter'));
    viewport.element.dispatchEvent(new MouseEvent('mouseleave'));
    expect(viewport.kinds()).toEqual(['enter']);

    viewport.update({ hasEnding: false, revision: 1, hovering: true });
    expect(viewport.kinds()).toEqual(['enter', 'leave']);
  });

  it('port: a leave during a touch waits for the touch to end, and a move cancels it (TST-PTR-04)', () => {
    const viewport = attachViewport();

    viewport.element.dispatchEvent(new PointerEvent('pointerdown', { pointerType: 'touch', bubbles: true }));
    viewport.element.dispatchEvent(new MouseEvent('mouseleave'));
    viewport.element.dispatchEvent(new PointerEvent('pointercancel', { pointerType: 'touch', bubbles: true }));
    expect(viewport.kinds()).toEqual(['leave']);

    viewport.element.dispatchEvent(new PointerEvent('pointerdown', { pointerType: 'touch', bubbles: true }));
    viewport.element.dispatchEvent(new MouseEvent('mouseleave'));
    viewport.element.dispatchEvent(new MouseEvent('mousemove'));
    viewport.element.dispatchEvent(new PointerEvent('pointerup', { pointerType: 'touch', bubbles: true }));
    expect(viewport.kinds()).toEqual(['leave', 'enter']);
  });

  it('port: a move pauses again once the store has cleared hovering', () => {
    const viewport = attachViewport();

    viewport.element.dispatchEvent(new MouseEvent('mousemove'));
    viewport.update({ revision: 1, hovering: true });
    viewport.element.dispatchEvent(new MouseEvent('mousemove'));
    viewport.update({ revision: 1, hovering: false });
    viewport.element.dispatchEvent(new MouseEvent('mousemove'));

    expect(viewport.kinds()).toEqual(['enter', 'enter']);
  });

  it('port: F6 records the previous focus, focuses the viewport and reports once; F6 on the viewport is not handled', () => {
    const button = document.createElement('button');
    document.body.prepend(button);
    button.focus();
    const viewport = attachViewport();

    const handled = !window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F6', cancelable: true }));
    const onViewport = viewport.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'F6', cancelable: true, bubbles: true }));

    expect(handled).toBe(true);
    expect(document.activeElement).toBe(viewport.element);
    expect(viewport.kinds()).toEqual(['f6']);
    expect(onViewport).toBe(true);

    viewport.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(button);
  });

  it('port: the global listeners are bound only while the list is non-empty, once per cycle (TST-FOC-14)', () => {
    const viewport = attachViewport({ empty: true });
    const add = vi.spyOn(window, 'addEventListener');

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F6', cancelable: true }));
    viewport.update({ empty: false });
    viewport.update({ empty: false, revision: 0 });
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F6', cancelable: true }));
    viewport.update({ empty: true });
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F6', cancelable: true }));

    expect(viewport.kinds().filter((kind) => kind === 'f6')).toEqual(['f6']);
    expect(add.mock.calls.filter(([type]) => type === 'keydown')).toHaveLength(1);
  });

  it('port: a touch outside reports, a touch inside or a mouse press outside does not (TST-PTR-05..07)', () => {
    const viewport = attachViewport();
    const inside = document.createElement('span');
    viewport.element.append(inside);

    inside.dispatchEvent(new PointerEvent('pointerdown', { pointerType: 'touch', bubbles: true }));
    document.body.dispatchEvent(new PointerEvent('pointerdown', { pointerType: 'mouse', bubbles: true }));
    document.body.dispatchEvent(new PointerEvent('pointerdown', { pointerType: 'touch', bubbles: true }));

    expect(viewport.kinds()).toEqual(['touch-outside']);
  });

  it('port: a window blur and focus report, the focused flag after a macrotask (TST-FOC-11, TST-FOC-12)', () => {
    vi.useFakeTimers();
    const viewport = attachViewport();

    window.dispatchEvent(new FocusEvent('blur'));
    window.dispatchEvent(new FocusEvent('focus'));
    expect(viewport.kinds()).toEqual(['window-blur', 'window-focus']);
    expect(viewport.reports()[1]).toEqual(expect.objectContaining({ resume: true }));

    vi.advanceTimersByTime(0);
    expect(viewport.kinds()).toEqual(['window-blur', 'window-focus', 'window-focused']);

    // A later focus inside the viewport, focus-visible, resumes nothing and finds the window focused already.
    const inside = document.createElement('button');
    viewport.element.append(inside);
    inside.focus();
    vi.advanceTimersByTime(0);
    expect(viewport.kinds()).toEqual(['window-blur', 'window-focus', 'window-focused', 'focus']);
  });

  it('port: focus inside reports once, and a blur to an element outside reports once (TST-FOC-05, TST-FOC-06)', () => {
    const viewport = attachViewport();
    const first = document.createElement('button');
    const second = document.createElement('button');
    const outside = document.createElement('button');
    viewport.element.append(first, second);
    document.body.append(outside);

    first.focus();
    second.focus();
    outside.focus();

    expect(viewport.kinds()).toEqual(['focus', 'blur']);
  });

  it('port: a guard focused from the viewport moves to the first focusable toast; from elsewhere, back to the previous focus', () => {
    const viewport = attachViewport();
    const button = document.createElement('button');
    document.body.prepend(button);
    const outerGuard = document.createElement('span');
    outerGuard.tabIndex = 0;
    outerGuard.setAttribute('data-base-ui-focus-guard', '');
    viewport.element.before(outerGuard);
    const [innerFirst, innerLast] = [0, 1].map(() => {
      const guard = document.createElement('span');
      guard.tabIndex = 0;
      guard.setAttribute('data-base-ui-focus-guard', '');
      return guard;
    });
    const toast = document.createElement('div');
    toast.tabIndex = 0;
    viewport.element.append(innerFirst!, toast, innerLast!);
    viewport.update({ focusTargetRef: 0 }, [toast]);

    button.focus();
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F6', cancelable: true }));
    innerFirst!.focus();
    expect(document.activeElement).toBe(toast);

    innerLast!.focus();
    expect(document.activeElement).toBe(button);

    outerGuard.focus();
    expect(document.activeElement).toBe(button);
  });

  it('port: a hand-off focuses its target once, and only while focus is inside the viewport (TST-FOC-07..09)', () => {
    const viewport = attachViewport();
    const [closing, next] = [0, 1].map(() => {
      const toast = document.createElement('div');
      toast.tabIndex = 0;
      viewport.element.append(toast);
      return toast;
    });
    const outside = document.createElement('button');
    document.body.append(outside);

    closing!.focus();
    viewport.update({ handOff: { revision: 1, toPrevious: false, targetRef: 0 } }, [next]);
    expect(document.activeElement).toBe(next);

    closing!.focus();
    viewport.update({ handOff: { revision: 1, toPrevious: false, targetRef: 0 } }, [next]);
    expect(document.activeElement).toBe(closing);

    outside.focus();
    viewport.update({ handOff: { revision: 2, toPrevious: false, targetRef: 0 } }, [next]);
    expect(document.activeElement).toBe(outside);
  });
});
