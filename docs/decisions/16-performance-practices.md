# ADR 16 — Performance practices

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | which Blazor performance techniques are rules from M0, which wait for a measurement, and which are rejected because they contradict an accepted decision |
| Affects | no requirement IDs; applies ADR 01 decision 1, ADR 02 decisions 3–4, ADR 04 §3–§4 and ADR 05 §4 without changing them |

## Context

Blazor re-renders a child component whenever any parameter is not a known-immutable primitive, and every part receives `ChildContent`, a new delegate per parent render, so parts cannot skip renders by comparing parameters; what matters is the cost of one part render, and ADR 02's scratch render per substituted part is the one known hot spot (already a verification item). The diff relies on stable sequence numbers; a dynamic sequence degrades to linear re-insertion. `EventCallback` is a struct, but a lambda in the render path allocates per render. `IJSObjectReference.InvokeAsync` serialises its arguments with `System.Text.Json`; reflection-based serialisation costs start-up on WebAssembly and fights trimming, while a source-generated `JsonSerializerContext` is trim-safe. `IsTrimmable` and `IsAotCompatible` turn on the ILLink and AOT analyzers at build time. `[JSImport]` is a faster, synchronous, WebAssembly-only transport; ADR 01 forbids mode-dependent behaviour beyond the synchronous decide path and ADR 04 limits traffic to one message per state change. CoreCLR reports exact per-thread allocation, so an allocation budget can be asserted in an L1 test; the WebAssembly interpreter reports process-wide allocation (ADR 14).

## Decision

### A. Rules from M0

1. The library sets `IsTrimmable` and `IsAotCompatible` with warnings as errors, so reflection or dynamic code fails the build.
2. Handlers are `EventCallback` fields created once per part (`EventCallback.Factory.Create(this, Method)` in initialisation), never lambdas in the render path; ADR 02's single real handler per event is one such field.
3. The pipeline uses literal sequence numbers and `OpenRegion` for every variable section; re-emitted substitute frames keep their original sequence numbers inside their own region.
4. One source-generated `JsonSerializerContext` covers every twin options record and callback payload.
5. Class and style merging is memoised on input string identity, so a merge runs once per distinct input rather than per render.
6. Parts register C# handlers only for events whose result is state (ADR 04 Amendments 2026-10-18), restated here as the Server traffic rule.
7. Allocation budgets are tests: L1 tests measure `GC.GetAllocatedBytesForCurrentThread()` around a part render, a store notification and a merge against a budget per test; `[MemoryDiagnoser]` benchmarks record the trend. Budgets tighten, never loosen, without an ADR amendment.

### B. Deferred to a measurement

* Caching the scratch-rendered frames of a substitute between renders: decided by ADR 02's M5 measurement.
* Distinct literal sequence numbers for the part-owned attributes (`id`, `class`, `style`, the generated data attributes), keeping the one shared number for the consumer's attribute bag (the pattern of Blazor's `@attributes` splatting): decided by the same M5 measurement.
* `[JSImport]` transport inside twins on WebAssembly: measured in M4 and not adopted (the finding of 2026-10-27 below: about 0.6 ms of main thread per hover-and-flip cycle); adopted only by an ADR 01/04 amendment if the M5 list-part measurement shows interop dominating, since twins would become the one mode-dependent place.

### C. Rejected

