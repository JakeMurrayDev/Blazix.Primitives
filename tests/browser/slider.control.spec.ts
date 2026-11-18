import { test, expect, Fixture, fixtureUrl, type Page, type RenderMode } from './helpers';
import type { CDPSession, Locator } from '@playwright/test';

// SLD §6.2 (docs/components/slider.md): the Control's pointer and touch gestures, the thumb's grab offset, stacking and
// inset positions, at L4 per render mode. Base UI mocks the Control's rectangle (0..100 px); the fixtures give the parts
// fixed pixel sizes instead, so a coordinate here is measured from the Track's box (the Control's content box) and
// Base UI's numbers carry over (its 1000 px sliders are scaled to 100 px). Pointer input is real `page.mouse`; touch
// input is CDP `Input.dispatchTouchEvent` with touch emulation, which also sends the touch's pointer events
// (select-lifecycle.spec.ts's precedent). Readiness is the fixture's `data-attached`.

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

/** The Track's box: the Control's content box in every fixture, the origin of Base UI's coordinates. */
async function origin(fixture: Locator): Promise<{ x: number; y: number; width: number; height: number }> {
  return (await fixture.getByTestId('track').boundingBox())!;
}

/** The sync barrier: a DOM click on the fixture's Sync button, answered once C# has handled every earlier message. */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((button) => (button as HTMLButtonElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

/** Presses a fixture control (not the gesture under test) with a DOM click, so a held touch or mouse is untouched. */
async function pressControl(control: Locator): Promise<void> {
  await control.evaluate((button) => (button as HTMLButtonElement).click());
}

async function touchSession(page: Page): Promise<CDPSession> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 2 });
  return cdp;
}

type TouchType = 'touchStart' | 'touchMove' | 'touchEnd' | 'touchCancel';

async function touch(cdp: CDPSession, type: TouchType, points: Array<{ x: number; y: number; id?: number }>): Promise<void> {
  await cdp.send('Input.dispatchTouchEvent', { type, touchPoints: points });
}

function changes(fixture: Locator): Promise<string | null> {
  return fixture.getAttribute('data-changes');
}

async function lastChange(fixture: Locator): Promise<string | undefined> {
  return ((await changes(fixture)) ?? '').split(' ').filter(Boolean).at(-1);
}

async function valuesOf(fixture: Locator): Promise<string[]> {
  return fixture.getByRole('slider').evaluateAll((inputs) => inputs.map((input) => input.getAttribute('aria-valuenow') ?? ''));
}

function inlineStyle(element: Locator, property: string): Promise<string> {
  return element.evaluate((node, name) => (node as HTMLElement).style.getPropertyValue(name), property);
}

