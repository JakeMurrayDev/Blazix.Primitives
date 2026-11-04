// L3 tests of the `composite` module's typeahead (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1), chosen by the
// user: typeahead wholly in the module, its reset timer and the typing flag included; CMP §4.2.5, §4.9, §5.5):
// Base UI's useTypeahead.test.tsx titles, verbatim, with fake timers, over the list harness. Base UI's `onMatch` sets the
// consumer's `activeIndex`; the port's module is that consumer, so a match while open is the highlight it writes and
// reports with the keydown, and a match while closed is its `OnListMatch` report. The labels come from C# (`typeahead.
// labels`, the `listRef` of strings Base UI's tests pass) unless a title registers items. There is no `onTyping` report
// (the typing flag stays in the module); its two titles are restated through the flag's one effect, the Space an item
// does not activate while typing (design point 2 (c)).
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { resetFocusManagerState } from './focus-manager';
import { resetInteractionsForTest } from './interactions';
import { resetFrameScheduler } from './transitions';
import { createListHarness, type ListHarness, type ListHarnessOptions } from './test-support/list-harness';

const harnesses: ListHarness[] = [];

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
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

/** Base UI's `Combobox`: an open popup whose typeahead matches `list` (C#'s labels), with no items rendered. */
function combobox(list: string[] = ['one', 'two', 'three'], extra: ListHarnessOptions = {}): ListHarness {
  const harness = createListHarness({
    reference: 'input',
    floatingRole: 'listbox',
    items: [],
    list: { typeahead: { resetMs: 750, labels: list } },
    session: { open: true },
    ...extra,
  });
  harnesses.push(harness);
  return harness;
}

/** The indices each match reported, in order: the highlight while open. */
function matched(harness: ListHarness): Array<number | null> {
  return harness.highlights.map((report) => report.index);
}

/** userEvent's keyboard on the focused element: a keydown per character. */
function type(text: string): void {
  for (const key of text) {
    const target = document.activeElement ?? document.body;
    target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, composed: true }));
  }
}

