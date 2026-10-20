# Autocomplete

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/autocomplete/` (own code: `root/AutocompleteRoot.tsx`, `value/AutocompleteValue.tsx`; type-narrowing aliases: `trigger/`, `input-group/`, `item/`, `separator/`; every other part re-exported from `combobox/`) |
| Tests | `packages/react/src/autocomplete/**/*.test.tsx` (3 files: `root/AutocompleteRoot.test.tsx`, `item/AutocompleteItem.test.tsx`, `value/AutocompleteValue.test.tsx`) + `root/AutocompleteRoot.spec.tsx` (type-level); Autocomplete fixtures in `combobox/root/ComboboxRoot.test.tsx`, `combobox/store.test.tsx`, `combobox/clear/ComboboxClear.test.tsx`, `combobox/list/ComboboxList.test.tsx`, `combobox/trigger/ComboboxTrigger.test.tsx`, `utils/listbox-separator/ListboxSeparator.test.tsx` |
| Docs page | `docs/src/app/(docs)/react/components/autocomplete/page.mdx` |
| Prefix | `AUT` |
| Status | approved |
| Builds on | [combobox](combobox.md) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) (all inherited through the Combobox engine) |

## 1. Overview

Autocomplete is a free-text input with a list of suggestions. The user types into `Autocomplete.Input`; the popup opens and (by default) shows the items that contain the typed text; the arrow keys move a *virtual* highlight through the list (DOM focus stays on the input, `aria-activedescendant` names the highlighted item); pressing an item or <kbd>Enter</kbd> on the highlighted item writes that item's label into the input and closes the popup. Unlike [Combobox](combobox.md), Autocomplete **remembers no selection**: its value (`value`/`defaultValue`/`onValueChange`) *is the input string*, items never render as selected, any text — matching an item or not — is a valid value, and the form submits the text itself.

Autocomplete is the Combobox engine (`combobox/root/AriaCombobox.tsx`) run with `selectionMode: 'none'` and a thin wrapper root that adds four behaviors: the `mode` prop (`list`, `both`, `inline`, `none` — the WAI-ARIA `aria-autocomplete` values) choosing whether the list is filtered and whether the input *temporarily* shows the highlighted item's label while navigating ("inline completion"); `autoHighlight: 'always'`; `keepHighlight`; and `submitOnItemClick`. Defaults differ from Combobox: the input does not open the popup on click (`openOnInputClick` defaults to `false`), and closing the popup never rewrites the input text. The usage guidelines recommend Combobox instead whenever the selection must be remembered or the text must be one of the options, and suggest Autocomplete for filterable command pickers (input inside the popup or an inline list in a Dialog) (`components/autocomplete/page.mdx` §Usage guidelines).

Only `Root` and `Value` have their own implementations. `Trigger`, `InputGroup`, `Item` and `Separator` are the Combobox (or shared listbox) components under narrower public types; every other part is the Combobox part itself. This document therefore adopts the Combobox requirements wholesale (AUT-STR-02) and specifies individually only what Autocomplete adds or overrides.

Base component: [combobox](combobox.md). Derived components: none.

## 2. Anatomy

```
<Autocomplete.Root>                     required, 1                 no element (renders children + one hidden <input>)   own wrapper around the Combobox engine
  <Autocomplete.InputGroup>             optional, 0..1              div  role="group"                                     = Combobox.InputGroup (narrowed type)
    <Autocomplete.Input>                optional, 0..1              input (outside the popup layout)                      = Combobox.Input
    <Autocomplete.Trigger>              optional, 0..n              button                                                = Combobox.Trigger (narrowed type)
    <Autocomplete.Icon>                 optional, 0..n              span aria-hidden                                      = Combobox.Icon
    <Autocomplete.Clear>                optional, 0..1              button                                                = Combobox.Clear
    <Autocomplete.Value>                optional, 0..n              no element (the input text)                           own
  <Autocomplete.Portal>                 required for Positioner     portal container                                      = Combobox.Portal
    <Autocomplete.Backdrop>             optional, 0..1              div role="presentation"                               = Combobox.Backdrop
    <Autocomplete.Positioner>           required for Popup/Arrow    div role="presentation"                               = Combobox.Positioner
      <Autocomplete.Popup>              optional, 0..1              div role="presentation" | "dialog" (input inside)     = Combobox.Popup
        <Autocomplete.Arrow>            optional, 0..1              div aria-hidden                                       = Combobox.Arrow
        <Autocomplete.Input>            (input-inside-popup layout)
        <Autocomplete.Status>           optional, 0..n              div role="status"                                     = Combobox.Status
        <Autocomplete.Empty>            optional, 0..1              div role="status"                                     = Combobox.Empty
        <Autocomplete.List>             optional, 0..1              div role="listbox" | "grid"                           = Combobox.List
          <Autocomplete.Collection>     optional, 0..n              no element                                            = Combobox.Collection
          <Autocomplete.Group>          optional, 0..n              div role="group" | "rowgroup"                         = Combobox.Group
            <Autocomplete.GroupLabel>   optional, 0..1              div aria-hidden                                       = Combobox.GroupLabel
          <Autocomplete.Row>            optional, 0..n              div role="row"                                        = Combobox.Row
            <Autocomplete.Item>         optional, 0..n              div role="option" | "gridcell"                        = Combobox.Item (narrowed type)
          <Autocomplete.Separator>      optional, 0..n              div role="presentation"                               = listbox separator (narrowed type)
