# src/Blazix.Primitives/Events

Event details, reasons and the controlled-value helper: what a part hands to a consumer's change callback and how a request runs. Namespace `Blazix.Primitives` (ADR 08 §1).

## Inputs

- `docs/architecture/event-details-and-reasons.md` (EVD): §3 the surface, §3.5 the 36 reasons, EVD-ALG-01..03/08/09/11, EVD-STA-01..10, EVD-EDGE-01..03.
- `docs/architecture/shared-utilities.md` (UTL) §4.1 (`useControlled`) and UTL-STA-36 (`useValueChanged`).
- Decision records 04 §3 (the event snapshot), 05 §2/§3 and its Amendments 2026-10-18 (controlled mode fixed, the native text edit exception), 08 §3/§4 (names), 16 rule 7 (budgets).

## Job

The public surface, with what each member does (requirement IDs follow for traceability):

- `EventReason`: the cause of a request, an enum of 36 members in this order: `None`, `TriggerPress`, `TriggerHover`, `TriggerFocus`, `OutsidePress`, `ItemPress`, `ClosePress`, `LinkPress`, `ClearPress`, `ChipRemovePress`, `TrackPress`, `IncrementPress`, `DecrementPress`, `InputChange`, `InputClear`, `InputBlur`, `InputPaste`, `InputPress`, `FocusOut`, `EscapeKey`, `CloseWatcher`, `ListNavigation`, `Keyboard`, `Pointer`, `Drag`, `Wheel`, `Scrub`, `PopupClose`, `CancelOpen`, `SiblingOpen`, `Disabled`, `Missing`, `Initial`, `ImperativeAction`, `Swipe`, `WindowResize`. `reason.ToLiteral() → string` returns the kebab-case Base UI literal (`TriggerPress` → `"trigger-press"`) and throws `ArgumentOutOfRangeException` for a value outside the enum (EVD §3.5).
- `ChangeEventDetails(EventReason reason, EventSnapshot? @event = null, EventTrigger? trigger = null)`: the mutable details of a cancelable request.
  - `Reason`, `Event`, `Trigger`: `Event` is the native event's snapshot, or a fresh placeholder (`Type == "base-ui"`) when `@event` is `null`. `Trigger` is `null` when no element started the request.
  - `Cancel()` sets `IsCanceled`: an uncontrolled part then does not commit and skips the follow-up effects.
  - `AllowPropagation()` sets `IsPropagationAllowed`: the part then does not stop the native event's propagation.
  - Both flags start `false` and are read live (EVD-ALG-01, EVD-API-06).
  - One object is passed unchanged from a child's callback to its parent's (EVD-EVT-04).
- Subclasses of `ChangeEventDetails`:
  - `OpenChangeEventDetails` adds `PreventUnmountOnClose()`, which keeps a closing popup mounted until its root's `unmount` action runs; the popup honours it only for a close that was not canceled.
  - `TabsChangeEventDetails(reason, TabsActivationDirection activationDirection, …)` adds `ActivationDirection`: `Left`, `Right`, `Up`, `Down`, or `None` for automatic changes.
  - `SliderChangeEventDetails(reason, int activeThumbIndex, …)` adds `ActiveThumbIndex`.
