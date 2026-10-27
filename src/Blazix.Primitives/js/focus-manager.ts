// The `focus-manager` mechanism module (ADR 04 §1; Amendments 2026-10-27): tabbable discovery, the outside marking,
// the one queued focus per page and the focus manager a popup's twin attaches (initial focus, the modal trap, the
// focus-out close, restoration, return focus, the managed `tabindex` and the inside focus guards' routing).
// Transcribed from Base UI v1.9.0: packages/react/src/floating-ui-react/utils/{tabbable,markOthers,enqueueFocus}.ts,
// utils/element.ts (`getFloatingFocusElement`) and components/FloatingFocusManager.tsx.
// Requirements: floating-and-positioning.md §4.4 (FLT-DOM-15..18), §4.5 (FLT-ARIA-01..03), §4.6 (FLT-KEY-04..06,
// FLT-KEY-09), §4.8 (FLT-FOC-05..22), FLT-A11Y-04, FLT-A11Y-07, FLT-EDGE-05..08, FLT-EDGE-14, FLT-ALG-18, 19, 21,
// 22, 27 and FLT-API-05.
// Page-global state (sanctioned, ADR 04 Amendments 2026-10-27; js/CONTEXT.md): the outside marking's counters, the
// previously-focused stack and the queued focus; `resetFocusManagerState` drops all three for tests.
import { isElementVisible } from './composite';
import { activeElement, closest, contains, getTarget, isHTMLElement, isShadowRoot, ownerDocument, ownerWindow, platform } from './dom';
import { eventInfo, type EventInfo } from './event-info';
import { cancelFrame, observeOpenInput, requestFrame } from './transitions';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  resolveReference as resolveTwinReference,
  type DotNetTwin,
  type TwinHandle,
  type TwinOptions,
  type TwinReferences,
} from './twin';

/** An element that can take focus. */
export type FocusableElement = HTMLElement | SVGElement;

function getNodeName(node: Node): string {
  return (node.nodeName || '').toLowerCase();
}

// ---------------------------------------------------------------------------------------------------------------
// Tabbable discovery (FLT-ALG-18, FLT-FOC-14, FLT-FOC-15, FLT-EDGE-14)
// ---------------------------------------------------------------------------------------------------------------

const CANDIDATE_SELECTOR =
  'a[href],button,input,select,textarea,summary,details,iframe,object,embed,[tabindex],[contenteditable]:not([contenteditable="false"]),audio[controls],video[controls]';

function getParentElement(element: Element): Element | null {
  const assignedSlot = (element as Element & { assignedSlot?: HTMLSlotElement | null }).assignedSlot;
  if (assignedSlot) {
    return assignedSlot;
  }

  if (element.parentElement) {
    return element.parentElement;
  }

  const rootNode = element.getRootNode();
  return isShadowRoot(rootNode) ? rootNode.host : null;
}

function getDetailsSummary(details: Element): Element | null {
  for (const child of Array.from(details.children)) {
    if (getNodeName(child) === 'summary') {
      return child;
    }
  }

  return null;
}

function isWithinOpenDetailsSummary(element: Element, details: Element): boolean {
  const summary = getDetailsSummary(details);
  return !!summary && (element === summary || contains(summary, element));
}

function isFocusableCandidate(element: Element | null): element is FocusableElement {
  const nodeName = element ? getNodeName(element) : '';

  return (
    element != null &&
    element.matches(CANDIDATE_SELECTOR) &&
    (nodeName !== 'summary' ||
      (element.parentElement != null &&
        getNodeName(element.parentElement) === 'details' &&
        getDetailsSummary(element.parentElement) === element)) &&
    (nodeName !== 'details' || getDetailsSummary(element) == null) &&
    (nodeName !== 'input' || (element as HTMLInputElement).type !== 'hidden')
  );
}

function isVisibleInTabbableTree(element: Element, isAncestor: boolean): boolean {
  const styles = getComputedStyle(element);

  if (!isAncestor) {
    return isElementVisible(element, styles);
  }

  return styles.display !== 'none';
}

function isFocusableElement(element: Element | null): element is FocusableElement {
  if (!isFocusableCandidate(element) || !element.isConnected || element.matches(':disabled')) {
    return false;
  }

  for (let current: Element | null = element; current; current = getParentElement(current)) {
    const isAncestor = current !== element;
    const isSlot = getNodeName(current) === 'slot';

    if (current.hasAttribute('inert')) {
      return false;
    }

    if (
      (isAncestor &&
        getNodeName(current) === 'details' &&
        !(current as HTMLDetailsElement).open &&
        !isWithinOpenDetailsSummary(element, current)) ||
      current.hasAttribute('hidden') ||
      (!isSlot && !isVisibleInTabbableTree(current, isAncestor))
    ) {
      return false;
    }
  }

  return true;
}

function getTabIndex(element: FocusableElement): number {
  const tabIndex = element.tabIndex;
  if (tabIndex < 0) {
    const nodeName = getNodeName(element);
    if (
      nodeName === 'details' ||
      nodeName === 'audio' ||
      nodeName === 'video' ||
      (isHTMLElement(element) && element.isContentEditable)
    ) {
      return 0;
    }
  }

  return tabIndex;
}

function getNamedRadioInput(element: FocusableElement): HTMLInputElement | null {
  if (getNodeName(element) !== 'input') {
    return null;
  }

  const input = element as HTMLInputElement;
  return input.type === 'radio' && input.name !== '' ? input : null;
}

function isTabbableRadio(element: FocusableElement, candidates: FocusableElement[]): boolean {
  const input = getNamedRadioInput(element);
  if (!input) {
    return true;
  }

  const checkedRadio = candidates.find((candidate) => {
    const radio = getNamedRadioInput(candidate);
    return radio?.name === input.name && radio.form === input.form && radio.checked;
  });

  if (checkedRadio) {
    return checkedRadio === input;
  }

  return (
    candidates.find((candidate) => {
      const radio = getNamedRadioInput(candidate);
      return radio?.name === input.name && radio.form === input.form;
    }) === input
  );
}

function getComposedChildren(container: ParentNode): Element[] {
  if (isHTMLElement(container) && getNodeName(container) === 'slot') {
    const assignedElements = (container as HTMLSlotElement).assignedElements({ flatten: true });
    if (assignedElements.length > 0) {
      return assignedElements;
    }
  }

  if (isHTMLElement(container) && container.shadowRoot) {
    return Array.from(container.shadowRoot.children);
  }

  return Array.from(container.children);
}

function appendCandidates(container: ParentNode, list: FocusableElement[]): void {
  getComposedChildren(container).forEach((child) => {
    if (isFocusableCandidate(child)) {
      list.push(child);
    }

    appendCandidates(child, list);
  });
}

function appendMatchingElements(container: ParentNode, selector: string, list: HTMLElement[]): void {
  getComposedChildren(container).forEach((child) => {
    if (isHTMLElement(child) && child.matches(selector)) {
      list.push(child);
    }

    appendMatchingElements(child, selector, list);
  });
}

/**
 * Determines whether an element is in the sequential focus order.
 * @param element The element, or `null`.
 * @returns `true` when the element is focusable and its tab index, with the zero floor of `<details>`, `<audio>`,
 * `<video>` and contenteditable hosts, is not negative.
 */
export function isTabbable(element: Element | null): boolean {
  return isFocusableElement(element) && getTabIndex(element) >= 0;
}

/**
 * Lists the focusable elements in a container, in composed-tree order (slotted content and open shadow roots
 * included).
 * @param container The container, which is not itself a candidate.
 * @returns The connected, enabled, rendered candidates outside `inert`, `hidden` and closed `<details>` subtrees.
 */
export function focusable(container: Element): FocusableElement[] {
  const candidates: FocusableElement[] = [];
  appendCandidates(container, candidates);
  return candidates.filter(isFocusableElement);
}

