import { test as base, type BrowserContext, type Page, type PlaywrightWorkerOptions, type TestInfo } from '@playwright/test';

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

type TraceOption = PlaywrightWorkerOptions['trace'];

/**
 * The options the runner traces a test with, or `undefined` when its mode records nothing for this run (Playwright 1.64's
 * `TestTracing.startIfNeeded` and `_shouldCaptureTrace`).
 */
function runnerTraceOptions(trace: TraceOption, retry: number) {
  const defaults = { screenshots: true, snapshots: true, sources: true };
  const options = typeof trace === 'string' ? { ...defaults, mode: trace } : { ...defaults, ...trace };
  const mode = options.mode === 'retry-with-trace' ? 'on-first-retry' : options.mode;
  const records =
    mode === 'on' ||
    mode === 'retain-on-failure' ||
    mode === 'retain-on-failure-and-retries' ||
    (mode === 'on-first-retry' && retry === 1) ||
    (mode === 'on-all-retries' && retry > 0) ||
    (mode === 'retain-on-first-failure' && retry === 0);
  return records ? options : undefined;
}

/**
 * ADR 11 Amendments 2026-11-04 (per-test tracing): gives the test a trace of its own on the worker's shared context.
 * The runner traces each test as a chunk of that context's trace, and a browser context's tracer keeps its network log
 * across chunks, so every chunk copied the log of every earlier test in the worker and the volume grew with the square
 * of the worker's test count. Restarting the tracer after the runner's chunk has started starts an empty log; the
 * runner's own `stopChunk` at the end of the test archives this one, so a failure keeps its trace (DOM snapshots
 * included) and a pass is discarded as before.
 */
async function restartTracing(context: BrowserContext, trace: TraceOption, testInfo: TestInfo): Promise<void> {
  const options = runnerTraceOptions(trace, testInfo.retry);
  if (!options) {
    return;
  }

  await context.tracing.stop();
  await context.tracing.start({
    screenshots: options.screenshots,
    snapshots: options.snapshots,
    sources: options.sources,
    name: `${testInfo.testId}-shared${testInfo.retry ? `-retry${testInfo.retry}` : ''}`,
    title: testInfo.titlePath.join(' › '),
  });
}

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

  // Every test gets its own page in the worker's context, and its own trace of that context.
  page: async ({ workerContext, mode, trace }, use, testInfo) => {
    await restartTracing(workerContext, trace, testInfo);
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
