import { type Locator } from '@playwright/test';
import { pointOf, type TouchPoint } from './touch';

// M6 track 1 (the M6 plan's decisions 3 (a) and 22 (a), chosen by the user 2026-11-18): real wheel input for
// NumberField (NUM-PTR-07, a native listener with `{ passive: false }`) and ScrollArea.

export type WheelModifier = 'Alt' | 'Control' | 'Meta' | 'Shift';

export interface WheelOptions {
  /** Where the pointer rests, as an offset from the element's top-left corner; its centre when omitted. */
  offset?: TouchPoint;
  /** The keys held down while the wheel turns, pressed in order and released in reverse. */
  modifiers?: readonly WheelModifier[];
}

/**
 * Moves the mouse over an element and turns the wheel with real input (`page.mouse.wheel`), so the browser targets the
 * element under the pointer, scrolls what it scrolls and honours a listener's `preventDefault`. Playwright resolves
 * once the browser has dispatched the `wheel` event; a scroll it causes may land later.
 * @param target The element to turn the wheel over.
 * @param deltaX The horizontal delta, in pixels.
 * @param deltaY The vertical delta, in pixels; positive scrolls down.
 * @param options The pointer's offset and the keys to hold.
 */
export async function wheelOver(
  target: Locator,
  deltaX: number,
  deltaY: number,
  options: WheelOptions = {},
): Promise<void> {
  const page = target.page();
  const { x, y } = await pointOf(target, options.offset);
  const modifiers = options.modifiers ?? [];
  await page.mouse.move(x, y);
  for (const key of modifiers) {
    await page.keyboard.down(key);
  }
  try {
    await page.mouse.wheel(deltaX, deltaY);
  } finally {
    for (const key of [...modifiers].reverse()) {
      await page.keyboard.up(key);
    }
  }
}
