# Combobox

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/combobox/` |
| Tests | `packages/react/src/combobox/**/*.test.{ts,tsx}` (38 files) |
| Docs page | `docs/src/app/(docs)/react/components/combobox/page.mdx` |
| Prefix | `CMB` |
| Status | approved |
| Builds on | none (the shared engine `combobox/root/AriaCombobox.tsx` is internal; Combobox is its `selectionMode: 'single' \| 'multiple'` surface) |
| Derived by | [autocomplete](autocomplete.md) (same engine with `selectionMode: 'none'`; cites CMB IDs for everything it shares) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Combobox is a text input combined with a filterable popup list of options. The user types into `Combobox.Input` to narrow the list, moves a *virtual* highlight through the items with the arrow keys (DOM focus never leaves the input; the highlighted item is exposed through `aria-activedescendant`), and commits a selection with <kbd>Enter</kbd> or a pointer press. In single-selection mode the chosen item's label is written back into the input and the popup closes; in multiple-selection mode (`multiple`) each press toggles the item, the popup stays open, and the selections are usually rendered as removable chips (`Combobox.Chips` / `Chip` / `ChipRemove`) inside `Combobox.InputGroup`. A hidden `<input>` (one per value when `multiple`) carries the selection to a form, and the whole component participates in Field/Form validation.

Two layouts are supported by the same parts. In the *input-outside-popup* layout the Input (optionally wrapped by InputGroup with a Trigger and Clear button) is the anchor and the popup is a non-modal listbox (`role="presentation"` wrapper around `role="listbox"`). In the *input-inside-popup* layout (`Combobox.Trigger` as the anchor, `Combobox.Input` rendered inside `Combobox.Popup`) the popup becomes a `role="dialog"` with a focus trap, the trigger carries the combobox ARIA, and `Combobox.Value` displays the selection in the trigger. An *inline* mode (`inline` prop) lets the list render without Portal/Positioner/Popup, typically inside a Dialog. Compared with Select, Combobox always has a text input and filters; compared with Autocomplete, Combobox *remembers* a selection (Autocomplete is the same engine configured with `selectionMode: 'none'` and is documented separately, citing this document for shared behavior).

Base component: none (this document describes the engine in `AriaCombobox.tsx` as configured by `ComboboxRoot.tsx`). Derived: Autocomplete.

## 2. Anatomy

```
<Root>                       required, 1            renders no element; provides the store, the floating context and the derived-items context; appends the hidden input(s) after its children
  <Label>                    optional, 0..n         div   (labels the Trigger only; see CMB-STR-10)
  <InputGroup>               optional, 0..1         div   role="group"; groups Input, Chips, Trigger, Clear (input-outside-popup layout)
    <Chips>                  optional, 0..1         div   role="toolbar" while it has chips (multiple mode)
      <Chip>                 optional, 0..n         div   one per selected value (consumer maps the value)
        <ChipRemove>         optional, 0..1         button
    <Input>                  optional, 0..1         input (the text control; outside or inside the popup)
    <Clear>                  optional, 0..1         button
    <Trigger>                optional, 0..n         button (the anchor when the Input is inside the popup; a secondary opener otherwise)
      <Value>                optional, 0..n         renders no element (text of the selection)
      <Icon>                 optional, 0..n         span  aria-hidden, "▼"
  <Portal>                   required for Positioner, 0..1   (FloatingPortal; container prop)
    <Backdrop>               optional, 0..1         div   role="presentation"
    <Positioner>             required for Popup/Arrow, 0..1   div role="presentation"
      <Popup>                optional, 0..1         div   role="presentation" | role="dialog" (input inside)
        <Arrow>              optional, 0..1         div   aria-hidden
        <Input>              (input-inside-popup layout)
        <Status>             optional, 0..n         div   role="status" live region
        <Empty>              optional, 0..1         div   role="status" live region, children only when the filtered list is empty
        <List>               optional, 0..1         div   role="listbox" | role="grid"; function children = closed template
          <Collection>       optional, 0..n         renders no element; maps filtered items (or a Group's items)
          <Group>            optional, 0..n         div   role="group" | role="rowgroup"
            <GroupLabel>     optional, 0..1         div   aria-hidden, id referenced by the Group
            <Collection>     …
          <Row>              optional, 0..n         div   role="row" (grid mode)
            <Item>           optional, 0..n         div   role="option" | role="gridcell"
              <ItemIndicator> optional, 0..1        span  aria-hidden; mounted only while selected unless keepMounted
          <Separator>        optional, 0..n         div   role="presentation"
```

- **CMB-STR-01** — MUST. `Combobox.Root` renders no element of its own. It renders its `children` followed by one hidden `<input>` (plus one `<input type="hidden">` per selected value when `multiple` and `name` is set, §5.2) and provides four contexts: the store, the floating root context, a `hasItems` flag and the derived-items context (`query`, `hasItems`, `filteredItems`, `flatFilteredValues`), plus the current input value.
  Source: `combobox/root/ComboboxRoot.tsx:12-34`; `combobox/root/AriaCombobox.tsx:1569-1579,1612-1721`. Test: [L2] `ComboboxRoot.test.tsx` "sets all aria attributes on the input when closed" (no wrapper element around the input); `store.test.tsx` "exposes real commands to a descendant ref callback on the first commit" (renders `Autocomplete.Root`; engine mechanics only, §16 Q12).
- **CMB-STR-02** — MUST. Every part other than Root reads the Root store and MUST throw when rendered outside `<Combobox.Root>`, with these exact messages: `useComboboxRootContext` → "Base UI: ComboboxRootContext is missing. Combobox parts must be placed within <Combobox.Root>."; `useComboboxFloatingContext` → "Base UI: ComboboxFloatingContext is missing. Combobox parts must be placed within <Combobox.Root>."; `useComboboxDerivedItemsContext` → "Base UI: ComboboxItemsContext is missing. Combobox parts must be placed within <Combobox.Root>.". Part-local contexts throw: ItemIndicator outside Item → "Base UI: ComboboxItemContext is missing. ComboboxItem parts must be placed within <Combobox.Item>."; ChipRemove outside Chip → "Base UI: ComboboxChipContext is missing. ComboboxChip parts must be placed within <Combobox.Chip>."; GroupLabel outside Group → "Base UI: ComboboxGroupContext is missing. ComboboxGroup parts must be placed within <Combobox.Group>.".
  Source: `combobox/root/ComboboxRootContext.tsx:30-58`; `combobox/item/ComboboxItemContext.ts:14`; `combobox/chip/ComboboxChipContext.ts:13`; `combobox/group/ComboboxGroupContext.ts:21`. Test: [L2] `ComboboxRootContext.test.tsx` "$name throws when its provider is missing" (×3); `ComboboxItemContext.test.tsx` "throws a descriptive error when used outside <Combobox.Item>"; `ComboboxChipRemove.test.tsx` "throws a descriptive error when rendered outside <Combobox.Chip>"; `ComboboxGroupContext.test.tsx` "throws a descriptive error when used outside <Combobox.Group>".
- **CMB-STR-03** — MUST. The component has two anchor layouts decided by where `Combobox.Input` is rendered. The store flag `inputInsidePopup` is `true` when the Input has a `Combobox.Positioner` ancestor *or* the Root is `inline`; it is `true` before any Input mounts (initial store value) and is updated when the Input element registers. In the input-inside-popup layout the Trigger is the anchor/reference element and receives the combobox ARIA; otherwise the Input is the reference element.
  Source: `combobox/root/AriaCombobox.tsx:484-485,1333-1340`; `combobox/input/ComboboxInput.tsx:64-65,95,113-125`. Test: [L2] `ComboboxPopup.test.tsx` "sets role to presentation when input renders outside the popup", "sets role to dialog when input renders inside the popup"; `store.test.tsx` "gives form value ownership to the hidden input when the input is inside the popup" (renders `Autocomplete.Root`; engine mechanics only, §16 Q12).
- **CMB-STR-04** — MUST. `Combobox.Positioner` MUST be a descendant of `Combobox.Portal` (otherwise it throws "Base UI: <Combobox.Portal> is missing."). `Combobox.Popup` and `Combobox.Arrow` MUST be descendants of `Combobox.Positioner` (otherwise "Base UI: <Combobox.Popup> and <Combobox.Arrow> must be used within the <Combobox.Positioner> component"). `Combobox.Input` and `Combobox.List` may read the positioner context optionally (no throw).
  Source: `combobox/portal/ComboboxPortalContext.tsx:9`; `combobox/positioner/ComboboxPositionerContext.tsx:25`; `combobox/input/ComboboxInput.tsx:64`; `combobox/list/ComboboxList.tsx:31`. Test: [L2] `ComboboxPortalContext.test.tsx` "throws a descriptive error when used outside <Combobox.Portal>"; `ComboboxPositionerContext.test.tsx` "throws a descriptive error when a required consumer is outside the positioner".
- **CMB-STR-05** — MUST. Inline composition: when `inline` is set the List MAY be rendered without Portal/Positioner/Popup. A List without a Positioner ancestor registers its own element as the store's `positionerElement` (the floating element), and the popup reference used for focus/scroll logic resolves to the nearest ancestor `[role="dialog"]` of the list when inline. The dismiss interactions are disabled in inline mode (CMB-DIS-05) and the floating context reports `open: true` permanently (CMB-ARIA-02).
  Source: `combobox/list/ComboboxList.tsx:31,71`; `combobox/root/AriaCombobox.tsx:998-1003,1334,1395`. Test: [L2] `ComboboxRoot.test.tsx` "bubbles Escape key when rendered inline without Positioner/Popup", "keeps input value on Enter when inline and no item is highlighted", "keeps the input expanded when rendered inline without the `open` prop".
- **CMB-STR-06** — MUST. Closed-template rendering: when `Combobox.List` receives a function as `children`, it wraps that function in an implicit `Combobox.Collection`. `Combobox.Collection` maps `children(item, index)` over the nearest `Combobox.Group`'s `items` when it is inside a Group that has `items`, otherwise over the Root's filtered items. A Group without `items` nested in a Collection renders nothing for its Collection.
  Source: `combobox/list/ComboboxList.tsx:52-61`; `combobox/collection/ComboboxCollection.tsx:15-22`; `combobox/group/ComboboxGroup.tsx:19-56`. Test: [L2] `ComboboxCollection.test.tsx` "renders filtered items", "renders nothing when a nested group does not provide items".
- **CMB-STR-07** — MUST. Internal dismiss buttons: while the popup is open *and* the focus manager is modal (`!inputInsidePopup || modal`), the Input renders a visually hidden, non-native button (`<span>` with button semantics, `aria-label="Dismiss"`, no `tabindex` attribute) *before* the input element; the Popup renders the same kind of button *after* the popup element whenever the focus manager is modal. Pressing either closes the popup with reason `close-press`. They are registered as "inside" elements of the focus manager.
  Source: `combobox/utils/ComboboxInternalDismissButton.tsx:15-46`; `combobox/input/ComboboxInput.tsx:96,436-443`; `combobox/popup/ComboboxPopup.tsx:125,135-143`. Test: [L2] `ComboboxRoot.test.tsx` "does not render the start dismiss button while closed", "renders internal dismiss buttons before the input and after the popup", "renders an internal dismiss button for the input-outside-popup pattern" (L4 for the focus-manager clause).
- **CMB-STR-08** — MUST. Structural-only parts (render an element with attributes but no behavior): `Icon` (`span`, `aria-hidden`, default content "▼"), `Row` (`div`, `role="row"`), `Separator` (`div`, `role="presentation"`, `data-orientation`), `Arrow`, `Backdrop`, `GroupLabel`, `Status`. `Value` and `Collection` render no element.
  Source: `combobox/icon/ComboboxIcon.tsx:18-25`; `combobox/row/ComboboxRow.tsx:20-24`; `combobox/separator/ComboboxSeparator.tsx:30`; `utils/listbox-separator/ListboxSeparator.tsx:16-24`; `combobox/value/ComboboxValue.tsx:39`. Test: [L2] `ComboboxIcon.test.tsx` describeConformance; `ComboboxRoot.test.tsx` "exposes the orientation attribute rendered by Separator".
- **CMB-STR-09** — MUST. Chips nesting: `Chips` provides a composite list of chip elements; `Chip` registers into that list (it dereferences the Chips context unconditionally, so a Chip outside Chips is unsupported); `ChipRemove` reads its Chip's `index`. The Input reads the Chips context optionally: chip keyboard behaviors (CMB-KEY-08, CMB-KEY-09) exist only when a Chips part is mounted somewhere in the Root.
  Source: `combobox/chips/ComboboxChips.tsx:27-50`; `combobox/chip/ComboboxChip.tsx:29,36`; `combobox/chip-remove/ComboboxChipRemove.tsx:34`; `combobox/input/ComboboxInput.tsx:63,150-152`. Test: [L2] `ComboboxInput.test.tsx` "keeps focus on the input when navigating toward chips but none are rendered", "removes the last selected value when no chips are rendered".
- **CMB-STR-10** — MUST. `Combobox.Label` labels the Trigger only. In development, when an Input element is registered *and* the input is outside the popup, mounting a Label logs (console error, once per Input element change) "<Combobox.Label> labels <Combobox.Trigger> only. When <Combobox.Input> is the form control, use a native <label> or <Field.Label> instead." (followed by the React owner stack when available). The warning MUST NOT run in production. Port: withdrawn (ADR 02 Amendments).
  Source: `combobox/label/ComboboxLabel.tsx:40-53`. Test: [L2] `ComboboxLabel.test.tsx` "warns without relying on React.captureOwnerStack when labeling an external input", "does not run the development warning in production"; `ComboboxRoot.test.tsx` "Combobox.Label does not label Combobox.Input and warns when input is the form control".

Parts that may be omitted: without `Trigger` the Input is the only opener (click/typing/arrow keys); without `Clear` the Escape-while-closed key path still clears (CMB-KEY-05); without `Empty` the Escape key is allowed to propagate when the filtered list is empty (CMB-DIS-02); without `Chips` the Backspace-in-empty-input path still removes the last *selected value* (CMB-KEY-08); without `Portal`/`Positioner`/`Popup` the Root must be `inline`.

## 3. Public API

Props common to every element-rendering part — `className`, `style`, `render` — follow [rendering-and-composition](../architecture/rendering-and-composition.md) (RND-API-01…03) and are listed only where the part treats them specially.

### 3.1 `Combobox.Root`

**Default element:** none (renders no element). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no.

`Combobox.Root` is generic in `Value`, `Multiple extends boolean | undefined = false` and `Item = Value`. It forwards every prop to the internal engine, translating `multiple` → `selectionMode: multiple ? 'multiple' : 'single'`, `value` → `selectedValue`, `defaultValue` → `defaultSelectedValue`, `onValueChange` → `onSelectedValueChange`, `autoComplete` → `formAutoComplete`. The engine's `autoComplete` (ARIA autocomplete mode), `fillInputOnItemPress`, `submitOnItemClick`, `keepHighlight` and `selectionMode` props are *not* exposed on `Combobox.Root` (their engine defaults apply: `'list'`, `true`, `false`, `false`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Value \| null` (single) / `readonly Value[] \| null` (multiple) | — | Controlled selected value. | CMB-API-01 |
| `defaultValue` | same as `value` | `null` (single) / `[]` (multiple) | Initial uncontrolled selected value. | CMB-API-02 |
| `onValueChange` | `(value, eventDetails: ChangeEventDetails) => void` | — | Called before the selected value changes. | CMB-API-03 |
| `multiple` | `boolean` | `false` | Multiple selection. | CMB-API-04 |
| `open` | `boolean` | — | Controlled popup open state. | CMB-API-05 |
| `defaultOpen` | `boolean` | `false` | Initial uncontrolled open state. | CMB-API-06 |
| `onOpenChange` | `(open, eventDetails: OpenChangeEventDetails) => void` | — | Called before the open state changes. | CMB-API-07 |
| `onOpenChangeComplete` | `(open: boolean) => void` | — | Called after open/close animations finish. | CMB-API-08 |
| `inputValue` | `string` (input `value` type) | — | Controlled text of the Input. | CMB-API-09 |
| `defaultInputValue` | `string` | derived | Initial uncontrolled Input text. | CMB-API-10 |
| `onInputValueChange` | `(inputValue: string, eventDetails: ChangeEventDetails) => void` | — | Called before the Input text changes. | CMB-API-11 |
| `items` | `readonly any[] \| readonly Group<any>[] \| ComboboxItemCollection` | — | Source items (flat, grouped, or a `createItems()` collection). | CMB-API-12 |
| `filteredItems` | `readonly Item[] \| readonly Group<Item>[]` | — | Externally filtered items; bypasses internal filtering. | CMB-API-13 |
| `filter` | `null \| (item, query, itemToString?) => boolean` | collator `contains` | Item matcher; `null` disables filtering. | CMB-API-14 |
| `locale` | `Intl.LocalesArgument` | runtime locale | Collator locale for the default filter. | CMB-API-15 |
| `limit` | `number` | `-1` | Maximum number of items rendered from `items`. | CMB-API-16 |
| `autoHighlight` | `boolean` | `false` | Highlight the first match while typing. | CMB-API-17 |
| `highlightItemOnHover` | `boolean` | `true` | Pointer movement highlights items. | CMB-API-18 |
| `loopFocus` | `boolean` | `true` | Arrow navigation loops through the input at the list ends. | CMB-API-19 |
| `openOnInputClick` | `boolean` | `true` | Clicking the Input opens the popup. | CMB-API-20 |
| `grid` | `boolean` | `false` | Grid layout/navigation (`Row` parts). | CMB-API-21 |
| `virtualized` | `boolean` | `false` | Items are externally virtualized. | CMB-API-22 |
| `inline` | `boolean` | `false` | List rendered inline (no own popup). | CMB-API-23 |
| `modal` | `boolean` | `false` | Modal popup (scroll lock, outside pointer blocked). | CMB-API-24 |
| `name` | `string` | Field name | Form field name. | CMB-API-25 |
| `form` | `string` | — | Owning form id for the hidden input(s). | CMB-API-26 |
| `id` | `string` | generated | Root id; propagated to the control. | CMB-API-27 |
| `required` | `boolean` | `false` | Required for form submission. | CMB-API-28 |
| `readOnly` | `boolean` | `false` | Browsing allowed, no edits. | CMB-API-29 |
| `disabled` | `boolean` | `false` | Ignores user interaction. | CMB-API-30 |
| `autoComplete` | `string` | — | Browser autofill hint for the hidden input. | CMB-API-31 |
| `itemToStringLabel` | `(itemValue: Value) => string` | — | Label of a value (display). | CMB-API-32 |
| `itemToStringValue` | `(itemValue: Value) => string` | — | Serialization of a value (form). | CMB-API-33 |
| `isItemEqualToValue` | `(itemValue: Value, value: Value) => boolean` | `Object.is` | Value equality. | CMB-API-34 |
| `inputRef` | `React.Ref<HTMLInputElement>` | — | Ref to the *hidden* input. | CMB-API-35 |
| `actionsRef` | `React.RefObject<Actions \| null>` | — | `{ unmount, close }`. | CMB-API-36 |
| `onItemHighlighted` | `(value: Value \| undefined, eventDetails: HighlightEventDetails) => void` | — | Highlight change notifications. | CMB-API-37 |
| `children` | `ReactNode` | — | Parts. | — |

**Consumed (not forwarded to any element):** every prop (Root renders no element).

- **CMB-API-01** — MUST. `value` makes the selected value controlled: the engine reads it via the controlled-state utility registered under `{ name: 'Combobox', state: 'selectedValue' }` and never writes to it; every selection attempt still calls `onValueChange`. Passing `null` as a controlled value in `multiple` mode is normalized to an empty array for every read (`selectedValue` is treated as `[]`).
  Source: `combobox/root/AriaCombobox.tsx:294-300`; `combobox/root/ComboboxRoot.tsx:28,158`. Test: [L2] `ComboboxRoot.test.tsx` "should not auto-close popup when open state is controlled" (controlled flow), "normalizes a controlled null value when selecting in multiple mode", "normalizes a controlled null value while closed in multiple mode", "keeps the highlight on an item a controlled value refused to select".
- **CMB-API-02** — MUST. `defaultValue` seeds the uncontrolled selected value and is read once. When omitted the initial value is `null` in single mode and the shared empty array in multiple mode.
  Source: `combobox/root/AriaCombobox.tsx:116,294-300`. Test: [L2] `ComboboxRoot.test.tsx` "derives input from defaultValue on first mount when unspecified", "multiple mode initial input remains empty"; `ComboboxTrigger.test.tsx` "does not have data-placeholder when multiple mode has a default value".
- **CMB-API-03** — MUST. `onValueChange(value, eventDetails)` is called synchronously before the stored value changes with the *next* value (an array in multiple mode, `Value | null` in single mode). Calling `eventDetails.cancel()` leaves the value unchanged and skips every follow-up (input fill, close, clear).
  Source: `combobox/root/AriaCombobox.tsx:854-877`. Test: [L2] `ComboboxRoot.test.tsx` "\"single\" keeps the popup open when selection is canceled", "does not clear popup input after canceled multiple value changes", "keeps the active item when the post-selection input clear is canceled".
- **CMB-API-04** — MUST. `multiple` (default `false`) selects the selection mode for the lifetime of the component: `true` → values are arrays, item presses toggle membership and do not close the popup, chips/hidden inputs per value; `false` → a single value, item press selects and closes. The `Multiple` type parameter changes the `value`/`defaultValue`/`onValueChange` types accordingly.
  Source: `combobox/root/ComboboxRoot.tsx:16,27,36-46,120,158-167`. Test: [L2] `ComboboxRoot.test.tsx` "\"single\" selects and closes, then reopens with selection focused", "should handle multiple selection", "multiple mode toggles selection and stays open" (`ComboboxItem.test.tsx`).
- **CMB-API-05** — MUST. `open` controls the popup; uncontrolled otherwise (`{ name: 'Combobox', state: 'open' }`). A controlled popup never changes on its own: item presses, Escape, outside presses and the `close` action only report through `onOpenChange`. A controlled reopen that interrupts a close (without `setOpen`) is detected via the open-changed handler (CMB-STA-21).
  Source: `combobox/root/AriaCombobox.tsx:330-337,1273-1278`. Test: [L2] `ComboboxRoot.test.tsx` "controls the open state", "should not auto-close popup when open state is controlled", "releases filtering when a controlled popup ignores a close request"; `popupConformanceTests.tsx` "opens the popup with the `open` prop", "is ignored when `open={true}` is passed, so Escape cannot close" (L4 for the Escape and outside-press clauses and the `popupConformanceTests` citations).
- **CMB-API-06** — MUST. `defaultOpen` (default `false`) seeds the uncontrolled open state and is ignored when `open` is passed.
  Source: `combobox/root/AriaCombobox.tsx:111,330-337`. Test: [L4] `popupConformanceTests.tsx` "opens initially and remains uncontrolled", "is ignored when `open={false}` is passed".
- **CMB-API-07** — MUST. `onOpenChange(open, eventDetails)` is called before every open-state change with `OpenChangeEventDetails` (§3.27, §7). It is not called when the requested state equals the current state (including `actions.close()` while closed).
  Source: `combobox/root/AriaCombobox.tsx:759-779`. Test: [L2] `ComboboxRoot.test.tsx` "fires when opening and closing", "does not call `onOpenChange` when the `close` action is called while closed"; `popupConformanceTests.tsx` "is called with the new open state and the change details" (L4 for the `popupConformanceTests` citations).
- **CMB-API-08** — MUST. `onOpenChangeComplete(open)` is called once the open transition of the Popup has finished (`true`) and once the close has fully completed, i.e. after exit animations and after any `preventUnmountOnClose` hold has been released (`false`). It is not called on mount while closed.
  Source: `combobox/popup/ComboboxPopup.tsx:64-72`; `combobox/root/AriaCombobox.tsx:952-992,1005-1015`. Test: [L4] `ComboboxRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once", "completes closing once when `unmount` is called twice in one batch"; `popupConformanceTests.tsx` "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation".
- **CMB-API-09** — MUST. `inputValue` controls the Input text (`{ name: 'Combobox', state: 'inputValue' }`); every attempted change is reported through `onInputValueChange` and the stored value is otherwise untouched. A controlled Input preserves the caret position when the value is replaced in the middle of the text.
  Source: `combobox/root/AriaCombobox.tsx:313-328`. Test: [L2] `ComboboxRoot.test.tsx` "inputValue overrides derivation when provided", "discards the request when the controlled input is cleared"; `ComboboxInput.test.tsx` "preserves caret position when controlled and inserting in the middle".
- **CMB-API-10** — MUST. `defaultInputValue` seeds the uncontrolled Input text; when omitted the initial text is derived from the initial selected value (CMB-STA-19).
  Source: `combobox/root/AriaCombobox.tsx:315-328`. Test: [L2] `ComboboxRoot.test.tsx` "defaultInputValue overrides derivation when provided", "derives input from defaultValue on first mount when unspecified".
- **CMB-API-11** — MUST. `onInputValueChange(inputValue, eventDetails)` is called before every Input text change, including programmatic ones (selection fill, clear on close, Clear button, Escape) with the reasons in §7. `cancel()` keeps the current text and skips the follow-ups of that write only.
  Source: `combobox/root/AriaCombobox.tsx:655-661`. Test: [L2] `ComboboxRoot.test.tsx` "reports a single input value change when the controlled value changes", "syncs again after a canceled sync to the same label", "does not run follow-up state changes after canceled input value changes".
- **CMB-API-12** — MUST. `items` supplies the source data: a flat array, an array of `{ value?, label?, items: [...] }` groups (a group is any entry whose `items` field is an array), or a `createItems()` collection (§3.27). When `items` is given the component derives labels from it for typeahead, input synchronization, autofill matching and `Combobox.Value`. Nullish entries are unsupported (they are skipped by the collection and rejected by the default filter). Passing a non-collection *object* (not an array) throws (CMB-EDGE-01).
  Source: `combobox/root/AriaCombobox.tsx:146-185,216-229,363-439`; `internals/resolveValueLabel.tsx:22-40`. Test: [L2] `ComboboxRoot.test.tsx` "derives input from defaultValue on first mount with items prop", "limits the number of items displayed when groups are used"; `createItems.test.tsx` "treats a non-array items field as item data rather than as a group", "treats an array-valued items field as a group when its declared type is unknown".
- **CMB-API-13** — MUST. `filteredItems` replaces the internal filter result: the list renders exactly these items (flat or grouped like `items`) — `limit` is *not* applied to them — and the single-mode "show everything when the query mirrors the selection" bypass is suppressed. When `items` is omitted, `filteredItems` alone drives rendering and label derivation. With a `createItems()` collection the array contains *source* items, which are projected into the derived-value domain.
  Source: `combobox/root/AriaCombobox.tsx:352-356,363-439`. Test: [L2] `ComboboxRoot.test.tsx` "uses filteredItems when items prop is omitted", "resets external filteredItems when reopening after a selection", "highlights the externally filtered item order when filtering reorders items"; `createItems.test.tsx` "projects externally filtered source items into the derived value domain".
- **CMB-API-14** — MUST. `filter(item, query, itemToString?)` decides whether a source item matches the current query; it receives the source item (the collection's item for `createItems()`, else the item itself) and the resolved `itemToStringLabel`-based stringifier. Passing `null` disables filtering (every item matches). The default (`filter` omitted) is, in every mode, the plain collator item matcher: `false` for a nullish item, otherwise the shared collator `contains` (`UTL-I18N-08..13`) with the Root `locale` and the resolved stringifier. The single-selection variant of `Combobox.useFilter` (CMB-API-65) is never used by the Root; the single-mode "show every item while the input mirrors the selection" behavior comes from the filtering bypass (CMB-STA-22), not from the filter.
  Source: `combobox/root/AriaCombobox.tsx:161,301-311,363-439`; `combobox/root/utils/index.ts:23-34`. Test: [L2] `ComboboxRoot.test.tsx` "should show all items when query is empty with enhanced filter", "uses custom filter to narrow results", "resets filtered results after selecting when using a custom search stringifier"; `createItems.test.tsx` "passes source items to a custom root filter", "lets a custom root filter match source fields that are neither the value nor the label".
- **CMB-API-15** — MUST. `locale` is passed to the collator of the default filter; it has no effect on a custom `filter`.
  Source: `combobox/root/AriaCombobox.tsx:301-311`. Test: [L2] `createItems.test.tsx` "uses the root locale for filtering".
- **CMB-API-16** — MUST. `limit` (default `-1`, meaning unlimited) caps the number of *rendered* items derived from `items` (it never truncates `filteredItems`): filtering stops once `limit` matches have been collected, the cap applies across groups in source order, groups emptied by the cap are dropped, `0` renders nothing, and a selected item outside the rendered window has no `selectedIndex`. `limit` has no effect when `items` is not used.
  Source: `combobox/root/AriaCombobox.tsx:363-439`. Test: [L2] `ComboboxRoot.test.tsx` "limits the number of items displayed when no groups are used", "limits the number of items displayed when groups are used", "respects limit when filtering items", "stops filtering grouped items once the limit is reached", "shows all items when limit is -1 (default)", "handles limit of 0 gracefully", "preserves order of items when applying limit across groups", "does not limit items when not using items prop", "updates displayed items when limit changes", "keeps the selected index unset when the selected item is outside the rendered limit".
- **CMB-API-17** — MUST. `autoHighlight` (boolean, default `false`): when `true`, typing a non-blank query highlights the first filtered item (index 0) as soon as the filtered list is derived, keeps the highlight while the query changes, and makes <kbd>ArrowDown</kbd> continue from it. It never highlights on a plain open with an empty query (the engine's `'always'` mode is not exposed on `Combobox.Root`).
  Source: `combobox/root/ComboboxRoot.tsx:85-89`; `combobox/root/AriaCombobox.tsx:712-719,1080-1242`. Test: [L2] `ComboboxRoot.test.tsx` "does not auto-highlight on initial open when no selection", "highlights the first matching item after typing (single mode)", "highlights the first matching item after typing (multiple mode)", "highlights the first matching item for a static list without the items prop", "retains highlight when query is cleared back to empty" (L4 for the ArrowDown clause).
- **CMB-API-18** — MUST. `highlightItemOnHover` (default `true`) is passed as the list-navigation `focusItemOnHover` option: pointer movement over an item highlights it; when `false` pointer movement never changes the highlight (CSS `:hover` and `data-highlighted` become distinguishable).
  Source: `combobox/root/AriaCombobox.tsx:1427`. Test: [L4] `ComboboxRoot.test.tsx` "highlights an item on mouse move by default", "does not highlight items from mouse movement when disabled".
- **CMB-API-19** — MUST. `loopFocus` (default `true`) is passed as the list-navigation `loopFocus`; `allowEscape` is `loopFocus && !autoHighlight`. With the default, <kbd>ArrowDown</kbd> on the last item returns the highlight to the input (no item) and a further <kbd>ArrowDown</kbd> highlights the first; with `false` the highlight stays on the end item.
  Source: `combobox/root/AriaCombobox.tsx:1423-1424`. Test: [L4] `ComboboxRoot.test.tsx` "loops focus from last to first item with ArrowDown by default", "loops focus from first to last item with ArrowUp by default", "does not loop focus from last to first with ArrowDown when loopFocus={false}", "does not loop focus from first to last with ArrowUp when loopFocus={false}".
- **CMB-API-20** — MUST. `openOnInputClick` (default `true`) enables the input-press opener (CMB-PTR-01) and the press handlers of InputGroup/Chips. When `false`, an input press only focuses the input; typing still opens; clearing the input text (outside popup) closes the popup with reason `input-clear`.
  Source: `combobox/root/AriaCombobox.tsx:1384-1392`; `combobox/input/ComboboxInput.tsx:270-275,304-306`; `combobox/utils/handleInputPress.ts:35-37`. Test: [L4] `ComboboxRoot.test.tsx` "keeps the popup open on a second input click by default", "does not open on input click when false, but opens on typing"; `ComboboxInputGroup.test.tsx` "focuses the input without opening when openOnInputClick is false"; `ComboboxInput.test.tsx` "closes on an empty composition update when input clicks do not open the popup".
- **CMB-API-21** — MUST. `grid` (default `false`) switches the List to `role="grid"`, Items to `role="gridcell"`, Groups to `role="rowgroup"`, `aria-haspopup="grid"`, and horizontal/vertical grid navigation inferred from the DOM rows (`Combobox.Row`), including uneven rows and RTL mirroring (CMB-KEY-15).
  Source: `combobox/root/AriaCombobox.tsx:1342,1429-1432`; `floating-ui-react/utils/gridNavigation.ts`. Test: [L2] `ComboboxRoot.test.tsx` "sets grid roles when grid is enabled and rows are used", "arrow keys navigate across rows and columns in grid mode", "supports uneven rows navigation", "supports uneven rows navigation within groups", "mirrors horizontal grid navigation in RTL mode", "renders groups with the rowgroup role" (L4 for the grid-navigation clause).
- **CMB-API-22** — MUST. `virtualized` (default `false`) declares that the consumer windows the items: the List does not wrap its children in a composite registry, each Item resolves its index from an explicit `index` prop or, failing that, from the position of its value in the flat filtered values; the scroll-to-top-on-query-change behavior is skipped (the consumer owns the scroller); the typeahead/labels registry is derived from `items`. The flag must be stable for an Item's lifetime (changing it remounts the Item).
  Source: `combobox/item/ComboboxItem.tsx:73,224-286`; `combobox/list/ComboboxList.tsx:105-107`; `combobox/root/AriaCombobox.tsx:694-695`. Test: [L2] `ComboboxItem.test.tsx` "selects the highlighted filtered item with the keyboard after filtering" (×2), "selects the clicked filtered item after filtering", "registers a rendered window at its filtered indexes", "does not register an item missing from the filtered values"; `ComboboxRoot.test.tsx` "restores the selected item through an externally virtualized list"; `createItems.test.tsx` "resolves virtualized collection items to their derived value".
- **CMB-API-23** — MUST. `inline` (default `false`): the list is considered permanently visible (`expanded` ARIA, floating context open), dismissal is disabled, the initial highlight is seeded from the selection on mount (when `open` and items exist), the highlight is saved on blur and restored on refocus within the same open cycle, and Enter without a highlight keeps the input text. The docs instruct consumers to pass `open` unconditionally or bind `open`/`onOpenChange` to a surrounding Dialog so transient state resets when the dialog closes.
  Source: `combobox/root/AriaCombobox.tsx:1919,454-549,1334,1345,1395`; `combobox/input/ComboboxInput.tsx:104-109,188-216,408-411`. Test: [L2] `ComboboxRoot.test.tsx` "sets aria-expanded on the input when rendered inline and open", "seeds the initial highlight for a matched inline value", "does not seed an initial highlight for an unmatched inline value", "keeps filtering responsive after selection when inline and open is controlled", "restores highlight when the input regains focus"; `store.test.tsx` "publishes an inline transition in a single store transaction" (renders `Autocomplete.Root`; engine mechanics only, §16 Q12) (L4 for the blur/refocus and Enter clauses).
- **CMB-API-24** — MUST. `modal` (default `false`): when `true` and the popup is mounted, the Positioner renders an internal backdrop (cut out around the InputGroup, else Input, else Trigger) that blocks outside pointer interaction, the anchored scroll lock engages while open (touch-opened popups lock only when they span nearly the full viewport width), and the focus manager is modal even in the input-inside-popup layout.
  Source: `combobox/positioner/ComboboxPositioner.tsx:91-96,125-130`; `combobox/popup/ComboboxPopup.tsx:125`; `utils/useAnchoredPopupScrollLock.ts`. Test: [L4] `ComboboxRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width", "does not apply scroll lock when a touch-opened popup is narrower than the viewport"; `ComboboxPositioner.test.tsx` "should not lock body scroll when controlled value={[]} triggers a re-render".
- **CMB-API-25** — MUST. `name` identifies the field: it is the hidden input's `name` in single mode; in multiple mode the per-value hidden inputs carry it and the main hidden input has no name. Defaults to the surrounding `Field.Root`'s name.
  Source: `combobox/root/AriaCombobox.tsx:1589-1610,1693`; `internals/field-register-control/useRegisterFieldControl.ts`. Test: [L2] `ComboboxRoot.test.tsx` "should receive name prop from Field.Root", "should create multiple hidden inputs for form submission".
- **CMB-API-26** — MUST. `form` is forwarded as the `form` attribute of the hidden input(s) so the combobox can submit to a form it is not nested in.
  Source: `combobox/root/AriaCombobox.tsx:1603,1692`. Test: [L2] `ComboboxRoot.test.tsx` "submits to an external form when `form` is provided", "submits multiple values to an external form when `form` is provided".
- **CMB-API-27** — MUST. `id` (generated when omitted) is the Root id: it is applied to the Input when the input is outside the popup and to the Trigger when the input is inside the popup; it prefixes item ids (`${id}-${index}`), the default popup id (`${id}-popup`), the default label id and the hidden input id (`${id}-hidden-input`, only when the hidden input has no name).
  Source: `combobox/root/AriaCombobox.tsx:1691`; `combobox/input/ComboboxInput.tsx:97`; `combobox/trigger/ComboboxTrigger.tsx:89-90`; `combobox/item/ComboboxItem.tsx:81`; `combobox/root/utils/index.ts:13-15`. Test: [L2] `ComboboxRoot.test.tsx` "sets the id on the input when it is outside the popup", "sets the id on the trigger when the input is inside the popup"; `index.test.ts` "derives an id only when the root has an id".
- **CMB-API-28** — MUST. `required` (default `false`) sets `aria-required` on the control (Input, or Trigger when the input is inside the popup) and `required` on the hidden input — except that in multiple mode the hidden input is not `required` once at least one value is selected. No part emits `data-required` (CMB-DOM-02).
  Source: `combobox/root/AriaCombobox.tsx:1588,1696`; `combobox/input/ComboboxInput.tsx:180`; `combobox/trigger/ComboboxTrigger.tsx:162`. Test: [L2] `ComboboxInput.test.tsx` "sets aria-required attribute when required"; `ComboboxRoot.test.tsx` "does not mark the hidden input as required when selection exists in multiple mode", "keeps the hidden input required when no selection exists in multiple mode".
- **CMB-API-29** — MUST. `readOnly` (default `false`): the popup can still be opened (input press, trigger press, arrow keys, Enter/Space on the trigger) and browsed with the arrow keys, but typing does not change the input, items cannot be selected or toggled, chips cannot be removed, the Clear button does nothing, closed-trigger typeahead is disabled, `aria-autocomplete` becomes `"none"`, and Enter on a highlighted item is swallowed (no submit, no commit).
  Source: `combobox/root/AriaCombobox.tsx:113,1363`; `combobox/input/ComboboxInput.tsx:249,323-329`; `combobox/trigger/ComboboxTrigger.tsx:112`; `combobox/item/ComboboxItem.tsx:186,196`; `combobox/clear/ComboboxClear.tsx:102`; `combobox/chip-remove/ComboboxChipRemove.tsx:45`. Test: [L4] `ComboboxRoot.test.tsx` "should render readOnly state on the input", "should expose aria-autocomplete=\"none\" when readOnly", "should open the popup from the input and the trigger when readOnly", "should browse the items with the arrow keys when readOnly", "should not change the input value when typing while readOnly", "should prevent value changes when readOnly with items", "should not submit the form with Enter on a highlighted item when readOnly", "should not commit a value with Enter on a highlighted item when readOnly", "should report arrow-key highlights as keyboard-driven when readOnly".
- **CMB-API-30** — MUST. `disabled` (default `false`, OR-ed with the Field's disabled state) disables every interactive part (Input, Trigger, Clear, Items, Chips, ChipRemove), prevents opening by any means, prevents keyboard interaction, disables the hidden input(s) (so nothing is submitted), and removes `data-focused`.
  Source: `combobox/root/AriaCombobox.tsx:112,1385,1395,1417,1606,1695`; `combobox/input/ComboboxInput.tsx:90`; `combobox/trigger/ComboboxTrigger.tsx:82`. Test: [L2] `ComboboxRoot.test.tsx` "should render disabled state on all interactive components", "should not open popup when disabled", "should prevent keyboard interactions when disabled", "should set disabled attribute on hidden input", "does not submit multiple values when disabled", "should receive disabled prop from Field.Root" (L4 for the keyboard clause).
- **CMB-API-31** — MUST. `autoComplete` is forwarded verbatim as the hidden input's `autocomplete` attribute (browser autofill hint). The visible Input always has `autocomplete="off"` (CMB-DOM-01).
  Source: `combobox/root/ComboboxRoot.tsx:31,84`; `combobox/root/AriaCombobox.tsx:1694`. Test: [L2] `ComboboxRoot.test.tsx` "should pass autoComplete to the hidden input".
- **CMB-API-32** — MUST. `itemToStringLabel(itemValue)` converts a value to its display string (input text after selection, `Combobox.Value`, typeahead labels, default filter text). Resolution order for a value: the `createItems()` collection's `getLabel` (when the value resolves in the collection) → `itemToStringLabel` → an object's `label` property → `String(value)`; the closed-trigger typeahead and `Combobox.Value` additionally look the value up in `items` by equality.
  Source: `combobox/root/AriaCombobox.tsx:216-229,342`; `internals/resolveValueLabel.tsx:68-100`. Test: [L2] `ComboboxRoot.test.tsx` "uses itemToStringLabel for input value synchronization", "shows the label for a controlled object value not in items"; `createItems.test.tsx` "keeps filtering on the collection label when itemToStringLabel is provided", "falls back to itemToStringLabel only for values the collection cannot resolve"; `ComboboxValue.test.tsx` "uses custom itemToStringLabel function".
- **CMB-API-33** — MUST. `itemToStringValue(itemValue)` converts a value to its form string. Default: an object's `value` property when present, else `String(value)`. It is used for the hidden input values, autofill matching and `validate`'s raw-vs-serialized split (validation receives the raw value).
  Source: `combobox/root/AriaCombobox.tsx:1581-1586,1598,1640`; `internals/resolveValueLabel.tsx:83-91`. Test: [L2] `ComboboxRoot.test.tsx` "uses itemToStringValue for form submission", "uses itemToStringValue for multiple selection form submission", "serializes {value,label} objects using their value field", "falls back to itemToStringValue when object lacks value", "passes raw value to validate when itemToStringValue is provided".
- **CMB-API-34** — MUST. `isItemEqualToValue(itemValue, value)` (default `Object.is`) is used for every value comparison: selected state of items, toggling/removal in multiple mode (item first, selected entry second), selected-index resolution, chip removal highlight clearing, dirty detection, and `createItems()` lookup. It is never called with `null`/`undefined` selections.
  Source: `combobox/root/AriaCombobox.tsx:139`; `internals/itemEquality.ts`. Test: [L2] `ComboboxRoot.test.tsx` "matches object values using the provided comparator", "properly deselects object values using the provided comparator", "passes item as the first comparator argument in multiple mode", "does not call comparator with null when clearing the value", "does not call comparator with undefined when items load asynchronously after opening"; `createItems.test.tsx` "resolves a value that only a custom comparer matches from the collection".
- **CMB-API-35** — MUST. `inputRef` is attached to the *hidden* form input, not to `Combobox.Input`.
  Source: `combobox/root/AriaCombobox.tsx:1699` (`hiddenInputRef` merges `inputRef`). Test: none (type/outline only; see §16).
- **CMB-API-36** — MUST. `actionsRef.current` is `{ unmount, close }`: `close()` requests a close with reason `imperative-action` (through `onOpenChange`, cancelable); `unmount()` ends a closing phase that was held with `preventUnmountOnClose()`; `unmount()` while open is ignored, but a later close still unmounts. Both may be called in the same batch.
  Source: `combobox/root/AriaCombobox.tsx:1017-1024,952-992`. Test: [L2] `ComboboxRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out", "ignores `unmount` while the popup is open", "unmounts when `close` and `unmount` are called in one batch", "still unmounts on a later close after `unmount` was called while open", "still unmounts on a later close after `unmount` and a reopen in one batch", "exposes an unmount action that is ignored while open".
- **CMB-API-37** — MUST. `onItemHighlighted(value | undefined, { reason: 'keyboard' | 'pointer' | 'none', event, index })` fires whenever the highlighted item changes (`undefined` when the highlight is cleared), with `index` the item's list index (`-1` when none) — see CMB-EVT-05.
  Source: `combobox/root/AriaCombobox.tsx:606-653,2012-2017`. Test: [L4] `ComboboxRoot.test.tsx` "fires on keyboard navigation", "passes native mouse and pointer events for pointer highlights", "reports a pointer highlight after keyboard navigation", "reports no keyboard reason when typing clears the highlight", "reports no keyboard reason when the clear button clears the highlight".

#### State object

`Combobox.Root.State` is an empty object; Root renders no element and exposes no state to `className`/`style`/`render`.
Source: `combobox/root/AriaCombobox.tsx:1964`; `combobox/root/ComboboxRoot.tsx:170`.

#### Data attributes

None (no element).

#### CSS variables

None.

### 3.2 `Combobox.Label`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | `${rootId}-label` | Label id (becomes the control's `aria-labelledby`). | CMB-ARIA-16 |
| native `div` props | | | Forwarded; `onClick`/`onMouseDown` are merged with the label's focus behavior (FFI). | — |

**Consumed:** `render`, `className`, `style`.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled`, `valid`, `touched`, `dirty`, `filled`, `focused` | Field state | Copied from the surrounding `Field.Root` state (defaults when none). |

- **CMB-STA-01** — MUST. The Label's state object is the surrounding Field root state verbatim (FFI `FieldRoot.State`); outside a Field it is the default field state.
  Source: `combobox/label/ComboboxLabel.tsx:29,65-70`. Test: [L2] `ComboboxLabel.test.tsx` describeConformance.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-valid` / `data-invalid` | field validity known | CMB-DOM-17 | C# |
| `data-disabled`, `data-touched`, `data-dirty`, `data-filled`, `data-focused` | default boolean mapping of the field state | CMB-DOM-17 | C# |

#### CSS variables

None.

### 3.3 `Combobox.InputGroup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `onMouseDown` | native | — | Merged before the built-in press handler; `preventBaseUIHandler()` skips it. | CMB-PTR-09 |
| native `div` props | | | Forwarded (`role` can be overridden). | CMB-ARIA-12 |

**Consumed:** `render`, `className`, `style`.

#### State object

| Field | Type | Meaning |
|---|---|---|
| field state | FFI | `disabled`, `valid`, `touched`, `dirty`, `filled`, `focused` from `Field.Root` |
| `open` | `boolean` | popup open |
| `disabled` | `boolean` | Root disabled |
| `readOnly` | `boolean` | Root readOnly |
| `popupSide` | `Side \| null` | side of the mounted positioner, else `null` |
| `listEmpty` | `boolean` | filtered list is empty |
| `placeholder` | `boolean` | no selected value |

- **CMB-STA-02** — MUST. The InputGroup state is `{ ...fieldState, open, disabled, readOnly, popupSide, listEmpty, placeholder }` where `disabled` is the Root's `disabled` (not the Field's), `popupSide` is `null` unless the popup is mounted and a Positioner element exists, `listEmpty` is `filteredItems.length === 0`, and `placeholder` is `!hasSelectedValue`.
  Source: `combobox/input-group/ComboboxInputGroup.tsx:27-49`; `combobox/utils/parts.ts:10-23`. Test: [L2] `ComboboxInputGroup.test.tsx` describeConformance; `ComboboxRoot.test.tsx` "should render disabled state on all interactive components".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-popup-open`, `data-pressed` | popup open | CMB-DOM-03 | C# |
| `data-disabled`, `data-readonly` | Root disabled / readOnly | CMB-DOM-03 | C# |
| `data-popup-side="<side>"` | popup mounted | CMB-DOM-03 | C# |
| `data-list-empty` | filtered list empty | CMB-DOM-03 | C# |
| `data-placeholder` | no selection | CMB-DOM-03 | C# |
| `data-valid`/`data-invalid`, `data-touched`, `data-dirty`, `data-filled`, `data-focused` | field state | CMB-DOM-03 | C# |

#### CSS variables

None.

### 3.4 `Combobox.Chips`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `onMouseDown` | native | — | Merged before the built-in press handler. | CMB-PTR-09 |
| native `div` props | | | Forwarded. | — |

**Consumed:** `render`, `className`, `style`.

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.5 `Combobox.Chip`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `onKeyDown`, `onMouseDown` | native | — | Merged with the chip behaviors (CMB-KEY-10, CMB-PTR-10). | — |
| native `div` props | | | Forwarded (`tabIndex` can be overridden). | CMB-DOM-11 |

**Consumed:** `render`, `className`, `style`.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Root disabled |

- **CMB-STA-03** — MUST. The Chip state is `{ disabled }` where `disabled` is the Root's disabled flag.
  Source: `combobox/chip/ComboboxChip.tsx:32,100-102,142-147`. Test: [L2] `ComboboxChip.test.tsx` "should render aria-disabled attribute when disabled".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-disabled` | Root disabled | CMB-DOM-11 | C# |

#### CSS variables

None.

### 3.6 `Combobox.ChipRemove`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Whether the rendered element is a native button (UBT). | CMB-API-53 |
| `disabled` | `boolean` | `false` | Local disabled, OR-ed with the Root's. | CMB-API-54 |
| `onClick`, `onKeyDown`, `onMouseDown` | native | — | Merged with the removal behavior. | — |

**Consumed:** `render`, `className`, `style`, `nativeButton`, `disabled`.

- **CMB-API-53** — MUST. `nativeButton` (default `true`) is passed to the shared button behavior (UBT-API-03/04): a native `<button>` gets `type="button"`, a non-native element gets `role="button"` and keyboard activation emulation.
  Source: `combobox/chip-remove/ComboboxChipRemove.tsx:28,43-47`. Test: [L2] `ComboboxChipRemove.test.tsx` describeConformance (`button: true`), "ignores click and keyboard activation on a non-native button: %o".
- **CMB-API-54** — MUST. `disabled` (default `false`) is OR-ed with the Root's `disabled`; the button behavior additionally treats `readOnly` as disabled while keeping the element focusable (`focusableWhenDisabled`).
  Source: `combobox/chip-remove/ComboboxChipRemove.tsx:41,43-47`. Test: [L2] `ComboboxChipRemove.test.tsx` "should render disabled attribute when disabled", "should not remove chip when disabled", "should be focusable but not functional when readOnly".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Root disabled OR local disabled |

- **CMB-STA-04** — MUST. The ChipRemove state is `{ disabled }`.
  Source: `combobox/chip-remove/ComboboxChipRemove.tsx:49-51`. Test: [L2] `ComboboxChipRemove.test.tsx` "should render disabled attribute when disabled".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-disabled` | disabled | CMB-DOM-11 | C# |

#### CSS variables

None.

### 3.7 `Combobox.Input`

**Default element:** `<input>`. **Accepts `render` element substitution:** yes (a `<textarea>` or other editable element is supported; see CMB-ARIA-05). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Local disabled, OR-ed with Field and Root. | CMB-API-38 |
| `id` | `string` | Root id (outside popup) / generated (inside) | Element id. | CMB-API-39 |
| `value`/`defaultValue` | — | — | Not consumer-settable: the text is the Root's input value. | CMB-API-09 |
| native `input` props | | | Forwarded; `onFocus`/`onBlur`/`onChange`/`onKeyDown`/`onCompositionStart`/`onCompositionEnd` merge with the built-in handlers. | — |

**Consumed:** `render`, `className`, `style`, `disabled`, `id`.

- **CMB-API-38** — MUST. `Combobox.Input`'s `disabled` (default `false`) is OR-ed with the Field's and the Root's disabled flags; a locally disabled Input does not open the popup and renders `disabled`/`data-disabled`.
  Source: `combobox/input/ComboboxInput.tsx:49,90`. Test: [L2] `ComboboxInput.test.tsx` "should render aria-disabled attribute when disabled", "should inherit disabled state from ComboboxRoot", "should inherit disabled state from Field.Root", "should not open popup when disabled", "should prioritize local disabled over root disabled".
- **CMB-API-39** — MUST. The Input's `id` is the consumer's `id`, else the Root id when the input is outside the popup, else a generated id. It also registers as the Field's control id (so `Field.Label` targets it) only when the input is outside the popup.
  Source: `combobox/input/ComboboxInput.tsx:97,187`. Test: [L2] `ComboboxRoot.test.tsx` "sets the id on the input when it is outside the popup", "Field.Label".

#### State object

| Field | Type | Meaning |
|---|---|---|
| field state | FFI | `valid`, `touched`, `dirty`, `filled`, `focused` (defaults when the input is inside the popup) |
| `open` | `boolean` | popup open |
| `disabled` | `boolean` | resolved disabled |
| `readOnly` | `boolean` | Root readOnly |
| `popupSide` | `Side \| null` | side of the mounted positioner |
| `listEmpty` | `boolean` | filtered list empty |

- **CMB-STA-05** — MUST. The Input state is `{ ...fieldState, open, disabled, readOnly, popupSide, listEmpty }`. When the input is rendered inside the popup (has a Positioner ancestor) the field portion is the default field state (`valid: null`, all flags `false`) regardless of the surrounding Field, and the Input is rendered under a *default* Field context so field attributes never reach it.
  Source: `combobox/trigger/ComboboxTrigger.tsx:134-142,261`; `combobox/trigger/ComboboxTriggerDataAttributes.ts:24-27`; `combobox/input/ComboboxInput.tsx:98,140-147,428-434`; `internals/field-constants/constants.ts:18-27`. Test: [L2] `ComboboxRoot.test.tsx` "does not apply validation ARIA attributes to input inside popup"; `ComboboxInput.test.tsx` describeConformance.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-popup-open`, `data-pressed` | popup open | CMB-DOM-02 | C# |
| `data-disabled`, `data-readonly` | respective flags (`data-required` is declared but never emitted) | CMB-DOM-02 | C# |
| `data-popup-side="<side>"` | popup mounted | CMB-DOM-02 | C# |
| `data-list-empty` | filtered list empty | CMB-DOM-02 | C# |
| `data-valid`/`data-invalid`, `data-touched`, `data-dirty`, `data-filled`, `data-focused` | field state (outside popup only) | CMB-DOM-02 | C# |

#### CSS variables

None.

### 3.8 `Combobox.Clear`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Native button semantics (UBT). | CMB-API-40 |
| `disabled` | `boolean` | `false` | Local disabled, OR-ed with Field and Root. | CMB-API-41 |
| `keepMounted` | `boolean` | `false` | Keep the element mounted while hidden. | CMB-API-42 |
| `children` | `ReactNode` | `"x"` | Content. | CMB-DOM-07 |
| `onClick`, `onMouseDown` | native | — | Merged with the clear behavior. | — |

**Consumed:** `render`, `className`, `style`, `nativeButton`, `disabled`, `keepMounted`.

- **CMB-API-40** — MUST. `nativeButton` (default `true`) configures the shared button behavior (UBT-API-03/04).
  Source: `combobox/clear/ComboboxClear.tsx:37,66-69`. Test: [L2] `ComboboxClear.test.tsx` describeConformance (`button: true`).
- **CMB-API-41** — MUST. `disabled` (default `false`) is OR-ed with the Field's and Root's disabled flags; a disabled Clear renders disabled and its click does nothing.
  Source: `combobox/clear/ComboboxClear.tsx:36,64,102`. Test: [L2] `ComboboxClear.test.tsx` "is disabled when root disabled and does nothing on click".
- **CMB-API-42** — MUST. `keepMounted` (default `false`): when `false` the Clear element is rendered only while `visible` or while its exit transition runs; when `true` it stays in the DOM and only its `data-visible`/transition attributes change.
  Source: `combobox/clear/ComboboxClear.tsx:38,71-88,138-144`. Test: [L2] `ComboboxClear.test.tsx` "does not render without a value by default", "triggers exit animation via data-ending-style before unmount".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | popup open |
| `disabled` | `boolean` | resolved disabled |
| `visible` | `boolean` | there is something to clear |
| `transitionStatus` | `TransitionStatus` | enter/exit status of the visibility |

- **CMB-STA-06** — MUST. The Clear state is `{ open, disabled, visible, transitionStatus }` where `visible` is `selectedValue != null` in single mode and "at least one selected value" in multiple mode, and `transitionStatus` is the shared transition status driven by `visible` (TRN). Server: relaxed, see §9.
  Source: `combobox/clear/ComboboxClear.tsx:55-62,71-78`. Test: [L2] `ComboboxClear.test.tsx` "renders in single mode when a value is selected", "renders and clears chips in multiple mode", "triggers enter animation via data-starting-style when becoming visible".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-visible` | visible | CMB-DOM-06 | C# |
| `data-disabled` | disabled | CMB-DOM-06 | C# |
| `data-popup-open` | popup open | CMB-DOM-06 | C# |
| `data-starting-style` / `data-ending-style` | transition status | CMB-DOM-06 | JS |

#### CSS variables

None.

### 3.9 `Combobox.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Native button semantics (UBT). | CMB-API-43 |
| `disabled` | `boolean` | `false` | Local disabled, OR-ed with Field and Root. | CMB-API-44 |
| `id` | `string` | Root id when the input is inside the popup | Element id; registered as the labelable control id in that layout. | CMB-API-45 |
| `onMouseDown`, `onKeyDown`, `onFocus`, `onBlur`, `onPointerDown`, `onPointerEnter` | native | — | Merged with the trigger behavior. | — |

**Consumed:** `render`, `className`, `style`, `nativeButton`, `disabled`, `id`.

- **CMB-API-43** — MUST. `nativeButton` (default `true`) configures the shared button behavior; a non-native disabled trigger ignores native keydown events.
  Source: `combobox/trigger/ComboboxTrigger.tsx:43,129-132`. Test: [L2] `ComboboxTrigger.test.tsx` describeConformance (`button: true`), "ignores native keydown events on a disabled non-native trigger".
- **CMB-API-44** — MUST. `disabled` (default `false`) is OR-ed with the Field's and Root's disabled flags; a disabled Trigger renders `disabled`/`aria-disabled` per UBT and never opens.
  Source: `combobox/trigger/ComboboxTrigger.tsx:44,82,193`. Test: [L2] `ComboboxTrigger.test.tsx` "should render aria-disabled attribute when disabled", "should inherit disabled state from ComboboxRoot", "should inherit disabled state from Field.Root", "should not open popup when disabled", "should prioritize local disabled over root disabled".
- **CMB-API-45** — MUST. When the input is inside the popup the Trigger's `id` defaults to the Root id and is registered as the labelable control id so `Field.Label`/`Combobox.Label` point at it; otherwise `id` is forwarded as given and nothing is registered.
  Source: `combobox/trigger/ComboboxTrigger.tsx:89-90`. Test: [L2] `ComboboxRoot.test.tsx` "sets the id on the trigger when the input is inside the popup", "Field.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id", "Combobox.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id".

#### State object

| Field | Type | Meaning |
|---|---|---|
| field state | FFI | `valid`, `touched`, `dirty`, `filled`, `focused` |
| `open` | `boolean` | popup open |
| `disabled` | `boolean` | resolved disabled |
| `readOnly` | `boolean` | Root readOnly |
| `popupSide` | `Side \| null` | side of the mounted positioner |
| `listEmpty` | `boolean` | filtered list empty |
| `placeholder` | `boolean` | no selected value |

- **CMB-STA-07** — MUST. The Trigger state is `{ ...fieldState, readOnly, open, disabled, popupSide, listEmpty, placeholder }` with `placeholder = !hasSelectedValue` (`hasSelectedValue` is `selectedValue != null` in single mode and a non-empty array in multiple mode).
  Source: `combobox/trigger/ComboboxTrigger.tsx:134-142`; `combobox/store.ts:141-150`. Test: [L2] `ComboboxTrigger.test.tsx` "has data-placeholder when no value is selected", "does not have data-placeholder when value is selected", "has data-placeholder when multiple mode has empty array", "does not have data-placeholder when multiple mode has a default value".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-popup-open`, `data-pressed` | popup open | CMB-DOM-04 | C# |
| `data-disabled`, `data-readonly` | respective flags (`data-required` is declared but never emitted; CMB-DOM-02) | CMB-DOM-04 | C# |
| `data-popup-side="<side>"` | popup mounted | CMB-DOM-04 | C# |
| `data-list-empty` | filtered list empty | CMB-DOM-04 | C# |
| `data-placeholder` | no selection | CMB-DOM-04 | C# |
| `data-valid`/`data-invalid`, `data-touched`, `data-dirty`, `data-filled`, `data-focused` | field state | CMB-DOM-04 | C# |

#### CSS variables

None.

### 3.10 `Combobox.Value`

**Default element:** none (renders a fragment). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode \| (selectedValue) => ReactNode` | — | Overrides the rendered content. | CMB-API-46 |
| `placeholder` | `ReactNode` | — | Shown when nothing is selected. | CMB-API-47 |

- **CMB-API-46** — MUST. `Combobox.Value` renders, in priority order: `children(selectedValue)` when `children` is a function (called with the raw selected value, `null` when none); `children` when it is any other non-nullish node; `placeholder` when nothing is selected *and* no `items` entry with a `null` value has a label; otherwise the resolved label(s): in multiple mode the labels of every selected value joined by `", "` (a fragment of nodes); in single mode the label of the selected value resolved through `items` (first match by equality, including grouped items), then the value's `label` property, then `itemToStringLabel`, then `String(value)`; a `null` value renders the `items` entry whose value is `null` (its label) or nothing. React nodes are allowed as labels. The result is never stale after `items` change.
  Source: `combobox/value/ComboboxValue.tsx:13-39`; `internals/resolveValueLabel.tsx:41-66,93-156`; `combobox/store.ts:152-154`. Test: [L2] `ComboboxValue.test.tsx` every test ("renders current selected value via function child" … "displays placeholder when multiple mode has empty array").
- **CMB-API-47** — MUST. `placeholder` is rendered only when no value is selected, `children` is nullish, and the `items` list has no `null`-valued entry with a label (such an entry wins over the placeholder).
  Source: `combobox/value/ComboboxValue.tsx:23-32`. Test: [L2] `ComboboxValue.test.tsx` "displays placeholder when no value is selected", "null item label in items takes precedence over placeholder", "uses placeholder when items have null value without label", "children prop takes precedence over placeholder".

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.11 `Combobox.Icon`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | `"▼"` | Content. | CMB-STR-08 |
| native `span` props | | | Forwarded (`aria-hidden` can be overridden). | CMB-ARIA-11 |

**Consumed:** `render`, `className`, `style`.

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.12 `Combobox.Portal`

**Default element:** the shared floating portal container (`<div>` appended to `container`, default `document.body`). **Accepts `render` element substitution:** yes (FloatingPortal). **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the portal subtree mounted while closed. | CMB-API-48 |
| `container` | `HTMLElement \| ShadowRoot \| RefObject \| null` | `document.body` | Portal target. | CMB-API-49 |

**Consumed:** `keepMounted`.

- **CMB-API-48** — MUST. `keepMounted` (default `false`) is published to the Positioner (which then keeps its element mounted-but-hidden while closed) and makes the Portal render its subtree even while the popup is not mounted (CMB-DOM-21).
  Source: `combobox/portal/ComboboxPortal.tsx:19-36`; `combobox/positioner/ComboboxPositioner.tsx:55,86`. Test: [L2] `ComboboxRoot.test.tsx` "registers selectedIndex before opening with a keepMounted portal (no items prop)", "does not navigate kept-mounted items while controlled closed"; `popupConformanceTests.tsx` "hides the kept-mounted popup once the exit animation finishes" (L4 for the `popupConformanceTests` citations).
- **CMB-API-49** — MUST. `container` is forwarded to the floating portal (FLT); the default is the document body.
  Source: `combobox/portal/ComboboxPortal.tsx:33,49-52`. Test: [L4] `ComboboxPortal.test.tsx` describeConformance.

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.13 `Combobox.Backdrop`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| native `div` props | | | Forwarded; the built-in `style` (`user-select: none`) merges with the consumer's. | CMB-DOM-14 |

**Consumed:** `render`, `className`, `style`.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | popup open |
| `transitionStatus` | `TransitionStatus` | popup transition status |

- **CMB-STA-08** — MUST. The Backdrop state is `{ open, transitionStatus }` read from the store. Server: relaxed, see §9.
  Source: `combobox/backdrop/ComboboxBackdrop.tsx:30-37`. Test: [L2] `ComboboxBackdrop.test.tsx` describeConformance.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open state | CMB-DOM-14 | C# |
| `data-starting-style` / `data-ending-style` | transition status | CMB-DOM-14 | JS |

#### CSS variables

None.

### 3.14 `Combobox.Positioner`

**Default element:** `<div>` (shared positioner element, FLT-DOM-01). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `anchor` | element, ref, virtual anchor or function | InputGroup → Input (input outside popup) / Trigger (input inside popup) | Positioning anchor. | CMB-API-50 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | CSS positioning strategy (FLT). | CMB-API-50 |
| `side` | `Side` | `'bottom'` | Preferred side. | CMB-API-50 |
| `align` | `Align` | `'center'` | Preferred alignment. | CMB-API-50 |
| `sideOffset`, `alignOffset` | `number \| OffsetFunction` | `0` | Offsets (FLT-API-12). | CMB-API-50 |
| `collisionBoundary` | `Boundary` | `'clipping-ancestors'` | Collision boundary (FLT-API-09). | CMB-API-50 |
| `collisionPadding` | `Padding` | `5` | Collision padding (FLT-API-08). | CMB-API-50 |
| `arrowPadding` | `number` | `5` | Arrow inset. | CMB-API-50 |
| `sticky` | `boolean` | `false` | Keep in view while scrolling. | CMB-API-50 |
| `disableAnchorTracking` | `boolean` | `false` | Skip anchor auto-update. | CMB-API-50 |
| `collisionAvoidance` | `CollisionAvoidance` | dropdown preset | Flip/shift strategy. | CMB-API-50 |
| native `div` props | | | Forwarded; `style` merges after the positioning styles. | — |

**Consumed:** `render`, `className`, `style`, and every positioning prop above.

- **CMB-API-50** — MUST. The Positioner passes its props to the shared anchor-positioning mechanism (FLT) with these Combobox-specific values: `anchor` defaults to the InputGroup element, else the Input element, when the input is outside the popup, and to the Trigger element when the input is inside the popup; `collisionBoundary` defaults to `'clipping-ancestors'`; `collisionAvoidance` defaults to the dropdown preset (`DROPDOWN_COLLISION_AVOIDANCE`); `lazyFlip` is always `true` (FLT-ANI-04); `keepMounted` is the Portal's `keepMounted`; `mounted` is the store's `mounted`; all other options use the FLT defaults (`side: 'bottom'`, `align: 'center'`, offsets `0`, `collisionPadding: 5`, `arrowPadding: 5`, `sticky: false`, `positionMethod: 'absolute'`, `disableAnchorTracking: false`).
  Source: `combobox/positioner/ComboboxPositioner.tsx:32-51,69-89`; `internals/useAnchorPositioning.ts:144-156`. Test: [L4] `ComboboxPositioner.test.tsx` "uses the input when input group is absent", "uses the input group when present", "keeps the popup on the preferred side when its capped height fits below tall content"; `positionerConformanceTests` (§13).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | popup open |
| `side` | `Side` | rendered side |
| `align` | `Align` | rendered alignment |
| `anchorHidden` | `boolean` | anchor clipped (FLT-POS-09) |
| `empty` | `boolean` | filtered list empty |

- **CMB-STA-09** — MUST. The Positioner state is `{ open, side, align, anchorHidden, empty }`; the rendered `side` is also published to the store (`popupSide`) post-commit pre-paint so Input/Trigger/InputGroup can expose `data-popup-side`. Server: relaxed, see §9.
  Source: `combobox/positioner/ComboboxPositioner.tsx:68,98-108`. Test: [L2] `ComboboxInput.test.tsx` "sets data-popup-side to the current popup side"; `ComboboxTrigger.test.tsx` "sets data-popup-side to the current popup side".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open state | CMB-DOM-13 | C# |
| `data-side="<side>"`, `data-align="<align>"` | always (rendered placement) | CMB-DOM-13 | C# → JS |
| `data-anchor-hidden` | anchor hidden | CMB-DOM-13 | JS |
| `data-empty` | filtered list empty | CMB-DOM-13 | C# |

#### CSS variables

| Variable | Value | Req | Writer |
|---|---|---|---|
| `--available-width`, `--available-height` | px available to the popup (FLT-DOM-05) | CMB-DOM-13 | C# → JS |
| `--anchor-width`, `--anchor-height` | px anchor size (FLT-DOM-06) | CMB-DOM-13 | C# → JS |
| `--transform-origin` | transform origin (FLT-DOM-07) | CMB-DOM-13 | C# → JS |

### 3.15 `Combobox.Popup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `initialFocus` | `boolean \| HTMLElement \| ref \| (interactionType) => boolean \| HTMLElement \| null \| void` | computed | Element focused when the popup opens. | CMB-API-51 |
| `finalFocus` | same shape (`closeType`) | computed | Element focused when the popup closes. | CMB-API-52 |
| `id` | `string` | `${rootId}-popup` when the input is inside the popup | Element id (referenced by the Trigger's `aria-controls`). | CMB-DOM-24 |
| `onFocus` | native | — | Merged with the focus-redirect handler (CMB-FOC-04). | — |

**Consumed:** `render`, `className`, `style`, `initialFocus`, `finalFocus`.

- **CMB-API-51** — MUST. `initialFocus` is forwarded to the focus manager (FLT-API-05). Its default is `false` (no initial focus; DOM focus stays on the Input) when the input is outside the popup, and, when the input is inside the popup, a function returning the Popup element for a `'touch'` open (so the virtual keyboard does not open) and the Input element otherwise.
  Source: `combobox/popup/ComboboxPopup.tsx:107-116,133`. Test: [L4] `ComboboxPopup.test.tsx` "focuses the popup instead of its input when opened by touch", "honors initialFocus={false}".
- **CMB-API-52** — MUST. `finalFocus` is forwarded as the focus manager's `returnFocus` (FLT-API-05). Its default is `undefined` (manager default: return to the anchor/trigger) when the input is inside the popup and `false` (no return focus) when it is outside.
  Source: `combobox/popup/ComboboxPopup.tsx:118-123,134`. Test: [L4] `ComboboxPopup.test.tsx` "returns focus to an explicitly provided element when the popup closes"; `ComboboxRoot.test.tsx` "selects with the keyboard, restores focus, and resets the query on reopen".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | popup open |
| `side` | `Side` | rendered side |
| `align` | `Align` | rendered alignment |
| `anchorHidden` | `boolean` | anchor clipped |
| `transitionStatus` | `TransitionStatus` | enter/exit status |
| `empty` | `boolean` | filtered list empty |

- **CMB-STA-10** — MUST. The Popup state is `{ open, side, align, anchorHidden, transitionStatus, empty }`. Server: relaxed, see §9.
  Source: `combobox/popup/ComboboxPopup.tsx:74-81`. Test: [L2] `ComboboxPopup.test.tsx` "exposes open state via data attributes mapping".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open state | CMB-DOM-12 | C# |
| `data-side`, `data-align` | rendered placement | CMB-DOM-12 | C# |
| `data-anchor-hidden` | anchor hidden | CMB-DOM-12 | C# |
| `data-starting-style` / `data-ending-style` | transition status | CMB-DOM-12 | JS |
| `data-empty` | filtered list empty | CMB-DOM-12 | C# |

#### CSS variables

None (the Positioner carries them).

### 3.16 `Combobox.Arrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| native `div` props | | | Forwarded; `style` merges after the arrow position styles; `aria-hidden` can be overridden. | CMB-DOM-15 |

**Consumed:** `render`, `className`, `style`.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | popup open |
| `side` | `Side` | rendered side |
| `align` | `Align` | rendered alignment |
| `uncentered` | `boolean` | arrow could not be centered on the anchor |

- **CMB-STA-11** — MUST. The Arrow state is `{ open, side, align, uncentered }` from the Positioner context (FLT-DOM-08). Server: relaxed, see §9.
  Source: `combobox/arrow/ComboboxArrow.tsx:23-32`. Test: [L2] `ComboboxArrow.test.tsx` describeConformance.

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open state | CMB-DOM-15 | C# |
| `data-side`, `data-align` | rendered placement | CMB-DOM-15 | C# → JS |
| `data-uncentered` | arrow uncentered | CMB-DOM-15 | JS |

#### CSS variables

None.

### 3.17 `Combobox.Status`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | — | Announced content. | CMB-ARIA-13 |
| native `div` props | | | Forwarded (`role`/`aria-live`/`aria-atomic` can be overridden). | CMB-ARIA-13 |

**Consumed:** `render`, `className`, `style`.

The docs require the Status element to stay mounted (no `display: none`, `hidden`, `aria-hidden` or conditional rendering of the part itself); change its children instead. It is typically rendered inside the Popup so it exists only while the popup is mounted.

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.18 `Combobox.Empty`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | — | Rendered only while the filtered list is empty. | CMB-DOM-23 |
| native `div` props | | | Forwarded. | CMB-ARIA-13 |

**Consumed:** `render`, `className`, `style`, `children`.

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.19 `Combobox.List`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode \| (item, index) => ReactNode` | — | Static children, or a function mapped over the filtered items. | CMB-API-55 |
| `onKeyDown` | native | — | Merged with the Enter handler (CMB-KEY-04). | — |
| native `div` props | | | Forwarded. | — |

**Consumed:** `render`, `className`, `style`, `children`.

- **CMB-API-55** — MUST. When `children` is a function the List renders an implicit `Combobox.Collection` with it (CMB-STR-06). Items render again only when their own parameters change, not on every active-index change (render count observed through bUnit, L2).
  Source: `combobox/list/ComboboxList.tsx:52-61,128-133`. Test: [L2] `ComboboxItem.test.tsx` "does not re-render still-mounted items when filtering keeps the same membership"; `ComboboxCollection.test.tsx` "renders filtered items".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `empty` | `boolean` | filtered list empty |

- **CMB-STA-12** — MUST. The List state is `{ empty }` where `empty` is `filteredItems.length === 0`.
  Source: `combobox/list/ComboboxList.tsx:42,63-65`. Test: [L2] `ComboboxInput.test.tsx` "toggles data-list-empty when the filtered list is empty" (same derived flag).

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-empty` | filtered list empty | CMB-DOM-08 | C# |

#### CSS variables

None.

### 3.20 `Combobox.Collection`

**Default element:** none (renders a fragment). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `(item, index) => ReactNode` | required | Mapper. | CMB-API-63 |

- **CMB-API-63** — MUST. `Combobox.Collection` calls `children(item, index)` for each item of the nearest Group's `items` when inside a Group that has `items`, otherwise for each of the Root's filtered items (CMB-STR-06); it renders nothing else.
  Source: `combobox/collection/ComboboxCollection.tsx:15-22`. Test: [L2] `ComboboxCollection.test.tsx` "renders filtered items", "renders nothing when a nested group does not provide items".

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.21 `Combobox.Group`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `items` | `readonly any[]` | — | Items for nested `Collection`s. | CMB-API-62 |
| native `div` props | | | Forwarded (`role`/`aria-labelledby` can be overridden). | CMB-ARIA-10 |

**Consumed:** `render`, `className`, `style`, `items`.

- **CMB-API-62** — MUST. `items`, when provided, is published to nested `Combobox.Collection`s through the Group context; the Group itself renders its children unchanged.
  Source: `combobox/group/ComboboxGroup.tsx:19-56`. Test: [L2] `ComboboxCollection.test.tsx` "renders filtered items" (grouped case); `ComboboxGroup.test.tsx` "should render group with label".

#### State object

Empty (`{}`), see CMB-STA-17. The Group additionally keeps the registered label id (CMB-STA-16).

- **CMB-STA-16** — MUST. A Group keeps the id of its mounted `GroupLabel` (set when the label mounts or its id changes). The label's cleanup clears the registration only if the registered id is still its own, so an older label unmounting cannot erase a newer label's id.
  Source: `combobox/group/ComboboxGroup.tsx:24-33`; `combobox/group-label/ComboboxGroupLabel.tsx:21-30`. Test: [L2] `ComboboxGroupLabel.test.tsx` "does not let an older label cleanup clear a newer label", "uses provided id in aria-labelledby".

#### Data attributes

None.

#### CSS variables

None.

### 3.22 `Combobox.GroupLabel`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated | Registered as the Group's label id. | CMB-ARIA-10 |
| native `div` props | | | Forwarded (`aria-hidden` can be overridden). | CMB-ARIA-10 |

**Consumed:** `render`, `className`, `style`.

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.23 `Combobox.Row`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

Only native `div` props (forwarded; `role` can be overridden). **Consumed:** `render`, `className`, `style`.

#### State object

Empty (`{}`), see CMB-STA-17.

#### Data attributes

None.

#### CSS variables

None.

### 3.24 `Combobox.Item`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `any` | `null` | The item's value. | CMB-API-56 |
| `index` | `number` | automatic | Explicit list index. | CMB-API-57 |
| `disabled` | `boolean` | `false` | Local disabled, OR-ed with the Root's. | CMB-API-58 |
| `nativeButton` | `boolean` | `false` | Button semantics (UBT). | CMB-API-59 |
| `onClick` | `(event) => void` | — | Fires on pointer activation and on <kbd>Enter</kbd> while highlighted. | CMB-API-60 |
| `children` | `ReactNode` | — | Item content; without `items` its text is the item's typeahead label (listed in the docs outline; no Combobox-specific handling). | CMB-STA-33 |
| `id` | `string` | `${rootId}-${index}` | Element id (consumer override wins). | CMB-DOM-09 |
| native `div` props | | | Forwarded; `onPointerDownCapture`/`onMouseDown`/`onMouseUp` merge with the selection handlers. | — |

**Consumed:** `render`, `className`, `style`, `value`, `index`, `disabled`, `nativeButton`.

- **CMB-API-56** — MUST. `value` (default `null`) identifies the item: it is registered in the Root's value registry at the item's index, compared with the selected value through `isItemEqualToValue` to derive `selected`, and passed to `handleSelection` on activation. Items are memoized on their props.
  Source: `combobox/item/ComboboxItem.tsx:40-45,76-82,98-110,152,260`. Test: [L2] `ComboboxItem.test.tsx` "selects item and closes in single mode", "reflects selected value with aria-selected when reopening".
- **CMB-API-57** — MUST. `index`, when provided, is the item's list index (skipping DOM-order resolution, CMP-STA-01); otherwise the index comes from the composite registry, or — when `virtualized` — from the item's position in the flat filtered values. An unregistered item (index `-1`) gets no id and is not navigable.
  Source: `combobox/item/ComboboxItem.tsx:52-55,73-74,81,224-252`. Test: [L2] `ComboboxItem.test.tsx` "updates the highlight on hover in a non-virtualized list", "does not assign an id to an explicitly unregistered item", "selects the highlighted filtered item with the keyboard after filtering" (explicit index); `ComboboxRoot.test.tsx` "Enter selects with manual indices provided to items".
- **CMB-API-58** — MUST. `disabled` (default `false`) is OR-ed with the Root's disabled; a disabled item is still highlightable but cannot be activated by pointer or <kbd>Enter</kbd>, and the button behavior keeps it focusable-when-disabled in composite mode.
  Source: `combobox/item/ComboboxItem.tsx:71,137-142,186,196`. Test: [L2] `ComboboxItem.test.tsx` "does not select disabled item", "inherits the disabled state from the root"; `popupListConformanceTests.tsx` "does not activate a disabled item", "stops on a disabled item while navigating" (L4 for the Enter clause and the `popupListConformanceTests` citations).
- **CMB-API-59** — MUST. `nativeButton` defaults to `false` (the item is a `div` with `role="option"`/`"gridcell"`; the button behavior runs in composite mode and emits no `tabindex`, UBT-DOM-05).
  Source: `combobox/item/ComboboxItem.tsx:47,137-142,170`. Test: [L2] `ComboboxItem.test.tsx` describeConformance; `ComboboxRoot.test.tsx` "sets correct attributes on the item when highlighted".
- **CMB-API-60** — MUST. `onClick` runs before the built-in selection for both pointer activation and <kbd>Enter</kbd> on the highlighted item (the Input/List dispatch a synthetic click on the highlighted element); calling `preventBaseUIHandler()` in it skips the selection.
  Source: `combobox/item/ComboboxItem.tsx:185-191,207,307-310`; `combobox/utils/parts.ts:46-58`. Test: [L2] `ComboboxItem.test.tsx` "calls onClick when clicked with a pointer", "calls onClick when selected with Enter key (via root interaction)", "does not select the item when onClick prevents Base UI handler" (L4 for the Enter clause).

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | resolved disabled |
| `selected` | `boolean` | value matches the selection |
| `highlighted` | `boolean` | item is the active (virtual-focused) item |

- **CMB-STA-13** — MUST. The Item state is `{ disabled, selected, highlighted }` where `selected` is `isItemEqualToValue`-membership in the selection and `highlighted` is `activeIndex === index`.
  Source: `combobox/item/ComboboxItem.tsx:76-82,144-148`; `combobox/store.ts:164-174`. Test: [L2] `ComboboxRoot.test.tsx` "sets correct attributes on the item when highlighted"; `ComboboxItem.test.tsx` "multiple mode toggles selection and stays open".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-selected` | selected | CMB-DOM-09 | C# |
| `data-highlighted` | highlighted | CMB-DOM-09 | C# |
| `data-disabled` | disabled | CMB-DOM-09 | C# |

#### CSS variables

None.

### 3.25 `Combobox.ItemIndicator`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element mounted while the item is not selected. | CMB-API-61 |
| `children` | `ReactNode` | `"✔️"` | Content. | CMB-DOM-10 |
| native `span` props | | | Forwarded (`aria-hidden` can be overridden). | CMB-DOM-10 |

**Consumed:** `render`, `className`, `style`, `keepMounted`.

- **CMB-API-61** — MUST. `keepMounted` (default `false`): when `false` the indicator is rendered only while its Item is selected and is removed in the deselecting commit with no exit phase (CMB-ANI-05); when `true` it is always rendered and the same element instance is preserved across selection changes.
  Source: `combobox/item-indicator/ComboboxItemIndicator.tsx:18-25`; `utils/ItemIndicator.tsx:21,41-51`. Test: [L2] `ComboboxItemIndicator.test.tsx` "mounts only while selected when keepMounted is false", "preserves the kept-mounted element across selection changes", "updates a mounted indicator when its item becomes unselected".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `selected` | `boolean` | item selected |
| `transitionStatus` | `TransitionStatus` | enter/exit status driven by `selected` |

- **CMB-STA-14** — MUST. The ItemIndicator state is `{ selected, transitionStatus }`. Server: relaxed, see §9.
  Source: `utils/ItemIndicator.tsx:23-26`; `combobox/item-indicator/ComboboxItemIndicator.tsx:40-49`. Test: [L2] `ComboboxItemIndicator.test.tsx` "updates a mounted indicator when its item becomes unselected".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-selected` | selected (default boolean projection) | CMB-DOM-10 | C# |
| `data-starting-style` / `data-ending-style` | transition status | CMB-DOM-10 | JS |

#### CSS variables

None.

### 3.26 `Combobox.Separator`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Separator orientation. | CMB-API-64 |
| native `div` props | | | Forwarded. | — |

**Consumed:** `render`, `className`, `style`, `orientation`.

- **CMB-API-64** — MUST. `Combobox.Separator` is the shared listbox separator: `orientation` (default `'horizontal'`) only feeds the state/`data-orientation` attribute.
  Source: `combobox/separator/ComboboxSeparator.tsx:6-30`; `utils/listbox-separator/ListboxSeparator.tsx:16-24`. Test: [L2] `ComboboxRoot.test.tsx` "exposes the orientation attribute rendered by Separator".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `orientation` | `Orientation` | as given |

- **CMB-STA-15** — MUST. The Separator state is `{ orientation }`.
  Source: `utils/listbox-separator/ListboxSeparator.tsx:18`. Test: [L2] `ComboboxRoot.test.tsx` "exposes the orientation attribute rendered by Separator".

#### Data attributes

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-orientation="horizontal\|vertical"` | always | CMB-DOM-16 | C# |

#### CSS variables

None.

- **CMB-STA-17** — MUST. The following parts expose an empty state object (`{}`) to `className`/`style`/`render` and emit no state-derived data attributes: `Chips`, `Value`, `Icon`, `Portal`, `Status`, `Empty`, `Collection`, `Group`, `GroupLabel`, `Row`.
  Source: `combobox/chips/ComboboxChips.tsx:53`; `combobox/value/ComboboxValue.tsx:42`; `combobox/icon/ComboboxIcon.tsx:32`; `combobox/portal/ComboboxPortal.tsx:38`; `combobox/status/ComboboxStatus.tsx:40`; `combobox/empty/ComboboxEmpty.tsx:50`; `combobox/collection/ComboboxCollection.tsx:25`; `combobox/group/ComboboxGroup.tsx:59`; `combobox/group-label/ComboboxGroupLabel.tsx:40`; `combobox/row/ComboboxRow.tsx:28`. Test: [L2] the respective describeConformance suites.

### 3.27 Shared types and functions

| Export | Shape | Req |
|---|---|---|
| `Combobox.useFilter(options?)` | `{ multiple?, value?, locale?, ...Intl.CollatorOptions }` → `{ contains, startsWith, endsWith }` | CMB-API-65 |
| `Combobox.useFilteredItems<T>()` | `() => T[]` (read-only) | CMB-API-66 |
| `Combobox.createItems(data, { getValue, getLabel })` | `ComboboxItemCollection<Item, Value>` | CMB-API-67, CMB-API-68 |
| `Combobox.Root.Actions` | `{ unmount(): void; close(): void }` | CMB-API-36 |
| `Combobox.Root.ChangeEventReason` | union of 15 reasons (§7) | CMB-EVT-01 |
| `Combobox.Root.ChangeEventDetails` | change-event details + `isItemPress?: boolean` | CMB-EVT-08 |
| `Combobox.Root.OpenChangeEventDetails` | `ChangeEventDetails & { preventUnmountOnClose(): void }` | CMB-EVT-01 |
| `Combobox.Root.HighlightEventReason` / `HighlightEventDetails` | `'keyboard' \| 'pointer' \| 'none'`; details `{ reason, event, index }` | CMB-EVT-05 |
| `ComboboxPrimitiveValue` | `string \| number \| bigint \| boolean` | CMB-API-68 |
| `ComboboxFilter`, `ComboboxFilterOptions` | the filter object / options types | CMB-API-65 |
| `Combobox.Separator.State` etc. | per-part `Props`/`State` namespaces and flat aliases | CMB-API-69 |

- **CMB-API-65** — MUST. `Combobox.useFilter({ multiple = false, value, ...collatorOptions })` returns an object with exactly the three matchers `contains`, `startsWith`, `endsWith` (no `collator` field; `ComboboxFilter` is that three-member shape) — the shared cached collator filter (`UTL-I18N-08..13`) with its `contains` replaced by a Combobox-aware matcher: in multiple mode `contains(item, query, itemToString?)` is `false` for a nullish item and otherwise the collator `contains`; in single mode it is additionally `true` when `query` is empty, and `true` when the stringified current `value` (via `itemToString.selected ?? itemToString`) equals the query under the collator and has the same length (so the full list shows while the input merely mirrors the selection). `startsWith`/`endsWith` are the shared ones. The returned object is referentially stable while `value`/`multiple`/collator options are unchanged. The hook is a consumer utility only: the Root's default `filter` is the plain multiple-mode matcher in every mode (CMB-API-14).
  Source: `combobox/root/utils/useFilter.ts:14-46`; `combobox/root/utils/index.ts:23-68`; `internals/filter.ts:76-83`. Test: [L1] `useFilter.test.tsx` "uses default options when called without arguments", "matches the start and end of an item label", "filters selected and unselected items in single and multiple modes"; `index.test.ts` "rejects nullish items and filters primitives and projected objects", "shows all items for an empty query and rejects nullish items", "shows all items when the query exactly matches the selected label", "otherwise filters the current item"; `createItems.test.tsx` "labels a derived selection when useFilter receives its value".
- **CMB-API-66** — MUST. `Combobox.useFilteredItems()` returns the Root's current filtered items (the same array the implicit Collection maps over), for use by consumers rendering their own list (e.g. virtualizers). It throws the derived-items context error outside a Root (CMB-STR-02).
  Source: `combobox/root/utils/useFilteredItems.ts:7-10`. Test: [L2] `createItems.test.tsx` "resolves virtualized collection items to their derived value", "resolves externally virtualized item indexes and callbacks in the derived value domain".
- **CMB-API-67** — MUST. `Combobox.createItems(data, { getValue, getLabel })` wraps flat or grouped source data (or `undefined` while loading) into a collection whose *selection value domain* is `getValue(item)` (a primitive) and whose labels come from `getLabel(item)`. Derivation is lazy: no accessor is called at creation; the value→item map is built on first lookup and only leaf items are projected; `undefined` data means "no items" (not an empty list that would render Empty). The collection exposes `data`, `value(item)`, `hasValue(value, isEqual)`, `itemLabel(item)` and `label(value, fallback?)`. When used as `items`, Items render with the derived value, `Combobox.Value` and the input show the derived label, filtering runs on the derived label unless a custom `filter` is given (which receives the source item), `itemToStringLabel`/`itemToStringValue`/`isItemEqualToValue` receive derived values, the label of a value not present in the collection falls back to `itemToStringLabel` then `String(value)`, and the first occurrence of a duplicated derived value wins the label while every item carrying it renders selected (dev error CMB-EDGE-02).
  Source: `combobox/items/createItems.ts:51-164`; `combobox/items/itemCollection.ts:5-55`; `combobox/root/AriaCombobox.tsx:146-185,216-229`. Test: [L2] `createItems.test.tsx` every test except "rejects an items object that is not a collection" (notably "uses the derived value for selection and resolves the label while closed", "treats undefined data as the absence of items rather than as an empty list", "does not call the accessors when creating a flat collection", "only projects the leaf items of the data", "resolves a duplicated derived value to the same label regardless of lookup order", "stops filtering when the limit is reached").
- **CMB-API-68** — MUST. `createItems` option types: `getValue: (item) => Value` where `Value extends string | number | bigint | boolean` (`null`/`undefined` are reserved for "no selection"; `0`, `''`, `false` and bigints are valid values), `getLabel: (item) => string`. Group-shaped source items (objects with an array `items` field) are rejected by the type when the item type is known and treated as groups at runtime; a non-array `items` field is ordinary item data.
  Source: `combobox/items/createItems.ts:8-70,92-96`. Test: [L2] `createItems.test.tsx` "resolves $label as a primitive selection" (×4), "treats a non-array items field as item data rather than as a group", "treats an array-valued items field as a group when its declared type is unknown", "does not stringify null when no value is selected".
- **CMB-API-69** — MUST. Every part exports `Props` and `State` both namespaced (`Combobox.<Part>.Props`) and as flat aliases (`Combobox<Part>Props`/`Combobox<Part>State`); the Root additionally exports `Actions`, `ChangeEventReason`, `ChangeEventDetails`, `OpenChangeEventDetails`, `HighlightEventReason`, `HighlightEventDetails`; the data-attribute and CSS-variable enums are exported as namespaces (`ComboboxInputDataAttributes`, `ComboboxInputGroupDataAttributes`, `ComboboxTriggerDataAttributes`, `ComboboxBackdropDataAttributes`, `ComboboxPositionerCssVars`, `ComboboxPositionerDataAttributes`, `ComboboxPopupDataAttributes`, `ComboboxArrowDataAttributes`, `ComboboxItemDataAttributes`, `ComboboxItemIndicatorDataAttributes`, `ComboboxClearDataAttributes`, `ComboboxSeparatorDataAttributes`).
  Source: `combobox/index.ts`; `combobox/index.parts.ts`; `combobox/root/ComboboxRoot.tsx:170-195`. Test: none (type-level).

## 4. State model

### 4.1 Owned state

Three pieces of state are independently controllable: the selected value, the input text and the open flag. Everything else (highlight, selected index, mounted/transition status, frozen close query, force-mount, open method, element registrations) is internal.

- **CMB-STA-18** — MUST. `selectedValue`, `inputValue` and `open` each use the shared controlled/uncontrolled utility (`UTL-STA-01..04`) with `name: 'Combobox'` and `state` `'selectedValue'` / `'inputValue'` / `'open'`; the ownership mode is fixed at first render and switching it later raises the shared dev error. Each setter (`setSelectedValue`, `setInputValue`, `setOpen`) first calls the corresponding consumer callback with the next value and event details, returns without any further effect when `isCanceled`, and otherwise commits the value (uncontrolled only) and runs that setter's follow-ups (EVD-STA-08). `setOpen` returns immediately when the requested state equals the current one.
  Source: `combobox/root/AriaCombobox.tsx:294-337,655-661,759-763,790-792,854-864`. Test: [L2] `ComboboxRoot.test.tsx` "controls the open state", "allows an attempted open to be canceled", "\"single\" keeps the popup open when selection is canceled"; `popupConformanceTests.tsx` "keeps the popup closed when the open change is canceled" (L4 for the `popupConformanceTests` citations).
- **CMB-STA-19** — MUST. Initial input text: when neither `inputValue` nor `defaultInputValue` is given (`hasInputValue` false), the uncontrolled initial text is the label of the initial selected value in single mode (resolved through the `items` collection/labels available on first render) and `''` in multiple mode; otherwise it is `defaultInputValue ?? ''`. An Input that registers *inside the popup* immediately writes `''` (reason `none`) when `hasInputValue` is false, so the popup input starts blank regardless of the selection.
  Source: `combobox/root/AriaCombobox.tsx:283,313-330`; `combobox/input/ComboboxInput.tsx:113-118`. Test: [L2] `ComboboxRoot.test.tsx` "derives input from defaultValue on first mount when unspecified", "derives input from defaultValue on first mount with items prop", "derives input from controlled value on first mount when unspecified", "defaultInputValue overrides derivation when provided", "multiple mode initial input remains empty", "does not set input value for input-inside-popup pattern"; `createItems.test.tsx` "resolves a server-rendered defaultValue label from the external window".
- **CMB-STA-20** — MUST. Input/selection synchronization (single mode, input outside the popup, `hasInputValue` false): after a selected-value change the input text is set to the selected label (reason `none`) when it differs; after `items` or the resolved selected label change the same sync runs unless a query was typed since the popup opened (`queryChangedAfterOpen`). At most one such write happens per commit even when several triggers fire together; a canceled write is retried by the next trigger. With `hasInputValue` true the component never writes the derived label on its own (the fill-on-select path, CMB-EVT-04, still applies).
  Source: `combobox/root/AriaCombobox.tsx:1280-1331`. Test: [L2] `ComboboxRoot.test.tsx` "updates derived input when controlled value changes externally", "re-derives input when items array changes", "restores the selected label after a one-step clear and items reload", "reports a single input value change when the controlled value changes", "syncs again after a canceled sync to the same label", "reports a single input value change when the selection is remapped", "re-syncs the input when the resolved label of an unchanged value changes", "restores derived input after items load asynchronously", "uses itemToStringLabel for input value synchronization"; `createItems.test.tsx` "updates a selected label when a stable accessor returns a new result".

### 4.2 Query and filtering

- **CMB-STA-21** — MUST. The *query* is the trimmed input text, except while the popup is closed (or closing) with a frozen *close query*, in which case the close query is used. `queryChangedAfterOpen` becomes `true` when a typed (not autofill-like) non-blank change happens, or when the query becomes non-empty, differs from the initial default input text and does not mirror the selection while open; it is reset when the popup finishes unmounting, when a close happens with an empty query, and on a reopen that interrupts a close when the input is empty/mirrors the selection. On close with `queryChangedAfterOpen` (not inline) the current query is frozen as the close query so the filtered list stays stable during the exit animation; the close query is released when the popup finishes unmounting, when typing resumes while open, or when a reopen interrupts the close (in which case a pending popup-input value that equals the close query or the selected label is cleared with reason `input-clear`).
  Source: `combobox/root/AriaCombobox.tsx:245-247,340,664-673,735-755,794-831,952-956,1262-1278`. Test: [L2] `ComboboxRoot.test.tsx` "keeps filtered popup content stable while closing with input inside popup", "keeps filtered popup content stable when input changes during the close animation", "releases the frozen query when controlled open reopens the popup", "unfreezes filtering when a controlled open ignores the close request (multiple, input outside popup)", "keeps the typed filter when items change afterwards (input outside popup)", "keeps filtering when typing reopens the popup (input outside popup)", "clears the pending filter with the \"input-clear\" reason when reopening interrupts the close animation", "releases the frozen query when reopening during the close animation with the input outside the popup", "clears the deferred popup input when reopening during close animation", "does not emit another clear when controlled input is already empty on interrupted close", "keeps a keystroke typed into an empty input when items refresh synchronously", "opens blank when the input only mirrors the selection (single, input inside popup)", "keeps an input value set in the same batch as a controlled reopen (input inside popup)", "shows every item when selection reopens the popup", "shows every item when a controlled reopen interrupts a canceled mirrored-selection clear", "handles closing after a changed query is cleared".
- **CMB-STA-22** — MUST. *Bypass*: in single mode, when no query has been typed since open, the query is non-empty, and the selected label equals the query under the collator (same length, collator `contains`), filtering is bypassed (the effective filter query is `''`, so every item shows). External `filteredItems` are ignored in that case only when `items` is also given and the selected value is resolvable (always for plain arrays; `collection.hasValue` for collections).
  Source: `combobox/root/AriaCombobox.tsx:342-356`. Test: [L1] `ComboboxRoot.test.tsx` "should show all items when query matches current selection", "should reset input value to selected value when popup closes without selection", "resets external filteredItems when reopening after a selection", "\"single\" selects and closes, then reopens with selection focused"; `createItems.test.tsx` "resets an emptied external result when reopening a grouped selection", "resets an initially empty external result when opening a grouped selection".
- **CMB-STA-23** — MUST. Filtered items derivation, in order: (1) `filteredItems` when given and not ignored (CMB-STA-22); (2) `[]` when no `items`; (3) grouped `items`: walk groups in order, stop once `limit` items were taken, take the group's first `remaining` items when the filter query is empty, else the first `remaining` items for which `filter(item, filterQuery, filterItemToString)` is true, and keep a group (with a copied `items` array) only when it contributed at least one item; (4) flat `items`: the first `limit` items when the query is empty (the original array when unlimited), else matching items up to `limit`. `flatFilteredValues` is the flattened leaf list projected through the collection's `value` accessor (the external window's precomputed values when `filteredItems` is used as-is). The `filterItemToString` passed to `filter` is `itemToStringLabel` for plain items and the collection's `itemLabel` (with `.selected` = label of a derived value) for collections.
  Source: `combobox/root/AriaCombobox.tsx:231-239,358-452`. Test: [L1] `ComboboxRoot.test.tsx` "omits groups with no matching items", "limits the number of items displayed when groups are used", "preserves order of items when applying limit across groups", "uses custom filter to narrow results", "uses filteredItems when items prop is omitted"; `createItems.test.tsx` "filters items by their derived labels and drops empty groups", "applies the limit across groups and drops the emptied ones", "keeps nullish entries out of the accessors and out of filtered results", "passes leaf source items to a custom root filter".

### 4.3 Highlight and selected index

- **CMB-STA-24** — MUST. `activeIndex` (highlighted item, `null` = none) and `selectedIndex` (index of the selected item in the rendered list, `null` = none/unknown) live in the store. `setIndices({ activeIndex?, selectedIndex?, type?, event? })` writes only the keys that are not `undefined`; when `activeIndex` was given it emits `onItemHighlighted` with the value registered at that index (or `undefined` for `null`), `reason = type ?? 'none'`, the event, and `index` (`-1` for none). Clearing an already-clear highlight emits nothing; re-emitting the same index for the same value is suppressed by the post-commit effect.
  Source: `combobox/root/AriaCombobox.tsx:260,606-653,1198-1227`. Test: [L2] `ComboboxRoot.test.tsx` "fires a single clearing highlight on Enter selection", "updates highlighted callback with newly filtered first item", "emits onItemHighlighted for the selected item when the query is cleared"; `ComboboxChipRemove.test.tsx` "records pointer-origin removal when clearing the highlighted item".
- **CMB-STA-25** — MUST. Selected-index sync runs post-commit pre-paint whenever `open`, the close query, the selection, the registry or the comparer changes, but only while closed, or while open at the moment the close query is released with `items`: it clears the pending pointer-down item when closed and sets `selectedIndex` to the index of the selected value in the registry (`flatFilteredValues` with `items`/`filteredItems`, otherwise the live values registered by mounted Items), where in multiple mode the anchor is the *first selected item in rendered order* (not value order). Mounted Items re-assert their own selected index when their registration moves; with no mounted items and no `items` the lookup yields `null`. Changing the selection while open never moves the highlight (CMP-STA-40). Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:1026-1071`; `combobox/item/ComboboxItem.tsx:112-135`; `internals/itemEquality.ts` (`findSelectionIndex`). Test: [L2] `ComboboxRoot.test.tsx` "syncs selected index when items change after close", "clears selectedIndex when the value is cleared externally while closed (no items prop)", "clears selectedIndex when the value is set to an unmatched value while closed (no items prop)", "registers selectedIndex before opening with a keepMounted portal (no items prop)", "does not highlight the previous item after the value changes while open (no items prop)", "re-highlights the selection on reopen when the value object is recreated with equal contents (no items prop)", "anchors the highlight to the first selected item in rendered order regardless of value order", "anchors the highlight to the first selected item with individually rendered items", "anchors to the first selected item in rendered order across groups", "re-syncs selectedIndex after an external controlled update when closing", "resets selectedIndex when clearing all selections while open", "clears selectedIndex when all values are removed externally while closed (no items prop)", "restores the selected direct item after its registration index changes", "opens a reordered external list at the selected value in rendered-list coordinates".
- **CMB-STA-26** — MUST. *Typed-query highlight (pending request).* A typed input change records a pending request `{ hasQuery }`; a programmatic clear with reason `input-clear` while the input is inside the popup records `{ hasQuery: false, selection: true }` (plus `toggledValue` when a multiple-mode press just added the item). Post-commit pre-paint, once the filtered list is derived: a `hasQuery` request with `autoHighlight` enabled and a non-blank input sets `activeIndex` to `0` as soon as the list is navigable (open, inline, or kept-mounted and visible) and a candidate exists (or no registry is used); a `hasQuery` request is discarded when `autoHighlight` is off, the input is blank, the inline input lost focus, its own open request was canceled, any other open change happens, or a controlled close/kept-mounted dialog close occurs. A cleared-query request (input now blank) is resolved after a microtask: when a selection exists the highlight returns to the toggled item if present, else to the first selected item in rendered order (`null` when the selection is absent from the list); a selection-driven clear without a selection clears the highlight; otherwise the highlight is left as is (`autoHighlight: true` keeps whatever was highlighted). The microtask is skipped when the popup closed meanwhile or the user typed again. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:261-267,682-689,721-729,783-788,1080-1192`. Test: [L2] `ComboboxRoot.test.tsx` "highlights a candidate arriving after typing with %s", "waits for a controlled popup to open", "discards the request when a rejected typed open is followed by a controlled open", "discards the request when an ignored typed open is followed by another open", "discards the request when a rejected typed open is followed by opening with %s", "discards the request when the controlled input is cleared", "discards the request when a controlled close is interrupted", "discards the request when autoHighlight is disabled", "does not reset keyboard navigation when candidates change", "keeps a hovered item highlighted when individually rendered items arrive", "highlights the first result when typing filters out individually rendered items", "returns the highlight to the selected item when the input is cleared", "returns the highlight to the selected item without the items prop", "restores an array-valued single selection when the query is cleared", "does not highlight anything when there is no selected item", "restores the selected item instead of the first item when clearing with autoHighlight", "consumes a query-clear restore when the selected item is absent", "keeps the toggled item highlighted when selecting with the keyboard while filtering", "restores the anchor rather than the toggled item when deselecting while filtering", "keeps the popup open and highlights the newly selected item after clearing its query", "restores the highlight to the first selected item when clearing (input inside popup)", "restores the highlight when clearing with the input outside the popup", "waits for a controlled query clear to commit before restoring the selection (items prop)", "clears the highlight when a keepMounted dialog closes (multiple=%s, query=\"%s\")", "retains highlight when query is cleared back to empty", "retains highlight when clearing the query with input-change behavior", "restores the first highlight after an always-highlighted query becomes empty".
- **CMB-STA-27** — MUST. Post-commit pre-paint while open or inline, after the pending-request handling: an `activeIndex` beyond the candidate list is reset to `null` (emitting a clearing highlight); an in-range index whose value or index differs from the last emitted highlight re-emits `onItemHighlighted` with reason `none` (so list changes report the new item under the highlight); when `autoHighlight` is enabled, `items` is used and the filtered list becomes empty, the highlight is cleared (after paint) so the next <kbd>ArrowDown</kbd> lands on the first item once results return. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:1194-1227,1254-1260`. Test: [L2] `ComboboxRoot.test.tsx` "updates highlighted callback with newly filtered first item", "clears the highlight when filtering deselects the only selected item", "moves the anchor when the anchor item value changes", "moves the anchor to the next selected item when the anchor item leaves the list", "does not snap the highlight to a stale index while filtering an inline list (items prop)".
- **CMB-STA-28** — MUST. Inline initial highlight: the store's initial `selectedIndex` is computed once at construction as the index of the selection in `flatFilteredValues` when `inline`, `open` and `items` are all set (so list navigation highlights and scrolls to the selection on mount); a selection or list that resolves later does not move an existing highlight. An inline Input saves the highlight on blur (unless `autoHighlight` is `'always'`), clears it, and restores it on refocus within the same open cycle provided the saved slot still holds a registered value.
  Source: `combobox/root/AriaCombobox.tsx:454-470`; `combobox/input/ComboboxInput.tsx:102-109,188-216`. Test: [L2] `ComboboxRoot.test.tsx` "seeds the initial highlight for a matched inline value", "does not seed an initial highlight for an unmatched inline value", "highlights and scrolls the selected item into view on mount when inline (items prop)", "does not restore the initial highlight after focus leaves an inline input (items prop)", "does not apply the initial highlight when a selection resolves after blur (items prop)", "highlights the first selected item on mount and keeps the highlight when toggling in a multiple inline list (items prop)", "does not highlight a non-selected item when an initial input value pre-filters the inline list (items prop)", "does not replace a pointer highlight when the initial selection resolves after mount (items prop)", "does not restore the initial highlight after pointer navigation is cleared (items prop)", "emits the selected item index to onItemHighlighted on mount when inline and virtualized (items prop)", "restores highlight when the input regains focus"; `ComboboxInput.test.tsx` "does not restore an inline highlight after the highlighted slot is removed"; `createItems.test.tsx` "highlights an initially selected derived value on mount when inline".

### 4.4 Mounting, transition and open method

- **CMB-STA-29** — MUST. `mounted` and `transitionStatus` come from the shared unmount-after-close mechanism (TRN-ANI-18..23, TRN-STA-*) driven by `open`, with the animated element being the Popup element — or, when `inline`, the closest `[role="dialog"]` ancestor of the list element. `preventUnmountOnClose()` on the open-change details holds the closing phase until `actions.unmount()`; the hold is cleared when a controlled reopen interrupts the close, is ignored on a canceled close, and is kept when a controlled close is applied inside a transition. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:757,777-778,833-836,998-1015`; `internals/useUnmountAfterClose.ts`. Test: [L4] `ComboboxRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing", "clears the opt-out when a controlled reopen interrupts a pending unmount", "ignores an opt-out on a canceled close", "keeps the opt-out when a controlled close is applied in a transition".
- **CMB-STA-30** — MUST. Unmount cleanup (runs once when the closing phase completes, in this order): `onOpenChangeComplete(false)`; reset `queryChangedAfterOpen` and the close query; clear `activeIndex`; in multiple mode, when the input still holds text and no `input-clear` write preceded (a canceled selection clear counts as none), clear the input with reason `input-clear`; in single mode, when the input is inside the popup clear a non-empty input with reason `input-clear`, otherwise set the input to the selected label when it differs, with reason `input-clear` when that label is empty (typed filter discarded) and `none` otherwise.
  Source: `combobox/root/AriaCombobox.tsx:256,664,952-992`. Test: [L2] `ComboboxRoot.test.tsx` "\"multiple\" clears typed input on close when no selection made", "\"single\" clears typed input on close when no selection made (input outside popup)", "still clears the kept filter text once the popup closes", "does not clear popup input after canceled multiple value changes", "clears a closing query and restores the first selected item on reopen", "clears a multiple inline query on close", "automatically unmounts with an actions ref and completes closing once"; `ComboboxInput.test.tsx` "closes the popup when tabbing out".
- **CMB-STA-31** — MUST. `openMethod` records the interaction type that opened the popup (PSH-PTR-03: set from the Trigger's pointer/click handlers only while closed, `null` after close). It selects the touch-specific focus behavior (CMB-API-51, CMB-FOC-04) and the touch scroll-lock rule (CMB-API-24).
  Source: `combobox/root/AriaCombobox.tsx:580`; `utils/useOpenInteractionType.ts`. Test: [L4] `ComboboxPopup.test.tsx` "focuses the popup instead of its input when opened by touch"; `ComboboxRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width".
- **CMB-STA-32** — MUST. `forceMounted` (store) and `forceMount()`: a Trigger press and single-mode autofill call `forceMount()`, which with `items` refreshes the typeahead labels from the filtered values and otherwise sets `forceMounted: true` (never reset) so the Portal renders the list while closed and Items register their values/labels; autofill additionally sets `forceMounted` when `items` is used but no serialized value matched (so rendered labels can be matched). The List registers rendered labels only when `items` is absent or the list is force-mounted.
  Source: `combobox/root/AriaCombobox.tsx:593-600,1679-1687`; `combobox/portal/ComboboxPortal.tsx:24-26`; `combobox/list/ComboboxList.tsx:109-112`; `combobox/trigger/ComboboxTrigger.tsx:201-202`. Test: [L2] `ComboboxRoot.test.tsx` "does not force-mount the list when autofill matches a serialized value with the items prop", "does not force-mount the list when controlled value changes externally (strict: %s)", "matches browser autofill when an earlier item has no rendered label"; `ComboboxTrigger.test.tsx` "cycles to the next matching item when typing after open/close (no items prop, keepMounted %s)".

### 4.5 Registries and store publication

- **CMB-STA-33** — MUST. Three parallel registries index the rendered list: `listRef` (item elements), `valuesRef` (item values) and `labelsRef` (typeahead labels). With `items` the values and the list length are taken from `flatFilteredValues` post-commit pre-paint and labels are derived on demand; without `items` each mounted Item writes its element and value at its index (and deletes them on unmount or index change) and the List's composite registry collects labels from item text (CMP-STA-01, `CompositeList` labels). Chips use a separate composite registry of chip elements. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:249-250,272,1073-1078`; `combobox/item/ComboboxItem.tsx:84-110`; `combobox/list/ComboboxList.tsx:109-118`; `combobox/chips/ComboboxChips.tsx:27,48`. Test: [L2] `ComboboxItem.test.tsx` "registers a rendered window at its filtered indexes", "does not register an item missing from the filtered values"; `ComboboxTrigger.test.tsx` "only matches mounted indexed item labels when typing on the focused trigger"; `ComboboxChip.test.tsx` "should navigate through only rendered chips".
- **CMB-STA-34** — MUST. Store publication: the prop bags (`popupProps`, `listProps`, `inputProps`, `triggerProps`, `itemRoot`) and `inline` are written before the first child render; every synchronized value (id, selection, open, mounted, transition status, items, mode flags, name/form, disabled/readOnly/required, grid, virtualized, openOnInputClick, label/equality functions, modal, autoHighlight mode, hasInputValue, openMethod) is written post-commit pre-paint in a single `update` together with `inputOwnsFormValue` (always `false` for Combobox), so subscribers never see an intermediate snapshot; the context commands (`setOpen`, `setInputValue`, `setSelectedValue`, `setIndices`, `handleSelection`, `forceMount`, `requestSubmit`, `onOpenChangeComplete`) are real from the first render of the Root (a descendant's DOM-handle callback can call them). The selectors `hasSelectionChips` (multiple with ≥1 value), `hasSelectedValue`, `hasNullItemLabel(items)`, `isActive(index)` and `isSelected(value)` derive their results from the store state with `isItemEqualToValue`. Server: met on both modes, see §9.
  Source: `combobox/root/AriaCombobox.tsx:1503-1567`; `combobox/store.ts:129-210`. Test: [L2] `ComboboxChips.test.tsx` "sets role=\"toolbar\" when there is at least one chip" (`hasSelectionChips`); engine mechanics only — `store.test.tsx` "publishes an inline transition in a single store transaction", "exposes real commands to a descendant ref callback on the first commit" (both render `Autocomplete.Root`, `selectionMode: 'none'`, where `inputOwnsFormValue` can flip to `true`; they pin the single-transaction publication and the first-commit commands, not Combobox ownership). No Combobox-mode test pins that `inputOwnsFormValue` stays `false`.

### 4.6 State transitions summary

| Event | Open | Selection | Input text | Highlight |
|---|---|---|---|---|
| Input press (`openOnInputClick`) | open (`input-press`) | — | — | none (pointer open) |
| Typing a non-blank query | open (`input-change`) | single/outside popup: emptied text clears the selection (`input-clear`) | typed (`input-change`) | cleared, or index 0 with `autoHighlight` |
| ArrowDown/ArrowUp on input | open (`list-navigation`) | — | — | selected item, else first/last |
| Item press / Enter (single) | close (`item-press`) | item (`item-press`) | label (`item-press`, outside popup) | — |
| Item press / Enter (multiple) | stays open; closes when filtering with input outside popup | toggled (`item-press`) | cleared (`input-clear`, `isItemPress: true`) when filtering with input inside popup | toggled item |
| Escape while open | close (`escape-key`) | — | restored to selection on unmount | cleared |
| Escape while closed | — | cleared (`escape-key`) | `''` (`escape-key`) | — |
| Clear press | — | cleared (`clear-press`) | `''` (`clear-press`) | cleared |
| ChipRemove / Backspace in empty input | — | value removed (`chip-remove-press` / `none`) | — | cleared when it was the removed item |
| Outside press / focus-out | close (`outside-press`/`focus-out`) | — | restored on unmount | cleared |
| Close completes | mounted=false | — | see CMB-STA-30 | cleared |

## 5. Rendered DOM and ARIA

### 5.1 `Input` element

- **CMB-DOM-01** — MUST. `Combobox.Input` renders an `<input>` (no `type` attribute is forced; a substituted `<textarea>` stays editable and receives no `type`) whose value is the Root input text, with `autocomplete="off"`, `spellcheck="false"`, `autocorrect="off"`, `autocapitalize="none"`, the id of CMB-API-39, `disabled`/`readonly`/`required`-related attributes per the resolved flags (`aria-*` in §5.5; the native `disabled` attribute follows the resolved disabled flag), and no `name` (the hidden input carries the form value). Outside the popup it also receives the Field validation props (`aria-invalid`, `aria-describedby`, FFI-ARIA-03/01) and the rendered Field state attributes.
  Source: `combobox/root/AriaCombobox.tsx:1342-1379`; `combobox/input/ComboboxInput.tsx:127-129,171-187,423`. Test: [L2] `ComboboxRoot.test.tsx` "sets all aria attributes on the input when closed", "sets combobox aria attributes on the input"; `ComboboxInput.test.tsx` "renders as a <textarea> without an invalid `type` attribute and stays editable", "should render aria-disabled attribute when disabled".
- **CMB-DOM-02** — MUST. Input data attributes: `data-popup-open=""` and `data-pressed=""` while open (pressable trigger mapping, PSH-DOM-02); `data-disabled`, `data-readonly` from the state flags (default boolean projection, RND-DOM-01/05); no `data-required` — although `ComboboxInputDataAttributes`/`ComboboxTriggerDataAttributes` and the docs outline declare it, neither the part state nor the Field state (`FieldRootState`: disabled, touched, dirty, valid, filled, focused) has a `required` field and the mapping adds none, so the attribute is never rendered (`required` surfaces only as `aria-required` and the hidden input's `required`, CMB-API-28); `data-popup-side="<side>"` only while the popup is mounted with a Positioner; `data-list-empty=""` while the filtered list is empty; `data-valid`/`data-invalid` (FFI validity mapping), `data-touched`, `data-dirty`, `data-filled`, `data-focused` from the Field state — all field-derived attributes are absent when the input is inside the popup (CMB-STA-05).
  Source: `combobox/utils/stateAttributesMapping.ts:7-19`; `combobox/input/ComboboxInput.tsx:140-147,425`; `combobox/input/ComboboxInputDataAttributes.ts:24-27`; `field/root/FieldRoot.tsx:112-122,239-264`. Test: [L2] `ComboboxInput.test.tsx` "sets data-popup-side to the current popup side", "toggles data-list-empty when the filtered list is empty", "should render aria-readonly and readonly attributes when readOnly"; `ComboboxRoot.test.tsx` "[data-touched]", "[data-dirty]", "adds [data-filled] attribute when filled", "[data-focused]", "[data-invalid]", "[data-valid]".

### 5.2 Hidden `<input>` elements

- **CMB-DOM-18** — MUST. The Root renders, after its children, one `<input>` with: `name` = the effective name in single mode and *no name* in multiple mode; `value` = the serialized selected value (`''` for arrays); `form`; `autocomplete` = the Root `autoComplete` prop; `disabled`; `required` = `required && !(multiple && selection non-empty)`; `readonly`; `tabindex="-1"`; `aria-hidden`; the Field validation props; inline style `visuallyHiddenInput` (UTL-DOM-14) when it has a name and `visuallyHidden` (UTL-DOM-13) otherwise; hydration warnings suppressed. Its `ref` is the Root `inputRef` merged with the Field validation input ref.
  Source: `combobox/root/AriaCombobox.tsx:1569,1581-1590,1615-1704`. Test: [L2] `ComboboxRoot.test.tsx` "should pass autoComplete to the hidden input", "should set disabled attribute on hidden input", "should set readOnly attribute on hidden input", "keeps the hidden input required when no selection exists in multiple mode", "serializes {value,label} objects using their value field", "triggers native HTML validation on submit".
- **CMB-DOM-19** — MUST. In multiple mode with a name, one `<input type="hidden" name form value disabled>` per selected value is rendered after the main hidden input, keyed by and valued with the serialized value (FFI-DOM-06); they are omitted entirely without a name.
  Source: `combobox/root/AriaCombobox.tsx:1592-1610,1705`. Test: [L2] `ComboboxRoot.test.tsx` "should create multiple hidden inputs for form submission", "serializes multiple {value,label} objects into multiple hidden inputs", "uses itemToStringValue for multiple selection form submission", "does not submit multiple values when disabled".
- **CMB-DOM-20** — MUST. The main hidden input has `id="${rootId}-hidden-input"` when, and only when, it has no name (FFI-DOM-03).
  Source: `combobox/root/AriaCombobox.tsx:1691`. Test: [L2] `ComboboxRoot.test.tsx` "focuses trigger and surfaces errors when input is inside popup" (hidden input lookup by id).

### 5.3 Trigger, InputGroup, Clear, Chips elements

- **CMB-DOM-03** — MUST. InputGroup data attributes: the same mapping as the Input (CMB-DOM-02) applied to the InputGroup state, plus `data-placeholder=""` when no value is selected; `data-required` is never emitted (not in its state).
  Source: `combobox/input-group/ComboboxInputGroup.tsx:41-49,69`; `combobox/input-group/ComboboxInputGroupDataAttributes.ts`. Test: [L2] `ComboboxInputGroup.test.tsx` describeConformance; `ComboboxRoot.test.tsx` "should render disabled state on all interactive components".
- **CMB-DOM-04** — MUST. Trigger data attributes: as CMB-DOM-02 applied to the Trigger state (including the Field state even when the input is outside the popup) plus `data-placeholder=""` when no value is selected.
  Source: `combobox/trigger/ComboboxTrigger.tsx:134-142,261`; `combobox/trigger/ComboboxTriggerDataAttributes.ts`. Test: [L2] `ComboboxTrigger.test.tsx` "sets data-popup-side to the current popup side", "toggles data-list-empty when the filtered list is empty", "has data-placeholder when no value is selected", "applies the data-readonly style hook only when readOnly".
- **CMB-DOM-05** — MUST. The Trigger renders a `<button>` per the shared button behavior (UBT-DOM-01/02/06; `type="button"` when native) with `tabindex="0"` when the input is inside the popup and `tabindex="-1"` otherwise (it is not a tab stop next to the input), and the Field validation props.
  Source: `combobox/trigger/ComboboxTrigger.tsx:148-162,258-259`. Test: [L2] `ComboboxTrigger.test.tsx` "sets tabIndex to -1 when not used as the main anchor", describeConformance (`button: true`).
- **CMB-DOM-06** — MUST. Clear data attributes: `data-visible=""` when visible, `data-disabled=""` when disabled, `data-popup-open=""` while the popup is open (plain trigger mapping, PSH-DOM-01), `data-starting-style`/`data-ending-style` per transition status (TRN-DOM-01/02).
  Source: `combobox/clear/ComboboxClear.tsx:18-21,73-78,136`; `combobox/clear/ComboboxClearDataAttributes.ts`. Test: [L4] `ComboboxClear.test.tsx` "triggers enter animation via data-starting-style when becoming visible", "triggers exit animation via data-ending-style before unmount", "is disabled when root disabled and does nothing on click".
- **CMB-DOM-07** — MUST. The Clear element is rendered iff `keepMounted || mounted` where `mounted` follows `visible` through the transition-status machine (TRN-ANI-01, TRN rows for `Combobox.Clear`); it is a `<button>` (UBT) with `tabindex="-1"` and default content `"x"`.
  Source: `combobox/clear/ComboboxClear.tsx:71-96,138-144`. Test: [L4] `ComboboxClear.test.tsx` "renders in single mode when a value is selected", "does not render without a value by default", "renders and clears the typed value in no-selection mode", "renders and clears chips in multiple mode".
- **CMB-DOM-11** — MUST. `Chip` renders a `<div tabindex="-1">` (focusable programmatically, not a tab stop) with `data-disabled` when the Root is disabled; `ChipRemove` renders a `<button>` (UBT, `tabindex="-1"`, focusable when disabled) with `data-disabled` when disabled. `Chips` renders a `<div>` with no state attributes.
  Source: `combobox/chip/ComboboxChip.tsx:104-111`; `combobox/chip-remove/ComboboxChipRemove.tsx:43-51,93-99`; `combobox/chips/ComboboxChips.tsx:29-42`. Test: [L2] `ComboboxChipRemove.test.tsx` "should have proper tab index", "should render disabled attribute when disabled"; `ComboboxChip.test.tsx` "should render aria-disabled attribute when disabled".

### 5.4 List, Item, ItemIndicator, Separator elements

- **CMB-DOM-08** — MUST. The List renders a `<div>` with `tabindex="-1"`, `id` = the floating context id (the value the Input's `aria-controls` references), `data-empty=""` while the filtered list is empty, and the list-navigation/role floating props.
  Source: `combobox/list/ComboboxList.tsx:67-82`; `combobox/root/AriaCombobox.tsx:1482-1485`. Test: [L2] `ComboboxRoot.test.tsx` "sets all aria attributes on the input when open" (`aria-controls` equals the listbox id); `ComboboxList.test.tsx` describeConformance.
- **CMB-DOM-09** — MUST. An Item renders a `<div>` with `id="${rootId}-${index}"` when the Root has an id and the item is registered (consumer `id` overrides), `data-selected=""` when selected, `data-highlighted=""` when highlighted, `data-disabled=""` when disabled, and no `tabindex`.
  Source: `combobox/item/ComboboxItem.tsx:81,144-148,163-170,204-208`; `combobox/item/ComboboxItemDataAttributes.ts`. Test: [L2] `ComboboxRoot.test.tsx` "sets correct attributes on the item when highlighted"; `ComboboxItem.test.tsx` "does not assign an id to an explicitly unregistered item", "multiple mode toggles selection and stays open".
- **CMB-DOM-10** — MUST. An ItemIndicator renders a `<span aria-hidden="true">` with default content `"✔️"`, `data-selected=""` while selected and the transition-status attributes; it is rendered iff `keepMounted || selected` (TRN-ANI-01 exception) — there is no exit-transition grace period without `keepMounted`; a kept-mounted unselected indicator gets no `hidden`/inline style (TRN-DOM-07).
  Source: `utils/ItemIndicator.tsx:28-39`; `combobox/item-indicator/ComboboxItemIndicator.tsx:18-25`. Test: [L2] `ComboboxItemIndicator.test.tsx` "updates a mounted indicator when its item becomes unselected", "mounts only while selected when keepMounted is false", "preserves the kept-mounted element across selection changes" (L4 for the transition-status attribute clause).
- **CMB-DOM-16** — MUST. The Separator renders `<div role="presentation" data-orientation="<orientation>">`.
  Source: `utils/listbox-separator/ListboxSeparator.tsx:16-24`. Test: [L2] `ComboboxRoot.test.tsx` "exposes the orientation attribute rendered by Separator".
- **CMB-DOM-23** — MUST. `Combobox.Empty` renders its element always (so the live region persists) but passes `children` only while the filtered list is empty (`null` otherwise); `Combobox.Status` always renders its children.
  Source: `combobox/empty/ComboboxEmpty.tsx:27-46`; `combobox/status/ComboboxStatus.tsx:22-36`. Test: [L2] `ComboboxEmpty.test.tsx` "renders when there are no filtered items", "does not render when there are items", "renders when the search query matches no items", "does not render when the search query matches an item"; `ComboboxStatus.test.tsx` "renders only when open" (rendered inside the Popup).

### 5.5 Popup, Positioner, Backdrop, Arrow, Portal, Label elements

- **CMB-DOM-12** — MUST. The Popup renders a `<div>` with the popup id (CMB-DOM-24), the focusable-popup props (PSH `FOCUSABLE_POPUP_PROPS`: `tabindex="-1"` plus the focus-redirect marker) merged with the dismiss floating props, the disabled-mount transition styles while `'starting'` (TRN-DOM-05), and attributes `data-open`/`data-closed` (exactly one), `data-side`, `data-align`, `data-anchor-hidden`, `data-starting-style`/`data-ending-style`, `data-empty`.
  Source: `combobox/popup/ComboboxPopup.tsx:22-25,83-105`; `combobox/root/AriaCombobox.tsx:1477-1480`; `utils/popupStateMapping.ts:48-61`. Test: [L2] `ComboboxPopup.test.tsx` "exposes open state via data attributes mapping"; `popupConformanceTests.tsx` "opens on trigger click and sets `aria-expanded` on the trigger" (L4 for the disabled-mount transition-style clause and the `popupConformanceTests` citation).
- **CMB-DOM-24** — MUST. Popup id resolution: the consumer's `id`, else `${rootId}-popup` when the input is inside the popup, else none. Post-commit pre-paint the store's `popupId` becomes the *rendered* element id (a `render`-supplied id wins), cleared on unmount; the Trigger uses it for `aria-controls`, falling back to `${rootId}-popup` during the commit in which the popup mounts. Server: relaxed, see §9.
  Source: `combobox/popup/ComboboxPopup.tsx:54-62`; `combobox/root/utils/index.ts:13-15`; `combobox/trigger/ComboboxTrigger.tsx:93-101`. Test: [L2] `ComboboxRoot.test.tsx` "sets aria-controls to the dialog popup on trigger", "points aria-controls at a custom popup id on the trigger", "points aria-controls at a popup id supplied through the render prop"; `popupConformanceTests.tsx` "allows a custom `id` prop" (L4 for the `popupConformanceTests` citations).
- **CMB-DOM-13** — MUST. The Positioner renders the shared positioner `<div role="presentation">` (FLT-DOM-01) with `hidden` while not mounted, `pointer-events: none` while not open, the positioning styles merged before the consumer `style`, attributes `data-open`/`data-closed`, `data-side`, `data-align`, `data-anchor-hidden`, `data-empty`, and the CSS variables `--available-width`, `--available-height`, `--anchor-width`, `--anchor-height`, `--transform-origin` (FLT-DOM-05..07).
  Source: `combobox/positioner/ComboboxPositioner.tsx:98-121`; `utils/usePositioner.tsx:24-41`; `combobox/positioner/ComboboxPositionerCssVars.ts`. Test: [L4] `ComboboxPositioner.test.tsx` describeConformance; `positionerConformanceTests` (§13).
- **CMB-DOM-14** — MUST. The Backdrop renders `<div role="presentation">` with `hidden` while not mounted, inline `user-select: none; -webkit-user-select: none`, and `data-open`/`data-closed`, `data-starting-style`/`data-ending-style`. (The *internal* modal backdrop of CMB-API-24 is a separate element, PSH-STR-09/DOM-14.)
  Source: `combobox/backdrop/ComboboxBackdrop.tsx:11-14,39-54`. Test: [L2] `ComboboxBackdrop.test.tsx` describeConformance (L4 for the `data-starting-style`/`data-ending-style` clause).
- **CMB-DOM-15** — MUST. The Arrow renders `<div aria-hidden="true">` with the arrow position styles (FLT-DOM-09) merged before the consumer `style`, and `data-open`/`data-closed`, `data-side`, `data-align`, `data-uncentered`.
  Source: `combobox/arrow/ComboboxArrow.tsx:34-43`; `combobox/arrow/ComboboxArrowDataAttributes.ts`. Test: [L4] `ComboboxArrow.test.tsx` describeConformance.
- **CMB-DOM-21** — MUST. The Portal renders its floating portal (FLT-DOM-11) iff `mounted || keepMounted || forceMounted`; otherwise nothing.
  Source: `combobox/portal/ComboboxPortal.tsx:19-36`. Test: [L2] `ComboboxRoot.test.tsx` "does not render the start dismiss button while closed" (nothing portaled while closed); `popupConformanceTests.tsx` "removes the popup when there is no exit animation defined" (L4 for the `popupConformanceTests` citations).
- **CMB-DOM-22** — MUST. Each internal dismiss button is a `<span>` with the non-native button props (`role="button"`, UBT-API-04), `aria-label="Dismiss"`, no `tabindex` attribute, and the `visuallyHiddenInput` style.
  Source: `combobox/utils/ComboboxInternalDismissButton.tsx:19-44`. Test: [L2] `ComboboxRoot.test.tsx` "renders internal dismiss buttons before the input and after the popup".
- **CMB-DOM-17** — MUST. The Label renders a `<div>` with its id (default `${rootId}-label`), the non-native label props (FFI-PTR-01: `pointerdown` prevented, click focuses the control), and the Field validity data attributes plus the default boolean projection of the field state.
  Source: `combobox/label/ComboboxLabel.tsx:36,55-70`. Test: [L2] `ComboboxLabel.test.tsx` describeConformance; `ComboboxRoot.test.tsx` "Combobox.Label focuses trigger without opening when input is inside popup".

### 5.6 ARIA

- **CMB-ARIA-01** — MUST. When the input is outside the popup the Input carries the combobox pattern: `role="combobox"`, `aria-expanded="true|false"`, `aria-haspopup="listbox"` (`"grid"` when `grid`), `aria-controls="<list id>"` only while expanded, `aria-activedescendant` per CMB-ARIA-03, `aria-autocomplete` per CMB-ARIA-04, `aria-readonly="true"` when readOnly, `aria-required="true"` when required, `aria-labelledby` = the Field label id when one exists. The attributes are present in server-rendered HTML.
  Source: `combobox/root/AriaCombobox.tsx:1342-1379`; `combobox/input/ComboboxInput.tsx:176-182`. Test: [L2] `ComboboxRoot.test.tsx` "sets all aria attributes on the input when closed", "sets all aria attributes on the input when open", "sets combobox aria attributes on the input", "sets the popup type on the input when rendered inline as a grid"; `ComboboxTrigger.test.tsx` "sets all aria attributes on the input when closed"; `ComboboxInput.test.tsx` "sets aria-required attribute when required".
- **CMB-ARIA-02** — MUST. `expanded` is `open || inline`: an inline combobox is permanently `aria-expanded="true"` (its floating context is permanently open) even when the internal `open` state is `false`, and `aria-controls` points at the list while expanded.
  Source: `combobox/root/AriaCombobox.tsx:1334,1345`. Test: [L2] `ComboboxRoot.test.tsx` "sets aria-expanded on the input when rendered inline and open", "keeps the input expanded when rendered inline without the `open` prop".
- **CMB-ARIA-03** — MUST. While expanded and an item is highlighted, the control carries `aria-activedescendant="${rootId}-${activeIndex}"` (CMP-ARIA-01, virtual focus); the List (floating element) does not duplicate it because the reference is a typeable combobox (CMP-ARIA-02). The attribute is removed when the highlight is cleared (typing, pointer leave, close).
  Source: `combobox/root/AriaCombobox.tsx:1416-1422`; CMP. Test: [L2] `ComboboxRoot.test.tsx` "sets correct attributes on the item when highlighted", "opens, navigates with ArrowDown, and Enter selects"; `popupListConformanceTests.tsx` "moves the highlight one item at a time with the arrow keys" (`focusModel: 'virtual'`) (L4 for the keyboard-navigation and `popupListConformanceTests` citations).
- **CMB-ARIA-04** — MUST. `aria-autocomplete` is `"none"` when readOnly and otherwise the engine's `autoComplete` mode, which for `Combobox.Root` is always `"list"`.
  Source: `combobox/root/AriaCombobox.tsx:140,1363`. Test: [L2] `ComboboxRoot.test.tsx` "should expose aria-autocomplete=\"none\" when readOnly", "sets all aria attributes on the input when closed".
- **CMB-ARIA-05** — MUST. When the control element is not a native text input (e.g. `render={<textarea />}`), the combobox role attributes (`role`, `aria-haspopup`, `aria-expanded`, `aria-controls`, `aria-autocomplete`, `aria-activedescendant`) are applied only while expanded (open or inline), so a closed textarea keeps its native semantics.
  Source: `combobox/root/AriaCombobox.tsx:1346-1379`. Test: [L2] `ComboboxInput.test.tsx` "applies combobox aria attributes to a <textarea> only while the popup is open", "applies combobox aria attributes to an inline <textarea> before it is interacted with".
- **CMB-ARIA-06** — MUST. An Input inside the popup receives no Field validation attributes (`aria-invalid`, `aria-describedby`), reports the default field state, and is not the labelable control (FFI-ARIA-10); the Trigger is the form control in that layout.
  Source: `combobox/input/ComboboxInput.tsx:98,127-129,428-434`. Test: [L2] `ComboboxRoot.test.tsx` "does not apply validation ARIA attributes to input inside popup", "focuses trigger and surfaces errors when input is inside popup".
- **CMB-ARIA-07** — MUST. Trigger ARIA: `role="combobox"` only when the input is inside the popup; `aria-expanded="true|false"` always; `aria-haspopup="dialog"` when the input is inside the popup and `"listbox"` otherwise; `aria-controls` = the popup id while open with the input inside, the list element id while open otherwise, absent when closed; `aria-required`/`aria-readonly` only when the input is inside the popup (the outside Input already carries them); `aria-labelledby` = Field label id ?? Combobox.Label id (FFI-ARIA-07, resolved only after hydration — the server HTML has no link). Server-rendered output already contains `role`, `aria-expanded`, `aria-haspopup` for the inside-popup layout.
  Source: `combobox/trigger/ComboboxTrigger.tsx:89-101,155-166`. Test: [L2] `ComboboxTrigger.test.tsx` "sets all aria attributes on the trigger when open", "sets aria-required attribute when required (input inside popup)", "does not set aria-required attribute when the input is outside the popup", "sets aria-readonly attribute when readOnly (input inside popup)", "does not set aria-readonly attribute when the input is outside the popup"; `ComboboxRoot.test.tsx` "sets combobox aria attributes on the trigger when input is inside popup", "does not link Combobox.Label to trigger before hydration", "sets aria-controls to the dialog popup on trigger"; `popupConformanceTests.tsx` "has the `aria-controls` attribute on the trigger", "has the `aria-haspopup` attribute on the trigger" (L4 for the `popupConformanceTests` citations).
- **CMB-ARIA-08** — MUST. The List has `role="listbox"` (`"grid"` when `grid`), `aria-multiselectable="true"` in multiple mode, `aria-readonly="true"` when readOnly *except* in grid mode, and never `aria-orientation` (CMP-DOM-04).
  Source: `combobox/list/ComboboxList.tsx:78-82`. Test: [L2] `ComboboxList.test.tsx` "sets role=listbox and aria-multiselectable in multiple mode", "sets aria-readonly on the listbox when the root is readOnly", "does not set aria-readonly on the grid when the root is readOnly", "does not set aria-readonly on the listbox by default"; `ComboboxRoot.test.tsx` "does not render aria-orientation on the listbox role", "does not render aria-orientation on the grid role"; `popupConformanceTests.tsx` "has the listbox role on the popup" (L4 for the `popupConformanceTests` citations).
- **CMB-ARIA-09** — MUST. An Item has `role="option"` (`"gridcell"` inside a `Row`) and `aria-selected="true|false"`; disabled state is exposed per the composite button rules (UBT-ARIA-05: `aria-disabled` and, in native mode, `disabled`).
  Source: `combobox/item/ComboboxItem.tsx:163-170`. Test: [L2] `ComboboxRoot.test.tsx` "sets correct attributes on the item when highlighted", "sets grid roles when grid is enabled and rows are used"; `ComboboxItem.test.tsx` "reflects selected value with aria-selected when reopening"; `popupListConformanceTests.tsx` "marks only the selected item as selected" (L4 for the `popupListConformanceTests` citations).
- **CMB-ARIA-10** — MUST. A Group has `role="group"` (`"rowgroup"` when `grid`) and `aria-labelledby` = its GroupLabel's id (CMB-STA-16); a GroupLabel has an `id` (consumer's or generated) and `aria-hidden="true"` by default (overridable); a Row has `role="row"`.
  Source: `combobox/group/ComboboxGroup.tsx:35-45`; `combobox/group-label/ComboboxGroupLabel.tsx:32-36`; `combobox/row/ComboboxRow.tsx:20-24`. Test: [L2] `ComboboxGroup.test.tsx` "should render group with label", "should associate label with group"; `ComboboxGroupLabel.test.tsx` "wires to group aria-labelledby", "is hidden from the accessibility tree by default", "allows overriding aria-hidden", "uses provided id in aria-labelledby"; `ComboboxRoot.test.tsx` "renders groups with the rowgroup role".
- **CMB-ARIA-11** — MUST. The Popup has `role="dialog"` when the input is inside it and `role="presentation"` otherwise; the Positioner, Backdrop and Separator have `role="presentation"`; the Arrow, Icon and ItemIndicator have `aria-hidden="true"`.
  Source: `combobox/popup/ComboboxPopup.tsx:90`; `combobox/icon/ComboboxIcon.tsx:22`; `combobox/arrow/ComboboxArrow.tsx:40`; `utils/ItemIndicator.tsx:33`. Test: [L2] `ComboboxPopup.test.tsx` "sets role to presentation when input renders outside the popup", "sets role to dialog when input renders inside the popup".
- **CMB-ARIA-12** — MUST. `Chips` has `role="toolbar"` only while at least one value is selected (so NVDA stays in focus mode) and no role otherwise; a `Chip` has `aria-disabled="true"` when the Root is disabled and `aria-readonly="true"` when readOnly; `InputGroup` has `role="group"`.
  Source: `combobox/chips/ComboboxChips.tsx:31-34`; `combobox/chip/ComboboxChip.tsx:109-111`; `combobox/input-group/ComboboxInputGroup.tsx:59`. Test: [L2] `ComboboxChips.test.tsx` "does not set role=\"toolbar\" when there are no chips", "sets role=\"toolbar\" when there is at least one chip"; `ComboboxChip.test.tsx` "should render aria-disabled attribute when disabled", "should render aria-readonly attribute when readOnly"; `ComboboxInputGroup.test.tsx` "has role prop".
- **CMB-ARIA-13** — MUST. `Status` and `Empty` are live regions: `role="status"`, `aria-live="polite"`, `aria-atomic="true"` (overridable). Both apply the initial-live-region text mutation (UTL-A11Y-01..04): on mount, the element's first non-empty text node is suffixed with a word-joiner marker and restored 200 ms later (or immediately on unmount within the delay), skipped on iOS, so screen readers announce content present at mount; content changed after mount is announced immediately. A custom `render` element is preserved.
  Source: `combobox/status/ComboboxStatus.tsx:24-36`; `combobox/empty/ComboboxEmpty.tsx:32-46`; `internals/useInitialLiveRegionTextMutation.ts`. Test: [L2] `ComboboxStatus.test.tsx` "removes the initial text mutation after the reset delay", "updates content immediately after the live region has mounted", "preserves a custom render prop on the visible element", "restores the marker before unmounting during the reset delay"; `ComboboxStatus.iOS.test.tsx` "skips the initial text mutation"; `ComboboxEmpty.test.tsx` "removes the initial text mutation after the reset delay", "updates the live region immediately when the empty content appears after mount", "preserves a custom render prop on the visible element".
- **CMB-ARIA-14** — MUST. The hidden form input is `aria-hidden` with `tabindex="-1"`; the internal dismiss buttons are labelled "Dismiss" and, being inside the focus manager's inside set, remain reachable to assistive technology while the popup is open.
  Source: `combobox/root/AriaCombobox.tsx:1701-1702`; `combobox/utils/ComboboxInternalDismissButton.tsx:40-42`; `combobox/popup/ComboboxPopup.tsx:135-138`. Test: [L2] `ComboboxRoot.test.tsx` "renders internal dismiss buttons before the input and after the popup", "wraps modal focus from popup controls back to the input" (L4 for the focus-manager reachability clause).
- **CMB-ARIA-15** — MUST. With the input outside the popup the focus manager is modal (CMB-FOC-03) but runs in the *untrapped typeable combobox* variant (FLT-ARIA-01/DOM-15): outside content is marked inert for pointer/outside-press purposes, yet the InputGroup/Input subtree (the anchor) is never `aria-hidden`, and the Trigger is hidden from assistive technology while the popup is open in that layout (it is outside the control inside set).
  Source: `combobox/popup/ComboboxPopup.tsx:125-131`; `floating-ui-react/components/FloatingFocusManager.tsx:295`. Test: [L4] `ComboboxRoot.test.tsx` "does not aria-hide the input group when the input is outside the popup", "hides the trigger when popup is open with input outside the popup".
- **CMB-ARIA-16** — MUST. Labelling: `Combobox.Label` registers its id in the store (`labelId`) using the shared label behavior with fallback control id = the Trigger element's id, or the Root id when the input is inside the popup; the Trigger's `aria-labelledby` prefers a `Field.Label` id over the Combobox.Label id (FFI-ARIA-05/07) and is absent when neither exists; `Field.Label` targets the Trigger (registered labelable control) only in the input-inside-popup layout, the outside Input otherwise; a `Field.Description` is referenced through `aria-describedby` on the field control. Changing the Root id re-links the label.
  Source: `combobox/label/ComboboxLabel.tsx:32-63`; `combobox/trigger/ComboboxTrigger.tsx:89-91,166`; `combobox/input/ComboboxInput.tsx:181`. Test: [L2] `ComboboxRoot.test.tsx` "Field.Label", "Field.Description", "does not set fallback aria-labelledby when no label is rendered", "updates Combobox.Label linkage when root id changes", "Field.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id", "Combobox.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id".
- **CMB-ARIA-17** — MUST. Disabled/readOnly exposure: the Input renders the native `disabled` attribute when disabled (the Input is not a button; see §16 on `aria-disabled`), and `aria-readonly` plus `readonly` when readOnly; Trigger/Clear/ChipRemove follow UBT-ARIA-*; an Item exposes `aria-disabled`; the Trigger exposes `data-readonly`/`aria-readonly` only as stated in CMB-ARIA-07.
  Source: `combobox/input/ComboboxInput.tsx:176-180`; `combobox/trigger/ComboboxTrigger.tsx:162-165`. Test: [L2] `ComboboxInput.test.tsx` "should render aria-readonly and readonly attributes when readOnly"; `ComboboxTrigger.test.tsx` "should render aria-disabled attribute when disabled"; `ComboboxRoot.test.tsx` "should render disabled state on all interactive components".

## 6. Interactions

All list navigation is *virtual*: DOM focus stays on the Input (or the Trigger when nothing is open) and the highlighted item is reported through `aria-activedescendant`. Keyboard and pointer mechanics are the shared mechanisms in [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) and [floating-and-positioning](../architecture/floating-and-positioning.md); this section states the options Combobox passes and the behaviors layered on top.

### 6.1 Keyboard

- **CMB-KEY-01** — MUST. List-navigation configuration (CMP): `enabled: !disabled`, `id` = Root id, `virtual: true`, `loopFocus` = prop, `allowEscape: loopFocus && !autoHighlight`, `focusItemOnOpen: 'auto'` except `false` when a query was typed since open, `focusItemOnHover` = `highlightItemOnHover`, `resetOnPointerLeave: true` (the engine's `keepHighlight` is `false`), `orientation: 'horizontal'` with `grid` navigation when `grid` (else vertical), `rtl` from the direction context, `disabledIndices: []` (disabled items stay navigable), and `onNavigate` that ignores automatic navigation while closed (except an inline reset to `null`) and while the transition status is `'ending'`, otherwise calling `setIndices({ activeIndex, type: getHighlightReason(event), event })`. The reference prop bag goes to the Input (merged with the dismiss/click/role reference props), the floating bag to the List, and the item bag (with `onFocus` removed) to each Item. Typeahead is not enabled on the open list (`typeahead: false` in the conformance suite).
  Source: `combobox/root/AriaCombobox.tsx:1416-1501`. Test: [L4] `popupListConformanceTests` (§13, `focusModel: 'virtual'`, `homeEnd: false`, `typeahead: false`, `listEnd: 'escape'`); `ComboboxRoot.test.tsx` "should report arrow-key highlights as keyboard-driven when readOnly", "does not navigate kept-mounted items while controlled closed".
- **CMB-KEY-02** — MUST. <kbd>ArrowDown</kbd> / <kbd>ArrowUp</kbd> on the Input: while closed, opens the popup with reason `list-navigation` and highlights the selected item (first selected item in rendered order in multiple mode), else the first (ArrowDown) or last (ArrowUp) item (CMP-KEY-22, CMP-STA-33/37); while open, moves the highlight one navigable item (CMP-KEY-32) and scrolls it into view; the default scrolling of navigation keys is prevented. After a typed query with `autoHighlight`, ArrowDown continues from the auto-highlighted first item. With a pointer-opened popup the first ArrowDown highlights item 0.
  Source: `combobox/root/AriaCombobox.tsx:1416-1450`; CMP. Test: [L4] `ComboboxRoot.test.tsx` "focuses first item on ArrowDown and last item on ArrowUp", "opens, navigates with ArrowDown, and Enter selects", "anchors to the first selected item when opened with the keyboard", "continues arrow navigation from the anchor rather than the top of the list", "navigates on first ArrowDown after editing selection to a new matching query", "continues ArrowDown navigation from the Enter-selected item (multiple mode)", "should browse the items with the arrow keys when readOnly"; `popupListConformanceTests.tsx` "opens on the first item with ArrowDown", "opens on the last item with ArrowUp", "opens on the selected item with %s", "moves the highlight one item at a time with the arrow keys", "starts without a highlight after a pointer opening", "prevents the default scrolling of navigation keys", "opens and handles keys without losing focus when the list is empty", "opens past a disabled first item", "stops on a disabled item while navigating", "does not activate anything when every item is disabled".
- **CMB-KEY-03** — MUST. List ends: with `loopFocus` (default) and `autoHighlight` off, ArrowDown on the last item clears the highlight (virtual focus returns to the input; `onNavigate(null)`) and the next ArrowDown highlights the first item; ArrowUp mirrors this from the first item (CMP-KEY-33 `allowEscape`). With `loopFocus={false}` the highlight stays on the end item. With `autoHighlight` the list wraps directly (no escape to the input).
  Source: `combobox/root/AriaCombobox.tsx:1423-1424`. Test: [L4] `ComboboxRoot.test.tsx` "loops focus from last to first item with ArrowDown by default", "loops focus from first to last item with ArrowUp by default", "does not loop focus from last to first with ArrowDown when loopFocus={false}", "does not loop focus from first to last with ArrowUp when loopFocus={false}"; `popupListConformanceTests.tsx` "escapes past the first item", "escapes past the last item", "keeps a single item reachable".
- **CMB-KEY-04** — MUST. <kbd>Enter</kbd> on the Input while open: with a highlighted item, the event is prevented and stopped and a synthetic `click` is dispatched on the highlighted item element (with the keydown recorded as the selection event, so `onValueChange` receives the keyboard event), which runs the Item's `onClick` and selection (CMB-PTR-05); with no highlighted item, the popup closes with reason `none` and the event is *not* prevented so a surrounding form submits — except in inline mode, where nothing happens and the input text is kept. The List's own `keydown` applies the same Enter rule when focus is inside the list (nothing when no highlight; disabled/readOnly ignored). Enter while closed is not intercepted (native form submission).
  Source: `combobox/input/ComboboxInput.tsx:404-420`; `combobox/list/ComboboxList.tsx:83-99`; `combobox/utils/parts.ts:46-58`. Test: [L4] `ComboboxRoot.test.tsx` "Enter selects highlighted item" (`ComboboxItem.test.tsx`), "fires onOpenChange once with reason item-press on Enter selection", "submits on Enter when no item is highlighted (does not prevent)", "prevents submit on Enter when an item is highlighted", "keeps input value on Enter when inline and no item is highlighted", "Enter selects with manual indices provided to items"; `ComboboxList.test.tsx` "does not prevent Enter when no item is highlighted", "selects the highlighted item with Enter", "ignores Enter when interaction is disabled: %o"; `popupListConformanceTests.tsx` "activates the highlighted item with {Enter}", "does not submit a surrounding form", "activates the hovered item with Enter"; `parts.test.ts` "clicks the rendered highlighted item with the originating event", "does nothing when the highlighted item is not rendered".
- **CMB-KEY-05** — MUST. <kbd>Escape</kbd> while the popup is *not mounted* (closed, not inline-dialog) on the Input: sets the input text to `''` and the selection to `[]` (multiple) / `null` (single), both with reason `escape-key` (`onInputValueChange` first, then `onValueChange`); when there was something to clear, the Root is not inline and the consumer did not call `allowPropagation()`, the event's propagation is stopped (a parent dialog does not close); when the selection was already empty, or inline, the event propagates (EVD-DIS-03). While open, Escape is handled by the dismiss mechanism (CMB-DIS-02). Server: relaxed, see §9.
  Source: `combobox/input/ComboboxInput.tsx:351-367`. Test: [L4] `ComboboxInput.test.tsx` "should move focus to clear button when pressing Escape and popup is closed", "clears a closed multiple value on Escape and stops propagation", "lets Escape propagate when a closed multiple value is already empty", "lets Escape propagate from a closed inline combobox".
- **CMB-KEY-06** — MUST. <kbd>Home</kbd> / <kbd>End</kbd> on the Input are consumed (prevented and stopped — list navigation does not handle them for a typeable reference, CMP-KEY-28) and move the caret to the start/end of the text, also resetting the horizontal scroll (`scrollLeft = 0` for Home; `±(scrollWidth − clientWidth)` for End, negative in RTL). On Gecko in RTL the caret positions are swapped (Home → end index, End → index 0). Modifier-key combinations are not intercepted.
  Source: `combobox/input/ComboboxInput.tsx:319-349`. Test: [L4] `ComboboxInput.test.tsx` "pressing Home moves caret to start", "pressing End moves caret to end", "scrolls to the start and end when pressing Home/End on overflowing input"; `ComboboxInput.gecko.test.tsx` "uses Gecko RTL caret positions for Home and End"; `popupListConformanceTests` (`homeEnd: false`).
- **CMB-KEY-07** — MUST. <kbd>Backspace</kbd> on an empty Input in multiple mode with at least one selected value (and a Chips part mounted) removes the last *rendered* chip's value (the last selected value when no chips are rendered) with reason `none`, after clearing the highlight when the removed item was highlighted; the popup state is unchanged.
  Source: `combobox/input/ComboboxInput.tsx:369-391`. Test: [L4] `ComboboxInput.test.tsx` "removes the last rendered chip when pressing Backspace in an empty input", "removes the last selected value when no chips are rendered", "removes the last chip after clicking from a chip back to the input", "removes the last chip after leaving a chip and tabbing back to the input", "removes the last chip after tabbing from a chip back to the input"; `ComboboxRoot.test.tsx` "keeps the active item highlighted after clearing the last selected value", "clears active highlight when removing the highlighted chip item", "clears highlight after removing the highlighted chip while filtering (multiple mode)".
- **CMB-KEY-08** — MUST. <kbd>ArrowLeft</kbd> (LTR; <kbd>ArrowRight</kbd> in RTL) on the Input when the caret is at position 0 (a `null` selection start counts as 0) and values are selected moves DOM focus to the last rendered chip (prevented default); with no rendered chips nothing happens and focus stays on the input. Without a Chips part the keys are not intercepted.
  Source: `combobox/input/ComboboxInput.tsx:149-169,393-397`; `combobox/utils/parts.ts:28-32`. Test: [L4] `ComboboxInput.test.tsx` "focuses the last chip with ArrowLeft after tabbing from a chip back to the input", "keeps focus on the input when navigating toward chips but none are rendered", "treats a null selectionStart as the beginning of a custom input"; `ComboboxChip.test.tsx` "mirrors chip keyboard navigation in RTL mode".
- **CMB-KEY-09** — MUST. Keys on a focused Chip (ignored when disabled or readOnly): previous-chip key (ArrowLeft LTR / ArrowRight RTL) focuses the previous chip, or the input when on the first chip; next-chip key focuses the next chip, or the input when on the last; <kbd>Backspace</kbd>/<kbd>Delete</kbd> (prevented and stopped) clear the highlight and selected index (reason `keyboard`), remove the chip's value (reason `none`) synchronously, then focus the chip now at the previous index or the input when none remains; <kbd>Enter</kbd>/<kbd>Space</kbd> (prevented and stopped) focus the input; <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> (prevented and stopped) open the popup with reason `list-navigation` synchronously and focus the input (which may mount with the popup); an unmodified printable key focuses the input (so the character is typed there); modified printable keys leave focus on the chip. Only rendered chips are navigated.
  Source: `combobox/chip/ComboboxChip.tsx:38-98,112-124`; `combobox/utils/parts.ts:28-40`. Test: [L4] `ComboboxChip.test.tsx` "should handle keyboard navigation when enabled", "returns focus to the input for activation, vertical navigation, and text entry", "focuses a conditional input when reopening from a chip with %s", "leaves focus on a chip for modified printable keys", "handles Delete as a chip removal key", "focuses the next chip after removing a chip rendered from the value", "should handle deletion when enabled", "should navigate through only rendered chips", "should prevent keyboard navigation when disabled", "should prevent deletion when disabled", "should prevent deletion when readOnly", "should prevent navigation when readOnly and also prevent deletion"; `parts.test.ts` "returns no index after removing the only chip".
- **CMB-KEY-10** — MUST. <kbd>Enter</kbd>/<kbd>Space</kbd> on a ChipRemove remove its chip's value with reason `chip-remove-press` (clearing the highlight when the removed item was highlighted, with the keyboard reason) and focus the input; the event is prevented and stopped unless the consumer called `allowPropagation()`. Other keys are ignored. Disabled/readOnly ChipRemove ignores activation (UBT-STA-02).
  Source: `combobox/chip-remove/ComboboxChipRemove.tsx:53-91,108-115`. Test: [L4] `ComboboxChipRemove.test.tsx` "should handle keyboard activation", "removes a chip with Space but ignores unrelated keys", "allows click and keyboard events to propagate when requested", "ignores click and keyboard activation on a non-native button: %o", "keeps a different active item highlighted when removing a chip", "removes a filtered-out chip without clearing the visible highlight".
- **CMB-KEY-11** — MUST. <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> on the Trigger (prevented and stopped) open the popup with reason `list-navigation` and focus the Input; the Trigger handler has no exemption for any other element (the test "does not open on ArrowDown/ArrowUp when reference is a textarea" renders no Popup/List, so it only shows that no listbox appears, see §16). <kbd>Enter</kbd>/<kbd>Space</kbd> on a focused Trigger activate it as a button (toggle, CMB-PTR-03) without highlighting an item.
  Source: `combobox/trigger/ComboboxTrigger.tsx:247-256`. Test: [L4] `ComboboxTrigger.test.tsx` "opens popup when pressing ArrowDown or ArrowUp", "does not open on ArrowDown/ArrowUp when reference is a textarea", "opens on $name when readOnly"; `popupListConformanceTests.tsx` "opens with %s without activating an item" (`enterSpaceOpen: false`).
- **CMB-KEY-12** — MUST. Closed-trigger typeahead: while the popup is closed, single mode, not readOnly and not disabled, printable characters typed on the focused Trigger run the shared typeahead (CMP-STA-42..45) over the label registry starting from the selected index, and a match selects that value with reason `none` (the trigger shows it via `Combobox.Value`). Labels come from `items` (derived labels, refreshed by `forceMount()` on focus/press) or from mounted indexed items; sparse labels without a registered value never select. A later mount/unmount of the list or reordering of `items` keeps typeahead working from the current selection.
  Source: `combobox/trigger/ComboboxTrigger.tsx:109-122,169-177,201-202`. Test: [L4] `ComboboxTrigger.test.tsx` "selects item when typing on focused trigger (input inside popup)", "selects item when typing after the popup has been opened and closed (items prop, strict: %s)", "cycles to the next matching item when typing after open/close (no items prop, keepMounted %s)", "cycles from the selected item after reordering items while closed (no items prop)", "only matches mounted indexed item labels when typing on the focused trigger", "does not select when closed typeahead matches a sparse label without a value", "does not commit a value with typeahead on a closed trigger"; `createItems.test.tsx` "uses derived labels for closed trigger typeahead".
- **CMB-KEY-13** — MUST. Grid mode: the list navigation uses the grid navigator over the DOM rows (CMP-KEY-29, uneven rows supported, groups allowed between rows); <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> move across cells (mirrored in RTL) and <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> across rows; when no item is highlighted, ArrowLeft/ArrowRight are released to the input (caret movement) by preventing the Base UI handler; keys the grid does not handle do not re-report the highlight; an `autoHighlight` item surfaced before the first arrow key keeps grid navigation; typing <kbd>Space</kbd> in row mode types into the input without selecting.
  Source: `combobox/root/AriaCombobox.tsx:1429-1432,1456-1469`; `floating-ui-react/utils/gridNavigation.ts`. Test: [L4] `ComboboxRoot.test.tsx` "arrow keys navigate across rows and columns in grid mode", "does not re-report the highlight on keys the grid does not handle", "moves the input caret on ArrowLeft when no item is highlighted in grid mode", "moves the input caret on ArrowRight when no item is highlighted in grid mode", "keeps grid navigation when autoHighlight surfaces an item before typing arrow keys", "mirrors horizontal grid navigation in RTL mode", "supports uneven rows navigation", "supports uneven rows navigation within groups", "keeps gridcell typeahead active across Space in row mode without selecting".
- **CMB-KEY-14** — MUST. Modifier and IME handling on the Input: a keydown with Ctrl/Shift/Alt/Meta is not intercepted by the Input handler (the popup stays open after modified arrow navigation); a keydown with `which === 229` (IME composition) is ignored after the chip handling (so an Enter that commits IME text does not select and arrow keys that pick a candidate do not move the highlight); composition start/end are tracked (not on Android) and the composition end commits the value with reason `input-change`, treated as typed input (CMB-STA-21) — an auto-highlight applies after composition; during composition, controlled updates are withheld (the composing value is shown locally), an emptied composition closes the popup with reason `input-clear` when `openOnInputClick` is false and the input is outside the popup, and a non-maintained highlight is cleared.
  Source: `combobox/input/ComboboxInput.tsx:100-101,223-287,319-321,400-402`; `combobox/root/AriaCombobox.tsx:679-681`. Test: [L4] `ComboboxInput.test.tsx` "keeps the popup open after modified keyboard navigation", "does not select an item for an IME keydown without a highlight", "closes on an empty composition update when input clicks do not open the popup", "clears the highlight on an empty composition update while staying open"; `ComboboxInput.android.test.tsx` "propagates changes during Android composition"; `ComboboxRoot.test.tsx` "does not select the highlighted item with an Enter that commits IME text", "does not move the highlight with arrow keys that pick an IME candidate", "highlights the first matching item after IME composition".
- **CMB-KEY-15** — MUST. readOnly: typing does not change the input (native `readonly`); arrow browsing still highlights (reported with reason `keyboard`); <kbd>Enter</kbd> on a highlighted item while open is prevented and stopped (no commit, no submit) and otherwise not intercepted. disabled: the Input handler ignores every key, list navigation is disabled, and the Trigger/ChipRemove ignore activation.
  Source: `combobox/input/ComboboxInput.tsx:323-329`; `combobox/root/AriaCombobox.tsx:1417`. Test: [L4] `ComboboxRoot.test.tsx` "should not change the input value when typing while readOnly", "should not submit the form with Enter on a highlighted item when readOnly", "should not commit a value with Enter on a highlighted item when readOnly", "should prevent keyboard interactions when disabled"; `ComboboxInput.test.tsx` "should not change the input value when typing while readOnly", "allows interactions when readOnly={false}".
- **CMB-KEY-16** — MUST. <kbd>Tab</kbd>/<kbd>Shift+Tab</kbd> are not intercepted: focus moves natively and the popup closes with reason `focus-out` (CMB-DIS-04); with the input inside the popup the modal trap wraps focus (CMB-FOC-09). <kbd>Space</kbd> on the Input types a space (never activates an item; `spaceActivates: false`).
  Source: `combobox/root/AriaCombobox.tsx:1394-1414`; CMP-KEY-34. Test: [L4] `ComboboxInput.test.tsx` "closes the popup when tabbing out"; `popupListConformanceTests.tsx` "closes on Tab without committing, and moves focus past the trigger", "closes on Shift+Tab without committing, and moves focus to the element before the popup", "activates the highlighted item with {Enter}" (Space variant excluded).
- **CMB-KEY-17** — MUST. Typing on the Input (a typed, non-autofill-like `input` event or composition commit) with a non-blank value opens the popup with reason `input-change` (unless readOnly/disabled) and, when `autoHighlight` is off, clears the current highlight so virtual focus returns to the input; with `autoHighlight` on, the highlight is kept and re-pointed at the first match (CMB-STA-26). In single mode with the input outside the popup, emptying the text clears the selection with reason `input-clear`, and with `openOnInputClick={false}` also closes the popup (reason `input-clear`). Programmatic `input` events without an `inputType` do not open; paste (`insertFromPaste`) does.
  Source: `combobox/input/ComboboxInput.tsx:239-317`. Test: [L4] `ComboboxInput.test.tsx` "should open popup on typing when enabled", "clears selected value when input text is cleared (single selection)"; `ComboboxRoot.test.tsx` "does not open on programmatic input events", "opens on paste input events", "does not open on input click when false, but opens on typing", "highlights the first matching item after typing (single mode)", "reports no keyboard reason when typing clears the highlight", "keeps highlight in sync after selecting then backspacing to a single match", "does not shift highlight to the previous selected item on Enter deselect".

### 6.2 Pointer and touch

- **CMB-PTR-01** — MUST. Input press: the click interaction is configured with `event: 'mousedown-only'`, `toggle: false`, `touchOpenDelay` = `0` when the input is inside the popup and `100` ms otherwise, `reason: 'input-press'`, enabled iff `!disabled && openOnInputClick` (FLT-PTR-02..05). A mousedown on the Input opens the popup (after the touch delay for touch pointers); a second press keeps it open; right clicks are ignored.
  Source: `combobox/root/AriaCombobox.tsx:1384-1392`. Test: [L4] `ComboboxInput.test.tsx` "fires with reason input-press when Input is clicked"; `ComboboxRoot.test.tsx` "keeps the popup open on a second input click by default", "should open the popup from the input and the trigger when readOnly"; `popupConformanceTests.tsx` "is called with the new open state and the change details" (`openReason: input-press`); `popupListConformanceTests.tsx` "ignores right clicks on the trigger and on items".
- **CMB-PTR-02** — MUST. Trigger press: the trigger's click interaction uses `event: 'mousedown'` (enabled iff the Root is not disabled; the Trigger's own disabled flag blocks the handlers) and toggles the popup with reason `trigger-press`. On mousedown (not disabled): when the input is outside the popup the Trigger becomes the floating DOM reference for this interaction; `forceMount()` runs so items are registered for the selection highlight; for non-touch pointers the Input is focused and, when the input is outside the popup, the default is prevented (the trigger never takes focus); when closing via trigger click with the input inside the popup, focus is not moved to the input. The Trigger tracks the pointer type from `pointerdown`/`pointerenter`.
  Source: `combobox/trigger/ComboboxTrigger.tsx:103-107,124-127,192-214`. Test: [L4] `ComboboxTrigger.test.tsx` "should toggle popup when enabled", "should call onOpenChange when toggling", "fires with reason trigger-press when Trigger is clicked", "should not open popup when disabled", "should not toggle when readOnly=false (control)", "opens the popup when readOnly"; `ComboboxRoot.test.tsx` "does not focus input when closing via trigger click (input inside popup)"; `popupListConformanceTests` (`triggerClickCloses: false` — the Input is the conformance "trigger").
- **CMB-PTR-03** — MUST. Drag-select and cancel-open (Trigger mousedown while closed, input inside the popup only): a one-shot document `mouseup` listener is attached; when the release target is inside the trigger, the positioner or the list, nothing happens (an item under the pointer commits via CMB-PTR-05); when the release is within the trigger's pseudo-element bounds plus 5 px (UTL-DOM-26) the popup stays open; otherwise the popup closes with reason `cancel-open`. A pending mouseup after the trigger unmounts is ignored. With the input outside the popup no listener is attached (drag-select still commits through the item mouseup).
  Source: `combobox/trigger/ComboboxTrigger.tsx:212-245`. Test: [L4] `ComboboxTrigger.test.tsx` "closes the popup when mouseup occurs outside the trigger bounds", "keeps the popup open when mouseup remains near the trigger bounds", "closes the popup when the release is more than 5px outside the trigger bounds", "ignores a pending mouseup after the trigger unmounts", "commits selection when the input is outside the popup", "commits selection when the input is inside the popup and the pointer is released over an item", "does not commit selection if the pointer never hovers the item", "does not double-commit when a non-primary touch lands on another item", "commits a later drag-select onto an item whose earlier pointerdown never released".
- **CMB-PTR-04** — MUST. Item pointer handling: `pointerdown` (capture) records the item as the pointer-down target when the pointer is primary and prevents the default (the Input keeps focus); `mousedown` also prevents the default (iOS synthetic mousedown); `click` commits the selection unless disabled/readOnly; `mouseup` commits the selection only when the primary button is released over a *highlighted* item on which the pointer did **not** go down (a drag-select from the trigger or another element) and clears the recorded pointer-down item. `commitSelection` calls `handleSelection(nativeEvent, value)` (synchronously flushed before `requestSubmit()` when `submitOnItemClick`, which Combobox.Root never sets). After a touch selection the Input remains focused.
  Source: `combobox/item/ComboboxItem.tsx:150-202`. Test: [L4] `ComboboxItem.test.tsx` "selects item and closes in single mode", "prevents default on mousedown so pointer selection does not steal input focus", "keeps the input focused after selecting an item with touch", "does not select disabled item", "calls onClick when clicked with a pointer"; `ComboboxRoot.test.tsx` "fires onOpenChange once with reason item-press on mouse click", "clicking on \"listbox\" keeps the focus on the input", "keeps the filter text when a drag-select release commits the selection"; `popupListConformanceTests.tsx` "activates the hovered item with Enter", "keeps the highlight when a touch scroll starts on the highlighted item".
- **CMB-PTR-05** — MUST. `handleSelection(event, value)` (reason `item-press`, with the recorded keyboard event substituted when Enter triggered the click): when the press target is inside an `<a href>`, the link handles the click — no selection; a `#`-prefixed href additionally closes the popup; an anchor without `href` behaves like a normal item. Single mode: `setSelectedValue(value)` then, unless canceled, `setOpen(false)`. Multiple mode: toggle membership (`isItemEqualToValue`), `setSelectedValue(next)`; unless canceled, when the input holds a non-blank query: with the input inside the popup clear it (reason `input-clear`, `isItemPress: true`) and remember the toggled value for highlight restoration; with the input outside the popup close the popup (reason `item-press`); with a blank query the popup stays open and the highlight stays on the toggled item.
  Source: `combobox/root/AriaCombobox.tsx:879-943`. Test: [L2] `ComboboxItem.test.tsx` "multiple mode toggles selection and stays open", "clicking a link inside an item does not select or close (anchor with href)", "clicking a hash link inside an item does not select and closes popup (anchor with #hash)", "clicking an anchor without href behaves like normal item (selects and closes)"; `ComboboxRoot.test.tsx` "should auto-close popup after selection when open state is uncontrolled", "\"multiple\" clears uncontrolled input after select when filtering", "does not close popup when filtering with input inside popup in multiple mode", "keeps the toggled item highlighted when deselecting a selected item", "clears the input with the \"input-clear\" reason and the item click event on pointer selection", "clears the input with the \"input-clear\" reason and the originating keydown event on keyboard selection", "marks the close-path clear as an item press only when an item press closed the popup (input outside popup)", "keeps the filter text when the \"item-press\" close is canceled (input outside popup)", "keeps the filter text when the selection clear is canceled (input inside popup)", "keeps the filter text in an inline list when the selection clear is canceled"; `createItems.test.tsx` "toggles derived values in multiple mode".
- **CMB-PTR-06** — MUST. Hover: with `highlightItemOnHover` (default) a `mousemove` over an item highlights it (reason `pointer`, the native `MouseEvent`); the pointer leaving the list clears the highlight (reason `pointer`, `PointerEvent`) because `resetOnPointerLeave` is on — except when the pointer type is touch or the input modality is keyboard (CMP-PTR-04/05); pointer highlighting can take the highlight back after keyboard navigation and keyboard navigation continues from the hovered item.
  Source: `combobox/root/AriaCombobox.tsx:1427-1428`. Test: [L4] `ComboboxRoot.test.tsx` "highlights an item on mouse move by default", "does not highlight items from mouse movement when disabled", "passes native mouse and pointer events for pointer highlights", "reports a pointer highlight after keyboard navigation"; `ComboboxItem.test.tsx` "updates the highlight on hover in a non-virtualized list"; `popupListConformanceTests.tsx` "highlights and focuses the hovered item", "clears the highlight when the pointer leaves the list without leaving focus on an item", "continues keyboard navigation from the hovered item", "lets the pointer take the highlight back after keyboard navigation".
- **CMB-PTR-07** — MUST. Clear press: `mousedown` prevents the default (no focus theft); `click` (ignored when disabled or readOnly) writes `''` to the input (reason `clear-press`), then sets the selection to `[]`/`null` (reason `clear-press`) and clears both `activeIndex` and `selectedIndex` with the highlight reason derived from the click (`pointer` for a mouse click, `keyboard` for a keyboard-generated click, UTL/EVD `getHighlightReason`), then focuses the Input. The popup is not closed (the Clear element is excluded from outside-press), and a popup Input regains focus.
  Source: `combobox/clear/ComboboxClear.tsx:98-131`. Test: [L4] `ComboboxClear.test.tsx` "click clears selected value and focuses input", "clears after pointer interaction marks keyboard navigation inactive", "does not dismiss the popup on click (outsidePress is blocked)", "does not dismiss a popup input when the clear button is rendered outside it", "is disabled when root disabled and does nothing on click", "when root is readOnly it does nothing on click", "clears selection without closing and restores popup input focus"; `ComboboxRoot.test.tsx` "reports no keyboard reason when the clear button clears the highlight", "resets selectedIndex when clearing all selections while open".
- **CMB-PTR-08** — MUST. Chips-area and InputGroup press (`mousedown`): skipped when the consumer called `preventBaseUIHandler()` (including from a nested Chip's `onMouseDown` or a `contextmenu`-triggered prevention); skipped when the target is not the element itself and is an interactive element (button, link, input, etc.) or — for InputGroup — inside the Chips container (so a chip press is not handled twice); otherwise the default is prevented and, unless disabled (Field or Root), the Input is focused and the popup opens with reason `input-press` when `openOnInputClick`. readOnly still focuses and opens. A press on the ChipRemove button is never treated as a chips-area press. The Input may be inside the popup, in which case the press opens the popup and the popup Input is focused.
  Source: `combobox/utils/handleInputPress.ts:8-38`; `combobox/chips/ComboboxChips.tsx:36-38`; `combobox/input-group/ComboboxInputGroup.tsx:60-64`. Test: [L4] `ComboboxChips.test.tsx` "focuses the input when clicking anywhere in the Chips area", "lets onMouseDown prevent the built-in focus and open behavior", "lets nested chip onMouseDown prevent the built-in focus and open behavior", "lets nested chip onContextMenu call preventBaseUIHandler", "does not treat chip remove presses as chips-area presses", "does not focus or open when disabled by Field.Root", "focuses the input and opens when readOnly", "opens and focuses an input rendered inside the popup when the chips area is pressed"; `ComboboxInputGroup.test.tsx` "focuses the input and opens when clicking input-group padding around chips", "does not handle chip presses a second time when chips are nested inside the input group", "focuses the input without opening when openOnInputClick is false", "does not focus or open when disabled by Field.Root", "focuses the input and opens when readOnly", "should not dismiss the popup when clicking inside the input group"; `handleInputPress.test.ts` "handles an event whose target is not an Element".
- **CMB-PTR-09** — MUST. Chip press: a mousedown on a Chip bubbles to the Chips handler (CMB-PTR-08), so an enabled or readOnly chip press focuses the Input (and opens when allowed); a disabled Root prevents it. Chips are excluded from outside-press dismissal, so pressing a chip never closes the popup through dismissal, but moving DOM focus onto a chip (e.g. via keyboard) closes it with reason `focus-out`.
  Source: `combobox/chips/ComboboxChips.tsx:36-38`; `combobox/root/AriaCombobox.tsx:1405-1413`. Test: [L4] `ComboboxChip.test.tsx` "should focus input on mouse down when enabled", "should prevent mouse interactions when disabled", "should prevent focus when disabled", "moves focus to the input when a chip is pressed while readOnly", "does not dismiss the popup when clicking a chip (outsidePress is blocked)", "closes the popup when a chip receives focus".
- **CMB-PTR-10** — MUST. ChipRemove click: `mousedown` prevents the default; `click` removes the chip's value (reason `chip-remove-press`), clears the highlight when the removed item was the highlighted one (highlight reason from the event), focuses the Input, and stops propagation unless `allowPropagation()` was called. The popup stays open and a popup Input (the chip rendered outside the popup) keeps focus.
  Source: `combobox/chip-remove/ComboboxChipRemove.tsx:53-107`. Test: [L4] `ComboboxChipRemove.test.tsx` "should remove chip on click when enabled", "should focus input after removing chip", "should keep the popup open while removing a chip", "keeps a popup input focused when removing a chip rendered outside the popup", "should prevent event propagation", "should not remove chip when disabled", "should not remove chip when readOnly", "should be focusable but not functional when readOnly".
- **CMB-PTR-11** — MUST. Touch: a touch-opened popup (open method `touch`) focuses the Popup element instead of the Input when the input is inside the popup (CMB-API-51) and the Popup's focus-redirect does not run (CMB-FOC-04); an outside *touch* press dismisses only intentionally (`touch: 'intentional'`, FLT-PTR-28/29) so the user can scroll the page; a modal popup locks page scroll on touch only when it spans nearly the viewport width (PSH-PTR-07).
  Source: `combobox/root/AriaCombobox.tsx:1396-1402`; `combobox/popup/ComboboxPopup.tsx:91-99,110-113`; `combobox/positioner/ComboboxPositioner.tsx:91-96`. Test: [L4] `ComboboxPopup.test.tsx` "focuses the popup instead of its input when opened by touch"; `ComboboxRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width", "does not apply scroll lock when a touch-opened popup is narrower than the viewport".
- **CMB-PTR-12** — MUST. Scroll reset on query change: a typed input change (non-virtualized) scrolls the nearest vertically scrollable ancestor of the list's first child (or the list) back to the top — walking overflow ancestors only within the Popup (or, with no popup, up to and excluding a `[role="dialog"]`), so a scrollable dialog around an inline list is never reset and only the nearest wrapper is reset; the reset happens before paint and is not deferred until hover; with `autoHighlight` the first item is kept in view; a reorder of retained items also resets.
  Source: `combobox/root/AriaCombobox.tsx:691-710`. Test: [L4] `ComboboxRoot.test.tsx` "resets the list scroll position to the top when the query changes", "resets manually filtered children without deferring the reset until hover", "resets the scroll position of a scrollable wrapper around the list", "skips clipping ancestors when finding the scroll container", "keeps the auto-highlighted first item in view after the query changes", "resets the scroll container when filtering reorders retained items", "resets only the nearest scrollable wrapper when composed in a scrollable dialog", "does not reset a surrounding dialog".
- **CMB-PTR-13** — MUST. Programmatic/scroll highlights: when the highlight is set programmatically after the filtered list changes, or restored to the selected item on open/clear, the item is scrolled into view (CMP-FOC-05, `scrollIntoView` under virtual focus), including through an externally virtualized list and when the input is inside the popup.
  Source: CMP-STA-35/ALG-23; `combobox/root/AriaCombobox.tsx:1141-1189`. Test: [L4] `ComboboxRoot.test.tsx` "highlights and scrolls the selected item into view on first open (no items prop)", "restores the highlight and scroll to the selected item when the query is cleared", "scrolls to programmatic highlights after the filtered list changes", "restores the selected item through an externally virtualized list", "scrolls the first selected item into view when clearing (input inside popup)"; `popupListConformanceTests.tsx` "reveals and highlights the selected item when opening", "scrolls the last item into view when opening with ArrowUp".

### 6.3 Focus management

- **CMB-FOC-01** — MUST. Items are never focusable (`tabindex` absent; the list-navigation `onFocus` item handler is stripped) and the List has `tabindex="-1"`; pointer presses on items/list prevent focus theft, so DOM focus remains on the Input during every interaction with the list.
  Source: `combobox/root/AriaCombobox.tsx:1487-1494`; `combobox/item/ComboboxItem.tsx:170-184`; `combobox/list/ComboboxList.tsx:76`. Test: [L4] `ComboboxRoot.test.tsx` "clicking on \"listbox\" keeps the focus on the input"; `popupListConformanceTests.tsx` "moves focus along with the highlight" (virtual: focus stays on the trigger/input).
- **CMB-FOC-02** — MUST. Tab order: the Input is the tab stop; the Trigger is `tabindex="-1"` when the input is outside the popup and `tabindex="0"` when inside; Clear, Chip and ChipRemove are `tabindex="-1"`; the hidden input is `tabindex="-1"`.
  Source: `combobox/trigger/ComboboxTrigger.tsx:157`; `combobox/clear/ComboboxClear.tsx:95`; `combobox/chip/ComboboxChip.tsx:109`; `combobox/chip-remove/ComboboxChipRemove.tsx:98`; `combobox/root/AriaCombobox.tsx:1701`. Test: [L2] `ComboboxTrigger.test.tsx` "sets tabIndex to -1 when not used as the main anchor"; `ComboboxChipRemove.test.tsx` "should have proper tab index".
- **CMB-FOC-03** — MUST. The Popup wraps its element in the shared focus manager (FLT) with: `disabled: !mounted`, `modal: !inputInsidePopup || modal`, `openInteractionType: openMethod`, `initialFocus`/`returnFocus` per CMB-API-51/52, and `getInsideElements` returning the two internal dismiss buttons. With the input outside the popup this is the *untrapped typeable combobox* variant (no inside guards, no `aria-hidden` on the anchor; CMB-ARIA-15); with the input inside the popup and `modal` false, focus is managed non-modally (focus-out closes, no trap).
  Source: `combobox/popup/ComboboxPopup.tsx:125-144`. Test: [L4] `ComboboxRoot.test.tsx` "wraps modal focus from popup controls back to the input", "does not aria-hide the input group when the input is outside the popup"; `ComboboxPopup.test.tsx` "honors initialFocus={false}".
- **CMB-FOC-04** — MUST. When focus lands on the Popup element itself or anywhere inside the List (e.g. a click on the listbox, or the focus manager's initial focus) and the popup was not opened by touch, focus is redirected to the Input; the focus marker props (PSH `FOCUSABLE_POPUP_PROPS`) make the popup focusable for that purpose.
  Source: `combobox/popup/ComboboxPopup.tsx:91-99`; `combobox/root/AriaCombobox.tsx:1477-1480`. Test: [L4] `ComboboxRoot.test.tsx` "clicking on \"listbox\" keeps the focus on the input", "does not navigate the list with arrow keys from portalled controls inside the popup".
- **CMB-FOC-05** — MUST. Focusing the hidden form input (native validation bubbles, autofill, `inputRef.current.focus()`) redirects focus: to the Trigger when the input is inside the popup, otherwise to the Input element, falling back to the Trigger when no Input is mounted (FFI-FOC-05); with no visible control nothing happens.
  Source: `combobox/root/AriaCombobox.tsx:1617-1625`. Test: [L4] `ComboboxRoot.test.tsx` "moves focus from the hidden control to an external input", "falls back to the trigger when the hidden control is focused", "falls back to the trigger after an external input unmounts", "moves hidden-control focus to the trigger when the input is inside the popup", "safely handles hidden-control focus without a visible control", "focuses trigger and surfaces errors when input is inside popup".
- **CMB-FOC-06** — MUST. Input-inside-popup selection flow: selecting with the keyboard keeps the popup Input focused until the popup closes, after which focus returns to the Trigger (focus manager return focus) and the next open starts with an empty query; a drag-select release over an item commits and returns focus to the trigger; after a chip removal from inside the popup the popup Input is refocused.
  Source: `combobox/popup/ComboboxPopup.tsx:118-123`; `combobox/chip-remove/ComboboxChipRemove.tsx:89`. Test: [L4] `ComboboxRoot.test.tsx` "selects with the keyboard, restores focus, and resets the query on reopen", "discards an uncommitted query on Escape and preserves the selection", "keeps the popup input focused through keyboard selection and restores the last selection", "closes the dialog after selecting an item and updates the trigger value"; `ComboboxChipRemove.test.tsx` "keeps a popup input focused when removing a chip rendered outside the popup".
- **CMB-FOC-07** — MUST. Chip focus: focusing a chip closes an open popup (focus-out) and the chip keeps focus; when focus returns to the Input (click, Tab, printable key, Enter/Space on a chip) no chip remains highlighted/focused and the chip list is navigable again from the input; opening the popup from a chip with ArrowDown/ArrowUp moves focus to the Input.
  Source: `combobox/chip/ComboboxChip.tsx:74-86,112-124`; `combobox/input/ComboboxInput.tsx:393-397`. Test: [L4] `ComboboxInput.test.tsx` "clears the chip highlight when focus returns to the input"; `ComboboxChips.test.tsx` "clears the highlighted chip when the popup opens"; `ComboboxChip.test.tsx` "closes the popup when a chip receives focus", "focuses a conditional input when reopening from a chip with %s".
- **CMB-FOC-08** — MUST. Field focus state: the Input (outside the popup) and the Trigger report `focused` to the Field on focus/blur through the shared focused-state hook (FFI), which also drops `data-focused` without a blur event when the focused control becomes disabled or unmounts. A Trigger blur whose `relatedTarget` is inside the Positioner is not a blur (no touched/validation); the Input blur marks the field touched, un-focused and commits `onBlur` validation; closing the popup by `focus-out`/`outside-press` with the input inside the popup marks touched, un-focused and validates. Focus moving into the popup does not mark the field touched.
  Source: `combobox/input/ComboboxInput.tsx:93,188-222`; `combobox/trigger/ComboboxTrigger.tsx:87,169-191`; `combobox/root/AriaCombobox.tsx:838-850`; `internals/field-root-context/useSetFieldFocused.ts`. Test: [L4] `ComboboxRoot.test.tsx` "[data-focused]", "is removed when the focused input becomes disabled", "is removed when the focused input unmounts", "is removed when the focused trigger becomes disabled", "is removed when the focused trigger unmounts", "does not mark as touched when focus moves into the popup", "validates when the popup is blurred", "validates the $name value when blurred" (`ComboboxTrigger.test.tsx`), "validates the autocomplete input when focus leaves its popup".
- **CMB-FOC-09** — MUST. Modal wrap (input outside the popup, or `modal`): tabbing forward from the last tabbable control inside the popup reaches the end internal dismiss button, and the focus guards/manager wrap focus back to the Input rather than leaving the page; Shift+Tab from the Input reaches the start dismiss button. With the input inside the popup and `modal` false, <kbd>Tab</kbd> leaves the popup and closes it.
  Source: `combobox/popup/ComboboxPopup.tsx:125-143`; `combobox/input/ComboboxInput.tsx:436-443`; FLT-KEY-04/05. Test: [L4] `ComboboxRoot.test.tsx` "wraps modal focus from popup controls back to the input", "renders internal dismiss buttons before the input and after the popup".
- **CMB-FOC-10** — MUST. Trigger focus: focusing an enabled Trigger schedules `forceMount()` after a 0 ms timeout (so a closed list registers labels for typeahead); the Field's focused state is set immediately.
  Source: `combobox/trigger/ComboboxTrigger.tsx:169-177`. Test: [L4] `ComboboxTrigger.test.tsx` "selects item when typing on focused trigger (input inside popup)", "cycles to the next matching item when typing after open/close (no items prop, keepMounted %s)".
- **CMB-FOC-11** — MUST. Dialog composition (inline list inside a Dialog): the combobox must not let a pending dialog initial-focus steal focus from an Input that becomes enabled asynchronously, and an unbound inline list unmounting with its dialog clears the highlight without emitting a stale item.
  Source: `combobox/root/AriaCombobox.tsx:1087-1093,1433-1442`. Test: [L4] `ComboboxRoot.test.tsx` "does not let pending dialog initial focus steal focus from an async-enabled combobox", "clears the highlight when an inline list in an unbound dialog unmounts".

### 6.4 Dismissal

- **CMB-DIS-01** — MUST. Dismiss configuration (FLT `useDismiss`): `enabled: !disabled && !inline`, `escapeKey` default (`true`), `outsidePressEvent: { mouse: 'sloppy', touch: 'intentional' }` (pen follows the mouse rule via the pointer-type lookup), `bubbles: true` when inline (unused since disabled) else default, and an `outsidePress` predicate that vetoes presses whose target is inside the Trigger, the Clear button, the Chips container or the InputGroup. The reference bag is on the Input and the floating bag on the Popup.
  Source: `combobox/root/AriaCombobox.tsx:1394-1414,1470,1478`. Test: [L4] `ComboboxInputGroup.test.tsx` "should not dismiss the popup when clicking inside the input group"; `ComboboxClear.test.tsx` "does not dismiss the popup on click (outsidePress is blocked)"; `ComboboxChip.test.tsx` "does not dismiss the popup when clicking a chip (outsidePress is blocked)".
- **CMB-DIS-02** — MUST. <kbd>Escape</kbd> while open closes the popup with reason `escape-key` without committing the typed query (the input is restored to the selection on unmount) and keeps focus on the Input; nested in a Dialog, only the combobox closes (FLT-KEY-02 bubbling rules). Before `onOpenChange` runs, propagation is pre-allowed when `items` is used, no item matches and no `Combobox.Empty` is mounted (so a parent popup can close when the list is hidden with CSS) — EVD-DIS-04; with an Empty part mounted, or when a consumer-rendered static list is empty, Escape does not bubble. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:765-775`. Test: [L4] `ComboboxRoot.test.tsx` "Escape closes the popup without committing when nothing highlighted", "bubbles Escape key when list is empty and popup hidden with CSS", "does not bubble Escape key when Empty component is present", "bubbles Escape key when rendered inline without Positioner/Popup", "discards an uncommitted query on Escape and preserves the selection"; `popupListConformanceTests.tsx` "closes on Escape without committing, and keeps focus on the trigger", "starts fresh when reopened after Escape", "closes only itself on Escape inside a dialog"; `popupConformanceTests.tsx` "is ignored when `open={true}` is passed, so Escape cannot close".
- **CMB-DIS-03** — MUST. Outside press closes the popup with reason `outside-press` without committing; presses on a plain wrapper around the Input (not the InputGroup) count as outside; presses on portalled content inside the Popup but outside the List, inside a nested popover opened from the popup, or on the excluded elements (CMB-DIS-01) do not close. In sloppy mode a mouse `pointerdown` outside closes immediately (FLT-PTR-26); touch requires an intentional tap (FLT-PTR-28).
  Source: `combobox/root/AriaCombobox.tsx:1394-1414`; FLT-DIS-*. Test: [L4] `ComboboxRoot.test.tsx` "dismisses the popup when clicking a plain wrapper around the input", "does not dismiss when pressing portalled content inside the popup but outside the list", "does not dismiss when pressing content inside a nested popover"; `popupListConformanceTests.tsx` "closes on an outside press without committing".
- **CMB-DIS-04** — MUST. Focus leaving the Input/Popup tree (Tab, Shift+Tab, focusing a chip, clicking a focusable outside element) closes the popup with reason `focus-out` (FLT-FOC-11, portal guards FLT-KEY-06/07); the typed query is discarded on unmount (CMB-STA-30). When the input is inside the popup this also marks the field touched and runs `onBlur` validation (CMB-FOC-08).
  Source: `combobox/root/AriaCombobox.tsx:838-850`; `combobox/popup/ComboboxPopup.tsx:125-131`. Test: [L4] `ComboboxInput.test.tsx` "closes the popup when tabbing out"; `ComboboxChip.test.tsx` "closes the popup when a chip receives focus"; `ComboboxRoot.test.tsx` "validates when the popup is blurred"; `popupListConformanceTests.tsx` "closes on Tab without committing, and moves focus past the trigger", "closes on Shift+Tab without committing, and moves focus to the element before the popup".
- **CMB-DIS-05** — MUST. Inline mode disables the dismiss interactions entirely: no Escape handling, no outside-press, no focus-out close; <kbd>Escape</kbd> on the input bubbles to the surrounding dialog.
  Source: `combobox/root/AriaCombobox.tsx:1395,1404`. Test: [L4] `ComboboxRoot.test.tsx` "bubbles Escape key when rendered inline without Positioner/Popup"; `ComboboxInput.test.tsx` "lets Escape propagate from a closed inline combobox".
- **CMB-DIS-06** — MUST. A Trigger press while open closes the popup with reason `trigger-press` (toggle); an Input press never closes (toggle off).
  Source: `combobox/trigger/ComboboxTrigger.tsx:124-127`; `combobox/root/AriaCombobox.tsx:1387`. Test: [L4] `ComboboxTrigger.test.tsx` "should toggle popup when enabled"; `ComboboxRoot.test.tsx` "keeps the popup open on a second input click by default".
- **CMB-DIS-07** — MUST. Activating an internal dismiss button closes the popup with reason `close-press` and `trigger` = that button element.
  Source: `combobox/utils/ComboboxInternalDismissButton.tsx:25-34`. Test: none (covered structurally by "renders internal dismiss buttons before the input and after the popup").
- **CMB-DIS-08** — MUST. A `cancel-open` close (CMB-PTR-03) goes through `onOpenChange` like any other close and is cancelable; `imperative-action` closes come from `actions.close()` (CMB-API-36).
  Source: `combobox/trigger/ComboboxTrigger.tsx:240`; `combobox/root/AriaCombobox.tsx:1021`. Test: [L4] `ComboboxTrigger.test.tsx` "closes the popup when mouseup occurs outside the trigger bounds"; `ComboboxRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out".

## 7. Events and callbacks

Event-details shapes, `cancel()`, `allowPropagation()` and the reason vocabulary are specified in [event-details-and-reasons](../architecture/event-details-and-reasons.md); the Combobox rows of its §4.3.7 matrix are authoritative for *which* reason each callback emits. This section states the Combobox-specific contracts.

| Callback | Signature | Details type | Reasons emitted |
|---|---|---|---|
| `onOpenChange` | `(open: boolean, details)` | `OpenChangeEventDetails` (`ChangeEventDetails` + `preventUnmountOnClose()`) | `trigger-press`, `input-press`, `outside-press`, `escape-key`, `item-press`, `close-press`, `list-navigation`, `focus-out`, `input-change`, `input-clear`, `cancel-open`, `imperative-action`, `none` |
| `onValueChange` | `(value, details)` | `ChangeEventDetails` | `item-press`, `input-clear`, `escape-key`, `clear-press`, `chip-remove-press`, `none` |
| `onInputValueChange` | `(inputValue: string, details)` | `ChangeEventDetails` (+ `isItemPress?`) | `input-change`, `input-clear`, `clear-press`, `escape-key`, `item-press` (fill after selection), `none` |
| `onItemHighlighted` | `(value \| undefined, details)` | `HighlightEventDetails` `{ reason, event, index }` | `keyboard`, `pointer`, `none` |
| `onOpenChangeComplete` | `(open: boolean)` | — | — |
| `Item.onClick` | native click handler | — | — |

- **CMB-EVT-01** — MUST. `onOpenChange` receives `OpenChangeEventDetails`: the shared change details (`reason`, `event`, `trigger`, `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed`, EVD-API-01) plus `preventUnmountOnClose()` (EVD-API-09, TRN-EVT-07); `reason` is one of the 15 literals of `Combobox.Root.ChangeEventReason` (the union is shared by the three change callbacks; `clear-press`/`chip-remove-press` are declared but never emitted for open changes). The callback runs synchronously inside the user event before any state is committed; for a Trigger press `trigger` is the trigger element.
  Source: `combobox/root/AriaCombobox.tsx:777-779,2019-2038`. Test: [L2] `ComboboxRoot.test.tsx` "fires when opening and closing"; `popupConformanceTests.tsx` "is called with the new open state and the change details" (L4 for the `popupConformanceTests` citations).
- **CMB-EVT-02** — MUST. `onOpenChange` reason matrix (EVD §4.3.7): `trigger-press` (Trigger press), `input-press` (Input/InputGroup/Chips press), `input-change` (typing opens), `list-navigation` (ArrowDown/ArrowUp on Input, Trigger or Chip), `item-press` (selection closes: single mode, or multiple mode while filtering with the input outside; `#` links), `escape-key`, `outside-press`, `focus-out` (dismiss), `close-press` (internal dismiss button), `input-clear` (input emptied with `openOnInputClick={false}` outside the popup; emptied composition), `cancel-open` (trigger mouseup outside bounds), `imperative-action` (`actions.close()`), `none` (Enter with no highlight). Exactly one `onOpenChange(false, item-press)` call happens per item selection, after `onValueChange`.
  Source: as listed in §6; `combobox/root/AriaCombobox.tsx:879-943`. Test: [L4] `ComboboxRoot.test.tsx` "fires onOpenChange once with reason item-press on mouse click", "fires onOpenChange once with reason item-press on Enter selection"; `ComboboxTrigger.test.tsx` "fires with reason trigger-press when Trigger is clicked"; `ComboboxInput.test.tsx` "fires with reason input-press when Input is clicked"; `ComboboxTrigger.test.tsx` "closes the popup when mouseup occurs outside the trigger bounds".
- **CMB-EVT-03** — MUST. `onValueChange` reasons: `item-press` (selection/toggle; `event` is the click, or the originating keydown for Enter), `input-clear` (single mode, input outside popup, text emptied by typing), `escape-key` (Escape while closed), `clear-press` (Clear button), `chip-remove-press` (ChipRemove), `none` (Backspace in empty input, Backspace/Delete on a chip, closed-trigger typeahead, browser autofill). In multiple mode the value passed is a new array (never the previous instance); in single mode `null` when cleared. `onValueChange` is never called for a no-op (e.g. a rejected autofill or a canceled selection).
  Source: `combobox/root/AriaCombobox.tsx:854-864,884,1635,1672`; `combobox/input/ComboboxInput.tsx:297-302,357-360,386-389`; `combobox/clear/ComboboxClear.tsx:113-117`; `combobox/chip-remove/ComboboxChipRemove.tsx:79-87`; `combobox/chip/ComboboxChip.tsx:69-72`; `combobox/trigger/ComboboxTrigger.tsx:116-121`. Test: [L2] `ComboboxRoot.test.tsx` "should handle multiple selection", "should handle browser autofill", "does not update field state when autofill is canceled"; `ComboboxChipRemove.test.tsx` "should remove chip on click when enabled"; `ComboboxClear.test.tsx` "click clears selected value and focuses input" (L4 for the keyboard-originated reasons).
- **CMB-EVT-04** — MUST. `onInputValueChange` reasons: `input-change` (typing; composition end); `input-clear` (multiple-mode clear after a selection with the input inside the popup — `isItemPress: true`; multiple-mode clear on close with the input outside — `isItemPress` = whether an item press closed it; unmount cleanup clears and interrupted-reopen clears — no `isItemPress` property; single mode with the input inside the popup on unmount; single mode outside when the selected label is empty on unmount); `clear-press`; `escape-key`; the selected-value change's own reason (`item-press`, `none`, `escape-key`, `clear-press`, `chip-remove-press`, `input-clear`) when the input is filled with the selected label after a selection (single mode, input outside the popup — `shouldFillInput`); `none` for derived re-syncs (CMB-STA-20), the popup-input initial blank, and unmount re-sync to a non-empty label. Autofill of the hidden input in single mode resolves to a selection (not an input write).
  Source: `combobox/root/AriaCombobox.tsx:664,753,823-829,866-875,918-923,966-991,1287`; `combobox/input/ComboboxInput.tsx:117,234-237,289-290,359`; `combobox/clear/ComboboxClear.tsx:108-111`. Test: [L2] `ComboboxRoot.test.tsx` "clears the input with the \"input-clear\" reason and the item click event on pointer selection", "clears the input with the \"input-clear\" reason and the originating keydown event on keyboard selection", "marks the close-path clear as an item press only when an item press closed the popup (input outside popup)", "does not re-emit a clear when closing again without typing (multiple, input outside popup)", "reports a single input value change when the controlled value changes" (L4 for the keyboard-originated reasons).
- **CMB-EVT-05** — MUST. `onItemHighlighted(value, { reason, event, index })` fires on every highlight change: `keyboard` for arrow-key/typeahead moves and keyboard-driven clears, `pointer` for hover (`MouseEvent`) and pointer-leave (`PointerEvent`) changes, `none` for typing, auto-highlight, list changes, open/close and programmatic clears (e.g. the Clear button when activated by keyboard reports `keyboard`; by pointer, `pointer` — EVD-EVT-24, `getHighlightReason`). `value` is `undefined` and `index` is `-1` when the highlight is cleared; clearing an already-clear highlight does not fire. On mount of an inline virtualized list the selected item's index is reported.
  Source: `combobox/root/AriaCombobox.tsx:606-653,1198-1227`; `utils/getHighlightReason.ts`. Test: [L4] `ComboboxRoot.test.tsx` "fires on keyboard navigation", "passes native mouse and pointer events for pointer highlights", "reports a pointer highlight after keyboard navigation", "reports no keyboard reason when typing clears the highlight", "reports no keyboard reason when the clear button clears the highlight", "fires a single clearing highlight on Enter selection", "emits the selected item index to onItemHighlighted on mount when inline and virtualized (items prop)"; `ComboboxChipRemove.test.tsx` "records pointer-origin removal when clearing the highlighted item".
- **CMB-EVT-06** — MUST. `onOpenChangeComplete(true)` is called once per open when the Popup's enter animations finish (immediately when none; TRN-EVT-01) and `onOpenChangeComplete(false)` once per close from the unmount cleanup (first step of CMB-STA-30), after exit animations and any `preventUnmountOnClose` hold — exactly once even when `unmount()` is called twice in one batch. It is not called on mount while closed, and not for inline lists without a dialog. Server: relaxed, see §9.
  Source: `combobox/popup/ComboboxPopup.tsx:64-72`; `combobox/root/AriaCombobox.tsx:953,1005-1015`. Test: [L4] `ComboboxRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once", "keeps the popup mounted until the unmount action completes closing", "completes closing once when `unmount` is called twice in one batch"; `popupConformanceTests.tsx` "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation".
- **CMB-EVT-07** — MUST. Cancellation: `cancel()` in `onOpenChange` keeps the current open state (a canceled open leaves the popup closed and discards a pending typed highlight, EVD-EDGE-11; a canceled item-press close keeps the popup open *and* the filter text); `cancel()` in `onValueChange` keeps the selection and skips the input fill/clear/close that would follow (the highlight stays on the pressed item); `cancel()` in `onInputValueChange` keeps the text and skips that write's follow-ups (auto-highlight, scroll reset, close-side clears) — a canceled selection clear does not suppress the later unmount cleanup clear, and a canceled sync is retried. Controlled consumers must honor `isCanceled` themselves (EVD-EVT-06).
  Source: `combobox/root/AriaCombobox.tsx:659-664,783-792,860-862,908-910,936-938`. Test: [L2] `ComboboxRoot.test.tsx` "allows an attempted open to be canceled", "\"single\" keeps the popup open when selection is canceled", "keeps the active item when the post-selection input clear is canceled", "keeps the filter text when the \"item-press\" close is canceled (input outside popup)", "keeps the filter text when the selection clear is canceled (input inside popup)", "still clears the kept filter text once the popup closes", "does not run follow-up state changes after canceled input value changes", "does not clear popup input after canceled multiple value changes", "keeps the highlight on an item a controlled value refused to select", "discards the request when a rejected typed open is followed by a controlled open"; `popupConformanceTests.tsx` "keeps the popup closed when the open change is canceled" (L4 for the `popupConformanceTests` citations).
- **CMB-EVT-08** — MUST. `ChangeEventDetails.isItemPress` is present only on `input-clear` writes that the component attributes to a selection: `true` for the clear that follows an item press (input inside the popup, multiple mode); `true`/`false` on the close-path clear in multiple mode with the input outside the popup according to whether the close reason was `item-press`; absent on automatic cleanup clears.
  Source: `combobox/root/AriaCombobox.tsx:823-829,918-923,972,981,2039-2045`. Test: [L2] `ComboboxRoot.test.tsx` "marks the close-path clear as an item press only when an item press closed the popup (input outside popup)", "clears the input with the \"input-clear\" reason and the item click event on pointer selection".
- **CMB-EVT-09** — MUST. `allowPropagation()`: on an Escape-while-closed clear it lets the keydown propagate (CMB-KEY-05); on a ChipRemove activation it lets the click/keydown propagate (otherwise stopped) — in both cases the component checks `isPropagationAllowed` after the callbacks return (EVD-API-06). Server: relaxed, see §9.
  Source: `combobox/input/ComboboxInput.tsx:362-364`; `combobox/chip-remove/ComboboxChipRemove.tsx:102-115`. Test: [L4] `ComboboxInput.test.tsx` "clears a closed multiple value on Escape and stops propagation"; `ComboboxChipRemove.test.tsx` "allows click and keyboard events to propagate when requested", "should prevent event propagation".
- **CMB-EVT-10** — MUST. Event ordering for an item selection (single): `Item.onClick` (consumer) → `onValueChange(value, item-press)` → `onInputValueChange(label, item-press)` (outside popup) → `onOpenChange(false, item-press)` → render; for a multiple toggle while filtering with the input inside: `onValueChange` → `onInputValueChange('', input-clear, isItemPress)` → microtask highlight restore → `onItemHighlighted`.
  Source: `combobox/root/AriaCombobox.tsx:866-943`; `combobox/item/ComboboxItem.tsx:185-191,207`. Test: [L2] `ComboboxRoot.test.tsx` "fires onOpenChange once with reason item-press on mouse click", "keeps the popup open and highlights the newly selected item after clearing its query"; `ComboboxItem.test.tsx` "calls onClick when clicked with a pointer".
- **CMB-EVT-11** — MUST. Autofill events: a browser autofill `change` on the hidden input (not default-prevented, not disabled/readOnly) is handled in a microtask: single mode resolves the text case-insensitively against the serialized values first, then against rendered labels, and calls `onValueChange(match, none)` (nothing for no match); multiple mode ignores it; the popup does not open or filter (`insertReplacementText`/missing `inputType` on the visible input is also treated as autofill-like and does not open). Field dirty/validation runs only when the value actually changes; a canceled autofill leaves field state untouched.
  Source: `combobox/root/AriaCombobox.tsx:1627-1689`; `combobox/input/ComboboxInput.tsx:241-246`. Test: [L2] `ComboboxRoot.test.tsx` "should handle browser autofill", "should not auto-close during browser autofill", "ignores hidden-input autofill when $lockState $label" (×4), "shows all items when opening after browser autofill", "does not open or filter the popup on browser autofill with insertReplacementText", "should handle browser autofill with object values", "should handle browser autofill with object values when autofill uses the label", "matches browser autofill against an item rendered label for primitive values regardless of case", "matches browser autofill by serialized value before an earlier rendered label", "matches browser autofill when an earlier item has no rendered label", "matches browser autofill against an item rendered label when using the items prop", "marks the field dirty and validates after successful autofill", "does not update field state when autofill is canceled", "does not mark the field dirty when input autofill is canceled", "ignores scalar browser autofill in multiple mode", "ignores unmatched browser autofill in single mode"; `createItems.test.tsx` "matches browser autofill against %s" (×2).
- **CMB-EVT-12** — MUST. `Item.onClick` fires for pointer activation and for <kbd>Enter</kbd> on the highlighted item (through the synthetic click) with the click event; for a disabled item (Root or Item `disabled`) the shared button behavior suppresses the click, so neither the consumer `onClick` nor the selection runs (UBT-PTR-01; the Item passes `disabled` with `focusableWhenDisabled: true`); when readOnly the consumer `onClick` still runs but the built-in handler returns before `commitSelection`, so nothing is selected.
  Source: `combobox/item/ComboboxItem.tsx:137-142,185-191,207`; see UBT-PTR-01. Test: [L2] `ComboboxItem.test.tsx` "calls onClick when clicked with a pointer", "calls onClick when selected with Enter key (via root interaction)", "does not select the item when onClick prevents Base UI handler" (L4 for the Enter clause).

## 8. Form integration

The field/form mechanics (registration, validation modes, touched/dirty/filled, errors, FormData) are specified in [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md); its §6 control matrix has the Combobox row. Combobox-specific facts:

- **CMB-FRM-01** — MUST. The Root registers as the Field control with: control element = the Input (outside popup) or the Trigger (inside popup); raw value = `selectedValue`; stringified value = `itemToStringValue`-serialization (array of strings in multiple mode); enabled iff not disabled; name = the Root `name` prop (the Field's name takes precedence for the effective name, FFI-STA-10).
  Source: `combobox/root/AriaCombobox.tsx:551-560,578-591`. Test: [L2] `ComboboxRoot.test.tsx` "should receive name prop from Field.Root", "submits stringified value to onFormSubmit when itemToStringValue is provided", "submits when input renders a field-aware input", "submits when input inside popup renders a field-aware input".
- **CMB-FRM-02** — MUST. Native form participation goes through the hidden input(s) (CMB-DOM-18/19): single mode submits `name=<serialized value>`; multiple mode submits one entry per value; disabled submits nothing; `form` targets an external form; `required` triggers native validation on submit (`valueMissing`), which focuses the visible control through the hidden-input focus redirect and surfaces the error.
  Source: `combobox/root/AriaCombobox.tsx:1581-1610,1691-1704`. Test: [L4] `ComboboxRoot.test.tsx` "submits to an external form when `form` is provided", "submits multiple values to an external form when `form` is provided", "triggers native HTML validation on submit", "focuses trigger and surfaces errors when input is inside popup", "serializes a derived value in single mode" (`createItems.test.tsx`), "serializes derived values in multiple mode", "serializes the derived value with itemToStringValue".
- **CMB-FRM-03** — MUST. On every selected-value change (any reason, including autofill): external errors for the field name are cleared, `dirty` is recomputed as "selection differs from the field's initial value" using `isItemEqualToValue` element-wise and **order-sensitively** for arrays (an equal object with a different reference is not dirty; the same values in a different order are), and `validation.change(selectedValue)` runs (debounced/immediate per `validationMode`). The input text never affects dirty/validation in Combobox (only in Autocomplete's `none` mode).
  Source: `combobox/root/AriaCombobox.tsx:1291-1304,1315-1324`; `internals/itemEquality.ts` (`isSelectedValueDirty`). Test: [L2] `ComboboxRoot.test.tsx` "[data-dirty]", "removes [data-dirty] in multiple mode after returning to the initial value", "removes [data-dirty] when an equal object value is reselected (different reference)", "keeps [data-dirty] when the same values are reselected in a different order", "clears external errors on change", "prop: validationMode=onChange", "marks the field dirty and validates after successful autofill".
- **CMB-FRM-04** — MUST. `filled` is recomputed post-commit pre-paint as `selectedValue != null` (single) / non-empty array (multiple), including for an initially filled control. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:1244-1252`. Test: [L2] `ComboboxRoot.test.tsx` "adds [data-filled] attribute when filled", "adds [data-filled] attribute when already filled".
- **CMB-FRM-05** — MUST. `touched` is set on Input blur, on Trigger blur not into the popup, and when the popup closes by `focus-out`/`outside-press` with the input inside the popup; focus moving into the popup does not touch the field. `validationMode="onBlur"` commits validation at those same moments with the raw selected value; `onSubmit` validates only on submit; `validate(value)` receives the raw (unserialized) value.
  Source: `combobox/input/ComboboxInput.tsx:208-222`; `combobox/trigger/ComboboxTrigger.tsx:178-191`; `combobox/root/AriaCombobox.tsx:838-850`. Test: [L2] `ComboboxRoot.test.tsx` "[data-touched]", "does not mark as touched when focus moves into the popup", "validates when the popup is blurred", "prop: validate", "passes raw value to validate when itemToStringValue is provided", "prop: validationMode=onSubmit", "prop: validationMode=onBlur", "[data-invalid]", "[data-valid]"; `ComboboxTrigger.test.tsx` "validates the $name value when blurred" (L4 for the focus-out/outside-press clause).
- **CMB-FRM-06** — MUST. `Field.Root` `disabled` and `name` flow into the Root (CMB-API-25/30); `Field.Label` labels the Input (outside popup) or the Trigger (inside popup, when the trigger has the registered id); `Field.Description` is referenced by the control's `aria-describedby`; a `Combobox.Input` rendered as a field-aware input (`render={<Field.Control />}`-style) still submits.
  Source: `combobox/input/ComboboxInput.tsx:62,181`; `combobox/trigger/ComboboxTrigger.tsx:89-91`. Test: [L2] `ComboboxRoot.test.tsx` "should receive disabled prop from Field.Root", "Field.Label", "Field.Description", "Field.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id".
- **CMB-FRM-07** — MUST. Enter-key submission: <kbd>Enter</kbd> on the Input with no highlighted item lets the surrounding form submit (the keydown is not prevented, popup closes with `none`); with a highlighted item it is prevented (selection instead); readOnly with a highlight prevents it too. `requestSubmit()` is only used by the engine's `submitOnItemClick` (never for Combobox) and is a no-op when no form owns the inputs or `requestSubmit` is unavailable.
  Source: `combobox/input/ComboboxInput.tsx:404-420`; `combobox/root/AriaCombobox.tsx:945-950`. Test: [L4] `ComboboxRoot.test.tsx` "submits on Enter when no item is highlighted (does not prevent)", "prevents submit on Enter when an item is highlighted", "does not request form submission when no form owns the autocomplete", "does not request form submission when requestSubmit is unavailable"; `popupListConformanceTests.tsx` "does not submit a surrounding form".
- **CMB-FRM-08** — MUST. The hidden input's `autocomplete` attribute is the Root `autoComplete` prop; browser autofill resolution is CMB-EVT-11 (FFI-FRM-31).
  Source: `combobox/root/AriaCombobox.tsx:1694`. Test: [L2] `ComboboxRoot.test.tsx` "should pass autoComplete to the hidden input".

## 9. Lifecycle, mounting and animation

See [transitions-and-mounting](../architecture/transitions-and-mounting.md) for the status machine, `keepMounted`, `onOpenChangeComplete` timing and the unmount action; its tables list the Combobox root (own `useUnmountAfterClose`), `Combobox.Clear` and the shared `ItemIndicator` rows.

- **CMB-ANI-01** — MUST. The Popup is the animated element: `data-starting-style`/`data-ending-style` follow the shared status (TRN-DOM-01/02), the positioner/popup receive the disabled-mount transition styles while `'starting'` (TRN-DOM-05), and the closing phase waits for the Popup's animations (or the dialog's when inline) before `mounted` becomes `false` (TRN-ANI-18). Without `keepMounted` the Portal subtree is removed when unmounted; with `keepMounted` the Positioner stays in the DOM, `hidden`, `inert`-styled (`pointer-events: none`) and not navigable (PSH-ANI-14). Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:1005-1015`; `combobox/positioner/ComboboxPositioner.tsx:114-121`; `combobox/popup/ComboboxPopup.tsx:101`. Test: [L4] `popupConformanceTests.tsx` "removes the popup when there is no exit animation defined", "hides the kept-mounted popup once the exit animation finishes"; `ComboboxRoot.test.tsx` "does not navigate kept-mounted items while controlled closed".
- **CMB-ANI-02** — MUST. During the exit animation the list keeps rendering the *frozen* filtered result (close query, CMB-STA-21) and, with the input inside the popup, the input keeps its text until unmount so the content does not flash to the unfiltered list; a reopen that interrupts the exit releases the freeze (and clears the deferred popup input) before the first open frame.
  Source: `combobox/root/AriaCombobox.tsx:735-755,794-831`. Test: [L4] `ComboboxRoot.test.tsx` "keeps filtered popup content stable while closing with input inside popup", "keeps filtered popup content stable when input changes during the close animation", "keeps filtered content stable when a controlled close is deferred", "clears a single-select query and restores the selection when reopening during the close animation", "preserves a typed query when input reopens single-select during the close animation", "clears the deferred popup input when reopening during close animation".
- **CMB-ANI-03** — MUST. `preventUnmountOnClose()` + `actions.unmount()` implement an externally controlled exit (TRN-ANI-20..23): with the hold, the popup stays mounted after `open` becomes `false` until `unmount()` is called, at which point cleanup and `onOpenChangeComplete(false)` run once.
  Source: `combobox/root/AriaCombobox.tsx:757,777-778,833-835,1017-1024`. Test: [L2] `ComboboxRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing", "automatically unmounts with an actions ref and completes closing once", "unmounts when `close` and `unmount` are called in one batch".
- **CMB-ANI-04** — MUST. `Combobox.Clear` animates its visibility: `visible` drives its own transition status (TRN rows), so `data-starting-style` is present on the first frame after it becomes visible and `data-ending-style` while it is leaving; without `keepMounted` the element unmounts once its exit animations finish. Server: relaxed, see §9.
  Source: `combobox/clear/ComboboxClear.tsx:71-88,138-144`. Test: [L4] `ComboboxClear.test.tsx` "triggers enter animation via data-starting-style when becoming visible", "triggers exit animation via data-ending-style before unmount".
- **CMB-ANI-05** — MUST. `Combobox.ItemIndicator` transition status is driven by `selected`, but only a kept-mounted indicator animates deselection: without `keepMounted` the wrapper returns `null` as soon as `selected` becomes `false`, so the element is removed in the deselecting commit with no `data-ending-style` phase and no completion wait (TRN-ANI-01 exception); with `keepMounted` the same element stays in the DOM, plays the ending phase in place (`data-ending-style` until its animations finish; completion batched) and then simply loses the status attributes (TRN-DOM-07). Server: relaxed, see §9.
  Source: `combobox/item-indicator/ComboboxItemIndicator.tsx:18-25`; `utils/ItemIndicator.tsx:21,41-51`; see TRN-ANI-01, TRN-DOM-07. Test: [L4] `ComboboxItemIndicator.test.tsx` "mounts only while selected when keepMounted is false", "updates a mounted indicator when its item becomes unselected", "preserves the kept-mounted element across selection changes".
- **CMB-ANI-06** — MUST. Items/highlight during exit: list-navigation proposals are ignored while the status is `'ending'` (CMB-KEY-01), the highlight is cleared by the unmount cleanup, and a kept-mounted/portaled list is not navigable while closed.
  Source: `combobox/root/AriaCombobox.tsx:1437-1442,960`. Test: [L4] `ComboboxRoot.test.tsx` "does not navigate kept-mounted items while controlled closed"; `popupListConformanceTests.tsx` "starts fresh when reopened after Escape".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- CMB-ANI-01 — TRN-ANI-03/09/10 row: the unmount after the Popup's exit animation lands in the first render batch after the completion message (about two round trips); JS sets `hidden` at animation end (optimistic convergent write). TRN-STA-04/05/07/09 row: the `data-starting-style`/`data-ending-style` flips are met on both modes (JS-owned).
- CMB-ANI-04 — TRN-ANI-03/09/10 row: Clear's removal after its exit animation lands about two round trips later, hidden meanwhile by the convergent write; TRN-STA-04/05/07/09 row: the first-frame `data-starting-style` is met on both modes (JS-owned).
- CMB-ANI-05 — TRN-ANI-10 row: the kept-mounted indicator's batched completion commits in the first render batch after the completion message.
- CMB-EVT-06 — TRN-ANI-09 row: `onOpenChangeComplete(false)` and the unmount commit arrive about two round trips after the exit animation ends.
- CMB-STA-29 — TRN-ANI-03/09/10 row: `mounted` becomes `false` about two round trips after the exit animation ends; the convergent write hides the element meanwhile.
- CMB-STA-06 — RND-STA-01 mirrored-field row: `transitionStatus` reaches the state object, and so `ClassValue`/`StyleValue`, one round trip after JS writes the data attributes.
- CMB-STA-08 — RND-STA-01 mirrored-field row: `transitionStatus` reaches the state object, and so `ClassValue`/`StyleValue`, one round trip after JS writes the data attributes.
- CMB-STA-10 — RND-STA-01 mirrored-field row: `transitionStatus`, `side`, `align` and `anchorHidden` reach the state object, and so `ClassValue`/`StyleValue`, one round trip after JS writes the data attributes.
- CMB-STA-14 — RND-STA-01 mirrored-field row: `transitionStatus` reaches the state object, and so `ClassValue`/`StyleValue`, one round trip after JS writes the data attributes.
- CMB-STA-11 — RND-STA-01 mirrored-field row: the Arrow's `side`/`align`/`uncentered` reach the state object one round trip after JS writes the attributes.
- CMB-STA-09 — RND-STA-01 mirrored-field row (Positioner `side`/`align`/`anchorHidden` mirrored one round trip late) and EVD-STA-07/CMP-STA-10 row (the `popupSide` publication and so `data-popup-side` on Input/Trigger/InputGroup land after paint plus a round trip).
- CMB-POS-04 — RND-STA-01 mirrored-field row (Positioner `side`/`align`/`anchorHidden` mirrored one round trip late) and EVD-STA-07/CMP-STA-10 row (the `popupSide` publication and so `data-popup-side` on Input/Trigger/InputGroup land after paint plus a round trip).
- CMB-STA-25 — EVD-STA-07/CMP-STA-10/FFI-registration row: the post-commit pre-paint work (selected-index sync, pending-highlight resolution and re-emission, registries, `popupId`, `filled`) runs after paint plus a round trip; observable only as latency of dependent attribute updates.
- CMB-STA-26 — EVD-STA-07/CMP-STA-10/FFI-registration row: the post-commit pre-paint work (selected-index sync, pending-highlight resolution and re-emission, registries, `popupId`, `filled`) runs after paint plus a round trip; observable only as latency of dependent attribute updates.
- CMB-STA-27 — EVD-STA-07/CMP-STA-10/FFI-registration row: the post-commit pre-paint work (selected-index sync, pending-highlight resolution and re-emission, registries, `popupId`, `filled`) runs after paint plus a round trip; observable only as latency of dependent attribute updates.
- CMB-STA-33 — EVD-STA-07/CMP-STA-10/FFI-registration row: the post-commit pre-paint work (selected-index sync, pending-highlight resolution and re-emission, registries, `popupId`, `filled`) runs after paint plus a round trip; observable only as latency of dependent attribute updates.
- CMB-DOM-24 — EVD-STA-07/CMP-STA-10/FFI-registration row: the post-commit pre-paint work (selected-index sync, pending-highlight resolution and re-emission, registries, `popupId`, `filled`) runs after paint plus a round trip; observable only as latency of dependent attribute updates.
- CMB-FRM-04 — EVD-STA-07/CMP-STA-10/FFI-registration row: the post-commit pre-paint work (selected-index sync, pending-highlight resolution and re-emission, registries, `popupId`, `filled`) runs after paint plus a round trip; observable only as latency of dependent attribute updates.
- CMB-KEY-05 — EVD-EVT-11/EVD-DIS-01/FLT-KEY-01 row: the consumer's `cancel()`/`allowPropagation()` arrives a round trip after the Escape dispatch ends; JS applies the ADR 04 §3 fallback (prevent and stop). WebAssembly meets the spec through the synchronous `decide()` path.
- CMB-DIS-02 — EVD-EVT-11/EVD-DIS-01/FLT-KEY-01 row: the consumer's `cancel()`/`allowPropagation()` arrives a round trip after the Escape dispatch ends; JS applies the ADR 04 §3 fallback (prevent and stop). WebAssembly meets the spec through the synchronous `decide()` path.
- CMB-EVT-09 (Escape clause) — EVD-EVT-11/EVD-DIS-01/FLT-KEY-01 row: the consumer's `cancel()`/`allowPropagation()` arrives a round trip after the Escape dispatch ends; JS applies the ADR 04 §3 fallback (prevent and stop). WebAssembly meets the spec through the synchronous `decide()` path.
- CMB-STA-34 — UTL-STA-19..22 row: met on both modes (prop-to-store sync runs in `OnParametersSet`, before the render that carries the new parameter).

## 10. Positioning

Positioning is the shared anchored-positioning mechanism ([floating-and-positioning](../architecture/floating-and-positioning.md)); the Combobox Positioner passes the options of CMB-API-50.

- **CMB-POS-01** — MUST. With the input inside the popup the anchor defaults to the Trigger element (even when an InputGroup is rendered); otherwise it defaults to the InputGroup element when present, else the Input element (CMB-API-50); the floating reference element of the root floating context is the Input or, inside-popup, the Trigger, and a Trigger press outside-popup temporarily makes the Trigger the DOM reference for that interaction (CMB-PTR-02).
  Source: `combobox/positioner/ComboboxPositioner.tsx:69-70`; `combobox/root/AriaCombobox.tsx:1333-1340`; `combobox/trigger/ComboboxTrigger.tsx:197-199`. Test: [L2] `ComboboxPositioner.test.tsx` "uses the input when input group is absent", "uses the input group when present" (L4 for the Trigger-press reference clause).
- **CMB-POS-02** — MUST. The CSS variables `--available-height`/`--available-width` are seeded on the positioner before the size middleware's first flip pass, so a List styled with `max-height: min(<n>px, var(--available-height))` is measured at its capped height and the popup stays on the preferred side when the capped height fits.
  Source: `internals/useAnchorPositioning.ts` (size middleware, FLT-DOM-05); `combobox/positioner/ComboboxPositioner.tsx:72-89`. Test: [L4] `ComboboxPositioner.test.tsx` "keeps the popup on the preferred side when its capped height fits below tall content".
- **CMB-POS-03** — MUST. Modal positioning extras: while mounted with `modal`, an internal backdrop (PSH-STR-09/DOM-14, `inert` while not open) is rendered before the positioner with a cutout around the InputGroup ?? Input ?? Trigger, and the anchored scroll lock (PSH-PTR-07) is enabled while `open && modal`, keyed on whether the popup was touch-opened; an update that passes a new empty array as the controlled `value` must not lock body scroll when not modal.
  Source: `combobox/positioner/ComboboxPositioner.tsx:91-96,125-130`. Test: [L4] `ComboboxPositioner.test.tsx` "should not lock body scroll when controlled value={[]} triggers a re-render"; `ComboboxRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width".
- **CMB-POS-04** — MUST. The rendered side is published to the store post-commit pre-paint (`popupSide`) and exposed on Input/Trigger/InputGroup as `data-popup-side` only while mounted with a Positioner (CMB-STA-09); the Positioner conformance suite (collision handling, `data-side`/`data-align`, `anchorHidden`, CSS variables) applies in full. Server: relaxed, see §9.
  Source: `combobox/positioner/ComboboxPositioner.tsx:106-108`; `combobox/utils/parts.ts:10-16`. Test: [L4] `positionerConformanceTests` (§13); `ComboboxInput.test.tsx` "sets data-popup-side to the current popup side".

## 11. Cross-cutting dependencies

| Mechanism | Document | Combobox configuration / IDs |
|---|---|---|
| Element rendering, `render`/`className`/`style`, state attributes, `mergeProps`, `preventBaseUIHandler` | [rendering-and-composition](../architecture/rendering-and-composition.md) | every part; conformance RND-DOM-36..49 (§13) |
| Event details, reasons, cancel/allowPropagation, `preventUnmountOnClose`, highlight reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | §7; EVD §4.3.7 Combobox rows; EVD-DIS-03/04, EVD-EDGE-11, EVD-EVT-24 |
| Button behavior (`nativeButton`, disabled, composite items) | [button-behavior](../architecture/button-behavior.md) | Trigger/Clear/ChipRemove (`native` default `true`), Item (`native` default `false`, `composite: true`, `focusableWhenDisabled`), internal dismiss button (non-native, UBT-API-04) |
| Field/Form registration, validation, labelable scope, hidden inputs | [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) | §8; FFI §6 Combobox row; FFI-DOM-03/06, FFI-ARIA-07/10, FFI-FOC-05, FFI-FRM-28/31, FFI-EDGE-03 |
| List navigation (virtual focus), composite item registry, typeahead, grid navigation | [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) | CMB-KEY-01 options; CMP-ARIA-01/02; CMP-KEY-22/28/29/33; CMP-STA-33/37/39/40; CMP-PTR-04/05; CMP-FOC-05; Chips registry |
| Anchored positioning, click/dismiss/focus-manager interactions, portal, focus guards | [floating-and-positioning](../architecture/floating-and-positioning.md) | CMB-API-50, CMB-PTR-01, CMB-DIS-01, CMB-FOC-03; FLT-API-05/09/10/13, FLT-ANI-04, FLT-DOM-05..07/11/15, FLT-ARIA-01..03, FLT-PTR-02..05/26..28, FLT-FOC-11 |
| Popup state mapping, open interaction type, focusable popup props, internal backdrop, scroll lock | [popup-store-and-handles](../architecture/popup-store-and-handles.md) | PSH-DOM-01..04, PSH-STA-41, PSH-PTR-03/05/07, PSH-STR-09, PSH-ARIA-04 (incl. its `Combobox.Trigger` `"dialog"` exception; CMB-ARIA-07) |
| Transition status, unmount after close, `onOpenChangeComplete`, indicator mounting | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | §9; TRN-ANI-01/18..23/27, TRN-DOM-01/02/05/07, TRN-EVT-01/02/07/08 |
| Controlled state, `useValueChanged`, collator filter, item stringification, live-region mutation, `visuallyHidden`, `isMouseWithinBounds`, direction, platform flags | [shared-utilities](../architecture/shared-utilities.md) | UTL-STA-01..04/36, UTL-I18N-01/08..13, UTL-A11Y-01..04, UTL-DOM-13/14/26/28, UTL-EDGE-02/04/05 |

## 12. Edge cases, invariants and dev warnings

- **CMB-EDGE-01** — MUST. Passing an `items` value that is an object but not a `createItems()` collection (no `label` function) throws synchronously during render: "Base UI: the `items` prop received an object that is not a collection, so its items cannot be read. Pass an array of items, an array of groups with items, or the result of `createItems()`. See https://base-ui.com/react/components/combobox#createitems".
  Source: `combobox/root/AriaCombobox.tsx:164-175`. Test: [L2] `createItems.test.tsx` "rejects an items object that is not a collection".
- **CMB-EDGE-02** — MUST. In development, when a `createItems()` collection derives the same value from two items, a console error is logged once per duplicate when the derivation runs: "Two items passed to createItems() derived the value <value>, so selection and label resolution cannot tell them apart: the first item wins the label and every item carrying the value renders as selected. Return a unique value from `getValue`." Runtime behavior is as the message states.
  Source: `combobox/items/createItems.ts:102-133`. Test: [L2] `createItems.test.tsx` "resolves a duplicated derived value to the same label regardless of lookup order".
- **CMB-EDGE-03** — MUST. The `Combobox.Label` development error of CMB-STR-10 (FFI-EDGE-03) is the only other dev-time diagnostic; the useControlled mode/default-change errors (UTL-EDGE-22/23) apply to `value`, `inputValue` and `open`.
  Source: `combobox/label/ComboboxLabel.tsx:40-53`; `combobox/root/AriaCombobox.tsx:294-337`. Test: [L2] `ComboboxLabel.test.tsx` "warns without relying on React.captureOwnerStack when labeling an external input".
- **CMB-EDGE-04** — MUST. `items` switching to `undefined` (data unloading) must not cause an unbounded update loop: the component treats it as "no items" (empty list, labels degrade to `itemToStringLabel`/`String(value)` or the raw id) and recovers when data returns, including restoring the selected label.
  Source: `combobox/root/AriaCombobox.tsx:177-178,284,358-361`. Test: [L2] `ComboboxRoot.test.tsx` "does not cause infinite re-renders when items becomes undefined", "restores derived input after items load asynchronously"; `createItems.test.tsx` "renders an empty list while the data is undefined and fills in once it loads", "resolves and degrades the selected label as the data loads and shrinks", "falls back to the raw selected ID when API results are cleared", "degrades the label of an externally filtered item after its window is gone".
- **CMB-EDGE-05** — MUST. Nullish entries in `items`/`filteredItems` are unsupported: a collection skips them during derivation and the default filter rejects them; a `null` *value* item is legal (a "none" option) and is what `Combobox.Value` uses for the placeholder-label rule.
  Source: `combobox/items/createItems.ts:108-112`; `combobox/root/utils/index.ts:28-30,46-48`. Test: [L2] `createItems.test.tsx` "keeps nullish entries out of the accessors and out of filtered results", "renders a sentinel item for the empty selection like any other item"; `ComboboxValue.test.tsx` "handles null items in grouped structure".
- **CMB-EDGE-06** — MUST. Server rendering: the Input (outside popup) and the Trigger (inside popup) carry the combobox ARIA attributes in server HTML; `Combobox.Label` is not linked to the Trigger before hydration; in Combobox mode (`selectionMode` single/multiple) the hidden input(s) carry `name` and the visible Input never does, both in server HTML and after hydration (`inputOwnsFormValue` is `false` from the initial store state on); the initial selected label of a `defaultValue` resolves from an external window during the server render.
  Source: `combobox/root/AriaCombobox.tsx:519-522,1564,1589-1590,1703`; `combobox/input/ComboboxInput.tsx:111,186`; `combobox/trigger/ComboboxTrigger.tsx:91`. Test: [L2] `ComboboxRoot.test.tsx` "sets combobox aria attributes on the input", "sets combobox aria attributes on the trigger when input is inside popup", "does not link Combobox.Label to trigger before hydration"; `createItems.test.tsx` "resolves a server-rendered defaultValue label from the external window". Hidden-input form ownership across hydration: none (`store.test.tsx` "keeps form value ownership across hydration" renders `Autocomplete.Root` and asserts the opposite — the visible input owns the name — so it is engine evidence for [autocomplete](autocomplete.md), not this clause).
- **CMB-EDGE-07** — MUST. `attach` and `dispose` of the part's JavaScript twin are idempotent; a repeated `attach` of the same element is a no-op, so typeahead after open/close and the no-force-mount rule on external value changes hold however often a twin is attached (Base UI guards the same property by running these tests with strict mode on and off).
  Source: `combobox/root/AriaCombobox.tsx:1026-1078`; `combobox/trigger/ComboboxTrigger.tsx:109-122`. Test: [L2] twin contract (a repeated `attach`/`dispose` of the same element records no further effect); Base UI evidence: `ComboboxRoot.test.tsx` "does not force-mount the list when controlled value changes externally (strict: %s)"; `ComboboxTrigger.test.tsx` "selects item when typing after the popup has been opened and closed (items prop, strict: %s)".
- **CMB-EDGE-08** — MUST. Duplicate values in a plain `items` array resolve labels to the first match; a `{ value, label }` item whose `value` field equals a derived selection is *not* treated as that selection when a collection is used (source fields are not derived values).
  Source: `internals/resolveValueLabel.tsx:93-140`; `combobox/root/AriaCombobox.tsx:216-229`. Test: [L2] `ComboboxValue.test.tsx` "handles duplicate values in items array (uses first match)"; `createItems.test.tsx` "does not treat source value fields as derived selections".
- **CMB-EDGE-09** — MUST. Rendering an Item without a Root throws (CMB-STR-02); rendering a Combobox inside a composite root (e.g. Toolbar) keeps both navigable: arrow keys typed in the Input are handled by the combobox (CMP-KEY-04 exempts text inputs from composite navigation) and composite navigation moves between the combobox and sibling composite items from outside the input.
  Source: `internals/composite/root/useCompositeRoot.ts` (CMP-KEY-04); `combobox/root/AriaCombobox.tsx:1416-1450`. Test: [L4] `ComboboxRoot.test.tsx` "should navigate between combobox and composite items".
- **CMB-EDGE-10** — MUST. Items skip unchanged renders: an input change that keeps the same filtered membership must not render still-mounted Items again (their parameters are unchanged; render count observed through bUnit, L2), and virtualized items subscribe to the derived-items context only through the virtualized index wrapper.
  Source: `combobox/item/ComboboxItem.tsx:224-286`; `combobox/list/ComboboxList.tsx:52-61`. Test: [L2] `ComboboxItem.test.tsx` "does not re-render still-mounted items when filtering keeps the same membership".
- **CMB-EDGE-11** — MUST. Controlled `null` in multiple mode is read as `[]` everywhere (selection, chips, hidden inputs, filled); controlled `value` objects recreated with equal contents (per `isItemEqualToValue`) keep the selected index on reopen; an unmatched controlled value yields no selected index but still shows its label (`itemToStringLabel`/`label` field).
  Source: `combobox/root/AriaCombobox.tsx:294-300,1026-1071`; `combobox/store.ts:136-174`. Test: [L2] `ComboboxRoot.test.tsx` "normalizes a controlled null value while closed in multiple mode", "re-highlights the selection on reopen when the value object is recreated with equal contents (no items prop)", "clears selectedIndex when the value is set to an unmatched value while closed (no items prop)", "shows the label for a controlled object value not in items".
- **CMB-EDGE-12** — MUST. The `createItems()` label of a selected value is resolved from current props only: collection data first, then the current external `filteredItems` window, then `itemToStringLabel`, then `String(value)`; nothing from a past window is remembered, and separately fetched selected metadata must be kept in the collection's data to stay resolvable. Each Root resolves against its own window when a collection is shared.
  Source: `combobox/root/AriaCombobox.tsx:185-229`. Test: [L2] `createItems.test.tsx` "keeps separately fetched selected metadata outside paginated search results", "labels a selection that arrives together with its externally filtered item", "re-labels an unchanged selection when its item arrives in a later external window", "labels a defaultValue whose item only exists in the externally filtered window", "uses the custom comparer to label a value from the externally filtered window", "keeps an external result visible when the selected item is absent from known data", "relabels a borrowed value when a later window carries a fresher item", "resolves each root against its own external window when the collection is shared", "labels multiple selected values from the current external window", "labels a selected value that is not in the collection as itself", "uses the default label fallback outside the collection", "labels a derived selection when useFilter receives its value".
- **CMB-EDGE-13** — MUST. Element-substitution callbacks are opaque (RND-DOM-32): a `render` *function* on a part whose id another part references MUST apply the `id` it is handed, and the owner does not recover from a dropped id. If a Popup callback drops it, `popupId` falls back to the computed id (`popupRef.current.id || popupId`), so the Trigger's `aria-controls` (input inside the popup) names an id no element carries; if a List callback drops it, the Input/Trigger `aria-controls` reads the List element's DOM id (`listElement.id`) and references no element. A port must reproduce this contract (pass the id through; do not invent a recovery) and document it.
  Source: `combobox/popup/ComboboxPopup.tsx:54-62,89`; `combobox/trigger/ComboboxTrigger.tsx:94-101`; `combobox/root/AriaCombobox.tsx:1370`; `combobox/list/ComboboxList.tsx:77`; `internals/resolveRenderedId.ts:12-14`; see RND-DOM-32. Test: none.
- **CMB-EDGE-14** — MUST. `Combobox.Label` and `Combobox.GroupLabel` register the id they compute, not the id that lands on their element (RND-DOM-54): the Label always registers `<rootId>-label` (`getDefaultLabelId`; a runtime `id` prop is deleted) as the store `labelId`, which the Trigger renders in `aria-labelledby` (CMB-ARIA-16); a GroupLabel registers its `id` prop or generated id (`useBaseUiId`) as the Group's `aria-labelledby` (CMB-ARIA-10). A `render` callback that drops the `id` it is handed, or a substitute element carrying its own different `id` (which wins on the element, RND-DOM-32), therefore leaves the Trigger's or Group's `aria-labelledby` pointing at an id no element carries. The Label's `for`/control association is unaffected, because its fallback control id is read from the rendered Trigger element (`triggerElement?.id`). A port MUST NOT invent a recovery (cf. CMB-EDGE-13).
  Source: `combobox/label/ComboboxLabel.tsx:24-38,55-68`; `utils/resolveAriaLabelledBy.ts:3-5`; `combobox/trigger/ComboboxTrigger.tsx:70,91`; `combobox/group-label/ComboboxGroupLabel.tsx:19-34`; see RND-DOM-32, RND-DOM-54. Test: none.

## 13. Behaviors evidenced by tests

Coverage of each of the 38 test files by the requirements above. `describeConformance` rows record only the options the part passes; the shared assertions are RND-DOM-36…RND-DOM-49 (and UBT for `button: true`). Tests marked `skipIf(isJSDOM)` run only in a real browser; `skipIf(!isJSDOM)` only under jsdom.

### `arrow/ComboboxArrow.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, rendered inside Root > Portal > Positioner) | CMB-STR-04, CMB-STA-11, CMB-DOM-15; RND-DOM-36…49 |

### `backdrop/ComboboxBackdrop.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`) | CMB-STA-08, CMB-DOM-14; RND-DOM-36…49 |

### `chip-remove/ComboboxChipRemove.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`) | CMB-API-53, CMB-DOM-11; RND-DOM-36…49; UBT |
| "throws a descriptive error when rendered outside <Combobox.Chip>" | CMB-STR-02 |
| "should render disabled attribute when disabled" | CMB-API-54, CMB-STA-04, CMB-DOM-11 |
| "should not remove chip when disabled" | CMB-API-54, CMB-PTR-10 |
| "should not remove chip when readOnly" | CMB-API-29, CMB-PTR-10 |
| "should be focusable but not functional when readOnly" | CMB-API-54, CMB-PTR-10 |
| "should remove chip on click when enabled" | CMB-PTR-10, CMB-EVT-03 |
| "should focus input after removing chip" | CMB-PTR-10 |
| "should keep the popup open while removing a chip" | CMB-PTR-10, CMB-DIS-01 |
| "keeps a popup input focused when removing a chip rendered outside the popup" | CMB-PTR-10, CMB-FOC-06 |
| "should prevent event propagation" | CMB-PTR-10, CMB-EVT-09 |
| "allows click and keyboard events to propagate when requested" | CMB-EVT-09, CMB-KEY-10 |
| "ignores click and keyboard activation on a non-native button: %o" (disabled / readOnly) | CMB-API-53, CMB-KEY-10 |
| "removes a chip with Space but ignores unrelated keys" | CMB-KEY-10 |
| "keeps a different active item highlighted when removing a chip" | CMB-KEY-10, CMB-PTR-10 |
| "removes a filtered-out chip without clearing the visible highlight" | CMB-KEY-10, CMB-PTR-10 |
| "records pointer-origin removal when clearing the highlighted item" | CMB-STA-24, CMB-EVT-05 |
| "should handle keyboard activation" | CMB-KEY-10 |
| "should have proper tab index" | CMB-DOM-11, CMB-FOC-02 |

### `chip/ComboboxChip.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Chips) | CMB-STR-09, CMB-STA-03; RND-DOM-36…49 |
| "should render aria-disabled attribute when disabled" | CMB-STA-03, CMB-ARIA-12 |
| "should prevent keyboard navigation when disabled" | CMB-KEY-09 |
| "should prevent deletion when disabled" | CMB-KEY-09 |
| "should prevent mouse interactions when disabled" | CMB-PTR-09 |
| "should prevent focus when disabled" | CMB-PTR-09 |
| "should render aria-readonly attribute when readOnly" | CMB-ARIA-12 |
| "should prevent deletion when readOnly" | CMB-KEY-09 |
| "should prevent navigation when readOnly and also prevent deletion" | CMB-KEY-09 |
| "moves focus to the input when a chip is pressed while readOnly" | CMB-PTR-09 |
| "does not dismiss the popup when clicking a chip (outsidePress is blocked)" | CMB-DIS-01, CMB-PTR-09 |
| "closes the popup when a chip receives focus" | CMB-DIS-04, CMB-FOC-07 |
| "should handle keyboard navigation when enabled" | CMB-KEY-09 |
| "returns focus to the input for activation, vertical navigation, and text entry" | CMB-KEY-09 |
| "focuses a conditional input when reopening from a chip with %s" (ArrowDown / ArrowUp) | CMB-KEY-09, CMB-FOC-07 |
| "leaves focus on a chip for modified printable keys" | CMB-KEY-09 |
| "handles Delete as a chip removal key" | CMB-KEY-09 |
| "focuses the next chip after removing a chip rendered from the value" | CMB-KEY-09 |
| "mirrors chip keyboard navigation in RTL mode" | CMB-KEY-08, CMB-KEY-09 |
| "should navigate through only rendered chips" | CMB-KEY-09, CMB-STA-33 |
| "should handle deletion when enabled" | CMB-KEY-09, CMB-EVT-03 |
| "should focus input on mouse down when enabled" | CMB-PTR-09 |

### `chips/ComboboxChips.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`) | CMB-STA-17, CMB-DOM-11; RND-DOM-36…49 |
| "does not set role=\"toolbar\" when there are no chips" | CMB-ARIA-12 |
| "sets role=\"toolbar\" when there is at least one chip" | CMB-ARIA-12, CMB-STA-34 |
| "focuses the input when clicking anywhere in the Chips area" | CMB-PTR-08 |
| "lets onMouseDown prevent the built-in focus and open behavior" | CMB-PTR-08 |
| "lets nested chip onMouseDown prevent the built-in focus and open behavior" | CMB-PTR-08 |
| "lets nested chip onContextMenu call preventBaseUIHandler" | CMB-PTR-08 |
| "does not treat chip remove presses as chips-area presses" | CMB-PTR-08 |
| "does not focus or open when disabled by Field.Root" | CMB-PTR-08, CMB-API-30 |
| "focuses the input and opens when readOnly" | CMB-PTR-08, CMB-API-29 |
| "opens and focuses an input rendered inside the popup when the chips area is pressed" | CMB-PTR-08 |
| "clears the highlighted chip when the popup opens" | CMB-FOC-07 |

### `clear/ComboboxClear.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`, rendered with a selected value) | CMB-API-40, CMB-DOM-07; RND-DOM-36…49; UBT |
| "renders in single mode when a value is selected" | CMB-STA-06, CMB-DOM-07 |
| "does not render without a value by default" | CMB-API-42, CMB-DOM-07 |
| "renders and clears the typed value in no-selection mode" | CMB-DOM-07 (Autocomplete mode; see [autocomplete](autocomplete.md)) |
| "renders and clears chips in multiple mode" | CMB-STA-06, CMB-PTR-07 |
| "click clears selected value and focuses input" | CMB-PTR-07, CMB-EVT-03 |
| "clears after pointer interaction marks keyboard navigation inactive" | CMB-PTR-07 |
| "does not dismiss the popup on click (outsidePress is blocked)" | CMB-DIS-01, CMB-PTR-07 |
| "does not dismiss a popup input when the clear button is rendered outside it" | CMB-PTR-07, CMB-DIS-01 |
| "is disabled when root disabled and does nothing on click" | CMB-API-41, CMB-DOM-06, CMB-PTR-07 |
| "when root is readOnly it does nothing on click" | CMB-API-29, CMB-PTR-07 |
| "clears selection without closing and restores popup input focus" | CMB-PTR-07 |
| "triggers enter animation via data-starting-style when becoming visible" (skipIf jsdom) | CMB-ANI-04, CMB-DOM-06, CMB-STA-06 |
| "triggers exit animation via data-ending-style before unmount" (skipIf jsdom) | CMB-ANI-04, CMB-API-42, CMB-DOM-06 |

### `collection/ComboboxCollection.test.tsx`

| Test | Requirements |
|---|---|
| "renders filtered items" | CMB-STR-06, CMB-API-62, CMB-API-63 |
| "renders nothing when a nested group does not provide items" | CMB-STR-06, CMB-API-63 |

### `empty/ComboboxEmpty.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root with items) | CMB-STA-17; RND-DOM-36…49 |
| "renders when there are no filtered items" | CMB-DOM-23 |
| "does not render when there are items" | CMB-DOM-23 |
| "renders when the search query matches no items" | CMB-DOM-23, CMB-STA-23 |
| "does not render when the search query matches an item" | CMB-DOM-23 |
| "removes the initial text mutation after the reset delay" | CMB-ARIA-13 |
| "updates the live region immediately when the empty content appears after mount" | CMB-ARIA-13 |
| "preserves a custom render prop on the visible element" | CMB-ARIA-13 |

### `group-label/ComboboxGroupLabel.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Group) | CMB-STR-02, CMB-STA-17; RND-DOM-36…49 |
| "wires to group aria-labelledby" | CMB-ARIA-10, CMB-STA-16 |
| "is hidden from the accessibility tree by default" | CMB-ARIA-10 |
| "allows overriding aria-hidden" | CMB-ARIA-10 |
| "uses provided id in aria-labelledby" | CMB-ARIA-10, CMB-STA-16 |
| "does not let an older label cleanup clear a newer label" | CMB-STA-16 |

### `group/ComboboxGroup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`) | CMB-STA-17; RND-DOM-36…49 |
| "should render group with label" | CMB-API-62, CMB-ARIA-10 |
| "should associate label with group" | CMB-ARIA-10 |

### `group/ComboboxGroupContext.test.tsx`

| Test | Requirements |
|---|---|
| "throws a descriptive error when used outside <Combobox.Group>" | CMB-STR-02 |

### `icon/ComboboxIcon.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLSpanElement`) | CMB-STR-08, CMB-STA-17, CMB-ARIA-11; RND-DOM-36…49 |

### `input-group/ComboboxInputGroup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`) | CMB-STA-02, CMB-DOM-03; RND-DOM-36…49 |
| "should not dismiss the popup when clicking inside the input group" | CMB-DIS-01, CMB-PTR-08 |
| "focuses the input and opens when clicking input-group padding around chips" | CMB-PTR-08 |
| "does not handle chip presses a second time when chips are nested inside the input group" | CMB-PTR-08 |
| "focuses the input without opening when openOnInputClick is false" | CMB-API-20, CMB-PTR-08 |
| "does not focus or open when disabled by Field.Root" | CMB-PTR-08, CMB-API-30 |
| "focuses the input and opens when readOnly" | CMB-PTR-08, CMB-API-29 |
| "has role prop" | CMB-ARIA-12 |

### `input/ComboboxInput.android.test.tsx`

| Test | Requirements |
|---|---|
| "propagates changes during Android composition" | CMB-KEY-14 |

### `input/ComboboxInput.gecko.test.tsx`

| Test | Requirements |
|---|---|
| "uses Gecko RTL caret positions for Home and End" | CMB-KEY-06 |

### `input/ComboboxInput.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLInputElement`) | CMB-STA-05, CMB-DOM-01; RND-DOM-36…49 |
| "should render aria-disabled attribute when disabled" | CMB-API-38, CMB-ARIA-17 |
| "should inherit disabled state from ComboboxRoot" | CMB-API-38, CMB-API-30 |
| "should inherit disabled state from Field.Root" | CMB-API-38 |
| "should not open popup when disabled" | CMB-API-38, CMB-PTR-01 |
| "should prioritize local disabled over root disabled" | CMB-API-38 |
| "renders as a <textarea> without an invalid `type` attribute and stays editable" | CMB-DOM-01, CMB-ARIA-05 |
| "applies combobox aria attributes to a <textarea> only while the popup is open" | CMB-ARIA-05 |
| "applies combobox aria attributes to an inline <textarea> before it is interacted with" | CMB-ARIA-05, CMB-ARIA-02 |
| "should render aria-readonly and readonly attributes when readOnly" | CMB-ARIA-17, CMB-DOM-02 |
| "should open popup when readOnly" | CMB-API-29, CMB-PTR-01 |
| "should not change the input value when typing while readOnly" | CMB-KEY-15 |
| "allows interactions when readOnly={false}" | CMB-KEY-15 |
| "sets aria-required attribute when required" | CMB-API-28, CMB-ARIA-01 |
| "fires with reason input-press when Input is clicked" | CMB-PTR-01, CMB-EVT-02 |
| "clears selected value when input text is cleared (single selection)" | CMB-KEY-17, CMB-EVT-03 |
| "should open popup on typing when enabled" | CMB-KEY-17 |
| "should handle multiple selection with chips when disabled" | CMB-API-30 |
| "should handle multiple selection with chips when readOnly" | CMB-API-29 |
| "should move focus to clear button when pressing Escape and popup is closed" | CMB-KEY-05, CMB-DIS-02 |
| "pressing Home moves caret to start" | CMB-KEY-06 |
| "pressing End moves caret to end" | CMB-KEY-06 |
| "scrolls to the start and end when pressing Home/End on overflowing input" (skipIf jsdom) | CMB-KEY-06 |
| "preserves caret position when controlled and inserting in the middle" | CMB-API-09 |
| "clears the chip highlight when focus returns to the input" | CMB-FOC-07 |
| "removes the last chip after clicking from a chip back to the input" | CMB-KEY-07 |
| "removes the last chip after leaving a chip and tabbing back to the input" | CMB-KEY-07 |
| "keeps focus on the input when navigating toward chips but none are rendered" | CMB-KEY-08, CMB-STR-09 |
| "treats a null selectionStart as the beginning of a custom input" | CMB-KEY-08 |
| "keeps the popup open after modified keyboard navigation" | CMB-KEY-14 |
| "does not select an item for an IME keydown without a highlight" | CMB-KEY-14 |
| "does not restore an inline highlight after the highlighted slot is removed" | CMB-STA-28 |
| "clears a closed multiple value on Escape and stops propagation" | CMB-KEY-05, CMB-EVT-09 |
| "lets Escape propagate when a closed multiple value is already empty" | CMB-KEY-05 |
| "lets Escape propagate from a closed inline combobox" | CMB-KEY-05, CMB-DIS-05 |
| "closes on an empty composition update when input clicks do not open the popup" | CMB-KEY-14, CMB-API-20 |
| "clears the highlight on an empty composition update while staying open" | CMB-KEY-14 |
| "removes the last rendered chip when pressing Backspace in an empty input" | CMB-KEY-07 |
| "removes the last chip after tabbing from a chip back to the input" | CMB-KEY-07 |
| "focuses the last chip with ArrowLeft after tabbing from a chip back to the input" | CMB-KEY-08 |
| "removes the last selected value when no chips are rendered" | CMB-KEY-07, CMB-STR-09 |
| "closes the popup when tabbing out" | CMB-DIS-04, CMB-KEY-16, CMB-STA-30 |
| "sets data-popup-side to the current popup side" (skipIf jsdom) | CMB-DOM-02, CMB-STA-09, CMB-POS-04 |
| "toggles data-list-empty when the filtered list is empty" | CMB-DOM-02, CMB-STA-12 |

### `item-indicator/ComboboxItemIndicator.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Combobox.ItemIndicator keepMounted />`, `refInstanceof: HTMLSpanElement`, inside Item) | CMB-API-61, CMB-STA-14; RND-DOM-36…49 |
| "updates a mounted indicator when its item becomes unselected" | CMB-DOM-10, CMB-STA-14, CMB-ANI-05 |
| "mounts only while selected when keepMounted is false" | CMB-API-61, CMB-DOM-10 |
| "preserves the kept-mounted element across selection changes" | CMB-API-61, CMB-ANI-05 |

### `item/ComboboxItem.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root > Portal > Positioner > Popup > List) | CMB-API-59, CMB-STA-13; RND-DOM-36…49 |
| "selects item and closes in single mode" | CMB-API-56, CMB-PTR-04, CMB-PTR-05 |
| "calls onClick when clicked with a pointer" | CMB-API-60, CMB-EVT-12 |
| "calls onClick when selected with Enter key (via root interaction)" | CMB-API-60, CMB-KEY-04, CMB-EVT-12 |
| "does not select the item when onClick prevents Base UI handler" | CMB-API-60, CMB-EVT-12 |
| "does not select disabled item" | CMB-API-58, CMB-PTR-04 |
| "inherits the disabled state from the root" | CMB-API-58 |
| "Enter selects highlighted item" | CMB-KEY-04 |
| "keeps the input focused after selecting an item with touch" (skipIf jsdom) | CMB-PTR-04 |
| "prevents default on mousedown so pointer selection does not steal input focus" | CMB-PTR-04, CMB-FOC-01 |
| "multiple mode toggles selection and stays open" | CMB-API-04, CMB-PTR-05, CMB-STA-13 |
| "reflects selected value with aria-selected when reopening" | CMB-ARIA-09, CMB-API-56 |
| "clicking a link inside an item does not select or close (anchor with href)" (skipIf !jsdom) | CMB-PTR-05 |
| "clicking a hash link inside an item does not select and closes popup (anchor with #hash)" | CMB-PTR-05 |
| "clicking an anchor without href behaves like normal item (selects and closes)" | CMB-PTR-05 |
| "selects the highlighted filtered item with the keyboard after filtering" (virtualized, no index) | CMB-API-22, CMB-API-57 |
| "selects the clicked filtered item after filtering" | CMB-API-22 |
| "registers a rendered window at its filtered indexes" | CMB-API-22, CMB-STA-33 |
| "does not register an item missing from the filtered values" | CMB-API-22, CMB-STA-33 |
| "selects the highlighted filtered item with the keyboard after filtering" (virtualized, explicit index) | CMB-API-57 |
| "does not re-render still-mounted items when filtering keeps the same membership" | CMB-API-55, CMB-EDGE-10 |
| "updates the highlight on hover in a non-virtualized list" | CMB-API-57, CMB-PTR-06 |
| "does not assign an id to an explicitly unregistered item" | CMB-API-57, CMB-DOM-09 |

### `item/ComboboxItemContext.test.tsx`

| Test | Requirements |
|---|---|
| "throws a descriptive error when used outside <Combobox.Item>" | CMB-STR-02 |

### `items/createItems.test.tsx`

| Test | Requirements |
|---|---|
| "rejects an items object that is not a collection" | CMB-EDGE-01 |
| "treats undefined data as the absence of items rather than as an empty list" | CMB-API-67 |
| "uses the derived value for selection and resolves the label while closed" | CMB-API-67, CMB-API-32 |
| "supports rich API-loaded items with primitive ID values end to end" | CMB-API-67, CMB-API-13 |
| "keeps separately fetched selected metadata outside paginated search results" | CMB-EDGE-12 |
| "falls back to the raw selected ID when API results are cleared" | CMB-EDGE-04 |
| "keeps nullish entries out of the accessors and out of filtered results" | CMB-EDGE-05, CMB-STA-23 |
| "treats a non-array items field as item data rather than as a group" | CMB-API-12, CMB-API-68 |
| "treats an array-valued items field as a group when its declared type is unknown" | CMB-API-12, CMB-API-68 |
| "renders a sentinel item for the empty selection like any other item" | CMB-EDGE-05 |
| "resolves the label of an initially selected value" | CMB-STA-19, CMB-API-67 |
| "renders an empty list while the data is undefined and fills in once it loads" | CMB-EDGE-04, CMB-API-67 |
| "resolves and degrades the selected label as the data loads and shrinks" | CMB-EDGE-04 |
| "highlights an initially selected derived value when opened" | CMB-STA-25, CMB-KEY-02 |
| "highlights an initially selected derived value on mount when inline" | CMB-STA-28 |
| "renders the selected label via Combobox.Value" | CMB-API-46, CMB-API-67 |
| "filters items by their derived labels" | CMB-API-67, CMB-STA-23 |
| "keeps filtering on the collection label when itemToStringLabel is provided" | CMB-API-32 |
| "falls back to itemToStringLabel only for values the collection cannot resolve" | CMB-API-32 |
| "uses the root locale for filtering" | CMB-API-15 |
| "updates a selected label when a stable accessor returns a new result" | CMB-STA-20 |
| "resolves a value that only a custom comparer matches from the collection" | CMB-API-34 |
| "passes source items to a custom root filter" | CMB-API-14 |
| "labels a derived selection when useFilter receives its value" | CMB-EDGE-12, CMB-API-65 |
| "lets a custom root filter match source fields that are neither the value nor the label" | CMB-API-14 |
| "labels a selected value that is not in the collection as itself" | CMB-EDGE-12 |
| "uses the default label fallback outside the collection" | CMB-EDGE-12 |
| "uses derived labels for closed trigger typeahead" | CMB-KEY-12 |
| "does not stringify null when no value is selected" | CMB-API-68 |
| "does not treat source value fields as derived selections" | CMB-EDGE-08 |
| "toggles derived values in multiple mode" | CMB-PTR-05 |
| "serializes derived values in multiple mode" | CMB-FRM-02 |
| "serializes a derived value in single mode" | CMB-FRM-02 |
| "serializes the derived value with itemToStringValue" | CMB-FRM-02, CMB-API-33 |
| "resolves $label as a primitive selection" (Zero / Empty string / False / Bigint) | CMB-API-68 |
| "passes the derived value to the Combobox.Value render prop" | CMB-API-46 |
| "renders every selected label via Combobox.Value in multiple mode" | CMB-API-46 |
| "labels a selection that arrives together with its externally filtered item" | CMB-EDGE-12 |
| "re-labels an unchanged selection when its item arrives in a later external window" | CMB-EDGE-12 |
| "labels a defaultValue whose item only exists in the externally filtered window" | CMB-EDGE-12 |
| "uses the custom comparer to label a value from the externally filtered window" | CMB-EDGE-12 |
| "keeps an external result visible when the selected item is absent from known data" | CMB-EDGE-12, CMB-STA-22 |
| "projects externally filtered source items into the derived value domain" | CMB-API-13 |
| "degrades the label of an externally filtered item after its window is gone" | CMB-EDGE-04, CMB-EDGE-12 |
| "relabels a borrowed value when a later window carries a fresher item" | CMB-EDGE-12 |
| "resolves each root against its own external window when the collection is shared" | CMB-EDGE-12 |
| "labels multiple selected values from the current external window" | CMB-EDGE-12 |
| "resolves a server-rendered defaultValue label from the external window" | CMB-EDGE-06, CMB-STA-19 |
| "opens a reordered external list at the selected value in rendered-list coordinates" | CMB-STA-25 |
| "resolves virtualized collection items to their derived value" | CMB-API-22, CMB-API-66 |
| "matches browser autofill against %s" (serialized derived value / derived label) | CMB-EVT-11 |
| "resolves a duplicated derived value to the same label regardless of lookup order" | CMB-EDGE-02, CMB-API-67 |
| "selects the derived value with the keyboard" | CMB-KEY-04 |
| "resolves externally virtualized item indexes and callbacks in the derived value domain" | CMB-API-66, CMB-API-22 |
| "uses explicit item values in the derived value domain" | CMB-API-56 |
| "stops filtering when the limit is reached" | CMB-API-16, CMB-API-67 |
| "does not call the accessors when creating a flat collection" | CMB-API-67 |
| "does not call the accessors when creating a grouped collection" | CMB-API-67 |
| "only projects the leaf items of the data" | CMB-API-67 |
| "resolves labels only for the values that need them" | CMB-API-67 |
| "uses the derived value for selection and resolves the label while closed" (grouped) | CMB-API-67 |
| "resolves the label of an initially selected value" (grouped) | CMB-STA-19 |
| "filters items by their derived labels and drops empty groups" | CMB-STA-23 |
| "passes leaf source items to a custom root filter" | CMB-STA-23, CMB-API-14 |
| "applies the limit across groups and drops the emptied ones" | CMB-API-16, CMB-STA-23 |
| "projects externally filtered grouped source items into the derived value domain" | CMB-API-13 |
| "resets an emptied external result when reopening a grouped selection" | CMB-STA-22 |
| "resets an initially empty external result when opening a grouped selection" | CMB-STA-22 |
| "only applies accessors to group items" | CMB-API-67 |

### `label/ComboboxLabel.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root with Trigger) | CMB-STA-01, CMB-DOM-17; RND-DOM-36…49 |
| "warns without relying on React.captureOwnerStack when labeling an external input" | CMB-STR-10, CMB-EDGE-03 |
| "does not run the development warning in production" (skipIf !jsdom) | CMB-STR-10 |

### `list/ComboboxList.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root > Portal > Positioner > Popup) | CMB-STA-12, CMB-DOM-08; RND-DOM-36…49 |
| "sets role=listbox and aria-multiselectable in multiple mode" | CMB-ARIA-08 |
| "sets aria-readonly on the listbox when the root is readOnly" | CMB-ARIA-08 |
| "does not set aria-readonly on the grid when the root is readOnly" | CMB-ARIA-08 |
| "does not set aria-readonly on the listbox by default" | CMB-ARIA-08 |
| "sets aria-readonly on the Autocomplete listbox when the root is readOnly" | CMB-ARIA-08 (Autocomplete composition) |
| "does not prevent Enter when no item is highlighted" | CMB-KEY-04 |
| "selects the highlighted item with Enter" | CMB-KEY-04 |
| "ignores Enter when interaction is disabled: %o" (disabled / readOnly) | CMB-KEY-04, CMB-KEY-15 |

### `popup/ComboboxPopup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root > Portal > Positioner) | CMB-STA-10, CMB-DOM-12; RND-DOM-36…49 |
| "exposes open state via data attributes mapping" | CMB-DOM-12, CMB-STA-10 |
| "sets role to presentation when input renders outside the popup" | CMB-ARIA-11, CMB-STR-03 |
| "sets role to dialog when input renders inside the popup" | CMB-ARIA-11, CMB-STR-03 |
| "focuses the popup instead of its input when opened by touch" | CMB-API-51, CMB-PTR-11, CMB-STA-31 |
| "honors initialFocus={false}" | CMB-API-51, CMB-FOC-03 |
| "returns focus to an explicitly provided element when the popup closes" | CMB-API-52 |

### `portal/ComboboxPortal.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root with `defaultOpen`) | CMB-API-49, CMB-DOM-21; RND-DOM-36…49 |

### `portal/ComboboxPortalContext.test.tsx`

| Test | Requirements |
|---|---|
| "throws a descriptive error when used outside <Combobox.Portal>" | CMB-STR-04 |

### `positioner/ComboboxPositioner.test.tsx`

| Test | Requirements |
|---|---|
| "should not lock body scroll when controlled value={[]} triggers a re-render" | CMB-POS-03, CMB-API-24 |
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root > Portal) | CMB-STA-09, CMB-DOM-13; RND-DOM-36…49 |
| positionerConformanceTests (Root > Input + Portal > Positioner > Popup; `render`) | CMB-API-50, CMB-DOM-13, CMB-POS-04; FLT positioner conformance |
| "keeps the popup on the preferred side when its capped height fits below tall content" (skipIf jsdom) | CMB-POS-02 |
| "uses the input when input group is absent" (skipIf jsdom) | CMB-API-50, CMB-POS-01 |
| "uses the input group when present" (skipIf jsdom) | CMB-API-50, CMB-POS-01 |

### `positioner/ComboboxPositionerContext.test.tsx`

| Test | Requirements |
|---|---|
| "throws a descriptive error when a required consumer is outside the positioner" | CMB-STR-04 |

### `root/ComboboxRootContext.test.tsx`

| Test | Requirements |
|---|---|
| "$name throws when its provider is missing" (useComboboxRootContext / useComboboxFloatingContext / useComboboxDerivedItemsContext) | CMB-STR-02 |

### `root/utils/index.test.ts`

| Test | Requirements |
|---|---|
| "derives an id only when the root has an id" | CMB-API-27, CMB-DOM-24 |
| "rejects nullish items and filters primitives and projected objects" | CMB-API-65, CMB-EDGE-05 |
| "shows all items for an empty query and rejects nullish items" | CMB-API-65 |
| "shows all items when the query exactly matches the selected label" | CMB-API-65 |
| "otherwise filters the current item" | CMB-API-65 |

### `root/utils/useFilter.test.tsx`

| Test | Requirements |
|---|---|
| "uses default options when called without arguments" | CMB-API-65 |
| "matches the start and end of an item label" | CMB-API-65 |
| "filters selected and unselected items in single and multiple modes" | CMB-API-65 |

### `status/ComboboxStatus.iOS.test.tsx`

| Test | Requirements |
|---|---|
| "skips the initial text mutation" | CMB-ARIA-13 |

### `status/ComboboxStatus.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Root > Portal > Positioner > Popup with `defaultOpen`) | CMB-STA-17; RND-DOM-36…49 |
| "renders only when open" | CMB-DOM-23, CMB-DOM-21 |
| "removes the initial text mutation after the reset delay" | CMB-ARIA-13 |
| "updates content immediately after the live region has mounted" | CMB-ARIA-13 |
| "preserves a custom render prop on the visible element" | CMB-ARIA-13 |
| "restores the marker before unmounting during the reset delay" | CMB-ARIA-13 |

### `store.test.tsx`

| Test | Requirements |
|---|---|
| "publishes an inline transition in a single store transaction" (`Autocomplete.Root`) | CMB-STA-34 (transaction shape only), CMB-API-23 |
| "settles inputOwnsFormValue through the useStore path when inline is set" (`Autocomplete.Root`) | engine `selectionMode: 'none'` only — see [autocomplete](autocomplete.md); §16 Q12 |
| "exposes real commands to a descendant ref callback on the first commit" (`Autocomplete.Root`) | CMB-STA-34, CMB-STR-01 |
| "gives form value ownership to the hidden input when the input is inside the popup" (`Autocomplete.Root`) | CMB-STR-03 (`inputInsidePopup` detection); engine ownership — §16 Q12 |
| "keeps form value ownership across hydration" (`Autocomplete.Root`; visible input owns) | engine `selectionMode: 'none'` only — see [autocomplete](autocomplete.md); §16 Q12 |

### `trigger/ComboboxTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`) | CMB-API-43, CMB-DOM-05; RND-DOM-36…49; UBT |
| "sets tabIndex to -1 when not used as the main anchor" | CMB-DOM-05, CMB-FOC-02 |
| "should render aria-disabled attribute when disabled" | CMB-API-44, CMB-ARIA-17 |
| "should inherit disabled state from ComboboxRoot" | CMB-API-44 |
| "should inherit disabled state from Field.Root" | CMB-API-44 |
| "should not open popup when disabled" | CMB-API-44, CMB-PTR-02 |
| "ignores native keydown events on a disabled non-native trigger" | CMB-API-43 |
| "should prioritize local disabled over root disabled" | CMB-API-44 |
| "validates the $name value when blurred" (single selection / no selection) | CMB-FRM-05, CMB-FOC-08 |
| "ignores a pending mouseup after the trigger unmounts" | CMB-PTR-03 |
| "does not select when closed typeahead matches a sparse label without a value" | CMB-KEY-12 |
| "applies the data-readonly style hook only when readOnly" | CMB-DOM-04 |
| "opens the popup when readOnly" | CMB-API-29, CMB-PTR-02 |
| "opens on $name when readOnly" (ArrowDown / ArrowUp / Enter / Space) | CMB-KEY-11, CMB-API-29 |
| "does not commit a value with typeahead on a closed trigger" | CMB-KEY-12, CMB-API-29 |
| "should not toggle when readOnly=false (control)" | CMB-PTR-02 |
| "should toggle popup when enabled" | CMB-PTR-02, CMB-DIS-06 |
| "should call onOpenChange when toggling" | CMB-PTR-02 |
| "opens popup when pressing ArrowDown or ArrowUp" | CMB-KEY-11 |
| "does not open on ArrowDown/ArrowUp when reference is a textarea" | CMB-KEY-11 |
| "fires with reason trigger-press when Trigger is clicked" | CMB-PTR-02, CMB-EVT-02 |
| "commits selection when the input is outside the popup" | CMB-PTR-03, CMB-PTR-04 |
| "commits selection when the input is inside the popup and the pointer is released over an item" | CMB-PTR-03, CMB-PTR-04 |
| "does not double-commit when a non-primary touch lands on another item" (skipIf jsdom) | CMB-PTR-04 |
| "commits a later drag-select onto an item whose earlier pointerdown never released" (skipIf jsdom) | CMB-PTR-04, CMB-STA-25 |
| "does not commit selection if the pointer never hovers the item" | CMB-PTR-04 |
| "closes the popup when mouseup occurs outside the trigger bounds" | CMB-PTR-03, CMB-DIS-08, CMB-EVT-02 |
| "keeps the popup open when mouseup remains near the trigger bounds" | CMB-PTR-03 |
| "closes the popup when the release is more than 5px outside the trigger bounds" | CMB-PTR-03 |
| "sets aria-required attribute when required (input inside popup)" | CMB-ARIA-07, CMB-API-28 |
| "does not set aria-required attribute when the input is outside the popup" | CMB-ARIA-07 |
| "sets aria-readonly attribute when readOnly (input inside popup)" | CMB-ARIA-07 |
| "does not set aria-readonly attribute when the input is outside the popup" | CMB-ARIA-07 |
| "sets all aria attributes on the input when closed" | CMB-ARIA-01 |
| "sets all aria attributes on the trigger when open" | CMB-ARIA-07 |
| "selects item when typing on focused trigger (input inside popup)" | CMB-KEY-12, CMB-FOC-10 |
| "selects item when typing after the popup has been opened and closed (items prop, strict: %s)" | CMB-KEY-12, CMB-EDGE-07 |
| "cycles to the next matching item when typing after open/close (no items prop, keepMounted %s)" | CMB-KEY-12, CMB-STA-32 |
| "cycles from the selected item after reordering items while closed (no items prop)" | CMB-KEY-12 |
| "only matches mounted indexed item labels when typing on the focused trigger" | CMB-KEY-12, CMB-STA-33 |
| "sets data-popup-side to the current popup side" (skipIf jsdom) | CMB-DOM-04, CMB-STA-09 |
| "toggles data-list-empty when the filtered list is empty" | CMB-DOM-04 |
| "has data-placeholder when no value is selected" | CMB-DOM-04, CMB-STA-07 |
| "does not have data-placeholder when value is selected" | CMB-STA-07 |
| "has data-placeholder when multiple mode has empty array" | CMB-STA-07 |
| "does not have data-placeholder when multiple mode has a default value" | CMB-STA-07, CMB-API-02 |

### `utils/handleInputPress.test.ts`

| Test | Requirements |
|---|---|
| "handles an event whose target is not an Element" | CMB-PTR-08 |

### `utils/parts.test.ts`

| Test | Requirements |
|---|---|
| "returns no index after removing the only chip" | CMB-KEY-09 |
| "does nothing when the highlighted item is not rendered" | CMB-KEY-04 |
| "clicks the rendered highlighted item with the originating event" | CMB-KEY-04 |

### `value/ComboboxValue.test.tsx`

| Test | Requirements |
|---|---|
| "renders current selected value via function child" | CMB-API-46 |
| "renders function child with null when no value selected" | CMB-API-46 |
| "renders function child with complex objects" | CMB-API-46 |
| "overrides the value display when children is a static ReactNode" | CMB-API-46 |
| "renders complex ReactNode children" | CMB-API-46 |
| "renders label from selected value object" | CMB-API-46, CMB-API-32 |
| "renders ReactNode label from selected value object" | CMB-API-46 |
| "handles selected value with null label" | CMB-API-46 |
| "renders null item label from items array when no value selected" | CMB-API-46, CMB-API-47 |
| "renders null item label when input is inside popup and no defaultValue" | CMB-API-46 |
| "displays the label from items array when value is selected" | CMB-API-46 |
| "updates the label when value changes with items array" | CMB-API-46 |
| "supports ReactNode labels in items array" | CMB-API-46 |
| "handles duplicate values in items array (uses first match)" | CMB-EDGE-08 |
| "is not stale after items are updated" | CMB-API-46 |
| "handles grouped items correctly" | CMB-API-46 |
| "handles null items in grouped structure" | CMB-API-46, CMB-EDGE-05 |
| "displays comma-separated labels from items array" | CMB-API-46 |
| "supports ReactNode labels for multiple selections" | CMB-API-46 |
| "falls back to raw values when items are not provided" | CMB-API-46 |
| "handles string values correctly" / "handles number values correctly" / "handles boolean values correctly" | CMB-API-46 |
| "uses custom itemToStringLabel function" | CMB-API-32, CMB-API-46 |
| "displays placeholder when no value is selected" | CMB-API-47 |
| "does not display placeholder when value is selected" | CMB-API-47 |
| "children prop takes precedence over placeholder" | CMB-API-47 |
| "children function takes precedence over placeholder" | CMB-API-47 |
| "null item label in items takes precedence over placeholder" | CMB-API-47 |
| "uses placeholder when items have null value without label" | CMB-API-47 |
| "supports ReactNode as placeholder" | CMB-API-47 |
| "displays placeholder when multiple mode has empty array" | CMB-API-47 |

### `root/ComboboxRoot.test.tsx`

| Test | Requirements |
|---|---|
| "exposes the orientation attribute rendered by Separator" | CMB-API-64, CMB-STA-15, CMB-DOM-16, CMB-STR-08 |
| popupConformanceTests (`triggerMouseAction: 'click'`, `expectedPopupRole: 'listbox'`, `combobox: true`, `openReason: input-press`, `exitAnimation: false`; the Input is the "trigger", the List receives the `popup` props) | CMB-API-05/06/07/08, CMB-STA-18, CMB-PTR-01, CMB-ARIA-01/07/08, CMB-DOM-24, CMB-ANI-01, CMB-EVT-01/06/07, CMB-DIS-02; PSH popup conformance |
| popupListConformanceTests (`itemRole: 'option'`, `focusModel: 'virtual'`, `homeEnd: false`, `selectable: true`, `spaceActivates: false`, `enterSpaceOpen: false`, `typeahead: false`, `modal: false`, `triggerClickCloses: false`, `listEnd: 'escape'`; function-children List) | CMB-KEY-01/02/03/04/06/11/16, CMB-PTR-04/06, CMB-ARIA-03/09, CMB-FOC-01, CMB-DIS-02/03/04/06, CMB-API-58, CMB-PTR-13, CMB-ANI-06; CMP list conformance |
| "does not select the highlighted item with an Enter that commits IME text" | CMB-KEY-14 |
| "does not move the highlight with arrow keys that pick an IME candidate" | CMB-KEY-14 |
| "automatically unmounts with an actions ref and completes closing once" | CMB-API-08, CMB-STA-30, CMB-ANI-03, CMB-EVT-06 |
| "keeps the popup mounted until the unmount action completes closing" | CMB-STA-29, CMB-ANI-03, CMB-EVT-06 |
| "clears the opt-out when a controlled reopen interrupts a pending unmount" | CMB-STA-29 |
| "ignores an opt-out on a canceled close" | CMB-STA-29 |
| "keeps the opt-out when a controlled close is applied in a transition" | CMB-STA-29 |
| "closes through the `close` action so `onOpenChange` can opt out" | CMB-API-36, CMB-DIS-08 |
| "ignores `unmount` while the popup is open" | CMB-API-36 |
| "unmounts when `close` and `unmount` are called in one batch" | CMB-API-36, CMB-ANI-03 |
| "still unmounts on a later close after `unmount` was called while open" | CMB-API-36 |
| "still unmounts on a later close after `unmount` and a reopen in one batch" | CMB-API-36 |
| "does not call `onOpenChange` when the `close` action is called while closed" | CMB-API-07, CMB-API-36 |
| "completes closing once when `unmount` is called twice in one batch" | CMB-API-08, CMB-EVT-06 |
| "sets combobox aria attributes on the input" (SSR) | CMB-ARIA-01, CMB-EDGE-06 |
| "sets combobox aria attributes on the trigger when input is inside popup" (SSR) | CMB-ARIA-07, CMB-EDGE-06 |
| "does not link Combobox.Label to trigger before hydration" (SSR) | CMB-ARIA-07, CMB-EDGE-06 |
| "does not focus input when closing via trigger click (input inside popup)" | CMB-PTR-02 |
| "selects with the keyboard, restores focus, and resets the query on reopen" | CMB-FOC-06, CMB-API-52, CMB-STA-30 |
| "discards an uncommitted query on Escape and preserves the selection" | CMB-DIS-02, CMB-FOC-06, CMB-STA-30 |
| "keeps filtering responsive when the selection close is canceled" | CMB-EVT-07, CMB-STA-21 |
| "clears a single-select query and restores the selection when reopening during the close animation" (skipIf jsdom) | CMB-ANI-02, CMB-STA-21 |
| "preserves a typed query when input reopens single-select during the close animation" (skipIf jsdom) | CMB-ANI-02, CMB-STA-21 |
| "does not aria-hide the input group when the input is outside the popup" | CMB-ARIA-15, CMB-FOC-03 |
| "dismisses the popup when clicking a plain wrapper around the input" | CMB-DIS-03 |
| "does not dismiss when pressing portalled content inside the popup but outside the list" | CMB-DIS-03 |
| "does not navigate the list with arrow keys from portalled controls inside the popup" | CMB-FOC-04, CMB-KEY-01 |
| "does not dismiss when pressing content inside a nested popover" | CMB-DIS-03 |
| "wraps modal focus from popup controls back to the input" | CMB-FOC-09, CMB-FOC-03, CMB-ARIA-14 |
| "does not cause infinite re-renders when items becomes undefined" | CMB-EDGE-04 |
| "hides the trigger when popup is open with input outside the popup" | CMB-ARIA-15 |
| "does not render the start dismiss button while closed" | CMB-STR-07, CMB-DOM-21 |
| "renders internal dismiss buttons before the input and after the popup" | CMB-STR-07, CMB-DOM-22, CMB-ARIA-14, CMB-FOC-09 |
| "renders an internal dismiss button for the input-outside-popup pattern" | CMB-STR-07 |
| "fires onOpenChange once with reason item-press on mouse click" | CMB-EVT-02, CMB-EVT-10, CMB-PTR-04 |
| "fires onOpenChange once with reason item-press on Enter selection" | CMB-EVT-02, CMB-KEY-04 |
| "should auto-close popup after selection when open state is uncontrolled" | CMB-PTR-05 |
| "syncs selected index when items change after close" | CMB-STA-25 |
| "should not auto-close popup when open state is controlled" | CMB-API-01, CMB-API-05 |
| "should show all items when query is empty with enhanced filter" | CMB-API-14 |
| "should show all items when query matches current selection" | CMB-STA-22 |
| "should reset input value to selected value when popup closes without selection" | CMB-STA-22, CMB-STA-30 |
| "should not auto-close during browser autofill" | CMB-EVT-11 |
| "highlights and scrolls the selected item into view on first open (no items prop)" (skipIf jsdom) | CMB-PTR-13, CMB-KEY-02 |
| "highlights and scrolls the selected item into view on mount when inline (items prop)" (skipIf jsdom) | CMB-STA-28 |
| "does not restore the initial highlight after focus leaves an inline input (items prop)" (skipIf jsdom) | CMB-STA-28 |
| "does not apply the initial highlight when a selection resolves after blur (items prop)" (skipIf jsdom) | CMB-STA-28 |
| "does not snap the highlight to a stale index while filtering an inline list (items prop)" (skipIf jsdom) | CMB-STA-27 |
| "does not transfer a stale highlight when clearing an inline list without a selection (items prop)" (skipIf jsdom) | CMB-STA-26 |
| "waits for a controlled query clear to commit before restoring the selection (items prop)" (skipIf jsdom) | CMB-STA-26 |
| "highlights the first selected item on mount and keeps the highlight when toggling in a multiple inline list (items prop)" (skipIf jsdom) | CMB-STA-28 |
| "does not highlight a non-selected item when an initial input value pre-filters the inline list (items prop)" (skipIf jsdom) | CMB-STA-28 |
| "does not replace a pointer highlight when the initial selection resolves after mount (items prop)" (skipIf jsdom) | CMB-STA-28 |
| "does not restore the initial highlight after pointer navigation is cleared (items prop)" (skipIf jsdom) | CMB-STA-28 |
| "emits the selected item index to onItemHighlighted on mount when inline and virtualized (items prop)" (skipIf jsdom) | CMB-STA-28, CMB-EVT-05 |
| "restores the highlight and scroll to the selected item when the query is cleared" (skipIf jsdom) | CMB-STA-26, CMB-PTR-13 |
| "restores the selected direct item after its registration index changes" (skipIf jsdom) | CMB-STA-25 |
| "does not navigate kept-mounted items while controlled closed" | CMB-API-48, CMB-ANI-01, CMB-ANI-06, CMB-KEY-01 |
| "restores the selected item with the input outside the popup" | CMB-STA-26 |
| "scrolls to programmatic highlights after the filtered list changes" (skipIf jsdom) | CMB-PTR-13 |
| "restores the selected item instead of the first item when clearing with autoHighlight" | CMB-STA-26 |
| "emits onItemHighlighted for the selected item when the query is cleared" | CMB-STA-24, CMB-STA-26 |
| "restores the selected item through an externally virtualized list" (skipIf jsdom) | CMB-API-22, CMB-PTR-13 |
| "consumes a query-clear restore when the selected item is absent" | CMB-STA-26 |
| "clears selectedIndex when the value is cleared externally while closed (no items prop)" | CMB-STA-25 |
| "clears selectedIndex when the value is set to an unmatched value while closed (no items prop)" | CMB-STA-25, CMB-EDGE-11 |
| "registers selectedIndex before opening with a keepMounted portal (no items prop)" | CMB-STA-25, CMB-API-48 |
| "does not highlight the previous item after the value changes while open (no items prop)" | CMB-STA-25 |
| "re-highlights the selection on reopen when the value object is recreated with equal contents (no items prop)" | CMB-STA-25, CMB-EDGE-11 |
| "should handle multiple selection" | CMB-API-04, CMB-EVT-03 |
| "opens with an empty input and highlights the first selected item (input inside popup)" | CMB-STA-19, CMB-STA-25 |
| "anchors the highlight to the first selected item in rendered order regardless of value order" | CMB-STA-25 |
| "anchors the highlight to the first selected item with individually rendered items" | CMB-STA-25 |
| "anchors to the first selected item in rendered order across groups" | CMB-STA-25 |
| "keeps the highlight on an item a controlled value refused to select" | CMB-API-01, CMB-EVT-07 |
| "anchors to the first selected item when opened with the keyboard" | CMB-KEY-02 |
| "continues arrow navigation from the anchor rather than the top of the list" | CMB-KEY-02 |
| "keeps the toggled item highlighted when selecting with the keyboard while filtering" | CMB-STA-26 |
| "moves the anchor when the anchor item value changes" | CMB-STA-27 |
| "moves the anchor to the next selected item when the anchor item leaves the list" | CMB-STA-27 |
| "restores the anchor rather than the toggled item when deselecting while filtering" | CMB-STA-26 |
| "restores the highlight to the first selected item when clearing (input inside popup)" | CMB-STA-26 |
| "keeps the popup open and highlights the newly selected item after clearing its query" | CMB-STA-26, CMB-EVT-10 |
| "keeps the toggled item highlighted when deselecting a selected item" | CMB-PTR-05 |
| "clears the highlight when filtering deselects the only selected item" | CMB-STA-27 |
| "clears a closing query and restores the first selected item on reopen" | CMB-STA-30 |
| "scrolls the first selected item into view when clearing (input inside popup)" (skipIf jsdom) | CMB-PTR-13 |
| "restores the highlight when clearing with the input outside the popup" | CMB-STA-26 |
| "resets selectedIndex when clearing all selections while open" | CMB-STA-25, CMB-PTR-07 |
| "re-syncs selectedIndex after an external controlled update when closing" | CMB-STA-25 |
| "clears selectedIndex when all values are removed externally while closed (no items prop)" | CMB-STA-25 |
| "should create multiple hidden inputs for form submission" | CMB-DOM-19, CMB-API-25 |
| "does not submit multiple values when disabled" | CMB-DOM-19, CMB-API-30 |
| "should handle disabled state with chips" | CMB-API-30 |
| "should handle readOnly state with chips" | CMB-API-29 |
| "focuses first item on ArrowDown and last item on ArrowUp" | CMB-KEY-02 |
| "opens, navigates with ArrowDown, and Enter selects" | CMB-KEY-02, CMB-KEY-04, CMB-ARIA-03 |
| "Enter selects with manual indices provided to items" | CMB-API-57, CMB-KEY-04 |
| "clicking on \"listbox\" keeps the focus on the input" | CMB-FOC-01, CMB-FOC-04, CMB-PTR-04 |
| "Escape closes the popup without committing when nothing highlighted" | CMB-DIS-02 |
| "bubbles Escape key when rendered inline without Positioner/Popup" | CMB-STR-05, CMB-DIS-02, CMB-DIS-05 |
| "keeps input value on Enter when inline and no item is highlighted" | CMB-STR-05, CMB-API-23, CMB-KEY-04 |
| "bubbles Escape key when list is empty and popup hidden with CSS" | CMB-DIS-02 |
| "does not bubble Escape key when Empty component is present" | CMB-DIS-02 |
| "sets all aria attributes on the input when closed" | CMB-ARIA-01, CMB-DOM-01, CMB-STR-01 |
| "sets all aria attributes on the input when open" | CMB-ARIA-01, CMB-DOM-08 |
| "sets aria-expanded on the input when rendered inline and open" | CMB-ARIA-02, CMB-API-23 |
| "sets the popup type on the input when rendered inline as a grid" | CMB-ARIA-01, CMB-API-21 |
| "keeps the input expanded when rendered inline without the `open` prop" | CMB-ARIA-02, CMB-STR-05 |
| "sets correct attributes on the item when highlighted" | CMB-ARIA-03, CMB-ARIA-09, CMB-DOM-09, CMB-STA-13, CMB-API-59 |
| "sets aria-controls to the dialog popup on trigger" | CMB-ARIA-07, CMB-DOM-24 |
| "points aria-controls at a custom popup id on the trigger" | CMB-DOM-24 |
| "points aria-controls at a popup id supplied through the render prop" | CMB-DOM-24 |
| "should handle browser autofill" | CMB-EVT-11, CMB-EVT-03 |
| "ignores hidden-input autofill when $lockState $label" (readOnly/disabled × inside/outside Field) | CMB-EVT-11 |
| "shows all items when opening after browser autofill" | CMB-EVT-11 |
| "does not open or filter the popup on browser autofill with insertReplacementText" | CMB-EVT-11, CMB-KEY-17 |
| "should handle browser autofill with object values" | CMB-EVT-11 |
| "should handle browser autofill with object values when autofill uses the label" | CMB-EVT-11 |
| "matches browser autofill against an item rendered label for primitive values regardless of case" | CMB-EVT-11 |
| "matches browser autofill by serialized value before an earlier rendered label" | CMB-EVT-11 |
| "matches browser autofill when an earlier item has no rendered label" | CMB-EVT-11, CMB-STA-32 |
| "matches browser autofill against an item rendered label when using the items prop" | CMB-EVT-11 |
| "does not force-mount the list when autofill matches a serialized value with the items prop" | CMB-STA-32 |
| "marks the field dirty and validates after successful autofill" | CMB-FRM-03, CMB-EVT-11 |
| "does not update field state when autofill is canceled" | CMB-EVT-11, CMB-EVT-03 |
| "does not mark the field dirty when input autofill is canceled" | CMB-EVT-11 |
| "should pass autoComplete to the hidden input" | CMB-API-31, CMB-DOM-18, CMB-FRM-08 |
| "applies scroll lock when a touch-opened popup covers the viewport width" (skipIf jsdom) | CMB-API-24, CMB-PTR-11, CMB-STA-31, CMB-POS-03 |
| "does not apply scroll lock when a touch-opened popup is narrower than the viewport" (skipIf jsdom) | CMB-API-24, CMB-PTR-11 |
| "does not open on programmatic input events" | CMB-KEY-17 |
| "opens on paste input events" | CMB-KEY-17 |
| "sets the id on the input when it is outside the popup" | CMB-API-27, CMB-API-39 |
| "sets the id on the trigger when the input is inside the popup" | CMB-API-27, CMB-API-45 |
| "should render disabled state on all interactive components" | CMB-API-30, CMB-STA-02, CMB-DOM-03, CMB-ARIA-17 |
| "should not open popup when disabled" | CMB-API-30 |
| "should prevent keyboard interactions when disabled" | CMB-API-30, CMB-KEY-15 |
| "should set disabled attribute on hidden input" | CMB-API-30, CMB-DOM-18 |
| "does not mark the hidden input as required when selection exists in multiple mode" | CMB-API-28 |
| "keeps the hidden input required when no selection exists in multiple mode" | CMB-API-28, CMB-DOM-18 |
| "should render readOnly state on the input" | CMB-API-29 |
| "should expose aria-autocomplete=\"none\" when readOnly" | CMB-ARIA-04, CMB-API-29 |
| "should open the popup from the input and the trigger when readOnly" | CMB-API-29, CMB-PTR-01 |
| "should browse the items with the arrow keys when readOnly" | CMB-API-29, CMB-KEY-02 |
| "should not change the input value when typing while readOnly" | CMB-API-29, CMB-KEY-15 |
| "should set readOnly attribute on hidden input" | CMB-DOM-18 |
| "should prevent value changes when readOnly with items" | CMB-API-29 |
| "should not submit the form with Enter on a highlighted item when readOnly" | CMB-API-29, CMB-KEY-15 |
| "should report arrow-key highlights as keyboard-driven when readOnly" | CMB-API-29, CMB-KEY-01, CMB-EVT-05 |
| "should not commit a value with Enter on a highlighted item when readOnly" | CMB-API-29, CMB-KEY-15 |
| "uses itemToStringLabel for input value synchronization" | CMB-API-32, CMB-STA-20 |
| "shows the label for a controlled object value not in items" | CMB-API-32, CMB-EDGE-11 |
| "uses itemToStringValue for form submission" | CMB-API-33 |
| "uses itemToStringValue for multiple selection form submission" | CMB-API-33, CMB-DOM-19 |
| "derives input from defaultValue on first mount when unspecified" | CMB-API-02, CMB-API-10, CMB-STA-19 |
| "derives input from defaultValue on first mount with items prop" | CMB-API-12, CMB-STA-19 |
| "derives input from controlled value on first mount when unspecified" | CMB-STA-19 |
| "defaultInputValue overrides derivation when provided" | CMB-API-10, CMB-STA-19 |
| "inputValue overrides derivation when provided" | CMB-API-09, CMB-STA-19 |
| "multiple mode initial input remains empty" | CMB-API-02, CMB-STA-19 |
| "does not set input value for input-inside-popup pattern" | CMB-STA-19 |
| "updates derived input when controlled value changes externally" | CMB-STA-20 |
| "does not force-mount the list when controlled value changes externally (strict: %s)" | CMB-STA-32, CMB-EDGE-07 |
| "re-derives input when items array changes" | CMB-STA-20 |
| "restores the selected label after a one-step clear and items reload" | CMB-STA-20 |
| "reports a single input value change when the controlled value changes" | CMB-API-11, CMB-STA-20, CMB-EVT-04 |
| "syncs again after a canceled sync to the same label" | CMB-API-11, CMB-STA-20 |
| "reports a single input value change when the selection is remapped" | CMB-STA-20 |
| "re-syncs the input when the resolved label of an unchanged value changes" | CMB-STA-20 |
| "restores derived input after items load asynchronously" | CMB-STA-20, CMB-EDGE-04 |
| "shows every item when a controlled reopen interrupts a canceled mirrored-selection clear" (skipIf jsdom) | CMB-STA-21 |
| "keeps the typed filter when items change afterwards (input outside popup)" (skipIf jsdom) | CMB-STA-21 |
| "keeps filtering when typing reopens the popup (input outside popup)" (skipIf jsdom) | CMB-STA-21 |
| "does not re-emit a clear when closing again without typing (multiple, input outside popup)" (skipIf jsdom) | CMB-EVT-04 |
| "keeps a keystroke typed into an empty input when items refresh synchronously" (skipIf jsdom) | CMB-STA-21 |
| "releases the frozen query when controlled open reopens the popup" (skipIf jsdom) | CMB-STA-21 |
| "unfreezes filtering when a controlled open ignores the close request (multiple, input outside popup)" | CMB-STA-21 |
| "opens blank when the input only mirrors the selection (single, input inside popup)" (skipIf jsdom) | CMB-STA-21 |
| "keeps an input value set in the same batch as a controlled reopen (input inside popup)" (skipIf jsdom) | CMB-STA-21 |
| "shows every item when selection reopens the popup" (skipIf jsdom) | CMB-STA-21, CMB-STA-22 |
| "does not render aria-orientation on the listbox role" | CMB-ARIA-08 |
| "sets grid roles when grid is enabled and rows are used" | CMB-API-21, CMB-ARIA-09, CMB-ARIA-10 |
| "arrow keys navigate across rows and columns in grid mode" | CMB-API-21, CMB-KEY-13 |
| "does not re-report the highlight on keys the grid does not handle" | CMB-KEY-13 |
| "moves the input caret on ArrowLeft when no item is highlighted in grid mode" | CMB-KEY-13 |
| "moves the input caret on ArrowRight when no item is highlighted in grid mode" | CMB-KEY-13 |
| "keeps grid navigation when autoHighlight surfaces an item before typing arrow keys" | CMB-KEY-13 |
| "mirrors horizontal grid navigation in RTL mode" | CMB-API-21, CMB-KEY-13 |
| "supports uneven rows navigation" | CMB-API-21, CMB-KEY-13 |
| "supports uneven rows navigation within groups" | CMB-API-21, CMB-KEY-13 |
| "does not render aria-orientation on the grid role" | CMB-ARIA-08 |
| "renders groups with the rowgroup role" | CMB-API-21, CMB-ARIA-10 |
| "\"single\" selects and closes, then reopens with selection focused" | CMB-API-04, CMB-STA-22 |
| "\"single\" keeps the popup open when selection is canceled" | CMB-API-03, CMB-STA-18, CMB-EVT-07 |
| "\"multiple\" clears uncontrolled input after select when filtering" | CMB-PTR-05 |
| "keeps the active item when the post-selection input clear is canceled" | CMB-API-03, CMB-EVT-07 |
| "keeps the filter text when the \"item-press\" close is canceled (input outside popup)" | CMB-EVT-07, CMB-PTR-05 |
| "marks the close-path clear as an item press only when an item press closed the popup (input outside popup)" | CMB-EVT-04, CMB-EVT-08, CMB-PTR-05 |
| "keeps the filter text when the selection clear is canceled (input inside popup)" | CMB-EVT-07, CMB-PTR-05 |
| "keeps the filter text in an inline list when the selection clear is canceled" | CMB-EVT-07, CMB-PTR-05 |
| "still clears the kept filter text once the popup closes" | CMB-STA-30, CMB-EVT-07 |
| "clears the input with the \"input-clear\" reason and the item click event on pointer selection" | CMB-EVT-04, CMB-EVT-08, CMB-PTR-05 |
| "clears the input with the \"input-clear\" reason and the originating keydown event on keyboard selection" | CMB-EVT-04, CMB-PTR-05, CMB-KEY-04 |
| "keeps the filter text when a drag-select release commits the selection" | CMB-PTR-04 |
| "clears the pending filter with the \"input-clear\" reason when reopening interrupts the close animation" (skipIf jsdom) | CMB-STA-21 |
| "releases the frozen query when reopening during the close animation with the input outside the popup" (skipIf jsdom) | CMB-STA-21 |
| "does not close popup when filtering with input inside popup in multiple mode" | CMB-PTR-05 |
| "keeps the popup input focused through keyboard selection and restores the last selection" | CMB-FOC-06 |
| "keeps filtered popup content stable while closing with input inside popup" (skipIf jsdom) | CMB-STA-21, CMB-ANI-02 |
| "keeps filtered popup content stable when input changes during the close animation" (skipIf jsdom) | CMB-STA-21, CMB-ANI-02 |
| "clears the deferred popup input when reopening during close animation" (skipIf jsdom) | CMB-STA-21, CMB-ANI-02 |
| "does not emit another clear when controlled input is already empty on interrupted close" (skipIf jsdom) | CMB-STA-21 |
| "\"multiple\" clears typed input on close when no selection made" | CMB-STA-30 |
| "does not clear popup input after canceled multiple value changes" | CMB-API-03, CMB-STA-30, CMB-EVT-07 |
| "\"single\" clears typed input on close when no selection made (input outside popup)" | CMB-STA-30 |
| "uses custom filter to narrow results" | CMB-API-14, CMB-STA-23 |
| "resets filtered results after selecting when using a custom search stringifier" | CMB-API-14 |
| "resets external filteredItems when reopening after a selection" | CMB-API-13, CMB-STA-22 |
| "uses filteredItems when items prop is omitted" | CMB-API-13, CMB-STA-23 |
| "highlights the externally filtered item order when filtering reorders items" | CMB-API-13 |
| "opens a reordered external list at the selected value in rendered-list coordinates" | CMB-STA-25 |
| "resets an initially empty external result when opening a grouped selection" | CMB-STA-22 |
| "keeps the popup open on a second input click by default" | CMB-API-20, CMB-PTR-01, CMB-DIS-06 |
| "does not open on input click when false, but opens on typing" | CMB-API-20, CMB-KEY-17 |
| "resets the list scroll position to the top when the query changes" (skipIf jsdom) | CMB-PTR-12 |
| "resets manually filtered children without deferring the reset until hover" (skipIf jsdom) | CMB-PTR-12 |
| "resets the scroll position of a scrollable wrapper around the list" (skipIf jsdom) | CMB-PTR-12 |
| "skips clipping ancestors when finding the scroll container" (skipIf jsdom) | CMB-PTR-12 |
| "keeps the auto-highlighted first item in view after the query changes" (skipIf jsdom) | CMB-PTR-12 |
| "resets the scroll container when filtering reorders retained items" (skipIf jsdom) | CMB-PTR-12 |
| "resets only the nearest scrollable wrapper when composed in a scrollable dialog" (skipIf jsdom) | CMB-PTR-12 |
| "does not reset a surrounding dialog" (skipIf jsdom) | CMB-PTR-12 |
| "highlights a candidate arriving after typing with %s" (items / filteredItems) | CMB-STA-26, CMB-API-17 |
| "waits for a controlled popup to open" | CMB-STA-26 |
| "discards the request when a rejected typed open is followed by a controlled open" | CMB-STA-26, CMB-EVT-07 |
| "keeps a hovered item highlighted when individually rendered items arrive" | CMB-STA-26 |
| "highlights the first result when typing filters out individually rendered items" | CMB-STA-26 |
| "discards the request when an ignored typed open is followed by another open" | CMB-STA-26 |
| "discards the request when a rejected typed open is followed by opening with %s" (Trigger / ArrowDown) | CMB-STA-26 |
| "discards the request when the controlled input is cleared" | CMB-STA-26, CMB-API-09 |
| "discards the request when a controlled close is interrupted" (skipIf jsdom) | CMB-STA-26 |
| "does not reset keyboard navigation when candidates change" | CMB-STA-26 |
| "discards the request when autoHighlight is disabled" | CMB-STA-26 |
| "does not auto-highlight on initial open when no selection" | CMB-API-17 |
| "shows the selected item as selected on initial open (no active highlight)" | CMB-API-17, CMB-ARIA-09 |
| "highlights the first matching item after typing (single mode)" | CMB-API-17, CMB-KEY-17 |
| "highlights the first matching item after IME composition" | CMB-API-17, CMB-KEY-14 |
| "highlights the first matching item for a static list without the items prop" | CMB-API-17 |
| "keeps gridcell typeahead active across Space in row mode without selecting" | CMB-KEY-13 |
| "does not run follow-up state changes after canceled input value changes" | CMB-API-11, CMB-EVT-07 |
| "retains highlight when query is cleared back to empty" | CMB-API-17, CMB-STA-26 |
| "retains highlight when clearing the query with input-change behavior" | CMB-STA-26 |
| "highlights the first matching item after typing (multiple mode)" | CMB-API-17 |
| "clears highlight after removing the highlighted chip while filtering (multiple mode)" | CMB-KEY-07 |
| "keeps the active item highlighted after clearing the last selected value" | CMB-KEY-07 |
| "does not shift highlight to the previous selected item on Enter deselect" | CMB-KEY-17 |
| "continues ArrowDown navigation from the Enter-selected item (multiple mode)" | CMB-KEY-02 |
| "clears active highlight when removing the highlighted chip item" | CMB-KEY-07 |
| "keeps highlight in sync after selecting then backspacing to a single match" | CMB-KEY-17 |
| "navigates on first ArrowDown after editing selection to a new matching query" | CMB-KEY-02 |
| "updates highlighted callback with newly filtered first item" | CMB-STA-24, CMB-STA-27 |
| "fires a single clearing highlight on Enter selection" | CMB-STA-24, CMB-EVT-05 |
| "fires on keyboard navigation" | CMB-API-37, CMB-EVT-05 |
| "passes native mouse and pointer events for pointer highlights" | CMB-API-37, CMB-EVT-05, CMB-PTR-06 |
| "reports a pointer highlight after keyboard navigation" | CMB-EVT-05, CMB-PTR-06 |
| "reports no keyboard reason when typing clears the highlight" | CMB-EVT-05, CMB-KEY-17 |
| "reports no keyboard reason when the clear button clears the highlight" | CMB-EVT-05, CMB-PTR-07 |
| "returns the highlight to the selected item when the input is cleared" | CMB-STA-26 |
| "returns the highlight to the selected item without the items prop" | CMB-STA-26 |
| "restores an array-valued single selection when the query is cleared" | CMB-STA-26 |
| "does not highlight anything when there is no selected item" | CMB-STA-26 |
| "controls the open state" | CMB-API-05, CMB-STA-18 |
| "keeps filtering responsive after selection when inline and open is controlled" | CMB-API-23 |
| "releases filtering when a controlled popup ignores a close request" | CMB-API-05, CMB-STA-21 |
| "keeps filtered content stable when a controlled close is deferred" (skipIf jsdom) | CMB-ANI-02 |
| "fires when opening and closing" | CMB-API-07, CMB-EVT-01 |
| "keeps the selected index unset when the selected item is outside the rendered limit" | CMB-API-16 |
| "limits the number of items displayed when no groups are used" | CMB-API-16 |
| "limits the number of items displayed when groups are used" | CMB-API-16, CMB-API-12, CMB-STA-23 |
| "respects limit when filtering items" | CMB-API-16 |
| "stops filtering grouped items once the limit is reached" | CMB-API-16 |
| "shows all items when limit is -1 (default)" | CMB-API-16 |
| "handles limit of 0 gracefully" | CMB-API-16 |
| "preserves order of items when applying limit across groups" | CMB-API-16, CMB-STA-23 |
| "does not limit items when not using items prop" | CMB-API-16 |
| "updates displayed items when limit changes" | CMB-API-16 |
| "clears the highlight when a keepMounted dialog closes (multiple=%s, query=\"%s\")" (×4) | CMB-STA-26 |
| "clears the highlight when an inline list in an unbound dialog unmounts" | CMB-FOC-11 |
| "does not let pending dialog initial focus steal focus from an async-enabled combobox" | CMB-FOC-11 |
| "clears input after filtering, removes filter and highlight" (dialog, multiple) | CMB-PTR-05, CMB-STA-26 |
| "still filters after selecting an item" (dialog, multiple) | CMB-STA-21 |
| "retains highlight on selected item when not filtering" (dialog, multiple) | CMB-PTR-05 |
| "closes the dialog after selecting an item and updates the trigger value" (dialog, single) | CMB-FOC-06, CMB-API-46 |
| "clears the filter input when re-opening after a selection" (dialog, single) | CMB-STA-30 |
| "restores highlight when the input regains focus" (dialog, single) | CMB-STA-28, CMB-API-23 |
| "submits stringified value to onFormSubmit when itemToStringValue is provided" | CMB-FRM-01, CMB-API-33 |
| "submits to an external form when `form` is provided" (skipIf jsdom) | CMB-API-26, CMB-FRM-02 |
| "submits multiple values to an external form when `form` is provided" (skipIf jsdom) | CMB-API-26, CMB-FRM-02 |
| "serializes {value,label} objects using their value field" | CMB-API-33, CMB-DOM-18 |
| "serializes multiple {value,label} objects into multiple hidden inputs" | CMB-DOM-19 |
| "falls back to itemToStringValue when object lacks value" | CMB-API-33 |
| "triggers native HTML validation on submit" | CMB-FRM-02, CMB-DOM-18 |
| "focuses trigger and surfaces errors when input is inside popup" | CMB-FOC-05, CMB-FRM-02, CMB-ARIA-06, CMB-DOM-20 |
| "submits when input renders a field-aware input" | CMB-FRM-01, CMB-FRM-06 |
| "submits when input inside popup renders a field-aware input" | CMB-FRM-01 |
| "clears external errors on change" | CMB-FRM-03 |
| "submits on Enter when no item is highlighted (does not prevent)" | CMB-KEY-04, CMB-FRM-07 |
| "prevents submit on Enter when an item is highlighted" | CMB-KEY-04, CMB-FRM-07 |
| "should receive disabled prop from Field.Root" | CMB-API-30, CMB-FRM-06 |
| "should receive name prop from Field.Root" | CMB-API-25, CMB-FRM-01 |
| "Field.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id" | CMB-API-45, CMB-ARIA-16, CMB-FRM-06 |
| "does not apply validation ARIA attributes to input inside popup" | CMB-STA-05, CMB-ARIA-06 |
| "Combobox.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id" | CMB-API-45, CMB-ARIA-16 |
| "Combobox.Label focuses trigger without opening when input is inside popup" | CMB-DOM-17 |
| "[data-touched]" | CMB-DOM-02, CMB-FRM-05 |
| "[data-dirty]" | CMB-DOM-02, CMB-FRM-03 |
| "removes [data-dirty] in multiple mode after returning to the initial value" | CMB-FRM-03 |
| "removes [data-dirty] when an equal object value is reselected (different reference)" | CMB-FRM-03 |
| "keeps [data-dirty] when the same values are reselected in a different order" | CMB-FRM-03 |
| "adds [data-filled] attribute when filled" | CMB-DOM-02, CMB-FRM-04 |
| "adds [data-filled] attribute when already filled" | CMB-FRM-04 |
| "[data-focused]" | CMB-DOM-02, CMB-FOC-08 |
| "is removed when the focused input becomes disabled" | CMB-FOC-08 |
| "is removed when the focused input unmounts" | CMB-FOC-08 |
| "is removed when the focused trigger becomes disabled" | CMB-FOC-08 |
| "is removed when the focused trigger unmounts" | CMB-FOC-08 |
| "does not mark as touched when focus moves into the popup" | CMB-FOC-08, CMB-FRM-05 |
| "validates when the popup is blurred" | CMB-FOC-08, CMB-DIS-04, CMB-FRM-05 |
| "[data-invalid]" | CMB-DOM-02, CMB-FRM-05 |
| "[data-valid]" | CMB-DOM-02, CMB-FRM-05 |
| "prop: validate" | CMB-FRM-05 |
| "passes raw value to validate when itemToStringValue is provided" | CMB-API-33, CMB-FRM-05 |
| "prop: validationMode=onSubmit" | CMB-FRM-05 |
| "prop: validationMode=onChange" (skipIf !jsdom) | CMB-FRM-03 |
| "prop: validationMode=onBlur" (skipIf !jsdom) | CMB-FRM-05 |
| "Field.Label" | CMB-API-39, CMB-ARIA-16, CMB-FRM-06 |
| "Combobox.Label does not label Combobox.Input and warns when input is the form control" | CMB-STR-10 |
| "does not set fallback aria-labelledby when no label is rendered" | CMB-ARIA-16 |
| "updates Combobox.Label linkage when root id changes" | CMB-ARIA-16 |
| "Field.Description" | CMB-ARIA-16, CMB-FRM-06 |
| "matches object values using the provided comparator" | CMB-API-34 |
| "properly deselects object values using the provided comparator" | CMB-API-34 |
| "passes item as the first comparator argument in multiple mode" | CMB-API-34 |
| "does not call comparator with null when clearing the value" | CMB-API-34 |
| "does not call comparator with undefined when items load asynchronously after opening" | CMB-API-34 |
| "keeps showing items after selecting in controlled input-inside-popup async load flow" | CMB-API-34, CMB-STA-22 |
| "highlights an item on mouse move by default" | CMB-API-18, CMB-PTR-06 |
| "does not highlight items from mouse movement when disabled" | CMB-API-18, CMB-PTR-06 |
| "loops focus from last to first item with ArrowDown by default" | CMB-API-19, CMB-KEY-03 |
| "loops focus from first to last item with ArrowUp by default" | CMB-API-19, CMB-KEY-03 |
| "does not loop focus from last to first with ArrowDown when loopFocus={false}" | CMB-API-19, CMB-KEY-03 |
| "does not loop focus from first to last with ArrowUp when loopFocus={false}" | CMB-API-19, CMB-KEY-03 |
| "allows an attempted open to be canceled" | CMB-STA-18, CMB-EVT-07 |
| "handles closing after a changed query is cleared" | CMB-STA-21 |
| "clears a multiple inline query on close" | CMB-STA-30 |
| "normalizes a controlled null value when selecting in multiple mode" | CMB-API-01, CMB-EDGE-11 |
| "normalizes a controlled null value while closed in multiple mode" | CMB-API-01, CMB-EDGE-11 |
| "does not seed an initial highlight for an unmatched inline value" | CMB-API-23, CMB-STA-28 |
| "seeds the initial highlight for a matched inline value" | CMB-API-23, CMB-STA-28 |
| "restores the first highlight after an always-highlighted query becomes empty" | CMB-STA-26 (engine `'always'` mode; Autocomplete) |
| "omits groups with no matching items" | CMB-STA-23 |
| "exposes an unmount action that is ignored while open" | CMB-API-36 |
| "moves focus from the hidden control to an external input" | CMB-FOC-05 |
| "falls back to the trigger when the hidden control is focused" | CMB-FOC-05 |
| "falls back to the trigger after an external input unmounts" | CMB-FOC-05 |
| "moves hidden-control focus to the trigger when the input is inside the popup" | CMB-FOC-05 |
| "safely handles hidden-control focus without a visible control" | CMB-FOC-05 |
| "ignores scalar browser autofill in multiple mode" | CMB-EVT-11 |
| "ignores unmatched browser autofill in single mode" | CMB-EVT-11 |
| "does not request form submission when no form owns the autocomplete" | CMB-FRM-07 |
| "does not request form submission when requestSubmit is unavailable" | CMB-FRM-07 |
| "validates the autocomplete input when focus leaves its popup" | CMB-FOC-08 (Autocomplete mode) |
| "should navigate between combobox and composite items" | CMB-EDGE-09 |

Test-environment notes: `isJSDOM` gates browser-only assertions (layout, scrolling, animations, real pointer events); the `strict` / `keepMounted` `it.each` variants assert that React Strict Mode double effects and kept-mounted portals do not change behavior; the conformance suites are shared with Select/Menu (`popupListConformanceTests`) and with every popup (`popupConformanceTests`, `positionerConformanceTests`).

## 14. React-specific notes and porting considerations

Non-normative.

- **Two timing phases plus two deferrals.** Most derived-state writes happen *post-commit pre-paint* (React layout effects): the store sync (CMB-STA-34), selected-index sync (CMB-STA-25), the pending-highlight resolution and highlight re-emission (CMB-STA-26/27), `filled` (CMB-FRM-04), `popupSide`/`popupId` publication, and the values/list registries. Two things are deferred further: the query-clear highlight restore runs in a **microtask** (so re-mounted items have published their indexes), and the empty-list highlight reset runs **after paint** (passive effect). Hidden-input autofill is handled in a microtask; the Trigger's focus-driven `forceMount` runs on a 0 ms macrotask; the touch open delay is 100 ms; the live-region marker is restored after 200 ms. A port must preserve these orderings (DOM committed → registries updated → index/highlight derived → paint) or the `onItemHighlighted` sequences in the tests will differ. Port: see decisions/01-hosting-model-and-timing.md §Decision (timing-term mapping) and §Relaxation table (CMP-STA-10, UTL-STA-19..22).
- **Synchronous flushes.** Chip removal by keyboard and the chip ArrowDown/ArrowUp open use `flushSync` so that the chip list / the popup Input exist before focus moves (CMB-KEY-09). `handleSelection` is flushed synchronously only for the engine's `submitOnItemClick` (never for Combobox). Port: see decisions/01-hosting-model-and-timing.md §Decision ("committed synchronously" row) and §Relaxation table.
- **Layout decision by ancestry.** `inputInsidePopup` is detected by the Input reading an optional Positioner context (and by `inline`). A port without React context must compute "is the input element a descendant of the positioner element" at registration time and re-evaluate when either element changes; the initial value is `true` (server HTML assumes the hidden input owns the form value). Port: see decisions/05-state-store-and-event-details.md §1 (store cascaded fixed; parts subscribe).
- **Store with selectors.** Parts subscribe to individual store fields; `isActive(index)`/`isSelected(value)` are per-item selectors so only the affected items re-render on highlight/selection changes. `inputValue` is deliberately *not* in the store (it is provided through a separate context) to avoid re-rendering every subscriber on each keystroke; the Input reads it from that context. Port: see decisions/05-state-store-and-event-details.md §1 (store and selectors with arguments ported).
- **Memoized items and prop bags.** `Combobox.Item` is `React.memo`; the List memoizes the implicit Collection so items do not re-render on active-index changes; the virtualized-index wrapper isolates the per-keystroke derived-items subscription. The `inputProps`/`listProps`/`popupProps`/`itemRoot` bags are memoized merges of the FLT/CMP hook outputs and are written to the store before children render (`useOnFirstRender`). Port: see decisions/05-state-store-and-event-details.md §1 (fixed cascade, per-part subscription as the re-render boundary).
- **Refs as registries.** `listRef`/`valuesRef`/`labelsRef` are mutable arrays indexed by composite index, written by Items in layout effects (sparse arrays with holes mid-reindex are expected; `Object.hasOwn` guards the inline restore). `chipsRef` is a separate composite registry. Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry move detection) and decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed (RND-DOM-20..24, DOM handles become `Element`).
- **Ref-callback writes during commit.** The Input's `setInputElement` ref callback writes `inputElement`/`inputInsidePopup`/`inputOwnsFormValue` to the store and may write `''` to the input value during the commit; the Root then folds `inputOwnsFormValue` into the same layout-effect `update` so subscribers never see a half-updated snapshot. Port: see decisions/05-state-store-and-event-details.md §1 (one `Update` per transition; prop-to-store sync in `OnParametersSet`) and decisions/01-hosting-model-and-timing.md §Decision (C# post-commit state work is `OnAfterRender`).
- **One-time computations.** `initialDefaultInputValue` and the store (including the inline `initialSelectedIndex`) are computed once with `useRefWithInit`; later prop changes do not recompute them. Port: see decisions/05-state-store-and-event-details.md §1.
- **Controlled state.** `useControlled` fixes ownership at mount; for `open` the component additionally detects controlled *reopen during close* through `useValueChanged(open)` because no `setOpen` call happens in that path. Port: see decisions/05-state-store-and-event-details.md §2 (`Value`/`ValueChanged`/`DefaultValue`, `OnValueChange` first).
- **Focus manager special case.** The input-outside-popup layout relies on the FLT focus manager's "untrapped typeable combobox" variant (modal marking without a trap and without `aria-hidden` on the anchor). A port must reproduce that variant, not a generic modal dialog trap. Port: see decisions/04-js-interop-boundary.md §1 (`focus-manager` module) and §2 (JavaScript writes outside marking).
- **IME.** `event.which === 229` (deprecated API) is used to detect in-progress composition on keydown; ports should use `event.isComposing` plus the same Android exception for composition start.
- **Internal dismiss buttons** are rendered by the Input (start) and Popup (end) parts, not by the Root; their existence depends on `open` and the modal flag at render time.
- **Autocomplete reuse.** Everything above is implemented in `AriaCombobox` parameterized by `selectionMode`; a port should implement the engine once and expose Combobox (`single`/`multiple`) and Autocomplete (`none`) as thin configurations, keeping the engine-only options (`autoComplete`, `fillInputOnItemPress`, `submitOnItemClick`, `keepHighlight`, `autoHighlight: 'always'`) internal for Combobox.

## 15. Requirement index

Counts: STR 10, API 69, STA 34, DOM 24, ARIA 17, KEY 17, PTR 13, FOC 11, DIS 8, EVT 12, FRM 8, ANI 6, POS 4, EDGE 12 — 245 requirements. A11Y and I18N carry no separate IDs: accessibility requirements are in ARIA and the live-region/labelling bullets; locale/direction behavior is in CMB-API-15, CMB-KEY-06, CMB-KEY-08/09, CMB-KEY-13 and the UTL references.

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| CMB-STR-01 | MUST | Combobox.Root renders no element of its own | 2 |
| CMB-STR-02 | MUST | Every part other than Root reads the Root store and MUST throw when rendered outside <Combob… | 2 |
| CMB-STR-03 | MUST | The component has two anchor layouts decided by where Combobox.Input is rendered | 2 |
| CMB-STR-04 | MUST | Combobox.Positioner MUST be a descendant of Combobox.Portal (otherwise it throws "Base UI: <… | 2 |
| CMB-STR-05 | MUST | Inline composition: when inline is set the List MAY be rendered without Portal/Positioner/Popup | 2 |
| CMB-STR-06 | MUST | Closed-template rendering | 2 |
| CMB-STR-07 | MUST | Internal dismiss buttons | 2 |
| CMB-STR-08 | MUST | Structural-only parts (render an element with attributes but no behavior): Icon (span, aria-… | 2 |
| CMB-STR-09 | MUST | Chips nesting | 2 |
| CMB-STR-10 | MUST | Combobox.Label labels the Trigger only | 2 |
| CMB-API-01 | MUST | value makes the selected value controlled | 3.1 |
| CMB-API-02 | MUST | defaultValue seeds the uncontrolled selected value and is read once | 3.1 |
| CMB-API-03 | MUST | onValueChange(value, eventDetails) is called synchronously before the stored value changes w… | 3.1 |
| CMB-API-04 | MUST | multiple (default false) selects the selection mode for the lifetime of the component: true … | 3.1 |
| CMB-API-05 | MUST | open controls the popup; uncontrolled otherwise ({ name: 'Combobox', state: 'open' }) | 3.1 |
| CMB-API-06 | MUST | defaultOpen (default false) seeds the uncontrolled open state and is ignored when open is pa… | 3.1 |
| CMB-API-07 | MUST | onOpenChange(open, eventDetails) is called before every open-state change with OpenChangeEve… | 3.1 |
| CMB-API-08 | MUST | onOpenChangeComplete(open) is called once the open transition of the Popup has finished (tru… | 3.1 |
| CMB-API-09 | MUST | inputValue controls the Input text ({ name | 3.1 |
| CMB-API-10 | MUST | defaultInputValue seeds the uncontrolled Input text; when omitted the initial text is derive… | 3.1 |
| CMB-API-11 | MUST | onInputValueChange(inputValue, eventDetails) is called before every Input text change, inclu… | 3.1 |
| CMB-API-12 | MUST | items supplies the source data | 3.1 |
| CMB-API-13 | MUST | filteredItems replaces the internal filter result | 3.1 |
| CMB-API-14 | MUST | filter(item, query, itemToString?) decides whether a source item matches the current query; … | 3.1 |
| CMB-API-15 | MUST | locale is passed to the collator of the default filter; it has no effect on a custom filter. | 3.1 |
| CMB-API-16 | MUST | limit (default -1, meaning unlimited) caps the number of rendered items derived from items (… | 3.1 |
| CMB-API-17 | MUST | autoHighlight (boolean, default false) | 3.1 |
| CMB-API-18 | MUST | highlightItemOnHover (default true) is passed as the list-navigation focusItemOnHover option… | 3.1 |
| CMB-API-19 | MUST | loopFocus (default true) is passed as the list-navigation loopFocus; allowEscape is loopFocu… | 3.1 |
| CMB-API-20 | MUST | openOnInputClick (default true) enables the input-press opener (CMB-PTR-01) and the press ha… | 3.1 |
| CMB-API-21 | MUST | grid (default false) switches the List to role="grid", Items to role="gridcell", Groups to r… | 3.1 |
| CMB-API-22 | MUST | virtualized (default false) declares that the consumer windows the items: the List does not … | 3.1 |
| CMB-API-23 | MUST | inline (default false) | 3.1 |
| CMB-API-24 | MUST | modal (default false) | 3.1 |
| CMB-API-25 | MUST | name identifies the field | 3.1 |
| CMB-API-26 | MUST | form is forwarded as the form attribute of the hidden input(s) so the combobox can submit to… | 3.1 |
| CMB-API-27 | MUST | id (generated when omitted) is the Root id | 3.1 |
| CMB-API-28 | MUST | required (default false) sets aria-required on the control (Input, or Trigger when the input… | 3.1 |
| CMB-API-29 | MUST | readOnly (default false) | 3.1 |
| CMB-API-30 | MUST | disabled (default false, OR-ed with the Field's disabled state) disables every interactive p… | 3.1 |
| CMB-API-31 | MUST | autoComplete is forwarded verbatim as the hidden input's autocomplete attribute (browser aut… | 3.1 |
| CMB-API-32 | MUST | itemToStringLabel(itemValue) converts a value to its display string (input text after select… | 3.1 |
| CMB-API-33 | MUST | itemToStringValue(itemValue) converts a value to its form string | 3.1 |
| CMB-API-34 | MUST | isItemEqualToValue(itemValue, value) (default Object.is) is used for every value comparison:… | 3.1 |
| CMB-API-35 | MUST | inputRef is attached to the hidden form input, not to Combobox.Input. | 3.1 |
| CMB-API-36 | MUST | actionsRef.current is { unmount, close } | 3.1 |
| CMB-API-37 | MUST | onItemHighlighted(value \| undefined, { reason | 3.1 |
| CMB-STA-01 | MUST | The Label's state object is the surrounding Field root state verbatim (FFI FieldRoot.State);… | 3.2 |
| CMB-STA-02 | MUST | The InputGroup state is { ...fieldState, open, disabled, readOnly, popupSide, listEmpty, pla… | 3.3 |
| CMB-STA-03 | MUST | The Chip state is { disabled } where disabled is the Root's disabled flag. | 3.5 |
| CMB-API-53 | MUST | nativeButton (default true) is passed to the shared button behavior (UBT-API-03/04): a nativ… | 3.6 |
| CMB-API-54 | MUST | disabled (default false) is OR-ed with the Root's disabled; the button behavior additionally… | 3.6 |
| CMB-STA-04 | MUST | The ChipRemove state is { disabled }. | 3.6 |
| CMB-API-38 | MUST | Combobox.Input's disabled (default false) is OR-ed with the Field's and the Root's disabled … | 3.7 |
| CMB-API-39 | MUST | The Input's id is the consumer's id, else the Root id when the input is outside the popup, e… | 3.7 |
| CMB-STA-05 | MUST | The Input state is { ...fieldState, open, disabled, readOnly, popupSide, listEmpty } | 3.7 |
| CMB-API-40 | MUST | nativeButton (default true) configures the shared button behavior (UBT-API-03/04). | 3.8 |
| CMB-API-41 | MUST | disabled (default false) is OR-ed with the Field's and Root's disabled flags; a disabled Cle… | 3.8 |
| CMB-API-42 | MUST | keepMounted (default false) | 3.8 |
| CMB-STA-06 | MUST | The Clear state is { open, disabled, visible, transitionStatus } where visible is selectedVa… | 3.8 |
| CMB-API-43 | MUST | nativeButton (default true) configures the shared button behavior; a non-native disabled tri… | 3.9 |
| CMB-API-44 | MUST | disabled (default false) is OR-ed with the Field's and Root's disabled flags; a disabled Tri… | 3.9 |
| CMB-API-45 | MUST | When the input is inside the popup the Trigger's id defaults to the Root id and is registere… | 3.9 |
| CMB-STA-07 | MUST | The Trigger state is { ...fieldState, readOnly, open, disabled, popupSide, listEmpty, placeh… | 3.9 |
| CMB-API-46 | MUST | Combobox.Value renders, in priority order | 3.10 |
| CMB-API-47 | MUST | placeholder is rendered only when no value is selected, children is nullish, and the items l… | 3.10 |
| CMB-API-48 | MUST | keepMounted (default false) is published to the Positioner (which then keeps its element mou… | 3.12 |
| CMB-API-49 | MUST | container is forwarded to the floating portal (FLT); the default is the document body. | 3.12 |
| CMB-STA-08 | MUST | The Backdrop state is { open, transitionStatus } read from the store. | 3.13 |
| CMB-API-50 | MUST | The Positioner passes its props to the shared anchor-positioning mechanism (FLT) with these … | 3.14 |
| CMB-STA-09 | MUST | The Positioner state is { open, side, align, anchorHidden, empty }; the rendered side is als… | 3.14 |
| CMB-API-51 | MUST | initialFocus is forwarded to the focus manager (FLT-API-05) | 3.15 |
| CMB-API-52 | MUST | finalFocus is forwarded as the focus manager's returnFocus (FLT-API-05) | 3.15 |
| CMB-STA-10 | MUST | The Popup state is { open, side, align, anchorHidden, transitionStatus, empty }. | 3.15 |
| CMB-STA-11 | MUST | The Arrow state is { open, side, align, uncentered } from the Positioner context (FLT-DOM-08). | 3.16 |
| CMB-API-55 | MUST | When children is a function the List renders an implicit Combobox.Collection with it (CMB-ST… | 3.19 |
| CMB-STA-12 | MUST | The List state is { empty } where empty is filteredItems.length === 0. | 3.19 |
| CMB-API-63 | MUST | Combobox.Collection calls children(item, index) for each item of the nearest Group's items w… | 3.20 |
| CMB-API-62 | MUST | items, when provided, is published to nested Combobox.Collections through the Group context;… | 3.21 |
| CMB-STA-16 | MUST | A Group keeps the id of its mounted GroupLabel (set when the label mounts or its id changes) | 3.21 |
| CMB-API-56 | MUST | value (default null) identifies the item | 3.24 |
| CMB-API-57 | MUST | index, when provided, is the item's list index (skipping DOM-order resolution, CMP-STA-01); … | 3.24 |
| CMB-API-58 | MUST | disabled (default false) is OR-ed with the Root's disabled; a disabled item is still highlig… | 3.24 |
| CMB-API-59 | MUST | nativeButton defaults to false (the item is a div with role="option"/"gridcell"; the button … | 3.24 |
| CMB-API-60 | MUST | onClick runs before the built-in selection for both pointer activation and <kbd>Enter</kbd> … | 3.24 |
| CMB-STA-13 | MUST | The Item state is { disabled, selected, highlighted } where selected is isItemEqualToValue-m… | 3.24 |
| CMB-API-61 | MUST | keepMounted (default false) | 3.25 |
| CMB-STA-14 | MUST | The ItemIndicator state is { selected, transitionStatus }. | 3.25 |
| CMB-API-64 | MUST | Combobox.Separator is the shared listbox separator | 3.26 |
| CMB-STA-15 | MUST | The Separator state is { orientation }. | 3.26 |
| CMB-STA-17 | MUST | The following parts expose an empty state object ({}) to className/style/render and emit no … | 3.26 |
| CMB-API-65 | MUST | Combobox.useFilter({ multiple = false, value, ...collatorOptions }) returns an object with ex… | 3.27 |
| CMB-API-66 | MUST | Combobox.useFilteredItems() returns the Root's current filtered items (the same array the im… | 3.27 |
| CMB-API-67 | MUST | Combobox.createItems(data, { getValue, getLabel }) wraps flat or grouped source data (or und… | 3.27 |
| CMB-API-68 | MUST | createItems option types | 3.27 |
| CMB-API-69 | MUST | Every part exports Props and State both namespaced (Combobox.<Part>.Props) and as flat alias… | 3.27 |
| CMB-STA-18 | MUST | selectedValue, inputValue and open each use the shared controlled/uncontrolled utility (UTL-… | 4.1 |
| CMB-STA-19 | MUST | Initial input text | 4.1 |
| CMB-STA-20 | MUST | Input/selection synchronization (single mode, input outside the popup, hasInputValue false):… | 4.1 |
| CMB-STA-21 | MUST | The query is the trimmed input text, except while the popup is closed (or closing) with a fr… | 4.2 |
| CMB-STA-22 | MUST | Bypass | 4.2 |
| CMB-STA-23 | MUST | Filtered items derivation, in order | 4.2 |
| CMB-STA-24 | MUST | activeIndex (highlighted item, null = none) and selectedIndex (index of the selected item in… | 4.3 |
| CMB-STA-25 | MUST | Selected-index sync runs post-commit pre-paint whenever open, the close query, the selection… | 4.3 |
| CMB-STA-26 | MUST | Typed-query highlight (pending request) | 4.3 |
| CMB-STA-27 | MUST | Post-commit pre-paint while open or inline, after the pending-request handling: an activeInd… | 4.3 |
| CMB-STA-28 | MUST | Inline initial highlight | 4.3 |
| CMB-STA-29 | MUST | mounted and transitionStatus come from the shared unmount-after-close mechanism (TRN-ANI-18.… | 4.4 |
| CMB-STA-30 | MUST | Unmount cleanup (runs once when the closing phase completes, in this order): onOpenChangeCom… | 4.4 |
| CMB-STA-31 | MUST | openMethod records the interaction type that opened the popup (PSH-PTR-03: set from the Trig… | 4.4 |
| CMB-STA-32 | MUST | forceMounted (store) and forceMount() | 4.4 |
| CMB-STA-33 | MUST | Three parallel registries index the rendered list | 4.5 |
| CMB-STA-34 | MUST | Store publication | 4.5 |
| CMB-DOM-01 | MUST | Combobox.Input renders an <input> (no type attribute is forced; a substituted <textarea> sta… | 5.1 |
| CMB-DOM-02 | MUST | Input data attributes | 5.1 |
| CMB-DOM-18 | MUST | The Root renders, after its children, one <input> with | 5.2 |
| CMB-DOM-19 | MUST | In multiple mode with a name, one <input type="hidden" name form value disabled> per selecte… | 5.2 |
| CMB-DOM-20 | MUST | The main hidden input has id="${rootId}-hidden-input" when, and only when, it has no name (F… | 5.2 |
| CMB-DOM-03 | MUST | InputGroup data attributes | 5.3 |
| CMB-DOM-04 | MUST | Trigger data attributes | 5.3 |
| CMB-DOM-05 | MUST | The Trigger renders a <button> per the shared button behavior (UBT-DOM-01/02/06; type="butto… | 5.3 |
| CMB-DOM-06 | MUST | Clear data attributes | 5.3 |
| CMB-DOM-07 | MUST | The Clear element is rendered iff keepMounted \|\| mounted where mounted follows visible throu… | 5.3 |
| CMB-DOM-11 | MUST | Chip renders a <div tabindex="-1"> (focusable programmatically, not a tab stop) with data-di… | 5.3 |
| CMB-DOM-08 | MUST | The List renders a <div> with tabindex="-1", id = the floating context id (the value the Inp… | 5.4 |
| CMB-DOM-09 | MUST | An Item renders a <div> with id="${rootId}-${index}" when the Root has an id and the item is… | 5.4 |
| CMB-DOM-10 | MUST | An ItemIndicator renders a <span aria-hidden="true"> with default content "✔️", data-sel… | 5.4 |
| CMB-DOM-16 | MUST | The Separator renders <div role="presentation" data-orientation="<orientation>">. | 5.4 |
| CMB-DOM-23 | MUST | Combobox.Empty renders its element always (so the live region persists) but passes children … | 5.4 |
| CMB-DOM-12 | MUST | The Popup renders a <div> with the popup id (CMB-DOM-24), the focusable-popup props (PSH FOC… | 5.5 |
| CMB-DOM-24 | MUST | Popup id resolution | 5.5 |
| CMB-DOM-13 | MUST | The Positioner renders the shared positioner <div role="presentation"> (FLT-DOM-01) with hid… | 5.5 |
| CMB-DOM-14 | MUST | The Backdrop renders <div role="presentation"> with hidden while not mounted, inline user-se… | 5.5 |
| CMB-DOM-15 | MUST | The Arrow renders <div aria-hidden="true"> with the arrow position styles (FLT-DOM-09) merge… | 5.5 |
| CMB-DOM-21 | MUST | The Portal renders its floating portal (FLT-DOM-11) iff mounted \|\| keepMounted \|\| forceMount… | 5.5 |
| CMB-DOM-22 | MUST | Each internal dismiss button is a <span> with the non-native button props (role="button", UB… | 5.5 |
| CMB-DOM-17 | MUST | The Label renders a <div> with its id (default ${rootId}-label), the non-native label props … | 5.5 |
| CMB-ARIA-01 | MUST | When the input is outside the popup the Input carries the combobox pattern: role="combobox",… | 5.6 |
| CMB-ARIA-02 | MUST | expanded is open \|\| inline | 5.6 |
| CMB-ARIA-03 | MUST | While expanded and an item is highlighted, the control carries aria-activedescendant="${root… | 5.6 |
| CMB-ARIA-04 | MUST | aria-autocomplete is "none" when readOnly and otherwise the engine's autoComplete mode, whic… | 5.6 |
| CMB-ARIA-05 | MUST | When the control element is not a native text input (e.g | 5.6 |
| CMB-ARIA-06 | MUST | An Input inside the popup receives no Field validation attributes (aria-invalid, aria-descri… | 5.6 |
| CMB-ARIA-07 | MUST | Trigger ARIA | 5.6 |
| CMB-ARIA-08 | MUST | The List has role="listbox" ("grid" when grid), aria-multiselectable="true" in multiple mode… | 5.6 |
| CMB-ARIA-09 | MUST | An Item has role="option" ("gridcell" inside a Row) and aria-selected="true\|false"; disabled… | 5.6 |
| CMB-ARIA-10 | MUST | A Group has role="group" ("rowgroup" when grid) and aria-labelledby = its GroupLabel's id (C… | 5.6 |
| CMB-ARIA-11 | MUST | The Popup has role="dialog" when the input is inside it and role="presentation" otherwise; t… | 5.6 |
| CMB-ARIA-12 | MUST | Chips has role="toolbar" only while at least one value is selected (so NVDA stays in focus m… | 5.6 |
| CMB-ARIA-13 | MUST | Status and Empty are live regions | 5.6 |
| CMB-ARIA-14 | MUST | The hidden form input is aria-hidden with tabindex="-1"; the internal dismiss buttons are la… | 5.6 |
| CMB-ARIA-15 | MUST | With the input outside the popup the focus manager is modal (CMB-FOC-03) but runs in the unt… | 5.6 |
| CMB-ARIA-16 | MUST | Labelling | 5.6 |
| CMB-ARIA-17 | MUST | Disabled/readOnly exposure | 5.6 |
| CMB-KEY-01 | MUST | List-navigation configuration (CMP) | 6.1 |
| CMB-KEY-02 | MUST | <kbd>ArrowDown</kbd> / <kbd>ArrowUp</kbd> on the Input | 6.1 |
| CMB-KEY-03 | MUST | List ends | 6.1 |
| CMB-KEY-04 | MUST | <kbd>Enter</kbd> on the Input while open | 6.1 |
| CMB-KEY-05 | MUST | <kbd>Escape</kbd> while the popup is not mounted (closed, not inline-dialog) on the Input: s… | 6.1 |
| CMB-KEY-06 | MUST | <kbd>Home</kbd> / <kbd>End</kbd> on the Input are consumed (prevented and stopped — list n… | 6.1 |
| CMB-KEY-07 | MUST | <kbd>Backspace</kbd> on an empty Input in multiple mode with at least one selected value (an… | 6.1 |
| CMB-KEY-08 | MUST | <kbd>ArrowLeft</kbd> (LTR; <kbd>ArrowRight</kbd> in RTL) on the Input when the caret is at p… | 6.1 |
| CMB-KEY-09 | MUST | Keys on a focused Chip (ignored when disabled or readOnly) | 6.1 |
| CMB-KEY-10 | MUST | <kbd>Enter</kbd>/<kbd>Space</kbd> on a ChipRemove remove its chip's value with reason chip-r… | 6.1 |
| CMB-KEY-11 | MUST | <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> on the Trigger (prevented and stopped) open the popu… | 6.1 |
| CMB-KEY-12 | MUST | Closed-trigger typeahead | 6.1 |
| CMB-KEY-13 | MUST | Grid mode | 6.1 |
| CMB-KEY-14 | MUST | Modifier and IME handling on the Input | 6.1 |
| CMB-KEY-15 | MUST | readOnly | 6.1 |
| CMB-KEY-16 | MUST | <kbd>Tab</kbd>/<kbd>Shift+Tab</kbd> are not intercepted | 6.1 |
| CMB-KEY-17 | MUST | Typing on the Input (a typed, non-autofill-like input event or composition commit) with a no… | 6.1 |
| CMB-PTR-01 | MUST | Input press | 6.2 |
| CMB-PTR-02 | MUST | Trigger press | 6.2 |
| CMB-PTR-03 | MUST | Drag-select and cancel-open (Trigger mousedown while closed, input inside the popup only): a… | 6.2 |
| CMB-PTR-04 | MUST | Item pointer handling | 6.2 |
| CMB-PTR-05 | MUST | handleSelection(event, value) (reason item-press, with the recorded keyboard event substitut… | 6.2 |
| CMB-PTR-06 | MUST | Hover | 6.2 |
| CMB-PTR-07 | MUST | Clear press | 6.2 |
| CMB-PTR-08 | MUST | Chips-area and InputGroup press (mousedown) | 6.2 |
| CMB-PTR-09 | MUST | Chip press | 6.2 |
| CMB-PTR-10 | MUST | ChipRemove click | 6.2 |
| CMB-PTR-11 | MUST | Touch | 6.2 |
| CMB-PTR-12 | MUST | Scroll reset on query change | 6.2 |
| CMB-PTR-13 | MUST | Programmatic/scroll highlights | 6.2 |
| CMB-FOC-01 | MUST | Items are never focusable (tabindex absent; the list-navigation onFocus item handler is stri… | 6.3 |
| CMB-FOC-02 | MUST | Tab order | 6.3 |
| CMB-FOC-03 | MUST | The Popup wraps its element in the shared focus manager (FLT) with: disabled: !mounted, moda… | 6.3 |
| CMB-FOC-04 | MUST | When focus lands on the Popup element itself or anywhere inside the List (e.g | 6.3 |
| CMB-FOC-05 | MUST | Focusing the hidden form input (native validation bubbles, autofill, inputRef.current.focus(… | 6.3 |
| CMB-FOC-06 | MUST | Input-inside-popup selection flow | 6.3 |
| CMB-FOC-07 | MUST | Chip focus | 6.3 |
| CMB-FOC-08 | MUST | Field focus state | 6.3 |
| CMB-FOC-09 | MUST | Modal wrap (input outside the popup, or modal) | 6.3 |
| CMB-FOC-10 | MUST | Trigger focus | 6.3 |
| CMB-FOC-11 | MUST | Dialog composition (inline list inside a Dialog) | 6.3 |
| CMB-DIS-01 | MUST | Dismiss configuration (FLT useDismiss) | 6.4 |
| CMB-DIS-02 | MUST | <kbd>Escape</kbd> while open closes the popup with reason escape-key without committing the … | 6.4 |
| CMB-DIS-03 | MUST | Outside press closes the popup with reason outside-press without committing; presses on a pl… | 6.4 |
| CMB-DIS-04 | MUST | Focus leaving the Input/Popup tree (Tab, Shift+Tab, focusing a chip, clicking a focusable ou… | 6.4 |
| CMB-DIS-05 | MUST | Inline mode disables the dismiss interactions entirely | 6.4 |
| CMB-DIS-06 | MUST | A Trigger press while open closes the popup with reason trigger-press (toggle); an Input pre… | 6.4 |
| CMB-DIS-07 | MUST | Activating an internal dismiss button closes the popup with reason close-press and trigger =… | 6.4 |
| CMB-DIS-08 | MUST | A cancel-open close (CMB-PTR-03) goes through onOpenChange like any other close and is cance… | 6.4 |
| CMB-EVT-01 | MUST | onOpenChange receives OpenChangeEventDetails | 7 |
| CMB-EVT-02 | MUST | onOpenChange reason matrix (EVD §4.3.7) | 7 |
| CMB-EVT-03 | MUST | onValueChange reasons | 7 |
| CMB-EVT-04 | MUST | onInputValueChange reasons | 7 |
| CMB-EVT-05 | MUST | onItemHighlighted(value, { reason, event, index }) fires on every highlight change: keyboard… | 7 |
| CMB-EVT-06 | MUST | onOpenChangeComplete(true) is called once per open when the Popup's enter animations finish … | 7 |
| CMB-EVT-07 | MUST | Cancellation | 7 |
| CMB-EVT-08 | MUST | ChangeEventDetails.isItemPress is present only on input-clear writes that the component attr… | 7 |
| CMB-EVT-09 | MUST | allowPropagation() | 7 |
| CMB-EVT-10 | MUST | Event ordering for an item selection (single) | 7 |
| CMB-EVT-11 | MUST | Autofill events | 7 |
| CMB-EVT-12 | MUST | Item.onClick fires for pointer activation and for <kbd>Enter</kbd> on the highlighted item (… | 7 |
| CMB-FRM-01 | MUST | The Root registers as the Field control with | 8 |
| CMB-FRM-02 | MUST | Native form participation goes through the hidden input(s) (CMB-DOM-18/19): single mode subm… | 8 |
| CMB-FRM-03 | MUST | On every selected-value change (any reason, including autofill): external errors for the fie… | 8 |
| CMB-FRM-04 | MUST | filled is recomputed post-commit pre-paint as selectedValue != null (single) / non-empty arr… | 8 |
| CMB-FRM-05 | MUST | touched is set on Input blur, on Trigger blur not into the popup, and when the popup closes … | 8 |
| CMB-FRM-06 | MUST | Field.Root disabled and name flow into the Root (CMB-API-25/30); Field.Label labels the Inpu… | 8 |
| CMB-FRM-07 | MUST | Enter-key submission | 8 |
| CMB-FRM-08 | MUST | The hidden input's autocomplete attribute is the Root autoComplete prop; browser autofill re… | 8 |
| CMB-ANI-01 | MUST | The Popup is the animated element | 9 |
| CMB-ANI-02 | MUST | During the exit animation the list keeps rendering the frozen filtered result (close query, … | 9 |
| CMB-ANI-03 | MUST | preventUnmountOnClose() + actions.unmount() implement an externally controlled exit (TRN-ANI… | 9 |
| CMB-ANI-04 | MUST | Combobox.Clear animates its visibility | 9 |
| CMB-ANI-05 | MUST | Combobox.ItemIndicator transition status is driven by selected, but only a kept-mounted indi… | 9 |
| CMB-ANI-06 | MUST | Items/highlight during exit | 9 |
| CMB-POS-01 | MUST | With the input inside the popup the anchor defaults to the Trigger element; otherwise the Inp… | 10 |
| CMB-POS-02 | MUST | The CSS variables --available-height/--available-width are seeded on the positioner before t… | 10 |
| CMB-POS-03 | MUST | Modal positioning extras | 10 |
| CMB-POS-04 | MUST | The rendered side is published to the store post-commit pre-paint (popupSide) and exposed on… | 10 |
| CMB-EDGE-01 | MUST | Passing an items value that is an object but not a createItems() collection (no label functi… | 12 |
| CMB-EDGE-02 | MUST | In development, when a createItems() collection derives the same value from two items, a con… | 12 |
| CMB-EDGE-03 | MUST | The Combobox.Label development error of CMB-STR-10 (FFI-EDGE-03) is the only other dev-time … | 12 |
| CMB-EDGE-04 | MUST | items switching to undefined (data unloading) must not cause an unbounded update loop: th… | 12 |
| CMB-EDGE-05 | MUST | Nullish entries in items/filteredItems are unsupported | 12 |
| CMB-EDGE-06 | MUST | Server rendering | 12 |
| CMB-EDGE-07 | MUST | attach and dispose of the part's JavaScript twin are idempotent; a repeated attach of the sa… | 12 |
| CMB-EDGE-08 | MUST | Duplicate values in a plain items array resolve labels to the first match; a { value, label … | 12 |
| CMB-EDGE-09 | MUST | Rendering an Item without a Root throws (CMB-STR-02); rendering a Combobox inside a composit… | 12 |
| CMB-EDGE-10 | MUST | Items skip unchanged renders | 12 |
| CMB-EDGE-11 | MUST | Controlled null in multiple mode is read as [] everywhere (selection, chips, hidden inputs, … | 12 |
| CMB-EDGE-12 | MUST | The createItems() label of a selected value is resolved from current props only: collection … | 12 |
| CMB-EDGE-13 | MUST | Element-substitution callbacks are opaque (RND-DOM-32): a render function on a part whose id… | 12 |
| CMB-EDGE-14 | MUST | Label / GroupLabel register the computed id; a dropped or overriding substitute id leaves `aria-labelledby` dangling | 12 |

## 16. Open questions

1. **Outline vs. code — data attributes.** The docs outline (`types.md`) omits attributes the code emits and tests assert: `data-selected` on `ItemIndicator` (default boolean projection of `selected`), `data-disabled` on `Chip` and `ChipRemove`, `data-empty` on `List` (state `empty`). Conversely `data-required` is declared in the `Input`/`Trigger`/`InputGroup` attribute enums but no `required` field exists in those parts' state objects (the Field state has no `required`), so it is unclear whether the attribute is ever emitted; CMB-DOM-02/04 record it as "when required (field state)" pending verification. Decide the canonical set before porting. Resolved (QA 2026-10-14) for `data-required`: never emitted — `ComboboxInputState`/`ComboboxTriggerState` spread `FieldRootState` (no `required`), add no `required` key, and `triggerStateAttributesMapping` has no `required` entry; only the enums (`ComboboxInputDataAttributes.ts:27`, `ComboboxTriggerDataAttributes.ts:27`) and the outline (`types.md`) list it. CMB-DOM-02/04 and CMB-API-28 now state it is not rendered; a port must not emit it (or the outline should drop it). The rest of this question (attributes emitted but missing from the outline) stays open.
2. **`aria-disabled` on the Input.** `ComboboxInput.test.tsx` titles say "renders aria-disabled" but the Input prop bag only sets the native `disabled` attribute (through the Field validation props). Verify what the assertion checks and whether a port must emit `aria-disabled` on a text input (CMB-ARIA-17 is written conservatively). Resolved (review 2026-10-18): the test (`combobox/input/ComboboxInput.test.tsx:20-29`) asserts only the native `disabled` attribute despite its title; `aria-disabled` is neither emitted nor required on the Input. CMB-ARIA-17 stands.
3. **PSH-ARIA-04 vs. Combobox.Trigger.** PSH states Combobox triggers render `aria-haspopup="listbox"`; the Trigger renders `"dialog"` when the input is inside the popup and `"listbox"` otherwise (CMB-ARIA-07). PSH should carry the exception, and the popup conformance suite's "trigger" for Combobox is the Input (`combobox: true`), not `Combobox.Trigger`. Resolved (QA 2026-10-14): verified `combobox/trigger/ComboboxTrigger.tsx:160` (`inputInsidePopup ? 'dialog' : 'listbox'`, never `"grid"`); PSH-ARIA-04 now carries this exception; CMB-ARIA-07 is the Combobox statement.
4. **Hinted props that do not exist on `Combobox.Root` in 1.9.0.** `keepHighlight` (removed in 1.0.0-rc.0; the engine default `false` is always used), `cols` (renamed `grid` in beta), `selectionMode`, `submitOnItemClick`, `fillInputOnItemPress`, `autoComplete` (ARIA mode) and `autoHighlight: 'always'` are engine/Autocomplete-only. The Root's public `autoHighlight` is typed `boolean`. The document models the engine so Autocomplete can cite it, but a port of *Combobox* must not expose them.
5. **`inputRef` semantics.** `Combobox.Root.inputRef` is a handle to the *hidden* form input, not to `Combobox.Input` (CMB-API-35, no test). Confirm this is the intended public contract (the outline says "A ref to the hidden input element") and whether the port should name it differently. Decided (2026-10-18): the port has no `inputRef`; `Combobox.Input.Element` is the visible input and the hidden input is internal (ADR 02 Amendments).
6. **`Item.onClick` for disabled/readOnly items.** The consumer handler is merged before the built-in one, and the built-in returns early when disabled/readOnly, so the consumer `onClick` may still fire for a disabled item in native-button mode but not in the default non-native composite mode (UBT suppresses the click). No test pins this down; CMB-EVT-12 leaves it open. Resolved (review 2026-10-18): the Item passes `disabled` to the shared button behavior with `focusableWhenDisabled: true` (`combobox/item/ComboboxItem.tsx:137-142`), which suppresses the consumer `onClick` on a disabled element in both native and non-native mode (UBT-PTR-01); only readOnly lets the consumer `onClick` run without a selection. CMB-EVT-12 now states this.
7. **Chip outside Chips.** `Combobox.Chip` dereferences the Chips context with a non-null assertion (a `TypeError`, not a Base UI error) when rendered outside `Combobox.Chips`. The port should either throw a descriptive error or document the nesting requirement (CMB-STR-09).
8. **`Combobox.Clear` with `data-popup-open`.** The Clear button uses the plain trigger mapping, so it carries `data-popup-open` although it is not an opener; the outline lists it. Confirm it is intentional for styling parity with the Trigger.
9. **`limit` and `filteredItems`.** `limit` never truncates external `filteredItems` (CMB-API-13/16); the outline wording ("maximum number of items to display") suggests otherwise. Clarify in the docs or make the port follow the code.
10. **`popupListConformanceTests` `listEnd: 'escape'`.** The suite verifies the escape-to-input behavior only for the default `loopFocus`; the interaction of `allowEscape` with `autoHighlight` (wrapping instead of escaping, CMB-KEY-03) is covered only by the `loopFocus` tests. Consider adding a test for `autoHighlight` + list ends.
11. **Reason union.** `clear-press`/`chip-remove-press` are declared in the shared `ChangeEventReason` union for `onOpenChange` but never emitted for open changes (EVD §4.3.7 "declared only"). A typed port may narrow the per-callback unions. Resolved: decisions/05-state-store-and-event-details.md §3 uses one `BaseUIEventReason` enum; per-callback subsets remain documentation (EVD §4.3.7).
12. **Autocomplete-specific tests in this folder.** Twelve tests (or `it.each` variants) in the combobox folder render `Autocomplete.Root` (`selectionMode: 'none'`), plus one shared-utility variant: `ComboboxClear.test.tsx` "renders and clears the typed value in no-selection mode"; `ComboboxList.test.tsx` "sets aria-readonly on the Autocomplete listbox…"; `ComboboxRoot.test.tsx` "restores the first highlight after an always-highlighted query becomes empty", "does not request form submission when no form owns the autocomplete", "does not request form submission when requestSubmit is unavailable", "validates the autocomplete input when focus leaves its popup"; all five `store.test.tsx` tests ("publishes an inline transition in a single store transaction", "settles inputOwnsFormValue through the useStore path when inline is set", "exposes real commands to a descendant ref callback on the first commit", "gives form value ownership to the hidden input when the input is inside the popup", "keeps form value ownership across hydration"); the `no selection` variant of `ComboboxTrigger.test.tsx` "validates the $name value when blurred"; and, outside this folder, the `Autocomplete.Separator` variant of `utils/listbox-separator/ListboxSeparator.test.tsx` "exposes the listbox separator behavior". Where they exercise shared engine behavior they are mapped to the engine requirements here; the ownership assertions (`inputOwnsFormValue` becoming `true`, the visible input owning the name) do not hold in Combobox mode and are not cited as Combobox evidence (CMB-STA-34, CMB-EDGE-06). All should be cross-cited from [autocomplete](autocomplete.md). Recounted (QA 2026-10-14).
13. **Template fit.** The template's §3 "one subsection per part" produced 27 subsections; the per-part "Data attributes" tables reference §5 IDs as instructed, but for parts whose attributes are the *same mapping* (Input/InputGroup/Trigger) the §5 bullets had to be written per part to keep the Req column resolvable. A shared "attribute mapping" subsection pattern in the template (one ID for a mapping, reused by several parts) would remove the repetition. Likewise, the template has no place for *hook/function* exports (`useFilter`, `useFilteredItems`, `createItems`); they were put in §3.27 "Shared types and functions".
14. **Trigger "textarea" test.** `combobox/trigger/ComboboxTrigger.test.tsx:440-454` "does not open on ArrowDown/ArrowUp when reference is a textarea" renders a Trigger and a bare `<textarea>` with no Portal/Popup/List, then asserts no `listbox` exists; the Trigger's keydown (`combobox/trigger/ComboboxTrigger.tsx:247-256`) has no textarea check, so the test passes vacuously. CMB-KEY-11 records the code behavior; confirm the test's intent (perhaps a planned exemption) before relying on it.
