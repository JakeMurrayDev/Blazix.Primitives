# Union features probe (issue 12)

Throwaway (ADR 14 §3), SDK 11.0.100-rc.2.26504.105. Probes the rows of ADR 05's union feature checklist that the
union-binding spike of issue 10 did not answer. Excluded from the solution; deleted in the same pull request.

- `Probe/Closed.cs`: `closed class` with two sealed subclasses and an exhaustive switch — compiles, no CS8509.
  Variants tried and removed: a switch missing one subtype (CS8509 names it), `closed abstract` (CS9384, a closed type
  is always abstract), `new` on a closed type (CS0144 as for any abstract class), `closed record class` (compiles).
- `Other/Outside.cs`: deriving from the closed type in another assembly — CS9382.
- `Probe/Unions.cs`: a nullable union matched with a `null` arm (works); `int?` to a union with an `int` case (CS0029,
  lifted conversions are missing; replaced by a match); `Program.cs` shows a `U?` holding `default(U)` matching `is null`.

Build with `dotnet build Other` (the expected CS9382 fails it); run `dotnet run --project Probe` after removing `Other`.
