// L3 tests of the `menu-filter` module (M5 track 12, issue 197). The key table first: Base UI's
// menu/filter-root/useMenuFilterKeyDown.test.ts, both `it.each` tables with every row, against the module's pure
// getMenuFilterKeyAction (MNU-KEY-22, KEY-30 re-tagged to L3 with "(L4 for …)" for the rest).

import { afterEach, describe, expect, it } from 'vitest';
import { attach, getMenuFilterKeyAction, type MenuFilterKey, type MenuFilterKeyAction, type MenuFilterOptions } from './menu-filter';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';
import type { TwinHandle } from './twin';

interface ValueChange {
  value: string;
  reason: string;
}

function key(value: string, init: Partial<MenuFilterKey> = {}): MenuFilterKey {
  return {
    key: value,
    which: 0,
    shiftKey: false,
    ctrlKey: false,
    altKey: false,
    metaKey: false,
    ...init,
  };
}

describe('getMenuFilterKeyAction', () => {
  it.each<[string, MenuFilterKey, boolean, boolean, MenuFilterKeyAction, boolean?]>([
    ['IME composition', key('Enter', { which: 229 }), true, true, 'ignore'],
    ['Tab', key('Tab'), true, false, 'ignore'],
    ['Shift+Tab', key('Tab', { shiftKey: true }), false, false, 'close'],
    ['Enter on a highlight', key('Enter'), true, false, 'activate'],
    ['Enter without a highlight', key('Enter'), false, true, 'navigate'],
    ['a character', key('a'), true, false, 'edit'],
    ['Space', key(' '), true, false, 'edit'],
    ['Shift+ArrowDown', key('ArrowDown', { shiftKey: true }), true, true, 'edit'],
    ['Ctrl+ArrowLeft', key('ArrowLeft', { ctrlKey: true }), true, true, 'edit'],
    ['Shift+Escape', key('Escape', { shiftKey: true }), true, true, 'navigate'],
    ['ArrowDown', key('ArrowDown'), false, true, 'navigate'],
    ['Home without a highlight', key('Home'), false, false, 'edit'],
    ['Home on a highlight in an empty input', key('Home'), true, false, 'navigate'],
    ['Home on a highlight in a non-empty input', key('Home'), true, true, 'edit'],
    ['ArrowRight on a submenu trigger', key('ArrowRight'), true, true, 'submenu', true],
    ['ArrowLeft on a submenu trigger', key('ArrowLeft'), true, false, 'submenu', true],
    ['ArrowRight on an item in a non-empty input', key('ArrowRight'), true, true, 'edit'],
    ['ArrowLeft on an item in an empty input', key('ArrowLeft'), true, false, 'navigate'],
    ['ArrowLeft in a non-empty input', key('ArrowLeft'), false, true, 'edit'],
    ['ArrowLeft in an empty input', key('ArrowLeft'), false, false, 'navigate'],
    ['Escape', key('Escape'), true, true, 'navigate'],
  ])('vertical: %s', (_, event, hasActiveItem, hasValue, expected, activeItemOpensSubmenu = false) => {
    expect(
      getMenuFilterKeyAction(event, {
        orientation: 'vertical',
        rtl: false,
        hasActiveItem,
        activeItemOpensSubmenu,
        hasValue,
      }),
    ).toBe(expected);
  });

  it.each<[string, MenuFilterKey, boolean, boolean, MenuFilterKeyAction, boolean?]>([
    ['ArrowDown without a highlight', key('ArrowDown'), false, true, 'enter-list'],
    ['ArrowUp without a highlight', key('ArrowUp'), false, false, 'enter-list'],
    ['ArrowDown on a submenu trigger', key('ArrowDown'), true, true, 'submenu', true],
    ['ArrowDown on an item in a non-empty input', key('ArrowDown'), true, true, 'edit'],
    ['ArrowRight in a non-empty input', key('ArrowRight'), false, true, 'edit'],
    ['ArrowRight in an empty input', key('ArrowRight'), false, false, 'navigate'],
    ['ArrowRight on a highlight', key('ArrowRight'), true, true, 'navigate'],
  ])('horizontal: %s', (_, event, hasActiveItem, hasValue, expected, activeItemOpensSubmenu = false) => {
    expect(
      getMenuFilterKeyAction(event, {
        orientation: 'horizontal',
        rtl: false,
        hasActiveItem,
        activeItemOpensSubmenu,
        hasValue,
      }),
    ).toBe(expected);
  });
});

