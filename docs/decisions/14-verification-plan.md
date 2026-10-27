# ADR 14 — Verification plan

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | when and how each verification item of ADRs 01–16 is checked; how items are tracked; how permanent tests are linked to the items they verify; how the .NET 11 release-candidate SDK is installed |
| Affects | the "Verification items" section of every ADR; ADR 12 (items block milestones); no requirement IDs |

## Context

ADRs 01–06 left fifteen items open (two were closed on 2026-10-18: case-insensitive parameter binding, and Blazor event directives compiling to attribute frames). Twelve are observable at runtime and can become permanent tests that pin the platform behaviour an ADR relies on; three are one-off readings or measurements. The union-binding item needs the .NET 11 SDK, which is not installed (only 10.0.300 and 10.0.301). RC2 installs side by side with 10.x, but the macOS installer package needs an administrator password; `dotnet-install.sh` can place an SDK under `~/.dotnet` without one at the cost of a second dotnet root. ADRs 07–16 add items of their own.

## Decision

### 1. The items of ADRs 01–06

| ADR item | Milestone | How |
|---|---|---|
| 01 `WebAssemblyCallQueue` deferral stays inside one browser task | M0 | read `WebAssemblyRenderer` in aspnetcore `release/11.0`; "Verified" note in ADR 01 |
| 01 Server round-trip cost of the TRN-ANI-09 row | M1 (Collapsible) | measurement under Playwright network throttling; the result is appended to ADR 01 and decides whether the optimistic hide stays; closes before Collapsible is declared done, not before M1's first component (Amendments 2026-10-21). 2026-10-21, by `collapsible.spec.ts` "@verifies ADR-01/2 …": measured median 1 / 54 / 156 ms at 0 / 50 / 150 ms added round trip without the hide, 0 ms with it; the optimistic hide stays (chosen by the user the same day). The latency is added by routing the circuit's WebSocket (`routeWebSocket`), because Chromium's network emulation does not delay it |
| 02 interactive activation replaces the prerendered subtree; roots activate in document order | M0 | permanent L4 test on the prerender fixtures, both modes |
| 02 the frame re-emitter keeps `@key` and `@ref` for a component root | M0 | permanent L2 tests in the conformance suite |
| 02 scratch-render cost on a list part | M5 (Menu) | timed measurement; result appended to ADR 02; decides frame caching (ADR 16 B) |
| 04 Server preserves the order between JS-to-.NET invocations and event dispatches | M0 | permanent L4 Server test on the twin base |
| 04 a render queued inside a synchronous invoke flushes before the listener returns | M0 | permanent L4 WebAssembly test on `decide()` |
| 04 a portal node with siblings in its logical parent | M4, track 1 (Amendments 2026-10-27) | permanent L4 test `@verifies ADR-04/3` on the test-only `FixturePortal` host, both modes, with the wrapper shape of ADR 04 Amendments 2026-10-27 ([issue 8](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/8)) 2026-10-27: verified, both modes, `tests/browser/portal.spec.ts` "@verifies ADR-04/3 …" (issue closed). |
| 04 build without Node via `BlazixSkipJsBuild` | M0 | a manual CI job that builds with the flag and prebuilt outputs (ADR 11), run before every tagged release |
| 05 Razor binds union-typed parameters | **now** | `spikes/union-binding/` on the RC2 SDK; blocks the parameter shape of M0 |
| 05 `EqualityComparer<T>.Default` does not box on WebAssembly | M0 | permanent L4 WebAssembly allocation probe (`GC.GetTotalAllocatedBytes` around N comparisons) on the interpreter; AOT by inspection of the generated code, stated as such |
| 05 union feature gaps per SDK release | now, then at every `global.json` change | a checklist in ADR 05, re-run per SDK bump |
| 06 `RendererInfo.IsInteractive` is `false` in prerender and `true` afterwards | M0 | permanent L4 test on the prerender fixture |
| 06 `page.clock` and SignalR coexist (auto-advancing clock) | M0 | permanent L4 Server test with the clock installed |
| 06 bUnit mocks `IJSObjectReference` handles that return handles | M0 | the first L2 twin contract test is the verification |

### 2. Items added by ADRs 07–16

