# Event details and reasons

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/internals/createBaseUIEventDetails.ts`, `internals/reasons.ts`, `internals/reason-parts.ts`, `internals/types.ts`, `types/index.ts`, `internals/useValueChanged.ts`, `packages/utils/src/useControlled.ts`, `utils/popups/popupStoreUtils.ts` (shared popup open-change sequence), `floating-ui-react/components/FloatingRootStore.ts` (internal `openchange` notification), `floating-ui-react/hooks/useDismiss.ts` (Escape cancel/propagation), `merge-props/mergeProps.ts` (`preventBaseUIHandler`), `utils/getHighlightReason.ts`, and every component `set*`/`handle*` path cited inline in §4.3 |
| Tests | `internals/createBaseUIEventDetails.spec.ts`, `internals/useValueChanged.test.tsx`, `packages/utils/src/useControlled.test.tsx`; partial (only the titles on this doc's `Test:` lines; the rest of each file is covered by the owning doc): `utils/popups/popupStoreUtils.test.tsx` ("getPopupOpenState" and "applyPopupOpenChange" blocks; rest PSH), `floating-ui-react/hooks/useDismiss.test.tsx` (Escape preventDefault titles; rest FLT), `merge-props/mergeProps.test.ts` (`preventBaseUIHandler` titles; rest RND); plus the component tests cited inline |
| Prefix | `EVD` |
| Status | approved |
| Used by | Every component document. Directly: accordion, alert-dialog, autocomplete, checkbox, checkbox-group, collapsible, combobox, context-menu, dialog, drawer, field, form, input, menu, menubar, navigation-menu, number-field, otp-field, popover, preview-card, radio, radio-group, select, slider, switch, tabs, toggle, toggle-group, tooltip. Architecture: `floating-and-positioning.md` (FLT), `popup-store-and-handles.md` (PSH), `transitions-and-mounting.md` (TRN), `field-and-form-infrastructure.md` (FFI), `button-behavior.md` (UBT), `rendering-and-composition.md` (RND), `shared-utilities.md` (UTL) |

## 1. Purpose

Every Base UI change callback (`onOpenChange`, `onValueChange`, `onCheckedChange`, `onPressedChange`, `onInputValueChange`, `onSnapPointChange`, `onItemHighlighted`, `onValueCommitted`, `onValueInvalid`, `onValueComplete`, `onFormSubmit`) receives the *new value* as its first argument and an **event-details object** as its second argument. This object is the only channel through which a consumer learns *why* a change happened (`reason`), *which native input* caused it (`event`), *which element* started it (`trigger`), and the only channel through which the consumer can **veto** the change (`cancel()`), **let the native event keep bubbling** where Base UI would otherwise stop it (`allowPropagation()`), or **keep a closing popup mounted** until it says otherwise (`preventUnmountOnClose()`).

A port cannot skip this mechanism because:

1. The entire controlled/uncontrolled contract is expressed through it: a callback is a *request*; uncontrolled components commit the request only if it is not canceled, and controlled components never commit anything themselves.
2. Dismissal of nested popups (Escape bubbling), focus return after close, form validation, group value propagation, commit callbacks and exit-animation handling are all gated on `isCanceled` / `isPropagationAllowed` / `preventUnmountOnClose`.
3. The `reason` string set is a public API (`Component.Part.ChangeEventReason` types) that consumers branch on; each component may only emit the reasons it declares, and the test suite asserts specific reasons for specific interactions.

This document specifies the shared object shape, the complete reason catalogue, the semantics of cancel / propagation / unmount prevention, the ordering of callbacks relative to state and DOM updates, the controlled/uncontrolled contract every component document links to, and a **matrix** of which reasons each component callback can emit.

## 2. Concepts and terminology

| Base UI / React name | Neutral term used in this and other docs | Definition |
|---|---|---|
| `eventDetails`, `details` | **event details** | The structured second argument of a change callback. |
| `BaseUIChangeEventDetails<Reason, Custom>` | **change-event details** | Event details for a *cancelable* change: carries `reason`, `event`, `trigger`, `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed`, plus component-specific fields. |
| `BaseUIGenericEventDetails<Reason, Custom>` | **generic-event details** | Event details for a *non-cancelable* notification (`onValueCommitted`, `onValueInvalid`, `onValueComplete`, `onFormSubmit`): carries only `reason`, `event` and component-specific fields. |
| `BaseUIHighlightEventDetails<Reason, Custom>` | **highlight-event details** | Generic-event details for `onItemHighlighted`; when `reason` is `pointer` the `event` may be a mouse event or a pointer event. |
| `OpenChangeEventDetails` | **open-change details** | Change-event details passed to a popup's `onOpenChange` (or NavigationMenu's `onValueChange`), extended with `preventUnmountOnClose()`. |
| `reason` | **reason** | A string literal naming the cause of the change. The full catalogue is in §3.5. |
| `REASONS.xxx` | reason constant | The source refers to reasons through constants (`REASONS.triggerPress`); the public value is the kebab-case literal (`'trigger-press'`). Component docs use the literal. |
| `event` (`event.nativeEvent` in React) | **native event** | The platform event that caused the change. When there is none, a synthetic placeholder event of type `base-ui` is supplied. |
| `trigger` | **initiating element** | The element whose interaction initiated the change (a trigger button, a focus guard, a swipe area), or `undefined`. |
| `cancel()` / `isCanceled` | **cancel** / **canceled** | Vetoes the change. See §4.3. |
| `allowPropagation()` / `isPropagationAllowed` | **allow propagation** | Opts out of Base UI's `stopPropagation()` on the native event. See §4.4. |
| `preventUnmountOnClose()` | **prevent unmount on close** | Keeps a closing popup in its closing phase until the consumer calls the `unmount` action. Owned by `transitions-and-mounting.md`; the part of the contract that lives on the details object is specified here. |
| `onOpenChangeComplete(open)` | **open-change-complete callback** | Fired after the enter or exit animation has finished. Not an event-details callback (single boolean argument). |
| `useControlled` | **controlled / uncontrolled value** | A value is *controlled* when the consumer passes the value prop (`open`, `value`, `checked`, `pressed`, `inputValue`, `snapPoint`, …) and *uncontrolled* when the consumer passes only the default prop (`defaultOpen`, `defaultValue`, …). |
| "internal state update", `setXxxUnwrapped` | **commit** | The step where an uncontrolled component stores the requested value. Controlled components skip it. |
| `preventBaseUIHandler()` / `baseUIHandlerPrevented` | **handler prevention** | A method added to every DOM event object passed to a consumer-supplied DOM handler (`onClick`, `onKeyDown`, …) that stops Base UI's own handler for the same event from running. Distinct from `cancel()`. |
| `FloatingUIOpenChangeDetails`, `openchange` event | **internal open-change notification** | A non-public notification emitted to the popup's interaction machinery after a non-canceled open change (§4.3, `EVD-EVT-14`). Owned by `floating-and-positioning.md`. |
| `instantType` | **instant phase** | A popup-store flag derived from the reason that suppresses animation for some transitions. Owned by `popup-store-and-handles.md` / `transitions-and-mounting.md`; the reason → flag mapping is stated here because the reason is its input. |

## 3. Public surface

### 3.1 Change-event details (`BaseUIChangeEventDetails<Reason, CustomProperties = {}>`)

Exported from the package root as a type (`types/index.ts:3-6`). Fields (`internals/createBaseUIEventDetails.ts:56-85`):

| Field | Type | Meaning |
|---|---|---|
| `reason` | `Reason` (string literal union) | Why the change happened. JSDoc: "The reason for the event." |
| `event` | `ReasonToEvent<Reason>` (native event type keyed by reason, see §3.5; falls back to `Event`) | "The native event associated with the custom event." |
| `cancel()` | `() => void` | "Cancels Base UI from handling the event." |
| `allowPropagation()` | `() => void` | "Allows the event to propagate in cases where Base UI will stop the propagation." |
| `isCanceled` | `boolean` (read-only accessor) | "Indicates whether the event has been canceled." |
| `isPropagationAllowed` | `boolean` (read-only accessor) | "Indicates whether the event is allowed to propagate." |
| `trigger` | `Element \| undefined` | "The element that triggered the event, if applicable." |
| …`CustomProperties` | component-specific | See §3.7. |

### 3.2 Generic-event details (`BaseUIGenericEventDetails<Reason, CustomProperties = {}>`)

Exported from the package root (`types/index.ts:3-6`). Fields (`internals/createBaseUIEventDetails.ts:98-112`): `reason`, `event`, plus `CustomProperties`. There is **no** `cancel`, `allowPropagation`, `isCanceled`, `isPropagationAllowed` or `trigger`. Used by `onValueCommitted` (Slider, NumberField), `onValueInvalid` / `onValueComplete` (OTPField), `onFormSubmit` (Form), and (through the highlight variant) `onItemHighlighted` (Menu, Combobox, Autocomplete).

### 3.3 Highlight-event details (`BaseUIHighlightEventDetails<Reason, CustomProperties = {}>`)

Not exported from the package root; surfaced per component as `Menu.Root.HighlightEventDetails`, `Combobox.Root.HighlightEventDetails`, `Autocomplete.Root.HighlightEventDetails`. Identical to generic-event details except that for `reason === 'pointer'` the `event` is typed `MouseEvent | PointerEvent` rather than `PointerEvent` (`internals/createBaseUIEventDetails.ts:114-120`), because hover highlights arrive on mouse events and leave-highlights on pointer events.

### 3.4 DOM-event handler prevention (`BaseUIEvent<E>`)

Every consumer-supplied DOM handler on a Base UI part receives the event object augmented with (`types/index.ts:23-26`):

| Member | Type | Meaning |
|---|---|---|
| `preventBaseUIHandler()` | `() => void` | Marks the event so that Base UI's own handler for the same event on the same part does not run. |
| `baseUIHandlerPrevented` | `boolean \| undefined` (read-only) | `true` once `preventBaseUIHandler()` was called. |

The prop-merging mechanism that installs these members is owned by `rendering-and-composition.md` (`mergeProps`). What it means for *callbacks* is specified in `EVD-API-11` / `EVD-EVT-26`.

### 3.5 Reason catalogue

All 36 literals are defined in `internals/reason-parts.ts:1-43` and re-exported as the `REASONS` namespace (`internals/reasons.ts:1-5`); `BaseUIEventReason` is the union of all of them. The "native event type" column is the `ReasonToEventMap` entry (`internals/createBaseUIEventDetails.ts:4-47`); it is a *type-level promise*, the runtime passes whatever event the emitter had.

| Literal | Constant | Native event type | Meaning / user action that produces it |
|---|---|---|---|
| `none` | `none` | `Event` | No specific interaction, or the component does not distinguish interactions (all form controls: Checkbox, Switch, Radio, Toggle, Field.Control, Tabs user activation); also programmatic / automatic syncs (autofill, typeahead-while-closed, input re-sync, detached-trigger unmount, delay-group handoff, `hidden="until-found"` reveal) and Form submit. |
| `trigger-press` | `triggerPress` | `MouseEvent \| PointerEvent \| TouchEvent \| KeyboardEvent` | The popup trigger was clicked / pressed (mouse, touch, pen, or keyboard-generated click); also a context-menu long-press/right-click; also Collapsible/Accordion trigger activation. |
| `trigger-hover` | `triggerHover` | `MouseEvent` | The pointer entered (open) or left (close) a hover-enabled trigger or its popup (Tooltip, PreviewCard, Popover with `openOnHover`, Menu with `openOnHover`, NavigationMenu). |
| `trigger-focus` | `triggerFocus` | `FocusEvent` | The trigger received keyboard focus (open) or lost it (close). Wired for Tooltip, PreviewCard and Menu (Menu: only `Menu.Trigger` inside a Menubar while another bar menu is open; never ContextMenu); declared but not wired for Popover (§10 Q3). |
| `outside-press` | `outsidePress` | `MouseEvent \| PointerEvent \| TouchEvent` | A press (pointerdown / click / touch, per `outsidePressEvent`) landed outside the popup and its triggers. |
| `item-press` | `itemPress` | `MouseEvent \| KeyboardEvent \| PointerEvent` | A list item / menu item / option was activated (click or Enter/Space-generated click). |
| `close-press` | `closePress` | `MouseEvent \| KeyboardEvent \| PointerEvent` | A dedicated Close part (`Dialog.Close`, `Popover.Close`, `Drawer.Close`, Combobox's internal dismiss button) was activated. |
| `link-press` | `linkPress` | `MouseEvent \| PointerEvent` | A `NavigationMenu.Link` with `closeOnClick` was activated. |
| `clear-press` | `clearPress` | `PointerEvent \| MouseEvent \| KeyboardEvent` | A Clear part (`Combobox.Clear`, `Menu.Clear`) was activated. |
| `chip-remove-press` | `chipRemovePress` | `PointerEvent \| MouseEvent \| KeyboardEvent` | A `Combobox.ChipRemove` button was activated (click, Enter, Space). |
| `track-press` | `trackPress` | `PointerEvent \| MouseEvent \| TouchEvent` | The slider control/track was pressed (initial pointerdown/touchstart). |
| `increment-press` | `incrementPress` | `PointerEvent \| MouseEvent \| TouchEvent` | `NumberField.Increment` was clicked or press-and-held. |
| `decrement-press` | `decrementPress` | `PointerEvent \| MouseEvent \| TouchEvent` | `NumberField.Decrement` was clicked or press-and-held. |
| `input-change` | `inputChange` | `InputEvent \| Event` | Text was typed (or a composition committed) into a text input (Combobox input, NumberField input, OTP slot, Menu filter input, hidden range input of Slider via form integration). |
| `input-clear` | `inputClear` | `InputEvent \| FocusEvent \| Event` | The text input became empty (typing/backspace, blur of an empty NumberField, Combobox clean-ups after selection or close). |
| `input-blur` | `inputBlur` | `FocusEvent` | NumberField input lost focus and the typed text was committed. |
| `input-paste` | `inputPaste` | `ClipboardEvent` | Text was pasted into NumberField / OTPField. |
| `input-press` | `inputPress` | `MouseEvent \| PointerEvent \| TouchEvent \| KeyboardEvent` | The Combobox/Autocomplete *input* (as opposed to the trigger button) was pressed to open. |
| `focus-out` | `focusOut` | `FocusEvent \| KeyboardEvent` | Focus left the popup (Tab / Shift+Tab onto a focus guard, blur to an outside element, Shift+Tab in a list). |
| `escape-key` | `escapeKey` | `KeyboardEvent` | Escape was pressed. |
| `close-watcher` | `closeWatcher` | `Event` | The platform `CloseWatcher` requested a close (Android back gesture / Esc handled by the UA) — Drawer only. |
| `list-navigation` | `listNavigation` | `KeyboardEvent` | An arrow key opened the popup from its trigger, or a cross-orientation arrow key closed a submenu. |
| `keyboard` | `keyboard` | `KeyboardEvent` | A keyboard interaction changed a value or highlight that is not list navigation (arrow/Home/End stepping, typeahead highlight, OTP Backspace/Delete, chip removal keys). |
| `pointer` | `pointer` | `PointerEvent` (highlight details: `MouseEvent \| PointerEvent`) | A hover moved the highlight. |
| `drag` | `drag` | `PointerEvent \| TouchEvent` | A slider thumb was dragged. |
| `wheel` | `wheel` | `WheelEvent` | The mouse wheel changed a NumberField value. |
| `scrub` | `scrub` | `PointerEvent` | A `NumberField.ScrubArea` drag changed the value. |
| `popup-close` | `popupClose` | *(not in the map; resolves to `Event`)* | The filter query is reset because its popup closed (`Menu.FilterProvider`). |
| `cancel-open` | `cancelOpen` | `MouseEvent` | A press-open was aborted: mousedown opened the popup (or started a hover-open) and the matching mouseup landed outside the trigger/popup. |
| `sibling-open` | `siblingOpen` | `Event` | A sibling submenu opened or the parent menu closed without a reason, so this submenu closes. |
| `disabled` | `disabled` | `Event` | The component became disabled while open/selected (Tooltip closes; Tabs falls back from a disabled selected tab). |
| `missing` | `missing` | `Event` | Tabs: the selected tab was removed or an explicit default never matched a tab. |
| `initial` | `initial` | `Event` | Tabs: the first automatic selection (no explicit default). |
| `imperative-action` | `imperativeAction` | `Event` | `actionsRef.current.close()`, or a detached handle's `open()` / `openWithPayload()` / `close()`. |
| `swipe` | `swipe` | `PointerEvent \| TouchEvent` | A Drawer swipe gesture opened, closed, or moved to a snap point. |
| `window-resize` | `windowResize` | `UIEvent` | The window resized while a Select popup was aligned to its trigger. |

### 3.6 Per-component exported type names

Each component re-exports its reason union and details type under its namespace (and as flat `XxxChangeEventReason` / `XxxChangeEventDetails` names). All of these appear in the API outline (`docs/src/app/(docs)/react/components/page.mdx`).

| Component namespace | Types | Source |
|---|---|---|
| `Accordion.Root` | `ChangeEventReason`, `ChangeEventDetails` | `accordion/root/AccordionRoot.tsx:218-229` |
| `Accordion.Item` | `ChangeEventReason`, `ChangeEventDetails` | `accordion/item/AccordionItem.tsx:171-181` |
| `AlertDialog.Root` | `ChangeEventReason` (= Dialog's), `ChangeEventDetails` (with `preventUnmountOnClose`) | `alert-dialog/root/AlertDialogRoot.tsx:47-60` |
| `Autocomplete.Root` | `ChangeEventReason`, `ChangeEventDetails`, `OpenChangeEventDetails`, `HighlightEventReason`, `HighlightEventDetails` | `autocomplete/root/AutocompleteRoot.tsx:135-144, 303-305` |
| `Checkbox.Root` | `ChangeEventReason`, `ChangeEventDetails` | `checkbox/root/CheckboxRoot.tsx:511-520` |
| `CheckboxGroup` | `ChangeEventReason`, `ChangeEventDetails` | `checkbox-group/CheckboxGroup.tsx:218-227` |
| `Collapsible.Root` | `ChangeEventReason`, `ChangeEventDetails` | `collapsible/root/CollapsibleRoot.tsx:106-115` |
| `Combobox.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `isItemPress?`), `OpenChangeEventDetails`, `HighlightEventReason`, `HighlightEventDetails` | `combobox/root/AriaCombobox.tsx:2012-2045`, `combobox/root/ComboboxRoot.tsx:174-195` |
| `ContextMenu.Root` / `.SubmenuRoot` / `.CheckboxItem` / `.RadioGroup` | same as the Menu equivalents | `context-menu/root/ContextMenuRoot.tsx:88-97` |
| `Dialog.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `preventUnmountOnClose`) | `dialog/root/DialogRoot.tsx:98-119` |
| `Drawer.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `preventUnmountOnClose`), `SnapPointChangeEventReason` (= `ChangeEventReason`), `SnapPointChangeEventDetails` (plain change-event details) | `drawer/root/DrawerRoot.tsx:356-386` |
| `Field.Control`, `Input` | `ChangeEventReason`, `ChangeEventDetails` | `field/control/FieldControl.tsx:213-223`, `input/Input.tsx:37-45` |
| `Form` | `SubmitEventReason`, `SubmitEventDetails` (generic) | `form/Form.tsx:178-179` |
| `Menu.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `preventUnmountOnClose`), `HighlightEventReason`, `HighlightEventDetails` (with `label`) | `menu/root/MenuRoot.tsx:924-955, 985-988` |
| `Menu.SubmenuRoot`, `Menu.CheckboxItem`, `Menu.RadioGroup` | `ChangeEventReason`, `ChangeEventDetails` (= `Menu.Root`'s) | `menu/submenu-root/MenuSubmenuRoot.tsx:70-77`, `menu/checkbox-item/MenuCheckboxItem.tsx:203-210`, `menu/radio-group/MenuRadioGroup.tsx:133-140` |
| `Menu.FilterProvider` | `ChangeEventReason`, `ChangeEventDetails` | `menu/filter-provider/MenuFilterProvider.tsx:52-59`, `filter-dropdown/root/FilterDropdownRootContext.ts:107-119` |
| `NavigationMenu.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `preventUnmountOnClose`) | `navigation-menu/root/NavigationMenuRoot.tsx:439-463` |
| `NumberField.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `direction?`), `CommitEventReason`, `CommitEventDetails` (generic) | `number-field/root/NumberFieldRoot.tsx:702-739`, `number-field/utils/types.ts:5-14` |
| `OTPField.Root` | `ChangeEventReason`, `ChangeEventDetails`, `InvalidEventReason`, `InvalidEventDetails` (generic), `CompleteEventReason`, `CompleteEventDetails` (generic) | `otp-field/root/OTPFieldRoot.tsx:610-637` |
| `Popover.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `preventUnmountOnClose`) | `popover/root/PopoverRoot.tsx:208-230` |
| `PreviewCard.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `preventUnmountOnClose`) | `preview-card/root/PreviewCardRoot.tsx:191-212` |
| `RadioGroup` | `ChangeEventReason`, `ChangeEventDetails` | `radio-group/RadioGroup.tsx:332-341` |
| `Select.Root` | `ChangeEventReason`, `ChangeEventDetails`, `OpenChangeEventDetails` (with `preventUnmountOnClose`) | `select/root/SelectRoot.tsx:746-775` |
| `Slider.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `activeThumbIndex`), `CommitEventReason`, `CommitEventDetails` (generic) | `slider/root/SliderRoot.tsx:578-612` |
| `Switch.Root` | `ChangeEventReason`, `ChangeEventDetails` | `switch/root/SwitchRoot.tsx:326-334` |
| `Tabs.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `activationDirection`) | `tabs/root/TabsRoot.tsx:461-474` |
| `Toggle` | `ChangeEventReason`, `ChangeEventDetails` | `toggle/Toggle.tsx:186-195` |
| `ToggleGroup` | `ChangeEventReason`, `ChangeEventDetails` | `toggle-group/ToggleGroup.tsx:194-203` |
| `Tooltip.Root` | `ChangeEventReason`, `ChangeEventDetails` (with `preventUnmountOnClose`) | `tooltip/root/TooltipRoot.tsx:225-247` |

Components with **no** event-details callbacks: Avatar (`onLoadingStatusChange(status)` takes a single argument, `avatar/image/AvatarImage.tsx:197`), Button, Field.Root, Fieldset, Menubar, Meter, Progress, ScrollArea, Separator, Toast (`onClose()` / `onRemove()` take no arguments, `toast/useToastManager.ts:80-84`), Toolbar.

### 3.7 Component-specific extensions of the details object

| Extra field | On | Type | Meaning | Source |
|---|---|---|---|---|
| `preventUnmountOnClose()` | open-change details of Dialog, AlertDialog, Drawer, Popover, Tooltip, PreviewCard, Menu (Root/SubmenuRoot/CheckboxItem/RadioGroup), Select, Combobox, Autocomplete; NavigationMenu `onValueChange` | `() => void` | "Prevents the popup from unmounting until the `unmount` action is called." On `Menu.CheckboxItem` / `Menu.RadioGroup` it is a no-op placeholder present only because the type is shared with `Menu.Root`. | `utils/popups/popupStoreUtils.ts:228-236`; `dialog/store/DialogStore.ts:76-78`; `menu/checkbox-item/MenuCheckboxItem.tsx:69-71`; `menu/radio-item/MenuRadioItem.tsx:73-75` |
| `activationDirection` | `Tabs.Root` `onValueChange` | `'left' \| 'right' \| 'up' \| 'down' \| 'none'` | Direction from the previous tab to the new one; `'none'` for automatic changes. | `tabs/root/TabsRoot.tsx:111-113, 126-134, 463-466`; `tabs/tab/TabsTab.tsx:126-128` |
| `activeThumbIndex` | `Slider.Root` `onValueChange` | `number` | "The index of the active thumb at the time of the change." | `slider/root/SliderRoot.tsx:236-238, 578-594`; `slider/control/SliderControl.tsx:300-302` |
| `direction` | `NumberField.Root` `onValueChange` | `-1 \| 1 \| undefined` | Step direction for step-based reasons; absent for direct text entry and for the "seed an empty field with 0" step. | `number-field/root/NumberFieldRoot.tsx:281-300`; `number-field/utils/types.ts:12-14` |
| `isItemPress` | `Combobox.Root` / `Autocomplete.Root` input-value details | `boolean \| undefined` | "When `reason` is `input-clear` in multiple mode, indicates whether an item press caused the clear. Automatic cleanup clears omit this property." | `combobox/root/AriaCombobox.tsx:823-829, 918-923, 2039-2045` |
| `index` | `Combobox.Root` / `Autocomplete.Root` `onItemHighlighted` | `number` | Index of the highlighted item (`-1` when the highlight is cleared). | `combobox/root/AriaCombobox.tsx:606-619, 2014-2017` |
| `label` | `Menu.Root` `onItemHighlighted` | `string \| undefined` | "The highlighted item's `label` prop, or its text content when the prop is not set." | `menu/root/MenuRoot.tsx:605-613, 947-955` |

### 3.8 Public props whose semantics are defined here

Component documents cite `EVD-API-13` for these props and only add their own prop pair names and reason lists.

| Prop (on the Root or the part named) | Signature | Semantics defined by this document |
|---|---|---|
| `onOpenChange` (Collapsible, Accordion.Item, Dialog, AlertDialog, Drawer, Popover, Tooltip, PreviewCard, Menu.Root/SubmenuRoot, ContextMenu, Select, Combobox, Autocomplete) | `(open: boolean, details) => void` | Change request for the open state; cancelable; popup variants carry `preventUnmountOnClose()`. Reasons per §4.3.7. |
| `onValueChange` (Accordion.Root, CheckboxGroup, RadioGroup, ToggleGroup, Tabs, Field.Control, Input, NumberField, OTPField, Slider, Select, Combobox (selected value), Autocomplete (input value), Menu.RadioGroup, Menu.FilterProvider, NavigationMenu) | `(value, details) => void` | Change request for the value; cancelable. NavigationMenu's variant carries `preventUnmountOnClose()`. |
| `onCheckedChange` (Checkbox.Root, Switch.Root, Menu.CheckboxItem) | `(checked: boolean, details) => void` | Change request; cancelable. |
| `onPressedChange` (Toggle) | `(pressed: boolean, details) => void` | Change request; cancelable; shares its details object with the parent ToggleGroup. |
| `onInputValueChange` (Combobox) | `(inputValue: string, details) => void` | Change request for the text input; cancelable; may carry `isItemPress`. |
| `onSnapPointChange` (Drawer) | `(snapPoint, details) => void` | Change request; cancelable; plain change-event details (no `preventUnmountOnClose`). |
| `onItemHighlighted` (Menu, Combobox, Autocomplete) | `(item, details) => void` | Notification; highlight-event details; not cancelable. |
| `onValueCommitted` (Slider, NumberField) | `(value, details) => void` | Notification; generic-event details; not cancelable; fires only for applied changes. |
| `onValueInvalid`, `onValueComplete` (OTPField) | `(value: string, details) => void` | Notifications; generic-event details. |
| `onFormSubmit` (Form) | `(values, details) => void` | Notification; generic-event details with `reason: 'none'`. |
| `onOpenChangeComplete` (all popup roots, Collapsible/Accordion excluded) | `(open: boolean) => void` | Not an event-details callback; ordering relative to `onOpenChange` in `EVD-EVT-20`. NavigationMenu calls it only with `false`. |
| the controlled/default prop pairs (`open`/`defaultOpen`, `value`/`defaultValue`, `checked`/`defaultChecked`, `pressed`/`defaultPressed`, `inputValue`/`defaultInputValue`, `snapPoint`/`defaultSnapPoint`, Menu.FilterProvider `value`/`defaultValue`) | — | Controlled/uncontrolled contract in §4.2 (`EVD-STA-01` … `EVD-STA-10`). |

## 4. Behavioral requirements

### 4.1 API — shape of the details object

- **EVD-API-01** — MUST. A change-event details object MUST expose exactly the members `reason`, `event`, `trigger`, `cancel()`, `allowPropagation()`, `isCanceled`, `isPropagationAllowed`, plus any component-specific fields listed in §3.7.
  Source: `internals/createBaseUIEventDetails.ts:56-85, 126-157`. Test: [L1] `collapsible/root/CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails".
