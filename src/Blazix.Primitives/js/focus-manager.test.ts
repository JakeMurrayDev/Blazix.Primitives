// L3 (ADR 06 §1): the `focus-manager` module in jsdom against a fake twin. Titles are Base UI's where a Base UI test
// covers the behaviour (utils/tabbable.test.ts, utils/markOthers.test.ts, components/FloatingFocusManager.test.tsx);
// the port's own titles cover the rows Base UI leaves untested (floating-and-positioning.md FLT-FOC-16, FLT-FOC-20,
// FLT-FOC-22) and the port's inputs (the open state read from the applied batch, the resolved targets, the relay).
// The six `it.skipIf(isJSDOM)` titles of tabbable.test.ts run at L4 in tests/browser/focus-manager.spec.ts.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  enqueueFocus,
  getTabbableNearElement,
  isTabbable,
  markOthers,
  resetFocusManagerState,
  tabbable,
} from './focus-manager';
import { resetFrameScheduler } from './transitions';

// utils/visuallyHidden.ts, as React writes it onto `element.style` (unitless numbers become px).
const visuallyHiddenBase = {
  clipPath: 'inset(50%)',
  overflow: 'hidden',
  whiteSpace: 'nowrap',
  border: '0',
  padding: '0',
  width: '1px',
  height: '1px',
  margin: '-1px',
};
const visuallyHidden = { ...visuallyHiddenBase, position: 'fixed', margin: '0', top: '0', left: '0' };
const visuallyHiddenInput = { ...visuallyHiddenBase, position: 'absolute' };

// FloatingFocusManager.test.tsx:62-73 stubs the frame: callbacks run on the next macrotask.
beforeEach(() => {
  vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback: FrameRequestCallback) => {
    return window.setTimeout(() => callback(performance.now()), 0) as unknown as number;
  });
  vi.spyOn(window, 'cancelAnimationFrame').mockImplementation((id: number) => {
    window.clearTimeout(id);
  });
});

afterEach(() => {
  resetFocusManagerState();
  resetFrameScheduler();
  document.body.replaceChildren();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

/** Resolves after pending microtasks and one stubbed frame. */
function nextFrame(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0)).then(() => new Promise((resolve) => setTimeout(resolve, 0)));
}

