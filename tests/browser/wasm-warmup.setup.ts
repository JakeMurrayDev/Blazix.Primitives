import { test, expect, gotoFixture } from './helpers';

// The `wasm` project depends on this setup project (playwright.config.ts). One WebAssembly page is opened and
// brought to interactive so the host's asset pipeline is warm and a broken boot fails here, once, with one message.
// The positioning entry, the largest module (it bundles the Floating UI engine), is requested once too, so the first
// positioned title does not pay the host's first serve of it (ADR 04 Amendments 2026-10-27). The `server` project
// does not wait for this setup, by design (it never pays the WebAssembly boot): its first positioned title per worker
// fetches the module from the local host, which the timeouts of that project cover.
test('warms up the WebAssembly fixture host', async ({ page, mode }) => {
  const probe = await gotoFixture(page, mode, 'probe');
  await expect(probe).toHaveAttribute('data-renderer', 'WebAssembly');
  const positioning = await page.evaluate(async () => (await fetch('/_content/Blazix.Primitives/js/positioning.js')).status);
  expect(positioning).toBe(200);
});
