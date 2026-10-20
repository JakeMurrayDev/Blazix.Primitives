# src/Blazix.Primitives/Events

Event details, reasons and the controlled-value helper: what a part hands to a consumer's change callback and how a request runs. Namespace `Blazix.Primitives` (ADR 08 §1).

## Inputs

- `docs/architecture/event-details-and-reasons.md` (EVD): §3 the surface, §3.5 the 36 reasons, EVD-ALG-01..03/08/09/11, EVD-STA-01..10, EVD-EDGE-01..03.
- `docs/architecture/shared-utilities.md` (UTL) §4.1 (`useControlled`) and UTL-STA-36 (`useValueChanged`).
- Decision records 04 §3 (the event snapshot), 05 §2/§3 and its Amendments 2026-10-18 (controlled mode fixed, the native text edit exception), 08 §3/§4 (names), 16 rule 7 (budgets).

## Job

- `ChangeEventDetails` (and `OpenChangeEventDetails`, `TabsChangeEventDetails`, `SliderChangeEventDetails`), `GenericEventDetails` (and `HighlightEventDetails`): one mutable object per request, live `IsCanceled` / `IsPropagationAllowed`, shared along a child-then-parent chain. Plain classes, not `closed` (ADR 05 Amendments 2026-10-20, closed hierarchies not applied to the details types).
- `EventReason` with `ToLiteral()`; `ValueChangeEventArgs<T>`; `EventTrigger`.
- `EventSnapshot`, `details.Event` in C#: the shape below is the contract with the JavaScript that takes the snapshot (ADR 04 §3).
- `Controlled<T>`: controlled iff the value parameter was supplied at the first parameter set; the request sequence `OnValueChange` → stop if canceled → `ValueChanged` → commit when uncontrolled; `NotifyAsync`/`CommitAsync` for a request that continues into a parent.
- Internal: `ValueChangedObserver<T>` (`useValueChanged`), `HighlightReason.From` (`getHighlightReason`), `DevelopmentWarnings` (the switch).
- The folder also holds the handler veto extensions (`PreventPartHandler`, ADR 02 decision 3), which the pipeline owns.

### The event snapshot JSON

JavaScript serialises the native event inside its dispatch as one object with exactly these properties (camelCase; `null` where the event has no such field):

| Property | Type | Meaning |
|---|---|---|
| `kind` | string | the most derived DOM interface: `Event`, `UIEvent`, `FocusEvent`, `MouseEvent`, `PointerEvent`, `WheelEvent`, `TouchEvent`, `KeyboardEvent`, `InputEvent`, `ClipboardEvent` |
| `type` | string | `event.type` |
| `key`, `code` | string or null | `KeyboardEvent.key`, `.code` |
| `altKey`, `ctrlKey`, `metaKey`, `shiftKey` | boolean | modifier flags; `false` where absent |
| `pointerType` | string or null | `PointerEvent.pointerType` |
| `detail` | number | `UIEvent.detail` (0 for a keyboard-generated click) |
| `buttons` | number | `MouseEvent.buttons` |
| `target` | object or null | `{ "id": string or null, "tagName": string or null }` |
| `defaultPrevented` | boolean | `event.defaultPrevented` when the snapshot was taken |
| `timeStamp` | number | `event.timeStamp` in milliseconds |

A request with no native event carries a fresh placeholder: `kind` `Event`, `type` `base-ui` (EVD-API-04).

### Development warnings

EVD-EDGE-01/02 (and the store's UTL-EDGE-25) go to the `ILogger` the part passes, at `Error`, only while the runtime switch `Blazix.Primitives.DevelopmentWarnings` is on (`AppContext.SetSwitch`, or `<RuntimeHostConfigurationOption Include="Blazix.Primitives.DevelopmentWarnings" Value="true" />` in a Debug build). Each distinct message is logged once per `Controlled<T>` instance. Base UI compares defaults by JSON serialisation; the port compares by `EqualityComparer<T>.Default`, then structurally for arrays and tuples, then item by item for sequences, never throwing.

## Outputs

- Types consumed by every family's change callbacks and by the twins that report native events.
- Tested at L1 by `tests/Blazix.Primitives.Tests/Events/` (Base UI titles where a Base UI test exists).

## Human check

- A new details subclass is added only for a field EVD §3.7 lists, and keeps the single shared object along a chain.
- `EventReason` keeps 36 members in EVD §3.5 order; `EventReasonTests` read the table from the document.
- A change to the snapshot JSON changes the JavaScript that writes it in the same pull request.
- The receiverless `ValueChangeEventArgs<T>` delivery stays allocation-free (ADR 16 rule 7).
