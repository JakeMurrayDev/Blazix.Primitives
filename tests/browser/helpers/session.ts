import { type Locator, type Page } from '@playwright/test';
import type { RenderMode } from './test';

/** One captured diagnostic from the fixture host's logger provider. */
export interface Diagnostic {
  level: string;
  category: string;
  message: string;
}

declare global {
  interface Window {
    blazixFixture: {
      advanceTime(token: string, milliseconds: number): Promise<void>;
      diagnostics(token: string): Promise<Diagnostic[]>;
    };
  }
}

/** The per-page session token the fixture container renders as `data-session`. */
export async function sessionToken(container: Locator): Promise<string> {
  const token = await container.first().getAttribute('data-session');
  if (!token) {
    throw new Error('The fixture container renders no data-session token');
  }
  return token;
}

/**
 * Advances the C# `TimeProvider` of this page's circuit (or WebAssembly app) by `milliseconds`. The call goes
 * through the page's JS interop so it runs on the renderer's dispatcher in both modes and is scoped to this page.
 */
export async function advanceTime(page: Page, container: Locator, milliseconds: number): Promise<void> {
  const token = await sessionToken(container);
  await page.evaluate(([t, ms]) => window.blazixFixture.advanceTime(t, ms), [token, milliseconds] as const);
}

/**
 * Reads the diagnostics the host captured for this page's session: on `server` through the test-only endpoint
 * (readable even after the circuit is gone), on `wasm` through the page hook (the only channel into the client).
 */
export async function readDiagnostics(page: Page, mode: RenderMode, container: Locator): Promise<Diagnostic[]> {
  const token = await sessionToken(container);
  if (mode === 'server') {
    const response = await page.request.get(`/_fixture/diagnostics/${token}`);
    if (!response.ok()) {
      throw new Error(`diagnostics endpoint answered ${response.status()}`);
    }
    return (await response.json()) as Diagnostic[];
  }
  return page.evaluate((t) => window.blazixFixture.diagnostics(t), token);
}
