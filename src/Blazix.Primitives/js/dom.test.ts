// L3 (ADR 06 §1): the `dom` module in jsdom against a fake twin. Titles are Base UI's where a Base UI test covers the
// helper (shadowDom.test.ts, getCssDimensions.test.ts, getPseudoElementBounds.test.ts, scrollEdges.test.ts,
// useInitialLiveRegionTextMutation.test.tsx, AvatarImage.test.tsx, CheckboxRoot.test.tsx); the port's own titles
// cover the rows Base UI leaves untested (shared-utilities.md §4.10, §4.11, §4.16 and UTL-ALG-11).
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  INITIAL_LIVE_REGION_TEXT_MUTATION_RESET_DELAY,
  activeElement,
  attach,
  closest,
  contains,
  deriveImageStatus,
  detectPlatform,
  findScrollableAncestor,
  findScrollableTouchTarget,
  getCssDimensions,
  getElementAtPoint,
  getElementTransform,
  getMaxScrollOffset,
  getPseudoElementBounds,
  isOverflowElement,
  getTarget,
  isMouseWithinBounds,
  isScrollable,
  isScrollableX,
  isScrollableY,
  isShadowRoot,
  markLiveRegion,
  normalizeScrollOffset,
  observeIntersection,
  observeResize,
  ownerDocument,
  styleWriter,
  ownerWindow,
  platform,
  probeImage,
  valueToPercent,
  type DomOptions,
  type ImageLoadingStatus,
} from './dom';
import type { TwinHandle } from './twin';
import { createFakeTwin } from './test-support/fake-twin';

const isJSDOM = /jsdom/.test(window.navigator.userAgent);
const WORD_JOINER = '⁠';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

/** Resolves after the browser would have painted the current frame: one animation frame, then one task. */
function afterPaint(): Promise<void> {
  return new Promise((resolve) => requestAnimationFrame(() => setTimeout(resolve, 0)));
}

function createElementWithRect(rect: DOMRectInit) {
  const element = document.createElement('div');
  element.getBoundingClientRect = () => DOMRect.fromRect(rect);
  return element;
}

function defineSizes(element: HTMLElement, sizes: Partial<Record<'offsetWidth' | 'offsetHeight' | 'scrollHeight' | 'clientHeight' | 'scrollWidth' | 'clientWidth', number>>) {
  for (const [name, value] of Object.entries(sizes)) {
    Object.defineProperty(element, name, { configurable: true, value });
  }
}

describe('platform', () => {
  const data = (userAgent: string, platformString: string, maxTouchPoints = 0) => ({ userAgent, platform: platformString, maxTouchPoints });

  it('computes every flag false without a navigator', () => {
    const flags = detectPlatform(undefined, false);

    expect(Object.values(flags.engine).concat(Object.values(flags.os), Object.values(flags.screenReader), Object.values(flags.env))).toEqual(
      Array(11).fill(false),
    );
    expect(flags.mediaQuery.iOS).toBe('@supports (-webkit-touch-callout: none)');
  });

  it('detects WebKit from CSS.supports and keeps the engine flags exclusive', () => {
    const safari = detectPlatform(data('Mozilla/5.0 (Macintosh) AppleWebKit/605.1.15 Version/18.0 Safari/605.1.15 Chrome-like', 'MacIntel'), true);

    expect(safari.engine).toEqual({ webkit: true, gecko: false, blink: false });
  });

  it('detects Gecko and Blink from the user agent', () => {
    expect(detectPlatform(data('Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0', 'Linux x86_64'), false).engine).toEqual({
      webkit: false,
      gecko: true,
      blink: false,
    });
    expect(detectPlatform(data('Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/140.0 Safari/537.36', 'Win32'), false).engine).toEqual({
      webkit: false,
      gecko: false,
      blink: true,
    });
  });

  it('detects iOS from the platform string and iPadOS from MacIntel with touch points', () => {
    for (const platformString of ['iPhone', 'iPad', 'iPod touch', 'iOS']) {
      expect(detectPlatform(data('', platformString), true).os.ios).toBe(true);
    }
    const iPadOS = detectPlatform(data('', 'MacIntel', 5), true);
    expect([iPadOS.os.ios, iPadOS.os.mac, iPadOS.os.apple]).toEqual([true, false, true]);
    expect(detectPlatform(data('', 'MacIntel', 1), true).os.ios).toBe(false);
  });

  it('detects Android, Mac, Windows and Linux', () => {
    const android = detectPlatform(data('Mozilla/5.0 (Linux; Android 16) Chrome/140.0', 'Linux armv81'), false).os;
    expect([android.android, android.linux]).toEqual([true, false]);
    expect(detectPlatform(data('', 'Android'), false).os.android).toBe(true);
    expect(detectPlatform(data('', 'MacIntel'), false).os.mac).toBe(true);
    expect(detectPlatform(data('', 'Win32'), false).os.windows).toBe(true);
    expect(detectPlatform(data('', 'Linux x86_64'), false).os.linux).toBe(true);
    expect(detectPlatform(data('', 'Chrome OS'), false).os.linux).toBe(true);
  });

  it('treats VoiceOver as possible on every Apple platform and nowhere else', () => {
    expect(detectPlatform(data('', 'MacIntel'), false).screenReader.voiceOver).toBe(true);
    expect(detectPlatform(data('', 'iPhone'), true).screenReader.voiceOver).toBe(true);
    expect(detectPlatform(data('', 'Win32'), false).screenReader.voiceOver).toBe(false);
  });

  it('flags the jsdom and happy-dom test environments', () => {
    expect(detectPlatform(data('Mozilla/5.0 (darwin) AppleWebKit/537.36 (KHTML, like Gecko) jsdom/30.1.3', ''), false).env.jsdom).toBe(true);
    expect(detectPlatform(data('Mozilla/5.0 HappyDOM/20.0', ''), false).env.jsdom).toBe(true);
    expect(detectPlatform(data('Mozilla/5.0 Chrome/140.0', ''), false).env.jsdom).toBe(false);
  });

  it('computes the page flags once when the module loads', () => {
    expect(platform.env.jsdom).toBe(isJSDOM);
    expect(Object.isFrozen(platform)).toBe(true);
  });
});

describe('owner', () => {
  it('ownerDocument returns the node document, or the global document for null', () => {
    const other = document.implementation.createHTMLDocument('other');
    const element = other.createElement('div');

    expect(ownerDocument(element)).toBe(other);
    expect(ownerDocument(null)).toBe(document);
  });

  it('ownerWindow returns the window of the node document, or the global window', () => {
    const element = document.createElement('div');

    expect(ownerWindow(element)).toBe(document.defaultView);
    expect(ownerWindow(null)).toBe(window);
  });
});

