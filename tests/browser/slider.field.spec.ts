import { test, expect, Fixture, fixtureUrl, holdCircuit, type Page, type RenderMode } from './helpers';
import type { Locator } from '@playwright/test';

// SLD §8, SLD-FOC-04..08, SLD-ARIA-12, SLD-EDGE-10, SLD-ANI-05 (docs/components/slider.md): the Field, Form, label and
// prerender titles of the Slider family at L4, per render mode. Titles are Base UI's (SliderRoot.test.tsx,
// SliderLabel.test.tsx, SliderThumb.test.tsx) except the port's own, which name their row. Fixtures: `slider-field` (a
// slider in a Field, optionally in a Form), `slider-label` (a slider with a Slider.Label outside a Field),
// `slider-edit-form` (an EditForm) and the prerendered `slider-prerender`. Readiness is the fixture's `data-attached`
// (the Root's `slider` handle, and the Slider.Label's and the Form's `form` handles where present). Base UI's `setProps`
// is a fixture toggle pressed with a DOM click, so focus stays where the title put it; its `fireEvent.focus`/`blur` are
// the input's own `focus()`/`blur()`, and its `user.keyboard('[Tab]')` is a real Tab.

type Query = Record<string, string>;

async function open(page: Page, mode: RenderMode, name: string, query: Query = {}): Promise<Locator> {
  const search = new URLSearchParams(query).toString();
  await page.goto(`${fixtureUrl(mode, name)}${search ? `?${search}` : ''}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** The sync barrier: a DOM click on the fixture's Sync button, answered once C# has handled every earlier message. */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((button) => (button as HTMLButtonElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

/** Presses a fixture toggle (Base UI's `setProps`) with a DOM click, which moves no focus. */
async function toggle(fixture: Locator, name: string): Promise<void> {
  await fixture.locator(`[data-toggle="${name}"]`).evaluate((button) => (button as HTMLButtonElement).click());
  await settle(fixture);
}

function inputOf(fixture: Locator, index = 0): Locator {
  return fixture.getByTestId(`thumb-${index}`).locator('input[type="range"]');
}

async function validations(fixture: Locator): Promise<string[]> {
  return ((await fixture.getAttribute('data-validations')) ?? '').split(' ').filter(Boolean);
}

async function focusLog(fixture: Locator): Promise<string[]> {
  return ((await fixture.getAttribute('data-focus-log')) ?? '').split(' ').filter(Boolean);
}

/** Adds a submit listener after the form module's that records the native form data of every submit event. */
async function recordFormData(form: Locator): Promise<() => Promise<Array<Array<[string, string]>>>> {
  await form.evaluate((element) => {
    const records: Array<Array<[string, string]>> = [];
    (element as HTMLElement & { recordedFormData?: typeof records }).recordedFormData = records;
    element.addEventListener('submit', (event) => {
      records.push([...new FormData(element as HTMLFormElement).entries()].map(([key, value]) => [key, String(value)]));
      event.preventDefault();
    });
  });
  return () => form.evaluate((element) => [...((element as HTMLElement & { recordedFormData?: Array<Array<[string, string]>> }).recordedFormData ?? [])]);
}

/** Moves an element of the fixture into the open shadow root of a new host appended to the fixture container. */
async function moveIntoShadowRoot(fixture: Locator, element: Locator): Promise<void> {
  const handle = await element.elementHandle();
  await fixture.evaluate((container, node) => {
    const host = document.createElement('div');
    host.setAttribute('data-testid', 'shadow-host');
    container.appendChild(host);
    const shadowRoot = host.attachShadow({ mode: 'open' });
    const inner = document.createElement('div');
    shadowRoot.appendChild(inner);
    inner.appendChild(node!);
  }, handle);
}

test.describe('<Slider.Root />', () => {
  test.describe('server-side rendering', () => {
    // SLD-ARIA-12, SLD-DOM-19, SLD-API-13: the static response is the server's render; the Slider.Label publishes its id
    // after the interactive render, so only the hydrated page carries the association.
    test('does not link Slider.Label before hydration', async ({ page, mode }) => {
      const html = await (await page.request.get(fixtureUrl(mode, 'slider-prerender'))).text();
      const single = html.slice(html.indexOf('data-case="single"'), html.indexOf('data-case="range"'));
      const rootTag = single.match(/<div[^>]*data-testid="root"[^>]*>/)?.[0];
      const labelTag = single.match(/<div[^>]*data-testid="label"[^>]*>/)?.[0];
      const inputTag = single.match(/<input[^>]*type="range"[^>]*>/)?.[0];
      expect(rootTag).toBeTruthy();
      expect(labelTag).toBeTruthy();
      expect(inputTag).toBeTruthy();
      const labelId = labelTag!.match(/ id="([^"]+)"/)?.[1];
      const rootId = rootTag!.match(/ id="([^"]+)"/)?.[1];
      expect(labelId).toBeTruthy();
      expect(rootId).toBeTruthy();
      expect(rootTag).not.toContain('aria-labelledby');
      expect(inputTag).not.toContain('aria-labelledby');

      await page.goto(fixtureUrl(mode, 'slider-prerender'), { waitUntil: 'commit' });
      const fixture = page.locator(Fixture.container('slider-prerender'));
      await expect(fixture).toHaveAttribute('data-attached', 'true');
      const scope = fixture.locator('[data-case="single"]');
      await expect(scope.getByTestId('label')).toHaveAttribute('id', labelId!);
      await expect(scope.getByRole('slider')).toHaveAttribute('aria-labelledby', labelId!);
      await expect(scope.getByTestId('root')).toHaveAttribute('aria-labelledby', labelId!);
    });
  });

  test.describe('form submission', () => {
    // SLD-FRM-01: one FormData entry per thumb, the invariant string of the value whatever the Format.
    test('should include the slider value', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-field', { form: '1', name: 'slider', value: '25', format: 'usd' });
      const recorded = await recordFormData(fixture.getByTestId('form'));
      await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuetext', '$25.00');

      await fixture.getByRole('button', { name: 'submit' }).click();

      await expect.poll(async () => (await recorded()).length).toBeGreaterThan(0);
      const entries = (await recorded())[0]!;
      expect(entries.filter(([key]) => key === 'slider').map(([, value]) => value)).toEqual(['25']);
    });

    test('should include range slider value', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-field', { form: '1', name: 'slider', values: '25,50', format: 'usd' });
      const recorded = await recordFormData(fixture.getByTestId('form'));

      await fixture.getByRole('button', { name: 'submit' }).click();

      await expect.poll(async () => (await recorded()).length).toBeGreaterThan(0);
      const entries = (await recorded())[0]!;
      expect(entries.filter(([key]) => key === 'slider').map(([, value]) => value)).toEqual(['25', '50']);
    });
  });

  test.describe('Field', () => {
    // SLD-FOC-04, SLD-FRM-15.
    test('[data-focused]', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-field');
      const root = fixture.getByTestId('root');
      const input = inputOf(fixture);

      await expect(root).not.toHaveAttribute('data-focused');
      await input.focus();
      await expect(root).toHaveAttribute('data-focused', '');
      await input.blur();
      await expect(root).not.toHaveAttribute('data-focused');
    });

    test.describe('[data-focused] without a blur event', () => {
      // SLD-FOC-04, SLD-EDGE-05, SLD-API-16.
      test('is removed when the focused thumb becomes disabled', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-field', { value: '20' });
        const field = fixture.getByTestId('field');
        await inputOf(fixture).focus();
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle(fixture, 'first-disabled');

        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is removed when the focused thumb unmounts', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-field', { value: '20' });
        const field = fixture.getByTestId('field');
        await inputOf(fixture).focus();
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle(fixture, 'first-mounted');

        await expect(fixture.getByRole('slider')).toHaveCount(0);
        await expect(field).not.toHaveAttribute('data-focused');
      });

      test('is kept when focus moved to a sibling before the first thumb is disabled', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-field', { values: '20,40' });
        const field = fixture.getByTestId('field');
        await inputOf(fixture, 0).focus();
        await inputOf(fixture, 1).focus();
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle(fixture, 'first-disabled');

        await expect(inputOf(fixture, 0)).toBeDisabled();
        await expect(field).toHaveAttribute('data-focused', '');
      });

      test('is kept when a sibling thumb is disabled or unmounted', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-field', { values: '20,40' });
        const field = fixture.getByTestId('field');
        await inputOf(fixture, 1).focus();
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle(fixture, 'first-disabled');
        await expect(inputOf(fixture, 0)).toBeDisabled();
        await expect(field).toHaveAttribute('data-focused', '');

        await toggle(fixture, 'first-mounted');
        await expect(fixture.getByRole('slider')).toHaveCount(1);
        await expect(field).toHaveAttribute('data-focused', '');
      });
    });

    test.describe('prop: validate', () => {
      // SLD-FOC-07, SLD-FRM-02: the registered control is the range input, which the blocked submission focuses.
      test('moves focus to the range input on invalid submit', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-field', { form: '1', validate: 'error', value: '50' });
        const input = fixture.getByRole('slider');
        await expect(input).not.toBeFocused();

        await fixture.getByRole('button', { name: 'submit' }).click();

        await expect(input).toBeFocused();
      });

      // SLD-FRM-06.
      test('validationMode=onBlur', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-field', { mode: 'onBlur', validate: 'gt1' });
        const input = fixture.getByRole('slider');
        await expect(input).not.toHaveAttribute('aria-invalid');

        await input.focus();
        await page.keyboard.press('ArrowRight');
        await page.keyboard.press('ArrowRight');
        await expect(input).toHaveAttribute('aria-valuenow', '2');
        await settle(fixture);
        await expect(input).not.toHaveAttribute('aria-invalid');

        await input.blur();
        await expect(input).toHaveAttribute('aria-invalid', 'true');
      });

      // SLD-FRM-06, SLD-STA-04.
      test('validationMode=onBlur preserves single-element array values', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-field', { mode: 'onBlur', validate: 'record', values: '25' });
        const input = fixture.getByRole('slider');

        await input.focus();
        await input.blur();

        await expect.poll(() => validations(fixture)).toEqual(['[25]']);
      });
    });

    // ADR 05 Amendments 2026-11-18 item 2 (decision 14 (a)), verification item 5: inside Blazor's EditForm the slider
    // notifies the EditContext, takes the property's name, and the field shows the DataAnnotations message as its invalid
    // state until a committed change makes the value valid.
    test('@verifies ADR-05/5 a slider bound in an EditForm notifies the EditContext and shows its validator\'s message', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-edit-form');
      const root = fixture.getByTestId('root');
      const input = fixture.getByRole('slider');
      const message = fixture.getByTestId('message');
      await expect(input).toHaveAttribute('name', 'Volume');
      await expect(input).toHaveAttribute('aria-valuenow', '60');

      await fixture.getByRole('button', { name: 'Submit' }).click();
      await expect(fixture).toHaveAttribute('data-invalid-submits', '1');
      await expect(message).toHaveText('The volume must be at most 50.');
      await expect(fixture.getByTestId('error')).toHaveText('The volume must be at most 50.');
      await expect(root).toHaveAttribute('data-invalid', '');
      await expect(input).toHaveAttribute('aria-invalid', 'true');

      await input.focus();
      await page.keyboard.press('PageDown');
      await expect(input).toHaveAttribute('aria-valuenow', '50');
      await expect(message).toHaveCount(0);
      await expect(root).not.toHaveAttribute('data-invalid');
      await expect(input).not.toHaveAttribute('aria-invalid');

      await fixture.getByRole('button', { name: 'Submit' }).click();
      await expect(fixture).toHaveAttribute('data-valid-submits', '1');
    });
  });

  test.describe('Slider.Label', () => {
    // SLD-FOC-06.
    test('Slider.Label focuses slider on click', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label');

      await fixture.getByTestId('label').click();

      await expect(fixture.getByRole('slider')).toBeFocused();
    });

    test('Slider.Label does not focus a thumb on click for range sliders', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label', { values: '20,80' });

      await fixture.getByTestId('label').click();
      await settle(fixture);

      await expect(fixture.getByRole('slider', { name: 'Minimum price' })).not.toBeFocused();
      await expect(fixture.getByRole('slider', { name: 'Maximum price' })).not.toBeFocused();
    });
  });

  test.describe('prop: disabled', () => {
    // SLD-FOC-05: Base UI spies on the input's `blur`; here the page wraps it.
    // Quarantined (ADR 11 §4) on Server until 2026-12-03, issue 328: failed twice in CI on Server (PR 320's run 44464038543;
    // main's run 44487136692 at e6b4ad9b), each the blur count (`Expected: > 0 / Received: 0`).
    test('explicitly blurs the focused thumb when disabled', {
      tag: '@quarantine',
      annotation: { type: 'open', description: 'https://github.com/JakeMurrayDev/Iodine.Headless/issues/328 (expires 2026-12-03)' },
    }, async ({ page, mode }) => {
      test.fixme(mode === 'server', 'Quarantined on Server until 2026-12-03 (issue 328)');
      const fixture = await open(page, mode, 'slider-label', { value: '30' });
      const input = fixture.getByRole('slider');
      await input.focus();
      await expect(input).toBeFocused();
      await input.evaluate((element) => {
        const calls: number[] = [];
        (window as unknown as { blurCalls: number[] }).blurCalls = calls;
        element.blur = function blur(this: HTMLElement) {
          calls.push(1);
          HTMLElement.prototype.blur.call(this);
        };
      });

      await toggle(fixture, 'disabled');

      await expect(input).toBeDisabled();
      await expect.poll(() => page.evaluate(() => (window as unknown as { blurCalls: number[] }).blurCalls.length)).toBeGreaterThan(0);
    });

    // SLD-FOC-05's "post-commit, pre-paint" (issue 328; design point 10k-1 (a), chosen by the user 2026-11-24). On Server the
    // render that sets `disabled` and the module's update arrive as two messages; held apart across two frames, Chromium's
    // focus fixup moves focus to <body> first (firing `blur` itself), so a blur run only from the update never calls
    // `blur()`. The module blurs in the render batch's own task, before any frame, as Base UI's layout effect does.
    test('port: a focused thumb is blurred explicitly before the next frame when a render disables the slider', async ({ page, mode }) => {
      test.skip(mode !== 'server', 'Server only: on WebAssembly the render and the module update are not split by the circuit');
      const hold = await holdCircuit(page, {
        // The module's `update` with `"disabled":true`, its JSON escaped as `\u0022` inside the invocation.
        from: (text) => text.includes('BeginInvokeJS') && text.includes('\\u0022disabled\\u0022:true'),
      });
      const fixture = await open(page, mode, 'slider-label', { value: '30' });
      const input = fixture.getByRole('slider');
      await input.focus();
      await expect(input).toBeFocused();
      await input.evaluate((element) => {
        const calls: number[] = [];
        (window as unknown as { blurCalls: number[] }).blurCalls = calls;
        element.blur = function blur(this: HTMLElement) {
          calls.push(1);
          HTMLElement.prototype.blur.call(this);
        };
      });

      hold.arm();
      await fixture.locator('[data-toggle="disabled"]').evaluate((button) => (button as HTMLButtonElement).click());
      await expect(input).toBeDisabled();
      await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
      await hold.release();
      await hold.released();
      await settle(fixture);

      expect(await page.evaluate(() => (window as unknown as { blurCalls: number[] }).blurCalls.length)).toBe(1);
      await expect(input).not.toBeFocused();
    });
  });

  test.describe('keyboard', () => {
    // SLD-FOC-08, SLD-KEY-11, SLD-PTR-07: a real pointer press focuses the input without :focus-visible; the keydown is
    // dispatched, as Base UI's fireEvent.keyDown, so the browser's own heuristic cannot make the input match.
    test('shows :focus-visible after keyboard interaction following a pointer press', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label');
      const input = fixture.getByRole('slider');
      const box = (await fixture.getByTestId('track').boundingBox())!;

      await page.mouse.move(box.x + 40, box.y + 5);
      await page.mouse.down();
      await expect(input).toBeFocused();
      expect(await input.evaluate((element) => element.matches(':focus-visible'))).toBe(false);
      await page.mouse.up();

      await input.dispatchEvent('keydown', { key: 'ArrowRight', bubbles: true });

      await expect.poll(() => input.evaluate((element) => element.matches(':focus-visible'))).toBe(true);
    });

    // SLD-EDGE-10: the Root, moved into a shadow root once attached, still handles a key on its input.
    test('should handle keyboard changes inside a shadow root', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label', { value: '3', name: 'shadow' });
      await moveIntoShadowRoot(fixture, fixture.getByTestId('root'));
      const input = fixture.getByTestId('shadow-host').locator('input[type="range"]');
      await expect(input).toHaveAttribute('name', 'shadow');

      await input.focus();
      await input.dispatchEvent('keydown', { key: 'ArrowRight', bubbles: true });

      await expect(fixture).toHaveAttribute('data-changes', '4:keyboard:0');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '4:keyboard:0');
    });
  });
});

test.describe('<Slider.Label />', () => {
  // SLD-FOC-06, SLD-FRM-12.
  test('focuses the registered thumb when composed within a Field', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-field', { value: '50', label: '1', unrelated: '1' });

    await fixture.getByTestId('label').click();

    await expect(fixture.getByRole('slider', { name: 'Volume' })).toBeFocused();
    await expect(fixture.getByRole('slider', { name: 'Unrelated range' })).not.toBeFocused();
  });

  // SLD-FOC-06, SLD-EDGE-10: the Field, moved into a shadow root once attached; an outside input takes the thumb's id.
  test('focuses the thumb in its shadow root when the outer document has a matching ID', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-field', { value: '50', label: '1' });
    await page.evaluate(() => {
      const unrelated = document.createElement('input');
      unrelated.setAttribute('aria-label', 'Unrelated control');
      unrelated.setAttribute('data-testid', 'unrelated');
      document.body.prepend(unrelated);
    });
    await moveIntoShadowRoot(fixture, fixture.getByTestId('field'));
    const host = fixture.getByTestId('shadow-host');
    const thumb = host.getByRole('slider', { name: 'Volume' });
    const thumbId = await thumb.getAttribute('id');
    expect(thumbId).toBeTruthy();
    await page.getByTestId('unrelated').evaluate((element, id) => {
      element.id = id;
    }, thumbId!);

    await host.getByText('Volume', { exact: true }).click();

    expect(await host.evaluate((element) => element.shadowRoot!.activeElement?.getAttribute('id') ?? null)).toBe(thumbId);
    await expect(page.getByTestId('unrelated')).not.toBeFocused();
  });

  // SLD-FOC-06, SLD-EDGE-10: only the label moves into a shadow root.
  test('focuses a thumb in the light DOM from a label in a shadow root', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-field', { value: '50', label: '1', unrelated: '1' });
    await moveIntoShadowRoot(fixture, fixture.getByTestId('label'));

    await fixture.getByTestId('shadow-host').getByText('Volume', { exact: true }).click();

    await expect(inputOf(fixture)).toBeFocused();
  });

  // SLD-EDGE-10: the Field, removed from the document once attached, as Base UI's detached root.
  test('does not throw when the label is clicked in a detached React root', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-field', { value: '50', label: '1' });
    await page.evaluate(() => {
      const errors: string[] = [];
      (window as unknown as { labelErrors: string[] }).labelErrors = errors;
      window.addEventListener('error', (event) => {
        event.preventDefault();
        errors.push(String(event.message));
      });
      window.addEventListener('unhandledrejection', (event) => errors.push(String(event.reason)));
    });
    const label = await fixture.getByTestId('label').elementHandle();
    await fixture.getByTestId('field').evaluate((field) => {
      (window as unknown as { detached: Element }).detached = field;
      field.remove();
    });

    await label!.evaluate((element) => (element as HTMLElement).click());
    await settle(fixture);

    expect(await page.evaluate(() => (window as unknown as { labelErrors: string[] }).labelErrors)).toEqual([]);
  });

  // SLD-FOC-06.
  test('does nothing when a Field slider has no thumb to focus', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-field', { value: '50', label: '1', thumbs: '0' });

    await fixture.getByTestId('label').click();
    await settle(fixture);

    expect(await page.evaluate(() => document.activeElement === document.body)).toBe(true);
  });
});

test.describe('<Slider.Thumb />', () => {
  test.describe('focus and blur', () => {
    // SLD-FOC-01, SLD-ARIA-01, SLD-DOM-14, SLD-API-22, SLD-KEY-09: the inputs are the slider's only tab stops, and the
    // consumer's onfocus and onblur are the input's (the fixture's `data-focus-log`). Focus starts on the fixture's
    // Disabled button, the element before the slider, in place of Base UI's body.
    test('single thumb', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label', { value: '50' });
      const input = fixture.getByRole('slider');
      expect(await input.evaluate((element) => element.tagName)).toBe('INPUT');
      await expect(input).toHaveAttribute('type', 'range');
      await fixture.getByRole('button', { name: 'Disabled' }).focus();

      await page.keyboard.press('Tab');
      await expect(input).toBeFocused();
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0']);

      await page.keyboard.press('Tab');
      await expect.poll(() => page.evaluate(() => document.activeElement === document.body)).toBe(true);
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0', 'blur:0']);
    });

    // SLD-FOC-02, SLD-KEY-10, SLD-ARIA-01.
    test('multiple thumbs', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label', { values: '50,70' });
      const [slider1, slider2] = await fixture.getByRole('slider').all();
      for (const slider of [slider1!, slider2!]) {
        expect(await slider.evaluate((element) => element.tagName)).toBe('INPUT');
        await expect(slider).toHaveAttribute('type', 'range');
      }
      await fixture.getByRole('button', { name: 'Disabled' }).focus();

      await page.keyboard.press('Tab');
      await expect(slider1!).toBeFocused();
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0']);

      await page.keyboard.press('Tab');
      await expect(slider2!).toBeFocused();
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0', 'blur:0', 'focus:1']);

      await page.keyboard.press('Tab');
      await expect.poll(() => page.evaluate(() => document.activeElement === document.body)).toBe(true);
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0', 'blur:0', 'focus:1', 'blur:1']);
    });

    // SLD-API-22: the consumer's onfocus and onblur are the input's. C# sees no currentTarget; the port's witness is that
    // the handlers run once for the input's own focus and blur.
    test('forwards focus and blur to the input so `currentTarget` is the input', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label', { value: '50' });
      const input = fixture.getByRole('slider');
      await fixture.getByRole('button', { name: 'Disabled' }).focus();

      await page.keyboard.press('Tab');
      await expect(input).toBeFocused();
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0']);

      await page.keyboard.press('Tab');
      await expect(input).not.toBeFocused();
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0', 'blur:0']);
    });

    // SLD-API-22, SLD-KEY-11.
    test('does not emit extra blur and focus events when restoring focus-visible', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-label');
      const input = fixture.getByRole('slider');
      const box = (await fixture.getByTestId('track').boundingBox())!;

      await page.mouse.move(box.x + 40, box.y + 5);
      await page.mouse.down();
      await expect(input).toBeFocused();
      await page.mouse.up();
      await expect.poll(() => focusLog(fixture)).toEqual(['focus:0']);

      await input.dispatchEvent('keydown', { key: 'ArrowRight', bubbles: true });
      await expect.poll(() => input.evaluate((element) => element.matches(':focus-visible'))).toBe(true);
      await settle(fixture);

      expect(await focusLog(fixture)).toEqual(['focus:0']);
    });

    // SLD-FOC-04, SLD-FRM-06.
    test('does not commit field validation when moving focus between range thumbs', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-field', { mode: 'onBlur', validate: 'record', values: '20,50' });
      const [thumb0, thumb1] = [inputOf(fixture, 0), inputOf(fixture, 1)];
      await fixture.locator('[data-toggle="first-mounted"]').focus();

      await page.keyboard.press('Tab');
      await expect(thumb0).toBeFocused();
      await settle(fixture);
      const before = (await validations(fixture)).length;

      await page.keyboard.press('Tab');
      await expect(thumb1).toBeFocused();
      await settle(fixture);
      expect((await validations(fixture)).length).toBe(before);

      await page.keyboard.press('Tab');
      await expect(thumb1).not.toBeFocused();
      await expect.poll(async () => (await validations(fixture)).length).toBe(before + 1);
    });

    // SLD-FRM-06, SLD-FRM-05, SLD-FOC-04.
    test('commits field validation when focus moves from a thumb to an arbitrary Control child', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-field', { mode: 'onBlur', validate: 'record', values: '20,50', help: '1' });
      const [thumb0, thumb1] = [inputOf(fixture, 0), inputOf(fixture, 1)];
      const field = fixture.getByTestId('field');
      await fixture.locator('[data-toggle="first-mounted"]').focus();

      await page.keyboard.press('Tab');
      await expect(thumb0).toBeFocused();
      await settle(fixture);
      const before = (await validations(fixture)).length;

      await page.keyboard.press('Tab');
      await expect(thumb1).toBeFocused();
      await settle(fixture);
      expect((await validations(fixture)).length).toBe(before);

      await page.keyboard.press('Tab');
      await expect(fixture.getByRole('button', { name: 'Help' })).toBeFocused();
      await expect.poll(async () => (await validations(fixture)).length).toBe(before + 1);
      await expect(field).toHaveAttribute('data-touched', '');
      await expect(field).not.toHaveAttribute('data-focused');

      await page.keyboard.press('Tab');
      await expect(fixture.getByRole('button', { name: 'Outside' })).toBeFocused();
      await settle(fixture);
      expect((await validations(fixture)).length).toBe(before + 1);
    });
  });

  // SLD-ANI-05, SLD-DOM-05/09: centre-mode thumbs are positioned by percentage in the server's markup. The page's
  // framework scripts are blocked, so what renders is the static response alone.
  test.describe('server-side rendering', () => {
    async function openStatic(page: Page, mode: RenderMode): Promise<Locator> {
      await page.route(/\/_framework\//, (route) => route.abort());
      await page.goto(fixtureUrl(mode, 'slider-prerender'));
      return page.locator(Fixture.container('slider-prerender'));
    }

    test('single thumb', async ({ page, mode }) => {
      const fixture = await openStatic(page, mode);
      const scope = fixture.locator('[data-case="single"]');
      const thumb = scope.getByTestId('thumb');
      await expect(thumb).toBeVisible();
      expect(await thumb.evaluate((element) => getComputedStyle(element).getPropertyValue('left'))).toBe('30px');
      expect(await scope.getByTestId('indicator').evaluate((element) => getComputedStyle(element).getPropertyValue('width'))).toBe('30px');
    });

    test('multiple thumbs', async ({ page, mode }) => {
      const fixture = await openStatic(page, mode);
      const scope = fixture.locator('[data-case="range"]');
      const [thumb0, thumb1] = [scope.getByTestId('thumb').nth(0), scope.getByTestId('thumb').nth(1)];
      await expect(thumb0).toBeVisible();
      await expect(thumb1).toBeVisible();
      expect(await thumb0.evaluate((element) => getComputedStyle(element).getPropertyValue('left'))).toBe('30px');
      expect(await thumb1.evaluate((element) => getComputedStyle(element).getPropertyValue('left'))).toBe('40px');
      const indicator = scope.getByTestId('indicator');
      expect(await indicator.evaluate((element) => getComputedStyle(element).getPropertyValue('left'))).toBe('30px');
      expect(await indicator.evaluate((element) => getComputedStyle(element).getPropertyValue('width'))).toBe('10px');
    });
  });
});
