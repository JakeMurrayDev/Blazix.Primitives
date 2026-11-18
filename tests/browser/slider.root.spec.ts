import { test, expect, Fixture, fixtureUrl, gotoFixture, holdCircuit, type Page, type RenderMode } from './helpers';
import type { CDPSession, Locator } from '@playwright/test';

// Records every inline start offset an element shows, read in a microtask queued from the observer's callback (the recorder
// rule of tests/browser/CONTEXT.md), so a snap-back that a later write undoes is still seen.
async function recordStarts(page: Page, element: Locator): Promise<void> {
  await element.evaluate((node) => {
    const log: string[] = [];
    (window as unknown as { sliderStarts: string[] }).sliderStarts = log;
    new MutationObserver(() => queueMicrotask(() => log.push((node as HTMLElement).style.getPropertyValue('inset-inline-start')))).observe(node, {
      attributes: true,
      attributeFilter: ['style'],
    });
  });
  void page;
}

async function recordedStarts(page: Page): Promise<string[]> {
  return page.evaluate(() => (window as unknown as { sliderStarts: string[] }).sliderStarts);
}

// SLD (docs/components/slider.md): the Root, the Value and the keys at L4, per render mode. Readiness is the fixture's
// `data-attached` (the Root's `slider` handle), never a settle alone.
test.describe('Slider', () => {
  // ADR 04 Amendments 2026-11-18 (the revision rule, decision 6): the family's first red L4 title. The key is stopped by the
  // module before Blazor's delegation, so nothing the page sends marks it: the title ends the hold itself (track 0's rule).
  test('port: a held circuit does not snap the thumb back after a further key press', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Only the Server circuit can be held.');
    const hold = await holdCircuit(page, {
      armOn: (text) => text.includes('OnValueRequest'),
      from: (text) => text.includes('update') && text.includes('revision'),
    });
    const fixture = await gotoFixture(page, mode, 'slider-basic');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const input = fixture.getByRole('slider');
    const thumb = fixture.getByTestId('thumb');
    await input.focus();

    hold.arm();
    await page.keyboard.press('ArrowRight');
    await expect(thumb).toHaveCSS('inset-inline-start', '26px');
    await page.keyboard.press('ArrowRight');
    await expect(thumb).toHaveCSS('inset-inline-start', '27px');
    await recordStarts(page, thumb);
    await hold.release();

    await expect(input).toHaveAttribute('aria-valuenow', '27');
    await expect(fixture).toHaveAttribute('data-changes', '26:keyboard:0 27:keyboard:0');
    await expect(input).toHaveValue('27');
    await expect(thumb).toHaveCSS('inset-inline-start', '27px');
    expect(await recordedStarts(page)).not.toContain('26%');
  });

  test('port: a held circuit does not snap the thumb back after a further move', async ({ page, mode }) => {
    test.skip(mode !== 'server', 'Only the Server circuit can be held.');
    const hold = await holdCircuit(page, {
      armOn: (text) => text.includes('OnValueRequest'),
      from: (text) => text.includes('update') && text.includes('revision'),
    });
    const fixture = await gotoFixture(page, mode, 'slider-basic');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const control = fixture.getByTestId('control');
    const thumb = fixture.getByTestId('thumb');
    const box = (await control.boundingBox())!;

    hold.arm();
    await page.mouse.move(box.x + 60, box.y + 5);
    await page.mouse.down();
    await expect(thumb).toHaveCSS('inset-inline-start', '60px');
    await page.mouse.move(box.x + 70, box.y + 5);
    await expect(thumb).toHaveCSS('inset-inline-start', '70px');
    await recordStarts(page, thumb);
    await hold.release();
    await page.mouse.up();

    await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '70');
    await expect(fixture).toHaveAttribute('data-commits', '70:drag');
    await expect(thumb).toHaveCSS('inset-inline-start', '70px');
    expect(await recordedStarts(page)).not.toContain('60%');
  });

  test('port: keys move the thumb and report', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'slider-basic');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const input = fixture.getByRole('slider');
    await expect(input).toHaveAttribute('aria-valuenow', '25');
    await input.focus();
    await page.keyboard.press('ArrowRight');
    await expect(input).toHaveAttribute('aria-valuenow', '26');
    await expect(fixture.getByTestId('value')).toHaveText('26');
    await expect(fixture).toHaveAttribute('data-changes', '26:keyboard:0');
    await expect(fixture).toHaveAttribute('data-commits', '26:keyboard');
    await expect(fixture.getByTestId('thumb')).toHaveCSS('inset-inline-start', '26px');
  });

  test('port: a track press and a drag', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'slider-basic');
    await expect(fixture).toHaveAttribute('data-attached', 'true');
    const control = fixture.getByTestId('control');
    const box = (await control.boundingBox())!;
    await page.mouse.move(box.x + 60, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 70, box.y + 5);
    await page.mouse.move(box.x + 80, box.y + 5);
    await page.mouse.up();
    const input = fixture.getByRole('slider');
    await expect(input).toHaveAttribute('aria-valuenow', '80');
    await expect(fixture).toHaveAttribute('data-changes', '60:track-press:0 70:drag:0 80:drag:0');
    await expect(fixture).toHaveAttribute('data-commits', '80:drag');
    await expect(input).toBeFocused();
  });
});

