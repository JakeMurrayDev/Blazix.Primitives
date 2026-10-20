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
- `platform.ts`: UTL-ALG-11 as `detectPlatform(navigatorData, webkitBackdrop)`, and `platform`, the page's flags computed once (and frozen) when the chunk loads; the `dom` module owns and re-exports both.
- `dom.ts` (entry `js/dom`): the helpers other modules import: `ownerDocument`, `ownerWindow`, `isElement`, `isHTMLElement`, `activeElement`, `contains`, `closest`, `getTarget`; `getCssDimensions`, `getElementTransform`, `getPseudoElementBounds`, `isMouseWithinBounds`, `getElementAtPoint`, `isScrollable(X|Y)`, `findScrollableAncestor`, `findScrollableTouchTarget`, `normalizeScrollOffset`, `getMaxScrollOffset`, `valueToPercent`; `observeResize` and `observeIntersection` (no-ops where the observer does not exist); `markLiveRegion`, `probeImage`, `deriveImageStatus`. Its `attach` runs the primitives `DomTwin` turns on: `indeterminate`, the live-region marker (after the next paint: one animation frame, then one task; restored 200 ms later if unchanged; never on iOS) and the image status (one report per change; the synchronous pass of a cached image reports only its result; the fallback gets `hidden` the moment the image loads). The Floating UI DOM predicates Base UI imports are re-implemented here so this module stays out of the positioning chunk.
- `button.ts` (entry `js/button`): UBT-ALG-01/02 as Base UI writes them, with two port points. A veto from a JavaScript listener that ran before the module (an earlier listener on the element, one on a descendant, or a capture listener on an ancestor) is `event.defaultPrevented`, checked where Base UI checks it; a veto from C# is the verdict of `decide(twin, 'activate', event, 'run')`, asked only where the module is about to act (prevent a key or click). The nested-handler flag is a symbol on the event, set right before each synthetic click, so two handles on one element click once. Also: `dispatchClickWithModifiers`, `isTouchLikePointerType`, `isElementDisabled`, disabled press suppression (click and pointerdown prevented, mousedown left alone), the composite `disabled` property correction, pointer-type classification, the native-mode development check, and the press-and-hold repeater whose ticks, release and single-action clicks are reports.
- `initializer.ts` → `wwwroot/Blazix.Primitives.lib.module.js`: imports `platform` and registers custom event types (none yet).
- `build.mjs`: the esbuild step. One ESM entry per `BlazixJsEntry` item of the csproj into `wwwroot/`, shared code as `js/chunks/*`, `legalComments: 'inline'` so the Floating UI banner survives, sourcemaps and no minification in Debug.

## Traceability (the sources carry no specification references)

- `twin.ts`, `decide.ts`: ADR 04 §3 (contract, decision points, fallback policies), ADR 02 decision 1 (the substitute callback passes the generated id), ADR 04 Amendments 2026-10-18 (handover at attach), ADR 01 decision 1 (the synchronous invoke serves decisions only).
- `event-info.ts`: ADR 04 §3, EVD-API-04 (the placeholder) and EVD-API-05 (`kind`).
- `platform.ts`: UTL-ALG-11, UTL-EDGE-01..09 (shared-utilities.md §4.16).
- `dom.ts`: shared-utilities.md §4.10 (UTL-DOM-18..22), §4.11 (UTL-DOM-23..31, UTL-ALG-12..14), §4.13 (UTL-A11Y-01..04), §4.16; checkbox.md CHK-DOM-13; avatar.md AVA-ALG-01/02, AVA-STA-09..13, AVA-EVT-02; ADR 04 §1 dom row and its 2026-10-18 and 2026-10-20 amendments.
- `button.ts`: button-behavior.md in full (UBT-ALG-01..08, UBT-KEY, UBT-PTR, UBT-EVT-04..10, UBT-EDGE-01..07); ADR 04 §3 (decision point `activate`, fallback `run`) and Amendments 2026-10-18 (dispatch-time decisions are JavaScript's); ADR 01 decision 1 (a tick is a report).
- `initializer.ts`, `build.mjs`: ADR 04 §4, ADR 07 (the Floating UI licence banner).

## Toolchain rules

- The csproj target `BlazixBuildJs` runs `node js/build.mjs` before the static-web-assets pipeline enumerates `wwwroot` (`BlazixRegisterJs`, `BeforeTargets="ResolveProjectStaticWebAssets"`), so generated files are packed. `node` is needed at build time only.
- `dotnet build -p:BlazixSkipJsBuild=true` skips the step and fails with a clear message if an output is missing. Verified locally with node off `PATH`:
  1. `dotnet build src/Blazix.Primitives -c Release` with node on `PATH` (produces `wwwroot/`).
  2. `PATH=$(echo "$PATH" | tr ':' '\n' | grep -v node | paste -sd: -) dotnet build src/Blazix.Primitives -c Release -p:BlazixSkipJsBuild=true` → succeeds.
  3. `rm -r src/Blazix.Primitives/wwwroot` then step 2 → fails with the `BlazixRegisterJs` error naming the missing output.
  Track 4 adds the manual CI job (ADR 11 §1, issue 9).
- Adding a module: a `<BlazixJsEntry Include="js/<module>.ts" Out="js/<module>" />` item, `js/<module>.test.ts` beside it (L3, vitest in jsdom, against `test-support/fake-twin.ts`, which records reports and decisions and behaves like Server or, with `synchronous: true`, like WebAssembly), and a `<Module>Twin` in `Interop/` with its options record in `BlazixJsonContext`.
- L3 titles are Base UI's where a Base UI test observes the module, kept verbatim; other tests carry the port's own titles. Browser-only Base UI tests keep their `it.skipIf(isJSDOM)` and run nowhere until a module-level browser runner exists.
- A module that owns no style property never calls `applyConsumerStyle`: `dom` and `button` ignore `style`.
- A mechanism module never imports a component module (ADR 04 §1); the L3 import-graph test of ADR 11 §5 enforces it from Track 4.

## Outputs

- `wwwroot/Blazix.Primitives.lib.module.js` (the initializer) and `wwwroot/js/<module>.js` with `wwwroot/js/chunks/*`, served at `_content/Blazix.Primitives/...` and packed as static web assets.

## Human check

- Every `decide()` call names its point and its fallback policy from ADR 04 §3; no module moves state through a synchronous invoke (ADR 01 decision 1).
- Every module checks `event.defaultPrevented` where the Base UI source does (ADR 04 Amendments 2026-10-18).
- One message per state change, never per frame (`report`).
