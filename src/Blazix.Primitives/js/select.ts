// The `select` component module (ADR 04 §1's component row, its `select` row dated 2026-11-04 by M5 track 9; Amendments
// 2026-11-03 (4), `activate` on the `select` twin, chosen by the user 2026-11-03; contract docs/components/select.md).
// Four kinds of handle, one per part that owns a twin:
// - The Positioner's: aligned placement after the positioning module's first pass (SEL-POS-03..19), its fallback and the
//   latch's report, scroll-grow (SEL-POS-11, 22, 23), the scroll arrows' visibility (SEL-STA-29, SEL-POS-12), the window
//   resize close while aligned (SEL-DIS-06), and the items' press-and-release gates and click classification, delegated
//   from the Positioner (SEL-PTR-05..09, 12, 22), each activation asking `activate` (fallback run).
// - The List's: its style, which the Positioner's handle writes the aligned layout over (SEL-DOM-32).
// - Each scroll arrow's: the 40 ms auto-scroll while hovered (SEL-PTR-15..17) and its `data-visible` (SEL-DOM-44).
// - The Trigger's: the release check of the opening press (SEL-PTR-04, a 0 ms macrotask, one document `mouseup`, the 5 px
//   slip) and its blur's related target (SEL-FOC-10).
// Transcribed from Base UI v1.9.0: select/popup/SelectPopup.tsx:85-176, 195-419, 535-592; select/item/SelectItem.tsx:
// 153-236; select/scroll-arrow/SelectScrollArrow.tsx:86-235; select/trigger/SelectTrigger.tsx:104-207; select/root/
// SelectRoot.tsx:342-350. It composes `dom` (the aligned band, the scroll offsets, the pseudo-element bounds), `positioning`
// (setAligned, readPlacement), `interactions` (isVirtualClick), `button` (isElementDisabled) and `platform`.
import { isElementDisabled } from './button';
import {
  contains,
  getCssDimensions,
  getMaxScrollOffset,
  getTarget,
  isHTMLElement,
  isMouseWithinBounds,
  normalizeScrollOffset,
  ownerDocument,
  ownerWindow,
  SCROLL_EDGE_TOLERANCE_PX,
  styleWriter,
  type StyleLayer,
} from './dom';
import { decide } from './decide';
import { eventInfo } from './event-info';
import { isVirtualClick } from './interactions';
import { platform } from './platform';
import { setAligned } from './positioning';
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

/** The name of the decision point asked before an item click activates the item. */
export const ACTIVATE_POINT = 'activate';

/** The part literals of the handles. */
export const POSITIONER_PART = 'positioner';
export const LIST_PART = 'list';
export const SCROLL_ARROW_PART = 'scroll-arrow';
export const TRIGGER_PART = 'trigger';

// select/trigger/SelectTrigger.tsx:27 (SEL-PTR-05).
export const SELECTED_DELAY = 400;

// select/scroll-arrow/SelectScrollArrow.tsx:118 (SEL-PTR-15/16).
export const SCROLL_STEP_DELAY = 40;

// SEL-POS-08 (select/popup/SelectPopup.tsx:286-297).
const PADDING_X = 5;
const TRIGGER_COLLISION_THRESHOLD = 20;
const DEFAULT_MARGIN = 10;
const DEFAULT_MIN_HEIGHT = 100;

// SEL-DOM-27, SEL-DOM-32 (select/popup/utils.ts:7-12).
const LIST_FUNCTIONAL_STYLES: ReadonlyArray<readonly [string, string]> = [
  ['position', 'relative'],
  ['max-height', '100%'],
  ['overflow-x', 'hidden'],
  ['overflow-y', 'auto'],
];

// SEL-POS-19 (select/popup/SelectPopup.tsx:565-592).
const TRANSFORM_STYLE_RESETS: ReadonlyArray<readonly [string, string]> = [
  ['transform', 'none'],
  ['scale', '1'],
  ['translate', '0 0'],
];

const OPTION_SELECTOR = '[role="option"]';

/** The options of a `select` handle; mirrors `SelectOptions` in C#. */
export interface SelectOptions extends TwinOptions {
  part: typeof POSITIONER_PART | typeof LIST_PART | typeof SCROLL_ARROW_PART | typeof TRIGGER_PART;
  open?: boolean;
  mounted?: boolean;
  ending?: boolean;
  alignItemWithTrigger?: boolean;
  touchOpened?: boolean;
  highlightItemOnHover?: boolean;
  rtl?: boolean;
  hasSelectedValue?: boolean;
  selectedTextId?: string | null;
  firstTextId?: string | null;
  itemsRevision?: number;
  trigger?: number | null;
  popup?: number | null;
  list?: number | null;
  value?: number | null;
  positioner?: number | null;
  direction?: 'up' | 'down' | null;
  decides?: boolean;
}

interface Scale {
  x: number;
  y: number;
}

interface Rect {
  top: number;
  left: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
}

const positionerHandle = Symbol('blazix.select.positioner');
// Interim review B2: arrows whose handle attached before the positioner's wait here, and the positioner adopts them.
const waitingArrows = Symbol('blazix.select.waitingArrows');
const listHandle = Symbol('blazix.select.list');
const arrowHandle = Symbol('blazix.select.arrow');
const triggerHandle = Symbol('blazix.select.trigger');

interface SelectElement extends HTMLElement {
  [positionerHandle]?: PositionerState;
  [waitingArrows]?: Set<ArrowState>;
  [listHandle]?: TwinHandle;
  [arrowHandle]?: TwinHandle;
  [triggerHandle]?: TwinHandle;
}

