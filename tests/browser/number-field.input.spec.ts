import { test, expect, type Page, type RenderMode } from './helpers';
import { Fixture } from './helpers/navigation';

// The input role's [L4] rows with real keys and a real wheel (issue 277, 6b; NUM-KEY-01, 02, 07, 09, 10; NUM-PTR-07):
// NumberFieldInput.test.tsx's and NumberFieldRoot.test.tsx's titles on the `number-field-input` fixture, Server then
// WebAssembly. The `number-field` module gates and prevents in the dispatch; C# steps and answers the text.

async function gotoInput(page: Page, mode: RenderMode, inputCase: string) {
  await page.goto(`/${mode}/number-field-input?case=${inputCase}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  const fixture = page.locator(Fixture.interactive('number-field-input'));
  await expect(fixture).toHaveAttribute('data-attached', 'true');
  const input = fixture.getByRole('textbox');
  await input.focus();
  return { fixture, input };
}

test('should not allow non-numeric characters on keydown', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'empty');

  await page.keyboard.press('a');

  await expect(input).toHaveValue('');
});

test('should increment on keydown ArrowUp', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'basic');

  await page.keyboard.press('ArrowUp');

  await expect(input).toHaveValue('1');
});

test('should decrement on keydown ArrowDown', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'basic');

  await page.keyboard.press('ArrowDown');

  await expect(input).toHaveValue('-1');
});

test('should set the value to min on keydown Home', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'bounds');

  await page.keyboard.press('Home');

  await expect(input).toHaveValue('-10');
});

test('should set the value to max on keydown End', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'bounds');

  await page.keyboard.press('End');

  await expect(input).toHaveValue('10');
});

// Base UI dispatches keydowns and a change; here the keys are typed, so the browser inserts what the gate lets through.
test('applies locale-aware decimal/group gating (de-DE)', async ({ page, mode }) => {
  const { input } = await gotoInput(page, mode, 'de');

  await page.keyboard.type('1,');
  await expect(input).toHaveValue('1,');

  await page.keyboard.press(',');
  await expect(input).toHaveValue('1,');

  await page.keyboard.type('..');
  await expect(input).toHaveValue('1,..');
});

test('should allow the user to scrub the input value with the mouse wheel', async ({ page, mode }) => {
  const { fixture, input } = await gotoInput(page, mode, 'wheel');
  const box = (await input.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);

  await page.mouse.wheel(0, 10);
  await expect(input).toHaveValue('4');

  await page.mouse.wheel(0, -10);
  await expect(input).toHaveValue('5');
  await expect(fixture).toHaveAttribute('data-changes', '4/wheel 5/wheel');
});