test.describe('<Slider.Control />', () => {
  // SLD-PTR-18, SLD-PTR-03 (a), SLD-EVT-11.
  test('moves the first of several thumbs stacked at the maximum', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-range', { values: '100,100,100' });
    const box = await origin(fixture);
    // The last thumb is on top of the stack (DOM order); its midpoint is the control's end.
    await page.mouse.move(box.x + 100, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 50, box.y + 5);
    await expect.poll(() => lastChange(fixture)).toBe('[50,100,100]:drag:0');
    await page.mouse.up();
  });

  // SLD-PTR-10, SLD-PTR-03, SLD-EDGE-11: the swap lands on a thumb with no element, so the grab offset is dropped.
  test('clears the grabbed offset when swapping to a thumb that is not rendered', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-range', { values: '20,40', collision: 'swap', thumbs: '1' });
    const box = await origin(fixture);
    // A grab 4 px right of the thumb's midpoint (20).
    await page.mouse.move(box.x + 24, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 70, box.y + 5);
    await page.mouse.move(box.x + 80, box.y + 5);
    await expect.poll(() => lastChange(fixture)).toBe('[40,80]:drag:1');
    await page.mouse.up();
  });

  // SLD-PTR-09, SLD-STA-17.
  test('ignores a drag when collision behavior cannot satisfy the minimum distance', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-range', { values: '20,40', collision: 'none', minSteps: '50' });
    const box = await origin(fixture);
    await page.mouse.move(box.x + 20, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 80, box.y + 5);
    await page.mouse.up();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-changes', '');
    expect(await valuesOf(fixture)).toEqual(['20', '40']);
  });

  // SLD-PTR-24, SLD-PTR-04, SLD-API-11, SLD-ANI-04: a 20 px thumb in a 100 px control; the press 10 px from the start
  // edge is the thumb's half-size, so it is the minimum.
  for (const { name, query, point } of [
    { name: 'horizontal', query: {}, point: { x: 10, y: 5 } },
    { name: 'vertical', query: { orientation: 'vertical' }, point: { x: 5, y: 90 } },
  ]) {
    test(`accounts for the thumb size when pressing an inset ${name} control`, async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-inset', { alignment: 'edge-client-only', values: '50', thumb: '20', ...query });
      await expect(fixture.getByTestId('thumb-0')).toBeVisible();
      const box = await origin(fixture);
      await page.mouse.move(box.x + point.x, box.y + point.y);
      await page.mouse.down();
      await expect(fixture).toHaveAttribute('data-changes', '0:track-press:0');
      await page.mouse.up();
    });
  }

  // SLD-STA-04, SLD-API-02, SLD-PTR-05.
  test('preserves single-element array values on track press', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-range', { values: '25' });
    const box = await origin(fixture);
    await page.mouse.move(box.x + 50, box.y + 5);
    await page.mouse.down();
    await page.mouse.up();
    await expect(fixture).toHaveAttribute('data-changes', '[50]:track-press:0');
    await expect(fixture).toHaveAttribute('data-commits', '[50]:track-press');
  });

  // SLD-PTR-06, SLD-PTR-11: the Control's capture calls are recorded; the input is a real mouse press and release.
  test('releases pointer capture when the interaction ends', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-basic', { value: '20' });
    const control = fixture.getByTestId('control');
    await control.evaluate((node) => {
      const log: string[] = [];
      (window as unknown as { captures: string[] }).captures = log;
      const element = node as HTMLElement;
      const set = element.setPointerCapture.bind(element);
      const release = element.releasePointerCapture.bind(element);
      element.setPointerCapture = (id: number) => {
        log.push(`set:${id}`);
        set(id);
      };
      element.releasePointerCapture = (id: number) => {
        log.push(`release:${id}`);
        release(id);
      };
    });
    const box = await origin(fixture);
    await page.mouse.move(box.x + 40, box.y + 5);
    await page.mouse.down();
    await page.mouse.up();
    await expect(fixture).toHaveAttribute('data-commits', '40:track-press');
    const captures = await page.evaluate(() => (window as unknown as { captures: string[] }).captures);
    expect(captures).toHaveLength(2);
    expect(captures[0]).toMatch(/^set:\d+$/);
    expect(captures[1]).toBe(captures[0]!.replace('set', 'release'));
  });

  // SLD-PTR-12, SLD-PTR-03: no thumb, so the press resolves no thumb and nothing happens.
  test('ignores touch interactions when no thumbs are composed', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-range', { values: '20', thumbs: '0' });
    const box = await origin(fixture);
    const cdp = await touchSession(page);
    await touch(cdp, 'touchStart', [{ x: box.x + 60, y: box.y + 5 }]);
    await touch(cdp, 'touchMove', [{ x: box.x + 80, y: box.y + 5 }]);
    await touch(cdp, 'touchEnd', []);
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-changes', '');
    await expect(fixture).toHaveAttribute('data-commits', '');
  });

  test.describe('cancelled gestures', () => {
    // SLD-PTR-13, SLD-PTR-11. Without `touch-action: none` the browser cancels the touch's pointer once it pans; the
    // title records that the cancel happened, so it proves the touch listeners kept the drag.
    test('keeps a touch drag going after pointercancel and commits and ends it on touchcancel', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-disabled', { value: '20' });
      const control = fixture.getByTestId('control');
      await page.evaluate(() => {
        const log: string[] = [];
        (window as unknown as { cancels: string[] }).cancels = log;
        document.addEventListener('pointercancel', (event) => log.push(event.pointerType), true);
      });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 40, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 55, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 70, y: box.y + 5 }]);
      await expect.poll(() => lastChange(fixture)).toBe('70:drag:0');
      await touch(cdp, 'touchCancel', []);
      await expect(fixture).toHaveAttribute('data-commits', '70:drag');
      await expect(control).not.toHaveAttribute('data-dragging');
      expect(await page.evaluate(() => (window as unknown as { cancels: string[] }).cancels)).toContain('touch');

      // A later touch elsewhere changes nothing.
      const elsewhere = (await fixture.getByTestId('elsewhere').boundingBox())!;
      await touch(cdp, 'touchStart', [{ x: elsewhere.x + 90, y: elsewhere.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: elsewhere.x + 95, y: elsewhere.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '40:track-press:0 55:drag:0 70:drag:0');
      await expect(fixture).toHaveAttribute('data-commits', '70:drag');
    });

    // SLD-PTR-12, SLD-PTR-13. The first finger's move pans, so the browser cancels its pointer and sends the second
    // finger no pointer events (only touch events, as Base UI fires). Only the `touchEnd` case: CDP's touchCancel
    // cancels every finger.
    test('ignores touchEnd from another finger during a touch drag', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-disabled', { value: '20' });
      const control = fixture.getByTestId('control');
      const box = await origin(fixture);
      const elsewhere = (await fixture.getByTestId('elsewhere').boundingBox())!;
      const first = (x: number) => ({ x: box.x + x, y: box.y + 5, id: 1 });
      const second = { x: elsewhere.x + 90, y: elsewhere.y + 5, id: 2 };
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [first(40)]);
      await touch(cdp, 'touchMove', [first(70)]);
      await touch(cdp, 'touchStart', [first(70), second]);
      await touch(cdp, 'touchEnd', [second]);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', '');
      await expect(control).toHaveAttribute('data-dragging', '');

      await touch(cdp, 'touchMove', [first(80)]);
      await expect.poll(() => lastChange(fixture)).toBe('80:drag:0');

      await touch(cdp, 'touchCancel', []);
      await expect(fixture).toHaveAttribute('data-commits', '80:drag');
      await expect(control).not.toHaveAttribute('data-dragging');
    });

    // SLD-EDGE-15, SLD-PTR-17, SLD-PTR-23, SLD-PTR-11. Base UI ends the mouse drag with a pointercancel, which a real
    // mouse cannot send; the release ends it here.
    test('port: ends a mouse drag after disabling and re-enabling during a touch drag', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-disabled', { value: '20' });
      const root = fixture.getByTestId('root');
      const control = fixture.getByTestId('control');
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 40, y: box.y + 5 }]);
      await expect(fixture).toHaveAttribute('data-changes', '40:track-press:0');
      await pressControl(fixture.getByTestId('toggle-disabled'));
      await expect(root).toHaveAttribute('data-disabled', '');
      await touch(cdp, 'touchCancel', []);
      await pressControl(fixture.getByTestId('toggle-disabled'));
      await expect(root).not.toHaveAttribute('data-disabled');
      await expect(fixture).toHaveAttribute('data-commits', '');

      await page.mouse.move(box.x + 50, box.y + 5);
      await page.mouse.down();
      await page.mouse.move(box.x + 70, box.y + 5);
      await page.mouse.up();
      await expect(fixture).toHaveAttribute('data-commits', '70:drag');
      await expect(control).not.toHaveAttribute('data-dragging');

      const elsewhere = (await fixture.getByTestId('elsewhere').boundingBox())!;
      await page.mouse.move(elsewhere.x + 90, elsewhere.y + 5);
      await page.mouse.down();
      await page.mouse.up();
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '40:track-press:0 50:track-press:0 70:drag:0');
      await expect(fixture).toHaveAttribute('data-commits', '70:drag');
    });
  });

  // SLD-PTR-19, SLD-EDGE-12: the controlled values do not follow the changes, so the pressed index (2) leaves the range.
  test('does not resurrect a removed thumb value when the range shrinks mid-drag', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-controlled');
    const box = await origin(fixture);
    await page.mouse.move(box.x + 30, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 50, box.y + 5);
    await expect(fixture).toHaveAttribute('data-changes', '[10,20,50]:drag:2');

    await pressControl(fixture.getByTestId('shrink'));
    await expect(fixture.getByRole('slider')).toHaveCount(2);

    await page.mouse.move(box.x + 55, box.y + 5);
    await page.mouse.up();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-changes', '[10,20,50]:drag:2');
    await expect(fixture).toHaveAttribute('data-commits', '');
  });

  // SLD-PTR-19, SLD-PTR-11, SLD-EDGE-12.
  test('does not commit a stale value when the range shrinks mid-drag and the pressed thumb stays valid', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-controlled');
    const box = await origin(fixture);
    await page.mouse.move(box.x + 20, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 25, box.y + 5);
    await expect(fixture).toHaveAttribute('data-changes', '[10,25,30]:drag:1');

    await pressControl(fixture.getByTestId('shrink'));
    await expect(fixture.getByRole('slider')).toHaveCount(2);

    await page.mouse.up();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-commits', '');
  });

  // SLD-PTR-19, SLD-PTR-23, SLD-EDGE-12.
  test('clears cached interaction state when the controlled range grows mid-drag', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-controlled', { values: '10,20', follow: '1' });
    const box = await origin(fixture);
    await page.mouse.move(box.x + 20, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 40, box.y + 5);
    await expect(fixture).toHaveAttribute('data-changes', '[10,40]:drag:1');

    await pressControl(fixture.getByTestId('grow'));
    await expect(fixture.getByRole('slider')).toHaveCount(3);
    await page.mouse.up();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-commits', '');

    await page.mouse.move(box.x + 30, box.y + 5);
    await page.mouse.down();
    await page.mouse.move(box.x + 80, box.y + 5);
    await page.mouse.up();
    await expect(fixture).toHaveAttribute('data-commits', '[10,20,80]:drag');
    expect(await lastChange(fixture)).toBe('[10,20,80]:drag:2');
  });

  // SLD-STA-08, SLD-PTR-08, SLD-PTR-11.
  test('port: a mouse press sets data-dragging until the release', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-basic', { value: '20' });
    const control = fixture.getByTestId('control');
    const box = await origin(fixture);
    await expect(control).not.toHaveAttribute('data-dragging');
    await page.mouse.move(box.x + 40, box.y + 5);
    await page.mouse.down();
    await expect(control).toHaveAttribute('data-dragging', '');
    await expect(fixture.getByTestId('root')).toHaveAttribute('data-dragging', '');
    await expect(fixture.getByTestId('thumb')).toHaveAttribute('data-dragging', '');
    await page.mouse.move(box.x + 60, box.y + 5);
    await page.mouse.up();
    await expect(control).not.toHaveAttribute('data-dragging');
    await expect(fixture).toHaveAttribute('data-commits', '60:drag');
  });
});