```

- **AUT-STR-01** — MUST. The `Autocomplete` namespace exposes exactly these members. Own implementations: `Root`, `Value`. The Combobox (or shared) component itself under an Autocomplete-specific public type: `Trigger` (= `Combobox.Trigger`), `InputGroup` (= `Combobox.InputGroup`), `Item` (= `Combobox.Item`), `Separator` (= the shared listbox separator, as `Combobox.Separator`). Re-exported unchanged from Combobox: `Input`, `Icon`, `Clear`, `List`, `Status`, `Portal`, `Backdrop`, `Positioner`, `Popup`, `Arrow`, `Group`, `GroupLabel`, `Row`, `Collection`, `Empty`. Functions: `useFilter` (the plain collator filter, **not** `Combobox.useFilter`; AUT-API-19) and `useFilteredItems` (= `Combobox.useFilteredItems`). The Combobox members `Label`, `Chips`, `Chip`, `ChipRemove`, `ItemIndicator` and `createItems` are **not** part of the Autocomplete namespace.
  Source: `autocomplete/index.parts.ts:1-25`; `autocomplete/trigger/AutocompleteTrigger.tsx:14`; `autocomplete/input-group/AutocompleteInputGroup.tsx:14`; `autocomplete/item/AutocompleteItem.tsx:12`; `autocomplete/separator/AutocompleteSeparator.tsx:30-32`. Docs: `components/autocomplete/page.mdx` §Anatomy. Test: [L2] `AutocompleteRoot.test.tsx` (all tests use these parts); `AutocompleteRoot.spec.tsx` "Autocomplete has no item type parameter, so collections are not accepted" (type-level).
- **AUT-STR-02** — MUST. Every re-exported or aliased part, and the engine `Autocomplete.Root` configures, satisfies every CMB requirement whose subject is that part or that engine behavior, with the substitutions below, **except** the CMB IDs listed under "Not adopted" (they concern parts or features Autocomplete does not have) and under "Overridden" (replaced by the AUT requirement named). The §13 matrices of this document may cite adopted CMB IDs directly.
  Source: `autocomplete/root/AutocompleteRoot.tsx:107-125`; `autocomplete/index.parts.ts:1-25`. Test: see §13.
- **AUT-STR-03** — MUST. `Autocomplete.Root` renders no element. It runs the Combobox engine with this fixed configuration: `selectionMode: 'none'`; `fillInputOnItemPress: true`; ARIA autocomplete mode (`autoComplete`) = `mode`; engine `itemToStringLabel` = the Autocomplete `itemToStringValue` prop; engine input text = the resolved input text of AUT-STA-04 (always a defined string, so the engine treats the input text as externally owned); engine `defaultInputValue` = `defaultValue`; engine `onInputValueChange` = the Autocomplete value handler (AUT-EVT-02); engine `onItemHighlighted` = the Autocomplete highlight handler (AUT-STA-05); engine `filter` per AUT-STA-07; engine `filterQuery` = the trimmed committed value only when `mode` is `'both'`; engine `openOnInputClick` = the Autocomplete prop (default `false`). The engine's selected-value props, `itemToStringValue`, `isItemEqualToValue` and `formAutoComplete` are never passed (engine defaults: no consumer selection, serialization by `String`, `Object.is`, no hidden-input `autocomplete` attribute). Every other Root prop is forwarded unchanged.
  Source: `autocomplete/root/AutocompleteRoot.tsx:40-48,107-125,146-174`. Test: [L2] `AutocompleteRoot.test.tsx` "should pass autoComplete to the visible input" (no hidden-input `autocomplete`), "mode=\"%s\": uses the locale when applying the default filter" (×2).
- **AUT-STR-04** — MUST. Parts other than `Root` and `Value` rendered outside an `Autocomplete.Root` throw the Combobox context errors of CMB-STR-02 verbatim — the messages name Combobox, e.g. "Base UI: ComboboxRootContext is missing. Combobox parts must be placed within <Combobox.Root>." (likewise the Portal/Positioner nesting errors of CMB-STR-04). `Autocomplete.Value` rendered outside a Root does **not** throw; it renders the empty string (or calls a function child with `''`).
  Source: `combobox/root/ComboboxRootContext.tsx:27-38,60-62`; `autocomplete/value/AutocompleteValue.tsx:14`. Test: none (the throwing paths are tested only through Combobox parts, CMB-STR-02).
- **AUT-STR-05** — MUST. `Autocomplete.Separator` is the shared listbox separator (CMB-API-64, CMB-DOM-16): `<div role="presentation" data-orientation="horizontal|vertical">`, `orientation` defaulting to `'horizontal'`, no `aria-orientation`; it needs no Root.
  Source: `autocomplete/separator/AutocompleteSeparator.tsx:30-32`; `utils/listbox-separator/ListboxSeparator.tsx:16-24`. Test: [L2] `AutocompleteRoot.test.tsx` "exposes the orientation attribute rendered by Separator"; `ListboxSeparator.test.tsx` "exposes the listbox separator behavior" (`Autocomplete.Separator` variant).

### 2.1 Adoption from base

Substitutions applied when reading an adopted CMB requirement:

| Where CMB says | Read |
|---|---|
| `Combobox.<Part>` | `Autocomplete.<Part>` |
| `Combobox.Root` / "the Root" | `Autocomplete.Root` (engine configured per AUT-STR-03) |
| `inputValue` / `defaultInputValue` / `onInputValueChange` | `value` / `defaultValue` / `onValueChange` |
| `itemToStringLabel` | `itemToStringValue` |
| `Combobox.Root.ChangeEventDetails` / `…OpenChangeEventDetails` / `…HighlightEventDetails` / `…Actions` / `…ChangeEventReason` / `…HighlightEventReason` | `Autocomplete.Root.*` of the same name (AUT-API-21) |
| `Combobox.useFilteredItems` | `Autocomplete.useFilteredItems` |
| "single mode" / "multiple mode" / "the selection" / "the selected value / label" | not applicable: Autocomplete is the engine's `none` mode; a clause that applies only in single or multiple mode does not apply, and a clause that depends on a selection behaves as with no selection, except as stated in AUT-STA-08 and AUT-KEY-07 |

**Not adopted** (no counterpart in Autocomplete):

| Topic | CMB IDs |
|---|---|
| Selected value, `multiple`, selection serialization and equality | CMB-API-01…04, CMB-API-31, CMB-API-33, CMB-API-34, CMB-STA-20, CMB-STA-22, CMB-DOM-19, CMB-EVT-03, CMB-EVT-08, CMB-EDGE-11 |
| `Combobox.Value` (replaced by an unrelated part) | CMB-API-46, CMB-API-47 |
| `Combobox.Label` | CMB-STR-10, CMB-STA-01, CMB-DOM-17, the `Combobox.Label` clauses of CMB-ARIA-16 and CMB-EDGE-03 |
| Chips, Chip, ChipRemove | CMB-STR-09, CMB-STA-03, CMB-STA-04, CMB-API-53, CMB-API-54, CMB-DOM-11, CMB-ARIA-12, CMB-KEY-07…10, the Chips clauses of CMB-PTR-08, CMB-PTR-09, CMB-PTR-10, CMB-FOC-07 |
| ItemIndicator | CMB-API-61, CMB-STA-14, CMB-DOM-10, CMB-ANI-05 |
| `createItems()` collections (not accepted by the Autocomplete types, §16 Q6) | CMB-API-67, CMB-API-68, the collection clauses of CMB-API-12…14, CMB-EDGE-02, CMB-EDGE-12, the second clause of CMB-EDGE-08 |
| `Combobox.useFilter` (replaced by AUT-API-19) | CMB-API-65 |
| Closed-trigger typeahead (single mode only) | CMB-KEY-12 (CMB-FOC-10's 0 ms `forceMount` on Trigger focus still runs and is adopted) |
| Frozen close query and selection bypass | CMB-ANI-02 |
| Inline initial highlight seeded from the selection | first sentence of CMB-STA-28 (the blur save/restore sentence is adopted) |
| Single-mode autofill force-mount | autofill clause of CMB-STA-32 |

**Overridden** (base ID → replacing ID):

| Base | Replaced by |
|---|---|
| CMB-STR-02 (outside-Root error for `Value`) | AUT-STR-04 |
| CMB-API-09, CMB-API-10, CMB-API-11 | AUT-API-01, AUT-API-02, AUT-API-03 |
| CMB-API-14, CMB-API-15 | AUT-API-09, AUT-API-10 |
| CMB-API-17 | AUT-API-05 |
| CMB-API-20 (default) | AUT-API-07 |
| CMB-API-25, CMB-API-26 (target of `name`/`form`) | AUT-FRM-02 |
| CMB-API-28 | AUT-FRM-04 |
| CMB-API-29 (`aria-autocomplete` and selection clauses) | AUT-API-17 |
| CMB-API-32 | AUT-API-08 |
| CMB-API-37 | AUT-API-15 |
| CMB-API-69 | AUT-API-21 |
| CMB-STA-02, CMB-STA-07 (`placeholder`) | AUT-STA-01 |
| CMB-STA-06 (`visible`) | AUT-STA-02 |
| CMB-STA-13 (`selected`) | AUT-STA-03 |
| CMB-STA-18 (input-text ownership) | AUT-STA-04 |
| CMB-STA-19 | AUT-STA-09 |
| CMB-STA-21 | AUT-STA-10 |
| CMB-STA-23 (which filter and query) | AUT-STA-07 |
| CMB-STA-25 | AUT-STA-08 |
| CMB-STA-26 (`'always'` clauses) | AUT-STA-11 |
| CMB-STA-30 | AUT-STA-09 |
| CMB-STA-34 (`inputOwnsFormValue`) | AUT-FRM-02 |
| CMB-DOM-01 (`name`, `required`) | AUT-DOM-01 |
| CMB-DOM-03, CMB-DOM-04 (`data-placeholder`) | AUT-DOM-04 |
| CMB-DOM-06 (`data-visible`) | AUT-DOM-05 |
| CMB-DOM-09 (`data-selected`) | AUT-DOM-03 |
| CMB-DOM-18, CMB-DOM-20 | AUT-DOM-02 |
| CMB-ARIA-04 | AUT-ARIA-01 |
| CMB-ARIA-09 (`aria-selected`) | AUT-ARIA-02 |
| CMB-KEY-01 (`focusItemOnOpen`, `resetOnPointerLeave`), CMB-KEY-02 (opening highlight) | AUT-KEY-01, AUT-PTR-05 |
| CMB-KEY-04 (submit on item activation) | AUT-KEY-04 |
| CMB-KEY-05 | AUT-KEY-07 |
| CMB-KEY-16 (typed query discarded) | AUT-KEY-09 |
| CMB-KEY-17 | AUT-KEY-08 |
| CMB-PTR-01 (enabled by default) | AUT-PTR-01 |
| CMB-PTR-04 (`submitOnItemClick` never set) | AUT-PTR-03 |
| CMB-PTR-05 | AUT-PTR-02 |
| CMB-PTR-06 (inline completion) | AUT-PTR-04 |
| CMB-PTR-07 | AUT-PTR-06 |
| CMB-FOC-06 (next open starts blank) | AUT-FOC-01 |
| CMB-FOC-08 (value committed to `onBlur` validation) | AUT-FRM-05 |
| CMB-DIS-02, CMB-DIS-03, CMB-DIS-04 (input restored on unmount) | AUT-DIS-01 |
| CMB-EVT-04 | AUT-EVT-01 |
| CMB-EVT-05 (inline side effect) | AUT-EVT-04 |
| CMB-EVT-07 (cancel in the input-value callback) | AUT-EVT-02 |
| CMB-EVT-10 | AUT-EVT-05 |
| CMB-EVT-11 | AUT-FRM-06 |
| CMB-FRM-01…05, CMB-FRM-07, CMB-FRM-08 | AUT-FRM-01…07 |
| CMB-EDGE-03 (controlled-state diagnostics) | AUT-EDGE-01 |
| CMB-EDGE-06 (form-value ownership in server HTML) | AUT-EDGE-03 |

Adopted unchanged (non-exhaustive reminder): CMB-STR-01, -03…08; CMB-API-05…08, -12, -13, -16, -18, -19, -21…24, -27, -30, -35, -36, -38…45, -48…52, -55…60, -62…64, -66; CMB-STA-05, -08…12, -15…17, -24, -27, -29, -31, -33; CMB-DOM-02, -05, -07, -08, -12…16, -21…24; CMB-ARIA-01…03, -05…08, -10, -11, -13…15, -17; CMB-KEY-03, -06, -11, -13…15; CMB-PTR-02, -03, -08 (InputGroup clause), -11…13; CMB-FOC-01…05, -09…11; CMB-DIS-01, -05…08; CMB-EVT-01, -02, -06, -09, -12; CMB-FRM-06; CMB-ANI-01, -03, -04, -06; CMB-POS-01…04; CMB-EDGE-01, -04, -05, -07, -09, -10.

Structural-only parts are Combobox's (`Icon`, `Row`, `Separator`, `Arrow`, `Backdrop`, `GroupLabel`, `Status`; `Value` and `Collection` render no element). Parts that may be omitted: without `Trigger` the input is the only opener (typing, arrow keys, and clicking only when `openOnInputClick`); without `Clear`, <kbd>Escape</kbd> on a closed popup still clears the text (AUT-KEY-07); without `Portal`/`Positioner`/`Popup` the Root must be `inline` (and then an item press does not fill the input, AUT-PTR-02); without `Input` (Trigger + `Value` only) the hidden input carries the form value.

## 3. Public API

Props common to every element-rendering part — `className`, `style`, `render` — follow [rendering-and-composition](../architecture/rendering-and-composition.md) (RND-API-01…03). Re-exported parts: see [combobox](combobox.md) §3 (AUT-STR-02). Only `Root`, `Value` and the aliased parts whose observable contract differs are specified here.

### 3.1 `Autocomplete.Root`

**Default element:** none (renders no element). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no.

`Autocomplete.Root` is generic in `ItemValue` (the item type); a first overload infers it from grouped `items` (`{ items: readonly any[] }[]`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `string \| number \| string[]` (the native input `value` type) | — | Controlled input text. `null` is read as `''`. | AUT-API-01 |
| `defaultValue` | `string \| number \| string[]` | `''` | Initial uncontrolled input text. | AUT-API-02 |
| `onValueChange` | `(value: string, eventDetails: ChangeEventDetails) => void` | — | Called for every input-text change. | AUT-API-03 |
| `mode` | `'list' \| 'both' \| 'inline' \| 'none'` | `'list'` | Filtering and inline completion. | AUT-API-04 |
| `autoHighlight` | `boolean \| 'always'` | `false` | Automatic first-item highlight. | AUT-API-05 |
| `keepHighlight` | `boolean` | `false` | Keep the highlight when the pointer leaves the list. | AUT-API-06 |
| `highlightItemOnHover` | `boolean` | `true` | Pointer movement highlights items. | CMB-API-18 |
| `openOnInputClick` | `boolean` | `false` | Pressing the input opens the popup. | AUT-API-07 |
| `itemToStringValue` | `(itemValue: ItemValue) => string` | — | Label of an item value (input fill, filtering, inline completion). | AUT-API-08 |
| `items` | `readonly ItemValue[] \| readonly Group<ItemValue>[]` | — | Source items (flat or grouped; no `createItems()` collections). | AUT-API-11 |
| `filteredItems` | `readonly ItemValue[] \| readonly Group<ItemValue>[]` | — | Externally filtered items. | AUT-API-11 |
| `filter` | `null \| (item, query, itemToString?) => boolean` | collator `contains` | Item matcher (ignored in modes `inline`/`none`). | AUT-API-09 |
| `locale` | `Intl.LocalesArgument` | runtime locale | Collator locale of the default filter. | AUT-API-10 |
| `limit` | `number` | `-1` | Maximum number of rendered items from `items`. | AUT-API-11 |
| `submitOnItemClick` | `boolean` | `false` | Item activation submits the owning form. | AUT-API-12 |
| `onItemHighlighted` | `(value: ItemValue \| undefined, eventDetails: HighlightEventDetails) => void` | — | Highlight notifications. | AUT-API-15 |
| `inline` | `boolean` | `false` | List rendered without the component's own popup. | AUT-API-16 |
| `readOnly` | `boolean` | `false` | Browsing allowed, no edits, no inline completion. | AUT-API-17 |
| `open` / `defaultOpen` / `onOpenChange` / `onOpenChangeComplete` | as Combobox | `—` / `false` / — / — | Popup open state. | CMB-API-05…08 |
| `actionsRef` | `RefObject<{ unmount(); close() } \| null>` | — | Imperative actions. | CMB-API-36 |
| `loopFocus` | `boolean` | `true` | Arrow navigation loops through the input. | CMB-API-19 |
| `grid` | `boolean` | `false` | Grid layout/navigation. | CMB-API-21 |
| `virtualized` | `boolean` | `false` | Items are externally virtualized. | CMB-API-22 |
| `modal` | `boolean` | `false` | Modal popup. | CMB-API-24 |
| `name` | `string` | Field name | Form field name (on the visible input or the hidden input). | AUT-API-13 |
| `form` | `string` | — | Owning form id. | AUT-API-13 |
| `required` | `boolean` | `false` | Text required for submission. | AUT-FRM-04 |
| `disabled` | `boolean` | `false` | Ignores user interaction (OR-ed with Field). | CMB-API-30 |
| `id` | `string` | generated | Root id. | CMB-API-27 |
| `inputRef` | `Ref<HTMLInputElement>` | — | Handle to the *hidden* input. | CMB-API-35 |
| `children` | `ReactNode` | — | Parts. | — |

**Consumed (not forwarded to any element):** every prop (Root renders no element). **Not accepted** (present on `Combobox.Root`, absent here): `multiple`, `defaultInputValue`/`inputValue`/`onInputValueChange` (renamed), `itemToStringLabel` (renamed), `isItemEqualToValue`, `autoComplete` (browser hint; set it on `Autocomplete.Input` instead).

- **AUT-API-01** — MUST. `value` makes the input text controlled. Any value other than `undefined` — including `null` — puts the Root in controlled mode, re-evaluated on every parameter update; a controlled `null` is read as `''` (the input is empty and the full list is shown). The Root never writes a controlled value; every attempted change is reported through `onValueChange`. The controlled mode may change between parameter updates without any diagnostic (AUT-EDGE-01). Port: relaxed (ADR 05 Amendments item 1).
  Source: `autocomplete/root/AutocompleteRoot.tsx:56,66-74,78`. Test: [L2] `AutocompleteRoot.test.tsx` "treats a controlled null value as an empty query", "mode=\"both\": external controlled updates replace the temporary inline value".
- **AUT-API-02** — MUST. `defaultValue` seeds the uncontrolled input text once (`defaultValue ?? ''`); later changes of the prop are ignored. A non-empty default marks the field filled from the first commit (AUT-FRM-03).
  Source: `autocomplete/root/AutocompleteRoot.tsx:57,121`. Test: [L2] `AutocompleteValue.test.tsx` "renders current input value via function child"; `AutocompleteRoot.test.tsx` "adds [data-filled] attribute when already filled with defaultValue", "submits a default popup input value through native FormData before the popup opens".
- **AUT-API-03** — MUST. `onValueChange(value, eventDetails)` is called synchronously for every change of the input text that the engine writes — typing, composition end, item activation, Clear, <kbd>Escape</kbd> on a closed popup, hidden-input autofill — with the reasons of AUT-EVT-01. It is **not** called for inline-completion changes of the displayed text (AUT-STA-05), nor for the engine's internal selection. In uncontrolled mode the Root stores the new text *before* calling the callback (AUT-EVT-02).
  Source: `autocomplete/root/AutocompleteRoot.tsx:82-88,122`; `combobox/root/AriaCombobox.tsx:655-661`. Test: [L2] `AutocompleteRoot.test.tsx` "preserves the typed value when the popup is dismissed with Escape", "ignores hidden-input autofill when readOnly"; `AutocompleteRoot.spec.tsx` (value typed `string`).
- **AUT-API-04** — MUST. `mode` (default `'list'`) selects filtering and inline completion:

  | `mode` | List filtered by the input text | Inline completion (input shows the highlighted item's label) | `aria-autocomplete` |
  |---|---|---|---|
  | `'list'` | yes (`filter`, default collator `contains`) | no | `list` |
  | `'both'` | yes, against the *typed* text, not the completed text | yes | `both` |
  | `'inline'` | no (every item, `filter` ignored) | yes | `inline` |
  | `'none'` | no (every item, `filter` ignored) | no | `none` |

  Inline completion is additionally disabled while `readOnly` (AUT-API-17). Filtering is specified in AUT-STA-07, inline completion in AUT-STA-05/06.
  Source: `autocomplete/root/AutocompleteRoot.tsx:45,51-52,79-80,115-119,199-206`. Docs: `components/autocomplete/page.mdx` §Inline autocomplete. Test: [L2] `AutocompleteRoot.test.tsx` "mode=\"list\" (default): no inline overlay, consumer handles filtering", "mode=\"both\": inline overlay + autocomplete handles filtering", "mode=\"inline\": static items with inline overlay", "mode=\"none\": static items without inline overlay".
- **AUT-API-05** — MUST. `autoHighlight` (default `false`) accepts three values: `false` — no automatic highlight; `true` — typing a non-blank query highlights the first filtered item and keeps the highlight while the query changes (CMB-STA-26), and an <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> that opens the popup highlights the first/last item (AUT-KEY-01); `'always'` — additionally the first item is highlighted whenever the list is open (or inline) with no highlight and at least one item (AUT-STA-11). Any truthy value disables escaping past the list ends (`allowEscape: false`, CMB-KEY-03), so navigation wraps.
  Source: `combobox/root/AriaCombobox.tsx:287-292,1199-1204,1424-1426`; `autocomplete/root/AutocompleteRoot.tsx:215-221`. Docs: `components/autocomplete/page.mdx` §Auto highlight. Test: [L2] `AutocompleteRoot.test.tsx` "prop: autoHighlight" (12 tests, §13) (L4 for the arrow-key open and wrap clauses).
- **AUT-API-06** — MUST. `keepHighlight` (default `false`) is passed as the inverse of the list-navigation `resetOnPointerLeave`: when `true`, the pointer leaving the list does not clear the highlight, and keyboard navigation continues from the kept item.
  Source: `combobox/root/AriaCombobox.tsx:129,1428`. Test: [L4] `AutocompleteRoot.test.tsx` "keeps the current highlight when the pointer leaves the list", "clears the highlight when the pointer leaves the list without keepHighlight", "continues keyboard navigation from the kept highlight after pointer leave".
- **AUT-API-07** — MUST. `openOnInputClick` defaults to `false` (Combobox: `true`): a press on the Input, or on the InputGroup padding, focuses the input but does not open the popup; typing or an arrow key opens it. Because the default is `false`, emptying the input by typing closes the popup (AUT-KEY-08). With `true` the Combobox input-press opener applies (CMB-PTR-01, CMB-PTR-08).
  Source: `autocomplete/root/AutocompleteRoot.tsx:41,111,292-296`; `combobox/root/AriaCombobox.tsx:1384-1392`; `combobox/utils/handleInputPress.ts:33-37`. Test: [L4] `AutocompleteRoot.test.tsx` "does not highlight first/last item when pressing ArrowDown/ArrowUp initially" (click does not open), "Enter submits when no item is highlighted" (`openOnInputClick` opens), "does not expose data-placeholder on Trigger or InputGroup".
- **AUT-API-08** — MUST. `itemToStringValue(itemValue)` converts an item value to the text used for display: the text written into the input on item activation, the inline-completion text, and the string the default filter matches against. When omitted, an object's `label` property is used, else its `value` property, else `String(value)` (UTL-I18N-13). It is **not** a form serialization: the form submits the input text (AUT-FRM-01).
  Source: `autocomplete/root/AutocompleteRoot.tsx:46,100-104,110,258-262`; `combobox/root/AriaCombobox.tsx:231-239,866-875`; `internals/resolveValueLabel.tsx:68-80`. Test: [L2] `AutocompleteRoot.test.tsx` "filters and displays using label for {label} objects", "uses itemToStringValue when object lacks label", "filters and displays using value for {value} objects", "mode=\"%s\": does not apply default filtering when filter is null" (×2).
- **AUT-API-09** — MUST. `filter(item, query, itemToString?)`: in modes `'list'` and `'both'` a function replaces the default matcher; `null` disables filtering (every item is shown); `undefined` uses the default — the plain collator `contains` (UTL-I18N-09/12/13) built with `locale`, with no selection-aware behavior. In modes `'inline'` and `'none'` the prop is ignored and never called. `itemToString` passed to the function is the Autocomplete `itemToStringValue` (or `undefined`).
  Source: `autocomplete/root/AutocompleteRoot.tsx:76-80,114`; `combobox/root/AriaCombobox.tsx:301-311`. Test: [L2] `AutocompleteRoot.test.tsx` "mode=\"%s\": uses a custom filter instead of the locale-aware default" (×2), "mode=\"%s\": does not apply default filtering when filter is null" (×2), "mode=\"inline\": does not call a custom filter".
- **AUT-API-10** — MUST. `locale` configures the collator of the default filter (Root-built) and is also forwarded to the engine; it has no effect on a custom `filter`. Example: with `locale="tr"`, typing `i` matches `İzmir` and not `Isparta`.
  Source: `autocomplete/root/AutocompleteRoot.tsx:76`; `internals/filter.ts:6-22`. Test: [L2] `AutocompleteRoot.test.tsx` "mode=\"%s\": uses the locale when applying the default filter" (×2).
- **AUT-API-11** — MUST. `items`, `filteredItems` and `limit` behave as CMB-API-12, CMB-API-13 and CMB-API-16 for flat and grouped arrays (nullish entries unsupported, `limit` never truncates `filteredItems`, `limit` also applies in modes `'inline'`/`'none'`). The public type does not accept a `createItems()` collection.
  Source: `autocomplete/root/AutocompleteRoot.tsx:17-36,180-193`; `combobox/root/AriaCombobox.tsx:363-439`. Test: [L2] `AutocompleteRoot.spec.tsx` "Autocomplete has no item type parameter, so collections are not accepted" (type-level); `AutocompleteRoot.test.tsx` "highlights a candidate arriving after typing" (`items` arriving later).
- **AUT-API-12** — MUST. `submitOnItemClick` (default `false`): when `true`, activating an item (pointer click, drag-release, or <kbd>Enter</kbd> on the highlighted item) first applies the item press synchronously (input filled, popup closing) and then requests submission of the owning form (AUT-PTR-03); when `false`, item activation never submits and <kbd>Enter</kbd> on a highlighted item is prevented (AUT-KEY-04).
  Source: `combobox/item/ComboboxItem.tsx:150-161`; `combobox/root/AriaCombobox.tsx:143,945-950`. Test: [L4] `AutocompleteRoot.test.tsx` "prop: submitOnItemClick" (6 tests, §13).
- **AUT-API-13** — MUST. `name` (defaulting to the surrounding `Field.Root`'s name) and `form` are applied to whichever control owns the form value: the visible `Autocomplete.Input` when it is outside the popup or the Root is `inline`, otherwise the hidden input (AUT-FRM-02). `form` is applied to both the visible input and the hidden input.
  Source: `combobox/input/ComboboxInput.tsx:111,185-186`; `combobox/root/AriaCombobox.tsx:280,1589-1590,1692-1693`. Test: [L2] `AutocompleteRoot.test.tsx` "submits the typed input value when name is provided on Autocomplete.Root", "submits to an external form when `form` is provided" (L4 for the cited native-FormData submission tests).
- **AUT-API-14** — MUST. The Combobox engine options not exposed by Autocomplete take fixed values: value equality is `Object.is` (used only for highlight bookkeeping); the hidden input never has an `autocomplete` attribute; there is no consumer-visible selected value. A browser autofill hint goes on `Autocomplete.Input` (`autoComplete="…"` overrides the default `off`, AUT-DOM-01).
  Source: `autocomplete/root/AutocompleteRoot.tsx:146-174`; `combobox/root/AriaCombobox.tsx:134,141,1694`. Test: [L2] `AutocompleteRoot.test.tsx` "should pass autoComplete to the visible input".
- **AUT-API-15** — MUST. `onItemHighlighted(value | undefined, { reason, event, index })` has the contract of CMB-API-37/CMB-EVT-05. The consumer callback runs first; afterwards, for a non-`pointer` reason, the Root updates the inline-completion text (AUT-STA-05).
  Source: `autocomplete/root/AutocompleteRoot.tsx:90-105`. Test: [L4] `AutocompleteRoot.test.tsx` "passes native mouse and pointer events for pointer highlights", "calls onItemHighlighted when the popup auto highlights on open".
- **AUT-API-16** — MUST. `inline` has the contract of CMB-API-23 (permanent `aria-expanded`, dismissal disabled, highlight saved on blur and restored on refocus, Enter without highlight keeps the text); the docs require `open` to be passed with it. In addition, with an inline list that has no `Autocomplete.Popup`, an item press does not fill the input (AUT-PTR-02), and the visible input owns the form value (AUT-FRM-02).
  Source: `autocomplete/root/AutocompleteRoot.tsx:207-214`; `combobox/root/AriaCombobox.tsx:866-868,1564`. Test: [L2] `AutocompleteRoot.test.tsx` "highlights asynchronous results while an inline input stays focused", "submits the inline input value through native FormData".
- **AUT-API-17** — MUST. `readOnly` (default `false`) behaves as CMB-API-29 (popup opens and can be browsed; typing, item activation and Clear do nothing; hidden-input autofill is ignored; <kbd>Enter</kbd> on a highlighted item is swallowed), and additionally: inline completion is disabled in every mode, so arrow navigation never changes the displayed text; turning `readOnly` on while an inline completion is displayed discards it (the typed text reappears and the completion does not come back when `readOnly` is turned off); `aria-autocomplete` becomes `"none"` (AUT-ARIA-01).
  Source: `autocomplete/root/AutocompleteRoot.tsx:50-51,60-64`; `combobox/root/AriaCombobox.tsx:1372,1629`; `combobox/item/ComboboxItem.tsx:186,196`. Test: [L4] `AutocompleteRoot.test.tsx` "ignores hidden-input autofill when readOnly", "opens the list with the arrow keys but does not commit on item press when readOnly", "does not fill the input with the highlighted item when readOnly", "drops a pending inline completion when readOnly is turned on", "exposes aria-autocomplete=\"none\" when readOnly".

#### State object

`Autocomplete.Root.State` is an empty object; the Root renders no element.
Source: `autocomplete/root/AutocompleteRoot.tsx:128`.

#### Data attributes

None (no element).

#### CSS variables

None.

### 3.2 `Autocomplete.Value`

**Default element:** none (renders a fragment). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode \| (value: string) => ReactNode` | — | Overrides the rendered content. | AUT-API-18 |

