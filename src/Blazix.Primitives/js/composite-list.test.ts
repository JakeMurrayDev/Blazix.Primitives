// L3 tests of the `composite` module's list navigation (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1)-(3),
// chosen by the user; CMP §4.2.4, §4.6-§4.8, §4.11, §4.12, §5.4, §5.6): Base UI's useListNavigation.test.tsx titles,
// verbatim, on DOM built after its `App` (a button reference, a `role="menu"` floating element holding a list of three
// items), over the list harness, which plays C#'s part (the commit, the render after it, the attach from
// `OnAfterRender`). `waitFor` stands for Testing Library's: it polls across frames, which the stub below runs on a 0 ms
// timeout (FloatingFocusManager.test.tsx:62-73, the focus-manager tests' precedent). Port titles say "port:".
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  attach,
  highlightListEdge,
  highlightListItem,
  LEAVE_POINT,
  LIST_HIGHLIGHT_REPORT,
  moveListHighlightFrom,
  NAVIGATE_POINT,
  relayKeyboardEvent,
  skipListNavigation,
  type CompositeListHighlightReport,
  type CompositeOptions,
} from './composite';
import { resetFocusManagerState } from './focus-manager';
import { getSession, resetInteractionsForTest } from './interactions';
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

  // MNU-FOC-03 (M2's repro on PR 183): a kept-mounted list's handle attaches before the open, and the relay of the open reaches it
  // before the batch that shows the popup (ADR 01's commit path). Base UI's effect runs after the commit, so its list is showing.
  it('port: a kept-mounted list still hidden when the open arrives focuses its first item once it shows', async () => {
    const harness = app({ keepMounted: true });
    const floating = menu(harness);
    floating.style.display = 'none';

    keyDown(harness.reference, 'ArrowDown');
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(document.activeElement).not.toBe(harness.item(0));

    floating.style.display = '';
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
  });

  // SEL-STA-07 (Select's finding; queued by the orchestrator 2026-11-10): useListNavigation.ts's close resets the index and
  // calls `onNavigate()`, which Base UI's Select ignores while closed, so its highlight stays on the item while the popup
  // animates out. A list with `retainHighlightWhileClosing` keeps it; one without clears it, as Menu's does.
  it('port: a list that retains its highlight while closing keeps it through the close, and one that does not clears it', async () => {
    const retained = app({ keepMounted: true, list: { retainHighlightWhileClosing: true } });
    keyDown(retained.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(retained.item(0));
    });
    keyDown(menu(retained), 'ArrowDown');
    expect(highlighted(retained.item(1))).toBe(true);
    retained.setOpen(false);
    await nextTask();
    expect(highlighted(retained.item(1))).toBe(true);

    const cleared = app({ keepMounted: true });
    keyDown(cleared.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(cleared.item(0));
    });
    cleared.setOpen(false);
    await nextTask();
    expect(highlighted(cleared.item(0))).toBe(false);
  });

  // CMP-ALG-24's dated note: the wait ends with the batch that shows the list, read from the attributes of the list and its
  // ancestors (a closed Positioner's `hidden`), however many frames later, and not after a count of frames.
  it('port: a kept-mounted list hidden by an ancestor focuses its first item when it shows, however many frames later, and a close ends that wait', async () => {
    const container = document.createElement('div');
    document.body.append(container);
    const harness = app({ keepMounted: true, container });
    container.hidden = true;
    // A browser's `checkVisibility` reads the ancestors; jsdom has none.
    menu(harness).checkVisibility = () => !container.hidden;
    // Frames as microtasks, so any count of them passes before the list shows.
    let frame = 0;
    vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback) => {
      queueMicrotask(() => callback(performance.now()));
      return ++frame;
    });

    keyDown(harness.reference, 'ArrowDown');
    await new Promise((resolve) => setTimeout(resolve, 300));
    expect(document.activeElement).not.toBe(harness.item(0));

    container.hidden = false;
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    // A close ends the wait (the interim review of PR 183, F3): here the close and the later show change no attribute on the
    // observed chain, so only the close can stop it; after a reopen that finds the list showing, an attribute write must not
    // run the first open's search again.
    const second = app({ keepMounted: true });
    let visible = false;
    menu(second).checkVisibility = () => visible;
    keyDown(second.reference, 'ArrowDown');
    await new Promise((resolve) => setTimeout(resolve, 20));
    second.popup.setOpen(false);
    await new Promise((resolve) => setTimeout(resolve, 20));
    visible = true;
    keyDown(second.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(second.item(0));
    });
    keyDown(menu(second), 'ArrowDown');
    expect(document.activeElement).toBe(second.item(1));
    menu(second).setAttribute('data-probe', '');
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(document.activeElement).toBe(second.item(1));
  });

  // F1 of PR 183 (settled by the orchestrator 2026-11-04 as (b)): the focus manager's handle can attach after the list's
  // open focus moved focus in, which Base UI's layout effect never sees. While open, the list marks the item it moved focus
  // to and the element focus came from, in the session's data slot; the close clears it.
  it('port: the open focus marks the item it moved focus to and the element focus came from, until the close', async () => {
    const harness = app();
    const slot = () => getSession(harness.popup.token)?.dataRef.current['blazix.composite.openFocus'];
    harness.reference.focus();

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    expect(slot()).toEqual({ element: harness.item(0), origin: harness.reference });

    keyDown(harness.item(0), 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });
    expect(slot()).toEqual({ element: harness.item(1), origin: harness.reference });

    harness.setOpen(false);
    expect(slot()).toBeUndefined();
  });

  it('opens on ArrowUp and focuses last item', async () => {
    const harness = app();

    keyDown(harness.reference, 'ArrowUp');
    expect(harness.floating).not.toBeNull();
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
  });

  // useListNavigation.ts:378-387 (found by M5 track 9 on Select, Escape then ArrowDown on Server under latency; queued by
  // the orchestrator 2026-11-10): Base UI clears the opening key when `open` or `focusItemOnOpen` changes, not on each
  // render while closed. The close's unmount can land after the next opening key, before C# commits that open.
  it('port: a close whose unmount lands after the next opening key keeps the key for that open', async () => {
    let hold = false;
    const harness = app({ session: { onOpenChange: (open, details) => (hold && open ? details.cancel() : undefined) } });
    // The request's commit has not arrived yet, so C# has not answered it either.
    harness.popup.answering = false;
    harness.setOpen(true);
    await waitFor(() => {
      expect(harness.floating).not.toBeNull();
    });

    // The close commits; C#'s render that unmounts the list runs after the key's open request.
    harness.popup.setOpen(false);
    hold = true;
    keyDown(harness.reference, 'ArrowUp');
    expect(harness.openRequests.at(-1)?.open).toBe(true);
    expect(harness.floating).toBeNull();

    hold = false;
    harness.popup.setOpen(true, 'list-navigation', harness.openRequests.at(-1)?.triggerId ?? null);
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
  });

  // The same effect's other dependency: a virtual click's focus-on-open value (CMP-STA-39) stays through a sync while the
  // list stays closed, here a close's unmount that lands after the click, before C# commits the click's open.
  it('port: a close whose unmount lands after a virtual click keeps the click\'s focus-on-open value for that open', async () => {
    let hold = false;
    const harness = app({ session: { onOpenChange: (open, details) => (hold && open ? details.cancel() : undefined) } });
    harness.popup.answering = false;
    harness.setOpen(true);
    await waitFor(() => {
      expect(harness.floating).not.toBeNull();
    });

    harness.popup.setOpen(false);
    hold = true;
    fireClick(harness.reference);
    expect(harness.popup.open).toBe(false);
    await waitFor(() => {
      expect(harness.floating).toBeNull();
    });

    hold = false;
    harness.popup.setOpen(true, 'trigger-press', harness.reference.id);
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
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

/**
 * `fireEvent.click`: a bubbling click with `detail: 0` and no pointer type, as Testing Library dispatches it, which
 * `isVirtualClick` reads as a virtual click (so `focusItemOnOpen: 'auto'` highlights the first item, as in Base UI's run).
 */
function click(target: HTMLElement): void {
  target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, composed: true, detail: 0 }));
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
      expect(highlighted(harness.item(0))).toBe(false);
      // The open left no highlight behind: the list's first ArrowDown starts at the first item, where a highlight on
      // item 0 would have moved it to item 1.
      keyDown(menu(harness), 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
      expect(highlighted(harness.item(0))).toBe(true);
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

    // A closed list's arrow key leaves the default behavior and keeps no key for the next open: a click then opens with
    // the first item highlighted, not the last one an ArrowUp open takes.
    it('does not open on ArrowDown when false', async () => {
      const harness = app({ list: { openOnArrowKeyDown: false } });
      const event = keyDown(harness.reference, 'ArrowDown');
      expect(harness.floating).toBeNull();
      expect(event.defaultPrevented).toBe(false);
      click(harness.reference);
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
      expect(highlighted(harness.item(0))).toBe(true);
    });

    it('does not open on ArrowUp when false', async () => {
      const harness = app({ list: { openOnArrowKeyDown: false } });
      const event = keyDown(harness.reference, 'ArrowUp');
      expect(harness.floating).toBeNull();
      expect(event.defaultPrevented).toBe(false);
      click(harness.reference);
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
      expect(highlighted(harness.item(0))).toBe(true);
      expect(highlighted(harness.item(2))).toBe(false);
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
      // The hover left the active index unset: ArrowDown starts at the first item, where a synced index 1 would have
      // moved it to item 2, and the move is the first report.
      keyDown(menu(harness), 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
      expect(harness.highlights.map((report) => report.index)).toEqual([0]);
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

  // Design point 9 (a) of M5 track 6 (settled by the orchestrator 2026-11-04; ADR 04 Amendments 2026-11-04): the report
  // carries the highlighted item's label, its registered label else its text (Menu's onItemHighlighted, MNU-EVT-10).
  it('port: the highlight report carries the highlighted item\'s label, its registered label else its text', async () => {
    const harness = app({ items: ['one', 'two', 'three'], itemRegistration: (index) => (index === 1 ? { label: 'Second' } : {}) });

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    keyDown(harness.item(0), 'ArrowDown');

    expect(harness.highlights.map((report) => [report.index, report.label])).toEqual([
      [0, 'one'],
      [1, 'Second'],
    ]);
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

// CMP-KEY-22's dated note of 2026-11-10 (found by TLB-STR-05; settled by the orchestrator 2026-11-10): Base UI's useButton
// returns from a disabled button's keydown before the trigger's merged handlers run, list navigation's and typeahead's
// among them (internals/use-button/useButton.ts:116-119; MenuTrigger.tsx:261 merges `getButtonProps` last). The gate reads
// useButton's `disabled` parameter, which reaches the DOM as the `disabled` attribute or, on a button focusable when
// disabled, as `aria-disabled="true"`; the port's reference and trigger handles read their element (`isElementDisabled`).
describe('a disabled list trigger (port)', () => {
  async function ignoresKeysUntilEnabled(attribute: 'disabled' | 'aria-disabled', value: string): Promise<void> {
    const harness = app({ synchronous: true, list: { typeahead: { resetMs: 750, labels: ['one', 'two', 'three'] } } });
    harness.reference.setAttribute(attribute, value);

    // The character first, while the list is closed, so an unguarded handle would report it as a match.
    const events = ['t', 'ArrowDown', 'ArrowUp'].map((key) => keyDown(harness.reference, key));

    expect({
      prevented: events.map((event) => event.defaultPrevented),
      decisions: harness.decisions.map((decision) => [decision.point, decision.event.key]),
      matches: harness.matches.map((match) => match.index),
      openRequests: harness.openRequests.map((request) => request.open),
      mounted: harness.floating !== null,
    }).toEqual({ prevented: [false, false, false], decisions: [], matches: [], openRequests: [], mounted: false });

    harness.reference.removeAttribute(attribute);
    keyDown(harness.reference, 'ArrowDown');

    expect(harness.decisions.map((decision) => [decision.point, decision.event.key])).toEqual([[NAVIGATE_POINT, 'ArrowDown']]);
    expect(harness.openRequests).toEqual([expect.objectContaining({ open: true, reason: 'list-navigation', triggerId: harness.reference.id })]);
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
  }

  it('port: a disabled trigger\'s list handle ignores the arrow keys and typeahead, and opens on ArrowDown once enabled', async () => {
    await ignoresKeysUntilEnabled('disabled', '');
  });

  it('port: an aria-disabled trigger\'s list handle, focusable when disabled, ignores the arrow keys and typeahead, and opens on ArrowDown once enabled', async () => {
    await ignoresKeysUntilEnabled('aria-disabled', 'true');
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

      // Seven presses climb the column to its first enabled item (3 is disabled); the eighth loops to the last.
      pressOnMenu(harness, 'ArrowUp');
      expect(option(harness, 38)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowUp', 6);
      expect(option(harness, 8)).toBe(document.activeElement);
      pressOnMenu(harness, 'ArrowUp');

      expect(option(harness, 43)).toBe(document.activeElement);
      expect(highlighted(option(harness, 43))).toBe(true);
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

        // Each move is the list's: the module writes `data-highlighted` on the item it focuses and reports its index.
        pressOnMenu(harness, arrowToStart);
        await waitFor(() => {
          expect(option(harness, 34)).toBe(document.activeElement);
        });
        expect(highlighted(option(harness, 34))).toBe(true);
        expect(harness.activeIndex).toBe(34);
        pressOnMenu(harness, arrowToStart, 6);
        await waitFor(() => {
          expect(option(harness, 28)).toBe(document.activeElement);
        });
        expect(highlighted(option(harness, 28))).toBe(true);
        expect(harness.activeIndex).toBe(28);
        pressOnMenu(harness, arrowToStart, 7);
        await waitFor(() => {
          expect(option(harness, 20)).toBe(document.activeElement);
        });
        expect(highlighted(option(harness, 20))).toBe(true);
        expect(harness.activeIndex).toBe(20);
        pressOnMenu(harness, arrowToStart, 11);
        await waitFor(() => {
          expect(option(harness, 7)).toBe(document.activeElement);
        });
        expect(highlighted(option(harness, 7))).toBe(true);
        expect(harness.activeIndex).toBe(7);
      });

      it('looping works on last row', async () => {
        const harness = complexGridFixture({ rtl, orientation: 'both', loopFocus: true });
        keyDown(harness.reference, 'Enter');
        fireClick(harness.reference);
        await opened(harness);

        option(harness, 36).focus();
        expect(highlighted(option(harness, 36))).toBe(true);

        // The last row holds 35 (disabled) and 36: the loop stays on 36, and the grid navigator consumes the key.
        const event = keyDown(menu(harness), arrowToEnd);
        expect(option(harness, 36)).toBe(document.activeElement);
        expect(event.defaultPrevented).toBe(true);
        expect(highlighted(option(harness, 36))).toBe(true);
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

/** The options a list handle in a role receives, with the module's roving fields at the twin's values. */
function listHandleOptions(token: string, list: CompositeOptions['list'], role: Partial<CompositeOptions>): string {
  return JSON.stringify({
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
    sessionToken: token,
    list,
    ...role,
  } satisfies CompositeOptions);
}

// useListNavigation.test.tsx:1396 (CMP-STA-40), on DOM built after `test/floating-ui-tests/ListboxFocus.tsx`: an open
// popup with no reference handle (the button carries no reference props), a listbox whose floating handle holds four
// options, the consumer's initial `activeIndex` of 1 as C#'s first highlight request, and the button's click as C#'s
// `setSelectedIndex(1)`.
describe('useListNavigation (ListboxFocus)', () => {
  it('selectedIndex changing does not steal focus', async () => {
    const popup = createPopupHarness({ open: true });
    popup.attachSession();
    const reference = document.createElement('button');
    reference.type = 'button';
    reference.setAttribute('data-testid', 'reference');
    reference.textContent = 'Select';
    const floating = document.createElement('div');
    floating.id = `${popup.token}-listbox`;
    floating.setAttribute('role', 'listbox');
    const options = ['Apple', 'Blueberry', 'Watermelon', 'Banana'].map((label, index) => {
      const element = document.createElement('button');
      element.type = 'button';
      element.id = `${popup.token}-option-${index}`;
      element.setAttribute('role', 'option');
      element.setAttribute('tabindex', '-1');
      element.textContent = label;
      return element;
    });
    floating.append(...options);
    document.body.append(reference, floating);
    popup.attachFloating(floating);
    let selectedIndex: number | null = null;
    const floatingOptions = (): string =>
      listHandleOptions(
        popup.token,
        { focusItemOnHover: false, typeahead: { resetMs: 750 }, highlight: { index: 1, revision: 1 }, ...(selectedIndex !== null ? { selectedIndex } : {}) },
        { floating: {}, items: options.map((element) => ({ id: element.id, tabIndex: null, label: element.textContent ?? '' })) },
      );
    const handle = attach(floating, floatingOptions(), createFakeTwin().twin);
    reference.addEventListener('click', () => {
      selectedIndex = 1;
      handle.update(floatingOptions());
    });
    options.forEach((element, index) =>
      element.addEventListener('click', () => {
        selectedIndex = index;
        handle.update(floatingOptions());
      }),
    );

    try {
      // TODO: This feels like a bug. It's the animation frame callback from `enqueueFocus` sometimes
      // kicking in after the click instead before, which causes flakeyness in this test as the wrong
      // element will be focused.
      await waitFor(() => {
        expect(document.activeElement?.getAttribute('role')).toBe('option');
      });

      await userClick(reference);

      await waitFor(() => {
        expect(reference).toBe(document.activeElement);
      });
      // The frames a focus would wait for have passed.
      await nextTask();
      await nextTask();
      expect(reference).toBe(document.activeElement);
    } finally {
      handle.dispose();
      popup.dispose();
    }
  });
});

// useListNavigation.webkit.test.tsx's two titles, verbatim, with `platform.engine.webkit` stubbed: the platform module is
// mocked and the module graph re-imported for the test, as the focus-manager tests stub it. Base UI's `App` again.
describe('useListNavigation (WebKit)', () => {
  async function webkitHarness(): Promise<{ harness: ListHarness; cleanup: () => Promise<void> }> {
    vi.resetModules();
    vi.doMock('./platform', async (importOriginal) => {
      const actual = await importOriginal<typeof import('./platform')>();
      return { ...actual, platform: { ...actual.platform, engine: { ...actual.platform.engine, webkit: true } } };
    });
    const isolated = await import('./test-support/list-harness');
    const interactions = await import('./interactions');
    const focusManager = await import('./focus-manager');
    const transitions = await import('./transitions');
    const harness = isolated.createListHarness();
    return {
      harness,
      cleanup: async () => {
        harness.dispose();
        interactions.resetInteractionsForTest();
        focusManager.resetFocusManagerState();
        transitions.resetFrameScheduler();
        vi.doUnmock('./platform');
        vi.resetModules();
      },
    };
  }

  /** `fireEvent.mouseMove` / `pointerMove`: no movement unless given, as WebKit's stationary events carry. */
  function stationary<T extends MouseEvent>(type: string, Constructor: new (type: string, init: PointerEventInit) => T, target: HTMLElement, init: PointerEventInit = {}): void {
    target.dispatchEvent(new Constructor(type, { bubbles: true, cancelable: true, composed: true, ...init }));
  }

  it('ignores stationary mousemove events fired when the list scrolls beneath the pointer', async () => {
    const { harness, cleanup } = await webkitHarness();
    try {
      keyDown(harness.reference, 'ArrowDown');
      await waitFor(() => {
        expect(harness.item(0)).toBe(document.activeElement);
      });

      // WebKit fires a `mousemove` event with zero movement deltas on the item
      // that moves under the stationary pointer during a keyboard-driven scroll.
      stationary('mousemove', MouseEvent, harness.item(1));
      expect(harness.item(0)).toBe(document.activeElement);
      expect(highlighted(harness.item(1))).toBe(false);

      // An actual pointer movement still moves the highlight.
      stationary('mousemove', MouseEvent, harness.item(1), { movementX: 10, movementY: 10 });
      await waitFor(() => {
        expect(harness.item(1)).toBe(document.activeElement);
      });
      expect(highlighted(harness.item(1))).toBe(true);
    } finally {
      await cleanup();
    }
  });

  it('keeps keyboard modality when a stationary pointermove fires on the floating element', async () => {
    const { harness, cleanup } = await webkitHarness();
    try {
      keyDown(harness.reference, 'ArrowDown');
      await waitFor(() => {
        expect(harness.item(0)).toBe(document.activeElement);
      });

      // The stationary sequence bubbles a `pointermove` through the floating
      // element before the item receives the `mousemove`.
      stationary('pointermove', PointerEvent, harness.floating!, { pointerType: 'mouse' });
      stationary('mousemove', MouseEvent, harness.item(1));
      expect(harness.item(0)).toBe(document.activeElement);

      // Scrolling can also move the active item out from under the pointer. This
      // must not reset the keyboard highlight as though the pointer had left it.
      harness.item(0).dispatchEvent(
        new PointerEvent('pointerleave', { bubbles: false, cancelable: false, composed: true, pointerType: 'mouse', relatedTarget: harness.floating }),
      );
      expect(highlighted(harness.item(0))).toBe(true);
    } finally {
      await cleanup();
    }
  });
});

// Port titles for CMP rows with `Test: none` (M5 track 1, issue 159; the M2 "none (port L3)" precedent), positive first.
describe('list navigation rows without a Base UI title (port)', () => {
  // `:45`/`:63` (settled by the orchestrator 2026-11-11, option (a)): on Server the floating list attaches a round trip after
  // the open, so a navigation key pressed meanwhile is kept, the newest only, and replayed once the list attaches.
  // One title, so the red check runs the guards against the base too: without the replay its first half fails. The bound
  // (settled 2026-11-11): the newest key only, dropped by a close or by any newer key (PR 193 delta review F1).
  it('port: a navigation key pressed on the open reference before the list attaches is replayed once it attaches, the newest only; a key the list does not navigate with is not kept, and a close or a newer key drops the kept key', async () => {
    const harness = app({ reference: 'input', floatingRole: 'listbox', items: ['a', 'b', 'c'], list: { virtual: true, focusItemOnOpen: false } });
    harness.reference.focus();
    // C# commits the open; its render, which mounts the list, comes later.
    harness.popup.setOpen(true, 'none');
    expect(harness.floating).toBeNull();

    // EDGE-09's toolbar arrow: ArrowLeft on a vertical list is not the list's, so it is neither stopped nor kept.
    const left = keyDown(harness.reference, 'ArrowLeft');
    const kept = [keyDown(harness.reference, 'ArrowDown'), keyDown(harness.reference, 'ArrowDown')];
    harness.setOpen(true);

    await waitFor(() => {
      expect(harness.activeIndex).toBe(0);
    });
    expect([kept.map((event) => event.defaultPrevented), left.defaultPrevented, harness.highlights.length]).toEqual([[true, true], false, 1]);

    const closed = app({ reference: 'input', floatingRole: 'listbox', items: ['a', 'b', 'c'], list: { virtual: true, focusItemOnOpen: false } });
    closed.reference.focus();
    closed.popup.setOpen(true, 'none');
    keyDown(closed.reference, 'ArrowDown');
    closed.popup.setOpen(false, 'none');
    closed.popup.setOpen(true, 'none');
    closed.setOpen(true);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(closed.highlights).toEqual([]);

    // A typed character after the arrow: Base UI's highlight would be cleared by the typing, so the arrow is not replayed.
    const typed = app({ reference: 'input', floatingRole: 'listbox', items: ['a', 'b', 'c'], list: { virtual: true, focusItemOnOpen: false } });
    typed.reference.focus();
    typed.popup.setOpen(true, 'none');
    keyDown(typed.reference, 'ArrowDown');
    keyDown(typed.reference, 'e');
    typed.setOpen(true);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(typed.highlights).toEqual([]);
  });

  it('port: in grid mode a typeable combobox input with no highlight keeps ArrowLeft and ArrowRight for its caret (CMB-KEY-13)', () => {
    const harness = app({
      reference: 'input',
      floatingRole: 'grid',
      items: ['a', 'b', 'c', 'd'],
      rowSize: 2,
      itemAttributes: () => ({ role: 'gridcell' }),
      session: { open: true },
      list: { virtual: true, orientation: 'horizontal', grid: true },
    });
    harness.reference.focus();

    const released = [keyDown(harness.reference, 'ArrowLeft'), keyDown(harness.reference, 'ArrowRight')];

    expect([released.map((event) => event.defaultPrevented), harness.highlights.length]).toEqual([[false, false], 0]);
  });

  // CMP-STR-06: the reference, trigger and floating roles act through their own handles, the item role through the
  // floating handle's delegated listeners; with `enabled: false` no role acts.
  it('port: the reference, trigger, floating and item roles act through their handles, and with enabled false none acts', async () => {
    const harness = app({ list: { virtual: false } });
    const trigger = document.createElement('button');
    trigger.id = `${harness.popup.token}-second-trigger`;
    document.body.appendChild(trigger);
    const fake = createFakeTwin();
    const triggerHandle = attach(trigger, listHandleOptions(harness.popup.token, {}, { trigger: {} }), fake.twin);
    try {
      // The trigger role: a second trigger requests the open for itself.
      keyDown(trigger, 'ArrowDown');
      expect(fake.reported('OnListOpenRequest')).toEqual([expect.objectContaining({ open: true, reason: 'list-navigation', triggerId: trigger.id })]);

      // The reference role opens; the floating role moves the highlight; the item role follows the pointer.
      keyDown(harness.reference, 'ArrowDown');
      await waitFor(() => {
        expect(document.activeElement).toBe(harness.item(0));
      });
      keyDown(menu(harness), 'ArrowDown');
      expect(document.activeElement).toBe(harness.item(1));
      mouseMove(harness.item(2));
      expect(document.activeElement).toBe(harness.item(2));
      expect(highlighted(harness.item(2))).toBe(true);

      harness.update({ enabled: false });
      triggerHandle.update(listHandleOptions(harness.popup.token, { enabled: false }, { trigger: {} }));

      expect([0, 1, 2].some((index) => highlighted(harness.item(index)))).toBe(false);
      const moved = keyDown(menu(harness), 'ArrowUp');
      mouseMove(harness.item(0));
      expect(moved.defaultPrevented).toBe(false);
      expect(document.activeElement).toBe(harness.item(2));
      expect(highlighted(harness.item(0))).toBe(false);
      keyDown(trigger, 'ArrowDown');
      expect(fake.reported('OnListOpenRequest')).toHaveLength(1);
      harness.setOpen(false);
      const requests = harness.openRequests.length;
      const opening = keyDown(harness.reference, 'ArrowDown');
      expect(opening.defaultPrevented).toBe(false);
      expect(harness.openRequests).toHaveLength(requests);
      expect(harness.floating).toBeNull();
    } finally {
      triggerHandle.dispose();
    }
  });

  // CMP-STA-32 (ADR 04 Amendments 2026-11-03 (2)): C#'s highlight requests by revision; the cursor follows a request.
  it('port: a C# highlight request moves the highlight and the cursor, so the next key steps from it', async () => {
    const harness = app();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    harness.update({ highlight: { index: 2, revision: 1 } });
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
    expect(highlighted(harness.item(2))).toBe(true);
    expect(harness.highlights.at(-1)).toMatchObject({ index: 2, revision: 1, event: null });

    keyDown(menu(harness), 'ArrowUp');
    expect(document.activeElement).toBe(harness.item(1));
  });

  it('port: a C# highlight request whose revision was already applied is ignored, and a newer one applies', async () => {
    const harness = app();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    harness.update({ highlight: { index: 2, revision: 1 } });
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(2));
    });
    keyDown(menu(harness), 'ArrowUp');
    expect(highlighted(harness.item(1))).toBe(true);

    // A later render resends the latest request with an unrelated change: the stale revision moves nothing.
    harness.update({ loopFocus: true });
    expect(highlighted(harness.item(1))).toBe(true);
    expect(document.activeElement).toBe(harness.item(1));

    harness.update({ highlight: { index: 0, revision: 2 } });
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    expect(harness.highlights.at(-1)).toMatchObject({ index: 0, revision: 2 });
  });

  // CMP-STA-32 with CMP-EDGE-15: a requested highlight out of bounds moves neither the focus nor the cursor; once the
  // items reach it, the next key resynchronizes the cursor to it before it steps.
  it('port: the next key resynchronizes the cursor to a C# highlight it could not follow while the highlight was out of bounds', async () => {
    const harness = app();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    harness.update({ highlight: { index: 4, revision: 1 } });
    await nextTask();
    expect(document.activeElement).toBe(harness.item(0));

    harness.setItems(['one', 'two', 'three', 'four', 'five']);
    expect(highlighted(harness.item(4))).toBe(true);

    keyDown(menu(harness), 'ArrowUp');
    expect(document.activeElement).toBe(harness.item(3));
  });

  // MenuRoot.tsx:574-592 (MNU-EVT-11, EVT-13): an index can come to point at a different element while its value stays the
  // same, so the highlight is reported again when the registry settles, with no event (Base UI's `none`).
  it('port: a list change that points the highlighted index at another item reports the highlight again with no event', async () => {
    const harness = app({ items: ['One', 'Two'] });
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    keyDown(menu(harness), 'ArrowDown');
    expect(document.activeElement).toBe(harness.item(1));
    const before = harness.highlights.length;

    harness.setItems(['Archive', 'One', 'Two']);
    await nextTask();

    expect(harness.highlights.length).toBeGreaterThan(before);
    expect(harness.highlights.at(-1)).toMatchObject({ event: null });
  });

  it('port: a C# highlight request out of bounds moves neither the focus nor the cursor, while one in bounds does', async () => {
    const harness = app();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    harness.update({ highlight: { index: 1, revision: 1 } });
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(1));
    });

    harness.update({ highlight: { index: 7, revision: 2 } });
    await nextTask();
    expect(document.activeElement).toBe(harness.item(1));

    // The cursor stayed on 1: ArrowDown steps to 2, not from 7.
    keyDown(menu(harness), 'ArrowDown');
    expect(document.activeElement).toBe(harness.item(2));
  });

  // CMP-KEY-25: Chrome's IME composition repeats ArrowDown with the legacy key code 229.
  it('port: the floating handling ignores a keydown whose legacy key code is 229, and handles the same key without it', async () => {
    const harness = app();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });

    const composing = keyDown(harness.item(0), 'ArrowDown', { which: 229, keyCode: 229 } as KeyboardEventInit);
    expect(composing.defaultPrevented).toBe(false);
    expect(document.activeElement).toBe(harness.item(0));

    const plain = keyDown(harness.item(0), 'ArrowDown');
    expect(plain.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(harness.item(1));
  });

  // CMP-STA-39 through CMP-ALG-28 (`isVirtualClick`, imported from `interactions`): a click with `detail: 0` (a screen
  // reader's or the keyboard's) opens with the first item highlighted under `focusItemOnOpen: 'auto'`.
  it('port: a virtual click on the trigger opens with the first item highlighted, and a pointer click opens with none', async () => {
    const virtual = app();
    fireClick(virtual.reference);
    await waitFor(() => {
      expect(document.activeElement).toBe(virtual.item(0));
    });
    expect(highlighted(virtual.item(0))).toBe(true);
    virtual.dispose();
    harnesses.splice(harnesses.indexOf(virtual), 1);

    const pointer = app();
    // A pointer's click: `detail: 1`, which is not virtual.
    pointer.reference.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, composed: true, detail: 1 }));
    await opened(pointer);
    await nextTask();
    expect([0, 1, 2].some((index) => highlighted(pointer.item(index)))).toBe(false);
    expect(pointer.highlights).toEqual([]);
  });

  // The focus manager's `data-tabindex` stash (CMP-DOM-01's M4 note, FLT-DOM-18): while an item carries it, the
  // highlight's tabindex goes into the stash, which the focus manager restores.
  it('port: the highlight\'s tabindex goes into an item\'s data-tabindex stash while it carries one, and into tabindex once it is gone', async () => {
    const harness = app({ itemAttributes: () => ({ 'data-tabindex': '-1' }) });
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(0));
    });
    expect(harness.item(0).getAttribute('data-tabindex')).toBe('0');
    expect(harness.item(0).getAttribute('tabindex')).toBe('-1');

    harness.item(1).removeAttribute('data-tabindex');
    keyDown(harness.item(0), 'ArrowDown');
    expect(harness.item(1).getAttribute('tabindex')).toBe('0');
    expect(harness.item(1).hasAttribute('data-tabindex')).toBe(false);
    expect(harness.item(0).getAttribute('data-tabindex')).toBe('-1');
  });

  // CMP-EDGE-17: the popup grid's origin is the first enabled item when the cursor lies past the last enabled one.
  it('port: the grid navigator moves from the first enabled item when the cursor lies past the last enabled one', async () => {
    const harness = app({
      items: ['0', '1', '2', '3', '4', '5'],
      itemTag: 'button',
      flat: true,
      itemAttributes: (index) => ({ type: 'button', role: 'option', ...(index === 5 ? { 'aria-disabled': 'true' } : {}) }),
      list: { orientation: 'horizontal', grid: true, cols: 3 },
    });
    click(harness.reference);
    await opened(harness);
    harness.update({ highlight: { index: 5, revision: 1 } });
    await waitFor(() => {
      expect(document.activeElement).toBe(harness.item(5));
    });

    keyDown(menu(harness), 'ArrowDown');
    expect(document.activeElement).toBe(harness.item(3));
  });

  // CMP-EDGE-03..05: Base UI's development warnings, with the `Blazix: ` prefix, where the options are set.
  it('port: warns where the options are set when allowEscape lacks loopFocus or virtual, and for a vertical grid', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});

    app({ list: { allowEscape: true } });
    expect(warn).toHaveBeenCalledWith('Blazix: `useListNavigation` looping must be enabled to allow escaping.');
    expect(warn).toHaveBeenCalledWith('Blazix: `useListNavigation` must be virtual to allow escaping.');

    warn.mockClear();
    app({ list: { grid: true } });
    expect(warn).toHaveBeenCalledWith('Blazix: In grid list navigation mode, the `orientation` should', 'be either "horizontal" or "both".');

    warn.mockClear();
    const quiet = app({ list: { allowEscape: true, loopFocus: true, virtual: true, grid: true, orientation: 'horizontal' } });
    expect(warn).not.toHaveBeenCalled();

    // An update that sets the options warns as a render with them would.
    quiet.update({ loopFocus: false });
    expect(warn).toHaveBeenCalledWith('Blazix: `useListNavigation` looping must be enabled to allow escaping.');
    warn.mockClear();
    quiet.update({ loopFocus: false, orientation: 'vertical' });
    expect(warn).toHaveBeenCalledWith('Blazix: In grid list navigation mode, the `orientation` should', 'be either "horizontal" or "both".');
  });
});