test.describe('<Slider.Thumb />', () => {
  // SLD-PTR-02, SLD-PTR-04: the vertical control's border and unequal padding are subtracted (SLD-PTR-04's offsets).
  test('preserves the grab offset when dragging a vertical thumb', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-vertical', { value: '50' });
    const box = await origin(fixture);
    // A grab 8 px below the thumb's midpoint (50 px from the top of the 100 px content box).
    await page.mouse.move(box.x + 5, box.y + 58);
    await page.mouse.down();
    await page.mouse.move(box.x + 5, box.y + 78);
    await expect.poll(() => lastChange(fixture)).toBe('30:drag:0');
    await page.mouse.up();
    await expect(fixture).toHaveAttribute('data-changes', '30:drag:0');
  });

  test.describe('stacking order', () => {
    // SLD-DOM-12, SLD-STA-07.
    test('relies on DOM order before any thumb is used', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-range', { values: '20,20' });
      expect(await inlineStyle(fixture.getByTestId('thumb-0'), 'z-index')).toBe('');
      expect(await inlineStyle(fixture.getByTestId('thumb-1'), 'z-index')).toBe('');
    });

    // SLD-DOM-12, SLD-STA-06, SLD-STA-07, SLD-FOC-03. Focus leaves for the fixture's After button rather than the body.
    test('keeps the most recently active thumb on top after focus moves away', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-range', { values: '20,20' });
      const [thumb0, thumb1] = [fixture.getByTestId('thumb-0'), fixture.getByTestId('thumb-1')];
      const inputs = fixture.getByRole('slider');
      await fixture.locator('[data-sync-button]').focus();

      await page.keyboard.press('Tab');
      await expect(inputs.nth(0)).toBeFocused();
      await expect.poll(() => inlineStyle(thumb0, 'z-index')).toBe('2');

      await page.keyboard.press('Tab');
      await expect(inputs.nth(1)).toBeFocused();
      await expect.poll(() => inlineStyle(thumb1, 'z-index')).toBe('2');

      await page.keyboard.press('Tab');
      await expect(fixture.getByTestId('after')).toBeFocused();
      await expect.poll(() => inlineStyle(thumb1, 'z-index')).toBe('1');
      expect(await inlineStyle(thumb0, 'z-index')).toBe('');
    });
  });

  test.describe('prop: thumbAlignment', () => {
    // SLD-ANI-01, SLD-ANI-02, SLD-DOM-10, SLD-DOM-06, SLD-API-11.
    test('recomputes inset positions when the slider becomes visible', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-inset', { hidden: '1', values: '30' });
      const thumb = fixture.getByTestId('thumb-0');
      const indicator = fixture.getByTestId('indicator');
      const read = async () => ({
        thumbVisibility: await inlineStyle(thumb, 'visibility'),
        thumbPosition: await inlineStyle(thumb, '--position'),
        indicatorVisibility: await inlineStyle(indicator, 'visibility'),
        indicatorStartPosition: await inlineStyle(indicator, '--start-position'),
      });
      await expect
        .poll(read)
        .toEqual({ thumbVisibility: 'hidden', thumbPosition: '0%', indicatorVisibility: 'hidden', indicatorStartPosition: '0%' });

      await fixture.getByTestId('show').click();

      await expect
        .poll(read)
        .toEqual({ thumbVisibility: '', thumbPosition: '32%', indicatorVisibility: '', indicatorStartPosition: '32%' });
    });

    // SLD-ANI-01, SLD-ANI-02, SLD-DOM-10, SLD-DOM-06.
    test('recomputes range inset positions when the slider becomes visible', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-inset', { hidden: '1', values: '30,70' });
      const start = fixture.getByTestId('thumb-0');
      const end = fixture.getByTestId('thumb-1');
      const indicator = fixture.getByTestId('indicator');
      const read = async () => ({
        startThumbVisibility: await inlineStyle(start, 'visibility'),
        startThumbPosition: await inlineStyle(start, '--position'),
        endThumbVisibility: await inlineStyle(end, 'visibility'),
        endThumbPosition: await inlineStyle(end, '--position'),
        indicatorVisibility: await inlineStyle(indicator, 'visibility'),
        indicatorStartPosition: await inlineStyle(indicator, '--start-position'),
        indicatorRelativeSize: await inlineStyle(indicator, '--relative-size'),
      });
      await expect.poll(read).toEqual({
        startThumbVisibility: 'hidden',
        startThumbPosition: '0%',
        endThumbVisibility: 'hidden',
        endThumbPosition: '0%',
        indicatorVisibility: 'hidden',
        indicatorStartPosition: '0%',
        indicatorRelativeSize: '0%',
      });

      await fixture.getByTestId('show').click();

      await expect.poll(read).toEqual({
        startThumbVisibility: '',
        startThumbPosition: '32%',
        endThumbVisibility: '',
        endThumbPosition: '68%',
        indicatorVisibility: '',
        indicatorStartPosition: '32%',
        indicatorRelativeSize: '36%',
      });
    });
  });

  test.describe('positioning styles', () => {
    test.describe('positions the thumb when dragged', () => {
      // SLD-DOM-09, SLD-STA-20. Base UI's 1000 px slider at a tenth of the size; the press is beside the 10 px thumb.
      test('single thumb', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-basic', { value: '0' });
        const thumb = fixture.getByTestId('thumb');
        const box = await origin(fixture);
        const cdp = await touchSession(page);
        await touch(cdp, 'touchStart', [{ x: box.x + 7, y: box.y + 5 }]);
        for (let i = 0; i < 3; i += 1) {
          await touch(cdp, 'touchMove', [{ x: box.x + 19.9, y: box.y + 5 }]);
        }
        await expect(thumb).toHaveCSS('left', '20px');
        await touch(cdp, 'touchEnd', []);
        await expect(fixture).toHaveAttribute('data-commits', '20:drag');
        await expect(thumb).toHaveCSS('left', '20px');
      });

      // SLD-DOM-09, SLD-STA-20.
      test('multiple thumbs', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-range', { values: '20,40' });
        const [thumb0, thumb1] = [fixture.getByTestId('thumb-0'), fixture.getByTestId('thumb-1')];
        const box = await origin(fixture);
        const cdp = await touchSession(page);
        await touch(cdp, 'touchStart', [{ x: box.x + 40, y: box.y + 5 }]);
        for (let i = 0; i < 3; i += 1) {
          await touch(cdp, 'touchMove', [{ x: box.x + 69.9, y: box.y + 5 }]);
        }
        await expect(thumb1).toHaveCSS('left', '70px');
        await touch(cdp, 'touchEnd', []);
        await expect(fixture).toHaveAttribute('data-commits', '[20,70]:drag');
        await expect(thumb0).toHaveCSS('left', '20px');
        await expect(thumb1).toHaveCSS('left', '70px');
      });

      test.describe('prop: thumbCollisionBehavior', () => {
        async function drag(page: Page, fixture: Locator, from: number, moves: number[]): Promise<void> {
          const box = await origin(fixture);
          const cdp = await touchSession(page);
          await touch(cdp, 'touchStart', [{ x: box.x + from, y: box.y + 5 }]);
          for (const to of moves) {
            await touch(cdp, 'touchMove', [{ x: box.x + to, y: box.y + 5 }]);
          }
          await touch(cdp, 'touchEnd', []);
          await expect(fixture).not.toHaveAttribute('data-commits', '');
        }

        // SLD-API-12, SLD-STA-19.
        test('prevents thumbs from passing each other when set to "none"', async ({ page, mode }) => {
          const fixture = await open(page, mode, 'slider-range', { values: '20,40', collision: 'none' });
          await drag(page, fixture, 20, [60]);
          await expect.poll(() => valuesOf(fixture)).toEqual(['40', '40']);
        });

        // SLD-API-12, SLD-STA-18, SLD-STA-19.
        test('pushes adjacent thumbs forward when set to "push"', async ({ page, mode }) => {
          const fixture = await open(page, mode, 'slider-range', { values: '20,40', collision: 'push' });
          await drag(page, fixture, 20, [65]);
          await expect.poll(() => valuesOf(fixture)).toEqual(['65', '65']);
        });

        // SLD-API-12, SLD-PTR-10, SLD-STA-19.
        test('allows thumbs to swap when set to "swap"', async ({ page, mode }) => {
          const fixture = await open(page, mode, 'slider-range', { values: '20,40', collision: 'swap' });
          await drag(page, fixture, 20, [70]);
          await expect.poll(() => valuesOf(fixture)).toEqual(['40', '70']);
        });

        // SLD-API-12, SLD-PTR-10, SLD-STA-19.
        test('maintains minimum steps between values when swapping', async ({ page, mode }) => {
          const fixture = await open(page, mode, 'slider-range', { values: '20,40,60', collision: 'swap', minSteps: '10' });
          await drag(page, fixture, 20, [50, 55, 80]);
          await expect.poll(() => valuesOf(fixture)).toEqual(['30', '50', '80']);
        });
      });
    });
  });
});

