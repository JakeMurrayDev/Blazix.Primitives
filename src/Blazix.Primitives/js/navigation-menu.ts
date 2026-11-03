// The `navigation-menu` component module (ADR 04 §1's component row; the `navigation-menu` row of Amendments 2026-10-18,
// widened by Amendments 2026-11-03 (4), chosen by the user 2026-11-03; contract docs/components/navigation-menu.md).
// One handle per part that owns a `NavigationMenuTwin`, each joined to its Root's menu through the Root element, which
// arrives as the first reference of every handle but the Root's (`rootRef`). The menu's shared state lives on that
// element under a symbol (the `tabs` and `styleWriter` precedent), so the module keeps no page-global state.
// - The size pass (NAV §9.2–9.4): the close-size freeze, the activation's size transition (*current* before the
//   activation, *measured* after the new Content is in place), the shared auto-size reset owner, the Content observer,
//   the window resize and the remembered size, written through `dom`'s `styleWriter` in its `auto-resize` band
//   (`--popup-*` on the Popup, `--positioner-*` on the Positioner; ADR 04 Amendments 2026-10-18 and 2026-10-20).
// - The activation path (NAV-PTR-18, STA-06): the direction from the previous and the new triggers' boxes, and a
//   report, no veto (design point 4, sent 2026-11-04): synchronous on WebAssembly, so C#'s batch renders inside the
//   dispatch as Base UI's `flushSync` does, then the size pass runs in the same task; on Server the module holds
//   *current* and runs the size transition when the applied batch shows the trigger active and its Content in the
//   Viewport (design point 3, sent 2026-11-04).
// - The open key with `stopEvent` (NAV-KEY-01, 02, I18N-01; never for nested or disabled triggers), the List's
//   propagation guard (KEY-06), the four focus guards' routing (KEY-10..12, 15; FOC-04) over `focus-manager`'s tabbable
//   exports, `viewportInert`'s reports (STA-09, FOC-07), the VoiceOver swap (ARIA-05), the `relatedTarget` checks over
//   the `interactions` session tree (FOC-10, 11, KEY-13), focus return (FOC-05, 06), the Positioner's focus in and out
//   (FOC-08), the Content's focus flag and its exiting `inert` (FOC-09, DOM-17), the Popup's anchoring styles (DOM-31)
//   and the Positioner's resize report (ANI-14, DOM-28; design point 6, sent 2026-11-04).
// Transcribed from Base UI v1.9.0: navigation-menu/trigger/NavigationMenuTrigger.tsx:113-430 (the size pass), 554-709
// (activation, keys, blur), 737-782 (the trigger guards); root/NavigationMenuRoot.tsx:122-200 (focus return, the
// freeze); viewport/NavigationMenuViewport.tsx:21-131; positioner/NavigationMenuPositioner.tsx:83-106, 141-168;
// popup/NavigationMenuPopup.tsx:40-62; content/NavigationMenuContent.tsx:111-133; list/NavigationMenuList.tsx:73-89;
// link/NavigationMenuLink.tsx:49-63; utils/isOutsideMenuEvent.ts; utils/setSharedFixedSize.ts.
import {
  activeElement,
  contains,
  getCssDimensions,
  getTarget,
  isHTMLElement,
  ownerDocument,
  ownerWindow,
  styleWriter,
  type Dimensions,
  type StyleLayer,
} from './dom';
import { eventInfo } from './event-info';
import { getNextTabbable, getPreviousTabbable, getTabbableNearElement, isOutsideEvent, disableFocusInside, enableFocusInside } from './focus-manager';
import { getNodeChildren, getSession } from './interactions';
import { platform } from './platform';
import { getPopupAnchoringStyles, type PhysicalSide } from './popup';
import { createAnimationsWatcher, createFrameSlot, ENDING_STYLE, STARTING_STYLE, type AnimationsWatcher, type FrameSlot } from './transitions';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
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

/** The part literals of the handles. */
export const ROOT_PART = 'root';
export const TRIGGER_PART = 'trigger';
export const LIST_PART = 'list';
export const LINK_PART = 'link';
export const CONTENT_PART = 'content';
export const POSITIONER_PART = 'positioner';
export const POPUP_PART = 'popup';
export const VIEWPORT_PART = 'viewport';
export const ARROW_PART = 'arrow';

// ADR 04 Amendments 2026-11-04 (chosen by the user 2026-11-04): the consumer's onKeyDown veto of the open key, the
// composite meaning of the name, fallback run.
/** The decision point asked before the open key is prevented, stopped and reported. */
export const NAVIGATE_POINT = 'navigate';

/** The report of an activation (`OnActivate`), and the synchronous invocation of the same name on WebAssembly. */
export const ACTIVATE_REPORT = 'OnActivate';
/** The report of a blur that leaves the menu (`focus-out`). */
export const FOCUS_OUT_REPORT = 'OnFocusOut';
/** The report of a change the module made to `viewportInert`. */
export const VIEWPORT_INERT_REPORT = 'OnViewportInertChange';
/** The report of a window resize while open, once per animation frame. */
export const RESIZE_REPORT = 'OnResize';
/** The report of the inline Viewport target's finished exit, the second completion watcher. */
export const VIEWPORT_EXIT_REPORT = 'OnExitComplete';

/** The CSS variables of the size pass. */
export const POPUP_WIDTH = '--popup-width';
export const POPUP_HEIGHT = '--popup-height';
export const POSITIONER_WIDTH = '--positioner-width';
export const POSITIONER_HEIGHT = '--positioner-height';

const FOCUS_GUARD = 'data-base-ui-focus-guard';
const ACTIVE_TRIGGER = 'data-popup-open';
const DEFAULT_SIZE: Dimensions = { width: 0, height: 0 };

/** The axis the top-level List runs along. */
export type NavigationMenuOrientation = 'horizontal' | 'vertical';

/** The direction from the previous active trigger to the new one. */
export type ActivationDirection = 'left' | 'right' | 'up' | 'down';

/** The reason an activation requests the value with. */
export type ActivationReason = 'trigger-hover' | 'trigger-press' | 'list-navigation';

/** The options of a `navigation-menu` handle; mirrors `NavigationMenuOptions` in C#. */
export interface NavigationMenuOptions extends TwinOptions {
  /** The part the handle is attached to. */
  part:
    | typeof ROOT_PART
    | typeof TRIGGER_PART
    | typeof LIST_PART
    | typeof LINK_PART
    | typeof CONTENT_PART
    | typeof POSITIONER_PART
    | typeof POPUP_PART
    | typeof VIEWPORT_PART
    | typeof ARROW_PART;
  /** The index of the Root element in the references; every part but the Root's names it. */
  rootRef?: number | null;
  // The Root's options.
  /** The top-level List's axis. */
  orientation?: NavigationMenuOrientation;
  /** Whether the reading direction is right to left. */
  rtl?: boolean;
  /** Whether the Root is nested in another Root's Content. */
  nested?: boolean;
  /** Whether the popup is mounted, as C# last committed it. */
  mounted?: boolean;
  /** The reason of the latest close request C# recorded, or `null`. */
  closeReason?: string | null;
  /** The Root's `interactions` session token, whose tree the blur checks read. */
  sessionToken?: string | null;
  /** The count of this menu's `focus-out` reports C# has answered, committed or canceled. */
  answered?: number;
  // The Trigger's options.
  /** Whether the Trigger's Item renders a Content; without one the size transition does not wait for it. */
  hasContent?: boolean;
  // The Viewport's options.
  /** Whether the Viewport is inside a Positioner. */
  hasPositioner?: boolean;
}

