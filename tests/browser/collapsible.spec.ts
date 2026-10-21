import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, Fixture, readDiagnostics, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/collapsible.md on the Collapsible parts over the `transitions` module (Root, Trigger)
// and the `collapsible` component module (Panel). Titles are Base UI's (collapsible/root/CollapsibleRoot.test.tsx,
// collapsible/panel/CollapsiblePanel.test.tsx). Every test runs on both render modes; a row in ADR 01's relaxation table
// asserts its Server substitute on `server` (§9 "Server relaxations"). Animated fixtures open with `?animations=1`
// (ADR 06 §2: the host disables the animations watcher otherwise), as Base UI's tests that clear
// BASE_UI_ANIMATIONS_DISABLED do. No sleeps and no frame counting: a state that lasts a few frames is witnessed by a
// marker the page records (a mutation record, an `animationstart` or `transitionrun` event), a lasting one by `expect`.

type FixtureName =
  | 'collapsible-basic'
  | 'collapsible-css-transition'
  | 'collapsible-css-animation'
  | 'collapsible-render-function'
  | 'collapsible-initially-open'
  | 'collapsible-reattach'
  | 'collapsible-hidden-until-found';

/** Opens a Collapsible fixture and waits until every part has its behavior attached. */
async function gotoCollapsible(page: Page, mode: RenderMode, name: FixtureName, options: { animations?: boolean } = {}): Promise<Locator> {
  const url = options.animations ? `${fixtureUrl(mode, name)}?animations=1` : fixtureUrl(mode, name);
  await page.goto(url, { waitUntil: 'commit' });
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
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

function collapsibleCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    root: container.locator('[data-root]'),
    trigger: container.locator('button:not([data-before]):not([data-external]), [role="button"]').first(),
    panel: container.locator('[data-panel]'),
    content: container.getByText('This is panel content'),
    before: container.locator('[data-before]'),
    external: container.locator('[data-external]'),
  };
}

/** One motion event or ending-style change of a panel, with the panel's state when it was recorded. */
interface MotionRecord {
  kind: 'animationstart' | 'transitionrun' | 'ending';
  name: string;
  open: boolean;
  closed: boolean;
  height: string;
  transitionDuration: string;
}

/**
 * Records, from now on, the `animationstart` and `transitionrun` events of the case's panel and each time it gains
 * `data-ending-style`, each with the panel's state at that moment. A motion that lasts 100 ms can be over before any
 * read the test makes when the runner is slow, and `getAnimations()` no longer lists a finished animation; its start
 * event and the attribute's mutation record are still delivered (ADR 06 §2: wait on markers, never on samples). The
 * records live on the case container, so they outlive a panel that its close removes or that a re-attach replaces.
 */
async function recordMotion(container: Locator): Promise<void> {
  await container.evaluate((node) => {
    const records: unknown[] = [];
    (node as unknown as { motion: unknown[] }).motion = records;
    const record = (kind: string, name: string, target: EventTarget | null) => {
      if (!(target instanceof HTMLElement) || !target.matches('[data-panel]')) {
        return;
      }
      records.push({
        kind,
        name,
        open: target.hasAttribute('data-open'),
        closed: target.hasAttribute('data-closed'),
        height: target.style.getPropertyValue('--collapsible-panel-height'),
        transitionDuration: target.style.transitionDuration,
      });
    };
    node.addEventListener('animationstart', (event) => record('animationstart', (event as AnimationEvent).animationName, event.target));
    node.addEventListener('transitionrun', (event) => record('transitionrun', (event as TransitionEvent).propertyName, event.target));
    new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        if (mutation.oldValue === null && mutation.target instanceof HTMLElement && mutation.target.hasAttribute('data-ending-style')) {
          record('ending', '', mutation.target);
        }
      }
    }).observe(node, { subtree: true, attributes: true, attributeOldValue: true, attributeFilter: ['data-ending-style'] });
  });
}

/** The records of {@link recordMotion}. */
function motion(container: Locator): Promise<MotionRecord[]> {
  return container.evaluate((node) => (node as unknown as { motion: MotionRecord[] }).motion);
}

/** Whether a recorded animation named `name` started while the panel was open (`'open'`) or closed (`'closed'`). */
async function animationStarted(container: Locator, name: string, side: 'open' | 'closed'): Promise<boolean> {
  return (await motion(container)).some((record) => record.kind === 'animationstart' && record.name === name && record[side]);
}

/**
 * Records, from now on, whether the case's panel was ever seen carrying `data-starting-style` together with `data-open`:
 * when it is inserted, or when a record shows the starting style taken off an open panel. The attribute can be added
 * and removed within one frame (the module attaches and settles before the next one), so a mutation record is the
 * only reliable witness; the result is read from the container's `data-seen-starting` attribute.
 */
async function recordStartingStyle(container: Locator): Promise<void> {
  await container.evaluate((node) => {
    const mark = (panel: Element, hadStartingStyle: boolean) => {
      if (hadStartingStyle && panel.hasAttribute('data-open')) {
        (node as HTMLElement).dataset.seenStarting = 'true';
      }
    };
    new MutationObserver((records) => {
      for (const record of records) {
        if (record.type === 'childList') {
          record.addedNodes.forEach((added) => {
            if (added instanceof Element && added.matches('[data-panel]')) {
              mark(added, added.hasAttribute('data-starting-style'));
            }
          });
        } else if (record.target instanceof Element && record.target.matches('[data-panel]') && record.attributeName === 'data-starting-style') {
          mark(record.target, record.oldValue !== null || record.target.hasAttribute('data-starting-style'));
        }
      }
    }).observe(node, { subtree: true, childList: true, attributes: true, attributeOldValue: true, attributeFilter: ['data-starting-style'] });
  });
}