- **AUT-API-18** — MUST. `Autocomplete.Value` renders, in priority order: `children(String(text))` when `children` is a function; `children` when it is any other non-nullish node; otherwise the text itself. *Text* is the engine's current input text, i.e. the displayed text including an active inline completion (AUT-STA-04), `''` when nothing has been typed. It has no `placeholder` prop and no label resolution. Typically rendered inside a Trigger when the input is inside the popup.
  Source: `autocomplete/value/AutocompleteValue.tsx:11-26`; `combobox/root/AriaCombobox.tsx:1714`. Test: [L2] `AutocompleteValue.test.tsx` "renders current input value via function child", "renders function child with empty string when no value typed", "overrides the display when children is a static ReactNode", "renders complex ReactNode children"; `AutocompleteRoot.test.tsx` "commits a keyboard selection, restores trigger focus, and preserves it on reopen".

#### State object

Empty (`{}`; `Autocomplete.Value.State`). Source: `autocomplete/value/AutocompleteValue.tsx:28`.

#### Data attributes

None.

#### CSS variables

None.

### 3.3 `Autocomplete.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

Props, data attributes and behavior as `Combobox.Trigger` (CMB §3.9; `nativeButton` default `true`, `disabled`, Root id when the input is inside the popup) except: there is no `data-placeholder` (AUT-DOM-04) and no closed-trigger typeahead (AUT-KEY-10).

#### State object

| Field | Type | Meaning |
|---|---|---|
| field state | FFI | `valid`, `touched`, `dirty`, `filled`, `focused` (and `disabled` from Field) |
| `open` | `boolean` | popup open |
| `disabled` | `boolean` | resolved disabled |
| `readOnly` | `boolean` | Root readOnly |
| `popupSide` | `Side \| null` | side of the mounted positioner |
| `listEmpty` | `boolean` | filtered list empty |

- **AUT-STA-01** — MUST. The Trigger and InputGroup state objects are the Combobox ones (CMB-STA-07, CMB-STA-02) with `placeholder` always `false` (the runtime object still carries the field; the public `Autocomplete.Trigger.State`/`Autocomplete.InputGroup.State` types omit it), so neither part ever exposes `data-placeholder`.
  Source: `combobox/trigger/ComboboxTrigger.tsx:134-142`; `combobox/input-group/ComboboxInputGroup.tsx:34-49`; `autocomplete/trigger/AutocompleteTrigger.tsx:16-37`; `autocomplete/input-group/AutocompleteInputGroup.tsx:16-37`. Test: [L2] `AutocompleteRoot.test.tsx` "does not expose data-placeholder on Trigger or InputGroup".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open`, `data-pressed` | popup open | CMB-DOM-04 |
| `data-disabled`, `data-readonly`, `data-required` | respective flags (`data-required`: see CMB §16 Q1) | CMB-DOM-04 |
| `data-popup-side` | popup mounted | CMB-DOM-04 |
| `data-list-empty` | filtered list empty | CMB-DOM-04 |
| `data-valid`/`data-invalid`, `data-touched`, `data-dirty`, `data-filled`, `data-focused` | field state | CMB-DOM-04 |
| `data-placeholder` | never | AUT-DOM-04 |

#### CSS variables

None.

### 3.4 `Autocomplete.InputGroup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

Props and behavior as `Combobox.InputGroup` (CMB §3.3; `role="group"`, press focuses the input and opens only when `openOnInputClick`, excluded from outside-press dismissal). State: AUT-STA-01. Data attributes: as CMB-DOM-03 without `data-placeholder` (AUT-DOM-04): `data-popup-open`, `data-pressed`, `data-disabled`, `data-readonly`, `data-popup-side`, `data-list-empty`, `data-valid`/`data-invalid`, `data-touched`, `data-dirty`, `data-filled`, `data-focused`. CSS variables: none.

### 3.5 `Autocomplete.Input`

**Default element:** `<input>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

The `Combobox.Input` part itself (CMB §3.7: `disabled`, `id`; state CMB-STA-05; data attributes CMB-DOM-02). Autocomplete-specific DOM output: AUT-DOM-01.

### 3.6 `Autocomplete.Clear`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

The `Combobox.Clear` part itself (CMB §3.8: `nativeButton`, `disabled`, `keepMounted`, default content `"x"`, `tabindex="-1"`).

- **AUT-STA-02** — MUST. The Clear state is `{ open, disabled, visible, transitionStatus }` (CMB-STA-06) where `visible` is "the engine's input text is not the empty string" (including a displayed inline completion); the enter/exit transition (CMB-ANI-04) follows that flag. Server: relaxed, see §9.
  Source: `combobox/clear/ComboboxClear.tsx:55-62,71`. Test: [L2] `ComboboxClear.test.tsx` "renders and clears the typed value in no-selection mode".

Data attributes: `data-visible` (AUT-DOM-05), `data-disabled`, `data-popup-open`, `data-starting-style`/`data-ending-style` (CMB-DOM-06). CSS variables: none.

### 3.7 `Autocomplete.Item`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `any` | `null` | The item's value (passed to `itemToStringValue`). | CMB-API-56 |
| `index` | `number` | automatic | Explicit list index. | CMB-API-57 |
| `disabled` | `boolean` | `false` | Local disabled, OR-ed with the Root's. | CMB-API-58 |
| `nativeButton` | `boolean` | `false` | Button semantics (UBT). | CMB-API-59 |
| `onClick` | `(event) => void` | — | Fires on pointer activation and on <kbd>Enter</kbd> while highlighted. | CMB-API-60 |
| `children` | `ReactNode` | — | Item content (listed in the docs outline; no Autocomplete-specific handling). | — |
| `id` | — | `${rootId}-${index}` | Not in the public type (`Omit<…, 'id'>`); a runtime `id` still overrides as in CMB-DOM-09. | AUT-DOM-03 |

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | resolved disabled |
| `highlighted` | `boolean` | item is the active (virtual-focused) item |

- **AUT-STA-03** — MUST. The Item state is the Combobox one (CMB-STA-13) with `selected` always `false` (items are never selectable in Autocomplete; the runtime object still carries `selected: false`, the public `Autocomplete.Item.State` type omits it).
  Source: `combobox/item/ComboboxItem.tsx:72,82,144-148`; `autocomplete/item/AutocompleteItem.tsx:14-23`. Test: [L2] `AutocompleteItem.test.tsx` "does not expose data-selected when reopening after a value is chosen".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-highlighted` | highlighted | CMB-DOM-09 |
| `data-disabled` | disabled | CMB-DOM-09 |
| `data-selected` | never | AUT-DOM-03 |

#### CSS variables

None.

### 3.8 `Autocomplete.Separator`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes. Props: `orientation` (default `'horizontal'`, CMB-API-64). State `{ orientation }` (CMB-STA-15). Data attribute `data-orientation` (CMB-DOM-16). See AUT-STR-05.

### 3.9 Re-exported parts

`Icon`, `List`, `Status`, `Portal`, `Backdrop`, `Positioner`, `Popup`, `Arrow`, `Group`, `GroupLabel`, `Row`, `Collection`, `Empty`: props, state objects, data attributes, CSS variables and requirements as [combobox](combobox.md) §3.11–§3.23 (AUT-STR-02). In particular the List exposes `aria-readonly="true"` when the Root is `readOnly` (CMB-ARIA-08) and never `aria-multiselectable`.

### 3.10 Imperative API and non-part exports

| Export | Shape | Req |
|---|---|---|
| `Autocomplete.useFilter(options?)` | `{ locale?, ...Intl.CollatorOptions }` → `{ contains, startsWith, endsWith }` | AUT-API-19 |
| `Autocomplete.useFilteredItems<T>()` | `() => T[]` (read-only) | AUT-API-20 |
| `Autocomplete.Root.Actions` | `{ unmount(): void; close(): void }` | CMB-API-36 |

- **AUT-API-19** — MUST. `Autocomplete.useFilter(options)` is the shared collator filter factory itself (`getFilter`, UTL-I18N-08…13): it accepts `locale` plus any `Intl.CollatorOptions` (defaults `usage: 'search'`, `sensitivity: 'base'`, `ignorePunctuation: true`, caller options win), returns `{ contains, startsWith, endsWith }`, and returns the same cached object for equal locale/options. It has **no** `multiple`/`value` options and none of `Combobox.useFilter`'s selection-aware behavior. It is intended for external filtering through `filteredItems` (docs §useFilter).
  Source: `autocomplete/index.parts.ts:24`; `combobox/root/utils/useFilter.ts:12`; `internals/filter.ts:6-83`. Docs: `components/autocomplete/page.mdx` §useFilter. Test: none in `autocomplete/` (mechanism: UTL-I18N-08…13).
- **AUT-API-20** — MUST. `Autocomplete.useFilteredItems()` is `Combobox.useFilteredItems()` (CMB-API-66): the Root's current filtered items (in modes `'inline'`/`'none'` every item, subject to `limit`); it throws the derived-items context error (CMB-STR-02) outside a Root.
  Source: `autocomplete/index.parts.ts:25`; `combobox/root/utils/useFilteredItems.ts:7-10`. Docs: `components/autocomplete/page.mdx` §useFilteredItems. Test: none in `autocomplete/`.

### 3.11 Base-component parameters overridden by this component

| Base part / prop | Base default or meaning | Under Autocomplete |
|---|---|---|
| `Root.value` / `defaultValue` / `onValueChange` | selected item value | the input text (`string`) |
| `Root.inputValue` / `defaultInputValue` / `onInputValueChange` | input text | removed (renamed to `value`/`defaultValue`/`onValueChange`) |
| `Root.openOnInputClick` | `true` | `false` |
| `Root.autoHighlight` | `boolean` | `boolean \| 'always'` |
| `Root.keepHighlight`, `Root.submitOnItemClick`, `Root.mode` | not exposed (engine `false`/`false`/`'list'`) | exposed |
| `Root.itemToStringLabel` / `itemToStringValue` | display label / form serialization | single `itemToStringValue` = display label; form submits the text |
| `Root.isItemEqualToValue`, `Root.multiple`, `Root.autoComplete` | exposed | not exposed |
| `Root.filter` default | collator `contains` (selection bypass in single mode) | collator `contains`; ignored in `inline`/`none` modes |
| `Item` `selected` / `aria-selected` / `data-selected` | from the selection | always `false` / absent / absent |
| `Trigger`, `InputGroup` `data-placeholder` | no selection | never |
| `Clear` visibility | a selection exists | the input text is non-empty |
| `Value` | selected label(s), `placeholder` | the input text, no `placeholder` |
| `useFilter` | `Combobox.useFilter` (selection-aware) | the plain collator filter |
| visible `Input` `name` / `required` | never | when it owns the form value / always |

### 3.12 Shared types and functions

- **AUT-API-21** — MUST. Exported types: `Autocomplete.Root.ChangeEventReason` = the 15-literal Combobox reason union (`trigger-press`, `input-press`, `outside-press`, `item-press`, `close-press`, `escape-key`, `list-navigation`, `focus-out`, `input-change`, `input-clear`, `clear-press`, `chip-remove-press`, `cancel-open`, `imperative-action`, `none`; CMB-EVT-01); `Autocomplete.Root.ChangeEventDetails` = the shared cancelable change details with that reason **without** the Combobox `isItemPress` field; `Autocomplete.Root.OpenChangeEventDetails` = those details plus `preventUnmountOnClose()`; `Autocomplete.Root.HighlightEventReason` (`'keyboard' | 'pointer' | 'none'`) and `HighlightEventDetails` (`{ reason, event, index }`, EVD); `Autocomplete.Root.Actions`; `Autocomplete.Root.State` (`{}`). Every part exports `Props`/`State` namespaced and as flat aliases (`AutocompleteInputProps` = `ComboboxInputProps`, …); `AutocompleteFilter`/`AutocompleteFilterOptions` are the collator `Filter`/options types; the attribute enums are exported as `AutocompleteTriggerDataAttributes`, `AutocompleteInputDataAttributes`, `AutocompleteInputGroupDataAttributes`, `AutocompleteClearDataAttributes`, `AutocompleteBackdropDataAttributes`, `AutocompletePositionerCssVariables`, `AutocompletePositionerDataAttributes`, `AutocompletePopupDataAttributes`, `AutocompleteArrowDataAttributes`, `AutocompleteItemDataAttributes` (`highlighted`, `disabled` only), `AutocompleteSeparatorDataAttributes` (`orientation`).
  Source: `autocomplete/root/AutocompleteRoot.tsx:128-144,299-308`; `autocomplete/index.ts:1-90`; `autocomplete/item/AutocompleteItemDataAttributes.ts:1-10`; `combobox/root/AriaCombobox.tsx:2019-2045`. Test: [L1] `AutocompleteRoot.spec.tsx` "isItemPress is only emitted by multiple-selection Combobox", "Hover can report a MouseEvent without pointer-specific methods" (type-level).

## 4. State model

### 4.1 Owned state

Autocomplete owns two consumer-visible pieces of state: the **committed text** (`value`/`defaultValue`) and the **open** flag (`open`/`defaultOpen`, CMB-STA-18 for `open`). On top of the committed text the Root keeps a transient **inline-completion text**; the text the input actually displays (and that the engine sees as its input text) is derived from both. The engine also keeps an internal selected value that no part exposes (AUT-STA-08).

- **AUT-STA-04** — MUST. *Displayed text* = the inline-completion text when inline completion is enabled (mode `'both'` or `'inline'` and not `readOnly`) and the inline-completion text is non-empty; otherwise the controlled `value` (`null` → `''`) when controlled; otherwise the internal committed text. The displayed text is what the Input shows, what `Autocomplete.Value` renders, what decides Clear visibility, and what the Field receives as its value (AUT-FRM-01). The committed text changes only through the value handler (AUT-EVT-02); in uncontrolled mode it is replaced by the handler's argument *before* `onValueChange` runs.
  Source: `autocomplete/root/AutocompleteRoot.tsx:50-74,82-88,120`. Test: [L2] `AutocompleteRoot.test.tsx` "mode=\"both\": inline overlay + autocomplete handles filtering", "mode=\"inline\": static items with inline overlay", "treats a controlled null value as an empty query".
- **AUT-STA-05** — MUST. *Inline completion.* Every time the highlight changes with a reason other than `pointer`, the inline-completion text becomes, when inline completion is enabled and the newly highlighted value is not nullish, the label of that value (`itemToStringValue`, else the `label`/`value` property, else `String`, UTL-I18N-13); otherwise it becomes `''` (so the committed text shows again). Pointer-driven highlight changes — hover and pointer leave — leave the inline-completion text unchanged. Highlight changes with reason `none` (auto-highlight, list changes, open/close cleanup) do update it.
  Source: `autocomplete/root/AutocompleteRoot.tsx:90-105`. Test: [L2] `AutocompleteRoot.test.tsx` "mode=\"both\": inline overlay + autocomplete handles filtering", "mode=\"both\": hovering items should not change the inline overlay (preserve temporary value)", "mode=\"list\" (default): no inline overlay, consumer handles filtering", "mode=\"none\": static items without inline overlay".
- **AUT-STA-06** — MUST. The inline-completion text is reset to `''`: (a) synchronously at the start of every committed-text change (the value handler); (b) after paint whenever `value`, the controlled/uncontrolled status or the inline enablement changes while the Root is controlled (so an external controlled update replaces a displayed completion); (c) after paint whenever any of those changes while inline completion is disabled (`readOnly` turned on, or `mode` changed to `'list'`/`'none'`) — the completion is *not* restored when inline completion is re-enabled; (d) on any non-pointer highlight clear (AUT-STA-05), including the highlight clear of the unmount cleanup (AUT-STA-09). A controlled consumer that ignores `onValueChange` still loses the inline completion on that attempt.
  Source: `autocomplete/root/AutocompleteRoot.tsx:60-64,82-84,100-104`. Test: [L2] `AutocompleteRoot.test.tsx` "mode=\"both\": external controlled updates replace the temporary inline value", "drops a pending inline completion when readOnly is turned on".