describe('shadow DOM utilities', () => {
  describe('closest', () => {
    it('finds an ancestor across nested shadow roots from a non-Element node', () => {
      const button = document.createElement('button');
      const outerHost = document.createElement('span');
      const innerHost = document.createElement('span');
      const target = document.createTextNode('target');

      document.body.appendChild(button);
      button.appendChild(outerHost);
      outerHost.attachShadow({ mode: 'open' }).appendChild(innerHost);
      innerHost.attachShadow({ mode: 'open' }).appendChild(target);

      expect(closest(target, 'button')).toBe(button);
    });

    it('follows the composed tree through slots', () => {
      const host = document.createElement('span');
      const target = document.createElement('span');
      const shadowAncestor = document.createElement('div');
      const slot = document.createElement('slot');

      shadowAncestor.dataset.testid = 'shadow-ancestor';
      shadowAncestor.appendChild(slot);
      host.attachShadow({ mode: 'open' }).appendChild(shadowAncestor);
      host.appendChild(target);
      document.body.appendChild(host);

      expect(closest(target, '[data-testid="shadow-ancestor"]')).toBe(shadowAncestor);
    });

    it('returns null for a detached node instead of falling back to the document element', () => {
      document.documentElement.classList.add('match');
      const element = document.createElement('div');

      expect(closest(element, '.match')).toBeNull();
      document.documentElement.classList.remove('match');
    });

    it('preserves native selector validation', () => {
      const element = document.createElement('div');

      expect(() => closest(element, '[')).toThrow();
    });

    it('finds an interactive ancestor outside a composed event target shadow root', () => {
      const button = document.createElement('button');
      let matchedElement: Element | null = null;
      const listener = vi.fn((event: PointerEvent) => {
        matchedElement = closest(getTarget(event) as Node, 'button');
      });
      const host = document.createElement('span');
      const target = document.createElement('span');

      document.body.appendChild(button);
      button.appendChild(host);
      host.attachShadow({ mode: 'open' }).appendChild(target);
      button.addEventListener('pointerdown', listener);

      target.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, composed: true }));

      expect(listener).toHaveBeenCalledOnce();
      expect(matchedElement).toBe(button);
    });
  });

  describe('getTarget', () => {
    it('returns the composed-path target while the event is being dispatched', () => {
      const parent = document.createElement('div');
      const child = document.createElement('span');
      parent.appendChild(child);
      document.body.appendChild(parent);

      let target: EventTarget | null = null;
      parent.addEventListener('click', (event) => {
        target = getTarget(event);
      });
      child.dispatchEvent(new Event('click', { bubbles: true }));

      expect(target).toBe(child);
      parent.remove();
    });

    it('falls back to `target` once dispatch has completed', () => {
      const element = document.createElement('div');
      document.body.appendChild(element);

      const event = new Event('click');
      element.dispatchEvent(event);

      expect(event.composedPath()).toHaveLength(0);
      expect(getTarget(event)).toBe(element);
      element.remove();
    });
  });

  describe('activeElement', () => {
    it('descends into open shadow roots to the deepest focused element', () => {
      const host = document.createElement('div');
      const inner = document.createElement('button');
      host.attachShadow({ mode: 'open' }).appendChild(inner);
      document.body.appendChild(host);

      inner.focus();

      expect(document.activeElement).toBe(host);
      expect(activeElement(document)).toBe(inner);
    });
  });

  describe('isShadowRoot', () => {
    // Exported for the focus manager's tabbable search and outside marking (M4 track 4), which walk out of shadow roots.
    it('recognizes a shadow root of this window and of another window, and nothing else', () => {
      const host = document.createElement('div');
      const root = host.attachShadow({ mode: 'open' });
      const iframe = document.createElement('iframe');
      document.body.appendChild(iframe);
      const otherHost = iframe.contentDocument!.createElement('div');
      const otherRoot = otherHost.attachShadow({ mode: 'open' });

      expect(isShadowRoot(root)).toBe(true);
      expect(isShadowRoot(otherRoot)).toBe(true);
      expect(isShadowRoot(host)).toBe(false);
      expect(isShadowRoot(document.createDocumentFragment())).toBe(false);
      expect(isShadowRoot(null)).toBe(false);
    });
  });

  describe('contains', () => {
    it('is false when either node is missing', () => {
      const element = document.createElement('div');

      expect(contains(null, element)).toBe(false);
      expect(contains(element, undefined)).toBe(false);
    });

    it('uses the native check and then walks out of shadow roots', () => {
      const parent = document.createElement('div');
      const host = document.createElement('span');
      const inner = document.createElement('span');
      parent.appendChild(host);
      host.attachShadow({ mode: 'open' }).appendChild(inner);
      document.body.appendChild(parent);

      expect(contains(parent, host)).toBe(true);
      expect(parent.contains(inner)).toBe(false);
      expect(contains(parent, inner)).toBe(true);
      expect(contains(inner, parent)).toBe(false);
    });
  });
});

describe('getCssDimensions', () => {
  // Parts that accept a `render` prop can be SVG elements, which have no `offsetWidth`/`offsetHeight`.
  it.skipIf(isJSDOM)('reads the CSS size of an SVG element', () => {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', '80');
    svg.setAttribute('height', '24');
    document.body.appendChild(svg);

    try {
      expect(getCssDimensions(svg)).toEqual({ width: 80, height: 24 });
    } finally {
      svg.remove();
    }
  });

  it('keeps the CSS size when it rounds to the offset size', () => {
    const element = document.createElement('div');
    element.style.width = '10.4px';
    element.style.height = '20.2px';
    defineSizes(element, { offsetWidth: 10, offsetHeight: 20 });
    document.body.appendChild(element);

    expect(getCssDimensions(element)).toEqual({ width: 10.4, height: 20.2 });
  });

  it('falls back to the offset size when the rounded CSS size differs', () => {
    const element = document.createElement('div');
    element.style.width = '10px';
    element.style.height = '20px';
    defineSizes(element, { offsetWidth: 15, offsetHeight: 20 });
    document.body.appendChild(element);

    expect(getCssDimensions(element)).toEqual({ width: 15, height: 20 });
  });

  it('treats a non-numeric CSS size as 0 and uses the CSS size of a non-HTML element as is', () => {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    document.body.appendChild(svg);

    expect(getCssDimensions(svg)).toEqual({ width: 0, height: 0 });
  });
});

