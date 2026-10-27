import { test, expect, delayCircuit, gotoFixture, type Page } from './helpers';
import type { Locator } from '@playwright/test';

// L4 (ADR 06 §2) of the `focus-manager` module on the test-only `focus-manager-*` fixtures (ADR 12 Amendments 2026-10-27
// item 1: no public part manages focus before Popover and Dialog). The browser-only Base UI titles of
// FloatingFocusManager.test.tsx (prop: restoreFocus) and tabbable.test.ts run here; the rest of the module is L3. The
// fixture plays the popup store (FocusManagerFixtureBase); the module reads the open state from the applied batch.
const focusManagerModule = '/_content/Blazix.Primitives/js/focus-manager.js';

async function gotoFocusManager(page: Page, mode: 'server' | 'wasm', name: string, query = ''): Promise<Locator> {
  if (query) {
    await page.goto(`/${mode}/${name}?${query}`, { waitUntil: 'commit' });
    await page.locator('html[data-blazix-started]').waitFor({ state: 'attached' });
  }
  const container = query ? page.locator(`[data-fixture="${name}"]`) : await gotoFixture(page, mode, name);
  await expect(container).toHaveAttribute('data-attached', 'true');
  return container;
}

/**
 * Records, in the page, every write of `data-open`, `aria-hidden` and `data-base-ui-inert` with the number of the
 * animation frame it landed in, so a test asserts that two writes landed before the same paint (ADR 06 §2: recorded
 * markers, no frame polls).
 */
async function recordWrites(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as { __fmFrame: number; __fmWrites: { name: string; target: string; value: string | null; frame: number }[] };
    w.__fmFrame = 0;
    w.__fmWrites = [];
    const tick = () => {
      w.__fmFrame += 1;
      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
    new MutationObserver((records) => {
      for (const record of records) {
        const target = record.target as Element;
        const name = target.hasAttribute('data-popup') ? 'popup' : target.hasAttribute('data-outside') ? 'outside' : target.tagName.toLowerCase();
        w.__fmWrites.push({ name: record.attributeName!, target: name, value: target.getAttribute(record.attributeName!), frame: w.__fmFrame });
      }
    }).observe(document.body, { subtree: true, attributes: true, attributeFilter: ['data-open', 'aria-hidden', 'data-base-ui-inert'] });
  });
}

async function writes(page: Page): Promise<{ name: string; target: string; value: string | null; frame: number }[]> {
  return page.evaluate(() => (window as unknown as { __fmWrites: { name: string; target: string; value: string | null; frame: number }[] }).__fmWrites);
}

