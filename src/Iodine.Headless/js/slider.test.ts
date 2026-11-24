// L3 (ADR 06 §1): the `slider` component module in jsdom. This first block is the seven value algorithms (SLD §4.1),
// which live only here (ADR 01 decision 2; ADR 04 Amendments 2026-10-18, the placement rule): Base UI's four util test
// files verbatim (`slider/utils/{roundValueToStep,getSliderValue,getPushedThumbValues,resolveThumbCollision}.test.ts`,
// 2 + 4 + 6 + 12 titles), then the port titles of SLD-STA-13 and SLD-STA-15, whose Base UI titles drive the algorithms
// through a rendered slider (`SliderRoot.test.tsx`), and of SLD-STA-17.
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  KEY_POINT,
  PRESS_POINT,
  attach,
  getDecimalPrecision,
  getNewValue,
  getPushedThumbValues,
  getSliderValue,
  resolveThumbCollision,
  roundValueToStep,
  validateMinimumDistance,
  type SliderOptions,
} from './slider';
import type { DecisionPolicy, TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

describe('roundValueToStep', () => {
  it('preserves precision from the step origin', () => {
    expect(roundValueToStep(0.35, 0.1, 0.25)).toBe(0.35);
  });

  it('preserves decimal precision for steps greater than one', () => {
    expect(roundValueToStep(13.2, 1.5, 10.2)).toBe(13.2);
  });

  // SLD-STA-13, SLD-STA-14: `SliderRoot.test.tsx` "should not fail to round value to step precision when step is very
  // small" (and "… and negative") drag a 100 px control from 20 px to 80 px; the port asserts the step rounding those
  // drags reach, the pointer path being the L4 title's.
  it('port: should not fail to round value to step precision when step is very small', () => {
    expect(getDecimalPrecision(0.00000001)).toBe(8);
    expect(roundValueToStep(0.000000079999, 0.00000001, 0)).toBe(8e-8);
    expect(roundValueToStep(0.00000002, 0.00000001, 0)).toBe(2e-8);
  });

  it('port: should not fail to round value to step precision when step is very small and negative', () => {
    expect(getDecimalPrecision(-0.0000001)).toBe(7);
    expect(roundValueToStep(-0.000000080001, 0.00000001, -0.0000001)).toBe(-8e-8);
    expect(roundValueToStep(-0.00000002, 0.00000001, -0.0000001)).toBe(-2e-8);
  });

  it('port: counts the fraction digits of an exponential mantissa and of a decimal', () => {
    expect(getDecimalPrecision(0)).toBe(0);
    expect(getDecimalPrecision(1.5e-7)).toBe(8);
    expect(getDecimalPrecision(0.25)).toBe(2);
    expect(getDecimalPrecision(10.2)).toBe(1);
    expect(getDecimalPrecision(100)).toBe(0);
  });

  it('port: should reach right edge value', () => {
    // SLD-STA-14: `6..108` step `10`, `max` off the grid: the nearest reachable grid value is 106.
    expect(roundValueToStep(108, 10, 6)).toBe(106);
  });
});

describe('getNewValue', () => {
  // SLD-STA-15 through `SliderRoot.test.tsx`'s keyboard titles: the caller rounds the thumb value first (SLD-KEY-06).
  it('port: preserves largeStep increments when step uses a different grid', () => {
    expect(getNewValue(roundValueToStep(20, 2, 0), 5, 1, 0, 100)).toBe(25);
  });

  it('port: rounds fractional values to the configured step', () => {
    expect(getNewValue(roundValueToStep(0.2, 0.1, 0), 0.1, 1, 0, 1)).toBe(0.3);
    expect(getNewValue(roundValueToStep(0.2, 0.1, 0), 0.1, -1, 0, 1)).toBe(0.1);
  });

  it('port: keypresses should correct invalid values', () => {
    expect(getNewValue(roundValueToStep(5.4698, 1, 0), 1, 1, 0, 10)).toBe(6);
  });

  it('port: clamps the stepped value to the bounds', () => {
    expect(getNewValue(roundValueToStep(99, 1, 0), 10, 1, 0, 100)).toBe(100);
    expect(getNewValue(roundValueToStep(1, 1, 0), 10, -1, 0, 100)).toBe(0);
  });
});

describe('getSliderValue', () => {
  it('clamps a single value to the min/max range', () => {
    expect(getSliderValue(150, 0, 0, 100, false, [50])).toBe(100);
    expect(getSliderValue(-10, 0, 0, 100, false, [50])).toBe(0);
  });

  describe('range neighbours', () => {
    it('does not let a thumb cross a neighbour whose value is 0', () => {
      expect(getSliderValue(5, 0, -10, 10, true, [-10, 0])).toEqual([0, 0]);
      expect(getSliderValue(-5, 1, -10, 10, true, [0, 10])).toEqual([0, 0]);
    });

    it('bounds a thumb between its real neighbours', () => {
      expect(getSliderValue(50, 1, 0, 100, true, [20, 40, 80])).toEqual([20, 50, 80]);
      expect(getSliderValue(90, 1, 0, 100, true, [20, 40, 80])).toEqual([20, 80, 80]);
      expect(getSliderValue(10, 1, 0, 100, true, [20, 40, 80])).toEqual([20, 20, 80]);
    });

    it('leaves the outer edges unbounded by missing neighbours', () => {
      expect(getSliderValue(-30, 0, -50, 50, true, [-20, 0])).toEqual([-30, 0]);
      expect(getSliderValue(40, 1, -50, 50, true, [-20, 0])).toEqual([-20, 40]);
    });
  });
});

describe('validateMinimumDistance', () => {
  // SLD-STA-17: `SliderRoot.test.tsx` "should enforce a minimum difference between range slider values" drives it
  // through the keyboard (L4); the port asserts the predicate.
  it('port: should enforce a minimum difference between range slider values', () => {
    expect(validateMinimumDistance(50, 10, 2)).toBe(true);
    expect(validateMinimumDistance([20, 40], 10, 2)).toBe(true);
    expect(validateMinimumDistance([20, 39], 10, 2)).toBe(false);
    expect(validateMinimumDistance([1, 1.5], 1, 0.4)).toBe(true);
    expect(validateMinimumDistance([20, Number.NaN], 1, 0)).toBe(false);
  });
});

describe('getPushedThumbValues', () => {
  it('pushes the next thumb forward when moving past it', () => {
    const result = getPushedThumbValues([20, 40], 0, 70, 0, 100, 1, 0);

    expect(result).toEqual([70, 70]);
  });

  it('ensures minimum distance between thumbs while pushing forward', () => {
    const result = getPushedThumbValues([20, 40], 0, 60, 0, 100, 1, 5);

    expect(result).toEqual([60, 65]);
  });

  it('pushes previous thumbs backward when moving before them', () => {
    const result = getPushedThumbValues([20, 40], 1, -10, 0, 100, 1, 0);

    expect(result).toEqual([0, 0]);
  });

  it('pushes multiple thumbs in sequence', () => {
    const result = getPushedThumbValues([10, 50, 90], 1, 95, 0, 100, 1, 5);

    expect(result).toEqual([10, 95, 100]);
  });

  it('allows fractional minimum distances', () => {
    const result = getPushedThumbValues([0, 1], 0, 1.4, 0, 10, 1, 0.4);

    expect(result[0]).toBe(1.4);
    expect(result[1]).toBe(1.8);
  });

  it('restores pushed thumbs towards their initial value when space allows', () => {
    const initialValues = [30, 50];

    const pushed = getPushedThumbValues(initialValues, 1, 20, 0, 100, 1, 0, initialValues);

    expect(pushed).toEqual([20, 20]);

    const restored = getPushedThumbValues(pushed, 1, 35, 0, 100, 1, 0, initialValues);

    expect(restored).toEqual([30, 35]);
  });
});

describe('resolveThumbCollision', () => {
  it('prevents thumbs from passing each other when behavior is "none"', () => {
    const result = resolveThumbCollision('none', [20, 40], undefined, undefined, 0, 70, 0, 100, 1, 0);

    expect(result.value).toEqual([40, 40]);
    expect(result.thumbIndex).toBe(0);
    expect(result.didSwap).toBe(false);
  });

  it('pushes thumbs forward without cling when behavior is "push"', () => {
    const result = resolveThumbCollision('push', [20, 40], undefined, undefined, 0, 70, 0, 100, 1, 0);

    expect(result.value).toEqual([70, 70]);
    expect(result.thumbIndex).toBe(0);
    expect(result.didSwap).toBe(false);
  });

  it('keeps pushed thumbs in place when moving backward in push mode', () => {
    const startValues = [20, 40];

    const pushed = resolveThumbCollision('push', startValues, startValues, startValues, 0, 70, 0, 100, 1, 0);

    const nextValues = pushed.value as number[];
    expect(nextValues).toEqual([70, 70]);

    const movedBack = resolveThumbCollision('push', nextValues, nextValues, startValues, 0, 30, 0, 100, 1, 0);

    expect(movedBack.value).toEqual([30, 70]);
    expect(movedBack.thumbIndex).toBe(0);
    expect(movedBack.didSwap).toBe(false);
  });

  it('swaps thumbs when behavior is "swap"', () => {
    const result = resolveThumbCollision('swap', [20, 40], undefined, undefined, 0, 65, 0, 100, 1, 0);

    expect(result.value).toEqual([40, 65]);
    expect(result.thumbIndex).toBe(1);
    expect(result.didSwap).toBe(true);
  });

  it('maintains swap continuity with minimum steps when provided current and initial values', () => {
    const startValues = [20, 80];

    const first = resolveThumbCollision('swap', startValues, startValues, startValues, 0, 85, 0, 100, 1, 10);

    const firstValues = first.value as number[];
    expect(firstValues).toEqual([70, 85]);
    expect(first.thumbIndex).toBe(1);
    expect(first.didSwap).toBe(true);

    const continued = resolveThumbCollision('swap', startValues, firstValues, startValues, first.thumbIndex, 95, 0, 100, 1, 10);

    const continuedValues = continued.value as number[];
    expect(continuedValues).toEqual([70, 95]);
    expect(continued.thumbIndex).toBe(1);
    expect(continued.didSwap).toBe(false);
  });

  it('does not swap before reaching neighbour value with minimum steps', () => {
    const result = resolveThumbCollision('swap', [25, 45], [40, 45], [25, 45], 0, 44, 0, 100, 1, 5);

    const resultValues = result.value as number[];
    expect(resultValues).toEqual([40, 45]);
    expect(result.thumbIndex).toBe(0);
    expect(result.didSwap).toBe(false);
  });

  it('swaps once reaching the neighbour value with minimum steps', () => {
    const result = resolveThumbCollision('swap', [25, 45], [40, 45], [25, 45], 0, 45, 0, 100, 1, 5);

    const resultValues = result.value as number[];
    expect(resultValues).toEqual([40, 45]);
    expect(result.thumbIndex).toBe(1);
    expect(result.didSwap).toBe(true);
  });

  it('does not swap backward before reaching neighbour value with minimum steps', () => {
    const result = resolveThumbCollision('swap', [25, 45], [25, 40], [25, 45], 1, 29, 0, 100, 1, 5);

    const resultValues = result.value as number[];
    expect(resultValues).toEqual([25, 30]);
    expect(result.thumbIndex).toBe(1);
    expect(result.didSwap).toBe(false);
  });

  it('swaps backward once reaching the neighbour value with minimum steps', () => {
    const result = resolveThumbCollision('swap', [25, 45], [25, 40], [25, 45], 1, 25, 0, 100, 1, 5);

    const resultValues = result.value as number[];
    expect(resultValues).toEqual([25, 30]);
    expect(result.thumbIndex).toBe(0);
    expect(result.didSwap).toBe(true);
  });

  it('does not move the clamped neighbour when swapping across with minimum steps', () => {
    const startValues = [25, 45];
    const currentValues = [40, 45];

    const result = resolveThumbCollision('swap', currentValues, currentValues, startValues, 0, 46, 0, 100, 1, 5);

    const resultValues = result.value as number[];
    expect(resultValues).toEqual([40, 46]);
    expect(result.thumbIndex).toBe(1);
    expect(result.didSwap).toBe(true);
  });

  // SLD-PTR-19 cites these two at L4 too; the algorithm's half runs here (the module's mid-gesture half at L4).
  it('uses current values when a controlled range grows during a swap interaction', () => {
    const result = resolveThumbCollision('swap', [20, 40], [20, 40, 60], [20, 40], 1, 70, 0, 100, 1, 0);

    expect(result).toEqual({
      value: [20, 60, 70],
      thumbIndex: 2,
      didSwap: true,
    });
  });

  it('returns a scalar when the live values shrink to one item during an interaction', () => {
    const result = resolveThumbCollision('push', [20, 40], [20], [20, 40], 0, 30, 0, 100, 1, 0);

    expect(result).toEqual({
      value: 30,
      thumbIndex: 0,
      didSwap: false,
    });
  });
});

// ---------------------------------------------------------------------------------------------------------------
// The module (port titles): the optimistic move and the revision rule (ADR 04 Amendments 2026-11-18), the bands, the
// gesture's fixed quirks (SLD §16 Q4, Q6; decision 23), the blur classification (ADR 04 Amendments 2026-10-20), and the
// two decision points (ADR 04 §3). jsdom has no layout: each element's rect is stubbed.
// ---------------------------------------------------------------------------------------------------------------

interface Mounted {
  root: HTMLElement;
  control: HTMLElement;
  thumbs: HTMLElement[];
  inputs: HTMLInputElement[];
  fake: FakeTwin;
  handle: TwinHandle;
  captured: number[];
  update(overrides: Partial<SliderOptions>): void;
}

const mountedHandles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of mountedHandles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
});

