# src/Blazix.Primitives.Generators

The Roslyn source generator that ships to consumers inside the library's package.

## Inputs

- Decision record 02 as amended by 05 (state-to-`data-*` projection by source generator, RND-ALG-07) and decision record 09 §1.
- The consumer's compilation: records marked `[PartState]`, in the library and in consumer code.
- `Microsoft.CodeAnalysis.CSharp` (pinned in `Directory.Packages.props`); the project targets `netstandard2.0` with a current `LangVersion`.

## Job

Emits typed code that projects each `[PartState]` record to its `data-*` attributes in declaration order, so no reflection runs at render time (the ADR 02 amendment recorded in ADR 05), and reports the consumer-facing diagnostics. It contains no component behaviour and no rendering code.

## Outputs

- `Blazix.Primitives.Generators.dll`, packed into `Blazix.Primitives` under `analyzers/dotnet/cs` and referenced by the library as an analyzer (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`).
- Generated sources inside the consumer's compilation; nothing is written to disk.
- Tested by `tests/Blazix.Primitives.Tests.Generators`.

## Human check

- The generator is incremental (`IIncrementalGenerator`) and takes no dependency beyond the compiler.
- Every diagnostic it reports has an id, a message and a test in `Tests.Generators`.
- The generated output is stable and deterministic for the same input.
- Only this project is packed; `../Blazix.Primitives.Analyzers` never is.
