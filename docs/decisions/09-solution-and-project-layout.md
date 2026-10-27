# ADR 09 — Solution and project layout

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | the repository tree; the entry and contract files that make it navigable; the projects (library, source generator, analyzers, one test project per layer, fixture host); where the Node toolchain and the TypeScript sources live; whether a demo application exists |
| Affects | no requirement IDs; ADR 04 §4 (placement of the JavaScript sources, refined), ADR 06 §1 (the L4 runner, amended the same day), ADR 10 (what is packed), ADR 11 §1 (the sample application's path in the classifier, Amendments 2026-10-22) |

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
  package.json                   one Node toolchain for the repository: typescript, esbuild, vitest, @playwright/test, @floating-ui/dom@1.8.0 (exact), tailwindcss and @tailwindcss/cli (the sample's styles, Amendments 2026-10-22)
  playwright.config.ts           the L4 projects (ADR 06 Amendments 2026-10-20)
  docs/                          the specification, unchanged (ADR 07 decision 5)
  src/
    Blazix.Primitives/           the library: one folder per family (ADR 08 §1), Rendering/, Store/, Events/, Interop/, Utilities/ (Amendments 2026-10-21); js/<module>.ts with js/<module>.test.ts beside it (L3); wwwroot/js/ is esbuild output and is ignored; CONTEXT.md
    Blazix.Primitives.Generators/  the [PartState] projection generator and consumer-facing diagnostics; packed into the NuGet package as an analyzer asset
    Blazix.Primitives.Analyzers/   library-internal convention analyzers (member order, field prefix, boxing); never packed
  tests/
    Blazix.Primitives.Tests/              L1 xUnit; references the library only, never bUnit, so the layer rule is enforced by the reference graph
    Blazix.Primitives.Tests.Components/   L2 bUnit, including the twin contract tests
    Blazix.Primitives.Tests.Generators/   generator and analyzer tests (Roslyn testing SDK)
    Blazix.Primitives.Fixtures/           L4 host: a Blazor Web App; the render mode is a route segment resolved to an IComponentRenderMode expression; prerendering off except on the dedicated prerender fixtures; test-only endpoints (readiness, TimeProvider control, captured diagnostics)
    Blazix.Primitives.Fixtures.Client/    the fixture components (WebAssembly-capable), referenced by the host
    browser/                              L4 TypeScript specs under @playwright/test, one file per family (<family>.spec.ts) plus the shared suites
  samples/                       the sample application for trying each milestone's families by hand (Amendments 2026-10-22); never packed; CONTEXT.md
    Blazix.Primitives.Demo/              the host: a Blazor Web App; the render mode is a route segment (/server/<family>, /wasm/<family>), prerendering on
    Blazix.Primitives.Demo.Client/       one demo page per family (WebAssembly-capable), referenced by the host
  spikes/                        verification probes (ADR 14), excluded from the solution, deleted when their item closes
```

### 2. Entry and contract files

`AGENTS.md` answers where things live and where to go for a task, in a routing table, and never holds content; `CLAUDE.md` imports it. Each `src/*` and `tests/*` folder gets a `CONTEXT.md` contract (what it reads, what it does, what it writes, what a human checks) when the folder is created in M0. Family folders get none: their contract is the component document, and `AGENTS.md` states the rule `src/Blazix.Primitives/<Family>/ ↔ docs/components/<family>.md ↔ tests/browser/<family>.spec.ts`. Generated indexes, if any, are rebuilt by script and never hand-edited.

### 3. Projects

One test project per layer, so that L1's "no Blazor" and L2's bUnit dependency are enforced by project references and CI can run L1–L3 without building the fixture host. The generator and the analyzers are separate projects because they ship to different audiences. The sample application under `samples/` (Amendments 2026-10-22) is where a milestone's families are tried by hand; the fixture host is the L4 host and nothing else. The solution file is `.slnx`; `Directory.Packages.props` holds central package versions; `Directory.Build.props` turns on nullable, warnings as errors, `IsTrimmable` and `IsAotCompatible` for the library (ADR 16).

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
* A demo app from day one (a third application to keep green while the API is still moving). Rejected 2026-10-20; reversed 2026-10-22 for a sample application that CI builds (Amendments).
* `package.json` inside the library project (a second `node_modules` for the L3 and L4 tooling).

## Amendments

**2026-10-20, time control and diagnostics through a page hook (M0 Track 3; chosen by the user the same day).** §1's fixture-host row and the Consequences read: Kestrel consumes `TimeProvider` as a singleton (a scoped registration fails host validation), so the host registers one `HarnessTimeProvider` that routes to the circuit's `FakeTimeProvider` by the renderer's synchronization context; a browser test advances time through `window.blazixFixture.advanceTime`, which invokes a `[JSInvokable]` method on the page's own session, on both render modes, so timers fire on the renderer's dispatcher and control is scoped to the page, which is what parallel Playwright workers against one host process need. Captured diagnostics are read through `/_fixture/diagnostics/{token}` on Server and through the page hook on WebAssembly; `/_fixture/ready` is the readiness URL for `webServer`. The "test-only endpoints for `TimeProvider` control" of §1 and the Consequences are therefore the page hook plus the diagnostics endpoint; ADR 06 §2's clock rule is unchanged.

**2026-10-21, a `Utilities/` folder for cross-family helpers (M1 planning; chosen by the user the same day).** The tree gains `src/Blazix.Primitives/Utilities/` (namespace `Blazix.Primitives.Utilities`), whose contract is `docs/architecture/shared-utilities.md` and which has its own `CONTEXT.md` like the other `src/*` folders: pure helpers that more than one family needs and that are neither pipeline, store, events nor twins, such as `clamp` (UTL-EDGE-14), `valueToPercent` (UTL-DOM-31), the visually hidden styles (UTL-DOM-13/14/15), label id registration, and the number serialization into attributes and inline styles (UTL §4.12.1) unless Track 1 already placed it under `Rendering/`, where it then stays. Types are internal unless a document makes one public. The M1 Meter track creates the folder; Progress and the M2 Field family reuse it. `AGENTS.md`'s code table lists the folder. Family folders still carry no `CONTEXT.md` (§2).

**2026-10-22, a sample application for manual verification per milestone (chosen by the user the same day).** §3's "There is no demo application before the first release: the fixture host is the browsable showcase" is replaced: the repository has a sample application, built in CI and never packed, so that each milestone's families can be tried by hand in a browser, the way a consumer would meet them, before the milestone closes. The tree gains `samples/`:

* `samples/Blazix.Primitives.Demo/` is a Blazor Web App and `samples/Blazix.Primitives.Demo.Client/` its WebAssembly-capable client, both referencing `src/Blazix.Primitives` by project reference and registering the library with `AddBlazixPrimitives()`, as the fixture host and its client do. Both are in `Blazix.Primitives.slnx` under a `samples` folder, with `IsPackable` false.
* The render mode is a route segment, as in the fixture host: `/server/<family>` and `/wasm/<family>` resolve to `InteractiveServerRenderMode(prerender: true)` and `InteractiveWebAssemblyRenderMode(prerender: true)`. Prerendering is on, unlike the fixtures, because an app a user visits prerenders. The home page lists every family with both links; each demo page shows live which mode it runs in.
* One page per family, in the order of the milestones: the markup of the Base UI documentation's Tailwind hero demo (`reference/base-ui/docs/src/app/(docs)/react/components/<family>/demos/hero/tailwind/`) translated to the port's parts and parameters, its utility classes carried over unchanged, and a second block of variants to try where the family has any. A milestone is not closed until every family it ships has a page (the human check of `samples/CONTEXT.md`).
* The styles are Tailwind, not Blazor CSS isolation (the user's choice, the same day, after a first version built on the documentation's CSS-module demos). Isolation does not scope nested rules and stamps its scope attribute only on elements a page's own markup writes, not on the elements the parts render, so the CSS-module demos needed their nesting written out and every selector under `::deep`, and a popup that portals out of the page would have escaped the scope. Tailwind's classes are global, so the markup is the documentation's and a portalled popup is styled like the rest. `tailwindcss` and `@tailwindcss/cli` join the root `package.json` (§4), pinned to 4.3.3, the version the documentation uses; the client project's build runs the CLI on `Styles/tailwind.css` (the documentation's theme and preflight over Tailwind's defaults, scanning `Demos/` only) and writes `wwwroot/tailwind.css`, ignored by git, as the library's esbuild step does for its modules. The Node toolchain is already a build requirement (ADR 04 §4), so the sample adds a pinned package, not a tool.
* It is not tested and has no tests-first obligation: it holds no behaviour of its own, only the library's parts composed the way a consumer would. C# in it follows ADR 08 §5 naming and needs no XML documentation. Nothing under `src/` references it, and a demo that reveals a library defect keeps showing the defect while the fix is made in the library.
* CI (ADR 11): `build-and-test` builds the sample host once (`dotnet build samples/Blazix.Primitives.Demo -c Release`) and runs nothing from it; `classify-paths.mjs` lists `samples/` with the paths no browser can observe, so a change confined to it runs `build-and-test` and no L4, and `samples/` beside a family change leaves the scope at that family. The fixture host stays the L4 host; its pages are no longer described as the showcase.
* The Context section's "third application to keep green while the API is still moving" is answered by that one build: the sample compiles against every change to the library, and a breaking change fails the pull request that makes it.

**2026-10-22, folders for a mechanism's C# half (M2 planning; chosen by the user the same day).** A mechanism whose requirements have a C# half and no owning family gets a folder named after the mechanism under `src/Blazix.Primitives/`, with the architecture document as its contract and its own `CONTEXT.md`: `Composite/` for CMP's root and item behaviours (the twin stays in `Interop/`, the module in `js/`); the popup store (PSH) in M4 follows the same rule. The field and form infrastructure (FFI) has owning families: its scopes, registry and validation service live in `Field/`, since `Field.Root` owns the scopes, and the form scope in `Form/`, as internal types the controls reuse. `DirectionProvider/` is a family folder whose contract is UTL §4.15.1, because the shared-utilities document is the one that specifies it. `Utilities/` keeps the UTL document as its contract and `Interop/` keeps the twins. `AGENTS.md`'s code table gains the `Composite/` row. The alternatives were `Interop/` for every behaviour, which widens that folder's contract from twins to mechanisms, and `Utilities/`, whose contract is one document.

**2026-10-27, the M4 folders (M4 planning, plan of 2026-10-27; chosen by the user the same day).** Under the 2026-10-22 rule the popup store's C# half goes in `src/Blazix.Primitives/Popup/` (contract `docs/architecture/popup-store-and-handles.md`: the store record and its selectors, the trigger registry, the handles, `ModalValue`, `InteractionType`, `PortalContainerValue`, the shared data-attribute constants; M4 track 1) and floating positioning's C# half in `src/Blazix.Primitives/Floating/` (contract `docs/architecture/floating-and-positioning.md`: the Positioner and Arrow behaviours, `Side`, `Align`, `PositionMethod`, `AnchorValue`, the collision and offset types; track 2), each with its `CONTEXT.md`; the twins stay in `Interop/`, the modules in `js/`. `AGENTS.md`'s code table gains both rows. The shared browser-suite helpers (one TypeScript helper per Base UI suite under `packages/react/test/`, called by each family's spec with its options) live under `tests/browser/suites/`. `js/floating-ui.ts` is the port's wrapper over the vendored engine (ADR 04 Amendments 2026-10-27), excluded from the module-graph kinds as `test-support/` is. The Razor probe of the payload type parameter is a throwaway under `spikes/payload-generic/` (ADR 14 Amendments 2026-10-27).
