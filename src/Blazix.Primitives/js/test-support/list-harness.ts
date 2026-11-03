// The list harness of the `composite` list-navigation L3 tests (ADR 06 §1; M5 track 1, issue 159): it plays C#'s part
// for one popup list over the interactions session harness. The reference (a button, or an input under virtual focus)
// carries an interactions trigger and a list reference handle; a list open request is committed through the session
// harness, and the commit renders the popup as C# would (the floating element and its items, `tabindex="-1"` under the
// handover) and attaches the floating list handle, as `OnAfterRender` does; a close removes the popup and disposes the
// handle. The highlight reports are mirrored as C#'s `Highlighted` mirror would be. In its WebAssembly form
// (`synchronous: true`) it answers `Ping` and `Decide`: `navigate` with the verdict a test chooses, `leave` by running the
// close inside the dispatch. Test-only: no module entry imports it, so esbuild never bundles it.
import {
  attach,
  LIST_HIGHLIGHT_REPORT,
  LIST_MATCH_REPORT,
  LIST_OPEN_REQUEST_REPORT,
  type CompositeItemRegistration,
  type CompositeListHighlightReport,
  type CompositeListMatchReport,
  type CompositeListOpenRequestReport,
  type CompositeListOptions,
  type CompositeOptions,
} from '../composite';
import type { EventInfo } from '../event-info';
import type { DecisionPolicy, DotNetTwin, TwinHandle } from '../twin';
import { createPopupHarness, type HarnessOptions, type PopupHarness } from './interactions-harness';

/** One report a list handle sent, as C# received it. */
export interface ListReport {
  method: string;
  payload: Record<string, unknown>;
}

export interface ListHarnessOptions {
  /** The list options every handle carries. */
  list?: CompositeListOptions;
  /** The items' labels, rendered as each item's text; `['one', 'two', 'three']` by default. */
  items?: string[];
  /** Extra attributes of an item. */
  itemAttributes?: (index: number, label: string) => Record<string, string>;
  /** The items' element; `li` by default. */
  itemTag?: string;
  /** Whether the items sit directly in the floating element instead of a `ul` (a grid fixture's buttons). */
  flat?: boolean;
  /** Groups the items, directly in the floating element, into `role="row"` elements of this many items each. */
  rowSize?: number;
  /** The registration fields C# sends for an item beyond its id. */
  itemRegistration?: (index: number, label: string) => Partial<CompositeItemRegistration>;
  /** The reference element: a button (the default) or an input. */
  reference?: 'button' | 'input';
  /** The floating element's role; `menu` by default. */
  floatingRole?: string;
  /** Whether the popup stays mounted while closed, as Select's does. */
  keepMounted?: boolean;
  /** `true` behaves like WebAssembly (synchronous invoke and the deciders); `false`, the default, like Blazor Server. */
  synchronous?: boolean;
  /** The verdict `navigate` returns on WebAssembly; `run` by default. */
  navigateVerdict?: DecisionPolicy | ((event: EventInfo) => DecisionPolicy);
  /** Whether C# reports the items' order. */
  reportItems?: boolean;
  /** The session harness's options (the consumer's `OnOpenChange`, the initial open state). */
  session?: HarnessOptions;
  /** Where the elements go; the document's body by default. */
  container?: HTMLElement;
}

export interface ListHarness {
  readonly popup: PopupHarness;
  readonly reference: HTMLElement;
  /** The floating element while it is mounted. */
  readonly floating: HTMLElement | null;
  /** Every report a list handle sent, in order. */
  readonly reports: ListReport[];
  /** Every `Decide` call a list handle made. */
  readonly decisions: { point: string; event: EventInfo }[];
  /** C#'s mirror of the highlight, from the reports. */
  readonly activeIndex: number | null;
  /** The highlight reports. */
  readonly highlights: CompositeListHighlightReport[];
  /** The open and close requests. */
  readonly openRequests: CompositeListOpenRequestReport[];
  /** The typeahead matches on a closed list. */
  readonly matches: CompositeListMatchReport[];
  /** The item element at an index of the rendered items. */
  item(index: number): HTMLElement;
  /** Sends every handle an `update` whose list options merge `list` (a re-render of the Root). */
  update(list: Partial<CompositeListOptions>): void;
  /** Commits an open change C# started. */
  setOpen(open: boolean, reason?: string): void;
  /** Re-renders the items with new labels, sending the floating handle's registrations. */
  setItems(items: string[]): void;
  /** Disposes the handles and removes the elements. */
  dispose(): void;
}

/**
 * Creates the harness of one popup list.
 * @param options The list options, the items and the mode.
 * @returns The harness.
 */
