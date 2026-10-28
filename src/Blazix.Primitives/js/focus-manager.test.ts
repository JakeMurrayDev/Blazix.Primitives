// L3 (ADR 06 §1): the `focus-manager` module in jsdom against a fake twin. Titles are Base UI's where a Base UI test
// covers the behaviour (utils/tabbable.test.ts, utils/markOthers.test.ts, components/FloatingFocusManager.test.tsx);
// the port's own titles cover the rows Base UI leaves untested (floating-and-positioning.md FLT-FOC-16, FLT-FOC-20,
// FLT-FOC-22) and the port's inputs (the open state read from the applied batch, the resolved targets, the relay).
// The six `it.skipIf(isJSDOM)` titles of tabbable.test.ts run at L4 in tests/browser/focus-manager.spec.ts.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  FOCUS_OUT_CLOSE_REPORT,
  INTERACTION_TYPE_REPORT,
  attach,
  disableFocusInside,
  enableFocusInside,
  enqueueFocus,
  getTabbableNearElement,
  isTabbable,
  markOthers,
  resetFocusManagerState,
  tabbable,
  type FocusManagerOptions,
  type FocusOutCloseRequest,
  type InteractionTypeChange,
  type OpenChangeRelay,
} from './focus-manager';
import { attach as attachComposite, type CompositeOptions } from './composite';
import { attach as attachPortal, getPortalState } from './portal';
import { resetInteractionsForTest } from './interactions';
import { createPopupHarness, type PopupHarness } from './test-support/interactions-harness';
import { resetFrameScheduler } from './transitions';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

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

// FLT-DOM-18 on a composite item (design point 4, settled by the orchestrator 2026-10-27): the composite writes its
// roving value into the stash while the item carries it, so enabling focus inside restores one tab stop.
describe('the focus stash on composite items', () => {
  it('enableFocusInside restores a composite group with one tab stop after the stop moved while stashed', async () => {
    const container = element('div');
    const root = element('div', undefined, { id: 'stash-root' });
    const items = ['1', '2', '3'].map((id) => element('div', `item-${id}`, { id: `stash-item-${id}`, tabindex: '-1' }));
    root.append(...items);
    container.append(root, element('button', 'plain'));
    document.body.append(container);
    const options: CompositeOptions = {
      orientation: 'both',
      loopFocus: true,
      enableHomeAndEndKeys: false,
      stopEventPropagation: true,
      highlightItemOnHover: false,
      modifierKeys: [],
      disabledIndices: null,
      direction: 'ltr',
      selectOnNavigation: false,
      reportFocusLeave: false,
      reportHighlight: false,
      items: items.map((item) => ({ id: item.id, tabIndex: null })),
    };
    handles.push(attachComposite(root, JSON.stringify(options), createFakeTwin().twin));
    expect(items[0]!.getAttribute('tabindex')).toBe('0');

    disableFocusInside(container);
    expect(tabbable(container)).toEqual([]);
    items[0]!.focus();
    items[0]!.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    await Promise.resolve();
    enableFocusInside(container);

    expect(items.map((item) => item.getAttribute('tabindex'))).toEqual(['-1', '0', '-1']);
    expect(tabbable(container)).toEqual([items[1], byTestId('plain')]);
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

// ---------------------------------------------------------------------------------------------------------------
// The manager. The test plays C#: it renders the popup and the inside guards, renders `data-open` (the applied batch),
// sends `update` with the resolved targets, the close type and the relay before the batch that applies them (design
// point 1 (d), chosen by the user 2026-10-27), and disposes the handle when it unmounts the popup. A Base UI popup rendered under
// `{open && …}` is attached with `data-open` already rendered (the handover at attach) and disposed on close. A
// popup Base UI's tests focus carries `tabindex="-1"`, which every port popup renders (PSH-DOM-15).
// ---------------------------------------------------------------------------------------------------------------

interface Manager {
  popup: HTMLElement;
  handle: TwinHandle & { update(optionsJson: string, references?: ReadonlyArray<Element | null> | null): void };
  fake: FakeTwin;
  options: FocusManagerOptions;
  update(next: Partial<FocusManagerOptions>, references?: ReadonlyArray<Element | null>): void;
}

const handles: TwinHandle[] = [];
let revision = 0;

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  revision = 0;
});

function attachManager(
  popup: HTMLElement,
  options: FocusManagerOptions = {},
  { synchronous = false, references }: { synchronous?: boolean; references?: ReadonlyArray<Element | null> } = {},
): Manager {
  const fake = createFakeTwin({ synchronous });
  let current: FocusManagerOptions = { treatPopupAsFloatingElement: true, domReferenceId: 'reference', ...options };
  let currentReferences = references ?? [];
  const handle = attach(popup, JSON.stringify(current), fake.twin, references) as Manager['handle'];
  handles.push(handle);
  return {
    popup,
    handle,
    fake,
    get options() {
      return current;
    },
    // C# sends the whole references array with every update.
    update(next, nextReferences = currentReferences) {
      current = { ...current, ...next };
      currentReferences = nextReferences;
      handle.update(JSON.stringify(current), nextReferences);
    },
  };
}

function relay(reason: string, eventKind: string | null = null, eventType: string | null = null, pointerType: string | null = null): OpenChangeRelay {
  revision += 1;
  return { revision, reason, eventKind, eventType, pointerType, triggerId: null };
}

/** Lets the batch's observer, the module's microtasks and one stubbed frame run. */
async function settle(): Promise<void> {
  await nextFrame();
  await nextFrame();
}

function byTestId<T extends HTMLElement = HTMLElement>(id: string): T {
  const element = document.querySelector<T>(`[data-testid="${id}"]`);
  if (!element) {
    throw new Error(`no element ${id}`);
  }
  return element;
}

function element<K extends keyof HTMLElementTagNameMap>(tag: K, testId?: string, attributes: Record<string, string> = {}): HTMLElementTagNameMap[K] {
  const created = document.createElement(tag);
  if (testId) {
    created.setAttribute('data-testid', testId);
  }
  for (const [name, value] of Object.entries(attributes)) {
    created.setAttribute(name, value);
  }
  return created;
}

/** An inside focus guard as C# renders it (FLT-DOM-13). */
function insideGuard(): HTMLSpanElement {
  const guard = element('span', undefined, { tabindex: '0', 'data-base-ui-focus-guard': '', 'data-type': 'inside', 'aria-hidden': 'true' });
  Object.assign(guard.style, visuallyHidden);
  return guard;
}

/** Base UI's `App`: a reference, the dialog with three buttons, and an outside tabbable. */
function renderApp({ guards = true, children = [] as HTMLElement[], modal }: { guards?: boolean; children?: HTMLElement[]; modal?: boolean } = {}) {
  const reference = element('button', 'reference', { id: 'reference' });
  const floating = element('div', 'floating', { role: 'dialog' });
  const one = element('button', 'one');
  const two = element('button', 'two', { id: 'two' });
  const three = element('button', 'three');
  floating.append(one, two, three, ...children);
  const last = element('div', 'last', { tabindex: '0' });
  const before = guards && modal !== false ? insideGuard() : null;
  const after = guards && modal !== false ? insideGuard() : null;
  document.body.append(reference, last);
  return {
    reference,
    floating,
    one,
    two,
    three,
    last,
    /** Renders the popup with `data-open` before `last` (the opening batch) and attaches the manager (`OnAfterRender`). */
    open(options: FocusManagerOptions = {}, attachOptions?: Parameters<typeof attachManager>[2]): Manager {
      floating.setAttribute('data-open', '');
      last.before(...[before, floating, after].filter((x): x is HTMLElement => x != null));
      return attachManager(floating, { modal, ...options }, attachOptions);
    },
  };
}

/** C# closes a popup rendered under `{open && …}`: the close's options, the batch that removes it, then dispose. */
async function unmount(manager: Manager, next: Partial<FocusManagerOptions> = {}): Promise<void> {
  manager.update(next);
  const popup = manager.popup;
  popup.removeAttribute('data-open');
  for (const guard of [popup.previousElementSibling, popup.nextElementSibling]) {
    if (guard?.matches('[data-base-ui-focus-guard][data-type="inside"]')) {
      guard.remove();
    }
  }
  popup.remove();
  manager.handle.dispose();
  await settle();
}

/** Plays C#'s answer to a report inside the call, as WebAssembly runs it. */
function answerInside(manager: Manager, method: string, answer: () => void): void {
  const twin = manager.fake.twin;
  const send = twin.invokeMethodAsync.bind(twin);
  twin.invokeMethodAsync = <T>(methodName: string, ...args: unknown[]): Promise<T> => {
    const sent = send<T>(methodName, ...args);
    if (methodName === method) {
      answer();
    }
    return sent;
  };
}

/**
 * A focus move the browser makes for a press or Tab: `focusout` while the body is the active element, the microtask
 * checkpoint, then focus on the new element unless a handler moved focus meanwhile. jsdom's `focus()` keeps the old
 * element active through `focusout`, so the order is played by hand; `blur()`'s own `focusout` has no related target.
 */
async function browserFocusMove(from: HTMLElement, to: HTMLElement): Promise<void> {
  from.blur();
  from.dispatchEvent(new FocusEvent('focusout', { bubbles: true, composed: true, relatedTarget: to }));
  await new Promise((resolve) => setTimeout(resolve, 0));
  if (document.activeElement === document.body) {
    to.focus();
  }
}

/** userEvent.tab(): a cancelable Tab keydown on the focused element, then the browser's sequential focus move. */
async function tab({ shift = false, doc = document } = {}): Promise<void> {
  const active = (doc.activeElement ?? doc.body) as HTMLElement;
  const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey: shift, bubbles: true, cancelable: true });
  active.dispatchEvent(event);
  if (!event.defaultPrevented) {
    const list = tabbable(doc.body);
    const index = list.indexOf(active);
    const next = shift ? (index <= 0 ? list[list.length - 1] : list[index - 1]) : index === -1 || index === list.length - 1 ? list[0] : list[index + 1];
    next?.focus();
  }
  active.dispatchEvent(new KeyboardEvent('keyup', { key: 'Tab', shiftKey: shift, bubbles: true }));
  await settle();
}

function keyDown(target: Element, key: string): void {
  target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
}

function pointerDown(target: Element, pointerType = 'mouse'): void {
  const event = new MouseEvent('pointerdown', { bubbles: true, cancelable: true, composed: true });
  Object.defineProperty(event, 'pointerType', { value: pointerType });
  target.dispatchEvent(event);
}

/** An outside focus guard as C# renders it beside the portal's wrapper (FLT-DOM-13, DOM-14). */
function outsideGuard(): HTMLSpanElement {
  const guard = element('span', undefined, { tabindex: '0', 'data-base-ui-focus-guard': '', 'data-type': 'outside', 'aria-hidden': 'true' });
  Object.assign(guard.style, visuallyHidden);
  return guard;
}

interface RenderedPortal {
  wrapper: HTMLElement;
  node: HTMLElement;
  /** Renders the popup and its inside guards in the node with `data-open`, and attaches the manager. */
  open(floating: HTMLElement, options?: FocusManagerOptions): Manager;
  /** The Portal's render after the manager's publication changed: the outside guards and the owner (FLT-DOM-14). */
  syncOutsideGuards(): void;
  /** C# closes the popup rendered under `{open && …}` inside the portal, then the Portal renders again. */
  close(manager: Manager, next?: Partial<FocusManagerOptions>): Promise<void>;
}

