import { expect, test, type Page, type RenderMode } from './helpers';
import { delayCircuit } from './helpers/circuit';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { dialogRootSharedTests } from './suites/dialogRootSharedTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPopupFixture, openChanges, setRootOpen, settle, type PopupFixtureOptions } from './suites/popupFixture';

// The [L4] rows of docs/components/alert-dialog.md (ADL), both render modes, Server first (ADR 04 §6). Titles are Base UI's
// (alert-dialog/root/AlertDialogRoot.test.tsx at v1.9.0) unless marked "port:". The shared suites run with AlertDialog's
// options (AlertDialogRoot.test.tsx:21-50); the forced regime runs the Dialog rows ADL-API-02 and ADL-DOM-01 name under the
// AlertDialog parts.

test.describe('<AlertDialog.Root />', () => {
  popupConformanceTests({
    fixture: 'alert-dialog-conformance',
    triggerMouseAction: 'click',
    expectedPopupRole: 'alertdialog',
    expectedAriaHasPopupValue: 'dialog',
  });

  dialogRootSharedTests({ fixture: 'alert-dialog-shared', popupRole: 'alertdialog' });
});

// AlertDialogRoot.test.tsx's detachedTriggersConformanceTests: click, aria-expanded, no throw on a missing trigger, closes
// through the Close part (an outside press never closes an alert dialog), no Positioner or Viewport.
detachedTriggersConformanceTests({
  fixture: 'alert-dialog-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: false,
  close: true,
});

const root = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) => gotoPopupFixture(page, mode, 'alert-dialog-root', options);
const alertDialog = (page: Page) => page.getByRole('alertdialog');
// The internal backdrop is the portal node's first element child (DLG-DOM-06). Its data-base-ui-inert marker is not a
// reliable handle: the focus manager's outside marking adds the same marker and removes it at the close.
const internalBackdrop = (page: Page) => page.locator('[data-base-ui-portal] > [role="presentation"]:first-child');

/** Presses the bottom right of the viewport, where only the internal backdrop (or a backdrop part) lies, after checking that. */
async function pressBehind(page: Page, button: 'left' | 'right' = 'left'): Promise<void> {
  const { width, height } = page.viewportSize()!;
  const [x, y] = [width - 10, height - 10];
  expect(await page.evaluate(([px, py]) => document.elementFromPoint(px!, py!)?.closest('[data-base-ui-portal]') !== null, [x, y])).toBe(true);
  await page.mouse.click(x, y, { button });
}

/** Reads whether the page's viewport scroller hides or clips its overflow (DialogRoot.test.tsx's isPageLocked). */
const isPageLocked = (page: Page) =>
  page.evaluate(() => {
    const html = document.documentElement;
    const { overflow, overflowX, overflowY } = getComputedStyle(html);
    const htmlScrolls = /auto|scroll|overlay|hidden|clip/.test(overflow + overflowY + overflowX);
    return /hidden|clip/.test(getComputedStyle(htmlScrolls ? html : document.body).overflowY);
  });

test.describe('<AlertDialog.Root /> in a browser', () => {
  test.describe('prop: onOpenChange', () => {
    test('does not close when the backdrop is clicked', async ({ page, mode }) => {
      const fixture = await root(page, mode, { defaultOpen: true });
      await expect(alertDialog(page)).toBeVisible();

      await pressBehind(page);
      await settle(fixture);

      expect(await openChanges(fixture)).toEqual([]);
      await expect(alertDialog(page)).toBeVisible();
    });
  });

  test.describe('handle-backed roots', () => {
    test('rewires the trigger and keeps pointer dismissal disabled after the root remounts while open', async ({ page, mode }) => {
      const fixture = await root(page, mode, { triggers: 'detached', case: 'remount' });
      const trigger = page.getByTestId('trigger');

      await trigger.click();
      await expect(alertDialog(page)).toBeVisible();
      expect(await trigger.getAttribute('aria-controls')).toBe(await alertDialog(page).getAttribute('id'));

      await alertDialog(page).getByRole('button', { name: 'Unmount root' }).click();
      await expect(alertDialog(page)).toHaveCount(0);

      await page.getByRole('button', { name: 'Remount root' }).click();
      await settle(fixture);
      await expect(alertDialog(page)).toHaveCount(0);

      await trigger.click();
      await expect(alertDialog(page)).toBeVisible();
      expect(await trigger.getAttribute('aria-controls')).toBe(await alertDialog(page).getAttribute('id'));

      await pressBehind(page);
      await settle(fixture);

      await expect(alertDialog(page)).toBeVisible();
      await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
    });

    test('enforces alert dialog state for handle-backed roots', async ({ page, mode }) => {
      const fixture = await root(page, mode, { triggers: 'detached' });

      await page.getByTestId('trigger').click();
      await expect(alertDialog(page)).toBeVisible();
      await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');

      await pressBehind(page);
      await settle(fixture);

      await expect(alertDialog(page)).toBeVisible();
      await expect(fixture).toHaveAttribute('data-handle-open-state', 'true');
      expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    });
  });

  test.describe('modality', () => {
    test('makes other interactive elements on the page inert when a modal dialog is open', async ({ page, mode }) => {
      await root(page, mode, { defaultOpen: true });

      // Base UI queries getByRole('presentation', { hidden: true }); Playwright's role query does not expose that role.
      await expect(page.locator('[role="presentation"]')).toHaveCount(1);
    });
  });
});

