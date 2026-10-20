import { type Locator } from '@playwright/test';
import { test, expect, gotoFixture, type Page, type RenderMode } from './helpers';

// L4 rows of docs/architecture/button-behavior.md (UBT) that need only the `button` module: the `button` fixture
// attaches `ButtonTwin` to plain elements and counts the clicks that reach C# through Blazor's own `onclick`.
// The rows that need a part (consumer handler gating, rendered attributes and focusability) wait for Button and
// Toggle in M1. Titles are Base UI's (useButton.test.tsx, Button.test.tsx).

/** Opens the `button` fixture and waits until every case has its twin attached. */
async function gotoButtons(page: Page, mode: RenderMode): Promise<Locator> {
  const fixture = await gotoFixture(page, mode, 'button');
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/**
 * A barrier: clicks the fixture's sync button and waits for C# to count it. Server processes one circuit's events in
 * order (ADR-04/1) and WebAssembly synchronously, so every click dispatched before has reached C# afterwards.
 */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').click();
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

function buttonCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return { container, element: container.locator('[data-button]') };
}

/** A consumer listener that runs before the module: a capture listener on an ancestor. */
async function preventInCapture(container: Locator, type: 'keydown' | 'keyup'): Promise<void> {
  await container.evaluate((element, eventType) => {
    element.addEventListener(eventType, (event) => event.preventDefault(), { capture: true });
  }, type);
}

test.describe('useButton', () => {
  test('key: Enter does not click non-native buttons when keydown calls preventDefault', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'non-native');
    await preventInCapture(container, 'keydown');

    // Match native buttons: preventing the keydown's default cancels Enter activation.
    await element.press('Enter');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-clicks', '0');
  });

  test('key: Space does not click non-native buttons when keyup calls preventDefault', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'non-native');
    await preventInCapture(container, 'keyup');

    // Match native buttons: preventing the keyup's default cancels Space activation.
    await element.press('Space');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-clicks', '0');
  });

  // A C# consumer's PreventPartHandler() is decided inside the dispatch on WebAssembly; on Server it arrives after the
  // dispatch has ended and the key is handled (ADR 01 relaxation table, fallback "run").
  test('does not click composite buttons when keydown calls preventBaseUIHandler', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'composite-veto');

    await element.press('Space');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-clicks', mode === 'wasm' ? '0' : '1');
    await expect(container).toHaveAttribute('data-decisions', mode === 'wasm' ? '1' : '0');
  });

  test('does not click non-composite buttons when keydown/keyup calls preventBaseUIHandler', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'veto');

    // Enter activates on keydown; the consumer prevents it.
    await element.press('Enter');
    // Space activates on keyup; the consumer prevents it.
    await element.press('Space');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-clicks', mode === 'wasm' ? '0' : '2');
    await expect(container).toHaveAttribute('data-decisions', mode === 'wasm' ? '3' : '0');
  });

  test('does not fire duplicate clicks for Space on native composite buttons', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'native-composite');

    await element.press('Space');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-clicks', '1');
  });

  test('key: Space preserves native submit semantics on composite buttons', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'submit');

    await element.press('Space');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-submits', '1');
  });

  test('key: Space preserves native reset semantics on composite buttons', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'reset');

    await element.press('Space');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-resets', '1');
  });

  test('fires a single click for nested non-native composite buttons', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'nested');

    await element.press('Space');
    await settle(fixture);
    await expect(container).toHaveAttribute('data-clicks', '1');

    await element.press('Enter');
    await settle(fixture);
    await expect(container).toHaveAttribute('data-clicks', '2');
  });
});

test.describe('Button', () => {
  test('custom link element: Space activates the link without scrolling the page', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element: link } = buttonCase(fixture, 'link');
    await expect(link).toHaveJSProperty('tagName', 'A');

    await link.focus();
    await page.keyboard.press('Space');

    await expect(page).toHaveURL(/#ubt-target$/);
    await settle(fixture);
    await expect(container).toHaveAttribute('data-clicks', '1');
    expect(await page.evaluate(() => window.scrollY)).toBe(0);
  });

  test('custom element: keyboard activation clicks carry modifier key state', async ({ page, mode }) => {
    const fixture = await gotoButtons(page, mode);
    const { container, element } = buttonCase(fixture, 'non-native');

    await element.press('Shift+Enter');
    await settle(fixture);

    await expect(container).toHaveAttribute('data-clicks', '1');
    await expect(container).toHaveAttribute('data-last-click', 'detail=0 shift=True');
  });
});
