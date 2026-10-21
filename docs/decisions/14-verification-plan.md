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
| 01 Server round-trip cost of the TRN-ANI-09 row | M1 (Collapsible) | measurement under Playwright network throttling; the result is appended to ADR 01 and decides whether the optimistic hide stays; closes before Collapsible is declared done, not before M1's first component (Amendments 2026-10-21). **Measured 2026-10-21** by `collapsible.spec.ts` "@verifies ADR-01/2 …": without the hide a closed panel would stay visible about one round trip after its exit (median 1, 54 and 156 ms at 0, 50 and 150 ms added round trip); with it, 0 ms. Whether the hide stays is the user's decision on these numbers (PR 69). The latency is added by routing the circuit's WebSocket (`routeWebSocket`), because Chromium's network emulation does not delay it |
| 02 interactive activation replaces the prerendered subtree; roots activate in document order | M0 | permanent L4 test on the prerender fixtures, both modes |
| 02 the frame re-emitter keeps `@key` and `@ref` for a component root | M0 | permanent L2 tests in the conformance suite |
| 02 scratch-render cost on a list part | M5 (Menu) | timed measurement; result appended to ADR 02; decides frame caching (ADR 16 B) |
| 04 Server preserves the order between JS-to-.NET invocations and event dispatches | M0 | permanent L4 Server test on the twin base |
| 04 a render queued inside a synchronous invoke flushes before the listener returns | M0 | permanent L4 WebAssembly test on `decide()` |
| 04 a portal node with siblings in its logical parent | M4 | permanent L4 test, both modes, plus the "only child of its wrapper" shape rule |
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
| 07 the Floating UI legal banner survives esbuild chunking | M4, the first pack that contains the `positioning` chunk (was "first pack", Amendments 2026-10-21) | inspect the chunk |
| 08 a `[Union]` struct parameter binds from a Razor literal | now, with the union spike | `spikes/union-binding/` |
| 08 MUI's trademark position on "Base UI" in the package description | before the first public release | read MUI's trademark policy; ask if unclear |
| 09 route-segment render mode with prerender off on a Blazor Web App serving WebAssembly assets | M0 | the first fixture |
| 09 `webServer` starts `dotnet run` and the readiness URL check works | M0 | the first browser run |
| 10 MinVer through a rolling-forward `global.json` | M1 | the first tagged build |
| 10 nuget.org accepts the RC2-built package | first publish | — |
| 11 the scoped-L4 path mapping classifies mixed PRs as full L4 | M0 | a synthetic PR |
| 11 `--fail-on-flaky-tests` with `retries: 0` is a guard only | first nightly | — |
| 15 the citation-migration tool works on the shallow clone (history depth) | before the first re-pin | — |
| 16 frame caching for substitutes; `[JSImport]` transport inside twins | M5; M4–M6 | measurements (ADR 16) |
| 16 `ChangeDetection`'s known-immutable set covers enums and nullable enums | M0 | read `ChangeDetection.cs` in aspnetcore `release/11.0`; note in ADR 16 |
| 16 generator-emitted `SetParametersAsync` and per-item unmatched-bag cost | M5 | part of the M5 list-part measurement |
| 16 Combobox `virtualized` (CMB-API-22) with Blazor `Virtualize` | M5 | permanent L4 test |

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
