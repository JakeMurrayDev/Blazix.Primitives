import { defineConfig } from 'vitest/config';

// The Content-move probe only (M5 track 5); not part of the L3 suite.
export default defineConfig({
  test: {
    include: ['spikes/navigation-menu-content-move/**/*.test.ts'],
    environment: 'jsdom',
  },
});
