// The event snapshot a module sends to C#: `details.event`. The property names and the `kind` values
// mirror `Events/EventSnapshot.cs` and `Events/EventKind.cs` (Track 2), which are the one place that defines them.

export type EventKind =
  | 'Event'
  | 'UIEvent'
  | 'FocusEvent'
  | 'MouseEvent'
  | 'PointerEvent'
  | 'WheelEvent'
  | 'TouchEvent'
  | 'KeyboardEvent'
  | 'InputEvent'
  | 'ClipboardEvent';

export interface EventTargetInfo {
  id: string | null;
  tagName: string | null;
}

export interface EventInfo {
  kind: EventKind;
  type: string;
  key: string | null;
  code: string | null;
  altKey: boolean;
  ctrlKey: boolean;
  metaKey: boolean;
  shiftKey: boolean;
  pointerType: string | null;
  detail: number;
  buttons: number;
  target: EventTargetInfo | null;
  defaultPrevented: boolean;
  timeStamp: number;
}

/**
 * Creates the placeholder snapshot for a request that no browser event caused: `kind` `Event`, `type` `base-ui`.
 * @returns A fresh placeholder.
 */
export function placeholderEvent(): EventInfo {
  return {
    kind: 'Event',
    type: 'base-ui',
    key: null,
    code: null,
    altKey: false,
    ctrlKey: false,
    metaKey: false,
    shiftKey: false,
    pointerType: null,
    detail: 0,
    buttons: 0,
    target: null,
    defaultPrevented: false,
    timeStamp: 0,
  };
}

/** The most derived DOM interface of the event, checked most-derived first. */
function kindOf(event: Event): EventKind {
  if (event instanceof KeyboardEvent) {
    return 'KeyboardEvent';
  }
  if (typeof PointerEvent !== 'undefined' && event instanceof PointerEvent) {
    return 'PointerEvent';
  }
  if (event instanceof WheelEvent) {
    return 'WheelEvent';
  }
  if (event instanceof MouseEvent) {
    return 'MouseEvent';
  }
  if (typeof TouchEvent !== 'undefined' && event instanceof TouchEvent) {
    return 'TouchEvent';
  }
  if (event instanceof FocusEvent) {
    return 'FocusEvent';
  }
  if (typeof InputEvent !== 'undefined' && event instanceof InputEvent) {
    return 'InputEvent';
  }
  if (typeof ClipboardEvent !== 'undefined' && event instanceof ClipboardEvent) {
    return 'ClipboardEvent';
  }
  if (event instanceof UIEvent) {
    return 'UIEvent';
  }
  return 'Event';
}

function targetOf(event: Event): EventTargetInfo | null {
  const target = event.target;
  if (target instanceof Element) {
    return { id: target.id || null, tagName: target.tagName };
  }
  return null;
}

/**
 * Snapshots a browser event inside its dispatch.
 * @param event The event, or `undefined` for a request with no browser event.
 * @returns The snapshot, or the placeholder.
 */
export function eventInfo(event: Event | undefined): EventInfo {
  if (event === undefined) {
    return placeholderEvent();
  }
  const kind = kindOf(event);
  const keyboard = kind === 'KeyboardEvent' ? (event as KeyboardEvent) : null;
  const mouse = event instanceof MouseEvent ? event : null;
  const modifiers = keyboard ?? mouse;
  return {
    kind,
    type: event.type,
    key: keyboard?.key ?? null,
    code: keyboard?.code ?? null,
    altKey: modifiers?.altKey ?? false,
    ctrlKey: modifiers?.ctrlKey ?? false,
    metaKey: modifiers?.metaKey ?? false,
    shiftKey: modifiers?.shiftKey ?? false,
    pointerType: kind === 'PointerEvent' ? (event as PointerEvent).pointerType : null,
    detail: event instanceof UIEvent ? event.detail : 0,
    buttons: mouse?.buttons ?? 0,
    target: targetOf(event),
    defaultPrevented: event.defaultPrevented,
    timeStamp: event.timeStamp,
  };
}
