// L3 tests of the `composite` module's list navigation (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1)-(3),
// chosen by the user; CMP §4.2.4, §4.6-§4.8, §4.11, §4.12, §5.4, §5.6): Base UI's useListNavigation.test.tsx titles,
// verbatim, on DOM built after its `App` (a button reference, a `role="menu"` floating element holding a list of three
// items), over the list harness, which plays C#'s part (the commit, the render after it, the attach from
// `OnAfterRender`). `waitFor` stands for Testing Library's: it polls across frames, which the stub below runs on a 0 ms
// timeout (FloatingFocusManager.test.tsx:62-73, the focus-manager tests' precedent). Port titles say "port:".
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  attach,
  LEAVE_POINT,
  LIST_HIGHLIGHT_REPORT,
  NAVIGATE_POINT,
  relayKeyboardEvent,
  type CompositeListHighlightReport,
  type CompositeOptions,
} from './composite';
import { resetFocusManagerState } from './focus-manager';
import { resetInteractionsForTest } from './interactions';
import { resetFrameScheduler } from './transitions';
import type { DotNetTwin, TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';
import { createPopupHarness } from './test-support/interactions-harness';
import { createListHarness, type ListHarness, type ListHarnessOptions } from './test-support/list-harness';
import { createMenuTree, type MenuSpec, type MenuTree, type MenuTreeOptions } from './test-support/menu-tree-harness';

const harnesses: ListHarness[] = [];

beforeEach(() => {
  vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback: FrameRequestCallback) => {
    return window.setTimeout(() => callback(performance.now()), 0) as unknown as number;
  });
  vi.spyOn(window, 'cancelAnimationFrame').mockImplementation((id: number) => {
    window.clearTimeout(id);
  });
});

afterEach(() => {
  for (const harness of harnesses.splice(0)) {
    harness.dispose();
  }
  resetInteractionsForTest();
  resetFocusManagerState();
  resetFrameScheduler();
  document.body.replaceChildren();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

/** Base UI's `App`: the list's defaults, the options a test passes, three items "one", "two", "three". */
function app(options: ListHarnessOptions = {}): ListHarness {
  const harness = createListHarness(options);
  harnesses.push(harness);
  return harness;
}

/** `fireEvent.keyDown`: a bubbling, cancelable keydown on the target. */
function keyDown(target: EventTarget, key: string, init: KeyboardEventInit = {}): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, composed: true, ...init });
  target.dispatchEvent(event);
  return event;
}

/** Testing Library's `waitFor`: the assertion, retried across tasks until it passes. */
function waitFor(assertion: () => void): Promise<void> {
  return vi.waitFor(assertion, { timeout: 1000, interval: 5 });
}

function menu(harness: ListHarness): HTMLElement {
  const floating = harness.floating;
  if (!floating) {
    throw new Error('the menu is not mounted');
  }
  return floating;
}

describe('useListNavigation', () => {
  // CMP-DOM-04 (re-tagged [L2] → [L3]): the module's writes on the floating element include no `aria-orientation`.
  it('does not add role-dependent aria-orientation', async () => {
    const harness = app({ list: { orientation: 'horizontal' } });

    keyDown(harness.reference, 'ArrowRight');
    await waitFor(() => {
      expect(harness.item(0)).toBe(document.activeElement);
    });

    expect(menu(harness).hasAttribute('aria-orientation')).toBe(false);
  });

  it('opens on ArrowDown and focuses first item', async () => {
    const harness = app();

    keyDown(harness.reference, 'ArrowDown');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
  });

  it('opens on ArrowUp and focuses last item', async () => {
    const harness = app();

    keyDown(harness.reference, 'ArrowUp');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
  });

  it('navigates down on ArrowDown', async () => {
    const harness = app();

    keyDown(harness.reference, 'ArrowDown');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    keyDown(menu(harness), 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });

    keyDown(menu(harness), 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });

    // Reached the end of the list.
    keyDown(menu(harness), 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
  });

  it('navigates up on ArrowUp', async () => {
    const harness = app();

    keyDown(harness.reference, 'ArrowUp');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });

    keyDown(menu(harness), 'ArrowUp');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });

    keyDown(menu(harness), 'ArrowUp');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    // Reached the end of the list.
    keyDown(menu(harness), 'ArrowUp');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
  });

  it('skips disabled item on initial navigation', async () => {
    const harness = app({
      list: { loopFocus: true, disabledIndices: [] },
      itemAttributes: (index) => (index === 0 ? { 'aria-disabled': 'true' } : {}),
    });

    keyDown(harness.reference, 'ArrowDown');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });

    keyDown(menu(harness), 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });

    keyDown(menu(harness), 'ArrowUp');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });

    keyDown(menu(harness), 'ArrowUp');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
  });

  it('skips items hidden with CSS in navigation', async () => {
    const harness = app({ list: { loopFocus: true, disabledIndices: [] }, itemAttributes: (index) => (index === 0 ? { style: 'display: none' } : {}) });

    keyDown(harness.reference, 'ArrowDown');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });

    keyDown(menu(harness), 'ArrowUp');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
  });

  it('skips visibility:hidden items in navigation', async () => {
    const harness = app({ list: { loopFocus: true, disabledIndices: [] }, itemAttributes: (index) => (index === 0 ? { style: 'visibility: hidden' } : {}) });

    keyDown(harness.reference, 'ArrowDown');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });

    keyDown(menu(harness), 'ArrowUp');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
  });

  describe('prop: loopFocus', () => {
    it('ArrowDown looping', async () => {
      const harness = app({ list: { loopFocus: true } });

      keyDown(harness.reference, 'ArrowDown');
      expect(harness.floating).not.toBeNull();
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });

      keyDown(menu(harness), 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });

      keyDown(menu(harness), 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });

      // Reached the end of the list and loops.
      keyDown(menu(harness), 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
    });

    it('ArrowUp looping', async () => {
      const harness = app({ list: { loopFocus: true } });

      keyDown(harness.reference, 'ArrowUp');
      expect(harness.floating).not.toBeNull();
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });

      keyDown(menu(harness), 'ArrowUp');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });

      keyDown(menu(harness), 'ArrowUp');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });

      // Reached the end of the list and loops.
      keyDown(menu(harness), 'ArrowUp');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });
    });
  });
});

// The second block of useListNavigation.test.tsx. Base UI's `App` renders `aria-selected` from its `activeIndex`; the
// port's titles read the module's `data-highlighted`, which is that highlight's writer (ADR 04 Amendments 2026-11-03 (2)).
function highlighted(element: HTMLElement): boolean {
  return element.hasAttribute('data-highlighted');
}

/** `fireEvent.click`: a bubbling click with `detail: 1`, as React Testing Library dispatches it. */
function click(target: HTMLElement): void {
  target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, composed: true, detail: 1 }));
}

/** `fireEvent.mouseMove` with movement. */
function mouseMove(target: HTMLElement): void {
  target.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, cancelable: true, composed: true, movementX: 10, movementY: 10 }));
}

/** `fireEvent.pointerLeave`: a `pointerleave`, which does not bubble. */
function pointerLeave(target: HTMLElement, init: PointerEventInit = {}): void {
  target.dispatchEvent(new PointerEvent('pointerleave', { bubbles: false, cancelable: false, composed: true, pointerType: 'mouse', ...init }));
}

