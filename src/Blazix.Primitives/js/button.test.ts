// L3 (ADR 06 §1): the `button` module in jsdom against a fake twin. Titles are Base UI's where a Base UI test observes
// what this module does (useButton.test.tsx, Button.test.tsx, NumberFieldIncrement.test.tsx, NumberFieldRoot.test.tsx);
// the port's own titles cover the rows Base UI tests through components or not at all (button-behavior.md, UBT).
// A consumer's `onKeyDown`/`onClick` spies become element listeners: C# handler gating is the part's job (L2/L4).
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  attach,
  dispatchClickWithModifiers,
  findHiddenInput,
  isElementDisabled,
  isTouchLikePointerType,
  type ButtonOptions,
} from './button';
import type { EventInfo } from './event-info';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin, type FakeTwinOptions } from './test-support/fake-twin';

const handles: TwinHandle[] = [];

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  document.body.replaceChildren();
  vi.useRealTimers();
});

function optionsJson(overrides: Partial<ButtonOptions> = {}): string {
  const options: ButtonOptions = { disabled: false, native: true, composite: false, ...overrides };
  return JSON.stringify(options);
}

interface Mounted<E extends HTMLElement> {
  element: E;
  handle: TwinHandle;
  fake: FakeTwin;
  onClick: ReturnType<typeof vi.fn>;
}

/**
 * Creates the element, lets `before` add consumer listeners that run ahead of the module, attaches the module and
 * counts the clicks that reach the element.
 */
function mount<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  options: Partial<ButtonOptions> = {},
  {
    before,
    parent = document.body,
    twin = {},
  }: { before?: (element: HTMLElementTagNameMap[K]) => void; parent?: HTMLElement; twin?: FakeTwinOptions } = {},
): Mounted<HTMLElementTagNameMap[K]> {
  const element = document.createElement(tag);
  parent.appendChild(element);
  before?.(element);
  const fake = createFakeTwin(twin);
  const handle = attach(element, optionsJson(options), fake.twin);
  handles.push(handle);
  const onClick = vi.fn();
  element.addEventListener('click', onClick);
  return { element, handle, fake, onClick };
}

