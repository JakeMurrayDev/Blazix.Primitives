# tests/Blazix.Primitives.Tests

L1: xUnit with no Blazor. The reference graph enforces the layer rule.

## Inputs

- `../../src/Blazix.Primitives` (the only library reference; never bUnit, never a Blazor test host) and the generators as an analyzer, so the project's own test state records are projected (`Rendering/TestStates.cs`, `[PartState]`) and given their `Copy` and `ShallowEquals` (`Store/TestStates.cs`, `[StoreState]`) as a consumer's would be.
- The `Test:` lines tagged `[L1]` in `docs/components/*.md` and `docs/architecture/*.md`; decision records 06 §1 and 16 rule 7.
- `docs/components/*.md` at test time, for the ownership lint (ADR 11 §5); `Lints/` finds them through `TestSupport/RepositoryRoot`, so `dotnet test` works from the repository root and from this folder.

## Job

Tests what needs no renderer: store and selectors, `MergeProps` and the merge rules, the generated projection, the id generator, event details and reasons, C# timers under `FakeTimeProvider`, and the allocation budgets (`GC.GetAllocatedBytesForCurrentThread()` around a part render, a store notification and a merge; every `*AllocationBudgetTests` class is in the `AllocationBudgets` collection, run with parallelization disabled, `TestSupport/AllocationBudgetsCollection`; issue 112). `AssemblyContractTests` pins the build-level contracts: trim and AOT metadata, `BaseUIVersion`, and that this assembly depends on neither bUnit nor a Blazor test host.

`Lints/` is the ownership lint (ADR 11 §5, `Writer` column of conventions 3.4): `WriterColumn` reads every table whose last column is `Writer` (also composite cells such as `C# (boolean); JS (`"until-found"`)`), `OwnershipRegistry.Declared` is the list of JavaScript-owned attributes by family, part (a whole word of the document heading) and attribute, and `OwnershipLint` compares them. A `JS` or `C# → JS` row without an entry fails; an entry whose row says `C#`, says something else or does not exist fails; every `[MirroredField]` property of a library `[PartState]` record must be named by an entry's `StateField`. A family with no folder under `src/Blazix.Primitives/` is skipped and counted (`The_documents_and_the_ownership_registry_agree` prints the counts), so the lint is green while no family exists. A family's pull request adds its entries to `OwnershipRegistry.Declared`.

## Outputs

- A pass or fail per test through `dotnet test --project tests/Blazix.Primitives.Tests` (Microsoft.Testing.Platform runner, set in `global.json`).
- Run by the `build-and-test` CI job on every change outside `docs/`.

## Human check

- A new test keeps Base UI's title and is shown failing with its fix reverted before it counts (ADR 06 §1).
- No `using Bunit`, no `WebApplicationFactory`; a test that needs a renderer belongs in `Tests.Components`.
- An allocation budget only tightens (ADR 16 rule 7).
