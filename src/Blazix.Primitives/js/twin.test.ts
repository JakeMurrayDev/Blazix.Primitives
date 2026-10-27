// L3 (ADR 06 §1): the twin channel of `twin.ts` against the fake twin in its Server form and its `synchronous: true`
// (WebAssembly) form. Port titles: Base UI has no counterpart.
import { describe, expect, it } from 'vitest';
import { createFakeTwin } from './test-support/fake-twin';
import { attachTarget, createTwinChannel, decisionPolicies, type DotNetTwin } from './twin';

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
