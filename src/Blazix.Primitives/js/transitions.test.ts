// L3 (ADR 06 §1): the `transitions` module in jsdom against a fake twin. Titles are Base UI's where a Base UI test
// observes the module in isolation (useAnimationsFinished.test.tsx, useTransitionStatus.test.tsx); the port's own
// titles cover the synchronous animations-disabled path (TRN-ANI-15, TRN-EDGE-01/02), the rows Base UI tests only
// through components, the until-found write, the reveal and the handle's idempotent attach and dispose.
/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  ENDING_STYLE,
  REVEAL_POINT,
  STARTING_STYLE,
  attach,
  createAnimationsWatcher,
  createTransitionMachine,
  evaluateTransition,
  getFiniteAnimations,
  requestFrame,
  resetFrameScheduler,
  type TransitionStatusLiteral,
  type TransitionsOptions,
} from './transitions';
import type { TwinHandle } from './twin';
import { createFakeTwin, type FakeTwin } from './test-support/fake-twin';

// --- harness -----------------------------------------------------------------------------------------------------

let frames: FrameRequestCallback[] = [];
const handles: TwinHandle[] = [];

beforeEach(() => {
  frames = [];
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    frames.push(callback);
    return frames.length;
  });
  globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
});

afterEach(() => {
  for (const handle of handles.splice(0)) {
    handle.dispose();
  }
  resetFrameScheduler();
  document.body.replaceChildren();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
});

/** Runs the native frame requests made so far (the scheduler's tick), as the browser's next frame would. */
function runFrame(): void {
  const pending = frames;
  frames = [];
  for (const callback of pending) {
    callback(0);
  }
}

/** Lets every queued microtask and the 0 ms timers run. */
function flushTasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function createAnimation(iterations = 1, duration = 1) {
  let resolveFinished!: () => void;
  let rejectFinished!: () => void;
  const finished = new Promise<void>((resolve, reject) => {
    resolveFinished = resolve;
    rejectFinished = reject;
  });
  // A canceled animation's rejection is handled by the watcher; keep vitest from reporting it when a test drops it.
  finished.catch(() => {});

  return {
    animation: {
      finished,
      effect: { getTiming: () => ({ duration, iterations }) },
      pending: false,
      playState: 'running',
    } as unknown as Animation,
    finish: resolveFinished,
    cancel: rejectFinished,
  };
}

function createElement(getAnimations?: () => Animation[]): HTMLElement {
  const element = document.createElement('div');
  if (getAnimations) {
    element.getAnimations = getAnimations;
  }
  document.body.append(element);
  return element;
}

const defaults: TransitionsOptions = {
  open: false,
  mounted: false,
  status: null,
  enableIdleState: false,
  deferEndingState: false,
  keepMounted: false,
  hiddenUntilFound: false,
  hiddenWhenUnmounted: false,
  suppressEndingStyle: false,
  reportOpenComplete: false,
  revealOnBeforeMatch: false,
  unmountRequests: 0,
  externalExit: false,
  openSelector: null,
};

function attachTo(element: HTMLElement, fake: FakeTwin, options: Partial<TransitionsOptions> = {}): TwinHandle {
  const handle = attach(element, JSON.stringify({ ...defaults, ...options }), fake.twin);
  handles.push(handle);
  return handle;
}

function update(handle: TwinHandle, options: Partial<TransitionsOptions>): void {
  handle.update(JSON.stringify({ ...defaults, ...options }));
}

// --- the watcher -------------------------------------------------------------------------------------------------

