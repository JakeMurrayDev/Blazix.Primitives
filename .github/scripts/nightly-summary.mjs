// ADR 11 sections 1 and 4: the summary of the nightly full L4 run.
//
//   node .github/scripts/nightly-summary.mjs merged-report.json
//
// The input is the merged Playwright JSON report of all shards (`playwright merge-reports --reporter json`). The
// summary lists each failing test once per project, the flaky count (retries are 0 and --fail-on-flaky-tests is set,
// so the count is a guard and stays 0) and the number of quarantined specs (`test.fixme` with the `@quarantine` tag).
import { appendFileSync, readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

function* specsOf(suite, titles) {
  for (const spec of suite.specs ?? []) yield { spec, titles: [...titles, spec.title] };
  for (const child of suite.suites ?? []) yield* specsOf(child, [...titles, child.title]);
}

/** @param {{ stats?: Record<string, number>, suites?: object[] }} report a merged Playwright JSON report */
export function summarizeNightly(report) {
  const failuresByTest = new Map();
  const quarantined = new Set();
  let flaky = 0;
  let total = 0;

  for (const fileSuite of report.suites ?? []) {
    for (const { spec, titles } of specsOf(fileSuite, [])) {
      const title = titles.join(' › ');
      if (spec.tags?.includes('quarantine')) quarantined.add(`${fileSuite.file}::${title}`);
      for (const test of spec.tests ?? []) {
        total += 1;
        if (test.status === 'flaky') flaky += 1;
        if (test.status !== 'unexpected') continue;
        const key = `${fileSuite.file}::${title}::${test.projectName}`;
        const known = failuresByTest.get(key);
        if (known) known.failedRepeats += 1;
        else failuresByTest.set(key, { file: fileSuite.file, title, project: test.projectName, failedRepeats: 1 });
      }
    }
  }

  const failures = [...failuresByTest.values()];
  flaky = Math.max(flaky, report.stats?.flaky ?? 0);
  const ok = failures.length === 0 && flaky === 0;
  const stats = report.stats ?? {};

  const lines = [
    '### Nightly L4 (full, both render modes, --repeat-each 3, four shards)',
    '',
    `Test runs: ${total}; passed ${stats['expected'] ?? 0}, failed ${stats['unexpected'] ?? failures.length}, skipped ${stats['skipped'] ?? 0}.`,
    `Flaky tests: ${flaky}${flaky === 0 ? ' (retries are 0 and --fail-on-flaky-tests is set: a flaky result cannot occur unless retries were turned on)' : ''}`,
    `Quarantined (\`@quarantine\`): ${quarantined.size}`,
    '',
  ];
  if (failures.length > 0) {
    lines.push('**Failures**', '');
    for (const failure of failures) {
      lines.push(`- \`${failure.file}\` › ${failure.title} (${failure.project}, failed in ${failure.failedRepeats} of the repeats)`);
    }
    lines.push('');
  }
  return { ok, failures, flaky, quarantined: quarantined.size, markdown: lines.join('\n') };
}

function main() {
  const { ok, markdown } = summarizeNightly(JSON.parse(readFileSync(process.argv[2] ?? 'merged-report.json', 'utf8')));
  if (process.env['GITHUB_STEP_SUMMARY']) appendFileSync(process.env['GITHUB_STEP_SUMMARY'], markdown + '\n');
  process.stdout.write(markdown + '\n');
  process.exit(ok ? 0 : 1);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
