// L4 for docs/components/select.md (M5 track 9, issue 173), SelectPopup.test.tsx's aligned-placement titles and its two
// keyboard titles: Server then WebAssembly. Fixtures: tests/Blazix.Primitives.Fixtures.Client/Select/SelectAligned*Fixture.razor.
//
// Base UI's `aligned popup scroll resizing` describe (1137-1726) and the titles at 875-1135 stub the viewport and the
// elements' boxes in their ref callbacks (`Object.defineProperty` of `getBoundingClientRect`, `offsetWidth`, `scrollTop`,
// `scrollHeight`, `clientHeight`, `documentElement.clientHeight`). Here the select renders inline (no portal), closed, so its
// elements exist before the open; `stubAligned` installs the same stubs in the page, then the open control opens the root and
// the placement runs on them, as it does in Base UI. A scroll is the dispatched `scroll` event of `fireEvent.scroll` (the
// stubbed `scrollTop` cannot be moved by a real wheel); the window-resize close uses a real viewport resize.
import { type Page } from '@playwright/test';
import { expect, test } from './helpers';
import { gotoPopupFixture, pressControl, settle } from './suites/popupFixture';

const positionerOf = (page: Page) => page.getByTestId('positioner');
const triggerOf = (page: Page) => page.getByTestId('trigger');
const option = (page: Page, name: string) => page.getByRole('option', { name, exact: true });

/** An inline style property of the positioner. */
const positionerStyle = (page: Page, property: string) =>
  positionerOf(page).evaluate((element, name) => (element as HTMLElement).style.getPropertyValue(name), property);

/** A bounding-box edge of an element by test id. */
const edge = (page: Page, testId: string, name: 'left' | 'right') =>
  page.getByTestId(testId).evaluate((element, side) => element.getBoundingClientRect()[side], name);

const centerY = (page: Page, testId: string) =>
  page.getByTestId(testId).evaluate((element) => {
    const rect = element.getBoundingClientRect();
    return rect.top + rect.height / 2;
  });

// ---------------------------------------------------------------------------------------------------------------------
// Stubs (SelectPopup.test.tsx:1139-1175, 1177-1297)
// ---------------------------------------------------------------------------------------------------------------------

interface AlignedOptions {
  viewportHeight: number;
  triggerTop: number;
  positionerHeight: number;
  listScrollHeight: number;
  listClientHeight: number;
  /** Vertical distance from the positioner top to the selected item's text. */
  selectedTextTop?: number;
  popupMaxHeight?: string;
  /** `popup`: the popup is the scroll container (no List). */
  kase?: 'list' | 'popup';
  /** A stubbed `visualViewport.scale`. */
  pinchScale?: number;
}

/** Installs Base UI's `renderAligned` stubs on the closed, inline select. */
function stubAligned(options: AlignedOptions): void {
  const w = window as unknown as { alignedStubs: { listScrollTop: number; popupScrollTop: number } };
  w.alignedStubs = { listScrollTop: 0, popupScrollTop: 0 };
  const stubProperty = (target: object, property: string, value: unknown) =>
    Object.defineProperty(target, property, { value, configurable: true });
  const stubRect = (node: Element, left: number, top: number, width: number, height: number) => {
    stubProperty(node, 'getBoundingClientRect', () => ({
      x: left,
      y: top,
      left,
      top,
      width,
      height,
      right: left + width,
      bottom: top + height,
      toJSON: () => ({}),
    }));
    // Floating UI derives the element scale by comparing the rect against the offset box.
    stubProperty(node, 'offsetWidth', width);
    stubProperty(node, 'offsetHeight', height);
  };
  const byId = (id: string) => document.querySelector(`[data-testid="${id}"]`)!;
  const doc = document.documentElement;
  stubProperty(doc, 'clientHeight', options.viewportHeight);
  stubProperty(doc, 'clientWidth', 400);
  if (options.pinchScale !== undefined && window.visualViewport) {
    stubProperty(window.visualViewport, 'scale', options.pinchScale);
  }

  stubRect(byId('trigger'), 10, options.triggerTop, 80, 30);
  stubRect(byId('value'), 10, options.triggerTop, 60, 30);
  stubRect(byId('positioner'), 10, 0, 108, options.positionerHeight);
  const selectedText = byId('selected-text');
  if (options.selectedTextTop !== undefined) {
    stubRect(selectedText, 10, options.selectedTextTop, 80, 30);
  }

  const scroller = byId(options.kase === 'popup' ? 'popup' : 'list');
  const key = options.kase === 'popup' ? 'popupScrollTop' : 'listScrollTop';
  Object.defineProperty(scroller, 'scrollTop', {
    configurable: true,
    get: () => w.alignedStubs[key],
    set: (value: number) => {
      w.alignedStubs[key] = value;
    },
  });
  stubProperty(scroller, 'scrollHeight', options.listScrollHeight);
  stubProperty(scroller, 'clientHeight', options.listClientHeight);
}

