// The `button` mechanism module (ADR 04 §1): keyboard activation through a synthetic click that keeps the key's
// modifiers (UBT-ALG-01/02/05), pointer-type classification (UBT-ALG-06), disabled press suppression (UBT-PTR-01..03),
// the focusable-when-disabled key suppression (UBT-KEY-20/21), the composite `disabled` property correction
// (UBT-ALG-04), the native-mode development check (UBT-EDGE-01..03), the press-and-hold repeater (UBT-ALG-07), the
// hidden-input bridge of Switch, Checkbox and Radio (UBT-EVT-09; ADR 12 Amendments 2026-10-22 item 4), their Enter
// suppression (UBT-KEY-06; ADR 04 Amendments 2026-10-18 item 4) and the input item of Toolbar.Input (UBT-DOM-12; ADR 04
// Amendments 2026-10-26, the `button` row).
// Transcribed from Base UI v1.9.0: internals/use-button/useButton.ts, utils/useFocusableWhenDisabled.ts,
// utils/dispatchClickWithModifiers.ts, internals/usePressAndHold.ts, packages/utils/src/useEnhancedClickHandler.ts,
// packages/utils/src/isElementDisabled.ts. Requirements: docs/architecture/button-behavior.md (UBT).
//
// Port mapping: the attribute resolution of UBT-ALG-03 is C# (rendered, also in prerender); the consumer's C#
// handlers are gated by the part. A consumer veto from JavaScript is a listener that ran before this module and called
// `preventDefault()` (checked wherever Base UI checks `defaultPrevented`); a veto from C# (`PreventPartHandler()`) is
// the verdict of the `activate` decision point, fallback `run` (ADR 04 §3, ADR 01 relaxation table). A press-and-hold
// tick is a report: its C# result is state and never crosses back synchronously (ADR 01 decision 1); C# stops the
// sequence by sending `disabled` (UBT-PTR-16).
import { decide } from './decide';
import { isHTMLElement, ownerWindow } from './dom';
import { eventInfo, type EventInfo } from './event-info';
import {
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinChannel,
  type TwinHandle,
  type TwinOptions,
} from './twin';

/** How a click happened, as the enhanced click handler classifies it. */
export type InteractionType = 'mouse' | 'touch' | 'pen' | 'keyboard' | '';

/** The press-and-hold repeater's timing; mirrors `PressAndHoldOptions` in C#. */
export interface PressAndHoldOptions {
  /** Interval between repeated ticks, in milliseconds (60 by default). */
  tickDelay?: number | null;
  /** Delay before the repeated ticks start, in milliseconds (400 by default). */
  startDelay?: number | null;
  /** Touch movement in pixels that cancels the hold as a scroll (8 by default). */
  scrollDistance?: number | null;
}

/** The options of a `button` handle; mirrors `ButtonOptions` in C#. */
export interface ButtonOptions extends TwinOptions {
  /** Whether the element ignores activation and presses. */
  disabled: boolean;
  /** Whether a disabled element stays focusable: `true`, `false`, or absent (unspecified). */
  focusableWhenDisabled?: boolean | null;
  /** Whether the element is declared a native `<button>`. */
  native: boolean;
  /** Whether the element is an item of a composite widget (Space activates on keydown). */
  composite: boolean;
  /** Turns the press-and-hold repeater on. */
  pressAndHold?: PressAndHoldOptions | null;
  /** Turns pointer-type classification reports on. */
  reportInteractionType?: boolean | null;
  /** Turns the native-mode development check on. */
  developmentChecks?: boolean | null;
  /** Turns the hidden-input bridge on: root clicks are forwarded to the input rendered after the element. */
  hiddenInput?: boolean | null;
  /** Whether the bridged input refuses user toggles: root clicks are ignored and input clicks reverted. */
  readOnly?: boolean | null;
  /** Whether an Enter keydown is prevented and never activates the element (Checkbox, Radio). */
  suppressEnter?: boolean | null;
  /** Whether the element is a toolbar input: keys never activate it and its Space is typed (Toolbar.Input). */
  inputItem?: boolean | null;
}

/** The name of the decision point a keyboard activation asks C# about. */
export const ACTIVATE_POINT = 'activate';