describe('useTypeahead', () => {
  it('passes the matching keydown event to onMatch', async () => {
    const harness = combobox();

    harness.reference.focus();
    type('t');

    expect(harness.highlights).toHaveLength(1);
    expect(harness.highlights.at(-1)?.index).toBe(1);
    expect(harness.highlights.at(-1)?.event?.type).toBe('keydown');
    expect(harness.highlights.at(-1)?.event?.key).toBe('t');
  });

  it('rapidly focuses list items when they start with the same letter', async () => {
    const harness = combobox();

    harness.reference.focus();

    type('t');
    expect(matched(harness)).toContain(1);

    type('t');
    expect(matched(harness)).toContain(2);

    type('t');
    expect(matched(harness).at(-1)).toBe(1);
  });

  it('bails out of rapid focus of first letter if the list contains a string that starts with two of the same letter', async () => {
    const harness = combobox(['apple', 'aaron', 'apricot']);

    harness.reference.focus();

    type('a');
    expect(matched(harness)).toEqual([0]);

    // "aa" matches "aaron"'s rival "apple" no more; the highlight stays (Base UI calls onMatch(0) again).
    type('a');
    expect(matched(harness).at(-1)).not.toBe(2);
  });

  it.each([false, true])('starts from the current activeIndex and correctly loops (strict: %s)', async () => {
    const harness = combobox(['Toy Story 2', 'Toy Story 3', 'Toy Story 4']);

    harness.reference.focus();

    type('toy');
    expect(matched(harness)).toEqual([0]);

    type('toy');
    expect(matched(harness)).toEqual([0]);

    vi.advanceTimersByTime(750);

    type('toy');
    expect(matched(harness).at(-1)).toBe(1);

    vi.advanceTimersByTime(750);

    type('toy');
    expect(matched(harness).at(-1)).toBe(2);

    vi.advanceTimersByTime(750);

    type('toy');
    expect(matched(harness).at(-1)).toBe(0);
  });

  it('capslock characters continue to match', async () => {
    const harness = combobox();

    harness.reference.focus();

    type('T');
    expect(matched(harness)).toEqual([1]);
  });

  it('does not depend on locale-sensitive lowercasing', async () => {
    const toLocaleLowerCase = String.prototype.toLocaleLowerCase;
    const spy = vi.spyOn(String.prototype, 'toLocaleLowerCase').mockImplementation(function lowerWithTurkishLocale(this: string) {
      return toLocaleLowerCase.call(this, 'tr');
    });
    try {
      const harness = combobox(['Istanbul']);

      harness.reference.focus();

      type('i');
      expect(matched(harness)).toEqual([0]);
    } finally {
      spy.mockRestore();
    }
  });

  it('matches when focus is within reference', async () => {
    const harness = combobox(['one', 'two', 'three'], { reference: 'button' });
    const input = harness.reference.appendChild(document.createElement('input'));
    input.readOnly = true;

    input.focus();

    type('t');
    expect(matched(harness)).toEqual([1]);
  });

  it('matches when focus is within floating', async () => {
    const harness = combobox(['one', 'two', 'three'], {
      items: ['one', 'two', 'three'],
      itemAttributes: () => ({ role: 'option' }),
    });

    harness.reference.focus();

    type('t');
    const option = harness.item(harness.activeIndex!);
    expect(option.textContent).toBe('two');
    option.focus();
    expect(document.activeElement).toBe(option);

    type('h');
    expect(harness.item(harness.activeIndex!).textContent).toBe('three');
  });

  // The typing flag (ADR 04 Amendments 2026-11-03 (1); design point 2 (c)): set by a typed character, cleared by the
  // reset timer; while it is set the floating handle prevents an item's Space before the item's own listener runs.
  it('port: onTyping is called with typing activity (a typed character sets the typing flag, the reset clears it)', async () => {
    const harness = combobox(['one', 'two', 'three'], { items: ['one', 'two', 'three'] });
    const space = () => {
      const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true, composed: true });
      harness.item(1).dispatchEvent(event);
      return event.defaultPrevented;
    };

    harness.reference.focus();
    // No Space before the first character: a leading Space stays in the buffer (CMP-EDGE-10, replicated).
    type('t');
    expect(space()).toBe(true);

    vi.advanceTimersByTime(750);
    expect(space()).toBe(false);
  });

  it('port: onTyping is called when the popup closes without moving focus (the close clears the typing flag)', async () => {
    const harness = combobox(['one', 'two', 'three'], { items: ['one', 'two', 'three'], keepMounted: true });
    const space = () => {
      const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true, composed: true });
      harness.item(1).dispatchEvent(event);
      return event.defaultPrevented;
    };

    harness.reference.focus();
    type('t');
    expect(space()).toBe(true);

    harness.setOpen(false);
    expect(space()).toBe(false);

    vi.advanceTimersByTime(750);
    harness.setOpen(true);
    expect(space()).toBe(false);
  });

  it('skips hidden items when matching with elementsRef', async () => {
    const harness = combobox(['apple', 'apricot', 'banana'], {
      items: ['apple', 'apricot', 'banana'],
      itemAttributes: (index) => ({ role: 'option', ...(index === 0 ? { style: 'display: none' } : {}) }),
      list: { typeahead: { resetMs: 750, skip: 'element' } },
    });

    harness.reference.focus();

    type('a');
    expect(matched(harness)).toEqual([1]);
  });

  it('does not let hidden double-letter items block rapid cycling with elementsRef', async () => {
    const harness = combobox(['aaron', 'apple', 'avocado'], {
      items: ['aaron', 'apple', 'avocado'],
      itemAttributes: (index) => ({ role: 'option', ...(index === 0 ? { style: 'display: none' } : {}) }),
      list: { typeahead: { resetMs: 750, skip: 'element' } },
    });

    harness.reference.focus();

    type('a');
    expect(matched(harness).at(-1)).toBe(1);

    type('a');
    expect(matched(harness).at(-1)).toBe(2);
  });

  it('skips visibility:hidden items when matching with elementsRef', async () => {
    const harness = combobox(['apple', 'apricot', 'banana'], {
      items: ['apple', 'apricot', 'banana'],
      itemAttributes: () => ({ role: 'option' }),
      list: { typeahead: { resetMs: 750, skip: 'element' } },
    });

    harness.item(0).style.visibility = 'hidden';

    harness.reference.focus();

    type('a');
    expect(matched(harness)).toEqual([1]);
  });
});

