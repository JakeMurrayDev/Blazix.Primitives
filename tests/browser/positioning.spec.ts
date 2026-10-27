import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, gotoFixture, type Page } from './helpers';
import { gotoPositionerCase, inlineStyle, positionerConformanceTests } from './suites/positioner-conformance';

// The [L4] rows of docs/architecture/floating-and-positioning.md that the `positioning` module owns (M4 track 2), on the
// test-only FixturePositioner (ADR 12 Amendments 2026-10-27 item 1; the M2/M3 composite precedent): no Positioner part
// ships before M4 tracks 5 to 7, which run the conformance suite again through their own parts.
// - The positioner conformance suite with its Viewport titles over an AdaptiveOrigin stand-in, and the 19 "transform
//   origin" titles of popover/positioner/PopoverPositioner.test.tsx, Popover being incidental to them (design point 8,
//   settled by the orchestrator 2026-10-27).
// - The four browser-only lazy-flip titles of internals/useAnchorPositioning.test.tsx in the page, importing the module
//   the library ships with a stub twin (the dom.spec.ts precedent).
// - Port titles: the pre-position style per mount (FLT-DOM-02, FLT-ANI-02), will-change at a high pixel ratio
//   (FLT-DOM-03), the prerendered markup (ADR 03), the arrow (FLT-DOM-09), the C# mirror per mode (the RND-STA-01 row of
//   ADR 01, FLT-DOM-08) and a scroll re-positioning in the same frame on both modes (FLT-POS-16, FLT-ALG-13).
// Every test runs on both render modes; Server latency is added through `page.routeWebSocket`. No sleeps: final values
// are polled, transient ones are recorded in the page by a MutationObserver.

positionerConformanceTests({ fixture: 'positioning-suite', viewport: true });

/** The Positioner of a case and the element it is anchored to. */
function parts(container: Locator): { positioner: Locator; trigger: Locator } {
  return { positioner: container.getByTestId('positioner'), trigger: container.getByTestId('trigger') };
}

async function rect(element: Locator): Promise<{ x: number; y: number; left: number; top: number; right: number; bottom: number }> {
  return element.evaluate((node) => {
    const { x, y, left, top, right, bottom } = node.getBoundingClientRect();
    return { x, y, left, top, right, bottom };
  });
}

