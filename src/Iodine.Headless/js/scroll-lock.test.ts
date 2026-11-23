// L3 (ADR 06 §1): the `scroll-lock` module in jsdom (shared-utilities.md §4.8, UTL-DOM-01..11, UTL-ALG-01/02). Base UI's
// useScrollLock has no test, so every title is a port title that states a positive behaviour, its absence folded in.
// jsdom does no layout: the window, the document element and the body get the measurements each case needs, the platform
// flags are a mutable stand-in, and fake timers drive the 0 ms deferral and the resize frame.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createFakeTwin } from './test-support/fake-twin';
import { resetFrameScheduler } from './transitions';

const flags = vi.hoisted(() => ({ webkit: false, ios: false }));

vi.mock('./platform', async (importOriginal) => {
  const original = await importOriginal<typeof import('./platform')>();
  return {
    ...original,
    platform: {
      ...original.platform,
      engine: {
        ...original.platform.engine,
        get webkit() {
          return flags.webkit;
        },
      },
      os: {
        ...original.platform.os,
        get ios() {
          return flags.ios;
        },
      },
    },
  };
});

const { SCROLL_LOCKED_ATTRIBUTE, acquireScrollLock, attach, resetScrollLockForTest, scrollLockCount } = await import('./scroll-lock');

/** Gives a document the measurements of a page with an inset scrollbar of `scrollbar` px, or none. */
function measure(doc: Document, { scrollbar = 0, scrollHeight = 2000, width = 1024, height = 768 } = {}): void {
  const html = doc.documentElement;
  const body = doc.body;
  const win = (doc.defaultView ?? window) as Window;
  Object.defineProperty(win, 'innerWidth', { configurable: true, value: width });
  Object.defineProperty(win, 'innerHeight', { configurable: true, value: height });
  Object.defineProperty(html, 'clientWidth', { configurable: true, value: width - scrollbar });
  Object.defineProperty(html, 'clientHeight', { configurable: true, value: height });
  Object.defineProperty(html, 'scrollHeight', { configurable: true, value: scrollHeight });
  Object.defineProperty(html, 'scrollWidth', { configurable: true, value: width - scrollbar });
  Object.defineProperty(body, 'clientWidth', { configurable: true, value: width - scrollbar });
  Object.defineProperty(body, 'clientHeight', { configurable: true, value: height });
}

/**
 * Whether Base UI's scroll lock is active on the document (test/scrollLock.ts, both signals). jsdom does not compose the
 * `overflow` shorthand from its longhands, so the longhands stand for it here; the L4 helper reads the shorthand.
 */
function isScrollLocked(doc: Document = document): boolean {
  const hidden = (style: CSSStyleDeclaration) => style.overflow === 'hidden' || (style.overflowX === 'hidden' && style.overflowY === 'hidden');
  return hidden(doc.documentElement.style) || doc.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE) || hidden(doc.body.style);
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'requestAnimationFrame', 'cancelAnimationFrame'] });
  flags.webkit = false;
  flags.ios = false;
  document.documentElement.removeAttribute('style');
  document.body.removeAttribute('style');
  document.documentElement.removeAttribute(SCROLL_LOCKED_ATTRIBUTE);
  // jsdom lays nothing out, so every offsetWidth is 0 and the gutter probe would always pass: no stable gutter by default.
  vi.stubGlobal('CSS', { supports: () => false });
  measure(document);
});