// CMP-EDGE-12's list-navigation and grid clauses: the list inside an open shadow root. The handles resolve the items by
// id from the floating element's root node, the shadow root; `activeElement` and `contains` read the composed tree (the
// document's active element is the shadow host), so the "focus inside the popup" check holds there; and the grid's
// `[role="row"]` lookup runs on rows inside the shadow tree.
describe('list navigation inside a shadow root (port)', () => {
  function shadowContainer(): { shadow: ShadowRoot; container: HTMLElement } {
    const host = document.body.appendChild(document.createElement('div'));
    const shadow = host.attachShadow({ mode: 'open' });
    const container = shadow.appendChild(document.createElement('div'));
    return { shadow, container };
  }

  it('port: inside a shadow root the list resolves its items by id, opens on ArrowDown and moves the focus and the highlight', async () => {
    const { shadow, container } = shadowContainer();
    const harness = app({ container });
    expect(harness.reference.getRootNode()).toBe(shadow);

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(shadow.activeElement).toBe(harness.item(0));
    });
    expect(highlighted(harness.item(0))).toBe(true);
    expect(harness.item(0).getAttribute('tabindex')).toBe('0');

    keyDown(menu(harness), 'ArrowDown');
    await waitFor(() => {
      expect(shadow.activeElement).toBe(harness.item(1));
    });
    expect(highlighted(harness.item(1))).toBe(true);
    expect(highlighted(harness.item(0))).toBe(false);
    expect(harness.highlights.map((report) => [report.index, report.itemId])).toEqual([
      [0, harness.item(0).id],
      [1, harness.item(1).id],
    ]);
  });

  it('port: inside a shadow root the main key with the focus on the popup itself starts at the boundary item', async () => {
    const { shadow, container } = shadowContainer();
    const harness = app({ container });

    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(shadow.activeElement).toBe(harness.item(0));
    });
    keyDown(menu(harness), 'ArrowDown');
    await waitFor(() => {
      expect(shadow.activeElement).toBe(harness.item(1));
    });

    // The focus rests on the popup, not on an item: the composed check sees it inside the popup, so ArrowDown starts at
    // the first item rather than moving on from the highlighted second one.
    menu(harness).focus();
    expect(document.activeElement).toBe(shadow.host);
    expect(shadow.activeElement).toBe(menu(harness));
    keyDown(menu(harness), 'ArrowDown');
    await waitFor(() => {
      expect(shadow.activeElement).toBe(harness.item(0));
    });
    expect(highlighted(harness.item(0))).toBe(true);
    expect(harness.activeIndex).toBe(0);
  });

  it('port: inside a shadow root the grid moves vertically along its [role="row"] rows', async () => {
    const { shadow, container } = shadowContainer();
    // Nine gridcells in rows of three, while the list's `cols` says five: vertical moves follow the DOM rows.
    const harness = app({
      container,
      items: Array.from({ length: 9 }, (_, index) => `Cell ${index}`),
      itemTag: 'button',
      rowSize: 3,
      floatingRole: 'grid',
      itemAttributes: () => ({ type: 'button', role: 'gridcell' }),
      list: { grid: true, cols: 5, orientation: 'horizontal', loopFocus: false, openOnArrowKeyDown: false },
    });

    fireClick(harness.reference);
    await waitFor(() => {
      expect(shadow.activeElement).toBe(option(harness, 0));
    });

    pressOnMenu(harness, 'ArrowDown');
    expect(shadow.activeElement).toBe(option(harness, 3));
    expect(highlighted(option(harness, 3))).toBe(true);
    pressOnMenu(harness, 'ArrowDown');
    expect(shadow.activeElement).toBe(option(harness, 6));
    pressOnMenu(harness, 'ArrowRight');
    expect(shadow.activeElement).toBe(option(harness, 7));
    pressOnMenu(harness, 'ArrowUp');
    expect(shadow.activeElement).toBe(option(harness, 4));
    expect(harness.activeIndex).toBe(4);
  });

  it('port: the grid finds an item\'s [role="row"] through its assigned slot, inside the row host\'s shadow root', async () => {
    // Each row host keeps its three gridcells as children; its shadow root holds the `role="row"` element around a slot,
    // so a cell's row is an ancestor only in the composed tree.
    const harness = app({
      items: Array.from({ length: 9 }, (_, index) => `Cell ${index}`),
      itemTag: 'button',
      rowSize: 3,
      slottedRows: true,
      floatingRole: 'grid',
      itemAttributes: () => ({ type: 'button', role: 'gridcell' }),
      list: { grid: true, cols: 5, orientation: 'horizontal', loopFocus: false, openOnArrowKeyDown: false },
    });

    fireClick(harness.reference);
    await waitFor(() => {
      expect(document.activeElement).toBe(option(harness, 0));
    });
    expect(option(harness, 0).closest('[role="row"]')).toBeNull();
    expect(option(harness, 0).assignedSlot?.parentElement?.getAttribute('role')).toBe('row');

    pressOnMenu(harness, 'ArrowDown');
    expect(document.activeElement).toBe(option(harness, 3));
    pressOnMenu(harness, 'ArrowDown');
    expect(document.activeElement).toBe(option(harness, 6));
    pressOnMenu(harness, 'ArrowUp');
    expect(document.activeElement).toBe(option(harness, 3));
    expect(highlighted(option(harness, 3))).toBe(true);
    expect(harness.activeIndex).toBe(3);
  });
});

