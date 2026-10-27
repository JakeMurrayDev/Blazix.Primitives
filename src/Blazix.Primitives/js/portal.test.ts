// L3 (ADR 06 §1): the `portal` module in jsdom against the fake twin. FloatingPortal.test.tsx's container titles are
// re-tagged from L4 to the module that moves the node (settled by the orchestrator 2026-10-27, plan §Settled "Layer tags
// and browser-only titles"), as the module observes them: each case builds the DOM C# renders (the wrapper, the hidden
// portal node, the guards and the owner beside the wrapper) and attaches as the twin would. Port titles cover the move,
// the restore and the cleanup, the selector, the published state, the VoiceOver swap and the lite mode.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createFakeTwin } from './test-support/fake-twin';

const flags = vi.hoisted(() => ({ voiceOver: false, webkit: false }));

vi.mock('./platform', async (importOriginal) => {
  const original = await importOriginal<typeof import('./platform')>();
  return {
    ...original,
    platform: {
      ...original.platform,
      engine: {
        ...original.platform.engine,
        get webkit() {
          return flags.webkit;
        },
      },
      screenReader: {
        get voiceOver() {
          return flags.voiceOver;
        },
      },
    },
  };
});

const { attach, getPortalState } = await import('./portal');

interface RenderedPortal {
  host: HTMLElement;
  wrapper: HTMLElement;
  node: HTMLElement;
  floating: HTMLElement;
  guards: HTMLElement[];
}

/** Renders what C# renders: [guard, owner,] wrapper(node(floating)) [, guard] inside a host. */
function renderPortal({ guards = false, nodeStyle = 'display: none' } = {}): RenderedPortal {
  const host = document.createElement('section');
  host.setAttribute('data-testid', 'host');
  const wrapper = document.createElement('div');
  wrapper.style.display = 'contents';
  const node = document.createElement('div');
  node.id = 'base-ui-1';
  node.setAttribute('data-base-ui-portal', '');
  node.setAttribute('style', nodeStyle);
  const floating = document.createElement('div');
  floating.setAttribute('data-testid', 'floating');
  node.appendChild(floating);
  wrapper.appendChild(node);
  const rendered: HTMLElement[] = [];
  if (guards) {
    const before = createGuard();
    const owner = document.createElement('span');
    owner.setAttribute('aria-owns', node.id);
    const after = createGuard();
    host.append(before, owner, wrapper, after);
    rendered.push(before, after);
  } else {
    host.append(wrapper);
  }
  document.body.appendChild(host);
  return { host, wrapper, node, floating, guards: rendered };
}

function createGuard(): HTMLElement {
  const guard = document.createElement('span');
  guard.tabIndex = 0;
  guard.setAttribute('data-base-ui-focus-guard', '');
  guard.setAttribute('data-type', 'outside');
  guard.setAttribute('aria-hidden', 'true');
  return guard;
}

beforeEach(() => {
  flags.voiceOver = false;
  flags.webkit = false;
});

afterEach(() => {
  document.body.replaceChildren();
});

describe('FloatingPortal', () => {
  it('allows custom containers', () => {
    const customRoot = document.createElement('div');
    customRoot.id = 'custom-root';
    document.body.appendChild(customRoot);
    const { floating } = renderPortal();

    attach(floating.parentElement!, JSON.stringify({ containerRef: 0 }), createFakeTwin().twin, [customRoot]);

    const parent = floating.parentElement;
    expect(parent?.hasAttribute('data-base-ui-portal')).toBe(true);
    expect(parent?.parentElement).toBe(customRoot);
    customRoot.remove();
  });

  it('allows refs as containers', () => {
    // A ref is an element C# holds an ElementReference to; Blazor revives it in the references array.
    const el = document.createElement('div');
    document.body.appendChild(el);
    const { node, floating } = renderPortal();

    attach(node, JSON.stringify({ containerRef: 0 }), createFakeTwin().twin, [el]);

    const parent = floating.parentElement;
    expect(parent?.hasAttribute('data-base-ui-portal')).toBe(true);
    expect(parent?.parentElement).toBe(el);
  });

  it('allows containers to be initially null', () => {
    // A pending container renders no node in C#, so the module first sees the node once a container exists.
    const { node, wrapper, floating } = renderPortal();
    expect(node.parentElement).toBe(wrapper);
    expect(node.style.display).toBe('none');

    const root = document.createElement('div');
    root.setAttribute('data-testid', 'root');
    document.body.appendChild(root);
    attach(node, JSON.stringify({ containerRef: 0 }), createFakeTwin().twin, [root]);

    expect(floating.parentElement?.parentElement).toBe(root);
    expect(node.style.display).toBe('');
  });

  it('reattaches the portal when the container changes', () => {
    const first = document.createElement('div');
    const second = document.createElement('div');
    document.body.append(first, second);
    const { node, floating } = renderPortal();

    const handle = attach(node, JSON.stringify({ containerRef: 0 }), createFakeTwin().twin, [first]);
    expect(floating.parentElement?.parentElement).toBe(first);

    handle.update(JSON.stringify({ containerRef: 0 }), [second]);
    expect(floating.parentElement?.parentElement).toBe(second);
    expect(first.children).toHaveLength(0);
  });
});

