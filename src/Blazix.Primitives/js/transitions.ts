// The `transitions` mechanism module (ADR 04 §1): the frame scheduler every frame deferral shares (TRN-EDGE-06), the
// finite-animation filter and the animations-finished watcher (TRN-ALG-02/03), the transition status machine of an
// attached element with its next-frame flips of `data-starting-style` / `data-ending-style` (TRN-ALG-01), the
// `hidden="until-found"` write and the `beforematch` reveal (TRN-ALG-07), and the completion notification with the
// optimistic convergent `hidden` (ADR 04 §2).
// Transcribed from Base UI v1.9.0: packages/utils/src/useAnimationFrame.ts; utils/getFiniteAnimations.ts;
// internals/useAnimationsFinished.ts, useTransitionStatus.ts, useOpenChangeComplete.tsx,
// TransitionStatusDataAttributes.ts; collapsible/panel/useCollapsiblePanel.ts (the until-found contract, lines
// 355-459). Requirements: docs/architecture/transitions-and-mounting.md (TRN).
//
// Port mapping (TRN §8, ADR 04 §2, Amendments 2026-10-18): C# renders the initial status of an element once, in
// prerender and in its first interactive render (handover at attach), and passes it with `attach`. From then on this
// module runs the machine for the element and is the only writer of the two transition attributes; C# mirrors the
// status through `OnStatusChange` (one message per change, never per frame). The machine's input is the `open` value
// of `update`. A "synchronous commit" of a completion is the callback itself: it writes the DOM and reports, and on
// WebAssembly the report renders before the callback returns.
// Two port decisions (ADR 01 and ADR 04 Amendments 2026-10-21): the open input of an attached element is read from the
// applied render batch through `openSelector` (TRN-STA-03 on Server; ADR 01 decision 4; the evaluation step in both
// languages, ADR 01 decision 2 note), and `beforematch` is the decision point `reveal`, fallback `run`, whose Server
// revert waits for C#'s answer to the request plus a frame (TRN-ANI-33/34; ADR 01 relaxation table).
import { decide } from './decide';
import { eventInfo } from './event-info';
import {
  applyConsumerStyle,
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinHandle,
  type TwinOptions,
} from './twin';

declare global {
  // The animations-disabled switch the test harnesses set (TRN-EDGE-01, ADR 06 §2).
  // eslint-disable-next-line no-var
  var BASE_UI_ANIMATIONS_DISABLED: boolean | undefined;
}

/** The phase of an enter or exit transition; `undefined` is the unset status. */
export type TransitionStatus = 'starting' | 'ending' | 'idle' | undefined;

/** The JSON form of {@link TransitionStatus}: the literal, or `null` for the unset status. */
export type TransitionStatusLiteral = 'starting' | 'ending' | 'idle' | null;

/** The attribute present while an element is animating in. */
export const STARTING_STYLE = 'data-starting-style';

/** The attribute present while an element is animating out. */
export const ENDING_STYLE = 'data-ending-style';

/** The name of the decision point a `beforematch` reveal asks C# about. */
export const REVEAL_POINT = 'reveal';

const noop = (): void => {};

// ---------------------------------------------------------------------------------------------------------------
// Frame scheduler (TRN-EDGE-06)
// ---------------------------------------------------------------------------------------------------------------

type FrameCallback = (timestamp: number) => void;

let lastRequestAnimationFrame: typeof requestAnimationFrame | undefined = globalThis.requestAnimationFrame;

// One native frame request per task: every request made before the frame fires joins it, and a canceled request is
// an O(1) no-op slot rather than a canceled native frame.
class FrameScheduler {
  callbacks: (FrameCallback | null)[] = [];
  count = 0;
  nextId: number;
  startId: number;
  scheduled = false;

  constructor(firstId: number) {
    this.nextId = firstId;
    this.startId = firstId;
  }

  readonly tick = (timestamp: number): void => {
    this.scheduled = false;
    const callbacks = this.callbacks;
    const count = this.count;
    // Reset before running: a callback may request the next frame.
    this.callbacks = [];
    this.count = 0;
    this.startId = this.nextId;
    if (count > 0) {
      for (const callback of callbacks) {
        callback?.(timestamp);
      }
    }
  };

  request(callback: FrameCallback): number {
    const id = this.nextId;
    this.nextId += 1;
    this.callbacks.push(callback);
    this.count += 1;
    // A test that replaces `requestAnimationFrame` must not inherit a frame scheduled through the previous one.
    const raf = globalThis.requestAnimationFrame;
    const replaced = raf !== lastRequestAnimationFrame;
    lastRequestAnimationFrame = raf;
    if (!this.scheduled || replaced) {
      raf(this.tick);
      this.scheduled = true;
    }
    return id;
  }

