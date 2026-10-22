import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, Fixture, type Page, type RenderMode } from './helpers';

// docs/components/input.md: Input is Field.Control under another name (INP-STR-04), so its rows run on the field suite at
// L2 over both parts. The browser clauses the document names: INP-API-07 (the already-focused autofocus control, the
// prerendered `field-autofocus` page with an Input) and INP-API-08 (the browser's own constraints on an Input in a
// field), plus the page ADR 13 §1 asks for, `input-showcase`, with an Input outside any field.

async function gotoInput(page: Page, mode: RenderMode, name: string, query = ''): Promise<Locator> {
  await page.goto(`${fixtureUrl(mode, name)}${query}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(name)).first().waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.container(name));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

test.describe('Input', () => {
  test('should sync focused state when autoFocus is used with SSR', async ({ page, mode }) => {
    const html = await (await page.request.get(`${fixtureUrl(mode, 'field-autofocus')}?control=input`)).text();
    expect(html).toMatch(/<input[^>]*autofocus/);

    const fixture = await gotoInput(page, mode, 'field-autofocus', '?control=input');
    const control = fixture.locator('[data-testid="control"]');

    await expect(control).toBeFocused();
    await expect(fixture.locator('[data-testid="root"]')).toHaveAttribute('data-focused', '');
    await expect(control).toHaveAttribute('data-focused', '');
  });

  test('should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty', async ({ page, mode }) => {
    const fixture = await gotoInput(page, mode, 'input-showcase');
    const control = fixture.locator('[data-case="field"] [data-testid="control"]');

    await control.focus();
    await control.blur();

    await expect(control).toHaveAttribute('data-invalid', '');
    await expect(control).toHaveAttribute('aria-invalid', 'true');
    await expect(fixture.locator('[data-case="field"] [data-testid="root"]')).toHaveAttribute('data-invalid', '');
  });

  // INP-STR-03, INP-FRM-07 (the port's title): outside a field the Input reports its changes and renders no field state.
  test('reports value changes outside a field', async ({ page, mode }) => {
    const fixture = await gotoInput(page, mode, 'input-showcase');
    const scope = fixture.locator('[data-case="standalone"]');
    const input = scope.locator('[data-testid="input"]');

    await input.pressSequentially('ab');

    await expect(scope).toHaveAttribute('data-values', 'a,ab');
    await expect(input).toHaveValue('ab');
    await expect(input).not.toHaveAttribute('data-dirty');
    await expect(input).not.toHaveAttribute('data-filled');
  });
});
