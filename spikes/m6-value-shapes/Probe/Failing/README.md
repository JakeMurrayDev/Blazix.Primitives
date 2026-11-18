# Cases that do not compile

Each case was built with the others in `Cases/` (SDK 11.0.100, `global.json`) and then moved here. `gen/` stands for
`obj/Debug/net11.0/generated/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/`.

| Case | Markup | Diagnostics |
|---|---|---|
| S04 | `<SliderRoot DefaultValue="@([25, 45])" />` (the range demo's `defaultValue={[25, 45]}`) | `Cases/S04_DefaultValueCollectionExpression.razor(1,29): CS9174: Cannot initialize type 'SliderValues?' with a collection expression because the type is not constructible.` A collection expression does not reach the list case through the union conversion. `S22` shows the form that compiles. |
| S11 | `<SliderRoot @bind-Value="value" />` with `SliderValues value` (not nullable) | `gen/Cases/S11_BindNonNullableUnionField_razor.g.cs(69,383): CS1503: Argument 2: cannot convert from 'EventCallback<SliderValues>' to 'EventCallback'`. The binder infers the callback's type from the field, and `EventCallback<SliderValues>` is not `EventCallback<SliderValues?>`. |
| S12 | `<SliderRoot @bind-Value="value" />` with `double value` | `gen/…S12_BindDoubleField_razor.g.cs(69,383): CS1503: Argument 2: cannot convert from 'EventCallback<double>' to 'EventCallback'`. |
| S13 | `<SliderRoot @bind-Value="value" />` with `double? value` | `gen/…S13_BindNullableDoubleField_razor.g.cs(69,383): CS1503: Argument 2: cannot convert from 'EventCallback<double?>' to 'EventCallback'`. |
| S14 | `<SliderRoot @bind-Value="value" />` with `IReadOnlyList<double> value` | `gen/…S14_BindReadOnlyListField_razor.g.cs(69,383): CS1503: … 'EventCallback<IReadOnlyList<double>>' …`, and `(70,292): CS9369: An expression tree may not contain a union conversion.` (the `ValueExpression` the bind supplies: `IReadOnlyList<double>` reaches `SliderValues` only through the `[Union]` constructor, never a user-defined operator, CS0552). |
| F05 | `<SliderTriplesRoot @bind-Value="value" />` with `double value` (the fallback's `Value : double?`) | `gen/…F05_BindValueDouble_razor.g.cs(69,368): CS1503: Argument 2: cannot convert from 'EventCallback<double>' to 'EventCallback'`. The fallback binds a `double?` field (`F04`), not a `double` one. |
| P07 | `<DrawerRoot SnapPoint="200px" />` | `Cases/P07_SnapPointStringLiteral.razor(1,27): CS1003: Syntax error, ',' expected` and `CS0103: The name 'px' does not exist in the current context`. The value is C#: write `SnapPoint="@("200px")"` (`P03`). |
| R01 | `<LabelPart Label="x" /> <LabelPart Label="0.5" />`, `LabelValue` a hand-written `[Union]` of `string` and `Func<int, string>` | `(1,19): CS0103: The name 'x' does not exist in the current context`; `(1,43): CS1503: Argument 1: cannot convert from 'double' to 'LabelValue?'`. On this SDK a bare attribute value on a hand-written union with a `string` case is C#, not a string literal; the October RC2 spike's R2 saw a literal (`union-binding-spike-2026-10-20.md` §4). |
