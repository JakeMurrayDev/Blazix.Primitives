import type { Page } from '@playwright/test';

// The positioner's transitions from now on, each recorded at its `transitionrun` (issue 175; shared by popover.spec.ts and
// menu.popups.spec.ts, issue 261): a switch between detached triggers moves the positioner (`data-testid="positioner"`),
// and its transitions are the ones a trigger-change callback waits on.
export async function recordSwitchTransitions(page: Page): Promise<void> {
  await page.evaluate(() => {
    const recorded: Animation[] = [];
    (window as unknown as { switchTransitions: Animation[] }).switchTransitions = recorded;
    document.addEventListener('transitionrun', (event) => {
      const target = event.target as Element;
      if (target.getAttribute('data-testid') !== 'positioner') {
        return;
      }
      const property = (event as TransitionEvent).propertyName;
      for (const animation of target.getAnimations()) {
        if ((animation as CSSTransition).transitionProperty === property && !recorded.includes(animation)) {
          recorded.push(animation);
        }
      }
    }, true);
  });
}

export const switchTransitions = (page: Page) => page.evaluate(() => (window as unknown as { switchTransitions: Animation[] }).switchTransitions.length);

// Finishes every recorded switch transition still running and waits until each has settled. A canceled one (a second
// positioning pass replaced it) is skipped: canceling replaces an animation's `finished` promise with a new pending one,
// which never settles (issue 261).
export const finishSwitchTransitions = (page: Page) => page.evaluate(async () => {
  const running = (window as unknown as { switchTransitions: Animation[] }).switchTransitions.filter(
    (animation) => animation.playState === 'running' || animation.playState === 'paused',
  );
  for (const animation of running) {
    animation.finish();
  }
  await Promise.allSettled(running.map((animation) => animation.finished));
});
