import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { gotoCombobox, type FixtureOptions } from './combobox.helpers';
import { pressControl, settle } from './suites/popupFixture';

// CMB-ARIA-13's titles (ComboboxStatus.test.tsx, ComboboxStatus.iOS.test.tsx and ComboboxEmpty.test.tsx's a11y blocks; M5
// track 10, issue 174; ported by session S): Base UI's titles verbatim, at L4 on both render modes, on the
// `combobox-live-regions` fixture (tests/Blazix.Primitives.Fixtures.Client/Combobox/L4/ComboboxLiveRegionsL4Fixture.razor).
// The initial live-region text mutation is the `dom` module's (`markLiveRegion`, UTL-A11Y-01..04), which bUnit cannot see.
// Base UI's fake clock becomes an init script that holds the module's 200 ms reset until a title runs it, so `tick` is
// Base UI's `clock.tick(INITIAL_LIVE_REGION_TEXT_MUTATION_RESET_DELAY)`; a reset that is never run cannot restore the text.

const FIXTURE = 'combobox-live-regions';
const RESET_DELAY = 200;
const MARKER = '⁠';

type HeldResets = Map<number, () => void>;

function goto(page: Page, mode: RenderMode, options: FixtureOptions): Promise<Locator> {
  return gotoCombobox(page, mode, FIXTURE, options);
}

/** Holds, from the page's first script on, every `setTimeout` of the reset delay until `tick` runs it. */
async function holdResets(page: Page): Promise<void> {
  await page.addInitScript((delay) => {
    const held = new Map<number, () => void>();
    let next = -1;
    const realSet = window.setTimeout.bind(window);
    const realClear = window.clearTimeout.bind(window);
    (window as unknown as { liveRegionResets: HeldResets }).liveRegionResets = held;
    window.setTimeout = ((handler: TimerHandler, timeout?: number, ...args: unknown[]) => {
      if (timeout === delay && typeof handler === 'function') {
        const id = next--;
        held.set(id, () => (handler as (...values: unknown[]) => void)(...args));
        return id;
      }
      return realSet(handler, timeout, ...args);
    }) as typeof window.setTimeout;
    window.clearTimeout = ((id?: number) => {
      if (id !== undefined && held.delete(id)) {
        return;
      }
      realClear(id);
    }) as typeof window.clearTimeout;
  }, RESET_DELAY);
}

/** Base UI's `clock.tick(INITIAL_LIVE_REGION_TEXT_MUTATION_RESET_DELAY)`: runs the held resets. */
async function tick(page: Page): Promise<void> {
  await page.evaluate(() => {
    const held = (window as unknown as { liveRegionResets: HeldResets }).liveRegionResets;
    for (const [id, run] of [...held]) {
      held.delete(id);
      run();
    }
  });
}

function heldResets(page: Page): Promise<number> {
  return page.evaluate(() => (window as unknown as { liveRegionResets: HeldResets }).liveRegionResets.size);
}

function textOf(locator: Locator): Promise<string | null> {
  return locator.evaluate((element) => element.textContent);
}

function tagOf(locator: Locator): Promise<string> {
  return locator.evaluate((element) => element.tagName);
}

/** Two animation frames: the module writes the marker after paint. */
function afterPaint(page: Page): Promise<void> {
  return page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
}

