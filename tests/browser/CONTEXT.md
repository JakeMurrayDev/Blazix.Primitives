# tests/browser

L4: TypeScript specs under `@playwright/test`, one file per family plus the shared suites and the harness's own specs. There is no C# browser test project.

## Inputs

- The fixture host (`../Blazix.Primitives.Fixtures`), started by Playwright's `webServer` with `dotnet run` and polled on `/_fixture/ready`.
- The `Test:` lines tagged `[L4]` in `docs/components/*.md`; decision records 06 §2 (browser harness), 09 §1, 11 (CI) and 14 §3 (`@verifies` tags).
- `playwright.config.ts` at the repository root: projects `server`, `wasm-warmup` (a setup project the `wasm` project depends on) and `wasm`; `retries: 0`, `failOnFlakyTests`; `expect` timeouts as ceilings (WebAssembly larger); the blob reporter in CI.

## Job

Runs every cross-boundary requirement once per render mode. `helpers/`:
- `test.ts`: the `mode` option (`server` | `wasm`), one browser context per worker (pages share its HTTP cache), `page.clock.install()` on every page, running (auto-advancing), so SignalR keep-alive flows on `server` (ADR-06/2).
- `navigation.ts`: `gotoFixture(page, mode, name)` waits for `html[data-blazix-started]` and `[data-fixture][data-interactive="true"]`, never `networkidle`, never a sleep; returns the container locator.
- `session.ts`: `advanceTime(page, container, ms)` moves the page's C# `TimeProvider`; `readDiagnostics(page, mode, container)` reads the captured log entries (endpoint on `server`, page hook on `wasm`).

Specs: `smoke.spec.ts` (the probe twin, the harness canary), `harness.spec.ts` (the M0 verification items, `@verifies ADR-NN/n` in the title), `wasm-warmup.setup.ts`, then `<family>.spec.ts` from M1.

## Outputs

- Playwright reports, traces and blob reports under `playwright-report/`, `test-results/` and `blob-report/` (ignored by git).
- Run by the `browser-server` and `browser-wasm` CI jobs: on a pull request a change confined to one family's folder or spec files runs that family's `<family>.spec.ts` files in both modes, a shared path or the label `full-l4` runs everything, and documents-only changes run none (`.github/scripts/classify-paths.mjs`, rule in `.github/CONTEXT.md`); on `main` everything over four shards; nightly everything with `--repeat-each 3` over four shards. Two shards on a pull request; the blob reports are merged into one HTML report artifact.
- The `red-check` job lists the tests here on the merge base and on the head (`playwright test --list --reporter=json`), and runs the spec files with added tests against the base's `src/` with these specs and the fixture host; a test that passes there fails the pull request. Tests added in a spec must fail against the base mode by mode.

## Human check

- Each title keeps Base UI's wording and its `@verifies` tag where a verification item applies.
- Gesture tests use real Playwright mouse and touch input, not `dispatchEvent`.
- `retries` stays 0; a flaky test is quarantined with `test.fixme`, `@quarantine` and an issue, for at most 14 days. The nightly summary counts the quarantined specs and reports any flaky result (there can be none while `retries` is 0).
- A test that needs frozen time pauses the clock itself; nothing waits on a timeout.
