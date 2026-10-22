# src/Blazix.Primitives/Composite

The C# half of the composite mechanism: the root and item behaviours a roving group's parts compose (ADR 09 Amendments 2026-10-22: a mechanism's C# half lives in a folder named after the mechanism when no family owns it). The JavaScript half is `../js/composite.ts` and the twin is `../Interop/CompositeTwin.cs`. Namespace `Blazix.Primitives`, every type internal (Base UI's `CompositeRoot`, `CompositeItem` and `CompositeList` are internal, CMP §3; ADR 08 §1 gives them no part names).

## Inputs

- `docs/architecture/composite-and-list-navigation.md` (CMP): §4.2.1 and §4.2.3 (registry and highlight), §4.3 (`tabindex`, the active marker, no attribute on the root), §4.5 (keys), §4.12 (CMP-EVT-01, CMP-EVT-12), §6.1–§6.6 (the options), §6.11 (constants), §7 (CMP-STR-02/CMP-EDGE-01), §8.
- `docs/architecture/shared-utilities.md` §4.15.1 (the direction it reads from `../DirectionProvider/`).
- Decision records: 01 decision 2 (the registry is JavaScript's) and the relaxation rows "EVD-STA-07, CMP-STA-10, FFI registration" and "… EVD-EVT-26 against composite/button key handling"; 02 Amendments 2026-10-21 (`ForwardsConsumerHandler`, the decline-and-run gate); 04 §1, §3 and Amendments 2026-10-18 (handover at attach; dispatch-time work is JavaScript's), 2026-10-20 (the RadioGroup options are this module's), 2026-10-22 (the module owns the items' `tabindex` from attach; the two decision points, names chosen by the user the same day); 08 §3 and the `Orientation` note; 09 Amendments 2026-10-22; 16 rules 2, 6, 7, 9, 10.

## Job

| File | Role |
|---|---|
| `CompositeRootBehavior.cs` | One root's mechanism, composed into the root part as `ButtonBehavior` is (never a nested component, ADR 16 rule 10). It owns one `CompositeTwin` and its handle, and attaches it as `ButtonBehavior` does: by `Element`, by the generated id under a substitute callback, or by a late id. The part sets the options on every parameter set: `Orientation` (a literal from `CompositeLiterals`), `LoopFocus`, `EnableHomeAndEndKeys`, `StopEventPropagation`, `HighlightItemOnHover`, `ModifierKeys`, `DisabledIndices`, `SelectOnNavigation`, `ReportFocusLeave`, `ReportHighlight`. It reads the nearest `DirectionProviderContext` (`ltr` without one) and re-renders the part when the direction changes. It sends one `update` per render batch that changed the options, the item ids, an item's consumer `tabindex` or an item's element, and nothing for an unchanged render. It mirrors the reports: `HighlightedIndex` (0 until a report) with `HighlightChanged` raised once per change, and `FocusLeft`. It gates the consumer's `onkeydown`: the part declines it and runs it from its own handler, or the `navigate` decision runs it on WebAssembly, with the one-shot marker. Pending the user (option (i) of ADR 04's note of 2026-10-22): `navigate` runs the focused item's handler first, on the same event, and on Server the module's handled-key report runs both; `ReportHandledKeys` is sent only while a handler exists. The part's own chains are `NavigationDecider` and `SelectionDecider`. |
| `CompositeRootContext.cs` | The registry the root part cascades with `IsFixed` inside its element (ADR 16 rule 9, BLZX2001). Items `Register`, `Unregister`, report `RegistrationChanged` and `ElementReplaced`, and ask for `FlushAsync`. `Require` throws the error of an item outside a root (CMP-STR-02, CMP-EDGE-01, internal guards). |
| `CompositeItemBehavior.cs` | One item's mechanism. Given the part's own `onkeydown` handler, it declines the consumer's and runs it from that handler with the one-shot marker, and the root runs it for a key it handles (pending the user, option (i) of ADR 04's note of 2026-10-22). It registers the id its part renders (the generated id, or the consumer's) with the consumer's `tabindex`, in the batch that creates the part, and unregisters on dispose. `AddAttributes` writes `tabindex="-1"` on every render: the initial value under the handover, unchanged afterwards, so Blazor never writes it again, while the consumer's value wins in the merge. After render it asks the root to send what changed, and reports a new element under the same id. |
| `CompositeLiterals.cs` | `Orientation?` → `horizontal`, `vertical`, `both` (`null`); `TextDirection` → `ltr`, `rtl`. |
| `CompositeAttributes.cs` | `ActiveItem = "data-composite-item-active"` (CMP §6.11): the attribute Radio (RAD-DOM-02) and Tabs write on the item that takes the initial tab stop. |

Removal-only batches: a batch that only removes items runs no item's after-render. The item's dispose therefore makes the root render, unless the root already rendered in that batch, and the root's after-render sends the one `update`. L2 proves both paths (`CompositeRootTests`: the nested unmount, and a parent removing an item).

What JavaScript owns (not here): the item order and move detection, the highlighted index and its publication adjustments, the key resolution, the focus moves, the roving `tabindex` from attach, the pending-navigation flag and the focus-driven selection, the leaving blur.

## Traceability (the sources carry no specification references)

- `CompositeRootBehavior`: CMP-STR-01, CMP-STA-10 (one rebuild per batch; Server relaxation row), CMP-STA-22 (root-owned highlight), CMP-EVT-01 (the mirror), CMP-EVT-12 (consumer `onKeyDown`, Server relaxation row), CMP-DOM-03 (no attribute on the root), CMP-I18N-01 (direction from the provider), CMP-KEY-15 (the part's `stopEventPropagation` default `true`), RDG-STA-06, RAD-FOC-05, RDG-FOC-04/05 (the RadioGroup options and reports).
- `CompositeItemBehavior`: CMP-STA-15 (index `-1`, `tabindex="-1"`, before the first publication), CMP-DOM-01 under ADR 04 Amendments 2026-10-22, RAD-DOM-16 (a consumer `tabindex` wins).
- `CompositeRootContext`: CMP §6.3, §6.6, CMP-STR-02, CMP-EDGE-01.

## Outputs

- `CompositeRootBehavior`, `CompositeRootContext`, `CompositeItemBehavior`, `CompositeLiterals`, `CompositeAttributes`. Consumers: RadioGroup and Radio (M2, track 8); ToggleGroup, Toolbar, Tabs and their items (M3).
- Tested at L1 (`tests/Blazix.Primitives.Tests/DirectionProvider/TextDirectionTests`: the literals), L2 (`tests/Blazix.Primitives.Tests.Components/Composite/` on the test-only `TestCompositeRoot` and `TestCompositeItem`; `Interop/CompositeTwinContractTests`) and L4 (`tests/browser/composite.spec.ts` on the `composite-*` fixtures).

## Human check

- No internal component instance: the behaviours are composed into the parts, and the cascade is the only component the root adds (ADR 16 rule 10).
- One `update` per render batch: a render that changes nothing sends nothing, and the options are compared field by field before a record is built (ADR 16 rule 7; `CompositeAllocationBudgetTests` only tightens).
- JavaScript owns every item's `tabindex` after attach: C# renders `-1` and never another value, and no C# code mirrors the highlighted index into an attribute.