* `ShouldRender` based on parameter comparison (`ChildContent` defeats it; a part that skips may hide a changed child).
* Branching component code on render mode for speed (ADR 01 decision 1).
* Reflection caches for projection or attribute bags (ADR 05's amendment of ADR 02).

## Consequences

* The allocation-budget tests are part of M0 (ADR 12) and run at L1 on every change (ADR 11).
* Rule 1 makes trimming and AOT compatibility a library guarantee rather than a consumer's discovery.

## Verification items

* The two items of §B, tracked in ADR 14 ([issue 25](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/25), [issue 26](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/26)). **Verified 2026-10-27 for issue 26 by `tests/browser/harness.spec.ts` "@verifies ADR-16/2 …" (M4 track 3): no material cost, the twins keep their transport**; the dated finding with its figures is in Amendments below. Issue 25 (frame caching) stays M5's.
* Added by Amendments 2026-10-20: Blazor's known-immutable set in `ChangeDetection` covers enums and nullable enums ([issue 27](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/27)). **Verified 2026-10-20 by reading `ChangeDetection.cs` in aspnetcore `release/11.0`: covered**; the dated finding is in Amendments below. The generator-emitted `SetParametersAsync` and per-item bag cost ([issue 28](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/28), M5) and Combobox `virtualized` with `Virtualize` ([issue 29](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/29), M5) stay open.

## Considered options

* The techniques of §C, recorded so nobody "optimises" a part into one of them.

## Amendments

**2026-10-20, from the Blazor rendering-performance guidance ([learn.microsoft.com, aspnetcore-11.0](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-11.0)), mapped onto the records.** Six of the page's techniques were already rules or settled decisions (ADR 01/04 event routing, ADR 02 one instance per part, ADR 05 fixed store cascade, rule 2 cached delegates, rule 6, the rejection in §C); the rest are recorded here.

*Rules added to §A:*

8. `PartBase<TState>` implements `IHandleEvent` so that a part re-renders only when it calls `StateHasChanged` itself: a request that commits (ADR 05 §2) renders, a cancelled request or a handler on a disabled part does not, and a controlled part renders only when its parameters change. `HandleEventAsync` routes exceptions through `DispatchExceptionAsync` so error boundaries keep working. Consumer handlers keep their own re-render because an `EventCallback` targets the consumer's component.
9. Every `CascadingValue` the library renders is `IsFixed = true`; change reaches parts by subscription only (the store subscription of ADR 05 §1 and the context subscriptions of the parts). The analyzers project checks it.
10. A part renders its internal elements (hidden inputs, indicator wrappers, visually hidden spans) inline in its own render tree; the library never introduces an internal component instance beyond the one per part (ADR 02 decision 4), because each instance costs about 0.06 ms per render on WebAssembly in the product team's measurement.

*Accepted cost:* `CaptureUnmatchedValues` on every part is Base UI's prop pass-through and the basis of the conformance suite (RND-DOM-36..49); it is not avoided for list items. Its per-item cost is part of the M5 measurement of §B.

*Added to §B (deferred to a measurement, folded into the M5 measurement issue):* a generator-emitted `SetParametersAsync` per part (the page reports under 10% at 10,000 instances on .NET 10, less later; hand-written it is rejected, generated it is free to try); the per-item render cost of list parts including the unmatched bag.

*Verification items added (tracked in ADR 14 and its issues):* Blazor's known-immutable set in `ChangeDetection` covers enums and nullable enums, so leaf parts whose parameters are enums and primitives get the built-in render skip (M0; read `ChangeDetection.cs` in aspnetcore `release/11.0`); the Combobox `virtualized` path (CMB-API-22) works with the consumer windowing items through Blazor's `Virtualize` component (M5, L4).

**2026-10-20, finding for the first item above (M0 track 1, [issue 27](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/27)): covered.** `ChangeDetection.IsKnownImmutableType` in aspnetcore `release/11.0` (`src/Components/Components/src/ChangeDetection.cs`) accepts any type whose `Type.GetTypeCode` is not `TypeCode.Object`, and an enum's type code is that of its underlying integral type, so every enum is known-immutable and compared by `Equals`. A nullable enum parameter reaches `MayHaveChanged<T1, T2>` boxed, which is the underlying enum value or `null`, so it is covered by the same test (the null case by the null comparison that precedes it). The set also holds `string`, `DateTime`, `decimal`, `Guid`, `DateOnly`, `TimeOnly` and, through `IEventCallback`, `EventCallback` and `EventCallback<T>` (compared by receiver and delegate). What it means for leaf parts: a part whose parameters are all primitives, strings, enums or nullable enums is skipped by Blazor when its parent re-renders with equal values; a part that receives `ChildContent`, a `Func`, a record or an `EventCallback` wrapping a lambda the parent recreates each render is not, since those compare by reference, which is the premise of rule 2 and of ADR 02's one instance per part. No action follows.

**2026-10-20, from the review of the pipeline against the Blazor sequence-number guidance ([learn.microsoft.com, aspnetcore-11.0](https://learn.microsoft.com/en-us/aspnet/core/blazor/advanced-scenarios?view=aspnetcore-11.0)).** Rule 3 held for the pipeline's host, attributes and children, but the consumer's element and component reference captures were numbered by a counter; each is now a literal (a capture before the children 3, after them 101, a substitute component's 2). A second capture on one element or component repeats its literal, which the guidance names as how the diff recognises a loop; Razor emits at most one capture there, so a repeat comes only from a hand-written fragment. `RenderPipelineSequenceTests` asserts the number of every frame the pipeline emits. *Added to §B:* the distinct literals for the part-owned attributes (the second bullet above). *Finding:* `ASP0006` is defined in `Microsoft.AspNetCore.App.Analyzers`, which the library does not load (it references the Components package, not the shared framework), so that analyzer never checked the library and the pragmas naming it were inert; it also asks for an integer literal at the call site and would flag named constants. *Added:* analyzer `BLZX2002` in the analyzers project, beside rule 9's `BLZX2001`, reports any `RenderTreeBuilder` sequence argument that is not a constant expression (a literal or a named constant); it is suppressed on `Reemit` and `AttributeValue.EmitTo` alone, where the number is the consumer's own or the caller's constant passed through.

**2026-10-27, the M4 popup core (M4 planning, plan of 2026-10-27; chosen by the user the same day).** *Rules applied:* the popup store's payload lives in the store's non-reactive context with a version counter in the record, so a value-type payload never boxes in a notification's comparison on the interpreter (ADR 05 §4; an allocation-budget test pins it); the shared `PopupStoreState<TPayload>` record composed into each family's record costs two record allocations per shared write, accepted (ADR 05 Amendments 2026-10-27); every twin's options record has value equality, so an `update` is sent only on change (ADR 04 §3's one message per state change); the open-change relay is one `update` per committed change, never one per listener; a Root's listeners render only the parts whose selection changed, measured at L2 by counting `ClassValue` invocations and quoted in the pull request (the lesson of [issue 110](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/110)); no internal component instance for a focus guard (rule 10; ADR 02 Amendments 2026-10-27). *§B's `[JSImport]` item* ([issue 26](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/26)) is measured in M4 by track 3 (ADR 14 Amendments 2026-10-27): a probe fixture compares the `IJSObjectReference` and `DotNetObjectReference` transport of the twin base against `[JSImport]`/`[JSExport]` for an `update`, a report and a `Decide` per message on WebAssembly under a hover-and-flip sample; the finding is recorded here and decides whether the twins get a WebAssembly-only transport by an ADR 01/04 amendment.

**2026-10-27, the popup store's reconciliation runs at the first popup part's render boundary of the batch (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 8, settled by the orchestrator 2026-10-27 as the worker recommended, under the Amendments 2026-10-27 above and ADR 05 §1; the user was told and may overturn; the dated notes on PSH-STA-16, PSH-STA-18, PSH-STA-23 and PSH-ALG-03 are the implementing pull request's).** The brief placed PSH-ALG-03's reconciliation in the Root's `OnAfterRender` with the Root rendering on every store write that changes its inputs (open, the trigger count, the active trigger's id and element). In Blazor a component that hosts consumer content re-renders every child whose parameters include a `RenderFragment`, because `ChangeDetection` treats delegates as always changed and a `CascadingValue` always re-renders its content (measured with a bUnit probe: a host's `StateHasChanged` re-rendered the child holding `ChildContent`, not the child holding a string), and every popup part takes `ChildContent`; so that wiring would re-render the whole Root content, inactive triggers included, on every open, close, trigger switch and trigger mount while open, the cost Base UI 1.9.0 removed in React with an internal leaf component that rule 10 forbids here. The store therefore marks the reconciliation pending when a write changes its inputs, and the Root's `OnAfterRender` and the shared trigger behaviour's `OnAfterRender` run it when it is pending: every such write renders at least one popup part (the requesting or registering trigger, a trigger whose `aria-expanded` or `aria-controls` changed, or the Root for its own parameters), and the one write that renders none, a registered trigger disposed while registered, asks the Root to render. PSH-STA-23's close is requested at the boundary that detects the loss, after the batch's parameter sets have all run. The Root subscribes only to the selections its own parameters need, so a selection renders only the parts whose selection changed, as the amendment above requires and the L2 render counts prove. The alternative, the brief's wiring with the claim narrowed to writes that do not touch the Root's inputs, was rejected for one extra render of every content part per open, close or trigger switch. *Caveat and refinement (the worker's L2 evidence, settled by the orchestrator the same day):* the boundary wiring needs a popup part that selects open or mounted to render in the batch; every family's Portal and Popup do, and all 29 reconciliation titles pass with one present, but a tree of Root and Triggers alone (the shape of Base UI's `store.set('open', true)` tests) fails the open claim, the trigger-less open and the claim after a controlled close, because no trigger's own state changes. The store therefore keeps both wirings behind one switch it flips itself: a popup part that selects open or mounted registers as a boundary part at subscription, and while none is registered the store renders the Root on its inputs (the brief's wiring), so a bare tree behaves as Base UI's and a real family never re-renders its content for an open or close. The rule is stated in the dated notes on PSH-STA-16 and PSH-STA-18 and in `Popup/CONTEXT.md`.

