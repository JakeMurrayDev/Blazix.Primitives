import { type Locator, type Page } from '@playwright/test';
import { expect, test, type RenderMode } from '../helpers';
import { gotoPopupFixture, pressInPage, settle, type PopupFixtureOptions } from './popupFixture';

// Base UI's viewportConformanceTests (packages/react/test/viewportConformanceTests.tsx at v1.9.0), titles verbatim, as a
// Playwright helper (ADR 09 Amendments 2026-10-27; ADR 06 Amendments 2026-10-27, one fixture base). A family's spec calls
// it with its fixture, a page inheriting PopupSuiteFixtureBase that renders the suite's createComponent tree. The suite's
// `render(...)` with root, portal and positioner props becomes the fixture's query; its `rerender` and state setters
// become `[data-…]` buttons, pressed in the page so a trigger opened by focus is not blurred. The `<style>` the suite
// renders (the morph animations or transitions, the trigger positions) is added to the page. The morph's states last
// one animation, so they are read from the records of a recorder installed before the switch (ADR 06 §2), never from
// the live page. Base UI's `ignoreActWarnings` setup is React's and has no port. A title listed in `open` is skipped
// with its reason.

/** The options of viewportConformanceTests (Base UI's ViewportTestConfig, the rendering half replaced by a fixture). */
export interface ViewportConformanceConfig {
  /**
   * The fixture page. Its root renders triggers `trigger-1`.. (`id` and `data-testid` `trigger-N`, payload `N`, text
   * "Trigger N"; `triggerCount`, default 2) and Portal > Positioner > Popup (`data-testid="popup"`) > Viewport
   * (`data-testid="viewport"`), whose child is `<div data-testid="content">` with the text `Content`, followed by
   * ` <payload>` when there is one. Query options:
   * - `open=true`: a controlled root that stays open; `defaultOpen`, `defaultTriggerId`; `keepMounted` (the portal's);
   * - `side`: the positioner's side, with collision avoidance `side: 'none'`; `direction=ltr|rtl`: a direction provider
   *   around the tree;
   * - `case=controlled`: a controlled root that starts closed and follows its OnOpenChange; `[data-set-open="false"]`
   *   closes it (the suite's `setOpenExternal(false)`);
   * - `case=retained`: the root's OnOpenChange keeps the popup mounted when it closes (Base UI's
   *   `preventUnmountOnClose()`); `[data-remount-trigger]` remounts the trigger (a new key, the same id). With
   *   `strict=true` it remounts it twice in one handler, a render between: the port's form of Strict Mode's repeated
   *   mount, whose property ADR 06 Amendments 2026-10-18 restate as an idempotent attach and dispose;
   * - `case=lagging`: `trigger-1` has no payload and `trigger-2` none until `[data-set-payload]` gives it `ready`;
   * - `animations=1`: the host's switch, set by the suite for the morphing titles.
   */
  fixture: string;
  /** How a trigger opens the popup or switches it to that trigger; the fixture's triggers open instantly on it (no delay). */
  openOn: 'click' | 'focus';
  /** Titles listed open, each with its row and the track or milestone that proves it. */
  open?: Readonly<Record<string, string>>;
  /** When given, only these titles are declared. */
  only?: readonly string[];
}

type Side = 'top' | 'bottom' | 'left' | 'right' | 'inline-start' | 'inline-end';

interface AnchoringCase {
  side: Side;
  direction: 'ltr' | 'rtl';
  expected: { position?: string; top?: string; right?: string; bottom?: string; left?: string };
}

interface TriggerPosition {
  top: number;
  left: number;
}

/** A `[data-previous]` or `[data-current]` container as a record saw it. */
interface ContainerRecord {
  /** The element's identity: one number for as long as the element lives, a new one for a remounted container. */
  id: number;
  text: string;
  inert: boolean;
  startingStyle: boolean;
  endingStyle: boolean;
  /** Its inline `--popup-width`. */
  width: string;
  /** Its inline `--popup-height`. */
  height: string;
}

/** The viewport and its containers at a DOM change, or at an animation or transition event on a container. */
interface ViewportRecord {
  /** `start`, `mutation`, or the event's type (`animationstart`, `transitionrun`, their `end` and `cancel`). */
  kind: string;
  /** The id of the container an event ran on; `null` for a DOM change. */
  target: number | null;
  transitioning: boolean;
  /** The tokens of the viewport's `data-activation-direction`. */
  direction: string[];
  previous: ContainerRecord | null;
  current: ContainerRecord | null;
}