- `GenericEventDetails(EventReason reason, EventSnapshot? @event = null)`: `Reason` and `Event` only, with no cancel, no propagation flag and no trigger. Used for notifications that cannot be canceled (EVD-API-02). `HighlightEventDetails(reason, int index, …)` adds `Index`, which is `-1` when the highlight was cleared.
- All details types are plain classes, not `closed`. On SDK 11.0.100-rc.2 a closed class is implicitly abstract (CS9384), so a closed `ChangeEventDetails` could not be constructed (ADR 05 Amendments 2026-10-20, the user's choice).
- `readonly record struct ValueChangeEventArgs<T>(T Value, ChangeEventDetails Details)`: the single argument of every `On<X>Change` callback.
  - Delivered through a receiverless `EventCallback` it allocates nothing.
  - Delivered through a component receiver it costs about 66 bytes per delivery: a 32-byte box plus Blazor's own dispatch.
- `EventTrigger(string? Id, ElementReference? Element)`: the element that started a request. `Element` is `null` when only JavaScript holds it.
- `EventSnapshot`: the native event's fields as a record (shape below). `EventSnapshot.CreatePlaceholder()` returns a new snapshot with `Kind = Event` and `Type = "base-ui"`. `IsInstanceOf(EventKind kind)` answers JavaScript's `instanceof` with the DOM's inheritance: every snapshot is an `Event`; focus, mouse, pointer, wheel, touch, keyboard and input events are `UIEvent`s; pointer and wheel events are `MouseEvent`s.
- `Controlled<T>(string name, string state = "value", ILogger? logger = null)`: one controlled/uncontrolled value.
  - `SetParameters(ParameterView parameters, string valueParameterName, string defaultValueParameterName)`, called from `SetParametersAsync` on every parameter set, or its raw form `Update(bool valueSupplied, T value, bool defaultValueSupplied, T defaultValue)`, records the parameters.
  - The first call fixes `IsControlled` (the value parameter was supplied) and the uncontrolled value (the default as supplied; a delegate is stored, never invoked).
  - `Value` is the controlled parameter while it is controlled and supplied, otherwise the uncontrolled value. A removed controlled value therefore falls back to the initial default.
  - `SetValueIfUncontrolled(T value)` stores the value only when uncontrolled.
  - `RequestAsync(T value, ChangeEventDetails details, EventCallback<ValueChangeEventArgs<T>> onValueChange, EventCallback<T> valueChanged, bool nativeTextEdit = false) → Task<bool>` invokes `onValueChange`. If the details are now canceled it returns `false`; otherwise it invokes `valueChanged`, commits when uncontrolled, and returns `true` so the caller runs its follow-up effects.
  - With `nativeTextEdit: true`, an uncontrolled request that was canceled still invokes `valueChanged` and commits, because the browser already changed the text, but returns `false`.
  - `NotifyAsync` (the first step, returns "not canceled") and `CommitAsync` (`valueChanged`, then commit) are the two halves for a request that continues into a parent with the same details (EVD-ALG-03/08, ADR 05 §2).
- Internal:
  - `ValueChangedObserver<T>(T initial, Action<T> onChange).Observe(T value)` calls `onChange(previous)` when the value changed by `Object.is` and is strictly unequal, so `0 → -0` and `NaN → NaN` are silent.
  - `HighlightReason.From(EventSnapshot?)` returns `Keyboard` for `key*` types, `Pointer` for `mouse*` and `pointer*` types, and `None` otherwise or with no event.
  - `DevelopmentWarnings` is the switch below.
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

The library's one debug switch is the AppContext switch `Blazix.Primitives.DevelopmentWarnings` (internal `DevelopmentWarnings.IsEnabled`; `DevelopmentWarnings.Write(ILogger, string)` logs at `Error`). Any later development-only check, such as one of the pipeline's, should go through it. EVD-EDGE-01/02 (and the store's UTL-EDGE-25) go to the `ILogger` the part passes, at `Error`, only while the runtime switch `Blazix.Primitives.DevelopmentWarnings` is on (`AppContext.SetSwitch`, or `<RuntimeHostConfigurationOption Include="Blazix.Primitives.DevelopmentWarnings" Value="true" />` in a Debug build). Each distinct message is logged once per `Controlled<T>` instance. Base UI compares defaults by JSON serialisation; the port compares by `EqualityComparer<T>.Default`, then structurally for arrays and tuples, then item by item for sequences, never throwing.

## Outputs

- Types consumed by every family's change callbacks and by the twins that report native events.
- Tested at L1 by `tests/Blazix.Primitives.Tests/Events/` (Base UI titles where a Base UI test exists).

## Human check

- A new details subclass is added only for a field EVD §3.7 lists, and keeps the single shared object along a chain.
- `EventReason` keeps 36 members in EVD §3.5 order; `EventReasonTests` read the table from the document.
- A change to the snapshot JSON changes the JavaScript that writes it in the same pull request.
- The receiverless `ValueChangeEventArgs<T>` delivery stays allocation-free (ADR 16 rule 7).