test.describe('<Slider.Root />', () => {
  test.describe('rtl', () => {
    // SLD-PTR-04, SLD-I18N-01, SLD-DOM-09: the inline start is the right edge, its border and padding subtracted.
    test('should handle RTL', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-rtl', { value: '30' });
      const thumb = fixture.getByTestId('thumb');
      expect(await inlineStyle(thumb, 'inset-inline-start')).toBe('30%');
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 20, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 22, y: box.y + 5 }]);
      await expect(fixture).toHaveAttribute('data-changes', '80:track-press:0 78:drag:0');
      await touch(cdp, 'touchEnd', []);
      await expect(fixture).toHaveAttribute('data-commits', '78:drag');
    });
  });

  test.describe('prop: disabled', () => {
    // SLD-PTR-17, SLD-PTR-12.
    test('should not respond to drag events if disabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-disabled', { value: '21', disabled: '1' });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 21, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 30, y: box.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await settle(fixture);
      await expect(fixture.getByRole('slider')).toHaveAttribute('aria-valuenow', '21');
      await expect(fixture).toHaveAttribute('data-changes', '');
    });

    // SLD-PTR-17, SLD-EDGE-15.
    test('should not respond to drag events after becoming disabled', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-disabled', { value: '0' });
      const input = fixture.getByRole('slider');
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 21, y: box.y + 5 }]);
      await expect(input).toHaveAttribute('aria-valuenow', '21');
      await expect(input).toBeFocused();

      await pressControl(fixture.getByTestId('toggle-disabled'));
      await expect(fixture.getByTestId('root')).toHaveAttribute('data-disabled', '');
      await expect(input).not.toBeFocused();

      await touch(cdp, 'touchMove', [{ x: box.x + 30, y: box.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await settle(fixture);
      await expect(input).toHaveAttribute('aria-valuenow', '21');
      await expect(fixture).toHaveAttribute('data-commits', '');
    });

    // SLD-PTR-01, SLD-API-16.
    test('does not drag a thumb disabled via the `disabled` prop', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-thumb-disabled');
      const disabledInput = fixture.getByTestId('thumb-1').locator('input');
      await expect(disabledInput).toBeDisabled();
      const box = await origin(fixture);
      await page.mouse.move(box.x + 80, box.y + 5);
      await page.mouse.down();
      await page.mouse.move(box.x + 40, box.y + 5);
      await page.mouse.up();
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(disabledInput).toHaveAttribute('aria-valuenow', '80');
    });

    // SLD-PTR-03 (b), SLD-PTR-01.
    test('does not select a disabled thumb when pressing the track', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-thumb-disabled', { collision: 'none' });
      const [, disabledInput] = await fixture.getByRole('slider').all();
      await expect(disabledInput!).toBeDisabled();
      const box = await origin(fixture);
      // The press is closest to the disabled thumb (at 80).
      await page.mouse.move(box.x + 95, box.y + 5);
      await page.mouse.down();
      await page.mouse.up();
      await expect.poll(() => lastChange(fixture)).toMatch(/:track-press:0$/);
      await expect(disabledInput!).toHaveAttribute('aria-valuenow', '80');
    });
  });

  test.describe('prop: orientation', () => {
    // SLD-PTR-04: the vertical control's border and unequal padding are subtracted.
    test('should report the right position', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-vertical', { value: '20' });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 5, y: box.y + 20 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 5, y: box.y + 22 }]);
      await expect(fixture).toHaveAttribute('data-changes', '80:track-press:0 78:drag:0');
      await touch(cdp, 'touchEnd', []);
    });
  });

  test.describe('events', () => {
    // SLD-PTR-12, SLD-PTR-09, SLD-PTR-02: each gesture starts on thumb 0, so only the moves change the value; the last
    // move rounds to the value it already has.
    test('should support touch events', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-range', { values: '20,30' });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      for (const [from, to] of [
        [20, 21],
        [21, 22],
        [22, 22.1],
      ] as const) {
        await touch(cdp, 'touchStart', [{ x: box.x + from, y: box.y + 5 }]);
        await touch(cdp, 'touchMove', [{ x: box.x + to, y: box.y + 5 }]);
        await touch(cdp, 'touchEnd', []);
      }
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '[21,30]:drag:0 [22,30]:drag:0');
    });

    // SLD-PTR-09: a pointerup consumed before it reaches the document (stopped in the window's capture phase, as
    // another element's handler can) leaves the drag open; the next button-less move ends it.
    test('should hedge against a dropped mouseup event', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-basic', { value: '50' });
      const box = await origin(fixture);
      await page.mouse.move(box.x + 1, box.y + 5);
      await page.mouse.down();
      await expect(fixture).toHaveAttribute('data-changes', '1:track-press:0');
      await page.mouse.move(box.x + 10, box.y + 5);
      await expect(fixture).toHaveAttribute('data-changes', '1:track-press:0 10:drag:0');

      await page.evaluate(() => {
        window.addEventListener('pointerup', (event) => event.stopPropagation(), { capture: true, once: true });
      });
      await page.mouse.up();
      await page.mouse.move(box.x + 11, box.y + 5);
      await expect(fixture).toHaveAttribute('data-commits', '10:drag');
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '1:track-press:0 10:drag:0');
    });

    // SLD-PTR-07.
    test('should focus the slider when dragging', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-basic', { value: '30' });
      const box = await origin(fixture);
      await page.mouse.move(box.x + 30, box.y + 5);
      await page.mouse.down();
      await expect(fixture.getByRole('slider')).toBeFocused();
      await page.mouse.up();
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
    });

    // SLD-PTR-16.
    test('should not react to right clicks', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-basic', { value: '50' });
      const box = await origin(fixture);
      await page.mouse.move(box.x + 41, box.y + 5);
      await page.mouse.down({ button: 'right' });
      await page.mouse.up({ button: 'right' });
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(fixture.getByRole('slider')).not.toBeFocused();
    });

    // SLD-PTR-15, SLD-PTR-05: a tap 3 px off the thumb's midpoint, which a track press would turn into 53.
    test('does not change the value on a touch tap on the thumb', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-basic', { value: '50' });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 53, y: box.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(fixture).toHaveAttribute('data-commits', '');
    });

    // SLD-PTR-15, SLD-PTR-05: Base UI's `pen` case. Its pen also fires compatibility touch events; CDP's pen sends pointer
    // events only, so the title is the port's.
    test('port: does not change the value on a pen tap on the thumb without compatibility touch events', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-basic', { value: '50' });
      const box = await origin(fixture);
      const cdp = await page.context().newCDPSession(page);
      await pen(cdp, 'mousePressed', box.x + 53, box.y + 5);
      await pen(cdp, 'mouseReleased', box.x + 53, box.y + 5);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(fixture).toHaveAttribute('data-commits', '');
    });

    // SLD-PTR-02, SLD-PTR-03 (a).
    test('drags the intended thumb when 3 thumbs are present', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-range', { values: '10,40,60' });
      const box = await origin(fixture);
      await page.mouse.move(box.x + 60, box.y + 5);
      await page.mouse.down();
      await page.mouse.move(box.x + 80, box.y + 5);
      await expect.poll(() => lastChange(fixture)).toBe('[10,40,80]:drag:2');
      await page.mouse.up();
    });
  });
});