- **EVD-API-02** — MUST. A generic-event details object MUST expose `reason` and `event` (plus component-specific fields) and MUST NOT expose `cancel`, `allowPropagation`, `isCanceled`, `isPropagationAllowed` or `trigger`.
  Source: `internals/createBaseUIEventDetails.ts:98-112, 159-174`. Test: [L1] `internals/createBaseUIEventDetails.spec.ts` (type-level: `reason` is the literal passed; an unknown literal is rejected).
- **EVD-API-03** — MUST. `reason` MUST be one of the 36 literals of §3.5, and for a given callback MUST be one of the literals that callback declares in §4.3.3.
  Source: `internals/reason-parts.ts:1-43`; per-component unions in §3.6. Test: [L1] `internals/createBaseUIEventDetails.spec.ts` "@ts-expect-error reason must exist in REASONS".
- **EVD-API-04** — MUST. When the change has no originating native event, `event` MUST be a freshly created generic event whose `type` is the string `base-ui` (never `undefined`).
  Source: `internals/createBaseUIEventDetails.ts:140, 170`. Test: [L1] `tabs/root/TabsRoot.test.tsx` "does not cancel automatic value changes" (asserts `event instanceof Event` and `event.type === 'base-ui'`).
- **EVD-API-05** — MUST. When the change has an originating native event, `event` MUST be the *native* platform event (not a framework wrapper); the emitter passes `event.nativeEvent`.
  Source: every emitter, e.g. `switch/root/SwitchRoot.tsx:184`, `collapsible/root/useCollapsibleRoot.ts:33`, `floating-ui-react/hooks/useDismiss.ts:207, 220`. Test: [L2] `collapsible/root/CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails" (`event instanceof MouseEvent`); `switch/root/SwitchRoot.test.tsx` (modifier keys readable on `event`, line 224).
- **EVD-API-06** — MUST. `isCanceled` and `isPropagationAllowed` MUST be live read-only accessors that are `false` on creation and become `true` only after `cancel()` / `allowPropagation()` respectively is called on the same object; a value read before the call MUST NOT be cached.
  Source: `internals/createBaseUIEventDetails.ts:135-136, 141-152`. Test: [L1] `collapsible/root/CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails" (`isCanceled === false`); `accordion/root/AccordionRoot.test.tsx` "onValueChange cancel() prevents opening while controlled" (reads `isCanceled` after `cancel()`).
- **EVD-API-07** — MUST. `trigger` MUST be the initiating element when the emitter knows it (trigger button for press/focus/hover/arrow-key opens, handle `open(triggerId)`, focus guard, swipe area, internal dismiss button) and `undefined` otherwise.
  Source: `floating-ui-react/hooks/useClick.ts:83`, `useFocus.ts:159-166`, `useHoverReferenceInteraction.ts:280, 482`, `useListNavigation.ts:817-824`, `utils/popups/popupHandle.ts:259-262`, `utils/popups/useTriggerFocusGuards.ts:46`, `drawer/swipe-area/DrawerSwipeArea.tsx:304, 308`, `combobox/utils/ComboboxInternalDismissButton.tsx:28`. Test: [L2] `popover/close/PopoverClose.test.tsx` "reports no trigger when the active trigger id has no mounted trigger"; `tabs/root/TabsRoot.test.tsx` "does not cancel automatic value changes" (`trigger === undefined`) (L4 for the focus, hover, arrow-key, focus-guard and swipe-area trigger clauses).
- **EVD-API-08** — MUST. The component-specific extensions of §3.7 MUST be present on the details object of the callbacks listed there, with the stated types.
  Source: §3.7. Test: [L2] `tabs/root/TabsRoot.test.tsx` "does not cancel automatic value changes" (`activationDirection === 'none'`); `slider/control/SliderControl.test.tsx` "moves the first of several thumbs stacked at the maximum" (`activeThumbIndex`); `combobox/root/ComboboxRoot.test.tsx` "emits onItemHighlighted for the selected item when the query is cleared" (`index`) (L4 for the Slider `activeThumbIndex` clause).
- **EVD-API-09** — MUST. The details object passed to a popup root's `onOpenChange` (and to `NavigationMenu.Root`'s `onValueChange`) MUST additionally expose `preventUnmountOnClose()`.
  Source: `utils/popups/popupStoreUtils.ts:228-236, 271`; `dialog/store/DialogStore.ts:76-78`; `menu/root/MenuRoot.tsx:362-364`; `select/root/SelectRoot.tsx:290-291`; `combobox/root/AriaCombobox.tsx:777-778`; `navigation-menu/root/NavigationMenuRoot.tsx:211-212`. Test: [L2] `popover/root/PopoverRoot.test.tsx` "preventUnmountOnClose()" › "unmounts on a normal close after a prevented close and initially open remount".
- **EVD-API-10** — MUST. Each component MUST expose the public type names listed in §3.6 (`ChangeEventReason`, `ChangeEventDetails`, and where listed `OpenChangeEventDetails`, `HighlightEventReason/Details`, `CommitEventReason/Details`, `InvalidEventReason/Details`, `CompleteEventReason/Details`, `SubmitEventReason/Details`, `SnapPointChangeEventReason/Details`), and the package root MUST export `BaseUIChangeEventDetails` and `BaseUIGenericEventDetails`.
  Source: `types/index.ts:3-6`; §3.6. Test: none (type surface; checked against `docs/src/app/(docs)/react/components/page.mdx`).
- **EVD-API-11** — MUST. Every consumer-supplied DOM event handler on a Base UI part MUST receive an event object exposing `preventBaseUIHandler()` and `baseUIHandlerPrevented`; this applies only to real DOM events, not to the change callbacks of this document (which carry `cancel()` instead).
  Source: `types/index.ts:23-26`; `internals/types.ts:6-30`; `merge-props/mergeProps.ts:210-263`. Test: [L1] `merge-props/mergeProps.test.ts` "sets baseUIHandlerPrevented to true after calling preventBaseUIHandler()"; `internals/useRenderElement.test.tsx` "makes single prop objects preventable".
- **EVD-API-12** — SHOULD. The `reason` → native-event-type table of §3.5 SHOULD be honored by emitters (e.g. an `escape-key` change carries a keyboard event, a `trigger-hover` change a mouse event); it is a typing promise in the source and not enforced at runtime.
  Source: `internals/createBaseUIEventDetails.ts:4-54`. Test: [L4] `menu/root/MenuRoot.test.tsx` "passes native mouse and pointer events for pointer highlights"; `combobox/root/ComboboxRoot.test.tsx` "clears the input with the "input-clear" reason and the originating keydown event on keyboard selection".

- **EVD-API-13** — MUST. The callback props listed in §3.8 MUST have the stated signatures and MUST follow the cancelability and ordering rules of §4.3; a component MUST NOT add positional arguments beyond `(value, details)`.
  Source: prop declarations cited in §3.6; `docs/src/app/(docs)/react/handbook/customization/page.mdx:6-37`. Docs: `CHANGELOG.md` v1.0.0-beta.3 "Base UI event details"; `CHANGELOG.md` v1.0.0-beta.5 "Granular change reasons" (`onOpenChange(open, event, reason)` became `onOpenChange(open, eventDetails)`). Test: [L2] `collapsible/root/CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- EVD-STA-07 — the post-commit, pre-paint value-changed side effects run after paint plus a round trip; observable only as latency of dependent attribute updates.
- EVD-EVT-11 — the consumer's `cancel()` decision on an Escape close arrives a round trip after the keydown dispatch ends; JS applies the ADR 04 §3 fallback (prevent and stop whenever the owning popup is the innermost open one); WebAssembly meets the spec through the synchronous `decide()` path.
- EVD-EVT-18 — the `trigger-hover` open commit lands a round trip later; same-task animation queries do not see it.
- EVD-EVT-26 — against composite/button key handling, a C# consumer's `preventBaseUIHandler()` arrives after dispatch, so on Server the key is handled regardless; WebAssembly honours synchronous consumer handlers through `decide()` (ADR 04 §3).
- EVD-DIS-01 — the consumer's `allowPropagation()` decision arrives a round trip after the Escape dispatch ends; JS stops propagation whenever the owning popup is the innermost open one (ADR 04 §3 fallback); WebAssembly meets the spec through `decide()`.

### 4.2 STA — controlled / uncontrolled contract

Every stateful prop pair (`open`/`defaultOpen`, `value`/`defaultValue`, `checked`/`defaultChecked`, `pressed`/`defaultPressed`, `inputValue`/`defaultInputValue`, `snapPoint`/`defaultSnapPoint`, `triggerId`/`defaultTriggerId`, Menu filter `value`/`defaultValue`, …) follows the contract below. Component documents link here instead of restating it; they only name their prop pairs and any deviation.

- **EVD-STA-01** — MUST. A value MUST be treated as *controlled* if, on the component's first render, the controlled prop is not `undefined`; otherwise it is *uncontrolled*. The mode MUST NOT change for the lifetime of the component even if the prop later becomes defined or undefined.
  Source: `packages/utils/src/useControlled.ts:41, 46`. Test: [L2] `packages/utils/src/useControlled.test.tsx` "works correctly when is not controlled", "works correctly when is controlled", "warns when switching from uncontrolled to controlled".
- **EVD-STA-02** — MUST. An uncontrolled value MUST be initialized from the default prop at first render; later changes to the default prop MUST be ignored (a dev-mode warning is emitted, `EVD-EDGE-02`).
  Source: `packages/utils/src/useControlled.ts:43, 66-80`. Test: [L2] `packages/utils/src/useControlled.test.tsx` "warns when changed after initial rendering", "should warn only when defaultValue changes".
- **EVD-STA-03** — MUST. When the default prop is a function it MUST be stored as the value itself, not invoked to produce the value.
  Source: `packages/utils/src/useControlled.ts:42-43`. Test: [L2] `packages/utils/src/useControlled.test.tsx` "stores a function default value instead of calling it".
- **EVD-STA-04** — MUST. A controlled component MUST render the controlled prop's value and MUST NOT store a requested value itself: its internal setter is a no-op in controlled mode. The only way the observable state changes is the consumer updating the prop (typically from inside the change callback).
  Source: `packages/utils/src/useControlled.ts:46, 83-90`; docs `handbook/customization/page.mdx` §"Controlling components with state". Test: [L2] `accordion/root/AccordionRoot.test.tsx` "onValueChange cancel() prevents opening while controlled"; `collapsible/root/CollapsibleRoot.test.tsx` "controlled trigger presses request open and close state changes".
- **EVD-STA-05** — MUST. If a controlled value later becomes `undefined`, the component MUST fall back to the *initial* default prop value (and warn, `EVD-EDGE-01`) rather than switching to uncontrolled mode.
  Source: `packages/utils/src/useControlled.ts:44-46`. Test: [L2] `packages/utils/src/useControlled.test.tsx` "warns and falls back to the default when switching from controlled to uncontrolled".
- **EVD-STA-06** — MUST. A change callback MUST be invoked only by the component's own interaction and automatic paths (§4.3.3). Changing a controlled prop from the consumer MUST NOT invoke the change callback.
  Source: no path reads the controlled prop to emit a callback: `packages/utils/src/useControlled.ts:28-93` only mirrors the prop; emitters are the `set*` functions cited in §4.3.3. Test: [L2] `menu/filter-root/MenuFilterRoot.test.tsx` "resets the input value once when the popup closes" (a controlled close yields exactly one `popup-close` callback, none for the prop sync); `tabs/root/TabsRoot.test.tsx` "does not emit a second change when the fallback resolves to the current value".
- **EVD-STA-07** — MUST. Internal "value changed" side effects (marking a field dirty, clearing server errors, running `onChange` validation, resetting per-open flags) MUST run post-commit, pre-paint whenever a *committed* value differs from the previously committed value by strict identity (`!==`), regardless of whether the change came from the consumer updating a controlled prop or from an uncontrolled commit. `-0` and `0` MUST be treated as equal. Server: see ADR 01 relaxation table.
  Source: `internals/useValueChanged.ts:6-17`; users: `switch/root/SwitchRoot.tsx:100`, `checkbox/root/CheckboxRoot.tsx:184`, `radio-group/RadioGroup.tsx:171`, `checkbox-group/CheckboxGroup.tsx:129`, `select/root/SelectRoot.tsx:281-286`, `slider/root/SliderRoot.tsx:178`, `field/control/FieldControl.tsx:96`, `number-field/input/NumberFieldInput.tsx:103`, `otp-field/root/OTPFieldRoot.tsx:201`, `combobox/root/AriaCombobox.tsx:1326-1331`, `form/Form.tsx:73`. Test: [L4] `internals/useValueChanged.test.tsx` "retains -0 as the previous value without treating it as a change from 0 (strict: %s)"; `select/root/SelectRoot.test.tsx` "does not update field state when autofill is canceled".
- **EVD-STA-08** — MUST. For a cancelable change, the sequence MUST be: compute the requested value → invoke the change callback with the request → if not canceled, commit (uncontrolled) and run the follow-up side effects → render. The callback therefore always observes the *previous* committed state.
  Source: `switch/root/SwitchRoot.tsx:183-192`; `select/root/SelectRoot.tsx:330-340`; `utils/popups/popupStoreUtils.ts:273-312`. Test: [L2] `utils/popups/popupStoreUtils.test.tsx` "runs the full sequence in order when the change is not canceled" (order `onOpenChange` → `onBeforeDispatch` → `dispatchOpenChange` → `update`).
- **EVD-STA-09** — MUST. Whether a change callback fires when the requested value equals the current value is component-specific and MUST match the table below.
  Source: per row of the table below. Test: [L2] per row of the table below (`none` where no test covers the row).

  | Callback | Fires when requested value equals current? | Source | Test |
  |---|---|---|---|
  | Switch/Checkbox `onCheckedChange`, Toggle `onPressedChange`, Collapsible/Accordion open change | n/a — the request is always the toggled value | `switch/root/SwitchRoot.tsx:183`, `toggle/Toggle.tsx:83`, `collapsible/root/useCollapsibleRoot.ts:32` | — |
  | RadioGroup `onValueChange` | No: a native `change` only fires when a different radio becomes checked | `radio/root/RadioRoot.tsx:196-215` | none |
  | Tabs `onValueChange` (user) | No: activation is guarded on `!active` | `tabs/tab/TabsTab.tsx:122-138` | `tabs/root/TabsRoot.test.tsx` "does not emit a second change when the fallback resolves to the current value" (automatic path) |
  | Tabs `onValueChange` (automatic) | No: "Already at the fallback value; no commit or notification" | `tabs/root/TabsRoot.tsx:299-304` | same |
  | NumberField `onValueChange` | Only for direct-entry reasons (`input-*`, `none`) when the *unvalidated* text differs from the current value or input sync is suspended; step reasons never re-fire | `number-field/root/NumberFieldRoot.tsx:244-251` | `number-field/input/NumberFieldInput.test.tsx` "reports an unchanged value once when a paste event carries no clipboard data" |
  | OTPField `onValueChange` | No (`normalizedValue === current` returns early; a complete paste of the same value still fires `onValueComplete`) | `otp-field/root/OTPFieldRoot.tsx:240-246` | `otp-field/root/OTPFieldRoot.test.tsx` "fires `input-paste` when a complete paste replaces a complete OTP" |
  | Slider `onValueChange` | No (`NaN` or equal values return `false` before notifying) | `slider/root/SliderRoot.tsx:193-197` | none |
  | Select `onValueChange` (single, item press) | Yes: no equality guard on item activation | `select/item/SelectItem.tsx:147-149`, `select/root/SelectRoot.tsx:330-340` | none (see §10 Q6) |
  | Combobox `onInputValueChange`, `onValueChange` | Yes: no equality guard in `setInputValue` / `setSelectedValue` (callers often guard) | `combobox/root/AriaCombobox.tsx:655-661, 854-864, 1284-1289` | `combobox/root/ComboboxRoot.test.tsx` "should reset input value to selected value when popup closes without selection" |
  | Combobox `onOpenChange` | No (`open === nextOpen` returns early) | `combobox/root/AriaCombobox.tsx:761-763` | `combobox/root/ComboboxRoot.test.tsx` "fires onOpenChange once with reason item-press on mouse click" |
  | Menu `onOpenChange` | No for a close while already closed; no for an identical (state, trigger, reason) request; yes otherwise (e.g. hover re-open on a different trigger while open) | `menu/root/MenuRoot.tsx:350-360` | none |
  | NavigationMenu `onValueChange` | No (callback skipped when `nextValue === value`; internal close bookkeeping still runs) | `navigation-menu/root/NavigationMenuRoot.tsx:214-216, 222-230` | none |
  | Dialog/Popover/Tooltip/PreviewCard/Select/Drawer `onOpenChange` | Yes at the store level — no equality guard; the interaction hooks (`useClick`, `useDismiss`, …) only request real transitions, and the imperative `close` of Select / `Dialog.Close` are guarded on `open` | `dialog/store/DialogStore.ts:72-95`, `popover/store/PopoverStore.ts:110-183`, `utils/popups/popupStoreUtils.ts:246-313`, `select/root/SelectRoot.tsx:321-325`, `dialog/close/DialogClose.tsx:39-43` | none |
  | Field.Control `onValueChange` | Yes: every native `change` is forwarded | `field/control/FieldControl.tsx:123-126` | none |

- **EVD-STA-10** — MUST. A popup that is *controlled* and whose consumer declines a close request MUST remain open with its previous trigger association and `openedWithoutTrigger` flag intact; the flag is cleared only once the popup is effectively closed.
  Source: `utils/popups/popupStoreUtils.ts:219-225, 444-456, 533-543`. Test: [L2] `tooltip/root/TooltipRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled".

