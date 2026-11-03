import { expect, test, type Page } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { gotoPopupFixture, settle } from './suites/popupFixture';
import { popupConformanceTests } from './suites/popupConformanceTests';

// The [L4] rows of docs/architecture/popup-store-and-handles.md (PSH) and transitions-and-mounting.md (TRN) on the
// test-only popup host (FixturePopupRoot, Trigger, Portal, Popup and Close; settled by the orchestrator 2026-10-27, plan
// §Settled "Test-only hosts and the shared suites"). The shared suites run here with the host's values: a trigger pressed
// by click from a C# onclick (design point 4), role and aria-haspopup `dialog`, an exit animation. Every test runs on both
// render modes. Titles that press Escape are DIS-01's: the host's root composes track 3's `interactions` session with
// Escape dismissal (M4 track 3, issue 119), so they run here.

// No title here asserts the `interactions` module's press semantics (a press's pointer type, open on press, the mixed
// toggle): each presses by click through the C# onclick, so none is listed open to track 3 for press semantics.
popupConformanceTests({
  fixture: 'popup-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'dialog',
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

// ADR 04 Amendments 2026-10-28 (design point 9 of M4 track 8, settled by the orchestrator 2026-10-28): Blazor revives an
// ElementReference with a document query, which pierces no shadow root, and the parts inside a portal attach after the
// move; the twins send the capture ids and the modules find the elements in the shadow roots the portal registered. The
// popup's own `transitions` handle reports its open and its close completion, which it cannot do without its element.
test.describe('a portal moved into a closed shadow root', () => {
  test('port: the popup inside it attaches its handles after the move, reports its completions, and Escape closes it', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-conformance', { container: 'shadow' });
    const shadowRoot = () =>
      page.evaluate(() => (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot | null } }).blazixFixtureDom.lastClosedShadowRoot != null);
    const insideShadowRoot = () =>
      page.evaluate(() => {
        const root = (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot | null } }).blazixFixtureDom.lastClosedShadowRoot;
        return root?.querySelector('[data-testid="popup"]') != null;
      });
    await expect.poll(shadowRoot).toBe(true);
    await settle(fixture);

    await page.getByTestId('trigger').click();
    await expect.poll(insideShadowRoot).toBe(true);
    await expect(page.locator('[data-testid="popup"]')).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');

    await page.keyboard.press('Escape');

    await expect.poll(insideShadowRoot).toBe(false);
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press false:escape-key');
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
  });
});

// FLT-STR-11 (M5 track 2, brief deliverable 1; ADR 05 Amendments 2026-11-03): the standalone host composes the open
// lifecycle alone, with an `interactions` session on its token and a transitions-watched popup, and no trigger registry,
// payload or handle. The session dismisses on Escape only while it knows the popup is open, so an Escape close proves the
// committed state and its relay reached it.
test.describe('the standalone lifecycle host', () => {
  test('port: opens without a trigger and closes on Escape, the session knowing each committed change', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-lifecycle');
    const popup = page.getByTestId('popup');

    await fixture.locator('[data-open-button]').click();

    await expect(popup).toHaveAttribute('data-open', '');
    await expect(popup).toHaveAttribute('role', 'dialog');
    expect(await popup.getAttribute('id')).toBeTruthy();
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');

    await page.keyboard.press('Escape');

    await expect(popup).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press false:escape-key');
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
  });

  test('port: the popup is added with its enter transition and removed after its exit transition, in order', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-lifecycle', { animations: 1 });
    await recordLifecycleMarks(page);

    await fixture.locator('[data-open-button]').click();
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('popup')).toHaveCount(0);

    const marks = await page.evaluate(() => (window as unknown as { lifecycleMarks: string[] }).lifecycleMarks);
    expect(marks[0]).toBe('added');
    expect(marks.indexOf('data-starting-style')).toBeGreaterThan(0);
    expect(marks.indexOf('data-ending-style')).toBeGreaterThan(marks.indexOf('data-starting-style'));
    expect(marks.at(-1)).toBe('removed');
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
  });

  // TRN-ANI-22's mechanism (useUnmountAfterClose.ts:102-116, useOpenChangeComplete.tsx:24-26): the unmount action while the
  // popup is closed and still animating out unmounts it in the same commit, and its exit completes no second time.
  test('port: an unmount while closing removes the popup at once, and its exit completes no second time', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-lifecycle', { animations: 1, exit: 'long' });
    const popup = page.getByTestId('popup');
    await fixture.locator('[data-open-button]').click();
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
    await page.keyboard.press('Escape');
    await expect(popup).toHaveAttribute('data-ending-style', '');

    await fixture.locator('[data-unmount-button]').click();

    await expect(popup).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
    await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-press false:escape-key');
  });
});

/** Records, in order, the popup's insertion (`added`), its status attributes as they appear, and its removal (`removed`). */
async function recordLifecycleMarks(page: Page): Promise<void> {
  await page.evaluate(() => {
    const marks: string[] = [];
    (window as unknown as { lifecycleMarks: string[] }).lifecycleMarks = marks;
    const isPopup = (node: Node): node is Element => node instanceof Element && node.matches('[data-testid="popup"]');
    new MutationObserver((records) => {
      for (const record of records) {
        if (record.type === 'childList') {
          for (const node of record.addedNodes) {
            if (isPopup(node)) {
              marks.push('added');
              for (const name of ['data-starting-style', 'data-ending-style']) {
                if (node.hasAttribute(name)) {
                  marks.push(name);
                }
              }
            }
          }

          for (const node of record.removedNodes) {
            if (isPopup(node)) {
              marks.push('removed');
            }
          }
        } else if (isPopup(record.target) && record.attributeName !== null && record.target.hasAttribute(record.attributeName)) {
          marks.push(record.attributeName);
        }
      }
    }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-starting-style', 'data-ending-style'] });
  });
}
