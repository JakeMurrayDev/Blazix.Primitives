// L3 (ADR 06 §1): the twin channel of `twin.ts` against the fake twin in its Server form and its `synchronous: true`
// (WebAssembly) form. Port titles: Base UI has no counterpart.
import { describe, expect, it } from 'vitest';
import { createFakeTwin } from './test-support/fake-twin';
import { attachTarget, createTwinChannel, decisionPolicies, registerShadowRoot, resolveElement, resolveReference, type DotNetTwin } from './twin';

/** Wraps a fake twin so the test can count the synchronous invocations the channel makes. */
function counting(twin: DotNetTwin): { twin: DotNetTwin; calls: string[] } {
  const calls: string[] = [];
  return {
    calls,
    twin: {
      invokeMethod<T>(methodName: string, ...args: unknown[]): T {
        calls.push(methodName);
        return twin.invokeMethod<T>(methodName, ...args);
      },
      invokeMethodAsync: twin.invokeMethodAsync.bind(twin),
    },
  };
}

// ADR 04 Amendments 2026-10-27 (element-typed option values cross as interop arguments): `attach` and `update` receive
// the references Blazor revived after the options string, and an element-typed option is the index of its reference.
describe('resolveReference', () => {
  it('port: an option index resolves to the revived reference attach or update received, and nothing resolves to null', () => {
    const element = document.createElement('div');
    const shadowRoot = element.attachShadow({ mode: 'closed' });
    const references: readonly unknown[] = [element, shadowRoot];

    expect(resolveReference<HTMLElement>(references, 0)).toBe(element);
    expect(resolveReference<ShadowRoot>(references, 1)).toBe(shadowRoot);
    expect(resolveReference(references, null)).toBeNull();
    expect(resolveReference(references, undefined)).toBeNull();
    expect(resolveReference(references, 2)).toBeNull();
    expect(resolveReference(undefined, 0)).toBeNull();
    expect(resolveReference(null, 0)).toBeNull();
  });
});

describe('createTwinChannel', () => {
  it('port: synchronous is true where the twin answers synchronous invocations, probed once for the channel', () => {
    const { twin, calls } = counting(createFakeTwin({ synchronous: true }).twin);
    const channel = createTwinChannel(twin);

    expect(channel.synchronous).toBe(true);
    expect(channel.synchronous).toBe(true);
    expect(channel.decide('select', undefined, 'run')).toBe('run');
    expect(calls).toEqual(['Ping', 'Decide']);
  });

  it('port: synchronous is false where the dispatcher refuses synchronous invocations, and decide then returns the fallback', () => {
    const { twin, calls } = counting(createFakeTwin().twin);
    const channel = createTwinChannel(twin);

    expect(channel.synchronous).toBe(false);
    expect(channel.decide('select', undefined, 'stop')).toBe('stop');
    expect(channel.lastDecisionWasSynchronous).toBe(false);
    expect(calls).toEqual(['Ping']);
  });
});

describe('decisionPolicies', () => {
  it('port: lists the literals of every C# DecisionPolicy member, prevent included', () => {
    expect(decisionPolicies).toEqual(['run', 'stop', 'start', 'prevent-and-stop', 'prevent']);
  });
});

describe('attachTarget', () => {
  it('port: the token form (null) has no element, while an id or an element resolves to the element', () => {
    const element = document.createElement('div');
    element.id = 'attached';
    document.body.append(element);

    expect(attachTarget(null)).toBeNull();
    expect(attachTarget('attached')).toBe(element);
    expect(attachTarget(element)).toBe(element);
    expect(() => attachTarget('missing')).toThrow('Blazix: ');
    element.remove();
  });
});

// ADR 04 Amendments 2026-10-28 (design point 9 of M4 track 8): Blazor revives an ElementReference with a document query
// for its capture attribute (`[_bl_<id>]`), which pierces no shadow root, so an element the portal moved into one arrives
// as null; the twin sends the capture ids beside the element and the references, and the lookup searches the shadow
// roots the portal registered.
describe('elements inside a shadow root', () => {
  it('port: an element revived as null resolves by its capture id in a registered shadow root, and the token form stays null', () => {
    const host = document.createElement('div');
    document.body.append(host);
    const root = host.attachShadow({ mode: 'closed' });
    const element = document.createElement('div');
    element.setAttribute('_bl_7', '');
    root.append(element);
    const unregister = registerShadowRoot(root);

    expect(attachTarget(null, ['7'])).toBe(element);
    expect(attachTarget(null)).toBeNull();
    expect(resolveReference([null], 0, [null, '7'])).toBe(element);

    unregister();
    expect(() => attachTarget(null, ['7'])).toThrow('Blazix: ');
    expect(resolveReference([null], 0, [null, '7'])).toBeNull();
    host.remove();
  });

  it('port: an id that no document element carries resolves inside a registered shadow root', () => {
    const host = document.createElement('div');
    document.body.append(host);
    const root = host.attachShadow({ mode: 'closed' });
    const element = document.createElement('div');
    element.id = 'inside';
    root.append(element);
    const unregister = registerShadowRoot(root);

    expect(resolveElement('inside')).toBe(element);

    unregister();
    host.remove();
  });
});
