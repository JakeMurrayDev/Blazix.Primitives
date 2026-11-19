// L3 for the `number-field` component module, 6a's minimal surface (issue 277; design point 2 (b) and 7 (a), settled by
// the orchestrator 2026-11-18; ADR 04 Amendments 2026-11-18): the visible input's `value` handover with the revision rule,
// and the focus and blur reports with their `defaultPrevented` checks and the `input-focus` / `input-blur` decision points
// (fallback run). 6b adds the gate, paste, wheel and scrub.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { attach, type NumberFieldInputOptions, type NumberFieldOptions } from './number-field';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin, type FakeTwinOptions } from './test-support/fake-twin';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
});

function optionsJson(overrides: Partial<NumberFieldOptions> = {}): string {
  const options: NumberFieldOptions = { value: '', revision: 0, ...overrides };
  return JSON.stringify(options);
}

function mount(options: Partial<NumberFieldOptions> = {}, twin: FakeTwinOptions = {}, rendered = ''): { input: HTMLInputElement; hidden: HTMLInputElement; handle: TwinHandle; fake: FakeTwin } {
  const input = document.createElement('input');
  input.type = 'text';
  input.value = rendered;
  const hidden = document.createElement('input');
  hidden.type = 'number';
  document.body.append(input, hidden);
  const fake = createFakeTwin(twin);
  // C# renders the text it sends: the handover keeps `rendered` unless the options say otherwise.
  const handle = attach(input, optionsJson({ value: rendered, ...options }), fake.twin, [hidden]);
  handles.push(handle);
  return { input, hidden, handle, fake };
}

// en-US with no format: the allowed set C# computes (NUM-KEY-07) and the locale's symbols (NUM-KEY-09), in the options'
// `input` part, which C# sends at attach and again only when it changes.
const GATE: NumberFieldInputOptions = { allowed: '.,．，٫٬-+', decimal: '.', currency: '', percent: '' };
const EN_US: Partial<NumberFieldOptions> = { input: GATE };

function gate(overrides: Partial<NumberFieldInputOptions>): Partial<NumberFieldOptions> {
  return { input: { ...GATE, ...overrides } };
}

function key(input: HTMLInputElement, init: KeyboardEventInit & { which?: number }): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
  if (init.which !== undefined) {
    Object.defineProperty(event, 'which', { value: init.which });
  }
  input.dispatchEvent(event);
  return event;
}

function type(input: HTMLInputElement, text: string): Event {
  input.value = text;
  const event = new Event('input', { bubbles: true, cancelable: true });
  input.dispatchEvent(event);
  return event;
}

function paste(input: HTMLInputElement, text: string | (() => string)): Event {
  const event = new Event('paste', { bubbles: true, cancelable: true }) as ClipboardEvent;
  const getData = typeof text === 'string' ? () => text : text;
  Object.defineProperty(event, 'clipboardData', { value: { getData } });
  input.dispatchEvent(event);
  return event;
}

function wheel(input: HTMLInputElement, init: WheelEventInit): WheelEvent {
  const event = new WheelEvent('wheel', { bubbles: true, cancelable: true, ...init });
  input.dispatchEvent(event);
  return event;
}