describe('the move, the restore and the cleanup', () => {
  it('port: the node moves out of its wrapper to the end of the body by default, takes the consumer style, and the wrapper stays in place', () => {
    const { host, wrapper, node } = renderPortal({ nodeStyle: 'display: none; color: red' });
    const after = document.createElement('p');
    document.body.appendChild(after);

    attach(node, JSON.stringify({ style: 'color: red' }), createFakeTwin().twin);

    expect(document.body.lastElementChild).toBe(node);
    expect(wrapper.parentElement).toBe(host);
    expect(wrapper.children).toHaveLength(0);
    expect(node.getAttribute('style')).toBe('color: red;');
  });

  it("port: a selector resolves the first match in the wrapper's document, and a selector that matches nothing keeps the node hidden in place", () => {
    const first = document.createElement('div');
    first.className = 'target';
    const second = document.createElement('div');
    second.className = 'target';
    document.body.append(first, second);
    const { node, wrapper } = renderPortal();

    const handle = attach(node, JSON.stringify({ containerSelector: '.missing' }), createFakeTwin().twin);
    expect(node.parentElement).toBe(wrapper);
    expect(node.style.display).toBe('none');

    handle.update(JSON.stringify({ containerSelector: '.target' }));
    expect(node.parentElement).toBe(first);
    expect(node.style.display).toBe('');
  });

  it("port: dispose puts the node back in its wrapper, hidden, while the wrapper is in the document, and attaching again moves it out", () => {
    const { node, wrapper } = renderPortal();
    const handle = attach(node, JSON.stringify({}), createFakeTwin().twin);
    expect(node.parentElement).toBe(document.body);

    handle.dispose();
    handle.dispose();
    expect(node.parentElement).toBe(wrapper);
    expect(node.style.display).toBe('none');
    expect(getPortalState(node)).toBeNull();

    attach(node, JSON.stringify({}), createFakeTwin().twin);
    expect(node.parentElement).toBe(document.body);
  });

  it('port: when Blazor removes the wrapper, the moved node leaves the document with it, before dispose', async () => {
    const { host, wrapper, node } = renderPortal();
    const handle = attach(node, JSON.stringify({}), createFakeTwin().twin);
    expect(node.isConnected).toBe(true);

    host.removeChild(wrapper);
    await Promise.resolve();

    expect(node.isConnected).toBe(false);
    expect(document.querySelector('[data-base-ui-portal]')).toBeNull();
    handle.dispose();
    expect(node.isConnected).toBe(false);
  });

  it('port: the wrapper moved by Blazor among its siblings keeps the node where it was moved', async () => {
    const { host, wrapper, node } = renderPortal();
    const sibling = document.createElement('p');
    host.appendChild(sibling);
    attach(node, JSON.stringify({}), createFakeTwin().twin);

    host.insertBefore(sibling, wrapper);
    await Promise.resolve();

    expect(node.parentElement).toBe(document.body);
    expect(wrapper.isConnected).toBe(true);
  });

  it('port: a nested portal moves into the enclosing portal node it receives as its container', () => {
    const outer = renderPortal();
    attach(outer.node, JSON.stringify({}), createFakeTwin().twin);
    const innerWrapper = document.createElement('div');
    const innerNode = document.createElement('div');
    innerNode.setAttribute('data-base-ui-portal', '');
    innerWrapper.appendChild(innerNode);
    outer.floating.appendChild(innerWrapper);

    attach(innerNode, JSON.stringify({ containerRef: 0 }), createFakeTwin().twin, [outer.node]);

    expect(innerNode.parentElement).toBe(outer.node);
  });

  it('port: a repeated attach of the same node returns the live handle', () => {
    const { node } = renderPortal();
    const twin = createFakeTwin().twin;

    expect(attach(node, '{}', twin)).toBe(attach(node, '{}', twin));
  });
});

describe('the published state and the outside guards', () => {
  it('port: the handle publishes its node, wrapper and outside guards on the node, found by position, and a slot for the focus manager', () => {
    const { node, wrapper, floating, guards } = renderPortal({ guards: true });
    attach(node, '{}', createFakeTwin().twin);

    const state = getPortalState(floating);
    expect(state?.node).toBe(node);
    expect(state?.wrapper).toBe(wrapper);
    expect(state?.outsideGuards()).toEqual({ before: guards[0], after: guards[1] });
    expect(state?.focusManager).toBeNull();
    state!.focusManager = { modal: false, closeOnFocusOut: true, open: true, domReference: null };
    expect(getPortalState(node)?.focusManager?.open).toBe(true);
  });

  it('port: under VoiceOver on WebKit the outside guards become buttons without aria-hidden, including guards rendered after attach', async () => {
    flags.voiceOver = true;
    flags.webkit = true;
    const { host, wrapper, node, guards } = renderPortal({ guards: true });
    attach(node, '{}', createFakeTwin().twin);

    for (const guard of guards) {
      expect(guard.getAttribute('role')).toBe('button');
      expect(guard.hasAttribute('aria-hidden')).toBe(false);
    }

    guards[1]!.remove();
    const later = createGuard();
    host.insertBefore(later, wrapper.nextSibling);
    await Promise.resolve();

    expect(later.getAttribute('role')).toBe('button');
    expect(later.hasAttribute('aria-hidden')).toBe(false);
  });

  it('port: without VoiceOver on WebKit, and in lite mode, the outside guards keep aria-hidden and get no role', () => {
    flags.voiceOver = true;
    const plain = renderPortal({ guards: true });
    attach(plain.node, '{}', createFakeTwin().twin);
    expect(plain.guards.map((guard) => [guard.getAttribute('role'), guard.getAttribute('aria-hidden')])).toEqual([
      [null, 'true'],
      [null, 'true'],
    ]);

    flags.webkit = true;
    const lite = renderPortal({ guards: true });
    const handle = attach(lite.node, JSON.stringify({ lite: true }), createFakeTwin().twin);
    expect(lite.guards.map((guard) => guard.getAttribute('aria-hidden'))).toEqual(['true', 'true']);
    expect(getPortalState(lite.node)?.lite).toBe(true);
    expect(lite.node.parentElement).toBe(document.body);
    handle.dispose();
  });
});