describe('getElementTransform', () => {
  const transformOf = (transform: string) => getElementTransform(document.createElement('div'), { transform } as CSSStyleDeclaration);

  it('reads translation and scale from a 2D matrix', () => {
    expect(transformOf('matrix(0, 2, -2, 0, 10, 20)')).toEqual({ x: 10, y: 20, scale: 2 });
  });

  it('reads translation and the first scale factor from a 3D matrix', () => {
    expect(transformOf('matrix3d(3, 0, 0, 0, 0, 3, 0, 0, 0, 0, 1, 0, 7, 8, 9, 1)')).toEqual({ x: 7, y: 8, scale: 3 });
  });

  it('is the identity for none, an absent value or an unrecognised value', () => {
    expect(transformOf('none')).toEqual({ x: 0, y: 0, scale: 1 });
    expect(transformOf('')).toEqual({ x: 0, y: 0, scale: 1 });
    expect(transformOf('rotate(45deg)')).toEqual({ x: 0, y: 0, scale: 1 });
  });

  it('reads the computed transform when no style is given', () => {
    const element = document.createElement('div');
    element.style.transform = 'matrix(1, 0, 0, 1, 4, 5)';
    document.body.appendChild(element);

    expect(getElementTransform(element)).toEqual({ x: 4, y: 5, scale: 1 });
  });
});

describe('getPseudoElementBounds', () => {
  it.skipIf(!isJSDOM)('does not read pseudo-element styles in jsdom', () => {
    const element = createElementWithRect({ x: 100, y: 50, width: 20, height: 10 });
    const getComputedStyle = vi.spyOn(window, 'getComputedStyle');

    expect(getPseudoElementBounds(element)).toMatchObject({
      left: 100,
      right: 120,
      top: 50,
      bottom: 60,
    });
    expect(getComputedStyle).not.toHaveBeenCalled();
  });

  it.skipIf(isJSDOM)('includes pseudo-element bounds in browsers', () => {
    const style = document.createElement('style');
    style.textContent = `
      .pseudo-element-bounds-test {
        position: absolute;
        left: 100px;
        top: 50px;
        width: 20px;
        height: 10px;
      }

      .pseudo-element-bounds-test::before {
        content: "";
        display: block;
        width: 40px;
        height: 30px;
      }
    `;

    const element = document.createElement('div');
    element.className = 'pseudo-element-bounds-test';

    document.head.appendChild(style);
    document.body.appendChild(element);

    try {
      expect(getPseudoElementBounds(element)).toEqual({
        left: 90,
        right: 130,
        top: 40,
        bottom: 70,
      });
    } finally {
      element.remove();
      style.remove();
    }
  });

  it('allows up to 5px of mouse drift around element bounds', () => {
    const element = createElementWithRect({ x: 100, y: 50, width: 20, height: 10 });

    const within = (clientX: number, clientY: number) =>
      isMouseWithinBounds(new MouseEvent('mouseup', { clientX, clientY }), element);

    // Left edge
    expect(within(95, 55)).toBe(true);
    expect(within(94, 55)).toBe(false);
    // Right edge
    expect(within(125, 55)).toBe(true);
    expect(within(126, 55)).toBe(false);
    // Top edge
    expect(within(110, 45)).toBe(true);
    expect(within(110, 44)).toBe(false);
    // Bottom edge
    expect(within(110, 65)).toBe(true);
    expect(within(110, 66)).toBe(false);
  });
});

describe('getElementAtPoint', () => {
  it('asks the root for the element at the point, or answers null for a root without elementFromPoint', () => {
    const element = document.createElement('div');
    const root = { elementFromPoint: vi.fn(() => element) } as unknown as Document;

    expect(getElementAtPoint(root, 3, 4)).toBe(element);
    expect(root.elementFromPoint).toHaveBeenCalledWith(3, 4);
    expect(getElementAtPoint(document.createElement('div'), 3, 4)).toBeNull();
    expect(getElementAtPoint(null, 3, 4)).toBeNull();
  });
});

describe('scrollable', () => {
  function scroller(overflow: string, sizes: Parameters<typeof defineSizes>[1]) {
    const element = document.createElement('div');
    element.style.overflowX = overflow;
    element.style.overflowY = overflow;
    defineSizes(element, sizes);
    return element;
  }

  it('is scrollable on an axis with auto or scroll overflow and more content than room', () => {
    expect(isScrollableY(scroller('auto', { scrollHeight: 200, clientHeight: 100 }))).toBe(true);
    expect(isScrollableY(scroller('scroll', { scrollHeight: 100, clientHeight: 100 }))).toBe(false);
    expect(isScrollableY(scroller('hidden', { scrollHeight: 200, clientHeight: 100 }))).toBe(false);
    expect(isScrollableX(scroller('scroll', { scrollWidth: 200, clientWidth: 100 }))).toBe(true);
  });

  it('counts a container with room on the axis when overflow intent is allowed', () => {
    const element = scroller('auto', { scrollHeight: 100, clientHeight: 100, scrollWidth: 0, clientWidth: 0 });

    expect(isScrollable(element, 'vertical', true)).toBe(true);
    expect(isScrollable(element, 'horizontal', true)).toBe(false);
  });

  it('finds the first scrollable ancestor across a shadow boundary, stopping at the root and at body', () => {
    const root = document.createElement('div');
    const outer = scroller('auto', { scrollHeight: 200, clientHeight: 100 });
    const host = document.createElement('div');
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    root.appendChild(outer);
    outer.appendChild(host);
    host.attachShadow({ mode: 'open' }).appendChild(svg);
    document.body.appendChild(root);

    expect(findScrollableAncestor(svg, root, 'vertical')).toBe(outer);
    expect(findScrollableAncestor(svg, outer, 'vertical')).toBeNull();
    expect(findScrollableAncestor(svg, root, 'horizontal')).toBeNull();
  });

  it('falls back to the root itself for a touch target when the root scrolls', () => {
    const root = scroller('auto', { scrollHeight: 200, clientHeight: 100 });
    const child = document.createElement('div');
    root.appendChild(child);
    document.body.appendChild(root);

    expect(findScrollableTouchTarget(child, root)).toBe(root);
    expect(findScrollableTouchTarget(child, root, 'horizontal')).toBeNull();
  });
});

// ADR 04 Amendments 2026-10-27 (the Floating UI predicates the M4 modules need, re-implemented here): `@floating-ui/utils`'
// isOverflowElement, which the scroll lock's viewport scroller reads (UTL-DOM-04). Port title: Floating UI's own test is
// not vendored.
describe('isOverflowElement', () => {
  it('port: an element whose overflow scrolls or clips is an overflow element, unless it is inline or display: contents', () => {
    const element = (style: Partial<CSSStyleDeclaration>) => {
      const node = document.createElement('div');
      Object.assign(node.style, style);
      document.body.appendChild(node);
      return node;
    };

    for (const overflow of ['auto', 'scroll', 'overlay', 'hidden', 'clip']) {
      expect(isOverflowElement(element({ overflowY: overflow }))).toBe(true);
    }
    expect(isOverflowElement(element({ overflowX: 'hidden' }))).toBe(true);
    expect(isOverflowElement(element({ overflow: 'auto', display: 'block' }))).toBe(true);
    expect(isOverflowElement(element({}))).toBe(false);
    expect(isOverflowElement(element({ overflow: 'visible' }))).toBe(false);
    expect(isOverflowElement(element({ overflow: 'hidden', display: 'inline' }))).toBe(false);
    expect(isOverflowElement(element({ overflow: 'auto', display: 'contents' }))).toBe(false);
  });
});

