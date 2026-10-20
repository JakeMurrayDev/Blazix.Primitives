# Select

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/select/` |
| Tests | `packages/react/src/select/**/*.test.tsx` (20 files; plus 2 type-level `*.spec.tsx`) |
| Docs page | `docs/src/app/(docs)/react/components/select/page.mdx` |
| Prefix | `SEL` |
| Status | approved |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [button-behavior](../architecture/button-behavior.md), [field-and-form-infrastructure](../architecture/field-and-form-infrastructure.md), [composite-and-list-navigation](../architecture/composite-and-list-navigation.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Select is the library's replacement for a native `<select>`: a button (`Select.Trigger`, WAI-ARIA `combobox` role) that shows the current value (`Select.Value`) and opens a floating list of options (`Select.Popup` / `Select.List`, `listbox` role) from which the user picks one value, or several in `multiple` mode. The user can pick with the pointer (including pressing the trigger, dragging over the list and releasing on an option), with the keyboard (arrow keys, Home/End, typeahead, Enter/Space) or, while the popup is closed, by typing the first letters of an option, which selects it without opening the popup. Values may be any type, not only strings: the component compares them with a configurable equality function and serializes them for form submission with a configurable stringifier.

Two things distinguish Select from the other list popups (Menu, Combobox). First, its default positioning mode, `alignItemWithTrigger`, lays the popup *over* the trigger so that the text of the selected option sits exactly where the trigger's value text is, and grows the popup in place as the user scrolls the list; it falls back to ordinary anchored positioning when there is not enough room, when the popup was opened by touch, or when the browser is pinch-zoomed in Safari. Second, it is a form control: it renders a visually hidden native `<input>` (one per value in `multiple` mode) that carries the serialized value, participates in native constraint validation (`required`), receives browser autofill, and reports touched/dirty/filled/focused/validity to an enclosing `Field.Root`.

The popup subtree is not rendered at all until the trigger is first focused (or the hidden input is autofilled); from then on it stays in the DOM, hidden while closed, so that the option labels are available for closed-trigger typeahead. No component builds on Select, and Select builds on no other component; it shares its item-indicator and separator implementations with Combobox (see RND).

## 2. Anatomy

```
<Select.Root>                   required, 1        renders no element of its own
                                                   (+ hidden <input> sibling after children,
                                                    + one <input type="hidden"> per value in multiple mode)
  <Select.Label>                optional, 0..n     default element: div
  <Select.Trigger>              required, 1        default element: button
    <Select.Value>              optional, 0..n     default element: span
    <Select.Icon>               optional, 0..n     default element: span
  <Select.Portal>               optional, 0..1     default element: div (portal container)
    <Select.Backdrop>           optional, 0..1     default element: div
    <Select.Positioner>         required*, 1       default element: div  (+ internal backdrop sibling when modal)
      <Select.Popup>            optional, 0..1     default element: div
        <Select.ScrollUpArrow>  optional, 0..1     default element: div
        <Select.Arrow>          optional, 0..1     default element: div
        <Select.List>           optional, 0..1     default element: div
          <Select.Item>         0..n               default element: div
            <Select.ItemText>   optional, 0..1     default element: div
            <Select.ItemIndicator> optional, 0..1  default element: span
          <Select.Separator>    0..n               default element: div
          <Select.Group>        0..n               default element: div
            <Select.GroupLabel> optional, 0..1     default element: div
            <Select.Item> …
        <Select.ScrollDownArrow> optional, 0..1    default element: div
```

`*` Positioner is required for anything that must register items (Popup, List, Item, Arrow, scroll arrows). `Select.Label` is listed in `index.parts.ts` (as the second part) but the orchestrator's hint list omitted it; it exists in v1.9.0 and is covered below.

- **SEL-STR-01** — MUST. `Select.Root` MUST NOT render an element of its own. It MUST render, in document order: its `children`, then one visually hidden native `<input>` (the *form-participation input*, §5.17), then — only when `multiple` is `true`, the value is an array and an effective `name` exists — one `<input type="hidden">` per selected value (§5.18). The inputs are siblings of the children, never descendants of any part.
  Source: `select/root/SelectRoot.tsx:511-586`. Test: [L2] `SelectRoot.test.tsx` "should serialize multiple values correctly for form submission", "should serialize empty array as empty string in multiple mode".
- **SEL-STR-02** — MUST. `Select.Trigger`, `Select.Value`, `Select.Icon`, `Select.Label`, `Select.Portal`, `Select.Backdrop`, `Select.Positioner`, `Select.Popup`, `Select.List`, `Select.Item`, `Select.ItemText`, `Select.Arrow`, `Select.ScrollUpArrow` and `Select.ScrollDownArrow` MUST be rendered inside the subtree of a `Select.Root`; rendering any of them elsewhere MUST fail with the error of SEL-EDGE-01. (`Select.Group`, `Select.GroupLabel`, `Select.Separator` and `Select.ItemIndicator` do not read the root's shared state and do not fail outside a Root.)
  Source: `select/root/SelectRootContext.ts:11-19`, each part's `useSelectRootContext()` call (e.g. `select/trigger/SelectTrigger.tsx:65`, `select/item/SelectItem.tsx:53`). Test: [L2] `SelectTrigger.test.tsx` "throws a descriptive error when rendered outside <Select.Root>".
- **SEL-STR-03** — MUST. `Select.Popup`, `Select.List`, `Select.Arrow`, `Select.ScrollUpArrow` and `Select.ScrollDownArrow` MUST be rendered inside the subtree of a `Select.Positioner`; otherwise they MUST fail with the error of SEL-EDGE-02.
  Source: `select/positioner/SelectPositionerContext.ts:17-25`, `select/popup/SelectPopup.tsx:58-64`, `select/list/SelectList.tsx:25`, `select/arrow/SelectArrow.tsx:23-24`, `select/scroll-arrow/SelectScrollArrow.tsx:32`. Test: [L2] `SelectPopup.test.tsx` "throws a descriptive error when rendered outside <Select.Positioner>".
- **SEL-STR-04** — MUST. `Select.ItemText` and `Select.ItemIndicator` MUST be rendered inside the subtree of a `Select.Item`; otherwise they MUST fail with the error of SEL-EDGE-03.
  Source: `select/item/SelectItemContext.ts:13-21`, `select/item-text/SelectItemText.tsx:19`, `select/item-indicator/SelectItemIndicator.tsx:18`. Test: [L2] `SelectItemText.test.tsx` "throws a descriptive error when rendered outside <Select.Item>".
- **SEL-STR-05** — MUST. `Select.GroupLabel` MUST be rendered inside the subtree of a `Select.Group`; otherwise it MUST fail with the error of SEL-EDGE-04.
  Source: `select/group/SelectGroupContext.ts:11-19`, `select/group-label/SelectGroupLabel.tsx:21`. Test: [L2] `SelectGroupLabel.test.tsx` "throws a descriptive error when rendered outside <Select.Group>".
- **SEL-STR-06** — MUST. `Select.Portal` MUST render nothing while the popup is neither *mounted* (§9) nor *force-mounted* (SEL-STA-09); once either condition has held, the portal and its subtree stay rendered for the lifetime of the Root (the positioner is hidden while closed, SEL-DOM-18). Consequently the option elements do not exist before the first trigger focus/open, and a programmatic value change alone MUST NOT mount the popup.
  Source: `select/portal/SelectPortal.tsx:18-27`, `select/trigger/SelectTrigger.tsx:153-158`, `select/root/SelectRoot.tsx:566`. Test: [L2] `SelectRoot.test.tsx` "does not force-mount the popup on a programmatic value change", "commits typeahead on a closed trigger when items are provided"; conformance suite option `alwaysMounted: 'only-after-open'`.
- **SEL-STR-07** — MUST. `Select.Positioner` MUST provide the *item registry* (CMP) for its subtree — every `Select.Item` registers with the nearest enclosing Positioner's registry, at any depth — and MUST render, in order: the internal backdrop (PSH-DOM-14, only while mounted and `modal` is `true`; `inert` while not open; `cutout` = the trigger element) as the immediately preceding sibling of the positioner element, then the positioner element.
  Source: `select/positioner/SelectPositioner.tsx:221-232`. Test: [L2] `SelectRoot.test.tsx` "should render an internal backdrop when `true`", "should not render an internal backdrop when `false`".
- **SEL-STR-08** — MUST. `Select.Popup` and `Select.List` are both optional. When a `Select.List` is rendered, it is the `listbox` and the scroll container and the Popup becomes a presentational wrapper (SEL-DOM-24); when no List is rendered, the Popup itself is the `listbox` and the scroll container (SEL-DOM-23). `Select.Item` MAY be rendered as a descendant of the Positioner without any Popup or List (then no focus management of §6.3 and no popup keyboard handling exists, but selection by click still works).
  Source: `select/popup/SelectPopup.tsx:435-445`, `select/list/SelectList.tsx:31-35`. Test: [L2] `SelectPopup.test.tsx` "has aria attributes when no Select.List is present", "places aria attributes on Select.List instead if it is present"; `SelectItem.test.tsx` "should select the item and close popup when clicked" (Item directly under Positioner).
- **SEL-STR-09** — MUST. `Select.Arrow` MUST render nothing while item-aligned positioning is active (SEL-POS-03); otherwise it renders its element.
  Source: `select/arrow/SelectArrow.tsx:42-46`. Test: none.
- **SEL-STR-10** — MUST. `Select.ScrollUpArrow` / `Select.ScrollDownArrow` MUST render their element iff their transition *mounted* flag is `true` (TRN, driven by SEL-STA-29 visibility) or `keepMounted` is `true`.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:46,145-150`. Test: [L4] `SelectRoot.test.tsx` "keeps a scroll arrow mounted while its exit animation runs".
- **SEL-STR-11** — MUST. `Select.ItemIndicator` MUST render its element iff its item is selected or `keepMounted` is `true` (RND-ANI-01); without `keepMounted` it is removed in the same update that deselects the item, so no exit transition is observable (confirms TRN §10 Q7).
  Source: `select/item-indicator/SelectItemIndicator.tsx:20-25`. Test: [L2] `SelectItemIndicator.test.tsx` "mounts only while selected when keepMounted is false", "preserves the kept-mounted element across selection changes".
- **SEL-STR-12** — MUST. `Select.Icon`, `Select.Backdrop`, `Select.ItemText`, `Select.Group`, `Select.GroupLabel` and `Select.Separator` are structural: they render an element with the attributes of §5 and forward consumer attributes/handlers, and attach no interaction of their own.
  Source: `select/icon/SelectIcon.tsx:27-32`, `select/backdrop/SelectBackdrop.tsx:39-54`, `select/item-text/SelectItemText.tsx:40-43`, `select/group/SelectGroup.tsx:29-38`, `select/group-label/SelectGroupLabel.tsx:32-35`, `select/separator/SelectSeparator.tsx:27-29`. Test: [L2] conformance suites (§13).
- **SEL-STR-13** — MUST. Exactly one `Select.Trigger` is supported per Root: the Root stores a single trigger element (the last one to attach wins) and all anchoring, `aria-controls`, focus return and label association target it.
  Source: `select/store.ts:51`, `select/trigger/SelectTrigger.tsx:98,229`. Test: none.

If `Select.Value` is omitted the trigger shows only its own children; the aligned positioning then aligns the popup to the trigger's inline edge instead of to a value text (SEL-POS-07 step 6). If `Select.Label` is omitted and no `Field.Label` exists, the trigger carries no `aria-labelledby` (SEL-ARIA-05).

## 3. Public API

### 3.1 `Select.Root`

**Default element:** none (renders no element; see SEL-STR-01). **Accepts `render` element substitution:** no. **Accepts `className`/`style`:** no (not in its parameter type).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `Value \| null` (single) / `readonly Value[] \| null` (multiple) | — (uncontrolled) | Consumer-owned selected value. | SEL-API-01 |
| `defaultValue` | same as `value` | `null`; `[]` in multiple mode | Initial component-owned value. | SEL-API-02 |
| `onValueChange` | `(value, eventDetails: Select.Root.ChangeEventDetails) => void` | — | Called when the user (or autofill / item removal) requests a new value. | SEL-API-03 |
| `open` | `boolean` | — (uncontrolled) | Consumer-owned open state. | SEL-API-04 |
| `defaultOpen` | `boolean` | `false` | Initial component-owned open state. | SEL-API-04 |
| `onOpenChange` | `(open: boolean, eventDetails: Select.Root.OpenChangeEventDetails) => void` | — | Called when the popup requests to open or close. | SEL-API-05 |
| `onOpenChangeComplete` | `(open: boolean) => void` | — | Called after the open/close animations complete. | SEL-API-06 |
| `name` | `string` | — | Form field name; overridden by `Field.Root.name`. | SEL-API-07 |
| `form` | `string` | — | `id` of the owning `<form>` when rendered outside it. | SEL-API-08 |
| `autoComplete` | `string` | — | Browser autofill hint placed on the hidden input. | SEL-API-09 |
| `id` | `string` | generated | `id` of the trigger (and basis of derived ids). | SEL-API-10 |
| `required` | `boolean` | `false` | A value must be chosen for native validation to pass. | SEL-API-11 |
| `readOnly` | `boolean` | `false` | Value locked; popup still opens and can be browsed. | SEL-API-12 |
| `disabled` | `boolean` | `false` | Ignore user interaction; combined (OR) with `Field.Root.disabled`. | SEL-API-13 |
| `multiple` | `boolean` | `false` | Array value; items toggle; popup stays open on selection. | SEL-API-14 |
| `highlightItemOnHover` | `boolean` | `true` | Pointer movement over items highlights them. | SEL-API-15 |
| `modal` | `boolean` | `true` | Internal backdrop blocks outside pointer interaction and page scroll is locked. | SEL-API-16 |
| `actionsRef` | DOM-handle-like object receiving `{ unmount(), close() }` | — | Imperative actions. | SEL-API-17 |
| `inputRef` | DOM handle for `HTMLInputElement` | — | Receives the hidden form-participation input. | SEL-API-18 |
| `items` | `Record<string, ReactNode> \| ReadonlyArray<{ label: ReactNode; value: any }> \| ReadonlyArray<Group<any>>` | — | Label lookup used by `Select.Value`. | SEL-API-19 |
| `itemToStringLabel` | `(itemValue: Value) => string` | — | Converts an object value to display text. | SEL-API-20 |
| `itemToStringValue` | `(itemValue: Value) => string` | — | Converts an object value to its form string. | SEL-API-21 |
| `isItemEqualToValue` | `(itemValue: Value, value: Value) => boolean` | `Object.is` | Equality between an item's value and the selected value. | SEL-API-22 |
| `children` | `ReactNode` | — | The parts. | SEL-API-23 |

**Consumed (not forwarded):** every prop (there is no element to forward to).

- **SEL-API-01** — MUST. `value` / `defaultValue` follow the controlled/uncontrolled contract of EVD-STA-01…06 and UTL-STA-01…06 with diagnostics name `'Select'`, state `'value'`: when `value` is not `undefined` on the first render the value is consumer-owned for the life of the component and MUST NOT be changed internally; a controlled prop change MUST NOT invoke `onValueChange`.
  Source: `select/root/SelectRoot.tsx:102-107`. Test: [L2] `SelectRoot.test.tsx` "should select the item specified by the value prop", "should update the selected item when the value prop changes", "should not update the internal value if the controlled value prop does not change".
- **SEL-API-02** — MUST. In single mode the initial component-owned value is `defaultValue`, defaulting to `null`. In `multiple` mode it is `defaultValue ?? []`, where the empty array is a shared frozen instance (a consumer's `children` function on `Select.Value` MUST treat it as read-only).
  Source: `select/root/SelectRoot.tsx:60,102-107`, `packages/utils/src/empty.ts`. Test: [L2] `SelectRoot.test.tsx` "should select the item by default", "should handle defaultValue as array in multiple mode"; `SelectValue.test.tsx` "defaults to empty array when no value is provided".
- **SEL-API-03** — MUST. `onValueChange` MUST receive the requested value as its first argument (the item value in single mode; a new array in multiple mode) and a change-event-details object (§3.20) as its second. Full firing rules are in §7.
  Source: `select/root/SelectRoot.tsx:330-340,731-736`. Test: [L2] `SelectRoot.test.tsx` "should call onValueChange when an item is selected".
- **SEL-API-04** — MUST. `open` / `defaultOpen` follow the same controlled/uncontrolled contract (diagnostics name `'Select'`, state `'open'`); `defaultOpen` defaults to `false`. A controlled `open` that the consumer does not update after a close request leaves the popup open.
  Source: `select/root/SelectRoot.tsx:63,109-114`. Test: [L2] `SelectRoot.test.tsx` "should open the select by default"; conformance suite "opens the popup with the `open` prop", "is ignored when `open={false}` is passed".
- **SEL-API-05** — MUST. `onOpenChange` MUST receive the requested open state and an open-change-details object that additionally exposes `preventUnmountOnClose()` (EVD-API-09, TRN-EVT-07). Rules in §7 and §9.
  Source: `select/root/SelectRoot.tsx:288-315,657-658,758-761`. Test: [L2] `SelectRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out".
- **SEL-API-06** — MUST. `onOpenChangeComplete` MUST receive exactly one boolean argument (TRN-API-04); timing in SEL-ANI-05/06.
  Source: `select/root/SelectRoot.tsx:206-214,453,662`, `select/popup/SelectPopup.tsx:178-186`. Test: [L2] `SelectRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once".
- **SEL-API-07** — MUST. The effective form name is `Field.Root.name` when defined, otherwise the `name` prop. It is placed only on the hidden input(s) (SEL-DOM-47, SEL-DOM-53), never on the trigger.
  Source: `select/root/SelectRoot.tsx:100,489,503,572`. Test: [L2] `SelectRoot.test.tsx` "should receive name prop from Field.Root", "should handle browser autofill".
- **SEL-API-08** — MUST. `form` MUST be forwarded as the `form` attribute of the hidden input and of every per-value hidden input.
  Source: `select/root/SelectRoot.tsx:502,571`. Test: [L2] `SelectRoot.test.tsx` "submits to an external form when `form` is provided", "submits multiple values to an external form when `form` is provided".
- **SEL-API-09** — MUST. `autoComplete` MUST be forwarded as the `autocomplete` attribute of the hidden input and nowhere else.
  Source: `select/root/SelectRoot.tsx:573`. Test: [L2] `SelectRoot.test.tsx` "should pass autoComplete to the hidden input".
- **SEL-API-10** — MUST. `id` is the trigger's `id` unless the trigger has its own `id` prop (SEL-API-26); when absent, a generated id is used. The same id is the labelable control id of the enclosing field scope (FFI), the basis of the default `Select.Label` id `${id}-label` and of the hidden input id `${id}-hidden-input`.
  Source: `select/root/SelectRoot.tsx:97,140,247,570`, `select/trigger/SelectTrigger.tsx:78,83,134`, `select/label/SelectLabel.tsx:31-32`. Test: [L2] `SelectRoot.test.tsx` "sets the id on the trigger", "applies the root id to the trigger", "sets a hidden input id when name is not provided", "updates Select.Label linkage when root id changes".
- **SEL-API-11** — MUST. `required` defaults to `false`. When `true`: the trigger carries `aria-required="true"` (no `data-required`, SEL-DOM-03), and the hidden input carries the `required` attribute except in multiple mode while at least one value is selected (SEL-DOM-50).
  Source: `select/root/SelectRoot.tsx:70,576`, `select/trigger/SelectTrigger.tsx:143`. Test: [L2] `SelectTrigger.test.tsx` "sets aria-required attribute when required"; `SelectRoot.test.tsx` "triggers native HTML validation on submit", "does not mark the hidden input as required when selection exists".
- **SEL-API-12** — MUST. `readOnly` defaults to `false`. When `true`: the trigger carries `aria-readonly="true"` and `data-readonly`, the listbox carries `aria-readonly="true"`, the hidden input carries `readonly`; the popup MAY still be opened and browsed (pointer, ArrowDown/ArrowUp, Enter, Space, typeahead while open), but no interaction commits a value (SEL-EDGE-06) and closed-trigger typeahead is disabled (SEL-KEY-04).
  Source: `select/root/SelectRoot.tsx:69,361-367,388-391,528,577`, `select/trigger/SelectTrigger.tsx:142`, `select/item/SelectItem.tsx:136-138`, `select/popup/SelectPopup.tsx:443`, `select/list/SelectList.tsx:35`. Test: [L2] `SelectRoot.test.tsx` "prop: readOnly" (all seven tests) (L4 for the open-and-browse clause). Docs: `CHANGELOG.md v1.8.0 "Allow opening and browsing the popup while \`readOnly\`"`.
- **SEL-API-13** — MUST. The Root's effective disabled state is `Field.Root.disabled || disabled` (default `false`). While disabled: the trigger is disabled (SEL-API-24), the trigger-press, list-navigation and typeahead interactions are disabled, every item is disabled (SEL-API-41), autofill is ignored (SEL-FRM-19), the hidden input(s) carry `disabled` so nothing is submitted (SEL-FRM-11), and the control does not register with the field (FFI `enabled = !disabled`).
  Source: `select/root/SelectRoot.tsx:68,99,250,365,372,391,505,575`, `select/item/SelectItem.tsx:61`. Test: [L2] `SelectRoot.test.tsx` "sets the disabled state", "updates the disabled state when the disabled prop changes", "should receive disabled prop from Field.Root", "does not submit multiple values when disabled"; `SelectItem.test.tsx` "inherits the disabled state from the root".
- **SEL-API-14** — MUST. `multiple` defaults to `false`. When `true`: the value is an array (SEL-STA-11), activating an item toggles its membership (SEL-EVT-05), the popup stays open after activation (SEL-DIS-07), the listbox carries `aria-multiselectable="true"`, per-value hidden inputs are rendered (SEL-DOM-53), closed-trigger typeahead is disabled (SEL-KEY-04) and autofill is ignored (SEL-FRM-19). The `multiple` prop MUST NOT change after mount (the type is a generic parameter; the hint "conditional `multiple`" refers only to typing).
  Source: `select/root/SelectRoot.tsx:76,104,229,391,443-448,536-539`. Test: [L2] `SelectRoot.test.tsx` "prop: multiple" (all tests).
- **SEL-API-15** — MUST. `highlightItemOnHover` defaults to `true` and is passed to list navigation as `focusItemOnHover` (CMP-API-03). It additionally gates the automatic first-item highlight of aligned positioning (SEL-POS-10).
  Source: `select/root/SelectRoot.tsx:80,385`, `select/popup/SelectPopup.tsx:396-403`. Test: [L4] `SelectRoot.test.tsx` "prop: highlightItemOnHover" (all tests).
- **SEL-API-16** — MUST. `modal` defaults to `true`. When `true` and the popup is mounted, the internal backdrop is rendered (SEL-STR-07) and page scroll is locked while open (SEL-POS-21). It does not trap focus (SEL-FOC-09). When `false`, neither backdrop nor scroll lock is applied unless aligned positioning is active.
  Source: `select/root/SelectRoot.tsx:71`, `select/positioner/SelectPositioner.tsx:89-94,228`. Test: [L2] `SelectRoot.test.tsx` "prop: modal" (both tests), "keeps a modal popover open when choosing a non-modal aligned select item" (L4 for the scroll-lock clause).
- **SEL-API-17** — MUST. `actionsRef`, when given, MUST be populated with an object exposing `close()` — which, only while open, requests a close with reason `imperative-action` and no event — and `unmount()` (TRN-EVT-08). `close()` while closed MUST NOT invoke `onOpenChange`.
  Source: `select/root/SelectRoot.tsx:317-328,741-744`. Test: [L2] `SelectRoot.test.tsx` "manual unmount lifecycle" (all tests), "unmounts the select when the `unmount` method is called", "does not call `onOpenChange` when the `close` action is called while closed".
- **SEL-API-18** — MUST. `inputRef` MUST receive the hidden form-participation input element (shared with the field's validation input handle).
  Source: `select/root/SelectRoot.tsx:487,578`. Test: none.
- **SEL-API-19** — MUST. `items` accepts (a) a record whose keys are string values and whose entries are labels, (b) an array of `{ value, label }` objects, or (c) an array of group objects `{ items: ReadonlyArray<...>, ...anything }` whose `items` are `{ value, label }` objects; `undefined` means "no lookup". It affects only label resolution in `Select.Value` (SEL-DOM-10) and the placeholder decision (SEL-DOM-11); it does not render items.
  Source: `select/root/SelectRoot.tsx:698-702`, `internals/resolveValueLabel.tsx:5-36`. Test: [L2] `SelectValue.test.tsx` "prop: items (object format)" (5 tests), "prop: items (array format)" (5 tests); `SelectRoot.spec.tsx` (grouped readonly items type-check).
- **SEL-API-20** — MUST. `itemToStringLabel`, when given, is used to produce the display text of a non-null value (SEL-DOM-10) and the label matched by autofill (SEL-FRM-18). It MUST NOT be invoked with `null`/`undefined`, nor with the whole value array in multiple mode.
  Source: `select/root/SelectRoot.tsx:548,707`, `internals/resolveValueLabel.tsx:68-71,102-104,142-158`. Test: [L2] `SelectRoot.test.tsx` "uses itemToStringLabel for trigger text when value is object", "updates trigger text with itemToStringLabel after selecting object item"; `SelectValue.test.tsx` "uses custom itemToStringLabel function".
- **SEL-API-21** — MUST. `itemToStringValue`, when given, is used to serialize each non-null value for form submission, the `filled`/placeholder decision and autofill matching. It MUST NOT be invoked with `null`/`undefined`, nor with the whole value array in multiple mode (each member is stringified separately).
  Source: `select/root/SelectRoot.tsx:225-240,497,547,712`, `internals/resolveValueLabel.tsx:83-91`. Test: [L2] `SelectRoot.test.tsx` "uses itemToStringValue for form submission", "uses itemToStringValue for multiple selection form submission", "does not invoke itemToStringValue with the value array in multiple mode", "submits stringified value to onFormSubmit when itemToStringValue is provided".
- **SEL-API-22** — MUST. `isItemEqualToValue(itemValue, selectedValue)` defaults to `Object.is`. The first argument is always an *item's* value and the second a *selected* value. It is consulted only when both operands are non-nullish (UTL `compareItemEquality`); nullish operands are compared with `Object.is` directly. In multiple mode it is never invoked with the value array as an operand.
  Source: `select/root/SelectRoot.tsx:79,714-717`, `internals/itemEquality.ts:9-21`. Test: [L2] `SelectRoot.test.tsx` "prop: isItemEqualToValue" (all three tests), "does not invoke isItemEqualToValue with the value array in multiple mode when empty", "compares object values with isItemEqualToValue when clearing [data-dirty] in multiple mode".
- **SEL-API-23** — MUST. `children` are rendered as-is before the hidden input(s); the Root exposes its shared state to every descendant part.
  Source: `select/root/SelectRoot.tsx:512-515`. Test: [L2] all.

#### State object

`Select.Root.State` is an empty object (the Root renders nothing). The internal store (`select/store.ts:11-60`) is described in §4.

#### Data attributes / CSS variables

None (no element).

### 3.2 `Select.Label`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) | — | Standard; functions receive the field state object. | SEL-API-28 |
| `id` | — | — | **Not accepted**: omitted from the type and deleted at runtime; the id is always derived (SEL-DOM-14). | SEL-API-32 |
| *other `<div>` attributes* | — | — | Forwarded. `onClick`/`onPointerDown` chain with the label behavior (SEL-DOM-15). | SEL-API-27 |

- **SEL-API-32** — MUST. `Select.Label` MUST ignore an `id` prop: its rendered `id` is always the registered label id (default `${rootId}-label`, SEL-DOM-14).
  Source: `select/label/SelectLabel.tsx:22-25,32-36,54-57`. Test: none.

#### State object

The enclosing field's state (FFI-STA-01: `disabled`, `touched`, `dirty`, `filled`, `focused`, `valid`), all defaults outside a `Field.Root`.

- **SEL-STA-18** — MUST. The Label state object is exactly the enclosing field's state object (FFI-STA-01), unmodified (it does not add the Root's `disabled`).
  Source: `select/label/SelectLabel.tsx:27,46`. Test: none.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled`, `data-touched`, `data-dirty`, `data-filled`, `data-focused` | field flag `true` | SEL-DOM-14 |
| `data-valid` / `data-invalid` | field `valid === true` / `=== false` | SEL-DOM-14 |

(The API outline lists no data attributes for Label; see §16.)

### 3.3 `Select.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Combined (OR) with the Root's and the Field's disabled. | SEL-API-24 |
| `nativeButton` | `boolean` | `true` | Declares whether the rendered element is a native `<button>` (UBT). | SEL-API-25 |
| `id` | `string` | Root `id` | Overrides the trigger's id (not listed in the API outline). | SEL-API-26 |
| `children` | `ReactNode` | — | Typically `Select.Value` and `Select.Icon`. | SEL-API-27 |
| `className`, `style`, `render` | RND | — | Functions receive the trigger state object. | SEL-API-28 |
| `aria-label`, `aria-labelledby`, `aria-describedby` | `string` | — | Forwarded; `aria-describedby` is composed with field message ids (SEL-ARIA-07); a consumer `aria-labelledby` overrides SEL-ARIA-05 (RND-DOM-19). | SEL-ARIA-05 |
| *other `<button>` attributes and handlers* | — | — | Forwarded, merged after the built-in attributes (consumer attributes win except `role`, SEL-ARIA-01). | SEL-API-27 |

**Consumed:** `render`, `className`, `style`, `id`, `disabled`, `nativeButton`.

- **SEL-API-24** — MUST. The trigger's effective disabled state is `Field.Root.disabled || Root.disabled || Trigger.disabled`; it is passed to the button mechanism (`disabled`, UBT) and sets `data-disabled`; when disabled the trigger's focus-ownership reports no field focus (SEL-FOC-10) and `tabindex` is `-1` (SEL-DOM-07).
  Source: `select/trigger/SelectTrigger.tsx:51,62-69,91-96,144,221`. Test: [L2] `SelectTrigger.test.tsx` "disabled state" (all three tests).
- **SEL-API-25** — MUST. `nativeButton` defaults to `true` and maps to the button mechanism's `native` option (UBT-API-02); `composite` is inferred (UBT-STA-01) and `focusableWhenDisabled` is not passed.
  Source: `select/trigger/SelectTrigger.tsx:52,93-96`; UBT §6.2 row `Select.Trigger`. Test: [L2] `SelectTrigger.test.tsx` conformance suite (`button: true`), "is removed from the tab order when disabled and not rendered as a button".
- **SEL-API-26** — MUST. When the trigger receives an `id` prop it is used as the trigger's `id` and registered as the field's control id (so `Field.Label` points at it) instead of the Root's id; otherwise the Root's id is used.
  Source: `select/trigger/SelectTrigger.tsx:50,83,86,134`. Test: [L2] `SelectRoot.test.tsx` "Field.Label links to trigger when trigger has an explicit id".
- **SEL-API-27** — MUST. Every prop not listed as consumed MUST be forwarded to the trigger element; consumer handlers chain with the built-in ones per RND-EVT-02 (consumer first). `children` are rendered inside the element.
  Source: `select/trigger/SelectTrigger.tsx:131-211,228-233`. Test: [L2] `SelectTrigger.test.tsx` conformance suite.
- **SEL-API-28** — MUST. For every part of this document, `className`, `style` and `render` MUST behave per RND (RND-API-01…03); the state object each part supplies is the one listed in its "State object" sub-section (an empty object for Portal, List, ItemText, Group, GroupLabel). Server: relaxed, see §9.
  Source: each part's `useRenderElement` call. Test: [L2] every part's conformance suite (§13).

#### State object

The enclosing field's state (FFI-STA-01) with five trigger fields layered on top:

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | The popup is open. |
| `disabled` | `boolean` | Effective disabled (SEL-API-24). |
| `readOnly` | `boolean` | Root `readOnly`. |
| `value` | `any` | Current value (not projected to an attribute). |
| `popupSide` | `Side \| null` | Resolved side of the mounted popup; `null` while unmounted or before a positioner exists (SEL-STA-28). |
| `placeholder` | `boolean` | No value is selected (`!hasSelectedValue`, SEL-STA-02). |
| `touched`, `dirty`, `valid`, `filled`, `focused` | field | FFI-STA-01. |

- **SEL-STA-15** — MUST. The trigger state object MUST contain exactly the fields above (the field's `disabled` is replaced by the effective trigger disabled).
  Source: `select/trigger/SelectTrigger.tsx:218-226,236-257`. Test: none.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open` | `""` while open | SEL-DOM-02 |
| `data-pressed` | `""` while open | SEL-DOM-02 |
| `data-disabled` | `""` while effectively disabled | SEL-DOM-03 |
| `data-readonly` | `""` while `readOnly` | SEL-DOM-03 |
| `data-required` | declared, **never emitted** in 1.9.0 (§16 Q6) | SEL-DOM-03 |
| `data-placeholder` | `""` while no value is selected | SEL-DOM-04 |
| `data-popup-side` | `'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-start' \| 'inline-end'` while the popup is mounted | SEL-DOM-05 |
| `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid` | field flags | SEL-DOM-06 |

#### CSS variables

None.

### 3.4 `Select.Value`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode \| ((value: any) => ReactNode)` | — | Overrides the rendered text; a function receives the current value (the array in multiple mode). | SEL-API-29 |
| `placeholder` | `ReactNode` | — | Shown when no value is selected, unless `children` is given or `items` contains a labelled `null` item. | SEL-API-30 |
| `className`, `style`, `render` | RND | — | Functions receive `{ value, placeholder }`. | SEL-API-28 |
| *other `<span>` attributes* | — | — | Forwarded. | SEL-API-27 |

**Consumed:** `className`, `render`, `children`, `placeholder`, `style`.

- **SEL-API-29** — MUST. When `children` is a function it MUST be called during each render of the part with the current value (SEL-API-02's shared empty array in multiple mode) and its result rendered; when it is any other non-nullish node that node is rendered; both take precedence over `items` lookup and over `placeholder`.
  Source: `select/value/SelectValue.tsx:47-51,81-96`. Test: [L2] `SelectValue.test.tsx` "accepts a function with a value parameter", "overrides the text when children is a string", "children function receives array of values in multiple mode", "children prop takes precedence over items in multiple mode", "uses children string over items object", "uses children function over items array", "children prop takes precedence over placeholder", "children function takes precedence over placeholder".
- **SEL-API-30** — MUST. `placeholder` is rendered iff no value is selected (SEL-STA-02), `children` is nullish, and `items` does not contain a `null`-valued entry with a non-null label (a record with a `"null"` key counts). Otherwise the label resolution of SEL-DOM-10 applies (so a labelled `null` item shows its label instead of the placeholder).
  Source: `select/value/SelectValue.tsx:39-40,52-53`, `select/store.ts:121-123`, `internals/resolveValueLabel.tsx:41-66`. Test: [L2] `SelectValue.test.tsx` "prop: placeholder" (all ten tests); `SelectTrigger.test.tsx` "should have the data-placeholder attribute when provided { value: null }".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `value` | `any` | Current value (not projected). |
| `placeholder` | `boolean` | No value is selected. |

- **SEL-STA-16** — MUST. The Value state object MUST contain exactly `value` and `placeholder`.
  Source: `select/value/SelectValue.tsx:42-45,70-79`. Test: none.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-placeholder` | `""` while no value is selected | SEL-DOM-11 |

### 3.5 `Select.Icon`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | `'▼'` (U+25BC) | Replaces the default glyph. | SEL-API-31 |
| `className`, `style`, `render` | RND | — | Functions receive `{ open }`. | SEL-API-28 |
| *other `<span>` attributes* | — | — | Forwarded (a consumer `aria-hidden` overrides the default). | SEL-API-27 |

- **SEL-API-31** — MUST. `Select.Icon` MUST render the single character `▼` as its content when the consumer supplies no `children`.
  Source: `select/icon/SelectIcon.tsx:30`. Test: none.
- **SEL-STA-17** — MUST. The Icon state object MUST contain exactly `open: boolean`.
  Source: `select/icon/SelectIcon.tsx:23-25,37-42`. Test: none.

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open` | `""` while open | SEL-DOM-13 |

### 3.6 `Select.Portal`

**Default element:** `<div>` (the portal container appended to `container`, FLT). **Accepts `render` element substitution:** yes (forwarded to the portal element). **Accepts `className`/`style`:** yes (forwarded).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `container` | `HTMLElement \| ShadowRoot \| DOM handle to either \| null` | document `<body>` | Where the portal element is appended. | SEL-API-33 |
| `className`, `style`, `render`, *other `<div>` attributes* | RND / FLT | — | Forwarded to the portal element. | SEL-API-28 |

- **SEL-API-33** — MUST. `container` and every other prop MUST be forwarded to the shared floating portal (FLT) unchanged; the Portal adds only the render predicate of SEL-STR-06. `keepMounted` is **not** a Portal prop in v1.9.0 (see §16).
  Source: `select/portal/SelectPortal.tsx:27,30-38`. Test: [L2] `SelectPortal.test.tsx` conformance suite. Docs: `CHANGELOG.md v1.0.0-beta.2 "Support \`ShadowRoot\` containers"`.
- **SEL-STA-27** — MUST. The state object of `Select.Portal`, `Select.List`, `Select.ItemText`, `Select.Group` and `Select.GroupLabel` is the empty object; they emit no state data attributes.
  Source: `select/portal/SelectPortal.tsx:30`, `select/list/SelectList.tsx:56`, `select/item-text/SelectItemText.tsx:49`, `select/group/SelectGroup.tsx:43`, `select/group-label/SelectGroupLabel.tsx:40`. Test: none.

### 3.7 `Select.Backdrop`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | RND | — | Functions receive `{ open, transitionStatus }`. | SEL-API-28 |
| *other `<div>` attributes* | — | — | Forwarded; a consumer `style` merges over the built-in `user-select: none` (RND-DOM-16). | SEL-API-27 |

- **SEL-STA-19** — MUST. The Backdrop state object MUST contain exactly `open: boolean` and `transitionStatus` (TRN-API-07).
  Source: `select/backdrop/SelectBackdrop.tsx:34-37,59-68`. Test: none.

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / not open | SEL-DOM-17 |
| `data-starting-style` / `data-ending-style` | transition phase | SEL-DOM-17 |

### 3.8 `Select.Positioner`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `alignItemWithTrigger` | `boolean` | `true` | Overlap the trigger so the selected item's text aligns with the value text (mouse/keyboard only; auto-disabled when it does not fit). | SEL-API-35 |
| `anchor` | `Element \| VirtualElement \| DOM handle \| () => Element \| VirtualElement \| null \| null` | the trigger | Positioning anchor. | SEL-API-34 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | CSS position used while not aligned. | SEL-API-34 |
| `side` | `Side` | `'bottom'` | Preferred side (ignored while aligned). | SEL-API-34 |
| `align` | `'start' \| 'center' \| 'end'` | `'center'` | Alignment (ignored while aligned). | SEL-API-34 |
| `sideOffset` / `alignOffset` | `number \| OffsetFunction` | `0` / `0` | Offsets. | SEL-API-34 |
| `collisionBoundary` | `Boundary` | `'clipping-ancestors'` | Collision boundary. | SEL-API-34 |
| `collisionPadding` | `Padding` | `5` | Padding from the boundary. | SEL-API-34 |
| `collisionAvoidance` | `CollisionAvoidance` | `{ fallbackAxisSide: 'none' }` | Dropdown preset: never flip to the perpendicular axis. | SEL-API-37 |
| `arrowPadding` | `number` | `5` | Arrow inset. | SEL-API-34 |
| `sticky` | `boolean` | `false` | Keep in viewport when anchor scrolls out. | SEL-API-34 |
| `disableAnchorTracking` | `boolean` | `undefined` → equals "aligned mode active" | Disable layout-shift tracking. | SEL-API-38 |
| `keepMounted` | — | — | **Not accepted** (type error). | SEL-API-36 |
| `className`, `style`, `render` | RND | — | Functions receive `{ open, side, align, anchorHidden }`. | SEL-API-28 |
| *other `<div>` attributes* | — | — | Forwarded (merged after the built-in `role`, `hidden`, `style`). | SEL-API-27 |

**Consumed:** all positioning props above, `alignItemWithTrigger`, `className`, `render`, `style`.

- **SEL-API-34** — MUST. The positioning props MUST be passed to the shared anchor-positioning mechanism (FLT) with the defaults in the table (`collisionBoundary` defaults locally to `'clipping-ancestors'`; the other defaults are the mechanism's). Their semantics are FLT's; §10 lists only what Select overrides.
  Source: `select/positioner/SelectPositioner.tsx:37-57,96-112`, `internals/useAnchorPositioning.ts:144-156`. Test: [L4] `SelectPositioner.test.tsx` positioner conformance suite (options: `align="center"`, `alignItemWithTrigger={false}`).
- **SEL-API-35** — MUST. `alignItemWithTrigger` defaults to `true`. Its effect is specified in SEL-POS-03…POS-24; it is only *requested* by the prop — the mechanism may fall back (SEL-POS-04/05).
  Source: `select/positioner/SelectPositioner.tsx:53,75-82,254-261`. Test: [L4] `SelectPopup.test.tsx` alignment tests (§13). Docs: `components/select/page.mdx` §Positioning.
- **SEL-API-36** — MUST. `Select.Positioner` MUST NOT accept a `keepMounted` prop (compile-time error); the positioner is always kept mounted internally (SEL-POS-02).
  Source: `select/positioner/SelectPositioner.spec.tsx:3-4`, `select/positioner/SelectPositioner.tsx:111`. Test: [L1] `SelectPositioner.spec.tsx` (type-level). Docs: `CHANGELOG.md v1.0.0-alpha.5 "Require \`Portal\` part"`.
- **SEL-API-37** — MUST. `collisionAvoidance` defaults to the dropdown preset `{ fallbackAxisSide: 'none' }` (UTL-EDGE-19 `DROPDOWN_COLLISION_AVOIDANCE`).
  Source: `select/positioner/SelectPositioner.tsx:54`, `internals/constants.ts:18-20`. Test: none.
- **SEL-API-38** — MUST. When `disableAnchorTracking` is `undefined`, anchor tracking MUST be disabled exactly while aligned positioning is active and enabled otherwise; an explicit boolean wins.
  Source: `select/positioner/SelectPositioner.tsx:109`. Test: none.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | The popup is open. |
| `side` | `Side \| 'none'` | Resolved side; the literal `'none'` while aligned positioning is active. |
| `align` | `'start' \| 'center' \| 'end'` | Resolved alignment (FLT). |
| `anchorHidden` | `boolean` | Anchor is hidden (FLT). |

- **SEL-STA-20** — MUST. The Positioner state object MUST contain exactly the fields above; `side` MUST be `'none'` while aligned positioning is active and the mechanism's resolved side otherwise. Server: relaxed, see §9.
  Source: `select/positioner/SelectPositioner.tsx:114,117-122,235-252`. Test: [L4] `SelectPopup.test.tsx` "keeps alignItemWithTrigger active at browser zoom" (`data-side="none"`); `SelectTrigger.test.tsx` "sets data-popup-side to the current popup side".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / not open | SEL-DOM-19 |
| `data-anchor-hidden` | `""` while the anchor is hidden | SEL-DOM-19 |
| `data-side` | `'none' \| 'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-start' \| 'inline-end'` | SEL-DOM-19 |
| `data-align` | `'start' \| 'center' \| 'end'` | SEL-DOM-19 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--anchor-width`, `--anchor-height` | positioner | anchor size in px (FLT) | SEL-DOM-22 |
| `--available-width`, `--available-height` | positioner | space to the viewport edge (FLT; `100vw`/`100vh` until positioned) | SEL-DOM-22 |
| `--transform-origin` | positioner (FLT); popup element in aligned mode | see SEL-DOM-22 / SEL-DOM-29 | SEL-DOM-22 |

### 3.9 `Select.Popup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `finalFocus` | `boolean \| DOM handle \| (closeType: InteractionType) => boolean \| HTMLElement \| null \| void` | `true` (trigger / previously focused element) | Element to focus when the popup closes. | SEL-API-39 |
| `children` | `ReactNode` | — | List content. | SEL-API-27 |
| `className`, `style`, `render` | RND | — | Functions receive `{ open, transitionStatus, side, align }`. | SEL-API-28 |
| *other `<div>` attributes* | — | — | Forwarded (consumer `onKeyDown`/`onScroll` chain; consumer `style` merges over built-in styles). | SEL-API-27 |

**Consumed:** `render`, `className`, `style`, `finalFocus`.

- **SEL-API-39** — MUST. `finalFocus` is passed as the focus manager's `returnFocus` (FLT-API-05, FLT-FOC-21) and resolved when the popup closes: `false`/`undefined` → focus is not moved; `true`/`null` → the default target (SEL-FOC-04); a DOM handle → its element; a function → called once with the close interaction type (`'mouse' | 'touch' | 'pen' | 'keyboard' | ''`) and its result interpreted the same way. There is no `initialFocus` prop.
  Source: `select/popup/SelectPopup.tsx:51,479-486,495-508`; see FLT-API-05, FLT-FOC-21. Test: [L4] `SelectPopup.test.tsx` `popupFocusPropsTests` (options: `initialFocus: false`, `triggerRole: 'combobox'`, `alwaysMounted: true`, `mouseCloseType: ''`). Docs: `CHANGELOG.md v1.2.0 "Add \`finalFocus\` prop"`.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | |
| `transitionStatus` | TRN status | |
| `side` | `Side \| 'none'` | As the Positioner's. |
| `align` | `Align` | As the Positioner's. |

- **SEL-STA-21** — MUST. The Popup state object MUST contain exactly the fields above.
  Source: `select/popup/SelectPopup.tsx:188-193,511-528`. Test: none.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / not open | SEL-DOM-26 |
| `data-starting-style` / `data-ending-style` | transition phase | SEL-DOM-26 |
| `data-side` | as Positioner (`'none'` while aligned) | SEL-DOM-26 |
| `data-align` | as Positioner | SEL-DOM-26 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--transform-origin` | popup element (inline, aligned mode only) | `50% <Y>%` where Y is the selected text's vertical center within the positioner | SEL-DOM-29 |

### 3.10 `Select.List`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style`:** yes (state is empty).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render`, *other `<div>` attributes* | RND | — | Forwarded; consumer `onScroll` chains; consumer `className` merges with the built-in scrollbar class. | SEL-API-27 |

No props of its own. State: empty (SEL-STA-27). Data attributes: none.

### 3.11 `Select.Arrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render`, *other `<div>` attributes* | RND | — | Forwarded; consumer `style` merges over the arrow position styles. | SEL-API-27 |

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | |
| `side` | `Side \| 'none'` | |
| `align` | `Align` | |
| `uncentered` | `boolean` | The arrow cannot be centered on the anchor (FLT). |

- **SEL-STA-22** — MUST. The Arrow state object MUST contain exactly `open`, `side`, `align`, `uncentered`.
  Source: `select/arrow/SelectArrow.tsx:28-33,49-66`. Test: none.

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / not open | SEL-DOM-40 |
| `data-side`, `data-align` | resolved placement | SEL-DOM-40 |
| `data-uncentered` | `""` while uncentered | SEL-DOM-40 |

### 3.12 `Select.Item`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `value` | `any` | `null` | The item's value. | SEL-API-40 |
| `disabled` | `boolean` | `false` | Combined (OR) with the Root's disabled. | SEL-API-41 |
| `label` | `string` | item text content | Text used for typeahead matching. | SEL-API-42 |
| `nativeButton` | `boolean` | `false` | Declares a native `<button>` substitution (UBT). | SEL-API-43 |
| `id` | — | — | Omitted from the type; a runtime `id` is forwarded but never generated. | SEL-API-44 |
| `children` | `ReactNode` | — | Content (typically `Select.ItemText` + `Select.ItemIndicator`). | SEL-API-27 |
| `className`, `style`, `render` | RND | — | Functions receive `{ disabled, selected, highlighted }`. | SEL-API-28 |
| *other `<div>` attributes and handlers* | — | — | Forwarded; `onClick`, `onKeyDown`, `onPointerDown/Enter/Move`, `onMouseUp` chain before the built-in handlers (RND-EVT-02) and are subject to the disabled gate of UBT-EVT-03. | SEL-API-27 |

**Consumed:** `render`, `className`, `style`, `value`, `label`, `disabled`, `nativeButton`.

- **SEL-API-40** — MUST. `value` defaults to `null`; a `null`-valued item is a legitimate option (selecting it sets the value to `null`, which `Select.Value` can label through `items`, SEL-API-30).
  Source: `select/item/SelectItem.tsx:39,278-281`. Test: [L2] `SelectValue.test.tsx` "null item label in items takes precedence over placeholder"; `SelectItem.test.tsx` "treats a null value as an empty selection in multiple mode".
- **SEL-API-41** — MUST. The item's effective disabled state is `Root.disabled || Item.disabled`; it is passed to the button mechanism with `focusableWhenDisabled: true` and `composite: true` (UBT), so a disabled item stays focusable and navigable but cannot be activated (SEL-PTR-10, SEL-KEY-08).
  Source: `select/item/SelectItem.tsx:41,54-61,120-125`; UBT §6.2 row `Select.Item`. Test: [L4] `SelectItem.test.tsx` "should focus disabled items", "should not select disabled item", "should not select disabled item when Enter is pressed", "inherits the disabled state from the root". Docs: `CHANGELOG.md v1.7.0 "Inherit the disabled state from \`<Select.Root>\` in \`<Select.Item>\`"`.
- **SEL-API-42** — MUST. `label`, when not `undefined`, is the item's typeahead label; otherwise the text content of its `Select.ItemText` element when one exists, else the item element's text content (CMP-STA-06). The same label is the third-priority autofill match (SEL-FRM-18).
  Source: `select/item/SelectItem.tsx:46-51,288-292`. Test: [L4] `SelectRoot.test.tsx` "matches an item by its label when the rendered text differs".
- **SEL-API-43** — MUST. `nativeButton` defaults to `false` (UBT-API-03); the item therefore gets non-native keyboard activation (Enter on keydown, Space on keydown in composite mode, UBT-KEY-01/12).
  Source: `select/item/SelectItem.tsx:42,123`. Test: [L4] `SelectItem.test.tsx` conformance suite (`button: true`), "should select item when Enter key is pressed".
- **SEL-API-44** — MUST. `Select.Item` MUST NOT generate an `id` of its own (no `aria-activedescendant` scheme is used, SEL-ARIA-14); the `id` key is excluded from its public parameter type, but a value supplied anyway is forwarded as an ordinary attribute.
  Source: `select/item/SelectItem.tsx:274-275,239-243`. Test: none.

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | Effective disabled. |
| `selected` | `boolean` | The item's value is (one of) the selected value(s) (SEL-STA-03). |
| `highlighted` | `boolean` | The item is the active (highlighted) item. |

- **SEL-STA-23** — MUST. The Item state object MUST contain exactly `disabled`, `selected`, `highlighted`.
  Source: `select/item/SelectItem.tsx:127-131,259-272`. Test: [L2] `SelectItem.test.tsx` "style hooks" (both tests).

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-selected` | `""` while selected | SEL-DOM-35 |
| `data-highlighted` | `""` while highlighted | SEL-DOM-35 |
| `data-disabled` | `""` while disabled | SEL-DOM-35 |

### 3.13 `Select.ItemText`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style`:** yes (state empty).

No props of its own; all attributes forwarded (SEL-API-27). State: empty (SEL-STA-27). Data attributes: none. Its element is the item's *text element*: the typeahead label source (SEL-API-42) and the alignment reference of aligned positioning (SEL-POS-07).

### 3.14 `Select.ItemIndicator`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the DOM while the item is not selected. | SEL-API-45 |
| `children` | `ReactNode` | `'✔️'` | Replaces the default glyph (RND-DOM-50). | SEL-API-27 |
| `className`, `style`, `render`, *other `<span>` attributes* | RND | — | Forwarded. | SEL-API-28 |

- **SEL-API-45** — MUST. `keepMounted` defaults to `false` and selects the render predicate of SEL-STR-11 and the kept-mounted transition behavior of RND-ANI-02/03.
  Source: `select/item-indicator/SelectItemIndicator.tsx:20,44-47`. Test: [L2] `SelectItemIndicator.test.tsx` (all three tests).
- **SEL-STA-24** — MUST. The ItemIndicator state object MUST contain exactly `selected: boolean` and `transitionStatus`.
  Source: `utils/ItemIndicator.tsx:22-26`, `select/item-indicator/SelectItemIndicator.tsx:28-37`. Test: none.

| Attribute | Present when / value | Req |
|---|---|---|
| `data-selected` | `""` while selected | SEL-DOM-39 |
| `data-starting-style` / `data-ending-style` | transition phase | SEL-DOM-39 |

### 3.15 `Select.Group`

**Default element:** `<div>`. No props of its own; state empty (SEL-STA-27); data attributes none. Provides the group-label id to its `Select.GroupLabel` (SEL-DOM-41).

### 3.16 `Select.GroupLabel`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style`:** yes (state empty).

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `id` | `string` | generated | The label's id, published to the group's `aria-labelledby`. | SEL-API-46 |
| `aria-hidden` | — | `true` | A consumer value (including `undefined`) overrides the default. | SEL-DOM-42 Port: relaxed (ADR 02, RND-DOM-19/32). |
| `className`, `style`, `render`, *other `<div>` attributes* | RND | — | Forwarded. | SEL-API-28 |

- **SEL-API-46** — MUST. `id` is used verbatim when given, else a generated id (RND-DOM-27…30); whichever is current is published to the enclosing group post-commit, pre-paint and withdrawn on unmount only if the group still holds that id (FFI-ARIA-08 semantics).
  Source: `select/group-label/SelectGroupLabel.tsx:19-30`. Test: [L2] `SelectGroupLabel.test.tsx` "removes the group aria-labelledby attribute when unmounted", "does not let an older label cleanup clear a newer label", "updates explicit and generated ids independently of ref churn", "replaces and unregisters its label in Strict Mode".

### 3.17 `Select.ScrollUpArrow` / `Select.ScrollDownArrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the element in the DOM while not visible. | SEL-API-47 |
| `children` | `ReactNode` | `'▲'` (up) / `'▼'` (down) | Replaces the default glyph. | SEL-DOM-43 |
| `className`, `style`, `render`, *other `<div>` attributes* | RND | — | Forwarded; consumer `style` merges over `position: absolute`. | SEL-API-28 |

- **SEL-API-47** — MUST. `keepMounted` defaults to `false` (SEL-STR-10). Both parts are the same implementation with `direction` fixed to `'up'` / `'down'`; `direction` is not a public prop.
  Source: `select/scroll-up-arrow/SelectScrollUpArrow.tsx:12-17,25-29`, `select/scroll-down-arrow/SelectScrollDownArrow.tsx:12-17,25-29`, `select/scroll-arrow/SelectScrollArrow.tsx:176-181`. Test: [L2] `SelectScrollUpArrow.test.tsx` / `SelectScrollDownArrow.test.tsx` conformance suites (`keepMounted`).
- **SEL-STA-25** — MUST. The scroll-arrow state object MUST contain exactly `direction: 'up' | 'down'`, `visible: boolean` (SEL-STA-29 and not opened by touch), `side: Side | 'none'` (as the Positioner's) and `transitionStatus`. Server: relaxed, see §9.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:40,73-78,153-170`. Test: none.

| Attribute | Present when / value | Req |
|---|---|---|
| `data-direction` | `'up'` / `'down'` | SEL-DOM-44 |
| `data-visible` | `""` while visible | SEL-DOM-44 |
| `data-side` | as Positioner | SEL-DOM-44 |
| `data-starting-style` / `data-ending-style` | transition phase | SEL-DOM-45 |

### 3.18 `Select.Separator`

**Default element:** `<div>` (shared `ListboxSeparator`, RND §4.10). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `orientation` | `'horizontal' \| 'vertical'` | `'horizontal'` | Reflected as `data-orientation`. | SEL-API-48 |
| `className`, `style`, `render`, *other `<div>` attributes* | RND | — | Forwarded. | SEL-API-28 |

- **SEL-API-48** — MUST. `orientation` defaults to `'horizontal'` (RND-API-13); the state object is `{ orientation }` and the element carries `role="presentation"` and `data-orientation` (RND-DOM-53, RND-ARIA-02).
  Source: `select/separator/SelectSeparator.tsx:6-19,27-29`, `utils/listbox-separator/ListboxSeparator.tsx:16-24`. Test: [L2] `SelectRoot.test.tsx` "exposes the orientation attribute rendered by Separator".
- **SEL-STA-26** — MUST. The Separator state object MUST contain exactly `orientation`.
  Source: `select/separator/SelectSeparator.tsx:14-19`. Test: none.

### 3.19 Shared types

- `Select.Root.Props<Value, Multiple extends boolean | undefined = false>`, `Select.Root.State` (empty), `Select.Root.Actions` (`{ unmount(): void; close(): void }`).
- `Select.Root.ChangeEventReason` — exactly: `'trigger-press' | 'outside-press' | 'escape-key' | 'window-resize' | 'item-press' | 'focus-out' | 'list-navigation' | 'cancel-open' | 'imperative-action' | 'none'` (`select/root/SelectRoot.tsx:746-756`). The union is shared by `onValueChange` and `onOpenChange`; which reasons each callback actually emits is in §7.
- `Select.Root.ChangeEventDetails` = the EVD change-event details object for that reason union (EVD-API-01). `Select.Root.OpenChangeEventDetails` = the same plus `preventUnmountOnClose(): void` (EVD-API-09).
- `Side` = `'top' | 'bottom' | 'left' | 'right' | 'inline-end' | 'inline-start'`; `Align` = `'start' | 'center' | 'end'`; `InteractionType` = `'mouse' | 'touch' | 'pen' | 'keyboard' | ''` (`packages/utils/src/useEnhancedClickHandler.ts:4`).
- `Group<Item>` = `{ items: ReadonlyArray<Item>; [key: string]: unknown }` (`internals/resolveValueLabel.tsx:13-16`).
- Every part exposes `Select.<Part>.Props` and `Select.<Part>.State`; the data-attribute name sets are exported as `SelectTriggerDataAttributes`, `SelectValueDataAttributes`, `SelectIconDataAttributes`, `SelectBackdropDataAttributes`, `SelectPositionerDataAttributes`, `SelectPositionerCssVariables`, `SelectPopupDataAttributes`, `SelectItemDataAttributes`, `SelectItemIndicatorDataAttributes`, `SelectArrowDataAttributes`, `SelectScrollUpArrowDataAttributes`, `SelectScrollDownArrowDataAttributes`, `SelectSeparatorDataAttributes` (`select/index.ts:23-35`).
- Type-level contract (not runtime): with `items` given, `Value` is inferred from the items; `onValueChange` receives `Value | null` in single mode when `value` may be `null`, and `Value[]` in multiple mode (`select/root/SelectRoot.spec.tsx`).

## 4. State model

Base UI keeps one reactive store per Root (UTL §4.2) whose record (`select/store.ts:11-60`) holds: the configuration mirrors (`id`, `labelId`, `modal`, `multiple`, `disabled`, `readOnly`, `required`, `highlightItemOnHover`, `items`, `itemToStringLabel`, `itemToStringValue`, `isItemEqualToValue`), the value (`value`), the open lifecycle (`open`, `mounted`, `forceMount`, `transitionStatus`, `openMethod`), the list cursor (`activeIndex`, `selectedIndex`), the interaction attribute bags for the trigger, popup and items, the element handles (`triggerElement`, `positionerElement`, `listElement`), the resolved `popupSide`, and the scroll-arrow flags (`scrollUpArrowVisible`, `scrollDownArrowVisible`, `hasScrollArrows`). Non-reactive bookkeeping lives beside it: the item element and label registries (CMP), the per-item value registry (index → value), the typing flag, the press-release gating flags (`allowSelectedMouseUp`, `allowUnselectedMouseUp`, `dragY`), the text-element handles used by aligned positioning, the "aligned mode active" flag and the initial value. None of this is public; the observable consequences are specified below.

- **SEL-STA-01** — MUST. Every Root parameter that parts observe (`id`, `modal`, `multiple`, `disabled`, `readOnly`, `required`, `highlightItemOnHover`, `value`, `open`, `mounted`, `transitionStatus`, `items`, `itemToStringLabel`, `itemToStringValue`, `isItemEqualToValue`, `openMethod` and the interaction attribute bags) MUST be published to all parts as one atomic update post-commit, pre-paint: no part may observe a record in which some of these reflect the new render and others the previous one. The imperative commands (`setValue`, `setOpen`, scroll-arrow visibility, `onOpenChangeComplete`) MUST be callable by descendants from their first attach (first commit) onward.
  Source: `select/root/SelectRoot.tsx:450-485`, `select/store.ts:85-91`. Test: [L2] `store.test.tsx` "publishes synchronized values in a single store transaction", "provides setOpen to a descendant ref callback on the first commit", "synchronizes an updated disabled prop after descendant ref callbacks".
- **SEL-STA-02** — MUST. *Has selected value* is: `false` when the value is `null`/`undefined`; in multiple mode with an array value, `true` iff the array is non-empty; otherwise `true` iff the value's form string (SEL-DOM-51) is not the empty string. It drives `placeholder` on Trigger/Value (SEL-DOM-04/11), the field's `filled` flag (SEL-FRM-05), the hidden input's `required` in multiple mode (SEL-DOM-50) and the aligned-positioning text reference (SEL-POS-07).
  Source: `select/store.ts:109-119`, `select/root/SelectRoot.tsx:256-258`. Test: [L2] `SelectTrigger.test.tsx` "placeholder state" (all five tests); `SelectRoot.test.tsx` "[data-filled]" (all six tests).
- **SEL-STA-03** — MUST. An item is *selected* iff: in multiple mode, the value is an array containing some element `v` with `compareItemEquality(itemValue, v, isItemEqualToValue)`; in single mode, `compareItemEquality(itemValue, value, isItemEqualToValue)` (UTL `compareItemEquality`: `Object.is` when either operand is nullish, else the comparer). The value is the sole source of truth: a stale selected index (e.g. after a controlled change while open) MUST NOT keep an item marked selected.
  Source: `select/store.ts:135-150`, `internals/itemEquality.ts:12-21`. Test: [L2] `SelectRoot.test.tsx` "should update selected items when the controlled value prop changes while open", "matches object values using the provided comparator", "resets selected index when value is set to null without a null item"; `SelectItem.test.tsx` "treats a null value as an empty selection in multiple mode".
- **SEL-STA-04** — MUST. *Selected index* (the index of the item the popup opens on and anchors keyboard navigation to): while the popup is **closed**, after each value / comparer / mode change it MUST be recomputed post-commit, pre-paint from the value registry as `findSelectionIndex(values, value, isItemEqualToValue, multiple)` (UTL): the first registered index whose value equals the selected value — in multiple mode the first index *in rendered order* equal to any selected value, independent of the order of the value array; `null` when none. While **open** the recomputation is skipped (the index is then maintained by items, SEL-STA-05/06). When no index is found, the text-element handle used for alignment is cleared.
  Source: `select/root/SelectRoot.tsx:264-279`, `internals/itemEquality.ts:86-101`. Test: [L2] `SelectRoot.test.tsx` "highlights the first selected item in rendered order regardless of value order", "anchors to the first selected item in rendered order across groups", "does not jump matches after a closed-state value reset".
- **SEL-STA-05** — MUST. Whenever an item registers or its `value`/index changes (post-commit, pre-paint), in single mode it MUST claim the selected index for itself when its value equals the current value (SEL-STA-03 comparison, with an `undefined` current value never matching) and otherwise leave the index unchanged; a claiming item also publishes its text element as the alignment reference (SEL-POS-07). Server: relaxed, see §9.
  Source: `select/item/SelectItem.tsx:80-115`. Test: [L2] `SelectRoot.test.tsx` "selects the controlled value when the popup is mounted on the initial render", "matches object values when the popup is mounted on the initial render"; `SelectItem.test.tsx` "keeps the selection while grouped items are reordered and inserted".
- **SEL-STA-06** — MUST. In multiple mode the selected index is the *anchor*: the first selected item in rendered order. On each item registration the anchor is resolved by UTL `resolveSelectedIndex`: a selected item becomes the anchor unless an earlier (lower-index) anchor is still selected; when the current anchor item stops being selected (its value changed, it was removed, or the comparer changed) the anchor is re-elected as the first selected item in rendered order; the anchor item owns the alignment text element and clears it when it loses the anchor.
  Source: `select/item/SelectItem.tsx:86-99`, `internals/itemEquality.ts:104-124`. Test: [L2] `SelectRoot.test.tsx` "re-elects the anchor when the anchor item value changes in place", "moves the anchor to the next selected item when the anchor item leaves the list", "re-anchors backwards when a new isItemEqualToValue identity re-runs every item", "anchors to the first selected item when opened with the keyboard", "continues arrow navigation from the anchor rather than the top of the list". Docs: `CHANGELOG.md v1.8.0 "Anchor multiple selection to the first selected item with a linear lookup"`.
- **SEL-STA-07** — MUST. *Active index* (the highlighted item; `null` = none) is owned by the Root and written by: list navigation (CMP-STA-31…37 — on open it becomes the selected index, keyboard moves it, hover moves it when `highlightItemOnHover`), any keydown on an item (that item's index, SEL-KEY-13), typeahead while open (the match, SEL-KEY-12), scroll-arrow hovering (`null`, SEL-PTR-15), aligned placement with no selection (`0`, SEL-POS-10), and the unmount cleanup (`null`, SEL-ANI-07). A request to clear it (`null`) while the popup is **not** open MUST be ignored so the highlight is retained through the closing transition.
  Source: `select/root/SelectRoot.tsx:160,207-209,377-384,402-403`, `select/item/SelectItem.tsx:162`, `select/scroll-arrow/SelectScrollArrow.tsx:91,99`, `select/popup/SelectPopup.tsx:396-403`. Test: [L4] `SelectRoot.test.tsx` "keeps the highlight on the item while the popup animates out", "keeps the active index on a deselected item in multiple mode", "keeps the highlight on an item deselected with the keyboard".
- **SEL-STA-08** — MUST. *Open method* (`'mouse' | 'touch' | 'pen' | 'keyboard' | '' | null`) is recorded from the trigger's activation per PSH-PTR-01/02 (the Root-level variant PSH-PTR-03: reset to `null` when `open` goes from `true` to `false`). Parts observe the *rendered* open method: the current one, or — while it is `null` — the one from the previous open, so that a closing transition and a quick reopen keep the touch-specific behavior (no alignment, no scroll arrows, no scroll lock) that the previous open established.
  Source: `select/root/SelectRoot.tsx:134,222-223,484`, `utils/useOpenInteractionType.ts:9-63`. Test: [L4] `SelectRoot.test.tsx` "keeps touch interaction type when reopening quickly after close", "keeps touch positioning during the close transition".
- **SEL-STA-09** — MUST. *Force mount* starts `false` and becomes `true` (and never returns to `false`) 0 ms after the trigger receives focus, and synchronously when the hidden input receives a change (autofill). While `true`, the Portal renders even though the popup is not mounted (SEL-STR-06), so the items register their elements, values and labels for closed-trigger typeahead and autofill matching.
  Source: `select/trigger/SelectTrigger.tsx:100,153-158`, `select/root/SelectRoot.tsx:158,566`, `select/portal/SelectPortal.tsx:22`. Test: [L2] `SelectRoot.test.tsx` "commits typeahead on a closed trigger when items are provided", "should handle browser autofill".
- **SEL-STA-10** — MUST. `mounted` and `transitionStatus` are produced by the unmount-after-close machine (TRN-ALG-05) with `open`, the popup element as the watched element, `animateInitialOpen: false`, `enableIdleState: false`, `deferEndingState: false` (TRN §6.1 row "Select root"); `mounted` is seeded synchronously to the initial `open` so an initially open Select never appears to be re-opening (PSH-STA-44 analogue).
  Source: `select/root/SelectRoot.tsx:155-156,196-215,475-476`. Test: [L2] `SelectRoot.test.tsx` "manual unmount lifecycle"; conformance suite "prop: onOpenChangeComplete".
- **SEL-STA-11** — MUST. Value normalization: in multiple mode a non-array value (e.g. a controlled `null`) MUST be treated as an empty selection for item state, activation (`[...[], item]`), serialization (no per-value inputs), dirty comparison and the filled flag; the component MUST NOT convert the consumer's value. In single mode an array is an ordinary value (never treated as a list).
  Source: `select/root/SelectRoot.tsx:236-237,257,492`, `select/item/SelectItem.tsx:86,142`, `select/store.ts:114,140-142`, `internals/itemEquality.ts:92-99`. Test: [L2] `SelectItem.test.tsx` "treats a null value as an empty selection in multiple mode".
- **SEL-STA-12** — MUST. *Item reconciliation (single mode).* Whenever the item registry publishes a changed item map (CMP-STA-09) and the map was non-empty before, and the value is not `null`, and the value registry is non-empty: if no registered item's value equals the current value, the component MUST request a value change (reason `none`, no event) to the *initial value* (the value present on the Root's first render) when that initial value is non-nullish and present among the registered values, otherwise to `null`; when the new value is `null` the selected index and alignment text element are cleared. Reordering items without removing the selected one MUST NOT request any change.
  Source: `select/positioner/SelectPositioner.tsx:141-175`, `select/root/SelectRoot.tsx:132`. Test: [L2] `SelectRoot.test.tsx` "unselects the selected item if removed", "resets the value when the selected item is replaced and the item count is unchanged", "resets the value when every item is replaced and the item count is unchanged", "keeps the value when the items are only reordered", "resets to default when the selected item is removed from the list", "resets via onValueChange and does not break in controlled mode when the selected item is removed", "falls back to null when both selected and initial default are removed (uncontrolled)", "… (controlled)". Docs: `CHANGELOG.md v1.0.0-beta.3 "Reset state when selected item is removed"`, `v1.2.0 "Avoid clearing selected value if item is not present in items array"`.
- **SEL-STA-13** — MUST. *Item reconciliation (multiple mode).* Under the same preconditions, with an array value, the component MUST request a value change (reason `none`) to the array filtered to the values still present among the registered items, only when that removes at least one entry; when the result is empty the selected index and alignment text element are cleared. Adding items never changes the value.
  Source: `select/positioner/SelectPositioner.tsx:177-194`. Test: [L2] `SelectRoot.test.tsx` "removes selections that no longer exist", "removes selections replaced without changing the item count", "keeps the selection when items are added and none of the selected ones are removed".
- **SEL-STA-14** — MUST. Reconciliation MUST NOT run on the first (initial) publication of the item map, nor while the value registry is empty; a controlled consumer that ignores the reconciliation request keeps its value and no item is marked selected (SEL-STA-03).
  Source: `select/positioner/SelectPositioner.tsx:143-152`. Test: [L2] `SelectRoot.test.tsx` "resets via onValueChange and does not break in controlled mode when the selected item is removed".
- **SEL-STA-28** — MUST. The store's `popupSide` is the anchor-positioning mechanism's resolved side (never `'none'`), updated post-commit, pre-paint; the trigger's `popupSide` state is that value while the popup is mounted and a positioner element exists, else `null`.
  Source: `select/positioner/SelectPositioner.tsx:124-126`, `select/trigger/SelectTrigger.tsx:77,81`. Test: [L2] `SelectTrigger.test.tsx` "sets data-popup-side to the current popup side", "sets data-popup-side to the resolved side when alignItemWithTrigger is active".
- **SEL-STA-29** — MUST. *Scroll-arrow visibility.* Given the scroller (the List element when present, else the popup element), `maxScrollTop = max(0, scrollHeight − clientHeight)` and `scrollTop′ = normalizeScrollOffset(scrollTop, maxScrollTop)` (UTL-DOM-30: snapped to the edges within 1 px), the up arrow is visible iff `scrollTop′ > 0` and the down arrow iff `scrollTop′ < maxScrollTop`. It is recomputed on every scroll of the scroller after placement (SEL-POS-23), after aligned placement, after a scroll-arrow auto-scroll step, and on the next frame after a non-aligned placement; both flags are reset to `false` on unmount and when aligned mode re-places the popup after the item map changes.
  Source: `select/root/SelectRoot.tsx:342-350`, `select/popup/SelectPopup.tsx:98,142,173,250-253,394`, `select/positioner/SelectPositioner.tsx:196-201`, `select/scroll-arrow/SelectScrollArrow.tsx:100`. Test: [L3] `SelectRoot.test.tsx` "normalizes overlapping fractional scroll ranges when toggling scroll arrow visibility", "clears scroll arrow visibility when manually unmounted (strict: %s)"; `SelectScrollArrow.test.tsx` "hides the arrow when an item-less popup is already scrolled to its edge" (L4 for the unmount-reset clause).
- **SEL-STA-30** — MUST. `hasScrollArrows` is `true` while at least one scroll-arrow part is mounted (a mount counter incremented/decremented post-commit, pre-paint, never below zero) and `false` otherwise; it only affects the List's scrollbar class (SEL-DOM-33).
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:48-61`. Test: none.
- **SEL-STA-31** — MUST. The *initial value* used as the reconciliation fallback (SEL-STA-12) is the value present on the Root's first render (`defaultValue` or the initial controlled `value`) and never changes afterwards.
  Source: `select/root/SelectRoot.tsx:132,190`. Test: [L2] `SelectRoot.test.tsx` "resets to default when the selected item is removed from the list".
- **SEL-STA-32** — MUST. A *typing* flag is `true` from the first accepted typeahead character until the typeahead session ends (reset timer, blur outside the composite, no match, open/close reset — CMP-STA-43…45); it suppresses Space activation of items (SEL-KEY-09).
  Source: `select/root/SelectRoot.tsx:123,408-410`, `select/item/SelectItem.tsx:164-168`. Test: [L4] `SelectRoot.test.tsx` "does not trigger selection when Space is pressed during text navigation"; `SelectItem.test.tsx` "should allow pointer click after a typeahead sequence that ends with Space".

State diagram for the popup (details in §9): `closed/unmounted` → (open request accepted) → `open/mounted, 'starting'` → (next frame) → `open/mounted` → (close request accepted) → `closed/mounted, 'ending'` → (animations finished, unless `preventUnmountOnClose()`) → `closed/unmounted` with the unmount cleanup (SEL-ANI-07). A reopen during `'ending'` returns to `open/mounted` without `'starting'` (TRN-STA-09).

Invariants: at most one item is highlighted; in single mode at most one item is selected (several items with equal values would all report selected — SEL-EDGE-08); `selectedIndex` is `null` or a registered index; `forceMount` is monotonic; the popup `open` state never changes without `onOpenChange` having been offered the change, except through the controlled prop.

## 5. Rendered DOM and ARIA

### 5.1 Trigger element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| element | `<button type="button">` by default (UBT-DOM-01); any element via `render` + `nativeButton={false}` (UBT-DOM-02 adds `role="button"`, overridden by SEL-ARIA-01) | SEL-DOM-01 | C# |
| `id` | trigger `id` prop ?? Root `id` | SEL-DOM-09 | C# |
| `role` | `combobox` (always) | SEL-ARIA-01 | C# |
| `aria-expanded` | `"true"` / `"false"` | SEL-ARIA-02 | C# |
| `aria-haspopup` | `"listbox"` | SEL-ARIA-03 | C# |
| `aria-controls` | listbox id while open; absent while closed | SEL-ARIA-04 | C# |
| `aria-labelledby` | field label id ?? `Select.Label` id; absent when neither | SEL-ARIA-05 | C# |
| `aria-readonly` / `aria-required` | `"true"` when set; absent otherwise | SEL-ARIA-06 | C# |
| `aria-describedby` / `aria-invalid` | per FFI-ARIA-01/03 | SEL-ARIA-07 | C# |
| `aria-disabled` / `disabled` | per UBT (native: `disabled` attribute; non-native: `aria-disabled="true"`) | SEL-DOM-07 | C# |
| `tabindex` | `0`; `-1` when disabled (non-native) | SEL-DOM-07 | C# |
| data attributes | see §3.3 | SEL-DOM-02…06 | C# |

- **SEL-DOM-01** — MUST. The trigger's default element is `<button>`; in native mode it carries `type="button"` (UBT-DOM-01).
  Source: `select/trigger/SelectTrigger.tsx:228`. Test: [L2] `SelectTrigger.test.tsx` conformance suite (`refInstanceof: HTMLButtonElement`). Docs: `CHANGELOG.md v1.0.0-beta.5 "Make the trigger native button by default"`.
- **SEL-DOM-02** — MUST. While `open` is `true` the trigger MUST carry both `data-popup-open=""` and `data-pressed=""` (pressable trigger mapping, PSH-DOM-02); neither otherwise.
  Source: `select/trigger/SelectTrigger.tsx:29-30`, `utils/popupStateMapping.ts:13-16,39-46`. Test: [L2] `SelectTrigger.test.tsx` "should have the data-popup-open and data-pressed attributes when open".
- **SEL-DOM-03** — MUST. The trigger MUST carry `data-disabled=""` while effectively disabled and `data-readonly=""` while `readOnly` (default projection of the state fields). `data-required` is declared in the attribute-name export and in the API outline but is **not** emitted in v1.9.0: `required` is not a field of the trigger state object (§16 Q6); a port MUST NOT emit it either.
  Source: `select/trigger/SelectTrigger.tsx:218-226,236-257`, `select/trigger/SelectTriggerDataAttributes.ts:14,18,27`. Test: [L2] `SelectRoot.test.tsx` "sets the disabled state", "sets the readOnly state".
- **SEL-DOM-04** — MUST. The trigger MUST carry `data-placeholder=""` iff no value is selected (SEL-STA-02).
  Source: `select/trigger/SelectTrigger.tsx:225`. Test: [L2] `SelectTrigger.test.tsx` "placeholder state" (all five tests). Docs: `CHANGELOG.md v1.0.0-beta.5 "Add \`data-placeholder\` attribute"`.
- **SEL-DOM-05** — MUST. The trigger MUST carry `data-popup-side="<side>"` with the resolved side (SEL-STA-28) while the popup is mounted and a positioner exists — including while aligned positioning is active, where it reports the side the fallback would use — and no such attribute otherwise.
  Source: `select/trigger/SelectTrigger.tsx:32-33,81`. Test: [L2] `SelectTrigger.test.tsx` "sets data-popup-side to the current popup side", "sets data-popup-side to the resolved side when alignItemWithTrigger is active". Docs: `CHANGELOG.md v1.5.0 "Add \`data-popup-side\` to trigger"`.
- **SEL-DOM-06** — MUST. The trigger MUST carry the field state attributes `data-touched`, `data-dirty`, `data-filled`, `data-focused` (`""` when the flag is `true`) and `data-valid=""` / `data-invalid=""` per FFI-DOM-01; outside a `Field.Root` only `data-filled`'s underlying flag can ever be true (it is not, since the default scope ignores `setFilled`) — i.e. none of these appear.
  Source: `select/trigger/SelectTrigger.tsx:31,219`, `internals/field-constants/constants.ts` (`fieldValidityMapping`). Test: [L2] `SelectRoot.test.tsx` "[data-touched]", "[data-dirty]", "[data-filled]", "[data-focused]".
- **SEL-DOM-07** — MUST. The trigger renders `tabindex="0"` while enabled and `tabindex="-1"` while disabled, supplied as an element attribute (so it applies in native and non-native mode alike, UBT-DOM-09); in native mode the `disabled` attribute is also present while disabled (UBT-DOM-06), in non-native mode `aria-disabled="true"` (UBT-ARIA-02).
  Source: `select/trigger/SelectTrigger.tsx:144,93-96`. Test: [L2] `SelectTrigger.test.tsx` "is removed from the tab order when disabled and not rendered as a button", "cannot be focused when disabled"; `SelectRoot.test.tsx` "sets the disabled state".
- **SEL-DOM-08** — MUST. The `value` state field MUST NOT be projected to a data attribute on the trigger or on `Select.Value`.
  Source: `select/trigger/SelectTrigger.tsx:34`, `select/value/SelectValue.tsx:9-11`. Test: none.
- **SEL-DOM-09** — MUST. The trigger's `id` is its `id` prop when given, else the Root's effective id (SEL-API-10); it is applied by the trigger itself (not through the shared interaction attributes).
  Source: `select/trigger/SelectTrigger.tsx:83,134`, `select/root/SelectRoot.tsx:413-422`. Test: [L2] `SelectRoot.test.tsx` "sets the id on the trigger".

### 5.2 Value element

- **SEL-DOM-10** — MUST. `Select.Value` renders a `<span>` whose content is resolved in this order: (1) `children` function → its result; (2) non-nullish `children` → as-is; (3) placeholder per SEL-API-30; (4) multiple mode (array value) → the labels of each value joined by the literal `", "` (UTL `resolveMultipleLabels`); (5) otherwise UTL `resolveSelectedLabel(value, items, itemToStringLabel)`: `itemToStringLabel(value)` for a non-null value when the function is given; else an object value's own non-null `label`; else the record entry `items[value]` (own property) or the matching `{ value, label }` entry of a flat or grouped array (matched by `===` on the value, or on `value.value` for an object without a label); else the fallback string `stringifyAsLabel(value)` (object `label` → `value` → `serializeValue`: `''` for nullish, the string itself, else JSON).
  Source: `select/value/SelectValue.tsx:47-58`, `internals/resolveValueLabel.tsx:68-81,93-158`. Test: [L2] `SelectValue.test.tsx` (all tests); `SelectRoot.test.tsx` "updates <Select.Value /> label when the value prop changes before the popup opens".
- **SEL-DOM-11** — MUST. The Value element MUST carry `data-placeholder=""` iff no value is selected (SEL-STA-02), regardless of whether a placeholder is actually rendered.
  Source: `select/value/SelectValue.tsx:44`. Test: [L2] `SelectTrigger.test.tsx` "placeholder state" (all five tests).
- **SEL-DOM-12** — MUST. The Value element is the *value text reference* for aligned positioning: its bounding box is what the selected item's text is aligned to (SEL-POS-07).
  Source: `select/value/SelectValue.tsx:62`, `select/popup/SelectPopup.tsx:271,308-319`. Test: [L4] `SelectPopup.test.tsx` "aligns the selected item with the trigger inline start in ltr".

### 5.3 Icon element

- **SEL-DOM-13** — MUST. `Select.Icon` renders a `<span aria-hidden="true">` containing `▼` by default and carries `data-popup-open=""` while open (trigger mapping, PSH-DOM-01).
  Source: `select/icon/SelectIcon.tsx:27-32`, `select/icon/SelectIconDataAttributes.ts:6`. Test: [L2] `SelectIcon.test.tsx` conformance suite. Docs: `CHANGELOG.md v1.0.0-beta.4 "Add \`open\` state for \`Select.Icon\`"`.

### 5.4 Label element

- **SEL-DOM-14** — MUST. `Select.Label` renders a `<div>` whose `id` is the registered label id — `${rootId}-label` by default (FFI-ARIA-07) — and publishes that id to the Root (for SEL-ARIA-05) and to the labelable scope; it carries the field state attributes of FFI-DOM-01.
  Source: `select/label/SelectLabel.tsx:30-49`, `utils/resolveAriaLabelledBy.ts:3-5`. Test: [L2] `SelectRoot.test.tsx` "Select.Label", "updates Select.Label linkage when root id changes", "does not link Select.Label before hydration".
- **SEL-DOM-15** — MUST. The Label behaves as a non-native label (FFI-PTR-01): `pointerdown` is default-prevented and `click` focuses the control (the trigger element when attached, looked up by id with `focus({ focusVisible: true })`) without opening the popup; clicks whose target is a `button`/`input`/`select`/`textarea` descendant are ignored; a double click is default-prevented.
  Source: `select/label/SelectLabel.tsx:34-42`, `internals/labelable-provider/useLabel.ts:30-76`. Test: [L4] `SelectRoot.test.tsx` "Select.Label focuses trigger without opening". Docs: `components/select/page.mdx` §Labeling a select; `CHANGELOG.md v1.9.0 "Fix label focus in Shadow DOM"`.

### 5.5 Portal element

- **SEL-DOM-16** — MUST. When rendered (SEL-STR-06), `Select.Portal` renders the shared floating portal's `<div>` appended to `container` (default: the document body) with the forwarded attributes; the positioner subtree is its content (FLT).
  Source: `select/portal/SelectPortal.tsx:27`. Test: [L2] `SelectPortal.test.tsx` conformance suite.

### 5.6 Backdrop element

- **SEL-DOM-17** — MUST. `Select.Backdrop` renders `<div role="presentation">` with inline `user-select: none; -webkit-user-select: none`, the `hidden` attribute while the popup is not mounted, `data-open=""`/`data-closed=""` (PSH-DOM-03) and the transition attributes (TRN-DOM-01…03).
  Source: `select/backdrop/SelectBackdrop.tsx:11-14,39-54`. Test: [L2] `SelectBackdrop.test.tsx` conformance suite (L4 for the transition-attribute clause).

### 5.7 Positioner element

- **SEL-DOM-18** — MUST. `Select.Positioner` renders `<div role="presentation">` (shared positioner host, FLT `usePositioner`) with the `hidden` attribute while not mounted and inline `pointer-events: none` while not open (so a closing popup ignores pointer input).
  Source: `select/positioner/SelectPositioner.tsx:130-137`, `utils/usePositioner.tsx:21-42`. Test: [L2] `SelectPositioner.test.tsx` "does not retain stale coordinates while closed" (`hidden`); `SelectItem.test.tsx` "should select the item and close popup when clicked" (not visible after close) (L4 for the `pointer-events` clause).
- **SEL-DOM-19** — MUST. The positioner carries `data-open`/`data-closed`, `data-anchor-hidden` (PSH-DOM-03/04), `data-side` (SEL-STA-20: `"none"` while aligned) and `data-align` (FLT). It carries no transition-status attributes (its state has none).
  Source: `select/positioner/SelectPositioner.tsx:117-122`, `select/positioner/SelectPositionerDataAttributes.ts:1-24`. Test: [L4] `SelectPopup.test.tsx` "keeps alignItemWithTrigger active at browser zoom".
- **SEL-DOM-20** — MUST. While aligned positioning is active the positioner's built-in inline style is exactly `position: fixed` (the coordinates/size are written imperatively, SEL-POS-07); otherwise it is the anchor-positioning mechanism's style set (FLT: `position`, `top`/`left`, `transform`, the CSS variables, `opacity: 0` until positioned). A consumer `style` merges over it (RND-DOM-16).
  Source: `select/positioner/SelectPositioner.tsx:25,115,131`. Test: [L4] `SelectRoot.test.tsx` "keeps touch positioning during the close transition" (`position: absolute` when opened by touch); `SelectPopup.test.tsx` "aligned popup scroll resizing" (inline `top`/`bottom`/`height`).
- **SEL-DOM-21** — MUST. While `transitionStatus` is `'starting'` the positioner carries inline `transition: none` (TRN-DOM-05).
  Source: `utils/usePositioner.tsx:37`, `select/positioner/SelectPositioner.tsx:132`. Test: none.
- **SEL-DOM-22** — MUST. The positioner carries the FLT CSS variables `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height` and `--transform-origin`; because the anchor is measured even in aligned mode (the mechanism's first pass runs before alignment overrides the coordinates), `--anchor-width` reflects the custom `anchor` when one is given.
  Source: `select/positioner/SelectPositionerCssVars.ts:1-25`, `select/popup/SelectPopup.tsx:230-242`. Test: [L4] `SelectPopup.test.tsx` "seeds anchor width from the custom anchor on first open".

### 5.8 Popup element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `listbox` when no `Select.List` is rendered; `presentation` otherwise | SEL-DOM-23/24 | C# |
| `id` | `${rootId}-list` when no List | SEL-DOM-23 | C# |
| `aria-multiselectable` | `"true"` in multiple mode, no List | SEL-DOM-23 | C# |
| `aria-readonly` | `"true"` when readOnly, no List | SEL-DOM-23 | C# |
| `tabindex` | `-1` | SEL-DOM-25 | C# |
| `data-base-ui-focusable` | `""` | SEL-DOM-25 | C# |
| data attributes | `data-open`/`data-closed`, `data-starting-style`/`data-ending-style`, `data-side`, `data-align` | SEL-DOM-26 | C#; JS for `data-starting-style`/`data-ending-style` |
| inline style | aligned mode: `height: 100%` (with List) or `position: relative; max-height: 100%; overflow-x: hidden; overflow-y: auto` (without List); `transition: none` while starting | SEL-DOM-27/30 | C# → JS |
| class | `base-ui-disable-scrollbar` when aligned and no List | SEL-DOM-28 | C# |
| `--transform-origin` | inline, aligned mode | SEL-DOM-29 | JS |

- **SEL-DOM-23** — MUST. When no `Select.List` element is registered, the Popup MUST carry `role="listbox"`, `id="${rootId}-list"`, `aria-multiselectable="true"` iff `multiple`, and `aria-readonly="true"` iff `readOnly`; the trigger's `aria-controls` then references this id.
  Source: `select/popup/SelectPopup.tsx:435-445`. Test: [L2] `SelectPopup.test.tsx` "has aria attributes when no Select.List is present"; `SelectRoot.test.tsx` "opens the popup when clicked" (readOnly), "should open the select by default" (no `aria-orientation`).
- **SEL-DOM-24** — MUST. When a `Select.List` is registered, the Popup MUST carry `role="presentation"` and none of the listbox attributes.
  Source: `select/popup/SelectPopup.tsx:436-439`. Test: [L2] `SelectPopup.test.tsx` "places aria attributes on Select.List instead if it is present".
- **SEL-DOM-25** — MUST. The Popup carries `tabindex="-1"` and `data-base-ui-focusable=""` (PSH-DOM-15), making it the popup focus element (CMP-DOM-05).
  Source: `select/root/SelectRoot.tsx:432-441`, `utils/popups/popupStoreUtils.ts:27-30`. Test: [L2] `SelectRoot.test.tsx` "does not carry an ArrowUp opening over to a later pointer opening" (listbox receives focus).
- **SEL-DOM-26** — MUST. The Popup carries `data-open`/`data-closed` (PSH-DOM-03), `data-starting-style`/`data-ending-style` (TRN-DOM-01…03), `data-side` (`"none"` while aligned) and `data-align`; its state has no `anchorHidden`, so `data-anchor-hidden` is never emitted on it.
  Source: `select/popup/SelectPopup.tsx:36-39,188-193`, `select/popup/SelectPopupDataAttributes.ts:1-28`. Test: [L4] `SelectRoot.test.tsx` "aligns the first selected item with the trigger when alignItemWithTrigger is active" (`data-side="none"` on the popup), "keeps the highlight on the item while the popup animates out" (`data-ending-style`).
- **SEL-DOM-27** — MUST. While aligned positioning is active the Popup's built-in inline style is `height: 100%` when a List is registered, and otherwise `position: relative; max-height: 100%; overflow-x: hidden; overflow-y: auto` (so the popup itself scrolls); outside aligned mode no built-in layout style is applied.
  Source: `select/popup/SelectPopup.tsx:457-459`, `select/popup/utils.ts:7-12`. Test: [L4] `SelectPopup.test.tsx` "grows the popup when it is itself the scroll container".
- **SEL-DOM-28** — MUST. While aligned positioning is active and no List is registered, the Popup MUST carry the class `base-ui-disable-scrollbar`. Independently of that condition, the Popup part MUST always emit the shared scrollbar-hiding `<style>` element (RND-DOM-35 / UTL-DOM-32) immediately before its element — unless `CSPProvider.disableStyleElements` is set (UTL-DOM-33) — with the CSP nonce; the List relies on this element for its own use of the class (SEL-DOM-33).
  Source: `select/popup/SelectPopup.tsx:68,460-461,478`, `utils/styles.tsx:1-12`. Test: none.
- **SEL-DOM-29** — MUST. In aligned mode, after placement, the Popup element's inline `--transform-origin` MUST be `50% <Y>%` where `<Y>` is the selected (or first) item text's vertical center as a percentage of the positioner's measured height, clamped to `[0, 100]`, or `50` when the positioner has no measurable height; the property is removed from the popup at the start of every placement and whenever no text element is available.
  Source: `select/popup/SelectPopup.tsx:245,376-388`. Test: [L4] `SelectPopup.test.tsx` "centers the transform origin when the positioner has no measurable height". Docs: `CHANGELOG.md v1.0.0-beta.3 "Fix \`transform-origin\` variable calculation…"`.
- **SEL-DOM-30** — MUST. While `transitionStatus` is `'starting'` the Popup carries inline `transition: none` (TRN-DOM-05).
  Source: `select/popup/SelectPopup.tsx:471`. Test: none.

### 5.9 List element

- **SEL-DOM-31** — MUST. `Select.List` renders `<div id="${rootId}-list" role="listbox">` with `aria-multiselectable="true"` iff `multiple` and `aria-readonly="true"` iff `readOnly`, and no `aria-orientation`.
  Source: `select/list/SelectList.tsx:31-35`. Test: [L2] `SelectPopup.test.tsx` "places aria attributes on Select.List instead if it is present"; `SelectRoot.test.tsx` "marks the listbox as readonly when rendered with Select.List".
- **SEL-DOM-32** — MUST. While aligned positioning is active the List's built-in inline style is `position: relative; max-height: 100%; overflow-x: hidden; overflow-y: auto`; otherwise none.
  Source: `select/list/SelectList.tsx:39-41`, `select/popup/utils.ts:7-12`. Test: none.
- **SEL-DOM-33** — MUST. The List MUST carry the class `base-ui-disable-scrollbar` iff at least one scroll arrow is mounted (SEL-STA-30) and the popup was not opened by touch; the `<style>` rule itself is emitted by the Popup (SEL-DOM-28).
  Source: `select/list/SelectList.tsx:42-43`. Test: none. Docs: `CHANGELOG.md v1.8.0 "Hide group labels and scrollbars from the accessibility tree"`.

### 5.10 Item element

- **SEL-DOM-34** — MUST. `Select.Item` renders `<div role="option">` (the button mechanism's `role="button"` is overridden, UBT-DOM-09) with `aria-selected="true"`/`"false"` (SEL-ARIA-09).
  Source: `select/item/SelectItem.tsx:158-159,239-243`. Test: [L2] `SelectItem.test.tsx` conformance suite; `SelectPopup.test.tsx` "places aria attributes on Select.List instead if it is present" (options found by role).
- **SEL-DOM-35** — MUST. The Item carries `data-selected=""` while selected, `data-highlighted=""` while highlighted and `data-disabled=""` while disabled (default projection), and nothing for the `false` states.
  Source: `select/item/SelectItem.tsx:127-131`, `select/item/SelectItemDataAttributes.ts:1-12`. Test: [L2] `SelectItem.test.tsx` "should apply data-highlighted attribute when item is highlighted", "should apply data-selected attribute when item is selected", "inherits the disabled state from the root".
- **SEL-DOM-36** — MUST. The Item renders `tabindex="0"` iff the popup is open and the item is highlighted, and `tabindex="-1"` otherwise (including every item while the popup is closed and kept in the DOM); the button mechanism emits no `tabindex` in composite mode (UBT-DOM-05).
  Source: `select/item/SelectItem.tsx:160`. Test: [L2] `SelectRoot.test.tsx` "does not leave a tabbable option while closed and kept mounted after tabbing out".
- **SEL-DOM-37** — MUST. A disabled item carries `aria-disabled="true"` (non-native, UBT-ARIA-02; in native mode with `focusableWhenDisabled: true`, `aria-disabled` per UBT-ARIA-01 and never the `disabled` attribute, UBT-DOM-07).
  Source: `select/item/SelectItem.tsx:120-125`. Test: [L2] `SelectItem.test.tsx` "should focus disabled items".

### 5.11 ItemText element

- **SEL-DOM-38** — MUST. `Select.ItemText` renders a `<div>` with no built-in attributes; the element is recorded as the item's text element (SEL-API-42), as the *first-item text reference* when its item's index is `0`, and as the *selected text reference* when its item currently holds the selected index (SEL-STA-04…06).
  Source: `select/item-text/SelectItemText.tsx:24-43`. Test: [L4] `SelectPopup.test.tsx` "keeps item text aligned with the trigger after reopening".

### 5.12 ItemIndicator element

- **SEL-DOM-39** — MUST. `Select.ItemIndicator` renders the shared indicator: `<span aria-hidden="true">` with default content `✔️`, `data-selected=""` while selected, the transition attributes, and no `selected` attribute (RND-DOM-50…52, RND-ARIA-01).
  Source: `select/item-indicator/SelectItemIndicator.tsx:25`, `utils/ItemIndicator.tsx:16-39`. Test: [L2] `SelectItemIndicator.test.tsx` "mounts only while selected when keepMounted is false" (L4 for the transition-attribute clause).

### 5.13 Arrow element

- **SEL-DOM-40** — MUST. `Select.Arrow` renders `<div aria-hidden="true">` with the mechanism's arrow position styles inline (FLT), `data-open`/`data-closed`, `data-side` (`"none"` is impossible here because the part renders nothing while aligned), `data-align` and `data-uncentered=""` while the arrow cannot be centered; it never carries `data-anchor-hidden` or transition attributes (not in its state).
  Source: `select/arrow/SelectArrow.tsx:28-40`, `select/arrow/SelectArrowDataAttributes.ts:1-24`. Test: [L4] `SelectArrow.test.tsx` conformance suite.

### 5.14 Group and GroupLabel elements

- **SEL-DOM-41** — MUST. `Select.Group` renders `<div role="group">` with `aria-labelledby` equal to its GroupLabel's id while a GroupLabel is mounted, and no `aria-labelledby` otherwise.
  Source: `select/group/SelectGroup.tsx:19-38`. Test: [L2] `SelectGroup.test.tsx` "should render group with label", "should associate label with group"; `SelectGroupLabel.test.tsx` "removes the group aria-labelledby attribute when unmounted".
- **SEL-DOM-42** — MUST. `Select.GroupLabel` renders `<div id="…" aria-hidden="true">`; a consumer-supplied `aria-hidden` (including an explicit `undefined`) replaces the default (RND-DOM-19). Port: relaxed (ADR 02, RND-DOM-19/32).
  Source: `select/group-label/SelectGroupLabel.tsx:32-35`. Test: [L2] `SelectGroupLabel.test.tsx` "is hidden from the accessibility tree by default", "allows overriding aria-hidden".

### 5.15 Scroll-arrow elements

- **SEL-DOM-43** — MUST. Each scroll arrow renders `<div aria-hidden="true">` with inline `position: absolute` and default content `▲` (up) / `▼` (down).
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:80-85`. Test: [L2] `SelectRoot.test.tsx` "normalizes overlapping fractional scroll ranges…" (found by `▲`/`▼`).
- **SEL-DOM-44** — MUST. Each scroll arrow carries `data-direction="up"`/`"down"`, `data-visible=""` while visible (SEL-STA-25), and `data-side` as the Positioner's.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:73-78`. Test: [L4] `SelectRoot.test.tsx` "clears scroll arrow visibility when manually unmounted (strict: %s)"; `SelectScrollArrow.test.tsx` "reflects scrollability on the %s arrow when the popup has no registered items".
- **SEL-DOM-45** — MUST. Each scroll arrow carries `data-starting-style`/`data-ending-style` from its own transition status (TRN §6.1 row "Select.ScrollUpArrow / ScrollDownArrow": `open` = visible), and while kept mounted but not visible it carries neither attribute nor any `hidden`/inline hiding style (TRN-DOM-07).
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:46,63-71,142`. Test: [L4] `SelectRoot.test.tsx` "keeps a scroll arrow mounted while its exit animation runs".

### 5.16 Separator element

- **SEL-DOM-46** — MUST. `Select.Separator` renders `<div role="presentation" data-orientation="horizontal|vertical">` (RND-DOM-53, RND-ARIA-02).
  Source: `utils/listbox-separator/ListboxSeparator.tsx:20-24`. Test: [L2] `SelectRoot.test.tsx` "exposes the orientation attribute rendered by Separator". Docs: `CHANGELOG.md v1.7.0 "Fix listbox separator semantics"`.

### 5.17 Hidden form-participation `<input>`

| Attribute | Value | Req | Writer |
|---|---|---|---|
| `type` | (default `text`) | SEL-DOM-47 | C# |
| `aria-hidden` | `"true"` | SEL-DOM-47 | C# |
| `tabindex` | `-1` | SEL-DOM-47 | C# |
| `id` | `${rootId}-hidden-input` when the input has no `name`; absent otherwise | SEL-DOM-48 | C# |
| `name` | effective name in single mode; absent in multiple mode | SEL-DOM-47 | C# |
| `form`, `autocomplete` | from props | SEL-API-08/09 | C# |
| `value` | serialized value (single); `""` (multiple) | SEL-DOM-51 | C# |
| `disabled`, `readonly` | from effective disabled / `readOnly` | SEL-DOM-47 | C# |
| `required` | `required && !(multiple && hasSelectedValue)` | SEL-DOM-50 | C# |
| `aria-describedby`, `aria-invalid` | FFI-ARIA-03 | SEL-ARIA-07 | C# |
| inline style | visually hidden (absolute when named, fixed otherwise) | SEL-DOM-49 | C# |

- **SEL-DOM-47** — MUST. The Root renders exactly one native `<input>` carrying `aria-hidden="true"`, `tabindex="-1"`, the effective `name` only in single mode, `form`, `autocomplete`, `disabled` iff effectively disabled, `readonly` iff `readOnly`, the validation attributes (SEL-ARIA-07) and the handlers of SEL-FOC-06 and SEL-FRM-18. It is the field's validation input (FFI §6.3 Table A row `Select.Root`).
  Source: `select/root/SelectRoot.tsx:516-583`. Test: [L2] `SelectRoot.test.tsx` "should handle browser autofill", "should pass autoComplete to the hidden input", "ignores hidden-input autofill when $lockState $label".
- **SEL-DOM-48** — MUST. The hidden input carries `id="${rootId}-hidden-input"` iff it has no `name` (nameless single mode or multiple mode) and the Root has an id; a named hidden input has no `id` (FFI-DOM-03).
  Source: `select/root/SelectRoot.tsx:489,570`. Test: [L2] `SelectRoot.test.tsx` "sets a hidden input id when name is not provided", "does not set a hidden input id when name is provided". Docs: `CHANGELOG.md v1.3.0 "Fix hidden input \`id\` fallback"`.
- **SEL-DOM-49** — MUST. The hidden input's inline style is UTL-DOM-14 `visuallyHiddenInput` (absolute) when an effective `name` exists and UTL-DOM-13 `visuallyHidden` (fixed at the viewport origin) otherwise (FFI-DOM-02: an absolutely positioned input anchors the browser's validation bubble next to the control).
  Source: `select/root/SelectRoot.tsx:579`, `packages/utils/src/visuallyHidden.ts:14-25`. Test: none.
- **SEL-DOM-50** — MUST. The hidden input carries `required` iff the `required` prop is `true` and not (multiple mode with a non-empty selection).
  Source: `select/root/SelectRoot.tsx:576`. Test: [L2] `SelectRoot.test.tsx` "does not mark the hidden input as required when selection exists", "keeps the hidden input required when no selection exists". Docs: `CHANGELOG.md v1.0.0-beta.5 "Fix \`required\` form submission with multiple values"`.
- **SEL-DOM-51** — MUST. In single mode the hidden input's `value` is `stringifyAsValue(value, itemToStringValue)` (UTL): `''` for nullish; `itemToStringValue(value) ?? ''` when the function is given; the serialized `value` member of a `{ value, label }` object; else `serializeValue` (string as-is, otherwise JSON). In multiple mode it is always `""`.
  Source: `select/root/SelectRoot.tsx:225-233,574`, `internals/resolveValueLabel.tsx:83-91`. Test: [L2] `SelectRoot.test.tsx` "uses itemToStringValue for form submission", "should serialize empty array as empty string in multiple mode"; `SelectValue.test.tsx` "falls back to label/value properties when functions are not provided".
- **SEL-DOM-52** — MUST. In multiple mode the shared hidden input is nameless (so it never submits and `itemToStringValue` is never invoked with the array).
  Source: `select/root/SelectRoot.tsx:226-230,489`. Test: [L2] `SelectRoot.test.tsx` "does not invoke itemToStringValue with the value array in multiple mode".

### 5.18 Per-value hidden inputs (multiple mode)

- **SEL-DOM-53** — MUST. In multiple mode with an array value and an effective name, the Root renders after the shared input one `<input type="hidden" name="<name>" value="<stringifyAsValue(v)>" form="<form>" disabled?>` per selected value, in value order, keyed by the serialized value; with an empty selection or no name none is rendered (FFI-DOM-06).
  Source: `select/root/SelectRoot.tsx:491-509,584`. Test: [L2] `SelectRoot.test.tsx` "should serialize multiple values correctly for form submission", "uses itemToStringValue for multiple selection form submission", "submits multiple values to an external form when `form` is provided", "does not submit multiple values when disabled".

### 5.19 Internal backdrop

- **SEL-DOM-54** — MUST. While mounted and `modal`, the internal backdrop (PSH-DOM-14, PSH-PTR-05/06) is rendered with `cutout` = the trigger element (so the trigger stays clickable through it) and the `inert` attribute while the popup is not open.
  Source: `select/positioner/SelectPositioner.tsx:228`, `utils/InternalBackdrop.tsx:6-35`. Test: [L2] `SelectRoot.test.tsx` "should render an internal backdrop when `true`". Docs: `CHANGELOG.md v1.0.0-beta.1 "Cut out internal backdrop to allow interacting with triggers"`.

### 5.20 Conformance

- **SEL-DOM-55** — MUST. Every part MUST satisfy RND-DOM-36…48 (and RND-DOM-49 for Trigger and Item, which are run with `button: true`); the options each suite passes are listed in §13.
  Source: `test/describeConformance.tsx`. Test: [L2] every part's "Base UI component API" suite.

### 5.21 ARIA

- **SEL-ARIA-01** — MUST. The trigger's `role` MUST be `combobox`, applied *after* every other attribute source so that a consumer `role` or a wrapping button part (e.g. `Toolbar.Button` rendering a `Select.Trigger`) cannot change it.
  Source: `select/trigger/SelectTrigger.tsx:135,214-216`. Test: [L2] `SelectPopup.test.tsx` "keeps arrow keys inside the popup when the trigger lives in a toolbar"; conformance suite "opens on trigger click and sets `aria-expanded` on the trigger".
- **SEL-ARIA-02** — MUST. The trigger carries `aria-expanded="true"` while open and `"false"` otherwise (PSH-ARIA-01).
  Source: `select/trigger/SelectTrigger.tsx:136`. Test: [L2] `SelectPopup.test.tsx` "has aria attributes when no Select.List is present".
- **SEL-ARIA-03** — MUST. The trigger carries `aria-haspopup="listbox"` at all times (PSH-ARIA-04).
  Source: `select/trigger/SelectTrigger.tsx:137`. Test: [L2] `SelectPopup.test.tsx` "places aria attributes on Select.List instead if it is present".
- **SEL-ARIA-04** — MUST. While open the trigger carries `aria-controls` equal to the List element's `id` when a List exists, else the popup focus element's `id` (the Popup, SEL-DOM-23); while closed the attribute is absent.
  Source: `select/trigger/SelectTrigger.tsx:138-140`. Test: [L2] `SelectPopup.test.tsx` "has aria attributes when no Select.List is present", "places aria attributes on Select.List instead if it is present".
- **SEL-ARIA-05** — MUST. The trigger's `aria-labelledby` is the enclosing field's label id when one is registered, else the `Select.Label` id when one is mounted, else absent (FFI-ARIA-07 `resolveAriaLabelledBy`); no native-label discovery fallback is performed. A consumer-supplied `aria-labelledby` replaces it; `aria-label` is forwarded unchanged.
  Source: `select/trigger/SelectTrigger.tsx:64,79,84,141`, `utils/resolveAriaLabelledBy.ts:7-12`. Test: [L2] `SelectRoot.test.tsx` "Field.Label", "Select.Label", "does not set fallback aria-labelledby when no label is rendered".
- **SEL-ARIA-06** — MUST. The trigger carries `aria-readonly="true"` iff `readOnly` and `aria-required="true"` iff `required`; neither attribute is present with a `false` value.
  Source: `select/trigger/SelectTrigger.tsx:142-143`. Test: [L2] `SelectRoot.test.tsx` "sets the readOnly state"; `SelectTrigger.test.tsx` "sets aria-required attribute when required".
- **SEL-ARIA-07** — MUST. Both the trigger and the hidden input receive the field's validation attributes (FFI-ARIA-03): `aria-describedby` composed from the consumer value and the field message ids (FFI-ARIA-01), and `aria-invalid="true"` while the field is invalid and the control not disabled.
  Source: `select/trigger/SelectTrigger.tsx:212`, `select/root/SelectRoot.tsx:517`. Test: [L2] `SelectRoot.test.tsx` "Field.Description", "clears external errors on change", "prop: validate".
- **SEL-ARIA-08** — MUST. Exactly one element carries `role="listbox"` (the List, else the Popup) with `aria-multiselectable="true"` iff `multiple` and `aria-readonly="true"` iff `readOnly`; no element carries `aria-orientation` (the listbox is implicitly vertical).
  Source: `select/popup/SelectPopup.tsx:440-445`, `select/list/SelectList.tsx:32-35`. Test: [L2] `SelectRoot.test.tsx` "should open the select by default"; `SelectPopup.test.tsx` "has aria attributes when no Select.List is present". Docs: `CHANGELOG.md v1.8.0 "Move \`aria-orientation\` to the role owners"`.
- **SEL-ARIA-09** — MUST. Each item carries `role="option"` and `aria-selected` equal to the string `"true"`/`"false"` of its selected state (never absent).
  Source: `select/item/SelectItem.tsx:158-159`. Test: [L2] `SelectItem.test.tsx` "treats a null value as an empty selection in multiple mode" (`aria-selected="false"`); `SelectRoot.test.tsx` "keeps the highlight on an item deselected with the keyboard".
- **SEL-ARIA-10** — MUST. A disabled item carries `aria-disabled="true"` (SEL-DOM-37); an enabled non-native item carries no `aria-disabled`.
  Source: `select/item/SelectItem.tsx:120-125`. Test: none.
- **SEL-ARIA-11** — MUST. `Select.Group` carries `role="group"` and `aria-labelledby` (SEL-DOM-41).
  Source: `select/group/SelectGroup.tsx:33-34`. Test: [L2] `SelectGroup.test.tsx` "should associate label with group".
- **SEL-ARIA-12** — MUST. `Select.GroupLabel` is `aria-hidden="true"` by default (SEL-DOM-42); the group's accessible name is still computed from it through `aria-labelledby`.
  Source: `select/group-label/SelectGroupLabel.tsx:34`. Test: [L2] `SelectGroupLabel.test.tsx` "is hidden from the accessibility tree by default".
- **SEL-ARIA-13** — MUST. `Select.Icon`, `Select.Arrow`, both scroll arrows, `Select.ItemIndicator` and the hidden input carry `aria-hidden="true"`.
  Source: `select/icon/SelectIcon.tsx:30`, `select/arrow/SelectArrow.tsx:38`, `select/scroll-arrow/SelectScrollArrow.tsx:81`, `utils/ItemIndicator.tsx:30-33`, `select/root/SelectRoot.tsx:581`. Test: [L2] `SelectItemIndicator.test.tsx` "mounts only while selected when keepMounted is false".
- **SEL-ARIA-14** — MUST. Select uses the DOM-focus model (CMP: `virtual: false`): the highlighted item receives real focus and no element carries `aria-activedescendant`.
  Source: `select/root/SelectRoot.tsx:371-386` (no `virtual` option). Test: [L4] `SelectItem.test.tsx` "navigating with keyboard should focus item".
- **SEL-ARIA-15** — MUST. `Select.Positioner`, `Select.Backdrop`, the internal backdrop, `Select.Separator` and a Popup wrapping a List carry `role="presentation"`.
  Source: `utils/usePositioner.tsx:36`, `select/backdrop/SelectBackdrop.tsx:44`, `utils/InternalBackdrop.tsx:21`, `utils/listbox-separator/ListboxSeparator.tsx:23`, `select/popup/SelectPopup.tsx:438`. Test: [L2] `SelectRoot.test.tsx` "should render an internal backdrop when `true`", "exposes the orientation attribute rendered by Separator".

## 6. Interactions

Select wires the trigger as the list-navigation *reference/trigger* element, the Popup as the *floating* element and each Item as an *item* (CMP §4.6–4.9), with these options: `enabled: !disabled`, `activeIndex`/`selectedIndex` from the store, `disabledIndices: []` (so `aria-disabled` items are navigable and count as list ends, while the opening highlight still skips them; SEL-KEY-20), `focusItemOnHover: highlightItemOnHover`, every other option at its default (`orientation: 'vertical'`, `loopFocus: false`, `virtual: false`, `focusItemOnOpen: 'auto'`, `openOnArrowKeyDown: true`, `allowEscape: false`, `nested: false`, `rtl: false`, `resetOnPointerLeave: true`, no grid). Typeahead (CMP §4.9) is attached to the same two elements with `enabled: !disabled && (open || (!readOnly && !multiple))`, the labels registry, `disabledIndices: (i) => isElementDisabled(items[i])` (UTL-DOM-17, attribute-only so that hidden force-mounted items are still matched), `resetMs` default 750 ms (CMP-STA-43; Menu's 500 ms `TYPEAHEAD_RESET_MS` is not applied), and `onMatch` / `onTyping` per SEL-KEY-04/12/SEL-STA-32. The trigger-press interaction is the shared click interaction with `enabled: !disabled`, `event: 'mousedown'`, `toggle: true` (default) (FLT-PTR-02, FLT-PTR-03); dismissal is the shared dismiss interaction with every default (`escapeKey: true`, `outsidePress: true`, `outsidePressEvent: 'sloppy'`, Escape not bubbling, outside press bubbling) (FLT-API-01, FLT-API-02, FLT-KEY-01, FLT-DIS-11); the Popup's focus manager is `modal={false}`, `restoreFocus`, `returnFocus={finalFocus}`, `initialFocus` and `closeOnFocusOut` at their defaults (SEL-FOC-02/04/05/12).

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>ArrowDown</kbd> | trigger focused, closed, enabled | Open (reason `list-navigation`); highlight and focus the selected item (multiple: the anchor), else the first item. Default prevented. | SEL-KEY-01 |
| <kbd>ArrowUp</kbd> | trigger focused, closed | Open (`list-navigation`); highlight the selected item, else the last item. | SEL-KEY-02 |
| <kbd>Enter</kbd>, <kbd>Space</kbd> | trigger focused, closed | Native button activation → open (reason `trigger-press`); highlight the selected item, else the first; no item is activated. | SEL-KEY-03 |
| printable character (no Ctrl/Meta/Alt) | trigger focused, closed, single mode, not `readOnly` | Typeahead selects the first matching enabled item (reason `none`) without opening. | SEL-KEY-04 |
| <kbd>Home</kbd>, <kbd>End</kbd>, <kbd>Escape</kbd>, <kbd>Tab</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>PageUp</kbd>, <kbd>PageDown</kbd> | trigger focused, closed | Not intercepted (native behavior). | SEL-KEY-05 |
| <kbd>ArrowDown</kbd> / <kbd>ArrowUp</kbd> | open, focus on an item or the popup | Move the highlight (and focus) to the next / previous item; stop at the ends (no wrap); disabled items are not skipped; from the popup container, go to the first / last item. Default prevented, propagation stopped. | SEL-KEY-06 |
| <kbd>Home</kbd> / <kbd>End</kbd> | open | Highlight the first / last item (disabled ends included). | SEL-KEY-07 |
| <kbd>Enter</kbd> | open, item focused | Activate the item: single → select and close (`item-press`); multiple → toggle; disabled → nothing; `readOnly` → nothing. | SEL-KEY-08 |
| <kbd>Space</kbd> | open, item focused | Same as Enter (activation on keydown), unless a typeahead session is in progress, in which case Space extends the search and activates nothing. | SEL-KEY-09 |
| <kbd>Escape</kbd> | open | Close (`escape-key`); default prevented; propagation stopped (nested popups stay open). | SEL-KEY-10 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | open | Close (`focus-out`) without committing; focus moves to the next / previous tabbable outside the popup (Shift+Tab: the trigger). | SEL-KEY-11 |
| printable character | open | Typeahead moves the highlight (and focus) to the matching enabled item; also in `readOnly` and multiple mode. | SEL-KEY-12 |
| any key | item focused | The item becomes the highlighted item before other handling. | SEL-KEY-13 |
| <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>Home</kbd>, <kbd>End</kbd>, <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd> | open, Select rendered inside a `Toolbar.Root` | Propagation stopped at the popup so the toolbar's roving focus does not move. | SEL-KEY-14 |
| <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>PageUp</kbd>, <kbd>PageDown</kbd> | open | Not intercepted. | SEL-KEY-16 |
| any of the above | Root disabled | Nothing (trigger not focusable; interactions disabled). | SEL-KEY-19 |

- **SEL-KEY-01** — MUST. <kbd>ArrowDown</kbd> on the focused, enabled trigger while closed MUST open the popup with reason `list-navigation` and `trigger` = the trigger element, consume the event, and — once the list is mounted — highlight and focus the selected item (the anchor in multiple mode) when there is one, otherwise the first item that is not attribute-disabled (CMP-STA-33/37, CMP-KEY-21/22). The highlighted item is scrolled into view.
  Source: `select/root/SelectRoot.tsx:371-386`, `floating-ui-react/hooks/useListNavigation.ts:391-411,440-477,843-895`. Test: [L4] `SelectRoot.test.tsx` "opens the popup with ArrowDown" (readOnly), "anchors to the first selected item when opened with the keyboard", "skips null items when navigating"; `SelectItem.test.tsx` "should focus disabled items"; popup-list suite "opens on the first item with ArrowDown", "opens on the selected item with {ArrowDown}", "opens past a disabled first item".
- **SEL-KEY-02** — MUST. <kbd>ArrowUp</kbd> on the focused trigger while closed MUST behave as SEL-KEY-01 except that without a selection the *last* non-attribute-disabled item is highlighted. The recorded opening key MUST NOT carry over to a later pointer opening after the popup closed.
  Source: as SEL-KEY-01; `floating-ui-react/hooks/useListNavigation.ts:378-381`. Test: [L4] `SelectRoot.test.tsx` "does not carry an ArrowUp opening over to a later pointer opening"; popup-list suite "opens on the last item with ArrowUp", "scrolls the last item into view when opening with ArrowUp", "opens on the selected item with {ArrowUp}".
- **SEL-KEY-03** — MUST. <kbd>Enter</kbd> and <kbd>Space</kbd> on the focused trigger while closed MUST open the popup with reason `trigger-press` (through the element's keyboard click — native button activation, or the synthetic click of UBT-KEY-01/02 for a non-native trigger) and MUST NOT activate any item; the opening then highlights and focuses the selected item or, without a selection, the first item (the key is recorded as a to-end navigation key, CMP-KEY-21, CMP-STA-37).
  Source: `floating-ui-react/hooks/useClick.ts:187-219`, `floating-ui-react/hooks/useListNavigation.ts:64-73,855-865`. Test: [L4] `SelectRoot.test.tsx` "opens the popup with Enter", "opens the popup with Space" (readOnly); `SelectPopup.test.tsx` "keeps arrow keys inside the popup when the trigger lives in a toolbar", "moves focus into the items when the boundary item is already highlighted"; popup-list suite "opens with {Enter} without activating an item", "opens with   without activating an item".
- **SEL-KEY-04** — MUST. While closed, enabled, not `readOnly` and not `multiple`, a printable key (length-1 key without Ctrl/Meta/Alt) on the trigger MUST run typeahead against the item labels (CMP §4.9: buffer, 750 ms reset per CMP-STA-43, rapid same-letter cycling, start after the selected index) skipping items whose element is attribute-disabled, and when a match exists request a value change to the matched item's value with reason `none` and no event, **without opening the popup**. A session starts afresh after focus leaves the trigger and returns. When `readOnly`, `multiple` or no enabled match exists nothing happens.
  Source: `select/root/SelectRoot.tsx:388-411`, `floating-ui-react/hooks/useTypeahead.ts:93-204`. Test: [L4] `SelectRoot.test.tsx` "typeahead" (all ten tests except the open-popup ones), "does not commit a value with typeahead on a closed trigger" (readOnly). Docs: `CHANGELOG.md v1.6.0 "Skip disabled items in typeahead and fix multiple-mode serialization"`.
- **SEL-KEY-05** — MUST. While closed, <kbd>Home</kbd>, <kbd>End</kbd>, <kbd>Escape</kbd>, <kbd>Tab</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>PageUp</kbd> and <kbd>PageDown</kbd> on the trigger are not intercepted by Select (native focus movement / scrolling occurs; Escape does nothing).
  Source: `floating-ui-react/hooks/useListNavigation.ts:843-895` (only MAIN keys handled on the trigger), `floating-ui-react/hooks/useDismiss.ts:203` (Escape only while open). Test: none.
- **SEL-KEY-06** — MUST. While open, <kbd>ArrowDown</kbd>/<kbd>ArrowUp</kbd> (CMP-KEY-30…32 with `loopFocus: false`, `disabledIndices: []`) MUST move the highlight one item down/up in registry order, stopping at the last/first item, treating disabled items as ordinary stops; when focus is inside the popup but on no item (e.g. on the popup container or on a non-item button inside it) the key highlights the first (ArrowDown) / last (ArrowUp) item and focuses it even if it was already highlighted (CMP-KEY-31). The event's default is prevented and propagation stopped.
  Source: `select/root/SelectRoot.tsx:376`, `floating-ui-react/hooks/useListNavigation.ts:644-700`. Test: [L4] `SelectItem.test.tsx` "navigating with keyboard should focus item", "should focus disabled items"; `SelectPopup.test.tsx` "moves focus into the items when the boundary item is already highlighted"; `SelectRoot.test.tsx` "navigates across groups without stopping on labels or separators", "continues arrow navigation from the anchor rather than the top of the list"; popup-list suite "moves the highlight one item at a time with the arrow keys", "moves focus along with the highlight", "stops on a disabled item while navigating", "stops past the first item", "stops past the last item", "keeps a single item reachable", "opens and handles keys without losing focus when the list is empty", "prevents the default scrolling of navigation keys".
- **SEL-KEY-07** — MUST. While open, <kbd>Home</kbd>/<kbd>End</kbd> MUST highlight and focus the first/last registered item (disabled items included), consuming the event (CMP-KEY-28).
  Source: `floating-ui-react/hooks/useListNavigation.ts:603-615`. Test: [L4] `SelectRoot.test.tsx` "navigates across groups without stopping on labels or separators"; popup-list suite "moves the highlight to the last and first items with End and Home", "moves to disabled ends with End and Home".
- **SEL-KEY-08** — MUST. <kbd>Enter</kbd> on a focused item (UBT-KEY-01: default prevented, synthetic click dispatched) MUST activate the item exactly as a click does (SEL-PTR-08 with a virtual click): in single mode request the value change with reason `item-press` and then request a close with reason `item-press`; in multiple mode toggle the item's membership (SEL-EVT-05) and keep the popup open; a disabled item, a disabled Root or `readOnly` MUST activate nothing (the disabled click is default-prevented and no handler runs, UBT-PTR-01; `readOnly` is checked in the commit).
  Source: `select/item/SelectItem.tsx:133-151,170-193`. Test: [L4] `SelectItem.test.tsx` "should select item when Enter key is pressed", "should not select disabled item when Enter is pressed"; `SelectRoot.test.tsx` "does not commit a value with Enter on a highlighted item" (readOnly), "keeps the highlight on an item deselected with the keyboard", "prop: validateMode=onSubmit"; popup-list suite "activates the highlighted item with {Enter}", "does not submit a surrounding form".
- **SEL-KEY-09** — MUST. <kbd>Space</kbd> keydown on a focused item (composite mode, UBT-KEY-12) MUST activate it like Enter, **except** while a typeahead session is in progress (SEL-STA-32): then the item default-prevents the keydown before the button mechanism sees it, so no click is dispatched (UBT-KEY-13, `role="option"`) and the Space character extends the typeahead buffer (CMP). A pointer click after a Space-terminated typeahead sequence MUST still select.
  Source: `select/item/SelectItem.tsx:161-169`, `floating-ui-react/hooks/useTypeahead.ts:132-136`. Test: [L4] `SelectRoot.test.tsx` "does not trigger selection when Space is pressed during text navigation"; `SelectItem.test.tsx` "should allow pointer click after a typeahead sequence that ends with Space"; popup-list suite "activates the highlighted item with  " (Space). Docs: `CHANGELOG.md v1.6.0 "Fix selection on space key press"` (listed under Combobox/Select fixes, #4930).
- **SEL-KEY-10** — MUST. <kbd>Escape</kbd> while open (keydown on the document, outside an IME composition) MUST request a close with reason `escape-key` (FLT-KEY-01, FLT-KEY-03) and the native keyboard event; when not canceled the keydown's default is prevented (EVD-EVT-11); propagation is stopped unless `allowPropagation()` was called (EVD-DIS-01), so an enclosing popup does not close. Nothing is committed. Server: relaxed, see §9.
  Source: `select/root/SelectRoot.tsx:369`, `floating-ui-react/hooks/useDismiss.ts:201-233`; see FLT-KEY-01, FLT-KEY-02, FLT-KEY-03. Test: [L4] `SelectRoot.test.tsx` "does not bubble Escape from a non-modal select to the popover", "automatically unmounts with an actions ref and completes closing once"; popup-list suite "closes on Escape without committing, and keeps focus on the trigger", "closes only itself on Escape inside a dialog", "starts fresh when reopened after Escape".
- **SEL-KEY-11** — MUST. <kbd>Tab</kbd> is not intercepted by list navigation (CMP-KEY-24 handles only <kbd>Shift</kbd>+<kbd>Tab</kbd>). `Select.Popup` passes `modal={false}` to its focus manager with `closeOnFocusOut` at its default `true`, inside the `Select.Portal` floating portal (`select/popup/SelectPopup.tsx:479-486`, `select/portal/SelectPortal.tsx:27`); the observable result is per FLT-KEY-06, FLT-KEY-07 and FLT-FOC-11: tabbing out of the popup closes it with reason `focus-out` (SEL-FOC-12) and the portal's guards place focus on the next tabbable element after the trigger, while the portaled items leave the document tab sequence (FLT-FOC-17). <kbd>Shift</kbd>+<kbd>Tab</kbd> inside the popup is consumed by list navigation: it closes with reason `focus-out` and, unless canceled, focuses the trigger (CMP-KEY-24). Neither commits a value.
  Source: `select/popup/SelectPopup.tsx:479-486`, `select/portal/SelectPortal.tsx:27`, `floating-ui-react/hooks/useListNavigation.ts:768-792`; see FLT-KEY-06, FLT-KEY-07, FLT-FOC-11, FLT-FOC-17, CMP-KEY-24. Test: [L4] `SelectRoot.test.tsx` "does not leave a tabbable option while closed and kept mounted after tabbing out"; popup-list suite "closes on Tab without committing, and moves focus past the trigger", "closes on Shift+Tab without committing, and moves focus to the element before the popup".
- **SEL-KEY-12** — MUST. While open, a printable key on the popup or an item MUST run typeahead (consuming the event and marking typing) and, on a match, set the highlighted item to the match (reason-less, no value change), skipping attribute-disabled items; this works in `readOnly` and `multiple` mode. A pause longer than 750 ms (the hook default `resetMs`, CMP-STA-43) starts a new search; focus leaving both the trigger and the popup ends the session.
  Source: `select/root/SelectRoot.tsx:401-407`, `floating-ui-react/hooks/useTypeahead.ts:156-159,183-187,206-223`. Test: [L4] `SelectRoot.test.tsx` "moves the highlight with typeahead while the popup is open", "skips disabled items when highlighting via typeahead on an open popup", "matches an item by its label when the rendered text differs"; popup-list suite "reveals an item reached by typing its label", "starts a new typeahead search after a pause". Docs: `CHANGELOG.md v1.0.0-beta.2 "Allow typeahead while open for \`multiple\` mode"`.
- **SEL-KEY-13** — MUST. Any `keydown` on an item MUST first set the highlighted item to that item (so a key pressed on a focused but un-highlighted item navigates from it).
  Source: `select/item/SelectItem.tsx:161-162`. Test: [L4] `SelectRoot.test.tsx` "keeps the highlight on an item deselected with the keyboard".
- **SEL-KEY-14** — MUST. When the Root is rendered inside a `Toolbar.Root`, `keydown` events for `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Home` and `End` on the Popup MUST have their propagation stopped so the toolbar's roving focus does not act on them; outside a toolbar they propagate normally. This is Select's side of TLB-KEY-15 (unlike `Dialog.Popup`, which stops them unconditionally).
  Source: `select/popup/SelectPopup.tsx:65,446-450`, `internals/composite/composite.ts:22`; see TLB-KEY-15. Test: [L4] `SelectPopup.test.tsx` "keeps arrow keys inside the popup when the trigger lives in a toolbar".
- **SEL-KEY-15** — MUST. Modifier handling: typeahead ignores keys pressed with Ctrl, Meta or Alt (CMP); list navigation does not inspect modifiers (an arrow key with Shift still navigates); <kbd>Enter</kbd>/<kbd>Space</kbd> activation copies the modifier state into the synthetic click (UBT-EVT-04).
  Source: `floating-ui-react/hooks/useTypeahead.ts:144-154`, `floating-ui-react/hooks/useListNavigation.ts:555-700`. Test: none.
- **SEL-KEY-16** — MUST. While open, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>PageUp</kbd> and <kbd>PageDown</kbd> are not intercepted (vertical orientation; no grid).
  Source: `floating-ui-react/hooks/useListNavigation.ts:56-62,644`. Test: none.
- **SEL-KEY-17** — MUST. A keydown on the trigger while the popup is open and the trigger has focus (possible in non-aligned mode after focus returned to it) MUST be handled per CMP-KEY-22: an arrow key navigates the open list from the selected index without re-opening.
  Source: `floating-ui-react/hooks/useListNavigation.ts:876-895`. Test: [L4] `SelectRoot.test.tsx` "prop: validateMode=onSubmit" (ArrowDown after submit focus).
- **SEL-KEY-18** — MUST. In `readOnly` mode <kbd>ArrowDown</kbd>, <kbd>Enter</kbd> and <kbd>Space</kbd> on the trigger still open the popup and the open-popup keys of SEL-KEY-06/07/12 still navigate; only commits are suppressed.
  Source: `select/root/SelectRoot.tsx:361-367,388-391`. Test: [L4] `SelectRoot.test.tsx` "opens the popup with $name", "does not commit a value with Enter on a highlighted item", "moves the highlight with typeahead while the popup is open".
- **SEL-KEY-19** — MUST. While the Root is disabled, no keyboard interaction (open, navigate, typeahead, activate) MUST occur; a native-button trigger is not focusable and a non-native one has `tabindex="-1"`.
  Source: `select/root/SelectRoot.tsx:365,372,391`, `select/trigger/SelectTrigger.tsx:144`. Test: [L4] `SelectRoot.test.tsx` "sets the disabled state"; `SelectTrigger.test.tsx` "cannot be focused when disabled".
- **SEL-KEY-20** — MUST. The Root passes `disabledIndices: []` (an empty array, not omitted) to list navigation (`select/root/SelectRoot.tsx:376`) and the predicate `(i) => isElementDisabled(items[i])` to typeahead (`select/root/SelectRoot.tsx:400`); the observable result is per CMP-ALG-02, CMP-STA-37, CMP-KEY-28 and CMP-KEY-32: because the array is present, arrow keys, <kbd>Home</kbd> and <kbd>End</kbd> skip only invisible or natively `:disabled` elements, so a disabled (`aria-disabled="true"`, SEL-DOM-37) item is an ordinary stop and a reachable list end (CMP-KEY-51, CMP-KEY-52); the initial highlight of a keyboard opening without a selection is computed **without** `disabledIndices`, so an `aria-disabled` first item (<kbd>ArrowDown</kbd>, <kbd>Enter</kbd>, <kbd>Space</kbd>) or last item (<kbd>ArrowUp</kbd>) is skipped on open (CMP-KEY-45). Consequence: with item 0 disabled, opening with <kbd>ArrowDown</kbd> highlights item 1, and a following <kbd>Home</kbd> highlights the disabled item 0. A selected item is highlighted on open even when disabled (CMP-STA-33 uses `selectedIndex` as-is). Typeahead never matches an attribute-disabled item (SEL-KEY-04/12).
  Source: `select/root/SelectRoot.tsx:376,400`, `floating-ui-react/hooks/useListNavigation.ts:459-470`, `floating-ui-react/utils/composite.ts:520-554`; see CMP-ALG-02, CMP-STA-37, CMP-KEY-28. Test: [L4] popup-list suite "opens past a disabled first item", "stops on a disabled item while navigating", "moves to disabled ends with End and Home"; `SelectItem.test.tsx` "should focus disabled items".

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| primary-button `mousedown` on trigger | closed, enabled | Open (reason `trigger-press`) on the next frame; pointer type recorded as the open method. | SEL-PTR-01 |
| primary-button `mousedown` on trigger | open | Close (reason `trigger-press`). | SEL-PTR-01 |
| non-primary button on trigger | any | Ignored. | SEL-PTR-02 |
| press trigger, drag, release outside trigger and popup | opened by this press | Close (reason `cancel-open`) unless the release is within 5 px of the trigger's bounds. | SEL-PTR-04 |
| press trigger, drag over an item, release on it | within 400 ms of opening | Selects only if the pointer moved ≥ 8 px vertically over items (accumulated) and the item is unselected; a release on the already-selected item never selects within the delay. | SEL-PTR-05/06/07 |
| press trigger, drag over an item, release on it | after 400 ms | Selects the item (synthetic click). | SEL-PTR-05/07 |
| `click` on an item that started with a `pointerdown` on that item | open | Activate (SEL-KEY-08 semantics). | SEL-PTR-08 |
| `click` on an item whose press started elsewhere (mouse) | open | Ignored. | SEL-PTR-08 |
| virtual click (`detail === 0`, no pointer data) on an un-highlighted item | open | Ignored; on a highlighted item or with pointer data (assistive tech) → activate. | SEL-PTR-08 |
| touch tap on an item | open | Activate via the click; a synthesized `mouseup` after touch never selects. | SEL-PTR-09 |
| `mousemove` over an item | open, `highlightItemOnHover` | Highlight and focus the item; leaving the list clears the highlight (not during close). | SEL-PTR-13 |
| `mousemove` over a scroll arrow | arrow visible | Clear the highlight; auto-scroll one item every 40 ms until the edge or `mouseleave`. | SEL-PTR-15…17 |
| press outside trigger and popup | open | Close (`outside-press`, sloppy: mouse `pointerdown`, touch `touchend`/scroll-away). | SEL-DIS-02 |
| right click on trigger or item | any | Ignored. | SEL-PTR-02 |
| click on `Select.Label` | any | Focus the trigger without opening. | SEL-PTR-24 |

- **SEL-PTR-01** — MUST. A primary-button `mousedown` on the enabled trigger MUST toggle the popup through the shared click interaction configured with `event: 'mousedown'` (`toggle` at its default `true`); the observable result is per FLT-PTR-02, FLT-PTR-03 and FLT-ALG-16: while closed it requests an open with reason `trigger-press` and `trigger` = the trigger, on the **next frame** (immediately for virtual presses); while open it requests a close with the same reason. The subsequent `click` of a pointer press is ignored (consumed by the mousedown path); a keyboard click (no preceding pointer) toggles on `click` (SEL-KEY-03).
  Source: `select/root/SelectRoot.tsx:364-367`, `floating-ui-react/hooks/useClick.ts:138-222`; see FLT-PTR-02, FLT-PTR-03, FLT-ALG-16. Test: [L4] `SelectItem.test.tsx` "should not select an item on quick mouseup when showing a placeholder (no null item)" (opens on mousedown); conformance suite "opens on trigger click and sets `aria-expanded` on the trigger"; popup-list suite "closes when the trigger is clicked again".
- **SEL-PTR-02** — MUST. Presses with a non-primary button MUST NOT open or close the popup, and right clicks on items MUST NOT activate them.
  Source: `floating-ui-react/hooks/useClick.ts:145-151`; see FLT-PTR-02. Test: [L4] popup-list suite "ignores right clicks on the trigger and on items".
- **SEL-PTR-03** — MUST. The open method (SEL-STA-08) MUST be recorded from the trigger's `pointerdown` (`pointerType`) and `click` (`'keyboard'` for `detail === 0`) per PSH-PTR-01/02, with the iOS hit-slop fallback (`''` → `'touch'` on iOS).
  Source: `select/root/SelectRoot.tsx:134,421`, `utils/useOpenInteractionType.ts:9-38`. Test: [L4] `SelectRoot.test.tsx` "keeps touch interaction type when reopening quickly after close".
- **SEL-PTR-04** — MUST. When a `mousedown` on the trigger occurs while closed, the trigger MUST, after a 0 ms macrotask (so that a Firefox `mouseup` dispatched during the mousedown is not caught), listen once for the next document `mouseup`; on that `mouseup`, if the target is inside the trigger or inside the positioner, or the pointer position lies within the trigger's bounds (including `::before`/`::after` pseudo-element extents, UTL-DOM-25) expanded by 5 px on every side, nothing happens; otherwise a close is requested with reason `cancel-open` and the mouse event. The pending listener is cleared when the popup closes.
  Source: `select/trigger/SelectTrigger.tsx:101,126,173-207`, `utils/getPseudoElementBounds.ts:14-29`. Test: [L4] `SelectTrigger.test.tsx` "keeps the popup open when the press slips up to 5px off the trigger", "closes the popup when the press is dragged off the trigger and released"; `SelectRoot.test.tsx` "does not consume the next outside press after a native drag from a modal select trigger outside all popups". Docs: `CHANGELOG.md v1.7.0 "Widen trigger slip-out release tolerance"`, `v1.0.0-alpha.5 "Handle pseudo-element bounds in mouseup detection"`.
- **SEL-PTR-05** — MUST. *Selection delay.* When the popup opens, both release-selection gates (`allowSelectedMouseUp`, `allowUnselectedMouseUp`) start `false` and the vertical drag accumulator at `0`; 400 ms after the open both gates become `true`. When the popup closes both gates and the accumulator are reset to `false`/`0` and the pending timer cleared.
  Source: `select/trigger/SelectTrigger.tsx:27,104-129`, `select/root/SelectRoot.tsx:126-130`. Test: [L4] `SelectItem.test.tsx` "should not select an unselected item within the selected delay when aligned with the trigger", "should allow small pointer movement to select after the opening delay", "calls onClick once for a click made after the opening delay".
- **SEL-PTR-06** — MUST. A `pointermove` over an item with `pointerType === 'mouse'` and the primary button held (`buttons === 1`) MUST add the event's `movementY` to the drag accumulator (shared across items, reset on each `pointerdown` on an item and on each `mouseup`); once the accumulated square is ≥ 64 (|Δy| ≥ 8 px) the *unselected* release gate becomes `true` regardless of the delay.
  Source: `select/item/SelectItem.tsx:153-155,197-211`. Test: [L4] `SelectItem.test.tsx` "should call onClick when selecting via drag-to-select (mousedown on trigger, mouseup on item)", "should accumulate drag-to-select movement across items", "should not treat small pointer movement as drag-to-select", "should select via drag-to-select when hover highlighting is disabled". Docs: `CHANGELOG.md v1.0.0-beta.7 "Support drag-to-select"`.
- **SEL-PTR-07** — MUST. A `mouseup` on an item (after resetting the accumulator) MUST do nothing when the item is disabled, when the last pointer type seen on the item was `touch`, or when a `pointerdown` occurred on this item (the ordinary `click` will handle it). Otherwise, if the item is selected and the *selected* gate is `false`, or unselected and the *unselected* gate is `false`, nothing happens; else the item dispatches a synthetic `click` on itself (through the element's `click()`), temporarily marking the press as having started on the item so that SEL-PTR-08 accepts it. The consumer's `onClick` therefore fires once for a drag-select.
  Source: `select/item/SelectItem.tsx:212-236`. Test: [L4] `SelectItem.test.tsx` "should call onClick when selecting via drag-to-select…", "does not fire click handlers on a disabled item during drag-to-select", "should not select a disabled item via drag-to-select", "should not select on mouseup that follows a touch interaction", "should not select item when onClick calls preventBaseUIHandler during drag-to-select". Docs: `CHANGELOG.md v1.3.0 "Fire \`onClick\` during drag-to-select"`.
- **SEL-PTR-08** — MUST. On `click`, an item MUST classify the click: it is a *mouse click* when the last pointer type seen on the item is not `touch`; a mouse click is *virtual* when `isVirtualClick(event)` (UTL/FLT: trusted `pointerType === ''`, Android pointer clicks with `buttons === 1`, or `detail === 0` without `pointerType`) **and** (the event carries a `pointerType`, possibly `''`, or the item is highlighted). A non-virtual mouse click is *invalid* unless a `pointerdown` was received on this item since the last click (the "press started here" flag, cleared on every click). Invalid clicks and clicks on a disabled item MUST do nothing; every other click MUST run the commit of SEL-KEY-08 (single: value then close, both `item-press`; multiple: toggle). The press flag is consumed even when the click is ignored.
  Source: `select/item/SelectItem.tsx:117-118,170-193,207-211`, `floating-ui-react/utils/event.ts:13-23`. Test: [L4] `SelectItem.test.tsx` "should select the item and close popup when clicked", "should select an unhighlighted item with the mouse", "should highlight a hovered item and commit it with a mouse click", "should ignore an unhighlighted item with a generic virtual click", "should ignore an opening click that did not start on the item", "should call onClick exactly once for a regular click", "should not select disabled item". Docs: `CHANGELOG.md v1.5.0 "Allow mouse selection without highlight"`.
- **SEL-PTR-09** — MUST. For touch input the item's `pointerenter`/`pointerdown` record `pointerType === 'touch'`; the browser's `click` after a tap activates the item (SEL-PTR-08 treats it as a non-mouse click, accepted without the press flag), and a `mouseup` synthesized after a touch gesture MUST NOT select.
  Source: `select/item/SelectItem.tsx:171,194-196,215-217`. Test: [L4] `SelectItem.test.tsx` "should not select on mouseup that follows a touch interaction"; `SelectRoot.test.tsx` "keeps the selected item highlighted when reopening after a touch-driven mouseleave"; popup-list suite "keeps the highlight when a touch scroll starts on the highlighted item". Docs: `CHANGELOG.md v1.4.0 "Improve touch selection"`.
- **SEL-PTR-10** — MUST. A disabled item MUST NOT activate for any pointer gesture and MUST NOT invoke a consumer `onClick` (UBT-PTR-01: the click is default-prevented and handlers skipped); `pointerdown` on it is default-prevented (UBT-PTR-03); it remains hoverable (highlight) and focusable.
  Source: `select/item/SelectItem.tsx:120-125,188-190,215`. Test: [L4] `SelectItem.test.tsx` "should not select disabled item", "inherits the disabled state from the root", "does not fire click handlers on a disabled item during drag-to-select"; popup-list suite "does not activate a disabled item".
- **SEL-PTR-11** — MUST. A consumer `onClick` on an item that calls `preventBaseUIHandler()` MUST prevent the activation (RND-EVT-03), including for the synthetic click of a drag-select; the popup stays open.
  Source: `select/item/SelectItem.tsx:239-243` (consumer props merged before the built-in click). Test: [L4] `SelectItem.test.tsx` "should not select item when onClick calls preventBaseUIHandler during drag-to-select".
- **SEL-PTR-12** — MUST. A regular click (pointerdown + mouseup + click on the same item) MUST invoke the consumer's `onClick` exactly once and commit once; `onValueChange` is not called twice for one selection.
  Source: `select/item/SelectItem.tsx:170-236`. Test: [L4] `SelectItem.test.tsx` "should call onClick exactly once for a regular click"; `SelectRoot.test.tsx` "is not called twice on select".
- **SEL-PTR-13** — MUST. With `highlightItemOnHover` (default), a non-stationary `mousemove` over an item MUST highlight and focus it (CMP-PTR-04, CMP-PTR-08), and a `pointerleave` to outside the item list MUST clear the highlight and move focus to the popup element (CMP-PTR-05) — except while the popup is closing, where the highlight is retained (SEL-STA-07). With `highlightItemOnHover={false}` pointer movement MUST NOT change the highlight and leaving the popup MUST NOT clear it.
  Source: `select/root/SelectRoot.tsx:377-385`, `floating-ui-react/hooks/useListNavigation.ts:702-752`. Test: [L4] `SelectRoot.test.tsx` "highlights an item on mouse move by default", "does not highlight items from mouse movement when disabled", "does not remove highlight when mousing out of popup when disabled", "keeps the highlight on the item while the popup animates out"; popup-list suite "highlights and focuses the hovered item", "clears the highlight when the pointer leaves the list without leaving focus on an item", "continues keyboard navigation from the hovered item", "lets the pointer take the highlight back after keyboard navigation". Docs: `CHANGELOG.md v1.2.0 "Fix \`highlightItemOnHover\` not being respected"`, `v1.1.0 "Fix highlight being removed on popup mouseout when \`highlightItemOnHover\` is disabled"`.
- **SEL-PTR-14** — MUST. A `click` on an item focuses it without scrolling (CMP-PTR-03), so keyboard navigation continues from the clicked item in multiple mode.
  Source: `floating-ui-react/hooks/useListNavigation.ts:708-713`. Test: [L4] `SelectRoot.test.tsx` "keeps the active index on a deselected item in multiple mode".
- **SEL-PTR-15** — MUST. A `mousemove` on a scroll arrow whose `movementX` and `movementY` are both `0` MUST be ignored; any other `mousemove` while no auto-scroll timer is pending MUST clear the highlight and start a 40 ms timer; a later `mousemove` while the timer is pending MUST NOT restart it. `mouseleave` MUST clear the timer.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:86-135`. Test: [L3] `SelectScrollArrow.test.tsx` "does not start auto-scrolling for a mouse move that did not move the pointer", "does not let continuous pointer movement postpone the scheduled scroll", "stops auto-scrolling once the pointer leaves the arrow" (L4 for the highlight-clearing clause).
- **SEL-PTR-16** — MUST. *Auto-scroll step.* When the timer fires: let the scroller be the List element, else the popup element (if neither exists, stop). Clear the highlight, recompute arrow visibility (SEL-STA-29), compute `maxScrollTop` and the normalized `scrollTop′`; if `scrollTop′` differs from the raw `scrollTop`, write it; if `scrollTop′` is already at the arrow's edge (`0` for up, `maxScrollTop` for down) stop. Otherwise, if items are registered, set `scrollTop` to the target of SEL-PTR-17; then restart the 40 ms timer.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:93-131`. Test: [L3] `SelectScrollArrow.test.tsx` "snaps a sub-pixel offset to the exact top edge and then stops scrolling", "scrolls the popup itself when no Select.List is rendered", "reflects scrollability on the %s arrow when the popup has no registered items", "ignores pointer interaction when the arrow has no scrollable popup" (L4 for the highlight-clearing clause).
- **SEL-PTR-17** — MUST. *Target offset.* With `h` = the arrow's `offsetHeight` (0 when absent) and tolerance 1 px: **up** — the first visible index is the first item whose `offsetTop ≥ scrollTop′ + h − 1` (default 0); the target is the item before it; the result is `normalizeScrollOffset(target.offsetTop − h, maxScrollTop)` when such an earlier item exists, else `0`. **down** — the last visible index is the item before the first whose `offsetTop + offsetHeight > scrollTop′ + clientHeight − h + 1` (default: the last item); the target is the item after it; the result is `normalizeScrollOffset(target.offsetTop + target.offsetHeight − clientHeight + h, maxScrollTop)` when a later item exists, else `maxScrollTop`.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:189-235`. Test: [L3] `SelectScrollArrow.test.tsx` "$name" (all five geometry cases). Docs: `CHANGELOG.md v1.4.0 "Fix scroll arrows stopping short of the edges"`, `v1.0.0-beta.3 "Fix scroll arrows not propagating scroll fully to start/end of list"`.
- **SEL-PTR-18** — MUST. Scroll arrows are never *visible* (SEL-STA-25) while the rendered open method is `touch`, whatever the scroll position.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:37-40`. Test: none.
- **SEL-PTR-19** — MUST. While `modal` and mounted, the internal backdrop blocks pointer interaction with everything outside the popup except the trigger's rectangle (PSH-PTR-05); a press on the backdrop counts as an outside press (PSH-PTR-06).
  Source: `select/positioner/SelectPositioner.tsx:228`. Test: [L4] `SelectRoot.test.tsx` "should render an internal backdrop when `true`"; popup-list suite "closes on an outside press without committing".
- **SEL-PTR-20** — MUST. Pressing portaled content that is a DOM descendant of the popup element (even if rendered elsewhere in the tree) MUST NOT count as an outside press.
  Source: `floating-ui-react/hooks/useDismiss.ts` (inside-tree marking). Test: [L4] `SelectRoot.test.tsx` "does not dismiss when pressing portalled content inside the popup but outside the list".
- **SEL-PTR-21** — MUST. A disabled trigger MUST NOT open the popup on any pointer gesture and MUST NOT invoke `onOpenChange`.
  Source: `select/root/SelectRoot.tsx:365`. Test: [L4] `SelectTrigger.test.tsx` "does not toggle the popup when disabled"; `SelectRoot.test.tsx` "sets the disabled state".
- **SEL-PTR-22** — MUST. With aligned positioning, opening can place an item directly under the still-held pointer; SEL-PTR-05/07/08 together guarantee that releasing the press over that item (or any neighbor) within 400 ms without an 8 px drag does not select it, and that a `click` not preceded by a `pointerdown` on the item is ignored.
  Source: `select/item/SelectItem.tsx:180-191,224-231`. Test: [L4] `SelectItem.test.tsx` "should not select an unselected item within the selected delay when aligned with the trigger", "should ignore an opening click that did not start on the item".
- **SEL-PTR-23** — MUST. A touch-opened popup (rendered open method `touch`) MUST use ordinary anchored positioning (SEL-POS-06) and lock page scroll only when the positioner is at least the viewport width minus 20 px wide (PSH-PTR-07); it keeps that behavior through the closing transition.
  Source: `select/positioner/SelectPositioner.tsx:77-78,89-94`. Test: [L4] `SelectRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width", "does not apply scroll lock when a touch-opened popup is narrower than the viewport", "keeps touch positioning during the close transition", "recomputes positioning before the popup becomes visible again after touch dismiss".
- **SEL-PTR-24** — MUST. A click on `Select.Label` focuses the trigger and does not open the popup (SEL-DOM-15). A click on a native `Field.Label` (`<label for="<trigger id>">`) is forwarded by the browser as an activation click on the trigger button; since no pointer press was recorded on the trigger, that click opens the popup (reason `trigger-press`, SEL-PTR-01) and focus lands on the listbox (SEL-FOC-02). The two label parts therefore behave differently (§16 Q8).
  Source: `select/label/SelectLabel.tsx:34-42`, `internals/labelable-provider/useLabel.ts:30-44,46-61,69-75`, `floating-ui-react/hooks/useClick.ts:187-219`. Test: [L4] `SelectRoot.test.tsx` "Select.Label focuses trigger without opening", "Field.Label links to trigger and focuses it", "Field.Label links to trigger when trigger has an explicit id".

### 6.3 Focus management

Select renders the Popup inside the shared focus manager (FLT `FloatingFocusManager`) configured with `modal: false`, `disabled: !mounted`, `openInteractionType: openMethod`, `returnFocus: finalFocus` (default `true`), `restoreFocus: true`, and the default `initialFocus: true` / `closeOnFocusOut: true`.

- **SEL-FOC-01** — MUST. The trigger is the only tab stop of a closed Select: `tabindex="0"` while enabled (SEL-DOM-07); the hidden input has `tabindex="-1"`; every item has `tabindex="-1"` while closed (SEL-DOM-36).
  Source: `select/trigger/SelectTrigger.tsx:144`, `select/root/SelectRoot.tsx:580`, `select/item/SelectItem.tsx:160`. Test: [L4] `SelectRoot.test.tsx` "anchors to the first selected item when opened with the keyboard" (Tab lands on the trigger), "does not leave a tabbable option while closed and kept mounted after tabbing out".
- **SEL-FOC-02** — MUST. On open, focus MUST move into the popup. `Select.Popup` leaves the focus manager's `initialFocus` at its default (`true`) and passes `openInteractionType` = the recorded open method (`select/popup/SelectPopup.tsx:479-486`); list navigation keeps `focusItemOnOpen: 'auto'` (`select/root/SelectRoot.tsx:371-386`). The observable result: focus goes to the highlighted item when one is determined by the opening (the selected item / anchor; the first or last item for a keyboard opening; the first item after an aligned pointer opening without selection, SEL-POS-10) per CMP-STA-33/37 and the focus procedure CMP-FOC-03; otherwise (pointer opening with no selection and no alignment) to the popup element itself per FLT-FOC-06 (after a microtask; synchronously for a virtual-click opening, FLT-A11Y-04).
  Source: `select/popup/SelectPopup.tsx:479-488`, `select/root/SelectRoot.tsx:371-386`, `floating-ui-react/hooks/useListNavigation.ts:330-375`; see FLT-FOC-06, FLT-A11Y-04, CMP-STA-33, CMP-STA-37, CMP-FOC-03. Test: [L4] `SelectItem.test.tsx` "should focus the selected item upon opening the popup", "navigating with keyboard should focus item"; `SelectRoot.test.tsx` "does not carry an ArrowUp opening over to a later pointer opening" (listbox focused), "does not remove highlight when mousing out of popup when disabled" (popup focused on hover with hover highlight off); popup-list suite "starts without a highlight after a pointer opening", "reveals and highlights the selected item when opening".
- **SEL-FOC-03** — MUST. While open, the highlighted item carries `tabindex="0"` and all other items `tabindex="-1"`; moving the highlight (keyboard, hover, typeahead) moves DOM focus to the new item with `preventScroll`, synchronously for keyboard/hover-driven moves and otherwise on the next frame (CMP-FOC-03), and scrolls it into view (`block: 'nearest'`) for keyboard moves and for the selected item on open (CMP-FOC-04).
  Source: `select/item/SelectItem.tsx:160`, `floating-ui-react/hooks/useListNavigation.ts:330-375`. Test: [L4] `SelectItem.test.tsx` "navigating with keyboard should focus item"; `SelectRoot.test.tsx` "scrolls the first selected item into view on open"; popup-list suite "moves focus along with the highlight".
- **SEL-FOC-04** — MUST. `Select.Popup` passes `returnFocus={finalFocus}` (SEL-API-39) and `openInteractionType` = the recorded open method to its focus manager, which is disabled while the popup is unmounted (`disabled={!mounted}`) (`select/popup/SelectPopup.tsx:479-486`); the observable result is per FLT-FOC-07, FLT-FOC-20, FLT-FOC-21 and FLT-FOC-08: when the popup closes (unmounts or the focus manager is disabled), focus returns after a microtask to the element resolved from `finalFocus`; the default target is the trigger element when it is connected, except that for a popup opened without a trigger interaction (open method `null`: controlled `open`, `defaultOpen`) the element that had focus before the popup opened is preferred over the trigger. Keyboard closes focus with `focusVisible: true` (FLT-KEY-09). Focus is not returned after a focus-out close (SEL-FOC-12), after an outside press on a platform without `focus({ preventScroll })` support, or when `finalFocus` resolves to `false`/`undefined`.
  Source: `select/popup/SelectPopup.tsx:479-486`; see FLT-FOC-07, FLT-FOC-08, FLT-FOC-20, FLT-FOC-21, FLT-KEY-09, FLT-ALG-21. Test: [L4] `SelectRoot.test.tsx` "returns focus to the opener when a select is opened programmatically inside a popover"; `SelectPopup.test.tsx` focus-props suite "focuses the trigger by default", "focuses the element of a ref", "focuses the right element when a function returns $name", "does not move focus when `false`", "passes the close type to a function"; popup-list suite "closes on Escape without committing, and keeps focus on the trigger".
- **SEL-FOC-05** — MUST. `Select.Popup` passes `restoreFocus` (`true`) to its focus manager (`select/popup/SelectPopup.tsx:485`); the observable result is per FLT-FOC-12 (the `true` branch): when the focused element inside the popup is removed or hidden and focus falls to the body while the popup is open, focus goes to the previously focused tabbable element inside the popup if it still exists, else the last tabbable element inside the popup, else the popup element.
  Source: `select/popup/SelectPopup.tsx:485`; see FLT-FOC-12. Test: none.
- **SEL-FOC-06** — MUST. When the hidden input receives focus (browser validation bubble, autofill UI, programmatic), focus MUST be redirected to the trigger with `focus({ focusVisible: true })` (FFI-FOC-05).
  Source: `select/root/SelectRoot.tsx:518-524`. Test: [L4] `SelectRoot.test.tsx` "redirects focus to the trigger when the hidden input is focused".
- **SEL-FOC-07** — MUST. When the trigger receives focus while the popup is open **and** aligned positioning is active (the popup overlaps the trigger), a close MUST be requested with reason `none` and the focus event, so the popup does not obscure the focused trigger. A non-aligned open popup stays open.
  Source: `select/trigger/SelectTrigger.tsx:145-151`. Test: [L4] `SelectTrigger.test.tsx` "closes an aligned popup when focus lands on the trigger so it is not obscured", "keeps a non-aligned popup open when focus lands on the trigger".
- **SEL-FOC-08** — MUST. When the trigger itself (not a descendant) receives focus while the popup is open, the highlight MUST be cleared (CMP-FOC-06).
  Source: `floating-ui-react/hooks/useListNavigation.ts:897-906`. Test: none.
- **SEL-FOC-09** — MUST. Select never traps focus: even with `modal` (which only adds the backdrop and scroll lock), <kbd>Tab</kbd> leaves the popup (SEL-KEY-11) and the focus manager is non-modal.
  Source: `select/popup/SelectPopup.tsx:481`. Test: [L4] popup-list suite "closes on Tab without committing, and moves focus past the trigger" (run with the default `modal: true`).
- **SEL-FOC-10** — MUST. The field's `focused` flag (FFI-STA-09) is owned by the trigger: set on trigger focus (unless disabled), cleared on trigger blur whose related target is **outside** the positioner (a blur into the popup keeps it), cleared when a close with reason `focus-out` or `outside-press` is applied, and released when the trigger becomes disabled or unmounts.
  Source: `select/trigger/SelectTrigger.tsx:91,145-146,160-167`, `select/root/SelectRoot.tsx:303-313`, `internals/field-root-context/useSetFieldFocused.ts:15-46`. Test: [L4] `SelectRoot.test.tsx` "[data-focused]", "is removed when the focused trigger becomes disabled", "is removed when the focused trigger unmounts", "does not mark as touched when focus moves into the popup", "validates when the popup is blurred".
- **SEL-FOC-11** — MUST. A trigger focus event MUST start the 0 ms force-mount timer (SEL-STA-09) so that a subsequent typeahead keystroke finds the item labels.
  Source: `select/trigger/SelectTrigger.tsx:153-158`. Test: [L4] `SelectRoot.test.tsx` "commits typeahead on a closed trigger when items are provided", "starts a new search after focus leaves the select".
- **SEL-FOC-12** — MUST. `Select.Popup`'s focus manager is non-modal with `closeOnFocusOut` at its default `true` (`select/popup/SelectPopup.tsx:479-486`); the observable result is per FLT-FOC-11, FLT-FOC-13 and FLT-FOC-08: while open, when focus moves (focusout on the trigger or popup, evaluated after a microtask) to an element that is inside neither the trigger, the popup, the portal node, a registered focus guard, nor a child/ancestor popup in the popup tree (FLT-ALG-22), and no pointer press is in progress on the trigger, a close is requested with reason `focus-out` and the focus event, and return-focus is suppressed for that close.
  Source: `select/popup/SelectPopup.tsx:479-486`; see FLT-FOC-11, FLT-FOC-13, FLT-FOC-08, FLT-ALG-22. Test: [L4] `SelectRoot.test.tsx` "validates when the popup is blurred"; popup-list suite "closes on Tab without committing, and moves focus past the trigger".

### 6.4 Dismissal

| Trigger of close | Reason | Req |
|---|---|---|
| Escape keydown | `escape-key` | SEL-DIS-01 |
| Outside press (mouse `pointerdown`; touch `touchend` within 1 s or scroll-away after `touchstart`), including on the internal backdrop | `outside-press` | SEL-DIS-02 |
| Focus leaves the trigger/popup tree (Tab, Shift+Tab, programmatic focus elsewhere) | `focus-out` | SEL-DIS-03 |
| Primary-button `mousedown` on the trigger while open; keyboard click on the trigger while open | `trigger-press` | SEL-DIS-04 |
| Release of the opening press outside trigger and popup | `cancel-open` | SEL-DIS-05 |
| Window `resize` while aligned positioning is active | `window-resize` | SEL-DIS-06 |
| Item activation in single mode | `item-press` | SEL-DIS-07 |
| `actionsRef.current.close()` | `imperative-action` | SEL-DIS-08 |
| Trigger focus while aligned and open | `none` | SEL-DIS-09 |

- **SEL-DIS-01** — MUST. Escape closes per SEL-KEY-10. It is not suppressed by `disabled` or `readOnly` (the dismiss interaction is always enabled).
  Source: `select/root/SelectRoot.tsx:369`. Test: [L4] `SelectRoot.test.tsx` "does not bubble Escape from a non-modal select to the popover".
- **SEL-DIS-02** — MUST. The Root calls the shared dismiss interaction with every option at its default (`select/root/SelectRoot.tsx:369`): `outsidePress: true`, `outsidePressEvent: 'sloppy'` (FLT-API-02), `referencePress` always `false` (FLT-API-07), outside presses bubbling (FLT-API-01). The observable result is per FLT-PTR-26, FLT-PTR-27, FLT-DIS-08 and FLT-DIS-11: an outside press (mouse-like `pointerdown`; for touch, `touchend` within 1 s of the `touchstart` or a scroll-away after it) on an element outside the trigger and the popup — the internal backdrop counts as outside — requests a close with reason `outside-press` and the native event, without committing a value; a press on the trigger is not an outside press (it toggles per SEL-DIS-04). After such a close the field is marked touched and, in `onBlur` mode, validated (SEL-FRM-03/07).
  Source: `select/root/SelectRoot.tsx:303-313,369`, `floating-ui-react/hooks/useDismiss.ts:118-125`; see FLT-API-02, FLT-PTR-26, FLT-PTR-27, FLT-DIS-08, FLT-DIS-11. Test: [L4] `SelectTrigger.test.tsx` "sets data-popup-side to the current popup side" (click on body closes); `SelectRoot.test.tsx` "recomputes positioning before the popup becomes visible again after touch dismiss"; popup-list suite "closes on an outside press without committing". Docs: `CHANGELOG.md v1.0.0-beta.2 "Improve outside press behavior with touch input"`.
- **SEL-DIS-03** — MUST. Focus leaving the trigger/popup tree closes with reason `focus-out` (SEL-FOC-12, SEL-KEY-11); the field is marked touched and `onBlur`-mode validation runs (SEL-FRM-03/07).
  Source: `select/root/SelectRoot.tsx:303-313`. Test: [L4] `SelectRoot.test.tsx` "validates when the popup is blurred".
- **SEL-DIS-04** — MUST. Pressing the trigger while open closes with reason `trigger-press` (SEL-PTR-01); a keyboard click on the focused trigger while open does the same (click interaction `toggle: true`, FLT-PTR-03, FLT-ALG-16).
  Source: `select/root/SelectRoot.tsx:364-367`, `floating-ui-react/hooks/useClick.ts:94-124`; see FLT-ALG-16. Test: [L4] `SelectRoot.test.tsx` "unmounts the select when the `unmount` method is called" (second trigger click closes); popup-list suite "closes when the trigger is clicked again".
- **SEL-DIS-05** — MUST. Releasing the opening press outside the trigger and the positioner (beyond the 5 px tolerance) closes with reason `cancel-open` (SEL-PTR-04).
  Source: `select/trigger/SelectTrigger.tsx:200`. Test: [L4] `SelectTrigger.test.tsx` "closes the popup when the press is dragged off the trigger and released".
- **SEL-DIS-06** — MUST. While the popup is open with aligned positioning active, a `resize` event on the positioner's window MUST request a close with reason `window-resize` and the `UIEvent`; the listener exists only while aligned and open.
  Source: `select/popup/SelectPopup.tsx:421-433`. Test: [L4] `SelectPopup.test.tsx` "closes the aligned popup when the window is resized".
- **SEL-DIS-07** — MUST. In single mode, activating an item requests a close with reason `item-press` immediately after the value request (even if the value request was canceled, see SEL-EVT-12); in multiple mode activation never closes.
  Source: `select/item/SelectItem.tsx:141-150`. Test: [L2] `SelectRoot.test.tsx` "should close popup in single select mode", "should not close popup when selecting items in multiple mode", "should select an item and close when clicked while opened by default".
- **SEL-DIS-08** — MUST. `actionsRef.current.close()` closes with reason `imperative-action` and no event while open, and does nothing while closed (SEL-API-17).
  Source: `select/root/SelectRoot.tsx:321-325`. Test: [L2] `SelectRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out", "does not call `onOpenChange` when the `close` action is called while closed".
- **SEL-DIS-09** — MUST. Trigger focus while aligned and open closes with reason `none` (SEL-FOC-07).
  Source: `select/trigger/SelectTrigger.tsx:149-151`. Test: [L4] `SelectTrigger.test.tsx` "closes an aligned popup when focus lands on the trigger so it is not obscured".
- **SEL-DIS-10** — MUST. A Select nested in another popup (e.g. `Popover`) closes only itself: Escape does not propagate (SEL-KEY-10), choosing an item in a non-modal Select inside a modal popover keeps the popover open, and after the Select closes a single outside press dismisses the parent popover (the Select's dismiss bookkeeping MUST NOT swallow it, including after an aligned reopen or a native drag from a modal trigger).
  Source: `floating-ui-react/hooks/useDismiss.ts` (tree bubbling; see FLT-KEY-02, FLT-DIS-10), `select/trigger/SelectTrigger.tsx:173-207`. Test: [L4] `SelectRoot.test.tsx` "select inside popover" (all six tests). Docs: `CHANGELOG.md v1.5.0 "Fix nested Select requiring extra outside clicks to dismiss parent"`, `v1.0.0-beta.3 "Fix unexpected close when nested inside two popovers"`.
- **SEL-DIS-11** — MUST. Scrolling never dismisses the popup; instead page scroll is locked while open when `modal` or aligned (SEL-POS-21), and the aligned popup consumes list scrolling into its own height (SEL-POS-11).
  Source: `select/positioner/SelectPositioner.tsx:89-94`. Test: none.
- **SEL-DIS-12** — MUST. Every dismissal is a request: a close canceled through `cancel()` in `onOpenChange` leaves the popup open, and a controlled consumer that does not apply `open={false}` keeps it open (EVD-EVT-07, EVD-STA-10).
  Source: `select/root/SelectRoot.tsx:292-296`. Test: [L2] `SelectRoot.test.tsx` "ignores an opt-out on a canceled close"; conformance suite "keeps the popup closed when the open change is canceled", "is ignored when `open={true}` is passed, so Escape cannot close".

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onValueChange` | `(value, details)` | item activation; closed-trigger typeahead match; browser autofill match; item reconciliation | `item-press`, `none` | yes |
| `onOpenChange` | `(open, details & { preventUnmountOnClose })` | every open/close request (§6.4 and openings) | `trigger-press`, `list-navigation`, `item-press`, `escape-key`, `outside-press`, `focus-out`, `cancel-open`, `window-resize`, `imperative-action`, `none` | yes |
| `onOpenChangeComplete` | `(open)` | enter animations finished; closing phase ended | — | no |
| `Select.Value` `children` (function) | `(value) => node` | on every render of the Value part | — | no |
| `isItemEqualToValue` | `(itemValue, value) => boolean` | whenever two non-nullish values are compared | — | no |
| `itemToStringLabel` / `itemToStringValue` | `(itemValue) => string` | label / serialization of a non-nullish value | — | no |
| `finalFocus` (function) | `(closeType) => …` | once per close, when focus return is resolved | — | no |

- **SEL-EVT-01** — MUST. `onValueChange(nextValue, details)` is invoked synchronously inside the originating handler, before any state or DOM update (EVD-EVT-02), with `details.event` the native event of the activation (click/keyboard click, or the autofill change event) or a fresh generic event for reason-less requests (EVD-API-04).
  Source: `select/root/SelectRoot.tsx:330-340`, `select/item/SelectItem.tsx:146-148`. Test: [L2] `SelectRoot.test.tsx` "should call onValueChange when an item is selected".
- **SEL-EVT-02** — MUST. The reasons emitted by `onValueChange` are exactly: `item-press` for an item activation (click, drag-release, Enter, Space); `none` for a closed-trigger typeahead match, an autofill match, and item reconciliation (SEL-STA-12/13). The other literals of the union are declared only (EVD §4.3.3) and MUST NOT be emitted by `onValueChange`.
  Source: `select/item/SelectItem.tsx:146-148`, `select/root/SelectRoot.tsx:405,533`, `select/positioner/SelectPositioner.tsx:150`. Test: [L2] `SelectRoot.test.tsx` "does not update field state when autofill is canceled", "resets the value when the selected item is replaced and the item count is unchanged".
- **SEL-EVT-03** — MUST. Calling `details.cancel()` in `onValueChange` MUST prevent the commit for an uncontrolled value and MUST suppress the value-change side effects (dirty, filled, error clearing, validation — SEL-FRM-06) for both modes (EVD-EVT-05/06/08); the selected index and item `aria-selected` stay as before.
  Source: `select/root/SelectRoot.tsx:334-336`. Test: [L2] `SelectRoot.test.tsx` "does not update field state when autofill is canceled".
- **SEL-EVT-04** — MUST. In single mode `onValueChange` MUST fire on every item activation, including activating the already-selected item (the requested value then equals the current value; EVD-STA-09 row "Select onValueChange (single, item press)").
  Source: `select/item/SelectItem.tsx:147-149`. Test: none (see §16 Q2).
- **SEL-EVT-05** — MUST. In multiple mode an activation requests, for an unselected item, the current array (or `[]` when the value is not an array) with the item's value appended, and for a selected item the current array with every entry equal to the item's value removed (UTL `removeItem` with the comparer); the array passed is a new instance.
  Source: `select/item/SelectItem.tsx:141-146`, `internals/itemEquality.ts:126-134`. Test: [L2] `SelectRoot.test.tsx` "should allow multiple selections when multiple is true", "should deselect items when clicked again in multiple mode", "passes item as the first comparator argument in multiple mode"; `SelectItem.test.tsx` "treats a null value as an empty selection in multiple mode".
- **SEL-EVT-06** — MUST. `onValueChange` MUST NOT fire for a change of the controlled `value` prop (EVD-STA-06); such a change still updates the rendered label, selected items, and runs the field side effects (SEL-FRM-06).
  Source: `select/root/SelectRoot.tsx:281-286`. Test: [L2] `SelectRoot.test.tsx` "revalidates when the controlled value changes externally", "should update the selected item when the value prop changes".
- **SEL-EVT-07** — MUST. Exactly one `onValueChange` call results from one activation (drag-release, click or key), and a controlled consumer that ignores the request keeps the previous value and selection.
  Source: `select/item/SelectItem.tsx:170-236`. Test: [L2] `SelectRoot.test.tsx` "is not called twice on select", "should not update the internal value if the controlled value prop does not change" (L4 for the drag-release and key clauses).
- **SEL-EVT-08** — MUST. `onOpenChange(nextOpen, details)` fires for every open/close request with the reasons of §6.4 plus `trigger-press` (SEL-PTR-01, SEL-KEY-03), `list-navigation` (SEL-KEY-01/02) and `none` (SEL-DIS-09) for openings/closings; `details.trigger` is the trigger element for `trigger-press` and `list-navigation` requests (EVD-API-07). It fires before the open state changes; it MUST NOT fire for a change of the controlled `open` prop.
  Source: `select/root/SelectRoot.tsx:288-315,746-756`. Test: [L4] conformance suite "is called with the new open state and the change details"; `SelectTrigger.test.tsx` "closes an aligned popup when focus lands on the trigger…" (`reason: 'none'`); `SelectRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out" (`imperative-action`).
- **SEL-EVT-09** — MUST. `details.cancel()` in `onOpenChange` MUST abort the request entirely (EVD-EVT-07): no state change, no `preventUnmountOnClose` capture, no touched/validation side effects.
  Source: `select/root/SelectRoot.tsx:294-296`. Test: [L2] `SelectRoot.test.tsx` "ignores an opt-out on a canceled close"; conformance suite "keeps the popup closed when the open change is canceled".
- **SEL-EVT-10** — MUST. `details.preventUnmountOnClose()` called during a non-canceled close request MUST keep the popup mounted (closed, `data-ending-style`) until `actionsRef.current.unmount()` is called (TRN-EVT-07, TRN-ANI-23…26); the flag is read only after `onOpenChange` returns and only when the close is applied.
  Source: `select/root/SelectRoot.tsx:291,298-300`, `utils/popups/popupStoreUtils.ts` (`attachPreventUnmountOnClose`). Test: [L2] `SelectRoot.test.tsx` "keeps the popup mounted until the unmount action completes closing", "clears the opt-out when a controlled reopen interrupts a pending unmount", "keeps the opt-out when a controlled close is applied in a transition", "unmounts when `close` and `unmount` are called in one batch" (L4 for the `data-ending-style` clause). Docs: `CHANGELOG.md v1.9.0 "Decouple automatic unmounting from \`actionsRef\`"`.
- **SEL-EVT-11** — MUST. `onOpenChangeComplete(true)` fires once per open after the popup element's enter animations finish (TRN-EVT-01, from the Popup part — it never fires when no `Select.Popup` is rendered); `onOpenChangeComplete(false)` fires once per close from the unmount cleanup (TRN-EVT-02), also when `unmount()` ends an opted-out close; neither fires for a canceled request, for a mount in the closed state, or for a reopen that interrupts a close (TRN-EVT-03/05). Server: relaxed, see §9.
  Source: `select/popup/SelectPopup.tsx:178-186`, `select/root/SelectRoot.tsx:206-214`. Test: [L4] `SelectRoot.test.tsx` "manual unmount lifecycle" (all tests); conformance suite "prop: onOpenChangeComplete" (all tests).
- **SEL-EVT-12** — MUST. In single mode the close request of SEL-DIS-07 is issued with its own details object after the value request, regardless of whether `onValueChange` canceled the value request; canceling the value change therefore does **not** keep the popup open (the Select exception recorded in EVD-EVT-08; see §16 Q3).
  Source: `select/item/SelectItem.tsx:148-149`. Test: none.
- **SEL-EVT-13** — MUST. Value-change side effects — clearing the form's external error for the effective name, recomputing `dirty`, running `validation.change` — run post-commit, pre-paint whenever the committed value differs (strict inequality) from the value at the previous commit, for user, autofill, reconciliation and controlled changes alike (EVD-STA-07, UTL-STA-36); `filled` is recomputed from `hasSelectedValue` separately on every change. Server: relaxed, see §9.
  Source: `select/root/SelectRoot.tsx:260-262,281-286`. Test: [L2] `SelectRoot.test.tsx` "marks the field dirty and validates after successful autofill", "clears external errors on change", "revalidates when the controlled value changes externally".
- **SEL-EVT-14** — MUST. The `children` function of `Select.Value` is invoked during rendering with the current value and MUST NOT mutate it (multiple mode may pass the shared frozen empty array).
  Source: `select/value/SelectValue.tsx:47-49,85-96`. Test: [L2] `SelectValue.test.tsx` "accepts a function with a value parameter", "children function receives array of values in multiple mode".
- **SEL-EVT-15** — MUST. `isItemEqualToValue` receives an item value as the first argument and a selected value as the second in every call site (selection state, selected-index resolution, reconciliation, dirty comparison, removal) and is never called with a nullish operand (SEL-API-22).
  Source: `internals/itemEquality.ts:12-21,37-134`. Test: [L2] `SelectRoot.test.tsx` "passes item as the first comparator argument in multiple mode". Docs: `CHANGELOG.md v1.2.0 "Fix inconsistent isItemEqualToValue argument order"`.
- **SEL-EVT-16** — MUST. `itemToStringValue` and `itemToStringLabel` are invoked only with non-nullish single values (SEL-API-20/21); a nullish return is treated as `''`.
  Source: `internals/resolveValueLabel.tsx:68-71,83-86`. Test: [L2] `SelectRoot.test.tsx` "does not invoke itemToStringValue with the value array in multiple mode".
- **SEL-EVT-17** — MUST. A `finalFocus` function is called exactly once per close with the close interaction type derived from the closing event (`'keyboard'` for Escape/Tab; `'mouse'`/`'touch'`/`'pen'` for pointer-initiated closes that carry a pointer event); an item-press close reports `''` because the item's own close request carries a click whose pointer type is not retained (known gap, §16 Q5). The function is not called again when the popup is already closed.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:795-800,846-853`. Test: [L4] `SelectPopup.test.tsx` focus-props suite "passes the close type to a function" (`mouseCloseType: ''`).

## 8. Form integration

Select registers with the enclosing field as described in FFI §6.3 (Table A/B/C rows `Select.Root` / `Select`); this section states the Select-specific configuration and cites the mechanism.

- **SEL-FRM-01** — MUST. The Root registers as the field's active control (FFI-ALG-12) with: control handle = the trigger element (focused on an invalid submission, FFI-FOC-01), control id = the Root's effective id, registered (raw) value = the current value (item value, `null`, or the array), `getValue` = the string projection of SEL-FRM-02, `name` = the Root's `name` prop, `enabled = !disabled`. Registration is refreshed in place on every value change. Server: relaxed, see §9.
  Source: `select/root/SelectRoot.tsx:242-252`, `internals/field-register-control/useRegisterFieldControl.ts:7-46`. Test: [L2] `SelectRoot.test.tsx` "submits stringified value to onFormSubmit when itemToStringValue is provided", "revalidates immediately after form submission errors".
- **SEL-FRM-02** — MUST. The value projected into `Form.Values` / `formValues` is `stringifyAsValue(value, itemToStringValue)` in single mode and the array of each member's stringification in multiple mode (FFI-FRM-29); the `validate` callback receives the **raw** value (FFI-EVT-05).
  Source: `select/root/SelectRoot.tsx:235-243`. Test: [L2] `SelectRoot.test.tsx` "submits stringified value to onFormSubmit when itemToStringValue is provided", "passes raw value to validate when itemToStringValue is provided". Docs: `CHANGELOG.md v1.0.0 "Respect \`itemToStringValue\` for \`onFormSubmit\`"`.
- **SEL-FRM-03** — MUST. The field is marked *touched* (FFI-STA-07) when the trigger blurs with a related target outside the positioner, and when a close with reason `focus-out` or `outside-press` is applied; a blur into the popup, and closes for other reasons, do not mark it.
  Source: `select/trigger/SelectTrigger.tsx:160-166`, `select/root/SelectRoot.tsx:303-308`. Test: [L4] `SelectRoot.test.tsx` "[data-touched]", "does not mark as touched when focus moves into the popup", "validates when the popup is blurred".
- **SEL-FRM-04** — MUST. *Dirty* (FFI-STA-05) is `isSelectedValueDirty(value, initialValue, isItemEqualToValue)` (UTL): for two arrays, not element-wise equal under the comparer in the same order (order-sensitive); otherwise `value !== initialValue`. It is recomputed on every committed value change and returns to `false` when the value returns to the initial one.
  Source: `select/root/SelectRoot.tsx:283`, `internals/itemEquality.ts:23-35`. Test: [L2] `SelectRoot.test.tsx` "[data-dirty]", "removes [data-dirty] in multiple mode after returning to the initial value", "compares object values with isItemEqualToValue when clearing [data-dirty] in multiple mode", "keeps [data-dirty] in multiple mode when the same values return in a different order". Docs: `CHANGELOG.md v1.6.0 "Fix dirty state not clearing in multiple mode"`.
- **SEL-FRM-05** — MUST. *Filled* (FFI-STA-08) is `hasSelectedValue` (SEL-STA-02), written post-commit, pre-paint on every change: a `null`/`undefined` value, a value serializing to `''` (including a non-string whose `itemToStringValue` returns `''`) and an empty array are not filled. Server: relaxed, see §9.
  Source: `select/root/SelectRoot.tsx:254-262`. Test: [L2] `SelectRoot.test.tsx` "[data-filled]" (all six tests). Docs: `CHANGELOG.md v1.1.0 "Fix \`data-filled\` state in \`multiple\` mode"`.
- **SEL-FRM-06** — MUST. On every committed value change (SEL-EVT-13) the Root runs `clearErrors(effectiveName)` → `setDirty(…)` → `validation.change(value)` (FFI-EVT-02); in `onChange` mode (or `onSubmit` after a submission) this validates immediately and replaces a server error with the validator's verdict (FFI-FRM-26).
  Source: `select/root/SelectRoot.tsx:281-286`. Test: [L2] `SelectRoot.test.tsx` "clears external errors on change", "revalidates immediately after form submission errors", "prop: validationMode=onChange", "revalidates when the controlled value changes externally".
- **SEL-FRM-07** — MUST. In `onBlur` mode the current value is committed (`validation.commit(value)`, FFI-FRM-03) at the same boundaries that mark the field touched (SEL-FRM-03): trigger blur outside the positioner, and `focus-out`/`outside-press` closes.
  Source: `select/trigger/SelectTrigger.tsx:169-171`, `select/root/SelectRoot.tsx:310-312`. Test: [L4] `SelectRoot.test.tsx` "prop: validationMode=onBlur", "prop: validate", "validates when the popup is blurred", "passes raw value to validate when itemToStringValue is provided".
- **SEL-FRM-08** — MUST. Native constraint validation uses the hidden input: with `required` and no selection the input reports `valueMissing`, so a `Form` submission is blocked, `Field.Error match="valueMissing"` renders, the trigger gets `aria-invalid="true"` and is focused (FFI-FOC-01); selecting a value then revalidates (SEL-FRM-06).
  Source: `select/root/SelectRoot.tsx:576`. Test: [L4] `SelectRoot.test.tsx` "triggers native HTML validation on submit", "revalidates immediately after form submission errors", "prop: validateMode=onSubmit".
- **SEL-FRM-09** — MUST. A `Form.errors` entry for the effective name marks the field invalid (`aria-invalid` on the trigger, error rendered) until the value changes (FFI-FRM-25/26).
  Source: `select/root/SelectRoot.tsx:282`. Test: [L2] `SelectRoot.test.tsx` "clears external errors on change", "does not update field state when autofill is canceled".
- **SEL-FRM-10** — MUST. `Field.Root.name` overrides the Root's `name` for the hidden input(s), error lookup and `clearErrors` (SEL-API-07, FFI-STA-10).
  Source: `select/root/SelectRoot.tsx:100`. Test: [L2] `SelectRoot.test.tsx` "should receive name prop from Field.Root".
- **SEL-FRM-11** — MUST. While effectively disabled the hidden input and every per-value input carry `disabled`, so nothing is submitted and the field does not register the control.
  Source: `select/root/SelectRoot.tsx:250,505,575`. Test: [L2] `SelectRoot.test.tsx` "does not submit multiple values when disabled", "should receive disabled prop from Field.Root".
- **SEL-FRM-12** — MUST. While `readOnly` the hidden input carries `readonly` (a barred input, FFI-FRM-08: no native constraint applies) and autofill is ignored (SEL-FRM-19).
  Source: `select/root/SelectRoot.tsx:528,577`. Test: [L2] `SelectRoot.test.tsx` "ignores hidden-input autofill when readOnly inside Field".
- **SEL-FRM-13** — MUST. The `form` prop lets a Select rendered outside its `<form>` submit with it (single and multiple), FFI-DOM-08.
  Source: `select/root/SelectRoot.tsx:502,571`. Test: [L4] `SelectRoot.test.tsx` "submits to an external form when `form` is provided", "submits multiple values to an external form when `form` is provided".
- **SEL-FRM-14** — MUST. A `Field.Label` is associated with the trigger: the label's `for` equals the trigger's id (Root id, or the trigger's own `id` prop) and the trigger's `aria-labelledby` is the label's id (SEL-ARIA-05); clicking the native label focuses the trigger.
  Source: `select/root/SelectRoot.tsx:97`, `select/trigger/SelectTrigger.tsx:86`. Test: [L2] `SelectRoot.test.tsx` "applies the root id to the trigger", "Field.Label", "Field.Label links to trigger and focuses it", "Field.Label links to trigger when trigger has an explicit id" (L4 for the label-click focus clause).
- **SEL-FRM-15** — MUST. `Select.Label` provides the accessible name when no `Field.Label` exists (SEL-ARIA-05, FFI-ARIA-07) and follows Root id changes (`${newId}-label`); in server-rendered markup the trigger has no `aria-labelledby` until hydration (FFI-FRM-34).
  Source: `select/label/SelectLabel.tsx:30-42`. Test: [L2] `SelectRoot.test.tsx` "Select.Label", "updates Select.Label linkage when root id changes", "does not link Select.Label before hydration".
- **SEL-FRM-16** — MUST. `Field.Description` / `Field.Error` ids are composed into the trigger's and hidden input's `aria-describedby` after any consumer-supplied ids (FFI-ARIA-01).
  Source: `select/trigger/SelectTrigger.tsx:212`. Test: [L2] `SelectRoot.test.tsx` "Field.Description".
- **SEL-FRM-17** — MUST. `aria-invalid="true"` appears on the trigger (and hidden input) while the field is invalid and the control not disabled, and disappears when validity is restored (FFI-ARIA-03).
  Source: `select/trigger/SelectTrigger.tsx:212`. Test: [L2] `SelectRoot.test.tsx` "prop: validate", "prop: validateMode=onSubmit", "clears external errors on change".
- **SEL-FRM-18** — MUST. *Browser autofill.* A `change` event on the hidden input that is not default-prevented, while enabled and not `readOnly`, MUST set force-mount (SEL-STA-09) and then, after a microtask (so the items can register), in single mode find the first registered item whose stringified value (SEL-DOM-51) or label string (UTL `stringifyAsLabel` with `itemToStringLabel`) equals the input text case-insensitively, else the first item whose rendered typeahead label equals it case-insensitively; when found and non-nullish, request a value change with reason `none` and the native change event (FFI-FRM-31).
  Source: `select/root/SelectRoot.tsx:525-568`. Test: [L2] `SelectRoot.test.tsx` "should handle browser autofill", "should handle browser autofill with object values", "should handle browser autofill with object values when autofill uses the label", "matches browser autofill against an item rendered label for primitive values regardless of case", "matches browser autofill by serialized value before an earlier rendered label", "matches browser autofill when an earlier item has no rendered label", "marks the field dirty and validates after successful autofill". Docs: `CHANGELOG.md v1.4.0 "Fix browser autofill with object values when autofill uses the label"`, `v1.2.0 "Add \`autoComplete\` prop…"`.
- **SEL-FRM-19** — MUST. Autofill MUST be ignored (no `onValueChange`, hidden input value unchanged, field state unchanged) when the Root is disabled or `readOnly`, in multiple mode, when the change event was default-prevented, or when no item matches.
  Source: `select/root/SelectRoot.tsx:528-530,536-539,558-563`. Test: [L2] `SelectRoot.test.tsx` "ignores browser autofill in multiple mode", "leaves the value untouched when autofill matches no item", "ignores hidden-input autofill when $lockState $label" (all four cases). Docs: `CHANGELOG.md v1.5.0 "Ignore hidden-input changes while readonly or disabled"`.
- **SEL-FRM-20** — MUST. The field's `focused` flag follows SEL-FOC-10, so `data-focused` is removed without a blur event when the focused trigger becomes disabled or unmounts.
  Source: `select/trigger/SelectTrigger.tsx:90-91`. Test: [L4] `SelectRoot.test.tsx` "is removed when the focused trigger becomes disabled", "is removed when the focused trigger unmounts".
- **SEL-FRM-21** — MUST. Multiple-mode submission produces one `FormData` entry per selected value under the effective name (SEL-DOM-53); an empty selection submits nothing while the shared input's value is `''` for validation purposes (SEL-DOM-52).
  Source: `select/root/SelectRoot.tsx:489-509`. Test: [L2] `SelectRoot.test.tsx` "should serialize multiple values correctly for form submission", "should serialize empty array as empty string in multiple mode" (L4 for the `FormData` clause).
- **SEL-FRM-22** — MUST. In multiple mode `required` is enforced only while the selection is empty (SEL-DOM-50).
  Source: `select/root/SelectRoot.tsx:576`. Test: [L2] `SelectRoot.test.tsx` "does not mark the hidden input as required when selection exists", "keeps the hidden input required when no selection exists".
- **SEL-FRM-23** — MUST. The Root's `id` becomes the labelable control id even inside a `Field.Root` (the field does not override it).
  Source: `select/root/SelectRoot.tsx:97`. Test: [L2] `SelectRoot.test.tsx` "applies the root id to the trigger". Docs: `CHANGELOG.md v1.8.0 "Fix the root ID being ignored inside a Field"`.
- **SEL-FRM-24** — MUST. Select does not react to a native form `reset` (FFI-FRM-30); the selected value, dirty/touched/filled flags and validity are unchanged by it.
  Source: FFI-FRM-30. Test: none.

## 9. Lifecycle, mounting and animation

Vocabulary: *mounted* = the popup subtree is rendered (TRN); *post-commit, pre-paint*, *next frame*, *on animations finished* per conventions §4.

- **SEL-ANI-01** — MUST. The popup's mounted flag and transition status follow TRN-ALG-01/05 with the options of SEL-STA-10: an accepted open sets `mounted = true` and `transitionStatus = 'starting'` in the same commit (cleared next frame); an accepted close sets `'ending'` in the closing commit and keeps the subtree mounted until the popup element's animations finish, then unmounts it (unless opted out, SEL-EVT-10) with the cleanup of SEL-ANI-07. Server: relaxed, see §9.
  Source: `select/root/SelectRoot.tsx:196-215`, `internals/useUnmountAfterClose.ts:48-130`. Test: [L4] `SelectRoot.test.tsx` "keeps the highlight on the item while the popup animates out"; conformance suite "animations" (both tests).
- **SEL-ANI-02** — MUST. While not mounted, the Portal renders only if force-mounted (SEL-STR-06); when it renders, the Positioner and Backdrop carry `hidden` (SEL-DOM-17/18) and the positioner's coordinates are reset to the viewport origin (`position: fixed; top: 0; left: 0`, no transform) because the anchor-positioning mechanism is told the elements are absent while unmounted (FLT keepMounted behavior, TRN §6.7).
  Source: `select/positioner/SelectPositioner.tsx:111,135`, `internals/useAnchorPositioning.ts:455-470`. Test: [L4] `SelectPositioner.test.tsx` "does not retain stale coordinates while closed".
- **SEL-ANI-03** — MUST. `Select.Popup` and `Select.Backdrop` carry `data-starting-style`/`data-ending-style` per TRN-DOM-01…03; `Select.Positioner` does not.
  Source: `select/popup/SelectPopup.tsx:36-39`, `select/backdrop/SelectBackdrop.tsx:11-14`, `select/positioner/SelectPositioner.tsx:117-122`. Test: [L4] `SelectRoot.test.tsx` "keeps touch positioning during the close transition" (`data-ending-style` on the popup).
- **SEL-ANI-04** — MUST. While `'starting'`, the Positioner and the Popup carry inline `transition: none` (SEL-DOM-21/30) so the enter transition does not run from the pre-positioned state.
  Source: `utils/usePositioner.tsx:37`, `select/popup/SelectPopup.tsx:471`. Test: none.
- **SEL-ANI-05** — MUST. `onOpenChangeComplete(true)` fires on animations finished for the Popup element, inspected once `data-starting-style` is removed (TRN-EVT-01), including for a Select mounted open (TRN-EVT-04).
  Source: `select/popup/SelectPopup.tsx:178-186`. Test: [L4] conformance suite "is called on open when there is no enter animation", "is called on open once the enter animation finishes".
- **SEL-ANI-06** — MUST. `onOpenChangeComplete(false)` fires exactly once per close, from the unmount cleanup: on animations finished for the Popup element, or when `actionsRef.current.unmount()` is called for an opted-out close; calling `unmount()` after the automatic unmount is a no-op (TRN-ANI-19…22). Server: relaxed, see §9.
  Source: `select/root/SelectRoot.tsx:206-214`. Test: [L4] `SelectRoot.test.tsx` "automatically unmounts with an actions ref and completes closing once", "completes closing once when `unmount` is called twice in one batch", "still unmounts on a later close after `unmount` was called while open", "still unmounts on a later close after `unmount` and a reopen in one batch", "ignores `unmount` while the popup is open".
- **SEL-ANI-07** — MUST. The unmount cleanup MUST, in one update, set the highlighted item to none, the open method to `null`, and both scroll-arrow visibility flags to `false`, then invoke `onOpenChangeComplete(false)`.
  Source: `select/root/SelectRoot.tsx:206-214`. Test: [L2] `SelectRoot.test.tsx` "clears scroll arrow visibility when manually unmounted (strict: %s)".
- **SEL-ANI-08** — MUST. `actionsRef.current.unmount()` while open is deferred until the current update commits and dropped if the popup is still open (TRN-ANI-20); `close()` followed by `unmount()` in one batch unmounts once (TRN-ANI-21).
  Source: `internals/useUnmountAfterClose.ts:102-116`. Test: [L2] `SelectRoot.test.tsx` "ignores `unmount` while the popup is open", "unmounts when `close` and `unmount` are called in one batch".
- **SEL-ANI-09** — MUST. A `preventUnmountOnClose()` opt-out belongs to one close cycle: a reopen (including a controlled `open={true}`) clears it, a canceled close discards it, and an opt-out recorded for a controlled close that commits later (e.g. in a transition) survives until that close commits (TRN-ANI-23…26).
  Source: `select/root/SelectRoot.tsx:196-205,288-300`. Test: [L2] `SelectRoot.test.tsx` "clears the opt-out when a controlled reopen interrupts a pending unmount", "ignores an opt-out on a canceled close", "keeps the opt-out when a controlled close is applied in a transition".
- **SEL-ANI-10** — MUST. A Select that mounts open (`defaultOpen`, controlled `open`) MUST NOT play an enter phase (`animateInitialOpen: false`, TRN-STA-13).
  Source: `select/root/SelectRoot.tsx:196-205`. Test: [L4] conformance suite "opens initially and remains uncontrolled".
- **SEL-ANI-11** — MUST. Each scroll arrow runs its own transition machine on its *visible* flag (TRN §6.1): becoming visible mounts it with `data-starting-style`; becoming not visible sets `data-ending-style` and unmounts it on animations finished (or keeps it, attribute-free, with `keepMounted`). Server: relaxed, see §9.
  Source: `select/scroll-arrow/SelectScrollArrow.tsx:46,63-71,145-150`. Test: [L4] `SelectRoot.test.tsx` "keeps a scroll arrow mounted while its exit animation runs".
- **SEL-ANI-12** — MUST. `Select.ItemIndicator` with `keepMounted` keeps the same element across selection changes, shows `data-ending-style` while its exit animations run after deselection and clears it on animations finished (RND-ANI-02/03); without `keepMounted` it is removed immediately on deselection (SEL-STR-11).
  Source: `select/item-indicator/SelectItemIndicator.tsx:20-25`, `utils/ItemIndicator.tsx:41-51`. Test: [L4] `SelectItemIndicator.test.tsx` "settles out of its transition state after the item is deselected", "preserves the kept-mounted element across selection changes", "mounts only while selected when keepMounted is false".
- **SEL-ANI-13** — MUST. The focus manager is disabled while the popup is not mounted, so focus return happens when the subtree unmounts (or when `mounted` becomes `false` for a kept-mounted close) and not at the moment `open` becomes `false`.
  Source: `select/popup/SelectPopup.tsx:482`. Test: [L4] `SelectRoot.test.tsx` "does not leave a tabbable option while closed and kept mounted after tabbing out".
- **SEL-ANI-14** — MUST. Items keep their highlight through the closing transition (SEL-STA-07) and ignore hover during it (the open-method and pointer-leave guards), so a fading popup does not re-highlight items under the pointer.
  Source: `select/root/SelectRoot.tsx:377-384`, `floating-ui-react/hooks/useListNavigation.ts:714-746`. Test: [L4] `SelectRoot.test.tsx` "keeps the highlight on the item while the popup animates out".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- SEL-ANI-01 — TRN-ANI-03/09/10: the exit unmount lands in the first render batch after the completion message (about two round trips); JS sets `hidden` at animation end (optimistic convergent write).
- SEL-ANI-06 — TRN-ANI-10: `onOpenChangeComplete(false)` and the unmount cleanup run in that later batch, not in the microtask the exit animation ends.
- SEL-ANI-11 — TRN-ANI-03/09: a scroll arrow's exit unmount lands about two round trips after its animations finish; JS hides it at animation end.
- SEL-EVT-11 — TRN-ANI-10: the closing `onOpenChangeComplete(false)` arrives about two round trips after the exit animation ends.
- SEL-KEY-10 — EVD-EVT-11, EVD-DIS-01, FLT-KEY-01: the consumer's `cancel()`/`allowPropagation()` arrives a round trip after dispatch; JS prevents and stops Escape whenever this popup is the innermost open one (ADR 04 §3 fallback). WebAssembly meets the spec through `decide()`.
- SEL-API-28 — RND-STA-01 (mirrored fields): `ClassValue`/`StyleValue` see `side`, `align`, `anchorHidden`, `transitionStatus` and scroll-arrow `visible` one round trip after the JS-written data attribute.
- SEL-STA-20 — RND-STA-01 (mirrored fields): the Positioner's `data-side`/`data-align` are JS-written at frame timing; the C# `side`/`align` state fields update one round trip later.
- SEL-STA-25 — RND-STA-01 (mirrored fields): the scroll arrow's `visible` and `side` state fields update one round trip after the JS-written attributes.
- SEL-STA-05 — CMP-STA-10: item registration and the selected-index claim run after paint plus a round trip; observable only as latency of dependent attribute updates.
- SEL-EVT-13 — EVD-STA-07: value-change side effects run after paint plus a round trip; observable only as latency of dependent attribute updates.
- SEL-FRM-01 — FFI registration: field registration runs after paint plus a round trip; observable only as latency.
- SEL-FRM-05 — FFI registration: `filled` is written after paint plus a round trip; observable only as latency of `data-filled`.

## 10. Positioning

The Positioner uses the shared anchor-positioning mechanism (FLT) with the props of §3.8 and the fixed options `keepMounted: true` and `disableAnchorTracking: disableAnchorTracking ?? alignItemWithTriggerActive`, through the shared positioner host (`usePositioner`: `role="presentation"`, `hidden: !mounted`, `inert: !open`). Everything specific to Select is the *item-aligned* mode below.

- **SEL-POS-01** — MUST. Outside aligned mode the popup is positioned by the mechanism with the Select defaults: `side 'bottom'`, `align 'center'`, offsets `0`, `collisionBoundary 'clipping-ancestors'`, `collisionPadding 5`, `arrowPadding 5`, `sticky false`, `positionMethod 'absolute'`, `collisionAvoidance { fallbackAxisSide: 'none' }` (flip between top and bottom only; never to left/right), and the CSS variables of SEL-DOM-22.
  Source: `select/positioner/SelectPositioner.tsx:96-112`. Test: [L4] `SelectPositioner.test.tsx` positioner conformance suite ("offsets the positioner when a number is specified", "offsets the positioner when a function is specified", "rests exactly at collisionPadding from the colliding edge", "uses transform positioning without Viewport"); `SelectTrigger.test.tsx` "sets data-popup-side to the current popup side" (`side="right"` honored when not aligned).
- **SEL-POS-02** — MUST. The positioner is always configured as kept-mounted (`keepMounted: true`): while not mounted the mechanism receives no reference/floating elements (no positioning runs, no anchor auto-update) and the element stays in the DOM hidden (SEL-ANI-02).
  Source: `select/positioner/SelectPositioner.tsx:111`, `internals/useAnchorPositioning.ts:455-470,582-586`. Test: [L4] `SelectPositioner.test.tsx` "does not retain stale coordinates while closed".
- **SEL-POS-03** — MUST. *Aligned mode is active* iff the popup is mounted, the latched `alignItemWithTrigger` (SEL-POS-05) is `true`, and the rendered open method is not `touch`. While active: the Positioner's and Popup's `side` state is `'none'` (`data-side="none"`), the Arrow renders nothing, the positioner's built-in style is `position: fixed` (SEL-DOM-20), anchor tracking is disabled, the scroll lock applies (SEL-POS-21), the window-resize close applies (SEL-DIS-06), and the trigger-focus close applies (SEL-FOC-07).
  Source: `select/positioner/SelectPositioner.tsx:75-94,109,114-115`, `select/popup/SelectPopup.tsx:421-433`. Test: [L4] `SelectPopup.test.tsx` "keeps alignItemWithTrigger active at browser zoom"; `SelectTrigger.test.tsx` "closes an aligned popup when focus lands on the trigger so it is not obscured". Docs: `components/select/page.mdx` §Positioning.
- **SEL-POS-04** — MUST. *Fallback.* During placement (SEL-POS-07) aligned mode MUST be abandoned — restoring the positioner's original inline `top`/`left`/`right`/`height`/`bottom`/`min-height`/`max-height`/`margin-top`/`margin-bottom` captured when the positioner was first measured (`top`/`left` default `'0'`), and latching `alignItemWithTrigger` to `false` — when any of: the trigger's top is less than 20 px from the viewport top; the trigger's bottom is less than 20 px from the usable viewport bottom; the computed popup height (rounded up) + 1 px is less than `min(scrollHeight of the scroller, minHeight)` where `minHeight` is the positioner's computed `min-height` in px or `100` when unset/0; or the browser is pinch-zoomed (`visualViewport.scale !== 1`) on a WebKit engine (UTL-EDGE-01).
  Source: `select/popup/SelectPopup.tsx:195-215,348-362`. Test: [L4] `SelectPopup.test.tsx` "keeps alignItemWithTrigger active when the aligned height is fractionally below minHeight", "ignores a pinch-zoomed visual viewport outside WebKit". Docs: `components/select/page.mdx` §Positioning; `CHANGELOG.md v1.0.0-alpha.5 "Fallback to standard positioning when pinch-zoomed in Safari"`.
- **SEL-POS-05** — MUST. The latched `alignItemWithTrigger` starts as the prop value and is re-synchronized to the prop only while the popup is **not mounted**; a fallback (SEL-POS-04) therefore lasts until the popup has fully closed, after which the next open tries aligned mode again. A prop change while mounted takes effect after the close.
  Source: `select/positioner/SelectPositioner.tsx:75-82`. Test: none.
- **SEL-POS-06** — MUST. A popup opened by touch (rendered open method `touch`, including through the closing transition and a quick reopen) MUST use ordinary positioning (`position: absolute` by default, `data-side` = resolved side) and MUST re-measure before becoming visible again after a touch dismiss.
  Source: `select/positioner/SelectPositioner.tsx:78`. Test: [L4] `SelectRoot.test.tsx` "keeps touch positioning during the close transition", "recomputes positioning before the popup becomes visible again after touch dismiss".
- **SEL-POS-07** — MUST. *Placement.* Post-commit, pre-paint after each committed update that changes the open state, the trigger, positioner or list element, the aligned-mode flag, `highlightItemOnHover`, the direction or the first-positioning-pass flag, and in which the popup is open with trigger, positioner and popup elements present, aligned mode active, the mechanism's first positioning pass complete, and the transition status not `'ending'`, the Popup MUST (with every length in CSS px, normalized by the elements' rendered scale so browser zoom does not distort them, and with the popup's inline `transform`/`scale`/`translate` temporarily forced to `none`/`1`/`0 0` and restored afterwards):
  1. Remove the popup's inline `--transform-origin`.
  2. Choose the *text element*: the selected item's text element if connected; else, when no value is selected, the first item's text element if connected; else none. The *value element* is `Select.Value` (SEL-DOM-12).
  3. Measure: trigger rect, positioner rect, scroller (`List` else Popup) `scrollHeight`, popup `border-bottom-width`, positioner `margin-top`/`margin-bottom` (each `|| 10`), positioner `min-height` (`|| 100`), popup max height = computed `max-height` when it ends in `px` and parses to a non-zero number, else `Infinity`; `viewportHeight = documentElement.clientHeight − marginTop − marginBottom`; `viewportWidth = documentElement.clientWidth`; `availableBelow = viewportHeight − trigger.bottom + trigger.height`.
  4. Horizontal: `alignedLeft = trigger.left` (LTR) or `trigger.right − positioner.width` (RTL). With a text element: `alignedLeft = positioner.left + (value.left − text.left)` (LTR) or `+ (value.right − text.right)` (RTL). Write `left = clamp(alignedLeft, 5, viewportWidth − 5 − positioner.width)`.
  5. Vertical: `offsetY = (text center − positioner.top) − (value center − trigger.top)` (0 without a text element); `idealHeight = availableBelow + offsetY + marginBottom + borderBottom`; `height = min(viewportHeight, idealHeight)`; `scrollTop = idealHeight − height`. Write `height`, `max-height: none`, `margin-top`/`margin-bottom` (the resolved values) on the positioner and `height: 100%` on the popup.
  6. Decide *top-positioned*: `scrollTop ≥ maxScrollTop(scroller) − 1`; if so `height = min(viewportHeight, positioner.height) − (scrollTop − maxScrollTop)`.
  7. Apply the fallback test (SEL-POS-04) and stop if it fires.
  8. Top-positioned: write `top = (positioner.height ≥ viewportHeight − marginTop − marginBottom) ? '0' : max(0, viewportHeight − idealHeight)px`, `height`, and scroll the scroller to its maximum. Otherwise write `bottom = '0'` and scroll the scroller to `scrollTop`.
  9. With a text element write the popup's `--transform-origin` (SEL-DOM-29).
  10. Mark *reached max height* when `max(minHeight, height) === viewportHeight` or `height ≥ popup max height`; recompute scroll-arrow visibility; and, when `highlightItemOnHover`, no selected index, no highlighted item and a first item exists, highlight the first item (SEL-POS-10).
  Source: `select/popup/SelectPopup.tsx:227-419,535-592`. Test: [L4] `SelectPopup.test.tsx` "aligns the selected item with the trigger inline start in ltr", "… when popup padding shifts the item text", "aligns the selected item with the trigger inline end in rtl", "aligns the popup with the trigger inline end in rtl when no selected item text is available", "keeps item text aligned with the trigger after reopening", "aligns the selected item with the trigger inline end on first open in the docs-style rtl layout", "treats short popups as top-positioned when maxScrollTop is off by 1px", "anchors the popup to the viewport bottom when the selection sits near the list top", "restores transform-related inline styles after measurement"; `SelectRoot.test.tsx` "aligns the first selected item with the trigger when alignItemWithTrigger is active", "resets aligned positioning after controlled value reset and option replacement" (and its two sibling tests). Docs: `CHANGELOG.md v1.4.0 "Fix RTL-aligned popup positioning with \`alignItemWithTrigger\`"`, `v1.3.0 "Fix \`alignItemWithTrigger\` fallback with browser zoom"`, `v1.2.0 "Fix \`alignItemWithTrigger\` transform with CSS animations"`.
- **SEL-POS-08** — MUST. The constants of SEL-POS-07 are normative: horizontal padding 5 px each side; trigger-to-viewport collision threshold 20 px; default margins 10 px; default `min-height` 100 px; edge tolerance 1 px (UTL `SCROLL_EDGE_TOLERANCE_PX`); a literal `0` for margin/min-height/max-height takes the default (the `|| N` fallback).
  Source: `select/popup/SelectPopup.tsx:286-297,535-538`, `utils/scrollEdges.ts:3`. Test: [L4] `SelectPopup.test.tsx` "treats a zero max-height as unconstrained rather than collapsing the popup".
- **SEL-POS-09** — MUST. The placement runs once per open (it is skipped while `'ending'`) and again whenever its inputs change while open (list element appearing, direction change, the mechanism re-positioning); each run starts from the current inline values, so a re-run after items were replaced must first clear the positioner's and popup's inline `height` (SEL-POS-13).
  Source: `select/popup/SelectPopup.tsx:233-244,407-419`, `select/positioner/SelectPositioner.tsx:196-205`. Test: [L4] `SelectRoot.test.tsx` "resets aligned positioning when value reset, option replacement, and controlled open happen together".
- **SEL-POS-10** — MUST. After an aligned placement, when `highlightItemOnHover` is `true`, no item is selected (selected index `null`), no item is highlighted and a first item exists, the first item MUST become highlighted (and therefore focused).
  Source: `select/popup/SelectPopup.tsx:396-403`. Test: [L4] `SelectRoot.test.tsx` "highlights the first item after opening when alignItemWithTrigger is active and no value is selected".
- **SEL-POS-11** — MUST. *Scroll-grow.* While aligned and placed and *reached max height* is `false`, a `scroll` event on the scroller (List, else Popup — bubbled scroll events on the Popup are ignored when a List exists) whose positioner has inline `top: 0px` (top-positioned) or `bottom: 0px` (bottom-positioned) MUST convert scrolling into height: with `maxAvailable = min(documentElement.clientHeight − marginTop − marginBottom, popup max height)`, `diff = maxScrollTop − scrollTop` (top-positioned) or `scrollTop` (bottom-positioned), `nextHeight = min(currentHeight + diff, maxAvailable)`:
  (a) if `diff ≤ 1`: grow by `clamp(diff, 0, maxAvailable − currentHeight)` when positive, pin the scroller to its edge (max for top-positioned, `0` otherwise), mark reached-max when within 1 px of `maxAvailable`, update arrow visibility, stop;
  (b) otherwise write `height = ceil(nextHeight)` (never `0`); then if `maxAvailable − nextHeight > 1` scroll to the edge (max / `0`), else if bottom-positioned and not at max scroll to `scrollTop − (diff − overshoot)` where `overshoot = currentHeight + diff − maxAvailable`; the target is clamped to the recomputed range and only written when it differs by more than 1 px; mark reached-max when `ceil(nextHeight) ≥ maxAvailable − 1`; update arrow visibility.
  Once reached-max is `true`, or when the positioner is neither top- nor bottom-positioned, a scroll only updates arrow visibility.
  Source: `select/popup/SelectPopup.tsx:85-176`, `select/list/SelectList.tsx:36-38`, `select/popup/SelectPopup.tsx:451-456`. Test: [L4] `SelectPopup.test.tsx` "aligned popup scroll resizing" (all fourteen tests), "keeps scrolling the aligned list at browser zoom after the popup fills the available height"; `SelectRoot.test.tsx` "keeps the highlighted item visible while keyboard navigation grows the popup", "does not move the popup or its list when hovering items at its edges". Docs: `CHANGELOG.md v1.2.0 "Fix scroll height loop"`, `v1.1.0 "Fix support for \`max-height\` popup style when \`alignItemWithTrigger\` is active"`.
- **SEL-POS-12** — MUST. Outside aligned mode, scroll-arrow visibility is computed on the next frame after placement (the List, when present, may mount after the Popup) and on every scroll of the scroller afterwards.
  Source: `select/popup/SelectPopup.tsx:247-254`. Test: [L4] `SelectRoot.test.tsx` "normalizes overlapping fractional scroll ranges when toggling scroll arrow visibility".
- **SEL-POS-13** — MUST. When the popup closes (or aligned mode deactivates) the placement flags are reset and the positioner's original inline styles (SEL-POS-04 list) restored; when the item map changes while open and aligned, both scroll-arrow flags are cleared and the inline `height` of positioner and popup is cleared before the next placement.
  Source: `select/popup/SelectPopup.tsx:217-225`, `select/positioner/SelectPositioner.tsx:196-205`. Test: [L4] `SelectRoot.test.tsx` "resets aligned positioning after controlled value reset and option replacement".
- **SEL-POS-14** — MUST. `data-side` is `"none"` on the Positioner, Popup and scroll arrows while aligned mode is active (so styles can target the aligned layout, e.g. hiding arrows only outside it).
  Source: `select/positioner/SelectPositioner.tsx:114,209-219`. Test: [L4] `SelectPopup.test.tsx` "keeps alignItemWithTrigger active at browser zoom". Docs: `CHANGELOG.md v1.0.0-beta.4 "Add \`Select.List\` component"`.
- **SEL-POS-15** — MUST. Even in aligned mode the trigger's `data-popup-side` reports the mechanism's resolved side (typically `bottom`), not `none` (SEL-DOM-05).
  Source: `select/positioner/SelectPositioner.tsx:124-126`. Test: [L4] `SelectTrigger.test.tsx` "sets data-popup-side to the resolved side when alignItemWithTrigger is active".
- **SEL-POS-16** — MUST. While aligned, the mechanism's anchor tracking (scroll/resize observers) is disabled unless `disableAnchorTracking` is explicitly `false` (SEL-API-38); the aligned geometry is measured once per placement.
  Source: `select/positioner/SelectPositioner.tsx:109`. Test: [L4] `SelectPopup.test.tsx` "closes the aligned popup when the window is resized" (resize closes instead of repositioning).
- **SEL-POS-17** — MUST. Direction (`DirectionProvider`, UTL-I18N-01) selects the horizontal alignment edge in SEL-POS-07 step 4 (LTR: left edges; RTL: right edges); the DOM `dir` attribute alone has no effect.
  Source: `select/popup/SelectPopup.tsx:66,304-314`. Test: [L4] `SelectPopup.test.tsx` "aligns the selected item with the trigger inline end in rtl", "rtl alignment" (both tests).
- **SEL-POS-18** — MUST. Rect and size measurements in placement and scroll-grow are divided by the element's rendered scale (FLT platform `getScale`) so CSS `zoom` and transforms on ancestors do not break alignment.
  Source: `select/popup/SelectPopup.tsx:102-107,278-281,544-563`. Test: [L4] `SelectPopup.test.tsx` "keeps alignItemWithTrigger active at browser zoom", "keeps scrolling the aligned list at browser zoom after the popup fills the available height".
- **SEL-POS-19** — MUST. The popup's inline `transform`, `scale` and `translate` are restored exactly (or removed when they were absent) after measurement.
  Source: `select/popup/SelectPopup.tsx:258,404-406,565-592`. Test: [L4] `SelectPopup.test.tsx` "restores transform-related inline styles after measurement". Docs: `CHANGELOG.md v1.1.0 "Fix support for individual transform animations when \`alignItemWithTrigger\` is active"`.
- **SEL-POS-20** — MUST. `Select.Arrow` renders only outside aligned mode (SEL-STR-09) and then carries the mechanism's arrow styles and `data-uncentered` (FLT).
  Source: `select/arrow/SelectArrow.tsx:23-46`. Test: [L4] `SelectArrow.test.tsx` conformance suite (rendered with `alignItemWithTrigger={false}`).
- **SEL-POS-21** — MUST. Page scroll is locked (UTL `useScrollLock` with the trigger as reference element, PSH-PTR-07) while the popup is open and (aligned mode is active or `modal` is `true`), with the touch exception of SEL-PTR-23.
  Source: `select/positioner/SelectPositioner.tsx:89-94`, `utils/useAnchoredPopupScrollLock.ts:18-43`. Test: [L4] `SelectRoot.test.tsx` "touch scroll lock" (both tests), "keeps touch interaction type when reopening quickly after close".
- **SEL-POS-22** — MUST. A scroll before the first placement of an open (the popup not yet measured) MUST neither resize the positioner nor update scroll-arrow visibility.
  Source: `select/popup/SelectPopup.tsx:86-88`. Test: [L4] `SelectPopup.test.tsx` "does not resize the positioner when the list scrolls before the popup is placed".
- **SEL-POS-23** — MUST. The scroller whose scroll events drive SEL-POS-11/12 and SEL-STA-29 is the List element when one is registered, else the Popup element; the List forwards its scroll events to the Popup's handler and the Popup ignores scroll events while a List exists.
  Source: `select/list/SelectList.tsx:36-38`, `select/popup/SelectPopup.tsx:176,451-456`. Test: [L4] `SelectPopup.test.tsx` "ignores scroll events bubbling to the popup while a Select.List owns scrolling", "grows the popup when it is itself the scroll container".
- **SEL-POS-24** — MUST. With `alignItemWithTrigger={false}` the selected item is revealed on open by scrolling it into view (CMP-STA-33, CMP-FOC-04) rather than by moving the popup.
  Source: `select/root/SelectRoot.tsx:371-386`. Test: [L4] `SelectRoot.test.tsx` "scrolls the first selected item into view on open".

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Rendering pipeline (`useRenderElement`), state → `data-*`, `mergeProps` | [RND](../architecture/rendering-and-composition.md) | Every part; custom mappings: Trigger = pressable trigger mapping + field validity mapping + `popupSide` → `data-popup-side` + `value` suppressed; Value = `value` suppressed; Icon = trigger mapping; Backdrop/Popup = popup mapping + transition mapping; Positioner = popup mapping (via `usePositioner`); Arrow = popup transition mapping; scroll arrows = transition mapping; Label = field validity mapping; Item/ItemText/List/Group/GroupLabel/Portal = default. Shared parts: `ItemIndicator` (RND §4.10), `ListboxSeparator` (RND §4.10). |
| Change-event details, reasons, cancel/propagation | [EVD](../architecture/event-details-and-reasons.md) | `Select.Root.ChangeEventReason` union (§3.19); `onValueChange` emits `item-press`/`none`; `onOpenChange` emits the ten reasons of §6.4/§7; `preventUnmountOnClose()` on open-change details; simplified value sequence EVD-ALG-03. |
| Button mechanism | [UBT](../architecture/button-behavior.md) | Trigger: `native: nativeButton` (default `true`), `disabled: field \|\| root \|\| prop`, `composite` inferred. Item: `native: nativeButton` (default `false`), `disabled: root \|\| prop`, `focusableWhenDisabled: true`, `composite: true`. Attribute-only disabled predicate UBT-ALG-08 for typeahead `disabledIndices`. |
| Field / Form infrastructure | [FFI](../architecture/field-and-form-infrastructure.md) | §6.3 rows `Select.Root`/`Select`: control = trigger element, raw value, string projection, hidden input = validation input, `getValidationProps` on trigger and hidden input, `useSetFieldFocused(disabled, triggerRef)`, `useLabelableId({ id })` on Root and Trigger, `useLabel` (non-native) in `Select.Label`, `resolveAriaLabelledBy(fieldLabelId, selectLabelId)`. |
| Item registry, list navigation, typeahead | [CMP](../architecture/composite-and-list-navigation.md) | Registry provided by Positioner (`elementsRef`, `labelsRef`, `onMapChange` → SEL-STA-12/13). Items register with `guess: true`, `label`, `textRef`. List navigation: `enabled: !disabled`, `activeIndex`, `selectedIndex`, `disabledIndices: []`, `onNavigate` (ignores `null` while closed), `focusItemOnHover: highlightItemOnHover`; all other options default. Typeahead: `enabled: !disabled && (open \|\| (!readOnly && !multiple))`, `listRef: labelsRef`, `activeIndex`, `selectedIndex`, `disabledIndices: attribute-disabled predicate`, `onMatch` (open → highlight; closed → value), `onTyping` → typing flag; `resetMs` default 750. |
| Anchor positioning, click/dismiss interactions, focus manager, portal | [FLT](../architecture/floating-and-positioning.md) | `useAnchorPositioning` with §3.8 props, `keepMounted: true`, `disableAnchorTracking ?? alignedActive`; `usePositioner({ hidden: !mounted, inert: !open })`; `useClick({ enabled: !disabled, event: 'mousedown' })`; `useDismiss()` defaults; `FloatingFocusManager({ modal: false, disabled: !mounted, openInteractionType: openMethod, returnFocus: finalFocus, restoreFocus: true })`; `FloatingPortal` with Portal props; floating root context with trigger as reference and positioner as floating element. |
| Popup store helpers | [PSH](../architecture/popup-store-and-handles.md) | No popup store / handle (Select keeps its own store); uses `FOCUSABLE_POPUP_PROPS` on the Popup (PSH-DOM-15), `useOpenInteractionType` (PSH-PTR-01…03), `useAnchoredPopupScrollLock((alignedActive \|\| modal) && open, openMethod === 'touch', positioner, trigger)` (PSH-PTR-07), `InternalBackdrop` with `cutout: trigger`, `inert: !open` (PSH-DOM-14, PSH-PTR-05/06), `pressableTriggerOpenStateMapping` / `popupStateMapping` / `popupTransitionStateMapping` / `triggerOpenStateMapping`, `attachPreventUnmountOnClose`. |
| Transitions and mounting | [TRN](../architecture/transitions-and-mounting.md) | `useUnmountAfterClose({ open, ref: popupRef, preventUnmountOnClose, onUnmount })` (TRN §6.4); `useOpenChangeComplete` on Popup (`open`), scroll arrows (`visible`), ItemIndicator (`selected`, batch); `useTransitionStatus` on scroll arrows and indicator; `getDisabledMountTransitionStyles` on Positioner and Popup. |
| Shared utilities | [UTL](../architecture/shared-utilities.md) | `useControlled` (`name: 'Select'`, `state: 'value'`/`'open'`); `ReactStore` with the §4 record and selectors; `Timeout` (trigger: 0 ms focus force-mount, 0 ms mouseup listener, 400 ms selection delay; scroll arrows: 40 ms step); `AnimationFrame` (popup: arrow visibility after placement); `useScrollLock` (via PSH); `visuallyHidden`/`visuallyHiddenInput`; `isElementDisabled`; `getPseudoElementBounds`/`isMouseWithinBounds` (5 px); `scrollEdges` (`SCROLL_EDGE_TOLERANCE_PX = 1`, `getMaxScrollOffset`, `normalizeScrollOffset`); `resolveValueLabel` (`stringifyAsValue`, `stringifyAsLabel`, `resolveSelectedLabel`, `resolveMultipleLabels`, `hasNullItemLabel`); `itemEquality` (`defaultItemEquality`, `compareItemEquality`, `findItemIndex`, `findSelectionIndex`, `resolveSelectedIndex`, `isSelectedValueDirty`, `removeItem`); `useValueChanged`; `usePreviousValue`; `DirectionProvider` (`useDirection`); `CSPProvider` (`nonce`, `disableStyleElements`); `styleDisableScrollbar`; `platform.engine.webkit`, `platform.os.ios`; `DROPDOWN_COLLISION_AVOIDANCE`. |

## 12. Edge cases, invariants and dev warnings

The `select/` sources contain no component-specific `warn(`, `console.error(` or development-only branch of their own; every diagnostic a Select can emit comes from a shared mechanism (SEL-EDGE-02/03). The thrown errors are the four missing-ancestor errors below.

- **SEL-EDGE-01** — MUST. The missing-ancestor errors are thrown with exactly these messages: `Base UI: SelectRootContext is missing. Select parts must be placed within <Select.Root>.` (SEL-STR-02); `Base UI: SelectFloatingContext is missing. Select parts must be placed within <Select.Root>.` (Positioner and Popup, same condition as the first — a Root always provides both); `Base UI: SelectPositionerContext is missing. SelectPositioner parts must be placed within <Select.Positioner>.` (SEL-STR-03); `Base UI: SelectItemContext is missing. SelectItem parts must be placed within <Select.Item>.` (SEL-STR-04); `Base UI: SelectGroupContext is missing. SelectGroup parts must be placed within <Select.Group>.` (SEL-STR-05).
  Source: `select/root/SelectRootContext.ts:14-16,24-26`, `select/positioner/SelectPositionerContext.ts:20-22`, `select/item/SelectItemContext.ts:16-18`, `select/group/SelectGroupContext.ts:14-16`. Test: [L2] `SelectTrigger.test.tsx` "throws a descriptive error when rendered outside <Select.Root>"; `SelectPopup.test.tsx` "throws a descriptive error when rendered outside <Select.Positioner>"; `SelectItemText.test.tsx` "throws a descriptive error when rendered outside <Select.Item>"; `SelectGroupLabel.test.tsx` "throws a descriptive error when rendered outside <Select.Group>".
- **SEL-EDGE-02** — SHOULD. The controlled-state diagnostics UTL-EDGE-22/23 are emitted with `name: 'Select'` and `state: 'value'` (switching `value` between defined and `undefined` after mount, or changing `defaultValue` after mount) or `state: 'open'` (same for `open`/`defaultOpen`). The store's synced-values diagnostic UTL-EDGE-26 can never fire (the key set is constant).
  Source: `select/root/SelectRoot.tsx:102-114,465-485`. Test: none.
- **SEL-EDGE-03** — SHOULD. The button-mechanism diagnostics UBT-EDGE-01/02 apply to the Trigger (default native: rendering a non-`<button>` without `nativeButton={false}` is reported) and to the Item (default non-native: substituting a `<button>` without `nativeButton` is reported).
  Source: `select/trigger/SelectTrigger.tsx:93-96`, `select/item/SelectItem.tsx:120-125`. Test: none.
- **SEL-EDGE-04** — MUST. *Disabled summary.* With the Root disabled (SEL-API-13): the trigger cannot be focused or pressed (native) / is out of the tab order (non-native); no pointer, keyboard or typeahead interaction opens the popup; a popup forced open (`open`/`defaultOpen`) still renders and can be dismissed (Escape, outside press) but its items are all disabled and commit nothing; hidden inputs are disabled; autofill is ignored; the field does not register the control (no validation, no `data-*` field flags for it).
  Source: `select/root/SelectRoot.tsx:99,250,365,372,391,505,528,575`, `select/item/SelectItem.tsx:61,136`. Test: [L4] `SelectRoot.test.tsx` "sets the disabled state", "does not commit item selection when root is disabled and forced open", "ignores hidden-input autofill when disabled $label"; `SelectItem.test.tsx` "inherits the disabled state from the root".
- **SEL-EDGE-05** — MUST. *Read-only summary.* With `readOnly` (SEL-API-12): everything opens and navigates normally; item activation (click, drag-release, Enter, Space) commits nothing and in single mode still requests the close (SEL-EVT-12); closed-trigger typeahead is off; autofill is ignored; the hidden input is `readonly`; `aria-readonly` appears on trigger and listbox.
  Source: `select/root/SelectRoot.tsx:361-367,388-391,528,577`, `select/item/SelectItem.tsx:136-138`. Test: [L4] `SelectRoot.test.tsx` "prop: readOnly" (all tests), "does not commit item selection when root is readOnly and forced open".
- **SEL-EDGE-06** — MUST. When the popup is forced open by the consumer while the Root is disabled or `readOnly`, item activation MUST neither invoke `onValueChange` nor mark the item selected (the commit itself re-checks both flags, independently of the interaction gates).
  Source: `select/item/SelectItem.tsx:133-138`. Test: [L2] `SelectRoot.test.tsx` "does not commit item selection when root is %s and forced open".
- **SEL-EDGE-07** — MUST. Typeahead treats attribute-disabled items as unavailable: a keystroke whose only matches are disabled commits/highlights nothing, a disabled item whose label starts with a doubled letter does not disable rapid same-letter cycling among the enabled items, and after a closed-state value reset the search starts from the first match again.
  Source: `select/root/SelectRoot.tsx:400`, `floating-ui-react/hooks/useTypeahead.ts:98-109,167-180`. Test: [L4] `SelectRoot.test.tsx` "commits nothing when the only typeahead match is disabled (closed trigger)", "does not let a disabled double-letter item block rapid cycling among enabled matches", "starts from the first match after value reset (closed)".
- **SEL-EDGE-08** — MUST. Duplicate item values: every item whose value equals the selected value reports selected (`aria-selected="true"`, `data-selected`); the selected index (and therefore the item opened on / aligned to) is the lowest registered index among them; the first registered item wins autofill and typeahead matching ties.
  Source: `internals/itemEquality.ts:53-67`, `select/store.ts:135-150`. Test: none.
- **SEL-EDGE-09** — MUST. With zero items the popup still opens and closes normally; arrow/Home/End keys and typeahead do nothing; the focus-on-open sync gives up after checking immediately, after a microtask and after the next frame (CMP-FOC-09); the hidden input serializes `''`; item reconciliation does not run (SEL-STA-14).
  Source: `floating-ui-react/hooks/useListNavigation.ts:440-477`, `select/positioner/SelectPositioner.tsx:143-145`. Test: [L4] popup-list suite "opens and handles keys without losing focus when the list is empty", "does not activate anything when every item is disabled".
- **SEL-EDGE-10** — MUST. Items removed while the popup is open leave no navigable gap: arrow keys skip vacated indices and stop at the last remaining item; if the removed item was selected, reconciliation (SEL-STA-12) runs after the registry publishes.
  Source: `floating-ui-react/utils/composite.ts` (index helpers skip `null` slots), `select/positioner/SelectPositioner.tsx:141-207`. Test: [L4] `SelectRoot.test.tsx` "skips null items when navigating", "unselects the selected item if removed".
- **SEL-EDGE-11** — MUST. Server-rendered output contains the trigger (with its generated id), the Label (with `${id}-label`) and the hidden input, but no `aria-labelledby` on the trigger (FFI-FRM-34) and no popup subtree unless the Select is initially open; the first client render matches.
  Source: `select/portal/SelectPortal.tsx:18-25`, `select/label/SelectLabel.tsx:34-42`. Test: [L2] `SelectRoot.test.tsx` "does not link Select.Label before hydration".
- **SEL-EDGE-12** — MUST. Inside a `Toolbar.Root` with the Trigger rendered through `Toolbar.Button`: the trigger keeps `role="combobox"` (SEL-ARIA-01), the toolbar's roving `tabindex` applies to it (UBT composite inference), opening with Enter focuses the selected item, and composite keys inside the popup do not reach the toolbar (SEL-KEY-14).
  Source: `select/trigger/SelectTrigger.tsx:214-216`, `select/popup/SelectPopup.tsx:65,446-450`. Test: [L4] `SelectPopup.test.tsx` "keeps arrow keys inside the popup when the trigger lives in a toolbar".
- **SEL-EDGE-13** — MUST. Inside another popup (Popover, Dialog): the Select is a child in the popup tree, so its Escape does not close the parent (SEL-KEY-10), its outside-press bookkeeping does not consume the parent's next outside press (SEL-DIS-10), focus returns to the opener inside the parent after a programmatic open (SEL-FOC-04), and the trigger-release listener (SEL-PTR-04) is cleared on close so a native drag that ends outside does not leave a stale listener.
  Source: `select/trigger/SelectTrigger.tsx:120-126`, `floating-ui-react/hooks/useDismiss.ts`. Test: [L4] `SelectRoot.test.tsx` "select inside popover" (all six tests).
- **SEL-EDGE-14** — MUST. Platform cases: on WebKit a pinch-zoomed visual viewport forces the positioning fallback (SEL-POS-04) and zero-movement `mousemove` events are ignored for hover highlighting (CMP-PTR-08); on iOS a trigger press that fires `mousedown` without `pointerdown` records the open method as `touch` (PSH-PTR-02); Firefox may dispatch `mouseup` during `mousedown`, which the 0 ms deferral of SEL-PTR-04 ignores; Android Chrome without `focus({ preventScroll })` support skips focus return after an outside press (SEL-FOC-04).
  Source: `select/popup/SelectPopup.tsx:355-356`, `select/trigger/SelectTrigger.tsx:203-206`, `utils/useOpenInteractionType.ts:19-23`, `floating-ui-react/components/FloatingFocusManager.tsx:814-836`. Test: [L4] `SelectPopup.test.tsx` "ignores a pinch-zoomed visual viewport outside WebKit".
- **SEL-EDGE-15** — MUST. A controlled multiple-mode `value` of `null` is treated as an empty selection (SEL-STA-11) and the first activation requests `[item]`; a single-mode `value` of `null` selects nothing unless a `null`-valued item exists, in which case that item is selected (SEL-STA-03 with `Object.is(null, null)`).
  Source: `select/item/SelectItem.tsx:142`, `internals/itemEquality.ts:18-19`. Test: [L2] `SelectItem.test.tsx` "treats a null value as an empty selection in multiple mode"; `SelectValue.test.tsx` "null item label in items takes precedence over placeholder".
- **SEL-EDGE-16** — MUST. An Item rendered outside any Positioner (no item registry) has index `-1`, registers no value or label, never holds the selected index, is never highlighted by navigation, and clicking it still commits its value (SEL-PTR-08) — the Root does not forbid it.
  Source: `select/item/SelectItem.tsx:47-51,71-78`; CMP-STR-03. Test: none.
- **SEL-EDGE-17** — MUST. Several `Select.Value` parts may be rendered; each resolves its content independently, and the last one to attach is the alignment value reference (SEL-DOM-12). Several `Select.Label` parts register the same id (`${rootId}-label`) and the trigger references that id; the DOM then contains duplicate ids (consumer error, not guarded).
  Source: `select/value/SelectValue.tsx:62`, `select/label/SelectLabel.tsx:32`. Test: none.
- **SEL-EDGE-18** — MUST. A `Select.GroupLabel` id change (explicit ↔ generated) MUST update the group's `aria-labelledby` without re-creating the label element, and the group MUST never end up referencing a removed label (SEL-API-46).
  Source: `select/group-label/SelectGroupLabel.tsx:25-30`. Test: [L2] `SelectGroupLabel.test.tsx` "updates explicit and generated ids independently of ref churn", "replaces and unregisters its label in Strict Mode".
- **SEL-EDGE-19** — MUST. `items` lookup in record form uses own-property lookup for label resolution but the `in` operator (prototype chain included) for the null-label check (UTL §10 Q7); a record with a `"null"` key therefore both labels a `null` value and suppresses the placeholder.
  Source: `internals/resolveValueLabel.tsx:43,113`. Test: [L2] `SelectValue.test.tsx` "can lookup null value", "null key label in object items takes precedence over placeholder". Docs: `CHANGELOG.md v1.8.0 "Fix record item label lookup reading from \`Object.prototype\`"`.
- **SEL-EDGE-20** — MUST. A `Select.Value` `children` function or `items` lookup that throws, or an `itemToStringValue` that throws for a value, surfaces as an ordinary render error; the component performs no guarding (the only guarded stringification is UTL `serializeValue`'s JSON fallback).
  Source: `internals/serializeValue.ts:8-12`. Test: [L2] `SelectRoot.test.tsx` "does not invoke itemToStringValue with the value array in multiple mode" (rendering succeeds as the regression guard).
- **SEL-EDGE-21** — MUST. A `render` callback is opaque (RND-DOM-32): the part hands it the computed `id` and does not detect whether the callback applies it, and Select adds no fallback. IDREFs that Select computes from an id rather than reading from the DOM therefore dangle when a callback drops the `id`: the trigger's `aria-labelledby` (the `Select.Label` id, SEL-ARIA-05), a `Field.Label`'s `for` (the trigger id, SEL-FRM-14) and a group's `aria-labelledby` (the GroupLabel id, SEL-DOM-41). The trigger's `aria-controls` reads the List/Popup element's actual `id` (SEL-ARIA-04), so a callback that drops the list or popup id yields an empty `aria-controls` rather than a reference to a missing element. Consumer callbacks are expected to spread the props they receive, `id` included, onto their element. The same three IDREFs also dangle when the substitute is an *element* carrying its own different `id` (which wins on the element, RND-DOM-32): `Select.Label` registers the root-derived `<rootId>-label` (a runtime `id` prop is deleted) and `Select.GroupLabel` its `id` prop or generated id (`useBaseUiId`), never the rendered id (RND-DOM-54).
  Source: `internals/resolveRenderedId.ts:9-14`, `select/trigger/SelectTrigger.tsx:84,137-141`, `select/label/SelectLabel.tsx:33-43`, `select/group-label/SelectGroupLabel.tsx:23-35`, `select/group/SelectGroup.tsx:34`; see RND-DOM-32. Test: none.
- **SEL-I18N-01** — MUST. Reading direction (`DirectionProvider`, default `ltr`) affects only the horizontal edge used by aligned positioning (SEL-POS-17); keyboard navigation is vertical and unaffected, and no DOM `dir` attribute is read.
  Source: `select/popup/SelectPopup.tsx:66,304-314`, `select/root/SelectRoot.tsx:371-386` (`rtl` not passed). Test: [L4] `SelectPopup.test.tsx` "aligns the selected item with the trigger inline end in rtl".

## 13. Behaviors evidenced by tests

Mapping of every test to requirements. The per-ID `Test:` lines are authoritative; this table is the derived view. Conformance suites: `describeConformance` → RND-DOM-36…48 (+RND-DOM-49 where `button: true`) and SEL-DOM-55.

### `SelectRoot.test.tsx`

| Test | Requirements |
|---|---|
| "exposes the orientation attribute rendered by Separator" | SEL-API-48, SEL-DOM-46 |
| "automatically unmounts with an actions ref and completes closing once" | SEL-API-17, SEL-ANI-06, SEL-EVT-11, SEL-KEY-10 |
| "keeps the popup mounted until the unmount action completes closing" | SEL-EVT-10, SEL-ANI-06, SEL-ANI-09 |
| "clears the opt-out when a controlled reopen interrupts a pending unmount" | SEL-ANI-09, SEL-EVT-10 |
| "ignores an opt-out on a canceled close" | SEL-EVT-09, SEL-ANI-09, SEL-DIS-12 |
| "keeps the opt-out when a controlled close is applied in a transition" | SEL-ANI-09, SEL-EVT-10 |
| "closes through the `close` action so `onOpenChange` can opt out" | SEL-API-17, SEL-DIS-08, SEL-EVT-08, SEL-EVT-10 |
| "ignores `unmount` while the popup is open" | SEL-ANI-06, SEL-ANI-08 |
| "unmounts when `close` and `unmount` are called in one batch" | SEL-ANI-08, SEL-EVT-10 |
| "still unmounts on a later close after `unmount` was called while open" / "still unmounts on a later close after `unmount` and a reopen in one batch" | SEL-ANI-06 |
| "does not call `onOpenChange` when the `close` action is called while closed" | SEL-API-17, SEL-DIS-08 |
| "completes closing once when `unmount` is called twice in one batch" | SEL-ANI-06 |
| Popup conformance suite (options: `triggerMouseAction: 'click'`, `expectedPopupRole: 'listbox'`, `alwaysMounted: 'only-after-open'`) — "opens the popup with the `open` prop", "opens initially and remains uncontrolled", "is ignored when `open={false}` is passed", "is ignored when `open={true}` is passed, so Escape cannot close", "is called with the new open state and the change details", "keeps the popup closed when the open change is canceled", "has the listbox role on the popup", "has the `aria-controls` attribute on the trigger", "opens on trigger click and sets `aria-expanded` on the trigger", "has the `aria-haspopup` attribute on the trigger", "allows a custom `id` prop", "prop: onOpenChangeComplete" (all), "animations" (all) | SEL-API-04, SEL-ANI-10, SEL-DIS-12, SEL-EVT-08, SEL-EVT-09, SEL-ARIA-01…04, SEL-DOM-23, SEL-STR-06, SEL-ANI-05, SEL-ANI-06, SEL-ANI-01 |
| Popup-list conformance suite (options: `itemRole: 'option'`, `selectable: true`, `listEnd: 'stop'`, `alignItemWithTrigger={false}`, defaults `focusModel: 'dom'`, `homeEnd`, `spaceActivates`, `enterSpaceOpen`, `typeahead`, `modal`, `triggerClickCloses`) — pointer: "highlights and focuses the hovered item", "clears the highlight when the pointer leaves the list without leaving focus on an item", "continues keyboard navigation from the hovered item", "activates the hovered item with Enter", "lets the pointer take the highlight back after keyboard navigation", "keeps the highlight when a touch scroll starts on the highlighted item", "does not activate a disabled item", "ignores right clicks on the trigger and on items" | SEL-PTR-13, SEL-KEY-08, SEL-PTR-09, SEL-PTR-10, SEL-PTR-02 |
| Popup-list suite — opening: "starts without a highlight after a pointer opening", "opens on the first item with ArrowDown", "opens on the last item with ArrowUp", "scrolls the last item into view when opening with ArrowUp", "opens past a disabled first item", "opens with {Enter} without activating an item", "opens with   without activating an item", "starts fresh when reopened after Escape", "opens on the selected item with {ArrowDown}/{ArrowUp}", "marks only the selected item as selected", "reveals and highlights the selected item when opening" | SEL-FOC-02, SEL-KEY-01, SEL-KEY-02, SEL-KEY-03, SEL-KEY-10, SEL-ARIA-09, SEL-FOC-03, SEL-KEY-20 |
| Popup-list suite — keyboard: "moves the highlight one item at a time with the arrow keys", "moves focus along with the highlight", "activates the highlighted item with {Enter}/ ", "does not submit a surrounding form", "stops on a disabled item while navigating", "moves the highlight to the last and first items with End and Home", "moves to disabled ends with End and Home", "reveals an item reached by typing its label", "starts a new typeahead search after a pause", "prevents the default scrolling of navigation keys" | SEL-KEY-06, SEL-FOC-03, SEL-KEY-08, SEL-KEY-09, SEL-KEY-07, SEL-KEY-12, SEL-KEY-20 |
| Popup-list suite — list boundaries: "stops past the first item", "stops past the last item", "keeps a single item reachable", "opens and handles keys without losing focus when the list is empty", "does not activate anything when every item is disabled" | SEL-KEY-06, SEL-EDGE-09 |
| Popup-list suite — dismissal: "closes on Escape without committing, and keeps focus on the trigger", "closes on Tab without committing, and moves focus past the trigger", "closes on Shift+Tab without committing, and moves focus to the element before the popup", "closes on an outside press without committing", "closes when the trigger is clicked again", "closes only itself on Escape inside a dialog" | SEL-KEY-10, SEL-FOC-04, SEL-KEY-11, SEL-FOC-09, SEL-FOC-12, SEL-DIS-02, SEL-PTR-19, SEL-DIS-04, SEL-DIS-10 |
| "does not link Select.Label before hydration" | SEL-DOM-14, SEL-FRM-15, SEL-EDGE-11 |
| "should select the item by default" | SEL-API-02 |
| "should select the item specified by the value prop" / "selects the controlled value when the popup is mounted on the initial render" / "should update the selected item when the value prop changes" | SEL-API-01, SEL-STA-03, SEL-STA-05, SEL-EVT-06 |
| "should not update the internal value if the controlled value prop does not change" | SEL-API-01, SEL-EVT-07 |
| "updates <Select.Value /> label when the value prop changes before the popup opens" | SEL-DOM-10 |
| "uses itemToStringValue for form submission" / "uses itemToStringValue for multiple selection form submission" / "does not invoke itemToStringValue with the value array in multiple mode" | SEL-API-21, SEL-DOM-51, SEL-DOM-52, SEL-DOM-53, SEL-EVT-16, SEL-EDGE-20 |
| "uses itemToStringLabel for trigger text when value is object" / "updates trigger text with itemToStringLabel after selecting object item" | SEL-API-20, SEL-DOM-10 |
| "should call onValueChange when an item is selected" / "is not called twice on select" | SEL-API-03, SEL-EVT-01, SEL-EVT-07, SEL-PTR-12 |
| "should open the select by default" | SEL-API-04, SEL-ARIA-08, SEL-DOM-23 |
| "should select an item and close when clicked while opened by default" | SEL-DIS-07, SEL-PTR-08 |
| "does not dismiss when pressing portalled content inside the popup but outside the list" | SEL-PTR-20 |
| "should handle browser autofill" / "should handle browser autofill with object values" / "should handle browser autofill with object values when autofill uses the label" / "matches browser autofill against an item rendered label for primitive values regardless of case" / "matches browser autofill by serialized value before an earlier rendered label" / "matches browser autofill when an earlier item has no rendered label" | SEL-FRM-18, SEL-STA-09, SEL-DOM-47, SEL-API-07 |
| "ignores browser autofill in multiple mode" / "leaves the value untouched when autofill matches no item" / "ignores hidden-input autofill when $lockState $label" (4 cases) | SEL-FRM-19, SEL-FRM-12, SEL-EDGE-04 |
| "redirects focus to the trigger when the hidden input is focused" | SEL-FOC-06 |
| "should pass autoComplete to the hidden input" | SEL-API-09 |
| "marks the field dirty and validates after successful autofill" | SEL-FRM-18, SEL-EVT-13, SEL-FRM-06 |
| "does not update field state when autofill is canceled" | SEL-EVT-02, SEL-EVT-03, SEL-FRM-09 |
| "removes [data-dirty] in multiple mode after returning to the initial value" / "compares object values with isItemEqualToValue when clearing [data-dirty] in multiple mode" / "keeps [data-dirty] in multiple mode when the same values return in a different order" | SEL-FRM-04, SEL-API-22 |
| "does not invoke isItemEqualToValue with the value array in multiple mode when empty" | SEL-API-22 |
| "does not commit item selection when root is %s and forced open" (disabled, readOnly) | SEL-EDGE-06, SEL-EDGE-04, SEL-EDGE-05 |
| "should render an internal backdrop when `true`" / "should not render an internal backdrop when `false`" | SEL-API-16, SEL-STR-07, SEL-DOM-54, SEL-PTR-19, SEL-ARIA-15 |
| "keeps touch interaction type when reopening quickly after close" | SEL-STA-08, SEL-PTR-03, SEL-POS-21 |
| "keeps touch positioning during the close transition" | SEL-STA-08, SEL-POS-06, SEL-DOM-20, SEL-ANI-03 |
| "keeps the selected item highlighted when reopening after a touch-driven mouseleave" | SEL-PTR-09 |
| "recomputes positioning before the popup becomes visible again after touch dismiss" | SEL-POS-06, SEL-DIS-02 |
| "applies scroll lock when a touch-opened popup covers the viewport width" / "does not apply scroll lock when a touch-opened popup is narrower than the viewport" | SEL-PTR-23, SEL-POS-21 |
| "unmounts the select when the `unmount` method is called" | SEL-API-17, SEL-DIS-04 |
| "clears scroll arrow visibility when manually unmounted (strict: %s)" | SEL-ANI-07, SEL-STA-29, SEL-DOM-44 |
| "does not leave a tabbable option while closed and kept mounted after tabbing out" | SEL-DOM-36, SEL-FOC-01, SEL-KEY-11, SEL-ANI-13 |
| "normalizes overlapping fractional scroll ranges when toggling scroll arrow visibility" | SEL-STA-29, SEL-POS-12, SEL-DOM-43 |
| "keeps a scroll arrow mounted while its exit animation runs" | SEL-STR-10, SEL-ANI-11, SEL-DOM-45 |
| "dismisses the popover with one outside press after reopening an aligned select" / "dismisses the popover after reopening a selected aligned select" / "keeps a modal popover open when choosing a non-modal aligned select item" / "does not bubble Escape from a non-modal select to the popover" / "returns focus to the opener when a select is opened programmatically inside a popover" / "does not consume the next outside press after a native drag from a modal select trigger outside all popups" | SEL-DIS-10, SEL-EDGE-13, SEL-API-16, SEL-KEY-10, SEL-DIS-01, SEL-FOC-04, SEL-PTR-04 |
| "sets the disabled state" / "updates the disabled state when the disabled prop changes" | SEL-API-13, SEL-DOM-03, SEL-DOM-07, SEL-KEY-19, SEL-PTR-21, SEL-EDGE-04 |
| "sets the readOnly state" / "opens the popup when clicked" / "marks the listbox as readonly when rendered with Select.List" / "opens the popup with $name" (ArrowDown, Enter, Space) / "does not commit a value when an item is clicked in a popup the user opened" / "does not commit a value with Enter on a highlighted item" / "does not commit a value with typeahead on a closed trigger" / "moves the highlight with typeahead while the popup is open" | SEL-API-12, SEL-ARIA-06, SEL-DOM-03, SEL-DOM-23, SEL-DOM-31, SEL-KEY-18, SEL-KEY-01, SEL-KEY-03, SEL-KEY-04, SEL-KEY-08, SEL-KEY-12, SEL-EDGE-05 |
| "sets the id on the trigger" / "sets a hidden input id when name is not provided" / "does not set a hidden input id when name is provided" | SEL-API-10, SEL-DOM-09, SEL-DOM-48 |
| "applies the root id to the trigger" / "should receive disabled prop from Field.Root" / "should receive name prop from Field.Root" | SEL-FRM-23, SEL-FRM-14, SEL-API-13, SEL-API-07, SEL-FRM-10, SEL-FRM-11 |
| "resets selected index when value is set to null without a null item" | SEL-STA-03, SEL-STA-04 |
| "does not force-mount the popup on a programmatic value change" | SEL-STR-06 |
| "submits stringified value to onFormSubmit when itemToStringValue is provided" | SEL-FRM-01, SEL-FRM-02, SEL-API-21 |
| "submits to an external form when `form` is provided" / "submits multiple values to an external form when `form` is provided" | SEL-API-08, SEL-FRM-13, SEL-DOM-53 |
| "triggers native HTML validation on submit" | SEL-API-11, SEL-FRM-08 |
| "clears external errors on change" / "revalidates immediately after form submission errors" | SEL-FRM-06, SEL-FRM-09, SEL-FRM-17, SEL-ARIA-07, SEL-FRM-01, SEL-FRM-08, SEL-EVT-13 |
| "[data-touched]" | SEL-FRM-03 |
| "[data-dirty]" | SEL-FRM-04 |
| "adds [data-filled] attribute when filled" / "adds [data-filled] attribute when already filled" / "does not add [data-filled] attribute when single value serializes to empty string" / "does not add [data-filled] attribute when a non-string value serializes to empty string" / "does not add [data-filled] attribute when multiple value is empty" / "does not add [data-filled] attribute when multiple defaultValue is empty array" | SEL-FRM-05, SEL-STA-02, SEL-DOM-06 |
| "[data-focused]" / "is removed when the focused trigger becomes disabled" / "is removed when the focused trigger unmounts" | SEL-FOC-10, SEL-FRM-20, SEL-DOM-06 |
| "does not mark as touched when focus moves into the popup" / "validates when the popup is blurred" | SEL-FRM-03, SEL-FRM-07, SEL-FOC-10, SEL-FOC-12, SEL-DIS-03 |
| "prop: validate" / "passes raw value to validate when itemToStringValue is provided" | SEL-FRM-07, SEL-FRM-02, SEL-FRM-17, SEL-ARIA-07 |
| "prop: validateMode=onSubmit" | SEL-FRM-08, SEL-FRM-17, SEL-KEY-17, SEL-KEY-08 |
| "prop: validationMode=onChange" / "revalidates when the controlled value changes externally" | SEL-FRM-06, SEL-EVT-06, SEL-EVT-13 |
| "prop: validationMode=onBlur" | SEL-FRM-07 |
| "Field.Label" / "Select.Label" / "does not set fallback aria-labelledby when no label is rendered" / "updates Select.Label linkage when root id changes" | SEL-ARIA-05, SEL-FRM-14, SEL-FRM-15, SEL-DOM-14, SEL-API-10 |
| "Select.Label focuses trigger without opening" | SEL-DOM-15, SEL-PTR-24 |
| "Field.Label links to trigger and focuses it" / "Field.Label links to trigger when trigger has an explicit id" | SEL-FRM-14, SEL-API-26, SEL-PTR-24 |
| "Field.Description" | SEL-FRM-16, SEL-ARIA-07 |
| "skips null items when navigating" | SEL-EDGE-10, SEL-KEY-01, SEL-KEY-06 |
| "unselects the selected item if removed" / "resets the value when the selected item is replaced and the item count is unchanged" / "resets the value when every item is replaced and the item count is unchanged" / "keeps the value when the items are only reordered" / "resets to default when the selected item is removed from the list" / "resets via onValueChange and does not break in controlled mode when the selected item is removed" / "falls back to null when both selected and initial default are removed (uncontrolled)" / "falls back to null when both selected and initial default are removed (controlled)" | SEL-STA-12, SEL-STA-14, SEL-STA-31, SEL-EVT-02, SEL-EDGE-10 |
| "resets aligned positioning after controlled value reset and option replacement" / "resets aligned positioning when value reset, option replacement, and controlled open happen together" / "resets aligned positioning when a stale controlled value, option replacement, and controlled open happen together" | SEL-POS-07, SEL-POS-09, SEL-POS-13 |
| "matches an item by its label when the rendered text differs" | SEL-API-42, SEL-KEY-12 |
| "starts a new search after focus leaves the select" | SEL-KEY-04, SEL-FOC-11 |
| "does not trigger selection when Space is pressed during text navigation" | SEL-KEY-09, SEL-STA-32 |
| "skips disabled items and commits the next match via typeahead on a closed trigger" / "commits typeahead on a closed trigger when items are provided" / "commits nothing when the only typeahead match is disabled (closed trigger)" / "does not let a disabled double-letter item block rapid cycling among enabled matches" / "starts from the first match after value reset (closed)" / "does not jump matches after a closed-state value reset" | SEL-KEY-04, SEL-EDGE-07, SEL-STR-06, SEL-STA-09, SEL-FOC-11, SEL-STA-04 |
| "skips disabled items when highlighting via typeahead on an open popup" | SEL-KEY-12 |
| "removes selections that no longer exist" / "removes selections replaced without changing the item count" / "keeps the selection when items are added and none of the selected ones are removed" | SEL-STA-13, SEL-STA-14 |
| "should allow multiple selections when multiple is true" / "should deselect items when clicked again in multiple mode" / "should update selected items when the controlled value prop changes while open" / "should update selected items when value prop changes" | SEL-API-14, SEL-EVT-05, SEL-STA-03, SEL-DIS-07 |
| "keeps the active index on a deselected item in multiple mode" / "keeps the highlight on an item deselected with the keyboard" | SEL-STA-07, SEL-PTR-14, SEL-KEY-13, SEL-ARIA-09 |
| "highlights the first selected item in rendered order regardless of value order" / "anchors to the first selected item in rendered order across groups" / "anchors to the first selected item when opened with the keyboard" / "continues arrow navigation from the anchor rather than the top of the list" | SEL-STA-04, SEL-STA-06, SEL-KEY-01, SEL-KEY-06, SEL-FOC-01 |
| "re-elects the anchor when the anchor item value changes in place" / "moves the anchor to the next selected item when the anchor item leaves the list" / "re-anchors backwards when a new isItemEqualToValue identity re-runs every item" | SEL-STA-06 |
| "aligns the first selected item with the trigger when alignItemWithTrigger is active" | SEL-POS-07, SEL-DOM-26, SEL-STA-06 |
| "scrolls the first selected item into view on open" | SEL-POS-24, SEL-FOC-03 |
| "should handle defaultValue as array in multiple mode" | SEL-API-02, SEL-STA-03 |
| "should serialize multiple values correctly for form submission" / "does not submit multiple values when disabled" / "should serialize empty array as empty string in multiple mode" | SEL-STR-01, SEL-DOM-53, SEL-DOM-51, SEL-DOM-52, SEL-FRM-11, SEL-FRM-21 |
| "does not mark the hidden input as required when selection exists" / "keeps the hidden input required when no selection exists" | SEL-API-11, SEL-DOM-50, SEL-FRM-22 |
| "should not close popup when selecting items in multiple mode" / "should close popup in single select mode" | SEL-DIS-07, SEL-API-14 |
| "matches object values using the provided comparator" / "matches object values when the popup is mounted on the initial render" / "passes item as the first comparator argument in multiple mode" | SEL-API-22, SEL-STA-03, SEL-STA-05, SEL-EVT-15, SEL-EVT-05 |
| "highlights an item on mouse move by default" / "does not highlight items from mouse movement when disabled" / "does not remove highlight when mousing out of popup when disabled" | SEL-API-15, SEL-PTR-13, SEL-FOC-02 |
| "highlights the first item after opening when alignItemWithTrigger is active and no value is selected" | SEL-POS-10, SEL-API-15 |
| "navigates across groups without stopping on labels or separators" | SEL-KEY-06, SEL-KEY-07 |
| "does not move the popup or its list when hovering items at its edges" / "keeps the highlighted item visible while keyboard navigation grows the popup" | SEL-POS-11, SEL-PTR-13 |
| "does not carry an ArrowUp opening over to a later pointer opening" | SEL-KEY-02, SEL-FOC-02, SEL-DOM-25 |
| "keeps the highlight on the item while the popup animates out" | SEL-STA-07, SEL-ANI-01, SEL-ANI-14, SEL-DOM-26 |
| "does not re-render the trigger while navigating the list" | non-normative (§14, render-count optimization); no requirement |

### `SelectTrigger.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite (`refInstanceof: HTMLButtonElement`, `button: true`, rendered in `<Select.Root open>`) | SEL-DOM-55, SEL-DOM-01, SEL-API-25, SEL-API-27 |
| "throws a descriptive error when rendered outside <Select.Root>" | SEL-STR-02, SEL-EDGE-01 |
| "closes an aligned popup when focus lands on the trigger so it is not obscured" | SEL-FOC-07, SEL-DIS-09, SEL-EVT-08, SEL-POS-03 |
| "keeps a non-aligned popup open when focus lands on the trigger" | SEL-FOC-07 |
| "keeps the popup open when the press slips up to 5px off the trigger" / "closes the popup when the press is dragged off the trigger and released" | SEL-PTR-04, SEL-DIS-05 |
| "is removed from the tab order when disabled and not rendered as a button" | SEL-API-25, SEL-DOM-07 |
| "cannot be focused when disabled" / "does not toggle the popup when disabled" | SEL-API-24, SEL-DOM-07, SEL-KEY-19, SEL-PTR-21 |
| "should have the data-placeholder attribute when provided `null` value" / "should have the data-placeholder attribute when provided custom property with `itemToStringValue`" / "should have the data-placeholder attribute when provided { value: null }" / "should not have the data-placeholder attribute when provided a value" / "should not have the data-placeholder attribute when multiple mode has a default value" | SEL-DOM-04, SEL-DOM-11, SEL-STA-02, SEL-API-30 |
| "sets data-popup-side to the current popup side" / "sets data-popup-side to the resolved side when alignItemWithTrigger is active" | SEL-DOM-05, SEL-STA-28, SEL-STA-20, SEL-POS-01, SEL-POS-15, SEL-DIS-02 |
| "should have the data-popup-open and data-pressed attributes when open" | SEL-DOM-02 |
| "sets aria-required attribute when required" | SEL-API-11, SEL-ARIA-06 |

### `SelectValue.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite (`refInstanceof: HTMLSpanElement`) | SEL-DOM-55 |
| "accepts a function with a value parameter" / "overrides the text when children is a string" | SEL-API-29, SEL-EVT-14, SEL-DOM-10 |
| "displays the label from items object when no children are provided" / "displays the label from items array when no children are provided" | SEL-API-19, SEL-DOM-10 |
| "updates the label when value changes with items object" / "updates the label when value changes with items array" | SEL-API-19, SEL-DOM-10 |
| "falls back to raw value when value is not in items object" / "falls back to raw value when value is not in items array" | SEL-DOM-10 |
| "supports ReactNode labels in items object" / "supports ReactNode labels in items array" | SEL-API-19, SEL-DOM-10 |
| "can lookup null value" | SEL-DOM-10, SEL-EDGE-19, SEL-API-40 |
| "is not stale after being updated" | SEL-DOM-10, SEL-API-19 |
| "uses custom itemToStringLabel function" / "falls back to label/value properties when functions are not provided" | SEL-API-20, SEL-DOM-10, SEL-DOM-51 |
| "uses children string over items object" / "uses children function over items array" | SEL-API-29 |
| "changes text when the value changes" | SEL-DOM-10 |
| "displays comma-separated labels for multiple values with items object" / "displays comma-separated labels for multiple values with items array" / "supports ReactNode labels for multiple selections" / "falls back to raw values when no items are provided" / "displays single value when only one value is selected in multiple mode" | SEL-DOM-10 |
| "displays empty when no values are selected in multiple mode" | SEL-DOM-10 |
| "defaults to empty array when no value is provided" | SEL-API-02, SEL-API-29 |
| "children function receives array of values in multiple mode" / "children prop takes precedence over items in multiple mode" | SEL-API-29, SEL-EVT-14 |
| "displays placeholder when no value is selected" / "displays placeholder when value is null" / "does not display placeholder when value is selected" / "supports ReactNode as placeholder" | SEL-API-30 |
| "children prop takes precedence over placeholder" / "children function takes precedence over placeholder" | SEL-API-29, SEL-API-30 |
| "null item label in items takes precedence over placeholder" | SEL-API-30, SEL-API-40, SEL-EDGE-15 |
| "uses placeholder when items have null value without label" / "displays placeholder when object items do not have a null key" | SEL-API-30 |
| "null key label in object items takes precedence over placeholder" | SEL-API-30, SEL-EDGE-19 |

### `SelectItem.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite (`<Select.Item value="" />`, `refInstanceof: HTMLDivElement`, `button: true`, rendered in `<Select.Root open>`) | SEL-DOM-55, SEL-DOM-34, SEL-API-43 |
| "treats a null value as an empty selection in multiple mode" | SEL-STA-11, SEL-EDGE-15, SEL-ARIA-09, SEL-EVT-05, SEL-API-40 |
| "keeps the selection while grouped items are reordered and inserted" | SEL-STA-05 |
| "should select the item and close popup when clicked" | SEL-STR-08, SEL-PTR-08, SEL-DOM-18 |
| "navigating with keyboard should focus item" | SEL-KEY-06, SEL-FOC-02, SEL-FOC-03, SEL-ARIA-14 |
| "should select item when Enter key is pressed" | SEL-KEY-08, SEL-API-43 |
| "should focus disabled items" / "should not select disabled item" / "should not select disabled item when Enter is pressed" / "inherits the disabled state from the root" | SEL-API-41, SEL-DOM-37, SEL-KEY-06, SEL-KEY-08, SEL-PTR-10, SEL-DOM-35, SEL-API-13, SEL-KEY-20 |
| "should call onClick exactly once for a regular click" | SEL-PTR-12, SEL-PTR-08 |
| "should select an unhighlighted item with the mouse" / "should highlight a hovered item and commit it with a mouse click" / "should ignore an unhighlighted item with a generic virtual click" / "should ignore an opening click that did not start on the item" | SEL-PTR-08, SEL-PTR-22 |
| "should focus the selected item upon opening the popup" | SEL-FOC-02 |
| "should allow pointer click after a typeahead sequence that ends with Space" | SEL-KEY-09, SEL-STA-32 |
| "calls onClick once for a click made after the opening delay" | SEL-PTR-05, SEL-PTR-07 |
| "should not select an item on quick mouseup when showing a placeholder (no null item)" / "should not select an unselected item within the selected delay when aligned with the trigger" | SEL-PTR-05, SEL-PTR-22, SEL-PTR-01 |
| "should call onClick when selecting via drag-to-select (mousedown on trigger, mouseup on item)" / "should accumulate drag-to-select movement across items" / "should select via drag-to-select when hover highlighting is disabled" / "should not treat small pointer movement as drag-to-select" / "should allow small pointer movement to select after the opening delay" | SEL-PTR-06, SEL-PTR-07, SEL-PTR-05 |
| "does not fire click handlers on a disabled item during drag-to-select" / "should not select a disabled item via drag-to-select" | SEL-PTR-07, SEL-PTR-10 |
| "should not select on mouseup that follows a touch interaction" | SEL-PTR-07, SEL-PTR-09 |
| "should not select item when onClick calls preventBaseUIHandler during drag-to-select" | SEL-PTR-11, SEL-PTR-07 |
| "should apply data-highlighted attribute when item is highlighted" / "should apply data-selected attribute when item is selected" | SEL-DOM-35, SEL-STA-23 |

### `SelectPopup.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite (`refInstanceof: HTMLDivElement`, rendered in Root/Portal/Positioner) | SEL-DOM-55 |
| "throws a descriptive error when rendered outside <Select.Positioner>" | SEL-STR-03, SEL-EDGE-01 |
| "keeps arrow keys inside the popup when the trigger lives in a toolbar" | SEL-KEY-14, SEL-KEY-03, SEL-ARIA-01, SEL-EDGE-12 |
| "moves focus into the items when the boundary item is already highlighted" | SEL-KEY-06, SEL-KEY-03 |
| "has aria attributes when no Select.List is present" / "places aria attributes on Select.List instead if it is present" | SEL-STR-08, SEL-DOM-23, SEL-DOM-24, SEL-DOM-31, SEL-ARIA-02, SEL-ARIA-03, SEL-ARIA-04, SEL-ARIA-08 |
| "restores transform-related inline styles after measurement" | SEL-POS-19, SEL-POS-07 |
| "keeps alignItemWithTrigger active at browser zoom" | SEL-POS-03, SEL-POS-18, SEL-STA-20, SEL-DOM-19, SEL-POS-14 |
| "aligns the selected item with the trigger inline start in ltr" / "aligns the selected item with the trigger inline start in ltr when popup padding shifts the item text" / "aligns the selected item with the trigger inline end in rtl" / "aligns the popup with the trigger inline end in rtl when no selected item text is available" / "keeps item text aligned with the trigger after reopening" / "aligns the selected item with the trigger inline end on first open in the docs-style rtl layout" | SEL-POS-07, SEL-POS-17, SEL-DOM-12, SEL-DOM-38, SEL-I18N-01 |
| "seeds anchor width from the custom anchor on first open" | SEL-DOM-22 |
| "keeps scrolling the aligned list at browser zoom after the popup fills the available height" | SEL-POS-11, SEL-POS-18 |
| "treats short popups as top-positioned when maxScrollTop is off by 1px" / "keeps alignItemWithTrigger active when the aligned height is fractionally below minHeight" | SEL-POS-07, SEL-POS-04 |
| "does not resize the positioner when the list scrolls before the popup is placed" | SEL-POS-22 |
| "anchors the popup to the viewport bottom when the selection sits near the list top" / "grows a bottom-anchored popup as the list is scrolled and re-pins it to the top" / "consumes a sub-pixel overscroll into the popup height" / "leaves the popup height alone when the list is already at the anchored edge" / "grows a top-anchored popup upward and keeps the list pinned to the bottom" / "stops growing a top-anchored popup once it fills the available height" / "pins a top-anchored popup that already fills the viewport to the end of the list" / "stops resizing once a max-height constrained popup is fully grown" / "treats a zero max-height as unconstrained rather than collapsing the popup" / "never applies a non-positive height when the viewport collapses below the margins" / "ignores scroll events bubbling to the popup while a Select.List owns scrolling" / "grows the popup when it is itself the scroll container" | SEL-POS-07, SEL-POS-11, SEL-POS-08, SEL-POS-23, SEL-DOM-27 |
| "centers the transform origin when the positioner has no measurable height" | SEL-DOM-29 |
| "ignores a pinch-zoomed visual viewport outside WebKit" | SEL-POS-04, SEL-EDGE-14 |
| "closes the aligned popup when the window is resized" | SEL-DIS-06, SEL-POS-16 |
| Focus-props suite (options: `initialFocus: false`, `triggerRole: 'combobox'`, `alwaysMounted: true`, `mouseCloseType: ''`) — "focuses the trigger by default", "focuses the element of a ref", "focuses the right element when a function returns $name" (element/true/null), "does not move focus when `false`", "passes the close type to a function" | SEL-API-39, SEL-FOC-04, SEL-EVT-17 |

### `SelectPositioner.test.tsx` / `SelectPositioner.spec.tsx`

| Test | Requirements |
|---|---|
| Conformance suite (`refInstanceof: HTMLDivElement`, rendered in Root/Portal) | SEL-DOM-55 |
| Positioner conformance suite (options: `align="center"`, `alignItemWithTrigger={false}`, trigger 72×36, popup 52×24) — "offsets the positioner when a number is specified", "offsets the positioner when a function is specified", callback data cases, "rests exactly at collisionPadding from the colliding edge", "uses transform positioning without Viewport" | SEL-API-34, SEL-POS-01 |
| "does not retain stale coordinates while closed" | SEL-POS-02, SEL-ANI-02, SEL-DOM-18 |
| `SelectPositioner.spec.tsx` (`keepMounted` is a type error) | SEL-API-36 |

### `SelectScrollArrow.test.tsx`

| Test | Requirements |
|---|---|
| "$name" (5 geometry cases: fractional bottom snap, fractional next-item bottom, trailing content, fractional previous-item top, no earlier item) | SEL-PTR-16, SEL-PTR-17 |
| "does not start auto-scrolling for a mouse move that did not move the pointer" / "does not let continuous pointer movement postpone the scheduled scroll" / "stops auto-scrolling once the pointer leaves the arrow" | SEL-PTR-15 |
| "snaps a sub-pixel offset to the exact top edge and then stops scrolling" | SEL-PTR-16, SEL-STA-29 |
| "scrolls the popup itself when no Select.List is rendered" / "reflects scrollability on the %s arrow when the popup has no registered items" / "hides the arrow when an item-less popup is already scrolled to its edge" | SEL-PTR-16, SEL-POS-23, SEL-STA-29, SEL-DOM-44 |
| "ignores pointer interaction when the arrow has no scrollable popup" | SEL-PTR-16 |

### `SelectScrollUpArrow.test.tsx` / `SelectScrollDownArrow.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suites (`keepMounted`, `refInstanceof: HTMLDivElement`, rendered in Root/Positioner) | SEL-DOM-55, SEL-API-47 |

### `SelectGroup.test.tsx` / `SelectGroupLabel.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suites (`refInstanceof: HTMLDivElement`) | SEL-DOM-55 |
| "should render group with label" / "should associate label with group" | SEL-DOM-41, SEL-ARIA-11 |
| "is hidden from the accessibility tree by default" / "allows overriding aria-hidden" | SEL-DOM-42, SEL-ARIA-12 |
| "throws a descriptive error when rendered outside <Select.Group>" | SEL-STR-05, SEL-EDGE-01 |
| "removes the group aria-labelledby attribute when unmounted" / "does not let an older label cleanup clear a newer label" / "updates explicit and generated ids independently of ref churn" / "replaces and unregisters its label in Strict Mode" | SEL-API-46, SEL-DOM-41, SEL-EDGE-18 |

### `SelectItemIndicator.test.tsx` / `SelectItemText.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suites (ItemIndicator `refInstanceof: HTMLSpanElement`; ItemText `refInstanceof: HTMLDivElement`) | SEL-DOM-55 |
| "settles out of its transition state after the item is deselected" / "preserves the kept-mounted element across selection changes" / "mounts only while selected when keepMounted is false" | SEL-STR-11, SEL-API-45, SEL-ANI-12, SEL-DOM-39, SEL-ARIA-13 |
| "throws a descriptive error when rendered outside <Select.Item>" | SEL-STR-04, SEL-EDGE-01 |

### `store.test.tsx`

| Test | Requirements |
|---|---|
| "publishes synchronized values in a single store transaction" / "provides setOpen to a descendant ref callback on the first commit" / "synchronizes an updated disabled prop after descendant ref callbacks" | SEL-STA-01 |

### `SelectArrow.test.tsx`, `SelectBackdrop.test.tsx`, `SelectIcon.test.tsx`, `SelectLabel.test.tsx`, `SelectList.test.tsx`, `SelectPortal.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suites only (Arrow in `<Select.Positioner alignItemWithTrigger={false}>`; Icon `HTMLSpanElement`; Label rendered beside a Trigger and Portal/Positioner; others `HTMLDivElement`) | SEL-DOM-55, SEL-STR-09 (Arrow suite needs `alignItemWithTrigger={false}` to render), SEL-DOM-13, SEL-DOM-14, SEL-DOM-16, SEL-DOM-17, SEL-DOM-40 |

### `SelectRoot.spec.tsx`

Type-level only: `Value` inference from `items` (including `readonly` arrays and grouped items), `onValueChange` receiving `Value | null` when `value` may be null, `isItemEqualToValue` argument typing, generic typed wrappers (§3.19). No runtime requirement.

### Additional requirements introduced here

None beyond SEL-EDGE-10 and the §13 rows above; every test assertion maps to a requirement in §2–§12.

## 14. React-specific notes and porting considerations

Non-normative.

- **Store and atomic publication.** The Root keeps its state in a reactive store and mirrors props into it in a layout effect (`useSyncedValues`, UTL-STA-21) so all parts observe one consistent record (SEL-STA-01). The interaction attribute bags are written on the very first render (`useOnFirstRender`) because descendants render in the same pass. A port with a different rendering model can simply pass the resolved values down; the observable contract is only "no torn reads". Port: see decisions/05-state-store-and-event-details.md §1.
- **Commands as stable callbacks.** `setValue`/`setOpen`/`handleScrollArrowVisibility`/`onOpenChangeComplete` are stored in the store's non-reactive context as stable trampolines so that descendants and timers always call the latest version (UTL-STA-16); the dev-only error of UTL-EDGE-27 fires if one is invoked during render. Port: see decisions/05-state-store-and-event-details.md §1.
- **Layout-effect timing.** The selected-index sync (SEL-STA-04), per-item claims (SEL-STA-05/06), filled/dirty/validation side effects (SEL-EVT-13), the popup placement (SEL-POS-07), the original-style capture, the group-label id publication and the scroll-arrow mount counting all run post-commit, pre-paint; the resize listener and the typeahead/dismiss listeners are passive-effect (after paint). Item effects run child-before-parent, which the multiple-mode anchor election relies on (the Root's own sync runs after the items' claims and is skipped while open). Port: see decisions/01-hosting-model-and-timing.md §Decision 4 (timing-term mapping); the placement and measurement run in the `select` JS module, decisions/04-js-interop-boundary.md §1–§2.
- **Timers.** Trigger focus → `forceMount` after a 0 ms timer; trigger mousedown → document `mouseup` listener after a 0 ms timer; open → 400 ms selection-delay timer; scroll arrows → 40 ms repeating timer; non-aligned arrow visibility → next frame; mouse open → next frame (useClick); autofill → microtask; focus-manager initial focus → microtask; return focus → microtask. A port must keep these orderings (e.g. items must be registered before the autofill match runs). Port: see decisions/01-hosting-model-and-timing.md §Decision 3–4 (C# never emulates frames or microtasks; timers per the mapping table).
- **Render-count optimization (not a requirement).** `Select.Item` and `Select.ItemText` are memoized and subscribe to a combined `itemRoot` selector; the trigger reads its interaction attributes from the store so that moving the highlight re-renders only the two items involved and not the trigger (`SelectRoot.test.tsx` "does not re-render the trigger while navigating the list"). Equivalent efficiency in another framework is desirable but not observable. Port: see decisions/05-state-store-and-event-details.md §1 (per-part selector subscriptions).
- **`React.useImperativeHandle` uses.** `actionsRef` (public), the aligned-mode flag shared with the trigger, and the popup scroll handler shared with the List are all "latest-value handles" — a port can use plain mutable fields. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24, DOM handles).
- **`Omit<…, 'id'>` on Item and Label.** The types forbid `id`; at runtime an Item forwards it (SEL-API-44) and a Label deletes it (SEL-API-32). Ports should match the runtime behavior.
- **Strict Mode / double effects.** The group-label registration and the scroll-arrow mount counter are written to survive effect double-invocation (`SelectGroupLabel.test.tsx` "replaces and unregisters its label in Strict Mode"; `SelectRoot.test.tsx` "clears scroll arrow visibility when manually unmounted (strict: true)"). The conformance suite for `onOpenChangeComplete` runs without Strict Mode because an initially-open completion would be reported twice under double effects. Port: see decisions/04-js-interop-boundary.md §3 (per-part `attach`/`dispose` handles; registration and mount counting must survive a dispose followed by a re-attach).
- **`focusVisible` focus option.** Label click and hidden-input focus redirection call `focus({ focusVisible: true })` (supported in Chrome 144+, Safari, Firefox); unsupported engines ignore the option.
- **Portal and DOM order.** The Portal renders into `document.body` (or `container`); `Select.Item` elements therefore live outside the trigger's DOM ancestry, and focus-out detection relies on the FLT portal guards and tree bookkeeping rather than DOM containment alone. Port: see decisions/04-js-interop-boundary.md §5.
- **`hidden` on the kept-mounted positioner.** The positioner is kept in the DOM after the first open; frameworks that unmount closed popups must still provide the item labels for closed-trigger typeahead (SEL-STA-09) — the simplest faithful port keeps the subtree mounted and hidden exactly as Base UI does.
- **React 17 vs 18/19.** `inert` is rendered as the string `'true'` on React < 19 and as a boolean on 19 (UTL-DOM-16); the labelable id fallback has a React 17 branch (FFI-FRM-36). The tests that need fake timers run with a fake clock that still advances time (`shouldAdvanceTime`). Port: see decisions/06-testing-harness.md §2 (page.clock / `TimeProvider`).

## 15. Requirement index

Every ID in this document, grouped by AREA (conventions §3.3 order) and numeric within each area.

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| SEL-STR-01 | MUST | `Select.Root` MUST NOT render an element of its own. It MUST render, in document order: its `children`, then … | 2 |
| SEL-STR-02 | MUST | `Select.Trigger`, `Select.Value`, `Select.Icon`, `Select.Label`, `Select.Portal`, `Select.Backdrop` … | 2 |
| SEL-STR-03 | MUST | `Select.Popup`, `Select.List`, `Select.Arrow`, `Select.ScrollUpArrow` and `Select.ScrollDownArrow` MUST be … | 2 |
| SEL-STR-04 | MUST | `Select.ItemText` and `Select.ItemIndicator` MUST be rendered inside the subtree of a `Select.Item` … | 2 |
| SEL-STR-05 | MUST | `Select.GroupLabel` MUST be rendered inside the subtree of a `Select.Group`; otherwise it MUST fail with the … | 2 |
| SEL-STR-06 | MUST | `Select.Portal` MUST render nothing while the popup is neither *mounted* (§9) nor *force-mounted* … | 2 |
| SEL-STR-07 | MUST | `Select.Positioner` MUST provide the *item registry* (CMP) for its subtree — every `Select.Item` registers … | 2 |
| SEL-STR-08 | MUST | `Select.Popup` and `Select.List` are both optional. When a `Select.List` is rendered, it is the `listbox` and … | 2 |
| SEL-STR-09 | MUST | `Select.Arrow` MUST render nothing while item-aligned positioning is active (SEL-POS-03); otherwise it … | 2 |
| SEL-STR-10 | MUST | `Select.ScrollUpArrow` / `Select.ScrollDownArrow` MUST render their element iff their transition *mounted* … | 2 |
| SEL-STR-11 | MUST | `Select.ItemIndicator` MUST render its element iff its item is selected or `keepMounted` is `true` … | 2 |
| SEL-STR-12 | MUST | `Select.Icon`, `Select.Backdrop`, `Select.ItemText`, `Select.Group`, `Select.GroupLabel` and … | 2 |
| SEL-STR-13 | MUST | Exactly one `Select.Trigger` is supported per Root: the Root stores a single trigger element (the last one to … | 2 |
| SEL-API-01 | MUST | `value` / `defaultValue` follow the controlled/uncontrolled contract of EVD-STA-01…06 and UTL-STA-01…06 with … | 3.1 |
| SEL-API-02 | MUST | In single mode the initial component-owned value is `defaultValue`, defaulting to `null`. In `multiple` mode … | 3.1 |
| SEL-API-03 | MUST | `onValueChange` MUST receive the requested value as its first argument (the item value in single mode; a new … | 3.1 |
| SEL-API-04 | MUST | `open` / `defaultOpen` follow the same controlled/uncontrolled contract (diagnostics name `'Select'`, state … | 3.1 |
| SEL-API-05 | MUST | `onOpenChange` MUST receive the requested open state and an open-change-details object that additionally … | 3.1 |
| SEL-API-06 | MUST | `onOpenChangeComplete` MUST receive exactly one boolean argument (TRN-API-04); timing in SEL-ANI-05/06. | 3.1 |
| SEL-API-07 | MUST | The effective form name is `Field.Root.name` when defined, otherwise the `name` prop. It is placed only on … | 3.1 |
| SEL-API-08 | MUST | `form` MUST be forwarded as the `form` attribute of the hidden input and of every per-value hidden input. | 3.1 |
| SEL-API-09 | MUST | `autoComplete` MUST be forwarded as the `autocomplete` attribute of the hidden input and nowhere else. | 3.1 |
| SEL-API-10 | MUST | `id` is the trigger's `id` unless the trigger has its own `id` prop (SEL-API-26); when absent, a generated id … | 3.1 |
| SEL-API-11 | MUST | `required` defaults to `false`. When `true`: the trigger carries `aria-required="true"` (no `data-required` … | 3.1 |
| SEL-API-12 | MUST | `readOnly` defaults to `false`. When `true`: the trigger carries `aria-readonly="true"` and `data-readonly` … | 3.1 |
| SEL-API-13 | MUST | The Root's effective disabled state is `Field.Root.disabled \|\| disabled` (default `false`). While disabled … | 3.1 |
| SEL-API-14 | MUST | `multiple` defaults to `false`. When `true`: the value is an array (SEL-STA-11), activating an item toggles … | 3.1 |
| SEL-API-15 | MUST | `highlightItemOnHover` defaults to `true` and is passed to list navigation as `focusItemOnHover` … | 3.1 |
| SEL-API-16 | MUST | `modal` defaults to `true`. When `true` and the popup is mounted, the internal backdrop is rendered … | 3.1 |
| SEL-API-17 | MUST | `actionsRef`, when given, MUST be populated with an object exposing `close()` — which, only while open … | 3.1 |
| SEL-API-18 | MUST | `inputRef` MUST receive the hidden form-participation input element (shared with the field's validation input … | 3.1 |
| SEL-API-19 | MUST | `items` accepts (a) a record whose keys are string values and whose entries are labels, (b) an array of `{ … | 3.1 |
| SEL-API-20 | MUST | `itemToStringLabel`, when given, is used to produce the display text of a non-null value (SEL-DOM-10) and the … | 3.1 |
| SEL-API-21 | MUST | `itemToStringValue`, when given, is used to serialize each non-null value for form submission, the … | 3.1 |
| SEL-API-22 | MUST | `isItemEqualToValue(itemValue, selectedValue)` defaults to `Object.is`. The first argument is always an … | 3.1 |
| SEL-API-23 | MUST | `children` are rendered as-is before the hidden input(s); the Root exposes its shared state to every … | 3.1 |
| SEL-API-24 | MUST | The trigger's effective disabled state is `Field.Root.disabled \|\| Root.disabled \|\| Trigger.disabled`; it is … | 3.3 |
| SEL-API-25 | MUST | `nativeButton` defaults to `true` and maps to the button mechanism's `native` option (UBT-API-02) … | 3.3 |
| SEL-API-26 | MUST | When the trigger receives an `id` prop it is used as the trigger's `id` and registered as the field's control … | 3.3 |
| SEL-API-27 | MUST | Every prop not listed as consumed MUST be forwarded to the trigger element; consumer handlers chain with the … | 3.3 |
| SEL-API-28 | MUST | For every part of this document, `className`, `style` and `render` MUST behave per RND (RND-API-01…03); the … | 3.3 |
| SEL-API-29 | MUST | When `children` is a function it MUST be called during each render of the part with the current value … | 3.4 |
| SEL-API-30 | MUST | `placeholder` is rendered iff no value is selected (SEL-STA-02), `children` is nullish, and `items` does not … | 3.4 |
| SEL-API-31 | MUST | `Select.Icon` MUST render the single character `▼` as its content when the consumer supplies no `children`. | 3.5 |
| SEL-API-32 | MUST | `Select.Label` MUST ignore an `id` prop: its rendered `id` is always the registered label id (default … | 3.2 |
| SEL-API-33 | MUST | `container` and every other prop MUST be forwarded to the shared floating portal (FLT) unchanged; the Portal … | 3.6 |
| SEL-API-34 | MUST | The positioning props MUST be passed to the shared anchor-positioning mechanism (FLT) with the defaults in … | 3.8 |
| SEL-API-35 | MUST | `alignItemWithTrigger` defaults to `true`. Its effect is specified in SEL-POS-03…POS-24; it is only … | 3.8 |
| SEL-API-36 | MUST | `Select.Positioner` MUST NOT accept a `keepMounted` prop (compile-time error); the positioner is always kept … | 3.8 |
| SEL-API-37 | MUST | `collisionAvoidance` defaults to the dropdown preset `{ fallbackAxisSide: 'none' }` (UTL-EDGE-19 … | 3.8 |
| SEL-API-38 | MUST | When `disableAnchorTracking` is `undefined`, anchor tracking MUST be disabled exactly while aligned … | 3.8 |
| SEL-API-39 | MUST | `finalFocus` is passed as the focus manager's `returnFocus` (FLT-API-05, FLT-FOC-21) and resolved when the … | 3.9 |
| SEL-API-40 | MUST | `value` defaults to `null`; a `null`-valued item is a legitimate option (selecting it sets the value to … | 3.12 |
| SEL-API-41 | MUST | The item's effective disabled state is `Root.disabled \|\| Item.disabled`; it is passed to the button mechanism … | 3.12 |
| SEL-API-42 | MUST | `label`, when not `undefined`, is the item's typeahead label; otherwise the text content of its … | 3.12 |
| SEL-API-43 | MUST | `nativeButton` defaults to `false` (UBT-API-03); the item therefore gets non-native keyboard activation … | 3.12 |
| SEL-API-44 | MUST | `Select.Item` MUST NOT generate an `id` of its own (no `aria-activedescendant` scheme is used, SEL-ARIA-14) … | 3.12 |
| SEL-API-45 | MUST | `keepMounted` defaults to `false` and selects the render predicate of SEL-STR-11 and the kept-mounted … | 3.14 |
| SEL-API-46 | MUST | `id` is used verbatim when given, else a generated id (RND-DOM-27…30); whichever is current is published to … | 3.16 |
| SEL-API-47 | MUST | `keepMounted` defaults to `false` (SEL-STR-10). Both parts are the same implementation with `direction` fixed … | 3.17 |
| SEL-API-48 | MUST | `orientation` defaults to `'horizontal'` (RND-API-13); the state object is `{ orientation }` and the element … | 3.18 |
| SEL-STA-01 | MUST | Every Root parameter that parts observe (`id`, `modal`, `multiple`, `disabled`, `readOnly`, `required` … | 4 |
| SEL-STA-02 | MUST | *Has selected value* is: `false` when the value is `null`/`undefined`; in multiple mode with an array value … | 4 |
| SEL-STA-03 | MUST | An item is *selected* iff: in multiple mode, the value is an array containing some element `v` with … | 4 |
| SEL-STA-04 | MUST | *Selected index* (the index of the item the popup opens on and anchors keyboard navigation to): while the … | 4 |
| SEL-STA-05 | MUST | Whenever an item registers or its `value`/index changes (post-commit, pre-paint), in single mode it MUST … | 4 |
| SEL-STA-06 | MUST | In multiple mode the selected index is the *anchor*: the first selected item in rendered order. On each item … | 4 |
| SEL-STA-07 | MUST | *Active index* (the highlighted item; `null` = none) is owned by the Root and written by: list navigation … | 4 |
| SEL-STA-08 | MUST | *Open method* (`'mouse' \| 'touch' \| 'pen' \| 'keyboard' \| '' \| null`) is recorded from the trigger's … | 4 |
| SEL-STA-09 | MUST | *Force mount* starts `false` and becomes `true` (and never returns to `false`) 0 ms after the trigger … | 4 |
| SEL-STA-10 | MUST | `mounted` and `transitionStatus` are produced by the unmount-after-close machine (TRN-ALG-05) with `open` … | 4 |
| SEL-STA-11 | MUST | Value normalization: in multiple mode a non-array value (e.g. a controlled `null`) MUST be treated as an … | 4 |
| SEL-STA-12 | MUST | *Item reconciliation (single mode).* Whenever the item registry publishes a changed item map (CMP-STA-09) and … | 4 |
| SEL-STA-13 | MUST | *Item reconciliation (multiple mode).* Under the same preconditions, with an array value, the component MUST … | 4 |
| SEL-STA-14 | MUST | Reconciliation MUST NOT run on the first (initial) publication of the item map, nor while the value registry … | 4 |
| SEL-STA-15 | MUST | The trigger state object MUST contain exactly the fields above (the field's `disabled` is replaced by the … | 3.3 |
| SEL-STA-16 | MUST | The Value state object MUST contain exactly `value` and `placeholder`. | 3.4 |
| SEL-STA-17 | MUST | The Icon state object MUST contain exactly `open: boolean`. | 3.5 |
| SEL-STA-18 | MUST | The Label state object is exactly the enclosing field's state object (FFI-STA-01), unmodified (it does not … | 3.2 |
| SEL-STA-19 | MUST | The Backdrop state object MUST contain exactly `open: boolean` and `transitionStatus` (TRN-API-07). | 3.7 |
| SEL-STA-20 | MUST | The Positioner state object MUST contain exactly the fields above; `side` MUST be `'none'` while aligned … | 3.8 |
| SEL-STA-21 | MUST | The Popup state object MUST contain exactly the fields above. | 3.9 |
| SEL-STA-22 | MUST | The Arrow state object MUST contain exactly `open`, `side`, `align`, `uncentered`. | 3.11 |
| SEL-STA-23 | MUST | The Item state object MUST contain exactly `disabled`, `selected`, `highlighted`. | 3.12 |
| SEL-STA-24 | MUST | The ItemIndicator state object MUST contain exactly `selected: boolean` and `transitionStatus`. | 3.14 |
| SEL-STA-25 | MUST | The scroll-arrow state object MUST contain exactly `direction: 'up' \| 'down'`, `visible: boolean` (SEL-STA-29 … | 3.17 |
| SEL-STA-26 | MUST | The Separator state object MUST contain exactly `orientation`. | 3.18 |
| SEL-STA-27 | MUST | The state object of `Select.Portal`, `Select.List`, `Select.ItemText`, `Select.Group` and `Select.GroupLabel` … | 3.6 |
| SEL-STA-28 | MUST | The store's `popupSide` is the anchor-positioning mechanism's resolved side (never `'none'`), updated … | 4 |
| SEL-STA-29 | MUST | *Scroll-arrow visibility.* Given the scroller (the List element when present, else the popup element) … | 4 |
| SEL-STA-30 | MUST | `hasScrollArrows` is `true` while at least one scroll-arrow part is mounted (a mount counter … | 4 |
| SEL-STA-31 | MUST | The *initial value* used as the reconciliation fallback (SEL-STA-12) is the value present on the Root's first … | 4 |
| SEL-STA-32 | MUST | A *typing* flag is `true` from the first accepted typeahead character until the typeahead session ends (reset … | 4 |
| SEL-DOM-01 | MUST | The trigger's default element is `<button>`; in native mode it carries `type="button"` (UBT-DOM-01). | 5.1 |
| SEL-DOM-02 | MUST | While `open` is `true` the trigger MUST carry both `data-popup-open=""` and `data-pressed=""` (pressable … | 5.1 |
| SEL-DOM-03 | MUST | The trigger MUST carry `data-disabled=""` while effectively disabled and `data-readonly=""` while `readOnly` … | 5.1 |
| SEL-DOM-04 | MUST | The trigger MUST carry `data-placeholder=""` iff no value is selected (SEL-STA-02). | 5.1 |
| SEL-DOM-05 | MUST | The trigger MUST carry `data-popup-side="<side>"` with the resolved side (SEL-STA-28) while the popup is … | 5.1 |
| SEL-DOM-06 | MUST | The trigger MUST carry the field state attributes `data-touched`, `data-dirty`, `data-filled`, `data-focused` … | 5.1 |
| SEL-DOM-07 | MUST | The trigger renders `tabindex="0"` while enabled and `tabindex="-1"` while disabled, supplied as an element … | 5.1 |
| SEL-DOM-08 | MUST | The `value` state field MUST NOT be projected to a data attribute on the trigger or on `Select.Value`. | 5.1 |
| SEL-DOM-09 | MUST | The trigger's `id` is its `id` prop when given, else the Root's effective id (SEL-API-10); it is applied by … | 5.1 |
| SEL-DOM-10 | MUST | `Select.Value` renders a `<span>` whose content is resolved in this order: (1) `children` function → its … | 5.2 |
| SEL-DOM-11 | MUST | The Value element MUST carry `data-placeholder=""` iff no value is selected (SEL-STA-02), regardless of … | 5.2 |
| SEL-DOM-12 | MUST | The Value element is the *value text reference* for aligned positioning: its bounding box is what the … | 5.2 |
| SEL-DOM-13 | MUST | `Select.Icon` renders a `<span aria-hidden="true">` containing `▼` by default and carries … | 5.3 |
| SEL-DOM-14 | MUST | `Select.Label` renders a `<div>` whose `id` is the registered label id — `${rootId}-label` by default … | 5.4 |
| SEL-DOM-15 | MUST | The Label behaves as a non-native label (FFI-PTR-01): `pointerdown` is default-prevented and `click` focuses … | 5.4 |
| SEL-DOM-16 | MUST | When rendered (SEL-STR-06), `Select.Portal` renders the shared floating portal's `<div>` appended to … | 5.5 |
| SEL-DOM-17 | MUST | `Select.Backdrop` renders `<div role="presentation">` with inline `user-select: none; -webkit-user-select … | 5.6 |
| SEL-DOM-18 | MUST | `Select.Positioner` renders `<div role="presentation">` (shared positioner host, FLT `usePositioner`) with … | 5.7 |
| SEL-DOM-19 | MUST | The positioner carries `data-open`/`data-closed`, `data-anchor-hidden` (PSH-DOM-03/04), `data-side` … | 5.7 |
| SEL-DOM-20 | MUST | While aligned positioning is active the positioner's built-in inline style is exactly `position: fixed` (the … | 5.7 |
| SEL-DOM-21 | MUST | While `transitionStatus` is `'starting'` the positioner carries inline `transition: none` (TRN-DOM-05). | 5.7 |
| SEL-DOM-22 | MUST | The positioner carries the FLT CSS variables `--anchor-width`, `--anchor-height`, `--available-width` … | 5.7 |
| SEL-DOM-23 | MUST | When no `Select.List` element is registered, the Popup MUST carry `role="listbox"`, `id="${rootId}-list"` … | 5.8 |
| SEL-DOM-24 | MUST | When a `Select.List` is registered, the Popup MUST carry `role="presentation"` and none of the listbox … | 5.8 |
| SEL-DOM-25 | MUST | The Popup carries `tabindex="-1"` and `data-base-ui-focusable=""` (PSH-DOM-15), making it the popup focus … | 5.8 |
| SEL-DOM-26 | MUST | The Popup carries `data-open`/`data-closed` (PSH-DOM-03), `data-starting-style`/`data-ending-style` … | 5.8 |
| SEL-DOM-27 | MUST | While aligned positioning is active the Popup's built-in inline style is `height: 100%` when a List is … | 5.8 |
| SEL-DOM-28 | MUST | While aligned positioning is active and no List is registered, the Popup MUST carry the class … | 5.8 |
| SEL-DOM-29 | MUST | In aligned mode, after placement, the Popup element's inline `--transform-origin` MUST be `50% <Y>%` where … | 5.8 |
| SEL-DOM-30 | MUST | While `transitionStatus` is `'starting'` the Popup carries inline `transition: none` (TRN-DOM-05). | 5.8 |
| SEL-DOM-31 | MUST | `Select.List` renders `<div id="${rootId}-list" role="listbox">` with `aria-multiselectable="true"` iff … | 5.9 |
| SEL-DOM-32 | MUST | While aligned positioning is active the List's built-in inline style is `position: relative; max-height … | 5.9 |
| SEL-DOM-33 | MUST | The List MUST carry the class `base-ui-disable-scrollbar` iff at least one scroll arrow is mounted … | 5.9 |
| SEL-DOM-34 | MUST | `Select.Item` renders `<div role="option">` (the button mechanism's `role="button"` is overridden … | 5.10 |
| SEL-DOM-35 | MUST | The Item carries `data-selected=""` while selected, `data-highlighted=""` while highlighted and … | 5.10 |
| SEL-DOM-36 | MUST | The Item renders `tabindex="0"` iff the popup is open and the item is highlighted, and `tabindex="-1"` … | 5.10 |
| SEL-DOM-37 | MUST | A disabled item carries `aria-disabled="true"` (non-native, UBT-ARIA-02; in native mode with … | 5.10 |
| SEL-DOM-38 | MUST | `Select.ItemText` renders a `<div>` with no built-in attributes; the element is recorded as the item's text … | 5.11 |
| SEL-DOM-39 | MUST | `Select.ItemIndicator` renders the shared indicator: `<span aria-hidden="true">` with default content `✔️` … | 5.12 |
| SEL-DOM-40 | MUST | `Select.Arrow` renders `<div aria-hidden="true">` with the mechanism's arrow position styles inline (FLT) … | 5.13 |
| SEL-DOM-41 | MUST | `Select.Group` renders `<div role="group">` with `aria-labelledby` equal to its GroupLabel's id while a … | 5.14 |
| SEL-DOM-42 | MUST | `Select.GroupLabel` renders `<div id="…" aria-hidden="true">`; a consumer-supplied `aria-hidden` (including … | 5.14 |
| SEL-DOM-43 | MUST | Each scroll arrow renders `<div aria-hidden="true">` with inline `position: absolute` and default content `▲` … | 5.15 |
| SEL-DOM-44 | MUST | Each scroll arrow carries `data-direction="up"`/`"down"`, `data-visible=""` while visible (SEL-STA-25), and … | 5.15 |
| SEL-DOM-45 | MUST | Each scroll arrow carries `data-starting-style`/`data-ending-style` from its own transition status (TRN §6.1 … | 5.15 |
| SEL-DOM-46 | MUST | `Select.Separator` renders `<div role="presentation" data-orientation="horizontal\|vertical">` (RND-DOM-53 … | 5.16 |
| SEL-DOM-47 | MUST | The Root renders exactly one native `<input>` carrying `aria-hidden="true"`, `tabindex="-1"`, the effective … | 5.17 |
| SEL-DOM-48 | MUST | The hidden input carries `id="${rootId}-hidden-input"` iff it has no `name` (nameless single mode or multiple … | 5.17 |
| SEL-DOM-49 | MUST | The hidden input's inline style is UTL-DOM-14 `visuallyHiddenInput` (absolute) when an effective `name` … | 5.17 |
| SEL-DOM-50 | MUST | The hidden input carries `required` iff the `required` prop is `true` and not (multiple mode with a non-empty … | 5.17 |
| SEL-DOM-51 | MUST | In single mode the hidden input's `value` is `stringifyAsValue(value, itemToStringValue)` (UTL): `''` for … | 5.17 |
| SEL-DOM-52 | MUST | In multiple mode the shared hidden input is nameless (so it never submits and `itemToStringValue` is never … | 5.17 |
| SEL-DOM-53 | MUST | In multiple mode with an array value and an effective name, the Root renders after the shared input one … | 5.18 |
| SEL-DOM-54 | MUST | While mounted and `modal`, the internal backdrop (PSH-DOM-14, PSH-PTR-05/06) is rendered with `cutout` = the … | 5.19 |
| SEL-DOM-55 | MUST | Every part MUST satisfy RND-DOM-36…48 (and RND-DOM-49 for Trigger and Item, which are run with `button … | 5.20 |
| SEL-ARIA-01 | MUST | The trigger's `role` MUST be `combobox`, applied *after* every other attribute source so that a consumer … | 5.21 |
| SEL-ARIA-02 | MUST | The trigger carries `aria-expanded="true"` while open and `"false"` otherwise (PSH-ARIA-01). | 5.21 |
| SEL-ARIA-03 | MUST | The trigger carries `aria-haspopup="listbox"` at all times (PSH-ARIA-04). | 5.21 |
| SEL-ARIA-04 | MUST | While open the trigger carries `aria-controls` equal to the List element's `id` when a List exists, else the … | 5.21 |
| SEL-ARIA-05 | MUST | The trigger's `aria-labelledby` is the enclosing field's label id when one is registered, else the … | 5.21 |
| SEL-ARIA-06 | MUST | The trigger carries `aria-readonly="true"` iff `readOnly` and `aria-required="true"` iff `required`; neither … | 5.21 |
| SEL-ARIA-07 | MUST | Both the trigger and the hidden input receive the field's validation attributes (FFI-ARIA-03) … | 5.21 |
| SEL-ARIA-08 | MUST | Exactly one element carries `role="listbox"` (the List, else the Popup) with `aria-multiselectable="true"` … | 5.21 |
| SEL-ARIA-09 | MUST | Each item carries `role="option"` and `aria-selected` equal to the string `"true"`/`"false"` of its selected … | 5.21 |
| SEL-ARIA-10 | MUST | A disabled item carries `aria-disabled="true"` (SEL-DOM-37); an enabled non-native item carries no … | 5.21 |
| SEL-ARIA-11 | MUST | `Select.Group` carries `role="group"` and `aria-labelledby` (SEL-DOM-41). | 5.21 |
| SEL-ARIA-12 | MUST | `Select.GroupLabel` is `aria-hidden="true"` by default (SEL-DOM-42); the group's accessible name is still … | 5.21 |
| SEL-ARIA-13 | MUST | `Select.Icon`, `Select.Arrow`, both scroll arrows, `Select.ItemIndicator` and the hidden input carry … | 5.21 |
| SEL-ARIA-14 | MUST | Select uses the DOM-focus model (CMP: `virtual: false`): the highlighted item receives real focus and no … | 5.21 |
| SEL-ARIA-15 | MUST | `Select.Positioner`, `Select.Backdrop`, the internal backdrop, `Select.Separator` and a Popup wrapping a List … | 5.21 |
| SEL-KEY-01 | MUST | ArrowDown on the focused, enabled trigger while closed MUST open the popup with reason `list-navigation` and … | 6.1 |
| SEL-KEY-02 | MUST | ArrowUp on the focused trigger while closed MUST behave as SEL-KEY-01 except that without a selection the … | 6.1 |
| SEL-KEY-03 | MUST | Enter and Space on the focused trigger while closed MUST open the popup with reason `trigger-press` (through … | 6.1 |
| SEL-KEY-04 | MUST | While closed, enabled, not `readOnly` and not `multiple`, a printable key (length-1 key without … | 6.1 |
| SEL-KEY-05 | MUST | While closed, Home, End, Escape, Tab, ArrowLeft, ArrowRight, PageUp and PageDown on the trigger are not … | 6.1 |
| SEL-KEY-06 | MUST | While open, ArrowDown/ArrowUp (CMP-KEY-30…32 with `loopFocus: false`, `disabledIndices: []`) MUST move the … | 6.1 |
| SEL-KEY-07 | MUST | While open, Home/End MUST highlight and focus the first/last registered item (disabled items included) … | 6.1 |
| SEL-KEY-08 | MUST | Enter on a focused item (UBT-KEY-01: default prevented, synthetic click dispatched) MUST activate the item … | 6.1 |
| SEL-KEY-09 | MUST | Space keydown on a focused item (composite mode, UBT-KEY-12) MUST activate it like Enter, **except** while a … | 6.1 |
| SEL-KEY-10 | MUST | Escape while open (keydown on the document, outside an IME composition) MUST request a close with reason … | 6.1 |
| SEL-KEY-11 | MUST | Tab is not intercepted by list navigation (CMP-KEY-24 handles only Shift+Tab). `Select.Popup` passes … | 6.1 |
| SEL-KEY-12 | MUST | While open, a printable key on the popup or an item MUST run typeahead (consuming the event and marking … | 6.1 |
| SEL-KEY-13 | MUST | Any `keydown` on an item MUST first set the highlighted item to that item (so a key pressed on a focused but … | 6.1 |
| SEL-KEY-14 | MUST | When the Root is rendered inside a `Toolbar.Root`, `keydown` events for `ArrowUp`, `ArrowDown`, `ArrowLeft`, … | 6.1 |
| SEL-KEY-15 | MUST | Modifier handling: typeahead ignores keys pressed with Ctrl, Meta or Alt (CMP); list navigation does not … | 6.1 |
| SEL-KEY-16 | MUST | While open, ArrowLeft, ArrowRight, PageUp and PageDown are not intercepted (vertical orientation; no grid). | 6.1 |
| SEL-KEY-17 | MUST | A keydown on the trigger while the popup is open and the trigger has focus (possible in non-aligned mode … | 6.1 |
| SEL-KEY-18 | MUST | In `readOnly` mode ArrowDown, Enter and Space on the trigger still open the popup and the open-popup keys of … | 6.1 |
| SEL-KEY-19 | MUST | While the Root is disabled, no keyboard interaction (open, navigate, typeahead, activate) MUST occur; a … | 6.1 |
| SEL-KEY-20 | MUST | The Root passes `disabledIndices: []` (an empty array, not omitted) to list navigation … | 6.1 |
| SEL-PTR-01 | MUST | A primary-button `mousedown` on the enabled trigger MUST toggle the popup through the shared click … | 6.2 |
| SEL-PTR-02 | MUST | Presses with a non-primary button MUST NOT open or close the popup, and right clicks on items MUST NOT … | 6.2 |
| SEL-PTR-03 | MUST | The open method (SEL-STA-08) MUST be recorded from the trigger's `pointerdown` (`pointerType`) and `click` … | 6.2 |
| SEL-PTR-04 | MUST | When a `mousedown` on the trigger occurs while closed, the trigger MUST, after a 0 ms macrotask (so that a … | 6.2 |
| SEL-PTR-05 | MUST | *Selection delay.* When the popup opens, both release-selection gates (`allowSelectedMouseUp` … | 6.2 |
| SEL-PTR-06 | MUST | A `pointermove` over an item with `pointerType === 'mouse'` and the primary button held (`buttons === 1`) … | 6.2 |
| SEL-PTR-07 | MUST | A `mouseup` on an item (after resetting the accumulator) MUST do nothing when the item is disabled, when the … | 6.2 |
| SEL-PTR-08 | MUST | On `click`, an item MUST classify the click: it is a *mouse click* when the last pointer type seen on the … | 6.2 |
| SEL-PTR-09 | MUST | For touch input the item's `pointerenter`/`pointerdown` record `pointerType === 'touch'`; the browser's … | 6.2 |
| SEL-PTR-10 | MUST | A disabled item MUST NOT activate for any pointer gesture and MUST NOT invoke a consumer `onClick` … | 6.2 |
| SEL-PTR-11 | MUST | A consumer `onClick` on an item that calls `preventBaseUIHandler()` MUST prevent the activation (RND-EVT-03) … | 6.2 |
| SEL-PTR-12 | MUST | A regular click (pointerdown + mouseup + click on the same item) MUST invoke the consumer's `onClick` exactly … | 6.2 |
| SEL-PTR-13 | MUST | With `highlightItemOnHover` (default), a non-stationary `mousemove` over an item MUST highlight and focus it … | 6.2 |
| SEL-PTR-14 | MUST | A `click` on an item focuses it without scrolling (CMP-PTR-03), so keyboard navigation continues from the … | 6.2 |
| SEL-PTR-15 | MUST | A `mousemove` on a scroll arrow whose `movementX` and `movementY` are both `0` MUST be ignored; any other … | 6.2 |
| SEL-PTR-16 | MUST | *Auto-scroll step.* When the timer fires: let the scroller be the List element, else the popup element (if … | 6.2 |
| SEL-PTR-17 | MUST | *Target offset.* With `h` = the arrow's `offsetHeight` (0 when absent) and tolerance 1 px: **up** — the first … | 6.2 |
| SEL-PTR-18 | MUST | Scroll arrows are never *visible* (SEL-STA-25) while the rendered open method is `touch`, whatever the scroll … | 6.2 |
| SEL-PTR-19 | MUST | While `modal` and mounted, the internal backdrop blocks pointer interaction with everything outside the popup … | 6.2 |
| SEL-PTR-20 | MUST | Pressing portaled content that is a DOM descendant of the popup element (even if rendered elsewhere in the … | 6.2 |
| SEL-PTR-21 | MUST | A disabled trigger MUST NOT open the popup on any pointer gesture and MUST NOT invoke `onOpenChange`. | 6.2 |
| SEL-PTR-22 | MUST | With aligned positioning, opening can place an item directly under the still-held pointer; SEL-PTR-05/07/08 … | 6.2 |
| SEL-PTR-23 | MUST | A touch-opened popup (rendered open method `touch`) MUST use ordinary anchored positioning (SEL-POS-06) and … | 6.2 |
| SEL-PTR-24 | MUST | A click on `Select.Label` focuses the trigger and does not open the popup (SEL-DOM-15). A click on a native … | 6.2 |
| SEL-FOC-01 | MUST | The trigger is the only tab stop of a closed Select: `tabindex="0"` while enabled (SEL-DOM-07); the hidden … | 6.3 |
| SEL-FOC-02 | MUST | On open, focus MUST move into the popup. `Select.Popup` leaves the focus manager's `initialFocus` at its … | 6.3 |
| SEL-FOC-03 | MUST | While open, the highlighted item carries `tabindex="0"` and all other items `tabindex="-1"`; moving the … | 6.3 |
| SEL-FOC-04 | MUST | `Select.Popup` passes `returnFocus={finalFocus}` (SEL-API-39) and `openInteractionType` = the recorded open … | 6.3 |
| SEL-FOC-05 | MUST | `Select.Popup` passes `restoreFocus` (`true`) to its focus manager (`select/popup/SelectPopup.tsx:485`); the … | 6.3 |
| SEL-FOC-06 | MUST | When the hidden input receives focus (browser validation bubble, autofill UI, programmatic), focus MUST be … | 6.3 |
| SEL-FOC-07 | MUST | When the trigger receives focus while the popup is open **and** aligned positioning is active (the popup … | 6.3 |
| SEL-FOC-08 | MUST | When the trigger itself (not a descendant) receives focus while the popup is open, the highlight MUST be … | 6.3 |
| SEL-FOC-09 | MUST | Select never traps focus: even with `modal` (which only adds the backdrop and scroll lock), Tab leaves the … | 6.3 |
| SEL-FOC-10 | MUST | The field's `focused` flag (FFI-STA-09) is owned by the trigger: set on trigger focus (unless disabled) … | 6.3 |
| SEL-FOC-11 | MUST | A trigger focus event MUST start the 0 ms force-mount timer (SEL-STA-09) so that a subsequent typeahead … | 6.3 |
| SEL-FOC-12 | MUST | `Select.Popup`'s focus manager is non-modal with `closeOnFocusOut` at its default `true` … | 6.3 |
| SEL-DIS-01 | MUST | Escape closes per SEL-KEY-10. It is not suppressed by `disabled` or `readOnly` (the dismiss interaction is … | 6.4 |
| SEL-DIS-02 | MUST | The Root calls the shared dismiss interaction with every option at its default … | 6.4 |
| SEL-DIS-03 | MUST | Focus leaving the trigger/popup tree closes with reason `focus-out` (SEL-FOC-12, SEL-KEY-11); the field is … | 6.4 |
| SEL-DIS-04 | MUST | Pressing the trigger while open closes with reason `trigger-press` (SEL-PTR-01); a keyboard click on the … | 6.4 |
| SEL-DIS-05 | MUST | Releasing the opening press outside the trigger and the positioner (beyond the 5 px tolerance) closes with … | 6.4 |
| SEL-DIS-06 | MUST | While the popup is open with aligned positioning active, a `resize` event on the positioner's window MUST … | 6.4 |
| SEL-DIS-07 | MUST | In single mode, activating an item requests a close with reason `item-press` immediately after the value … | 6.4 |
| SEL-DIS-08 | MUST | `actionsRef.current.close()` closes with reason `imperative-action` and no event while open, and does nothing … | 6.4 |
| SEL-DIS-09 | MUST | Trigger focus while aligned and open closes with reason `none` (SEL-FOC-07). | 6.4 |
| SEL-DIS-10 | MUST | A Select nested in another popup (e.g. `Popover`) closes only itself: Escape does not propagate (SEL-KEY-10) … | 6.4 |
| SEL-DIS-11 | MUST | Scrolling never dismisses the popup; instead page scroll is locked while open when `modal` or aligned … | 6.4 |
| SEL-DIS-12 | MUST | Every dismissal is a request: a close canceled through `cancel()` in `onOpenChange` leaves the popup open … | 6.4 |
| SEL-EVT-01 | MUST | `onValueChange(nextValue, details)` is invoked synchronously inside the originating handler, before any state … | 7 |
| SEL-EVT-02 | MUST | The reasons emitted by `onValueChange` are exactly: `item-press` for an item activation (click, drag-release … | 7 |
| SEL-EVT-03 | MUST | Calling `details.cancel()` in `onValueChange` MUST prevent the commit for an uncontrolled value and MUST … | 7 |
| SEL-EVT-04 | MUST | In single mode `onValueChange` MUST fire on every item activation, including activating the already-selected … | 7 |
| SEL-EVT-05 | MUST | In multiple mode an activation requests, for an unselected item, the current array (or `[]` when the value is … | 7 |
| SEL-EVT-06 | MUST | `onValueChange` MUST NOT fire for a change of the controlled `value` prop (EVD-STA-06); such a change still … | 7 |
| SEL-EVT-07 | MUST | Exactly one `onValueChange` call results from one activation (drag-release, click or key), and a controlled … | 7 |
| SEL-EVT-08 | MUST | `onOpenChange(nextOpen, details)` fires for every open/close request with the reasons of §6.4 plus … | 7 |
| SEL-EVT-09 | MUST | `details.cancel()` in `onOpenChange` MUST abort the request entirely (EVD-EVT-07): no state change, no … | 7 |
| SEL-EVT-10 | MUST | `details.preventUnmountOnClose()` called during a non-canceled close request MUST keep the popup mounted … | 7 |
| SEL-EVT-11 | MUST | `onOpenChangeComplete(true)` fires once per open after the popup element's enter animations finish … | 7 |
| SEL-EVT-12 | MUST | In single mode the close request of SEL-DIS-07 is issued with its own details object after the value request, … | 7 |
| SEL-EVT-13 | MUST | Value-change side effects — clearing the form's external error for the effective name, recomputing `dirty` … | 7 |
| SEL-EVT-14 | MUST | The `children` function of `Select.Value` is invoked during rendering with the current value and MUST NOT … | 7 |
| SEL-EVT-15 | MUST | `isItemEqualToValue` receives an item value as the first argument and a selected value as the second in every … | 7 |
| SEL-EVT-16 | MUST | `itemToStringValue` and `itemToStringLabel` are invoked only with non-nullish single values (SEL-API-20/21) … | 7 |
| SEL-EVT-17 | MUST | A `finalFocus` function is called exactly once per close with the close interaction type derived from the … | 7 |
| SEL-FRM-01 | MUST | The Root registers as the field's active control (FFI-ALG-12) with: control handle = the trigger element … | 8 |
| SEL-FRM-02 | MUST | The value projected into `Form.Values` / `formValues` is `stringifyAsValue(value, itemToStringValue)` in … | 8 |
| SEL-FRM-03 | MUST | The field is marked *touched* (FFI-STA-07) when the trigger blurs with a related target outside the … | 8 |
| SEL-FRM-04 | MUST | *Dirty* (FFI-STA-05) is `isSelectedValueDirty(value, initialValue, isItemEqualToValue)` (UTL): for two … | 8 |
| SEL-FRM-05 | MUST | *Filled* (FFI-STA-08) is `hasSelectedValue` (SEL-STA-02), written post-commit, pre-paint on every change: a … | 8 |
| SEL-FRM-06 | MUST | On every committed value change (SEL-EVT-13) the Root runs `clearErrors(effectiveName)` → `setDirty(…)` → … | 8 |
| SEL-FRM-07 | MUST | In `onBlur` mode the current value is committed (`validation.commit(value)`, FFI-FRM-03) at the same … | 8 |
| SEL-FRM-08 | MUST | Native constraint validation uses the hidden input: with `required` and no selection the input reports … | 8 |
| SEL-FRM-09 | MUST | A `Form.errors` entry for the effective name marks the field invalid (`aria-invalid` on the trigger, error … | 8 |
| SEL-FRM-10 | MUST | `Field.Root.name` overrides the Root's `name` for the hidden input(s), error lookup and `clearErrors` … | 8 |
| SEL-FRM-11 | MUST | While effectively disabled the hidden input and every per-value input carry `disabled`, so nothing is … | 8 |
| SEL-FRM-12 | MUST | While `readOnly` the hidden input carries `readonly` (a barred input, FFI-FRM-08: no native constraint … | 8 |
| SEL-FRM-13 | MUST | The `form` prop lets a Select rendered outside its `<form>` submit with it (single and multiple), FFI-DOM-08. | 8 |
| SEL-FRM-14 | MUST | A `Field.Label` is associated with the trigger: the label's `for` equals the trigger's id (Root id, or the … | 8 |
| SEL-FRM-15 | MUST | `Select.Label` provides the accessible name when no `Field.Label` exists (SEL-ARIA-05, FFI-ARIA-07) and … | 8 |
| SEL-FRM-16 | MUST | `Field.Description` / `Field.Error` ids are composed into the trigger's and hidden input's `aria-describedby` … | 8 |
| SEL-FRM-17 | MUST | `aria-invalid="true"` appears on the trigger (and hidden input) while the field is invalid and the control … | 8 |
| SEL-FRM-18 | MUST | *Browser autofill.* A `change` event on the hidden input that is not default-prevented, while enabled and not … | 8 |
| SEL-FRM-19 | MUST | Autofill MUST be ignored (no `onValueChange`, hidden input value unchanged, field state unchanged) when the … | 8 |
| SEL-FRM-20 | MUST | The field's `focused` flag follows SEL-FOC-10, so `data-focused` is removed without a blur event when the … | 8 |
| SEL-FRM-21 | MUST | Multiple-mode submission produces one `FormData` entry per selected value under the effective name … | 8 |
| SEL-FRM-22 | MUST | In multiple mode `required` is enforced only while the selection is empty (SEL-DOM-50). | 8 |
| SEL-FRM-23 | MUST | The Root's `id` becomes the labelable control id even inside a `Field.Root` (the field does not override it). | 8 |
| SEL-FRM-24 | MUST | Select does not react to a native form `reset` (FFI-FRM-30); the selected value, dirty/touched/filled flags … | 8 |
| SEL-ANI-01 | MUST | The popup's mounted flag and transition status follow TRN-ALG-01/05 with the options of SEL-STA-10: an … | 9 |
| SEL-ANI-02 | MUST | While not mounted, the Portal renders only if force-mounted (SEL-STR-06); when it renders, the Positioner and … | 9 |
| SEL-ANI-03 | MUST | `Select.Popup` and `Select.Backdrop` carry `data-starting-style`/`data-ending-style` per TRN-DOM-01…03 … | 9 |
| SEL-ANI-04 | MUST | While `'starting'`, the Positioner and the Popup carry inline `transition: none` (SEL-DOM-21/30) so the enter … | 9 |
| SEL-ANI-05 | MUST | `onOpenChangeComplete(true)` fires on animations finished for the Popup element, inspected once … | 9 |
| SEL-ANI-06 | MUST | `onOpenChangeComplete(false)` fires exactly once per close, from the unmount cleanup: on animations finished … | 9 |
| SEL-ANI-07 | MUST | The unmount cleanup MUST, in one update, set the highlighted item to none, the open method to `null`, and … | 9 |
| SEL-ANI-08 | MUST | `actionsRef.current.unmount()` while open is deferred until the current update commits and dropped if the … | 9 |
| SEL-ANI-09 | MUST | A `preventUnmountOnClose()` opt-out belongs to one close cycle: a reopen (including a controlled … | 9 |
| SEL-ANI-10 | MUST | A Select that mounts open (`defaultOpen`, controlled `open`) MUST NOT play an enter phase … | 9 |
| SEL-ANI-11 | MUST | Each scroll arrow runs its own transition machine on its *visible* flag (TRN §6.1): becoming visible mounts … | 9 |
| SEL-ANI-12 | MUST | `Select.ItemIndicator` with `keepMounted` keeps the same element across selection changes, shows … | 9 |
| SEL-ANI-13 | MUST | The focus manager is disabled while the popup is not mounted, so focus return happens when the subtree … | 9 |
| SEL-ANI-14 | MUST | Items keep their highlight through the closing transition (SEL-STA-07) and ignore hover during it (the … | 9 |
| SEL-POS-01 | MUST | Outside aligned mode the popup is positioned by the mechanism with the Select defaults: `side 'bottom'` … | 10 |
| SEL-POS-02 | MUST | The positioner is always configured as kept-mounted (`keepMounted: true`): while not mounted the mechanism … | 10 |
| SEL-POS-03 | MUST | *Aligned mode is active* iff the popup is mounted, the latched `alignItemWithTrigger` (SEL-POS-05) is `true` … | 10 |
| SEL-POS-04 | MUST | *Fallback.* During placement (SEL-POS-07) aligned mode MUST be abandoned — restoring the positioner's … | 10 |
| SEL-POS-05 | MUST | The latched `alignItemWithTrigger` starts as the prop value and is re-synchronized to the prop only while the … | 10 |
| SEL-POS-06 | MUST | A popup opened by touch (rendered open method `touch`, including through the closing transition and a quick … | 10 |
| SEL-POS-07 | MUST | *Placement.* Post-commit, pre-paint after each relevant committed update while the popup is open with trigger, positioner and … | 10 |
| SEL-POS-08 | MUST | The constants of SEL-POS-07 are normative: horizontal padding 5 px each side; trigger-to-viewport collision … | 10 |
| SEL-POS-09 | MUST | The placement runs once per open (it is skipped while `'ending'`) and again whenever its inputs change while … | 10 |
| SEL-POS-10 | MUST | After an aligned placement, when `highlightItemOnHover` is `true`, no item is selected (selected index … | 10 |
| SEL-POS-11 | MUST | *Scroll-grow.* While aligned and placed and *reached max height* is `false`, a `scroll` event on the scroller … | 10 |
| SEL-POS-12 | MUST | Outside aligned mode, scroll-arrow visibility is computed on the next frame after placement (the List, when … | 10 |
| SEL-POS-13 | MUST | When the popup closes (or aligned mode deactivates) the placement flags are reset and the positioner's … | 10 |
| SEL-POS-14 | MUST | `data-side` is `"none"` on the Positioner, Popup and scroll arrows while aligned mode is active (so styles … | 10 |
| SEL-POS-15 | MUST | Even in aligned mode the trigger's `data-popup-side` reports the mechanism's resolved side (typically … | 10 |
| SEL-POS-16 | MUST | While aligned, the mechanism's anchor tracking (scroll/resize observers) is disabled unless … | 10 |
| SEL-POS-17 | MUST | Direction (`DirectionProvider`, UTL-I18N-01) selects the horizontal alignment edge in SEL-POS-07 step 4 (LTR … | 10 |
| SEL-POS-18 | MUST | Rect and size measurements in placement and scroll-grow are divided by the element's rendered scale (FLT … | 10 |
| SEL-POS-19 | MUST | The popup's inline `transform`, `scale` and `translate` are restored exactly (or removed when they were … | 10 |
| SEL-POS-20 | MUST | `Select.Arrow` renders only outside aligned mode (SEL-STR-09) and then carries the mechanism's arrow styles … | 10 |
| SEL-POS-21 | MUST | Page scroll is locked (UTL `useScrollLock` with the trigger as reference element, PSH-PTR-07) while the popup … | 10 |
| SEL-POS-22 | MUST | A scroll before the first placement of an open (the popup not yet measured) MUST neither resize the … | 10 |
| SEL-POS-23 | MUST | The scroller whose scroll events drive SEL-POS-11/12 and SEL-STA-29 is the List element when one is … | 10 |
| SEL-POS-24 | MUST | With `alignItemWithTrigger={false}` the selected item is revealed on open by scrolling it into view … | 10 |
| SEL-I18N-01 | MUST | Reading direction (`DirectionProvider`, default `ltr`) affects only the horizontal edge used by aligned … | 12 |
| SEL-EDGE-01 | MUST | The missing-ancestor errors are thrown with exactly these messages: `Base UI: SelectRootContext is missing. … | 12 |
| SEL-EDGE-02 | SHOULD | The controlled-state diagnostics UTL-EDGE-22/23 are emitted with `name: 'Select'` and `state: 'value'` … | 12 |
| SEL-EDGE-03 | SHOULD | The button-mechanism diagnostics UBT-EDGE-01/02 apply to the Trigger (default native: rendering a … | 12 |
| SEL-EDGE-04 | MUST | *Disabled summary.* With the Root disabled (SEL-API-13): the trigger cannot be focused or pressed (native) / … | 12 |
| SEL-EDGE-05 | MUST | *Read-only summary.* With `readOnly` (SEL-API-12): everything opens and navigates normally; item activation … | 12 |
| SEL-EDGE-06 | MUST | When the popup is forced open by the consumer while the Root is disabled or `readOnly`, item activation MUST … | 12 |
| SEL-EDGE-07 | MUST | Typeahead treats attribute-disabled items as unavailable: a keystroke whose only matches are disabled … | 12 |
| SEL-EDGE-08 | MUST | Duplicate item values: every item whose value equals the selected value reports selected … | 12 |
| SEL-EDGE-09 | MUST | With zero items the popup still opens and closes normally; arrow/Home/End keys and typeahead do nothing; the … | 12 |
| SEL-EDGE-10 | MUST | Items removed while the popup is open leave no navigable gap: arrow keys skip vacated indices and stop at the … | 12 |
| SEL-EDGE-11 | MUST | Server-rendered output contains the trigger (with its generated id), the Label (with `${id}-label`) and the … | 12 |
| SEL-EDGE-12 | MUST | Inside a `Toolbar.Root` with the Trigger rendered through `Toolbar.Button`: the trigger keeps … | 12 |
| SEL-EDGE-13 | MUST | Inside another popup (Popover, Dialog): the Select is a child in the popup tree, so its Escape does not close … | 12 |
| SEL-EDGE-14 | MUST | Platform cases: on WebKit a pinch-zoomed visual viewport forces the positioning fallback (SEL-POS-04) and … | 12 |
| SEL-EDGE-15 | MUST | A controlled multiple-mode `value` of `null` is treated as an empty selection (SEL-STA-11) and the first … | 12 |
| SEL-EDGE-16 | MUST | An Item rendered outside any Positioner (no item registry) has index `-1`, registers no value or label, never … | 12 |
| SEL-EDGE-17 | MUST | Several `Select.Value` parts may be rendered; each resolves its content independently, and the last one to … | 12 |
| SEL-EDGE-18 | MUST | A `Select.GroupLabel` id change (explicit ↔ generated) MUST update the group's `aria-labelledby` without … | 12 |
| SEL-EDGE-19 | MUST | `items` lookup in record form uses own-property lookup for label resolution but the `in` operator (prototype … | 12 |
| SEL-EDGE-20 | MUST | A `Select.Value` `children` function or `items` lookup that throws, or an `itemToStringValue` that throws for … | 12 |
| SEL-EDGE-21 | MUST | A `render` callback is opaque (RND-DOM-32): the part hands it the computed `id` and does not detect whether … | 12 |

## 16. Open questions

1. **`keepMounted` on the Portal (SEL-API-33, SEL-API-36).** The `actionsRef` JSDoc (`select/root/SelectRoot.tsx:677-682`) says "Whether it leaves the DOM is decided by `keepMounted` on the portal", and `CHANGELOG.md v1.0.0-alpha.5` moved `keepMounted` to the Portal, but `SelectPortal.Props` (`select/portal/SelectPortal.tsx:32-38`) exposes only `container`. In 1.9.0 the popup subtree is effectively always kept mounted once force-mounted (SEL-STR-06) and `unmount()` only ends the closing phase (hidden positioner). The orchestrator's hint "keepMounted" does not exist as a Select prop; the doc specifies the actual behavior. Is the JSDoc stale, or is a Portal `keepMounted` planned?
2. **Unchanged-value `onValueChange` (SEL-EVT-04).** Confirmed from `select/item/SelectItem.tsx:147-149`: activating the already-selected item in single mode calls `onValueChange` with the same value (no equality guard), matching EVD §4.3 "Select onValueChange (single, item press)" and EVD §10 Q6. No test covers it; a port should preserve the call (consumers may rely on it to re-close) unless upstream adds a guard.
3. **Close after a canceled value change (SEL-EVT-12).** `select/item/SelectItem.tsx:146-149` issues the `item-press` close with a separate details object after `setValue`, without checking `isCanceled`; canceling `onValueChange` therefore does not keep the popup open. EVD-EVT-08 lists "the item-press close of Combobox/Select (`onOpenChange` not requested after a canceled selection)" — true for Combobox, not for Select in 1.9.0. The EVD author should narrow that clause. Resolved (QA 2026-10-14): EVD-EVT-08 now records Select as the exception (`select/item/SelectItem.tsx:146-149` re-verified); SEL-EVT-12 cites it.
4. **`SelectFloatingContext` error (SEL-EDGE-01).** `select/root/SelectRootContext.ts:21-29` defines a second error message for the floating context; since the Root always provides both contexts, the message is unreachable on its own (the root-context check runs first in every part). Documented verbatim for completeness.
5. **`finalFocus` close type for item presses (SEL-EVT-17).** `select/popup/SelectPopup.test.tsx:1733-1735` documents a "known gap": an item press closes through the Root's own `setOpen`, so the focus manager never records a pointer close type and the function receives `''` instead of `'mouse'`. Ports should reproduce `''` to pass the suite, but this looks like a bug upstream.
6. **`data-required` on the trigger (SEL-DOM-03).** `select/trigger/SelectTriggerDataAttributes.ts:24-27` and the API outline list `data-required`, but `SelectTriggerState` (`select/trigger/SelectTrigger.tsx:236-257`) has no `required` field and the state object built at `:218-226` does not include it, so the attribute is never rendered (`required` is read only for `aria-required`). Either the attribute export/docs are wrong or the state is missing a field. The doc follows the runtime.
7. **Outline discrepancies.** `components/page.mdx` lists no data attributes for `Select.Label`, yet it renders the field attributes (`data-disabled`, `data-touched`, `data-dirty`, `data-filled`, `data-focused`, `data-valid`, `data-invalid`, SEL-DOM-14). `Select.Trigger` accepts an `id` prop (`select/trigger/SelectTrigger.tsx:50`) not listed in the outline (SEL-API-26). `Select.Icon` and `Select.ScrollUpArrow`/`ScrollDownArrow` accept `children` (not listed). `Select.Item` omits `id` from its type but forwards it at runtime (SEL-API-44).
8. **`Field.Label` vs `Select.Label` click (SEL-PTR-24).** A native `Field.Label` click opens the popup (the browser's forwarded activation click reaches the trigger's click handler with no recorded pointer type), while `Select.Label` only focuses the trigger. `SelectRoot.test.tsx` "Field.Label links to trigger and focuses it" (`select/root/SelectRoot.test.tsx:4387`) asserts the *listbox* has focus, i.e. the popup opened, while "Select.Label focuses trigger without opening" (`:4363`) asserts the opposite for `Select.Label`. The docs page says a label click "focuses the select trigger without opening the popup" only for `Select.Label`. Intended asymmetry?
9. **Typeahead reset delay.** Select uses the typeahead hook's default `resetMs = 750` (`floating-ui-react/hooks/useTypeahead.ts:81`); the shared constant `TYPEAHEAD_RESET_MS = 500` (`internals/constants.ts:3`) is not applied to Select. The popup-list suite waits 1000 ms, so either value passes; the doc states 750 ms. Resolved (QA 2026-10-14): the timeout is cited as CMP-STA-43 (default `resetMs` 750 ms; CMP §6 constants table agrees); Menu alone passes `TYPEAHEAD_RESET_MS` (`menu/root/MenuRoot.tsx:561`).
10. **`onOpenChangeComplete(true)` without a Popup.** The open-completion watcher lives in `Select.Popup` (`select/popup/SelectPopup.tsx:178-186`); a Select rendered without a Popup part never reports `true` while `false` is still reported by the Root. Probably acceptable (Popup is the documented anatomy) but worth noting for ports.
11. **Record `items` null-label lookup (SEL-EDGE-19).** `hasNullItemLabel` uses `'null' in items` (prototype chain) while label lookup uses own-property (`internals/resolveValueLabel.tsx:43,113`); already raised in UTL §10 Q7. Only observable with records inheriting a `null` key.
12. **TRN §10 Q7 confirmation.** `select/item-indicator/SelectItemIndicator.tsx:20-25` unmounts the indicator in the deselecting commit when `keepMounted` is `false`; the exit phase exists only with `keepMounted` (SEL-STR-11, SEL-ANI-12). Confirmed.
13. **Scroll-arrow `data-side="none"`.** Scroll arrows read `side` from the Positioner context (`select/scroll-arrow/SelectScrollArrow.tsx:32,76`), so while aligned they carry `data-side="none"` even though the arrows are positioned relative to the popup, not the anchor; harmless, noted for styling.
14. **`popupListConformanceTests` `modal` default.** Select runs the suite with the default `modal: true` (`test/popupListConformanceTests.tsx:33`); the suite's outside-press test uses a forced click through the backdrop (`test/popupListConformanceTests.tsx:999-1003`). A port whose backdrop swallows the press differently may need the same escape hatch.
