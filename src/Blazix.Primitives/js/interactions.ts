// The `interactions` mechanism module (ADR 04 §1's row: press-to-toggle, dismissal, focus-to-open, cursor anchoring,
// hover with safe polygon, delay group, floating tree; contract docs/architecture/floating-and-positioning.md §4.7,
// §4.9, §4.10, §5.3). ADR 04 Amendments 2026-10-27 (chosen by the user 2026-10-27): the token form of `attach` and the
// per-popup session, the open-change relay, the decision point `press`, the verdict `Prevent` at `escape`, every hover,
// rest, close, intent and group timer in JavaScript, and the sanctioned page-global state (the session map and the
// delay groups, js/CONTEXT.md).
// One handle per attaching part, by role (design point 2 of M4 track 3):
// - `session`: the elementless handle of a popup Root (the token form). It owns Base UI's root-store context (FLT-STR-03,
//   FLT-STR-05), applies the relay and emits `openchange` (FLT-STA-02/03), and listens on the document for Escape and
//   outside presses (FLT-DIS-*, FLT-KEY-*), asking `escape` on its twin.
// - `trigger`: a Trigger's element: click (`press`), reference press, hover, focus, cursor anchoring, nested tooltip
//   triggers and the hovered line.
// - `floating`: the floating element (a Positioner, or Dialog's Popup): the inside-press marks, the floating `keydown`,
//   the hover-floating leave; it publishes the tree node's context (FLT-STR-08).
// - `provider`: the elementless handle of a TooltipProvider: one delay group per provider token (FLT-ALG-23).
// Transcribed from Base UI v1.9.0, packages/react/src/floating-ui-react: components/FloatingRootStore.ts,
// FloatingTreeStore.ts, FloatingTree.tsx, FloatingDelayGroup.tsx; hooks/useClick.ts, useDismiss.ts, useFocus.ts,
// useClientPoint.ts, useHoverReferenceInteraction.ts, useHoverFloatingInteraction.ts, useHoverShared.ts,
// useHoverInteractionSharedState.ts; safePolygon.ts; utils/createEventEmitter.ts, event.ts, element.ts, nodes.ts.
import { classifyClick, type InteractionType } from './button';
import { decide } from './decide';
import { activeElement, closest, contains, getTarget, isElement, isHTMLElement, ownerDocument, ownerWindow, platform } from './dom';
import { eventInfo } from './event-info';
import { cancelFrame, requestFrame } from './transitions';
import {
  attachTarget,
  createTwinChannel,
  type AttachTarget,
  parseOptions,
  type DotNetTwin,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
  type TwinCaptures,
} from './twin';

// ---------------------------------------------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------------------------------------------

/** The decision point a trigger handle asks before it requests the open value a press computes. */
export const PRESS_POINT = 'press';

/** The decision point a session asks before it closes on Escape. */
export const ESCAPE_POINT = 'escape';

/** The role literals of `InteractionsOptions`: which sub-record a handle reads. */
export type InteractionsRole = 'session' | 'trigger' | 'floating' | 'provider';

// FLT-ALG-28: Base UI's utils/constants.ts.
const TYPEABLE_SELECTOR = "input:not([type='hidden']):not([disabled]),[contenteditable]:not([contenteditable='false']),textarea:not([disabled])";
// tooltip/trigger/TooltipTriggerDataAttributes.ts:10, read by isTargetInsideEnabledTrigger.
const TRIGGER_DISABLED_ATTRIBUTE = 'data-trigger-disabled';

// ---------------------------------------------------------------------------------------------------------------
// The options (mirrors InteractionsOptions in C#)
// ---------------------------------------------------------------------------------------------------------------

// ADR 04 Amendments 2026-10-27
/**
 * The last committed open change, relayed by C# in `update`; mirrors the popup store's
 * relay record. The session emits `openchange` when `revision` moves.
 */
export interface OpenChangeRelay {
  revision: number;
  reason: string;
  eventKind?: string | null;
  eventType?: string | null;
  pointerType?: string | null;
  triggerId?: string | null;
  /** The revision of a close C# committed before the open this relay carries, in the same batch; else absent. */
  coalescedCloseRevision?: number | null;
  /** The reason of that close. */
  coalescedCloseReason?: string | null;
}

// FLT-API-02
/** The press type an outside press is read as. */
export type PressType = 'intentional' | 'sloppy';

// FLT §6.4
/** The options of `useDismiss` on a session; `null` turns dismissal off. */
export interface DismissOptions {
  // FLT-KEY-01
  /** Whether Escape closes the popup; `true` when absent. */
  escapeKey?: boolean | null;
  // FLT-DIS-09
  /** Whether an outside press closes the popup; `true` when absent. */
  outsidePress?: boolean | null;
  // FLT-API-02
  /** The press type per pointer type; `sloppy` for both when absent. */
  outsidePressEvent?: { mouse?: PressType | null; touch?: PressType | null } | null;
  // FLT-API-01
  /** Whether Escape bubbles through the tree; `false` when absent. */
  bubblesEscapeKey?: boolean | null;
  // FLT-API-01
  /** Whether an outside press bubbles through the tree; `true` when absent. */
  bubblesOutsidePress?: boolean | null;
  /** Dialog's predicate: only a primary-button press dismisses. */
  primaryButtonOnly?: boolean | null;
  /** Dialog's predicate: only a single-touch press dismisses. */
  singleTouch?: boolean | null;
  /** Dialog's predicate: the ids of the popup's own backdrops, a press on which dismisses even though they lie outside. */
  ownBackdropIds?: string[] | null;
  /** Dialog's predicate: only the topmost open popup dismisses. */
  topmost?: boolean | null;
}

/** The options of a `session` handle (a popup Root). */
export interface SessionRoleOptions {
  /** Whether the popup is open, as committed by C#. */
  open?: boolean | null;
  /** The id of the registered trigger the popup is anchored to. */
  activeTriggerId?: string | null;
  /** The token of the enclosing popup's session, from the cascade; `null` at the top level. */
  parentToken?: string | null;
  /** The popup element's id (Base UI's `floatingId`). */
  floatingId?: string | null;
  // FLT-PTR-13
  /** Whether the popup is in its closing transition (`transitionStatus === 'ending'`). */
  ending?: boolean | null;
  /** The number of requests C# answered, canceled ones included; a move re-arms a session that requested a close. */
  answered?: number | null;
  /** Dismissal; `null` turns it off. */
  dismiss?: DismissOptions | null;
}

// FLT §6.3
/** The options of `useClick` on a trigger; `null` turns it off. */
export interface ClickOptions {
  // FLT-PTR-02/03
  /** Which pointer event toggles; `click` when absent. */
  event?: 'click' | 'mousedown' | 'mousedown-only' | null;
  // FLT-PTR-05
  /** Whether a repeated press closes; `true` when absent. */
  toggle?: boolean | null;
  /** Whether strictly mouse-like presses are dropped. */
  ignoreMouse?: boolean | null;
  // FLT-PTR-06
  /** Whether a press keeps a hover- or focus-opened popup open; `true` when absent. */
  stickIfOpen?: boolean | null;
  /** Whether `stickIfOpen` holds only within the patient-click window after a hover open (Popover). */
  patientClick?: boolean | null;
  // FLT-PTR-04
  /** The touch-only open deferral in milliseconds. */
  touchOpenDelay?: number | null;
  /** The reason of the request; `trigger-press` when absent. */
  reason?: 'trigger-press' | 'input-press' | null;
}

// FLT §6.7; §6.9
/** The options of `useHoverReferenceInteraction` on a trigger; `null` turns it off. */
export interface HoverOptions {
  /** The open delay in milliseconds. */
  delayOpen?: number | null;
  /** The close delay in milliseconds. */
  delayClose?: number | null;
  // FLT-PTR-16
  /** The rest time before a hover opens. */
  restMs?: number | null;
  /** Whether the first `mousemove` also opens; `true` when absent. */
  move?: boolean | null;
  // FLT-PTR-11
  /** Whether only mouse-like pointers hover. */
  mouseOnly?: boolean | null;
  // FLT-ALG-15
  /** Whether leaving the trigger is judged by the safe polygon. */
  safePolygon?: boolean | null;
  // FLT-DOM-19
  /** The safe polygon's `blockPointerEvents`. */
  blockPointerEvents?: boolean | null;
  // FLT-PTR-18
  /** Whether a `mouseout` cancels a pending open. */
  guardStaleOpen?: boolean | null;
  /** Whether a nested tooltip trigger (`data-base-ui-tooltip-trigger`) vetoes the hover (Tooltip's `shouldOpen`). */
  nestedTooltipTriggers?: boolean | null;
  // FLT-ALG-23
  /** The provider token whose delay group the trigger belongs to. */
  delayGroupToken?: string | null;
}

// FLT §6.5
/** The options of `useFocus` on a trigger; `null` turns it off. */
export interface FocusOptions {
  // FLT-FOC-01
  /** The open delay in milliseconds. */
  delay?: number | null;
}

// FLT §6.6
/** The options of `useClientPoint` on a trigger; `null` turns it off. */
export interface ClientPointOptions {
  // FLT-API-14
  /** The tracked axis; `both` when absent. */
  axis?: 'x' | 'y' | 'both' | null;
}

/** The options of a `trigger` handle. */
export interface TriggerRoleOptions {
  /** The trigger's id in the session's trigger map; the element's `id` when absent. */
  triggerId?: string | null;
  click?: ClickOptions | null;
  // FLT-PTR-30
  /** Whether a press on the trigger closes the open popup. */
  referencePress?: boolean | null;
  /** Whether a press on a closed popup's trigger cancels a pending hover open on the bus alone (Tooltip). */
  cancelPendingOpenOnPress?: boolean | null;
  hover?: HoverOptions | null;
  focus?: FocusOptions | null;
  clientPoint?: ClientPointOptions | null;
  /** Whether the trigger records the hovered line for the inline middleware (PreviewCard). */
  hoveredLine?: boolean | null;
}

// FLT §6.8
/** The options of `useHoverFloatingInteraction` on a floating element; `null` turns it off. */
export interface FloatingHoverOptions {
  // FLT-PTR-24
  /** The close delay in milliseconds after the pointer leaves the floating element. */
  closeDelay?: number | null;
}

/** The options of a `floating` handle. */
export interface FloatingRoleOptions {
  // FLT-STR-07
  /** Whether the session registers a tree node for this floating element; `true` when absent. */
  node?: boolean | null;
  hover?: FloatingHoverOptions | null;
}

// FLT §6.9
/** The options of a `provider` handle (a TooltipProvider). */
export interface ProviderRoleOptions {
  /** The provider token the group is keyed by. */
  token?: string | null;
  /** The group's open delay in milliseconds. */
  delayOpen?: number | null;
  /** The group's close delay in milliseconds. */
  delayClose?: number | null;
  /** The warm period after the last member closes, in milliseconds. */
  timeout?: number | null;
}

/** The options of an `interactions` handle; mirrors `InteractionsOptions` in C#. Exactly one role is set. */
export interface InteractionsOptions extends TwinOptions {
  /** The session token the handle joins: a Root's token, drawn by C# and never rendered. */
  sessionToken?: string | null;
  /** The last committed open change; read by the `session` handle. */
  relay?: OpenChangeRelay | null;
  session?: SessionRoleOptions | null;
  trigger?: TriggerRoleOptions | null;
  floating?: FloatingRoleOptions | null;
  provider?: ProviderRoleOptions | null;
}

// ---------------------------------------------------------------------------------------------------------------
// Event and element predicates (FLT-ALG-26, FLT-ALG-28): utils/event.ts and utils/element.ts
// ---------------------------------------------------------------------------------------------------------------

// License: https://github.com/adobe/react-spectrum/blob/main/packages/@react-aria/utils/src/isVirtualEvent.ts
/**
 * Determines whether a click was produced without a pointer press: a keyboard, an assistive technology or a script.
 * @param event The click.
 * @returns `true` for a trusted click with an empty pointer type, Android's virtual click, or a click with no click
 * count and no pointer type.
 */
export function isVirtualClick(event: MouseEvent | PointerEvent): boolean {
  if ((event as PointerEvent).pointerType === '' && event.isTrusted) {
    return true;
  }
  if (platform.os.android && (event as PointerEvent).pointerType) {
    return event.type === 'click' && event.buttons === 1;
  }
  return event.detail === 0 && !(event as PointerEvent).pointerType;
}

/**
 * Determines whether a pointer event was synthesized by a screen reader (TalkBack, VoiceOver, NVDA).
 * @param event The pointer event.
 * @returns `true` for the zero-size, the pressureless 1x1 mouse `pointerdown` and the iOS VoiceOver shapes; always
 * `false` in jsdom.
 */
export function isVirtualPointerEvent(event: PointerEvent): boolean {
  if (platform.env.jsdom) {
    return false;
  }
  return (
    (!platform.os.android && event.width === 0 && event.height === 0) ||
    (event.type === 'pointerdown' &&
      event.width === 1 &&
      event.height === 1 &&
      event.pressure === 0 &&
      event.detail === 0 &&
      event.pointerType === 'mouse' &&
      (platform.os.android || event.buttons === 0)) ||
    (event.width < 1 && event.height < 1 && event.pressure === 0 && event.detail === 0 && event.pointerType === 'touch')
  );
}

/**
 * Determines whether a pointer type behaves like a mouse. Chromium on some Linux machines reports a mouse as `pen`.
 * @param pointerType The pointer type.
 * @param strict Whether an empty or missing pointer type counts as not mouse-like.
 * @returns `true` for `mouse` and `pen`, and unless `strict` for `''` and `undefined`.
 */
export function isMouseLikePointerType(pointerType: string | undefined, strict?: boolean): boolean {
  if (pointerType === 'mouse' || pointerType === 'pen') {
    return true;
  }
  return !strict && (pointerType === '' || pointerType === undefined);
}

// FLT-STA-04
/**
 * Determines whether an event is click-like.
 * @param event The event, or a stand-in carrying its type.
 * @returns `true` for `click`, `mousedown`, `keydown` and `keyup`.
 */
export function isClickLikeEvent(event: { type: string }): boolean {
  const type = event.type;
  return type === 'click' || type === 'mousedown' || type === 'keydown' || type === 'keyup';
}

/**
 * Determines whether an element accepts typed text.
 * @param element The element.
 * @returns `true` for an enabled non-hidden input, an enabled textarea and an editable element.
 */
export function isTypeableElement(element: unknown): boolean {
  return isHTMLElement(element) && element.matches(TYPEABLE_SELECTOR);
}

/**
 * Determines whether an element is interactive or inside an interactive element.
 * @param element The element.
 * @returns `true` inside a button, a link, a `role="button"`, a select, a tabbable element or a typeable element.
 */
export function isInteractiveElement(element: Element | null): boolean {
  return closest(element, `button,a[href],[role="button"],select,[tabindex]:not([tabindex="-1"]),${TYPEABLE_SELECTOR}`) != null;
}

/**
 * Determines whether an element is the document element or the body.
 * @param element The element.
 * @returns `true` for `<html>` and `<body>`.
 */
export function isRootElement(element: Element): boolean {
  return element.matches('html,body');
}

/**
 * Determines whether an event's path passes through a node.
 * @param event The event.
 * @param node The node, or `null`.
 * @returns `true` when the composed path includes the node (or, without a composed path, the node contains the target).
 */
export function isEventTargetWithin(event: Event, node: Node | null | undefined): boolean {
  if (node == null) {
    return false;
  }
  if ('composedPath' in event) {
    return event.composedPath().includes(node);
  }
  const target = (event as Event).target;
  return target != null && node.contains(target as Node);
}

// FLT-A11Y-08
/**
 * Determines whether an element matches `:focus-visible`.
 * @param element The element, or `null`.
 * @returns The match, or `true` without an element, in jsdom and where the selector is unsupported.
 */
export function matchesFocusVisible(element: Element | null): boolean {
  if (!element || platform.env.jsdom) {
    return true;
  }
  try {
    return element.matches(':focus-visible');
  } catch {
    return true;
  }
}

/**
 * Determines whether a target is a registered trigger, or inside one, that is not disabled.
 * @param target The target.
 * @param triggers The session's trigger map.
 * @returns `true` inside a registered trigger without `data-trigger-disabled`.
 */
export function isTargetInsideEnabledTrigger(target: EventTarget | null, triggers: SessionTriggerMap): boolean {
  if (!isElement(target)) {
    return false;
  }
  if (triggers.hasElement(target)) {
    return !target.hasAttribute(TRIGGER_DISABLED_ATTRIBUTE);
  }
  for (const [, trigger] of triggers.entries()) {
    if (contains(trigger, target)) {
      return !trigger.hasAttribute(TRIGGER_DISABLED_ATTRIBUTE);
    }
  }
  return false;
}

// ---------------------------------------------------------------------------------------------------------------
// The event bus (FLT-STR-04): utils/createEventEmitter.ts
// ---------------------------------------------------------------------------------------------------------------

/** The synchronous emitter of a session or of a tree. */
export interface SessionEvents {
  emit(event: string, data?: unknown): void;
  on(event: string, handler: (data: any) => void): void;
  off(event: string, handler: (data: any) => void): void;
}

/**
 * Creates a synchronous emitter: a set of listeners per name, called in registration order.
 * @returns The emitter.
 */
