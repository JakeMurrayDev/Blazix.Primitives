import type { Locator, Route } from '@playwright/test';
import { test, expect, gotoFixture, type Page } from './helpers';
import { gotoPopupFixture, settle } from './suites/popupFixture';

// L4 (ADR 06 §2) for the `interactions` mechanism module (M4 track 3, issue 119), Server first, then WebAssembly, on the
// test-only press host (`tests/Blazix.Primitives.Fixtures.Client/Interactions/`): the decision point `press` with a
// consumer `onclick` veto on a trigger carrying a `button` handle and an `interactions` handle (FLT-PTR-03, FLT-ALG-16;
// ADR 04 and ADR 01 Amendments 2026-10-27), and the attach order of the two handles and of the session (the lesson of
// CI run 40461905812). The Escape titles (FLT-KEY-01, FLT-EVT-05) run on track 1's popup host (`popup-conformance`,
// FixturePopupRoot composing the session): on WebAssembly the decision point `escape` runs the consumer's `onOpenChange`
// inside the keydown; on Server the module applies the fallback `prevent-and-stop` to the innermost popup and reports the
// close (ADR 01 relaxation row "EVD-EVT-11, EVD-DIS-01, FLT-KEY-01").

/** Holds every request for a module until `release` is called; WebAssembly requests the fingerprinted file name. */
async function stall(page: Page, module: string): Promise<() => Promise<void>> {
  const held: Route[] = [];
  let released = false;
  const url = new RegExp(`/_content/Blazix\\.Primitives/js/${module}(\\.[a-z0-9]+)?\\.js$`);
  await page.route(url, (route) => (released ? route.continue() : held.push(route)));
  return async () => {
    released = true;
    for (const route of held.splice(0)) {
      await route.continue();
    }
  };
}

test.describe('press', () => {
  test('port: a press toggles the popup through press and runs the consumer onclick once per click', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'interactions-press');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const trigger = fixture.locator('[data-trigger]');

    await trigger.click();
    await expect(fixture).toHaveAttribute('data-open', 'true');
    await expect(fixture.locator('[data-popup]')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-consumer-clicks', '1');

    await trigger.click();
    await expect(fixture).toHaveAttribute('data-open', 'false');
    await expect(fixture).toHaveAttribute('data-consumer-clicks', '2');
    await expect(fixture).toHaveAttribute('data-requests', '2');
  });

  test('port: a consumer onclick veto of the press holds on both modes', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'interactions-press');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await fixture.locator('[data-veto]').check();
    const trigger = fixture.locator('[data-trigger]');

    await trigger.click();
    await expect(fixture).toHaveAttribute('data-consumer-clicks', '1');
    // The veto keeps it closed: no request reached the store (on Server, after the report's round trip).
    await fixture.locator('[data-veto]').uncheck();
    await trigger.click();
    await expect(fixture).toHaveAttribute('data-open', 'true');
    await expect(fixture).toHaveAttribute('data-consumer-clicks', '2');
    await expect(fixture).toHaveAttribute('data-requests', '1');
  });
});

test.describe('attach order', () => {
  test('port: with js/interactions.js stalled the button handle attaches first, and the trigger toggles once when the module arrives', async ({ page, mode }) => {
    const release = await stall(page, 'interactions');
    const fixture = await gotoFixture(page, mode, 'interactions-press');
    await expect(fixture).toHaveAttribute('data-attached', 'false');

    await release();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await fixture.locator('[data-trigger]').click();

    await expect(fixture).toHaveAttribute('data-open', 'true');
    await expect(fixture).toHaveAttribute('data-requests', '1');
    await expect(fixture).toHaveAttribute('data-consumer-clicks', '1');
  });

  test('port: with js/button.js stalled the interactions handles attach first, and the trigger with two handles toggles once', async ({ page, mode }) => {
    const release = await stall(page, 'button');
    const fixture = await gotoFixture(page, mode, 'interactions-press');
    await expect(fixture).toHaveAttribute('data-attached', 'false');

    await release();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await fixture.locator('[data-trigger]').click();

    await expect(fixture).toHaveAttribute('data-open', 'true');
    await expect(fixture).toHaveAttribute('data-requests', '1');
    await expect(fixture).toHaveAttribute('data-consumer-clicks', '1');
  });

  test('port: an option changed while js/interactions.js is stalled reaches the module once it loads', async ({ page, mode }) => {
    const release = await stall(page, 'interactions');
    const fixture = await gotoFixture(page, mode, 'interactions-press');
    // The trigger's re-render sends `Click.Toggle = false` while its attach is still in flight.
    await fixture.locator('[data-keep-open]').check();

    await release();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const trigger = fixture.locator('[data-trigger]');
    await trigger.click();
    await expect(fixture).toHaveAttribute('data-open', 'true');
    await trigger.click();

    await expect(fixture).toHaveAttribute('data-requests', '2');
    await expect(fixture).toHaveAttribute('data-open', 'true');
  });

  test('port: a trigger attached before its session still toggles once the session attaches', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'interactions-press-late-session');
    const trigger = fixture.locator('[data-trigger]');

    await fixture.locator('[data-attach-session]').click();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await trigger.click();
    await expect(fixture).toHaveAttribute('data-open', 'true');

    await trigger.click();
    await expect(fixture).toHaveAttribute('data-open', 'false');
    await expect(fixture).toHaveAttribute('data-requests', '2');
  });
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

function popupOf(page: Page): Locator {
  return page.getByTestId('popup');
}

test.describe('Escape (FLT-KEY-01, FLT-EVT-05)', () => {
  test('dismisses with escape key', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-conformance', { defaultOpen: true });
    await expect(popupOf(page)).toBeVisible();

    await page.keyboard.press('Escape');

    await expect(popupOf(page)).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key');
  });

  test('calls preventDefault on escape key dismiss', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-conformance', { defaultOpen: true });
    await expect(popupOf(page)).toBeVisible();

    const seen = await dispatchEscape(page);

    expect(seen).toEqual({ defaultPrevented: true, reachedWindow: false });
    await expect(popupOf(page)).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key');
  });

  test('does not call preventDefault on escape key if close is canceled', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-conformance', { defaultOpen: true, cancelClose: true });
    await expect(popupOf(page)).toBeVisible();

    const seen = await dispatchEscape(page);
    await settle(fixture);

    // Server: the consumer's cancel arrives a round trip after the dispatch, which got the fallback (ADR 01).
    expect(seen.defaultPrevented).toBe(mode === 'server');
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key');
    await expect(popupOf(page)).toBeVisible();
  });

  test('port: a canceled Escape close leaves the session armed, so the next Escape asks again', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-conformance', { defaultOpen: true, cancelClose: true });
    await expect(popupOf(page)).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key');
    // On Server the session re-arms when C#'s answer (`Answered`) arrives; the barrier waits for it.
    await settle(fixture);
    await page.keyboard.press('Escape');

    await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key false:escape-key');
    await expect(popupOf(page)).toBeVisible();
  });

  test('port: allowPropagation lets the Escape keydown reach the window while the popup closes', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'popup-conformance', { defaultOpen: true, allowPropagation: true });
    await expect(popupOf(page)).toBeVisible();

    const seen = await dispatchEscape(page);

    // WebAssembly: the verdict `prevent`; Server: the fallback stops the keydown (ADR 01).
    expect(seen).toEqual({ defaultPrevented: true, reachedWindow: mode === 'wasm' });
    await expect(popupOf(page)).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-open-changes', 'false:escape-key');
  });
});
