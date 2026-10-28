import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { gotoPopupFixture, openChanges, pressControl, settle, type PopupFixtureOptions } from './suites/popupFixture';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { gotoPositionerCase, positionerConformanceTests } from './suites/positioner-conformance';
import { viewportConformanceTests } from './suites/viewportConformanceTests';

// The [L4] rows of docs/components/preview-card.md (M4 track 7, issue 122) on the PreviewCard fixtures, both render modes,
// Server first. Base UI's titles verbatim; port titles start with "port:". The shared suites run with PreviewCard's options
// (PreviewCardRoot.test.tsx:31-46, PreviewCardRoot.detached-triggers.test.tsx:35-49, PreviewCardPositioner.test.tsx:108-122,
// PreviewCardViewport.test.tsx:25-47).

popupConformanceTests({
  fixture: 'preview-card-conformance',
  triggerMouseAction: 'hover',
});

detachedTriggersConformanceTests({
  fixture: 'preview-card-triggers',
  openInteractions: ['hover', 'focus'],
  ariaExpanded: false,
  throwOnMissingTrigger: true,
  close: false,
  positioner: true,
  viewport: true,
});

positionerConformanceTests({ fixture: 'preview-card-positioner', viewport: true });

viewportConformanceTests({
  fixture: 'preview-card-viewport',
  openOn: 'focus',
});

const OPEN_DELAY = 600;
const CLOSE_DELAY = 300;

// The clock belongs to the worker's browser context, which every page of the worker shares: a title that paused it
// resumes it, or the next page's WebAssembly boot waits on its timers.
test.afterEach(async ({ page }) => {
  await page.clock.resume();
});

const frozenPages = new WeakSet<Page>();

const FREEZE_MARGIN = 50;

/**
 * Pauses the page's clock: its timers fire only when the test runs it forward. The page's clock runs on while the target
 * travels to the browser, so the target sits a margin ahead, read again if the clock passed it on a slow runner; the
 * titles freeze right after the fixture attached, when no timer a title watches is pending.
 */
async function freezeClock(page: Page): Promise<void> {
  for (let attempt = 1; ; attempt += 1) {
    const now = await page.evaluate(() => Date.now());
    try {
      await page.clock.pauseAt(now + FREEZE_MARGIN);
      break;
    } catch (error) {
      if (attempt === 3 || !String(error).includes('Cannot fast-forward to the past')) {
        throw error;
      }
    }
  }

  frozenPages.add(page);
}

/**
 * Moves the real pointer away from the cards, to the page's bottom-left corner. The safe polygon decides on the leave only
 * once the session holds the floating element and its placement; on Server they arrive after C#'s render, and until then
 * it waits for a later move. The first positioning pass removes the positioner's `opacity: 0` and publishes the placement
 * in one step (FLT-STA-08, FLT-ANI-02), so no open positioner may still carry it. Under a paused clock the frames run
 * first.
 */
async function moveAway(page: Page): Promise<void> {
  if (frozenPages.has(page)) {
    await page.clock.runFor(32);
  }

  await expect(page.locator('[role="presentation"][data-open][style*="opacity: 0"]')).toHaveCount(0);
  await page.mouse.move(5, 700);
}

