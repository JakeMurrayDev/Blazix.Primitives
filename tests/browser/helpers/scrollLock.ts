import { type Page } from '@playwright/test';

/**
 * Whether Base UI's scroll lock is active on the page's document (packages/react/test/scrollLock.ts at v1.9.0): an inline
 * `overflow: hidden` on `<html>` or `<body>`, or the full path's `data-base-ui-scroll-locked` on `<html>`. Shared by the
 * popup families' specs and the shared suites.
 */
export function isScrollLocked(page: Page): Promise<boolean> {
  return page.evaluate(
    () =>
      document.documentElement.style.overflow === 'hidden' ||
      document.documentElement.hasAttribute('data-base-ui-scroll-locked') ||
      document.body.style.overflow === 'hidden',
  );
}
