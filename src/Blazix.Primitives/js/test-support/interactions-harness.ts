// The session harness of the `interactions` L3 tests (ADR 06 §1): it plays C#'s part for one popup. Each handle gets its
// own fake twin; a reported request is committed as an uncontrolled popup store would commit it (the consumer's
// `onOpenChange` first, a cancel honoured, then the store write, then the relay in the session handle's `update`), so
// Base UI's assertions read the committed state. In its WebAssembly form (`synchronous: true`) it answers `Ping`, the
// `press` hand-over and `Decide` for `press` and `escape` inside the dispatch, as the twin's deciders do. Test-only: no
// module entry imports it, so esbuild never bundles it.
import { attach, type InteractionsOptions, type OpenChangeRelay } from '../interactions';
import type { EventInfo } from '../event-info';
import type { DecisionPolicy, DotNetTwin, TwinHandle } from '../twin';

/** The details C# hands the consumer's `OnOpenChange`, reduced to what the tests read and call. */
export interface HarnessChangeDetails {
  reason: string;
  event: EventInfo | null;
  triggerId: string | null;
  instant: boolean;
  isCanceled: boolean;
  isPropagationAllowed: boolean;
  cancel(): void;
  allowPropagation(): void;
}

/** One request a handle reported, as C# received it. */
export interface HarnessRequest {
  method: string;
  payload: Record<string, unknown>;
}

export interface HarnessOptions {
  /** The session token; a fresh one by default. */
  token?: string;
  /** `true` behaves like WebAssembly (synchronous invoke and the deciders); `false`, the default, like Blazor Server. */
  synchronous?: boolean;
  /** The initial open state. */
  open?: boolean;
  /** Whether a request is committed (an uncontrolled store); `false` records it only (a controlled store that ignores it). */
  commit?: boolean;
  /** The consumer's `OnOpenChange`, which may cancel or allow propagation. */
  onOpenChange?: (open: boolean, details: HarnessChangeDetails) => void;
  /** The consumer's `onclick` on the trigger, run by the `press` decider; returning `false` vetoes (`PreventBaseUIHandler()`). */
  onTriggerClick?: (event: EventInfo) => boolean | void;
}

export interface PopupHarness {
  readonly token: string;
  readonly open: boolean;
  readonly revision: number;
  readonly activeTriggerId: string | null;
  /** Every request any handle reported, in order. */
  readonly requests: HarnessRequest[];
  /** Every `OnOpenChange` call: the open value and the details. */
  readonly changes: { open: boolean; details: HarnessChangeDetails }[];
  /** Every `Decide` call: the point and the snapshot. */
  readonly decisions: { point: string; event: EventInfo }[];
  /** Every synchronous hand-over before `press`. */
  readonly pressings: Record<string, unknown>[];
  /** Attaches the session handle (the Root's token form). */
  attachSession(options?: Partial<InteractionsOptions>): TwinHandle;
  /** Attaches a trigger handle to an element; the element gets an id when it has none. */
  attachTrigger(element: HTMLElement, options?: Partial<InteractionsOptions>): TwinHandle;
  /** Attaches a floating handle to an element. */
  attachFloating(element: HTMLElement, options?: Partial<InteractionsOptions>): TwinHandle;
  /** Sends a new session `update`, merging `options` into the last one sent (a re-render of the Root). */
  updateSession(options: Partial<InteractionsOptions>): void;
  /**
   * Makes an attached trigger the popup's anchor without a commit (the test shim's `refs.setReference`, which Base UI's
   * hook tests rely on while the popup is closed); sends a session `update`.
   */
  setReference(element: HTMLElement): void;
  /** Commits a change C# started (a controlled flip, a handle's `OpenAsync`), relayed like any commit. */
  setOpen(open: boolean, reason?: string, triggerId?: string | null): void;
  /** Disposes every handle the harness attached. */
  dispose(): void;
}

let tokens = 0;

/**
 * Creates the harness of one popup.
 * @param options The mode, the initial state and the consumer's callbacks.
 * @returns The harness.
 */
