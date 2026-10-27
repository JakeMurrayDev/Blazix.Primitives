// The `scroll-lock` mechanism module (ADR 04 §1; ADR 04 Amendments 2026-10-27, the scroll-lock bullet, chosen by the user
// 2026-10-27): Base UI's useScrollLock, transcribed from packages/utils/src/useScrollLock.ts at v1.9.0. Requirements:
// shared-utilities.md §4.8 (UTL-DOM-01..12), UTL-ALG-01 (acquisition and release, page-global, reference-counted, deferred
// by a 0 ms timer) and UTL-ALG-02 (the inset-scrollbar lock and its resize relock on the next frame).
// - The lock count is page-global, as in Base UI: sanctioned page-global state (ADR 04 Amendments 2026-10-27), named in
//   js/CONTEXT.md, with the test reset `resetScrollLockForTest`.
// - A handle attaches to the popup's reference element (the element whose document is locked, UTL-DOM-11) and holds one
//   acquisition while `enabled`; `update` carries `enabled` (on Server a round trip after the commit: the "JS-owned values
//   that originate in C# state" row of ADR 01, the dated note on UTL-DOM-12). There is no report and no decision point.
// - The page-level writes (inline styles on <html> and <body>, `data-base-ui-scroll-locked` on <html>) are recorded in
//   js/CONTEXT.md and ADR 04 §2's note.
import { isOverflowElement, ownerDocument, ownerWindow } from './dom';
import { platform } from './platform';
import { cancelFrame, requestFrame } from './transitions';
import { parseOptions, resolveElement, type DotNetTwin, type TwinHandle, type TwinOptions } from './twin';

/** The options of a `scroll-lock` handle; mirrors `ScrollLockOptions` in C#. */
export interface ScrollLockOptions extends TwinOptions {
  /** Whether the handle holds a lock on its element's document. */
  enabled: boolean;
}

// UTL-DOM-08.
/** The attribute the full inset path writes on `<html>` while locked. */
export const SCROLL_LOCKED_ATTRIBUTE = 'data-base-ui-scroll-locked';

type StyleSnapshot = Record<string, string>;

const noop = (): void => {};

// The viewport's overflow comes from <html> when it establishes its own scroll container and propagates from <body>
// otherwise (UTL-DOM-04).
function getViewportScroller(html: HTMLElement, body: HTMLElement): HTMLElement {
  return isOverflowElement(html) ? html : body;
}

function isPageScrollLocked(win: Window, html: HTMLElement, body: HTMLElement): boolean {
  return /hidden|clip/.test(win.getComputedStyle(getViewportScroller(html, body)).overflowY);
}

// UTL-DOM-05, UTL-DOM-11: the window of a document node is the global window (a document has no ownerDocument).
function hasInsetScrollbars(referenceElement: Element | null): boolean {
  const doc = ownerDocument(referenceElement);
  const win = ownerWindow(doc);
  return win.innerWidth - doc.documentElement.clientWidth > 0;
}

function snapshot(style: CSSStyleDeclaration, properties: readonly string[]): StyleSnapshot {
  const values: StyleSnapshot = {};
  for (const property of properties) {
    values[property] = style.getPropertyValue(property);
  }
  return values;
}

function restore(style: CSSStyleDeclaration, values: StyleSnapshot): void {
  for (const [property, value] of Object.entries(values)) {
    if (value) {
      style.setProperty(property, value);
    } else {
      style.removeProperty(property);
    }
  }
}

// UTL-DOM-07: a probe with `scrollbar-gutter: stable` on <html>: the scroller's width is the same with a scrollbar and
// without one.
function supportsStableScrollbarGutter(referenceElement: Element | null): boolean {
  const supported = typeof CSS !== 'undefined' && typeof CSS.supports === 'function' && CSS.supports('scrollbar-gutter', 'stable');
  if (!supported) {
    return false;
  }

  const doc = ownerDocument(referenceElement);
  const html = doc.documentElement;
  const scrollContainer = getViewportScroller(html, doc.body);
  const originalOverflowY = scrollContainer.style.overflowY;
  const originalGutter = html.style.getPropertyValue('scrollbar-gutter');

  html.style.setProperty('scrollbar-gutter', 'stable');
  scrollContainer.style.overflowY = 'scroll';
  const before = scrollContainer.offsetWidth;
  scrollContainer.style.overflowY = 'hidden';
  const after = scrollContainer.offsetWidth;
  scrollContainer.style.overflowY = originalOverflowY;
  if (originalGutter) {
    html.style.setProperty('scrollbar-gutter', originalGutter);
  } else {
    html.style.removeProperty('scrollbar-gutter');
  }

  return before === after;
}

// UTL-DOM-05: simple mode, overflow hidden on the viewport scroller only.
function preventScrollOverlayScrollbars(referenceElement: Element | null): () => void {
  const doc = ownerDocument(referenceElement);
  const elementToLock = getViewportScroller(doc.documentElement, doc.body);
  const original = { overflowY: elementToLock.style.overflowY, overflowX: elementToLock.style.overflowX };
  elementToLock.style.overflowY = 'hidden';
  elementToLock.style.overflowX = 'hidden';
  return () => {
    elementToLock.style.overflowY = original.overflowY;
    elementToLock.style.overflowX = original.overflowX;
  };
}

