// L3 (ADR 06 §1): the `drawer` module's `swipe-area` role, M6 track 8b slice 4 (issue 279). The opening gesture
// (DRW-PTR-25…31), the open movement of DRW-ALG-04 on the Popup, Backdrop and Provider, the outside-press gate and its
// release guard (DRW-DIS-02). C# answers an open request by rendering the drawer open and sending the Popup and Backdrop in
// an update, which the tests do by hand.
import { afterEach, describe, expect, it } from 'vitest';
import { attach, drawerVisualState, type DrawerOptions } from './drawer';
import { attach as attachInteractions, getSession } from './interactions';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

const handles: TwinHandle[] = [];
const elements: HTMLElement[] = [];
let tokenCount = 0;

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  for (const element of elements.splice(0)) {
    element.remove();
  }
});

function element(style?: string): HTMLElement {
  const created = document.createElement('div');
  if (style) {
    created.setAttribute('style', style);
  }
  document.body.append(created);
  elements.push(created);
  return created;
}

let stamp = 10_000;

function pointer(type: 'pointerdown' | 'pointermove' | 'pointerup', target: Element, clientX: number, clientY: number, gap = 16): PointerEvent {
  const event = new PointerEvent(type, {
    bubbles: true,
    cancelable: true,
    button: 0,
    buttons: type === 'pointerup' ? 0 : 1,
    pointerId: 1,
    pointerType: 'mouse',
    clientX,
    clientY,
  });
  stamp += gap;
  Object.defineProperty(event, 'timeStamp', { value: stamp });
  target.dispatchEvent(event);
  return event;
}

interface Area {
  area: HTMLElement;
  popup: HTMLElement;
  backdrop: HTMLElement;
  fake: FakeTwin;
  handle: TwinHandle;
  /** C#'s answer: the drawer renders open, and the update hands over the Popup and Backdrop. */
  opened(next?: Partial<DrawerOptions>): void;
  update(next: Partial<DrawerOptions>): void;
  requests(): Array<{ open: boolean; event: { type: string } }>;
}

/** A closed drawer's swipe area opening it `left` (a drawer dismissed to the right), with a 100 by 100 px popup. */
function swipeArea(options: Partial<DrawerOptions> = {}, popupStyle?: string): Area {
  const area = element();
  const backdrop = element('--drawer-swipe-progress: 0');
  const popup = element(popupStyle);
  popup.id = `drawer-popup-${++tokenCount}`;
  Object.defineProperty(popup, 'offsetWidth', { value: 100, configurable: true });
  Object.defineProperty(popup, 'offsetHeight', { value: 100, configurable: true });
  const fake = createFakeTwin();
  let merged: DrawerOptions = {
    role: 'swipe-area',
    open: false,
    mounted: false,
    nested: false,
    nestedOpenDrawerCount: 0,
    swipeDirection: 'left',
    answeredDismiss: 0,
    popupId: popup.id,
    ...options,
  };
  const handle = attach(area, JSON.stringify(merged), fake.twin, []);
  handles.push(handle);
  const send = (next: Partial<DrawerOptions>, references: HTMLElement[]) => {
    merged = { ...merged, ...next };
    handle.update(JSON.stringify(merged), references);
  };
  return {
    area,
    popup,
    backdrop,
    fake,
    handle,
    opened: (next = {}) => send({ open: true, mounted: true, popup: 0, backdrop: 1, ...next }, [popup, backdrop]),
    update: (next) => send(next, merged.popup == null ? [] : [popup, backdrop]),
    requests: () => fake.reported('OnOpenRequest'),
  };
}

const movementX = (popup: HTMLElement) => Number.parseFloat(popup.style.getPropertyValue('--drawer-swipe-movement-x'));