describe('useListNavigation (second block)', () => {
  describe('prop: orientation', () => {
    it('navigates down on ArrowRight', async () => {
      const harness = app({ list: { orientation: 'horizontal' } });

      keyDown(harness.reference, 'ArrowRight');
      expect(harness.floating).not.toBeNull();
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });

      keyDown(menu(harness), 'ArrowRight');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });

      keyDown(menu(harness), 'ArrowRight');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });

      // Reached the end of the list.
      keyDown(menu(harness), 'ArrowRight');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });
    });

    it('navigates up on ArrowLeft', async () => {
      const harness = app({ list: { orientation: 'horizontal' } });

      keyDown(harness.reference, 'ArrowLeft');
      expect(harness.floating).not.toBeNull();
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });

      keyDown(menu(harness), 'ArrowLeft');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });

      keyDown(menu(harness), 'ArrowLeft');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });

      // Reached the end of the list.
      keyDown(menu(harness), 'ArrowLeft');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
    });
  });

  // CMP-I18N-04: `rtl` from the direction provider swaps the horizontal arrows.
  describe('prop: rtl', () => {
    it('navigates down on ArrowLeft', async () => {
      const harness = app({ list: { rtl: true, orientation: 'horizontal' } });

      keyDown(harness.reference, 'ArrowLeft');
      expect(harness.floating).not.toBeNull();
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });

      keyDown(menu(harness), 'ArrowLeft');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });

      keyDown(menu(harness), 'ArrowLeft');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });

      // Reached the end of the list.
      keyDown(menu(harness), 'ArrowLeft');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });
    });

    it('navigates up on ArrowRight', async () => {
      const harness = app({ list: { rtl: true, orientation: 'horizontal' } });

      keyDown(harness.reference, 'ArrowRight');
      expect(harness.floating).not.toBeNull();
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(2));
      });

      keyDown(menu(harness), 'ArrowRight');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });

      keyDown(menu(harness), 'ArrowRight');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });

      // Reached the end of the list.
      keyDown(menu(harness), 'ArrowRight');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
    });
  });

  describe('prop: focusItemOnOpen', () => {
    it('focuses the first item on click when true', async () => {
      const harness = app({ list: { focusItemOnOpen: true } });
      click(harness.reference);
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
    });

    it('does not focus the first item on click when false', async () => {
      const harness = app({ list: { focusItemOnOpen: false } });
      click(harness.reference);
      await waitFor(() => {
        expect(harness.floating).not.toBeNull();
        expect(document.activeElement).not.toBe(harness.item(0));
      });
    });
  });

  describe('prop: selectedIndex', () => {
    it('scrolls the selected item into view on open', async () => {
      const frames: FrameRequestCallback[] = [];
      vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback) => {
        frames.push(callback);
        return 0;
      });
      const scrollIntoView = vi.fn();
      const originalScrollIntoView = HTMLElement.prototype.scrollIntoView;
      HTMLElement.prototype.scrollIntoView = scrollIntoView;
      try {
        const harness = app({ list: { selectedIndex: 0 } });
        click(harness.reference);
        await Promise.resolve();
        await Promise.resolve();
        expect(frames.length).toBeGreaterThan(0);
        // Run the timer
        frames.splice(0).forEach((callback) => callback(0));
        expect(scrollIntoView).toHaveBeenCalled();
      } finally {
        HTMLElement.prototype.scrollIntoView = originalScrollIntoView;
      }
    });
  });

  // CMP-KEY-33, CMP-ARIA-01's escape clause.
  describe('allowEscape + virtual', () => {
    it('when true', async () => {
      const harness = app({ list: { allowEscape: true, virtual: true, loopFocus: true } });
      keyDown(harness.reference, 'ArrowDown');
      expect(highlighted(harness.item(0))).toBe(true);
      keyDown(harness.reference, 'ArrowUp');
      expect(highlighted(harness.item(0))).toBe(false);
      keyDown(harness.reference, 'ArrowDown');
      expect(highlighted(harness.item(0))).toBe(true);
      keyDown(harness.reference, 'ArrowDown');
      expect(highlighted(harness.item(1))).toBe(true);
      keyDown(harness.reference, 'ArrowDown');
      expect(highlighted(harness.item(2))).toBe(true);
      keyDown(harness.reference, 'ArrowDown');
      expect(highlighted(harness.item(2))).toBe(false);
    });

    it('when false', async () => {
      const harness = app({ list: { allowEscape: false, virtual: true, loopFocus: true } });
      keyDown(harness.reference, 'ArrowDown');
      expect(highlighted(harness.item(0))).toBe(true);
      keyDown(harness.reference, 'ArrowDown');
      expect(highlighted(harness.item(1))).toBe(true);
    });

    it('true - onNavigate is called with `null` when escaped', async () => {
      const harness = app({ list: { allowEscape: true, virtual: true, loopFocus: true } });
      keyDown(harness.reference, 'ArrowDown');
      keyDown(harness.reference, 'ArrowUp');
      expect(harness.highlights).toHaveLength(2);
      expect(harness.highlights.some((report) => report.index === null)).toBe(true);
    });
  });

  describe('reference focus', () => {
    it('clears ordinary item focus when the open reference receives focus', async () => {
      const harness = app();
      const reference = harness.reference;

      keyDown(reference, 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });

      reference.focus();

      expect(highlighted(harness.item(0))).toBe(false);
    });
  });

  // CMP-KEY-19
  describe('prop: openOnArrowKeyDown', () => {
    it('opens on ArrowDown when true', async () => {
      const harness = app({ list: { openOnArrowKeyDown: true } });
      keyDown(harness.reference, 'ArrowDown');
      expect(harness.floating).not.toBeNull();
    });

    it('opens on ArrowUp when true', async () => {
      const harness = app({ list: { openOnArrowKeyDown: true } });
      keyDown(harness.reference, 'ArrowUp');
      expect(harness.floating).not.toBeNull();
    });

    it('does not open on ArrowDown when false', () => {
      const harness = app({ list: { openOnArrowKeyDown: false } });
      keyDown(harness.reference, 'ArrowDown');
      expect(harness.floating).toBeNull();
    });

    it('does not open on ArrowUp when false', () => {
      const harness = app({ list: { openOnArrowKeyDown: false } });
      keyDown(harness.reference, 'ArrowUp');
      expect(harness.floating).toBeNull();
    });
  });

  describe('prop: disabledIndices', () => {
    it('indices are skipped in focus order', async () => {
      const harness = app({ list: { disabledIndices: [0] }, itemAttributes: (index) => (index === 0 ? { 'aria-disabled': 'true' } : {}) });
      keyDown(harness.reference, 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });
      keyDown(menu(harness), 'ArrowUp');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(1));
      });
    });
  });

  // CMP-API-03, CMP-PTR-04..08.
  describe('prop: focusItemOnHover', () => {
    it('true - focuses item on hover and syncs the active index', async () => {
      const harness = app();
      click(harness.reference);
      await waitFor(() => {
        expect(harness.floating).not.toBeNull();
      });
      mouseMove(harness.item(1));
      expect(document.activeElement).toBe(harness.item(1));
      pointerLeave(harness.item(1));
      expect(document.activeElement).toBe(menu(harness));
      expect(harness.highlights.some((report) => report.index === 1)).toBe(true);
    });

    it('true - syncs an item on hover when activeIndex is null but selectedIndex matches', async () => {
      const harness = app({ list: { focusItemOnOpen: false, selectedIndex: 1 } });

      click(harness.reference);
      await waitFor(() => {
        expect(harness.floating).not.toBeNull();
      });
      mouseMove(harness.item(1));

      expect(document.activeElement).toBe(harness.item(1));
      expect(harness.highlights.some((report) => report.index === 1)).toBe(true);
    });

    it('false - does not focus item on hover and does not sync the active index', async () => {
      const harness = app({ list: { focusItemOnOpen: false, focusItemOnHover: false } });
      click(harness.reference);
      await waitFor(() => {
        expect(harness.floating).not.toBeNull();
      });
      mouseMove(harness.item(1));
      expect(document.activeElement).not.toBe(harness.item(1));
      expect(harness.highlights).toHaveLength(0);
    });

    it('clears the active item when the pointer leaves a clipped container while still within the item bounds', async () => {
      const harness = app();

      click(harness.reference);
      await waitFor(() => {
        expect(harness.floating).not.toBeNull();
      });

      const floating = menu(harness);
      const item = harness.item(1);
      floating.style.overflow = 'auto';
      floating.style.maxHeight = '40px';

      mouseMove(item);

      await waitFor(() => {
        expect(document.activeElement).toBe(item);
      });

      pointerLeave(item, { clientX: 50, clientY: 60, relatedTarget: document.body });

      await waitFor(() => {
        expect(highlighted(item)).toBe(false);
      });
      expect(harness.highlights.at(-1)?.index).toBe(null);
    });
  });
});

