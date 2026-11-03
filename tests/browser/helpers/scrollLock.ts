import { expect, type Page } from '@playwright/test';

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

/** A third-party scroll locker's DOM mechanism; `lock` runs in the page and returns its release. */
export interface ExternalLocker {
  name: string;
  lock: () => () => void;
}

/** DialogRoot.test.tsx's three third-party lockers ("external scroll lock handoff", v1.9.0), each its exact DOM mechanism. */
export const externalLockers: readonly ExternalLocker[] = [
  {
    name: 'react-remove-scroll, via an attribute and a stylesheet',
    lock: () => {
      const style = document.createElement('style');
      style.textContent = 'body[data-scroll-locked]{overflow:hidden!important;}';
      document.head.appendChild(style);
      document.body.setAttribute('data-scroll-locked', '1');
      return () => {
        style.remove();
        document.body.removeAttribute('data-scroll-locked');
      };
    },
  },
  {
    name: 'silk-hq, via the <body> overflow shorthand',
    lock: () => {
      document.body.style.setProperty('overflow', 'hidden');
      return () => document.body.style.removeProperty('overflow');
    },
  },
  {
    name: 'Ariakit, via <html> overflow longhands',
    lock: () => {
      const { style } = document.documentElement;
      style.setProperty('scrollbar-gutter', 'stable');
      style.setProperty('overflow-x', 'hidden');
      style.setProperty('overflow-y', 'hidden');
      return () => {
        style.removeProperty('scrollbar-gutter');
        style.removeProperty('overflow-x');
        style.removeProperty('overflow-y');
      };
    },
  },
];

/** The page's lock after one DOM change, and whether the third-party lock had been released by then. */
export interface PageLockRecord {
  locked: boolean;
  released: boolean;
}

interface ExternalLockWindow {
  externalLock: { records: PageLockRecord[]; released: boolean; release: () => void };
}

/**
 * Applies a third-party lock and records the page's lock from then on (issue 154): a `MutationObserver` of `<html>`'s
 * subtree reads it in a microtask queued after each change's observers, as the next frame would see the page, so a gap
 * any frame could paint is recorded and nothing waits on a timer (ADR 06 §2). The lock is DialogRoot.test.tsx's
 * isPageLocked: the viewport scroller's computed overflow hides or clips.
 */
export async function lockExternally(page: Page, locker: ExternalLocker): Promise<void> {
  await page.evaluate((lockSource) => {
    const isPageLocked = () => {
      const html = document.documentElement;
      const { overflow, overflowX, overflowY } = getComputedStyle(html);
      const htmlScrolls = /auto|scroll|overlay|hidden|clip/.test(overflow + overflowY + overflowX);
      return /hidden|clip/.test(getComputedStyle(htmlScrolls ? html : document.body).overflowY);
    };
    const state: ExternalLockWindow['externalLock'] = { records: [], released: false, release: () => {} };
    (window as unknown as ExternalLockWindow).externalLock = state;
    new MutationObserver(() => {
      queueMicrotask(() => state.records.push({ locked: isPageLocked(), released: state.released }));
    }).observe(document.documentElement, { attributes: true, childList: true, subtree: true });
    // eslint-disable-next-line no-new-func
    state.release = new Function(`return (${lockSource})()`)() as () => void;
  }, locker.lock.toString());
}

/**
 * Releases the third-party lock once the popup's own lock is waiting for it, as Base UI's test defers the release past
 * its deferred lock (an exit animation in practice). The `scroll-lock` module the library loaded counts the popup as a
 * holder, and the release runs in a 0 ms timer queued after the module's deferred lock (a 0 ms timer from the
 * acquisition), which has then found the page locked and observes it. A fixed delay instead raced the popup's own lock on
 * a slow host (issue 154).
 */
export async function releaseExternalLockWhenPending(page: Page): Promise<void> {
  // The import map resolves the library's path to the same URL the library imported, so this is its instance.
  await expect
    .poll(() =>
      page.evaluate(async (path) => {
        const module = (await import(path)) as { scrollLockCount: () => number };
        return module.scrollLockCount();
      }, '/_content/Blazix.Primitives/js/scroll-lock.js'),
    )
    .toBe(1);
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        setTimeout(() => {
          const state = (window as unknown as ExternalLockWindow).externalLock;
          state.released = true;
          state.release();
          resolve();
        }, 0);
      }),
  );
}

/** The records since `lockExternally`. */
export function pageLockRecords(page: Page): Promise<PageLockRecord[]> {
  return page.evaluate(() => (window as unknown as ExternalLockWindow).externalLock.records);
}