describe('useAnimationsFinished', () => {
  beforeEach(() => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
  });

  it.each(['finish', 'cancel'] as const)('ignores infinite animations when a finite animation completes via %s', async (completion) => {
    const finite = createAnimation();
    const infinite = createAnimation(Infinity);
    const onFinished = vi.fn();
    let animations = [finite.animation, infinite.animation];
    const getAnimations = vi.fn(() => animations);

    createAnimationsWatcher(createElement(getAnimations)).run(onFinished);
    runFrame();
    expect(getAnimations).toHaveBeenCalled();
    expect(onFinished).not.toHaveBeenCalled();

    animations = [infinite.animation];
    finite[completion]();
    await flushTasks();

    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['iteration count', Infinity, 1],
    ['duration', 1, Infinity],
  ] as const)('finishes when the element only has an animation with an infinite %s', async (_timing, iterations, duration) => {
    const infinite = createAnimation(iterations, duration);
    const onFinished = vi.fn();

    createAnimationsWatcher(createElement(() => [infinite.animation])).run(onFinished);
    runFrame();
    await flushTasks();

    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('waits for a replacement animation after an animation is canceled', async () => {
    const initialAnimation = createAnimation();
    const replacementAnimation = createAnimation();
    const infiniteAnimation = createAnimation(Infinity);
    const onFinished = vi.fn();
    let animations: Animation[] = [initialAnimation.animation, infiniteAnimation.animation];
    let getAnimationsCallCount = 0;

    createAnimationsWatcher(
      createElement(() => {
        getAnimationsCallCount += 1;
        return animations;
      }),
    ).run(onFinished);
    runFrame();
    expect(getAnimationsCallCount).toBeGreaterThan(0);

    animations = [replacementAnimation.animation, infiniteAnimation.animation];
    initialAnimation.cancel();
    await flushTasks();
    expect(onFinished).not.toHaveBeenCalled();

    animations = [];
    replacementAnimation.finish();
    await flushTasks();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('finishes when a canceled animation has no replacement', async () => {
    const initialAnimation = createAnimation();
    const onFinished = vi.fn();
    let animations: Animation[] = [initialAnimation.animation];
    let getAnimationsCallCount = 0;

    createAnimationsWatcher(
      createElement(() => {
        getAnimationsCallCount += 1;
        return animations;
      }),
    ).run(onFinished);
    runFrame();
    expect(getAnimationsCallCount).toBeGreaterThan(0);

    animations = [];
    initialAnimation.cancel();
    await flushTasks();

    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('batches opted-in callbacks that finish in the same microtask into a single commit', async () => {
    const first = createAnimation();
    const second = createAnimation();
    const queueMicrotask = globalThis.queueMicrotask;
    let flushing = false;
    const flushes = vi.spyOn(globalThis, 'queueMicrotask').mockImplementation((task) =>
      queueMicrotask(() => {
        flushing = true;
        try {
          task();
        } finally {
          flushing = false;
        }
      }),
    );
    const ranInFlush: boolean[] = [];

    createAnimationsWatcher(createElement(() => [first.animation]), false, true).run(() => ranInFlush.push(flushing));
    createAnimationsWatcher(createElement(() => [second.animation]), false, true).run(() => ranInFlush.push(flushing));
    runFrame();

    first.finish();
    second.finish();
    await flushTasks();

    expect(ranInFlush).toEqual([true, true]);
    expect(flushes).toHaveBeenCalledTimes(1);
  });

  it('skips a callback whose signal aborts while the batch is flushing', async () => {
    const first = createAnimation();
    const second = createAnimation();
    const secondController = new AbortController();
    const onFirstFinished = vi.fn(() => secondController.abort());
    const onSecondFinished = vi.fn();

    createAnimationsWatcher(createElement(() => [first.animation]), false, true).run(onFirstFinished);
    createAnimationsWatcher(createElement(() => [second.animation]), false, true).run(onSecondFinished, secondController.signal);
    runFrame();

    first.finish();
    second.finish();
    await flushTasks();

    expect(onFirstFinished).toHaveBeenCalledTimes(1);
    expect(onSecondFinished).not.toHaveBeenCalled();
  });

  it('commits each callback separately by default so later callbacks observe earlier updates', async () => {
    const first = createAnimation();
    const second = createAnimation();
    const onSecondUnmount = vi.fn();

    // Mirrors the completion watcher: each wait is aborted when its popup's open state flips, and the completion
    // re-reads the latest open state. The first popup's close completion reopens the second.
    function popup(animation: Animation, onCloseComplete: () => void) {
      const state = { open: false, controller: new AbortController() };
      const watcher = createAnimationsWatcher(createElement(() => [animation]));
      const arm = () => watcher.run(() => !state.open && onCloseComplete(), state.controller.signal);
      arm();
      return {
        reopen() {
          state.open = true;
          state.controller.abort();
          state.controller = new AbortController();
          arm();
        },
      };
    }

    let second$: { reopen(): void } | undefined;
    popup(first.animation, () => second$?.reopen());
    second$ = popup(second.animation, onSecondUnmount);
    runFrame();

    first.finish();
    second.finish();
    await flushTasks();

    expect(onSecondUnmount).not.toHaveBeenCalled();
  });

  it('inspects the animations on the next frame after the wait is armed', () => {
    const getAnimations = vi.fn((): Animation[] => []);

    createAnimationsWatcher(createElement(getAnimations)).run(() => {});
    expect(getAnimations).not.toHaveBeenCalled();

    runFrame();
    expect(getAnimations).toHaveBeenCalledTimes(1);
  });

  it('waits for data-starting-style to be removed before the first inspection, and its abort disconnects the observer', async () => {
    const getAnimations = vi.fn((): Animation[] => []);
    const element = createElement(getAnimations);
    element.setAttribute(STARTING_STYLE, '');
    const onFinished = vi.fn();

    createAnimationsWatcher(element, true).run(onFinished);
    runFrame();
    await flushTasks();
    expect(getAnimations).not.toHaveBeenCalled();

    element.removeAttribute(STARTING_STYLE);
    await flushTasks();
    expect(getAnimations).toHaveBeenCalledTimes(1);
    expect(onFinished).toHaveBeenCalledTimes(1);

    const aborted = createElement(getAnimations);
    aborted.setAttribute(STARTING_STYLE, '');
    const controller = new AbortController();
    const onAborted = vi.fn();
    createAnimationsWatcher(aborted, true).run(onAborted, controller.signal);
    controller.abort();
    aborted.removeAttribute(STARTING_STYLE);
    await flushTasks();
    expect(getAnimations).toHaveBeenCalledTimes(1);
    expect(onAborted).not.toHaveBeenCalled();
  });

  it('inspects on the next frame when waiting for a starting style that is not there', async () => {
    const getAnimations = vi.fn((): Animation[] => []);
    const onFinished = vi.fn();

    createAnimationsWatcher(createElement(getAnimations), true).run(onFinished);
    expect(getAnimations).not.toHaveBeenCalled();

    runFrame();
    await flushTasks();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('a new wait cancels a pending frame-deferred inspection', async () => {
    const watcher = createAnimationsWatcher(createElement(() => []));
    const first = vi.fn();
    const second = vi.fn();
    const third = vi.fn();
    const fourth = vi.fn();

    watcher.run(first);
    watcher.run(second);
    runFrame();
    await flushTasks();

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);

    // Also when the new wait completes at once and requests no frame.
    watcher.run(third);
    globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
    watcher.run(fourth);
    runFrame();
    await flushTasks();

    expect(third).not.toHaveBeenCalled();
    expect(fourth).toHaveBeenCalledTimes(1);
  });

  it('never runs the callback when the element is missing', async () => {
    const onFinished = vi.fn();

    createAnimationsWatcher(null).run(onFinished);
    createAnimationsWatcher(() => null).run(onFinished);
    runFrame();
    await flushTasks();

    expect(onFinished).not.toHaveBeenCalled();
    expect(frames).toHaveLength(0);
  });

  it('an aborted wait does not run its callback or restart after a canceled animation', async () => {
    const initial = createAnimation();
    const replacement = createAnimation();
    let animations = [initial.animation];
    const getAnimations = vi.fn(() => animations);
    const controller = new AbortController();
    const onFinished = vi.fn();

    createAnimationsWatcher(createElement(getAnimations)).run(onFinished, controller.signal);
    runFrame();
    const inspections = getAnimations.mock.calls.length;

    controller.abort();
    animations = [replacement.animation];
    initial.cancel();
    replacement.finish();
    await flushTasks();

    expect(getAnimations).toHaveBeenCalledTimes(inspections);
    expect(onFinished).not.toHaveBeenCalled();
  });

  it('filters out infinite animations', () => {
    const finite = createAnimation();
    const loop = createAnimation(Infinity);
    const spinner = createAnimation(1, Infinity);
    const element = createElement(() => [finite.animation, loop.animation, spinner.animation]);

    expect(getFiniteAnimations(element)).toEqual([finite.animation]);
  });
});

describe('the synchronous path', () => {
  it('runs the callback synchronously, with no frame and no microtask, when animations are disabled', () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
    const getAnimations = vi.fn((): Animation[] => []);
    const onFinished = vi.fn();

    createAnimationsWatcher(createElement(getAnimations), true).run(onFinished);

    expect(onFinished).toHaveBeenCalledTimes(1);
    expect(getAnimations).not.toHaveBeenCalled();
    expect(frames).toHaveLength(0);
  });

  it('runs the callback synchronously when the element has no getAnimations', () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const element = createElement();
    Object.defineProperty(element, 'getAnimations', { value: undefined });
    const onFinished = vi.fn();

    createAnimationsWatcher(element).run(onFinished);

    expect(onFinished).toHaveBeenCalledTimes(1);
    expect(frames).toHaveLength(0);
  });

  it('reads the animations-disabled switch at each wait', () => {
    const element = createElement(() => []);
    const watcher = createAnimationsWatcher(element);
    const onFinished = vi.fn();

    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    watcher.run(onFinished);
    expect(onFinished).not.toHaveBeenCalled();

    globalThis.BASE_UI_ANIMATIONS_DISABLED = true;
    watcher.run(onFinished);
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('completes a close in the closing pass, so data-ending-style is present for one commit only', () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, { open: true, mounted: true, keepMounted: true, hiddenWhenUnmounted: true });
    const seen: boolean[] = [];
    new MutationObserver(() => seen.push(element.hasAttribute(ENDING_STYLE))).observe(element, { attributes: true });

    update(handle, { open: false, keepMounted: true, hiddenWhenUnmounted: true });

    expect(element.hasAttribute(ENDING_STYLE)).toBe(false);
    expect(element.hasAttribute('hidden')).toBe(true);
    expect(fake.reports.map((r) => r.method)).toEqual(['OnStatusChange', 'OnTransitionComplete']);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: 'ending', mounted: true }]);
    expect(fake.reported('OnTransitionComplete')).toEqual([{ open: false }]);
  });
});

// --- the machine -------------------------------------------------------------------------------------------------

describe('useTransitionStatus', () => {
  const machine = (open: boolean, enableIdleState = false, deferEndingState = false) =>
    createTransitionMachine(
      { open, mounted: open, status: open && enableIdleState ? 'idle' : undefined },
      { enableIdleState, deferEndingState },
      () => {},
    );

  it('does not request a frame for an element that mounts already open', () => {
    const status = machine(true);

    expect(status.status).toBe(undefined);
    expect(frames).toHaveLength(0);
  });

  it('clears the starting status on the next frame after opening', () => {
    const status = machine(false);

    status.setOpen(true);
    expect(status.status).toBe('starting');
    expect(frames).toHaveLength(1);

    runFrame();
    expect(status.status).toBe(undefined);
  });

  it('clears a stale ending status when reopened before the exit settles', () => {
    const status = machine(true);
    status.setOpen(false);
    expect(status.status).toBe('ending');

    status.setOpen(true);
    expect(frames).toHaveLength(1);

    runFrame();
    expect(status.status).toBe(undefined);
  });

  it('two machines that open in the same task clear their starting status in the same frame', () => {
    const first = machine(false);
    const second = machine(false);

    first.setOpen(true);
    second.setOpen(true);
    expect(frames).toHaveLength(1);

    runFrame();
    expect([first.status, second.status]).toEqual([undefined, undefined]);
  });

  it('cancels the pending clearing frame when it closes during the starting phase', () => {
    const status = machine(false);
    status.setOpen(true);

    status.setOpen(false);
    expect(status.status).toBe('ending');

    runFrame();
    expect(status.status).toBe('ending');
  });

  it('clears the status when the mounted flag is cleared during the ending phase', () => {
    const status = machine(true);
    status.setOpen(false);

    status.setMounted(false);

    expect(status.status).toBe(undefined);
    expect(status.mounted).toBe(false);
  });

  it('with the idle state, settles from starting to idle on the next frame', () => {
    const status = machine(false, true);
    status.setOpen(true);
    expect(status.status).toBe('starting');

    runFrame();
    expect(status.status).toBe('idle');
  });

  it('with the idle state, a reopen during the exit passes through starting before it settles', () => {
    const status = machine(true, true);
    status.setOpen(false);
    expect(status.status).toBe('ending');

    status.setOpen(true);
    expect(status.status).toBe('starting');

    runFrame();
    expect(status.status).toBe('idle');
  });

  it('with a deferred ending, applies ending on the next frame and drops it when reopened first', () => {
    const status = machine(true, true, true);
    status.setOpen(false);
    expect(status.status).toBe('idle');

    runFrame();
    expect(status.status).toBe('ending');

    const reopened = machine(true, true, true);
    reopened.setOpen(false);
    reopened.setOpen(true);
    runFrame();
    expect(reopened.status).toBe('idle');
  });

  it('with the idle state, an element that mounts open requests one frame that only rewrites idle', () => {
    const changes: unknown[] = [];
    const status = createTransitionMachine({ open: true, mounted: true, status: 'idle' }, { enableIdleState: true, deferEndingState: false }, (state) =>
      changes.push(state.status),
    );

    expect(frames).toHaveLength(1);
    runFrame();

    expect(status.status).toBe('idle');
    expect(changes).toEqual([]);
  });
});

describe('the evaluation step', () => {
  interface EvaluationCase {
    name: string;
    open: boolean;
    mounted: boolean;
    status: TransitionStatusLiteral;
    deferEndingState: boolean;
    expected: { mounted: boolean; status: TransitionStatusLiteral };
  }

  // The table the L1 test of the C# rules reads too: the evaluation step exists in both languages. (jsdom's `URL`
  // does not resolve file URLs, so the path is joined from the module's own path.)
  const casesPath = join(dirname(fileURLToPath(import.meta.url)), 'test-support', 'transition-evaluation.cases.json');
  const { cases } = JSON.parse(readFileSync(casesPath, 'utf8')) as {
    cases: EvaluationCase[];
  };

  it.each(cases.map((c) => [c.name, c] as const))('%s', (_name, c) => {
    const result = evaluateTransition({ open: c.open, mounted: c.mounted, status: c.status ?? undefined }, c.deferEndingState);

    expect({ mounted: result.mounted, status: result.status ?? null }).toEqual(c.expected);
  });
});

// --- the twin-facing entry ---------------------------------------------------------------------------------------

describe('attach', () => {
  it('writes data-starting-style when it opens and removes it on the next frame, reporting each change once', () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, { keepMounted: true, hiddenWhenUnmounted: true });

    update(handle, { open: true, keepMounted: true, hiddenWhenUnmounted: true });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
    expect(frames).toHaveLength(1);

    update(handle, { open: true, keepMounted: true, hiddenWhenUnmounted: true });
    runFrame();

    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    expect(fake.reported('OnStatusChange')).toEqual([
      { status: 'starting', mounted: true },
      { status: null, mounted: true },
    ]);
  });

  it('starts from the status C# rendered and clears it on the next frame', () => {
    const fake = createFakeTwin();
    const element = createElement();
    element.setAttribute(STARTING_STYLE, '');

    attachTo(element, fake, { open: true, mounted: true, status: 'starting' });
    expect(fake.reports).toEqual([]);

    runFrame();
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: null, mounted: true }]);
  });

  // The C# render and the attach can land before the frame that first styles the element (Server on a fast circuit):
  // the element's style is computed with the starting style before the clearing frame, so a transition runs from it.
  it('computes the style of an element that shows data-starting-style before the frame that clears it', () => {
    const fake = createFakeTwin();
    const rendered = createElement();
    rendered.setAttribute(STARTING_STYLE, '');
    const opened = createElement();
    const computed = vi.spyOn(window, 'getComputedStyle');

    try {
      attachTo(rendered, fake, { open: true, mounted: true, status: 'starting' });
      const handle = attachTo(opened, fake, { keepMounted: true });
      update(handle, { open: true, keepMounted: true });

      expect(computed.mock.calls.map(([element]) => element)).toEqual([rendered, opened]);
      expect(frames).toHaveLength(1);
    } finally {
      computed.mockRestore();
    }
  });

  it('does not request a frame or write a transition attribute for an element attached open', () => {
    const fake = createFakeTwin();
    const element = createElement();

    attachTo(element, fake, { open: true, mounted: true });

    expect(frames).toHaveLength(0);
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    expect(element.hasAttribute(ENDING_STYLE)).toBe(false);
    expect(fake.reports).toEqual([]);
  });

  it('writes data-ending-style in the closing pass, and one frame later with a deferred ending', () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const fake = createFakeTwin();
    const immediate = createElement(() => []);
    const handle = attachTo(immediate, fake, { open: true, mounted: true });
    update(handle, { open: false });
    expect(immediate.hasAttribute(ENDING_STYLE)).toBe(true);

    const deferredFake = createFakeTwin();
    const deferred = createElement(() => []);
    const deferredHandle = attachTo(deferred, deferredFake, { open: true, mounted: true, status: 'idle', enableIdleState: true, deferEndingState: true });
    update(deferredHandle, { open: false, enableIdleState: true, deferEndingState: true });
    expect(deferred.hasAttribute(ENDING_STYLE)).toBe(false);

    runFrame();
    expect(deferred.hasAttribute(ENDING_STYLE)).toBe(true);
    expect(deferredFake.reported('OnStatusChange')).toEqual([{ status: 'ending', mounted: true }]);
  });

  it('waits for the exit animations, then hides the element and reports the completion once', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const exit = createAnimation();
    const fake = createFakeTwin();
    const element = createElement(() => [exit.animation]);
    const handle = attachTo(element, fake, { open: true, mounted: true, keepMounted: true, hiddenWhenUnmounted: true });

    update(handle, { open: false, keepMounted: true, hiddenWhenUnmounted: true });
    runFrame();
    await flushTasks();
    expect(element.hasAttribute(ENDING_STYLE)).toBe(true);
    expect(fake.reported('OnTransitionComplete')).toEqual([]);

    exit.finish();
    await flushTasks();

    expect(element.hasAttribute(ENDING_STYLE)).toBe(false);
    expect(element.getAttribute('hidden')).toBe('');
    expect(fake.reported('OnTransitionComplete')).toEqual([{ open: false }]);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: 'ending', mounted: true }]);
  });

  it('a reopen before the exit finishes aborts the close completion', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const exit = createAnimation();
    const fake = createFakeTwin();
    const element = createElement(() => [exit.animation]);
    const handle = attachTo(element, fake, { open: true, mounted: true });

    update(handle, { open: false });
    runFrame();
    update(handle, { open: true });
    exit.finish();
    await flushTasks();
    runFrame();

    expect(fake.reported('OnTransitionComplete')).toEqual([]);
    expect(element.hasAttribute('hidden')).toBe(false);
    expect(element.hasAttribute(ENDING_STYLE)).toBe(false);
  });

  it('a reopen aborts the close wait, so an earlier exit that finishes later does not end the next close', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const firstExit = createAnimation();
    const secondExit = createAnimation();
    let animations = [firstExit.animation];
    const fake = createFakeTwin();
    const element = createElement(() => animations);
    const handle = attachTo(element, fake, { open: true, mounted: true });

    update(handle, { open: false });
    runFrame();
    update(handle, { open: true });
    animations = [secondExit.animation];
    update(handle, { open: false });
    runFrame();

    firstExit.finish();
    await flushTasks();
    expect(fake.reported('OnTransitionComplete')).toEqual([]);

    secondExit.finish();
    await flushTasks();
    expect(fake.reported('OnTransitionComplete')).toEqual([{ open: false }]);
  });

  it('removes the hidden it wrote at exit end when the element opens again', () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, { open: true, mounted: true });

    update(handle, { open: false });
    expect(element.getAttribute('hidden')).toBe('');

    update(handle, { open: true });
    expect(element.hasAttribute('hidden')).toBe(false);
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
  });

  it('does not hide an indicator-style kept element and never writes data-ending-style when the ending style is suppressed', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const exit = createAnimation();
    const fake = createFakeTwin();
    const element = createElement(() => [exit.animation]);
    const handle = attachTo(element, fake, { open: true, mounted: true, keepMounted: true, suppressEndingStyle: true });

    update(handle, { open: false, keepMounted: true, suppressEndingStyle: true });
    runFrame();
    expect(element.hasAttribute(ENDING_STYLE)).toBe(false);

    exit.finish();
    await flushTasks();

    expect(element.hasAttribute('hidden')).toBe(false);
    expect(fake.reported('OnStatusChange')).toEqual([]);
    expect(fake.reported('OnTransitionComplete')).toEqual([{ open: false }]);
  });

  it('never inspects the animations of an open element unless the open completion is asked for', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const getAnimations = vi.fn((): Animation[] => []);
    const fake = createFakeTwin();
    const handle = attachTo(createElement(getAnimations), fake);

    update(handle, { open: true });
    runFrame();
    await flushTasks();

    expect(getAnimations).not.toHaveBeenCalled();
    expect(fake.reported('OnTransitionComplete')).toEqual([]);
  });

  it('reports the open completion once the enter animations finish, when asked', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const enter = createAnimation();
    const fake = createFakeTwin();
    const element = createElement(() => [enter.animation]);
    const handle = attachTo(element, fake, { reportOpenComplete: true });

    update(handle, { open: true, reportOpenComplete: true });
    runFrame();
    await flushTasks();
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    expect(fake.reported('OnTransitionComplete')).toEqual([]);

    enter.finish();
    await flushTasks();
    expect(fake.reported('OnTransitionComplete')).toEqual([{ open: true }]);
  });

  it('an unmount request from C# clears the status while closed and is dropped after a reopen', () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const fake = createFakeTwin();
    const element = createElement(() => []);
    const handle = attachTo(element, fake, { open: true, mounted: true, keepMounted: true });

    update(handle, { open: false, keepMounted: true });
    update(handle, { open: false, keepMounted: true, unmountRequests: 1 });
    expect(element.hasAttribute(ENDING_STYLE)).toBe(false);
    expect(fake.reported('OnStatusChange')).toEqual([
      { status: 'ending', mounted: true },
      { status: null, mounted: false },
    ]);

    update(handle, { open: true, keepMounted: true, unmountRequests: 1 });
    runFrame();
    const reports = fake.reports.length;
    update(handle, { open: true, keepMounted: true, unmountRequests: 2 });

    expect(fake.reports).toHaveLength(reports);
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
  });

  it('an external exit waits for an unmount request instead of the element\'s own animations', async () => {
    // The Collapsible Root and Trigger: their 'ending' lasts until the panel's exit ends (COL-STA-05, COL-DOM-03/06).
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const fake = createFakeTwin();
    const element = createElement(() => []);
    const options = { enableIdleState: true, deferEndingState: true, keepMounted: true, externalExit: true };
    const handle = attachTo(element, fake, { ...options, open: true, mounted: true, status: 'idle' });

    update(handle, { ...options, open: false });
    runFrame();
    expect(element.hasAttribute(ENDING_STYLE)).toBe(true);
    runFrame();
    runFrame();
    await flushTasks();
    runFrame();
    expect(element.hasAttribute(ENDING_STYLE)).toBe(true);

    update(handle, { ...options, open: false, unmountRequests: 1 });
    expect(element.hasAttribute(ENDING_STYLE)).toBe(false);
    expect(fake.reported('OnStatusChange')).toEqual([
      { status: 'ending', mounted: true },
      { status: null, mounted: false },
    ]);
  });

  it('applies the consumer style only when the part hands it over', () => {
    const fake = createFakeTwin();
    const kept = createElement();
    kept.style.color = 'red';
    attachTo(kept, fake);
    expect(kept.style.color).toBe('red');

    const owned = createElement();
    owned.style.color = 'red';
    const handle = attachTo(owned, fake, { style: 'margin: 1px' });
    expect(owned.getAttribute('style')).toBe('margin: 1px;');

    update(handle, { style: 'margin: 2px' });
    expect(owned.getAttribute('style')).toBe('margin: 2px;');
  });

  it('a repeated attach of the same element returns the live handle, and dispose is idempotent', () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake);

    const again = attach(element, JSON.stringify({ ...defaults, open: true }), createFakeTwin().twin);
    expect(again).toBe(handle);
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);

    handle.dispose();
    handle.dispose();
    update(handle, { open: true });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    expect(fake.reports).toEqual([]);

    const fresh = attachTo(element, fake);
    expect(fresh).not.toBe(handle);
  });

  it('dispose cancels the pending frames and the close wait', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const exit = createAnimation();
    const fake = createFakeTwin();
    const element = createElement(() => [exit.animation]);
    const handle = attachTo(element, fake, { open: true, mounted: true });
    update(handle, { open: false });

    handle.dispose();
    runFrame();
    exit.finish();
    await flushTasks();

    expect(fake.reported('OnTransitionComplete')).toEqual([]);
  });
});