// SliderRoot.test.tsx's touch, disabled and dragging-state titles, SliderControl.test.tsx's remaining Control titles,
// SliderThumb.test.tsx's controlled positioning titles and SliderIndicator.test.tsx's parity matrix, on the
// `slider-config` fixture (its header) unless a title names another. Base UI's touch titles fire touch events alone; a
// real touch also sends pointer events first, which start the gesture as a pointer press (SLD-PTR-12), so a title whose
// point is the touch-only path dispatches `TouchEvent`s in the page instead (tests/browser/CONTEXT.md's dated note of
// 2026-11-11, the context-menu precedent) and is a `port:` title; the others use real CDP touch.

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

type DispatchedTouch = { id: number; x: number; y: number };

/**
 * Dispatches a `TouchEvent` of the given points as Base UI's `fireEvent.touch*(target, createTouches(points))`: on the
 * element (`body` for the document's body), with the points as its `changedTouches` and `touches`.
 */
async function dispatchTouch(target: Locator, type: 'touchstart' | 'touchmove' | 'touchend', points: DispatchedTouch[]): Promise<void> {
  await target.evaluate(
    (element, { type: eventType, points: list }) => {
      const touches = list.map(
        (point) => new Touch({ identifier: point.id, target: element, clientX: point.x, clientY: point.y }),
      );
      element.dispatchEvent(new TouchEvent(eventType, { bubbles: true, cancelable: true, changedTouches: touches, touches }));
    },
    { type, points },
  );
}

