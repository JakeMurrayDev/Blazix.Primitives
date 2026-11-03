// L3 tests of the `composite` module's list navigation (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1)-(3),
// chosen by the user; CMP §4.2.4, §4.6-§4.8, §4.11, §4.12, §5.4, §5.6): Base UI's useListNavigation.test.tsx titles,
// verbatim, on DOM built after its `App` (a button reference, a `role="menu"` floating element holding a list of three
// items), over the list harness, which plays C#'s part (the commit, the render after it, the attach from
// `OnAfterRender`). `waitFor` stands for Testing Library's: it polls across frames, which the stub below runs on a 0 ms
// timeout (FloatingFocusManager.test.tsx:62-73, the focus-manager tests' precedent). Port titles say "port:".
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LEAVE_POINT, NAVIGATE_POINT } from './composite';
import { resetFocusManagerState } from './focus-manager';
import { resetInteractionsForTest } from './interactions';
import { resetFrameScheduler } from './transitions';
import { createListHarness, type ListHarness, type ListHarnessOptions } from './test-support/list-harness';

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