/**
 * Base UI's `<FloatingPortal>` as C# renders it (FLT-DOM-11, DOM-14): the in-place wrapper (`display: contents`) holding
 * the hidden portal node, which the `portal` module moves into its container (the end of the body by default). The
 * popup and its inside guards render inside the node; the outside guards and the owner render beside the wrapper while
 * the manager's publication is non-modal and open.
 */
let portalIds = 0;

function renderPortal(place: (wrapper: HTMLElement) => void, { container }: { container?: Node } = {}): RenderedPortal {
  // C# renders in the top document, whatever the container.
  const doc = document;
  const wrapper = doc.createElement('div');
  wrapper.setAttribute('style', 'display: contents');
  const node = doc.createElement('div');
  portalIds += 1;
  node.id = `base-ui-portal-${portalIds}`;
  node.setAttribute('data-base-ui-portal', '');
  node.setAttribute('style', 'display: none');
  wrapper.append(node);
  place(wrapper);
  const portal = attachPortal(node, JSON.stringify(container ? { containerRef: 0 } : {}), createFakeTwin().twin, container ? [container] : undefined);
  handles.push(portal);
  let rendered: HTMLElement[] = [];
  const syncOutsideGuards = () => {
    const publication = getPortalState(node)?.focusManager;
    const renders = publication != null && !publication.modal && publication.open;
    if (renders && rendered.length === 0) {
      const before = outsideGuard();
      const owner = element('span', undefined, { 'aria-owns': node.id });
      const after = outsideGuard();
      wrapper.before(before, owner);
      wrapper.after(after);
      rendered = [before, owner, after];
    } else if (!renders && rendered.length > 0) {
      for (const each of rendered) {
        each.remove();
      }
      rendered = [];
    }
  };
  return {
    wrapper,
    node,
    open(floating, options = {}) {
      floating.setAttribute('data-open', '');
      node.append(insideGuard(), floating, insideGuard());
      const manager = attachManager(floating, { modal: false, ...options });
      syncOutsideGuards();
      return manager;
    },
    syncOutsideGuards,
    async close(manager, next = {}) {
      await unmount(manager, next);
      syncOutsideGuards();
    },
  };
}

