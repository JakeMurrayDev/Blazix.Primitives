import { expect, Fixture, fixtureUrl, test, type Page, type RenderMode } from './helpers';
import type { Locator, Route } from '@playwright/test';
import { delayCircuit } from './helpers/circuit';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { dialogRootSharedTests } from './suites/dialogRootSharedTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import {
  animationStarts,
  gotoPopupFixture,
  openChangeCompletes,
  openChanges,
  recordAnimationStarts,
  setRootOpen,
  settle,
  type PopupFixtureOptions,
} from './suites/popupFixture';
import { popupFocusPropsTests } from './suites/popupFocusPropsTests';

// The [L4] rows of docs/components/dialog.md (DLG), both render modes, Server first (ADR 04 §6). Titles are Base UI's
// (dialog/**/*.test.tsx at v1.9.0) unless marked "port:". The shared suites run with Dialog's options (DialogRoot.test.tsx).

test.describe('<Dialog.Root />', () => {
  popupConformanceTests({
    fixture: 'dialog-conformance',
    triggerMouseAction: 'click',
    expectedPopupRole: 'dialog',
  });

  dialogRootSharedTests({ fixture: 'dialog-shared', popupRole: 'dialog' });
});

// DialogRoot.detached-triggers.test.tsx: click, aria-expanded, no throw on a missing trigger, a Close part, no Positioner or
// Viewport.
detachedTriggersConformanceTests({
  fixture: 'dialog-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: false,
  close: true,
});

// DialogPopup.test.tsx: a modal dialog with a backdrop traps focus and marks the rest of the page, so it resolves the
// focus props through another path than a non-modal one.
test.describe('modal', () => {
  popupFocusPropsTests({ fixture: 'dialog-focus-props' });
});

test.describe('non-modal', () => {
  popupFocusPropsTests({ fixture: 'dialog-focus-props-nonmodal' });
});

// DialogRoot.test.tsx's `describe.for` over its three TestDialog components, on the `dialog-root` fixture
// (`triggers=contained|detached|multiple`). Base UI's fireEvent is a dispatched DOM event here; its user-event is a real
// Playwright action.
const triggerModes = [
  { name: 'contained triggers', triggers: 'contained' },
  { name: 'detached triggers', triggers: 'detached' },
  { name: 'multiple detached triggers', triggers: 'multiple' },
] as const;

