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
* `[JSImport]` transport inside twins on WebAssembly: only if M4–M6 measurements show interop cost on WebAssembly, and then as an ADR 01/04 amendment, since twins would become the one mode-dependent place.

### C. Rejected

* `ShouldRender` based on parameter comparison (`ChildContent` defeats it; a part that skips may hide a changed child).
* Branching component code on render mode for speed (ADR 01 decision 1).
* Reflection caches for projection or attribute bags (ADR 05's amendment of ADR 02).

## Consequences

* The allocation-budget tests are part of M0 (ADR 12) and run at L1 on every change (ADR 11).
* Rule 1 makes trimming and AOT compatibility a library guarantee rather than a consumer's discovery.

## Verification items

* The two items of §B, tracked in ADR 14 ([issue 25](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/25), [issue 26](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/26)).
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

**2026-10-27, the popup store's reconciliation runs at the first popup part's render boundary of the batch (M4 track 1, [issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117); design point 8, settled by the orchestrator 2026-10-27 as the worker recommended, under the Amendments 2026-10-27 above and ADR 05 §1; the user was told and may overturn; the dated notes on PSH-STA-16, PSH-STA-18, PSH-STA-23 and PSH-ALG-03 are the implementing pull request's).** The brief placed PSH-ALG-03's reconciliation in the Root's `OnAfterRender` with the Root rendering on every store write that changes its inputs (open, the trigger count, the active trigger's id and element). In Blazor a component that hosts consumer content re-renders every child whose parameters include a `RenderFragment`, because `ChangeDetection` treats delegates as always changed and a `CascadingValue` always re-renders its content (measured with a bUnit probe: a host's `StateHasChanged` re-rendered the child holding `ChildContent`, not the child holding a string), and every popup part takes `ChildContent`; so that wiring would re-render the whole Root content, inactive triggers included, on every open, close, trigger switch and trigger mount while open, the cost Base UI 1.9.0 removed in React with an internal leaf component that rule 10 forbids here. The store therefore marks the reconciliation pending when a write changes its inputs, and the Root's `OnAfterRender` and the shared trigger behaviour's `OnAfterRender` run it when it is pending: every such write renders at least one popup part (the requesting or registering trigger, a trigger whose `aria-expanded` or `aria-controls` changed, or the Root for its own parameters), and the one write that renders none, a registered trigger disposed while registered, asks the Root to render. PSH-STA-23's close is requested at the boundary that detects the loss, after the batch's parameter sets have all run. The Root subscribes only to the selections its own parameters need, so a selection renders only the parts whose selection changed, as the amendment above requires and the L2 render counts prove. The alternative, the brief's wiring with the claim narrowed to writes that do not touch the Root's inputs, was rejected for one extra render of every content part per open, close or trigger switch.
