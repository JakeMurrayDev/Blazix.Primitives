// L3 (ADR 06 §1): the `combobox` component module in jsdom against a fake twin, in its Server form and its
// `synchronous: true` (WebAssembly) form where the paths differ. The platform is injected through `createAttach`, so the
// Gecko and Android titles of `ComboboxInput.gecko.test.tsx` and `ComboboxInput.android.test.tsx` run here (the brief's
// re-tag to L3 with `detectPlatform` injected). Positive titles only: each asserts the behaviour first.
import { afterEach, describe, expect, it } from 'vitest';
import {
  CLEAR_POINT,
  attach,
  caretFor,
  createAttach,
  isTypedInput,
  resolveCommittedValue,
  type ComboboxInputChange,
  type ComboboxOptions,
} from './combobox';
import { detectPlatform } from './platform';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
});

const desktop = detectPlatform({ userAgent: 'Mozilla/5.0 (Macintosh) AppleWebKit/537.36 Chrome/130', platform: 'MacIntel', maxTouchPoints: 0 }, false);
const gecko = detectPlatform({ userAgent: 'Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0', platform: 'Linux x86_64', maxTouchPoints: 0 }, false);
const android = detectPlatform({ userAgent: 'Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/130 Mobile', platform: 'Linux armv8l', maxTouchPoints: 5 }, false);

function options(overrides: Partial<ComboboxOptions> = {}): string {
  return JSON.stringify({ part: 'input', valueRevision: 0, ...overrides } satisfies ComboboxOptions);
}

function setupInput(value = '', overrides: Partial<ComboboxOptions> = {}, synchronous = false, platform = desktop): { input: HTMLInputElement; fake: FakeTwin; handle: TwinHandle } {
  const input = document.createElement('input');
  input.value = value;
  document.body.append(input);
  const fake = createFakeTwin({ synchronous });
  const handle = createAttach(platform)(input, options(overrides), fake.twin);
  handles.push(handle);
  return { input, fake, handle };
}

function typeInto(input: HTMLInputElement, text: string, inputType = 'insertText'): void {
  input.value = text;
  input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType }));
}