for (const { name, triggers } of triggerModes) {
  test.describe(`when using '${name}'`, () => {
    const root = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) =>
      gotoPopupFixture(page, mode, 'dialog-root', { triggers, ...options });
    const dialog = (page: Page) => page.getByRole('dialog');

    test('rewires dismiss interactions after closing and reopening', async ({ page, mode }) => {
      await root(page, mode, { modal: false });
      const trigger = page.getByTestId('trigger');

      await trigger.click();
      await expect(dialog(page)).toHaveCount(1);
      await page.keyboard.press('Escape');
      await expect(dialog(page)).toHaveCount(0);

      await trigger.click();
      await expect(dialog(page)).toHaveCount(1);
      await page.locator('body').dispatchEvent('click');
      await expect(dialog(page)).toHaveCount(0);
    });

    test.describe('prop: onOpenChange', () => {
      test('calls onOpenChange with the reason for change when user clicks backdrop while the modal dialog is open', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true });

        await page.locator('[role="presentation"]').first().click();

        await expect.poll(() => openChanges(fixture)).toEqual(['false:outside-press']);
      });

      test('calls onOpenChange with the reason for change when user clicks outside while the non-modal dialog is open', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: false });

        await page.getByTestId('outside').click({ position: { x: 300, y: 10 } });

        await expect.poll(() => openChanges(fixture)).toEqual(['false:outside-press']);
      });

      test.describe('clicks on user backdrop', () => {
        test('detects clicks on user backdrop', async ({ page, mode }) => {
          const fixture = await root(page, mode, { defaultOpen: true, backdrop: true });

          await page.getByTestId('backdrop').click({ position: { x: 5, y: 5 } });

          await expect.poll(() => openChanges(fixture)).toEqual(['false:outside-press']);
        });

        test('does not change open state on non-main button clicks', async ({ page, mode }) => {
          const fixture = await root(page, mode, { defaultOpen: true, backdrop: true });

          await page.getByTestId('backdrop').click({ position: { x: 5, y: 5 }, button: 'right' });
          await settle(fixture);

          expect(await openChanges(fixture)).toEqual([]);
          await expect(dialog(page)).toHaveCount(1);
        });
      });
    });

    test.describe('prop: disablePointerDismissal', () => {
      for (const { disablePointerDismissal, expectDismissed, outcome } of [
        { disablePointerDismissal: true, expectDismissed: false, outcome: 'does not close' },
        { disablePointerDismissal: false, expectDismissed: true, outcome: 'closes' },
        { disablePointerDismissal: undefined, expectDismissed: true, outcome: 'closes' },
      ]) {
        test(`'${outcome}' the dialog when clicking outside if disablePointerDismissal=${disablePointerDismissal}`, async ({ page, mode }) => {
          const fixture = await root(page, mode, { defaultOpen: true, modal: false, disablePointerDismissal });
          const outside = page.getByTestId('outside');

          await outside.dispatchEvent('mousedown');
          await outside.dispatchEvent('click');
          await settle(fixture);

          expect((await openChanges(fixture)).length === 1).toBe(expectDismissed);
          await expect(dialog(page)).toHaveCount(expectDismissed ? 0 : 1);
        });
      }
    });

    test.describe('outside press event with backdrops', () => {
      test('uses intentional outside press with user backdrop (mouse): closes on click, not on mousedown', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: false, backdrop: true });
        const backdrop = page.getByTestId('backdrop');

        await backdrop.dispatchEvent('mousedown');
        await settle(fixture);
        await expect(dialog(page)).toHaveCount(1);
        expect(await openChanges(fixture)).toEqual([]);

        await backdrop.dispatchEvent('click');
        await expect(dialog(page)).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });

      test('uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: true });
        const internalBackdrop = page.locator('[role="presentation"]').first();

        await internalBackdrop.dispatchEvent('mousedown');
        await settle(fixture);
        await expect(dialog(page)).toHaveCount(1);
        expect(await openChanges(fixture)).toEqual([]);

        await internalBackdrop.dispatchEvent('click');
        await expect(dialog(page)).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });
    });

    test.describe('outside press event with backdrops (shadow DOM)', () => {
      // DLG-STR-04, API-18, DIS-06: the parts inside the portal moved into the shadow root resolve through the portal's
      // registered shadow roots (ADR 04 Amendments 2026-10-28, design point 9 of M4 track 8, PR 144).
      // Base UI attaches an open shadow root; the port's portal takes a closed one a page script holds
      // (blazixFixtureDom.lastClosedShadowRoot), whose elements the test reaches through that script, as Base UI's do through
      // the root it holds.
      const inShadow = (page: Page, selector: string) =>
        page.evaluate((s) => (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot | null } }).blazixFixtureDom.lastClosedShadowRoot?.querySelector(s) != null, selector);
      const clickInShadow = (page: Page, selector: string) =>
        page.evaluate((s) => {
          const element = (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot } }).blazixFixtureDom.lastClosedShadowRoot.querySelector(s) as HTMLElement;
          element.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true, cancelable: true }));
        }, selector);

      test('closing via intentional outside press with user backdrop (modal=true): works when portaled into a shadow DOM', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: true, backdrop: true, container: 'shadow' });
        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(true);

        await clickInShadow(page, '[data-testid="backdrop"]');

        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(false);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });

      test('closing via outside press: works when clicking another element inside the same shadow root', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: false, container: 'shadow' });
        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(true);
        await page.evaluate(() => {
          const shadow = (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot } }).blazixFixtureDom.lastClosedShadowRoot;
          const button = document.createElement('button');
          button.dataset.testid = 'shadow-outside';
          button.textContent = 'Outside';
          shadow.prepend(button);
        });

        await clickInShadow(page, '[data-testid="shadow-outside"]');

        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(false);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });

      test('closing via outside press: works when clicking outside the shadow root', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: false, container: 'shadow' });
        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(true);

        await page.locator('body').dispatchEvent('click');

        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(false);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });

      test('closing via outside press: works for a modal dialog when clicking outside the shadow root', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: true, container: 'shadow' });
        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(true);

        await page.locator('body').dispatchEvent('click');

        await expect.poll(() => inShadow(page, '[role="dialog"]')).toBe(false);
        expect(await openChanges(fixture)).toEqual(['false:outside-press']);
      });
    });

    test('waits for the exit transition to finish before unmounting', async ({ page, mode }) => {
      const fixture = await root(page, mode, { open: true, modal: false, keepMounted: true, animated: true, animations: 1 });
      await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
      await recordAnimationStarts(page, 'dialog-root');

      await setRootOpen(fixture, false);

      await expect
        .poll(() => animationStarts(page))
        .toContainEqual(expect.objectContaining({ name: 'dialog-root-exit', endingStyle: true, visible: true }));
      await expect(page.getByTestId('dialog-popup')).toBeHidden();
      await expect.poll(() => openChangeCompletes(fixture)).toEqual(['true', 'false']);
    });

    test.describe('prop: modal', () => {
      test('makes other interactive elements on the page inert when a modal dialog is open', async ({ page, mode }) => {
        await root(page, mode, { defaultOpen: true, modal: true });

        await expect(page.locator('[role="presentation"]')).toHaveCount(1);
        // markOthers hides the siblings of the path to the popup, so an ancestor of the outside button carries it.
        await expect.poll(() => page.getByTestId('outside-button').evaluate((el) => el.closest('[aria-hidden="true"]') !== null)).toBe(true);
      });

      test('does not make other interactive elements on the page inert when a non-modal dialog is open', async ({ page, mode }) => {
        const fixture = await root(page, mode, { defaultOpen: true, modal: false });

        await expect(dialog(page)).toHaveCount(1);
        await expect(page.locator('[role="presentation"]')).toHaveCount(0);
        await settle(fixture);
        expect(await page.getByTestId('outside-button').evaluate((el) => el.closest('[aria-hidden="true"]') !== null)).toBe(false);
      });
    });

    test.describe('pointerdown removal', () => {
      test('moves focus to the popup when a focused child is removed on pointerdown and outside press still dismisses', async ({ page, mode }) => {
        await root(page, mode, { defaultOpen: true, modal: 'trap-focus', case: 'remove-on-pointerdown' });
        const remove = page.getByTestId('remove');
        await expect(remove).toBeFocused();

        await remove.dispatchEvent('pointerdown');

        await expect(page.getByTestId('dialog-popup')).toBeFocused();
        await page.mouse.click(2, 2);
        await expect(dialog(page)).toHaveCount(0);
      });

      // DLG-PTR-17 re-cited (ADR 13 Amendments 2026-10-22): NumberField's scrub area is a prevented pointerdown inside
      // the popup (NumberField is a later milestone).
      test('port: dismisses on the first outside click after a pointerdown inside the popup whose default was prevented', async ({ page, mode }) => {
        await root(page, mode, { defaultOpen: true, modal: false, case: 'prevented-pointerdown' });
        const prevented = page.getByTestId('prevented');
        await prevented.evaluate((element) => {
          element.addEventListener('pointerdown', (event) => event.preventDefault());
          element.addEventListener('mousedown', (event) => event.preventDefault());
        });

        await prevented.dispatchEvent('pointerdown', { pointerType: 'mouse', button: 0 });
        await prevented.dispatchEvent('mousedown', { button: 0 });
        await page.locator('body').dispatchEvent('pointerup', { pointerType: 'mouse', button: 0 });
        await page.locator('body').dispatchEvent('mouseup', { button: 0 });
        await page.locator('body').dispatchEvent('click');

        await expect(dialog(page)).toHaveCount(0);
      });
    });

    test.describe('prop: onOpenChangeComplete', () => {
      test('waits for a restarted enter animation to finish', async ({ page, mode }) => {
        const fixture = await root(page, mode, { open: false, indicator: 'swap', animations: 1 });
        await recordAnimationStarts(page, 'dialog-root');

        await setRootOpen(fixture, true);
        await expect.poll(() => animationStarts(page)).toContainEqual(expect.objectContaining({ name: 'test-enter-a' }));
        await fixture.locator('[data-swap-animation]').click();
        expect(await openChangeCompletes(fixture)).toEqual([]);

        await expect.poll(() => openChangeCompletes(fixture)).toEqual(['true']);
        expect(await animationStarts(page)).toContainEqual(expect.objectContaining({ name: 'test-enter-b' }));
      });

      test('does not get called on open when dismissed during the enter animation', async ({ page, mode }) => {
        const fixture = await root(page, mode, { open: false, indicator: 'transition', animations: 1 });
        await page.evaluate(() => {
          const runs: string[] = [];
          (window as unknown as { transitionRuns: string[] }).transitionRuns = runs;
          document.addEventListener('transitionrun', (event) => runs.push((event.target as HTMLElement).dataset.testid ?? ''));
        });

        await setRootOpen(fixture, true);
        await expect.poll(() => page.evaluate(() => (window as unknown as { transitionRuns: string[] }).transitionRuns)).toContain('dialog-popup');
        await page.locator('body').dispatchEvent('click');

        await expect(page.getByTestId('dialog-popup')).toHaveCount(0);
        expect(await openChangeCompletes(fixture)).toEqual(['false']);
      });
    });

    test('does not dismiss previous modal dialog when clicking new modal dialog', async ({ page, mode }) => {
      await root(page, mode, { case: 'siblings' });

      await page.getByTestId('trigger').click();
      await page.getByRole('button', { name: 'Open nested 1' }).click();
      await page.getByRole('button', { name: 'Open nested 2' }).click();

      await expect(page.getByText('Final nested')).toBeVisible();
      await expect(page.getByTestId('level-1')).toHaveCount(1);
      await expect(page.getByTestId('level-2')).toHaveCount(1);
    });

    test('dismisses non-nested dialogs one by one', async ({ page, mode }) => {
      await root(page, mode, { case: 'siblings' });

      // Each dialog's initial focus is the readiness of its focus manager: Base UI marks the page at the commit, the port
      // when the manager's handle attaches, and a dialog opened before that attach would be marked as outside.
      await page.getByTestId('trigger').dispatchEvent('click');
      await expect(page.getByRole('button', { name: 'Open nested 1' })).toBeFocused();
      await page.getByRole('button', { name: 'Open nested 1' }).dispatchEvent('click');
      await expect(page.getByRole('button', { name: 'Open nested 2' })).toBeFocused();
      await page.getByRole('button', { name: 'Open nested 2' }).dispatchEvent('click');
      await expect(page.getByTestId('level-3')).toHaveCount(1);
      const backdrops = page.locator('[role="presentation"]');
      await expect(backdrops).toHaveCount(3);

      await backdrops.nth(2).dispatchEvent('click');
      await expect(page.getByTestId('level-3')).toHaveCount(0);
      await expect(page.getByTestId('level-2')).toHaveCount(1);
      await expect(page.getByTestId('level-1')).toHaveCount(1);

      await backdrops.nth(1).dispatchEvent('click');
      await expect(page.getByTestId('level-2')).toHaveCount(0);
      await expect(page.getByTestId('level-1')).toHaveCount(1);

      await backdrops.nth(0).dispatchEvent('click');
      await expect(page.getByTestId('level-1')).toHaveCount(0);
    });
  });
}

test('does not close on a right-button outside press', async ({ page, mode }) => {
  const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: 'trap-focus' });

  await page.getByTestId('outside-button').dispatchEvent('pointerdown', { bubbles: true, button: 2, pointerType: 'mouse' });
  await settle(fixture);

  await expect(page.getByRole('dialog')).toHaveCount(1);
  expect(await openChanges(fixture)).toEqual([]);
});

