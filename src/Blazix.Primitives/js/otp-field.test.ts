// L3 (ADR 06 §1): the `otp-field` component module in jsdom against a fake twin, in its Server form and its
// `synchronous: true` (WebAssembly) form. Base UI's two module titles of OTP-STA-12 run verbatim; the rest are the port's
// titles for the paths ADR 04 Amendments 2026-11-18 and 2026-11-19 give the module (design points 1–7 (a), chosen by the
// user 2026-11-19): the keys, the same-character advance, the restore, paste, composition and the Android path, the
// `edit` handover and verdict, the optimistic advance of variant (ii) and the revision rule, focus and blur, the reset
// re-sync and the submit revision. The browser-only behaviour (real focus order, IME, clipboard) runs at L4.
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  EDIT_POINT,
  KEY_POINT,
  attach,
  setEnvironmentForTesting,
  type OtpFieldEditReport,
  type OtpFieldOptions,
} from './otp-field';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  setEnvironmentForTesting(undefined);
  vi.restoreAllMocks();
  vi.useRealTimers();
});

const ids = (length: number) => Array.from({ length }, (_, i) => (i === 0 ? 'otp' : `otp-${i + 1}`));

function options(overrides: Partial<OtpFieldOptions> = {}): OtpFieldOptions {
  const length = overrides.length ?? 6;
  return {
    slots: ids(length),
    length,
    value: '',
    revision: 0,
    activeIndex: 0,
    submitRevision: 0,
    optimistic: true,
    slotPattern: '\\d{1}',
    ...overrides,
  };
}

/** Six slots, the length every title renders, typed so each index is a slot. */
type Slots = [HTMLInputElement, HTMLInputElement, HTMLInputElement, HTMLInputElement, HTMLInputElement, HTMLInputElement];

interface Field {
  root: HTMLDivElement;
  slots: Slots;
  hidden: HTMLInputElement;
  form: HTMLFormElement;
}

/** Renders what `OtpFieldRoot` renders: the group, its slots with their first values, the hidden input after it. */
function render(length: number, value: string): Field {
  const form = document.createElement('form');
  const root = document.createElement('div');
  root.setAttribute('role', 'group');
  const slots = ids(length).map((id, i) => {
    const slot = document.createElement('input');
    slot.id = id;
    slot.value = value[i] ?? '';
    root.append(slot);
    return slot;
  });
  const hidden = document.createElement('input');
  hidden.setAttribute('aria-hidden', 'true');
  hidden.value = value;
  form.append(root, hidden);
  document.body.append(form);
  return { root, slots: slots as Slots, hidden, form };
}

interface Attached extends Field {
  fake: FakeTwin;
  handle: TwinHandle;
  handovers: OtpFieldEditReport[];
  update(overrides: Partial<OtpFieldOptions>): void;
}

/**
 * Attaches the module. `onDecide` runs inside a synchronous `edit` decision, as C#'s chain does on WebAssembly when its
 * commit renders and the update reaches the module before the handler returns.
 */
function mount(
  overrides: Partial<OtpFieldOptions> = {},
  twin: { synchronous?: boolean; verdict?: 'run' | 'stop'; onDecide?: (point: string, attached: Attached) => void } = {},
): Attached {
  const opts = options(overrides);
  const field = render(opts.length, opts.value);
  const fake = createFakeTwin({ synchronous: twin.synchronous ?? false, verdict: twin.verdict ?? 'run' });
  const handovers: OtpFieldEditReport[] = [];
  const invoke = fake.twin.invokeMethod.bind(fake.twin);
  let current = opts;
  const attached = {} as Attached;
  fake.twin.invokeMethod = <T,>(method: string, ...args: unknown[]): T => {
    if (method === 'OnEditHandover' && twin.synchronous) {
      handovers.push(JSON.parse(args[0] as string) as OtpFieldEditReport);
      return undefined as T;
    }
    const result = invoke<T>(method, ...args);
    if (method === 'Decide') {
      twin.onDecide?.(args[0] as string, attached);
    }
    return result;
  };
  const handle = attach(field.root, JSON.stringify(opts), fake.twin);
  handles.push(handle);
  Object.assign(attached, field, {
    fake,
    handle,
    handovers,
    update(next: Partial<OtpFieldOptions>) {
      current = { ...current, ...next };
      handle.update(JSON.stringify(current));
    },
  });
  return attached;
}