describe('useListNavigation (typeable combobox reference)', () => {
  // CMP-KEY-28's typeable branch.
  it('Home or End key press is ignored for typeable combobox reference', async () => {
    const harness = app({ reference: 'input' });

    harness.reference.focus();

    keyDown(harness.reference, 'ArrowDown');

    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    keyDown(harness.item(0), 'End');

    expect(document.activeElement).toBe(harness.item(0));

    keyDown(harness.item(0), 'ArrowDown');
    keyDown(harness.item(1), 'Home');

    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });
  });
});

// The highlight's writer (ADR 04 Amendments 2026-11-03 (2); CMP-STA-31, CMP-STA-34, CMP-STA-35; FOC-03..05's DOM
// clauses), port titles: the module writes each item's `tabindex` and `data-highlighted` in the task that moves the focus,
// and reports the highlight once per change.
describe('list highlight writer (port)', () => {
  it('port: writes the highlighted item\'s tabindex and data-highlighted in the task that moves the focus, and reports it once', async () => {
    const harness = app();

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    expect(harness.item(0).getAttribute('tabindex')).toBe('0');
    expect(harness.item(0).hasAttribute('data-highlighted')).toBe(true);

    keyDown(harness.item(0), 'ArrowDown');
    // Same task: the focus, the tabindex and the attribute moved together.
    expect(document.activeElement).toBe(harness.item(1));
    expect(harness.item(1).getAttribute('tabindex')).toBe('0');
    expect(harness.item(1).hasAttribute('data-highlighted')).toBe(true);
    expect(harness.item(0).getAttribute('tabindex')).toBe('-1');
    expect(harness.item(0).hasAttribute('data-highlighted')).toBe(false);

    expect(harness.highlights.map((report) => report.index)).toEqual([0, 1]);
    expect(harness.highlights[1]!.itemId).toBe(harness.item(1).id);
    expect(harness.highlights[1]!.event?.type).toBe('keydown');
    expect(harness.activeIndex).toBe(1);
  });

  it('port: requests the open with the reason list-navigation and the trigger, and the leave close with focus-out', async () => {
    const harness = app();

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    expect(harness.openRequests[0]).toMatchObject({ open: true, reason: 'list-navigation', triggerId: harness.reference.id });

    const event = keyDown(harness.item(0), 'Tab', { shiftKey: true });
    expect(event.defaultPrevented).toBe(true);
    expect(harness.openRequests[1]).toMatchObject({ open: false, reason: 'focus-out', triggerId: null });
    expect(harness.floating).toBeNull();
    expect(document.activeElement).toBe(harness.reference);
  });

  it('port: on WebAssembly a canceled Shift+Tab close leaves the focus where it was', async () => {
    const harness = app({
      synchronous: true,
      session: { onOpenChange: (open, details) => !open && details.reason === 'focus-out' && details.cancel() },
    });

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    keyDown(harness.item(0), 'Tab', { shiftKey: true });

    expect(harness.decisions.map((decision) => decision.point)).toContain(LEAVE_POINT);
    expect(harness.openRequests.filter((request) => !request.open)).toEqual([]);
    expect(harness.floating).not.toBeNull();
    expect(document.activeElement).toBe(harness.item(0));
  });

  // ADR 04 Amendments 2026-11-03 (3): `navigate` on the list handles, the consumer's onKeyDown veto (EVD-EVT-26).
  it('port: on WebAssembly a navigate veto leaves the key to the page, asked once before the first effect', async () => {
    const vetoed = app({ synchronous: true, navigateVerdict: (event) => (event.key === 'ArrowUp' ? 'stop' : 'run') });
    keyDown(vetoed.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(vetoed.item(0));
    });

    const event = keyDown(vetoed.item(0), 'ArrowUp');

    expect(vetoed.decisions.map((decision) => [decision.point, decision.event.key])).toEqual([
      [NAVIGATE_POINT, 'ArrowDown'],
      [NAVIGATE_POINT, 'ArrowUp'],
    ]);
    expect(event.defaultPrevented).toBe(false);
    expect(document.activeElement).toBe(vetoed.item(0));
  });
});

// Virtual focus (M5 track 1, Deliverable 5; ADR 04 Amendments 2026-11-03 (2); CMP-ARIA-01, CMP-ARIA-02, CMP-KEY-20,
// CMP-KEY-33, CMP-FOC-05). The module writes `aria-activedescendant` from the active item's own element id (a dated
// deviation from CMP-ARIA-01's `{id}-{index}` scheme), on the reference and, unless the reference is a typeable combobox,
// on the floating element, while the list is open with an active item and only under virtual focus; DOM focus stays on
// the reference.
describe('list virtual focus (port)', () => {
  function descendantOf(element: Element | null): string | null {
    return element?.getAttribute('aria-activedescendant') ?? null;
  }

  it('port: under virtual focus writes aria-activedescendant from the active item\'s own id on the reference and the floating element, and DOM focus stays on the reference', async () => {
    const harness = app({ list: { virtual: true } });
    harness.reference.focus();

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(highlighted(harness.item(0))).toBe(true);
    });
    expect(descendantOf(harness.reference)).toBe(harness.item(0).id);
    expect(descendantOf(menu(harness))).toBe(harness.item(0).id);
    expect(document.activeElement).toBe(harness.reference);

    keyDown(harness.reference, 'ArrowDown');
    expect(descendantOf(harness.reference)).toBe(harness.item(1).id);
    expect(descendantOf(menu(harness))).toBe(harness.item(1).id);
    expect(document.activeElement).toBe(harness.reference);
    expect(harness.item(1).getAttribute('tabindex')).toBe('-1');
  });

  it('port: under virtual focus with a typeable combobox reference writes aria-activedescendant on the reference only', async () => {
    const harness = app({ reference: 'input', list: { virtual: true } });
    harness.reference.focus();

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(0).id);
    });
    expect(menu(harness).hasAttribute('aria-activedescendant')).toBe(false);
    expect(document.activeElement).toBe(harness.reference);
  });

  it('port: aria-activedescendant is removed on close and absent while open with no active item', async () => {
    const harness = app({ list: { virtual: true } });
    harness.reference.focus();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(0).id);
    });

    harness.setOpen(false);
    expect(harness.reference.hasAttribute('aria-activedescendant')).toBe(false);

    // A pointer opening highlights nothing (CMP-STA-39).
    click(harness.reference);
    await waitFor(() => {
      expect(harness.floating).not.toBeNull();
    });
    expect(harness.reference.hasAttribute('aria-activedescendant')).toBe(false);
    expect(menu(harness).hasAttribute('aria-activedescendant')).toBe(false);
  });

  it('port: aria-activedescendant is never written under DOM focus, and leaving virtual focus removes the one written', async () => {
    const harness = app({ list: { virtual: true } });
    harness.reference.focus();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(descendantOf(menu(harness))).toBe(harness.item(0).id);
    });

    harness.update({ virtual: false });
    expect(harness.reference.hasAttribute('aria-activedescendant')).toBe(false);
    expect(menu(harness).hasAttribute('aria-activedescendant')).toBe(false);

    // Under DOM focus a key on the open reference, which holds the focus, starts at the boundary (useListNavigation.ts:
    // 637-645: the reference contains the focused element and no item does).
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    expect(harness.reference.hasAttribute('aria-activedescendant')).toBe(false);
    expect(menu(harness).hasAttribute('aria-activedescendant')).toBe(false);
  });

  // useListNavigation.test.tsx:319, on DOM built after its `Autocomplete`: an input reference without the combobox role,
  // `useDismiss` (the session's Escape), the consumer's `onChange` played as C# (a value clears the highlight by a request
  // and opens; an empty value closes), and `active-index` read from C#'s mirror of the highlight.
  it('resets indexRef to -1 upon close', async () => {
    const data = ['a', 'ab', 'abc', 'abcd'];
    const harness = app({ reference: 'input', items: data, list: { virtual: true, loopFocus: true } });
    const input = harness.reference as HTMLInputElement;
    input.removeAttribute('role');
    input.setAttribute('aria-autocomplete', 'list');
    harness.popup.updateSession({ session: { dismiss: {} } });
    let revision = 0;
    const onChange = (value: string) => {
      input.value = value;
      if (value) {
        harness.update({ highlight: { index: null, revision: (revision += 1) } });
        harness.setOpen(true);
      } else {
        harness.setOpen(false);
      }
    };
    const typeKey = (key: string, value: string) => {
      keyDown(input, key);
      onChange(value);
    };

    input.focus();
    typeKey('a', 'a');

    expect(harness.floating).not.toBeNull();
    expect(harness.activeIndex).toBe(null);

    keyDown(input, 'ArrowDown');
    keyDown(input, 'ArrowDown');
    keyDown(input, 'ArrowDown');

    expect(harness.activeIndex).toBe(2);

    keyDown(input, 'Escape');

    await waitFor(() => {
      expect(harness.activeIndex).toBe(null);
    });

    typeKey('Backspace', '');
    typeKey('a', 'a');

    expect(harness.floating).not.toBeNull();
    expect(harness.activeIndex).toBe(null);

    keyDown(input, 'ArrowDown');

    expect(harness.activeIndex).toBe(0);
  });
});