test.describe('touch outside press', () => {
  type Point = { x: number; y: number; id?: number };
  type TouchStep = { type: 'touchstart' | 'touchmove' | 'touchend'; touches: Point[]; changed?: Point[] };

  // Base UI's createTouch and fireEvent.touch*: TouchEvents built in the page on the outside button.
  async function touch(page: Page, steps: TouchStep[]): Promise<void> {
    await page.getByTestId('outside-button').evaluate((target, all) => {
      const make = (p: { x: number; y: number; id?: number }) => new Touch({ identifier: p.id ?? 1, target, clientX: p.x, clientY: p.y });
      for (const step of all) {
        target.dispatchEvent(
          new TouchEvent(step.type, { bubbles: true, cancelable: true, touches: step.touches.map(make), changedTouches: (step.changed ?? step.touches).map(make) }),
        );
      }
    }, steps);
  }

  // A finger tapping outside: a touchstart, a ~6 px touchmove that arms dismissal below the 10 px threshold, and a
  // touchend whose lifted point is in changedTouches (touches empty), as in real browsers.
  const tapOutside: TouchStep[] = [
    { type: 'touchstart', touches: [{ x: 50, y: 50 }] },
    { type: 'touchmove', touches: [{ x: 50, y: 56 }] },
    { type: 'touchend', touches: [], changed: [{ x: 50, y: 56 }] },
  ];

  for (const [title, modal] of [
    ['closes a non-modal dialog without a backdrop', false],
    ['closes a trap-focus dialog without a backdrop', 'trap-focus'],
  ] as const) {
    test(title, async ({ page, mode }) => {
      const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal });
      await expect(page.getByRole('dialog')).toHaveCount(1);

      await touch(page, tapOutside);

      await expect(page.getByRole('dialog')).toHaveCount(0);
      expect(await openChanges(fixture)).toEqual(['false:outside-press']);
    });
  }

  test('does not close when another touch is still active on touchend', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: false });

    await touch(page, [
      { type: 'touchstart', touches: [{ x: 50, y: 50 }, { x: 70, y: 70 }] },
      { type: 'touchmove', touches: [{ x: 50, y: 56 }, { x: 70, y: 70 }] },
      { type: 'touchend', touches: [{ x: 70, y: 70 }], changed: [{ x: 50, y: 56 }] },
    ]);
    await settle(fixture);

    await expect(page.getByRole('dialog')).toHaveCount(1);
    expect(await openChanges(fixture)).toEqual([]);
  });

  test('does not close when two touches are lifted simultaneously on touchend', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: false });

    await touch(page, [
      { type: 'touchstart', touches: [{ x: 50, y: 50 }, { x: 70, y: 70 }] },
      { type: 'touchmove', touches: [{ x: 50, y: 56 }, { x: 70, y: 76 }] },
      { type: 'touchend', touches: [], changed: [{ x: 50, y: 56 }, { x: 70, y: 76 }] },
    ]);
    await settle(fixture);

    await expect(page.getByRole('dialog')).toHaveCount(1);
    expect(await openChanges(fixture)).toEqual([]);
  });

  test('does not close when multiple touches move past the drag dismissal threshold', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: false });

    await touch(page, [
      { type: 'touchstart', touches: [{ x: 50, y: 50 }, { x: 70, y: 70, id: 2 }] },
      { type: 'touchmove', touches: [{ x: 50, y: 70 }, { x: 70, y: 90, id: 2 }] },
    ]);
    await settle(fixture);

    await expect(page.getByRole('dialog')).toHaveCount(1);
    expect(await openChanges(fixture)).toEqual([]);
  });

  // DLG-PTR-08's window (FLT-PTR-27): once 1000 ms have passed, a mousedown while the touch is still down and the touch's
  // lift dismiss nothing (useDismiss.ts:599-604, 704-711: the touchend clears the touch state, so only a mousedown before
  // it is gated). The window is a timer, which page.clock advances; the clock belongs to the worker's context, so the
  // title resumes it (track 6's rule).
  test('port: after the 1000 ms window neither a mousedown during the armed touch nor its lift closes the dialog', async ({ page, mode }) => {
    await page.clock.install();
    try {
      const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: false });
      await touch(page, tapOutside.slice(0, 2));

      await page.clock.runFor(1001);
      await page.getByTestId('outside-button').dispatchEvent('mousedown', { button: 0, buttons: 1 });
      await touch(page, tapOutside.slice(2));
      await settle(fixture);

      await expect(page.getByRole('dialog')).toHaveCount(1);
      expect(await openChanges(fixture)).toEqual([]);

      // Inside the window the same touch dismisses (the titles above).
      await touch(page, tapOutside);
      await expect(page.getByRole('dialog')).toHaveCount(0);
      expect(await openChanges(fixture)).toEqual(['false:outside-press']);
    } finally {
      await page.clock.resume();
    }
  });

  test('closes as soon as a single touch moves past the drag dismissal threshold', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: false });

    await touch(page, [
      { type: 'touchstart', touches: [{ x: 50, y: 50 }] },
      { type: 'touchmove', touches: [{ x: 50, y: 70 }] },
    ]);

    await expect(page.getByRole('dialog')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['false:outside-press']);
  });
});

test.describe('external scroll lock handoff', () => {
  // DialogRoot.test.tsx's isPageLocked: the viewport scroller's computed overflow hides or clips.
  const isPageLocked = (page: Page) =>
    page.evaluate(() => {
      const html = document.documentElement;
      const { overflow, overflowX, overflowY } = getComputedStyle(html);
      const htmlScrolls = /auto|scroll|overlay|hidden|clip/.test(overflow + overflowY + overflowX);
      return /hidden|clip/.test(getComputedStyle(htmlScrolls ? html : document.body).overflowY);
    });

  // Each entry reproduces a third-party locker's DOM mechanism; its cleanup is deferred past the dialog's own deferred
  // lock (200 ms), as an exit animation does in practice.
  const lockers = [
    {
      name: 'react-remove-scroll, via an attribute and a stylesheet',
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
      name: 'silk-hq, via the <body> overflow shorthand',
      lock: () => {
        document.body.style.setProperty('overflow', 'hidden');
        setTimeout(() => document.body.style.removeProperty('overflow'), 200);
      },
    },
    {
      name: 'Ariakit, via <html> overflow longhands',
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

  test.describe('when a third-party overlay is still unlocking', () => {
    for (const { name, lock } of lockers) {
      test.describe(`'${name}'`, () => {
        test('keeps the page locked until it takes over, and unlocks on close', async ({ page, mode }) => {
          const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { open: false });
          expect(`initial: ${await isPageLocked(page)}`).toBe('initial: false');

          // A sampler records the page's lock every 10 ms for 450 ms from the open click on: the record, not a poll of a
          // passing style, witnesses that there is no gap (ADR 06 §2).
          await page.evaluate((lockSource) => {
            const samples: { at: number; locked: boolean; open: boolean }[] = [];
            (window as unknown as { lockSamples: typeof samples }).lockSamples = samples;
            const start = performance.now();
            const sample = () => {
              const html = document.documentElement;
              const { overflow, overflowX, overflowY } = getComputedStyle(html);
              const htmlScrolls = /auto|scroll|overlay|hidden|clip/.test(overflow + overflowY + overflowX);
              const locked = /hidden|clip/.test(getComputedStyle(htmlScrolls ? html : document.body).overflowY);
              samples.push({ at: performance.now() - start, locked, open: document.querySelector('[role="dialog"]') !== null });
              if (performance.now() - start < 450) {
                setTimeout(sample, 10);
              }
            };
            // eslint-disable-next-line no-new-func
            new Function(`(${lockSource})()`)();
            sample();
          }, lock.toString());
          await setRootOpen(fixture, true);
          await expect(page.getByRole('dialog')).toHaveCount(1);
          await expect
            .poll(() => page.evaluate(() => (window as unknown as { lockSamples: { at: number }[] }).lockSamples.at(-1)?.at ?? 0))
            .toBeGreaterThanOrEqual(440);

          const samples = await page.evaluate(() => (window as unknown as { lockSamples: { at: number; locked: boolean; open: boolean }[] }).lockSamples);
          expect(samples.filter((s) => !s.locked).map((s) => Math.round(s.at))).toEqual([]);

          // The popup's own Close, by exact name: the fixture's "Set closed" control matches "Close" as a substring
          // until the modal's outside marking hides it (run 40719682511).
          await page.getByTestId('dialog-popup').getByRole('button', { name: 'Close', exact: true }).click();
          await expect(page.getByRole('dialog')).toHaveCount(0);
          await expect.poll(async () => `after close: ${await isPageLocked(page)}`).toBe('after close: false');
        });
      });
    }
  });

  test('locks immediately when an external <body> lock cannot affect the page', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { open: false });
    // <html> owns the viewport scroll, so `overflow: hidden` on <body> leaves the page scrollable and is not a lock.
    await page.evaluate(() => {
      document.documentElement.style.overflowY = 'scroll';
      document.body.style.overflowY = 'hidden';
    });

    await setRootOpen(fixture, true);

    // hasOwnScrollLock: Base UI hides <html>'s overflow-x itself; an external <body> lock never touches <html>.
    await expect.poll(() => page.evaluate(() => document.documentElement.style.overflowX === 'hidden')).toBe(true);
    // The popup's own Close, by exact name: the fixture's "Set closed" control matches "Close" as a substring until the
    // modal's outside marking hides it, which lands when the focus manager attaches (run 40719682511, WebAssembly).
    await page.getByTestId('dialog-popup').getByRole('button', { name: 'Close', exact: true }).click();
    await expect.poll(() => page.evaluate(() => document.documentElement.style.overflowX === 'hidden')).toBe(false);
  });
});