type ViewportRecorderWindow = {
  viewportRecords?: ViewportRecord[];
  viewportIdOf?: (element: Element | null) => number | null;
  viewportStopRecording?: () => void;
};

const morphTransitionStyles = `
  [data-transitioning] [data-current] {
    transition: transform 10s linear, opacity 10s linear;
  }
  [data-transitioning] [data-current][data-starting-style] {
    transform: translateX(30%);
    opacity: 0;
  }
  [data-transitioning] [data-previous] {
    transition: transform 10s linear, opacity 10s linear;
  }
  [data-transitioning] [data-previous][data-ending-style] {
    transform: translateX(-30%);
    opacity: 0;
  }
`;

const anchoringCases: readonly AnchoringCase[] = [
  { side: 'top', direction: 'ltr', expected: { position: 'absolute', bottom: '0px', left: '0px' } },
  { side: 'top', direction: 'rtl', expected: { position: 'absolute', bottom: '0px', left: '0px' } },
  { side: 'bottom', direction: 'ltr', expected: {} },
  { side: 'bottom', direction: 'rtl', expected: {} },
  { side: 'left', direction: 'ltr', expected: { position: 'absolute', top: '0px', right: '0px' } },
  { side: 'left', direction: 'rtl', expected: { position: 'absolute', top: '0px', right: '0px' } },
  { side: 'right', direction: 'ltr', expected: {} },
  { side: 'right', direction: 'rtl', expected: {} },
  { side: 'inline-start', direction: 'ltr', expected: { position: 'absolute', top: '0px', right: '0px' } },
  { side: 'inline-start', direction: 'rtl', expected: {} },
  { side: 'inline-end', direction: 'ltr', expected: {} },
  { side: 'inline-end', direction: 'rtl', expected: { position: 'absolute', top: '0px', right: '0px' } },
];

