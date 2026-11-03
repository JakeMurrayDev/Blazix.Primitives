# src/Blazix.Primitives/Utilities

The pure helpers that more than one family needs and that are neither pipeline, store, events nor twins (ADR 09 Amendments 2026-10-21). They are internal: a document makes a helper public only by naming it in a public API table. The first consumers are Meter (M1 track 2) and Progress (M1 track 3); Field (M2) reuses the label-id slot.

## Inputs

- `docs/architecture/shared-utilities.md` (UTL): the helpers' own requirements, cited in a `//` comment above each member: UTL-EDGE-14 (`clamp`), UTL-DOM-31 (`valueToPercent`), UTL-DOM-37 (number serialization, §4.12.1), UTL-DOM-13/14/15 (the visually hidden declarations), UTL-I18N-05..07 (number formatting).
- `docs/architecture/field-and-form-infrastructure.md` FFI-ARIA-08 (label-id registration) and `docs/components/meter.md` / `progress.md` (MTR-ALG-01..03, MTR-STA-03, MTR-I18N-01..03 and the PRG equivalents).
- Decision records 01 decision 2 and its amendment of 2026-10-18 (.NET globalization: equivalent, not byte-identical), 08 Amendments 2026-10-21 (`Format : string?`, `Culture : CultureInfo?`), 09 Amendments 2026-10-21 (this folder), 16 rule 7 (allocation).

## Job