// Base UI drives Chromium's CDP Input.dispatchMouseEvent; Playwright's mouse is the same trusted input.
test('ignores a native click whose pointerdown opened the dialog', async ({ page, mode, browserName }) => {
  test.skip(browserName !== 'chromium', 'Chromium only, as Base UI (platform.engine.blink)');
  const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { open: false, backdrop: true, case: 'open-on-pointerdown' });
  const box = (await page.getByTestId('open-on-pointerdown').boundingBox())!;
  const center = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
  await page.evaluate(() => {
    const clicks: { trusted: boolean; target: string; insidePopup: boolean }[] = [];
    (window as unknown as { documentClicks: typeof clicks }).documentClicks = clicks;
    document.addEventListener(
      'click',
      (event) => {
        const target = event.target as HTMLElement;
        clicks.push({ trusted: event.isTrusted, target: target.dataset.testid ?? target.tagName, insidePopup: target.closest('[role="dialog"]') !== null });
      },
      true,
    );
  });
  const clicks = () => page.evaluate(() => (window as unknown as { documentClicks: { trusted: boolean; target: string; insidePopup: boolean }[] }).documentClicks);

  await page.mouse.move(center.x, center.y);
  await page.mouse.down();
  // Visible, not only present: the portal node is `display: none` in place until the module moves it (a round trip on
  // Server), and until then the backdrop does not cover the button the press began on.
  await expect(page.getByTestId('dialog-popup')).toBeVisible();
  await expect(page.getByTestId('backdrop')).toBeVisible();
  await page.mouse.up();

  await expect.poll(async () => (await clicks()).some((click) => click.trusted)).toBe(true);
  const trusted = (await clicks()).find((click) => click.trusted)!;
  expect(trusted.target).not.toBe('open-on-pointerdown');
  expect(trusted.insidePopup).toBe(false);
  await settle(fixture);
  await expect(page.getByTestId('dialog-popup')).toHaveCount(1);
  expect(await openChanges(fixture)).not.toContain('false:outside-press');

  // Only the gesture that opened the dialog is suppressed: a fresh press observed while open dismisses.
  await page.mouse.down();
  await page.mouse.up();
  await expect(page.getByTestId('dialog-popup')).toHaveCount(0);
  expect(await openChanges(fixture)).toContain('false:outside-press');
});

// DLG-FOC-06's Menu title re-cited (ADR 13 Amendments 2026-10-22; Menu is M5): the detached trigger lives in a plain
// toggled panel that closes when the dialog opens, so the trigger has unmounted when focus returns, and the toggle, focused
// when the dialog opened (the panel keeps focus where it is on press, as a menu item does), is the return target.
test('port: returns focus to the panel toggle when a detached dialog trigger in the closed panel unmounted', async ({ page, mode }) => {
  await gotoPopupFixture(page, mode, 'dialog-root', { triggers: 'detached', case: 'panel' });
  const toggle = page.getByTestId('panel-toggle');

  await toggle.click();
  await expect(toggle).toBeFocused();
  await page.getByTestId('panel-trigger').click();
  await expect(page.getByRole('dialog')).toHaveCount(1);
  await expect(page.getByTestId('panel')).toHaveCount(0);

  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(toggle).toBeFocused();
});

// DLG-KEY-06 and FOC-12's ScrollArea title re-cited (ScrollArea is a later milestone): a non-scrollable scroll container
// inside the popup.
test('port: keeps focus trapped when dialog content contains a non-scrollable scroll container', async ({ page, mode }) => {
  await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: 'trap-focus', case: 'scroll-area' });
  const popup = page.getByRole('dialog');
  const inside = () => popup.evaluate((element) => element.contains(document.activeElement));
  await expect.poll(inside).toBe(true);

  // Tab from the last tabbable lands on the inside guard and wraps on the next frame (FLT-KEY-05), so the settled focus
  // is asserted.
  await page.keyboard.press('Tab');
  await expect.poll(inside).toBe(true);
  await page.keyboard.press('Tab');
  await expect.poll(inside).toBe(true);
  await page.keyboard.press('Shift+Tab');
  await expect.poll(inside).toBe(true);
  await expect(page.getByTestId('outside-button')).not.toBeFocused();
  await expect(page.getByTestId('outside-after')).not.toBeFocused();
});

// DialogPopup.test.tsx's own browser titles, on the `dialog-popup` fixture.
// DialogRoot.test.tsx's two close-confirmation titles, whose confirmation is a controlled AlertDialog (DLG-KEY-10, FOC-06,
// FOC-07, FOC-11, FOC-16; ADL-FOC-01), on the `dialog-confirmation` fixture (M4 track 9).
test('returns focus into the dialog when a close confirmation opened by an outside press closes', async ({ page, mode }) => {
  const fixture = await gotoPopupFixture(page, mode, 'dialog-confirmation', { case: 'outside-press' });
  await page.getByTestId('trigger').click();
  await expect(page.getByRole('dialog')).toBeVisible();

  const textarea = page.getByTestId('textarea');
  await textarea.click();
  await page.keyboard.type('x');
  await expect(textarea).toBeFocused();

  // Pressing the backdrop moves focus to the body before the close is canceled and the confirmation dialog opens.
  await page.getByTestId('backdrop').click();

  const goBack = page.getByTestId('go-back');
  await expect(goBack).toBeFocused();
  expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:outside-press']);

  await page.keyboard.press('Enter');

  await expect(goBack).toHaveCount(0);
  await expect(textarea).toBeFocused();
  await expect(page.getByRole('dialog')).toBeVisible();
});

test('does not leak a non-modal popup field into an unrelated dialog return focus', async ({ page, mode }) => {
  const fixture = await gotoPopupFixture(page, mode, 'dialog-confirmation', { case: 'no-leak' });

  const field = page.getByTestId('nonmodal-field');
  await field.click();
  await expect(field).toBeFocused();

  // Blur the field to the body and open the confirmation while it is focused.
  await page.getByTestId('surface').click();

  const goBack = page.getByTestId('go-back');
  await expect(goBack).toBeFocused();

  await page.keyboard.press('Enter');

  await expect(goBack).toHaveCount(0);
  // The modal gate keeps the non-modal field out of the shared stack, so the confirmation must not restore focus into the
  // unrelated dialog.
  await settle(fixture);
  expect(await page.evaluate(() => document.activeElement?.getAttribute('data-testid') ?? document.activeElement?.tagName)).not.toBe('nonmodal-field');
  await expect(field).not.toBeFocused();
});

