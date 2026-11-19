// L3 for the `number-field` component module, 6a's minimal surface (issue 277; design point 2 (b) and 7 (a), settled by
// the orchestrator 2026-11-18; ADR 04 Amendments 2026-11-18): the visible input's `value` handover with the revision rule,
// and the focus and blur reports with their `defaultPrevented` checks and the `input-focus` / `input-blur` decision points
// (fallback run). 6b adds the gate, paste, wheel and scrub.
import { afterEach, describe, expect, it } from 'vitest';
import { attach, type NumberFieldOptions } from './number-field';
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

function mount(options: Partial<NumberFieldOptions> = {}, twin: FakeTwinOptions = {}, rendered = ''): { input: HTMLInputElement; handle: TwinHandle; fake: FakeTwin } {
  const input = document.createElement('input');
  input.type = 'text';
  input.value = rendered;
  document.body.appendChild(input);
  const fake = createFakeTwin(twin);
  const handle = attach(input, optionsJson(options), fake.twin);
  handles.push(handle);
  return { input, handle, fake };
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
