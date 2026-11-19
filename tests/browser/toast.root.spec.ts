import { test, expect, holdCircuit } from './helpers';
import { addToast, centreOf, mouseSwipe, openToast, recorded } from './toast.helpers';

// Toast.Root's [L4] rows (docs/components/toast.md §5.2, §6.1, §6.2, §9; issue 280, M6 track 9b): the swipe with the real
// mouse (Toast's gesture does not depend on velocity, the M6 plan's decision 22), its styles, the measurement, the
// transitions and the removal, Escape and the focus hand-off, on the `toast-basic` fixture. Titles are Base UI's from
// ToastRoot.test.tsx where the row cites one, "port:" otherwise. Base UI's `BASE_UI_ANIMATIONS_DISABLED = false` is the
// host's `animations=1`; its `user.pointer` touch swipe is the real mouse here (the changed path in the rows' notes).

test.describe('Toast.Root', () => {
  // Decision 6 (a) of ADR 04 Amendments 2026-11-18 (chosen by the user): Toast's first red L4 title. The dismissal is the
  // module's optimistic move; C#'s messages are held from the release report on while the pointer acts again (it hovers the
  // other toast), then let go. The dismissed Root never shows its resting transform before data-ending-style, and the
  // updates that land late leave its movement variables at the released drag.
  test('port: a stale update after a swipe dismissal never snaps the toast back', async ({ page, mode }) => {
    test.skip(mode === 'wasm', 'WebAssembly has no circuit to hold.');
    const hold = await holdCircuit(page, {
      armOn: (text) => text.includes('OnReport') && text.includes('release'),
      from: () => true,
      maxMs: 10_000,
    });
    await openToast(page, mode, { directions: 'right', animated: '1', animations: '1' });
    await addToast(page);
    await addToast(page);
    const [dismissed, other] = [page.locator('[data-toast-id="t2"]'), page.locator('[data-toast-id="t1"]')];
    await dismissed.evaluate((element) => {
      const records: Array<{ ending: boolean; transform: string; movement: string }> = [];
      (window as unknown as { toastRecords: typeof records }).toastRecords = records;
      new MutationObserver(() =>
        queueMicrotask(() =>
          records.push({
            ending: element.hasAttribute('data-ending-style'),
            transform: element.style.transform,
            movement: element.style.getPropertyValue('--toast-swipe-movement-x'),
          }),
        ),
      ).observe(element, { attributes: true, attributeFilter: ['style', 'data-ending-style', 'data-swipe-direction'] });
    });

    const start = await centreOf(dismissed);
    hold.arm();
    await mouseSwipe(page, start, { x: 80, y: 0 });
    await expect(dismissed).toHaveAttribute('data-swipe-direction', 'right');
    await page.mouse.move(...Object.values(await centreOf(other)) as [number, number], { steps: 3 });
    await hold.release();

    await expect(dismissed).toHaveAttribute('data-ending-style', '');
    const records = await page.evaluate(() => (window as unknown as { toastRecords: Array<{ ending: boolean; transform: string; movement: string }> }).toastRecords);
    expect(records.length).toBeGreaterThan(0);
    expect(records.filter((record) => !record.ending && record.transform === '')).toEqual([]);
    expect(await dismissed.evaluate((element) => element.style.getPropertyValue('--toast-swipe-movement-x'))).toBe('79px');
  });

  for (const [direction, deltaX, deltaY] of [
    ['left', -60, 0],
    ['right', 60, 0],
    ['up', 0, -60],
    ['down', 0, 60],
  ] as const) {
    test(`dismisses with a real ${direction} pointer swipe`, async ({ page, mode }) => {
      const fixture = await openToast(page, mode, { directions: direction });
      const root = await addToast(page);

      await mouseSwipe(page, await centreOf(root), { x: deltaX, y: deltaY });

      await expect(page.locator('[data-toast-id]')).toHaveCount(0);
      expect(await recorded(fixture, 'data-removed')).toEqual(['t1']);
    });
  }

  test('applies [data-swiping] attribute when swiping', async ({ page, mode }) => {
    const fixture = await openToast(page, mode, { directions: 'up' });
    const root = await addToast(page);
    const start = await centreOf(root);

    await page.mouse.move(start.x, start.y);
    await page.mouse.down();

    await expect(root).toHaveAttribute('data-swiping', '');
    // TST-PTR-10: the press expands the stack.
    await expect(page.getByTestId('viewport')).toHaveAttribute('data-expanded', '');
    await expect.poll(async () => recorded(fixture, 'data-reports')).toContain('swipe-start');
    await page.mouse.up();
    await expect(root).not.toHaveAttribute('data-swiping');
  });

  test('locks to a single axis on the first move of a two-axis swipe', async ({ page, mode }) => {
    await openToast(page, mode, { directions: 'down,right' });
    const root = await addToast(page);
    const start = await centreOf(root);

    await page.mouse.move(start.x, start.y);
    await page.mouse.down();
    await page.mouse.move(start.x + 1, start.y + 1);
    await page.mouse.move(start.x + 20, start.y + 50, { steps: 3 });

    expect(await root.evaluate((element) => element.style.getPropertyValue('--toast-swipe-movement-x'))).toBe('0px');
    expect(await root.evaluate((element) => element.style.getPropertyValue('--toast-swipe-movement-y'))).not.toBe('0px');
    expect(await root.evaluate((element) => element.style.transition)).toBe('none');
    await page.mouse.up();
  });

  test('resets drag state after releasing a far swipe even when the release lands on the document', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    const root = await addToast(page);
    const start = await centreOf(root);

    // The default directions are down and right; a far drag to the left is damped and released outside the toast.
    await mouseSwipe(page, start, { x: -start.x + 5, y: 0 });

    await expect(root).not.toHaveAttribute('data-swiping');
    await expect(root).not.toHaveAttribute('data-swipe-direction');
    expect(await root.evaluate((element) => [
      element.style.getPropertyValue('--toast-swipe-movement-x'),
      element.style.getPropertyValue('--toast-swipe-movement-y'),
      element.style.transition,
      element.style.transform,
    ])).toEqual(['0px', '0px', '', '']);

    await mouseSwipe(page, await centreOf(root), { x: 50, y: 0 });
    await expect(page.locator('[data-toast-id]')).toHaveCount(0);
    expect(await recorded(fixture, 'data-removed')).toEqual(['t1']);
  });

  test('ignores swipe gestures when toast is anchored', async ({ page, mode }) => {
    const fixture = await openToast(page, mode, { directions: 'up', anchored: '1' });
    const root = await addToast(page);

    await mouseSwipe(page, await centreOf(root), { x: 0, y: -45 });
    await page.locator('[data-sync-button]').dispatchEvent('click');
    await expect(fixture).toHaveAttribute('data-sync', '1');

    await expect(root).toBeAttached();
    await expect(root).not.toHaveAttribute('data-swiping');
    expect(await recorded(fixture, 'data-reports')).not.toContain('swipe-start');
    expect(await recorded(fixture, 'data-closed')).toEqual([]);
  });

  test('clears swipe state when re-adding a swipe-dismissed toast', async ({ page, mode }) => {
    await openToast(page, mode, { id: 'save', directions: 'right', animated: '1', animations: '1' });
    const root = await addToast(page);

    await mouseSwipe(page, await centreOf(root), { x: 60, y: 0 });
    await expect(root).toHaveAttribute('data-ending-style', '');
    await expect(root).toHaveAttribute('data-swipe-direction', 'right');

    await page.getByTestId('add').click();
    await expect(page.locator('[data-toast-id]')).toHaveCount(1);
    expect(await root.evaluate((element) => element.isConnected)).toBe(true);
    await expect(root).not.toHaveAttribute('data-starting-style');
    await expect(root).not.toHaveAttribute('data-swipe-direction');
    expect(await root.evaluate((element) => [
      element.style.getPropertyValue('--toast-swipe-movement-x'),
      element.style.getPropertyValue('--toast-swipe-movement-y'),
    ])).toEqual(['0px', '0px']);
  });

  test('clears swipe state when a retained root is reused for another toast', async ({ page, mode }) => {
    await openToast(page, mode, { keyed: 'index', directions: 'right', animated: '1', animations: '1' });
    await addToast(page);
    await addToast(page);
    await addToast(page);
    const swiped = page.getByTestId('root-1');
    const handle = await swiped.elementHandle();

    await mouseSwipe(page, await centreOf(swiped), { x: 60, y: 0 });
    await expect(swiped).toHaveAttribute('data-swipe-direction', 'right');

    // Index keys shift every retained root onto another toast, so this root now renders one that was never swiped.
    await page.getByTestId('add').click();
    expect(await handle!.evaluate((element) => element.getAttribute('data-testid'))).toBe('root-1');
    await expect(swiped).not.toHaveAttribute('data-swipe-direction');
    await expect(swiped).not.toHaveAttribute('data-ending-style');
    expect(await swiped.evaluate((element) => [
      element.style.getPropertyValue('--toast-swipe-movement-x'),
      element.style.getPropertyValue('--toast-swipe-movement-y'),
    ])).toEqual(['0px', '0px']);
  });

  test('moves focus to the next toast when closing in an index-keyed list', async ({ page, mode }) => {
    await openToast(page, mode, { keyed: 'index', animated: '1', animations: '1', toastTimeout: '0' });
    await addToast(page);
    await addToast(page);
    await addToast(page);

    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('root-0')).toBeFocused();

    await page.keyboard.press('Escape');

    // Focus hands off to the next active toast, not the one animating out.
    await expect(page.getByTestId('root-1')).toBeFocused();
  });

  test('closes when pressing escape', async ({ page, mode }) => {
    const fixture = await openToast(page, mode);
    await page.getByTestId('add').focus();
    await addToast(page);

    await page.keyboard.press('F6');
    await expect(page.getByTestId('viewport')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.locator('[data-toast-id="t1"]')).toBeFocused();
    await page.keyboard.press('Escape');

    await expect(page.locator('[data-toast-id]')).toHaveCount(0);
    expect(await recorded(fixture, 'data-closed')).toEqual(['t1']);
    // TST-FOC-08: the only toast closed, focus returns to the element focused before F6.
    await expect(page.getByTestId('add')).toBeFocused();
  });

  test('ignores Escape when focus is in portaled content', async ({ page, mode }) => {
    const fixture = await openToast(page, mode, { portaled: '1' });
    const root = await addToast(page);

    await page.getByTestId('portaled').focus();
    await page.keyboard.press('Escape');
    await page.locator('[data-sync-button]').dispatchEvent('click');
    await expect(fixture).toHaveAttribute('data-sync', '1');

    await expect(root).toBeAttached();
    expect(await recorded(fixture, 'data-closed')).toEqual([]);
  });

  test('recalculates height when content mutates', async ({ page, mode }) => {
    await openToast(page, mode, { width: '30', content: '1' });
    const root = await addToast(page);
    const initial = Number(await root.getAttribute('data-height'));
    await expect.poll(() => root.evaluate((element) => element.style.getPropertyValue('--toast-height'))).toBe(`${initial}px`);

    await page.getByTestId('update').click();

    await expect.poll(async () => Number(await root.getAttribute('data-height'))).toBeGreaterThan(initial);
    const grown = Number(await root.getAttribute('data-height'));
    await expect.poll(() => root.evaluate((element) => element.style.getPropertyValue('--toast-height'))).toBe(`${grown}px`);
  });

  test('clears the starting state and restores the height when re-adding an ending toast', async ({ page, mode }) => {
    await openToast(page, mode, { id: 'save', animated: '1', animations: '1', toastTimeout: '0' });
    const root = await addToast(page);
    await expect(root).not.toHaveAttribute('data-starting-style');
    const initialHeight = await root.evaluate((element) => element.style.getPropertyValue('--toast-height'));
    expect(initialHeight).not.toBe('');

    await page.getByTestId('close-newest').click();
    await expect(root).toHaveAttribute('data-ending-style', '');
    await expect.poll(() => root.evaluate((element) => element.style.getPropertyValue('--toast-height'))).toBe('');

    await page.getByTestId('add').click();
    expect(await root.evaluate((element) => element.isConnected)).toBe(true);
    await expect(root).not.toHaveAttribute('data-starting-style');
    await expect.poll(() => root.evaluate((element) => element.style.getPropertyValue('--toast-height'))).toBe(initialHeight);
  });

  test('registers an active toast after its root remounts', async ({ page, mode }) => {
    await openToast(page, mode, { id: 'save', width: '30', toastTimeout: '0' });
    const initial = await addToast(page);
    await expect(initial).not.toHaveAttribute('data-starting-style');
    const initialHeight = Number(await initial.getAttribute('data-height'));

    await page.getByTestId('hide').click();
    await expect(page.locator('[data-toast-id]')).toHaveCount(0);
    await page.getByTestId('show').click();
    const remounted = page.locator('[data-toast-id]');
    await expect(remounted).toHaveCount(1);

    await expect.poll(async () => Number(await remounted.getAttribute('data-height'))).toBeGreaterThan(initialHeight);

    await page.getByTestId('show').focus();
    await page.keyboard.press('F6');
    await page.keyboard.press('Tab');
    await expect(remounted).toBeFocused();
  });

  test('keeps stacking intact when re-adding an ending toast among other toasts', async ({ page, mode }) => {
    await openToast(page, mode, { content: '1', animated: '1', animations: '1' });
    await page.getByTestId('add-one').click();
    await page.getByTestId('add-two').click();
    await expect(page.locator('[data-toast-id]:not([data-height])')).toHaveCount(0);
    await expect(page.locator('[data-toast-id]')).toHaveCount(2);
    await page.getByTestId('close-one').click();

    const one = page.locator('[data-toast-id="one"]');
    await expect(one).toHaveAttribute('data-ending-style', '');

    await page.getByTestId('add-one').click();

    await expect(one).not.toHaveAttribute('data-starting-style');
    await expect.poll(() => one.evaluate((element) => element.style.getPropertyValue('--toast-height'))).not.toBe('');
    // The revived toast is frontmost and its content is not hidden as "behind".
    await expect.poll(() => one.evaluate((element) => element.style.getPropertyValue('--toast-index'))).toBe('0');
    await expect(page.getByTestId('content-one')).not.toHaveAttribute('data-behind');
    // The unrelated toast is unaffected and stacks behind the revived one.
    const two = page.locator('[data-toast-id="two"]');
    await expect(two).not.toHaveAttribute('data-ending-style');
    await expect.poll(() => two.evaluate((element) => element.style.getPropertyValue('--toast-index'))).toBe('1');
    await expect(page.getByTestId('content-two')).toHaveAttribute('data-behind', '');
  });

  // The standing rule on two handles on one element: the Root's `toast` and `transitions` handles attach independently.
  // With `transitions.js` held, the `toast` handle still measures and swipes, and the close's exit (and so the removal) waits
  // for the `transitions` handle; released, the removal follows.
  test('port: a stalled transitions module holds the removal, never the measurement or the swipe', async ({ page, mode }) => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    await page.route(/\/_content\/Blazix\.Primitives\/js\/transitions(\.[a-z0-9]+)?\.js$/, async (route) => {
      await held;
      await route.continue();
    });
    const fixture = await openToast(page, mode, { directions: 'right' });
    const root = await addToast(page);

    await mouseSwipe(page, await centreOf(root), { x: 60, y: 0 });
    await expect.poll(async () => recorded(fixture, 'data-closed')).toEqual(['t1']);
    await expect(root).toBeAttached();
    expect(await recorded(fixture, 'data-removed')).toEqual([]);

    release();
    await expect(page.locator('[data-toast-id]')).toHaveCount(0);
    expect(await recorded(fixture, 'data-removed')).toEqual(['t1']);
  });
});
