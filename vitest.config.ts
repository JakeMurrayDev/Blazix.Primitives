import { defineConfig } from 'vitest/config';

// L3 (ADR 06 §1): each JavaScript module in isolation, in jsdom, against a fake twin.
export default defineConfig({
  test: {
    include: ['src/Iodine.Headless/js/**/*.test.ts', '.github/scripts/**/*.test.mjs'],
    environment: 'jsdom',
    // The red check's coverage marker (ADR 11 Amendments 2026-11-19): `it(title, { tags: ['coverage'] }, ...)`.
    tags: [{ name: 'coverage' }],
  },
});