// The Input handle (MNU-EVT-16, EVT-19, KEY-31, STA-22, DOM-21's value): an `input` element C# rendered, attached as the
// MenuInput's twin would, with the options C# sends.
describe('the Input handle', () => {
  const handles: TwinHandle[] = [];

  afterEach(() => {
    handles.splice(0).forEach((handle) => handle.dispose());
    document.body.replaceChildren();
  });

  function renderInput(options: Partial<MenuFilterOptions> = {}, fake: FakeTwin = createFakeTwin()) {
    const input = document.createElement('input');
    input.value = options.value ?? '';
    document.body.append(input);
    const json = (next: Partial<MenuFilterOptions>) => JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true, valueRevision: 0, ...next });
    const handle = attach(input, json(options), fake.twin);
    handles.push(handle);
    return { input, fake, handle, update: (next: Partial<MenuFilterOptions>) => handle.update(json({ ...options, ...next })) };
  }

  function type(input: HTMLInputElement, value: string, init: InputEventInit = {}) {
    input.value = value;
    input.dispatchEvent(new InputEvent('input', { bubbles: true, ...init }));
  }

  it('port: reports a typed edit as input-change and an emptied input as input-clear', () => {
    const { input, fake } = renderInput();

    type(input, 'ap');
    type(input, '');

    expect(fake.reported<ValueChange>('OnValueChange').map(({ value, reason }) => [value, reason])).toEqual([
      ['ap', 'input-change'],
      ['', 'input-clear'],
    ]);
  });

  it('port: drops an edit made while the menu is closed', () => {
    const { input, fake, update } = renderInput();
    update({ open: false });

    type(input, 'ap');

    expect(fake.reported('OnValueChange')).toEqual([]);
    type(input, 'a');
    update({ open: true });
    type(input, 'ap');
    expect(fake.reported<ValueChange>('OnValueChange').map(({ value }) => value)).toEqual(['ap']);
  });

  it('port: reports nothing during an IME composition and the committed text at compositionend', () => {
    const { input, fake } = renderInput();

    input.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
    type(input, 'に', { isComposing: true });
    type(input, '日本', { isComposing: true });
    expect(fake.reported('OnValueChange')).toEqual([]);
    input.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true, data: '日本' }));

    expect(fake.reported<ValueChange>('OnValueChange').map(({ value, reason }) => [value, reason])).toEqual([['日本', 'input-change']]);
  });

  it('port: writes a committed value when its revision moves and the text differs, and re-asserts one the consumer did not apply', () => {
    const { input, update } = renderInput({ value: 'a', valueRevision: 1 });
    type(input, 'ab');

    update({ value: 'ab', valueRevision: 2 });
    expect(input.value).toBe('ab');

    type(input, 'abc');
    update({ value: 'ab', valueRevision: 2 });
    expect(input.value).toBe('abc');

    update({ value: 'ab', valueRevision: 3 });
    expect(input.value).toBe('ab');

    update({ value: '', valueRevision: 4 });
    expect(input.value).toBe('');
  });

  it('port: shows the focus ring while focused, until keyboard navigation highlights an item', async () => {
    const { input, fake } = renderInput();

    input.focus();
    expect(input.hasAttribute('data-highlighted')).toBe(true);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    input.setAttribute('aria-activedescendant', 'item-1');
    await Promise.resolve();
    expect(input.hasAttribute('data-highlighted')).toBe(false);

    input.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    expect(input.hasAttribute('data-highlighted')).toBe(true);
    input.blur();
    expect(input.hasAttribute('data-highlighted')).toBe(false);
    expect(fake.reported<{ highlighted: boolean }>('OnHighlightedChange').map((r) => r.highlighted)).toEqual([true, false, true, false]);
  });

  it('port: attaching the same element twice returns the first handle', () => {
    const { input, handle, fake } = renderInput();

    expect(attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true }), fake.twin)).toBe(handle);
  });
});
