// L4 (ADR 06 §2): ScrollArea's rendered DOM that only the browser shows, on both render modes (docs/components/scroll-area.md
// §5, §12): the registration of the overflow variables (SCR-DOM-12, design point 9 (a) of the ScrollArea track) and the
// prerendered markup before and after interactivity (SCR-EDGE-13, SCR-STA-08).
import { type Locator, type Route } from '@playwright/test';
import { expect, Fixture, fixtureUrl, test, type Page, type RenderMode } from './helpers';
import { attributeFlags, computed, gotoParts, inlineStyle, scrollViewport } from './scroll-area.helpers';

const OVERFLOW_Y_START = '--scroll-area-overflow-y-start';

const OVERFLOW_ATTRIBUTES = [
  'data-has-overflow-x',
  'data-has-overflow-y',
  'data-overflow-x-start',
  'data-overflow-x-end',
  'data-overflow-y-start',
  'data-overflow-y-end',
] as const;

/** Scrolls the viewport 100 px down and waits for the module's measurement to write the variable. */
async function scrolledTo100(page: Page): Promise<{ viewport: Locator; inner: Locator }> {
  const viewport = page.getByTestId('viewport');
  const inner = page.getByTestId('inner');
  await scrollViewport(page, { top: 100 });
  await expect.poll(() => inlineStyle(viewport, OVERFLOW_Y_START)).toBe('100px');
  return { viewport, inner };
}

/** Whether registering the property again throws, which it does once the page has registered it. */
function registered(page: Page, name: string): Promise<boolean> {
  return page.evaluate((property) => {
    try {
      CSS.registerProperty({ name: property, syntax: '<length>', inherits: false, initialValue: '0px' });
      return false;
    } catch {
      return true;
    }
  }, name);
}

test.describe('ScrollArea: overflow variable registration', () => {
  // SCR-DOM-12: ScrollAreaViewport.tsx:31-69 registers the four variables once, `inherits: false`, initial 0px.
  test('port: the overflow variables are registered as non-inheriting, so a descendant reads 0px unless it opts in with inherit', async ({ page, mode }) => {
    await gotoParts(page, mode, {});
    const { viewport, inner } = await scrolledTo100(page);

    const content = page.getByTestId('content');
    expect((await computed(viewport, OVERFLOW_Y_START)).trim()).toBe('100px');
    expect((await computed(content, OVERFLOW_Y_START)).trim()).toBe('0px');
    expect((await computed(inner, OVERFLOW_Y_START)).trim()).toBe('0px');

    // Each level opts in: the Content (the Viewport's child), then the element inside it.
    const optIn = (element: Element, name: string) => (element as HTMLElement).style.setProperty(name, 'inherit');
    await content.evaluate(optIn, OVERFLOW_Y_START);
    expect((await computed(content, OVERFLOW_Y_START)).trim()).toBe('100px');
    expect((await computed(inner, OVERFLOW_Y_START)).trim()).toBe('0px');
    await inner.evaluate(optIn, OVERFLOW_Y_START);
    expect((await computed(inner, OVERFLOW_Y_START)).trim()).toBe('100px');
    expect(await registered(page, OVERFLOW_Y_START)).toBe(true);
  });

  // SCR-DOM-12's WebKit clause: the registration is skipped there, so the variables inherit as any custom property does.
  // The platform reads the engine once, when js/platform.ts loads (WebKit is `CSS.supports('-webkit-backdrop-filter:none')`),
  // so an init script answers that check yes before any script runs (menu.filter-webkit.spec.ts's deviation).
  test('port: on WebKit the overflow variables are not registered, so a descendant inherits the value', async ({ page, mode }) => {
    await page.addInitScript(() => {
      const supports = CSS.supports.bind(CSS);
      CSS.supports = ((...args: unknown[]) =>
        String(args[0]).includes('-webkit-backdrop-filter') ? true : (supports as (...a: unknown[]) => boolean)(...args)) as typeof CSS.supports;
    });
    await gotoParts(page, mode, {});
    const { viewport, inner } = await scrolledTo100(page);

    expect((await computed(viewport, OVERFLOW_Y_START)).trim()).toBe('100px');
    expect((await computed(page.getByTestId('content'), OVERFLOW_Y_START)).trim()).toBe('100px');
    expect((await computed(inner, OVERFLOW_Y_START)).trim()).toBe('100px');
    expect(await registered(page, OVERFLOW_Y_START)).toBe(false);
  });
});

/**
 * Opens the prerendered fixture with the request that starts the interactive runtime held (the circuit's negotiation on
 * Server, the framework's scripts on WebAssembly; avatar.spec.ts's hold), so the page shows the server-rendered markup
 * until `release` is called.
 */
