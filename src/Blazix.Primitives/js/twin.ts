// The JavaScript half of the twin contract (ADR 04 §3): what every module receives from `attach` and what it
// hands back. No module-level registries: everything a handle needs lives in the handle's closure.
import { eventInfo } from './event-info';

/** The `DotNetObjectReference` of the twin, as Blazor's JS interop revives it. */
export interface DotNetTwin {
  invokeMethod<T>(methodName: string, ...args: unknown[]): T;
  invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T>;
}

/** The verdict of a decision point and the fallback policies (ADR 04 §3; `DecisionPolicy` in C#). */
export type DecisionPolicy = 'run' | 'stop' | 'start' | 'prevent-and-stop';

/** What `attach` returns to C#: an `IJSObjectReference` with `update(options)` and `dispose()`. */
export interface TwinHandle {
  update(optionsJson: string): void;
  dispose(): void;
}

/** The fields every options record carries (`TwinOptions` in C#). */
export interface TwinOptions {
  style?: string | null;
}

/** The module's channel to its twin: decision points and callbacks, one message per state change. */
export interface TwinChannel {
  /**
   * Resolves a decision point. On WebAssembly the twin's C# chain runs synchronously inside the dispatch and its
   * verdict is returned; elsewhere the fallback policy is returned without a call. The mode is detected from the
   * runtime, per channel, by one probe (`Ping`) on the first decision: Blazor Server's dispatcher throws
   * synchronously on `invokeMethod`, WebAssembly's answers.
   */
  decide(point: string, event: Event | undefined, fallback: DecisionPolicy): DecisionPolicy;
  /** Whether `decide` ran the C# chain on the last call (`true`) or returned the fallback (`false`). */
  readonly lastDecisionWasSynchronous: boolean;
  /** Sends one callback payload to the twin's `[JSInvokable]` method; never awaited by the module. */
  report(methodName: string, payload: unknown): void;
}

export function createTwinChannel(twin: DotNetTwin): TwinChannel {
  let synchronous: boolean | undefined;
  let lastSynchronous = false;

  function supportsSynchronousInvoke(): boolean {
    if (synchronous === undefined) {
      try {
        synchronous = twin.invokeMethod<boolean>('Ping') === true;
      } catch {
        synchronous = false;
      }
    }
    return synchronous;
  }

  return {
    decide(point, event, fallback) {
      if (!supportsSynchronousInvoke()) {
        lastSynchronous = false;
        return fallback;
      }
      lastSynchronous = true;
      return twin.invokeMethod<DecisionPolicy>('Decide', point, JSON.stringify(eventInfo(event)));
    },
    get lastDecisionWasSynchronous() {
      return lastSynchronous;
    },
    report(methodName, payload) {
      void twin.invokeMethodAsync<void>(methodName, JSON.stringify(payload));
    },
  };
}

/** Resolves `attach`'s first argument: the element itself or the part's generated id (substitute callback, ADR 02). */
export function resolveElement(elementOrId: Element | string): HTMLElement {
  const element = typeof elementOrId === 'string' ? document.getElementById(elementOrId) : elementOrId;
  if (!(element instanceof HTMLElement)) {
    throw new Error(`Blazix: no element for ${typeof elementOrId === 'string' ? `id '${elementOrId}'` : 'the reference'}`);
  }
  return element;
}

export function parseOptions<T extends TwinOptions>(optionsJson: string): T {
  return JSON.parse(optionsJson) as T;
}

/**
 * Handover at attach (ADR 04 Amendments 2026-10-18): from `attach` on, JavaScript composes `element.style` from the
 * consumer's merged style string plus the module's own declarations, applied by the caller after this.
 */
export function applyConsumerStyle(element: HTMLElement, options: TwinOptions): void {
  element.style.cssText = options.style ?? '';
}