describe('tabbable', () => {
  it('includes basic tabbable controls and excludes hidden inputs', () => {
    const button = document.createElement('button');
    const input = document.createElement('input');
    const hiddenInput = document.createElement('input');

    hiddenInput.type = 'hidden';
    document.body.append(button, input, hiddenInput);

    expect(tabbable(document.body)).toEqual([button, input]);
  });

  it('includes embedded focusable elements in the tab order', () => {
    const iframe = document.createElement('iframe');

    document.body.append(iframe);

    expect(tabbable(document.body)).toContain(iframe);
  });

  it('excludes disabled controls from the tab order', () => {
    const enabledButton = document.createElement('button');
    const disabledButton = document.createElement('button');

    disabledButton.disabled = true;
    document.body.append(enabledButton, disabledButton);

    expect(tabbable(document.body)).toEqual([enabledButton]);
  });

  it('includes slotted light DOM elements in the tab order', () => {
    const host = document.createElement('div');
    const shadowRoot = host.attachShadow({ mode: 'open' });
    const slot = document.createElement('slot');
    const button = document.createElement('button');

    shadowRoot.appendChild(slot);
    host.appendChild(button);
    document.body.appendChild(host);

    expect(tabbable(document.body)).toContain(button);
  });

  it('excludes unslotted light DOM elements from a shadow host tab order', () => {
    const host = document.createElement('div');
    const shadowRoot = host.attachShadow({ mode: 'open' });
    const shadowButton = document.createElement('button');
    const hiddenLightButton = document.createElement('button');

    shadowRoot.appendChild(shadowButton);
    host.appendChild(hiddenLightButton);
    document.body.appendChild(host);

    const tabbableElements = tabbable(document.body);

    expect(tabbableElements).toContain(shadowButton);
    expect(tabbableElements).not.toContain(hiddenLightButton);
  });

  it('keeps the summary tabbable but excludes closed details content', () => {
    const details = document.createElement('details');
    const summary = document.createElement('summary');
    const button = document.createElement('button');

    summary.textContent = 'Summary';
    button.textContent = 'Hidden';
    details.append(summary, button);
    document.body.append(details);

    expect(tabbable(document.body)).toContain(summary);
    expect(tabbable(document.body)).not.toContain(button);
  });

  it('keeps only the first summary tabbable and includes details without a summary', () => {
    const closedDetails = document.createElement('details');
    const closedSummary = document.createElement('summary');
    const hiddenButton = document.createElement('button');
    const openDetails = document.createElement('details');
    const openSummary = document.createElement('summary');
    const ignoredSummary = document.createElement('summary');
    const visibleButton = document.createElement('button');
    const summarylessDetails = document.createElement('details');

    openDetails.open = true;

    closedSummary.textContent = 'closed';
    openSummary.textContent = 'open';
    ignoredSummary.textContent = 'ignored';

    closedDetails.append(closedSummary, hiddenButton);
    openDetails.append(openSummary, ignoredSummary, visibleButton);
    summarylessDetails.textContent = 'summaryless';

    document.body.append(closedDetails, openDetails, summarylessDetails);

    expect(tabbable(document.body)).toEqual([closedSummary, openSummary, visibleButton, summarylessDetails]);
  });

  it('keeps aria-disabled elements in the tab order', () => {
    const element = document.createElement('div');

    element.tabIndex = 0;
    element.setAttribute('aria-disabled', 'true');
    document.body.append(element);

    expect(isTabbable(element)).toBe(true);
    expect(tabbable(document.body)).toContain(element);
  });

  it('excludes elements hidden with CSS visibility from the tab order', () => {
    const button = document.createElement('button');

    button.style.visibility = 'hidden';
    document.body.append(button);

    expect(isTabbable(button)).toBe(false);
    expect(tabbable(document.body)).not.toContain(button);
  });

  it('keeps descendants of display: contents ancestors in the tab order', () => {
    const wrapper = document.createElement('div');
    const dialog = document.createElement('div');
    const button = document.createElement('button');

    wrapper.style.display = 'contents';
    Object.defineProperty(wrapper, 'checkVisibility', {
      configurable: true,
      value: () => false,
    });
    dialog.setAttribute('role', 'dialog');
    dialog.append(button);
    wrapper.append(dialog);
    document.body.append(wrapper);

    expect(isTabbable(button)).toBe(true);
    expect(tabbable(document.body)).toContain(button);
  });

  it('excludes descendants of hidden display: contents ancestors from the tab order', () => {
    const wrapper = document.createElement('div');
    const button = document.createElement('button');

    wrapper.style.display = 'contents';
    wrapper.style.visibility = 'hidden';
    wrapper.append(button);
    document.body.append(wrapper);

    expect(isTabbable(button)).toBe(false);
    expect(tabbable(document.body)).not.toContain(button);
  });

  it('keeps descendants that override ancestor visibility in the tab order', () => {
    const wrapper = document.createElement('div');
    const button = document.createElement('button');

    wrapper.style.visibility = 'hidden';
    button.style.visibility = 'visible';
    wrapper.append(button);
    document.body.append(wrapper);

    expect(isTabbable(button)).toBe(true);
    expect(tabbable(document.body)).toContain(button);
  });

  it('excludes display: contents candidates when checkVisibility reports them hidden', () => {
    const button = document.createElement('button');

    button.style.display = 'contents';
    Object.defineProperty(button, 'checkVisibility', {
      configurable: true,
      value: () => false,
    });
    document.body.append(button);

    expect(isTabbable(button)).toBe(false);
    expect(tabbable(document.body)).not.toContain(button);
  });

  it('excludes display: contents candidates when checkVisibility is unavailable', () => {
    const button = document.createElement('button');

    button.style.display = 'contents';
    Object.defineProperty(button, 'checkVisibility', {
      configurable: true,
      value: undefined,
    });
    document.body.append(button);

    expect(isTabbable(button)).toBe(false);
    expect(tabbable(document.body)).not.toContain(button);
  });

  it('excludes descendants of display: none ancestors from the tab order', () => {
    const wrapper = document.createElement('div');
    const button = document.createElement('button');

    wrapper.style.display = 'none';
    wrapper.append(button);
    document.body.append(wrapper);

    expect(isTabbable(button)).toBe(false);
    expect(tabbable(document.body)).not.toContain(button);
  });

  it('keeps visuallyHidden elements in the tab order', () => {
    const button = document.createElement('button');

    Object.assign(button.style, visuallyHidden);
    document.body.append(button);

    expect(isTabbable(button)).toBe(true);
    expect(tabbable(document.body)).toContain(button);
  });

  it('keeps visuallyHiddenInput elements in the tab order', () => {
    const input = document.createElement('input');

    input.type = 'checkbox';
    Object.assign(input.style, visuallyHiddenInput);
    document.body.append(input);

    expect(isTabbable(input)).toBe(true);
    expect(tabbable(document.body)).toContain(input);
  });

  it('keeps only the checked radio in a named group', () => {
    const firstRadio = document.createElement('input');
    const checkedRadio = document.createElement('input');
    const button = document.createElement('button');

    firstRadio.type = 'radio';
    firstRadio.name = 'group';
    checkedRadio.type = 'radio';
    checkedRadio.name = 'group';
    checkedRadio.checked = true;

    document.body.append(firstRadio, checkedRadio, button);

    const tabbableElements = tabbable(document.body);

    expect(tabbableElements).not.toContain(firstRadio);
    expect(tabbableElements).toContain(checkedRadio);
    expect(tabbableElements).toContain(button);
  });

  it('keeps only the first radio when a named group has no checked item', () => {
    const firstRadio = document.createElement('input');
    const secondRadio = document.createElement('input');

    firstRadio.type = 'radio';
    firstRadio.name = 'group';
    secondRadio.type = 'radio';
    secondRadio.name = 'group';

    document.body.append(firstRadio, secondRadio);

    const tabbableElements = tabbable(document.body);

    expect(tabbableElements).toContain(firstRadio);
    expect(tabbableElements).not.toContain(secondRadio);
  });

  it('treats slotted elements inside inert shadow content as untabbable', () => {
    const host = document.createElement('div');
    const shadowRoot = host.attachShadow({ mode: 'open' });
    const wrapper = document.createElement('div');
    const slot = document.createElement('slot');
    const button = document.createElement('button');

    wrapper.setAttribute('inert', '');
    wrapper.appendChild(slot);
    shadowRoot.appendChild(wrapper);
    host.appendChild(button);
    document.body.appendChild(host);

    // The positive half first (the red check rejects absence-only titles): the same button is tabbable once the
    // wrapper is no longer inert.
    wrapper.removeAttribute('inert');
    expect(tabbable(document.body)).toContain(button);
    wrapper.setAttribute('inert', '');

    expect(tabbable(document.body)).not.toContain(button);
  });

  it.each(['shadow', 'slot'] as const)('finds adjacent controls around a non-tabbable anchor in a %s tree', (type) => {
    const before = document.createElement('button');
    const after = document.createElement('button');
    const host = document.createElement('div');
    const shadowRoot = host.attachShadow({ mode: 'open' });
    const anchor = document.createElement('button');
    anchor.tabIndex = -1;
    if (type === 'slot') {
      shadowRoot.appendChild(document.createElement('slot'));
      host.appendChild(anchor);
    } else {
      shadowRoot.appendChild(anchor);
    }
    document.body.append(before, host, after);

    expect(getTabbableNearElement(anchor, -1)).toBe(before);
    expect(getTabbableNearElement(anchor, 1)).toBe(after);
    expect(tabbable(document.body)).toEqual([before, after]);
    expect(anchor.tabIndex).toBe(-1);
  });

  it('finds adjacent controls around a disabled anchor', () => {
    const before = document.createElement('button');
    const anchor = document.createElement('button');
    const after = document.createElement('button');
    anchor.disabled = true;
    document.body.append(before, anchor, after);

    expect(getTabbableNearElement(anchor, -1)).toBe(before);
    expect(getTabbableNearElement(anchor, 1)).toBe(after);
  });

  it('does not choose a destination when the anchor is absent from the composed tree', () => {
    document.body.appendChild(document.createElement('button'));
    const detachedAnchor = document.createElement('button');

    // The positive half first: once connected, the same anchor finds the button before it.
    document.body.appendChild(detachedAnchor);
    expect(getTabbableNearElement(detachedAnchor, -1)).toBe(document.body.firstElementChild);
    detachedAnchor.remove();

    expect(getTabbableNearElement(detachedAnchor, -1)).toBe(null);
    expect(getTabbableNearElement(detachedAnchor, 1)).toBe(null);
  });

  it.each([
    [false, 1],
    [false, -1],
    [true, 1],
    [true, -1],
  ] as const)('does not let a disabled radio anchor suppress its peer when checked=%s and direction=%s', (checked, direction) => {
    const anchor = document.createElement('input');
    const peer = document.createElement('input');
    const other = document.createElement('button');
    anchor.type = 'radio';
    peer.type = 'radio';
    anchor.name = 'group';
    peer.name = 'group';
    anchor.disabled = true;
    anchor.checked = checked;
    document.body.append(...(direction === 1 ? [anchor, peer, other] : [other, peer, anchor]));

    expect(tabbable(document.body)).toEqual(direction === 1 ? [peer, other] : [other, peer]);
    expect(getTabbableNearElement(anchor, direction)).toBe(peer);
    expect(getTabbableNearElement(anchor, direction === 1 ? -1 : 1)).toBe(other);
  });
});