**2026-10-27, rule 7's budgets run in one non-parallel collection per test project, a budget samples the least of N where one operation may hit a doubling, and a new budget is taken from full runs ([issue 112](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/112); the collection and the least of five settled by the orchestrator 2026-10-27 after CI run 40521661363 failed two budgets; the reproduction and the envelope by M4 track 2, [PR 127](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/127)).**
- *The collection.* Every allocation-budget class (`*AllocationBudgetTests`, `*RenderBudgetTests`) in `tests/Blazix.Primitives.Tests` and `tests/Blazix.Primitives.Tests.Components` carries `[Collection(AllocationBudgetsCollection.Name)]`, a collection with `DisableParallelization = true`. It runs after the parallel collections, one class at a time.
- *Least of N.* A budget whose operation may hit bUnit's list doubling takes the least of five samples, as the Switch toggle budget already did.
- *Reproduction* (the full `Tests.Components` project in Release, parallel, issue 112's task 1):
  - before the collection, 20 runs gave 5 failures: the Positioner at 6376, 6568 and 6288 B against 6208, Composite at 4168 against 4056, and Tabs at 3112 against 2608;
  - after it, Composite and Tabs did not fail in 45 runs, but the Positioner still failed 4 of 20 at 6288 to 6320.
- *Why the Positioner still failed.* Its five samples are equal within a run, and the test alone always measures 6208. Across 46 full runs the floor is 6208 (31 runs), 6288, 6320 or 6352: the state earlier tests leave in the process (the harness's pools, the JIT's tiering) moves it, not work inside the measured window.
- *A new budget's first figure* is therefore taken as the envelope of full runs before it lands: 6352 for the Positioner (25 of 25 full runs green). This figure was settled by the orchestrator 2026-10-27: a fresh budget pins the full-run envelope, because rule 7 exists to catch a regression, and the 6208 the orchestrator had first asked to keep is the test's figure alone.
- *2026-10-28, the Positioner's figure before it lands (M4 track 2, after M4 track 1's PR 126; the rule above applied, settled by the orchestrator 2026-10-28).* Merging `main` adds 24 B to the measured re-render, and the part's `[CascadingParameter] PopupPortalContext` another 24 B, which every portaled Positioner part carries (the portal's move, ADR 04 Amendments 2026-10-27): the test alone measures 6256. Over 25 full runs the floor is 6256 (20 runs), 6336, 6368 (3 runs) and 6400, the same +144 spread, so 6352 would fail 4 runs in 25 and the fresh budget is again the envelope: 6400.
- *A landed budget still only tightens.* A landed budget that fails a full run is re-measured over full runs under issue 112 before any change.
- *2026-10-28, the `*AllocationProbeTests` classes joined the collection (M4 track 4, after run 40696270440; settled by the orchestrator 2026-10-28).* `RadioAllocationProbeTests`' "port: the value comparisons allocate nothing for a record struct" measured "896 bytes over 1000 rounds for Point" in that run, outside the collection; a class that holds a budget title under another name (`FocusManagerBehaviorTests`, two titles) joins it too.
- *2026-10-29, the CI runner's +112 B floor shift (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29).* CI runs 40736179291 and 40739478504 failed `CompositeAllocationBudgetTests` (4176 against 4064) and `InteractionsAllocationBudgetTests` (2184 against 2072) on the least of five, with nothing in either measured window changed. The CI runner's floor shift of +112 B survives the least-of-five sampler, so a budget measured before PR 127's rule that shows it is re-pinned to its envelope without a further settlement: Composite 4176 (25 full Release runs: 4064 in 24, 4176 in 1), Interactions 2184 (25 runs: 2072 in 25, with CI's figure as the floor).