/** The name of the decision point a click on the bridged hidden input asks C# about. */
export const TOGGLE_POINT = 'toggle';

const DEFAULT_TICK_DELAY = 60;
const DEFAULT_START_DELAY = 400;
const DEFAULT_SCROLL_DISTANCE = 8;
const TOUCH_TIMEOUT = 50;
const MAX_POINTER_MOVES_AFTER_TOUCH = 3;

const noop = (): void => {};

// Per dispatch, like Base UI's `baseUIHandlerPrevented`: set right before a synthetic click is dispatched so another
// handle of this module on the same element (a part substituting another button-like part) does not activate again.
const activated = Symbol('blazix.button.activated');

interface MarkedEvent extends Event {
  [activated]?: true;
}

function markActivated(event: Event): void {
  (event as MarkedEvent)[activated] = true;
}

function wasActivated(event: Event): boolean {
  return (event as MarkedEvent)[activated] === true;
}

function isButtonElement(element: Element | null): element is HTMLButtonElement {
  return isHTMLElement(element) && element.tagName === 'BUTTON';
}

function isValidLinkElement(element: Element | null): element is HTMLAnchorElement {
  return isHTMLElement(element) && element.tagName === 'A' && Boolean((element as HTMLAnchorElement).href);
}

// UBT-ALG-06 (utils/useEnhancedClickHandler.ts:20-34); also the interactions module's press (design point 5 of M4 track
// 3, settled by the orchestrator 2026-10-27).
/**
 * Classifies how a click happened, as the enhanced click handler does.
 * @param event The click.
 * @param lastPointerType The pointer type of the last `pointerdown` on the element, or `''`.
 * @returns `keyboard` for a click without a click count, else the click's own pointer type (Chromium's `PointerEvent`
 * clicks), else `lastPointerType` (Safari's and Firefox's `MouseEvent` clicks).
 */
export function classifyClick(event: MouseEvent, lastPointerType: InteractionType): InteractionType {
  if (event.detail === 0) {
    return 'keyboard';
  }
  return 'pointerType' in event ? ((event as PointerEvent).pointerType as InteractionType) : lastPointerType;
}

/**
 * Determines whether a pointer type is touch-like. Pen counts as touch so a stylus tap does not force the software
 * keyboard.
 * @param pointerType The pointer type.
 * @returns `true` for `touch` and `pen`.
 */
export function isTouchLikePointerType(pointerType: string): boolean {
  return pointerType === 'touch' || pointerType === 'pen';
}

/**
 * The attribute-only disabled predicate list navigation shares.
 * @param element The element, or `null`.
 * @returns `true` for `null`, a `disabled` attribute or `aria-disabled="true"`.
 */
export function isElementDisabled(element: Element | null): boolean {
  return element == null || element.hasAttribute('disabled') || element.getAttribute('aria-disabled') === 'true';
}

/** The modifier keys a synthetic click copies. */
export interface ModifierState {
  shiftKey: boolean;
  ctrlKey: boolean;
  altKey: boolean;
  metaKey: boolean;
}

/**
 * Dispatches a constructed click on the target so it carries the source event's modifier state, which `click()`
 * always reports as unpressed. Like `click()`, the untrusted click still runs the element's activation behavior
 * (form submission, link navigation, checkbox toggling).
 * @param target The element to click.
 * @param source The event whose modifier keys the click copies.
 * @param init `detail` (0 by default) and `pointerType` (empty by default), the convention for keyboard clicks; a
 * click that stands for a mouse gesture passes `detail: 1` and `pointerType: 'mouse'`.
 */
export function dispatchClickWithModifiers(
  target: Element,
  source: ModifierState,
  { detail = 0, pointerType = '' }: { detail?: number; pointerType?: string } = {},
): void {
  target.dispatchEvent(
    new (ownerWindow(target).PointerEvent)('click', {
      bubbles: true,
      cancelable: true,
      composed: true,
      detail,
      pointerType,
      shiftKey: source.shiftKey,
      ctrlKey: source.ctrlKey,
      altKey: source.altKey,
      metaKey: source.metaKey,
    }),
  );
}