describe('normalizeScrollOffset', () => {
  it('returns 0 when max is non-positive', () => {
    expect(normalizeScrollOffset(10, 0)).toBe(0);
    expect(normalizeScrollOffset(10, -1)).toBe(0);
  });

  it('snaps to the start edge within the tolerance', () => {
    expect(normalizeScrollOffset(1, 100)).toBe(0);
  });

  it('snaps to the end edge within the tolerance', () => {
    expect(normalizeScrollOffset(99, 100)).toBe(100);
  });

  it('keeps values away from edges unchanged', () => {
    expect(normalizeScrollOffset(50, 100)).toBe(50);
  });

  it('chooses the closest edge when tolerances overlap', () => {
    expect(normalizeScrollOffset(0.4, 1)).toBe(0);
    expect(normalizeScrollOffset(0.6, 1)).toBe(1);
  });

  it('measures the scroll range as the overflow, never negative', () => {
    expect(getMaxScrollOffset(300, 100)).toBe(200);
    expect(getMaxScrollOffset(100, 300)).toBe(0);
  });
});

describe('valueToPercent', () => {
  it('maps the value onto the range without clamping', () => {
    expect(valueToPercent(25, 0, 50)).toBe(50);
    expect(valueToPercent(-10, 0, 100)).toBe(-10);
    expect(valueToPercent(150, 50, 150)).toBe(100);
  });
});

describe('observers', () => {
  it('observeResize does nothing without ResizeObserver', () => {
    vi.stubGlobal('ResizeObserver', undefined);
    const callback = vi.fn();

    const unsubscribe = observeResize(document.createElement('div'), callback);

    expect(() => unsubscribe()).not.toThrow();
    expect(callback).not.toHaveBeenCalled();
  });

  it('observeResize forwards each entry and disconnects on unsubscribe', () => {
    const instances: { callback: ResizeObserverCallback; observe: ReturnType<typeof vi.fn>; disconnect: ReturnType<typeof vi.fn> }[] = [];
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe = vi.fn();
        disconnect = vi.fn();
        constructor(public callback: ResizeObserverCallback) {
          instances.push(this);
        }
      },
    );
    const element = document.createElement('div');
    const callback = vi.fn();

    const unsubscribe = observeResize(element, callback, { box: 'border-box' });
    const entry = { target: element } as unknown as ResizeObserverEntry;
    instances[0]!.callback([entry], {} as ResizeObserver);
    unsubscribe();

    expect(instances[0]!.observe).toHaveBeenCalledWith(element, { box: 'border-box' });
    expect(callback).toHaveBeenCalledWith(entry);
    expect(instances[0]!.disconnect).toHaveBeenCalledOnce();
  });

  it('observeIntersection does nothing without IntersectionObserver and forwards entries with it', () => {
    vi.stubGlobal('IntersectionObserver', undefined);
    expect(() => observeIntersection(document.createElement('div'), vi.fn())()).not.toThrow();

    const instances: { callback: IntersectionObserverCallback; options?: IntersectionObserverInit | undefined; disconnect: ReturnType<typeof vi.fn> }[] = [];
    vi.stubGlobal(
      'IntersectionObserver',
      class {
        observe = vi.fn();
        disconnect = vi.fn();
        constructor(
          public callback: IntersectionObserverCallback,
          public options?: IntersectionObserverInit,
        ) {
          instances.push(this);
        }
      },
    );
    const callback = vi.fn();
    const unsubscribe = observeIntersection(document.createElement('div'), callback, { threshold: 1 });
    const entry = { isIntersecting: true } as IntersectionObserverEntry;
    instances[0]!.callback([entry], {} as IntersectionObserver);
    unsubscribe();

    expect(instances[0]!.options).toEqual({ threshold: 1 });
    expect(callback).toHaveBeenCalledWith(entry);
    expect(instances[0]!.disconnect).toHaveBeenCalledOnce();
  });
});

describe('useInitialLiveRegionTextMutation', () => {
  function status(...texts: string[]) {
    const element = document.createElement('div');
    element.dataset.testid = 'status';
    element.append(...texts.map((text) => document.createTextNode(text)));
    document.body.appendChild(element);
    return element;
  }

  it('does nothing when its ref is not attached', async () => {
    const element = status('Status');
    const fake = createFakeTwin();
    handles.push(attach(element, JSON.stringify({} satisfies DomOptions), fake.twin));

    await afterPaint();

    expect(element.textContent).toBe('Status');
  });

  it('marks the text when the element appears after mount', async () => {
    const element = status('Ready');

    markLiveRegion(element);
    expect(element.firstChild).toHaveProperty('nodeValue', 'Ready');

    await afterPaint();

    expect(element.firstChild).toHaveProperty('nodeValue', `Ready${WORD_JOINER}`);
  });

  it('skips empty text nodes when finding the announcement text', async () => {
    const text = document.createTextNode('Status');
    const empty = document.createTextNode('');
    const element = status();
    element.append(text, empty);

    markLiveRegion(element);
    await afterPaint();

    expect(text.data).toBe(`Status${WORD_JOINER}`);
    expect(empty.data).toBe('');
  });

  describe('with fake timers', () => {
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'requestAnimationFrame', 'cancelAnimationFrame'] });
    });

    /** Runs the animation frame and then the task the marker waits for, nothing later. */
    function paint() {
      vi.advanceTimersToNextFrame();
      vi.advanceTimersToNextTimer();
    }

    it('does not overwrite text that changes before the reset', () => {
      const element = status('Status');
      markLiveRegion(element);
      paint();
      element.firstChild!.nodeValue = 'Updated';

      vi.advanceTimersByTime(INITIAL_LIVE_REGION_TEXT_MUTATION_RESET_DELAY);

      expect(element.textContent).toBe('Updated');
    });

    it('restores the original text 200 ms after marking it', () => {
      const element = status('Status');
      markLiveRegion(element);
      paint();

      vi.advanceTimersByTime(INITIAL_LIVE_REGION_TEXT_MUTATION_RESET_DELAY - 1);
      expect(element.textContent).toBe(`Status${WORD_JOINER}`);

      vi.advanceTimersByTime(1);
      expect(element.textContent).toBe('Status');
    });

    it('removing the owner before the reset cancels the timer and restores the text at once', () => {
      const element = status('Status');
      const cleanup = markLiveRegion(element);
      paint();

      cleanup();

      expect(element.textContent).toBe('Status');
      expect(vi.getTimerCount()).toBe(0);
    });

    it('a cleanup before the paint marks nothing', () => {
      const element = status('Status');
      const cleanup = markLiveRegion(element);

      cleanup();
      paint();

      expect(element.textContent).toBe('Status');
    });

    it('marks nothing without a non-empty text node', () => {
      const element = status('', '');
      markLiveRegion(element);
      paint();

      expect(Array.from(element.childNodes, (node) => node.nodeValue)).toEqual(['', '']);
      expect(vi.getTimerCount()).toBe(0);
    });

    it('the twin marks on attach and whenever liveRegion turns on, and restores when it turns off', () => {
      const element = status('Status');
      const handle = attach(element, JSON.stringify({ liveRegion: true } satisfies DomOptions), createFakeTwin().twin);
      handles.push(handle);
      paint();
      expect(element.textContent).toBe(`Status${WORD_JOINER}`);

      handle.update(JSON.stringify({ liveRegion: false } satisfies DomOptions));
      expect(element.textContent).toBe('Status');

      handle.update(JSON.stringify({ liveRegion: true } satisfies DomOptions));
      paint();
      expect(element.textContent).toBe(`Status${WORD_JOINER}`);

      handle.dispose();
      expect(element.textContent).toBe('Status');
    });
  });

  it('marks nothing on iOS', async () => {
    vi.resetModules();
    vi.doMock('./platform', async (importOriginal) => {
      const actual = await importOriginal<typeof import('./platform')>();
      return { ...actual, platform: { ...actual.platform, os: { ...actual.platform.os, ios: true } } };
    });
    try {
      const { markLiveRegion: markOnIOS } = await import('./dom');
      const element = status('Status');

      markOnIOS(element);
      await afterPaint();

      expect(element.textContent).toBe('Status');
    } finally {
      vi.doUnmock('./platform');
      vi.resetModules();
    }
  });
});

