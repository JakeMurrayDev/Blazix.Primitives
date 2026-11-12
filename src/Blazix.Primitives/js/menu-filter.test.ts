// L3 tests of the `menu-filter` module (M5 track 12, issue 197). The key table first: Base UI's
// menu/filter-root/useMenuFilterKeyDown.test.ts, both `it.each` tables with every row, against the module's pure
// getMenuFilterKeyAction (MNU-KEY-22, KEY-30 re-tagged to L3 with "(L4 for …)" for the rest).

import { afterEach, describe, expect, it, vi } from 'vitest';
import { resetFocusManagerState } from './focus-manager';
import { resetInteractionsForTest } from './interactions';

import { attach, getMenuFilterKeyAction, type MenuFilterKey, type MenuFilterKeyAction, type MenuFilterOptions } from './menu-filter';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';
import { createListHarness, type ListHarness } from './test-support/list-harness';
import { resetFrameScheduler } from './transitions';
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

// The List handle's item texts (MNU-STA-15, STA-17; design point 2 (b′)): the List reports the rendered `textContent` of
// the items C# names, as useFilterDropdownItem.ts:100-140 reads it, so items without a label filter by what they render.
describe('the List handle', () => {
  const handles: TwinHandle[] = [];

  afterEach(() => {
    handles.splice(0).forEach((handle) => handle.dispose());
    document.body.replaceChildren();
  });

  interface Texts {
    ids: string[];
    texts: string[];
  }

  function renderList(items: Record<string, string>, textIds: string[], fake: FakeTwin = createFakeTwin()) {
    const list = document.createElement('div');
    for (const [id, text] of Object.entries(items)) {
      const item = document.createElement('div');
      item.id = id;
      item.innerHTML = `<span>${text}</span>`;
      list.append(item);
    }
    document.body.append(list);
    const json = (ids: string[]) => JSON.stringify({ part: 'list', menuKey: 'menu-1', textIds: ids });
    const handle = attach(list, json(textIds), fake.twin);
    handles.push(handle);
    return { list, fake, update: (ids: string[]) => handle.update(json(ids)) };
  }

  it('port: reports the rendered text of each requested item once', () => {
    const { fake, update } = renderList({ a: 'Rename', b: 'Copy' }, ['a', 'b']);

    update(['a', 'b']);

    expect(fake.reported<Texts>('OnTexts')).toEqual([{ ids: ['a', 'b'], texts: ['Rename', 'Copy'] }]);
  });

  it('port: reports an item whose rendered text changes, and only that item', async () => {
    const { list, fake } = renderList({ a: 'Rename', b: 'Copy' }, ['a', 'b']);

    list.querySelector('#b span')!.textContent = 'Duplicate';
    await Promise.resolve();

    expect(fake.reported<Texts>('OnTexts')[1]).toEqual({ ids: ['b'], texts: ['Duplicate'] });
  });

  // MNU-PTR-20 (useMenuItemCommonProps.ts:12-14, 84): under virtual focus an item's mousedown keeps focus on the input.
  it('port: prevents the mousedown default on an item, and leaves the list background alone', () => {
    const { list } = renderList({ a: 'Rename', b: 'Copy' }, []);
    list.querySelectorAll<HTMLElement>('[id]').forEach((element) => element.setAttribute('role', 'menuitem'));

    const onItem = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    list.querySelector('#a span')!.dispatchEvent(onItem);
    const onList = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    list.dispatchEvent(onList);

    expect(onItem.defaultPrevented).toBe(true);
    expect(onList.defaultPrevented).toBe(false);
  });

  it('port: reports a newly requested item on update and skips an id the list does not render', () => {
    const { list, fake, update } = renderList({ a: 'Rename' }, ['a']);
    const added = document.createElement('div');
    added.id = 'c';
    added.textContent = 'Pin';
    list.append(added);

    update(['a', 'c', 'gone']);

    expect(fake.reported<Texts>('OnTexts')).toEqual([
      { ids: ['a'], texts: ['Rename'] },
      { ids: ['c'], texts: ['Pin'] },
    ]);
  });
});