// The bridge finds the input by position, at click time (ADR 04 Amendments 2026-10-22): the part renders it as the
// element's next sibling, after the optional `<input type="hidden">` of the unchecked value (SWT-STR-01, CHK-STR-01,
// RAD-STR-01). It has no `id` under `nativeButton`, so neither an id nor an element reference names it. Under a
// `RenderFunction` callback the input follows the callback's top-level element, which may contain the part's element, as
// the docs' native radio wraps its <button> in a <label> (RAD-EDGE-09; design point R9 of M2 tracks 7-8, option (a),
// settled by the orchestrator 2026-10-23): when no input follows the element, the search moves up to each ancestor in
// turn. A sibling that is not an input stops the search at its level, so another control's input is never taken.
/**
 * Finds the hidden checkbox or radio input a bridged part renders after its element.
 * @param element The part's element.
 * @returns The first `checkbox` or `radio` input among the following siblings of the element, past `hidden` inputs, or else
 * of the nearest ancestor that has one; otherwise `null`.
 */
export function findHiddenInput(element: Element): HTMLInputElement | null {
  const InputElement = ownerWindow(element).HTMLInputElement;
  for (let level: Element | null = element; level; level = level.parentElement) {
    for (let sibling = level.nextElementSibling; sibling instanceof InputElement; sibling = sibling.nextElementSibling) {
      if (sibling.type === 'checkbox' || sibling.type === 'radio') {
        return sibling;
      }
      if (sibling.type !== 'hidden') {
        break;
      }
    }
  }
  return null;
}

interface PressAndHold {
  onPointerDown(event: PointerEvent): void;
  onPointerUp(event: PointerEvent): void;
  onPointerMove(event: PointerEvent): void;
  onMouseEnter(event: MouseEvent): void;
  onMouseLeave(): void;
  onMouseUp(): void;
  onTouchStart(): void;
  onTouchEnd(): void;
  shouldSkipClick(event: MouseEvent): boolean;
  /** Stops the sequence and forgets the press, as when the element becomes disabled. */
  reset(): void;
  dispose(): void;
}