### 4.3 EVT — callbacks, cancelation, propagation, ordering

#### 4.3.1 Signature and timing

- **EVD-EVT-01** — MUST. Every change callback MUST be called as `callback(newValue, details)` where `details` is a change-event details object (cancelable callbacks) or a generic-event details object (`onValueCommitted`, `onValueInvalid`, `onValueComplete`, `onFormSubmit`, `onItemHighlighted`).
  Source: prop declarations listed in §3.6, e.g. `switch/root/SwitchRoot.tsx:302-303`, `slider/root/SliderRoot.tsx:551-556, 557-565`, `form/Form.tsx:213-214`. Test: [L2] `collapsible/root/CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails".
- **EVD-EVT-02** — MUST. A change callback MUST be invoked synchronously inside the handling of the originating input (same task as the native event), *before* the component's own state is updated and before any DOM reflecting the new state is produced.
  Source: `switch/root/SwitchRoot.tsx:186-192`; `utils/popups/popupStoreUtils.ts:273-312`; `menu/root/MenuRoot.tsx:372-378`. Test: [L2] `utils/popups/popupStoreUtils.test.tsx` "runs the full sequence in order when the change is not canceled".
- **EVD-EVT-03** — MUST. A change callback MUST be invoked exactly once per change request, even when several internal layers participate (item → root, child → group, trigger → root).
  Source: `combobox/root/AriaCombobox.tsx:759-763, 884-941`; `checkbox/root/CheckboxRoot.tsx:222-245`. Test: [L2] `combobox/root/ComboboxRoot.test.tsx` "fires onOpenChange once with reason item-press on mouse click", "fires onOpenChange once with reason item-press on Enter selection"; `menu/filter-root/MenuFilterRoot.test.tsx` "resets the input value once when the popup closes".
- **EVD-EVT-04** — MUST. Chained callbacks that share one change (child part then parent part) MUST receive the *same* details object, in child-then-parent order: `Accordion.Item.onOpenChange` → `Accordion.Root.onValueChange`; `Checkbox.Root.onCheckedChange` → `CheckboxGroup.onValueChange`; `Toggle.onPressedChange` → `ToggleGroup.onValueChange`; Combobox `onValueChange` (item press) → `onOpenChange`; `Drawer.Root.onOpenChange` → `onSnapPointChange` (re-wrapped, new object, same reason/event/trigger).
  Source: `accordion/item/AccordionItem.tsx:56-66`, `accordion/root/AccordionRoot.tsx:66-88`; `checkbox/root/CheckboxRoot.tsx:225-245`; `toggle/Toggle.tsx:86-100`; `combobox/root/AriaCombobox.tsx:906-910, 934-941`; `drawer/root/DrawerRoot.tsx:150-169`. Test: [L2] `toggle/Toggle.test.tsx` "canceling in a grouped Toggle prevents the group value from changing"; `accordion/root/AccordionRoot.test.tsx` "onOpenChange cancel() prevents opening while uncontrolled" (root callback count 0).

#### 4.3.2 Cancelation (`cancel()` / `isCanceled`)

- **EVD-EVT-05** — MUST. For an uncontrolled value, calling `cancel()` inside the change callback MUST prevent the commit: the component's state, ARIA attributes and DOM remain as before the request.
  Source: `switch/root/SwitchRoot.tsx:188-192`; `checkbox/root/CheckboxRoot.tsx:227-237`; `toggle/Toggle.tsx:90-102`; `radio-group/RadioGroup.tsx:82-92`; `collapsible/root/useCollapsibleRoot.ts:35-41`; `tabs/root/TabsRoot.tsx:115-123`; `select/root/SelectRoot.tsx:292-301, 332-338`; `utils/popups/popupStoreUtils.ts:273-277`; docs `handbook/customization/page.mdx` §"Canceling a Base UI event". Test: [L2] `collapsible/root/CollapsibleRoot.test.tsx` "eventDetails.cancel() prevents opening while uncontrolled", "eventDetails.cancel() prevents closing while uncontrolled"; `switch/root/SwitchRoot.test.tsx` "does not change state when canceled via a root click"; `checkbox/root/CheckboxRoot.test.tsx` "does not update its state when onCheckedChange cancels the event"; `toggle/Toggle.test.tsx` "does not change the pressed state when the event is canceled"; `radio-group/RadioGroup.test.tsx` "does not change state when canceled via arrow key navigation"; `tabs/root/TabsRoot.test.tsx` "does not move an uncontrolled selection when a user-initiated change is canceled"; `menu/checkbox-item/MenuCheckboxItem.test.tsx` "does not toggle when `onCheckedChange` cancels the event"; `menu/radio-item/MenuRadioItem.test.tsx` "does not select when `onValueChange` cancels the event".
- **EVD-EVT-06** — MUST. For a controlled value, `cancel()` has no effect on the component (it never commits); the consumer MUST itself read `isCanceled` and skip its own state update to honor a cancelation made earlier in the same callback or by a wrapped callback.
  Source: `packages/utils/src/useControlled.ts:83-90`; test helpers mirror the contract: `select/root/SelectRoot.test.tsx:55-60`, `autocomplete/root/AutocompleteRoot.test.tsx:41`. Test: [L2] `accordion/root/AccordionRoot.test.tsx` "onValueChange cancel() prevents opening while controlled".
- **EVD-EVT-07** — MUST. For a popup open change, a canceled request MUST stop the whole sequence: no internal open-change notification, no trigger re-association, no `preventUnmountOnClose` capture, no instant-phase update, no commit. The order when *not* canceled is `onOpenChange` → store-specific pre-dispatch work → internal notification → commit (see `EVD-ALG-04`).
  Source: `utils/popups/popupStoreUtils.ts:265-312`; `dialog/store/DialogStore.ts:86-94`; `popover/store/PopoverStore.ts:139-182`; `menu/root/MenuRoot.tsx:372-378`. Test: [L2] `utils/popups/popupStoreUtils.test.tsx` "notifies onOpenChange but short-circuits before dispatch when canceled"; `dialog/root/DialogRoot.test.tsx` "does not emit internal openchange when an Escape close is canceled".
- **EVD-EVT-08** — MUST. A canceled change MUST suppress the follow-up side effects that depend on the change having been applied, specifically: field `dirty`/`filled`/`touched` updates and validation (Switch, Radio, Select autofill — but see `EVD-EDGE-06`); for an uncontrolled `Field.Control` only `clearErrors` and validation are suppressed — `dirty` and `filled` are still written from the raw input value before the cancelation check (a controlled `Field.Control` writes nothing from the event and syncs from the `value` prop instead); group value propagation (Checkbox → CheckboxGroup, Toggle → ToggleGroup); `onValueCommitted` (Slider; NumberField keyboard, wheel, keyboard-click and blur paths — NumberField stepper press-and-hold and scrub releases commit the current value regardless, `EVD-EVT-23`); OTP completion; the item-press close of Combobox (`onOpenChange` not requested after a canceled selection; Select is an exception — its single-mode item press issues the close with a separate details object after the value request, so canceling `onValueChange` does not keep the popup open); the Select/Combobox `setTouched`/`validation.commit` on focus-out close; the CheckboxGroup parent status update; the Drawer swipe-dismiss animation (reverted) and snap-point close; the Combobox multiple-mode input clear after a canceled selection.
  Source: `switch/root/SwitchRoot.tsx:188-192`; `radio/root/RadioRoot.tsx:210-214`; `field/control/FieldControl.tsx:123-143`; `select/item/SelectItem.tsx:146-149` (Select exception); `checkbox/root/CheckboxRoot.tsx:227-245`; `toggle/Toggle.tsx:90-102`; `number-field/root/NumberFieldRoot.tsx:254-257`, `number-field/input/NumberFieldInput.tsx:160-162, 207-210`, `number-field/root/useNumberFieldStepperButton.ts:93-97`; `slider/root/SliderRoot.tsx:216-218, 242-244`, `slider/control/SliderControl.tsx:305-317`; `otp-field/root/OTPFieldRoot.tsx:250-252`; `combobox/root/AriaCombobox.tsx:908-910, 936-938`; `select/root/SelectRoot.tsx:294-313`; `checkbox-group/useCheckboxGroupParent.ts:93-95, 114-117`; `drawer/viewport/DrawerViewport.tsx:563-567, 654-664`. Test: [L2] `switch/root/SwitchRoot.test.tsx` "does not change state when canceled via a root click" (no `data-dirty`/`data-filled`); `radio-group/RadioGroup.test.tsx` "does not change state when canceled via a root click" (no `data-touched`); `field/control/FieldControl.test.tsx` "does not validate when the change is canceled"; `number-field/root/NumberFieldRoot.test.tsx` "does not commit a canceled keyboard change", "does not commit a canceled clear on blur"; `slider/root/SliderRoot.test.tsx` "does not commit a canceled change"; `checkbox-group/CheckboxGroup.test.tsx` "does not update the group when onValueChange cancels the event"; `drawer/root/DrawerRoot.test.tsx` "honors canceled snap point changes and falls back from invalid uncontrolled values".
- **EVD-EVT-09** — MUST. Cancelation of a shared details object by an earlier callback in a chain (`EVD-EVT-04`) MUST prevent the later callback from being invoked at all.
  Source: `checkbox/root/CheckboxRoot.tsx:225-231`; `toggle/Toggle.tsx:88-96`; `accordion/item/AccordionItem.tsx:58-64`; `combobox/root/AriaCombobox.tsx:906-910, 934-941`. Test: [L2] `toggle/Toggle.test.tsx` "canceling in a grouped Toggle prevents the group value from changing" (`onValueChange` not called); `accordion/root/AccordionRoot.test.tsx` "onOpenChange cancel() prevents opening while uncontrolled".
- **EVD-EVT-10** — MUST. Automatic changes MUST NOT be cancelable: Tabs' `initial` / `disabled` / `missing` notifications are issued post-commit, pre-paint, *after* the fallback value (and the `'none'` activation direction) has been scheduled for commit but before the synchronous re-render that applies it — so a callback that reads the DOM still sees the previous selection, and the fallback is applied before paint whatever the callback does (`TAB-EVT-04`) — and a `close-watcher` request whose native event is not cancelable has its `cancel()` replaced by a no-op so `isCanceled` stays `false`.
  Source: `tabs/root/TabsRoot.tsx:245-261` (value scheduled at 249, callback at 256), `294-321` (JSDoc at `454-455`); `drawer/root/DrawerRoot.tsx:470-474`. Test: [L2] `tabs/root/TabsRoot.test.tsx` "does not cancel automatic value changes"; `drawer/root/DrawerRoot.test.tsx` "closes on a close request that cannot be prevented even if onOpenChange cancels it" (L4 for the `close-watcher` clause).
- **EVD-EVT-11** — MUST. When an Escape-key close is canceled, the keydown MUST NOT have `preventDefault()` called on it; when it is not canceled, `preventDefault()` MUST be called. (Propagation is governed by `EVD-DIS-01`.) Server: see ADR 01 relaxation table.
  Source: `floating-ui-react/hooks/useDismiss.ts:220-226`. Test: [L4] `floating-ui-react/hooks/useDismiss.test.tsx` "calls preventDefault on escape key dismiss", "does not call preventDefault on escape key if close is canceled".
- **EVD-EVT-12** — MUST. When a `close-watcher` close is canceled, the component MUST call `preventDefault()` on the watcher's `cancel` event so that the same watcher stays active for the next close request; a non-canceled request lets the watcher close.
  Source: `drawer/root/DrawerRoot.tsx:476-482`. Test: [L4] `drawer/root/DrawerRoot.test.tsx` "keeps the same watcher active when a cancelable close request is canceled", "steps through content on back presses until one cannot be prevented (controlled: %s)".
- **EVD-EVT-13** — MUST. The close requested when the active trigger unmounts (`reason: 'none'`, Tooltip and PreviewCard only) MUST be issued one microtask after the unmount is observed post-commit, pre-paint (so a same-tick replacement trigger with the same id can register first); when it is canceled, the popup MUST keep its previous trigger ownership rather than claiming another registered trigger.
  Source: `utils/popups/popupStoreUtils.ts:527-544`; enabled at `tooltip/root/TooltipRoot.tsx:81`, `preview-card/root/PreviewCardRoot.tsx:59`. Test: [L2] `tooltip/root/TooltipRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled"; `preview-card/root/PreviewCardRoot.detached-triggers.test.tsx` same title.

#### 4.3.3 Internal notification, trigger bookkeeping and reason-driven flags

- **EVD-EVT-14** — MUST. After a non-canceled popup open change (and before the commit), the popup MUST emit an internal open-change notification `{ open, reason, nativeEvent, nested, triggerElement }` to its interaction machinery; this notification MUST NOT be emitted for a canceled change. Consumers of the notification (focus-open blocking after `trigger-press`/`escape-key`, hover-close bookkeeping, dismiss session boundaries, return-focus decisions) are specified in `floating-and-positioning.md`.
  Source: `floating-ui-react/components/FloatingRootStore.ts:106-118, 126-135`; `utils/popups/popupStoreUtils.ts:281`; `dialog/store/DialogStore.ts:92`; `menu/root/MenuRoot.tsx:378`; `popover/store/PopoverStore.ts:145`; consumers `floating-ui-react/hooks/useFocus.ts:100-108`, `useHoverReferenceInteraction.ts:170-181`, `useDismiss.ts:278-291`, `components/FloatingFocusManager.tsx:796-822`. Test: [L2] `dialog/root/DialogRoot.test.tsx` "emits a single internal openchange when closing on Escape", "does not emit internal openchange when an Escape close is canceled" (L4 for the Escape-close citations).
- **EVD-EVT-15** — MUST. On a non-canceled *open*, the store MUST record `details.trigger` as the active trigger element and `details.trigger.id` as the active trigger id (null when the trigger has no `id` or is absent), and MUST record whether the open carried no trigger at all. On a *close*, the previous active trigger MUST be retained (so exit animations and focus return still target it) unless the close carries a trigger.
  Source: `utils/popups/popupStoreUtils.ts:189-226`. Test: [L2] `popover/close/PopoverClose.test.tsx` "keeps the trigger when closing with a tooltip trigger close button"; `dialog/root/DialogRoot.test.tsx` "reports no trigger when closing with an initial trigger id that is not mounted".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through detachedTriggersConformanceTests on Popover, "opens with any trigger on $interaction and marks only that trigger as active" (L4, both modes), and `PopoverRootTests` "port: a trigger that becomes active again writes its payload again, after a move or a reopen" (L2); the cited `PopoverClose.test.tsx` title needs a Tooltip trigger and is written at PR 137's re-merge.
- **EVD-EVT-16** — MUST. Before invoking `onOpenChange` for a *close* whose details carry no `trigger`, Dialog and Menu MUST back-fill `details.trigger` with the current active trigger element (Dialog only when an active trigger id exists); Popover MUST do so only for `close-press`, resolving the registered element by id first and the stored element second.
  Source: `dialog/store/DialogStore.ts:80-84`; `menu/root/MenuRoot.tsx:366-370`; `popover/store/PopoverStore.ts:125-137`. Test: [L2] `popover/close/PopoverClose.test.tsx` "falls back to the active trigger element when the active trigger id is unregistered", "reports no trigger when the active trigger id has no mounted trigger"; docs `components/popover/page.mdx:159`, `components/dialog/page.mdx:258`.
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through `PopoverPartsTests` "falls back to the active trigger element when the active trigger id is unregistered" and "reports no trigger when the active trigger id has no mounted trigger" (L2), `PopoverStoreTests` "port: a close press without a trigger reports the registered active trigger" (L1), and `popover.spec.ts` "renders focus guards around the active detached trigger and reports it on close" (L4, both modes).
- **EVD-EVT-17** — MUST. The reason of a non-canceled open change MUST drive the popup's instant-phase flag as follows (shared stores): open with `trigger-focus` → `'focus'`; close with `trigger-press` or `escape-key` → `'dismiss'`; any `trigger-hover` change → cleared; other reasons leave it unchanged. Popover (own sequence, written after the commit): keyboard-generated `trigger-press` (`event.detail === 0`, open or close) → `'click'`, close with `escape-key` or no reason → `'dismiss'`, `focus-out` → `'focus'`, every other reason (including `trigger-hover` and pointer `trigger-press`) → cleared (unlike the shared stores, Popover never leaves it unchanged). Menu: menubar-parent with `trigger-focus`/`focus-out`/`trigger-hover`/`list-navigation`/`sibling-open` → `'group'`; keyboard click → `'click'`; close with `escape-key` or no reason → `'dismiss'`; else cleared. The rendering consequences are in `transitions-and-mounting.md`.
  Source: `utils/popups/popupStoreUtils.ts:265-269, 296-304`; `popover/store/PopoverStore.ts:114-119, 174-182`; `menu/root/MenuRoot.tsx:404, 422-437`; `menu/utils/isKeyboardOpen.ts:8-21`. Test: [L1] `utils/popups/popupStoreUtils.test.tsx` "maps the change reason to instantType".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): Popover proves the row through `PopoverStoreTests` "port: the instant type follows the reason and the press's detail" and "port: an applied open change writes its reason and instant type in one write, and a canceled one writes nothing" (L1).
