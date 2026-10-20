# ADR 08 — Naming and namespaces

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | how Base UI part names become Blazor component type and tag names; the namespace layout; the names of state records, details, enums, unions, data-attribute constants, contexts, handles, twins and JavaScript modules; the parameter names for class and style; the rule for Base UI identifiers that carry the product name; member ordering and field naming |
| Affects | every part; RND-API-01..11 (parameter names), RND-EVT-03/04 (the veto's name), EVD §3.5 (the reason enum's name); amends ADR 02 decision 3 and ADR 05 §3 (names only) |

## Context

Razor resolves a component tag only when it is fully qualified or when its namespace is imported by a `@using` directive in a Razor file (verified on SDK 10.0.301 with a throwaway class library: `<Probe.Switch.Root>` compiled; `<Switch.Root>` under `@using Probe` and a nested-class `<Menu.Root>` both failed with RZ10012). C# global usings do not take part in tag resolution. Base UI reuses part names across families (Root in every family, Trigger in 20 documents, Popup in 15, Positioner in 11), so short class names per family collide as soon as two families appear in one file, which every form does. The old port used a namespace per family with flat names (`Blazix.BaseUI.Switch.SwitchRoot`); its demo pages carried up to eight `@using` lines each. The public surface will be around 900 types (the old port had 667). Fluent UI Blazor and MudBlazor put every component in one namespace. Base UI's single-part components are `Button`, `Input`, `Separator`, `Toggle`, `Form`, `DirectionProvider` and `CSPProvider`.

Razor binds component attributes to parameters case-insensitively (`class="x"` binds to a parameter named `Class`; verified 2026-10-18 and again 2026-10-20), so the name of the union-typed class parameter decides whether the plain `class` string reaches the unmatched bag. C# 15 unions are declared types, so the `string | Func<…>` shorthand of ADRs 02 and 05 needs named types. ADR 05 dropped Base UI's `BaseUI` prefix from the details types (`ChangeEventDetails`, `GenericEventDetails`, `HighlightEventDetails`) but kept it on `BaseUIEventReason` and on the veto `PreventBaseUIHandler()`; "Base UI" is MUI's product name and MIT grants no trademark rights. The old port's member order (eleven groups) was enforced by an analyzer that ended up suppressed because Roslyn did not apply the editorconfig to generated `razor.g.cs` files, a cause that disappears under ADR 02 (parts are plain C# classes). The old port forbade underscore prefixes on fields.

## Decision

### 1. Part names: flat, dot removed, one namespace (option N1)

A part `Family.Part` becomes the type and tag `FamilyPart`: `Switch.Root` → `SwitchRoot`, `Menu.CheckboxItemIndicator` → `MenuCheckboxItemIndicator`, `Popover.Positioner` → `PopoverPositioner`, `AlertDialog.Root` → `AlertDialogRoot`, `NavigationMenu.Viewport` → `NavigationMenuViewport`. Single-part components keep Base UI's name: `Button`, `Input`, `Separator`, `Toggle`, `Form`, `DirectionProvider`, `CspProvider`. Every part and its public companions live in the namespace `Blazix.Primitives`; a consumer adds one `@using`. Library-author surfaces live in sub-namespaces: `Blazix.Primitives.Rendering` (`PartBase<TState>`, `RenderElement<TState>`, `RenderProps<TState>`, `MergeProps`), `Blazix.Primitives.Store` (`Store<TState>`, `ComponentStore<TState>`, selectors), `Blazix.Primitives.Interop` (twins, internal). The mapping from a document's part name to the type name is mechanical (remove the dot) and tooling may rely on it.

### 2. Class and style parameters: `ClassValue` and `StyleValue` (two channels kept)

The union-typed parameters of ADR 05 §4 are named `ClassValue` and `StyleValue` (`string | Func<TState, string?>`). The plain `class` and `style` strings keep arriving through the unmatched bag and ADR 02's P1 concatenation rule stands (consumer string, then consumer function). No part declares a parameter named `Class` or `Style`. The one-line explanation for consumers: `class` is the Blazor attribute; `ClassValue` is Base UI's `className`, string or function.

### 3. Companion types

