// L3 (ADR 06 §1): the `swipe` mechanism module's shared constants, selectors and displacement in jsdom. Base UI tests none
// of them directly (they are module constants of utils/useSwipeDismiss.ts and internals/constants.ts), so every title is
// the port's own: PSH §4.7.5's constants and displacement, and UTL-EDGE-19's swipe-ignore clause. The tracker's titles are
// session D's (PR 8b).
import { describe, expect, it } from 'vitest';
import {
  BASE_UI_SWIPE_IGNORE_ATTRIBUTE,
  BASE_UI_SWIPE_IGNORE_SELECTOR,
  DEFAULT_IGNORE_SELECTOR,
  DEFAULT_SWIPE_THRESHOLD,
  LEGACY_SWIPE_IGNORE_ATTRIBUTE,
  LEGACY_SWIPE_IGNORE_SELECTOR,
  MAX_RELEASE_VELOCITY_AGE_MS,
  MIN_RELEASE_VELOCITY_DURATION_MS,
  MIN_VELOCITY_DURATION_MS,
  MIN_VELOCITY_SAMPLE_DISTANCE,
  REVERSE_CANCEL_THRESHOLD,
  getDisplacement,
  type SwipeDirection,
} from './swipe';

function fragment(html: string): HTMLElement {
  const host = document.createElement('div');
  host.innerHTML = html;
  return host;
}

// PSH §4.7.5 (useSwipeDismiss.ts:41-54).
describe('getDisplacement', () => {
  it('port: measures the displacement along each direction, positive toward it', () => {
    expect(getDisplacement('up', 3, -20)).toBe(20);
    expect(getDisplacement('up', 3, 20)).toBe(-20);
    expect(getDisplacement('down', 3, 20)).toBe(20);
    expect(getDisplacement('down', 3, -20)).toBe(-20);
    expect(getDisplacement('left', -15, 4)).toBe(15);
    expect(getDisplacement('left', 15, 4)).toBe(-15);
    expect(getDisplacement('right', 15, 4)).toBe(15);
    expect(getDisplacement('right', -15, 4)).toBe(-15);
  });

  it('port: measures zero along an unknown direction', () => {
    expect(getDisplacement('right', 15, 4)).toBe(15);
    expect(getDisplacement('diagonal' as SwipeDirection, 15, 4)).toBe(0);
    expect(getDisplacement(undefined as unknown as SwipeDirection, 15, 4)).toBe(0);
  });
});

// PSH §4.7.5 (useSwipeDismiss.ts:32-39).
describe('constants', () => {
  it("port: keeps the tracker's thresholds and durations", () => {
    expect(DEFAULT_SWIPE_THRESHOLD).toBe(40);
    expect(REVERSE_CANCEL_THRESHOLD).toBe(10);
    expect(MIN_VELOCITY_DURATION_MS).toBe(50);
    expect(MIN_RELEASE_VELOCITY_DURATION_MS).toBe(4);
    expect(MAX_RELEASE_VELOCITY_AGE_MS).toBe(80);
    expect(MIN_VELOCITY_SAMPLE_DISTANCE).toBe(1);
  });

  it('port: the default ignored-target selector matches each interactive element it lists, and not a plain element', () => {
    const host = fragment(
      '<button></button><a></a><input><select></select><textarea></textarea><label></label><div role="button"></div><div id="plain"></div><span role="link"></span>',
    );
    const matched = [...host.children].filter((element) => element.matches(DEFAULT_IGNORE_SELECTOR));
    expect(matched.map((element) => element.tagName.toLowerCase() + (element.getAttribute('role') ?? ''))).toEqual([
      'button',
      'a',
      'input',
      'select',
      'textarea',
      'label',
      'divbutton',
    ]);
    expect(DEFAULT_IGNORE_SELECTOR).toBe('button,a,input,select,textarea,label,[role="button"]');
  });
});

// UTL-EDGE-19, the swipe-ignore clause (internals/constants.ts:8-12).
describe('swipe-ignore selectors', () => {
  it("port: each swipe-ignore selector matches its own attribute and not the other's", () => {
    const host = fragment('<div data-base-ui-swipe-ignore></div><div data-swipe-ignore></div><div data-base-ui-swipe-ignore="x"></div>');
    const [current, legacy, axis] = [...host.children];

    expect(BASE_UI_SWIPE_IGNORE_ATTRIBUTE).toBe('data-base-ui-swipe-ignore');
    expect(LEGACY_SWIPE_IGNORE_ATTRIBUTE).toBe('data-swipe-ignore');
    expect(BASE_UI_SWIPE_IGNORE_SELECTOR).toBe('[data-base-ui-swipe-ignore]');
    expect(LEGACY_SWIPE_IGNORE_SELECTOR).toBe('[data-swipe-ignore]');

    expect(current!.matches(BASE_UI_SWIPE_IGNORE_SELECTOR)).toBe(true);
    expect(axis!.matches(BASE_UI_SWIPE_IGNORE_SELECTOR)).toBe(true);
    expect(legacy!.matches(LEGACY_SWIPE_IGNORE_SELECTOR)).toBe(true);
    expect(current!.matches(LEGACY_SWIPE_IGNORE_SELECTOR)).toBe(false);
    expect(legacy!.matches(BASE_UI_SWIPE_IGNORE_SELECTOR)).toBe(false);
  });
});