test.describe('transform origin', () => {
  const popupWidth = 52;
  const anchorWidth = 72;
  const anchorHeight = 36;

  async function origin(page: Page, mode: 'server' | 'wasm', name: string): Promise<{ positioner: Locator; trigger: Locator }> {
    const container = await gotoPositionerCase(page, mode, 'positioning-origin', name);
    const found = parts(container);
    // The first pass has run once the positioner is no longer transparent.
    await expect.poll(() => inlineStyle(found.positioner, 'opacity')).not.toBe('0');
    return found;
  }

  const getTransformOrigin = (positioner: Locator) => inlineStyle(positioner, '--transform-origin');

  test('points to the anchor center for center alignment', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'center');

    await expect.poll(() => getTransformOrigin(positioner)).toBe(`${popupWidth / 2}px 0px`);
  });

  test('points to the popup start edge for start alignment', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'start');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('0% 0px');
  });

  test('points to the popup logical start edge for start alignment in RTL', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'start-rtl');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('100% 0px');
  });

  test('uses the floating element direction for RTL alignment', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'floating-rtl');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('100% 0px');
  });

  test('points to the popup end edge for end alignment', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'end');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('100% 0px');
  });

  test('points to the popup logical end edge for end alignment in RTL', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'end-rtl');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('0% 0px');
  });

  test('places the side coordinate first for horizontal sides', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'horizontal');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('0px 0%');
  });

  test('points to the popup end edge for horizontal end alignment', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'horizontal-end');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('calc(100% + 7px) 100%');
  });

  test('accounts for sideOffset', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'side-offset');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('0% -7px');
  });

  test('accounts for sideOffset on top side', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'side-offset-top');

    await expect.poll(() => getTransformOrigin(positioner)).toBe('0% calc(100% + 7px)');
  });

  test('keeps the popup start edge as the origin with alignOffset', async ({ page, mode }) => {
    const alignOffset = 7;
    const { positioner, trigger } = await origin(page, mode, 'align-offset');

    await expect.poll(async () => (await rect(positioner)).left - (await rect(trigger)).left).toBe(alignOffset);
    expect(await getTransformOrigin(positioner)).toBe('0% 0px');
  });

  test('does not let the virtual arrow displace the popup on a narrow anchor', async ({ page, mode }) => {
    const { positioner, trigger } = await origin(page, mode, 'narrow');

    // The arrow padding must not move a popup that has no arrow to keep off its corners.
    await expect.poll(async () => (await rect(positioner)).left).toBe((await rect(trigger)).left);
    expect(await getTransformOrigin(positioner)).toBe('0% 0px');
  });

  test('points to the flipped edge when the alignment flips', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'align-flip');

    await expect(positioner).toHaveAttribute('data-align', 'end');
    await expect.poll(() => getTransformOrigin(positioner)).toBe('100% 0px');
  });

  test('keeps the popup start edge as the origin for a 1px cross-axis shift', async ({ page, mode }) => {
    const { positioner, trigger } = await origin(page, mode, 'one-px-shift');

    await expect.poll(async () => Math.round((await rect(positioner)).left - (await rect(trigger)).left)).toBe(-1);
    expect(await getTransformOrigin(positioner)).toBe('0% 0px');
  });

  test('points to the anchor center when start alignment is shifted on the cross axis', async ({ page, mode }) => {
    const { positioner, trigger } = await origin(page, mode, 'start-shifted');

    await expect.poll(async () => (await rect(positioner)).left < (await rect(trigger)).left).toBe(true);
    const anchorRect = await rect(trigger);
    const positionerRect = await rect(positioner);
    await expect.poll(() => getTransformOrigin(positioner)).toBe(`${anchorRect.x + anchorWidth / 2 - positionerRect.x}px 0px`);
  });

  test('points to the anchor center when end alignment is shifted on the cross axis', async ({ page, mode }) => {
    const { positioner, trigger } = await origin(page, mode, 'end-shifted');

    await expect.poll(async () => (await rect(positioner)).right > (await rect(trigger)).right).toBe(true);
    const anchorRect = await rect(trigger);
    const positionerRect = await rect(positioner);
    await expect.poll(() => getTransformOrigin(positioner)).toBe(`${anchorRect.x + anchorWidth / 2 - positionerRect.x}px 0px`);
  });

  test('points to the anchor center when horizontal alignment is shifted on the cross axis', async ({ page, mode }) => {
    const { positioner, trigger } = await origin(page, mode, 'horizontal-shifted');

    await expect.poll(async () => (await rect(positioner)).top < (await rect(trigger)).top).toBe(true);
    const anchorRect = await rect(trigger);
    const positionerRect = await rect(positioner);
    await expect.poll(() => getTransformOrigin(positioner)).toBe(`0px ${anchorRect.y + anchorHeight / 2 - positionerRect.y}px`);
  });

  test('points to the arrow when present regardless of alignment', async ({ page, mode }) => {
    const { positioner } = await origin(page, mode, 'arrow');

    // With start alignment the anchor's center sits half an anchor width into the popup.
    await expect.poll(() => getTransformOrigin(positioner)).toBe(`${anchorWidth / 2}px 0px`);
  });

  test('points to the anchor center when the popup is shifted to overlap the anchor', async ({ page, mode }) => {
    const { positioner, trigger } = await origin(page, mode, 'overlap');

    await expect.poll(async () => (await rect(positioner)).top < (await rect(trigger)).bottom).toBe(true);
    const anchorRect = await rect(trigger);
    const positionerRect = await rect(positioner);
    await expect.poll(() => getTransformOrigin(positioner)).toBe(`${popupWidth / 2}px ${anchorRect.y + anchorHeight / 2 - positionerRect.y}px`);
  });
});

