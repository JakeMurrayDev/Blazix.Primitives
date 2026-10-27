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

/** A trigger's membership in its provider's group. */
function createDelayGroupController(_context: HandleContext): Controller {
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

function createHoveredLineController(_context: HandleContext): Controller {
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
