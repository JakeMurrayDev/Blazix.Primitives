// @vitest-environment node
import { describe, expect, it } from 'vitest';
import {
  addedNames,
  dotnetOutcomes,
  isExempt,
  judge,
  parseDotnetList,
  parsePlaywrightList,
  parseVitestList,
  playwrightOutcomes,
  summarize,
  vitestFailedFiles,
  vitestOutcomes,
} from './red-check.mjs';

// ADR 11 section 3: the pure parts of the red-check job. The inputs below have the shapes the real tools print
// (`dotnet test --list-tests json`, a JUnit report, vitest's and Playwright's JSON reporters).
const root = '/work/repo';

describe('listing tests', () => {
  it('reads the display names from dotnet test --list-tests json, ignoring the lines printed before the JSON', () => {
    const output = [
      'Discovering tests from /work/repo/tests/A/bin/A.dll',
      '{"version":"1.0","testContainers":[{"tests":[{"displayName":"first test"},{"displayName":"theory(x: 1)"}]}]}',
    ].join('\n');
    expect(parseDotnetList(output)).toEqual(['first test', 'theory(x: 1)']);
  });

  it('reads the names from vitest\'s JSON reporter (vitest list does not expand it.each), relative to the repository root', () => {
    const json = JSON.stringify({
      testResults: [
        {
          name: `${root}/src/Blazix.Primitives/js/dom.test.ts`,
          assertionResults: [
            { ancestorTitles: ['outer'], title: 'inner one', status: 'passed' },
            { ancestorTitles: [], title: 'allows it when a is b', status: 'passed' },
            { ancestorTitles: [], title: 'allows it when c is d', status: 'failed' },
          ],
        },
      ],
    });
    expect(parseVitestList(json, root)).toEqual([
      'src/Blazix.Primitives/js/dom.test.ts::outer > inner one',
      'src/Blazix.Primitives/js/dom.test.ts::allows it when a is b',
      'src/Blazix.Primitives/js/dom.test.ts::allows it when c is d',
    ]);
  });

  it('reads Playwright --list --reporter=json as file::describe > title, once for both projects', () => {
    const test = (projectName) => ({ projectName, results: [], status: 'skipped' });
    const output = JSON.stringify({
      suites: [
        {
          title: 'tabs.spec.ts',
          file: 'tabs.spec.ts',
          specs: [{ title: 'top level', tags: [], tests: [test('server'), test('wasm')] }],
          suites: [{ title: 'indicator', specs: [{ title: 'moves', tags: [], tests: [test('server'), test('wasm')] }] }],
        },
      ],
    });
    expect(parsePlaywrightList(output)).toEqual(['tests/browser/tabs.spec.ts::top level', 'tests/browser/tabs.spec.ts::indicator > moves']);
  });
});

describe('Playwright output lists a test once per project', () => {
  const project = (projectName, status) => ({ projectName, results: [{ status }], status: 'skipped' });

  it('names each test once, however many projects it runs in', () => {
    const output = JSON.stringify({
      suites: [
        {
          title: 'trial.spec.ts',
          file: 'trial.spec.ts',
          specs: [],
          suites: [
            {
              title: 'trial',
              specs: [
                { title: 'moves', tests: [project('server', 'passed')] },
                { title: 'moves', tests: [project('wasm', 'passed')] },
              ],
            },
          ],
        },
      ],
    });
    expect(parsePlaywrightList(output)).toEqual(['tests/browser/trial.spec.ts::trial > moves']);
  });

  it('counts a test as passed when it passed in either project, even if it failed in the other', () => {
    const output = JSON.stringify({
      suites: [
        {
          file: 'trial.spec.ts',
          specs: [
            { title: 'moves', tests: [project('server', 'failed')] },
            { title: 'moves', tests: [project('wasm', 'passed')] },
            { title: 'broken', tests: [project('server', 'failed')] },
            { title: 'broken', tests: [project('wasm', 'skipped')] },
          ],
          suites: [],
        },
      ],
    });
    expect(Object.fromEntries(playwrightOutcomes(output))).toEqual({
      'tests/browser/trial.spec.ts::moves': 'passed',
      'tests/browser/trial.spec.ts::broken': 'failed',
    });
  });
});

describe('added names', () => {
  it('are the names on the head that the base does not have', () => {
    expect(addedNames(['a', 'b'], ['a', 'b', 'c', 'd'])).toEqual(['c', 'd']);
  });

  it('count duplicates, so a second test with an existing name is added', () => {
    expect(addedNames(['a', 'a'], ['a', 'a', 'a'])).toEqual(['a']);
  });

  it('are empty when nothing was added or tests were only removed', () => {
    expect(addedNames(['a', 'b'], ['a'])).toEqual([]);
    expect(addedNames([], [])).toEqual([]);
  });
});

