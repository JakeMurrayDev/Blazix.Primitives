# tests/Blazix.Primitives.Tests.Generators

Tests for the generator and the analyzers, on the Roslyn testing SDK.

## Inputs

- `../../src/Blazix.Primitives.Generators` and `../../src/Blazix.Primitives.Analyzers` (project references).
- `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` and `...Analyzer.Testing`, pinned in `Directory.Packages.props` with the compiler packages they must match.
- Decision records 05 §4, 08 §5 (with their Amendments 2026-10-20) and 16 rules 3 and 9 and §C for the behaviour under test; `src/Blazix.Primitives.Analyzers/CONTEXT.md` for the analyzers' placements and allow-list.

## Job

Runs each generator and analyzer against source text and compares the diagnostics and the generated output with the expected ones: `CSharpSourceGeneratorTest` for the generator, `CSharpAnalyzerTest` for the analyzers. A violation test per case, a compliant test and a suppression test per rule. The reference assemblies come from the testing SDK, which downloads them on first use: .NET 10 with `Microsoft.AspNetCore.Components` (and `Microsoft.JSInterop`) 10.0, the newest it resolves. Two consequences for the analyzer tests: the union tests declare `System.Runtime.CompilerServices.UnionAttribute` and `IUnion` in their source (they ship with .NET 11) and set the language version to preview for a declared `union`; the `<inheritdoc />` tests pin the documentation mode to `Diagnose`, as the library builds.

## Outputs

- A pass or fail per test through `dotnet test --project tests/Blazix.Primitives.Tests.Generators`.
- Run by the `build-and-test` CI job on every change outside `docs/`.

## Human check

- Expected output is written out in the test, not regenerated from the generator.
- The Roslyn packages stay on one version; an NU1608 means they drifted apart.
- Every new rule or diagnostic arrives with its test in the same pull request.
