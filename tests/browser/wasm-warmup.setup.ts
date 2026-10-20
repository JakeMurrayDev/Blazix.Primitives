import { test, expect, gotoFixture } from './helpers';

// The `wasm` project depends on this setup project (playwright.config.ts). One WebAssembly page is opened and
// brought to interactive so the host's asset pipeline is warm and a broken boot fails here, once, with one message.
test('warms up the WebAssembly fixture host', async ({ page, mode }) => {
  const probe = await gotoFixture(page, mode, 'probe');
  await expect(probe).toHaveAttribute('data-renderer', 'WebAssembly');
});