// The rows ADL-API-02 and ADL-DOM-01 name, run under the AlertDialog parts: an alert dialog is always modal.
test.describe('the regime alert mode forces', () => {
  // DLG-KEY-06, DLG-FOC-12, ADL-KEY-02.
  test('port: Tab and Shift+Tab cycle inside an open alert dialog and never leave it', async ({ page, mode }) => {
    const fixture = await root(page, mode, { defaultOpen: true });
    const input = page.getByTestId('popup-input');
    const cancel = alertDialog(page).getByRole('button', { name: 'Cancel' });
    await expect(input).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(cancel).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(input).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(cancel).toBeFocused();

    expect(await openChanges(fixture)).toEqual([]);
    await expect(alertDialog(page)).toBeVisible();
  });

  // DLG-KEY-07, ADL-KEY-02.
  test('port: Tab in an alert dialog with no tabbable content keeps focus on the popup', async ({ page, mode }) => {
    const fixture = await root(page, mode, { defaultOpen: true, case: 'empty' });
    await expect(alertDialog(page)).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(alertDialog(page)).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(alertDialog(page)).toBeFocused();

    expect(await openChanges(fixture)).toEqual([]);
  });

  // DLG-ARIA-11, DLG-ARIA-12, ADL-ARIA-03: markOthers hides the siblings of the paths to the popup and to the page's live
  // regions, so the live region stays exposed; no outside element is made inert.
  test('port: an open alert dialog hides the rest of the page from assistive technology except a live region, and marks nothing inert', async ({ page, mode }) => {
    await root(page, mode, { defaultOpen: true });
    await expect(alertDialog(page)).toBeVisible();

    await expect.poll(() => page.getByTestId('outside-button').evaluate((el) => el.closest('[aria-hidden="true"]') !== null)).toBe(true);
    expect(await page.getByTestId('live').evaluate((el) => el.closest('[aria-hidden="true"]') === null)).toBe(true);
    expect(await page.evaluate(() => [...document.querySelectorAll('[inert]')].filter((el) => !el.closest('[data-base-ui-portal]')).length)).toBe(0);
  });

  // DLG-DOM-26, DLG-DOM-27, ADL-DOM-01.
  for (const backdrop of [false, true]) {
    test(`port: an alert dialog locks page scroll from its open until its close (backdrop part: ${backdrop})`, async ({ page, mode }) => {
      const fixture = await root(page, mode, { open: false, backdrop });
      expect(await isPageLocked(page)).toBe(false);

      await setRootOpen(fixture, true);
      await expect(alertDialog(page)).toBeVisible();
      await expect.poll(() => isPageLocked(page)).toBe(true);
      await expect(internalBackdrop(page)).toHaveCount(1);

      await alertDialog(page).getByRole('button', { name: 'Cancel' }).click();
      await expect(alertDialog(page)).toHaveCount(0);
      await expect.poll(() => isPageLocked(page)).toBe(false);
    });
  }

  // DLG-DOM-06, ADL-DOM-01: the internal backdrop becomes inert in the batch that starts the exit, while the popup animates
  // out; a mutation record witnesses the overlap (ADR 06 §2).
  test('port: the internal backdrop is inert while the alert dialog animates out', async ({ page, mode }) => {
    const fixture = await root(page, mode, { open: true, animated: true, animations: 1 });
    await expect(alertDialog(page)).toBeVisible();
    await expect(internalBackdrop(page)).not.toHaveAttribute('inert');
    await page.evaluate(() => {
      const seen: { inert: boolean; ending: boolean }[] = [];
      (window as unknown as { inertDuringExit: typeof seen }).inertDuringExit = seen;
      new MutationObserver(() => {
        const backdrop = document.querySelector('[data-base-ui-portal] > [role="presentation"]:first-child');
        const popup = document.querySelector('[data-testid="alert-dialog-popup"]');
        if (backdrop && popup) {
          seen.push({ inert: backdrop.hasAttribute('inert'), ending: popup.hasAttribute('data-ending-style') });
        }
      }).observe(document.body, { subtree: true, attributes: true, attributeFilter: ['inert', 'data-ending-style'] });
    });

    await setRootOpen(fixture, false);
    await expect(alertDialog(page)).toHaveCount(0);

    const seen = await page.evaluate(() => (window as unknown as { inertDuringExit: { inert: boolean; ending: boolean }[] }).inertDuringExit);
    expect(seen).toContainEqual({ inert: true, ending: true });
  });

  // DLG-PTR-18, ADL-DIS-01: a press on the page behind lands on the internal backdrop, which requests nothing, with either
  // button and whether or not a backdrop part is rendered.
  for (const backdrop of [false, true]) {
    test(`port: a press on the page behind an open alert dialog lands on its backdrop and closes nothing (backdrop part: ${backdrop})`, async ({ page, mode }) => {
      const fixture = await root(page, mode, { defaultOpen: true, backdrop });
      await expect(alertDialog(page)).toBeVisible();
      const outside = await page.getByTestId('outside-button').boundingBox();
      const [x, y] = [outside!.x + outside!.width / 2, outside!.y + outside!.height / 2];
      const target = backdrop ? '[data-testid="backdrop"]' : '[data-base-ui-inert]';
      expect(await page.evaluate(([px, py, selector]) => document.elementFromPoint(Number(px), Number(py))?.matches(String(selector)) ?? false, [x, y, target])).toBe(true);

      await page.mouse.click(x, y);
      await page.mouse.click(x, y, { button: 'right' });
      await pressBehind(page);
      await settle(fixture);

      expect(await openChanges(fixture)).toEqual([]);
      await expect(alertDialog(page)).toBeVisible();
    });
  }

  // DLG-DOM-26 with a third-party locker still releasing the page (DialogRoot.test.tsx's three lockers), under the
  // AlertDialog parts: the lock holds from the open on, with no gap.
  const lockers = [
    {
      name: 'react-remove-scroll',
      lock: () => {
        const style = document.createElement('style');
        style.textContent = 'body[data-scroll-locked]{overflow:hidden!important;}';
        document.head.appendChild(style);
        document.body.setAttribute('data-scroll-locked', '1');
        setTimeout(() => {
          style.remove();
          document.body.removeAttribute('data-scroll-locked');
        }, 200);
      },
    },
    {
      name: 'silk-hq',
      lock: () => {
        document.body.style.setProperty('overflow', 'hidden');
        setTimeout(() => document.body.style.removeProperty('overflow'), 200);
      },
    },
    {
      name: 'Ariakit',
      lock: () => {
        const { style } = document.documentElement;
        style.setProperty('scrollbar-gutter', 'stable');
        style.setProperty('overflow-x', 'hidden');
        style.setProperty('overflow-y', 'hidden');
        setTimeout(() => {
          style.removeProperty('scrollbar-gutter');
          style.removeProperty('overflow-x');
          style.removeProperty('overflow-y');
        }, 200);
      },
    },
  ];

  for (const { name, lock } of lockers) {
    // Quarantined (ADR 11 §4) until 2026-11-12, issue 154, as dialog.spec.ts's hand-off titles: main's run 40872081690
    // failed the react-remove-scroll and Ariakit variants on WebAssembly with an unlocked sample.
    const quarantined = name !== 'silk-hq';
    (quarantined ? test.fixme : test)(`port: an alert dialog keeps the page locked after ${name} releases it, and unlocks on close`, quarantined ? {
      tag: '@quarantine',
      annotation: { type: 'issue', description: 'https://github.com/JakeMurrayDev/Blazix.Primitives/issues/154' },
    } : {}, async ({ page, mode }) => {
      const fixture = await root(page, mode, { open: false });
      expect(await isPageLocked(page)).toBe(false);

      await page.evaluate((lockSource) => {
        const samples: { at: number; locked: boolean }[] = [];
        (window as unknown as { lockSamples: typeof samples }).lockSamples = samples;
        const start = performance.now();
        const sample = () => {
          const html = document.documentElement;
          const { overflow, overflowX, overflowY } = getComputedStyle(html);
          const htmlScrolls = /auto|scroll|overlay|hidden|clip/.test(overflow + overflowY + overflowX);
          samples.push({ at: performance.now() - start, locked: /hidden|clip/.test(getComputedStyle(htmlScrolls ? html : document.body).overflowY) });
          if (performance.now() - start < 450) {
            setTimeout(sample, 10);
          }
        };
        // eslint-disable-next-line no-new-func
        new Function(`(${lockSource})()`)();
        sample();
      }, lock.toString());
      await setRootOpen(fixture, true);
      await expect(alertDialog(page)).toBeVisible();
      await expect
        .poll(() => page.evaluate(() => (window as unknown as { lockSamples: { at: number }[] }).lockSamples.at(-1)?.at ?? 0))
        .toBeGreaterThanOrEqual(440);

      const samples = await page.evaluate(() => (window as unknown as { lockSamples: { at: number; locked: boolean }[] }).lockSamples);
      expect(samples.filter((s) => !s.locked).map((s) => Math.round(s.at))).toEqual([]);

      await alertDialog(page).getByRole('button', { name: 'Cancel' }).click();
      await expect(alertDialog(page)).toHaveCount(0);
      await expect.poll(() => isPageLocked(page)).toBe(false);
    });
  }
});