describe('exempt tests', () => {
  it('lint tests verify the repository, not the base branch\'s src, so the check leaves them to their own planted-violation run', () => {
    expect(isExempt('l1', 'Blazix.Primitives.Tests.Lints.OwnershipLintTests.Flags_a_js_row_without_an_entry')).toBe(true);
    expect(isExempt('l3', 'src/Blazix.Primitives/js/module-graph.lint.test.ts::a mechanism module imports no component module')).toBe(true);
  });

  it('ordinary tests are not exempt', () => {
    expect(isExempt('l1', 'Blazix.Primitives.Tests.Rendering.MergePropsTests.Merges')).toBe(false);
    expect(isExempt('l3', 'src/Blazix.Primitives/js/dom.test.ts::reads the platform flags')).toBe(false);
    expect(isExempt('l4', 'tests/browser/tabs.spec.ts::moves')).toBe(false);
  });
});

describe('outcomes', () => {
  it('reads a JUnit report: passed, failed and skipped by test name', () => {
    const xml = `<testsuites><testsuite name="A.dll">
      <testcase name="passes" classname="X" />
      <testcase name="fails" classname="X"><failure message="boom">trace</failure></testcase>
      <testcase name="errors" classname="X"><error message="boom" /></testcase>
      <testcase name="skipped" classname="X"><skipped>no</skipped></testcase>
      <testcase name="a &amp; b &quot;quoted&quot;" classname="X" />
    </testsuite></testsuites>`;
    expect(Object.fromEntries(dotnetOutcomes(xml))).toEqual({
      passes: 'passed',
      fails: 'failed',
      errors: 'failed',
      skipped: 'skipped',
      'a & b "quoted"': 'passed',
    });
  });

  it('reads the vitest JSON reporter as file::ancestors > title', () => {
    const json = JSON.stringify({
      testResults: [
        {
          name: `${root}/src/Blazix.Primitives/js/dom.test.ts`,
          assertionResults: [
            { ancestorTitles: ['outer'], title: 'inner one', status: 'failed' },
            { ancestorTitles: [], title: 'top level', status: 'passed' },
            { ancestorTitles: [], title: 'later', status: 'pending' },
          ],
        },
      ],
    });
    expect(Object.fromEntries(vitestOutcomes(json, root))).toEqual({
      'src/Blazix.Primitives/js/dom.test.ts::outer > inner one': 'failed',
      'src/Blazix.Primitives/js/dom.test.ts::top level': 'passed',
      'src/Blazix.Primitives/js/dom.test.ts::later': 'skipped',
    });
  });

  it('reads the Playwright JSON reporter; a test that passes in either project counts as passed', () => {
    const result = (status) => ({ status });
    const json = JSON.stringify({
      suites: [
        {
          file: 'tabs.spec.ts',
          specs: [
            {
              title: 'moves',
              tests: [
                { projectName: 'server', results: [result('failed')] },
                { projectName: 'wasm', results: [result('passed')] },
              ],
            },
            { title: 'broken', tests: [{ projectName: 'server', results: [result('timedOut')] }, { projectName: 'wasm', results: [result('failed')] }] },
            { title: 'parked', tests: [{ projectName: 'server', results: [result('skipped')] }] },
          ],
          suites: [],
        },
      ],
    });
    expect(Object.fromEntries(playwrightOutcomes(json))).toEqual({
      'tests/browser/tabs.spec.ts::moves': 'passed',
      'tests/browser/tabs.spec.ts::broken': 'failed',
      'tests/browser/tabs.spec.ts::parked': 'skipped',
    });
  });
});

