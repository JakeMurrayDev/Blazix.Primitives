// The `collapsible` component module (ADR 04 §1, placement rule of 2026-10-18: a same-task measurement): the
// Collapsible panel's measurement passes interleaved with the transition flips, composed from the `transitions`
// module's factories and `dom`'s reads. Accordion's panel (M3) attaches the same module.
// Transcribed from Base UI v1.9.0: collapsible/panel/useCollapsiblePanel.ts (the layout effect at lines 149-282, the
// open-side completion at 284-300, the closing sequence at 302-353, the until-found write at 355-366, the reveal at
// 368-459, the helpers at 480-591), collapsible/panel/CollapsiblePanel.tsx (the rendered style, 108-123).
// Requirements: docs/components/collapsible.md §9 (COL-ANI-01..30), COL-DOM-12..18, COL-STA-04/06, COL-EVT-04/05,
// COL-EDGE-03; transitions-and-mounting.md TRN-ALG-01/03/07.
//
// Port mapping:
// - The handover at attach (ADR 04 Amendments 2026-10-18): C# rendered the element's status, transition attributes and
//   style once. From `attach` this module writes `data-starting-style`, `data-ending-style`, `hidden="until-found"` and
//   the element's whole `style`: the consumer's merged string (`options.style`), the two size variables, the
//   `animation-name: none` suppression and the temporary overrides of a measurement.
// - The open input is read from the applied render batch through `openSelector` (ADR 04 Amendments 2026-10-21); Base
//   UI's layout effect is the machine's per-pass hook, so it runs after the batch and before paint on both modes.
// - Base UI's Root status is C#'s, mirrored from the Root's `transitions` twin. This module runs the same machine for
//   the panel element and reports only what the Root does not know: the forced idle of a motion-skipping open
//   (COL-STA-06), the end of the exit (`OnTransitionComplete`, which is `setMounted(false)`), the detected animation
//   type, and the reveal request.
// - A render function can withhold or replace the element (COL-ANI-15/18/19). C# then attaches by the panel's id and
//   sends `update` after every render; an update that finds the element disconnected re-resolves it by that id: a new
//   element starts a fresh session from what C# rendered, and no element ends an `'ending'` close at once.
import { ownerWindow } from './dom';
import { eventInfo } from './event-info';
import {
  createAnimationsWatcher,
  createFrameSlot,
  createHiddenController,
  createRevealController,
  createTransitionAttributes,
  createTransitionMachine,
  observeOpenInput,
  REVEAL_POINT,
  statusFromLiteral,
  statusToLiteral,
  type RevealController,
  type RevealDecision,
  type TransitionState,
  type TransitionStatusLiteral,
} from './transitions';
import { createTwinChannel, parseOptions, resolveElement, type DotNetTwin, type TwinChannel, type TwinHandle, type TwinOptions } from './twin';
import { decide } from './decide';

/** How the panel animates, detected from its computed style. */
export type AnimationType = 'css-transition' | 'css-animation' | 'none';

/** The panel's size: `undefined` renders `auto`. */
export interface Dimensions {
  height: number | undefined;
  width: number | undefined;
}

/** The variable that carries a Collapsible panel's measured height (`heightVariable` names another). */
export const PANEL_HEIGHT = '--collapsible-panel-height';

/** The variable that carries a Collapsible panel's measured width (`widthVariable` names another). */
export const PANEL_WIDTH = '--collapsible-panel-width';

const EMPTY_DIMENSIONS: Dimensions = { height: undefined, width: undefined };

const LAYOUT_PROPERTIES = ['justify-content', 'align-items', 'align-content', 'justify-items'] as const;

/** The options of a `collapsible` handle; mirrors `CollapsibleOptions` in C#. */
export interface CollapsibleOptions extends TwinOptions {
  /** Whether the panel is open; the input while no element can be read. */
  open: boolean;
  /** The mounted flag C# rendered the element with; read when a session starts. */
  mounted: boolean;
  /** The Root status C# rendered the element with; read when a session starts. */
  status?: TransitionStatusLiteral;
  /** Whether the element stays in the document while closed. */
  keepMounted: boolean;
  /** Whether the closed element is hidden with `hidden="until-found"`. */
  hiddenUntilFound: boolean;
  /** Whether the opening keyframe animation is suppressed (`animation-name: none`) until the panel closes; read when a session starts. */
  suppressOpenAnimation: boolean;
  /** The panel's id, by which a replaced or withheld element is found again. */
  panelId: string;
  /** The selector the element matches while C# renders it open (`[data-open]`). */
  openSelector?: string | null;
  /** The variable that carries the measured height; absent for `--collapsible-panel-height`. Read when a session starts. */
  heightVariable?: string | null;
  /** The variable that carries the measured width; absent for `--collapsible-panel-width`. Read when a session starts. */
  widthVariable?: string | null;
  /** Whether the machine's status is reported (`OnPanelStatusChange`); set by a panel whose shared state has no root twin. */
  reportStatus?: boolean | null;
}

