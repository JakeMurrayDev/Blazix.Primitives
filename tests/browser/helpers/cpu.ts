import { type Page } from '@playwright/test';

/**
 * Slows the page's main thread by `rate` through CDP's `Emulation.setCPUThrottlingRate` (Chromium only), for a title that
 * must hold on a slow host (issue 154: a CI runner's WebAssembly page). It lasts for the page, which the worker's context
 * closes after the test. On Server it slows only the browser's side; the circuit's latency is `delayCircuit`'s.
 */
export async function throttleCpu(page: Page, rate: number): Promise<void> {
  const session = await page.context().newCDPSession(page);
  await session.send('Emulation.setCPUThrottlingRate', { rate });
}
