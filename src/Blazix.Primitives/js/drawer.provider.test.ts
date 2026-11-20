// L3 (ADR 06 §1): the `drawer` module's Provider visual state, its `indent` and `popup` roles, and the nested swipe progress
// a nested drawer publishes on its ancestors' popups. M6 track 8b slice 3 (issue 279). Base UI keeps the Provider's visual
// state (DRW-STA-15) and a Root's nested swipe progress (DRW-STA-13) in small stores outside React's render; the port keeps
// them in the module, keyed by the Provider's token and by the ancestor popup element, so the titles that drive those
// stores directly run here.
import { afterEach, describe, expect, it } from 'vitest';
import { attach, drawerVisualState, nestedSwipeProgress, type DrawerOptions } from './drawer';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];
const elements: HTMLElement[] = [];
let pointTarget: Element | null = null;
let providerCount = 0;

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  for (const element of elements.splice(0)) {
    element.remove();
  }
  pointTarget = null;
});

Object.defineProperty(document, 'elementFromPoint', { value: () => pointTarget, configurable: true });

const viewportDefaults: DrawerOptions = {
  role: 'viewport',
  open: true,
  mounted: true,
  nested: false,
  nestedOpenDrawerCount: 0,
  swipeDirection: 'down',
  answeredDismiss: 0,
  popup: 0,
  backdrop: 1,
};

function element(style?: string): HTMLElement {
  const created = document.createElement('div');
  if (style) {
    created.setAttribute('style', style);
  }
  document.body.append(created);
  elements.push(created);
  return created;
}

function track(handle: TwinHandle): TwinHandle {
  handles.push(handle);
  return handle;
}

/** An Indent as C# renders it, with its resting `--drawer-swipe-progress: 0`, attached to a fresh Provider's token. */
function indent(): { indent: HTMLElement; provider: string; handle: TwinHandle } {
  providerCount += 1;
  const provider = `provider-${providerCount}`;
  const target = element('--drawer-swipe-progress: 0');
  const handle = track(attach(target, JSON.stringify({ role: 'indent', provider }), createFakeTwin().twin));
  return { indent: target, provider, handle };
}

const progress = (target: HTMLElement) => target.style.getPropertyValue('--drawer-swipe-progress');
const height = (target: HTMLElement) => target.style.getPropertyValue('--drawer-height');

let stamp = 5000;

function pointer(type: 'pointerdown' | 'pointermove' | 'pointerup', target: Element, clientY: number): void {
  const event = new PointerEvent(type, {
    bubbles: true,
    cancelable: true,
    button: 0,
    buttons: type === 'pointerup' ? 0 : 1,
    pointerId: 1,
    pointerType: 'mouse',
    clientX: 0,
    clientY,
  });
  stamp += 100;
  Object.defineProperty(event, 'timeStamp', { value: stamp });
  target.dispatchEvent(event);
}

/** A drawer's viewport with a 100 px popup; `options` add the Provider token, the frontmost height and the ancestors. */
function viewport(options: Partial<DrawerOptions>, ancestors: HTMLElement[] = []) {
  const target = element();
  const backdrop = element('--drawer-swipe-progress: 0');
  const popup = document.createElement('div');
  target.append(popup);
  Object.defineProperty(popup, 'offsetHeight', { value: 100, configurable: true });
  Object.defineProperty(popup, 'offsetWidth', { value: 100, configurable: true });
  const fake = createFakeTwin();
  const references = [popup, backdrop, ...ancestors];
  const merged = { ...viewportDefaults, ancestorPopups: ancestors.map((_, index) => index + 2), ...options };
  const handle = track(attach(target, JSON.stringify(merged), fake.twin, references));
  pointTarget = popup;
  return {
    viewport: target,
    popup,
    backdrop,
    fake,
    handle,
    update: (next: Partial<DrawerOptions>) => handle.update(JSON.stringify({ ...merged, ...next }), references),
  };
}

