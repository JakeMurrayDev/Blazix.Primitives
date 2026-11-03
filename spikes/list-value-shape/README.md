# List value-shape probe (M5 track 4)

Throwaway probe for ADR 08 Amendments 2026-11-03 (1), run by M5 track 4 on 2026-11-04
([issue 162](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/162)) before the value layer and the wave-2 roots
are shaped. It is the issue-116 kind (`spikes/payload-generic/`) and is excluded from the solution. SDK
11.0.100-rc.2.26504.105 (`global.json`), the SDK's own Razor source generator, `Microsoft.AspNetCore.App` 11.0.0-rc.2 as a
framework reference.

- `Probe/Parts/`: the parts as C# classes (ADR 02 decision 4).
  - `ListRoot<TValue>`: `SelectRoot<TValue>` and `ComboboxRoot<TValue>`'s value shape. It has `[CascadingTypeParameter]` and:
    - the single-mode triple `Value`/`ValueChanged`/`DefaultValue`, `OnValueChange` and `ValueExpression`;
    - the multiple-mode triple `Values`/`ValuesChanged`/`DefaultValues`, `OnValuesChange` and `ValuesExpression`, over `IReadOnlyList<TValue>`;
    - `bool Multiple`, `Items : IReadOnlyList<ProbeItem<TValue>>?` and `ChildContent : RenderFragment?`.

    It is elementless: it renders a marker of what it received, and its `SetParametersAsync` rejects a triple that does not match `Multiple`.
  - `ListItem<TValue>`: `Value`, with the supplied flag.
  - `ListPortal`, `ListPositioner`, `ListPopup`: non-generic wrappers.
  - `ValueChangeEventArgs<TValue>`: the library type's shape.
  - `ProbeItem<TValue>(TValue Value, string Label)`, `Fruit` (an enum) and `Person` (a record class).
- `Probe/Cases/`: the consumers that compile. `Harness/` renders each through `HtmlRenderer` and prints what each root and
  item received (`results.txt`).
- `Probe/Failing/`: the consumers that do not compile, with their diagnostics (`Failing/README.md`).

## Finding

**The shape holds.** Every pass condition of the brief is met.

| Question | Result |
|---|---|
| 1. `TValue` inferred from each parameter | Yes, from each of `Value`, `DefaultValue`, `@bind-Value`, `Values`, `DefaultValues`, `@bind-Values` and `Items`, for `string`, `string?`, `int`, `int?`, an enum and a record class (`Q1a`..`Q1g`). A literal needs C# syntax: `Value="@("apple")"` gives `string`, `Value="3"` gives `int`. |
| 2. `@bind` against the natural field types | Yes. `@bind-Value` compiles against `string?`, `int?`, the enum and the record. `@bind-Values` compiles against `IReadOnlyList<T>`. Each bind supplies its expression (`ValueExpression`, `ValuesExpression`), and `@bind-Value:after` and `@bind-Values:after` compile (`Q1c`, `Q1f`). `@bind-Values` against a `List<string>` or `string[]` field does not compile: `CS1503`, the binder's `EventCallback<List<string>>` is not an `EventCallback<IReadOnlyList<string>>` (`Q2b`, `Q2c`). The one-way `Values` accepts a `List<string>`, a `string[]` and a collection expression (`Q2a`, `Q2d`; the collection expression gives `string`). |
| 3. `[CascadingTypeParameter]` through wrappers | It reaches items nested three non-generic wrappers deep in one file, inside a `@foreach` too (`Q3a`). It stops at a child `.razor` component (`Q3b`) and at a `RenderFragment` written outside the root's markup (`Q3c`), both `RZ10001`. There an item names `TValue` or infers it from its own `Value`, and it still finds the root's cascaded value (`Q3d`, `Q3e`). For a value type this means the exact type: an item's `Value="1"` is `int`, and it does not find an `int?` root. |
| 4. Uses that need an explicit `TValue` | A root with no inferring parameter (`Q4a`, `RZ10001`; an item's `Value` does not infer the root, as issue 116's H9). `OnValueChange` alone as a method group (`Q4b`, `CS1503: cannot convert from 'method group' to 'EventCallback'`). `OnValueChange` alone as a lambda with a typed parameter infers (`Q4c`). |
| 5. Both triples on one component | No diagnostic from declaring both. A use that supplies both of one type compiles (`Q5a`, `Q5c` with both binds), and the parameter-set check rejects it at run time. A use that supplies both of different types is `CS0411` (`Q5b`). |
| 6. The supplied triple told at the parameter set | Yes. `SetParametersAsync` reads the names in the `ParameterView` before any assignment. `Multiple` with a single-mode parameter (`Value`, `ValueChanged`, `DefaultValue`, `OnValueChange`, `ValueExpression`) or no `Multiple` with a multiple-mode one throws `InvalidOperationException` (`Q5a`, `Q5c`, `Q6a`..`Q6c`). The messages are in `ListRoot.cs`; `Q6d` renders the matched uses. |
| 7. An item's omitted `Value` against `default` for `int` | Distinguished: `parameters.TryGetValue<TValue>(nameof(Value), out _)` is false for `<ListItem />` and true for `<ListItem Value="0" />` under `TValue="int"` (`Q4d`). |