describe('the input', () => {
  it('port: classifies an inputType other than insertReplacementText as typed, and a missing one as autofill-like', () => {
    expect(isTypedInput(new InputEvent('input', { inputType: 'insertText' }), false)).toBe(true);
    expect(isTypedInput(new InputEvent('input', { inputType: 'insertReplacementText' }), false)).toBe(false);
    expect(isTypedInput(new Event('input'), false)).toBe(false);
    expect(isTypedInput(new Event('input'), true)).toBe(true);
  });

  it('port: reports a typed change and an autofill-like change with their classification', () => {
    const { input, fake } = setupInput();
    typeInto(input, 'a');
    typeInto(input, 'apple', 'insertReplacementText');
    const changes = fake.reported<ComboboxInputChange>('OnInputChange');
    expect(changes.map((change) => [change.text, change.typed, change.composing])).toEqual([
      ['a', true, false],
      ['apple', false, false],
    ]);
  });

  it('port: during a composition reports the text as composing, then commits it once at compositionend', () => {
    const { input, fake } = setupInput();
    input.dispatchEvent(new CompositionEvent('compositionstart'));
    typeInto(input, 'に', 'insertCompositionText');
    input.dispatchEvent(new CompositionEvent('compositionend'));
    typeInto(input, 'に', 'insertText');
    const changes = fake.reported<ComboboxInputChange>('OnInputChange');
    expect(changes.map((change) => [change.text, change.composing])).toEqual([
      ['に', true],
      ['に', false],
    ]);
  });

  it('port: on Android a composition is not tracked, so each change is committed as typed input', () => {
    const { input, fake } = setupInput('', {}, false, android);
    input.dispatchEvent(new CompositionEvent('compositionstart'));
    typeInto(input, 'he', 'insertCompositionText');
    const changes = fake.reported<ComboboxInputChange>('OnInputChange');
    expect(changes.map((change) => [change.text, change.typed, change.composing])).toEqual([['he', true, false]]);
  });

  it('port: a committed value that echoes the reported typing is not written, so newer typing survives', () => {
    const { input, handle } = setupInput('');
    typeInto(input, 'a');
    typeInto(input, 'ab');
    handle.update(options({ value: 'a', valueRevision: 1 }));
    expect(input.value).toBe('ab');
    handle.update(options({ value: 'ab', valueRevision: 2 }));
    expect(input.value).toBe('ab');
  });

  it('port: a committed value that is no echo is written: a fill, a clear, and a controlled value the consumer did not apply', () => {
    const { input, handle } = setupInput('ban');
    handle.update(options({ value: 'banana', valueRevision: 1 }));
    expect(input.value).toBe('banana');
    handle.update(options({ value: '', valueRevision: 2 }));
    expect(input.value).toBe('');
    typeInto(input, 'x');
    handle.update(options({ value: '', valueRevision: 3, controlled: true }));
    expect(input.value).toBe('');
  });

  it('port: the value writer consumes the echoes up to the matched one and clears them on a value that is no echo', () => {
    const pending = ['a', 'ab', 'abc'];
    expect(resolveCommittedValue(pending, 'ab', 'abc')).toBeNull();
    expect(pending).toEqual(['abc']);
    expect(resolveCommittedValue(pending, 'z', 'abc')).toBe('z');
    expect(pending).toEqual([]);
  });

  it('moves the caret to the start on Home and to the end on End', () => {
    const { input } = setupInput('hello world');
    input.setSelectionRange(5, 5);
    const home = new KeyboardEvent('keydown', { key: 'Home', bubbles: true, cancelable: true });
    input.dispatchEvent(home);
    expect([input.selectionStart, home.defaultPrevented]).toEqual([0, true]);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true, cancelable: true }));
    expect(input.selectionStart).toBe(11);
  });

  it('swaps Home and End in a right-to-left input on Gecko', () => {
    expect(caretFor('Home', 5, true, true)).toBe(5);
    expect(caretFor('End', 5, true, true)).toBe(0);
    expect(caretFor('Home', 5, true, false)).toBe(0);
    const { input } = setupInput('שלום', { rtl: true }, false, gecko);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true, cancelable: true }));
    expect(input.selectionStart).toBe(4);
  });

  it('port: on Server the closed Escape stops the keydown when there is something to clear and reports the clear', () => {
    const { input, fake } = setupInput('apple', { hasValue: true, mounted: false });
    const outside: string[] = [];
    document.body.addEventListener('keydown', () => outside.push('bubbled'));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    expect(fake.reported('OnClear')).toHaveLength(1);
    expect(outside).toEqual([]);
  });

  it('port: on Server the Escape after a close clears once the popup list left the document, though the update still says mounted', () => {
    const list = document.createElement('div');
    list.id = 'gone-list';
    document.body.append(list);
    const { input, fake } = setupInput('apple', { hasValue: true, mounted: true, listId: 'gone-list' });
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    expect(fake.reported('OnClear')).toHaveLength(0);
    list.remove();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    expect(fake.reported('OnClear')).toHaveLength(1);
  });

  it('port: on Server the closed Escape lets the keydown bubble when there is nothing to clear, or the root is inline', () => {
    const { input } = setupInput('', { hasValue: false, mounted: false });
    const outside: string[] = [];
    document.body.addEventListener('keydown', () => outside.push('bubbled'));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    expect(outside).toEqual(['bubbled']);
  });

  it('port: on WebAssembly the closed Escape asks the clear point and reports nothing, its verdict deciding the propagation', () => {
    const { input, fake } = setupInput('apple', { hasValue: true, mounted: false }, true);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    expect(fake.decisions.map((decision) => decision.point)).toEqual([CLEAR_POINT]);
    expect(fake.reported('OnClear')).toHaveLength(0);
  });

  it('port: Enter on an open list clicks the highlighted item, prevented and stopped, after recording the keydown', () => {
    const { input, fake } = setupInput('', { open: true });
    const item = document.createElement('div');
    item.id = 'item-1';
    const clicks: string[] = [];
    item.addEventListener('click', () => clicks.push('click'));
    document.body.append(item);
    input.setAttribute('aria-activedescendant', 'item-1');
    const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    input.dispatchEvent(enter);
    expect([clicks, enter.defaultPrevented, fake.reported('OnSelectionEvent').length]).toEqual([['click'], true, 1]);
  });

  it('port: Enter on an open list without a highlight is not prevented and reports the close', () => {
    const { input, fake } = setupInput('', { open: true });
    const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    input.dispatchEvent(enter);
    expect([enter.defaultPrevented, fake.reported('OnEnterClose').length]).toEqual([false, 1]);
  });

  it('port: a typed change resets the list scroll, unless the list is virtualized', () => {
    const popup = document.createElement('div');
    popup.id = 'popup';
    const scroller = document.createElement('div');
    const list = document.createElement('div');
    list.id = 'list';
    list.append(document.createElement('div'));
    scroller.append(list);
    popup.append(scroller);
    document.body.append(popup);
    Object.defineProperty(scroller, 'scrollHeight', { value: 400 });
    Object.defineProperty(scroller, 'clientHeight', { value: 100 });
    scroller.style.overflowY = 'auto';
    scroller.scrollTop = 50;
    const { input } = setupInput('', { listId: 'list', popupId: 'popup' });
    typeInto(input, 'a');
    expect(scroller.scrollTop).toBe(0);
  });

  it('port: the start dismiss button before the input reports its press', () => {
    const { input, fake } = setupInput();
    const dismiss = document.createElement('span');
    dismiss.setAttribute('role', 'button');
    dismiss.setAttribute('aria-label', 'Dismiss');
    input.before(dismiss);
    dismiss.click();
    expect(fake.reported('OnDismiss')).toHaveLength(1);
  });
});

