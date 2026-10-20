// The JS initializer, built to wwwroot/Blazix.Primitives.lib.module.js. Importing `platform` computes the
// flags once per page; the modules share the same chunk, so no module recomputes them. Custom event
// types are registered here as they appear (none yet).
import { platform } from './platform';

interface BlazorLike {
  registerCustomEventType(eventName: string, options: object): void;
}

function registerCustomEventTypes(_blazor: BlazorLike): void {
  // None yet.
}

/**
 * Blazor Web App hook, called after blazor.web.js has started.
 * @param blazor The Blazor object, used to register custom event types.
 */
export function afterWebStarted(blazor: BlazorLike): void {
  void platform;
  registerCustomEventTypes(blazor);
}

/**
 * Hook for a standalone Server or WebAssembly host that is not a Web App.
 * @param blazor The Blazor object, used to register custom event types.
 */
export function afterStarted(blazor: BlazorLike): void {
  void platform;
  registerCustomEventTypes(blazor);
}
