// L3 (ADR 06 §1): the twin channel of `twin.ts` against the fake twin in its Server form and its `synchronous: true`
// (WebAssembly) form. Port titles: Base UI has no counterpart.
import { describe, expect, it } from 'vitest';
import { createFakeTwin } from './test-support/fake-twin';
import { createTwinChannel, resolveReference, type DotNetTwin } from './twin';

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
