/// <reference types="node" />
import { readdirSync, readFileSync } from 'node:fs';
import { posix } from 'node:path';

// The import rules of the JavaScript modules and the list that makes them checkable (js/CONTEXT.md has the rules). Read by
// module-graph.lint.test.ts, which fails the build when a rule is broken. This file is not a module of the library.

/** mechanism: one per architecture document; component: one component's loops; shared: the twin contract and entry points. */
export type ModuleKind = 'mechanism' | 'component' | 'shared';

/**
 * The kind of every module under js/, by file name without extension. Adding a module is a recorded decision and adds a
 * row here; a file this list does not name fails the lint.
 */
export const moduleKinds: Record<string, ModuleKind> = {
  // Mechanism modules: one per architecture document.
  positioning: 'mechanism',
  interactions: 'mechanism',
  'focus-manager': 'mechanism',
  portal: 'mechanism',
  transitions: 'mechanism',
  composite: 'mechanism',
  button: 'mechanism',
  'scroll-lock': 'mechanism',
  dom: 'mechanism',
  swipe: 'mechanism',
  form: 'mechanism',
  // Component modules.
  slider: 'component',
  'scroll-area': 'component',
  drawer: 'component',
  'number-field': 'component',
  tabs: 'component',
  select: 'component',
  'otp-field': 'component',
  toast: 'component',
  'context-menu': 'component',
  'navigation-menu': 'component',
  collapsible: 'component',
  // Shared helpers: the twin contract, the event snapshot, the platform flags and the initializer.
  twin: 'shared',
  decide: 'shared',
  'event-info': 'shared',
  platform: 'shared',
  initializer: 'shared',
};

// ADR 04 and ADR 07 Amendments 2026-10-27.
/**
 * The Floating UI wrapper: js/floating-ui.ts carries Floating UI's legal comment
 * and re-exports the engine, so the comment ships in the one output that bundles the engine. It is no module of its own
 * (`readModules` skips it, as it skips test-support/), `positioning` is its only importer, and no module imports
 * `@floating-ui/*` around it.
 */
export const floatingUiWrapper = { name: 'floating-ui', importer: 'positioning' } as const;

const isFloatingUiPackage = (specifier: string): boolean => specifier.startsWith('@floating-ui/');

const stripComments = (source: string): string => source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|\s)\/\/.*$/gm, '$1');

// import ... from 'x' | export ... from 'x' | import 'x' | import('x'), in order of appearance.
const importPattern = /(?:\b(?:import|export)\b[^'";()]*?\bfrom\s*|\bimport\s*\(\s*|\bimport\s*)(['"])([^'"]+)\1/g;

/** @returns the module specifiers a source file imports, in order */
export function extractImports(source: string): string[] {
  return [...stripComments(source).matchAll(importPattern)].map((match) => match[2] ?? '');
}

const targetName = (from: string, specifier: string): string =>
  posix.join(posix.dirname(from), specifier).replace(/\.(?:js|ts|mjs)$/, '');

/** @returns one message per broken rule; empty when the graph is clean */
export function checkGraph(modules: { name: string; imports: string[] }[], kinds: Record<string, ModuleKind>): string[] {
  const problems: string[] = [];
  for (const { name, imports } of modules) {
    const kind = kinds[name];
    if (kind === undefined) {
      problems.push(`${name}: no kind is named for this module (add it to moduleKinds; see js/CONTEXT.md)`);
      continue;
    }
    for (const specifier of imports) {
      if (!specifier.startsWith('.')) {
        if (isFloatingUiPackage(specifier)) {
          problems.push(`${name} imports ${specifier}: a module imports Floating UI only through js/floating-ui.ts, which carries its license banner`);
        } else if (kind === 'component') {
          problems.push(`${name} (component) imports the package ${specifier}: a component module imports only mechanism modules and shared helpers`);
        }
        continue;
      }
      const target = targetName(name, specifier);
      const targetKind = kinds[target];
      if (target === floatingUiWrapper.name) {
        if (name !== floatingUiWrapper.importer) {
          problems.push(`${name} imports ${specifier}: only positioning imports the Floating UI wrapper, so the engine and its banner stay in js/positioning.js`);
        }
      } else if (targetKind === undefined) {
        problems.push(`${name} imports ${specifier}, which names no module in moduleKinds`);
      } else if (targetKind === 'component' && target !== name) {
        const reason = kind === 'component' ? 'a component module never imports another component module' : `a ${kind} module never imports a component module`;
        problems.push(`${name} (${kind}) imports ${target} (component): ${reason}`);
      }
    }
  }
  return problems;
}

/**
 * @param directory the js/ folder
 * @returns every module in it (tests, test support, declarations, lint files and the Floating UI wrapper excluded) with its imports
 */
export function readModules(directory: string): { name: string; imports: string[] }[] {
  return readdirSync(directory, { recursive: true, encoding: 'utf8' })
    .map((file) => file.split('\\').join('/'))
    .filter(
      (file) =>
        file.endsWith('.ts') &&
        file !== `${floatingUiWrapper.name}.ts` &&
        !/\.(?:test|lint|d)\.ts$/.test(file) &&
        !/(?:^|\/)(?:node_modules|chunks|test-support)\//.test(file),
    )
    .sort()
    .map((file) => ({ name: file.slice(0, -'.ts'.length), imports: extractImports(readFileSync(posix.join(directory.split('\\').join('/'), file), 'utf8')) }));
}