/**
 * Lists the tabbable elements in a container, in composed-tree order.
 * @param container The container, which is not itself a candidate.
 * @returns The focusable elements whose tab index is not negative, with each named radio group collapsed to its
 * checked radio, or to its first radio when none is checked.
 */
export function tabbable(container: Element): FocusableElement[] {
  const candidates = focusable(container);
  return candidates.filter((element) => getTabIndex(element) >= 0 && isTabbableRadio(element, candidates));
}

function getTabbableIn(container: HTMLElement, dir: 1 | -1): FocusableElement | undefined {
  const list = tabbable(container);
  const len = list.length;
  if (len === 0) {
    return undefined;
  }

  const active = activeElement(ownerDocument(container)) as FocusableElement;
  const index = list.indexOf(active);
  const nextIndex = index === -1 ? (dir === 1 ? 0 : len - 1) : index + dir;

  return list[nextIndex];
}

/**
 * Finds the tabbable element after the focused one in the reference element's document body.
 * @param referenceElement The fallback, and the element whose document is searched.
 * @returns The next tabbable element (the first when focus is not on a tabbable element), else `referenceElement`.
 */
export function getNextTabbable(referenceElement: Element | null): FocusableElement | null {
  return getTabbableIn(ownerDocument(referenceElement).body, 1) || (referenceElement as FocusableElement | null);
}

/**
 * Finds the tabbable element before the focused one in the reference element's document body.
 * @param referenceElement The fallback, and the element whose document is searched.
 * @returns The previous tabbable element (the last when focus is not on a tabbable element), else `referenceElement`.
 */
export function getPreviousTabbable(referenceElement: Element | null): FocusableElement | null {
  return getTabbableIn(ownerDocument(referenceElement).body, -1) || (referenceElement as FocusableElement | null);
}

/**
 * Walks the body's candidates circularly from an element and returns the first tabbable one in a direction.
 * @param referenceElement The element to start from, which may itself be untabbable.
 * @param direction `1` for forward, `-1` for backward.
 * @param exclude An element whose subtree is skipped.
 * @returns The first tabbable, radio-eligible element outside `exclude`; else `referenceElement`; `null` when
 * `referenceElement` is missing or not in the composed tree.
 */
export function getTabbableNearElement(
  referenceElement: Element | null,
  direction: 1 | -1,
  exclude?: Element | null,
): FocusableElement | null {
  if (!referenceElement) {
    return null;
  }

  // Keep the anchor's composed-tree position separate from the focusable radio candidates.
  const list: FocusableElement[] = [];
  appendCandidates(ownerDocument(referenceElement).body, list);
  const index = list.indexOf(referenceElement as FocusableElement);
  if (index === -1) {
    return null;
  }

  const candidates = list.filter(isFocusableElement);
  for (let offset = 1; offset < list.length; offset += 1) {
    const element = list[(index + direction * offset + list.length) % list.length]!;
    if (!contains(exclude, element) && isTabbable(element) && isTabbableRadio(element, candidates)) {
      return element;
    }
  }

  return list[index] ?? null;
}

/**
 * Determines whether a focus event moved focus out of a container.
 * @param event The `focus`, `blur`, `focusin` or `focusout` event.
 * @param container The container; the event's current target when omitted.
 * @returns `true` when there is no related target or it is outside the container (composed).
 */
export function isOutsideEvent(event: FocusEvent, container?: Element): boolean {
  const containerElement = container || (event.currentTarget as Element);
  const relatedTarget = event.relatedTarget as HTMLElement | null;
  return !relatedTarget || !contains(containerElement, relatedTarget);
}

/**
 * Takes every tabbable element in a container out of the tab order, stashing each element's `tabindex` attribute (or
 * `''`) in `data-tabindex`.
 * @param container The container.
 */
export function disableFocusInside(container: HTMLElement): void {
  const tabbableElements = tabbable(container);
  tabbableElements.forEach((element) => {
    element.dataset.tabindex = element.getAttribute('tabindex') || '';
    element.setAttribute('tabindex', '-1');
  });
}

/**
 * Restores the `tabindex` attributes {@link disableFocusInside} stashed, removing the attribute where the stash is
 * empty, and deletes the stash.
 * @param container The container.
 */
export function enableFocusInside(container: HTMLElement): void {
  const elements: HTMLElement[] = [];
  appendMatchingElements(container, '[data-tabindex]', elements);
  elements.forEach((element) => {
    const tabindex = element.dataset.tabindex;
    delete element.dataset.tabindex;
    if (tabindex) {
      element.setAttribute('tabindex', tabindex);
    } else {
      element.removeAttribute('tabindex');
    }
  });
}

// ---------------------------------------------------------------------------------------------------------------
// Outside marking (FLT-ALG-19, FLT-ARIA-01..03, FLT-DOM-16, FLT-EDGE-05); a modified copy of theKashey/aria-hidden
// ---------------------------------------------------------------------------------------------------------------

/** The options of {@link markOthers}. */
export interface MarkOthersOptions {
  /** Whether the outside elements also get `aria-hidden="true"` (live regions and their ancestors excepted). */
  ariaHidden?: boolean | undefined;
  /** Whether the outside elements get the inert marker `data-base-ui-inert`; `true` by default. */
  mark?: boolean | undefined;
}

const markerName = 'data-base-ui-inert';

// Page-global: one count per element across every open popup, reset when the last lock releases.
let ariaHiddenCounterMap = new WeakMap<Element, number>();
let uncontrolledElementsSet = new WeakSet<Element>();
let markerCounterMap = new WeakMap<Element, number>();
let lockCount = 0;

function unwrapHost(node: Node | null): Element | null {
  if (!node) {
    return null;
  }

  return isShadowRoot(node) ? node.host : unwrapHost(node.parentNode);
}

function correctElements(parent: HTMLElement, targets: Element[]): Element[] {
  return targets
    .map((target) => {
      if (parent.contains(target)) {
        return target;
      }

      const correctedTarget = unwrapHost(target);

      if (parent.contains(correctedTarget)) {
        return correctedTarget;
      }

      return null;
    })
    .filter((x): x is Element => x != null);
}

function buildKeepSet(targets: Element[]): Set<Node> {
  const keep = new Set<Node>();

  targets.forEach((target) => {
    let node: Node | null = target;
    while (node && !keep.has(node)) {
      keep.add(node);
      node = node.parentNode;
    }
  });

  return keep;
}

function collectOutsideElements(root: HTMLElement, keepElements: Set<Node>, stopElements: Set<Node>): Element[] {
  const outside: Element[] = [];

  const walk = (parent: Element | null) => {
    if (!parent || stopElements.has(parent)) {
      return;
    }

    Array.from(parent.children).forEach((node: Element) => {
      if (getNodeName(node) === 'script') {
        return;
      }

      if (keepElements.has(node)) {
        walk(node);
      } else {
        outside.push(node);
      }
    });
  };

  walk(root);

  return outside;
}

