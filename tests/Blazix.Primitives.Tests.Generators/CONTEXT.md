# tests/Blazix.Primitives.Tests.Generators

Tests for the generator and the analyzers, on the Roslyn testing SDK.

## Inputs

- `../../src/Blazix.Primitives.Generators` and `../../src/Blazix.Primitives.Analyzers` (project references).
- `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` and `...Analyzer.Testing`, pinned in `Directory.Packages.props` with the compiler packages they must match.
- Decision records 05, 08 §5 and 16 rules 3 and 9 for the behaviour under test.

## Job

Runs each generator and analyzer against source text and compares the diagnostics and the generated output with the expected ones: `CSharpSourceGeneratorTest` for the generator, `CSharpAnalyzerTest` for the analyzers. A violation test and a compliant test per rule. The reference assemblies come from the testing SDK, which downloads them on first use.

## Outputs

- A pass or fail per test through `dotnet test --project tests/Blazix.Primitives.Tests.Generators`.
- Run by the `build-and-test` CI job on every change outside `docs/`.

## Human check

- Expected output is written out in the test, not regenerated from the generator.
- The Roslyn packages stay on one version; an NU1608 means they drifted apart.
- Every new rule or diagnostic arrives with its test in the same pull request.
