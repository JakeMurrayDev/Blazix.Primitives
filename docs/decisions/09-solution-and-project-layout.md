# ADR 09 — Solution and project layout

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | the repository tree; the entry and contract files that make it navigable; the projects (library, source generator, analyzers, one test project per layer, fixture host); where the Node toolchain and the TypeScript sources live; whether a demo application exists |
| Affects | no requirement IDs; ADR 04 §4 (placement of the JavaScript sources, refined), ADR 06 §1 (the L4 runner, amended the same day), ADR 10 (what is packed) |

## Context

ADR 06 needs four runners: xUnit without Blazor (L1), bUnit (L2), vitest (L3) and Playwright against one fixture host that serves both render modes from the same fixture components (L4). A Blazor Web App that renders WebAssembly components needs those components in a separate client assembly, so the fixture host is two projects. ADR 04 puts the TypeScript sources inside the library project and runs esbuild from an MSBuild target; ADR 05 needs a source generator for `[PartState]` projection, and that generator must ship to consumers (their own `[PartState]` records behind `RenderElement<TState>`), while the convention analyzers of ADR 08 §5 must not. `reference/` is ignored (ADR 07), so the build cannot import Floating UI from `reference/vendor`; it has to be an npm dependency pinned to exactly 1.8.0, which resolves core 1.8.0 and utils 0.2.12, the vendored versions. The old port had a demo app and a documentation site beside the library, both to be kept green.

The repository is organised under the Interpretable Context Methodology (ICM): a short routing entry file at the root, one contract file per working folder, reference material apart from working artefacts, state derivable from the file system. The root `CONTEXT.md` is already the glossary of the domain-modelling method; ICM's per-folder contracts use the same file name. The same session amended ADR 06 to run L4 under the Node test runner (`@playwright/test`), which moves the L4 tests out of a C# project.

## Decision

### 1. The tree

```
Blazix.Primitives/
  AGENTS.md                      ICM entry file: where everything lives, where to go per task (at most ~60 lines); routes, holds no content
  CLAUDE.md                      contains only "@AGENTS.md"
  CONTEXT.md                     the glossary (unchanged); AGENTS.md states that the root file is the glossary and that folder-level CONTEXT.md files are contracts
  README.md  LICENSE  THIRD-PARTY-NOTICES.md
  Blazix.Primitives.slnx  Directory.Build.props  Directory.Packages.props  global.json  .editorconfig  .node-version
  package.json                   one Node toolchain for the repository: typescript, esbuild, vitest, @playwright/test, @floating-ui/dom@1.8.0 (exact)
  playwright.config.ts           the L4 projects (ADR 06 Amendments 2026-10-20)
  docs/                          the specification, unchanged (ADR 07 decision 5)
  src/
    Blazix.Primitives/           the library: one folder per family (ADR 08 §1), Rendering/, Store/, Events/, Interop/; js/<module>.ts with js/<module>.test.ts beside it (L3); wwwroot/js/ is esbuild output and is ignored; CONTEXT.md
    Blazix.Primitives.Generators/  the [PartState] projection generator and consumer-facing diagnostics; packed into the NuGet package as an analyzer asset
    Blazix.Primitives.Analyzers/   library-internal convention analyzers (member order, field prefix, boxing); never packed
  tests/
    Blazix.Primitives.Tests/              L1 xUnit; references the library only, never bUnit, so the layer rule is enforced by the reference graph
    Blazix.Primitives.Tests.Components/   L2 bUnit, including the twin contract tests
    Blazix.Primitives.Tests.Generators/   generator and analyzer tests (Roslyn testing SDK)
    Blazix.Primitives.Fixtures/           L4 host: a Blazor Web App; the render mode is a route segment resolved to an IComponentRenderMode expression; prerendering off except on the dedicated prerender fixtures; test-only endpoints (readiness, TimeProvider control, captured diagnostics)
    Blazix.Primitives.Fixtures.Client/    the fixture components (WebAssembly-capable), referenced by the host
    browser/                              L4 TypeScript specs under @playwright/test, one file per family (<family>.spec.ts) plus the shared suites
  spikes/                        verification probes (ADR 14), excluded from the solution, deleted when their item closes
```

### 2. Entry and contract files

`AGENTS.md` answers where things live and where to go for a task, in a routing table, and never holds content; `CLAUDE.md` imports it. Each `src/*` and `tests/*` folder gets a `CONTEXT.md` contract (what it reads, what it does, what it writes, what a human checks) when the folder is created in M0. Family folders get none: their contract is the component document, and `AGENTS.md` states the rule `src/Blazix.Primitives/<Family>/ ↔ docs/components/<family>.md ↔ tests/browser/<family>.spec.ts`. Generated indexes, if any, are rebuilt by script and never hand-edited.