function applyAttributeToOthers(
  uncorrectedAvoidElements: Element[],
  body: HTMLElement,
  ariaHidden: boolean,
  mark: boolean,
): () => void {
  const avoidElements = correctElements(body, uncorrectedAvoidElements);
  const markerTargets = mark ? collectOutsideElements(body, buildKeepSet(avoidElements), new Set<Node>(avoidElements)) : [];
  const hiddenElements: Element[] = [];
  const markedElements: Element[] = [];

  if (ariaHidden) {
    const ariaLiveElements = correctElements(body, Array.from(body.querySelectorAll('[aria-live]')));
    const controlElements = avoidElements.concat(ariaLiveElements);
    const controlTargets = collectOutsideElements(body, buildKeepSet(controlElements), new Set<Node>(controlElements));

    controlTargets.forEach((node) => {
      const attr = node.getAttribute('aria-hidden');
      const alreadyHidden = attr !== null && attr !== 'false';
      const counterValue = (ariaHiddenCounterMap.get(node) || 0) + 1;

      ariaHiddenCounterMap.set(node, counterValue);
      hiddenElements.push(node);

      if (counterValue === 1 && alreadyHidden) {
        uncontrolledElementsSet.add(node);
      }

      if (!alreadyHidden) {
        node.setAttribute('aria-hidden', 'true');
      }
    });
  }

  if (mark) {
    markerTargets.forEach((node) => {
      const markerValue = (markerCounterMap.get(node) || 0) + 1;

      markerCounterMap.set(node, markerValue);
      markedElements.push(node);

      if (markerValue === 1) {
        node.setAttribute(markerName, '');
      }
    });
  }

  lockCount += 1;

  return () => {
    hiddenElements.forEach((element) => {
      const counterValue = (ariaHiddenCounterMap.get(element) || 0) - 1;
      ariaHiddenCounterMap.set(element, counterValue);

      if (!counterValue) {
        if (!uncontrolledElementsSet.has(element)) {
          element.removeAttribute('aria-hidden');
        }

        uncontrolledElementsSet.delete(element);
      }
    });

    if (mark) {
      markedElements.forEach((element) => {
        const markerValue = (markerCounterMap.get(element) || 0) - 1;

        markerCounterMap.set(element, markerValue);

        if (!markerValue) {
          element.removeAttribute(markerName);
        }
      });
    }

    lockCount -= 1;

    if (!lockCount) {
      resetOutsideMarking();
    }
  };
}

function resetOutsideMarking(): void {
  ariaHiddenCounterMap = new WeakMap();
  uncontrolledElementsSet = new WeakSet();
  markerCounterMap = new WeakMap();
  lockCount = 0;
}

/**
 * Marks every element outside a set of inside elements, with reference counting across overlapping calls. The native
 * `inert` attribute is never written.
 * @param avoidElements The inside elements; one inside another document's shadow tree counts as its shadow host.
 * @param options Whether to write `aria-hidden="true"` (off by default) and the inert marker (on by default).
 * @returns The function that releases this call's marks; an element's mark is removed when its last lock releases,
 * and an `aria-hidden` the element had before its first lock is kept.
 */
export function markOthers(avoidElements: Element[], options: MarkOthersOptions = {}): () => void {
  const { ariaHidden = false, mark = true } = options;
  const body = ownerDocument(avoidElements[0]).body;
  return applyAttributeToOthers(avoidElements, body, ariaHidden, mark);
}

// ---------------------------------------------------------------------------------------------------------------
// The queued focus (FLT-FOC-22): one pending frame per page
// ---------------------------------------------------------------------------------------------------------------

/** The options of {@link enqueueFocus}. */
export interface EnqueueFocusOptions {
  /** Passed to `focus()`. */
  preventScroll?: boolean | undefined;
  /** Focuses at once instead of on the next frame. */
  sync?: boolean | undefined;
  /** Read when the frame runs; `false` skips the focus. */
  shouldFocus?: (() => boolean) | undefined;
}

const noop = (): void => {};

// Page-global: the frame of the one pending focus, 0 when none is pending.
let queuedFocusFrame = 0;

/**
 * Focuses an element on the next animation frame, canceling the focus any earlier call left pending.
 * @param el The element, or `null` to only cancel.
 * @param options The scroll, synchronous and gate options.
 * @returns The function that cancels this call's frame while it is still the pending one.
 */
