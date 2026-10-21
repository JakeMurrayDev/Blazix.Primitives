import { test, expect, gotoFixture } from './helpers';

// Separator has no [L4] row (docs/components/separator.md §13): every row is C#-owned and tested at L2. This spec is
// the port's own check that the fixture page renders in both render modes and that what C# wrote survives the
// browser: the role, `aria-orientation` and `data-orientation` of each case, no `tabindex`, and the consumer's
// attributes over the built-in ones (SEP-ARIA-01, SEP-ARIA-02, SEP-DOM-01, SEP-DOM-02, SEP-FOC-01). It also gives the
// family its spec file, which the scoped L4 classification targets (ADR 09 §2).
test.describe('Separator', () => {
  test('renders role="separator" with aria-orientation and data-orientation for each case', async ({ page, mode }) => {
    const fixture = await gotoFixture(page, mode, 'separator-showcase');

    const horizontal = fixture.locator('[data-case="horizontal"]');
    await expect(horizontal).toHaveAttribute('role', 'separator');
    await expect(horizontal).toHaveAttribute('aria-orientation', 'horizontal');
    await expect(horizontal).toHaveAttribute('data-orientation', 'horizontal');
    await expect(horizontal).not.toHaveAttribute('tabindex');
    await expect(horizontal).not.toHaveAttribute('orientation');

    const vertical = fixture.locator('[data-case="vertical"]');
    await expect(vertical).toHaveAttribute('role', 'separator');
    await expect(vertical).toHaveAttribute('aria-orientation', 'vertical');
    await expect(vertical).toHaveAttribute('data-orientation', 'vertical');
    await expect(vertical).not.toHaveAttribute('tabindex');
    await expect(vertical).not.toHaveAttribute('orientation');

    // The consumer's attributes come last: the role and aria-orientation are replaced, data-orientation stays the state's.
    const override = fixture.locator('[data-case="override"]');
    await expect(override).toHaveAttribute('role', 'presentation');
    await expect(override).toHaveAttribute('aria-orientation', 'vertical');
    await expect(override).toHaveAttribute('data-orientation', 'horizontal');
  });
});
