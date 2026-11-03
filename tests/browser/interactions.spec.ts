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

/** A screen reader's press: a 0 × 0 `pointerdown` (FLT-ALG-26), then `mousedown`, `mouseup` and `click`, in one task. */
async function pressVirtually(target: Locator): Promise<void> {
  await target.evaluate((element) => {
    const init = { bubbles: true, cancelable: true, composed: true, button: 0, buttons: 0, detail: 0 };
    const pointer = { ...init, pointerType: 'mouse', width: 0, height: 0, pressure: 0, isPrimary: true };
    element.dispatchEvent(new PointerEvent('pointerdown', pointer));
    element.dispatchEvent(new MouseEvent('mousedown', init));
    element.dispatchEvent(new PointerEvent('pointerup', pointer));
    element.dispatchEvent(new MouseEvent('mouseup', init));
    element.dispatchEvent(new MouseEvent('click', init));
  });
}

/** Holds every animation frame the page requests until the returned function releases them. */
async function holdFrames(page: Page): Promise<() => Promise<void>> {
  await page.evaluate(() => {
    const held: FrameRequestCallback[] = [];
    const native = window.requestAnimationFrame;
    window.requestAnimationFrame = (callback) => {
      held.push(callback);
      return 0;
    };
    (window as unknown as { releaseFrames: () => void }).releaseFrames = () => {
      window.requestAnimationFrame = native;
      for (const callback of held.splice(0)) {
        native.call(window, callback);
      }
    };
  });
  return () => page.evaluate(() => (window as unknown as { releaseFrames: () => void }).releaseFrames());
}

/** Resolves after two animation frames, so a press deferred to the next frame has fired. */
async function afterFrames(page: Page): Promise<void> {
  await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
}

// The mousedown press (M5 track 2), on the test-only host `interactions-mousedown`: two triggers that toggle from the
// mousedown as Menu's trigger will, one with ignoreMouse. A virtual press is a 0 × 0 `pointerdown`, which jsdom cannot
// type (FLT-ALG-26), so these rows are proven here (ADR 13 §1). The consumer's onmousedown is declined and runs once per
// mousedown: inside the press for a press inside its event, from Blazor's dispatch for a press deferred to a frame.
test.describe('mousedown press (FLT-PTR-01, FLT-PTR-02, FLT-A11Y-03)', () => {
  test('port: a virtual press is typed virtual: ignoreMouse drops a mouse press on a mousedown trigger, not a 0 × 0 pointerdown press', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'interactions-mousedown');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const trigger = fixture.locator('[data-trigger-ignore-mouse]');

    await trigger.click();
    // ignoreMouse drops the press inside the mousedown, before Blazor's dispatch ran the consumer's handler.
    await expect(fixture).toHaveAttribute('data-consumer-mousedowns', '1');
    await expect(fixture).toHaveAttribute('data-requests', '0');

    await pressVirtually(trigger.locator('[data-descendant]'));
    await expect(fixture).toHaveAttribute('data-open', 'true');
    await expect(fixture).toHaveAttribute('data-consumer-mousedowns', '2');
    await expect(fixture).toHaveAttribute('data-requests', '1');
  });

  test('port: a virtual press opens from the mousedown without a frame and reports the trigger, not the pressed descendant, while a mouse press waits the frame', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'interactions-mousedown');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const trigger = fixture.locator('[data-trigger-plain]');
    const id = await trigger.getAttribute('id');
    const box = await trigger.boundingBox();

    let release = await holdFrames(page);
    await pressVirtually(trigger.locator('[data-descendant]'));
    await expect(fixture).toHaveAttribute('data-open', 'true');
    await expect(fixture).toHaveAttribute('data-active-trigger', id!);
    await pressVirtually(trigger.locator('[data-descendant]'));
    await expect(fixture).toHaveAttribute('data-open', 'false');
    await release();

    release = await holdFrames(page);
    await page.mouse.click(box!.x + box!.width / 2, box!.y + box!.height / 2);
    // Blazor's dispatch ran the consumer's handler; the press waits the held frame.
    await expect(fixture).toHaveAttribute('data-consumer-mousedowns', '3');
    await expect(fixture).toHaveAttribute('data-requests', '2');
    await expect(fixture).toHaveAttribute('data-open', 'false');
    await release();
    await expect(fixture).toHaveAttribute('data-open', 'true');
    await expect(fixture).toHaveAttribute('data-requests', '3');
  });

  test('port: a consumer onmousedown veto holds for a virtual press and for a mouse press, whose deferred press runs the handler once', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'interactions-mousedown');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await fixture.locator('[data-veto]').check();
    const trigger = fixture.locator('[data-trigger-plain]');

    await pressVirtually(trigger);
    await expect(fixture).toHaveAttribute('data-consumer-mousedowns', '1');
    await trigger.click();
    await expect(fixture).toHaveAttribute('data-consumer-mousedowns', '2');
    // The deferred press has fired, so on Server its report is on the circuit before the box's change.
    await afterFrames(page);
    await fixture.locator('[data-veto]').uncheck();

    await trigger.click();
    await expect(fixture).toHaveAttribute('data-open', 'true');
    // The open came from the deferred press, which ran no second handler: the count is final.
    await expect(fixture).toHaveAttribute('data-consumer-mousedowns', '3');
    await expect(fixture).toHaveAttribute('data-requests', '1');
  });
});

// The platform flags (M5 track 2, design point 4; MNU-A11Y-01) on the test-only host `interactions-platform-flags`: C#'s scoped
// PlatformFlags reads the platform module's VoiceOver hint and macOS from the first interactive render on, and reads
// unread until the module answers, which a part renders as neither (ADR 01's row of 2026-11-04). The module computes
// them from `navigator.platform` when it loads, so the init script sets it before any module runs.
test.describe('platform flags (MNU-A11Y-01)', () => {
  for (const [platformName, expected] of [
    ['MacIntel', 'true'],
    ['Win32', 'false'],
  ] as const) {
    test(`port: the flags are unread until the module answers, then macOS and the VoiceOver hint read ${expected} on ${platformName}`, async ({ page, mode }) => {
      await page.addInitScript((value) => {
        Object.defineProperty(Navigator.prototype, 'platform', { get: () => value, configurable: true });
        Object.defineProperty(Navigator.prototype, 'maxTouchPoints', { get: () => 0, configurable: true });
      }, platformName);
      const release = await stall(page, 'interactions');
      const fixture = await gotoFixture(page, mode, 'interactions-platform-flags');

      await expect(fixture).toHaveAttribute('data-reading', 'true');
      await expect(fixture).toHaveAttribute('data-mac', 'unread');
      await expect(fixture).toHaveAttribute('data-voice-over', 'unread');

      await release();
      await expect(fixture).toHaveAttribute('data-mac', expected);
      await expect(fixture).toHaveAttribute('data-voice-over', expected);
    });
  }
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
