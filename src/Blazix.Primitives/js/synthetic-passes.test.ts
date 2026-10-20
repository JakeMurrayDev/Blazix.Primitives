import { expect, it } from 'vitest';

// Synthetic pull request for the red check (ADR 11 section 3): this vitest test already passes against main's src/, so
// the red-check job must fail. Not for merge.
it('synthetic: passes against main', () => {
  expect(1 + 1).toBe(2);
});