// skipListNavigation (M5 track 12, issue 197; granted by the orchestrator 2026-11-11): a module whose element is a list's
// reference and owns some keys itself (the menu-filter Input's `edit` keys, MNU-KEY-22) marks a keydown so the list's
// handles leave it alone: no navigation, no typeahead, no preventDefault.
describe('skipListNavigation (port)', () => {
  function descendantOf(element: Element | null): string | null {
    return element?.getAttribute('aria-activedescendant') ?? null;
  }

  it('port: a keydown marked by skipListNavigation is neither navigated nor prevented', async () => {
    const harness = app({ reference: 'input', list: { virtual: true } });
    harness.reference.focus();
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(0).id);
    });
    const mark = (event: Event): void => skipListNavigation(event as KeyboardEvent);
    harness.reference.addEventListener('keydown', mark, { capture: true });

    const marked = keyDown(harness.reference, 'ArrowDown');

    expect(marked.defaultPrevented).toBe(false);
    expect(descendantOf(harness.reference)).toBe(harness.item(0).id);

    harness.reference.removeEventListener('keydown', mark, { capture: true });
    const unmarked = keyDown(harness.reference, 'ArrowDown');
    expect(unmarked.defaultPrevented).toBe(true);
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(1).id);
    });
  });

  it('port: a marked keydown on the closed reference does not open the list', () => {
    const harness = app({ reference: 'input', list: { virtual: true } });
    harness.reference.focus();
    harness.reference.addEventListener('keydown', (event) => skipListNavigation(event as KeyboardEvent), { capture: true });

    const event = keyDown(harness.reference, 'ArrowDown');

    expect(event.defaultPrevented).toBe(false);
    expect(harness.floating).toBe(null);
  });
});

