// @vitest-environment node
/// <reference types="node" />
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { checkGraph, extractImports, moduleKinds, readModules, type ModuleKind } from './module-graph.lint';

// The module-graph lint (the rules and the traceability are in js/CONTEXT.md): a mechanism module never imports a component module;
// a component module imports only mechanism modules, the shared helpers and the vendored Floating UI. The list of
// modules and their kinds is `moduleKinds`, the only place a module's kind is written down; a file under js/ that the
// list does not name fails, because adding a module is a decision that is recorded.
const kinds: Record<string, ModuleKind> = {
  positioning: 'mechanism',
  dom: 'mechanism',
  slider: 'component',
  tabs: 'component',
  twin: 'shared',
};
const module = (name: string, ...imports: string[]) => ({ name, imports });

describe('reading imports', () => {
  it('finds static, side-effect, re-export, type and dynamic imports', () => {
    const source = [
      "import { a } from './dom.js';",
      "import type { B } from './twin.js';",
      "import './tabs.js';",
      "export * from './slider.js';",
      "export { c } from '@floating-ui/dom';",
      "const lazy = await import('./positioning.js');",
      'import {',
      '  d,',
      "} from './dom.js';",
    ].join('\n');
    expect(extractImports(source)).toEqual(['./dom.js', './twin.js', './tabs.js', './slider.js', '@floating-ui/dom', './positioning.js', './dom.js']);
  });

  it('ignores imports in comments', () => {
    const source = ["// import { a } from './slider.js';", "/* import './tabs.js'; */", "import { b } from './dom.js';"].join('\n');
    expect(extractImports(source)).toEqual(['./dom.js']);
  });
});

describe('the rules', () => {
  it('flags a mechanism module that imports a component module (a planted import on a scratch module)', () => {
    const problems = checkGraph([module('positioning', './slider.js')], kinds);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('positioning');
    expect(problems[0]).toContain('slider');
  });

  it('flags a component module that imports another component module', () => {
    expect(checkGraph([module('slider', './tabs.js')], kinds)).toHaveLength(1);
  });

  it('flags a shared helper that imports a component module', () => {
    expect(checkGraph([module('twin', './slider.js')], kinds)).toHaveLength(1);
  });

  it('flags a component module that imports a package other than Floating UI', () => {
    const problems = checkGraph([module('slider', 'lodash')], kinds);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('lodash');
  });

  it('flags a file that no kind names, and an import of one', () => {
    expect(checkGraph([module('mystery')], kinds)).toHaveLength(1);
    expect(checkGraph([module('slider', './mystery.js')], kinds)).toHaveLength(1);
  });

  it.each([
    ['a mechanism module imports a mechanism module', module('positioning', './dom.js')],
    ['a mechanism module imports a shared helper', module('positioning', './twin.js')],
    ['a mechanism module imports a package', module('positioning', '@floating-ui/dom')],
    ['a component module imports a mechanism module', module('slider', './dom.js', './positioning')],
    ['a component module imports a shared helper', module('slider', './twin.ts')],
    ['a component module imports Floating UI', module('slider', '@floating-ui/dom')],
    ['a shared helper imports a shared helper', module('twin', './twin.js')],
  ])('allows it when %s', (_name, subject) => {
    expect(checkGraph([subject], kinds)).toEqual([]);
  });
});

describe('src/Blazix.Primitives/js', () => {
  it('has no mechanism-to-component import, no component-to-component import, and names the kind of every module', () => {
    const directory = fileURLToPath(new URL('.', import.meta.url));
    expect(checkGraph(readModules(directory), moduleKinds)).toEqual([]);
  });
});
