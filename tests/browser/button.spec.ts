import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/button.md: BTN-KEY-01/02, BTN-PTR-02, BTN-FOC-01..03 and BTN-EVT-01, on the Button
// part over the `button` module. Titles are Base UI's (button/Button.test.tsx), plus the per-mode rows of the BTN-KEY-01
// Server relaxation (ADR 01 relaxation table, ADR 04 §3: a C# consumer's veto is decided inside the dispatch on
// WebAssembly and arrives a round trip late on Server). The fixtures count the consumer handlers Blazor invoked.

type FixtureName = 'button-native' | 'button-focusable' | 'button-custom' | 'button-link' | 'button-loading';

/** Opens a Button fixture and waits until every Button has its behavior attached. */
async function gotoButtons(page: Page, mode: RenderMode, name: FixtureName): Promise<Locator> {
  const fixture = await gotoFixture(page, mode, name);
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/**
 * A barrier: clicks the fixture's sync button and waits for C# to count it. Server processes one circuit's events in
 * order (ADR-04/1) and WebAssembly synchronously, so every event dispatched before has reached C# afterwards. The click
 * is `HTMLElement.click()`, which does not move focus, so the focus assertions after it still read the Button's.
 */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

function buttonCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    button: container.locator('[data-button]'),
    before: container.locator('[data-before]'),
    after: container.locator('[data-after]'),
  };
}

/** Presses Tab from the native button placed before the case. */
async function tabFromBefore(page: Page, before: Locator): Promise<void> {
  await before.focus();
  await page.keyboard.press('Tab');
}

/** Asserts that none of the gated consumer handlers ran. */
async function expectNoGatedHandler(container: Locator): Promise<void> {
  await expect(container).toHaveAttribute('data-clicks', '0');
  await expect(container).toHaveAttribute('data-mousedowns', '0');
  await expect(container).toHaveAttribute('data-pointerdowns', '0');
  await expect(container).toHaveAttribute('data-keydowns', '0');
}

/** Counts in the page the clicks a listener on the element observes in the capture phase. */
async function countCaptureClicks(element: Locator): Promise<void> {
  await element.evaluate((node) => {
    node.addEventListener('click', () => {
      const dataset = (node as HTMLElement).dataset;
      dataset.captureClicks = String(Number(dataset.captureClicks ?? '0') + 1);
    }, { capture: true });
  });
}

/** A consumer JavaScript listener that runs before the module: a capture listener on an ancestor. */
async function preventInCapture(container: Locator, type: 'keydown' | 'keyup'): Promise<void> {
  await container.evaluate((element, eventType) => {
    element.addEventListener(eventType, (event) => event.preventDefault(), { capture: true });
  }, type);
}