describe('AvatarImage', () => {
  type MockImage = {
    complete: boolean;
    naturalWidth: number;
    onload: (() => void) | null;
    onerror: (() => void) | null;
    referrerPolicy: string;
    crossOrigin: string | null;
    sizes: string;
    src: string;
    srcset: string;
    assignments: string[];
  };

  /**
   * When `completeOnSet` is true, simulates cached-image behavior: setting a
   * source immediately marks the image as complete before an async load event.
   */
  function mockImageLoading({ completeOnSet = false, naturalWidth = 100 } = {}) {
    const images: MockImage[] = [];
    function MockImage() {
      let srcValue = '';
      let srcSetValue = '';
      let sizesValue = '';
      let crossOriginValue: string | null = null;
      let referrerPolicyValue = '';
      const obj: MockImage = {
        complete: false,
        naturalWidth: 0,
        onload: null,
        onerror: null,
        assignments: [],
        get referrerPolicy() {
          return referrerPolicyValue;
        },
        set referrerPolicy(value: string) {
          referrerPolicyValue = value;
          obj.assignments.push('referrerPolicy');
        },
        get crossOrigin() {
          return crossOriginValue;
        },
        set crossOrigin(value: string | null) {
          crossOriginValue = value;
          obj.assignments.push('crossOrigin');
        },
        get sizes() {
          return sizesValue;
        },
        set sizes(value: string) {
          sizesValue = value;
          obj.assignments.push('sizes');
        },
        get src() {
          return srcValue;
        },
        set src(value: string) {
          srcValue = value;
          obj.assignments.push('src');
          if (completeOnSet) {
            obj.complete = true;
            obj.naturalWidth = naturalWidth;
          }
        },
        get srcset() {
          return srcSetValue;
        },
        set srcset(value: string) {
          srcSetValue = value;
          obj.assignments.push('srcset');
          if (completeOnSet) {
            obj.complete = true;
            obj.naturalWidth = naturalWidth;
          }
        },
      };
      images.push(obj);
      return obj;
    }
    vi.stubGlobal('Image', MockImage);
    return { images };
  }

  function statuses(onStatus: ReturnType<typeof vi.fn>): ImageLoadingStatus[] {
    return onStatus.mock.calls.map(([status]) => status as ImageLoadingStatus);
  }

  it('passes responsive image props to the loading probe', () => {
    const imageMock = mockImageLoading();

    probeImage({ sizes: '48px', src: 'fallback.png', srcSet: 'avatar.png 1x, avatar@2x.png 2x' }, vi.fn());

    expect(imageMock.images[0]!.sizes).toBe('48px');
    expect(imageMock.images[0]!.srcset).toBe('avatar.png 1x, avatar@2x.png 2x');
    expect(imageMock.images[0]!.src).toBe('fallback.png');
  });

  it('configures the probe before assigning its source, and the source last', () => {
    const imageMock = mockImageLoading();

    probeImage({ src: 'a.png', srcSet: 'a.png 1x', sizes: '48px', crossOrigin: 'anonymous', referrerPolicy: 'no-referrer' }, vi.fn());
    probeImage({ src: 'b.png' }, vi.fn());

    expect(imageMock.images[0]!.assignments).toEqual(['referrerPolicy', 'crossOrigin', 'sizes', 'srcset', 'src']);
    expect(imageMock.images[0]!.crossOrigin).toBe('anonymous');
    // crossOrigin is always assigned; null means no CORS request.
    expect(imageMock.images[1]!.assignments).toEqual(['crossOrigin', 'src']);
    expect(imageMock.images[1]!.crossOrigin).toBeNull();
  });

  it('fires when the image loads', () => {
    const imageMock = mockImageLoading();
    const onStatus = vi.fn();

    probeImage({ src: 'avatar.png' }, onStatus);
    expect(statuses(onStatus)).toEqual(['loading']);

    imageMock.images.at(-1)?.onload?.();

    expect(statuses(onStatus)).toEqual(['loading', 'loaded']);
  });

  it('fires when the image errors', () => {
    const imageMock = mockImageLoading();
    const onStatus = vi.fn();

    probeImage({ src: 'avatar.png' }, onStatus);
    imageMock.images.at(-1)?.onerror?.();

    expect(statuses(onStatus)).toEqual(['loading', 'error']);
  });

  it('fires for cached image errors without emitting idle', () => {
    mockImageLoading({ completeOnSet: true, naturalWidth: 0 });
    const onStatus = vi.fn();

    probeImage({ src: 'avatar.png' }, onStatus);

    expect(statuses(onStatus)).toEqual(['error']);
  });

  it('shows the image immediately for a cached src', () => {
    mockImageLoading({ completeOnSet: true });
    const onStatus = vi.fn();

    probeImage({ src: 'https://example.com/cached-avatar.png' }, onStatus);

    expect(statuses(onStatus)).toEqual(['loaded']);
  });

  it('reports an error without a probe when there is no source', () => {
    const imageMock = mockImageLoading();
    const onStatus = vi.fn();

    probeImage({ sizes: '48px' }, onStatus);

    expect(statuses(onStatus)).toEqual(['error']);
    expect(imageMock.images).toHaveLength(0);
  });

  it('ignores the results of a superseded probe', () => {
    const imageMock = mockImageLoading();
    const onStatus = vi.fn();

    const supersede = probeImage({ src: 'a.png' }, onStatus);
    supersede();
    imageMock.images[0]!.onload?.();

    expect(statuses(onStatus)).toEqual(['loading']);
  });

  it('reports an error when there is no source', () => {
    const image = document.createElement('img');
    Object.defineProperty(image, 'complete', { configurable: true, value: true });

    expect(deriveImageStatus(image)).toBe('error');
  });

  it('derives the status of a rendered image from complete and naturalWidth', () => {
    const image = document.createElement('img');
    Object.defineProperty(image, 'complete', { configurable: true, writable: true, value: false });
    Object.defineProperty(image, 'naturalWidth', { configurable: true, writable: true, value: 0 });

    expect(deriveImageStatus(image)).toBe('loading');

    Object.assign(image, { complete: true, naturalWidth: 12 });
    expect(deriveImageStatus(image)).toBe('loaded');
    expect(deriveImageStatus(document.createElement('div'))).toBeNull();
  });

  describe('through the twin', () => {
    interface ImageStatus {
      status: ImageLoadingStatus;
    }

    it('sends one message per status change, coalescing the synchronous pass', () => {
      const imageMock = mockImageLoading();
      const root = document.createElement('span');
      document.body.appendChild(root);
      const fake = createFakeTwin();

      handles.push(attach(root, JSON.stringify({ image: { src: 'a.png', keepMounted: false } } satisfies DomOptions), fake.twin));
      imageMock.images[0]!.onload?.();

      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loading' }, { status: 'loaded' }]);
    });

    it('reports a cached image as loaded without reporting loading', () => {
      mockImageLoading({ completeOnSet: true });
      const fake = createFakeTwin();

      handles.push(attach(document.body.appendChild(document.createElement('span')), JSON.stringify({ image: { src: 'a.png', keepMounted: false } } satisfies DomOptions), fake.twin));

      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loaded' }]);
    });

    it('evaluates again with a fresh probe only when the image options change', () => {
      const imageMock = mockImageLoading();
      const fake = createFakeTwin();
      const handle = attach(document.body.appendChild(document.createElement('span')), JSON.stringify({ image: { src: 'a.png', keepMounted: false } } satisfies DomOptions), fake.twin);
      handles.push(handle);

      handle.update(JSON.stringify({ image: { src: 'a.png', keepMounted: false }, liveRegion: false } satisfies DomOptions));
      expect(imageMock.images).toHaveLength(1);

      handle.update(JSON.stringify({ image: { src: 'b.png', keepMounted: false } } satisfies DomOptions));
      imageMock.images[0]!.onload?.();
      imageMock.images[1]!.onerror?.();

      expect(imageMock.images).toHaveLength(2);
      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loading' }, { status: 'error' }]);
    });

    it('derives the status from the rendered image in kept-mounted mode without a probe', () => {
      const imageMock = mockImageLoading();
      const image = document.body.appendChild(document.createElement('img'));
      Object.defineProperty(image, 'complete', { configurable: true, value: true });
      Object.defineProperty(image, 'naturalWidth', { configurable: true, value: 40 });
      const fake = createFakeTwin();

      handles.push(attach(image, JSON.stringify({ image: { src: 'a.png', keepMounted: true } } satisfies DomOptions), fake.twin));

      expect(imageMock.images).toHaveLength(0);
      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loaded' }]);
    });

    it('hides the fallback the moment the image loads', () => {
      const imageMock = mockImageLoading();
      const fallback = document.body.appendChild(document.createElement('span'));
      fallback.id = 'base-ui-fallback';
      const fake = createFakeTwin();

      handles.push(
        attach(
          document.body.appendChild(document.createElement('span')),
          JSON.stringify({ image: { src: 'a.png', keepMounted: false, hideWhenLoaded: 'base-ui-fallback' } } satisfies DomOptions),
          fake.twin,
        ),
      );
      expect(fallback.hidden).toBe(false);

      imageMock.images[0]!.onload?.();

      expect(fallback.hidden).toBe(true);
    });

    /** An `<img>` whose `complete` and `naturalWidth` the test sets, as a browser reports them for its current source. */
    function renderedImage(complete: boolean, naturalWidth: number) {
      const image = document.body.appendChild(document.createElement('img'));
      const state = { complete, naturalWidth };
      Object.defineProperty(image, 'complete', { configurable: true, get: () => state.complete });
      Object.defineProperty(image, 'naturalWidth', { configurable: true, get: () => state.naturalWidth });
      return { image, state };
    }

    function fallbacks(...ids: string[]) {
      return ids.map((id) => {
        const fallback = document.body.appendChild(document.createElement('span'));
        fallback.id = id;
        return fallback;
      });
    }

    // AVA-STA-10, AVA-EDGE-02: every fallback of the root is hidden in the synchronous pass of a cached probe.
    it('does not flash fallback for a cached image during SSR hydration', () => {
      mockImageLoading({ completeOnSet: true });
      const [initials, icon] = fallbacks('base-ui-1', 'base-ui-2');
      const fake = createFakeTwin();

      handles.push(
        attach(
          document.body.appendChild(document.createElement('span')),
          JSON.stringify({ image: { src: 'cached.png', keepMounted: false, hideWhenLoaded: 'base-ui-1 base-ui-2' } } satisfies DomOptions),
          fake.twin,
        ),
      );

      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loaded' }]);
      expect(initials!.hidden).toBe(true);
      expect(icon!.hidden).toBe(true);
    });

    // AVA-ALG-02, AVA-EDGE-03: the first derivation over server HTML finds the image complete and hides the fallbacks.
    it('renders the image in the server HTML and resolves cached images on hydration', () => {
      const { image } = renderedImage(true, 40);
      image.setAttribute('src', 'cached.png');
      image.setAttribute('aria-hidden', 'true');
      const [initials, icon] = fallbacks('base-ui-1', 'base-ui-2');
      const fake = createFakeTwin();

      handles.push(attach(image, JSON.stringify({ image: { src: 'cached.png', keepMounted: true, hideWhenLoaded: 'base-ui-1 base-ui-2' } } satisfies DomOptions), fake.twin));

      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loaded' }]);
      expect(initials!.hidden).toBe(true);
      expect(icon!.hidden).toBe(true);
    });

    // AVA-ALG-02, AVA-STA-12, AVA-EVT-02: a render that changes the element's source (a `src` parameter, or a
    // substitute's own `src`) re-derives the status in the batch that applies it, before `update` arrives. The `error`
    // that follows is the element's event, which the part handles in C#.
    it('resets the status when the source changes to an unloaded one', async () => {
      const { image, state } = renderedImage(true, 40);
      image.setAttribute('src', 'cached.png');
      const fake = createFakeTwin();
      handles.push(attach(image, JSON.stringify({ image: { src: 'cached.png', keepMounted: true } } satisfies DomOptions), fake.twin));

      state.complete = false;
      state.naturalWidth = 0;
      image.setAttribute('src', '/missing-avatar.png');
      await Promise.resolve();

      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loaded' }, { status: 'loading' }]);
    });

    // AVA-STA-12, AVA-STA-14: the element's load and error events set the status in C#, which the module does not see,
    // so a source change reports what it derives even when that equals the module's last report.
    it('reports the derived status after every source change of a kept-mounted image', async () => {
      const { image, state } = renderedImage(false, 0);
      image.setAttribute('src', 'a.png');
      const fake = createFakeTwin();
      handles.push(attach(image, JSON.stringify({ image: { src: 'a.png', keepMounted: true } } satisfies DomOptions), fake.twin));

      // `a.png` loads (reported by the element's event in C#), then the source changes to an image still loading.
      state.complete = true;
      state.naturalWidth = 40;
      state.complete = false;
      image.setAttribute('src', 'b.png');
      await Promise.resolve();
      image.setAttribute('srcset', 'b.png 1x');
      await Promise.resolve();

      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loading' }, { status: 'loading' }, { status: 'loading' }]);

      // A disposed handle no longer observes the element.
      handles.pop()!.dispose();
      image.setAttribute('src', 'c.png');
      await Promise.resolve();
      expect(fake.reported<ImageStatus>('OnImageStatus')).toHaveLength(3);
    });

    it('stops listening to the probe on dispose', () => {
      const imageMock = mockImageLoading();
      const fake = createFakeTwin();
      const handle = attach(document.body.appendChild(document.createElement('span')), JSON.stringify({ image: { src: 'a.png', keepMounted: false } } satisfies DomOptions), fake.twin);

      handle.dispose();
      imageMock.images[0]!.onload?.();

      expect(fake.reported<ImageStatus>('OnImageStatus')).toEqual([{ status: 'loading' }]);
    });
  });
});

