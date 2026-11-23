# M6 value-shape probe (M6 track 1)

Throwaway probe for ADR 08 Amendments 2026-11-18 (the Slider, ScrollArea, Drawer and Toast lines) and ADR 05 Amendments
2026-11-18 item 4, run by M6 session U on 2026-11-18 ([issue 272](https://github.com/JakeMurrayDev/Iodine.Headless/issues/272),
the M6 plan's decisions 3 (a) and 12 (a)) before four families shape their public values. It is the issue-116 and M5 track 4
kind (`spikes/payload-generic/`, `spikes/list-value-shape/`) and is excluded from the solution. SDK 11.0.100 (`global.json`),
the SDK's own Razor source generator, `Microsoft.AspNetCore.App` 11.0.0 as a framework reference; `Nullable`, warnings as
errors, `IsTrimmable` and `IsAotCompatible` as the library sets them.

Run it with `dotnet run --project Harness` from this folder; `results.txt` is its output.

- `Probe/Parts/`: the parts as C# classes (ADR 02 decision 4), each elementless, rendering a marker of what it received.
  When a `…Changed` or `OnValueChange` callback is bound, the part invokes it once in `OnInitializedAsync` (Slider: the
  number 50 or the list `[10, 20]`, of the case it received; Drawer: the string `"200px"`), so the marker's `field=` shows
  the bind setter writing the consumer's field.
  - `SliderValues`: a hand-written `[Union]` readonly struct over `double` and `IReadOnlyList<double>`, `IEquatable<T>`,
    `==`, `!=`, element-wise equality by index. Implicit operators from `double`, `double[]` and `List<double>`; C# allows
    none from an interface (CS0552), so `IReadOnlyList<double>` converts through the `[Union]` constructor's union
    conversion. `SliderRoot` takes `Value : SliderValues?`, `ValueChanged : EventCallback<SliderValues?>`,
    `DefaultValue : SliderValues?`, `ValueExpression : Expression<Func<SliderValues?>>?` and
    `OnValueChange : EventCallback<ValueChangeEventArgs<SliderValues>>`, and reads a cascaded `EditContext`, printing
    `FieldIdentifier.Create(ValueExpression)`.
  - `SliderTriplesRoot`: the fallback, `Value : double?` / `ValueChanged : EventCallback<double?>` / `DefaultValue` /
    `ValueExpression : Expression<Func<double?>>?` / `OnValueChange : EventCallback<ValueChangeEventArgs<double>>`, and
    `Values : IReadOnlyList<double>?` / `ValuesChanged` / `DefaultValues` / `ValuesExpression` / `OnValuesChange`, with a
    `ListValueParameters.Check`-like check in `SetParametersAsync` that rejects a parameter of both triples.
  - `SliderValuesBuildable` and `SliderBuildableRoot`: an option beside the brief's shape, the union made a collection type
    (`[CollectionBuilder]` plus `IEnumerable<double>`), for the demo's collection expression.
  - `SnapPointValue` (`[Union]` over `double` and `string`) on `DrawerRoot` (`SnapPoints : IReadOnlyList<SnapPointValue>?`,
    `SnapPoint : SnapPointValue?`, `SnapPointChanged : EventCallback<SnapPointValue?>`, `DefaultSnapPoint`).
  - `OverflowEdgeThresholdValue` (`[Union]` over `double` and the record
    `ScrollAreaOverflowEdges(double? XStart, double? XEnd, double? YStart, double? YEnd)`) on
    `ScrollAreaRoot.OverflowEdgeThreshold : OverflowEdgeThresholdValue?`.
  - Toast: `ToastObject<TData>` (a record) and `ToastObject : ToastObject<NoPayload>`, `ToastManager<TData>` (with `Toasts`
    and a `Changed` event) and `ToastManager : ToastManager<NoPayload>`, `ToastRoot<TData>` with
    `Toast : ToastObject<TData>`, the non-generic `ToastTitle`, `ToastDescription` and `ToastClose`, and `ToastViewport`
    with `ChildContent : RenderFragment<IReadOnlyList<ToastObject>>` (its `Toasts` parameter stands in for the Provider's
    manager).
  - `LabelValue` on `LabelPart`: a `string | Func<int, string>` union, to check the Razor literal rule on this SDK.
- `Probe/Cases/`: the consumers that compile; `Harness/` renders each through `HtmlRenderer` and prints the markers, then
  three equality checks.
- `Probe/Failing/`: the consumers that do not compile, with their diagnostics (`Failing/README.md`).

## Finding

| Shape | Case | Result |
|---|---|---|
| **Slider, the union** | numeric literal | Works. `Value="25"`, `DefaultValue="25"` and `Value="25.5"` are C# expressions converted to `SliderValues?` and hold the number case (`S01`..`S03`). |
| | a list | One-way works from a `double[]` field, a `List<double>` field, an `IReadOnlyList<double>` field and `@(new double[] { 25, 45 })` (`S05`..`S08`). The demo's collection expression `DefaultValue="@([25, 45])"` does not compile: `CS9174 … the type is not constructible` (`Failing/S04`). The same union with `[CollectionBuilder]` and `IEnumerable<double>` accepts it (`S22`). |
| | `@bind-Value` | Works against a `SliderValues?` field, number and range, with `@bind-Value:after` (`S09`, `S10`, `S15`): the setter writes the field. Does not compile against a `SliderValues`, `double`, `double?` or `IReadOnlyList<double>` field (`CS1503`, `Failing/S11`..`S14`; `S14` also `CS9369`). The binder needs the field's type to be exactly `ValueChanged`'s, so the nullable `EventCallback<SliderValues?>` is the form that binds a `SliderValues?` field; a non-nullable callback would bind only a `SliderValues` field and its `ValueExpression` would carry a conversion. |
| | `ValueExpression` | Works as `@bind` supplies it, and inside an `EditForm` gives `FieldIdentifier` `Model.Volume` for a `SliderValues?` property (`S16`); an explicit `ValueExpression="@(() => value)"` works (`S21`). For a `double` property, `ValueExpression="@(() => model.Gain)"` compiles but holds `Convert(Convert(model.Gain, SliderValues), Nullable)`, and `FieldIdentifier.Create` throws `ArgumentException: … contains a UnaryExpression which is not supported` (`S17`). For an `IReadOnlyList<double>` property it does not compile (`CS9369`, `S14`). |
| | `OnValueChange` | A typed lambda, an untyped lambda and a method group all compile and receive the value (`S18`..`S20`); the root is not generic. |
| | equality | `[25, 45]` from an array equals `[25, 45]` from a list; `[25, 45]` differs from `25`; 1,000 comparisons allocate 0 B (the harness). |
| **Slider, the fallback** | | Works. `Value="25"`, `DefaultValues="@([25, 45])"`, `Values` from a `double[]` and a `List<double>` (`F01`..`F03`); `@bind-Value` against a `double?` field and `@bind-Values` against an `IReadOnlyList<double>` field, each with `:after` (`F04`, `F06`); inside an `EditForm` both give a `FieldIdentifier` (`Model.Volume`, `Model.Range`, `F07`); `OnValueChange` as a lambda and `OnValuesChange` as a method group (`F08`); a use that supplies `Value` and `Values` throws at the parameter set (`F09`). `@bind-Value` against a `double` field does not compile (`CS1503`, `Failing/F05`). |
| **Drawer, the snap point** | | Works. `SnapPoint="0.5"` is a C# expression and holds the number (`P01`); `SnapPoint="@(0.5)"`, `DefaultSnapPoint="@(1)"` (an `int` through `double`) and `SnapPoint="@("200px")"` (`P02`, `P03`); `SnapPoints="@([0.5, "200px", 1])"`, a `SnapPointValue[]` and a `List<SnapPointValue>` (`P04`, `P05`); `@bind-SnapPoint` against a `SnapPointValue?` field (`P06`). `SnapPoint="200px"` does not compile (`CS1003`, `CS0103`, `Failing/P07`). Two equal lists that are different instances compare equal by sequence and not by reference, and the string `"1"` does not equal the number `1` (the harness). |
| **ScrollArea, the threshold** | | Works. `OverflowEdgeThreshold="20"` (an `int` literal through the standard conversion to `double`, the user-defined conversion and the lift to the nullable union), `"20.5"`, a `ScrollAreaOverflowEdges` expression and field (`A01`..`A04`). |
| **Toast, inference** | | Works. `<ToastRoot Toast="toast">` infers `MyData` in a `@foreach` over an `IReadOnlyList<ToastObject<MyData>>` (`T01`) and `NoPayload` over an `IReadOnlyList<ToastObject>` (`T02`) and inside `ToastViewport`'s `RenderFragment<IReadOnlyList<ToastObject>>` context (`T03`, `T05`); the non-generic children need no type argument. A consumer of a `ToastManager<MyData>` reaches `ToastRoot<MyData>` only by iterating the manager's own `IReadOnlyList<ToastObject<MyData>>` and re-rendering on its `Changed` event, outside the viewport or inside it while ignoring its context (`T04`, `T06`): a `ToastObject<MyData>` is not a `ToastObject`, so the viewport's non-generic list cannot carry it. |
| **The Razor literal rule** | | On this SDK a bare attribute value on a hand-written `[Union]` with a `string` case is a C# expression: `Label="x"` is `CS0103` and `Label="0.5"` is `CS1503` (`Failing/R01`); `Label="@("x")"` binds (`R02`). The October spike under RC2 saw a string literal for its hand-written `string | TrapFocusLiteral` union (`union-binding-spike-2026-10-20.md` §4, R2). |

The outcome per the M6 plan's decision 12 (a) is in ADR 08 Amendments 2026-11-18, the line "Track 1's probe, 2026-11-18".
