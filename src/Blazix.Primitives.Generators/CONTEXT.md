# src/Blazix.Primitives.Generators

The Roslyn source generators that ship to consumers inside the library's package.

## Inputs

- Decision record 02 as amended by 05 (state-to-`data-*` projection by source generator, RND-ALG-07) and decision record 09 §1.
- Decision record 05 §1 and 16 rule 1 for the store state record's `Copy` and `ShallowEquals` (UTL-STA-10..12): the store cannot copy or compare a record without reflection, so they are generated.
- The consumer's compilation: records marked `[PartState]` or `[StoreState]`, in the library and in consumer code.
- `Microsoft.CodeAnalysis.CSharp` (pinned in `Directory.Packages.props`); the project targets `netstandard2.0` with a current `LangVersion`.

## Job

Emits typed code that projects each `[PartState]` record to its `data-*` attributes in declaration order, so no reflection runs at render time (the ADR 02 amendment recorded in ADR 05), and reports the consumer-facing diagnostics. `PartStateGenerator` implements `Blazix.Primitives.Rendering.IPartState<TSelf>` on every `sealed partial record` marked `[PartState]`: a bool emits `data-<name>=""` when true, a number or char its invariant string when non-zero, a string when non-empty, an enum the kebab-case form of its member, nullable forms when they have a value; a property marked `[MirroredField]` is skipped; a custom mapping for the field (`StateAttributesMapping<TState>.TryProject`) replaces the default. Diagnostics: BLZX1001 not a record class, BLZX1002 not sealed, BLZX1003 a property that cannot be projected, BLZX1004 the record or a containing type not partial.

`StoreStateGenerator` writes `Copy` and `ShallowEquals`, the two static members of `Blazix.Primitives.Store.IStoreState<TSelf>`, into every `sealed partial record` marked `[StoreState]`, so no state record writes them by hand. `Copy` returns `state with { }`. `ShallowEquals` joins with `&&`, in declaration order, one comparison per instance field of the record, which is exactly what `with` copies. A positional, auto-implemented, get-only or init-only property is read through the property, and an explicit field directly, whatever its accessibility. A computed property has no field and a static member no per-record value, so neither takes part, and a record with no such member compares equal.

Each comparison is the store's `Equality.Is` rule (`Object.is` by the static type), written for the member's own type so that it calls no generic method and no `EqualityComparer<T>`. Both box a value type on the WebAssembly interpreter (Track 3's measurement: 32 bytes per comparison for `int`, `long`, `double` and `bool`). The comparisons are:

| Member type | Comparison |
|---|---|
| `bool`, a number other than floating point, `char`, `decimal`, `nint`, an enum | `==`, and the lifted `==` for the nullable form |
| `double`, `float`, `Half` | identical bits or both `NaN`, inlined (`BitConverter.DoubleToInt64Bits`, `double.IsNaN`). `==` would make `NaN` differ from itself and `0` equal `-0` |
| `string` | `string.Equals(a, b)`, ordinal |
| any other reference type: a record, a store, a class, an interface, `object`, a delegate, an array | `object.ReferenceEquals` |
| Blazor's `ElementReference` | `string.Equals` on `Id` and `object.ReferenceEquals` on `Context`: an element handle is identity, the same element from the same renderer, as `Object.is` on a DOM node is. `ElementReference` has neither `==` nor `IEquatable<T>`, and a popup store holds several, so it is named here rather than left to boxing |
| a struct with an `==` operator taking two of it (`Guid`, a record struct) | `==`, lifted for the nullable form |
| a struct with only `IEquatable<T>` (a tuple, which has no `==` operator in metadata) | `a.Equals(b)` |
| a struct with neither | `Equality.Is`, which boxes it, with warning BLZX1011 (`EventCallback` is one: it exposes nothing comparable without boxing) |
| a type parameter | `Equality.Is`, because how it compares is known only at instantiation |

The nullable form of a floating-point type, of `ElementReference` and of an `IEquatable`-only struct is `left.X is { } leftX ? right.X is { } rightX && <the comparison of leftX and rightX> : right.X is null`. Where a struct has both an `==` operator and `IEquatable<T>`, `==` is used: it is the shorter form and is lifted for nullable members.

Diagnostics: BLZX1005 not a record class, BLZX1006 not sealed, BLZX1007 the record or a containing type not partial, BLZX1008 the record does not implement `IStoreState` of itself, BLZX1009 the record declares `Copy` or `ShallowEquals` itself (that member is then not generated), BLZX1010 the record derives from another record, whose members it could not compare, all errors, and BLZX1011, a warning, for a struct member compared through `Equality.Is`. After BLZX1006, BLZX1008 and BLZX1010 the members are still generated, so the record's diagnostic is the only error rather than a cascade of unimplemented-member errors.

`DiagnosticInfo` carries a diagnostic through the incremental pipeline, and `PartialDeclarations` holds what both generators need to write a partial declaration back into a type nested in partial containers. `AnalyzerReleases.*.md` track the rule ids (RS2008). The project contains no component behaviour and no rendering code.

## Outputs

- `Blazix.Primitives.Generators.dll`, packed into `Blazix.Primitives` under `analyzers/dotnet/cs` and referenced by the library as an analyzer (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`).
- Generated sources inside the consumer's compilation; nothing is written to disk.
- Tested by `tests/Blazix.Primitives.Tests.Generators`.

## Human check

- Each generator is incremental (`IIncrementalGenerator`) and takes no dependency beyond the compiler.
- Every diagnostic they report has an id, a message and a test in `Tests.Generators`.
- The generated output is stable and deterministic for the same input.
- Only this project is packed; `../Blazix.Primitives.Analyzers` never is.