// highlightListEdge (M5 track 12, issue 197; granted by the orchestrator 2026-11-12): the menu-filter Input's `enter-list`
// (useMenuFilterKeyDown.ts:155-167, MNU-KEY-24) highlights the list's first item on ArrowDown and its last on ArrowUp,
// with no disabled index, from a keydown the list's own handles skip.
describe('highlightListEdge (port)', () => {
  function descendantOf(element: Element | null): string | null {
    return element?.getAttribute('aria-activedescendant') ?? null;
  }

  it('port: highlightListEdge highlights the first or the last item of an open list from its reference', async () => {
    const harness = app({ reference: 'input', list: { virtual: true }, session: { open: true } });
    harness.reference.focus();

    expect(highlightListEdge(harness.reference, 'last', new KeyboardEvent('keydown', { key: 'ArrowUp' }))).toBe(true);
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(2).id);
    });

    expect(highlightListEdge(harness.reference, 'first', new KeyboardEvent('keydown', { key: 'ArrowDown' }))).toBe(true);
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(0).id);
    });
  });

  // FilterDropdownRoot.tsx:109-125 (MNU-STA-20): the filter's item-set change clears the highlight it no longer holds.
  it('port: highlightListEdge with no edge clears the highlight', async () => {
    const harness = app({ reference: 'input', list: { virtual: true }, session: { open: true } });
    harness.reference.focus();
    highlightListEdge(harness.reference, 'first', new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(0).id);
    });

    expect(highlightListEdge(harness.reference, null, undefined)).toBe(true);

    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(null);
    });
  });

  // MNU-STA-20 (FilterDropdownRoot.tsx:109-125; the orchestrator's 2026-11-12 grant): a list with itemsChangeHighlight
  // does not let another item take the highlighted index when its items change; 'clear' clears, before any report.
  it('port: itemsChangeHighlight clears a highlight another item would take, without reporting that item', async () => {
    const harness = app({ reference: 'input', list: { virtual: true, itemsChangeHighlight: 'clear' }, session: { open: true } });
    harness.reference.focus();
    highlightListEdge(harness.reference, 'first', new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    keyDown(harness.reference, 'ArrowDown');
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(1).id);
    });
    const before = harness.highlights.length;

    harness.setItems(['zero', 'one', 'two', 'three']);

    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(null);
    });
    expect(harness.highlights.slice(before).map((report) => report.index ?? null)).toEqual([null]);
  });

  // MenuFilterSubmenuRoot.tsx:47-49 (the orchestrator's 2026-11-12 grant): leaving a filterable submenu highlights its
  // trigger in the parent list.
  it('port: highlightListItem highlights the given item of the list from its reference', async () => {
    const harness = app({ reference: 'input', list: { virtual: true }, session: { open: true } });
    harness.reference.focus();

    expect(highlightListItem(harness.reference, harness.item(2), new KeyboardEvent('keydown', { key: 'ArrowLeft' }))).toBe(true);
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(2).id);
    });

    expect(highlightListItem(harness.reference, document.createElement('li'), undefined)).toBe(false);
  });

  // moveHighlightFrom.ts (MenuFilterSubmenuRoot.tsx:157-167): a main-axis key moves the parent's highlight on from the
  // submenu's trigger with the list's own navigation, its disabled skipping and its loop included.
  it('port: moveListHighlightFrom moves on from the given item with the list navigation, skipping disabled items and looping', async () => {
    const harness = app({
      reference: 'input',
      list: { virtual: true, loopFocus: true },
      session: { open: true },
      itemAttributes: (index) => (index === 1 ? { 'aria-disabled': 'true' } : {}),
    });
    harness.reference.focus();

    expect(moveListHighlightFrom(harness.reference, harness.item(0), new KeyboardEvent('keydown', { key: 'ArrowDown', cancelable: true }))).toBe(true);
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(2).id);
    });

    moveListHighlightFrom(harness.reference, harness.item(2), new KeyboardEvent('keydown', { key: 'ArrowDown', cancelable: true }));
    await waitFor(() => {
      expect(descendantOf(harness.reference)).toBe(harness.item(0).id);
    });
  });

  it('port: highlightListEdge does nothing on an element without a list handle', () => {
    const element = document.createElement('input');

    expect(highlightListEdge(element, 'first', new KeyboardEvent('keydown', { key: 'ArrowDown' }))).toBe(false);
  });
});