// The popup grid (M5 track 1, Deliverable 9; CMP-ALG-07, ALG-08, ALG-13, KEY-29, EDGE-16, EDGE-17):
// useListNavigation.test.tsx's grid titles, verbatim, on DOM built after `test/floating-ui-tests/Grid.tsx` (49 option
// buttons, 5 columns), `ComplexGrid.tsx` (37, 7 columns), the file's own `VirtualizedGridRows` (an input reference, 15
// rendered gridcells in `role="row"` rows of 5 out of a longer list) and `EmojiPicker.tsx` (an input inside the popup, 3
// columns). The fixtures wrap Base UI's navigator with their column count (`gridNavigationWithColumns.ts`); the port
// reads it from the list's `cols`, and a virtualized list's length (`listRef.current.length`) from `itemCount`.
// `fireEvent.click` dispatches `detail: 0`, which `isVirtualClick` reads as a virtual click (CMP-ALG-28), so a click
// opening highlights the first enabled item as Base UI's does; `data-active-index` is C#'s mirror of the highlight.
function fireClick(target: HTMLElement): void {
  target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, composed: true, button: 0 }));
}

/** `screen.getAllByRole('option')[index]` (or `gridcell`): the rendered item at an index. */
function option(harness: ListHarness, index: number): HTMLElement {
  return harness.item(index);
}

async function opened(harness: ListHarness): Promise<void> {
  await waitFor(() => {
    expect(harness.floating).not.toBeNull();
  });
}

/** Grid.tsx's `Main`: 49 option buttons in 5 columns, 12 of them disabled, opened by a click only. */
function gridFixture(props: { orientation?: 'horizontal' | 'both'; loopFocus?: boolean } = {}): ListHarness {
  const disabledIndices = [0, 1, 2, 3, 4, 5, 6, 7, 10, 15, 45, 48];
  return app({
    items: Array.from({ length: 49 }, (_, index) => `Item ${index}`),
    itemTag: 'button',
    flat: true,
    itemAttributes: (index) => ({ type: 'button', role: 'option', ...(disabledIndices.includes(index) ? { disabled: '' } : {}) }),
    list: {
      orientation: props.orientation ?? 'horizontal',
      loopFocus: props.loopFocus ?? false,
      openOnArrowKeyDown: false,
      disabledIndices,
      grid: true,
      cols: 5,
    },
  });
}

/** ComplexGrid.tsx's `Main`: 37 option buttons in 7 columns, 11 of them disabled. */
function complexGridFixture(props: { orientation?: 'horizontal' | 'both'; loopFocus?: boolean; rtl?: boolean } = {}): ListHarness {
  const disabledIndices = [0, 1, 2, 3, 4, 5, 6, 9, 14, 23, 35];
  return app({
    items: Array.from({ length: 37 }, (_, index) => `Item ${index}`),
    itemTag: 'button',
    flat: true,
    floatingRole: 'presentation',
    itemAttributes: (index) => ({ type: 'button', role: 'option', ...(disabledIndices.includes(index) ? { disabled: '' } : {}) }),
    list: {
      orientation: props.orientation ?? 'horizontal',
      loopFocus: props.loopFocus ?? false,
      rtl: props.rtl ?? false,
      openOnArrowKeyDown: false,
      disabledIndices,
      grid: true,
      cols: 7,
    },
  });
}

/**
 * useListNavigation.test.tsx's `VirtualizedGridRows`: open from the start, an input reference under virtual focus, the
 * first three rows of five gridcells rendered out of `totalItems`, the consumer's initial `activeIndex` as C#'s first
 * highlight request.
 */
function virtualizedGridRows(
  props: { totalItems?: number; initialActiveIndex?: number; loopFocus?: boolean; disabledIndices?: number[]; hiddenIndices?: number[] } = {},
): ListHarness {
  const { totalItems = 100, initialActiveIndex = 0, loopFocus = true, disabledIndices, hiddenIndices } = props;
  const COLUMNS = 5;
  const VISIBLE_ROWS = 3;
  const harness = app({
    reference: 'input',
    floatingRole: 'grid',
    items: Array.from({ length: Math.min(totalItems, COLUMNS * VISIBLE_ROWS) }, (_, index) => `${index}`),
    itemTag: 'button',
    rowSize: COLUMNS,
    itemAttributes: (index) => ({ type: 'button', role: 'gridcell', ...(hiddenIndices?.includes(index) ? { style: 'display: none' } : {}) }),
    session: { open: true },
    list: {
      virtual: true,
      loopFocus,
      orientation: 'horizontal',
      ...(disabledIndices ? { disabledIndices } : {}),
      grid: true,
      itemCount: totalItems,
      highlight: { index: initialActiveIndex, revision: 1 },
    },
  });
  harness.reference.removeAttribute('role');
  return harness;
}

/** Presses a key on the floating element `times` times, as consecutive `fireEvent.keyDown` calls. */
function pressOnMenu(harness: ListHarness, key: string, times = 1): void {
  for (let count = 0; count < times; count += 1) {
    keyDown(menu(harness), key);
  }
}