// SliderRoot.test.tsx's keyboard, commit, cancel and reason titles at L4 on the `slider-config` fixture, whose Root props
// come from the query (its header). Base UI's 0..100 px mocked Control is the fixture's 100 px Track, so its coordinates
// carry over; a thumb is 10 px wide and centred on its value. Keys are real `page.keyboard` input after a real Tab; pointer
// input is real `page.mouse`; touch input is CDP `Input.dispatchTouchEvent`, which sends the touch's pointer events before
// its touch events as a browser does. Base UI's `fireEvent.change` is the input's value set in the page and its `input` and
// `change` dispatched, as the browser does for a user change. Readiness is the fixture's `data-attached`.

type Query = Record<string, string>;

async function open(page: Page, mode: RenderMode, query: Query = {}): Promise<Locator> {
  const name = 'slider-config';
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

/** The Track's box: the origin of Base UI's coordinates. */
async function origin(fixture: Locator): Promise<{ x: number; y: number }> {
  return (await fixture.getByTestId('track').boundingBox())!;
}

/** Base UI's `user.keyboard('[Tab]')` from the document's start: the Sync button precedes the slider. */
async function tabIntoSlider(page: Page, fixture: Locator): Promise<void> {
  await fixture.locator('[data-sync-button]').focus();
  await page.keyboard.press('Tab');
}

/** Base UI's `fireEvent.change(input, { target: { value } })`: the value set, then the browser's `input` and `change`. */
async function changeInput(input: Locator, value: string): Promise<void> {
  await input.evaluate((element, next) => {
    (element as HTMLInputElement).value = next;
    element.dispatchEvent(new Event('input', { bubbles: true }));
    element.dispatchEvent(new Event('change', { bubbles: true }));
  }, value);
}

async function touchSession(page: Page): Promise<CDPSession> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 2 });
  return cdp;
}

type TouchType = 'touchStart' | 'touchMove' | 'touchEnd' | 'touchCancel';

/** CDP touch input; a `touchEnd` lists the points it releases, an empty one releases every point. */
async function touch(cdp: CDPSession, type: TouchType, points: Array<{ x: number; y: number; id?: number }>): Promise<void> {
  await cdp.send('Input.dispatchTouchEvent', { type, touchPoints: points });
}

/** CDP pen input: Chromium sends a `pen` pointer for these mouse events, and no touch events. */
async function pen(cdp: CDPSession, type: 'mousePressed' | 'mouseMoved' | 'mouseReleased', x: number, y: number): Promise<void> {
  await cdp.send('Input.dispatchMouseEvent', {
    type,
    x,
    y,
    button: 'left',
    buttons: type === 'mouseReleased' ? 0 : 1,
    clickCount: type === 'mouseMoved' ? 0 : 1,
    pointerType: 'pen',
  });
}

/** Records the id and type of every pointerdown, so a title can dispatch the pointercancel no real mouse or pen sends. */
async function recordPointerDowns(page: Page): Promise<void> {
  await page.evaluate(() => {
    const log: Array<{ id: number; type: string }> = [];
    (window as unknown as { pointerDowns: typeof log }).pointerDowns = log;
    document.addEventListener('pointerdown', (event) => log.push({ id: event.pointerId, type: event.pointerType }), true);
  });
}

