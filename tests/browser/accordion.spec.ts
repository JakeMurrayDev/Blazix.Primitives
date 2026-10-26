import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, Fixture, readDiagnostics, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/accordion.md, and the L4 clauses of four [L2] rows (ACC-DOM-15, ACC-EDGE-04/05: the
// `hidden="until-found"` literal; ACC-EVT-05: keyboard activation), on the Accordion parts over the `button` and
// `collapsible` modules. Titles are Base UI's (accordion/root/AccordionRoot.test.tsx, accordion/trigger/AccordionTrigger.test.tsx,
// accordion/panel/AccordionPanel.test.tsx); the port titles cover the reveal through the item's chain (ACC-EVT-06, ACC-ANI-05;
// settled by the orchestrator 2026-10-25) and the Server relaxation rows of ACC §9. Every test runs on both render modes; a
// row of ADR 01's relaxation table asserts its Server substitute on `server`. Animated fixtures open with `?animations=1`
// (ADR 06 §2). No sleeps and no frame counting: a state that lasts a few frames is witnessed by a marker the page records.

type FixtureName = 'accordion-basic' | 'accordion-css-transition' | 'accordion-initially-open' | 'accordion-reattach' | 'accordion-hidden-until-found';

/** Opens an Accordion fixture and waits until every listed part has its behavior attached. */
async function gotoAccordion(page: Page, mode: RenderMode, name: FixtureName, options: { animations?: boolean } = {}): Promise<Locator> {
  const url = options.animations ? `${fixtureUrl(mode, name)}?animations=1` : fixtureUrl(mode, name);
  await page.goto(url, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** A barrier: clicks the fixture's sync button and waits for C# to count it (every earlier event has reached C# then). */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

function accordionCase(fixture: Locator, name: string) {
  const container = fixture.locator(`[data-case="${name}"]`);
  return {
    container,
    triggers: container.locator('button:not([data-before]):not([data-external]), [role="button"]'),
    before: container.locator('[data-before]'),
    external: container.locator('[data-external]'),
  };
}

/** Dispatches a synthetic `beforematch`, as Base UI's tests do; with `removeHidden`, drops the attribute in the same task. */
async function fireBeforeMatch(panel: Locator, options: { removeHidden?: boolean } = {}): Promise<void> {
  await panel.evaluate((node, removeHidden) => {
    node.dispatchEvent(new Event('beforematch', { bubbles: true, cancelable: false }));
    if (removeHidden) {
      node.removeAttribute('hidden');
    }
  }, options.removeHidden ?? false);
}

/** Adds a one-way delay to the circuit's WebSocket in each direction (CDP emulation does not delay it). */
async function delayCircuit(page: Page, oneWayMs: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), oneWayMs);
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), oneWayMs);
    });
  });
}

