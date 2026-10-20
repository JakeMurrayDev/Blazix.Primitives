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

Specs: `smoke.spec.ts` (the probe twin, the harness canary, and the button twin), `button-behavior.spec.ts` (the UBT rows that need only the `button` module; the rest arrive with Button and Toggle in M1), `dom.spec.ts` (the `dom` helpers' browser-only Base UI tests, importing the shipped module in the page), `harness.spec.ts` (the M0 verification items, `@verifies ADR-NN/n` in the title), `wasm-warmup.setup.ts`, then `<family>.spec.ts` from M1.

## Outputs

- Playwright reports, traces and blob reports under `playwright-report/`, `test-results/` and `blob-report/` (ignored by git).
- Run by the `browser-server` and `browser-wasm` CI jobs, two shards each; the blob reports are merged into one HTML report artifact.

## Human check

- Each title keeps Base UI's wording and its `@verifies` tag where a verification item applies.
- Gesture tests use real Playwright mouse and touch input, not `dispatchEvent`.
- `retries` stays 0; a flaky test is quarantined with `test.fixme`, `@quarantine` and an issue, for at most 14 days.
- A test that needs frozen time pauses the clock itself; nothing waits on a timeout.