test.describe('<Slider.Root /> (props on slider-config)', () => {
  test.describe('prop: step', () => {
    // SLD-PTR-04, SLD-STA-14, SLD-STA-10.
    test('should round value to step precision', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '0.2', min: '0', max: '1', step: '0.1' });
      const slider = fixture.getByRole('slider');
      await slider.focus();
      await expect(slider).toHaveAttribute('aria-valuenow', '0.2');
      const box = await origin(fixture);
      const cdp = await touchSession(page);

      await touch(cdp, 'touchStart', [{ x: box.x + 20, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 80, y: box.y + 5 }]);
      await expect(slider).toHaveAttribute('aria-valuenow', '0.8');
      await touch(cdp, 'touchMove', [{ x: box.x + 40, y: box.y + 5 }]);
      await expect(slider).toHaveAttribute('aria-valuenow', '0.4');
      await touch(cdp, 'touchEnd', []);
    });
  });

  test.describe('prop: disabled', () => {
    // SLD-API-16, SLD-EDGE-05, SLD-PTR-12.
    test('does not drag a disabled thumb with touch events', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-thumb-disabled');
      const [enabledInput, disabledInput] = await fixture.getByRole('slider').all();
      await expect(disabledInput!).toBeDisabled();
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 80, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 40, y: box.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(enabledInput!).toHaveAttribute('aria-valuenow', '20');
      await expect(disabledInput!).toHaveAttribute('aria-valuenow', '80');
    });

    // SLD-API-16, SLD-EDGE-05, SLD-PTR-03.
    test('does not change a single disabled thumb when pressing the track', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '20', thumbDisabled: '0' });
      const input = fixture.getByRole('slider');
      await expect(input).toBeDisabled();
      const box = await origin(fixture);
      await page.mouse.move(box.x + 80, box.y + 5);
      await page.mouse.down();
      await page.mouse.up();
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await expect(input).toHaveAttribute('aria-valuenow', '20');
    });
  });

  test.describe('events', () => {
    // SLD-PTR-12. A real second finger sends pointer events, which the gesture's pointer listeners would take; Base UI's
    // touch-only events are dispatched instead. The controlled value stays 0.
    test('port: should only listen to changes from the same touchpoint (dispatched touch events)', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '0', controlled: '1', follow: '0' });
      const control = fixture.getByTestId('control');
      const body = page.locator('body');
      const box = await origin(fixture);
      const at = (id: number, x: number) => ({ id, x: box.x + x, y: box.y + 5 });

      await dispatchTouch(control, 'touchstart', [at(1, 0)]);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');
      await dispatchTouch(body, 'touchstart', [at(2, 40)]);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '');

      await dispatchTouch(body, 'touchmove', [at(1, 1)]);
      await expect(fixture).toHaveAttribute('data-changes', '1:drag:0');
      await expect(fixture).toHaveAttribute('data-commits', '');
      await dispatchTouch(body, 'touchmove', [at(2, 41)]);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-changes', '1:drag:0');
      await expect(fixture).toHaveAttribute('data-commits', '');

      await dispatchTouch(body, 'touchend', [at(1, 2)]);
      await expect(fixture).toHaveAttribute('data-commits', /^\S+:drag$/);
      await expect(fixture).toHaveAttribute('data-changes', '1:drag:0');
    });

    // SLD-PTR-11.
    test('does not call onValueCommitted on outside taps after a touch interaction', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '0' });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 5, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 10, y: box.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await expect(fixture).toHaveAttribute('data-commits', /^\S+$/);
      const commits = await fixture.getAttribute('data-commits');

      const elsewhere = (await fixture.getByTestId('elsewhere').boundingBox())!;
      await touch(cdp, 'touchStart', [{ x: elsewhere.x + 80, y: elsewhere.y + 20 }]);
      await touch(cdp, 'touchEnd', []);
      await settle(fixture);
      await expect(fixture).toHaveAttribute('data-commits', commits!);
    });

    // SLD-PTR-11: every document `touchend` listener the gesture added is removed by its end.
    test('removes the document touchend listener after a touch interaction', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '0' });
      await page.evaluate(() => {
        const added: EventListenerOrEventListenerObject[] = [];
        const removed: EventListenerOrEventListenerObject[] = [];
        (window as unknown as { touchEndListeners: { added: typeof added; removed: typeof removed } }).touchEndListeners = { added, removed };
        const add = document.addEventListener.bind(document);
        const remove = document.removeEventListener.bind(document);
        document.addEventListener = ((type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) => {
          if (type === 'touchend' && listener != null) {
            added.push(listener);
          }
          add(type, listener, options);
        }) as typeof document.addEventListener;
        document.removeEventListener = ((type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) => {
          if (type === 'touchend' && listener != null) {
            removed.push(listener);
          }
          remove(type, listener, options);
        }) as typeof document.removeEventListener;
      });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 5, y: box.y + 5 }]);
      await touch(cdp, 'touchMove', [{ x: box.x + 10, y: box.y + 5 }]);
      await touch(cdp, 'touchEnd', []);
      await expect(fixture).toHaveAttribute('data-commits', /^\S+$/);

      const { added, removedAll } = await page.evaluate(() => {
        const { added: list, removed } = (window as unknown as {
          touchEndListeners: { added: EventListenerOrEventListenerObject[]; removed: EventListenerOrEventListenerObject[] };
        }).touchEndListeners;
        return { added: list.length, removedAll: list.every((listener) => removed.includes(listener)) };
      });
      expect(added).toBeGreaterThan(0);
      expect(removedAll).toBe(true);
    });

    // SLD-PTR-12.
    test('should focus the slider when touching', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '30' });
      const box = await origin(fixture);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [{ x: box.x + 0.2, y: box.y + 5 }]);
      await expect(fixture.getByRole('slider')).toBeFocused();
      await touch(cdp, 'touchEnd', []);
    });

    // SLD-PTR-20, SLD-PTR-21, SLD-EVT-04: a document listener and one on an ancestor (Base UI's wrapping `div`, here the
    // fixture's container) see the element the finger pressed. The controlled value stays 0, so nothing changes.
    test('should not override the event.target on touch events', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '0', controlled: '1', follow: '0' });
      const box = await origin(fixture);
      const point = { x: box.x + 0.2, y: box.y + 5 };
      await fixture.evaluate((container, at) => {
        const log: Array<{ where: string; same: boolean }> = [];
        (window as unknown as { targets: typeof log }).targets = log;
        const pressed = document.elementFromPoint(at.x, at.y);
        document.addEventListener('touchstart', (event) => log.push({ where: 'document', same: event.target === pressed }));
        container.addEventListener('touchstart', (event) => log.push({ where: 'container', same: event.target === pressed }));
      }, point);
      const cdp = await touchSession(page);
      await touch(cdp, 'touchStart', [point]);
      await touch(cdp, 'touchEnd', []);
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-changes', '');
      expect(await page.evaluate(() => (window as unknown as { targets: unknown[] }).targets)).toEqual([
        { where: 'container', same: true },
        { where: 'document', same: true },
      ]);
    });

    // SLD-PTR-20, SLD-PTR-21. A real mouse press sends pointer events first, so Base UI's lone `mousedown` is dispatched.
    test('port: should not override the event.target on a dispatched mousedown', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '0', controlled: '1', follow: '0' });
      const control = fixture.getByTestId('control');
      await fixture.evaluate((container) => {
        const log: Array<{ where: string; id: string | null }> = [];
        (window as unknown as { targets: typeof log }).targets = log;
        document.addEventListener('mousedown', (event) => log.push({ where: 'document', id: (event.target as HTMLElement).dataset.testid ?? null }));
        container.addEventListener('mousedown', (event) => log.push({ where: 'container', id: (event.target as HTMLElement).dataset.testid ?? null }));
      });
      await control.evaluate((element) => element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true })));
      await settle(fixture);

      await expect(fixture).toHaveAttribute('data-changes', '');
      expect(await page.evaluate(() => (window as unknown as { targets: unknown[] }).targets)).toEqual([
        { where: 'container', id: 'control' },
        { where: 'document', id: 'control' },
      ]);
    });
  });

  // SLD-PTR-14, SLD-STA-08, SLD-DOM-02: a touch-only gesture is dragging from its third move. A real touch's pointerdown
  // starts a pointer gesture, dragging from the press (SLD-PTR-08), so Base UI's touch events are dispatched.
  test.describe('dragging state', () => {
    test('port: should not apply data-dragging for click modality (dispatched touch events)', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '90' });
      const control = fixture.getByTestId('control');
      const body = page.locator('body');
      const box = await origin(fixture);
      const at = (x: number) => ({ id: 1, x: box.x + x, y: box.y + 5 });
      await dispatchTouch(control, 'touchstart', [at(20)]);
      await dispatchTouch(body, 'touchmove', [at(21)]);
      await expect.poll(() => lastChange(fixture)).toBe('21:drag:0');
      await settle(fixture);
      await expect(control).not.toHaveAttribute('data-dragging');
      await dispatchTouch(body, 'touchend', [at(0)]);
    });

    test('port: should apply data-dragging for dragging modality (dispatched touch events)', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '90' });
      const control = fixture.getByTestId('control');
      const body = page.locator('body');
      const box = await origin(fixture);
      const at = (x: number) => ({ id: 1, x: box.x + x, y: box.y + 5 });
      await dispatchTouch(control, 'touchstart', [at(20)]);
      await dispatchTouch(body, 'touchmove', [at(200)]);
      await dispatchTouch(body, 'touchmove', [at(200)]);
      await settle(fixture);
      await expect(control).not.toHaveAttribute('data-dragging');

      await dispatchTouch(body, 'touchmove', [at(200)]);
      await expect(control).toHaveAttribute('data-dragging', '');
      await dispatchTouch(body, 'touchend', [at(0)]);
      await expect(control).not.toHaveAttribute('data-dragging');
    });
  });
});

