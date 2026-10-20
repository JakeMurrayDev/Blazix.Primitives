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
* `[JSImport]` transport inside twins on WebAssembly: only if M4–M6 measurements show interop cost on WebAssembly, and then as an ADR 01/04 amendment, since twins would become the one mode-dependent place.

### C. Rejected

* `ShouldRender` based on parameter comparison (`ChildContent` defeats it; a part that skips may hide a changed child).
* Branching component code on render mode for speed (ADR 01 decision 1).
* Reflection caches for projection or attribute bags (ADR 05's amendment of ADR 02).

## Consequences

* The allocation-budget tests are part of M0 (ADR 12) and run at L1 on every change (ADR 11).
* Rule 1 makes trimming and AOT compatibility a library guarantee rather than a consumer's discovery.

## Verification items

* The two items of §B, tracked in ADR 14.

## Considered options

* The techniques of §C, recorded so nobody "optimises" a part into one of them.