export function createPopupHarness(options: HarnessOptions = {}): PopupHarness {
  tokens += 1;
  const token = options.token ?? `session-${tokens}`;
  const requests: HarnessRequest[] = [];
  const changes: { open: boolean; details: HarnessChangeDetails }[] = [];
  const decisions: { point: string; event: EventInfo }[] = [];
  const pressings: Record<string, unknown>[] = [];
  const handles: TwinHandle[] = [];
  let open = options.open ?? false;
  let revision = 0;
  let activeTriggerId: string | null = null;
  let relay: OpenChangeRelay = { revision: 0, reason: 'none', eventKind: null, eventType: null, pointerType: null, triggerId: null };
  let sessionHandle: TwinHandle | null = null;
  let sessionOptions: InteractionsOptions = {};
  let stash: Record<string, unknown> | null = null;

  const sendSession = () => {
    sessionOptions = {
      ...sessionOptions,
      sessionToken: token,
      relay,
      session: { ...sessionOptions.session, open, activeTriggerId },
    };
    sessionHandle?.update(JSON.stringify(sessionOptions));
  };

  const details = (payload: Record<string, unknown>): HarnessChangeDetails => {
    const record: HarnessChangeDetails = {
      reason: payload.reason as string,
      event: (payload.event as EventInfo | null) ?? null,
      triggerId: (payload.triggerId as string | null) ?? null,
      instant: payload.instant === true,
      isCanceled: false,
      isPropagationAllowed: false,
      cancel() {
        record.isCanceled = true;
      },
      allowPropagation() {
        record.isPropagationAllowed = true;
      },
    };
    return record;
  };

  // The popup store's open-change sequence (ADR 05 §2): the consumer first, a cancel honoured, then the write and the relay.
  const commit = (payload: Record<string, unknown>): HarnessChangeDetails => {
    const next = payload.open === true;
    const change = details(payload);
    changes.push({ open: next, details: change });
    options.onOpenChange?.(next, change);
    if (change.isCanceled || options.commit === false) {
      return change;
    }
    open = next;
    revision += 1;
    if (next && change.triggerId !== null) {
      activeTriggerId = change.triggerId;
    }
    relay = {
      revision,
      reason: change.reason,
      eventKind: change.event?.kind ?? null,
      eventType: change.event?.type ?? null,
      pointerType: change.event?.pointerType ?? null,
      triggerId: change.triggerId,
    };
    sendSession();
    return change;
  };

  const verdictOf = (change: HarnessChangeDetails): DecisionPolicy => {
    if (change.isCanceled) {
      return change.isPropagationAllowed ? 'run' : 'stop';
    }
    return change.isPropagationAllowed ? 'prevent' : 'prevent-and-stop';
  };

  const twinFor = (): DotNetTwin => ({
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      if (!options.synchronous) {
        throw new Error('The current dispatcher does not support synchronous calls from JS to .NET.');
      }
      switch (methodName) {
        case 'Ping':
          return true as T;
        case 'OnPressing': {
          stash = JSON.parse(args[0] as string) as Record<string, unknown>;
          pressings.push(stash);
          return undefined as T;
        }
        case 'Decide': {
          const point = args[0] as string;
          const event = JSON.parse(args[1] as string) as EventInfo;
          decisions.push({ point, event });
          if (point === 'press') {
            const pressed = stash;
            stash = null;
            if (options.onTriggerClick?.(event) === false) {
              return 'stop' as T;
            }
            if (pressed !== null) {
              commit({ open: pressed.open, reason: (pressed.reason as string | undefined) ?? 'trigger-press', event, triggerId: pressed.triggerId });
            }
            return 'run' as T;
          }
          if (point === 'escape') {
            return verdictOf(commit({ open: false, reason: 'escape-key', event, triggerId: null })) as T;
          }
          return 'run' as T;
        }
        default:
          throw new Error(`Unexpected synchronous call to ${methodName}`);
      }
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      const payload = JSON.parse(args[0] as string) as Record<string, unknown>;
      requests.push({ method: methodName, payload });
      if (methodName === 'OnRequest') {
        commit(payload);
      } else if (methodName === 'OnPress') {
        if (options.onTriggerClick?.(payload.event as EventInfo) !== false) {
          commit({ ...payload, reason: (payload.reason as string | undefined) ?? 'trigger-press' });
        }
      }
      return Promise.resolve(undefined as T);
    },
  });

  const track = (handle: TwinHandle): TwinHandle => {
    handles.push(handle);
    return handle;
  };

  let ids = 0;
  const ensureId = (element: HTMLElement) => {
    if (!element.id) {
      ids += 1;
      element.id = `${token}-element-${ids}`;
    }
  };

  return {
    token,
    get open() {
      return open;
    },
    get revision() {
      return revision;
    },
    get activeTriggerId() {
      return activeTriggerId;
    },
    requests,
    changes,
    decisions,
    pressings,
    attachSession(extra = {}) {
      sessionOptions = {
        ...extra,
        sessionToken: token,
        relay,
        session: { ...extra.session, open, activeTriggerId },
      };
      sessionHandle = track(attach(null, JSON.stringify(sessionOptions), twinFor()));
      return sessionHandle;
    },
    attachTrigger(element, extra = {}) {
      ensureId(element);
      return track(attach(element, JSON.stringify({ trigger: {}, ...extra, sessionToken: token }), twinFor()));
    },
    attachFloating(element, extra = {}) {
      ensureId(element);
      return track(attach(element, JSON.stringify({ floating: {}, ...extra, sessionToken: token }), twinFor()));
    },
    updateSession(extra) {
      sessionOptions = { ...sessionOptions, ...extra, session: { ...sessionOptions.session, ...extra.session } };
      sendSession();
    },
    setReference(element) {
      ensureId(element);
      activeTriggerId = element.id;
      sendSession();
    },
    setOpen(next, reason = 'none', triggerId = null) {
      commit({ open: next, reason, event: null, triggerId });
    },
    dispose() {
      for (const handle of handles.splice(0)) {
        handle.dispose();
      }
      sessionHandle = null;
    },
  };
}
