import { expect, it } from 'vitest';
import { thing } from './select.js';

// Synthetic pull request for the red check: against main's src/ this file cannot import ./select.js, so vitest reports
// the file as a failed suite; the red-check job must count the tests as red and pass. Not for merge.
it('synthetic: reads the thing of a module that main does not have', () => {
  expect(thing).toBe(1);
});

it.each([1, 2])('synthetic: a parametrized test of the same file, row %s', (row) => {
  expect(row).toBeGreaterThan(0);
});
