import { type Locator, type Page } from '@playwright/test';
import type { RenderMode } from './test';

/** The attribute every fixture container renders (ADR 06 §2). */
export const Fixture = {
  container: (name: string) => `[data-fixture="${name}"]`,
  interactive: (name: string) => `[data-fixture="${name}"][data-interactive="true"]`,
  started: 'html[data-blazix-started]',
} as const;

export function fixtureUrl(mode: RenderMode, name: string): string {
  return `/${mode}/${name}`;
}

/**
 * Navigates to a fixture and waits for both readiness markers: `data-blazix-started` on `<html>` (set by the host's
 * JS initializer in `afterWebStarted`) and `data-interactive="true"` on the fixture container. Never waits for
 * `networkidle` (Blazor Server's SignalR connection is never idle) and never sleeps (ADR 06 §2).
 * Returns the container locator (every root of the fixture when it renders more than one).
 */
export async function gotoFixture(page: Page, mode: RenderMode, name: string): Promise<Locator> {
  await page.goto(fixtureUrl(mode, name), { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const container = page.locator(Fixture.container(name));
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  return container;
}
