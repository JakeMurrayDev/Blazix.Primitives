// The `focus-manager` mechanism module (ADR 04 §1; Amendments 2026-10-27): tabbable discovery, the outside marking,
// the one queued focus per page and the focus manager a popup's twin attaches (initial focus, the modal trap, the
// focus-out close, restoration, return focus, the managed `tabindex` and the inside focus guards' routing).
// Transcribed from Base UI v1.9.0: packages/react/src/floating-ui-react/utils/{tabbable,markOthers,enqueueFocus}.ts,
// utils/element.ts (`getFloatingFocusElement`) and components/FloatingFocusManager.tsx.
// Requirements: floating-and-positioning.md §4.4 (FLT-DOM-15..18), §4.5 (FLT-ARIA-01..03), §4.6 (FLT-KEY-04..06,
// FLT-KEY-09), §4.8 (FLT-FOC-05..22), FLT-A11Y-04, FLT-A11Y-07, FLT-EDGE-05..08, FLT-EDGE-14, FLT-ALG-18, 19, 21,
// 22, 27 and FLT-API-05.
// Page-global state (sanctioned, ADR 04 Amendments 2026-10-27; js/CONTEXT.md): the outside marking's counters and the
// queued focus; `resetFocusManagerState` drops them for tests.
import { isElementVisible } from './composite';
import { activeElement, contains, isHTMLElement, isShadowRoot, ownerDocument } from './dom';
import { cancelFrame, requestFrame } from './transitions';

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
 * Focuses an element on the next animation frame, cancelling the focus any earlier call left pending.
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

/**
 * Drops the module's page-global state: every outside-marking lock's bookkeeping and the queued focus. For tests only,
 * like `resetFrameScheduler`: the state is shared by every handle on the page, so a lock or a pending focus one test
 * left behind would otherwise reach the next.
 */
export function resetFocusManagerState(): void {
  resetOutsideMarking();
  cancelFrame(queuedFocusFrame);
  queuedFocusFrame = 0;
}