describe('markOthers', () => {
  it('single call', () => {
    const other = document.createElement('div');
    document.body.appendChild(other);
    const target = document.createElement('div');
    document.body.appendChild(target);

    const cleanup = markOthers([target], { ariaHidden: true });

    expect(other.getAttribute('aria-hidden')).toBe('true');

    cleanup();

    expect(other.getAttribute('aria-hidden')).toBe(null);
  });

  it('multiple calls', () => {
    const other = document.createElement('div');
    document.body.appendChild(other);
    const target = document.createElement('div');
    document.body.appendChild(target);

    const cleanup = markOthers([target], { ariaHidden: true });

    expect(other.getAttribute('aria-hidden')).toBe('true');

    const nextTarget = document.createElement('div');
    document.body.appendChild(nextTarget);

    const nextCleanup = markOthers([nextTarget], { ariaHidden: true });

    expect(target.getAttribute('aria-hidden')).toBe('true');
    expect(nextTarget.getAttribute('aria-hidden')).toBe(null);

    document.body.removeChild(nextTarget);

    nextCleanup();

    expect(target.getAttribute('aria-hidden')).toBe(null);
    expect(other.getAttribute('aria-hidden')).toBe('true');

    cleanup();

    expect(other.getAttribute('aria-hidden')).toBe(null);

    document.body.appendChild(nextTarget);
  });

  it('out of order cleanup', () => {
    const other = document.createElement('div');
    document.body.appendChild(other);
    const target = document.createElement('div');
    target.setAttribute('data-testid', '');
    document.body.appendChild(target);

    const cleanup = markOthers([target], { ariaHidden: true });

    expect(other.getAttribute('aria-hidden')).toBe('true');

    const nextTarget = document.createElement('div');
    document.body.appendChild(nextTarget);

    const nextCleanup = markOthers([nextTarget], { ariaHidden: true });

    expect(target.getAttribute('aria-hidden')).toBe('true');
    expect(nextTarget.getAttribute('aria-hidden')).toBe(null);

    cleanup();

    expect(nextTarget.getAttribute('aria-hidden')).toBe(null);
    expect(target.getAttribute('aria-hidden')).toBe('true');
    expect(other.getAttribute('aria-hidden')).toBe('true');

    nextCleanup();

    expect(nextTarget.getAttribute('aria-hidden')).toBe(null);
    expect(other.getAttribute('aria-hidden')).toBe(null);
    expect(target.getAttribute('aria-hidden')).toBe(null);
  });

  it('multiple cleanups with differing controlAttribute', () => {
    const other = document.createElement('div');
    document.body.appendChild(other);
    const target = document.createElement('div');
    target.setAttribute('data-testid', '1');
    document.body.appendChild(target);

    const cleanup = markOthers([target], { ariaHidden: true });

    expect(other.getAttribute('aria-hidden')).toBe('true');

    const target2 = document.createElement('div');
    target2.setAttribute('data-testid', '2');
    document.body.appendChild(target2);

    const cleanup2 = markOthers([target2]);

    expect(target.getAttribute('aria-hidden')).not.toBe('true');
    expect(target.getAttribute('data-base-ui-inert')).toBe('');

    cleanup();

    expect(other.getAttribute('aria-hidden')).toBe(null);

    cleanup2();

    expect(target.getAttribute('data-base-ui-inert')).toBe(null);
  });

  it('keeps aria-hidden until the last aria-hidden lock releases, alongside marker-only locks', () => {
    const other = document.createElement('div');
    document.body.appendChild(other);

    const A = document.createElement('div');
    document.body.appendChild(A);

    const B = document.createElement('div');
    document.body.appendChild(B);

    const C = document.createElement('div');
    document.body.appendChild(C);

    const cleanupA = markOthers([A], { ariaHidden: true });
    const cleanupB = markOthers([B], { ariaHidden: true });
    const cleanupC = markOthers([C]);

    expect(other.getAttribute('aria-hidden')).toBe('true');
    expect(other.getAttribute('data-base-ui-inert')).toBe('');

    cleanupC();

    expect(other.getAttribute('aria-hidden')).toBe('true');
    expect(other.getAttribute('data-base-ui-inert')).toBe('');

    cleanupB();

    expect(other.getAttribute('aria-hidden')).toBe('true');
    expect(other.getAttribute('data-base-ui-inert')).toBe('');

    cleanupA();

    expect(other.hasAttribute('aria-hidden')).toBe(false);
    expect(other.hasAttribute('data-base-ui-inert')).toBe(false);
  });

  it('preserves externally owned aria-hidden during overlapping aria-hidden locks', () => {
    const keep = document.createElement('div');
    const outside = document.createElement('div');
    outside.setAttribute('aria-hidden', 'true');
    // The positive half first: a sibling without its own aria-hidden is hidden by the same locks.
    const sibling = document.createElement('div');
    document.body.append(keep, outside, sibling);

    let cleanupFirst: (() => void) | undefined;
    let cleanupSecond: (() => void) | undefined;

    try {
      cleanupFirst = markOthers([keep], { ariaHidden: true, mark: false });
      cleanupSecond = markOthers([keep], { ariaHidden: true, mark: false });

      expect(sibling.getAttribute('aria-hidden')).toBe('true');
      expect(outside.getAttribute('aria-hidden')).toBe('true');

      cleanupFirst();
      cleanupFirst = undefined;

      expect(outside.getAttribute('aria-hidden')).toBe('true');

      cleanupSecond();
      cleanupSecond = undefined;

      expect(sibling.hasAttribute('aria-hidden')).toBe(false);
      expect(outside.getAttribute('aria-hidden')).toBe('true');
    } finally {
      cleanupSecond?.();
      cleanupFirst?.();
    }
  });

  it('does not let mark-only overlap disturb control cleanup bookkeeping', () => {
    const keep = document.createElement('div');
    const outside = document.createElement('div');
    document.body.append(keep, outside);

    let cleanupMarkOnly: (() => void) | undefined;
    let cleanupControlOnly: (() => void) | undefined;

    try {
      cleanupMarkOnly = markOthers([keep], { mark: true });
      cleanupControlOnly = markOthers([keep], { ariaHidden: true, mark: false });

      expect(outside.hasAttribute('data-base-ui-inert')).toBe(true);
      expect(outside.getAttribute('aria-hidden')).toBe('true');

      cleanupMarkOnly();
      cleanupMarkOnly = undefined;

      expect(outside.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(outside.getAttribute('aria-hidden')).toBe('true');

      cleanupControlOnly();
      cleanupControlOnly = undefined;

      expect(outside.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(outside.hasAttribute('aria-hidden')).toBe(false);
    } finally {
      cleanupControlOnly?.();
      cleanupMarkOnly?.();
    }
  });

  it('re-reads externally owned aria-hidden once every lock has released', () => {
    const keep = document.createElement('div');
    const outside = document.createElement('div');
    document.body.append(keep, outside);

    const cleanupFirst = markOthers([keep], { ariaHidden: true });
    expect(outside.getAttribute('aria-hidden')).toBe('true');
    cleanupFirst();
    expect(outside.hasAttribute('aria-hidden')).toBe(false);

    outside.setAttribute('aria-hidden', 'true');
    const cleanupSecond = markOthers([keep], { ariaHidden: true });
    cleanupSecond();
    expect(outside.getAttribute('aria-hidden')).toBe('true');

    outside.removeAttribute('aria-hidden');
    const cleanupThird = markOthers([keep], { ariaHidden: true });
    expect(outside.getAttribute('aria-hidden')).toBe('true');
    cleanupThird();
    expect(outside.hasAttribute('aria-hidden')).toBe(false);
  });

  it('re-reads externally owned aria-hidden while an unrelated lock stays held', () => {
    const keep = document.createElement('div');
    const outside = document.createElement('div');
    outside.setAttribute('aria-hidden', 'true');
    document.body.append(keep, outside);

    // Keeps the lock count above zero so the bookkeeping is never reset wholesale.
    const cleanupHeld = markOthers([keep, outside], { ariaHidden: true });

    try {
      const cleanupFirst = markOthers([keep], { ariaHidden: true });
      cleanupFirst();
      expect(outside.getAttribute('aria-hidden')).toBe('true');

      outside.removeAttribute('aria-hidden');
      const cleanupSecond = markOthers([keep], { ariaHidden: true });
      expect(outside.getAttribute('aria-hidden')).toBe('true');
      cleanupSecond();
      expect(outside.hasAttribute('aria-hidden')).toBe(false);
    } finally {
      cleanupHeld();
    }
  });

  it('leaves live regions, their ancestors, and scripts exposed', () => {
    const keep = document.createElement('div');
    const liveRegion = document.createElement('div');
    liveRegion.setAttribute('aria-live', 'polite');
    const liveWrapper = document.createElement('div');
    const nestedLiveRegion = document.createElement('div');
    nestedLiveRegion.setAttribute('aria-live', 'assertive');
    const liveSibling = document.createElement('div');
    liveWrapper.append(nestedLiveRegion, liveSibling);
    const script = document.createElement('script');
    document.body.append(keep, liveRegion, liveWrapper, script);

    const cleanup = markOthers([keep], { ariaHidden: true, mark: false });

    expect(liveRegion.hasAttribute('aria-hidden')).toBe(false);
    expect(liveWrapper.hasAttribute('aria-hidden')).toBe(false);
    expect(nestedLiveRegion.hasAttribute('aria-hidden')).toBe(false);
    expect(liveSibling.getAttribute('aria-hidden')).toBe('true');
    expect(script.hasAttribute('aria-hidden')).toBe(false);

    cleanup();

    expect(liveSibling.hasAttribute('aria-hidden')).toBe(false);
  });

  it('does not recurse infinitely with target inside anchor in shadow root', () => {
    const host = document.createElement('div');
    document.body.appendChild(host);
    // The positive half first: an element beside the host is hidden.
    const outside = document.createElement('div');
    document.body.appendChild(outside);

    const shadowRoot = host.attachShadow({ mode: 'open' });
    const anchor = document.createElement('a');
    anchor.href = 'https://floating-ui.com';

    const target = document.createElement('button');
    anchor.appendChild(target);
    shadowRoot.appendChild(anchor);

    // Infinite recursion would throw a RangeError here instead of returning.
    const cleanup = markOthers([target], { ariaHidden: true });

    expect(outside.getAttribute('aria-hidden')).toBe('true');
    // The host contains the target, so it must not have been hidden.
    expect(host.hasAttribute('aria-hidden')).toBe(false);

    cleanup();
  });

  it('uses shadow root host as avoid element when parent chain includes anchor', () => {
    const outside = document.createElement('div');
    document.body.appendChild(outside);

    const host = document.createElement('div');
    document.body.appendChild(host);

    const shadowRoot = host.attachShadow({ mode: 'open' });
    const anchor = document.createElement('a');
    anchor.href = 'https://floating-ui.com';

    const target = document.createElement('button');
    anchor.appendChild(target);
    shadowRoot.appendChild(anchor);

    const cleanup = markOthers([target], { ariaHidden: true });

    expect(outside.getAttribute('aria-hidden')).toBe('true');
    expect(host.getAttribute('aria-hidden')).toBe(null);

    cleanup();

    expect(outside.getAttribute('aria-hidden')).toBe(null);
  });

  // FLT-EDGE-05's page-global counters (ADR 04 Amendments 2026-10-27): the test reset drops every lock's bookkeeping,
  // so a lock a failed test left behind cannot keep a later test's elements hidden.
  it('resetFocusManagerState forgets the outstanding locks', () => {
    const keep = document.createElement('div');
    const outside = document.createElement('div');
    document.body.append(keep, outside);

    markOthers([keep], { ariaHidden: true });
    expect(outside.getAttribute('aria-hidden')).toBe('true');
    outside.removeAttribute('aria-hidden');
    outside.removeAttribute('data-base-ui-inert');

    resetFocusManagerState();
    const cleanup = markOthers([keep], { ariaHidden: true });
    expect(outside.getAttribute('aria-hidden')).toBe('true');
    cleanup();

    expect(outside.hasAttribute('aria-hidden')).toBe(false);
    expect(outside.hasAttribute('data-base-ui-inert')).toBe(false);
  });
});

// FLT-FOC-22 has no Base UI title; these are the port's.
describe('enqueueFocus', () => {
  it('focuses on the next frame, and a second call cancels the first', async () => {
    const first = document.createElement('button');
    const second = document.createElement('button');
    document.body.append(first, second);

    enqueueFocus(first);
    enqueueFocus(second);
    expect(document.activeElement).toBe(document.body);

    await nextFrame();

    expect(document.activeElement).toBe(second);
  });

  it('reads shouldFocus when the frame runs and skips the focus when it returns false', async () => {
    const first = document.createElement('button');
    const second = document.createElement('button');
    document.body.append(first, second);
    let allowed = true;

    enqueueFocus(first, { shouldFocus: () => allowed });
    await nextFrame();
    expect(document.activeElement).toBe(first);

    enqueueFocus(second, { shouldFocus: () => allowed });
    allowed = false;
    await nextFrame();

    expect(document.activeElement).toBe(first);
  });

  it('focuses at once with sync and cancels the pending frame', async () => {
    const first = document.createElement('button');
    const second = document.createElement('button');
    document.body.append(first, second);

    enqueueFocus(first);
    enqueueFocus(second, { sync: true });
    expect(document.activeElement).toBe(second);

    await nextFrame();

    expect(document.activeElement).toBe(second);
  });

  it('passes preventScroll to focus', async () => {
    const button = document.createElement('button');
    document.body.append(button);
    const focus = vi.spyOn(button, 'focus');

    enqueueFocus(button, { preventScroll: true });
    await nextFrame();

    expect(focus).toHaveBeenCalledWith({ preventScroll: true });
  });

  it('returns a cancel that drops only its own pending frame', async () => {
    const first = document.createElement('button');
    const second = document.createElement('button');
    document.body.append(first, second);

    const cancelFirst = enqueueFocus(first);
    enqueueFocus(second);
    cancelFirst();
    await nextFrame();
    expect(document.activeElement).toBe(second);

    const cancelThird = enqueueFocus(first);
    cancelThird();
    await nextFrame();

    expect(document.activeElement).toBe(second);
  });
});