describe('FloatingFocusManager', () => {
  describe('prop: initialFocus', () => {
    it('default behavior focuses first tabbable element', async () => {
      const app = renderApp();
      app.reference.focus();

      app.open();
      await settle();

      expect(document.activeElement).toBe(app.one);
    });

    it('default behavior focuses the checked radio in a named group', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', undefined, { role: 'dialog', 'data-open': '' });
      const radioOne = element('input', 'radio-one', { type: 'radio', name: 'group' });
      const radioTwo = element('input', 'radio-two', { type: 'radio', name: 'group' });
      radioTwo.checked = true;
      floating.append(radioOne, radioTwo, element('button', 'after-radio'));
      document.body.append(reference, floating);

      attachManager(floating);
      await settle();

      expect(document.activeElement).toBe(radioTwo);
    });

    // FLT-API-05's element case: the target crosses as the index of a reference passed with the options (ADR 04
    // Amendments 2026-10-27); its C# half is open until the popup core's references array is on `main`.
    it('ref', async () => {
      const app = renderApp();

      app.open({ initialFocusElement: 0 }, { references: [app.two] });
      await settle();

      expect(document.activeElement).toBe(app.two);
    });

    it('respects autoFocus', async () => {
      const input = element('input', 'input');
      const app = renderApp({ children: [input] });

      app.floating.setAttribute('data-open', '');
      app.last.before(app.floating);
      // The browser runs autofocus when the batch inserts the element, before the manager attaches.
      input.focus();
      attachManager(app.floating);
      await settle();

      expect(document.activeElement).toBe(input);
    });

    it('keeps the frame delay for a real mouse press', async () => {
      const app = renderApp();

      app.open();
      await Promise.resolve();
      await Promise.resolve();
      expect(document.activeElement).not.toBe(app.one);

      await settle();
      expect(document.activeElement).toBe(app.one);
    });

    // Port titles: the open's resolved target as C# sends it (FLT-API-05; ADR 04 Amendments 2026-10-27).
    it('false moves no focus', async () => {
      const app = renderApp();
      app.reference.focus();

      app.open({ initialFocus: false });
      await settle();
      expect(document.activeElement).toBe(app.reference);

      app.floating.removeAttribute('data-open');
      await settle();
      app.floating.setAttribute('data-open', '');
      handles[0]!.update(JSON.stringify({ treatPopupAsFloatingElement: true, domReferenceId: 'reference', initialFocus: true }));
      app.floating.removeAttribute('data-open');
      await settle();
      app.floating.setAttribute('data-open', '');
      await settle();
      expect(document.activeElement).toBe(app.one);
    });

    it('takes an element id as the target, and the default when the id names no element', async () => {
      const app = renderApp();

      const manager = app.open({ initialFocusId: 'two' });
      await settle();
      expect(document.activeElement).toBe(app.two);

      app.reference.focus();
      manager.update({ initialFocusId: 'missing' });
      app.floating.removeAttribute('data-open');
      await settle();
      app.floating.setAttribute('data-open', '');
      await settle();

      expect(document.activeElement).toBe(app.one);
    });

    it('falls back to the default when the reference index names no element', async () => {
      const app = renderApp();

      app.open({ initialFocusElement: 3 }, { references: [app.two] });
      await settle();

      expect(document.activeElement).toBe(app.one);
    });
  });

  describe('prop: returnFocus', () => {
    it('when true', async () => {
      const app = renderApp();
      app.reference.focus();

      const manager = app.open();
      await settle();
      expect(document.activeElement).toBe(app.one);

      app.two.focus();
      manager.update({ returnFocus: false });
      expect(document.activeElement).toBe(app.two);

      await unmount(manager);
      expect(document.activeElement).not.toBe(app.reference);
    });

    it('when false', async () => {
      const app = renderApp();
      app.reference.focus();

      const manager = app.open({ returnFocus: false });
      await settle();
      expect(document.activeElement).toBe(app.one);

      // The positive half first: with return focus on, the same close returns focus to the reference.
      await unmount(manager, { returnFocus: true });
      expect(document.activeElement).toBe(app.reference);

      app.reference.focus();
      const second = app.open({ returnFocus: false });
      await settle();
      await unmount(second);

      expect(document.activeElement).not.toBe(app.reference);
    });

    it('ref', async () => {
      const wrapper = element('div');
      const target = element('input', 'focus-target');
      wrapper.append(element('input'), target, element('input'));
      document.body.append(wrapper);
      const app = renderApp();
      app.reference.focus();

      const manager = app.open({}, { references: [] });
      await settle();

      manager.update({ returnFocusElement: 0 }, [target]);
      await unmount(manager);

      expect(document.activeElement).toBe(target);
    });

    it('return to the first focusable descendent of the reference, if the reference is not focusable', async () => {
      const reference = element('div', 'non-focusable-reference', { id: 'reference' });
      const openDialog = element('button', 'open-dialog');
      reference.append(openDialog);
      const floating = element('div', undefined, { 'data-open': '' });
      floating.append(element('button', 'close-dialog'));
      document.body.append(reference, floating);
      openDialog.focus();

      const manager = attachManager(floating);
      await settle();
      keyDown(document.activeElement!, 'Escape');

      await unmount(manager, { closeInteractionType: 'keyboard', relay: relay('escape-key', 'KeyboardEvent', 'keydown') });

      expect(document.activeElement).toBe(openDialog);
    });

    it('returns focus to reference on outside press when preventScroll is supported', async () => {
      const reference = element('button', undefined, { id: 'reference' });
      reference.textContent = 'reference';
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(reference, floating);
      reference.focus();

      const manager = attachManager(floating);
      await settle();
      expect(document.activeElement).toBe(floating);

      // An engine that supports the option reads it (FloatingFocusManager.test.tsx:550-558).
      const original = HTMLElement.prototype.focus;
      HTMLElement.prototype.focus = function focus(this: HTMLElement, focusOptions?: FocusOptions) {
        void focusOptions?.preventScroll;
        original.call(this, focusOptions);
      };
      try {
        pointerDown(document.body);
        await unmount(manager, { closeInteractionType: 'mouse', relay: relay('outside-press', 'PointerEvent', 'pointerdown', 'mouse') });
      } finally {
        HTMLElement.prototype.focus = original;
      }

      expect(document.activeElement).toBe(reference);
    });

    it('does not return focus to reference on outside press when preventScroll is not supported', async () => {
      const reference = element('button', undefined, { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(reference, floating);
      reference.focus();

      const manager = attachManager(floating);
      await settle();
      expect(document.activeElement).toBe(floating);

      // jsdom's focus() never reads `preventScroll`, which is what an engine without the option looks like.
      await unmount(manager, { closeInteractionType: 'mouse', relay: relay('outside-press', 'PointerEvent', 'pointerdown', 'mouse') });

      expect(document.activeElement).not.toBe(reference);
    });

    it('passes focusVisible when returning focus after keyboard close', async () => {
      const reference = element('button', undefined, { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(reference, floating);
      reference.focus();

      const manager = attachManager(floating);
      await settle();
      expect(document.activeElement).toBe(floating);

      const focusSpy = vi.spyOn(reference, 'focus');
      keyDown(floating, 'Escape');
      await unmount(manager, { closeInteractionType: 'keyboard', relay: relay('escape-key', 'KeyboardEvent', 'keydown') });

      expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true, focusVisible: true });
    });

    it('omits focusVisible when returning focus after pointer close', async () => {
      const reference = element('button', undefined, { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(reference, floating);
      reference.focus();

      const manager = attachManager(floating);
      await settle();
      expect(document.activeElement).toBe(floating);

      const focusSpy = vi.spyOn(reference, 'focus');
      pointerDown(reference);
      await unmount(manager, { closeInteractionType: 'mouse', relay: relay('trigger-press', 'MouseEvent', 'click', 'mouse') });

      expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true });
      expect(focusSpy).not.toHaveBeenCalledWith(expect.objectContaining({ focusVisible: true }));
    });

    it('does not return focus while open when the reference changes', async () => {
      const first = element('button', 'first', { id: 'first' });
      const second = element('button', 'second', { id: 'second' });
      const floating = element('div', undefined, { 'data-open': '' });
      const child = element('button', 'child');
      floating.append(child, element('button', 'close'));
      document.body.append(first, second, floating);
      first.focus();

      const manager = attachManager(floating, { domReferenceId: 'first' });
      await settle();
      child.focus();

      // The return-focus arming re-arms for the new reference while the popup stays open, so the cleanup's queued
      // return focus is cancelled rather than yanking focus mid-open.
      manager.update({ domReferenceId: 'second' });
      await settle();
      expect(document.activeElement).toBe(child);

      await unmount(manager);

      // The cancelled job must not have left return focus suppressed for the real close.
      expect(document.activeElement).toBe(second);
    });

    it('returns focus to last connected element', async () => {
      const parentReference = element('button', 'parent-reference', { id: 'parent-reference' });
      const parentFloating = element('div', undefined, { 'data-open': '' });
      const parentFloatingReference = element('button', 'parent-floating-reference');
      parentFloating.append(parentFloatingReference);
      document.body.append(parentReference, parentFloating);
      parentReference.focus();

      const parent = attachManager(parentFloating, { domReferenceId: 'parent-reference' });
      await settle();
      expect(document.activeElement).toBe(parentFloatingReference);

      // The click opens the drawer and closes the parent in one commit.
      const drawer = element('div', undefined, { 'data-open': '' });
      const childReference = element('button', 'child-reference');
      drawer.append(childReference);
      document.body.append(drawer);
      const child = attachManager(drawer, { domReferenceId: null });
      await unmount(parent);
      expect(document.activeElement).toBe(childReference);

      keyDown(childReference, 'Escape');
      child.update({ closeInteractionType: 'keyboard' });
      drawer.removeAttribute('data-open');
      drawer.remove();
      child.handle.dispose();
      await settle();

      expect(document.activeElement).toBe(parentReference);
    });

    // FLT-FOC-20 has no Base UI title: a programmatic open (`null`) prefers the element focused before the open.
    it('prefers the previously focused element over the reference after a programmatic open', async () => {
      const app = renderApp();
      const elsewhere = element('button', 'elsewhere');
      document.body.append(elsewhere);
      elsewhere.focus();

      const programmatic = app.open({ openInteractionType: null });
      await settle();
      await unmount(programmatic);
      expect(document.activeElement).toBe(elsewhere);

      const byKeyboard = app.open({ openInteractionType: 'keyboard' });
      await settle();
      await unmount(byKeyboard);

      expect(document.activeElement).toBe(app.reference);
    });
  });

  describe('prop: modal', () => {
    it('when true', async () => {
      const app = renderApp({ modal: true });

      app.open();
      await settle();

      await tab();
      expect(document.activeElement).toBe(app.two);

      await tab();
      expect(document.activeElement).toBe(app.three);

      await tab();
      expect(document.activeElement).toBe(app.one);

      await tab({ shift: true });
      expect(document.activeElement).toBe(app.three);

      await tab({ shift: true });
      expect(document.activeElement).toBe(app.two);

      await tab({ shift: true });
      expect(document.activeElement).toBe(app.one);

      await tab({ shift: true });
      expect(document.activeElement).toBe(app.three);

      await tab();
      expect(document.activeElement).toBe(app.one);
    });

    it('when false', async () => {
      const app = renderApp({ modal: false });

      const manager = app.open();
      await settle();

      await tab();
      expect(document.activeElement).toBe(app.two);

      await tab();
      expect(document.activeElement).toBe(app.three);

      await tab();

      // Focus leaving the floating element asks C# to close it.
      expect(manager.fake.reported<FocusOutCloseRequest>(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
      expect(manager.fake.reported<FocusOutCloseRequest>(FOCUS_OUT_CLOSE_REPORT)[0]!.event).toMatchObject({ kind: 'FocusEvent', type: 'focusout' });
      expect(document.activeElement).toBe(app.last);
    });

    it('closeOnFocusOut: false keeps a non-modal element open when focus leaves', async () => {
      const app = renderApp({ modal: false });

      const manager = app.open({ closeOnFocusOut: false });
      await settle();

      await tab();
      expect(document.activeElement).toBe(app.two);

      await tab();
      expect(document.activeElement).toBe(app.three);

      // Move focus out of the floating element entirely.
      await tab();

      // The positive half first: the same tab sequence with the option on asks for the close.
      expect(document.activeElement).toBe(app.last);
      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);
      manager.update({ closeOnFocusOut: true });
      app.three.focus();
      await tab();
      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
    });

    it('clicking a nested click trigger does not suppress the next focus-out close', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { role: 'dialog', 'data-open': '' });
      const nestedTrigger = element('button', 'nested-trigger', { 'data-base-ui-click-trigger': '' });
      floating.append(nestedTrigger);
      const last = element('button', 'last');
      document.body.append(reference, floating, last);
      reference.focus();

      const manager = attachManager(floating, { modal: false });
      await settle();

      pointerDown(nestedTrigger);
      nestedTrigger.focus();
      await settle();

      await tab();

      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
      expect(document.activeElement).toBe(last);
    });

    it('false - comboboxes do not hide all other nodes', async () => {
      const reference = element('input', 'reference', { role: 'combobox', id: 'reference' });
      const btn1 = element('button', 'btn-1');
      const btn2 = element('button', 'btn-2');
      const floating = element('div', 'floating', { role: 'listbox', 'data-open': '' });
      document.body.append(reference, btn1, btn2, floating);

      attachManager(floating, { modal: false });
      await settle();

      // The marker is the only write in non-modal mode; `inert` and `aria-hidden` are never written.
      expect(btn1.hasAttribute('data-base-ui-inert')).toBe(true);
      expect(reference.hasAttribute('inert')).toBe(false);
      expect(floating.hasAttribute('inert')).toBe(false);
      expect(btn1.hasAttribute('inert')).toBe(false);
      expect(btn2.hasAttribute('inert')).toBe(false);
      expect(btn1.hasAttribute('aria-hidden')).toBe(false);
    });

    it('fallback to floating element when it has no tabbable content', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(reference, insideGuard(), floating, insideGuard());

      attachManager(floating, { modal: true });
      await settle();

      expect(document.activeElement).toBe(floating);
      await tab();
      expect(document.activeElement).toBe(floating);
      await tab({ shift: true });
      expect(document.activeElement).toBe(floating);
    });

    it('true - applies aria-hidden to outside nodes', async () => {
      const reference = element('input', 'reference', { id: 'reference' });
      const wrapper = element('div', 'outside-wrapper');
      const live = element('div', 'aria-live', { 'aria-live': 'polite' });
      const btn1 = element('button', 'btn-1');
      const btn2 = element('button', 'btn-2');
      wrapper.append(live, btn1, btn2);
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(reference, wrapper, floating);

      const manager = attachManager(floating);
      await settle();

      expect(reference.getAttribute('aria-hidden')).toBe('true');
      expect(floating.hasAttribute('aria-hidden')).toBe(false);
      expect(live.hasAttribute('aria-hidden')).toBe(false);
      expect(btn1.getAttribute('aria-hidden')).toBe('true');
      expect(btn2.getAttribute('aria-hidden')).toBe('true');

      await unmount(manager);

      expect(reference.hasAttribute('aria-hidden')).toBe(false);
      expect(live.hasAttribute('aria-hidden')).toBe(false);
      expect(btn1.hasAttribute('aria-hidden')).toBe(false);
      expect(btn2.hasAttribute('aria-hidden')).toBe(false);
    });

    // `getInsideElements` crosses as references (or ids) the options name.
    it('true - keeps supplied inside elements outside the floating node exposed to assistive tech', async () => {
      const reference = element('input', 'reference', { id: 'reference' });
      const wrapper = element('div', 'outside-wrapper');
      wrapper.append(element('button', 'outside-button'));
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      const dismiss = element('button', 'dismiss');
      document.body.append(reference, wrapper, floating, dismiss);

      attachManager(floating, { insideElements: [0] }, { references: [dismiss] });
      await settle();

      expect(floating.hasAttribute('aria-hidden')).toBe(false);
      expect(dismiss.hasAttribute('aria-hidden')).toBe(false);
      expect(wrapper.getAttribute('aria-hidden')).toBe('true');
    });

    it('false - does not apply inert to outside nodes', async () => {
      const reference = element('input', 'reference', { id: 'reference' });
      const wrapper = element('div', 'outside-wrapper');
      const live = element('div', 'aria-live', { 'aria-live': 'polite' });
      const btn1 = element('button', 'btn-1');
      const btn2 = element('button', 'btn-2');
      wrapper.append(live, btn1, btn2);
      const floating = element('div', 'floating', { role: 'listbox', 'data-open': '' });
      document.body.append(reference, wrapper, floating);

      const manager = attachManager(floating, { modal: false });
      await settle();

      expect(floating.hasAttribute('inert')).toBe(false);
      expect(live.hasAttribute('inert')).toBe(false);
      expect(btn1.hasAttribute('inert')).toBe(false);
      expect(btn2.hasAttribute('inert')).toBe(false);
      expect(reference.hasAttribute('data-base-ui-inert')).toBe(true);
      expect(wrapper.hasAttribute('data-base-ui-inert')).toBe(true);
      expect(btn1.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(btn2.hasAttribute('data-base-ui-inert')).toBe(false);

      await unmount(manager);

      expect(reference.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(wrapper.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(btn1.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(btn2.hasAttribute('data-base-ui-inert')).toBe(false);
    });

    it('false - keeps marker on top-level outside ancestor when reference has siblings', async () => {
      const wrapper = element('div', 'outside-wrapper');
      const reference = element('input', 'reference', { id: 'reference' });
      const btn1 = element('button', 'btn-1');
      const btn2 = element('button', 'btn-2');
      const nested = element('div', 'nested-wrapper');
      const nestedBtn = element('button', 'nested-btn');
      nested.append(nestedBtn);
      wrapper.append(reference, btn1, btn2, nested);
      const sibling = element('div', 'outside-sibling');
      const floating = element('div', 'floating', { role: 'listbox', 'data-open': '' });
      document.body.append(wrapper, sibling, floating);

      const manager = attachManager(floating, { modal: false });
      await settle();

      expect(floating.hasAttribute('inert')).toBe(false);
      expect(wrapper.hasAttribute('data-base-ui-inert')).toBe(true);
      expect(sibling.hasAttribute('data-base-ui-inert')).toBe(true);
      expect(reference.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(btn1.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(btn2.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(nested.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(nestedBtn.hasAttribute('data-base-ui-inert')).toBe(false);

      await unmount(manager);

      expect(wrapper.hasAttribute('data-base-ui-inert')).toBe(false);
      expect(sibling.hasAttribute('data-base-ui-inert')).toBe(false);
    });

    it('trapped combobox prevents focus moving outside floating element', async () => {
      const reference = element('input', 'input', { role: 'combobox', id: 'reference' });
      const floating = element('div', 'floating', { role: 'listbox', 'data-open': '' });
      const inside = element('button', 'inside');
      floating.append(inside);
      const outside = element('button', 'outside');
      document.body.append(reference, insideGuard(), floating, insideGuard(), outside);
      reference.focus();

      attachManager(floating, { modal: true });
      await settle();
      expect(document.activeElement).toBe(inside);

      await tab();
      expect(document.activeElement).not.toBe(outside);
      expect(document.activeElement).toBe(inside);
    });
  });

  describe('prop: disabled', () => {
    it('true -> false', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { role: 'dialog', 'data-open': '' });
      document.body.append(reference, floating);

      const manager = attachManager(floating, { disabled: true });
      await settle();
      expect(document.activeElement).not.toBe(floating);

      manager.update({ disabled: false });
      await settle();

      expect(document.activeElement).toBe(floating);
    });

    it('when false', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(reference, floating);

      attachManager(floating, { disabled: false });
      await settle();

      expect(document.activeElement).toBe(floating);
    });

    it('supports keepMounted behavior', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating');
      const child = element('button', 'child');
      floating.append(child);
      const after = element('button', 'after');
      document.body.append(reference, floating, after);

      const manager = attachManager(floating, { disabled: true, modal: false });
      await settle();
      expect(document.activeElement).not.toBe(floating);

      reference.focus();
      manager.update({ disabled: false });
      floating.setAttribute('data-open', '');
      await settle();
      expect(document.activeElement).toBe(child);

      await tab();
      expect(document.activeElement).toBe(after);

      // Tabbing out closed it (a request C# commits): the close's options, then the batch.
      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
      manager.update({ disabled: true });
      floating.removeAttribute('data-open');
      await settle();

      await tab({ shift: true });
      reference.focus();
      manager.update({ disabled: false, openInteractionType: 'mouse' });
      floating.setAttribute('data-open', '');
      await settle();
      expect(document.activeElement).toBe(child);

      keyDown(child, 'Escape');
      manager.update({ disabled: true, closeInteractionType: 'keyboard', relay: relay('escape-key', 'KeyboardEvent', 'keydown') });
      floating.removeAttribute('data-open');
      await settle();

      expect(document.activeElement).toBe(reference);
    });

    it('treats a returnFocus resolver returning true as explicit', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const close = element('button', 'close');
      const floating = element('div', undefined, { 'data-open': '' });
      const child = element('button', 'child');
      floating.append(child);
      document.body.append(reference, close, floating);
      reference.focus();

      // C# resolved the function to `true` and marks it explicit, as Base UI counts a function.
      const manager = attachManager(floating, { returnFocus: true, explicitReturnFocus: true });
      await settle();
      expect(document.activeElement).toBe(child);

      close.focus();
      await unmount(manager);

      expect(document.activeElement).toBe(reference);
    });

    it('uses the latest explicitReturnFocus value without moving focus while open', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const close = element('button', 'close');
      const floating = element('div', undefined, { 'data-open': '' });
      const child = element('button', 'child');
      floating.append(child);
      document.body.append(reference, close, floating);
      reference.focus();

      const manager = attachManager(floating, { explicitReturnFocus: true, modal: false, closeOnFocusOut: false });
      await settle();
      expect(document.activeElement).toBe(child);

      manager.update({ explicitReturnFocus: false });
      expect(document.activeElement).toBe(child);

      close.focus();
      await unmount(manager);
      expect(document.activeElement).toBe(close);
    });
  });

  // FLT-FOC-10's two titles are restated (dated note on FLT-FOC-10): the close type is classified in C# from the
  // request's snapshot and the type this module records; here, the recording and its reset per open session.
  describe('the recorded interaction type', () => {
    it('resets close modality between keep-mounted open sessions', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div');
      const child = element('button', 'child');
      floating.append(child);
      document.body.append(reference, floating);

      const manager = attachManager(floating, { disabled: true });
      manager.update({ disabled: false });
      floating.setAttribute('data-open', '');
      await settle();
      expect(document.activeElement).toBe(child);

      keyDown(child, 'Escape');
      expect(manager.fake.reported<InteractionTypeChange>(INTERACTION_TYPE_REPORT).map((r) => r.type)).toEqual(['keyboard']);
      manager.update({ disabled: true, closeInteractionType: 'keyboard' });
      floating.removeAttribute('data-open');
      await settle();

      manager.update({ disabled: false });
      floating.setAttribute('data-open', '');
      await settle();

      // The new session starts empty, so a programmatic close classifies as `''`, not as the last session's keyboard.
      expect(manager.fake.reported<InteractionTypeChange>(INTERACTION_TYPE_REPORT).map((r) => r.type)).toEqual(['keyboard', '']);

      pointerDown(reference, 'mouse');
      expect(manager.fake.reported<InteractionTypeChange>(INTERACTION_TYPE_REPORT).map((r) => r.type)).toEqual(['keyboard', '', 'mouse']);
    });

    // Base UI reopens from a layout effect, a second commit that its act() flushes after the return job's microtask.
    // In the port the reopen is a later message (Server) or a later render (WebAssembly), so the job has already
    // captured the close's type when the reopen's options reset the recording (dated note on FLT-FOC-10).
    it('preserves keyboard close modality when reopening before focus restoration', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div');
      const child = element('button', 'child');
      floating.append(child);
      document.body.append(reference, floating);
      reference.focus();

      const manager = attachManager(floating, { disabled: true });
      manager.update({ disabled: false });
      floating.setAttribute('data-open', '');
      await settle();
      expect(document.activeElement).toBe(child);

      const focusSpy = vi.spyOn(reference, 'focus');
      keyDown(child, 'Escape');
      manager.update({ disabled: true, closeInteractionType: 'keyboard' });
      floating.removeAttribute('data-open');
      // The reopen's render clears the close type before the job runs; the job keeps the type it captured.
      manager.update({ closeInteractionType: null });
      await Promise.resolve();
      manager.update({ disabled: false, openInteractionType: 'mouse' });
      floating.setAttribute('data-open', '');
      await settle();

      expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true, focusVisible: true });
      expect(floating.hasAttribute('data-open')).toBe(true);
      expect(document.activeElement).toBe(child);
    });

    it('reports a recorded type once per change', async () => {
      const app = renderApp();

      const manager = app.open();
      await settle();

      pointerDown(app.one, 'touch');
      pointerDown(app.two, 'touch');
      keyDown(app.two, 'a');
      keyDown(app.two, 'b');
      pointerDown(app.two, '');

      // An empty pointer type is a keyboard or assistive press (FloatingFocusManager.tsx:386-387).
      expect(manager.fake.reported<InteractionTypeChange>(INTERACTION_TYPE_REPORT).map((r) => r.type)).toEqual(['touch', 'keyboard']);
    });
  });

  describe('Navigation', () => {
    // The `useHover` shim becomes a hover open and close carried by the relay (dated note on FLT-FOC-08).
    it('does not focus reference when hovering it', async () => {
      const app = renderApp({ modal: false });

      const manager = app.open({ initialFocus: false, openInteractionType: 'mouse' });
      await settle();
      await unmount(manager, { closeInteractionType: 'mouse', relay: relay('trigger-hover', 'MouseEvent', 'mouseleave', 'mouse') });

      // The positive half: a press close of the same popup returns focus.
      const pressed = app.open({ initialFocus: false });
      app.one.focus();
      await settle();
      await unmount(pressed, { relay: relay('trigger-press', 'MouseEvent', 'click', 'mouse') });
      expect(document.activeElement).toBe(app.reference);

      app.reference.blur();
      const hovered = app.open({ initialFocus: false, openInteractionType: 'mouse' });
      await settle();
      await unmount(hovered, { relay: relay('trigger-hover', 'MouseEvent', 'mouseleave', 'mouse') });

      expect(document.activeElement).not.toBe(app.reference);
    });

    it('returns focus to reference when floating element was opened by hover but is closed by esc key', async () => {
      const app = renderApp({ modal: false });

      const manager = app.open({ openInteractionType: 'mouse' });
      await settle();
      keyDown(document.activeElement!, 'Escape');
      await unmount(manager, { closeInteractionType: 'keyboard', relay: relay('escape-key', 'KeyboardEvent', 'keydown') });

      expect(document.activeElement).toBe(app.reference);
    });

    it('returns focus to reference when floating element was opened by hover but is closed by an explicit close button', async () => {
      const app = renderApp({ modal: false });

      const manager = app.open({ openInteractionType: 'mouse' });
      await settle();
      app.three.focus();
      keyDown(app.three, 'Enter');
      await unmount(manager, { closeInteractionType: 'keyboard', relay: relay('none', 'MouseEvent', 'click') });

      expect(document.activeElement).toBe(app.reference);
    });

    it('focus does not return to reference when floating element is triggered by hover', async () => {
      const app = renderApp({ modal: false });
      app.reference.focus();

      const manager = app.open({ openInteractionType: 'mouse' });
      await settle();
      expect(document.activeElement).toBe(app.one);

      await unmount(manager, { closeInteractionType: 'mouse', relay: relay('trigger-hover', 'MouseEvent', 'mouseleave', 'mouse') });

      expect(document.activeElement).not.toBe(app.reference);
    });
  });

  describe('prop: restoreFocus', () => {
    it('restores focus to the nearest tabbable element when the focused element becomes hidden', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      const one = element('button');
      const two = element('button', undefined, { id: 'two' });
      const three = element('button');
      floating.append(one, two, three);
      document.body.append(reference, floating);

      attachManager(floating, { restoreFocus: 'tabbable', initialFocusId: 'two' });
      await settle();
      expect(document.activeElement).toBe(two);

      document.body.tabIndex = -1;
      two.style.visibility = 'hidden';
      document.body.focus();
      await settle();

      expect(document.activeElement).toBe(three);
    });

    // Port title: `'popup'` restores to the floating element itself, again on the next frame (FLT-FOC-12).
    it('popup: restores focus to the popup when the focused element becomes hidden', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { 'data-open': '', tabindex: '-1' });
      const two = element('button', undefined, { id: 'two' });
      floating.append(element('button'), two, element('button'));
      document.body.append(reference, floating);

      attachManager(floating, { restoreFocus: 'popup', initialFocusId: 'two' });
      await settle();
      expect(document.activeElement).toBe(two);

      document.body.tabIndex = -1;
      two.style.visibility = 'hidden';
      document.body.focus();
      await settle();

      expect(document.activeElement).toBe(floating);
    });
  });

  describe('the managed tabindex', () => {
    it('floating element with no focusable elements and no listbox role gets tabIndex=0 when initialFocus is -1', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { role: 'dialog', 'data-open': '' });
      document.body.append(reference, floating);

      attachManager(floating, { initialFocus: false });
      await settle();

      expect(floating.getAttribute('tabindex')).toBe('0');
    });

    it('floating element with managed tabIndex is downgraded once content becomes tabbable', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { role: 'dialog', 'data-open': '' });
      document.body.append(reference, floating);

      attachManager(floating, { initialFocus: false });
      await settle();
      expect(floating.getAttribute('tabindex')).toBe('0');

      // C# renders tabbable content; a focus-out of the anchor re-runs the management (FLT-DOM-17).
      floating.append(element('button', 'inside'));
      reference.focus();
      reference.blur();
      await settle();

      expect(floating.getAttribute('tabindex')).toBe('-1');
    });

    it('floating element with listbox role ignores tabIndex setting', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { role: 'listbox', 'data-open': '' });
      document.body.append(reference, floating);

      // The positive half first: the same element with a dialog role is managed.
      floating.setAttribute('role', 'dialog');
      const managed = attachManager(floating, { initialFocus: false });
      await settle();
      expect(floating.getAttribute('tabindex')).toBe('0');
      managed.handle.dispose();
      floating.removeAttribute('tabindex');
      floating.removeAttribute('data-tabindex');
      floating.setAttribute('role', 'listbox');

      attachManager(floating, { initialFocus: false });
      await settle();

      expect(floating.hasAttribute('tabindex')).toBe(false);
    });

    it('handles manual tabindex on dialog floating element', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { role: 'dialog', tabindex: '-1', 'data-open': '' });
      document.body.append(reference, floating);

      attachManager(floating, { initialFocus: false });
      await settle();

      expect(floating.getAttribute('tabindex')).toBe('-1');
      expect(floating.hasAttribute('data-tabindex')).toBe(false);
    });
  });

  it('focus is placed on element with floating props when floating element is a wrapper', async () => {
    const reference = element('button', undefined, { id: 'reference' });
    const outer = element('div', 'outer');
    const inner = element('div', 'inner', { role: 'dialog', 'data-base-ui-focusable': '', 'data-open': '' });
    outer.append(inner);
    document.body.append(reference, outer);

    // The handle is on the popup (inner); its parent is the floating element.
    attachManager(inner, { treatPopupAsFloatingElement: false });
    await settle();

    expect(document.activeElement).toBe(inner);
  });

  // Port titles: the open state is read from the applied batch (ADR 04 Amendments 2026-10-27).
  describe('the open input', () => {
    it('releases the marking in the batch that removes data-open, with no update', async () => {
      const app = renderApp();
      const outside = element('div', 'outside');
      document.body.append(outside);

      app.open();
      await settle();
      expect(outside.getAttribute('aria-hidden')).toBe('true');
      expect(outside.hasAttribute('data-base-ui-inert')).toBe(true);

      app.floating.removeAttribute('data-open');
      await Promise.resolve();

      expect(outside.hasAttribute('aria-hidden')).toBe(false);
      expect(outside.hasAttribute('data-base-ui-inert')).toBe(false);
    });

    it('marks in the batch that renders data-open on an attached popup, before any update', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating');
      floating.append(element('button'));
      const outside = element('div', 'outside');
      document.body.append(reference, floating, outside);
      attachManager(floating);
      await settle();
      expect(outside.hasAttribute('aria-hidden')).toBe(false);

      floating.setAttribute('data-open', '');
      await Promise.resolve();

      expect(outside.getAttribute('aria-hidden')).toBe('true');
    });

    // ADR 01 and ADR 04 Amendments 2026-10-27 (design point 1, chosen by the user): return focus also runs when the batch
    // removes the popup, before C# disposes the handle a round trip later on Server.
    it('returns focus in the batch that removes the popup, before the handle is disposed', async () => {
      const app = renderApp();
      app.reference.focus();

      const manager = app.open();
      await settle();
      expect(document.activeElement).toBe(app.one);

      manager.update({ closeInteractionType: 'keyboard' });
      const focusSpy = vi.spyOn(app.reference, 'focus');
      for (const guard of Array.from(document.querySelectorAll('[data-base-ui-focus-guard]'))) {
        guard.remove();
      }
      app.floating.removeAttribute('data-open');
      app.floating.remove();
      await settle();

      expect(document.activeElement).toBe(app.reference);
      expect(focusSpy).toHaveBeenCalledWith({ preventScroll: true, focusVisible: true });
      manager.handle.dispose();
      await settle();
      expect(focusSpy).toHaveBeenCalledTimes(1);
    });

    it('a late update cannot reopen what a newer batch closed', async () => {
      const app = renderApp();
      const outside = element('div', 'outside');
      document.body.append(outside);
      app.reference.focus();

      const manager = app.open();
      await settle();
      app.floating.removeAttribute('data-open');
      await settle();
      app.reference.focus();

      // The update that carried the open arrives after the batch that closed it.
      manager.update({ initialFocusId: 'two', openInteractionType: 'keyboard' });
      await settle();

      expect(outside.hasAttribute('aria-hidden')).toBe(false);
      expect(document.activeElement).toBe(app.reference);
    });

    it('a disabled open takes no focus and marks nothing when its options precede the batch', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating');
      floating.append(element('button', 'inside'));
      const outside = element('div', 'outside');
      document.body.append(reference, floating, outside);
      reference.focus();
      const manager = attachManager(floating, { disabled: true });

      // A hover-opened Popover passes `disabled` (PopoverPopup.tsx:112).
      manager.update({ disabled: true, openInteractionType: 'mouse' });
      floating.setAttribute('data-open', '');
      await settle();
      expect(document.activeElement).toBe(reference);
      expect(outside.hasAttribute('aria-hidden')).toBe(false);

      manager.update({ disabled: false });
      await settle();

      expect(document.activeElement).toBe(byTestId('inside'));
      expect(outside.getAttribute('aria-hidden')).toBe('true');
    });
  });

  // The attach-order rule (design point 1's composite clause): composite items render `tabindex="-1"` until the
  // `composite` module attaches, so a group whose module attaches after initial focus chose its default is re-picked
  // once, while focus is still where initial focus put it.
  describe('a composite tab stop written after initial focus', () => {
    function renderGroupPopup() {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      const root = element('div', undefined, { id: 'group-root' });
      const items = ['1', '2'].map((id) => element('div', `item-${id}`, { id: `group-item-${id}`, tabindex: '-1' }));
      root.append(...items);
      const after = element('button', 'after-group');
      floating.append(root, after);
      document.body.append(reference, floating);
      const attachGroup = () =>
        handles.push(
          attachComposite(
            root,
            JSON.stringify({
              orientation: 'both',
              loopFocus: true,
              enableHomeAndEndKeys: false,
              stopEventPropagation: true,
              highlightItemOnHover: false,
              modifierKeys: [],
              direction: 'ltr',
              selectOnNavigation: false,
              reportFocusLeave: false,
              reportHighlight: false,
              items: items.map((item) => ({ id: item.id, tabIndex: null })),
            } satisfies CompositeOptions),
            createFakeTwin().twin,
          ),
        );
      return { reference, floating, items, after, attachGroup };
    }

    it('moves initial focus onto the group tab stop the composite writes after the frame', async () => {
      const popup = renderGroupPopup();
      popup.reference.focus();

      attachManager(popup.floating);
      await settle();
      expect(document.activeElement).toBe(popup.after);

      popup.attachGroup();
      await settle();

      expect(popup.items[0]!.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(popup.items[0]);
    });

    it('re-picks at most once, and not after the user has interacted', async () => {
      const first = renderGroupPopup();
      first.reference.focus();
      const firstManager = attachManager(first.floating);
      await settle();
      first.attachGroup();
      await settle();
      expect(document.activeElement).toBe(first.items[0]);

      // A later tabindex write inside the popup (the composite moving its tab stop) is not a second re-pick.
      first.items[0]!.setAttribute('tabindex', '-1');
      first.items[1]!.setAttribute('tabindex', '0');
      await settle();
      expect(document.activeElement).toBe(first.items[0]);
      firstManager.handle.dispose();
      document.body.replaceChildren();

      const second = renderGroupPopup();
      second.reference.focus();
      attachManager(second.floating);
      await settle();
      expect(document.activeElement).toBe(second.after);

      keyDown(second.after, 'a');
      second.attachGroup();
      await settle();
      expect(second.items[0]!.getAttribute('tabindex')).toBe('0');
      expect(document.activeElement).toBe(second.after);
    });
  });

  // FLT-ARIA-01's hover title: the `useHover` shim becomes a hover open carried by the relay (dated note on FLT-ARIA-01).
  it('uses aria-hidden instead of inert on outside nodes if opened with hover and modal=true', async () => {
    const reference = element('button', 'reference', { id: 'reference' });
    const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
    const outside = element('button');
    outside.textContent = 'outside';
    document.body.append(reference, floating, outside);

    attachManager(floating, { openInteractionType: 'mouse', relay: relay('trigger-hover', 'MouseEvent', 'mouseenter', 'mouse') });
    await settle();

    expect(outside.hasAttribute('inert')).toBe(false);
    expect(outside.getAttribute('aria-hidden')).toBe('true');
  });

  // The options the families pass, each at the lowest layer that observes it.
  describe('the element and selector options', () => {
    it('keeps the trigger focus guards and the anchor exposed, and a focus that moves to the after-guard closes nothing', async () => {
      const triggerBefore = insideGuard();
      triggerBefore.setAttribute('data-type', 'trigger');
      const reference = element('button', 'reference', { id: 'reference' });
      const triggerAfter = insideGuard();
      triggerAfter.setAttribute('data-type', 'trigger');
      const outside = element('button', 'outside');
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      const inside = element('button', 'inside');
      floating.append(inside);
      document.body.append(triggerBefore, reference, triggerAfter, outside, floating);

      const manager = attachManager(floating, { triggerFocusGuards: true, modal: false });
      await settle();
      expect(document.activeElement).toBe(inside);

      triggerAfter.focus();
      await settle();
      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);
      inside.focus();
      outside.focus();
      await settle();
      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
      manager.handle.dispose();

      // Modal: the guard before the anchor is inside; the anchor and the guard after it are the focusable neighbours.
      // The guards' own `aria-hidden` is dropped so the marking is what the assertions read.
      triggerBefore.removeAttribute('aria-hidden');
      triggerAfter.removeAttribute('aria-hidden');
      floating.setAttribute('data-open', '');
      attachManager(floating, { triggerFocusGuards: true });
      await settle();
      expect(outside.getAttribute('aria-hidden')).toBe('true');
      expect(triggerBefore.hasAttribute('aria-hidden')).toBe(false);
      expect(reference.hasAttribute('aria-hidden')).toBe(false);
      expect(triggerAfter.hasAttribute('aria-hidden')).toBe(false);
    });

    it('takes the return focus target by element id', async () => {
      const app = renderApp();
      const target = element('button', 'target', { id: 'return-target' });
      document.body.append(target);
      app.reference.focus();

      const manager = app.open({ returnFocusId: 'return-target' });
      await settle();
      await unmount(manager);

      expect(document.activeElement).toBe(target);
    });

    it('keeps the inside elements named by id exposed', async () => {
      const reference = element('input', 'reference', { id: 'reference' });
      const wrapper = element('div', 'outside-wrapper');
      wrapper.append(element('button'));
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      const dismiss = element('button', 'dismiss', { id: 'dismiss' });
      document.body.append(reference, wrapper, floating, dismiss);

      attachManager(floating, { insideElementIds: ['dismiss'] });
      await settle();

      expect(wrapper.getAttribute('aria-hidden')).toBe('true');
      expect(dismiss.hasAttribute('aria-hidden')).toBe(false);
    });

    it('reads the open state from the selector the options name', async () => {
      const reference = element('button', 'reference', { id: 'reference' });
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      const outside = element('div', 'outside');
      document.body.append(reference, floating, outside);

      attachManager(floating, { openSelector: '[data-state="open"]' });
      await settle();
      expect(outside.hasAttribute('aria-hidden')).toBe(false);

      floating.setAttribute('data-state', 'open');
      await Promise.resolve();
      expect(outside.getAttribute('aria-hidden')).toBe('true');

      floating.setAttribute('data-state', 'closed');
      await Promise.resolve();
      expect(outside.hasAttribute('aria-hidden')).toBe(false);
    });

    // Menu's previousFocusableElement and nextFocusableElement (M5), by reference index (ADR 04 Amendments 2026-10-27).
    it('keeps the previous focusable element named by reference exposed while modal', async () => {
      const app = renderApp();
      const previous = element('button', 'previous');
      const outside = element('button', 'outside');
      document.body.append(previous, outside);

      app.open({ previousFocusableElement: 0 }, { references: [previous] });
      await settle();

      expect(outside.getAttribute('aria-hidden')).toBe('true');
      expect(previous.hasAttribute('aria-hidden')).toBe(false);
    });

    it('keeps the next focusable element named by reference exposed while modal', async () => {
      const app = renderApp();
      const next = element('button', 'next');
      const outside = element('button', 'outside');
      document.body.append(next, outside);

      app.open({ nextFocusableElement: 0 }, { references: [next] });
      await settle();

      expect(outside.getAttribute('aria-hidden')).toBe('true');
      expect(next.hasAttribute('aria-hidden')).toBe(false);
    });
  });

  // ADR 04 Amendments 2026-10-27 (the answered-requests counter): a focus-out close is a request, so after a consumer's
  // cancel C# moves `answered` and the module asks again.
  it('asks again for a focus-out close once the answered count moves', async () => {
    const app = renderApp({ modal: false });
    const elsewhere = element('button', 'elsewhere');
    document.body.append(elsewhere);

    const manager = app.open();
    await settle();
    elsewhere.focus();
    await settle();
    expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);

    // The consumer cancelled: nothing rendered, and the module is still waiting.
    app.one.focus();
    elsewhere.focus();
    await settle();
    expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);

    manager.update({ answered: 1 });
    app.one.focus();
    elsewhere.focus();
    await settle();

    expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(2);
  });

  it('returns focus in the batch that removes an ancestor of the popup', async () => {
    const reference = element('button', 'reference', { id: 'reference' });
    const ancestor = element('div');
    const parent = element('div');
    const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
    const inside = element('button');
    floating.append(inside);
    parent.append(floating);
    ancestor.append(parent);
    document.body.append(reference, ancestor);
    reference.focus();

    const manager = attachManager(floating);
    await settle();
    expect(document.activeElement).toBe(inside);

    ancestor.remove();
    await settle();

    expect(document.activeElement).toBe(reference);
    manager.handle.dispose();
  });

  // FLT-DOM-13, FLT-A11Y-07: the guards' VoiceOver role is a JavaScript write after attach, on the guards this module
  // listens on.
  it('gives the inside guards role="button" and no aria-hidden under VoiceOver on WebKit', async () => {
    vi.resetModules();
    vi.doMock('./platform', async (importOriginal) => {
      const actual = await importOriginal<typeof import('./platform')>();
      return {
        ...actual,
        platform: { ...actual.platform, engine: { ...actual.platform.engine, webkit: true }, screenReader: { voiceOver: true } },
      };
    });
    try {
      const isolated = await import('./focus-manager');
      const reference = element('button', undefined, { id: 'reference' });
      const before = insideGuard();
      const floating = element('div', undefined, { 'data-open': '' });
      const after = insideGuard();
      document.body.append(reference, before, floating, after);

      const handle = isolated.attach(floating, JSON.stringify({ treatPopupAsFloatingElement: true }), createFakeTwin().twin);
      handles.push(handle);

      expect(before.getAttribute('role')).toBe('button');
      expect(before.hasAttribute('aria-hidden')).toBe(false);
      expect(after.getAttribute('role')).toBe('button');
      expect(after.hasAttribute('aria-hidden')).toBe(false);
    } finally {
      vi.doUnmock('./platform');
      vi.resetModules();
    }
  });

  // FLT-FOC-16 has no Base UI title: on WebKit a typeable element focused inside is blurred in the closing batch.
  it('blurs a focused input inside the popup in the batch that closes it on WebKit', async () => {
    vi.resetModules();
    vi.doMock('./platform', async (importOriginal) => {
      const actual = await importOriginal<typeof import('./platform')>();
      return { ...actual, platform: { ...actual.platform, engine: { ...actual.platform.engine, webkit: true } } };
    });
    try {
      const isolated = await import('./focus-manager');
      const reference = element('button', undefined, { id: 'reference' });
      const floating = element('div', undefined, { 'data-open': '' });
      const input = element('input');
      floating.append(input);
      document.body.append(reference, floating);

      const handle = isolated.attach(floating, JSON.stringify({ treatPopupAsFloatingElement: true, returnFocus: false }), createFakeTwin().twin);
      handles.push(handle);
      await settle();
      expect(document.activeElement).toBe(input);

      floating.removeAttribute('data-open');
      await Promise.resolve();

      expect(document.activeElement).toBe(document.body);
    } finally {
      vi.doUnmock('./platform');
      vi.resetModules();
    }
  });

  describe('the handle', () => {
    it('returns the live handle for a repeated attach and disposes idempotently', async () => {
      const app = renderApp();
      const outside = element('div');
      document.body.append(outside);

      const manager = app.open();
      const again = attach(app.floating, JSON.stringify({}), createFakeTwin().twin);
      expect(again).toBe(manager.handle);
      await settle();
      expect(outside.getAttribute('aria-hidden')).toBe('true');

      manager.handle.dispose();
      manager.handle.dispose();

      expect(outside.hasAttribute('aria-hidden')).toBe(false);
    });

    it('sends one focus-out close request per open session', async () => {
      const app = renderApp({ modal: false });
      const elsewhere = element('button', 'elsewhere');
      document.body.append(elsewhere);

      const manager = app.open();
      await settle();
      app.three.focus();
      await tab();
      app.one.focus();
      elsewhere.focus();
      await settle();
      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);

      // The next open arms it again.
      app.floating.removeAttribute('data-open');
      await settle();
      app.floating.setAttribute('data-open', '');
      await settle();
      app.one.focus();
      elsewhere.focus();
      await settle();

      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(2);
    });

    it.each([false, true])('reports the focus-out close through the channel when synchronous=%s', async (synchronous) => {
      const app = renderApp({ modal: false });

      const manager = app.open({}, { synchronous });
      await settle();
      app.three.focus();
      await tab();

      expect(manager.fake.reported<FocusOutCloseRequest>(FOCUS_OUT_CLOSE_REPORT)).toEqual([
        expect.objectContaining({ interactionType: 'keyboard', event: expect.objectContaining({ type: 'focusout' }) }),
      ]);
      expect(manager.fake.decisions).toEqual([]);
    });

    // M4 track 5's Gap 12 (settled by the orchestrator 2026-10-28): WebAssembly answers the focus-out close inside the
    // request. A consumer that clears the anchor as it closes (Popover's controlled `TriggerId`) then sends the
    // commit-path update and the batch that removes the popup while the browser is still moving focus, with the body as
    // the active element: the re-arm's cancelled job cleared the suppression, and return focus took focus back.
    it('port: a focus-out close answered inside the request leaves focus where the browser moved it', async () => {
      const opener = element('button', 'opener');
      const reference = element('button', 'reference', { id: 'reference' });
      const outside = element('button', 'outside');
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      const inside = element('button', 'inside');
      floating.append(inside);
      document.body.append(opener, reference, outside, floating);
      opener.focus();
      const manager = attachManager(floating, { modal: false, openInteractionType: null }, { synchronous: true });
      await settle();
      expect(document.activeElement).toBe(inside);

      answerInside(manager, FOCUS_OUT_CLOSE_REPORT, () => {
        manager.update({ domReferenceId: null });
        floating.removeAttribute('data-open');
        floating.remove();
      });
      await browserFocusMove(inside, outside);
      await settle();

      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
      expect(document.activeElement).toBe(outside);
    });
  });

  // FloatingFocusManager.test.tsx's titles that render `FloatingPortal`: the `portal` module moves the node and routes
  // its outside guards (FLT-KEY-07, FOC-17), the test renders what C# renders around both handles, and the manager
  // publishes its state on the portal node (FLT-FOC-18, design point 3 of the focus-manager track, settled by the
  // orchestrator 2026-10-27).
  describe('FloatingPortal', () => {
    describe('the publication', () => {
      it('port: publishes modal, closeOnFocusOut, open, the anchor and the inside guards on the enclosing portal node while enabled, and withdraws them when disabled or disposed', async () => {
        const reference = element('button', 'reference', { id: 'reference' });
        document.body.append(reference);
        const portal = renderPortal((wrapper) => reference.after(wrapper));
        const floating = element('div', 'floating');
        floating.append(element('button', 'inside'));

        const manager = portal.open(floating);
        await settle();

        const [before, after] = Array.from(portal.node.querySelectorAll<HTMLElement>('[data-type="inside"]'));
        expect(getPortalState(portal.node)?.focusManager).toMatchObject({
          modal: false,
          closeOnFocusOut: true,
          open: true,
          domReference: reference,
          insideGuards: { before, after },
        });

        manager.update({ disabled: true });
        expect(getPortalState(portal.node)?.focusManager).toBeNull();

        manager.update({ disabled: false, modal: true, closeOnFocusOut: false });
        expect(getPortalState(portal.node)?.focusManager).toMatchObject({ modal: true, closeOnFocusOut: false, open: true });

        floating.removeAttribute('data-open');
        await settle();
        expect(getPortalState(portal.node)?.focusManager).toMatchObject({ open: false });

        manager.handle.dispose();
        expect(getPortalState(portal.node)?.focusManager).toBeNull();
      });

      it("port: the publication's close asks C# for a focus-out close", async () => {
        const reference = element('button', 'reference', { id: 'reference' });
        document.body.append(reference);
        const portal = renderPortal((wrapper) => reference.after(wrapper));
        const floating = element('div', 'floating');
        floating.append(element('button', 'inside'));

        const manager = portal.open(floating);
        await settle();
        getPortalState(portal.node)?.focusManager?.close?.(new FocusEvent('focusin'));

        expect(manager.fake.reported<FocusOutCloseRequest>(FOCUS_OUT_CLOSE_REPORT)).toEqual([
          expect.objectContaining({ event: expect.objectContaining({ kind: 'FocusEvent', type: 'focusin' }) }),
        ]);
      });
    });

    describe('prop: returnFocus', () => {
      // `{isOpen && <FloatingPortal>…}`: the reference and the whole portal leave in the batch that closes it.
      it.each([true, false])('preserves tabbable context next to reference element if removed (modal=%s)', async (modal) => {
        const reference = element('button', 'reference', { id: 'reference' });
        const fallback = element('button', 'fallback');
        document.body.append(reference, fallback);
        const portal = renderPortal((wrapper) => fallback.before(wrapper));
        const floating = element('div', 'floating');
        const remove = element('button', 'remove');
        floating.append(remove);

        reference.focus();
        const manager = portal.open(floating, { modal });
        await settle();
        expect(document.activeElement).toBe(remove);

        remove.click();
        reference.remove();
        await portal.close(manager);
        portal.wrapper.remove();
        await settle();

        await tab();

        expect(document.activeElement).toBe(fallback);
      });

      it('does not insert fallback element when return element is falsy', async () => {
        const reference = element('button', 'reference', { id: 'reference' });
        document.body.append(reference);
        // `<FloatingPortal>{isOpen && …}</FloatingPortal>`: the port's in-place wrapper (FLT-DOM-11's note) is the
        // reference's sibling; the focus manager inserts nothing beside it.
        const portal = renderPortal((wrapper) => reference.after(wrapper));
        const floating = element('div', 'floating');
        const close = element('button', 'close');
        floating.append(close);

        reference.focus();
        reference.click();
        const manager = portal.open(floating, { modal: true, returnFocus: false });
        await settle();

        expect(reference.nextElementSibling).toBe(portal.wrapper);

        close.click();
        await portal.close(manager, { returnFocus: false });

        expect(document.querySelector('[data-testid="close"]')).toBeNull();
        expect(reference.nextElementSibling).toBe(portal.wrapper);
      });
    });

    describe('prop: disabled', () => {
      it('clears outside pointer state between keep-mounted open sessions', async () => {
        const before = element('button', 'before');
        const reference = element('button', 'reference', { id: 'reference' });
        const after = element('button', 'after');
        document.body.append(before, reference, after);
        const portal = renderPortal((wrapper) => after.before(wrapper));
        const floating = element('div', 'floating');
        const child = element('button', 'child');
        floating.append(child);

        // Kept mounted: `disabled={!isOpen}`, the open state from the batch.
        reference.click();
        const manager = portal.open(floating);
        await settle();
        expect(document.activeElement).toBe(child);

        // The outside press: the dismiss closes it (C#'s commit: the close's options, then the batch).
        pointerDown(after);
        manager.update({ disabled: true });
        floating.removeAttribute('data-open');
        await settle();

        reference.click();
        manager.update({ disabled: false });
        floating.setAttribute('data-open', '');
        await settle();
        expect(document.activeElement).toBe(child);

        // Base UI reads `dataRef.current.insideReactTree`; the marker is observed through the focus-out close it
        // suppresses: a press state leaked from the previous session would leave it unset and the close requested.
        child.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: after }));
        await settle();

        expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);
      });
    });

    describe('non-modal + FloatingPortal', () => {
      function renderTabbingApp() {
        const first = element('span', 'first', { tabindex: '0' });
        const reference = element('button', 'reference', { id: 'reference' });
        const last = element('span', 'last', { tabindex: '0' });
        document.body.append(first, reference, last);
        const portal = renderPortal((wrapper) => last.before(wrapper));
        const floating = element('div', 'floating');
        floating.append(element('span', 'inside', { tabindex: '0' }));
        return { first, reference, last, portal, floating };
      }

      it('focuses inside element, tabbing out focuses last document element', async () => {
        const app = renderTabbingApp();

        app.reference.click();
        const manager = app.portal.open(app.floating);
        await settle();

        expect(document.activeElement).toBe(byTestId('inside'));

        await tab();
        // The after-outside guard asked C# to close it (reason focus-out); C# closes.
        expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
        await app.portal.close(manager);

        expect(document.querySelector('[data-testid="floating"]')).toBeNull();
        expect(document.activeElement).toBe(app.last);
      });

      it('shift+tab', async () => {
        const app = renderTabbingApp();

        app.reference.click();
        const manager = app.portal.open(app.floating);
        await settle();

        await tab({ shift: true });

        expect(document.querySelector('[data-testid="floating"]')).not.toBeNull();
        expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);

        await tab({ shift: true });
        expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(1);
        await app.portal.close(manager);

        expect(document.querySelector('[data-testid="floating"]')).toBeNull();
      });

      it('standard tabbing back and forth of a non-modal floating element', async () => {
        const reference = element('button', 'reference', { id: 'reference' });
        document.body.append(reference);
        const portal = renderPortal((wrapper) => reference.after(wrapper));
        const floating = element('div', 'floating', { role: 'dialog', tabindex: '-1' });
        const inner = element('button', 'inner');
        floating.append(inner);

        reference.click();
        portal.open(floating);
        await settle();

        expect(floating.getAttribute('tabindex')).toBe('-1');
        expect(document.activeElement).toBe(inner);
        await tab({ shift: true });
        expect(document.activeElement).toBe(reference);
        await tab();
        expect(document.activeElement).toBe(inner);
      });
    });

    describe('iframe focus navigation', () => {
      // The port renders in the top document and the portal moves its node into a container inside the iframe; here
      // the whole app is moved there, as Base UI renders it, so every tabbable query runs in the iframe's document.
      function renderIframeApp() {
        const iframe = element('iframe', 'iframe');
        document.body.append(element('a', undefined, { href: '#' }), iframe, element('a', undefined, { href: '#' }));
        const iframeDoc = iframe.contentDocument!;
        iframeDoc.open();
        iframeDoc.write('<div id="rootIframe"></div>');
        iframeDoc.close();
        const root = iframeDoc.getElementById('rootIframe')!;
        const app = element('div');
        const prev = element('a', 'prev', { href: '#' });
        const reference = element('button', 'reference', { id: 'reference' });
        const next = element('a', 'next', { href: '#' });
        app.append(prev, reference, next);
        root.append(app);
        const portal = renderPortal((wrapper) => next.before(wrapper), { container: root });
        const floating = element('div', 'floating');
        const popover = element('div', 'popover');
        popover.append(element('a', 'link-1', { href: '#' }), element('a', 'link-2', { href: '#' }));
        floating.append(popover);
        return { iframeDoc, prev, reference, next, portal, floating };
      }

      it('tabs from the popover to the next element in the iframe', async () => {
        const app = renderIframeApp();

        app.reference.focus();
        app.reference.click();
        app.portal.open(app.floating);
        await settle();
        expect(app.floating.ownerDocument).toBe(app.iframeDoc);

        await tab({ doc: app.iframeDoc });
        await tab({ doc: app.iframeDoc });

        expect(app.iframeDoc.activeElement).toBe(app.next);
      });

      it('shift+tab from the popover to the previous element in the iframe', async () => {
        const app = renderIframeApp();

        app.reference.focus();
        app.reference.click();
        app.portal.open(app.floating);
        await settle();

        await tab({ shift: true, doc: app.iframeDoc });

        expect(app.iframeDoc.activeElement).toBe(app.reference);
      });
    });
  });

  // The titles that compose the manager with the interactions session (M4 track 3): the session harness plays the popup
  // store and relays each commit in the session's `update`, the test renders the popup as C# would after the commit, and
  // the manager joins the same session by its token. The manager then reads the session's `openchange` (with `nested`,
  // the real event and the trigger), its `dataRef`, its tree and its registered triggers.
  describe('with the interactions session', () => {
    const popups: PopupHarness[] = [];

    afterEach(() => {
      for (const popup of popups.splice(0)) {
        popup.dispose();
      }
      resetInteractionsForTest();
    });

    function sessionPopup(options: Parameters<typeof createPopupHarness>[0] = {}): PopupHarness {
      const popup = createPopupHarness(options);
      popups.push(popup);
      return popup;
    }

    // `fireEvent`'s defaults: the events bubble (but mouseenter and mouseleave), are cancelable and composed.
    const mouse = (target: EventTarget, type: string, init: MouseEventInit = {}) => {
      const bubbles = type !== 'mouseenter' && type !== 'mouseleave';
      target.dispatchEvent(new MouseEvent(type, { bubbles, cancelable: bubbles, composed: true, ...init }));
    };
    const pointer = (target: EventTarget, type: string, init: PointerEventInit = {}) => {
      target.dispatchEvent(new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, ...init }));
    };
    // user-event's primary mouse click: the pointer, mouse and click events of one press.
    const userClick = (target: HTMLElement) => {
      pointer(target, 'pointerdown', { pointerType: 'mouse', button: 0, buttons: 1 });
      mouse(target, 'mousedown', { button: 0, buttons: 1, detail: 1 });
      target.focus();
      pointer(target, 'pointerup', { pointerType: 'mouse', button: 0 });
      mouse(target, 'mouseup', { button: 0, detail: 1 });
      pointer(target, 'click', { pointerType: 'mouse', button: 0, detail: 1 });
    };

    it('focuses without a frame delay when a screen reader press opens it', async () => {
      const popup = sessionPopup();
      popup.attachSession();
      const reference = element('button', 'reference', { id: 'reference' });
      document.body.append(reference);
      popup.attachTrigger(reference, { trigger: { click: { event: 'mousedown' } } });

      // A screen reader press is a `mousedown` with `detail: 0`; with no `pointerdown` before it, the click region opens
      // on the next frame, as Base UI's does.
      mouse(reference, 'mousedown', { detail: 0 });
      await nextFrame();
      expect(popup.open).toBe(true);

      // C#'s opening batch, then the manager's attach.
      const floating = element('div', 'floating', { role: 'dialog', tabindex: '-1', 'data-open': '' });
      const one = element('button', 'one');
      floating.append(one);
      document.body.append(floating);
      popup.attachFloating(floating);
      vi.spyOn(window, 'requestAnimationFrame').mockImplementation(() => 0);
      attachManager(floating, { sessionToken: popup.token });
      await Promise.resolve();
      await Promise.resolve();

      expect(document.activeElement).toBe(one);
    });

    it('always returns to the reference for nested elements', async () => {
      const outer = sessionPopup();
      const inner = sessionPopup();
      outer.attachSession({ session: { dismiss: { bubblesOutsidePress: false } } });
      inner.attachSession({ session: { parentToken: outer.token, dismiss: { bubblesOutsidePress: false } } });
      const openDialog = element('button', 'open-dialog', { id: 'open-dialog' });
      document.body.append(openDialog);
      outer.attachTrigger(openDialog, { trigger: { click: {} } });

      userClick(openDialog);
      expect(outer.open).toBe(true);
      const outerPortal = renderPortal((wrapper) => document.body.append(wrapper));
      const outerFloating = element('div', 'outer-floating', { tabindex: '-1' });
      const openNested = element('button', 'open-nested-dialog', { id: 'open-nested-dialog' });
      outerFloating.append(openNested, element('button', 'close-dialog'));
      outerPortal.open(outerFloating, { modal: true, sessionToken: outer.token, domReferenceId: 'open-dialog' });
      outer.attachFloating(outerFloating);
      await settle();
      inner.attachTrigger(openNested, { trigger: { click: {} } });

      userClick(openNested);
      expect(inner.open).toBe(true);
      // The nested portal's container is the enclosing portal's node (FLT-API-13).
      const innerPortal = renderPortal((wrapper) => outerFloating.append(wrapper), { container: outerPortal.node });
      const innerFloating = element('div', 'inner-floating', { tabindex: '-1' });
      innerFloating.append(element('button', 'close-nested-dialog'));
      const innerManager = innerPortal.open(innerFloating, { modal: true, sessionToken: inner.token, domReferenceId: 'open-nested-dialog' });
      const innerHandle = inner.attachFloating(innerFloating);
      await settle();
      expect(document.querySelector('[data-testid="close-nested-dialog"]')).not.toBeNull();

      pointer(document.body, 'pointerdown');
      expect(inner.open).toBe(false);
      expect(outer.open).toBe(true);
      await innerPortal.close(innerManager);
      innerHandle.dispose();
      expect(document.activeElement).toBe(openNested);

      pointer(document.body, 'pointerdown');
      expect(outer.open).toBe(false);
    });

    it('mixed modality and nesting', async () => {
      const outer = sessionPopup();
      const side = sessionPopup();
      outer.attachSession({ session: { dismiss: { bubblesOutsidePress: false } } });
      side.attachSession({ session: { parentToken: outer.token, dismiss: { bubblesOutsidePress: false } } });
      const openDialog = element('button', 'open-dialog', { id: 'open-dialog' });
      document.body.append(openDialog);
      outer.attachTrigger(openDialog, { trigger: { click: {} } });

      userClick(openDialog);
      const outerPortal = renderPortal((wrapper) => document.body.append(wrapper));
      const outerFloating = element('div', 'outer-floating', { tabindex: '-1' });
      const openNested = element('button', 'open-nested-dialog');
      outerFloating.append(element('button', 'close-dialog'), openNested);
      const outerManager = outerPortal.open(outerFloating, { modal: false, sessionToken: outer.token, domReferenceId: 'open-dialog' });
      outer.attachFloating(outerFloating);
      await settle();

      // `onClick={() => setSideDialogOpen(true)}`: a controlled open of the side dialog, a tree child of the outer one
      // rendered beside it in its own portal.
      userClick(openNested);
      side.setOpen(true);
      const sidePortal = renderPortal((wrapper) => outerPortal.wrapper.after(wrapper));
      const sideFloating = element('div', 'side-floating', { tabindex: '-1' });
      sideFloating.append(element('button', 'close-nested-dialog'));
      sidePortal.open(sideFloating, { modal: true, sessionToken: side.token, domReferenceId: null });
      side.attachFloating(sideFloating);
      await settle();

      // Focus moved into the side dialog, a descendant node: the outer non-modal dialog asks for no close.
      expect(document.activeElement).toBe(byTestId('close-nested-dialog'));
      expect(outerManager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);
      expect(document.querySelector('[data-testid="close-dialog"]')).not.toBeNull();
      expect(document.querySelector('[data-testid="close-nested-dialog"]')).not.toBeNull();
    });

    it('focus does not return to reference when floating element is triggered by hover', async () => {
      const popup = sessionPopup();
      popup.attachSession();
      const reference = element('button', 'reference', { id: 'reference' });
      document.body.append(reference);
      popup.attachTrigger(reference, { trigger: { hover: {} } });
      reference.focus();

      mouse(reference, 'mouseenter');
      await settle();
      expect(popup.open).toBe(true);
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(floating);
      popup.attachFloating(floating, { floating: { hover: { closeDelay: 0 } } });
      const manager = attachManager(floating, { sessionToken: popup.token });
      await settle();
      expect(document.activeElement).toBe(floating);

      mouse(floating, 'mouseleave');
      await settle();
      expect(popup.open).toBe(false);
      await unmount(manager);

      expect(document.activeElement).not.toBe(reference);
    });

    describe('Navigation', () => {
      // test/floating-ui-tests/Navigation.tsx's Product item: hover, focus and dismiss on the reference, a non-modal
      // manager without initial focus in a portal. The safe polygon is left out: its pointer geometry is the hover
      // region's own titles; the floating element's leave closes through its handle.
      function renderNavigation() {
        const popup = sessionPopup();
        popup.attachSession({ session: { dismiss: {} } });
        const product = element('a', 'product', { href: '#', id: 'product' });
        document.body.append(product);
        popup.attachTrigger(product, { trigger: { hover: {}, focus: {} } });
        const portal = renderPortal((wrapper) => document.body.append(wrapper));
        let floating: HTMLElement | null = null;
        let manager: Manager | null = null;
        return {
          popup,
          product,
          get floating() {
            return floating!;
          },
          /** C#'s batch after a commit: renders the subnavigation when open, removes it when closed. */
          async render() {
            if (popup.open && manager === null) {
              floating = element('div', 'subnavigation');
              floating.append(element('button', 'close'), element('a', 'link-1', { href: '#' }));
              manager = portal.open(floating, { modal: false, initialFocus: false, sessionToken: popup.token, domReferenceId: 'product' });
              popup.attachFloating(floating, { floating: { hover: { closeDelay: 0 } } });
            } else if (!popup.open && manager !== null) {
              await portal.close(manager);
              manager = null;
            }
            await settle();
          },
        };
      }

      it('does not re-open after closing via escape key', async () => {
        const nav = renderNavigation();
        mouse(nav.product, 'mouseenter');
        await settle();
        await nav.render();
        expect(nav.popup.open).toBe(true);

        keyDown(document.activeElement ?? document.body, 'Escape');
        expect(nav.popup.open).toBe(false);
        await nav.render();
        await nav.render();

        expect(document.activeElement).toBe(nav.product);
        expect(nav.popup.open).toBe(false);
        expect(document.querySelector('[data-testid="link-1"]')).toBeNull();
      });

      it('closes when unhovering floating element even when focus is inside it', async () => {
        const nav = renderNavigation();
        mouse(nav.product, 'mouseenter');
        await settle();
        await nav.render();
        expect(nav.popup.open).toBe(true);

        // userEvent.click(subnavigation): the pointer leaves the reference for the floating element; the press focuses inside.
        mouse(nav.product, 'mouseleave');
        mouse(nav.floating, 'mouseenter');
        byTestId('close').focus();
        mouse(nav.floating, 'mouseleave');
        await settle();
        expect(nav.popup.open).toBe(false);
        await nav.render();
        await nav.render();

        mouse(nav.product, 'mouseenter');
        await settle();
        await nav.render();
        mouse(nav.product, 'mouseleave');
        await settle();
        await nav.render();

        expect(nav.popup.open).toBe(false);
        expect(document.querySelector('[data-testid="subnavigation"]')).toBeNull();
      });
    });

    it('dismisses when clicking outside a shared shadow root while focus is managed', async () => {
      const popup = sessionPopup({ open: true });
      popup.attachSession({ session: { dismiss: {} } });
      const reference = element('button', 'reference', { id: 'reference' });
      document.body.append(reference);
      popup.attachTrigger(reference);
      popup.setReference(reference);
      const host = element('div');
      document.body.append(host);
      const shadowRoot = host.attachShadow({ mode: 'open' });
      // The port renders in the top document; the portal moves the node into the shadow root (a script-held container).
      const portal = renderPortal((wrapper) => document.body.append(wrapper), { container: shadowRoot });
      const floating = element('div', 'dialog', { role: 'dialog', tabindex: '-1' });
      portal.open(floating, { modal: true, sessionToken: popup.token });
      popup.attachFloating(floating);
      await settle();
      expect(floating.getRootNode()).toBe(shadowRoot);

      userClick(document.body);

      expect(popup.open).toBe(false);
      expect(popup.changes.at(-1)?.details.reason).toBe('outside-press');
    });

    // Port titles for the rows' session clauses (FLT-FOC-08's virtual branch, FLT-FOC-11's registered triggers).
    it('port: where preventScroll is unsupported, an outside press returns focus only when a screen reader made it', async () => {
      const popup = sessionPopup();
      popup.attachSession({ session: { dismiss: {} } });
      const reference = element('button', 'reference', { id: 'reference' });
      document.body.append(reference);
      popup.attachTrigger(reference, { trigger: { click: {} } });
      const open = async () => {
        userClick(reference);
        expect(popup.open).toBe(true);
        const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
        document.body.append(floating);
        const handle = popup.attachFloating(floating);
        const manager = attachManager(floating, { sessionToken: popup.token });
        await settle();
        expect(document.activeElement).toBe(floating);
        return { manager, handle };
      };

      // jsdom's focus() never reads `preventScroll`: a mouse press outside returns no focus.
      const first = await open();
      pointer(document.body, 'pointerdown', { pointerType: 'mouse' });
      expect(popup.open).toBe(false);
      await unmount(first.manager);
      first.handle.dispose();
      expect(document.activeElement).not.toBe(reference);

      // A virtual pointer press (no pointer type, detail 0) returns it.
      const second = await open();
      pointer(document.body, 'pointerdown', { pointerType: '' });
      expect(popup.open).toBe(false);
      await unmount(second.manager);

      expect(document.activeElement).toBe(reference);
    });

    // FLT-ALG-22's tree clause, without the portal whose focus-out marker would keep the popup open anyway.
    it('port: focus moving from a non-modal popup into a nested popup asks for no close', async () => {
      const outer = sessionPopup();
      const inner = sessionPopup();
      outer.attachSession();
      inner.attachSession({ session: { parentToken: outer.token } });
      const reference = element('button', 'reference', { id: 'reference' });
      document.body.append(reference);
      outer.attachTrigger(reference, { trigger: { click: {} } });
      userClick(reference);
      const outerFloating = element('div', 'outer-floating', { tabindex: '-1', 'data-open': '' });
      outerFloating.append(element('button', 'outer-inside'));
      document.body.append(outerFloating);
      outer.attachFloating(outerFloating);
      const outerManager = attachManager(outerFloating, { modal: false, sessionToken: outer.token });
      await settle();
      expect(document.activeElement).toBe(byTestId('outer-inside'));

      inner.setOpen(true);
      const innerFloating = element('div', 'inner-floating', { tabindex: '-1', 'data-open': '' });
      innerFloating.append(element('button', 'inner-inside'));
      document.body.append(innerFloating);
      inner.attachFloating(innerFloating);
      attachManager(innerFloating, { modal: false, sessionToken: inner.token, domReferenceId: null });
      await settle();

      expect(document.activeElement).toBe(byTestId('inner-inside'));
      expect(outerManager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);
    });

    // FLT-FOC-07's tree clause: focus inside a descendant node's floating element counts as inside the floating tree.
    it('port: closing a popup while focus is inside its nested popup returns focus to the reference', async () => {
      const outer = sessionPopup();
      const inner = sessionPopup();
      outer.attachSession();
      inner.attachSession({ session: { parentToken: outer.token } });
      const reference = element('button', 'reference', { id: 'reference' });
      document.body.append(reference);
      outer.attachTrigger(reference, { trigger: { click: {} } });
      userClick(reference);
      const outerFloating = element('div', 'outer-floating', { tabindex: '-1', 'data-open': '' });
      outerFloating.append(element('button', 'outer-inside'));
      document.body.append(outerFloating);
      outer.attachFloating(outerFloating);
      const outerManager = attachManager(outerFloating, { modal: false, sessionToken: outer.token });
      await settle();

      // The nested popup renders elsewhere in the document; focus moves into it.
      inner.setOpen(true);
      const innerFloating = element('div', 'inner-floating', { tabindex: '-1', 'data-open': '' });
      const innerInside = element('button', 'inner-inside');
      innerFloating.append(innerInside);
      document.body.append(innerFloating);
      inner.attachFloating(innerFloating);
      innerInside.focus();
      await settle();

      await unmount(outerManager);

      expect(document.activeElement).toBe(reference);
    });

    // FLT-ALG-22's ancestor clause: an ancestor node's floating element is related.
    it("port: focus moving from a nested non-modal popup to its parent's floating element asks for no close", async () => {
      const outer = sessionPopup();
      const inner = sessionPopup();
      outer.attachSession();
      inner.attachSession({ session: { parentToken: outer.token } });
      outer.setOpen(true);
      const outerFloating = element('div', 'outer-floating', { tabindex: '-1', 'data-open': '' });
      document.body.append(outerFloating);
      outer.attachFloating(outerFloating);
      const innerReference = element('button', 'inner-reference', { id: 'inner-reference' });
      outerFloating.append(innerReference);
      inner.attachTrigger(innerReference, { trigger: { click: {} } });
      userClick(innerReference);
      const innerFloating = element('div', 'inner-floating', { tabindex: '-1', 'data-open': '' });
      innerFloating.append(element('button', 'inner-inside'));
      document.body.append(innerFloating);
      inner.attachFloating(innerFloating);
      const innerManager = attachManager(innerFloating, { modal: false, sessionToken: inner.token, domReferenceId: 'inner-reference' });
      await settle();
      expect(document.activeElement).toBe(byTestId('inner-inside'));

      outerFloating.focus();
      await settle();

      expect(innerManager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);
    });

    it('port: focus moving from a non-modal popup to another of its registered triggers asks for no close', async () => {
      const popup = sessionPopup();
      popup.attachSession();
      const reference = element('button', 'reference', { id: 'reference' });
      const other = element('button', 'other', { id: 'other' });
      document.body.append(reference, other);
      popup.attachTrigger(reference, { trigger: { click: {} } });
      popup.attachTrigger(other, { trigger: { click: {} } });
      userClick(reference);
      const floating = element('div', 'floating', { tabindex: '-1', 'data-open': '' });
      floating.append(element('button', 'inside'));
      document.body.append(floating);
      popup.attachFloating(floating);
      const manager = attachManager(floating, { modal: false, sessionToken: popup.token });
      await settle();
      expect(document.activeElement).toBe(byTestId('inside'));

      other.focus();
      await settle();

      expect(manager.fake.reported(FOCUS_OUT_CLOSE_REPORT)).toHaveLength(0);
    });
  });
});
