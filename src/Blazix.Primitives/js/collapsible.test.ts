// L3 (ADR 06 §1): the `collapsible` component module in jsdom against a fake twin. The one Base UI title that observes the
// module alone is COL-ANI-21's ("restores the inline transition duration when an instant open is interrupted", Base UI's
// React.Activity test); the port's own titles cover the passes the browser suite (tests/browser/collapsible.spec.ts) runs
// through the parts. jsdom computes no layout, so a test that needs a size defines `scrollHeight` and `scrollWidth`.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { attach, PANEL_HEIGHT, PANEL_WIDTH, type CollapsibleOptions } from './collapsible';
import { resetFrameScheduler, STARTING_STYLE, ENDING_STYLE } from './transitions';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

let frames: FrameRequestCallback[] = [];
const handles: TwinHandle[] = [];

beforeEach(() => {
  frames = [];
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    frames.push(callback);
    return frames.length;
  });
  globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
});

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  resetFrameScheduler();
  document.body.replaceChildren();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
});

function runFrame(): void {
  const pending = frames;
  frames = [];
  for (const callback of pending) {
    callback(0);
  }
}

/** Lets the MutationObserver callbacks of a render batch and the 0 ms timers run. */
function flushTasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

const defaults: CollapsibleOptions = {
  open: false,
  mounted: false,
  status: null,
  keepMounted: false,
  hiddenUntilFound: false,
  suppressOpenAnimation: false,
  panelId: 'panel',
  openSelector: '[data-open]',
  style: null,
};

/** A panel element as the part renders it: its id, its open attribute, and the size jsdom cannot compute. */
function createPanel(options: { open?: boolean; style?: string; size?: number } = {}): HTMLElement {
  const element = document.createElement('div');
  element.id = 'panel';
  element.toggleAttribute(options.open ? 'data-open' : 'data-closed', true);
  if (options.style) {
    element.setAttribute('style', options.style);
  }
  const size = options.size ?? 120;
  Object.defineProperty(element, 'scrollHeight', { configurable: true, get: () => size });
  Object.defineProperty(element, 'scrollWidth', { configurable: true, get: () => size * 2 });
  // jsdom has no getAnimations; the watcher awaits these (none) when the animations switch is off.
  element.getAnimations = () => [];
  document.body.append(element);
  return element;
}

function attachTo(element: HTMLElement, fake: FakeTwin, options: Partial<CollapsibleOptions> = {}): TwinHandle {
  const handle = attach(element, JSON.stringify({ ...defaults, ...options }), fake.twin);
  handles.push(handle);
  return handle;
}

function update(handle: TwinHandle, options: Partial<CollapsibleOptions>): void {
  handle.update(JSON.stringify({ ...defaults, ...options }));
}

/** What C#'s render batch writes on a toggle: the open attribute, read by the module after the batch. */
async function renderOpen(element: HTMLElement, open: boolean): Promise<void> {
  element.toggleAttribute('data-open', open);
  element.toggleAttribute('data-closed', !open);
  await flushTasks();
}

