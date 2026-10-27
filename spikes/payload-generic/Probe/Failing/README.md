# Cases that do not compile

Each case was built alone (copied into `Cases/`, built, removed) with SDK 11.0.100-rc.2.26504.105. `gen/` stands for
`obj/Debug/net11.0/generated/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/`.

| Case | Markup | Diagnostics |
|---|---|---|
| P1 | `<PopRoot DefaultOpen="true"><PopTrigger>open</PopTrigger></PopRoot>` (a parameter both `PopRoot` and `PopRoot<TPayload>` declare) | `RZ10012: The component 'PopRoot' cannot be disambiguated between generic and non-generic versions. Provide a type parameter (e.g., TItem="...") to use the generic component, or use different parameter names to avoid ambiguity. Components: Probe.Parts.PopRoot<TPayload>, Probe.Parts.PopRoot` |
| G1 | `<PopRoot Handle="@handle" DefaultOpen="true">…` (a generic-only parameter beside a shared one) | `RZ10012`, as P1 |
| G2 | `<PopRoot TPayload="string" DefaultOpen="true">…` (explicit type argument and a shared parameter) | `RZ10009: The component parameter 'DefaultOpen' is used two or more times for this component.` The generated code opens `PopRoot<string>` and adds `DefaultOpen` twice, once per matched component. |
| G4 | `<PopRoot Handle="@handle"><PopTrigger>open</PopTrigger></PopRoot>` (only the generic-only parameter) | `CS1503: cannot convert from 'PopHandle<int>' to 'PopHandle<TPayload>'`; `gen/Cases/G4_razor.g.cs: CS0246: The type or namespace name 'TPayload' could not be found`. The generated code opens the **non-generic** `PopRoot` and binds `nameof(PopRoot<TPayload>.Handle)` with `TypeCheck<PopHandle<TPayload>>`: no type inference runs. |
| G3 | as G4, plus `<PopTrigger Payload="7">` | G4's errors, plus `CS1503: cannot convert from 'int' to 'TPayload'` on the Trigger; the cascaded type parameter does not reach the dual-named `PopTrigger`. |
| G5 | detached `<PopTrigger Handle="@handle" Payload="3">` | `CS1503` on `Handle` and `Payload`, `CS0246 'TPayload'` (as G4). |
| G6 | `<PopTrigger Payload="@("x")">` | `CS1503: cannot convert from 'string' to 'TPayload'`, `CS0246 'TPayload'`. |
| G7 | `<PopRoot Handle="@handle"><span>@context</span></PopRoot>` | G4's errors, plus `CS0103: The name 'context' does not exist`: the child content was emitted as a plain `RenderFragment`. |
| G8 | nested `<PopRoot Handle="@outer"><PopRoot Handle="@inner">…` | G4's errors, twice. |
| H6 | nested generic-only `<GenRoot Handle="@outer"><GenRoot Handle="@inner">…` with `RenderFragment<TPayload?>` child content and no `Context` | `RZ9999: The child content element 'ChildContent' of component 'GenRoot' uses the same parameter name ('context') as enclosing child content element 'ChildContent' of component 'GenRoot'. Specify the parameter name like: '<ChildContent Context="another_name"> to resolve the ambiguity` |
| H8 | `<GenRoot DefaultOpen="true"><GenTrigger>open</GenTrigger></GenRoot>` (generic-only, no type-bearing parameter, no type argument) | `RZ10001: The type of component 'GenTrigger' cannot be inferred …`; `CS0411` on `CreateGenRoot_0_CaptureParameters<TPayload>(bool, out bool)` |
| H9 | `<GenRoot DefaultOpen="true"><GenTrigger Payload="@("a")">…` (a child's `Payload` does not infer the Root) | `CS0411` on the Root, as H8 |
