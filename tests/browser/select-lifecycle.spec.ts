// L4 for docs/components/select.md (M5 track 9, issue 173; L4 agent lifecycle): the L4 titles of Base UI's
// select/root/SelectRoot.test.tsx lines 29-2853 outside `conformance` and the form, press and navigation scopes: the
// `manual unmount lifecycle` describe, the root-level pointer titles, `prop: modal` and `scroll locking`, `prop: actionsRef`,
// `scroll arrows` and `select inside popover`, run on Server then WebAssembly with real Playwright input.
//
// Base UI's `pressWithTouch` fires `pointerdown` (touch) and `mousedown` on the element. Here a touch is a real tap: CDP touch
// emulation and `Input.dispatchTouchEvent` (a `touchStart`/`touchEnd` at the element's center), which the browser turns into
// a `pointerdown` of type `touch`, the `touchend` and the compatibility mouse events.
import { type CDPSession, type Locator } from '@playwright/test';
import { expect, isScrollLocked, test, type Page } from './helpers';
import { waitForPositioned } from './helpers/positioner';
import {
  animationStarts,
  gotoPopupFixture,
  openChangeCompletes,
  openChanges,
  pressControl,
  recordAnimationStarts,
  setRootOpen,
  settle,
} from './suites/popupFixture';

// The clock belongs to the worker's browser context, which every page of the worker shares: a title that froze it resumes it,
// or the next page's WebAssembly boot waits on its timers.
test.afterEach(async ({ page }) => {
  await page.clock.resume().catch(() => undefined);
});

const FREEZE_MARGIN = 50;

/** Pauses the page's clock: its timers and frames run only when the title runs it forward. */
async function freezeClock(page: Page): Promise<void> {
  for (let attempt = 1; ; attempt += 1) {
    const now = await page.evaluate(() => Date.now());
    try {
      await page.clock.pauseAt(now + FREEZE_MARGIN);
      return;
    } catch (error) {
      if (attempt === 3 || !String(error).includes('Cannot fast-forward to the past')) {
        throw error;
      }
    }
  }
}

// The popup of a Select stays in the DOM after a close (hidden), so "unmounted" is the role query's: no accessible listbox.
const listbox = (page: Page): Locator => page.getByRole('listbox');
const trigger = (page: Page): Locator => page.getByRole('combobox');

/** The count of `false` completions the fixture recorded. */
async function closeCompletes(fixture: Locator): Promise<number> {
  return (await openChangeCompletes(fixture)).filter((phase) => phase === 'false').length;
}

/** A real touch tap at the center of an element: touch emulation on, `touchStart` then `touchEnd`. */
async function touchSession(page: Page): Promise<CDPSession> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 1 });
  return cdp;
}

/**
 * Waits for the first pass of the Positioner around `target` before a title reads its box (issue 191): until then the
 * Positioner holds `opacity: 0` over the unpositioned layout. On a paused clock the wait steps the clock only while the pass
 * has not landed, so a title's timer accounting is unchanged once it has.
 */
async function positioned(target: Locator, clockFrozen = false): Promise<void> {
  // The target first: right after the press that opens the popup it may not be in the page yet.
  await target.waitFor({ state: 'attached' });
  const positioner = target.locator('xpath=ancestor::*[@data-testid="positioner" or @data-testid="select-positioner"]').last();
  if ((await positioner.count()) === 0) {
    return;
  }

  if ((await positioner.evaluate((element) => (element as HTMLElement).style.opacity)) === '0') {
    await waitForPositioned(positioner, { clockFrozen });
  }
}

