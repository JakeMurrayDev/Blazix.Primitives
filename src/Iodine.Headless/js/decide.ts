// `decide(point, eventInfo, fallbackPolicy)` as a free function over a handle's channel, so a module
// reads `decide(twin, 'escape', event, 'prevent-and-stop')` where the ADR says it does.
import type { DecisionPolicy, TwinChannel } from './twin';

export type { DecisionPolicy };

/**
 * Resolves a decision point: on WebAssembly the twin's C# chain runs synchronously inside the dispatch and its verdict
 * is returned; elsewhere `fallback` is returned without a call.
 * @param twin The handle's channel to its twin.
 * @param point The name of the decision point, for example `activate`.
 * @param event The event being dispatched, or `undefined` for a request with no browser event.
 * @param fallback The policy to apply when C# cannot be asked synchronously.
 * @returns The verdict.
 */
export function decide(twin: TwinChannel, point: string, event: Event | undefined, fallback: DecisionPolicy): DecisionPolicy {
  return twin.decide(point, event, fallback);
}
