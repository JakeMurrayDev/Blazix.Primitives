import { expect, test, type Page } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { gotoPopupFixture, settle } from './suites/popupFixture';
import { popupConformanceTests } from './suites/popupConformanceTests';

// The [L4] rows of docs/architecture/popup-store-and-handles.md (PSH) and transitions-and-mounting.md (TRN) on the
// test-only popup host (FixturePopupRoot, Trigger, Portal, Popup and Close; settled by the orchestrator 2026-10-27, plan
// §Settled "Test-only hosts and the shared suites"). The shared suites run here with the host's values: a trigger pressed
// by click from a C# onclick (design point 4), role and aria-haspopup `dialog`, an exit animation. Every test runs on both
// render modes. Titles that press Escape are DIS-01's, proven by track 3's `interactions` module.

const escape = 'PSH-DIS-01 (Escape): open to M4 track 3, the interactions module; the popup host has no dismissal';

popupConformanceTests({
  fixture: 'popup-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'dialog',
  open: {
    'opens initially and remains uncontrolled': escape,
    'is ignored when `open={true}` is passed, so Escape cannot close': escape,
    'is called with the new open state and the change details': escape,
  },
});

detachedTriggersConformanceTests({
  fixture: 'popup-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: true,
  close: true,
  open: {
    'allows controlling the open state and active trigger programmatically':
      'PSH-FOC (focus returns to the opener after the close): open to M4 track 4, the focus manager',
  },
});

// The other branch of the handle's missing-trigger behaviour (Dialog's), on the same fixture with `throws=false`.
test.describe('with a handle that does not throw on a missing trigger', () => {
  detachedTriggersConformanceTests({
    fixture: 'popup-triggers',
    openInteractions: ['click'],
    ariaExpanded: true,
    throwOnMissingTrigger: false,
    close: true,
    only: ['opens unassociated with a warning when opened with an unregistered trigger id'],
  });
});

/** Delays every circuit message by `oneWayMs` each way (Server latency; CDP emulation does not delay the circuit). */
async function delayCircuit(page: Page, oneWayMs: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), oneWayMs);
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), oneWayMs);
    });
  });
}

interface RemovalRecord {
  node: 'trigger' | 'popup';
  time: number;
}

/** Records when the given trigger and the popup leave the document, a marker for a state that lasts one round trip. */
async function recordRemovals(page: Page, triggerId: string): Promise<void> {
  await page.evaluate((id) => {
    const records: RemovalRecord[] = [];
    (window as unknown as { removalRecords: RemovalRecord[] }).removalRecords = records;
    new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        for (const node of mutation.removedNodes) {
          if (!(node instanceof HTMLElement)) {
            continue;
          }

          if (node.matches(`[data-testid="${id}"]`)) {
            records.push({ node: 'trigger', time: performance.now() });
          }

          if (node.matches('[data-testid="popup"]') || node.querySelector('[data-testid="popup"]')) {
            records.push({ node: 'popup', time: performance.now() });
          }
        }
      }
    }).observe(document.body, { childList: true, subtree: true });
  }, triggerId);
}

// PSH-STA-23 per render mode (ADR 01's row "PSH-STA-23, FLD-FRM-17"): the check runs at the render-batch boundary, so on
// Server the popup outlives its trigger by at least a round trip; on WebAssembly the close follows in the next batch.
test.describe('PSH-STA-23 at the render-batch boundary', () => {
  const oneWayMs = 150;

  test('closes when the active trigger unregisters while open', async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, oneWayMs);
    }

    const fixture = await gotoPopupFixture(page, mode, 'popup-unmount');
    await page.locator('[data-testid="trigger-1"]').click();
    await expect(page.locator('[data-testid="popup"]')).toBeVisible();
    await recordRemovals(page, 'trigger-1');

    await fixture.locator('[data-remove-trigger="trigger-1"]').click();

    await expect(page.locator('[data-testid="popup"]')).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press false:none');
    const records = await page.evaluate(() => (window as unknown as { removalRecords: RemovalRecord[] }).removalRecords);
    expect(records.map((record) => record.node)).toEqual(['trigger', 'popup']);
    if (mode === 'server') {
      // The boundary runs after the client acknowledged the batch that removed the trigger, then the close renders.
      expect(records[1]!.time - records[0]!.time).toBeGreaterThanOrEqual(2 * oneWayMs - 20);
    }
  });

  test('keeps the popup open when the active trigger is replaced with the same id', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-unmount');
    await page.locator('[data-testid="trigger-1"]').click();
    await expect(page.locator('[data-testid="popup"]')).toBeVisible();

    await fixture.locator('[data-replace-trigger="trigger-1"]').click();
    await expect(page.locator('[data-testid="trigger-1"]')).toHaveAttribute('data-generation', '1');
    await settle(fixture);

    await expect(page.locator('[data-testid="popup"]')).toBeVisible();
    await expect(page.locator('[data-testid="trigger-1"]')).toHaveAttribute('data-popup-open');
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press');
  });
});
