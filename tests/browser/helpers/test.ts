import { test as base, type BrowserContext, type Page } from '@playwright/test';

export type RenderMode = 'server' | 'wasm';

type TestOptions = {
  /** The render mode this project exercises; the route segment of every fixture URL (ADR 06 §2). */
  mode: RenderMode;
};

type WorkerFixtures = {
  /**
   * One browser context per worker. Pages of the same worker share its HTTP cache, so on `wasm` the runtime
   * download is paid once per worker rather than once per test. Playwright contexts never share a cache across
   * workers; the `wasm-warmup` project warms the host's side (first-request asset pipeline) and fails fast.
   */
  workerContext: BrowserContext;
};

export const test = base.extend<TestOptions, WorkerFixtures>({
  mode: ['server', { option: true }],

  workerContext: [
    async ({ browser }, use) => {
      const context = await browser.newContext();
      await use(context);
      await context.close();
    },
    { scope: 'worker' },
  ],

  // Every test gets its own page in the worker's context.
  page: async ({ workerContext, mode }, use) => {
    const page = await workerContext.newPage();
    // ADR 06 §2: browser timers run under page.clock. The clock is installed running (auto-advancing), never paused
    // by default, so SignalR keep-alive keeps flowing on `server` (issue 14); a test that needs frozen time pauses it.
    await page.clock.install();
    void mode;
    await use(page);
    await page.close();
  },
});

export { expect } from '@playwright/test';
export type { Page };