test.describe('focus manager', () => {
  test.describe('prop: restoreFocus', () => {
    test('true: restores focus to nearest tabbable element if currently focused element is removed', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-restore');
      await fixture.locator('[data-mode="tabbable"]').click();
      await expect(fixture).toHaveAttribute('data-restore', 'tabbable');

      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-two]')).toBeFocused();

      // fireEvent.click: the press does not move focus; C# removes the focused button in a render batch.
      await fixture.locator('[data-remove]').dispatchEvent('click');

      await expect(fixture.locator('[data-three]')).toBeFocused();
    });

    test('false: does not restore focus to nearest tabbable element if currently focused element is removed', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-restore');
      await fixture.locator('[data-mode="none"]').click();
      await expect(fixture).toHaveAttribute('data-restore', 'none');

      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-two]')).toBeFocused();

      await fixture.locator('[data-remove]').dispatchEvent('click');
      await expect(fixture.locator('[data-two]')).toHaveCount(0);

      await expect(page.locator('body')).toBeFocused();
    });

    test('restores focus to the nearest tabbable element when the focused element becomes hidden', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-restore');
      await fixture.locator('[data-reference]').click();
      const two = fixture.locator('[data-two]');
      await expect(two).toBeFocused();

      await two.evaluate((element) => {
        document.body.tabIndex = -1;
        (element as HTMLElement).style.visibility = 'hidden';
        document.body.focus();
      });

      await expect(fixture.locator('[data-three]')).toBeFocused();
    });

    // FLT-FOC-12's `'popup'`, which Dialog and Popover pass and no Base UI title covers.
    test("port: 'popup' restores focus to the popup itself when the focused element is removed", async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-restore');
      await fixture.locator('[data-mode="popup"]').click();
      await expect(fixture).toHaveAttribute('data-restore', 'popup');

      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-two]')).toBeFocused();

      await fixture.locator('[data-remove]').dispatchEvent('click');

      await expect(fixture.locator('[data-popup]')).toBeFocused();
    });
  });

  // FLT-DOM-15's port titles: C# renders the inside guards for an enabled modal (or portaled) manager and the module
  // listens on them (ADR 02 and ADR 04 Amendments 2026-10-27).
  test.describe('inside focus guards', () => {
    test('port: an enabled modal manager has its inside guards and Tab wraps through them', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-modal');
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-one]')).toBeFocused();
      await expect(fixture.locator('[data-base-ui-focus-guard][data-type="inside"]')).toHaveCount(2);

      await page.keyboard.press('Tab');
      await expect(fixture.locator('[data-two]')).toBeFocused();
      await page.keyboard.press('Tab');
      await expect(fixture.locator('[data-close]')).toBeFocused();
      await page.keyboard.press('Tab');
      await expect(fixture.locator('[data-one]')).toBeFocused();
      await page.keyboard.press('Shift+Tab');
      await expect(fixture.locator('[data-close]')).toBeFocused();
    });

    test('port: a disabled manager has no inside guards and moves no focus', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-modal');
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-base-ui-focus-guard][data-type="inside"]')).toHaveCount(2);
      await fixture.locator('[data-close]').click();
      await expect(fixture.locator('[data-popup]')).toHaveCount(0);

      await fixture.locator('[data-disable]').click();
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-popup]')).toHaveAttribute('data-handle-attached', '');

      await expect(fixture.locator('[data-base-ui-focus-guard][data-type="inside"]')).toHaveCount(0);
      await expect(fixture.locator('[data-reference]')).toBeFocused();
    });

    test('port: a non-modal manager outside a portal has no inside guards, and Tab out of it asks for the close', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-non-modal');
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-one]')).toBeFocused();
      await expect(fixture.locator('[data-base-ui-focus-guard]')).toHaveCount(0);

      await page.keyboard.press('Tab');
      await expect(fixture.locator('[data-two]')).toBeFocused();
      await page.keyboard.press('Tab');

      await expect(fixture.locator('[data-last]')).toBeFocused();
      await expect(fixture).toHaveAttribute('data-focus-out-closes', '1');
      await expect(fixture.locator('[data-popup]')).toHaveCount(0);
    });
  });

  // FLT-ARIA-01, A11Y-07, DOM-16 in the page, with the outside content the brief names.
  test('port: a modal manager hides the outside except live regions, their ancestors and scripts, and keeps an existing aria-hidden', async ({ page, mode }) => {
    const fixture = await gotoFocusManager(page, mode, 'focus-manager-modal');
    await fixture.locator('[data-reference]').click();
    await expect(fixture.locator('[data-one]')).toBeFocused();

    await expect(fixture.locator('[data-outside-button]')).toHaveAttribute('aria-hidden', 'true');
    await expect(fixture.locator('[data-reference]')).toHaveAttribute('aria-hidden', 'true');
    await expect(fixture.locator('[data-outside]')).not.toHaveAttribute('aria-hidden');
    await expect(fixture.locator('[data-live]')).not.toHaveAttribute('aria-hidden');
    await expect(fixture.locator('[data-script]')).not.toHaveAttribute('aria-hidden');
    await expect(fixture.locator('[data-outside]')).toHaveAttribute('data-base-ui-inert', '');
    await expect(fixture.locator('[data-popup]')).not.toHaveAttribute('aria-hidden');
    await expect(fixture.locator('[data-reference]')).not.toHaveAttribute('inert');

    await page.keyboard.press('Escape');
    await expect(fixture.locator('[data-popup]')).toHaveCount(0);

    await expect(fixture.locator('[data-outside-button]')).not.toHaveAttribute('aria-hidden');
    await expect(fixture.locator('[data-outside]')).not.toHaveAttribute('data-base-ui-inert');
    await expect(fixture.locator('[data-own-hidden]')).toHaveAttribute('aria-hidden', 'true');
    await expect(fixture.locator('[data-reference]')).toBeFocused();
  });

  // FLT-ALG-27, DOM-17, KEY-04: a dialog-role popup rendered without a tabindex and with nothing to tab to.
  test('port: a dialog with no tabbable content and no tabindex is managed to tabindex 0 and keeps focus on Tab', async ({ page, mode }) => {
    const fixture = await gotoFocusManager(page, mode, 'focus-manager-empty');
    await fixture.locator('[data-reference]').click();
    const popup = fixture.locator('[data-popup]');
    await expect(popup).toBeFocused();
    await expect(popup).toHaveAttribute('tabindex', '0');
    await expect(popup).toHaveAttribute('data-tabindex', '0');

    await page.keyboard.press('Tab');
    await expect(popup).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(popup).toBeFocused();
  });

  // The attach-order rule (the standing rule of CI run 40461905812; design point 1's composite clause): every composite
  // item renders tabindex="-1" until js/composite.js attaches, so with that module held back initial focus first finds
  // the button after the group and moves onto the group's tab stop once the composite writes it.
  test("port: initial focus lands on the composite group's tab stop when js/composite.js attaches last", async ({ page, mode }) => {
    // WebAssembly requests the fingerprinted URL (`composite.<hash>.js`); the route matches both and counts its hits.
    let stalled = 0;
    await page.route(/\/js\/composite(\.[\w-]+)?\.js(\?.*)?$/, (route) => {
      stalled += 1;
      setTimeout(() => void route.continue(), 1000);
    });
    const fixture = await gotoFocusManager(page, mode, 'focus-manager-composite');
    await fixture.locator('[data-reference]').click();
    const first = fixture.locator('[data-item="1"]');

    await expect(first).toHaveAttribute('tabindex', '0', { timeout: 10_000 });
    await expect(first).toBeFocused();
    expect(stalled).toBeGreaterThan(0);
  });

  // FLT-FOC-11 (re-tagged [L4]; ADR 01's FLT-FOC-11 row, settled by the orchestrator 2026-10-27): the module decides the
  // focus-out close in the microtask after the focusout and C# commits it, a round trip later on Server and in the same
  // task on WebAssembly. The page records the frame of the focusout and of the batch that removes the popup.
  test.describe('the focus-out close', () => {
    test.beforeEach(async ({ page, mode }) => {
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
    });

    test('port: the focus-out close commits a round trip after the focusout on Server and before the next frame on WebAssembly', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-non-modal');
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-one]')).toBeFocused();
      await page.evaluate(() => {
        const w = window as unknown as { __fmFrame: number; __fmFocusOut: number | null; __fmRemoved: number | null };
        w.__fmFrame = 0;
        w.__fmFocusOut = null;
        w.__fmRemoved = null;
        const tick = () => {
          w.__fmFrame += 1;
          requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
        const popup = document.querySelector('[data-popup]')!;
        popup.addEventListener('focusout', () => {
          w.__fmFocusOut ??= w.__fmFrame;
        });
        new MutationObserver(() => {
          if (!popup.isConnected) {
            w.__fmRemoved ??= w.__fmFrame;
          }
        }).observe(document.body, { childList: true, subtree: true });
      });

      await fixture.locator('[data-last]').focus();
      await expect(fixture.locator('[data-popup]')).toHaveCount(0);
      await expect(fixture).toHaveAttribute('data-focus-out-closes', '1');

      const frames = await page.evaluate(() => {
        const w = window as unknown as { __fmFocusOut: number | null; __fmRemoved: number | null };
        return { focusOut: w.__fmFocusOut, removed: w.__fmRemoved };
      });
      expect(frames.focusOut).not.toBeNull();
      expect(frames.removed).not.toBeNull();
      if (mode === 'server') {
        expect(frames.removed!).toBeGreaterThan(frames.focusOut!);
      } else {
        expect(frames.removed).toBe(frames.focusOut);
      }
    });

    // The answered-requests counter (ADR 04 Amendments 2026-10-27, settled by the orchestrator in the verification of
    // PR 128): a consumer's cancel renders nothing, so the host's answer re-arms the module and the next focus-out asks again.
    test('port: a cancelled focus-out close is asked again', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-non-modal');
      await fixture.locator('[data-cancel-switch]').click();
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-one]')).toBeFocused();

      await fixture.locator('[data-last]').focus();
      await expect(fixture).toHaveAttribute('data-focus-out-closes', '1');
      await expect(fixture.locator('[data-popup]')).toHaveAttribute('data-open', '');

      await fixture.locator('[data-one]').focus();
      await fixture.locator('[data-last]').focus();

      await expect(fixture).toHaveAttribute('data-focus-out-closes', '2');
      await expect(fixture.locator('[data-popup]')).toHaveAttribute('data-open', '');
    });
  });

  // FloatingFocusManager.test.tsx's "non-modal + FloatingPortal" App on `focus-manager-portal`: the popup is rendered
  // inside the test-only portal, so the `portal` module moves it to the end of the body, the manager publishes its state
  // (FLT-FOC-18, both halves) and the portal renders and routes its outside guards (FLT-DOM-14, KEY-07, FOC-17).
  test.describe('non-modal + FloatingPortal', () => {
    test('does not mark reference siblings due to outside focus guards', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-portal');
      await fixture.locator('[data-reference]').click();

      await expect(page.locator('[data-popup]')).toBeVisible();
      await expect(page.locator('[data-base-ui-focus-guard][data-type="outside"]')).toHaveCount(2);
      await expect(fixture.locator('[data-reference]')).not.toHaveAttribute('data-base-ui-inert');
      await expect(fixture.locator('[data-reference-sibling="1"]')).not.toHaveAttribute('data-base-ui-inert');
      await expect(fixture.locator('[data-reference-sibling="2"]')).not.toHaveAttribute('data-base-ui-inert');
    });

    test('port: the popup moves into the portal node at the end of the body with its inside guards, and Tab leaves through the outside guards to the last element and asks for the close', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-portal');
      await fixture.locator('[data-reference]').click();
      await expect(page.locator('[data-inside]')).toBeFocused();
      expect(await page.locator('[data-popup]').evaluate((popup) => popup.closest('[data-base-ui-portal]')?.parentElement === document.body)).toBe(true);
      await expect(page.locator('[data-base-ui-portal] [data-base-ui-focus-guard][data-type="inside"]')).toHaveCount(2);

      await page.keyboard.press('Tab');

      await expect(fixture.locator('[data-last]')).toBeFocused();
      await expect(fixture).toHaveAttribute('data-focus-out-closes', '1');
      await expect(page.locator('[data-popup]')).toHaveCount(0);
      await expect(page.locator('[data-base-ui-focus-guard]')).toHaveCount(0);
    });

    test('port: Shift+Tab from the popup reaches the reference through the outside guards and keeps it open; Tab returns into it', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-portal');
      await fixture.locator('[data-reference]').click();
      await expect(page.locator('[data-inside]')).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(fixture.locator('[data-reference]')).toBeFocused();
      await expect(page.locator('[data-popup]')).toHaveAttribute('data-open', '');
      // FLT-FOC-17, DOM-18: the portal's tabbables left the tab order when focus left it.
      await expect(page.locator('[data-inside]')).toHaveAttribute('tabindex', '-1');

      await page.keyboard.press('Tab');
      await expect(page.locator('[data-inside]')).toBeFocused();
      await expect(page.locator('[data-inside]')).toHaveAttribute('tabindex', '0');
      await expect(fixture).toHaveAttribute('data-focus-out-closes', '0');
    });

    // FLT-DOM-18 with design point 4 (the composite writes its roving value into the stash): a group's one tab stop
    // survives the portal taking its items out of the tab order and putting them back.
    test('port: a composite group in a non-modal portal leaves the tab order with the portal and comes back with one tab stop', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-portal', 'composite=true');
      await fixture.locator('[data-reference]').click();
      const items = page.locator('[data-item]');
      await expect(items.nth(0)).toBeFocused();
      await page.keyboard.press('ArrowRight');
      await expect(items.nth(1)).toBeFocused();

      await page.keyboard.press('Shift+Tab');
      await expect(fixture.locator('[data-reference]')).toBeFocused();
      await expect(items.nth(0)).toHaveAttribute('tabindex', '-1');
      await expect(items.nth(1)).toHaveAttribute('tabindex', '-1');
      await expect(items.nth(1)).toHaveAttribute('data-tabindex', '0');

      await page.keyboard.press('Tab');
      await expect(items.nth(1)).toBeFocused();
      await expect(page.locator('[data-item][tabindex="0"]')).toHaveCount(1);
      await expect(page.locator('[data-item][data-tabindex]')).toHaveCount(0);
    });
  });

  // ADR 04 Amendments 2026-10-27 (the focus manager's open input): an attached popup's marking and trap land with the
  // batch that opens or closes it, before paint, on both modes; on Server under a 150 ms one-way delay.
  test.describe('the open input under latency', () => {
    test.beforeEach(async ({ page, mode }) => {
      if (mode === 'server') {
        await delayCircuit(page, 150);
      }
    });

    test('port: the marking lands in the batch that opens an attached popup and is released in the batch that closes it', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-kept-mounted');
      await recordWrites(page);

      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-one]')).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(fixture.locator('[data-popup]')).not.toHaveAttribute('data-open');
      await expect(fixture.locator('[data-outside]')).not.toHaveAttribute('aria-hidden');

      const recorded = await writes(page);
      const opened = recorded.find((w) => w.target === 'popup' && w.name === 'data-open' && w.value !== null);
      const hidden = recorded.find((w) => w.target === 'outside' && w.name === 'aria-hidden' && w.value === 'true');
      const closed = recorded.find((w) => w.target === 'popup' && w.name === 'data-open' && w.value === null);
      const released = recorded.find((w) => w.target === 'outside' && w.name === 'aria-hidden' && w.value === null);
      expect(opened && hidden && closed && released).toBeTruthy();
      expect(hidden!.frame).toBe(opened!.frame);
      expect(released!.frame).toBe(closed!.frame);
    });

    test('port: an open the options disable takes no focus and marks nothing', async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-kept-mounted');
      await recordWrites(page);

      await fixture.locator('[data-hover-open]').click();
      await expect(fixture.locator('[data-popup]')).toHaveAttribute('data-open', '');
      await expect(fixture.locator('[data-popup]')).toHaveAttribute('data-handle-attached', '');
      // The disabled open moved no focus: the button the press focused keeps it.
      await expect(fixture.locator('[data-hover-open]')).toBeFocused();
      // The positive half: a press open of the same popup marks the page and moves focus.
      await fixture.locator('[data-reference]').dispatchEvent('click');
      await expect(fixture.locator('[data-popup]')).not.toHaveAttribute('data-open');
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-one]')).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(fixture.locator('[data-popup]')).not.toHaveAttribute('data-open');

      const recorded = await writes(page);
      const firstOpen = recorded.findIndex((w) => w.target === 'popup' && w.name === 'data-open' && w.value !== null);
      const firstClose = recorded.findIndex((w, i) => i > firstOpen && w.target === 'popup' && w.name === 'data-open' && w.value === null);
      expect(firstOpen).toBeGreaterThanOrEqual(0);
      expect(recorded.slice(firstOpen, firstClose).filter((w) => w.name === 'aria-hidden' || w.name === 'data-base-ui-inert')).toEqual([]);
      expect(recorded.some((w) => w.target === 'outside' && w.name === 'aria-hidden' && w.value === 'true')).toBe(true);
    });

    test("port: this open's initial focus target is used", async ({ page, mode }) => {
      const fixture = await gotoFocusManager(page, mode, 'focus-manager-kept-mounted');
      await fixture.locator('[data-reference]').click();
      await expect(fixture.locator('[data-one]')).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(fixture.locator('[data-reference]')).toBeFocused();

      await fixture.locator('[data-target-two]').click();
      await fixture.locator('[data-reference]').click();

      await expect(fixture.locator('[data-two]')).toBeFocused();
    });
  });
});