test.describe('<Dialog.Popup />', () => {
  test.describe('prop: initialFocus', () => {
    test('focuses the popup itself rather than inner content when opened by touch', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'dialog-popup', { case: 'touch' });
      const trigger = page.getByText('Open');

      await trigger.dispatchEvent('pointerdown', { pointerType: 'touch' });
      await trigger.dispatchEvent('click', { detail: 1 });

      // On touch the default focuses the popup to avoid opening the virtual keyboard.
      await expect(page.getByTestId('dialog')).toBeFocused();
      await expect(page.getByTestId('input')).not.toBeFocused();
    });
  });

  test.describe('display: contents ancestors', () => {
    test('keeps initial focus working when the popup is wrapped by a display: contents ancestor', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'dialog-popup', { case: 'contents' });

      await page.getByText('Open').click();

      await expect(page.getByTestId('dialog-input')).toBeFocused();
    });

    test('keeps trap-focus tab cycling inside the popup when wrapped by a display: contents ancestor', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'dialog-popup', { case: 'contents', modal: 'trap-focus', defaultOpen: true });
      const popup = page.getByTestId('dialog-popup');
      await expect(page.getByTestId('first-input')).toBeFocused();

      await page.keyboard.press('Tab');
      await expect(page.getByTestId('second-button')).toBeFocused();
      await page.keyboard.press('Tab');
      await expect(page.getByTestId('first-input')).toBeFocused();
      await expect(page.getByTestId('outside-before')).not.toBeFocused();
      await expect(page.getByTestId('outside-after')).not.toBeFocused();
      await page.keyboard.press('Shift+Tab');
      await expect(page.getByTestId('second-button')).toBeFocused();
      expect(await popup.evaluate((element) => element.contains(document.activeElement))).toBe(true);
    });
  });

  test.describe('prop: finalFocus', () => {
    test('respects finalFocus when initialFocus points outside the popup', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'dialog-popup', { case: 'outside-initial', final: true });

      await page.getByText('Open').click();
      await expect(page.getByTestId('initial-outside')).toBeFocused();
      await page.getByRole('button', { name: 'Close', includeHidden: true }).click();

      await expect(page.getByTestId('final-outside')).toBeFocused();
    });

    test('moves final focus to trigger if initialFocus points outside the popup and finalFocus is not specified', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'dialog-popup', { case: 'outside-initial' });

      await page.getByText('Open').click();
      await expect(page.getByTestId('initial-outside')).toBeFocused();
      await page.getByRole('button', { name: 'Close', includeHidden: true }).click();

      await expect(page.getByRole('button', { name: 'Open', includeHidden: true })).toBeFocused();
    });
  });
});

// DialogRoot.test.tsx's "closed shadow root" describe (DLG-STR-04, DIS-03, DIS-06, PTR-11, API-18), on the
// `dialog-shadow` fixture: the roots render in the page and their portals move into a closed shadow root a page script
// holds (Blazor renders components into the document only). Elements are found and clicked through that script's handle,
// as Base UI's test does through the root it holds. The parts inside the moved portals attach after the move and resolve
// through the portal's registered shadow roots (ADR 04 Amendments 2026-10-28, design point 9, PR 144).
test.describe('closed shadow root', () => {
  type PortalContainer = 'shadow root' | 'body' | undefined;
  const portalSetups: { outer: PortalContainer; nested: PortalContainer }[] = [
    { outer: 'shadow root', nested: undefined },
    { outer: 'shadow root', nested: 'shadow root' },
    { outer: 'shadow root', nested: 'body' },
    { outer: undefined, nested: undefined },
    { outer: undefined, nested: 'shadow root' },
  ];
  const queryValue = (target: PortalContainer) => (target === 'shadow root' ? 'shadow' : target);
  const named = (target: PortalContainer) => (target === undefined ? 'undefined' : `'${target}'`);

  async function open(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<Locator> {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-shadow', options);
    await expect(fixture).toHaveAttribute('data-ready', 'true');
    return fixture;
  }

  /** Whether an element with the test id is in the shadow root or the document. */
  const exists = (page: Page, testId: string) =>
    page.evaluate((id) => {
      const selector = `[data-testid="${id}"]`;
      const root = (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot | null } }).blazixFixtureDom.lastClosedShadowRoot;
      return (root?.querySelector(selector) ?? document.querySelector(selector)) != null;
    }, testId);

  /** Base UI's `click`: the element's own `click()`, wherever it is. */
  const click = (page: Page, testId: string) =>
    page.evaluate((id) => {
      const selector = `[data-testid="${id}"]`;
      const root = (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot | null } }).blazixFixtureDom.lastClosedShadowRoot;
      ((root?.querySelector(selector) ?? document.querySelector(selector)) as HTMLElement).click();
    }, testId);

  for (const { name, kind } of [
    { name: 'modal dialog', kind: 'modal' },
    { name: 'non-modal dialog', kind: 'non-modal' },
    { name: 'alert dialog', kind: 'alert' },
  ]) {
    test.describe(`nested '${name}'`, () => {
      for (const { outer, nested } of portalSetups) {
        // ADR 04 Amendments 2026-10-29 (design point 10 of M4 track 8, issue 146): Blazor dispatches C# event handlers
        // from the document's event target, the shadow host for an event from inside a shadow root, so a Close inside one
        // never runs. Where the nested popup sits in the shadow root, the port title closes it through the modules' paths.
        const nestedInShadow = nested === 'shadow root' || (outer === 'shadow root' && nested === undefined);
        if (!nestedInShadow) {
          test(`keeps the parent open on close (outer: ${named(outer)}, nested: ${named(nested)})`, async ({ page, mode }) => {
            const fixture = await open(page, mode, { kind, outer: queryValue(outer), nested: queryValue(nested) });

            await click(page, 'open-nested');
            await expect.poll(() => exists(page, 'nested')).toBe(true);

            await click(page, 'close-nested');

            await expect.poll(() => exists(page, 'nested')).toBe(false);
            expect(await exists(page, 'outer')).toBe(true);
            await settle(fixture);
            expect(await openChanges(fixture)).toEqual([]);
          });
          continue;
        }

        // An alert dialog closes on Escape, and a press on its backdrop never closes it (ADL-DIS-01, ADL-DIS-03).
        if (kind === 'alert') {
          test(`port: keeps the parent open when the nested alert dialog inside the shadow root closes on Escape, and a press on its backdrop leaves it open (outer: ${named(outer)}, nested: ${named(nested)})`, async ({ page, mode }) => {
            const fixture = await open(page, mode, { kind, outer: queryValue(outer), nested: queryValue(nested) });

            await click(page, 'open-nested');
            await expect.poll(() => exists(page, 'nested')).toBe(true);
            // A nested root's Backdrop renders only with ForceRender (DLG-STR-05): its backdrop is the internal one, the
            // first child of its portal node in the shadow root.
            await page.evaluate(() => {
              const root = (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot } }).blazixFixtureDom.lastClosedShadowRoot;
              const node = root.querySelector('[data-testid="nested"]')!.closest('[data-base-ui-portal]')!;
              (node.querySelector(':scope > [role="presentation"]') as HTMLElement).click();
            });
            await settle(fixture);
            expect(await exists(page, 'nested')).toBe(true);

            await page.keyboard.press('Escape');
            await expect.poll(() => exists(page, 'nested')).toBe(false);
            expect(await exists(page, 'outer')).toBe(true);
            await settle(fixture);
            expect(await openChanges(fixture)).toEqual([]);
          });
          continue;
        }

        test(`port: keeps the parent open when the nested dialog inside the shadow root closes on Escape and on an outside press (outer: ${named(outer)}, nested: ${named(nested)})`, async ({ page, mode }) => {
          const fixture = await open(page, mode, { kind, outer: queryValue(outer), nested: queryValue(nested) });

          await click(page, 'open-nested');
          await expect.poll(() => exists(page, 'nested')).toBe(true);
          await page.keyboard.press('Escape');
          await expect.poll(() => exists(page, 'nested')).toBe(false);
          expect(await exists(page, 'outer')).toBe(true);

          // A modal dialog's outside press closes it only on its own backdrop (DLG-PTR-10), here the internal backdrop in its
          // portal node (a nested root's Backdrop renders only with ForceRender, DLG-STR-05); a non-modal one on any element.
          await click(page, 'open-nested');
          await expect.poll(() => exists(page, 'nested')).toBe(true);
          if (kind === 'modal') {
            await page.evaluate(() => {
              const root = (globalThis as unknown as { blazixFixtureDom: { lastClosedShadowRoot: ShadowRoot } }).blazixFixtureDom.lastClosedShadowRoot;
              const node = root.querySelector('[data-testid="nested"]')!.closest('[data-base-ui-portal]')!;
              (node.querySelector(':scope > [role="presentation"][data-base-ui-inert]') as HTMLElement).click();
            });
          } else {
            await click(page, 'outside');
          }
          await expect.poll(() => exists(page, 'nested')).toBe(false);
          expect(await exists(page, 'outer')).toBe(true);

          await settle(fixture);
          expect(await openChanges(fixture)).toEqual([]);
        });
      }
    });
  }

  test('still dismisses on a press outside the popup', async ({ page, mode }) => {
    const fixture = await open(page, mode, { case: 'outside', outer: 'shadow' });
    expect(await exists(page, 'outer')).toBe(true);

    await click(page, 'outside');

    await expect.poll(() => exists(page, 'outer')).toBe(false);
    expect(await openChanges(fixture)).toEqual(['false:outside-press']);
  });
});