describe('CheckboxRoot', () => {
  function hiddenInput() {
    const input = document.createElement('input');
    input.type = 'checkbox';
    document.body.appendChild(input);
    return input;
  }

  it('sets the native input state when indeterminate', () => {
    const input = hiddenInput();

    handles.push(attach(input, JSON.stringify({ indeterminate: true } satisfies DomOptions), createFakeTwin().twin));

    expect(input.indeterminate).toBe(true);
  });

  it('re-asserts the property after a native click, inside the dispatch', () => {
    const input = hiddenInput();
    handles.push(attach(input, JSON.stringify({ indeterminate: true } satisfies DomOptions), createFakeTwin().twin));
    let seenByLaterListener: boolean | undefined;
    input.addEventListener('click', () => (seenByLaterListener = input.indeterminate));

    input.click();

    expect(input.checked).toBe(true);
    expect(seenByLaterListener).toBe(true);
    expect(input.indeterminate).toBe(true);
  });

  it('follows the value of every update and leaves the property alone without one', () => {
    const input = hiddenInput();
    const handle = attach(input, JSON.stringify({ indeterminate: true } satisfies DomOptions), createFakeTwin().twin);
    handles.push(handle);

    handle.update(JSON.stringify({ indeterminate: false } satisfies DomOptions));
    expect(input.indeterminate).toBe(false);

    input.indeterminate = true;
    handle.update(JSON.stringify({} satisfies DomOptions));
    input.click();
    expect(input.indeterminate).toBe(false);
  });
});