// tabbable.test.ts's `it.skipIf(isJSDOM)` titles (FLT-FOC-14, ALG-18 "L4 for the browser-only cases"): the page imports
// the module the library ships and runs the query on real layout. The render mode plays no part (the dom.spec.ts
// precedent).
test.describe('tabbable', () => {
  // Each case builds its elements in the page and names the ones the assertions read.
  const cases: { key: string; title: string; expectTabbable: Record<string, boolean>; inList: Record<string, boolean> }[] = [
    {
      key: 'contents-visible',
      title: 'keeps visible descendants of display: contents ancestors in the tab order in Chromium',
      expectTabbable: { wrapper: false, button: true },
      inList: { wrapper: false, button: true },
    },
    {
      key: 'block-content-visibility',
      title: 'excludes descendants of block content-visibility:hidden ancestors from the tab order',
      expectTabbable: { button: false },
      inList: { button: false },
    },
    {
      key: 'contents-content-visibility',
      title: 'keeps descendants of display: contents content-visibility:hidden ancestors in the tab order',
      expectTabbable: { button: true },
      inList: { button: true },
    },
    {
      key: 'inline-content-visibility',
      title: 'keeps descendants of inline content-visibility:hidden ancestors in the tab order',
      expectTabbable: { button: true },
      inList: { button: true },
    },
    {
      key: 'candidate-content-visibility',
      title: 'keeps content-visibility:hidden candidates in the tab order',
      expectTabbable: { button: true },
      inList: { button: true },
    },
    {
      key: 'zero-size',
      title: 'keeps zero-size elements in the tab order',
      expectTabbable: { element: true },
      inList: { element: true },
    },
  ];

  for (const { key, title, expectTabbable, inList } of cases) {
    test(title, async ({ page, mode }) => {
      await gotoFixture(page, mode, 'probe');

      const result = await page.evaluate(
        async ({ path, key: caseKey }) => {
          const { isTabbable, tabbable } = await import(path);
          const root = document.createElement('div');
          const elements: Record<string, HTMLElement> = {};
          const button = document.createElement('button');
          if (caseKey === 'zero-size') {
            const element = document.createElement('div');
            element.tabIndex = 0;
            Object.assign(element.style, { width: '0', height: '0', padding: '0', border: '0' });
            root.append(element);
            elements.element = element;
          } else if (caseKey === 'candidate-content-visibility') {
            button.style.setProperty('content-visibility', 'hidden');
            root.append(button);
            elements.button = button;
          } else {
            const wrapper = document.createElement('div');
            if (caseKey === 'contents-visible') {
              wrapper.style.display = 'contents';
              wrapper.tabIndex = 0;
              elements.wrapper = wrapper;
            } else if (caseKey === 'contents-content-visibility') {
              wrapper.style.display = 'contents';
              wrapper.style.setProperty('content-visibility', 'hidden');
            } else if (caseKey === 'inline-content-visibility') {
              wrapper.style.display = 'inline';
              wrapper.style.setProperty('content-visibility', 'hidden');
            } else {
              wrapper.style.setProperty('content-visibility', 'hidden');
            }
            wrapper.append(button);
            root.append(wrapper);
            elements.button = button;
          }
          document.body.append(root);
          try {
            const list = tabbable(document.body) as Element[];
            return {
              tabbable: Object.fromEntries(Object.entries(elements).map(([name, element]) => [name, isTabbable(element) as boolean])),
              inList: Object.fromEntries(Object.entries(elements).map(([name, element]) => [name, list.includes(element)])),
            };
          } finally {
            root.remove();
          }
        },
        { path: focusManagerModule, key },
      );

      expect(result.tabbable).toEqual(expectTabbable);
      expect(result.inList).toEqual(inList);
    });
  }
});
