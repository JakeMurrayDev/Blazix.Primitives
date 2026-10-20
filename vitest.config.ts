import { defineConfig } from 'vitest/config';

// L3 (ADR 06 §1): each JavaScript module in isolation, in jsdom, against a fake twin.
export default defineConfig({
  test: {
    include: ['src/Blazix.Primitives/js/**/*.test.ts'],
    environment: 'jsdom',
  },
});