function key(target: HTMLElement, keyName: string, init: KeyboardEventInit = {}): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key: keyName, bubbles: true, cancelable: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function type(slot: HTMLInputElement, text: string): Event {
  slot.value = text;
  const event = new Event('input', { bubbles: true });
  slot.dispatchEvent(event);
  return event;
}

function paste(slot: HTMLInputElement, getData: () => string): Event {
  const event = new Event('paste', { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'clipboardData', { value: { getData } });
  slot.dispatchEvent(event);
  return event;
}

const edits = (fake: FakeTwin) => fake.reported<OtpFieldEditReport>('OnEdit');

describe('otp-field: keyboard navigation (OTP-STA-12, OTP-KEY-02..08)', () => {
  it('moves focus to the empty end slot with End', () => {
    const { slots } = mount({ value: '12' });
    slots[0].focus();
    key(slots[0], 'End');
    expect(document.activeElement).toBe(slots[2]);
  });

  it('keeps focus on the final slot with ArrowDown when the value is complete', () => {
    const { slots } = mount({ value: '123456' });
    slots[2].focus();
    key(slots[2], 'ArrowDown');
    expect(document.activeElement).toBe(slots[5]);
  });

  it('port: the arrows move one slot, Ctrl+ArrowRight goes to the end target and Home to the first slot, each stopped', () => {
    const { slots } = mount({ value: '1234' });
    slots[1].focus();
    const right = key(slots[1], 'ArrowRight');
    expect(document.activeElement).toBe(slots[2]);
    expect(right.defaultPrevented).toBe(true);
    key(slots[2], 'ArrowLeft');
    expect(document.activeElement).toBe(slots[1]);
    key(slots[1], 'ArrowRight', { ctrlKey: true });
    expect(document.activeElement).toBe(slots[4]);
    key(slots[4], 'Home');
    expect(document.activeElement).toBe(slots[0]);
  });

  it('port: under rtl ArrowLeft moves to the next slot and ArrowRight to the previous one (OTP-I18N-01)', () => {
    const { slots } = mount({ value: '1234', rtl: true });
    slots[1].focus();
    key(slots[1], 'ArrowLeft');
    expect(document.activeElement).toBe(slots[2]);
    key(slots[2], 'ArrowRight');
    expect(document.activeElement).toBe(slots[1]);
  });

  it('port: a fully selected slot retyped with its own character moves focus on, Ctrl held or not (OTP-KEY-09, §16 item 5)', () => {
    const { slots, fake } = mount({ value: 'aab', slotPattern: '[a-zA-Z]{1}' });
    slots[0].focus();
    const event = key(slots[0], 'a', { ctrlKey: true });
    expect(document.activeElement).toBe(slots[1]);
    expect(event.defaultPrevented).toBe(true);
    expect(edits(fake)).toEqual([]);
  });

  it('port: on WebAssembly a navigation key asks `key` when C# lists it, and a stop verdict leaves focus in place', () => {
    const { slots, fake } = mount({ value: '1234', points: [KEY_POINT] }, { synchronous: true, verdict: 'stop' });
    slots[1].focus();
    const event = key(slots[1], 'ArrowRight');
    expect(fake.decisions.map((d) => d.point)).toEqual([KEY_POINT]);
    expect(document.activeElement).toBe(slots[1]);
    expect(event.defaultPrevented).toBe(false);
  });

  it('port: a disabled field ignores keys and presses', () => {
    const { slots } = mount({ value: '1234', disabled: true });
    slots[1].focus();
    const event = key(slots[1], 'ArrowRight');
    const press = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    slots[3].dispatchEvent(press);
    expect(document.activeElement).toBe(slots[1]);
    expect([event.defaultPrevented, press.defaultPrevented]).toEqual([false, false]);
  });
});

describe('otp-field: editing keys (OTP-KEY-10..12, OTP-KEY-14..16)', () => {
  it('port: Backspace, Delete and Ctrl+Backspace each report one edit of their kind and are stopped', () => {
    const { slots, fake } = mount({ value: '123' });
    slots[2].focus();
    const backspace = key(slots[2], 'Backspace');
    const del = key(slots[2], 'Delete');
    const clear = key(slots[2], 'Backspace', { ctrlKey: true });
    expect(edits(fake).map((e) => [e.index, e.kind, e.raw, e.revision])).toEqual([
      [2, 'backspace', '', 1],
      [2, 'delete', '', 2],
      [2, 'clear', '', 3],
    ]);
    expect([backspace, del, clear].map((e) => e.defaultPrevented)).toEqual([true, true, true]);
  });

  it('port: a read-only field moves focus with the arrows and reports no editing key', () => {
    const { slots, fake } = mount({ value: '123', readOnly: true });
    slots[1].focus();
    key(slots[1], 'Backspace');
    key(slots[1], 'ArrowRight');
    expect(document.activeElement).toBe(slots[2]);
    expect(edits(fake)).toEqual([]);
  });
});

describe('otp-field: typing and the revision rule (variant (ii), decision 6 (a); design points 3 and 4 (a))', () => {
  it('port: on Server a digit the slot pattern accepts is written, focus advances and the edit is reported advanced', () => {
    const { slots, fake } = mount({ value: '' });
    slots[0].focus();
    type(slots[0], '7');
    expect(document.activeElement).toBe(slots[1]);
    expect(slots[0].value).toBe('7');
    expect(edits(fake).map((e) => [e.index, e.kind, e.raw, e.advanced, e.revision])).toEqual([[0, 'input', '7', true, 1]]);
  });

  it('port: a character the slot pattern rejects is reported unadvanced and the update restores the empty slot (OTP-KEY-17)', () => {
    const attached = mount({ value: '' });
    const { slots, fake } = attached;
    slots[0].focus();
    type(slots[0], 'x');
    expect(document.activeElement).toBe(slots[0]);
    expect(edits(fake).map((e) => [e.raw, e.advanced])).toEqual([['x', false]]);
    attached.update({ value: '', revision: 1 });
    expect(slots[0].value).toBe('');
  });

  it('port: a focused slot the update restores stays selected (OTP-KEY-17, "keeps the filled slot selected when typing an invalid character")', () => {
    const attached = mount({ value: '1' });
    const { slots } = attached;
    slots[0].focus();
    type(slots[0], 'a');
    attached.update({ value: '1', revision: 1 });
    expect([slots[0].value, slots[0].selectionStart, slots[0].selectionEnd]).toEqual(['1', 0, 1]);
    expect(document.activeElement).toBe(slots[0]);
  });

  it('port: a field without the optimistic advance keeps focus until the update, whose focus request runs once per revision (OTP-FOC-10)', () => {
    const attached = mount({ value: '', optimistic: false });
    const { slots } = attached;
    slots[0].focus();
    type(slots[0], '4');
    expect(document.activeElement).toBe(slots[0]);
    attached.update({ value: '4', revision: 1, focus: { index: 1, revision: 1 } });
    expect(document.activeElement).toBe(slots[1]);
    slots[0].focus();
    attached.update({ value: '4', revision: 1, focus: { index: 1, revision: 1 }, activeIndex: 1 });
    expect(document.activeElement).toBe(slots[0]);
  });

  it('port: an update older than the last act is ignored, so two quick digits do not snap back', () => {
    const attached = mount({ value: '' });
    const { slots } = attached;
    slots[0].focus();
    type(slots[0], '1');
    type(slots[1], '2');
    attached.update({ value: '1', revision: 1 });
    expect([slots[0].value, slots[1].value]).toEqual(['1', '2']);
    expect(document.activeElement).toBe(slots[2]);
    attached.update({ value: '12', revision: 2 });
    expect([slots[0].value, slots[1].value]).toEqual(['1', '2']);
  });

  it('port: a rollback is applied even when older, returning focus to the cancelled slot (design point 4 (a))', () => {
    const attached = mount({ value: '' });
    const { slots } = attached;
    slots[0].focus();
    type(slots[0], '1');
    type(slots[1], '2');
    attached.update({ value: '1', revision: 1, rollback: true, focus: { index: 0, revision: 1 } });
    expect(document.activeElement).toBe(slots[0]);
    expect([slots[0].value, slots[1].value]).toEqual(['1', '']);
  });

  it('port: on WebAssembly an edit is handed over before `edit` is asked, not reported, and the update inside the decision writes the slots', () => {
    const { slots, fake, handovers } = mount(
      { value: '' },
      {
        synchronous: true,
        onDecide: (point, attached) => {
          if (point === EDIT_POINT) {
            attached.update({ value: '12', revision: 1, focus: { index: 2, revision: 1 } });
          }
        },
      },
    );
    slots[0].focus();
    type(slots[0], '12');
    expect(handovers.map((e) => [e.index, e.kind, e.raw, e.advanced, e.revision])).toEqual([[0, 'input', '12', false, 1]]);
    expect(fake.decisions.map((d) => d.point)).toEqual([EDIT_POINT]);
    expect(edits(fake)).toEqual([]);
    expect([slots[0].value, slots[1].value]).toEqual(['1', '2']);
    expect(document.activeElement).toBe(slots[2]);
  });

  it('port: on WebAssembly a vetoed edit restores the slot and moves nothing (OTP-KEY-13)', () => {
    const { slots, fake } = mount({ value: '5' }, { synchronous: true, verdict: 'stop' });
    slots[0].focus();
    type(slots[0], '9');
    expect(slots[0].value).toBe('5');
    expect(document.activeElement).toBe(slots[0]);
    expect(edits(fake)).toEqual([]);
  });

  it('port: on WebAssembly a vetoed edit does not hold back the next update', () => {
    const attached = mount({ value: '5' }, { synchronous: true, verdict: 'stop' });
    const { slots } = attached;
    slots[0].focus();
    type(slots[0], '9');
    attached.update({ value: '7', revision: 0 });
    expect(slots[0].value).toBe('7');
  });

  it('port: on WebAssembly a vetoed Backspace is not prevented', () => {
    const { slots } = mount({ value: '5' }, { synchronous: true, verdict: 'stop' });
    slots[0].focus();
    expect(key(slots[0], 'Backspace').defaultPrevented).toBe(false);
  });
});

describe('otp-field: paste and composition (OTP-KEY-18..23, OTP-EDGE-05)', () => {
  it('port: a paste reports the clipboard text as one edit and prevents the native paste', () => {
    const { slots, fake } = mount({ value: '' });
    slots[0].focus();
    const event = paste(slots[0], () => '12 34');
    expect(event.defaultPrevented).toBe(true);
    expect(edits(fake).map((e) => [e.index, e.kind, e.raw, e.advanced])).toEqual([[0, 'paste', '12 34', false]]);
  });

  it('port: an unreadable paste is abandoned and reported only while the development checks are on', () => {
    const { slots, fake } = mount({ value: '', developmentChecks: true });
    slots[0].focus();
    const event = paste(slots[0], () => {
      throw new Error('denied');
    });
    expect(event.defaultPrevented).toBe(false);
    expect(fake.reported('OnPasteUnreadable')).toEqual([{ index: 0 }]);
    expect(edits(fake)).toEqual([]);
  });

  it('port: text typed during a composition is buffered and committed once on compositionend', () => {
    const { slots, fake } = mount({ value: '', optimistic: false });
    slots[0].focus();
    slots[0].dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
    type(slots[0], 'd');
    type(slots[0], 'dd');
    const backspace = key(slots[0], 'Backspace');
    expect(edits(fake)).toEqual([]);
    expect(backspace.defaultPrevented).toBe(false);
    slots[0].value = '3';
    slots[0].dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }));
    expect(edits(fake).map((e) => [e.kind, e.raw])).toEqual([['input', '3']]);
  });

  it('port: on WebAssembly the update a committed composition gets inside the decision rewrites the composed slot', () => {
    const { slots } = mount(
      { value: '', optimistic: false, slotPattern: '[a-zA-Z0-9]{1}' },
      {
        synchronous: true,
        onDecide: (point, attached) => {
          if (point === EDIT_POINT) {
            attached.update({ value: 'ddd', revision: 1, focus: { index: 3, revision: 1 } });
          }
        },
      },
    );
    slots[0].focus();
    slots[0].dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
    type(slots[0], 'ddd');
    slots[0].dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }));
    expect(slots.map((s) => s.value).join('')).toBe('ddd');
    expect(document.activeElement).toBe(slots[3]);
  });

  it('port: a composition that ends after the field became read-only commits nothing and restores the slot', () => {
    const attached = mount({ value: '', optimistic: false });
    const { slots, fake } = attached;
    slots[0].focus();
    slots[0].dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
    type(slots[0], 'abc');
    attached.update({ readOnly: true });
    slots[0].dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }));
    expect(edits(fake)).toEqual([]);
    expect(slots[0].value).toBe('');
  });

  it('port: on Android every input during a composition is committed (OTP-KEY-22, OTP-KEY-23)', () => {
    setEnvironmentForTesting({ android: true });
    const { slots, fake } = mount({ value: '', optimistic: false });
    slots[0].focus();
    slots[0].dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
    type(slots[0], '1');
    slots[0].dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }));
    expect(edits(fake).map((e) => e.raw)).toEqual(['1']);
  });
});

