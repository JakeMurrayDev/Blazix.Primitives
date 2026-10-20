# src/Blazix.Primitives.Analyzers

Convention analyzers for the library's own code. They never ship.

## Inputs

- Decision record 08 §5 and its Amendments 2026-10-20 (member order of eleven groups; field prefix: `s_` on private statics, none elsewhere, `_x` and `m_x` rejected; `<inheritdoc />` on overriding and implementing members; nullable union parameters), decision record 05 §4 and its union-binding amendment (no boxing where the port controls it; a union never travels through an `object` slot), decision record 16 rules 3 and 9 and §C (every sequence number passed to a `RenderTreeBuilder` is a constant expression; every `CascadingValue` the library renders is `IsFixed`).
- The compilations of `../Blazix.Primitives` and `../Blazix.Primitives.Generators`, which reference this project as an analyzer.
- `Microsoft.CodeAnalysis.CSharp` (pinned in `Directory.Packages.props`); the project targets `netstandard2.0` with a current `LangVersion`.

## Job

Enforces, at build time and with warnings as errors, the conventions that `.editorconfig` cannot express. It reads the library's syntax and symbols and reports diagnostics; it generates nothing. Generated code is not analyzed.

| Id | Analyzer | Reports |
|---|---|---|
| BLZX2001 | `FixedCascadeAnalyzer` | a `CascadingValue<T>` opened without `IsFixed` set to `true` (decision record 16 rule 9) |
| BLZX2002 | `SequenceNumberAnalyzer` | a `RenderTreeBuilder` sequence argument that is not a constant expression (decision record 16 rule 3) |
| BLZX2003 | `MemberOrderAnalyzer` | a member that comes after a member of a later group (decision record 08 §5; placements below) |
| BLZX2004 | `FieldPrefixAnalyzer` | an internal, protected or public field, not a constant, named `_x` or `m_x` (decision record 08 §5; the private cases belong to IDE1006, below) |
| BLZX2005 | `BoxingAnalyzer` | a union converted to `object` or an interface anywhere; any other value type converted to `object` or an interface outside the allow-list below; `Enum.ToString`, `HasFlag`, `Equals(object)`, `GetValues`, `GetNames` (decision record 05 §4, decision record 16 §C) |
| BLZX2006 | `NullableUnionParameterAnalyzer` | a `[Parameter]` or cascading parameter whose type is a union and is not declared `U?` (decision records 05 and 08, Amendments 2026-10-20) |
| BLZX2007 | `InheritDocAnalyzer` | an overriding or implementing method, property or event whose documentation comment is not `<inheritdoc />` with at most `<remarks>` and `<exception>` added (decision record 08 Amendments 2026-10-20) |

`ConventionSymbols` holds the shared symbol tests. A union is a struct marked `System.Runtime.CompilerServices.UnionAttribute` or implementing `System.Runtime.CompilerServices.IUnion` (a declared `union` compiles to the latter), matched by name so the analyzers stay off Roslyn's experimental `ITypeSymbol.IsUnion`. A component parameter carries `[Parameter]` or an attribute deriving from `CascadingParameterAttributeBase` (`[CascadingParameter]`, `[SupplyParameterFromQuery]`, ...).

### Member order (BLZX2003)

Members appear in these groups, in this order; within a group any order is accepted. The rows marked *placement* are where decision record 08 §5 is silent: the M0 track 5 recommendations, chosen by the user on 2026-10-20 (pull request 56; recorded in decision record 08 Amendments 2026-10-20).

1. constants (`const` fields of any accessibility)
2. fields (every other field, static or instance, of any accessibility); field-like and accessor events (*placement*)
3. constructors, the static constructor and the finalizer, after the fields (*placement*; the contributor template already had them there)
4. private properties and indexers (`[Inject]` ones included)
5. parameter properties: `[Parameter]` and cascading parameters, whatever their accessibility
6. public properties and indexers; protected and protected internal ones (*placement*); explicit interface implementations of properties (*placement*)
7. internal properties and indexers; private protected ones (*placement*)
8. lifecycle methods, by name: `SetParametersAsync`, `OnInitialized*`, `OnParametersSet*`, `OnAfterRender*`, `ShouldRender`, and `BuildRenderTree` (*placement*)
9. dispose methods, by name: `Dispose`, `DisposeAsync`, and `DisposeAsyncCore` (*placement*), every overload and accessibility, explicit implementations included
10. public methods; protected and protected internal ones (*placement*); operators and conversion operators (*placement*); explicit interface implementations whose name is not a lifecycle or dispose name (*placement*)
11. internal methods; private protected ones (*placement*)
12. private methods
13. nested types and delegates, last (*placement*)