// internals/useAnchorPositioning.test.tsx's browser-only titles (FLT-ANI-04; FLT-API-10 and FLT-ALG-05 for the last):
// Base UI's TestLazyFlip in the page, the shipped module attached to a 100×100 floating element with a stub twin, its
// height shrunk to 10 through an update of the consumer's style, as C# would send it.
test.describe('useAnchorPositioning', () => {
  const positioningModule = '/_content/Blazix.Primitives/js/positioning.js';

  async function renderLazyFlip(
    page: Page,
    mode: 'server' | 'wasm',
    props: { side?: 'right' | 'bottom'; align?: 'start' | 'center'; lazyFlip?: 'side' | 'placement' | null },
  ): Promise<{ floating: Locator; anchor: Locator; shrink(): Promise<void> }> {
    await gotoFixture(page, mode, 'probe');
    const { side = 'right', align = 'start', lazyFlip = 'side' } = props;
    await page.evaluate(
      async ({ path, side, align, lazyFlip }) => {
        const { attach } = await import(path);
        const anchor = document.createElement('div');
        anchor.id = 'lazy-anchor';
        anchor.setAttribute('data-testid', 'lazy-anchor');
        anchor.style.cssText = 'position: fixed; right: 200px; bottom: 30px; width: 20px; height: 20px';
        const floating = document.createElement('div');
        floating.setAttribute('data-testid', 'lazy-floating');
        floating.setAttribute('data-open', '');
        document.body.append(anchor, floating);
        const twin = {
          invokeMethod() {
            throw new Error('The current dispatcher does not support synchronous calls from JS to .NET.');
          },
          invokeMethodAsync() {
            return Promise.resolve();
          },
        };
        const options = {
          triggerId: 'lazy-anchor',
          positionMethod: 'fixed',
          side,
          align,
          collisionAvoidance: { fallbackAxisSide: 'none' },
          lazyFlip,
          style: 'width: 100px; height: 100px',
        };
        const handle = attach(floating, JSON.stringify(options), twin);
        (window as unknown as { lazyFlip: unknown }).lazyFlip = { handle, options };
      },
      { path: positioningModule, side, align, lazyFlip },
    );
    return {
      floating: page.getByTestId('lazy-floating'),
      anchor: page.getByTestId('lazy-anchor'),
      shrink: () =>
        page.evaluate(() => {
          const { handle, options } = (window as unknown as { lazyFlip: { handle: { update(json: string): void }; options: object } }).lazyFlip;
          handle.update(JSON.stringify({ ...options, style: 'width: 100px; height: 10px' }));
        }),
    };
  }

  test('locks a flipped alignment after the popup shrinks', async ({ page, mode }) => {
    // Only `'placement'` locks the align axis. Plain `true` stays side-only so Combobox keeps recomputing its alignment.
    const { floating, anchor, shrink } = await renderLazyFlip(page, mode, { lazyFlip: 'placement' });

    await expect(floating).toHaveAttribute('data-align', 'end');

    await shrink();

    await expect.poll(async () => Math.abs((await rect(anchor)).bottom - (await rect(floating)).bottom) < 1).toBe(true);

    await expect(floating).toHaveAttribute('data-align', 'end');
  });

  test('leaves the alignment free when lazy flipping is side-only', async ({ page, mode }) => {
    const { floating, shrink } = await renderLazyFlip(page, mode, { lazyFlip: 'side' });

    await expect(floating).toHaveAttribute('data-align', 'end');

    await shrink();

    await expect(floating).toHaveAttribute('data-align', 'start');
  });

  test('locks a flipped side after the popup shrinks', async ({ page, mode }) => {
    const { floating, anchor, shrink } = await renderLazyFlip(page, mode, { side: 'bottom', align: 'center' });

    // The anchor sits at the viewport's bottom edge, so the popup flips above it.
    await expect(floating).toHaveAttribute('data-side', 'top');

    // Shrinking makes the preferred bottom side fit again, but the flip is locked.
    await shrink();

    await expect.poll(async () => Math.abs((await rect(anchor)).top - (await rect(floating)).bottom) < 1).toBe(true);

    await expect(floating).toHaveAttribute('data-side', 'top');
  });

  test('flips back to the preferred side without lazy flipping', async ({ page, mode }) => {
    const { floating, shrink } = await renderLazyFlip(page, mode, { side: 'bottom', align: 'center', lazyFlip: null });

    await expect(floating).toHaveAttribute('data-side', 'top');

    await shrink();

    await expect(floating).toHaveAttribute('data-side', 'bottom');
  });
});

/** Adds latency to the circuit's WebSocket in both directions (copied per spec, as tabs.spec.ts does). */
async function delayCircuit(page: Page, delay: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), delay);
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), delay);
    });
  });
}

