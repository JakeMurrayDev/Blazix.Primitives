// The `portal` mechanism module (ADR 04 §1 and §5; ADR 04 Amendments 2026-10-27, decision 5, chosen by the user
// 2026-10-27): the DOM half of Base UI's FloatingPortal and FloatingPortalLite (floating-ui-react/components/
// FloatingPortal.tsx, utils/FloatingPortalLite.tsx at v1.9.0). Requirements: FLT-API-13, FLT-DOM-11..14, FLT-ARIA-04/05,
// FLT-FOC-18 (the portal's side), PSH-STR-11/12; the outside guards' routing (FLT-KEY-07, FLT-FOC-17, FLT-DOM-18) imports
// `focus-manager`'s tabbable exports and is wired by track 4 after this module lands (design point 5, settled by the
// orchestrator 2026-10-27).
// - C# renders the portal node `<div id data-base-ui-portal>` with the consumer's attributes as the only child of an
//   in-place wrapper (`display: contents`, the pipeline's opt-in host wrapper, ADR 02 Amendments 2026-10-27), hidden
//   (`display: none`) until moved; the outside guards and the `aria-owns` owner are the wrapper's siblings. The handle
//   attaches to the node, moves it into the resolved container and owns its `style` from then on (the handover of ADR 04
//   Amendments 2026-10-18). Blazor keeps editing the wrapper's siblings in place and the node's children wherever it is,
//   so its `insertBefore` and `removeChild` never meet a foreign parent (verification item 3, issue 8).
// - The container: the reference the options name (an element or a node a script holds, through the references array of
//   `attach`/`update`), else the first match of a selector in the wrapper's document, else that document's body; C# sends
//   the enclosing portal's node as the reference when the consumer gives none. A selector that matches nothing leaves the
//   node hidden in place until an update resolves one.
// - When Blazor removes the wrapper (the Portal left the render tree), the node goes with it at once: a `childList`
//   observer on the wrapper's parent sees the wrapper disconnected, so no node is left behind before `dispose` arrives.
// - The handle is published on the node under `Symbol.for('blazix.portal')` (settled by the orchestrator 2026-10-27 with
//   track 4's design point 3, so the two modules import nothing from each other): the node, the wrapper, the outside
//   guards found by position, and the slot the focus manager publishes its state in (track 4; FLT-FOC-18).
// - Under VoiceOver on WebKit each outside guard becomes `role="button"` without `aria-hidden` (FLT-DOM-13, FLT-ARIA-05),
//   a JavaScript write after attach, applied to the guards C# renders later too.
import { ownerDocument } from './dom';
import { platform } from './platform';
import { applyConsumerStyle, parseOptions, resolveElement, resolveReference, type DotNetTwin, type TwinHandle, type TwinOptions, type TwinReferences } from './twin';

/** The options of a `portal` handle; mirrors `PortalOptions` in C#. */
export interface PortalOptions extends TwinOptions {
  /** The index of the container in the references: an element or a node a script holds. */
  containerRef?: number | null;
  /** A CSS selector of the container, matched in the wrapper's document. */
  containerSelector?: string | null;
  /** The lite portal: no outside guards and no owner (PSH-STR-11/12). */
  lite?: boolean;
}

/** The state a focus manager inside the portal publishes to it (FLT-FOC-18; written by track 4). */
export interface PortalFocusPublication {
  modal: boolean;
  closeOnFocusOut: boolean;
  open: boolean;
  domReference: Element | null;
  insideGuards?: { before: HTMLElement | null; after: HTMLElement | null };
  close?(event: Event): void;
}

/** What the portal publishes on its node, for the focus manager and the `popup` module. */
export interface PortalState {
  readonly node: HTMLElement;
  readonly wrapper: HTMLElement | null;
  readonly lite: boolean;
  /** The outside guards beside the wrapper, found by position; `null` while C# renders none. */
  outsideGuards(): { before: HTMLElement | null; after: HTMLElement | null };
  /** The focus manager's publication, or `null` while none is published. */
  focusManager: PortalFocusPublication | null;
}

/** The key the portal's state is published under on its node. */
export const PORTAL_STATE = Symbol.for('blazix.portal');

const FOCUS_GUARD_ATTRIBUTE = 'data-base-ui-focus-guard';

interface PortalNode extends HTMLElement {
  [PORTAL_STATE]?: PortalState;
}

const attachedHandle = Symbol('blazix.portal.handle');

interface AttachedNode extends PortalNode {
  [attachedHandle]?: TwinHandle;
}

/**
 * Returns the state the portal published on a portal node.
 * @param node The portal node, or an element inside it (the closest `[data-base-ui-portal]` is used).
 * @returns The state, or `null` when no portal handle is attached there.
 */
export function getPortalState(node: Element | null): PortalState | null {
  const portal = node?.closest('[data-base-ui-portal]') as PortalNode | null | undefined;
  return portal?.[PORTAL_STATE] ?? null;
}

function isOutsideGuard(element: Element | null): element is HTMLElement {
  return element instanceof HTMLElement && element.hasAttribute(FOCUS_GUARD_ATTRIBUTE) && element.getAttribute('data-type') === 'outside';
}