  cancel(id: number): void {
    const index = id - this.startId;
    if (index < 0 || index >= this.callbacks.length || this.callbacks[index] === null) {
      return;
    }
    this.callbacks[index] = null;
    this.count -= 1;
  }
}

let scheduler = new FrameScheduler(1);

/**
 * Requests a callback on the next animation frame. Every request made in one task shares one native frame request.
 * @param callback Called with the frame's timestamp.
 * @returns The request id for {@link cancelFrame}.
 */
export function requestFrame(callback: FrameCallback): number {
  return scheduler.request(callback);
}

/**
 * Cancels a frame request. Cancelling a request that already ran, or an unknown id, does nothing.
 * @param id The id {@link requestFrame} returned.
 */
export function cancelFrame(id: number): void {
  scheduler.cancel(id);
}

/**
 * Drops every pending frame callback. For tests only: the scheduler is page-global, so a callback that never ran in
 * one test would otherwise run in a later one.
 */
export function resetFrameScheduler(): void {
  const previous = scheduler;
  scheduler = new FrameScheduler(previous.nextId);
  previous.callbacks = [];
  previous.count = 0;
}

/** One pending frame at most: requesting again replaces the pending request. */
export interface FrameSlot {
  request(callback: () => void): void;
  cancel(): void;
}

/**
 * Creates a slot that holds at most one pending frame request.
 * @returns The slot.
 */
export function createFrameSlot(): FrameSlot {
  let current: number | null = null;
  const cancel = () => {
    if (current !== null) {
      scheduler.cancel(current);
      current = null;
    }
  };
  return {
    request(callback) {
      cancel();
      current = scheduler.request(() => {
        current = null;
        callback();
      });
    },
    cancel,
  };
}

// ---------------------------------------------------------------------------------------------------------------
// Animations-finished watcher (TRN-ALG-02, TRN-ALG-03)
// ---------------------------------------------------------------------------------------------------------------

/**
 * Determines whether the animations-disabled switch is on. Read at every check, so a test can turn it off mid-run.
 * @returns `true` when `globalThis.BASE_UI_ANIMATIONS_DISABLED` is truthy.
 */
export function animationsDisabled(): boolean {
  return Boolean(globalThis.BASE_UI_ANIMATIONS_DISABLED);
}

/**
 * Returns an element's running animations whose effect timing has a finite duration and a finite iteration count;
 * infinite animations never finish while they run.
 * @param element The element.
 * @param options Passed to `getAnimations`; the completion watcher passes none (descendants are not awaited).
 * @returns The finite animations.
 */
export function getFiniteAnimations(element: Element, options?: GetAnimationsOptions): Animation[] {
  return element.getAnimations(options).filter((animation) => {
    const timing = animation.effect?.getTiming();
    return timing?.duration !== Infinity && timing?.iterations !== Infinity;
  });
}

let pendingFlush: (() => void)[] | null = null;

// Completions that become ready in the same microtask run together, in one flush.
function flushBeforePaint(callback: () => void): void {
  if (pendingFlush === null) {
    const callbacks: (() => void)[] = [];
    pendingFlush = callbacks;
    queueMicrotask(() => {
      pendingFlush = null;
      for (const queued of callbacks) {
        queued();
      }
    });
  }
  pendingFlush.push(callback);
}

/** The element a watcher inspects: the element, a function that returns it when the wait is armed, or `null`. */
export type WatchedElement = HTMLElement | (() => HTMLElement | null) | null;

/** Runs a callback once every finite animation of an element has finished. */
export interface AnimationsWatcher {
  /**
   * Arms a wait, cancelling a previously armed wait whose first inspection is still frame-deferred.
   * @param callback Runs once the element's finite animations have finished; never runs when the element is missing.
   * @param signal Aborts the wait: the callback, a restart after a canceled animation and the attribute observer.
   */
  run(callback: () => void, signal?: AbortSignal | null): void;
  /** Cancels a pending frame-deferred inspection. */
  cancel(): void;
}

/**
 * Creates the animations-finished watcher of one element (Base UI's `useAnimationsFinished`).
 * @param element The element to watch, or a function that resolves it each time a wait is armed.
 * @param waitForStartingStyleRemoved When the element carries `data-starting-style` as the wait is armed, the first
 * inspection runs in the microtask after its removal instead of on the next frame.
 * @param batch Completions ready in the same microtask run together in one flush; each re-checks its signal there.
 * @returns The watcher.
 */