describe('the list and the press defaults', () => {
  function setupList(): { list: HTMLElement; item: HTMLElement; handle: TwinHandle } {
    const list = document.createElement('div');
    const item = document.createElement('div');
    item.setAttribute('role', 'option');
    list.append(item);
    document.body.append(list);
    const handle = attach(list, options({ part: 'list' }), createFakeTwin().twin);
    handles.push(handle);
    return { list, item, handle };
  }

  it('prevents the default of a pointerdown and a mousedown on an item, so the input keeps the focus', () => {
    const { item } = setupList();
    const pointerDown = new PointerEvent('pointerdown', { bubbles: true, cancelable: true, isPrimary: true });
    const mouseDown = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    item.dispatchEvent(pointerDown);
    item.dispatchEvent(mouseDown);
    expect([pointerDown.defaultPrevented, mouseDown.defaultPrevented]).toEqual([true, true]);
  });

  it('port: a mouseup on a highlighted item the press did not start on clicks it, and one that started there does not', () => {
    const { item } = setupList();
    const clicks: string[] = [];
    item.addEventListener('click', () => clicks.push('click'));
    item.setAttribute('data-highlighted', '');
    item.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, button: 0 }));
    expect(clicks).toEqual(['click']);
    item.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, isPrimary: true }));
    item.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, button: 0 }));
    expect(clicks).toEqual(['click']);
  });

  it('port: a mousedown on an input group outside the input prevents its default, so the input keeps the focus', () => {
    const group = document.createElement('div');
    const input = document.createElement('input');
    input.id = 'the-input';
    const icon = document.createElement('span');
    group.append(input, icon);
    document.body.append(group);
    handles.push(attach(group, options({ part: 'input-group', inputId: 'the-input' }), createFakeTwin().twin));
    const onIcon = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    const onInput = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    icon.dispatchEvent(onIcon);
    input.dispatchEvent(onInput);
    expect([onIcon.defaultPrevented, onInput.defaultPrevented]).toEqual([true, false]);
  });
});