/** Opens the aligned scroll fixture on stubbed geometry; resolves once the popup is placed (`data-side="none"`). */
async function renderAligned(page: Page, mode: 'server' | 'wasm', options: AlignedOptions) {
  const fixture = await gotoPopupFixture(page, mode, 'select-aligned-scroll', {
    case: options.kase ?? 'list',
    popupStyle: `max-height: ${options.popupMaxHeight ?? 'none'}`,
  });
  await page.evaluate(stubAligned, options);
  await pressControl(fixture.locator('[data-open]'));
  await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
  return { fixture, positioner: positionerOf(page), list: page.getByTestId('list'), popup: page.getByTestId('popup') };
}

type Scroller = 'list' | 'popup';

/** Sets the stubbed scroll offset of a scroller. */
function setScrollTop(page: Page, scroller: Scroller, value: number) {
  return page.evaluate(([name, top]) => {
    (window as unknown as { alignedStubs: Record<string, number> }).alignedStubs[`${name}ScrollTop`] = top as number;
  }, [scroller, value] as const);
}

function getScrollTop(page: Page, scroller: Scroller) {
  return page.evaluate((name) => (window as unknown as { alignedStubs: Record<string, number> }).alignedStubs[`${name}ScrollTop`], scroller);
}

/** `fireEvent.scroll(element)`: a `scroll` event, which does not bubble, dispatched on the element. */
function fireScroll(page: Page, testId: string) {
  return page.getByTestId(testId).evaluate((element) => element.dispatchEvent(new Event('scroll')));
}

// ---------------------------------------------------------------------------------------------------------------------
// The titles
// ---------------------------------------------------------------------------------------------------------------------

