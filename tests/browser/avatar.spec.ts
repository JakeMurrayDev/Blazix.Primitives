import { type Locator, type Route } from '@playwright/test';
import { test, expect, advanceTime, Fixture, fixtureUrl, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/avatar.md: AVA-ANI-01..06, AVA-DOM-03, AVA-EDGE-02, AVA-EDGE-03 and AVA-STA-14, on
// the Avatar parts over the `dom` and `transitions` modules, plus the fixtures' delay and substitute-callback pages.
// Titles are Base UI's (avatar/image/AvatarImage.test.tsx, avatar/fallback/AvatarFallback.test.tsx). Image requests are
// held or answered with `page.route`, so a load or an error happens when the test says. A recorder in the page takes a
// snapshot of the avatar at every mutation checkpoint (the end of each task that changed it), which is where the browser
// may paint: "no painted frame shows X" is asserted as "no snapshot shows X".
//
// Relaxation rows per mode (avatar.md §9, ADR 01): AVA-EDGE-02/03 are met on both modes by the convergent write;
// AVA-ANI-02 is met on both; AVA-ANI-03 on Server: the module hides the image at exit end and the removal follows the
// completion message, so a snapshot shows the image hidden before it is removed; AVA-STA-07 holds on both (the mirrored
// status lags a round trip on Server, which no assertion here depends on).

interface Snapshot {
  interactive: boolean;
  fallback: boolean;
  image: boolean;
  imageHidden: boolean;
  starting: boolean;
  ending: boolean;
  loading: boolean;
  error: boolean;
}

interface Recorder {
  snapshots: Snapshot[];
  imageGetAnimations: number;
}

declare global {
  interface Window {
    avatarRecorder: Recorder;
  }
}

// Installed before the page's scripts: snapshots of the first avatar root, and a count of getAnimations() calls on the
// image (the enter phase is never awaited, AVA-ANI-02).
function installRecorder(): void {
  const recorder: Recorder = { snapshots: [], imageGetAnimations: 0 };
  window.avatarRecorder = recorder;
  const visible = (element: Element | null) => element instanceof HTMLElement && !element.hidden && element.isConnected;
  const take = () => {
    const root = document.querySelector('[data-testid=root]');
    if (!root) {
      return;
    }
    const image = root.querySelector('[data-testid=image]');
    const snapshot: Snapshot = {
      interactive: root.closest('[data-fixture]')?.getAttribute('data-interactive') === 'true',
      fallback: visible(root.querySelector('[data-testid=fallback]')),
      image: visible(image),
      imageHidden: image instanceof HTMLElement && image.hidden,
      starting: image?.hasAttribute('data-starting-style') ?? false,
      ending: image?.hasAttribute('data-ending-style') ?? false,
      loading: image?.hasAttribute('data-loading') ?? false,
      error: image?.hasAttribute('data-error') ?? false,
    };
    const last = recorder.snapshots.at(-1);
    if (!last || JSON.stringify(last) !== JSON.stringify(snapshot)) {
      recorder.snapshots.push(snapshot);
    }
  };
  new MutationObserver(take).observe(document, { subtree: true, childList: true, attributes: true });
  const getAnimations = Element.prototype.getAnimations;
  if (getAnimations) {
    Element.prototype.getAnimations = function (this: Element, ...args: Parameters<typeof getAnimations>) {
      if (this.matches('[data-testid=image]')) {
        recorder.imageGetAnimations++;
      }
      return getAnimations.apply(this, args);
    };
  }
}

async function recorded(page: Page): Promise<Recorder> {
  return page.evaluate(() => window.avatarRecorder);
}

/** Opens a fixture as gotoFixture does, optionally with the animations switched on (`?animations=1`). */
async function gotoAvatar(page: Page, mode: RenderMode, name: string, animations = false): Promise<Locator> {
  await page.addInitScript(installRecorder);
  if (!animations) {
    return gotoFixture(page, mode, name);
  }
  await page.goto(`${fixtureUrl(mode, name)}?animations=1`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  return page.locator(Fixture.container(name));
}

/** Holds every request matching `url` until the test answers it. */
async function holdImages(page: Page, url: string | RegExp) {
  const held: Route[] = [];
  await page.route(url, (route) => {
    held.push(route);
  });
  const next = async () => {
    await expect.poll(() => held.length).toBeGreaterThan(0);
    return held.shift()!;
  };
  return {
    load: async () => (await next()).continue(),
    fail: async () => (await next()).fulfill({ status: 404, body: '' }),
  };
}

function parts(fixture: Locator) {
  return {
    image: fixture.locator('[data-testid=image]'),
    fallback: fixture.locator('[data-testid=fallback]'),
  };
}

/** The snapshots taken from the first one that matches `from` on. */
function after(snapshots: Snapshot[], from: (snapshot: Snapshot) => boolean): Snapshot[] {
  const index = snapshots.findIndex(from);
  return index < 0 ? [] : snapshots.slice(index);
}

test.describe('Avatar.Image animations', () => {
  test('triggers enter animation via data-starting-style when mounting', async ({ page, mode }) => {
    const fixture = await gotoAvatar(page, mode, 'avatar-preload', true);
    // No source: the module reports `error` without a probe once attached.
    await expect(fixture).toHaveAttribute('data-statuses', 'error');
    const { image } = parts(fixture);
    await expect(image).toHaveCount(0);

    await fixture.locator('[data-show-image]').click();

    await expect(image).toBeVisible();
    await expect(fixture).toHaveAttribute('data-statuses', /loaded$/);
    await expect(image).not.toHaveAttribute('data-starting-style');
    // The fade-in ran to its end without the module waiting for it.
    await expect(image).toHaveCSS('opacity', '1');
    const record = await recorded(page);
    // AVA-ANI-01, AVA-DOM-03: the image appeared with data-starting-style, which the next frame removed.
    expect(after(record.snapshots, (s) => s.image)[0]?.starting).toBe(true);
    // AVA-ANI-02 (met on both modes): the enter phase is not awaited, so no animation inspection ran.
    expect(record.imageGetAnimations).toBe(0);
  });

  test('applies data-ending-style before unmount', async ({ page, mode }) => {
    const fixture = await gotoAvatar(page, mode, 'avatar-preload', true);
    await expect(fixture).toHaveAttribute('data-statuses', 'error');
    const { image } = parts(fixture);
    await fixture.locator('[data-show-image]').click();
    await expect(image).toBeVisible();
    await expect(image).not.toHaveAttribute('data-starting-style');

    await fixture.locator('[data-hide-image]').click();

    await expect(image).toHaveCount(0);
    const exit = after((await recorded(page)).snapshots, (s) => s.ending);
    // AVA-ANI-03, AVA-DOM-03, AVA-STA-08: the image showed data-ending-style before it was removed.
    expect(exit.length).toBeGreaterThan(0);
    expect(exit[0]!.image).toBe(true);
    if (mode === 'server') {
      // ADR 01's TRN-ANI-03 row: the module hides the image at exit end; the removal follows the completion message.
      expect(exit.some((s) => s.imageHidden)).toBe(true);
    }
  });

  test('does not apply the not-loaded state attributes without keepMounted', async ({ page, mode }) => {
    const fixture = await gotoAvatar(page, mode, 'avatar-preload', true);
    await expect(fixture).toHaveAttribute('data-statuses', 'error');
    const { image } = parts(fixture);
    await fixture.locator('[data-show-image]').click();
    await expect(image).not.toHaveAttribute('data-starting-style');

    await fixture.locator('[data-missing-image]').click();

    await expect(image).toHaveCount(0);
    const exit = after((await recorded(page)).snapshots, (s) => s.ending);
    expect(exit.length).toBeGreaterThan(0);
    // AVA-DOM-04: no data-loading or data-error while the preload image animates out.
    expect(exit.some((s) => s.loading || s.error)).toBe(false);
  });

  test('does not apply data-ending-style with keepMounted', async ({ page, mode }) => {
    const fixture = await gotoAvatar(page, mode, 'avatar-kept-mounted', true);
    const { image } = parts(fixture);
    await expect(fixture).toHaveAttribute('data-statuses', /loaded$/);
    await expect(image).not.toHaveAttribute('data-loading');

    await fixture.locator('[data-missing-image]').click();

    // The new source is derived as `loading` in the render that applies it; the 404 then reports `error`.
    await expect(fixture).toHaveAttribute('data-statuses', /loaded,loading,error$/);
    await expect(image).toHaveAttribute('data-error');
    const snapshots = after((await recorded(page)).snapshots, (s) => s.loading);
    // AVA-ANI-05, AVA-STA-07, AVA-DOM-03: a kept image never shows data-ending-style; data-loading carries the state.
    expect(snapshots.length).toBeGreaterThan(0);
    expect((await recorded(page)).snapshots.some((s) => s.ending)).toBe(false);
  });
});

test.describe('Avatar.Fallback', () => {
  test('keeps only one of image or fallback mounted when switching to image', async ({ page, mode }) => {
    const fixture = await gotoAvatar(page, mode, 'avatar-preload', true);
    await expect(fixture).toHaveAttribute('data-statuses', 'error');
    const { image, fallback } = parts(fixture);
    await expect(fallback).toBeVisible();

    await fixture.locator('[data-show-image]').click();

    await expect(fallback).toHaveCount(0);
    await expect(image).toBeVisible();
    // AVA-ANI-06: no snapshot shows the appearing image and the fallback together.
    expect((await recorded(page)).snapshots.some((s) => s.image && s.fallback)).toBe(false);
  });

  // Fixture page of the AVA-ANI-07 rows (L2): the delay runs under the C# clock the spec advances.
  test('shows the fallback when the delay has elapsed', async ({ page, mode }) => {
    const fixture = await gotoAvatar(page, mode, 'avatar-delay');
    const { fallback } = parts(fixture);
    await expect(fallback).toHaveCount(0);

    await advanceTime(page, fixture, 299);
    await expect(fallback).toHaveCount(0);
    await advanceTime(page, fixture, 1);

    await expect(fallback).toHaveText('JD');
  });
});

test.describe('Avatar.Image keepMounted', () => {
  test('derives the status from the rendered element load event', async ({ page, mode }) => {
    const images = await holdImages(page, /\/avatar\.png\?kept=0$/);
    const fixture = await gotoAvatar(page, mode, 'avatar-kept-mounted');
    await expect(fixture).toHaveAttribute('data-statuses', 'loading');

    await images.load();

    await expect(fixture).toHaveAttribute('data-statuses', 'loading,loaded');
    await expect(parts(fixture).fallback).toHaveCount(0);
  });

  test('keeps the image mounted when it fails to load', async ({ page, mode }) => {
    const images = await holdImages(page, /\/avatar\.png\?kept=0$/);
    const fixture = await gotoAvatar(page, mode, 'avatar-kept-mounted');
    await expect(fixture).toHaveAttribute('data-statuses', 'loading');

    await images.fail();

    await expect(fixture).toHaveAttribute('data-statuses', 'loading,error');
    const { image, fallback } = parts(fixture);
    await expect(image).toHaveAttribute('data-error');
    await expect(image).toBeAttached();
    await expect(fallback).toHaveText('JD');
  });

  test('calls the user onError handler', async ({ page, mode }) => {
    const images = await holdImages(page, /\/avatar\.png\?kept=0$/);
    const fixture = await gotoAvatar(page, mode, 'avatar-kept-mounted');
    await expect(fixture).toHaveAttribute('data-statuses', 'loading');

    await images.fail();

    await expect(fixture).toHaveAttribute('data-error-calls', '1');
    await expect(parts(fixture).fallback).toHaveText('JD');
  });

  test('calls the user onLoad handler', async ({ page, mode }) => {
    const images = await holdImages(page, /\/avatar\.png\?kept=0$/);
    const fixture = await gotoAvatar(page, mode, 'avatar-kept-mounted');
    await expect(fixture).toHaveAttribute('data-statuses', 'loading');

    await images.load();

    await expect(fixture).toHaveAttribute('data-load-calls', '1');
    await expect(parts(fixture).fallback).toHaveCount(0);
  });

  test('lets a user handler prevent the status update', async ({ page, mode }) => {
    const images = await holdImages(page, /\/avatar\.png\?kept=0$/);
    const fixture = await gotoAvatar(page, mode, 'avatar-kept-mounted');
    await expect(fixture).toHaveAttribute('data-statuses', 'loading');
    await fixture.locator('[data-prevent-button]').click();
    await expect(fixture).toHaveAttribute('data-prevent', 'true');

    await images.load();

    await expect(fixture).toHaveAttribute('data-load-calls', '1');
    const { image, fallback } = parts(fixture);
    await expect(image).toHaveAttribute('data-loading');
    await expect(fallback).toHaveText('JD');
    await expect(fixture).toHaveAttribute('data-statuses', 'loading');
  });

  // Fixture page of AVA-STA-13 / AVA-EVT-03 (L2): a substitute callback leaves the part no element to derive from.
  test('keeps the status reported by an element that does not forward a ref', async ({ page, mode }) => {
    const fixture = await gotoAvatar(page, mode, 'avatar-render-function');
    const { image, fallback } = parts(fixture);
    await expect(fallback).toHaveCount(0);

    await fixture.locator('[data-restyle]').click();

    await expect(image).toHaveClass('updated');
    await expect(fallback).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-statuses', 'loaded');
  });
});

test.describe('Avatar server rendering', () => {
  // AVA-EDGE-02 (met on both modes by the convergent write).
  test('does not flash fallback for a cached image during SSR hydration', async ({ page, mode }) => {
    // Warm the browser's cache with the image the fixture loads.
    await page.goto('/avatar.png');
    const html = await (await page.request.get(fixtureUrl(mode, 'avatar-prerender-preload'))).text();
    expect(html).toContain('data-testid="fallback"');
    expect(html).not.toMatch(/<img[^>]*data-testid="image"/);

    const fixture = await gotoAvatar(page, mode, 'avatar-prerender-preload');
    const { image, fallback } = parts(fixture);

    await expect(image).toBeVisible();
    await expect(fallback).toHaveCount(0);
    await expect(fixture).toHaveAttribute('data-statuses', /loaded$/);
    const snapshots = (await recorded(page)).snapshots;
    // No snapshot shows the image and the fallback together, and once the image is shown the fallback never returns.
    expect(snapshots.some((s) => s.image && s.fallback)).toBe(false);
    expect(after(snapshots, (s) => s.image).some((s) => s.fallback)).toBe(false);
  });

  // AVA-EDGE-03 (met on both modes by the convergent write), AVA-ARIA-01, AVA-DOM-02.
  test('renders the image in the server HTML and resolves cached images on hydration', async ({ page, mode }) => {
    const html = await (await page.request.get(fixtureUrl(mode, 'avatar-prerender-kept-mounted'))).text();
    const serverImage = html.match(/<img[^>]*data-testid="image"[^>]*>/)?.[0] ?? '';
    expect(serverImage).toContain('src="/avatar.png"');
    expect(serverImage).toContain('aria-hidden="true"');
    expect(serverImage).not.toContain('data-loading');
    expect(serverImage).not.toContain('data-error');
    expect(html).toContain('data-testid="fallback"');

    const fixture = await gotoPrerenderedAfterImageLoad(page, mode, 'avatar-prerender-kept-mounted');
    const { image, fallback } = parts(fixture);

    await expect(fallback).toHaveCount(0);
    await expect(image).not.toHaveAttribute('aria-hidden');
    await expect(image).toHaveAttribute('src', '/avatar.png');
    await expect(fixture).toHaveAttribute('data-statuses', 'loaded');
  });

  // AVA-ANI-04, AVA-EDGE-03 (TRN-ANI-35).
  test('does not replay the enter animation for a cached image on hydration', async ({ page, mode }) => {
    const fixture = await gotoPrerenderedAfterImageLoad(page, mode, 'avatar-prerender-kept-mounted', true);
    const { image, fallback } = parts(fixture);

    await expect(fallback).toHaveCount(0);
    await expect(image).not.toHaveAttribute('aria-hidden');
    const snapshots = (await recorded(page)).snapshots;
    expect(snapshots.some((s) => s.starting)).toBe(false);
  });
});

/**
 * Opens a prerendered fixture and lets it become interactive only once its server-rendered image has loaded: the
 * request that starts the interactive runtime (the circuit's negotiation on Server, the framework's scripts on
 * WebAssembly) is held until then, as Base UI's tests wait for `complete` before hydrating.
 */
async function gotoPrerenderedAfterImageLoad(page: Page, mode: RenderMode, name: string, animations = false): Promise<Locator> {
  await page.addInitScript(installRecorder);
  const held: Route[] = [];
  const pattern = mode === 'server' ? /\/_blazor\/negotiate/ : /\/_framework\//;
  let release = false;
  await page.route(pattern, (route) => (release ? route.continue() : held.push(route)));

  await page.goto(`${fixtureUrl(mode, name)}${animations ? '?animations=1' : ''}`, { waitUntil: 'commit' });
  await expect
    .poll(() => page.evaluate(() => {
      const image = document.querySelector<HTMLImageElement>('[data-testid=image]');
      return image !== null && image.complete && image.naturalWidth > 0;
    }))
    .toBe(true);
  release = true;
  for (const route of held.splice(0)) {
    await route.continue();
  }

  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  return page.locator(Fixture.container(name));
}