**2026-10-27, finding for §B's `[JSImport]` item (M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); [issue 26](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/26); the probe of the M4 note above).** Measured by `tests/browser/harness.spec.ts` "@verifies ADR-16/2 the twin base transport against [JSImport]/[JSExport] on WebAssembly, per message and per hover-and-flip cycle" on the `interactions-transport` fixture (WebAssembly only): a Release host, Chromium under Playwright, macOS arm64; five runs of 30 batches of 500 messages per kind (the interpreter's clock ticks in whole milliseconds), the same JSON both ways and the same work at each end (the module parses an update; C# deserializes a report and a `Decide`'s snapshot through `BlazixJsonContext`). Per message, median (95th percentile) in microseconds, the range across the five runs:

| Message | Twin base | `[JSImport]` / `[JSExport]` |
|---|---|---|
| `update` (as `TwinHandle.UpdateAsync` serializes the record) | 138–146 (142–152) | 0–2 (2) |
| `update` (the same string) | 102 (102–104) | 0–2 (2) |
| report (`invokeMethodAsync`; its un-awaited dispatch share is the same) | 264–274 (266–292) | 126–138 (128–140) |
| `Decide` (synchronous) | 230–244 (232–256) | 98–104 (100–116) |
| hover-and-flip cycle (see below) | 996–1026 | 378–418 |

A hover-and-flip cycle (a hover open and close, two request reports and two session updates, plus one positioning mirror report per flip) costs 996–1026 µs over the twin base against 378–418 µs native: about 0.6 ms of main thread per cycle, and about 0.13 ms per Escape or press `Decide`. No material cost at M4's traffic (settled by the orchestrator 2026-10-27 under the user's plan decision 16): the twins keep the `IJSObjectReference`/`DotNetObjectReference` transport, the issue closes with this finding, and `[JSImport]` is adopted only by an ADR 01/04 amendment if the M5 list-part measurement shows interop dominating (§B as reworded).