| ADR item | Milestone | How |
|---|---|---|
| 07 the Floating UI legal banner survives esbuild chunking | M4, track 2 (was "first pack", Amendments 2026-10-21; the track, Amendments 2026-10-27) | a permanent check in `.github/scripts/assert-package.mjs` that the packed `wwwroot/js/positioning.js` (the engine's only importer, pinned by the module-graph lint) carries the banner of `js/floating-ui.ts` (ADR 07 Amendments 2026-10-27; [issue 16](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/16)). 2026-10-27, by `assert-package.test.mjs` "@verifies ADR-07/1 accepts the minified engine output that carries the wrapper banner inline, and reports one that lacks it" and the script's `checkEngineBanner` on the packed output, with a local pack run quoted in [PR 127](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/127): closed |
| 08 a `[Union]` struct parameter binds from a Razor literal | now, with the union spike | `spikes/union-binding/` |
| 08 MUI's trademark position on "Base UI" in the package description | before the first public release | read MUI's trademark policy; ask if unclear |
| 09 route-segment render mode with prerender off on a Blazor Web App serving WebAssembly assets | M0 | the first fixture |
| 09 `webServer` starts `dotnet run` and the readiness URL check works | M0 | the first browser run |
| 10 MinVer through a rolling-forward `global.json` | M1 | the first tagged build |
| 10 nuget.org accepts the RC2-built package | first publish | — |
| 11 the scoped-L4 path mapping classifies mixed PRs as full L4 | M0 | a synthetic PR |
| 11 `--fail-on-flaky-tests` with `retries: 0` is a guard only | first nightly | — |
| 15 the citation-migration tool works on the shallow clone (history depth) | before the first re-pin | — |
| 16 frame caching for substitutes; `[JSImport]` transport inside twins | M5; M4, track 3 (was M4–M6, Amendments 2026-10-27) | measurements (ADR 16); the `[JSImport]` probe fixture of ADR 16 Amendments 2026-10-27 ([issue 26](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/26)) |
| 08 a non-generic and a generic part of one tag name resolve without ambiguity; a `RenderFragment<TPayload?>` child content binds plain markup (ADR 08 Amendments 2026-10-27) | M4, track 1, before the popup parts are shaped | `spikes/payload-generic/` on the RC2 SDK; the finding appended to ADR 08 ([issue 116](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/116)). 2026-10-27, by `spikes/payload-generic/` (M4 track 1): the dual-name shape does not compile a plain popup (`RZ10012`) and does not infer a payload user's type (`CS0246`); a generic-only `RenderFragment<TPayload?>` binds plain markup; the parts are generic only with `NoPayload` (ADR 08 Amendments 2026-10-27, the probe's finding); issue closed |
| 16 `ChangeDetection`'s known-immutable set covers enums and nullable enums | M0 | read `ChangeDetection.cs` in aspnetcore `release/11.0`; note in ADR 16 |
| 16 generator-emitted `SetParametersAsync` and per-item unmatched-bag cost | M5 | part of the M5 list-part measurement |
| 16 Combobox `virtualized` (CMB-API-22) with Blazor `Virtualize` | M5 | permanent L4 test |
| 05 the form controls participate in a cascaded `EditContext` (Amendments 2026-10-22) | M2 | permanent L2 tests per control and one L4 title per render mode on a fixture with `DataAnnotationsValidator`; the trim and AOT probe by the Field track |

### 3. Tracking, linking, SDK

* **Tracking:** one GitHub issue per item, label `verification`, assigned to its milestone (GitHub milestones M0–M6). The ADR line links the issue and, once closed, names the test or the finding. Created 2026-10-20: issues 1–15 for the rows of §1 and 16–26 for the rows of §2, in table order; issues 27–29 for the three rows added by ADR 16's amendment (`https://github.com/JakeMurrayDev/Blazix.Primitives/issues/<n>`).
* **Permanent tests** are tagged so a grep lists them and the ADR lines can cite them: `@verifies ADR-05/3` in a TypeScript title, `[Trait("Verifies", "ADR05-3")]` in C#.
* **Spikes** live in `spikes/<item>/`, excluded from the solution, and are deleted when the item closes.
* **SDK:** the user installs the official RC2 package (side by side with 10.x); the repository's `global.json` pins it (ADR 10); the union spike runs the same day.

## Consequences

* Milestone M0 carries ten items; none of them is a component.
* Every ADR's "Verification items" section stays the source of the item text; this ADR holds the schedule only.

## Verification items

* None of its own.

## Considered options

* A verification log file (a second home for what the issues already are); untagged tests (the link from an ADR to its evidence is lost); `dotnet-install.sh` into `~/.dotnet` (works today but leaves two dotnet roots to keep straight).

## Amendments

**2026-10-21 (M1 planning; chosen by the user the same day).** §2: the ADR 07 banner item moves from "first pack" to M4, the first pack that contains the `positioning` chunk, because no module imported `@floating-ui/dom` before the `positioning` module (M0 Track 3 phase B re-implemented Floating UI's DOM predicates in `dom.ts`, so the M0 and M1 packs carry no Floating UI chunk to inspect); [issue 16](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/16) is re-assigned to milestone M4. §1: the ADR 01 round-trip item ([issue 2](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/2)) closes before Collapsible is declared done, per ADR 12 Amendments 2026-10-21, so the pipeline-only M1 families are not held by it.

**2026-10-22 (M2 planning; chosen by the user the same day).** §2 gains the ADR 05 row for the form controls inside a cascaded `EditContext` (ADR 05 Amendments 2026-10-22), assigned to M2 as [issue 76](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/76), label `verification`; it is an item of the milestone as a whole and closes before the M2 tag (ADR 12 Amendments 2026-10-21). M2 has no other verification item.

**2026-10-27 (M4 planning; chosen by the user the same day).** §1: the ADR 04 portal item ([issue 8](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/8)) is M4 track 1's: the permanent L4 test `@verifies ADR-04/3` on the test-only `FixturePortal` host, both modes, with the wrapper shape of ADR 04 Amendments 2026-10-27. §2: the ADR 07 banner item ([issue 16](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/16)) is track 2's and becomes a permanent check in `.github/scripts/assert-package.mjs` that the packed `wwwroot/js/positioning.js` carries the banner of `js/floating-ui.ts` (ADR 07 Amendments 2026-10-27; the locator settled by the orchestrator 2026-10-27); the `[JSImport]` item ([issue 26](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/26)) is measured in M4 by track 3, with a probe fixture and an `update`, a report and a `Decide` per message against `[JSImport]`/`[JSExport]` on WebAssembly under a hover-and-flip traffic sample, the finding recorded in ADR 16 and the issue closed or re-assigned by the figures (ADR 16 Amendments 2026-10-27); §2 gains the ADR 08 row for the Razor probe of the payload type parameter (ADR 08 Amendments 2026-10-27), track 1's, as [issue 116](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/116), label `verification`, run before the popup parts are shaped. Every M4 item closes inside its track (ADR 12 Amendments 2026-10-27).
