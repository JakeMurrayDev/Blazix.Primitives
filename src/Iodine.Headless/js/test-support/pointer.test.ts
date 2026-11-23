// L3 (ADR 06 §1): the timestamped pointer fire every velocity-dependent module test uses, translated from Base UI's
// packages/react/test/pointer.test.tsx. The second and third titles are Base UI's, verbatim. The first is restated: Base
// UI asserts through a React handler ("reports the requested timeStamp to React handlers"), and the port has no React
// handlers (ADR 13 §3), so it asserts through a native listener, which is what a module reads.
import { afterEach, describe, expect, it } from 'vitest';
import { firePointer, pressWithTouch } from './pointer';

describe('firePointer', () => {
  let target: HTMLElement;

  function render(onEvent: (event: PointerEvent) => void): HTMLElement {
    target = document.createElement('div');
    for (const type of ['pointerdown', 'pointermove', 'pointerup']) {
      target.addEventListener(type, (event) => onEvent(event as PointerEvent));
    }
    document.body.appendChild(target);
    return target;
  }

  afterEach(() => {
    target?.remove();
  });

  it('reports the requested timeStamp to listeners', () => {
    const seen: number[] = [];
    const element = render((event) => seen.push(event.timeStamp));

    firePointer.down(element, { pointerId: 1, timeStamp: 1000 });
    firePointer.move(element, { pointerId: 1, timeStamp: 1050 });
    firePointer.up(element, { pointerId: 1, timeStamp: 1100 });

    expect(seen).toEqual([1000, 1050, 1100]);
  });

  it('delivers the rest of the init alongside the timeStamp', () => {
    const seen: Array<Record<string, unknown>> = [];
    const element = render((event) =>
      seen.push({
        type: event.type,
        bubbles: event.bubbles,
        pointerId: event.pointerId,
        pointerType: event.pointerType,
        clientX: event.clientX,
        clientY: event.clientY,
        buttons: event.buttons,
        timeStamp: event.timeStamp,
      }),
    );

    firePointer.move(element, {
      pointerId: 1,
      pointerType: 'mouse',
      clientX: 120,
      clientY: 40,
      buttons: 1,
      timeStamp: 1050,
    });

    expect(seen).toEqual([
      {
        type: 'pointermove',
        bubbles: true,
        pointerId: 1,
        pointerType: 'mouse',
        clientX: 120,
        clientY: 40,
        buttons: 1,
        timeStamp: 1050,
      },
    ]);
  });

  it('rejects a non-positive timeStamp instead of falling back to the real clock', () => {
    const seen: number[] = [];
    const element = render((event) => seen.push(event.timeStamp));

    expect(() => firePointer.down(element, { pointerId: 1, timeStamp: 0 })).toThrow(/timeStamp must be greater than 0/);
    expect(() => firePointer.down(element, { pointerId: 1, timeStamp: Number.NaN })).toThrow(
      /timeStamp must be greater than 0/,
    );
    expect(seen).toEqual([]);
  });
});

describe('pressWithTouch', () => {
  it('port: presses with a touch pointerdown, then the compatibility mousedown', () => {
    const element = document.createElement('button');
    const seen: string[] = [];
    element.addEventListener('pointerdown', (event) => seen.push(`pointerdown:${event.pointerType}`));
    element.addEventListener('mousedown', () => seen.push('mousedown'));

    pressWithTouch(element);

    expect(seen).toEqual(['pointerdown:touch', 'mousedown']);
  });
});