describe('drawer module: Provider visual state (DrawerProvider.test.tsx)', () => {
  // DRW-STA-15, DRW-DOM-32 (DrawerProvider.test.tsx:153-176). Base UI's buttons call the store's `set`; the port's store is
  // the module's, keyed by the Provider's token.
  it('synchronizes and restores visual state on Drawer.Indent', () => {
    const { indent: target, provider, handle } = indent();
    const store = drawerVisualState(provider);

    store.set({ swipeProgress: 0.5, frontmostHeight: 120 });
    expect(progress(target)).toBe('0.5');
    expect(height(target)).toBe('120px');

    store.set({ swipeProgress: 0 });
    expect(progress(target)).toBe('0');
    expect(height(target)).toBe('120px');

    store.set({ frontmostHeight: 0 });
    expect(height(target)).toBe('');

    store.set({ swipeProgress: Number.NaN, frontmostHeight: Infinity });
    expect(progress(target)).toBe('0');
    expect(height(target)).toBe('');

    store.set({ swipeProgress: 0.5, frontmostHeight: 120 });
    handle.dispose();
    expect(progress(target)).toBe('0');
    expect(height(target)).toBe('');
  });

  // DRW-STA-15: listeners run only when a field changes; a non-finite value is stored as 0.
  it('port: the visual state notifies only when a field changes', () => {
    const { provider } = indent();
    const store = drawerVisualState(provider);
    let notified = 0;
    const unsubscribe = store.subscribe(() => {
      notified += 1;
    });

    store.set({ swipeProgress: 0.5 });
    store.set({ swipeProgress: 0.5, frontmostHeight: 0 });
    expect(notified).toBe(1);

    store.set({ swipeProgress: Number.NaN });
    expect(store.get()).toEqual({ swipeProgress: 0, frontmostHeight: 0 });
    expect(notified).toBe(2);
    unsubscribe();
  });

  // DRW-PTR-16's Provider half (DrawerViewport.test.tsx:5033-5108): 39 px of a 100 px popup's drag is 0.39, shown with the
  // frontmost height, and both clear when the drag returns to its start.
  it('port: a viewport publishes its swipe progress and frontmost height through its Provider', () => {
    const { indent: target, provider } = indent();
    const { viewport: root } = viewport({ provider, frontmostHeight: 100 });

    pointer('pointerdown', root, 0);
    pointer('pointermove', root, 1);
    pointer('pointermove', root, 40);
    expect(Number.parseFloat(progress(target))).toBeCloseTo(0.39);
    expect(height(target)).toBe('100px');

    pointer('pointermove', root, 0);
    expect(progress(target)).toBe('0');
    expect(height(target)).toBe('');
    pointer('pointerup', root, 0);
  });

  // DRW-ANI-05 (DrawerViewport.test.tsx:5327-5420): the Viewport's teardown clears the Provider's visual state and the
  // Backdrop's `data-swiping`.
  it('clears provider and backdrop swipe state during layout teardown', () => {
    const { indent: target, provider } = indent();
    const { backdrop, handle } = viewport({ provider, frontmostHeight: 100 });
    drawerVisualState(provider).set({ swipeProgress: 0.5, frontmostHeight: 100 });
    backdrop.setAttribute('data-swiping', '');
    expect(progress(target)).toBe('0.5');
    expect(height(target)).toBe('100px');

    handle.dispose();
    expect(progress(target)).toBe('0');
    expect(height(target)).toBe('');
    expect(backdrop.hasAttribute('data-swiping')).toBe(false);
  });

  // DRW-PTR-16: a nested drawer publishes no Provider progress, and a drawer without a Provider publishes none.
  it('port: a nested drawer leaves the Provider at rest', () => {
    const { indent: target, provider } = indent();
    const { viewport: root } = viewport({ provider, frontmostHeight: 100, nested: true });

    pointer('pointerdown', root, 0);
    pointer('pointermove', root, 1);
    pointer('pointermove', root, 40);
    expect(progress(target)).toBe('0');
    expect(height(target)).toBe('');
    pointer('pointerup', root, 0);
  });
});

/** A Popup's handle: `offsetHeight` reads the variable `measured`, as the browser's layout would. */
function popupRole(options: Record<string, unknown> = {}) {
  const target = element('--nested-drawers: 0');
  let measured = 100;
  Object.defineProperty(target, 'offsetHeight', { get: () => measured, configurable: true });
  const fake = createFakeTwin();
  const merged = { role: 'popup', open: true, mounted: true, hasNestedDrawer: false, ending: false, frontmostHeight: 0, ...options };
  const handle = track(attach(target, JSON.stringify(merged), fake.twin));
  const reports = () => fake.reports.filter((report) => report.method === 'OnPopupHeightChange').map((report) => (report.payload as { height: number }).height);
  return {
    popup: target,
    fake,
    handle,
    reports,
    resize(next: number) {
      measured = next;
    },
    update: (next: Record<string, unknown>) => handle.update(JSON.stringify(Object.assign(merged, next))),
  };
}