describe('the open input from the applied batch', () => {
  const panel: Partial<TransitionsOptions> = { keepMounted: true, hiddenWhenUnmounted: true, openSelector: '[data-open]' };

  // What Blazor's render batch does to the element: it applies the attributes C# renders, synchronously.
  function renderOpen(element: HTMLElement, open: boolean) {
    element.toggleAttribute('data-open', open);
    element.toggleAttribute('data-closed', !open);
    element.toggleAttribute('hidden', false);
  }

  it('writes data-starting-style in the microtask after the batch that opens the part, before any frame', async () => {
    const fake = createFakeTwin();
    const element = createElement();
    element.setAttribute('data-closed', '');
    element.setAttribute('hidden', '');
    attachTo(element, fake, panel);

    renderOpen(element, true);
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    await Promise.resolve();

    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: 'starting', mounted: true }]);
    expect(frames).toHaveLength(1);

    runFrame();
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
  });

  it('writes data-ending-style in the microtask after the batch that closes the part', async () => {
    globalThis.BASE_UI_ANIMATIONS_DISABLED = false;
    const fake = createFakeTwin();
    const element = createElement(() => []);
    element.setAttribute('data-open', '');
    attachTo(element, fake, { ...panel, open: true, mounted: true });

    renderOpen(element, false);
    await Promise.resolve();

    expect(element.hasAttribute(ENDING_STYLE)).toBe(true);
  });

  it('takes the applied DOM over the open of a late update', async () => {
    const fake = createFakeTwin();
    const element = createElement();
    element.setAttribute('data-closed', '');
    const handle = attachTo(element, fake, panel);

    renderOpen(element, true);
    await Promise.resolve();
    update(handle, { ...panel, open: false });

    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: 'starting', mounted: true }]);
  });

  it('reads the selector on attach, when a batch opened the part before the attach arrived', () => {
    const fake = createFakeTwin();
    const element = createElement();
    element.setAttribute('data-open', '');

    attachTo(element, fake, { ...panel, open: false, mounted: false });

    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: 'starting', mounted: true }]);
  });

  it('accepts a selector that matches the absence of an attribute', async () => {
    const fake = createFakeTwin();
    const element = createElement();
    element.setAttribute('aria-hidden', 'true');
    attachTo(element, fake, { keepMounted: true, suppressEndingStyle: true, openSelector: ':not([aria-hidden])' });

    element.removeAttribute('aria-hidden');
    await Promise.resolve();

    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
  });

  it('without a selector the open input is the update', async () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, { keepMounted: true });

    renderOpen(element, true);
    await Promise.resolve();
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);

    update(handle, { keepMounted: true, open: true });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
  });

  it('dispose stops reading the batch', async () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, panel);

    handle.dispose();
    renderOpen(element, true);
    await Promise.resolve();

    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
  });
});

