# .github

The continuous integration of the repository: workflows, the composite actions they share, and the scripts that decide, check and summarize. Decision record 11 is the contract; this file says how it is built.

## Inputs

- `docs/decisions/11-continuous-integration.md` (triggers, runner shape, red check, flakiness, lints), `10-packaging-and-versioning.md` §1 and §5 (what the package contains, the artifact on `main`), `04-js-interop-boundary.md` §4 and its first verification item (`BlazixSkipJsBuild`), `06-testing-harness.md` §1 (the red rule), `14-verification-plan.md` §2 (issues 22, 23).
- `global.json` (SDK), `.node-version` (Node), `package-lock.json`, `Directory.Packages.props` (the cache keys), `playwright.config.ts` (projects, `retries: 0`, `failOnFlakyTests`).
- The pull request's changed paths, its labels, and for the red check its merge base with the base branch.

## Job

### Workflows

| Workflow | Trigger | Jobs |
|---|---|---|
| `ci.yml` | every pull request (also on `labeled` and `unlabeled`); push to `main` | `changes`, `docs-lint`, `build-and-test`, `browser-server`, `browser-wasm`, `browser-report`, `red-check`, `pack` (push only), `gate` |
| `nightly.yml` | 02:17 UTC daily on `main`; manual | `browser-server`, `browser-wasm` (full L4, `--repeat-each 3`, 4 shards each, each shard's disk report), `report` (merged report and the summary) |
| `skip-js-build.yml` | manual (run before every tagged release) | `build-without-node` |

| Job | What it does |
|---|---|
| `changes` | runs `scripts/classify-paths.mjs`: `docs` (a path under `docs/`), `code` (a path outside it), `l4` (`none`, `scoped`, `full`), `run-l4`, and the `specs` of a scoped run |
| `docs-lint` | `docs/_tools/check_docs.py` against `docs-lint.baseline.txt` (the seven accepted React-term mentions); only when `docs` |
| `build-and-test` | the `src/` lint, restore, build, one `dotnet build` of the sample application (`samples/Blazix.Primitives.Demo`, no tests; its client project runs the Tailwind CLI from the root `node_modules`), L1, L2 and the generator tests without the allocation-budget classes, then those classes one process each (`scripts/allocation-budgets.mjs`; ADR 16 and ADR 11 Amendments 2026-11-11, issue 216), the ownership lint printed alone, `npm run typecheck` (`tsc -p tsconfig.json --noEmit` over the modules, the vitest files and the browser specs), then `npm run test:js` (L3, the module-graph lint, and the unit tests of the scripts); only when `code` |
| `browser-server`, `browser-wasm` | L4 for one render mode: `browser-server` has 2 shards on a pull request and 4 on `main`, `browser-wasm` 4 on both (ADR 11 Amendments 2026-11-04, issue 158: at 2 shards the gate's WebAssembly contexts ran out of disk at their close); a scoped run names the spec files, a full run names none; only when `run-l4`. The gate's traces keep their DOM snapshots. Around the action, each `browser-wasm` shard runs `actions/disk-sampler` as the nightly's shards do (`stage: start`, then `stage: report` under `if: always()`), so its step summary shows the free space before the setup, the lowest free space and the peak of `test-results/` |
| `browser-report` | merges the shards' blob reports into one HTML report artifact |
| `red-check` | `scripts/red-check.mjs` (ADR 11 §3): lists the tests of the merge base and of the head at each layer, runs the added ones against the base's `src/` with the head's `tests/`, fails when one passes; a pull request that adds no test passes at once. An added test that cannot run there is red, not "did not run": a .NET project or the fixture host that does not build against the base, and a vitest file whose import of a module the pull request adds does not resolve (vitest reports the file as a failed suite with no per-test results; the summary names the file). A test that produced no result for another reason is reported as not run and fails the job |
| `pack` | `dotnet pack` with symbols, `scripts/assert-package.mjs` on the `.nupkg` with the build's inputs manifest (`--inputs obj/Release/net11.0/blazix-js.inputs.json`), then uploads the artifacts `packages` (`.nupkg`, `.snupkg`) and `js-outputs` (`wwwroot/`, for `skip-js-build.yml`) |
| `gate` | the one required check: fails when any job failed or was cancelled; a job skipped by its `if` counts as passed |
| nightly `browser-server`, `browser-wasm` | the composite action with `repeat-each: 3`, traced as the gate is (`retain-on-failure` with DOM snapshots): `tests/browser/helpers/test.ts` gives each test a trace of its own on the worker's shared context, so the volume grows with the tests, not with their square (issue 158, ADR 11 Amendments 2026-11-04). Around the action, `actions/disk-sampler` (`stage: start`, then `stage: report` under `if: always()`) records the free disk and the size of `test-results/` every 15 s, and the step summary reports the free space before the setup, the lowest free space and the peak |
| nightly `report` | merges the blob reports to HTML and JSON and writes the summary: failures per project, the flaky count (a guard: retries are 0, so it stays 0), the `@quarantine` count |
| `build-without-node` | downloads `js-outputs` of the latest successful `main` run, takes node off `PATH` (and asserts it), packs with `-p:BlazixSkipJsBuild=true`, asserts the package, and checks that a missing output still fails with its message |

### Scoped L4 (the rule `classify-paths.mjs` applies)

- A push to `main`, the label `full-l4`, any shared path or any path nobody classified: **full**. Shared: `src/Blazix.Primitives/{Rendering,Store,Events,Interop,js}/`, the library's root files, the generators and analyzers, `tests/Blazix.Primitives.Fixtures*`, `tests/browser/helpers/`, `tests/browser/wasm-warmup.setup.ts`, the root configuration, `.github/`.
- Only family paths: **scoped** to `tests/browser/<family>.spec.ts` and `<family>.*.spec.ts` of those families, in both modes. A family is a stem of `docs/components/*.md`; its folder is the PascalCase of the stem (`alert-dialog` → `AlertDialog`); a spec belongs to the family named by the part of its file name before the first dot. A harness spec (`harness.spec.ts`, `smoke.spec.ts`) scopes to itself.
- Only `docs/`, Markdown, `LICENSE`, `samples/` (the sample application: `build-and-test` builds it, no spec visits it), and the three C# unit-test projects (`Tests`, `Tests.Components`, `Tests.Generators`): **none** (docs-only runs `docs-lint` alone; the others run `build-and-test` too).
- A family whose parts are shells over another family's behaviours runs with it: a change under `src/Blazix.Primitives/Dialog/` also runs `alert-dialog`'s specs (the dependents list in `classify-paths.mjs`; design point 1 of M4 track 9, settled by the orchestrator 2026-10-27). The dependence runs one way: an AlertDialog-only change scopes to `alert-dialog` alone. Drawer joins the list in M6.
- Because `tests/Blazix.Primitives.Fixtures.Client/FixtureCatalog.cs` is one file every fixture is registered in, a family's pull request that adds a fixture is full L4 until the catalog is split.

### Scripts (`scripts/`, Node, no dependencies; each has a `*.test.mjs` run by `npm run test:js`)

| Script | Role |
|---|---|
| `classify-paths.mjs` | the rule above; writes `docs`, `code`, `l4`, `run-l4`, `families`, `specs` to `$GITHUB_OUTPUT` and a table to the step summary |
| `red-check.mjs` | the red check; the pure half (listings, added names, outcomes, verdict, summary) is tested, the driver builds and runs the tools |
| `nightly-summary.mjs` | the nightly summary from the merged Playwright JSON report |
| `assert-package.mjs` | the package holds `LICENSE`, `THIRD-PARTY-NOTICES.md`, the library, the generator under `analyzers/dotnet/cs` and the JavaScript under `staticwebassets`, and not the analyzers assembly; the packed output that bundles the engine (the one whose inputs in the build's `blazix-js.inputs.json` include `js/floating-ui.ts`: a shared chunk once a second entry imports `positioning`, `js/positioning.js` without the manifest) carries the `/*! … */` Floating UI legal comment of `js/floating-ui.ts` naming the versions `package.json` pins, MIT and the copyright (ADR 07 Amendments 2026-10-27 and 2026-10-28, `@verifies ADR-07/1`, [issue 16](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/16)). The `pack` job runs on `push` only, so a pull request that changes the banner quotes a local `dotnet pack src/Blazix.Primitives -c Release` followed by the script |
| `allocation-budgets.mjs` | the classes of the `AllocationBudgets` collection of `tests/Blazix.Primitives.Tests` and `tests/Blazix.Primitives.Tests.Components` (a class declared with `[Collection(AllocationBudgetsCollection.Name)]`, or a Razor test component whose `@attribute` directive carries it), each run by its own `dotnet test --filter-class` process in Release; a class whose filter runs no test fails. `--exclusions <project>` prints the `--filter-not-class` arguments `build-and-test`'s main run of that project passes. Local, after a Release build: `node .github/scripts/allocation-budgets.mjs` |
| `src-lint.mjs` | `git grep -P` over the `.cs` files under `src/`: no specification reference (ADR numbers, requirement IDs, `§`, `docs/`, "decision record", "glossary") in a `///` line or a string literal, no "a value that indicates whether", no UK spelling in a `///` line. `//` comments and `CONTEXT.md` files are exempt. Local: `node .github/scripts/src-lint.mjs` |

The red check leaves out lint tests, which read the repository and are green on the base by construction: C# tests in a `Lints` namespace and TypeScript tests in a `*.lint.test.ts` file (the ownership lint and the module-graph lint). A lint shows its red run with a planted violation, quoted in the pull request.

### Caches and runner

`ubuntu-latest`; the SDK from `global.json`, Node from `.node-version` (`.github/actions/setup`). Cache keys: NuGet on `global.json`, `Directory.Packages.props`, `package-lock.json` and every `*.csproj`; npm on `package-lock.json` (`setup-node`); the Playwright browser on its version, `global.json` and `package-lock.json`.

The browser's system libraries (2026-11-12): on a cache miss the setup downloads Chromium alone (`npx playwright install chromium`, without `--with-deps`). Then, on a hit or a miss, it runs `npx playwright install-deps chromium`, three attempts 15 s and 30 s apart. Each attempt is bounded at 120 s by `timeout`, which runs as root so it can kill apt (a healthy run takes 11–34 s), so the worst case is about 7 minutes. If apt still fails, the step emits a warning instead of failing the job, and the job relies on the runner image's preinstalled libraries. The trigger was an outage in which the Ubuntu mirrors failed to resolve from the runners for about 2.5 hours. Every `browser-*` and `red-check` job died in apt with exit 100, and an unbounded attempt then outlasted the job's 30-minute limit, because each failed lookup took about 40 s. A library the image lacks still fails the job, at the browser's launch, with Playwright's missing-dependency error naming it.

### Required check

Branch protection is a repository setting, not a file. Require the check **`gate`**: it collects `docs-lint`, `build-and-test`, `browser-server`, `browser-wasm` and `red-check` (and the report and pack jobs), so it holds the five checks of ADR 11 §2 and §3 without naming each matrix shard.

## Outputs

- Check results on pull requests; the `playwright-report`, `packages` and `js-outputs` artifacts of `main`; the nightly report.
- `docs-lint.baseline.txt` (the accepted lint output; the first line's counts are masked).

## Human check

- A change to a script comes with its test, red first (ADR 11 §3).
- A new `docs/components` document is a new family: nothing here changes. A new top-level folder under `src/Blazix.Primitives/` that is not a family is full L4 until `classify-paths.mjs` names it.
- Actions are pinned by major version; a third-party action is pinned by commit SHA.
- `retries` stay 0; a quarantined test carries `test.fixme`, `@quarantine` and an issue, and is counted in the nightly summary.
