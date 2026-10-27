# src/Blazix.Primitives/Popup

The C# half of the popup store and handles: the store record, the store and its trigger registry, the public handle, the shared value types and data-attribute constants, and the behaviours every popup family's Root, Trigger, Portal and Popup compose (ADR 09 Amendments 2026-10-22 and 2026-10-27: a mechanism's C# half lives in a folder named after the mechanism when no family owns it). The JavaScript halves are `../js/portal.ts` and `../js/scroll-lock.ts`, the twins `../Interop/PortalTwin.cs` and `../Interop/ScrollLockTwin.cs`, and the relay record `../Interop/OpenChangeRelay.cs`. Namespace `Blazix.Primitives`; the public types are the ones Base UI exports (the handle, `ModalValue`, `InteractionType`, `PortalContainerValue`, `FocusTargetValue`, `NoPayload`, the data-attribute constants), every other type is internal.

## Inputs

- `docs/architecture/popup-store-and-handles.md` (PSH): the store (§4.4), the trigger registry and the reconciliation (PSH-ALG-03..05, PSH-STA-15..29), the open-change sequence (PSH-ALG-01/02), the handle (§4.2, PSH-API-01..09), the portal (PSH-STR-11/12), the transitions plumbing (PSH-STA-44/45).
- `docs/architecture/floating-and-positioning.md` (FLT): the portal (FLT-API-13, FLT-DOM-11..14, FLT-ARIA-04/05, FLT-FOC-18's portal side).
- `docs/architecture/transitions-and-mounting.md` (TRN-ALG-01/05, TRN-ANI-19..27); `docs/architecture/shared-utilities.md` §4.8 (the scroll lock, UTL-DOM-01..12); `docs/architecture/event-details-and-reasons.md` (EVD-ALG-04/05).
- M4 track 1 ([issue 117](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/117)) and the decisions it builds: ADR 01 decision 3's amendment (the render-batch boundary); ADR 04 Amendments 2026-10-27 (the relay, the references array, the portal's decision 5, the scroll lock, the portal's move as a task); ADR 05 Amendments 2026-10-27 (the store is C#'s); ADR 08 Amendments 2026-10-27 (the names, the payload template from issue 116's probe); ADR 16 Amendments 2026-10-27 (the reconciliation boundary, design point 8).

## Job

| File | Role |
|---|---|
| `PopupStoreState.cs` | The shared record every family's store record nests (`IPopupStoreState<TSelf, TPayload>.Popup`): open and the controlled prop, mounted and the transition status, the trigger count, the active trigger's id and element, the popup and positioner elements, the relay (`OpenChange`), the open method; the computed effective values. |
| `PopupStore.cs` | The store: a `ComponentStore<TState>` a family subclasses for its record and its steps of the open-change sequence. It holds the non-reactive context (the registry, the controlled open and trigger id, the payload, the callbacks, the relay's revision, the session token). `RequestOpenAsync` runs the consumer's `OnOpenChange` first and stops on a cancel, then `OpenChanged`, the family's `BeforeDispatch`, the relay and one write. Every write that changes the effective open state evaluates mounted and the status (TRN-ALG-01). |
| `PopupStore.cs`, the render-batch boundary | `AtBatchBoundaryAsync` runs the reconciliation (PSH-ALG-03) when a write changed its inputs (open, the trigger count, the active trigger's id and element), then a pending unmount action. **Who brings the boundary** (design point 8, ADR 16 Amendments 2026-10-27): a popup part that selects the open or mounted state (every family's Portal and Popup) calls `RegisterBoundaryPart()` at subscription, disposes the registration with the part, and calls `AtBatchBoundaryAsync` from its `OnAfterRender`; the Root's and the trigger behaviour's `OnAfterRender` call it too. While no such part is registered, the store raises `BoundaryRequested` on every write that changes the reconciliation's inputs, so the Root renders and brings the boundary (the shape of Base UI's bare-store tests); the reconciliation's own writes never ask. A registered trigger disposed while registered always asks. |
| `PopupStore.cs`, the relay | `OpenChangeRelay` moves its `Revision` on every committed open change. An open committed after a close that no boundary has seen carries `CoalescedCloseRevision` and `CoalescedCloseReason` (ADR 04 Amendments 2026-10-27, the relay's Server semantics). |
| `PopupTriggerRegistry.cs`, `PopupTriggerElement.cs` | The registry keyed by id, in registration order, with the reverse index that throws on one element under two ids (PSH-EDGE-07). An element is the (part, `ElementReference?`) pair: registered at the trigger's parameter set and again after its first render. |
| `PopupHandle.cs` | The public handle (`<Family>Handle<TPayload>`, created with `new`): `IsOpen`, `OpenAsync(triggerId)`, `CloseAsync()`. It attaches to the Root's store, keeps an inert fallback store while detached, and warns of two Roots on one handle in development. |
| Payload template (for the family Roots) | A family Root's parameters are `ChildContent : RenderFragment?` and `PayloadContent : RenderFragment<TPayload?>?` (ADR 08 Amendments 2026-10-27, the probe's finding). Its XML documentation says that two nested Roots each using `PayloadContent` without `Context` fail to compile (`RZ9999`, the `context` name clash) and that `Context="payload"` names it. The Trigger tells an omitted `Payload` from `default(TPayload)` by whether the parameter was supplied (`ParameterView.TryGetValue`). |
| `PopupRootBehavior.cs`, `PopupRootParameters.cs` | The Root: creates the store at the first parameter set (the popup id, then the session token), applies the controlled open and trigger id, cascades the store fixed around the child content and the payload template, attaches the handle at its first `OnAfterRender`, runs the boundary. It subscribes only to what its own parameters need (the payload version when it renders the payload template). |
| `PopupTriggerBehavior.cs` | The Trigger: registers by id at parameter set, forwards its payload while active, migrates when the cascaded store changes, renders `aria-expanded`, `aria-haspopup`, `aria-controls` and the open state attributes, runs the boundary after its renders. |
| `PopupSelectors.cs`, `PopupStateMapping.cs`, `PopupDataAttributes.cs`, `PopupInstantChange.cs` | The named selectors; the four state mappings (trigger open, pressable trigger open, popup open, anchor hidden); the data-attribute and CSS-variable constants; the instant-change literal a family's store maps from the reason. |
| `PopupPortalBehavior.cs`, `PopupPortalContext.cs`, `PortalFocusPublication.cs` | The portal (FloatingPortal and FloatingPortalLite): the part's host is the node `<div id data-base-ui-portal>`, wrapped by the pipeline's `display: contents` host wrapper and rendered `display: none` (frozen from the first render) until the `portal` module moves it. The outside guards and the `aria-owns` owner render beside the wrapper while a non-modal, open focus manager has published to the portal's context, never in the lite portal. A pending container renders no node. The context cascades the node (a nested portal's default container), the `Moved` task a portaled part awaits before its first attach, and the publication slot the focus manager fills (track 4). |
| `PopupScrollLockBehavior.cs` | The scroll lock a family composes (Dialog's modal open, Popover's anchored decision): `ApplyAsync(referenceElement, enabled)` from the part's `OnAfterRenderAsync`, attaching once enabled and sending `Enabled` only on change. |
| `ModalValue.cs`, `ModalTrapFocus.cs`, `InteractionType.cs`, `InteractionTypeLiterals.cs`, `PortalContainerValue.cs`, `PortalContainerPending.cs`, `FocusTargetValue.cs`, `NoPayload.cs` | The public value types: `bool` or `trap-focus`; the interaction type and its literals; the portal container (an element, a script-held node, a selector, or pending); a focus target; the payload type of a popup without payloads. |