export function createAnimationsWatcher(element: WatchedElement, waitForStartingStyleRemoved = false, batch = false): AnimationsWatcher {
  const frame = createFrameSlot();

  return {
    run(callback, signal = null) {
      frame.cancel();

      const resolved = typeof element === 'function' ? element() : element;
      if (resolved == null) {
        return;
      }

      const done = () => {
        if (!batch) {
          callback();
          return;
        }
        flushBeforePaint(() => {
          // The signal may abort between queueing and the flush.
          if (!signal?.aborted) {
            callback();
          }
        });
      };

      if (typeof resolved.getAnimations !== 'function' || animationsDisabled()) {
        callback();
        return;
      }

      const inspect = () => {
        // Discard the fulfilled Animation values so a pending wait cannot retain its targets.
        Promise.all(getFiniteAnimations(resolved).map((animation) => animation.finished.then(noop))).then(
          () => {
            if (!signal?.aborted) {
              done();
            }
          },
          () => {
            if (signal?.aborted) {
              return;
            }
            // A canceled animation is often replaced by a new one when the property it animated changes mid-flight.
            if (getFiniteAnimations(resolved).some((animation) => animation.pending || animation.playState !== 'finished')) {
              inspect();
              return;
            }
            done();
          },
        );
      };

      if (waitForStartingStyleRemoved) {
        // Without the attribute, wait one frame so the commit's own transitions are registered first.
        if (!resolved.hasAttribute(STARTING_STYLE)) {
          frame.request(inspect);
          return;
        }
        const observer = new MutationObserver(() => {
          if (!resolved.hasAttribute(STARTING_STYLE)) {
            observer.disconnect();
            inspect();
          }
        });
        observer.observe(resolved, { attributes: true, attributeFilter: [STARTING_STYLE] });
        signal?.addEventListener('abort', () => observer.disconnect(), { once: true });
        return;
      }

      frame.request(inspect);
    },
    cancel() {
      frame.cancel();
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// Transition status machine (TRN-ALG-01)
// ---------------------------------------------------------------------------------------------------------------

/** The machine's input and variables. */
export interface TransitionState {
  readonly open: boolean;
  readonly mounted: boolean;
  readonly status: TransitionStatus;
}

/** The fixed configuration of a machine. */
export interface TransitionMachineOptions {
  /** Reports `'idle'` when settled instead of unset, and passes a reopen during the exit through `'starting'`. */
  enableIdleState: boolean;
  /** Applies `'ending'` on the next frame after the closing commit instead of in it. */
  deferEndingState: boolean;
}

/** The transition status machine of one element. */
export interface TransitionMachine extends TransitionState {
  /** Sets the open input. */
  setOpen(open: boolean): void;
  /** Sets the mounted flag from outside: `false` once exit animations finished, `true` to show an element without `'starting'`. */
  setMounted(mounted: boolean): void;
  /** Cancels every pending frame; later inputs are ignored. */
  dispose(): void;
}

/**
 * The evaluation step of the machine: what the commit that applies an input renders. C# runs the same step for the
 * render that first shows an element; one case table tests both (`test-support/transition-evaluation.cases.json`).
 * @param state The state after the input changed.
 * @param deferEndingState Whether `'ending'` waits for the next frame.
 * @returns The mounted flag and the status of the same commit.
 */
export function evaluateTransition(state: TransitionState, deferEndingState: boolean): { mounted: boolean; status: TransitionStatus } {
  let { mounted, status } = state;
  if (state.open && !mounted) {
    // The same commit that first renders the part as open.
    mounted = true;
    status = 'starting';
  }
  if (!state.open && mounted && status !== 'ending' && !deferEndingState) {
    status = 'ending';
  }
  if (!state.open && !mounted && status === 'ending') {
    status = undefined;
  }
  return { mounted, status };
}

/**
 * Creates the transition status machine of an element (Base UI's `useTransitionStatus`). Each input change is one
 * commit: the evaluation step runs, `onCommit` sees the new state (the DOM write), then the frame steps are armed.
 * @param initial The state the element was rendered with.
 * @param options The machine options.
 * @param onCommit Called after every commit that changed the state, before the frame steps are armed. It may set an
 * input; the change is applied once it returns.
 * @returns The machine. Its first commit (the frame steps for the initial state) runs before it is returned.
 */
export function createTransitionMachine(
  initial: TransitionState,
  options: TransitionMachineOptions,
  onCommit: (state: TransitionState, previous: TransitionState) => void,
): TransitionMachine {
  let { open, mounted, status } = initial;
  let committed: TransitionState = { open, mounted, status };
  const endingFrame = createFrameSlot();
  const clearFrame = createFrameSlot();
  const idleFrame = createFrameSlot();
  // The dependency values each frame step last ran with; a step re-runs only when one of them changed.
  let endingDeps: string | null = null;
  let clearDeps: string | null = null;
  let idleDeps: string | null = null;
  let running = false;
  let again = false;
  let disposed = false;

  const evaluate = () => {
    ({ mounted, status } = evaluateTransition({ open, mounted, status }, options.deferEndingState));
  };

  const setStatus = (next: TransitionStatus) => {
    if (disposed || status === next) {
      return;
    }
    status = next;
    cycle();
  };

  const frameSteps = () => {
    const { enableIdleState, deferEndingState } = options;

    // Deferred ending: the closing commit keeps the open styles for one frame.
    const ending = `${open}|${mounted}|${status}|${deferEndingState}`;
    if (ending !== endingDeps) {
      endingDeps = ending;
      endingFrame.cancel();
      if (!open && mounted && status !== 'ending' && deferEndingState) {
        endingFrame.request(() => setStatus('ending'));
      }
    }

    // Default settle: clears 'starting', or a stale 'ending' after a reopen.
    const clear = `${enableIdleState}|${open}|${status}`;
    if (clear !== clearDeps) {
      clearDeps = clear;
      clearFrame.cancel();
      if (open && !enableIdleState && status !== undefined) {
        clearFrame.request(() => setStatus(undefined));
      }
    }

    // Idle settle.
    const idle = `${enableIdleState}|${open}|${mounted}|${status}`;
    if (idle !== idleDeps) {
      idleDeps = idle;
      idleFrame.cancel();
      if (open && enableIdleState) {
        if (mounted && status !== 'idle') {
          // Post-commit, pre-paint; the next pass re-runs this step for the new status.
          status = 'starting';
        }
        idleFrame.request(() => setStatus('idle'));
      }
    }
  };

  const cycle = () => {
    if (running) {
      again = true;
      return;
    }
    running = true;
    try {
      do {
        again = false;
        evaluate();
        if (open !== committed.open || mounted !== committed.mounted || status !== committed.status) {
          const previous = committed;
          committed = { open, mounted, status };
          onCommit(committed, previous);
        }
        if (disposed) {
          return;
        }
        frameSteps();
        if (status !== committed.status) {
          again = true;
        }
      } while (again);
    } finally {
      running = false;
    }
  };

  cycle();

  return {
    get open() {
      return open;
    },
    get mounted() {
      return mounted;
    },
    get status() {
      return status;
    },
    setOpen(next) {
      if (disposed || next === open) {
        return;
      }
      open = next;
      cycle();
    },
    setMounted(next) {
      if (disposed || next === mounted) {
        return;
      }
      mounted = next;
      cycle();
    },
    dispose() {
      disposed = true;
      endingFrame.cancel();
      clearFrame.cancel();
      idleFrame.cancel();
    },
  };
}

/**
 * Converts the JSON literal of a status.
 * @param literal The literal, or `null`/absent for the unset status.
 * @returns The status.
 */
export function statusFromLiteral(literal: TransitionStatusLiteral | undefined): TransitionStatus {
  return literal ?? undefined;
}

/**
 * Converts a status to its JSON literal.
 * @param status The status.
 * @returns The literal, or `null` for the unset status.
 */
export function statusToLiteral(status: TransitionStatus): TransitionStatusLiteral {
  return status ?? null;
}

// ---------------------------------------------------------------------------------------------------------------
// Attribute writers (TRN-DOM-01/02, TRN-DOM-06/08/09, TRN-EDGE-08)
// ---------------------------------------------------------------------------------------------------------------

/** The two transition attributes of one element. */
export interface TransitionAttributes {
  /**
   * Writes `data-starting-style` and `data-ending-style`, each only when its value changes, as a renderer diffs: an
   * attribute something else changed in between (the browser or the reveal handler) stays as it is until then.
   * @param starting Whether `data-starting-style` is present: the shown status is `'starting'`, or a hidden
   * hidden-until-found element keeps its collapsed starting styles.
   * @param ending Whether `data-ending-style` is present.
   */
  write(starting: boolean, ending: boolean): void;
}

/**
 * Creates the writer of an element's transition attributes. The first write always applies.
 * @param element The element.
 * @returns The writer.
 */
export function createTransitionAttributes(element: HTMLElement): TransitionAttributes {
  let startingWritten: boolean | null = null;
  let endingWritten: boolean | null = null;
  return {
    write(starting, ending) {
      if (starting !== startingWritten) {
        startingWritten = starting;
        element.toggleAttribute(STARTING_STYLE, starting);
        if (starting) {
          // The element's style is computed with the starting style now: a render and an attach that land before the
          // frame that first styles the element (Server on a fast circuit) would otherwise have the clearing frame take
          // it off before any style was computed with it, and no transition would run from it.
          element.ownerDocument.defaultView?.getComputedStyle(element).getPropertyValue('opacity');
        }
      }
      if (ending !== endingWritten) {
        endingWritten = ending;
        element.toggleAttribute(ENDING_STYLE, ending);
      }
    },
  };
}

/** The `hidden` attribute of one element, as far as the module writes it. */
export interface HiddenController {
  /**
   * Writes `hidden` when the hidden predicate or the until-found option changed since the last call, and never
   * otherwise, so the browser's own removal during a reveal stands until the revert. The first call is the mount.
   * @param hidden Whether the element is closed and not mounted.
   * @param untilFound Whether it is hidden with the literal `hidden="until-found"`, written on every change to hidden.
   * @param hideOnChange Whether a change to hidden writes a boolean `hidden` (the convergent write at exit end); not on
   * the mount, where C# rendered the value.
   */
  write(hidden: boolean, untilFound: boolean, hideOnChange: boolean): void;
  /** Stops turning a boolean `hidden` into the literal. */
  dispose(): void;
}

/**
 * Creates the controller of an element's `hidden` attribute. While the element is hidden until found, a boolean
 * `hidden` that something else writes (C#'s render) becomes the literal in the microtask after the write, before
 * paint. A `hidden` the controller wrote is removed once the element is no longer hidden.
 * @param element The element.
 * @returns The controller.
 */
export function createHiddenController(element: HTMLElement): HiddenController {
  let state: { hidden: boolean; untilFound: boolean } | null = null;
  let written = false;

  const observer = new MutationObserver(() => {
    if (state?.hidden && state.untilFound && element.getAttribute('hidden') === '') {
      element.setAttribute('hidden', 'until-found');
    }
  });
  observer.observe(element, { attributes: true, attributeFilter: ['hidden'] });

  return {
    write(hidden, untilFound, hideOnChange) {
      const previous = state;
      if (previous !== null && previous.hidden === hidden && previous.untilFound === untilFound) {
        return;
      }
      state = { hidden, untilFound };
      if (hidden && untilFound) {
        written = true;
        element.setAttribute('hidden', 'until-found');
      } else if (hidden && previous !== null && hideOnChange) {
        written = true;
        element.setAttribute('hidden', '');
      } else if (!hidden && written) {
        written = false;
        element.removeAttribute('hidden');
      }
    },
    dispose() {
      observer.disconnect();
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The `beforematch` reveal (TRN-ALG-07)
// ---------------------------------------------------------------------------------------------------------------

/** The outcome of a reveal request inside the `beforematch` dispatch. */
export interface RevealDecision {
  /** Whether the reveal goes ahead; `false` when a C# handler canceled the request inside the dispatch. */
  proceed: boolean;
  /**
   * When no C# handler could run inside the dispatch (Server), C#'s answer once the request has run: whether the part
   * is open. The revert check waits for it instead of for a 0 ms task. `null` when the request ran inside the dispatch.
   */
  answer: Promise<boolean> | null;
}

/** What a reveal controller reads from and does to its element. */
export interface RevealHost {
  /** The latest open input. */
  readonly open: boolean;
  /** Resolves the `reveal` decision point and sends the request. */
  request(event: Event): RevealDecision;
}

/** The reveal handling of one hidden-until-found element. */
export interface RevealController {
  /** Whether a `beforematch` armed the one-shot motion skip that the next opening pass consumes. */
  readonly skipArmed: boolean;
  /** Consumes the one-shot motion skip: returns whether it was armed, and disarms it. */
  consumeSkip(): boolean;
  /** Cancels a pending revert and removes the listener. */
  dispose(): void;
}

/**
 * Listens for `beforematch` on an element and handles the reveal: the open request inside the dispatch, the
 * synchronous removal of `data-starting-style` before the browser measures the match, the one-shot motion skip, and
 * the revert to `hidden="until-found"` when the open does not arrive. The revert is decided after a 0 ms task when the
 * request ran inside the dispatch, and when C#'s answer arrives otherwise; it is applied on the next frame.
 * @param element The panel element.
 * @param host The open input and the request.
 * @returns The controller.
 */
export function createRevealController(element: HTMLElement, host: RevealHost): RevealController {
  let skipNextOpen = false;
  let decisionTimeout: ReturnType<typeof setTimeout> | undefined;
  // Each reveal supersedes the previous one's pending answer; disposal supersedes them all.
  let generation = 0;
  const revertFrame = createFrameSlot();

  // Whether the open arrived is decided in a task, so a pending open commit is always observed first (or, on Server,
  // when C# has answered, which is after the request's own render); the DOM revert then runs in a frame so it is
  // atomic with respect to paint.
  const scheduleRevert = (resetSkip: boolean, restoreStartingStyle: boolean, answer: Promise<boolean> | null) => {
    clearTimeout(decisionTimeout);
    decisionTimeout = undefined;
    revertFrame.cancel();
    generation += 1;
    const current = generation;

    const decide = (openAfterRequest: boolean) => {
      if (current !== generation || openAfterRequest || host.open) {
        return;
      }
      revertFrame.request(() => {
        if (host.open) {
          return;
        }
        if (resetSkip) {
          // The open never happened, so the next one is an ordinary open with its authored motion.
          skipNextOpen = false;
        }
        if (restoreStartingStyle) {
          element.setAttribute(STARTING_STYLE, '');
        }
        element.setAttribute('hidden', 'until-found');
      });
    };

    if (answer !== null) {
      answer.then(decide, () => decide(false));
      return;
    }
    decisionTimeout = setTimeout(() => {
      decisionTimeout = undefined;
      decide(false);
    }, 0);
  };

  const onBeforeMatch = (event: Event) => {
    // Taken off before the request: on WebAssembly the request commits the open, whose opening pass runs before the
    // request returns and must already see the skip and the attribute gone. A canceled request puts both back inside
    // this dispatch, before the browser measures the match.
    const hadStartingStyle = element.hasAttribute(STARTING_STYLE);
    const skipBefore = skipNextOpen;
    skipNextOpen = true;
    element.removeAttribute(STARTING_STYLE);

    const { proceed, answer } = host.request(event);
    if (!proceed) {
      skipNextOpen = skipBefore;
      if (hadStartingStyle && !host.open) {
        element.setAttribute(STARTING_STYLE, '');
      }
      scheduleRevert(false, false, answer);
      return;
    }

    scheduleRevert(true, hadStartingStyle, answer);
  };

  element.addEventListener('beforematch', onBeforeMatch);

  return {
    get skipArmed() {
      return skipNextOpen;
    },
    consumeSkip() {
      const armed = skipNextOpen;
      skipNextOpen = false;
      return armed;
    },
    dispose() {
      clearTimeout(decisionTimeout);
      decisionTimeout = undefined;
      generation += 1;
      revertFrame.cancel();
      element.removeEventListener('beforematch', onBeforeMatch);
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The open input read from the applied render batch
// ---------------------------------------------------------------------------------------------------------------

/**
 * Reads a part's open input from the attributes C# renders on its element: the element is open while it matches the
 * selector. The observer's callback runs in the microtask after a render batch is applied, before paint, so the
 * transition attributes of an already-attached element land with the batch that opened or closed it on both render
 * modes (on Server the `update` that carries the same value arrives a round trip later).
 * @param element The element.
 * @param selector The selector C#'s attributes satisfy while open, such as `[data-open]`.
 * @param onChange Called with the open value whenever an attribute of the element changed.
 * @returns The function that stops observing.
 */
export function observeOpenInput(element: HTMLElement, selector: string, onChange: (open: boolean) => void): () => void {
  const observer = new MutationObserver(() => onChange(element.matches(selector)));
  observer.observe(element, { attributes: true });
  return () => observer.disconnect();
}

// ---------------------------------------------------------------------------------------------------------------
// The twin-facing entry
// ---------------------------------------------------------------------------------------------------------------

/** The options of a `transitions` handle; mirrors `TransitionsOptions` in C#. */
export interface TransitionsOptions extends TwinOptions {
  /** The machine's input: whether the part is open. */
  open: boolean;
  /** The mounted flag the element was rendered with; read on `attach` only. */
  mounted: boolean;
  /** The status the element was rendered with (the handover's initial value); read on `attach` only. */
  status?: TransitionStatusLiteral;
  /** Reports `'idle'` when settled and passes a reopen during the exit through `'starting'`. */
  enableIdleState: boolean;
  /** Applies `'ending'` one frame after the closing commit. */
  deferEndingState: boolean;
  /** Whether the element stays in the document while not mounted; `false` means C# removes it. */
  keepMounted: boolean;
  /** Hides the element with `hidden="until-found"` while it is closed and not mounted, and reveals it on `beforematch`. */
  hiddenUntilFound: boolean;
  /** Whether a kept element carries `hidden` while it is not mounted (a container, not an indicator). */
  hiddenWhenUnmounted: boolean;
  /** Never writes `data-ending-style` and reports `'ending'` as unset; the exit is still awaited. */
  suppressEndingStyle: boolean;
  /** Reports the completion of the open side too, once the enter animations finished. */
  reportOpenComplete: boolean;
  /** Listens for `beforematch` on the element. */
  revealOnBeforeMatch: boolean;
  /** How many times C# has asked the module to unmount the element; each increase while closed unmounts it. */
  unmountRequests: number;
  /**
   * Whether another part's exit ends this element's: no exit watcher is armed, and the exit ends only when
   * `unmountRequests` increases (the Collapsible Root and Trigger, whose `'ending'` lasts until the panel's ends).
   */
  externalExit?: boolean;
  /**
   * The selector the element matches while C# renders it open. When set, it is the open input, read from the DOM
   * after every render batch, and `open` is not read after `attach`.
   */
  openSelector?: string | null;
}

// A symbol on the element, not a module registry: a repeated attach of the same element returns the live handle.
const attachedHandle = Symbol('blazix.transitions.handle');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

/**
 * Attaches the transition machine to an animated part's element. From here on the module writes
 * `data-starting-style` and `data-ending-style`, reports each status change once (`OnStatusChange`), waits for the
 * exit animations of a close (and, if asked, the enter animations of an open) and reports each completion once
 * (`OnTransitionComplete`). At exit end it hides an element C# is about to remove or hide, ahead of that render.
 * The consumer's style is applied only when the part hands it over (`options.style` set).
 * @param elementOrId The element, or its id when the part has no element reference.
 * @param optionsJson The `TransitionsOptions` JSON.
 * @param twinReference The twin's object reference.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin): TwinHandle {
  const element = resolveElement(elementOrId) as AttachedElement;
  const existing = element[attachedHandle];
  if (existing) {
    return existing;
  }

  const twin = createTwinChannel(twinReference);
  let options = parseOptions<TransitionsOptions>(optionsJson);
  let disposed = false;
  let ready = false;
  let unmountRequests = options.unmountRequests;
  // The opening pass of a `beforematch` reveal shows the element settled while the machine still says 'starting'.
  let forcedIdle = false;
  let closeCompleted = false;
  let reported: { status: TransitionStatus; mounted: boolean } = { status: statusFromLiteral(options.status), mounted: options.mounted };
  const attributes = createTransitionAttributes(element);
  const hiddenController = createHiddenController(element);

  const closeWatcher = createAnimationsWatcher(element);
  const openWatcher = createAnimationsWatcher(element, true);
  const closeDelay = createFrameSlot();
  let closeKey: string | null = null;
  let openKey: boolean | null = null;
  let closeController: AbortController | null = null;
  let openController: AbortController | null = null;
  let reveal: RevealController | null = null;

  const shownStatus = (status: TransitionStatus): TransitionStatus => {
    if (forcedIdle && status === 'starting') {
      return 'idle';
    }
    if (options.suppressEndingStyle && status === 'ending') {
      return undefined;
    }
    return status;
  };

  const isHidden = (state: TransitionState) => !state.open && !state.mounted;

  const writeAttributes = (state: TransitionState) => {
    const shown = shownStatus(state.status);
    // A hidden-until-found element keeps its collapsed starting styles while hidden, so a reveal starts from them.
    attributes.write(shown === 'starting' || (options.hiddenUntilFound && isHidden(state)), shown === 'ending');
  };

  // The convergent write at exit end: the render C# makes for this state removes or hides the element anyway.
  const writeHidden = (state: TransitionState) =>
    hiddenController.write(isHidden(state), options.hiddenUntilFound, !options.keepMounted || options.hiddenWhenUnmounted);

  const report = (state: TransitionState) => {
    const status = shownStatus(state.status);
    if (status === reported.status && state.mounted === reported.mounted) {
      return;
    }
    reported = { status, mounted: state.mounted };
    if (closeCompleted && !state.mounted) {
      // The completion message stands for this change: C# clears the mounted flag and the status from it.
      closeCompleted = false;
      twin.report('OnTransitionComplete', { open: false });
      return;
    }
    twin.report('OnStatusChange', { status: statusToLiteral(status), mounted: state.mounted });
  };

  const completeClose = () => {
    if (!machine.open && machine.mounted) {
      closeCompleted = true;
      machine.setMounted(false);
    }
  };

  const completeOpen = () => {
    if (machine.open) {
      twin.report('OnTransitionComplete', { open: true });
    }
  };

  // Each watcher arms whenever its enabled state or the open input changes, aborting the previous wait.
  const armWatchers = (state: TransitionState) => {
    const closeEnabled = !options.externalExit && state.mounted && !state.open && (!options.deferEndingState || state.status === 'ending');
    const nextCloseKey = `${closeEnabled}|${state.open}`;
    if (nextCloseKey !== closeKey) {
      closeKey = nextCloseKey;
      closeController?.abort();
      closeController = null;
      closeDelay.cancel();
      if (closeEnabled) {
        const controller = new AbortController();
        closeController = controller;
        const run = () => closeWatcher.run(completeClose, controller.signal);
        // After a deferred ending, one more frame: an exit transition the browser registers late is still seen.
        if (options.deferEndingState) {
          closeDelay.request(run);
        } else {
          run();
        }
      }
    }

    const openEnabled = options.reportOpenComplete && state.open;
    if (openEnabled !== openKey) {
      openKey = openEnabled;
      openController?.abort();
      openController = null;
      if (openEnabled) {
        const controller = new AbortController();
        openController = controller;
        openWatcher.run(completeOpen, controller.signal);
      }
    }
  };

  const pass = (state: TransitionState) => {
    writeAttributes(state);
    writeHidden(state);
    report(state);
    armWatchers(state);
  };

  const onCommit = (state: TransitionState, previous: TransitionState) => {
    if (state.open && state.status === 'starting' && previous.status !== 'starting') {
      // The opening pass consumes a reveal's one-shot skip, whatever the motion.
      forcedIdle = reveal?.consumeSkip() ?? false;
    } else if (state.status !== 'starting') {
      forcedIdle = false;
    }
    if (ready) {
      pass(state);
    }
  };

  const request = (event: Event): RevealDecision => {
    const verdict = decide(twin, REVEAL_POINT, event, 'run');
    if (twin.lastDecisionWasSynchronous) {
      return { proceed: verdict === 'run', answer: null };
    }
    // No C# chain ran inside the dispatch: the request reaches C# as an invocation whose result says whether the part
    // is open once it ran; a failed invocation (the circuit is gone) counts as not open.
    const answer = twinReference
      .invokeMethodAsync<boolean>('OnRevealRequest', JSON.stringify({ event: eventInfo(event) }))
      .then((open) => open === true);
    return { proceed: verdict === 'run', answer };
  };

  // With a selector the applied DOM is the input: it is never older than an `update`, which may arrive after a newer
  // batch on Server.
  const openInput = () => (options.openSelector ? element.matches(options.openSelector) : options.open);

  const applyOpen = () => {
    machine.setOpen(openInput());
    writeHidden(machine);
  };

  let stopObserving: (() => void) | null = null;
  let observedSelector: string | null = null;
  const applyOpenSelector = () => {
    const selector = options.openSelector ?? null;
    if (selector === observedSelector) {
      return;
    }
    stopObserving?.();
    stopObserving = null;
    observedSelector = selector;
    if (selector !== null) {
      stopObserving = observeOpenInput(element, selector, () => {
        if (!disposed) {
          applyOpen();
        }
      });
    }
  };

  const applyStyle = () => {
    if (options.style != null) {
      applyConsumerStyle(element, options);
    }
  };

  const applyReveal = () => {
    if (options.revealOnBeforeMatch && reveal === null) {
      reveal = createRevealController(element, {
        get open() {
          return machine.open;
        },
        request,
      });
    } else if (!options.revealOnBeforeMatch && reveal !== null) {
      reveal.dispose();
      reveal = null;
    }
  };

  const machine = createTransitionMachine(
    { open: options.open, mounted: options.mounted, status: statusFromLiteral(options.status) },
    { enableIdleState: options.enableIdleState, deferEndingState: options.deferEndingState },
    onCommit,
  );

  applyStyle();
  applyReveal();
  ready = true;
  pass(machine);
  applyOpenSelector();
  applyOpen();

  const handle: TwinHandle = {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<TransitionsOptions>(nextOptionsJson);
      applyStyle();
      applyReveal();
      applyOpenSelector();
      if (options.unmountRequests > unmountRequests) {
        unmountRequests = options.unmountRequests;
        // A request that arrives after a reopen is dropped.
        if (!openInput()) {
          machine.setMounted(false);
        }
      }
      applyOpen();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete element[attachedHandle];
      machine.dispose();
      closeController?.abort();
      openController?.abort();
      closeDelay.cancel();
      closeWatcher.cancel();
      openWatcher.cancel();
      reveal?.dispose();
      reveal = null;
      stopObserving?.();
      stopObserving = null;
      hiddenController.dispose();
    },
  };
  element[attachedHandle] = handle;
  return handle;
}