| Kind | Rule | Example |
|---|---|---|
| Part parameters | PascalCase of the Base UI prop; `children` → `ChildContent`, `className`/`style` → `ClassValue`/`StyleValue`, `render` → `Render`, `ref` → `Element` (ADR 02). A Base UI prop becomes a typed parameter; a pass-through HTML attribute (`id`, `aria-*`, `data-*`) stays in the unmatched bag | `nativeButton` → `NativeButton` |
| State record | `<Part>State`, a `sealed record` marked `[PartState]` | `SwitchRootState` |
| Details and args | as ADR 05: shared `ChangeEventDetails`, `OpenChangeEventDetails`, `GenericEventDetails`; specialised `<Family>ChangeEventDetails`; `ValueChangeEventArgs<T>`; callbacks `On<X>Change` plus the `@bind` pair `<X>` / `<X>Changed` / `Default<X>` | `TabsChangeEventDetails` |
| Enums | type named for the concept, shared in `Blazix.Primitives` when the literal set is shared, family-prefixed when it is one family's; members are PascalCase of the Base UI literal; no `Undefined` or `None` sentinel unless Base UI has the literal (a nullable enum expresses "not supplied") | `Side.InlineStart`, `TransitionStatus.Idle`, `EventReason.CloseWatcher` |
| Boolean-or-literal props | a hand-written `[Union]` struct per ADR 05 §4 with an implicit conversion from `bool` and the literal as a named case | `modal` → `ModalValue` with `TrapFocus` |
| Union types | named for what they hold, suffix `Value`; the parameter may share the name | `ClassValue<TState>`, `RenderValue<TState>`, `AnchorValue`, `SliderValue`, `AttributeValue` |
| Data attributes, CSS variables | `static class <Part>DataAttributes` / `<Part>CssVars` with PascalCase string constants, mirroring Base UI's exported `DataAttributes` and `CssVariables` types | `SwitchRootDataAttributes.Checked = "data-checked"` |
| Ancestor-provided shared state | `internal sealed class <Part>Context`; never public | `MenuRootContext` |
| Handles | `<Family>Handle<TPayload> : PopupHandle<TPayload>`, created with `new` | `MenuHandle<TPayload>` |
| Twins | `internal sealed class <Module>Twin` in `Blazix.Primitives.Interop`, PascalCase of the module name | `FocusManagerTwin`, `NumberFieldTwin` |
| JavaScript modules | file name is the ADR 04 §1 table name in kebab-case; component modules use the Base UI directory name; exports are camelCase; the initializer is `Blazix.Primitives.lib.module.js` | `focus-manager.ts`, `number-field.ts` |

### 4. Product-name rule

A Base UI identifier that carries the product name loses it in the port's C# surface: `BaseUIEventReason` → `EventReason`; `preventBaseUIHandler()` → `e.PreventPartHandler()`, with the query `e.PartHandlerPrevented()` ("part" is the glossary's word for the library's unit). The details types already follow this rule. DOM-level literals keep Base UI's form because tests pin them and consumers never type them: generated ids stay `base-ui-<n>` (RND-DOM-27, ADR 02 decision 5) and the harness switch stays `BASE_UI_ANIMATIONS_DISABLED` (TRN-EDGE-01). The documents keep Base UI's names throughout, as they do for `className`; the mapping is this ADR's.

### 5. Member order and field names

Members of every class follow the eleven-group order of the old port: constants, fields, private properties, parameter properties, public properties, internal properties, lifecycle methods, dispose, public methods, internal methods, private methods. Private fields carry no underscore prefix. Both are stated in the contributor template for `PartBase<TState>` (ADR 02) and enforced by the ordering and field-prefix analyzers of the analyzers project (ADR 09) once it exists, by review until then.

## Consequences

* ADR 02 decision 3 and ADR 05 §3 are amended for the names of the veto and the reason enum (see their Amendments sections); `CONTEXT.md` is updated.
* One wide namespace in IntelliSense; `Button`, `Input` and `Form` are generic names a consumer project may also define and disambiguate by namespace.
* A consumer who declares a parameter named `Class` on their own wrapper component captures the `class` attribute; the documentation says so once.

## Verification items

* Razor binds a literal to a hand-written `[Union]` struct parameter through its implicit conversion (`Modal="true"`, `Modal="@ModalValue.TrapFocus"`) on the .NET 11 SDK; if not, boolean-or-literal props fall back to two parameters (`Modal` and `ModalMode`). Tracked in ADR 14.
* MUI's trademark position on "Base UI" appearing in an unofficial port's package description and acknowledgements (no API name carries it after this ADR). Tracked in ADR 14.

## Considered options

* Namespace per family with short class names (`Blazix.Primitives.Switch.Root`): tags fully qualified or one `@using` per family, and collisions between imported families; the Radix-style `<Switch.Root>` is unreachable in Razor. Namespace per family with flat names (the old port): the same usings for no gain. A library prefix (`BlazixSwitchRoot`): long tags that stop matching the documents.
* `Class`/`Style` as the union parameter names (one channel, exactly Base UI's shape): amends ADR 02's superset and depends on union binding; its fallback pair (`Class` string, `ClassValue` function) is worse than keeping `ClassValue`.
* A `Union` suffix on union types; an `Undefined` enum member (the old port's habit: a meaningless case in every switch that hides "not supplied" from the type).
* Keeping `BaseUIEventReason` and `PreventBaseUIHandler()` (another product's name in every veto call and reason switch, inconsistent with the unprefixed details types); renaming the id prefix and the harness flag too (every translated test asserting the prefix would change for a literal no consumer depends on).
* A light member order or none (drift across hundreds of agent-written classes); `_camelCase` fields (the wider .NET convention, rejected for continuity with the user's prior rule).
