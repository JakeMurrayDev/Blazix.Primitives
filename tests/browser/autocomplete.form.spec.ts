import { type Locator } from '@playwright/test';
import { expect, test, type Page, type RenderMode } from './helpers';
import { center, gotoCombobox, moveTo, recorded } from './combobox.helpers';
import { settle } from './suites/popupFixture';

// Issues 248 and 257 (M6 track 11): the Autocomplete titles PR 245 left open by ID, and ComboboxClear.test.tsx's
// no-selection row, Base UI's titles verbatim, at L4 on both render modes, on the `autocomplete-form` fixture
// (tests/Iodine.Headless.Fixtures.Client/Autocomplete/L4/AutocompleteFormL4Fixture.razor), one Base UI render per `case`.
// The titles come from AutocompleteRoot.test.tsx (:1088, :1984, :2096, :2608, :3003), ComboboxRoot.test.tsx (its three
// Autocomplete titles), ComboboxTrigger.test.tsx (the `no selection` row of "validates the $name value when blurred") and
// ComboboxClear.test.tsx. AutocompleteItem.test.tsx:40 and store.test.tsx's hydration title use the `autocomplete-root` and
// `autocomplete-prerender` fixtures, in autocomplete.spec.ts.

function goto(page: Page, mode: RenderMode, testCase: string, options: Record<string, string | boolean> = {}): Promise<Locator> {
  return gotoCombobox(page, mode, 'autocomplete-form', { case: testCase, ...options });
}

/** Base UI's `createFormDataSpy('q')` (or a plain submit spy): each native submit of `form`, as its FormData `q`. */
async function watchSubmits(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { submitted: string[] }).submitted = [];
    document.querySelector('[data-testid=form]')!.addEventListener('submit', (event) => {
      (window as unknown as { submitted: string[] }).submitted.push(String(new FormData(event.target as HTMLFormElement).get('q')));
    });
  });
}

function submitted(page: Page): Promise<string[]> {
  return page.evaluate(() => (window as unknown as { submitted: string[] }).submitted);
}

/** Uncaught page errors (a `requestSubmit` call on a form without one would throw). */
function watchErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text());
  });
  return errors;
}

const combobox = (page: Page): Locator => page.getByRole('combobox');

test.describe('<Autocomplete.Root />', () => {
  test.describe('prop: autoHighlight', () => {
    // AUT-STA-11. Base UI's `fireEvent.pointerDown(outside)` + `blur` + `focus` is a real click on the outside button here.
    test('keeps the latest pointer highlight on outside blur when behavior is "always"', { tag: '@coverage' }, async ({ page, mode }) => {
      const fixture = await goto(page, mode, 'inline-always');
      const input = page.getByTestId('input');
      const banana = page.getByRole('option', { name: 'banana' });

      await input.focus();
      await moveTo(page, banana, { unanchored: true });

      await expect(banana).toHaveAttribute('data-highlighted', '');
      await expect(input).toHaveAttribute('aria-activedescendant', (await banana.getAttribute('id'))!);

      await page.getByTestId('outside').click();
      await expect(page.getByTestId('outside')).toBeFocused();
      await settle(fixture);

      await expect(banana).toHaveAttribute('data-highlighted', '');
      await expect(input).toHaveAttribute('aria-activedescendant', (await banana.getAttribute('id'))!);
      await expect(page.getByRole('option', { name: 'apple' })).not.toHaveAttribute('data-highlighted');
    });
  });

  test.describe('prop: submitOnItemClick', () => {
    // AUT-PTR-03
    test('uses the combobox input form when another unscoped control owns the validation ref', { tag: '@coverage' }, async ({ page, mode }) => {
      await goto(page, mode, 'validation-ref');
      await watchSubmits(page);

      await combobox(page).click();
      await combobox(page).pressSequentially('a');
      await page.getByRole('option', { name: 'alpha' }).click();

      await expect.poll(() => submitted(page)).toHaveLength(1);
    });

    // AUT-KEY-04. Base UI's `listbox.focus()` runs in the page; the List is not focusable, so focus stays on the Input.
    test('focusing the listbox should keep the input focused and maintain functionality', { tag: '@coverage' }, async ({ page, mode }) => {
      await goto(page, mode, 'field-submit');
      await watchSubmits(page);

      await combobox(page).click();
      await combobox(page).pressSequentially('al');
      await expect(page.getByRole('option')).toHaveCount(2);
      await center(page.getByRole('listbox'));
      await page.keyboard.press('ArrowDown');

      const alpha = page.getByRole('option', { name: 'alpha' });
      await expect(alpha).toHaveAttribute('data-highlighted', '');

      await page.getByRole('listbox').evaluate((listbox) => (listbox as HTMLElement).focus());
      await expect(combobox(page)).toBeFocused();

      await page.keyboard.press('Enter');

      await expect.poll(() => submitted(page)).toEqual(['alpha']);
    });
  });

  test.describe('Form', () => {
    // AUT-KEY-04, AUT-PTR-03. Base UI's `<Form onSubmit>` spy is the page's listener on the Form's element.
    test('pressing Enter in the List when it has focus submits the owning form', { tag: '@coverage' }, async ({ page, mode }) => {
      await goto(page, mode, 'field-submit', { form: 'base' });
      await watchSubmits(page);

      await combobox(page).click();
      await combobox(page).pressSequentially('al');
      await expect(page.getByRole('option')).toHaveCount(2);
      await center(page.getByRole('listbox'));
      await page.keyboard.press('ArrowDown');

      await expect(page.getByRole('option', { name: 'alpha' })).toHaveAttribute('data-highlighted', '');

      await page.getByRole('listbox').evaluate((list) => (list as HTMLElement).focus());

      await page.keyboard.press('Enter');

      await expect.poll(() => submitted(page)).toEqual(['alpha']);
    });
  });

  test.describe('Field', () => {
    // AUT-FRM-01. Base UI's `user.clear` is `fill('')`. Red on WebAssembly (2026-11-19, 3 of 3): the Field validates the typed
    // values out of order there ("o one on"), so "on"'s result lands last; open by ID, a library defect for track 10.
    // Quarantined (ADR 11 §4) on Server until 2026-12-08, issue 338: failed twice in nightlies on Server (44716429829 at
    // 955897bf; 45159975605 at 18d29112, `browser-server (3)`), the input still carrying `aria-invalid`; read as issue 309's
    // defect on a slower circuit, so it lifts with 309's fix (track 10, group 10g).
    test('prop: validationMode=onSubmit', {
      tag: ['@coverage', '@quarantine'],
      annotation: { type: 'open', description: 'https://github.com/JakeMurrayDev/Iodine.Headless/issues/338 (expires 2026-12-08)' },
    }, async ({ page, mode }) => {
      const openRow = 'AUT-FRM-01 (issue 309): on WebAssembly the Field validates typed values out of order, so a stale value\'s result lands last';
      if (mode === 'wasm') test.info().annotations.push({ type: 'open', description: openRow });
      test.fixme(mode === 'wasm', openRow);
      test.fixme(mode === 'server', 'Quarantined on Server until 2026-12-08 (issue 338)');
      await goto(page, mode, 'on-submit');
      const input = page.getByTestId('input');
      await expect(input).not.toHaveAttribute('aria-invalid');

      await page.getByText('submit').click();
      await expect(input).toHaveAttribute('aria-invalid', 'true');

      await input.click();
      await input.pressSequentially('two');
      await expect(input).not.toHaveAttribute('aria-invalid');

      await input.fill('');
      await input.pressSequentially('one');
      await expect(input).toHaveAttribute('aria-invalid', 'true');

      await input.fill('');
      await input.pressSequentially('three');
      await expect(input).not.toHaveAttribute('aria-invalid');
    });
  });
});