/** The Positioner handle's state, which the arrows and the trigger of the same select read. */
interface PositionerState {
  readonly element: HTMLElement;
  readonly handle: TwinHandle;
  readonly arrows: Set<ArrowState>;
  options: SelectOptions;
  popup(): HTMLElement | null;
  list(): HTMLElement | null;
  scroller(): HTMLElement | null;
  items(): HTMLElement[];
  updateArrowVisibility(): void;
  scheduleArrowVisibility(): void;
}

interface ArrowState {
  readonly element: HTMLElement;
  readonly direction: 'up' | 'down';
}

// ---------------------------------------------------------------------------------------------------------------
// Pure helpers (exported for the L3 titles)
// ---------------------------------------------------------------------------------------------------------------

// SEL-POS-18 (floating-ui's platform.getScale, through `dom`'s CSS dimensions).
/**
 * Returns an element's rendered scale: its box size over its CSS size.
 * @param element The element.
 * @returns The horizontal and vertical scale; 1 when not measurable.
 */
export function getScale(element: HTMLElement): Scale {
  const rect = element.getBoundingClientRect();
  const { width, height } = getCssDimensions(element);
  let x = width ? rect.width / width : 1;
  let y = height ? rect.height / height : 1;
  if (!x || !Number.isFinite(x)) {
    x = 1;
  }
  if (!y || !Number.isFinite(y)) {
    y = 1;
  }
  return { x, y };
}

function normalizeRect(rect: DOMRect | DOMRectReadOnly, scale: Scale): Rect {
  const left = rect.left / scale.x;
  const top = rect.top / scale.y;
  const width = rect.width / scale.x;
  const height = rect.height / scale.y;
  return { top, left, width, height, right: left + width, bottom: top + height };
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(value, max));
}

// select/popup/SelectPopup.tsx:535-538.
/**
 * Reads the popup's maximum height: its computed `max-height` in pixels, `Infinity` for any other or a zero value.
 * @param styles The popup's computed styles.
 * @returns The maximum height.
 */
export function getMaxPopupHeight(styles: CSSStyleDeclaration): number {
  const maxHeight = styles.maxHeight;
  return maxHeight.endsWith('px') ? parseFloat(maxHeight) || Infinity : Infinity;
}

function getMaxScrollTop(scroller: HTMLElement): number {
  return getMaxScrollOffset(scroller.scrollHeight, scroller.clientHeight);
}

// SEL-STA-29 (select/root/SelectRoot.tsx:342-350).
/**
 * Computes the scroll arrows' visibility of a scroller.
 * @param scroller The scroll container.
 * @returns Whether the list can scroll up and down.
 */
export function getArrowVisibility(scroller: HTMLElement): { up: boolean; down: boolean } {
  const maxScrollTop = getMaxScrollTop(scroller);
  const scrollTop = normalizeScrollOffset(scroller.scrollTop, maxScrollTop);
  return { up: scrollTop > 0, down: scrollTop < maxScrollTop };
}

// SEL-PTR-17 (select/scroll-arrow/SelectScrollArrow.tsx:189-235).
/**
 * Computes the scroll offset one auto-scroll step of an arrow moves the list to.
 * @param items The items, in order.
 * @param isUp Whether the arrow scrolls up.
 * @param scrollTop The normalized scroll offset.
 * @param clientHeight The scroller's client height.
 * @param scrollArrowHeight The arrow's height.
 * @param maxScrollTop The largest scroll offset.
 * @returns The target offset.
 */
export function getTargetScrollTop(
  items: ReadonlyArray<HTMLElement | null>,
  isUp: boolean,
  scrollTop: number,
  clientHeight: number,
  scrollArrowHeight: number,
  maxScrollTop: number,
): number {
  if (isUp) {
    let firstVisibleIndex = 0;
    const visibleTop = scrollTop + scrollArrowHeight - SCROLL_EDGE_TOLERANCE_PX;
    for (let i = 0; i < items.length; i += 1) {
      const item = items[i];
      if (item && item.offsetTop >= visibleTop) {
        firstVisibleIndex = i;
        break;
      }
    }
    const targetIndex = Math.max(0, firstVisibleIndex - 1);
    const targetItem = items[targetIndex];
    return targetIndex < firstVisibleIndex && targetItem
      ? normalizeScrollOffset(targetItem.offsetTop - scrollArrowHeight, maxScrollTop)
      : 0;
  }

  let lastVisibleIndex = items.length - 1;
  const visibleBottom = scrollTop + clientHeight - scrollArrowHeight + SCROLL_EDGE_TOLERANCE_PX;
  for (let i = 0; i < items.length; i += 1) {
    const item = items[i];
    if (item && item.offsetTop + item.offsetHeight > visibleBottom) {
      lastVisibleIndex = Math.max(0, i - 1);
      break;
    }
  }
  const targetIndex = Math.min(items.length - 1, lastVisibleIndex + 1);
  const targetItem = items[targetIndex];
  return targetIndex > lastVisibleIndex && targetItem
    ? normalizeScrollOffset(targetItem.offsetTop + targetItem.offsetHeight - clientHeight + scrollArrowHeight, maxScrollTop)
    : maxScrollTop;
}

