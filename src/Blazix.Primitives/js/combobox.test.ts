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