/** Moves the real pointer onto the center of an element, in one step. */
async function moveOnto(page: Page, target: Locator): Promise<void> {
  const box = (await target.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
}

// ---------------------------------------------------------------------------------------------------------------------
// PreviewCardRoot.test.tsx's `describe.for` trio (:47-499) on `preview-card-root`: one root with a contained trigger, one
// detached trigger, and two detached triggers. Base UI dispatches the events under fake timers; here the real pointer
// and keyboard drive the trigger, and a title that needs frozen time pauses the page's clock and runs it forward (ADR 06
// §2). The `actionsRef` titles run in jsdom only (`describe.skipIf(!isJSDOM)`), so at L2 here too.
// ---------------------------------------------------------------------------------------------------------------------

const trio = [
  { name: 'contained triggers', case: 'contained' },
  { name: 'detached triggers', case: 'detached' },
  { name: 'multiple detached triggers', case: 'multiple' },
] as const;

for (const variant of trio) {
  test.describe(`when using ${variant.name}`, () => {
    const open = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) =>
      gotoPopupFixture(page, mode, 'preview-card-root', { case: variant.case, ...options });
    const trigger = (page: Page) => page.locator('[data-testid="trigger"]');
    const popup = (page: Page) => page.locator('[data-testid="popup"]');
    const positioner = (page: Page) => page.locator('[data-testid="positioner"]');

    test.describe('uncontrolled open', () => {
      test('should open when the trigger is hovered', async ({ page, mode }) => {
        await open(page, mode);
        await freezeClock(page);

        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);

        await expect(popup(page)).toHaveText('Content');
      });

      test('should close when the trigger is unhovered', async ({ page, mode }) => {
        const fixture = await open(page, mode);
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        await moveAway(page);
        await page.clock.runFor(CLOSE_DELAY);

        await expect(popup(page)).toHaveCount(0);
        await expect(fixture).toHaveAttribute('data-open-changes', 'true:trigger-hover false:trigger-hover');
      });

      // Base UI returns early in a browser, where its synthetic focus is not `:focus-visible`; Playwright's focus() is.
      test('should open when the trigger is focused', async ({ page, mode }) => {
        await open(page, mode);
        await freezeClock(page);

        await trigger(page).focus();
        await page.clock.runFor(OPEN_DELAY);

        await expect(popup(page)).toHaveText('Content');
      });

      test('should close when the trigger is blurred', async ({ page, mode }) => {
        await open(page, mode);
        await freezeClock(page);
        await trigger(page).focus();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        await trigger(page).blur();
        await page.clock.runFor(CLOSE_DELAY);

        await expect(popup(page)).toHaveCount(0);
      });
    });

    test.describe('prop: onOpenChange', () => {
      test('should call onOpenChange when the open state changes', async ({ page, mode }) => {
        const fixture = await open(page, mode, { open: false });
        await expect(popup(page)).toHaveCount(0);
        await freezeClock(page);

        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toHaveText('Content');

        await moveAway(page);
        await page.clock.runFor(CLOSE_DELAY);

        await expect(popup(page)).toHaveCount(0);
        expect(await openChanges(fixture)).toEqual(['true:trigger-hover', 'false:trigger-hover']);
      });

      // With two detached triggers and no trigger id the card has no anchor and stays unpositioned (FLT-STA-09), so the
      // pointer crosses the positioner wherever it is; Base UI's title checks only that the content is still rendered.
      test('does not close after hovering out of a popup opened externally', async ({ page, mode }) => {
        const fixture = await open(page, mode, { open: false });
        await pressControl(fixture.locator('[data-set-open="true"]'));
        await expect(popup(page)).toHaveText('Content');
        await freezeClock(page);

        await moveOnto(page, positioner(page));
        await page.mouse.move(5, 700);
        await page.clock.runFor(CLOSE_DELAY);
        await settle(fixture);

        await expect(popup(page)).toHaveText('Content');
      });

      test('closes after hovering out of a popup opened by its trigger', async ({ page, mode }) => {
        await open(page, mode, { open: false });
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();

        // From the trigger into the card below it, then out of the card.
        await page.clock.runFor(32);
        await moveOnto(page, positioner(page));
        await moveAway(page);
        await page.clock.runFor(CLOSE_DELAY);

        await expect(popup(page)).toHaveCount(0);
      });

      test('does not call onOpenChange again when the trigger is re-hovered while open', async ({ page, mode }) => {
        const fixture = await open(page, mode, { open: false });
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toBeVisible();
        expect(await openChanges(fixture)).toEqual(['true:trigger-hover']);

        // Leave and re-enter before the close delay runs out: the card stays open.
        const box = (await trigger(page).boundingBox())!;
        await page.mouse.move(box.x + box.width / 2, box.y - 20);
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await page.clock.runFor(OPEN_DELAY);
        await settle(fixture);

        await expect(popup(page)).toBeVisible();
        expect(await openChanges(fixture)).toEqual(['true:trigger-hover']);
      });
    });

    test.describe('prop: defaultOpen', () => {
      test('closes when the trigger is unhovered after opening by default', async ({ page, mode }) => {
        await open(page, mode, { defaultOpen: true });
        await expect(popup(page)).toHaveText('Content');
        await freezeClock(page);

        await trigger(page).hover();
        await moveAway(page);
        await page.clock.runFor(CLOSE_DELAY);

        await expect(popup(page)).toHaveCount(0);
      });

      test('does not close after hovering out of a popup opened without trigger hover', async ({ page, mode }) => {
        const fixture = await open(page, mode, { defaultOpen: true });
        await expect(popup(page)).toHaveText('Content');
        await freezeClock(page);

        await moveOnto(page, positioner(page));
        await moveAway(page);
        await page.clock.runFor(CLOSE_DELAY);
        await settle(fixture);

        await expect(popup(page)).toHaveText('Content');
      });
    });

    test.describe('prop: delay', () => {
      test('should open after delay with rest type by default', async ({ page, mode }) => {
        const fixture = await open(page, mode, { delay: 100 });
        await freezeClock(page);

        await trigger(page).hover();
        await settle(fixture);
        await expect(popup(page)).toHaveCount(0);

        await page.clock.runFor(100);

        await expect(popup(page)).toHaveText('Content');
      });
    });

    test.describe('prop: closeDelay', () => {
      test('should close after delay', async ({ page, mode }) => {
        const fixture = await open(page, mode, { closeDelay: 100 });
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(OPEN_DELAY);
        await expect(popup(page)).toHaveText('Content');

        await moveAway(page);
        await settle(fixture);
        await expect(popup(page)).toHaveText('Content');

        await page.clock.runFor(100);

        await expect(popup(page)).toHaveCount(0);
      });
    });

    test.describe('dismissal', () => {
      test('reopens on hover after Escape closes it', async ({ page, mode }) => {
        const fixture = await open(page, mode, { delay: 100 });
        await freezeClock(page);
        await trigger(page).hover();
        await page.clock.runFor(100);
        await expect(popup(page)).toHaveText('Content');

        await page.keyboard.press('Escape');
        await page.clock.runFor(32);
        await expect(popup(page)).toHaveCount(0);
        await settle(fixture);

        // Re-enter with the real pointer only.
        await moveAway(page);
        await trigger(page).hover();
        await page.clock.runFor(100);

        await expect(popup(page)).toHaveText('Content');
      });
    });
  });
}