test.describe('<Accordion.Root />', () => {
  test.describe('keyboard interactions', () => {
    for (const { triggerKind, caseName, key } of [
      { triggerKind: 'interactive', caseName: 'uncontrolled', key: 'Enter' },
      { triggerKind: 'interactive', caseName: 'uncontrolled', key: 'Space' },
      { triggerKind: 'non-interactive', caseName: 'non-native', key: 'Enter' },
      { triggerKind: 'non-interactive', caseName: 'non-native', key: 'Space' },
    ]) {
      test(`key: ${key} toggles the Accordion open state with ${triggerKind} triggers`, async ({ page, mode }) => {
        const fixture = await gotoAccordion(page, mode, 'accordion-basic');
        const { container, triggers, before } = accordionCase(fixture, caseName);
        const trigger = triggers.first();
        const content = container.getByText('Panel contents 1');

        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(content).toHaveCount(0);

        // ACC-KEY-04: Tab is not intercepted; it reaches the trigger in the native order.
        await before.focus();
        await page.keyboard.press('Tab');
        await expect(trigger).toBeFocused();
        await page.keyboard.press(key);

        await expect(trigger).toHaveAttribute('aria-expanded', 'true');
        await expect(trigger).toHaveAttribute('data-panel-open');
        await expect(content).toBeVisible();
        await expect(content).toHaveAttribute('data-open');

        await page.keyboard.press(key);

        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(content).toHaveCount(0);

        // ACC-KEY-04's absence clause: an arrow key is not handled (no focus move, no preventDefault).
        const prevented = await trigger.evaluate((node) => {
          const event = new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true });
          node.dispatchEvent(event);
          return event.defaultPrevented;
        });
        expect(prevented).toBe(false);
        await expect(trigger).toBeFocused();
      });
    }
  });

  test.describe('keyboard activation timing', () => {
    for (const { triggerKind, caseName } of [
      { triggerKind: 'interactive', caseName: 'uncontrolled' },
      { triggerKind: 'non-interactive', caseName: 'non-native' },
    ]) {
      test(`opens and closes on Space keyup when rendering ${triggerKind} triggers`, async ({ page, mode }) => {
        const fixture = await gotoAccordion(page, mode, 'accordion-basic');
        const { container, triggers, before } = accordionCase(fixture, caseName);
        const trigger = triggers.first();
        const content = container.getByText('Panel contents 1');

        await before.focus();
        await page.keyboard.press('Tab');
        await expect(trigger).toBeFocused();

        await page.keyboard.down('Space');
        await settle(fixture);
        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(content).toHaveCount(0);
        await expect(container).toHaveAttribute('data-item-calls', '0');

        await page.keyboard.up('Space');
        await expect(trigger).toHaveAttribute('aria-expanded', 'true');
        await expect(content).toHaveCount(1);
        await expect(container).toHaveAttribute('data-item-calls', '1');
        await expect(container).toHaveAttribute('data-last-item-open', 'true');

        await page.keyboard.down('Space');
        await settle(fixture);
        await expect(trigger).toHaveAttribute('aria-expanded', 'true');
        await expect(container).toHaveAttribute('data-item-calls', '1');

        await page.keyboard.up('Space');
        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(content).toHaveCount(0);
        await expect(container).toHaveAttribute('data-item-calls', '2');
        await expect(container).toHaveAttribute('data-last-item-open', 'false');
      });
    }
  });

  test.describe('prop: disabled', () => {
    for (const part of ['root', 'item']) {
      test(`does not toggle or fire callbacks when the ${part} is disabled`, async ({ page, mode }) => {
        const fixture = await gotoAccordion(page, mode, 'accordion-basic');
        const { container, triggers } = accordionCase(fixture, `disabled-${part}`);
        const trigger = triggers.first();

        // A native disabled trigger has `disabled`: the pointer press does nothing and it cannot take focus, so the keys
        // that follow reach no trigger (Base UI's trigger1.focus() then user.keyboard).
        await expect(trigger).toBeDisabled();
        await trigger.click({ force: true });
        await trigger.evaluate((node) => (node as HTMLElement).focus());
        await expect(trigger).not.toBeFocused();
        await page.keyboard.press('Space');
        await page.keyboard.press('Enter');
        await settle(fixture);

        await expect(trigger).toHaveAttribute('aria-expanded', 'false');
        await expect(container.getByText('Panel contents 1')).toHaveCount(0);
        await expect(container).toHaveAttribute('data-value-calls', '0');
        await expect(container).toHaveAttribute('data-item-calls', '0');
      });
    }
  });

  test.describe('prop: onValueChange', () => {
    test('default item value', async ({ page, mode }) => {
      // ACC-EVT-05's keyboard clause: the Space activation's details carry the click it produced, never a placeholder.
      const fixture = await gotoAccordion(page, mode, 'accordion-basic');
      const { container, triggers } = accordionCase(fixture, 'values');

      await triggers.nth(0).click();
      await expect(container).toHaveAttribute('data-value-calls', '1');
      await expect(container).toHaveAttribute('data-last-value', '0');
      await expect(container).toHaveAttribute('data-last-value-reason', 'trigger-press');
      await expect(container).toHaveAttribute('data-last-value-event', /^(MouseEvent|PointerEvent):click:/);

      await triggers.nth(1).focus();
      await page.keyboard.press('Space');
      await expect(container).toHaveAttribute('data-value-calls', '2');
      await expect(container).toHaveAttribute('data-last-value', '0,1');
      await expect(container).toHaveAttribute('data-last-value-reason', 'trigger-press');
      await expect(container).toHaveAttribute('data-last-value-event', /^MouseEvent:click:detail=0$/);
    });
  });

  test('preserves generated part associations during hydration', async ({ page, mode }) => {
    // The static response first (React's renderToString), then the interactive page that hydrates it.
    const response = await page.request.get(fixtureUrl(mode, 'accordion-initially-open'));
    await page.setContent((await response.text()).replace(/<script[\s\S]*?<\/script>/g, ''));
    let trigger = page.getByRole('button', { name: 'Trigger 1' });
    let panel = page.getByText('Panel contents 1');
    const staticPanelId = await panel.getAttribute('id');
    const staticTriggerId = await trigger.getAttribute('id');
    expect(staticPanelId).toMatch(/^base-ui-/);
    await expect(trigger).toHaveAttribute('aria-controls', staticPanelId!);
    await expect(panel).toHaveAttribute('aria-labelledby', staticTriggerId!);

    const fixture = await gotoAccordion(page, mode, 'accordion-initially-open');
    trigger = fixture.getByRole('button', { name: 'Trigger 1' });
    panel = fixture.getByText('Panel contents 1');
    await expect(trigger).toHaveAttribute('aria-controls', staticPanelId!);
    await expect(panel).toHaveAttribute('id', staticPanelId!);
    await expect(panel).toHaveAttribute('aria-labelledby', staticTriggerId!);
    await expect(trigger).toHaveAttribute('id', staticTriggerId!);
  });
});

