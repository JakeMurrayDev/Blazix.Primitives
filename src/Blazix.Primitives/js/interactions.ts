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
import { decide } from './decide';
import { activeElement, closest, contains, getTarget, isElement, isHTMLElement, ownerDocument, ownerWindow, platform } from './dom';
import { eventInfo } from './event-info';
import { cancelFrame, requestFrame } from './transitions';
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

/** The press type an outside press is read as (FLT-API-02). */
export type PressType = 'intentional' | 'sloppy';

/** The options of `useDismiss` on a session (FLT §6.4); `null` turns dismissal off. */
export interface DismissOptions {
  /** Whether Escape closes the popup (FLT-KEY-01); `true` when absent. */
  escapeKey?: boolean | null;
  /** Whether an outside press closes the popup (FLT-DIS-09); `true` when absent. */
  outsidePress?: boolean | null;
  /** The press type per pointer type (FLT-API-02); `sloppy` for both when absent. */
  outsidePressEvent?: { mouse?: PressType | null; touch?: PressType | null } | null;
  /** Whether Escape and outside presses bubble through the tree (FLT-API-01); `{ escapeKey: false, outsidePress: true }` when absent. */
  bubbles?: { escapeKey?: boolean | null; outsidePress?: boolean | null } | null;
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
  /** Whether the popup is in its closing transition (`transitionStatus === 'ending'`, FLT-PTR-13). */
  ending?: boolean | null;
  /** Dismissal; `null` turns it off. */
  dismiss?: DismissOptions | null;
}

/** The options of `useClick` on a trigger (FLT §6.3); `null` turns it off. */
export interface ClickOptions {
  /** Which pointer event toggles (FLT-PTR-02/03); `click` when absent. */
  event?: 'click' | 'mousedown' | 'mousedown-only' | null;
  /** Whether a repeated press closes (FLT-PTR-05); `true` when absent. */
  toggle?: boolean | null;
  /** Whether strictly mouse-like presses are dropped. */
  ignoreMouse?: boolean | null;
  /** Whether a press keeps a hover- or focus-opened popup open (FLT-PTR-06); `true` when absent. */
  stickIfOpen?: boolean | null;
  /** Whether `stickIfOpen` holds only within the patient-click window after a hover open (Popover). */
  patientClick?: boolean | null;
  /** The touch-only open deferral in milliseconds (FLT-PTR-04). */
  touchOpenDelay?: number | null;
  /** The reason of the request; `trigger-press` when absent. */
  reason?: 'trigger-press' | 'input-press' | null;
}

/** The options of `useHoverReferenceInteraction` on a trigger (FLT §6.7, §6.9); `null` turns it off. */
export interface HoverOptions {
  /** The open delay in milliseconds. */
  delayOpen?: number | null;
  /** The close delay in milliseconds. */
  delayClose?: number | null;
  /** The rest time before a hover opens (FLT-PTR-16). */
  restMs?: number | null;
  /** Whether the first `mousemove` also opens; `true` when absent. */
  move?: boolean | null;
  /** Whether only mouse-like pointers hover (FLT-PTR-11). */
  mouseOnly?: boolean | null;
  /** Whether leaving the trigger is judged by the safe polygon (FLT-ALG-15). */
  safePolygon?: boolean | null;
  /** The safe polygon's `blockPointerEvents` (FLT-DOM-19). */
  blockPointerEvents?: boolean | null;
  /** Whether a `mouseout` cancels a pending open (FLT-PTR-18). */
  guardStaleOpen?: boolean | null;
  /** Whether a nested tooltip trigger (`data-base-ui-tooltip-trigger`) vetoes the hover (Tooltip's `shouldOpen`). */
  nestedTooltipTriggers?: boolean | null;
  /** The provider token whose delay group the trigger belongs to (FLT-ALG-23). */
  delayGroupToken?: string | null;
}

/** The options of `useFocus` on a trigger (FLT §6.5); `null` turns it off. */
export interface FocusOptions {
  /** The open delay in milliseconds (FLT-FOC-01). */
  delay?: number | null;
}

/** The options of `useClientPoint` on a trigger (FLT §6.6); `null` turns it off. */
export interface ClientPointOptions {
  /** The tracked axis (FLT-API-14); `both` when absent. */
  axis?: 'x' | 'y' | 'both' | null;
}

/** The options of a `trigger` handle. */
export interface TriggerRoleOptions {
  /** The trigger's id in the session's trigger map; the element's `id` when absent. */
  triggerId?: string | null;
  click?: ClickOptions | null;
  /** Whether a press on the trigger closes the open popup (FLT-PTR-30). */
  referencePress?: boolean | null;
  /** Whether a press on a closed popup's trigger cancels a pending hover open on the bus alone (Tooltip). */
  cancelPendingOpenOnPress?: boolean | null;
  hover?: HoverOptions | null;
  focus?: FocusOptions | null;
  clientPoint?: ClientPointOptions | null;
  /** Whether the trigger records the hovered line for the inline middleware (PreviewCard). */
  hoveredLine?: boolean | null;
}

/** The options of `useHoverFloatingInteraction` on a floating element (FLT §6.8); `null` turns it off. */
export interface FloatingHoverOptions {
  /** The close delay in milliseconds after the pointer leaves the floating element (FLT-PTR-24). */
  closeDelay?: number | null;
}

/** The options of a `floating` handle. */
export interface FloatingRoleOptions {
  /** Whether the session registers a tree node for this floating element (FLT-STR-07); `true` when absent. */
  node?: boolean | null;
  hover?: FloatingHoverOptions | null;
}