- **EVD-EVT-18** — MUST. A `trigger-hover` open change MUST be committed synchronously (flushed before the handler returns) so that animation queries made in the same task observe the new state; other reasons MAY commit asynchronously. Server: see ADR 01 relaxation table.
  Source: `utils/popups/popupStoreUtils.ts:306-312`; `popover/store/PopoverStore.ts:161-172`; `navigation-menu/trigger/NavigationMenuTrigger.tsx:469-473`. Test: none.
- **EVD-EVT-19** — MUST. Stores MUST record the last open-change reason (`openChangeReason` / `lastOpenChangeReason`) because later behavior branches on it: hover-opened popovers/menus get `pointer-events: none` backdrops and are non-modal, a `trigger-hover`-opened menu closes on `cancel-open` mouseup outside, a `list-navigation`-opened submenu focuses its first item, and Tooltip's delay-group instant phase keys on `none`.
  Source: `tooltip/store/TooltipStore.ts:88-90`; `popover/store/PopoverStore.ts:157`; `menu/root/MenuRoot.tsx:419`; consumers `popover/backdrop/PopoverBackdrop.tsx:42`, `popover/popup/PopoverPopup.tsx:112`, `popover/positioner/PopoverPositioner.tsx:98`, `menu/backdrop/MenuBackdrop.tsx:47`, `menu/positioner/MenuPositioner.tsx:246, 267`, `menu/trigger/MenuTrigger.tsx:162, 378`, `menu/submenu-trigger/MenuSubmenuTrigger.tsx:217`, `tooltip/root/TooltipRoot.tsx:104`, `menu/filter-root/MenuFilterPopup.tsx:28-35`. Test: [L2] `menu/trigger/MenuTrigger.test.tsx` "should keep the menu open when re-hovered and clicked within the patient threshold" (L4 for the `trigger-hover` clauses).

#### 4.3.4 Open-change completion and unmount prevention

- **EVD-EVT-20** — MUST. `onOpenChangeComplete(true)` MUST fire on animations finished for the popup element after `onOpenChange(true, …)` and after the open state is rendered; `onOpenChangeComplete(false)` MUST fire when the closing phase ends (on animations finished for the popup element, or when the `unmount` action is called), after `onOpenChange(false, …)` and the close commit. Neither MUST fire for a canceled request. Exception: `NavigationMenu.Root` wires only the close completion and MUST NOT call `onOpenChangeComplete(true)`. The animation detection itself is specified in `transitions-and-mounting.md`.
  Source: `internals/useOpenChangeComplete.tsx:9-28`; `popover/popup/PopoverPopup.tsx:56-64` (and the equivalent in `dialog/popup/DialogPopup.tsx:52`, `menu/popup/MenuPopup.tsx:101-105`, `select/popup/SelectPopup.tsx:183`, `tooltip/popup/TooltipPopup.tsx:43`, `preview-card/popup/PreviewCardPopup.tsx:42`, `combobox/popup/ComboboxPopup.tsx:69`, `drawer/popup/DrawerPopup.tsx:301`); `utils/popups/popupStoreUtils.ts:586-595`; `select/root/SelectRoot.tsx:206-214`; `combobox/root/AriaCombobox.tsx:952-953`; `navigation-menu/root/NavigationMenuRoot.tsx:133-151`; `internals/useUnmountAfterClose.ts:118-127`. Test: [L2] `popover/root/PopoverRoot.test.tsx` "preventUnmountOnClose()" › "does not leak from a canceled close into a synchronous second close"; (per-component complete-ordering tests belong to the component docs) (L4 for the animations-finished clauses).
- **EVD-EVT-21** — MUST. `preventUnmountOnClose()` MUST take effect only when called during `onOpenChange` for a *close* that is *not* canceled; it MUST keep the popup mounted and in its closing phase until the consumer calls the root's `unmount` action, at which point the close completes and `onOpenChangeComplete(false)` fires. A subsequent open MUST clear the request. For Dialog the request is written to the store immediately when called (`EVD-EDGE-09` notes the consequence of a canceled close).
  Source: `utils/popups/popupStoreUtils.ts:228-236, 271, 283-289, 195-201`; `internals/useUnmountAfterClose.ts:66-76, 102-127`; `select/root/SelectRoot.tsx:291, 298-300`; `combobox/root/AriaCombobox.tsx:778, 833-835`; `navigation-menu/root/NavigationMenuRoot.tsx:212, 222-227`; `dialog/store/DialogStore.ts:76-78`; docs `handbook/animation/page.mdx:190-191`. Test: [L2] `popover/root/PopoverRoot.test.tsx` "preventUnmountOnClose()" › "does not leak from a canceled close into a synchronous second close", "unmounts on a normal close after a prevented close and initially open remount"; `tooltip/root/TooltipRoot.test.tsx` "preventUnmountOnClose()"; `select/root/SelectRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out".
- **EVD-EVT-22** — MUST. `NavigationMenu.Root` MUST record a `preventUnmountOnClose()` request only when the value actually transitions to `null` (a redundant close that skipped `onValueChange` MUST NOT discard a previously recorded request).
  Source: `navigation-menu/root/NavigationMenuRoot.tsx:222-227`. Test: [L2] `navigation-menu/root/NavigationMenuRoot.test.tsx` "closes through the `close` action so `onValueChange` can opt out".

#### 4.3.5 Generic (non-cancelable) callbacks

- **EVD-EVT-23** — MUST. `onValueCommitted` (NumberField, Slider) MUST carry the reason of the interaction it commits. Whether it requires the value to have changed differs per component and per path:
  - **Slider** fires only after a change was *applied* (not canceled; `NaN` and equal values are rejected before notifying, `EVD-STA-09`): synchronously with `onValueChange` for `keyboard` and `input-change`; on pointer/touch release for `drag`/`track-press` only if at least one change during the gesture was applied, with the last applied value and the reason of the last applied change.
  - **NumberField** fires synchronously for `keyboard` and `wheel` only when the step (or Home/End boundary) change was applied; for a keyboard-generated (`detail === 0`) stepper click (`increment-press`/`decrement-press`) only when the committed value differs from the value before the click; on **every** pointer release of a stepper press-and-hold and on **every** release of an active `scrub`, with the last applied value or else the current value, even when nothing changed (e.g. a hold that hit a bound); for `input-blur` on blur whenever the user typed since the last sync, the parsed value differs from the current value, or a programmatic change is pending — so blurring after manual typing commits even when the value is unchanged — unless that blur's own `onValueChange` was canceled; for `input-clear` on blur of an empty input unless the clear was canceled, and only when the user typed, a programmatic change is pending, or the previous value was not `null`.
  Source: `slider/root/SliderRoot.tsx:193-197, 214-222, 242-244`, `slider/control/SliderControl.tsx:298-306, 369-375`; `number-field/input/NumberFieldInput.tsx:160-170, 197-223, 381-409`, `number-field/root/NumberFieldRoot.tsx:397-409`, `number-field/root/useNumberFieldStepperButton.ts:111-116, 141-151`, `internals/usePressAndHold.ts:138-146, 277-285`, `number-field/scrub-area/NumberFieldScrubArea.tsx:168-171`. Test: [L2] `slider/root/SliderRoot.test.tsx` "does not commit a canceled change", "single value"; `number-field/root/NumberFieldRoot.test.tsx` "does not commit a canceled keyboard change", "includes the correct reason for increment and decrement button presses", "does not commit when a wheel step is a no-op at the boundary"; `number-field/root/useNumberFieldStepperButton.test.tsx` "commits on release after a hold reaches the boundary"; `number-field/scrub-area/NumberFieldScrubArea.test.tsx` "calls onValueChange while scrubbing and onValueCommitted on pointerup" (L4 for the Slider pointer-release and NumberField wheel, press-and-hold and scrub clauses).
- **EVD-EVT-24** — MUST. `onItemHighlighted` (Menu, Combobox, Autocomplete) MUST carry `reason: 'keyboard'` for keyboard-driven highlight moves (arrow keys, typeahead), `'pointer'` for hover-driven moves (with the originating mouse or pointer event), and `'none'` for every other cause (typing, auto-highlight, list changes, open/close); Menu MUST additionally report `'none'` (and no event) when a write merely restores the already-reported item.
  Source: `utils/getHighlightReason.ts:8-21`; `combobox/root/AriaCombobox.tsx:606-653, 1198-1227`; `menu/store/MenuStore.ts:224-240`; `menu/root/MenuRoot.tsx:562-566, 598-613`; docs JSDoc `menu/root/MenuRoot.tsx:850-860`, `combobox/root/ComboboxRoot.tsx:139-148`. Test: [L4] `menu/root/MenuRoot.test.tsx` "passes the keydown event for keyboard highlights", "passes native mouse and pointer events for pointer highlights", "does not report a stale highlight reason for a later list change"; `combobox/root/ComboboxRoot.test.tsx` "fires on keyboard navigation", "reports no keyboard reason when typing clears the highlight".
- **EVD-EVT-25** — MUST. `OTPField.Root.onValueComplete` MUST fire *after* `onValueChange` and after the new value is applied (post-commit, pre-paint, once the committed value equals the pending complete value), and MUST fire for a complete paste that equals the current value even though `onValueChange` does not; `onValueInvalid` MUST fire *before* `onValueChange` for the same input. `Form.onFormSubmit` MUST fire with `reason: 'none'` and the native submit event after `preventDefault()` was called on it.
  Source: `otp-field/root/OTPFieldRoot.tsx:216-226, 228-262, 423-433` (JSDoc `570-578`); `otp-field/input/OTPFieldInput.tsx:113-115`; `form/Form.tsx:125-136`. Test: [L2] `otp-field/root/OTPFieldRoot.test.tsx` "fires when typing completes the OTP", "fires `input-paste` when a complete paste replaces a complete OTP", "fires when typing is normalized before the OTP value updates"; `slider/root/SliderRoot.test.tsx` "submits clamped range slider values to onFormSubmit".

#### 4.3.6 DOM-handler prevention versus callbacks

- **EVD-EVT-26** — MUST. Calling `preventBaseUIHandler()` on a DOM event inside a consumer handler MUST stop Base UI's own handler for that event on that part; consequently the change callback that handler would have requested MUST NOT fire (e.g. `onClick` + `preventBaseUIHandler()` on `Menu.Item` keeps the menu open; on `Dialog.Close` keeps the dialog open; on `Combobox.Item` does not select). Handlers merged *after* the prevention call are also skipped; handlers supplied as prop-getter functions must check `baseUIHandlerPrevented` themselves. Server: see ADR 01 relaxation table.
  Source: `merge-props/mergeProps.ts:13-31, 210-263`; `internals/use-button/useButton.ts:116-125, 184-200`; `combobox/utils/handleInputPress.ts:14-16`. Test: [L4] `merge-props/mergeProps.test.ts` "prevents internal handler if event.preventBaseUIHandler() is called", "prevents handlers merged after event.preventBaseUIHandler() is called", "allows props getter handlers to check baseUIHandlerPrevented manually"; `menu/item/MenuItem.test.tsx` "does not close the menu when onClick prevents Base UI handler"; `dialog/close/DialogClose.test.tsx` "does not close the dialog when the Base UI click handler is prevented"; `combobox/item/ComboboxItem.test.tsx` "does not select the item when onClick prevents Base UI handler"; `menu/trigger/MenuTrigger.test.tsx` "prevents opening the menu with keyboard when `preventBaseUIHandler` is called in onClick".
- **EVD-EVT-27** — MUST. Base UI MUST itself call `preventBaseUIHandler()` on a keydown/keyup it converts into a synthetic click (Enter / Space activation of non-native buttons and composite items) so that the same key event is not handled twice; the resulting click then produces the change callback with the *click* as its native event (`event.detail === 0`).
  Source: `internals/use-button/useButton.ts:146-149, 172-175, 214-215`; `checkbox/root/CheckboxRoot.tsx:320-332`; `menu/utils/isKeyboardOpen.ts:3-13`. Test: [L4] `internals/use-button/useButton.test.tsx` "does not click composite buttons when keydown calls preventBaseUIHandler"; `menu/root/MenuRoot.test.tsx` "closes with a `detail === 0` click event on keyboard item activation".

#### 4.3.7 Reason matrix — which reasons each callback emits

- **EVD-EVT-28** — MUST. Each callback below MUST emit only the reasons listed for it, under the conditions listed, and MUST emit the listed reason for the listed user action. Rows marked *declared only* are in the public type union but have no emitter in v1.9.0; a port MUST keep them in the type union and MUST NOT emit them from other interactions.
  Source: per row of the tables below. Test: [L2] per row of the tables below (`none` where no test asserts the reason). Rows that originate in a shared interaction cite the shared emitter and link `floating-and-positioning.md` (FLT) or `popup-store-and-handles.md` (PSH) (L4 for the rows whose emitter is a JavaScript-owned interaction: hover, focus, Escape, outside press, list navigation, swipe).

**Shared emitters** (the popup roots below wire these; the component docs state which options are passed):

| Reason | Emitter | Source |
|---|---|---|
| `trigger-press` (open/toggle) | click interaction on the trigger | `floating-ui-react/hooks/useClick.ts:76-92` (`reason` option defaults to `trigger-press`) |
| `trigger-press` (close) | reference press when the dismiss `referencePress` option returns true (Tooltip `closeOnClick`) | `floating-ui-react/hooks/useDismiss.ts:187-199` |
| `trigger-hover` (open/close) | hover interaction on trigger / popup | `floating-ui-react/hooks/useHoverReferenceInteraction.ts:194-205, 278-299, 477-484`; `useHoverFloatingInteraction.ts:165-172, 236-245` |
| `trigger-focus` (open/close) | focus interaction on the trigger | `floating-ui-react/hooks/useFocus.ts:158-183, 222-232` |
| `outside-press` | press outside popup and triggers | `floating-ui-react/hooks/useDismiss.ts:387-520` |
| `escape-key` | Escape keydown on document, trigger or popup | `floating-ui-react/hooks/useDismiss.ts:201-232` |
| `focus-out` | focus leaves a non-modal popup; Shift+Tab in a list; focus guard after the trigger; portal focus guard | `floating-ui-react/components/FloatingFocusManager.tsx:540-568`; `floating-ui-react/hooks/useListNavigation.ts:771-792`; `utils/popups/useTriggerFocusGuards.ts:38-55`; `floating-ui-react/components/FloatingPortal.tsx:275-290` |
| `list-navigation` (open) | ArrowDown/ArrowUp (orientation-dependent) on the trigger | `floating-ui-react/hooks/useListNavigation.ts:815-825` |
| `list-navigation` (close) | cross-orientation arrow key inside a nested list | `floating-ui-react/hooks/useListNavigation.ts:570-583` |
| `imperative-action` | `actionsRef.current.close()`; handle `open(triggerId)` / `openWithPayload()` / `close()` | per root (see rows); `utils/popups/popupHandle.ts:259-262, 284`; `dialog/store/DialogHandle.ts:55-56` |
| `none` (active-trigger unmount) | the active trigger unmounts while open, roots that enable `closeOnActiveTriggerUnmount` | `utils/popups/popupStoreUtils.ts:524-546` |

**Collapsible / Accordion**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `Collapsible.Root.onOpenChange` | `trigger-press` | Trigger clicked or activated by keyboard | `collapsible/root/useCollapsibleRoot.ts:31-42` | `collapsible/root/CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails" |
| `Collapsible.Root.onOpenChange` | `none` | Browser find-in-page reveals a `hidden="until-found"` panel (`beforematch`) | `collapsible/panel/useCollapsiblePanel.ts:421-433` | `collapsible/panel/CollapsiblePanel.test.tsx` "does not open or suppress the next trigger open when beforematch is canceled" |
| `Accordion.Item.onOpenChange`, then `Accordion.Root.onValueChange` | `trigger-press` | Item trigger activated | `collapsible/root/useCollapsibleRoot.ts:31-42` → `accordion/item/AccordionItem.tsx:56-66` → `accordion/root/AccordionRoot.tsx:66-88` | `accordion/root/AccordionRoot.test.tsx` "default item value" |
| `Accordion.Item.onOpenChange`, then `Accordion.Root.onValueChange` | `none` | `beforematch` reveal of an item panel | `collapsible/panel/useCollapsiblePanel.ts:421-433` (reached via `accordion/panel/AccordionPanel.tsx:84`) | none in `accordion/` (the shared mechanism is tested only through Collapsible: `collapsible/panel/CollapsiblePanel.test.tsx` "re-collapses the panel when the open state never arrives"; see TRN-ANI-32..34) |

**Form controls**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `Checkbox.Root.onCheckedChange` | `none` | Hidden input `change` after click / Space / Enter / label click | `checkbox/root/CheckboxRoot.tsx:211-237` | `checkbox/root/CheckboxRoot.test.tsx` "does not update its state when onCheckedChange cancels the event" |
| `CheckboxGroup.onValueChange` | `none` | A child checkbox toggled (same details object); the parent checkbox toggled | `checkbox/root/CheckboxRoot.tsx:231, 239-245`; `checkbox-group/useCheckboxGroupParent.ts:78-96, 101-118`; `checkbox-group/CheckboxGroup.tsx:69-79` | `checkbox-group/CheckboxGroup.test.tsx` "does not update the group when onValueChange cancels the event" |
| `Switch.Root.onCheckedChange` | `none` | Hidden input `change` | `switch/root/SwitchRoot.tsx:172-193` | `switch/root/SwitchRoot.test.tsx` "does not change state when canceled via a hidden input click" |
| `RadioGroup.onValueChange` | `none` | A radio's hidden input `change` (click, Space, arrow-key navigation) | `radio/root/RadioRoot.tsx:196-215` → `radio-group/RadioGroup.tsx:82-92` | `radio-group/RadioGroup.test.tsx` "does not change state when canceled via arrow key navigation" |
| `Toggle.onPressedChange` | `none` | Click (incl. keyboard-generated) | `toggle/Toggle.tsx:82-103` | `toggle/Toggle.test.tsx` "does not change the pressed state when the event is canceled" |
| `ToggleGroup.onValueChange` | `none` | A child Toggle pressed (same details object) | `toggle/Toggle.tsx:94-96` → `toggle-group/ToggleGroup.tsx:55-81` | `toggle-group/ToggleGroup.test.tsx` "does not change the value when the event is canceled" |
| `Tabs.Root.onValueChange` | `none` | A Tab activated by click or by focus (automatic activation) | `tabs/tab/TabsTab.tsx:122-138` → `tabs/root/TabsRoot.tsx:109-124` | `tabs/root/TabsRoot.test.tsx` "does not move an uncontrolled selection when a user-initiated change is canceled" |
| `Tabs.Root.onValueChange` | `initial` | Uncontrolled root with no explicit `defaultValue`: first automatic selection (including a fallback when the implicit first tab is disabled) | `tabs/root/TabsRoot.tsx:227-229, 294-321` | `tabs/root/TabsRoot.test.tsx` "calls onValueChange when auto-selecting the first tab on mount", "calls onValueChange when the implicit first tab is disabled" |
| `Tabs.Root.onValueChange` | `disabled` | Uncontrolled root: the selected tab became disabled → fallback to first enabled tab or `null` | `tabs/root/TabsRoot.tsx:279, 296-316` | `tabs/root/TabsRoot.test.tsx` "calls onValueChange when the selected tab becomes disabled" |
| `Tabs.Root.onValueChange` | `missing` | Uncontrolled root: the selected tab was removed, all tabs removed, or an explicit `defaultValue` never matched a tab | `tabs/root/TabsRoot.tsx:263-274, 280, 296-316` | `tabs/root/TabsRoot.test.tsx` "calls onValueChange when the selected tab is removed", "calls onValueChange when an explicit defaultValue points at a tab that is never present" |
| `Field.Control.onValueChange`, `Input.onValueChange` | `none` | Native `change` on the input | `field/control/FieldControl.tsx:123-143` | `field/control/FieldControl.test.tsx` "does not validate when the change is canceled" |
| `Form.onFormSubmit` (generic) | `none` | Native `submit` (default prevented) | `form/Form.tsx:118-137` | `slider/root/SliderRoot.test.tsx` "submits clamped range slider values to onFormSubmit" |