// CMP-EVT-10 on a closed list (ADR 04 Amendments 2026-11-03 (1)): the match is the family's value request.
describe('typeahead on a closed list (port)', () => {
  it('port: a match while the list is closed is reported for the family\'s value request, and moves no highlight', async () => {
    const harness = combobox(['one', 'two', 'three'], { session: { open: false }, list: { typeahead: { resetMs: 750, labels: ['one', 'two', 'three'] }, selectedIndex: 0 } });

    harness.reference.focus();
    type('t');

    expect(harness.matches.map((match) => [match.index, match.event.key])).toEqual([[1, 't']]);
    expect(harness.highlights).toEqual([]);
  });

  // CMP-KEY-36 on a closed trigger: the typing flag's capture listener is the floating handle's, for the items, whose own
  // keydown listener would otherwise activate them first. A closed trigger's typed Space is consumed by the reference
  // handle's typeahead itself (useTypeahead.ts:132-136 on SelectTrigger's own keydown): a prevented Space keydown cancels
  // a native button's activation whatever the order of the trigger's keydown listeners.
  it('port: on a closed button trigger a Space while the buffer holds text is consumed by the reference handle and opens nothing', async () => {
    const harness = combobox(['one', 'two', 'three'], {
      reference: 'button',
      session: { open: false },
      list: { typeahead: { resetMs: 750, labels: ['one', 'two', 'three'] }, selectedIndex: 0 },
    });
    const reached: string[] = [];
    const listener = (event: KeyboardEvent) => reached.push(event.key);
    document.addEventListener('keydown', listener);
    try {
      harness.reference.focus();
      type('t');
      expect(harness.matches.map((match) => match.index)).toEqual([1]);
      // A closed list's character key is matched and not stopped.
      expect(reached).toEqual(['t']);
      reached.length = 0;

      const space = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true, composed: true });
      harness.reference.dispatchEvent(space);

      expect(space.defaultPrevented).toBe(true);
      expect(reached).toEqual([]);
      expect(harness.openRequests).toEqual([]);
      expect(harness.floating).toBeNull();
    } finally {
      document.removeEventListener('keydown', listener);
    }
  });
});

// Port titles for typeahead rows with `Test: none` (M5 track 1, issue 159), positive first.
describe('typeahead rows without a Base UI title (port)', () => {
  function keyDownOn(target: Element, key: string): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, composed: true });
    target.dispatchEvent(event);
    return event;
  }

  /** The typing flag, read through its one effect: an item's Space prevented before the item's own listener runs. */
  function typing(harness: ListHarness): boolean {
    const item = harness.item(0);
    // The item's own listener stops the Space there, so the floating handle's typeahead never sees it.
    const stop = (event: Event) => event.stopPropagation();
    item.addEventListener('keydown', stop);
    try {
      return keyDownOn(item, ' ').defaultPrevented;
    } finally {
      item.removeEventListener('keydown', stop);
    }
  }

  // CMP-KEY-36, first clause: Space with text in the buffer is consumed and continues the session.
  it('port: Space while the buffer holds text is consumed and keeps the typing flag', async () => {
    const harness = combobox(['one', 'two', 'three'], { items: ['one', 'two', 'three'] });
    harness.reference.focus();

    type('t');
    expect(typing(harness)).toBe(true);

    const space = keyDownOn(harness.reference, ' ');
    expect(space.defaultPrevented).toBe(true);
    expect(typing(harness)).toBe(true);
  });

  // CMP-KEY-36, second clause: a key that is not Space ends the typing flag once the buffer matches no available label.
  it('port: a key that is not Space ends the typing flag once the buffer matches no label, and keeps it while it matches', async () => {
    const harness = combobox(['one', 'two', 'three'], { items: ['one', 'two', 'three'] });
    harness.reference.focus();

    type('t');
    keyDownOn(harness.reference, 'Shift');
    expect(typing(harness)).toBe(true);

    // C#'s labels change under the buffer.
    harness.update({ typeahead: { resetMs: 750, labels: ['one'] } });
    keyDownOn(harness.reference, 'Shift');
    expect(typing(harness)).toBe(false);
  });

  // CMP-EDGE-10, replicated as the source behaves (ADR 12 Amendments 2026-11-03 (5)): a leading Space is not consumed and
  // stays in the buffer, so the next Space is consumed and the next character does not match on its own.
  it('port: a typeahead session that starts with Space is not consumed and keeps the space in its buffer', async () => {
    const harness = combobox(['one', 'two', 'three'], { items: ['one', 'two', 'three'] });
    harness.reference.focus();

    const leading = keyDownOn(harness.reference, ' ');
    expect(leading.defaultPrevented).toBe(false);
    const second = keyDownOn(harness.reference, ' ');
    expect(second.defaultPrevented).toBe(true);

    // The buffer reads " o", which matches nothing, and the failed character clears it.
    type('o');
    expect(matched(harness)).toEqual([]);
    type('o');
    expect(matched(harness)).toEqual([0]);
  });
});