function keyDown(target: EventTarget, key: string, init: KeyboardEventInit = {}): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, composed: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function keyUp(target: EventTarget, key: string, init: KeyboardEventInit = {}): KeyboardEvent {
  const event = new KeyboardEvent('keyup', { key, bubbles: true, cancelable: true, composed: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function press(target: EventTarget, key: string, init: KeyboardEventInit = {}): void {
  keyDown(target, key, init);
  keyUp(target, key, init);
}

function pointer(target: EventTarget, type: string, init: PointerEventInit = {}): PointerEvent {
  const event = new PointerEvent(type, { bubbles: true, cancelable: true, composed: true, button: 0, ...init });
  target.dispatchEvent(event);
  return event;
}

function mouse(target: EventTarget, type: string, init: MouseEventInit = {}): MouseEvent {
  const event = new MouseEvent(type, { bubbles: type !== 'mouseenter' && type !== 'mouseleave', cancelable: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function click(target: EventTarget, init: MouseEventInit = {}): MouseEvent {
  return mouse(target, 'click', { detail: 1, ...init });
}

function touch(target: EventTarget, type: 'touchstart' | 'touchend'): void {
  target.dispatchEvent(new Event(type, { bubbles: true, cancelable: true }));
}

const SPACE = ' ';

describe('useButton', () => {
  describe('non-native button', () => {
    describe('keyboard interactions', () => {
      it.each([{ key: 'Enter' }, { key: 'Space' }])('can be activated with $key key', ({ key }) => {
        const { element, onClick } = mount('span', { native: false });

        press(element, key === 'Space' ? SPACE : key);

        expect(onClick).toHaveBeenCalledTimes(1);
      });

      it('can be activated with Enter when the keyboard event originates inside a shadow root', () => {
        const { element, onClick } = mount('span', { native: false });
        const inner = document.createElement('span');
        inner.tabIndex = 0;
        element.attachShadow({ mode: 'open' }).appendChild(inner);

        keyDown(inner, 'Enter');

        expect(onClick).toHaveBeenCalledTimes(1);
      });
    });
  });

  describe('param: focusableWhenDisabled', () => {
    it('force overrides disabled attribute when put in a composite', () => {
      const options = { disabled: true, focusableWhenDisabled: true, composite: true };
      const first = mount('button', options, { before: (b) => (b.disabled = true) });

      expect(first.element.disabled).toBe(false);

      // Ensure it works after ref change
      const second = mount('button', options, { before: (b) => (b.disabled = true) });

      expect(second.element.disabled).toBe(false);
    });

    it('prevents interactions except focus and blur', () => {
      const { element, onClick } = mount('span', { native: false, disabled: true, focusableWhenDisabled: true });

      expect(keyDown(element, 'Enter').defaultPrevented).toBe(true);
      keyUp(element, 'Enter');
      expect(onClick).toHaveBeenCalledTimes(0);

      press(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(0);

      expect(click(element).defaultPrevented).toBe(true);

      // Tab is never prevented, so the disabled element can be left with the keyboard.
      expect(keyDown(element, 'Tab').defaultPrevented).toBe(false);
    });
  });

  describe('event handlers', () => {
    it('key: Space fires keyup then click on non-composite buttons', () => {
      const { element, onClick } = mount('span', { native: false });

      expect(keyDown(element, SPACE).defaultPrevented).toBe(true);
      expect(onClick).toHaveBeenCalledTimes(0);

      keyUp(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('key: Space fires keydown then click on composite buttons', () => {
      const { element, onClick } = mount('span', { native: false, composite: true });

      keyDown(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);

      keyUp(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('key: Space fires keydown then click on composite links', () => {
      const { element: link, onClick } = mount('a', { native: false, composite: true }, { before: (a) => (a.href = '#test') });

      keyDown(link, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);

      keyUp(link, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('does not click composite links when Space is prevented for text navigation', () => {
      const { element: link, onClick } = mount(
        'a',
        { native: false, composite: true },
        {
          before: (a) => {
            a.href = '#test';
            a.setAttribute('role', 'menuitem');
            a.addEventListener('keydown', (event) => event.preventDefault());
          },
        },
      );

      keyDown(link, SPACE);
      expect(onClick).toHaveBeenCalledTimes(0);
    });

    it('does not click composite gridcells when Space is prevented', () => {
      const { element: gridcell, onClick } = mount(
        'div',
        { native: false, composite: true },
        {
          before: (div) => {
            div.setAttribute('role', 'gridcell');
            div.addEventListener('keydown', (event) => event.preventDefault());
          },
        },
      );

      keyDown(gridcell, SPACE);
      expect(onClick).toHaveBeenCalledTimes(0);
    });

    it('clicks composite switches when Space is prevented', () => {
      const { element: switchElement, onClick } = mount(
        'div',
        { native: false, composite: true },
        {
          before: (div) => {
            div.setAttribute('role', 'switch');
            div.addEventListener('keydown', (event) => event.preventDefault());
          },
        },
      );

      keyDown(switchElement, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('key: Space fires keydown then click on native composite buttons', () => {
      const { element, onClick } = mount('button', { composite: true });

      keyDown(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);

      keyUp(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('does not fire duplicate clicks for Space on native composite buttons', () => {
      const { element, onClick } = mount('button', { composite: true });

      keyDown(element, SPACE);
      // jsdom has no native keyup activation; preventing the keyup is what stops the browser's own click (L4).
      expect(keyUp(element, SPACE).defaultPrevented).toBe(true);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('fires a single click for nested non-native composite buttons', () => {
      const { element, onClick } = mount('span', { native: false, composite: true });
      handles.push(attach(element, optionsJson({ native: false, composite: true }), createFakeTwin().twin));

      press(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);

      press(element, 'Enter');
      expect(onClick).toHaveBeenCalledTimes(2);
    });

    it('key: Space preserves native submit semantics on composite buttons', () => {
      const form = document.createElement('form');
      document.body.appendChild(form);
      const handleSubmit = vi.fn((event: Event) => event.preventDefault());
      form.addEventListener('submit', handleSubmit);
      const { element: button } = mount('button', { composite: true }, { parent: form, before: (b) => (b.type = 'submit') });

      keyDown(button, SPACE);
      expect(handleSubmit).toHaveBeenCalledTimes(1);

      keyUp(button, SPACE);
      expect(handleSubmit).toHaveBeenCalledTimes(1);
    });

    it('key: Space preserves native reset semantics on composite buttons', () => {
      const form = document.createElement('form');
      document.body.appendChild(form);
      const handleReset = vi.fn((event: Event) => event.preventDefault());
      form.addEventListener('reset', handleReset);
      const { element: button } = mount('button', { composite: true }, { parent: form, before: (b) => (b.type = 'reset') });

      keyDown(button, SPACE);
      expect(handleReset).toHaveBeenCalledTimes(1);

      keyUp(button, SPACE);
      expect(handleReset).toHaveBeenCalledTimes(1);
    });

    // A C# consumer's PreventPartHandler() reaches the module as the verdict of the `activate` decision point.
    it('does not click composite buttons when keydown calls preventBaseUIHandler', () => {
      const { element, onClick, fake } = mount(
        'span',
        { native: false, composite: true },
        { twin: { synchronous: true, verdict: 'stop' } },
      );

      const event = keyDown(element, SPACE);

      expect(onClick).toHaveBeenCalledTimes(0);
      expect(event.defaultPrevented).toBe(false);
      expect(fake.decisions).toEqual([{ point: 'activate', event: expect.objectContaining({ type: 'keydown', key: SPACE }) }]);
    });

    it('does not click non-composite buttons when keydown/keyup calls preventBaseUIHandler', () => {
      const { element, onClick, fake } = mount('span', { native: false }, { twin: { synchronous: true, verdict: 'stop' } });

      // Enter activates on keydown; the consumer prevents it.
      keyDown(element, 'Enter');
      expect(onClick).toHaveBeenCalledTimes(0);

      // Space activates on keyup; the consumer prevents it.
      keyDown(element, SPACE);
      keyUp(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(0);
      expect(fake.decisions.map((d) => d.event.type)).toEqual(['keydown', 'keydown', 'keyup']);
    });

    it('key: Enter does not click non-native buttons when keydown calls preventDefault', () => {
      const { element, onClick } = mount(
        'span',
        { native: false },
        { before: (span) => span.addEventListener('keydown', (event) => event.preventDefault()) },
      );

      // Match native buttons: preventing the keydown's default cancels Enter activation.
      press(element, 'Enter');
      expect(onClick).toHaveBeenCalledTimes(0);
    });

    it('key: Space does not click non-native buttons when keyup calls preventDefault', () => {
      const { element, onClick } = mount(
        'span',
        { native: false },
        { before: (span) => span.addEventListener('keyup', (event) => event.preventDefault()) },
      );

      // Match native buttons: preventing the keyup's default cancels Space activation.
      press(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(0);
    });

    // The part resolves composite mode from its ancestors and passes it in the options.
    it('key: Space fires keydown then click when in composite root context', () => {
      const { element, onClick } = mount('span', { native: false, composite: true });

      keyDown(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);

      keyUp(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('key: Space fires keydown then click on native buttons in composite root context', () => {
      const { element, onClick } = mount('button', { composite: true });

      keyDown(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);

      keyUp(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('`composite=false` keeps keyup activation inside composite root context', () => {
      const { element, onClick } = mount('span', { native: false, composite: false });

      keyDown(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(0);

      keyUp(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('key: Enter fires keydown then click on non-native buttons', () => {
      const { element, onClick } = mount('span', { native: false });

      keyDown(element, 'Enter');
      expect(onClick).toHaveBeenCalledTimes(1);
    });
  });
});

describe('Button', () => {
  it('custom element: applies button semantics and dispatches real clicks from keyboard activation', () => {
    const ancestor = document.createElement('div');
    document.body.appendChild(ancestor);
    const handleAncestorClick = vi.fn();
    const handleCaptureClick = vi.fn();
    ancestor.addEventListener('click', handleAncestorClick);
    ancestor.addEventListener('click', handleCaptureClick, { capture: true });
    const handleRenderClick = vi.fn();
    const { element: button, onClick: handleClick } = mount(
      'span',
      { native: false },
      { parent: ancestor, before: (span) => span.addEventListener('click', handleRenderClick) },
    );

    press(button, 'Enter');
    press(button, SPACE);

    expect(handleCaptureClick).toHaveBeenCalledTimes(2);
    expect(handleRenderClick).toHaveBeenCalledTimes(2);
    expect(handleClick).toHaveBeenCalledTimes(2);
    expect(handleAncestorClick).toHaveBeenCalledTimes(2);
  });

  it('custom element: keyboard activation clicks carry modifier key state', () => {
    const { element: button, onClick: handleClick } = mount('span', { native: false });

    press(button, 'Enter', { shiftKey: true });

    expect(handleClick).toHaveBeenCalledTimes(1);
    expect((handleClick.mock.calls[0]![0] as MouseEvent).shiftKey).toBe(true);
  });
});

describe('button module: keyboard activation', () => {
  it('leaves keys other than Enter and Space alone', () => {
    const { element, onClick } = mount('span', { native: false });

    for (const key of ['ArrowDown', 'a', 'Escape', 'Tab']) {
      expect(keyDown(element, key).defaultPrevented).toBe(false);
      expect(keyUp(element, key).defaultPrevented).toBe(false);
    }
    expect(onClick).toHaveBeenCalledTimes(0);
  });

  it('does not intercept keys pressed on a focusable descendant', () => {
    const { element, onClick } = mount('span', { native: false });
    const child = document.createElement('input');
    element.appendChild(child);

    expect(keyDown(child, 'Enter').defaultPrevented).toBe(false);
    press(child, SPACE);
    expect(onClick).toHaveBeenCalledTimes(0);
  });

  it('leaves Enter and Space to the browser on a native button outside a composite', () => {
    const { element, onClick } = mount('button');

    expect(keyDown(element, 'Enter').defaultPrevented).toBe(false);
    expect(keyDown(element, SPACE).defaultPrevented).toBe(false);
    expect(keyUp(element, SPACE).defaultPrevented).toBe(false);
    expect(onClick).toHaveBeenCalledTimes(0);
  });

  it('leaves Enter to the browser on a non-native link', () => {
    const { element: link, onClick } = mount('a', { native: false }, { before: (a) => (a.href = '#test') });

    expect(keyDown(link, 'Enter').defaultPrevented).toBe(false);
    expect(onClick).toHaveBeenCalledTimes(0);
  });

  it('activates a non-native link with Space on keyup and prevents the keydown scroll', () => {
    const { element: link, onClick } = mount('a', { native: false }, { before: (a) => (a.href = '#test') });

    expect(keyDown(link, SPACE).defaultPrevented).toBe(true);
    expect(onClick).toHaveBeenCalledTimes(0);

    keyUp(link, SPACE);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('treats an anchor without href as a non-native button', () => {
    const { element, onClick } = mount('a', { native: false });

    keyDown(element, 'Enter');
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('prevents Space but never clicks a native-mode composite element that is not a button', () => {
    const { element, onClick } = mount('span', { native: true, composite: true });

    expect(keyDown(element, SPACE).defaultPrevented).toBe(true);
    expect(keyDown(element, 'Enter').defaultPrevented).toBe(false);
    keyUp(element, SPACE);
    expect(onClick).toHaveBeenCalledTimes(0);
  });

  it('a default-prevented Space keydown does not cancel the keyup activation', () => {
    const { element, onClick } = mount(
      'span',
      { native: false },
      { before: (span) => span.addEventListener('keydown', (event) => event.preventDefault()) },
    );

    press(element, SPACE);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('suppresses the native Space keyup click of a composite button even when the activation is vetoed', () => {
    const { element } = mount('button', { composite: true }, { twin: { synchronous: true, verdict: 'stop' } });

    expect(keyUp(element, SPACE).defaultPrevented).toBe(true);
  });

  it('has no composition guard: Enter during composition still activates', () => {
    const { element, onClick } = mount('span', { native: false });

    keyDown(element, 'Enter', { isComposing: true });
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('marks keyboard clicks with detail 0 and an empty pointerType', () => {
    const { element, onClick } = mount('span', { native: false });

    press(element, 'Enter');
    press(element, SPACE, { ctrlKey: true, altKey: true, metaKey: true });

    const [enterClick, spaceClick] = onClick.mock.calls.map((call) => call[0] as PointerEvent);
    expect(enterClick).toBeInstanceOf(PointerEvent);
    expect(enterClick!.detail).toBe(0);
    expect(enterClick!.pointerType).toBe('');
    expect(spaceClick!.ctrlKey && spaceClick!.altKey && spaceClick!.metaKey).toBe(true);
    expect(spaceClick!.shiftKey).toBe(false);
  });
});

describe('button module: decision point', () => {
  it('asks no question on Server and runs the activation (fallback run)', () => {
    const { element, onClick, fake } = mount('span', { native: false });

    keyDown(element, 'Enter');

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(fake.decisions).toEqual([]);
  });

  it('runs the activation when the C# chain answers run', () => {
    const { element, onClick, fake } = mount('span', { native: false }, { twin: { synchronous: true, verdict: 'run' } });

    keyDown(element, 'Enter');

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(fake.decisions).toHaveLength(1);
  });

  it('asks only when the module would act on the key', () => {
    const { element, fake } = mount('span', { native: false }, { twin: { synchronous: true } });

    keyDown(element, 'ArrowDown');
    keyDown(element, 'Tab');
    keyUp(element, 'Enter');

    expect(fake.decisions).toEqual([]);
  });

  it('a vetoed Space keydown on a link leaves the default action alone', () => {
    const { element: link } = mount('a', { native: false }, { before: (a) => (a.href = '#test'), twin: { synchronous: true, verdict: 'stop' } });

    expect(keyDown(link, SPACE).defaultPrevented).toBe(false);
  });

  it('sends the event snapshot the C# record reads', () => {
    const { element, fake } = mount('span', { native: false }, { twin: { synchronous: true } });
    element.id = 'button-1';

    keyDown(element, 'Enter', { shiftKey: true, code: 'Enter' });

    const event: EventInfo = fake.decisions[0]!.event;
    expect(event).toMatchObject({
      kind: 'KeyboardEvent',
      type: 'keydown',
      key: 'Enter',
      code: 'Enter',
      shiftKey: true,
      altKey: false,
      target: { id: 'button-1', tagName: 'SPAN' },
      defaultPrevented: false,
    });
  });
});

describe('button module: disabled press suppression', () => {
  it('prevents the default action of a click and of a pointerdown while disabled', () => {
    const { element } = mount('span', { native: false, disabled: true });

    expect(click(element).defaultPrevented).toBe(true);
    expect(pointer(element, 'pointerdown', { pointerType: 'mouse' }).defaultPrevented).toBe(true);
  });

  it('leaves the default action of a mousedown alone while disabled', () => {
    const { element } = mount('span', { native: false, disabled: true });

    expect(mouse(element, 'mousedown').defaultPrevented).toBe(false);
  });

  it('performs no keyboard activation while disabled, whatever the mode', () => {
    for (const options of [{ native: false }, { native: false, composite: true }, { native: true, composite: true }]) {
      const { element, onClick } = mount(options.native ? 'button' : 'span', { ...options, disabled: true });

      press(element, 'Enter');
      press(element, SPACE);
      expect(onClick).toHaveBeenCalledTimes(0);
    }
  });

  it('does not prevent keys of a disabled element that is not focusable when disabled', () => {
    const { element } = mount('span', { native: false, disabled: true, composite: true });

    expect(keyDown(element, 'ArrowRight').defaultPrevented).toBe(false);
    expect(keyDown(element, SPACE).defaultPrevented).toBe(false);
  });

  it('leaves the default action of a click and a pointerdown alone while enabled', () => {
    const { element } = mount('span', { native: false });

    expect(click(element).defaultPrevented).toBe(false);
    expect(pointer(element, 'pointerdown', { pointerType: 'mouse' }).defaultPrevented).toBe(false);
  });

  it('reads the disabled flag from the latest update', () => {
    const { element, handle, onClick } = mount('span', { native: false });

    handle.update(optionsJson({ native: false, disabled: true }));
    press(element, 'Enter');
    expect(onClick).toHaveBeenCalledTimes(0);

    handle.update(optionsJson({ native: false }));
    press(element, 'Enter');
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});

describe('button module: focusable when disabled', () => {
  it('prevents every key but Tab while disabled and keeps them propagating', () => {
    const ancestor = document.createElement('div');
    document.body.appendChild(ancestor);
    const seenByAncestor = vi.fn();
    ancestor.addEventListener('keydown', seenByAncestor);
    const { element } = mount(
      'span',
      { native: false, disabled: true, focusableWhenDisabled: true, composite: true },
      { parent: ancestor },
    );

    expect(keyDown(element, 'ArrowRight').defaultPrevented).toBe(true);
    expect(keyDown(element, 'Tab', { shiftKey: true }).defaultPrevented).toBe(false);
    expect(seenByAncestor).toHaveBeenCalledTimes(2);
  });

  it('corrects the disabled property again when an update makes the composite button disabled', () => {
    const { element, handle } = mount('button', { composite: true, focusableWhenDisabled: true });
    element.disabled = true;

    handle.update(optionsJson({ composite: true, focusableWhenDisabled: true, disabled: true }));

    expect(element.disabled).toBe(false);
  });

  it('leaves the disabled property alone where the part itself renders the disabled attribute', () => {
    const outside = mount('button', { disabled: true, focusableWhenDisabled: true, composite: false }, { before: (b) => (b.disabled = true) });
    const nonFocusable = mount('button', { disabled: true, focusableWhenDisabled: false, composite: true }, { before: (b) => (b.disabled = true) });
    const unspecified = mount('button', { disabled: true, composite: true }, { before: (b) => (b.disabled = true) });

    expect(outside.element.disabled).toBe(true);
    expect(nonFocusable.element.disabled).toBe(true);
    expect(unspecified.element.disabled).toBe(true);
  });
});

describe('button module: development checks', () => {
  it('reports a native-mode element that is not a button once per check', () => {
    const { fake } = mount('span', { native: true, developmentChecks: true });

    expect(fake.reported('OnNativeButtonMismatch')).toEqual([{ expectedNative: true }]);
  });

  it('reports a non-native-mode button once per check', () => {
    const { fake } = mount('button', { native: false, developmentChecks: true });

    expect(fake.reported('OnNativeButtonMismatch')).toEqual([{ expectedNative: false }]);
  });

  it('checks again only when native changes', () => {
    const { handle, fake } = mount('span', { native: true, developmentChecks: true });

    handle.update(optionsJson({ native: true, developmentChecks: true, disabled: true }));
    handle.update(optionsJson({ native: false, developmentChecks: true }));
    handle.update(optionsJson({ native: true, developmentChecks: true }));

    expect(fake.reported('OnNativeButtonMismatch')).toEqual([{ expectedNative: true }, { expectedNative: true }]);
  });

  it('checks nothing unless development checks are on', () => {
    const { fake } = mount('span', { native: true });

    expect(fake.reports).toEqual([]);
  });
});

describe('dispatchClickWithModifiers', () => {
  it('dispatches an untrusted PointerEvent click that bubbles, is cancelable and composed', () => {
    const target = document.createElement('span');
    document.body.appendChild(target);
    const seen = vi.fn();
    document.body.addEventListener('click', seen);

    dispatchClickWithModifiers(target, { shiftKey: false, ctrlKey: false, altKey: false, metaKey: false });

    const event = seen.mock.calls[0]![0] as PointerEvent;
    expect(event).toBeInstanceOf(PointerEvent);
    expect([event.type, event.bubbles, event.cancelable, event.composed, event.isTrusted]).toEqual(['click', true, true, true, false]);
  });

  it('copies the four modifier keys of the source event', () => {
    const target = document.createElement('span');
    const seen = vi.fn();
    target.addEventListener('click', seen);

    dispatchClickWithModifiers(target, { shiftKey: true, ctrlKey: false, altKey: true, metaKey: true });

    const event = seen.mock.calls[0]![0] as PointerEvent;
    expect([event.shiftKey, event.ctrlKey, event.altKey, event.metaKey]).toEqual([true, false, true, true]);
  });

  it('defaults detail to 0 and pointerType to the empty string, the convention for keyboard clicks', () => {
    const target = document.createElement('span');
    const seen = vi.fn();
    target.addEventListener('click', seen);

    dispatchClickWithModifiers(target, new KeyboardEvent('keydown'));

    const event = seen.mock.calls[0]![0] as PointerEvent;
    expect([event.detail, event.pointerType]).toEqual([0, '']);
  });

  it('carries detail 1 and pointerType mouse for a click that represents a mouse gesture', () => {
    const target = document.createElement('span');
    const seen = vi.fn();
    target.addEventListener('click', seen);

    dispatchClickWithModifiers(target, new MouseEvent('mouseup'), { detail: 1, pointerType: 'mouse' });

    const event = seen.mock.calls[0]![0] as PointerEvent;
    expect([event.detail, event.pointerType]).toEqual([1, 'mouse']);
  });
});

describe('useEnhancedClickHandler', () => {
  interface Interaction {
    interactionType: string;
    event: EventInfo;
  }

  function interactions(fake: FakeTwin): string[] {
    return fake.reported<Interaction>('OnInteraction').map((i) => i.interactionType);
  }

  it('reports the pointer type on pointerdown and again on the click', () => {
    const { element, fake } = mount('button', { reportInteractionType: true });

    pointer(element, 'pointerdown', { pointerType: 'touch' });
    pointer(element, 'click', { pointerType: 'touch', detail: 1 });

    expect(interactions(fake)).toEqual(['touch', 'touch']);
    expect(fake.reported<Interaction>('OnInteraction')[0]!.event).toMatchObject({ kind: 'PointerEvent', type: 'pointerdown' });
  });

  it('classifies a click with detail 0 as keyboard and reports it once', () => {
    const { element, fake } = mount('span', { native: false, reportInteractionType: true });

    keyDown(element, 'Enter');

    expect(interactions(fake)).toEqual(['keyboard']);
  });

  it('falls back to the pointerdown pointer type for a MouseEvent click and clears it afterwards', () => {
    const { element, fake } = mount('button', { reportInteractionType: true });

    pointer(element, 'pointerdown', { pointerType: 'pen' });
    click(element);
    click(element);

    expect(interactions(fake)).toEqual(['pen', 'pen', '']);
  });

  it('does not clear the recorded pointer type on a keyboard click', () => {
    const { element, fake } = mount('button', { reportInteractionType: true });

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    click(element, { detail: 0 });
    click(element);

    expect(interactions(fake)).toEqual(['mouse', 'keyboard', 'mouse']);
  });

  it('ignores a default-prevented pointerdown', () => {
    const { element, fake } = mount(
      'button',
      { reportInteractionType: true },
      { before: (b) => b.addEventListener('pointerdown', (event) => event.preventDefault()) },
    );

    pointer(element, 'pointerdown', { pointerType: 'touch' });
    click(element);

    expect(interactions(fake)).toEqual(['']);
  });

  it('reports nothing unless asked, and nothing while disabled', () => {
    const quiet = mount('button');
    const disabled = mount('button', { reportInteractionType: true, disabled: true, focusableWhenDisabled: true });

    for (const { element } of [quiet, disabled]) {
      pointer(element, 'pointerdown', { pointerType: 'mouse' });
      click(element);
    }

    expect(quiet.fake.reports).toEqual([]);
    expect(disabled.fake.reports).toEqual([]);
  });
});

describe('usePressAndHold', () => {
  const START_AUTO_CHANGE_DELAY = 400;
  const CHANGE_VALUE_TICK_DELAY = 60;
  // Mirrors the touch settle delay in `usePressAndHold`.
  const TOUCH_TIMEOUT = 50;

  beforeEach(() => {
    vi.useFakeTimers();
  });

  function stepper(options: Partial<ButtonOptions> = {}, parent?: HTMLElement) {
    return mount('button', { pressAndHold: {}, focusableWhenDisabled: true, ...options }, parent ? { parent } : {});
  }

  const ticks = (fake: FakeTwin) => fake.reported('OnHoldTick').length;

  describe('press and hold', () => {
    it('stops the hold on release when an ancestor stops pointerup propagation', () => {
      const ancestor = document.createElement('div');
      document.body.appendChild(ancestor);
      ancestor.addEventListener('pointerup', (event) => event.stopPropagation());
      const { element: button, fake } = stepper({}, ancestor);

      pointer(button, 'pointerdown', { pointerType: 'mouse' });

      expect(ticks(fake)).toBe(1);

      pointer(button, 'pointerup', { pointerType: 'mouse' });
      mouse(button, 'mouseup');

      vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY);
      vi.advanceTimersByTime(CHANGE_VALUE_TICK_DELAY);
      vi.advanceTimersByTime(CHANGE_VALUE_TICK_DELAY);

      expect(ticks(fake)).toBe(1);
    });

    it('starts the hold once a touch press settles and ignores the compatibility click', () => {
      const { element: increment, fake } = stepper();

      touch(increment, 'touchstart');
      pointer(increment, 'pointerdown', { pointerType: 'touch' });

      expect(ticks(fake)).toBe(0);

      vi.advanceTimersByTime(TOUCH_TIMEOUT);

      expect(ticks(fake)).toBe(1);

      vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY);
      vi.advanceTimersByTime(CHANGE_VALUE_TICK_DELAY);
      vi.advanceTimersByTime(CHANGE_VALUE_TICK_DELAY);

      expect(ticks(fake)).toBe(3);

      pointer(increment, 'pointerup', { pointerType: 'touch' });
      touch(increment, 'touchend');
      click(increment, { detail: 1 });

      vi.advanceTimersByTime(CHANGE_VALUE_TICK_DELAY);

      expect(ticks(fake)).toBe(3);
      expect(fake.reported('OnHoldClick')).toEqual([]);
    });

    it('does not start the hold when the touch moves like a scroll', () => {
      const { element: increment, fake } = stepper();

      touch(increment, 'touchstart');
      pointer(increment, 'pointerdown', { pointerType: 'touch', clientX: 0, clientY: 0 });
      pointer(increment, 'pointermove', { pointerType: 'touch', clientX: 0, clientY: 20 });

      vi.advanceTimersByTime(TOUCH_TIMEOUT);
      vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY);
      vi.advanceTimersByTime(CHANGE_VALUE_TICK_DELAY);

      expect(ticks(fake)).toBe(0);
    });

    it('treats a touch press with several small moves as a tap', () => {
      const { element: increment, fake } = stepper();

      touch(increment, 'touchstart');
      pointer(increment, 'pointerdown', { pointerType: 'touch', clientX: 0, clientY: 0 });
      pointer(increment, 'pointermove', { pointerType: 'touch', clientX: 1, clientY: 0 });
      pointer(increment, 'pointermove', { pointerType: 'touch', clientX: 2, clientY: 0 });
      pointer(increment, 'pointermove', { pointerType: 'touch', clientX: 3, clientY: 0 });

      vi.advanceTimersByTime(TOUCH_TIMEOUT);
      vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY);

      expect(ticks(fake)).toBe(0);

      pointer(increment, 'pointerup', { pointerType: 'touch' });
      touch(increment, 'touchend');
      click(increment, { detail: 1 });

      expect(fake.reported('OnHoldClick')).toHaveLength(1);
    });
  });

  it('fires once per press-release even after mouseleave/mouseenter during a hold', () => {
    const { element: inc, fake } = stepper();

    pointer(inc, 'pointerdown', { button: 0, pointerType: 'mouse' });
    mouse(inc, 'mouseleave');
    mouse(inc, 'mouseenter', { buttons: 1 });
    pointer(inc, 'pointerup', { button: 0, pointerType: 'mouse' });

    expect(fake.reported('OnHoldStop')).toHaveLength(1);
  });

  it('a primary mouse press prevents the default action, ticks at once, then repeats after 400 ms every 60 ms', () => {
    const { element, fake } = stepper();

    expect(pointer(element, 'pointerdown', { pointerType: 'mouse' }).defaultPrevented).toBe(true);
    expect(ticks(fake)).toBe(1);

    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY - 1);
    expect(ticks(fake)).toBe(1);

    vi.advanceTimersByTime(1);
    expect(ticks(fake)).toBe(2);

    vi.advanceTimersByTime(CHANGE_VALUE_TICK_DELAY * 3);
    expect(ticks(fake)).toBe(5);
  });

  it('honours the configured delays and scroll distance', () => {
    const { element, fake } = stepper({ pressAndHold: { startDelay: 100, tickDelay: 10, scrollDistance: 30 } });

    touch(element, 'touchstart');
    pointer(element, 'pointerdown', { pointerType: 'touch', clientX: 0, clientY: 0 });
    pointer(element, 'pointermove', { pointerType: 'touch', clientX: 0, clientY: 20 });
    vi.advanceTimersByTime(TOUCH_TIMEOUT);
    expect(ticks(fake)).toBe(1);

    vi.advanceTimersByTime(110);
    expect(ticks(fake)).toBe(2);
  });

  it('every tick carries the event that started the sequence', () => {
    const { element, fake } = stepper();

    pointer(element, 'pointerdown', { pointerType: 'mouse', shiftKey: true });
    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY);

    const events = fake.reported<{ event: EventInfo }>('OnHoldTick').map((t) => t.event);
    expect(events).toHaveLength(2);
    for (const event of events) {
      expect(event).toMatchObject({ kind: 'PointerEvent', type: 'pointerdown', pointerType: 'mouse', shiftKey: true });
    }
  });

  it('a default-prevented, non-primary or disabled press starts nothing', () => {
    const prevented = mount('button', { pressAndHold: {} }, { before: (b) => b.addEventListener('pointerdown', (e) => e.preventDefault()) });
    const secondary = stepper();
    const disabled = stepper({ disabled: true });

    pointer(prevented.element, 'pointerdown', { pointerType: 'mouse' });
    pointer(secondary.element, 'pointerdown', { pointerType: 'mouse', button: 2 });
    pointer(disabled.element, 'pointerdown', { pointerType: 'mouse' });
    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY);

    expect([ticks(prevented.fake), ticks(secondary.fake), ticks(disabled.fake)]).toEqual([0, 0, 0]);
  });

  it('a mouseenter while the mouse is still pressed restarts the sequence with an immediate tick', () => {
    const { element, fake } = stepper();

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    mouse(element, 'mouseleave');
    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY);
    expect(ticks(fake)).toBe(1);

    mouse(element, 'mouseenter', { buttons: 1 });
    expect(ticks(fake)).toBe(2);
  });

  it('ignores the compatibility mouse events while a touch is down', () => {
    const { element, fake } = stepper();

    touch(element, 'touchstart');
    pointer(element, 'pointerdown', { pointerType: 'touch' });
    vi.advanceTimersByTime(TOUCH_TIMEOUT);
    mouse(element, 'mouseleave');
    mouse(element, 'mouseup');
    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY);

    expect(ticks(fake)).toBe(2);
  });

  it('suppresses the context menu on the window only while a sequence runs', () => {
    const { element } = stepper();
    const contextMenu = () => {
      const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
      document.body.dispatchEvent(event);
      return event.defaultPrevented;
    };

    expect(contextMenu()).toBe(false);

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    expect(contextMenu()).toBe(true);

    pointer(element, 'pointerup', { pointerType: 'mouse' });
    expect(contextMenu()).toBe(false);
  });

  it('reports the release with the pointerup event', () => {
    const { element, fake } = stepper();

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    pointer(document.body, 'pointerup', { pointerType: 'mouse' });

    expect(fake.reported<{ event: EventInfo }>('OnHoldStop')).toEqual([{ event: expect.objectContaining({ type: 'pointerup' }) }]);
  });

  it('becoming disabled during a press stops the sequence and resets the press', () => {
    const { element, handle, fake } = stepper();

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    handle.update(optionsJson({ pressAndHold: {}, focusableWhenDisabled: true, disabled: true }));
    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY);
    expect(ticks(fake)).toBe(1);

    handle.update(optionsJson({ pressAndHold: {}, focusableWhenDisabled: true }));
    mouse(element, 'mouseenter', { buttons: 1 });
    expect(ticks(fake)).toBe(1);
  });

  it('dispose removes every timer and window listener', () => {
    const { element, handle, fake } = stepper();

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    handle.dispose();
    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY * 3);
    pointer(document.body, 'pointerup', { pointerType: 'mouse' });

    expect(ticks(fake)).toBe(1);
    expect(fake.reported('OnHoldStop')).toEqual([]);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('a keyboard click performs the single action; a mouse click after a press, or a prevented click, does not', () => {
    const { element, fake } = stepper();

    click(element, { detail: 0 });
    expect(fake.reported('OnHoldClick')).toHaveLength(1);

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    pointer(element, 'pointerup', { pointerType: 'mouse' });
    click(element, { detail: 1 });
    expect(fake.reported('OnHoldClick')).toHaveLength(1);

    document.body.addEventListener('click', (event) => event.preventDefault(), { capture: true, once: true });
    click(element, { detail: 0 });
    expect(fake.reported('OnHoldClick')).toHaveLength(1);
  });

  it('runs nothing without the press-and-hold options', () => {
    const { element, fake } = mount('button');

    pointer(element, 'pointerdown', { pointerType: 'mouse' });
    click(element, { detail: 0 });
    vi.advanceTimersByTime(START_AUTO_CHANGE_DELAY + CHANGE_VALUE_TICK_DELAY);

    expect(fake.reports).toEqual([]);
  });
});

describe('isTouchLikePointerType', () => {
  it('treats touch and pen as touch-like and nothing else', () => {
    expect(['touch', 'pen', 'mouse', ''].map(isTouchLikePointerType)).toEqual([true, true, false, false]);
  });
});

describe('isElementDisabled', () => {
  it('is true for null, a disabled attribute or aria-disabled="true", and false otherwise', () => {
    const withAttribute = (name: string, value: string) => {
      const element = document.createElement('div');
      element.setAttribute(name, value);
      return element;
    };

    expect(isElementDisabled(null)).toBe(true);
    expect(isElementDisabled(withAttribute('disabled', ''))).toBe(true);
    expect(isElementDisabled(withAttribute('aria-disabled', 'true'))).toBe(true);
    expect(isElementDisabled(withAttribute('aria-disabled', 'false'))).toBe(false);
    expect(isElementDisabled(document.createElement('div'))).toBe(false);
  });
});

describe('button module: twin handle', () => {
  it('attaches by id when the part has no element reference', () => {
    const element = document.createElement('span');
    element.id = 'base-ui-7';
    document.body.appendChild(element);
    const onClick = vi.fn();
    element.addEventListener('click', onClick);

    handles.push(attach('base-ui-7', optionsJson({ native: false }), createFakeTwin().twin));
    keyDown(element, 'Enter');

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('dispose removes every listener and is idempotent; update after dispose does nothing', () => {
    const { element, handle, onClick } = mount('span', { native: false, disabled: true, focusableWhenDisabled: true });

    handle.dispose();
    handle.dispose();
    handle.update(optionsJson({ native: false }));

    expect(keyDown(element, 'ArrowDown').defaultPrevented).toBe(false);
    expect(click(element).defaultPrevented).toBe(false);
    keyDown(element, 'Enter');
    expect(onClick).toHaveBeenCalledTimes(1);
    expect((onClick.mock.calls[0]![0] as MouseEvent).detail).toBe(1);
  });

  it('never touches the element style', () => {
    const { element, handle } = mount('span', { native: false, style: 'color: red' }, { before: (s) => (s.style.cssText = 'margin: 1px;') });

    handle.update(optionsJson({ native: false }));

    expect(element.style.cssText).toBe('margin: 1px;');
  });
});

// The hidden-input bridge (UBT-EVT-09; ADR 12 Amendments 2026-10-22 item 4; switch.md SWT-PTR-01/02/03/07/11): Switch,
// Checkbox and Radio render a visually hidden input as the root's next sibling, and every toggle is a click on it. Port
// titles: Base UI tests these through the components (SwitchRoot.test.tsx, L4 in the port).
interface Bridged {
  root: HTMLElement;
  input: HTMLInputElement;
  handle: TwinHandle;
  fake: FakeTwin;
  inputClicks: MouseEvent[];
}

function mountBridge(
  options: Partial<ButtonOptions> = {},
  { twin = {}, parent = document.body, tag = 'span', withHidden = false, before }: {
    twin?: FakeTwinOptions;
    parent?: HTMLElement;
    tag?: 'span' | 'button';
    withHidden?: boolean;
    before?: (root: HTMLElement, input: HTMLInputElement) => void;
  } = {},
): Bridged {
  const root = document.createElement(tag);
  parent.appendChild(root);
  if (withHidden) {
    const hidden = document.createElement('input');
    hidden.type = 'hidden';
    parent.appendChild(hidden);
  }
  const input = document.createElement('input');
  input.type = 'checkbox';
  parent.appendChild(input);
  before?.(root, input);
  const fake = createFakeTwin(twin);
  const handle = attach(root, optionsJson({ native: tag === 'button', hiddenInput: true, ...options }), fake.twin);
  handles.push(handle);
  const inputClicks: MouseEvent[] = [];
  input.addEventListener('click', (event) => inputClicks.push(event));
  return { root, input, handle, fake, inputClicks };
}

interface Toggle {
  checked: boolean;
  event: EventInfo;
}

// RAD-EDGE-09 (design point R9 of M2 tracks 7-8, option (a), settled by the orchestrator 2026-10-23; ADR 04 Amendments
// 2026-10-22, the bridge row, as amended 2026-10-23): a render callback may wrap the part's element, as the docs' native
// radio wraps its <button> in a <label>; the input then follows the callback's top-level element.
describe('findHiddenInput (port)', () => {
  function input(type: string): HTMLInputElement {
    const element = document.createElement('input');
    element.type = type;
    return element;
  }

  it('finds the input that follows the element, past hidden inputs', () => {
    const root = document.createElement('span');
    const checkbox = input('checkbox');
    document.body.append(root, input('hidden'), checkbox);

    expect(findHiddenInput(root)).toBe(checkbox);
  });

  it('finds the input that follows the render callback element containing the element', () => {
    const label = document.createElement('label');
    const button = document.createElement('button');
    label.append(button, 'Apple');
    const radio = input('radio');
    document.body.append(label, radio);

    expect(findHiddenInput(button)).toBe(radio);
  });

  it('lets a non-input sibling stop the search at each level, so a neighbour\'s input is never taken', () => {
    const wrapper = document.createElement('div');
    const button = document.createElement('button');
    const neighbour = document.createElement('span');
    wrapper.append(button, neighbour, input('radio'));
    const radio = input('radio');
    document.body.append(wrapper, radio);

    // The wrapper's own children: the button's next sibling is not an input, so the search moves up a level.
    expect(findHiddenInput(button)).toBe(radio);

    const lone = document.createElement('div');
    const loneButton = document.createElement('button');
    lone.append(loneButton, document.createElement('span'), input('radio'));
    document.body.append(lone, document.createElement('p'));

    expect(findHiddenInput(loneButton)).toBeNull();
  });
});

describe.each([
  ['Server', false],
  ['WebAssembly', true],
])('button module: hidden-input bridge under a render callback (%s)', (_mode, synchronous) => {
  it('forwards a click on a button wrapped in a label to the input that follows the label', () => {
    const label = document.createElement('label');
    document.body.appendChild(label);
    const { input, inputClicks, root } = mountBridge({}, {
      twin: { synchronous },
      tag: 'button',
      parent: label,
      before: (button, hidden) => {
        label.appendChild(document.createTextNode('Apple'));
        label.after(hidden);
        void button;
      },
    });

    click(root);

    expect(inputClicks).toHaveLength(1);
    expect(input.checked).toBe(true);
  });
});

describe.each([
  ['Server', false],
  ['WebAssembly', true],
])('button module: hidden-input bridge (%s)', (mode, synchronous) => {
  it('forwards a root click to the hidden input as a click carrying its modifiers, with detail 0 and no pointer type', () => {
    const { root, input, inputClicks } = mountBridge({}, { twin: { synchronous } });

    const rootClick = click(root, { shiftKey: true, metaKey: true });

    expect(rootClick.defaultPrevented).toBe(true);
    expect(inputClicks).toHaveLength(1);
    const forwarded = inputClicks[0] as PointerEvent;
    expect(forwarded).toBeInstanceOf(PointerEvent);
    expect(forwarded.detail).toBe(0);
    expect(forwarded.pointerType).toBe('');
    expect(forwarded.shiftKey).toBe(true);
    expect(forwarded.metaKey).toBe(true);
    expect(forwarded.ctrlKey).toBe(false);
    expect(forwarded.bubbles && forwarded.cancelable && forwarded.composed).toBe(true);
    expect(input.checked).toBe(true);
  });

  it('finds the hidden input after the unchecked-value input', () => {
    const { root, input } = mountBridge({}, { twin: { synchronous }, withHidden: true });

    click(root);

    expect(input.checked).toBe(true);
  });

  it('forwards a keyboard activation with the key event modifiers', () => {
    const { root, inputClicks } = mountBridge({}, { twin: { synchronous } });

    keyDown(root, 'Enter', { altKey: true });

    expect(inputClicks).toHaveLength(1);
    expect(inputClicks[0]!.altKey).toBe(true);
    expect(inputClicks[0]!.detail).toBe(0);
  });

  it('prevents a root click and tolerates it while no hidden input is connected', () => {
    const { root, input, fake } = mountBridge({}, { twin: { synchronous } });
    input.remove();

    let rootClick: MouseEvent | undefined;
    expect(() => (rootClick = click(root))).not.toThrow();
    expect(rootClick!.defaultPrevented).toBe(true);
    expect(fake.reports).toEqual([]);
  });

  it('propagates a single click event to ancestors per user click', () => {
    const ancestor = document.createElement('div');
    document.body.appendChild(ancestor);
    const seen: EventTarget[] = [];
    ancestor.addEventListener('click', (event) => seen.push(event.target!));
    const { root, input } = mountBridge({}, { twin: { synchronous }, parent: ancestor });

    click(root);
    input.click();

    expect(seen).toEqual([root]);
  });

  it('toggles once when a wrapping native label is clicked through the root', () => {
    const label = document.createElement('label');
    document.body.appendChild(label);
    const { root, input, inputClicks } = mountBridge({}, { twin: { synchronous }, parent: label });

    const rootClick = click(root);

    expect(rootClick.defaultPrevented).toBe(true);
    expect(inputClicks).toHaveLength(1);
    expect(inputClicks[0]!.detail).toBe(0);
    expect(input.checked).toBe(true);
  });

  it('ignores a hidden input click canceled before the module handles it', () => {
    let cancel = true;
    const { input, fake } = mountBridge(
      {},
      { twin: { synchronous }, before: (_root, hidden) => hidden.addEventListener('click', (event) => cancel && event.preventDefault()) },
    );

    input.click();
    expect(input.checked).toBe(false);
    cancel = false;
    input.click();

    const asked = synchronous ? fake.decisions.filter((d) => d.point === 'toggle').length : fake.reported('OnToggle').length;
    expect(asked).toBe(1);
  });

  it('ignores a root click while read-only and reverts a click that reaches the input', () => {
    const { root, input, inputClicks, fake } = mountBridge({ readOnly: true }, { twin: { synchronous } });

    const rootClick = click(root);
    expect(rootClick.defaultPrevented).toBe(false);
    expect(inputClicks).toHaveLength(0);

    input.click();
    expect(input.checked).toBe(false);
    expect(fake.reports).toEqual([]);
    expect(fake.decisions).toEqual([]);
  });

  it('starts listening to the hidden input when an update turns the bridge on, and stops on dispose', () => {
    const { input, handle, fake } = mountBridge({ hiddenInput: false }, { twin: { synchronous } });

    input.click();
    expect(fake.reports.concat()).toEqual([]);
    handle.update(optionsJson({ native: false, hiddenInput: true }));
    input.click();
    handle.dispose();
    input.click();

    const asked = synchronous ? fake.decisions.filter((d) => d.point === 'toggle').length : fake.reported('OnToggle').length;
    expect(asked).toBe(1);
  });

  if (mode === 'Server') {
    it('reports the toggle once with the input click and the new checked value', () => {
      const { root, fake } = mountBridge({}, { twin: { synchronous } });

      click(root, { shiftKey: true });

      expect(fake.reported<Toggle>('OnToggle')).toEqual([
        expect.objectContaining({
          checked: true,
          event: expect.objectContaining({ kind: 'PointerEvent', type: 'click', detail: 0, pointerType: '', shiftKey: true }),
        }),
      ]);
      expect(fake.decisions).toEqual([]);
    });
  } else {
    it('asks the toggle decision inside the input click and sends no report', () => {
      const { root, fake } = mountBridge({}, { twin: { synchronous, verdict: 'run' } });

      click(root, { ctrlKey: true });

      const toggles = fake.decisions.filter((d) => d.point === 'toggle');
      expect(toggles).toHaveLength(1);
      expect(toggles[0]!.event).toMatchObject({ type: 'click', detail: 0, ctrlKey: true });
      expect(fake.reports).toEqual([]);
    });

    it('reverts the box natively when the toggle decision answers stop, also for a click that reaches the input directly', () => {
      const { input } = mountBridge({}, { twin: { synchronous, verdict: 'stop' } });
      const changes = vi.fn();
      input.addEventListener('change', changes);

      input.click();

      expect(input.checked).toBe(false);
      expect(changes).not.toHaveBeenCalled();
    });

    it('asks the activation decision with the root click and forwards nothing when it answers stop', () => {
      const { root, inputClicks, fake } = mountBridge({}, { twin: { synchronous, verdict: 'stop' } });

      const rootClick = click(root);

      expect(fake.decisions.map((d) => [d.point, d.event.type])).toEqual([['activate', 'click']]);
      expect(rootClick.defaultPrevented).toBe(false);
      expect(inputClicks).toHaveLength(0);
    });
  }
});

describe('button module: Enter suppression', () => {
  it.each([
    ['non-native', 'span', false],
    ['native', 'button', true],
  ] as const)('prevents Enter and never activates (%s)', (_name, tag, native) => {
    const { element, onClick, fake } = mount(tag, { native, suppressEnter: true }, { twin: { synchronous: true } });

    const enter = keyDown(element, 'Enter');

    expect(enter.defaultPrevented).toBe(true);
    expect(onClick).not.toHaveBeenCalled();
    expect(fake.decisions).toEqual([]);
  });

  it('prevents Enter from a descendant and while disabled, and leaves Space activation alone', () => {
    const { element, onClick } = mount('span', { native: false, suppressEnter: true });
    const child = element.appendChild(document.createElement('i'));

    expect(keyDown(child, 'Enter').defaultPrevented).toBe(true);
    press(element, SPACE);
    expect(onClick).toHaveBeenCalledTimes(1);

    const disabled = mount('span', { native: false, disabled: true, suppressEnter: true });
    expect(keyDown(disabled.element, 'Enter').defaultPrevented).toBe(true);
  });
});