export function createListHarness(options: ListHarnessOptions = {}): ListHarness {
  // A commit from any handle (the trigger's press included) renders the popup after the store's write.
  let renderAfterCommit: () => void = () => {};
  const popup = createPopupHarness({
    synchronous: options.synchronous ?? false,
    ...options.session,
    onOpenChange: (open, details) => {
      options.session?.onOpenChange?.(open, details);
      queueMicrotask(() => renderAfterCommit());
    },
  });
  const container = options.container ?? document.body;
  const reports: ListReport[] = [];
  const decisions: { point: string; event: EventInfo }[] = [];
  let list: CompositeListOptions = { ...options.list };
  let labels = options.items ?? ['one', 'two', 'three'];
  let activeIndex: number | null = null;
  let floating: HTMLElement | null = null;
  let floatingHandle: TwinHandle | null = null;
  let mounted = false;

  const reference = document.createElement(options.reference ?? 'button');
  reference.id = `${popup.token}-reference`;
  if (options.reference === 'input') {
    reference.setAttribute('role', 'combobox');
  }
  container.appendChild(reference);

  const commonOptions = (): CompositeOptions =>
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
      sessionToken: popup.token,
      list,
    }) as CompositeOptions;

  const registrations = (): CompositeItemRegistration[] =>
    floating === null
      ? []
      : Array.from(floating.querySelectorAll<HTMLElement>('[data-list-item]'), (element, index) => ({
          id: element.id,
          tabIndex: null,
          ...options.itemRegistration?.(index, labels[index]!),
        }));

  const floatingOptions = (): string =>
    JSON.stringify({ ...commonOptions(), floating: {}, items: registrations(), ...(options.reportItems ? { reportItems: true } : {}) });

  const referenceOptions = (): string => JSON.stringify({ ...commonOptions(), reference: {} });

  const record = (method: string, payload: Record<string, unknown>): void => {
    reports.push({ method, payload });
    if (method === LIST_HIGHLIGHT_REPORT) {
      activeIndex = (payload as unknown as CompositeListHighlightReport).index;
    } else if (method === LIST_OPEN_REQUEST_REPORT) {
      const request = payload as unknown as CompositeListOpenRequestReport;
      popup.setOpen(request.open, request.reason, request.triggerId);
      render();
    }
  };

  const twin: DotNetTwin = {
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      if (!options.synchronous) {
        throw new Error('The current dispatcher does not support synchronous calls from JS to .NET.');
      }
      if (methodName === 'Ping') {
        return true as T;
      }
      if (methodName === 'Decide') {
        const point = args[0] as string;
        const event = JSON.parse(args[1] as string) as EventInfo;
        decisions.push({ point, event });
        if (point === 'leave') {
          popup.setOpen(false, 'focus-out');
          const change = popup.changes.at(-1);
          render();
          return (change?.details.isCanceled ? 'stop' : 'run') as T;
        }
        if (point === 'navigate') {
          const verdict = options.navigateVerdict;
          return (typeof verdict === 'function' ? verdict(event) : (verdict ?? 'run')) as T;
        }
        return 'run' as T;
      }
      throw new Error(`Unexpected synchronous call to ${methodName}`);
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      record(methodName, JSON.parse(args[0] as string) as Record<string, unknown>);
      return Promise.resolve(undefined as T);
    },
  };

  const renderItems = (target: HTMLElement): void => {
    const elements = labels.map((label, index) => {
      const li = document.createElement(options.itemTag ?? 'li');
      li.id = `${popup.token}-item-${index}`;
      li.setAttribute('data-testid', `item-${index}`);
      li.setAttribute('data-list-item', '');
      li.setAttribute('tabindex', '-1');
      li.textContent = label;
      for (const [name, value] of Object.entries(options.itemAttributes?.(index, label) ?? {})) {
        li.setAttribute(name, value);
      }
      return li;
    });
    const rowSize = options.rowSize;
    if (rowSize) {
      const rows: HTMLElement[] = [];
      for (let start = 0; start < elements.length; start += rowSize) {
        const row = document.createElement('div');
        row.setAttribute('role', 'row');
        row.append(...elements.slice(start, start + rowSize));
        rows.push(row);
      }
      target.replaceChildren(...rows);
    } else if (options.flat) {
      target.replaceChildren(...elements);
    } else {
      const ul = document.createElement('ul');
      ul.append(...elements);
      target.replaceChildren(ul);
    }
  };

  // C#'s render after a commit: the popup mounts with the open, unmounts with the close (unless kept mounted).
  const render = (): void => {
    const shouldMount = popup.open || options.keepMounted === true;
    if (shouldMount && !mounted) {
      mounted = true;
      floating = document.createElement('div');
      floating.id = `${popup.token}-floating`;
      floating.setAttribute('role', options.floatingRole ?? 'menu');
      floating.setAttribute('tabindex', '-1');
      renderItems(floating);
      container.appendChild(floating);
      popup.attachFloating(floating);
      floatingHandle = attach(floating, floatingOptions(), twin);
    } else if (!shouldMount && mounted) {
      mounted = false;
      floatingHandle?.dispose();
      floatingHandle = null;
      floating?.remove();
      floating = null;
    }
  };

  renderAfterCommit = render;
  popup.attachSession();
  popup.attachTrigger(reference, { trigger: { click: {} } });
  popup.setReference(reference);
  const referenceHandle = attach(reference, referenceOptions(), twin);
  render();

  return {
    popup,
    reference,
    get floating() {
      return floating;
    },
    reports,
    decisions,
    get activeIndex() {
      return activeIndex;
    },
    get highlights() {
      return reports.filter((report) => report.method === LIST_HIGHLIGHT_REPORT).map((report) => report.payload as unknown as CompositeListHighlightReport);
    },
    get openRequests() {
      return reports.filter((report) => report.method === LIST_OPEN_REQUEST_REPORT).map((report) => report.payload as unknown as CompositeListOpenRequestReport);
    },
    get matches() {
      return reports.filter((report) => report.method === LIST_MATCH_REPORT).map((report) => report.payload as unknown as CompositeListMatchReport);
    },
    item(index) {
      const found = floating?.querySelector<HTMLElement>(`[data-testid="item-${index}"]`);
      if (!found) {
        throw new Error(`no item ${index}`);
      }
      return found;
    },
    update(next) {
      list = { ...list, ...next };
      referenceHandle.update(referenceOptions());
      floatingHandle?.update(floatingOptions());
    },
    setOpen(open, reason = 'none') {
      popup.setOpen(open, reason);
      render();
    },
    setItems(items) {
      labels = items;
      if (floating) {
        renderItems(floating);
        floatingHandle?.update(floatingOptions());
      }
    },
    dispose() {
      floatingHandle?.dispose();
      referenceHandle.dispose();
      popup.dispose();
      floating?.remove();
      reference.remove();
    },
  };
}
