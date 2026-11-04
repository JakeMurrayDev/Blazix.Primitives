// The menu-tree harness of the `composite` nested-list L3 tests (ADR 06 §1; M5 track 1, issue 159, Deliverables 6 and 7):
// it plays C#'s part for the menus of Base UI's `test/floating-ui-tests/Menu.tsx` and `MenuOrientation.tsx`. Each menu
// has its own interactions session, linked to its parent menu's by a test-only parent token (until the family-agnostic
// cascade of ADR 04 Amendments 2026-11-03 (5) is on `main`), its trigger is a button (the root's) or an item of the parent
// menu's popup, and its popup is portaled: a sibling under the container, outside the parent popup's DOM. A menu's
// trigger carries the list's reference handle and an interactions trigger; its popup, while mounted (open, or kept
// mounted with `visibility: hidden`), carries the floating handle; nested menus mount with their parent's popup. A list
// open request is committed (the popup's style first, as C#'s render patches the DOM before the session's `update`), a
// menu that opens closes its open siblings (the fixture's `menuopen` tree event), and a root menu's popup takes the focus
// when it opens (the focus manager's initial focus, `initialFocus={!isNested}`). In its WebAssembly form
// (`synchronous: true`) it answers `Ping` and `Decide` (`navigate` with `run`, `leave` by closing inside the dispatch).
// Test-only: no module entry imports it, so esbuild never bundles it.
import {
  attach,
  LIST_HIGHLIGHT_REPORT,
  LIST_OPEN_REQUEST_REPORT,
  type CompositeListHighlightReport,
  type CompositeListOpenRequestReport,
  type CompositeListOptions,
  type CompositeOptions,
  type Orientation,
} from '../composite';
import type { DotNetTwin, TwinHandle } from '../twin';
import { createPopupHarness, type PopupHarness } from './interactions-harness';

/** A menu item: a label, and whether it is disabled. */
export interface MenuItemSpec {
  label: string;
  disabled?: boolean;
}

/** A menu: its trigger's label, its items and nested menus, and the fixture's props. */
export interface MenuSpec {
  label: string;
  items: Array<MenuItemSpec | MenuSpec>;
  keepMounted?: boolean;
  orientation?: Orientation;
  grid?: boolean;
  /** List options beyond the fixture's. */
  list?: Partial<CompositeListOptions>;
}

export interface MenuTreeOptions {
  /** `true` behaves like WebAssembly (synchronous invoke and the deciders); `false`, the default, like Blazor Server. */
  synchronous?: boolean;
  /** An existing element to use as the root menu's trigger (a menubar's item) instead of a new button. */
  rootTrigger?: HTMLElement;
  /** The references every floating handle receives, by the indices its list options name. */
  references?: unknown[];
  /** Where the triggers and popups go; the document's body by default. */
  container?: HTMLElement;
}

/** One menu as C# holds it. */
export interface MenuNode {
  readonly spec: MenuSpec;
  readonly parent: MenuNode | null;
  readonly popup: PopupHarness;
  /** The trigger element while the menu is mounted. */
  readonly trigger: HTMLElement | null;
  /** The popup element while it is mounted. */
  readonly floating: HTMLElement | null;
  /** C#'s mirror of the list's highlight. */
  readonly activeIndex: number | null;
  /** The list's open and close requests. */
  readonly openRequests: CompositeListOpenRequestReport[];
}

export interface MenuTree {
  readonly root: MenuNode;
  /** The menu whose trigger has a label. */
  menu(label: string): MenuNode;
  /** `screen.getByText(label)`: the mounted item or trigger with that label. */
  byText(label: string): HTMLElement;
  /** Disposes every handle and removes the elements. */
  dispose(): void;
}

function isMenuSpec(item: MenuItemSpec | MenuSpec): item is MenuSpec {
  return 'items' in item;
}

interface NodeState extends MenuNode {
  readonly parent: NodeState | null;
  trigger: HTMLElement | null;
  floating: HTMLElement | null;
  activeIndex: number | null;
  children: NodeState[];
  referenceHandle: TwinHandle | null;
  floatingHandle: TwinHandle | null;
}