- **AUT-STA-07** — MUST. *Filtering per mode.* The engine's filtered-items derivation (CMB-STA-23) runs with: filter = always-true in modes `'inline'`/`'none'` and when `filter` is `null`, else `filter ?? collator.contains`; filter query = in mode `'both'` the trimmed **committed** text (so an inline completion never narrows the list), otherwise the trimmed displayed text. With the always-true filter every item is kept (still capped by `limit`). The single-mode selection bypass (CMB-STA-22) never applies.
  Source: `autocomplete/root/AutocompleteRoot.tsx:76-80,114-119`; `combobox/root/AriaCombobox.tsx:301-311,340-351`. Test: [L2] `AutocompleteRoot.test.tsx` "mode=\"both\": keeps filtering against the typed query during inline completion", "mode=\"inline\": does not call a custom filter", "mode=\"none\": static items without inline overlay", "mode=\"%s\": does not apply default filtering when filter is null" (×2).
- **AUT-STA-08** — MUST. *No selection.* No part exposes a selection: items are never `selected` (AUT-STA-03), the Trigger/InputGroup are never `placeholder` (AUT-STA-01), and the engine's selected-index synchronization (CMB-STA-25) is skipped. The engine nevertheless records the value of the last activated item as an internal selected value (initially `null`, reset to `null` by <kbd>Escape</kbd> on a closed popup); this internal value has exactly two observable effects in v1.9.0: the propagation rule of AUT-KEY-07, and — for lists rendered without `items` — a mounted Item whose value equals it registers its index as the selected index, which an opening <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> with `autoHighlight` then highlights instead of the first/last item (§16 Q3).
  Source: `combobox/root/AriaCombobox.tsx:294-299,854-865,1043-1045`; `combobox/item/ComboboxItem.tsx:72,82,111-135`; `combobox/input/ComboboxInput.tsx:351-366`. Test: [L2] `AutocompleteItem.test.tsx` "does not expose data-selected when reopening after a value is chosen"; `AutocompleteRoot.test.tsx` "does not expose data-placeholder on Trigger or InputGroup".
- **AUT-STA-09** — MUST. *Text persistence.* The initial displayed text is `value ?? defaultValue ?? ''` in both layouts — an Input mounted inside the popup does **not** blank itself (the engine always considers the input text externally provided). Closing the popup never writes the input: the unmount cleanup (after exit animations, CMB-STA-29) runs `onOpenChangeComplete(false)`, resets the query-tracking flags, and clears both the highlight (`activeIndex`, emitting a clearing `onItemHighlighted` with reason `none` when something was highlighted) and the selected index — nothing else. Reopening shows the previous text and the list filtered by it.
  Source: `combobox/root/AriaCombobox.tsx:283,313-330,952-961`; `combobox/input/ComboboxInput.tsx:113-118`. Test: [L2] `AutocompleteRoot.test.tsx` "commits a keyboard selection, restores trigger focus, and preserves it on reopen", "preserves the typed value when the popup is dismissed with Escape", "automatically unmounts with an actions ref and completes closing once".
- **AUT-STA-10** — MUST. The query is the trimmed displayed text (or, in mode `'both'`, the trimmed committed text for filtering, AUT-STA-07). No close query is frozen on close (the freeze of CMB-STA-21 exists only in single/multiple mode), so the list reflects the live text during exit animations; the typed-query flag of CMB-STA-21 is still tracked but has no effect in Autocomplete.
  Source: `combobox/root/AriaCombobox.tsx:340,800-831`. Test: none.

### 4.2 Highlight

- **AUT-STA-11** — MUST. *`autoHighlight: 'always'`.* Post-commit pre-paint, whenever the list is open (or inline) with no highlighted item and at least one candidate (filtered values when `items`/`filteredItems` are used, else the registered item values), the highlight is set to index 0 (reported to `onItemHighlighted` with reason `none`). When a typed query becomes empty (a cleared-query request), the highlight is set to index 0 synchronously and again after a microtask unless the popup closed or the user typed meanwhile. An inline Input blur does not save and clear the highlight (CMB-STA-28 save/restore is skipped), and a pending typed-query highlight is not discarded when an inline input loses focus. When the filtered list becomes empty the highlight is cleared after paint (CMB-STA-27) and restored to index 0 once items return.
  Source: `combobox/root/AriaCombobox.tsx:1101-1137,1186-1188,1198-1204,1254-1260`; `combobox/input/ComboboxInput.tsx:213-216`. Test: [L2] `AutocompleteRoot.test.tsx` "highlights the first item immediately when behavior is \"always\"", "keeps the latest pointer highlight on outside blur when behavior is \"always\"", "handles asynchronous results after an inline input blurs with autoHighlight=%s" (×2); `ComboboxRoot.test.tsx` "restores the first highlight after an always-highlighted query becomes empty" (L4 for the microtask re-highlight and pointer/outside-blur clauses).
- **AUT-STA-12** — MUST. *`autoHighlight: true`* follows CMB-STA-26 (pending typed-query highlight resolved to index 0 once a candidate exists and the list is navigable; discarded when the input becomes blank, when the Clear button empties it, when the popup closes — including a controlled close or a `keepMounted` close — or when an inline input has lost focus) and keeps whatever is highlighted when the query is cleared (no selection to return to). The highlight follows the first filtered item while typing (`aria-activedescendant` updated with the item ids), and trailing whitespace does not clear it.
  Source: `combobox/root/AriaCombobox.tsx:682-689,712-719,1080-1192`. Test: [L2] `AutocompleteRoot.test.tsx` "calls onItemHighlighted when the popup auto highlights on open", "highlights the first item when typing and keeps it during filtering", "links aria-activedescendant to the highlighted item after filtering", "retains highlight when clearing the query with autoHighlight enabled", "highlights a candidate arriving after typing", "highlights asynchronous results while an inline input stays focused", "discards the request when the clear button clears the query", "discards the request when closing with keepMounted=%s" (×2), "discards the request when a controlled popup closes".

### 4.3 State transitions summary

| Event | Open | Committed text (`onValueChange` reason) | Inline completion | Highlight |
|---|---|---|---|---|
| Typing a non-blank character | open (`input-change`) | typed (`input-change`) | cleared | cleared, or index 0 with `autoHighlight` |
| Typing that empties the input (outside popup, `openOnInputClick` false) | close (`input-clear`) | `''` (`input-change`) | cleared | cleared |
| ArrowDown/ArrowUp on closed input | open (`list-navigation`) | — | label of the highlighted item (if any) | none, or first/last with `autoHighlight` |
| ArrowDown/ArrowUp while open | — | — | label of the new item; `''` when escaping to the input | moved |
| Hover / pointer leave | — | — | unchanged | moved / cleared (unless `keepHighlight`) |
| Item press / Enter on highlight | close (`item-press`) | item label (`item-press`) when a Popup is mounted | cleared | — (cleared at unmount) |
| Enter, no highlight | close (`none`) | — | — | — |
| Escape while open; outside press; focus-out | close (`escape-key`/`outside-press`/`focus-out`) | — | cleared at unmount | cleared at unmount |
| Escape while closed | — | `''` (`escape-key`) | cleared | — |
| Clear press | — | `''` (`clear-press`) | cleared | cleared |
| Hidden-input autofill | — | autofilled text (`none`) | cleared | — |

## 5. Rendered DOM and ARIA

All elements are the Combobox ones (CMB §5, adopted). Differences:

### 5.1 `Input` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `value` | displayed text (AUT-STA-04) | AUT-DOM-01 | C# |
| `name` | effective name, when the Input owns the form value | AUT-DOM-01 | C# |
| `required` | Root `required` (native attribute) | AUT-DOM-01 | C# |
| `autocomplete` | `off` unless the consumer passes `autoComplete` on the Input | AUT-DOM-01 | C# |
| `aria-autocomplete` | `mode`, or `none` when readOnly | AUT-ARIA-01 | C# |
| other attributes, data attributes | as Combobox | CMB-DOM-01, CMB-DOM-02, CMB-ARIA-01 | C# |