function createPressAndHold(element: HTMLElement, twin: TwinChannel, current: () => ButtonOptions): PressAndHold {
  let pressed = false;
  let movesAfterTouch = 0;
  let down = { x: 0, y: 0 };
  let touching = false;
  let ignoreClick = false;
  let pointerType = '';
  let settleTimer: ReturnType<typeof setTimeout> | undefined;
  let startTimer: ReturnType<typeof setTimeout> | undefined;
  let repeatTimer: ReturnType<typeof setInterval> | undefined;
  let unsubscribeContextMenu = noop;
  let unsubscribePointerUp = noop;

  const timing = () => {
    const hold = current().pressAndHold ?? {};
    return {
      tickDelay: hold.tickDelay ?? DEFAULT_TICK_DELAY,
      startDelay: hold.startDelay ?? DEFAULT_START_DELAY,
      scrollDistance: hold.scrollDistance ?? DEFAULT_SCROLL_DISTANCE,
    };
  };

  // The window `pointerup` listener is kept on purpose, so a sequence stopped early still reports its release.
  const stop = () => {
    clearTimeout(settleTimer);
    clearTimeout(startTimer);
    clearInterval(repeatTimer);
    settleTimer = startTimer = repeatTimer = undefined;
    unsubscribeContextMenu();
    unsubscribeContextMenu = noop;
    movesAfterTouch = 0;
  };

  const tick = (trigger: EventInfo) => twin.report('OnHoldTick', { event: trigger });

  const begin = (trigger: Event) => {
    stop();
    const win = ownerWindow(element);

    // A long touch slightly outside the element must not open the context menu.
    const onContextMenu = (event: Event) => event.preventDefault();
    win.addEventListener('contextmenu', onContextMenu);
    unsubscribeContextMenu = () => win.removeEventListener('contextmenu', onContextMenu);

    // Replace any earlier release listener so a mouseleave/mouseenter cycle never reports the release twice.
    unsubscribePointerUp();
    const onPointerUp = (event: PointerEvent) => {
      unsubscribePointerUp = noop;
      pressed = false;
      stop();
      twin.report('OnHoldStop', { event: eventInfo(event) });
    };
    win.addEventListener('pointerup', onPointerUp, { once: true });
    unsubscribePointerUp = () => win.removeEventListener('pointerup', onPointerUp);

    const snapshot = eventInfo(trigger);
    const { startDelay, tickDelay } = timing();
    tick(snapshot);
    startTimer = setTimeout(() => {
      startTimer = undefined;
      repeatTimer = setInterval(() => tick(snapshot), tickDelay);
    }, startDelay);
  };

  return {
    onTouchStart() {
      touching = true;
    },
    onTouchEnd() {
      touching = false;
    },
    onPointerDown(event) {
      if (event.defaultPrevented || event.button !== 0 || current().disabled) {
        return;
      }
      pointerType = event.pointerType;
      ignoreClick = false;
      pressed = true;
      down = { x: event.clientX, y: event.clientY };

      if (!isTouchLikePointerType(pointerType)) {
        event.preventDefault();
        begin(event);
        return;
      }

      // A touch may start a scroll or a pinch: start the hold only once it has settled without moving.
      clearTimeout(settleTimer);
      settleTimer = setTimeout(() => {
        settleTimer = undefined;
        const moves = movesAfterTouch;
        movesAfterTouch = 0;
        if (pressed && moves < MAX_POINTER_MOVES_AFTER_TOUCH) {
          begin(event);
          // The click the browser fires after a touch hold is not a second action.
          ignoreClick = true;
        } else {
          ignoreClick = false;
          stop();
        }
      }, TOUCH_TIMEOUT);
    },
    onPointerUp(event) {
      // A quick tap released before the touch settles must not start the hold.
      if (isTouchLikePointerType(event.pointerType)) {
        pressed = false;
      }
    },
    onPointerMove(event) {
      if (current().disabled || !isTouchLikePointerType(event.pointerType) || !pressed) {
        return;
      }
      movesAfterTouch += 1;
      const dx = down.x - event.clientX;
      const dy = down.y - event.clientY;
      const { scrollDistance } = timing();
      if (dx ** 2 + dy ** 2 > scrollDistance ** 2) {
        stop();
      }
    },
    onMouseEnter(event) {
      if (event.defaultPrevented || current().disabled || !pressed || touching || isTouchLikePointerType(pointerType)) {
        return;
      }
      begin(event);
    },
    onMouseLeave() {
      if (!touching) {
        stop();
      }
    },
    // Fallback for the window `pointerup` listener, which an ancestor can keep from the window with stopPropagation.
    onMouseUp() {
      if (!touching) {
        stop();
      }
    },
    shouldSkipClick(event) {
      if (event.defaultPrevented) {
        return true;
      }
      if (isTouchLikePointerType(pointerType)) {
        return ignoreClick;
      }
      // A mouse press already acted on pointerdown; a keyboard click (detail 0) acts now.
      return event.detail !== 0;
    },
    reset() {
      pressed = false;
      touching = false;
      pointerType = '';
      stop();
    },
    dispose() {
      stop();
      unsubscribePointerUp();
      unsubscribePointerUp = noop;
    },
  };
}

/**
 * Attaches the button behavior to an element. The module writes no attribute and no style: the part renders the
 * attributes, and only the DOM `disabled` property of a composite `<button>` is corrected here.
 * @param elementOrId The element, or its id when the part has no element reference.
 * @param optionsJson The `ButtonOptions` JSON.
 * @param twinReference The twin's object reference.
 * @returns The handle with `update(optionsJson)` and `dispose()`.
 */
