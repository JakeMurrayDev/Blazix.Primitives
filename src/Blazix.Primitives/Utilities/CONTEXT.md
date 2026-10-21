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

Rules the folder keeps: pure C# and allocation-free in steady state (the serializer's common case, a whole number, builds its string from the integer; nothing else here allocates beyond the string it returns); no reference to `Rendering/`, `Store/`, `Events/` or `Interop/`; a member cites its UTL ID in a `//` comment, never in `///`.

## Outputs

- `Blazix.Primitives.Utilities` (internal). Tested at L1 in `tests/Blazix.Primitives.Tests/Meter/` with the Meter document's titles for the algorithms and port titles for the serialization, the visually hidden strings, the formatting samples and the slot; a family that adds a helper adds its L1 tests with it.
- Reused by `src/Blazix.Primitives/Meter/` and, from M1 track 3, `src/Blazix.Primitives/Progress/`.

## Human check

- A new helper is needed by two families, cites its UTL ID, and is not already under `Rendering/` (the number serialization stayed here because Track 1 placed none there).
- No second implementation of an algorithm exists in a family folder (ADR 01 decision 2): a family calls these helpers.
- The formatting divergences from `Intl.NumberFormat` stay recorded in the pull request that accepted them: `de-DE` and `fr-FR` percent use an ASCII space where `Intl` writes a no-break space (`de-DE`) or a narrow no-break space (`fr-FR`); `C` takes the culture's own currency symbol where `Intl` formats the `currency` code given; `Format` is a .NET format string, not an options record.