export function viewportConformanceTests(config: ViewportConformanceConfig): void {
  const { fixture: fixtureName, openOn, open: listedOpen = {}, only } = config;

  const getTrigger = (page: Page, number: number) => page.locator(`[data-testid="trigger-${number}"]`);
  const getPopup = (page: Page) => page.locator('[data-testid="popup"]');
  const getViewport = (page: Page) => page.locator('[data-testid="viewport"]');
  const getContent = (page: Page, text: string) => page.getByText(text, { exact: true });

  function render(page: Page, mode: RenderMode, options: PopupFixtureOptions = {}): Promise<Locator> {
    return gotoPopupFixture(page, mode, fixtureName, options);
  }

  /** Renders a morphing case: animations on, and the suite's `<style>` added to the page. */
  async function renderMorphing(page: Page, mode: RenderMode, styles: string, options: PopupFixtureOptions = {}): Promise<Locator> {
    const fixture = await render(page, mode, { ...options, animations: 1 });
    await page.addStyleTag({ content: styles });
    return fixture;
  }

  function renderPositionedTriggers(
    page: Page,
    mode: RenderMode,
    first: TriggerPosition,
    second: TriggerPosition,
    animationDuration: string,
  ): Promise<Locator> {
    return renderMorphing(page, mode, getMorphAnimationStyles(animationDuration) + getTriggerStyles([first, second]));
  }

  async function activateTrigger(page: Page, trigger: Locator): Promise<void> {
    if (openOn === 'click') {
      await trigger.click();
      return;
    }

    await waitFrames(page, 1);
    await trigger.focus();
  }

  /** Declares a title; one listed open is skipped with its reason. */
  function it(title: string, body: (args: { page: Page; mode: RenderMode }) => Promise<void>): void {
    if (only && !only.includes(title)) {
      return;
    }

    test(title, async ({ page, mode }) => {
      test.skip(title in listedOpen, listedOpen[title]);
      await body({ page, mode });
    });
  }

  test.describe('Viewport conformance', () => {
    it('renders children in the `current` container by default', async ({ page, mode }) => {
      const fixture = await render(page, mode, { open: true, triggerCount: 1 });
      await expect(page.locator('[data-testid="content"]')).toHaveCount(1);
      await settle(fixture);

      await expect
        .poll(() => page.locator('[data-testid="content"]').evaluate((node) => node.closest('[data-current]')?.textContent?.trim() ?? null))
        .toBe('Content');
      await expect(page.locator('[data-previous]')).toHaveCount(0);

      const viewport = getViewport(page);
      await expect(viewport).not.toHaveAttribute('data-transitioning');
      await expect(viewport).not.toHaveAttribute('data-activation-direction');
    });

    it('remounts the `current` container when the active trigger changes', async ({ page, mode }) => {
      // Base UI renders a payload image per trigger; the payload's text marks the container here.
      await render(page, mode);
      await recordViewport(page);

      await activateTrigger(page, getTrigger(page, 1));
      await expect(getContent(page, 'Content 1')).toHaveCount(1);
      const firstContainer = await currentContainerOf(page, 'Content 1');
      expect(firstContainer).not.toBeNull();

      await activateTrigger(page, getTrigger(page, 2));
      await expect.poll(() => currentContainerOf(page, 'Content 2')).not.toBeNull();
      expect(await currentContainerOf(page, 'Content 2')).not.toBe(firstContainer);
    });

    for (const strict of [false, true]) {
      it(`does not restart a transition when the active trigger remounts while closed and retained (strict: ${strict})`, async ({ page, mode }) => {
        const fixture = await render(page, mode, { case: 'retained', triggerCount: 1, defaultOpen: true, defaultTriggerId: 'trigger-1', strict });
        await expect(getPopup(page)).toBeVisible();

        await page.keyboard.press('Escape');
        await expect(getPopup(page)).toHaveAttribute('data-closed', '');

        await recordViewport(page);
        await press(fixture, '[data-remount-trigger]');
        await settle(fixture);
        await waitFrames(page, 1);

        await expect(getPopup(page)).toHaveAttribute('data-closed', '');
        // A restarted transition marks the viewport and adds a previous container, if only for a frame.
        expect((await viewportRecords(page)).filter((record) => record.transitioning || record.previous !== null)).toEqual([]);
        await expect(getViewport(page)).not.toHaveAttribute('data-transitioning');
        await expect(page.locator('[data-previous]')).toHaveCount(0);
      });
    }

    for (const { side, direction, expected } of anchoringCases) {
      it(`anchors the popup for side=${side} in ${direction} mode`, async ({ page, mode }) => {
        const fixture = await render(page, mode, { open: true, triggerCount: 1, side, direction });
        await expect(getPopup(page)).toBeVisible();
        await settle(fixture);
        await waitFrames(page, 1);

        await expect
          .poll(() =>
            getPopup(page).evaluate((node) => {
              const { style } = node as HTMLElement;
              return { position: style.position, top: style.top, right: style.right, bottom: style.bottom, left: style.left };
            }),
          )
          .toEqual({
            position: expected.position ?? '',
            top: expected.top ?? '',
            right: expected.right ?? '',
            bottom: expected.bottom ?? '',
            left: expected.left ?? '',
          });
      });
    }

    test.describe('morphing containers', () => {
      it('creates morphing containers during transitions', async ({ page, mode }) => {
        await renderPositionedTriggers(page, mode, { top: 10, left: 10 }, { top: 100, left: 200 }, '0.3s');

        await activateTrigger(page, getTrigger(page, 1));
        await expect(getContent(page, 'Content 1')).toBeVisible();
        const viewport = getViewport(page);
        await expect(viewport).not.toHaveAttribute('data-transitioning');
        await expect(viewport).not.toHaveAttribute('data-activation-direction');

        await recordViewport(page);
        await activateTrigger(page, getTrigger(page, 2));

        // One record holds the whole morph: the previous container, inert and sized, beside the new current one.
        const pixels = /^\d+(?:\.\d+)?px$/;
        await expect
          .poll(() => viewportRecords(page))
          .toContainEqual(
            expect.objectContaining({
              transitioning: true,
              direction: ['right', 'down'],
              previous: expect.objectContaining({
                text: 'Content 1',
                inert: true,
                width: expect.stringMatching(pixels),
                height: expect.stringMatching(pixels),
              }),
              current: expect.objectContaining({ text: 'Content 2' }),
            }),
          );

        // The previous container is removed once the animations finish.
        await expect(page.locator('[data-previous]')).toHaveCount(0);

        await expect(viewport).not.toHaveAttribute('data-transitioning');
        await expect(page.locator('[data-current]')).toBeVisible();
        await expect(getContent(page, 'Content 2')).toBeVisible();
      });

      it('sets `data-activation-direction` from the previous to the new trigger', async ({ page, mode }) => {
        // `getActivationDirection` unit tests cover every direction and the tolerance.
        // This case checks the measured trigger centers reach the attribute.
        await renderPositionedTriggers(page, mode, { top: 100, left: 200 }, { top: 10, left: 10 }, '0.2s');

        await activateTrigger(page, getTrigger(page, 1));
        await expect(getContent(page, 'Content 1')).toBeVisible();

        await recordViewport(page);
        await activateTrigger(page, getTrigger(page, 2));

        await expect.poll(() => viewportRecords(page)).toContainEqual(expect.objectContaining({ direction: ['left', 'up'] }));
      });

      it('creates morphing containers after a kept-mounted popup closes and reopens', async ({ page, mode }) => {
        const fixture = await renderMorphing(page, mode, getMorphAnimationStyles('0.2s'), { case: 'controlled', keepMounted: true });

        const trigger1 = getTrigger(page, 1);
        const trigger2 = getTrigger(page, 2);

        await activateTrigger(page, trigger1);
        await expect(getContent(page, 'Content 1')).toBeVisible();

        await recordViewport(page);
        await activateTrigger(page, trigger2);
        await expect
          .poll(() => viewportRecords(page))
          .toContainEqual(expect.objectContaining({ previous: expect.objectContaining({ text: 'Content 1' }) }));
        await expect(page.locator('[data-previous]')).toHaveCount(0);

        await press(fixture, '[data-set-open="false"]');
        // `toBeVisible` turns false once the exit animation reaches opacity 0, before the exit
        // completes, so also wait for the ending state to clear before reopening.
        await expect(getPopup(page)).toBeHidden();
        await expect(getPopup(page)).not.toHaveAttribute('data-ending-style');

        await activateTrigger(page, trigger1);
        await expect(getContent(page, 'Content 1')).toBeVisible();

        await recordViewport(page);
        await activateTrigger(page, trigger2);

        await expect
          .poll(() => viewportRecords(page))
          .toContainEqual(
            expect.objectContaining({
              previous: expect.objectContaining({ text: 'Content 1' }),
              current: expect.objectContaining({ text: 'Content 2' }),
            }),
          );
        await expect(getContent(page, 'Content 2')).toBeVisible();
      });

      it('shows the latest content after rapid trigger changes', async ({ page, mode }) => {
        await renderMorphing(page, mode, getMorphAnimationStyles('0.2s'), { triggerCount: 3 });

        const trigger1 = getTrigger(page, 1);

        await activateTrigger(page, trigger1);
        await activateTrigger(page, getTrigger(page, 2));
        await activateTrigger(page, getTrigger(page, 3));
        await activateTrigger(page, trigger1);

        await expect(page.locator('[data-current]').getByText('Content 1', { exact: true })).toBeVisible();
      });

      it('keeps the latest transition active during rapid trigger changes', async ({ page, mode }) => {
        const fixture = await renderMorphing(page, mode, getMorphAnimationStyles('10s'), { triggerCount: 3 });

        await activateTrigger(page, getTrigger(page, 1));
        await expect(getContent(page, 'Content 1')).toBeVisible();

        await recordViewport(page);
        await activateTrigger(page, getTrigger(page, 2));

        await expect.poll(async () => runningOn(await viewportRecords(page), 'Content 2')).toBe(1);
        // Allow `useAnimationsFinished` to begin waiting before replacing the current container.
        await waitFrames(page, 1);

        await activateTrigger(page, getTrigger(page, 3));
        await settle(fixture);
        await waitFrames(page, 1);

        await expect.poll(async () => runningOn(await viewportRecords(page), 'Content 3')).toBe(1);
        await expect
          .poll(() => viewportRecords(page))
          .toContainEqual(
            expect.objectContaining({
              transitioning: true,
              previous: expect.objectContaining({ text: 'Content 2' }),
              current: expect.objectContaining({ text: 'Content 3' }),
            }),
          );

        // A frame after the switch, the containers move from starting to ending styles.
        await expect
          .poll(() => viewportRecords(page))
          .toContainEqual(
            expect.objectContaining({
              previous: expect.objectContaining({ text: 'Content 2', endingStyle: true }),
              current: expect.objectContaining({ text: 'Content 3', startingStyle: false }),
            }),
          );
      });

      it('cleans up the transition when a lagging payload remounts the current container', async ({ page, mode }) => {
        const styles = morphTransitionStyles + getTriggerStyles([{ top: 10, left: 10 }, { top: 100, left: 200 }]);
        const fixture = await renderMorphing(page, mode, styles, { case: 'lagging' });

        await activateTrigger(page, getTrigger(page, 1));
        await expect(page.locator('[data-current]')).toHaveCount(1);

        await recordViewport(page);
        await activateTrigger(page, getTrigger(page, 2));

        // The morph is in progress: the previous snapshot exists and both containers
        // are running their (long) transitions.
        await expect.poll(async () => runningOnLatest(await viewportRecords(page), 'previous')).toBeGreaterThanOrEqual(1);
        await expect.poll(async () => runningOnLatest(await viewportRecords(page), 'current')).toBeGreaterThanOrEqual(1);

        // Allow `useAnimationsFinished` to begin waiting before the container is replaced.
        await waitFrames(page, 2);

        const containerBeforePayload = await currentContainerId(page);

        // The payload for the already-active trigger arrives a render later, which
        // bumps the content key and remounts the current container mid-morph.
        await press(fixture, '[data-set-payload]');

        await expect.poll(() => currentContainerId(page)).not.toBe(containerBeforePayload);
        await expect(page.locator('[data-current]')).toHaveText('Content ready');
        const remounted = await currentContainerId(page);

        // The remounted container must restart its entry transition, otherwise the
        // cleanup watcher finds nothing to await and truncates the exit transition.
        await expect.poll(async () => running(await viewportRecords(page), remounted)).toBeGreaterThanOrEqual(1);

        // The previous container's exit transition is still running, so it must not
        // have been torn down in the frames right after the remount.
        await waitFrames(page, 4);
        const records = await viewportRecords(page);
        const sinceRemount = records.slice(Math.max(0, records.findIndex((record) => record.current?.id === remounted)));
        expect(sinceRemount.filter((record) => record.previous === null)).toEqual([]);

        // Finish the live animations so the cleanup watcher can settle.
        await expect
          .poll(async () => {
            await page.evaluate(() => {
              document.querySelectorAll('[data-previous], [data-current]').forEach((element) => {
                element.getAnimations().forEach((animation) => animation.finish());
              });
            });
            return page.locator('[data-previous]').count();
          })
          .toBe(0);

        await expect(getViewport(page)).not.toHaveAttribute('data-transitioning');
      });
    });
  });
}