// The key router (MNU-KEY-22, KEY-30; useMenuFilterKeyDown.ts:103-170): the Input is the list's reference under virtual
// focus, and the module keeps `composite`'s list navigation out of the keys getMenuFilterKeyAction gives to the input.
describe('the key router', () => {
  const harnesses: ListHarness[] = [];
  const handles: TwinHandle[] = [];

  afterEach(() => {
    handles.splice(0).forEach((handle) => handle.dispose());
    harnesses.splice(0).forEach((harness) => harness.dispose());
    resetInteractionsForTest();
    resetFocusManagerState();
    resetFrameScheduler();
    document.body.replaceChildren();
  });

  function keyDown(target: EventTarget, key: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, composed: true, ...init });
    target.dispatchEvent(event);
    return event;
  }

  // An open filterable menu: the harness's input reference under virtual focus, with the menu-filter Input handle on it.
  async function openFilterMenu(itemAttributes?: (index: number, label: string) => Record<string, string>): Promise<{ harness: ListHarness; input: HTMLInputElement; fake: FakeTwin }> {
    const harness = createListHarness({ reference: 'input', list: { virtual: true }, ...(itemAttributes ? { itemAttributes } : {}) });
    harnesses.push(harness);
    const input = harness.reference as HTMLInputElement;
    const fake = createFakeTwin();
    handles.push(attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true, orientation: 'vertical' }), fake.twin));
    input.focus();
    keyDown(input, 'ArrowDown');
    await vi.waitFor(() => expect(input.getAttribute('aria-activedescendant')).toBe(harness.item(0).id), { timeout: 1000, interval: 5 });
    return { harness, input, fake };
  }

  // KEY-22's "leaves modified editing keys to the input while an item is highlighted": a modified main-axis arrow is an
  // `edit` key (text selection), which `composite`'s reference handle would take as navigation.
  it.each(['ArrowDown', 'ArrowUp'])('port: Shift+%s in the input selects text instead of moving the highlight', async (key) => {
    const { harness, input } = await openFilterMenu();
    input.value = 'on';

    const event = keyDown(input, key, { shiftKey: true });

    expect(event.defaultPrevented).toBe(false);
    expect(input.getAttribute('aria-activedescendant')).toBe(harness.item(0).id);
  });

  // KEY-25 (useMenuFilterKeyDown.ts:173-176): Enter on a highlighted item clicks it, with the key's modifiers.
  it('port: Enter on a highlighted item clicks it', async () => {
    const { harness, input } = await openFilterMenu();
    const clicks: MouseEvent[] = [];
    harness.item(0).addEventListener('click', (event) => clicks.push(event));

    const event = keyDown(input, 'Enter', { ctrlKey: true });

    expect(event.defaultPrevented).toBe(true);
    expect(clicks).toHaveLength(1);
    expect(clicks[0]?.ctrlKey).toBe(true);
  });

  // KEY-29 (useMenuFilterKeyDown.ts:161-169): the cross-axis key on a highlighted submenu trigger is the trigger's own.
  it('port: the cross-axis key on a highlighted submenu trigger is dispatched on the trigger', async () => {
    const { harness, input } = await openFilterMenu((index) => (index === 0 ? { 'aria-haspopup': 'menu' } : {}));
    input.value = 'on';
    const keys: string[] = [];
    harness.item(0).addEventListener('keydown', (event) => keys.push(event.key));

    keyDown(input, 'ArrowRight');

    expect(keys).toEqual(['ArrowRight']);
    expect(input.getAttribute('aria-activedescendant')).toBe(harness.item(0).id);
  });

  // KEY-24 (useMenuFilterKeyDown.ts:155-167): in a horizontal menu with nothing highlighted, ArrowDown highlights the first
  // item and ArrowUp the last.
  it.each([
    ['ArrowDown', 0],
    ['ArrowUp', 2],
  ] as const)('port: %s in a horizontal menu with nothing highlighted enters the list at its edge', async (key, index) => {
    const harness = createListHarness({ reference: 'input', list: { virtual: true, orientation: 'horizontal' }, session: { open: true } });
    harnesses.push(harness);
    const input = harness.reference as HTMLInputElement;
    handles.push(attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true, orientation: 'horizontal' }), createFakeTwin().twin));
    input.focus();

    const event = keyDown(input, key);

    expect(event.defaultPrevented).toBe(true);
    await vi.waitFor(() => expect(input.getAttribute('aria-activedescendant')).toBe(harness.item(index).id), { timeout: 1000, interval: 5 });
  });

  // Design point 6 (a) (ADR 04 Amendments 2026-11-10's pattern): while C# holds the input's consumer onkeydown, the router
  // asks `navigate` first; a stop verdict skips the router's action and the list navigation.
  it('port: a stop verdict from the navigate point skips the router and the list navigation', async () => {
    const { harness, input } = await openFilterMenu();
    const decider = createFakeTwin({ synchronous: true, verdict: 'stop' });
    handles.splice(0).forEach((handle) => handle.dispose());
    handles.push(attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true, orientation: 'vertical', decidesKeyDown: true }), decider.twin));
    const clicks: Event[] = [];
    harness.item(0).addEventListener('click', (event) => clicks.push(event));

    const enter = keyDown(input, 'Enter');
    const down = keyDown(input, 'ArrowDown');

    expect(decider.decisions.map((decision) => decision.point)).toEqual(['navigate', 'navigate']);
    expect(clicks).toHaveLength(0);
    expect(enter.defaultPrevented).toBe(false);
    expect(down.defaultPrevented).toBe(false);
    expect(input.getAttribute('aria-activedescendant')).toBe(harness.item(0).id);
  });

  // KEY-28 with design point 6 (a): the trigger's relay asks `navigate` first; a stop verdict leaves the key on the trigger.
  it('port: a stop verdict from the navigate point leaves the key on the trigger', async () => {
    const { input } = await openFilterMenu();
    const trigger = document.createElement('button');
    document.body.append(trigger);
    const decider = createFakeTwin({ synchronous: true, verdict: 'stop' });
    handles.push(attach(trigger, JSON.stringify({ part: 'trigger', menuKey: 'menu-1', open: true, decidesKeyDown: true }), decider.twin));
    trigger.focus();

    keyDown(trigger, 'ArrowDown');

    expect(decider.decisions.map((decision) => decision.point)).toEqual(['navigate']);
    expect(document.activeElement).toBe(trigger);
    expect(input.getAttribute('aria-activedescendant')).not.toBe(null);
  });

  // KEY-26 (useMenuFilterKeyDown.ts:179-192): Shift+Tab in the input asks C# to close with focus-out.
  it('port: Shift+Tab in the input reports a focus-out close', async () => {
    const { input, fake } = await openFilterMenu();

    const event = keyDown(input, 'Tab', { shiftKey: true });

    expect(event.defaultPrevented).toBe(true);
    expect(fake.reported('OnFocusOut')).toHaveLength(1);
  });
});