// ADL §9's relaxation rows, per mode, with the circuit delayed 75 ms each way on Server.
test.describe('Server relaxations (ADR 01)', () => {
  test.beforeEach(async ({ page, mode }) => {
    if (mode === 'server') {
      await delayCircuit(page, 75);
    }
  });

  /** Dispatches an Escape keydown on the body and reports what the page saw once the dispatch returned. */
  async function dispatchEscape(page: Page): Promise<{ defaultPrevented: boolean; reachedWindow: boolean }> {
    return page.evaluate(() => {
      let reachedWindow = false;
      const listener = () => {
        reachedWindow = true;
      };
      window.addEventListener('keydown', listener);
      const event = new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true, cancelable: true });
      document.body.dispatchEvent(event);
      window.removeEventListener('keydown', listener);
      return { defaultPrevented: event.defaultPrevented, reachedWindow };
    });
  }

  // ADL-KEY-01, ADL-DIS-02 (the EVD-EVT-11 / EVD-DIS-01 / FLT-KEY-01 row).
  test('port: allowPropagation lets the Escape keydown reach the window on WebAssembly, and the Server fallback stops it while the alert dialog closes', async ({ page, mode }) => {
    const fixture = await root(page, mode, { defaultOpen: true, allowPropagation: true });
    await expect(alertDialog(page)).toBeVisible();

    const seen = await dispatchEscape(page);

    expect(seen).toEqual({ defaultPrevented: true, reachedWindow: mode === 'wasm' });
    await expect(alertDialog(page)).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['false:escape-key']);
  });

  test('port: a canceled Escape close of an alert dialog is not prevented on WebAssembly, and on Server the fallback prevented it before the cancel arrived', async ({ page, mode }) => {
    const fixture = await root(page, mode, { defaultOpen: true, cancelClose: true });
    await expect(alertDialog(page)).toBeVisible();

    const seen = await dispatchEscape(page);
    await settle(fixture);

    expect(seen.defaultPrevented).toBe(mode === 'server');
    expect(await openChanges(fixture)).toEqual(['false:escape-key']);
    await expect(alertDialog(page)).toBeVisible();
  });

  // ADL-EVT-01 (the TRN-ANI-03/09/10 row, adopted through DLG-ANI-03).
  test('port: an animated alert dialog popup is hidden at its exit end, and removed with onOpenChangeComplete(false) in a later batch, a round trip later on Server', async ({ page, mode }) => {
    const fixture = await root(page, mode, { animated: true, animations: 1 });
    await page.getByTestId('trigger').click();
    await expect(alertDialog(page)).toBeVisible();
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
    await page.evaluate(() => {
      const timing: { hidden?: number; removed?: number } = {};
      (window as unknown as { timing: typeof timing }).timing = timing;
      new MutationObserver((records) => {
        const now = performance.now();
        for (const entry of records) {
          if (entry.type === 'childList') {
            for (const node of entry.removedNodes) {
              if (node instanceof Element && (node.matches('[data-testid="alert-dialog-popup"]') || node.querySelector('[data-testid="alert-dialog-popup"]')) && timing.removed === undefined) {
                timing.removed = now;
              }
            }
          } else if ((entry.target as Element).matches('[data-testid="alert-dialog-popup"]') && (entry.target as Element).hasAttribute('hidden') && timing.hidden === undefined) {
            timing.hidden = now;
          }
        }
      }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden'] });
    });

    await page.keyboard.press('Escape');
    await expect(alertDialog(page)).toHaveCount(0);

    const timing = await page.evaluate(() => (window as unknown as { timing: { hidden?: number; removed?: number } }).timing);
    expect(timing.hidden).toBeDefined();
    expect(timing.removed).toBeDefined();
    expect(timing.hidden!).toBeLessThanOrEqual(timing.removed!);
    if (mode === 'server') {
      expect(timing.removed! - timing.hidden!).toBeGreaterThanOrEqual(140);
    }
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
  });

  // ADL-STR-02 (ADR 01 Amendments 2026-10-27, the newly created popup's row, adopted through dialog.md §9): a popup the
  // opening render creates moves focus in, hides the outside and locks scroll once its portal node is moved and its
  // handles attach, a round trip after the opening batch on Server; on WebAssembly within the batch's frames.
  test("port: a newly created alert dialog popup moves focus in, hides the outside and locks scroll after its opening batch, a round trip later on Server", async ({ page, mode }) => {
    await root(page, mode);
    await page.evaluate(() => {
      const t: { open?: number; focus?: number; ariaHidden?: number; locked?: number } = {};
      (window as unknown as { signals: typeof t }).signals = t;
      const html = document.documentElement;
      new MutationObserver((records) => {
        const now = performance.now();
        if (t.open === undefined && document.querySelector('[data-testid="alert-dialog-popup"]')?.hasAttribute('data-open')) {
          t.open = now;
        }
        for (const record of records) {
          const target = record.target as Element;
          if (t.ariaHidden === undefined && record.attributeName === 'aria-hidden' && target.getAttribute('aria-hidden') === 'true' && !target.closest('[data-base-ui-portal]') && !target.hasAttribute('data-base-ui-focus-guard')) {
            t.ariaHidden = now;
          }
        }
        if (t.locked === undefined && (html.style.overflow === 'hidden' || html.hasAttribute('data-base-ui-scroll-locked') || document.body.style.overflow === 'hidden')) {
          t.locked = now;
        }
      }).observe(html, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-open', 'aria-hidden', 'style', 'data-base-ui-scroll-locked'] });
      document.addEventListener(
        'focusin',
        (event) => {
          if (t.focus === undefined && (event.target as Element).closest('[data-testid="alert-dialog-popup"]')) {
            t.focus = performance.now();
          }
        },
        true,
      );
    });

    await page.getByTestId('trigger').click();
    await expect(alertDialog(page)).toBeVisible();
    const signals = () => page.evaluate(() => (window as unknown as { signals: { open?: number; focus?: number; ariaHidden?: number; locked?: number } }).signals);
    await expect.poll(async () => { const t = await signals(); return t.focus !== undefined && t.ariaHidden !== undefined && t.locked !== undefined; }).toBe(true);

    const t = await signals();
    expect(t.open).toBeDefined();
    if (mode === 'server') {
      expect(t.focus! - t.open!).toBeGreaterThanOrEqual(140);
    }
  });
});