/**
 * Records, from now on, the viewport and its containers at every DOM change and at every animation or transition event
 * on a container, replacing the records of an earlier call. A morph lasts one animation and can be over before any
 * read the test makes; its records stay (ADR 06 §2: wait on markers, never on samples).
 */
async function recordViewport(page: Page): Promise<void> {
  await page.evaluate(() => {
    const state = window as unknown as ViewportRecorderWindow;
    state.viewportStopRecording?.();

    const idOf = (state.viewportIdOf ??= (() => {
      const ids = new WeakMap<Element, number>();
      let next = 0;
      return (element: Element | null) => {
        if (element === null) {
          return null;
        }

        let id = ids.get(element);
        if (id === undefined) {
          next += 1;
          id = next;
          ids.set(element, id);
        }

        return id;
      };
    })());

    const container = (element: Element | null): ContainerRecord | null =>
      element instanceof HTMLElement
        ? {
            id: idOf(element) ?? 0,
            text: (element.textContent ?? '').trim(),
            inert: element.hasAttribute('inert'),
            startingStyle: element.hasAttribute('data-starting-style'),
            endingStyle: element.hasAttribute('data-ending-style'),
            width: element.style.getPropertyValue('--popup-width'),
            height: element.style.getPropertyValue('--popup-height'),
          }
        : null;

    const records: ViewportRecord[] = [];
    state.viewportRecords = records;
    const take = (kind: string, target: number | null) => {
      const viewport = document.querySelector('[data-testid="viewport"]');
      records.push({
        kind,
        target,
        transitioning: viewport?.hasAttribute('data-transitioning') ?? false,
        direction: (viewport?.getAttribute('data-activation-direction') ?? '').split(' ').filter(Boolean),
        previous: container(document.querySelector('[data-previous]')),
        current: container(document.querySelector('[data-current]')),
      });
    };

    const observer = new MutationObserver(() => take('mutation', null));
    observer.observe(document.body, {
      subtree: true,
      childList: true,
      characterData: true,
      attributes: true,
      attributeFilter: [
        'data-transitioning',
        'data-activation-direction',
        'data-previous',
        'data-current',
        'data-starting-style',
        'data-ending-style',
        'inert',
        'style',
      ],
    });

    const abort = new AbortController();
    const onEvent = (event: Event) => {
      const target = event.target;
      if (target instanceof Element && (target.hasAttribute('data-previous') || target.hasAttribute('data-current'))) {
        take(event.type, idOf(target));
      }
    };
    for (const type of ['animationstart', 'animationend', 'animationcancel', 'transitionrun', 'transitionend', 'transitioncancel']) {
      document.addEventListener(type, onEvent, { capture: true, signal: abort.signal });
    }

    state.viewportStopRecording = () => {
      observer.disconnect();
      abort.abort();
    };
    take('start', null);
  });
}