test.describe('Select.Popup', () => {
  // SEL-EDGE-12, SEL-KEY-03, SEL-KEY-14 (SelectPopup.test.tsx:43-88).
  test('keeps arrow keys inside the popup when the trigger lives in a toolbar', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-aligned-focus', { case: 'toolbar' });
    const trigger = triggerOf(page);
    await page.evaluate(() => {
      const w = window as unknown as { toolbarKeydowns: number };
      w.toolbarKeydowns = 0;
      document.querySelector('[data-testid="toolbar"]')!.addEventListener('keydown', () => {
        w.toolbarKeydowns += 1;
      });
    });
    await trigger.focus();

    // Opening with the keyboard moves focus onto the selected item inside the popup.
    await page.keyboard.press('Enter');
    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(option(page, 'a')).toBeFocused();

    // Enter isn't a composite key, so opening legitimately reaches the toolbar.
    await page.evaluate(() => {
      (window as unknown as { toolbarKeydowns: number }).toolbarKeydowns = 0;
    });

    // A horizontal toolbar navigates with ArrowRight. While the select popup owns focus that key belongs to the popup, so it
    // must not bubble out and move focus off the toolbar button.
    await page.keyboard.press('ArrowRight');
    await settle(fixture);

    expect(await page.evaluate(() => (window as unknown as { toolbarKeydowns: number }).toolbarKeydowns)).toBe(0);
    await expect(page.getByTestId('next')).not.toBeFocused();
  });

  // SEL-KEY-03, SEL-KEY-06 (SelectPopup.test.tsx:90-124).
  test('moves focus into the items when the boundary item is already highlighted', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-focus', { case: 'boundary' });
    await triggerOf(page).focus();
    await page.keyboard.press('Enter');

    const a = option(page, 'a');
    await expect(a).toBeVisible();
    await expect(a).toHaveAttribute('data-highlighted', '');
    await page.getByRole('button', { name: 'Select all' }).focus();
    await expect(a).toHaveAttribute('data-highlighted', '');

    await page.keyboard.press('ArrowDown');
    await expect(a).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(option(page, 'b')).toBeFocused();
  });

  // SEL-POS-07, SEL-POS-19 (SelectPopup.test.tsx:196-229). The popup's inline transform, scale and translate come from the
  // fixture's style attribute (Base UI's ref callback writes them before the placement).
  test('restores transform-related inline styles after measurement', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'transform' });
    await expect(page.getByTestId('popup')).toBeVisible();
    // `await new Promise(queueMicrotask)` in Base UI: one task after the placement.
    await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
    const styles = await page
      .getByTestId('popup')
      .evaluate((element) => ['transform', 'scale', 'translate'].map((name) => (element as HTMLElement).style.getPropertyValue(name)));
    expect(styles).toEqual(['translateX(10px)', '0.8', '1px 2px']);
  });

  // SEL-DOM-19, SEL-POS-03, SEL-POS-14, SEL-POS-18, SEL-STA-20 (SelectPopup.test.tsx:231-266).
  test('keeps alignItemWithTrigger active at browser zoom', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'zoom' });
    await page.evaluate(() => {
      document.documentElement.style.zoom = '0.9';
    });
    await triggerOf(page).click();
    await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
  });

  // SEL-DOM-12, SEL-POS-07 (SelectPopup.test.tsx:268-328).
  test('aligns the selected item with the trigger inline start in ltr', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'ltr' });
    await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
    await expect
      .poll(async () => Math.abs((await edge(page, 'value', 'left')) - (await edge(page, 'item-text', 'left'))))
      .toBeLessThan(1);
  });

  // (SelectPopup.test.tsx:330-402; no SEL row).
  test('aligns the selected item with the trigger inline start in ltr when popup padding shifts the item text', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'ltr-padding' });
    await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
    await expect
      .poll(async () => Math.abs((await edge(page, 'value', 'left')) - (await edge(page, 'item-text', 'left'))))
      .toBeLessThan(1);
  });

  // SEL-POS-07, SEL-POS-17 (SelectPopup.test.tsx:404-464).
  test('aligns the selected item with the trigger inline end in rtl', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'rtl' });
    await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
    await expect
      .poll(async () => Math.abs((await edge(page, 'value', 'right')) - (await edge(page, 'item-text', 'right'))))
      .toBeLessThan(1);
  });

  // SEL-POS-07 (SelectPopup.test.tsx:466-520).
  test('aligns the popup with the trigger inline end in rtl when no selected item text is available', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'rtl-no-text' });
    await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
    await expect
      .poll(async () => Math.abs((await edge(page, 'trigger', 'right')) - (await edge(page, 'positioner', 'right'))))
      .toBeLessThan(1);
  });

  // SEL-DOM-38, SEL-POS-07 (SelectPopup.test.tsx:522-612).
  test('keeps item text aligned with the trigger after reopening', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'reopen' });
    const trigger = triggerOf(page);

    async function expectItemAligned(testId: string) {
      await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
      await expect
        .poll(async () => Math.abs((await centerY(page, 'value')) - (await centerY(page, testId))))
        .toBeLessThan(1);
    }

    await trigger.click();
    await expectItemAligned('first-item-text');

    await page.keyboard.press('Escape');
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');

    await trigger.click();
    await expectItemAligned('first-item-text');

    await page.getByTestId('selected-item-text').click();
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');

    await trigger.click();
    await expectItemAligned('selected-item-text');
  });

  test.describe('rtl alignment', () => {
    // SEL-POS-07, SEL-POS-17 (SelectPopup.test.tsx:743-760).
    test('aligns the selected item with the trigger inline end on first open in the docs-style rtl layout', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'docs-rtl' });
      await page.getByRole('combobox').click();
      await expect(positionerOf(page)).toBeVisible();
      await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
      await expect
        .poll(async () => Math.abs((await edge(page, 'value', 'right')) - (await edge(page, 'item-text', 'right'))))
        .toBeLessThan(1);
    });

    // SEL-DOM-22, SEL-POS-17 (SelectPopup.test.tsx:762-787).
    test('seeds anchor width from the custom anchor on first open', async ({ page, mode }) => {
      await gotoPopupFixture(page, mode, 'select-aligned-layout', { case: 'docs-rtl-anchor' });
      await page.getByRole('combobox').click();
      await expect(positionerOf(page)).toBeVisible();
      await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
      await expect.poll(() => positionerStyle(page, '--anchor-width')).toBe('240px');
    });
  });

  // SEL-POS-11, SEL-POS-17, SEL-POS-18 (SelectPopup.test.tsx:790-873). Base UI opens at zoom 0.9, waits for the placement,
  // then stubs the positioner's box and the list's scroll metrics, sets the positioner's height and bottom, and scrolls.
  test('keeps scrolling the aligned list at browser zoom after the popup fills the available height', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-aligned-scroll', {
      wrapStyle: 'padding-top: 100px; padding-left: 10px',
      triggerStyle: 'width: 108px; height: 30px',
      popupStyle: 'max-height: 225.538px',
    });
    await page.evaluate(() => {
      document.documentElement.style.zoom = '0.9';
    });
    await pressControl(fixture.locator('[data-open]'));
    const positioner = positionerOf(page);
    await expect(positioner).toHaveAttribute('data-side', 'none');

    await page.evaluate(() => {
      const stubs = { listScrollTop: 0 };
      (window as unknown as { alignedStubs: typeof stubs }).alignedStubs = stubs;
      const positionerElement = document.querySelector('[data-testid="positioner"]') as HTMLElement;
      const list = document.querySelector('[data-testid="list"]') as HTMLElement;
      const rect = { x: 10, y: 0, left: 10, top: 0, width: 108, height: 202.984375, right: 118, bottom: 202.984375, toJSON: () => ({}) };
      Object.defineProperty(positionerElement, 'getBoundingClientRect', { value: () => rect, configurable: true });
      Object.defineProperty(list, 'scrollTop', {
        configurable: true,
        get: () => stubs.listScrollTop,
        set: (value: number) => {
          stubs.listScrollTop = value;
        },
      });
      Object.defineProperty(list, 'scrollHeight', { value: 598, configurable: true });
      Object.defineProperty(list, 'clientHeight', { value: 226, configurable: true });
      positionerElement.style.height = '225.538px';
      positionerElement.style.bottom = '0px';
      stubs.listScrollTop = 14;
      list.dispatchEvent(new Event('scroll'));
    });

    await expect.poll(() => positionerStyle(page, 'height')).toBe('226px');
    expect(await getScrollTop(page, 'list')).toBe(14);
  });

  // SEL-POS-07, SEL-POS-17 (SelectPopup.test.tsx:875-983).
  test('treats short popups as top-positioned when maxScrollTop is off by 1px', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-aligned-scroll', { case: 'short' });
    await page.evaluate(stubShort, {});
    await pressControl(fixture.locator('[data-open]'));
    const positioner = positionerOf(page);

    // Check the side and the inset styles together so they come from the same layout pass.
    await expect
      .poll(() =>
        positioner.evaluate((element) => ({
          side: element.getAttribute('data-side'),
          hasTop: (element as HTMLElement).style.top !== '',
          bottom: (element as HTMLElement).style.bottom,
        })),
      )
      .toEqual({ side: 'none', hasTop: true, bottom: '' });
  });

  // SEL-POS-04, SEL-POS-17 (SelectPopup.test.tsx:985-1087).
  test('keeps alignItemWithTrigger active when the aligned height is fractionally below minHeight', async ({ page, mode }) => {
    const fixture = await gotoPopupFixture(page, mode, 'select-aligned-scroll', { case: 'short', popupStyle: 'min-height: 65px' });
    await page.evaluate(stubShort, {});
    await pressControl(fixture.locator('[data-open]'));
    await expect(positionerOf(page)).toHaveAttribute('data-side', 'none');
  });

  // SEL-POS-17, SEL-POS-22 (SelectPopup.test.tsx:1089-1135).
  test('does not resize the positioner when the list scrolls before the popup is placed', async ({ page, mode }) => {
    await gotoPopupFixture(page, mode, 'select-aligned-scroll', { case: 'closed' });
    await page.evaluate(() => {
      const list = document.querySelector('[data-testid="list"]')!;
      Object.defineProperty(list, 'scrollTop', { value: 100, configurable: true });
      Object.defineProperty(list, 'scrollHeight', { value: 400, configurable: true });
      Object.defineProperty(list, 'clientHeight', { value: 200, configurable: true });
    });
    const geometry = () =>
      positionerOf(page).evaluate((element) => {
        const { height, top, bottom } = (element as HTMLElement).style;
        return { height, top, bottom };
      });
    const before = await geometry();

    // A closed popup has never been measured, so a stray scroll must neither rewrite the positioner geometry nor light up
    // the scroll arrows.
    await fireScroll(page, 'list');

    expect(await geometry()).toEqual(before);
    await expect(page.getByTestId('up-arrow')).not.toHaveAttribute('data-visible');
  });

  test.describe('aligned popup scroll resizing', () => {
    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1299-1312).
    test('anchors the popup to the viewport bottom when the selection sits near the list top', async ({ page, mode }) => {
      const { positioner } = await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
      });

      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');
      expect(await positionerStyle(page, 'top')).toBe('');
      await expect(positioner).toHaveAttribute('data-side', 'none');
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1314-1336).
    test('grows a bottom-anchored popup as the list is scrolled and re-pins it to the top', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');

      await setScrollTop(page, 'list', 100);
      await fireScroll(page, 'list');

      // The scrolled distance is converted into extra popup height, and the list snaps back so the newly revealed rows appear
      // by growing rather than by scrolling.
      await expect.poll(() => positionerStyle(page, 'height')).toBe('300px');
      expect(await getScrollTop(page, 'list')).toBe(0);
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1338-1358).
    test('consumes a sub-pixel overscroll into the popup height', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');

      await setScrollTop(page, 'list', 0.5);
      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe('200.5px');
      expect(await getScrollTop(page, 'list')).toBe(0);
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1360-1381).
    test('leaves the popup height alone when the list is already at the anchored edge', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');
      const heightBefore = await positionerStyle(page, 'height');

      await setScrollTop(page, 'list', 0);
      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe(heightBefore);
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1383-1405).
    test('grows a top-anchored popup upward and keeps the list pinned to the bottom', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 400,
        triggerTop: 200,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 300,
        selectedTextTop: 300,
      });
      await expect.poll(() => positionerStyle(page, 'top')).toBe('0px');
      expect(await positionerStyle(page, 'bottom')).toBe('');

      await setScrollTop(page, 'list', 0);
      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe('300px');
      expect(await getScrollTop(page, 'list')).toBe(100);
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1407-1437).
    test('stops growing a top-anchored popup once it fills the available height', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 400,
        triggerTop: 200,
        positionerHeight: 300,
        listScrollHeight: 400,
        listClientHeight: 300,
        selectedTextTop: 300,
      });
      await expect.poll(() => positionerStyle(page, 'top')).toBe('0px');

      await setScrollTop(page, 'list', 0);
      await fireScroll(page, 'list');

      // 300px of popup plus 100px of remaining scroll saturates the 380px of available height, so it clamps instead of
      // overshooting the viewport.
      await expect.poll(() => positionerStyle(page, 'height')).toBe('380px');

      // Once saturated, further scrolling must not keep resizing.
      await setScrollTop(page, 'list', 50);
      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe('380px');
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1439-1473).
    test('pins a top-anchored popup that already fills the viewport to the end of the list', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 400,
        triggerTop: 200,
        positionerHeight: 380,
        listScrollHeight: 400,
        listClientHeight: 300,
        selectedTextTop: 300,
      });

      // A popup at least as tall as the usable viewport is flush with its top edge.
      await expect.poll(() => positionerStyle(page, 'top')).toBe('0px');
      const heightBefore = await positionerStyle(page, 'height');

      await setScrollTop(page, 'list', 100);
      await fireScroll(page, 'list');

      await expect.poll(() => getScrollTop(page, 'list')).toBe(100);
      expect(await positionerStyle(page, 'height')).toBe(heightBefore);

      // Having reached its maximum, the popup stops taking over scrolling entirely: a later sub-pixel scroll is left to the
      // list instead of being snapped back to the edge.
      await setScrollTop(page, 'list', 99.5);
      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe(heightBefore);
      expect(await getScrollTop(page, 'list')).toBe(99.5);
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1475-1497).
    test('stops resizing once a max-height constrained popup is fully grown', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
        popupMaxHeight: '100px',
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');
      const heightBefore = await positionerStyle(page, 'height');

      await setScrollTop(page, 'list', 100);
      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe(heightBefore);
    });

    // SEL-DOM-20, SEL-POS-08, SEL-POS-11 (SelectPopup.test.tsx:1499-1521).
    test('treats a zero max-height as unconstrained rather than collapsing the popup', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
        popupMaxHeight: '0px',
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');

      await setScrollTop(page, 'list', 100);
      await fireScroll(page, 'list');

      // `max-height: 0` is treated as unset, matching the margin/min-height fallbacks, so the popup still grows by the scrolled
      // distance instead of clamping to nothing.
      await expect.poll(() => positionerStyle(page, 'height')).toBe('300px');
    });

    // SEL-DOM-20, SEL-DOM-29, SEL-POS-11 (SelectPopup.test.tsx:1523-1540).
    test('centers the transform origin when the positioner has no measurable height', async ({ page, mode }) => {
      const { popup } = await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 0,
        listScrollHeight: 400,
        listClientHeight: 200,
        selectedTextTop: 100,
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');

      expect(await popup.evaluate((element) => (element as HTMLElement).style.getPropertyValue('--transform-origin'))).toBe('50% 50%');
    });

    // SEL-DOM-20, SEL-POS-11 (SelectPopup.test.tsx:1542-1566).
    test('never applies a non-positive height when the viewport collapses below the margins', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');
      const heightBefore = await positionerStyle(page, 'height');

      // The viewport shrinks to less than the popup's own margins after placement.
      await page.evaluate(() => {
        Object.defineProperty(document.documentElement, 'clientHeight', { value: 20, configurable: true });
      });
      await setScrollTop(page, 'list', 100);
      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe(heightBefore);
    });

    // SEL-DOM-20, SEL-EDGE-14, SEL-POS-04, SEL-POS-11 (SelectPopup.test.tsx:1568-1586). A mocked `visualViewport.scale` of 1.5.
    test('ignores a pinch-zoomed visual viewport outside WebKit', async ({ page, mode }) => {
      const { positioner } = await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
        pinchScale: 1.5,
      });

      // Only WebKit mispositions pinch-zoomed popups, so other engines keep item alignment.
      await expect(positioner).toHaveAttribute('data-side', 'none');
    });

    // SEL-DOM-20, SEL-POS-11, SEL-POS-23 (SelectPopup.test.tsx:1588-1623).
    test('ignores scroll events bubbling to the popup while a Select.List owns scrolling', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');
      const heightBefore = await positionerStyle(page, 'height');

      // Give the popup its own (different) scroll geometry so handling it would be detectable.
      await page.getByTestId('popup').evaluate((popup) => {
        Object.defineProperty(popup, 'scrollTop', { value: 50, configurable: true });
        Object.defineProperty(popup, 'scrollHeight', { value: 400, configurable: true });
        Object.defineProperty(popup, 'clientHeight', { value: 200, configurable: true });
      });

      await setScrollTop(page, 'list', 100);
      // The list, not the popup, is the scroll container here. Handling the bubbled event on the popup as well would resize
      // using the wrong element's offsets.
      await fireScroll(page, 'popup');

      await expect.poll(() => positionerStyle(page, 'height')).toBe(heightBefore);

      await fireScroll(page, 'list');

      await expect.poll(() => positionerStyle(page, 'height')).toBe('300px');
    });

    // SEL-DOM-20, SEL-DOM-27, SEL-POS-11, SEL-POS-23 (SelectPopup.test.tsx:1625-1703).
    test('grows the popup when it is itself the scroll container', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
        kase: 'popup',
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');

      await setScrollTop(page, 'popup', 100);
      await fireScroll(page, 'popup');

      await expect.poll(() => positionerStyle(page, 'height')).toBe('300px');
      expect(await getScrollTop(page, 'popup')).toBe(0);
    });

    // SEL-DIS-06, SEL-DOM-20, SEL-POS-11, SEL-POS-16 (SelectPopup.test.tsx:1705-1725). Base UI fires `new UIEvent('resize')`;
    // here the viewport is resized, which makes the browser fire the window's `resize`.
    test('closes the aligned popup when the window is resized', async ({ page, mode }) => {
      await renderAligned(page, mode, {
        viewportHeight: 720,
        triggerTop: 300,
        positionerHeight: 200,
        listScrollHeight: 400,
        listClientHeight: 200,
      });
      await expect.poll(() => positionerStyle(page, 'bottom')).toBe('0px');
      await expect(triggerOf(page)).toHaveAttribute('aria-expanded', 'true');

      // The aligned geometry is measured once, so a resize invalidates it entirely.
      const size = page.viewportSize()!;
      await page.setViewportSize({ width: size.width - 40, height: size.height - 40 });

      await expect(triggerOf(page)).toHaveAttribute('aria-expanded', 'false');
      await expect(page.getByRole('listbox')).toHaveCount(0);
    });
  });
});