// SEL-POS-19: the popup's inline transform, scale and translate forced off for a measurement, restored after.
function unsetTransformStyles(popup: HTMLElement): () => void {
  const { style } = popup;
  const original = TRANSFORM_STYLE_RESETS.map(([property]) => [property, style.getPropertyValue(property), style.getPropertyPriority(property)] as const);
  for (const [property, value] of TRANSFORM_STYLE_RESETS) {
    style.setProperty(property, value, 'important');
  }
  return () => {
    for (const [property, value, priority] of original) {
      if (value) {
        style.setProperty(property, value, priority);
      } else {
        style.removeProperty(property);
      }
    }
  };
}

function writeLayer(layer: StyleLayer, declarations: ReadonlyArray<readonly [string, string]>): void {
  for (const [property, value] of declarations) {
    layer.set(property, value);
  }
}

// ---------------------------------------------------------------------------------------------------------------
// The Positioner's handle
// ---------------------------------------------------------------------------------------------------------------

function attachPositioner(element: SelectElement, initial: SelectOptions, twinReference: DotNetTwin, initialReferences: TwinReferences, initialCaptures: TwinCaptures): TwinHandle {
  const twin: TwinChannel = createTwinChannel(twinReference);
  let options = initial;
  let references = initialReferences;
  let captures = initialCaptures;
  let disposed = false;

  const resolve = (index: number | null | undefined): HTMLElement | null => {
    const found = resolveReference<Element>(references, index, captures);
    return isHTMLElement(found) ? found : null;
  };

  const positionerLayer = styleWriter(element).layer('aligned');
  let popupLayer: StyleLayer | null = null;
  let popupLayerElement: HTMLElement | null = null;
  let listLayer: StyleLayer | null = null;
  let listLayerElement: HTMLElement | null = null;

  // The placement flags (select/popup/SelectPopup.tsx:79-81).
  let initialPlaced = false;
  let reachedMaxHeight = false;
  let alignedSent = false;
  // SEL-POS-04 (select/positioner/SelectPositioner.tsx:70-82): the fallback holds until the unmount, so an update C# sent
  // before it latched the report re-runs nothing.
  let fellBack = false;
  let placementKey: string | null = null;
  let reportedArrows: { up: boolean; down: boolean } | null = null;
  let arrowFrame: number | null = null;

  // The press gates (SEL-PTR-05/06; SelectRoot.tsx:126-130).
  const selection = { allowSelectedMouseUp: false, allowUnselectedMouseUp: false, dragY: 0 };
  let gateTimer: ReturnType<typeof setTimeout> | null = null;
  let wasOpen = false;
  const pointerTypes = new WeakMap<Element, string>();
  const pressedItems = new WeakSet<Element>();

  const popupElement = (): HTMLElement | null => resolve(options.popup);
  const listElement = (): HTMLElement | null => resolve(options.list);
  const scrollerElement = (): HTMLElement | null => listElement() ?? popupElement();
  const items = (): HTMLElement[] => {
    const scroller = scrollerElement() ?? element;
    return Array.from(scroller.querySelectorAll<HTMLElement>(OPTION_SELECTOR));
  };

  const candidate = (): boolean => Boolean(options.mounted && options.alignItemWithTrigger && !options.touchOpened && !fellBack);

  // SEL-STA-29: the arrows' visibility, reported once per change and written on the attached arrows. While unmounted the
  // flags stay reset (the unmount clears them, `clearArrows`), as Base UI's store keeps them until the next placement.
  const updateArrowVisibility = () => {
    const scroller = scrollerElement();
    if (!scroller || !options.mounted) {
      return;
    }
    const next = getArrowVisibility(scroller);
    for (const arrow of state.arrows) {
      const visible = arrow.direction === 'up' ? next.up : next.down;
      arrow.element.toggleAttribute('data-visible', visible && !options.touchOpened);
    }
    if (reportedArrows === null || reportedArrows.up !== next.up || reportedArrows.down !== next.down) {
      reportedArrows = next;
      twin.report('OnArrowsChange', next);
    }
  };

  const syncLayers = () => {
    const popup = popupElement();
    const list = listElement();
    if (popup !== popupLayerElement) {
      popupLayer?.clear();
      popupLayerElement = popup;
      popupLayer = popup ? styleWriter(popup).layer('aligned') : null;
    }
    if (list !== listLayerElement) {
      listLayer?.clear();
      listLayerElement = list;
      listLayer = list ? styleWriter(list).layer('aligned') : null;
    }
  };

  // SEL-DOM-27, SEL-DOM-32: the aligned layout of the popup and the list, while aligned placement is active.
  const writeLayout = (active: boolean) => {
    syncLayers();
    if (!active) {
      popupLayer?.clear();
      listLayer?.clear();
      return;
    }
    if (listLayer) {
      popupLayer?.set('height', '100%');
      writeLayer(listLayer, LIST_FUNCTIONAL_STYLES);
    } else if (popupLayer) {
      writeLayer(popupLayer, LIST_FUNCTIONAL_STYLES);
    }
  };

  // SEL-POS-04, SEL-POS-13 (clearStyles): the positioner's original styles are what its other writers hold, so a reset
  // clears the band.
  const resetPlacement = () => {
    positionerLayer.clear();
    popupLayer?.remove('--transform-origin');
    initialPlaced = false;
    reachedMaxHeight = false;
    placementKey = null;
  };

  // SEL-POS-07 (select/popup/SelectPopup.tsx:227-419).
  const place = () => {
    const trigger = resolve(options.trigger);
    const popup = popupElement();
    if (disposed || !options.open || !trigger || !popup || options.ending) {
      return;
    }

    const list = listElement();
    const key = JSON.stringify([trigger.id, list !== null, options.highlightItemOnHover, options.rtl, options.itemsRevision ?? 0, options.selectedTextId, options.firstTextId]);
    if (initialPlaced && key === placementKey) {
      return;
    }
    placementKey = key;
    initialPlaced = true;
    popupLayer?.remove('--transform-origin');
    const restoreTransformStyles = unsetTransformStyles(popup);
    try {
      const doc = ownerDocument(trigger);
      let textElement = options.selectedTextId ? doc.getElementById(options.selectedTextId) : null;
      if (!textElement?.isConnected) {
        textElement = !options.hasSelectedValue && options.firstTextId ? doc.getElementById(options.firstTextId) : null;
      }
      const valueElement = resolve(options.value);

      const win = ownerWindow(element);
      const positionerStyles = win.getComputedStyle(element);
      const popupStyles = win.getComputedStyle(popup);
      const scale = getScale(trigger);
      const triggerRect = normalizeRect(trigger.getBoundingClientRect(), scale);
      const positionerRect = normalizeRect(element.getBoundingClientRect(), scale);
      const triggerHeight = triggerRect.height;
      const scroller = list ?? popup;
      const scrollHeight = scroller.scrollHeight;
      const borderBottom = parseFloat(popupStyles.borderBottomWidth);
      const marginTop = parseFloat(positionerStyles.marginTop) || DEFAULT_MARGIN;
      const marginBottom = parseFloat(positionerStyles.marginBottom) || DEFAULT_MARGIN;
      const minHeight = parseFloat(positionerStyles.minHeight) || DEFAULT_MIN_HEIGHT;
      const maxPopupHeight = getMaxPopupHeight(popupStyles);
      const viewportHeight = doc.documentElement.clientHeight - marginTop - marginBottom;
      const viewportWidth = doc.documentElement.clientWidth;
      const availableSpaceBeneathTrigger = viewportHeight - triggerRect.bottom + triggerHeight;

      let textRect: Rect | undefined;
      let alignedLeft = options.rtl ? triggerRect.right - positionerRect.width : triggerRect.left;
      let offsetY = 0;
      if (textElement && valueElement) {
        const valueRect = normalizeRect(valueElement.getBoundingClientRect(), scale);
        textRect = normalizeRect(textElement.getBoundingClientRect(), scale);
        alignedLeft = positionerRect.left + (options.rtl ? valueRect.right - textRect.right : valueRect.left - textRect.left);
        const valueCenterFromTriggerTop = valueRect.top - triggerRect.top + valueRect.height / 2;
        const textCenterFromPositionerTop = textRect.top - positionerRect.top + textRect.height / 2;
        offsetY = textCenterFromPositionerTop - valueCenterFromTriggerTop;
      }

      const idealHeight = availableSpaceBeneathTrigger + offsetY + marginBottom + borderBottom;
      let height = Math.min(viewportHeight, idealHeight);
      const maxHeight = viewportHeight - marginTop - marginBottom;
      const scrollTop = idealHeight - height;
      const maxRight = viewportWidth - PADDING_X;

      positionerLayer.set('left', `${clamp(alignedLeft, PADDING_X, maxRight - positionerRect.width)}px`);
      positionerLayer.set('height', `${height}px`);
      positionerLayer.set('max-height', 'none');
      positionerLayer.set('margin-top', `${marginTop}px`);
      positionerLayer.set('margin-bottom', `${marginBottom}px`);
      popupLayer?.set('height', '100%');

      const maxScrollTop = getMaxScrollTop(scroller);
      const isTopPositioned = scrollTop >= maxScrollTop - SCROLL_EDGE_TOLERANCE_PX;
      if (isTopPositioned) {
        height = Math.min(viewportHeight, positionerRect.height) - (scrollTop - maxScrollTop);
      }

      // SEL-POS-04: the fallback.
      const fallback =
        triggerRect.top < TRIGGER_COLLISION_THRESHOLD ||
        triggerRect.bottom > viewportHeight - TRIGGER_COLLISION_THRESHOLD ||
        Math.ceil(height) + SCROLL_EDGE_TOLERANCE_PX < Math.min(scrollHeight, minHeight);
      const isPinchZoomed = (win.visualViewport?.scale ?? 1) !== 1 && platform.engine.webkit;
      if (fallback || isPinchZoomed) {
        fellBack = true;
        stopAligned();
        twin.report('OnAlignedChange', { aligned: false });
        return;
      }

      const initialHeight = Math.max(minHeight, height);
      if (isTopPositioned) {
        const topOffset = Math.max(0, viewportHeight - idealHeight);
        positionerLayer.set('top', positionerRect.height >= maxHeight ? '0' : `${topOffset}px`);
        positionerLayer.remove('bottom');
        positionerLayer.set('height', `${height}px`);
        scroller.scrollTop = getMaxScrollTop(scroller);
      } else {
        positionerLayer.remove('top');
        positionerLayer.set('bottom', '0');
        scroller.scrollTop = scrollTop;
      }

      // SEL-DOM-29.
      if (textRect) {
        const popupTop = positionerRect.top;
        const popupHeight = positionerRect.height;
        const textCenterY = textRect.top + textRect.height / 2;
        const clampedY = clamp(popupHeight > 0 ? ((textCenterY - popupTop) / popupHeight) * 100 : 50, 0, 100);
        popupLayer?.set('--transform-origin', `50% ${clampedY}%`);
      }

      if (initialHeight === viewportHeight || height >= maxPopupHeight) {
        reachedMaxHeight = true;
      }

      updateArrowVisibility();

      // SEL-POS-10: C# asks list navigation for the first item when nothing is selected or highlighted.
      twin.report('OnPlaced', {});
    } finally {
      restoreTransformStyles();
    }
  };

  // A positioning handle not attached yet answers false; the next update tries again.
  const startAligned = () => {
    if (alignedSent) {
      return;
    }
    writeLayout(true);
    alignedSent = setAligned(element, true, place);
  };

  const stopAligned = () => {
    if (!alignedSent) {
      writeLayout(false);
      return;
    }
    alignedSent = false;
    setAligned(element, false);
    writeLayout(false);
    resetPlacement();
  };

  // SEL-POS-11 (select/popup/SelectPopup.tsx:85-176).
  const handleScroll = (scroller: HTMLElement) => {
    if (!initialPlaced) {
      return;
    }
    const isTopPositioned = element.style.top === '0px';
    const isBottomPositioned = element.style.bottom === '0px';
    if (reachedMaxHeight || !alignedSent || (!isTopPositioned && !isBottomPositioned)) {
      updateArrowVisibility();
      return;
    }

    const scale = getScale(element);
    const currentHeight = element.getBoundingClientRect().height / scale.y;
    const doc = ownerDocument(element);
    const win = ownerWindow(element);
    const positionerStyles = win.getComputedStyle(element);
    const marginTop = parseFloat(positionerStyles.marginTop);
    const marginBottom = parseFloat(positionerStyles.marginBottom);
    const popup = popupElement();
    const maxPopupHeight = popup ? getMaxPopupHeight(win.getComputedStyle(popup)) : Infinity;
    const maxAvailableHeight = Math.min(doc.documentElement.clientHeight - marginTop - marginBottom, maxPopupHeight);
    const scrollTop = scroller.scrollTop;
    const maxScrollTop = getMaxScrollTop(scroller);
    let nextScrollTop: number | null = null;
    const setHeight = (value: number) => positionerLayer.set('height', `${value}px`);

    const diff = isTopPositioned ? maxScrollTop - scrollTop : scrollTop;
    const nextHeight = Math.min(currentHeight + diff, maxAvailableHeight);

    if (diff <= SCROLL_EDGE_TOLERANCE_PX) {
      const heightDelta = clamp(diff, 0, maxAvailableHeight - currentHeight);
      if (heightDelta > 0) {
        setHeight(currentHeight + heightDelta);
      }
      scroller.scrollTop = isTopPositioned ? maxScrollTop : 0;
      if (maxAvailableHeight - (currentHeight + heightDelta) <= SCROLL_EDGE_TOLERANCE_PX) {
        reachedMaxHeight = true;
      }
      updateArrowVisibility();
      return;
    }

    if (maxAvailableHeight - nextHeight > SCROLL_EDGE_TOLERANCE_PX) {
      nextScrollTop = isTopPositioned ? Infinity : 0;
    } else if (isBottomPositioned && scrollTop < maxScrollTop) {
      const overshoot = currentHeight + diff - maxAvailableHeight;
      nextScrollTop = scrollTop - (diff - overshoot);
    }

    const nextPositionerHeight = Math.ceil(nextHeight);
    if (nextPositionerHeight !== 0) {
      setHeight(nextPositionerHeight);
    }

    if (nextScrollTop != null) {
      const target = clamp(nextScrollTop, 0, getMaxScrollTop(scroller));
      if (Math.abs(scroller.scrollTop - target) > SCROLL_EDGE_TOLERANCE_PX) {
        scroller.scrollTop = target;
      }
    }

    if (nextPositionerHeight >= maxAvailableHeight - SCROLL_EDGE_TOLERANCE_PX) {
      reachedMaxHeight = true;
    }
    updateArrowVisibility();
  };

  // SEL-POS-23: the scroller is the List while one is registered, else the Popup; a scroll bubbling to the Popup from
  // the List is ignored (the listener sits on the scroller itself, scroll events do not bubble).
  let scrollTarget: HTMLElement | null = null;
  const onScroll = (event: Event) => {
    if (event.currentTarget === scrollTarget && scrollTarget !== null) {
      handleScroll(scrollTarget);
    }
  };
  const syncScrollListener = () => {
    const next = options.mounted ? scrollerElement() : null;
    if (next === scrollTarget) {
      return;
    }
    scrollTarget?.removeEventListener('scroll', onScroll);
    scrollTarget = next;
    scrollTarget?.addEventListener('scroll', onScroll, { passive: true });
  };

  // SEL-DIS-06 (select/popup/SelectPopup.tsx:421-433).
  let resizeWindow: Window | null = null;
  const onResize = (event: Event) => twin.report('OnWindowResize', eventInfo(event));
  const syncResize = () => {
    const next = alignedSent && options.open ? ownerWindow(element) : null;
    if (next === resizeWindow) {
      return;
    }
    resizeWindow?.removeEventListener('resize', onResize);
    resizeWindow = next;
    resizeWindow?.addEventListener('resize', onResize);
  };

  // SEL-POS-12: outside aligned placement, the arrows' visibility on the next frame after the placement.
  const scheduleArrowVisibility = () => {
    if (arrowFrame !== null) {
      cancelAnimationFrame(arrowFrame);
    }
    arrowFrame = requestAnimationFrame(() => {
      arrowFrame = null;
      if (!disposed && options.open) {
        updateArrowVisibility();
      }
    });
  };

  // SEL-PTR-05 (select/trigger/SelectTrigger.tsx:104-129): the gates open 400 ms after the open and reset on the close.
  const syncGates = () => {
    const open = Boolean(options.open);
    if (open === wasOpen) {
      return;
    }
    wasOpen = open;
    if (gateTimer !== null) {
      clearTimeout(gateTimer);
      gateTimer = null;
    }
    selection.allowSelectedMouseUp = false;
    selection.allowUnselectedMouseUp = false;
    selection.dragY = 0;
    if (open) {
      gateTimer = setTimeout(() => {
        gateTimer = null;
        selection.allowSelectedMouseUp = true;
        selection.allowUnselectedMouseUp = true;
      }, SELECTED_DELAY);
    }
  };

  // The item an event is about: an option inside this Positioner.
  const itemOf = (event: Event): HTMLElement | null => {
    const target = getTarget(event);
    const found = target instanceof Element ? target.closest<HTMLElement>(OPTION_SELECTOR) : null;
    return found && contains(element, found) ? found : null;
  };

  // SEL-PTR-08, SEL-PTR-11, SEL-PTR-12: `activate` on WebAssembly runs the item's consumer onclick and the commit inside
  // the click; on Server the activation is reported.
  const activate = (item: HTMLElement, event: MouseEvent) => {
    if (twin.synchronous) {
      twinReference.invokeMethod<void>('OnActivating', JSON.stringify({ itemId: item.id }));
      decide(twin, ACTIVATE_POINT, event, 'run');
      return;
    }
    decide(twin, ACTIVATE_POINT, event, 'run');
    twin.report('OnActivate', { itemId: item.id, event: eventInfo(event) });
  };

  const onPointerOver = (event: PointerEvent) => {
    const item = itemOf(event);
    if (item) {
      pointerTypes.set(item, event.pointerType);
    }
  };

  const onPointerDown = (event: PointerEvent) => {
    const item = itemOf(event);
    if (!item) {
      return;
    }
    pointerTypes.set(item, event.pointerType);
    pressedItems.add(item);
    selection.dragY = 0;
  };

  // SEL-PTR-06.
  const onPointerMove = (event: PointerEvent) => {
    if (!itemOf(event) || event.pointerType !== 'mouse' || event.buttons !== 1) {
      return;
    }
    selection.dragY += event.movementY;
    if (selection.dragY ** 2 >= 64) {
      selection.allowUnselectedMouseUp = true;
    }
  };

  // SEL-PTR-07, SEL-PTR-09, SEL-PTR-22.
  const onMouseUp = (event: MouseEvent) => {
    const item = itemOf(event);
    if (!item) {
      return;
    }
    selection.dragY = 0;
    if (isElementDisabled(item) || pointerTypes.get(item) === 'touch' || pressedItems.has(item)) {
      return;
    }
    const selected = item.getAttribute('aria-selected') === 'true';
    if ((!selection.allowSelectedMouseUp && selected) || (!selection.allowUnselectedMouseUp && !selected)) {
      return;
    }
    pressedItems.add(item);
    item.click();
    pressedItems.delete(item);
  };

  // SEL-PTR-08: the click classification.
  // SEL-PTR-08 (SelectItem.tsx:212-234): Base UI reads the item's highlight as last rendered, before this click. The
  // `composite` module's own click listener, on the popup inside this element, focuses and highlights the item first,
  // so the highlight is recorded in the capture phase, before any of the click's listeners run.
  let highlightedAtClick: Element | null = null;
  const onClickCapture = (event: MouseEvent) => {
    const item = itemOf(event);
    highlightedAtClick = item?.hasAttribute('data-highlighted') ? item : null;
  };

  const onClick = (event: MouseEvent) => {
    const item = itemOf(event);
    const wasHighlighted = item !== null && highlightedAtClick === item;
    highlightedAtClick = null;
    if (!item) {
      return;
    }
    const isMouseClick = pointerTypes.get(item) !== 'touch';
    const clickPointerType = (event as PointerEvent).pointerType;
    const isVirtualMouseClick = isMouseClick && isVirtualClick(event) && (clickPointerType !== undefined || wasHighlighted);
    const isInvalidMouseClick = isMouseClick && !isVirtualMouseClick && !pressedItems.has(item);
    pressedItems.delete(item);
    if (isElementDisabled(item) || isInvalidMouseClick) {
      return;
    }
    activate(item, event);
  };

  element.addEventListener('pointerover', onPointerOver);
  element.addEventListener('pointerdown', onPointerDown);
  element.addEventListener('pointermove', onPointerMove);
  element.addEventListener('mouseup', onMouseUp);
  element.addEventListener('click', onClickCapture, true);
  element.addEventListener('click', onClick);

  const sync = () => {
    syncGates();
    syncLayers();
    if (candidate() && options.open && !options.ending) {
      startAligned();
      if (alignedSent && initialPlaced) {
        // An input changed while placed: SEL-POS-09, SEL-POS-13 (cleared heights before the next placement).
        place();
      }
    } else if (!candidate()) {
      stopAligned();
      // SelectPopup.tsx:217-246: an anchored open is placed once the popup shows (scroll events then update the arrows),
      // and its flags reset on the close.
      if (options.open && !options.ending) {
        initialPlaced = true;
        scheduleArrowVisibility();
      } else if (!options.open) {
        initialPlaced = false;
        reachedMaxHeight = false;
      }
    }
    if (!options.mounted) {
      // SEL-STA-29 (SelectRoot.tsx:206-214): the unmount resets both flags; C# resets its own in the same write, and a kept
      // arrow (a force-mounted portal) keeps no `data-visible` from the last session.
      reportedArrows = null;
      fellBack = false;
      for (const arrow of state.arrows) {
        arrow.element.removeAttribute('data-visible');
      }
    }
    syncScrollListener();
    syncResize();
  };

  let previousItemsRevision = options.itemsRevision ?? 0;
  const handle: TwinHandle = {
    update(nextJson, nextReferences, nextCaptures) {
      if (disposed) {
        return;
      }
      options = parseOptions<SelectOptions>(nextJson);
      references = nextReferences;
      captures = nextCaptures;
      state.options = options;
      // SEL-POS-13: the item map changed while open and aligned: cleared heights before the next placement.
      const itemsRevision = options.itemsRevision ?? 0;
      if (itemsRevision !== previousItemsRevision) {
        previousItemsRevision = itemsRevision;
        if (alignedSent && options.open) {
          positionerLayer.remove('height');
          popupLayer?.remove('height');
          reportedArrows = null;
          twin.report('OnArrowsChange', { up: false, down: false });
        }
      }
      sync();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      if (gateTimer !== null) {
        clearTimeout(gateTimer);
      }
      if (arrowFrame !== null) {
        cancelAnimationFrame(arrowFrame);
      }
      element.removeEventListener('pointerover', onPointerOver);
      element.removeEventListener('pointerdown', onPointerDown);
      element.removeEventListener('pointermove', onPointerMove);
      element.removeEventListener('mouseup', onMouseUp);
      element.removeEventListener('click', onClickCapture, true);
      element.removeEventListener('click', onClick);
      scrollTarget?.removeEventListener('scroll', onScroll);
      resizeWindow?.removeEventListener('resize', onResize);
      if (alignedSent) {
        setAligned(element, false);
      }
      positionerLayer.clear();
      popupLayer?.clear();
      listLayer?.clear();
      delete element[positionerHandle];
    },
  };

  const state: PositionerState = {
    element,
    handle,
    arrows: new Set(),
    options,
    popup: popupElement,
    list: listElement,
    scroller: scrollerElement,
    items,
    updateArrowVisibility,
    scheduleArrowVisibility,
  };
  element[positionerHandle] = state;
  const waiting = element[waitingArrows];
  if (waiting) {
    delete element[waitingArrows];
    for (const arrow of waiting) {
      state.arrows.add(arrow);
    }
    updateArrowVisibility();
    scheduleArrowVisibility();
  }
  sync();
  return handle;
}