// DLG-STA-07's gate is the nested count, not the page's latest open popup (ADR 04 Amendments 2026-10-28, a fidelity
// correction of the 2026-10-27 settlement): two non-nested dialogs each answer an outside press of their own, and a
// Popover open inside a modal Dialog does not shield the Dialog's own backdrop.
test.describe('the topmost gate of non-nested dialogs', () => {
  test("port: a press on the first modal dialog's own backdrop, outside a later non-nested modal dialog's confined backdrop, closes only the first", async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { case: 'siblings', confine: true });
    await page.getByTestId('trigger').click();
    await expect(page.getByRole('button', { name: 'Open nested 1' })).toBeFocused();
    await page.getByRole('button', { name: 'Open nested 1' }).click();
    // The confining box is part of the page the first modal hides, so its content is found by test id.
    await expect(page.getByTestId('level-2').locator('button')).toBeFocused();

    await page.mouse.click(5, 5);

    await expect(page.getByTestId('level-1')).toHaveCount(0);
    await expect(page.getByTestId('level-2')).toBeVisible();
    expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:outside-press']);
  });

  test('port: a press on a modal dialog\'s own backdrop closes it and the popover open inside it', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-popover', { case: 'popover-in-dialog' });
    await expect(page.getByTestId('dialog')).toBeVisible();
    await page.getByRole('button', { name: 'Popover trigger' }).click();
    await expect(page.getByTestId('popover')).toBeVisible();

    const height = page.viewportSize()!.height;
    await page.mouse.click(10, height - 10);

    await expect(page.getByTestId('popover')).toHaveCount(0);
    await expect(page.getByTestId('dialog')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['false:outside-press']);
  });

  test('port: an outside press that moves no focus closes both of two open non-nested non-modal dialogs', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { case: 'siblings', modal: false });
    await page.getByTestId('trigger').click();
    await expect(page.getByRole('button', { name: 'Open nested 1' })).toBeFocused();
    await page.getByRole('button', { name: 'Open nested 1' }).dispatchEvent('click', { detail: 1 });
    await expect(page.getByTestId('level-2')).toBeVisible();

    // A dispatched press moves no focus, so neither dialog's focus-out close runs and the outside press is what closes.
    await page.getByTestId('outside-button').evaluate((target) => {
      const init = { bubbles: true, cancelable: true, button: 0, buttons: 1, detail: 1 };
      target.dispatchEvent(new PointerEvent('pointerdown', { ...init, pointerType: 'mouse', isPrimary: true }));
      target.dispatchEvent(new MouseEvent('mousedown', init));
      target.dispatchEvent(new PointerEvent('pointerup', { ...init, buttons: 0, pointerType: 'mouse', isPrimary: true }));
      target.dispatchEvent(new MouseEvent('mouseup', { ...init, buttons: 0 }));
      target.dispatchEvent(new MouseEvent('click', { ...init, buttons: 0 }));
    });

    await expect(page.getByTestId('level-1')).toHaveCount(0);
    await expect(page.getByTestId('level-2')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['true:trigger-press', 'false:outside-press', 'false:outside-press']);
  });
});

// DLG §16 Q10 (DLG-PTR-05): with `modal: 'trap-focus'` the press mode is sloppy, until a consumer Backdrop exists, when it
// becomes intentional for mouse too. A mousedown on the backdrop then closes nothing; its click closes.
test("port: a trap-focus dialog with a backdrop closes on the backdrop's click, not on its mousedown", async ({ page, mode }) => {
  const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, modal: 'trap-focus', backdrop: true });
  await expect(page.getByTestId('dialog-popup')).toBeVisible();
  await expect(page.getByTestId('backdrop')).toBeVisible();
  const height = page.viewportSize()!.height;

  await page.mouse.move(10, height - 10);
  await page.mouse.down();
  await settle(fixture);
  await expect(page.getByTestId('dialog-popup')).toBeVisible();
  expect(await openChanges(fixture)).toEqual([]);

  await page.mouse.up();
  await expect(page.getByTestId('dialog-popup')).toHaveCount(0);
  expect(await openChanges(fixture)).toEqual(['false:outside-press']);
});

// DialogRoot.detached-triggers.test.tsx (DLG-KEY-01, DLG-ANI-09; §16 Q11): an inactive detached trigger inside an open
// popover; Escape from it closes the non-modal dialog on every open, and the popover stays open (the interactions
// module's per-keydown mark: the dialog's session handles the keydown once and the popover's skips it).
test('closes a nonmodal dialog on Escape from an inactive trigger inside a popover on every open', async ({ page, mode }) => {
  await gotoPopupFixture(page, mode, 'dialog-popover', { case: 'popover-triggers' });
  await expect(page.getByTestId('popover')).toBeVisible();

  for (let open = 0; open < 3; open++) {
    await page.getByRole('button', { name: 'Dialog trigger 1' }).click();
    await expect(page.getByTestId('dialog')).toBeVisible();

    await page.getByRole('button', { name: 'Dialog trigger 2' }).focus();
    await page.keyboard.press('Escape');

    await expect(page.getByTestId('dialog')).toHaveCount(0);
    await expect(page.getByTestId('popover')).toBeVisible();
  }
});

// DialogRoot.detached-triggers.test.tsx's browser-only title (DLG-KEY-01), on the `dialog-root` fixture's two detached
// triggers.
test('closes a non-modal dialog with Escape from an inactive detached trigger', async ({ page, mode }) => {
  await gotoPopupFixture(page, mode, 'dialog-root', { triggers: 'multiple', modal: false });
  await page.getByTestId('trigger').click();
  await expect(page.getByTestId('dialog-popup')).toBeVisible();

  await page.getByTestId('trigger-2').focus();
  await page.keyboard.press('Escape');

  await expect(page.getByRole('dialog')).toHaveCount(0);
});