describe('the trigger', () => {
  function setupTrigger(overrides: Partial<ComboboxOptions> = {}): { trigger: HTMLButtonElement; input: HTMLInputElement; fake: FakeTwin } {
    const trigger = document.createElement('button');
    trigger.getBoundingClientRect = () => ({ left: 0, right: 100, top: 0, bottom: 20, width: 100, height: 20, x: 0, y: 0, toJSON: () => ({}) });
    const input = document.createElement('input');
    input.id = 'the-input';
    const outside = document.createElement('div');
    outside.id = 'outside';
    document.body.append(trigger, input, outside);
    const fake = createFakeTwin();
    handles.push(attach(trigger, options({ part: 'trigger', inputId: 'the-input', insidePopup: true, ...overrides }), fake.twin));
    return { trigger, input, fake };
  }

  function release(target: Element, clientX: number, clientY: number): void {
    target.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, clientX, clientY }));
  }

  it('closes the popup when mouseup occurs outside the trigger bounds', () => {
    const { trigger, fake } = setupTrigger();
    trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 300, 300);
    expect(fake.reported('OnCancelOpen')).toHaveLength(1);
  });

  it('keeps the popup open when mouseup remains near the trigger bounds', () => {
    const { trigger, fake } = setupTrigger();
    trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 300, 300);
    trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 104, 24);
    expect(fake.reported('OnCancelOpen')).toHaveLength(1);
  });

  it('closes the popup when the release is more than 5px outside the trigger bounds', () => {
    const { trigger, fake } = setupTrigger();
    trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 106, 10);
    expect(fake.reported('OnCancelOpen')).toHaveLength(1);
  });

  it('ignores a pending mouseup after the trigger unmounts', () => {
    const { trigger, fake } = setupTrigger();
    trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 300, 300);
    trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    trigger.remove();
    release(document.getElementById('outside')!, 300, 300);
    expect(fake.reported('OnCancelOpen')).toHaveLength(1);
  });

  it('port: a press while open, or with the input outside the popup, waits for no release', () => {
    const closed = setupTrigger();
    closed.trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 300, 300);
    const open = setupTrigger({ open: true });
    open.trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 300, 300);
    const outside = setupTrigger({ insidePopup: false });
    outside.trigger.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    release(document.getElementById('outside')!, 300, 300);
    expect([closed.fake.reported('OnCancelOpen').length, open.fake.reported('OnCancelOpen').length, outside.fake.reported('OnCancelOpen').length]).toEqual([1, 0, 0]);
  });

  it('port: a mouse press focuses the input, and keeps the focus there when the input is outside the popup', () => {
    const { trigger, input } = setupTrigger({ insidePopup: false });
    const press = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    trigger.dispatchEvent(press);
    expect([document.activeElement, press.defaultPrevented]).toEqual([input, true]);
  });

  it('port: a touch press leaves the focus alone', () => {
    const { trigger } = setupTrigger({ insidePopup: false });
    trigger.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch' }));
    const press = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    trigger.dispatchEvent(press);
    expect([document.activeElement, press.defaultPrevented]).toEqual([document.body, false]);
  });
});

describe('the input group and the chips', () => {
  function setupGroup(part: 'input-group' | 'chips', overrides: Partial<ComboboxOptions> = {}): { group: HTMLElement; input: HTMLInputElement; button: HTMLButtonElement; chips: HTMLElement; fake: FakeTwin } {
    const group = document.createElement('div');
    const input = document.createElement('input');
    input.id = 'the-input';
    const button = document.createElement('button');
    const chips = document.createElement('div');
    chips.id = 'the-chips';
    const chip = document.createElement('span');
    chips.append(chip);
    group.append(chips, input, button);
    document.body.append(group);
    const fake = createFakeTwin();
    handles.push(attach(group, options({ part, inputId: 'the-input', chipsId: 'the-chips', ...overrides }), fake.twin));
    return { group, input, button, chips, fake };
  }

  function press(target: Element): MouseEvent {
    const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    return event;
  }

  it('port: a press on the input group focuses the input and reports an input press', () => {
    const { group, input, fake } = setupGroup('input-group');
    const event = press(group);
    expect([event.defaultPrevented, document.activeElement, fake.reported('OnInputPress').length]).toEqual([true, input, 1]);
  });

  it('port: a press on an interactive element or inside the chips container of an input group is left alone', () => {
    const { group, button, chips, fake } = setupGroup('input-group');
    press(group);
    const onButton = press(button);
    const onChip = press(chips.firstElementChild!);
    expect([onButton.defaultPrevented, onChip.defaultPrevented, fake.reported('OnInputPress').length]).toEqual([false, false, 1]);
  });

  it('port: a press on a disabled input group prevents its default and reports nothing', () => {
    const enabled = setupGroup('input-group');
    press(enabled.group);
    const disabled = setupGroup('input-group', { disabled: true });
    const event = press(disabled.group);
    expect([enabled.fake.reported('OnInputPress').length, event.defaultPrevented, disabled.fake.reported('OnInputPress').length]).toEqual([1, true, 0]);
  });

  it('port: a press on the chips container focuses the input and reports an input press', () => {
    const { chips, input, fake } = setupGroup('chips');
    const event = press(chips.firstElementChild!);
    expect([event.defaultPrevented, document.activeElement, fake.reported('OnInputPress').length]).toEqual([true, input, 1]);
  });
});

