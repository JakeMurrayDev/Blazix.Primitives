// The event snapshot a module sends to C# (ADR 04 §3). The property names mirror the C# record
// `Blazix.Primitives.Interop.JsEventInfo` (camel-cased), which is the one place that defines them.

export type EventKind = 'keyboard' | 'pointer' | 'mouse' | 'touch' | 'focus' | 'generic' | 'base-ui';

export interface EventTargetInfo {
  tagName: string;
  id: string | null;
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
  button: number;
  target: EventTargetInfo | null;
  defaultPrevented: boolean;
  timeStamp: number;
}

/** The placeholder for a request that no browser event caused (EVD-API-05's `base-ui` form). */
export const baseUiEvent: EventInfo = Object.freeze({
  kind: 'base-ui',
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
  button: -1,
  target: null,
  defaultPrevented: false,
  timeStamp: 0,
});

function kindOf(event: Event): EventKind {
  if (event instanceof KeyboardEvent) {
    return 'keyboard';
  }
  if (typeof PointerEvent !== 'undefined' && event instanceof PointerEvent) {
    return 'pointer';
  }
  if (event instanceof MouseEvent) {
    return 'mouse';
  }
  if (typeof TouchEvent !== 'undefined' && event instanceof TouchEvent) {
    return 'touch';
  }
  if (event instanceof FocusEvent) {
    return 'focus';
  }
  return 'generic';
}

function targetOf(event: Event): EventTargetInfo | null {
  const target = event.target;
  if (target instanceof Element) {
    return { tagName: target.tagName.toLowerCase(), id: target.id || null };
  }
  return null;
}

/** Snapshots a browser event; `undefined` yields the `base-ui` placeholder. */
export function eventInfo(event: Event | undefined): EventInfo {
  if (event === undefined) {
    return baseUiEvent;
  }
  const kind = kindOf(event);
  const keyboard = kind === 'keyboard' ? (event as KeyboardEvent) : null;
  const mouse = kind === 'pointer' || kind === 'mouse' ? (event as MouseEvent) : null;
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
    pointerType: kind === 'pointer' ? (event as PointerEvent).pointerType : null,
    detail: event instanceof UIEvent ? event.detail : 0,
    buttons: mouse?.buttons ?? 0,
    button: mouse?.button ?? -1,
    target: targetOf(event),
    defaultPrevented: event.defaultPrevented,
    timeStamp: event.timeStamp,
  };
}