describe('drawer module: the popup role (DrawerPopup.tsx)', () => {
  // DRW-STA-10 (c), DRW-DOM-14: the height is measured on attach, and the popup declares no nested progress yet.
  it('port: the popup reports its measured height on attach and declares no nested progress', () => {
    const { popup, reports } = popupRole();
    expect(reports()).toEqual([100]);
    expect(progress(popup)).toBe('0');
    expect(height(popup)).toBe('');
  });

  // DRW-STA-10 (a), (b) (DrawerPopup.test.tsx "keeps the last measured popup height while nested content stretches it"): a
  // measurement runs when the popup mounts and when a nested drawer opens or closes (DrawerPopup.tsx:217-242).
  it('port: keeps the last measured popup height while nested content stretches it', () => {
    const { reports, resize, update } = popupRole();
    update({ hasNestedDrawer: true, frontmostHeight: 200, nestedOpenDrawerCount: 1 });
    expect(reports()).toEqual([100, 100]);

    resize(200);
    update({ nestedOpenDrawerCount: 0 });
    expect(reports()).toEqual([100, 100]);

    resize(100);
    update({ hasNestedDrawer: false, frontmostHeight: 100, nestedOpenDrawerCount: 1 });
    expect(reports()).toEqual([100, 100]);

    resize(150);
    update({ nestedOpenDrawerCount: 0 });
    expect(reports()).toEqual([100, 100, 150]);
  });

  // DRW-STA-10: an unmounted popup reports 0 each time it re-runs, and never measures.
  it('port: an unmounted popup reports 0 and ignores a later measurement', () => {
    const { reports, resize, update } = popupRole();
    update({ mounted: false });
    expect(reports()).toEqual([100, 0]);

    resize(140);
    update({ nestedOpenDrawerCount: 1 });
    expect(reports()).toEqual([100, 0, 0]);
  });

  // DRW-DOM-09: `--drawer-height` only while a nested drawer is present or the popup animates out.
  it('port: the popup declares its height only while a nested drawer is present or it is ending', () => {
    const { popup, update } = popupRole();
    expect(height(popup)).toBe('');

    update({ hasNestedDrawer: true });
    expect(height(popup)).toBe('100px');

    update({ hasNestedDrawer: false });
    expect(height(popup)).toBe('');

    update({ ending: true });
    expect(height(popup)).toBe('100px');

    update({ ending: false, mounted: false });
    expect(height(popup)).toBe('');
  });

  // DRW-STA-10 (DrawerPopup.test.tsx "ignores a queued measurement after the popup unmounts"): a resize notification that
  // runs after the handle is disposed measures nothing and reports nothing.
  it('port: a resize notification queued before the popup unmounts reports nothing after it', () => {
    const callbacks: ResizeObserverCallback[] = [];
    const original = globalThis.ResizeObserver;
    globalThis.ResizeObserver = class {
      constructor(callback: ResizeObserverCallback) {
        callbacks.push(callback);
      }
      observe() {}
      unobserve() {}
      disconnect() {}
    } as unknown as typeof ResizeObserver;
    try {
      const { popup, handle, reports, resize } = popupRole();
      // The port's observer callback runs per entry, so the queued notification carries the popup's.
      const entries = [{ target: popup } as unknown as ResizeObserverEntry];
      resize(140);
      for (const callback of callbacks) {
        callback(entries, {} as ResizeObserver);
      }
      expect(reports()).toEqual([100, 140]);

      handle.dispose();
      resize(180);
      for (const callback of callbacks) {
        callback(entries, {} as ResizeObserver);
      }
      expect(reports()).toEqual([100, 140]);
    } finally {
      globalThis.ResizeObserver = original;
    }
  });

  // DRW-DOM-09: "in place in the same commit that adds data-nested-drawer-open". C# renders that attribute before the nested
  // drawer's presence reaches the module, so the popup role reads the attribute too, in the mutation's own microtask.
  it('port: the popup declares its height as soon as C# marks a nested drawer open', async () => {
    const { popup } = popupRole();
    const seen: string[] = [];
    new MutationObserver(() => seen.push(`${popup.hasAttribute('data-nested-drawer-open')}:${height(popup)}`)).observe(popup, {
      attributes: true,
      attributeFilter: ['data-nested-drawer-open', 'style'],
    });

    popup.setAttribute('data-nested-drawer-open', '');
    await Promise.resolve();
    expect(seen[0]).toBe('true:100px');

    popup.removeAttribute('data-nested-drawer-open');
    await Promise.resolve();
    expect(height(popup)).toBe('');
  });

  // DRW-STA-13 (DrawerRoot.test.tsx:973-998).
  it('normalizes invalid nested swipe progress without notifying for duplicate values', () => {
    const { popup } = popupRole();
    const channel = nestedSwipeProgress(popup);
    let notified = 0;
    const unsubscribe = channel.subscribe(() => {
      notified += 1;
    });

    channel.set(0.5);
    expect(channel.get()).toBe(0.5);
    expect(progress(popup)).toBe('0.5');
    expect(notified).toBe(1);

    channel.set(0.5);
    expect(notified).toBe(1);

    channel.set(Number.NaN);
    expect(channel.get()).toBe(0);
    expect(progress(popup)).toBe('0');
    expect(notified).toBe(2);
    unsubscribe();
  });

  // DRW-DOM-14, DRW-PTR-16's parent half, DRW-STA-13's forwarding: a nested drawer's drag shows on every ancestor popup
  // and clears when the drag ends.
  it('port: a nested drawer publishes its drag progress on its ancestors\' popups', () => {
    const { popup: grandparent } = popupRole();
    const { popup: parent } = popupRole();
    const { viewport: root } = viewport({ nested: true, hasParent: true }, [parent, grandparent]);

    pointer('pointerdown', root, 0);
    pointer('pointermove', root, 1);
    pointer('pointermove', root, 40);
    expect(Number.parseFloat(progress(parent))).toBeCloseTo(0.39);
    expect(Number.parseFloat(progress(grandparent))).toBeCloseTo(0.39);

    pointer('pointerup', root, 40);
    expect(progress(parent)).toBe('0');
    expect(progress(grandparent)).toBe('0');
  });

  // DRW-DOM-14 (DrawerViewport.test.tsx "clears nested progress before descendant layout effects when the child closes"):
  // the child's close and its teardown send 0.
  it('port: a nested drawer clears its ancestors\' progress when it closes and when it is torn down', () => {
    const { popup: parent } = popupRole();
    const { update, handle } = viewport({ nested: true, hasParent: true }, [parent]);
    nestedSwipeProgress(parent).set(0.5);
    expect(progress(parent)).toBe('0.5');

    update({ open: false });
    expect(progress(parent)).toBe('0');

    nestedSwipeProgress(parent).set(0.5);
    handle.dispose();
    expect(progress(parent)).toBe('0');
  });
});