describe('the clear and chip remove buttons', () => {
  function setupButton(part: 'clear' | 'chip-remove', overrides: Partial<ComboboxOptions> = {}): { button: HTMLButtonElement; input: HTMLInputElement } {
    const button = document.createElement('button');
    const input = document.createElement('input');
    input.id = 'the-input';
    document.body.append(input, button);
    handles.push(attach(button, options({ part, inputId: 'the-input', ...overrides }), createFakeTwin().twin));
    return { button, input };
  }

  it('port: a click on the clear or a chip remove button moves the focus to the input, unless disabled or read-only', () => {
    const results: boolean[] = [];
    for (const part of ['clear', 'chip-remove'] as const) {
      for (const overrides of [{}, { disabled: true }, { readOnly: true }]) {
        const { button, input } = setupButton(part, overrides);
        button.focus();
        button.click();
        results.push(document.activeElement === input);
        document.body.replaceChildren();
      }
    }
    expect(results).toEqual([true, false, false, true, false, false]);
  });
});

describe('the chips keys', () => {
  type Three<T> = [T, T, T];

  function setupChips(overrides: Partial<ComboboxOptions> = {}, synchronous = false): { chips: HTMLElement; chip: Three<HTMLElement>; remove: Three<HTMLButtonElement>; input: HTMLInputElement; fake: FakeTwin; removeFakes: Three<FakeTwin> } {
    const chips = document.createElement('div');
    chips.id = 'the-chips';
    const chip: HTMLElement[] = [];
    const remove: HTMLButtonElement[] = [];
    const removeFakes: FakeTwin[] = [];
    for (const name of ['a', 'b', 'c']) {
      const element = document.createElement('div');
      element.id = 'chip-' + name;
      element.tabIndex = -1;
      const button = document.createElement('button');
      element.append(button);
      chips.append(element);
      chip.push(element);
      remove.push(button);
    }
    const input = document.createElement('input');
    input.id = 'the-input';
    document.body.append(chips, input);
    const fake = createFakeTwin();
    handles.push(attach(chips, options({ part: 'chips', inputId: 'the-input', chipIds: ['chip-a', 'chip-b', 'chip-c'], ...overrides }), fake.twin));
    remove.forEach((button, index) => {
      const removeFake = createFakeTwin({ synchronous });
      removeFakes.push(removeFake);
      handles.push(attach(button, options({ part: 'chip-remove', inputId: 'the-input', index, ...overrides }), removeFake.twin));
    });
    return { chips, chip: chip as Three<HTMLElement>, remove: remove as Three<HTMLButtonElement>, input, fake, removeFakes: removeFakes as Three<FakeTwin> };
  }

  function key(target: Element, name: string): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    return event;
  }

  it('port: the arrow keys move the focus between the chips and out to the input, mirrored in RTL', () => {
    const { chip, input } = setupChips();
    chip[1].focus();
    key(chip[1], 'ArrowLeft');
    const afterPrevious = document.activeElement;
    key(chip[0], 'ArrowLeft');
    const afterFirst = document.activeElement;
    chip[1].focus();
    key(chip[1], 'ArrowRight');
    const afterNext = document.activeElement;
    key(chip[2], 'ArrowRight');
    expect([afterPrevious, afterFirst, afterNext, document.activeElement]).toEqual([chip[0], input, chip[2], input]);
  });

  it('port: in RTL, ArrowRight moves to the previous chip', () => {
    const { chip } = setupChips({ rtl: true });
    chip[1].focus();
    key(chip[1], 'ArrowRight');
    expect(document.activeElement).toBe(chip[0]);
  });

  it('port: Backspace or Delete on a chip reports its removal and moves the focus to the chip that takes its place', () => {
    const { chip, fake } = setupChips();
    chip[1].focus();
    const backspace = key(chip[1], 'Backspace');
    const afterMiddle = document.activeElement;
    chip[2].focus();
    key(chip[2], 'Delete');
    expect([backspace.defaultPrevented, afterMiddle, document.activeElement]).toEqual([true, chip[2], chip[1]]);
    expect(fake.reported<{ index: number }>('OnRemove').map((report) => report.index)).toEqual([1, 2]);
  });

  it('port: Enter, Space and a printable key on a chip move the focus to the input; ArrowDown also asks for the open', () => {
    const { chip, input, fake } = setupChips();
    const focused: (Element | null)[] = [];
    for (const name of ['Enter', ' ', 'x', 'ArrowDown']) {
      chip[0].focus();
      key(chip[0], name);
      focused.push(document.activeElement);
    }
    expect(focused).toEqual([input, input, input, input]);
    expect(fake.reported('OnOpenRequest')).toHaveLength(1);
  });

  it('port: a disabled or read-only chips container ignores the chip keys', () => {
    const enabled = setupChips();
    enabled.chip[1].focus();
    key(enabled.chip[1], 'ArrowLeft');
    const moved = document.activeElement === enabled.chip[0];
    document.body.replaceChildren();
    const readOnly = setupChips({ readOnly: true });
    readOnly.chip[1].focus();
    key(readOnly.chip[1], 'ArrowLeft');
    expect([moved, document.activeElement === readOnly.chip[1], readOnly.fake.reported('OnRemove').length]).toEqual([true, true, 0]);
  });

  it('port: ArrowLeft at the start of the input moves the focus to the last chip', () => {
    const { chips, chip } = setupChips();
    const input = document.getElementById('the-input') as HTMLInputElement;
    handles.push(createAttach(desktop)(input, options({ part: 'input', multiple: true, hasValue: true, chipsId: chips.id }), createFakeTwin().twin));
    input.focus();
    const event = key(input, 'ArrowLeft');
    expect([event.defaultPrevented, document.activeElement]).toEqual([true, chip[2]]);
  });

  it('port: Backspace in an empty input reports the removal with the number of rendered chips', () => {
    const { chips } = setupChips();
    const input = document.getElementById('the-input') as HTMLInputElement;
    const fake = createFakeTwin();
    handles.push(createAttach(desktop)(input, options({ part: 'input', multiple: true, hasValue: true, chipsId: chips.id }), fake.twin));
    key(input, 'Backspace');
    expect(fake.reported<{ index: number }>('OnRemove').map((report) => report.index)).toEqual([3]);
  });

  it('port: a chip remove button asks the remove point, reports on Server and stops the click by default', () => {
    const { remove, removeFakes } = setupChips();
    let bubbled = 0;
    document.body.addEventListener('click', () => bubbled++);
    remove[1].click();
    const enter = key(remove[2], 'Enter');
    expect(removeFakes[1].reported<{ index: number }>('OnRemove').map((report) => report.index)).toEqual([1]);
    expect(removeFakes[2].reported<{ index: number }>('OnRemove').map((report) => report.index)).toEqual([2]);
    expect([bubbled, enter.defaultPrevented]).toEqual([0, true]);
  });

  it('port: on WebAssembly the remove point runs in C# and its verdict decides the propagation', () => {
    const { remove, removeFakes } = setupChips({}, true);
    let bubbled = 0;
    document.body.addEventListener('click', () => bubbled++);
    remove[0].click();
    expect([removeFakes[0].decisions.length, removeFakes[0].reported('OnRemove').length, bubbled]).toEqual([1, 0, 1]);
  });
});