// dialog.md §9's Server relaxations (ADR 01's relaxation table), asserted per mode; on Server the circuit is delayed 75 ms
// each way so the round trip the rows name is visible.
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

  type Timing = { hidden?: number; removed?: number; startingRemoved?: number; startingClassRemoved?: number };

  /**
   * Records, from now on, when the popup (`dialog-popup`, created or present) loses `data-starting-style` and the
   * `cs-starting` class after having them, when it gains `hidden`, and when it leaves the document.
   */
  async function record(page: Page): Promise<void> {
    await page.evaluate(() => {
      const timing: Record<string, number> = {};
      (window as unknown as { timing: typeof timing }).timing = timing;
      const seen = new WeakMap<Element, { starting: boolean; startingClass: boolean }>();
      const popupOf = (node: Node) => (node instanceof Element ? node.closest('[data-testid="dialog-popup"]') ?? node.querySelector('[data-testid="dialog-popup"]') : null);
      new MutationObserver((records) => {
        const now = performance.now();
        for (const entry of records) {
          if (entry.type === 'childList') {
            for (const node of entry.removedNodes) {
              if (popupOf(node) !== null && timing.removed === undefined) {
                timing.removed = now;
              }
            }
            for (const node of entry.addedNodes) {
              const popup = popupOf(node);
              if (popup !== null) {
                seen.set(popup, { starting: popup.hasAttribute('data-starting-style'), startingClass: popup.classList.contains('cs-starting') });
              }
            }
            continue;
          }

          const popup = entry.target as Element;
          if (popup.getAttribute('data-testid') !== 'dialog-popup') {
            continue;
          }

          const state = seen.get(popup) ?? { starting: false, startingClass: false };
          seen.set(popup, state);
          if (popup.hasAttribute('data-starting-style')) {
            state.starting = true;
          } else if (state.starting && timing.startingRemoved === undefined) {
            timing.startingRemoved = now;
          }
          if (popup.classList.contains('cs-starting')) {
            state.startingClass = true;
          } else if (state.startingClass && timing.startingClassRemoved === undefined) {
            timing.startingClassRemoved = now;
          }
          if (popup.hasAttribute('hidden') && timing.hidden === undefined) {
            timing.hidden = now;
          }
        }
      }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden', 'data-starting-style', 'class'] });
    });
  }

  const timingOf = (page: Page) => page.evaluate(() => (window as unknown as { timing: Timing }).timing);

  // DLG-KEY-02, DLG-DIS-07 (the EVD-EVT-11 / EVD-DIS-01 / FLT-KEY-01 row).
  test('port: allowPropagation lets the Escape keydown reach the window on WebAssembly, and the Server fallback stops it while the dialog closes', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, allowPropagation: true });
    await expect(page.getByTestId('dialog-popup')).toBeVisible();

    const seen = await dispatchEscape(page);

    expect(seen).toEqual({ defaultPrevented: true, reachedWindow: mode === 'wasm' });
    await expect(page.getByRole('dialog')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['false:escape-key']);
  });

  test('port: a canceled Escape close is not prevented on WebAssembly, and on Server the fallback prevented it before the cancel arrived', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true, cancelClose: true });
    await expect(page.getByTestId('dialog-popup')).toBeVisible();

    const seen = await dispatchEscape(page);
    await settle(fixture);

    expect(seen.defaultPrevented).toBe(mode === 'server');
    expect(await openChanges(fixture)).toEqual(['false:escape-key']);
    await expect(page.getByTestId('dialog-popup')).toBeVisible();
  });

  // DLG-API-05, DLG-EVT-08, DLG-ANI-03 (the TRN-ANI-03/09/10 row).
  test('port: an animated popup is hidden at its exit end, and removed with onOpenChangeComplete(false) in a later batch, a round trip later on Server', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { animated: true, animations: 1 });
    await page.getByTestId('trigger').click();
    await expect(page.getByTestId('dialog-popup')).toBeVisible();
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true');
    await record(page);

    await page.keyboard.press('Escape');
    await expect(page.getByTestId('dialog-popup')).toHaveCount(0);

    const timing = await timingOf(page);
    expect(timing.hidden).toBeDefined();
    expect(timing.removed).toBeDefined();
    expect(timing.hidden!).toBeLessThanOrEqual(timing.removed!);
    if (mode === 'server') {
      expect(timing.removed! - timing.hidden!).toBeGreaterThanOrEqual(140);
    }
    await expect(fixture).toHaveAttribute('data-open-change-completes', 'true false');
  });

  // DLG-STA-11 (the RND-STA-01 row for mirrored fields): C# renders the opening status itself (the handover), JavaScript
  // removes `data-starting-style` on the next frame, and the report of that change reaches the class function after it.
  test("port: the popup's class function leaves the starting status after JavaScript removes data-starting-style, a round trip later on Server", async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'dialog-root', { animated: true, animations: 1 });
    await record(page);

    await page.getByTestId('trigger').click();
    await expect(page.getByTestId('dialog-popup')).toBeVisible();
    await expect(page.getByTestId('dialog-popup')).not.toHaveClass(/cs-starting/);

    const timing = await timingOf(page);
    expect(timing.startingRemoved).toBeDefined();
    expect(timing.startingClassRemoved).toBeDefined();
    expect(timing.startingRemoved!).toBeLessThanOrEqual(timing.startingClassRemoved!);
    if (mode === 'server') {
      expect(timing.startingClassRemoved! - timing.startingRemoved!).toBeGreaterThanOrEqual(140);
    }
  });
});

// The attach-order rule (the standing rule of CI run 40461905812): the Popup attaches three handles to its element
// (focus-manager, interactions, transitions) and the Trigger two (button, interactions), each from the same after-render
// and none waiting for another. Each title holds one module's script back, asserts the route was hit and what the page
// already does without it, then releases it and asserts the full outcome.
test.describe('attach order', () => {
  async function stall(page: Page, module: string): Promise<{ hits: () => number; release: () => Promise<void> }> {
    const held: Route[] = [];
    let hits = 0;
    let released = false;
    await page.route(new RegExp(`/_content/Blazix\\.Primitives/js/${module}(\\.[0-9a-z]+)?\\.js$`), (route) => {
      hits += 1;
      return released ? route.continue() : held.push(route);
    });
    return {
      hits: () => hits,
      release: async () => {
        released = true;
        for (const route of held.splice(0)) {
          await route.continue();
        }
      },
    };
  }

  async function gotoUnready(page: Page, mode: RenderMode, options: PopupFixtureOptions): Promise<Locator> {
    const query = new URLSearchParams(Object.entries(options).map(([key, value]) => [key, String(value)]));
    await page.goto(`${fixtureUrl(mode, 'dialog-root')}?${query}`, { waitUntil: 'commit' });
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await page.locator(Fixture.interactive('dialog-root')).first().waitFor({ state: 'attached' });
    return page.locator(Fixture.container('dialog-root'));
  }

  const inside = (page: Page) => () => page.getByRole('dialog').evaluate((element) => element.contains(document.activeElement));

  test('port: with js/focus-manager.js stalled a default-open modal popup attaches its other handles first, and focus moves in and returns once the module arrives', async ({ page, mode }) => {
    const focusManager = await stall(page, 'focus-manager');
    const fixture = await gotoPopupFixture(page, mode, 'dialog-root', { defaultOpen: true });
    await expect.poll(focusManager.hits).toBeGreaterThan(0);
    await expect(page.getByTestId('dialog-popup')).toBeVisible();
    expect(await inside(page)()).toBe(false);

    await focusManager.release();
    await expect.poll(inside(page)).toBe(true);
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['false:escape-key']);

    const trigger = page.getByTestId('trigger');
    await trigger.click();
    await expect.poll(inside(page)).toBe(true);
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(trigger).toBeFocused();
  });

  test('port: with js/interactions.js stalled a default-open modal popup traps focus first, and closes on Escape and an outside press once the module arrives', async ({ page, mode }) => {
    const interactions = await stall(page, 'interactions');
    const fixture = await gotoUnready(page, mode, { defaultOpen: true });
    await expect.poll(interactions.hits).toBeGreaterThan(0);
    await expect.poll(inside(page)).toBe(true);
    await expect(fixture).toHaveAttribute('data-attached', 'false');

    await interactions.release();
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toHaveCount(0);

    await page.getByTestId('trigger').click();
    await expect.poll(inside(page)).toBe(true);
    await page.mouse.click(5, 5);
    await expect(page.getByRole('dialog')).toHaveCount(0);
    expect(await openChanges(fixture)).toEqual(['false:escape-key', 'true:trigger-press', 'false:outside-press']);
  });

  for (const module of ['button', 'interactions']) {
    test(`port: with js/${module}.js stalled the trigger attaches its other handle first, and opens the dialog once when the module arrives`, async ({ page, mode }) => {
      const stalled = await stall(page, module);
      const fixture = await gotoUnready(page, mode, {});
      await expect.poll(stalled.hits).toBeGreaterThan(0);
      await expect(fixture).toHaveAttribute('data-attached', 'false');

      await stalled.release();
      await expect(fixture).toHaveAttribute('data-attached', 'true');
      await page.getByTestId('trigger').click();

      await expect(page.getByTestId('dialog-popup')).toBeVisible();
      await settle(fixture);
      expect(await openChanges(fixture)).toEqual(['true:trigger-press']);
    });
  }
});

