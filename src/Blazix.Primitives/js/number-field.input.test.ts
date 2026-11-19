// L3 for the `number-field` module's input role (issue 277, 6b): NumberFieldInput.test.tsx's titles the module alone
// decides, the paste warning and the consumer-prevented defaults. A Blazor consumer's handler runs after the module's
// listener (Blazor delegates events at the root), so a default prevented before the module sees the event comes from a
// script listener on the input, which these tests add before the module attaches, as Base UI's consumer handler runs
// before its own. What C# does with a report is L2's; here the report is withheld.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { attach, type NumberFieldInputOptions, type NumberFieldOptions } from './number-field';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
});

// en-US with no format: the allowed set and the locale's symbols C# sends at attach.
const GATE: NumberFieldInputOptions = { allowed: '.,．，٫٬+＋﹢-−－‒–—﹣', decimal: '.', currency: '', percent: '', developmentChecks: true };

function mount(rendered: string, listen?: (input: HTMLInputElement) => void): { input: HTMLInputElement; fake: FakeTwin } {
  const input = document.createElement('input');
  input.type = 'text';
  input.value = rendered;
  document.body.append(input);
  listen?.(input);
  const fake = createFakeTwin();
  const options: NumberFieldOptions = { value: rendered, revision: 0, input: GATE };
  handles.push(attach(input, JSON.stringify(options), fake.twin));
  return { input, fake };
}

const prevent = (event: Event) => event.preventDefault();

describe('<NumberFieldInput /> in the number-field module', () => {
  it('warns in development when clipboard text cannot be read during paste handling', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    try {
      const { input, fake } = mount('12');
      input.focus();

      const pasteEvent = new Event('paste', { bubbles: true, cancelable: true });
      Object.defineProperty(pasteEvent, 'clipboardData', {
        value: {
          getData() {
            throw new DOMException('Blocked', 'SecurityError');
          },
        },
      });
      input.dispatchEvent(pasteEvent);

      expect(input.value).toBe('12');
      expect(pasteEvent.defaultPrevented).toBe(false);
      expect(fake.reported('OnInputPaste')).toEqual([]);
      expect(warn).toHaveBeenCalledTimes(1);
      expect(String(warn.mock.calls[0]?.[0])).toContain('Blazix: <NumberFieldInput> could not read clipboard text during paste handling.');
    } finally {
      warn.mockRestore();
    }
  });

  describe('consumer-prevented defaults', () => {
    it('does not reformat, touch, or commit on blur when the blur default is prevented', () => {
      const { input, fake } = mount('', (el) => el.addEventListener('blur', prevent));
      input.focus();

      input.value = '5.10';
      input.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));
      expect(fake.reported<{ text: string }>('OnInputChange').map((r) => r.text)).toEqual(['5.10']);

      // The blur is not reported, so C# neither marks the field touched nor commits nor sends a reformatted text.
      input.dispatchEvent(new FocusEvent('blur', { cancelable: true }));

      expect(input.value).toBe('5.10');
      expect(fake.reported('OnInputBlur')).toEqual([]);
    });

    it('ignores a change whose native default was already prevented', () => {
      const { input, fake } = mount('5', (el) => el.addEventListener('input', prevent));
      input.focus();

      input.value = '9';
      input.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));

      // Base UI's input is controlled: the ignored change leaves the text it rendered, 5.
      expect(input.value).toBe('5');
      expect(fake.reported('OnInputChange')).toEqual([]);
    });

    it('does not step when the keydown default is prevented', () => {
      const unprevented = mount('5');
      const { input, fake } = mount('5', (el) => el.addEventListener('keydown', prevent));

      unprevented.input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true, cancelable: true }));
      expect(unprevented.fake.reported<{ key: string }>('OnKeyStep').map((r) => r.key)).toEqual(['ArrowUp']);

      input.focus();
      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true, cancelable: true }));

      expect(input.value).toBe('5');
      expect(fake.reported('OnKeyStep')).toEqual([]);
    });
  });
});