test.describe('<Slider.Control /> (slider-config)', () => {
  // SLD-EDGE-08: the substitute renders the attributes on a `div` and drops the element, so the module has no Control.
  test('degrades safely when a custom render function drops the control ref', async ({ page, mode }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    const fixture = await open(page, mode, 'slider-config', { value: '20', dropref: '1' });
    const box = await origin(fixture);
    await page.mouse.move(box.x + 80, box.y + 5);
    await page.mouse.down();
    await page.mouse.up();
    await settle(fixture);
    await expect(fixture).toHaveAttribute('data-changes', '');

    await pressControl(fixture.getByTestId('mounted'));
    await expect(fixture.getByRole('slider')).toHaveCount(0);
    await settle(fixture);
    expect(errors).toEqual([]);
  });

  // SLD-PTR-12. A real touch's target is an element, never a text node, so the touchstart is dispatched on the text node.
  test('port: handles touch interactions dispatched on a text node', async ({ page, mode }) => {
    const fixture = await open(page, mode, 'slider-config', { value: '20', text: '1' });
    const box = await origin(fixture);
    await fixture.getByTestId('track-text').evaluate(
      (span, at) => {
        const text = span.firstChild!;
        const point = new Touch({ identifier: 1, target: text, clientX: at.x, clientY: at.y });
        text.dispatchEvent(new TouchEvent('touchstart', { bubbles: true, cancelable: true, changedTouches: [point], touches: [point] }));
        const end = new Touch({ identifier: 1, target: document.body, clientX: at.x, clientY: at.y });
        document.body.dispatchEvent(new TouchEvent('touchend', { bubbles: true, cancelable: true, changedTouches: [end], touches: [] }));
      },
      { x: box.x + 60, y: box.y + 5 },
    );
    await expect(fixture).toHaveAttribute('data-changes', '60:track-press:0');
  });

  test.describe('cancelled gestures', () => {
    // SLD-PTR-11, SLD-PTR-08, SLD-STA-08, SLD-PTR-23. No real mouse or pen sends a pointercancel, so the title dispatches
    // the cancel of the real press on the document's body, as Base UI's `fireEvent.pointerCancel(document.body)`; the pen
    // is CDP's. A later press, move and release elsewhere change nothing.
    for (const pointer of ['mouse', 'pen'] as const) {
      test(`port: commits and ends a ${pointer} drag on a dispatched pointercancel`, async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-config', { value: '20' });
        const control = fixture.getByTestId('control');
        await page.evaluate(() => {
          const log: Array<{ id: number; type: string }> = [];
          (window as unknown as { pointerDowns: typeof log }).pointerDowns = log;
          document.addEventListener('pointerdown', (event) => log.push({ id: event.pointerId, type: event.pointerType }), true);
        });
        const box = await origin(fixture);
        const cdp = await page.context().newCDPSession(page);
        const press = async (x: number, y: number) => (pointer === 'mouse' ? (await page.mouse.move(x, y), await page.mouse.down()) : pen(cdp, 'mousePressed', x, y));
        const move = async (x: number, y: number) => (pointer === 'mouse' ? page.mouse.move(x, y) : pen(cdp, 'mouseMoved', x, y));
        const release = async (x: number, y: number) => (pointer === 'mouse' ? page.mouse.up() : pen(cdp, 'mouseReleased', x, y));

        await press(box.x + 40, box.y + 5);
        await move(box.x + 70, box.y + 5);
        await expect(control).toHaveAttribute('data-dragging', '');
        await expect.poll(() => lastChange(fixture)).toBe('70:drag:0');

        await page.evaluate(() => {
          const last = (window as unknown as { pointerDowns: Array<{ id: number; type: string }> }).pointerDowns.at(-1)!;
          document.body.dispatchEvent(new PointerEvent('pointercancel', { bubbles: true, pointerId: last.id, pointerType: last.type }));
        });
        await expect(fixture).toHaveAttribute('data-commits', '70:drag');
        await expect(control).not.toHaveAttribute('data-dragging');
        await release(box.x + 70, box.y + 5);
        const changes = await fixture.getAttribute('data-changes');

        const elsewhere = (await fixture.getByTestId('elsewhere').boundingBox())!;
        await press(elsewhere.x + 90, elsewhere.y + 5);
        await move(elsewhere.x + 91, elsewhere.y + 5);
        await release(elsewhere.x + 91, elsewhere.y + 5);
        await settle(fixture);
        await expect(fixture).toHaveAttribute('data-changes', changes!);
        await expect(fixture).toHaveAttribute('data-commits', '70:drag');
      });
    }
  });
});

