import { expect, type Locator } from '@playwright/test';

/**
 * Waits until a Positioner's first positioning pass has landed. Until then the Positioner is transparent (FLT-STA-09: the
 * inline `opacity: 0` while not positioned, as Base UI's `useAnchorPositioning`), and `toBeVisible()` ignores opacity, so a
 * title that reads a box inside a just-opened popup can read the unpositioned layout, which the pass then moves before the
 * pointer arrives (issue 191). Pass `clockFrozen` when the title paused the page's clock: the pass may wait for a frame,
 * so each sample steps the clock one frame.
 */
export async function waitForPositioned(positioner: Locator, options: { clockFrozen?: boolean } = {}): Promise<void> {
  // A readiness check on a stable end state, the pass having landed (the opacity stays cleared while the popup is open),
  // not a poll over a transient style, so it stays within ADR 06 §2.
  await expect
    .poll(async () => {
      if (options.clockFrozen) {
        await positioner.page().clock.runFor(16);
      }
      return positioner.evaluate((element) => (element as HTMLElement).style.opacity);
    })
    .not.toBe('0');
}
