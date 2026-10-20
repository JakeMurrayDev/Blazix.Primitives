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
| `nightly.yml` | 02:17 UTC daily on `main`; manual | `browser-server`, `browser-wasm` (full L4, `--repeat-each 3`, 4 shards each), `report` (merged report and the summary) |
| `skip-js-build.yml` | manual (run before every tagged release) | `build-without-node` |

| Job | What it does |
|---|---|
| `changes` | runs `scripts/classify-paths.mjs`: `docs` (a path under `docs/`), `code` (a path outside it), `l4` (`none`, `scoped`, `full`), `run-l4`, and the `specs` of a scoped run |
| `docs-lint` | `docs/_tools/check_docs.py` against `docs-lint.baseline.txt` (the seven accepted React-term mentions); only when `docs` |
| `build-and-test` | the `src/` lint, restore, build, L1, L2 and the generator tests, the ownership lint printed alone, then `npm run test:js` (L3, the module-graph lint, and the unit tests of the scripts); only when `code` |
| `browser-server`, `browser-wasm` | L4 for one render mode: 2 shards on a pull request, 4 on `main`; a scoped run names the spec files, a full run names none; only when `run-l4` |
| `browser-report` | merges the shards' blob reports into one HTML report artifact |
| `red-check` | `scripts/red-check.mjs` (ADR 11 §3): lists the tests of the merge base and of the head at each layer, runs the added ones against the base's `src/` with the head's `tests/`, fails when one passes; a pull request that adds no test passes at once. An added test that cannot run there is red, not "did not run": a .NET project or the fixture host that does not build against the base, and a vitest file whose import of a module the pull request adds does not resolve (vitest reports the file as a failed suite with no per-test results; the summary names the file). A test that produced no result for another reason is reported as not run and fails the job |
| `pack` | `dotnet pack` with symbols, `scripts/assert-package.mjs` on the `.nupkg`, then uploads the artifacts `packages` (`.nupkg`, `.snupkg`) and `js-outputs` (`wwwroot/`, for `skip-js-build.yml`) |
| `gate` | the one required check: fails when any job failed or was cancelled; a job skipped by its `if` counts as passed |
| nightly `report` | merges the blob reports to HTML and JSON and writes the summary: failures per project, the flaky count (a guard: retries are 0, so it stays 0), the `@quarantine` count |
| `build-without-node` | downloads `js-outputs` of the latest successful `main` run, takes node off `PATH` (and asserts it), packs with `-p:BlazixSkipJsBuild=true`, asserts the package, and checks that a missing output still fails with its message |

### Scoped L4 (the rule `classify-paths.mjs` applies)

- A push to `main`, the label `full-l4`, any shared path or any path nobody classified: **full**. Shared: `src/Blazix.Primitives/{Rendering,Store,Events,Interop,js}/`, the library's root files, the generators and analyzers, `tests/Blazix.Primitives.Fixtures*`, `tests/browser/helpers/`, `tests/browser/wasm-warmup.setup.ts`, the root configuration, `.github/`.
- Only family paths: **scoped** to `tests/browser/<family>.spec.ts` and `<family>.*.spec.ts` of those families, in both modes. A family is a stem of `docs/components/*.md`; its folder is the PascalCase of the stem (`alert-dialog` → `AlertDialog`); a spec belongs to the family named by the part of its file name before the first dot. A harness spec (`harness.spec.ts`, `smoke.spec.ts`) scopes to itself.
- Only `docs/`, Markdown, `LICENSE`, and the three C# unit-test projects (`Tests`, `Tests.Components`, `Tests.Generators`): **none** (docs-only runs `docs-lint` alone; the others run `build-and-test` too).
- Because `tests/Blazix.Primitives.Fixtures.Client/FixtureCatalog.cs` is one file every fixture is registered in, a family's pull request that adds a fixture is full L4 until the catalog is split.

### Scripts (`scripts/`, Node, no dependencies; each has a `*.test.mjs` run by `npm run test:js`)

| Script | Role |
|---|---|
| `classify-paths.mjs` | the rule above; writes `docs`, `code`, `l4`, `run-l4`, `families`, `specs` to `$GITHUB_OUTPUT` and a table to the step summary |
| `red-check.mjs` | the red check; the pure half (listings, added names, outcomes, verdict, summary) is tested, the driver builds and runs the tools |
| `nightly-summary.mjs` | the nightly summary from the merged Playwright JSON report |
| `assert-package.mjs` | the package holds `LICENSE`, `THIRD-PARTY-NOTICES.md`, the library, the generator under `analyzers/dotnet/cs` and the JavaScript under `staticwebassets`, and not the analyzers assembly |
| `src-lint.mjs` | `git grep -P` over the `.cs` files under `src/`: no specification reference (ADR numbers, requirement IDs, `§`, `docs/`, "decision record", "glossary") in a `///` line or a string literal, no "a value that indicates whether", no UK spelling in a `///` line. `//` comments and `CONTEXT.md` files are exempt. Local: `node .github/scripts/src-lint.mjs` |

The red check leaves out lint tests, which read the repository and are green on the base by construction: C# tests in a `Lints` namespace and TypeScript tests in a `*.lint.test.ts` file (the ownership lint and the module-graph lint). A lint shows its red run with a planted violation, quoted in the pull request.

### Caches and runner

`ubuntu-latest`; the SDK from `global.json`, Node from `.node-version` (`.github/actions/setup`). Cache keys: NuGet on `global.json`, `Directory.Packages.props`, `package-lock.json` and every `*.csproj`; npm on `package-lock.json` (`setup-node`); the Playwright browser on its version, `global.json` and `package-lock.json`.

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