test.describe('the Server gap of a newly created modal popup (ADR 01, M4 track 8)', () => {
  // Deliverable 12 of issue 123, issue 2's method: the circuit's WebSocket delayed each way by page.routeWebSocket, and
  // 20 opens of a modal dialog the opening render creates (not kept mounted). A page observer times each signal from the
  // opening batch's `data-open` (t0): the portal node's move to <body>, focus landing inside the popup, the first outside
  // `aria-hidden`, and the scroll lock (an inline `overflow: hidden` on <html> or <body>, or `data-base-ui-scroll-locked`).
  // The trap is armed by the same focus-manager attach that moves initial focus. A kept-mounted dialog is the control.
  type Signals = { open?: number; moved?: number; focus?: number; ariaHidden?: number; locked?: number };
  const opens = 20;

  async function armObserver(page: Page): Promise<void> {
    await page.evaluate(() => {
      const t: { open?: number; moved?: number; focus?: number; ariaHidden?: number; locked?: number } = {};
      (window as unknown as { dialogSignals: typeof t }).dialogSignals = t;
      const html = document.documentElement;
      const check = (records: MutationRecord[]) => {
        const now = performance.now();
        const popup = document.querySelector('[data-testid="dialog-popup"]');
        if (t.open === undefined && popup?.hasAttribute('data-open')) {
          t.open = now;
        }
        if (t.open !== undefined && t.moved === undefined && popup?.closest('[data-base-ui-portal]')?.parentElement === document.body) {
          t.moved = now;
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
      };
      new MutationObserver(check).observe(html, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-open', 'aria-hidden', 'style', 'data-base-ui-scroll-locked'] });
      document.addEventListener(
        'focusin',
        (event) => {
          if (t.focus === undefined && (event.target as Element).closest('[data-testid="dialog-popup"]')) {
            t.focus = performance.now();
          }
        },
        true,
      );
    });
  }

  async function measure(page: Page, mode: RenderMode, keepMounted: boolean): Promise<Record<string, number[]>> {
    await gotoPopupFixture(page, mode, 'dialog-root', { keepMounted });
    const gaps: Record<string, number[]> = { moved: [], focus: [], ariaHidden: [], locked: [] };
    for (let run = 0; run < opens; run++) {
      await armObserver(page);
      await page.getByTestId('trigger').click();
      await expect
        .poll(() => page.evaluate(() => { const t = (window as unknown as { dialogSignals: Signals }).dialogSignals; return t.focus !== undefined && t.ariaHidden !== undefined && t.locked !== undefined && t.moved !== undefined; }))
        .toBe(true);
      const t = await page.evaluate(() => (window as unknown as { dialogSignals: Signals }).dialogSignals);
      for (const key of ['moved', 'focus', 'ariaHidden', 'locked'] as const) {
        gaps[key]!.push(Math.max(0, t[key]! - t.open!));
      }
      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toHaveCount(0);
      await expect.poll(() => page.evaluate(() => document.documentElement.style.overflow === '' && !document.documentElement.hasAttribute('data-base-ui-scroll-locked'))).toBe(true);
    }
    return gaps;
  }

  function summarize(gaps: Record<string, number[]>): Record<string, { medianMs: number; p95Ms: number; maxMs: number }> {
    const summary: Record<string, { medianMs: number; p95Ms: number; maxMs: number }> = {};
    for (const [key, values] of Object.entries(gaps)) {
      const sorted = [...values].sort((a, b) => a - b);
      const round = (value: number) => Math.round(value * 10) / 10;
      summary[key] = {
        medianMs: round(sorted[Math.floor(sorted.length / 2)]!),
        p95Ms: round(sorted[Math.min(sorted.length - 1, Math.ceil(sorted.length * 0.95) - 1)]!),
        maxMs: round(sorted[sorted.length - 1]!),
      };
    }
    return summary;
  }

  for (const delay of [0, 25, 75]) {
    test(`port: a newly created modal popup's move, focus, outside aria-hidden and scroll lock after its opening batch (round trip +${delay * 2} ms)`, async ({ page, mode }) => {
      test.skip(mode === 'wasm' && delay > 0, 'the circuit exists on Server only');
      test.setTimeout(180_000);
      if (delay > 0) {
        await delayCircuit(page, delay);
      }

      const gaps = await measure(page, mode, false);

      const summary = summarize(gaps);
      await test.info().attach(`dialog-created-popup-gap-${mode}-${delay * 2}ms.json`, { body: JSON.stringify({ mode, addedRoundTripMs: delay * 2, opens, summary, gaps }), contentType: 'application/json' });
      console.log(`DLG-GAP ${mode} +${delay * 2}ms ${JSON.stringify(summary)}`);
      for (const values of Object.values(gaps)) {
        expect(values).toHaveLength(opens);
      }
    });
  }

  test('port: a kept-mounted modal popup moves focus, hides the outside and locks scroll in its opening batch on both modes', async ({ page, mode }) => {
    test.setTimeout(180_000);
    if (mode === 'server') {
      await delayCircuit(page, 75);
    }

    const gaps = await measure(page, mode, true);

    const summary = summarize(gaps);
    await test.info().attach(`dialog-kept-mounted-gap-${mode}.json`, { body: JSON.stringify({ mode, opens, summary, gaps }), contentType: 'application/json' });
    console.log(`DLG-GAP kept-mounted ${mode} ${JSON.stringify(summary)}`);
    // The node was moved by the first open; focus and the outside marking follow the commit-path options, so no signal
    // waits for a round trip (75 ms each way here); the scroll lock's own 0 ms deferral is a task, not a round trip.
    expect(summary.focus!.medianMs).toBeLessThan(150);
    expect(summary.ariaHidden!.medianMs).toBeLessThan(150);
  });
});

// Issue 147 (DLG-EVT-05's 'ending' clause, TRN-ANI-23; ADR 04 Amendments 2026-10-29, (B1) with issue 148): a close kept
// mounted by PreventUnmountOnClose keeps the popup's ending phase until Unmount(). The popup announces it through the
// transitions module's export at the call, so it holds on Server under the circuit's latency, for a close inside the popup's
// first attach round trip too.
test.describe('<Dialog.Root /> a retained close', () => {
  for (const latency of [0, 250]) {
    const suffix = latency === 0 ? '' : ', within the first attach round trip under circuit latency on Server';
    test(`port: a close kept mounted by PreventUnmountOnClose keeps data-ending-style until Unmount()${suffix}`, async ({ page, mode }) => {
      if (latency > 0 && mode === 'server') {
        await delayCircuit(page, latency);
      }
      const fixture = await gotoPopupFixture(page, mode, 'dialog-shared', { case: 'prevent-unmount' });
      const trigger = page.getByRole('button', { name: 'Open', exact: true, includeHidden: true });
      // Scoped to the portal: a hidden-role query of the page also finds Blazor's own reconnect <dialog>.
      const popup = page.locator('[data-base-ui-portal] [role="dialog"]');
      await trigger.click();
      await expect(popup).toBeVisible();

      // At once: under latency the popup's transitions attach is still answering.
      await trigger.click();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(popup).toHaveAttribute('data-ending-style');
      await settle(fixture);
      await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      await expect(popup).toHaveAttribute('data-ending-style');
      await expect(popup).not.toHaveAttribute('hidden');

      await fixture.locator('[data-unmount]').click();
      await expect(popup).toHaveCount(0);
    });
  }
});