test.describe('<Collapsible.Root />', () => {
  test.describe('open state', () => {
    test('controlled trigger presses request open and close state changes', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
      const { trigger, content } = collapsibleCase(fixture, 'controlled');

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(content).toHaveCount(0);

      await trigger.click();
      await expect(trigger).toHaveAttribute('aria-expanded', 'true');
      await expect(content).toHaveCount(1);

      await trigger.click();
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(content).toHaveCount(0);
    });

    test('does not change controlled open state without an external update', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
      const { container, trigger, content } = collapsibleCase(fixture, 'ignored');

      await trigger.click();
      await settle(fixture);

      await expect(container).toHaveAttribute('data-calls', '1');
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(content).toHaveCount(0);
    });

    test('controlled mode', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
      const { container, trigger, content, external } = collapsibleCase(fixture, 'controlled');

      await expect(trigger).not.toHaveAttribute('aria-controls');
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(content).toHaveCount(0);

      await external.click();

      await expect(trigger).toHaveAttribute('aria-expanded', 'true');
      await expect(trigger).toHaveAttribute('aria-controls');
      // COL-DOM-20: the content is visible in the batch that sets aria-expanded.
      await expect(content).toBeVisible();
      await expect(content).toHaveAttribute('data-open');
      await expect(trigger).toHaveAttribute('data-panel-open');

      await external.click();

      await expect(trigger).not.toHaveAttribute('aria-controls');
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(content).toHaveCount(0);
      // COL-EVT-06: a prop change calls no OnOpenChange.
      await settle(fixture);
      await expect(container).toHaveAttribute('data-calls', '0');
    });

    test('uncontrolled mode', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
      const { trigger, content } = collapsibleCase(fixture, 'uncontrolled');

      await expect(trigger).not.toHaveAttribute('aria-controls');
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(content).toHaveCount(0);

      await trigger.click();

      await expect(trigger).toHaveAttribute('aria-expanded', 'true');
      await expect(trigger).toHaveAttribute('aria-controls');
      await expect(content).toBeVisible();
      await expect(content).toHaveAttribute('data-open');
      await expect(trigger).toHaveAttribute('data-panel-open');

      await trigger.click();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(trigger).not.toHaveAttribute('aria-controls');
      await expect(trigger).not.toHaveAttribute('data-panel-open');
      await expect(content).toHaveCount(0);
    });
  });

  test.describe('keyboard interactions', () => {
    for (const key of ['Enter', 'Space']) {
      test(`key: ${key} does not toggle or call onOpenChange when disabled`, async ({ page, mode }) => {
        const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
        const { container, trigger, content, before } = collapsibleCase(fixture, 'disabled');

        await before.focus();
        await page.keyboard.press('Tab');
        await expect(trigger).not.toBeFocused();

        await page.keyboard.press(key);
        await settle(fixture);

        await expect(container).toHaveAttribute('data-calls', '0');
        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(content).toHaveCount(0);
      });

      test(`key: ${key} should toggle the Collapsible`, async ({ page, mode }) => {
        const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
        const { trigger, content, before } = collapsibleCase(fixture, 'uncontrolled');

        await expect(trigger).not.toHaveAttribute('aria-controls');
        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(content).toHaveCount(0);

        await before.focus();
        await page.keyboard.press('Tab');
        await expect(trigger).toBeFocused();
        await page.keyboard.press(key);

        await expect(trigger).toHaveAttribute('aria-controls');
        await expect(trigger).toHaveAttribute('aria-expanded', 'true');
        await expect(trigger).toHaveAttribute('data-panel-open');
        await expect(content).toBeVisible();
        await expect(content).toHaveAttribute('data-open');

        await page.keyboard.press(key);

        await expect(trigger).not.toHaveAttribute('aria-controls');
        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(trigger).not.toHaveAttribute('data-panel-open');
        await expect(content).toHaveCount(0);
      });
    }
  });
});

/** Reads a computed style property of the element. */
function computed(element: Locator, property: string): Promise<string> {
  return element.evaluate((node, name) => getComputedStyle(node).getPropertyValue(name), property);
}

/** Counts the element's animations that have not finished. */
function runningAnimations(element: Locator): Promise<number> {
  return element.evaluate((node) => node.getAnimations().filter((animation) => animation.playState !== 'finished').length);
}

/**
 * Dispatches a synthetic `beforematch`, as Base UI's tests do (`fireBeforeMatch`). With `removeHidden`, the attribute is
 * then dropped in the same task, as the browser does when it reveals a match.
 */
async function fireBeforeMatch(panel: Locator, options: { removeHidden?: boolean } = {}): Promise<void> {
  await panel.evaluate((node, removeHidden) => {
    node.dispatchEvent(new Event('beforematch', { bubbles: true, cancelable: false }));
    if (removeHidden) {
      node.removeAttribute('hidden');
    }
  }, options.removeHidden ?? false);
}

