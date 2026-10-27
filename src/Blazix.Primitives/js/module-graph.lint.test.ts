// @vitest-environment node
/// <reference types="node" />
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { checkGraph, extractImports, moduleKinds, readModules, type ModuleKind } from './module-graph.lint';

// The module-graph lint (the rules and the traceability are in js/CONTEXT.md): a mechanism module never imports a component module;
// a component module imports only mechanism modules and the shared helpers; only positioning imports Floating UI, through js/floating-ui.ts. The list of
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

  it('flags a component module that imports a package', () => {
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
    ['a mechanism module imports a package', module('dom', 'tabbable')],
    ['positioning imports the Floating UI wrapper', module('positioning', './floating-ui.js')],
    ['a component module imports a mechanism module', module('slider', './dom.js', './positioning')],
    ['a component module imports a shared helper', module('slider', './twin.ts')],
    ['a shared helper imports a shared helper', module('twin', './twin.js')],
  ])('allows it when %s', (_name, subject) => {
    expect(checkGraph([subject], kinds)).toEqual([]);
  });
});

// ADR 04 and ADR 07 Amendments 2026-10-27: js/floating-ui.ts carries Floating UI's legal comment and re-exports the
// engine, so the comment ships in the one output that bundles the engine. The lint keeps that output unique: the wrapper
// is not a module of its own, positioning is its only importer, and no module imports @floating-ui/* around it.
describe('the Floating UI wrapper', () => {
  it('flags a second importer of the wrapper (a planted import on a scratch module)', () => {
    const problems = checkGraph([module('dom', './floating-ui.js')], kinds);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('only positioning imports the Floating UI wrapper');
  });

  it('flags a component module that imports the wrapper', () => {
    const problems = checkGraph([module('slider', './floating-ui')], kinds);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('only positioning imports the Floating UI wrapper');
  });

  it.each([
    ['positioning', '@floating-ui/dom'],
    ['positioning', '@floating-ui/utils'],
    ['dom', '@floating-ui/utils/dom'],
    ['slider', '@floating-ui/dom'],
  ])('flags %s importing %s around the wrapper', (name, specifier) => {
    const problems = checkGraph([module(name, specifier)], kinds);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('imports Floating UI only through js/floating-ui.ts');
  });

  it('reads the wrapper as no module of its own (a scratch js/ folder)', () => {
    const directory = mkdtempSync(join(tmpdir(), 'blazix-module-graph-'));
    try {
      writeFileSync(join(directory, 'positioning.ts'), "export * from './floating-ui';\n");
      writeFileSync(join(directory, 'floating-ui.ts'), "export { computePosition } from '@floating-ui/dom';\n");
      expect(readModules(directory)).toEqual([{ name: 'positioning', imports: ['./floating-ui'] }]);
    } finally {
      rmSync(directory, { recursive: true, force: true });
    }
  });
});

describe('src/Blazix.Primitives/js', () => {
  it('has no mechanism-to-component import, no component-to-component import, and names the kind of every module', () => {
    const directory = fileURLToPath(new URL('.', import.meta.url));
    expect(checkGraph(readModules(directory), moduleKinds)).toEqual([]);
  });
});