// MNU-ARIA-15 (MenuStore.ts:107-111, useMenuItemCommonProps.ts:63-68, 82; design point 5 (c)): on WebKit only, while the
// input has focus, the item its aria-activedescendant names carries aria-selected="true", written by the Input handle.
describe('the Input handle and aria-selected', () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.doUnmock('./platform');
    vi.resetModules();
  });

  function renderItems(): { input: HTMLInputElement; first: HTMLElement; second: HTMLElement } {
    const input = document.createElement('input');
    const first = document.createElement('div');
    first.id = 'item-1';
    const second = document.createElement('div');
    second.id = 'item-2';
    document.body.append(input, first, second);
    return { input, first, second };
  }

  async function attachUnder(webkit: boolean): Promise<typeof import('./menu-filter')> {
    vi.resetModules();
    vi.doMock('./platform', async (importOriginal) => {
      const actual = await importOriginal<typeof import('./platform')>();
      return { ...actual, platform: { ...actual.platform, engine: { ...actual.platform.engine, webkit } } };
    });
    return import('./menu-filter');
  }

  it('port: marks the highlighted item aria-selected on WebKit while the input has focus', async () => {
    const module = await attachUnder(true);
    const { input, first, second } = renderItems();
    const handle = module.attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true }), createFakeTwin().twin);
    input.focus();

    input.setAttribute('aria-activedescendant', 'item-1');
    await Promise.resolve();
    expect(first.getAttribute('aria-selected')).toBe('true');

    input.setAttribute('aria-activedescendant', 'item-2');
    await Promise.resolve();
    expect(first.hasAttribute('aria-selected')).toBe(false);
    expect(second.getAttribute('aria-selected')).toBe('true');

    input.blur();
    expect(second.hasAttribute('aria-selected')).toBe(false);
    handle.dispose();
  });

  // MNU-A11Y-02 (MenuInput.tsx:32-55): the mount-time check of the active element. On WebAssembly the focus manager's
  // initial focus and composite's aria-activedescendant can both land before the Input's handle attaches.
  it('port: marks the highlighted item when the input was focused and named it before its handle attached', async () => {
    const module = await attachUnder(true);
    const { input, first } = renderItems();
    input.focus();
    input.setAttribute('aria-activedescendant', 'item-1');

    const handle = module.attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true }), createFakeTwin().twin);

    expect(first.getAttribute('aria-selected')).toBe('true');
    handle.dispose();
    expect(first.hasAttribute('aria-selected')).toBe(false);
  });

  it('does not set aria-selected on highlighted items outside WebKit', async () => {
    const module = await attachUnder(false);
    const { input, first } = renderItems();
    const handle = module.attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true }), createFakeTwin().twin);
    input.focus();

    input.setAttribute('aria-activedescendant', 'item-1');
    await Promise.resolve();

    expect(first.hasAttribute('aria-selected')).toBe(false);
    handle.dispose();
  });
});

