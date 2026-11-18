// Red stub (M6 track 1): every export is present with a wrong value; the next commit replaces it.
export type SwipeDirection = 'up' | 'down' | 'left' | 'right';
export const DEFAULT_SWIPE_THRESHOLD = 0;
export const REVERSE_CANCEL_THRESHOLD = 0;
export const MIN_VELOCITY_DURATION_MS = 0;
export const MIN_RELEASE_VELOCITY_DURATION_MS = 0;
export const MAX_RELEASE_VELOCITY_AGE_MS = 0;
export const MIN_VELOCITY_SAMPLE_DISTANCE = 0;
export const DEFAULT_IGNORE_SELECTOR = '[data-none]';
export const BASE_UI_SWIPE_IGNORE_ATTRIBUTE = '';
export const LEGACY_SWIPE_IGNORE_ATTRIBUTE = '';
export const BASE_UI_SWIPE_IGNORE_SELECTOR = '[data-none]';
export const LEGACY_SWIPE_IGNORE_SELECTOR = '[data-none]';
export function getDisplacement(direction: SwipeDirection, deltaX: number, deltaY: number): number {
  void direction;
  void deltaX;
  void deltaY;
  return Number.NaN;
}
