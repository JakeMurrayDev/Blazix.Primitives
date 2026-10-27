# Payload type parameter probe (issue 116)

Throwaway probe for ADR 08 Amendments 2026-10-27's payload bullet and ADR 14's row for it
([issue 116](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/116)), run by M4 track 1 on 2026-10-27 before the
popup parts were shaped. Excluded from the solution (ADR 14 §3). SDK 11.0.100-rc.2.26504.105 (`global.json`), the SDK's
own Razor source generator, `Microsoft.AspNetCore.App` 11.0.0-rc.2 as a framework reference.

- `Probe/Parts/`: the parts as C# classes (ADR 02 decision 4). `PopRoot` and `PopRoot<TPayload>`, `PopTrigger` and
  `PopTrigger<TPayload>`: a non-generic and a generic component of one tag name, the generic ones with
  `[CascadingTypeParameter]`, `Handle : PopHandle<TPayload>?`, `Payload : TPayload?` and, on the Root,
  `ChildContent : RenderFragment<TPayload?>?`. `GenRoot<TPayload>` and `GenTrigger<TPayload>`: the same generic parts with
  no non-generic twin. `TplRoot<TPayload>`: the second shape, `ChildContent : RenderFragment?` beside
  `PayloadContent : RenderFragment<TPayload?>?`. `NoPayload`: the marker record.
- `Probe/Cases/`: the consumers that compile; `Harness/` renders each through `HtmlRenderer` and prints which component
  each tag resolved to (`results.txt`).
- `Probe/Failing/`: the consumers that do not compile, with their diagnostics (`Failing/README.md`).

## Finding

**(i) fails.** The RC2 Razor compiler accepts a non-generic and a generic component of one tag name in one project and
has a rule for choosing between them, but the rule does not serve the payload shape:

| Use | Result |
|---|---|
| no attribute, or child content only (`P2`, `P3`) | the non-generic component |
| any parameter both declare, such as `DefaultOpen` or `Open` (`P1`, `G1`) | `RZ10012`, "cannot be disambiguated between generic and non-generic versions" |
| only parameters the generic declares, such as `Handle` or `Payload` (`G3`..`G8`) | the compiler opens the **non-generic** component and binds the generic one's parameter against the unbound `TPayload` (`CS0246`, `CS1503`); no type inference runs, and the cascaded type parameter does not reach a dual-named child |
| an explicit type argument (`G2b`) | the generic component, but a shared parameter beside it is emitted once per matched component (`G2`, `RZ10009`) |

A plain popup always sets a shared parameter (`Open`, `DefaultOpen`, `OnOpenChange`), so the dual-name shape cannot
compile it, and a payload user's `Handle` or `Payload` does not reach the generic component either.

**(ii) holds, on a generic-only part.** `ChildContent : RenderFragment<TPayload?>` binds plain markup without `Context`
(`H1`), and `@context` reads the payload (`H5`). Two such Roots nested without `Context` fail with `RZ9999` (the
`context` name clash, `H6`); the second shape's `RenderFragment?` child content nests freely (`T1`).

**The cascaded type parameter**, on generic-only parts: a Root inferred from `Handle` gives a child
`GenTrigger<TPayload>` its type argument with or without `Payload` (`H2`, `H3`); a detached Trigger infers from `Handle`
(`H7`); a Root that names neither `Handle` nor `TPayload` cannot be inferred, even when a child's `Payload` is typed
(`H8`, `H9`); `TPayload="NoPayload"` on the Root serves a plain popup and its Triggers inherit it (`H4`).

**Outcome:** the second shape of ADR 08 Amendments 2026-10-27: generic parts only, `[CascadingTypeParameter]`, a plain
popup names `NoPayload` (or its own type) on the Root, `ChildContent : RenderFragment?` beside a payload template
parameter used with `Context` (`T1`, `T2`).

**Also observed:** an unconstrained `TPayload? Payload` is `TPayload` for a value type, so a Trigger without `Payload`
under `TPayload=int` receives `0`, not "no payload" (`H2` renders `payload="0"`). A part that must tell an omitted
payload from `default(TPayload)` reads whether the parameter was supplied.
