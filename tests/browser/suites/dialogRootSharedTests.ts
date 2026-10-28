import { type Page } from '@playwright/test';
import { expect, test } from '../helpers';
import { gotoPopupFixture, openChanges } from './popupFixture';

// Base UI's dialogRootSharedTests (packages/react/test/dialogRootSharedTests.tsx at v1.9.0), titles verbatim, as a
// Playwright helper (ADR 09 Amendments 2026-10-27; M4 track 8). Root behaviour Dialog and AlertDialog share: the alert mode
// only changes modality, pointer dismissal and the popup role. The family's fixture page plays Base UI's trees by `case`:
// `trigger-close`, `prevent-unmount`, `prevent-once`, `close-method` and `payload`, with the buttons `[data-unmount]`,
// `[data-close]`, `[data-open-with-payload]` and `[data-handle-close]` standing for the actions ref and the handle, and the
// container's `data-open-change-triggers` for each change's trigger id (tests/browser/CONTEXT.md, the suites).

/** The options of dialogRootSharedTests (Base UI's DialogRootSharedTestsConfig, the parts replaced by a fixture). */
export interface DialogRootSharedTestsConfig {
  /** The fixture page. */
  fixture: string;
  /** The `role` of the popup element. */
  popupRole: 'dialog' | 'alertdialog';
}

export function dialogRootSharedTests(config: DialogRootSharedTestsConfig): void {
  const { fixture: fixtureName, popupRole } = config;
  // Scoped to the portal: a hidden-role query of the page also finds Blazor's own reconnect <dialog>.
  const popup = (page: Page) => page.locator(`[data-base-ui-portal] [role="${popupRole}"]:not([hidden])`);
  const anyPopup = (page: Page) => page.locator(`[data-base-ui-portal] [role="${popupRole}"]`);

  test.describe('Dialog root shared behavior', () => {
    test.describe('prop: onOpenChange', () => {
      test('reports the trigger and reason when opened by a trigger and closed by a close button', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, fixtureName, { case: 'trigger-close' });
        const trigger = page.getByRole('button', { name: 'Open', exact: true, includeHidden: true });
        const triggerId = await trigger.getAttribute('id');

        await trigger.click();
        await expect.poll(() => openChanges(fixture)).toEqual(['true:trigger-press']);
        await expect(fixture).toHaveAttribute('data-open-change-triggers', `${triggerId}`);

        await page.getByRole('button', { name: 'Close', exact: true }).click();
        await expect.poll(() => openChanges(fixture)).toEqual(['true:trigger-press', 'false:close-press']);
        await expect(fixture).toHaveAttribute('data-open-change-triggers', `${triggerId} ${triggerId}`);
      });
    });

    test.describe('prop: actionsRef', () => {
      test('unmounts the popup when the `unmount` method is called', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, fixtureName, { case: 'prevent-unmount' });
        const trigger = page.getByRole('button', { name: 'Open', exact: true, includeHidden: true });

        await trigger.click();
        await expect(popup(page)).toBeVisible();
        await trigger.click();
        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(anyPopup(page)).toHaveAttribute('data-closed', '');

        await fixture.locator('[data-unmount]').click();
        await expect(anyPopup(page)).toHaveCount(0);
      });

      test('clears manual unmount state after the `unmount` method is called', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, fixtureName, { case: 'prevent-once' });
        const trigger = page.getByRole('button', { name: 'Open', exact: true, includeHidden: true });

        await trigger.click();
        await expect(popup(page)).toBeVisible();
        await page.getByRole('button', { name: 'Close', exact: true }).click();
        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(anyPopup(page)).toHaveAttribute('data-closed', '');
        await fixture.locator('[data-unmount]').click();
        await expect(anyPopup(page)).toHaveCount(0);

        // The next close unmounts on its own again.
        await trigger.click();
        await expect(popup(page)).toBeVisible();
        await page.getByRole('button', { name: 'Close', exact: true }).click();
        await expect(anyPopup(page)).toHaveCount(0);
      });

      test('closes the popup when the `close` method is called', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, fixtureName, { case: 'close-method' });
        await expect(popup(page)).toBeVisible();

        await fixture.locator('[data-close]').click();

        await expect(popup(page)).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual(['false:imperative-action']);
      });
    });

    test.describe('imperative actions on the handle', () => {
      test('opens with a payload set programmatically', async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, fixtureName, { case: 'payload' });
        const trigger1 = page.getByRole('button', { name: 'Trigger 1', includeHidden: true });
        const trigger2 = page.getByRole('button', { name: 'Trigger 2', includeHidden: true });
        await expect(popup(page)).toHaveCount(0);

        await fixture.locator('[data-open-with-payload]').click();
        await expect(popup(page)).toHaveCount(1);
        await expect(page.getByTestId('content')).toHaveText('8');
        await expect(trigger1).toHaveAttribute('aria-expanded', 'false');
        await expect(trigger2).toHaveAttribute('aria-expanded', 'false');

        await fixture.locator('[data-handle-close]').click();
        await expect(popup(page)).toHaveCount(0);
      });
    });
  });
}