- **AUT-DOM-01** — MUST. `Autocomplete.Input` renders as CMB-DOM-01 with these differences: its `value` is the displayed text (AUT-STA-04); it carries the native `required` attribute whenever the Root is `required`, because the shared Input writes `required` only when the engine's `selectionMode === 'none'` (always the case for Autocomplete; Combobox's single/multiple modes never set it on the visible input) — independently of form-value ownership, so an Input inside the popup is also `required`; it carries `name` = the effective name (Field name ?? Root `name`) when it owns the form value, i.e. when it has no Positioner ancestor (outside the popup, or in an `inline` list without a popup) — an Input inside the popup never has `name`. A consumer `autoComplete` prop on the Input replaces the default `autocomplete="off"` (consumer attributes merge after the engine's).
  Source: `combobox/input/ComboboxInput.tsx:111,171-187` (`:184` `required: selectionMode === 'none' ? required : undefined`); `combobox/root/AriaCombobox.tsx:1357-1364`. Test: [L2] `AutocompleteRoot.test.tsx` "should pass autoComplete to the visible input", "sets `required` on the visible input", "submits the typed input value when name is provided on Autocomplete.Root", "submits the popup input value through native FormData when rendering a field-aware input" (popup Input has no `name`).

### 5.2 Hidden `<input>`

- **AUT-DOM-02** — MUST. The Root's hidden input (CMB-DOM-18 shape: `tabindex="-1"`, `aria-hidden`, `form`, `disabled`, `readonly`, visually hidden, hydration warnings suppressed) differs as follows: `value` = the displayed text as a string (not an item serialization); `required` = Root `required`; no `autocomplete` attribute; `name` = the effective name **only when the visible Input does not own the form value** (Input inside the popup, or no Input mounted, and the Root not `inline`), otherwise no `name` — and when it has no name it gets `id="${rootId}-hidden-input"`. There are never per-value hidden inputs.
  Source: `combobox/root/AriaCombobox.tsx:1564,1581-1590,1612-1704`. Test: [L2] `AutocompleteRoot.test.tsx` "should pass autoComplete to the visible input" (nameless hidden input with an `id`, no `autocomplete`), "submits the inline input value through native FormData"; `store.test.tsx` "gives form value ownership to the hidden input when the input is inside the popup".

### 5.3 Trigger, InputGroup, Clear, Item elements

- **AUT-DOM-03** — MUST. An Item never has `data-selected` (even for the value of the last activated item); its data attributes are `data-highlighted` and `data-disabled` only (CMB-DOM-09 otherwise: `div`, `id="${rootId}-${index}"`, no `tabindex`).
  Source: `combobox/item/ComboboxItem.tsx:72,82,144-148,204-208`; `autocomplete/item/AutocompleteItemDataAttributes.ts:1-10`. Test: [L2] `AutocompleteItem.test.tsx` "does not expose data-selected when reopening after a value is chosen".
- **AUT-DOM-04** — MUST. `Autocomplete.Trigger` and `Autocomplete.InputGroup` never render `data-placeholder`, before or after typing (AUT-STA-01); all other attributes are CMB-DOM-03/CMB-DOM-04.
  Source: `combobox/trigger/ComboboxTrigger.tsx:141`; `combobox/input-group/ComboboxInputGroup.tsx:39`. Test: [L2] `AutocompleteRoot.test.tsx` "does not expose data-placeholder on Trigger or InputGroup".
- **AUT-DOM-05** — MUST. `Autocomplete.Clear` renders (and carries `data-visible=""`) while the displayed text is non-empty (AUT-STA-02); otherwise it is unmounted after its exit transition unless `keepMounted` (CMB-DOM-07). Server: relaxed, see §9.
  Source: `combobox/clear/ComboboxClear.tsx:55-62,71-96`. Test: [L2] `ComboboxClear.test.tsx` "renders and clears the typed value in no-selection mode" (L4 for the exit-transition clause).

### 5.4 ARIA

- **AUT-ARIA-01** — MUST. The control (the Input; it carries the combobox role in both layouts) has `aria-autocomplete` = `mode` (`"list"` by default, `"both"`, `"inline"` or `"none"`), except `"none"` whenever the Root is `readOnly`. The attribute follows the CMB-ARIA-05 rule for non-input elements (only while expanded).
  Source: `autocomplete/root/AutocompleteRoot.tsx:119`; `combobox/root/AriaCombobox.tsx:1366-1373`. Test: [L2] `AutocompleteRoot.test.tsx` "exposes aria-autocomplete=\"none\" when readOnly".
- **AUT-ARIA-02** — MUST. Items have `role="option"` (`"gridcell"` in a Row) and **no** `aria-selected` attribute; the List has no `aria-multiselectable` (CMB-ARIA-08 otherwise, including `aria-readonly="true"` when readOnly outside grid mode).
  Source: `combobox/item/ComboboxItem.tsx:163-166`; `combobox/list/ComboboxList.tsx:78-82`. Test: [L2] `ComboboxList.test.tsx` "sets aria-readonly on the Autocomplete listbox when the root is readOnly"; `AutocompleteItem.test.tsx` "does not expose data-selected when reopening after a value is chosen".

All other roles and ARIA wiring (combobox role on the Input or, with the input inside the popup, on the Trigger; `aria-expanded`, `aria-haspopup`, `aria-controls`, `aria-activedescendant`, Popup `role="dialog"`/`"presentation"`, live regions, Field label/description) are CMB-ARIA-01…03, -05…08, -10, -11, -13…15, -17 and the `Field.Label` clauses of CMB-ARIA-16. There is no `Autocomplete.Label`; label the Input with a native `<label>` or `Field.Label`.

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| Printable character / paste / IME commit | Input | Commits the new text (`input-change`), opens when non-blank (`input-change`), clears the highlight (or index 0 with `autoHighlight`), drops the inline completion | AUT-KEY-08, CMB-KEY-14 |
| <kbd>Backspace</kbd>/<kbd>Delete</kbd> emptying the text | Input outside the popup, `openOnInputClick` false | Commits `''` and closes with `input-clear` | AUT-KEY-08 |
| <kbd>ArrowDown</kbd> / <kbd>ArrowUp</kbd> | Input, closed | Opens (`list-navigation`); no highlight unless `autoHighlight` (then first/last) | AUT-KEY-01 |
| <kbd>ArrowDown</kbd> / <kbd>ArrowUp</kbd> | Input, open | Moves the highlight (CMB-KEY-02/03); modes `both`/`inline`: input shows the highlighted label; escaping to the input restores the typed text | AUT-KEY-02 |
| <kbd>Enter</kbd> | Input or List, open, item highlighted | Activates the item: input filled with its label, popup closes; form submitted only with `submitOnItemClick` | AUT-KEY-04 |
| <kbd>Enter</kbd> | Input, open, no highlight | Closes (`none`); not prevented, so the form submits the text (inline: nothing) | AUT-KEY-05 |
| <kbd>Enter</kbd> | Input, closed | Not intercepted (native submission) | AUT-KEY-05 |
| <kbd>Escape</kbd> | Input, open | Closes (`escape-key`); committed text kept; inline completion dropped at unmount | AUT-KEY-06 |
| <kbd>Escape</kbd> | Input, popup not mounted | Clears the text (`escape-key`); propagation per AUT-KEY-07 | AUT-KEY-07 |
| <kbd>Tab</kbd> / <kbd>Shift+Tab</kbd> | Input | Not intercepted; popup closes (`focus-out`); text kept | AUT-KEY-09 |
| <kbd>Home</kbd> / <kbd>End</kbd> | Input | Caret to start/end (CMB-KEY-06) | CMB-KEY-06 |
| <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> | Input, `grid` | Grid navigation when highlighted, caret otherwise (CMB-KEY-13) | CMB-KEY-13 |
| <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> | Trigger | Opens (`list-navigation`) and focuses the Input (CMB-KEY-11) | CMB-KEY-11 |
| Printable character | Trigger, closed | Nothing (no typeahead) | AUT-KEY-10 |
| <kbd>Enter</kbd>/<kbd>Space</kbd> | Trigger | Button activation: toggles the popup | CMB-KEY-11 |
| any key with Ctrl/Shift/Alt/Meta | Input | Not intercepted by the Input handler | CMB-KEY-14 |
| any | readOnly | Arrows browse; no text change; no inline completion; Enter on a highlight swallowed | AUT-KEY-11 |

- **AUT-KEY-01** — MUST. <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> on a closed Input open the popup with reason `list-navigation`. Without `autoHighlight` no item is highlighted on open (list navigation `focusItemOnOpen: false`), so the *next* arrow press highlights the first (ArrowDown) or last (ArrowUp) item; with `autoHighlight` `true` or `'always'` the opening press highlights the first/last item (`focusItemOnOpen: 'auto'`, CMP). A typed query since opening also suppresses the opening highlight (CMB-KEY-01).
  Source: `combobox/root/AriaCombobox.tsx:1425-1426`. Test: [L4] `AutocompleteRoot.test.tsx` "does not highlight first/last item when pressing ArrowDown/ArrowUp initially", "highlights the first item when opening via ArrowDown", "does not highlight on open via click or when pressing arrow keys initially".
- **AUT-KEY-02** — MUST. In modes `'both'` and `'inline'` (not readOnly), each arrow-key highlight move makes the Input display the label of the newly highlighted item (AUT-STA-05) without calling `onValueChange` and without resetting the list scroll position; moving past a list end back to the input (`loopFocus` with escape, CMB-KEY-03) clears the highlight with reason `keyboard`, restoring the typed text. In mode `'both'` the list stays filtered by the typed text. In modes `'list'` and `'none'` the input text never changes during navigation.
  Source: `autocomplete/root/AutocompleteRoot.tsx:90-105,115-118`; `combobox/root/AriaCombobox.tsx:691-710`. Test: [L4] `AutocompleteRoot.test.tsx` "mode=\"both\": inline overlay + autocomplete handles filtering", "mode=\"inline\": static items with inline overlay", "mode=\"list\" (default): no inline overlay, consumer handles filtering", "mode=\"none\": static items without inline overlay", "mode=\"both\": inline navigation does not reset the scroll to the top".
- **AUT-KEY-03** — MUST. Typing while an inline completion is displayed edits the *displayed* text: the displayed text as edited by the keystroke (for example completion `apple` plus `b` → `appleb`) is committed with reason `input-change` and the completion is dropped. The component does not select the completed suffix, so there is no "accept" key distinct from continued typing, <kbd>Enter</kbd> (item activation) or the text being committed by typing; <kbd>Escape</kbd>, <kbd>Tab</kbd> and outside presses discard the completion (AUT-KEY-06, AUT-KEY-09).
  Source: `combobox/input/ComboboxInput.tsx:239-317`; `autocomplete/root/AutocompleteRoot.tsx:82-88`. Test: [L4] `AutocompleteRoot.test.tsx` "mode=\"inline\": static items with inline overlay" (`apple` + `b` → `appleb`).
- **AUT-KEY-04** — MUST. <kbd>Enter</kbd> with a highlighted item while open (on the Input, or on the List when it has focus) is prevented and stopped and activates the item through a synthetic click (CMB-KEY-04): `Item.onClick` runs, the input text becomes the item's label (`onValueChange(label, 'item-press')`), and the popup closes (`item-press`). Without `submitOnItemClick` the owning form is not submitted; with it the form is submitted after the activation is applied, so the submitted text is the item's label.
  Source: `combobox/input/ComboboxInput.tsx:404-420`; `combobox/list/ComboboxList.tsx:83-99`; `combobox/item/ComboboxItem.tsx:150-161`. Test: [L4] `AutocompleteRoot.test.tsx` "closes popup on Tab after selecting with Enter and typing again", "prevents submit on Enter when an item is highlighted by default (false)", "when true, pressing Enter in the Input submits the owning form when an item is highlighted", "pressing Enter in the List when it has focus submits the owning form", "focusing the listbox should keep the input focused and maintain functionality", "uses itemToStringValue when object lacks label"; `AutocompleteItem.test.tsx` "calls onClick when selected with Enter key (via root interaction)".
- **AUT-KEY-05** — MUST. <kbd>Enter</kbd> on the open Input with no highlighted item closes the popup with reason `none` and is not prevented, so a surrounding form submits the current text (CMB-KEY-04, CMB-FRM-07); in an `inline` Root nothing happens. <kbd>Enter</kbd> on a closed Input is not intercepted.
  Source: `combobox/input/ComboboxInput.tsx:404-416`. Test: [L4] `AutocompleteRoot.test.tsx` "Enter submits when no item is highlighted", "pressing Enter in the Input submits the owning form when no item is highlighted".
- **AUT-KEY-06** — MUST. <kbd>Escape</kbd> on the open Input closes the popup with reason `escape-key` (dismissal, CMB-DIS-02, including the empty-list propagation rule) and leaves the committed text unchanged — `onValueChange` is not called. A displayed inline completion is dropped when the unmount cleanup clears the highlight (AUT-STA-09), restoring the typed text. With the input inside the popup, focus returns to the Trigger, which (through `Autocomplete.Value`) shows the typed text. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:952-961`; `autocomplete/root/AutocompleteRoot.tsx:90-105`. Test: [L4] `AutocompleteRoot.test.tsx` "preserves the typed value when the popup is dismissed with Escape", "automatically unmounts with an actions ref and completes closing once".
- **AUT-KEY-07** — MUST. <kbd>Escape</kbd> on the Input while the popup is not mounted clears the text: `onValueChange('', 'escape-key')`, and resets the engine's internal selected value to `null`. The keydown's propagation is stopped only when the internal selected value was non-null before the press (an item had been activated since the last such reset), the Root is not `inline`, and the consumer did not call `eventDetails.allowPropagation()`; otherwise it propagates (e.g. to close a parent Dialog). Disabled/readOnly inputs ignore the key. Server: relaxed, see §9.
  Source: `combobox/input/ComboboxInput.tsx:323-329,351-367`. Test: none (the Combobox counterpart is CMB-KEY-05).
- **AUT-KEY-08** — MUST. A typed change of the Input (non-autofill `input` event, paste, or composition end) commits the new text with reason `input-change` (AUT-EVT-02). Unless that call was canceled: when the new text is empty, the input is outside the popup and `openOnInputClick` is `false` (the default), the popup closes with reason `input-clear` (no selection is cleared — Autocomplete has none); when the trimmed text is non-blank and the Root is neither readOnly nor disabled, the popup opens with reason `input-change`; without `autoHighlight` the highlight is cleared, with it the first match is highlighted (AUT-STA-12). Programmatic `input` events without an `inputType` and `insertReplacementText` changes still commit the text but do not open the popup.
  Source: `combobox/input/ComboboxInput.tsx:239-317`. Test: [L4] `AutocompleteRoot.test.tsx` "closes popup on Tab after selecting with Enter and typing again", "mode=\"list\" (default): no inline overlay, consumer handles filtering", "highlights the first item when typing and keeps it during filtering".
- **AUT-KEY-09** — MUST. <kbd>Tab</kbd>/<kbd>Shift+Tab</kbd> are not intercepted; when focus leaves the Input/popup tree the popup closes with reason `focus-out` (CMB-DIS-04) and the committed text is kept (Combobox would discard the query); an inline completion is dropped at unmount.
  Source: `combobox/root/AriaCombobox.tsx:952-961`; FLT-FOC-11. Test: [L4] `AutocompleteRoot.test.tsx` "closes popup on Tab after selecting with Enter and typing again".
- **AUT-KEY-10** — MUST. Printable keys on a focused, closed `Autocomplete.Trigger` do nothing: the closed-trigger typeahead of CMB-KEY-12 is enabled only in single-selection mode.
  Source: `combobox/trigger/ComboboxTrigger.tsx:109-122`. Test: none.
- **AUT-KEY-11** — MUST. readOnly (AUT-API-17): ArrowDown/ArrowUp open the popup and move the highlight (reason `keyboard`), the displayed text never changes (no inline completion in any mode), typing does not change the text, and <kbd>Enter</kbd> on a highlighted item is prevented and stopped without activating it (CMB-KEY-15).
  Source: `autocomplete/root/AutocompleteRoot.tsx:51`; `combobox/input/ComboboxInput.tsx:323-329`. Test: [L4] `AutocompleteRoot.test.tsx` "opens the list with the arrow keys but does not commit on item press when readOnly", "does not fill the input with the highlighted item when readOnly".

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| Press on Input | `openOnInputClick` false (default) | Focus only | AUT-PTR-01 |
| Press on Input | `openOnInputClick` true | Opens (`input-press`, 100 ms touch delay outside the popup) | CMB-PTR-01 |
| Press on InputGroup padding | — | Focuses the Input; opens only with `openOnInputClick` | AUT-PTR-01 |
| Press on Trigger | — | Toggles (`trigger-press`); focuses the Input (non-touch) | CMB-PTR-02 |
| Click / drag-release on Item | Popup mounted | Input filled with the item label (`item-press`), popup closes; submits with `submitOnItemClick` | AUT-PTR-02, AUT-PTR-03 |
| Click on Item | `inline` without Popup | No fill; close request only | AUT-PTR-02 |
| Hover over Item | `highlightItemOnHover` | Highlight (`pointer`); inline completion unchanged | AUT-PTR-04 |
| Pointer leaves list | — | Highlight cleared (`pointer`) unless `keepHighlight`; inline completion unchanged | AUT-PTR-05 |
| Click on Clear | not disabled/readOnly | Text `''` (`clear-press`), highlight cleared, Input focused | AUT-PTR-06 |
| Outside press | open, not inline | Closes (`outside-press`); text kept | AUT-DIS-01 |

- **AUT-PTR-01** — MUST. With the default `openOnInputClick={false}`, a mousedown on the Input does not open the popup (the input-press opener is disabled), and a press on the InputGroup outside interactive descendants prevents the default, focuses the Input and does not open; with `openOnInputClick` both open with reason `input-press` (CMB-PTR-01, CMB-PTR-08).
  Source: `combobox/root/AriaCombobox.tsx:1384-1392`; `combobox/utils/handleInputPress.ts:27-37`; `autocomplete/root/AutocompleteRoot.tsx:41`. Test: [L4] `AutocompleteRoot.test.tsx` "does not highlight first/last item when pressing ArrowDown/ArrowUp initially" (a click leaves the popup closed), "mode=\"inline\": static items with inline overlay" (`openOnInputClick` opens on click).
- **AUT-PTR-02** — MUST. Item activation (click, <kbd>Enter</kbd> synthetic click, or drag-release, CMB-PTR-04; not for a disabled item, where the consumer `Item.onClick` is suppressed as well, nor when readOnly, where `Item.onClick` still runs, CMB-EVT-12) runs, after `Item.onClick` and unless the press target is inside an `<a href>` (CMB-PTR-05 link rule): (1) the engine records the item value as its internal selected value; (2) **if an `Autocomplete.Popup` element is mounted**, the input text becomes the label of the item value (`itemToStringValue`/`label`/`value`/`String`) through `onValueChange(label, 'item-press')` with the click (or originating keydown) as `event`; (3) the popup closes with `onOpenChange(false, 'item-press')`. Without a mounted Popup (an `inline` list rendered without one), step (2) is skipped — the text is not filled and `onValueChange` is not called. Calling `cancel()` in that `onValueChange` does not prevent the close (the close is guarded only by the engine's internal selection callback, which Autocomplete does not expose) and, when uncontrolled, does not prevent the text change either (AUT-EVT-02).
  Source: `combobox/root/AriaCombobox.tsx:854-877,879-943`; `combobox/item/ComboboxItem.tsx:150-202`. Test: [L4] `AutocompleteRoot.test.tsx` "filters and displays using label for {label} objects", "filters and displays using value for {value} objects", "keeps the popup mounted until the unmount action completes closing" (item click closes), "submits the popup input value through native FormData when rendering a field-aware input"; `AutocompleteItem.test.tsx` "calls onClick when clicked with a pointer"; `ComboboxRoot.test.tsx` "does not request form submission when no form owns the autocomplete".
- **AUT-PTR-03** — MUST. With `submitOnItemClick`, an item activation applies AUT-PTR-02 synchronously (committed before the next step) and then requests submission of the owning form: the form of the Field's validation input, else the form of the Autocomplete Input element (so another control holding the Field validation handle does not redirect it); nothing happens when no form owns them or the form has no `requestSubmit`. The submitted value is the filled text. An external form linked by `form` is submitted as well.
  Source: `combobox/item/ComboboxItem.tsx:150-161`; `combobox/root/AriaCombobox.tsx:945-950`. Test: [L4] `AutocompleteRoot.test.tsx` "when true, clicking with pointer submits the owning form", "uses the combobox input form when another unscoped control owns the validation ref", "when true, clicking with pointer submits an associated external form when `form` is provided"; `ComboboxRoot.test.tsx` "does not request form submission when no form owns the autocomplete", "does not request form submission when requestSubmit is unavailable".
- **AUT-PTR-04** — MUST. Hover highlighting is CMB-PTR-06 (reason `pointer`, native `MouseEvent` for moves and `PointerEvent` for leaves). Pointer-driven highlight changes never change the inline completion: the input keeps showing the label of the last keyboard/`none` highlight while another item is hovered.
  Source: `autocomplete/root/AutocompleteRoot.tsx:96-98`. Test: [L4] `AutocompleteRoot.test.tsx` "passes native mouse and pointer events for pointer highlights", "mode=\"both\": hovering items should not change the inline overlay (preserve temporary value)".
- **AUT-PTR-05** — MUST. The pointer leaving the list clears the highlight (reason `pointer`) only when `keepHighlight` is `false` (CMP-PTR-04/05 exemptions for touch/keyboard modality apply); with `keepHighlight` the hovered/auto-highlighted item stays highlighted and the next <kbd>ArrowDown</kbd> moves from it. With `autoHighlight: 'always'` and no `keepHighlight` a cleared highlight returns to the first item (AUT-STA-11).
  Source: `combobox/root/AriaCombobox.tsx:1428`. Test: [L4] `AutocompleteRoot.test.tsx` "keeps the current highlight when the pointer leaves the list", "clears the highlight when the pointer leaves the list without keepHighlight", "continues keyboard navigation from the kept highlight after pointer leave".
- **AUT-PTR-06** — MUST. Clear click (not disabled/readOnly; mousedown default prevented): writes `''` with `onValueChange('', 'clear-press')`, clears the highlight (`activeIndex` only; reason from the click, CMB-PTR-07) without touching any selection, and focuses the Input. The popup is not closed. A pending typed-query highlight is discarded (input blank).
  Source: `combobox/clear/ComboboxClear.tsx:98-131`. Test: [L4] `ComboboxClear.test.tsx` "renders and clears the typed value in no-selection mode"; `AutocompleteRoot.test.tsx` "discards the request when the clear button clears the query".

### 6.3 Focus management

Focus management is CMB §6.3 (virtual focus on the Input, untrapped modal focus manager outside the popup, focus trap and Trigger return focus when the input is inside the popup, hidden-input focus redirect, focusing the List/Popup redirects to the Input).

- **AUT-FOC-01** — MUST. Input-inside-popup flow: opening from the Trigger focuses the popup Input (non-touch); keyboard activation of a highlighted item fills the input, closes the popup and returns focus to the Trigger, whose `Autocomplete.Value` shows the filled text; reopening shows the *same* text in the popup Input and the list filtered by it (Combobox starts blank). Dismissing with <kbd>Escape</kbd> likewise keeps the typed text.
  Source: `combobox/popup/ComboboxPopup.tsx:107-123`; `combobox/input/ComboboxInput.tsx:113-118`. Test: [L4] `AutocompleteRoot.test.tsx` "commits a keyboard selection, restores trigger focus, and preserves it on reopen", "preserves the typed value when the popup is dismissed with Escape".

### 6.4 Dismissal

- **AUT-DIS-01** — MUST. Every dismissal of CMB §6.4 (Escape, outside press, focus-out, internal dismiss button, Trigger toggle, `actions.close()`, `cancel-open`) closes with the same reasons as Combobox but never changes the committed text: there is no unmount-time restore or clear. Only the inline completion is dropped (when the unmount cleanup clears the highlight). An Input inside the popup that closes by `focus-out`/`outside-press` marks the field touched and validates the text (AUT-FRM-05). Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:759-852,952-961`. Test: [L4] `AutocompleteRoot.test.tsx` "preserves the typed value when the popup is dismissed with Escape", "closes through the `close` action so `onOpenChange` can opt out"; `ComboboxRoot.test.tsx` "validates the autocomplete input when focus leaves its popup".

### 6.5 Reading direction

Not applicable beyond Combobox — RTL affects only Home/End caret handling and grid navigation (CMB-KEY-06, CMB-KEY-13).

## 7. Events and callbacks

Event-details shapes, `cancel()`, `allowPropagation()` and the reason vocabulary are specified in [event-details-and-reasons](../architecture/event-details-and-reasons.md) (§4.3.7, "Combobox / Autocomplete" rows).

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value: string, details: ChangeEventDetails)` | every engine write of the input text | `input-change`, `item-press`, `clear-press`, `escape-key`, `none` | yes, but see AUT-EVT-02 |
| `onOpenChange` | `(open: boolean, details: OpenChangeEventDetails)` | before every open change | `trigger-press`, `input-press`, `input-change`, `input-clear`, `list-navigation`, `item-press`, `escape-key`, `outside-press`, `focus-out`, `close-press`, `cancel-open`, `imperative-action`, `none` | yes (CMB-EVT-07) |
| `onItemHighlighted` | `(value \| undefined, details: HighlightEventDetails)` | every highlight change | `keyboard`, `pointer`, `none` | no |
| `onOpenChangeComplete` | `(open: boolean)` | after open/close animations | — | — |
| `Item.onClick` | native click handler | pointer activation, Enter on highlight | — | `preventBaseUIHandler()` (CMB-API-60) |

- **AUT-EVT-01** — MUST. `onValueChange` reasons: `input-change` — typing, paste, composition end (AUT-KEY-08); `item-press` — the fill after an item activation (AUT-PTR-02; `event` = the click or the originating keydown); `clear-press` — `Autocomplete.Clear` (AUT-PTR-06); `escape-key` — <kbd>Escape</kbd> on a closed popup (AUT-KEY-07); `none` — browser autofill of the hidden input (AUT-FRM-06). `input-clear` is declared but never emitted for the text in Autocomplete (its only writers are single/multiple-mode paths). Inline-completion changes of the displayed text never call it.
  Source: `combobox/input/ComboboxInput.tsx:230-237,289-290,357-359`; `combobox/root/AriaCombobox.tsx:866-875,1650-1652`; `combobox/clear/ComboboxClear.tsx:108-111`. Test: [L2] `AutocompleteRoot.test.tsx` "preserves the typed value when the popup is dismissed with Escape" (last call `'al'`), "should handle browser autofill", "ignores hidden-input autofill when readOnly", "ignores hidden-input autofill when disabled".
- **AUT-EVT-02** — MUST. The value handler runs, in order: (1) discard the inline completion; (2) when uncontrolled, store the new committed text; (3) call `onValueChange(value, details)`. The engine then checks `details.isCanceled`: when canceled it skips that write's follow-ups — typed-query highlight request, list scroll reset, auto-highlight, and the Input's open/close step (AUT-KEY-08) — but, because step (2) already ran, an *uncontrolled* Autocomplete still shows the new text. A controlled consumer rejects a change by not updating `value` (EVD-EVT-06). Port: see ADR 05 Amendments item 2 (uncontrolled text-edit exception).
  Source: `autocomplete/root/AutocompleteRoot.tsx:82-88`; `combobox/root/AriaCombobox.tsx:655-661`; `combobox/input/ComboboxInput.tsx:289-294`. Test: none (§16 Q2).
- **AUT-EVT-03** — MUST. `onOpenChange` emits the Combobox reason matrix (CMB-EVT-02) with these Autocomplete-specific sources: `input-press` only with `openOnInputClick`; `input-clear` whenever typing empties an outside-popup input while `openOnInputClick` is `false` (the default) and for an emptied IME composition under the same conditions; `item-press` on every item activation (one call, after the `onValueChange` fill); `none` for <kbd>Enter</kbd> without a highlight. `clear-press` and `chip-remove-press` are declared only. `preventUnmountOnClose()` and `actions.unmount()`/`close()` behave as CMB-ANI-03/CMB-API-36.
  Source: `combobox/input/ComboboxInput.tsx:248-258,270-275,296-306,404-416`; `combobox/root/AriaCombobox.tsx:879-943,1017-1024`. Test: [L4] `AutocompleteRoot.test.tsx` "manual unmount lifecycle" (12 tests, §13).
- **AUT-EVT-04** — MUST. `onItemHighlighted` follows CMB-EVT-05 (reason `keyboard` for arrow navigation and keyboard-driven clears, `pointer` with the native `MouseEvent`/`PointerEvent` for hover and pointer leave, `none` for typing, auto-highlight, list changes, open/close; `index` `-1` and value `undefined` when cleared). After the consumer callback returns, a non-`pointer` notification updates the inline completion (AUT-STA-05). An auto-highlight on typing reports the first item with reason `none`.
  Source: `autocomplete/root/AutocompleteRoot.tsx:90-105`; `combobox/root/AriaCombobox.tsx:606-653`. Test: [L4] `AutocompleteRoot.test.tsx` "passes native mouse and pointer events for pointer highlights", "calls onItemHighlighted when the popup auto highlights on open".
- **AUT-EVT-05** — MUST. Event order for an item activation: `Item.onClick` (consumer) → `onValueChange(label, 'item-press')` (only with a mounted Popup) → `onOpenChange(false, 'item-press')` → (with `submitOnItemClick`, after the state is committed) the form's `submit` event → after the exit animation `onOpenChangeComplete(false)` and a clearing `onItemHighlighted(undefined, { reason: 'none' })`.
  Source: `combobox/item/ComboboxItem.tsx:150-161,185-191`; `combobox/root/AriaCombobox.tsx:854-943,952-961`. Test: [L4] `AutocompleteItem.test.tsx` "calls onClick when clicked with a pointer"; `AutocompleteRoot.test.tsx` "when true, clicking with pointer submits the owning form".

## 8. Form integration

The field/form mechanics are [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md); its §6 control matrix has an Autocomplete row. Autocomplete-specific facts:

- **AUT-FRM-01** — MUST. The Root registers as the Field control with: control element = the Input when it is outside the popup and the Root is not `inline`, else the Trigger (the engine treats an `inline` input as inside the popup, CMB-STR-03); raw value = stringified value = the engine's input text, i.e. the displayed text including an active inline completion (AUT-STA-04); enabled iff not disabled; name = the Root `name` (the Field's name takes precedence). `validate(value)` receives that text. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:551-560,582-591`. Test: [L2] `AutocompleteRoot.test.tsx` "submits the input value directly (not selection value)", "prop: validationMode=onSubmit"; `ComboboxTrigger.test.tsx` "validates the $name value when blurred" (`no selection` variant).
- **AUT-FRM-02** — MUST. *Form-value ownership.* The visible Input owns the form value (carries `name`, AUT-DOM-01; the hidden input is nameless, AUT-DOM-02) when it is rendered outside the popup or the Root is `inline`; otherwise (Input inside the popup, or no Input) the hidden input carries `name` with the text as its value. The store flag is recomputed post-commit pre-paint as `inline || !inputInsidePopup`, in the same store transaction that publishes `inline`, so no observer sees `inline` without the matching ownership; its initial value is `true`, so the first (server) render names only the visible input when present and no control at all in the input-inside-popup layout. Exactly one control carries the name at any time after hydration, and the submitted entry is `name=<text>`.
  Source: `combobox/root/AriaCombobox.tsx:519-522,1556-1567,1589-1590`; `combobox/input/ComboboxInput.tsx:111,120-124,186`. Test: [L2] `AutocompleteRoot.test.tsx` "submits the typed input value when wrapped in Field.Root", "submits the typed input value when name is provided on Autocomplete.Root", "submits the popup input value through native FormData when rendering a field-aware input", "submits the inline input value through native FormData", "server-renders only the inline input name for native FormData", "server-renders only the outside-popup input name for native FormData", "submits a default popup input value through native FormData before the popup opens", "server-renders the default popup input value without a form name before hydration", "submits to an external form when `form` is provided"; `store.test.tsx` "publishes an inline transition in a single store transaction", "settles inputOwnsFormValue through the useStore path when inline is set", "gives form value ownership to the hidden input when the input is inside the popup", "keeps form value ownership across hydration" (L4 for the native-FormData submission tests).