test.describe('Button', () => {
  test.describe('prop: nativeButton', () => {
    test('custom link element: Space activates the link without scrolling the page', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-link');
      const { container, button: link, before } = buttonCase(fixture, 'link');
      await expect(link).toHaveJSProperty('tagName', 'A');
      await expect(link).toHaveAttribute('role', 'button');

      await tabFromBefore(page, before);
      await expect(link).toBeFocused();
      await page.keyboard.press('Space');

      await expect(page).toHaveURL(/#target$/);
      await settle(fixture);
      await expect(container).toHaveAttribute('data-clicks', '1');
      // BTN-EVT-01: the synthetic click is a real click with detail 0.
      await expect(container).toHaveAttribute('data-last-click', 'detail=0 shift=false');
      expect(await page.evaluate(() => window.scrollY)).toBe(0);
    });

    test('custom element: applies button semantics and dispatches real clicks from keyboard activation', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-custom');
      const { container, button, before } = buttonCase(fixture, 'enabled');
      await countCaptureClicks(button);

      await expect(button).toHaveJSProperty('tagName', 'SPAN');
      await expect(button).toHaveAttribute('role', 'button');
      await expect(button).toHaveAttribute('tabindex', '0');

      await tabFromBefore(page, before);
      await expect(button).toBeFocused();

      await page.keyboard.press('Enter');
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(button).toHaveAttribute('data-capture-clicks', '2');
      await expect(container).toHaveAttribute('data-substitute-clicks', '2');
      await expect(container).toHaveAttribute('data-clicks', '2');
      // BTN-EVT-01: the click bubbles to the ancestor and carries detail 0.
      await expect(container).toHaveAttribute('data-ancestor-clicks', '2');
      await expect(container).toHaveAttribute('data-last-click', 'detail=0 shift=false');
    });

    test('custom element: keyboard activation clicks carry modifier key state', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-custom');
      const { container, button, before } = buttonCase(fixture, 'enabled');

      await tabFromBefore(page, before);
      await expect(button).toBeFocused();
      await page.keyboard.press('Shift+Enter');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      await expect(container).toHaveAttribute('data-last-click', 'detail=0 shift=true');
    });
  });

  test.describe('prop: disabled', () => {
    test('native button: uses the disabled attribute and is not focusable', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-native');
      const { container, button, before, after } = buttonCase(fixture, 'disabled');

      await expect(button).toHaveAttribute('disabled', '');
      await expect(button).toHaveAttribute('data-disabled', '');
      await expect(button).not.toHaveAttribute('aria-disabled');

      await tabFromBefore(page, before);
      await expect(button).not.toBeFocused();
      await expect(after).toBeFocused();

      await button.click({ force: true });
      await page.keyboard.press('Space');
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expectNoGatedHandler(container);
    });

    test('custom element: applies aria-disabled and is not focusable', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-custom');
      const { container, button, before, after } = buttonCase(fixture, 'disabled');

      await expect(button).not.toHaveAttribute('disabled');
      await expect(button).toHaveAttribute('data-disabled', '');
      await expect(button).toHaveAttribute('aria-disabled', 'true');
      await expect(button).toHaveAttribute('tabindex', '-1');

      await tabFromBefore(page, before);
      await expect(button).not.toBeFocused();
      await expect(after).toBeFocused();

      await button.click({ force: true });
      // Programmatically focusable (UBT-FOC-02), so the keys reach it and are still ignored.
      await button.focus();
      await page.keyboard.press('Space');
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expectNoGatedHandler(container);
    });
  });

  test.describe('prop: focusableWhenDisabled', () => {
    test('native button: prevents interactions but remains focusable', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-focusable');
      const { container, button, before } = buttonCase(fixture, 'native');

      await expect(button).not.toHaveAttribute('disabled');
      await expect(button).toHaveAttribute('data-disabled', '');
      await expect(button).toHaveAttribute('aria-disabled', 'true');
      await expect(button).toHaveAttribute('tabindex', '0');

      await tabFromBefore(page, before);
      await expect(button).toBeFocused();

      await button.click({ force: true });
      await page.keyboard.press('Space');
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expectNoGatedHandler(container);
      await expect(button).toBeFocused();
    });

    test('native button: allows hover handlers while blocking activation', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-focusable');
      const { container, button } = buttonCase(fixture, 'native');

      await button.hover({ force: true });
      const box = await button.boundingBox();
      await page.mouse.move(box!.x + box!.width / 2 + 2, box!.y + box!.height / 2 + 1);
      await settle(fixture);
      expect(Number(await container.getAttribute('data-mousemoves'))).toBeGreaterThan(0);

      await button.click({ force: true });
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '0');
    });

    test('keeps focus and suppresses interactions after becoming disabled', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-loading');
      const { container, button, before } = buttonCase(fixture, 'loading');

      await tabFromBefore(page, before);
      await expect(button).toBeFocused();

      await button.click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      await expect(button).toBeFocused();
      await expect(button).toHaveAttribute('aria-disabled', 'true');

      await button.click({ force: true });
      await page.keyboard.press('Enter');
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '1');
      await expect(button).toBeFocused();
    });

    test('custom element: prevents interactions but remains focusable', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-focusable');
      const { container, button, before } = buttonCase(fixture, 'custom');

      await expect(button).not.toHaveAttribute('disabled');
      await expect(button).toHaveAttribute('data-disabled', '');
      await expect(button).toHaveAttribute('aria-disabled', 'true');
      await expect(button).toHaveAttribute('tabindex', '0');

      await tabFromBefore(page, before);
      await expect(button).toBeFocused();

      await button.click({ force: true });
      await page.keyboard.press('Space');
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expectNoGatedHandler(container);
      await expect(button).toBeFocused();
    });
  });

  test.describe('consumer vetoes (BTN-KEY-01, ADR 01 relaxation table)', () => {
    test('key: Enter does not click non-native buttons when keydown calls preventDefault', async ({ page, mode }) => {
      // A consumer JavaScript listener's veto is honoured on both render modes.
      const fixture = await gotoButtons(page, mode, 'button-custom');
      const { container, button } = buttonCase(fixture, 'enabled');
      await preventInCapture(container, 'keydown');

      // Focused directly: the listener would cancel a Tab too.
      await button.focus();
      await page.keyboard.press('Enter');
      await settle(fixture);

      await expect(button).toBeFocused();
      await expect(container).toHaveAttribute('data-clicks', '0');
    });

    test('key: Space does not click non-native buttons when keyup calls preventDefault', async ({ page, mode }) => {
      const fixture = await gotoButtons(page, mode, 'button-custom');
      const { container, button } = buttonCase(fixture, 'enabled');
      await preventInCapture(container, 'keyup');

      await button.focus();
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', '0');
    });

    test('does not click non-composite buttons when keydown/keyup calls preventBaseUIHandler', async ({ page, mode }) => {
      // A C# consumer's PreventPartHandler() is decided inside the dispatch on WebAssembly; on Server it arrives after
      // the dispatch has ended and the key is handled (fallback "run"). Either way each handler runs once per key.
      const fixture = await gotoButtons(page, mode, 'button-custom');
      const { container, button } = buttonCase(fixture, 'veto');

      // Focused directly: a Tab's keyup would land on the button and count.
      await button.focus();
      await page.keyboard.press('Enter');
      await page.keyboard.press('Space');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-clicks', mode === 'wasm' ? '0' : '2');
      await expect(container).toHaveAttribute('data-keydowns', '2');
      await expect(container).toHaveAttribute('data-keyups', '2');
    });
  });

  test('an enabled native button calls the consumer handlers once per press', async ({ page, mode }) => {
    // BTN-PTR-01 and BTN-KEY-01 (native, not intercepted): the browser activates the button itself.
    const fixture = await gotoButtons(page, mode, 'button-native');
    const { container, button, before } = buttonCase(fixture, 'enabled');

    await button.click();
    await tabFromBefore(page, before);
    await expect(button).toBeFocused();
    await page.keyboard.press('Enter');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-clicks', '2');
    await expect(container).toHaveAttribute('data-mousedowns', '1');
    await expect(container).toHaveAttribute('data-pointerdowns', '1');
    await expect(container).toHaveAttribute('data-keydowns', '1');
  });
});