export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin): TwinHandle {
  const element = resolveElement(elementOrId);
  const twin = createTwinChannel(twinReference);
  let options = parseOptions<ButtonOptions>(optionsJson);
  let disposed = false;
  let lastPointerType: InteractionType = '';
  let checkedNative: boolean | undefined;
  const hold = createPressAndHold(element, twin, () => options);

  // A C# consumer's veto. Asked only where the module is about to act, so other keys cost no synchronous call.
  const vetoed = (event: Event) => decide(twin, ACTIVATE_POINT, event, 'run') !== 'run';

  // The hidden-input bridge (UBT-EVT-09; SWT-PTR-03/04/07/11, SWT-EVT-01/03/04). Every toggle is a click on the input:
  // its propagation is stopped, a click prevented before this listener is ignored (the browser reverts it), a read-only
  // click is reverted, and otherwise the toggle goes to C#. On WebAssembly the `toggle` decision runs the request inside
  // the dispatch and a refusal reverts the box natively; on Server the decision falls back to `run` and the toggle is
  // reported with the new `checked` (one message per toggle; C# restores the box through the `dom` primitive).
  let boundInput: HTMLInputElement | null = null;

  const onInputClick = (event: MouseEvent) => {
    event.stopPropagation();
    if (event.defaultPrevented) {
      return;
    }
    if (options.readOnly) {
      event.preventDefault();
      return;
    }
    const checked = (event.currentTarget as HTMLInputElement).checked;
    if (decide(twin, TOGGLE_POINT, event, 'run') !== 'run') {
      event.preventDefault();
      return;
    }
    if (!twin.lastDecisionWasSynchronous) {
      twin.report('OnToggle', { checked, event: eventInfo(event) });
    }
  };

  const bindInput = () => {
    const input = options.hiddenInput && !disposed ? findHiddenInput(element) : null;
    if (input === boundInput) {
      return;
    }
    boundInput?.removeEventListener('click', onInputClick as EventListener);
    boundInput = input;
    boundInput?.addEventListener('click', onInputClick as EventListener);
  };

  // SWT-PTR-01/02, SWT-EDGE-05: the root click's default is prevented, so an enclosing label does not click the input a
  // second time, and a click carrying its modifiers is dispatched on the input only while it is connected. A read-only
  // root click is ignored entirely; a consumer veto (`activate`) leaves it alone.
  const forwardToInput = (event: MouseEvent) => {
    if (options.readOnly || vetoed(event)) {
      return;
    }
    event.preventDefault();
    bindInput();
    if (boundInput?.isConnected) {
      dispatchClickWithModifiers(boundInput, event);
    }
  };

  const reportInteraction = (interactionType: InteractionType, event: Event) =>
    twin.report('OnInteraction', { interactionType, event: eventInfo(event) });

  // A composite <button> whose `disabled` property an inner part or a forwarded attribute set, while the part itself
  // renders no `disabled` attribute (focusable when disabled), must stay focusable.
  const correctDisabled = () => {
    if (!isButtonElement(element)) {
      return;
    }
    const partRendersDisabled = options.native && options.focusableWhenDisabled !== true;
    if (options.composite && options.disabled && !partRendersDisabled && element.disabled) {
      element.disabled = false;
    }
  };

  const checkNative = () => {
    if (!options.developmentChecks || options.native === checkedNative) {
      return;
    }
    checkedNative = options.native;
    if (options.native !== isButtonElement(element)) {
      twin.report('OnNativeButtonMismatch', { expectedNative: options.native });
    }
  };

  const onKeyDown = (event: KeyboardEvent) => {
    // The options of this dispatch: a synchronous decision may deliver an update before the handler returns.
    const { disabled, native, composite, focusableWhenDisabled, suppressEnter } = options;

    // Checkbox and Radio never activate on Enter, whatever the target and the mode (CHK-KEY-03, RAD-KEY-02).
    if (suppressEnter && event.key === 'Enter') {
      if (!event.defaultPrevented) {
        event.preventDefault();
      }
      return;
    }

    // Keep a disabled, focusable element from acting on keys, but let Tab leave it and let the key propagate.
    if (disabled && focusableWhenDisabled === true && event.key !== 'Tab') {
      event.preventDefault();
    }
    // A toolbar input is never activated by a key, and a space is typed into it (Toolbar.Input applies only the
    // focusable-when-disabled props, toolbar/input/ToolbarInput.tsx:40-45).
    if (disabled || options.inputItem || wasActivated(event)) {
      return;
    }

    const isCurrentTarget = event.target === element;
    const isButton = isButtonElement(element);
    const isLink = !native && isValidLinkElement(element);
    const shouldClick = isCurrentTarget && (native ? isButton : !isLink);
    const isEnter = event.key === 'Enter';
    const isSpace = event.key === ' ';
    const role = element.getAttribute('role');
    const isTextNavigationRole = role?.startsWith('menuitem') || role === 'option' || role === 'gridcell';

    if (isCurrentTarget && composite && isSpace) {
      // Space was consumed as typed text by typeahead or filtering.
      if (event.defaultPrevented && isTextNavigationRole) {
        return;
      }
      if (vetoed(event)) {
        return;
      }
      event.preventDefault();
      // Only a native-mode item that is not a real <button> is excluded.
      if (!native || isButton) {
        markActivated(event);
        dispatchClickWithModifiers(element, event);
      }
      return;
    }

    if (!shouldClick || native || (!isSpace && !isEnter)) {
      // Space activates a link on keyup; prevent the page scroll. Enter is the browser's native link activation.
      if (isCurrentTarget && isLink && isSpace && !vetoed(event)) {
        event.preventDefault();
      }
      return;
    }

    // Match native buttons: preventing the keydown's default cancels activation.
    if (event.defaultPrevented || vetoed(event)) {
      return;
    }
    event.preventDefault();
    if (isEnter) {
      markActivated(event);
      dispatchClickWithModifiers(element, event);
    }
  };

  const onKeyUp = (event: KeyboardEvent) => {
    const { disabled, native, composite, inputItem } = options;
    if (disabled || inputItem) {
      return;
    }
    const isCurrentTarget = event.target === element;

    // Preventing the keyup of a <button> stops the browser's own Space click; the keydown already clicked.
    if (isCurrentTarget && native && composite && isButtonElement(element) && event.key === ' ') {
      event.preventDefault();
      return;
    }
    if (wasActivated(event)) {
      return;
    }
    // No state is kept from the keydown, so a prevented Space keydown does not cancel this activation.
    if (isCurrentTarget && !native && !composite && !event.defaultPrevented && event.key === ' ') {
      if (vetoed(event)) {
        return;
      }
      markActivated(event);
      dispatchClickWithModifiers(element, event);
    }
  };

  const onClick = (event: MouseEvent) => {
    const current = options;
    if (current.disabled) {
      event.preventDefault();
      return;
    }
    if (current.reportInteractionType) {
      reportInteraction(classifyClick(event, lastPointerType), event);
      if (event.detail !== 0) {
        lastPointerType = '';
      }
    }
    if (current.pressAndHold && !hold.shouldSkipClick(event)) {
      twin.report('OnHoldClick', { event: eventInfo(event) });
    }
    if (current.hiddenInput) {
      forwardToInput(event);
    }
  };

  const onPointerDown = (event: PointerEvent) => {
    const current = options;
    if (current.disabled) {
      event.preventDefault();
      return;
    }
    if (current.reportInteractionType && !event.defaultPrevented) {
      lastPointerType = event.pointerType as InteractionType;
      reportInteraction(lastPointerType, event);
    }
    if (current.pressAndHold) {
      hold.onPointerDown(event);
    }
  };

  const whenHolding =
    <E extends Event>(handler: (event: E) => void) =>
    (event: E) => {
      if (options.pressAndHold) {
        handler(event);
      }
    };

  const listeners: [string, EventListener][] = [
    ['keydown', onKeyDown as EventListener],
    ['keyup', onKeyUp as EventListener],
    ['click', onClick as EventListener],
    ['pointerdown', onPointerDown as EventListener],
    ['pointerup', whenHolding(hold.onPointerUp) as EventListener],
    ['pointermove', whenHolding(hold.onPointerMove) as EventListener],
    ['mouseenter', whenHolding(hold.onMouseEnter) as EventListener],
    ['mouseleave', whenHolding(hold.onMouseLeave)],
    ['mouseup', whenHolding(hold.onMouseUp)],
    ['touchstart', whenHolding(hold.onTouchStart)],
    ['touchend', whenHolding(hold.onTouchEnd)],
  ];

  for (const [type, listener] of listeners) {
    element.addEventListener(type, listener);
  }
  correctDisabled();
  checkNative();
  bindInput();

  return {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      const previous = options;
      options = parseOptions<ButtonOptions>(nextOptionsJson);
      if ((options.disabled && !previous.disabled) || (!options.pressAndHold && previous.pressAndHold)) {
        hold.reset();
      }
      correctDisabled();
      checkNative();
      bindInput();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      for (const [type, listener] of listeners) {
        element.removeEventListener(type, listener);
      }
      hold.dispose();
      bindInput();
    },
  };
}