test.describe('Collapsible details and relaxations', () => {
  test('a keyboard activation reports the click it produced', async ({ page, mode }) => {
    // COL-EVT-02's keyboard clause: the native trigger's Enter is the browser's click (detail 0); the non-native trigger's
    // Enter is the click the button module dispatches.
    const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
    for (const name of ['uncontrolled', 'non-native']) {
      const { container, before } = collapsibleCase(fixture, name);
      const trigger = container.getByRole('button', { name: 'Trigger' });
      await before.focus();
      await page.keyboard.press('Tab');
      await expect(trigger).toBeFocused();
      await page.keyboard.press('Enter');

      await expect(container).toHaveAttribute('data-calls', '1');
      await expect(container).toHaveAttribute('data-last-reason', 'trigger-press');
      await expect(container).toHaveAttribute('data-last-event', /^MouseEvent:click:detail=0$/);
      await expect(container).toHaveAttribute('data-last-trigger', 'none');
    }
  });

  test('a click on a disabled non-native trigger has its default prevented', async ({ page, mode }) => {
    // COL-PTR-02's non-native clause: the button module prevents the click's and the pointerdown's default.
    const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
    const { container } = collapsibleCase(fixture, 'non-native-disabled');
    const trigger = container.getByRole('button', { name: 'Trigger' });
    await container.evaluate((node) => {
      for (const type of ['click', 'pointerdown']) {
        node.addEventListener(type, (event) => {
          (node as HTMLElement).dataset[`${type}Prevented`] = String(event.defaultPrevented);
        });
      }
    });

    await trigger.click({ force: true });
    await settle(fixture);

    await expect(container).toHaveAttribute('data-click-prevented', 'true');
    await expect(container).toHaveAttribute('data-pointerdown-prevented', 'true');
    await expect(container).toHaveAttribute('data-calls', '0');
  });

  test('the class function sees the transition status once the root mirrors it', async ({ page, mode }) => {
    // COL-STA-07 (ADR 01, RND-STA-01 for mirrored fields): the open render evaluates 'starting'; the 'idle' the module
    // flips on the next frame reaches the state, and the class, when its report arrives (a round trip later on Server).
    const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
    const { container, trigger } = collapsibleCase(fixture, 'uncontrolled');
    const root = container.locator('[data-root]');
    await expect(root).toHaveClass(/status-none/);

    await trigger.click();

    await expect(root).toHaveClass(/status-idle/);
    await expect(root).not.toHaveAttribute('data-starting-style');
  });
});

test.describe('<Collapsible.Trigger />', () => {
  test('removes a disabled non-native trigger from the tab order', async ({ page, mode }) => {
    const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
    const { container, before } = collapsibleCase(fixture, 'non-native-disabled');
    const trigger = container.getByRole('button', { name: 'Trigger' });

    await expect(trigger).toHaveAttribute('aria-disabled', 'true');
    await expect(trigger).toHaveAttribute('tabindex', '-1');
    await expect(trigger).not.toHaveAttribute('disabled');

    await before.focus();
    await page.keyboard.press('Tab');
    await expect(trigger).not.toBeFocused();
  });
});