**2026-11-05, the §B items decided by the list part measurement (M5 track 6, [issue 172](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/172); design point 4, settled by the orchestrator 2026-11-04 as what the figures support; closes [issue 25](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/25) and [issue 28](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/28)).** At L2, Release, around bUnit's synchronous render (`MenuListMeasureTests`), per item per render: plain 134–157 B and about 5–10 µs; with a `Render` substitute 374–397 B and about the same time; with three unmatched attributes 462–485 B and about 6–11 µs. In a browser on a Release host (`tests/browser/menu-figures.spec.ts` on `menu-measure`), the median time of one render of all the items: Server 1 ms at 100 items and 2–4 ms at 500, plain or with substitutes; WebAssembly 6 ms and 41 ms plain, 9 ms and 55 ms with substitutes.
- *Caching a substitute's scratch-rendered frames: not adopted.* It would save about 240 B per item per render and about 30 µs per item on WebAssembly, so 14 ms for a parent render of 500 items with substitutes there. A highlight move does not pay it: it renders only the items whose mirror changed. A `Render` fragment is a delegate that may close over changed values, so a cache could not tell when its frames went stale without running the fragment.
- *A generated `SetParametersAsync`: not adopted.* A spike with `MenuItem`'s parameters, not kept: `SetParameterProperties` takes about 137 ns with no bag and 320 ns with three unmatched attributes; a hand-written setter takes 62 ns and 190 ns, with the same allocation (0 B and 216 B). The saving is 1–2% of an item's render.
- *The per-item unmatched bag: the accepted cost stands.* Three attributes cost about 330 B per item per render, 216 B of it the bag; an item with none pays nothing.
- *Distinct literal sequence numbers for the part-owned attributes: not adopted, and not measured directly.* It needs a `Rendering/` change, so no spike was run. The whole of a plain item's render above bounds what it could save.
- *`[JSImport]` inside twins: stays not adopted.* The figures above are C#'s render and the batch, with no twin call in them, and track 1's per-key figures (ADR 14) show about two messages per held arrow key.
- These figures are for a menu. Issue 29's per-keystroke budget at 1,000 items (track 10) may reopen them.