describe('useListNavigation (grid)', () => {
  describe('grid navigation', () => {
    it('ArrowDown focuses first item', async () => {
      const harness = gridFixture();

      fireClick(harness.reference);
      await opened(harness);
      keyDown(document, 'ArrowDown');
      await waitFor(() => {
        expect(option(harness, 8)).toBe(document.activeElement);
      });
    });

    it('focuses first non-disabled item in grid', async () => {
      const harness = gridFixture();
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await waitFor(() => {
        expect(option(harness, 8)).toBe(document.activeElement);
      });
    });

    it('focuses next item using ArrowRight key, skipping disabled items', async () => {
      const harness = gridFixture();
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);
      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 9)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 11)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowRight', 3);
      expect(option(harness, 14)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 16)).toBe(document.activeElement);
    });

    it('focuses previous item using ArrowLeft key, skipping disabled items', async () => {
      const harness = gridFixture();
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);

      option(harness, 47).focus();

      pressOnMenu(harness, 'ArrowLeft');
      expect(option(harness, 46)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowLeft');
      expect(option(harness, 44)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowLeft', 3);
      expect(option(harness, 41)).toBe(document.activeElement);
    });

    it('skips row and remains on same column when pressing ArrowDown', async () => {
      const harness = gridFixture();
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);
      pressOnMenu(harness, 'ArrowDown');
      expect(option(harness, 13)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowDown');
      expect(option(harness, 18)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowDown');
      expect(option(harness, 23)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowDown');
      expect(option(harness, 28)).toBe(document.activeElement);
    });

    it('skips row and remains on same column when pressing ArrowUp', async () => {
      const harness = gridFixture();
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);

      option(harness, 47).focus();

      pressOnMenu(harness, 'ArrowUp');
      expect(option(harness, 42)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowUp');
      expect(option(harness, 37)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowUp');
      expect(option(harness, 32)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowUp');
      expect(option(harness, 27)).toBe(document.activeElement);
    });

    it('loops on the same column with ArrowDown', async () => {
      const harness = gridFixture({ loopFocus: true });
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);

      pressOnMenu(harness, 'ArrowDown', 8);

      expect(option(harness, 8)).toBe(document.activeElement);
    });

    it('loops on the same column with ArrowUp', async () => {
      const harness = gridFixture({ loopFocus: true });
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);

      option(harness, 43).focus();

      pressOnMenu(harness, 'ArrowUp', 8);

      expect(option(harness, 43)).toBe(document.activeElement);
    });

    it('does not leave row with "both" orientation while looping', async () => {
      const harness = gridFixture({ orientation: 'both', loopFocus: true });
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);

      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 9)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 8)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowLeft');
      expect(option(harness, 9)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowLeft');
      expect(option(harness, 8)).toBe(document.activeElement);

      pressOnMenu(harness, 'ArrowDown');
      expect(option(harness, 13)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 14)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 11)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowLeft');
      expect(option(harness, 14)).toBe(document.activeElement);
    });

    it('looping works on last row', async () => {
      const harness = gridFixture({ orientation: 'both', loopFocus: true });
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await opened(harness);

      option(harness, 46).focus();

      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 47)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowRight');
      expect(option(harness, 46)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowLeft');
      expect(option(harness, 47)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowLeft');
      expect(option(harness, 46)).toBe(document.activeElement);
    });

    it('wraps ArrowUp to the last row in the full list for virtualized rows', async () => {
      const harness = virtualizedGridRows();

      harness.reference.focus();

      keyDown(harness.reference, 'ArrowUp');

      await waitFor(() => {
        expect(harness.activeIndex).toBe(95);
      });
    });

    it('clamps ArrowUp to the last item in a partial last row for virtualized rows', async () => {
      const harness = virtualizedGridRows({ totalItems: 98, initialActiveIndex: 4 });

      harness.reference.focus();

      keyDown(harness.reference, 'ArrowUp');

      await waitFor(() => {
        expect(harness.activeIndex).toBe(97);
      });
    });

    it('clamps ArrowDown into a partial last row for virtualized rows', async () => {
      const harness = virtualizedGridRows({ totalItems: 98, initialActiveIndex: 93 });

      harness.reference.focus();

      keyDown(harness.reference, 'ArrowDown');

      await waitFor(() => {
        expect(harness.activeIndex).toBe(97);
      });
    });

    it('does not wrap ArrowUp when loopFocus is false for virtualized rows', async () => {
      const harness = virtualizedGridRows({ totalItems: 98, initialActiveIndex: 4, loopFocus: false });

      harness.reference.focus();

      const event = keyDown(harness.reference, 'ArrowUp');

      await waitFor(() => {
        expect(harness.activeIndex).toBe(4);
      });
      // The grid navigator consumed the key all the same (`stopEvent: true`).
      expect(event.defaultPrevented).toBe(true);
    });

    it('still clamps ArrowDown into a partial last row when loopFocus is false', async () => {
      const harness = virtualizedGridRows({ totalItems: 98, initialActiveIndex: 93, loopFocus: false });

      harness.reference.focus();

      keyDown(harness.reference, 'ArrowDown');

      await waitFor(() => {
        expect(harness.activeIndex).toBe(97);
      });
    });

    it('falls back left in a partial last row when the preferred candidate is disabled', async () => {
      const harness = virtualizedGridRows({ totalItems: 98, initialActiveIndex: 93, disabledIndices: [97] });

      harness.reference.focus();

      keyDown(harness.reference, 'ArrowDown');

      await waitFor(() => {
        expect(harness.activeIndex).toBe(96);
      });
    });

    it('falls back left when the preferred candidate is hidden', async () => {
      const harness = virtualizedGridRows({ initialActiveIndex: 9, hiddenIndices: [14] });

      harness.reference.focus();

      keyDown(harness.reference, 'ArrowDown');

      await waitFor(() => {
        expect(harness.activeIndex).toBe(13);
      });
    });
  });

  describe('grid navigation in a multi-column grid with disabled items', () => {
    it('focuses first non-disabled item in grid', async () => {
      const harness = complexGridFixture();
      keyDown(harness.reference, 'Enter');
      fireClick(harness.reference);
      await waitFor(() => {
        expect(option(harness, 7)).toBe(document.activeElement);
      });
    });

    describe.each([
      { rtl: false, arrowToStart: 'ArrowLeft', arrowToEnd: 'ArrowRight' },
      { rtl: true, arrowToStart: 'ArrowRight', arrowToEnd: 'ArrowLeft' },
    ])('with rtl $rtl', ({ rtl, arrowToStart, arrowToEnd }) => {
      it(`focuses next item using ${arrowToEnd} key, skipping disabled items`, async () => {
        const harness = complexGridFixture({ rtl });
        keyDown(harness.reference, 'Enter');
        fireClick(harness.reference);
        await opened(harness);
        pressOnMenu(harness, arrowToEnd);
        expect(option(harness, 8)).toBe(document.activeElement);
        pressOnMenu(harness, arrowToEnd);
        expect(option(harness, 10)).toBe(document.activeElement);
        pressOnMenu(harness, arrowToEnd, 3);
        expect(option(harness, 13)).toBe(document.activeElement);
        pressOnMenu(harness, arrowToEnd);
        expect(option(harness, 15)).toBe(document.activeElement);
        pressOnMenu(harness, arrowToEnd, 5);
        expect(option(harness, 20)).toBe(document.activeElement);
        pressOnMenu(harness, arrowToEnd, 3);
        expect(option(harness, 24)).toBe(document.activeElement);
        pressOnMenu(harness, arrowToEnd, 10);
        expect(option(harness, 34)).toBe(document.activeElement);
        pressOnMenu(harness, arrowToEnd);
        expect(option(harness, 36)).toBe(document.activeElement);
      });

      it(`focuses previous item using ${arrowToStart} key, skipping disabled items`, async () => {
        const harness = complexGridFixture({ rtl });
        keyDown(harness.reference, 'Enter');
        fireClick(harness.reference);
        await opened(harness);

        option(harness, 36).focus();

        pressOnMenu(harness, arrowToStart);
        await waitFor(() => {
          expect(option(harness, 34)).toBe(document.activeElement);
        });
        pressOnMenu(harness, arrowToStart, 6);
        await waitFor(() => {
          expect(option(harness, 28)).toBe(document.activeElement);
        });
        pressOnMenu(harness, arrowToStart, 7);
        await waitFor(() => {
          expect(option(harness, 20)).toBe(document.activeElement);
        });
        pressOnMenu(harness, arrowToStart, 11);
        await waitFor(() => {
          expect(option(harness, 7)).toBe(document.activeElement);
        });
      });

      it('looping works on last row', async () => {
        const harness = complexGridFixture({ rtl, orientation: 'both', loopFocus: true });
        keyDown(harness.reference, 'Enter');
        fireClick(harness.reference);
        await opened(harness);

        option(harness, 36).focus();

        pressOnMenu(harness, arrowToEnd);
        expect(option(harness, 36)).toBe(document.activeElement);
      });
    });
  });

  it('grid navigation with changing list items', async () => {
    const picker = emojiPicker();

    fireClick(picker.button);

    await flushMicrotasks();

    await waitFor(() => {
      expect(picker.input()).toBe(document.activeElement);
    });

    picker.type('appl');
    const initialActiveIndex = picker.activeIndex;
    picker.press('ArrowDown');

    await waitFor(() => {
      expect(picker.activeIndex).not.toBe(initialActiveIndex);
    });

    picker.press('ArrowDown');

    await waitFor(() => {
      expect(picker.activeIndex).not.toBe(initialActiveIndex);
    });

    expect(picker.activeIndex).not.toBeNull();
  });

  it('grid navigation with disabled list items', async () => {
    const picker = emojiPicker();

    fireClick(picker.button);

    await flushMicrotasks();

    await waitFor(() => {
      expect(picker.input()).toBe(document.activeElement);
    });

    picker.type('o');
    const initialActiveIndex = picker.activeIndex;
    picker.press('ArrowDown');

    expect(picker.option('orange').hasAttribute('data-highlighted')).toBe(false);
    await waitFor(() => {
      expect(picker.activeIndex).not.toBe(initialActiveIndex);
    });

    picker.press('ArrowDown');

    await waitFor(() => {
      expect(picker.activeIndex).not.toBe(initialActiveIndex);
    });

    expect(picker.activeIndex).not.toBeNull();

    picker.unmount();

    const next = emojiPicker();

    fireClick(next.button);

    await flushMicrotasks();

    await waitFor(() => {
      expect(next.input()).toBe(document.activeElement);
    });

    const nextInitialActiveIndex = next.activeIndex;
    next.press('ArrowDown');
    next.press('ArrowDown');
    next.press('ArrowRight');
    next.press('ArrowUp');

    await waitFor(() => {
      expect(next.activeIndex).not.toBe(nextInitialActiveIndex);
    });
    expect(next.option('cherry').hasAttribute('data-highlighted')).toBe(true);
  });
});

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => queueMicrotask(resolve));
}