test.describe('<Accordion.Trigger />', () => {
  test('removes a disabled non-native trigger from the tab order', async ({ page, mode }) => {
    const fixture = await gotoAccordion(page, mode, 'accordion-basic');
    const { container, before } = accordionCase(fixture, 'non-native-disabled');
    const trigger = container.getByRole('button', { name: 'Trigger' });

    await expect(trigger).toHaveAttribute('aria-disabled', 'true');
    await expect(trigger).toHaveAttribute('tabindex', '-1');
    await expect(trigger).not.toHaveAttribute('disabled');

    await before.focus();
    await page.keyboard.press('Tab');
    await expect(trigger).not.toBeFocused();
  });

  test('keeps a non-native trigger tabbable', async ({ page, mode }) => {
    // ACC-FOC-01: every enabled trigger is an independent tab stop, and opening moves no focus.
    const fixture = await gotoAccordion(page, mode, 'accordion-basic');
    const { triggers, before } = accordionCase(fixture, 'non-native');
    const trigger = triggers.first();

    await expect(trigger).toHaveAttribute('tabindex', '0');
    await before.focus();
    await page.keyboard.press('Tab');
    await expect(trigger).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await expect(trigger).toBeFocused();
  });
});

test.describe('<Accordion.Panel />', () => {
  test('warns when hiddenUntilFound overrides keepMounted={false}', async ({ page, mode }) => {
    const fixture = await gotoAccordion(page, mode, 'accordion-hidden-until-found');
    const panel = accordionCase(fixture, 'root-warning').container.getByText('Panel');

    await expect(panel).toHaveAttribute('hidden', 'until-found');
    const diagnostics = await readDiagnostics(page, mode, fixture);
    expect(diagnostics.map((entry) => entry.message)).toContain(
      'Blazix: The `KeepMounted` parameter set to `false` on `AccordionRoot` is ignored when `HiddenUntilFound` is enabled, since panels must remain mounted while closed.',
    );
  });

  test('warns when a panel enables hiddenUntilFound and disables keepMounted', async ({ page, mode }) => {
    const fixture = await gotoAccordion(page, mode, 'accordion-hidden-until-found');
    const panel = accordionCase(fixture, 'panel-warning').container.getByText('This is panel content');

    await expect(panel).toHaveAttribute('hidden', 'until-found');
    const diagnostics = await readDiagnostics(page, mode, fixture);
    expect(diagnostics.map((entry) => entry.message)).toContain(
      'Blazix: The `KeepMounted` parameter set to `false` on an `AccordionPanel` is ignored when `HiddenUntilFound` is enabled on the panel or root, since the panel must remain mounted while closed.',
    );
  });

  test('passes root hiddenUntilFound to closed panels and allows panel overrides', async ({ page, mode }) => {
    const fixture = await gotoAccordion(page, mode, 'accordion-hidden-until-found');
    const { container } = accordionCase(fixture, 'overrides');

    await expect(container.getByText('This is panel content')).toHaveAttribute('hidden', 'until-found');
    await expect(container.getByText('Overridden panel')).toHaveCount(0);
  });

  test.describe('server-side rendering', () => {
    test('suppresses the initial keyframe animation from inline styles when rendered open', async ({ page, mode }) => {
      const response = await page.request.get(fixtureUrl(mode, 'accordion-initially-open'));
      await page.setContent((await response.text()).replace(/<script[\s\S]*?<\/script>/g, ''));
      const panel = page.getByTestId('panel');

      expect(await panel.evaluate((node) => node.style.animationName)).toBe('none');
      expect(await panel.evaluate((node) => node.style.animationDuration)).toBe('100ms');
      expect(await panel.evaluate((node) => node.style.getPropertyValue('--accordion-panel-height'))).toBe('auto');
      // ACC-DOM-17: the Collapsible-named variables are never set on an Accordion panel.
      expect(await panel.evaluate((node) => node.style.getPropertyValue('--collapsible-panel-height'))).toBe('');
    });
  });

  test.describe('CSS transitions', () => {
    test('keeps the closing panel visible until its exit transition completes when switching items', async ({ page, mode }) => {
      const fixture = await gotoAccordion(page, mode, 'accordion-css-transition', { animations: true });
      const { container } = accordionCase(fixture, 'switching');
      const trigger2 = container.getByRole('button', { name: 'Trigger 2' });
      const panel1 = container.getByTestId('panel-1');
      const panel2 = container.getByTestId('panel-2');

      await expect.poll(() => panel1.evaluate((node) => node.style.getPropertyValue('--accordion-panel-height'))).toBe('auto');
      await expect(panel1).toHaveAttribute('data-open');

      // The closing panel's mid-transition state is transient: a mutation record captures it in one snapshot.
      await container.evaluate((node) => {
        const panel = node.querySelector('[data-testid="panel-1"]') as HTMLElement;
        const other = node.querySelector('[data-testid="panel-2"]') as HTMLElement;
        new MutationObserver((_records, observer) => {
          if (panel.hasAttribute('data-ending-style')) {
            node.dataset.snapshot = JSON.stringify({
              panel1EndingStyle: true,
              panel1Hidden: panel.hasAttribute('hidden'),
              panel1HeightInPx: /px$/.test(panel.style.getPropertyValue('--accordion-panel-height')),
              panel1CollapsibleVariable: panel.style.getPropertyValue('--collapsible-panel-height'),
              panel2Open: other.hasAttribute('data-open'),
            });
            observer.disconnect();
          }
        }).observe(panel, { attributes: true, attributeFilter: ['data-ending-style'] });
      });

      await trigger2.click();

      await expect(container).toHaveAttribute('data-snapshot', /.+/);
      expect(JSON.parse((await container.getAttribute('data-snapshot'))!)).toEqual({
        panel1EndingStyle: true,
        panel1Hidden: false,
        panel1HeightInPx: true,
        panel1CollapsibleVariable: '',
        panel2Open: true,
      });

      await expect(panel1).toHaveAttribute('hidden');
      await expect(panel2).not.toHaveAttribute('hidden');
    });
  });

  test.describe('React.Activity', () => {
    test('does not replay open keyframe animations from inline styles when revealing a panel opened by the user', async ({ page, mode }) => {
      const fixture = await gotoAccordion(page, mode, 'accordion-reattach', { animations: true });
      const { container, triggers, external: toggle } = accordionCase(fixture, 'inline-keyframes-user-opened');
      const panel = container.getByTestId('panel');

      await triggers.first().click();
      await expect(panel).toHaveAttribute('data-open');
      await expect.poll(() => panel.evaluate((node) => node.getAnimations().filter((animation) => animation.playState !== 'finished').length)).toBe(0);
      // The module has reported the keyframe type before the panel goes away.
      await settle(fixture);

      // From here no opening keyframe may start: the re-attached panel renders animation-name: none (COL-ANI-20).
      await container.evaluate((node) => {
        node.dataset.animationStarts = '0';
        node.addEventListener('animationstart', () => {
          node.dataset.animationStarts = String(Number(node.dataset.animationStarts) + 1);
        });
      });
      await toggle.click();
      await expect(panel).toHaveCount(0);
      await toggle.click();
      await expect(panel).toHaveAttribute('data-open');
      await settle(fixture);
      await panel.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

      expect(await panel.evaluate((node) => node.style.animationName)).toBe('none');
      await expect(container).toHaveAttribute('data-animation-starts', '0');
    });
  });
});

