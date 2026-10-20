# tests/Blazix.Primitives.Tests

L1: xUnit with no Blazor. The reference graph enforces the layer rule.

## Inputs

- `../../src/Blazix.Primitives` (the only library reference; never bUnit, never a Blazor test host) and the generators as an analyzer, so the project's own test state records are projected (`Rendering/TestStates.cs`, `[PartState]`) and given their `Copy` and `ShallowEquals` (`Store/TestStates.cs`, `[StoreState]`) as a consumer's would be.
- The `Test:` lines tagged `[L1]` in `docs/components/*.md` and `docs/architecture/*.md`; decision records 06 §1 and 16 rule 7.
- `docs/components/*.md` at test time, for the ownership lint (ADR 11 §5).

## Job

Tests what needs no renderer: store and selectors, `MergeProps` and the merge rules, the generated projection, the id generator, event details and reasons, C# timers under `FakeTimeProvider`, and the allocation budgets (`GC.GetAllocatedBytesForCurrentThread()` around a part render, a store notification and a merge). `AssemblyContractTests` pins the build-level contracts: trim and AOT metadata, `BaseUIVersion`, and that this assembly depends on neither bUnit nor a Blazor test host.

## Outputs

- A pass or fail per test through `dotnet test --project tests/Blazix.Primitives.Tests` (Microsoft.Testing.Platform runner, set in `global.json`).
- Run by the `build-and-test` CI job on every change outside `docs/`.

## Human check

- A new test keeps Base UI's title and is shown failing with its fix reverted before it counts (ADR 06 §1).
- No `using Bunit`, no `WebApplicationFactory`; a test that needs a renderer belongs in `Tests.Components`.
- An allocation budget only tightens (ADR 16 rule 7).