export function enqueueFocus(el: FocusableElement | null | undefined, options: EnqueueFocusOptions = {}): () => void {
  const { preventScroll = false, sync = false, shouldFocus } = options;

  cancelFrame(queuedFocusFrame);
  queuedFocusFrame = 0;

  function exec(): void {
    if (shouldFocus && !shouldFocus()) {
      return;
    }
    el?.focus({ preventScroll });
  }

  if (sync) {
    exec();
    return noop;
  }

  const frame = requestFrame(() => {
    if (queuedFocusFrame === frame) {
      queuedFocusFrame = 0;
    }
    exec();
  });
  queuedFocusFrame = frame;
  return () => {
    if (queuedFocusFrame === frame) {
      cancelFrame(frame);
      queuedFocusFrame = 0;
    }
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The previously-focused stack (FLT-FOC-09, FLT-EDGE-06): page-global, at most 20 weakly held entries
// ---------------------------------------------------------------------------------------------------------------

const LIST_LIMIT = 20;
let previouslyFocusedElements: WeakRef<Element>[] = [];

function clearDisconnectedPreviouslyFocusedElements(): void {
  previouslyFocusedElements = previouslyFocusedElements.filter((entry) => entry.deref()?.isConnected);
}

function addPreviouslyFocusedElement(element: Element | null | undefined): void {
  clearDisconnectedPreviouslyFocusedElements();
  if (element && getNodeName(element) !== 'body') {
    previouslyFocusedElements.push(new WeakRef(element));
    if (previouslyFocusedElements.length > LIST_LIMIT) {
      previouslyFocusedElements = previouslyFocusedElements.slice(-LIST_LIMIT);
    }
  }
}

function getPreviouslyFocusedElement(): Element | undefined {
  clearDisconnectedPreviouslyFocusedElements();
  return previouslyFocusedElements[previouslyFocusedElements.length - 1]?.deref();
}

/**
 * Drops the module's page-global state: every outside-marking lock's bookkeeping, the previously-focused stack and the
 * queued focus. For tests only, like `resetFrameScheduler`: the state is shared by every handle on the page, so a lock
 * or a pending focus one test left behind would otherwise reach the next.
 */
export function resetFocusManagerState(): void {
  resetOutsideMarking();
  previouslyFocusedElements = [];
  cancelFrame(queuedFocusFrame);
  queuedFocusFrame = 0;
}

// ---------------------------------------------------------------------------------------------------------------
// The focus manager's handle (FloatingFocusManager.tsx)
// ---------------------------------------------------------------------------------------------------------------

const FOCUSABLE_ATTRIBUTE = 'data-base-ui-focusable';
const FOCUS_GUARD_ATTRIBUTE = 'data-base-ui-focus-guard';
const CLICK_TRIGGER_ATTRIBUTE = 'data-base-ui-click-trigger';
const PORTAL_ATTRIBUTE = 'data-base-ui-portal';
const TYPEABLE_SELECTOR =
  "input:not([type='hidden']):not([disabled]),[contenteditable]:not([contenteditable='false']),textarea:not([disabled])";

/** The interaction type literals (Base UI's `InteractionType`); `''` when none is known. */
export type InteractionTypeLiteral = 'mouse' | 'touch' | 'pen' | 'keyboard' | '';

/** Where focus goes when the focused element inside the popup is lost; Base UI's `false`, `true` and `'popup'`. */
export type FocusRestoration = 'none' | 'tabbable' | 'popup';

// The record is the popup core's `OpenChangeRelay` (ADR 04 Amendments 2026-10-27), with the two coalesced-close
// members of track 3's design point 11.
/** The last committed open change, as C# relays it in `update`. */
export interface OpenChangeRelay {
  /** Moves on every committed open change; the module acts once per value. */
  revision: number;
  /** The change's reason literal, such as `focus-out` or `trigger-hover`. */
  reason: string;
  /** The `kind` of the change's event snapshot, or `null` when no browser event caused it. */
  eventKind?: string | null;
  /** The `type` of the change's event, such as `mouseleave`. */
  eventType?: string | null;
  /** The event's pointer type. */
  pointerType?: string | null;
  /** The id of the trigger the change names. */
  triggerId?: string | null;
  /** The revision of a close the same batch committed before this open, or `null` when there was none. */
  coalescedCloseRevision?: number | null;
  /** The reason of that close. */
  coalescedCloseReason?: string | null;
}

/** The options of a focus manager handle; mirrors `FocusManagerOptions` in C#. */
export interface FocusManagerOptions extends TwinOptions {
  /** Traps focus and hides the outside with `aria-hidden`; `true` by default. */
  modal?: boolean | undefined;
  /** Turns every behavior off: no initial focus, trap, marking or return focus. */
  disabled?: boolean | undefined;
  /** The open's resolved initial focus: `false` does nothing; `true` takes the target, else the default. */
  initialFocus?: boolean | undefined;
  /** The initial focus target, by the index of its reference in the references passed with the options. */
  initialFocusElement?: number | null | undefined;
  /** The initial focus target, by element id. */
  initialFocusId?: string | null | undefined;
  /** The close's resolved return focus: `false` does nothing; `true` takes the target, else the default. */
  returnFocus?: boolean | undefined;
  /** The return focus target, by reference index. */
  returnFocusElement?: number | null | undefined;
  /** The return focus target, by element id. */
  returnFocusId?: string | null | undefined;
  /** Whether return focus moves focus even when it already left the popup; by default, whether a target is set. */
  explicitReturnFocus?: boolean | null | undefined;
  /** The close's interaction type, as C# classified it; `keyboard` asks for `focusVisible`. */
  closeInteractionType?: string | null | undefined;
  /** Where focus goes when the focused element inside is removed or hidden; `none` by default. */
  restoreFocus?: FocusRestoration | undefined;
  /** Closes a non-modal popup when focus leaves it and restores focus inside; `true` by default. */
  closeOnFocusOut?: boolean | undefined;
  /** The open's interaction type literal; `null` for a programmatic open, which prefers the previous focus on return. */
  openInteractionType?: string | null | undefined;
  /** The selector the popup element matches while C# renders it open; `[data-open]` by default. */
  openSelector?: string | null | undefined;
  /** Whether the popup element is the floating element (Dialog); otherwise its parent is (the Positioner). */
  treatPopupAsFloatingElement?: boolean | undefined;
  /** The id of the anchor element, the active trigger. */
  domReferenceId?: string | null | undefined;
  /**
   * Whether the anchor carries the trigger focus guards: the guard before it is an inside element, the anchor is the
   * element before the popup in focus order and the guard after it the element after (Popover).
   */
  triggerFocusGuards?: boolean | undefined;
  /** The element focused when Tab leaves the popup backward, by reference index (Menu's). */
  previousFocusableElement?: number | null | undefined;
  /** The element focused when Tab leaves the popup forward, by reference index (Menu's). */
  nextFocusableElement?: number | null | undefined;
  /** Elements outside the popup that count as inside it, by reference index (Base UI's `getInsideElements`). */
  insideElements?: number[] | null | undefined;
  /** Elements outside the popup that count as inside it, by element id. */
  insideElementIds?: string[] | null | undefined;
  /** The last committed open change; the module acts when its revision moves. */
  relay?: OpenChangeRelay | null | undefined;
  /** How many of the module's requests C# has answered, canceled or not; the module asks again when it moves. */
  answered?: number | undefined;
}

/** The focus-out close request; mirrors `FocusOutCloseRequest` in C#. */
export interface FocusOutCloseRequest {
  /** The `focusout` event's snapshot. */
  event: EventInfo;
  /** The interaction type the module last recorded. */
  interactionType: InteractionTypeLiteral;
}

/** The recorded interaction type; mirrors `InteractionTypeChange` in C#. */
export interface InteractionTypeChange {
  /** The literal, `''` after an open reset it. */
  type: InteractionTypeLiteral;
}

/** The payload of the internal `openchange` listener (Base UI's `FloatingUIOpenChangeDetails`). */
interface OpenChangeDetails {
  reason: string;
  nativeEvent: { type: string; pointerType: string | null } | undefined;
  nested: boolean;
  triggerElement: Element | undefined;
}

/** The report a focus-out close sends. */
export const FOCUS_OUT_CLOSE_REPORT = 'OnFocusOutClose';
/** The report the recorded interaction type sends, once per change. */
export const INTERACTION_TYPE_REPORT = 'OnInteractionTypeChange';

// A symbol on the element, not a module registry: a repeated attach of the same element returns the live handle.
const attachedHandle = Symbol('blazix.focus-manager.handle');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

// The `portal` module publishes its state on its node under this key and the focus manager fills its slot (design
// point 3 of the focus-manager track, settled by the orchestrator 2026-10-27): the two modules import nothing from each
// other, so the shapes are declared on both sides.
const PORTAL_STATE = Symbol.for('blazix.portal');

/** What the manager publishes to the portal it is rendered inside (Base UI's `FocusManagerState`). */
interface PortalFocusPublication {
  modal: boolean;
  closeOnFocusOut: boolean;
  open: boolean;
  domReference: Element | null;
  insideGuards: { before: HTMLElement | null; after: HTMLElement | null };
  close(event: Event): void;
}

/** The part of the portal's published state the manager reads and writes. */
interface PortalState {
  outsideGuards(): { before: HTMLElement | null; after: HTMLElement | null };
  focusManager: PortalFocusPublication | null;
}

interface PortalNode extends HTMLElement {
  [PORTAL_STATE]?: PortalState;
}

/** One effect of the manager: set up while its key is non-null, torn down and set up again when the key changes. */
interface Effect {
  key: string | null;
  deps: unknown[];
  cleanup: (() => void) | void;
}

function sameDeps(a: unknown[], b: unknown[]): boolean {
  return a.length === b.length && a.every((value, index) => Object.is(value, b[index]));
}

function getFloatingFocusElement(floatingElement: HTMLElement | null): HTMLElement | null {
  if (!floatingElement) {
    return null;
  }
  return floatingElement.hasAttribute(FOCUSABLE_ATTRIBUTE)
    ? floatingElement
    : (floatingElement.querySelector<HTMLElement>(`[${FOCUSABLE_ATTRIBUTE}]`) ?? floatingElement);
}

function getFirstTabbableElement(container: Element | null): Element | null {
  if (!container) {
    return null;
  }

  if (isTabbable(container)) {
    return container;
  }

  return tabbable(container)[0] || container;
}

// FLT-ALG-27
function handleTabIndex(floatingFocusElement: HTMLElement): void {
  if (floatingFocusElement.hasAttribute('tabindex') && !floatingFocusElement.hasAttribute('data-tabindex')) {
    return;
  }

  if (!floatingFocusElement.getAttribute('role')?.includes('dialog')) {
    return;
  }

  const focusableElements = focusable(floatingFocusElement);
  const tabbableContent = focusableElements.filter((element) => {
    const dataTabIndex = element.getAttribute('data-tabindex') || '';
    return isTabbable(element) || (element.hasAttribute('data-tabindex') && !dataTabIndex.startsWith('-'));
  });
  const tabIndex = floatingFocusElement.getAttribute('tabindex');

  if (tabbableContent.length === 0) {
    if (tabIndex !== '0') {
      floatingFocusElement.setAttribute('tabindex', '0');
      // Marks the module's own write, so the consumer-managed early return above does not freeze management.
      floatingFocusElement.setAttribute('data-tabindex', '0');
    }
  } else if (
    tabIndex !== '-1' ||
    (floatingFocusElement.hasAttribute('data-tabindex') && floatingFocusElement.getAttribute('data-tabindex') !== '-1')
  ) {
    floatingFocusElement.setAttribute('tabindex', '-1');
    floatingFocusElement.setAttribute('data-tabindex', '-1');
  }
}

function isTypeableElement(element: unknown): boolean {
  return isHTMLElement(element) && element.matches(TYPEABLE_SELECTOR);
}

function stopEvent(event: Event): void {
  event.preventDefault();
  event.stopPropagation();
}

function isInsideGuard(element: Element | null): element is HTMLElement {
  return (
    isHTMLElement(element) && element.hasAttribute(FOCUS_GUARD_ATTRIBUTE) && element.getAttribute('data-type') === 'inside'
  );
}

function isFocusGuard(element: Element | null | undefined): element is HTMLElement {
  return isHTMLElement(element) && element.hasAttribute(FOCUS_GUARD_ATTRIBUTE);
}

// Feature-detects `focus({ preventScroll })` by focusing a detached element with a getter (FLT-FOC-08).
function supportsPreventScroll(doc: Document): boolean {
  let supported = false;
  doc.createElement('div').focus({
    get preventScroll() {
      supported = true;
      return false;
    },
  });
  return supported;
}

/**
 * Attaches the focus manager to a popup element. From here on the module reads the open state from the applied
 * batch (the element matching `openSelector`), marks the outside while open, traps Tab when modal, moves initial focus
 * on open and returns it when the manager stops, closes a non-modal popup when focus leaves it (a request), restores
 * focus inside, manages the floating focus element's `tabindex`, routes the inside focus guards C# renders beside the
 * element and reports the interaction type it records.
 * @param elementOrId The popup element, or its id when the part has no element reference.
 * @param optionsJson The `FocusManagerOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param references The elements the options name by index, revived by Blazor.
 * @returns The handle with `update(optionsJson, references)` and `dispose()`; the live handle when the element is
 * already attached.
 */
export function attach(
  elementOrId: Element | string,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
): TwinHandle {
  const popup = resolveElement(elementOrId) as AttachedElement;
  const existing = popup[attachedHandle];
  if (existing) {
    return existing;
  }

  const twin = createTwinChannel(twinReference);
  let options = parseOptions<FocusManagerOptions>(optionsJson);
  let optionReferences: TwinReferences = references;
  let disposed = false;
  const doc = ownerDocument(popup);

  const openSelectorOf = (current: FocusManagerOptions) => current.openSelector || '[data-open]';
  let open = popup.matches(openSelectorOf(options));

  // The per-handle refs of FloatingFocusManager.tsx:308-313.
  let preventReturnFocus = false;
  let isPointerDown = false;
  let pointerDownOutside = false;
  let lastFocusedTabbable: Element | null = null;
  let lastInteractionType: InteractionTypeLiteral = '';
  let pointerDownTimeout: ReturnType<typeof setTimeout> | undefined;
  let blurTimeout: ReturnType<typeof setTimeout> | undefined;
  let restoreFocusFrame = 0;
  // Base UI's `dataRef.current.insideReactTree`; the session's once the interactions export is on `main`.
  const dataRef: { current: { insideReactTree?: boolean } } = { current: {} };
  // A focus-out close is a request: the module disarms after sending one until the next open (ADR 04 Amendments
  // 2026-10-27, "a module disarms itself after requesting a close until the next open arrives").
  let closeRequested = false;
  let relayRevision = options.relay?.revision ?? null;
  let answered = options.answered ?? 0;
  let onOpenChangeLocal: ((details: OpenChangeDetails) => void) | null = null;
  let pendingReturnFocus: { cancelled: boolean } | null = null;
  // Whether the batch removed the popup element: the manager stops as it does on dispose (design point 1, chosen by
  // the user 2026-10-27: return focus also runs when the element leaves the document).
  let removed = false;
  // Stops the one re-pick of the default initial focus (the attach-order rule); a recorded interaction calls it.
  let stopRepick: (() => void) | null = null;
  // The inside guards whose VoiceOver role has been written.
  const swappedGuards = new WeakSet<Element>();
  let guardListeners: { element: HTMLElement; listener: (event: FocusEvent) => void }[] = [];

  const resolveId = (id: string | null | undefined): HTMLElement | null => {
    if (!id) {
      return null;
    }
    const scope = popup.getRootNode();
    const found =
      'getElementById' in scope && typeof (scope as Document | ShadowRoot).getElementById === 'function'
        ? (scope as Document | ShadowRoot).getElementById(id)
        : null;
    const element = found ?? doc.getElementById(id);
    return isHTMLElement(element) ? element : null;
  };

  const resolveReference = (index: number | null | undefined): HTMLElement | null => {
    const element = resolveTwinReference<Element>(optionReferences, index);
    return isHTMLElement(element) ? element : null;
  };

  const resolveTarget = (index: number | null | undefined, id: string | null | undefined): HTMLElement | null =>
    resolveReference(index) ?? resolveId(id);

  const hasTarget = (index: number | null | undefined, id: string | null | undefined): boolean => index != null || !!id;

  const floatingElement = (): HTMLElement | null =>
    options.treatPopupAsFloatingElement ? popup : isHTMLElement(popup.parentElement) ? popup.parentElement : popup;
  const domReferenceElement = (): HTMLElement | null => resolveId(options.domReferenceId);
  const beforeGuard = (): HTMLElement | null => (isInsideGuard(popup.previousElementSibling) ? popup.previousElementSibling : null);
  const afterGuard = (): HTMLElement | null => (isInsideGuard(popup.nextElementSibling) ? popup.nextElementSibling : null);
  // Popover's trigger focus guards (PSH-FOC-09, FLT-KEY-06), found by position around the anchor.
  const triggerBeforeGuard = (reference: Element | null): HTMLElement | null =>
    options.triggerFocusGuards && isFocusGuard(reference?.previousElementSibling) ? reference.previousElementSibling : null;
  // Popover passes the trigger and its after-guard (by position, `triggerFocusGuards`); Menu passes elements (by index).
  const previousFocusableElement = (reference: HTMLElement | null): HTMLElement | null =>
    resolveReference(options.previousFocusableElement) ?? (options.triggerFocusGuards ? reference : null);
  const nextFocusableElement = (reference: Element | null): HTMLElement | null =>
    resolveReference(options.nextFocusableElement) ??
    (options.triggerFocusGuards && isFocusGuard(reference?.nextElementSibling) ? reference.nextElementSibling : null);
  const insideElements = (reference: Element | null): Element[] => [
    ...(options.insideElements ?? []).map(resolveReference),
    ...(options.insideElementIds ?? []).map(resolveId),
    triggerBeforeGuard(reference),
  ].filter((x): x is HTMLElement => x != null);
  // The enclosing portal's node (Base UI's `PortalContext`), and the state the `portal` module published on it.
  const portalNode = (floating: Element | null): HTMLElement | null =>
    floating?.parentElement?.closest<HTMLElement>(`[${PORTAL_ATTRIBUTE}]`) ?? null;
  const portalState = (portal: HTMLElement | null): PortalState | null => (portal as PortalNode | null)?.[PORTAL_STATE] ?? null;
  const outsideGuards = (portal: HTMLElement | null): (HTMLElement | null)[] => {
    const guards = portalState(portal)?.outsideGuards();
    return [guards?.before ?? null, guards?.after ?? null];
  };

  const getTabbableContent = (container: Element | null): FocusableElement[] => (container ? tabbable(container) : []);

  const recordInteractionType = (type: InteractionTypeLiteral): void => {
    if (type === lastInteractionType) {
      return;
    }
    lastInteractionType = type;
    twin.report(INTERACTION_TYPE_REPORT, { type } satisfies InteractionTypeChange);
  };

  const requestClose = (snapshot: EventInfo): void => {
    if (closeRequested || disposed) {
      return;
    }
    closeRequested = true;
    twin.report(FOCUS_OUT_CLOSE_REPORT, { event: snapshot, interactionType: lastInteractionType } satisfies FocusOutCloseRequest);
  };

  // ---- the effects, in FloatingFocusManager.tsx's order ----

  // The Tab trap (FLT-KEY-04), :342-363.
  const trapEffect = (floatingFocusElement: HTMLElement | null) => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Tab') {
        return;
      }
      // The focus guards have nothing to focus, so the event is stopped.
      if (
        contains(floatingFocusElement, activeElement(ownerDocument(floatingFocusElement))) &&
        getTabbableContent(floatingFocusElement).length === 0
      ) {
        stopEvent(event);
      }
    };
    const target = ownerDocument(floatingFocusElement);
    target.addEventListener('keydown', onKeyDown);
    return () => target.removeEventListener('keydown', onKeyDown);
  };

  // The interaction recording (FLT-FOC-10's JavaScript half, FLT-FOC-13), :365-421.
  const recordingEffect = (floating: HTMLElement | null, reference: HTMLElement | null, portal: HTMLElement | null) => {
    const clearPointerDownOutside = () => {
      pointerDownOutside = false;
    };
    const onPointerDown = (event: PointerEvent) => {
      const target = getTarget(event) as Element | null;
      const pointerTargetInside =
        contains(floating, target) ||
        contains(reference, target) ||
        contains(portal, target) ||
        insideElements(reference).some((element) => element === target || contains(element, target));
      pointerDownOutside = !pointerTargetInside;
      stopRepick?.();
      recordInteractionType(((event.pointerType as InteractionTypeLiteral) || 'keyboard') as InteractionTypeLiteral);

      if (closest(target, `[${CLICK_TRIGGER_ATTRIBUTE}]`)) {
        isPointerDown = true;
        // Reset on the next tick so one click on a click trigger does not suppress focus-out closing for good.
        clearTimeout(pointerDownTimeout);
        pointerDownTimeout = setTimeout(() => {
          isPointerDown = false;
        }, 0);
      }
    };
    const onKeyDown = () => {
      stopRepick?.();
      recordInteractionType('keyboard');
    };
    doc.addEventListener('pointerdown', onPointerDown, true);
    doc.addEventListener('pointerup', clearPointerDownOutside, true);
    doc.addEventListener('pointercancel', clearPointerDownOutside, true);
    doc.addEventListener('keydown', onKeyDown, true);
    return () => {
      doc.removeEventListener('pointerdown', onPointerDown, true);
      doc.removeEventListener('pointerup', clearPointerDownOutside, true);
      doc.removeEventListener('pointercancel', clearPointerDownOutside, true);
      doc.removeEventListener('keydown', onKeyDown, true);
      // A stale `true` must not leak into the next open of a kept-mounted popup (FLT-EDGE-08).
      clearPointerDownOutside();
    };
  };

  // The focus-out close and the restoration (FLT-FOC-11, FOC-12, FOC-13, ALG-22), :423-617.
  const focusOutEffect = (
    floating: HTMLElement | null,
    floatingFocusElement: HTMLElement | null,
    reference: HTMLElement | null,
    portal: HTMLElement | null,
    modal: boolean,
    restoreFocus: FocusRestoration,
  ) => {
    // In Safari, buttons lose focus when pressing them.
    const handlePointerDown = () => {
      isPointerDown = true;
      clearTimeout(pointerDownTimeout);
      pointerDownTimeout = setTimeout(() => {
        isPointerDown = false;
      }, 0);
    };

    const handleFocusIn = (event: FocusEvent) => {
      const target = getTarget(event) as Element | null;
      if (isTabbable(target)) {
        lastFocusedTabbable = target;
      }
    };

    const handleFocusOutside = (event: FocusEvent) => {
      const relatedTarget = event.relatedTarget as HTMLElement | null;
      const currentTarget = event.currentTarget;
      const target = getTarget(event) as HTMLElement | null;
      const snapshot = eventInfo(event);

      // Focus lost to the body (a backdrop press): record the element that had it, so a dialog opened while the body
      // is focused can return focus to it. Modal only, so non-modal popups do not pollute the shared stack.
      if (modal && relatedTarget == null && target != null && contains(floating, target)) {
        addPreviouslyFocusedElement(target);
      }

      queueMicrotask(() => {
        const inside = insideElements(reference);
        const isRelatedFocusGuard =
          isFocusGuard(relatedTarget) &&
          [
            beforeGuard(),
            afterGuard(),
            ...outsideGuards(portal),
            previousFocusableElement(reference),
            nextFocusableElement(reference),
          ].includes(relatedTarget);

        const movedToUnrelatedNode = !(
          contains(reference, relatedTarget) ||
          contains(floating, relatedTarget) ||
          contains(relatedTarget, floating) ||
          contains(portal, relatedTarget) ||
          inside.some((element) => element === relatedTarget || contains(element, relatedTarget)) ||
          isRelatedFocusGuard
        );

        if (currentTarget === reference && floatingFocusElement) {
          handleTabIndex(floatingFocusElement);
        }

        // Restore focus to the previously focused tabbable element so focus is not lost outside the floating tree.
        if (
          restoreFocus !== 'none' &&
          currentTarget !== reference &&
          !isElementVisible(target) &&
          activeElement(doc) === doc.body
        ) {
          if (isHTMLElement(floatingFocusElement)) {
            floatingFocusElement.focus();
            // `popup` restores to the floating element itself without searching for a tabbable element.
            if (restoreFocus === 'popup') {
              // An element removed on pointerdown makes the browser move focus back to it right after the call above,
              // and because it is gone focus is lost instead; re-focusing on the next frame wins that race.
              cancelFrame(restoreFocusFrame);
              restoreFocusFrame = requestFrame(() => {
                floatingFocusElement.focus();
              });
              return;
            }
          }

          const tabbableContent = getTabbableContent(floatingFocusElement) as Element[];
          const prevTabbable = lastFocusedTabbable;
          const nodeToFocus =
            (prevTabbable && tabbableContent.includes(prevTabbable) ? prevTabbable : null) ||
            tabbableContent[tabbableContent.length - 1] ||
            floatingFocusElement;

          if (isHTMLElement(nodeToFocus)) {
            nodeToFocus.focus();
          }
        }

        if (dataRef.current.insideReactTree) {
          dataRef.current.insideReactTree = false;
          return;
        }

        // Focus moved outside the floating tree and no portal guard handles the close.
        if (
          !modal &&
          relatedTarget &&
          movedToUnrelatedNode &&
          !isPointerDown &&
          relatedTarget !== getPreviouslyFocusedElement()
        ) {
          preventReturnFocus = true;
          requestClose(snapshot);
        }
      });
    };

    const markInsideReactTree = () => {
      if (pointerDownOutside) {
        return;
      }
      dataRef.current.insideReactTree = true;
      clearTimeout(blurTimeout);
      blurTimeout = setTimeout(() => {
        dataRef.current.insideReactTree = false;
      }, 0);
    };

    reference?.addEventListener('focusout', handleFocusOutside);
    reference?.addEventListener('pointerdown', handlePointerDown);
    floating?.addEventListener('focusin', handleFocusIn);
    floating?.addEventListener('focusout', handleFocusOutside);
    if (floating && portal) {
      floating.addEventListener('focusout', markInsideReactTree, true);
    }
    return () => {
      reference?.removeEventListener('focusout', handleFocusOutside);
      reference?.removeEventListener('pointerdown', handlePointerDown);
      floating?.removeEventListener('focusin', handleFocusIn);
      floating?.removeEventListener('focusout', handleFocusOutside);
      floating?.removeEventListener('focusout', markInsideReactTree, true);
    };
  };

  // The outside marking while open (FLT-ARIA-01..03, DOM-16), :619-677.
  const markingEffect = (floating: HTMLElement, reference: HTMLElement | null, portal: HTMLElement | null, modal: boolean) => {
    // Portals nested inside this portal stay exposed.
    const portalNodes = Array.from(portal?.querySelectorAll(`[${PORTAL_ATTRIBUTE}]`) ?? []);
    const controlInsideElements = [
      floating,
      ...portalNodes,
      beforeGuard(),
      afterGuard(),
      ...outsideGuards(portal),
      ...insideElements(reference),
    ];
    const inside = [...controlInsideElements, previousFocusableElement(reference), nextFocusableElement(reference)].filter(
      (x): x is Element => x != null,
    );

    const ariaHiddenCleanup = markOthers(inside, { ariaHidden: modal, mark: false });
    const markerCleanup = markOthers([floating, ...portalNodes]);

    return () => {
      markerCleanup();
      ariaHiddenCleanup();
    };
  };

  // Initial focus on open (FLT-FOC-06, A11Y-04), :679-769.
  const initialFocusEffect = (floatingFocusElement: HTMLElement) => {
    // FLT-EDGE-08: the recorded type starts empty in each open session.
    recordInteractionType('');

    const previouslyFocusedElement = activeElement(doc);

    // Waits for the other post-batch writes, such as the managed `tabindex`, to land.
    queueMicrotask(() => {
      if (disposed || options.initialFocus === false) {
        return;
      }

      if (contains(floatingFocusElement, previouslyFocusedElement)) {
        return;
      }

      let focusableElements: FocusableElement[] | null = null;
      const getDefaultFocusElement = () => {
        if (focusableElements == null) {
          focusableElements = getTabbableContent(floatingFocusElement);
        }
        return focusableElements[0] || floatingFocusElement;
      };

      const target = resolveTarget(options.initialFocusElement, options.initialFocusId);
      const elToFocus: FocusableElement = target || getDefaultFocusElement();

      const hadFocusInside = contains(floatingFocusElement, activeElement(doc));

      if (!target) {
        watchLateTabStop(floatingFocusElement, elToFocus, previouslyFocusedElement);
      }

      // The interactions session's recorded open event decides FLT-A11Y-04's synchronous focus; until it is on
      // `main` no open is a virtual press.
      const openedByVirtualPress = false;

      enqueueFocus(elToFocus, {
        sync: openedByVirtualPress,
        preventScroll: elToFocus === floatingFocusElement,
        shouldFocus() {
          // A popup that closed before this runs (tabbing out of a kept-mounted popup) does not pull focus back.
          if (!open || disposed) {
            return false;
          }

          if (hadFocusInside) {
            return true;
          }

          const currentActiveElement = activeElement(doc);
          const focusMovedInside =
            currentActiveElement !== elToFocus && contains(floatingFocusElement, currentActiveElement);

          return !focusMovedInside;
        },
      });
    });

    return () => {
      stopRepick?.();
    };
  };

  // The attach-order rule (design point 1's composite clause): a composite item renders `tabindex="-1"` until the
  // `composite` module attaches, so while initial focus holds the default it chose and nothing has been interacted with,
  // a `tabindex` written inside the floating focus element re-picks the default once.
  const watchLateTabStop = (floatingFocusElement: HTMLElement, chosen: Element, previouslyFocused: Element | null): void => {
    stopRepick?.();
    const observer = new (ownerWindow(floatingFocusElement).MutationObserver)((records) => {
      if (!records.some((record) => record.target !== floatingFocusElement)) {
        return;
      }
      const active = activeElement(doc);
      const untouched = active === chosen || active === previouslyFocused || active === doc.body;
      const next = getTabbableContent(floatingFocusElement)[0] || floatingFocusElement;
      if (!untouched || !open || disposed) {
        stop();
        return;
      }
      if (next !== chosen) {
        stop();
        enqueueFocus(next, { preventScroll: next === floatingFocusElement, shouldFocus: () => open && !disposed });
      }
    });
    const stop = () => {
      observer.disconnect();
      if (stopRepick === stop) {
        stopRepick = null;
      }
    };
    observer.observe(floatingFocusElement, { subtree: true, attributes: true, attributeFilter: ['tabindex'] });
    stopRepick = stop;
  };

  // Return focus, armed while enabled and released when the manager stops (FLT-FOC-07, FOC-08, FOC-20, FOC-21,
  // ALG-21, KEY-09, EDGE-07), :771-952.
  const returnFocusEffect = (floating: HTMLElement | null, floatingFocusElement: HTMLElement, reference: HTMLElement | null) => {
    if (pendingReturnFocus) {
      pendingReturnFocus.cancelled = true;
      pendingReturnFocus = null;
    }

    const elementFocusedBeforeOpen = activeElement(doc);
    // Only `null` is a programmatic open; an absent value is Base UI's `''`.
    const preferPreviousFocus = options.openInteractionType === null;

    addPreviouslyFocusedElement(elementFocusedBeforeOpen);

    // The batch that removes the popup, or any of its ancestors, releases return focus pre-paint, before C# disposes
    // the handle. Each ancestor's own child list is observed, not the document's subtree; a move (the portal's) is
    // followed by observing the new ancestors.
    const observeAncestors = () => {
      for (let node = popup.parentNode; node; node = node.parentNode) {
        removal.observe(node, { childList: true });
      }
    };
    const removal = new (ownerWindow(popup).MutationObserver)(() => {
      if (removed) {
        return;
      }
      if (!popup.isConnected) {
        removed = true;
        sync();
        return;
      }
      observeAncestors();
    });
    observeAncestors();

    onOpenChangeLocal = (details) => {
      // Focus guards transfer focus themselves; other close handlers may still need return focus.
      if (
        (details.reason === 'focus-out' && details.triggerElement?.hasAttribute(FOCUS_GUARD_ATTRIBUTE)) ||
        (details.reason === 'trigger-hover' && details.nativeEvent?.type === 'mouseleave')
      ) {
        preventReturnFocus = true;
      }

      if (details.reason !== 'outside-press') {
        return;
      }

      if (details.nested) {
        preventReturnFocus = false;
      } else {
        // An outside press returns focus only where `focus({ preventScroll })` is supported; elsewhere restoring
        // focus scrolls the page (Chrome on Android, Samsung Internet). The virtual-press branch reads the
        // interactions session's event once it is on `main`.
        preventReturnFocus = !supportsPreventScroll(ownerDocument(floatingFocusElement));
      }
    };

    const getReturnElement = (): Element | null => {
      if (options.returnFocus === false) {
        return null;
      }

      const referenceReturnElement = reference?.isConnected ? reference : null;
      const previousReturnElement =
        elementFocusedBeforeOpen?.isConnected && getNodeName(elementFocusedBeforeOpen) !== 'body'
          ? elementFocusedBeforeOpen
          : null;

      let defaultReturnElement = preferPreviousFocus
        ? previousReturnElement || referenceReturnElement
        : referenceReturnElement || previousReturnElement;

      if (!defaultReturnElement) {
        defaultReturnElement = getPreviouslyFocusedElement() || null;
      }

      if (!hasTarget(options.returnFocusElement, options.returnFocusId)) {
        return defaultReturnElement;
      }

      return resolveTarget(options.returnFocusElement, options.returnFocusId) || defaultReturnElement || null;
    };

    return () => {
      removal.disconnect();
      onOpenChangeLocal = null;

      const activeEl = activeElement(doc);
      const isFocusInsideFloatingTree =
        contains(floating, activeEl) ||
        insideElements(reference).some((element) => element === activeEl || contains(element, activeEl));

      const returnFocusEnabled = options.returnFocus !== false;
      const closeType = options.closeInteractionType ?? '';
      const returnElement = getReturnElement();

      const job = { cancelled: false };
      pendingReturnFocus = job;

      queueMicrotask(() => {
        if (pendingReturnFocus === job) {
          pendingReturnFocus = null;
        }
        // The return element if it is tabbable, otherwise its first tabbable child, otherwise the element itself.
        const tabbableReturnElement = getFirstTabbableElement(returnElement);
        // Read here on purpose: the latest `explicitReturnFocus` decides.
        const hasExplicitReturnFocus =
          options.explicitReturnFocus ?? hasTarget(options.returnFocusElement, options.returnFocusId);

        if (
          !job.cancelled &&
          returnFocusEnabled &&
          !preventReturnFocus &&
          isHTMLElement(tabbableReturnElement) &&
          // Focus that moved somewhere else after mount entered a different element, which is respected.
          (!hasExplicitReturnFocus && tabbableReturnElement !== activeEl && activeEl !== doc.body
            ? isFocusInsideFloatingTree
            : true)
        ) {
          const focusOptions: FocusOptions & { focusVisible?: boolean } = { preventScroll: true };
          if (closeType === 'keyboard') {
            focusOptions.focusVisible = true;
          }
          tabbableReturnElement.focus(focusOptions);
        }

        // A canceled return also clears the suppression before the next close.
        preventReturnFocus = false;
      });
    };
  };

  // Safari may scroll to the bottom of the page when an input inside a popup has focus as the popup unmounts, so the
  // input is blurred in the batch that closes it (FLT-FOC-16), :954-970.
  const webkitBlurEffect = (floating: HTMLElement) => {
    const activeEl = activeElement(ownerDocument(floating));
    if (isHTMLElement(activeEl) && isTypeableElement(activeEl) && contains(floating, activeEl)) {
      activeEl.blur();
    }
  };

  // The publication to the enclosing portal (FLT-FOC-18), :972-990: the portal routes its outside guards with it, and
  // its close is the focus-out request.
  const publicationEffect = (state: PortalState, modal: boolean, closeOnFocusOut: boolean, reference: HTMLElement | null) => {
    state.focusManager = {
      modal,
      closeOnFocusOut,
      open,
      domReference: reference,
      insideGuards: { before: beforeGuard(), after: afterGuard() },
      close: (event: Event) => requestClose(eventInfo(event)),
    };
    return () => {
      state.focusManager = null;
    };
  };

  // The managed `tabindex` (FLT-DOM-17), :992-1001.
  const tabIndexEffect = (floatingFocusElement: HTMLElement) => {
    handleTabIndex(floatingFocusElement);
    return () => {
      queueMicrotask(clearDisconnectedPreviouslyFocusedElements);
    };
  };

  const effects: Record<string, Effect> = {};
  const runEffect = (name: string, active: boolean, deps: unknown[], setup: () => (() => void) | void): void => {
    const current = effects[name];
    const key = active ? name : null;
    if (current && current.key === key && (key === null || sameDeps(current.deps, deps))) {
      return;
    }
    current?.cleanup?.();
    effects[name] = { key, deps, cleanup: key ? setup() : undefined };
  };

  // Reconciles every effect with the current inputs: the open state of the last batch and the latest options. The
  // layout effects run before the others, as React commits them.
  const sync = (): void => {
    const disabled = disposed || removed || options.disabled === true;
    const modal = options.modal !== false;
    const closeOnFocusOut = options.closeOnFocusOut !== false;
    const restoreFocus = options.restoreFocus ?? 'none';
    const floating = floatingElement();
    const floatingFocusElement = getFloatingFocusElement(floating);
    const reference = domReferenceElement();
    const portal = portalNode(floating);
    const nextFocusable = nextFocusableElement(reference);
    const triggerGuard = triggerBeforeGuard(reference);

    // Cleanups first, in declaration order, then setups: a teardown and re-arm in one sync is one commit.
    runEffect('initialFocus', open && !disabled && isHTMLElement(floatingFocusElement), [floatingFocusElement], () =>
      initialFocusEffect(floatingFocusElement!),
    );
    runEffect('returnFocus', !disabled && !!floatingFocusElement, [floating, floatingFocusElement, reference], () =>
      returnFocusEffect(floating, floatingFocusElement!, reference),
    );
    runEffect('webkitBlur', !disposed && !removed && platform.engine.webkit && !open && !!floating, [floating], () => webkitBlurEffect(floating!));
    const state = portalState(portal);
    runEffect(
      'publication',
      !disabled && state !== null,
      [state, modal, open, closeOnFocusOut, reference, beforeGuard(), afterGuard()],
      () => publicationEffect(state!, modal, closeOnFocusOut, reference),
    );
    runEffect('tabIndex', !disabled && !!floatingFocusElement, [floatingFocusElement], () =>
      tabIndexEffect(floatingFocusElement!),
    );
    runEffect('trap', !disabled && modal, [floatingFocusElement], () => trapEffect(floatingFocusElement));
    runEffect('recording', !disabled && open, [floating, reference, floatingFocusElement, portal], () =>
      recordingEffect(floating, reference, portal),
    );
    runEffect(
      'focusOut',
      !disabled && closeOnFocusOut && (!!floating || !!reference),
      [floating, floatingFocusElement, reference, portal, modal, restoreFocus, nextFocusable, triggerGuard],
      () => focusOutEffect(floating, floatingFocusElement, reference, portal, modal, restoreFocus),
    );
    runEffect(
      'marking',
      !disabled && !!floating && open,
      [floating, reference, portal, modal, nextFocusable, triggerGuard, beforeGuard(), afterGuard()],
      () => markingEffect(floating!, reference, portal, modal),
    );
    syncGuards(disabled);
  };

  // The inside focus guards (FLT-DOM-15, KEY-05, KEY-06), :1003-1054. C# renders them; the module listens on them and
  // writes the VoiceOver role once (FLT-DOM-13).
  const syncGuards = (disabled: boolean): void => {
    const guards = disabled ? [] : [beforeGuard(), afterGuard()].filter((x): x is HTMLElement => x != null);
    if (guards.length === guardListeners.length && guards.every((guard, index) => guardListeners[index]?.element === guard)) {
      return;
    }
    for (const { element, listener } of guardListeners) {
      element.removeEventListener('focusin', listener);
    }
    guardListeners = guards.map((element) => {
      const before = element === beforeGuard();
      const listener = (event: FocusEvent) => onGuardFocus(before, event);
      // React's `onFocus` is `focusin`, so the portal's capture listeners on its node run first (FLT-FOC-17).
      element.addEventListener('focusin', listener);
      if (!swappedGuards.has(element) && platform.screenReader.voiceOver && platform.engine.webkit) {
        swappedGuards.add(element);
        element.setAttribute('role', 'button');
        element.removeAttribute('aria-hidden');
      }
      return { element, listener };
    });
  };

  const onGuardFocus = (before: boolean, event: FocusEvent): void => {
    const floatingFocusElement = getFloatingFocusElement(floatingElement());
    if (options.modal !== false) {
      const els = getTabbableContent(floatingFocusElement);
      enqueueFocus(before ? els[els.length - 1] : els[0]);
      return;
    }
    const portal = portalNode(floatingElement());
    if (!portal) {
      return;
    }
    const reference = domReferenceElement();
    const [beforeOutside, afterOutside] = outsideGuards(portal);
    if (before) {
      preventReturnFocus = false;
      if (isOutsideEvent(event, portal)) {
        getNextTabbable(reference)?.focus();
      } else {
        (previousFocusableElement(reference) ?? beforeOutside)?.focus();
      }
    } else {
      if (options.closeOnFocusOut !== false) {
        preventReturnFocus = true;
      }
      if (isOutsideEvent(event, portal)) {
        getPreviousTabbable(reference)?.focus();
      } else {
        (nextFocusableElement(reference) ?? afterOutside)?.focus();
      }
    }
  };

  // The relay (ADR 04 Amendments 2026-10-27): the internal `openchange` runs once per committed change.
  const applyRelay = (): void => {
    const relay = options.relay;
    if (!relay || relay.revision === relayRevision) {
      return;
    }
    relayRevision = relay.revision;
    onOpenChangeLocal?.({
      reason: relay.reason,
      nativeEvent: relay.eventType ? { type: relay.eventType, pointerType: relay.pointerType ?? null } : undefined,
      nested: false,
      triggerElement: resolveId(relay.triggerId) ?? undefined,
    });
  };

  const stopObserving = observeOpenInput(popup, openSelectorOf(options), (matches) => {
    if (disposed || matches === open) {
      return;
    }
    open = matches;
    if (open) {
      closeRequested = false;
    }
    sync();
  });

  sync();

  const handle: TwinHandle = {
    update(nextOptionsJson: string, nextReferences?: TwinReferences) {
      if (disposed) {
        return;
      }
      options = parseOptions<FocusManagerOptions>(nextOptionsJson);
      optionReferences = nextReferences;
      // A request C# answered, canceled or not, re-arms the focus-out close (ADR 04 Amendments 2026-10-27).
      if ((options.answered ?? 0) !== answered) {
        answered = options.answered ?? 0;
        closeRequested = false;
      }
      applyRelay();
      // The batch decides open and closed after attach; `update` never changes it.
      sync();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      stopObserving();
      sync();
      for (const { element, listener } of guardListeners) {
        element.removeEventListener('focusin', listener);
      }
      guardListeners = [];
      clearTimeout(pointerDownTimeout);
      clearTimeout(blurTimeout);
      cancelFrame(restoreFocusFrame);
      delete popup[attachedHandle];
    },
  };
  popup[attachedHandle] = handle;
  return handle;
}