describe('dom module: twin handle', () => {
  it('attaches by id and touches nothing it was not asked to', () => {
    const element = document.body.appendChild(document.createElement('div'));
    element.id = 'base-ui-3';
    element.style.cssText = 'margin: 1px;';
    element.textContent = 'Status';

    const handle = attach('base-ui-3', JSON.stringify({ style: 'color: red' } satisfies DomOptions), createFakeTwin().twin);
    handle.update(JSON.stringify({} satisfies DomOptions));
    handle.dispose();
    handle.dispose();
    handle.update(JSON.stringify({ liveRegion: true } satisfies DomOptions));

    expect(element.style.cssText).toBe('margin: 1px;');
    expect(element.textContent).toBe('Status');
  });
});

// The `checked` property of a hidden checkbox or radio input is the `dom` primitive's after attach (ADR 04 Amendments
// 2026-10-22 item 2; SWT-DOM-08, SWT-EVT-04, SWT-STA-03). It is set on attach and on every update and is never re-asserted
// inside a native click, which would undo an accepted toggle before C# commits it; a request that leaves the value
// unchanged raises `checkedRestores`, so the update still arrives. Port titles: Base UI relies on React's controlled input.
describe.each([
  ['Server', false],
  ['WebAssembly', true],
])('SwitchRoot hidden input checked (%s)', (_mode, synchronous) => {
  function hiddenInput(checked = false) {
    const input = document.createElement('input');
    input.type = 'checkbox';
    input.checked = checked;
    document.body.appendChild(input);
    return input;
  }

  it('sets the checked property of the hidden input on attach', () => {
    const input = hiddenInput(false);

    handles.push(attach(input, JSON.stringify({ checked: true } satisfies DomOptions), createFakeTwin({ synchronous }).twin));

    expect(input.checked).toBe(true);
  });

  it('follows the value of every update', () => {
    const input = hiddenInput(false);
    const handle = attach(input, JSON.stringify({ checked: false } satisfies DomOptions), createFakeTwin({ synchronous }).twin);
    handles.push(handle);

    handle.update(JSON.stringify({ checked: true } satisfies DomOptions));
    expect(input.checked).toBe(true);

    handle.update(JSON.stringify({ checked: false } satisfies DomOptions));
    expect(input.checked).toBe(false);
  });

  it('keeps a native click until C# answers, then restores the unchanged value when the restore count rises', () => {
    const input = hiddenInput(false);
    const handle = attach(input, JSON.stringify({ checked: false, checkedRestores: 0 } satisfies DomOptions), createFakeTwin({ synchronous }).twin);
    handles.push(handle);

    input.click();
    expect(input.checked).toBe(true);

    handle.update(JSON.stringify({ checked: false, checkedRestores: 1 } satisfies DomOptions));
    expect(input.checked).toBe(false);
  });

  it('restores the property a native click changed on any later update', () => {
    const input = hiddenInput(true);
    const handle = attach(input, JSON.stringify({ checked: true } satisfies DomOptions), createFakeTwin({ synchronous }).twin);
    handles.push(handle);

    input.checked = false;
    handle.update(JSON.stringify({ checked: true, liveRegion: false } satisfies DomOptions));

    expect(input.checked).toBe(true);
  });
});