describe('number-field module: the visible input', () => {
  it('port: the text is handed over at attach and follows each update', () => {
    const { input, handle } = mount({ value: '1,234' }, {}, '1,234');
    expect(input.value).toBe('1,234');

    handle.update(optionsJson({ value: '1,235' }));
    expect(input.value).toBe('1,235');
  });

  it('port: an update that echoes an older revision than the last blur report keeps the text', () => {
    const { input, handle, fake } = mount({ value: '5' }, {}, '5');

    input.value = '7';
    input.dispatchEvent(new FocusEvent('blur'));
    const [blur] = fake.reported<{ text: string; revision: number }>('OnInputBlur');
    expect(blur).toMatchObject({ text: '7', revision: 1 });

    // A C# update that predates the blur (it echoes revision 0) does not overwrite the text the user left.
    handle.update(optionsJson({ value: '5', revision: 0 }));
    expect(input.value).toBe('7');

    // The update that answers the blur echoes its revision and is applied.
    handle.update(optionsJson({ value: '7.00', revision: 1 }));
    expect(input.value).toBe('7.00');
  });

  it('port: focus and blur are reported unless their default was prevented', () => {
    const { input, fake } = mount();

    input.dispatchEvent(new FocusEvent('focus'));
    input.dispatchEvent(new FocusEvent('blur'));
    expect(fake.reported('OnInputFocus')).toHaveLength(1);
    expect(fake.reported('OnInputBlur')).toHaveLength(1);

    const prevent = (event: Event) => event.preventDefault();
    input.addEventListener('focus', prevent, { capture: true });
    input.addEventListener('blur', prevent, { capture: true });
    input.dispatchEvent(new FocusEvent('focus', { cancelable: true }));
    input.dispatchEvent(new FocusEvent('blur', { cancelable: true }));
    expect(fake.reported('OnInputFocus')).toHaveLength(1);
    expect(fake.reported('OnInputBlur')).toHaveLength(1);
  });

  // NumberFieldInput.test.tsx "consumer-prevented defaults": the field is marked focused only by the module's focus
  // report, so a consumer listener that prevents the focus default keeps it unmarked.
  it('does not mark the field focused when the focus default is prevented', () => {
    const input = document.createElement('input');
    input.type = 'text';
    document.body.appendChild(input);
    input.addEventListener('focus', (event) => event.preventDefault());
    const fake = createFakeTwin();
    handles.push(attach(input, optionsJson({ value: '1,234' }), fake.twin));

    input.dispatchEvent(new FocusEvent('focus', { cancelable: true }));

    expect(fake.reported('OnInputFocus')).toEqual([]);
  });

  it('port: a C# veto at input-focus and input-blur keeps the report back on WebAssembly', () => {
    const { input, fake } = mount({}, { synchronous: true, verdict: 'stop' });

    input.dispatchEvent(new FocusEvent('focus'));
    input.dispatchEvent(new FocusEvent('blur'));

    expect(fake.decisions.map((d) => d.point)).toEqual(['input-focus', 'input-blur']);
    expect(fake.reports).toEqual([]);

    const server = mount({}, { synchronous: false });
    server.input.dispatchEvent(new FocusEvent('focus'));
    expect(server.fake.reported('OnInputFocus')).toHaveLength(1);
  });

  it('port: dispose removes the listeners', () => {
    const { input, handle, fake } = mount();
    input.dispatchEvent(new FocusEvent('focus'));
    expect(fake.reported('OnInputFocus')).toHaveLength(1);

    handle.dispose();
    input.dispatchEvent(new FocusEvent('focus'));
    expect(fake.reported('OnInputFocus')).toHaveLength(1);
  });
});