// Nested lists (M5 track 1, Deliverable 6; CMP-STA-41, KEY-23, KEY-27, DIS-01, EVT-07, FOC-08): useListNavigation.test.tsx's
// three `it.skipIf(!isJSDOM)` titles, verbatim, on DOM built after `test/floating-ui-tests/Menu.tsx` and
// `MenuOrientation.tsx` by the menu-tree harness: one session per menu linked by the parent's token, each popup portaled
// outside its parent popup's DOM. In Base UI a portaled child popup's keydown bubbles through the React tree to its parent
// popup's handlers; the port hands a keydown the child's floating handle did not stop to the parent list's floating
// handle. userEvent's `click` and `keyboard` are restated as their DOM events on the focused element, each followed by the
// task userEvent's zero delay awaits (the frame stub runs on it).
const trees: MenuTree[] = [];

afterEach(() => {
  for (const tree of trees.splice(0)) {
    tree.dispose();
  }
});

function menuTree(spec: MenuSpec, options: MenuTreeOptions = {}): MenuTree {
  const tree = createMenuTree(spec, options);
  trees.push(tree);
  return tree;
}

function nextTask(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

/** userEvent's `click`: the pointer and mouse sequence, the focus on mousedown, then the click. */
async function userClick(target: HTMLElement): Promise<void> {
  target.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, composed: true, pointerType: 'mouse' }));
  target.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, composed: true, detail: 1 }));
  target.focus();
  target.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, cancelable: true, composed: true, pointerType: 'mouse' }));
  target.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, composed: true, detail: 1 }));
  target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, composed: true, detail: 1 }));
  await nextTask();
  await nextTask();
}

/** userEvent's `keyboard('{Key}')`: a keydown on the focused element. */
async function keyboard(key: string): Promise<void> {
  keyDown(document.activeElement ?? document.body, key);
  await nextTask();
  await nextTask();
}

/** Menu.tsx's `Main`. */
function nestedMenu(): MenuSpec {
  return {
    label: 'Edit',
    items: [
      { label: 'Undo' },
      { label: 'Redo' },
      { label: 'Cut', disabled: true },
      {
        label: 'Copy as',
        keepMounted: true,
        items: [
          { label: 'Text' },
          { label: 'Video' },
          { label: 'Image', keepMounted: true, grid: true, orientation: 'horizontal', items: [{ label: '.png' }, { label: '.jpg' }, { label: '.svg' }, { label: '.gif' }] },
          { label: 'Audio' },
        ],
      },
      { label: 'Share', items: [{ label: 'Mail' }, { label: 'Instagram' }] },
    ],
  };
}

/** MenuOrientation.tsx's `HorizontalMenu`. */
function horizontalMenu(): MenuSpec {
  return {
    label: 'Edit',
    orientation: 'horizontal',
    items: [
      { label: 'Undo' },
      { label: 'Redo' },
      { label: 'Cut', disabled: true },
      {
        label: 'Copy as',
        keepMounted: true,
        items: [
          { label: 'Text' },
          { label: 'Video' },
          { label: 'Image', keepMounted: true, grid: true, items: [{ label: '.png' }, { label: '.jpg' }, { label: '.svg' }, { label: '.gif' }] },
          { label: 'Audio' },
        ],
      },
      { label: 'Share', items: [{ label: 'Mail' }, { label: 'Instagram' }] },
    ],
  };
}

describe('useListNavigation (nested)', () => {
  // In JSDOM it will not focus the first item, but will in the browser
  it('focus management in nested lists', async () => {
    const tree = menuTree(nestedMenu());
    await userClick(tree.byText('Edit'));
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowRight');

    expect(tree.byText('Text')).toBe(document.activeElement);
  });

  // In JSDOM it will not focus the first item, but will in the browser
  it('keyboard navigation in nested menus lists', async () => {
    const tree = menuTree(nestedMenu());

    await userClick(tree.byText('Edit'));
    await flushMicrotasks();
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowRight'); // opens first submenu
    await flushMicrotasks();

    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowRight'); // opens second submenu
    await flushMicrotasks();

    expect(tree.byText('.png')).toBe(document.activeElement);

    // it navigation with orientation = 'both'
    await keyboard('ArrowRight');
    expect(tree.byText('.jpg')).toBe(document.activeElement);

    await keyboard('ArrowDown');
    expect(tree.byText('.gif')).toBe(document.activeElement);

    await keyboard('ArrowLeft');
    expect(tree.byText('.svg')).toBe(document.activeElement);

    await keyboard('ArrowUp');
    expect(tree.byText('.png')).toBe(document.activeElement);

    // escape closes the submenu
    await keyboard('Escape');
    expect(tree.byText('Image')).toBe(document.activeElement);
  });

  // In JSDOM it will not focus the first item, but will in the browser
  it('keyboard navigation in nested menus with different orientation', async () => {
    const tree = menuTree(horizontalMenu());

    await userClick(tree.byText('Edit'));
    await flushMicrotasks();
    await keyboard('ArrowRight');
    await keyboard('ArrowRight');
    await keyboard('ArrowRight');
    await keyboard('ArrowDown'); // opens the Copy as submenu
    await flushMicrotasks();

    await keyboard('ArrowRight');
    await keyboard('ArrowDown'); // opens the Share submenu
    await flushMicrotasks();

    expect(tree.byText('Mail')).toBe(document.activeElement);

    await keyboard('ArrowLeft');
    expect(tree.byText('Copy as')).toBe(document.activeElement);
  });

  // CTX-KEY-07 not replicated (ADR 04 Amendments 2026-10-18): ContextMenu's root menu is nested under the context-menu
  // parent, and `keepOpenOnCrossOrientationKey` keeps it open on its cross-orientation close key (CMP-KEY-27).
  it('port: a nested list with keepOpenOnCrossOrientationKey stays open on its cross-orientation close key, which another nested list closes on', async () => {
    const tree = menuTree(nestedMenu());
    await userClick(tree.byText('Edit'));
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowRight');
    expect(tree.byText('Text')).toBe(document.activeElement);

    // Without the option the vertical submenu closes on ArrowLeft and returns the focus to its trigger.
    await keyboard('ArrowLeft');
    expect(tree.menu('Copy as').openRequests.at(-1)).toMatchObject({ open: false, reason: 'list-navigation', triggerId: null });
    expect(tree.menu('Copy as').popup.open).toBe(false);
    expect(tree.byText('Copy as')).toBe(document.activeElement);

    const kept = menuTree({ ...nestedMenu(), label: 'Edit 2', items: nestedMenu().items.map((item) => ('items' in item && item.label === 'Copy as' ? { ...item, label: 'Copy as 2', list: { keepOpenOnCrossOrientationKey: true } } : item)) });
    await userClick(kept.byText('Edit 2'));
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowDown');
    await keyboard('ArrowRight');
    const text = Array.from(kept.menu('Copy as 2').floating!.querySelectorAll<HTMLElement>('button')).find((element) => element.textContent === 'Text')!;
    expect(text).toBe(document.activeElement);

    const event = keyDown(text, 'ArrowLeft');
    expect(kept.menu('Copy as 2').openRequests.filter((request) => !request.open)).toEqual([]);
    expect(kept.menu('Copy as 2').popup.open).toBe(true);
    expect(event.defaultPrevented).toBe(false);
  });
});