describe('<Collapsible.Panel />', () => {
  describe('interrupted beforematch opens', () => {
    it('restores the inline transition duration when an instant open is interrupted', async () => {
      const fake = createFakeTwin({ synchronous: true });
      const panel = createPanel({ style: 'transition-duration: 123ms' });
      const handle = attachTo(panel, fake, { hiddenUntilFound: true, keepMounted: true, style: 'transition-duration: 123ms' });

      panel.dispatchEvent(new Event('beforematch'));
      await renderOpen(panel, true);
      expect(panel.style.transitionDuration).toBe('0s');

      // The twin is disposed while the instant open still holds its override (the subtree was hidden).
      handle.dispose();

      expect(panel.style.transitionDuration).toBe('123ms');
    });
  });

  describe('the opening pass', () => {
    it('measures the panel and holds the size before the starting style is removed', async () => {
      const fake = createFakeTwin();
      const panel = createPanel({ style: 'transition-duration: 100ms' });
      attachTo(panel, fake, { keepMounted: true, style: 'transition-duration: 100ms' });
      expect(panel.style.getPropertyValue(PANEL_HEIGHT)).toBe('auto');

      await renderOpen(panel, true);

      expect(panel.hasAttribute(STARTING_STYLE)).toBe(true);
      expect(panel.style.getPropertyValue(PANEL_HEIGHT)).toBe('120px');
      expect(panel.style.getPropertyValue(PANEL_WIDTH)).toBe('240px');
      // The alignment that would distort the measurement is neutralised until the next frame.
      expect(panel.style.getPropertyPriority('justify-content')).toBe('important');

      runFrame();
      expect(panel.hasAttribute(STARTING_STYLE)).toBe(false);
      expect(panel.style.getPropertyValue('justify-content')).toBe('');
    });

    it('shows a no-motion open settled before paint and reports the forced idle', async () => {
      const fake = createFakeTwin();
      const panel = createPanel();
      attachTo(panel, fake, { keepMounted: true });

      await renderOpen(panel, true);

      expect(panel.hasAttribute(STARTING_STYLE)).toBe(false);
      expect(fake.reported('OnForcedIdleChange')).toEqual([{ forcedIdle: true }]);
      runFrame();
      expect(fake.reported('OnForcedIdleChange')).toEqual([{ forcedIdle: true }, { forcedIdle: false }]);
    });
  });

  describe('the closing passes', () => {
    it('ends a no-motion close at once and hides a kept panel ahead of the render', async () => {
      const fake = createFakeTwin();
      const panel = createPanel({ open: true });
      attachTo(panel, fake, { open: true, mounted: true, status: 'idle', keepMounted: true });

      await renderOpen(panel, false);

      expect(panel.hasAttribute('hidden')).toBe(true);
      expect(panel.hasAttribute(ENDING_STYLE)).toBe(false);
      expect(fake.reported('OnTransitionComplete')).toEqual([{ open: false }]);
    });

    it('holds the measured size for the close and ends a zero-size exit on the ending frame', async () => {
      const fake = createFakeTwin();
      const panel = createPanel({ open: true, style: 'transition-duration: 10s', size: 0 });
      attachTo(panel, fake, { open: true, mounted: true, status: 'idle', style: 'transition-duration: 10s' });

      await renderOpen(panel, false);
      expect(panel.style.getPropertyValue(PANEL_HEIGHT)).toBe('0px');
      expect(fake.reported('OnTransitionComplete')).toEqual([]);

      runFrame();
      expect(fake.reported('OnTransitionComplete')).toEqual([{ open: false }]);
    });

    it('renders the cached size when a keyframe panel closes from auto', async () => {
      const fake = createFakeTwin();
      const style = 'animation-name: slide; animation-duration: 100ms';
      const panel = createPanel({ style, size: 80 });
      attachTo(panel, fake, { keepMounted: true, style });

      await renderOpen(panel, true);
      runFrame();
      await flushTasks();
      runFrame();
      await flushTasks();
      // The open side's completion clears the held size; the measurement stays cached.
      expect(panel.style.getPropertyValue(PANEL_HEIGHT)).toBe('auto');

      Object.defineProperty(panel, 'scrollHeight', { configurable: true, get: () => 999 });
      const writes = vi.spyOn(panel.style, 'setProperty');
      await renderOpen(panel, false);

      // The first height the closing pass writes is the cached one, then the new measurement (COL-ANI-12).
      const heights = writes.mock.calls.filter(([property]) => property === PANEL_HEIGHT).map(([, value]) => value);
      expect(heights.slice(0, 2)).toEqual(['80px', '999px']);
    });
  });

  describe('the suppression of the opening keyframe animation', () => {
    it('keeps animation-name none while an initially open panel stays open and drops it on the first close', async () => {
      const fake = createFakeTwin();
      const panel = createPanel({ open: true, style: 'color: red' });
      attachTo(panel, fake, { open: true, mounted: true, status: 'idle', keepMounted: true, suppressOpenAnimation: true, style: 'color: red' });

      expect(panel.style.animationName).toBe('none');
      expect(panel.style.color).toBe('red');

      await renderOpen(panel, false);
      expect(panel.style.animationName).toBe('');
      await renderOpen(panel, true);
      expect(panel.style.animationName).toBe('');
    });
  });

  describe('the style', () => {
    it('composes the consumer style under its own declarations and follows a new consumer string', () => {
      const fake = createFakeTwin();
      const panel = createPanel({ open: true });
      const handle = attachTo(panel, fake, { open: true, mounted: true, status: 'idle', style: 'color: red; --collapsible-panel-height: 5px' });

      expect(panel.style.color).toBe('red');
      expect(panel.style.getPropertyValue(PANEL_HEIGHT)).toBe('auto');

      update(handle, { open: true, mounted: true, status: 'idle', style: 'color: blue' });
      expect(panel.style.color).toBe('blue');
      expect(panel.style.getPropertyValue(PANEL_HEIGHT)).toBe('auto');
    });
  });

  describe('a withheld or replaced element', () => {
    it('ends a close at once when no element is left in the ending phase, and finds a new one by the panel id', async () => {
      // With the watcher on, a no-motion open keeps its measured size until the frame after it.
      globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
      const fake = createFakeTwin();
      const panel = createPanel({ open: true });
      const handle = attachTo(panel, fake, { open: true, mounted: true, status: 'idle' });

      panel.remove();
      update(handle, { open: false, mounted: true, status: 'idle' });
      expect(fake.reported('OnTransitionComplete')).toEqual([]);
      update(handle, { open: false, mounted: true, status: 'ending' });
      expect(fake.reported('OnTransitionComplete')).toEqual([{ open: false }]);

      const replacement = createPanel({ open: true });
      update(handle, { open: true, mounted: true, status: 'starting' });
      expect(replacement.style.getPropertyValue(PANEL_HEIGHT)).toBe('120px');
    });

    it('a repeated attach of the same element returns the live handle and dispose is idempotent', () => {
      const fake = createFakeTwin();
      const panel = createPanel();
      const first = attachTo(panel, fake);
      const second = attach(panel, JSON.stringify(defaults), fake.twin);

      expect(second).toBe(first);
      first.dispose();
      first.dispose();
      expect(() => update(first, { open: true })).not.toThrow();
    });
  });

  describe('the detected type', () => {
    it('reports each detected type once, with the mixed flag when both motions are found', async () => {
      const fake = createFakeTwin();
      const style = 'transition-duration: 100ms; animation-name: fade; animation-duration: 100ms';
      const panel = createPanel({ style });
      attachTo(panel, fake, { keepMounted: true, style });

      await renderOpen(panel, true);

      expect(fake.reported('OnAnimationTypeChange')).toEqual([{ type: 'css-transition', mixed: true }]);
    });
  });

  describe('beforematch', () => {
    it('a canceled reveal keeps the starting style and arms no skip', async () => {
      const fake = createFakeTwin({ synchronous: true, verdict: 'stop' });
      const panel = createPanel({ style: 'transition-duration: 123ms' });
      attachTo(panel, fake, { hiddenUntilFound: true, keepMounted: true, style: 'transition-duration: 123ms' });
      expect(panel.getAttribute('hidden')).toBe('until-found');
      expect(panel.hasAttribute(STARTING_STYLE)).toBe(true);

      panel.dispatchEvent(new Event('beforematch'));
      panel.removeAttribute('hidden');
      expect(panel.hasAttribute(STARTING_STYLE)).toBe(true);
      expect(fake.decisions.map((decision) => decision.point)).toEqual(['reveal']);

      await flushTasks();
      runFrame();
      expect(panel.getAttribute('hidden')).toBe('until-found');

      // The next ordinary open keeps the authored duration.
      await renderOpen(panel, true);
      expect(panel.style.transitionDuration).toBe('123ms');
    });
  });
});