// ---------------------------------------------------------------------------------------------------------------
// The List's handle (SEL-DOM-32): the consumer's style under the aligned layout the Positioner's handle writes.
// ---------------------------------------------------------------------------------------------------------------

function attachList(element: SelectElement, initial: SelectOptions): TwinHandle {
  const writer = styleWriter(element);
  writer.setConsumer(initial.style);
  const handle: TwinHandle = {
    update(nextJson) {
      writer.setConsumer(parseOptions<SelectOptions>(nextJson).style);
    },
    dispose() {
      delete element[listHandle];
    },
  };
  element[listHandle] = handle;
  return handle;
}

// ---------------------------------------------------------------------------------------------------------------
// A scroll arrow's handle (SEL-PTR-15..17)
// ---------------------------------------------------------------------------------------------------------------

function attachScrollArrow(element: SelectElement, initial: SelectOptions, twinReference: DotNetTwin, references: TwinReferences, captures: TwinCaptures): TwinHandle {
  const twin = createTwinChannel(twinReference);
  const positionerElement = resolveReference<SelectElement>(references, initial.positioner, captures);
  const arrow: ArrowState = { element, direction: initial.direction === 'down' ? 'down' : 'up' };
  const isUp = arrow.direction === 'up';
  // An arrow's handle can attach before the positioner's: the positioner is resolved on each use until it has a handle,
  // and the arrow joins it then. Its visibility is also read again on the next frame, once the scroller has its layout.
  let positioner: PositionerState | null = null;
  const resolvePositioner = (): PositionerState | null => {
    if (positioner === null && !disposed) {
      positioner = positionerElement?.[positionerHandle] ?? null;
      if (positioner !== null) {
        positioner.arrows.add(arrow);
        positioner.updateArrowVisibility();
        positioner.scheduleArrowVisibility();
      }
    }
    return positioner;
  };
  let timer: ReturnType<typeof setTimeout> | null = null;
  let disposed = false;
  if (resolvePositioner() === null && positionerElement) {
    (positionerElement[waitingArrows] ??= new Set()).add(arrow);
  }

  const clear = () => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
  };

  // SEL-PTR-16.
  const scrollNextItem = () => {
    timer = null;
    const current = resolvePositioner();
    const scroller = current?.scroller();
    if (disposed || !current || !scroller) {
      return;
    }
    twin.report('OnClearHighlight', {});
    current.updateArrowVisibility();
    const maxScrollTop = getMaxScrollOffset(scroller.scrollHeight, scroller.clientHeight);
    const scrollTop = normalizeScrollOffset(scroller.scrollTop, maxScrollTop);
    const isScrolledToEdge = scrollTop === (isUp ? 0 : maxScrollTop);
    const itemElements = current.items();
    if (scrollTop !== scroller.scrollTop) {
      scroller.scrollTop = scrollTop;
    }
    if (isScrolledToEdge) {
      return;
    }
    if (itemElements.length > 0) {
      scroller.scrollTop = getTargetScrollTop(itemElements, isUp, scrollTop, scroller.clientHeight, element.offsetHeight || 0, maxScrollTop);
    }
    timer = setTimeout(scrollNextItem, SCROLL_STEP_DELAY);
  };

  // SEL-PTR-15.
  const onMouseMove = (event: MouseEvent) => {
    if ((event.movementX === 0 && event.movementY === 0) || timer !== null) {
      return;
    }
    twin.report('OnClearHighlight', {});
    timer = setTimeout(scrollNextItem, SCROLL_STEP_DELAY);
  };

  element.addEventListener('mousemove', onMouseMove);
  element.addEventListener('mouseleave', clear);

  const handle: TwinHandle = {
    update() {
      resolvePositioner()?.updateArrowVisibility();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      clear();
      element.removeEventListener('mousemove', onMouseMove);
      element.removeEventListener('mouseleave', clear);
      positioner?.arrows.delete(arrow);
      positionerElement?.[waitingArrows]?.delete(arrow);
      delete element[arrowHandle];
    },
  };
  element[arrowHandle] = handle;
  return handle;
}

