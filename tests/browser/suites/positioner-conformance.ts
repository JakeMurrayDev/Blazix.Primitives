import { type Locator } from '@playwright/test';
import { test, expect, Fixture, fixtureUrl, type Page, type RenderMode } from '../helpers';

// Base UI's test/positionerConformanceTests.tsx as one helper (ADR 06 §1, the shared positioner suite; M4 track 2): the
// anchor positioning every `*.Positioner` shares through the `positioning` module, browser-only in Base UI too. Titles
// are Base UI's, except "reads $description inside the function", restated as "applies the override for $description"
// (design point 2 of the positioning track, settled by the orchestrator 2026-10-27: the port's offset is a declarative
// record evaluated on every pass, so the side or align a pass saw is witnessed by the pixel position the override
// produces, with data-side and data-align). Called by each Positioner family's spec, and by tests/browser/positioning.spec.ts
// on the test-only FixturePositioner.
//
// The case contract a family fixture implements. The page renders one case, named by its `?case=` query, inside its
// FixtureContainer, which carries `data-attached="true"` once the case's positioner (if rendered at first render) has its
// behavior attached. The anchor is the element the positioner is anchored to: 72×36, border-box, at the viewport origin
// (fixed there, since the host keeps the browser's 8px body margin), and the positioner carries `data-testid="positioner"`
// and holds a 52×24 popup, so the baseline is translate(10px, 36px).
// - `default`: no offsets.
// - `sideOffset-number`, `alignOffset-number`: the offset 7.
// - `sideOffset-function`: `new Offset(AnchorWidth: 1, PositionerWidth: 1)`; `alignOffset-function`: `new Offset(PositionerWidth: 1)`.
// - `<prop>-override-<n>`, n = 1..4, Base UI's callback-data cases, the prop being `new Offset { Overrides = [o] }` with a
//   zero base and one override `o` of `Constant: 5` keyed by the expected value:
//   1 `side: Left`, keyed `Side.Right`; 2 `side: Right, align: Start`, keyed `Align.End`; 3 `side: InlineStart`, keyed
//   `Side.InlineEnd`; 4 `side: InlineStart` under `<DirectionProvider Direction="Rtl">`, keyed `Side.InlineStart`.
// - `collision-padding`: the anchor inside a `position: fixed; bottom: 8px; left: 16px` wrapper; `side: Bottom`,
//   `sideOffset: 8`, `collisionPadding: 12`, `collisionAvoidance: { fallbackAxisSide: None }`; the popup
//   `width: 200px; height: 1000px; max-height: var(--available-height)`; closed at first, opened by `[data-open-button]`.
// - with `viewport: true`, `viewport` (the Viewport part rendered, or the stand-in) and `viewport-toggle` (a
//   `[data-viewport-toggle]` button rendered after the anchor mounts and unmounts it).

/** What a family passes: its fixture and whether it has a Viewport part. */
export interface PositionerConformanceConfig {
  /** The fixture that renders the cases of the contract above. */
  fixture: string;
  /** Whether the component has a Viewport part, which switches the positioner to top/left positioning. Defaults to `false`. */
  viewport?: boolean;
}

// The anchor sits at the viewport origin and the popup is centered below it, so the positioner starts at
// x = (anchorWidth - popupWidth) / 2 and y = anchorHeight.
const anchorWidth = 72;
const anchorHeight = 36;
const popupWidth = 52;
const baselineX = (anchorWidth - popupWidth) / 2;
const baselineY = anchorHeight;

const offsetCases = [
  {
    prop: 'sideOffset',
    numberPosition: (offset: number) => ({ x: baselineX, y: baselineY + offset }),
    functionPosition: { x: baselineX, y: baselineY + popupWidth + anchorWidth },
    // Where each override case renders: the flipped or requested horizontal side plus 5, centered or end-aligned.
    overridePositions: [
      { x: anchorWidth + 5, y: 6 },
      { x: anchorWidth + 5, y: 12 },
      { x: anchorWidth + 5, y: 6 },
      { x: anchorWidth + 5, y: 6 },
    ],
  },
  {
    prop: 'alignOffset',
    numberPosition: (offset: number) => ({ x: baselineX + offset, y: baselineY }),
    functionPosition: { x: baselineX + popupWidth, y: baselineY },
    // A positive align offset moves a centered popup toward the end and an end-aligned one toward the start.
    overridePositions: [
      { x: anchorWidth, y: 6 + 5 },
      { x: anchorWidth, y: 12 - 5 },
      { x: anchorWidth, y: 6 + 5 },
      { x: anchorWidth, y: 6 + 5 },
    ],
  },
] as const;

const callbackDataCases = [
  { description: 'the latest side after a flip', key: 'side', expected: 'right' },
  { description: 'the latest align after a flip', key: 'align', expected: 'end' },
  { description: 'the logical side after a flip', key: 'side', expected: 'inline-end' },
  { description: 'the logical side in RTL mode', key: 'side', expected: 'inline-start' },
] as const;

