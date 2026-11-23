// The timestamped pointer fire (M6 track 1; the M6 plan's decision 22 (a), chosen by the user 2026-11-18), Base UI's
// packages/react/test/pointer.ts translated for listeners: velocity-dependent module tests describe a fixed timeline
// instead of the runner's clock (shared-utilities.md §8.3: `firePointer` requires `timeStamp > 0`; `pressWithTouch`).
// Test-only: no module entry imports it, so esbuild never bundles it.

/**
 * A pointer event's init plus the `timeStamp` it must carry. `timeStamp` is required: omitting it would leave the event
 * stamped off the runner's clock, the dependency this helper removes.
 */
export type PointerInit = PointerEventInit & { timeStamp: number };

function firePointerEvent(
  type: 'pointerdown' | 'pointermove' | 'pointerup',
  element: Element,
  init: PointerInit,
): boolean {
  const { timeStamp, ...eventInit } = init;

  if (!(timeStamp > 0)) {
    // A falsy stamp would read as the real clock, and the swipe tracker rejects anything <= 0, so a zero stamp can never
    // express "time zero" (Base UI's pointer.ts:34-39, its message kept).
    throw new Error(`firePointer: timeStamp must be greater than 0, received ${timeStamp}.`);
  }

  const event = new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, ...eventInit });

  // `timeStamp` is read-only and not part of PointerEventInit, so it is set on the event itself (pointer.ts:44-48).
  Object.defineProperty(event, 'timeStamp', { value: timeStamp });

  return element.dispatchEvent(event);
}

/**
 * Fires pointer events that carry the `timeStamp` given to them, so that tests asserting on gesture velocity describe a
 * fixed timeline. Each returns `dispatchEvent`'s result: `false` when a listener prevented the default.
 */
export const firePointer = {
  down: (element: Element, init: PointerInit) => firePointerEvent('pointerdown', element, init),
  move: (element: Element, init: PointerInit) => firePointerEvent('pointermove', element, init),
  up: (element: Element, init: PointerInit) => firePointerEvent('pointerup', element, init),
};

/**
 * Presses an element the way a touch screen does before the click: a touch `pointerdown` followed by the compatibility
 * `mousedown`.
 * @param element The element to press.
 */
export function pressWithTouch(element: Element): void {
  const init = { bubbles: true, cancelable: true, composed: true };
  element.dispatchEvent(new PointerEvent('pointerdown', { ...init, pointerType: 'touch' }));
  element.dispatchEvent(new MouseEvent('mousedown', init));
}