// ---------------------------------------------------------------------------------------------------------------
// The Trigger's handle (SEL-PTR-04, SEL-FOC-10)
// ---------------------------------------------------------------------------------------------------------------

function attachTrigger(element: SelectElement, initial: SelectOptions, twinReference: DotNetTwin, initialReferences: TwinReferences, initialCaptures: TwinCaptures): TwinHandle {
  const twin = createTwinChannel(twinReference);
  let options = initial;
  let references = initialReferences;
  let captures = initialCaptures;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let listening: Document | null = null;
  let disposed = false;

  const positionerElement = (): HTMLElement | null => {
    const found = resolveReference<Element>(references, options.positioner, captures);
    return isHTMLElement(found) ? found : null;
  };

  const stop = () => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
    listening?.removeEventListener('mouseup', onMouseUp);
    listening = null;
  };

  // select/trigger/SelectTrigger.tsx:179-201.
  const onMouseUp = (event: MouseEvent) => {
    listening = null;
    if (disposed) {
      return;
    }
    const target = getTarget(event);
    const targetElement = target instanceof Element ? target : null;
    if (contains(element, targetElement) || contains(positionerElement(), targetElement)) {
      return;
    }
    if (isMouseWithinBounds(event, element)) {
      return;
    }
    twin.report('OnCancelOpen', eventInfo(event));
  };

  // A press on the closed trigger: after a 0 ms macrotask (a Firefox mouseup during the mousedown), the next mouseup.
  const onMouseDown = () => {
    if (options.open) {
      return;
    }
    stop();
    const doc = ownerDocument(element);
    timer = setTimeout(() => {
      timer = null;
      if (!disposed) {
        listening = doc;
        doc.addEventListener('mouseup', onMouseUp, { once: true });
      }
    }, 0);
  };

  // SEL-FOC-10: a blur into the positioner is no blur of the field.
  const onBlur = (event: FocusEvent) => {
    const related = event.relatedTarget instanceof Element ? event.relatedTarget : null;
    twin.report('OnTriggerBlur', { intoPopup: contains(positionerElement(), related) });
  };

  element.addEventListener('mousedown', onMouseDown);
  element.addEventListener('blur', onBlur);

  let wasOpen = Boolean(options.open);
  const handle: TwinHandle = {
    update(nextJson, nextReferences, nextCaptures) {
      options = parseOptions<SelectOptions>(nextJson);
      references = nextReferences;
      captures = nextCaptures;
      // SEL-PTR-04: the pending listener is cleared when the popup closes.
      const open = Boolean(options.open);
      if (wasOpen && !open) {
        stop();
      }
      wasOpen = open;
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      stop();
      element.removeEventListener('mousedown', onMouseDown);
      element.removeEventListener('blur', onBlur);
      delete element[triggerHandle];
    },
  };
  element[triggerHandle] = handle;
  return handle;
}