// MNU-PTR-19 (MenuFilterPopup.tsx): a screen reader's press on the trigger opens the menu with its input focused. The
// popup's handle sees the press; under a slow WebAssembly start the input's handle can attach a frame later.
describe('a screen reader press on the trigger', () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.doUnmock('./platform');
    vi.resetModules();
  });

  async function attachOutsideJsdom(): Promise<typeof import('./menu-filter')> {
    vi.resetModules();
    vi.doMock('./platform', async (importOriginal) => {
      const actual = await importOriginal<typeof import('./platform')>();
      return { ...actual, platform: { ...actual.platform, env: { ...actual.platform.env, jsdom: false } } };
    });
    return import('./menu-filter');
  }

  function virtualPress(target: HTMLElement): void {
    const event = new MouseEvent('pointerdown', { bubbles: true, cancelable: true, detail: 0 });
    for (const [name, value] of Object.entries({ pointerType: 'touch', width: 0.333, height: 0.333, pressure: 0, isPrimary: true })) {
      Object.defineProperty(event, name, { value });
    }
    target.dispatchEvent(event);
  }

  const frames = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 50));

  it('port: focuses the input when its handle and the popup handle attach together', async () => {
    const module = await attachOutsideJsdom();
    const trigger = document.createElement('button');
    const popup = document.createElement('div');
    const input = document.createElement('input');
    popup.append(input);
    document.body.append(trigger, popup);
    const handles = [module.attach(trigger, JSON.stringify({ part: 'trigger', menuKey: 'menu-1', open: false }), createFakeTwin().twin)];
    virtualPress(trigger);

    handles.push(module.attach(popup, JSON.stringify({ part: 'popup', menuKey: 'menu-1', open: true }), createFakeTwin().twin));
    handles.push(module.attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true }), createFakeTwin().twin));
    await frames();

    expect(document.activeElement).toBe(input);
    handles.forEach((handle) => handle.dispose());
  });

  it('port: focuses an input whose handle attaches after the popup handle saw the press', async () => {
    const module = await attachOutsideJsdom();
    const trigger = document.createElement('button');
    const popup = document.createElement('div');
    const input = document.createElement('input');
    popup.append(input);
    document.body.append(trigger, popup);
    const handles = [module.attach(trigger, JSON.stringify({ part: 'trigger', menuKey: 'menu-1', open: false }), createFakeTwin().twin)];
    virtualPress(trigger);

    handles.push(module.attach(popup, JSON.stringify({ part: 'popup', menuKey: 'menu-1', open: true }), createFakeTwin().twin));
    await frames();
    handles.push(module.attach(input, JSON.stringify({ part: 'input', menuKey: 'menu-1', open: true }), createFakeTwin().twin));
    await frames();

    expect(document.activeElement).toBe(input);
    handles.forEach((handle) => handle.dispose());
  });
});