/**
 * Creates the harness of a menu tree.
 * @param spec The root menu.
 * @param options The mode, the root trigger and the references.
 * @returns The harness.
 */
export function createMenuTree(spec: MenuSpec, options: MenuTreeOptions = {}): MenuTree {
  const container = options.container ?? document.body;
  const nodes: NodeState[] = [];

  const createNode = (menuSpec: MenuSpec, parent: NodeState | null): NodeState => {
    // A commit the session's own handles started (the root trigger's press) renders after the store's write.
    const popup = createPopupHarness({
      synchronous: options.synchronous ?? false,
      onOpenChange: () => queueMicrotask(() => render(node)),
    });
    const node: NodeState = {
      spec: menuSpec,
      parent,
      popup,
      trigger: null,
      floating: null,
      activeIndex: null,
      openRequests: [],
      children: [],
      referenceHandle: null,
      floatingHandle: null,
    };
    popup.attachSession({ session: { parentToken: parent?.popup.token ?? null } });
    node.children = menuSpec.items.filter(isMenuSpec).map((child) => createNode(child, node));
    nodes.push(node);
    return node;
  };

  const listOptions = (node: NodeState): CompositeListOptions => {
    const { spec: menuSpec } = node;
    return {
      ...(node.parent ? { nested: true } : {}),
      orientation: menuSpec.orientation ?? (menuSpec.grid ? 'both' : 'vertical'),
      ...(menuSpec.grid ? { grid: true } : {}),
      ...menuSpec.list,
    };
  };

  const common = (node: NodeState): CompositeOptions =>
    ({
      orientation: 'both',
      loopFocus: true,
      enableHomeAndEndKeys: false,
      stopEventPropagation: true,
      highlightItemOnHover: false,
      modifierKeys: [],
      direction: 'ltr',
      items: [],
      selectOnNavigation: false,
      reportFocusLeave: false,
      reportHighlight: false,
      sessionToken: node.popup.token,
      list: listOptions(node),
    }) as CompositeOptions;

  // The fixture's popup style: hidden while kept mounted and closed.
  const applyVisibility = (node: NodeState, open: boolean): void => {
    if (node.floating && node.spec.keepMounted) {
      node.floating.style.visibility = open ? 'visible' : 'hidden';
      node.floating.setAttribute('aria-hidden', String(!open));
    }
  };

  const twinFor = (node: NodeState): DotNetTwin => ({
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      if (!options.synchronous) {
        throw new Error('The current dispatcher does not support synchronous calls from JS to .NET.');
      }
      if (methodName === 'Ping') {
        return true as T;
      }
      if (methodName === 'Decide') {
        if (args[0] === 'leave') {
          requestOpen(node, false, 'focus-out', null);
          return 'run' as T;
        }
        return 'run' as T;
      }
      throw new Error(`Unexpected synchronous call to ${methodName}`);
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      const payload = JSON.parse(args[0] as string) as Record<string, unknown>;
      if (methodName === LIST_HIGHLIGHT_REPORT) {
        node.activeIndex = (payload as unknown as CompositeListHighlightReport).index;
      } else if (methodName === LIST_OPEN_REQUEST_REPORT) {
        const request = payload as unknown as CompositeListOpenRequestReport;
        node.openRequests.push(request);
        requestOpen(node, request.open, request.reason, request.triggerId);
      }
      return Promise.resolve(undefined as T);
    },
  });

  // C#'s commit of a list request: the render patches the popup's style, the session's `update` relays the open, and a
  // menu that opens closes its open siblings.
  const requestOpen = (node: NodeState, open: boolean, reason: string, triggerId: string | null): void => {
    applyVisibility(node, open);
    node.popup.setOpen(open, reason, triggerId);
    render(node);
    if (open) {
      for (const sibling of node.parent?.children ?? []) {
        if (sibling !== node && sibling.popup.open) {
          requestOpen(sibling, false, 'none', null);
        }
      }
    }
  };

  const itemElement = (node: NodeState, item: MenuItemSpec | MenuSpec, index: number): HTMLElement => {
    const element = document.createElement('button');
    element.type = 'button';
    element.id = `${node.popup.token}-item-${index}`;
    element.setAttribute('tabindex', '-1');
    element.textContent = item.label;
    if (isMenuSpec(item)) {
      element.setAttribute('aria-haspopup', 'menu');
    } else {
      element.setAttribute('role', 'menuitem');
      if (item.disabled) {
        element.disabled = true;
      }
    }
    return element;
  };

  const mountPopup = (node: NodeState): void => {
    const floating = document.createElement('div');
    floating.id = `${node.popup.token}-floating`;
    floating.setAttribute('role', 'menu');
    floating.setAttribute('tabindex', '-1');
    const elements = node.spec.items.map((item, index) => itemElement(node, item, index));
    if (node.spec.grid) {
      // renderGridRows.tsx: rows of GRID_COLUMN_COUNT (2), `display: contents`.
      for (let start = 0; start < elements.length; start += 2) {
        const row = document.createElement('div');
        row.setAttribute('role', 'row');
        row.style.display = 'contents';
        row.append(...elements.slice(start, start + 2));
        floating.appendChild(row);
      }
    } else {
      floating.append(...elements);
    }
    container.appendChild(floating);
    node.floating = floating;
    applyVisibility(node, node.popup.open);
    node.popup.attachFloating(floating);
    node.floatingHandle = attach(
      floating,
      JSON.stringify({ ...common(node), floating: {}, items: elements.map((element) => ({ id: element.id, tabIndex: null })) }),
      twinFor(node),
      options.references,
    );
    // The nested menus mount with this popup, each on its item.
    let childIndex = 0;
    node.spec.items.forEach((item, index) => {
      if (isMenuSpec(item)) {
        mountMenu(node.children[childIndex]!, elements[index]!);
        childIndex += 1;
      }
    });
    // FloatingFocusManager's initial focus for a root menu.
    if (!node.parent && node.popup.open) {
      floating.focus();
    }
  };

  const unmountPopup = (node: NodeState): void => {
    for (const child of node.children) {
      unmountMenu(child);
    }
    node.floatingHandle?.dispose();
    node.floatingHandle = null;
    node.floating?.remove();
    node.floating = null;
  };

  // The popup renders while open or kept mounted.
  const render = (node: NodeState): void => {
    if (!node.trigger) {
      return;
    }
    const shouldMount = node.popup.open || node.spec.keepMounted === true;
    if (shouldMount && !node.floating) {
      mountPopup(node);
    } else if (!shouldMount && node.floating) {
      unmountPopup(node);
    } else {
      applyVisibility(node, node.popup.open);
    }
  };

  const mountMenu = (node: NodeState, trigger: HTMLElement): void => {
    node.trigger = trigger;
    node.popup.attachTrigger(trigger, { trigger: node.parent ? {} : { click: {} } });
    node.popup.setReference(trigger);
    node.referenceHandle = attach(trigger, JSON.stringify({ ...common(node), reference: {} }), twinFor(node));
    render(node);
  };

  const unmountMenu = (node: NodeState): void => {
    if (node.popup.open) {
      node.popup.setOpen(false);
    }
    unmountPopup(node);
    node.referenceHandle?.dispose();
    node.referenceHandle = null;
    node.trigger = null;
  };

  const root = createNode(spec, null);
  const rootTrigger = options.rootTrigger ?? document.createElement('button');
  if (!options.rootTrigger) {
    rootTrigger.setAttribute('type', 'button');
    rootTrigger.textContent = spec.label;
    container.appendChild(rootTrigger);
  }
  mountMenu(root, rootTrigger);

  return {
    root,
    menu(label) {
      const found = nodes.find((node) => node.spec.label === label);
      if (!found) {
        throw new Error(`no menu ${label}`);
      }
      return found;
    },
    byText(label) {
      const found = Array.from(container.querySelectorAll<HTMLElement>('button')).find((element) => element.textContent === label);
      if (!found) {
        throw new Error(`no element with the text ${label}`);
      }
      return found;
    },
    dispose() {
      unmountMenu(root);
      for (const node of nodes) {
        node.popup.dispose();
      }
      if (!options.rootTrigger) {
        rootTrigger.remove();
      }
    },
  };
}