test.describe('<Collapsible.Panel />', () => {
  test('warns when hiddenUntilFound overrides keepMounted={false}', async ({ page, mode }) => {
    const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found');
    const { content } = collapsibleCase(fixture, 'keep-mounted-false');

    await expect(content).toHaveAttribute('hidden', 'until-found');
    const diagnostics = await readDiagnostics(page, mode, fixture);
    expect(diagnostics.map((entry) => entry.message)).toContain(
      'Blazix: The `KeepMounted` parameter set to `false` on `CollapsiblePanel` is ignored when `HiddenUntilFound` is enabled, since the panel must remain mounted while closed.',
    );
  });

  test.describe('prop: keepMounted', () => {
    test('hides the panel after external controlled close when true and no animations are applied', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-basic');
      const { trigger, panel, external } = collapsibleCase(fixture, 'keep-mounted');

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(panel).toHaveAttribute('hidden');
      await expect(panel).toHaveAttribute('data-closed');
      await expect(panel).not.toHaveAttribute('data-ending-style');

      // COL-STA-06: a 'none' open is forced idle, so the panel never shows data-starting-style once its module ran.
      let sawStartingStyle = false;
      await external.click();
      await expect(trigger).toHaveAttribute('aria-expanded', 'true');
      await expect(panel).not.toHaveAttribute('hidden');
      await expect(panel).toHaveAttribute('data-open');
      sawStartingStyle = await panel.evaluate((node) => node.hasAttribute('data-starting-style'));
      expect(sawStartingStyle).toBe(false);

      // COL-ANI-09 (a), COL-STA-04: a 'none' close hides the panel without data-ending-style. Server: the module hides it
      // in the closing pass (the convergent write) and C#'s render of the unmounted state follows.
      await panel.evaluate((node) => {
        (window as unknown as { endingSeen: boolean }).endingSeen = false;
        new MutationObserver(() => {
          if (node.hasAttribute('data-ending-style')) {
            (window as unknown as { endingSeen: boolean }).endingSeen = true;
          }
        }).observe(node, { attributes: true });
      });
      await external.click();
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(panel).toHaveAttribute('hidden');
      await expect(panel).toHaveAttribute('data-closed');
      await expect(panel).not.toHaveAttribute('data-ending-style');
      await settle(fixture);
      expect(await page.evaluate(() => (window as unknown as { endingSeen: boolean }).endingSeen)).toBe(false);
    });
  });

  test.describe('CSS transitions', () => {
    test('applies data-starting-style while opening', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition');
      const { container, trigger } = collapsibleCase(fixture, 'opening');

      await recordStartingStyle(container);
      await trigger.click();

      // The transient starting style lasts until the frame after the module measured the panel.
      await expect(container).toHaveAttribute('data-seen-starting', 'true');
      const panel = container.locator('[data-panel]');
      await expect(panel).toHaveAttribute('data-open');
      // COL-ANI-04: one frame later the starting style is gone and the measured size is held.
      await expect(panel).not.toHaveAttribute('data-starting-style');
      await expect.poll(() => panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toMatch(/px$|^auto$/);
    });

    test('restores a measured height before applying closing transition styles', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'closing');

      await expect.poll(() => panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toBe('auto');
      await recordMotion(container);

      await trigger.click();

      // Both hold only mid-transition: the ending style arrives on a panel already holding its measured pixel height.
      await expect.poll(async () => (await motion(container)).find((record) => record.kind === 'ending')?.height).toMatch(/px$/);
      await expect(panel).toHaveCount(0);
    });

    test('unmounts a zero-size panel without waiting for unrelated transitions', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition', { animations: true });
      const { trigger, panel } = collapsibleCase(fixture, 'zero-size');

      await expect(panel).toHaveAttribute('data-open');
      await trigger.click();

      // The 10 s opacity transition is not awaited: the zero-size panel unmounts on the ending frame (COL-ANI-10).
      await expect(panel).toHaveCount(0, { timeout: 5_000 });
    });

    test('preserves inline alignment styles while measuring an opening panel', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition');
      const { container, trigger, panel } = collapsibleCase(fixture, 'mixed');

      await expect(panel).toHaveCSS('justify-content', 'center');
      // The neutralised alignment lasts until the next frame: a mutation record is its witness.
      await panel.evaluate((node) => {
        new MutationObserver(() => {
          if (node.hasAttribute('data-starting-style')
            && node.style.getPropertyValue('justify-content') === 'initial'
            && node.style.getPropertyPriority('justify-content') === 'important') {
            node.dataset.seenMeasuring = 'true';
          }
        }).observe(node, { attributes: true, attributeFilter: ['style', 'data-starting-style'] });
      });
      await trigger.click();

      await expect(panel).toHaveAttribute('data-seen-measuring', 'true');
      // COL-ANI-05: a frame later the authored inline value is back.
      await expect.poll(() => panel.evaluate((node) => node.style.justifyContent)).toBe('center');
      // COL-ANI-02, COL-EDGE-03: both motions found: the transition path and the warning.
      await settle(fixture);
      const diagnostics = await readDiagnostics(page, mode, fixture);
      expect(diagnostics.map((entry) => entry.message)).toContain(
        'Blazix: CSS transitions and CSS animations both detected on a collapsible or accordion panel. Only one of either animation type should be used.',
      );
      void container;
    });

    test('keeps exit transitions working after a close is interrupted by reopening', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'interrupted');
      const element = await panel.elementHandle();

      await trigger.click();
      await expect(panel).toHaveAttribute('data-ending-style');

      await trigger.click();
      await expect(panel).toHaveAttribute('data-open');
      await expect(panel).not.toHaveAttribute('data-starting-style');

      await trigger.click();
      await expect(panel).toHaveAttribute('data-ending-style');
      // The same element, not a new one.
      expect(await container.locator('[data-panel]').evaluate((node, original) => node === original, element)).toBe(true);
    });

    test('keeps the measured size when an open animation finishes during a close commit', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition', { animations: true });
      const { trigger, panel } = collapsibleCase(fixture, 'late-open-animation');

      // A 10 s animation reported while the panel is open, finished by the commit that closes it (Base UI's
      // ResolveAnimationOnClose, here a MutationObserver registered after the module's, so it runs after the closing pass).
      await panel.evaluate((node) => {
        let current: Animation | null = null;
        node.getAnimations = () => {
          if (!node.hasAttribute('data-open')) {
            return [];
          }
          if (current === null || current.playState === 'finished') {
            current = new Animation(new KeyframeEffect(null, [], 10_000), document.timeline);
            current.play();
            node.dataset.animationStarted = 'true';
          }
          return [current];
        };
        new MutationObserver(() => {
          if (node.hasAttribute('data-closed')) {
            current?.finish();
          }
        }).observe(node, { attributes: true, attributeFilter: ['data-closed'] });
      });
      // The open side's watcher ran at attach, before the stub: a reopen arms it again on the stubbed animation.
      await trigger.click();
      await expect(panel).toHaveAttribute('data-closed');
      await trigger.click();
      await expect(panel).toHaveAttribute('data-open');
      await expect(panel).toHaveAttribute('data-animation-started', 'true');

      // The held size lasts from the closing pass until the close completes a few frames later, so the variable is
      // recorded on each style mutation until the ending style arrives.
      await panel.evaluate((node) => {
        const element = node as HTMLElement;
        const heights: string[] = [];
        element.dataset.closeHeights = '';
        new MutationObserver((mutations, observer) => {
          heights.push(element.style.getPropertyValue('--collapsible-panel-height'));
          if (mutations.some((mutation) => mutation.attributeName === 'data-ending-style') && element.hasAttribute('data-ending-style')) {
            element.dataset.closeHeights = heights.join(',');
            observer.disconnect();
          }
        }).observe(element, { attributes: true, attributeFilter: ['style', 'data-ending-style'] });
      });
      await trigger.click();
      await expect(panel).toHaveAttribute('data-closed');
      // COL-ANI-17: the open side's completion, which resolved during the close commit, does not clear the held size:
      // from the closing pass to the ending style, the variable never goes back to auto.
      await expect(panel).toHaveAttribute('data-close-heights', /px/);
      const heights = (await panel.getAttribute('data-close-heights'))!.split(',');
      expect(heights.slice(heights.findIndex((height) => height.endsWith('px')))).not.toContain('auto');
    });

    test('does not restart the entrance transition when a close animation finishes after reopening', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'late-close-animation');
      const element = await panel.elementHandle();

      await panel.evaluate((node) => {
        const closeAnimation = new Animation(new KeyframeEffect(null, [], 10_000), document.timeline);
        closeAnimation.play();
        (window as unknown as { closeAnimation: Animation }).closeAnimation = closeAnimation;
        node.getAnimations = () => {
          if (node.hasAttribute('data-ending-style')) {
            node.dataset.closeAnimationStarted = 'true';
            return [closeAnimation];
          }
          return [];
        };
      });

      await trigger.click();
      await expect(panel).toHaveAttribute('data-close-animation-started', 'true');

      await trigger.click();
      await expect(panel).toHaveAttribute('data-open');
      await expect(panel).not.toHaveAttribute('data-starting-style');

      await page.evaluate(() => (window as unknown as { closeAnimation: Animation }).closeAnimation.finish());
      await settle(fixture);

      await expect(panel).toHaveAttribute('data-open');
      await expect(panel).not.toHaveAttribute('data-starting-style');
      expect(await container.locator('[data-panel]').evaluate((node, original) => node === original, element)).toBe(true);
    });
  });

  test.describe('CSS animations', () => {
    test('does not run the mount animation when initially open', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-animation', { animations: true });
      const { panel } = collapsibleCase(fixture, 'initially-open');

      await expect(panel).toHaveAttribute('data-open');
      expect(await panel.evaluate((node) => node.getAnimations().length)).toBe(0);
      expect(await computed(panel, 'animation-name')).toBe('none');
    });

    test('still animates on close and reopen after being initially open', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-animation', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'close-reopen');

      expect(await panel.evaluate((node) => node.getAnimations().length)).toBe(0);
      await recordMotion(container);

      await trigger.click();
      // The animation runs only while the panel is closing: its start is recorded on the closed panel.
      await expect.poll(() => animationStarted(container, 'panel-slide-up', 'closed')).toBe(true);

      await expect(panel).toHaveAttribute('hidden');
      await trigger.click();
      await expect.poll(() => animationStarted(container, 'panel-slide-down', 'open')).toBe(true);
    });

    test('restores measured dimensions before applying a closing keyframe animation', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-animation', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'closing-keyframe');

      await expect.poll(() => panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toBe('auto');
      await recordMotion(container);

      await trigger.click();
      // All three hold only while the closing animation runs: the closing keyframes start on the closed panel, and the
      // ending style arrives on a panel holding its measured pixel height.
      await expect.poll(() => animationStarted(container, 'panel-slide-up', 'closed')).toBe(true);
      await expect.poll(async () => (await motion(container)).find((record) => record.kind === 'ending')?.height).toMatch(/px$/);
    });

    test('still animates on reopen after being initially open when only open keyframes are defined', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-animation', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'only-open-keyframes');

      expect(await panel.evaluate((node) => node.getAnimations().length)).toBe(0);

      await trigger.click();
      await expect(panel).toHaveAttribute('data-closed');
      await recordMotion(container);

      await trigger.click();
      // The animation runs only while the panel is opening: its start is recorded on the open panel.
      await expect.poll(() => animationStarted(container, 'panel-slide-down', 'open')).toBe(true);
    });

    test('animates on reopen after an initially open panel is removed by the render function', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-render-function', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'removable-animation');

      expect(await panel.evaluate((node) => node.getAnimations().length)).toBe(0);

      await trigger.click();
      await expect(panel).toHaveCount(0);

      await recordMotion(container);
      await trigger.click();
      await expect(panel).toHaveAttribute('data-open');
      expect(await computed(panel, 'animation-name')).toBe('removable-panel-open');
      // The new element's opening keyframes run (the suppression ended with the close, COL-ANI-15).
      await expect.poll(() => animationStarted(container, 'removable-panel-open', 'open')).toBe(true);
    });
  });

  test.describe('server-side rendering', () => {
    test('suppresses the initial keyframe animation when rendered open', async ({ page, mode }) => {
      // COL-ANI-14, the handover at attach: the prerendered markup carries animation-name: none.
      const response = await page.request.get(fixtureUrl(mode, 'collapsible-initially-open'));
      const html = await response.text();
      await page.setContent(html.replace(/<script[\s\S]*?<\/script>/g, ''));
      const panel = page.locator('[data-case="class"] [data-panel]');
      expect(await panel.evaluate((node) => node.style.animationName)).toBe('none');
      expect(await panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toBe('auto');
    });

    test('suppresses the initial keyframe animation from inline styles when rendered open', async ({ page, mode }) => {
      const response = await page.request.get(fixtureUrl(mode, 'collapsible-initially-open'));
      const html = await response.text();
      await page.setContent(html.replace(/<script[\s\S]*?<\/script>/g, ''));
      const panel = page.locator('[data-case="inline"] [data-panel]');
      expect(await panel.evaluate((node) => node.style.animationName)).toBe('none');
      expect(await panel.evaluate((node) => node.style.animationDuration)).toBe('100ms');
    });

    test('hydration of an initially open panel does not replay its keyframe animation', async ({ page, mode }) => {
      // COL-ANI-14's hydration clause: the interactive render and the module keep animation-name: none.
      const fixture = await gotoCollapsible(page, mode, 'collapsible-initially-open', { animations: true });
      for (const name of ['class', 'inline']) {
        const panel = collapsibleCase(fixture, name).panel;
        await expect(panel).toHaveAttribute('data-open');
        expect(await panel.evaluate((node) => node.style.animationName)).toBe('none');
        expect(await runningAnimations(panel)).toBe(0);
      }
    });
  });

  test.describe('re-attachment (React.Activity)', () => {
    test('does not replay open transitions when revealing an initially open panel', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-reattach');
      const { container, panel, external: toggle } = collapsibleCase(fixture, 'transition-initially-open');

      await expect.poll(() => panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toBe('auto');
      await container.evaluate((node) => {
        node.dataset.transitionRuns = '0';
        node.addEventListener('transitionrun', () => {
          node.dataset.transitionRuns = String(Number(node.dataset.transitionRuns) + 1);
        });
      });

      await toggle.click();
      await expect(panel).toHaveCount(0);
      await toggle.click();
      await expect(panel).toHaveAttribute('data-open');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-transition-runs', '0');
    });

    test('does not replay open transitions when revealing a panel opened by the user', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-reattach', { animations: true });
      const { container, trigger, panel, external: toggle } = collapsibleCase(fixture, 'transition-user-opened');

      await trigger.click();
      // The height switches to auto once the opening transition ends (COL-ANI-07).
      await expect.poll(() => panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toBe('auto');
      await expect(panel).toHaveAttribute('data-open');
      await container.evaluate((node) => {
        node.dataset.transitionRuns = '0';
        node.addEventListener('transitionrun', () => {
          node.dataset.transitionRuns = String(Number(node.dataset.transitionRuns) + 1);
        });
      });

      await toggle.click();
      await expect(panel).toHaveCount(0);
      await toggle.click();
      await expect(panel).toHaveAttribute('data-open');
      await settle(fixture);

      await expect(container).toHaveAttribute('data-transition-runs', '0');
    });

    for (const [title, name, userOpened] of [
      ['does not replay open keyframe animations when revealing an initially open panel', 'keyframes-initially-open', false],
      ['does not replay open keyframe animations when revealing a panel opened by the user', 'keyframes-user-opened', true],
      ['does not replay open keyframe animations from inline styles when revealing a panel opened by the user', 'inline-keyframes-user-opened', true],
    ] as const) {
      test(title, async ({ page, mode }) => {
        const fixture = await gotoCollapsible(page, mode, 'collapsible-reattach', { animations: true });
        const { container, trigger, panel, external: toggle } = collapsibleCase(fixture, name);

        if (userOpened) {
          await recordMotion(container);
          await trigger.click();
          await expect(panel).toHaveAttribute('data-open');
          // The user's open plays the keyframes once; the panel is settled when its start has been recorded and no
          // animation runs any more.
          await expect.poll(() => animationStarted(container, 'panel-slide-down', 'open')).toBe(true);
        }
        await expect.poll(() => runningAnimations(panel)).toBe(0);
        // The module has reported the keyframe type before the panel goes away.
        await settle(fixture);

        // From here no opening keyframe may start: the re-attached panel renders animation-name: none (COL-ANI-20).
        await recordMotion(container);
        await toggle.click();
        await expect(panel).toHaveCount(0);
        await toggle.click();
        await expect(panel).toHaveAttribute('data-open');
        await settle(fixture);
        await panel.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

        expect(await panel.evaluate((node) => node.style.animationName)).toBe('none');
        expect((await motion(container)).filter((record) => record.kind === 'animationstart')).toEqual([]);
      });
    }
  });

  test.describe('render function', () => {
    test('finishes closing when the render function removes the panel element', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-render-function');
      const { container, trigger, content } = collapsibleCase(fixture, 'removes');

      await expect(content).toHaveAttribute('data-open');

      await trigger.click();

      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(content).toHaveCount(0);
      await expect(container).toHaveAttribute('data-last-open', 'false');
      // Closing settles even though no element is left to animate (COL-ANI-18, COL-DOM-06).
      await expect(trigger).not.toHaveAttribute('data-ending-style');
      await expect(container.locator('[data-root]')).not.toHaveAttribute('data-ending-style');
    });

    test('unmounts a panel that mounts after the close has already entered the ending phase', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-render-function');
      const { container, trigger, content } = collapsibleCase(fixture, 'mounts-during-ending');

      await trigger.click();

      await expect(container.locator('[data-rendered-statuses]')).toHaveAttribute('data-rendered-statuses', /ending/);
      await expect(content).toHaveCount(0);
    });
  });
});