describe('otp-field: pointer and focus (OTP-PTR-01, OTP-FOC-01..07)', () => {
  it('port: a mousedown is prevented and focuses its slot, and a slot past the value focuses the end target (OTP-FOC-03)', () => {
    const { slots } = mount({ value: '12' });
    const press = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    slots[1].dispatchEvent(press);
    expect(press.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(slots[1]);
    slots[5].dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(slots[2]);
  });

  it('port: a focused slot is selected and becomes the only tab stop', () => {
    const { slots } = mount({ value: '1234' });
    slots[2].focus();
    expect([slots[2].selectionStart, slots[2].selectionEnd]).toEqual([0, 1]);
    expect(slots.map((s) => s.tabIndex)).toEqual([-1, -1, 0, -1, -1, -1]);
  });

  it('port: focus is reported once when it enters the root and blur once when it leaves', () => {
    const { slots, fake } = mount({ value: '1234' });
    const outside = document.createElement('button');
    document.body.append(outside);
    slots[0].focus();
    slots[1].focus();
    slots[2].focus();
    outside.focus();
    expect(fake.reports.map((r) => r.method)).toEqual(['OnFocus', 'OnBlur']);
    expect(fake.reported<{ index: number; outside: boolean }>('OnBlur').map((r) => [r.index, r.outside])).toEqual([[2, true]]);
    expect(slots.map((s) => s.tabIndex)).toEqual([-1, -1, -1, -1, 0, -1]);
  });
});

describe('otp-field: form (OTP-FRM-14, OTP-EVT-13)', () => {
  it('port: a form reset writes the slots and the hidden input back from the value (§16 item 9)', async () => {
    const { slots, hidden, form } = mount({ value: '123' });
    form.reset();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(slots.slice(0, 3).map((s) => s.value)).toEqual(['1', '2', '3']);
    expect(hidden.value).toBe('123');
  });

  it('port: each increase of the submit revision submits the owning form once, and attach submits nothing', () => {
    const attached = mount({ value: '123456', submitRevision: 1 });
    const submit = vi.spyOn(attached.form, 'requestSubmit').mockImplementation(() => {});
    attached.update({ submitRevision: 1, revision: 0 });
    expect(submit).not.toHaveBeenCalled();
    attached.update({ submitRevision: 2 });
    expect(submit).toHaveBeenCalledTimes(1);
  });

  it('port: the `form` id names the submitted form when it is a form', () => {
    const attached = mount({ value: '123456', form: 'other' });
    const other = document.createElement('form');
    other.id = 'other';
    document.body.append(other);
    const own = vi.spyOn(attached.form, 'requestSubmit').mockImplementation(() => {});
    const named = vi.spyOn(other, 'requestSubmit').mockImplementation(() => {});
    attached.update({ submitRevision: 1 });
    expect([own.mock.calls.length, named.mock.calls.length]).toEqual([0, 1]);
  });
});
