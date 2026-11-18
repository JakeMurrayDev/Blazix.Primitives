// The `swipe` mechanism module (ADR 04 §1's `swipe` row; PSH §4.7.5): the values Drawer's and Toast's gestures share.
// Built by M6 track 1 (the M6 plan's decision 3 (a), chosen by the user 2026-11-18): the literal direction type, the
// displacement along a direction, the tracker's constants and the swipe-ignore selectors, exported so `toast` and the
// tracker import one copy (toast.md: "Same values").
// Not built here: the swipe tracker and its options (PSH-PTR-09..28, PSH-ALG-07..12, §6.1), the eight swipe rows ADR
// 04's `Affects` header added on 2026-11-18 (PSH-DOM-17/18, PSH-EVT-09, PSH-I18N-02, PSH-EDGE-10..13), and any twin or
// csproj entry (design point 2 (a), pending the user).
// Owner: session D (M6 track 8, PR 8b) from the merge of M6 track 1's PR (ADR 12 Amendments 2026-11-18 item 1).
// Transcribed from Base UI v1.9.0: packages/react/src/utils/useSwipeDismiss.ts:32-54; internals/constants.ts:8-12.

/** A physical swipe direction; never mirrored in a right-to-left layout. */
export type SwipeDirection = 'up' | 'down' | 'left' | 'right';

// PSH §4.7.5: the tracker's constants (useSwipeDismiss.ts:32-39).

/** The displacement, in pixels, a swipe must exceed to dismiss when no threshold is given. */
export const DEFAULT_SWIPE_THRESHOLD = 40;

/** The movement back, in pixels, that cancels a swipe once it has moved toward a direction. */
export const REVERSE_CANCEL_THRESHOLD = 10;

/** The shortest gesture duration, in milliseconds, over which the overall velocity is measured. */
export const MIN_VELOCITY_DURATION_MS = 50;

/** The shortest time between two samples, in milliseconds, over which a release velocity is measured. */
export const MIN_RELEASE_VELOCITY_DURATION_MS = 4;

/** The greatest age, in milliseconds, of the last sample a release velocity is measured from. */
export const MAX_RELEASE_VELOCITY_AGE_MS = 80;

/** The smallest movement, in pixels, recorded as a velocity sample. */
export const MIN_VELOCITY_SAMPLE_DISTANCE = 1;

/** The targets a swipe does not start on when no selector is given: the interactive elements. */
export const DEFAULT_IGNORE_SELECTOR = 'button,a,input,select,textarea,label,[role="button"]';

// UTL-EDGE-19, the swipe-ignore clause (internals/constants.ts:8-12). Toast ignores both selectors, joined with its own
// interactive list (ToastRoot.tsx:39,253: `button,a,input,textarea,[role="button"]` and both selectors); Drawer's
// Viewport ignores only BASE_UI_SWIPE_IGNORE_SELECTOR (DrawerViewport.tsx:1095), so `data-swipe-ignore` stops a toast's
// swipe and not a drawer's. Each consumer composes its selector from these.

/** The attribute that marks an element whose subtree does not start a swipe. */
export const BASE_UI_SWIPE_IGNORE_ATTRIBUTE = 'data-base-ui-swipe-ignore';

/** The earlier attribute with the same meaning, still honored where Base UI honors it. */
export const LEGACY_SWIPE_IGNORE_ATTRIBUTE = 'data-swipe-ignore';

/** Matches an element that carries {@link BASE_UI_SWIPE_IGNORE_ATTRIBUTE}, whatever its value. */
export const BASE_UI_SWIPE_IGNORE_SELECTOR = `[${BASE_UI_SWIPE_IGNORE_ATTRIBUTE}]`;

/** Matches an element that carries {@link LEGACY_SWIPE_IGNORE_ATTRIBUTE}. */
export const LEGACY_SWIPE_IGNORE_SELECTOR = `[${LEGACY_SWIPE_IGNORE_ATTRIBUTE}]`;

// PSH §4.7.5: displacement along a direction (useSwipeDismiss.ts:41-54).
/**
 * Measures a movement along a swipe direction.
 * @param direction The direction.
 * @param deltaX The horizontal movement, in pixels, positive to the right.
 * @param deltaY The vertical movement, in pixels, positive downward.
 * @returns The movement toward `direction` (`up`: `-deltaY`, `down`: `deltaY`, `left`: `-deltaX`, `right`: `deltaX`);
 *   0 for any other value.
 */
export function getDisplacement(direction: SwipeDirection, deltaX: number, deltaY: number): number {
  switch (direction) {
    case 'up':
      return -deltaY;
    case 'down':
      return deltaY;
    case 'left':
      return -deltaX;
    case 'right':
      return deltaX;
    default:
      return 0;
  }
}