const HTML_PROPERTIES = ['scrollbar-gutter', 'overflow-y', 'overflow-x'] as const;
const BODY_PROPERTIES = ['position', 'height', 'width', 'box-sizing', 'overflow-y', 'overflow-x', 'scroll-behavior'] as const;

// UTL-ALG-02: the inset-scrollbar lock, its restoration and its resize relock.
function preventScrollInsetScrollbars(referenceElement: Element | null): () => void {
  const doc = ownerDocument(referenceElement);
  const html = doc.documentElement;
  const body = doc.body;
  const win = ownerWindow(html);

  let scrollTop = 0;
  let scrollLeft = 0;
  let updateGutterOnly = false;
  let originalHtml: StyleSnapshot = {};
  let originalBody: StyleSnapshot = {};
  let originalHtmlScrollBehavior = '';
  let resizeFrame: number | null = null;

  // UTL-DOM-06: pinch-zoom in Safari causes a shift; no lock while zoomed.
  if (platform.engine.webkit && (win.visualViewport?.scale ?? 1) !== 1) {
    return noop;
  }

  function lockScroll(): void {
    // DOM reads.
    const htmlStyles = win.getComputedStyle(html);
    const bodyStyles = win.getComputedStyle(body);
    const gutter = htmlStyles.getPropertyValue('scrollbar-gutter') || '';
    const scrollbarGutterValue = gutter.includes('both-edges') ? 'stable both-edges' : 'stable';

    scrollTop = html.scrollTop;
    scrollLeft = html.scrollLeft;
    originalHtml = snapshot(html.style, HTML_PROPERTIES);
    originalHtmlScrollBehavior = html.style.getPropertyValue('scroll-behavior');
    originalBody = snapshot(body.style, BODY_PROPERTIES);

    const isScrollableY = html.scrollHeight > html.clientHeight;
    const isScrollableX = html.scrollWidth > html.clientWidth;
    const hasConstantOverflowY = htmlStyles.overflowY === 'scroll' || bodyStyles.overflowY === 'scroll';
    const hasConstantOverflowX = htmlStyles.overflowX === 'scroll' || bodyStyles.overflowX === 'scroll';
    // Values can be negative in Firefox.
    const scrollbarWidth = Math.max(0, win.innerWidth - body.clientWidth);
    const scrollbarHeight = Math.max(0, win.innerHeight - body.clientHeight);
    const marginY = parseFloat(bodyStyles.marginTop) + parseFloat(bodyStyles.marginBottom);
    const marginX = parseFloat(bodyStyles.marginLeft) + parseFloat(bodyStyles.marginRight);
    const elementToLock = getViewportScroller(html, body);

    updateGutterOnly = supportsStableScrollbarGutter(referenceElement);

    // DOM writes: nothing is read past this point.
    if (updateGutterOnly) {
      html.style.setProperty('scrollbar-gutter', scrollbarGutterValue);
      elementToLock.style.overflowY = 'hidden';
      elementToLock.style.overflowX = 'hidden';
      return;
    }

    html.style.setProperty('scrollbar-gutter', scrollbarGutterValue);
    html.style.overflowY = isScrollableY || hasConstantOverflowY ? 'scroll' : 'hidden';
    html.style.overflowX = isScrollableX || hasConstantOverflowX ? 'scroll' : 'hidden';

    body.style.position = 'relative';
    body.style.height = marginY || scrollbarHeight ? `calc(100dvh - ${marginY + scrollbarHeight}px)` : '100dvh';
    body.style.width = marginX || scrollbarWidth ? `calc(100vw - ${marginX + scrollbarWidth}px)` : '100vw';
    body.style.boxSizing = 'border-box';
    body.style.overflowY = 'hidden';
    body.style.overflowX = 'hidden';
    body.style.setProperty('scroll-behavior', 'unset');

    body.scrollTop = scrollTop;
    body.scrollLeft = scrollLeft;
    html.setAttribute(SCROLL_LOCKED_ATTRIBUTE, '');
    html.style.setProperty('scroll-behavior', 'unset');
  }

  function cleanup(): void {
    restore(html.style, originalHtml);
    restore(body.style, originalBody);
    if (!updateGutterOnly) {
      html.scrollTop = scrollTop;
      html.scrollLeft = scrollLeft;
      html.removeAttribute(SCROLL_LOCKED_ATTRIBUTE);
      if (originalHtmlScrollBehavior) {
        html.style.setProperty('scroll-behavior', originalHtmlScrollBehavior);
      } else {
        html.style.removeProperty('scroll-behavior');
      }
    }
  }

  // UTL-DOM-10: a resize restores at once and relocks on the next frame, re-measuring.
  function handleResize(): void {
    cleanup();
    if (resizeFrame !== null) {
      cancelFrame(resizeFrame);
    }
    resizeFrame = requestFrame(() => {
      resizeFrame = null;
      lockScroll();
    });
  }

  lockScroll();
  win.addEventListener('resize', handleResize);

  return () => {
    if (resizeFrame !== null) {
      cancelFrame(resizeFrame);
      resizeFrame = null;
    }
    cleanup();
    win.removeEventListener('resize', handleResize);
  };
}