describe('a vitest file that does not load', () => {
  // Against the base's src/, a test file for a module the pull request adds cannot import it: vitest reports the file as
  // a failed suite with no per-test results. That is the same red as a .NET project that does not build.
  const report = JSON.stringify({
    testResults: [
      {
        name: `${root}/src/Blazix.Primitives/js/dom.test.ts`,
        status: 'failed',
        message: 'Failed to resolve import "./dom.js" from "src/Blazix.Primitives/js/dom.test.ts". Does the file exist?',
        assertionResults: [],
      },
      {
        name: `${root}/src/Blazix.Primitives/js/twin.test.ts`,
        status: 'passed',
        message: '',
        assertionResults: [{ ancestorTitles: [], title: 'attaches', status: 'passed' }],
      },
      {
        name: `${root}/src/Blazix.Primitives/js/decide.test.ts`,
        status: 'failed',
        message: '',
        assertionResults: [{ ancestorTitles: [], title: 'falls back', status: 'failed' }],
      },
    ],
  });

  it('is a failed file with no per-test results; a file whose tests ran and failed is not one', () => {
    expect(vitestFailedFiles(report, root)).toEqual(['src/Blazix.Primitives/js/dom.test.ts']);
  });

  it('counts every added test of such a file as red, and says why', () => {
    const added = ['src/Blazix.Primitives/js/dom.test.ts::reads the flags', 'src/Blazix.Primitives/js/dom.test.ts::outer > inner'];
    const verdict = judge(added, new Map(), { failedFiles: ['src/Blazix.Primitives/js/dom.test.ts'] });
    expect(verdict).toMatchObject({ red: added, unloadable: added, passed: [], notRun: [], ok: true });
  });

  it('still reports as not run a test whose file ran but which produced no result', () => {
    const outcomes = new Map([['src/Blazix.Primitives/js/twin.test.ts::attaches', 'passed']]);
    const added = ['src/Blazix.Primitives/js/dom.test.ts::reads the flags', 'src/Blazix.Primitives/js/twin.test.ts::filtered out'];
    const verdict = judge(added, outcomes, { failedFiles: ['src/Blazix.Primitives/js/dom.test.ts'] });
    expect(verdict.red).toEqual(['src/Blazix.Primitives/js/dom.test.ts::reads the flags']);
    expect(verdict.notRun).toEqual(['src/Blazix.Primitives/js/twin.test.ts::filtered out']);
    expect(verdict.ok).toBe(false);
  });

  it('fails when another added test of the layer passes, whatever the unloadable file says', () => {
    const outcomes = new Map([['src/Blazix.Primitives/js/twin.test.ts::attaches', 'passed']]);
    const added = ['src/Blazix.Primitives/js/dom.test.ts::reads the flags', 'src/Blazix.Primitives/js/twin.test.ts::attaches'];
    expect(judge(added, outcomes, { failedFiles: ['src/Blazix.Primitives/js/dom.test.ts'] }).ok).toBe(false);
  });

  it('is named in the summary', () => {
    const added = ['src/Blazix.Primitives/js/dom.test.ts::a', 'src/Blazix.Primitives/js/dom.test.ts::b'];
    const verdict = judge(added, new Map(), { failedFiles: ['src/Blazix.Primitives/js/dom.test.ts'] });
    const { markdown, ok } = summarize([{ layer: 'l3', added, exempt: [], verdict }]);
    expect(ok).toBe(true);
    expect(markdown).toContain('| l3 | 2 | 2 | 0 |');
    expect(markdown).toContain('red: the test file does not load against the base');
    expect(markdown).toContain('`src/Blazix.Primitives/js/dom.test.ts` (2 tests)');
  });
});

describe('judging a layer', () => {
  const outcomes = new Map([
    ['red one', 'failed'],
    ['green one', 'passed'],
    ['parked', 'skipped'],
  ]);

  it('fails the check when an added test passes against the base branch\'s src', () => {
    const verdict = judge(['red one', 'green one'], outcomes);
    expect(verdict.passed).toEqual(['green one']);
    expect(verdict.red).toEqual(['red one']);
    expect(verdict.ok).toBe(false);
  });

  it('passes when every added test fails', () => {
    expect(judge(['red one'], outcomes)).toMatchObject({ red: ['red one'], passed: [], ok: true });
  });

  it('does not count a skipped test either way', () => {
    expect(judge(['red one', 'parked'], outcomes)).toMatchObject({ red: ['red one'], skipped: ['parked'], ok: true });
  });

  it('fails when an added test never ran, so a wrong filter cannot pass the check', () => {
    const verdict = judge(['red one', 'missing'], outcomes);
    expect(verdict.notRun).toEqual(['missing']);
    expect(verdict.ok).toBe(false);
  });

  it('counts every added test as red when the layer does not build against the base branch', () => {
    expect(judge(['a', 'b'], new Map(), { buildFailed: true })).toMatchObject({ red: ['a', 'b'], passed: [], notRun: [], ok: true });
  });

  it('has nothing to judge when nothing was added', () => {
    expect(judge([], new Map())).toMatchObject({ red: [], passed: [], notRun: [], ok: true });
  });
});

describe('summary', () => {
  it('skips cleanly when no layer added a test', () => {
    const { markdown, ok } = summarize([
      { layer: 'l1', added: [], exempt: [] },
      { layer: 'l3', added: [], exempt: [] },
    ]);
    expect(ok).toBe(true);
    expect(markdown).toContain('No test was added');
  });

  it('lists each layer\'s added, red and passing tests and fails on a passing one', () => {
    const { markdown, ok } = summarize([
      { layer: 'l1', added: ['a', 'b'], exempt: ['lint'], verdict: { red: ['a'], passed: ['b'], skipped: [], notRun: [], ok: false } },
      { layer: 'l2', added: [], exempt: [] },
    ]);
    expect(ok).toBe(false);
    expect(markdown).toContain('| l1 | 2 | 1 | 1 |');
    expect(markdown).toContain('passes against the base branch');
    expect(markdown).toContain('`b`');
  });
});