describe('hidden until found', () => {
  const untilFound: Partial<TransitionsOptions> = { keepMounted: true, hiddenUntilFound: true, revealOnBeforeMatch: true };

  it('writes hidden="until-found" while hidden, and no hidden attribute while open', () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);
    expect(element.getAttribute('hidden')).toBe('until-found');

    update(handle, { ...untilFound, open: true });
    expect(element.hasAttribute('hidden')).toBe(false);

    update(handle, { ...untilFound, open: false });
    expect(element.getAttribute('hidden')).toBe('until-found');
  });

  it('rewrites a boolean hidden that C# renders to the literal before paint', async () => {
    const fake = createFakeTwin();
    const element = createElement();
    attachTo(element, fake, untilFound);

    element.setAttribute('hidden', '');
    await Promise.resolve();

    expect(element.getAttribute('hidden')).toBe('until-found');
  });

  it('keeps data-starting-style on a hidden panel and drops it while open', () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);

    update(handle, { ...untilFound, open: true });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
    runFrame();
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
  });

  it('does not restore hidden after the browser removed it, until the revert', () => {
    const fake = createFakeTwin();
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    update(handle, untilFound);

    expect(element.hasAttribute('hidden')).toBe(false);
  });

  it('reports the reveal request with the event and drops data-starting-style inside the dispatch', () => {
    const fake = createFakeTwin();
    const element = createElement();
    attachTo(element, fake, untilFound);
    let insideDispatch: boolean | undefined;
    element.addEventListener('beforematch', () => {
      insideDispatch = element.hasAttribute(STARTING_STYLE);
    });

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));

    expect(insideDispatch).toBe(false);
    expect(fake.reported<{ event: { type: string; kind: string } }>('OnRevealRequest')).toMatchObject([{ event: { type: 'beforematch', kind: 'Event' } }]);
  });

  /** A Server-like fake twin whose `OnRevealRequest` invocation resolves when the test answers it. */
  function serverTwin() {
    const fake = createFakeTwin();
    const answers: ((open: boolean) => void)[] = [];
    const invokeMethodAsync = fake.twin.invokeMethodAsync.bind(fake.twin);
    fake.twin.invokeMethodAsync = <T>(method: string, ...args: unknown[]): Promise<T> => {
      const recorded = invokeMethodAsync<T>(method, ...args);
      if (method !== 'OnRevealRequest') {
        return recorded;
      }
      return new Promise<T>((resolve) => answers.push((open) => resolve(open as T)));
    };
    return { fake, answer: (open: boolean) => answers.shift()?.(open) };
  }

  it('on Server, re-collapses the panel one frame after C# answers that the request did not open it', async () => {
    const { fake, answer } = serverTwin();
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));
    expect(fake.reported('OnRevealRequest')).toHaveLength(1);

    // A 0 ms task and a frame pass, as they would during the round trip: the panel stays revealed.
    await flushTasks();
    runFrame();
    expect(element.hasAttribute('hidden')).toBe(false);
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);

    answer(false);
    await flushTasks();
    expect(element.hasAttribute('hidden')).toBe(false);
    runFrame();

    expect(element.getAttribute('hidden')).toBe('until-found');
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);

    // The skip was disarmed: the next ordinary open shows its starting style.
    update(handle, { ...untilFound, open: true });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
  });

  it('on Server, keeps the panel revealed when C# answers that it is open, before its open update arrives', async () => {
    const { fake, answer } = serverTwin();
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));
    answer(true);
    await flushTasks();
    runFrame();
    expect(element.hasAttribute('hidden')).toBe(false);

    update(handle, { ...untilFound, open: true });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: 'idle', mounted: true }]);
  });

  it('on Server, an open that arrives before the answer keeps the panel revealed', async () => {
    const { fake, answer } = serverTwin();
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));
    update(handle, { ...untilFound, open: true });
    answer(false);
    await flushTasks();
    runFrame();

    expect(element.hasAttribute('hidden')).toBe(false);
  });

  it('on Server, teardown drops the revert of a pending answer', async () => {
    const { fake, answer } = serverTwin();
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));
    handle.dispose();
    answer(false);
    await flushTasks();
    runFrame();

    expect(element.hasAttribute('hidden')).toBe(false);
  });

  it('re-collapses the panel after a 0 ms task and a frame when the open never arrives', async () => {
    const fake = createFakeTwin({ synchronous: true, verdict: 'run' });
    const element = createElement();
    attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));
    expect(fake.decisions.map((d) => d.point)).toEqual([REVEAL_POINT]);
    expect(fake.reported('OnRevealRequest')).toEqual([]);

    await flushTasks();
    expect(element.hasAttribute('hidden')).toBe(false);
    runFrame();

    expect(element.getAttribute('hidden')).toBe('until-found');
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
  });

  it('a canceled reveal keeps data-starting-style and restores hidden="until-found"', async () => {
    const fake = createFakeTwin({ synchronous: true, verdict: 'stop' });
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);
    let insideDispatch: boolean | undefined;
    element.addEventListener('beforematch', () => {
      insideDispatch = element.hasAttribute(STARTING_STYLE);
    });

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));
    expect(insideDispatch).toBe(true);

    await flushTasks();
    runFrame();
    expect(element.getAttribute('hidden')).toBe('until-found');

    // The skip was never armed: the next ordinary open shows its starting style.
    update(handle, { ...untilFound, open: true });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
  });

  it('an accepted reveal opens the panel without data-starting-style, and the skip is consumed by that one open', async () => {
    const fake = createFakeTwin({ synchronous: true, verdict: 'run' });
    const element = createElement();
    let handle: TwinHandle | undefined;
    // On WebAssembly the C# request commits the open and its render's update arrives inside the decision.
    const invokeMethod = fake.twin.invokeMethod.bind(fake.twin);
    fake.twin.invokeMethod = <T>(method: string, ...args: unknown[]): T => {
      const verdict = invokeMethod<T>(method, ...args);
      if (method === 'Decide') {
        update(handle!, { ...untilFound, open: true });
      }
      return verdict;
    };
    handle = attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));

    expect(element.hasAttribute(STARTING_STYLE)).toBe(false);
    expect(element.hasAttribute('hidden')).toBe(false);
    expect(fake.reported('OnStatusChange')).toEqual([{ status: 'idle', mounted: true }]);

    await flushTasks();
    runFrame();
    expect(element.hasAttribute('hidden')).toBe(false);

    update(handle, { ...untilFound, open: false });
    update(handle, { ...untilFound, open: false, unmountRequests: 1 });
    update(handle, { ...untilFound, open: true, unmountRequests: 1 });
    expect(element.hasAttribute(STARTING_STYLE)).toBe(true);
  });

  it('teardown cancels a pending reveal check', async () => {
    const fake = createFakeTwin({ synchronous: true, verdict: 'run' });
    const element = createElement();
    const handle = attachTo(element, fake, untilFound);

    element.removeAttribute('hidden');
    element.dispatchEvent(new Event('beforematch'));
    handle.dispose();
    await flushTasks();
    runFrame();

    expect(element.hasAttribute('hidden')).toBe(false);
  });

  it('listens for beforematch only when asked', () => {
    const fake = createFakeTwin();
    const element = createElement();
    attachTo(element, fake, { keepMounted: true });

    element.dispatchEvent(new Event('beforematch'));

    expect(fake.reports).toEqual([]);
  });
});

describe('the frame scheduler', () => {
  it('shares one native frame between the requests of one task and skips a canceled request', () => {
    const ran: number[] = [];
    requestFrame(() => ran.push(1));
    requestFrame(() => ran.push(2));
    expect(frames).toHaveLength(1);

    runFrame();
    expect(ran).toEqual([1, 2]);
  });
});
