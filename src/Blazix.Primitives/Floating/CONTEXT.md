# src/Blazix.Primitives/Floating

The C# half of the floating mechanisms that no family owns (ADR 09 Amendments 2026-10-22 and 2026-10-27: a mechanism's C# half lives in a folder named after it). Namespace `Blazix.Primitives`; everything here is internal. M4 track 2 (positioning) and track 3 (interactions) add their behaviours here too; this file holds track 4's rows until their pull requests merge it.

## Inputs

- `docs/architecture/floating-and-positioning.md` (FLT): §4.4 (FLT-DOM-13..15), §4.8 (FLT-FOC-06..22), FLT-API-05, FLT-KEY-06; §10 item 15.
- Decision records:
  - 01 Amendments 2026-10-27: the commit-path `update` (design point 1 of the focus-manager track, chosen by the user).
  - 04 §1 (the `focus-manager` row) and Amendments 2026-10-27: focus targets, element-typed option values (`TwinReferences`), the relay, the answered counter, the portal's published state.
  - 16 rule 7 (allocation budgets), rule 10 (behaviours composed into parts, never nested components), Amendments 2026-10-27 (options value equality).

## Job (M4 track 4, the focus manager; design point 6, settled by the orchestrator 2026-10-27)

| File | Role |
|---|---|
| `FocusManagerBehavior.cs` | The focus manager of one Popup part, composed as `ButtonBehavior` is. It owns the `FocusManagerTwin`; `SetInputs(FocusManagerInputs)` on every parameter set sends changed options at once to an attached module, before the render that applies them; `BuildBeforeElement`/`BuildAfterElement` render the inside guards iff `!Disabled && (Modal \|\| InsidePortal)` (FLT-DOM-15, DOM-13); `Portal` (the cascaded `PopupPortalContext`) receives `PortalFocusPublication(Modal, Open, CloseOnFocusOut)` while enabled and `null` otherwise (FLT-FOC-18); `AfterRenderAsync(element, id)` attaches after `PopupPortalContext.WhenMoved(portal)`, and drops an attach whose popup left the document; the focus-out request goes to the part's `requestClose` and raises `Answered` for every request; the close type is classified when `CloseEvent` changes. |
| `FocusManagerInputs.cs` | The inputs of one parameter set: `Open`, `Modal`, `Disabled`, `InitialFocus`/`ReturnFocus` (`FocusTargetValue?`, resolved by the family), `ExplicitReturnFocus`, `RestoreFocus`, `CloseOnFocusOut`, `OpenInteractionType` (`null` for a programmatic open), `CloseEvent`, `OpenSelector`, `TreatPopupAsFloatingElement`, `DomReferenceId`, `TriggerFocusGuards`, `PreviousFocusableElement`, `NextFocusableElement`, `InsideElements`, `InsideElementIds`, `Relay`. Value equality allocates nothing. |
| `CloseTypeClassifier.cs` | `Classify(EventSnapshot?, InteractionType?) : InteractionType`, FLT-FOC-10's `getEventType` over the close's snapshot and the recorded type; `Unknown` is Base UI's empty literal. |

## Outputs

- Tests: `tests/Blazix.Primitives.Tests/Floating/CloseTypeClassifierTests.cs` (L1), `tests/Blazix.Primitives.Tests.Components/Floating/FocusManagerBehaviorTests.cs` over `TestFocusManaged` (L2), `tests/browser/focus-manager.spec.ts` over `FixtureFocusManaged` (L4).

## Human check

- A family's Popup calls `SetInputs` from `OnParametersSet` (after setting `Portal`), never only from `OnAfterRender`, or a kept-mounted or hover-opened popup marks the page for a round trip on Server.
- A `FocusTargetValue?` built in a conditional with `null` takes the implicit string conversion: return `default(FocusTargetValue?)` instead.
