# src/Blazix.Primitives/Floating

The C# half of floating positioning: the public types of the twelve shared Positioner props and the internal Positioner and Arrow behaviours a family's parts compose (ADR 09 Amendments 2026-10-22 and 2026-10-27: a mechanism's C# half lives in a folder named after it when no family owns it). The JavaScript half is `../js/positioning.ts` over the bundled engine (`../js/floating-ui.ts`), and the twin is `../Interop/PositioningTwin.cs`. Namespace `Blazix.Primitives`. The types are public; the behaviours, the context, the inputs, the mirror and the literals are internal. M4 track 3 (`interactions`) and track 4 (`focus-manager`) add their behaviours here (ADR 04 Amendments 2026-10-27).

## Inputs

- `docs/architecture/floating-and-positioning.md` (FLT): §3.1 (the twelve shared Positioner props), §4.3 (FLT-API-08..12, FLT-API-17), §4.4 (FLT-DOM-01..10), FLT-ANI-02..05, §4.12 (FLT-POS-01..17), FLT-I18N-01..03, §6.1 and §6.2 (the private options), §8.
- `docs/architecture/shared-utilities.md` §4.15.1 (the direction it reads from `../DirectionProvider/`).
- Decision records:
  - 03: the bundled engine; C# renders the requested side and align, no transform and the seeded variables before the first pass.
  - 04: §1 (the `positioning` row); §2 (the Positioner and Arrow rows: `style`, `data-side`, `data-align`, `data-anchor-hidden`, `data-uncentered` are JavaScript's from attach).
  - 04 Amendments 2026-10-18 (the handover at attach) and 2026-10-27 (the layered-style writer, the anchor, offsets and collision options, the Arrow's id through the context, value equality on the options). The positioning track's design points 1–8 were settled by the orchestrator 2026-10-27, and the shared token name `SessionToken` with them.
  - 05 §4: hand-written `[Union]` structs.
  - 08 Amendments 2026-10-27: the unions, the `Offset` record, the enums in `Floating/`.
  - 16 rule 7: allocation budgets.
  - 16 Amendments 2026-10-27: options value equality.

## Job

| File | Role |
|---|---|
| `Side.cs`, `Align.cs`, `PositionMethod.cs` | Base UI's `Side` (with `InlineEnd`, `InlineStart`), `Align` and `positionMethod`. |
| `CollisionAvoidanceMode.cs`, `FallbackAxisSide.cs`, `CollisionAvoidance.cs` | `CollisionAvoidanceMode { Flip, Shift, None }` for both axes and `FallbackAxisSide { Start, End, None }` (`None` is a Base UI literal). `CollisionAvoidance(CollisionAvoidanceMode? Side, CollisionAvoidanceMode? Align, FallbackAxisSide? FallbackAxisSide)`: a `null` member is Base UI's per-key default (`flip`, `flip`, `end`, FLT-API-10). |
| `AnchorValue.cs`, `VirtualAnchor.cs` | `AnchorValue`: an `ElementReference`, a `string` element id (Razor's plain-text literal) or a `VirtualAnchor(double X, double Y, double Width, double Height)`. There is no function case, and a moved virtual anchor is a new value. |
| `CollisionBoundaryValue.cs`, `Rect.cs` | `CollisionBoundaryValue`: `ClippingAncestors` (a static instance, `IsClippingAncestors`), an `ElementReference`, an `IReadOnlyList<ElementReference>` (implicit from an array or a `List`) or a `Rect(double X, double Y, double Width, double Height)`. |
| `CollisionPaddingValue.cs`, `PaddingSides.cs` | `CollisionPaddingValue`: a `double` or `PaddingSides(double Top = 0, double Right = 0, double Bottom = 0, double Left = 0)`. |
| `OffsetValue.cs`, `Offset.cs`, `OffsetOverride.cs` | `OffsetValue`: a `double`, or `Offset(double Constant = 0, double AnchorWidth = 0, double AnchorHeight = 0, double PositionerWidth = 0, double PositionerHeight = 0)` with `IReadOnlyList<OffsetOverride>? Overrides`. `OffsetOverride(Side? Side, Align? Align, …the same terms)`. The first override whose non-null keys match the evaluated logical side and align wins. The module evaluates it on every pass. |
| `FloatingLiterals.cs` | The literals of `Side`, `Align`, `PositionMethod`, `CollisionAvoidanceMode`, `FallbackAxisSide` and `TransitionStatus?`; `ParseSide` and `ParseAlign` for the report. |
| `PositionerInputs.cs` | The record a Positioner part fills on every parameter set: the twelve §3.1 values, plus `Open`, `Mounted`, `KeepMounted`, `TransitionStatus`, `AdaptiveOrigin` (a Viewport is present), `TriggerId` (the active trigger) and `SessionToken`. |
| `PositionerBehavior.cs` | One Positioner's mechanism, composed into the part as `ButtonBehavior` is. It owns the `PositioningTwin` and attaches by `Element` or, under a substitute callback, by id. `SetDirectionContext`, `SetInputs` (on every parameter set), `AddAttributes`, `FinishAttributes`, `AfterRenderAsync(element, id)`. See the rules below the table. |
| `PositionerArrowBehavior.cs` | One Arrow's mechanism. `SetContext(PositionerContext)`, `AddAttributes` (`aria-hidden="true"`, the placement from the mirror), `FinishAttributes(attributes, id)` (`position: absolute` under the consumer's style; `data-side`, `data-align`, `data-uncentered`), `AfterRenderAsync()` (flushes the Positioner). It freezes from the first attach or update of the Positioner that carries its id. |
| `PositionerContext.cs` | Cascaded fixed by the Positioner part. It holds the one registered Arrow (the last wins), the mirror, the Arrow's listeners and `FlushAsync`. |
| `PositionerMirror.cs` | `PositionerMirror(Side Side, Align Align, bool AnchorHidden, bool Uncentered)`: the report as C# mirrors it. Before the first report it holds the requested side and align, a shown anchor and an uncentered arrow, as Base UI's hook does before its first pass. |

`PositionerBehavior`'s rules:
- It renders `role="presentation"`, `hidden` while not mounted, the requested `data-side` and `data-align`, and the pre-position style: `position: fixed; top: 0px; left: 0px; --available-width: 100vw; --available-height: 100vh; opacity: 0;`, then `pointer-events: none;` while closed, `transition: none;` while starting, and the consumer's merged style last. The style and the placement attributes are frozen at attach.
- It sends one `update` per change of the options. No options are built while no input, the consumer's style, the direction and the Arrow changed.
- It mirrors each report. The part is rendered only when side, align or anchorHidden changed, and the Arrow is notified only when side, align or uncentered changed. A part renders on a mirror change only when its state record changed, because the Positioner's render may already have rendered its Arrow.
- It re-attaches by id when the element is replaced.
- The element cases of `AnchorValue` and `CollisionBoundaryValue` wait for track 1's references array (`AnchorRef`, `BoundaryRefs`); until then they send nothing.
- Under a Portal, the first attach awaits the portal's move task (the Toolbar first-attach precedent, settled by the orchestrator 2026-10-27), so the module never measures a node still `display: none` in place. The member is track 1's, and this is open until track 1 is on `main`; `FixturePositioner` has no portal.

What JavaScript owns (not here): the pipeline and its middleware, the tracking, `isPositioned`, the physical side, `update()`, and every style and placement attribute from attach. `readPlacement(el)` and `update(el)` are the module's exports for the `popup` module and PreviewCard.

## Traceability (the sources carry no specification references)

- `PositionerBehavior`: FLT-DOM-01 (role, hidden, pointer-events while closed), FLT-DOM-02 (the pre-position style), FLT-DOM-05 (the seeds), FLT-ANI-05 (`transition: none` while starting), FLT-POS-01, FLT-I18N-01/02 (the requested placement, the direction literal), FLT-DOM-08 (the mirror; ADR 01's RND-STA-01 row), FLT-POS-17 (one update per change), FLT-STA-09 (`hidden` gates the module's tracking).
- `PositionerArrowBehavior`, `PositionerContext`: FLT-DOM-09, FLT-DOM-10 (the stand-in while no Arrow is registered), ADR 04 Amendments 2026-10-27 (the Arrow's id through the context).
- The types: FLT §3.1, FLT-API-08 (padding), FLT-API-09 (boundary), FLT-API-10 (avoidance), FLT-API-11 (anchor; dated port note on the function clause), FLT-API-12 (offsets; dated port note), FLT-POS-03..06.

## Outputs

- The public types above, for the Positioner and Arrow parts of Popover, Tooltip and PreviewCard (M4 tracks 5–7) and of Menu, Select, Combobox and NavigationMenu later.
- The behaviours, the context and the inputs, composed by those parts. The test-only `TestPositioner` and `TestPositionerArrow` (L2) and `FixturePositioner` and `FixturePositionerArrow` (L4) host them until the families exist.

## Human check

- A family's Positioner sets every input on every parameter set and renders no `style` of its own. Its `ClassValue` and `StyleValue` read the mirror, which follows the module a round trip later on Server.
- A new options field is compared by value (a list by hand), so an equal re-render sends nothing; the re-render budget (`PositionerAllocationBudgetTests`) only tightens.
- No `///` comment cites a specification, and a misuse message starts `Blazix: `.