- **AUT-FRM-03** — MUST. On every change of the engine's input text: the Form's external errors for the field name are cleared, `dirty` = text differs from the Field's initial value, and `validation.change(text)` runs (per `validationMode`); `filled` = `String(text) !== ''`, recomputed post-commit pre-paint (so a non-empty `defaultValue` is filled from mount). Inline-completion changes count as text changes. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:1244-1247,1315-1324,1331`. Test: [L2] `AutocompleteRoot.test.tsx` "clears external errors on change", "[data-dirty]", "adds [data-filled] attribute when input has content", "adds [data-filled] attribute when already filled with defaultValue", "prop: validationMode=onChange".
- **AUT-FRM-04** — MUST. `required` (default `false`) sets the native `required` attribute on the visible Input and on the hidden input, and `aria-required` on the control (CMB-API-28); submitting with an empty text triggers native `valueMissing` validation surfaced by `Field.Error match="valueMissing"`.
  Source: `combobox/input/ComboboxInput.tsx:180,184`; `combobox/root/AriaCombobox.tsx:1696`. Test: [L2] `AutocompleteRoot.test.tsx` "sets `required` on the visible input", "triggers native validation when required and empty", "[data-valid]" (L4 for the native `valueMissing` validation clause).
- **AUT-FRM-05** — MUST. `touched`/`focused` and `onBlur` validation follow CMB-FOC-08/CMB-FRM-05 with the text as the validated value: Input blur marks touched, un-focused and (with `validationMode="onBlur"`) commits validation of the text; a Trigger blur not into the popup does the same; closing by `focus-out`/`outside-press` with the input inside the popup does the same; focus moving into the popup does not.
  Source: `combobox/input/ComboboxInput.tsx:188-222`; `combobox/trigger/ComboboxTrigger.tsx:178-191`; `combobox/root/AriaCombobox.tsx:838-850`. Test: [L2] `AutocompleteRoot.test.tsx` "[data-touched]", "[data-focused]", "prop: validate", "prop: validationMode=onBlur", "[data-valid]"; `ComboboxRoot.test.tsx` "validates the autocomplete input when focus leaves its popup"; `ComboboxTrigger.test.tsx` "validates the $name value when blurred" (`no selection`).
- **AUT-FRM-06** — MUST. Browser autofill: a `change` event on the hidden input (not default-prevented, Root not disabled/readOnly) writes the hidden input's new value as the text after a microtask, through `onValueChange(text, 'none')`; there is no item matching and no list force-mount, and the popup does not open. A visible-input autofill (`insertReplacementText` or no `inputType`) commits the text without opening (AUT-KEY-08).
  Source: `combobox/root/AriaCombobox.tsx:1627-1688`. Test: [L2] `AutocompleteRoot.test.tsx` "should handle browser autofill", "ignores hidden-input autofill when readOnly", "ignores hidden-input autofill when disabled".
- **AUT-FRM-07** — MUST. Field integration otherwise follows CMB-FRM-06: `Field.Root` `disabled`/`name` flow into the Root; `Field.Label` labels the Input outside the popup (`aria-labelledby`) or the Trigger inside; `Field.Description` is referenced by the control's `aria-describedby`; `Field.Root invalid` yields `data-invalid`; a field-aware element substituted for the Input still submits through the owning control.
  Source: `combobox/input/ComboboxInput.tsx:62,127-129,181`. Test: [L2] `AutocompleteRoot.test.tsx` "Field.Label", "Field.Description", "[data-invalid]", "submits the popup input value through native FormData when rendering a field-aware input".

Enter-key submission is AUT-KEY-04/05; `submitOnItemClick` is AUT-PTR-03.

## 9. Lifecycle, mounting and animation

Timing vocabulary per conventions §4. The Popup mounting, transition status, `keepMounted`, `onOpenChangeComplete`, `preventUnmountOnClose()`/`actions.unmount()` and Clear/exit animations are CMB-ANI-01, -03, -04, -06 and CMB-STA-29 (adopted). Differences:

- **AUT-ANI-01** — MUST. No filtered result is frozen for the exit animation (CMB-ANI-02 does not apply): during the closing phase the list renders the items for the current text, which does not change on close. The unmount cleanup (AUT-STA-09) runs once when the closing phase completes — immediately without exit animations, after them otherwise, or when `actions.unmount()` ends a held close — and is the moment a displayed inline completion is dropped. Server: relaxed, see §9.
  Source: `combobox/root/AriaCombobox.tsx:800-836,952-1015`. Test: [L4] `AutocompleteRoot.test.tsx` "manual unmount lifecycle" (12 tests: "automatically unmounts with an actions ref and completes closing once", "keeps the popup mounted until the unmount action completes closing", "clears the opt-out when a controlled reopen interrupts a pending unmount", "ignores an opt-out on a canceled close", "keeps the opt-out when a controlled close is applied in a transition", "closes through the `close` action so `onOpenChange` can opt out", "ignores `unmount` while the popup is open", "unmounts when `close` and `unmount` are called in one batch", "still unmounts on a later close after `unmount` was called while open", "still unmounts on a later close after `unmount` and a reopen in one batch", "does not call `onOpenChange` when the `close` action is called while closed", "completes closing once when `unmount` is called twice in one batch").

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- AUT-ANI-01 — via TRN-ANI-03, TRN-ANI-09, TRN-ANI-10 (adopted through CMB-ANI-01/03): the unmount cleanup and `onOpenChangeComplete(false)` land in the first render batch after the `transitions` module's completion message, about two round trips after the exit animation ends; JS sets `hidden` at animation end (optimistic convergent write).
- AUT-DOM-05 — via TRN-ANI-03, TRN-ANI-09: the Clear's exit unmount lands about two round trips after its animations finish; JS hides it at animation end.
- AUT-KEY-06, AUT-DIS-01 (Escape clause) — EVD-EVT-11, EVD-DIS-01, FLT-KEY-01: the consumer's `cancel()`/`allowPropagation()` arrives a round trip after dispatch; JS prevents and stops Escape whenever this popup is the innermost open one (ADR 04 §3 fallback). WebAssembly meets the spec through `decide()`.
- AUT-KEY-07 — EVD-EVT-11: the consumer's `allowPropagation()` on the closed-input Escape arrives a round trip after dispatch; JS applies the ADR 04 §3 fallback policy. WebAssembly meets the spec through `decide()`.
- AUT-STA-02 — RND-STA-01 (mirrored fields): the Clear's `transitionStatus` (and so `ClassValue`/`StyleValue`) updates one round trip after the JS-written transition attributes.
- AUT-FRM-01 — FFI registration: field-control registration runs after paint plus a round trip; observable only as latency of dependent attribute updates.
- AUT-FRM-03 — FFI registration: `filled` is recomputed after paint plus a round trip; observable only as latency of `data-filled`.

## 10. Positioning

No Autocomplete-specific requirements. `Autocomplete.Positioner` is `Combobox.Positioner`; CMB-API-50 and CMB-POS-01…04 are adopted unchanged (anchor: InputGroup ?? Input when the input is outside the popup; the Trigger when the input is inside the popup, even if an InputGroup is rendered; `side: 'bottom'`, `align: 'center'`, `collisionPadding: 5`, dropdown collision avoidance, CSS variables `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height`, `--transform-origin`).

## 11. Cross-cutting dependencies

