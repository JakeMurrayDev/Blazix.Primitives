Cases that do not compile, kept out of the build (`DefaultItemExcludes` in Probe.csproj) so the others can
run. Errors as reported by SDK 11.0.100-rc.2.26504.105 / csc 5.12.0-1.26504.105:

- A3.razor: RZ10012 Found markup element with unexpected name 'Render' — the child-content element form is
  only offered for parameters typed RenderFragment / RenderFragment<T>, not for a union of them.
- A4.razor: CS0201 (lambda `props => builder => builder.AddContent(...)`; the compiler binds the body
  against a guessed case and reports the body, not the conversion).
- A5.razor: CS1061 'RenderProps<CheckedState>' does not contain a definition for 'AddContent' — the lambda
  `builder => ...` was bound against the RenderFragment<RenderProps<CheckedState>> case, not RenderFragment.
- B3.razor: CS1660 Cannot convert lambda expression to type 'ClassValue<CheckedState>?' because it is not a
  delegate type.
- B4.razor: CS1503 Argument 1: cannot convert from 'method group' to 'Probe.ClassValue<Probe.CheckedState>?'.
- D4.razor: CS0103 The name 'trap' does not exist in the current context (and 'focus') — a non-string
  parameter's attribute value is C#, so the plain string never reaches the struct.
- G2.razor: CS0411 The type arguments for method 'TypeInference.CreateGenericPart_0<TState>(...)' cannot be
  inferred from the usage — TState is not inferred through a union conversion.
- R1.razor: CS0103 The name 'x' does not exist in the current context — `Render="x"` on a union without a
  string case takes the C# path.
- R3.razor: CS0103 The name 'x' does not exist in the current context — `Plain="x"` on a plain struct with
  `implicit operator PlainFromString(string)` also takes the C# path; the literal rule is union-specific.