// ---------------------------------------------------------------------------------------------------------------
// The entry point
// ---------------------------------------------------------------------------------------------------------------

/**
 * Attaches the module to a select part's element.
 * @param elementOrId The part's element, or its id.
 * @param optionsJson The `SelectOptions` JSON.
 * @param twinReference The twin's object reference.
 * @param references The elements the options name by index.
 * @param captures The capture ids of the references.
 * @returns The handle; the live handle when the element is already attached in the same part.
 */
export function attach(
  elementOrId: Element | string | null,
  optionsJson: string,
  twinReference: DotNetTwin,
  references?: TwinReferences,
  captures?: TwinCaptures,
): TwinHandle {
  const element = resolveElement(elementOrId, captures?.[0]) as SelectElement;
  const options = parseOptions<SelectOptions>(optionsJson);
  switch (options.part) {
    case LIST_PART:
      return element[listHandle] ?? attachList(element, options);
    case SCROLL_ARROW_PART:
      return element[arrowHandle] ?? attachScrollArrow(element, options, twinReference, references, captures);
    case TRIGGER_PART:
      return element[triggerHandle] ?? attachTrigger(element, options, twinReference, references, captures);
    default:
      return element[positionerHandle]?.handle ?? attachPositioner(element, options, twinReference, references, captures);
  }
}