// The keyboard relay (M5 track 1, Deliverable 7; CMP-KEY-14; ADR 04 Amendments 2026-11-03 (4); design point 3 (a), settled
// by the orchestrator 2026-11-04): a composite root publishes its key resolution under a symbol on its element;
// `relayKeyboardEvent(rootElement, event)` runs it for a keydown that did not bubble through the root and stops the event
// when handled; a list whose `relayElement` names the root in its references relays the keydowns its floating handle did
// not stop (MenuRoot.tsx:692-699). The host is a test-only menubar: a horizontal composite root of three buttons, whose
// "Edit" item is the trigger of a menu (Menubar's: nested under the bar's horizontal orientation) portaled outside it.
describe.each([false, true])('list keyboard relay (port, synchronous: %s)', (synchronous) => {
  interface Bar {
    root: HTMLElement;
    items: HTMLElement[];
    fake: FakeTwin;
    handle: TwinHandle;
  }

  const bars: Bar[] = [];
  const cleanups: Array<() => void> = [];
  let barIds = 0;

  afterEach(() => {
    for (const bar of bars.splice(0)) {
      bar.handle.dispose();
    }
    for (const cleanup of cleanups.splice(0)) {
      cleanup();
    }
  });

  /** Records whether each keydown reached the document, which a stopped event does not. */
  function documentKeys(): string[] {
    const keys: string[] = [];
    const listener = (event: KeyboardEvent) => keys.push(event.key);
    document.addEventListener('keydown', listener);
    cleanups.push(() => document.removeEventListener('keydown', listener));
    return keys;
  }

  function menubar(): Bar {
    barIds += 1;
    const root = document.createElement('div');
    root.setAttribute('role', 'menubar');
    const items = ['File', 'Edit', 'View'].map((label, index) => {
      const element = document.createElement('button');
      element.type = 'button';
      element.id = `menubar-${barIds}-item-${index}`;
      element.textContent = label;
      element.setAttribute('tabindex', '-1');
      return element;
    });
    root.append(...items);
    document.body.appendChild(root);
    const fake = createFakeTwin({ synchronous });
    const options: CompositeOptions = {
      orientation: 'horizontal',
      loopFocus: true,
      enableHomeAndEndKeys: true,
      stopEventPropagation: true,
      highlightItemOnHover: false,
      modifierKeys: [],
      direction: 'ltr',
      items: items.map((element) => ({ id: element.id, tabIndex: null })),
      selectOnNavigation: false,
      reportFocusLeave: false,
      reportHighlight: false,
      reportHandledKeys: true,
    };
    const bar: Bar = { root, items, fake, handle: attach(root, JSON.stringify(options), fake.twin) };
    bars.push(bar);
    return bar;
  }

  function barMenu(bar: Bar, relay = true): MenuTree {
    return menuTree(
      {
        label: 'Edit',
        items: [{ label: 'Undo' }, { label: 'Redo' }],
        list: { nested: true, parentOrientation: 'horizontal', ...(relay ? { relayElement: 0 } : {}) },
      },
      { synchronous, rootTrigger: bar.items[1]!, references: [bar.root] },
    );
  }

  async function openFromBar(bar: Bar, tree: MenuTree): Promise<HTMLElement> {
    bar.items[1]!.focus();
    keyDown(bar.items[1]!, 'ArrowDown');
    const undo = await vi.waitFor(() => {
      const found = tree.byText('Undo');
      expect(found).toBe(document.activeElement);
      return found;
    });
    return undo;
  }

  it('port: a keydown the list does not stop is relayed to the composite root its relayElement names, and a key it handles is not', async () => {
    const bar = menubar();
    const tree = barMenu(bar);
    const undo = await openFromBar(bar, tree);
    expect(bar.items[1]!.getAttribute('tabindex')).toBe('0');

    // The list handles ArrowDown: nothing reaches the bar.
    keyDown(undo, 'ArrowDown');
    expect(tree.byText('Redo')).toBe(document.activeElement);
    expect(bar.items[1]!.getAttribute('tabindex')).toBe('0');

    // ArrowRight is no key of the vertical list: the bar moves to the next item, and the event stops there.
    const reached = documentKeys();
    const event = keyDown(tree.byText('Redo'), 'ArrowRight');
    expect(event.defaultPrevented).toBe(true);
    expect(reached).toEqual([]);
    expect(bar.items[2]!.getAttribute('tabindex')).toBe('0');
    await flushMicrotasks();
    expect(document.activeElement).toBe(bar.items[2]);

    if (synchronous) {
      // WebAssembly: the bar's `navigate` point runs the bar's handlers for the relayed key (option (i)).
      expect(bar.fake.decisions.map((decision) => [decision.point, decision.event.key])).toEqual([[NAVIGATE_POINT, 'ArrowRight']]);
      expect(bar.fake.reported('OnKeyHandled')).toEqual([]);
    } else {
      // Server: the bar's handled-key report runs them.
      expect(bar.fake.reported<{ event: { key: string } }>('OnKeyHandled').map((report) => report.event.key)).toEqual(['ArrowRight']);
    }
  });

  it('port: a nested close key that is the bar\'s main key closes the menu and is relayed, moving the bar to the previous item', async () => {
    const bar = menubar();
    const tree = barMenu(bar);
    const undo = await openFromBar(bar, tree);

    const event = keyDown(undo, 'ArrowLeft');

    expect(tree.root.openRequests.at(-1)).toMatchObject({ open: false, reason: 'list-navigation' });
    expect(event.defaultPrevented).toBe(true);
    expect(bar.items[0]!.getAttribute('tabindex')).toBe('0');
    await flushMicrotasks();
    expect(document.activeElement).toBe(bar.items[0]);
  });

  it('port: without relayElement a keydown the list does not stop leaves the bar unchanged', async () => {
    const bar = menubar();
    const relayed = barMenu(bar);
    const undo = await openFromBar(bar, relayed);
    keyDown(undo, 'ArrowRight');
    expect(bar.items[2]!.getAttribute('tabindex')).toBe('0');
    relayed.dispose();
    trees.splice(trees.indexOf(relayed), 1);

    const other = menubar();
    const tree = barMenu(other, false);
    const otherUndo = await openFromBar(other, tree);
    const event = keyDown(otherUndo, 'ArrowRight');
    expect(event.defaultPrevented).toBe(false);
    expect(other.items[1]!.getAttribute('tabindex')).toBe('0');
    expect(other.items[2]!.getAttribute('tabindex')).toBe('-1');
  });
});

