// One-family helpers of the OtpField specs (decision 22 (a) of the M6 plan: they stay beside the specs). Readiness is the
// fixture's data-attached, set once the root's `otp-field` module is attached, never a settle alone (track 0's rule).
import type { Locator } from '@playwright/test';
import { expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

export interface OtpField {
  fixture: Locator;
  root: Locator;
  slot(index: number): Locator;
  slots: Locator;
  hidden: Locator;
  outside: Locator;
}

/**
 * Opens an OtpField fixture with its query and waits for the module's attach.
 * @param page The page.
 * @param mode The render mode.
 * @param query The fixture's query, without `?`.
 * @param name The fixture, `otp-field-basic` by default.
 * @returns The fixture's parts.
 */
export async function gotoOtp(page: Page, mode: RenderMode, query = '', name = 'otp-field-basic'): Promise<OtpField> {
  await page.goto(`/${mode}/${name}${query ? `?${query}` : ''}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive(name)).first();
  await fixture.waitFor({ state: 'attached' });
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  return {
    fixture,
    root: fixture.getByTestId('root'),
    slot: (index) => fixture.getByTestId(`slot-${index}`),
    slots: fixture.locator('[data-testid^="slot-"]'),
    hidden: fixture.locator('input[aria-hidden="true"]'),
    outside: fixture.getByTestId('outside'),
  };
}

/** The slots' values, joined. */
export async function slotValues(field: OtpField): Promise<string> {
  return (await field.slots.evaluateAll((inputs) => inputs.map((input) => (input as HTMLInputElement).value || '_'))).join('');
}

/**
 * Dispatches a paste whose clipboard holds `text` on the focused slot, as Base UI's `fireEvent.paste` does (the
 * tests/browser/CONTEXT.md note for dispatched `ClipboardEvent`s); `throws` makes `getData` throw.
 */
export async function paste(slot: Locator, text: string, throws = false): Promise<void> {
  await slot.evaluate(
    (input, [value, fail]) => {
      const data = new DataTransfer();
      data.setData('text/plain', value as string);
      const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data });
      if (fail) {
        Object.defineProperty(event, 'clipboardData', {
          value: {
            getData() {
              throw new Error('denied');
            },
          },
        });
      }
      input.dispatchEvent(event);
    },
    [text, throws] as const,
  );
}