describe('drawer module: the nested swipe report (DRW-PTR-17)', () => {
  // DRW-PTR-17: once per gesture, from 10 px along the direction; `false` only after `true`, here when the drag returns
  // to its start, and nothing without a parent drawer.
  it('port: a nested drawer reports swiping to its parent once past the threshold and clears it when the drag returns', () => {
    const { popup: parent } = popupRole();
    const { viewport: root, fake } = viewport({ nested: true, hasParent: true }, [parent]);
    const swiping = () => fake.reported<{ swiping: boolean }>('OnNestedSwipingChange').map((change) => change.swiping);

    pointer('pointerdown', root, 0);
    pointer('pointermove', root, 1);
    pointer('pointermove', root, 9);
    expect(swiping()).toEqual([]);

    pointer('pointermove', root, 12);
    pointer('pointermove', root, 30);
    expect(swiping()).toEqual([true]);

    pointer('pointermove', root, 0);
    expect(swiping()).toEqual([true, false]);
    pointer('pointerup', root, 0);
    expect(swiping()).toEqual([true, false]);

    const { viewport: topLevel, fake: topFake } = viewport({});
    pointer('pointerdown', topLevel, 0);
    pointer('pointermove', topLevel, 1);
    pointer('pointermove', topLevel, 30);
    pointer('pointerup', topLevel, 30);
    expect(topFake.reported('OnNestedSwipingChange')).toEqual([]);
  });
});

export type { FakeTwin };