| Mechanism | Doc | Autocomplete-specific configuration |
|---|---|---|
| Combobox engine (`AriaCombobox`) | [combobox](combobox.md) | `selectionMode: 'none'`, `fillInputOnItemPress: true`, `autoComplete: mode`, `itemToStringLabel: itemToStringValue`, `inputValue` always defined, `filter` per mode, `filterQuery` in mode `both`, `openOnInputClick` default `false` (AUT-STR-03); defaults relied on: `isItemEqualToValue: Object.is`, no `itemToStringValue`, no `formAutoComplete`, `limit: -1`, `loopFocus: true`, `highlightItemOnHover: true`, `modal: false` |
| Collator filter | [shared-utilities](../architecture/shared-utilities.md) | `getFilter({ locale })` built by the Root for the default `contains` (UTL-I18N-08…13); exported as `Autocomplete.useFilter` |
| List navigation | [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) | as CMB-KEY-01 with `focusItemOnOpen: false` unless `autoHighlight`, `resetOnPointerLeave: !keepHighlight`, `allowEscape: loopFocus && !autoHighlight` |
| Event details and reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | §7; EVD §4.3.7 Combobox/Autocomplete rows; EVD-EVT-24 |
| Field/Form | [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md) | raw value = text; visible input owns `name` outside the popup / inline (FFI §6 Autocomplete row) |
| Button behavior | [button-behavior](../architecture/button-behavior.md) | as Combobox (Trigger, Clear native `true`; Item native `false`, composite) |
| Floating, popup store, transitions | [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md) | unchanged from Combobox (CMB §11) |
| Rendering | [rendering-and-composition](../architecture/rendering-and-composition.md) | every part; no `describeConformance` runs for Autocomplete parts in `autocomplete/` (the Combobox parts' suites apply) |

## 12. Edge cases, invariants and dev warnings

Autocomplete's own code emits no warnings and throws no errors. Inherited diagnostics: the Combobox context and nesting errors (AUT-STR-04), the non-collection `items` object error (CMB-EDGE-01, verbatim there), and the controlled/uncontrolled diagnostics of the shared controlled-state utility for `open` (UTL-EDGE-22/23, `name: 'Combobox'`, `state: 'open'`).

- **AUT-EDGE-01** — SHOULD. No controlled/uncontrolled diagnostic is emitted for the text: the Root re-decides controlled mode on every parameter update (`value !== undefined`) and always hands the engine a defined text, so switching between controlled and uncontrolled `value`, or changing `defaultValue` later, produces no warning (the internal text keeps its last uncontrolled value). Port: relaxed (ADR 05 Amendments item 1). The `open` prop keeps the shared diagnostics with the name `'Combobox'`.
  Source: `autocomplete/root/AutocompleteRoot.tsx:56-57,120`; `combobox/root/AriaCombobox.tsx:325-337`. Test: none.
- **AUT-EDGE-02** — MUST. A controlled `value={null}` is an empty text (`''`), the input renders empty and the full list is available; `undefined` means uncontrolled.
  Source: `autocomplete/root/AutocompleteRoot.tsx:70-72,78`. Test: [L2] `AutocompleteRoot.test.tsx` "treats a controlled null value as an empty query".
- **AUT-EDGE-03** — MUST. Server rendering: the Input outside the popup carries the combobox ARIA and the `name` in the server HTML and remains the only named control after hydration; an inline Input likewise; in the input-inside-popup layout the server HTML contains no named control (the popup is not rendered and the hidden input is nameless until hydration), and after hydration the hidden input carries the name and the default text, so a submit before the popup ever opens sends it.
  Source: `combobox/root/AriaCombobox.tsx:519-522,1556-1567`. Test: [L2] `AutocompleteRoot.test.tsx` "server-renders only the inline input name for native FormData", "server-renders only the outside-popup input name for native FormData", "server-renders the default popup input value without a form name before hydration", "submits a default popup input value through native FormData before the popup opens"; `store.test.tsx` "keeps form value ownership across hydration" (L4 for the native-FormData submission tests).
- **AUT-EDGE-04** — MUST. Asynchronous items: a typed query with `autoHighlight` whose results arrive later highlights the first arriving item (reason `none`) while the popup is open (or an inline input is focused, or always with `'always'`), and <kbd>Enter</kbd> then activates it; the request is dropped when the popup closes, the input is cleared, or an inline input blurs (AUT-STA-11/12). `items` changing to an empty array clears the highlight after paint.
  Source: `combobox/root/AriaCombobox.tsx:1095-1123,1254-1260`. Test: [L2] `AutocompleteRoot.test.tsx` "asynchronous items" (7 tests, §13) (L4 for the Enter-activation clause).
- **AUT-EDGE-05** — MUST. Disabled: as CMB-API-30 (no opening, no key handling, hidden input disabled so nothing is submitted), and hidden-input autofill is ignored.
  Source: `combobox/root/AriaCombobox.tsx:1629`. Test: [L2] `AutocompleteRoot.test.tsx` "ignores hidden-input autofill when disabled".
- **AUT-EDGE-06** — MUST. Scroll reset: a typed change scrolls the list's nearest scrollable wrapper back to the top (CMB-PTR-12); inline-completion navigation does not (it is not a typed change), so arrow navigation can scroll the list far down in mode `'both'`.
  Source: `combobox/root/AriaCombobox.tsx:691-710`. Test: [L4] `AutocompleteRoot.test.tsx` "resets the list scroll position to the top when typing", "mode=\"both\": inline navigation does not reset the scroll to the top".

## 13. Behaviors evidenced by tests

No shared conformance suite (`describeConformance`, popup, popup-list, positioner) is run on `Autocomplete.*` parts; the Combobox parts they alias are covered by the Combobox suites (CMB §13). Tests marked "browser only" are `it.skipIf(isJSDOM)`; "jsdom only" are `it.skipIf(!isJSDOM)`. `AutocompleteRoot.test.tsx` has 96 test titles (101 cases with parameterization).

### `root/AutocompleteRoot.test.tsx`

| Test | Requirements |
|---|---|
| "exposes the orientation attribute rendered by Separator" | AUT-STR-05, CMB-DOM-16 |
| manual unmount lifecycle: "automatically unmounts with an actions ref and completes closing once" | AUT-ANI-01, AUT-STA-09, AUT-KEY-06, CMB-ANI-03, CMB-EVT-06, CMB-API-36 |
| "keeps the popup mounted until the unmount action completes closing" | AUT-ANI-01, AUT-PTR-02, CMB-ANI-03, CMB-STA-29 |
| "clears the opt-out when a controlled reopen interrupts a pending unmount" / "keeps the opt-out when a controlled close is applied in a transition" / "ignores `unmount` while the popup is open" / "unmounts when `close` and `unmount` are called in one batch" / "still unmounts on a later close after `unmount` was called while open" / "still unmounts on a later close after `unmount` and a reopen in one batch" / "completes closing once when `unmount` is called twice in one batch" | AUT-ANI-01, CMB-ANI-03, CMB-STA-29, CMB-API-36, CMB-EVT-06 |
| "ignores an opt-out on a canceled close" | AUT-ANI-01, CMB-STA-29, CMB-EVT-07 |
| "closes through the `close` action so `onOpenChange` can opt out" | AUT-DIS-01, AUT-EVT-03, CMB-API-36, CMB-DIS-08 |
| "does not call `onOpenChange` when the `close` action is called while closed" | AUT-EVT-03, CMB-API-07 |
| keyboard interactions: "closes popup on Tab after selecting with Enter and typing again" | AUT-KEY-04, AUT-KEY-08, AUT-KEY-09, AUT-STA-12 |
| input inside popup composition: "commits a keyboard selection, restores trigger focus, and preserves it on reopen" | AUT-FOC-01, AUT-STA-09, AUT-KEY-04, AUT-API-18 |
| "preserves the typed value when the popup is dismissed with Escape" | AUT-FOC-01, AUT-KEY-06, AUT-DIS-01, AUT-EVT-01, AUT-STA-09 |
| "should handle browser autofill" | AUT-FRM-06, AUT-EVT-01 |
| "ignores hidden-input autofill when readOnly" | AUT-API-17, AUT-FRM-06 |
| "opens the list with the arrow keys but does not commit on item press when readOnly" | AUT-API-17, AUT-KEY-11, CMB-ARIA-08 |
| "does not fill the input with the highlighted item when readOnly" | AUT-API-17, AUT-KEY-11 |
| "drops a pending inline completion when readOnly is turned on" | AUT-API-17, AUT-STA-06 |
| "exposes aria-autocomplete=\"none\" when readOnly" | AUT-ARIA-01, AUT-API-17 |
| "ignores hidden-input autofill when disabled" | AUT-FRM-06, AUT-EDGE-05 |
| "should pass autoComplete to the visible input" | AUT-DOM-01, AUT-DOM-02, AUT-API-14, AUT-STR-03 |
| "does not expose data-placeholder on Trigger or InputGroup" | AUT-STA-01, AUT-DOM-04, AUT-STA-08 |
| prop: onItemHighlighted: "passes native mouse and pointer events for pointer highlights" | AUT-API-15, AUT-PTR-04, AUT-EVT-04, CMB-EVT-05 |
| prop: autoHighlight: "calls onItemHighlighted when the popup auto highlights on open" | AUT-API-05, AUT-STA-12, AUT-EVT-04 |
| "highlights the first item when typing and keeps it during filtering" | AUT-STA-12, AUT-KEY-08, CMB-ARIA-03 |
| "does not highlight on open via click or when pressing arrow keys initially" | AUT-KEY-01, AUT-API-05 |
| "highlights the first item when opening via ArrowDown" | AUT-KEY-01, AUT-API-05 |
| "links aria-activedescendant to the highlighted item after filtering" | AUT-STA-12, CMB-ARIA-03 |
| "does not highlight first/last item when pressing ArrowDown/ArrowUp initially" | AUT-KEY-01, AUT-API-07, AUT-PTR-01 |
| "retains highlight when clearing the query with autoHighlight enabled" | AUT-STA-12 |
| "highlights the first item immediately when behavior is \"always\"" | AUT-STA-11, AUT-API-05 |
| "keeps the latest pointer highlight on outside blur when behavior is \"always\"" | AUT-STA-11, AUT-API-06, AUT-API-16 |
| asynchronous items: "highlights a candidate arriving after typing" | AUT-STA-12, AUT-EDGE-04, AUT-KEY-04 |
| "highlights asynchronous results while an inline input stays focused" | AUT-STA-12, AUT-EDGE-04, AUT-API-16 |
| "handles asynchronous results after an inline input blurs with autoHighlight=%s" (×2: `true`, `'always'`) | AUT-STA-11, AUT-STA-12, AUT-EDGE-04 |
| "discards the request when the clear button clears the query" | AUT-STA-12, AUT-PTR-06 |
| "discards the request when closing with keepMounted=%s" (×2) | AUT-STA-12, AUT-STA-09, AUT-EDGE-04 |
| "discards the request when a controlled popup closes" | AUT-STA-12, AUT-EDGE-04 |
| prop: keepHighlight: "keeps the current highlight when the pointer leaves the list" / "clears the highlight when the pointer leaves the list without keepHighlight" / "continues keyboard navigation from the kept highlight after pointer leave" | AUT-API-06, AUT-PTR-05 |
| prop: mode: "mode=\"%s\": uses the locale when applying the default filter" (×2: `list`, `both`) | AUT-API-10, AUT-STA-07, AUT-STR-03 |
| "mode=\"list\" (default): no inline overlay, consumer handles filtering" | AUT-API-04, AUT-KEY-02, AUT-STA-05 |
| "mode=\"both\": inline overlay + autocomplete handles filtering" | AUT-API-04, AUT-KEY-02, AUT-STA-04, AUT-STA-05 |
| "mode=\"both\": hovering items should not change the inline overlay (preserve temporary value)" | AUT-PTR-04, AUT-STA-05 |
| "mode=\"both\": external controlled updates replace the temporary inline value" | AUT-STA-06, AUT-API-01 |
| "mode=\"inline\": static items with inline overlay" | AUT-API-04, AUT-KEY-02, AUT-KEY-03, AUT-STA-04, AUT-PTR-01 |
| "mode=\"none\": static items without inline overlay" | AUT-API-04, AUT-KEY-02, AUT-STA-07 |
| scroll reset: "resets the list scroll position to the top when typing" (browser only) | AUT-EDGE-06, CMB-PTR-12 |
| "mode=\"both\": inline navigation does not reset the scroll to the top" (browser only) | AUT-EDGE-06, AUT-KEY-02 |
| prop: filter: "mode=\"both\": keeps filtering against the typed query during inline completion" | AUT-STA-07, AUT-API-04 |
| "mode=\"inline\": does not call a custom filter" | AUT-API-09, AUT-STA-07 |
| "mode=\"%s\": uses a custom filter instead of the locale-aware default" (×2) | AUT-API-09 |
| "mode=\"%s\": does not apply default filtering when filter is null" (×2) | AUT-API-09, AUT-API-08, AUT-STA-07 |
| prop: value: "treats a controlled null value as an empty query" | AUT-API-01, AUT-EDGE-02, AUT-STA-04 |
| prop: submitOnItemClick: "prevents submit on Enter when an item is highlighted by default (false)" | AUT-API-12, AUT-KEY-04 |
| "when true, clicking with pointer submits the owning form" | AUT-API-12, AUT-PTR-03, AUT-EVT-05 |
| "uses the combobox input form when another unscoped control owns the validation ref" | AUT-PTR-03 |
| "when true, pressing Enter in the Input submits the owning form when an item is highlighted" | AUT-KEY-04, AUT-PTR-03 |
| "when true, clicking with pointer submits an associated external form when `form` is provided" (browser only) | AUT-PTR-03, AUT-API-13 |
| "focusing the listbox should keep the input focused and maintain functionality" | AUT-KEY-04, CMB-FOC-04 |
| Form: "submits the typed input value when wrapped in Field.Root" | AUT-FRM-01, AUT-FRM-02 |
| "submits the typed input value when name is provided on Autocomplete.Root" | AUT-FRM-02, AUT-DOM-01, AUT-API-13 |
| "submits the popup input value through native FormData when rendering a field-aware input" | AUT-FRM-02, AUT-FRM-07, AUT-DOM-01, AUT-PTR-02 |
| "submits the inline input value through native FormData" | AUT-FRM-02, AUT-DOM-02, AUT-API-16 |
| "server-renders only the inline input name for native FormData" / "server-renders only the outside-popup input name for native FormData" | AUT-FRM-02, AUT-EDGE-03 |
| "submits a default popup input value through native FormData before the popup opens" | AUT-FRM-02, AUT-EDGE-03, AUT-API-02 |
| "server-renders the default popup input value without a form name before hydration" | AUT-EDGE-03 |
| "submits to an external form when `form` is provided" (browser only) | AUT-API-13, AUT-FRM-02 |
| "triggers native validation when required and empty" | AUT-FRM-04 |
| "clears external errors on change" | AUT-FRM-03 |
| "submits the input value directly (not selection value)" | AUT-FRM-01 |
| "Enter submits when no item is highlighted" | AUT-KEY-05, AUT-API-07 |
| "pressing Enter in the Input submits the owning form when no item is highlighted" | AUT-KEY-05 |
| "pressing Enter in the List when it has focus submits the owning form" | AUT-KEY-04, AUT-PTR-03, CMB-KEY-04 |
| object item stringification: "filters and displays using label for {label} objects" / "filters and displays using value for {value} objects" | AUT-API-08, AUT-PTR-02 |
| "uses itemToStringValue when object lacks label" | AUT-API-08, AUT-KEY-04 |
| Field: "sets `required` on the visible input" | AUT-DOM-01, AUT-FRM-04 |
| "[data-touched]" / "[data-focused]" / "prop: validate" / "prop: validationMode=onBlur" (jsdom only) | AUT-FRM-05 |
| "[data-dirty]" / "prop: validationMode=onChange" (jsdom only) | AUT-FRM-03 |
| "adds [data-filled] attribute when input has content" | AUT-FRM-03 |
| "adds [data-filled] attribute when already filled with defaultValue" | AUT-FRM-03, AUT-API-02 |
| "[data-invalid]" / "Field.Label" / "Field.Description" | AUT-FRM-07 |
| "[data-valid]" | AUT-FRM-04, AUT-FRM-05 |
| "prop: validationMode=onSubmit" | AUT-FRM-01 |

### `item/AutocompleteItem.test.tsx`

| Test | Requirements |
|---|---|
| prop: onClick: "calls onClick when clicked with a pointer" | CMB-API-60, AUT-PTR-02, AUT-EVT-05 |
| "calls onClick when selected with Enter key (via root interaction)" | CMB-API-60, AUT-KEY-04 |
| "does not expose data-selected when reopening after a value is chosen" | AUT-STA-03, AUT-DOM-03, AUT-STA-08, AUT-ARIA-02 |

### `value/AutocompleteValue.test.tsx`

| Test | Requirements |
|---|---|
| prop: children: "renders current input value via function child" | AUT-API-18, AUT-API-02 |
| "renders function child with empty string when no value typed" / "overrides the display when children is a static ReactNode" / "renders complex ReactNode children" | AUT-API-18 |

### `root/AutocompleteRoot.spec.tsx` (type-level)

Compile-time assertions (no runtime titles): `itemToStringValue` receives the leaf item type for flat and grouped `items` (not the group); `value`/`defaultValue`/`onValueChange` are string-typed and a controlled item object is rejected; `onValueChange`'s details have no `isItemPress`; a `Combobox.createItems()` collection is rejected for `items`; `onItemHighlighted` narrows `details.event` by reason (`MouseEvent | PointerEvent` for `pointer`, `KeyboardEvent` for `keyboard`, `Event` otherwise). Requirements: AUT-API-01…03, AUT-API-08, AUT-API-11, AUT-API-15, AUT-API-21.

### Tests in other files that exercise this component

| File / test | Requirements |
|---|---|
| `combobox/root/ComboboxRoot.test.tsx` "restores the first highlight after an always-highlighted query becomes empty" | AUT-STA-11 |
| `combobox/root/ComboboxRoot.test.tsx` "does not request form submission when no form owns the autocomplete" / "does not request form submission when requestSubmit is unavailable" | AUT-PTR-03, AUT-PTR-02 |
| `combobox/root/ComboboxRoot.test.tsx` "validates the autocomplete input when focus leaves its popup" | AUT-FRM-05, AUT-DIS-01 |
| `combobox/store.test.tsx` "publishes an inline transition in a single store transaction" / "settles inputOwnsFormValue through the useStore path when inline is set" | AUT-FRM-02 |
| `combobox/store.test.tsx` "exposes real commands to a descendant ref callback on the first commit" (`Autocomplete.Trigger` ArrowDown during the first commit) | CMB-STA-34, CMB-KEY-11 |
| `combobox/store.test.tsx` "gives form value ownership to the hidden input when the input is inside the popup" | AUT-FRM-02, AUT-DOM-02 |
| `combobox/store.test.tsx` "keeps form value ownership across hydration" | AUT-FRM-02, AUT-EDGE-03 |
| `combobox/clear/ComboboxClear.test.tsx` "renders and clears the typed value in no-selection mode" | AUT-STA-02, AUT-DOM-05, AUT-PTR-06 |
| `combobox/list/ComboboxList.test.tsx` "sets aria-readonly on the Autocomplete listbox when the root is readOnly" | AUT-ARIA-02, CMB-ARIA-08 |
| `combobox/trigger/ComboboxTrigger.test.tsx` "validates the $name value when blurred" (`no selection` variant of ×2) | AUT-FRM-01, AUT-FRM-05 |
| `utils/listbox-separator/ListboxSeparator.test.tsx` "%s" › "exposes the listbox separator behavior" (`Autocomplete.Separator` variant of ×3) | AUT-STR-05 |

## 14. React-specific notes and porting considerations

Non-normative.

- **Two layers of ownership.** The wrapper Root mirrors the uncontrolled text in its own React state and *always* passes a defined `inputValue` to the engine, so the engine runs permanently in its "controlled input" branch (`hasInputValue = true`). A port should model this directly: Autocomplete owns the committed text and an overlay, the engine only reads a text and reports write attempts. Because the wrapper writes its internal state before calling `onValueChange`, cancellation cannot block uncontrolled edits (AUT-EVT-02); a port that wants cancelable edits must reorder these steps and document the divergence. Port: see decisions/05-state-store-and-event-details.md §2 (request sequence `OnValueChange` → stop if canceled → commit when uncontrolled, so the port's cancel does block uncontrolled edits; controlled iff `Value` was supplied at the first parameter set, unlike AUT-API-01's per-render check — see §16 Q2).
- **Inline-completion reset timing.** The reset in AUT-STA-06(b)/(c) is a passive effect (after paint), so for one paint after a controlled `value` change the old completion is still displayed; the reset in (a) is synchronous inside the event handler. Ports should apply (b)/(c) in the same update if possible; no test depends on the intermediate frame. Port: see decisions/01-hosting-model-and-timing.md §Decision (timing-term mapping, item 4).
- **Type-only aliases.** `Autocomplete.Trigger`, `InputGroup`, `Item` and `Separator` are the same runtime components as Combobox's; only the TypeScript `Props`/`State` types differ (they hide `placeholder`, `selected`, `id`). A port in a nominally typed language can expose the same component with narrowed state types or wrap it; runtime state objects still carry `placeholder: false`/`selected: false` (§16 Q8).
- **`useFilter` is not a hook.** `Autocomplete.useFilter` is the module-level cached `getFilter` function (no React hook calls); it can be called anywhere and returns a shared object per locale/options key.
- **Engine-only prop.** `filterQuery` is an internal engine prop (not in the public engine props type) used only by Autocomplete mode `'both'`; it overrides both the live query and the close query.
- **Form-ownership transaction.** `inputOwnsFormValue` has two writers in one commit (the Input's ref callback and the Root's layout effect); the store tests pin that observers only see the Root's value. A port should compute it in one place (`mode none && (inline || input not inside a positioner)`) and publish it atomically with `inline`. Port: see decisions/05-state-store-and-event-details.md §1 (one `Update` per transition).
- **Server rendering.** The engine's initial store assumes the visible input owns the form value (`inputOwnsFormValue: true`) so the server HTML never names two controls; ports with SSR must keep that initial value. Port: see decisions/01-hosting-model-and-timing.md §Consequences (prerender in scope for markup only).
- **Inherited engine notes.** Every Combobox porting note (CMB §14) applies unchanged: list navigation and virtual highlight, dismissal/focus-out and anchor positioning run in JavaScript, and the store, controlled values and highlight details are C#. Port: see decisions/04-js-interop-boundary.md §1 (`composite`, `interactions`, `positioning` modules); decisions/05-state-store-and-event-details.md §3 (`HighlightEventDetails.Index` for `onItemHighlighted`).

## 15. Requirement index

Counts: STR 5, API 21, STA 12, DOM 5, ARIA 2, KEY 11, PTR 6, FOC 1, DIS 1, EVT 5, FRM 7, ANI 1, EDGE 6 — 83 requirements.

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| AUT-STR-01 | MUST | Namespace members: own Root/Value; aliased Trigger/InputGroup/Item/Separator; re-exported Combobox parts; no Label/Chips/ItemIndicator/createItems | 2 |
| AUT-STR-02 | MUST | Adopts CMB requirements with substitutions, except Not adopted / Overridden | 2 |
| AUT-STR-03 | MUST | Root runs the engine with selectionMode none, fillInputOnItemPress, autoComplete=mode, defined input text, per-mode filter | 2 |
| AUT-STR-04 | MUST | Parts outside Root throw the Combobox-named errors; Value renders '' instead | 2 |
| AUT-STR-05 | MUST | Separator is the listbox separator (role presentation, data-orientation) | 2 |
| AUT-API-01 | MUST | value controls the input text; null → ''; controlled-ness re-evaluated on every parameter update | 3.1 |
| AUT-API-02 | MUST | defaultValue seeds the text once (default '') | 3.1 |
| AUT-API-03 | MUST | onValueChange fires for every engine text write, not for inline completion | 3.1 |
| AUT-API-04 | MUST | mode list/both/inline/none: filtering × inline completion matrix | 3.1 |
| AUT-API-05 | MUST | autoHighlight false/true/'always'; truthy disables list-end escape | 3.1 |
| AUT-API-06 | MUST | keepHighlight keeps the highlight on pointer leave | 3.1 |
| AUT-API-07 | MUST | openOnInputClick defaults to false | 3.1 |
| AUT-API-08 | MUST | itemToStringValue is the display label (fill, inline, filter), not a form serializer | 3.1 |
| AUT-API-09 | MUST | filter: custom / null / default collator contains; ignored in inline/none | 3.1 |
| AUT-API-10 | MUST | locale configures the default filter collator | 3.1 |
| AUT-API-11 | MUST | items/filteredItems/limit as Combobox; no createItems collections | 3.1 |
| AUT-API-12 | MUST | submitOnItemClick submits after item activation | 3.1 |
| AUT-API-13 | MUST | name/form apply to the control owning the form value | 3.1 |
| AUT-API-14 | MUST | Unexposed engine options fixed (Object.is, no hidden autocomplete attr) | 3.1 |
| AUT-API-15 | MUST | onItemHighlighted as Combobox; consumer first, then inline update | 3.1 |
| AUT-API-16 | MUST | inline as Combobox; no fill without Popup; visible input owns form value | 3.1 |
| AUT-API-17 | MUST | readOnly as Combobox plus no inline completion and aria-autocomplete none | 3.1 |
| AUT-API-18 | MUST | Value renders the input text (function child gets String(text)) | 3.2 |
| AUT-API-19 | MUST | useFilter is the plain cached collator filter | 3.10 |
| AUT-API-20 | MUST | useFilteredItems = Combobox.useFilteredItems | 3.10 |
| AUT-API-21 | MUST | Exported types and attribute enums; ChangeEventDetails without isItemPress | 3.12 |
| AUT-STA-01 | MUST | Trigger/InputGroup state placeholder always false | 3.3 |
| AUT-STA-02 | MUST | Clear visible iff the input text is non-empty | 3.6 |
| AUT-STA-03 | MUST | Item selected always false | 3.7 |
| AUT-STA-04 | MUST | Displayed text = inline completion ?? controlled value ?? internal text | 4.1 |
| AUT-STA-05 | MUST | Non-pointer highlight changes set the inline completion (label or '') | 4.1 |
| AUT-STA-06 | MUST | Inline completion reset rules (writes, controlled changes, disabling) | 4.1 |
| AUT-STA-07 | MUST | Filtering per mode; mode both filters by the committed text | 4.1 |
| AUT-STA-08 | MUST | No exposed selection; internal last-activated value and its two effects | 4.1 |
| AUT-STA-09 | MUST | Text persists: no popup-input blanking, no close-time rewrite; cleanup clears indices | 4.1 |
| AUT-STA-10 | MUST | No frozen close query | 4.1 |
| AUT-STA-11 | MUST | autoHighlight 'always' rules | 4.2 |
| AUT-STA-12 | MUST | autoHighlight true follows CMB-STA-26 with no selection to restore | 4.2 |
| AUT-DOM-01 | MUST | Input: displayed value, native required, name when owner, consumer autocomplete | 5.1 |
| AUT-DOM-02 | MUST | Hidden input: text value, named only when the Input does not own the value | 5.2 |
| AUT-DOM-03 | MUST | Item never data-selected | 5.3 |
| AUT-DOM-04 | MUST | Trigger/InputGroup never data-placeholder | 5.3 |
| AUT-DOM-05 | MUST | Clear rendered / data-visible while the text is non-empty | 5.3 |
| AUT-ARIA-01 | MUST | aria-autocomplete = mode, or none when readOnly | 5.4 |
| AUT-ARIA-02 | MUST | Items have no aria-selected; no aria-multiselectable | 5.4 |
| AUT-KEY-01 | MUST | Opening arrow highlights only with autoHighlight | 6.1 |
| AUT-KEY-02 | MUST | Arrow navigation shows the highlighted label in modes both/inline | 6.1 |
| AUT-KEY-03 | MUST | Typing over a completion commits the edited displayed text; no suffix selection | 6.1 |
| AUT-KEY-04 | MUST | Enter on a highlight fills, closes, submits only with submitOnItemClick | 6.1 |
| AUT-KEY-05 | MUST | Enter without highlight closes (none) and lets the form submit | 6.1 |
| AUT-KEY-06 | MUST | Escape while open closes, text kept, completion dropped at unmount | 6.1 |
| AUT-KEY-07 | MUST | Escape while closed clears the text; propagation depends on internal value | 6.1 |
| AUT-KEY-08 | MUST | Typing commits (input-change), opens, emptying closes (input-clear) | 6.1 |
| AUT-KEY-09 | MUST | Tab closes (focus-out) and keeps the text | 6.1 |
| AUT-KEY-10 | MUST | No closed-trigger typeahead | 6.1 |
| AUT-KEY-11 | MUST | readOnly keyboard behavior | 6.1 |
| AUT-PTR-01 | MUST | Input/InputGroup press does not open by default | 6.2 |
| AUT-PTR-02 | MUST | Item activation: record, fill (only with a Popup), close | 6.2 |
| AUT-PTR-03 | MUST | submitOnItemClick: synchronous apply then requestSubmit on the owning form | 6.2 |
| AUT-PTR-04 | MUST | Pointer highlights never change the inline completion | 6.2 |
| AUT-PTR-05 | MUST | Pointer leave clears the highlight unless keepHighlight | 6.2 |
| AUT-PTR-06 | MUST | Clear: '' (clear-press), highlight cleared, input focused | 6.2 |
| AUT-FOC-01 | MUST | Input-inside-popup: focus back to Trigger, text preserved on reopen | 6.3 |
| AUT-DIS-01 | MUST | Dismissals never change the committed text | 6.4 |
| AUT-EVT-01 | MUST | onValueChange reasons: input-change, item-press, clear-press, escape-key, none | 7 |
| AUT-EVT-02 | MUST | Value handler order; uncontrolled cancel does not block the text change | 7 |
| AUT-EVT-03 | MUST | onOpenChange reason sources specific to Autocomplete | 7 |
| AUT-EVT-04 | MUST | onItemHighlighted as Combobox, then inline update | 7 |
| AUT-EVT-05 | MUST | Event order for an item activation | 7 |
| AUT-FRM-01 | MUST | Field registration: raw value = input text | 8 |
| AUT-FRM-02 | MUST | Form-value ownership: visible input outside popup/inline, else hidden input | 8 |
| AUT-FRM-03 | MUST | dirty/validation/filled from the input text | 8 |
| AUT-FRM-04 | MUST | required: native required on visible and hidden input | 8 |
| AUT-FRM-05 | MUST | touched/focused/onBlur validation with the text | 8 |
| AUT-FRM-06 | MUST | Hidden-input autofill writes the text (none) after a microtask | 8 |
| AUT-FRM-07 | MUST | Field.Label/Description/invalid/disabled integration as Combobox | 8 |
| AUT-ANI-01 | MUST | No frozen result during exit; cleanup drops the inline completion | 9 |
| AUT-EDGE-01 | SHOULD | No controlled/uncontrolled diagnostic for the text | 12 |
| AUT-EDGE-02 | MUST | Controlled null is '' | 12 |
| AUT-EDGE-03 | MUST | Server rendering of form-value ownership | 12 |
| AUT-EDGE-04 | MUST | Asynchronous items and autoHighlight | 12 |
| AUT-EDGE-05 | MUST | Disabled also ignores hidden-input autofill | 12 |
| AUT-EDGE-06 | MUST | Scroll reset only on typed changes, not inline navigation | 12 |

## 16. Open questions

1. **No selection of the completed suffix.** For `aria-autocomplete="inline"`/`"both"` the APG pattern selects the completed portion so the next keystroke replaces it. Base UI only swaps the displayed text (AUT-STA-05) and never calls `setSelectionRange` for it (the only caret code is Home/End, `combobox/input/ComboboxInput.tsx:335-349`), so typing after a completion appends to the full label (test "mode=\"inline\": static items with inline overlay": `apple` + `b` → `appleb`), and there is no accept key other than item activation. Ports must decide whether to replicate this.
2. **`onValueChange` cancellation is ineffective when uncontrolled.** `handleValueChange` stores the new text before calling the consumer (`autocomplete/root/AutocompleteRoot.tsx:82-88`), so `cancel()` only suppresses the engine's follow-ups (opening, highlight, scroll reset). Untested; possibly unintended. Resolved for the port by decisions/05-state-store-and-event-details.md §2: the request sequence calls `OnValueChange` before the uncontrolled commit and stops when canceled, so the port's `cancel()` blocks uncontrolled edits (a deliberate divergence from AUT-EVT-02's Base UI ordering); the same section fixes controlled-ness at the first parameter set, unlike the per-render check of AUT-API-01/AUT-EDGE-01. Decided (2026-10-18): kept as written; the port adds the uncontrolled-text-edit exception to the request sequence (ADR 05 Amendments item 2).
3. **Hidden selection memory.** The engine still records the last activated item as `selectedValue` (`combobox/root/AriaCombobox.tsx:854-865`). Effects: (a) <kbd>Escape</kbd> on a closed popup stops propagation only after an item has been activated (`combobox/input/ComboboxInput.tsx:352-366`), so the same keypress may or may not close a parent Dialog; (b) in lists without `items`, Items re-register that value's index as `selectedIndex` (`combobox/item/ComboboxItem.tsx:111-135`), so with `autoHighlight` an opening arrow key highlights the previously activated item instead of the first/last. Both untested; likely unintended.
4. **`autoHighlight` + inline completion.** With mode `'both'`/`'inline'` and `autoHighlight`, the auto-highlight after the first typed character is reported with reason `none`, which AUT-STA-05 turns into an inline completion — the input immediately shows the full first match and later keystrokes append to it (consequence of Q1). Untested.
5. **Inline completion is part of the value.** The engine sees the displayed text (`autocomplete/root/AutocompleteRoot.tsx:120`), so a visible completion marks the field dirty, runs `onChange` validation, shows the Clear button, renders in `Autocomplete.Value` and is what a form submit sends — yet it is dropped (without `onValueChange`) on Escape, Tab or outside press at unmount. Untested; confirm intent.
6. **`createItems()` collections.** Rejected by the Autocomplete types (`AutocompleteRoot.spec.tsx`), but `items` is forwarded verbatim (`autocomplete/root/AutocompleteRoot.tsx:109`), so a collection passed at runtime would be handled by the engine; behavior (labels, default filter receiving source items) is unspecified here.
7. **Inline list without `Autocomplete.Popup`.** The fill on item activation is gated on a mounted Popup element (`combobox/root/AriaCombobox.tsx:866-868`), so a command-palette style inline list (docs demo `command-palette`) never writes the text or calls `onValueChange` on item press. Probably intended for command lists, but undocumented.
8. **Public types hide runtime state fields.** `Autocomplete.Trigger.State`/`InputGroup.State` omit `placeholder` and `Autocomplete.Item.State` omits `selected`, but the runtime state objects passed to `className`/`style`/`render` contain `placeholder: false` / `selected: false` (`combobox/trigger/ComboboxTrigger.tsx:141`, `combobox/input-group/ComboboxInputGroup.tsx:39`, `combobox/item/ComboboxItem.tsx:144-148`). Likewise `Autocomplete.Item.Props` omits `id` while the runtime forwards it.
9. **Error messages name Combobox.** All context/nesting errors thrown by Autocomplete parts say "Combobox parts must be placed within <Combobox.Root>" (`combobox/root/ComboboxRootContext.tsx:30-58`), and the controlled-state diagnostics for `open` use `name: 'Combobox'`. A port may prefer Autocomplete-specific wording.
10. **Outline / docs vs. source.** (a) The outline lists `data-required` for `Autocomplete.Input`/`Trigger` (see CMB §16 Q1: the field state has no `required`). (b) The outline omits `data-empty` on `List` (as for Combobox) and intercepted native props (`id`, `required`, `name` handling on `Input`). (c) `types.md` types `value`/`defaultValue` as `string | number | string[]` (the native input value type) while `onValueChange` reports `string`; `filter`'s description reads "AutocompleteFilter function used to match…" (generator renamed `Filter`), and `required`/`readOnly` descriptions ("must choose a value", "unable to choose a different option") describe Combobox semantics. (d) Everything else in the outline matches the source.
11. **Misleading test title.** "submits the typed input value when name is provided on Autocomplete.Root" sets `name` on `Field.Root`, not on `Autocomplete.Root`; the Root `name` path is exercised only by "should pass autoComplete to the visible input" and the browser-only external-form test.
12. **`AutocompleteSeparatorDataAttributes`** re-exports the standalone Separator's attribute module (`autocomplete/index.ts:90` → `separator/SeparatorDataAttributes.ts`), while the component is the listbox separator; the only member (`orientation` = `data-orientation`) coincides, so this is cosmetic.
13. **`inputOwnsFormValue` double writer.** `Combobox.Input` writes `selectionMode === 'none' && !hasPositionerParent` from its ref callback (`combobox/input/ComboboxInput.tsx:111,120-124`) while the Root writes `selectionMode === 'none' && (inline || !inputInsidePopup)` (`combobox/root/AriaCombobox.tsx:1564`); they disagree for an `inline` Root whose Input is inside a Positioner. The Root's value wins within the same commit (store tests); AUT-FRM-02 follows the Root.

### Base-doc discrepancies — to be fixed in `combobox.md`

- **CMB-API-65 / §3.27 table.** States that `Combobox.useFilter` returns `{ contains, startsWith, endsWith, collator }`; the returned object has no `collator` (`combobox/root/utils/useFilter.ts:33-45`, `internals/filter.ts:76-83`). It also states "The same two matchers are what the Root uses as its default `filter`" — false: the Root's default filter is `createCollatorItemFilter` in every mode (`combobox/root/AriaCombobox.tsx:301-311`, with the comment that the single-selection bypass makes a selection-aware variant unnecessary). Resolved: CMB-API-65 now states the three-matcher shape with no `collator` and that the Root's default `filter` is the plain matcher in every mode.
- **CMB-API-14.** "using the single-selection variant in single mode (CMB-API-65)" — the Root never uses the single-selection variant (same source); the "show everything while the input mirrors the selection" behavior comes only from the bypass (CMB-STA-22). Resolved: CMB-API-14 now says the single-selection variant is never used by the Root.
- **CMB §16 Q12** lists four Autocomplete-mode tests in the combobox folder; there are more: `ComboboxRoot.test.tsx` "does not request form submission when no form owns the autocomplete" and "does not request form submission when requestSubmit is unavailable"; all five tests of `combobox/store.test.tsx` (Autocomplete fixtures); `ComboboxTrigger.test.tsx` "validates the $name value when blurred" (`no selection` variant); plus `utils/listbox-separator/ListboxSeparator.test.tsx` (`Autocomplete.Separator` variant). Resolved: CMB §16 Q12 now lists all twelve tests plus the shared-utility variant.
- **CMB-EDGE-06 / CMB-STA-34 test citations.** CMB-EDGE-06 cites `store.test.tsx` "keeps form value ownership across hydration" for "the hidden input is the form-value owner both before and after hydration", but that test renders `Autocomplete.Root` and asserts that the *visible* input owns the name. CMB-STA-34 ("`inputOwnsFormValue` always `false` for Combobox") cites "publishes an inline transition…" and "settles inputOwnsFormValue…", which are Autocomplete fixtures asserting `true`. The statements may hold for Combobox, but no Combobox test backs them; the citations should move to this document (AUT-FRM-02, AUT-EDGE-03). Resolved: CMB-EDGE-06 now records no Combobox test for hidden-input ownership across hydration, and CMB-STA-34 cites the store tests as engine mechanics only; both tests are cited here under AUT-FRM-02/AUT-EDGE-03.
