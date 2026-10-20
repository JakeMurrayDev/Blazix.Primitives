import { test, expect, gotoFixture, advanceTime, readDiagnostics } from './helpers';

// The harness canary (issue 33, deliverable 6): a plain fixture component attaches the `probe` twin and module and
// drives every edge of the twin contract of ADR 04 §3 through the DOM: attach with the consumer's style, a
// `[JSInvokable]` callback flipping a data attribute, `update(options)`, `dispose()`, and `decide()`.
test.describe('probe twin', () => {
  test('attaches the module and flips a data attribute through a [JSInvokable] callback on click', async ({ page, mode }) => {
    const probe = await gotoFixture(page, mode, 'probe');
    const button = probe.locator('[data-probe]');

    await expect(button).toHaveAttribute('data-probe-attached', 'true');
    await expect(probe).toHaveAttribute('data-flipped', 'false');

    await button.click();

    await expect(probe).toHaveAttribute('data-flipped', 'true');
  });

  test('the consumer style travels on attach and on update (handover at attach)', async ({ page, mode }) => {
    const probe = await gotoFixture(page, mode, 'probe');
    const button = probe.locator('[data-probe]');

    await expect(button).toHaveCSS('color', 'rgb(1, 2, 3)');

    await probe.locator('[data-probe-update]').click();

    await expect(button).toHaveCSS('color', 'rgb(4, 5, 6)');
  });

  test('dispose detaches the module and a later click reaches nothing', async ({ page, mode }) => {
    const probe = await gotoFixture(page, mode, 'probe');
    const button = probe.locator('[data-probe]');

    await button.click();
    await expect(probe).toHaveAttribute('data-clicks', '1');

    await probe.locator('[data-probe-dispose]').click();
    await expect(button).toHaveAttribute('data-probe-attached', 'false');

    await button.click();
    // Blazor's own handler still counts the click; the module's callback does not.
    await expect(probe).toHaveAttribute('data-blazor-clicks', '2');
    await expect(probe).toHaveAttribute('data-clicks', '1');
  });

  test('decide() runs the C# chain synchronously on WebAssembly and returns the fallback elsewhere', async ({ page, mode }) => {
    const probe = await gotoFixture(page, mode, 'probe');
    const button = probe.locator('[data-probe]');

    await button.click();

    await expect(button).toHaveAttribute('data-verdict', 'run');
    await expect(button).toHaveAttribute('data-verdict-source', mode === 'wasm' ? 'twin' : 'fallback');
  });
});

test.describe('harness channels', () => {
  test('advanceTime drives a C# timer under the fixture TimeProvider without waiting', async ({ page, mode }) => {
    const probe = await gotoFixture(page, mode, 'probe');

    await probe.locator('[data-probe-timer]').click();
    await expect(probe).toHaveAttribute('data-timer', 'armed');

    await advanceTime(page, probe, 9_999);
    await expect(probe).toHaveAttribute('data-timer', 'armed');

    await advanceTime(page, probe, 1);
    await expect(probe).toHaveAttribute('data-timer', 'fired');
  });

  test('readDiagnostics returns the warnings this page logged and nothing from other pages', async ({ page, mode }) => {
    const probe = await gotoFixture(page, mode, 'probe');

    await probe.locator('[data-probe-warn]').click();
    await expect(probe).toHaveAttribute('data-warnings', '1');

    await expect
      .poll(async () => (await readDiagnostics(page, mode, probe)).filter((d) => d.level === 'Warning'))
      .toEqual([
        expect.objectContaining({
          category: 'Blazix.Primitives.Fixtures.Client.Probe.ProbeFixture',
          message: expect.stringContaining('probe warning 1'),
        }),
      ]);
  });
});
