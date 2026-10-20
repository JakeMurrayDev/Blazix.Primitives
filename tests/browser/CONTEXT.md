# tests/browser

L4: TypeScript specs under `@playwright/test`, one file per family plus the shared suites. There is no C# browser test project.

## Inputs

- The fixture host (`../Blazix.Primitives.Fixtures`), started by Playwright's `webServer` with `dotnet run`.
- The `Test:` lines tagged `[L4]` in `docs/components/*.md`; decision records 06 §2 (browser harness), 09 §1 and 11 (CI).
- `playwright.config.ts` at the repository root (the L4 projects, one per render mode), added by Track 3.

## Job

Runs every cross-boundary requirement once per render mode: keyboard, focus, dismissal, pointer gestures, positioning with pixel assertions, animations. A spec is `<family>.spec.ts`, mapped to `src/Blazix.Primitives/<Family>/` and `docs/components/<family>.md`. Readiness is a marker, never a timeout: no `networkidle`, no fixed sleeps. Browser timers run under `page.clock`.

## Outputs

- Playwright reports, traces and blob reports under `playwright-report/`, `test-results/` and `blob-report/` (ignored by git).
- Run by the browser jobs of CI per render mode; a scoped run covers one family, a shared change runs everything (ADR 11 §1).

## Human check

- Each title keeps Base UI's wording and its `@verifies` tag where a verification item applies.
- Gesture tests use real Playwright mouse and touch input, not `dispatchEvent`.
- `retries` stays 0; a flaky test is quarantined with `test.fixme`, `@quarantine` and an issue, for at most 14 days.