/** What an activation reports. */
export interface ActivationReport {
  /** The reason; `null` when the activation requests nothing (the menu is closed). */
  reason: ActivationReason | null;
  /** The new direction, or `null` to keep the current one. */
  direction: ActivationDirection | null;
  event: ReturnType<typeof eventInfo>;
}

// ---------------------------------------------------------------------------------------------------------------
// Pure helpers
// ---------------------------------------------------------------------------------------------------------------

// NAV-STA-06; NavigationMenuTrigger.tsx:556-569.
/**
 * Computes the activation direction from the previous active trigger to the new one.
 * @param previous The previous trigger's box, or `null`.
 * @param next The new trigger's box.
 * @param orientation The List's axis.
 * @returns The direction, or `null` when the compared coordinate is equal (the direction is kept).
 */
export function computeActivationDirection(previous: DOMRect | null, next: DOMRect, orientation: NavigationMenuOrientation): ActivationDirection | null {
  if (previous === null) {
    return null;
  }
  if (orientation === 'horizontal' && next.left !== previous.left) {
    return next.left > previous.left ? 'right' : 'left';
  }
  if (orientation === 'vertical' && next.top !== previous.top) {
    return next.top > previous.top ? 'down' : 'up';
  }
  return null;
}

// NAV-KEY-01, 02, I18N-01; NavigationMenuTrigger.tsx:684-688.
/**
 * Determines whether a key is the open key of a top-level Trigger.
 * @param key The `KeyboardEvent.key`.
 * @param orientation The List's axis.
 * @param rtl Whether the reading direction is right to left.
 * @returns `true` for ArrowDown when horizontal, for ArrowRight (ArrowLeft in rtl) when vertical.
 */
export function isOpenKey(key: string, orientation: NavigationMenuOrientation, rtl: boolean): boolean {
  if (orientation === 'horizontal') {
    return key === 'ArrowDown';
  }
  return key === (rtl ? 'ArrowLeft' : 'ArrowRight');
}

// NAV-KEY-06; NavigationMenuList.tsx:76-83.
/**
 * Determines whether the top-level List stops a key's propagation.
 * @param key The `KeyboardEvent.key`.
 * @param orientation The List's axis.
 * @returns `true` for the arrows of the List's axis.
 */
export function isListGuardKey(key: string, orientation: NavigationMenuOrientation): boolean {
  return orientation === 'horizontal' ? key === 'ArrowLeft' || key === 'ArrowRight' : key === 'ArrowUp' || key === 'ArrowDown';
}

// NAV-DOM-31; NavigationMenuPopup.tsx:40-47.
/**
 * Resolves the physical side the Popup grows away from.
 * @param side The Positioner's resolved `data-side`.
 * @param rtl Whether the reading direction is right to left.
 * @returns `top` or `left` when the Popup is pinned; `bottom` otherwise.
 */
export function getPhysicalSide(side: string | null, rtl: boolean): PhysicalSide {
  if (side === 'top') {
    return 'top';
  }
  const left = side === 'left' || (side === 'inline-start' && !rtl) || (side === 'inline-end' && rtl);
  return left ? 'left' : 'bottom';
}

// NAV-ANI-03; NavigationMenuRoot.tsx:39-57.
function readPositionerFixedSize(positioner: HTMLElement): Dimensions | null {
  const width = parseFloat(positioner.style.getPropertyValue(POSITIONER_WIDTH)) || 0;
  const height = parseFloat(positioner.style.getPropertyValue(POSITIONER_HEIGHT)) || 0;
  if (width <= 0 || height <= 0) {
    return null;
  }
  return { width, height };
}

function isFocusGuard(element: Element | null): element is HTMLElement {
  return isHTMLElement(element) && element.hasAttribute(FOCUS_GUARD);
}

// FLT-DOM-13 (the PSH focus-guard contract): VoiceOver's virtual cursor fires focus only on focusable or role=button
// elements through WebKit.
function swapGuardForVoiceOver(guard: HTMLElement | null): void {
  if (guard !== null && guard.getAttribute('role') !== 'button') {
    guard.setAttribute('role', 'button');
    guard.removeAttribute('aria-hidden');
  }
}

// ---------------------------------------------------------------------------------------------------------------
// The menu: the Root's shared state, kept on the Root element
// ---------------------------------------------------------------------------------------------------------------

interface AutoSizeReset {
  abort: AbortController | null;
  owner: TriggerController | null;
}

interface TriggerGuards {
  before: HTMLElement | null;
  after: HTMLElement | null;
}

/** The Root's shared state and the parts registered with it (NAV-STA-08). */
interface Menu {
  readonly root: HTMLElement;
  options: NavigationMenuOptions;
  readonly triggers: Set<TriggerController>;
  /** The handles that follow the Popup's state on every pass (the Arrows, NAV-ANI-15). */
  readonly followers: Set<() => void>;
  popup: HTMLElement | null;
  positioner: HTMLElement | null;
  viewport: HTMLElement | null;
  target: HTMLElement | null;
  viewportGuards: TriggerGuards;
  /** The last size the Popup's resize observer recorded (NAV-ANI-11). */
  prevSize: Dimensions;
  /** The pending auto-size reset and the trigger that owns it (NAV-ANI-05). */
  readonly autoSizeReset: AutoSizeReset;
  /** The previous active trigger element (NAV-STA-07). */
  prevTrigger: HTMLElement | null;
  /** The Popup that was mounted while the popup last closed, for focus return. */
  closingPopup: HTMLElement | null;
  /** The close reason seen while mounted, for focus return (NAV-STA-15). */
  closeReason: string | null;
  /** Whether a `focus-out` report is in flight: sent and not yet answered (ADR 04 Amendments 2026-10-28, 2026-10-29). */
  closeInFlight: boolean;
  invalidate(): void;
  flush(): void;
}

const menuKey = Symbol('blazix.navigationMenu');

interface MenuElement extends HTMLElement {
  [menuKey]?: MenuState;
}

interface MenuState extends Menu {
  passQueued: boolean;
  lastOpen: boolean;
  lastStatus: string | undefined;
  lastPopup: HTMLElement | null;
  lastPositioner: HTMLElement | null;
  lastActive: TriggerController | null;
  observedContent: HTMLElement | null;
  contentObserver: MutationObserver | null;
  targetObserver: MutationObserver | null;
  observedTarget: HTMLElement | null;
  resizeListening: (() => void) | null;
  answered: number;
  resizeObserverStop: (() => void) | null;
  rootObserver: MutationObserver | null;
  references: number;
  pass(): void;
}

function isOpen(menu: Menu): boolean {
  return menu.root.hasAttribute('data-open');
}

function popupStatus(menu: Menu): string | undefined {
  const popup = menu.popup;
  if (popup === null) {
    return undefined;
  }
  if (popup.hasAttribute(STARTING_STYLE)) {
    return 'starting';
  }
  if (popup.hasAttribute(ENDING_STYLE)) {
    return 'ending';
  }
  return undefined;
}

function findActiveTrigger(menu: Menu): TriggerController | null {
  let found: TriggerController | null = null;
  for (const trigger of menu.triggers) {
    if (!trigger.isActive()) {
      continue;
    }
    // Duplicate values make several Triggers active (§16 Q6): the first in document order leads, as the session's
    // ActiveTriggerId does.
    if (found === null || found.element.compareDocumentPosition(trigger.element) & Node.DOCUMENT_POSITION_PRECEDING) {
      found = trigger;
    }
  }
  return found;
}