/** Dispatches the pointercancel of the last pointerdown on the document, as Base UI's `fireEvent.pointerCancel`. */
async function dispatchPointerCancel(page: Page): Promise<void> {
  await page.evaluate(() => {
    const last = (window as unknown as { pointerDowns: Array<{ id: number; type: string }> }).pointerDowns.at(-1)!;
    document.body.dispatchEvent(new PointerEvent('pointercancel', { bubbles: true, pointerId: last.id, pointerType: last.type }));
  });
}

function entries(text: string | null): string[] {
  return (text ?? '').split(' ').filter(Boolean);
}

async function lastChange(fixture: Locator): Promise<string | undefined> {
  return entries(await fixture.getAttribute('data-changes')).at(-1);
}

test.describe('<Slider.Root />', () => {
  test.describe('prop: max', () => {
    // SLD-KEY-06.
    test('should not go more than the max', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '100', step: '100', max: '200' });
      const input = fixture.getByRole('slider');
      await tabIntoSlider(page, fixture);
      await expect(input).toBeFocused();

      await page.keyboard.press('ArrowRight');
      await expect(input).toHaveAttribute('aria-valuenow', '200');
      await page.keyboard.press('ArrowRight');
      await settle(fixture);
      await expect(input).toHaveAttribute('aria-valuenow', '200');
      await expect(fixture).toHaveAttribute('data-changes', '200:keyboard:0');
    });
  });

  test.describe('prop: min', () => {
    // SLD-KEY-06.
    test('should not go less than the min', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '1', step: '1', min: '0' });
      const input = fixture.getByRole('slider');
      await tabIntoSlider(page, fixture);
      await expect(input).toBeFocused();

      await page.keyboard.press('ArrowLeft');
      await expect(input).toHaveAttribute('aria-valuenow', '0');
      await page.keyboard.press('ArrowLeft');
      await settle(fixture);
      await expect(input).toHaveAttribute('aria-valuenow', '0');
      await expect(fixture).toHaveAttribute('data-changes', '0:keyboard:0');
    });
  });

  test.describe('prop: onValueCommitted', () => {
    // SLD-EVT-05, SLD-STA-23, SLD-PTR-05, SLD-PTR-11, SLD-API-04.
    test('single value', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0' });
      const input = fixture.getByRole('slider');
      const box = await origin(fixture);
      await page.mouse.move(box.x + 10, box.y + 5);
      await page.mouse.down();
      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-commits', '10:track-press');

      await input.focus();
      await changeInput(input, '23');
      await expect(fixture).toHaveAttribute('data-commits', '10:track-press 23:input-change');
    });

    // SLD-PTR-12, SLD-PTR-11, SLD-EVT-05: the browser's touch pointerdown precedes its touchstart; one commit.
    test('commits a touch track tap', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0', controlled: '1' });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 50, y: box.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await expect(fixture).toHaveAttribute('data-commits', '50:track-press');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', '50:track-press');
    });

    // SLD-PTR-12, SLD-PTR-11, SLD-EVT-05. Base UI's pen case fires an Apple Pencil's compatibility touch events after its
    // pointer events; no real input sends both, so CDP's pen (pointer events only) is the port's case.
    test('port: commits a pen track tap without compatibility touch events', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0', controlled: '1' });
      const box = await origin(fixture);
      const cdp = await page.context().newCDPSession(page);
      await pen(cdp, 'mousePressed', box.x + 50, box.y + 5);
      await pen(cdp, 'mouseReleased', box.x + 50, box.y + 5);
      await expect(fixture).toHaveAttribute('data-commits', '50:track-press');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', '50:track-press');
    });

    // SLD-PTR-12, SLD-EVT-05.
    test('commits a real touch track tap', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0' });
      const control = (await fixture.getByTestId('control').boundingBox())!;
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: control.x + control.width / 2, y: control.y + control.height / 2 }]);
      await touch(cdp, 'touchEnd', []);
      await expect(fixture).toHaveAttribute('data-commits', '50:track-press');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', '50:track-press');
    });

    // SLD-PTR-13. Without `touch-action: none` the browser cancels the touch's pointer once it pans; the title records that
    // the cancel happened, so it proves the touch listeners kept tracking the finger.
    test('keeps tracking a touch drag with touch listeners after the browser cancels the pointer', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0' });
      await page.evaluate(() => {
        const log: string[] = [];
        (window as unknown as { cancels: string[] }).cancels = log;
        document.addEventListener('pointercancel', (event) => log.push(event.pointerType), true);
      });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 50, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 60, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 70, y: box.y + 5 }]);
      await expect.poll(() => lastChange(fixture)).toBe('70:drag:0');
      await touch(cdp, 'touchEnd', []);
      await expect(fixture).toHaveAttribute('data-commits', '70:drag');
      expect(await page.evaluate(() => (window as unknown as { cancels: string[] }).cancels)).toContain('touch');
    });

    // SLD-PTR-13, SLD-PTR-16, SLD-PTR-12, SLD-EVT-09: after the cancelled gesture, a touch press whose pointerdown a
    // JavaScript listener prevents is started by the touch handler (the `touch` case; the cancel is CDP's touchCancel).
    test('handles a touch press normally after a cancelled touch gesture', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0' });
      await preventPointerDownsLater(fixture);
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 50, y: box.y + 5 }]);
      await expect.poll(() => lastChange(fixture)).toBe('50:track-press:0');
      await touch(cdp, 'touchCancel', []);
      await settle(fixture);
      const commits = entries(await fixture.getAttribute('data-commits'));

      await startPreventingPointerDowns(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 30, y: box.y + 5 }]);
      await expect.poll(() => lastChange(fixture)).toBe('30:track-press:0');
      await touch(cdp, 'touchEnd', []);
      await expect.poll(async () => entries(await fixture.getAttribute('data-commits'))).toEqual([...commits, '30:track-press']);
    });

    // As above for Base UI's `mouse` and `pen` cases: no real mouse or pen sends a pointercancel, so the title dispatches
    // the cancel of the real press in the page; the pen is CDP's.
    for (const pointer of ['mouse', 'pen'] as const) {
      test(`port: handles a touch press normally after a cancelled ${pointer} gesture ended by a dispatched pointercancel`, async ({ page, mode }) => {
        const fixture = await open(page, mode, { value: '0' });
        await preventPointerDownsLater(fixture);
        await recordPointerDowns(page);
        const box = await origin(fixture);
        const cdp = await touchSession(page);
        if (pointer === 'mouse') {
          await page.mouse.move(box.x + 50, box.y + 5);
          await page.mouse.down();
        } else {
          await pen(cdp, 'mousePressed', box.x + 50, box.y + 5);
        }
        await expect.poll(() => lastChange(fixture)).toBe('50:track-press:0');
        await dispatchPointerCancel(page);
        if (pointer === 'mouse') {
          await page.mouse.up();
        } else {
          await pen(cdp, 'mouseReleased', box.x + 50, box.y + 5);
        }
        await settle(fixture);
        const commits = entries(await fixture.getAttribute('data-commits'));

        await startPreventingPointerDowns(page);
        await touch(cdp, 'touchStart', [{ x: box.x + 30, y: box.y + 5 }]);
        await expect.poll(() => lastChange(fixture)).toBe('30:track-press:0');
        await touch(cdp, 'touchEnd', []);
        await expect.poll(async () => entries(await fixture.getAttribute('data-commits'))).toEqual([...commits, '30:track-press']);
      });
    }

    // SLD-EVT-05, SLD-PTR-11, SLD-API-04: the press is on the second thumb (centred on 20), so it changes nothing itself.
    test('array value', async ({ page, mode }) => {
      const fixture = await open(page, mode, { values: '10,20' });
      const [thumb1] = await fixture.getByRole('slider').all();
      const box = await origin(fixture);
      await page.mouse.move(box.x + 20, box.y + 5);
      await page.mouse.down();
      await page.mouse.move(box.x + 30, box.y + 5);
      await expect.poll(() => lastChange(fixture)).toBe('[10,30]:drag:1');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', '');

      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-commits', '[10,30]:drag');

      await thumb1!.focus();
      await changeInput(thumb1!, '23');
      await expect(fixture).toHaveAttribute('data-commits', '[10,30]:drag [23,30]:input-change');
    });

    // SLD-EVT-06, SLD-STA-09, SLD-KEY-06.
    test('does not commit when keyboard interaction leaves the value unchanged', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '100' });
      const input = fixture.getByRole('slider');
      await input.focus();
      await page.keyboard.press('ArrowRight');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(fixture).toHaveAttribute('data-commits', '');
      await expect(input).toHaveAttribute('aria-valuenow', '100');
    });

    // SLD-EVT-06, SLD-KEY-07.
    test('does not commit when keyboard interaction leaves a range value unchanged', async ({ page, mode }) => {
      const fixture = await open(page, mode, { values: '50,50' });
      const [slider1, slider2] = await fixture.getByRole('slider').all();
      await slider1!.focus();
      await page.keyboard.press('ArrowRight');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(fixture).toHaveAttribute('data-commits', '');
      await expect(slider1!).toHaveAttribute('aria-valuenow', '50');
      await expect(slider2!).toHaveAttribute('aria-valuenow', '50');
    });

    // SLD-EVT-03: the fixture cancels candidates above 15, Base UI's `cancel = true` after the first press.
    test('does not commit a canceled track press', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0', cancelAbove: '15' });
      const input = fixture.getByRole('slider');
      const box = await origin(fixture);
      await page.mouse.move(box.x + 10, box.y + 5);
      await page.mouse.down();
      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-commits', '10:track-press');
      await expect(input).toHaveAttribute('aria-valuenow', '10');

      await page.mouse.move(box.x + 20, box.y + 5);
      await page.mouse.down();
      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-changes', '10:track-press:0 20:track-press:0');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', '10:track-press');
      await expect(input).toHaveAttribute('aria-valuenow', '10');
    });

    // SLD-EVT-03: the second press is on the thumb (centred on 10), so only the drag proposes a value.
    test('does not commit a canceled drag', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0', cancelAbove: '15' });
      const input = fixture.getByRole('slider');
      const box = await origin(fixture);
      await page.mouse.move(box.x + 10, box.y + 5);
      await page.mouse.down();
      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-commits', '10:track-press');
      await expect(input).toHaveAttribute('aria-valuenow', '10');

      await page.mouse.move(box.x + 10, box.y + 5);
      await page.mouse.down();
      await page.mouse.move(box.x + 20, box.y + 5);
      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-changes', '10:track-press:0 20:drag:0');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', '10:track-press');
      await expect(input).toHaveAttribute('aria-valuenow', '10');
    });

    // SLD-EVT-12, SLD-EVT-03: candidates above 50 are canceled, Base UI's `cancel = true` between the moves.
    test('commits the last applied range value when a later move is canceled', async ({ page, mode }) => {
      const fixture = await open(page, mode, { values: '20,80', cancelAbove: '50' });
      const [slider1, slider2] = await fixture.getByRole('slider').all();
      const box = await origin(fixture);
      await page.mouse.move(box.x + 20, box.y + 5);
      await page.mouse.down();
      await page.mouse.move(box.x + 40, box.y + 5);
      await page.mouse.move(box.x + 60, box.y + 5);
      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-commits', '[40,80]:drag');
      await expect(fixture).toHaveAttribute('data-changes', '[40,80]:drag:0 [60,80]:drag:0');
      await expect(slider1!).toHaveAttribute('aria-valuenow', '40');
      await expect(slider2!).toHaveAttribute('aria-valuenow', '80');
    });
  });

  test.describe('prop: onValueChange', () => {
    // SLD-EVT-02, SLD-KEY-12.
    test('provides the change reason for keyboard interactions', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '40' });
      await fixture.getByRole('slider').focus();
      await page.keyboard.press('ArrowRight');
      await expect(fixture).toHaveAttribute('data-changes', '41:keyboard:0');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '41:keyboard:0');
    });

    // SLD-EVT-02, SLD-PTR-05.
    test('provides the change reason for track presses', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '0' });
      const box = await origin(fixture);
      await page.mouse.move(box.x + 80, box.y + 5);
      await page.mouse.down();
      await expect(fixture).toHaveAttribute('data-changes', '80:track-press:0');
      await page.mouse.up();
    });

    // SLD-PTR-05.
    test('is called when clicking on the control', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '50' });
      const box = await origin(fixture);
      await page.mouse.move(box.x + 41, box.y + 5);
      await page.mouse.down();
      await expect(fixture).toHaveAttribute('data-changes', '41:track-press:0');
      await page.mouse.up();
    });

    // SLD-PTR-05: the thumb spans 45..55.
    test('is not called when clicking on the thumb', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '50' });
      const box = await origin(fixture);
      await page.mouse.move(box.x + 51, box.y + 5);
      await page.mouse.down();
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await page.mouse.up();
    });

    // SLD-PTR-10, SLD-EVT-11, SLD-STA-19. Base UI's thumbs have no width, so its `track press` at 20 misses them; here the
    // press is beside the first thumb (15..25), at 13, which the track press moves to.
    for (const interaction of ['thumb drag', 'track press'] as const) {
      test(`keeps focus and the active index on the logical thumb after a swap from a ${interaction}`, async ({ page, mode }) => {
        const fixture = await open(page, mode, { values: '20,40', collision: 'swap' });
        const inputs = fixture.getByRole('slider');
        const box = await origin(fixture);
        await page.mouse.move(box.x + (interaction === 'thumb drag' ? 20 : 13), box.y + 5);
        await page.mouse.down();
        // The press focuses the first thumb on the next frame (SLD-PTR-07); a user's move comes after it. Base UI's
        // synchronous events assert before that frame, which here could land after the swap and refocus the first thumb.
        await expect(inputs.nth(0)).toBeFocused();
        await page.mouse.move(box.x + 70, box.y + 5);

        await expect(inputs.nth(1)).toBeFocused();
        await expect.poll(() => lastChange(fixture)).toBe('[40,70]:drag:1');

        await page.mouse.up();
        await expect(fixture).toHaveAttribute('data-commits', '[40,70]:drag');
      });
    }

    // SLD-EVT-03, SLD-PTR-10: candidates whose active thumb is above 60 are canceled (Base UI cancels `value[1] === 70`).
    // The rollback returns focus and the pressed thumb to the originally pressed one (js/slider.ts, the revision rule).
    test('keeps the original pressed thumb after a canceled swap and commits the next accepted move', async ({ page, mode }) => {
      const fixture = await open(page, mode, { values: '20,40', collision: 'swap', cancelAbove: '60' });
      const firstInput = fixture.getByRole('slider').first();
      const box = await origin(fixture);
      await firstInput.focus();
      await page.mouse.move(box.x + 20, box.y + 5);
      await page.mouse.down();
      await page.mouse.move(box.x + 70, box.y + 5);
      await expect.poll(() => lastChange(fixture)).toBe('[40,70]:drag:1');
      await settle(fixture);
      await expect(firstInput).toBeFocused();

      await page.mouse.move(box.x + 30, box.y + 5);
      await page.mouse.up();
      await expect.poll(() => lastChange(fixture)).toBe('[30,40]:drag:0');
      await expect(fixture).toHaveAttribute('data-commits', '[30,40]:drag');
    });

    // SLD-EDGE-09, SLD-EVT-04. Base UI's title runs in JSDOM only, where `window.event` is a plain global; the port
    // redefines the browser's `window.event` so that reading it throws, and changes the input.
    test('port: should not rely on the global event when reporting change events', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '3', controlled: '1', follow: '0', name: 'change-testing' });
      const errors: string[] = [];
      page.on('pageerror', (error) => errors.push(error.message));
      await page.evaluate(() => {
        Object.defineProperty(window, 'event', {
          configurable: true,
          get() {
            throw new Error('Should not read the global event');
          },
          set() {
            // Ignores the browser's assignments, so nothing can read the global event.
          },
        });
      });
      const input = fixture.getByRole('slider');
      await input.focus();
      await changeInput(input, '4');
      await expect(fixture).toHaveAttribute('data-changes', '4:input-change:0');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '4:input-change:0');
      expect(errors).toEqual([]);
    });
  });

  test.describe('keyboard interactions', () => {
    const keyboardCases = [
      { direction: 'ltr', orientation: 'horizontal', decrementKeys: ['ArrowLeft', 'ArrowDown'], incrementKeys: ['ArrowRight', 'ArrowUp'] },
      { direction: 'ltr', orientation: 'vertical', decrementKeys: ['ArrowLeft', 'ArrowDown'], incrementKeys: ['ArrowRight', 'ArrowUp'] },
      { direction: 'rtl', orientation: 'horizontal', decrementKeys: ['ArrowRight', 'ArrowDown'], incrementKeys: ['ArrowLeft', 'ArrowUp'] },
      { direction: 'rtl', orientation: 'vertical', decrementKeys: ['ArrowRight', 'ArrowDown'], incrementKeys: ['ArrowLeft', 'ArrowUp'] },
    ] as const;

    /** Tabs to the slider, presses the key (with Shift held when asked) and expects one change to `value`. */
    async function pressAndExpect(page: Page, fixture: Locator, key: string, value: string, shift = false): Promise<void> {
      const input = fixture.getByRole('slider');
      await tabIntoSlider(page, fixture);
      await expect(input).toBeFocused();
      await page.keyboard.press(shift ? `Shift+${key}` : key);
      await expect(fixture).toHaveAttribute('data-changes', `${value}:keyboard:0`);
      await expect(input).toHaveAttribute('aria-valuenow', value);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', `${value}:keyboard:0`);
    }

    // SLD-KEY-02..07, SLD-KEY-10, SLD-KEY-13, SLD-API-08: Base UI's `describe.each(keyboardCases)`, one block per case.
    for (const { direction, orientation, decrementKeys, incrementKeys } of keyboardCases) {
      test.describe(`${direction}, orientation: ${orientation}`, () => {
        const axis = { dir: direction, orientation };

        for (const key of decrementKeys) {
          test.describe(`key: ${key}`, () => {
            test('decrements the value', async ({ page, mode }) => {
              const fixture = await open(page, mode, { ...axis, value: '20' });
              await pressAndExpect(page, fixture, key, '19');
            });

            test('decrements the value by largeStep when Shift is pressed', async ({ page, mode }) => {
              const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '10' });
              await pressAndExpect(page, fixture, key, '10', true);
            });

            test('stops at min when decrementing while Shift is pressed', async ({ page, mode }) => {
              const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '10', min: '15' });
              await pressAndExpect(page, fixture, key, '15', true);
            });
          });
        }

        for (const key of incrementKeys) {
          test.describe(`key: ${key}`, () => {
            test('increments the value', async ({ page, mode }) => {
              const fixture = await open(page, mode, { ...axis, value: '20' });
              await pressAndExpect(page, fixture, key, '21');
            });

            // Also ported at L3 (slider.test.ts, the step algorithm); this is the key's path.
            test('rounds fractional values to the configured step', async ({ page, mode }) => {
              const fixture = await open(page, mode, { ...axis, value: '0.2', min: '0', max: '1', step: '0.1' });
              await pressAndExpect(page, fixture, key, '0.3');
            });

            test('increments the value by largeStep when Shift is pressed', async ({ page, mode }) => {
              const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '10' });
              await pressAndExpect(page, fixture, key, '30', true);
            });

            test('stops at max when incrementing while Shift is pressed', async ({ page, mode }) => {
              const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '10', max: '21' });
              await pressAndExpect(page, fixture, key, '21', true);
            });
          });
        }

        test.describe('key: End', () => {
          test('sets value to max in a single value slider', async ({ page, mode }) => {
            const fixture = await open(page, mode, { ...axis, value: '20', max: '77' });
            await pressAndExpect(page, fixture, 'End', '77');
          });

          test('sets value to the maximum possible value in a range slider', async ({ page, mode }) => {
            const fixture = await open(page, mode, { dir: direction, values: '20,50', max: '77' });
            const [input1, input2] = await fixture.getByRole('slider').all();
            await tabIntoSlider(page, fixture);
            await expect(input1!).toBeFocused();

            await page.keyboard.press('End');
            await expect(fixture).toHaveAttribute('data-changes', '[50,50]:keyboard:0');
            await page.keyboard.press('End');
            await settle(fixture);
            await expect(fixture).toHaveAttribute('data-changes', '[50,50]:keyboard:0');

            await page.keyboard.press('Tab');
            await expect(input2!).toBeFocused();
            await page.keyboard.press('End');
            await expect(fixture).toHaveAttribute('data-changes', '[50,50]:keyboard:0 [50,77]:keyboard:1');
          });
        });

        test.describe('key: Home', () => {
          test('sets value to min in a single value slider', async ({ page, mode }) => {
            const fixture = await open(page, mode, { ...axis, value: '20', min: '17' });
            await pressAndExpect(page, fixture, 'Home', '17');
          });

          test('sets value to the minimum possible value in a range slider', async ({ page, mode }) => {
            const fixture = await open(page, mode, { dir: direction, values: '20,50', min: '7' });
            const [input1, input2] = await fixture.getByRole('slider').all();
            await tabIntoSlider(page, fixture);
            await page.keyboard.press('Tab');
            await expect(input2!).toBeFocused();

            await page.keyboard.press('Home');
            await expect(fixture).toHaveAttribute('data-changes', '[20,20]:keyboard:1');
            await page.keyboard.press('Home');
            await settle(fixture);
            await expect(fixture).toHaveAttribute('data-changes', '[20,20]:keyboard:1');

            await page.keyboard.press('Shift+Tab');
            await expect(input1!).toBeFocused();
            await page.keyboard.press('Home');
            await expect(fixture).toHaveAttribute('data-changes', '[20,20]:keyboard:1 [7,20]:keyboard:0');
          });
        });

        test.describe('key: PageUp', () => {
          test('increments the value by largeStep', async ({ page, mode }) => {
            const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '5' });
            await pressAndExpect(page, fixture, 'PageUp', '25');
          });

          // Also ported at L3 (slider.test.ts, the step algorithm); this is the key's path.
          test('preserves largeStep increments when step uses a different grid', async ({ page, mode }) => {
            const fixture = await open(page, mode, { ...axis, value: '20', step: '2', largeStep: '5' });
            await pressAndExpect(page, fixture, 'PageUp', '25');
          });

          test('does not exceed max', async ({ page, mode }) => {
            const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '5', max: '21' });
            await pressAndExpect(page, fixture, 'PageUp', '21');
          });
        });

        test.describe('key: PageDown', () => {
          test('decrements the value by largeStep', async ({ page, mode }) => {
            const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '5' });
            await pressAndExpect(page, fixture, 'PageDown', '15');
          });

          test('does not go below min', async ({ page, mode }) => {
            const fixture = await open(page, mode, { ...axis, value: '20', largeStep: '5', min: '17' });
            await pressAndExpect(page, fixture, 'PageDown', '17');
          });
        });
      });
    }
  });
});