export function createEventEmitter(): SessionEvents {
  const map = new Map<string, Set<(data: any) => void>>();
  return {
    emit(event, data) {
      map.get(event)?.forEach((listener) => listener(data));
    },
    on(event, listener) {
      let listeners = map.get(event);
      if (!listeners) {
        listeners = new Set();
        map.set(event, listeners);
      }
      listeners.add(listener);
    },
    off(event, listener) {
      map.get(event)?.delete(listener);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The tree (FLT-STR-06..09, FLT-ALG-20): utils/nodes.ts over the session forest
// ---------------------------------------------------------------------------------------------------------------

// FLT-STR-08
/** A tree node's context, written by the session from its floating handle. */
export interface SessionFloatingContext {
  readonly open: boolean;
  readonly nodeId: string;
  readonly floatingId: string | undefined;
  readonly elements: { readonly domReference: Element | null; readonly floating: HTMLElement | null };
  readonly dataRef: { readonly current: SessionData };
  readonly events: SessionEvents;
  readonly placement: string | undefined;
}

/** A node of the session tree: the session token and the enclosing popup's token. */
export interface SessionNode {
  readonly id: string | undefined;
  readonly parentId: string | null;
  readonly context?: ({ readonly open: boolean } & Partial<SessionFloatingContext>) | undefined;
}

/** The tree view of a session, shaped like Base UI's FloatingTreeStore. */
export interface SessionTree {
  readonly nodesRef: { readonly current: readonly SessionNode[] };
  readonly events: SessionEvents;
}

// FLT-ALG-20
/**
 * Returns the descendants of a node, depth first.
 * @param nodes The tree's nodes.
 * @param id The node's id.
 * @param onlyOpenChildren Whether closed or contextless descendants are left out (their own descendants are kept).
 * @returns The descendants.
 */
export function getNodeChildren<T extends SessionNode>(nodes: readonly T[], id: string | undefined, onlyOpenChildren = true): T[] {
  const directChildren = nodes.filter((node) => node.parentId === id);
  return directChildren.flatMap((child) => [
    ...(!onlyOpenChildren || child.context?.open ? [child] : []),
    ...getNodeChildren(nodes, child.id, onlyOpenChildren),
  ]);
}

// FLT-ALG-20
/**
 * Returns the ancestors of a node, nearest first.
 * @param nodes The tree's nodes.
 * @param id The node's id.
 * @returns The ancestors.
 */
export function getNodeAncestors<T extends SessionNode>(nodes: readonly T[], id: string | undefined): T[] {
  let allAncestors: T[] = [];
  let currentParentId = nodes.find((node) => node.id === id)?.parentId;
  while (currentParentId) {
    const currentNode = nodes.find((node) => node.id === currentParentId);
    currentParentId = currentNode?.parentId;
    if (currentNode) {
      allAncestors = allAncestors.concat(currentNode);
    }
  }
  return allAncestors;
}

// ---------------------------------------------------------------------------------------------------------------
// The session (ADR 04 Amendments 2026-10-27, decision 3): the token-keyed map
// ---------------------------------------------------------------------------------------------------------------

// FLT-STR-05
/** Base UI's `dataRef.current`. A module that adds a key owns it. */
export interface SessionData {
  openEvent?: Event | undefined;
  floatingContext?: SessionFloatingContext | undefined;
  insideReactTree?: boolean;
  __escapeKeyBubbles?: boolean;
  __outsidePressBubbles?: boolean;
  [key: string]: unknown;
}

// FLT-STA-02
/** The payload of the session bus's `openchange`. */
export interface SessionOpenChangeDetails {
  open: boolean;
  reason: string;
  nativeEvent: Event | undefined;
  nested: boolean;
  triggerElement: Element | undefined;
}

// PSH-STA-33 (the trigger map)
/** The triggers registered in a session, by id. */
export interface SessionTriggerMap {
  readonly size: number;
  getById(id: string): Element | undefined;
  hasElement(element: Element): boolean;
  hasMatchingElement(predicate: (element: Element) => boolean): boolean;
  entries(): IterableIterator<[string, Element]>;
  elements(): IterableIterator<Element>;
}

/** A virtual reference: cursor anchoring's point or the hovered line's rect. */
export interface SessionVirtualElement {
  getBoundingClientRect(): DOMRect | { x: number; y: number; width: number; height: number; top: number; right: number; bottom: number; left: number };
  getClientRects?(): DOMRect[] | DOMRectList;
  contextElement?: Element | undefined;
}

/** The per-popup session: what Base UI's root store keeps out of React state, shared by every module's handles. */
export interface InteractionSession {
  readonly token: string;
  /** Whether the popup is open, as last applied from C#. */
  readonly open: boolean;
  /** 0 while closed; otherwise a page-wide counter taken when the open was applied (the topmost gate). */
  readonly openOrder: number;
  readonly dataRef: { readonly current: SessionData };
  readonly events: SessionEvents;
  readonly node: SessionNode;
  readonly nested: boolean;
  readonly tree: SessionTree;
  readonly floatingElement: HTMLElement | null;
  readonly domReferenceElement: Element | null;
  readonly triggerElements: SessionTriggerMap;
  readonly positionReference: SessionVirtualElement | null;
  /**
   * Subscribes to changes of `positionReference`.
   * @param listener Called after each change.
   * @returns The unsubscribe function.
   */
  subscribePositionReference(listener: () => void): () => void;
  /**
   * Publishes the placement of the latest positioning pass, read by the safe polygon.
   * @param placement The placement, for example `bottom-start`.
   */
  setPlacement(placement: string | undefined): void;
}

/** The pending request a session's handle sent, kept so its event can be paired with the commit the relay brings. */
interface PendingRequest {
  open: boolean;
  reason: string;
  event: Event | undefined;
}

/** The module-side state of a session; the exported interface is its read-only face. */
interface SessionRecord extends InteractionSession {
  references: number;
  open: boolean;
  openOrder: number;
  ending: boolean;
  activeTriggerId: string | null;
  parentToken: string | null;
  floatingId: string | undefined;
  floatingElement: HTMLElement | null;
  hasNode: boolean;
  placement: string | undefined;
  positionReference: SessionVirtualElement | null;
  lastRevision: number | undefined;
  /** The answered-requests counter last applied. */
  answered: number;
  pending: PendingRequest | null;
  /** A press's open event recorded on its request, with the one it replaced, until C# commits or cancels the request. */
  optimisticOpenEvent: { request: PendingRequest; previous: Event | undefined } | null;
  /** The element the next relayed `focus-out` close without a trigger names (`nameRelayTrigger`). */
  namedTrigger: Element | null;
  triggers: Map<string, Element>;
  /** The channel of the session handle (the Root's twin), which answers `escape`. */
  channel: TwinChannel | null;
  positionListeners: Set<() => void>;
  /** The hook regions' private per-session state (`sessionSlot`). */
  slots: Map<string, unknown>;
  /** The session's tree node, a live view built on first use. */
  nodeView: SessionNode | undefined;
  /** Called after the session applied a commit, or gained or lost its floating element or a trigger (`onSessionChange`). */
  changeListeners: Set<() => void>;
  treeEvents: SessionEvents;
  context: SessionFloatingContext;
}

// The sanctioned page-global state of the module (ADR 04 Amendments 2026-10-27; js/CONTEXT.md): the session map. An
// entry is created by the first handle, of any module, that names a token, and deleted with the last release.
const sessions = new Map<string, SessionRecord>();
let openCounter = 0;

function createTriggerMap(triggers: Map<string, Element>): SessionTriggerMap {
  return {
    get size() {
      return triggers.size;
    },
    getById: (id) => triggers.get(id),
    hasElement(element) {
      for (const trigger of triggers.values()) {
        if (trigger === element) {
          return true;
        }
      }
      return false;
    },
    hasMatchingElement(predicate) {
      for (const trigger of triggers.values()) {
        if (predicate(trigger)) {
          return true;
        }
      }
      return false;
    },
    entries: () => triggers.entries(),
    elements: () => triggers.values(),
  };
}

function liveNodes(): SessionNode[] {
  const nodes: SessionNode[] = [];
  for (const record of sessions.values()) {
    if (record.hasNode) {
      nodes.push(record.node);
    }
  }
  return nodes;
}

function forestRoot(record: SessionRecord): SessionRecord {
  let current = record;
  const seen = new Set<SessionRecord>();
  while (current.parentToken !== null && !seen.has(current)) {
    seen.add(current);
    const parent = sessions.get(current.parentToken);
    if (!parent) {
      break;
    }
    current = parent;
  }
  return current;
}

function createSessionRecord(token: string): SessionRecord {
  const dataRef: { current: SessionData } = { current: {} };
  const events = createEventEmitter();
  const triggers = new Map<string, Element>();
  const triggerElements = createTriggerMap(triggers);
  const record = {
    token,
    references: 0,
    open: false,
    openOrder: 0,
    ending: false,
    activeTriggerId: null,
    parentToken: null,
    floatingId: undefined,
    floatingElement: null,
    hasNode: false,
    placement: undefined,
    positionReference: null,
    lastRevision: undefined,
    answered: 0,
    pending: null,
    optimisticOpenEvent: null,
    namedTrigger: null,
    triggers,
    channel: null,
    positionListeners: new Set(),
    slots: new Map(),
    nodeView: undefined,
    changeListeners: new Set(),
    treeEvents: createEventEmitter(),
    dataRef,
    events,
    triggerElements,
    get nested() {
      return record.parentToken !== null;
    },
    get domReferenceElement() {
      return record.activeTriggerId === null ? null : (triggers.get(record.activeTriggerId) ?? null);
    },
    get node(): SessionNode {
      return nodeOf(record);
    },
    get tree(): SessionTree {
      return { nodesRef: { get current() { return liveNodes(); } }, events: forestRoot(record).treeEvents };
    },
    subscribePositionReference(listener: () => void) {
      record.positionListeners.add(listener);
      return () => {
        record.positionListeners.delete(listener);
      };
    },
    setPlacement(placement: string | undefined) {
      record.placement = placement;
    },
  } as unknown as SessionRecord;
  // FLT-STR-08: the node's context, a live view of the session.
  record.context = {
    get open() {
      return record.open;
    },
    nodeId: token,
    get floatingId() {
      return record.floatingId;
    },
    elements: {
      get domReference() {
        return record.domReferenceElement;
      },
      get floating() {
        return record.floatingElement;
      },
    },
    dataRef,
    events,
    get placement() {
      return record.placement;
    },
  };
  return record;
}

// The node is a live view of the session, built once and kept on the session itself.
function nodeOf(record: SessionRecord): SessionNode {
  const cached = record.nodeView;
  if (cached) {
    return cached;
  }
  const node: SessionNode = {
    id: record.token,
    get parentId() {
      return record.parentToken;
    },
    get context() {
      return record.floatingElement ? record.context : undefined;
    },
  };
  record.nodeView = node;
  return node;
}

/**
 * Takes a reference on the session of a token, creating it on first use. Every handle that names a token, of any
 * module, takes one at `attach`, in any order.
 * @param token The session token C# drew for the popup.
 * @returns The session.
 */
export function acquireSession(token: string): InteractionSession {
  return acquireRecord(token);
}

function acquireRecord(token: string): SessionRecord {
  let record = sessions.get(token);
  if (!record) {
    record = createSessionRecord(token);
    sessions.set(token, record);
  }
  record.references += 1;
  return record;
}

/**
 * Drops one reference on a session; the entry is deleted when the last reference goes.
 * @param session The session `acquireSession` returned.
 */
export function releaseSession(session: InteractionSession): void {
  const record = session as SessionRecord;
  record.references -= 1;
  if (record.references <= 0 && sessions.get(record.token) === record) {
    sessions.delete(record.token);
    record.positionListeners.clear();
  }
}

/**
 * Returns the live session of a token.
 * @param token The session token.
 * @returns The session, or `undefined` when no handle holds it.
 */
export function getSession(token: string): InteractionSession | undefined {
  return sessions.get(token);
}

/**
 * Names the element the next relayed `focus-out` close without a trigger carries as its trigger element, such as a
 * popup trigger's focus guard, which the guard's own close names; a canceled close clears it.
 * @param session The session.
 * @param element The element.
 */
export function nameRelayTrigger(session: InteractionSession, element: Element): void {
  (session as SessionRecord).namedTrigger = element;
}

/**
 * Determines whether no open session was opened later than this one (the topmost gate of nested popups).
 * @param session The session.
 * @returns `true` when the session is open and topmost.
 */
export function isTopmostOpenSession(session: InteractionSession): boolean {
  if (!session.open) {
    return false;
  }
  for (const record of sessions.values()) {
    if (record !== session && record.open && record.openOrder > session.openOrder) {
      return false;
    }
  }
  return true;
}

/** Clears the page-global state of the module: the session map and the delay groups. For tests. */
export function resetInteractionsForTest(): void {
  sessions.clear();
  delayGroups.clear();
  openCounter = 0;
}

// FLT-STA-03: the recorded open event. A pending hover open never downgrades a click open; a click upgrades a hover open.
function syncOpenEvent(record: SessionRecord, newOpen: boolean, event: Event | undefined): void {
  if (!newOpen || !record.open || (event != null && isClickLikeEvent(event))) {
    record.dataRef.current.openEvent = newOpen ? event : undefined;
  }
}

// A committed change C# did not start from this session's last request carries no browser event: a stand-in with the
// relayed type and pointer type takes its place (design point 3's interface).
function relayedEvent(record: SessionRecord, open: boolean, relay: OpenChangeRelay): Event | undefined {
  const pending = record.pending;
  if (pending !== null && pending.open === open && pending.reason === relay.reason && (pending.event?.type ?? null) === (relay.eventType ?? null)) {
    return pending.event;
  }
  if (relay.eventType == null || relay.eventType === 'base-ui') {
    return undefined;
  }
  return { type: relay.eventType, pointerType: relay.pointerType ?? undefined } as unknown as Event;
}

// FLT-STA-02 (with the FLT-EVT-02 fidelity correction: C# called the consumer first and relays only a change it did
// not cancel): record the open event, then emit `openchange`, then apply the committed state.
function applySessionOptions(record: SessionRecord, options: InteractionsOptions): void {
  const role = options.session ?? {};
  const open = role.open === true;
  const relay = options.relay ?? null;
  record.parentToken = role.parentToken ?? null;
  record.floatingId = role.floatingId ?? undefined;
  record.ending = role.ending === true;
  if (relay !== null && relay.revision !== record.lastRevision) {
    const first = record.lastRevision === undefined;
    const previousRevision = record.lastRevision ?? 0;
    record.lastRevision = relay.revision;
    // ADR 04 Amendments 2026-10-27 (design point 11, settled by the orchestrator 2026-10-27): a close C# committed before
    // this open in the same batch is replayed first with its own reason, and its state applied, as Base UI dispatches it.
    if (open && relay.coalescedCloseRevision != null && relay.coalescedCloseRevision > previousRevision && record.open) {
      replayClose(record, relay.coalescedCloseReason ?? 'none');
    }
    if (!first || relay.revision > 0) {
      const event = relayedEvent(record, open, relay);
      record.pending = null;
      record.optimisticOpenEvent = null;
      syncOpenEvent(record, open, event);
      const details: SessionOpenChangeDetails = {
        open,
        reason: relay.reason,
        nativeEvent: event,
        nested: record.nested,
        triggerElement: relay.triggerId != null ? record.triggers.get(relay.triggerId) : namedTrigger(record, open, relay.reason),
      };
      record.events.emit('openchange', details);
    }
  }
  if (open && !record.open) {
    openCounter += 1;
    record.openOrder = openCounter;
  } else if (!open) {
    record.openOrder = 0;
  }
  record.open = open;
  record.activeTriggerId = role.activeTriggerId ?? null;
  // Design point 10 (settled by the orchestrator 2026-10-27): a canceled request moves no revision, so the answered
  // counter is what re-arms a session that requested a close.
  const answered = role.answered ?? 0;
  if (answered !== record.answered) {
    record.answered = answered;
    // A canceled close moves no revision: the name it was given does not wait for a later close.
    record.namedTrigger = null;
    rollBackOptimisticOpenEvent(record);
    rearmDismiss(record);
  }
  notifySessionChange(record);
}

// ADR 04 Amendments 2026-10-29 (gap 16 of M4 track 5): Base UI's `useClick` writes the open event in its click handler, so
// a press records its click-like open event on its request, as a hover open awaiting its commit already counts as open;
// the relay's commit confirms it, and an answer that leaves the request pending (C# canceled it) restores the previous.
function recordOptimisticOpenEvent(record: SessionRecord, request: PendingRequest): void {
  const previous = record.dataRef.current.openEvent;
  syncOpenEvent(record, true, request.event);
  record.optimisticOpenEvent = record.dataRef.current.openEvent === previous ? null : { request, previous };
}

function rollBackOptimisticOpenEvent(record: SessionRecord): void {
  const optimistic = record.optimisticOpenEvent;
  record.optimisticOpenEvent = null;
  if (optimistic !== null && record.pending === optimistic.request && record.dataRef.current.openEvent === optimistic.request.event) {
    record.dataRef.current.openEvent = optimistic.previous;
  }
}

// Design point 4's sub-point of M4 track 5 (settled by the orchestrator 2026-10-28): the element `nameRelayTrigger` named
// is the trigger of the next relayed `focus-out` close that names none, once.
function namedTrigger(record: SessionRecord, open: boolean, reason: string): Element | undefined {
  const named = record.namedTrigger;
  record.namedTrigger = null;
  return !open && reason === 'focus-out' && named !== null ? named : undefined;
}

// The coalesced close's dispatch: its `openchange` (with the session's own request event when it was that close), then
// its state, without the post-commit notification (Base UI never renders the transient close).
function replayClose(record: SessionRecord, reason: string): void {
  const pending = record.pending;
  const event = pending !== null && !pending.open && pending.reason === reason ? pending.event : undefined;
  if (event !== undefined) {
    record.pending = null;
  }
  syncOpenEvent(record, false, event);
  const details: SessionOpenChangeDetails = { open: false, reason, nativeEvent: event, nested: record.nested, triggerElement: undefined };
  record.events.emit('openchange', details);
  record.open = false;
  record.openOrder = 0;
}

// ---------------------------------------------------------------------------------------------------------------
// Shared plumbing of the hooks: timers, listeners, requests
// ---------------------------------------------------------------------------------------------------------------

/** A restartable timer (Base UI's `Timeout`): `start` replaces a pending callback. */
interface Timer {
  start(delay: number, callback: () => void): void;
  clear(): void;
  isStarted(): boolean;
}

function createTimer(): Timer {
  let id: ReturnType<typeof setTimeout> | undefined;
  return {
    start(delay, callback) {
      if (id !== undefined) {
        clearTimeout(id);
      }
      id = setTimeout(() => {
        id = undefined;
        callback();
      }, delay);
    },
    clear() {
      if (id !== undefined) {
        clearTimeout(id);
        id = undefined;
      }
    },
    isStarted: () => id !== undefined,
  };
}

type Cleanup = () => void;

function listen<K extends keyof DocumentEventMap>(
  target: EventTarget,
  type: K | string,
  listener: (event: any) => void,
  options?: boolean | AddEventListenerOptions,
): Cleanup {
  target.addEventListener(type, listener, options);
  return () => target.removeEventListener(type, listener, options);
}

function mergeCleanups(...cleanups: (Cleanup | false | null | undefined)[]): Cleanup {
  return () => {
    for (const cleanup of cleanups) {
      if (cleanup) {
        cleanup();
      }
    }
  };
}

function triggerIdOf(record: SessionRecord, element: Element): string | null {
  for (const [id, trigger] of record.triggers) {
    if (trigger === element) {
      return id;
    }
  }
  return element.id || null;
}

// ADR 04 Amendments 2026-10-27
/**
 * A request a handle sends: recorded for the relay's pairing, then reported on the
 * handle's own twin. Nothing is emitted here: `openchange` follows the commit the relay brings.
 */
function requestOpenChange(
  record: SessionRecord,
  channel: TwinChannel,
  open: boolean,
  reason: string,
  event: Event | undefined,
  trigger?: Element | null,
  extra?: Record<string, unknown>,
): void {
  record.pending = { open, reason, event };
  channel.report('OnRequest', {
    open,
    reason,
    event: event === undefined ? null : eventInfo(event),
    triggerId: trigger ? triggerIdOf(record, trigger) : null,
    ...extra,
  });
}

/**
 * A region's private state on a session, created on first use: each hook region keeps what Base UI keeps in its refs
 * per root store here (never in a module-level map), under its own key.
 */
function sessionSlot<T>(record: SessionRecord, key: string, create: () => T): T {
  let value = record.slots.get(key) as T | undefined;
  if (value === undefined) {
    value = create();
    record.slots.set(key, value);
  }
  return value;
}

/**
 * Subscribes to a session's applied changes: a commit the relay brought, after the state is applied (the moment Base UI's
 * effects keyed on the store re-run), and the floating element or a trigger attaching or detaching.
 * @returns The unsubscribe function.
 */
function onSessionChange(record: SessionRecord, listener: () => void): Cleanup {
  record.changeListeners.add(listener);
  return () => {
    record.changeListeners.delete(listener);
  };
}

function notifySessionChange(record: SessionRecord): void {
  for (const listener of [...record.changeListeners]) {
    listener();
  }
}

/** What a hook's controller reads: its session, its handle's element and channel, and the handle's current options. */
interface HandleContext {
  readonly record: SessionRecord;
  /** The trigger or floating element; `null` for the session handle. */
  readonly element: HTMLElement | null;
  readonly channel: TwinChannel;
  readonly twinReference: DotNetTwin;
  /** The options of the last `attach` or `update`. */
  readonly options: InteractionsOptions;
}

/** One hook's half of a handle: told after every `update`, disposed with the handle. */
interface Controller {
  update?(previous: InteractionsOptions): void;
  dispose(): void;
}

// ---------------------------------------------------------------------------------------------------------------
// #region dismiss: useDismiss (FLT-DIS-01..16, FLT-KEY-01..03, FLT-PTR-26..30, FLT-ALG-17) and the `escape` point
// ---------------------------------------------------------------------------------------------------------------
// Transcribed from hooks/useDismiss.ts. The hook's refs live in the session's `dismiss` slot, shared by its three
// halves: the session handle (the document listeners, the options, the published bubbling flags), the floating handle
// (the floating prop bag) and each trigger handle (the reference prop bag). The options are the session's; a `null`
// `dismiss` is Base UI's `enabled: false`. Escape asks `escape` on the session's twin before it acts (ADR 04
// Amendments 2026-10-27, the verdict `Prevent`); Dialog's `outsidePress` function is the four data predicates.

// FLT-DIS-06: `createAttribute('inert')`, the marker the focus manager's outside marking writes (useDismiss.ts:405).
const INERT_MARKER_SELECTOR = '[data-base-ui-inert]';
// The portal node's attribute, read by Dialog's backdrop predicate (dialog/root/useDialogRoot.ts:72-76).
const PORTAL_ATTRIBUTE = 'data-base-ui-portal';

// One close per keydown (FLT §10 Q11, POP §16 Q7; the js/button.ts precedent): the tokens of the sessions that asked
// `escape` for a keydown, so a session's document listener skips a keydown its floating or reference listener handled.
const escapeHandled = Symbol('blazix.interactions.escapeHandled');

interface EscapeMarkedEvent extends KeyboardEvent {
  [escapeHandled]?: Set<string>;
}

// useDismiss.ts:151-157.
/** The touch an outside `touchstart` records. */
interface DismissTouchState {
  startTime: number;
  startX: number;
  startY: number;
  dismissOnTouchEnd: boolean;
  dismissOnMouseDown: boolean;
}

/** What `useDismiss` keeps in refs for one popup, shared by the session's three dismissal halves. */
interface DismissState {
  /** The options the session handle last received; `null` while dismissal is off or no session handle is attached. */
  options: DismissOptions | null;
  /** The session handle's context that stored `options`. */
  owner: HandleContext | null;
  pressStartedInside: boolean;
  pressStartPrevented: boolean;
  /** Ignore only the very next outside click after dragging from inside to outside. */
  suppressNextOutsideClick: boolean;
  /** A click whose press began before the popup opened is the tail of that gesture, not an outside press. */
  sawPressWhileOpen: boolean;
  isComposing: boolean;
  currentPointerType: string;
  touchState: DismissTouchState | null;
  /** Whether a close was requested and the relay has not brought the next open since. */
  disarmed: boolean;
  cancelDismissOnEndTimeout: Timer;
  clearInsideReactTreeTimeout: Timer;
  /** Outlives the listeners' re-installs, so a pending reset cannot leave `isComposing` stuck. */
  compositionTimeout: Timer;
}

/** Re-arms a session that requested a close: C# answered a request (canceled ones included). */
function rearmDismiss(record: SessionRecord): void {
  const state = record.slots.get('dismiss') as DismissState | undefined;
  if (state !== undefined) {
    state.disarmed = false;
  }
}

function dismissState(record: SessionRecord): DismissState {
  return sessionSlot(record, 'dismiss', () => ({
    options: null,
    owner: null,
    pressStartedInside: false,
    pressStartPrevented: false,
    suppressNextOutsideClick: false,
    sawPressWhileOpen: false,
    isComposing: false,
    currentPointerType: '',
    touchState: null,
    disarmed: false,
    cancelDismissOnEndTimeout: createTimer(),
    clearInsideReactTreeTimeout: createTimer(),
    compositionTimeout: createTimer(),
  }));
}

/**
 * Normalizes the bubbling flags of a dismissal: a boolean applies to both keys; otherwise Escape does not bubble and
 * outside presses do, unless a key says otherwise.
 * @param bubbles The option.
 * @returns The two flags.
 */
export function normalizeBubbles(
  bubbles?: boolean | { escapeKey?: boolean | null | undefined; outsidePress?: boolean | null | undefined } | null,
): { escapeKey: boolean; outsidePress: boolean } {
  return {
    escapeKey: typeof bubbles === 'boolean' ? bubbles : (bubbles?.escapeKey ?? false),
    outsidePress: typeof bubbles === 'boolean' ? bubbles : (bubbles?.outsidePress ?? true),
  };
}

// useDismiss.ts:164-167.
function clearInsideReactTree(record: SessionRecord): void {
  dismissState(record).clearInsideReactTreeTimeout.clear();
  record.dataRef.current.insideReactTree = false;
}

// useDismiss.ts:234-237 (FLT-DIS-12).
function markInsideReactTree(record: SessionRecord): void {
  record.dataRef.current.insideReactTree = true;
  dismissState(record).clearInsideReactTreeTimeout.start(0, () => clearInsideReactTree(record));
}

// useDismiss.ts:239-258 (FLT-DIS-12).
function markPressStartedInsideReactTree(record: SessionRecord, event: MouseEvent): void {
  const state = dismissState(record);
  if (!record.open || event.button !== 0) {
    return;
  }
  // Only presses that start within the floating DOM subtree are inside, so nested portals do not suppress the parent.
  if (!contains(record.floatingElement, getTarget(event) as Element | null)) {
    return;
  }
  if (!state.pressStartedInside) {
    state.pressStartedInside = true;
    state.pressStartPrevented = false;
  }
}

// useDismiss.ts:260-274 (FLT-DIS-13).
function markInsidePressStartPrevented(record: SessionRecord, event: MouseEvent): void {
  const state = dismissState(record);
  if (!record.open || !event.defaultPrevented) {
    return;
  }
  if (state.pressStartedInside) {
    state.pressStartPrevented = true;
  }
}

// useDismiss.ts:169-178 (FLT-KEY-02, FLT-DIS-10): an open descendant in the session forest that does not bubble.
function hasBlockingChild(record: SessionRecord, bubbleKey: '__escapeKeyBubbles' | '__outsidePressBubbles'): boolean {
  const nodeId = record.dataRef.current.floatingContext?.nodeId;
  const children = getNodeChildren(record.tree.nodesRef.current, nodeId);
  return children.some((child) => child.context?.open && !child.context.dataRef?.current[bubbleKey]);
}

// useDismiss.ts:180-185.
function isEventWithinOwnElements(record: SessionRecord, event: Event): boolean {
  return isEventTargetWithin(event, record.floatingElement) || isEventTargetWithin(event, record.domReferenceElement);
}

/**
 * Closes the popup on Escape: an open, enabled, not composing popup with no open descendant that keeps Escape asks
 * `escape` on its session's twin, then prevents and stops the keydown as the verdict says (never stopping it when
 * Escape bubbles), and on Server reports the close itself. A keydown this session already handled, or a second Escape
 * before the next open arrives, is left alone.
 */
function closeOnEscapeKeyDown(record: SessionRecord, event: KeyboardEvent): void {
  const state = dismissState(record);
  const options = state.options;
  if (!record.open || options === null || options.escapeKey === false || event.key !== 'Escape') {
    return;
  }
  const handled = (event as EscapeMarkedEvent)[escapeHandled];
  if (handled?.has(record.token)) {
    return;
  }
  // Wait until IME is settled: Escape while composing closes the compose menu, not the popup. The flag covers Safari,
  // which fires `compositionend` before `keydown`; `isComposing` covers compositions that began while closed.
  if (state.isComposing || event.isComposing) {
    return;
  }
  // The innermost open popup takes the key (FLT-KEY-02): with Escape not bubbling, an open descendant in the session
  // forest that does not bubble it blocks this popup.
  const escapeKeyBubbles = normalizeBubbles({ escapeKey: options.bubblesEscapeKey, outsidePress: options.bubblesOutsidePress }).escapeKey;
  if (!escapeKeyBubbles && hasBlockingChild(record, '__escapeKeyBubbles')) {
    return;
  }
  const channel = record.channel;
  if (channel === null || state.disarmed) {
    return;
  }
  if (handled) {
    handled.add(record.token);
  } else {
    (event as EscapeMarkedEvent)[escapeHandled] = new Set([record.token]);
  }
  // On WebAssembly C# requests the close inside the decider; the pending request pairs the relay with this keydown.
  if (channel.synchronous) {
    record.pending = { open: false, reason: 'escape-key', event };
  }
  // An answer C# sends while the request is being made (a commit-path `update` inside the dispatch) already re-armed
  // the session, so the disarm below applies only when none arrived (design point 10).
  const answeredBefore = record.answered;
  const verdict = decide(channel, ESCAPE_POINT, event, 'prevent-and-stop');
  if (!channel.lastDecisionWasSynchronous) {
    requestOpenChange(record, channel, false, 'escape-key', event);
  }
  const accepted = verdict === 'prevent-and-stop' || verdict === 'prevent';
  if (accepted) {
    event.preventDefault();
  }
  if (!escapeKeyBubbles && (verdict === 'prevent-and-stop' || verdict === 'stop')) {
    event.stopPropagation();
  }
  // A close is pending until the relay brings the next open or C# answers the request; a cancel the verdict reports
  // leaves the session armed.
  if (accepted && record.answered === answeredBefore) {
    state.disarmed = true;
  }
}

// Dialog's `outsidePress` function (dialog/root/useDialogRoot.ts:43-80) as data: each predicate set narrows the presses
// that dismiss; with none set every press passes, as without a function.
function passesOutsidePressGuard(record: SessionRecord, event: MouseEvent | PointerEvent | TouchEvent): boolean {
  const options = dismissState(record).options;
  if (options === null) {
    return true;
  }
  // For mouse events, only the left button.
  if (options.primaryButtonOnly === true && 'button' in event && event.button !== 0) {
    return false;
  }
  // A single touch is the left button; on `touchend`, exactly one touch ended and no other finger is still down.
  if (options.singleTouch === true && 'touches' in event) {
    if (event.type === 'touchend') {
      if (event.changedTouches.length !== 1 || event.touches.length !== 0) {
        return false;
      }
    } else if (event.touches.length !== 1) {
      return false;
    }
  }
  if (options.topmost === true && !isTopmostOpenSession(record)) {
    return false;
  }
  // Only a press on one of the popup's own backdrops, or on an ancestor of the popup other than a portal node.
  const backdropIds = options.ownBackdropIds;
  if (backdropIds != null && backdropIds.length > 0) {
    const target = getTarget(event);
    if (!isElement(target)) {
      return false;
    }
    return (target.id !== '' && backdropIds.includes(target.id)) || (contains(target, record.floatingElement) && !target.hasAttribute(PORTAL_ATTRIBUTE));
  }
  return true;
}

function nodeNameOf(node: Node): string {
  return (node.nodeName || '').toLowerCase();
}

function isShadowRootNode(value: unknown): value is ShadowRoot {
  return typeof ShadowRoot !== 'undefined' && value instanceof ownerWindow(value as Node).ShadowRoot;
}

// @floating-ui/utils/dom's isLastTraversableNode and getParentNode.
function isLastTraversableNode(node: Node): boolean {
  return /^(html|body|#document)$/.test(nodeNameOf(node));
}

function getParentNodeOf(node: Node): Node {
  if (nodeNameOf(node) === 'html') {
    return node;
  }
  const result: Node =
    (node as Element).assignedSlot || node.parentNode || (isShadowRootNode(node) ? node.host : null) || ownerDocument(node).documentElement;
  return isShadowRootNode(result) ? result.host : result;
}

function addTargetEventListenerOnce<E extends Event>(event: E, listener: (event: E) => void): void {
  const target = getTarget(event);
  if (!target) {
    return;
  }
  const unsubscribe = listen(target, event.type, () => {
    listener(event);
    unsubscribe();
  });
}

/**
 * The session's half: while the popup is open and dismissal is on, listens on the document for Escape, compositions
 * and outside presses, and publishes the bubbling flags for the enclosing popups to read.
 */
function createSessionDismissController(context: HandleContext): Controller {
  const { record } = context;
  const state = dismissState(record);
  let effectKey: string | null = null;
  let effectCleanup: Cleanup | null = null;

  // A new hook instance starts with fresh refs.
  state.pressStartedInside = false;
  state.pressStartPrevented = false;
  state.suppressNextOutsideClick = false;
  state.sawPressWhileOpen = false;
  state.isComposing = false;
  state.currentPointerType = '';
  state.touchState = null;
  state.disarmed = false;

  // FLT-DIS-14: a same-batch close and reopen never renders closed, so only `openchange` observes that boundary; only
  // the closing half ends it, since a redundant open (hovering an inactive trigger) must not drop a press mid-gesture.
  const onOpenChange = (details: SessionOpenChangeDetails) => {
    if (!details.open) {
      state.sawPressWhileOpen = false;
    } else {
      // The relay brought the next open: the session may request a close again.
      state.disarmed = false;
    }
  };

  // useDismiss.ts:297-753: Base UI's effect, re-run when one of its inputs changes.
  const install = (options: DismissOptions | null): Cleanup => {
    if (!record.open || options === null) {
      // Reset in the effect body, not the cleanup, which also runs when a dependency changes mid-gesture.
      if (!record.open) {
        state.sawPressWhileOpen = false;
        state.isComposing = false;
        state.currentPointerType = '';
        state.touchState = null;
      }
      return () => clearInsideReactTree(record);
    }

    // FLT-DIS-15.
    const bubbles = normalizeBubbles({ escapeKey: options.bubblesEscapeKey, outsidePress: options.bubblesOutsidePress });
    record.dataRef.current.__escapeKeyBubbles = bubbles.escapeKey;
    record.dataRef.current.__outsidePressBubbles = bubbles.outsidePress;

    const preventedPressSuppressionTimeout = createTimer();
    const doc = ownerDocument(record.floatingElement);

    function handleCompositionStart() {
      state.compositionTimeout.clear();
      state.isComposing = true;
    }

    function handleCompositionEnd() {
      // Safari fires `compositionend` before `keydown`, so `isComposing` clears on the next tick (5 ms on WebKit, where
      // 0 or 1 ms do not work). Set here too: closing resets it while a composition can continue into the next open.
      state.isComposing = true;
      state.compositionTimeout.start(platform.engine.webkit ? 5 : 0, () => {
        state.isComposing = false;
      });
    }

    function suppressImmediateOutsideClickAfterPreventedStart() {
      state.suppressNextOutsideClick = true;
      // Firefox can emit the synthetic outside click in a later task after pointer lock exit.
      preventedPressSuppressionTimeout.start(0, () => {
        state.suppressNextOutsideClick = false;
      });
    }

    function resetPressStartState() {
      state.pressStartedInside = false;
      state.pressStartPrevented = false;
    }

    // FLT-API-02: resolved at each event; `touch` selects the touch type, any other pointer type the mouse type.
    function getOutsidePressEvent(): PressType {
      const type = state.currentPointerType;
      const computedType = type === 'pen' || !type ? 'mouse' : type;
      const value = state.options?.outsidePressEvent;
      return (computedType === 'touch' ? value?.touch : value?.mouse) ?? 'sloppy';
    }

    // FLT-DIS-03.
    function shouldIgnoreEvent(event: Event) {
      const computedOutsidePressEvent = getOutsidePressEvent();
      return (computedOutsidePressEvent === 'intentional' && event.type !== 'click') || (computedOutsidePressEvent === 'sloppy' && event.type === 'click');
    }

    // FLT-DIS-08.
    function isEventWithinFloatingTree(event: Event) {
      const nodeId = record.dataRef.current.floatingContext?.nodeId;
      const targetIsInsideChildren = getNodeChildren(record.tree.nodesRef.current, nodeId).some((node) =>
        isEventTargetWithin(event, node.context?.elements?.floating),
      );
      return isEventWithinOwnElements(record, event) || targetIsInsideChildren;
    }

    // FLT-ALG-17.
    function closeOnPressOutside(event: MouseEvent | PointerEvent | TouchEvent) {
      if (shouldIgnoreEvent(event)) {
        // A new press began outside the floating element and its trigger: clear any leftover drag-out suppression so
        // this press's eventual click can dismiss.
        if (event.type !== 'click' && !isEventWithinOwnElements(record, event)) {
          preventedPressSuppressionTimeout.clear();
          state.suppressNextOutsideClick = false;
        }
        clearInsideReactTree(record);
        return;
      }

      // FLT-DIS-04.
      if (record.dataRef.current.insideReactTree) {
        clearInsideReactTree(record);
        return;
      }

      const target = getTarget(event);
      const targetRoot = isElement(target) ? target.getRootNode() : null;
      const markers = Array.from(
        (isShadowRootNode(targetRoot) ? targetRoot : ownerDocument(record.floatingElement)).querySelectorAll(INERT_MARKER_SELECTOR),
      );

      // FLT-DIS-05: a press on another trigger does not close the popup.
      const triggers = record.triggerElements;
      if (isElement(target) && (triggers.hasElement(target) || triggers.hasMatchingElement((trigger) => contains(trigger, target)))) {
        return;
      }

      let targetRootAncestor = isElement(target) ? target : null;
      while (targetRootAncestor && !isLastTraversableNode(targetRootAncestor)) {
        const nextParent = getParentNodeOf(targetRootAncestor);
        if (isLastTraversableNode(nextParent) || !isElement(nextParent)) {
          break;
        }
        targetRootAncestor = nextParent;
      }

      // FLT-DIS-06: a press on a third-party element injected after the floating element rendered.
      if (
        markers.length &&
        isElement(target) &&
        !isRootElement(target) &&
        // A press on a direct ancestor (an overlay) is not one.
        !contains(target, record.floatingElement) &&
        // The target's top-level ancestor contains none of the markers: it was injected after the popup opened.
        markers.every((marker) => !contains(targetRootAncestor, marker))
      ) {
        return;
      }

      // FLT-DIS-07, FLT-I18N-04: a press on the target's scrollbar. Touch is skipped: scrollbars get no touch events on
      // most platforms.
      if (isHTMLElement(target) && !('touches' in event)) {
        const lastTraversableNode = isLastTraversableNode(target);
        const style = ownerWindow(target).getComputedStyle(target);
        const scrollRe = /auto|scroll/;
        const isScrollableX = lastTraversableNode || scrollRe.test(style.overflowX);
        const isScrollableY = lastTraversableNode || scrollRe.test(style.overflowY);
        const canScrollX = isScrollableX && target.clientWidth > 0 && target.scrollWidth > target.clientWidth;
        const canScrollY = isScrollableY && target.clientHeight > 0 && target.scrollHeight > target.clientHeight;
        const isRTL = style.direction === 'rtl';
        // A body or window scrollbar on the left side is rare and not checked; a backdrop matters more.
        const pressedVerticalScrollbar =
          canScrollY && (isRTL ? event.offsetX <= target.offsetWidth - target.clientWidth : event.offsetX > target.clientWidth);
        const pressedHorizontalScrollbar = canScrollX && event.offsetY > target.clientHeight;
        if (pressedVerticalScrollbar || pressedHorizontalScrollbar) {
          return;
        }
      }

      if (isEventWithinFloatingTree(event)) {
        return;
      }

      // Only `click` events reach this point in intentional mode (FLT-PTR-28, FLT-A11Y-05).
      if (getOutsidePressEvent() === 'intentional') {
        // Press-less clicks (keyboard, assistive technology, `element.click()`) report no click count; `isVirtualClick`
        // also catches the ones that do.
        if ((event as MouseEvent).detail !== 0 && !isVirtualClick(event as MouseEvent) && !state.sawPressWhileOpen) {
          return;
        }
        // A press that starts inside and ends outside gets one suppressed outside click (FLT-PTR-29). Checked after the
        // inside-target checks so inside clicks do not consume it.
        if (state.suppressNextOutsideClick) {
          preventedPressSuppressionTimeout.clear();
          state.suppressNextOutsideClick = false;
          return;
        }
      }

      // FLT-DIS-09.
      if (!passesOutsidePressGuard(record, event)) {
        return;
      }

      // FLT-DIS-10.
      if (hasBlockingChild(record, '__outsidePressBubbles')) {
        return;
      }

      // FLT-DIS-11. The disarm (ADR 04 Amendments 2026-10-27): one close request until the relay brings the next open,
      // so a press's compatibility `mousedown` does not request again before the commit arrives.
      if (!state.disarmed) {
        const answeredBefore = record.answered;
        requestOpenChange(record, context.channel, false, 'outside-press', event);
        if (record.answered === answeredBefore) {
          state.disarmed = true;
        }
      }
      clearInsideReactTree(record);
    }

    // FLT-PTR-26.
    function handlePointerDown(event: PointerEvent) {
      if (getOutsidePressEvent() !== 'sloppy' || event.pointerType === 'touch' || !record.open || isEventWithinOwnElements(record, event)) {
        return;
      }
      closeOnPressOutside(event);
    }

    // FLT-PTR-27.
    function handleTouchStart(event: TouchEvent) {
      if (getOutsidePressEvent() !== 'sloppy' || !record.open || isEventWithinOwnElements(record, event)) {
        return;
      }
      const touch = event.touches[0];
      if (touch) {
        state.touchState = {
          startTime: Date.now(),
          startX: touch.clientX,
          startY: touch.clientY,
          dismissOnTouchEnd: false,
          dismissOnMouseDown: true,
        };
        state.cancelDismissOnEndTimeout.start(1000, () => {
          if (state.touchState) {
            state.touchState.dismissOnTouchEnd = false;
            state.touchState.dismissOnMouseDown = false;
          }
        });
      }
    }

    function handleTouchStartCapture(event: TouchEvent) {
      state.currentPointerType = 'touch';
      addTargetEventListenerOnce(event, handleTouchStart);
    }

    // FLT-DIS-02: the capture listener only records; the decision runs on the target, after its own handlers and the
    // floating element's capture handlers.
    function closeOnPressOutsideCapture(event: PointerEvent | MouseEvent) {
      state.cancelDismissOnEndTimeout.clear();
      // Only `pointerdown` marks a press; `mousedown` is its compatibility event, and counting it would misattribute a
      // gesture that started before open.
      if (event.type === 'pointerdown') {
        // Only a primary press can produce a `click`.
        if (event.button === 0) {
          state.sawPressWhileOpen = true;
        }
        state.currentPointerType = (event as PointerEvent).pointerType;
      }
      if (event.type === 'mousedown' && state.touchState && !state.touchState.dismissOnMouseDown) {
        return;
      }
      addTargetEventListenerOnce(event, (targetEvent) => {
        if (targetEvent.type === 'pointerdown') {
          handlePointerDown(targetEvent as PointerEvent);
        } else {
          closeOnPressOutside(targetEvent as MouseEvent);
        }
      });
    }

    // FLT-PTR-28, FLT-PTR-29.
    function handlePressEndCapture(event: PointerEvent | MouseEvent) {
      // A canceled gesture produces no click. Not cleared on `pointerup`: the click fires after it.
      if (event.type === 'pointercancel') {
        state.sawPressWhileOpen = false;
      }
      if (!state.pressStartedInside) {
        return;
      }
      const pressStartedInsideDefaultPrevented = state.pressStartPrevented;
      resetPressStartState();
      if (getOutsidePressEvent() !== 'intentional') {
        return;
      }
      if (event.type === 'pointercancel') {
        if (pressStartedInsideDefaultPrevented) {
          suppressImmediateOutsideClickAfterPreventedStart();
        }
        return;
      }
      if (isEventWithinFloatingTree(event)) {
        return;
      }
      // A prevented pointerdown may generate no click, but Firefox may still emit one right after pointerup (a scrub
      // with pointer lock): suppress for one tick to absorb that synthetic click only.
      if (pressStartedInsideDefaultPrevented) {
        suppressImmediateOutsideClickAfterPreventedStart();
        return;
      }
      // Avoid suppressing when the guard ignores this target.
      if (!passesOutsidePressGuard(record, event)) {
        return;
      }
      preventedPressSuppressionTimeout.clear();
      state.suppressNextOutsideClick = true;
      clearInsideReactTree(record);
    }

    function handleTouchMove(event: TouchEvent) {
      if (getOutsidePressEvent() !== 'sloppy' || !state.touchState || isEventWithinOwnElements(record, event)) {
        return;
      }
      const touch = event.touches[0];
      if (!touch) {
        return;
      }
      const deltaX = Math.abs(touch.clientX - state.touchState.startX);
      const deltaY = Math.abs(touch.clientY - state.touchState.startY);
      const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);
      if (distance > 5) {
        state.touchState.dismissOnTouchEnd = true;
      }
      if (distance > 10) {
        closeOnPressOutside(event);
        state.cancelDismissOnEndTimeout.clear();
        state.touchState = null;
      }
    }

    function handleTouchMoveCapture(event: TouchEvent) {
      addTargetEventListenerOnce(event, handleTouchMove);
    }

    function handleTouchEnd(event: TouchEvent) {
      if (getOutsidePressEvent() !== 'sloppy' || !state.touchState || isEventWithinOwnElements(record, event)) {
        return;
      }
      if (state.touchState.dismissOnTouchEnd) {
        closeOnPressOutside(event);
      }
      state.cancelDismissOnEndTimeout.clear();
      state.touchState = null;
    }

    function handleTouchEndCapture(event: TouchEvent) {
      addTargetEventListenerOnce(event, handleTouchEnd);
    }

    // FLT-DIS-01, FLT-DIS-16.
    const unsubscribe = mergeCleanups(
      options.escapeKey !== false &&
        mergeCleanups(
          listen(doc, 'keydown', (event: KeyboardEvent) => closeOnEscapeKeyDown(record, event)),
          listen(doc, 'compositionstart', handleCompositionStart),
          listen(doc, 'compositionend', handleCompositionEnd),
        ),
      options.outsidePress !== false &&
        mergeCleanups(
          listen(doc, 'click', closeOnPressOutsideCapture, true),
          listen(doc, 'pointerdown', closeOnPressOutsideCapture, true),
          listen(doc, 'pointerup', handlePressEndCapture, true),
          listen(doc, 'pointercancel', handlePressEndCapture, true),
          listen(doc, 'mousedown', closeOnPressOutsideCapture, true),
          listen(doc, 'mouseup', handlePressEndCapture, true),
          listen(doc, 'touchstart', handleTouchStartCapture, { capture: true, passive: true }),
          listen(doc, 'touchmove', handleTouchMoveCapture, { capture: true, passive: true }),
          listen(doc, 'touchend', handleTouchEndCapture, { capture: true, passive: true }),
        ),
    );

    return () => {
      unsubscribe();
      preventedPressSuppressionTimeout.clear();
      resetPressStartState();
      state.suppressNextOutsideClick = false;
    };
  };

  const sync = () => {
    const options = context.options.session?.dismiss ?? null;
    state.options = options;
    state.owner = context;
    const bubbles = normalizeBubbles({ escapeKey: options?.bubblesEscapeKey, outsidePress: options?.bubblesOutsidePress });
    // The effect's inputs that can change here (useDismiss.ts:754-773); the stable callbacks read `state.options`.
    const key = [record.open, options !== null, options?.escapeKey !== false, options?.outsidePress !== false, bubbles.escapeKey, bubbles.outsidePress].join();
    if (key === effectKey) {
      return;
    }
    effectKey = key;
    effectCleanup?.();
    effectCleanup = install(options);
  };

  record.events.on('openchange', onOpenChange);
  sync();

  return {
    update: sync,
    dispose() {
      record.events.off('openchange', onOpenChange);
      effectCleanup?.();
      effectCleanup = null;
      effectKey = null;
      // Not cleared by the listeners' cleanup, which can run mid-press; in a closed shadow root the marker is the only
      // inside-press signal (useDismiss.ts:293-295).
      clearInsideReactTree(record);
      state.cancelDismissOnEndTimeout.clear();
      state.compositionTimeout.clear();
      if (state.owner === context) {
        state.options = null;
        state.owner = null;
      }
    },
  };
}

/**
 * The floating element's half: Escape on the floating element, and the inside-press marks of its capture and bubble
 * handlers, run for its session and for every enclosing session.
 */
function createFloatingDismissController(context: HandleContext): Controller {
  const { record } = context;
  const element = context.element as HTMLElement;

  // React's synthetic events bubble through the React tree across portals, so a press inside a nested popup's floating
  // element also runs the floating handlers of every enclosing popup (each Popover Root has its own FloatingTree in
  // Base UI). The forest stands in: the handlers run for this session and its ancestors by `parentToken`, each one
  // whose dismissal is on (Base UI's prop bag is empty while disabled).
  const enabledSessionsUp = (): SessionRecord[] => {
    const chain: SessionRecord[] = [];
    let current: SessionRecord | undefined = record;
    while (current && !chain.includes(current)) {
      chain.push(current);
      current = current.parentToken === null ? undefined : sessions.get(current.parentToken);
    }
    return chain.filter((session) => dismissState(session).options !== null);
  };

  const onKeyDown = (event: KeyboardEvent) => closeOnEscapeKeyDown(record, event);

  // `onPointerDown`/`onMouseDown`: the latter may be blocked when `preventDefault()` is called in the former (a
  // NumberField scrub area).
  const onPressStart = (event: MouseEvent) => {
    for (const session of enabledSessionsUp()) {
      markInsidePressStartPrevented(session, event);
    }
  };

  const onInsideCapture = () => {
    for (const session of enabledSessionsUp()) {
      markInsideReactTree(session);
    }
  };

  const onPressStartCapture = (event: MouseEvent) => {
    for (const session of enabledSessionsUp()) {
      markInsideReactTree(session);
      markPressStartedInsideReactTree(session, event);
    }
  };

  const cleanup = mergeCleanups(
    listen(element, 'keydown', onKeyDown),
    listen(element, 'pointerdown', onPressStart),
    listen(element, 'mousedown', onPressStart),
    listen(element, 'click', onInsideCapture, true),
    listen(element, 'mousedown', onPressStartCapture, true),
    listen(element, 'pointerdown', onPressStartCapture, true),
    listen(element, 'mouseup', onInsideCapture, true),
    listen(element, 'touchend', onInsideCapture, true),
    listen(element, 'touchmove', onInsideCapture, true),
  );
  return { dispose: cleanup };
}

/**
 * The trigger's half: Escape on the trigger, and the reference press, which requests a close with reason
 * `trigger-press` on each `pointerdown` and `click` while the trigger's `referencePress` is set.
 */
function createReferenceDismissController(context: HandleContext): Controller {
  const { record, channel } = context;
  const element = context.element as HTMLElement;

  // useDismiss.ts:187-199 (FLT-PTR-30, FLT-API-07): the getter is read at each press; closes carry no trigger.
  const closeOnReferencePress = (event: Event) => {
    if (dismissState(record).options === null || context.options.trigger?.referencePress !== true) {
      return;
    }
    requestOpenChange(record, channel, false, 'trigger-press', event);
  };

  const onKeyDown = (event: KeyboardEvent) => closeOnEscapeKeyDown(record, event);

  const cleanup = mergeCleanups(
    listen(element, 'keydown', onKeyDown),
    listen(element, 'pointerdown', closeOnReferencePress),
    listen(element, 'click', closeOnReferencePress),
  );
  return { dispose: cleanup };
}

// #endregion dismiss

// ---------------------------------------------------------------------------------------------------------------
// #region click: useClick (FLT-PTR-01..06, FLT-ALG-16) and the `press` point
// ---------------------------------------------------------------------------------------------------------------

/** A next-frame callback over `transitions.ts`'s frame scheduler (Base UI's `useAnimationFrame`). */
interface FrameRequest {
  request(callback: () => void): void;
  cancel(): void;
}

function createFrameRequest(): FrameRequest {
  let id: number | undefined;
  return {
    request(callback) {
      if (id !== undefined) {
        cancelFrame(id);
      }
      id = requestFrame(() => {
        id = undefined;
        callback();
      });
    },
    cancel() {
      if (id !== undefined) {
        cancelFrame(id);
        id = undefined;
      }
    },
  };
}

// popover/store/PopoverStore.ts:161-167, internals/constants.ts:4 (PATIENT_CLICK_THRESHOLD).
const PATIENT_CLICK_THRESHOLD = 500;

/** How a click happened (Base UI's `InteractionType`); `''` when unknown. */
export type PressInteractionType = InteractionType;

// Popover's patient-click window (FLT-API-06), per session: a hover open or close keeps `stickIfOpen` on for 500 ms, then
// off; read at press time and never sent to C#. It starts when the relay brings the hover change, as the store applies it.
interface PatientClickState {
  stickIfOpen: boolean;
  timer: Timer;
  listening: number;
  onOpenChange: (details: SessionOpenChangeDetails) => void;
}

function patientClickState(record: SessionRecord): PatientClickState {
  return sessionSlot(record, 'click.patient', () => {
    const state: PatientClickState = {
      stickIfOpen: true,
      timer: createTimer(),
      listening: 0,
      onOpenChange: (details) => {
        if (details.reason === 'trigger-hover') {
          state.stickIfOpen = true;
          state.timer.start(PATIENT_CLICK_THRESHOLD, () => {
            state.stickIfOpen = false;
          });
        }
      },
    };
    return state;
  });
}

// FLT-ALG-16
/**
 * useClick on a trigger (useClick.ts) with the decision point `press`: the trigger computes the next open value from the
 * relayed state and asks `press` on its own twin, fallback `run`. On WebAssembly it first hands the press
 * over (`OnPressing`) and C# runs the consumer's declined `onclick` and the request inside the dispatch; on Server it
 * reports the press (`OnPress`) with its snapshot and interaction type, and C# runs both a round trip later.
 */
function createClickController(context: HandleContext): Controller {
  const { record, channel, twinReference } = context;
  const element = context.element as HTMLElement;
  const frame = createFrameRequest();
  const touchOpenTimeout = createTimer();
  let pointerType: 'mouse' | 'pen' | 'touch' | 'virtual' | string | undefined;
  // The enhanced click handler's last pointerdown type (PSH-ALG-19), kept apart from useClick's own record.
  let lastPressPointerType: PressInteractionType = '';
  let cleanup: Cleanup | null = null;
  let patient: PatientClickState | null = null;

  const clickOptions = (): ClickOptions | null => context.options.trigger?.click ?? null;

  // PSH-ALG-19 (utils/useOpenInteractionType.ts:18-27): `button.ts`'s classification (design point 5, settled by the
  // orchestrator 2026-10-27); iOS's empty type is touch.
  const classify = (event: MouseEvent): PressInteractionType => classifyClick(event, lastPressPointerType) || (platform.os.ios ? 'touch' : '');

  const press = (nextOpen: boolean, event: MouseEvent, target: HTMLElement) => {
    const options = clickOptions();
    const reason = options?.reason ?? 'trigger-press';
    const payload = { open: nextOpen, reason, triggerId: triggerIdOf(record, target === element ? element : target), interactionType: classify(event) };
    const request: PendingRequest = { open: nextOpen, reason, event };
    record.pending = request;
    if (nextOpen) {
      recordOptimisticOpenEvent(record, request);
    }
    if (channel.synchronous) {
      twinReference.invokeMethod<void>('OnPressing', JSON.stringify(payload));
      decide(channel, PRESS_POINT, event, 'run');
      return;
    }
    decide(channel, PRESS_POINT, event, 'run');
    channel.report('OnPress', { ...payload, event: eventInfo(event) });
  };

  // FLT-PTR-04: a touch open waits `touchOpenDelay`; a close never waits.
  const setOpenWithTouchDelay = (nextOpen: boolean, event: MouseEvent, target: HTMLElement, type: string | undefined) => {
    const touchOpenDelay = clickOptions()?.touchOpenDelay ?? 0;
    if (nextOpen && type === 'touch' && touchOpenDelay > 0) {
      touchOpenTimeout.start(touchOpenDelay, () => press(true, event, target));
    } else {
      press(nextOpen, event, target);
    }
  };

  // FLT-ALG-16: the next open value.
  const getNextOpen = (open: boolean, currentTarget: EventTarget | null, isClickLikeOpenEvent: (type: string | undefined) => boolean) => {
    const options = clickOptions();
    const openEvent = record.dataRef.current.openEvent;
    const hasClickedOnInactiveTrigger = record.domReferenceElement !== currentTarget;
    if (open && hasClickedOnInactiveTrigger) {
      return true;
    }
    if (!open) {
      return true;
    }
    if (options?.toggle === false) {
      return true;
    }
    const stickIfOpen = options?.patientClick ? (patient?.stickIfOpen ?? true) : options?.stickIfOpen !== false;
    if (openEvent && stickIfOpen) {
      return !isClickLikeOpenEvent(openEvent.type);
    }
    return false;
  };

  const onPointerDown = (event: PointerEvent) => {
    pointerType = isMouseLikePointerType(event.pointerType, true) && isVirtualPointerEvent(event) ? 'virtual' : event.pointerType;
    if (!event.defaultPrevented) {
      lastPressPointerType = event.pointerType as PressInteractionType;
    }
  };

  // FLT-PTR-02: the mousedown paths (no M4 consumer; built and proven at L3).
  const onMouseDown = (event: MouseEvent) => {
    const options = clickOptions();
    const eventOption = options?.event ?? 'click';
    const type = pointerType;
    if (event.button !== 0 || eventOption === 'click' || (isMouseLikePointerType(type, true) && options?.ignoreMouse)) {
      return;
    }
    const nextOpen = getNextOpen(record.open, element, (openEventType) => openEventType === 'click' || openEventType === 'mousedown');
    const target = getTarget(event);
    const isTypeable = isTypeableElement(target);
    if (isTypeable || type === 'virtual') {
      setOpenWithTouchDelay(nextOpen, event, (isTypeable ? target : element) as HTMLElement, type);
      return;
    }
    frame.request(() => setOpenWithTouchDelay(nextOpen, event, element, type));
  };

  // FLT-PTR-03.
  const onClick = (event: MouseEvent) => {
    const options = clickOptions();
    const eventOption = options?.event ?? 'click';
    if (eventOption === 'mousedown-only') {
      return;
    }
    const type = pointerType;
    if (eventOption === 'mousedown' && type) {
      pointerType = undefined;
      return;
    }
    if (isMouseLikePointerType(type, true) && options?.ignoreMouse) {
      return;
    }
    const nextOpen = getNextOpen(
      record.open,
      element,
      (openEventType) => openEventType === 'click' || openEventType === 'mousedown' || openEventType === 'keydown' || openEventType === 'keyup',
    );
    setOpenWithTouchDelay(nextOpen, event, element, type);
    lastPressPointerType = '';
  };

  // FLT-KEY-08.
  const onKeyDown = () => {
    pointerType = undefined;
  };

  const install = () => {
    if (cleanup !== null || clickOptions() === null) {
      return;
    }
    patient = patientClickState(record);
    if (patient.listening === 0) {
      record.events.on('openchange', patient.onOpenChange);
    }
    patient.listening += 1;
    const state = patient;
    cleanup = mergeCleanups(
      listen(element, 'pointerdown', onPointerDown),
      listen(element, 'mousedown', onMouseDown),
      listen(element, 'click', onClick),
      listen(element, 'keydown', onKeyDown),
      () => {
        state.listening -= 1;
        if (state.listening === 0) {
          record.events.off('openchange', state.onOpenChange);
          state.timer.clear();
        }
      },
    );
  };

  const uninstall = () => {
    cleanup?.();
    cleanup = null;
    frame.cancel();
    touchOpenTimeout.clear();
  };

  install();
  return {
    update() {
      if (clickOptions() === null) {
        uninstall();
      } else {
        install();
      }
    },
    dispose: uninstall,
  };
}

// #endregion click

// ---------------------------------------------------------------------------------------------------------------
// #region hover: the hover pair, the shared hover state and safePolygon (FLT-PTR-11..25, FLT-ALG-15, FLT-ALG-24)
// ---------------------------------------------------------------------------------------------------------------

// Tooltip's private marker of an enabled Trigger (tooltip/trigger/TooltipTrigger.tsx:31), rendered by C#.
const TOOLTIP_TRIGGER_IDENTIFIER = 'data-base-ui-tooltip-trigger';

// FLT-ALG-15: safePolygon.ts:10-12.
const CURSOR_SPEED_THRESHOLD = 0.1;
const CURSOR_SPEED_THRESHOLD_SQUARED = CURSOR_SPEED_THRESHOLD * CURSOR_SPEED_THRESHOLD;
const POLYGON_BUFFER = 0.5;

/**
 * Resolves a hover delay where it is used: a pointer type that is not mouse-like has none.
 * @param value The delay in milliseconds, or nothing.
 * @param pointerType The recorded pointer type.
 * @returns 0 for a recorded pointer type that is not mouse-like; otherwise the delay, or `undefined` without one.
 */
function getHoverDelay(value: number | null | undefined, pointerType?: string): number | undefined {
  // FLT-API-03: useHoverShared.ts:31-57 (`getDelay`). The port's delays are numbers, resolved at each use (FLT-API-04).
  if (pointerType != null && !isMouseLikePointerType(pointerType)) {
    return 0;
  }
  return value ?? undefined;
}

/**
 * Determines whether the popup counts as opened by a click for the hover hooks.
 * @param openEventType The type of the recorded open event.
 * @param interactedInside Whether a nested popup's trigger was pressed inside the floating element.
 * @returns `true` after a press inside, or for a `click` or `mousedown` open.
 */
function isClickLikeOpenEvent(openEventType: string | undefined, interactedInside: boolean): boolean {
  // FLT-STA-06: useHoverShared.ts:66-68.
  return interactedInside || openEventType === 'click' || openEventType === 'mousedown';
}

/**
 * Determines whether an open event is a hover.
 * @param openEventType The type of the recorded open event.
 * @returns `true` for a type that contains `mouse` other than `mousedown`.
 */
function isHoverOpenEvent(openEventType: string | undefined): boolean {
  // FLT-STA-05: useHoverShared.ts:70-72.
  return openEventType !== undefined && openEventType.includes('mouse') && openEventType !== 'mousedown';
}

// ---------------------------------------------------------------------------------------------------------------
// safePolygon (FLT-ALG-15, FLT-EDGE-13): safePolygon.ts
// ---------------------------------------------------------------------------------------------------------------

/** The options a close strategy was created with. */
export interface SafePolygonOptions {
  /** Whether pointer events outside the trigger and the floating element are blocked while the popup is hover-open. */
  blockPointerEvents?: boolean | undefined;
}

/** What a close strategy is invoked with when the pointer leaves a trigger. */
export interface HandleCloseContext {
  x: number | null;
  y: number | null;
  placement: string | null | undefined;
  elements: { readonly domReference: Element | null; readonly floating: HTMLElement | null };
  onClose: () => void;
  nodeId?: string | undefined;
  tree?: SessionTree | null | undefined;
  leave?: boolean | undefined;
}

/** A close strategy: invoked with the close context, it returns the `mousemove` handler that decides the close. */
export interface HandleClose {
  (context: HandleCloseContext): (event: MouseEvent) => void;
  __options?: SafePolygonOptions | undefined;
}

function hasIntersectingEdge(pointX: number, pointY: number, xi: number, yi: number, xj: number, yj: number): boolean {
  return yi >= pointY !== yj >= pointY && pointX <= ((xj - xi) * (pointY - yi)) / (yj - yi) + xi;
}

function isPointInQuadrilateral(
  pointX: number,
  pointY: number,
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  x3: number,
  y3: number,
  x4: number,
  y4: number,
): boolean {
  let isInsideValue = false;
  if (hasIntersectingEdge(pointX, pointY, x1, y1, x2, y2)) {
    isInsideValue = !isInsideValue;
  }
  if (hasIntersectingEdge(pointX, pointY, x2, y2, x3, y3)) {
    isInsideValue = !isInsideValue;
  }
  if (hasIntersectingEdge(pointX, pointY, x3, y3, x4, y4)) {
    isInsideValue = !isInsideValue;
  }
  if (hasIntersectingEdge(pointX, pointY, x4, y4, x1, y1)) {
    isInsideValue = !isInsideValue;
  }
  return isInsideValue;
}

function isInsideRect(pointX: number, pointY: number, rect: { x: number; y: number; width: number; height: number }): boolean {
  return pointX >= rect.x && pointX <= rect.x + rect.width && pointY >= rect.y && pointY <= rect.y + rect.height;
}

function isInsideAxisAlignedRect(pointX: number, pointY: number, x1: number, y1: number, x2: number, y2: number): boolean {
  const minX = Math.min(x1, x2);
  const maxX = Math.max(x1, x2);
  const minY = Math.min(y1, y2);
  const maxY = Math.max(y1, y2);
  return pointX >= minX && pointX <= maxX && pointY >= minY && pointY <= maxY;
}

/**
 * Creates the safe-polygon close strategy: once the pointer leaves the trigger, the popup stays open while the pointer
 * travels through the trough between the two elements or the triangle from the leave point to the popup's far edge,
 * and closes when it leaves that area, rests outside it, or lingers in the triangle for 40 ms without landing.
 * @param options The strategy's options.
 * @returns The strategy; each invocation starts a fresh traversal.
 */
export function safePolygon(options: SafePolygonOptions = {}): HandleClose {
  const { blockPointerEvents = false } = options;
  const timeout = createTimer();

  const fn: HandleClose = ({ x, y, placement, elements, onClose, nodeId, tree }) => {
    const side = placement?.split('-')[0];
    // FLT-EDGE-13: fresh traversal state per invocation (safePolygon.ts:94-99).
    let hasLanded = false;
    let lastX: number | null = null;
    let lastY: number | null = null;
    let lastCursorTime = typeof performance !== 'undefined' ? performance.now() : 0;

    function isCursorMovingSlowly(nextX: number, nextY: number): boolean {
      const currentTime = performance.now();
      const elapsedTime = currentTime - lastCursorTime;

      if (lastX === null || lastY === null || elapsedTime === 0) {
        lastX = nextX;
        lastY = nextY;
        lastCursorTime = currentTime;
        return false;
      }

      const deltaX = nextX - lastX;
      const deltaY = nextY - lastY;
      const distanceSquared = deltaX * deltaX + deltaY * deltaY;
      const thresholdSquared = elapsedTime * elapsedTime * CURSOR_SPEED_THRESHOLD_SQUARED;

      lastX = nextX;
      lastY = nextY;
      lastCursorTime = currentTime;

      return distanceSquared < thresholdSquared;
    }

    function close(): void {
      timeout.clear();
      onClose();
    }

    return function onMouseMove(event: MouseEvent): void {
      timeout.clear();

      const domReference = elements.domReference;
      const floating = elements.floating;
      if (!domReference || !floating || side == null || x == null || y == null) {
        return;
      }

      const { clientX, clientY } = event;
      const target = getTarget(event) as Element | null;
      const isLeave = event.type === 'mouseleave';
      const isOverFloatingEl = contains(floating, target);
      const isOverReferenceEl = contains(domReference, target);

      if (isOverFloatingEl) {
        hasLanded = true;

        if (!isLeave) {
          return;
        }
      }

      if (isOverReferenceEl) {
        hasLanded = false;

        if (!isLeave) {
          hasLanded = true;
          return;
        }
      }

      // Prevent overlapping floating element from being stuck in an open-close
      // loop: https://github.com/floating-ui/floating-ui/issues/1910
      if (isLeave && isElement(event.relatedTarget) && contains(floating, event.relatedTarget)) {
        return;
      }

      // FLT-ALG-20 over the session forest (FLT-STR-08): the open descendants of the popup's node.
      function hasOpenChildNode(): boolean {
        return Boolean(tree && getNodeChildren(tree.nodesRef.current, nodeId).length > 0);
      }

      function closeIfNoOpenChild(): void {
        if (!hasOpenChildNode()) {
          close();
        }
      }

      // If any nested child is open, abort.
      if (hasOpenChildNode()) {
        return;
      }

      const refRect = domReference.getBoundingClientRect();
      const rect = floating.getBoundingClientRect();
      const cursorLeaveFromRight = x > rect.right - rect.width / 2;
      const cursorLeaveFromBottom = y > rect.bottom - rect.height / 2;
      const isFloatingWider = rect.width > refRect.width;
      const isFloatingTaller = rect.height > refRect.height;
      const left = (isFloatingWider ? refRect : rect).left;
      const right = (isFloatingWider ? refRect : rect).right;
      const top = (isFloatingTaller ? refRect : rect).top;
      const bottom = (isFloatingTaller ? refRect : rect).bottom;

      // If the pointer is leaving from the opposite side, the "buffer" logic
      // creates a point where the floating element remains open, but should be
      // ignored.
      // A constant of 1 handles floating point rounding errors.
      if (
        (side === 'top' && y >= refRect.bottom - 1) ||
        (side === 'bottom' && y <= refRect.top + 1) ||
        (side === 'left' && x >= refRect.right - 1) ||
        (side === 'right' && x <= refRect.left + 1)
      ) {
        closeIfNoOpenChild();
        return;
      }

      // Ignore when the cursor is within the rectangular trough between the
      // two elements. Since the triangle is created from the cursor point,
      // which can start beyond the ref element's edge, traversing back and
      // forth from the ref to the floating element can cause it to close. This
      // ensures it always remains open in that case.
      let isInsideTroughRect = false;

      switch (side) {
        case 'top':
          isInsideTroughRect = isInsideAxisAlignedRect(clientX, clientY, left, refRect.top + 1, right, rect.bottom - 1);
          break;
        case 'bottom':
          isInsideTroughRect = isInsideAxisAlignedRect(clientX, clientY, left, rect.top + 1, right, refRect.bottom - 1);
          break;
        case 'left':
          isInsideTroughRect = isInsideAxisAlignedRect(clientX, clientY, rect.right - 1, bottom, refRect.left + 1, top);
          break;
        case 'right':
          isInsideTroughRect = isInsideAxisAlignedRect(clientX, clientY, refRect.right - 1, bottom, rect.left + 1, top);
          break;
        default:
      }

      if (isInsideTroughRect) {
        return;
      }

      if (hasLanded && !isInsideRect(clientX, clientY, refRect)) {
        closeIfNoOpenChild();
        return;
      }

      if (!isLeave && isCursorMovingSlowly(clientX, clientY)) {
        closeIfNoOpenChild();
        return;
      }

      let isInsidePolygon = false;

      switch (side) {
        case 'top': {
          const cursorXOffset = isFloatingWider ? POLYGON_BUFFER / 2 : POLYGON_BUFFER * 4;
          const cursorPointOneX = isFloatingWider ? x + cursorXOffset : cursorLeaveFromRight ? x + cursorXOffset : x - cursorXOffset;
          const cursorPointTwoX = isFloatingWider ? x - cursorXOffset : cursorLeaveFromRight ? x + cursorXOffset : x - cursorXOffset;
          const cursorPointY = y + POLYGON_BUFFER + 1;

          const commonYLeft = cursorLeaveFromRight ? rect.bottom - POLYGON_BUFFER : isFloatingWider ? rect.bottom - POLYGON_BUFFER : rect.top;
          const commonYRight = cursorLeaveFromRight ? (isFloatingWider ? rect.bottom - POLYGON_BUFFER : rect.top) : rect.bottom - POLYGON_BUFFER;

          isInsidePolygon = isPointInQuadrilateral(
            clientX,
            clientY,
            cursorPointOneX,
            cursorPointY,
            cursorPointTwoX,
            cursorPointY,
            rect.left,
            commonYLeft,
            rect.right,
            commonYRight,
          );
          break;
        }
        case 'bottom': {
          const cursorXOffset = isFloatingWider ? POLYGON_BUFFER / 2 : POLYGON_BUFFER * 4;
          const cursorPointOneX = isFloatingWider ? x + cursorXOffset : cursorLeaveFromRight ? x + cursorXOffset : x - cursorXOffset;
          const cursorPointTwoX = isFloatingWider ? x - cursorXOffset : cursorLeaveFromRight ? x + cursorXOffset : x - cursorXOffset;
          const cursorPointY = y - POLYGON_BUFFER;

          const commonYLeft = cursorLeaveFromRight ? rect.top + POLYGON_BUFFER : isFloatingWider ? rect.top + POLYGON_BUFFER : rect.bottom;
          const commonYRight = cursorLeaveFromRight ? (isFloatingWider ? rect.top + POLYGON_BUFFER : rect.bottom) : rect.top + POLYGON_BUFFER;

          isInsidePolygon = isPointInQuadrilateral(
            clientX,
            clientY,
            cursorPointOneX,
            cursorPointY,
            cursorPointTwoX,
            cursorPointY,
            rect.left,
            commonYLeft,
            rect.right,
            commonYRight,
          );
          break;
        }
        case 'left': {
          const cursorYOffset = isFloatingTaller ? POLYGON_BUFFER / 2 : POLYGON_BUFFER * 4;
          const cursorPointOneY = isFloatingTaller ? y + cursorYOffset : cursorLeaveFromBottom ? y + cursorYOffset : y - cursorYOffset;
          const cursorPointTwoY = isFloatingTaller ? y - cursorYOffset : cursorLeaveFromBottom ? y + cursorYOffset : y - cursorYOffset;
          const cursorPointX = x + POLYGON_BUFFER + 1;

          const commonXTop = cursorLeaveFromBottom ? rect.right - POLYGON_BUFFER : isFloatingTaller ? rect.right - POLYGON_BUFFER : rect.left;
          const commonXBottom = cursorLeaveFromBottom ? (isFloatingTaller ? rect.right - POLYGON_BUFFER : rect.left) : rect.right - POLYGON_BUFFER;

          isInsidePolygon = isPointInQuadrilateral(
            clientX,
            clientY,
            commonXTop,
            rect.top,
            commonXBottom,
            rect.bottom,
            cursorPointX,
            cursorPointOneY,
            cursorPointX,
            cursorPointTwoY,
          );
          break;
        }
        case 'right': {
          const cursorYOffset = isFloatingTaller ? POLYGON_BUFFER / 2 : POLYGON_BUFFER * 4;
          const cursorPointOneY = isFloatingTaller ? y + cursorYOffset : cursorLeaveFromBottom ? y + cursorYOffset : y - cursorYOffset;
          const cursorPointTwoY = isFloatingTaller ? y - cursorYOffset : cursorLeaveFromBottom ? y + cursorYOffset : y - cursorYOffset;
          const cursorPointX = x - POLYGON_BUFFER;

          const commonXTop = cursorLeaveFromBottom ? rect.left + POLYGON_BUFFER : isFloatingTaller ? rect.left + POLYGON_BUFFER : rect.right;
          const commonXBottom = cursorLeaveFromBottom ? (isFloatingTaller ? rect.left + POLYGON_BUFFER : rect.right) : rect.left + POLYGON_BUFFER;

          isInsidePolygon = isPointInQuadrilateral(
            clientX,
            clientY,
            cursorPointX,
            cursorPointOneY,
            cursorPointX,
            cursorPointTwoY,
            commonXTop,
            rect.top,
            commonXBottom,
            rect.bottom,
          );
          break;
        }
        default:
      }

      if (!isInsidePolygon) {
        closeIfNoOpenChild();
      } else if (!hasLanded) {
        timeout.start(40, closeIfNoOpenChild);
      }
    };
  };

  // FLT-PTR-19: safePolygon.ts:444-448.
  fn.__options = {
    ...options,
    blockPointerEvents,
  };

  return fn;
}

// ---------------------------------------------------------------------------------------------------------------
// The shared hover state and the pointer-events mutation (FLT-STR-05, FLT-DOM-19): useHoverInteractionSharedState.ts
// ---------------------------------------------------------------------------------------------------------------

/** What a hover state's last pointer-events mutation touched. */
interface PointerEventsMutationState {
  performedPointerEventsMutation: boolean;
  pointerEventsScopeElement: HTMLElement | SVGSVGElement | null;
  pointerEventsReferenceElement: HTMLElement | SVGSVGElement | null;
  pointerEventsFloatingElement: HTMLElement | null;
}

/** The hover state of a session, shared by its trigger and floating hover controllers. */
interface HoverInteraction extends PointerEventsMutationState {
  pointerType: string | undefined;
  interactedInside: boolean;
  handler: ((event: MouseEvent) => void) | undefined;
  blockMouseMove: boolean;
  restTimeoutPending: boolean;
  openChangeTimeout: Timer;
  restTimeout: Timer;
  handleCloseOptions: SafePolygonOptions | undefined;
  dispose(): void;
}

// The owner of a scope element's mutation, kept on the element (Base UI keeps a module-level WeakMap,
// useHoverInteractionSharedState.ts:47-50; the port keeps no module-level registry).
const pointerEventsOwner = Symbol('blazix.interactions.pointerEventsOwner');

type PointerEventsScope = (HTMLElement | SVGSVGElement) & { [pointerEventsOwner]?: PointerEventsMutationState | undefined };

/**
 * Removes a hover state's inline `pointer-events` from its scope, anchor and floating element, unless a newer hover
 * state owns the scope by now, and forgets the mutation.
 * @param instance The hover state.
 */
function clearSafePolygonPointerEventsMutation(instance: PointerEventsMutationState): void {
  if (!instance.performedPointerEventsMutation) {
    return;
  }

  const scopeElement = instance.pointerEventsScopeElement as PointerEventsScope | null;

  if (scopeElement && scopeElement[pointerEventsOwner] === instance) {
    instance.pointerEventsScopeElement?.style.removeProperty('pointer-events');
    instance.pointerEventsReferenceElement?.style.removeProperty('pointer-events');
    instance.pointerEventsFloatingElement?.style.removeProperty('pointer-events');
    delete scopeElement[pointerEventsOwner];
  }

  instance.performedPointerEventsMutation = false;
  instance.pointerEventsScopeElement = null;
  instance.pointerEventsReferenceElement = null;
  instance.pointerEventsFloatingElement = null;
}

/**
 * Blocks pointer events on a scope element except on the anchor and the floating element, taking the scope over from
 * its previous owner.
 * @param instance The hover state.
 * @param options The scope, the anchor and the floating element.
 */
function applySafePolygonPointerEventsMutation(
  instance: PointerEventsMutationState,
  options: { scopeElement: HTMLElement | SVGSVGElement; referenceElement: HTMLElement | SVGSVGElement; floatingElement: HTMLElement },
): void {
  const { referenceElement, floatingElement } = options;
  const scopeElement = options.scopeElement as PointerEventsScope;

  const existingOwner = scopeElement[pointerEventsOwner];
  if (existingOwner && existingOwner !== instance) {
    clearSafePolygonPointerEventsMutation(existingOwner);
  }

  clearSafePolygonPointerEventsMutation(instance);
  instance.performedPointerEventsMutation = true;
  instance.pointerEventsScopeElement = scopeElement;
  instance.pointerEventsReferenceElement = referenceElement;
  instance.pointerEventsFloatingElement = floatingElement;
  scopeElement[pointerEventsOwner] = instance;

  scopeElement.style.pointerEvents = 'none';
  referenceElement.style.pointerEvents = 'auto';
  floatingElement.style.pointerEvents = 'auto';
}

/**
 * Returns the session's hover state, creating it on first use.
 * @param record The session.
 * @returns The state every hover controller of the session shares.
 */
function getHoverInteraction(record: SessionRecord): HoverInteraction {
  // FLT-STR-05: useHoverInteractionSharedState.ts:103-116, kept in `dataRef.current.hoverInteractionState`.
  const data = record.dataRef.current;
  let instance = data.hoverInteractionState as HoverInteraction | undefined;
  if (instance === undefined) {
    const created: HoverInteraction = {
      pointerType: undefined,
      interactedInside: false,
      handler: undefined,
      blockMouseMove: true,
      performedPointerEventsMutation: false,
      pointerEventsScopeElement: null,
      pointerEventsReferenceElement: null,
      pointerEventsFloatingElement: null,
      restTimeoutPending: false,
      openChangeTimeout: createTimer(),
      restTimeout: createTimer(),
      handleCloseOptions: undefined,
      dispose() {
        created.openChangeTimeout.clear();
        created.restTimeout.clear();
      },
    };
    instance = created;
    data.hoverInteractionState = instance;
  }
  return instance;
}

/** The session's last relayed open-change reason, read by the nested-trigger close. */
interface HoverSessionState {
  lastOpenChangeReason: string | null;
}

/**
 * Returns the session's hover slot: the reason of the last committed open change, kept from the session's relay.
 * @param record The session.
 * @returns The slot.
 */
function hoverSessionState(record: SessionRecord): HoverSessionState {
  // Tooltip's `lastOpenChangeReason` (TooltipStore.ts:40, 85-91): every applied open change records its reason.
  return sessionSlot(record, 'hover', () => {
    const state: HoverSessionState = { lastOpenChangeReason: null };
    record.events.on('openchange', (details: SessionOpenChangeDetails) => {
      state.lastOpenChangeReason = details.reason;
    });
    return state;
  });
}

// ---------------------------------------------------------------------------------------------------------------
// The trigger side (FLT-PTR-11..19, FLT-ALG-24, FLT-EDGE-10): useHoverReferenceInteraction.ts, with Tooltip's nested
// triggers and press cancel (tooltip/trigger/TooltipTrigger.tsx:92-258)
// ---------------------------------------------------------------------------------------------------------------

function hoverListenerKey(options: HoverOptions | null): string | null {
  return options === null ? null : [options.move !== false, options.guardStaleOpen === true, options.nestedTooltipTriggers === true].join();
}

/**
 * The hover interaction on a trigger. A `mouseenter` (and, with `move`, the first `mousemove`) opens after the open
 * delay, or on rest when only a rest time is set; a `mouseleave` closes after the close delay or hands the decision to
 * the safe polygon; `pointerdown` and `pointerenter` record the pointer type. With `nestedTooltipTriggers`, a nested
 * tooltip trigger under the pointer vetoes the hover and closes a hover-opened popup; with `cancelPendingOpenOnPress`,
 * a press on a closed popup's trigger drops a pending hover open on the session's bus alone. Every timer reports a
 * request when it fires.
 */
function createHoverReferenceController(context: HandleContext): Controller {
  const { record, channel } = context;
  const element = context.element as HTMLElement;
  const instance = getHoverInteraction(record);
  const session = hoverSessionState(record);
  // useHoverReferenceInteraction.ts:99: whether the last close was hover-initiated (FLT-PTR-13, FLT-PTR-17).
  let isHoverCloseActive = false;
  // TooltipTrigger.tsx:108-111: the nested-trigger state and the trigger's own copy of the pointer type.
  let isNestedTriggerHovered = false;
  const nestedTriggerOpenTimeout = createTimer();
  let triggerPointerType: string | undefined;
  let handleClose: HandleClose | null = null;
  let handleCloseKey: string | null = null;
  let hoverCleanup: Cleanup | null = null;
  let hoverKey: string | null = null;
  let pressCleanup: Cleanup | null = null;

  const hoverOptions = (): HoverOptions | null => context.options.trigger?.hover ?? null;

  // Base UI's `isActiveTrigger`: this trigger is the session's anchor, or no anchor is set yet.
  const isActiveTrigger = () => record.domReferenceElement === null || record.domReferenceElement === element;

  // A delay group's reading while the trigger belongs to one (TooltipTrigger.tsx:92-97).
  const groupReading = (): DelayGroupReading | null => {
    const token = hoverOptions()?.delayGroupToken;
    return token == null ? null : readDelayGroup(token);
  };

  // FLT-API-04: the rest time, resolved at each use; 0 while the group is active (TooltipTrigger.tsx:113-119, 151).
  const resolveRestMs = (): number => {
    const reading = groupReading();
    if (reading !== null && reading.delayOpen === 0) {
      return 0;
    }
    return hoverOptions()?.restMs ?? 0;
  };

  // FLT-API-03: a delay, resolved at each use; the close delay falls back to the group's (TooltipTrigger.tsx:152-157).
  const resolveDelay = (phase: 'open' | 'close', pointerType: string | undefined): number | undefined => {
    const options = hoverOptions();
    const reading = groupReading();
    if (phase === 'open') {
      return getHoverDelay(reading !== null && reading.delayOpen === 0 ? 0 : options?.delayOpen, pointerType);
    }
    return getHoverDelay(options?.delayClose ?? reading?.delayClose, pointerType);
  };

  const isClickLikeOpen = () => isClickLikeOpenEvent(record.dataRef.current.openEvent?.type, instance.interactedInside);

  // FLT-PTR-12's `shouldOpen`: Tooltip's veto while a nested trigger is hovered (TooltipTrigger.tsx:161-163).
  const checkShouldOpen = () => !(hoverOptions()?.nestedTooltipTriggers === true && isNestedTriggerHovered);

  // A hover open of this session awaiting its commit (a round trip on Blazor Server) counts as open for the leave.
  const isHoverOpenPending = () => record.pending !== null && record.pending.open && record.pending.reason === 'trigger-hover';

  // FLT-EDGE-10, FLT-ALG-24: useHoverReferenceInteraction.ts:116-140.
  const isOverInactiveTrigger = (currentDomReference: Element | null, currentTarget: Element, target: EventTarget | null): boolean => {
    const allTriggers = record.triggerElements;

    // Fast path for normal usage where handlers are attached directly to triggers.
    if (allTriggers.hasElement(currentTarget)) {
      return !currentDomReference || !contains(currentDomReference, currentTarget);
    }

    // Fallback for delegated/wrapper usage where currentTarget may be outside the trigger map.
    if (!isElement(target)) {
      return false;
    }

    const targetElement = target;
    return (
      allTriggers.hasMatchingElement((trigger) => contains(trigger, targetElement)) &&
      (!currentDomReference || !contains(currentDomReference, targetElement))
    );
  };

  const cleanupMouseMoveHandler = () => {
    if (!instance.handler) {
      return;
    }
    const doc = ownerDocument(record.domReferenceElement);
    doc.removeEventListener('mousemove', instance.handler);
    instance.handler = undefined;
  };

  const clearPointerEvents = () => clearSafePolygonPointerEventsMutation(instance);

  // FLT-PTR-19 (useHoverReferenceInteraction.ts:156-159): the strategy, rebuilt when its options change, and its options
  // published to the shared state while this trigger is the active one. A trigger without hover publishes nothing.
  const refreshHandleClose = () => {
    const options = hoverOptions();
    if (options === null) {
      return;
    }
    const key = options.safePolygon === true ? String(options.blockPointerEvents === true) : null;
    if (key !== handleCloseKey) {
      handleCloseKey = key;
      handleClose = key === null ? null : safePolygon({ blockPointerEvents: options.blockPointerEvents === true });
    }
    if (isActiveTrigger()) {
      instance.handleCloseOptions = handleClose?.__options;
    }
  };

  // An open the trigger reports: the hovered line recomputed first, the delay group's instant phase carried with it.
  const requestHoverOpen = (event: MouseEvent, trigger: Element | null) => {
    recordHoveredLineBeforeOpen(context, event);
    const token = hoverOptions()?.delayGroupToken;
    const instant = token != null && delayGroupInstantPhase(record, token);
    requestOpenChange(record, channel, true, 'trigger-hover', event, trigger, instant ? { instant: true } : undefined);
  };

  // FLT-PTR-17 (useHoverReferenceInteraction.ts:163-187): runs when the relay brings the commit.
  const onOpenChange = (details: SessionOpenChangeDetails) => {
    if (!details.open) {
      isHoverCloseActive = details.reason === 'trigger-hover';
      cleanupMouseMoveHandler();
      instance.openChangeTimeout.clear();
      instance.restTimeout.clear();
      instance.blockMouseMove = true;
      instance.restTimeoutPending = false;
    } else {
      isHoverCloseActive = false;
      // Base UI re-renders the trigger the open made active, which publishes its strategy's options; the anchor is
      // applied after the relay, so the trigger is matched by the relayed one.
      if (details.triggerElement === element && hoverOptions() !== null) {
        instance.handleCloseOptions = handleClose?.__options;
      }
    }
  };

  // FLT-PTR-15 (useHoverReferenceInteraction.ts:194-206): the close and `floating.closed` (FLT-EVT-03), after the close
  // delay or at once.
  const closeWithDelay = (event: MouseEvent, runElseBranch = true) => {
    // FLT §10 Q12, the port's reading (ADR 04 Amendments 2026-10-27, untested quirks): a leave while the popup is closed
    // (and no hover open of the session awaits its commit) cancels the pending open only; Base UI would also request a
    // close of the closed popup.
    if (!record.open && !isHoverOpenPending()) {
      instance.openChangeTimeout.clear();
      return;
    }
    const closeDelay = resolveDelay('close', instance.pointerType);
    if (closeDelay) {
      instance.openChangeTimeout.start(closeDelay, () => {
        requestOpenChange(record, channel, false, 'trigger-hover', event);
        record.tree.events.emit('floating.closed', event);
      });
    } else if (runElseBranch) {
      instance.openChangeTimeout.clear();
      requestOpenChange(record, channel, false, 'trigger-hover', event);
      record.tree.events.emit('floating.closed', event);
    }
  };

  // FLT-PTR-12, FLT-ALG-24: useHoverReferenceInteraction.ts:216-300.
  const onMouseEnter = (event: MouseEvent) => {
    instance.openChangeTimeout.clear();
    instance.blockMouseMove = false;

    const options = hoverOptions();
    if (options === null || (options.mouseOnly === true && !isMouseLikePointerType(instance.pointerType))) {
      return;
    }

    // Only rest delay is set; there's no fallback delay.
    // This will be handled by `onMouseMove`.
    const restMsValue = resolveRestMs();
    const openDelay = resolveDelay('open', instance.pointerType);
    const eventTarget = getTarget(event);
    const currentTarget = (event.currentTarget as HTMLElement | null) ?? null;
    const currentDomReference = record.domReferenceElement;
    let triggerNode: HTMLElement | null = currentTarget;

    // Wrapper/delegated mode: resolve the actual trigger from the event target.
    if (isElement(eventTarget) && !record.triggerElements.hasElement(eventTarget)) {
      for (const triggerElement of record.triggerElements.elements()) {
        if (contains(triggerElement, eventTarget)) {
          triggerNode = triggerElement as HTMLElement;
          break;
        }
      }
    }

    // Wrapper/delegated mode fallback: if the wrapper contains the active trigger,
    // treat this as re-entering that active trigger.
    if (
      isElement(currentTarget) &&
      isElement(currentDomReference) &&
      !record.triggerElements.hasElement(currentTarget) &&
      contains(currentTarget, currentDomReference)
    ) {
      triggerNode = currentDomReference as HTMLElement;
    }

    const isOverInactive = triggerNode == null ? false : isOverInactiveTrigger(currentDomReference, triggerNode, eventTarget);
    const isOpen = record.open;
    // FLT-PTR-13: the closing transition is C#'s `transitionStatus === 'ending'`.
    const isInClosingTransition = record.ending;
    const isHoverCloseTransition = !isOpen && isInClosingTransition && isHoverCloseActive;
    const isReenteringSameTriggerDuringCloseTransition =
      !isOverInactive &&
      isElement(triggerNode) &&
      isElement(currentDomReference) &&
      contains(currentDomReference, triggerNode) &&
      isHoverCloseTransition;
    const isRestOnlyDelay = restMsValue > 0 && !openDelay;
    const shouldOpenImmediately = (isOverInactive && (isOpen || isHoverCloseTransition)) || isReenteringSameTriggerDuringCloseTransition;

    const shouldOpen = !isOpen || isOverInactive;

    // Open immediately when moving between triggers while open, or during
    // a hover-driven close transition (including same-trigger re-entry).
    if (shouldOpenImmediately) {
      if (checkShouldOpen()) {
        requestHoverOpen(event, triggerNode);
      }
      return;
    }

    if (isRestOnlyDelay) {
      return;
    }

    if (openDelay) {
      instance.openChangeTimeout.start(openDelay, () => {
        if (shouldOpen && checkShouldOpen()) {
          requestHoverOpen(event, triggerNode);
        }
      });
    } else if (shouldOpen) {
      if (checkShouldOpen()) {
        requestHoverOpen(event, triggerNode);
      }
    }
  };

  // FLT-PTR-14: useHoverReferenceInteraction.ts:302-360.
  const onMouseLeave = (event: MouseEvent) => {
    if (isClickLikeOpen()) {
      clearPointerEvents();
      return;
    }

    cleanupMouseMoveHandler();

    const doc = ownerDocument(record.domReferenceElement);
    instance.restTimeout.clear();
    instance.restTimeoutPending = false;

    // `getHandleCloseContext` has no M4 consumer: the session's published floating context is the close context.
    const handleCloseContextBase = record.dataRef.current.floatingContext;

    if (isTargetInsideEnabledTrigger(event.relatedTarget, record.triggerElements)) {
      return;
    }

    if (handleClose && handleCloseContextBase) {
      if (!record.open) {
        instance.openChangeTimeout.clear();
      }

      const currentTrigger = element;

      instance.handler = handleClose({
        ...handleCloseContextBase,
        tree: record.tree,
        x: event.clientX,
        y: event.clientY,
        onClose() {
          clearPointerEvents();
          cleanupMouseMoveHandler();
          if (hoverOptions() !== null && !isClickLikeOpen() && currentTrigger === record.domReferenceElement) {
            closeWithDelay(event, true);
          }
        },
      });

      doc.addEventListener('mousemove', instance.handler);
      instance.handler(event);

      return;
    }

    const shouldClose = instance.pointerType === 'touch' ? !contains(record.floatingElement, event.relatedTarget as Element | null) : true;

    if (shouldClose) {
      closeWithDelay(event);
    }
  };

  // FLT-PTR-18: backup cancellation for Chrome's dropped `mouseleave` (useHoverReferenceInteraction.ts:362-374).
  const onMouseOut = (event: MouseEvent) => {
    if (contains(element, event.relatedTarget as Element | null)) {
      return; // moved within the trigger's own subtree
    }
    instance.openChangeTimeout.clear();
    instance.restTimeout.clear();
    instance.restTimeoutPending = false;
  };

  // FLT-PTR-11: useHoverReferenceInteraction.ts:419-421.
  const setPointerRef = (event: PointerEvent) => {
    instance.pointerType = event.pointerType;
  };

  // FLT-PTR-16: useHoverReferenceInteraction.ts:426-497 (the prop bag's `onMouseMove`, with React's `event.target`).
  const onMouseMove = (event: MouseEvent) => {
    const options = hoverOptions();
    if (options === null) {
      return;
    }
    const trigger = event.currentTarget as HTMLElement;

    const currentDomReference = record.domReferenceElement;
    const currentOpen = record.open;
    const isOverInactive = isOverInactiveTrigger(currentDomReference, trigger, event.target);

    if (options.mouseOnly === true && !isMouseLikePointerType(instance.pointerType)) {
      return;
    }

    if (currentOpen && isOverInactive && instance.handleCloseOptions?.blockPointerEvents) {
      const floatingElement = record.floatingElement;

      if (floatingElement) {
        // `getScope` has no M4 consumer (Navigation Menu's).
        const scopeElement = trigger.ownerDocument.body;

        applySafePolygonPointerEventsMutation(instance, {
          scopeElement,
          referenceElement: trigger,
          floatingElement,
        });
      }
    }

    const restMsValue = resolveRestMs();
    if ((currentOpen && !isOverInactive) || restMsValue === 0) {
      return;
    }

    if (!isOverInactive && instance.restTimeoutPending && (event.movementX ?? 0) ** 2 + (event.movementY ?? 0) ** 2 < 2) {
      return;
    }

    instance.restTimeout.clear();

    const handleMouseMove = () => {
      instance.restTimeoutPending = false;

      // A delayed hover open should not override a click-like open that happened
      // while the hover delay was pending.
      if (isClickLikeOpen()) {
        return;
      }

      const latestOpen = record.open;

      if (!instance.blockMouseMove && (!latestOpen || isOverInactive) && checkShouldOpen()) {
        requestHoverOpen(event, trigger);
      }
    };

    if (instance.pointerType === 'touch') {
      handleMouseMove();
    } else if (isOverInactive && currentOpen) {
      handleMouseMove();
    } else {
      instance.restTimeoutPending = true;
      instance.restTimeout.start(restMsValue, handleMouseMove);
    }
  };

  // TooltipTrigger.tsx:121-131.
  const isEnabledNestedTriggerTarget = (target: Element | null): boolean => {
    if (!target) {
      return false;
    }
    const nearestTrigger = closest(target, `[${TOOLTIP_TRIGGER_IDENTIFIER}]`);
    return nearestTrigger !== null && nearestTrigger !== element && contains(element, nearestTrigger);
  };

  // TooltipTrigger.tsx:133-144.
  const detectNestedTriggerHover = (target: Element | null): boolean => {
    const nestedTriggerHovered = isEnabledNestedTriggerTarget(target);

    isNestedTriggerHovered = nestedTriggerHovered;
    if (nestedTriggerHovered) {
      instance.openChangeTimeout.clear();
      instance.restTimeout.clear();
      instance.restTimeoutPending = false;
      nestedTriggerOpenTimeout.clear();
    }
    return nestedTriggerHovered;
  };

  // TooltipTrigger.tsx:168-214 (React's `onMouseOver`).
  const handleNestedTriggerHover = (event: MouseEvent) => {
    const wasNestedTriggerHovered = isNestedTriggerHovered;
    const eventTarget = getTarget(event);
    const target = isElement(eventTarget) ? eventTarget : null;
    const nestedTriggerHovered = detectNestedTriggerHover(target);
    const targetInsideTrigger = target !== null && contains(element, target);

    // Only close hover-opened parents. Focus/click-like opens remain owned by
    // their original interaction and should not be clobbered by nested hover.
    if (nestedTriggerHovered && record.open && session.lastOpenChangeReason === 'trigger-hover') {
      requestOpenChange(record, channel, false, 'trigger-hover', event);
      return;
    }

    if (
      wasNestedTriggerHovered &&
      !nestedTriggerHovered &&
      targetInsideTrigger &&
      hoverOptions() !== null &&
      !record.open &&
      // Match the hover hook's non-strict mouse fallback for mouse-only event sequences.
      isMouseLikePointerType(triggerPointerType)
    ) {
      const open = () => {
        if (!isNestedTriggerHovered && hoverOptions() !== null && !record.open) {
          requestHoverOpen(event, element);
        }
      };

      const openDelay = resolveRestMs();

      // With `move: false`, the hover hook only listens to mouseenter/mouseleave
      // on the parent trigger. Leaving a nested child for the parent area fires
      // no event the hook can react to, so reopen locally.
      if (openDelay === 0) {
        nestedTriggerOpenTimeout.clear();
        open();
      } else {
        nestedTriggerOpenTimeout.start(openDelay, open);
      }
    }
  };

  // TooltipTrigger.tsx:239-248.
  const onNestedTriggerMouseLeave = () => {
    isNestedTriggerHovered = false;
    nestedTriggerOpenTimeout.clear();
    triggerPointerType = undefined;
  };

  const setTriggerPointerType = (event: PointerEvent) => {
    triggerPointerType = event.pointerType;
  };

  // Tooltip's press cancel (TooltipTrigger.tsx:247-258, TooltipStore.ts:93-99): the root store's `dispatchOpenChange`
  // alone, so the session's listeners drop a pending hover open; nothing is reported and nothing commits. The emission
  // is not an applied change, so the last relayed reason is kept.
  const cancelPendingOpen = (event: MouseEvent) => {
    if (record.open) {
      return;
    }
    record.dataRef.current.openEvent = undefined;
    const lastOpenChangeReason = session.lastOpenChangeReason;
    const details: SessionOpenChangeDetails = {
      open: false,
      reason: 'trigger-press',
      nativeEvent: event,
      nested: record.nested,
      triggerElement: undefined,
    };
    record.events.emit('openchange', details);
    session.lastOpenChangeReason = lastOpenChangeReason;
  };

  const installHover = () => {
    const options = hoverOptions();
    if (hoverCleanup !== null || options === null) {
      return;
    }
    const nested = options.nestedTooltipTriggers === true;
    hoverKey = hoverListenerKey(options);
    record.events.on('openchange', onOpenChange);
    // The hook's native listeners first, then its prop bag, then Tooltip's handlers (React runs delegated handlers last).
    hoverCleanup = mergeCleanups(
      options.move !== false && listen(element, 'mousemove', onMouseEnter, { once: true }),
      listen(element, 'mouseenter', onMouseEnter),
      listen(element, 'mouseleave', onMouseLeave),
      options.guardStaleOpen === true && listen(element, 'mouseout', onMouseOut),
      listen(element, 'pointerdown', setPointerRef),
      listen(element, 'pointerenter', setPointerRef),
      listen(element, 'mousemove', onMouseMove),
      nested && listen(element, 'mouseover', handleNestedTriggerHover),
      nested && listen(element, 'mouseleave', onNestedTriggerMouseLeave),
      nested && listen(element, 'pointerenter', setTriggerPointerType),
      nested && listen(element, 'pointerdown', setTriggerPointerType),
      () => record.events.off('openchange', onOpenChange),
    );
  };

  const uninstallHover = () => {
    hoverCleanup?.();
    hoverCleanup = null;
    hoverKey = null;
  };

  const installPress = () => {
    if (pressCleanup !== null || context.options.trigger?.cancelPendingOpenOnPress !== true) {
      return;
    }
    pressCleanup = mergeCleanups(listen(element, 'pointerdown', cancelPendingOpen), listen(element, 'click', cancelPendingOpen));
  };

  const uninstallPress = () => {
    pressCleanup?.();
    pressCleanup = null;
  };

  refreshHandleClose();
  installHover();
  installPress();
  return {
    update() {
      refreshHandleClose();
      if (hoverListenerKey(hoverOptions()) !== hoverKey) {
        uninstallHover();
        installHover();
      }
      if ((context.options.trigger?.cancelPendingOpenOnPress === true) !== (pressCleanup !== null)) {
        uninstallPress();
        installPress();
      }
    },
    dispose() {
      uninstallHover();
      uninstallPress();
      cleanupMouseMoveHandler();
      nestedTriggerOpenTimeout.clear();
      instance.dispose();
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The floating side (FLT-PTR-20..25): useHoverFloatingInteraction.ts
// ---------------------------------------------------------------------------------------------------------------

/**
 * The hover interaction on a floating element. Entering it cancels a pending close; leaving it closes a hover-opened
 * popup after the close delay, hands the leave to an active safe polygon, or waits for a nested popup's close; a
 * `pointerdown` inside records whether a nested popup's trigger was pressed; while the popup is hover-open with blocked
 * pointer events, pointer events stay off everything but the anchor and the floating element. A close resets the
 * shared pointer type.
 */
function createHoverFloatingController(context: HandleContext): Controller {
  const { record, channel } = context;
  const floatingElement = context.element as HTMLElement;
  const instance = getHoverInteraction(record);
  const childClosedTimeout = createTimer();
  let cleanup: Cleanup | null = null;
  let pointerEventsCleanup: Cleanup | null = null;
  let subscribedTreeEvents: SessionEvents | null = null;

  const hoverOptions = (): FloatingHoverOptions | null => context.options.floating?.hover ?? null;

  const isClickLikeOpen = () => isClickLikeOpenEvent(record.dataRef.current.openEvent?.type, instance.interactedInside);

  const isHoverOpen = () => isHoverOpenEvent(record.dataRef.current.openEvent?.type);

  const clearPointerEvents = () => clearSafePolygonPointerEventsMutation(instance);

  // useHoverFloatingInteraction.ts:81-154: the two layout effects, run as React runs them when their dependencies
  // change (the mutation effect's cleanup, then the reset on a change of `open`, then the mutation effect).
  const runLayoutEffects = (open: boolean, domReference: Element | null, openChanged: boolean) => {
    pointerEventsCleanup?.();
    pointerEventsCleanup = null;

    // FLT-PTR-20.
    if (openChanged && !open) {
      instance.pointerType = undefined;
      instance.restTimeoutPending = false;
      instance.interactedInside = false;
      clearPointerEvents();
    }

    // FLT-PTR-21.
    if (hoverOptions() === null) {
      return;
    }

    if (open && instance.handleCloseOptions?.blockPointerEvents && isHoverOpen() && isElement(domReference)) {
      const ref = domReference as HTMLElement | SVGSVGElement;
      const doc = ownerDocument(floatingElement);
      const parentId = record.parentToken;

      const parentFloating =
        parentId === null ? null : (record.tree.nodesRef.current.find((node) => node.id === parentId)?.context?.elements?.floating ?? null);

      if (parentFloating) {
        parentFloating.style.pointerEvents = '';
      }

      // A keep-mounted submenu can appear in the tree before it opens, so a
      // cached scope or parent lookup may resolve to the submenu itself. That
      // would not shield sibling items in the parent menu.
      const cachedScopeElement = instance.pointerEventsScopeElement !== floatingElement ? instance.pointerEventsScopeElement : null;
      const parentScopeElement = parentFloating !== floatingElement ? parentFloating : null;
      // `getScope` has no M4 consumer (Navigation Menu's).
      const scopeElement = cachedScopeElement ?? parentScopeElement ?? closest<HTMLElement>(ref, '[data-rootownerid]') ?? doc.body;

      applySafePolygonPointerEventsMutation(instance, {
        scopeElement,
        referenceElement: ref,
        floatingElement,
      });

      pointerEventsCleanup = clearPointerEvents;
    }
  };

  // The effects re-run once the session applied a change of `open` or of the anchor (the commit the relay brought, or an
  // anchor that attached later), as React re-runs them after the render that changed their dependencies. A change of
  // neither (the press cancel on a closed popup) runs nothing, as it re-renders nothing in Base UI.
  let lastOpen = record.open;
  let lastReference = record.domReferenceElement;
  const onSessionApplied = () => {
    const open = record.open;
    const domReference = record.domReferenceElement;
    const openChanged = open !== lastOpen;
    if (openChanged || domReference !== lastReference) {
      lastOpen = open;
      lastReference = domReference;
      runLayoutEffects(open, domReference, openChanged);
    }
  };

  const hasParentChildren = () => {
    const parentId = record.parentToken;
    return !!(parentId && getNodeChildren(record.tree.nodesRef.current, parentId).length > 0);
  };

  // FLT-PTR-15 with `closeDelay` (useHoverFloatingInteraction.ts:165-178).
  const closeWithDelay = (event: MouseEvent) => {
    const closeDelay = getHoverDelay(hoverOptions()?.closeDelay, instance.pointerType);
    const close = () => {
      requestOpenChange(record, channel, false, 'trigger-hover', event);
      record.tree.events.emit('floating.closed', event);
    };

    if (closeDelay) {
      instance.openChangeTimeout.start(closeDelay, close);
    } else {
      instance.openChangeTimeout.clear();
      close();
    }
  };

  const unsubscribeNodeClosed = () => {
    subscribedTreeEvents?.off('floating.closed', onNodeClosed);
    subscribedTreeEvents = null;
  };

  // FLT-PTR-25: useHoverFloatingInteraction.ts:235-245.
  function onNodeClosed(event: MouseEvent): void {
    if (!record.parentToken || hasParentChildren()) {
      return;
    }
    // Allow the mouseenter event to fire in case child was closed because mouse moved into parent.
    childClosedTimeout.start(0, () => {
      unsubscribeNodeClosed();
      requestOpenChange(record, channel, false, 'trigger-hover', event);
      record.tree.events.emit('floating.closed', event);
    });
  }

  const subscribeNodeClosed = () => {
    const events = record.tree.events;
    if (subscribedTreeEvents !== events) {
      unsubscribeNodeClosed();
    }
    events.on('floating.closed', onNodeClosed);
    subscribedTreeEvents = events;
  };

  // FLT-PTR-22: useHoverFloatingInteraction.ts:180-188.
  const handleInteractInside = (event: PointerEvent) => {
    const target = getTarget(event) as Element | null;
    if (!isInteractiveElement(target)) {
      instance.interactedInside = false;
      return;
    }

    instance.interactedInside = closest(target, '[aria-haspopup]') != null;
  };

  // FLT-PTR-23: useHoverFloatingInteraction.ts:190-195.
  const onFloatingMouseEnter = () => {
    instance.openChangeTimeout.clear();
    childClosedTimeout.clear();
    unsubscribeNodeClosed();
    clearPointerEvents();
  };

  // FLT-PTR-24: useHoverFloatingInteraction.ts:197-233.
  const onFloatingMouseLeave = (event: MouseEvent) => {
    if (hasParentChildren()) {
      subscribeNodeClosed();
      return;
    }

    if (isTargetInsideEnabledTrigger(event.relatedTarget, record.triggerElements)) {
      // If the mouse is leaving the reference element to another trigger, don't explicitly close the popup
      // as it will be moved.
      return;
    }

    const currentNodeId = record.dataRef.current.floatingContext?.nodeId;
    const relatedTarget = event.relatedTarget;
    const isMovingIntoDescendantFloating =
      currentNodeId &&
      isElement(relatedTarget) &&
      getNodeChildren(record.tree.nodesRef.current, currentNodeId, false).some((node) => contains(node.context?.elements?.floating, relatedTarget));

    if (isMovingIntoDescendantFloating) {
      return;
    }

    // If the safePolygon handler is active, let it handle the close logic.
    if (instance.handler) {
      instance.handler(event);
      return;
    }

    clearPointerEvents();
    if (isHoverOpen() && !isClickLikeOpen()) {
      closeWithDelay(event);
    }
  };

  const install = () => {
    if (cleanup !== null || hoverOptions() === null) {
      return;
    }
    cleanup = mergeCleanups(
      listen(floatingElement, 'mouseenter', onFloatingMouseEnter),
      listen(floatingElement, 'mouseleave', onFloatingMouseLeave),
      listen(floatingElement, 'pointerdown', handleInteractInside, true),
      unsubscribeNodeClosed,
    );
  };

  const uninstall = () => {
    cleanup?.();
    cleanup = null;
  };

  const stopListening = onSessionChange(record, onSessionApplied);
  runLayoutEffects(record.open, record.domReferenceElement, true);
  install();
  return {
    update(previous) {
      const enabled = hoverOptions() !== null;
      if ((previous.floating?.hover ?? null) !== null !== enabled) {
        runLayoutEffects(record.open, record.domReferenceElement, false);
        if (enabled) {
          install();
        } else {
          uninstall();
        }
      }
    },
    dispose() {
      stopListening();
      uninstall();
      pointerEventsCleanup = null;
      clearPointerEvents();
      childClosedTimeout.clear();
      instance.dispose();
    },
  };
}

// #endregion hover

// ---------------------------------------------------------------------------------------------------------------
// #region delay-group: FloatingDelayGroup and useDelayGroup (FLT-ALG-23, FLT-EVT-07, FLT-ANI-01)
// ---------------------------------------------------------------------------------------------------------------

// A delay as a provider gives it (Base UI's `Delay` in its object form; FloatingDelayGroup's `delay`).
interface GroupDelay {
  open: number | undefined;
  close: number | undefined;
}

// The member that opened last (Base UI's `currentContextRef.current`): its session, whose Root twin closes it when
// another member opens. Base UI also keeps the member's `setIsInstantPhase`; the port carries the phase on the requests
// instead (`delayGroupInstantPhase` on the open, `instant: true` on the previous member's close), and C# holds it.
interface DelayGroupContext {
  record: SessionRecord;
}

// One group per provider token: the sanctioned page-global state of the module beside the session map (FLT-ALG-23's
// `G`). The refs of FloatingDelayGroup.tsx:64-105 are its fields.
interface DelayGroup {
  token: string;
  /** The provider handles that hold the group; the last dispose deletes it. */
  references: number;
  /** Base UI's `delayRef.current`: what a member reads. */
  delay: GroupDelay;
  /** Base UI's `initialDelayRef.current`: the provider's delay, restored on reset. */
  initialDelay: GroupDelay;
  /** How long the group stays warm after its last member closed. */
  timeoutMs: number;
  timer: Timer;
  /** The session token of the member keeping the group active (Base UI's `currentIdRef.current`, the `floatingId`). */
  currentId: string | null;
  currentContext: DelayGroupContext | null;
}

const delayGroups = new Map<string, DelayGroup>();

// useHoverShared.ts:46-57 (`getDelay`) over the object form.
function resolveGroupDelay(value: GroupDelay, prop: 'open' | 'close'): number | undefined {
  return value[prop];
}

function providerDelayOf(options: InteractionsOptions): GroupDelay {
  return { open: options.provider?.delayOpen ?? undefined, close: options.provider?.delayClose ?? undefined };
}

/**
 * Defers a hook's effects to a microtask, once however often it is asked before then: React runs a hook's effects after
 * the commit of the render an event or a store change caused, and the session emits `openchange` before it applies the
 * committed state, so the effects re-read the session a microtask later, still before the next paint.
 * @param run The effects.
 * @returns The scheduler.
 */
function createDeferredRun(run: () => void): () => void {
  let scheduled = false;
  return () => {
    if (scheduled) {
      return;
    }
    scheduled = true;
    queueMicrotask(() => {
      scheduled = false;
      run();
    });
  };
}

/**
 * Attaches a TooltipProvider's group (FloatingDelayGroup): the group of the token is created by the first handle and
 * deleted with the last; an `update` that changes the delay replaces the provider's delay, keeping the open delay at 0
 * while a member is current.
 * @param token The provider token.
 * @param initialOptions The options of the `attach`.
 * @param _twinReference The twin, which the group never reports to.
 * @returns The handle.
 */
function attachProvider(token: string, initialOptions: InteractionsOptions, _twinReference: DotNetTwin): TwinHandle {
  let group = delayGroups.get(token);
  if (!group) {
    const initialDelay = providerDelayOf(initialOptions);
    group = {
      token,
      references: 0,
      delay: initialDelay,
      initialDelay,
      timeoutMs: 0,
      timer: createTimer(),
      currentId: null,
      currentContext: null,
    };
    delayGroups.set(token, group);
  }
  group.references += 1;
  const owned = group;
  let delay = providerDelayOf(initialOptions);
  let disposed = false;

  // FLT-EDGE-12: FloatingDelayGroup.tsx:73-85, run at mount and when the provider's `delay` changes.
  const applyDelay = () => {
    owned.initialDelay = delay;

    if (!owned.currentId) {
      owned.delay = delay;
      return;
    }

    owned.delay = {
      open: resolveGroupDelay(owned.delay, 'open'),
      close: resolveGroupDelay(delay, 'close'),
    };
  };

  // FloatingDelayGroup's `timeoutMs = 0` default; the members read it from the group, as from Base UI's context.
  owned.timeoutMs = initialOptions.provider?.timeout ?? 0;
  applyDelay();

  return {
    update(optionsJson) {
      if (disposed) {
        return;
      }
      const options = parseOptions<InteractionsOptions>(optionsJson);
      owned.timeoutMs = options.provider?.timeout ?? 0;
      const next = providerDelayOf(options);
      if (next.open !== delay.open || next.close !== delay.close) {
        delay = next;
        applyDelay();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      owned.references -= 1;
      if (owned.references <= 0 && delayGroups.get(token) === owned) {
        // useTimeout clears the group's timer when the provider unmounts.
        owned.timer.clear();
        delayGroups.delete(token);
      }
    },
  };
}

/** What a member trigger's hover reads from its group at `mouseenter`. */
interface DelayGroupReading {
  /** The group's open delay now: 0 while a member is current (open, or closed and still warm), else the provider's. */
  delayOpen: number | undefined;
  /** The group's close delay: the provider's. */
  delayClose: number | undefined;
}

/**
 * Reads the group a trigger's hover belongs to: its current delay.
 * @param token The provider token the trigger names.
 * @returns The reading, or `null` without a provider of that token.
 */
export function readDelayGroup(token: string | null | undefined): DelayGroupReading | null {
  const group = token ? delayGroups.get(token) : undefined;
  if (!group) {
    return null;
  }
  return { delayOpen: resolveGroupDelay(group.delay, 'open'), delayClose: resolveGroupDelay(group.delay, 'close') };
}

/**
 * The instant phase an open request of this session carries: whether another member of the group is the current one,
 * so C# renders `data-instant="delay"` in the batch of `data-open`.
 * @param record The session that requests the open.
 * @param token The provider token its trigger names.
 * @returns `true` when the group has a current member and it is another session.
 */
export function delayGroupInstantPhase(record: SessionRecord, token: string | null | undefined): boolean {
  const group = token ? delayGroups.get(token) : undefined;
  // FloatingDelayGroup.tsx:234: `prevId !== null && prevId !== floatingId`, read when the open is requested.
  return group !== undefined && group.currentId !== null && group.currentId !== record.token;
}

/**
 * A trigger's membership in its provider's group (useDelayGroup): the trigger is a member while its hover names a
 * provider token whose group exists, and its `open` is whether the popup is open and anchored to it. When that changes
 * (on the relay, a microtask later), an open member becomes the group's current one and closes the previous member
 * through that popup's Root twin with reason `none`; a member that closes keeps the group warm for the provider's
 * timeout, then resets it.
 */
function createDelayGroupController(context: HandleContext): Controller {
  const { record } = context;
  const element = context.element as HTMLElement;
  // Base UI's `floatingId`: the popup's identity in the group, shared by every trigger of one popup.
  const floatingId = record.token;
  let group: DelayGroup | null = null;
  let open = false;
  // Base UI's `openRef.current`.
  let openRef = false;
  let timeoutMs = 0;
  let closeEffectCleanup: Cleanup | null = null;
  let disposed = false;

  const tokenOf = () => context.options.trigger?.hover?.delayGroupToken ?? null;
  const isOpenedByThisTrigger = () => record.open && record.domReferenceElement === element;

  // FloatingDelayGroup.tsx:165-214: a member that closes while it is the current one.
  const runCloseEffect = (current: DelayGroup, isOpen: boolean): Cleanup | null => {
    function unset() {
      // `currentContextRef.current?.setIsInstantPhase(false)`: that member is closed, and its last request carried the
      // phase C# holds.
      current.currentId = null;
      current.currentContext = null;
      current.delay = current.initialDelay;
      current.timer.clear();
    }

    if (!current.currentId) {
      return null;
    }

    if (!isOpen && current.currentId === floatingId) {
      // `setIsInstantPhase(false)`: the close request carries no instant phase.
      if (current.timeoutMs) {
        const closingId = floatingId;
        current.timer.start(current.timeoutMs, () => {
          // If another tooltip has taken over the group, skip resetting.
          if (record.open || (current.currentId && current.currentId !== closingId)) {
            return;
          }
          unset();
        });
        return () => {
          if (openRef || current.currentId !== closingId) {
            current.timer.clear();
          }
        };
      }

      unset();
    }

    return null;
  };

  // FloatingDelayGroup.tsx:216-251 (FLT-ALG-23, FLT-EVT-07, FLT-ANI-01): a member that opens becomes the current one.
  const runOpenEffect = (current: DelayGroup, isOpen: boolean) => {
    if (!isOpen) {
      return;
    }

    const prevContext = current.currentContext;
    const prevId = current.currentId;

    // A new tooltip is opening, so cancel any pending timeout that would reset
    // the group's delay back to the initial value.
    current.timer.clear();
    current.currentContext = { record };
    current.currentId = floatingId;
    current.delay = {
      open: 0,
      close: resolveGroupDelay(current.initialDelay, 'close'),
    };

    if (prevId !== null && prevId !== floatingId) {
      // `setIsInstantPhase(true)` on both: this member's open request carried it (`delayGroupInstantPhase`) and the
      // previous member's close carries it. `prevContext?.onOpenChange(false, details('none'))` is that popup's
      // `setOpen`: a request through its Root's twin, as it is when the close is sent.
      const previous = prevContext?.record;
      const channel = previous?.channel;
      if (previous && channel) {
        requestOpenChange(previous, channel, false, 'none', undefined, null, { instant: true });
      }
    }
    // Else `setIsInstantPhase(false)` on both: the open request carried no instant phase.
  };

  // FloatingDelayGroup.tsx:253-267 (FLT-EDGE-11), after the close effect's own cleanup, as React unmounts in order.
  const leave = (current: DelayGroup) => {
    closeEffectCleanup?.();
    closeEffectCleanup = null;
    if (current.currentId === floatingId) {
      current.currentContext = null;

      if (!openRef) {
        return;
      }

      current.currentId = null;
      current.delay = current.initialDelay;
      current.timer.clear();
    }
  };

  // The effects run when their inputs change, as React re-runs them on commit: all of them when the member joins a
  // group, the close effect when `open` or the timeout changes, the open effect when `open` changes. The cleanup runs
  // first, with `openRef` still the previous value, then `openRef` is written, then the effects run in order.
  const sync = () => {
    if (disposed) {
      return;
    }
    const token = tokenOf();
    const next = token !== null ? (delayGroups.get(token) ?? null) : null;
    const isOpen = isOpenedByThisTrigger();
    if (next !== group) {
      if (group) {
        leave(group);
      }
      group = next;
      if (!group) {
        return;
      }
      openRef = isOpen;
      open = isOpen;
      timeoutMs = group.timeoutMs;
      closeEffectCleanup = runCloseEffect(group, isOpen);
      runOpenEffect(group, isOpen);
      return;
    }
    if (!group) {
      return;
    }
    const openChanged = isOpen !== open;
    if (!openChanged && group.timeoutMs === timeoutMs) {
      return;
    }
    closeEffectCleanup?.();
    closeEffectCleanup = null;
    openRef = isOpen;
    open = isOpen;
    timeoutMs = group.timeoutMs;
    closeEffectCleanup = runCloseEffect(group, isOpen);
    if (openChanged) {
      runOpenEffect(group, isOpen);
    }
  };

  const stopListening = onSessionChange(record, sync);
  sync();

  return {
    update: sync,
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      stopListening();
      if (group) {
        leave(group);
        group = null;
      }
    },
  };
}

// #endregion delay-group

// ---------------------------------------------------------------------------------------------------------------
// #region focus: useFocus (FLT-FOC-01..04, FLT-API-15)
// ---------------------------------------------------------------------------------------------------------------

// FLT-A11Y-06: Mac Safari moves no focus on a pointer press, so modality is read from the last keydown or pointerdown.
const isMacSafari = platform.os.mac && platform.engine.webkit;

// The `data-base-ui-focus-guard` attribute and its `data-type="outside"` (portal guards), read by the blur close.
const FOCUS_GUARD_ATTRIBUTE = 'data-base-ui-focus-guard';

// FLT-FOC-01; FLT-FOC-02/03; FLT-FOC-04
/**
 * useFocus on a trigger (useFocus.ts): a focus opens the popup after `delay`; a window blur while the
 * reference holds focus, and a press or Escape close, block the next focus on that reference; a blur
 * closes it after 0 ms unless focus stayed in the reference, moved into the floating element, onto an outside focus
 * guard or onto another enabled trigger. React's `onFocus`/`onBlur` are `focusin`/`focusout`.
 */
function createFocusController(context: HandleContext): Controller {
  const { record, channel } = context;
  const element = context.element as HTMLElement;
  const timeout = createTimer();
  let blockFocus = false;
  let blockedReference: Element | null = null;
  let keyboardModality = true;
  let cleanup: Cleanup | null = null;

  const focusOptions = (): FocusOptions | null => context.options.trigger?.focus ?? null;

  const resetBlockedFocus = () => {
    blockFocus = false;
    blockedReference = null;
  };

  const onWindowBlur = () => {
    const currentDomReference = record.domReferenceElement;
    if (!record.open && isHTMLElement(currentDomReference) && currentDomReference === activeElement(ownerDocument(currentDomReference))) {
      blockFocus = true;
      blockedReference = currentDomReference;
    }
  };

  // FLT-FOC-03: the listener runs when the relay brings the commit (on Server a round trip after it).
  const onOpenChange = (details: SessionOpenChangeDetails) => {
    if (details.reason === 'trigger-press' || details.reason === 'escape-key') {
      const referenceElement = record.domReferenceElement;
      if (isElement(referenceElement)) {
        blockedReference = referenceElement;
        blockFocus = true;
      }
    }
  };

  const onFocus = (event: FocusEvent) => {
    const delay = focusOptions()?.delay ?? 0;
    // Tooltip's nested triggers (tooltip/trigger/TooltipTrigger.tsx:234-238): a focus inside an enabled nested tooltip
    // trigger is that trigger's, so the outer trigger's focus handling is skipped (its preventBaseUIHandler()).
    if (context.options.trigger?.hover?.nestedTooltipTriggers) {
      const target = getTarget(event);
      const nested = isElement(target) ? closest(target, `[${TOOLTIP_TRIGGER_IDENTIFIER}]`) : null;
      if (nested !== null && nested !== element && contains(element, nested)) {
        return;
      }
    }
    if (blockFocus) {
      if (blockedReference === element) {
        return;
      }
      resetBlockedFocus();
    }
    const target = getTarget(event);
    if (isElement(target)) {
      if (isMacSafari && !event.relatedTarget) {
        if (!keyboardModality && !isTypeableElement(target)) {
          return;
        }
      } else if (!matchesFocusVisible(target)) {
        return;
      }
    }
    const movedFromOtherEnabledTrigger = isTargetInsideEnabledTrigger(event.relatedTarget, record.triggerElements);
    // A delay group's member opens in the group's instant phase whatever the origin (FloatingDelayGroup.tsx:216-251, on
    // commit): the request carries it, as a hover open's does.
    const open = () => {
      const instant = delayGroupInstantPhase(record, context.options.trigger?.hover?.delayGroupToken);
      requestOpenChange(record, channel, true, 'trigger-focus', event, element, instant ? { instant: true } : undefined);
    };
    if ((record.open && movedFromOtherEnabledTrigger) || !delay) {
      open();
      return;
    }
    timeout.start(delay, () => {
      if (blockFocus) {
        return;
      }
      open();
    });
  };

  const onBlur = (event: FocusEvent) => {
    resetBlockedFocus();
    const relatedTarget = event.relatedTarget;
    const movedToFocusGuard =
      isElement(relatedTarget) && relatedTarget.hasAttribute(FOCUS_GUARD_ATTRIBUTE) && relatedTarget.getAttribute('data-type') === 'outside';
    timeout.start(0, () => {
      const domReference = record.domReferenceElement;
      const activeEl = activeElement(ownerDocument(domReference ?? element));
      if (!relatedTarget && activeEl === domReference) {
        return;
      }
      if (contains(record.dataRef.current.floatingContext?.elements.floating, activeEl) || contains(domReference, activeEl) || movedToFocusGuard) {
        return;
      }
      const nextFocusedElement = relatedTarget ?? activeEl;
      if (isTargetInsideEnabledTrigger(nextFocusedElement, record.triggerElements)) {
        return;
      }
      requestOpenChange(record, channel, false, 'trigger-focus', event);
    });
  };

  const install = () => {
    if (cleanup !== null || focusOptions() === null) {
      return;
    }
    const win = ownerWindow(element);
    record.events.on('openchange', onOpenChange);
    cleanup = mergeCleanups(
      listen(win, 'blur', onWindowBlur),
      isMacSafari && listen(win, 'keydown', () => (keyboardModality = true), true),
      isMacSafari && listen(win, 'pointerdown', () => (keyboardModality = false), true),
      listen(element, 'mouseleave', resetBlockedFocus),
      listen(element, 'focusin', onFocus),
      listen(element, 'focusout', onBlur),
      () => record.events.off('openchange', onOpenChange),
    );
  };

  const uninstall = () => {
    cleanup?.();
    cleanup = null;
    timeout.clear();
  };

  install();
  return {
    update() {
      if (focusOptions() === null) {
        uninstall();
      } else {
        install();
      }
    },
    dispose: uninstall,
  };
}

// #endregion focus

// ---------------------------------------------------------------------------------------------------------------
// #region client-point: useClientPoint (FLT-PTR-07..10, FLT-ALG-25) and the hovered line (PSH-PTR-08/29)
// ---------------------------------------------------------------------------------------------------------------

function setPositionReference(record: SessionRecord, reference: SessionVirtualElement | null): void {
  if (record.positionReference === reference) {
    return;
  }
  record.positionReference = reference;
  for (const listener of [...record.positionListeners]) {
    listener();
  }
}

// The cursor point a virtual reference is built around, and what its later measurements read (useClientPoint.ts:12-18).
interface ClientPointData {
  axis: 'x' | 'y' | 'both';
  dataRef: { readonly current: SessionData };
  pointerType: string | undefined;
  x: number | null;
  y: number | null;
}

/**
 * Creates the virtual reference of cursor anchoring: its first measurement is at the cursor; later ones follow the DOM
 * element by the first offset, or keep the cursor when a mouse hover opened the popup. On the untracked axis it takes
 * the element's coordinate and extent.
 * @param domElement The element measured, or none.
 * @param data The cursor point, the axis, the pointer type and the session data.
 * @returns The virtual reference.
 */
function createVirtualElement(domElement: Element | null | undefined, data: ClientPointData): SessionVirtualElement {
  // FLT-ALG-25: useClientPoint.ts:10-75.
  let offsetX: number | null = null;
  let offsetY: number | null = null;
  let isAutoUpdateEvent = false;

  return {
    contextElement: domElement || undefined,
    getBoundingClientRect() {
      const domRect = domElement?.getBoundingClientRect() || {
        width: 0,
        height: 0,
        x: 0,
        y: 0,
      };

      const isXAxis = data.axis === 'x' || data.axis === 'both';
      const isYAxis = data.axis === 'y' || data.axis === 'both';
      const canTrackCursorOnAutoUpdate =
        ['mouseenter', 'mousemove'].includes(data.dataRef.current.openEvent?.type || '') && data.pointerType !== 'touch';

      const width = data.axis === 'y' ? domRect.width : 0;
      const height = data.axis === 'x' ? domRect.height : 0;
      let x = domRect.x;
      let y = domRect.y;

      if (offsetX == null && data.x && isXAxis) {
        offsetX = domRect.x - data.x;
      }

      if (offsetY == null && data.y && isYAxis) {
        offsetY = domRect.y - data.y;
      }

      x -= offsetX || 0;
      y -= offsetY || 0;

      if (!isAutoUpdateEvent || canTrackCursorOnAutoUpdate) {
        x = isXAxis && data.x != null ? data.x : x;
        y = isYAxis && data.y != null ? data.y : y;
      }

      isAutoUpdateEvent = true;

      return {
        width,
        height,
        x,
        y,
        top: y,
        right: x + width,
        bottom: y + height,
        left: x,
      };
    },
  };
}

function isMouseBasedEvent(event: Event | undefined): event is MouseEvent {
  return event != null && (event as MouseEvent).clientX != null;
}

// The inputs of useClientPoint's main effect as of its last run (useClientPoint.ts:202-212), and of the two `initialRef`
// effects.
interface ClientPointDeps {
  enabled: boolean;
  open: boolean;
  floating: HTMLElement | null;
  domReference: Element | null;
  openCheck: HTMLElement | boolean | null;
  reactive: number;
}

/**
 * useClientPoint on a trigger: while closed, a pointer move or entry on the trigger anchors the popup at the cursor (a
 * virtual reference in the session's `positionReference`); while open, a window `mousemove` listener keeps following
 * the cursor until it lands on the floating element, and a move back on the trigger re-attaches it. A popup opened by a
 * non-mouse event is anchored to the DOM reference instead. Turning the option off anchors to the DOM reference and
 * ignores cursor updates while the popup stays open; disposing clears the position reference. A trigger whose option
 * has never been on leaves the position reference alone.
 */
function createClientPointController(context: HandleContext): Controller {
  const { record } = context;
  const element = context.element as HTMLElement;
  const dataRef = record.dataRef;
  // Whether the hook runs at all: from the first time the option is on, as Base UI calls it only where it is used.
  let mounted = false;
  // Base UI's `initialRef.current`.
  let initial = false;
  // Base UI's `cleanupListenerRef.current`.
  let cleanupListener: Cleanup | null = null;
  let pointerType: string | undefined;
  // Base UI's `reactive` state: a change re-runs the main effect.
  let reactive = 0;
  let effectCleanup: Cleanup | null = null;
  let deps: ClientPointDeps | null = null;
  let referenceListeners: Cleanup | null = null;
  let disposed = false;

  const clientPointOptions = (): ClientPointOptions | null => context.options.trigger?.clientPoint ?? null;

  // `store.set('positionReference', reference)`.
  const resetReference = (reference: Element | null) => {
    setPositionReference(record, reference);
  };

  const setReference = (newX: number | null, newY: number | null, referenceElement?: Element | null) => {
    if (initial) {
      return;
    }

    // FLT-PTR-09: prevent setting if the open event was not a mouse-like one
    // (e.g. focus to open, then hover over the reference element).
    // Only apply if the event exists.
    if (dataRef.current.openEvent && !isMouseBasedEvent(dataRef.current.openEvent)) {
      return;
    }

    setPositionReference(
      record,
      createVirtualElement(referenceElement ?? record.domReferenceElement, {
        x: newX,
        y: newY,
        axis: clientPointOptions()?.axis ?? 'both',
        dataRef,
        pointerType,
      }),
    );
  };

  // The main effect and the `initialRef` effects re-run after the event that changed their inputs (Base UI's re-render).
  const schedule = createDeferredRun(() => sync());

  // FLT-PTR-07: useClientPoint.ts:150-160.
  const handleReferenceEnterOrMove = (event: MouseEvent) => {
    if (!record.open) {
      setReference(event.clientX, event.clientY, event.currentTarget as Element);
    } else if (!cleanupListener) {
      // If there's no cleanup, there's no listener, but we want to ensure
      // we add the listener if the cursor landed on the floating element and
      // then back on the reference (i.e. it's interactive).
      setReference(event.clientX, event.clientY, event.currentTarget as Element);
      reactive += 1;
      schedule();
    }
  };

  const setPointerTypeRef = (event: PointerEvent) => {
    pointerType = event.pointerType;
    schedule();
  };

  // FLT-PTR-08, FLT-PTR-10: useClientPoint.ts:168-212.
  const runEffect = (
    enabled: boolean,
    openCheck: HTMLElement | boolean | null,
    floating: HTMLElement | null,
    domReference: Element | null,
  ): Cleanup | null => {
    if (!enabled) {
      resetReference(domReference);
      return null;
    }

    if (!openCheck) {
      return null;
    }

    function cleanup() {
      cleanupListener?.();
      cleanupListener = null;
    }

    const win = ownerWindow(floating);

    function handleMouseMove(event: MouseEvent) {
      const target = getTarget(event) as Element | null;

      if (!contains(floating, target)) {
        setReference(event.clientX, event.clientY);
      } else {
        cleanup();
      }
    }

    if (!dataRef.current.openEvent || isMouseBasedEvent(dataRef.current.openEvent)) {
      cleanupListener = listen(win, 'mousemove', handleMouseMove);
    } else {
      resetReference(domReference);
    }

    return cleanup;
  };

  const sync = () => {
    if (disposed) {
      return;
    }
    const enabled = clientPointOptions() !== null;
    if (!mounted) {
      if (!enabled) {
        return;
      }
      mounted = true;
    }

    // The prop bag, only while enabled (useClientPoint.ts:234-250).
    if (enabled && referenceListeners === null) {
      referenceListeners = mergeCleanups(
        listen(element, 'pointerdown', setPointerTypeRef),
        listen(element, 'pointerenter', setPointerTypeRef),
        listen(element, 'mousemove', handleReferenceEnterOrMove),
        listen(element, 'mouseenter', handleReferenceEnterOrMove),
      );
    } else if (!enabled && referenceListeners !== null) {
      referenceListeners();
      referenceListeners = null;
    }

    const open = record.open;
    const floating = record.floatingElement;
    const domReference = record.domReferenceElement;
    // If the pointer is a mouse-like pointer, we want to continue following the
    // mouse even if the floating element is transitioning out. On touch
    // devices, this is undesirable because the floating element will move to
    // the dismissal touch point.
    const openCheck = isMouseLikePointerType(pointerType) ? floating : open;
    const previous = deps;
    deps = { enabled, open, floating, domReference, openCheck, reactive };

    if (
      previous === null ||
      previous.openCheck !== openCheck ||
      previous.enabled !== enabled ||
      previous.floating !== floating ||
      previous.domReference !== domReference ||
      previous.reactive !== reactive
    ) {
      effectCleanup?.();
      effectCleanup = runEffect(enabled, openCheck, floating, domReference);
    }

    // useClientPoint.ts:222-232.
    if (previous === null || previous.enabled !== enabled || previous.floating !== floating) {
      if (enabled && !floating) {
        initial = false;
      }
    }

    if (previous === null || previous.enabled !== enabled || previous.open !== open) {
      if (!enabled && open) {
        initial = true;
      }
    }
  };

  const stopListening = onSessionChange(record, sync);
  sync();

  return {
    update: sync,
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      stopListening();
      referenceListeners?.();
      referenceListeners = null;
      effectCleanup?.();
      effectCleanup = null;
      // Clear virtual cursor references when the hook unmounts (FLT-PTR-10).
      if (mounted) {
        setPositionReference(record, null);
      }
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The hovered line: the recording half of utils/popups/inlineRect.ts and preview-card/store/PreviewCardStore.ts:75-98.
// The record is the session's (Base UI's `inlineRectCoordsRef`, one per PreviewCard store); the inline middleware that
// reads it through `readHoveredLine` is positioning's.
// ---------------------------------------------------------------------------------------------------------------

/** The hovered point of a trigger that renders more than one line box, and the index of the line box under it. */
export interface HoveredLine {
  /** The x position in viewport coordinates. */
  x: number;
  /** The y position in viewport coordinates. */
  y: number;
  /** The line index under the pointer when the coordinates were captured. */
  lineIndex?: number | undefined;
  /** The trigger element whose rects produced these coordinates. */
  element: Element;
}

const HOVERED_LINE_SLOT = 'client-point.hovered-line';

interface HoveredLineRef {
  current: HoveredLine | undefined;
}

interface LineRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
}

function hoveredLineRef(record: SessionRecord): HoveredLineRef {
  return sessionSlot<HoveredLineRef>(record, HOVERED_LINE_SLOT, () => ({ current: undefined }));
}

// inlineRect.ts:62-94: the line boxes of a trigger's client rects, those whose tops are within half a line merged.
function getLineRects(rects: ArrayLike<LineRect>): LineRect[] {
  const lines: LineRect[] = [];
  let previousRect: LineRect | undefined;

  for (const rect of Array.from(rects).sort((a, b) => a.top - b.top)) {
    if (!previousRect || rect.top - previousRect.top > previousRect.height / 2) {
      lines.push({
        left: rect.left,
        top: rect.top,
        right: rect.right,
        bottom: rect.bottom,
        width: rect.width,
        height: rect.height,
      });
    } else {
      const line = lines[lines.length - 1]!;
      line.left = Math.min(line.left, rect.left);
      line.right = Math.max(line.right, rect.right);
      line.bottom = Math.max(line.bottom, rect.bottom);
      line.width = line.right - line.left;
      line.height = line.bottom - line.top;
    }

    previousRect = rect;
  }

  return lines;
}

// inlineRect.ts:96-104: the line box under a point, with a 2px tolerance.
function findLineIndex(lines: LineRect[], x: number, y: number): number {
  return lines.findIndex(
    (lineRect) => x > lineRect.left - 2 && x < lineRect.right + 2 && y > lineRect.top - 2 && y < lineRect.bottom + 2,
  );
}

// inlineRect.ts:110-129.
function getInlineRectCoords(element: Element, clientX: number, clientY: number): HoveredLine | undefined {
  const lines = getLineRects(element.getClientRects());

  if (lines.length < 2) {
    return undefined;
  }

  const lineIndex = findLineIndex(lines, clientX, clientY);

  return {
    x: clientX,
    y: clientY,
    lineIndex: lineIndex === -1 ? undefined : lineIndex,
    element,
  };
}

/**
 * Reads the session's hovered line, for the inline middleware.
 * @param session The session.
 * @returns The record, or `undefined` when none was taken or the trigger did not wrap.
 */
export function readHoveredLine(session: InteractionSession): HoveredLine | undefined {
  return ((session as SessionRecord).slots.get(HOVERED_LINE_SLOT) as HoveredLineRef | undefined)?.current;
}

/**
 * Records the hovered line of a trigger at a client point, replacing the session's record.
 * @param session The session.
 * @param element The trigger.
 * @param clientX The point's x in viewport coordinates.
 * @param clientY The point's y in viewport coordinates.
 * @returns The new record: `undefined` when the trigger renders fewer than two line boxes.
 */
export function updateHoveredLine(
  session: InteractionSession,
  element: Element,
  clientX: number,
  clientY: number,
): HoveredLine | undefined {
  // inlineRect.ts:226-235 (`updateInlineRectCoords`).
  const nextCoords = getInlineRectCoords(element, clientX, clientY);
  hoveredLineRef(session as SessionRecord).current = nextCoords;
  return nextCoords;
}

/**
 * Recomputes the hovered line just before the hover region reports a hover open, unless the record already belongs to
 * this trigger: the trigger's own listeners ignore moves while the popup is open by it. A no-op unless the trigger
 * records the hovered line.
 */
function recordHoveredLineBeforeOpen(context: HandleContext, event: MouseEvent): void {
  // PSH-PTR-29: PreviewCardStore.ts:78-96; the hover region calls this for a `trigger-hover` open of this trigger.
  const trigger = context.element;
  if (context.options.trigger?.hoveredLine !== true || trigger === null) {
    return;
  }
  if ('clientX' in event && 'clientY' in event && hoveredLineRef(context.record).current?.element !== trigger) {
    updateHoveredLine(context.record, trigger, event.clientX, event.clientY);
  }
}

/**
 * The hovered line on a trigger (`getInlineRectTriggerProps`): `mouseenter` and `mousemove` record it while the popup is
 * not open by this trigger, and a focus clears it.
 */
function createHoveredLineController(context: HandleContext): Controller {
  const { record } = context;
  const element = context.element as HTMLElement;
  let cleanup: Cleanup | null = null;

  const isOpenedByThisTrigger = () => record.open && record.domReferenceElement === element;

  // PSH-PTR-08: inlineRect.ts:203-224.
  function updateCoordsIfClosed(event: MouseEvent) {
    if (!isOpenedByThisTrigger()) {
      updateHoveredLine(record, event.currentTarget as Element, event.clientX, event.clientY);
    }
  }

  function onFocus() {
    hoveredLineRef(record).current = undefined;
  }

  const install = () => {
    if (cleanup !== null || context.options.trigger?.hoveredLine !== true) {
      return;
    }
    cleanup = mergeCleanups(
      listen(element, 'focusin', onFocus),
      listen(element, 'mouseenter', updateCoordsIfClosed),
      listen(element, 'mousemove', updateCoordsIfClosed),
    );
  };

  const uninstall = () => {
    cleanup?.();
    cleanup = null;
  };

  // Called by the hover region before it reports a hover open.
  void recordHoveredLineBeforeOpen;

  install();
  return {
    update() {
      if (context.options.trigger?.hoveredLine === true) {
        install();
      } else {
        uninstall();
      }
    },
    dispose: uninstall,
  };
}

// #endregion client-point

// ---------------------------------------------------------------------------------------------------------------
// Handles
// ---------------------------------------------------------------------------------------------------------------

type ControllerFactory = (context: HandleContext) => Controller;

// Builds a handle over its controllers: `update` stores the options, then tells each controller; `dispose` is idempotent
// and a repeated `attach` of the same element is the caller's (ADR 06 Amendments 2026-10-18 rule 2).
function composeHandle(
  record: SessionRecord,
  element: HTMLElement | null,
  initialOptions: InteractionsOptions,
  twinReference: DotNetTwin,
  factories: ControllerFactory[],
  onUpdate: (options: InteractionsOptions, previous: InteractionsOptions) => void,
  onDispose: () => void,
): TwinHandle {
  const channel = createTwinChannel(twinReference);
  const context = { record, element, channel, twinReference, options: initialOptions };
  const controllers = factories.map((factory) => factory(context));
  let disposed = false;
  return {
    update(optionsJson) {
      if (disposed) {
        return;
      }
      const previous = context.options;
      context.options = parseOptions<InteractionsOptions>(optionsJson);
      onUpdate(context.options, previous);
      for (const controller of controllers) {
        controller.update?.(previous);
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      for (const controller of controllers) {
        controller.dispose();
      }
      onDispose();
      releaseSession(record);
    },
  };
}

function attachSession(token: string, initialOptions: InteractionsOptions, twinReference: DotNetTwin): TwinHandle {
  const record = acquireRecord(token);
  applySessionOptions(record, initialOptions);
  let channel: TwinChannel | null = null;
  const handle = composeHandle(
    record,
    null,
    initialOptions,
    twinReference,
    [
      (context) => {
        channel = context.channel;
        record.channel = context.channel;
        return { dispose() {} };
      },
      createSessionDismissController,
    ],
    (options) => applySessionOptions(record, options),
    () => {
      if (record.channel === channel) {
        record.channel = null;
      }
    },
  );
  return handle;
}

function attachTrigger(element: HTMLElement, token: string, initialOptions: InteractionsOptions, twinReference: DotNetTwin): TwinHandle {
  const record = acquireRecord(token);
  let triggerId = initialOptions.trigger?.triggerId ?? element.id;
  record.triggers.set(triggerId, element);
  const handle = composeHandle(
    record,
    element,
    initialOptions,
    twinReference,
    [
      createReferenceDismissController,
      createClickController,
      createHoverReferenceController,
      createDelayGroupController,
      createFocusController,
      createClientPointController,
      createHoveredLineController,
    ],
    (options) => {
      const nextId = options.trigger?.triggerId ?? element.id;
      if (nextId !== triggerId) {
        if (record.triggers.get(triggerId) === element) {
          record.triggers.delete(triggerId);
        }
        triggerId = nextId;
        record.triggers.set(triggerId, element);
        notifySessionChange(record);
      }
    },
    () => {
      if (record.triggers.get(triggerId) === element) {
        record.triggers.delete(triggerId);
        notifySessionChange(record);
      }
    },
  );
  notifySessionChange(record);
  return handle;
}

function attachFloating(element: HTMLElement, token: string, initialOptions: InteractionsOptions, twinReference: DotNetTwin): TwinHandle {
  const record = acquireRecord(token);
  record.floatingElement = element;
  record.hasNode = initialOptions.floating?.node !== false;
  // FLT-STR-05, FLT-STR-08: the latest floating context, never cleared once published (FLT §10 Q13, replicated).
  record.dataRef.current.floatingContext = record.context;
  const handle = composeHandle(
    record,
    element,
    initialOptions,
    twinReference,
    [createFloatingDismissController, createHoverFloatingController],
    (options) => {
      record.hasNode = options.floating?.node !== false;
    },
    () => {
      if (record.floatingElement === element) {
        record.floatingElement = null;
        record.hasNode = false;
        notifySessionChange(record);
      }
    },
  );
  notifySessionChange(record);
  return handle;
}

/**
 * Attaches the module. The token form (`elementOrId` `null`) attaches an elementless handle, a popup Root's session or a
 * TooltipProvider's group, by the token in the options; the element and id forms attach a trigger or a floating element.
 * @param elementOrId The element, its id, or `null` for the token form.
 * @param optionsJson The JSON of an `InteractionsOptions` with exactly one role set.
 * @param twinReference The twin's `DotNetObjectReference`.
 * @param _references The element-typed option values passed after the options; the module's options carry none.
 * @param captures The capture ids C# sends beside the element and the references, which find them inside a shadow root.
 * @returns The handle.
 */
export function attach(
  elementOrId: AttachTarget,
  optionsJson: string,
  twinReference: DotNetTwin,
  _references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const options = parseOptions<InteractionsOptions>(optionsJson);
  if (options.provider) {
    return attachProvider(options.provider.token ?? '', options, twinReference);
  }
  const token = options.sessionToken ?? '';
  const element = attachTarget(elementOrId, captures);
  if (element === null) {
    return attachSession(token, options, twinReference);
  }
  if (options.floating) {
    return attachFloating(element, token, options, twinReference);
  }
  return attachTrigger(element, token, options, twinReference);
}
