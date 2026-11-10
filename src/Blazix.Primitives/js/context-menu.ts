// The `context-menu` component module (ADR 04 §1's component row; the `context-menu` row of Amendments 2026-10-18 and
// 2026-11-03 (4), chosen by the user 2026-11-03; contract docs/components/context-menu.md). Two roles, each a handle of
// one `ContextMenuTwin`, joined through the menu's interactions session token (design point 1 (a) of M5 track 7):
// - The Root (no element, the token form): Base UI's ContextMenuRoot state that is not React's (ContextMenuRoot.tsx:17-27).
//   At attach the tree's release starts armed with the context menu's rules and the anchor is the origin rect (CTX-STA-04,
//   STA-06, EDGE-06); `update` brings the consumer Backdrop, the internal backdrop and the root Positioner as references
//   (design point 2 (a)).
// - Each Trigger (ContextMenuTrigger.tsx:44-192): the right-click open with its one-shot document `mouseup` and 500 ms
//   window (CTX-PTR-01, 04, 05), the long press (PTR-06..08), and the document suppressor of the native menu (PTR-03).
// The shared state lives in the session's `dataRef` (the Base UI context's place), so the module keeps no page-global
// state; both roles take a reference on the session at attach (`acquireSession`), so the order in which they and the
// session's own handle attach does not matter. The anchor, the release slot and the release rules are `interactions`'
// (`setSessionPositionReference`, `armMenuRelease`); the root-owner walk is `dom`'s `closest`, which crosses shadow hosts.
// The consumer's `oncontextmenu` and `ontouch*` handlers are declined by C# and run through this module at each dispatch
// (design point 3 (a), settled by the orchestrator 2026-11-11): `press` on WebAssembly, after the synchronous `OnPressing`
// handover of the coordinates and touches, whose verdict skips the part's own handling as `preventBaseUIHandler()` does,
// at each `contextmenu` and `touchstart` and at a touch move, end or cancel while a long press is pending; otherwise, and
// always on Server, a report, where C# applies the veto to the open request it precedes. Base UI's handler stops the propagation of `contextmenu` and of a
// single-touch `touchstart`, so a handler left to Blazor's delegated dispatch would never run.
import { closest, contains, getTarget, isElement, ownerDocument } from './dom';
import { decide } from './decide';
import { eventInfo, type EventInfo } from './event-info';
import { acquireSession, armMenuRelease, releaseSession, setSessionPositionReference, type InteractionSession, type MenuReleaseRules } from './interactions';
import { platform } from './platform';
import {
  attachTarget,
  createTwinChannel,
  parseOptions,
  resolveReference,
  type DotNetTwin,
  type TwinCaptures,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

// ---------------------------------------------------------------------------------------------------------------
// Constants and types
// ---------------------------------------------------------------------------------------------------------------

/** The part name of the Root's handle. */
export const ROOT_PART = 'root';
/** The part name of a Trigger's handle. */
export const TRIGGER_PART = 'trigger';
// ADR 04 Amendments 2026-11-03 (3): `press` on the `context-menu` twin, fallback `run`.
/** The decision point asked before the part handles an event the consumer supplied a handler for. */
export const PRESS_POINT = 'press';
/** The synchronous handover before `press` on WebAssembly. */
export const PRESSING_HANDOVER = 'OnPressing';
/** The report of a consumer handler's event on Server. */
export const PRESS_REPORT = 'OnPress';
/** The report of an open request. */
export const OPEN_REPORT = 'OnOpen';
/** The report of a `cancel-open` close request. */
export const CANCEL_OPEN_REPORT = 'OnCancelOpen';
// ContextMenuTrigger.tsx:16.
/** The long press's delay and the release window, in milliseconds. */
export const LONG_PRESS_DELAY = 500;

// ContextMenuTrigger.tsx:144.
const MOVE_THRESHOLD = 10;
const ROOT_KEY = 'blazix.contextMenu';
const ROOT_OWNER_SELECTOR = '[data-rootownerid]';
const attachedHandle = Symbol('blazix.contextMenu.trigger');

/** The options of a handle: the Root's or a Trigger's. */
export interface ContextMenuOptions extends TwinOptions {
  /** `root` or `trigger`. */
  part?: string | null;
  /** The token of the menu's interactions session. */
  token?: string | null;
  /** A Trigger's: the tree's root id, which every popup of the tree renders as `data-rootownerid`. */
  rootId?: string | null;
  /** A Trigger's: whether the Root is disabled. */
  disabled?: boolean | null;
  /** A Trigger's: the events the consumer supplied a handler for, of `contextmenu` and the four touch events. */
  handlers?: readonly string[] | null;
  /** The Root's: the reference index of the consumer Backdrop's element. */
  backdrop?: number | null;
  /** The Root's: the reference index of the internal backdrop's element. */
  internalBackdrop?: number | null;
  /** The Root's: the reference index of the root Positioner's element. */
  positioner?: number | null;
}

/** One touch of a `ContextMenuPress`. */
export interface ContextMenuTouch {
  identifier: number;
  clientX: number;
  clientY: number;
  screenX: number;
  screenY: number;
  pageX: number;
  pageY: number;
}

/** The event of a consumer handler, as C# rebuilds its arguments from it: the `OnPressing` and `OnPress` payloads. */
export interface ContextMenuPress {
  clientX: number;
  clientY: number;
  screenX: number;
  screenY: number;
  pageX: number;
  pageY: number;
  offsetX: number;
  offsetY: number;
  button: number;
  buttons: number;
  touches: ContextMenuTouch[];
  targetTouches: ContextMenuTouch[];
  changedTouches: ContextMenuTouch[];
  event: EventInfo;
}

/** The `OnOpen` payload: an open request at a point. */
export interface ContextMenuOpenReport {
  x: number;
  y: number;
  /** `mouse` for a right click, `touch` for a long press. */
  pointerType: 'mouse' | 'touch';
  /** The `contextmenu` or `touchstart` event. */
  event: EventInfo;
}

/** The `OnCancelOpen` payload: a release outside the tree after the window. */
export interface ContextMenuCancelOpenReport {
  event: EventInfo;
}

// The Base UI context's fields that are not React state (ContextMenuRootContext.ts:5-17), kept in the session.
interface RootRecord {
  backdrop: Element | null;
  internalBackdrop: Element | null;
  positioner: Element | null;
  /** `allowMouseUpTriggerRef`'s value as this module last wrote it: `armMenuRelease` takes it whole. */
  armed: boolean;
  /** Whether a gesture set the anchor and the rules, which the Root's attach then keeps. */
  gestured: boolean;
}

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

// ---------------------------------------------------------------------------------------------------------------
// Shared state
// ---------------------------------------------------------------------------------------------------------------

function rootRecord(session: InteractionSession): RootRecord {
  const data = session.dataRef.current;
  let record = data[ROOT_KEY] as RootRecord | undefined;
  if (record === undefined) {
    record = { backdrop: null, internalBackdrop: null, positioner: null, armed: true, gestured: false };
    data[ROOT_KEY] = record;
  }
  return record;
}

// ContextMenuRoot.tsx:28; useMenuItemCommonProps.ts:107-140 (the slop, the non-macOS skip, the right button), as the
// rules `interactions`' item release applies (design point 2 (a) of M5 track 6). The macOS read stays here, at dispatch.
function releaseRules(initialPoint: { x: number; y: number } | null): MenuReleaseRules {
  return { button: 2, skipButton: platform.os.mac ? null : 2, slop: 1, initialPoint };
}

function touchList(list: TouchList | undefined): ContextMenuTouch[] {
  if (!list) {
    return [];
  }
  return Array.from(list, (touch) => ({
    identifier: touch.identifier,
    clientX: touch.clientX,
    clientY: touch.clientY,
    screenX: touch.screenX ?? 0,
    screenY: touch.screenY ?? 0,
    pageX: touch.pageX ?? 0,
    pageY: touch.pageY ?? 0,
  }));
}

function pressOf(event: MouseEvent | TouchEvent): ContextMenuPress {
  const mouse = event as Partial<MouseEvent>;
  const touches = event as Partial<TouchEvent>;
  return {
    clientX: mouse.clientX ?? 0,
    clientY: mouse.clientY ?? 0,
    screenX: mouse.screenX ?? 0,
    screenY: mouse.screenY ?? 0,
    pageX: mouse.pageX ?? 0,
    pageY: mouse.pageY ?? 0,
    offsetX: mouse.offsetX ?? 0,
    offsetY: mouse.offsetY ?? 0,
    button: mouse.button ?? 0,
    buttons: mouse.buttons ?? 0,
    touches: touchList(touches.touches),
    targetTouches: touchList(touches.targetTouches),
    changedTouches: touchList(touches.changedTouches),
    event: eventInfo(event),
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The Root
// ---------------------------------------------------------------------------------------------------------------

function attachRoot(options: ContextMenuOptions, references: TwinReferences, captures: TwinCaptures): TwinHandle {
  const token = options.token ?? '';
  const session = acquireSession(token);
  const record = rootRecord(session);
  const read = (next: ContextMenuOptions, nextReferences: TwinReferences, nextCaptures: TwinCaptures) => {
    record.backdrop = resolveReference<Element>(nextReferences, next.backdrop, nextCaptures);
    record.internalBackdrop = resolveReference<Element>(nextReferences, next.internalBackdrop, nextCaptures);
    record.positioner = resolveReference<Element>(nextReferences, next.positioner, nextCaptures);
  };
  read(options, references, captures);
  // ContextMenuRoot.tsx:17-28: the anchor's origin rect and `allowMouseUpTriggerRef`'s `true`, unless a Trigger's gesture
  // came first.
  if (!record.gestured) {
    armMenuRelease(token, record.armed, releaseRules(null));
    setSessionPositionReference(token, { x: 0, y: 0, width: 0, height: 0 });
  }
  let disposed = false;
  return {
    update(optionsJson, nextReferences, nextCaptures) {
      if (!disposed) {
        read(parseOptions<ContextMenuOptions>(optionsJson), nextReferences, nextCaptures);
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      record.backdrop = null;
      record.internalBackdrop = null;
      record.positioner = null;
      releaseSession(session);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The Trigger
// ---------------------------------------------------------------------------------------------------------------

function attachTrigger(element: HTMLElement, initial: ContextMenuOptions, channel: TwinChannel, twin: DotNetTwin): TwinHandle {
  let options = initial;
  const session = acquireSession(options.token ?? '');
  const record = rootRecord(session);
  const doc = ownerDocument(element);
  let longPressTimer: ReturnType<typeof setTimeout> | undefined;
  let allowMouseUpTimer: ReturnType<typeof setTimeout> | undefined;
  let allowMouseUp = false;
  let touchPosition: { x: number; y: number } | null = null;
  let removeMouseUp: (() => void) | null = null;

  const token = () => session.token;
  const disabled = () => options.disabled === true;

  const arm = (armed: boolean, rules?: MenuReleaseRules) => {
    record.armed = armed;
    armMenuRelease(token(), armed, rules);
  };

  // Runs the consumer's handler for the event, when it supplied one, and returns whether the part's own handling runs:
  // `press` on WebAssembly (its verdict), a report on Server (the veto is C#'s, on the open the report precedes). Design
  // point 3 (a), settled by the orchestrator 2026-11-11: a touch move, end or cancel is decided only while a long press
  // is pending (`decided`), as there is nothing else to veto; otherwise its handler runs from a report on both modes.
  const consumerAllows = (event: MouseEvent | TouchEvent, decided = true): boolean => {
    if (options.handlers?.includes(event.type) !== true) {
      return true;
    }
    const press = pressOf(event);
    if (channel.synchronous && decided) {
      twin.invokeMethod<void>(PRESSING_HANDOVER, JSON.stringify(press));
      return decide(channel, PRESS_POINT, event, 'run') === 'run';
    }
    channel.report(PRESS_REPORT, press);
    return true;
  };

  const cancelLongPress = () => {
    if (longPressTimer !== undefined) {
      clearTimeout(longPressTimer);
      longPressTimer = undefined;
    }
    touchPosition = null;
  };

  // ContextMenuTrigger.tsx:56-81: the anchor at the point, the open request and the release window.
  const openAt = (x: number, y: number, event: MouseEvent | TouchEvent) => {
    const touch = event.type.startsWith('touch');
    record.gestured = true;
    arm(record.armed, releaseRules({ x, y }));
    setSessionPositionReference(token(), { x, y, width: touch ? 10 : 0, height: touch ? 10 : 0 });
    allowMouseUp = false;
    const report: ContextMenuOpenReport = { x, y, pointerType: touch ? 'touch' : 'mouse', event: eventInfo(event) };
    channel.report(OPEN_REPORT, report);
    clearTimeout(allowMouseUpTimer);
    allowMouseUpTimer = setTimeout(() => {
      allowMouseUpTimer = undefined;
      allowMouseUp = true;
    }, LONG_PRESS_DELAY);
  };

  // ContextMenuTrigger.tsx:96-127: the one-shot release, removed with the Trigger.
  const onDocumentMouseUp = (event: MouseEvent) => {
    removeMouseUp?.();
    arm(false);
    if (!allowMouseUp) {
      return;
    }
    clearTimeout(allowMouseUpTimer);
    allowMouseUpTimer = undefined;
    allowMouseUp = false;
    const target = getTarget(event);
    const targetElement = isElement(target) ? target : null;
    if (contains(record.positioner, targetElement)) {
      return;
    }
    const rootId = options.rootId;
    if (rootId && targetElement !== null && closest(targetElement, ROOT_OWNER_SELECTOR)?.getAttribute('data-rootownerid') === rootId) {
      return;
    }
    const report: ContextMenuCancelOpenReport = { event: eventInfo(event) };
    channel.report(CANCEL_OPEN_REPORT, report);
  };

  // ContextMenuTrigger.tsx:83-128.
  const onContextMenu = (event: MouseEvent) => {
    const allowed = consumerAllows(event);
    if (!allowed || disabled()) {
      return;
    }
    record.armed = true;
    event.preventDefault();
    event.stopPropagation();
    openAt(event.clientX, event.clientY, event);
    removeMouseUp?.();
    doc.addEventListener('mouseup', onDocumentMouseUp);
    removeMouseUp = () => {
      doc.removeEventListener('mouseup', onDocumentMouseUp);
      removeMouseUp = null;
    };
  };

  // ContextMenuTrigger.tsx:135-155.
  const onTouchStart = (event: TouchEvent) => {
    const allowed = consumerAllows(event);
    if (!allowed) {
      return;
    }
    if (disabled()) {
      cancelLongPress();
      return;
    }
    arm(false);
    if (event.touches.length !== 1) {
      cancelLongPress();
      return;
    }
    event.stopPropagation();
    const touch = event.touches[0]!;
    const position = { x: touch.clientX, y: touch.clientY };
    touchPosition = position;
    clearTimeout(longPressTimer);
    longPressTimer = setTimeout(() => {
      longPressTimer = undefined;
      openAt(position.x, position.y, event);
    }, LONG_PRESS_DELAY);
  };

  // ContextMenuTrigger.tsx:157-174.
  const onTouchMove = (event: TouchEvent) => {
    if (!consumerAllows(event, longPressTimer !== undefined)) {
      return;
    }
    if (event.touches.length !== 1) {
      cancelLongPress();
      return;
    }
    if (longPressTimer !== undefined && touchPosition !== null) {
      const touch = event.touches[0]!;
      const deltaX = Math.abs(touch.clientX - touchPosition.x);
      const deltaY = Math.abs(touch.clientY - touchPosition.y);
      if (deltaX > MOVE_THRESHOLD || deltaY > MOVE_THRESHOLD) {
        cancelLongPress();
      }
    }
  };

  // ContextMenuTrigger.tsx:207-208.
  const onTouchEnd = (event: TouchEvent) => {
    if (consumerAllows(event, longPressTimer !== undefined)) {
      cancelLongPress();
    }
  };

  // ContextMenuTrigger.tsx:177-192: the native menu stays suppressed over the Trigger and the backdrops, even when a
  // consumer veto skipped the part's own handling.
  const onDocumentContextMenu = (event: MouseEvent) => {
    if (disabled()) {
      return;
    }
    const target = getTarget(event);
    const targetElement = isElement(target) ? target : null;
    if (contains(element, targetElement) || contains(record.internalBackdrop, targetElement) || contains(record.backdrop, targetElement)) {
      event.preventDefault();
    }
  };

  element.addEventListener('contextmenu', onContextMenu);
  element.addEventListener('touchstart', onTouchStart);
  element.addEventListener('touchmove', onTouchMove);
  element.addEventListener('touchend', onTouchEnd);
  element.addEventListener('touchcancel', onTouchEnd);
  doc.addEventListener('contextmenu', onDocumentContextMenu);

  let disposed = false;
  return {
    update(optionsJson) {
      if (!disposed) {
        options = parseOptions<ContextMenuOptions>(optionsJson);
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      element.removeEventListener('contextmenu', onContextMenu);
      element.removeEventListener('touchstart', onTouchStart);
      element.removeEventListener('touchmove', onTouchMove);
      element.removeEventListener('touchend', onTouchEnd);
      element.removeEventListener('touchcancel', onTouchEnd);
      doc.removeEventListener('contextmenu', onDocumentContextMenu);
      removeMouseUp?.();
      cancelLongPress();
      clearTimeout(allowMouseUpTimer);
      allowMouseUpTimer = undefined;
      releaseSession(session);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The twin contract
// ---------------------------------------------------------------------------------------------------------------

/**
 * Attaches a ContextMenu handle: the Root's by token (no element), or a Trigger's on its element.
 * @param elementOrId The Trigger's element or its id, or `null` for the Root's token form.
 * @param optionsJson The `ContextMenuOptions` JSON.
 * @param twinReference The `ContextMenuTwin`'s `DotNetObjectReference`.
 * @param references The Root's elements, named by index in its options.
 * @param captures The capture ids of the elements.
 * @returns The handle.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const options = parseOptions<ContextMenuOptions>(optionsJson);
  const element = attachTarget(elementOrId, captures) as AttachedElement | null;
  if (element === null || options.part === ROOT_PART) {
    return attachRoot(options, references, captures);
  }
  const existing = element[attachedHandle];
  if (existing) {
    return existing;
  }
  const handle = attachTrigger(element, options, createTwinChannel(twinReference), twinReference);
  const wrapped: TwinHandle = {
    update(nextOptionsJson, nextReferences, nextCaptures) {
      handle.update(nextOptionsJson, nextReferences, nextCaptures);
    },
    dispose() {
      if (element[attachedHandle] === wrapped) {
        delete element[attachedHandle];
      }
      handle.dispose();
    },
  };
  element[attachedHandle] = wrapped;
  return wrapped;
}