async function openHeld(page: Page, mode: RenderMode, name: string): Promise<{ release: () => Promise<void> }> {
  const held: Route[] = [];
  let released = false;
  await page.route(mode === 'server' ? /\/_blazor\/negotiate/ : /\/_framework\//, (route) => (released ? route.continue() : held.push(route)));
  await page.goto(fixtureUrl(mode, name), { waitUntil: 'commit' });
  await page.locator(Fixture.container(name)).waitFor({ state: 'attached' });
  return {
    release: async () => {
      released = true;
      for (const route of held.splice(0)) {
        await route.continue();
      }
    },
  };
}

test.describe('ScrollArea: prerendering', () => {
  // SCR-EDGE-13, SCR-STA-08: the server-rendered markup is the unmeasured state; the measurement updates it after
  // interactivity.
  test('port: the prerendered markup is the unmeasured state, and the measurement updates it once interactive', async ({ page, mode }) => {
    const name = 'scroll-area-prerender';
    const { release } = await openHeld(page, mode, name);
    const fixture = page.locator(Fixture.container(name));
    const root = page.getByTestId('root');
    const viewport = page.getByTestId('viewport');
    const verticalBar = page.getByTestId('scrollbar-vertical');
    const verticalThumb = page.getByTestId('thumb-vertical');

    await expect(fixture).toHaveAttribute('data-interactive', 'false');
    // The Root: role, position and the corner variables at 0px (SCR-ARIA-01, SCR-DOM-01, SCR-DOM-02).
    await expect(root).toHaveAttribute('role', 'presentation');
    expect(await computed(root, 'position')).toBe('relative');
    expect((await computed(root, '--scroll-area-corner-height')).trim()).toBe('0px');
    expect((await computed(root, '--scroll-area-corner-width')).trim()).toBe('0px');
    // The scrollbar-hiding style element, right before the Root (SCR-DOM-05).
    expect(await root.evaluate((element) => {
      const style = element.previousElementSibling;
      return style?.tagName === 'STYLE' && (style.textContent ?? '').includes('.base-ui-disable-scrollbar');
    })).toBe(true);
    // The Viewport: out of the tab order, scrollable, its scrollbars hidden, with no overflow state (SCR-DOM-08, SCR-DOM-07).
    await expect(viewport).toHaveAttribute('tabindex', '-1');
    expect(await computed(viewport, 'overflow')).toBe('scroll');
    await expect(viewport).toHaveClass(/(^|\s)base-ui-disable-scrollbar(\s|$)/);
    for (const part of [root, viewport, page.getByTestId('content')]) {
      expect(Object.values(await attributeFlags(part, OVERFLOW_ATTRIBUTES)).some(Boolean)).toBe(false);
    }
    expect(await viewport.getAttribute('style')).not.toContain('--scroll-area-overflow');
    // The scrollbar that is not kept mounted and the Corner are absent (SCR-DOM-15, SCR-DOM-26).
    await expect(page.getByTestId('scrollbar-horizontal')).toHaveCount(0);
    await expect(page.getByTestId('corner')).toHaveCount(0);
    // The kept-mounted scrollbar is present with a 0px thumb, its Thumb hidden (SCR-DOM-20, SCR-DOM-23).
    expect(await inlineStyle(verticalBar, '--scroll-area-thumb-height')).toBe('0px');
    expect(await computed(verticalThumb, 'visibility')).toBe('hidden');

    await release();
    await page.locator(Fixture.started).waitFor({ state: 'attached' });
    await page.locator(Fixture.interactive(name)).waitFor({ state: 'attached' });
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    await expect(fixture).toHaveAttribute('data-measured', 'true');

    await expect(root).toHaveAttribute('data-has-overflow-x');
    await expect(root).toHaveAttribute('data-has-overflow-y');
    await expect(viewport).toHaveAttribute('tabindex', '0');
    await expect.poll(() => inlineStyle(viewport, '--scroll-area-overflow-y-end')).toBe('800px');
    await expect(page.getByTestId('scrollbar-horizontal')).toHaveCount(1);
    await expect(page.getByTestId('corner')).toHaveCount(1);
    await expect.poll(() => inlineStyle(page.getByTestId('corner'), 'width')).toBe('10px');
    await expect.poll(() => inlineStyle(verticalBar, '--scroll-area-thumb-height')).toBe('38px');
    await expect.poll(() => computed(verticalThumb, 'visibility')).toBe('visible');
    expect((await computed(root, '--scroll-area-corner-width')).trim()).toBe('10px');
  });
});