function rect(left: number, top: number, width: number, height: number): DOMRect {
  return { left, top, width, height, right: left + width, bottom: top + height, x: left, y: top, toJSON: () => ({}) } as DOMRect;
}

function baseOptions(count: number, overrides: Partial<SliderOptions>): SliderOptions {
  return {
    values: [25],
    range: false,
    min: 0,
    max: 100,
    step: 1,
    largeStep: 10,
    minStepsBetweenValues: 0,
    vertical: false,
    rtl: false,
    disabled: false,
    inset: false,
    collision: 'push',
    thumbs: Array.from({ length: count }, (_, i) => ({ inputId: `input-${i}`, disabled: false })),
    controlRef: 0,
    indicators: [],
    revision: 0,
    rollback: false,
    keyHandlers: false,
    pressHandler: false,
    ...overrides,
  };
}

// A 100 px control at the origin with 10 px thumbs centred on their values.
function mount(overrides: Partial<SliderOptions> = {}, twinOptions: { synchronous?: boolean; verdict?: DecisionPolicy } = {}): Mounted {
  const values = overrides.values ?? [25];
  const root = document.createElement('div');
  const control = document.createElement('div');
  control.getBoundingClientRect = () => rect(0, 0, 100, 10);
  const captured: number[] = [];
  (control as unknown as { setPointerCapture(id: number): void }).setPointerCapture = (id: number) => captured.push(id);
  (control as unknown as { hasPointerCapture(id: number): boolean }).hasPointerCapture = () => false;
  root.append(control);
  const thumbs: HTMLElement[] = [];
  const inputs: HTMLInputElement[] = [];
  values.forEach((value, i) => {
    const thumb = document.createElement('div');
    thumb.getBoundingClientRect = () => rect(value - 5, 0, 10, 10);
    const input = document.createElement('input');
    input.type = 'range';
    input.id = `input-${i}`;
    input.value = String(value);
    thumb.append(input);
    control.append(thumb);
    thumbs.push(thumb);
    inputs.push(input);
  });
  document.body.append(root);
  const fake = createFakeTwin(twinOptions);
  let options = baseOptions(values.length, overrides);
  const handle = attach(root, JSON.stringify(options), fake.twin, [control]);
  mountedHandles.push(handle);
  return {
    root,
    control,
    thumbs,
    inputs,
    fake,
    handle,
    captured,
    update(next) {
      options = { ...options, ...next };
      handle.update(JSON.stringify(options), [control]);
    },
  };
}

