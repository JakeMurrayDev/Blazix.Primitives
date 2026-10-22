import { type Locator } from '@playwright/test';
import { test, expect, fixtureUrl, gotoFixture, type Page, type RenderMode } from './helpers';

// The [L4] rows of docs/components/checkbox-group.md that render no Form, and the L4 clauses of its L2 rows, on
// CheckboxGroup and its member and parent boxes. Titles are Base UI's (checkbox-group/CheckboxGroup.test.tsx); "port:"
// titles assert the per-mode rows of checkbox-group §9 (CKG-STA-11, CKG-ARIA-03, CKG-ARIA-06). Field.Error is not on
// main yet (PR 1c): the two "keeps the error" titles read the field's valueMissing verdict from the members'
// aria-invalid, which with no validator is that verdict. The FormData titles run on native forms (the Form Base UI renders
// around them takes no part in what they assert). Rows that need Form stay open (CKG-FOC-02, CKG-FRM-03/06/17).

/** Opens the field fixture and waits until every listed box has its behaviors attached. */
async function gotoGroups(page: Page, mode: RenderMode): Promise<Locator> {
  const fixture = await gotoFixture(page, mode, 'checkbox-group-field');
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return fixture;
}

/** A barrier: clicks the fixture's sync button and waits for C# to count it (checkbox.spec.ts). */
async function settle(fixture: Locator): Promise<void> {
  const before = Number(await fixture.getAttribute('data-sync'));
  await fixture.locator('[data-sync-button]').evaluate((element) => (element as HTMLElement).click());
  await expect(fixture).toHaveAttribute('data-sync', String(before + 1));
}

function box(container: Locator, testId: string): Locator {
  return container.locator(`[data-testid="${testId}"]`);
}

/** The ids of the elements inside a case, in document order. */
async function idsIn(container: Locator): Promise<string[]> {
  return container.evaluate((node) => Array.from(node.querySelectorAll('[id]'), (element) => element.id));
}

/** The markup of one case of the prerendered fixture, from the static response. */
async function staticCase(page: Page, mode: RenderMode, name: string): Promise<string> {
  const html = await (await page.request.get(fixtureUrl(mode, 'checkbox-group-prerender'))).text();
  const start = html.indexOf(`data-case="${name}"`);
  const next = html.indexOf('data-case="', start + 1);
  return html.slice(start, next < 0 ? undefined : next);
}

function staticIds(markup: string): string[] {
  return Array.from(markup.matchAll(/ id="([^"]+)"/g), (match) => match[1]!);
}

/** Submits the case's form (prevented) and returns the values FormData lists under `name`. */
async function submittedValues(container: Locator, name: string): Promise<string[]> {
  return container.evaluate(async (node, field) => {
    const form = node.querySelector('form')!;
    const submitted = new Promise<string[]>((resolve) => {
      form.addEventListener('submit', (event) => {
        event.preventDefault();
        resolve(new FormData(form).getAll(field).map(String));
      }, { once: true });
    });
    node.querySelector<HTMLElement>('[data-submit]')!.click();
    return submitted;
  }, name);
}