// The input role (issue 277, 6b; ADR 04 Amendments 2026-11-18, the `number-field` scope; design points 1 (a) and 7 (a)):
// NumberFieldInput.tsx's keydown gate, change, paste and wheel handlers, the hidden input's focus redirect and autofill.
// The allowed set and the locale symbols are C#'s (decision 7 (a)); C# re-validates and parses every text it is sent.
describe('number-field module: the input role', () => {
  describe('the keydown gate', () => {
    it('port: blocks a character outside the numerals and the allowed set, and reports nothing (NUM-KEY-10)', () => {
      const { input, fake } = mount(EN_US, {}, '12');

      const event = key(input, { key: 'a' });

      expect(event.defaultPrevented).toBe(true);
      expect(fake.reports).toEqual([]);
    });

    it('port: lets every supported numeral system and the allowed set through (NUM-KEY-06, NUM-KEY-07)', () => {
      const { input } = mount(EN_US);

      for (const ch of ['7', '٧', '۷', '７', '七', '〇', ',', '．', '٫']) {
        expect(key(input, { key: ch }).defaultPrevented).toBe(false);
      }
      // A character outside them is blocked (the red check's positive half).
      expect(key(input, { key: 'a' }).defaultPrevented).toBe(true);
    });

    it('port: does not intercept navigation keys, unknown multi-character keys, modifier shortcuts or IME composition (NUM-KEY-03..05)', () => {
      const { input, fake } = mount(EN_US, {}, '12');

      for (const k of ['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'Tab', 'Enter', 'Escape', 'PageUp', 'Insert', 'F5', 'Shift']) {
        expect(key(input, { key: k }).defaultPrevented).toBe(false);
      }
      expect(key(input, { key: 'a', ctrlKey: true }).defaultPrevented).toBe(false);
      expect(key(input, { key: 'a', metaKey: true }).defaultPrevented).toBe(false);
      expect(key(input, { key: 'a', altKey: true }).defaultPrevented).toBe(false);
      expect(key(input, { key: 'a', which: 229 }).defaultPrevented).toBe(false);
      expect(fake.reports).toEqual([]);
      // A step key is intercepted and reported (the red check's positive half).
      expect(key(input, { key: 'ArrowUp' }).defaultPrevented).toBe(true);
      expect(fake.reports.map((r) => r.method)).toEqual(['OnKeyStep']);
    });

    it('port: lets a sign through only when the text has none, all of it is selected, or the selection covers the sign (NUM-KEY-08)', () => {
      const { input } = mount(EN_US, {}, '-12');

      input.setSelectionRange(3, 3);
      expect(key(input, { key: '-' }).defaultPrevented).toBe(true);
      expect(key(input, { key: '+' }).defaultPrevented).toBe(true);

      input.setSelectionRange(0, 1);
      expect(key(input, { key: '-' }).defaultPrevented).toBe(false);

      input.setSelectionRange(0, 3);
      expect(key(input, { key: '+' }).defaultPrevented).toBe(false);

      input.value = '12';
      input.setSelectionRange(2, 2);
      expect(key(input, { key: '-' }).defaultPrevented).toBe(false);
    });

    it('port: lets the decimal separator through once unless the selection covers it (NUM-KEY-09)', () => {
      const { input } = mount(EN_US, {}, '1.5');

      input.setSelectionRange(3, 3);
      expect(key(input, { key: '.' }).defaultPrevented).toBe(true);

      input.setSelectionRange(1, 2);
      expect(key(input, { key: '.' }).defaultPrevented).toBe(false);
    });

    it('port: ArrowUp and ArrowDown, with Alt or Shift too, are prevented and reported as steps with the text and the event (NUM-KEY-01)', () => {
      const { input, fake } = mount(EN_US, {}, '1.5');

      const up = key(input, { key: 'ArrowUp' });
      const down = key(input, { key: 'ArrowDown', altKey: true });

      expect(up.defaultPrevented).toBe(true);
      expect(down.defaultPrevented).toBe(true);
      expect(fake.reported<{ key: string; text: string; revision: number }>('OnKeyStep')).toEqual([
        expect.objectContaining({ key: 'ArrowUp', text: '1.5', revision: 1 }),
        expect.objectContaining({ key: 'ArrowDown', text: '1.5', revision: 2 }),
      ]);
    });

    it('port: Home and End are steps only while the bound is defined (NUM-KEY-02)', () => {
      const { input, handle, fake } = mount(gate({ min: false, max: false }));

      expect(key(input, { key: 'Home' }).defaultPrevented).toBe(false);
      expect(key(input, { key: 'End' }).defaultPrevented).toBe(false);

      handle.update(optionsJson(gate({ min: true, max: true })));
      expect(key(input, { key: 'Home' }).defaultPrevented).toBe(true);
      expect(key(input, { key: 'End' }).defaultPrevented).toBe(true);
      expect(fake.reported<{ key: string }>('OnKeyStep').map((r) => r.key)).toEqual(['Home', 'End']);
    });

    it('port: does nothing while disabled or read-only, or when a listener prevented the keydown (NUM-KEY-11, NUM-EVT-14)', () => {
      const disabled = mount(gate({ disabled: true }));
      const readOnly = mount(gate({ readOnly: true }));
      const prevented = mount(EN_US);
      prevented.input.addEventListener('keydown', (event) => event.preventDefault(), { capture: true });

      for (const { input, fake } of [disabled, readOnly]) {
        expect(key(input, { key: 'a' }).defaultPrevented).toBe(false);
        key(input, { key: 'ArrowUp' });
        expect(fake.reports).toEqual([]);
      }
      key(prevented.input, { key: 'ArrowUp' });
      expect(prevented.fake.reports).toEqual([]);
      // An enabled, editable field with no listener steps (the red check's positive half).
      const active = mount(EN_US);
      key(active.input, { key: 'ArrowUp' });
      expect(active.fake.reports.map((r) => r.method)).toEqual(['OnKeyStep']);
    });

    it('port: a C# veto at input-key keeps the key from acting on WebAssembly (design point 1 (a))', () => {
      const { input, fake } = mount(EN_US, { synchronous: true, verdict: 'stop' });

      const event = key(input, { key: 'ArrowUp' });

      expect(fake.decisions.map((d) => d.point)).toEqual(['input-key']);
      expect(event.defaultPrevented).toBe(false);
      expect(fake.reports).toEqual([]);
    });
  });

  it('port: an update without the input part keeps the last gate (the text updates of a step stay small, ADR 16 rule 7)', () => {
    const { input, handle } = mount(EN_US, {}, '12');

    handle.update(optionsJson({ value: '13', revision: 0 }));

    expect(input.value).toBe('13');
    expect(key(input, { key: 'a' }).defaultPrevented).toBe(true);
    expect(key(input, { key: ',' }).defaultPrevented).toBe(false);
  });

  describe('the change', () => {
    it('port: an allowed text is reported with a revision (NUM-EVT-15)', () => {
      const { input, fake } = mount(EN_US);

      type(input, '1,2');

      expect(fake.reported<{ text: string; revision: number }>('OnInputChange')).toEqual([expect.objectContaining({ text: '1,2', revision: 1 })]);
    });

    it('port: a text with a character outside the allowed set is reverted to the last accepted text and not reported (NUM-EDGE-06)', () => {
      const { input, fake } = mount(EN_US, {}, '12');

      type(input, '12a');

      expect(input.value).toBe('12');
      expect(fake.reports).toEqual([]);
    });

    it('port: a change whose default a listener prevented is ignored (NUM-EVT-14)', () => {
      const { input, fake } = mount(EN_US);
      input.addEventListener('input', (event) => event.preventDefault(), { capture: true });

      type(input, '5');

      expect(fake.reports).toEqual([]);
      // Without the listener the same edit is reported (the red check's positive half).
      const other = mount(EN_US);
      type(other.input, '5');
      expect(other.fake.reports.map((r) => r.method)).toEqual(['OnInputChange']);
    });
  });

  describe('paste', () => {
    it('port: prevents the paste, reports the text spliced at the selection, and restores the caret after C# echoes it (NUM-EVT-16)', () => {
      const { input, handle, fake } = mount(EN_US, {}, '123');
      input.setSelectionRange(1, 2);

      const event = paste(input, '45');

      expect(event.defaultPrevented).toBe(true);
      const [report] = fake.reported<{ text: string; revision: number }>('OnInputPaste');
      expect(report).toEqual(expect.objectContaining({ text: '1453', revision: 1 }));

      handle.update(optionsJson({ ...EN_US, value: '1453', revision: 1 }));
      expect(input.value).toBe('1453');
      expect([input.selectionStart, input.selectionEnd]).toEqual([3, 3]);
    });

    it('port: a clipboard that throws warns once, with the development checks on, and lets the browser paste (NUM-EDGE-03)', () => {
      const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
      try {
        const { input, fake } = mount(gate({ developmentChecks: true }), {}, '1');
        const throwing = () => {
          throw new DOMException('denied', 'SecurityError');
        };

        const first = paste(input, throwing);
        paste(input, throwing);

        expect(first.defaultPrevented).toBe(false);
        expect(fake.reports).toEqual([]);
        expect(warn).toHaveBeenCalledTimes(1);
        expect(String(warn.mock.calls[0]![0])).toMatch(/^Blazix: <NumberFieldInput> could not read clipboard text during paste handling\. /);
      } finally {
        warn.mockRestore();
      }
    });

    it('port: does nothing while disabled or read-only', () => {
      const { input, fake } = mount(gate({ readOnly: true }), {}, '1');

      expect(paste(input, '2').defaultPrevented).toBe(false);
      expect(fake.reports).toEqual([]);
      // An editable field takes the same paste (the red check's positive half).
      const editable = mount(EN_US, {}, '1');
      expect(paste(editable.input, '2').defaultPrevented).toBe(true);
      expect(editable.fake.reports.map((r) => r.method)).toEqual(['OnInputPaste']);
    });
  });

  describe('the wheel', () => {
    it('port: steps the focused input against the vertical delta and prevents the scroll (NUM-PTR-07)', () => {
      const { input, fake } = mount(gate({ wheel: true }));
      input.focus();

      const down = wheel(input, { deltaY: 10 });
      const up = wheel(input, { deltaY: -10 });

      expect(down.defaultPrevented).toBe(true);
      expect(up.defaultPrevented).toBe(true);
      expect(fake.reported<{ direction: number }>('OnWheelStep').map((r) => r.direction)).toEqual([-1, 1]);
    });

    it('port: ignores the wheel with Ctrl, while unfocused, without a delta, or horizontal without Shift; Shift takes the larger horizontal delta (NUM-PTR-07, NUM-PTR-08)', () => {
      const { input, fake } = mount(gate({ wheel: true }));

      expect(wheel(input, { deltaY: 10 }).defaultPrevented).toBe(false);
      input.focus();
      expect(wheel(input, { deltaY: 10, ctrlKey: true }).defaultPrevented).toBe(false);
      expect(wheel(input, { deltaY: 0 }).defaultPrevented).toBe(false);
      expect(wheel(input, { deltaX: 10, deltaY: 1 }).defaultPrevented).toBe(false);
      expect(wheel(input, { deltaX: -10, deltaY: 1, shiftKey: true }).defaultPrevented).toBe(true);
      expect(fake.reported<{ direction: number }>('OnWheelStep').map((r) => r.direction)).toEqual([1]);
    });

    it('port: the listener is attached only while the options allow the wheel (NUM-PTR-09)', () => {
      const { input, handle, fake } = mount(EN_US);
      input.focus();

      expect(wheel(input, { deltaY: 10 }).defaultPrevented).toBe(false);
      handle.update(optionsJson(gate({ wheel: true })));
      expect(wheel(input, { deltaY: 10 }).defaultPrevented).toBe(true);
      handle.update(optionsJson(gate({ wheel: false })));
      expect(wheel(input, { deltaY: 10 }).defaultPrevented).toBe(false);
      expect(fake.reported('OnWheelStep')).toHaveLength(1);
    });
  });

  describe('the hidden input', () => {
    // Design point 12 (a), chosen by the user 2026-11-19: the hidden input's `value` attribute follows its value, as React
    // keeps it (react issue 12334; NumberFieldRoot.tsx:530-532), so a field without `min` measures its step from the value.
    it('port: the hidden input\'s value attribute follows every write of its value and an autofill (NUM-DOM-06)', () => {
      const { hidden, handle } = mount(EN_US, {}, '1');
      hidden.step = '1';

      hidden.value = '54.5';
      expect(hidden.getAttribute('value')).toBe('54.5');
      expect(hidden.validity.stepMismatch).toBe(false);

      hidden.value = '';
      expect(hidden.getAttribute('value')).toBe('');

      handle.dispose();
      hidden.value = '7';
      expect(hidden.getAttribute('value')).toBe('');
    });

    it('port: focusing it moves the focus to the visible input with the caret at the end (NUM-FOC-01, NUM-FOC-02)', () => {
      const { input, hidden } = mount(EN_US, {}, '1,234');

      hidden.focus();

      expect(document.activeElement).toBe(input);
      expect([input.selectionStart, input.selectionEnd]).toEqual([5, 5]);
    });

    it('port: an autofill change is reported with valueAsNumber, or null when it is not a number (NUM-FRM-05)', () => {
      const { hidden, fake } = mount(EN_US);

      hidden.value = '42';
      hidden.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));
      hidden.value = '';
      hidden.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));

      expect(fake.reported<{ value: number | null }>('OnAutofill').map((r) => r.value)).toEqual([42, null]);
    });
  });
});


// The toolbar's disabled input item (TLB-KEY-13) and any listener attached after the module prevent a keydown on the input
// itself; Base UI's handler runs at React's root, after them (NumberFieldInput.tsx:272-275), so the step key is not acted
// on whatever the attach order (the standing rules' attach order rule; toolbar.spec.ts pins it at L4).
describe('the keydown after the input\'s own listeners', () => {
  it('port: a listener attached after the module that prevents the keydown keeps it from stepping', () => {
    const { input, fake } = mount(EN_US, {}, '5');
    input.addEventListener('keydown', (event) => event.preventDefault());

    key(input, { key: 'ArrowUp' });

    expect(fake.reports.filter((r) => r.method === 'OnKeyStep')).toEqual([]);
    // Without the listener's veto the key steps (the red check's positive half).
    const other = mount(EN_US, {}, '5');
    key(other.input, { key: 'ArrowUp' });
    expect(other.fake.reports.filter((r) => r.method === 'OnKeyStep')).toHaveLength(1);
  });
});