/** The stubs of the two `short popup` titles (SelectPopup.test.tsx:906-966, 1016-1077). */
function stubShort(): void {
  const stubProperty = (target: object, property: string, value: unknown) =>
    Object.defineProperty(target, property, { value, configurable: true });
  const createRect = (left: number, top: number, width: number, height: number) => ({
    x: left,
    y: top,
    left,
    top,
    width,
    height,
    right: left + width,
    bottom: top + height,
    toJSON: () => ({}),
  });
  const doc = document.documentElement;
  stubProperty(doc, 'clientHeight', 646);
  stubProperty(doc, 'clientWidth', 300);
  const trigger = document.querySelector('[data-testid="trigger"]')!;
  stubProperty(trigger, 'offsetWidth', 80);
  stubProperty(trigger, 'offsetHeight', 30);
  stubProperty(trigger, 'getBoundingClientRect', () => createRect(10, 170.3793487548828, 80, 30));
  const positioner = document.querySelector('[data-testid="positioner"]')!;
  stubProperty(positioner, 'getBoundingClientRect', () => createRect(10, 0, 108, 63.96739196777344));
  const popup = document.querySelector('[data-testid="popup"]')!;
  stubProperty(popup, 'scrollHeight', 64);
  stubProperty(popup, 'clientHeight', 63);
}