test.describe('<Combobox.Root />', () => {
  // AUT-PTR-03, AUT-PTR-02. Base UI's `fireEvent.click` on the option is a real click.
  test('does not request form submission when no form owns the autocomplete', { tag: '@coverage' }, async ({ page, mode }) => {
    const errors = watchErrors(page);
    const fixture = await goto(page, mode, 'no-form');

    await page.getByRole('option', { name: 'apple' }).click();
    await expect(combobox(page)).toHaveValue('apple');
    await settle(fixture);
    expect(errors).toEqual([]);
  });

  // AUT-PTR-03, AUT-PTR-02
  test('does not request form submission when requestSubmit is unavailable', { tag: '@coverage' }, async ({ page, mode }) => {
    const errors = watchErrors(page);
    const fixture = await goto(page, mode, 'no-request-submit');
    await watchSubmits(page);
    await page.getByRole('form', { name: 'search' }).evaluate((form) => {
      Object.defineProperty(form, 'requestSubmit', { configurable: true, value: undefined });
    });

    await page.getByRole('option', { name: 'apple' }).click();
    await expect(combobox(page)).toHaveValue('apple');
    await settle(fixture);
    expect(errors).toEqual([]);
    expect(await submitted(page)).toEqual([]);
  });

  // AUT-FRM-05, AUT-DIS-01. Base UI's `user.click(document.body)` is a real click on the page's empty corner.
  test('validates the autocomplete input when focus leaves its popup', { tag: '@coverage' }, async ({ page, mode }) => {
    const fixture = await goto(page, mode, 'popup-validate');
    await expect(page.getByTestId('input')).toBeVisible();
    await center(page.getByTestId('popup'));

    const viewport = page.viewportSize()!;
    await page.mouse.click(viewport.width - 5, viewport.height - 5);

    await expect.poll(() => recorded(fixture, 'data-validations')).toContain('query');
  });
});

test.describe('<Combobox.Trigger />', () => {
  // AUT-FRM-01, AUT-FRM-05: the `no selection` row (an Autocomplete.Root) of Base UI's it.each.
  test('validates the no selection value when blurred', { tag: '@coverage' }, async ({ page, mode }) => {
    const fixture = await goto(page, mode, 'trigger-validate');
    const trigger = page.getByTestId('trigger');
    await trigger.focus();
    await expect(trigger).toBeFocused();
    await trigger.blur();

    await expect(fixture).toHaveAttribute('data-validations', 'query');
  });
});

test.describe('<Combobox.Clear />', () => {
  // CMB-DOM-07, AUT-PTR-06 (issue 257 (3)): the Autocomplete row of ComboboxClear.test.tsx, at L4 as the row's tag says.
  test('renders and clears the typed value in no-selection mode', { tag: '@coverage' }, async ({ page, mode }) => {
    await goto(page, mode, 'clear');
    const input = combobox(page);
    await expect(input).toHaveValue('apple');

    await page.getByTestId('clear').click();

    await expect(input).toHaveValue('');
    await expect(input).toBeFocused();
  });
});