// UTL-ALG-01: the page-global, reference-counted, deferred lock.
class ScrollLocker {
  lockCount = 0;
  restore: (() => void) | null = null;
  private lockTimer: ReturnType<typeof setTimeout> | undefined;
  private unlockTimer: ReturnType<typeof setTimeout> | undefined;

  acquire(referenceElement: Element | null): () => void {
    this.lockCount += 1;
    if (this.lockCount === 1 && this.restore === null) {
      clearTimeout(this.lockTimer);
      this.lockTimer = setTimeout(() => this.lock(referenceElement), 0);
    }
    let released = false;
    return () => {
      if (!released) {
        released = true;
        this.release();
      }
    };
  }

  reset(): void {
    clearTimeout(this.lockTimer);
    clearTimeout(this.unlockTimer);
    this.restore?.();
    this.restore = null;
    this.lockCount = 0;
  }

  private release(): void {
    this.lockCount -= 1;
    if (this.lockCount === 0 && this.restore) {
      clearTimeout(this.unlockTimer);
      this.unlockTimer = setTimeout(() => this.unlock(), 0);
    }
  }

  private unlock(): void {
    if (this.lockCount === 0 && this.restore) {
      this.restore();
      this.restore = null;
    }
  }

  private lock(referenceElement: Element | null): void {
    if (this.lockCount === 0 || this.restore !== null) {
      return;
    }

    const doc = ownerDocument(referenceElement);
    const html = doc.documentElement;
    const body = doc.body;
    const win = ownerWindow(html);

    // UTL-DOM-03: the page is already locked by someone else; wait for that lock to clear before taking over, or the
    // locked state would be snapshotted and restored after this lock is released.
    if (isPageScrollLocked(win, html, body)) {
      const observer = new win.MutationObserver(() => {
        if (isPageScrollLocked(win, html, body)) {
          return;
        }
        observer.disconnect();
        this.restore = null;
        this.lock(referenceElement);
      });
      // Every attribute: locks arrive as inline styles, classes, or attributes paired with a stylesheet.
      observer.observe(html, { attributes: true });
      observer.observe(body, { attributes: true });
      this.restore = () => observer.disconnect();
      return;
    }

    const hasOverlayScrollbars = platform.os.ios || !hasInsetScrollbars(referenceElement);
    this.restore = hasOverlayScrollbars ? preventScrollOverlayScrollbars(referenceElement) : preventScrollInsetScrollbars(referenceElement);
  }
}

let locker = new ScrollLocker();

/**
 * Acquires the page-global scroll lock for the document that owns `referenceElement`; the lock applies after a 0 ms timer
 * once the first holder acquires it, and releases after a 0 ms timer once the last holder releases it.
 * @param referenceElement The element whose document is locked, or `null` for the global document.
 * @returns The release function; calling it more than once releases once.
 */
export function acquireScrollLock(referenceElement: Element | null): () => void {
  return locker.acquire(referenceElement);
}

/** The number of holders of the page-global lock. */
export function scrollLockCount(): number {
  return locker.lockCount;
}

/**
 * Releases the page-global lock and forgets its holders. For tests only: the count is page-global, so a lock left by one
 * test would otherwise leak into the next.
 */
export function resetScrollLockForTest(): void {
  locker.reset();
  locker = new ScrollLocker();
}

const attachedHandle = Symbol('blazix.scroll-lock.handle');

interface AttachedElement extends HTMLElement {
  [attachedHandle]?: TwinHandle;
}

/**
 * Attaches a scroll-lock handle to a popup's reference element: while `enabled`, it holds one acquisition of the
 * page-global lock on that element's document.
 * @param elementOrId The reference element, or its id.
 * @param optionsJson The `ScrollLockOptions` JSON.
 * @param _twinReference The twin; this module neither reports nor asks a decision point.
 * @returns The handle with `update(optionsJson)` and `dispose()`; the live handle when the element is already attached.
 */
export function attach(elementOrId: Element | string, optionsJson: string, _twinReference: DotNetTwin): TwinHandle {
  const element: AttachedElement = resolveElement(elementOrId);
  const existing = element[attachedHandle];
  if (existing) {
    return existing;
  }

  let options = parseOptions<ScrollLockOptions>(optionsJson);
  let release: (() => void) | null = null;
  let disposed = false;

  const apply = () => {
    if (options.enabled && release === null) {
      release = acquireScrollLock(element);
    } else if (!options.enabled && release !== null) {
      release();
      release = null;
    }
  };

  apply();

  const handle: TwinHandle = {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<ScrollLockOptions>(nextOptionsJson);
      apply();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      delete element[attachedHandle];
      release?.();
      release = null;
    },
  };
  element[attachedHandle] = handle;
  return handle;
}
