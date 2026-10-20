# src/Blazix.Primitives.Analyzers

Convention analyzers for the library's own code. They never ship.

## Inputs

- Decision record 08 §5 (member order of eleven groups, field prefix (`s_` on private statics, none elsewhere), boxing) and decision record 16 rules 3 and 9 (every sequence number passed to a `RenderTreeBuilder` is a constant expression; every `CascadingValue` the library renders is `IsFixed`).
- The library's compilation, as an analyzer reference from `../Blazix.Primitives`.
- `Microsoft.CodeAnalysis.CSharp` (pinned in `Directory.Packages.props`); the project targets `netstandard2.0` with a current `LangVersion`.

## Job

Enforces, at build time and with warnings as errors, the conventions that `.editorconfig` cannot express. It reads the library's syntax and symbols and reports diagnostics; it generates nothing.

## Outputs

- `Blazix.Primitives.Analyzers.dll`, consumed by `src/Blazix.Primitives` with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`; it is not packed and not a package dependency.
- Diagnostics in the library's build output.
- Tested by `tests/Blazix.Primitives.Tests.Generators` (the Roslyn analyzer testing SDK).

## Human check

- Each analyzer has a test for the violation and one for the compliant case.
- A new rule cites the decision record that demands it in its description.
- The member-order and field-prefix analyzers replace review for the library, so a suppression needs a comment saying why.
- The project is not referenced from anywhere that packs.