// The active Content: an open Content the Viewport target holds directly (the portal module appends it there); a nested
// Root's Contents sit in that Root's own target.
function findActiveContent(menu: Menu): HTMLElement | null {
  const container = menu.target ?? menu.viewport;
  if (container === null) {
    return null;
  }
  for (const child of container.children) {
    if (isHTMLElement(child) && child.hasAttribute('data-open') && !child.hidden) {
      return child;
    }
  }
  return null;
}

function popupLayer(menu: Menu): StyleLayer | null {
  return menu.popup === null ? null : styleWriter(menu.popup).layer('auto-resize');
}

function positionerLayer(menu: Menu): StyleLayer | null {
  return menu.positioner === null ? null : styleWriter(menu.positioner).layer('auto-resize');
}

// navigation-menu/utils/setSharedFixedSize.ts.
function setSharedFixedSize(menu: Menu, width: number, height: number): void {
  popupLayer(menu)?.set(POPUP_WIDTH, `${width}px`);
  popupLayer(menu)?.set(POPUP_HEIGHT, `${height}px`);
  positionerLayer(menu)?.set(POSITIONER_WIDTH, `${width}px`);
  positionerLayer(menu)?.set(POSITIONER_HEIGHT, `${height}px`);
}

// NAV-FOC-10, 11, KEY-13; utils/isOutsideMenuEvent.ts over the session forest.
function isOutsideMenuEvent(menu: Menu, currentTarget: Element, relatedTarget: Element | null): boolean {
  const token = menu.options.sessionToken;
  const session = token ? getSession(token) : undefined;
  const childContains =
    session !== undefined &&
    getNodeChildren(session.tree.nodesRef.current, token ?? undefined).some((node) => contains(node.context?.elements?.floating ?? null, relatedTarget));
  const popup = menu.popup;
  if (popup === null) {
    return !contains(menu.root, relatedTarget) && !childContains;
  }
  return !contains(popup, currentTarget) && !contains(popup, relatedTarget) && !contains(menu.root, relatedTarget) && !childContains;
}

function getMenu(root: HTMLElement): MenuState {
  const element = root as MenuElement;
  const existing = element[menuKey];
  if (existing) {
    existing.references += 1;
    return existing;
  }

  const menu: MenuState = {
    root,
    options: { part: ROOT_PART },
    triggers: new Set(),
    followers: new Set(),
    popup: null,
    positioner: null,
    viewport: null,
    target: null,
    viewportGuards: { before: null, after: null },
    prevSize: DEFAULT_SIZE,
    autoSizeReset: { abort: null, owner: null },
    prevTrigger: null,
    closingPopup: null,
    closeReason: null,
    closeInFlight: false,
    answered: 0,
    passQueued: false,
    lastOpen: root.hasAttribute('data-open'),
    lastStatus: undefined,
    lastPopup: null,
    lastPositioner: null,
    lastActive: null,
    observedContent: null,
    contentObserver: null,
    targetObserver: null,
    observedTarget: null,
    resizeListening: null,
    resizeObserverStop: null,
    rootObserver: null,
    references: 1,
    invalidate() {
      if (menu.passQueued) {
        return;
      }
      menu.passQueued = true;
      queueMicrotask(() => {
        if (menu.passQueued) {
          menu.pass();
        }
      });
    },
    // Runs the pass now: the synchronous activation on WebAssembly, whose batch C# applied inside the dispatch. The
    // observers' records still queue a pass, which then finds nothing changed.
    flush() {
      menu.pass();
    },
    pass() {
      menu.passQueued = false;
      runPass(menu);
    },
  };

  menu.rootObserver = new MutationObserver(() => menu.invalidate());
  menu.rootObserver.observe(root, { attributes: true, attributeFilter: ['data-open'] });
  element[menuKey] = menu;
  return menu;
}

function releaseMenu(menu: MenuState): void {
  menu.references -= 1;
  if (menu.references > 0) {
    return;
  }
  menu.rootObserver?.disconnect();
  menu.contentObserver?.disconnect();
  menu.targetObserver?.disconnect();
  menu.resizeListening?.();
  menu.resizeObserverStop?.();
  menu.autoSizeReset.abort?.abort();
  delete (menu.root as MenuElement)[menuKey];
}

function resolveMenu(element: HTMLElement, options: NavigationMenuOptions, references: TwinReferences, captures: TwinCaptures): MenuState | null {
  if (options.part === ROOT_PART) {
    return getMenu(element);
  }
  const root = resolveReference<HTMLElement>(references, options.rootRef, captures);
  return root === null ? null : getMenu(root);
}

// The size observer of the Popup (NAV-ANI-11; NavigationMenuTrigger.tsx:314-331): one per menu, as every Trigger's
// observer records the same size.
function syncPopupObserver(menu: MenuState): void {
  if (menu.popup === menu.lastPopup) {
    return;
  }
  menu.resizeObserverStop?.();
  menu.resizeObserverStop = null;
  const popup = menu.popup;
  if (popup !== null && typeof ResizeObserver === 'function') {
    const observer = new ResizeObserver(() => {
      menu.prevSize = { width: popup.offsetWidth, height: popup.offsetHeight };
    });
    observer.observe(popup);
    menu.resizeObserverStop = () => observer.disconnect();
  }
}

// The window resize of the active Trigger (NAV-ANI-10; NavigationMenuTrigger.tsx:333-352): listened to while the menu is
// open with an active Trigger, a Popup and a Positioner; the Trigger active at the event syncs.
function syncResizeListener(menu: MenuState, active: TriggerController | null, open: boolean): void {
  const wanted = open && active !== null && menu.popup !== null && menu.positioner !== null;
  if (wanted === (menu.resizeListening !== null)) {
    return;
  }
  if (!wanted) {
    menu.resizeListening?.();
    menu.resizeListening = null;
    return;
  }
  const win = ownerWindow(menu.positioner);
  const onResize = () => findActiveTrigger(menu)?.onWindowResize();
  win.addEventListener('resize', onResize);
  menu.resizeListening = () => win.removeEventListener('resize', onResize);
}

// The active Content's mutations (NAV-ANI-07; NavigationMenuTrigger.tsx:354-409).
function syncContentObserver(menu: MenuState, active: TriggerController | null, open: boolean): void {
  const content = open && active !== null && menu.popup !== null && menu.positioner !== null ? findActiveContent(menu) : null;
  if (content === menu.observedContent && (content === null || menu.contentObserver !== null)) {
    return;
  }
  menu.contentObserver?.disconnect();
  menu.contentObserver = null;
  menu.observedContent = content;
  if (content === null || typeof MutationObserver !== 'function') {
    return;
  }
  const observer = new MutationObserver(() => findActiveTrigger(menu)?.onContentMutation());
  observer.observe(content, {
    childList: true,
    subtree: true,
    characterData: true,
    // A `keepMounted` switch toggles `hidden` instead of inserting or removing nodes.
    attributes: true,
    attributeFilter: ['hidden'],
  });
  menu.contentObserver = observer;
}

