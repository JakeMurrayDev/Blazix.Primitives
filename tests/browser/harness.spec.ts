import { test, expect, Fixture, fixtureUrl, gotoFixture } from './helpers';

// Permanent tests for the M0 verification items of ADR 14 §1 and §2 that the browser harness can observe.
// Each title carries its `@verifies ADR-NN/n` tag (ADR 14 §3).

test.describe('fixture host', () => {
  test('@verifies ADR-09/1 the route segment selects the render mode and the fixture renders without prerendering', async ({ page, mode }) => {
    const response = await page.request.get(fixtureUrl(mode, 'probe'));
    expect(response.status()).toBe(200);
    // Prerender off: the static response carries the component marker only, never the fixture's markup.
    expect(await response.text()).not.toContain('data-fixture="probe"');

    const probe = await gotoFixture(page, mode, 'probe');
    await expect(probe).toHaveAttribute('data-renderer', mode === 'server' ? 'Server' : 'WebAssembly');
  });

  test('@verifies ADR-09/2 webServer started the host through dotnet run and its readiness endpoint answers', async ({ page }) => {
    const response = await page.request.get('/_fixture/ready');
    expect(response.status()).toBe(200);
    expect(await response.text()).toBe('ready');
  });
});

test.describe('prerender fixtures', () => {
  test('@verifies ADR-06/1 RendererInfo.IsInteractive is false in prerender and true afterwards', async ({ page, mode }) => {
    const response = await page.request.get(fixtureUrl(mode, 'prerender'));
    const html = await response.text();
    expect(html).toContain('data-fixture="prerender"');
    expect(html).toContain('data-interactive="false"');
    expect(html).not.toContain('data-interactive="true"');

    const roots = await gotoFixture(page, mode, 'prerender');
    await expect(roots).toHaveCount(2);
    for (const root of await roots.all()) {
      await expect(root).toHaveAttribute('data-interactive', 'true');
    }
  });

  test('@verifies ADR-02/1 interactive activation replaces the prerendered subtree and roots activate in document order', async ({ page, mode }) => {
    const roots = await gotoFixture(page, mode, 'prerender');
    await expect(roots).toHaveCount(2);
    for (const root of await roots.all()) {
      await expect(root).toHaveAttribute('data-interactive', 'true');
    }

    // The host tags every prerendered fixture node with an attribute Blazor never rendered before Blazor starts.
    // A reconciled subtree would keep it; a replaced one cannot.
    await expect(page.locator('[data-prerendered-node]')).toHaveCount(0);

    // Each root takes the next number from a per-circuit counter when it first renders interactively.
    await expect(roots.nth(0)).toHaveAttribute('data-root', '0');
    await expect(roots.nth(0)).toHaveAttribute('data-activation-order', '1');
    await expect(roots.nth(1)).toHaveAttribute('data-root', '1');
    await expect(roots.nth(1)).toHaveAttribute('data-activation-order', '2');
  });
});

test.describe('twin base', () => {
  test('@verifies ADR-04/1 Server preserves the order between JS-to-.NET invocations and Blazor event dispatches on one circuit', async ({ page, mode }) => {
    const probe = await gotoFixture(page, mode, 'probe');
    const button = probe.locator('[data-probe]');

    // The module's element listener reports through the twin before Blazor's document-level dispatcher sees the
    // click; both travel the same channel, so C# must observe them strictly alternating.
    for (let i = 0; i < 5; i += 1) {
      await button.click();
    }

    await expect(probe).toHaveAttribute('data-clicks', '5');
    await expect(probe).toHaveAttribute('data-blazor-clicks', '5');
    await expect(probe).toHaveAttribute('data-log', Array(5).fill('js-click,blazor-click').join(','));
  });

  test('@verifies ADR-04/2 on WebAssembly a render queued inside a synchronous invoke flushes before the JS listener returns', async ({ page, mode }) => {
    test.skip(mode !== 'wasm', 'the synchronous decide() path exists on WebAssembly only (ADR 01 decision 1)');
    const probe = await gotoFixture(page, mode, 'probe');
    const button = probe.locator('[data-probe]');

    await button.click();

    // The C# Decide handler calls StateHasChanged; the module reads the rendered attribute before its listener returns.
    await expect(button).toHaveAttribute('data-verdict-source', 'twin');
    await expect(button).toHaveAttribute('data-flushed-in-dispatch', 'true');
    await expect(probe).toHaveAttribute('data-decisions', '1');
  });
});

test.describe('store', () => {
  test('@verifies ADR-05/3 EqualityComparer<T>.Default does not allocate for value types on the WebAssembly interpreter', async ({ page, mode }) => {
    test.skip(mode !== 'wasm', 'the item is about the WebAssembly runtime; Server runs CoreCLR');
    const probe = await gotoFixture(page, mode, 'allocation');

    await probe.locator('[data-measure]').click();

    await expect(probe).toHaveAttribute('data-iterations', /^[1-9]\d*$/);
    await expect(probe).toHaveAttribute('data-allocated', '0');
  });
});

test.describe('clock', () => {
  test('@verifies ADR-06/2 the server project runs with page.clock installed and a circuit survives longer than the SignalR keep-alive', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'the keep-alive belongs to the Server circuit');
    test.setTimeout(60_000);
    const fixture = await gotoFixture(page, mode, 'keep-alive');

    // The host runs the hub with KeepAliveInterval 2 s and ClientTimeoutInterval 10 s, and the client mirrors them.
    // Waiting for the circuit to keep rendering ticks past the timeout is the assertion, not a sleep.
    await expect(fixture).toHaveAttribute('data-ticks', /^(2[4-9]|[3-9]\d|\d{3,})$/, { timeout: 30_000 });
    await expect(page.locator('#components-reconnect-modal')).toBeHidden();
    await expect(page.locator(Fixture.started)).toHaveCount(1);

    // And the circuit still answers input after that.
    await fixture.locator('[data-ping]').click();
    await expect(fixture).toHaveAttribute('data-pings', '1');
  });
});