**NumberField**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `NumberField.Root.onValueChange` | `input-change` | Typing produced a parseable number | `number-field/input/NumberFieldInput.tsx:237-270` | `number-field/root/NumberFieldRoot.test.tsx` "includes the reason for parseable typing" |
| `NumberField.Root.onValueChange` | `input-clear` | Input text emptied by typing; blur of an emptied input | `number-field/input/NumberFieldInput.tsx:240-243, 156-162` | "includes the reason when clearing the value" |
| `NumberField.Root.onValueChange` | `input-blur` | Blur commits a parsed value different from the stored one | `number-field/input/NumberFieldInput.tsx:197-215` | none |
| `NumberField.Root.onValueChange` | `input-paste` | Paste producing a parseable number | `number-field/input/NumberFieldInput.tsx:445-454` | `number-field/root/NumberFieldRoot.test.tsx` "pastes %s numerals through the input contract"; `number-field/input/NumberFieldInput.test.tsx` "reports an unchanged value once when a paste event carries no clipboard data" |
| `NumberField.Root.onValueChange` | `keyboard` | ArrowUp / ArrowDown step, Home / End boundary | `number-field/input/NumberFieldInput.tsx:381-403` | "includes the reason for keyboard increments" |
| `NumberField.Root.onValueChange` | `increment-press` / `decrement-press` | Increment / Decrement button click or press-and-hold tick; also the dirty-input sync before the step | `number-field/root/useNumberFieldStepperButton.ts:66-68, 86-98, 104-111, 138-146` | "includes the reason for increment button presses", "includes the reason for decrement button presses" |
| `NumberField.Root.onValueChange` | `wheel` | Wheel over the focused input | `number-field/root/NumberFieldRoot.tsx:395-403` | "includes the reason for wheel scrubbing" |
| `NumberField.Root.onValueChange` | `scrub` | ScrubArea pointer drag | `number-field/scrub-area/NumberFieldScrubArea.tsx:233-240` | none |
| `NumberField.Root.onValueChange` | `none` | Browser autofill of the hidden number input | `number-field/root/NumberFieldRoot.tsx:505-521` | "validates the autofilled value even when the change is canceled" |
| `NumberField.Root.onValueCommitted` (generic) | `input-blur`, `input-clear` | Blur after typing / after clearing | `number-field/input/NumberFieldInput.tsx:166-171, 197-217` | "does not commit a canceled clear on blur" |
| `NumberField.Root.onValueCommitted` | `keyboard` | Synchronously with a keyboard step | `number-field/input/NumberFieldInput.tsx:381, 407-409` | "does not commit a canceled keyboard change" |
| `NumberField.Root.onValueCommitted` | `increment-press` / `decrement-press` | Pointer released after a button press/hold; click without hold | `number-field/root/useNumberFieldStepperButton.ts:112-117, 148-151` | "includes the correct reason for increment and decrement button presses" |
| `NumberField.Root.onValueCommitted` | `wheel` | Synchronously with a wheel step | `number-field/root/NumberFieldRoot.tsx:404-409` | "calls onValueChange and onValueCommitted on wheel" |
| `NumberField.Root.onValueCommitted` | `scrub` | Scrub pointer released | `number-field/scrub-area/NumberFieldScrubArea.tsx:160-171` | none |
| `NumberField.Root.onValueCommitted` | `none` | *declared only* ("kept for consistency … never commits with it") | `number-field/root/NumberFieldRoot.tsx:718-728` | — |

**OTPField**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `OTPField.Root.onValueChange` | `input-change` | Typing / IME commit into a slot; password-manager autofill through the hidden input | `otp-field/input/OTPFieldInput.tsx:101-141, 196-218`; `otp-field/root/OTPFieldRoot.tsx:430-433` | `otp-field/root/OTPFieldRoot.test.tsx` "fires `input-change` when typing", "handles password manager autofill through the hidden input" |
| `OTPField.Root.onValueChange` | `input-clear` | A slot emptied by text input | `otp-field/input/OTPFieldInput.tsx:117-130` | "fires `input-clear` when clearing a slot by input" |
| `OTPField.Root.onValueChange` | `input-paste` | Paste | `otp-field/input/OTPFieldInput.tsx:322-329` → `134-141` | "fills consecutive slots when pasting a code" |
| `OTPField.Root.onValueChange` | `keyboard` | Backspace / Delete and other value-changing keys | `otp-field/input/OTPFieldInput.tsx:262-270` | "removes a character and moves focus to the previous slot with backspace" |
| `OTPField.Root.onValueInvalid` (generic) | `input-change`, `input-paste` | Typed/pasted/autofilled text had characters rejected by validation or normalization | `otp-field/input/OTPFieldInput.tsx:113-115`; `otp-field/root/OTPFieldRoot.tsx:423-428` | "fires when typing is normalized before the OTP value updates", "fires `input-paste` when pasted text is normalized before the OTP value updates" |
| `OTPField.Root.onValueComplete` (generic) | `input-change`, `input-paste` | Value reached full length by typing/paste, or a complete paste replaced a complete value | `otp-field/root/OTPFieldRoot.tsx:231-262` | "fires when typing completes the OTP", "fires `input-paste` when a complete paste replaces a complete OTP" |

**Slider**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `Slider.Root.onValueChange` | `input-change` | Hidden range input `change` (form integration / programmatic input) | `slider/root/SliderRoot.tsx:228-247` | `slider/root/SliderRoot.test.tsx` "provides the change reason for input events" |
| `Slider.Root.onValueChange` | `keyboard` | Arrow / Home / End / PageUp / PageDown on a thumb | `slider/root/SliderRoot.tsx:233` | "provides the change reason for keyboard interactions" |
| `Slider.Root.onValueChange` | `track-press` | Pointer/touch down on the control (not on a thumb) | `slider/control/SliderControl.tsx:293-303, 428-437, 535-540` | "provides the change reason for track presses" |
| `Slider.Root.onValueChange` | `drag` | Pointer/touch move while pressed | `slider/control/SliderControl.tsx:320-349` | `slider/control/SliderControl.test.tsx` "moves the first of several thumbs stacked at the maximum" |
| `Slider.Root.onValueChange` | `none` | *declared only* (JSDoc: "when the change is triggered without a specific interaction"; no emitter found, §10 Q7) | `slider/root/SliderRoot.tsx:549, 585-590` | — |
| `Slider.Root.onValueCommitted` (generic) | `input-change`, `keyboard` | Synchronously after an applied input/keyboard change | `slider/root/SliderRoot.tsx:242-244` | "single value" (line 863), "provides the change reason for keyboard interactions" |
| `Slider.Root.onValueCommitted` | `track-press`, `drag` | Pointer released; reason is that of the last applied change | `slider/control/SliderControl.tsx:351-375` | "single value" (line 855), "array value" |
| `Slider.Root.onValueCommitted` | `none` | *declared only* (initial value of the last-reason record; a release without any applied change does not commit) | `slider/root/SliderRoot.tsx:135`, `slider/control/SliderControl.tsx:369-375` | — |

**Select**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `Select.Root.onOpenChange` | `trigger-press` | Trigger press (shared click interaction) | `select/root/SelectRoot.tsx:364-368`; FLT | none |
| `Select.Root.onOpenChange` | `outside-press`, `escape-key` | shared dismiss | `select/root/SelectRoot.tsx:369`; FLT | none |
| `Select.Root.onOpenChange` | `list-navigation` | ArrowUp/ArrowDown on the trigger opens | `select/root/SelectRoot.tsx:371`; `floating-ui-react/hooks/useListNavigation.ts:815-825` | none |
| `Select.Root.onOpenChange` | `item-press` | Single-select item activated (after `onValueChange`) | `select/item/SelectItem.tsx:133-151` | none |
| `Select.Root.onOpenChange` | `focus-out` | Focus leaves the popup / Shift+Tab | `select/popup/SelectPopup.tsx:479` (focus manager); FLT | none |
| `Select.Root.onOpenChange` | `window-resize` | Window resized while the popup is aligned to the trigger | `select/popup/SelectPopup.tsx:421-433` | none |
| `Select.Root.onOpenChange` | `cancel-open` | Mousedown on the trigger opened the popup and the mouseup landed outside trigger and popup | `select/trigger/SelectTrigger.tsx:173-206` | none |
| `Select.Root.onOpenChange` | `imperative-action` | `actionsRef.current.close()` while open | `select/root/SelectRoot.tsx:317-328` | `select/root/SelectRoot.test.tsx` "closes through the `close` action so `onOpenChange` can opt out" |
| `Select.Root.onOpenChange` | `none` | Trigger receives focus while the popup is open in item-aligned mode (popup would obscure the trigger) | `select/trigger/SelectTrigger.tsx:145-151` | `select/trigger/SelectTrigger.test.tsx` "closes an aligned popup when focus lands on the trigger so it is not obscured" |
| `Select.Root.onValueChange` | `item-press` | Item activated (click, Enter, Space, drag-release) | `select/item/SelectItem.tsx:133-151` | none |
| `Select.Root.onValueChange` | `none` | Typeahead match while closed; browser autofill of the hidden input; selected value no longer among the items (fallback to initial value / null) | `select/root/SelectRoot.tsx:401-406, 526-565`; `select/positioner/SelectPositioner.tsx:141-190` | "does not update field state when autofill is canceled" |
| `Select.Root.onValueChange` | `trigger-press`, `outside-press`, `escape-key`, `window-resize`, `focus-out`, `list-navigation`, `cancel-open`, `imperative-action` | *declared only* for this callback (the union is shared with `onOpenChange`) | `select/root/SelectRoot.tsx:746-756` | — |

