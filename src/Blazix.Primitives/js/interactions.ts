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
import { closest, contains, isElement, isHTMLElement, platform } from './dom';
import { createTwinChannel, parseOptions, resolveElement, type DotNetTwin, type TwinChannel, type TwinHandle, type TwinOptions } from './twin';

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

/**
 * The last committed open change, relayed by C# in `update` (ADR 04 Amendments 2026-10-27); mirrors the popup store's
 * relay record. The session emits `openchange` when `revision` moves.
 */
export interface OpenChangeRelay {
  revision: number;
  reason: string;
  eventKind?: string | null;
  eventType?: string | null;
  pointerType?: string | null;
  triggerId?: string | null;
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
  /** Whether the popup is in its closing transition (`transitionStatus === 'ending'`). */
  ending?: boolean | null;
}

/** The options of a `trigger` handle. */
export interface TriggerRoleOptions {
  /** The trigger's id in the session's trigger map; the element's `id` when absent. */
  triggerId?: string | null;
}

/** The options of a `floating` handle. */
export interface FloatingRoleOptions {
  /** Whether the session registers a tree node for this floating element (FLT-STR-07). */
  node?: boolean | null;
}

/** The options of a `provider` handle (a TooltipProvider). */
export interface ProviderRoleOptions {
  /** The provider token the group is keyed by. */
  token?: string | null;
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

/**
 * Determines whether an event is click-like (FLT-STA-04).
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

/**
 * Determines whether an element matches `:focus-visible` (FLT-A11Y-08).
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

/** A tree node's context (FLT-STR-08), written by the session from its floating handle. */
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

/**
 * Returns the descendants of a node, depth first (FLT-ALG-20).
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

/**
 * Returns the ancestors of a node, nearest first (FLT-ALG-20).
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

/** Base UI's `dataRef.current` (FLT-STR-05). A module that adds a key owns it. */
export interface SessionData {
  openEvent?: Event | undefined;
  floatingContext?: SessionFloatingContext | undefined;
  insideReactTree?: boolean;
  __escapeKeyBubbles?: boolean;
  __outsidePressBubbles?: boolean;
  [key: string]: unknown;
}

/** The payload of the session bus's `openchange` (FLT-STA-02). */
export interface SessionOpenChangeDetails {
  open: boolean;
  reason: string;
  nativeEvent: Event | undefined;
  nested: boolean;
  triggerElement: Element | undefined;
}

/** The triggers registered in a session, by id (PSH-STA-33's map). */
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
  pending: PendingRequest | null;
  triggers: Map<string, Element>;
  /** The channel of the session handle (the Root's twin), which answers `escape`. */
  channel: TwinChannel | null;
  positionListeners: Set<() => void>;
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
    pending: null,
    triggers,
    channel: null,
    positionListeners: new Set(),
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

const nodeCache = new WeakMap<SessionRecord, SessionNode>();

function nodeOf(record: SessionRecord): SessionNode {
  const cached = nodeCache.get(record);
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
  nodeCache.set(record, node);
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
    record.lastRevision = relay.revision;
    if (!first || relay.revision > 0) {
      const event = relayedEvent(record, open, relay);
      record.pending = null;
      syncOpenEvent(record, open, event);
      const details: SessionOpenChangeDetails = {
        open,
        reason: relay.reason,
        nativeEvent: event,
        nested: record.nested,
        triggerElement: relay.triggerId != null ? record.triggers.get(relay.triggerId) : undefined,
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
}

// ---------------------------------------------------------------------------------------------------------------
// The delay groups (FLT-ALG-23): one per provider token, sanctioned page-global state
// ---------------------------------------------------------------------------------------------------------------

interface DelayGroup {
  token: string;
}

const delayGroups = new Map<string, DelayGroup>();

// ---------------------------------------------------------------------------------------------------------------
// Handles
// ---------------------------------------------------------------------------------------------------------------

function attachSession(token: string, initialOptions: InteractionsOptions, twinReference: DotNetTwin): TwinHandle {
  const record = acquireRecord(token);
  const channel = createTwinChannel(twinReference);
  let disposed = false;
  record.channel = channel;
  applySessionOptions(record, initialOptions);

  return {
    update(optionsJson) {
      if (disposed) {
        return;
      }
      applySessionOptions(record, parseOptions<InteractionsOptions>(optionsJson));
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      if (record.channel === channel) {
        record.channel = null;
      }
      releaseSession(record);
    },
  };
}

function attachTrigger(element: HTMLElement, token: string, initialOptions: InteractionsOptions, _twinReference: DotNetTwin): TwinHandle {
  const record = acquireRecord(token);
  let disposed = false;
  let triggerId = initialOptions.trigger?.triggerId ?? element.id;
  record.triggers.set(triggerId, element);

  return {
    update(optionsJson) {
      if (disposed) {
        return;
      }
      const options = parseOptions<InteractionsOptions>(optionsJson);
      const nextId = options.trigger?.triggerId ?? element.id;
      if (nextId !== triggerId) {
        if (record.triggers.get(triggerId) === element) {
          record.triggers.delete(triggerId);
        }
        triggerId = nextId;
        record.triggers.set(triggerId, element);
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      if (record.triggers.get(triggerId) === element) {
        record.triggers.delete(triggerId);
      }
      releaseSession(record);
    },
  };
}

function attachFloating(element: HTMLElement, token: string, initialOptions: InteractionsOptions, _twinReference: DotNetTwin): TwinHandle {
  const record = acquireRecord(token);
  let disposed = false;
  record.floatingElement = element;
  record.hasNode = initialOptions.floating?.node !== false;
  // FLT-STR-05, FLT-STR-08: the latest floating context, never cleared once published (FLT §10 Q13, replicated).
  record.dataRef.current.floatingContext = record.context;

  return {
    update(optionsJson) {
      if (disposed) {
        return;
      }
      const options = parseOptions<InteractionsOptions>(optionsJson);
      record.hasNode = options.floating?.node !== false;
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      if (record.floatingElement === element) {
        record.floatingElement = null;
        record.hasNode = false;
      }
      releaseSession(record);
    },
  };
}

function attachProvider(token: string, _initialOptions: InteractionsOptions, _twinReference: DotNetTwin): TwinHandle {
  let group = delayGroups.get(token);
  if (!group) {
    group = { token };
    delayGroups.set(token, group);
  }
  const owned = group;
  let disposed = false;
  return {
    update() {},
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      if (delayGroups.get(token) === owned) {
        delayGroups.delete(token);
      }
    },
  };
}

/**
 * Attaches the module. The token form (`elementOrId` `null`) attaches an elementless handle, a popup Root's session or a
 * TooltipProvider's group, by the token in the options; the element and id forms attach a trigger or a floating element.
 * @param elementOrId The element, its id, or `null` for the token form.
 * @param optionsJson The JSON of an `InteractionsOptions` with exactly one role set.
 * @param twinReference The twin's `DotNetObjectReference`.
 * @returns The handle.
 */
export function attach(elementOrId: Element | string | null, optionsJson: string, twinReference: DotNetTwin): TwinHandle {
  const options = parseOptions<InteractionsOptions>(optionsJson);
  if (options.provider) {
    return attachProvider(options.provider.token ?? '', options, twinReference);
  }
  const token = options.sessionToken ?? '';
  if (elementOrId === null) {
    return attachSession(token, options, twinReference);
  }
  const element = resolveElement(elementOrId);
  if (options.floating) {
    return attachFloating(element, token, options, twinReference);
  }
  return attachTrigger(element, token, options, twinReference);
}