async function tap(cdp: CDPSession, target: Locator): Promise<void> {
  await positioned(target);
  const box = (await target.boundingBox())!;
  const x = box.x + box.width / 2;
  const y = box.y + box.height / 2;
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

async function centerOf(target: Locator, clockFrozen = false): Promise<{ x: number; y: number }> {
  await positioned(target, clockFrozen);
  const box = (await target.boundingBox())!;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

test.describe('<Select.Root />', () => {
  test.describe('manual unmount lifecycle', () => {
    // SEL-ANI-06, SEL-EVT-11, SEL-KEY-10.
    test('automatically unmounts with an actions ref and completes closing once', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions');
      expect(await openChangeCompletes(fixture)).toEqual([]);
      await trigger(page).click();
      await expect(listbox(page)).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(() => closeCompletes(fixture)).toBe(1);

      // Calling the action after the automatic unmount must not repeat the completion.
      await pressControl(fixture.locator('[data-unmount]'));
      await settle(fixture);
      expect(await closeCompletes(fixture)).toBe(1);
    });

    // SEL-API-17, SEL-EVT-10, SEL-EVT-11, SEL-STA-10.
    test('keeps the popup mounted until the unmount action completes closing', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, retain: true });
      await page.getByRole('option').click();
      await expect(listbox(page)).toHaveCount(1);
      await settle(fixture);
      expect(await closeCompletes(fixture)).toBe(0);
      await pressControl(fixture.locator('[data-unmount]'));
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(() => closeCompletes(fixture)).toBe(1);

      await setRootOpen(fixture, true);
      await expect(listbox(page)).toBeVisible();
      await setRootOpen(fixture, false);
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(() => closeCompletes(fixture)).toBe(2);
    });

    // SEL-ANI-09, SEL-EVT-11.
    test('clears the opt-out when a controlled reopen interrupts a pending unmount', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, retain: true });
      await page.getByRole('option').click();
      await expect(listbox(page)).toHaveCount(1);
      await settle(fixture);
      expect(await closeCompletes(fixture)).toBe(0);
      await setRootOpen(fixture, true);
      await setRootOpen(fixture, false);
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(() => closeCompletes(fixture)).toBe(1);
    });

    // SEL-ANI-09, SEL-DIS-12, SEL-EVT-09, SEL-EVT-11.
    test('ignores an opt-out on a canceled close', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, cancelRetained: true });
      await page.getByRole('option').click();
      await settle(fixture);
      await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
      await expect(listbox(page)).toHaveCount(1);
      await pressControl(fixture.locator('[data-set-cancel="false"]'));
      await page.getByRole('option').click();
      await expect(listbox(page)).toHaveCount(0);
    });

    // SEL-ANI-09, SEL-EVT-10, SEL-EVT-11. `React.startTransition(() => setOpen(next))` is the fixture's `deferred`: the
    // controlled `open` follows the change one task later.
    test('keeps the opt-out when a controlled close is applied in a transition', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, retain: true, deferred: true });
      await page.getByRole('option').click();
      await expect(trigger(page)).toHaveAttribute('aria-expanded', 'false');
      await expect(listbox(page)).toHaveCount(1);
      await settle(fixture);
      expect(await closeCompletes(fixture)).toBe(0);

      await pressControl(fixture.locator('[data-unmount]'));
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(() => closeCompletes(fixture)).toBe(1);
    });

    // SEL-API-05, SEL-API-17, SEL-DIS-08, SEL-EVT-08, SEL-EVT-11.
    test('closes through the `close` action so `onOpenChange` can opt out', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, retain: true });
      await pressControl(fixture.locator('[data-close]'));
      await expect(trigger(page)).toHaveAttribute('aria-expanded', 'false');
      expect(await openChanges(fixture)).toEqual(['false:imperative-action']);
      await expect(listbox(page)).toHaveCount(1);

      await pressControl(fixture.locator('[data-unmount]'));
      await expect(listbox(page)).toHaveCount(0);
    });

    // SEL-ANI-06, SEL-EVT-11.
    test('ignores `unmount` while the popup is open', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true });
      await expect(listbox(page)).toBeVisible();
      await listbox(page).evaluate((element) => element.setAttribute('data-probe', 'same'));

      await pressControl(fixture.locator('[data-unmount]'));
      await settle(fixture);

      await expect(listbox(page)).toHaveAttribute('data-probe', 'same');
      expect(await closeCompletes(fixture)).toBe(0);
      await expect(trigger(page)).toHaveAttribute('aria-expanded', 'true');
    });

    // SEL-ANI-08, SEL-EVT-10, SEL-EVT-11.
    test('unmounts when `close` and `unmount` are called in one batch', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, retain: true });
      await pressControl(fixture.locator('[data-close-and-unmount]'));
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(() => closeCompletes(fixture)).toBe(1);
    });

    // SEL-ANI-06, SEL-EVT-11. A stale exit-animation callback can call `unmount()` after a quick reopen.
    test('still unmounts on a later close after `unmount` was called while open', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true });
      await expect(listbox(page)).toBeVisible();
      await pressControl(fixture.locator('[data-unmount]'));
      await settle(fixture);
      await expect(listbox(page)).toHaveCount(1);

      await page.keyboard.press('Escape');
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(async () => (await openChangeCompletes(fixture)).at(-1)).toBe('false');
    });

    // SEL-ANI-06, SEL-EVT-11. The fixture's `reopenOnComplete` is the exit-animation callback that reopens right after it unmounts.
    test('still unmounts on a later close after `unmount` and a reopen in one batch', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, retain: true, reopenOnComplete: true });
      await expect(listbox(page)).toBeVisible();
      await pressControl(fixture.locator('[data-close]'));
      await expect(listbox(page)).toHaveCount(1);

      await pressControl(fixture.locator('[data-unmount]'));
      await settle(fixture);
      await expect(listbox(page)).toHaveCount(1);
      await expect.poll(() => closeCompletes(fixture)).toBe(1);

      await pressControl(fixture.locator('[data-set-retain="false"]'));
      await page.getByRole('option').click();
      await expect(listbox(page)).toHaveCount(0);
      await expect.poll(() => closeCompletes(fixture)).toBe(2);
    });

    // SEL-API-17, SEL-DIS-08, SEL-EVT-11, SEL-STA-10.
    test('does not call `onOpenChange` when the `close` action is called while closed', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions');
      await pressControl(fixture.locator('[data-close]'));
      await settle(fixture);
      expect(await openChanges(fixture)).toEqual([]);
      await expect(trigger(page)).toHaveAttribute('aria-expanded', 'false');
    });

    // SEL-ANI-06, SEL-EVT-11.
    test('completes closing once when `unmount` is called twice in one batch', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { defaultOpen: true, retain: true });
      await page.getByRole('option').click();
      await expect(listbox(page)).toHaveCount(1);

      await pressControl(fixture.locator('[data-unmount-twice]'));
      await expect(listbox(page)).toHaveCount(0);
      await settle(fixture);
      expect(await closeCompletes(fixture)).toBe(1);
    });
  });

  test.describe('prop: onValueChange', () => {
    // SEL-PTR-12. Base UI ticks 200 ms of fake time before the click; here the page clock runs 200 ms.
    test('is not called twice on select', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-basic');
      await trigger(page).click();
      const option = page.getByRole('option', { name: 'b' });
      await expect(option).toBeVisible();
      await page.clock.runFor(200);
      await option.click();
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-value-changes', 'b:item-press');
    });
  });

  // SEL-PTR-20. The press lands on a div inside the popup element but beside the list (a DOM descendant of the popup, as the
  // requirement words it: Blazor has no portal-in-tree distinction a React portal into `document.body` makes).
  test('does not dismiss when pressing portalled content inside the popup but outside the list', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-basic', { defaultOpen: true, extra: 'content', items: 'apple' });
    await expect(listbox(page)).toBeVisible();
    await page.getByText('Portalled content').click();
    await expect(fixture).toHaveAttribute('data-portalled-presses', '1');
    await settle(fixture);
    await expect(listbox(page)).toBeVisible();
    expect(await openChanges(fixture)).toEqual([]);
  });

  // SEL-FOC-06. Browsers can focus the visually hidden input; focus lands on the visible control instead.
  test('redirects focus to the trigger when the hidden input is focused', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-lifecycle-basic', { name: 'select' });
    await page.locator('input[name="select"]').evaluate((input) => (input as HTMLInputElement).focus());
    await expect(trigger(page)).toBeFocused();
  });

  test.describe('prop: modal', () => {
    // SEL-PTR-19.
    test('should render an internal backdrop when `true`', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-lifecycle-basic', { modal: true, outside: true, items: '1' });
      await trigger(page).click();
      await expect(listbox(page)).toBeVisible();
      await expect(page.getByTestId('positioner')).toBeAttached();
      expect(await page.getByTestId('positioner').evaluate((element) => element.previousElementSibling?.getAttribute('role'))).toBe('presentation');
    });

    test.describe('scroll locking', () => {
      test.describe('interaction type tracking (openMethod)', () => {
        // SEL-POS-21, SEL-PTR-03, SEL-STA-08. Base UI holds the animation frames back so the reopen lands while the close
        // animation is pending; here the popup's exit animation is 1000 ms (the fixture's `exit-1000`), so the reopen always does.
        test('keeps touch interaction type when reopening quickly after close', async ({ page, mode }) => {
          const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-touch', { animations: 1, modal: true, exit: 1000 });
          const cdp = await touchSession(page);
          const open = trigger(page);

          await tap(cdp, open);
          await expect(listbox(page)).toBeVisible();
          await tap(cdp, open);
          await expect(open).toHaveAttribute('aria-expanded', 'false');

          // Re-open while the previous close animation is still pending.
          await tap(cdp, open);
          await expect(open).toHaveAttribute('aria-expanded', 'true');
          await expect(listbox(page)).toBeVisible();
          await page.clock.runFor(30);
          await settle(fixture);

          expect(await isScrollLocked(page)).toBe(false);
        });

        // SEL-ANI-03, SEL-DOM-20, SEL-POS-06, SEL-PTR-23, SEL-STA-08. The close animation is 1000 ms (Base UI: 100) so the
        // ending state is observed by an ordinary assertion.
        test('keeps touch positioning during the close transition', async ({ page, mode }) => {
          await gotoPopupFixture(page, mode, 'select-lifecycle-touch', { animations: 1, exit: 1000, padTop: 80 });
          const cdp = await touchSession(page);
          const open = trigger(page);

          await tap(cdp, open);
          const popup = listbox(page);
          await expect(popup).toBeVisible();
          const positioner = page.getByTestId('positioner');
          expect(await positioner.evaluate((element) => getComputedStyle(element).position)).toBe('absolute');

          await tap(cdp, open);
          await expect(popup).toHaveAttribute('data-ending-style', '');
          expect(await positioner.evaluate((element) => getComputedStyle(element).position)).toBe('absolute');
        });

        // SEL-PTR-09. Base UI: `fireEvent.pointerDown(optionB, touch)`, `click`, `mouseLeave(optionB, { clientX: -1, clientY: -1 })`.
        // Here: a real touch tap on the item (the pointerdown, the click and the close that follows); no `mouseleave` is
        // synthesized, a tap on a touch device produces none.
        test('keeps the selected item highlighted when reopening after a touch-driven mouseleave', async ({ page, mode }) => {
          await gotoPopupFixture(page, mode, 'select-lifecycle-touch', { case: 'highlight' });
          const cdp = await touchSession(page);
          const open = trigger(page);

          await tap(cdp, open);
          await expect(listbox(page)).toBeVisible();
          await tap(cdp, page.getByRole('option', { name: 'b' }));
          await expect(listbox(page)).toHaveCount(0);

          await tap(cdp, open);
          await expect(page.getByRole('option', { name: 'b' })).toHaveAttribute('data-highlighted', '');
        });

        // SEL-DIS-02, SEL-POS-06, SEL-PTR-23. The wrapper's padding puts the trigger 100 px above the viewport's bottom edge
        // (Base UI's layout effect).
        test('recomputes positioning before the popup becomes visible again after touch dismiss', async ({ page, mode }) => {
          const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-touch', { animations: 1, case: 'reopen' });
          const open = trigger(page);
          await expect(open).toBeVisible();
          await page.evaluate(() => {
            const wrapper = document.querySelector<HTMLElement>('[data-testid="wrapper"]')!;
            const button = document.querySelector<HTMLElement>('[role="combobox"]')!;
            for (let attempt = 0; attempt < 5; attempt += 1) {
              const gap = document.documentElement.clientHeight - button.getBoundingClientRect().bottom;
              if (Math.abs(gap - 100) <= 1) {
                break;
              }

              wrapper.style.paddingTop = `${(parseFloat(wrapper.style.paddingTop) || 0) + gap - 100}px`;
            }
          });
          const cdp = await touchSession(page);

          await tap(cdp, open);
          await expect(listbox(page)).toBeVisible();
          const positioner = page.getByTestId('positioner');
          await expect(positioner).toHaveAttribute('data-side', 'top');

          await tap(cdp, page.getByTestId('outside'));
          await expect(open).toHaveAttribute('aria-expanded', 'false');
          await expect.poll(async () => (await openChangeCompletes(fixture)).includes('false')).toBe(true);
          await expect(positioner).toHaveCSS('opacity', '0');

          await tap(cdp, open);
          await expect(positioner).not.toHaveCSS('opacity', '0');
          await expect(positioner).toHaveAttribute('data-side', 'top');
          const height = (await listbox(page).boundingBox())!.height;
          expect(height).toBeGreaterThan(200);

          // `user.click(outside)`: jsdom has no hit testing; here the modal popup's internal backdrop covers the button, so the
          // real click lands where the button is, on the backdrop.
          const outside = await centerOf(page.getByTestId('outside'));
          await page.mouse.click(outside.x, outside.y);
        });
      });

      test.describe('touch scroll lock', () => {
        // SEL-PTR-23, SEL-POS-21.
        test('applies scroll lock when a touch-opened popup covers the viewport width', async ({ page, mode }) => {
          await gotoPopupFixture(page, mode, 'select-lifecycle-touch', { modal: true, positionerWidth: 'full' });
          const cdp = await touchSession(page);
          await tap(cdp, trigger(page));
          await expect(listbox(page)).toBeVisible();
          await expect.poll(() => isScrollLocked(page)).toBe(true);
        });

        // SEL-PTR-23, SEL-POS-21.
        test('does not apply scroll lock when a touch-opened popup is narrower than the viewport', async ({ page, mode }) => {
          await gotoPopupFixture(page, mode, 'select-lifecycle-touch', { modal: true, positionerWidth: '240px' });
          const cdp = await touchSession(page);
          await tap(cdp, trigger(page));
          await expect(listbox(page)).toBeVisible();
          await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
          expect(await isScrollLocked(page)).toBe(false);
        });
      });
    });
  });

  test.describe('prop: actionsRef', () => {
    // SEL-API-17, SEL-DIS-04.
    test('unmounts the select when the `unmount` method is called', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-actions', { uncontrolled: true, retain: true });
      await trigger(page).click();
      await expect(listbox(page)).toBeVisible();
      await trigger(page).click();
      await expect(listbox(page)).toHaveCount(1);

      await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
      await pressControl(fixture.locator('[data-unmount]'));
      await expect(listbox(page)).toHaveCount(0);
    });

    // SEL-ANI-07, SEL-DOM-44, SEL-STA-29. React's `strict` render option has no Blazor counterpart: both cases run the same page.
    for (const strict of [false, true]) {
      test(`clears scroll arrow visibility when manually unmounted (strict: ${strict})`, async ({ page, mode }) => {
        const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-arrows', { case: 'unmount' });
        await trigger(page).click();
        const list = page.getByTestId('list');
        await expect(list).toBeVisible();
        // Base UI defines scrollTop 20 over a scroll height of 100 and a client height of 50, then fires `scroll`.
        await list.evaluate((element) => {
          element.scrollTop = 20;
        });

        const up = page.getByTestId('up-arrow');
        const down = page.getByTestId('down-arrow');
        const upHandle = await up.elementHandle();
        const downHandle = await down.elementHandle();
        await expect(up).toHaveAttribute('data-visible', '');
        await expect(down).toHaveAttribute('data-visible', '');

        await trigger(page).click();
        await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
        await pressControl(fixture.locator('[data-unmount]'));

        await expect(listbox(page)).toHaveCount(0);
        await expect.poll(() => upHandle!.evaluate((element) => element.hasAttribute('data-visible'))).toBe(false);
        await expect.poll(() => downHandle!.evaluate((element) => element.hasAttribute('data-visible'))).toBe(false);
      });
    }

    // SEL-ANI-13, SEL-DOM-36, SEL-FOC-01, SEL-KEY-11.
    test('does not leave a tabbable option while closed and kept mounted after tabbing out', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-lifecycle-basic', {
        inputs: true, modal: false, defaultValue: '1', items: '1|2', retainEveryClose: true,
      });
      await trigger(page).click();

      const option = page.getByTestId('item-1');
      await expect(option).toBeVisible();
      await option.focus();
      await expect(option).toBeFocused();
      await expect(option).toHaveAttribute('tabindex', '0');

      await page.keyboard.press('Tab');

      await expect(page.getByTestId('after')).toBeFocused();
      await expect(listbox(page)).not.toHaveAttribute('data-open');
      await expect(option).toHaveAttribute('tabindex', '-1');
    });
  });

  test.describe('scroll arrows', () => {
    // SEL-POS-12, SEL-STA-29. Base UI defines `scrollTop` (0.4, then 0.2), a scroll height of 60.6 and a client height of 60 on
    // the list node and fires `scroll`. A browser rounds `scrollHeight` and snaps `scrollTop`, so the page defines the same
    // properties on the list the moment it is inserted (an init script) and the title fires the `scroll` event itself.
    test('normalizes overlapping fractional scroll ranges when toggling scroll arrow visibility', async ({ page, mode }) => {
      await page.addInitScript(() => {
        const state = window as unknown as { __scrollTop: number };
        state.__scrollTop = 0.4;
        const patch = (node: Element) => {
          if (node.getAttribute?.('role') !== 'listbox' || Object.getOwnPropertyDescriptor(node, 'scrollTop')?.configurable) {
            return;
          }

          Object.defineProperty(node, 'scrollTop', { configurable: true, get: () => state.__scrollTop, set: (value: number) => { state.__scrollTop = value; } });
          Object.defineProperty(node, 'scrollHeight', { value: 60.6, configurable: true });
          Object.defineProperty(node, 'clientHeight', { value: 60, configurable: true });
        };

        new MutationObserver((records) => {
          for (const record of records) {
            record.addedNodes.forEach((added) => {
              if (added instanceof Element) {
                patch(added);
                added.querySelectorAll('[role="listbox"]').forEach(patch);
              }
            });
          }
        }).observe(document, { childList: true, subtree: true });
      });
      await gotoPopupFixture(page, mode, 'select-lifecycle-arrows', { case: 'fractional' });

      const list = page.getByRole('listbox');
      const up = page.getByTestId('up-arrow');
      const down = page.getByTestId('down-arrow');
      await expect(list).toBeVisible();
      await expect(up).toHaveAttribute('data-visible', '');
      await expect(down).not.toHaveAttribute('data-visible', '');

      await page.evaluate(() => {
        (window as unknown as { __scrollTop: number }).__scrollTop = 0.2;
        document.querySelector('[role="listbox"]')!.dispatchEvent(new Event('scroll', { bubbles: false }));
      });

      // Both arrows are updated by the same scroll handler.
      await expect(down).toHaveAttribute('data-visible', '');
      await expect(up).not.toHaveAttribute('data-visible', '');
    });

    // SEL-ANI-11, SEL-DOM-45, SEL-STR-10. Base UI defines scroll metrics (scrollTop 40 over 100 and 60); here the list has
    // three 34 px items under a 60 px maximum height (102 over 60) and the scroll is a real `scrollTop` to the end. The
    // arrow's `data-ending-style` is witnessed by the record of its `animationstart`.
    test('keeps a scroll arrow mounted while its exit animation runs', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-lifecycle-arrows', { animations: 1, case: 'exit' });
      await recordAnimationStarts(page, 'select-lifecycle-arrows');

      const list = page.getByRole('listbox');
      const arrow = page.getByTestId('down-arrow');
      await expect(list).toBeVisible();
      await expect(arrow).toHaveAttribute('data-visible', '');

      await page.getByTestId('list').evaluate((element) => {
        element.scrollTop = element.scrollHeight;
      });

      await expect.poll(async () => (await animationStarts(page)).some((record) => record.endingStyle && record.visible)).toBe(true);
      await expect(arrow).toHaveCount(0);
    });
  });

  test.describe('select inside popover', () => {
    /** A real held press on empty page: down, 50 ms, up. */
    async function heldPress(page: Page, x: number, y: number): Promise<void> {
      await page.mouse.move(x, y);
      await page.mouse.down();
      await page.waitForTimeout(50);
      await page.mouse.up();
    }

    const popover = (page: Page): Locator => page.getByTestId('popover-popup');
    const selectTrigger = (page: Page): Locator => page.getByTestId('select-trigger');

    // SEL-DIS-10, SEL-EDGE-13. `user.pointer({ keys: '[MouseLeft>]', target })` is a mouse button held on the select trigger,
    // released over the Gala item (the page clock is frozen so the release lands inside the selected delay).
    test('dismisses the popover with one outside press after reopening an aligned select', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-popover');
      await expect(fixture).toHaveAttribute('data-select-attached', 'true');
      await expect(popover(page)).toBeVisible();
      await freezeClock(page);

      const center = await centerOf(selectTrigger(page));
      await page.mouse.move(center.x, center.y);
      await page.mouse.down();
      await page.clock.runFor(32);
      const gala = page.getByRole('option', { name: 'Gala' });
      await expect(gala).toBeVisible();
      await settle(fixture);
      await page.clock.runFor(16);
      const galaCenter = await centerOf(gala, true);
      await page.mouse.move(galaCenter.x, galaCenter.y, { steps: 2 });
      await page.mouse.up();
      await page.clock.resume();

      await page.getByRole('option', { name: 'Fuji' }).click();
      await expect(listbox(page)).toHaveCount(0);
      await expect(popover(page)).toBeVisible();

      await heldPress(page, 1200, 700);
      await expect(popover(page)).toHaveCount(0);
    });

    // SEL-DIS-10, SEL-EDGE-13.
    test('dismisses the popover after reopening a selected aligned select', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-popover', { case: 'selected-reopen' });
      await expect(fixture).toHaveAttribute('data-attached', 'true');
      await page.getByRole('button', { name: 'Open popover' }).click();
      await expect(popover(page)).toBeVisible();
      await expect(fixture).toHaveAttribute('data-select-attached', 'true');

      await selectTrigger(page).click();
      await page.getByRole('option', { name: 'Gala' }).click();
      await expect(listbox(page)).toHaveCount(0);

      await freezeClock(page);
      const center = await centerOf(selectTrigger(page));
      await page.mouse.move(center.x, center.y);
      await page.mouse.down();
      await page.clock.runFor(32);
      const selected = page.getByRole('option', { name: 'Gala' });
      await expect(selected).toBeVisible();
      await settle(fixture);
      await page.clock.runFor(16);
      const selectedCenter = await centerOf(selected, true);
      await page.mouse.move(selectedCenter.x, selectedCenter.y, { steps: 2 });
      await page.mouse.up();
      await page.clock.resume();

      await page.getByRole('option', { name: 'Fuji' }).click();
      await expect(listbox(page)).toHaveCount(0);

      await heldPress(page, 1200, 700);
      await expect(popover(page)).toHaveCount(0);
    });

    // SEL-API-16, SEL-DIS-10, SEL-EDGE-13.
    test('keeps a modal popover open when choosing a non-modal aligned select item', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-popover', { case: 'modal-popover' });
      await expect(fixture).toHaveAttribute('data-select-attached', 'true');
      await selectTrigger(page).click();
      await expect(listbox(page)).toBeVisible();

      await page.getByRole('option', { name: 'Two' }).click();

      await expect(listbox(page)).toHaveCount(0);
      await expect(popover(page)).toBeVisible();
    });

    // SEL-DIS-01, SEL-DIS-10, SEL-EDGE-13, SEL-KEY-10.
    test('does not bubble Escape from a non-modal select to the popover', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-popover', { case: 'escape' });
      await expect(fixture).toHaveAttribute('data-select-attached', 'true');
      await selectTrigger(page).click();
      await expect(listbox(page)).toBeVisible();

      await page.keyboard.press('Escape');

      await expect(listbox(page)).toHaveCount(0);
      await expect(popover(page)).toBeVisible();
    });

    // SEL-DIS-10, SEL-EDGE-13, SEL-FOC-04.
    test('returns focus to the opener when a select is opened programmatically inside a popover', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-popover', { case: 'programmatic' });
      await expect(fixture).toHaveAttribute('data-select-attached', 'true');
      const opener = page.getByRole('button', { name: 'Open select programmatically' });
      await opener.click();
      await expect(listbox(page)).toBeVisible();

      await page.getByRole('option', { name: 'Two' }).click();

      await expect(listbox(page)).toHaveCount(0);
      await expect(opener).toBeFocused();
      await expect(popover(page)).toBeVisible();
    });

    // SEL-DIS-10, SEL-EDGE-13, SEL-PTR-04. Base UI fires the pointer and mouse events of a drag from the trigger to
    // (400, 20) and back at `document.body`; here the mouse button goes down on the trigger, the pointer travels to (400, 20)
    // and releases (a real native drag), and a later real click lands there.
    test('does not consume the next outside press after a native drag from a modal select trigger outside all popups', async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'select-lifecycle-popover', { case: 'drag' });
      await expect(fixture).toHaveAttribute('data-select-attached', 'true');
      const center = await centerOf(selectTrigger(page));
      await page.mouse.move(center.x, center.y);
      await page.mouse.down();
      await expect(listbox(page)).toBeVisible();

      await page.mouse.move(400, 20, { steps: 5 });
      await page.mouse.up();

      await expect(listbox(page)).toHaveCount(0);
      await expect(popover(page)).toBeVisible();

      await page.mouse.click(400, 20);
      await expect(popover(page)).toHaveCount(0);
    });
  });
});