test.describe('<CheckboxGroup />', () => {
  test.describe('Form', () => {
    test('includes the checkbox group value in form submission', async ({ page, mode }) => {
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="submit"]');
      expect(await submittedValues(container, 'apple')).toEqual(['fuji-apple', 'gala-apple']);
    });

    test('excludes parent checkboxes from form submission', async ({ page, mode }) => {
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="submit-parent"]');
      await expect(box(container, 'parent')).toHaveAttribute('aria-checked', 'mixed');

      await box(container, 'button-3').click();
      await expect(box(container, 'parent')).toHaveAttribute('aria-checked', 'true');

      expect(await submittedValues(container, 'apple')).toEqual(['fuji-apple', 'gala-apple', 'granny-smith-apple']);
    });
  });

  test.describe('Field', () => {
    test('validationMode=onChange keeps the error until every required checkbox is ticked', async ({ page, mode }) => {
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="required-change"]');
      await expect(box(container, 'cb-http')).not.toHaveAttribute('aria-invalid');

      // Ticking the second (last-mounted) checkbox must not clear the requirement on the first.
      await box(container, 'cb-https').click();
      await expect(box(container, 'cb-http')).toHaveAttribute('aria-invalid', 'true');
      await expect(box(container, 'cb-https')).toHaveAttribute('aria-invalid', 'true');

      await box(container, 'cb-http').click();
      await expect(box(container, 'cb-http')).not.toHaveAttribute('aria-invalid');
      await expect(box(container, 'cb-https')).not.toHaveAttribute('aria-invalid');
    });

    test('validationMode=onBlur keeps the error until every required checkbox is ticked', async ({ page, mode }) => {
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="required-blur"]');

      await box(container, 'cb-https').click();
      await settle(fixture);
      await expect(box(container, 'cb-https')).not.toHaveAttribute('aria-invalid');

      await box(container, 'cb-https').evaluate((element) => (element as HTMLElement).blur());
      await expect(box(container, 'cb-https')).toHaveAttribute('aria-invalid', 'true');

      // A toggle in onBlur mode clears the field's error (FFI-FRM-05's revalidate pass) once nothing is missing.
      await box(container, 'cb-http').click();
      await expect(box(container, 'cb-http')).not.toHaveAttribute('aria-invalid');
      await expect(box(container, 'cb-https')).not.toHaveAttribute('aria-invalid');
    });

    test('clears custom validity from disabled registered inputs when the group becomes valid', async ({ page, mode }) => {
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="custom-validity"]');
      const httpInput = container.locator('input[type="checkbox"][value="http"]');

      await box(container, 'cb-http').click();
      await expect.poll(() => httpInput.evaluate((input) => (input as HTMLInputElement).validity.customError)).toBe(true);

      await container.locator('[data-disable]').click();
      await expect(box(container, 'cb-http')).toHaveAttribute('data-disabled', '');
      await box(container, 'cb-https').click();

      await expect.poll(() => httpInput.evaluate((input) => (input as HTMLInputElement).validity.customError)).toBe(false);
    });

    test('port: [data-filled] reaches the group and its members after the interactive render', async ({ page, mode }) => {
      // CKG-STA-11 (checkbox-group §9: on Server the write lands one round trip after the render).
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="filled"]');
      await expect(container.locator('[data-group]')).toHaveAttribute('data-filled', '');
      await expect(box(container, 'apple')).toHaveAttribute('data-filled', '');
      await expect(box(container, 'apple')).toHaveAttribute('aria-checked', 'false');
    });

    test('port: a parent box controls the members that registered, and drops one that unmounts', async ({ page, mode }) => {
      // CKG-ARIA-06 (checkbox-group §9: on Server the registration lands one round trip after the render).
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="controls"]');
      const fuji = await box(container, 'fuji').getAttribute('id');
      const gala = await box(container, 'gala').getAttribute('id');
      await expect(box(container, 'parent')).toHaveAttribute('aria-controls', `${fuji} ${gala}`);

      await container.locator('[data-unmount]').click();

      await expect(box(container, 'parent')).toHaveAttribute('aria-controls', `${fuji}`);
    });
  });

  test.describe('Field.Label', () => {
    test('gives each checkbox in a shared Field.Root its own accessible name', async ({ page, mode }) => {
      const fixture = await gotoGroups(page, mode);
      const container = fixture.locator('[data-case="names"]');
      for (const [testId, name] of [['parent', 'All'], ['fuji', 'Fuji'], ['gala', 'Gala']] as const) {
        await expect(box(container, testId)).toHaveAttribute('aria-labelledby', /.+/);
        const labelId = await box(container, testId).getAttribute('aria-labelledby');
        await expect(page.locator(`[id="${labelId}"]`)).toHaveText(name);
        await expect(box(container, testId)).toHaveAccessibleName(name);
      }
    });

    for (const nativeButton of [false, true]) {
      test(`keeps checkbox ids unique in a shared Field.Root during SSR (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const ids = staticIds(await staticCase(page, mode, nativeButton ? 'shared-native' : 'shared'));
        expect(ids).toHaveLength(nativeButton ? 4 : 7);
        expect(new Set(ids).size).toBe(ids.length);

        // CKG-DOM-06 on the client.
        const fixture = await gotoFixture(page, mode, 'checkbox-group-prerender');
        const container = fixture.locator(`[data-case="${nativeButton ? 'shared-native' : 'shared'}"]`);
        await expect(container.locator('[data-label]')).not.toHaveAttribute('for');
        const clientIds = await idsIn(container);
        expect(clientIds).toHaveLength(nativeButton ? 4 : 7);
        expect(new Set(clientIds).size).toBe(clientIds.length);
      });
    }

    test('labels the group rather than pointing Field.Label at one checkbox inside it', async ({ page, mode }) => {
      // CKG-ARIA-03: the server markup still carries the label's `for` (checkbox-group §9).
      const markup = await staticCase(page, mode, 'shared');
      expect(markup.match(/<label[^>]*data-label[^>]*>/)?.[0]).toMatch(/ for="[^"]+"/);

      const fixture = await gotoFixture(page, mode, 'checkbox-group-prerender');
      const container = fixture.locator('[data-case="shared"]');
      const label = container.locator('[data-label]');
      await expect(label).not.toHaveAttribute('for');
      await expect(container.locator('[role="group"]')).toHaveAttribute('aria-labelledby', (await label.getAttribute('id'))!);
    });

    for (const nativeButton of [false, true]) {
      for (const parent of [false, true]) {
        test(`associates Field.Label with a grouped Checkbox during SSR (nativeButton=${nativeButton}, parent=${parent})`, async ({ page, mode }) => {
          const markup = await staticCase(page, mode, `associate-${nativeButton ? 'native' : 'input'}-${parent ? 'parent' : 'member'}`);
          const control = nativeButton
            ? markup.match(/<button[^>]*data-checkbox[^>]*>/)?.[0]
            : markup.match(/<input[^>]*type="checkbox"[^>]*>/)?.[0];
          const controlId = control?.match(/ id="([^"]+)"/)?.[1];
          expect(controlId).toBeTruthy();
          expect(markup.match(/<label[^>]*data-label[^>]*>/)?.[0].match(/ for="([^"]+)"/)?.[1]).toBe(controlId);
        });
      }
    }

    for (const nativeButton of [false, true]) {
      test(`points parent aria-controls at the children once they register (nativeButton=${nativeButton})`, async ({ page, mode }) => {
        const name = nativeButton ? 'items-native' : 'items';
        const markup = await staticCase(page, mode, name);

        // Server markup claims nothing: the parent can't control a child that hasn't mounted.
        expect(markup.match(/<[a-z]+[^>]*data-testid="parent"[^>]*>/)?.[0]).not.toMatch(/aria-controls=/);
        const labels = Array.from(markup.matchAll(/<label[^>]*data-label[^>]*>/g), (match) => match[0].match(/ for="([^"]+)"/)?.[1]);
        for (const [index, testId] of ['parent', 'fuji'].entries()) {
          const root = markup.match(new RegExp(`<[a-z]+[^>]*data-testid="${testId}"[^>]*>[\\s\\S]*?<input[^>]*type="checkbox"[^>]*>`))?.[0] ?? '';
          const labelable = nativeButton ? root.match(/^<[^>]*>/)?.[0] : root.match(/<input[^>]*type="checkbox"[^>]*>$/)?.[0];
          const labelableId = labelable?.match(/ id="([^"]+)"/)?.[1];
          expect(labelableId).toBeTruthy();
          expect(labels[index]).toBe(labelableId);
        }

        const fixture = await gotoFixture(page, mode, 'checkbox-group-prerender');
        const container = fixture.locator(`[data-case="${name}"]`);
        const fuji = await box(container, 'fuji').getAttribute('id');
        await expect(box(container, 'parent')).toHaveAttribute('aria-controls', fuji!);
      });
    }
  });
});