describe('drawer module: the swipe area (DrawerSwipeArea.test.tsx)', () => {
  // DRW-ALG-04, PTR-29, PTR-31 (DrawerSwipeArea.test.tsx:383-459): 250 px of a 100 px popup overshoots by √150; the start
  // point shows progress 0; the release clears everything.
  it('synchronizes overshoot styles across the popup, backdrop, and provider indent', () => {
    const indent = element('--drawer-swipe-progress: 0');
    const provider = `swipe-area-provider-${++tokenCount}`;
    handles.push(attach(indent, JSON.stringify({ role: 'indent', provider }), createFakeTwin().twin));
    const { area, popup, backdrop, opened, requests } = swipeArea({ provider, frontmostHeight: 100 });

    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 150, 0);
    expect(requests().map((request) => request.open)).toEqual([true]);
    opened();
    pointer('pointermove', area, -50, 0);
    expect(movementX(popup)).toBeCloseTo(-Math.sqrt(150));
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-y')).toBe('0px');
    expect(popup.style.transition).toBe('none');
    expect(backdrop.hasAttribute('data-swiping')).toBe(true);
    expect(backdrop.style.getPropertyValue('--drawer-height')).toBe('100px');
    expect(indent.style.getPropertyValue('--drawer-swipe-progress')).toBe('1');
    expect(indent.style.getPropertyValue('--drawer-height')).toBe('100px');

    pointer('pointermove', area, 200, 0);
    expect(indent.style.getPropertyValue('--drawer-swipe-progress')).toBe('0');
    expect(indent.style.getPropertyValue('--drawer-height')).toBe('');

    pointer('pointermove', area, -50, 0);
    pointer('pointerup', area, -50, 0);
    expect(backdrop.hasAttribute('data-swiping')).toBe(false);
    expect(backdrop.style.getPropertyValue('--drawer-swipe-progress')).toBe('0');
    expect(indent.style.getPropertyValue('--drawer-swipe-progress')).toBe('0');
    expect(indent.style.getPropertyValue('--drawer-height')).toBe('');
    expect(popup.hasAttribute('data-swiping')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-x')).toBe('');
  });

  // DRW-ALG-04 (DrawerSwipeArea.test.tsx:461-502): the popup rests 60 px out, so 40 px of travel leaves 20 px.
  it('opens from the rendered transform distance in either horizontal direction', () => {
    const { area, popup, opened } = swipeArea({ swipeDirection: 'right' }, 'transform: matrix(1, 0, 0, 1, 60, 0)');

    pointer('pointerdown', area, 0, 10);
    pointer('pointermove', area, 10, 10);
    opened();
    pointer('pointermove', area, 40, 10);
    expect(movementX(popup)).toBeCloseTo(-20);
    pointer('pointerup', area, 40, 10);
  });

  // DRW-PTR-28, PTR-30: one open request per gesture, with the start event; a release past half the popup commits it.
  it('port: requests one open per gesture from the start event and keeps it on a committing release', () => {
    const { area, opened, requests } = swipeArea();

    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 199, 0);
    pointer('pointermove', area, 180, 0);
    expect(requests()).toHaveLength(1);
    expect(requests()[0]).toMatchObject({ open: true, event: { type: 'pointerdown' } });
    opened();
    pointer('pointermove', area, 120, 0);
    pointer('pointerup', area, 120, 0, 400);
    expect(requests()).toHaveLength(1);
  });

  // DRW-PTR-30: a release short of half the popup and slower than 0.1 px/ms closes the drawer this gesture opened.
  it('port: closes the drawer it opened when the release does not commit', () => {
    const { area, opened, requests } = swipeArea();

    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 190, 0, 200);
    opened();
    pointer('pointermove', area, 180, 0, 200);
    pointer('pointerup', area, 180, 0, 400);
    expect(requests().map((request) => request.open)).toEqual([true, false]);
  });

  // DRW-PTR-30 on Blazor Server: a release that does not commit before C# answers the gesture's open closes the drawer once
  // the answer says it opened, and asks for nothing when the open was rejected.
  it('port: a release before the open is answered closes the drawer only once it opened', () => {
    const accepted = swipeArea();
    pointer('pointerdown', accepted.area, 200, 0);
    pointer('pointermove', accepted.area, 190, 0, 200);
    pointer('pointermove', accepted.area, 180, 0, 200);
    pointer('pointerup', accepted.area, 180, 0, 400);
    expect(accepted.requests().map((request) => request.open)).toEqual([true]);
    accepted.opened({ answeredOpen: 1 });
    expect(accepted.requests().map((request) => request.open)).toEqual([true, false]);

    const rejected = swipeArea();
    pointer('pointerdown', rejected.area, 200, 0);
    pointer('pointermove', rejected.area, 190, 0, 200);
    pointer('pointermove', rejected.area, 180, 0, 200);
    pointer('pointerup', rejected.area, 180, 0, 400);
    rejected.update({ open: false, answeredOpen: 1 });
    expect(rejected.requests().map((request) => request.open)).toEqual([true]);
  });

  // DRW-PTR-28, EVT-01 (DrawerSwipeArea.test.tsx:539-575): a controlled owner that keeps the drawer closed sees no movement
  // and no close.
  it('port: applies no movement and requests no close when the open is rejected', () => {
    const { area, popup, backdrop, update, requests } = swipeArea();

    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 150, 0);
    update({ open: false, popup: 0, backdrop: 1 });
    pointer('pointermove', area, 100, 0);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-x')).toBe('');
    expect(backdrop.hasAttribute('data-swiping')).toBe(false);
    pointer('pointerup', area, 180, 0, 400);
    expect(requests().map((request) => request.open)).toEqual([true]);
  });

  // DRW-PTR-26, PTR-27: a disabled area takes no gesture; a mouse press is default-prevented on an enabled one.
  it('port: a disabled area takes no gesture, and an enabled one prevents a mouse press', () => {
    const { area, update, requests } = swipeArea({ disabled: true });
    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 100, 0);
    pointer('pointerup', area, 100, 0);
    expect(requests()).toEqual([]);

    update({ disabled: false });
    const press = pointer('pointerdown', area, 200, 0);
    expect(press.defaultPrevented).toBe(true);
    pointer('pointerup', area, 200, 0);
  });

  // DRW-PTR-31: the area turning disabled mid-gesture clears the movement and ends the gesture.
  it('port: clears the gesture when the area becomes disabled mid-swipe', () => {
    const { area, popup, backdrop, opened, update } = swipeArea();
    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 150, 0);
    opened();
    pointer('pointermove', area, 120, 0);
    expect(backdrop.hasAttribute('data-swiping')).toBe(true);

    update({ disabled: true });
    expect(backdrop.hasAttribute('data-swiping')).toBe(false);
    expect(popup.style.getPropertyValue('--drawer-swipe-movement-x')).toBe('');
    expect(area.hasAttribute('data-swiping')).toBe(false);
  });

  // DRW-DIS-02: the gate closes at the start; the release's own click keeps it closed; the next press opens it.
  it('port: closes the outside-press gate for the gesture and reopens it on the next press, not the release click', () => {
    const sessionToken = `swipe-area-session-${++tokenCount}`;
    handles.push(attachInteractions(null, JSON.stringify({ sessionToken, session: { open: false } }), createFakeTwin().twin));
    const session = getSession(sessionToken) as unknown as { outsidePressEnabled: boolean };
    const { area, opened } = swipeArea({ sessionToken });

    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 150, 0);
    expect(session.outsidePressEnabled).toBe(false);
    opened();
    pointer('pointermove', area, 100, 0);
    pointer('pointerup', area, 100, 0);
    expect(session.outsidePressEnabled).toBe(false);

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));
    expect(session.outsidePressEnabled).toBe(false);
    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 0 }));
    expect(session.outsidePressEnabled).toBe(true);
  });

  // DRW-PTR-29's first paint: once the drawer renders open, the popup the options name by id takes the movement before an
  // update hands it over (Blazor Server's answer is a round trip away).
  it('port: moves the popup as soon as it renders open, before the update that hands it over', async () => {
    const { area, popup } = swipeArea();
    popup.remove();

    pointer('pointerdown', area, 200, 0);
    pointer('pointermove', area, 150, 0);
    popup.setAttribute('data-open', '');
    document.body.append(popup);
    await Promise.resolve();
    expect(movementX(popup)).toBeCloseTo(50);
    expect(popup.hasAttribute('data-swiping')).toBe(true);
    pointer('pointerup', area, 150, 0);
  });
});

export { drawerVisualState };