test.describe('<Accordion.Panel /> beforematch', () => {
  // Base UI runs the Collapsible reveal tests where `onbeforematch` exists (Chromium).
  test.beforeEach(async ({ page }) => {
    test.skip(!(await page.evaluate(() => 'onbeforematch' in window)), 'the browser has no beforematch');
  });

  test('port: a reveal opens the item through the root value and closes the other item in single mode', async ({ page, mode }) => {
    const fixture = await gotoAccordion(page, mode, 'accordion-hidden-until-found');
    const { container } = accordionCase(fixture, 'reveal');
    const panelA = container.locator('[data-panel="a"]');
    const panelB = container.locator('[data-panel="b"]');
    await expect(panelA).toHaveAttribute('data-open');
    await expect(panelB).toHaveAttribute('hidden', 'until-found');

    await fireBeforeMatch(panelB, { removeHidden: true });

    await expect(container).toHaveAttribute('data-item-calls', '1');
    await expect(container).toHaveAttribute('data-last-item-reason', 'none');
    await expect(container).toHaveAttribute('data-last-item-event', /:beforematch:/);
    await expect(container).toHaveAttribute('data-value-calls', '1');
    await expect(container).toHaveAttribute('data-last-value', 'b');
    await expect(panelB).toHaveAttribute('data-open');
    await expect(panelB).not.toHaveAttribute('hidden');
    await expect(panelA).toHaveAttribute('data-closed');
    // The other item closes through its own lifecycle and ends hidden until found again.
    await expect(panelA).toHaveAttribute('hidden', 'until-found');
  });

  for (const [title, name, valueCalls] of [
    ['port: a reveal canceled in the item callback leaves the item closed and asks the root nothing', 'cancel-item', '0'],
    ['port: a reveal canceled in onValueChange leaves the item closed', 'cancel-root', '1'],
    ['port: a reveal that a controlled root ignores is reverted', 'controlled-ignoring', '1'],
  ] as const) {
    test(title, async ({ page, mode }) => {
      const fixture = await gotoAccordion(page, mode, 'accordion-hidden-until-found');
      const { container } = accordionCase(fixture, name);
      const panel = container.locator('[data-panel="a"]');
      const trigger = container.getByRole('button', { name: 'Trigger A' });

      await fireBeforeMatch(panel, { removeHidden: true });

      await expect(container).toHaveAttribute('data-item-calls', '1');
      await expect(container).toHaveAttribute('data-value-calls', valueCalls);
      await expect(trigger).toHaveAttribute('aria-expanded', 'false');
      await expect(panel).toHaveAttribute('data-closed');
      // WebAssembly decides inside the dispatch; Server shows the content until C#'s answer, then reverts (ACC §9, the
      // reveal row). Either way the panel ends hidden until found, with its collapsed starting style.
      await expect(panel).toHaveAttribute('hidden', 'until-found');
      await expect(panel).toHaveAttribute('data-starting-style');
    });
  }
});