/** Opens one case of a positioner fixture and waits until its positioner, if rendered, has its behavior attached. */
export async function gotoPositionerCase(page: Page, mode: RenderMode, fixture: string, name: string): Promise<Locator> {
  await page.goto(`${fixtureUrl(mode, fixture)}?case=${name}`, { waitUntil: 'commit' });
  await page.locator(Fixture.started).waitFor({ state: 'attached' });
  await page.locator(Fixture.interactive(fixture)).first().waitFor({ state: 'attached' });
  const container = page.locator(Fixture.container(fixture));
  await expect(container).toHaveAttribute('data-attached', 'true');
  return container;
}

/** Reads one property of the element's inline style. */
export function inlineStyle(element: Locator, property: string): Promise<string> {
  return element.evaluate((node, name) => (node as HTMLElement).style.getPropertyValue(name), property);
}

async function expectPosition(positioner: Locator, x: number, y: number): Promise<void> {
  await expect.poll(() => inlineStyle(positioner, 'transform')).toBe(`translate(${x}px, ${y}px)`);
  const rect = await positioner.evaluate((node) => {
    const { x: left, y: top } = node.getBoundingClientRect();
    return { x: left, y: top };
  });
  expect(rect).toEqual({ x, y });
}

// Base UI's waitForPositioned: opacity no longer 0, and visible.
async function waitForPositioned(positioner: Locator): Promise<void> {
  await expect.poll(() => inlineStyle(positioner, 'opacity')).not.toBe('0');
  await expect(positioner).toBeVisible();
}

/**
 * Tests the anchor positioning behavior that every `*.Positioner` part shares through the `positioning` module. All
 * cases need real layout, so they run in the browser only.
 * @param config The family's fixture and whether it has a Viewport part.
 */
export function positionerConformanceTests(config: PositionerConformanceConfig): void {
  const { fixture, viewport: hasViewport = false } = config;

  test.describe('Positioner conformance', () => {
    for (const { prop, numberPosition, functionPosition, overridePositions } of offsetCases) {
      test.describe(`prop: ${prop}`, () => {
        test('offsets the positioner when a number is specified', async ({ page, mode }) => {
          const container = await gotoPositionerCase(page, mode, fixture, `${prop}-number`);

          const { x, y } = numberPosition(7);
          await expectPosition(container.getByTestId('positioner'), x, y);
        });

        test('offsets the positioner when a function is specified', async ({ page, mode }) => {
          const container = await gotoPositionerCase(page, mode, fixture, `${prop}-function`);

          await expectPosition(container.getByTestId('positioner'), functionPosition.x, functionPosition.y);
        });

        callbackDataCases.forEach(({ description, key, expected }, index) => {
          test(`applies the override for ${description}`, async ({ page, mode }) => {
            const container = await gotoPositionerCase(page, mode, fixture, `${prop}-override-${index + 1}`);
            const positioner = container.getByTestId('positioner');

            await expect(positioner).toHaveAttribute(`data-${key}`, expected);
            const { x, y } = overridePositions[index]!;
            await expectPosition(positioner, x, y);
          });
        });
      });
    }

    // https://github.com/mui/base-ui/issues/5131
    test('rests exactly at collisionPadding from the colliding edge', async ({ page, mode }) => {
      const collisionPadding = 12;
      const container = await gotoPositionerCase(page, mode, fixture, 'collision-padding');

      await container.locator('[data-open-button]').click();

      const positioner = container.getByTestId('positioner');
      await expect(positioner).toHaveAttribute('data-side', 'top');

      // The preferred-side bias used by flip() must not leak into the resting position: the popup should sit exactly
      // `collisionPadding` away from the top edge, not +1px.
      await expect.poll(() => positioner.evaluate((node) => Math.round(node.getBoundingClientRect().top))).toBe(collisionPadding);
    });

    test('uses transform positioning without Viewport', async ({ page, mode }) => {
      const container = await gotoPositionerCase(page, mode, fixture, 'default');

      await expect.poll(() => inlineStyle(container.getByTestId('positioner'), 'transform')).toBe(`translate(${baselineX}px, ${baselineY}px)`);
    });

    if (hasViewport) {
      test('uses top/left positioning with Viewport', async ({ page, mode }) => {
        const container = await gotoPositionerCase(page, mode, fixture, 'viewport');
        const positioner = container.getByTestId('positioner');

        await waitForPositioned(positioner);
        expect(await inlineStyle(positioner, 'transform')).toBe('');
        // A pass can measure the popup before it has a size, so wait for the final coordinates.
        await expect.poll(() => inlineStyle(positioner, 'left')).toBe(`${baselineX}px`);
        await expect.poll(() => inlineStyle(positioner, 'top')).toBe(`${baselineY}px`);
      });

      test('updates positioning when Viewport mounts and unmounts', async ({ page, mode }) => {
        const container = await gotoPositionerCase(page, mode, fixture, 'viewport-toggle');
        const positioner = container.getByTestId('positioner');
        const toggle = container.locator('[data-viewport-toggle]');

        await expect.poll(() => inlineStyle(positioner, 'transform')).toBe(`translate(${baselineX}px, ${baselineY}px)`);

        await toggle.click();
        await expect.poll(() => inlineStyle(positioner, 'transform')).toBe('');
        await expect.poll(() => inlineStyle(positioner, 'top')).toBe(`${baselineY}px`);

        await toggle.click();
        await expect.poll(() => inlineStyle(positioner, 'transform')).toBe(`translate(${baselineX}px, ${baselineY}px)`);
      });
    }
  });
}
