// The fake twin every L3 module test attaches against (ADR 06 §1): it records the callbacks a module reports and the
// decisions it asks for, and behaves like Blazor Server (no synchronous invoke) or WebAssembly (synchronous invoke
// answering with a chosen verdict). Test-only: no module entry imports it, so esbuild never bundles it.
import type { DecisionPolicy, DotNetTwin } from '../twin';
import type { EventInfo } from '../event-info';

/** One callback the module sent through `report`, with its payload parsed back from JSON. */
export interface FakeReport {
  method: string;
  payload: unknown;
}

/** One decision the module asked for on the synchronous path. */
export interface FakeDecision {
  point: string;
  event: EventInfo;
}

export interface FakeTwinOptions {
  /** `true` behaves like WebAssembly (synchronous invoke); `false`, the default, like Blazor Server. */
  synchronous?: boolean;
  /** The verdict the synchronous `Decide` returns; `run` by default. */
  verdict?: DecisionPolicy;
}

export interface FakeTwin {
  twin: DotNetTwin;
  reports: FakeReport[];
  decisions: FakeDecision[];
  /** The payloads of every report sent to `method`, in order. */
  reported<T>(method: string): T[];
}

export function createFakeTwin(options: FakeTwinOptions = {}): FakeTwin {
  const reports: FakeReport[] = [];
  const decisions: FakeDecision[] = [];
  const twin: DotNetTwin = {
    invokeMethod<T>(methodName: string, ...args: unknown[]): T {
      if (!options.synchronous) {
        throw new Error('The current dispatcher does not support synchronous calls from JS to .NET.');
      }
      if (methodName === 'Ping') {
        return true as T;
      }
      if (methodName === 'Decide') {
        decisions.push({ point: args[0] as string, event: JSON.parse(args[1] as string) as EventInfo });
        return (options.verdict ?? 'run') as T;
      }
      throw new Error(`Unexpected synchronous call to ${methodName}`);
    },
    invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T> {
      reports.push({ method: methodName, payload: JSON.parse(args[0] as string) });
      return Promise.resolve(undefined as T);
    },
  };
  return {
    twin,
    reports,
    decisions,
    reported<T>(method: string): T[] {
      return reports.filter((r) => r.method === method).map((r) => r.payload as T);
    },
  };
}
