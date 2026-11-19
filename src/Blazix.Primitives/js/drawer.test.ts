// L3 (ADR 06 §1): the `drawer` component module in jsdom, against the fake twin. Slice 1 of M6 track 8b (issue 279):
// the Viewport handle's mouse and pen dismiss gesture without snap points (DRW-PTR-01…05, 07…10, 12…14, the
// Backdrop half of PTR-16), driven through the `swipe` tracker. Base UI's titles for these rows are browser titles
// (DrawerViewport.test.tsx, DrawerRoot.test.tsx) and run at L4 in tests/browser/drawer.spec.ts; these port titles pin
// the module's own contract: its writes, its reports and its `dismiss` decision point.
import { afterEach, describe, expect, it } from 'vitest';
import { attach, type DrawerOptions } from './drawer';
import { styleWriter } from './dom';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];
const elements: HTMLElement[] = [];
let pointTarget: Element | null = null;

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  for (const element of elements.splice(0)) {
    element.remove();
  }
  pointTarget = null;
  document.getSelection()?.removeAllRanges();
});

// jsdom has no hit testing: the element under the point is the target the test names.
Object.defineProperty(document, 'elementFromPoint', { value: () => pointTarget, configurable: true });

const defaults: DrawerOptions = {
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

interface Mounted {
  viewport: HTMLElement;
  popup: HTMLElement;
  backdrop: HTMLElement;
  content: HTMLElement;
  fake: FakeTwin;
  handle: TwinHandle;
  update(options: Partial<DrawerOptions>): void;
}

function mount(options: Partial<DrawerOptions> = {}, synchronous = false, verdict: 'run' | 'stop' = 'run'): Mounted {
  const viewport = document.createElement('div');
  const backdrop = document.createElement('div');
  const popup = document.createElement('div');
  const content = document.createElement('div');
  content.setAttribute('data-drawer-content', '');
  popup.append(content);
  viewport.append(popup);
  document.body.append(backdrop, viewport);
  elements.push(backdrop, viewport);
  Object.defineProperty(popup, 'offsetHeight', { value: 200, configurable: true });
  Object.defineProperty(popup, 'offsetWidth', { value: 300, configurable: true });

  const fake = createFakeTwin({ synchronous, verdict });
  const handle = attach(viewport, JSON.stringify({ ...defaults, ...options }), fake.twin, [popup, backdrop]);
  handles.push(handle);
  pointTarget = popup;
  return {
    viewport,
    popup,
    backdrop,
    content,
    fake,
    handle,
    update: (next) => handle.update(JSON.stringify({ ...defaults, ...options, ...next }), [popup, backdrop]),
  };
}

let stamp = 1000;

function pointer(type: 'pointerdown' | 'pointermove' | 'pointerup', target: Element, init: PointerEventInit & { at?: number } = {}): void {
  const { at, ...rest } = init;
  const event = new PointerEvent(type, {
    bubbles: true,
    cancelable: true,
    button: 0,
    buttons: type === 'pointerup' ? 0 : 1,
    pointerId: 1,
    pointerType: 'mouse',
    ...rest,
  });
  stamp = at ?? stamp + 100;
  Object.defineProperty(event, 'timeStamp', { value: stamp });
  target.dispatchEvent(event);
}

/** A slow mouse drag along the y axis: press, a baseline move, the drag, the release. */
function drag(target: Element, toY: number): void {
  pointer('pointerdown', target, { clientX: 10, clientY: 10 });
  pointer('pointermove', target, { clientX: 10, clientY: 10 });
  pointer('pointermove', target, { clientX: 10, clientY: 10 + toY });
  pointer('pointerup', target, { clientX: 10, clientY: 10 + toY });
}

describe('drawer module: the Viewport dismiss gesture (mouse and pen)', () => {
  it('port: a mouse drag on the popup moves it through the component band and marks the popup and backdrop as swiping', () => {
    const { popup, backdrop, fake } = mount();

    pointer('pointerdown', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 50 });

    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('40px');
    expect(popup.style.getPropertyValue('transition')).toBe('none');
    expect(popup.hasAttribute('data-swiping')).toBe(true);
    expect(backdrop.hasAttribute('data-swiping')).toBe(true);
    expect(fake.reported('OnSwipingChange')).toEqual([{ swiping: true }]);

    // The drag's declarations sit in the popup's component band: clearing it leaves nothing behind.
    styleWriter(popup).layer('component').clear();
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('');
  });

  it('port: a release past half the popup height requests the close with reason swipe and marks the swipe dismissal', () => {
    const { popup, backdrop, fake } = mount();

    drag(popup, 120);

    expect(fake.reported('OnSwipingChange')).toEqual([{ swiping: true }, { swiping: false }]);
    expect(popup.hasAttribute('data-swiping')).toBe(false);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);
    expect(backdrop.hasAttribute('data-swipe-dismiss')).toBe(true);
    expect(backdrop.style.getPropertyValue('--drawer-swipe-progress')).toBe('0');
    const [request] = fake.reported<{ request: number; event: { type: string } }>('OnDismissRequest');
    expect(request).toMatchObject({ request: 1, event: { type: 'pointerup' } });
  });

  it('port: a release under the threshold snaps back and requests nothing', () => {
    const { popup, fake } = mount();

    drag(popup, 90);

    expect(fake.reported('OnDismissRequest')).toEqual([]);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('0px');
  });

  it('port: a fast release dismisses before the threshold', () => {
    const { popup, fake } = mount();

    pointer('pointerdown', popup, { clientX: 10, clientY: 10, at: 5000 });
    pointer('pointermove', popup, { clientX: 10, clientY: 10, at: 5010 });
    pointer('pointermove', popup, { clientX: 10, clientY: 50, at: 5040 });
    pointer('pointerup', popup, { clientX: 10, clientY: 50, at: 5050 });

    // 40 px over 50 ms (the velocity floor) is 0.8 px/ms, past 0.5.
    expect(fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: a dismissal that C# answers while the drawer stays open rolls back', () => {
    const { popup, backdrop, fake, update } = mount();

    drag(popup, 120);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);

    // An update that does not answer the request yet changes nothing.
    update({ open: true, answeredDismiss: 0 });
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);

    update({ open: true, answeredDismiss: 1 });
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(backdrop.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('0px');
    expect(fake.reported('OnDismissRequest')).toHaveLength(1);
  });

  it('port: an accepted dismissal keeps the swipe dismissal marked', () => {
    const { popup, update } = mount();

    drag(popup, 120);
    update({ open: false, answeredDismiss: 1 });

    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);
  });

  it('port: on WebAssembly the dismissal asks the dismiss point, and a canceled one rolls back at once without a report', () => {
    const { popup, fake } = mount({}, true, 'stop');

    drag(popup, 120);

    expect(fake.decisions.map((decision) => decision.point)).toEqual(['dismiss']);
    expect(fake.decisions[0]!.event.type).toBe('pointerup');
    expect(fake.reported('OnDismissRequest')).toEqual([]);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('0px');
  });

  it('port: on WebAssembly an accepted dismissal is not reported again', () => {
    const { popup, fake } = mount({}, true, 'run');

    drag(popup, 120);

    expect(fake.decisions).toHaveLength(1);
    expect(fake.reported('OnDismissRequest')).toEqual([]);
    expect(popup.hasAttribute('data-swipe-dismiss')).toBe(true);
  });

  it('port: no gesture starts while closed, while a nested drawer is open, from Drawer.Content, from a swipe-ignore target, or from a touch pointer', () => {
    const closed = mount({ open: false });
    drag(closed.popup, 120);
    expect(closed.fake.reports).toEqual([]);

    const nested = mount({ nestedOpenDrawerCount: 1 });
    drag(nested.popup, 120);
    expect(nested.fake.reports).toEqual([]);

    const content = mount();
    pointTarget = content.content;
    drag(content.content, 120);
    expect(content.fake.reports).toEqual([]);

    const ignored = mount();
    const ignore = document.createElement('div');
    ignore.setAttribute('data-base-ui-swipe-ignore', 'y');
    ignored.popup.append(ignore);
    pointTarget = ignore;
    drag(ignore, 120);
    expect(ignored.fake.reports).toEqual([]);

    const touch = mount();
    pointer('pointerdown', touch.popup, { clientX: 10, clientY: 10, pointerType: 'touch' });
    pointer('pointermove', touch.popup, { clientX: 10, clientY: 130, pointerType: 'touch' });
    pointer('pointerup', touch.popup, { clientX: 10, clientY: 130, pointerType: 'touch' });
    expect(touch.fake.reports).toEqual([]);
  });

  // ADR 04 Amendments 2026-11-18, design point 2 as revised 2026-11-19 (the opt-in absorber, chosen by the user
  // 2026-11-19): C# renders the popup's `--nested-drawers` in the commit path, so a re-render rewrites the `style`
  // attribute; the module opts the popup's writer in, and the drag's declarations survive the rewrite.
  it('port: a C# re-render of the popup style during a drag keeps the drag declarations and takes the rendered ones', async () => {
    const { popup } = mount();

    pointer('pointerdown', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 10 });
    pointer('pointermove', popup, { clientX: 10, clientY: 50 });
    popup.setAttribute('style', '--nested-drawers: 1');
    await new Promise<void>((resolve) => setTimeout(resolve, 0));

    expect(popup.style.getPropertyValue('--nested-drawers')).toBe('1');
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('40px');
    expect(popup.style.getPropertyValue('transition')).toBe('none');
  });

  it('port: a mouse swipe start clears a selection inside the popup and keeps one outside it', () => {
    const inside = mount();
    const text = document.createTextNode('selectable');
    inside.content.append(text);
    const outside = document.createElement('p');
    outside.textContent = 'outside';
    document.body.append(outside);
    elements.push(outside);
    const selection = document.getSelection()!;

    selection.setBaseAndExtent(outside.firstChild!, 0, outside.firstChild!, 3);
    pointer('pointerdown', inside.popup, { clientX: 10, clientY: 10 });
    expect(selection.isCollapsed).toBe(false);
    pointer('pointerup', inside.popup, { clientX: 10, clientY: 10 });

    selection.setBaseAndExtent(text, 0, text, 4);
    pointer('pointerdown', inside.popup, { clientX: 10, clientY: 10 });
    expect(selection.rangeCount).toBe(0);
  });
});