/** The records of {@link recordViewport}. */
function viewportRecords(page: Page): Promise<ViewportRecord[]> {
  return page.evaluate(() => (window as unknown as ViewportRecorderWindow).viewportRecords ?? []);
}

/** The recorder's id of the live `[data-current]` container, the one that outlasts a morph. */
function currentContainerId(page: Page): Promise<number | null> {
  return page.evaluate(() => (window as unknown as ViewportRecorderWindow).viewportIdOf?.(document.querySelector('[data-current]')) ?? null);
}

/** The recorder's id of the `[data-current]` container around the content showing `text`, or `null`. */
function currentContainerOf(page: Page, text: string): Promise<number | null> {
  return page.evaluate((expected) => {
    const content = [...document.querySelectorAll('[data-testid="content"]')].find((node) => (node.textContent ?? '').trim() === expected);
    return (window as unknown as ViewportRecorderWindow).viewportIdOf?.(content?.closest('[data-current]') ?? null) ?? null;
  }, text);
}

/** The animations and transitions recorded as started, and not ended or canceled, on the container `id`. */
function running(records: readonly ViewportRecord[], id: number | null): number {
  let count = 0;
  for (const record of records) {
    if (id === null || record.target !== id) {
      continue;
    }

    count += record.kind === 'animationstart' || record.kind === 'transitionrun' ? 1 : -1;
  }

  return count;
}