test.describe('positioning module', () => {
  test('port: the Positioner stays fixed at the viewport origin and transparent until the first pass of each mount', async ({ page, mode }) => {
    const container = await gotoPositionerCase(page, mode, 'positioning-lifecycle', 'kept-mounted');
    const { positioner } = parts(container);
    // Every attribute change of the Positioner, in order, with the style it had then (FLT-DOM-02, FLT-ANI-02).
    await positioner.evaluate((node) => {
      const element = node as HTMLElement;
      const records: { hidden: boolean; position: string; opacity: string; transform: string }[] = [];
      (element as unknown as { records: typeof records }).records = records;
      new MutationObserver(() => {
        records.push({ hidden: element.hidden, position: element.style.position, opacity: element.style.opacity, transform: element.style.transform });
      }).observe(element, { attributes: true });
    });
    const records = () => positioner.evaluate((node) => (node as unknown as { records: { hidden: boolean; position: string; opacity: string; transform: string }[] }).records);
    const positioned = (record: { opacity: string; transform: string }) => record.opacity === '' && record.transform.startsWith('translate(');

    await container.locator('[data-open-button]').click();
    await expect.poll(async () => (await records()).some(positioned)).toBe(true);
    let shown = (await records()).filter((record) => !record.hidden);
    expect(shown[0]).toEqual({ hidden: false, position: 'fixed', opacity: '0', transform: '' });

    await container.locator('[data-close-button]').click();
    await expect(positioner).toHaveAttribute('hidden', '');
    await expect.poll(() => inlineStyle(positioner, 'opacity')).toBe('0');
    expect(await inlineStyle(positioner, 'position')).toBe('fixed');
    expect(await inlineStyle(positioner, 'transform')).toBe('');
    expect(await inlineStyle(positioner, 'pointer-events')).toBe('none');

    // A kept-mounted reopen starts hidden again: the coordinates of the previous open are not applied.
    const before = (await records()).length;
    await container.locator('[data-open-button]').click();
    await expect.poll(async () => (await records()).slice(before).some(positioned)).toBe(true);
    shown = (await records()).slice(before).filter((record) => !record.hidden);
    expect(shown[0]).toEqual({ hidden: false, position: 'fixed', opacity: '0', transform: '' });
    expect(await inlineStyle(positioner, 'pointer-events')).toBe('');
  });

  test('port: will-change is set at a device pixel ratio of 1.5 or more', async ({ browser, page, mode }) => {
    // FLT-DOM-03: the coordinates rounded to the device pixel grid, plus will-change: transform at dpr >= 1.5.
    const container = await gotoPositionerCase(page, mode, 'positioning-suite', 'default');
    await expect.poll(() => inlineStyle(container.getByTestId('positioner'), 'transform')).toBe('translate(10px, 36px)');
    expect(await inlineStyle(container.getByTestId('positioner'), 'will-change')).toBe('');

    const context = await browser.newContext({ deviceScaleFactor: 2 });
    try {
      const highDensity = await context.newPage();
      await highDensity.clock.install();
      const dense = await gotoPositionerCase(highDensity, mode, 'positioning-suite', 'default');
      await expect.poll(() => inlineStyle(dense.getByTestId('positioner'), 'transform')).toBe('translate(10px, 36px)');
      expect(await inlineStyle(dense.getByTestId('positioner'), 'will-change')).toBe('transform');
    } finally {
      await context.close();
    }
  });

  test('port: the prerendered Positioner carries the requested placement and the pre-position style, and no transform', async ({ page, mode }) => {
    // ADR 03 Consequences, ADR 04 Amendments 2026-10-18 and 2026-10-27; FLT-DOM-01, FLT-DOM-02, FLT-DOM-05.
    const html = await (await page.request.get(fixtureUrl(mode, 'positioning-prerender'))).text();
    const positionerTag = /<div[^>]*data-testid="positioner"[^>]*>/.exec(html)?.[0] ?? '';

    expect(positionerTag).toContain('role="presentation"');
    expect(positionerTag).toContain('data-side="top"');
    expect(positionerTag).toContain('data-align="start"');
    expect(positionerTag).toContain(
      'style="position: fixed; top: 0px; left: 0px; --available-width: 100vw; --available-height: 100vh; opacity: 0; color: rgb(1, 2, 3)"',
    );
    expect(positionerTag).not.toContain('transform');

    const container = await gotoFixture(page, mode, 'positioning-prerender');
    await expect(container).toHaveAttribute('data-attached', 'true');
    const positioner = container.getByTestId('positioner');
    await expect.poll(() => inlineStyle(positioner, 'transform')).toMatch(/^translate\(/);
    expect(await inlineStyle(positioner, 'opacity')).toBe('');
    expect(await inlineStyle(positioner, 'color')).toBe('rgb(1, 2, 3)');
    await expect(positioner).toHaveAttribute('data-side', 'top');
  });

  test('port: the Arrow points at the anchor center, and is marked uncentered when it cannot', async ({ page, mode }) => {
    // FLT-DOM-09: the arrow's position from the arrow middleware, data-uncentered from its centerOffset.
    let container = await gotoPositionerCase(page, mode, 'positioning-lifecycle', 'arrow');
    let arrow = container.getByTestId('arrow');
    await expect.poll(() => inlineStyle(arrow, 'left')).toBe('21px');
    expect(await inlineStyle(arrow, 'position')).toBe('absolute');
    await expect(arrow).toHaveAttribute('data-side', 'bottom');
    await expect(arrow).not.toHaveAttribute('data-uncentered');
    await expect(arrow).toHaveAttribute('data-mirrored-uncentered', 'false');

    container = await gotoPositionerCase(page, mode, 'positioning-lifecycle', 'arrow-edge');
    arrow = container.getByTestId('arrow');
    await expect(arrow).toHaveAttribute('data-uncentered', '');
    await expect(arrow).toHaveAttribute('data-mirrored-uncentered', 'true');
    expect(await inlineStyle(arrow, 'left')).toBe('5px');
  });

  test('port: data-side flips with the positioning pass and the C# mirror follows, a round trip later on Server', async ({ page, mode }) => {
    // FLT-DOM-08 with ADR 01's "RND-STA-01 for mirrored fields" row: the attribute is the module's, written at frame
    // timing; C#'s mirror (rendered by the fixture as data-mirrored-side) follows the report.
    if (mode === 'server') await delayCircuit(page, 150);
    const container = await gotoPositionerCase(page, mode, 'positioning-lifecycle', 'scroll');
    const { positioner } = parts(container);
    await expect(positioner).toHaveAttribute('data-side', 'top');
    await expect(positioner).toHaveAttribute('data-mirrored-side', 'top');

    // The mirror's value when the module's flip lands (the record of the batch that wrote data-side).
    await positioner.evaluate((node) => {
      const element = node as HTMLElement;
      const witness: { atFlip?: string | null } = {};
      (element as unknown as { witness: typeof witness }).witness = witness;
      new MutationObserver(() => {
        if (witness.atFlip === undefined && element.getAttribute('data-side') === 'bottom') {
          witness.atFlip = element.getAttribute('data-mirrored-side');
        }
      }).observe(element, { attributes: true });
    });
    await container.locator('[data-scroller]').evaluate((node) => {
      node.scrollTop = 170;
    });

    await expect(positioner).toHaveAttribute('data-side', 'bottom');
    const witness = () => positioner.evaluate((node) => (node as unknown as { witness: { atFlip?: string | null } }).witness);
    if (mode === 'server') {
      // The module wrote the flip at once; the report needs a round trip of 300 ms before C# renders the mirror.
      expect(await witness()).toEqual({ atFlip: 'top' });
    }
    await expect(positioner).toHaveAttribute('data-mirrored-side', 'bottom');
  });

  test('port: scrolling an ancestor of the anchor re-positions the Positioner in the same frame on both modes', async ({ page, mode }) => {
    // FLT-POS-16 and FLT-ALG-13, met on both modes because the loop is JavaScript's (ADR 01 decision 2, relaxation table).
    const container = await gotoPositionerCase(page, mode, 'positioning-lifecycle', 'scroll');
    const { positioner } = parts(container);
    await expect.poll(() => inlineStyle(positioner, 'transform')).toMatch(/^translate\(/);
    const before = await inlineStyle(positioner, 'transform');

    // Read inside the scroll event's own dispatch, by a listener added after the module's: the module's listener starts
    // the pass and the microtask checkpoint that follows it commits the pass, before any frame paints. (Not a frame
    // callback: page.clock fakes requestAnimationFrame.)
    const inFrame = await container.locator('[data-scroller]').evaluate(
      (node, selector) =>
        new Promise<{ transform: string; side: string | null }>((resolve) => {
          const target = document.querySelector<HTMLElement>(selector)!;
          node.addEventListener('scroll', () => resolve({ transform: target.style.transform, side: target.getAttribute('data-side') }), { once: true });
          node.scrollTop = 40;
        }),
      '[data-fixture="positioning-lifecycle"] [data-testid="positioner"]',
    );

    expect(inFrame.transform).not.toBe(before);
    const translateY = (transform: string) => Number(/translate\([^,]+, (-?[\d.]+)px\)/.exec(transform)?.[1]);
    expect(translateY(before) - translateY(inFrame.transform)).toBe(40);
    expect(inFrame.side).toBe('top');
  });
});
