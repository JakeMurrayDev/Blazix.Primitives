// `decide(point, eventInfo, fallbackPolicy)` of ADR 04 §3 as a free function over a handle's channel, so a module
// reads `decide(twin, 'escape', event, 'prevent-and-stop')` where the ADR says it does.
import type { DecisionPolicy, TwinChannel } from './twin';

export type { DecisionPolicy };

export function decide(twin: TwinChannel, point: string, event: Event | undefined, fallback: DecisionPolicy): DecisionPolicy {
  return twin.decide(point, event, fallback);
}