test.describe('<Collapsible.Panel /> beforematch', () => {
  // Base UI runs these where `onbeforematch` exists (Chromium; not Firefox at 1.9.0).
  test.beforeEach(async ({ page }) => {
    test.skip(!(await page.evaluate(() => 'onbeforematch' in window)), 'the browser has no beforematch');
  });

  test.describe('interrupted beforematch opens', () => {
    test('keeps the temporary zero animation duration until the panel closes', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'zero-duration');

      await fireBeforeMatch(panel);
      await expect(panel).toHaveAttribute('data-open');
      await settle(fixture);
      await panel.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

      expect(await computed(panel, 'animation-duration')).toBe('0s');
      await recordMotion(container);

      await trigger.click();
      // The animation runs only while the panel is closing: its start is recorded on the closed panel.
      await expect.poll(() => animationStarted(container, 'panel-slide-up', 'closed')).toBe(true);
      expect(await computed(panel, 'animation-duration')).toBe('0.123s');
    });

    test('restores the transition duration before the first close after a beforematch open', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found', { animations: true });
      const { container, trigger, panel } = collapsibleCase(fixture, 'restores-duration');

      await fireBeforeMatch(panel);
      await expect(panel).toHaveAttribute('data-open');
      await settle(fixture);
      await panel.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

      expect(await computed(panel, 'transition-duration')).toBe('0s');
      await recordMotion(container);

      await trigger.click();
      // All three hold only while the closing transition runs: the ending style arrives with the authored inline
      // duration back, and the closing transition runs.
      await expect.poll(async () => (await motion(container)).find((record) => record.kind === 'ending')?.transitionDuration).toBe('123ms');
      await expect.poll(async () => (await motion(container)).some((record) => record.kind === 'transitionrun' && record.name === 'height')).toBe(true);
    });

    test('does not suppress a later animated open after a no-motion beforematch open', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found');
      const { trigger, panel, external: enableMotion } = collapsibleCase(fixture, 'later-motion');

      await fireBeforeMatch(panel);
      await expect(panel).toHaveAttribute('data-open');

      await trigger.click();
      await expect(panel).toHaveAttribute('data-closed');

      await enableMotion.click();
      await expect(panel).toHaveClass(/motion-panel/);
      await trigger.click();

      await expect(panel).toHaveAttribute('data-open');
      await expect.poll(() => panel.evaluate((node) => node.style.transitionDuration)).toBe('123ms');
    });

    test('does not keep a hidden transition running after a hiddenUntilFound panel closes', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found', { animations: true });
      const { trigger, panel } = collapsibleCase(fixture, 'no-hidden-transition');

      await trigger.click();
      await expect(panel).toHaveAttribute('data-open');

      await trigger.click();
      await expect(panel).toHaveAttribute('hidden', 'until-found');
      await panel.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

      expect(await runningAnimations(panel)).toBe(0);
      expect(await computed(panel, 'opacity')).toBe('0');
    });
  });

  test.describe('prop: hiddenUntilFound', () => {
    test('does not open or suppress the next trigger open when beforematch is canceled', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found');
      const { container, trigger, panel } = collapsibleCase(fixture, 'canceled');

      await fireBeforeMatch(panel, { removeHidden: true });

      await expect(container).toHaveAttribute('data-calls', '1');
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(panel).toHaveAttribute('data-closed');
      // WebAssembly decides inside the dispatch, so the collapsed styles never leave. Server: the decision point's
      // fallback `run` showed the content until C#'s answer arrived; the revert then restores them (ADR 01, reveal row).
      await expect(panel).toHaveAttribute('data-starting-style');
      await expect.poll(() => computed(panel, 'height')).toBe('0px');
      // A canceled reveal must also stay hidden and searchable.
      await expect(panel).toHaveAttribute('hidden', 'until-found');

      await trigger.click();

      await expect(container).toHaveAttribute('data-calls', '2');
      await expect(panel).toHaveAttribute('data-open');
      await expect.poll(() => panel.evaluate((node) => node.style.transitionDuration)).toBe('123ms');
    });

    test('expands the panel while beforematch is being dispatched', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found');
      const { panel } = collapsibleCase(fixture, 'dispatch');

      await expect(panel).toHaveAttribute('data-starting-style');

      // Registered after the module's own listener, so this observes the panel from where the browser does.
      const revealed = await panel.evaluate((node) => {
        let startingStyleWhenRevealed: boolean | undefined;
        let heightWhenRevealed: string | undefined;
        node.addEventListener('beforematch', () => {
          startingStyleWhenRevealed = node.hasAttribute('data-starting-style');
          node.removeAttribute('hidden');
          heightWhenRevealed = getComputedStyle(node).height;
        });
        node.dispatchEvent(new Event('beforematch', { bubbles: true, cancelable: false }));
        return { startingStyleWhenRevealed, heightWhenRevealed };
      });

      expect(revealed.startingStyleWhenRevealed).toBe(false);
      expect(revealed.heightWhenRevealed).not.toBe('0px');
    });

    test('re-collapses the panel when the open state never arrives', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found');
      const { container, trigger, panel, external: rerender } = collapsibleCase(fixture, 'never-arrives');

      await expect(panel).toHaveAttribute('data-starting-style');

      await fireBeforeMatch(panel, { removeHidden: true });

      await expect(panel).toHaveAttribute('hidden', 'until-found');
      await expect(panel).toHaveAttribute('data-starting-style');
      expect(await computed(panel, 'height')).toBe('0px');

      await rerender.click();
      await expect(container.locator('[data-root]')).toHaveAttribute('data-rerenders', '1');

      await expect(panel).toHaveAttribute('data-starting-style');
      expect(await computed(panel, 'height')).toBe('0px');

      // The reveal never opened the panel, so it must not consume the motion of the next ordinary open.
      await trigger.click();

      await expect(panel).toHaveAttribute('data-open');
      expect(await computed(panel, 'transition-duration')).toBe('0.123s');
    });

    test('re-collapses a keyframe panel when the open state never arrives', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found');
      const { container, trigger, panel } = collapsibleCase(fixture, 'keyframe-never-arrives');

      // A full open/close cycle reaches the steady state where the closed panel no longer carries data-starting-style
      // (keyframe panels only hold it until the animation type has been detected).
      await trigger.click();
      await expect(panel).toHaveAttribute('data-open');
      await trigger.click();
      await expect(panel).toHaveAttribute('hidden', 'until-found');
      await expect(panel).not.toHaveAttribute('data-starting-style');

      await fireBeforeMatch(panel, { removeHidden: true });

      await expect(panel).toHaveAttribute('hidden', 'until-found');
      await expect(panel).not.toHaveAttribute('data-starting-style');

      await recordMotion(container);
      await trigger.click();

      await expect(panel).toHaveAttribute('data-open');
      expect(await computed(panel, 'animation-duration')).toBe('0.1s');
      // The opening keyframes ran: their start is recorded on the open panel (a 100 ms animation can be over before a read).
      await expect.poll(() => animationStarted(container, 'panel-slide-down', 'open')).toBe(true);
    });

    test('uses `hidden="until-found" to hide panel when true', async ({ page, mode }) => {
      const fixture = await gotoCollapsible(page, mode, 'collapsible-hidden-until-found');
      const { container, panel } = collapsibleCase(fixture, 'until-found');

      await expect(panel).toHaveAttribute('hidden', 'until-found');
      await fireBeforeMatch(panel);

      await expect(container).toHaveAttribute('data-calls', '1');
      await expect(container).toHaveAttribute('data-last-reason', 'none');
      await expect(panel).toHaveAttribute('data-open');
    });
  });
});