/**
 * Returns the panel's measured size.
 * @param element The panel element.
 * @returns Its `scrollHeight` and `scrollWidth`.
 */
export function getDimensions(element: HTMLElement): { height: number; width: number } {
  return { height: element.scrollHeight, width: element.scrollWidth };
}

function hasNonZeroDuration(value: string): boolean {
  return value
    .split(',')
    .map((part) => part.trim())
    .some((part) => part !== '' && Number.parseFloat(part) > 0);
}

// COL-ANI-01/02.
/**
 * Classifies the panel's motion from its computed style.
 * @param element The panel element.
 * @param suppressedOpenAnimation Whether `animation-name: none` currently suppresses an opening keyframe animation,
 * which still counts as an animation.
 * @returns The type, and whether both a transition and an animation were found.
 */
export function getAnimationType(element: HTMLElement, suppressedOpenAnimation: boolean): { type: AnimationType; mixed: boolean } {
  const styles = ownerWindow(element).getComputedStyle(element);
  const hasAnimation =
    ((styles.animationName ?? '')
      .split(',')
      .map((name) => name.trim())
      .some((name) => name !== '' && name !== 'none') ||
      suppressedOpenAnimation) &&
    hasNonZeroDuration(styles.animationDuration ?? '');
  const hasTransition = hasNonZeroDuration(styles.transitionDuration ?? '');

  if (hasAnimation && hasTransition) {
    return { type: 'css-transition', mixed: true };
  }
  if (hasTransition) {
    return { type: 'css-transition', mixed: false };
  }
  if (hasAnimation) {
    return { type: 'css-animation', mixed: false };
  }
  return { type: 'none', mixed: false };
}

// ---------------------------------------------------------------------------------------------------------------
// The element's style, in three layers: the consumer's string, the module's declarations, the temporary overrides
// ---------------------------------------------------------------------------------------------------------------

interface Declaration {
  value: string;
  priority: string;
}

/** The inline style of a panel, which this module owns from `attach`. */
interface PanelStyle {
  /** Applies the consumer's merged style string and every declaration of the module over it. */
  setConsumer(style: string | null | undefined): void;
  /** Sets or removes one of the module's own declarations (the size variables, the suppression). */
  setBase(property: string, value: string | null): void;
  // COL-ANI-05 (the alignment reset), COL-ANI-22 (the 0s override of a reveal open).
  /**
   * Overrides a property until the returned function runs, which restores what the layers below say.
   */
  setTemporary(property: string, value: string, priority?: string): () => void;
}