test.describe('<Slider.Thumb /> (slider-config)', () => {
  test.describe('positioning styles', () => {
    test.describe('positions the thumb when the controlled value changes externally', () => {
      // SLD-DOM-09, SLD-STA-11, SLD-EVT-07: the `set-a` button is Base UI's external `setVal`.
      test('multiple thumbs', async ({ page, mode }) => {
        const fixture = await open(page, mode, 'slider-config', { values: '20,50', controlled: '1', setA: '33,72' });
        const [thumb1, thumb2] = [fixture.getByTestId('thumb-0'), fixture.getByTestId('thumb-1')];
        await expect(thumb1).toHaveCSS('left', '20px');
        await expect(thumb2).toHaveCSS('left', '50px');

        await pressControl(fixture.getByTestId('set-a'));

        await expect(thumb1).toHaveCSS('left', '33px');
        await expect(thumb2).toHaveCSS('left', '72px');
        await expect(fixture).toHaveAttribute('data-changes', '');
      });
    });

    // SLD-EDGE-04, SLD-STA-03, SLD-DOM-09: `set-a` and `set-b` are Base UI's `max` and `min` buttons.
    test('thumb should not go out of bounds when the controlled value goes out of bounds', async ({ page, mode }) => {
      const fixture = await open(page, mode, 'slider-config', { value: '50', controlled: '1', setA: '119.9', setB: '-7.31' });
      const thumb = fixture.getByTestId('thumb-0');
      await expect(thumb).toHaveCSS('left', '50px');

      await fixture.getByTestId('set-a').click();
      await expect(thumb).toHaveCSS('left', '100px');

      await fixture.getByTestId('set-b').click();
      await expect(thumb).toHaveCSS('left', '0px');
    });
  });
});

test.describe('<Slider.Indicator />', () => {
  const parityCases = ['horizontal-ltr', 'horizontal-rtl', 'vertical-ltr'].flatMap((axis) =>
    ['center', 'edge'].flatMap((alignment) => ['single', 'range'].map((valueShape) => ({ axis, alignment, valueShape }))),
  );

  // SLD-DOM-05, SLD-DOM-06, SLD-ANI-01, SLD-API-25, SLD-KEY-02, SLD-KEY-04, SLD-KEY-05, SLD-I18N-01. Base UI's inline
  // styles, and the geometry they compute to: the Indicator's start offset from the Track's start edge (the right edge in
  // right-to-left, the bottom edge when vertical) and its length, in px of the 100 px Track.
  for (const { axis, alignment, valueShape } of parityCases) {
    test(`preserves keyboard and indicator parity for ${axis} ${alignment} ${valueShape} sliders`, async ({ page, mode }) => {
      const vertical = axis === 'vertical-ltr';
      const direction = axis === 'horizontal-rtl' ? 'rtl' : 'ltr';
      const range = valueShape === 'range';
      const edge = alignment === 'edge';
      const fixture = await open(page, mode, 'slider-config', {
        ...(range ? { values: '30,70' } : { value: '30' }),
        orientation: vertical ? 'vertical' : 'horizontal',
        dir: direction,
        alignment,
      });
      const indicator = fixture.getByTestId('indicator');
      const startSide = vertical ? 'bottom' : 'inset-inline-start';
      const sizeSide = vertical ? 'height' : 'width';
      const start = edge ? '32%' : '30%';
      const size = edge ? '36%' : '40%';
      let expectedStartSide = range ? start : '0px';
      let expectedSizeSide = range ? size : start;
      if (edge) {
        expectedStartSide = range ? 'var(--start-position)' : '0px';
        expectedSizeSide = range ? 'var(--relative-size)' : 'var(--start-position)';
      }

      await expect.poll(() => inlineStyle(indicator, 'visibility')).toBe('');
      expect(await inlineStyle(indicator, startSide)).toBe(expectedStartSide);
      expect(await inlineStyle(indicator, sizeSide)).toBe(expectedSizeSide);
      expect(await inlineStyle(indicator, '--start-position')).toBe(edge ? start : '');
      expect(await inlineStyle(indicator, '--relative-size')).toBe(edge && range ? size : '');

      const geometry = await fixture.evaluate((container, isVertical) => {
        const track = container.querySelector('[data-testid="track"]')!.getBoundingClientRect();
        const bar = container.querySelector('[data-testid="indicator"]')!.getBoundingClientRect();
        const rtl = getComputedStyle(container.querySelector('[data-testid="track"]')!).direction === 'rtl';
        if (isVertical) {
          return { start: track.bottom - bar.bottom, size: bar.height };
        }
        return rtl ? { start: track.right - bar.right, size: bar.width } : { start: bar.left - track.left, size: bar.width };
      }, vertical);
      const startPx = range ? Number.parseFloat(start) : 0;
      const sizePx = range ? Number.parseFloat(size) : Number.parseFloat(start);
      expect(geometry.start).toBeCloseTo(startPx, 1);
      expect(geometry.size).toBeCloseTo(sizePx, 1);

      const input = fixture.getByRole('slider').first();
      let incrementKey = 'ArrowRight';
      if (vertical) {
        incrementKey = 'ArrowUp';
      } else if (direction === 'rtl') {
        incrementKey = 'ArrowLeft';
      }
      await fixture.locator('[data-sync-button]').focus();
      await page.keyboard.press('Tab');
      await expect(input).toBeFocused();
      for (const key of [incrementKey, 'PageUp', 'PageDown', 'End']) {
        await page.keyboard.press(key);
      }
      await expect(input).toHaveAttribute('aria-valuenow', range ? '70' : '100');
      await page.keyboard.press('Home');
      await expect(input).toHaveAttribute('aria-valuenow', '0');
    });
  }
});
