# src/Blazix.Primitives/Floating

The C# half of FLT (`docs/architecture/floating-and-positioning.md`): the behaviours a popup family's parts compose for the `interactions` module (this section, M4 track 3) and for the `positioning` module (track 2's section). ADR 09 Amendments 2026-10-27 names the folder; design point 8 of M4 track 3 (option (a), `Floating/`) was settled by the orchestrator 2026-10-27, and whichever of tracks 2 and 3 lands first creates this file, the other adding its section. Namespace `Blazix.Primitives`, every type internal: Base UI's floating-ui-react hooks have no public parts.

## The interactions behaviours (M4 track 3, issue 119)

### Inputs

- FLT §4.1–§4.10, §5.3 and §6.3–§6.9 (the options); PSH-STA-33 (the trigger map), PSH-PTR-01/02 and PSH-ALG-19 (the open interaction type).
- ADR 04 Amendments 2026-10-27 (chosen by the user 2026-10-27): the token form of `attach` and the per-popup session, the relay, `press`, the verdict `Prevent` at `escape`, the hover timers and the delay group, Dialog's `outsidePress` predicates. ADR 01 relaxation rows "C#-origin consumer vetoes", "EVD-EVT-11, EVD-DIS-01, FLT-KEY-01", "EVD-EVT-18, PSH-ANI-03". ADR 02 Amendments 2026-10-21 (`ForwardsConsumerHandler`, the decline-and-run gate with the one-shot marker). ADR 05 §2 (the open-change sequence the family's store runs). ADR 16 rules 6 and 7.
- Design points of M4 track 3: 1 (the token form), 2 (the roles), 5 (the press payload), 8 (`Floating/`) and 9 settled by the orchestrator 2026-10-27, and 6 (the names) accepted the same day, each as recommended (ADR 04 Amendments 2026-10-27).

### Job

| File | Role |
|---|---|
| `InteractionsSessionBehavior.cs` | A popup root's session: joins the token it is given (a popup family passes its store's `SessionToken`, PSH-STR-01, so every module of the popup shares one session) or draws one (`PartIdGenerator`), never renders it, attaches the session handle by token (`TwinBase.AttachTokenAsync`), sends `Open`, `ActiveTriggerId`, `ParentToken`, `FloatingId`, `Ending`, `Dismiss` and `Relay` (the store's `OpenChangeRelay`) when they changed (compared before a record is built), and answers every request it routes with `Answered` (design point 10: the counter moves before the store's sequence, so a commit's render carries it and a cancel sends it alone; the root therefore sets `Open` and `ActiveTriggerId` in its commit path). Routes every `OnRequest` of the session's twin into the store (`OpenChangeRequest`), and answers `escape` on WebAssembly by running the store's request with `EventReason.EscapeKey` synchronously and mapping `IsCanceled`/`IsPropagationAllowed` to `PreventAndStop`, `Prevent`, `Stop`, `Run`. A popup family sets the inputs from the store's committed state in its commit path (a selector subscription on `Open`, `ActiveTriggerId`, `OpenChange`, `TransitionStatus` and `PopupId`) and again each render: `FixturePopupRoot` in the fixture project is the shape. |
| `InteractionsTriggerBehavior.cs` | A trigger's handle: attaches to the trigger's own element (by `ElementReference` or the id it renders on that element), sends `InteractionsTriggerOptions` when they changed, declines the consumer's `onclick` while `Click` is set, and runs it once per press: inside `press` on WebAssembly (a `PreventPartHandler()` answers `stop`, else the request runs), or from the `OnPress` report on Server, whichever of the report and Blazor's dispatch of the click arrives first running the handler and the other reading its verdict. Every other request of the trigger (hover, focus, reference press) arrives as `OnRequest`. |
| `InteractionsFloatingBehavior.cs` | The floating element's handle (a Positioner, or Dialog's Popup): `Node` and `Hover.CloseDelay`; its requests (the hover leave) reach the store. Inside a portal (`PortalContext`, the cascaded `PopupPortalContext`) its first attach awaits `PopupPortalContext.WhenMoved` (design point 3 of M4 track 1). |
| `InteractionsProviderBehavior.cs` | A tooltip provider's handle: draws the provider token its tooltips' triggers carry (`InteractionsHoverOptions.DelayGroupToken`), attaches by token, sends the group's delays. |
| `InteractionsAttachment.cs` | The attach-then-flush cycle the four share; the first attach awaits an optional `ready` task (the portal's move) and builds its options after it; counts the records sent (`Sends`, read by the L2 tests). |
| `OpenChangeRequest.cs` | `(bool Open, OpenChangeEventDetails Details, bool Instant, InteractionType? InteractionType)`: what a store receives; `Instant` is the delay group's instant phase, `InteractionType` a press's (mapped by `InteractionTypeLiterals.FromLiteral`; the store records it with `SetOpenInteractionType`). `InteractionsRequests` maps the payloads (the reason by `InteractionsLiterals.ParseReason`, the trigger as `EventTrigger(id, null)`). |

### Outputs

- Consumers: Popover (track 5: session, trigger with `Click` and `Hover`, floating), Tooltip and PreviewCard (tracks 6 and 7: session, trigger with `Hover`, `Focus`, `ClientPoint`/`HoveredLine`, floating, provider), Dialog (track 8: session with `Dismiss` and Dialog's predicates, trigger with `Click`, floating on the Popup). The test-only hosts are `tests/Blazix.Primitives.Tests.Components/Interactions/` (L2) and, at L4, the press host (`tests/Blazix.Primitives.Fixtures.Client/Interactions/`) and track 1's popup host, whose `FixturePopupRoot` composes the session.

### Human check

- No family adds its own dismissal, hover or group timer, tree, session map or C# press-to-toggle: they compose these behaviours.
