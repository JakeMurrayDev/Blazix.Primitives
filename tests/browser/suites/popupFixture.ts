import { type Locator, type Page } from '@playwright/test';
import { expect, Fixture, fixtureUrl, type RenderMode } from '../helpers';

// The fixture contract the shared popup suites read (ADR 06 Amendments 2026-10-27, design point 7 of M4 track 1): a page
// inheriting PopupSuiteFixtureBase takes the root's props from its query and renders the root's callbacks on its container
// (`data-open-changes`: space-separated `open:reason` entries; `data-open-change-completes`: space-separated booleans;
// `data-attached` once every handle attached; `data-sync` counting the sync button). Documented in tests/browser/CONTEXT.md.

/** A query option of a popup suite fixture. */
export type PopupFixtureOptions = Record<string, string | number | boolean | undefined>;

/** Opens a popup suite fixture with options and waits until it is interactive and attached. */
export async function gotoPopupFixture(page: Page, mode: RenderMode, name: string, options: PopupFixtureOptions = {}): Promise<Locator> {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(options)) {
    if (value !== undefined) {
      query.set(key, String(value));
    }
  }

  const search = query.toString();
  await page.goto(search ? `${fixtureUrl(mode, name)}?${search}` : fixtureUrl(mode, name), { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/**
 * A barrier: clicks the fixture's sync button and waits for C# to count it. Server processes one circuit's events in
 * order (ADR-04/1) and WebAssembly synchronously, so every event dispatched before has reached C# afterwards.
 */
// A click on a fixture's own control (the barrier, a phase, the root's mount, the handle's calls) is an outside press, which
// a family's dismissal answers by closing, where Base UI changes the same things through `rerender` and direct handle calls.
// A control carrying `data-key-control` counts a neutral keydown (Shift) instead, which no dismissal reads; track 1's
// test-only host, whose dismissal ignores outside presses, keeps the click.
function activateControl(element: Element): void {
  if (element.hasAttribute('data-key-control')) {
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', code: 'ShiftLeft', bubbles: true }));
  } else {
    (element as HTMLElement).click();
  }
}

/** Activates a fixture control: a neutral keydown when it carries `data-key-control`, else Playwright's click. */
export async function pressControl(control: Locator): Promise<void> {
  if ((await control.getAttribute('data-key-control')) !== null) {
    await control.evaluate(activateControl);
  } else {
    await control.click();
  }
}

export async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate(activateControl);
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

/** The root's recorded open changes, as `open:reason` entries. */
export async function openChanges(fixture: Locator): Promise<string[]> {
  return splitList(await fixture.getAttribute('data-open-changes'));
}

/** The root's recorded completions. */
export async function openChangeCompletes(fixture: Locator): Promise<string[]> {
  return splitList(await fixture.getAttribute('data-open-change-completes'));
}

/** Sets a controlled root's `open` through the fixture's `[data-set-open]` buttons, as the suite's rerender does. */
export async function setRootOpen(fixture: Locator, open: boolean): Promise<void> {
  await fixture.locator(`[data-set-open="${open}"]`).click();
}

/** What the page saw when an animation started on an element: the marker the animation titles assert (ADR 06 §2). */
export interface AnimationStartRecord {
  name: string;
  endingStyle: boolean;
  visible: boolean;
  completes: string;
}

/**
 * Records every `animationstart` in the page from now on, with the element's `data-ending-style`, its visibility and the
 * fixture's recorded completions at that moment, so a state that lasts one animation is witnessed by a record, never by a
 * poll of the live style.
 */
export async function recordAnimationStarts(page: Page, fixtureName: string): Promise<void> {
  await page.evaluate((name) => {
    const records: AnimationStartRecord[] = [];
    (window as unknown as { popupAnimationStarts: AnimationStartRecord[] }).popupAnimationStarts = records;
    document.addEventListener(
      'animationstart',
      (event) => {
        const target = event.target as HTMLElement;
        records.push({
          name: (event as AnimationEvent).animationName,
          endingStyle: target.hasAttribute('data-ending-style'),
          visible: target.checkVisibility(),
          completes: document.querySelector(`[data-fixture="${name}"]`)?.getAttribute('data-open-change-completes') ?? '',
        });
      },
      true,
    );
  }, fixtureName);
}

/** The animation starts recorded since recordAnimationStarts. */
export function animationStarts(page: Page): Promise<AnimationStartRecord[]> {
  return page.evaluate(() => (window as unknown as { popupAnimationStarts?: AnimationStartRecord[] }).popupAnimationStarts ?? []);
}

function splitList(value: string | null): string[] {
  return value ? value.split(' ').filter(Boolean) : [];
}