test.describe('<Combobox.Status />', () => {
  test.describe('a11y', () => {
    test.beforeEach(async ({ page }) => {
      await holdResets(page);
    });

    // CMB-ARIA-13
    test('removes the initial text mutation after the reset delay', async ({ page, mode }) => {
      await goto(page, mode, { case: 'status', text: 'Searching…' });
      const status = page.getByTestId('status');

      await expect(page.getByRole('status')).toHaveAttribute('data-testid', 'status');
      await expect.poll(() => textOf(status)).toBe(`Searching…${MARKER}`);

      await tick(page);

      expect(await textOf(status)).toBe('Searching…');
    });

    // CMB-ARIA-13
    test('updates content immediately after the live region has mounted', async ({ page, mode }) => {
      const fixture = await goto(page, mode, { case: 'status' });
      const status = page.getByTestId('status');

      await expect(page.getByRole('status')).toHaveAttribute('data-testid', 'status');
      await expect(status).toHaveText('');

      await pressControl(page.getByTestId('set-status'));

      // The reset is held: a marker on the new text would stay.
      await expect.poll(() => textOf(status)).toBe('Searching…');
      await settle(fixture);
      await afterPaint(page);
      expect(await textOf(status)).toBe('Searching…');
    });

    // CMB-ARIA-13
    test('preserves a custom render prop on the visible element', async ({ page, mode }) => {
      await goto(page, mode, { case: 'status-render' });
      const status = page.getByTestId('custom-status');

      expect(await tagOf(status)).toBe('P');
      await expect(page.getByRole('status')).toHaveAttribute('data-testid', 'custom-status');
      await expect.poll(() => textOf(status)).toBe(`Searching…${MARKER}`);

      await tick(page);

      expect(await textOf(status)).toBe('Searching…');
    });

    // CMB-ARIA-13. Base UI unmounts the rendered tree; the fixture's `unmount` control removes the root. The reset stays
    // held, so only the unmount can restore the text.
    test('restores the marker before unmounting during the reset delay', async ({ page, mode }) => {
      await goto(page, mode, { case: 'status', text: 'Searching…' });
      const status = page.getByTestId('status');
      await expect.poll(() => textOf(status)).toBe(`Searching…${MARKER}`);
      const element = await status.elementHandle();

      await pressControl(page.getByTestId('unmount'));

      await expect(status).toHaveCount(0);
      await expect.poll(() => element!.evaluate((node) => node.textContent)).toBe('Searching…');
    });
  });
});

test.describe('<Combobox.Status /> iOS', () => {
  test.beforeEach(async ({ page }) => {
    // Base UI mocks `platform.os.ios`; the port's `platform` module reads `navigator.platform` once, when it loads.
    await page.addInitScript(() => {
      Object.defineProperty(Navigator.prototype, 'platform', { get: () => 'iPhone', configurable: true });
    });
    await holdResets(page);
  });

  // CMB-ARIA-13
  test('skips the initial text mutation', async ({ page, mode }) => {
    const fixture = await goto(page, mode, { case: 'status', text: 'Searching…' });
    const status = page.getByTestId('status');

    await expect(page.getByRole('status')).toHaveAttribute('data-testid', 'status');
    // The Status attaches in its first render; past a round trip and a paint the marker would have been written.
    await settle(fixture);
    await afterPaint(page);
    expect(await textOf(status)).toBe('Searching…');
    expect(await heldResets(page)).toBe(0);

    await tick(page);

    expect(await textOf(status)).toBe('Searching…');
  });
});

test.describe('<Combobox.Empty />', () => {
  test.describe('a11y', () => {
    test.beforeEach(async ({ page }) => {
      await holdResets(page);
    });

    // CMB-ARIA-13
    test('removes the initial text mutation after the reset delay', async ({ page, mode }) => {
      await goto(page, mode, { case: 'empty' });
      const empty = page.getByTestId('empty');

      await expect(page.getByRole('status')).toHaveAttribute('data-testid', 'empty');
      await expect.poll(() => textOf(empty)).toBe(`No results${MARKER}`);

      await tick(page);

      expect(await textOf(empty)).toBe('No results');
    });

    // CMB-ARIA-13
    test('updates the live region immediately when the empty content appears after mount', async ({ page, mode }) => {
      const fixture = await goto(page, mode, { case: 'empty-appears' });
      const empty = page.getByTestId('empty');

      await expect(empty).toHaveText('');
      await expect(page.getByRole('status')).toHaveAttribute('data-testid', 'empty');

      await pressControl(page.getByTestId('empty-items'));

      // The reset is held: a marker on the new text would stay.
      await expect.poll(() => textOf(empty)).toBe('No results');
      await settle(fixture);
      await afterPaint(page);
      expect(await textOf(empty)).toBe('No results');
    });

    // CMB-ARIA-13
    test('preserves a custom render prop on the visible element', async ({ page, mode }) => {
      await goto(page, mode, { case: 'empty-render' });
      const empty = page.getByTestId('custom-empty');

      expect(await tagOf(empty)).toBe('P');
      await expect(page.getByRole('status')).toHaveAttribute('data-testid', 'custom-empty');
      await expect.poll(() => textOf(empty)).toBe(`No results${MARKER}`);

      await tick(page);

      expect(await textOf(empty)).toBe('No results');
    });
  });
});