afterEach(() => {
  resetScrollLockForTest();
  resetFrameScheduler();
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('acquisition and release (UTL-ALG-01)', () => {
  it('port: the lock applies after a 0 ms timer once for nested holders, and releases after a 0 ms timer once the last holder releases', () => {
    const releaseOuter = acquireScrollLock(null);
    expect(isScrollLocked()).toBe(false);
    vi.advanceTimersByTime(0);
    expect(document.body.style.overflowY).toBe('hidden');

    const releaseInner = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    releaseInner();
    vi.advanceTimersByTime(0);
    expect(scrollLockCount()).toBe(1);
    expect(document.body.style.overflowY).toBe('hidden');

    releaseOuter();
    expect(document.body.style.overflowY).toBe('hidden');
    vi.advanceTimersByTime(0);
    expect(scrollLockCount()).toBe(0);
    expect(document.body.style.overflowY).toBe('');
  });

  it('port: a lock acquired and released within the same task applies nothing, and a release re-acquired within one task keeps the lock', () => {
    const write = vi.spyOn(document.body.style, 'overflowY', 'set');
    acquireScrollLock(null)();
    vi.advanceTimersByTime(0);
    expect(write).not.toHaveBeenCalled();
    expect(isScrollLocked()).toBe(false);

    const release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(document.body.style.overflowY).toBe('hidden');
    release();
    const again = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(document.body.style.overflowY).toBe('hidden');
    again();
    vi.advanceTimersByTime(0);
    expect(document.body.style.overflowY).toBe('');
  });

  it('port: when the page is already locked by someone else, the lock waits for that lock to clear and then applies', () => {
    document.body.style.overflowY = 'clip';
    const release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(document.body.style.overflowY).toBe('clip');

    document.body.style.overflowY = '';
    return Promise.resolve().then(() => {
      // The observer's callback runs in the microtask after the attribute mutation.
      expect(document.body.style.overflowY).toBe('hidden');
      release();
      vi.advanceTimersByTime(0);
      expect(document.body.style.overflowY).toBe('');
    });
  });
});

describe('modes', () => {
  it('port: simple mode locks the viewport scroller only, <html> when it is an overflow element and <body> otherwise, and restores it', () => {
    measure(document, { scrollbar: 0 });
    let release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(document.body.style.overflowY).toBe('hidden');
    expect(document.body.style.overflowX).toBe('hidden');
    expect(document.documentElement.style.overflowY).toBe('');
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(false);
    release();
    vi.advanceTimersByTime(0);

    document.documentElement.style.overflowX = 'auto';
    release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(document.documentElement.style.overflowY).toBe('hidden');
    expect(document.body.style.overflowY).toBe('');
    release();
    vi.advanceTimersByTime(0);
    expect(document.documentElement.style.overflowY).toBe('');
    expect(document.documentElement.style.overflowX).toBe('auto');
  });

  it('port: iOS takes simple mode even with an inset scrollbar', () => {
    flags.ios = true;
    measure(document, { scrollbar: 15 });
    acquireScrollLock(null);
    vi.advanceTimersByTime(0);

    expect(document.body.style.overflowY).toBe('hidden');
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(false);
    expect(document.body.style.position).toBe('');
  });

  it('port: the full inset path writes the html and body styles and the attribute, moves the scroll offsets, and restores all of them', () => {
    measure(document, { scrollbar: 15 });
    document.body.style.position = 'static';
    document.documentElement.scrollTop = 120;

    const release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);

    const html = document.documentElement.style;
    const body = document.body.style;
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(true);
    expect(html.getPropertyValue('scrollbar-gutter')).toBe('stable');
    expect(html.overflowY).toBe('scroll');
    expect(html.overflowX).toBe('hidden');
    expect(html.getPropertyValue('scroll-behavior')).toBe('unset');
    expect(body.position).toBe('relative');
    expect(body.width).toBe('calc(100vw - 15px)');
    expect(body.boxSizing).toBe('border-box');
    expect(body.overflowY).toBe('hidden');
    expect(document.body.scrollTop).toBe(120);

    release();
    vi.advanceTimersByTime(0);
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(false);
    expect(html.overflowY).toBe('');
    expect(html.getPropertyValue('scrollbar-gutter')).toBe('');
    expect(html.getPropertyValue('scroll-behavior')).toBe('');
    expect(body.position).toBe('static');
    expect(body.width).toBe('');
    expect(document.documentElement.scrollTop).toBe(120);
  });

  it('port: the gutter path writes only scrollbar-gutter and overflow when a stable gutter keeps the width', () => {
    measure(document, { scrollbar: 15 });
    vi.stubGlobal('CSS', { supports: (property: string, value: string) => property === 'scrollbar-gutter' && value === 'stable' });
    Object.defineProperty(document.body, 'offsetWidth', { configurable: true, value: 1009 });

    const release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);

    expect(document.documentElement.style.getPropertyValue('scrollbar-gutter')).toBe('stable');
    expect(document.body.style.overflowY).toBe('hidden');
    expect(document.body.style.position).toBe('');
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(false);
    release();
    vi.advanceTimersByTime(0);
    expect(document.documentElement.style.getPropertyValue('scrollbar-gutter')).toBe('');
  });

  it('port: under pinch-zoom on WebKit an inset lock writes nothing, and the same page locks at a scale of 1', () => {
    flags.webkit = true;
    measure(document, { scrollbar: 15 });
    Object.defineProperty(window, 'visualViewport', { configurable: true, value: { scale: 1.5 } });
    let release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(isScrollLocked()).toBe(false);
    release();
    vi.advanceTimersByTime(0);

    Object.defineProperty(window, 'visualViewport', { configurable: true, value: { scale: 1 } });
    release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(isScrollLocked()).toBe(true);
    release();
    vi.advanceTimersByTime(0);
    Object.defineProperty(window, 'visualViewport', { configurable: true, value: undefined });
  });

  it('port: a resize restores the inset lock at once and relocks it on the next frame with the new measurements', () => {
    measure(document, { scrollbar: 15 });
    const release = acquireScrollLock(null);
    vi.advanceTimersByTime(0);
    expect(document.body.style.width).toBe('calc(100vw - 15px)');

    measure(document, { scrollbar: 17 });
    window.dispatchEvent(new Event('resize'));
    expect(document.body.style.width).toBe('');
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(false);

    vi.advanceTimersToNextFrame();
    expect(document.body.style.width).toBe('calc(100vw - 17px)');
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(true);

    release();
    vi.advanceTimersByTime(0);
    window.dispatchEvent(new Event('resize'));
    vi.advanceTimersToNextFrame();
    expect(document.documentElement.hasAttribute(SCROLL_LOCKED_ATTRIBUTE)).toBe(false);
  });

  it("port: the lock measures and writes the reference element's document", () => {
    const other = document.implementation.createHTMLDocument('other');
    const reference = other.createElement('div');
    other.body.appendChild(reference);
    measure(document, { scrollbar: 0 });
    Object.defineProperty(other.documentElement, 'clientWidth', { configurable: true, value: 1024 });

    const release = acquireScrollLock(reference);
    vi.advanceTimersByTime(0);

    expect(other.body.style.overflowY).toBe('hidden');
    expect(document.body.style.overflowY).toBe('');
    release();
    vi.advanceTimersByTime(0);
    expect(other.body.style.overflowY).toBe('');
  });
});

describe('the twin handle', () => {
  it('port: a handle holds one acquisition while enabled, releases it when disabled and on dispose, and attaches once per element', () => {
    const element = document.createElement('div');
    document.body.appendChild(element);
    const fake = createFakeTwin();

    const handle = attach(element, JSON.stringify({ enabled: true }), fake.twin);
    expect(attach(element, JSON.stringify({ enabled: true }), fake.twin)).toBe(handle);
    expect(scrollLockCount()).toBe(1);
    vi.advanceTimersByTime(0);
    expect(isScrollLocked()).toBe(true);

    handle.update(JSON.stringify({ enabled: false }));
    expect(scrollLockCount()).toBe(0);
    handle.update(JSON.stringify({ enabled: true }));
    handle.update(JSON.stringify({ enabled: true }));
    expect(scrollLockCount()).toBe(1);

    handle.dispose();
    handle.dispose();
    expect(scrollLockCount()).toBe(0);
    vi.advanceTimersByTime(0);
    expect(isScrollLocked()).toBe(false);
    expect(fake.reports).toEqual([]);
  });
});