// The wrapper's siblings by position: before it the guard, then the owner; after it the guard (FLT-DOM-14, FLT-ARIA-04).
function findOutsideGuards(wrapper: HTMLElement | null): { before: HTMLElement | null; after: HTMLElement | null } {
  if (wrapper === null) {
    return { before: null, after: null };
  }

  let before = wrapper.previousElementSibling;
  if (before !== null && before.hasAttribute('aria-owns')) {
    before = before.previousElementSibling;
  }

  const after = wrapper.nextElementSibling;
  return { before: isOutsideGuard(before) ? before : null, after: isOutsideGuard(after) ? after : null };
}

// FLT-DOM-13: VoiceOver's virtual cursor fires focus only on focusable or role=button elements through WebKit.
function swapGuardForVoiceOver(guard: HTMLElement | null): void {
  if (guard !== null && guard.getAttribute('role') !== 'button') {
    guard.setAttribute('role', 'button');
    guard.removeAttribute('aria-hidden');
  }
}

/**
 * Resolves a portal's container (FLT-API-13): the referenced node, else the first match of the selector in the wrapper's
 * document, else that document's body.
 * @param options The handle's options.
 * @param references The references received with the options.
 * @param wrapper The node's in-place wrapper, whose document a selector and the default resolve in.
 * @returns The container, or `null` when a selector matches nothing.
 */
export function resolveContainer(options: PortalOptions, references: TwinReferences, wrapper: Element): Node | null {
  const referenced = resolveReference<Node>(references, options.containerRef);
  if (referenced !== null) {
    return referenced;
  }

  const doc = ownerDocument(wrapper);
  if (options.containerSelector) {
    return doc.querySelector(options.containerSelector);
  }

  return doc.body;
}

/**
 * Attaches the portal to its node: moves the node out of its in-place wrapper into the resolved container, applies the
 * consumer's style, and keeps both until `update` changes them or `dispose` restores the node.
 * @param elementOrId The portal node, or its id.
 * @param optionsJson The `PortalOptions` JSON.
 * @param _twinReference The twin; the portal reports nothing to it yet.
 * @param references The container's reference, when the options name one.
 * @returns The handle with `update(optionsJson, references)` and `dispose()`; the live handle when the node is already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, _twinReference: DotNetTwin, references?: TwinReferences): TwinHandle {
  const node: AttachedNode = resolveElement(elementOrId);
  const existing = node[attachedHandle];
  if (existing) {
    return existing;
  }

  let options = parseOptions<PortalOptions>(optionsJson);
  let currentReferences = references;
  const wrapper = node.parentElement;
  const voiceOver = platform.screenReader.voiceOver && platform.engine.webkit;
  let disposed = false;

  const state: PortalState = {
    node,
    wrapper,
    get lite() {
      return options.lite === true;
    },
    outsideGuards: () => findOutsideGuards(wrapper),
    focusManager: null,
  };
  node[PORTAL_STATE] = state;

  // The node is visible only in a container; in place (a selector that matched nothing) it stays hidden.
  const place = () => {
    const container = wrapper === null ? null : resolveContainer(options, currentReferences, wrapper);
    if (container !== null && node.parentNode !== container) {
      container.appendChild(node);
    } else if (container === null && wrapper !== null && node.parentNode !== wrapper) {
      wrapper.appendChild(node);
    }

    applyConsumerStyle(node, options);
    if (container === null) {
      node.style.display = 'none';
    }
  };

  const swapGuards = () => {
    if (voiceOver && !options.lite) {
      const guards = findOutsideGuards(wrapper);
      swapGuardForVoiceOver(guards.before);
      swapGuardForVoiceOver(guards.after);
    }
  };

  const release = () => {
    disposed = true;
    observer?.disconnect();
    delete node[attachedHandle];
    if (node[PORTAL_STATE] === state) {
      delete node[PORTAL_STATE];
    }
  };

  // The wrapper's parent sees the guards C# renders later (for the VoiceOver swap) and the wrapper's own removal.
  const parent = wrapper?.parentNode ?? null;
  const observer =
    parent === null
      ? null
      : new MutationObserver(() => {
          if (disposed) {
            return;
          }
          if (wrapper !== null && !wrapper.isConnected) {
            // Blazor removed the Portal: its node, wherever it was moved, leaves with it.
            release();
            node.remove();
            return;
          }
          swapGuards();
        });
  observer?.observe(parent!, { childList: true });

  place();
  swapGuards();

  const handle: TwinHandle = {
    update(nextOptionsJson: string, nextReferences?: TwinReferences) {
      if (disposed) {
        return;
      }
      options = parseOptions<PortalOptions>(nextOptionsJson);
      currentReferences = nextReferences;
      place();
      swapGuards();
    },
    dispose() {
      if (disposed) {
        return;
      }
      release();
      // Back in place before Blazor removes the wrapper, or gone with a wrapper Blazor already removed.
      if (wrapper !== null && wrapper.isConnected) {
        if (node.parentNode !== wrapper) {
          wrapper.appendChild(node);
        }
        node.style.display = 'none';
      } else {
        node.remove();
      }
    },
  };
  node[attachedHandle] = handle;
  return handle;
}