### 3. Projects

One test project per layer, so that L1's "no Blazor" and L2's bUnit dependency are enforced by project references and CI can run L1–L3 without building the fixture host. The generator and the analyzers are separate projects because they ship to different audiences. There is no demo application before the first release: the fixture host is the browsable showcase, every fixture page doubling as a demo. The solution file is `.slnx`; `Directory.Packages.props` holds central package versions; `Directory.Build.props` turns on nullable, warnings as errors, `IsTrimmable` and `IsAotCompatible` for the library (ADR 16).

### 4. Toolchain placement

One `package.json` at the repository root owns the Node toolchain (one `node_modules`, one lockfile); the TypeScript sources stay inside the library project per ADR 04 §4 and the esbuild target resolves the root `node_modules`. `.node-version` pins the active Node LTS line. The vitest configuration points at `src/Blazix.Primitives/js/**/*.test.ts`.

## Consequences

* ADR 06 §1's L4 row and §2's fixture-host bullets are amended (L4 under `@playwright/test`); the C# browser test project that ADR 06 implied does not exist.
* The fixture host needs a readiness endpoint and test-only endpoints for `TimeProvider` control and diagnostic capture, since the tests run in another process.
* `AGENTS.md` and `CLAUDE.md` exist from the first commit; the folder contracts arrive with M0.

## Verification items

* A Blazor Web App can select the interactive render mode per route from an `IComponentRenderMode` expression with prerendering disabled, while serving the WebAssembly client's static assets from the same host (M0, the first fixture). **Verified 2026-10-20:** `tests/Blazix.Primitives.Fixtures/Components/Pages/FixturePage.razor` (`/{mode}/{fixture}`, `RenderTreeBuilder.AddComponentRenderMode` with `new InteractiveServerRenderMode(prerender)` / `new InteractiveWebAssemblyRenderMode(prerender)` from the catalog entry) and `tests/browser/harness.spec.ts` "@verifies ADR-09/1 the route segment selects the render mode and the fixture renders without prerendering" (`data-renderer` reads `Server` or `WebAssembly`; the static response carries no fixture markup). Two facts for the next fixtures: the client project's `wwwroot` is served at the site root, not under `_content/`, and the library must reference the `Microsoft.AspNetCore.Components.Web` package rather than the `Microsoft.AspNetCore.App` framework for the client to reference it (NETSDK1082). [issue 18](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/18).
* Playwright's `webServer` starts the fixture host with `dotnet run` and its readiness URL check works with the host's readiness endpoint (M0). **Verified 2026-10-20:** `playwright.config.ts` (`dotnet run --project tests/Blazix.Primitives.Fixtures`, `url: /_fixture/ready`, `reuseExistingServer` outside CI) and `tests/browser/harness.spec.ts` "@verifies ADR-09/2 webServer started the host through dotnet run and its readiness endpoint answers". [issue 19](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/19).

## Considered options

* One test project with traits (faster to create; the layer rule is no longer enforced by references and L1–L3 cannot run without compiling L4).
* One Roslyn project for generator and analyzers (the convention analyzers would ship to consumers or need per-rule packing tricks).
* A demo app from day one (a third application to keep green while the API is still moving).
* `package.json` inside the library project (a second `node_modules` for the L3 and L4 tooling).

## Amendments

**2026-10-20, time control and diagnostics through a page hook (M0 Track 3; chosen by the user the same day).** §1's fixture-host row and the Consequences read: Kestrel consumes `TimeProvider` as a singleton (a scoped registration fails host validation), so the host registers one `HarnessTimeProvider` that routes to the circuit's `FakeTimeProvider` by the renderer's synchronization context; a browser test advances time through `window.blazixFixture.advanceTime`, which invokes a `[JSInvokable]` method on the page's own session, on both render modes, so timers fire on the renderer's dispatcher and control is scoped to the page, which is what parallel Playwright workers against one host process need. Captured diagnostics are read through `/_fixture/diagnostics/{token}` on Server and through the page hook on WebAssembly; `/_fixture/ready` is the readiness URL for `webServer`. The "test-only endpoints for `TimeProvider` control" of §1 and the Consequences are therefore the page hook plus the diagnostics endpoint; ADR 06 §2's clock rule is unchanged.