/** {@link running} on the last container recorded as `[data-current]` while it showed `text`. */
function runningOn(records: readonly ViewportRecord[], text: string): number {
  const showing = records.filter((record) => record.current?.text === text).at(-1);
  return running(records, showing?.current?.id ?? null);
}

/** {@link running} on the `role` container of the latest record. */
function runningOnLatest(records: readonly ViewportRecord[], role: 'previous' | 'current'): number {
  return running(records, records.at(-1)?.[role]?.id ?? null);
}

/** Presses a fixture control in the page: no pointer or focus moves, so the popup sees no outside press and no blur. */
// In the page, so a trigger opened by focus is not blurred; a control marked `data-key-control` takes a keydown, because a
// click on it would be an outside press (popupFixture.ts).
async function press(fixture: Locator, selector: string): Promise<void> {
  await pressInPage(fixture.locator(selector));
}

/** Waits `count` animation frames in the page (Base UI's `waitSingleFrame`). */
async function waitFrames(page: Page, count: number): Promise<void> {
  await page.evaluate(async (frames) => {
    for (let frame = 0; frame < frames; frame += 1) {
      await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    }
  }, count);
}

function getMorphAnimationStyles(duration: string): string {
  return `
    [data-transitioning] [data-previous] {
      animation: slide-out ${duration} ease-out forwards;
    }
    [data-transitioning] [data-current] {
      animation: slide-in ${duration} ease-out forwards;
    }
    @keyframes slide-out {
      from { transform: translateX(0); opacity: 1; }
      to { transform: translateX(-30%); opacity: 0; }
    }
    @keyframes slide-in {
      from { transform: translateX(30%); opacity: 0; }
      to { transform: translateX(0); opacity: 1; }
    }
  `;
}

/** Base UI's trigger `style` per position, as rules on the fixture's triggers `trigger-1`.. in order. */
function getTriggerStyles(positions: readonly TriggerPosition[]): string {
  return positions
    .map(({ top, left }, index) => {
      const rule = `position: absolute; top: ${top}px; left: ${left}px; width: 100px; height: 50px;`;
      return `[data-testid="trigger-${index + 1}"] { ${rule} }`;
    })
    .join('\n');
}