function createPanelStyle(element: HTMLElement): PanelStyle {
  let consumer: string | null = null;
  // Parsed once per consumer string, to restore a property the consumer declared.
  const parsed = document.createElement('div').style;
  const base = new Map<string, Declaration>();
  const temporary = new Map<string, Declaration[]>();

  const effective = (property: string): Declaration | null => {
    const stack = temporary.get(property);
    if (stack && stack.length > 0) {
      return stack[stack.length - 1]!;
    }
    const own = base.get(property);
    if (own) {
      return own;
    }
    const value = parsed.getPropertyValue(property);
    return value === '' ? null : { value, priority: parsed.getPropertyPriority(property) };
  };

  const write = (property: string) => {
    const declaration = effective(property);
    if (declaration === null) {
      element.style.removeProperty(property);
    } else if (element.style.getPropertyValue(property) !== declaration.value || element.style.getPropertyPriority(property) !== declaration.priority) {
      element.style.setProperty(property, declaration.value, declaration.priority);
    }
  };

  return {
    setConsumer(style) {
      const next = style ?? '';
      if (consumer === next) {
        return;
      }
      consumer = next;
      parsed.cssText = next;
      element.style.cssText = next;
      for (const property of new Set([...base.keys(), ...temporary.keys()])) {
        write(property);
      }
    },
    setBase(property, value) {
      if (value === null) {
        base.delete(property);
      } else {
        base.set(property, { value, priority: '' });
      }
      write(property);
    },
    setTemporary(property, value, priority = '') {
      const declaration = { value, priority };
      const stack = temporary.get(property) ?? [];
      stack.push(declaration);
      temporary.set(property, stack);
      write(property);
      let restored = false;
      return () => {
        if (restored) {
          return;
        }
        restored = true;
        const current = temporary.get(property);
        if (!current) {
          return;
        }
        const index = current.indexOf(declaration);
        if (index >= 0) {
          current.splice(index, 1);
        }
        if (current.length === 0) {
          temporary.delete(property);
        }
        write(property);
      };
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
// One element's session
// ---------------------------------------------------------------------------------------------------------------

interface Session {
  readonly element: HTMLElement | null;
  /** Applies new options to the same element. */
  update(options: CollapsibleOptions): void;
  dispose(): void;
}

interface SessionHost {
  readonly twin: TwinChannel;
  readonly twinReference: DotNetTwin;
  /** Reports the end of an exit once per unmount. */
  completeExit(): void;
  /** Reports the forced idle when it changes. */
  reportForcedIdle(forcedIdle: boolean): void;
  /** Reports the detected type when it changes. */
  reportType(type: AnimationType, mixed: boolean): void;
  /** Reports the machine's status, before the forced idle, when it changes and the options ask for it. */
  reportStatus(status: TransitionStatusLiteral): void;
}

// The session of an attached element: Base UI's `useCollapsiblePanel` for one element.
function createElementSession(element: HTMLElement, initialOptions: CollapsibleOptions, host: SessionHost): Session {
  let options = initialOptions;
  // Accordion's panel names its own variables (ACC-DOM-17); the Collapsible ones are then never written.
  const heightVariable = options.heightVariable ?? PANEL_HEIGHT;
  const widthVariable = options.widthVariable ?? PANEL_WIDTH;
  let disposed = false;
  let ready = false;
  let animationType: AnimationType | null = null;
  let dimensions: Dimensions = EMPTY_DIMENSIONS;
  let lastMeasured: Dimensions = EMPTY_DIMENSIONS;
  let forcedIdle = false;
  // One-shot suppression of the opening keyframe animation: an initially open panel (COL-ANI-13) or a re-attached
  // one (COL-ANI-20); both end the first time the panel closes.
  let suppressOpenAnimation = options.open && options.suppressOpenAnimation;
  let pendingTemporaryRestore: (() => void) | null = null;
  // The cleanup the previous pass returned, run before the next pass (an effect's cleanup).
  let passCleanup: (() => void) | null = null;
  // Set by a closing pass that held the measured size, so the render after it flushes the element's style.
  let heldForClose = false;

  const style = createPanelStyle(element);
  const attributes = createTransitionAttributes(element);
  const hiddenController = createHiddenController(element);
  const openWatcher = createAnimationsWatcher(element, true);
  const closeWatcher = createAnimationsWatcher(element);
  const closeDelay = createFrameSlot();
  let openKey: string | null = null;
  let closeKey: string | null = null;
  let openController: AbortController | null = null;
  let closeController: AbortController | null = null;
  let reveal: RevealController | null = null;

  const setForcedIdle = (next: boolean) => {
    if (forcedIdle !== next) {
      forcedIdle = next;
      host.reportForcedIdle(next);
    }
  };

  const setDimensions = (next: Dimensions, cache = true) => {
    if (cache) {
      lastMeasured = next;
    }
    dimensions = next;
  };

  const restorePendingTemporaryStyle = () => {
    const restore = pendingTemporaryRestore;
    pendingTemporaryRestore = null;
    restore?.();
  };

  const setPendingTemporaryStyle = (property: string, value: string) => {
    restorePendingTemporaryStyle();
    pendingTemporaryRestore = style.setTemporary(property, value);
  };

  // The alignment styles that distort `scrollHeight`, neutralised for the measurement and restored a frame later.
  const resetLayoutStyles = (): (() => void) => {
    const restores = LAYOUT_PROPERTIES.map((property) => style.setTemporary(property, 'initial', 'important'));
    const restore = () => {
      for (const restoreOne of restores) {
        restoreOne();
      }
    };
    const frame = createFrameSlot();
    frame.request(restore);
    return () => {
      frame.cancel();
      restore();
    };
  };

  const shownStatus = (state: TransitionState) => (forcedIdle && state.status === 'starting' ? 'idle' : state.status);
  const isHidden = (state: TransitionState) => !state.open && !state.mounted;

  // What Base UI renders from the state: the transition attributes, `hidden`, and the style's own declarations.
  const render = (state: TransitionState) => {
    const shown = shownStatus(state);
    const persistStartingStyle = options.hiddenUntilFound && isHidden(state) && animationType !== 'css-animation';
    attributes.write(shown === 'starting' || persistStartingStyle, shown === 'ending');
    hiddenController.write(isHidden(state), options.hiddenUntilFound, true);

    // A keyframe close starts from the last measured size when the held size has been reset to `auto` (COL-ANI-12).
    const rendered =
      !state.open && state.mounted && animationType === 'css-animation' && dimensions.height === undefined && dimensions.width === undefined
        ? lastMeasured
        : dimensions;
    style.setBase(heightVariable, rendered.height === undefined ? 'auto' : `${rendered.height}px`);
    style.setBase(widthVariable, rendered.width === undefined ? 'auto' : `${rendered.width}px`);
    style.setBase('animation-name', state.open && suppressOpenAnimation ? 'none' : null);
  };

  const unmount = () => {
    setDimensions(EMPTY_DIMENSIONS, false);
    machine.setMounted(false);
  };

  // Base UI's layout effect (useCollapsiblePanel.ts:149-282), run after every commit of the machine.
  const measure = (state: TransitionState) => {
    const { open, mounted, status } = state;
    if (!open) {
      suppressOpenAnimation = false;
    }
    const preventOpenAnimation = open && suppressOpenAnimation;

    if (!open && pendingTemporaryRestore) {
      restorePendingTemporaryStyle();
    }

    const detected = getAnimationType(element, preventOpenAnimation);
    if (detected.type !== animationType || detected.mixed) {
      host.reportType(detected.type, detected.mixed);
    }
    animationType = detected.type;

    // An open panel attached in `'idle'` with a suppressed keyframe animation caches its size for the first close.
    if (open && status === 'idle' && preventOpenAnimation && detected.type === 'css-animation') {
      lastMeasured = getDimensions(element);
      return;
    }

    if (open && status === 'starting') {
      const skipNextOpen = reveal?.consumeSkip() ?? false;

      if (detected.type === 'none') {
        setDimensions(getDimensions(element));
        setForcedIdle(true);
        return;
      }

      if (detected.type === 'css-transition') {
        const restoreLayoutStyles = resetLayoutStyles();
        setDimensions(getDimensions(element));
        passCleanup = restoreLayoutStyles;
        if (skipNextOpen) {
          setPendingTemporaryStyle('transition-duration', '0s');
          setForcedIdle(true);
        }
        return;
      }

      // The keyframe pass's `animation-name: none` set and restore has no observable effect (§16 Q6) and is left out.
      setDimensions(getDimensions(element));
      if (skipNextOpen) {
        setPendingTemporaryStyle('animation-duration', '0s');
        setForcedIdle(true);
      }
      return;
    }

    // The size is captured as soon as the close is requested, before the deferred ending applies closed styles.
    if (!open && mounted && (status === 'idle' || status === 'starting')) {
      if (detected.type === 'none') {
        unmount();
        return;
      }
      setDimensions(getDimensions(element));
      heldForClose = true;
      return;
    }

    if (status !== 'ending') {
      return;
    }

    if (detected.type === 'none') {
      unmount();
      return;
    }

    const next = getDimensions(element);
    if (!(next.height > 0 || next.width > 0)) {
      unmount();
      return;
    }
    setDimensions(next);
  };

  // The completion watchers (useCollapsiblePanel.ts:284-353), re-armed when their dependencies change.
  const armWatchers = (state: TransitionState) => {
    const shown = shownStatus(state);
    const openEnabled = state.open && state.mounted && shown === 'idle';
    const nextOpenKey = `${openEnabled}`;
    if (nextOpenKey !== openKey) {
      openKey = nextOpenKey;
      openController?.abort();
      openController = null;
      if (openEnabled) {
        const controller = new AbortController();
        openController = controller;
        openWatcher.run(() => {
          // The open-side completion clears the held size only while the panel is still open (COL-ANI-17).
          if (machine.open) {
            setDimensions(EMPTY_DIMENSIONS, false);
            render(machine);
          }
        }, controller.signal);
      }
    }

    const closeEnabled = !state.open && state.mounted && shown === 'ending';
    const nextCloseKey = `${closeEnabled}|${state.open}|${state.mounted}|${shown}`;
    if (nextCloseKey !== closeKey) {
      closeKey = nextCloseKey;
      closeController?.abort();
      closeController = null;
      closeDelay.cancel();
      if (closeEnabled) {
        const controller = new AbortController();
        closeController = controller;
        // One frame first: an exit transition the browser registers late is still seen.
        closeDelay.request(() =>
          closeWatcher.run(() => {
            // A panel that reopened meanwhile is not unmounted (COL-ANI-16).
            if (!machine.open) {
              unmount();
            }
          }, controller.signal),
        );
      }
    }
  };

  const pass = (state: TransitionState) => {
    // The forced idle lasts while the shared status is still 'starting'.
    if (forcedIdle && state.status !== 'starting') {
      setForcedIdle(false);
    }
    const cleanup = passCleanup;
    passCleanup = null;
    cleanup?.();

    // The commit: the attributes and style of the new state, then the effect that measures them.
    render(state);
    measure(state);
    render(machine);
    if (heldForClose) {
      heldForClose = false;
      // The ending flip lands in the next frame's callback, before that frame computes styles, where React commits it in
      // a later task. Computing the style here makes the held pixel size the value the closing transition starts from.
      ownerWindow(element).getComputedStyle(element).getPropertyValue(heightVariable);
    }
    armWatchers(machine);
  };

  const request = (event: Event): RevealDecision => {
    const verdict = decide(host.twin, REVEAL_POINT, event, 'run');
    if (host.twin.lastDecisionWasSynchronous) {
      return { proceed: verdict === 'run', answer: null };
    }
    const answer = host.twinReference
      .invokeMethodAsync<boolean>('OnRevealRequest', JSON.stringify({ event: eventInfo(event) }))
      .then((open) => open === true);
    return { proceed: verdict === 'run', answer };
  };

  const machine = createTransitionMachine(
    { open: options.open, mounted: options.mounted, status: statusFromLiteral(options.status) },
    { enableIdleState: true, deferEndingState: true },
    (state, previous) => {
      if (!ready || disposed) {
        return;
      }
      pass(state);
      reportStatus(state, previous);
      // The end of an exit, whichever pass ended it: Base UI's setMounted(false), once per close (COL-STA-04).
      if (previous.mounted && !state.mounted && !state.open) {
        host.completeExit();
      }
    },
  );

  const openInput = () => (options.openSelector ? element.matches(options.openSelector) : options.open);

  // ACC-STA-03/09: the status of a panel whose shared state has no root twin (an Accordion Item), what C# cannot evaluate
  // itself. The unset status is implied by `OnTransitionComplete`, and the `starting` of a panel that was not mounted is
  // the evaluation step's, which C# runs in the same render (TransitionStatusRules, ADR 01 Amendments 2026-10-21).
  let knownStatus = machine.status;
  function reportStatus(state: TransitionState, previous: TransitionState): void {
    const status = state.status;
    if (status === knownStatus) {
      return;
    }
    knownStatus = status;
    if (status === undefined || (status === 'starting' && !previous.mounted) || !options.reportStatus) {
      return;
    }
    host.reportStatus(statusToLiteral(status));
  }

  style.setConsumer(options.style);
  // The listener exists whenever the element does, whatever `hiddenUntilFound` says (§16 Q9).
  reveal = createRevealController(element, {
    get open() {
      return machine.open;
    },
    request,
  });
  ready = true;
  pass({ open: machine.open, mounted: machine.mounted, status: machine.status });

  const stopObserving = options.openSelector
    ? observeOpenInput(element, options.openSelector, (open) => {
        if (!disposed) {
          machine.setOpen(open);
        }
      })
    : null;
  machine.setOpen(openInput());

  return {
    element,
    update(next) {
      if (disposed) {
        return;
      }
      const hiddenChanged = next.hiddenUntilFound !== options.hiddenUntilFound || next.keepMounted !== options.keepMounted;
      options = next;
      style.setConsumer(options.style);
      if (hiddenChanged) {
        render(machine);
      }
      machine.setOpen(openInput());
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      machine.dispose();
      openController?.abort();
      closeController?.abort();
      closeDelay.cancel();
      openWatcher.cancel();
      closeWatcher.cancel();
      reveal?.dispose();
      reveal = null;
      stopObserving?.();
      hiddenController.dispose();
      const cleanup = passCleanup;
      passCleanup = null;
      cleanup?.();
      // A pending `0s` override of a reveal is restored at once (COL-ANI-21).
      restorePendingTemporaryStyle();
    },
  };
}

// The session while no element exists (a render function withholds it). Base UI ends the close at once when the shared
// status is `'ending'` and no element is left (COL-ANI-18); the shared status is the Root's, which C# sends with every
// update of a part without an element reference, so the check runs on each update rather than on a machine of its own.
function createMissingSession(initialOptions: CollapsibleOptions, host: SessionHost): Session {
  let completed = false;
  const check = (options: CollapsibleOptions) => {
    if (!options.open && options.mounted && statusFromLiteral(options.status) === 'ending') {
      if (!completed) {
        completed = true;
        host.completeExit();
      }
    } else if (options.open || !options.mounted) {
      completed = false;
    }
  };
  check(initialOptions);

  return {
    element: null,
    update: check,
    dispose() {},
  };
}

// ---------------------------------------------------------------------------------------------------------------
// The twin-facing entry
// ---------------------------------------------------------------------------------------------------------------

// A symbol on the element, not a module registry: a repeated attach of the same element returns the live handle.
const attachedHandle = Symbol('blazix.collapsible.handle');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

function findElement(id: string): HTMLElement | null {
  const found = id ? document.getElementById(id) : null;
  return found instanceof HTMLElement ? found : null;
}

/**
 * Attaches the panel's measurement passes and transition flips to its element. From here on the module owns the
 * element's `style`, `data-starting-style`, `data-ending-style` and `hidden="until-found"`, and reports the forced idle
 * of a motion-skipping open (`OnForcedIdleChange`), the end of an exit (`OnTransitionComplete`), the detected animation
 * type (`OnAnimationTypeChange`) and, on `beforematch`, the reveal (the `reveal` decision point, or `OnRevealRequest`).
 * @param elementOrId The element, or its id when the part has no element reference.
 * @param optionsJson The `CollapsibleOptions` JSON.
 * @param twinReference The twin's object reference.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin): TwinHandle {
  const first = resolveElement(elementOrId) as AttachedElement;
  const existing = first[attachedHandle];
  if (existing) {
    return existing;
  }

  const twin = createTwinChannel(twinReference);
  let options = parseOptions<CollapsibleOptions>(optionsJson);
  let disposed = false;
  let reportedForcedIdle = false;
  let reportedType: string | null = null;

  const host: SessionHost = {
    twin,
    twinReference,
    completeExit() {
      twin.report('OnTransitionComplete', { open: false });
    },
    reportForcedIdle(forcedIdle) {
      if (forcedIdle !== reportedForcedIdle) {
        reportedForcedIdle = forcedIdle;
        twin.report('OnForcedIdleChange', { forcedIdle });
      }
    },
    reportType(type, mixed) {
      const key = `${type}|${mixed}`;
      if (key !== reportedType) {
        reportedType = key;
        twin.report('OnAnimationTypeChange', { type, mixed });
      }
    },
    reportStatus(status) {
      twin.report('OnPanelStatusChange', { status });
    },
  };

  const bind = (element: HTMLElement | null): Session => {
    if (element === null) {
      return createMissingSession(options, host);
    }
    (element as AttachedElement)[attachedHandle] = handle;
    return createElementSession(element, options, host);
  };

  const unbind = (current: Session) => {
    if (current.element) {
      delete (current.element as AttachedElement)[attachedHandle];
    }
    current.dispose();
  };

  const handle: TwinHandle = {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<CollapsibleOptions>(nextOptionsJson);
      const current = session.element;
      if (current !== null && current.isConnected) {
        session.update(options);
        return;
      }
      // The element was withheld or replaced by a render function: find the one C# rendered now, by the panel's id.
      const found = findElement(options.panelId);
      if (found === null && current === null) {
        session.update(options);
        return;
      }
      unbind(session);
      session = bind(found);
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      unbind(session);
    },
  };

  let session: Session = bind(first);
  return handle;
}