// SelectArrow.test.tsx runs only the conformance suite (L2 here, `SelectArrowConformanceTests`); this port title is the L4
// half of its rows (M5 track 9, final review B2) on `select-basic` with `arrow`: what the `positioning` module writes on the
// Arrow outside aligned mode, and nothing rendered while aligned.
test.describe('<Select.Arrow />', () => {
  // SEL-DOM-40, SEL-POS-20, SEL-STR-09
  test("port: outside aligned mode the Arrow carries the positioning module's style, data-side, data-align and data-uncentered, and aligned it renders nothing", async ({ page, mode }) => {
    const centerOffset = async () => {
      const arrowBox = (await page.getByTestId('arrow').boundingBox())!;
      const triggerBox = (await page.getByTestId('trigger').boundingBox())!;
      return Math.abs(Math.round(arrowBox.x + arrowBox.width / 2 - (triggerBox.x + triggerBox.width / 2)));
    };

    await gotoPopupFixture(page, mode, 'select-basic', { align: false, arrow: 'center' });
    await page.getByTestId('trigger').click();
    const arrow = page.getByTestId('arrow');
    await expect(arrow).toBeVisible();
    await expect(arrow).toHaveAttribute('aria-hidden', 'true');
    await expect(arrow).toHaveAttribute('data-open', '');
    await expect(arrow).toHaveAttribute('data-side', 'bottom');
    await expect(arrow).toHaveAttribute('data-align', 'center');
    await expect(arrow).not.toHaveAttribute('data-uncentered');
    expect(await arrow.evaluate((element) => (element as HTMLElement).style.position)).toBe('absolute');
    await expect.poll(centerOffset).toBe(0);
    for (const name of ['data-closed', 'data-anchor-hidden', 'data-starting-style', 'data-ending-style']) {
      await expect(arrow).not.toHaveAttribute(name);
    }

    // A 20px trigger at the left edge under a 200px popup: the popup shifts inside the viewport and the arrow cannot center.
    await gotoPopupFixture(page, mode, 'select-basic', { align: false, arrow: 'edge' });
    await page.getByTestId('trigger').click();
    await expect(arrow).toHaveAttribute('data-uncentered', '');
    await expect(arrow).toHaveAttribute('data-side', 'bottom');
    expect(await arrow.evaluate((element) => (element as HTMLElement).style.left)).toMatch(/px$/);
    expect(await centerOffset()).not.toBe(0);

    // Aligned (the default): the popup is placed over the trigger and the Arrow renders nothing.
    await gotoPopupFixture(page, mode, 'select-basic', { arrow: 'center' });
    await page.getByTestId('trigger').click();
    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(page.getByTestId('positioner')).toHaveAttribute('data-side', 'none');
    await expect(arrow).toHaveCount(0);
  });
});
