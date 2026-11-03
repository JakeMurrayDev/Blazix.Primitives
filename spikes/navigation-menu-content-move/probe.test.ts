// Throwaway probe (ADR 04 Amendments 2026-11-03 (4), M5 track 5 deliverable 5): can a lite `portal` handle per
// NavigationMenu.Content, with the Viewport target as its `ContainerRef`, carry Base UI's Content move? Each case builds
// the DOM C# renders (the Item's `li`, the Content's in-place wrapper and node, the Portal's node holding the Viewport
// target) and replays the DOM operations Blazor performs (`removeChild` on the topmost removed element, attribute edits
// on the node wherever it is). Run: npx vitest run --config spikes/navigation-menu-content-move/vitest.config.ts
import { afterEach, describe, expect, it } from 'vitest';
import { attach } from '../../src/Blazix.Primitives/js/portal';
import { createFakeTwin } from '../../src/Blazix.Primitives/js/test-support/fake-twin';

interface Content {
  wrapper: HTMLElement;
  node: HTMLElement;
}

function renderItem(): { li: HTMLElement; content: Content } {
  const li = document.createElement('li');
  const wrapper = document.createElement('div');
  wrapper.style.display = 'contents';
  const node = document.createElement('div');
  node.setAttribute('style', 'display: none');
  node.textContent = 'content';
  wrapper.appendChild(node);
  li.appendChild(wrapper);
  document.body.appendChild(li);
  return { li, content: { wrapper, node } };
}

// The Portal part: its in-place wrapper stays where C# renders it; its node (moved to the body) holds the Viewport target.
function renderPortal(): { portalWrapper: HTMLElement; portalNode: HTMLElement; target: HTMLElement } {
  const portalWrapper = document.createElement('div');
  portalWrapper.style.display = 'contents';
  document.body.appendChild(portalWrapper);
  const portalNode = document.createElement('div');
  portalNode.setAttribute('data-base-ui-portal', '');
  const target = document.createElement('div');
  target.setAttribute('data-testid', 'viewport-target');
  portalNode.appendChild(target);
  document.body.appendChild(portalNode);
  return { portalWrapper, portalNode, target };
}

const lite = (style = '') => JSON.stringify({ containerRef: 0, lite: true, style });

afterEach(() => {
  document.body.replaceChildren();
});

describe('Content move through a lite portal handle', () => {
  it('moves the Content node into the Viewport target and hands its style to the module', () => {
    const { content } = renderItem();
    const { target } = renderPortal();

    attach(content.node, lite('color: red'), createFakeTwin().twin, [target]);

    expect(content.node.parentElement).toBe(target);
    expect(content.node.style.cssText).toBe('color: red;');
    expect(content.wrapper.childElementCount).toBe(0);
  });

  it('keeps the same element across a switch, with both Contents in the target', () => {
    const first = renderItem();
    const second = renderItem();
    const { target } = renderPortal();

    const a = attach(first.content.node, lite(), createFakeTwin().twin, [target]);
    attach(second.content.node, lite(), createFakeTwin().twin, [target]);
    const before = first.content.node;
    // The exiting Content's style changes (position: absolute; top: 0; left: 0) through an update; Blazor edits its
    // attributes wherever it is.
    a.update(lite('position: absolute; top: 0; left: 0'), [target]);
    first.content.node.setAttribute('data-ending-style', '');

    expect(target.children[0]).toBe(before);
    expect(target.children[1]).toBe(second.content.node);
    expect(before.style.position).toBe('absolute');
  });

  it('follows a container that disappears and reappears, through an update', () => {
    const { content } = renderItem();
    const first = renderPortal();
    const handle = attach(content.node, lite(), createFakeTwin().twin, [first.target]);

    // A non-kept Portal leaves: Blazor removes its wrapper; the portal module removes its node, and the target with
    // the Content inside goes with it.
    first.portalWrapper.parentNode!.removeChild(first.portalWrapper);
    first.portalNode.remove();
    expect(content.node.isConnected).toBe(false);
    // Blazor still edits the kept Content while it is detached.
    content.node.setAttribute('data-closed', '');

    const second = renderPortal();
    handle.update(lite(), [second.target]);

    expect(content.node.parentElement).toBe(second.target);
    expect(content.node.hasAttribute('data-closed')).toBe(true);
  });

  it('puts a detached Content back in its wrapper on dispose, before Blazor removes the wrapper', () => {
    const { li, content } = renderItem();
    const { portalWrapper, portalNode, target } = renderPortal();
    const handle = attach(content.node, lite(), createFakeTwin().twin, [target]);
    portalWrapper.remove();
    portalNode.remove();

    handle.dispose();
    expect(content.node.parentElement).toBe(content.wrapper);
    expect(content.node.style.display).toBe('none');
    // Blazor removes the Content: the wrapper, which never left the `li`.
    expect(() => li.removeChild(content.wrapper)).not.toThrow();
  });

  it('removes the moved node when Blazor removes the wrapper in place', async () => {
    const { li, content } = renderItem();
    const { target } = renderPortal();
    attach(content.node, lite(), createFakeTwin().twin, [target]);

    li.removeChild(content.wrapper);
    await Promise.resolve();

    expect(target.contains(content.node)).toBe(false);
  });

  it('the hazard: an update whose reference no longer resolves moves the node into the body', () => {
    const { content } = renderItem();
    const { target } = renderPortal();
    const handle = attach(content.node, lite(), createFakeTwin().twin, [target]);

    // Blazor revives an ElementReference by its capture id; a removed element revives as null.
    handle.update(lite(), [null]);

    expect(content.node.parentElement).toBe(document.body);
  });
});