**2026-11-10, a budget's envelope covers the per-process variation of dynamic PGO; NavigationMenu's re-render budget is re-pinned to 11,992 bytes (issue-112 rule; chosen by the user 2026-11-10, "Do recommended for decisions"; found when CI run 42769822221 of PR 201, a JS-only change, failed it).** CI measured 11,992 B against a budget of 11,960 (the floor of 11,848 plus Amendments 2026-10-27's 112-byte envelope). The figure varies per process, not per run or per code change. Each process gives five equal samples; on a Release host (macOS arm64), across processes:

| Configuration | Measured (B) | Processes |
|---|---|---|
| default, the title alone | 11,848, or 11,960 | 9 and 1 of 10 |
| `DOTNET_TieredPGO=0` | 11,848 | 8 of 8 |
| `DOTNET_TieredCompilation=0` | 11,584 | 8 of 8 |
| inside three full L2 runs | 11,848 | 3 of 3 |

The variation is dynamic PGO's per-process decisions, not tests that ran earlier. On CI, 35 of the last 40 `build-and-test` jobs passed every budget. Two exceeded a budget by the same 32 B on 2026-11-10, 144 B over their floors: this one, and Combobox's in run 42769069830 (3,056 against 3,024). Rule 7 is loosened for this budget only: its envelope is CI's observed +144, so the budget is 11,992. Other budgets pinned at floor + 112 can fail the same way; widening them to +144, or running the budget collection with dynamic PGO off, is left to each budget's owner and the orchestrator. *Corrected 2026-11-11 (Amendments 2026-11-11, issue 216): the steps are not dynamic PGO's. With PGO off, full runs still show them, and every title run alone measured its floor; the cause is the state earlier tests leave in the process.*

**2026-11-11, the budgets run with dynamic PGO off and one class per process ([issue 216](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/216); settled by the orchestrator 2026-11-11 under the user's "Do recommended for decisions", option (C) of the measurement below; found when the positioner's re-render budget measured 6,408 B against 6,400 in runs 42831892003 (PR 212) and 42842958555 (PR 214), both changing only TypeScript).**

*The measurement.* Release on macOS arm64, `main` at a188a810. Every budget title of both projects was measured: 58 test methods, each figure the least of five in one process. An uncommitted probe recorded the figures of passing titles too.
- Full runs of both projects, one process per run:
  - with PGO on (the default), 5 runs;
  - with PGO off, 20 runs. In 10 of these, the positioner title had a longer warm-up: 120 more renders and a 300 ms pause for the tier-up.
  - with `DOTNET_TieredCompilation=0`, 5 runs.
- Each title alone, one process per test method, with PGO off: 3 rounds.

The titles that varied (bytes, "figure: processes"):

| Title | Budget | PGO on, full runs (5) | PGO off, full runs (20) | `TieredCompilation=0`, full runs (5) | PGO off, alone (3) |
|---|---|---|---|---|---|
| a re-render of a positioner | 6,400 | 6,264: 4, 6,376: 1 | 6,264: 15, 6,344: 3, 6,376: 2 | 6,184: 5 | 6,264: 3 |
| a re-render of a preview card root and its trigger | 2,120 | 2,008: 5 | 2,008: 19, 2,120: 1 | 1,952: 5 | 2,008: 3 |
| port: a re-render of an open dialog | 7,144 | 7,032: 4, 7,112: 1 | 7,032: 20 | 6,864: 5 | 7,032: 3 |
| port: a re-render of a 100-item list | 41,696 | 41,440: 4, 41,520: 1 | 41,440: 19, 41,520: 1 | 41,384: 5 | 41,440: 3 |
| a re-render of an open menu (NavigationMenu) | 11,992 | 11,848: 5 | 11,848: 20 | 11,584: 4, 11,776: 1 | 11,848: 3 |

- The other 53 titles gave one figure in every process under every configuration.
- `TieredCompilation=0` lowers most floors by 24 to 264 B.
- Alone, with PGO off, none of the 174 measurements varied.
- The positioner's steps were the same with the longer warm-up (6,264: 8, 6,344: 1, 6,376: 1 of those 10).

*The reading, which corrects Amendments 2026-11-10.* The per-process steps of +80 to +192 B are not dynamic PGO's decisions.
- PGO off removes the Dialog title's step, but the positioner varied in 5 of 20 processes against 1 of 5.
- A tier-up pause leaves them, and `TieredCompilation=0` has its own.
- A title run alone measures its floor every time.

The cause is the state the earlier tests of a full run leave in the process: bUnit's and the runtime's pools and caches. The positioner budget's own comment already named it. A budget's figure is therefore measured where no earlier test ran.

*The decision.*
1. The two projects that hold budgets, `tests/Blazix.Primitives.Tests` and `tests/Blazix.Primitives.Tests.Components`, build with `<TieredPGO>false</TieredPGO>`, CI and local runs alike. It is kept because it removed one step, and the alone figures above are PGO-off figures.
2. A main `dotnet test` run skips the classes of the `AllocationBudgets` collection: 39 classes, 31 in L2 and 8 in L1. Four landed after the measurement: Menu's `MenuAllocationBudgetTests` and `MenuListMeasureTests` (PR 183), ContextMenu's `ContextMenuAllocationBudgetTests` (PR 228) and Select's `SelectAllocationBudgetTests` (PR 205). Select's is a Razor test component, in the collection by its `@attribute` directive. `.github/scripts/allocation-budgets.mjs` runs each of them in a `dotnet test --filter-class` process of its own (ADR 11 Amendments 2026-11-11). A plain local run of a whole project still runs them inside it, so a budget that fails there is re-checked with the script first.
3. Rule 7 is unchanged, and no budget changes. The step costs:
   - locally, about 90 s for the 39 processes (87 and 94 s), about 2 s each, while the main runs of the three projects take 16 s;
   - on CI, 59 s for the 37 classes before PRs 205 and 228 (run 42853153530); later figures are in the implementing pull request.

*The floors under the new setup* (PGO off, alone), recorded only. Tightening is a separate decision under rule 7. A budget not listed here has no headroom on this host: 34 budgets equal their floor, the two FocusManager titles' 0 B included. The headroom of those listed:

| Title | Budget | Floor | Headroom |
|---|---|---|---|
| a re-render of a 100-item list (list navigation) | 41,696 | 41,440 | 256 |
| a re-render of a root with a list, tabs, an indicator and panels | 2,728 | 2,560 | 168 |
| a re-render of an open menu (NavigationMenu) | 11,992 | 11,848 | 144 |
| a re-render of a positioner | 6,400 | 6,264 | 136 |
| a re-render of a composite root | 4,176 | 4,064 | 112 |
| port: a re-render of an open dialog | 7,144 | 7,032 | 112 |
| a re-render of a popup root and its trigger | 2,184 | 2,072 | 112 |
| port: a re-render of the standalone root (NavigationMenu) | 2,088 | 1,976 | 112 |
| a re-render of a preview card root and its trigger | 2,120 | 2,008 | 112 |
| a re-render of a tooltip root and its trigger | 3,024 | 2,912 | 112 |
| an indicator status report that changes nothing (Checkbox, Radio) | 328 | 272 | 56 |
| a keystroke on an uncontrolled control after the first | 1,024 | 1,016 | 8 |

*A recommendation only.* NavigationMenu's re-render budget can return from +144 to Amendments 2026-10-27's +112, that is 11,960 B. Its figure was its floor, 11,848, in every process measured: 5 with PGO on, 20 with PGO off and 3 alone. CI's 11,992 was measured inside a full run, which the budget step no longer makes. The re-pin should wait until the budget step has passed in several CI runs, and it is the orchestrator's to settle.