test.describe('Accordion relaxations (ACC §9)', () => {
  test('the panel class sees the idle status once the item mirrors the panel report', async ({ page, mode }) => {
    // ACC-STA-03 (the RND-STA-01 row for mirrored fields): the open render evaluates 'starting'; the 'idle' the module
    // flips reaches the panel's state, and its class, when the panel's status report arrives (a round trip later on Server).
    const fixture = await gotoAccordion(page, mode, 'accordion-basic');
    const { container, triggers } = accordionCase(fixture, 'uncontrolled');

    await triggers.first().click();
    const panel = container.locator('[data-panel]');

    await expect(panel).toHaveClass(/status-idle/);
    await expect(panel).not.toHaveAttribute('data-starting-style');
  });

  test('a closed panel is hidden at its exit end ahead of its removal on Server', async ({ page, mode }) => {
    // ACC-STA-09, ACC-ANI-02 (the TRN-ANI-03/09/10 row, measured at about one round trip): the module hides the closing
    // panel when its exit ends (the convergent write); the render that removes it lands a round trip later on Server.
    test.skip(mode !== 'server', 'the round trip exists on Server only');
    await delayCircuit(page, 50);
    const fixture = await gotoAccordion(page, mode, 'accordion-css-transition', { animations: true });
    const { container } = accordionCase(fixture, 'unkept');
    const panel1 = container.getByTestId('panel-1');
    await expect.poll(() => panel1.evaluate((node) => node.style.getPropertyValue('--accordion-panel-height'))).toBe('auto');

    await container.evaluate((node) => {
      const element = node.querySelector('[data-testid="panel-1"]') as HTMLElement;
      new MutationObserver((records, observer) => {
        for (const record of records) {
          if (record.type === 'attributes' && element.hasAttribute('hidden') && node.dataset.hiddenAt === undefined) {
            node.dataset.hiddenAt = String(performance.now());
          }
          if (record.type === 'childList' && [...record.removedNodes].includes(element)) {
            node.dataset.removedAt = String(performance.now());
            observer.disconnect();
          }
        }
      }).observe(node, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden'] });
    });
    await container.getByRole('button', { name: 'Trigger 2' }).click();

    await expect(panel1).toHaveCount(0);
    const hiddenAt = Number(await container.getAttribute('data-hidden-at'));
    const removedAt = Number(await container.getAttribute('data-removed-at'));
    expect(hiddenAt).toBeGreaterThan(0);
    expect(hiddenAt).toBeLessThanOrEqual(removedAt);
  });

  test('the panel status report costs one Server message per status change', async ({ page, mode }) => {
    // Design point D1 (option (a), settled by the orchestrator 2026-10-26): switching items opens one panel ('idle' after its
    // starting frame) and closes the other ('ending'); the starting of the opened panel and the unset status at the end of the
    // exit are C#'s own and are never sent. Counted on the circuit's WebSocket.
    test.skip(mode !== 'server', 'the messages exist on Server only');
    const sent: string[] = [];
    await page.routeWebSocket(/_blazor/, (socket) => {
      const server = socket.connectToServer();
      socket.onMessage((message) => {
        // The circuit's frames are SignalR messages, text or binary.
        const text = typeof message === 'string' ? message : message.toString('utf8');
        if (text.includes('OnPanelStatusChange')) {
          sent.push(text);
        }
        server.send(message);
      });
      server.onMessage((message) => socket.send(message));
    });
    const fixture = await gotoAccordion(page, mode, 'accordion-css-transition', { animations: true });
    const { container } = accordionCase(fixture, 'switching');
    const panel1 = container.getByTestId('panel-1');
    const panel2 = container.getByTestId('panel-2');
    await expect.poll(() => panel1.evaluate((node) => node.style.getPropertyValue('--accordion-panel-height'))).toBe('auto');
    expect(sent).toEqual([]);

    await container.getByRole('button', { name: 'Trigger 2' }).click();
    await expect(panel1).toHaveAttribute('hidden');
    await expect.poll(() => panel2.evaluate((node) => node.style.getPropertyValue('--accordion-panel-height'))).toBe('auto');
    await settle(fixture);

    // A frame can carry several invocations (SignalR's record separator), so every report in it is counted.
    const statuses = sent.flatMap((frame) => [...frame.matchAll(/OnPanelStatusChange[^\u001e]*?status\\*":\\*"(\w+)/g)].map((match) => match[1]!)).sort();
    await test.info().attach('accordion-status-messages.json', { body: JSON.stringify({ frames: sent.length, statuses }), contentType: 'application/json' });
    console.log(`Accordion status reports per switch: ${JSON.stringify({ frames: sent.length, statuses })}`);
    expect(statuses).toEqual(['ending', 'idle']);
  });

  test('a new panel element shows its starting look until its module attaches', async ({ page, mode }) => {
    // The ADR 01 row of 2026-10-21 for a panel element that the opening render creates: on WebAssembly the module attaches
    // in the same task; on Server, with latency, the new element keeps data-starting-style for about a round trip.
    if (mode === 'server') {
      await delayCircuit(page, 50);
    }
    const fixture = await gotoAccordion(page, mode, 'accordion-css-transition', { animations: true });
    const { container } = accordionCase(fixture, 'unkept');
    await container.evaluate((node) => {
      new MutationObserver((records) => {
        for (const record of records) {
          record.addedNodes.forEach((added) => {
            if (added instanceof HTMLElement && added.matches('[data-testid="panel-2"]')) {
              node.dataset.insertedStarting = String(added.hasAttribute('data-starting-style'));
            }
          });
        }
      }).observe(node, { subtree: true, childList: true });
    });

    await container.getByRole('button', { name: 'Trigger 2' }).click();
    const panel2 = container.getByTestId('panel-2');

    await expect(panel2).toHaveAttribute('data-open');
    await expect(container).toHaveAttribute('data-inserted-starting', 'true');
    await expect(panel2).not.toHaveAttribute('data-starting-style');
    await expect.poll(() => panel2.evaluate((node) => node.style.getPropertyValue('--accordion-panel-height'))).toBe('auto');
  });
});
