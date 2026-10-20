// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { summarizeNightly } from './nightly-summary.mjs';

// ADR 11 sections 1 and 4: the nightly full L4 run (--repeat-each 3, four shards, both render modes) ends with a
// summary of its failures, its flaky count and its quarantine count, read from the merged Playwright JSON report.
const test = (projectName, status, repeatEachIndex = 0) => ({ projectName, status, repeatEachIndex, results: [{ status: status === 'expected' ? 'passed' : 'failed' }] });
const spec = (title, tests, tags = []) => ({ title, tags, tests });
const report = (specs, stats = {}) => ({
  stats: { expected: 0, unexpected: 0, flaky: 0, skipped: 0, ...stats },
  suites: [{ title: 'tabs.spec.ts', file: 'tabs.spec.ts', specs, suites: [] }],
});

describe('nightly summary', () => {
  it('is green with no failures, no flaky tests and the quarantine count when everything passed', () => {
    const { ok, markdown, flaky, quarantined, failures } = summarizeNightly(
      report(
        [
          spec('moves', [test('server', 'expected'), test('wasm', 'expected')]),
          spec('parked', [test('server', 'skipped'), test('wasm', 'skipped')], ['quarantine']),
        ],
        { expected: 2, skipped: 2 },
      ),
    );
    expect(ok).toBe(true);
    expect(failures).toEqual([]);
    expect(flaky).toBe(0);
    expect(quarantined).toBe(1);
    expect(markdown).toContain('Flaky tests: 0');
    expect(markdown).toContain('Quarantined (`@quarantine`): 1');
  });

  it('lists each failing test once per project, however many repeats failed', () => {
    const { ok, failures, markdown } = summarizeNightly(
      report(
        [spec('moves', [test('server', 'unexpected', 0), test('server', 'unexpected', 2), test('wasm', 'expected', 0)])],
        { expected: 1, unexpected: 2 },
      ),
    );
    expect(ok).toBe(false);
    expect(failures).toEqual([{ file: 'tabs.spec.ts', title: 'moves', project: 'server', failedRepeats: 2 }]);
    expect(markdown).toContain('`tabs.spec.ts` › moves (server, failed in 2 of the repeats)');
  });

  it('reports a flaky test and fails, because retries are 0 and a flaky result means retries were turned on', () => {
    const { ok, flaky, markdown } = summarizeNightly(report([spec('moves', [test('server', 'flaky')])], { flaky: 1 }));
    expect(ok).toBe(false);
    expect(flaky).toBe(1);
    expect(markdown).toContain('Flaky tests: 1');
  });

  it('counts a quarantined test once, not once per project or per repeat', () => {
    const { quarantined } = summarizeNightly(
      report([spec('parked', [test('server', 'skipped', 0), test('server', 'skipped', 1), test('wasm', 'skipped', 0)], ['quarantine'])]),
    );
    expect(quarantined).toBe(1);
  });

  it('descends into describe blocks', () => {
    const nested = {
      stats: { expected: 0, unexpected: 1, flaky: 0, skipped: 0 },
      suites: [{ title: 'tabs.spec.ts', file: 'tabs.spec.ts', specs: [], suites: [{ title: 'indicator', specs: [spec('moves', [test('wasm', 'unexpected')])] }] }],
    };
    expect(summarizeNightly(nested).failures).toEqual([{ file: 'tabs.spec.ts', title: 'indicator › moves', project: 'wasm', failedRepeats: 1 }]);
  });
});