describe('relayKeyboardEvent (port)', () => {
  it('port: runs a composite root\'s key resolution for a keydown that did not bubble through it, stops it when handled, and returns false for an element that is no root', async () => {
    const root = document.createElement('div');
    const items = ['a', 'b', 'c'].map((label, index) => {
      const element = document.createElement('button');
      element.id = `relay-item-${index}`;
      element.textContent = label;
      return element;
    });
    root.append(...items);
    const outside = document.createElement('button');
    document.body.append(root, outside);
    const fake = createFakeTwin();
    const handle = attach(
      root,
      JSON.stringify({
        orientation: 'horizontal',
        loopFocus: true,
        enableHomeAndEndKeys: false,
        stopEventPropagation: false,
        highlightItemOnHover: false,
        modifierKeys: [],
        direction: 'ltr',
        items: items.map((element) => ({ id: element.id, tabIndex: null })),
        selectOnNavigation: false,
        reportFocusLeave: false,
        reportHighlight: true,
      } satisfies CompositeOptions),
      fake.twin,
    );
    const reached: string[] = [];
    const onDocumentKeyDown = (event: KeyboardEvent) => reached.push(event.key);
    document.addEventListener('keydown', onDocumentKeyDown);
    try {
      const results: boolean[] = [];
      outside.addEventListener('keydown', (event) => {
        results.push(relayKeyboardEvent(root, event));
        results.push(relayKeyboardEvent(outside, event));
      });

      // The root's own `stopEventPropagation` is off: the relay stops the handled event all the same.
      const event = keyDown(outside, 'ArrowRight');

      expect(results).toEqual([true, false]);
      expect(event.defaultPrevented).toBe(true);
      expect(reached).toEqual([]);
      expect(fake.reported('OnHighlightChange')).toEqual([{ index: 1 }]);
      await flushMicrotasks();
      expect(document.activeElement).toBe(items[1]);

      // A key the root does not act on is not handled, and nothing stops it.
      keyDown(outside, 'ArrowDown');
      expect(results.slice(2)).toEqual([false, false]);
      expect(reached).toEqual(['ArrowDown']);
    } finally {
      document.removeEventListener('keydown', onDocumentKeyDown);
      handle.dispose();
    }
  });
});

interface EmojiPicker {
  readonly button: HTMLElement;
  /** C#'s mirror of the highlight (`emoji-picker-active-index`). */
  readonly activeIndex: number | null;
  input(): HTMLInputElement;
  option(name: string): HTMLElement;
  /** userEvent's keyboard: a keydown per character on the input, and the input's change. */
  type(text: string): void;
  press(key: string): void;
  unmount(): void;
}

/**
 * EmojiPicker.tsx's `Main` as C# would render it: the button is the popup's trigger (a click opens, the session's
 * dismissal is on), and the list's reference is the search input inside the popup, so the reference handle attaches to
 * it with the floating handle when the popup renders; the focus manager's initial focus lands on the input. The input's
 * change clears the highlight by a request and filters the options.
 */
function emojiPicker(): EmojiPicker {
  const emojis = ['apple', 'orange', 'watermelon', 'strawberry', 'pear', 'banana', 'pineapple', 'cherry', 'peach'];
  let render: () => void = () => {};
  const popup = createPopupHarness({ onOpenChange: () => queueMicrotask(() => render()) });
  const button = document.createElement('button');
  button.setAttribute('aria-label', 'Choose emoji');
  document.body.appendChild(button);
  popup.attachSession({ session: { dismiss: {} } });
  popup.attachTrigger(button, { trigger: { click: {} } });
  popup.setReference(button);

  let search = '';
  let revision = 0;
  let activeIndex: number | null = null;
  let floating: HTMLElement | null = null;
  let input: HTMLInputElement | null = null;
  let listbox: HTMLElement | null = null;
  let floatingHandle: TwinHandle | null = null;
  let referenceHandle: TwinHandle | null = null;

  const twin: DotNetTwin = {
    invokeMethod<T>(): T {
      throw new Error('The current dispatcher does not support synchronous calls from JS to .NET.');
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      if (methodName === LIST_HIGHLIGHT_REPORT) {
        activeIndex = (JSON.parse(args[0] as string) as CompositeListHighlightReport).index;
      }
      return Promise.resolve(undefined as T);
    },
  };

  const common = (): CompositeOptions =>
    ({
      orientation: 'both',
      loopFocus: true,
      enableHomeAndEndKeys: false,
      stopEventPropagation: true,
      highlightItemOnHover: false,
      modifierKeys: [],
      direction: 'ltr',
      items: [],
      selectOnNavigation: false,
      reportFocusLeave: false,
      reportHighlight: false,
      sessionToken: popup.token,
      list: {
        orientation: 'horizontal',
        loopFocus: true,
        focusItemOnOpen: false,
        virtual: true,
        allowEscape: true,
        grid: true,
        cols: 3,
        ...(revision > 0 ? { highlight: { index: null, revision } } : {}),
      },
    }) as CompositeOptions;

  const renderOptions = (): void => {
    const filtered = emojis.filter((name) => name.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
    listbox!.replaceChildren(
      ...filtered.map((name) => {
        const element = document.createElement('button');
        element.id = `${popup.token}-${name}`;
        element.type = 'button';
        element.setAttribute('role', 'option');
        element.setAttribute('aria-label', name);
        element.setAttribute('tabindex', '-1');
        element.setAttribute('data-option', '');
        element.disabled = name === 'orange';
        element.textContent = name;
        return element;
      }),
    );
  };

  const floatingOptions = (): string =>
    JSON.stringify({
      ...common(),
      floating: {},
      items: Array.from(listbox!.querySelectorAll<HTMLElement>('[data-option]'), (element) => ({ id: element.id, tabIndex: null })),
    });
  const referenceOptions = (): string => JSON.stringify({ ...common(), reference: {} });

  // The input's onChange: `setActiveIndex(null)` and the new search, one render batch.
  const onChange = (): void => {
    revision += 1;
    search = input!.value;
    renderOptions();
    floatingHandle?.update(floatingOptions());
    referenceHandle?.update(referenceOptions());
  };

  render = () => {
    if (popup.open && !floating) {
      floating = document.createElement('div');
      floating.id = `${popup.token}-floating`;
      floating.setAttribute('role', 'menu');
      input = document.createElement('input');
      input.placeholder = 'Search emoji';
      input.addEventListener('input', onChange);
      listbox = document.createElement('div');
      listbox.setAttribute('role', 'listbox');
      floating.append(input, listbox);
      renderOptions();
      document.body.appendChild(floating);
      popup.attachFloating(floating);
      floatingHandle = attach(floating, floatingOptions(), twin);
      referenceHandle = attach(input, referenceOptions(), twin);
      input.focus();
    } else if (!popup.open && floating) {
      floatingHandle?.dispose();
      referenceHandle?.dispose();
      floatingHandle = null;
      referenceHandle = null;
      floating.remove();
      floating = null;
      search = '';
    }
  };

  const picker: EmojiPicker = {
    button,
    get activeIndex() {
      return activeIndex;
    },
    input() {
      if (!input) {
        throw new Error('the emoji picker is not open');
      }
      return input;
    },
    option(name) {
      const found = listbox?.querySelector<HTMLElement>(`[aria-label="${name}"]`);
      if (!found) {
        throw new Error(`no option ${name}`);
      }
      return found;
    },
    type(text) {
      for (const key of text) {
        keyDown(picker.input(), key);
        picker.input().value += key;
        picker.input().dispatchEvent(new Event('input', { bubbles: true }));
      }
    },
    press(key) {
      keyDown(document.activeElement ?? document.body, key);
    },
    unmount() {
      floatingHandle?.dispose();
      referenceHandle?.dispose();
      popup.dispose();
      floating?.remove();
      button.remove();
    },
  };
  pickers.push(picker);
  return picker;
}

const pickers: EmojiPicker[] = [];

afterEach(() => {
  for (const picker of pickers.splice(0)) {
    picker.unmount();
  }
});