| File | Role |
|---|---|
| `RangeMath.cs` | `Clamp` (UTL-EDGE-14), `ValueToPercent` (UTL-DOM-31), and the two compositions the value-in-a-range parts share: `ClampedPercentage` (MTR-ALG-01: `NaN` becomes 0, then `[0, 100]`) and `ClampedValue` (MTR-ALG-02: `NaN` becomes `min`, then `[min, max]`) |
| `NumberSerializer.cs` | `Serialize`: a number written into an attribute value or an inline style, exactly as ECMAScript `Number::toString(x, 10)` (UTL-DOM-37); `double.ToString()` agrees on the digits but not on exponents |
| `NumberFormatting.cs` | `Format` (`double.ToString(format, culture ?? CurrentCulture)`, UTL-I18N-05), `FormatPercentage` (`P0` of the percentage divided by 100) and `FormatMeasure` (a format, even an empty one, formats the clamped value; no format formats the percentage; MTR-API-04). The port has no formatter instance, no cache and no locale string, so UTL-I18N-06/07 are met by construction (port notes in the UTL rows) |
| `VisuallyHidden.cs` | the three declaration strings (UTL-DOM-13/14/15) as constants |
| `LabelIdSlot.cs` | the label-id registration (FFI-ARIA-08, MTR-STA-03): `Publish`, `Withdraw` only while still the tracked id, and a `Changed` action the owning root renders from |
| `ListValueParameters.cs` (M5 track 4) | `Check(in ParameterView, string rootName)`: a value-typed list root's always-on mismatch between `Multiple` and the triple supplied, read before any assignment (ADR 08 Amendments 2026-11-03 (1) and the probe's finding of 2026-11-04) |
| `ValueNull.cs` (M5 track 4) | `IsNull<TValue>`: the null test without the box on unoptimized code (moved from `Radio/RadioValues.cs`; ADR 08 Amendments 2026-10-22) |
| `ItemEquality.cs` (M5 track 4) | UTL-ALG-06: `Compare` (`Equality.Is` by default, `IsItemEqualToValue` only for two non-null operands, item first), `IsDirty` for a value (`StrictEquals`) and for lists (element-wise), `Includes`, `FindItemIndex`, `FindSelectionIndex` single and multiple (anchored in rendered order), `ResolveSelectedIndex`, `RemoveItem`, over `ReadOnlySpan<ItemSlot<TValue>>`; the multiple matcher is an open-addressing set with an unboxed hash, thread-static and reused (`HashSet<T>` boxes a value type on the WebAssembly interpreter) |
| `CollatorFilter.cs` (M5 track 4) | UTL-I18N-08..12, ALG-05: a value over `CultureInfo.CompareInfo` with `IgnoreCase | IgnoreNonSpace | IgnoreSymbols | IgnoreKanaType | IgnoreWidth` (one `CompareOptions?` replaces them whole), Base UI's window of `query.Length` code units, `Contains`/`StartsWith`/`EndsWith` over spans and over items through `ValueLabels.StringifyAsLabel` |
| `ValueLabels.cs` (M5 track 4) | UTL-ALG-07, I18N-13: `HasNullItemLabel`, `StringifyAsLabel`, `StringifyAsValue` (invariant text, `FormValue.Serialize`, unless a converter is given), `ResolveSelectedLabel` (=== leaves), `ResolveMultipleLabels` (`", "`), over the internal view `LabeledItems<TValue>` (`Flat`, `Grouped`, `Keyed` by the value's invariant text with `"null"`) of `LabeledItem<TValue>(TValue Value, string? Label)` |
| `ItemIndicatorBehavior.cs` (M5 track 4) | RND-STR-10, API-12, DOM-50..52, ARIA-01, ANI-01..03: the item indicator Select's and Combobox's shells compose (`Initialize`, `Update`, `Shown`, the mirrored `TransitionStatus`, `AddPartAttributes`, `AfterRenderAsync`, `DefaultContent`), with the internal `ItemIndicatorDataAttributes`; each shell declares its own state record (`bool Selected`, `[MirroredField] TransitionStatus? TransitionStatus`) and the ownership-registry row for it, since the ownership lint gives every mirrored field of the library a family; `tests/Blazix.Primitives.Tests.Components/ListValues/TestItemIndicator.cs` is the composition a shell copies |
| `ListboxSeparatorAttributes.cs` (M5 track 4) | RND-STR-11, API-13, DOM-53, ARIA-02: `Write(AttributeList)`, `role="presentation"` and no `aria-orientation`; `data-orientation` comes from the shell's state record |
| `DisableScrollbarStyle.cs` (M5 track 4) | UTL-DOM-32, DOM-33: `Render(builder, CspProviderContext?)` inside the caller's region, the inline `<style>` with the nearest `CspProvider`'s nonce and none under `DisableStyleElements`; `ClassName` stays applied |

Rules the folder keeps: allocation-free in steady state (the serializer's common case, a whole number, builds its string from the integer; nothing else here allocates beyond the string it returns); a member cites its UTL or RND ID in a `//` comment, never in `///`.

From M5 track 4 (design point 1, settled by the orchestrator 2026-11-04; ADR 09 Amendments 2026-11-04) the folder also holds the shared internals of the value-typed list families, part behaviours included, with RND §4.10 as their second contract. Its members may reference `Store/Equality`, `Field/FormValue`, `Rendering/` (the attribute list, the state projection) and the `transitions` twin; the number and label helpers above stay pure. A family composes these and never copies them (no second equality, view, matcher, filter, label resolution, serializer, behaviour or emitter).

What the value-typed list roots' consumers write (the probe's finding, ADR 08 Amendments 2026-11-03 (1), dated 2026-11-04). Wave 2's roots put these in their XML docs and their `<remarks>`:
- `ValuesChanged` and `Values`: `@bind-Values` needs a field of type `IReadOnlyList<TValue>`; a `List<T>` or array field is `CS1503`. The one-way `Values` takes any list and a collection expression.
- The cascading type parameter reaches the items in the root's own markup only. Items in a child `.razor` component or a `RenderFragment` field write `TValue` or their own `Value` (`RZ10001` otherwise), and find the root only when the type matches exactly (an `int` item does not find an `int?` root).
- An explicit `TValue` is needed when the root sets no inferring parameter, or only a method-group `OnValueChange` (`CS1503`); a typed lambda infers.
- Two triples of different types are `CS0411`, a compile-time error and the right outcome; two of one type compile, and `ListValueParameters.Check` rejects the triple `Multiple` does not choose at run time.

## Outputs

- `Blazix.Primitives.Utilities` (internal). Tested at L1 in `tests/Blazix.Primitives.Tests/Meter/` with the Meter document's titles for the algorithms and port titles for the serialization, the visually hidden strings, the formatting samples and the slot; a family that adds a helper adds its L1 tests with it. The list internals are tested in `tests/Blazix.Primitives.Tests/ListValues/` (L1, with the collation table in `Data/`), `tests/Blazix.Primitives.Tests.Components/ListValues/` and `CspProvider/` (L2, the test-only roots, indicator and separator) and `tests/browser/list-values.spec.ts` (L4, the `list-values-*` fixtures).
- Reused by `src/Blazix.Primitives/Meter/` and, from M1 track 3, `src/Blazix.Primitives/Progress/`.

## Human check

- A new helper is needed by two families, cites its UTL ID, and is not already under `Rendering/` (the number serialization stayed here because Track 1 placed none there).
- No second implementation of an algorithm exists in a family folder (ADR 01 decision 2): a family calls these helpers.
- The formatting divergences from `Intl.NumberFormat` stay recorded in the pull request that accepted them: `de-DE` and `fr-FR` percent use an ASCII space where `Intl` writes a no-break space (`de-DE`) or a narrow no-break space (`fr-FR`); `C` takes the culture's own currency symbol where `Intl` formats the `currency` code given; `Format` is a .NET format string, not an options record.
