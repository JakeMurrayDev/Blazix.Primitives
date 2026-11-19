import { test, expect } from './helpers';
import { openToast } from './toast.helpers';

// Toast.Positioner's and Toast.Arrow's [L4] rows (docs/components/toast.md §5.7, §10; issue 280, M6 track 9b): the
// placement the `positioning` module computes, on the `toast-anchored` fixture. Titles are Base UI's from
// ToastPositioner.test.tsx and ToastArrow.test.tsx.

test.describe('Toast.Positioner', () => {
  test('positions an anchored toast against its anchor element', async ({ page, mode }) => {
    await openToast(page, mode, {}, 'toast-anchored');
    const anchor = page.getByTestId('anchor');

    await anchor.click();

    const positioner = page.getByTestId('anchored');
    await expect(positioner).toHaveAttribute('data-side', 'bottom');
    const anchorBottom = (await anchor.boundingBox())!.y + 20;
    await expect.poll(async () => Math.round((await positioner.boundingBox())!.y)).toBe(Math.round(anchorBottom + 8));
    // TST-ARIA-09.
    await expect(positioner).toHaveAttribute('role', 'presentation');
  });

  test('falls back to the viewport when no anchor is provided', async ({ page, mode }) => {
    await openToast(page, mode, {}, 'toast-anchored');

    await page.getByTestId('add').click();

    // An unanchored positioner still renders, with the defaults applied.
    const positioner = page.getByTestId('unanchored');
    await expect(positioner).toHaveAttribute('data-side', 'top');
    await expect(positioner).toHaveAttribute('data-align', 'center');
    await expect(positioner).toHaveText('title');
  });

  test('keeps the DOM index while a toast animates out', async ({ page, mode }) => {
    await openToast(page, mode, { animations: '1' }, 'toast-anchored');
    await page.getByTestId('add-oldest').click();
    await page.getByTestId('add-newest').click();
    const [newest, oldest] = [page.getByTestId('newest'), page.getByTestId('oldest')];
    await expect.poll(() => newest.evaluate((element) => element.style.getPropertyValue('--toast-index'))).toBe('0');
    await expect.poll(() => oldest.evaluate((element) => element.style.getPropertyValue('--toast-index'))).toBe('1');

    await page.getByTestId('close-newest').click();
    await expect(page.locator('[data-toast-id="newest"]')).toHaveAttribute('data-ending-style', '');

    // The closing toast is excluded from the visible stack, but it must keep a real index while it animates out rather
    // than collapsing to `-1`.
    await expect.poll(() => oldest.evaluate((element) => element.style.getPropertyValue('--toast-index'))).toBe('0');
    expect(await newest.evaluate((element) => element.style.getPropertyValue('--toast-index'))).toBe('0');
  });
});

test.describe('Toast.Arrow', () => {
  test('mirrors the resolved side of its positioner', async ({ page, mode }) => {
    await openToast(page, mode, {}, 'toast-anchored');

    await page.getByTestId('anchor').click();

    const arrow = page.getByTestId('arrow');
    await expect(arrow).toHaveAttribute('data-side', 'bottom');
    await expect(arrow).toHaveAttribute('aria-hidden', 'true');
  });
});