A member is reported once, against the first member of a later group that precedes it. Each partial declaration is checked on its own.

### Field names (BLZX2004 and IDE1006)

`.editorconfig` raises IDE1006 to a build error for private fields and constants: private instance fields camelCase with no prefix, private static fields (readonly or not) `s_camelCase`, constants PascalCase, `_x` and `m_x` rejected. Probed on SDK 11.0.100-rc.2 in M0 track 5: those rules cover every private field and every constant, and no non-private field; BLZX2004 covers that gap and nothing else. Local constants and locals are not fields and are checked by neither.

### Boxing allow-list (BLZX2005)

A value type may convert to `object` or an interface only into Blazor's own `object` contracts:

- the `object` value of `RenderTreeBuilder.AddAttribute(int, string, object?)` and `RenderTreeBuilder.AddComponentParameter(int, string, object?)`, and the argument of `RenderTreeBuilder.AddMultipleAttributes`;
- an element of the argument array (written out or built for `params`) of an `Invoke*` method of `IJSRuntime` or `IJSObjectReference`, their in-process interfaces (`IJSInProcessRuntime`, `IJSInProcessObjectReference`) and the extension methods on them (`InvokeVoidAsync`, ...);
- a value stored into a dictionary of `string` to `object`, the type of the unmatched attribute bag: an indexer write or `Add`/`TryAdd` on a type implementing `IDictionary<string, object>` or `IReadOnlyDictionary<string, object>`, or the value of a `KeyValuePair<string, object>`.

A union is rejected at those sites too. A value-type operand of a string concatenation (`+`, `+=`) is not a box when its type overrides `ToString`: the compiler emits a constrained `ToString()` call (checked on SDK 11.0.100-rc.2, no `box` opcode), so `"a" + index` passes, while an enum or a struct without its own `ToString` is reported, since that call boxes it at run time; interpolation boxes neither. Calibration on `main` (M0 track 5): 9 hits before the allow-list, 9 after it; 3 fixed, 6 suppressed where a platform contract forces the box (`AttributeList`'s interface enumerator members, `AttributeValue.Value` for `IUnion`, `EventCallback.Equals(object)` in `RenderPipeline`, whose removal is issue 57). The allow-list, the suppressions, the concatenation case and the unseen cases below were chosen by the user on 2026-10-20 (pull request 56; decision record 05 Amendments 2026-10-20). Not seen by the rule: an unconstrained type parameter converted to `object` (not known to be a value type), and a receiver boxed by calling a non-overridden `object` member on a struct.

### Scope

The library runs all seven rules. The generator runs all but BLZX2005, which `NoWarn` turns off in its project file: the boxing rule guards the library at run time, and the generator runs inside the compiler (chosen by the user on 2026-10-20, pull request 56).

## Outputs

- `Blazix.Primitives.Analyzers.dll`, consumed by `src/Blazix.Primitives` and `src/Blazix.Primitives.Generators` with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`; it is not packed and not a package dependency.
- Diagnostics in the library's and the generator's build output; `AnalyzerReleases.Unshipped.md` lists every id (the project never ships, so every rule stays unshipped).
- Tested by `tests/Blazix.Primitives.Tests.Generators` (the Roslyn analyzer testing SDK).

## Human check

- Each analyzer has a test for every violation case, one for the compliant case and one for a suppression, written and shown red before the analyzer.
- A rule cites the decision record that demands it in a `//` comment above its class and its descriptor; the title, message and description state the rule in their own words, the message starts with `Blazix: ` and quotes code in backticks, and none references the specification.
- The analyzers replace review for the library, so every suppression is a `#pragma warning disable` around one member or statement with a `//` comment saying why.
- A placement or allow-list change is a decision for the user, recorded in decision record 08 §5 or 05 §4 and here.
- The project is not referenced from anywhere that packs it.