// switch.md §16 Q1 (b), chosen by the user 2026-10-22; FFI-FRM-30's dated note: a form reset puts the input back to its
// `checked` attribute (the value C# rendered at first), so the primitive re-asserts the property once the reset has run.
describe('SwitchRoot hidden input checked after a form reset', () => {
  function inputInForm() {
    const form = document.createElement('form');
    const input = document.createElement('input');
    input.type = 'checkbox';
    form.appendChild(input);
    document.body.appendChild(form);
    return { form, input };
  }

  it('re-asserts the checked property after the owning form resets', async () => {
    const { form, input } = inputInForm();
    handles.push(attach(input, JSON.stringify({ checked: true } satisfies DomOptions), createFakeTwin().twin));

    form.reset();
    expect(input.checked).toBe(false);
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(input.checked).toBe(true);
  });

  it('follows an external form named by the form attribute and stops on dispose', async () => {
    const { form } = inputInForm();
    form.id = 'external';
    const input = document.createElement('input');
    input.type = 'checkbox';
    input.setAttribute('form', 'external');
    document.body.appendChild(input);
    const handle = attach(input, JSON.stringify({ checked: true } satisfies DomOptions), createFakeTwin().twin);

    form.reset();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(input.checked).toBe(true);

    handle.dispose();
    form.reset();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(input.checked).toBe(false);
  });
});

// ADR 04 Amendments 2026-10-27 (Decision 2, chosen by the user; the bands settled by the orchestrator 2026-10-27 as
// design point 1 of the positioning track): one writer per element composes its whole `style` from the consumer's string
// and the modules' named layers. Under the consumer: `positioning`, then `transitions` (Base UI merges the positioner
// styles, then `transition: none`, then the consumer's props); over it: `positioning-measured`, then `auto-resize` (the
// variables Base UI writes with `setProperty` after render win, FLT-DOM-05).
describe('styleWriter', () => {
  function spiedElement(): { element: HTMLElement; writes: string[] } {
    const element = document.createElement('div');
    document.body.appendChild(element);
    const writes: string[] = [];
    const setProperty = element.style.setProperty.bind(element.style);
    const removeProperty = element.style.removeProperty.bind(element.style);
    vi.spyOn(element.style, 'setProperty').mockImplementation((name, value, priority) => {
      writes.push(`set ${name}`);
      setProperty(name, value, priority);
    });
    vi.spyOn(element.style, 'removeProperty').mockImplementation((name) => {
      writes.push(`remove ${name}`);
      return removeProperty(name);
    });
    return { element, writes };
  }

  it('composes the consumer between the layers under it and the layers over it', () => {
    const element = document.createElement('div');
    const writer = styleWriter(element);

    writer.layer('positioning').set('opacity', '0');
    writer.layer('positioning').set('position', 'fixed');
    writer.layer('positioning-measured').set('--available-height', '300px');
    writer.setConsumer('opacity: 0.5; --available-height: 10px; color: red');

    expect(element.style.getPropertyValue('opacity')).toBe('0.5');
    expect(element.style.getPropertyValue('position')).toBe('fixed');
    expect(element.style.getPropertyValue('--available-height')).toBe('300px');
    expect(element.style.getPropertyValue('color')).toBe('red');
  });

  it('orders the layers of a band: transitions over positioning, auto-resize over positioning-measured', () => {
    const element = document.createElement('div');
    const writer = styleWriter(element);

    writer.layer('transitions').set('transition', 'none');
    writer.layer('positioning').set('transition', 'opacity 1s');
    writer.layer('auto-resize').set('--positioner-width', '120px');
    writer.layer('positioning-measured').set('--positioner-width', '80px');

    expect(element.style.getPropertyValue('transition')).toBe('none');
    expect(element.style.getPropertyValue('--positioner-width')).toBe('120px');

    // TRN-DOM-05: a consumer inline `transition` wins over `transition: none`.
    writer.setConsumer('transition: transform 2s');
    expect(element.style.getPropertyValue('transition')).toBe('transform 2s');
  });

  it('writes only the properties whose winning value changed', () => {
    const { element, writes } = spiedElement();
    const writer = styleWriter(element);
    const positioning = writer.layer('positioning');

    positioning.set('transform', 'translate(1px, 2px)');
    positioning.set('transform', 'translate(1px, 2px)');
    writer.setConsumer('color: red');
    writer.setConsumer('color: red');
    expect(writes).toEqual(['set transform', 'set color']);

    // A layer under the consumer changing a property the consumer holds writes nothing.
    writer.setConsumer('color: red; opacity: 1');
    writes.length = 0;
    positioning.set('opacity', '0');
    positioning.set('transform', 'translate(3px, 2px)');
    expect(writes).toEqual(['set transform']);
  });

  it('hands a property back to the next writer when a layer removes it or is cleared, and removes it when no writer holds it', () => {
    const element = document.createElement('div');
    const writer = styleWriter(element);
    const positioning = writer.layer('positioning');
    const measured = writer.layer('positioning-measured');

    positioning.set('top', '0px');
    positioning.set('left', '0px');
    measured.set('--transform-origin', '10px 0px');
    writer.setConsumer('--transform-origin: center');

    measured.clear();
    expect(element.style.getPropertyValue('--transform-origin')).toBe('center');

    positioning.remove('top');
    expect(element.style.getPropertyValue('top')).toBe('');
    expect(element.style.getPropertyValue('left')).toBe('0px');

    positioning.clear();
    writer.setConsumer(null);
    expect(element.style.cssText).toBe('');
  });

  it('keeps one writer per element, so every module composes into the same style', () => {
    const element = document.createElement('div');

    styleWriter(element).layer('positioning').set('position', 'absolute');
    styleWriter(element).layer('transitions').set('transition', 'none');
    styleWriter(element).setConsumer('color: blue');

    expect(styleWriter(element)).toBe(styleWriter(element));
    expect(element.style.getPropertyValue('position')).toBe('absolute');
    expect(element.style.getPropertyValue('transition')).toBe('none');
    expect(element.style.getPropertyValue('color')).toBe('blue');
  });

  it('keeps the priority of an important consumer declaration', () => {
    const element = document.createElement('div');
    const writer = styleWriter(element);

    writer.layer('positioning-measured').set('--anchor-width', '72px');
    writer.setConsumer('--anchor-width: 10px !important; opacity: 0.5 !important');

    // An over layer still wins: Base UI's setProperty writes no priority and replaces the declaration.
    expect(element.style.getPropertyValue('--anchor-width')).toBe('72px');
    expect(element.style.getPropertyPriority('opacity')).toBe('important');
  });
});