**Combobox / Autocomplete** (Autocomplete re-exports Combobox's types; `Autocomplete.Root.onValueChange` is the *input value* callback)

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `onOpenChange` | `trigger-press` | `Combobox.Trigger` press (shared click interaction) | `combobox/trigger/ComboboxTrigger.tsx:124`; FLT | `combobox/trigger/ComboboxTrigger.test.tsx` "fires with reason trigger-press when Trigger is clicked" |
| `onOpenChange` | `input-press` | Input (or input group) pressed with `openOnInputClick` | `combobox/utils/handleInputPress.ts:8-38`; `combobox/root/AriaCombobox.tsx:1384-1392` (click interaction configured with `reason: input-press`) | `combobox/input/ComboboxInput.test.tsx` "fires with reason input-press when Input is clicked"; `floating-ui-react/hooks/useClick.test.tsx` "uses the configured reason" |
| `onOpenChange` | `outside-press`, `escape-key` | shared dismiss (not inline) | `combobox/root/AriaCombobox.tsx:1394`; FLT | none |
| `onOpenChange` | `item-press` | Item activated (after `onValueChange`; single mode, or multiple mode while filtering with the input outside the popup; anchor links) | `combobox/root/AriaCombobox.tsx:879-943` | `combobox/root/ComboboxRoot.test.tsx` "fires onOpenChange once with reason item-press on mouse click", "… on Enter selection" |
| `onOpenChange` | `close-press` | Internal dismiss button inside the popup (mobile layout) | `combobox/utils/ComboboxInternalDismissButton.tsx:25-30` | none |
| `onOpenChange` | `list-navigation` | ArrowDown/ArrowUp on the trigger or a chip; list-navigation open on the input | `combobox/trigger/ComboboxTrigger.tsx:247-256`; `combobox/chip/ComboboxChip.tsx:77-86`; FLT | none |
| `onOpenChange` | `focus-out` | Focus leaves (shared focus manager / portal guards / Shift+Tab) | FLT | none |
| `onOpenChange` | `input-change` | Typing a non-empty query opens | `combobox/input/ComboboxInput.tsx:248-258` | `combobox/root/ComboboxRoot.test.tsx` "discards the request when an ignored typed open is followed by another open" |
| `onOpenChange` | `input-clear` | Input emptied while `openOnInputClick` is false and the input is outside the popup | `combobox/input/ComboboxInput.tsx:270-275, 296-306` | none |
| `onOpenChange` | `cancel-open` | Trigger mousedown opened, mouseup outside trigger/popup (input inside popup) | `combobox/trigger/ComboboxTrigger.tsx:215-245` | `combobox/trigger/ComboboxTrigger.test.tsx` "closes the popup when mouseup occurs outside the trigger bounds" |
| `onOpenChange` | `imperative-action` | `actionsRef.current.close()` | `combobox/root/AriaCombobox.tsx:1017-1024` | "closes through the `close` action so `onOpenChange` can opt out" |
| `onOpenChange` | `none` | Enter with no highlighted item closes (allows form submission) | `combobox/input/ComboboxInput.tsx:404-416` | none |
| `onOpenChange` | `clear-press`, `chip-remove-press` | *declared only* for this callback (shared union) | `combobox/root/AriaCombobox.tsx:2019-2034` | — |
| `onInputValueChange` (Combobox) / `onValueChange` (Autocomplete) | `input-change` | Typing; composition end | `combobox/input/ComboboxInput.tsx:230-238, 289-294` | none |
| same | `input-clear` | Multiple mode: clear after selection (`isItemPress: true`) or on close (`isItemPress` set from the close reason); after close completes; cleanup when a reopen interrupts the close; single mode with input inside popup after close | `combobox/root/AriaCombobox.tsx:751-754, 819-829, 912-923, 963-991` | "clears the input with the "input-clear" reason and the item click event on pointer selection", "\"multiple\" clears typed input on close when no selection made", "clears the pending filter with the "input-clear" reason when reopening interrupts the close animation" |
| same | `clear-press` | `Combobox.Clear` activated | `combobox/clear/ComboboxClear.tsx:101-111` | none |
| same | `escape-key` | Escape while the popup is closed (clears input and selection) | `combobox/input/ComboboxInput.tsx:351-367` | none |
| same | `item-press`, `none`, `input-clear`, `clear-press`, `escape-key` (re-used from the selected-value change) | The input is filled with the selected label after a selected-value change (single mode with input outside the popup; `selectionMode="none"` with `fillInputOnItemPress`) | `combobox/root/AriaCombobox.tsx:866-875` | none |
| same | `none` | Input re-synced to the selected label (close without selection, items/label change); input inside popup mounted with no value | `combobox/root/AriaCombobox.tsx:1284-1289, 984-989`; `combobox/input/ComboboxInput.tsx:113-118` | "should reset input value to selected value when popup closes without selection", "should handle browser autofill" |
| `onValueChange` (Combobox selected value) | `item-press` | Item activated | `combobox/root/AriaCombobox.tsx:879-943` | none |
| same | `input-clear` | Single mode: input emptied by typing (input outside popup) | `combobox/input/ComboboxInput.tsx:296-302` | none |
| same | `escape-key` | Escape while closed clears the selection | `combobox/input/ComboboxInput.tsx:351-361` | none |
| same | `clear-press` | `Combobox.Clear` activated (`selectionMode !== 'none'`) | `combobox/clear/ComboboxClear.tsx:113-117` | none |
| same | `chip-remove-press` | `Combobox.ChipRemove` click / Enter / Space | `combobox/chip-remove/ComboboxChipRemove.tsx:76-115` | `combobox/chip-remove/ComboboxChipRemove.test.tsx` "allows click and keyboard events to propagate when requested" |
| same | `none` | Backspace in an empty input removes the last chip; Backspace/Delete on a focused chip; typeahead on the closed trigger; browser autofill of the hidden input | `combobox/input/ComboboxInput.tsx:369-391`; `combobox/chip/ComboboxChip.tsx:56-73`; `combobox/trigger/ComboboxTrigger.tsx:110-122`; `combobox/root/AriaCombobox.tsx:1626-1665` | "should handle browser autofill with object values when autofill uses the label" |
| `onItemHighlighted` (highlight details, `index`) | `keyboard` | Arrow-key / typeahead highlight move | `combobox/root/AriaCombobox.tsx:606-653`; `utils/getHighlightReason.ts:8-21` | "fires on keyboard navigation", "should report arrow-key highlights as keyboard-driven when readOnly" |
| same | `pointer` | Hover highlight (mouse event) / pointer leave (pointer event) | same | "passes native mouse and pointer events for pointer highlights" |
| same | `none` | Typing, auto-highlight, item list change, open/close, clear button | `combobox/root/AriaCombobox.tsx:1198-1227` | "reports no keyboard reason when typing clears the highlight", "reports no keyboard reason when the clear button clears the highlight" |

**Menu / ContextMenu** (`Menu.SubmenuRoot`, `ContextMenu.Root`, `ContextMenu.SubmenuRoot` share `Menu.Root`'s union and emitters)

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `Menu.Root.onOpenChange` | `trigger-hover` | Hover open/close with `openOnHover`; submenu trigger hover | `menu/trigger/MenuTrigger.tsx` (hover interaction), `menu/submenu-trigger/MenuSubmenuTrigger.tsx` (hover interaction); FLT | `floating-ui-react/hooks/useHoverReferenceInteraction.test.tsx` "reports the hover reason" |
| `Menu.Root.onOpenChange` | `trigger-focus` | Only `Menu.Trigger` inside a Menubar while another bar menu is open (`hasSubmenuOpen`): trigger focus opens, trigger blur closes. Never emitted outside a Menubar, never by ContextMenu or a submenu trigger | `menu/trigger/MenuTrigger.tsx:173,205-207`; `floating-ui-react/hooks/useFocus.ts:158-183,222-232` | none |
| `Menu.Root.onOpenChange` | `trigger-press` | Trigger press; ContextMenu right-click / long-press | FLT; `context-menu/trigger/ContextMenuTrigger.tsx:68-69` | `menu/trigger/MenuTrigger.test.tsx` "prevents opening the menu with keyboard when `preventBaseUIHandler` is called in onClick" (negative) |
| `Menu.Root.onOpenChange` | `outside-press`, `escape-key` | shared dismiss | FLT | `menu/root/MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local, keepMounted=%s"; `menu/root/MenuRoot.test.tsx` "closes the entire tree when clicking outside the deepest submenu" |
| `Menu.Root.onOpenChange` | `focus-out` | Focus leaves; Shift+Tab; trigger focus guards; focus returns to a submenu trigger through its guard | FLT; `utils/popups/useTriggerFocusGuards.ts:38-55`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:245-253`; `menu/filter-root/useMenuFilterKeyDown.ts:189-191` | `menu/root/MenuRoot.test.tsx` "keeps focus in the menu when a Shift+Tab close is canceled" |
| `Menu.Root.onOpenChange` | `list-navigation` | Arrow key on the trigger opens; cross-orientation arrow closes a submenu; filter submenu keyboard open/close | FLT; `menu/filter-submenu-root/MenuFilterSubmenuRoot.tsx:214, 275` | none |
| `Menu.Root.onOpenChange` | `item-press` | A `Menu.Item` with `closeOnClick` activated (tree-wide close event relayed to every open menu in the branch) | `menu/item/useMenuItemCommonProps.ts:102-106` → `menu/popup/MenuPopup.tsx:108-121` | `menu/root/MenuRoot.test.tsx` "closes with a `detail === 0` click event on keyboard item activation"; `context-menu/root/ContextMenuRoot.test.tsx` "closes nested submenus when releasing the context menu pointer over an item" |
| `Menu.Root.onOpenChange` | `close-press` | *declared only* (no emitter found, §10 Q1) | `menu/root/MenuRoot.tsx:933` | — |
| `Menu.Root.onOpenChange` | `sibling-open` | A sibling submenu opened; a different parent item hovered (after `closeDelay`); the parent menu closed with no reason | `menu/positioner/MenuPositioner.tsx:135-147, 156-175, 186-216` | `menu/positioner/MenuPositioner.test.tsx` "closes an open submenu with a sibling reason when its controlled parent closes"; `menu/root/MenuRoot.test.tsx` "keeps the root menu open when a submenu opens and the trigger `render` element has a custom id" |
| `Menu.Root.onOpenChange` | `cancel-open` | Hover-opened menu: mouseup outside trigger and menu; ContextMenu: mouseup outside after long-press open | `menu/trigger/MenuTrigger.tsx:134-170` → `menu/popup/MenuPopup.tsx:108-121`; `context-menu/trigger/ContextMenuTrigger.tsx:96-119` | `menu/root/MenuRoot.test.tsx` "closes the menu on click, drag outside, release" |
| `Menu.Root.onOpenChange` | `imperative-action` | `actionsRef.current.close()`; handle open/close | `menu/root/MenuRoot.tsx:478-486`; PSH | `utils/popups/popupHandle.test.ts` "closes the attached root" |
| `Menu.Root.onOpenChange` | `none` | A filtered-out submenu trigger unmounts while its submenu is open; parent-menu close relayed without a reason | `menu/filter-root/useMenuFilterSubmenuTrigger.ts:39-45`; `menu/positioner/MenuPositioner.tsx:166` | none |
| `Menu.CheckboxItem.onCheckedChange` | `item-press` | Item click (details carry a no-op `preventUnmountOnClose`) | `menu/checkbox-item/MenuCheckboxItem.tsx:68-74, 119-127` | `menu/checkbox-item/MenuCheckboxItem.test.tsx` "does not toggle when `onCheckedChange` cancels the event" |
| `Menu.RadioGroup.onValueChange` | `item-press` | `Menu.RadioItem` click | `menu/radio-item/MenuRadioItem.tsx:72-78` → `menu/radio-group/MenuRadioGroup.tsx:39-49` | `menu/radio-item/MenuRadioItem.test.tsx` "does not select when `onValueChange` cancels the event" |
| `Menu.Root.onItemHighlighted` (highlight details, `label`) | `keyboard` | Arrow keys, typeahead, filter-input arrow handoff | `menu/root/MenuRoot.tsx:555-568`; `menu/store/MenuStore.ts:224-240`; `menu/filter-root/moveHighlightFrom.ts:39`; `menu/filter-root/useMenuFilterKeyDown.ts:164` | `menu/root/MenuRoot.test.tsx` "passes the keydown event for keyboard highlights", "passes the keydown event for typeahead highlights" |
| same | `pointer` | Hover over an item / submenu trigger; pointer leave | `menu/submenu-trigger/MenuSubmenuTrigger.tsx:155`; CMP list navigation | "passes native mouse and pointer events for pointer highlights", "passes the mouse event when a submenu trigger is hovered" |
| same | `none` | Automatic highlight while filtering, list change, popup open/close, blur of a submenu trigger, write-back of the already reported item | `menu/root/MenuRoot.tsx:598-601`; `menu/store/MenuStore.ts:232-238`; `menu/submenu-trigger/MenuSubmenuTrigger.tsx:254-258`; `menu/filter-root/useVirtualFocusParentHandoff.ts:41, 61`; `menu/filter-root/MenuFilterDropdown.tsx:64-66` | "does not report a stale highlight reason for a later list change"; `menu/filter-root/MenuFilterRoot.test.tsx` "reports no keyboard reason when Enter opens a filterable submenu" |
| `Menu.FilterProvider.onValueChange` | `input-change` | Typing in `Menu.Input` | `filter-dropdown/input/FilterDropdownInput.tsx:45-53` → `menu/filter-root/MenuFilterDropdown.tsx:54-61` | `menu/filter-root/MenuFilterRoot.test.tsx` "reports input-change while typing" |
| `Menu.FilterProvider.onValueChange` | `input-clear` | Input emptied by typing | `filter-dropdown/input/FilterDropdownInput.tsx:51` | "reports input-clear when the field is emptied" |
| `Menu.FilterProvider.onValueChange` | `clear-press` | `Menu.Clear` activated | `filter-dropdown/clear/FilterDropdownClear.tsx:55-59` | "reports clear-press when the clear button is used" |
| `Menu.FilterProvider.onValueChange` | `popup-close` | The menu closed (any reason, controlled or not) while the query is non-empty: value reset to `''` post-commit, pre-paint; cancel keeps the query | `filter-dropdown/root/useFilterDropdownCloseQuery.ts:26-35` | "reports popup-close when a controlled close discards the query", "resets the input value once when the popup closes"; Docs: `components/menu/page.mdx:432` |

**Dialog / AlertDialog / Drawer**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `Dialog.Root.onOpenChange` (AlertDialog identical except that it never emits `outside-press` or `focus-out`) | `trigger-press` | Trigger press | FLT (click interaction wired in `dialog/root/useRenderDialogRoot.tsx`) | none |
| same | `outside-press` | Press on the backdrop / outside (Dialog only — AlertDialog forces `disablePointerDismissal`, so the outside-press predicate always rejects) | FLT; `dialog/root/useRenderDialogRoot.tsx:38`, `dialog/root/useDialogRoot.ts:66-85` | `dialog/root/DialogRoot.test.tsx` "calls onOpenChange with the reason for change when user clicks backdrop while the modal dialog is open", "… clicks outside while the non-modal dialog is open" |
| same | `escape-key` | Escape | FLT | "emits a single internal openchange when closing on Escape" |
| same | `close-press` | `Dialog.Close` activated while open | `dialog/close/DialogClose.tsx:39-43` | `dialog/close/DialogClose.test.tsx` "does not close the dialog when the Base UI click handler is prevented" (negative) |
| same | `focus-out` | Non-modal dialog loses focus (Dialog only, and only without `disablePointerDismissal`; AlertDialog passes `closeOnFocusOut={false}`) | FLT (`FloatingFocusManager` `closeOnFocusOut`); `dialog/popup/DialogPopup.tsx:102` | none |
| same | `imperative-action` | `actionsRef.current.close()`; handle `open`/`openWithPayload`/`close` | `dialog/root/useRenderDialogRoot.tsx:80-87`; `dialog/store/DialogHandle.ts:55-56`; PSH | "reports no trigger when closing with an initial trigger id that is not mounted" |
| same | `none` | *declared only* (Dialog does not enable the trigger-unmount close) | `dialog/root/useRenderDialogRoot.tsx:77`; `dialog/root/DialogRoot.tsx:105` | — |
| `Drawer.Root.onOpenChange` | `trigger-press`, `outside-press`, `escape-key`, `close-press`, `focus-out`, `imperative-action` | As Dialog (Drawer builds on the dialog store; `Drawer.Close` is `Dialog.Close`) | `drawer/close/DrawerClose.tsx:12`; `drawer/root/DrawerRoot.tsx:150-169` | `drawer/root/DrawerRoot.test.tsx` "provides event details when snap point changes" (`close-press`) |
| `Drawer.Root.onOpenChange` | `close-watcher` | Platform `CloseWatcher` `cancel` (back gesture / UA Esc) while open | `drawer/root/DrawerRoot.tsx:460-482` | "closes when the watcher requests a close" |
| `Drawer.Root.onOpenChange` | `swipe` | Swipe-to-dismiss release past the threshold; `Drawer.SwipeArea` swipe open/close | `drawer/viewport/DrawerViewport.tsx:648-652`; `drawer/swipe-area/DrawerSwipeArea.tsx:302-309` | `drawer/viewport/DrawerViewport.test.tsx` "dismisses with a touch swipe down that starts on an SVG inside a top-edge scroll container" |
| `Drawer.Root.onOpenChange` | `none` | *declared only* | `drawer/root/DrawerRoot.tsx:365` | — |
| `Drawer.Root.onSnapPointChange` | `swipe` | Swipe release settles on a snap point or on `null` (dismiss); revert after a canceled/rejected dismiss | `drawer/viewport/DrawerViewport.tsx:541-571, 654-658, 676-684` | none (the `swipe` reason is asserted only on `onOpenChange`: `drawer/viewport/DrawerViewport.test.tsx` `it.each` at line 939) |
| `Drawer.Root.onSnapPointChange` | any `Drawer.Root.onOpenChange` reason | The drawer closes with snap points configured: the active snap point resets to the default, re-wrapped with the close's reason, event and trigger | `drawer/root/DrawerRoot.tsx:158-167` | `drawer/root/DrawerRoot.test.tsx` "provides event details when snap point changes" |
| `Drawer.Root.onSnapPointChange` | `none` | Internal snap-point set without details (e.g. uncontrolled fallback) | `drawer/root/DrawerRoot.tsx:79-94` | "honors canceled snap point changes and falls back from invalid uncontrolled values" |

**Popover / Tooltip / PreviewCard**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `Popover.Root.onOpenChange` | `trigger-hover` | `openOnHover` trigger hover / popup leave | `popover/trigger/PopoverTrigger.tsx` (hover interaction); `popover/popup/PopoverPopup.tsx:66`; FLT | none |
| same | `trigger-focus` | *declared only* (no focus interaction wired, §10 Q3) | `popover/root/PopoverRoot.tsx:210` | — |
| same | `trigger-press` | Trigger press | FLT | `popover/root/PopoverRoot.detached-triggers.test.tsx` (patient-click comment at line 393) |
| same | `outside-press`, `escape-key` | shared dismiss | FLT | "closing via outside press: works when clicking another element inside the same shadow root" |
| same | `close-press` | `Popover.Close` activated | `popover/close/PopoverClose.tsx:39-50` | `popover/close/PopoverClose.test.tsx` "keeps the trigger when closing with a tooltip trigger close button" |
| same | `focus-out` | Focus leaves a non-modal popover; trigger focus guards; portal guards | FLT; `utils/popups/useTriggerFocusGuards.ts:38-55` | none |
| same | `imperative-action` | `actionsRef.current.close()`; handle | `popover/root/PopoverRoot.tsx:74-81`; PSH | none |
| same | `none` | *declared only* (trigger-unmount close not enabled) | `popover/root/PopoverRoot.tsx:58, 217` | — |
| `Tooltip.Root.onOpenChange` | `trigger-hover` | Trigger hover (after `delay`), leave (after `closeDelay`), nested-trigger handoff | `tooltip/trigger/TooltipTrigger.tsx:146, 172-202`; FLT | `floating-ui-react/hooks/useHoverReferenceInteraction.test.tsx` "reports the hover reason" |
| same | `trigger-focus` | Trigger keyboard focus / blur | `tooltip/trigger/TooltipTrigger.tsx:166`; FLT | none |
| same | `trigger-press` | Trigger pressed while open with `closeOnClick` (dismiss `referencePress`) | `tooltip/root/TooltipRoot.tsx:260-263`; `floating-ui-react/hooks/useDismiss.ts:187-199` | none |
| same | `outside-press`, `escape-key` | shared dismiss | `tooltip/root/TooltipRoot.tsx:260`; FLT | `tooltip/root/TooltipRoot.test.tsx` "should close when Escape is pressed" |
| same | `disabled` | `disabled` becomes true while open (post-commit, pre-paint) | `tooltip/root/TooltipRoot.tsx:96-100` | none |
| same | `imperative-action` | `actionsRef.current.close()`; handle | `tooltip/root/TooltipRoot.tsx:128-135`; PSH | "closes the tooltip when the `close` method is called" |
| same | `none` | Delay-group handoff: another grouped tooltip opens; active trigger unmounts | `floating-ui-react/components/FloatingDelayGroup.tsx:234-238`; `utils/popups/popupStoreUtils.ts:524-546` (enabled `tooltip/root/TooltipRoot.tsx:81`) | `tooltip/root/TooltipRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled" |
| `PreviewCard.Root.onOpenChange` | `trigger-hover` | Trigger hover / leave; popup leave | `preview-card/trigger/PreviewCardTrigger.tsx:70`; `preview-card/popup/PreviewCardPopup.tsx:47`; FLT | none |
| same | `trigger-focus` | Trigger focus / blur (after `delay`) | `preview-card/trigger/PreviewCardTrigger.tsx:80`; FLT | none |
| same | `trigger-press` | *declared only* (dismiss wired without `referencePress`, §10 Q3) | `preview-card/root/PreviewCardRoot.tsx:98, 194` | — |
| same | `outside-press`, `escape-key` | shared dismiss | `preview-card/root/PreviewCardRoot.tsx:98`; FLT | none |
| same | `imperative-action` | `actionsRef.current.close()`; handle | `preview-card/root/PreviewCardRoot.tsx:72-79`; PSH | "closes the preview card when the `close` method is called" |
| same | `none` | Active trigger unmounts | `utils/popups/popupStoreUtils.ts:524-546` (enabled `preview-card/root/PreviewCardRoot.tsx:59`) | `preview-card/root/PreviewCardRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled" |

**NavigationMenu**

| Callback | Reason | Emitted when | Source | Test |
|---|---|---|---|---|
| `NavigationMenu.Root.onValueChange` | `trigger-press` | Trigger click (shared click interaction relayed through the trigger's open-change handler; also direct activation while another item is open) | `navigation-menu/trigger/NavigationMenuTrigger.tsx:432-474, 537, 584-592` | none |
| same | `trigger-hover` | Trigger hover / leave (`openOnHover`) | `navigation-menu/trigger/NavigationMenuTrigger.tsx:519, 584-592` | none |
| same | `outside-press`, `escape-key` | shared dismiss on the list | `navigation-menu/list/NavigationMenuList.tsx:54`; FLT | none |
| same | `list-navigation` | ArrowDown (horizontal) / ArrowRight-or-Left (vertical, RTL-aware) on the trigger | `navigation-menu/trigger/NavigationMenuTrigger.tsx:684-692` | none |
| same | `focus-out` | Trigger or link blur to outside the menu; Tab past the popup's focus guard | `navigation-menu/trigger/NavigationMenuTrigger.tsx:694-708, 775-777`; `navigation-menu/link/NavigationMenuLink.tsx:49-63` | none |
| same | `link-press` | `NavigationMenu.Link` with `closeOnClick` activated (also closes a nested menu's parent) | `navigation-menu/link/NavigationMenuLink.tsx:44-48`; `navigation-menu/root/NavigationMenuRoot.tsx:234-241` | none |
| same | `imperative-action` | `actionsRef.current.close()` | `navigation-menu/root/NavigationMenuRoot.tsx:245-252` | `navigation-menu/root/NavigationMenuRoot.test.tsx` "closes through the `close` action so `onValueChange` can opt out" |
| same | `none` | *declared only* (§10 Q2) | `navigation-menu/root/NavigationMenuRoot.tsx:448` | — |

#### 4.3.8 Reverse index — reason → emitting callbacks

| Reason | Callbacks |
|---|---|
| `none` | every form control (Checkbox, CheckboxGroup, Switch, RadioGroup, Toggle, ToggleGroup, Tabs user change, Field.Control, Input), Form submit, Collapsible/Accordion `beforematch`, NumberField autofill, Select (typeahead-closed, autofill, item fallback, aligned-trigger focus), Combobox (input re-sync, chip keyboard removal, trigger typeahead, autofill, Enter-no-highlight close), Menu (filtered submenu trigger unmount, reason-less parent close), Menu/Combobox/Autocomplete highlight, Tooltip (delay group, trigger unmount), PreviewCard (trigger unmount), Drawer snap point (internal) |
| `trigger-press` | Collapsible, Accordion, Dialog, AlertDialog, Drawer, Popover, Tooltip (close with `closeOnClick`), Menu, ContextMenu, Select, Combobox, Autocomplete, NavigationMenu |
| `trigger-hover` | Popover, Tooltip, PreviewCard, Menu, ContextMenu (submenus), NavigationMenu |
| `trigger-focus` | Tooltip, PreviewCard, Menu |
| `outside-press` | Dialog, Drawer, Popover, Tooltip, PreviewCard, Menu, ContextMenu, Select, Combobox, Autocomplete, NavigationMenu |
| `item-press` | Select (`onValueChange`, `onOpenChange`), Combobox/Autocomplete (`onValueChange`, `onOpenChange`, re-used on `onInputValueChange`), Menu/ContextMenu (`onOpenChange`, `CheckboxItem`, `RadioGroup`) |
| `close-press` | Dialog, AlertDialog, Drawer, Popover, Combobox/Autocomplete (internal dismiss button) |
| `link-press` | NavigationMenu |
| `clear-press` | Combobox/Autocomplete (`onInputValueChange`, `onValueChange`), Menu.FilterProvider |
| `chip-remove-press` | Combobox (`onValueChange`) |
| `track-press`, `drag` | Slider (`onValueChange`, `onValueCommitted`) |
| `increment-press`, `decrement-press`, `wheel`, `scrub` | NumberField (`onValueChange`, `onValueCommitted`) |
| `input-change` | NumberField, OTPField (change, invalid, complete), Slider, Combobox/Autocomplete (`onInputValueChange`, `onOpenChange`), Menu.FilterProvider |
| `input-clear` | NumberField (change, commit), OTPField, Combobox/Autocomplete (`onInputValueChange`, `onValueChange`, `onOpenChange`), Menu.FilterProvider |
| `input-blur` | NumberField (change, commit) |
| `input-paste` | NumberField, OTPField (change, invalid, complete) |
| `input-press` | Combobox/Autocomplete (`onOpenChange`) |
| `focus-out` | Dialog, Drawer, Popover, Menu, ContextMenu, Select, Combobox, Autocomplete, NavigationMenu |
| `escape-key` | all popups above; Combobox/Autocomplete (`onInputValueChange`, `onValueChange` while closed) |
| `close-watcher` | Drawer |
| `list-navigation` | Menu, ContextMenu, Select, Combobox, Autocomplete, NavigationMenu |
| `keyboard` | NumberField, OTPField, Slider (change/commit); Menu/Combobox/Autocomplete highlight |
| `pointer` | Menu/Combobox/Autocomplete highlight |
| `popup-close` | Menu.FilterProvider |
| `cancel-open` | Menu, ContextMenu, Select, Combobox, Autocomplete |
| `sibling-open` | Menu, ContextMenu |
| `disabled` | Tooltip, Tabs |
| `missing`, `initial` | Tabs |
| `imperative-action` | all popups with `actionsRef`/handles; NavigationMenu |
| `swipe` | Drawer (`onOpenChange`, `onSnapPointChange`) |
| `window-resize` | Select |

### 4.4 DIS — propagation of dismissal events

- **EVD-DIS-01** — MUST. When an Escape keydown closes a popup, the component MUST call `stopPropagation()` on the keydown so that ancestor popups do not also close, unless the consumer called `allowPropagation()` in `onOpenChange` or the popup is configured to let Escape bubble (`bubbles.escapeKey`, see FLT). This applies whether or not the close was canceled. Server: see ADR 01 relaxation table.
  Source: `floating-ui-react/hooks/useDismiss.ts:228-230`; docs `handbook/customization/page.mdx:59-75`. Test: [L4] `tooltip/root/TooltipRoot.test.tsx` "allowPropagation() prevents stopPropagation on Escape while still closing"; `menu/root/MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local, keepMounted=%s".
- **EVD-DIS-02** — MUST. A popup MUST NOT request an `escape-key` close (and MUST NOT call `onOpenChange`) while it has an open descendant popup that does not bubble Escape; only the innermost such popup handles the key.
  Source: `floating-ui-react/hooks/useDismiss.ts:169-178, 216-218`. Test: [L4] `menu/root/MenuRoot.triggerless.test.tsx` "keeps nested Escape and focus local, keepMounted=%s".
- **EVD-DIS-03** — MUST. Combobox: when Escape is pressed while the popup is not mounted, the input and selection are cleared with `reason: 'escape-key'` and the keydown's propagation MUST be stopped unless there was nothing to clear, the combobox is inline, or `allowPropagation()` was called in either callback.
  Source: `combobox/input/ComboboxInput.tsx:351-367`. Test: none.
- **EVD-DIS-04** — MUST. Combobox: an `escape-key` close requested while items exist but none match the filter and no `Empty` part is rendered MUST have propagation pre-allowed by the component (so the key can close a parent popup) before `onOpenChange` is invoked.
  Source: `combobox/root/AriaCombobox.tsx:764-775`. Test: none.
- **EVD-DIS-05** — MUST. `Combobox.ChipRemove`: the click, Enter or Space that removed a chip MUST have its propagation stopped (so the chip's own handlers do not run) unless `allowPropagation()` was called in `onValueChange`.
  Source: `combobox/chip-remove/ComboboxChipRemove.tsx:102-115`. Test: [L4] `combobox/chip-remove/ComboboxChipRemove.test.tsx` "allows click and keyboard events to propagate when requested" and the preceding test asserting the chip's `onClick` is not called (line 300-305).

### 4.5 FOC — focus consequences of cancelation and reasons

- **EVD-FOC-01** — MUST. When a Shift+Tab (`focus-out`) close of a list popup is canceled, focus MUST remain inside the popup (the return-focus move to the trigger MUST be skipped).
  Source: `floating-ui-react/hooks/useListNavigation.ts:782-789`; `menu/filter-root/useMenuFilterKeyDown.ts:189-191`. Test: [L4] `menu/root/MenuRoot.test.tsx` "keeps focus in the menu when a Shift+Tab close is canceled".
- **EVD-FOC-02** — MUST. A `focus-out` close requested by a trigger focus guard MUST move focus to the next/previous tabbable element after the close request is flushed, regardless of cancelation (the guard's own focus movement is not gated on `isCanceled`).
  Source: `utils/popups/useTriggerFocusGuards.ts:38-55`. Test: none.
- **EVD-FOC-03** — MUST. Return-focus-on-close MUST be suppressed when the close reason is `focus-out` with a focus-guard `trigger`, or `trigger-hover` with a `mouseleave` native event; NavigationMenu additionally blocks return focus for `trigger-hover`, `outside-press` and `focus-out`. The remaining return-focus rules are in `floating-and-positioning.md`.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:796-822`; `navigation-menu/root/NavigationMenuRoot.tsx:33-37`. Test: none in this document's scope.

## 5. Algorithms

Pseudo-code is language-neutral. `details` is the event-details object; `callback(value, details)` is the consumer's change callback.

- **EVD-ALG-01** — MUST. The implementation reproduces the following procedure exactly when creating change-event details.
  Source: `internals/createBaseUIEventDetails.ts:126-157`. Test: [L1] `collapsible/root/CollapsibleRoot.test.tsx` "calls onOpenChange with eventDetails".

```
createChangeEventDetails(reason, nativeEvent?, trigger?, custom?):
  canceled ← false
  propagationAllowed ← false
  details ← {
    reason: reason,
    event: nativeEvent if provided else new Event("base-ui"),
    cancel(): canceled ← true,
    allowPropagation(): propagationAllowed ← true,
    get isCanceled(): return canceled,              // live accessor
    get isPropagationAllowed(): return propagationAllowed,
    trigger: trigger (may be undefined),
    ...custom (component-specific fields; empty object by default)
  }
  return details
```

- **EVD-ALG-02** — MUST. The implementation reproduces the following procedure exactly when creating generic-event details.
  Source: `internals/createBaseUIEventDetails.ts:159-174`. Test: [L1] `internals/createBaseUIEventDetails.spec.ts` (reason literal preserved).

```
createGenericEventDetails(reason, nativeEvent?, custom?):
  return { reason, event: nativeEvent ?? new Event("base-ui"), ...custom }
```

- **EVD-ALG-03** — MUST. The implementation reproduces the following procedure exactly for every cancelable value change that is not a popup open change (Switch, Checkbox, CheckboxGroup, Radio/RadioGroup, Toggle/ToggleGroup, Tabs user change, Collapsible/Accordion, Field.Control, Select value, Combobox values, Menu.RadioGroup, Menu.FilterProvider, Drawer snap point, NavigationMenu value, NumberField, OTPField, Slider).
  Source: `switch/root/SwitchRoot.tsx:183-192`; `checkbox/root/CheckboxRoot.tsx:222-245`; `toggle/Toggle.tsx:82-103`; `select/root/SelectRoot.tsx:330-340`; `number-field/root/NumberFieldRoot.tsx:244-277`; `slider/root/SliderRoot.tsx:193-226`; `otp-field/root/OTPFieldRoot.tsx:228-262`. Test: [L2] `switch/root/SwitchRoot.test.tsx` "does not change state when canceled via a root click"; `toggle/Toggle.test.tsx` "canceling in a grouped Toggle prevents the group value from changing".

```
requestValueChange(nextValue, details):
  // 0. component-specific pre-checks (readOnly/disabled guards, equality guard per EVD-STA-09,
  //    normalization/clamping of nextValue, custom fields assigned onto details)
  // 1. notify (child-level callback first when a part hierarchy is involved)
  callback(nextValue, details)
  if details.isCanceled: return NOT_APPLIED
  // 2. propagate to the parent (group / root) with the SAME details object
  if parentCallback exists:
    parentCallback(derivedParentValue, details)
    if details.isCanceled: return NOT_APPLIED
  // 3. commit (no-op when controlled, per EVD-STA-04)
  commitIfUncontrolled(nextValue)
  // 4. follow-up side effects that assume the change applied
  //    (field touched/dirty/filled, validation, commit callbacks, focus moves, closing a popup, …)
  return APPLIED
```

- **EVD-ALG-04** — MUST. The implementation reproduces the following procedure exactly for every popup open change (Tooltip and PreviewCard through the shared routine; Dialog/AlertDialog/Drawer, Popover and Menu through their own equivalent sequences with the noted differences; Select, Combobox/Autocomplete and NavigationMenu through the simplified value-style sequence of `EVD-ALG-03` plus steps 1a and 3a).
  Source: `utils/popups/popupStoreUtils.ts:246-313`; `dialog/store/DialogStore.ts:72-95`; `popover/store/PopoverStore.ts:110-183`; `menu/root/MenuRoot.tsx:341-445`; `select/root/SelectRoot.tsx:288-315`; `combobox/root/AriaCombobox.tsx:759-852`; `navigation-menu/root/NavigationMenuRoot.tsx:202-243`. Test: [L1] `utils/popups/popupStoreUtils.test.tsx` "runs the full sequence in order when the change is not canceled", "notifies onOpenChange but short-circuits before dispatch when canceled", "maps the change reason to instantType", "merges extraState into the update with `open` always reflecting nextOpen" (L2 for the Menu, Select, Combobox and NavigationMenu root sequences).

```
requestOpenChange(nextOpen, details):
  // 0. store-specific pre-checks
  //    Menu: ignore a close while already closed; ignore identical (open, trigger, reason) request
  //    Combobox: ignore when nextOpen == open; pre-allow Escape propagation when no items match and no Empty part
  //    Dialog: if closing and details.trigger is absent and an active trigger id exists → details.trigger ← active trigger element
  //    Menu: if closing and details.trigger is absent → details.trigger ← active trigger element
  //    Popover: if closing with reason close-press and details.trigger absent → details.trigger ← registered element by active id, else stored element
  // 1a. attach the unmount-prevention capture
  requested ← false
  details.preventUnmountOnClose ← (): requested ← true          // Dialog instead writes the store flag immediately
  // 1b. notify
  onOpenChange(nextOpen, details)
  if details.isCanceled: return
  // 2. store-specific pre-dispatch work (PreviewCard: capture hover coordinates for inline-rect anchoring)
  // 3. internal open-change notification { open: nextOpen, reason, nativeEvent: details.event, nested, triggerElement: details.trigger }
  dispatchInternalOpenChange(nextOpen, details)
  // Menu only: a close with a touch-generated click within 300 ms of a trigger-focus open is dropped here
  // 3a. commit
  state ← createPopupOpenState(state, nextOpen, details.trigger, requested)   // EVD-ALG-05
  state.instantType ← per EVD-EVT-17      // Popover: written in a separate update after the commit; unlisted reasons clear it
  // Popover/Tooltip/PreviewCard/Menu also store details.reason as the last open-change reason
  if details.reason == trigger-hover: commit synchronously (flush) else commit normally
```

- **EVD-ALG-05** — MUST. The implementation reproduces the following procedure exactly when deriving the committed open state from a request.
  Source: `utils/popups/popupStoreUtils.ts:189-226`. Test: [L1] `utils/popups/popupStoreUtils.test.tsx` "getPopupOpenState" › "clears a previous unmount-prevention request when opening", "sets the unmount-prevention request when closing", "preserves the active trigger when closing without a trigger", "records whether an open request carried a trigger", "keeps the trigger-less open flag through a close request"; `popover/close/PopoverClose.test.tsx` "keeps the trigger when closing with a tooltip trigger close button"; `popover/root/PopoverRoot.test.tsx` "preventUnmountOnClose()" › "does not leak from a canceled close into a synchronous second close".

```
createPopupOpenState(state, open, trigger, preventUnmountOnClose):
  preventUnmounting ← state.preventUnmountingOnClose
  if open: preventUnmounting ← false                 // opening starts a new close cycle
  else if preventUnmountOnClose: preventUnmounting ← true
  triggerId ← trigger?.id ?? null
  activeTriggerId ← state.activeTriggerId; activeTriggerElement ← state.activeTriggerElement
  if triggerId != null or open:                      // a close without a trigger keeps the previous one
    activeTriggerId ← triggerId; activeTriggerElement ← trigger ?? null
  return {
    open, preventUnmountingOnClose: preventUnmounting, activeTriggerId, activeTriggerElement,
    openedWithoutTrigger: (open ? trigger == null : state.openedWithoutTrigger)
  }
```

- **EVD-ALG-06** — MUST. The implementation reproduces the following procedure exactly for close completion and unmount prevention (the animation-finished detection itself is `transitions-and-mounting.md`).
  Source: `internals/useUnmountAfterClose.ts:48-129`; `internals/useOpenChangeComplete.tsx:9-28`; `utils/popups/popupStoreUtils.ts:573-608`. Test: [L2] `popover/root/PopoverRoot.test.tsx` "preventUnmountOnClose()" › "unmounts on a normal close after a prevented close and initially open remount" (L4 for the animations-finished steps).

```
on each render:
  preventUnmounting ← open ? false : storedPreventUnmountOnClose
  post-commit, pre-paint, when open became true: storedPreventUnmountOnClose ← false
  if mounted and not open and not preventUnmounting:
    on animations finished (popup element): if still not open → unmount()
unmount():                                   // also invoked by actionsRef.current.unmount()
  if already unmounted: return               // idempotent
  mounted ← false
  clear active trigger, preventUnmountingOnClose ← false
  onOpenChangeComplete(false)
on the Popup part: on animations finished after open became true → onOpenChangeComplete(true)
```

- **EVD-ALG-07** — MUST. The implementation reproduces the following procedure exactly for an Escape keydown reaching a popup (trigger, popup, or document listener).
  Source: `floating-ui-react/hooks/useDismiss.ts:201-232`. Test: [L4] `floating-ui-react/hooks/useDismiss.test.tsx` "calls preventDefault on escape key dismiss", "does not call preventDefault on escape key if close is canceled"; `tooltip/root/TooltipRoot.test.tsx` "allowPropagation() prevents stopPropagation on Escape while still closing".

```
onEscapeKeyDown(keydown):
  if not open or not enabled or escapeKey option disabled or keydown.key != "Escape": return
  if an IME composition is in progress (or ended within the last 0 ms / 5 ms on WebKit): return
  if not bubbles.escapeKey and some open child popup has bubbles.escapeKey == false: return
  details ← createChangeEventDetails(escape-key, keydown)
  requestOpenChange(false, details)                   // EVD-ALG-04
  if not details.isCanceled: keydown.preventDefault()
  if not bubbles.escapeKey and not details.isPropagationAllowed: keydown.stopPropagation()
```

- **EVD-ALG-08** — MUST. The implementation reproduces the following procedure exactly for every controlled/uncontrolled value.
  Source: `packages/utils/src/useControlled.ts:34-93`. Test: [L1] `packages/utils/src/useControlled.test.tsx` "works correctly when is not controlled", "works correctly when is controlled", "warns and falls back to the default when switching from controlled to uncontrolled", "stores a function default value instead of calling it".

```
initialize(controlledProp, defaultProp):
  isControlled ← controlledProp !== undefined          // fixed for the component's lifetime
  internal ← defaultProp                               // stored as-is, even if it is a function
  initialDefault ← defaultProp                         // remembered for the dev warning
on each render:
  value ← (isControlled and controlledProp !== undefined) ? controlledProp : internal
  dev: if isControlled != (controlledProp !== undefined): warn EVD-EDGE-01
  dev: if not isControlled and serialize(defaultProp) != serialize(initialDefault): warn EVD-EDGE-02
set(next):
  if not isControlled: internal ← next (and re-render)
  // controlled: no-op
```

- **EVD-ALG-09** — MUST. The implementation reproduces the following procedure exactly for internal value-changed observation.
  Source: `internals/useValueChanged.ts:6-17`. Test: [L1] `internals/useValueChanged.test.tsx` "retains -0 as the previous value without treating it as a change from 0 (strict: %s)".

```
observeValueChanged(value, onChange):
  previous ← value (at first render)
  post-commit, pre-paint, after each render:
    if previous !== value: onChange(previous)       // strict identity; -0 === 0
    previous ← value
```

- **EVD-ALG-10** — MUST. The implementation reproduces the following procedure exactly when a part merges a consumer-supplied DOM handler with its own handler for the same event (full prop-merging rules in `rendering-and-composition.md`).
  Source: `merge-props/mergeProps.ts:210-263`; `internals/use-button/useButton.ts:116-125, 184-200`. Test: [L1] `merge-props/mergeProps.test.ts` "prevents internal handler if event.preventBaseUIHandler() is called", "does not prevent internal handler if event.preventBaseUIHandler() is not called", "does not automatically prevent handlers that are manually called by getter handlers".

```
mergedHandler(event):
  if event is a DOM (synthetic) event:
    event.preventBaseUIHandler ← (): event.baseUIHandlerPrevented ← true
    result ← consumerHandler(event)                     // consumer runs FIRST
    if not event.baseUIHandlerPrevented: baseUIHandler(event)   // Base UI's handler, which may request a change
    return result
  else:                                                 // custom (non-DOM) events: no prevention
    result ← consumerHandler(event); baseUIHandler(event); return result
// Handlers produced by prop-getter functions are not wrapped: they must test event.baseUIHandlerPrevented themselves.
```

- **EVD-ALG-11** — MUST. The implementation reproduces the following procedure exactly when deriving a highlight reason from an input event.
  Source: `utils/getHighlightReason.ts:8-21`. Test: [L1] `combobox/root/ComboboxRoot.test.tsx` "reports a pointer highlight after keyboard navigation".

```
getHighlightReason(event?):
  if event is absent: return none
  if event.type starts with "key": return keyboard
  if event.type starts with "mouse" or "pointer": return pointer
  return none
```

## 6. Configuration surface used by components

| Option | Type | Default | Meaning |
|---|---|---|---|
| `createChangeEventDetails(reason, event?, trigger?, customProperties?)` | function | — | Builds change-event details. `event` omitted → placeholder `base-ui` event. `trigger` → `details.trigger`. `customProperties` → spread onto the object (§3.7). |
| `createGenericEventDetails(reason, event?, customProperties?)` | function | — | Builds generic-event details; `reason` must be a key of the reason→event map (so `popup-close` cannot be used here). |
| `useClick({ reason })` (FLT) | `'trigger-press' \| 'input-press'` | `'trigger-press'` | Reason reported by the click interaction; Combobox passes `input-press` for its input (`combobox/root/AriaCombobox.tsx:1391`). |
| `useDismiss({ escapeKey, outsidePress, outsidePressEvent, referencePress, bubbles })` (FLT) | see FLT | `escapeKey: true`, `outsidePress: true`, `outsidePressEvent: 'sloppy'`, `referencePress: () => false`, `bubbles: { escapeKey: false, outsidePress: true }` | Which dismissal reasons can be emitted (`escape-key`, `outside-press`, `trigger-press` on reference press) and whether Escape propagation is stopped (`EVD-DIS-01`). Tooltip passes `referencePress: () => closeOnClick` (`tooltip/root/TooltipRoot.tsx:260-263`). |
| `useImplicitActiveTrigger(store, { closeOnActiveTriggerUnmount })` (PSH) | `boolean` | `false` | Whether an active trigger unmount requests a `none` close (Tooltip, PreviewCard pass `true`). |
| `applyPopupOpenChange(store, nextOpen, details, { onBeforeDispatch?, extraState? })` (PSH) | functions / partial state | `{}` | Store-specific hook before the internal notification (PreviewCard) and extra state merged into the commit (Tooltip stores `openChangeReason`). |
| `useControlled({ controlled, default, name, state })` (UTL) | see `EVD-ALG-08` | `state: 'value'` | `name`/`state` only parameterize the dev warnings (`EVD-EDGE-01/02`). |
| `useValueChanged(value, onChange)` (UTL) | — | — | Post-commit, pre-paint change observation (`EVD-ALG-09`). |
| `useOpenChangeComplete({ enabled, open, ref, batch, onComplete })` (TRN) | — | `enabled: true`, `batch: false` | Fires `onComplete` on animations finished for `ref`; popups pass `onComplete` → `onOpenChangeComplete(true)` when open. |
| `useUnmountAfterClose({ open, ref, preventUnmountOnClose, setPreventUnmountOnClose, onUnmount, animateInitialOpen })` (TRN) | — | `animateInitialOpen: false` | Close-completion sequencing (`EVD-ALG-06`); `onUnmount` → `onOpenChangeComplete(false)`. |
| component `onXxx` props | see §3.8 | `undefined` | A missing callback is treated as a callback that neither cancels nor allows propagation. |

## 7. Edge cases and dev warnings

- **EVD-EDGE-01** — MUST. In development builds, if a value's controlled-ness differs from its first-render mode on any later render, the component MUST log an error containing exactly: `A component is changing the {''|'un'}controlled {state} state of {name} to be {'un'|''}controlled.` followed by the lines `Elements should not switch from uncontrolled to controlled (or vice versa).`, `Decide between using a controlled or uncontrolled {name} element for the lifetime of the component.`, `The nature of the state is determined during the first render. It's considered controlled if the value is not \`undefined\`.`, `More info: https://fb.me/react-controlled-components` (joined with newlines; `{state}` defaults to `value`). Component docs cite this ID with their `name`/`state` values.
  Source: `packages/utils/src/useControlled.ts:48-64`. Test: [L2] `packages/utils/src/useControlled.test.tsx` "warns when switching from uncontrolled to controlled", "warns and falls back to the default when switching from controlled to uncontrolled".
- **EVD-EDGE-02** — MUST. In development builds, if an uncontrolled value's default prop changes after the first render (compared by a JSON-like serialization), the component MUST log an error containing exactly: `A component is changing the default {state} state of an uncontrolled {name} after being initialized. To suppress this warning opt to use a controlled {name}.`
  Source: `packages/utils/src/useControlled.ts:66-80`. Test: [L2] `packages/utils/src/useControlled.test.tsx` "warns when changed after initial rendering", "should warn only when defaultValue changes".
- **EVD-EDGE-03** — SHOULD. The default-change comparison of `EVD-EDGE-02` SHOULD serialize functions, framework elements, `bigint` (as `__bigint__:<n>`), `NaN`, arrays and `null` without throwing, SHOULD treat repeated object references as `__object__:<id>`, and SHOULD fall back to `__unserializable__` (never throwing) for values that cannot be serialized.
  Source: `packages/utils/src/useControlled.ts:95-126`. Test: [L1] `packages/utils/src/useControlled.test.tsx` "does not throw when defaultValue has React elements", "does not throw when defaultValue has function", "does not throw when defaultValue has bigint", "does not warn when NaN", "should not fail on null values".
- **EVD-EDGE-04** — MUST. The `popup-close` reason MUST exist in the reason catalogue and in `Menu.FilterProvider.ChangeEventReason` even though it is absent from the reason→native-event map; its `event` is the `base-ui` placeholder.
  Source: `internals/reason-parts.ts:34`; `internals/createBaseUIEventDetails.ts:4-47` (no entry); `filter-dropdown/root/useFilterDropdownCloseQuery.ts:31`. Test: [L2] `menu/filter-root/MenuFilterRoot.test.tsx` "reports popup-close when a controlled close discards the query".
- **EVD-EDGE-05** — MUST. Slider MUST replace `details.event` with a clone of the native event whose `target` is the object `{ value: <new value>, name: <slider name> }` before invoking `onValueChange` (form-library integration); the original event is not mutated.
  Source: `slider/root/SliderRoot.tsx:199-212` (JSDoc `539-541`). Test: none.
- **EVD-EDGE-06** — MUST. NumberField MUST run field validation with the autofilled value even when the autofill `onValueChange` (reason `none`) is canceled.
  Source: `number-field/root/NumberFieldRoot.tsx:505-521`. Test: [L2] `number-field/root/NumberFieldRoot.test.tsx` "validates the autofilled value even when the change is canceled".
- **EVD-EDGE-07** — MUST. `Menu.CheckboxItem.onCheckedChange` and `Menu.RadioGroup.onValueChange` details MUST expose a `preventUnmountOnClose()` member that has no effect.
  Source: `menu/checkbox-item/MenuCheckboxItem.tsx:69-71`; `menu/radio-item/MenuRadioItem.tsx:73-75`. Test: none.
- **EVD-EDGE-08** — MUST. Drawer: after a non-canceled `swipe` close request, if the open state is still `true` on the next frame (a controlled consumer declined), the dismiss animation MUST be reverted and the pending snap point restored with a new `swipe` snap-point change.
  Source: `drawer/viewport/DrawerViewport.tsx:666-692`. Test: none.
- **EVD-EDGE-09** — MUST. Dialog/AlertDialog/Drawer: `preventUnmountOnClose()` writes the store's unmount-prevention flag immediately when called, independently of a later `cancel()` in the same callback; the flag is cleared when the popup next opens.
  Source: `dialog/store/DialogStore.ts:76-78`; `internals/useUnmountAfterClose.ts:71-76`. Test: none (see §10 Q5).
- **EVD-EDGE-10** — MUST. Tabs: an automatic change notification MUST be issued only after the fallback value has been scheduled for commit (`EVD-EVT-10`), so a callback that throws cannot prevent the fallback.
  Source: `tabs/root/TabsRoot.tsx:245-261`. Test: [L2] `tabs/root/TabsRoot.test.tsx` "does not cancel automatic value changes".
- **EVD-EDGE-11** — MUST. Combobox: a pending "highlight after typed open" request MUST be discarded when its own `input-change` open is canceled or when any other open change is not canceled.
  Source: `combobox/root/AriaCombobox.tsx:781-788`. Test: [L2] `combobox/root/ComboboxRoot.test.tsx` "discards the request when an ignored typed open is followed by another open".
- **EVD-EDGE-12** — MUST. Menu: after a `trigger-focus` open, a close requested by a touch-generated `click` (`pointerType === 'touch'`) with a reason other than `item-press` MUST be dropped *after* `onOpenChange` and the internal notification but *before* the commit, for 300 ms; any other open change clears this window.
  Source: `menu/root/MenuRoot.tsx:380-402`. Test: none.
- **EVD-EDGE-13** — SHOULD. Imperative `open`/`close` through a detached handle with no attached root SHOULD log a dev warning and emit nothing (messages owned by `popup-store-and-handles.md`).
  Source: `utils/popups/popupHandle.ts:251-257, 271-285`; `dialog/store/DialogHandle.ts:45-53`. Test: none here.

## 8. React-specific notes and porting considerations

Non-normative.

* **Synthetic vs native events.** React wraps DOM events; every emitter unwraps with `event.nativeEvent` before storing it on the details object (`EVD-API-05`). A port that has no wrapper layer passes its platform event directly. The `preventBaseUIHandler()` / `baseUIHandlerPrevented` members exist only on React's synthetic events passed to consumer props; handlers that Base UI attaches natively (document-level `keydown` in `useDismiss`, `wheel`, pointer-lock listeners, `CloseWatcher`) bypass them — the handbook says "In various cases, native events are used instead of React events, so this method has no effect" (`docs/src/app/(docs)/react/handbook/customization/page.mdx:90`). Port: see decisions/04-js-interop-boundary.md §3 (`details.event` is a C# snapshot record; EVD-API-05's `instanceof` form becomes its `Kind` field) and decisions/02-render-pipeline-and-attribute-merging.md §Decision item 3 (`PreventBaseUIHandler()` / `BaseUIHandlerPrevented()` as `EventArgs` extensions; the cross-part case of EVD-EVT-27 uses a consumed flag on the enclosing part's cascaded context).
* **Live accessors.** `isCanceled` / `isPropagationAllowed` are getters backed by closure variables. Ports must expose them as properties read at call time (not snapshots) because components read them *after* the callback returns, and consumers sometimes read them inside the callback after calling `cancel()` (`accordion/root/AccordionRoot.test.tsx:717-720`). Port: see decisions/05-state-store-and-event-details.md §3 (live `IsCanceled` / `IsPropagationAllowed`).
* **The details object is mutable and shared.** Components assign fields after creation: `trigger` back-fill (`EVD-EVT-16`), `event` replacement (Slider, `EVD-EDGE-05`), `cancel` replaced by a no-op (Drawer close watcher, `EVD-EVT-10`), `preventUnmountOnClose` attached per request (`EVD-ALG-04`), `activationDirection` written by Tabs (`tabs/root/TabsRoot.tsx:113`). One object flows through child and parent callbacks (`EVD-EVT-04`). A port should model the object as a mutable record (or construct it with all fields known up front) rather than an immutable value. Port: see decisions/05-state-store-and-event-details.md §3 (`ChangeEventDetails` mutable class, `OpenChangeEventDetails` and component subclasses, `GenericEventDetails` with `Reason` and `Event` only, one object through chained callbacks).
* **Timing primitives.** "Post-commit, pre-paint" is React's layout-effect phase (`useIsoLayoutEffect`): used by `useValueChanged`, Tabs automatic notifications, Tooltip `disabled` close, `Menu.FilterProvider` `popup-close`, trigger registration. The trigger-unmount close is deferred by `queueMicrotask`. `trigger-hover` commits use `ReactDOM.flushSync`. Drawer's controlled-dismiss check uses one `requestAnimationFrame`. Menu's touch guard uses a 300 ms timer; Popover's and NavigationMenu's "patient click" window after a hover open is 500 ms (`PATIENT_CLICK_THRESHOLD`, owned by the component docs). Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (timing-term mapping) and §Relaxation table (EVD-EVT-18 synchronous hover commit and EVD-STA-07 pre-paint value-changed work are not met on Server).
* **Consumer decisions inside a native dispatch.** `EVD-EVT-11`, `EVD-DIS-01` and `EVD-EVT-26` assume the consumer's callback or handler runs inside the same native event dispatch, so that `cancel()`, `allowPropagation()` or `preventBaseUIHandler()` can still change whether Base UI calls `preventDefault()` / `stopPropagation()` or runs its own key handling. Port: see decisions/04-js-interop-boundary.md §3 (`decide(point, eventInfo, fallbackPolicy)` decision points; fallback policies: prevent and stop for Escape when the owning popup is the innermost open one, run for composite and button key handling) and decisions/01-hosting-model-and-timing.md §Relaxation table (EVD-EVT-11, EVD-DIS-01, EVD-EVT-26: met on WebAssembly, fallback policy on Server).
* **Strict Mode / double effects.** React may run effects twice in development; `useValueChanged` and the Tabs/OTP notification paths are written to be idempotent (the `useValueChanged` test runs under Strict Mode). A port with a single-pass lifecycle does not need the guards but must preserve the "exactly once per change" guarantee (`EVD-EVT-03`).
* **Stable callbacks.** Consumer callbacks are wrapped with `useStableCallback` so the latest callback is invoked even from timers started earlier (hover delays, press-and-hold). Ports must not capture a stale callback reference in deferred work.
* **Type-level only.** `ReasonToEvent`, the per-component `ChangeEventReason` unions, `OpenChangeEventDetails` and the `Omit<…, 'preventUnmountOnClose'>` plumbing are TypeScript constructs. A port should model reasons as a closed set of string constants (an enum whose serialized form is the kebab-case literal) and the per-callback subsets as documented in §4.3.7; the runtime never validates a reason. Port: see decisions/05-state-store-and-event-details.md §3 (one `enum BaseUIEventReason` with the 36 members of §3.5 and a `ToLiteral()` extension; per-callback subsets remain documentation).
* **Placeholder event.** `new Event('base-ui')` requires a DOM `Event` constructor. A non-browser port needs an equivalent inert sentinel whose "type" reads as `base-ui`; tests assert `event instanceof Event` and `event.type === 'base-ui'`. Port: see decisions/04-js-interop-boundary.md §3 (the placeholder is the snapshot record with `Type = "base-ui"`).
* **Controlled state is derived, not stored.** React re-renders on prop changes, so a controlled component "updates" when the consumer re-renders with a new prop. In a port with explicit parameter binding, the equivalent is: the component renders from the bound parameter and never writes to it; the consumer's handler for the change callback writes to its own state, which flows back as the parameter (`EVD-STA-04`). Port: see decisions/05-state-store-and-event-details.md §2 (`Value` / `ValueChanged` / `DefaultValue` plus `OnValueChange(details)`; controlled iff `Value` is supplied at the first parameter set; request sequence of `EVD-ALG-03`/`EVD-ALG-04` with `OnValueChange` called first, a cancel stopping the request, then `ValueChanged`) and §1 (`Controlled<T>` for `EVD-ALG-08`, `EVD-STA-01..06`).
* **Dev-mode warnings** are gated on `process.env.NODE_ENV !== 'production'`; a port should gate them on its own debug configuration. Port: see decisions/05-state-store-and-event-details.md §3 (`EVD-EDGE-01/02` kept behind a debug switch).
* **`useOpenChangeComplete` and `useUnmountAfterClose`** are React effect hooks; their observable contract (`EVD-EVT-20`, `EVD-ALG-06`) is what the port must keep. See `transitions-and-mounting.md` for the animation detection. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (on animations finished: JS watcher notifying C#) and decisions/04-js-interop-boundary.md §1 (transitions module).

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| EVD-API-01 | MUST | Change-event details expose reason, event, trigger, cancel, allowPropagation, isCanceled, isPropagationAllowed (+ custom) | 4.1 |
| EVD-API-02 | MUST | Generic-event details expose only reason, event (+ custom) | 4.1 |
| EVD-API-03 | MUST | `reason` is one of the 36 literals and within the callback's declared subset | 4.1 |
| EVD-API-04 | MUST | Missing native event → placeholder event of type `base-ui` | 4.1 |
| EVD-API-05 | MUST | `event` is the native platform event | 4.1 |
| EVD-API-06 | MUST | `isCanceled` / `isPropagationAllowed` are live accessors, initially false | 4.1 |
| EVD-API-07 | MUST | `trigger` is the initiating element or undefined | 4.1 |
| EVD-API-08 | MUST | Component-specific extension fields present as listed | 4.1 |
| EVD-API-09 | MUST | Popup open-change details expose `preventUnmountOnClose()` | 4.1 |
| EVD-API-10 | MUST | Public type names per component; root exports the two base types | 4.1 |
| EVD-API-11 | MUST | DOM handlers receive `preventBaseUIHandler` / `baseUIHandlerPrevented` | 4.1 |
| EVD-API-12 | SHOULD | Emitters honor the reason → native-event-type table | 4.1 |
| EVD-API-13 | MUST | Callback props have the §3.8 signatures; no extra positional arguments | 4.1 |
| EVD-STA-01 | MUST | Controlled-ness fixed at first render by `prop !== undefined` | 4.2 |
| EVD-STA-02 | MUST | Uncontrolled value initialized from default; later default changes ignored | 4.2 |
| EVD-STA-03 | MUST | Function defaults stored as-is | 4.2 |
| EVD-STA-04 | MUST | Controlled component never commits; renders the prop | 4.2 |
| EVD-STA-05 | MUST | Controlled prop becoming undefined → fall back to initial default | 4.2 |
| EVD-STA-06 | MUST | Change callbacks fire only from interaction/automatic paths, never from prop sync | 4.2 |
| EVD-STA-07 | MUST | Post-commit value-changed side effects on strict-identity change (−0 equals 0) | 4.2 |
| EVD-STA-08 | MUST | Sequence: compute → callback → (if not canceled) commit → side effects → render | 4.2 |
| EVD-STA-09 | MUST | Equal-value firing behavior per table | 4.2 |
| EVD-STA-10 | MUST | Controlled popup declining a close keeps trigger association and flags | 4.2 |
| EVD-EVT-01 | MUST | Callback signature `(value, details)` | 4.3.1 |
| EVD-EVT-02 | MUST | Callback invoked synchronously before state/DOM update | 4.3.1 |
| EVD-EVT-03 | MUST | Exactly one callback per change request | 4.3.1 |
| EVD-EVT-04 | MUST | Chained callbacks share one details object, child first | 4.3.1 |
| EVD-EVT-05 | MUST | `cancel()` prevents the uncontrolled commit | 4.3.2 |
| EVD-EVT-06 | MUST | Controlled: consumer honors `isCanceled` itself | 4.3.2 |
| EVD-EVT-07 | MUST | Canceled popup change stops the whole sequence; order when not canceled | 4.3.2 |
| EVD-EVT-08 | MUST | Canceled change suppresses dependent side effects (list; uncontrolled Field.Control dirty/filled and Select item-press close excepted) | 4.3.2 |
| EVD-EVT-09 | MUST | Cancel earlier in a chain prevents later callbacks | 4.3.2 |
| EVD-EVT-10 | MUST | Automatic changes (Tabs: called after the fallback is scheduled, before it renders; non-cancelable close watcher) are not cancelable | 4.3.2 |
| EVD-EVT-11 | MUST | Escape: `preventDefault` only when not canceled | 4.3.2 |
| EVD-EVT-12 | MUST | Canceled close-watcher close keeps the watcher active | 4.3.2 |
| EVD-EVT-13 | MUST | Trigger-unmount close after a microtask; canceled → keep ownership | 4.3.2 |
| EVD-EVT-14 | MUST | Internal open-change notification after callback, only if not canceled | 4.3.3 |
| EVD-EVT-15 | MUST | Trigger recorded on open; retained on close | 4.3.3 |
| EVD-EVT-16 | MUST | Trigger back-fill on close (Dialog, Menu; Popover close-press) | 4.3.3 |
| EVD-EVT-17 | MUST | Reason → instant-phase mapping (Popover clears it for unlisted reasons) | 4.3.3 |
| EVD-EVT-18 | MUST | `trigger-hover` changes commit synchronously | 4.3.3 |
| EVD-EVT-19 | MUST | Last open-change reason recorded and consulted | 4.3.3 |
| EVD-EVT-20 | MUST | `onOpenChangeComplete` ordering (true on animations finished; false at close completion); NavigationMenu never `true` | 4.3.4 |
| EVD-EVT-21 | MUST | `preventUnmountOnClose()` semantics | 4.3.4 |
| EVD-EVT-22 | MUST | NavigationMenu records the request only on a real close | 4.3.4 |
| EVD-EVT-23 | MUST | `onValueCommitted` carries the change's reason; per-path firing rules (Slider applied-only; NumberField releases always, blur after typing even if unchanged) | 4.3.5 |
| EVD-EVT-24 | MUST | `onItemHighlighted` reason derivation (keyboard / pointer / none; Menu write-back) | 4.3.5 |
| EVD-EVT-25 | MUST | OTP complete/invalid ordering; Form submit details | 4.3.5 |
| EVD-EVT-26 | MUST | `preventBaseUIHandler()` stops Base UI's handler and thus the change callback | 4.3.6 |
| EVD-EVT-27 | MUST | Base UI prevents its own keydown/keyup when converting to a click | 4.3.6 |
| EVD-EVT-28 | MUST | Reason matrix: each callback emits only its listed reasons for the listed actions | 4.3.7 |
| EVD-DIS-01 | MUST | Escape close stops propagation unless allowed / bubbling | 4.4 |
| EVD-DIS-02 | MUST | Only the innermost non-bubbling popup handles Escape | 4.4 |
| EVD-DIS-03 | MUST | Combobox Escape-while-closed clears and stops propagation unless allowed | 4.4 |
| EVD-DIS-04 | MUST | Combobox pre-allows propagation when no items match and no Empty part | 4.4 |
| EVD-DIS-05 | MUST | ChipRemove stops propagation unless allowed | 4.4 |
| EVD-FOC-01 | MUST | Canceled Shift+Tab close keeps focus in the popup | 4.5 |
| EVD-FOC-02 | MUST | Focus-guard close moves focus regardless of cancelation | 4.5 |
| EVD-FOC-03 | MUST | Return focus suppressed for guard focus-out / hover mouseleave; NavigationMenu list | 4.5 |
| EVD-ALG-01 | MUST | createChangeEventDetails | 5 |
| EVD-ALG-02 | MUST | createGenericEventDetails | 5 |
| EVD-ALG-03 | MUST | Cancelable value-change sequence | 5 |
| EVD-ALG-04 | MUST | Popup open-change sequence (shared + variants) | 5 |
| EVD-ALG-05 | MUST | createPopupOpenState | 5 |
| EVD-ALG-06 | MUST | Close completion and unmount prevention | 5 |
| EVD-ALG-07 | MUST | Escape dismissal | 5 |
| EVD-ALG-08 | MUST | Controlled/uncontrolled value | 5 |
| EVD-ALG-09 | MUST | Value-changed observation | 5 |
| EVD-ALG-10 | MUST | Handler merging with prevention | 5 |
| EVD-ALG-11 | MUST | getHighlightReason | 5 |
| EVD-EDGE-01 | MUST | Dev error: controlled/uncontrolled switch (verbatim) | 7 |
| EVD-EDGE-02 | MUST | Dev error: default changed after init (verbatim) | 7 |
| EVD-EDGE-03 | SHOULD | Default-change serialization never throws | 7 |
| EVD-EDGE-04 | MUST | `popup-close` exists despite missing map entry | 7 |
| EVD-EDGE-05 | MUST | Slider clones the event with `target = { value, name }` | 7 |
| EVD-EDGE-06 | MUST | NumberField autofill validates even when canceled | 7 |
| EVD-EDGE-07 | MUST | Menu item details carry a no-op `preventUnmountOnClose` | 7 |
| EVD-EDGE-08 | MUST | Drawer reverts a declined swipe close on the next frame | 7 |
| EVD-EDGE-09 | MUST | Dialog writes the unmount-prevention flag immediately | 7 |
| EVD-EDGE-10 | MUST | Tabs automatic notification after the fallback is scheduled; throwing cannot cancel | 7 |
| EVD-EDGE-11 | MUST | Combobox discards a pending typed-open highlight on cancel / other open | 7 |
| EVD-EDGE-12 | MUST | Menu drops a touch click close within 300 ms of a focus open, post-callback | 7 |
| EVD-EDGE-13 | SHOULD | Handle open/close with no root warns and emits nothing | 7 |

## 10. Open questions

1. **`close-press` declared for Menu but never emitted.** `menu/root/MenuRoot.tsx:933` lists `REASONS.closePress` in `MenuRootChangeEventReason`, yet no menu part creates details with that reason (the only emitters are `dialog/close/DialogClose.tsx:41`, `popover/close/PopoverClose.tsx:44`, `combobox/utils/ComboboxInternalDismissButton.tsx:28`). Treated as *declared only* in §4.3.7; confirm whether a `Menu.Close`-like part is planned or the literal is vestigial.
   Resolved: confirmed by menu.md (MNU-EVT-04, menu.md §16 Q1) — no Menu part emits `close-press`; the literal is kept in the union as *declared only*.
2. **`none` declared for NavigationMenu, Dialog, Popover, Drawer, Select `onValueChange`-shared unions without an emitter.** `navigation-menu/root/NavigationMenuRoot.tsx:448`, `dialog/root/DialogRoot.tsx:105`, `popover/root/PopoverRoot.tsx:217`, `drawer/root/DrawerRoot.tsx:365`. The shared `useImplicitActiveTrigger` path only emits `none` when `closeOnActiveTriggerUnmount` is enabled (Tooltip, PreviewCard). Marked *declared only*.
   Resolved for Dialog, Drawer and Popover: dialog.md §16 Q3, drawer.md §16 Q7 and popover.md (POP-EVT-10) confirm `none` is never emitted for their open changes. NavigationMenu remains open (navigation-menu.md §16 Q5: reason literals mostly untested).
3. **`trigger-focus` declared for Popover and `trigger-press` declared for PreviewCard without an emitter.** Popover wires no focus interaction (`useFocus` call sites: `tooltip/trigger/TooltipTrigger.tsx:166`, `preview-card/trigger/PreviewCardTrigger.tsx:80`, `menu/trigger/MenuTrigger.tsx:205`) and `popover/store/PopoverStore.ts:179-181` only maps `focus-out`; PreviewCard's dismiss is wired without `referencePress` (`preview-card/root/PreviewCardRoot.tsx:98`). Marked *declared only*; a second reader should confirm no other path (e.g. a detached-trigger handle) can produce them.
   Resolved: confirmed by popover.md (POP-EVT-10, §16 Q1 — `trigger-focus` and `none` never emitted) and preview-card.md (PRV-EVT-03 — `trigger-press` never emitted).
4. **`popup-close` is missing from `ReasonToEventMap`** (`internals/createBaseUIEventDetails.ts:4-47` vs `internals/reason-parts.ts:34`), so `createGenericEventDetails` cannot be called with it and `ReasonToEvent<'popup-close'>` is the fallback `Event`. Runtime is unaffected; documented as `EVD-EDGE-04`.
5. **Dialog's immediate `preventUnmountOnClose` write** (`dialog/store/DialogStore.ts:76-78`) differs from the capture-then-apply pattern of the other stores (`utils/popups/popupStoreUtils.ts:228-236, 283-289`). If a consumer calls `preventUnmountOnClose()` and then `cancel()` in the same `onOpenChange`, the Dialog store keeps `preventUnmountingOnClose = true` while remaining open; it is cleared on the next open. The Popover test "does not leak from a canceled close into a synchronous second close" has no Dialog counterpart. Behavior recorded as `EVD-EDGE-09`; confirm whether this is intended.
6. **Select single-mode re-selection.** `select/item/SelectItem.tsx:147-149` requests `setValue(itemValue)` with `item-press` even when the item is already selected, so `onValueChange` fires with an unchanged value; no test asserts either way. Recorded in `EVD-STA-09`.
   Resolved: select.md (SEL-EVT-04, §16 Q2) confirms from the same lines that single-mode activation of the selected item fires `onValueChange` with the unchanged value.
7. **Slider `none` reason.** `slider/root/SliderRoot.tsx:549, 585-590` documents and declares `'none'` ("when the change is triggered without a specific interaction") but no slider emitter uses it; the only `none` in slider tests is Form's `onFormSubmit`. Marked *declared only*.
   Resolved: slider.md (SLD-EVT-02, §16 Q3) confirms `none` is never emitted.
8. **NumberField `CommitEventReason` includes `none`** with the source comment "kept for consistency … never commits with it" (`number-field/root/NumberFieldRoot.tsx:718-728`). Marked *declared only*.
   Resolved: number-field.md §3 (`CommitEventReason`: `none` declared for consistency but never emitted) confirms.
9. **Combobox shared union.** `AriaCombobox.ChangeEventReason` (`combobox/root/AriaCombobox.tsx:2019-2034`) is shared by `onOpenChange`, `onInputValueChange` and `onValueChange`; the per-callback subsets in §4.3.7 are derived from the emitters I could trace, not from distinct type unions. A second reader should re-check the `onInputValueChange` fill path (`AriaCombobox.tsx:866-875`) for reasons I may have missed.
10. **Tooltip's internal `trigger-press` notification without a callback.** `tooltip/store/TooltipStore.ts:94-99` (`cancelPendingOpen`) dispatches an internal open-change notification with reason `trigger-press` and `open: false` *without* calling `onOpenChange`, to cancel a pending hover open. This is not a public callback (not in the matrix) but a port must replicate it for the focus/hover machinery; owned by `floating-and-positioning.md` / the tooltip doc.
11. **Escape + `allowPropagation()` may request the close twice** (popover.md §16 Q7). EVD-ALG-07 runs for both the floating element's `keydown` handler and the same hook's document listener; when `allowPropagation()` skips `stopPropagation()`, the document listener can observe a stale `open === true` and call `onOpenChange(false, …)` a second time with a new details object. Analysed in floating-and-positioning.md §10 Q11; recorded as a suspected defect, not as a requirement.