/** Adds a capture pointerdown listener on the Control that prevents the press once the page asks it to. */
async function preventPointerDownsLater(fixture: Locator): Promise<void> {
  await fixture.getByTestId('control').evaluate((control) => {
    control.addEventListener(
      'pointerdown',
      (event) => {
        if ((window as unknown as { preventPointerDown?: boolean }).preventPointerDown) {
          event.preventDefault();
        }
      },
      true,
    );
  });
}

/** Base UI's `preventPointerDown = true`. */
async function startPreventingPointerDowns(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { preventPointerDown?: boolean }).preventPointerDown = true;
  });
}

test.describe('<Slider.Thumb />', () => {
  // SLD-API-22, SLD-KEY-01, SLD-KEY-09, SLD-EVT-09: the thumb's C# `onkeydown` (the fixture's `keydown=1`), recorded as
  // `data-keys`. Keys are real `page.keyboard` input on the focused input.
  test.describe('prop: onKeyDown', () => {
    async function keyDown(input: Locator, key: string): Promise<void> {
      await input.page().keyboard.press(key);
    }

    test('forwards key events that the slider does not handle', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '50', keydown: '1' });
      const slider = fixture.getByRole('slider');
      await slider.focus();
      await keyDown(slider, 'Enter');
      await expect(fixture).toHaveAttribute('data-keys', 'Enter');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-keys', 'Enter');
    });

    for (const key of ['ArrowRight', 'PageUp']) {
      test(`forwards handled ${key} key events`, async ({ page, mode }) => {
        const fixture = await open(page, mode, { value: '50', keydown: '1' });
        const slider = fixture.getByRole('slider');
        await slider.focus();
        await keyDown(slider, key);
        await expect(fixture).toHaveAttribute('data-keys', key);
        await settle(fixture);
        await expect(fixture).toHaveAttribute('data-keys', key);
      });
    }

    // SLD-KEY-01, SLD-EVT-09: the C# handler's `PreventPartHandler()` is Base UI's `preventBaseUIHandler()` (EVD-API-11),
    // which skips the part's handling and leaves the browser's own default: on WebAssembly the `key` decision stops the
    // module, so the range input steps natively and the module reports it as `input-change`; on Server the handler runs a
    // round trip after the module handled the key (SLD §9). Base UI's title calls `preventDefault()`, which a C# handler
    // cannot; a JavaScript listener's `preventDefault()` blocks both on both modes (slider.relaxations.spec.ts).
    test('port: allows preventing the internal key handling', async ({ page, mode }) => {
      const fixture = await open(page, mode, { value: '50', keydown: '1', prevent: '1' });
      const slider = fixture.getByRole('slider');
      await slider.focus();
      await keyDown(slider, 'ArrowRight');
      await expect(fixture).toHaveAttribute('data-keys', 'ArrowRight');
      await settle(fixture);
      await expect(slider).toHaveAttribute('aria-valuenow', '51');
      await expect(fixture).toHaveAttribute('data-changes', mode === 'wasm' ? '51:input-change:0' : '51:keyboard:0');
    });
  });
});
