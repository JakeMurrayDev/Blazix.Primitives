# src/Blazix.Primitives/js

The TypeScript side of the library: one module per row of ADR 04 §1, the JS initializer, and the shared helpers that implement the twin contract of ADR 04 §3. Built by esbuild from MSBuild (ADR 04 §4); `wwwroot/` is the output and is ignored by git.

## Inputs

- `docs/architecture/*.md`: each mechanism module implements the JS-resident requirements of exactly one architecture document; a component module holds one component's loops (ADR 04 §1, placement rule 2026-10-18).
- `docs/decisions/04-js-interop-boundary.md` §3 (contract, decision points) and §4 (toolchain); ADR 01 (what Server cannot do); ADR 11 §5 (module-graph lint).
- The Base UI source under `reference/` for the algorithms and the vitest titles.
- `node_modules` at the repository root (`package.json`: esbuild, typescript, vitest, `@floating-ui/dom` 1.8.0).

## Job

- `twin.ts`: what every module receives from `attach(elementOrId, optionsJson, dotNetRef)` and hands back (`{ update(optionsJson), dispose() }`); `createTwinChannel` gives a handle its `decide` and `report`; `resolveElement`, `parseOptions`, `applyConsumerStyle` (handover at attach). No module-level registries: a handle's state lives in its closure.
- `decide.ts`: `decide(twin, point, event, fallback)`. The render mode is detected from the runtime, per channel, on the first decision: one `Ping` through `invokeMethod`, which Blazor Server's dispatcher rejects synchronously and WebAssembly's answers. WebAssembly then runs the C# chain inside the dispatch; everywhere else the fallback policy is returned without a call.
- `event-info.ts`: the event snapshot. Its property names mirror `Events/EventSnapshot.cs` (Track 2), which is the one place that defines them; `kind` is the DOM interface name.
- `platform.ts`: UTL-ALG-11, computed once per page when the chunk loads; the `dom` module owns and re-exports it (Phase B).
- `initializer.ts` → `wwwroot/Blazix.Primitives.lib.module.js`: imports `platform` and registers custom event types (none yet).
- `build.mjs`: the esbuild step. One ESM entry per `BlazixJsEntry` item of the csproj into `wwwroot/`, shared code as `js/chunks/*`, `legalComments: 'inline'` so the Floating UI banner survives, sourcemaps and no minification in Debug.

## Toolchain rules

- The csproj target `BlazixBuildJs` runs `node js/build.mjs` before the static-web-assets pipeline enumerates `wwwroot` (`BlazixRegisterJs`, `BeforeTargets="ResolveProjectStaticWebAssets"`), so generated files are packed. `node` is needed at build time only.
- `dotnet build -p:BlazixSkipJsBuild=true` skips the step and fails with a clear message if an output is missing. Verified locally with node off `PATH`:
  1. `dotnet build src/Blazix.Primitives -c Release` with node on `PATH` (produces `wwwroot/`).
  2. `PATH=$(echo "$PATH" | tr ':' '\n' | grep -v node | paste -sd: -) dotnet build src/Blazix.Primitives -c Release -p:BlazixSkipJsBuild=true` → succeeds.
  3. `rm -r src/Blazix.Primitives/wwwroot` then step 2 → fails with the `BlazixRegisterJs` error naming the missing output.
  Track 4 adds the manual CI job (ADR 11 §1, issue 9).
- Adding a module: a `<BlazixJsEntry Include="js/<module>.ts" Out="js/<module>" />` item, `js/<module>.test.ts` beside it (L3, vitest in jsdom), and a `<Module>Twin` in `Interop/` with its options record in `BlazixJsonContext`.
- A mechanism module never imports a component module (ADR 04 §1); the L3 import-graph test of ADR 11 §5 enforces it from Track 4.

## Outputs

- `wwwroot/Blazix.Primitives.lib.module.js` (the initializer) and `wwwroot/js/<module>.js` with `wwwroot/js/chunks/*`, served at `_content/Blazix.Primitives/...` and packed as static web assets.

## Human check

- Every `decide()` call names its point and its fallback policy from ADR 04 §3; no module moves state through a synchronous invoke (ADR 01 decision 1).
- Every module checks `event.defaultPrevented` where the Base UI source does (ADR 04 Amendments 2026-10-18).
- One message per state change, never per frame (`report`).
