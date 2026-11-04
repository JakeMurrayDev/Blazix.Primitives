# ADR 11 — Continuous integration

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | which test layers run on which triggers; the runner shape; how the "a test fails with its fix reverted" rule of ADR 06 is enforced; the flakiness policy; the lints of the documents, the ownership list and the module graph |
| Affects | ADR 06 §1 (the red rule, now mechanised) and §2 (timeouts as ceilings); ADR 04 §1 (module import rule) and §2 (ownership list, now linted); no requirement IDs |

## Context

The repository is private until the first release (ADR 07) and private repositories have a monthly GitHub Actions allowance (2,000 minutes on Free, 3,000 on Pro; Linux at 1×, macOS at 10×). A full L4 pass is about 1,736 tagged titles per render mode plus parameterised variants; L1–L3 take minutes. ADR 06 requires every new test to be shown failing with its fix reverted, names no mechanism, and says L1–L3 run on every change and L4 per render mode without naming the trigger. Both runners can list tests by name (`dotnet test --list-tests`, `npx playwright test --list`, vitest's JSON reporter), so CI can diff the test inventory between base and PR and run only the added tests against the base branch's `src/` with the PR's `tests/`. The Node runner (ADR 06 Amendments 2026-10-20) has `--shard`, `--repeat-each`, `--fail-on-flaky-tests`, `retries` and `test.fixme`. ADR 04 calls the ownership list lint-checkable and the `Writer` column now exists in every component document; ADR 04 §1 forbids a mechanism module importing a component module. The old port's answer to WebAssembly slowness was a 3× timeout multiplier and fixed sleeps, which ADR 06 replaced with readiness markers.

## Decision

### 1. Triggers and layers (option C2)

| Trigger | Runs |
|---|---|
| every pull request | `docs-lint` when `docs/` changed; `build-and-test` (build, L1, L2, L3, the ownership and module-graph lints) when anything outside `docs/` changed; **scoped L4**: a change confined to `src/Blazix.Primitives/<Family>/` or `tests/browser/<family>*` runs that family's specs in both modes; any change under `Rendering/`, `Store/`, `Events/`, `Interop/`, `js/` or the fixture host runs full L4; the label `full-l4` forces full L4 |
| push to `main` | everything above plus full L4 in both modes, sharded; `dotnet pack` output attached as an artifact (ADR 10) |
| nightly on `main` | full L4 in both modes with `--repeat-each 3`; the report lists failures and the quarantine count |
| manual | the `BlazixSkipJsBuild=true` build with prebuilt outputs (ADR 04 verification item), run before every tagged release: `.github/workflows/skip-js-build.yml` ([issue 9](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/9)) |

Docs-only pull requests run only `docs-lint`.

### 2. Runner shape

`ubuntu-latest`; the SDK from `global.json` (`setup-dotnet` with `global-json-file`), Node from `.node-version`; caches for NuGet, npm and Playwright browsers; jobs `docs-lint`, `build-and-test`, `browser-server` and `browser-wasm` (each a 2-shard matrix on pull requests, 4 on `main`), all required checks on pull requests; blob reports merged into one HTML report per run.

### 3. Red check (the rule of ADR 06 §1, option F-B)

A job on every pull request lists the tests on the base branch and on the PR at each layer, takes the added names, checks out the base branch's `src/` while keeping the PR's `tests/` and fixtures, runs the added tests at their layer, and fails the PR if any of them passes. Built in M0; until it exists, the rule is enforced by process: the test commit precedes the fix commit and the PR description quotes the red run.

### 4. Flakiness

The gate runs with `retries: 0`; `--fail-on-flaky-tests` is set so that enabling retries anywhere still fails the gate. An intermittent test is quarantined with `test.fixme` and the tag `@quarantine`, linked to an issue, excluded from the gate for at most 14 days and counted in the nightly report; the nightly `--repeat-each 3` run is the flake hunt. Per-project `expect` timeouts (larger for WebAssembly) are ceilings, never waits, per ADR 06 §2.

### 5. Lints

* **Documents:** `docs/_tools/check_docs.py` on any pull request touching `docs/`; the seven accepted React-term mentions are its allowlist.
* **Ownership:** an L1 xUnit test parses every component document's `Writer` column and compares it with the code's ownership registry (the custom projection mappings and the twin option records): a `JS` or `C# → JS` row without a registry entry, or a registry entry whose document row says `C#`, fails the build.
* **Module graph:** an L3 vitest test over the import graph: mechanism modules import no component module; component modules import only mechanism modules and the vendored Floating UI.
* **TypeScript types** (added 2026-10-21, see Amendments): `npm run typecheck` in the `build-and-test` job.

## Consequences

* A pull request that touches one family and nothing shared costs a few runner-minutes; a shared change costs a full L4 pass, which is the point.
* The ownership lint makes the documents a build input: the test project reads `docs/components/*.md` at test time.
* The red-check job doubles the scoped L4 cost of a PR that adds browser tests.

## Verification items

* The path-to-spec mapping of the scoped L4 run classifies a PR that touches both a family and a shared folder as full L4 (test with a synthetic PR in M0). **Verified 2026-10-20:** `.github/scripts/classify-paths.test.mjs` "a family folder and a shared folder together are full L4 (the synthetic pull request of issue 22)", and the synthetic pull request [#47](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/47), which touches `src/Blazix.Primitives/Button/` and `src/Blazix.Primitives/Rendering/`: the `changes` job logged `l4=full` with `families=button` and the shared path as the reason, and both browser jobs ran every spec ([run](https://github.com/JakeMurrayDev/Blazix.Primitives/actions/runs/39436665099)). [issue 22](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/22).
* `--fail-on-flaky-tests` with `retries: 0` behaves as intended (no test can be reported flaky, so the flag is a guard only); confirm on the first nightly run. **Verified 2026-10-20 by the first nightly run on `main` (dispatched at 5c5b76d, run 39442128653, success: 94 test runs, 0 failed, `Flaky tests: 0`, `Quarantined: 0`; the first scheduled run on 2026-10-21 succeeded as well), so the flag is a guard only; [issue 23](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/23) closed. Mechanism:** `.github/workflows/nightly.yml` runs full L4 in both modes with `--repeat-each 3` over four shards and `.github/scripts/nightly-summary.mjs` prints `Flaky tests: N` and fails on a non-zero count; the pipeline ran once on a pull request before the merge ([#50](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/50), [run](https://github.com/JakeMurrayDev/Blazix.Primitives/actions/runs/39436377880): 94 test runs, failed 0, `Flaky tests: 0`, `Quarantined: 0`). The finding for this item comes from the first scheduled run on `main`. [issue 23](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/23).

## Considered options

* Full L4 on every pull request (tens of minutes per PR and a quota a busy week exhausts while private); L4 nightly only (regressions surface a day late and the implemented gate stops being a PR check).
* Red check by process only (an agent-authored PR can assert a red run it never made, and the reviewer is one person).
* Retries in the gate (hides exactly the timing defects the port exists to get right).
* A Python ownership lint over a build-emitted JSON (a second toolchain for one check); ESLint `no-restricted-imports` for the module graph (works, but the rule then lives outside the suite that documents it).

## Amendments

**2026-10-21, the typecheck is a lint ([issue 70](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/70)).** §5 gains a fourth lint: `npm run typecheck` (`tsc -p tsconfig.json --noEmit` over the TypeScript modules, their vitest files and the browser specs) runs in the `build-and-test` job after the setup's `npm ci`, so `gate` fails on a type error. Vitest and Playwright strip types without checking them, so before this a type error in `tests/browser/avatar.spec.ts` reached `main` with every job green (PR 68).

**2026-10-29, a family's dependents share its scoped L4 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); design point 1, settled by the orchestrator 2026-10-27).** AlertDialog's parts are sealed shells over the behaviours in `src/Blazix.Primitives/Dialog/` (ADR 08 Amendments 2026-10-27), so §1's scoped run of a `Dialog/` change also runs `tests/browser/alert-dialog.spec.ts`. `classify-paths.mjs` keeps a dependents list (`dialog` → `alert-dialog`, with its unit test in `classify-paths.test.mjs`); the dependence runs one way, so an AlertDialog-only change, or a change to `dialog.spec.ts` alone, scopes to that family. Drawer (M6) joins the list. `.github/CONTEXT.md` §Scoped L4 states the rule.

**2026-11-04, the nightly traces without DOM snapshots (M5 track 3, [issue 158](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/158); settled by the orchestrator 2026-11-04 under the track's brief, decision 16 of the M5 plan chosen by the user 2026-11-03).** §1's nightly row and §4's flake hunt are unchanged; only the nightly's traces change.
- *The cause.* Every test of a worker shares one browser context (`tests/browser/helpers/test.ts`, ADR 06 §2's shared HTTP cache). The runner traces each test as a chunk of that context's trace, and for a browser context Playwright 1.64.1 keeps the network log across chunks: each chunk copies the whole cumulative log twice, and nothing deletes the copies until the worker ends. With DOM snapshots, which turn the network recorder on, the volume grows with the square of a worker's test count. Each repeat index is a new worker, and a WebAssembly page load records about 207 `.wasm` requests (0.44 MB of log per test). `retain-on-first-failure` is the same as `retain-on-failure` under `retries: 0`, so it would not have helped.
- *The setting.* `nightly.yml` sets `BLAZIX_TRACE_SNAPSHOTS=false`, which `playwright.config.ts` reads as `trace: { mode: 'retain-on-failure', snapshots: false }`. Without it, as in the gate, the trace stays `'retain-on-failure'`. Every failure still keeps its trace: the actions, the screencast frames, the console, the sources and the error context.
- *The cost.* The nightly's failure traces have no DOM snapshots and no network log. A diagnosis that needs them reproduces the failure locally or reads the gate's trace.
- *The measurement.* Each nightly browser shard reports its disk in the step summary through `.github/actions/disk-sampler`: the free space before the setup, the lowest free space, and the peak of `test-results/`.
  - Locally (WebAssembly, `tabs.spec.ts` and `checkbox.spec.ts`, `--repeat-each=3`, one worker), `test-results/` held 9.8 GB at 210 tests before; after, it peaked at 11.7 MB over all 652 tests.
  - On CI, dispatch 41681848386 (before, the sampler only): every runner started with 87.5 GB free. The WebAssembly shards peaked at 85.01, 79.67, 83.34 and 85.00 GB (two ran out of space at the context's close after every test passed), the Server shards at 5.6–20.7 GB.
  - Dispatch 41685879786 (after): every shard peaked at 0.03–0.07 GB, with 85.1 GB free at the lowest.
- *The gate is not changed.* A PR gate's WebAssembly shard runs about as many tests per worker context as the nightly contexts that reached 85 GB: track 2's run 41685318689 ran 896 tests on two workers in one repeat. The gate has no sampler, so its figure is not measured. The choice between snapshots off on the gate, four WebAssembly shards on pull requests, and per-test tracing in `helpers/test.ts` is the user's.

**2026-11-04, the gate runs four WebAssembly shards on pull requests (chosen by the user 2026-11-04; the gate's share of [issue 158](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/158)'s mechanism).** §2's runner shape changes for one job: `browser-wasm` is a four-shard matrix on pull requests, as on `main`. `browser-server` stays at two shards on pull requests, and the gate's traces keep their DOM snapshots.
- *The failures.* On 2026-11-04 the gate's WebAssembly shards ran every test green and then failed at `browserContext.close: ENOSPC` while copying a `.network` trace file. This happened in PR 167's run 41693686248, in PR 170's run 41715876606 (both shards, on both attempts) and in PR 169's run 41731270044 (`browser-wasm (2)`, 893 passed). `main`'s push runs, at four shards, stayed green.
- *Why four shards.* The amendment above shows that a worker's trace volume grows with the square of its test count. Halving the tests per shard therefore roughly quarters the peak.
- *The cost.* A full-L4 pull-request run gets two more WebAssembly jobs, and each pays the setup. Their minutes count against the private repository's monthly allowance (§Context).
- *The margin is measured (settled by the orchestrator 2026-11-04; the user was told and may overturn).* The gate's WebAssembly shards run `.github/actions/disk-sampler`, as the nightly's do, so the margin left as the suite grows shows in each run's step summary.
- *Not chosen.*
  - Snapshots off on the gate: a one-line change, but the gate's failure traces would lose the DOM snapshots and the network log.
  - Per-test tracing in `tests/browser/helpers/test.ts`: this bounds the volume whatever a shard's size, so it stays open as the lasting fix if the four-shard margin runs out.

**2026-11-04, per-test tracing (chosen by the user 2026-11-04, "Do recommended"; the first small-runner sighting).** The gate's paragraph above left per-test tracing open as the lasting fix. It is now to be built, for two reasons:
- *A small runner.* The sampler's first push run on `main` (run 41740993182 at 82dcdf51) showed `browser-wasm (3)` starting with 18.8 GB free, while the other three shards started with 87.5 GB. Its `test-results/` peaked at 16.42 GB and it ran out of space after all 446 tests passed. Four shards hold only on a full-size runner.
- *The margin left.* The four-shard peak of about 21 GB per shard leaves roughly a doubling of the WebAssembly suite, and wave 2 adds Menu, Select and Combobox.
- *The mechanism is the implementing pull request's*, in `tests/browser/helpers/test.ts` and `playwright.config.ts`. It must keep ADR 06 §2's shared browser context and its HTTP cache. If it cannot, that is a design point for the user. It must bound a worker's trace volume per test rather than letting it grow with the worker's test count, and keep each failure's trace.
- *The nightly.* Whether the nightly's `BLAZIX_TRACE_SNAPSHOTS=false` (the issue 158 paragraph) can then return to full snapshots is decided in that pull request from the sampler's figures.
- *The owner.* Session V, as a follow-up of track 3, in a pull request of its own after the quarantine pull request. Its evidence is the sampler's figures before and after, on the gate and on a nightly dispatch.
