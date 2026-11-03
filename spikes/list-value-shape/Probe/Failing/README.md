# Cases that do not compile

Each case was built alone (copied into `Cases/`, built, removed) with SDK 11.0.100-rc.2.26504.105. `gen/` stands for
`obj/Debug/net11.0/generated/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/`.

| Case | Markup | Diagnostics |
|---|---|---|
| Q2b | `<ListRoot Multiple="true" @bind-Values="list" />` with `List<string> list` | `gen/Cases/Q2b_BindValuesList_razor.g.cs: CS1503: Argument 5: cannot convert from 'EventCallback<List<string>>' to 'EventCallback<IReadOnlyList<string>>'` |
| Q2c | `<ListRoot Multiple="true" @bind-Values="array" />` with `string[] array` | `CS1503: cannot convert from 'EventCallback<string[]>' to 'EventCallback<IReadOnlyList<string>>'` |
| Q3b | `<ListRoot @bind-Value="ns"><Q3bItems /></ListRoot>`, and `Q3bItems.razor` holds `<ListItem />` | `Cases/Q3bItems.razor(2,1): RZ10001: The type of component 'ListItem' cannot be inferred based on the values provided. Consider specifying the type arguments directly using the following attributes: 'TValue'.` The cascading type parameter stops at a child `.razor` component. |
| Q3c | `<ListRoot @bind-Value="ns">@items</ListRoot>` with `RenderFragment items = @<ListItem />;` | `RZ10001` on the `ListItem` of the field, as Q3b. The cascading type parameter does not reach a `RenderFragment` written outside the root's markup. |
| Q4a | `<ListRoot><ListItem Value="@("a")" /></ListRoot>` | `RZ10001: The type of component 'ListRoot' cannot be inferred …`. An item's `Value` does not infer the root (the issue-116 finding H9 again). |
| Q4b | `<ListRoot OnValueChange="Changed" />` with `void Changed(ValueChangeEventArgs<string> args)` | `CS1503: Argument 2: cannot convert from 'method group' to 'EventCallback'`. A method group does not infer `TValue`; a typed lambda does (`Q4c`). |
| Q5b | `<ListRoot Value="@ns" Values="@li" />` with `string? ns` and `IReadOnlyList<int> li` | `CS0411: The type arguments for method 'TypeInference.CreateListRoot_0_CaptureParameters<TValue>(TValue, out TValue, IReadOnlyList<TValue>, out IReadOnlyList<TValue>)' cannot be inferred from the usage.` |