// The Viewport target's direct children (the moved Contents): their arrival and their `data-open`/`hidden` changes tell
// the pass that a held activation's Content is in place.
function syncTargetObserver(menu: MenuState): void {
  const container = menu.target ?? menu.viewport;
  if (container === menu.observedTarget) {
    return;
  }
  menu.targetObserver?.disconnect();
  menu.targetObserver = null;
  menu.observedTarget = container;
  if (container === null) {
    return;
  }
  const observer = new MutationObserver(() => menu.invalidate());
  observer.observe(container, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-open', 'hidden'] });
  menu.targetObserver = observer;
}

// One pass per applied batch (the `openSelector` and `tabs` precedent): the effects Base UI keys on the Root's and the
// Triggers' state, in the order React runs them.
function runPass(menu: MenuState): void {
  const open = isOpen(menu);
  const status = popupStatus(menu);
  const active = findActiveTrigger(menu);
  const { popup, positioner } = menu;

  for (const follow of menu.followers) {
    follow();
  }

  // NAV-ANI-12, NavigationMenuTrigger.tsx:149-157: a Trigger that stopped being active cancels its frames and its reset.
  for (const trigger of menu.triggers) {
    trigger.setActive(trigger === active);
  }

  // NavigationMenuTrigger.tsx:296-306: every Trigger's close effect.
  if (!open && menu.lastOpen) {
    for (const trigger of menu.triggers) {
      trigger.onMenuClosed();
    }
    menu.closingPopup = popup ?? menu.lastPopup;
  }

  // NAV-ANI-03, NavigationMenuRoot.tsx:172-196: the close-size freeze, when the menu closes or its elements change while
  // closed.
  if (!open && popup !== null && positioner !== null && (menu.lastOpen || popup !== menu.lastPopup || positioner !== menu.lastPositioner)) {
    const size = readPositionerFixedSize(positioner);
    if (size !== null) {
      setSharedFixedSize(menu, size.width, size.height);
    }
  }

  // NAV-STA-07, NavigationMenuTrigger.tsx:547-552: the active Trigger's element becomes the previous trigger element.
  if (active !== null && open) {
    menu.prevTrigger = active.element;
  }

  syncPopupObserver(menu);
  syncTargetObserver(menu);

  // NavigationMenuTrigger.tsx:411-430: the post-commit size pass, keyed on open, the elements and the status, unless the
  // activation already ran it (NAV-ANI-04, 06).
  const depsChanged = open !== menu.lastOpen || status !== menu.lastStatus || popup !== menu.lastPopup || positioner !== menu.lastPositioner;
  if (active !== null && open && popup !== null && positioner !== null) {
    if (depsChanged) {
      active.onLayoutPass();
    }
    active.runHeldActivation();
  }

  syncResizeListener(menu, open ? active : null, open);
  syncContentObserver(menu, active, open);

  menu.lastOpen = open;
  menu.lastStatus = status;
  menu.lastPopup = popup;
  menu.lastPositioner = positioner;
  menu.lastActive = active;
}

// NAV-FOC-05, 06; NavigationMenuRoot.tsx:33-37, 133-157: run when C#'s mounted flag clears (the close completed).
const blockedReturnFocusReasons = new Set(['trigger-hover', 'outside-press', 'focus-out']);

function onCloseCompleted(menu: MenuState): void {
  const popup = menu.popup ?? menu.closingPopup;
  const doc = ownerDocument(menu.root);
  const active = activeElement(doc);
  const blocked = menu.closeReason !== null && blockedReturnFocusReasons.has(menu.closeReason);
  const trigger = menu.prevTrigger;
  if (!blocked && trigger !== null && trigger.isConnected && popup !== null && (active === doc.body || contains(popup, active))) {
    trigger.focus({ preventScroll: true });
    menu.prevTrigger = null;
  }
  menu.closingPopup = null;
  menu.closeReason = null;
  // NavigationMenuTrigger.tsx:308-312: the remembered size resets once the popup is no longer mounted.
  menu.prevSize = DEFAULT_SIZE;
}

// ---------------------------------------------------------------------------------------------------------------
// The Trigger
// ---------------------------------------------------------------------------------------------------------------

interface HeldActivation {
  current: Dimensions;
  skip: boolean;
}

/** A Trigger's handle: its size-pass state, the activation path, the open key, the blur check and its guards. */
interface TriggerController {
  readonly element: HTMLElement;
  isActive(): boolean;
  setActive(active: boolean): void;
  onMenuClosed(): void;
  onLayoutPass(): void;
  runHeldActivation(): void;
  onWindowResize(): void;
  onContentMutation(): void;
  guards(): TriggerGuards;
}

function attachTrigger(element: HTMLElement, initial: NavigationMenuOptions, menu: MenuState, channel: TwinChannel, twinReference: DotNetTwin): TwinHandle {
  let options = initial;
  let disposed = false;
  // NAV-STA-14.
  let pointerType = '';
  // NAV-ANI-06.
  let skipAutoSizeSync = false;
  // Design point 3: *current*, measured at an activation whose batch has not been applied yet.
  let held: HeldActivation | null = null;
  let wasActive = false;
  const mutationFrame: FrameSlot = createFrameSlot();
  const resizeFrame: FrameSlot = createFrameSlot();
  const sizeFrame: FrameSlot = createFrameSlot();
  const watcher: AnimationsWatcher = createAnimationsWatcher(() => menu.popup);

  const isActive = () => element.hasAttribute(ACTIVE_TRIGGER);
  const isDisabled = () => element.hasAttribute('data-disabled');
  const orientation = () => menu.options.orientation ?? 'horizontal';

  const cancelAutoSizeReset = (force = false) => {
    if (!force && menu.autoSizeReset.owner !== controller) {
      return;
    }
    menu.autoSizeReset.abort?.abort();
    menu.autoSizeReset.abort = null;
    menu.autoSizeReset.owner = null;
  };

  const setAutoSizes = () => {
    popupLayer(menu)?.set(POPUP_WIDTH, 'auto');
    popupLayer(menu)?.set(POPUP_HEIGHT, 'auto');
  };

  const clearFixedSizes = () => {
    popupLayer(menu)?.remove(POPUP_WIDTH);
    popupLayer(menu)?.remove(POPUP_HEIGHT);
    positionerLayer(menu)?.remove(POSITIONER_WIDTH);
    positionerLayer(menu)?.remove(POSITIONER_HEIGHT);
  };

  // NAV-ANI-05; NavigationMenuTrigger.tsx:171-183.
  const scheduleAutoSizeReset = () => {
    cancelAutoSizeReset(true);
    const abort = new AbortController();
    menu.autoSizeReset.abort = abort;
    menu.autoSizeReset.owner = controller;
    watcher.run(() => {
      menu.autoSizeReset.abort = null;
      menu.autoSizeReset.owner = null;
      setAutoSizes();
    }, abort.signal);
  };

  // NAV-ANI-04 steps 3–4, ANI-21; NavigationMenuTrigger.tsx:185-222.
  const handleValueChange = (currentWidth: number, currentHeight: number) => {
    const popup = menu.popup;
    if (popup === null || menu.positioner === null) {
      return;
    }
    cancelAutoSizeReset(true);
    clearFixedSizes();
    const { width, height } = getCssDimensions(popup);
    const measuredWidth = width || menu.prevSize.width;
    const measuredHeight = height || menu.prevSize.height;
    if (currentHeight === 0 || currentWidth === 0) {
      currentWidth = measuredWidth;
      currentHeight = measuredHeight;
    }
    popupLayer(menu)?.set(POPUP_WIDTH, `${currentWidth}px`);
    popupLayer(menu)?.set(POPUP_HEIGHT, `${currentHeight}px`);
    positionerLayer(menu)?.set(POSITIONER_WIDTH, `${measuredWidth}px`);
    positionerLayer(menu)?.set(POSITIONER_HEIGHT, `${measuredHeight}px`);
    sizeFrame.request(() => {
      if (!isActive()) {
        return;
      }
      popupLayer(menu)?.set(POPUP_WIDTH, `${measuredWidth}px`);
      popupLayer(menu)?.set(POPUP_HEIGHT, `${measuredHeight}px`);
      scheduleAutoSizeReset();
    });
  };

  // NAV-ANI-08; NavigationMenuTrigger.tsx:224-257.
  const handleInterruptedMutationResize = (currentWidth: number, currentHeight: number) => {
    sizeFrame.cancel();
    mutationFrame.cancel();
    cancelAutoSizeReset(true);
    if (currentWidth === 0 || currentHeight === 0) {
      return;
    }
    setSharedFixedSize(menu, currentWidth, currentHeight);
    mutationFrame.request(() => {
      mutationFrame.request(() => {
        const popup = menu.popup;
        if (popup === null) {
          return;
        }
        clearFixedSizes();
        const { width, height } = getCssDimensions(popup);
        const measuredWidth = width || currentWidth;
        const measuredHeight = height || currentHeight;
        setSharedFixedSize(menu, currentWidth, currentHeight);
        sizeFrame.request(() => {
          if (!isActive()) {
            return;
          }
          setSharedFixedSize(menu, measuredWidth, measuredHeight);
          scheduleAutoSizeReset();
        });
      });
    });
  };

  // NAV-ANI-09; NavigationMenuTrigger.tsx:259-275.
  const syncCurrentSize = () => {
    const popup = menu.popup;
    if (popup === null) {
      return;
    }
    sizeFrame.cancel();
    cancelAutoSizeReset(true);
    clearFixedSizes();
    const { width, height } = getCssDimensions(popup);
    if (width === 0 || height === 0) {
      return;
    }
    menu.prevSize = { width, height };
    setAutoSizes();
    positionerLayer(menu)?.set(POSITIONER_WIDTH, `${width}px`);
    positionerLayer(menu)?.set(POSITIONER_HEIGHT, `${height}px`);
  };

  // NavigationMenuTrigger.tsx:277-294.
  const getMutationBaseline = (popup: HTMLElement): { size: Dimensions; syncPositioner: boolean } => {
    const popupWidth = popup.style.getPropertyValue(POPUP_WIDTH);
    const popupHeight = popup.style.getPropertyValue(POPUP_HEIGHT);
    const resizing = popupWidth !== '' && popupWidth !== 'auto' && popupHeight !== '' && popupHeight !== 'auto';
    if (!resizing) {
      return { size: menu.prevSize, syncPositioner: false };
    }
    return {
      size: { width: popup.offsetWidth || menu.prevSize.width, height: popup.offsetHeight || menu.prevSize.height },
      syncPositioner: true,
    };
  };

  // NAV-STA-06; NavigationMenuTrigger.tsx:556-569: measured while the popup is mounted and a previous trigger is known.
  const directionFrom = (): ActivationDirection | null => {
    const previous = menu.prevTrigger;
    if (menu.options.mounted !== true || previous === null || !previous.isConnected) {
      return null;
    }
    return computeActivationDirection(previous.getBoundingClientRect(), element.getBoundingClientRect(), orientation());
  };

  // Design point 4: the activation is a report with no veto. On WebAssembly the invocation is synchronous, so C# runs
  // the request and renders inside the dispatch (Base UI's flushSync) and the pass runs at once; on Server it is a
  // report and the pass runs when the applied batch arrives.
  const report = (payload: ActivationReport) => {
    if (channel.synchronous) {
      twinReference.invokeMethod<void>(ACTIVATE_REPORT, JSON.stringify(payload));
      menu.flush();
      return;
    }
    channel.report(ACTIVATE_REPORT, payload);
  };

  // NAV-PTR-18; NavigationMenuTrigger.tsx:554-617.
  const activate = (event: Event): void => {
    const direction = directionFrom();
    // NAV-PTR-07; NavigationMenuTrigger.tsx:571-576: a hover (or key) activation of an open menu resets the session's
    // recorded open event, so a click-opened menu switched by hover is treated as hover-opened from then on. The
    // session's dataRef is shared by every module of the session; the key is the interactions module's.
    if (event.type !== 'click' && isOpen(menu)) {
      const token = menu.options.sessionToken;
      const session = token ? getSession(token) : undefined;
      if (session !== undefined) {
        session.dataRef.current.openEvent = undefined;
      }
    }
    // NavigationMenuTrigger.tsx:578-580: a touch mouseenter requests nothing.
    if (pointerType === 'touch' && event.type !== 'click') {
      if (direction !== null) {
        report({ reason: null, direction, event: eventInfo(event) });
      }
      return;
    }
    // NavigationMenuTrigger.tsx:584-592: only an open menu is switched; the open key requested `list-navigation` itself.
    const reason: ActivationReason | null = isOpen(menu) && event.type !== 'keydown' ? (event.type === 'mouseenter' ? 'trigger-hover' : 'trigger-press') : null;
    if (reason !== null || direction !== null) {
      report({ reason, direction, event: eventInfo(event) });
    }
  };

  // NavigationMenuTrigger.tsx:619-640: *current* before the activation, the activation, then the size transition with it.
  const hold = (event: Event): void => {
    const popup = menu.popup;
    if (popup === null || menu.positioner === null) {
      held = null;
      return;
    }
    const switching = isOpen(menu) && !isActive();
    held = { current: getCssDimensions(popup), skip: switching && (event.type === 'click' || pointerType !== 'touch') };
  };

  const handleOpenEvent = (event: Event) => {
    if (isDisabled()) {
      return;
    }
    hold(event);
    activate(event);
    controller.runHeldActivation();
  };

  const contentReady = () => options.hasContent === false || findActiveContent(menu) !== null;

  const controller: TriggerController = {
    element,
    isActive,
    setActive(active) {
      if (active) {
        wasActive = true;
        return;
      }
      if (!wasActive) {
        return;
      }
      wasActive = false;
      held = null;
      mutationFrame.cancel();
      sizeFrame.cancel();
      resizeFrame.cancel();
      cancelAutoSizeReset();
    },
    onMenuClosed() {
      mutationFrame.cancel();
      resizeFrame.cancel();
      sizeFrame.cancel();
      cancelAutoSizeReset(true);
      skipAutoSizeSync = false;
      held = null;
      pointerType = '';
    },
    // NavigationMenuTrigger.tsx:411-430.
    onLayoutPass() {
      if (skipAutoSizeSync) {
        skipAutoSizeSync = false;
        return;
      }
      const popup = menu.popup;
      if (popup !== null) {
        const { width, height } = getCssDimensions(popup);
        handleValueChange(width, height);
      }
    },
    // The held activation runs once the batch shows this Trigger active and its Content in the Viewport.
    runHeldActivation() {
      if (held === null || !isActive() || !isOpen(menu) || menu.popup === null || menu.positioner === null || !contentReady()) {
        return;
      }
      const { current, skip } = held;
      held = null;
      if (skip) {
        skipAutoSizeSync = true;
      }
      handleValueChange(current.width, current.height);
    },
    onWindowResize() {
      resizeFrame.request(syncCurrentSize);
    },
    onContentMutation() {
      const popup = menu.popup;
      if (popup === null || menu.positioner === null || !isActive()) {
        return;
      }
      if (popupStatus(menu) === 'starting') {
        syncCurrentSize();
        return;
      }
      const { size, syncPositioner } = getMutationBaseline(popup);
      if (syncPositioner) {
        handleInterruptedMutationResize(size.width, size.height);
        return;
      }
      handleValueChange(size.width, size.height);
    },
    // NAV-DOM-10: the guard, the owner and the guard C# renders after the active Trigger, found by position. The owner
    // carries `aria-owns` only while a Viewport is registered, so it is the element between the guards either way.
    guards() {
      const first = element.nextElementSibling;
      if (!isFocusGuard(first)) {
        return { before: null, after: null };
      }
      let second = first.nextElementSibling;
      if (second !== null && !isFocusGuard(second)) {
        second = second.nextElementSibling;
      }
      return { before: first, after: isFocusGuard(second) ? second : null };
    },
  };

  // The Trigger's own state is read from the applied batch: `data-popup-open` while its Item is the active one.
  const selfObserver = new MutationObserver(() => menu.invalidate());
  selfObserver.observe(element, { attributes: true, attributeFilter: [ACTIVE_TRIGGER] });

  const onMouseEnter = (event: MouseEvent) => handleOpenEvent(event);
  const onClick = (event: MouseEvent) => handleOpenEvent(event);
  const onPointer = (event: PointerEvent) => {
    pointerType = event.pointerType;
  };

  // NAV-KEY-01, 02, 08, EDGE-06; NavigationMenuTrigger.tsx:676-693: one `list-navigation` request, then the activation's
  // size transition; the key is prevented and stopped in the dispatch.
  const onKeyDown = (event: KeyboardEvent) => {
    if (menu.options.nested === true || isDisabled() || !isOpenKey(event.key, orientation(), menu.options.rtl === true)) {
      return;
    }
    // Base UI merges the consumer's onKeyDown first (NavigationMenuTrigger.tsx:729-735): on WebAssembly its
    // PreventBaseUIHandler() stops the open; on Server the point falls back to run (ADR 01, "C#-origin consumer vetoes").
    if (channel.decide(NAVIGATE_POINT, event, 'run') !== 'run') {
      return;
    }
    hold(event);
    event.preventDefault();
    event.stopPropagation();
    report({ reason: 'list-navigation', direction: directionFrom(), event: eventInfo(event) });
    controller.runHeldActivation();
  };

  const reportFocusOut = (event: FocusEvent) => {
    if (!menu.closeInFlight) {
      menu.closeInFlight = true;
      channel.report(FOCUS_OUT_REPORT, { event: eventInfo(event) });
    }
  };

  // NAV-FOC-10, KEY-13; NavigationMenuTrigger.tsx:694-708. A closed menu still gets the request, whose redundant close
  // only records the reason (NAV-EVT-05, EVT-06).
  const onFocusOut = (event: FocusEvent) => {
    if (event.target !== element || menu.positioner === null || menu.popup === null) {
      return;
    }
    if (isOutsideMenuEvent(menu, element, event.relatedTarget as Element | null)) {
      reportFocusOut(event);
    }
  };

  // NAV-KEY-10..12, 15, FOC-04; NavigationMenuTrigger.tsx:739-780: the guards after the active Trigger.
  const parent = element.parentElement;
  const onGuardFocus = (event: FocusEvent) => {
    const { before, after } = controller.guards();
    const target = event.target;
    if (target === null || (target !== before && target !== after)) {
      return;
    }
    const reference = menu.positioner ?? menu.viewport;
    const fromOutside = reference !== null && isOutsideEvent(event, reference);
    if (target === before) {
      if (fromOutside) {
        menu.viewportGuards.before?.focus();
      } else {
        getPreviousTabbable(element)?.focus();
      }
      return;
    }
    if (fromOutside) {
      clearViewportInert(menu, channel);
      (menu.viewportGuards.after ?? element).focus();
      return;
    }
    const nested = menu.options.nested === true;
    let next = getNextTabbable(element);
    if (nested && menu.positioner === null && reference !== null && next !== null && contains(reference, next)) {
      next = getTabbableNearElement(menu.viewportGuards.after, 1);
    }
    next?.focus();
    if ((!nested || menu.positioner !== null) && !contains(menu.root, next)) {
      reportFocusOut(event);
    }
  };

  // NAV-ARIA-05: the guards C# renders after the Trigger while it is active.
  const guardObserver =
    platform.screenReader.voiceOver && platform.engine.webkit && parent !== null
      ? new MutationObserver(() => {
          const { before, after } = controller.guards();
          swapGuardForVoiceOver(before);
          swapGuardForVoiceOver(after);
        })
      : null;
  if (guardObserver !== null && parent !== null) {
    guardObserver.observe(parent, { childList: true });
  }

  element.addEventListener('mouseenter', onMouseEnter);
  element.addEventListener('click', onClick);
  element.addEventListener('pointerenter', onPointer);
  element.addEventListener('pointerdown', onPointer);
  element.addEventListener('keydown', onKeyDown);
  element.addEventListener('focusout', onFocusOut);
  parent?.addEventListener('focusin', onGuardFocus as EventListener);
  menu.triggers.add(controller);
  menu.invalidate();

  return {
    update(optionsJson: string) {
      if (!disposed) {
        options = parseOptions<NavigationMenuOptions>(optionsJson);
        menu.invalidate();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      selfObserver.disconnect();
      guardObserver?.disconnect();
      mutationFrame.cancel();
      resizeFrame.cancel();
      sizeFrame.cancel();
      cancelAutoSizeReset();
      element.removeEventListener('mouseenter', onMouseEnter);
      element.removeEventListener('click', onClick);
      element.removeEventListener('pointerenter', onPointer);
      element.removeEventListener('pointerdown', onPointer);
      element.removeEventListener('keydown', onKeyDown);
      element.removeEventListener('focusout', onFocusOut);
      parent?.removeEventListener('focusin', onGuardFocus as EventListener);
      menu.triggers.delete(controller);
      menu.invalidate();
      releaseMenu(menu);
    },
  };
}

// NAV-STA-09, KEY-12; NavigationMenuTrigger.tsx:753-757: the trailing guard reached from outside clears `viewportInert`
// before focusing into the Viewport. C# renders `inert`; the module lifts it at once (an optimistic convergent write)
// so the focus lands, and reports the change.
function clearViewportInert(menu: Menu, channel: TwinChannel): void {
  const viewport = menu.viewport;
  if (viewport !== null && viewport.hasAttribute('inert')) {
    viewport.removeAttribute('inert');
    channel.report(VIEWPORT_INERT_REPORT, { inert: false });
  }
}

// ---------------------------------------------------------------------------------------------------------------
// The other parts
// ---------------------------------------------------------------------------------------------------------------

function attachRoot(initial: NavigationMenuOptions, menu: MenuState): TwinHandle {
  let disposed = false;
  let mounted = false;
  const apply = (options: NavigationMenuOptions) => {
    menu.options = options;
    // NAV-STA-15: the reason of the latest close request, kept until the close completes.
    if (options.closeReason != null && options.mounted === true) {
      menu.closeReason = options.closeReason;
    }
    // A `focus-out` report is answered, committed or canceled (ADR 04 Amendments 2026-10-28, widened 2026-10-29).
    const answered = options.answered ?? 0;
    if (answered !== menu.answered) {
      menu.answered = answered;
      menu.closeInFlight = false;
    }
    const nextMounted = options.mounted === true;
    if (mounted && !nextMounted) {
      onCloseCompleted(menu);
    }
    mounted = nextMounted;
    menu.invalidate();
  };
  apply(initial);
  return {
    update(optionsJson: string) {
      if (!disposed) {
        apply(parseOptions<NavigationMenuOptions>(optionsJson));
      }
    },
    dispose() {
      if (!disposed) {
        disposed = true;
        releaseMenu(menu);
      }
    },
  };
}

// NAV-KEY-06: the top-level List stops its axis' arrows even where the composite moved nothing.
function attachList(element: HTMLElement, menu: MenuState): TwinHandle {
  let disposed = false;
  const onKeyDown = (event: KeyboardEvent) => {
    if (menu.options.nested !== true && isListGuardKey(event.key, menu.options.orientation ?? 'horizontal')) {
      event.stopPropagation();
    }
  };
  element.addEventListener('keydown', onKeyDown);
  return {
    update() {},
    dispose() {
      if (!disposed) {
        disposed = true;
        element.removeEventListener('keydown', onKeyDown);
        releaseMenu(menu);
      }
    },
  };
}

// NAV-FOC-11: a Link outside the Popup closes the menu when its blur leaves it; C# attaches no handle to a Link inside
// a Content, whose check never passes (§16 Q7).
function attachLink(element: HTMLElement, menu: MenuState, channel: TwinChannel): TwinHandle {
  let disposed = false;
  const onFocusOut = (event: FocusEvent) => {
    if (event.target !== element || menu.positioner === null || menu.popup === null || menu.closeInFlight) {
      return;
    }
    if (isOutsideMenuEvent(menu, element, event.relatedTarget as Element | null)) {
      menu.closeInFlight = true;
      channel.report(FOCUS_OUT_REPORT, { event: eventInfo(event) });
    }
  };
  element.addEventListener('focusout', onFocusOut);
  return {
    update() {},
    dispose() {
      if (!disposed) {
        disposed = true;
        element.removeEventListener('focusout', onFocusOut);
        releaseMenu(menu);
      }
    },
  };
}

// NAV-FOC-09, DOM-17: the Content's focus flag, and `inert` on an exiting Content unless focus is inside it.
function attachContent(element: HTMLElement, menu: MenuState): TwinHandle {
  let disposed = false;
  let focusInside = false;
  const exiting = () => element.hasAttribute('data-closed') && !element.hidden;
  const write = () => {
    const inert = exiting() && !focusInside;
    if (element.hasAttribute('inert') !== inert) {
      element.toggleAttribute('inert', inert);
    }
  };
  const onFocusIn = (event: FocusEvent) => {
    const target = getTarget(event) as Element | null;
    if (target !== null && target.hasAttribute(FOCUS_GUARD)) {
      return;
    }
    focusInside = true;
    write();
  };
  const onFocusOut = (event: FocusEvent) => {
    if (!contains(element, event.relatedTarget as Element | null)) {
      focusInside = false;
      write();
    }
  };
  const observer = new MutationObserver(write);
  observer.observe(element, { attributes: true, attributeFilter: ['data-open', 'data-closed', 'hidden'] });
  element.addEventListener('focusin', onFocusIn);
  element.addEventListener('focusout', onFocusOut);
  write();
  menu.invalidate();
  return {
    update() {},
    dispose() {
      if (!disposed) {
        disposed = true;
        observer.disconnect();
        element.removeEventListener('focusin', onFocusIn);
        element.removeEventListener('focusout', onFocusOut);
        menu.invalidate();
        releaseMenu(menu);
      }
    },
  };
}

// NAV-FOC-08, ANI-14, DOM-28: the Positioner's focus in and out, and its resize report.
function attachPositioner(element: HTMLElement, menu: MenuState, channel: TwinChannel): TwinHandle {
  let disposed = false;
  const reportFrame = createFrameSlot();
  let reportPending = false;
  // NavigationMenuPositioner.tsx:83-106: capture, so the restore runs before the guards' own focus listeners.
  const onFocus = (event: FocusEvent) => {
    if (isOutsideEvent(event)) {
      if (event.type === 'focusin') {
        enableFocusInside(element);
      } else {
        disableFocusInside(element);
      }
    }
  };
  // NavigationMenuPositioner.tsx:156-168: each resize while open marks the Positioner instant; C# owns the 100 ms timer
  // (design point 6), so the reports are coalesced to one per animation frame.
  const win = ownerWindow(element);
  const onResize = () => {
    if (!isOpen(menu) || reportPending) {
      return;
    }
    reportPending = true;
    channel.report(RESIZE_REPORT, {});
    reportFrame.request(() => {
      reportPending = false;
    });
  };
  element.addEventListener('focusin', onFocus, true);
  element.addEventListener('focusout', onFocus, true);
  win.addEventListener('resize', onResize);
  menu.positioner = element;
  menu.invalidate();
  return {
    update() {},
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      reportFrame.cancel();
      element.removeEventListener('focusin', onFocus, true);
      element.removeEventListener('focusout', onFocus, true);
      win.removeEventListener('resize', onResize);
      positionerLayer(menu)?.clear();
      if (menu.positioner === element) {
        menu.positioner = null;
      }
      menu.invalidate();
      releaseMenu(menu);
    },
  };
}

// NAV-ANI-15; arrow/NavigationMenuArrow.tsx:39 (getDisabledMountTransitionStyles): while the Root's status is
// `starting`, read from the Popup's data-starting-style as the Popup's own suppression is, the Arrow carries
// `transition: none`, written under the consumer's style in the `transitions` band.
function attachArrow(element: HTMLElement, menu: MenuState): TwinHandle {
  const layer = styleWriter(element).layer('transitions');
  let suppressed = false;
  const follow = () => {
    const starting = menu.popup?.hasAttribute(STARTING_STYLE) === true;
    if (starting === suppressed) {
      return;
    }
    suppressed = starting;
    if (starting) {
      layer.set('transition', 'none');
    } else {
      layer.remove('transition');
    }
  };
  menu.followers.add(follow);
  follow();
  return {
    update() {},
    dispose() {
      menu.followers.delete(follow);
      layer.clear();
      releaseMenu(menu);
    },
  };
}

// NAV-DOM-31: the Popup grows away from its anchor; its status and open state feed the size pass.
function attachPopup(element: HTMLElement, menu: MenuState): TwinHandle {
  let disposed = false;
  const anchoring = styleWriter(element).layer('positioning');
  let written: Record<string, string> = {};
  let observedPositioner: HTMLElement | null = null;
  const writeAnchoring = () => {
    const side = menu.positioner?.getAttribute('data-side') ?? null;
    const next = getPopupAnchoringStyles(getPhysicalSide(side, menu.options.rtl === true));
    for (const property of Object.keys(written)) {
      if (!(property in next)) {
        anchoring.remove(property);
      }
    }
    for (const [property, value] of Object.entries(next)) {
      if (written[property] !== value) {
        anchoring.set(property, value);
      }
    }
    written = next;
  };
  const sideObserver = new MutationObserver(writeAnchoring);
  const syncSideObserver = () => {
    if (menu.positioner === observedPositioner) {
      return;
    }
    sideObserver.disconnect();
    observedPositioner = menu.positioner;
    if (observedPositioner !== null) {
      sideObserver.observe(observedPositioner, { attributes: true, attributeFilter: ['data-side'] });
    }
  };
  const statusObserver = new MutationObserver(() => {
    syncSideObserver();
    writeAnchoring();
    menu.invalidate();
  });
  statusObserver.observe(element, { attributes: true, attributeFilter: [STARTING_STYLE, ENDING_STYLE, 'data-open', 'data-closed'] });
  menu.popup = element;
  syncSideObserver();
  writeAnchoring();
  menu.invalidate();
  return {
    update() {
      if (!disposed) {
        syncSideObserver();
        writeAnchoring();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      sideObserver.disconnect();
      statusObserver.disconnect();
      anchoring.clear();
      popupLayer(menu)?.clear();
      if (menu.popup === element) {
        menu.popup = null;
      }
      menu.invalidate();
      releaseMenu(menu);
    },
  };
}

// NAV-DOM-34, KEY-10..12, FOC-07, ARIA-05: the Viewport's target, its guards and their routing, and its blur.
function attachViewport(element: HTMLElement, initial: NavigationMenuOptions, menu: MenuState, channel: TwinChannel): TwinHandle {
  let options = initial;
  let disposed = false;
  // Found by position: with a Positioner the guards are the Viewport's siblings and the Viewport is the target; without
  // one they are its first and last children around the inner target `div`.
  const findParts = () => {
    if (options.hasPositioner === true) {
      const before = element.previousElementSibling;
      const after = element.nextElementSibling;
      return { target: null, guards: { before: isFocusGuard(before) ? before : null, after: isFocusGuard(after) ? after : null } };
    }
    const first = element.firstElementChild;
    const last = element.lastElementChild;
    const guarded = isFocusGuard(first) && isFocusGuard(last) && first !== last;
    const target = guarded ? (first.nextElementSibling as HTMLElement | null) : (first as HTMLElement | null);
    return { target: isHTMLElement(target) && !isFocusGuard(target) ? target : null, guards: guarded ? { before: first, after: last } : { before: null, after: null } };
  };
  const sync = () => {
    const parts = findParts();
    menu.target = parts.target;
    menu.viewportGuards = parts.guards;
    if (platform.screenReader.voiceOver && platform.engine.webkit) {
      swapGuardForVoiceOver(parts.guards.before);
      swapGuardForVoiceOver(parts.guards.after);
    }
    menu.invalidate();
  };
  const reference = () => menu.positioner ?? menu.viewport;
  // NavigationMenuViewport.tsx:21-64.
  const onGuardFocus = (event: FocusEvent) => {
    const { before, after } = menu.viewportGuards;
    const target = event.target;
    if (target !== before && target !== after) {
      return;
    }
    const container = reference();
    const fromOutside = container !== null && isOutsideEvent(event, container);
    const active = findActiveTrigger(menu)?.guards() ?? { before: null, after: null };
    if (target === before) {
      if (fromOutside) {
        getNextTabbable(container)?.focus();
      } else {
        active.before?.focus();
      }
      return;
    }
    if (fromOutside) {
      getPreviousTabbable(container)?.focus();
    } else {
      active.after?.focus();
    }
  };
  // NavigationMenuViewport.tsx:104-118: focus leaving the Viewport for neither its inside nor the active trigger.
  const onFocusOut = (event: FocusEvent) => {
    const related = event.relatedTarget as Element | null;
    const trigger = findActiveTrigger(menu)?.element ?? null;
    if (options.hasPositioner !== true && related !== null && !contains(element, related) && related !== trigger && !element.hasAttribute('inert')) {
      channel.report(VIEWPORT_INERT_REPORT, { inert: true });
    }
  };
  // NavigationMenuRoot.tsx:160-170: without a Positioner the target may be the animated element, so a second completion
  // watcher waits for its animations once the menu closes, and stops when it reopens; C# completes the close unless the
  // opt-out holds.
  const exitWatcher = createAnimationsWatcher(() => menu.target);
  let exitAbort: AbortController | null = null;
  let wasOpen = isOpen(menu);
  const onRootChange = () => {
    const open = isOpen(menu);
    if (open === wasOpen) {
      return;
    }
    wasOpen = open;
    exitAbort?.abort();
    exitAbort = null;
    if (!open && options.hasPositioner !== true && menu.target !== null) {
      const abort = new AbortController();
      exitAbort = abort;
      exitWatcher.run(() => {
        if (exitAbort === abort) {
          exitAbort = null;
          channel.report(VIEWPORT_EXIT_REPORT, {});
        }
      }, abort.signal);
    }
  };
  const rootObserver = new MutationObserver(onRootChange);
  rootObserver.observe(menu.root, { attributes: true, attributeFilter: ['data-open'] });

  const observer = new MutationObserver(sync);
  const scope = options.hasPositioner === true ? element.parentElement : element;
  if (scope !== null) {
    observer.observe(scope, { childList: true });
  }
  scope?.addEventListener('focusin', onGuardFocus as EventListener);
  element.addEventListener('focusout', onFocusOut);
  menu.viewport = element;
  sync();
  return {
    update(optionsJson: string) {
      if (!disposed) {
        options = parseOptions<NavigationMenuOptions>(optionsJson);
        sync();
      }
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      observer.disconnect();
      rootObserver.disconnect();
      exitAbort?.abort();
      exitWatcher.cancel();
      scope?.removeEventListener('focusin', onGuardFocus as EventListener);
      element.removeEventListener('focusout', onFocusOut);
      if (menu.viewport === element) {
        menu.viewport = null;
        menu.target = null;
        menu.viewportGuards = { before: null, after: null };
      }
      menu.invalidate();
      releaseMenu(menu);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The twin-facing entry
// ---------------------------------------------------------------------------------------------------------------

const attachedHandle = Symbol('blazix.navigationMenu.handle');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

/**
 * Attaches one part's handle. Every part but the Root names the Root element as its first reference; the parts of one
 * Root share its state on that element.
 * @param elementOrId The part's element, or its id.
 * @param optionsJson The `NavigationMenuOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param references The Root element, for every part but the Root.
 * @param captures The capture ids C# sends beside the element and the references, which find them inside a shadow root.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const element = resolveElement(elementOrId, captures?.[0]) as AttachedElement;
  const existing = element[attachedHandle];
  if (existing) {
    return existing;
  }
  const options = parseOptions<NavigationMenuOptions>(optionsJson);
  const menu = resolveMenu(element, options, references, captures);
  if (menu === null) {
    // The Root left the document before the part attached: nothing to join.
    return { update() {}, dispose() {} };
  }
  const channel = createTwinChannel(twinReference);
  let handle: TwinHandle;
  switch (options.part) {
    case ROOT_PART:
      handle = attachRoot(options, menu);
      break;
    case TRIGGER_PART:
      handle = attachTrigger(element, options, menu, channel, twinReference);
      break;
    case LIST_PART:
      handle = attachList(element, menu);
      break;
    case LINK_PART:
      handle = attachLink(element, menu, channel);
      break;
    case CONTENT_PART:
      handle = attachContent(element, menu);
      break;
    case POSITIONER_PART:
      handle = attachPositioner(element, menu, channel);
      break;
    case POPUP_PART:
      handle = attachPopup(element, menu);
      break;
    case ARROW_PART:
      handle = attachArrow(element, menu);
      break;
    default:
      handle = attachViewport(element, options, menu, channel);
      break;
  }
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
