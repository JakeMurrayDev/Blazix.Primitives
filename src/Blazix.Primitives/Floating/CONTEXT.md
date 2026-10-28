# src/Blazix.Primitives/Floating

The C# half of floating positioning: the public types of the twelve shared Positioner props and the internal Positioner and Arrow behaviours a family's parts compose (ADR 09 Amendments 2026-10-22 and 2026-10-27: a mechanism's C# half lives in a folder named after it when no family owns it). The JavaScript half is `../js/positioning.ts` over the bundled engine (`../js/floating-ui.ts`), and the twin is `../Interop/PositioningTwin.cs`. Namespace `Blazix.Primitives`. The types are public; the behaviours, the context, the inputs, the mirror and the literals are internal. M4 track 3 (`interactions`) and track 4 (`focus-manager`) add their behaviours here (ADR 04 Amendments 2026-10-27).

## Inputs

- `docs/architecture/floating-and-positioning.md` (FLT): §3.1 (the twelve shared Positioner props), §4.3 (FLT-API-08..12, FLT-API-17), §4.4 (FLT-DOM-01..10), FLT-ANI-02..05, §4.12 (FLT-POS-01..17), FLT-I18N-01..03, §6.1 and §6.2 (the private options), §8.
- `docs/architecture/shared-utilities.md` §4.15.1 (the direction it reads from `../DirectionProvider/`).
- `../Interop/TwinReferences.cs` (the element inputs cross beside the options) and `../Popup/PopupPortalContext.cs` (`WhenMoved`, the portal's move), M4 track 1.
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
| `PositionerInputs.cs` | The record a Positioner part fills on every parameter set: the twelve §3.1 values, plus `Open`, `Mounted`, `KeepMounted`, `TransitionStatus`, `AdaptiveOrigin` (a Viewport is present), `HoveredLine` (PreviewCard's inline middleware, 2026-10-29), `TriggerId` (the active trigger) and `SessionToken`. |
| `PositionerBehavior.cs` | One Positioner's mechanism, composed into the part as `ButtonBehavior` is. It owns the `PositioningTwin` and attaches by `Element` or, under a substitute callback, by id. `SetDirectionContext`, `SetPortalContext` and `SetInputs` (on every parameter set), `AddAttributes`, `FinishAttributes`, `AfterRenderAsync(element, id)`. See the rules below the table. |
| `PositionerArrowBehavior.cs` | One Arrow's mechanism. `SetContext(PositionerContext)`, `AddAttributes` (`aria-hidden="true"`, the placement from the mirror), `FinishAttributes(attributes, id)` (`position: absolute` under the consumer's style; `data-side`, `data-align`, `data-uncentered`), `AfterRenderAsync()` (flushes the Positioner). It freezes from the first attach or update of the Positioner that carries its id. |
| `PositionerContext.cs` | Cascaded fixed by the Positioner part. It holds the one registered Arrow (the last wins), the mirror, the Arrow's listeners and `FlushAsync`. |
| `PositionerMirror.cs` | `PositionerMirror(Side Side, Align Align, bool AnchorHidden, bool Uncentered)`: the report as C# mirrors it. Before the first report it holds the requested side and align, a shown anchor and an uncentered arrow, as Base UI's hook does before its first pass. |

`PositionerBehavior`'s rules:
- It renders `role="presentation"`, `hidden` while not mounted, the requested `data-side` and `data-align`, and the pre-position style: `position: fixed; top: 0px; left: 0px; --available-width: 100vw; --available-height: 100vh; opacity: 0;`, then `pointer-events: none;` while closed, `transition: none;` while starting, and the consumer's merged style last. The style and the placement attributes are frozen at attach.
- It sends one `update` per change of the options. No options are built while no input, the consumer's style, the direction and the Arrow changed.
- It mirrors each report. The part is rendered only when side, align or anchorHidden changed, and the Arrow is notified only when side, align or uncentered changed. A part renders on a mirror change only when its state record changed, because the Positioner's render may already have rendered its Arrow.
- It re-attaches by id when the element is replaced.
- An attach that fails because the element left the document before the attach arrived (closed while the module loaded, or on Server a round trip after the render) is dropped, and a later render attaches again (added 2026-10-28, M4 track 6, [issue 134](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/134); design point 13.11, settled by the orchestrator 2026-10-28; the `FocusManagerBehavior` precedent).
- The element cases of `AnchorValue` and `CollisionBoundaryValue` cross beside the options as `TwinReferences`, the anchor element first and the boundary elements after it, named by `AnchorRef` and `BoundaryRefs` (`PositioningIndexes`). An update is also sent when only an element at the same index changed (the anchor and boundary inputs last sent are kept beside the options). An element reference not captured yet has no id: an anchor sends nothing, so the trigger applies, and a boundary element or a list whose elements are all uncaptured is sent as `clipping-ancestors` (an uncaptured reference never narrows the boundary, settled by the orchestrator 2026-10-28).
- Under a Portal (`SetPortalContext` with the cascaded `PopupPortalContext`), the first attach awaits `PopupPortalContext.WhenMoved(context)` (the Toolbar first-attach precedent, settled by the orchestrator 2026-10-27), so the module never measures a node still `display: none` in place. A render while the move is pending only records its element, and the attach takes the latest. `FixturePositioner` and `TestPositioner` read the context as a family's part will.

What JavaScript owns (not here): the pipeline and its middleware, the tracking, `isPositioned`, the physical side, `update()`, and every style and placement attribute from attach. `readPlacement(el)` and `update(el)` are the module's exports for the `popup` module and PreviewCard.

## Traceability (the sources carry no specification references)

- `PositionerBehavior`: FLT-DOM-01 (role, hidden, pointer-events while closed), FLT-DOM-02 (the pre-position style; the first attach after a portal's move), FLT-DOM-05 (the seeds), FLT-ANI-05 (`transition: none` while starting), FLT-POS-01, FLT-I18N-01/02 (the requested placement, the direction literal), FLT-DOM-08 (the mirror; ADR 01's RND-STA-01 row), FLT-POS-17 (one update per change), FLT-STA-09 (`hidden` gates the module's tracking; a portaled Positioner attaches after the move), FLT-API-09 and FLT-API-11 (the element inputs as references).
- `PositionerArrowBehavior`, `PositionerContext`: FLT-DOM-09, FLT-DOM-10 (the stand-in while no Arrow is registered), ADR 04 Amendments 2026-10-27 (the Arrow's id through the context).
- The types: FLT §3.1, FLT-API-08 (padding), FLT-API-09 (boundary), FLT-API-10 (avoidance), FLT-API-11 (anchor; dated port note on the function clause), FLT-API-12 (offsets; dated port note), FLT-POS-03..06.

## Outputs

- The public types above, for the Positioner and Arrow parts of Popover, Tooltip and PreviewCard (M4 tracks 5–7) and of Menu, Select, Combobox and NavigationMenu later.
- The behaviours, the context and the inputs, composed by those parts. The test-only `TestPositioner` and `TestPositionerArrow` (L2) and `FixturePositioner` and `FixturePositionerArrow` (L4) host them until the families exist.

## Human check

- A family's Positioner sets every input on every parameter set and renders no `style` of its own. Its `ClassValue` and `StyleValue` read the mirror, which follows the module a round trip later on Server.
- A new options field is compared by value (a list by hand), so an equal re-render sends nothing; the re-render budget (`PositionerAllocationBudgetTests`) only tightens.
- No `///` comment cites a specification, and a misuse message starts `Blazix: `.

## The interactions behaviours (M4 track 3, issue 119)

The C# half of the `interactions` module (`../js/interactions.ts`, twin `../Interop/InteractionsTwin.cs`), every type internal: Base UI's floating-ui-react hooks have no public parts. Design point 8 of M4 track 3 (option (a), `Floating/`) was settled by the orchestrator 2026-10-27; track 2 created this file and this section follows its own.

### Inputs

- FLT §4.1–§4.10, §5.3 and §6.3–§6.9 (the options); PSH-STA-33 (the trigger map), PSH-PTR-01/02 and PSH-ALG-19 (the open interaction type).
- ADR 04 Amendments 2026-10-27 (chosen by the user 2026-10-27): the token form of `attach` and the per-popup session, the relay, `press`, the verdict `Prevent` at `escape`, the hover timers and the delay group, Dialog's `outsidePress` predicates. ADR 01 relaxation rows "C#-origin consumer vetoes", "EVD-EVT-11, EVD-DIS-01, FLT-KEY-01", "EVD-EVT-18, PSH-ANI-03". ADR 02 Amendments 2026-10-21 (`ForwardsConsumerHandler`, the decline-and-run gate with the one-shot marker). ADR 05 §2 (the open-change sequence the family's store runs). ADR 16 rules 6 and 7.
- Design points of M4 track 3: 1 (the token form), 2 (the roles), 5 (the press payload), 8 (`Floating/`) and 9 settled by the orchestrator 2026-10-27, and 6 (the names) accepted the same day, each as recommended (ADR 04 Amendments 2026-10-27).

### Job

| File | Role |
|---|---|
| `InteractionsSessionBehavior.cs` | A popup root's session: joins the token it is given (a popup family passes its store's `SessionToken`, PSH-STR-01, so every module of the popup shares one session) or draws one (`PartIdGenerator`), never renders it, attaches the session handle by token (`TwinBase.AttachTokenAsync`), sends `Open`, `ActiveTriggerId`, `ParentToken`, `FloatingId`, `Ending`, `Dismiss` and `Relay` (the store's `OpenChangeRelay`) when they changed (compared before a record is built), and answers every request it routes with `Answered` (design point 10: the counter moves before the store's sequence, so a commit's render carries it and a cancel sends it alone; the root therefore sets `Open` and `ActiveTriggerId` in its commit path). Routes every `OnRequest` of the session's twin into the store (`OpenChangeRequest`), and answers `escape` on WebAssembly by running the store's request with `EventReason.EscapeKey` synchronously and mapping `IsCanceled`/`IsPropagationAllowed` to `PreventAndStop`, `Prevent`, `Stop`, `Run`. A popup family sets the inputs from the store's committed state in its commit path (a selector subscription on `Open`, `ActiveTriggerId`, `OpenChange`, `TransitionStatus` and `PopupId`) and again each render: `FixturePopupRoot` in the fixture project is the shape. |
| `InteractionsTriggerBehavior.cs` | A trigger's handle: attaches to the trigger's own element (by `ElementReference` or the id it renders on that element), sends `InteractionsTriggerOptions` when they changed, declines the consumer's `onclick` while `Click` is set, and runs it once per press: inside `press` on WebAssembly (a `PreventPartHandler()` answers `stop`, else the request runs), or from the `OnPress` report on Server, whichever of the report and Blazor's dispatch of the click arrives first running the handler and the other reading its verdict. Every other request of the trigger (hover, focus, reference press) arrives as `OnRequest`. 2026-10-28 (M4 track 5, settled by the orchestrator; a fidelity correction of track 3's behaviour, found on detached triggers): the module keys the handle by the session it attached under, so the behaviour attaches only once `SessionToken` is set, detaches and attaches again under a different token, and keeps the live handle and its token through a render without one (a recreated handle before its root attaches). |
| `InteractionsFloatingBehavior.cs` | The floating element's handle (a Positioner, beside `PositionerBehavior`, or Dialog's Popup): `Node` and `Hover.CloseDelay`; its requests (the hover leave) reach the store. Inside a portal (`PortalContext`, the cascaded `PopupPortalContext`) its first attach awaits `PopupPortalContext.WhenMoved`, as `PositionerBehavior`'s does (design point 3 of M4 track 1). |
| `InteractionsProviderBehavior.cs` | A tooltip provider's handle: draws the provider token its tooltips' triggers carry (`InteractionsHoverOptions.DelayGroupToken`), attaches by token, sends the group's delays. |
| `InteractionsAttachment.cs` | The attach-then-flush cycle the four share; the first attach awaits an optional `ready` task (the portal's move) and builds its options after it; counts the records sent (`Sends`, read by the L2 tests). |
| `OpenChangeRequest.cs` | `(bool Open, OpenChangeEventDetails Details, bool Instant, InteractionType? InteractionType)`: what a store receives; `Instant` is the delay group's instant phase, `InteractionType` a press's (mapped by `InteractionTypeLiterals.FromLiteral`; the store records it with `SetOpenInteractionType`). `InteractionsRequests` maps the payloads (the reason by `InteractionsLiterals.ParseReason`, the trigger as `EventTrigger(id, null)`). |

### Outputs

- Consumers: Popover (track 5: session, trigger with `Click` and `Hover`, floating), Tooltip and PreviewCard (tracks 6 and 7: session, trigger with `Hover`, `Focus`, `ClientPoint`/`HoveredLine`, floating, provider), Dialog (track 8: session with `Dismiss` and Dialog's predicates, trigger with `Click`, floating on the Popup). The test-only hosts are `tests/Blazix.Primitives.Tests.Components/Interactions/` (L2) and, at L4, the press host (`tests/Blazix.Primitives.Fixtures.Client/Interactions/`) and track 1's popup host, whose `FixturePopupRoot` composes the session.

### Human check

- No family adds its own dismissal, hover or group timer, tree, session map or C# press-to-toggle: they compose these behaviours.

## The focus manager's behaviour (M4 track 4, issue 120)

The C# half of the `focus-manager` module (`../js/focus-manager.ts`, twin `../Interop/FocusManagerTwin.cs`), every type internal. Design point 6 of M4 track 4 (option (a), `Floating/`) was settled by the orchestrator 2026-10-27; this section follows track 3's.

### Inputs

- FLT §4.4 (FLT-DOM-13..15), §4.8 (FLT-FOC-06..22), FLT-API-05, FLT-KEY-06; §10 item 17.
- ADR 01 Amendments 2026-10-27: the commit-path `update` (design point 1 of M4 track 4, chosen by the user 2026-10-27).
- ADR 04 §1 (the `focus-manager` row) and Amendments 2026-10-27: the focus targets, element-typed option values (`../Interop/TwinReferences.cs`), the relay, the answered counter, the portal's published state (`../Popup/PopupPortalContext.cs`, `PortalFocusPublication`).
- ADR 16 rule 7 (allocation budgets), rule 10 (behaviours composed into parts) and Amendments 2026-10-27 (options value equality).

### Job

| File | Role |
|---|---|
| `FocusManagerBehavior.cs` | The focus manager of one Popup part, composed as `ButtonBehavior` is. It owns the `FocusManagerTwin`; `SetInputs(FocusManagerInputs)` on every parameter set sends changed options at once to an attached module, before the render that applies them; `BuildBeforeElement`/`BuildAfterElement` render the inside guards iff `!Disabled && (Modal \|\| InsidePortal)` (FLT-DOM-15, DOM-13); `Portal` (the cascaded `PopupPortalContext`) receives `PortalFocusPublication(Modal, Open, CloseOnFocusOut)` while enabled and `null` otherwise (FLT-FOC-18); `AfterRenderAsync(element, id)` attaches after `PopupPortalContext.WhenMoved(portal)` and drops an attach whose popup left the document; the focus-out request goes to the part's `requestClose` and raises `Answered` for every request; the close type is classified when `CloseEvent` changes. The options are built at attach and when an input, `Answered` or the close type changed, never for an unchanged render (ADR 16 rule 7). |
| `FocusManagerInputs.cs` | The inputs of one parameter set: `Open`, `Modal`, `Disabled`, `InitialFocus`/`ReturnFocus` (`FocusTargetValue?`, resolved by the family), `ExplicitReturnFocus`, `RestoreFocus`, `CloseOnFocusOut`, `OpenInteractionType` (`null` for a programmatic open), `CloseEvent`, `OpenSelector`, `TreatPopupAsFloatingElement`, `DomReferenceId`, `TriggerFocusGuards`, `PreviousFocusableElement`, `NextFocusableElement`, `InsideElements`, `InsideElementIds`, `Relay`, `SessionToken` (the popup store's, PSH-STR-01). Value equality allocates nothing. |
| `CloseTypeClassifier.cs` | `Classify(EventSnapshot?, InteractionType?) : InteractionType`, FLT-FOC-10's `getEventType` over the close's snapshot and the recorded type; `Unknown` is Base UI's empty literal. |

### Outputs

- Consumers: Popover (track 5) and Dialog (track 8) Popup parts. Tests: `tests/Blazix.Primitives.Tests/Floating/CloseTypeClassifierTests.cs` (L1), `tests/Blazix.Primitives.Tests.Components/Floating/FocusManagerBehaviorTests.cs` over `TestFocusManaged` (L2), `tests/browser/focus-manager.spec.ts` over `FixtureFocusManaged` (L4).

### Human check

- A family's Popup calls `SetInputs` from `OnParametersSet` (after setting `Portal`), never only from `OnAfterRender`, or a kept-mounted or hover-opened popup marks the page for a round trip on Server.
- A `FocusTargetValue?` built in a conditional with `null` takes the implicit string conversion: return `default(FocusTargetValue?)` instead.
- An element target the opening render creates is uncaptured at that render's parameter set and names nothing (the default target); the family sets its inputs again after that render, with the captured reference.
