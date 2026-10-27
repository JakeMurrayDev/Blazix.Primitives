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

/** The session's half: the document listeners for Escape and outside presses. */
function createSessionDismissController(_context: HandleContext): Controller {
  void sessionSlot;
  return { dispose() {} };
}

/** The floating element's half: the inside-press marks and the floating `keydown`. */
function createFloatingDismissController(_context: HandleContext): Controller {
  return { dispose() {} };
}

/** The trigger's half: the reference press and the reference `keydown`. */
function createReferenceDismissController(_context: HandleContext): Controller {
  return { dispose() {} };
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

  // The relay runs before the session applies the commit, so `record.open` and the anchor are still the previous ones
  // here: the effects re-run when the commit changes either. An emission that changes neither (the press cancel on a
  // closed popup) runs nothing, as it re-renders nothing in Base UI.
  const onOpenChange = (details: SessionOpenChangeDetails) => {
    const domReference = details.open && details.triggerElement ? details.triggerElement : record.domReferenceElement;
    const openChanged = details.open !== record.open;
    if (openChanged || domReference !== record.domReferenceElement) {
      runLayoutEffects(details.open, domReference, openChanged);
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

  record.events.on('openchange', onOpenChange);
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
      record.events.off('openchange', onOpenChange);
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
