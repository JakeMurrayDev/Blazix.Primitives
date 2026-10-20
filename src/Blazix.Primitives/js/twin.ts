// The JavaScript half of the twin contract: what every module receives from `attach` and what it
// hands back. No module-level registries: everything a handle needs lives in the handle's closure.
import { eventInfo } from './event-info';

/** The twin's `DotNetObjectReference`, as Blazor's JS interop revives it. */
export interface DotNetTwin {
  invokeMethod<T>(methodName: string, ...args: unknown[]): T;
  invokeMethodAsync<T>(methodName: string, ...args: unknown[]): Promise<T>;
}

// Mirrors `DecisionPolicy` in C#.
/** The verdict of a decision point, and the fallback policies a module applies when C# cannot be asked synchronously. */
export type DecisionPolicy = 'run' | 'stop' | 'start' | 'prevent-and-stop';

/** What `attach` returns to C#: the per-element handle with `update(optionsJson)` and `dispose()`. */
export interface TwinHandle {
  update(optionsJson: string): void;
  dispose(): void;
}

/** The fields every options record carries; mirrors `TwinOptions` in C#. */
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

/**
 * Creates a module's channel to its twin.
 * @param twin The twin's `DotNetObjectReference` received with `attach`.
 * @returns The channel; its synchronous-invoke detection is cached for the channel's lifetime.
 */
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

// A substitute callback leaves the part without an ElementReference, so the generated id is passed instead.
/**
 * Resolves `attach`'s first argument: the element itself, or the id of the element in the document.
 * @param elementOrId The element, or its `id` attribute.
 * @returns The HTML element.
 * @throws When no element has that id or the reference is not an HTML element.
 */
export function resolveElement(elementOrId: Element | string): HTMLElement {
  const element = typeof elementOrId === 'string' ? document.getElementById(elementOrId) : elementOrId;
  if (!(element instanceof HTMLElement)) {
    throw new Error(`Blazix: no element for ${typeof elementOrId === 'string' ? `id '${elementOrId}'` : 'the reference'}`);
  }
  return element;
}

/**
 * Parses the options JSON received with `attach` or `update`.
 * @param optionsJson The JSON string the twin serialised.
 * @returns The options record.
 */
export function parseOptions<T extends TwinOptions>(optionsJson: string): T {
  return JSON.parse(optionsJson) as T;
}

/**
 * Applies the consumer's merged style string. From `attach` on, JavaScript owns `element.style` and composes it from
 * this string plus the module's own declarations, which the caller applies after this.
 * @param element The host element.
 * @param options The options received with `attach` or `update`.
 */
export function applyConsumerStyle(element: HTMLElement, options: TwinOptions): void {
  element.style.cssText = options.style ?? '';
}