/** The options of a `provider` handle (a TooltipProvider, FLT §6.9). */
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
  /** The hook regions' private per-session state (`sessionSlot`). */
  slots: Map<string, unknown>;
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
    slots: new Map(),
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

/**
 * A request a handle sends (ADR 04 Amendments 2026-10-27): recorded for the relay's pairing, then reported on the
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
  const escapeKeyBubbles = normalizeBubbles(options.bubbles).escapeKey;
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
  // A close is pending until the relay brings it; a cancel the verdict reports leaves the session armed.
  if (accepted) {
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
    const bubbles = normalizeBubbles(options.bubbles);
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
        requestOpenChange(record, context.channel, false, 'outside-press', event);
        state.disarmed = true;
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
      // A cancelled gesture produces no click. Not cleared on `pointerup`: the click fires after it.
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
    const bubbles = normalizeBubbles(options?.bubbles);
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

function createClickController(_context: HandleContext): Controller {
  void createFrameRequest;
  return { dispose() {} };
}

// #endregion click

// ---------------------------------------------------------------------------------------------------------------
// #region hover: the hover pair, the shared hover state and safePolygon (FLT-PTR-11..25, FLT-ALG-15, FLT-ALG-24)
// ---------------------------------------------------------------------------------------------------------------

function createHoverReferenceController(_context: HandleContext): Controller {
  return { dispose() {} };
}

function createHoverFloatingController(_context: HandleContext): Controller {
  return { dispose() {} };
}

// #endregion hover

// ---------------------------------------------------------------------------------------------------------------
// #region delay-group: FloatingDelayGroup and useDelayGroup (FLT-ALG-23, FLT-EVT-07, FLT-ANI-01)
// ---------------------------------------------------------------------------------------------------------------

/** One group per provider token: the sanctioned page-global state of the module beside the session map. */
interface DelayGroup {
  token: string;
}

const delayGroups = new Map<string, DelayGroup>();

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

/** What a member trigger's hover reads from its group at `mouseenter` (FLT-API-16). */
interface DelayGroupReading {
  /** The group's open delay now: 0 while another member is open (or the group is warm), else the provider's. */
  delayOpen: number | undefined;
  /** The provider's close delay. */
  delayClose: number | undefined;
}

/**
 * Reads the group a trigger's hover belongs to.
 * @returns The reading, or `null` without a provider of that token.
 */
function readDelayGroup(_token: string | null | undefined): DelayGroupReading | null {
  return null;
}

/**
 * The instant phase an open request of this session carries (FLT-ALG-23): whether another member of the group is the
 * current one, so C# renders `data-instant="delay"` in the batch of `data-open`.
 */
function delayGroupInstantPhase(_record: SessionRecord, _token: string | null | undefined): boolean {
  return false;
}

/** A trigger's membership in its provider's group. */
function createDelayGroupController(_context: HandleContext): Controller {
  void readDelayGroup;
  void delayGroupInstantPhase;
  return { dispose() {} };
}

// #endregion delay-group

// ---------------------------------------------------------------------------------------------------------------
// #region focus: useFocus (FLT-FOC-01..04, FLT-API-15)
// ---------------------------------------------------------------------------------------------------------------

// FLT-A11Y-06: Mac Safari moves no focus on a pointer press, so modality is read from the last keydown or pointerdown.
const isMacSafari = platform.os.mac && platform.engine.webkit;

// The `data-base-ui-focus-guard` attribute and its `data-type="outside"` (portal guards), read by the blur close.
const FOCUS_GUARD_ATTRIBUTE = 'data-base-ui-focus-guard';

/**
 * useFocus on a trigger (useFocus.ts): a focus opens the popup after `delay` (FLT-FOC-01); a window blur while the
 * reference holds focus, and a press or Escape close, block the next focus on that reference (FLT-FOC-02/03); a blur
 * closes it after 0 ms unless focus stayed in the reference, moved into the floating element, onto an outside focus
 * guard or onto another enabled trigger (FLT-FOC-04). React's `onFocus`/`onBlur` are `focusin`/`focusout`.
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
    if ((record.open && movedFromOtherEnabledTrigger) || !delay) {
      requestOpenChange(record, channel, true, 'trigger-focus', event, element);
      return;
    }
    timeout.start(delay, () => {
      if (blockFocus) {
        return;
      }
      requestOpenChange(record, channel, true, 'trigger-focus', event, element);
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

function createClientPointController(_context: HandleContext): Controller {
  void setPositionReference;
  return { dispose() {} };
}

/**
 * Recomputes the hovered line just before the hover region reports an open (PSH-PTR-08/29); a no-op unless the
 * trigger records it.
 */
function recordHoveredLineBeforeOpen(_context: HandleContext, _event: MouseEvent): void {}

function createHoveredLineController(_context: HandleContext): Controller {
  void recordHoveredLineBeforeOpen;
  return { dispose() {} };
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
  return composeHandle(
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
      }
    },
    () => {
      if (record.triggers.get(triggerId) === element) {
        record.triggers.delete(triggerId);
      }
    },
  );
}

function attachFloating(element: HTMLElement, token: string, initialOptions: InteractionsOptions, twinReference: DotNetTwin): TwinHandle {
  const record = acquireRecord(token);
  record.floatingElement = element;
  record.hasNode = initialOptions.floating?.node !== false;
  // FLT-STR-05, FLT-STR-08: the latest floating context, never cleared once published (FLT §10 Q13, replicated).
  record.dataRef.current.floatingContext = record.context;
  return composeHandle(
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
      }
    },
  );
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