What JavaScript owns (not here): moving the portal node and its cleanup when Blazor removes the wrapper, the VoiceOver swap of the guards, the scroll lock's page-level writes and its page-global count, every `openchange` a module emits from the relay.

## Traceability (the sources carry no specification references)

- `PopupStore`, `PopupStoreState`: PSH-ALG-01..05, PSH-STA-01..29, PSH-STA-40..45, PSH-EVT-01, PSH-ANI-04, PSH-DIS-05/06, EVD-ALG-04/05, TRN-ALG-01/05, TRN-ANI-19..27.
- `PopupHandle`: PSH-API-01..09, PSH-EDGE-06.
- `PopupTriggerRegistry`, `PopupTriggerElement`: PSH-STA-15..17, PSH-EDGE-07.
- `PopupRootBehavior`, `PopupTriggerBehavior`: PSH-STR-01/03/04, PSH-STA-16/18..25, PSH-ARIA-01..04/08.
- `PopupPortalBehavior`, `PopupPortalContext`: FLT-API-13, FLT-DOM-11..14, FLT-ARIA-04/05, FLT-FOC-18, PSH-STR-11/12.
- `PopupScrollLockBehavior`: UTL-DOM-11/12.

## Outputs

- The store, the behaviours and the public types above. Consumers: Popover, Tooltip, PreviewCard, Dialog and AlertDialog (M4 tracks 5 to 8), then Menu, Select, Combobox and NavigationMenu.
- Tested at L1 (`tests/Blazix.Primitives.Tests/Popup/` on the test-only family store), L2 (`tests/Blazix.Primitives.Tests.Components/Popup/` on `TestPopupRoot`, `TestPopupTrigger`, `TestPopupPopup` and `TestPopupPortal`; `Interop/PortalTwinContractTests`, `Interop/ScrollLockTwinContractTests`) and L3 (`../js/portal.test.ts`, `../js/scroll-lock.test.ts`).

## Human check

- No internal component instance: the behaviours are composed into the parts, and the store's cascade is the only component the Root adds (ADR 16 rule 10); every cascade is `IsFixed`.
- An open or a close re-renders neither the Root nor an inactive trigger while a popup part is registered as a boundary part (L2: `PopupStoreUtilsTests`, the render counts through `ClassValue`).
- A write that changes nothing is a no-op, and the payload lives in the store's context, never in the record.
- The portal node's `style` is the module's from attach: C# renders the frozen string and never another.