test.describe('ADR 01 verification item 2', () => {
  // Issue 2: the Server round trip of the TRN-ANI-03/09/10 row. After the exit animation, the panel stays in the document
  // until the render batch that C# sends for the completion message, about two round trips later. The optimistic
  // convergent write (ADR 04 §2) hides it at the exit's end meanwhile. This measures, per added round trip, the time from
  // that hide (t0, the module's `hidden` write) to the batch that removes the element (t1): how long the closed panel
  // would stay visible without the write. Chromium's network emulation does not delay the circuit's WebSocket, so the
  // latency is added by routing the WebSocket through the test with a delay each way.
  const oneWayDelays = [0, 25, 75];
  const closes = 20;

  for (const delay of oneWayDelays) {
    test(`@verifies ADR-01/2 the closed panel is hidden at exit end ahead of its removal (round trip +${delay * 2} ms)`, async ({ page, mode }) => {
      test.skip(mode !== 'server', 'the round trip exists on Server only');
      test.setTimeout(120_000);
      if (delay > 0) {
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
      const fixture = await gotoCollapsible(page, mode, 'collapsible-css-transition', { animations: true });
      const { container, trigger } = collapsibleCase(fixture, 'opening');
      const gaps: number[] = [];

      for (let run = 0; run < closes; run++) {
        await trigger.click();
        const panel = container.locator('[data-panel]');
        await expect(panel).toHaveAttribute('data-open');
        await expect.poll(() => panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toBe('auto');

        await container.evaluate((node) => {
          const element = node.querySelector('[data-panel]') as HTMLElement;
          const timing: { hidden?: number; removed?: number } = {};
          (window as unknown as { closeTiming: typeof timing }).closeTiming = timing;
          new MutationObserver((records, observer) => {
            for (const record of records) {
              if (record.type === 'attributes' && record.target === element && element.hasAttribute('hidden') && timing.hidden === undefined) {
                timing.hidden = performance.now();
              }
              if (record.type === 'childList' && [...record.removedNodes].includes(element)) {
                timing.removed = performance.now();
                observer.disconnect();
              }
            }
          }).observe(node, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden'] });
        });
        await trigger.click();
        await expect(panel).toHaveCount(0);
        const timing = await page.evaluate(() => (window as unknown as { closeTiming: { hidden?: number; removed?: number } }).closeTiming);

        // The optimistic hide lands first, in every run.
        expect(timing.hidden).toBeDefined();
        expect(timing.removed).toBeDefined();
        expect(timing.hidden!).toBeLessThanOrEqual(timing.removed!);
        gaps.push(timing.removed! - timing.hidden!);
      }

      const sorted = [...gaps].sort((a, b) => a - b);
      const median = sorted[Math.floor(sorted.length / 2)]!;
      const p95 = sorted[Math.min(sorted.length - 1, Math.ceil(sorted.length * 0.95) - 1)]!;
      const summary = { addedRoundTripMs: delay * 2, closes, medianMs: Math.round(median * 10) / 10, p95Ms: Math.round(p95 * 10) / 10, maxMs: Math.round(sorted[sorted.length - 1]! * 10) / 10 };
      await test.info().attach(`adr-01-2-round-trip-${delay * 2}ms.json`, { body: JSON.stringify({ ...summary, gaps }), contentType: 'application/json' });
      console.log(`ADR-01/2 ${JSON.stringify(summary)}`);
    });
  }
});
