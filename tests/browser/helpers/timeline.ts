import { type Locator } from '@playwright/test';
import { type TouchPoint } from './touch';

// M6 track 1 (the M6 plan's decision 22 (a), chosen by the user 2026-11-18; ADR 06 Amendments 2026-11-18): velocity-
// dependent titles dispatch in-page PointerEvents on a fixed timeline, because the page owns the event. Base UI's
// `swipe` helper translated (drawer/swipe-area/DrawerSwipeArea.test.tsx:99-130: start at 1 ms, step 16 ms, a one-pixel
// first step toward the end) with the event stamped as firePointer stamps it (packages/react/test/pointer.ts:44-48).

export interface TimelineOptions {
  /** The pointer type; `mouse` by default. */
  pointerType?: 'mouse' | 'pen' | 'touch';
  /** The time between two events, in milliseconds; 16 by default. */
  timeStepMs?: number;
  /** The first event's `timeStamp`, greater than 0; 1 by default. */
  startTimeMs?: number;
  /** The pointer id; 1 by default. */
  pointerId?: number;
  /** Runs after the last move and before the `pointerup`; it may dispatch more events on the same timeline. */
  beforeRelease?: (timeline: Timeline) => Promise<void>;
}

/** The timeline a `beforeRelease` hook continues. */
export interface Timeline {
  /** Advances the timeline by one step and returns the new `timeStamp`. */
  nextTimeStamp(): number;
  /** Dispatches one pointer event at a point, stamped with the next step. */
  dispatch(type: 'pointermove' | 'pointerup' | 'pointercancel', point: TouchPoint): Promise<TimelineEvent>;
}

/** One dispatched event, as the page saw it. */
export interface TimelineEvent {
  type: string;
  timeStamp: number;
  /** The tag of the element the event was dispatched on. */
  target: string;
  defaultPrevented: boolean;
}

interface DispatchInit {
  type: string;
  x: number;
  y: number;
  timeStamp: number;
  pointerId: number;
  pointerType: string;
}

/**
 * Swipes from `start` to `end` with in-page `PointerEvent`s on a fixed timeline: a `pointerdown` at `start`, a
 * `pointermove` one pixel toward `end` on each moving axis, a `pointermove` at `end`, the optional hook, then a
 * `pointerup` at `end`, each `timeStep` after the last. Each event is constructed bubbling, cancelable and composed,
 * with `pointerId`, `pointerType`, `isPrimary`, `button`/`buttons` (0/1 on the down, -1/1 on a move, 0/0 on the up) and
 * `clientX`/`clientY`, stamped with `Object.defineProperty(event, 'timeStamp', { value })`, which wins over
 * `page.clock`'s patched getter, and dispatched on the element under the point (the swiped element when none is).
 *
 * Limits: the pointer is synthetic, so the browser has no active pointer for it and `setPointerCapture` throws
 * `NotFoundError` (the swipe tracker swallows it, PSH-EDGE-10); no default action runs (no `click`, no compatibility
 * mouse events, no scroll, no focus); the page owns the event, which is why velocity titles use it and every other
 * gesture uses real `page.mouse` and CDP touch (`touchDrag`).
 * @param target The swiped element; its document is the page's.
 * @param start The first point, in viewport coordinates.
 * @param end The last point.
 * @param options The pointer type, the timeline and a hook before the release.
 * @returns Every dispatched event, in order.
 */
export async function swipeOnTimeline(
  target: Locator,
  start: TouchPoint,
  end: TouchPoint,
  options: TimelineOptions = {},
): Promise<TimelineEvent[]> {
  const { pointerType = 'mouse', timeStepMs = 16, startTimeMs = 1, pointerId = 1, beforeRelease } = options;
  if (!(startTimeMs > 0)) {
    throw new Error(`swipeOnTimeline: startTimeMs must be greater than 0, received ${startTimeMs}.`);
  }

  const handle = await target.elementHandle();
  if (!handle) {
    throw new Error('swipeOnTimeline: the target is not attached.');
  }

  const events: TimelineEvent[] = [];
  let timeStamp = startTimeMs;
  const fire = async (type: string, point: TouchPoint, stamp: number): Promise<TimelineEvent> => {
    const init: DispatchInit = { type, x: point.x, y: point.y, timeStamp: stamp, pointerId, pointerType };
    const event = await handle.evaluate((element, init: DispatchInit) => {
      const down = init.type === 'pointerdown';
      const moving = init.type === 'pointermove';
      const event = new PointerEvent(init.type, {
        bubbles: true,
        cancelable: true,
        composed: true,
        pointerId: init.pointerId,
        pointerType: init.pointerType,
        isPrimary: true,
        button: moving ? -1 : 0,
        buttons: down || moving ? 1 : 0,
        clientX: init.x,
        clientY: init.y,
      });
      Object.defineProperty(event, 'timeStamp', { value: init.timeStamp });
      const at = element.ownerDocument.elementFromPoint(init.x, init.y) ?? element;
      at.dispatchEvent(event);
      const target = at.tagName.toLowerCase();
      return { type: event.type, timeStamp: event.timeStamp, target, defaultPrevented: event.defaultPrevented };
    }, init);
    events.push(event);
    return event;
  };
  const timeline: Timeline = {
    nextTimeStamp: () => (timeStamp += timeStepMs),
    dispatch: (type, point) => fire(type, point, timeline.nextTimeStamp()),
  };

  try {
    const first = { x: start.x + Math.sign(end.x - start.x), y: start.y + Math.sign(end.y - start.y) };
    await fire('pointerdown', start, timeStamp);
    await timeline.dispatch('pointermove', first);
    await timeline.dispatch('pointermove', end);
    await beforeRelease?.(timeline);
    await timeline.dispatch('pointerup', end);
  } finally {
    await handle.dispose();
  }
  return events;
}