interface Request {
  reason: string;
  activeThumbIndex: number;
  values: number[];
  revision: number;
}

function pointer(target: EventTarget, type: string, clientX: number, init: PointerEventInit = {}): PointerEvent {
  const event = new PointerEvent(type, { bubbles: true, cancelable: true, clientX, clientY: 5, button: 0, buttons: 1, pointerId: 1, pointerType: 'mouse', ...init });
  target.dispatchEvent(event);
  return event;
}

function key(input: HTMLInputElement, keyName: string, init: KeyboardEventInit = {}): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key: keyName, bubbles: true, cancelable: true, ...init });
  input.dispatchEvent(event);
  return event;
}

describe('slider module', () => {
  it('port: a key moves the thumb at once and reports the candidate with a revision', () => {
    const { thumbs, inputs, fake } = mount();

    const event = key(inputs[0]!, 'ArrowRight');

    expect(event.defaultPrevented).toBe(true);
    expect(thumbs[0]!.style.getPropertyValue('inset-inline-start')).toBe('26%');
    expect(inputs[0]!.value).toBe('26');
    expect(fake.reported<Request>('OnValueRequest')).toMatchObject([{ reason: 'keyboard', activeThumbIndex: 0, values: [26], revision: 1 }]);
  });

  it('port: an update older than the last act keeps the moved thumb', () => {
    const { thumbs, inputs, update } = mount();
    key(inputs[0]!, 'ArrowRight');
    key(inputs[0]!, 'ArrowRight');

    // C#'s answer to the first act arrives after the second.
    update({ values: [26], revision: 1 });

    expect(thumbs[0]!.style.getPropertyValue('inset-inline-start')).toBe('27%');
    expect(inputs[0]!.value).toBe('27');

    update({ values: [27], revision: 2 });
    expect(thumbs[0]!.style.getPropertyValue('inset-inline-start')).toBe('27%');
  });

  it('port: a rollback is applied though it is older than the last act', () => {
    const { thumbs, inputs, update } = mount();
    key(inputs[0]!, 'ArrowRight');
    key(inputs[0]!, 'ArrowRight');

    update({ values: [25], revision: 1, rollback: true });

    expect(thumbs[0]!.style.getPropertyValue('inset-inline-start')).toBe('25%');
    expect(inputs[0]!.value).toBe('25');
  });

  it('port: writes the thumb position in the component-under band, under the consumer style', () => {
    const { thumbs, update } = mount();

    update({ thumbs: [{ inputId: 'input-0', disabled: false, style: 'inset-inline-start: 5%; color: red' }] });

    expect(thumbs[0]!.style.getPropertyValue('inset-inline-start')).toBe('5%');
    expect(thumbs[0]!.style.getPropertyValue('color')).toBe('red');
    expect(thumbs[0]!.style.getPropertyValue('position')).toBe('absolute');
    expect(thumbs[0]!.style.getPropertyValue('translate')).toBe('-50% -50%');
  });

  it('port: a track press applies the value at the press point and reports a gesture', () => {
    const { control, fake } = mount();

    pointer(control, 'pointerdown', 80);

    expect(fake.reported('OnGesture')).toEqual([{ active: true, dragging: true, commit: false }]);
    expect(fake.reported<Request>('OnValueRequest')).toMatchObject([{ reason: 'track-press', values: [80] }]);
  });

  it('port: does not report a move that leaves the candidate unchanged', () => {
    const { control, fake } = mount();
    pointer(control, 'pointerdown', 80);

    pointer(document.body, 'pointermove', 80);
    pointer(document.body, 'pointermove', 90);

    expect(fake.reported<Request>('OnValueRequest').map((r) => r.values[0])).toEqual([80, 90]);
  });

  // SLD §16 Q4, fixed (decision 23).
  it('port: captures the pointer whose id is 0', () => {
    const { control, captured } = mount();

    pointer(control, 'pointerdown', 80, { pointerId: 0 });

    expect(captured).toEqual([0]);
  });

  // SLD-FOC-05, "post-commit, pre-paint" (issue 328): the render that disables the slider writes the root's
  // `data-disabled`, and the focused thumb is blurred in that render's task, before the module's update arrives.
  it('port: a render that marks the root disabled blurs the focused thumb before the update', async () => {
    const { root, inputs } = mount();
    inputs[0]!.focus();
    expect(document.activeElement).toBe(inputs[0]);
    const blur = vi.spyOn(inputs[0]!, 'blur');

    root.setAttribute('data-disabled', '');
    inputs[0]!.disabled = true;
    await new Promise<void>((resolve) => setTimeout(resolve, 0));

    // jsdom ignores blur() on a disabled input; the focus leaving is the L4 title's (slider.field.spec.ts).
    expect(blur).toHaveBeenCalledTimes(1);
  });

  // SLD §16 Q6, fixed (decision 23).
  it('port: a gesture dropped by disabling clears the pressed thumb and its grab offset', () => {
    const { control, thumbs, fake, update } = mount();
    pointer(thumbs[0]!, 'pointerdown', 27);
    expect(fake.reported<Request>('OnValueRequest')).toEqual([]);

    update({ disabled: true });
    update({ disabled: false });
    pointer(control, 'pointerdown', 80);

    expect(fake.reported<Request>('OnValueRequest')).toMatchObject([{ reason: 'track-press', values: [80] }]);
    expect(fake.reported('OnGesture')).toContainEqual({ active: false, dragging: false, commit: false });
  });

  it('port: a press on a thumb stacked at the maximum drags the lowest thumb of the stack', () => {
    const { thumbs, fake } = mount({ values: [100, 100], range: true });

    pointer(thumbs[1]!, 'pointerdown', 100);
    pointer(document.body, 'pointermove', 60);

    expect(fake.reported<Request>('OnValueRequest')).toMatchObject([{ reason: 'drag', activeThumbIndex: 0, values: [60, 100] }]);
  });

  it('port: a release reports the end of the gesture for C# to commit', () => {
    const { control, fake } = mount();
    pointer(control, 'pointerdown', 80);

    pointer(document, 'pointerup', 80);

    expect(fake.reported('OnGesture').at(-1)).toMatchObject({ active: false, dragging: false, commit: true });
  });

  it('port: a pointer move with no button down ends the gesture', () => {
    const { control, fake } = mount();
    pointer(control, 'pointerdown', 80);

    pointer(document.body, 'pointermove', 90, { buttons: 0 });

    expect(fake.reported<Request>('OnValueRequest').map((r) => r.values[0])).toEqual([80]);
    expect(fake.reported('OnGesture').at(-1)).toMatchObject({ commit: true });
  });

  it('port: a JavaScript listener that prevents the pointerdown keeps the gesture from starting', () => {
    const { thumbs, fake } = mount();
    thumbs[0]!.addEventListener('pointerdown', (event) => event.preventDefault());

    pointer(thumbs[0]!, 'pointerdown', 25);

    expect(fake.reported('OnGesture')).toEqual([]);
  });

  it('port: the press decision answering stop keeps the gesture from starting', () => {
    const { control, fake } = mount({ pressHandler: true }, { synchronous: true, verdict: 'stop' });

    pointer(control, 'pointerdown', 80);

    expect(fake.decisions.map((d) => d.point)).toEqual([PRESS_POINT]);
    expect(fake.reported('OnGesture')).toEqual([]);
    expect(fake.reported('OnValueRequest')).toEqual([]);
  });

  it('port: the key decision answering stop leaves the key to the browser', () => {
    const { inputs, fake } = mount({ keyHandlers: true }, { synchronous: true, verdict: 'stop' });

    const event = key(inputs[0]!, 'ArrowRight');

    expect(fake.decisions.map((d) => d.point)).toEqual([KEY_POINT]);
    expect(event.defaultPrevented).toBe(false);
    expect(fake.reported('OnValueRequest')).toEqual([]);
  });

  it('port: reports each key for the consumer handler where no decision ran inside the dispatch', () => {
    const { inputs, fake } = mount({ keyHandlers: true });

    key(inputs[0]!, 'Enter');
    key(inputs[0]!, 'ArrowRight');

    expect(fake.reported<{ index: number }>('OnKeyDown').map((r) => r.index)).toEqual([0, 0]);
    expect(fake.reported('OnValueRequest')).toHaveLength(1);
  });

  // SLD-KEY-08: the arrow keys, Home and End stop propagating; Page Up and Page Down do not.
  it('port: stops the propagation of the arrow, Home and End keys only', () => {
    const { root, inputs } = mount();
    const reached: string[] = [];
    document.body.addEventListener('keydown', (event) => reached.push(event.key));

    key(inputs[0]!, 'ArrowRight');
    key(inputs[0]!, 'Home');
    key(inputs[0]!, 'PageUp');
    key(inputs[0]!, 'Tab');

    expect(reached).toEqual(['PageUp', 'Tab']);
    expect(root).toBeTruthy();
  });

  it('port: classifies a thumb blur by its related target', () => {
    const { inputs, fake } = mount({ values: [20, 50], range: true });

    inputs[0]!.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: inputs[1]! }));
    inputs[1]!.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.body }));

    expect(fake.reported<{ index: number; outside: boolean }>('OnFocusChange').map((r) => [r.index, r.outside])).toEqual([
      [0, false],
      [1, true],
    ]);
  });

  it('port: Home and End stop at the neighbours of a range thumb', () => {
    const { inputs, fake } = mount({ values: [20, 50], range: true, minStepsBetweenValues: 5 });

    key(inputs[0]!, 'End');
    key(inputs[1]!, 'Home');

    expect(fake.reported<Request>('OnValueRequest').map((r) => r.values)).toEqual([
      [45, 50],
      [45, 50],
    ]);
  });

  // SLD-EVT-04 (design point 6 (a), settled by the orchestrator 2026-11-18): the request's snapshot target is `{ value, name }`.
  it('port: should pass "name" and "value" as part of the event.target for onValueChange', () => {
    const { inputs, fake } = mount({ values: [20, 50], range: true });
    inputs.forEach((input) => (input.name = 'volume'));

    key(inputs[1]!, 'ArrowRight');

    const [request] = fake.reported<{ event: { target: unknown } }>('OnValueRequest');
    expect(request!.event.target).toEqual({ id: null, tagName: null, value: '[20,51]', name: 'volume' });
  });

  it('port: a native change of the input is reported as input-change', () => {
    const { inputs, fake } = mount();

    inputs[0]!.value = '60';
    inputs[0]!.dispatchEvent(new Event('input', { bubbles: true }));

    expect(fake.reported<Request>('OnValueRequest')).toMatchObject([{ reason: 'input-change', values: [60] }]);
  });
});