// ---------------------------------------------------------------------------------------------------------------------
// PreviewCardRoot.test.tsx's nested cards (:501-787) on `preview-card-nested`: the inner Root's session names the outer
// one (PRV-STR-10, the tree join), so the outer card's dismissal and hover leave see the inner card as inside.
// ---------------------------------------------------------------------------------------------------------------------

test.describe('nested preview card interactions', () => {
  const byTestId = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`);
  const nested = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) => gotoPopupFixture(page, mode, 'preview-card-nested', options);

  test('keeps the parent preview card open when clicking nested trigger', async ({ page, mode }) => {
    const fixture = await nested(page, mode, { parentOpen: true });
    await expect(byTestId(page, 'parent-popup')).toBeVisible();

    await byTestId(page, 'child-trigger').click();
    await settle(fixture);

    await expect(byTestId(page, 'parent-popup')).toBeVisible();
  });

  // Base UI's synthetic `pointerdown` in the child popup and `click` outside land in one tick, inside the 0 ms window of
  // `markPressStartedInsideReactTree`; a real drag outlasts it (ADR 04 Amendments 2026-10-29, gap 15's withdrawal), so the
  // two events are dispatched in one task, a commented exception to the real-input rule, as Popover's title is.
  test('keeps the parent preview card open when press starts in nested popup and ends outside', async ({ page, mode }) => {
    const fixture = await nested(page, mode, { parentOpen: true, childOpen: true });
    await expect(byTestId(page, 'parent-popup')).toBeVisible();
    await expect(byTestId(page, 'child-popup')).toBeVisible();

    await page.evaluate(() => {
      const child = document.querySelector('[data-testid="child-popup"]')!;
      const outside = document.querySelector<HTMLElement>('[data-testid="outside"]')!;
      child.dispatchEvent(new PointerEvent('pointerdown', { pointerType: 'mouse', button: 0, bubbles: true, composed: true }));
      outside.dispatchEvent(new MouseEvent('click', { button: 0, detail: 1, bubbles: true, composed: true }));
    });
    await settle(fixture);

    await expect(byTestId(page, 'parent-popup')).toBeVisible();
    await expect(byTestId(page, 'child-popup')).toBeVisible();
  });

  test('keeps the parent preview card open when hovering nested trigger', async ({ page, mode }) => {
    const fixture = await nested(page, mode, { parentOpen: true });
    await expect(byTestId(page, 'parent-popup')).toBeVisible();

    await byTestId(page, 'child-trigger').hover();
    await settle(fixture);

    await expect(byTestId(page, 'parent-popup')).toBeVisible();
  });

  test.describe('race condition between close timers and hover-open logic', () => {
    test('keeps the parent open and re-opens the child when re-entering after partial close', async ({ page, mode }) => {
      const fixture = await nested(page, mode, { parentOpen: true });
      await expect(byTestId(page, 'parent-popup')).toBeVisible();
      await freezeClock(page);

      await byTestId(page, 'child-trigger').hover();
      await page.clock.runFor(OPEN_DELAY);
      await expect(byTestId(page, 'child-popup')).toBeVisible();

      // Out of both cards, then back into the parent's own content before its close delay runs out.
      await moveAway(page);
      await page.clock.runFor(CLOSE_DELAY / 2);
      await moveOnto(page, page.getByText('Parent content'));
      await page.clock.runFor(CLOSE_DELAY);
      await settle(fixture);
      // The child's exit ends on the next frame after its commit, which arrives a round trip later on Server.
      await page.clock.resume();

      await expect(byTestId(page, 'parent-popup')).toBeVisible();
      await expect(byTestId(page, 'child-popup')).toHaveCount(0);

      await freezeClock(page);
      await byTestId(page, 'child-trigger').hover();
      await page.clock.runFor(OPEN_DELAY);
      await expect(byTestId(page, 'child-popup')).toBeVisible();

      // From the child trigger into the child card, which lies to its right.
      await page.clock.runFor(32);
      await moveOnto(page, byTestId(page, 'child-positioner'));
      await page.clock.runFor(CLOSE_DELAY);
      await settle(fixture);

      await expect(byTestId(page, 'parent-popup')).toBeVisible();
      await expect(byTestId(page, 'child-popup')).toBeVisible();
    });
  });

  test.describe('synchronized closing', () => {
    test('parent popup closes as soon as the child popup closes', async ({ page, mode }) => {
      test.skip(true, 'open to design point 11.6 of M4 track 7 (PRV-DIS-08, FLT-PTR-24/25): the floating hover handle reads its tree parent as the enclosing session, where Base UI reads the card\'s own node');
      await nested(page, mode);
      await freezeClock(page);

      await byTestId(page, 'parent-trigger').hover();
      await page.clock.runFor(OPEN_DELAY);
      await expect(byTestId(page, 'parent-popup')).toBeVisible();

      await page.clock.runFor(32);
      await moveOnto(page, byTestId(page, 'parent-positioner'));
      await byTestId(page, 'child-trigger').hover();
      await page.clock.runFor(OPEN_DELAY);
      await expect(byTestId(page, 'child-popup')).toBeVisible();

      await page.clock.runFor(32);
      await moveOnto(page, byTestId(page, 'child-positioner'));
      await moveAway(page);
      await page.clock.runFor(CLOSE_DELAY + 10);
      // The parent's cascade close (FLT-PTR-25) is requested on a zero timer after the child's, and each exit ends on a
      // frame after its commit, a round trip later on Server.
      await page.clock.resume();

      await expect(byTestId(page, 'child-popup')).toHaveCount(0);
      await expect(byTestId(page, 'parent-popup')).toHaveCount(0);
    });
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// PreviewCardRoot.detached-triggers.test.tsx's browser-only titles (:230-830) on `preview-card-detached`. The close
// transition of Base UI's 50 ms keyframe runs 1000 ms here, and the titles wait for its start on animation frames
// (`waitForEnding`), so a real pointer move lands inside it on both modes; the titles' 200 ms windows are kept.
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<PreviewCard.Root /> multiple triggers', () => {
  const detached = (page: Page, mode: RenderMode, options: PopupFixtureOptions) => gotoPopupFixture(page, mode, 'preview-card-detached', options);
  const link = (page: Page, name: string) => page.getByRole('link', { name, exact: true });
  const popup = (page: Page) => page.locator('[data-testid="popup"]');
  const content = (page: Page) => page.locator('[data-testid="content"]');
  // Polled on animation frames: Playwright's expect polls back off to a second, longer than the window it watches.
  const waitForEnding = (page: Page) =>
    page.waitForFunction(() => document.querySelector('[data-testid="popup"]')?.hasAttribute('data-ending-style') === true, undefined, { polling: 'raf' });

  test.describe('multiple triggers within Root', () => {
    test('should open the preview card immediately when hovering another trigger', async ({ page, mode }) => {
      await detached(page, mode, { case: 'multiple-root' });

      await link(page, 'Trigger 1').hover();
      await expect(popup(page)).toBeVisible();
      await expect(popup(page)).toHaveText('Content: 1');

      await link(page, 'Trigger 2').hover();
      await expect(popup(page)).toBeVisible();
      await expect(popup(page)).toHaveText('Content: 2');
    });
  });

  test.describe('multiple detached triggers', () => {
    test('should reposition to a different trigger when reopened with keepMounted=true', async ({ page, mode }) => {
      await detached(page, mode, { case: 'keep-mounted' });
      const positioner = page.locator('[data-testid="positioner"]');
      const left = (locator: Locator) => locator.evaluate((element) => element.getBoundingClientRect().left);

      await link(page, 'Trigger 1').hover();
      await expect(page.getByText('Content')).toBeVisible();
      const trigger1Left = await left(link(page, 'Trigger 1'));
      await expect.poll(async () => Math.abs((await left(positioner)) - trigger1Left)).toBeLessThanOrEqual(1);

      await moveAway(page);
      await expect(positioner).toHaveAttribute('hidden', '');

      await link(page, 'Trigger 2').hover();
      await expect(page.getByText('Content')).toBeVisible();
      const trigger2Left = await left(link(page, 'Trigger 2'));
      await expect.poll(async () => Math.abs((await left(positioner)) - trigger2Left)).toBeLessThanOrEqual(1);
    });

    test('opens immediately when entering trigger B during trigger A close transition', async ({ page, mode }) => {
      await detached(page, mode, { case: 'close-transition', closeMs: 1000, animations: 1 });

      await link(page, 'Trigger 1').hover();
      await expect(content(page)).toHaveText('1');

      await moveAway(page);
      await waitForEnding(page);

      await link(page, 'Trigger 2').hover();

      await expect(content(page)).toHaveText('2', { timeout: 200 });
    });

    test('still respects trigger B open delay after trigger A close transition finishes', async ({ page, mode }) => {
      await detached(page, mode, { case: 'close-transition', closeMs: 1000, animations: 1 });

      await link(page, 'Trigger 1').hover();
      await expect(content(page)).toHaveText('1');

      await moveAway(page);
      await waitForEnding(page);

      // Once the close transition is done, this behaves like a normal delayed open.
      await expect(popup(page)).not.toHaveAttribute('data-ending-style');
      await expect(popup(page)).toHaveAttribute('data-closed', '');

      await link(page, 'Trigger 2').hover();
      const hovered = Date.now();

      // Must not open immediately once the close transition has finished.
      await expect(popup(page)).toHaveAttribute('data-open', '');
      expect(Date.now() - hovered).toBeGreaterThanOrEqual(200);
    });

    test('reopens immediately when re-hovering trigger A during its close transition', async ({ page, mode }) => {
      await detached(page, mode, { case: 'close-transition', single: true, trigger1Delay: OPEN_DELAY, closeMs: 1000, animations: 1 });

      await link(page, 'Trigger 1').hover();
      await expect(content(page)).toHaveText('1');

      await moveAway(page);
      await waitForEnding(page);

      await link(page, 'Trigger 1').hover();

      await expect(popup(page)).toHaveAttribute('data-open', '', { timeout: 200 });
      await expect(popup(page)).not.toHaveAttribute('data-closed');
      await expect(content(page)).toHaveText('1');
    });

    test('respects open delay on later same-trigger hovers after close lifecycle finishes', async ({ page, mode }) => {
      await detached(page, mode, { case: 'close-transition', single: true, trigger1Delay: OPEN_DELAY, closeMs: 1000, animations: 1 });

      // First cycle: a close and an immediate re-hover during the close lifecycle reopen.
      await link(page, 'Trigger 1').hover();
      await expect(content(page)).toHaveText('1');
      await moveAway(page);
      await waitForEnding(page);
      await link(page, 'Trigger 1').hover();
      await expect(popup(page)).toHaveAttribute('data-open', '', { timeout: 200 });
      await expect(popup(page)).not.toHaveAttribute('data-closed');

      // Second cycle: once the close lifecycle has fully finished, a fresh hover honors the open delay.
      await moveAway(page);
      await expect(popup(page)).toHaveAttribute('data-closed', '');
      await expect(popup(page)).not.toHaveAttribute('data-ending-style');

      await link(page, 'Trigger 1').hover();
      const hovered = Date.now();

      await expect(popup(page)).toHaveAttribute('data-open', '');
      expect(Date.now() - hovered).toBeGreaterThanOrEqual(200);
    });
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// PreviewCardPositioner.test.tsx's "multiline inline trigger" titles (:123-800) on `preview-card-inline` (PRV-POS-03,
// PRV-PTR-12..14, PRV-STA-13, PSH-POS-03). As in Base UI, the triggers carry `pointer-events: none` and the titles dispatch
// `mouseenter` and `mousemove` with client coordinates, and two titles replace a trigger's client rects (`mockClientRects`).
// ---------------------------------------------------------------------------------------------------------------------

interface RectLike {
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
}

function expectWithin(actual: number, expected: number, tolerance = 2): void {
  expect(Math.abs(actual - expected)).toBeLessThanOrEqual(tolerance);
}

/** Base UI's `hoverAt`: a `mouseenter` and a `mousemove` at a client point. */
async function hoverAt(target: Locator, clientX: number, clientY: number): Promise<void> {
  await target.evaluate(
    (element, [x, y]) => {
      element.dispatchEvent(new MouseEvent('mouseenter', { clientX: x, clientY: y }));
      element.dispatchEvent(new MouseEvent('mousemove', { clientX: x, clientY: y, bubbles: true }));
    },
    [clientX, clientY] as const,
  );
}

async function dispatchMouse(target: Locator, type: 'mouseenter' | 'mousemove' | 'mouseleave', clientX = 0, clientY = 0): Promise<void> {
  await target.evaluate(
    (element, [eventType, x, y]) => {
      element.dispatchEvent(new MouseEvent(eventType, { clientX: x, clientY: y, bubbles: eventType === 'mousemove' }));
    },
    [type, clientX, clientY] as const,
  );
}

function clientRects(target: Locator): Promise<RectLike[]> {
  return target.evaluate((element) =>
    Array.from(element.getClientRects()).map((rect) => ({
      left: rect.left,
      top: rect.top,
      right: rect.right,
      bottom: rect.bottom,
      width: rect.width,
      height: rect.height,
    })),
  );
}

function rectOf(target: Locator): Promise<RectLike> {
  return target.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom, width: rect.width, height: rect.height };
  });
}

/** Base UI's `mockClientRects`: the element's client rects and bounding rect, replaced on the instance. */
async function mockClientRects(target: Locator, rects: RectLike[]): Promise<void> {
  await target.evaluate((element, list) => {
    const left = Math.min(...list.map((rect) => rect.left));
    const top = Math.min(...list.map((rect) => rect.top));
    const right = Math.max(...list.map((rect) => rect.right));
    const bottom = Math.max(...list.map((rect) => rect.bottom));
    const boundingRect = DOMRect.fromRect({ x: left, y: top, width: right - left, height: bottom - top });
    Object.defineProperty(element, 'getClientRects', {
      configurable: true,
      value: () => list.map((rect) => DOMRect.fromRect({ x: rect.left, y: rect.top, width: rect.width, height: rect.height })),
    });
    Object.defineProperty(element, 'getBoundingClientRect', { configurable: true, value: () => boundingRect });
  }, rects);
}

const center = (rect: RectLike) => [rect.left + rect.width / 2, rect.top + rect.height / 2] as const;

test.describe('<PreviewCard.Positioner /> multiline inline trigger', () => {
  const inline = (page: Page, mode: RenderMode, options: PopupFixtureOptions = {}) => gotoPopupFixture(page, mode, 'preview-card-inline', options);
  const trigger = (page: Page) => page.locator('[data-testid="trigger"]');
  const positioner = (page: Page) => page.locator('[data-testid="positioner"]');
  const positionerY = async (page: Page) => (await rectOf(positioner(page))).top;

  test('positions the popup relative to the hovered line of a multiline trigger', async ({ page, mode }) => {
    await inline(page, mode);
    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(2);

    // Enter over the second line so opening on hover uses the correct inline rect.
    const second = rects[1]!;
    await hoverAt(trigger(page), ...center(second));

    await expect(positioner(page)).toBeVisible();
    // Positioned against the second line, not the first line or the bounding rect.
    await expect.poll(async () => Math.abs((await positionerY(page)) - (second.bottom + 5))).toBeLessThanOrEqual(2);
    const { left } = await rectOf(positioner(page));
    expect(left).toBeGreaterThanOrEqual(second.left - 10);
    expect(left).toBeLessThanOrEqual(second.right + 10);
  });

  test.describe('with delayed opening', () => {
    test('uses the latest hovered line', async ({ page, mode }) => {
      const fixture = await inline(page, mode, { delay: 100 });
      const rects = await clientRects(trigger(page));
      expect(rects.length).toBeGreaterThan(2);
      const [first, second] = [rects[0]!, rects[1]!];
      await freezeClock(page);

      await dispatchMouse(trigger(page), 'mouseenter', ...center(first));
      await dispatchMouse(trigger(page), 'mousemove', ...center(first));
      await dispatchMouse(trigger(page), 'mousemove', ...center(second));
      await settle(fixture);
      await expect(positioner(page)).toHaveCount(0);

      await page.clock.runFor(100);
      await page.clock.runFor(16);

      await expect(positioner(page)).toBeVisible();
      await page.clock.runFor(32);
      await expect.poll(async () => Math.abs((await positionerY(page)) - (second.bottom + 5))).toBeLessThanOrEqual(2);
    });
  });

  test('keeps the popup aligned after page scroll', async ({ page, mode }) => {
    await inline(page, mode, { scroll: true });
    await page.evaluate(() => window.scrollTo(0, 1000));
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(1000);

    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(1);
    const second = rects[1]!;
    await hoverAt(trigger(page), ...center(second));

    await expect(positioner(page)).toBeVisible();
    await expect.poll(async () => Math.abs((await positionerY(page)) - (second.bottom + 5))).toBeLessThanOrEqual(2);
    const { left } = await rectOf(positioner(page));
    expect(left).toBeGreaterThanOrEqual(second.left - 10);
    expect(left).toBeLessThanOrEqual(second.right + 10);
  });

  // Base UI counts the passes through a `sideOffset` function; the port's offset functions are evaluated by the module,
  // so the title waits two frames after the resize, which runs the module's pass (`autoUpdate`), and asserts the line.
  test('stays anchored to the opened line while already open', async ({ page, mode }) => {
    await inline(page, mode);
    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(2);
    const [first, second] = [rects[0]!, rects[1]!];

    await hoverAt(trigger(page), ...center(first));
    await expect.poll(async () => Math.abs((await positionerY(page)) - (first.bottom + 5))).toBeLessThanOrEqual(2);

    await dispatchMouse(trigger(page), 'mouseleave');
    await hoverAt(trigger(page), ...center(second));
    await page.evaluate(async () => {
      window.dispatchEvent(new Event('resize'));
      await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    });

    expectWithin(await positionerY(page), first.bottom + 5);
  });

  test('re-anchors to a newly entered line while reopening during close transition', async ({ page, mode }) => {
    test.skip(true, 'open to design point 11.5 of M4 track 7 (PRV-ANI-05, PRV-STA-13): the positioning module runs no pass when the card reopens while mounted');
    await inline(page, mode, { closeDelay: 0, animated: true, animations: 1 });
    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(2);
    const [first, second] = [rects[0]!, rects[1]!];

    await hoverAt(trigger(page), ...center(first));
    await expect.poll(async () => Math.abs((await positionerY(page)) - (first.bottom + 5))).toBeLessThanOrEqual(2);

    await dispatchMouse(trigger(page), 'mouseleave');
    await expect(page.locator('[data-testid="popup"]')).toHaveAttribute('data-ending-style', '');

    await dispatchMouse(trigger(page), 'mousemove', ...center(second));
    await dispatchMouse(trigger(page), 'mouseenter', ...center(second));

    await expect.poll(async () => Math.abs((await positionerY(page)) - (second.bottom + 5))).toBeLessThanOrEqual(2);
  });

  test('positions the popup relative to the side-aligned rect when open is controlled', async ({ page, mode }) => {
    await inline(page, mode, { case: 'controlled-open' });
    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(1);
    const target = rects[rects.length - 1]!;

    await expect(positioner(page)).toBeVisible();
    await expect.poll(async () => Math.abs((await positionerY(page)) - (target.bottom + 5))).toBeLessThanOrEqual(2);
    const { left } = await rectOf(positioner(page));
    expect(left).toBeGreaterThanOrEqual(target.left - 10);
    expect(left).toBeLessThanOrEqual(target.right + 10);
  });

  test('clears hovered-line coords after close before controlled reopen of the same trigger', async ({ page, mode }) => {
    test.skip(true, 'open to design point 11.2 of M4 track 7 (PRV-STA-13(d), PRV-ANI-04): the session record is not cleared at the unmount yet');
    const fixture = await inline(page, mode, { case: 'controlled-reopen' });
    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(2);
    const second = rects[1]!;
    await hoverAt(trigger(page), ...center(second));
    await expect.poll(async () => Math.abs((await positionerY(page)) - (second.bottom + 5))).toBeLessThanOrEqual(2);

    await pressControl(fixture.locator('[data-close-button]'));
    await expect(positioner(page)).toHaveAttribute('hidden', '');

    await pressControl(fixture.locator('[data-open-button]'));

    const target = rects[rects.length - 1]!;
    await expect(positioner(page)).toBeVisible();
    await expect.poll(async () => Math.abs((await positionerY(page)) - (target.bottom + 5))).toBeLessThanOrEqual(2);
  });

  test('ignores stale hovered coords when a controlled trigger switch reuses the popup', async ({ page, mode }) => {
    const fixture = await inline(page, mode, { case: 'switch' });
    await mockClientRects(page.locator('[data-testid="trigger-1"]'), [
      { left: 180, top: 0, right: 220, bottom: 10, width: 40, height: 10 },
      { left: 100, top: 20, right: 160, bottom: 30, width: 60, height: 10 },
    ]);
    await mockClientRects(page.locator('[data-testid="trigger-2"]'), [
      { left: 180, top: 100, right: 220, bottom: 110, width: 40, height: 10 },
      { left: 100, top: 120, right: 160, bottom: 130, width: 60, height: 10 },
    ]);

    await hoverAt(page.locator('[data-testid="trigger-1"]'), 200, 5);
    await expect(positioner(page)).toBeVisible();

    await pressControl(fixture.locator('[data-switch-button]'));

    await expect.poll(async () => Math.abs((await positionerY(page)) - 135)).toBeLessThanOrEqual(2);
    const { left } = await rectOf(positioner(page));
    expect(left).toBeGreaterThanOrEqual(90);
    expect(left).toBeLessThanOrEqual(170);
  });

  test('uses the hovered line with a custom anchor in a clipped keepMounted portal', async ({ page, mode }) => {
    await inline(page, mode, { case: 'custom-anchor' });
    await page.addStyleTag({ content: 'body { margin: 0 !important; }' });
    await mockClientRects(trigger(page), [
      { left: 180, top: 80, right: 220, bottom: 90, width: 40, height: 10 },
      { left: 0, top: 100, right: 60, bottom: 110, width: 60, height: 10 },
    ]);

    await hoverAt(trigger(page), 30, 105);

    await expect(positioner(page)).toBeVisible();
    expect(await page.locator('[data-testid="portal-container"]').evaluate((container) => container.querySelector('[data-testid="positioner"]') !== null)).toBe(true);
    await expect(positioner(page)).toHaveAttribute('data-side', 'top');
    await expect.poll(async () => Math.abs((await positionerY(page)) - 55)).toBeLessThanOrEqual(2);
  });

  test('positions the popup relative to the side-aligned rect when opened via focus', async ({ page, mode }) => {
    await inline(page, mode, { case: 'focus' });
    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(1);
    const target = rects[0]!;

    await trigger(page).focus();

    await expect(positioner(page)).toBeVisible();
    await expect.poll(async () => Math.abs((await positionerY(page)) - (target.top - 40 - 5))).toBeLessThanOrEqual(2);
    const { left } = await rectOf(positioner(page));
    expect(left).toBeGreaterThanOrEqual(target.left - 10);
    expect(left).toBeLessThanOrEqual(target.right + 10);
  });

  test('clears hovered-line coords when opened via focus', async ({ page, mode }) => {
    const fixture = await inline(page, mode, { case: 'focus-controlled' });
    const rects = await clientRects(trigger(page));
    expect(rects.length).toBeGreaterThan(2);
    const second = rects[1]!;
    await hoverAt(trigger(page), ...center(second));
    await settle(fixture);

    const target = rects[0]!;
    await trigger(page).focus();

    await expect(positioner(page)).toBeVisible();
    await expect.poll(async () => Math.abs((await positionerY(page)) - (target.top - 40 - 5))).toBeLessThanOrEqual(2);
  });
});

// ---------------------------------------------------------------------------------------------------------------------
// The arrow's mirrored placement (PRV-DOM-17, PRV-STA-07): Base UI's PreviewCardArrow.test.tsx runs describeConformance
// only, so these are port titles on `preview-card-positioner`'s arrow cases.
// ---------------------------------------------------------------------------------------------------------------------

test.describe('<PreviewCard.Arrow />', () => {
  const arrow = (page: Page) => page.locator('[data-testid="arrow"]');

  test('port: the arrow is hidden from assistive technology and mirrors the resolved side', async ({ page, mode }) => {
    await gotoPositionerCase(page, mode, 'preview-card-positioner', 'arrow');

    await expect(arrow(page)).toHaveAttribute('aria-hidden', 'true');
    await expect(arrow(page)).toHaveAttribute('data-side', 'bottom');
    await expect(arrow(page)).toHaveAttribute('data-open', '');
  });

  test('port: the arrow is marked uncentered when it cannot point at the anchor', async ({ page, mode }) => {
    await gotoPositionerCase(page, mode, 'preview-card-positioner', 'arrow-uncentered');

    await expect(arrow(page)).toHaveAttribute('data-uncentered', '');
  });

  test('port: the arrow is not marked uncentered when it can point at the anchor', async ({ page, mode }) => {
    await gotoPositionerCase(page, mode, 'preview-card-positioner', 'arrow-centered');

    await expect(arrow(page)).toHaveAttribute('data-side', 'bottom');
    await expect(arrow(page)).not.toHaveAttribute('data-uncentered');
  });
});
