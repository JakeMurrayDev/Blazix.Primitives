# Button behavior (press and keyboard activation)

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/internals/use-button/useButton.ts`, `internals/use-button/index.ts`, `utils/useFocusableWhenDisabled.ts`, `utils/dispatchClickWithModifiers.ts`, `internals/usePressAndHold.ts`, `packages/utils/src/useEnhancedClickHandler.ts`, `packages/utils/src/isElementDisabled.ts` |
| Tests | `internals/use-button/useButton.test.tsx`, `button/Button.test.tsx`, `toolbar/button/ToolbarButton.test.tsx`, `number-field/increment/NumberFieldIncrement.test.tsx`, plus the `nativeButton` / `focusableWhenDisabled` cases in component test files cited inline |
| Prefix | `UBT` |
| Status | approved |
| Revised | 2026-10-27 |
| Used by | `components/button.md`, `components/checkbox.md`, `components/switch.md`, `components/radio.md`, `components/toggle.md`, `components/toggle-group.md`, `components/tabs.md`, `components/accordion.md`, `components/collapsible.md`, `components/dialog.md`, `components/alert-dialog.md`, `components/drawer.md`, `components/popover.md`, `components/menu.md`, `components/menubar.md`, `components/context-menu.md`, `components/select.md`, `components/combobox.md`, `components/autocomplete.md`, `components/navigation-menu.md`, `components/toast.md`, `components/toolbar.md`, `components/number-field.md`, `components/slider.md` (touch-like pointer predicate only); `architecture/popup-store-and-handles.md` (interaction-type detection) |

## 1. Purpose

Every Base UI part that "acts as a button" (the public `Button`, every popup `Trigger` and `Close`, `Checkbox.Root`, `Switch.Root`, `Radio.Root`, `Toggle`, `Tabs.Tab`, `Accordion.Trigger`, `Collapsible.Trigger`, `Menu.Item` and its variants, `Select.Item`, `Combobox.Item`, `Combobox.Clear`, `Combobox.ChipRemove`, `Toast.Close`, `Toast.Action`, `Toolbar.Button`, `NumberField.Increment` / `Decrement`, `NavigationMenu.Trigger`) delegates one thing to a shared mechanism: turning an arbitrary element into something that behaves like a button. The mechanism decides

* which attributes make the element *look* like a button to assistive technology and to the browser (`type="button"`, `role="button"`, `tabindex`, `disabled`, `aria-disabled`);
* how <kbd>Enter</kbd> and <kbd>Space</kbd> become clicks when the element is not a real `<button>`, including the differences between standalone buttons, links, and items inside composite widgets (lists, toolbars, menus);
* how a disabled button suppresses interaction while, optionally, staying focusable;
* how clicks synthesized from the keyboard keep the modifier-key state of the key press.

Three smaller helpers travel with it: an "enhanced" click handler that reports *how* a click happened (mouse / touch / pen / keyboard), a press-and-hold repeater used by the number field stepper buttons, and the attribute-only "is this element disabled" predicate shared by list navigation.

A port cannot skip this document: it is the single place where the `nativeButton`, `disabled` and `focusableWhenDisabled` props of two dozen parts get their meaning, and the keyboard tests of nearly every component assume exactly the activation timing specified here (Enter on keydown, Space on keyup, Space on keydown inside composites, one click per key press, modifiers preserved).

## 2. Concepts and terminology

| Base UI / React name | Neutral term used below | Meaning |
|---|---|---|
| `useButton` | **button mechanism** | The shared behavior applied to a button-like part. It takes five options (§6) and returns (a) a *props resolver* that merges button attributes and handlers onto the part's element props and (b) a *DOM handle* that must be attached to the rendered element. |
| `nativeButton` prop / `native` option | **native mode** vs **non-native mode** | A declaration by the consumer of whether the rendered element *is* a `<button>`. In native mode the browser supplies activation (Enter/Space → click, `disabled` semantics, form submission); the mechanism only adds attributes and gating. In non-native mode the mechanism supplies activation itself and uses `role="button"` + `tabindex`. The mode is never inferred from the element at runtime (a development-only check reports mismatches, §7). |
| `composite` option / composite root ancestor | **composite mode** | The element is an item inside a roving-tabindex widget (toolbar, tab list, toggle group, radio group, menu, listbox). Composite mode changes *when* Space activates (keydown instead of keyup) and leaves `tabindex` to the composite machinery (see `composite-and-list-navigation.md`). |
| `disabled` option | **disabled** | The part should ignore user interaction. |
| `focusableWhenDisabled` option | **focusable-when-disabled** | A disabled part keeps its place in the tab order and keeps receiving focus/blur, hover and move events, but no activation. |
| `getButtonProps(externalProps)` | **props resolver** | Function that receives the props accumulated so far for the element (consumer props, the part's own props) and returns the final props. Handlers for `click`, `mousedown`, `pointerdown`, `keydown`, `keyup` found in the input are *wrapped* (called from inside the mechanism's handler, after its disabled gate); all other input props pass through, non-handler props overriding the mechanism's defaults. See `rendering-and-composition.md` for the generic merge rules (`mergeProps`). |
| `buttonRef` | **button DOM handle** | Callback that receives the rendered element. Used for the development check and for the composite `disabled`-attribute correction (UBT-DOM-10). |
| `event.preventBaseUIHandler()` / `event.baseUIHandlerPrevented` | **internal-handler suppression** | A consumer handler may mark an event so the part's own handler for the same event does not run. Defined in `rendering-and-composition.md`; the button mechanism honors it on keydown and keyup and *sets* it itself after dispatching a synthetic click so outer wrappers do not activate twice. |
| `dispatchClickWithModifiers` | **synthetic click** | An untrusted `click` event constructed as a pointer event and dispatched on the element, carrying the modifier keys of the originating keyboard or mouse event. Like `element.click()`, it runs the browser's activation behavior (form submission, link navigation, checkbox toggling). |
| `useEnhancedClickHandler` / `InteractionType` | **enhanced click**, **interaction type** | A pair of `pointerdown` + `click` handlers that call one consumer callback with the event and a classification `'mouse' | 'touch' | 'pen' | 'keyboard' | ''`. |
| `usePressAndHold` | **press-and-hold repeater** | Pointer handlers that run a `tick` callback immediately on press and then repeatedly while the pointer stays down. |
| `isTouchLikePointerType` | **touch-like pointer** | `pointerType === 'touch' || pointerType === 'pen'`. Pen is treated like touch so stylus taps do not force the software keyboard. |
| `isElementDisabled` | **attribute-disabled predicate** | `element == null || element.hasAttribute('disabled') || element.getAttribute('aria-disabled') === 'true'`. |
| text-navigation role | — | A `role` attribute equal to `option`, `gridcell`, or beginning with `menuitem`. In such roles a default-prevented Space keydown means "Space was consumed as typed text" and must not activate the item. |
| activation | — | The consumer-observable `click` event (and the browser's activation behavior that follows it) resulting from a pointer press or a key press. |

Timing constants (all in milliseconds unless stated):

| Name | Value | Where |
|---|---|---|
| press-and-hold start delay (`startDelay`) | 400 | `internals/usePressAndHold.ts:11` |
| press-and-hold tick interval (`tickDelay`) | 60 | `internals/usePressAndHold.ts:10` |
| press-and-hold scroll distance (`scrollDistance`) | 8 px | `internals/usePressAndHold.ts:12` |
| touch settle timeout (`TOUCH_TIMEOUT`) | 50 | `internals/usePressAndHold.ts:13` |
| max pointer moves for a touch to still count as a press (`MAX_POINTER_MOVES_AFTER_TOUCH`) | 3 | `internals/usePressAndHold.ts:14` |

## 3. Public surface

The mechanism itself is internal (`internals/use-button` is not exported from `packages/react/src/index.ts`). What is public is the trio of props every button-like part exposes, whose semantics are defined here and merely *cited* by the component docs.

| Prop (on the part) | Type | Default | Meaning |
|---|---|---|---|
| `nativeButton` | `boolean` | `true` for most parts; `false` for `Checkbox.Root`, `Switch.Root`, `Radio.Root`, `Menu.Item`, `Menu.CheckboxItem`, `Menu.RadioItem`, `Menu.SubmenuTrigger`, `Select.Item`, `Combobox.Item` (and the `ContextMenu` / `Menubar` / `Autocomplete` re-exports of those) | Declares native vs non-native mode (UBT-API-02, UBT-API-03). JSDoc for the `true`-default variant: "Whether the component renders a native `<button>` element when replacing it via the `render` prop. Set to `false` if the rendered element is not a button (for example, `<div>`)." The `false`-default variant ends "Set to `true` if the rendered element is a native button." (`internals/types.ts:63-81`) |
| `disabled` | `boolean` | `false` (parts may also derive it from an ancestor: field, group, toolbar, root) | "Whether the component should ignore user interaction." |
| `focusableWhenDisabled` | `boolean` | `false` on `Button`; `true` on `Toolbar.Button`; fixed (not exposed) on other parts, see §6 | "Whether the button may receive focus even if it is disabled." |

Types referenced by component docs:

| Type | Definition |
|---|---|
| `NativeButtonProps` | `{ nativeButton?: boolean }` with default `true` (`internals/types.ts:63-71`) |
| `NonNativeButtonProps` | `{ nativeButton?: boolean }` with default `false` (`internals/types.ts:73-81`) |
| `InteractionType` | `'mouse' \| 'touch' \| 'pen' \| 'keyboard' \| ''` (`packages/utils/src/useEnhancedClickHandler.ts:4`) — the value of `openMethod` in popup state and the `data-*` interaction attributes documented in `popup-store-and-handles.md` |

The public `Button` component is documented in `components/button.md`; `mergeProps` and `useRender` in `rendering-and-composition.md`.

## 4. Behavioral requirements

### STR — Structure

Not applicable — the mechanism is applied to a single element supplied by the host part; it adds no elements of its own.

### API — Options and defaults

- **UBT-API-01** — MUST. The button mechanism accepts exactly the options `disabled` (default `false`), `focusableWhenDisabled` (default unspecified), `tabIndex` (default `0`), `native` (default `true`) and `composite` (default: inferred, UBT-STA-01); every other configuration arrives through the element props given to the props resolver.
  Source: `internals/use-button/useButton.ts:14-21,264-287`. Test: [L2] `useButton.test.tsx` "returns tabIndex in getButtonProps when host component is BUTTON" (default `tabIndex`), "does not set a type prop" (non-native).
- **UBT-API-02** — MUST. A part's `nativeButton` prop maps one-to-one onto the `native` option, its `disabled` prop (after ancestor merging) onto `disabled`, and its `focusableWhenDisabled` prop, where exposed, onto `focusableWhenDisabled`; the mode is taken from the declaration and never from inspecting the rendered element.
  Source: `button/Button.tsx:27-31`; call sites in §6. Test: [L2] `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation".
- **UBT-API-03** — MUST. `nativeButton` defaults to `true` on every button-like part except `Checkbox.Root`, `Switch.Root`, `Radio.Root`, `Menu.Item`, `Menu.CheckboxItem`, `Menu.RadioItem`, `Menu.SubmenuTrigger`, `Select.Item` and `Combobox.Item`, where it defaults to `false`.
  Source: `checkbox/root/CheckboxRoot.tsx:67`, `switch/root/SwitchRoot.tsx:47`, `radio/root/RadioRoot.tsx:48`, `menu/item/MenuItem.tsx:20`, `menu/checkbox-item/MenuCheckboxItem.tsx:27`, `menu/radio-item/MenuRadioItem.tsx:26`, `menu/submenu-trigger/MenuSubmenuTrigger.tsx:39`, `select/item/SelectItem.tsx:42`, `combobox/item/ComboboxItem.tsx:47`; `true` defaults listed in §6. Test: [L2] `CheckboxRoot.test.tsx` "can render a native button"; `SwitchRoot.test.tsx` "can render a native button". Docs: `components/*/types.md` rows for `nativeButton`.
- **UBT-API-04** — MUST. `Menu.LinkItem` and the combobox's internal dismiss button always use non-native mode (no `nativeButton` prop is exposed).
  Source: `menu/link-item/MenuLinkItem.tsx:41-44`, `combobox/utils/ComboboxInternalDismissButton.tsx:19-21`. Test: none.

### STA — State model

- **UBT-STA-01** — MUST. Composite mode is the explicit `composite` option when one is given; otherwise it is `true` exactly when the element is rendered inside a composite root (the ancestor-provided state of `CompositeRoot`, see `composite-and-list-navigation.md`) and `false` otherwise.
  Source: `internals/use-button/useButton.ts:25-26`. Test: [L2] `useButton.test.tsx` "key: Space fires keydown then click when in composite root context", "`composite=false` keeps keyup activation inside composite root context", "key: Space fires keydown then click on composite buttons" (explicit `true`, no ancestor).
  Port note 2026-10-26 (ADR 04 Amendments 2026-10-26, chosen by the user; M3 tracks 2-3): the port passes `composite` per call to `ButtonBehavior.AddAttributes` and `AfterRenderAsync`. `Toggle` infers it, as Base UI does, from the optional `CompositeRootContext` cascade: `true` in a ToggleGroup and under any other composite root, a bare toggle included (TGL-EDGE-09). `ToolbarButton` (M3 track 4) is always composite. `AccordionTrigger` and the M1 and M2 button-like parts pass an explicit `false`: Radio as Base UI does, Button, CollapsibleTrigger, Switch and Checkbox where Base UI infers, so those four activate Space on keyup when substituted into a composite item. Their retrofit is [issue 107](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/107), after M3 track 4 (Toolbar), whose evidence is an L4 title "Space on a Switch substituted into a toolbar button activates on keydown". The inference is asserted through Toggle by `ToggleTests` "port: under a composite root without a group a toggle is in composite button mode and is not an item", `ToggleGroupTests` "port: a grouped toggle's button twin is composite, and the toggle registers with the group's root" and `toggle-group.spec.ts` "key: Space toggles the pressed state".
- **UBT-STA-02** — MUST. The `disabled` option is the only gate on interaction inside the mechanism; `readOnly` and similar part-level states are folded into `disabled` by the part before calling the mechanism (e.g. `disabled || readOnly` for the number field steppers and `Combobox.ChipRemove`) or handled in the part's own handlers.
  Source: `number-field/root/useNumberFieldStepperButton.ts:177`, `combobox/chip-remove/ComboboxChipRemove.tsx:43-45`. Test: [L2] `ComboboxChipRemove.test.tsx` "should be focusable but not functional when readOnly".

### DOM — Rendered attributes

The complete attribute resolution is given as UBT-ALG-03; the requirements below state the observable results.

- **UBT-DOM-01** — MUST. In native mode the props resolver emits `type="button"`.
  Source: `internals/use-button/useButton.ts:226`. Test: [L2] `Button.test.tsx` conformance (`button: true`); `useButton.test.tsx` "does not set a type prop" (inverse). Docs: `components/button/page.mdx` §Usage guidelines ("`type="submit"` must be specified on Button for it to act as a submit button").
- **UBT-DOM-02** — MUST. In non-native mode the props resolver emits `role="button"` and no `type`.
  Source: `internals/use-button/useButton.ts:226`. Test: [L2] `useButton.test.tsx` "does not set a type prop"; `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation"; `MenuTrigger.test.tsx` "has a role prop inside a Popover when not a native button".
- **UBT-DOM-03** — MUST. Outside composite mode the props resolver always emits `tabindex` equal to the `tabIndex` option (default `0`), in both native and non-native mode, including on a real `<button>`.
  Source: `utils/useFocusableWhenDisabled.ts:30-32`. Test: [L2] `useButton.test.tsx` "returns tabIndex in getButtonProps when host component is BUTTON", "returns tabIndex in getButtonProps when host component is not BUTTON", "returns tabIndex in getButtonProps if it is explicitly provided"; `AccordionTrigger.test.tsx` "keeps a non-native trigger tabbable".
- **UBT-DOM-04** — MUST. Outside composite mode, a disabled non-native element gets `tabindex="-1"` unless `focusableWhenDisabled` is `true`, in which case it keeps the `tabIndex` option value.
  Source: `utils/useFocusableWhenDisabled.ts:33-35`. Test: [L2] `Button.test.tsx` "custom element: applies aria-disabled and is not focusable" (`-1`), "custom element: prevents interactions but remains focusable" (`0`); `SelectTrigger.test.tsx` "is removed from the tab order when disabled and not rendered as a button"; `AccordionTrigger.test.tsx` and `CollapsibleTrigger.test.tsx` "removes a disabled non-native trigger from the tab order".
- **UBT-DOM-05** — MUST. In composite mode the props resolver emits no `tabindex` at all, so the roving `tabindex` supplied by the composite item (`0` for the highlighted item, `-1` otherwise) is the one rendered.
  Source: `utils/useFocusableWhenDisabled.ts:30` (branch skipped when `composite`), `internals/composite/item/useCompositeItem.ts:25`. Test: [L2] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (`tabindex="0"` returns to the first item); `useButton.test.tsx` composite cases pass `tabIndex={0}` explicitly because the mechanism supplies none.
- **UBT-DOM-06** — MUST. In native mode the boolean `disabled` attribute reflects the `disabled` option when `focusableWhenDisabled` is not `true`, and also when the element is a composite item with `focusableWhenDisabled` explicitly `false`; when the option is `false` the attribute is absent.
  Source: `utils/useFocusableWhenDisabled.ts:45-47`. Test: [L2] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable"; `ToolbarButton.test.tsx` "uses the disabled attribute when focusableWhenDisabled is false"; `Toggle.test.tsx` "disables the component"; `AccordionTrigger.test.tsx` / `CollapsibleTrigger.test.tsx` "renders the disabled attribute when disabled"; `useButton.test.tsx` "adds disabled attribute" (server rendering); `ToolbarButton.test.tsx` "disables direct ToggleGroup children when Toolbar.Group is disabled", "skips disabled direct ToggleGroup > Toggle children", "skips a direct Toggle that becomes disabled at runtime" (native composite toggles carry `disabled`; roving skip owned by `components/toolbar.md`).
- **UBT-DOM-07** — MUST. In native mode with `focusableWhenDisabled` `true` (outside composite mode), or in composite mode with `focusableWhenDisabled` `true`, the `disabled` attribute is never emitted, even while disabled.
  Source: `utils/useFocusableWhenDisabled.ts:45-47`. Test: [L2] `Button.test.tsx` "native button: prevents interactions but remains focusable"; `ToolbarButton.test.tsx` "disables the button"; `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default".
- **UBT-DOM-08** — MUST. In non-native mode the props resolver never emits the `disabled` attribute.
  Source: `utils/useFocusableWhenDisabled.ts:45` (guarded by `isNativeButton`). Test: [L2] `Button.test.tsx` "custom element: applies aria-disabled and is not focusable"; `CheckboxRoot.test.tsx` / `SwitchRoot.test.tsx` / `RadioRoot.test.tsx` "uses aria-disabled instead of HTML disabled".
- **UBT-DOM-09** — MUST. Non-handler props present in the input to the props resolver (`type`, `role`, `tabIndex`, `disabled`, `aria-disabled`, and any other attribute) override the values the mechanism would emit; this is how `type="submit"`, `role="menuitem"` / `"tab"` / `"option"`, `tabIndex={-1}` on stepper buttons, and a forwarded `disabled` reach the DOM.
  Source: `internals/use-button/useButton.ts:102-229` (input props merged last), `number-field/root/useNumberFieldStepperButton.ts:120-128,187`, `toolbar/button/ToolbarButton.tsx:71`. Test: [L2] `useButton.test.tsx` "key: Space preserves native submit semantics on composite buttons" (`type: 'submit'`), "does not click composite links when Space is prevented for text navigation" (`role: 'menuitem'`), "arbitrary props" "are passed to the host component"; `ToolbarButton.test.tsx` "renders a menu trigger", "renders a select trigger", "renders a dialog trigger", "renders a popover trigger", "renders toggle and toggle group" (the substituted part's `role` / `aria-*` reach the DOM through the outer resolver).
- **UBT-DOM-10** — MUST. When the element is a real `<button>` in composite mode, `disabled` is `true`, the mechanism itself did *not* emit a `disabled` attribute (UBT-DOM-07), and the element's DOM `disabled` property is nevertheless `true` (set by an inner part or forwarded prop, e.g. `<Toolbar.Button disabled render={<Menu.Trigger />} />`), the mechanism sets the DOM `disabled` property to `false`; this correction runs when the DOM handle receives the element and again whenever `disabled`, composite mode, or the emitted `disabled` value changes, including after the element is replaced.
  Source: `internals/use-button/useButton.ts:68-89,234-237`. Test: [L3] `useButton.test.tsx` "force overrides disabled attribute when put in a composite" (verified before and after a re-mount with a new key); `ToolbarButton.test.tsx` "disabled state" (Menu, Select, Dialog, Popover, Toggle and ToggleGroup blocks: no `disabled` attribute on the substituted element, which stays focusable; the Switch block is cited at UBT-PTR-01).
- **UBT-DOM-11** — MUST. All attributes above are part of the initial (server-rendered) markup, not applied after mount.
  Source: `utils/useFocusableWhenDisabled.ts:20-58` (pure computation). Test: [L2] `useButton.test.tsx` "should server-side render", "adds disabled attribute".
- **UBT-DOM-12** — MUST. `Toolbar.Input` applies the same attribute resolution with `composite: true` and `isNativeButton: false` (yielding `aria-disabled="true"` only, no `tabindex`, no `disabled`) so a disabled toolbar input stays navigable; the input's own `click` and `pointerdown` are default-prevented while disabled.
  Source: `toolbar/input/ToolbarInput.tsx:40-45,53-62`. Test: [L2] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default", "skips a disabled Toolbar.Input with focusableWhenDisabled={false}" (L3 for the default-prevented clause, L4 for the keyboard-navigation citations).
  Port note 2026-10-27 (ADR 04 Amendments 2026-10-26 and 2026-10-27, the `button` row; chosen by the user 2026-10-26): the resolution is `ButtonAttributeWriter.WriteInputItem` and the behaviour the `button` module's `inputItem` option, which keeps UBT-KEY-20's key prevention and the disabled press suppression and skips every key activation, so a space is typed (`ButtonAttributeWriterTests` "port: the input item variant renders only aria-disabled=\"true\", and only while disabled"; `js/button.test.ts` "button module: input item", both forms; `ToolbarInputTests` "port: the input attaches one button twin with the input item option, not native, in composite mode").

### ARIA

- **UBT-ARIA-01** — MUST. In native mode, when `focusableWhenDisabled` is `true`, or when in composite mode with `focusableWhenDisabled` not explicitly `false`, `aria-disabled` is emitted with the stringified boolean value of `disabled` — i.e. `aria-disabled="false"` is present on an *enabled* element in these configurations.
  Source: `utils/useFocusableWhenDisabled.ts:38-43`. Test: [L2] `ToggleGroup.test.tsx` "can disable individual items" (`aria-disabled="false"` on the enabled toggle, `"true"` on the disabled one); `Button.test.tsx` "native button: prevents interactions but remains focusable".
- **UBT-ARIA-02** — MUST. In non-native mode `aria-disabled="true"` is emitted exactly when `disabled` is `true`, and the attribute is absent otherwise.
  Source: `utils/useFocusableWhenDisabled.ts:38-43` (`!isNativeButton && disabled`). Test: [L2] `Button.test.tsx` "custom element: applies aria-disabled and is not focusable"; `RadioGroup.test.tsx` "should have the `aria-disabled` attribute", "should not have the aria attribute when `disabled` is not set"; `MenuRoot.test.tsx` "includes disabled items during keyboard navigation".
- **UBT-ARIA-03** — MUST. In native mode outside composite mode with `focusableWhenDisabled` not `true`, no `aria-disabled` is emitted; the `disabled` attribute alone conveys the state.
  Source: `utils/useFocusableWhenDisabled.ts:38-43`. Test: [L2] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable"; `AccordionTrigger.test.tsx` / `CollapsibleTrigger.test.tsx` "renders the disabled attribute when disabled".
- **UBT-ARIA-04** — MUST. In native composite mode with `focusableWhenDisabled` explicitly `false`, no `aria-disabled` is emitted (the `disabled` attribute is, UBT-DOM-06).
  Source: `utils/useFocusableWhenDisabled.ts:15-16,38-47`. Test: [L2] `ToolbarButton.test.tsx` "uses the disabled attribute when focusableWhenDisabled is false".
- **UBT-ARIA-05** — MUST. In native composite mode with `focusableWhenDisabled` unspecified and `disabled` `true`, both `disabled` and `aria-disabled="true"` are emitted (UBT-DOM-06 and UBT-ARIA-01 both apply).
  Source: `utils/useFocusableWhenDisabled.ts:38-47`. Test: [L2] `ToggleGroup.test.tsx` "can disable the whole group" (asserts `aria-disabled="true"`; the `disabled` attribute follows from UBT-DOM-06).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- UBT-KEY-03 — met on both modes when the veto comes from a consumer JavaScript listener (the `button` module checks `defaultPrevented`); a C# consumer's veto is `PreventBaseUIHandler()`, synchronous on WebAssembly through `decide()`, a round trip late on Server (fallback "run"); Blazor event directives are rejected on parts (ADR 04 Amendments 2026-10-18).
- UBT-KEY-04 — as UBT-KEY-03, for the keyup <kbd>Space</kbd> activation.
- UBT-KEY-06 — (EVD-EVT-26 row, against button key handling) a C# consumer's `PreventBaseUIHandler()` arrives after dispatch ends, so on Server the key is handled regardless (fallback "run"); WebAssembly honours synchronous consumer handlers through `decide()` (ADR 04 §3).

### KEY — Keyboard interaction

The full keydown / keyup decision procedures are UBT-ALG-01 and UBT-ALG-02. "Target is the element" below means the key event's target, after shadow-DOM retargeting, is the element the mechanism is applied to (not a focusable descendant).

Keyboard table (element enabled). "click" means a synthetic click (UBT-EVT-04) that reaches the consumer's `onClick` exactly once.

| Key | Mode | On keydown | On keyup |
|---|---|---|---|
| <kbd>Enter</kbd> | native | not intercepted (browser activates a real `<button>`) | not intercepted |
| <kbd>Enter</kbd> | non-native, not a link | default prevented; click (unless keydown already default-prevented or internal handler suppressed) | nothing |
| <kbd>Enter</kbd> | non-native link (`<a href>`) | not intercepted (browser follows the link) | nothing |
| <kbd>Space</kbd> | native, non-composite | not intercepted | not intercepted (browser clicks) |
| <kbd>Space</kbd> | non-native, non-composite, not a link | default prevented (no page scroll); no click | click unless keyup default-prevented or suppressed |
| <kbd>Space</kbd> | non-native, non-composite link | default prevented | click (link navigates) |
| <kbd>Space</kbd> | composite, non-native (any element incl. links) | default prevented; click — except when keydown was already default-prevented *and* the role is a text-navigation role | nothing |
| <kbd>Space</kbd> | composite, native, element is `<button>` | default prevented; click | default prevented (suppresses the browser's own click) |
| <kbd>Space</kbd> | composite, native, element is not `<button>` (misconfiguration) | default prevented; no click | nothing |
| <kbd>Tab</kbd>, <kbd>Shift</kbd>+<kbd>Tab</kbd> | all | not intercepted | not intercepted |
| any other key | all | not intercepted (consumer `onKeyDown` still runs) | not intercepted |
| any key with <kbd>Shift</kbd>/<kbd>Ctrl</kbd>/<kbd>Alt</kbd>/<kbd>Meta</kbd> | all | same as the unmodified key; the modifier state is copied onto the synthetic click | same |

Keyboard table (element disabled):

| Condition | On keydown | On keyup |
|---|---|---|
| disabled, native, not focusable-when-disabled | element cannot be focused (`disabled` attribute); nothing reaches it | — |
| disabled, non-native, not focusable-when-disabled | element is out of the tab order (`tabindex="-1"`); if focused programmatically: consumer handlers not invoked, no click | consumer handlers not invoked |
| disabled, focusable-when-disabled (`true`) | every key except <kbd>Tab</kbd> is default-prevented; consumer `onKeyDown` not invoked; no click | consumer `onKeyUp` not invoked; no click |
| disabled, composite, `focusableWhenDisabled` unspecified | consumer `onKeyDown` not invoked; no click; default *not* prevented | consumer `onKeyUp` not invoked |

- **UBT-KEY-01** — MUST. In non-native mode, an <kbd>Enter</kbd> keydown whose target is the element, on an element that is not a link, prevents the key's default action and dispatches a synthetic click on the element.
  Source: `internals/use-button/useButton.ts:127-131,155,165-175`. Test: [L3] `useButton.test.tsx` "can be activated with $key key" (2 variants: Enter here, Space at UBT-KEY-02), "key: Enter fires keydown then click on non-native buttons"; `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation"; `ToolbarButton.test.tsx` "custom element: dispatches real clicks from Enter keyboard activation".
- **UBT-KEY-02** — MUST. In non-native, non-composite mode, a <kbd>Space</kbd> keydown whose target is the element (not a link) prevents the default action and dispatches no click; the subsequent <kbd>Space</kbd> keyup whose target is the element dispatches the synthetic click.
  Source: `internals/use-button/useButton.ts:155-175` (keydown), `:207-216` (keyup). Test: [L3] `useButton.test.tsx` "can be activated with Space key", "key: Space fires keyup then click on non-composite buttons"; `RadioGroup.test.tsx` "should select an item with Space on keyup".
- **UBT-KEY-03** — MUST. If the consumer's keydown handler (or any handler that ran before the mechanism) called `preventDefault()`, the <kbd>Enter</kbd> activation of UBT-KEY-01 is canceled (no click), matching a native `<button>`. Server: see ADR 01 relaxation table.
  Source: `internals/use-button/useButton.ts:165-168`. Test: [L4] `useButton.test.tsx` "key: Enter does not click non-native buttons when keydown calls preventDefault"; `RadioRoot.tsx:138-143` relies on this to make Radio ignore Enter (`RadioGroup.test.tsx` "should not select an item with Enter"; `CheckboxRoot.test.tsx` "does not activate with Enter key").
- **UBT-KEY-04** — MUST. If the consumer's keyup handler called `preventDefault()`, the non-composite <kbd>Space</kbd> activation of UBT-KEY-02 is canceled. Server: see ADR 01 relaxation table.
  Source: `internals/use-button/useButton.ts:207-216` (`!event.defaultPrevented`). Test: [L4] `useButton.test.tsx` "key: Space does not click non-native buttons when keyup calls preventDefault".
- **UBT-KEY-05** — SHOULD. No state is carried from keydown to keyup: a default-prevented non-composite <kbd>Space</kbd> *keydown* does not by itself cancel the keyup activation (the source documents this as a known limitation relative to native buttons).
  Source: `internals/use-button/useButton.ts:202-206` (comment), `:166-168` (keydown returns early without recording anything). Test: none.
- **UBT-KEY-06** — MUST. If the consumer's keydown or keyup handler invokes internal-handler suppression (`preventBaseUIHandler()`), the mechanism performs no activation for that event (no click, and for keydown no `preventDefault()` by the mechanism). Server: see ADR 01 relaxation table.
  Source: `internals/use-button/useButton.ts:121-125,198-200`. Test: [L4] `useButton.test.tsx` "does not click composite buttons when keydown calls preventBaseUIHandler", "does not click non-composite buttons when keydown/keyup calls preventBaseUIHandler".
- **UBT-KEY-07** — MUST. Keys other than <kbd>Enter</kbd> and <kbd>Space</kbd> are never intercepted by the mechanism (no `preventDefault()`, no click); the consumer's `onKeyDown` / `onKeyUp` still run when enabled.
  Source: `internals/use-button/useButton.ts:155-163`. Test: [L4] `ComboboxChipRemove.test.tsx` "removes a chip with Space but ignores unrelated keys"; `ToolbarButton.test.tsx` "prevents composite keydowns from escaping" (2 tests; Dialog and AlertDialog blocks: ArrowRight in a popup opened from a toolbar trigger does not move toolbar focus; containment is owned by the popup and composite docs).
- **UBT-KEY-08** — MUST. Keyboard activation (all Enter/Space rules above) applies only when the key event's target is the element itself; a key pressed on a focusable descendant is not intercepted. An event dispatched from inside the element's shadow root counts as targeting the element (retargeting), so <kbd>Enter</kbd> from a focusable shadow child activates.
  Source: `internals/use-button/useButton.ts:127,131,138,159,188,208`. Test: [L3] `useButton.test.tsx` "can be activated with Enter when the keyboard event originates inside a shadow root".
- **UBT-KEY-09** — MUST. In native mode the mechanism performs no synthetic activation on keydown or keyup (the browser's own Enter/Space handling of `<button>` applies), with the single exception of composite Space (UBT-KEY-12).
  Source: `internals/use-button/useButton.ts:131,155` (`isNativeButton` short-circuits). Test: [L3] `useButton.test.tsx` "key: Space fires keydown then click on native buttons in composite root context" (inverse); `Button.test.tsx` conformance; `ToolbarButton.test.tsx` "navigates and selects direct ToggleGroup > Toggle children", "supports multiple selection for direct ToggleGroup > Toggle children", "supports a controlled ToggleGroup value" (<kbd>Enter</kbd> on a native composite toggle activates through the browser).
- **UBT-KEY-10** — MUST. In non-native mode, when the element is an anchor with an `href`, <kbd>Enter</kbd> is not intercepted: no `preventDefault()`, no synthetic click; the browser's native link activation produces the click.
  Source: `internals/use-button/useButton.ts:130-131,155-163` (`isLink` makes `shouldClick` false; Enter falls through). Test: none directly (`TabsTab.test.tsx` "renders as an anchor and toggles selection when `nativeButton` is false" covers pointer activation of anchors). Docs: `components/button/page.mdx` §Rendering links as buttons; `components/tabs/page.mdx` (anchor tabs).
- **UBT-KEY-11** — MUST. In non-native, non-composite mode on an anchor with an `href`, a <kbd>Space</kbd> keydown whose target is the element is default-prevented (no page scroll) and the <kbd>Space</kbd> keyup dispatches a synthetic click, which both reaches the consumer's `onClick` and follows the link.
  Source: `internals/use-button/useButton.ts:156-162` (keydown), `:207-216` (keyup, no link exclusion). Test: [L4] `Button.test.tsx` "custom link element: Space activates the link without scrolling the page".
- **UBT-KEY-12** — MUST. In composite mode, a <kbd>Space</kbd> keydown whose target is the element prevents the default action and, if the mode is non-native or the element is a real `<button>`, dispatches a synthetic click and marks the event as internally handled (suppression flag set); the matching keyup performs no activation.
  Source: `internals/use-button/useButton.ts:138-152`. Test: [L3] `useButton.test.tsx` "key: Space fires keydown then click on composite buttons", "key: Space fires keydown then click on composite links", "key: Space fires keydown then click on native composite buttons", "key: Space fires keydown then click when in composite root context"; `ToolbarButton.test.tsx` "custom element: dispatches real clicks from Space keyboard activation".
- **UBT-KEY-13** — MUST. In composite mode, when the <kbd>Space</kbd> keydown is already default-prevented on arrival and the element's `role` is a text-navigation role (`option`, `gridcell`, or starts with `menuitem`), no click is dispatched (Space was consumed as typed text by typeahead/filtering).
  Source: `internals/use-button/useButton.ts:134-141`. Test: [L3] `useButton.test.tsx` "does not click composite links when Space is prevented for text navigation", "does not click composite gridcells when Space is prevented"; `MenuRoot.test.tsx` "opens a focused submenu trigger with Space when not typing".
- **UBT-KEY-14** — MUST. In composite mode, a <kbd>Space</kbd> keydown that is already default-prevented but whose element has any *other* role (or none) still dispatches the click.
  Source: `internals/use-button/useButton.ts:139-149`. Test: [L3] `useButton.test.tsx` "clicks composite switches when Space is prevented".
- **UBT-KEY-15** — MUST. In native composite mode on a real `<button>`, the <kbd>Space</kbd> keyup whose target is the element is default-prevented so that the browser's own keyup-click does not fire; exactly one click results per Space press. This suppression happens even if the consumer's keyup handler invoked internal-handler suppression.
  Source: `internals/use-button/useButton.ts:182-196`. Test: [L3] `useButton.test.tsx` "does not fire duplicate clicks for Space on native composite buttons", "key: Space fires keydown then click on native composite buttons" (L4 for the exactly-one-click clause).
- **UBT-KEY-16** — MUST. In native composite mode on a real `<button>` that is a submit or reset button, the keydown-dispatched Space click triggers the form's submit / reset exactly once.
  Source: `internals/use-button/useButton.ts:146-149`, `utils/dispatchClickWithModifiers.ts:10-14`. Test: [L4] `useButton.test.tsx` "key: Space preserves native submit semantics on composite buttons", "key: Space preserves native reset semantics on composite buttons".
- **UBT-KEY-17** — MUST. In native composite mode when the element is *not* a `<button>` (a misconfiguration reported by UBT-EDGE-01), <kbd>Space</kbd> keydown is default-prevented but no click is dispatched, and <kbd>Enter</kbd> is not intercepted; the element therefore has no keyboard activation.
  Source: `internals/use-button/useButton.ts:145-149,155`. Test: none.
- **UBT-KEY-18** — MUST. When one props resolver's output is fed into another (nested application, e.g. `<Toolbar.Button render={<Menu.Trigger />} />`), a single key press produces a single click: the inner resolver sets the suppression flag immediately before it dispatches, and the outer one, whose handler invoked the inner one as its consumer handler, returns on seeing it.
  Source: `internals/use-button/useButton.ts:123-125,147,173,214`. Test: [L4] `useButton.test.tsx` "fires a single click for nested non-native composite buttons"; `ToolbarButton.test.tsx` "handles interactions" (7 tests; Switch, Menu, Select, Dialog, AlertDialog, Popover, Toggle and ToggleGroup blocks: one consumer `onClick` per click / <kbd>Enter</kbd> / <kbd>Space</kbd> through a `Toolbar.Button` substituting another button-like part).
- **UBT-KEY-19** — MUST. While `disabled`, the consumer's `onKeyDown` and `onKeyUp` are not invoked and no activation occurs, regardless of mode or focusability.
  Source: `internals/use-button/useButton.ts:117-119,178-180`. Test: [L4] `useButton.test.tsx` "prevents interactions except focus and blur"; `Button.test.tsx` "native button: prevents interactions but remains focusable", "custom element: prevents interactions but remains focusable"; `MenuItem.test.tsx` "can be focused but not interacted with when disabled"; `ComboboxTrigger.test.tsx` "ignores native keydown events on a disabled non-native trigger".
- **UBT-KEY-20** — MUST. While `disabled` with `focusableWhenDisabled` `true`, every keydown whose key is not `Tab` is default-prevented at the element; propagation is not stopped, so ancestor navigation handlers still observe the event.
  Source: `utils/useFocusableWhenDisabled.ts:21-27`. Test: [L3] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (arrow keys still navigate between disabled items); `MenuRoot.test.tsx` "includes disabled items during keyboard navigation".
- **UBT-KEY-21** — MUST. <kbd>Tab</kbd> is never default-prevented by the mechanism, including while disabled and focusable, so keyboard users can always leave the element.
  Source: `utils/useFocusableWhenDisabled.ts:24`. Test: [L3] `useButton.test.tsx` "prevents interactions except focus and blur" (Tab blurs the disabled element).
- **UBT-KEY-22** — MUST. Typing-while-highlighted in filterable menus activates the highlighted item with the same synthetic click (Enter on the filter input dispatches a modifier-preserving click on the active item).
  Source: `menu/filter-root/useMenuFilterKeyDown.ts:177-180`. Test: see `components/menu.md`.

### PTR — Pointer interaction

Press gating (all modes):

- **UBT-PTR-01** — MUST. A `click` on a disabled element is default-prevented by the mechanism and the consumer's `onClick` is not invoked (this also cancels link navigation / form submission on a focusable-when-disabled element that carries no `disabled` attribute).
  Source: `internals/use-button/useButton.ts:104-110`. Test: [L2] `Button.test.tsx` "native button: prevents interactions but remains focusable", "custom element: prevents interactions but remains focusable", "keeps focus and suppresses interactions after becoming disabled"; `Toggle.test.tsx` "disables the component" (programmatic `click()`); `FilterDropdownClear.test.tsx` "ignores a click on a disabled non-native button"; `ToolbarButton.test.tsx` "disabled state" (Switch block: consumer `onClick` not called for click, <kbd>Enter</kbd> or <kbd>Space</kbd>) (L3 for the default-prevented clause, L4 for the navigation/submission clause).
- **UBT-PTR-02** — MUST. A `mousedown` on a disabled element does not invoke the consumer's `onMouseDown`; its default action is *not* prevented by the mechanism.
  Source: `internals/use-button/useButton.ts:111-115`. Test: [L2] `Button.test.tsx` "native button: prevents interactions but remains focusable" (`handleMouseDown` not called) (L3 for the default-action clause).
- **UBT-PTR-03** — MUST. A `pointerdown` on a disabled element is default-prevented and the consumer's `onPointerDown` is not invoked.
  Source: `internals/use-button/useButton.ts:218-224`. Test: [L2] `Button.test.tsx` "custom element: prevents interactions but remains focusable" (`handlePointerDown` not called); `ToolbarButton.test.tsx` "disables the button" (L3 for the default-prevented clause).
- **UBT-PTR-04** — MUST. Handlers other than `click`, `mousedown`, `pointerdown`, `keydown`, `keyup` (e.g. `mousemove`, `mouseenter`, `mouseleave`, `pointerup`, `focus`, `blur`) pass through the props resolver unchanged and keep firing while disabled, so hover-driven affordances such as tooltips keep working on focusable-when-disabled buttons.
  Source: `internals/use-button/useButton.ts:93-100,228`. Test: [L2] `Button.test.tsx` "native button: allows hover handlers while blocking activation"; `ToolbarButton.test.tsx` "allows hover handlers while blocking activation"; `useButton.test.tsx` "prevents interactions except focus and blur" (focus/blur fire).
- **UBT-PTR-05** — MUST. When enabled, the consumer's `onClick`, `onMouseDown` and `onPointerDown` are each invoked exactly once per corresponding event, with the event object.
  Source: `internals/use-button/useButton.ts:109,113,223`. Test: [L2] `useButton.test.tsx` "arbitrary props" / "can be activated with Enter key"; `TabsTab.test.tsx` "renders as an anchor and toggles selection when `nativeButton` is false".

Press-and-hold repeater (`usePressAndHold`; used by `NumberField.Increment` / `NumberField.Decrement`, see `components/number-field.md` for the value semantics of each tick). The full state machine is UBT-ALG-07.

- **UBT-PTR-06** — MUST. A `pointerdown` with the primary button (`event.button === 0`), not default-prevented, not touch-like, while enabled: the default action is prevented, `tick` is called immediately once, and if it returns `true` a repeating sequence starts after `startDelay` (400 ms) firing `tick` every `tickDelay` (60 ms).
  Source: `internals/usePressAndHold.ts:188-202,112-162`. Test: [L3] `NumberFieldIncrement.test.tsx` "stops the hold on release when an ancestor stops pointerup propagation" (immediate tick); `components/number-field.md` for hold tests using `START_AUTO_CHANGE_DELAY` / `CHANGE_VALUE_TICK_DELAY`.
- **UBT-PTR-07** — MUST. `tick` returning `false` (initially or on any repeat) stops the sequence; stopping it this way does not remove the window release listener, so `onStop` still fires once on release.
  Source: `internals/usePressAndHold.ts:150-161,134-137`. Test: [L3] `NumberFieldRoot.test.tsx` "fires once per press-release even after mouseleave/mouseenter during a hold" (release always reports once).
- **UBT-PTR-08** — MUST. A `pointerdown` that is default-prevented, uses a non-primary button, or occurs while disabled starts nothing.
  Source: `internals/usePressAndHold.ts:189-191`. Test: none.
- **UBT-PTR-09** — MUST. A touch-like `pointerdown` does not tick immediately; after `TOUCH_TIMEOUT` (50 ms) the sequence starts (immediate tick, then the 400/60 ms schedule) only if the pointer is still pressed and fewer than 3 `pointermove` events were seen since the press; in that case the compatibility `click` the browser fires after the touch is ignored. Otherwise no sequence starts and the following `click` performs a single action.
  Source: `internals/usePressAndHold.ts:203-222`. Test: [L3] `NumberFieldIncrement.test.tsx` "starts the hold once a touch press settles and ignores the compatibility click", "treats a touch press with several small moves as a tap".
- **UBT-PTR-10** — MUST. During a touch-like press, a `pointermove` whose Euclidean distance from the press point exceeds `scrollDistance` (8 px) cancels the pending or active sequence (treated as scrolling) and resets the move counter; every touch-like `pointermove` while pressed and enabled, whatever its distance, increments the move counter of UBT-PTR-09.
  Source: `internals/usePressAndHold.ts:231-245`. Test: [L3] `NumberFieldIncrement.test.tsx` "does not start the hold when the touch moves like a scroll".
- **UBT-PTR-11** — MUST. A touch-like `pointerup` on the element marks the press as released so a quick tap released before the 50 ms settle does not start a hold.
  Source: `internals/usePressAndHold.ts:224-230`. Test: [L3] `NumberFieldIncrement.test.tsx` "treats a touch press with several small moves as a tap" (release before the hold).
- **UBT-PTR-12** — MUST. Once a sequence has started, a `pointerup` anywhere in the element's window ends the press: the sequence stops and `onStop(pointerupEvent)` is called exactly once per press/release cycle, even if the pointer left and re-entered the element during the hold.
  Source: `internals/usePressAndHold.ts:134-148`. Test: [L3] `NumberFieldRoot.test.tsx` "fires once per press-release even after mouseleave/mouseenter during a hold".
- **UBT-PTR-13** — MUST. A `mouseleave` while not touching stops the repeating sequence; a `mouseenter` while the mouse is still pressed (and not touching, not touch-like) restarts it (immediate tick, then the 400/60 ms schedule).
  Source: `internals/usePressAndHold.ts:246-265`. Test: [L3] `NumberFieldRoot.test.tsx` "fires once per press-release even after mouseleave/mouseenter during a hold".
- **UBT-PTR-14** — MUST. A `mouseup` on the element while not touching stops the sequence, as a fallback for the window `pointerup` listener when an ancestor stops `pointerup` propagation.
  Source: `internals/usePressAndHold.ts:266-274`. Test: [L3] `NumberFieldIncrement.test.tsx` "stops the hold on release when an ancestor stops pointerup propagation".
- **UBT-PTR-15** — MUST. While a sequence is active, `contextmenu` events on the element's window are default-prevented (so a long touch press, even slightly outside the hit area, does not open a context menu); the listener is removed when the sequence stops.
  Source: `internals/usePressAndHold.ts:122-132,108`. Test: none.
- **UBT-PTR-16** — MUST. If `disabled` becomes `true` during a press, the sequence stops and the press, touching and pointer-type state is reset.
  Source: `internals/usePressAndHold.ts:172-179`. Test: none (CHANGELOG v1.9.0 "Stop incrementing when disabled during press-and-hold (#5435)").
- **UBT-PTR-17** — MUST. `shouldSkipClick(clickEvent)` returns `true` when the click is default-prevented; otherwise, for a touch-like press, `true` exactly when a hold sequence started for that press; otherwise `true` exactly when `clickEvent.detail !== 0` (mouse clicks are skipped because `pointerdown` already acted; keyboard-generated clicks with `detail === 0` proceed).
  Source: `internals/usePressAndHold.ts:277-285`. Test: [L3] `NumberFieldIncrement.test.tsx` "starts the hold once a touch press settles and ignores the compatibility click", "treats a touch press with several small moves as a tap"; "should increment by exact step without rounding when snapOnStep is false", "should snap on increment when snapOnStep is true", "should increment with respect to the min value" (each step comes from a `detail === 0` click with no prior `pointerdown`, which proceeds; step values belong to `components/number-field.md`).
- **UBT-PTR-18** — MUST. When the host unmounts, all timers and the window listeners are removed.
  Source: `internals/usePressAndHold.ts:164-170`. Test: none.
- **UBT-PTR-19** — MUST. `touchstart` / `touchend` on the element set and clear a "touching" flag that suppresses the compatibility `mouseenter` / `mouseleave` / `mouseup` logic (UBT-PTR-13, UBT-PTR-14) during touch interaction.
  Source: `internals/usePressAndHold.ts:182-187,251,260-261,269-270`. Test: [L3] `NumberFieldIncrement.test.tsx` touch cases (fire `touchStart` before `pointerDown`).
- **UBT-PTR-20** — MUST. The stepper button's own `pointerdown` handler (which runs before the repeater's) focuses the number field input only for non-touch-like pointers, so a stylus/finger tap does not raise the software keyboard.
  Source: `number-field/root/useNumberFieldStepperButton.ts:153-170`, `internals/usePressAndHold.ts:16-21`. Test: [L4] `NumberFieldIncrement.test.tsx` "places the caret at the end of the input when a mouse press focuses it".

### FOC — Focus

- **UBT-FOC-01** — MUST. A disabled native-mode button that is not focusable-when-disabled is excluded from the tab order and cannot receive focus (consequence of the `disabled` attribute, UBT-DOM-06).
  Source: `utils/useFocusableWhenDisabled.ts:45-47`. Test: [L4] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable"; `SelectTrigger.test.tsx` "cannot be focused when disabled".
- **UBT-FOC-02** — MUST. A disabled non-native-mode element that is not focusable-when-disabled is excluded from the tab order via `tabindex="-1"` (UBT-DOM-04) but remains programmatically focusable.
  Source: `utils/useFocusableWhenDisabled.ts:33-35`. Test: [L4] `Button.test.tsx` "custom element: applies aria-disabled and is not focusable"; `AccordionTrigger.test.tsx` "removes a disabled non-native trigger from the tab order".
- **UBT-FOC-03** — MUST. A focusable-when-disabled element stays in the tab order (`tabindex` unchanged), receives focus via <kbd>Tab</kbd>, and its consumer `onFocus` / `onBlur` fire normally while disabled.
  Source: `utils/useFocusableWhenDisabled.ts:30-35,45-47`. Test: [L4] `useButton.test.tsx` "allows disabled buttons to be focused", "prevents interactions except focus and blur"; `Button.test.tsx` "native button: prevents interactions but remains focusable", "custom element: prevents interactions but remains focusable".
- **UBT-FOC-04** — MUST. When a focused, focusable-when-disabled element becomes disabled (e.g. a button entering a loading state after its own click), focus stays on it and subsequent clicks / Enter / Space do nothing.
  Source: `utils/useFocusableWhenDisabled.ts:45-47`, `internals/use-button/useButton.ts:104-110`. Test: [L4] `Button.test.tsx` "keeps focus and suppresses interactions after becoming disabled". Docs: `components/button/page.mdx` §Loading states.
- **UBT-FOC-05** — MUST. In composite mode, a disabled non-native item keeps its roving `tabindex` and is therefore reachable by arrow-key navigation (and is the Tab stop if highlighted) unless the composite root excludes it; a disabled native composite item with `focusableWhenDisabled` unspecified carries the `disabled` attribute (UBT-ARIA-05) and is not focusable.
  Source: `utils/useFocusableWhenDisabled.ts:30-47`. Test: [L4] `MenuRoot.test.tsx` "includes disabled items during keyboard navigation" (non-native); `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default" (native with `focusableWhenDisabled: true`). See §10 Q2.
- **UBT-FOC-06** — MUST. The mechanism never moves focus itself: it does not focus the element on `pointerdown` or `mousedown`, and it does not blur it when disabled.
  Source: `internals/use-button/useButton.ts:104-224` (no `focus()` / `blur()` calls). Test: [L4] `Button.test.tsx` "keeps focus and suppresses interactions after becoming disabled".

### DIS — Dismissal

Not applicable — the mechanism has no dismissal behavior; popups built on it get dismissal from `floating-and-positioning.md`.

### EVT — Callbacks, ordering and synthetic events

- **UBT-EVT-01** — MUST. The props resolver wraps exactly five handlers from its input — `onClick`, `onMouseDown`, `onKeyDown`, `onKeyUp`, `onPointerDown` — and invokes them from inside its own handler for the same event: for `click`, `mousedown`, `pointerdown` the consumer handler runs only after the disabled gate passes; for `keydown` / `keyup` the consumer handler runs (when enabled) *before* the mechanism's activation logic, so it can cancel activation via `preventDefault()` (UBT-KEY-03/04) or internal-handler suppression (UBT-KEY-06).
  Source: `internals/use-button/useButton.ts:93-100,104-224`. Test: [L2] `useButton.test.tsx` "key: Space fires keyup then click on non-composite buttons" (keydown spy called before click), "does not click non-composite buttons when keydown/keyup calls preventBaseUIHandler" (L4 for the keydown/keyup clause).
- **UBT-EVT-02** — MUST. The focusable-when-disabled key suppression (UBT-KEY-20) runs before the mechanism's own keydown logic for the same event (it is merged as a separate handler that executes first); it never stops propagation.
  Source: `internals/use-button/useButton.ts:102-229` (focusable props merged after the internal handlers; right-to-left handler execution per `rendering-and-composition.md`), `utils/useFocusableWhenDisabled.ts:23-27`. Test: [L3] `ToolbarRoot.test.tsx` "toolbar items can be focused when disabled by default".
- **UBT-EVT-03** — MUST. Because a part passes the props resolver at a specific position in its merge list, every handler merged *before* that position (the consumer's props and the part's own handlers merged earlier) is subject to the disabled gate and the keyboard-activation rules, while handlers merged *after* it are not.
  Source: `button/Button.tsx:37-41` (`[elementProps, getButtonProps]`), `checkbox/root/CheckboxRoot.tsx` (`[ownHandlers, elementProps, otherGroupProps, getButtonProps, getDescriptionProps, validationProps]`), `number-field/root/useNumberFieldStepperButton.ts:184-189` (`[props, elementProps, getButtonProps]`). Test: [L2] `Button.test.tsx` "native button: prevents interactions but remains focusable" (all four consumer handlers gated).
- **UBT-EVT-04** — MUST. A synthetic click is a `PointerEvent` of type `click` created in the element's own window, with `bubbles: true`, `cancelable: true`, `composed: true`, `detail` (default `0`), `pointerType` (default `''`), and `shiftKey`, `ctrlKey`, `altKey`, `metaKey` copied from the source event; it is dispatched on the target element, so capture-phase and bubble-phase listeners on the element, on an element substituted via `render`, and on ancestors all observe one click, and the browser's activation behavior for the element (form submit/reset, link navigation, checkbox toggle) runs.
  Source: `utils/dispatchClickWithModifiers.ts:20-41`. Test: [L3] `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation" (capture, render-element, consumer and ancestor listeners each called once per key), "custom element: keyboard activation clicks carry modifier key state"; `useButton.test.tsx` "key: Space preserves native submit semantics on composite buttons".
- **UBT-EVT-05** — MUST. Keyboard-originated synthetic clicks (UBT-KEY-01/02/11/12/22) use the defaults `detail: 0` and `pointerType: ''`, the native convention for keyboard-generated clicks, so downstream classification (UBT-EVT-07) reports `'keyboard'`.
  Source: `utils/dispatchClickWithModifiers.ts:15-17,23-26`, `internals/use-button/useButton.ts:148,174,215`. Test: [L3] `MenuRoot.test.tsx` "closes with a `detail === 0` click event on keyboard item activation".
- **UBT-EVT-06** — MUST. A synthetic click that represents a mouse gesture (the menu's press-on-trigger, drag, release-on-item activation) is dispatched with `detail: 1` and `pointerType: 'mouse'` so it is not classified as keyboard activation.
  Source: `menu/item/useMenuItemCommonProps.ts:129-141`. Test: [L4] `MenuRoot.test.tsx` "triggers a menu item and closes the menu on click, drag, release" (`detail === 1`).
- **UBT-EVT-07** — MUST. The enhanced click handler classifies a `click` as `'keyboard'` when `event.detail === 0`; otherwise as the click event's own `pointerType` when the event exposes one (Chromium delivers `PointerEvent` clicks); otherwise (Safari, Firefox deliver `MouseEvent` clicks) as the `pointerType` recorded from the most recent non-default-prevented `pointerdown`, or `''` if none; the recorded value is cleared after every click classified on these two pointer branches, and is *not* cleared by a keyboard (`detail === 0`) click, which returns before the reset.
  Source: `packages/utils/src/useEnhancedClickHandler.ts:16-47`. Test: none in this slice (covered by popup `openMethod` tests in `popup-store-and-handles.md`).
- **UBT-EVT-08** — MUST. The enhanced click handler invokes the consumer callback twice per pointer click — once on `pointerdown` (unless the event is default-prevented) with that event and its `pointerType`, and once on `click` with the classification of UBT-EVT-07 — and once per keyboard activation (on `click` only, as `'keyboard'`).
  Source: `packages/utils/src/useEnhancedClickHandler.ts:18-49`. Test: none in this slice.
- **UBT-EVT-09** — MUST. `Checkbox.Root`, `Switch.Root` and `Radio.Root` forward an enabled, non-read-only click on the root to their hidden `<input>` as a synthetic click (after default-preventing the root click), so the change callback's event carries the modifier state of the original click or key press.
  Source: `checkbox/root/CheckboxRoot.tsx:363-375`, `switch/root/SwitchRoot.tsx:143-155`, `radio/root/RadioRoot.tsx:144-154`. Test: [L4] `CheckboxRoot.test.tsx`, `SwitchRoot.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange"; `RadioGroup.test.tsx` "should report keyboard modifier event properties when calling onCheckedChange". (Value semantics belong to `components/checkbox.md`, `components/switch.md`, `components/radio.md`.)
- **UBT-EVT-10** — MUST. The press-and-hold `tick` receives the native event that started the current sequence (the `pointerdown` or `mouseenter` event) on every call, so modifier-dependent step sizes are read from the originating press.
  Source: `internals/usePressAndHold.ts:150-161,202,257`. Test: [L3] `NumberFieldRoot.test.tsx` "should increment the value by the default `largeStep` prop of 10 while holding the shift key".

### FRM — Form integration

- **UBT-FRM-01** — MUST. A native-mode button defaults to `type="button"` and therefore does not submit an enclosing form; a consumer-supplied `type="submit"` or `type="reset"` overrides it (UBT-DOM-09).
  Source: `internals/use-button/useButton.ts:226`. Test: [L2] `useButton.test.tsx` "key: Space preserves native submit semantics on composite buttons". Docs: `components/button/page.mdx` §Usage guidelines.
- **UBT-FRM-02** — MUST. Keyboard-synthesized clicks run the browser's activation behavior exactly once: submit/reset for submit/reset buttons (UBT-KEY-16), navigation for links (UBT-KEY-11), toggling for the hidden inputs of UBT-EVT-09.
  Source: `utils/dispatchClickWithModifiers.ts:10-14`. Test: [L4] `useButton.test.tsx` "key: Space preserves native reset semantics on composite buttons"; `Button.test.tsx` "custom link element: Space activates the link without scrolling the page".

### ANI — Mount / transitions

Not applicable — no lifecycle behavior beyond the dev check and the composite `disabled` correction, both specified above.

### POS — Positioning

Not applicable.

### A11Y — Other accessibility

- **UBT-A11Y-01** — MUST. Keyboard activation is available in non-native mode without any pointer, and a focusable-when-disabled element announces its state via `aria-disabled="true"` while remaining reachable, which is what the "loading state" guidance relies on.
  Source: `utils/useFocusableWhenDisabled.ts:38-43`. Test: [L4] `Button.test.tsx` "native button: prevents interactions but remains focusable". Docs: `components/button/page.mdx` §Loading states (recommends `aria-labelledby` for changing text, a consumer concern).

### I18N

Not applicable — no direction- or locale-dependent behavior.

### EDGE — see §7.

## 5. Algorithms

- **UBT-ALG-01** — MUST. The keydown procedure of the props resolver is reproduced exactly as below (the focusable-when-disabled step runs first as a separate handler).
  Source: `internals/use-button/useButton.ts:116-176`, `utils/useFocusableWhenDisabled.ts:23-27`. Test: [L3] `useButton.test.tsx` "event handlers" block (all keydown cases), `Button.test.tsx` "prop: nativeButton" block.

```
on keydown(event):
  # step 0 — focusable-when-disabled suppression (separate handler, runs first)
  if disabled and focusableWhenDisabled == true and event.key != "Tab":
      event.preventDefault()

  # step 1 — disabled gate
  if disabled: return                                  # consumer onKeyDown NOT called

  # step 2 — consumer handler
  attach suppression API to event
  call consumer.onKeyDown(event) if present
  if event.internalHandlerSuppressed: return

  # step 3 — classify
  isCurrentTarget := (event.target == event.currentTarget)   # after shadow retargeting
  el          := event.currentTarget
  isButton    := el is <button>
  isLink      := not native and el is <a> with non-empty href
  shouldClick := isCurrentTarget and (native ? isButton : not isLink)
  isEnter     := event.key == "Enter"
  isSpace     := event.key == " "
  role        := el.getAttribute("role")
  isTextNav   := role startsWith "menuitem" or role == "option" or role == "gridcell"

  # step 4 — composite Space (keydown activation)
  if isCurrentTarget and composite and isSpace:
      if event.defaultPrevented and isTextNav: return    # Space consumed as typed text
      event.preventDefault()                              # no page scroll
      if not native or isButton:
          event.suppressInternalHandlers()               # outer wrappers must not re-activate
          dispatchSyntheticClick(el, modifiers of event)  # detail 0, pointerType ""
      return

  # step 5 — everything the mechanism leaves to the browser
  if not shouldClick or native or (not isSpace and not isEnter):
      if isCurrentTarget and isLink and isSpace:
          event.preventDefault()                          # link: Space activates on keyup, no scroll
      return

  # step 6 — non-native, non-link, non-composite Enter / Space
  if event.defaultPrevented: return                      # consumer canceled activation
  event.preventDefault()
  if isEnter:
      event.suppressInternalHandlers()
      dispatchSyntheticClick(el, modifiers of event)
  # Space: nothing more here; activation happens on keyup (ALG-02)
```

- **UBT-ALG-02** — MUST. The keyup procedure of the props resolver is reproduced exactly as below.
  Source: `internals/use-button/useButton.ts:177-217`. Test: [L3] `useButton.test.tsx` "key: Space fires keyup then click on non-composite buttons", "does not fire duplicate clicks for Space on native composite buttons", "key: Space does not click non-native buttons when keyup calls preventDefault".

```
on keyup(event):
  if disabled: return                                  # consumer onKeyUp NOT called
  attach suppression API to event
  call consumer.onKeyUp(event) if present

  # native composite <button>: swallow the browser's own Space-keyup click
  if event.target == event.currentTarget and native and composite
     and event.currentTarget is <button> and event.key == " ":
      event.preventDefault()
      return

  if event.internalHandlerSuppressed: return

  # non-native, non-composite Space activation
  if event.target == event.currentTarget and not native and not composite
     and not event.defaultPrevented and event.key == " ":
      event.suppressInternalHandlers()
      dispatchSyntheticClick(event.currentTarget, modifiers of event)
```

- **UBT-ALG-03** — MUST. The attribute resolution for `disabled` / `focusableWhenDisabled` is reproduced exactly as below; `F` is the `focusableWhenDisabled` option and may be `true`, `false` or unspecified, `T` is the `tabIndex` option (default 0), and the resulting matrix is normative.
  Source: `utils/useFocusableWhenDisabled.ts:15-49`. Test: [L1] `Button.test.tsx` "prop: disabled" and "prop: focusableWhenDisabled" blocks; `ToolbarButton.test.tsx` "prop: disabled" block; `ToggleGroup.test.tsx` "prop: disabled" block; `AccordionTrigger.test.tsx` / `CollapsibleTrigger.test.tsx` disabled cases (L2 for the rendered-attribute blocks).

```
resolveFocusableProps(native, disabled, F, composite, T):
  isFocusableComposite    := composite and F != false        # unspecified counts as focusable
  isNonFocusableComposite := composite and F == false
  props := { onKeyDown: <step 0 of ALG-01> }

  if not composite:
      props.tabindex := T
      if not native and disabled:
          props.tabindex := F ? T : -1

  if (native and (F == true or isFocusableComposite)) or (not native and disabled):
      props["aria-disabled"] := disabled                   # rendered as "true" / "false"

  if native and (F != true or isNonFocusableComposite):
      props.disabled := disabled                           # boolean attribute; omitted when false

  return props
```

Resulting matrix (D = disabled):

| native | composite | F | D | `tabindex` | `disabled` attr | `aria-disabled` |
|---|---|---|---|---|---|---|
| yes | no | unspecified/false | false | T | — | — |
| yes | no | unspecified/false | true | T | present | — |
| yes | no | true | false | T | — | `"false"` |
| yes | no | true | true | T | — | `"true"` |
| yes | yes | unspecified | false | (composite) | — | `"false"` |
| yes | yes | unspecified | true | (composite) | present | `"true"` |
| yes | yes | true | any | (composite) | — | `"true"`/`"false"` |
| yes | yes | false | false | (composite) | — | — |
| yes | yes | false | true | (composite) | present | — |
| no | no | unspecified/false | false | T | — | — |
| no | no | unspecified/false | true | `-1` | — | `"true"` |
| no | no | true | false | T | — | — |
| no | no | true | true | T | — | `"true"` |
| no | yes | any | false | (composite) | — | — |
| no | yes | any | true | (composite) | — | `"true"` |

- **UBT-ALG-04** — MUST. The composite DOM `disabled` correction is reproduced exactly as below.
  Source: `internals/use-button/useButton.ts:72-89,234-237`. Test: [L3] `useButton.test.tsx` "force overrides disabled attribute when put in a composite".

```
correctDisabled():
  el := attached element
  if el is not <button>: return
  if composite and disabled and resolvedProps.disabled is unspecified and el.disabled == true:
      el.disabled := false

run correctDisabled() when the DOM handle receives an element,
and again whenever (disabled, resolvedProps.disabled, composite) changes.
```

- **UBT-ALG-05** — MUST. Synthetic click construction is reproduced exactly as below.
  Source: `utils/dispatchClickWithModifiers.ts:20-41`. Test: [L3] `Button.test.tsx` "custom element: keyboard activation clicks carry modifier key state"; `MenuRoot.test.tsx` "closes with a `detail === 0` click event on keyboard item activation", "triggers a menu item and closes the menu on click, drag, release".

```
dispatchSyntheticClick(target, source, detail = 0, pointerType = ""):
  ev := new PointerEvent("click", {           # constructor from target's own window
          bubbles: true, cancelable: true, composed: true,
          detail, pointerType,
          shiftKey: source.shiftKey, ctrlKey: source.ctrlKey,
          altKey: source.altKey, metaKey: source.metaKey })
  target.dispatchEvent(ev)                   # untrusted; still runs activation behavior
```

- **UBT-ALG-06** — MUST. Interaction-type classification is reproduced exactly as below.
  Source: `packages/utils/src/useEnhancedClickHandler.ts:13-50`. Test: none in this slice (exercised through popup `openMethod` tests, see `popup-store-and-handles.md`).

```
state last := ""
on pointerdown(event):
  if event.defaultPrevented: return
  last := event.pointerType
  callback(event, event.pointerType)
on click(event):
  if event.detail == 0: callback(event, "keyboard"); return
  if event has pointerType: callback(event, event.pointerType)
  else:                     callback(event, last)
  last := ""
```

- **UBT-ALG-07** — MUST. The press-and-hold state machine is reproduced exactly as below.
  Source: `internals/usePressAndHold.ts:80-288`. Test: [L3] `NumberFieldIncrement.test.tsx` "press and hold" block (four cases); `NumberFieldRoot.test.tsx` "fires once per press-release even after mouseleave/mouseenter during a hold".

```
state: pressed=false, movesAfterTouch=0, down=(0,0), touching=false,
       ignoreClick=false, pointerType="", ctxMenuUnsub=noop, pointerUpUnsub=noop
timers: settle (one-shot), start (one-shot), repeat (interval)

stop():
  clear settle, start, repeat; ctxMenuUnsub(); movesAfterTouch := 0
  # NOTE: the window pointerup listener is intentionally kept

begin(triggerEvent):
  stop()
  el := anchor element; if none: return
  win := el's window
  ctxMenuUnsub := win.addListener("contextmenu", e => e.preventDefault())
  pointerUpUnsub()                                    # never stack two release listeners
  pointerUpUnsub := win.addListener("pointerup", once, e => {
      pressed := false; stop(); onStop?(e) })
  if not tick(triggerEvent): stop(); return           # immediate first action
  start.after(startDelay=400ms):
      repeat.every(tickDelay=60ms): if not tick(triggerEvent): stop()

on unmount: stop(); pointerUpUnsub()
when disabled becomes true: pressed := false; touching := false; pointerType := ""; stop()

onTouchStart: touching := true
onTouchEnd:   touching := false
onPointerDown(e):
  if e.defaultPrevented or e.button != 0 or disabled: return
  pointerType := e.pointerType; ignoreClick := false; pressed := true; down := (e.clientX, e.clientY)
  if pointerType not in {"touch","pen"}:
      e.preventDefault(); begin(e.native)
  else:
      settle.after(50ms):
          moves := movesAfterTouch; movesAfterTouch := 0
          if pressed and moves < 3: begin(e.native); ignoreClick := true
          else: ignoreClick := false; stop()
onPointerUp(e):   if e.pointerType in {"touch","pen"}: pressed := false
onPointerMove(e):
  if disabled or e.pointerType not in {"touch","pen"} or not pressed: return
  movesAfterTouch += 1
  if (down.x - e.clientX)^2 + (down.y - e.clientY)^2 > scrollDistance^2 (8^2): stop()
onMouseEnter(e):
  if e.defaultPrevented or disabled or not pressed or touching
     or pointerType in {"touch","pen"}: return
  begin(e.native)
onMouseLeave: if touching: return; stop()
onMouseUp:    if touching: return; stop()

shouldSkipClick(click):
  if click.defaultPrevented: return true
  if pointerType in {"touch","pen"}: return ignoreClick
  return click.detail != 0
```

- **UBT-ALG-08** — MUST. The attribute-disabled predicate is `element == null || element.hasAttribute("disabled") || element.getAttribute("aria-disabled") == "true"`; it inspects attributes only (not component state), which is why list-navigation consumers (select, composite root, submenu trigger) can treat any element produced by UBT-ALG-03 uniformly. The composite item uses the equivalent inline check (`item.hasAttribute('disabled') || item.ariaDisabled === 'true'`).
  Source: `packages/utils/src/isElementDisabled.ts:1-7`, `internals/composite/item/useCompositeItem.ts:35`. Test: [L3] `MenuRoot.test.tsx` "skips natively disabled items in text navigation"; `MenuItem.test.tsx` "skips a natively disabled item during keyboard navigation"; `CompositeRoot.test.tsx` "allows navigating items disabled in the DOM when their index is excluded".

## 6. Configuration surface used by components

### 6.1 Button mechanism options

| Option | Type | Default | Meaning |
|---|---|---|---|
| `disabled` | `boolean` | `false` | Gate for click / mousedown / pointerdown / keydown / keyup and for activation. |
| `focusableWhenDisabled` | `boolean \| undefined` | unspecified | `true`: keep tab order, emit `aria-disabled`, suppress all keys but Tab while disabled. `false`: force the `disabled` attribute even in composite mode (native). Unspecified: composite items are treated as focusable for `aria-disabled` purposes (UBT-ALG-03). |
| `tabIndex` | `number` | `0` | Emitted as `tabindex` outside composite mode. No call site in v1.9.0 passes a non-default value; parts that need `-1` pass it as an element prop instead (UBT-DOM-09). |
| `native` | `boolean` | `true` | Native vs non-native mode. |
| `composite` | `boolean \| undefined` | inferred from a composite root ancestor (UBT-STA-01) | Space activates on keydown; `tabindex` left to the composite item. |

### 6.2 Call sites

Every application of the mechanism in v1.9.0 (`grep "useButton("`). "ctx" = inferred from a composite root ancestor. `nativeButton` column is the part's prop default.

| Part | File:line | `native` | `disabled` | `focusableWhenDisabled` | `composite` | Notes |
|---|---|---|---|---|---|---|
| `Button` | `button/Button.tsx:27` | prop (`true`) | prop | prop (`false`) | ctx (none) | |
| `Accordion.Trigger` | `accordion/trigger/AccordionTrigger.tsx:44` | prop (`true`) | item/root disabled | — | ctx (none — **not** composite: `Accordion.Root` renders a `CompositeList` (`accordion/root/AccordionRoot.tsx:121`), not a composite root, so no composite context is found) | non-composite key handling: Space activates on keyup (`AccordionRoot.test.tsx` "opens and closes on Space keyup when rendering $triggerKind triggers") |
| `Collapsible.Trigger` | `collapsible/trigger/CollapsibleTrigger.tsx:44` | prop (`true`) | root disabled | — | ctx | |
| `Checkbox.Root` | `checkbox/root/CheckboxRoot.tsx:129` | prop (`false`) | merged disabled | — | ctx | forwards clicks to hidden input (UBT-EVT-09) |
| `Switch.Root` | `switch/root/SwitchRoot.tsx:107` | prop (`false`) | merged disabled | — | ctx | forwards clicks to hidden input |
| `Radio.Root` | `radio/root/RadioRoot.tsx:176` | prop (`false`) | merged disabled | — | **`false`** (explicit; RadioGroup is a composite root) | Space activates on keyup; Enter canceled via keydown `preventDefault` |
| `Toggle` | `toggle/Toggle.tsx:68` | prop (`true`) | merged disabled | — | ctx (ToggleGroup / Toolbar) | |
| `Tabs.Tab` | `tabs/tab/TabsTab.tsx:111` | prop (`true`) | prop | **`true`** | ctx (Tabs.List) | |
| `Toolbar.Button` | `toolbar/button/ToolbarButton.tsx:42` | prop (`true`) | toolbar/group/prop | prop (**`true`**) | ctx (Toolbar.Root) | forwards `disabled` as element prop only when `render` is given |
| `Toolbar.Input` | `toolbar/input/ToolbarInput.tsx:40` | `isNativeButton: false` | toolbar/group/prop | prop (`true`) | `true` | uses `useFocusableWhenDisabled` directly, not the full mechanism |
| `Dialog.Trigger` / `AlertDialog.Trigger` / `Drawer.Trigger` | `dialog/trigger/DialogTrigger.tsx:63` | prop (`true`) | prop | — | ctx | |
| `Dialog.Close` / `AlertDialog.Close` / `Drawer.Close` | `dialog/close/DialogClose.tsx:32` | prop (`true`) | prop | — | ctx | |
| `Popover.Trigger` | `popover/trigger/PopoverTrigger.tsx:107` | prop (`true`) | prop | — | ctx | |
| `Popover.Close` | `popover/close/PopoverClose.tsx:30` | prop (`true`) | prop | **`false`** (explicit) | ctx | |
| `Menu.Trigger` (also Menubar / ContextMenu re-use) | `menu/trigger/MenuTrigger.tsx:116` | prop (`true`) | prop/root | — | ctx (Menubar is composite) | |
| `Menu.Item`, `Menu.CheckboxItem`, `Menu.RadioItem`, `Menu.SubmenuTrigger` | `menu/item/useMenuItem.ts:29` | prop (`false`) | prop/group | **`true`** | **`true`** | `role="menuitem*"` overrides `role="button"` |
| `Menu.LinkItem` | `menu/link-item/MenuLinkItem.tsx:41` | **`false`** (fixed) | — | — | **`true`** | anchor: Space keydown click, Enter native |
| `Select.Trigger` | `select/trigger/SelectTrigger.tsx:93` | prop (`true`) | prop/root/field | — | ctx | |
| `Select.Item` | `select/item/SelectItem.tsx:120` | prop (`false`) | prop | **`true`** | **`true`** | `role="option"` |
| `Combobox.Trigger` / `Autocomplete.Trigger` | `combobox/trigger/ComboboxTrigger.tsx:129` | prop (`true`) | prop/root/field | — | ctx | |
| `Combobox.Clear` / `Autocomplete.Clear` | `combobox/clear/ComboboxClear.tsx:66` | prop (`true`) | prop/root | — | ctx | |
| `Combobox.ChipRemove` | `combobox/chip-remove/ComboboxChipRemove.tsx:43` | prop (`true`) | `disabled \|\| readOnly` | **`true`** | ctx | |
| `Combobox.Item` / `Autocomplete.Item` | `combobox/item/ComboboxItem.tsx:137` | prop (`false`) | prop | **`true`** | **`true`** | `role="option"` |
| combobox internal dismiss button | `combobox/utils/ComboboxInternalDismissButton.tsx:19` | **`false`** (fixed) | — | — | ctx | |
| `NavigationMenu.Trigger` | `navigation-menu/trigger/NavigationMenuTrigger.tsx:711` | prop (`true`) | prop | **`true`** | ctx (NavigationMenu list is composite) | |
| `Toast.Close` | `toast/close/ToastClose.tsx:33` | prop (`true`) | prop | — | ctx | |
| `Toast.Action` | `toast/action/ToastAction.tsx:32` | prop (`true`) | prop | — | ctx | |
| `NumberField.Increment` / `Decrement` | `number-field/root/useNumberFieldStepperButton.ts:173` | prop (`true`) | `disabled \|\| readOnly \|\| atBoundary` | **`true`** | ctx | element prop `tabIndex: -1`; element prop `disabled` (the effective disabled, excluding `readOnly`) overrides the focusable-when-disabled result per UBT-DOM-09, so a native stepper still renders the `disabled` attribute and is not focusable while disabled (NUM-DOM-10, NUM-FOC-06); press-and-hold (§6.3) |
| `FilterDropdown.Clear` | `filter-dropdown/clear/FilterDropdownClear.tsx:35` | prop (`true`) | prop | — | ctx | out of scope (not exported in 1.9.0) |

### 6.3 Press-and-hold repeater options

| Option | Type | Default | Meaning |
|---|---|---|---|
| `disabled` | `boolean` | required | Stops and resets when it becomes `true` (UBT-PTR-16). |
| `tick(triggerEvent?)` | `(Event?) => boolean` | required | Called once immediately and then on every repeat; return `false` to stop. |
| `onStop(pointerupEvent)` | `(PointerEvent) => void` | — | Called once on the window `pointerup` that ends a started sequence. |
| `tickDelay` | `number` ms | 60 | Repeat interval. |
| `startDelay` | `number` ms | 400 | Delay before the first repeat. |
| `scrollDistance` | `number` px | 8 | Touch movement that cancels the hold. |
| `elementRef` | DOM handle | required | Element whose window receives the `pointerup` / `contextmenu` listeners. The number field passes the *input's* handle, not the button's. |

Returned: `pointerHandlers` (`onTouchStart`, `onTouchEnd`, `onPointerDown`, `onPointerUp`, `onPointerMove`, `onMouseEnter`, `onMouseLeave`, `onMouseUp`) to spread on the button, and `shouldSkipClick(event)` to call at the top of the button's `onClick`.

Consumers: `NumberField.Increment` / `NumberField.Decrement` (`number-field/root/useNumberFieldStepperButton.ts:101-118`, with `tick` stepping the value and `onStop` committing it). `Slider.Control` imports only `isTouchLikePointerType` (`slider/control/SliderControl.tsx:548`).

### 6.4 Enhanced click handler and synthetic click consumers

| Helper | Consumer | Purpose |
|---|---|---|
| `useEnhancedClickHandler` | `utils/useOpenInteractionType.ts:29` (`useOpenMethodTriggerProps`; see `popup-store-and-handles.md`) | Records which input modality opened a popup (`openMethod`), used for focus-return and initial-focus decisions. That consumer substitutes `'touch'` for an empty classification on iOS (`platform.os.ios`) because iOS Safari's hit-slop can fire `mousedown` without `pointerdown`. |
| `useEnhancedClickHandler` | `menu/filter-root/useMenuFilterSubmenuTrigger.ts:47` | Returns focus to the filter input after a keyboard (or mouse-after-trigger-press) click on an open submenu trigger. |
| `dispatchClickWithModifiers` | `useButton` (3 sites) | Keyboard activation (UBT-KEY-01/02/11/12). |
| `dispatchClickWithModifiers` | `CheckboxRoot`, `SwitchRoot`, `RadioRoot` | Root click → hidden input (UBT-EVT-09). |
| `dispatchClickWithModifiers` | `menu/item/useMenuItemCommonProps.ts:137` | Press-drag-release activation with `detail: 1`, `pointerType: 'mouse'` (UBT-EVT-06). |
| `dispatchClickWithModifiers` | `menu/filter-root/useMenuFilterKeyDown.ts:179` | Enter in a filterable menu activates the highlighted item (UBT-KEY-22). |
| `isElementDisabled` | `internals/composite/root/useCompositeRoot.ts:229`, `select/root/SelectRoot.tsx:400`, `menu/submenu-trigger/MenuSubmenuTrigger.tsx:138` | Attribute-only disabled checks (UBT-ALG-08). |

## 7. Edge cases and dev warnings

- **UBT-EDGE-01** — MUST. In development builds, when the mechanism is in native mode and the attached element is not a `<button>`, exactly this message is logged as an error (prefixed `Base UI: ` and followed, when available, by the component owner stack): "A component that acts as a button expected a native <button> because the `nativeButton` prop is true. Rendering a non-<button> removes native button semantics, which can impact forms and accessibility. Use a real <button> in the `render` prop, or set `nativeButton` to `false`."
  Source: `internals/use-button/useButton.ts:36-54`, `packages/utils/src/error.ts:3`, `packages/utils/src/createLogOnce.ts:7-23`. Test: [L2] `useButton.test.tsx` "errors if nativeButton=true but ref is not a button"; `ToolbarButton.test.tsx` "renders a switch", and the Switch block's "handles interactions" / "disabled state" (native `Toolbar.Button` substituting a non-`<button>` `Switch.Root`; logged once per test).
- **UBT-EDGE-02** — MUST. In development builds, when the mechanism is in non-native mode and the attached element is a `<button>`, exactly this message is logged as an error (same prefix/suffix rules): "A component that acts as a button expected a non-<button> because the `nativeButton` prop is false. Rendering a <button> keeps native behavior while Base UI applies non-native attributes and handlers, which can add unintended extra attributes (such as `role` or `aria-disabled`). Use a non-<button> in the `render` prop, or set `nativeButton` to `true`."
  Source: `internals/use-button/useButton.ts:55-64`. Test: [L2] `useButton.test.tsx` "errors if nativeButton=false but ref is a button"; `ToolbarButton.test.tsx` "Select" › "disabled state" (expects the same text via `toErrorDev` when a `Select.Trigger nativeButton={false}` substituted into a `Toolbar.Button` ends up on a `<button>` element).
- **UBT-EDGE-03** — MUST. Each distinct error message is logged at most once per page session (deduplicated on severity + full text), never in production builds, and only when an element is attached; the check re-runs when `native` changes.
  Source: `packages/utils/src/createLogOnce.ts:1-23`, `internals/use-button/useButton.ts:36-66`. Test: [L2] `useButton.test.tsx` "errors if nativeButton=true but ref is not a button" (`toHaveBeenCalledTimes(1)`); production gating: none for this message (the analogous `MenuSubmenuTrigger.test.tsx` "does not inspect rendered disabled elements in production" covers that component's own warning, not this one).
- **UBT-EDGE-04** — MUST. The mechanism never throws; a missing element (DOM handle never attached) disables the dev check and the composite `disabled` correction but leaves attribute and handler output unchanged.
  Source: `internals/use-button/useButton.ts:39-41,75-77`. Test: [L2] `useButton.test.tsx` cases that do not attach `buttonRef` (e.g. "key: Space fires keyup then click on non-composite buttons").
- **UBT-EDGE-05** — MUST. A misconfigured native-mode element that is not a `<button>` renders `type="button"` and `tabindex`, gets no `role`, and has no keyboard activation (UBT-KEY-17); a misconfigured non-native `<button>` gets `role="button"` plus, when disabled, `aria-disabled` and `tabindex="-1"` instead of `disabled`, and goes through the non-native keyboard path: the <kbd>Enter</kbd> / <kbd>Space</kbd> keydown is default-prevented (which, per the source's own native-button model, cancels the browser's activation) and the mechanism dispatches the synthetic click (Enter on keydown, Space on keyup; see §10 Q3).
  Source: `internals/use-button/useButton.ts:129-131,165-175,202-216,226`; `utils/useFocusableWhenDisabled.ts:33-43`. Test: none beyond the dev-error tests.
- **UBT-EDGE-06** — MUST. The mechanism performs no IME / composition guard: a keydown with `key === "Enter"` during text composition is treated as Enter. (Composition guards exist only in text inputs and dismissal, see `floating-and-positioning.md` and `components/combobox.md`.)
  Source: `internals/use-button/useButton.ts:132-133` (no `isComposing` check; confirmed by grep). Test: none.
- **UBT-EDGE-07** — MUST. The mechanism has no platform-specific branches (no user-agent or engine checks); the only platform-dependent behavior in this slice is the iOS fallback inside the popup interaction-type consumer noted in §6.4, owned by `popup-store-and-handles.md`.
  Source: `internals/use-button/useButton.ts` (no `platform` import), `packages/utils/src/useEnhancedClickHandler.ts` (comment only), `utils/useOpenInteractionType.ts:19-23`. Test: none.
- **UBT-EDGE-08** — MUST. In composite mode the mechanism does not emit `tabindex`, so a composite item used *outside* a composite root with explicit `composite: true` (menu/select/combobox items are examples, inside a floating list rather than a composite root) must receive its `tabindex` from the list-navigation layer or as an element prop.
  Source: `utils/useFocusableWhenDisabled.ts:30`; `useButton.test.tsx:316-329` (tests pass `tabIndex={0}`). Test: [L2] `useButton.test.tsx` "key: Space fires keydown then click on composite buttons".

## 8. React-specific notes and porting considerations

Non-normative.

* **Props resolver as a function in a merge list.** React parts pass `getButtonProps` as a *function* element of the `useRenderElement` props array. `mergeProps` calls it with the props merged so far and uses its return value as the new accumulator. The port's equivalent is a "decorate the element's attribute/handler bag" step whose position in the part's pipeline decides what the disabled gate covers (UBT-EVT-03). Handlers to the left of the resolver are wrapped (gated); handlers to the right run first and are not gated. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 3 (chain order substitute, consumer, part) and decisions/04-js-interop-boundary.md §1 (the gate and activation live in the `button` JS module).
* **Handler execution order.** `mergeProps` runs merged handlers right-to-left (rightmost first). Inside the resolver, the focusable-when-disabled `onKeyDown` is merged *after* the mechanism's own `onKeyDown`, hence runs *before* it (UBT-EVT-02). The consumer handlers extracted from the input are not merged; they are invoked explicitly from inside the mechanism's handlers (UBT-EVT-01). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 3.
* **`preventBaseUIHandler` / `baseUIHandlerPrevented`.** These are properties attached to React's synthetic event object by `makeEventPreventable`. The port needs an equivalent per-event flag that (a) a consumer can set from its keydown/keyup handler and (b) the mechanism sets itself after dispatching a synthetic click, so that an enclosing application of the same mechanism (nested parts) sees it. Because React synthetic events are per-dispatch objects, the flag naturally scopes to one dispatch; a port that reuses event objects must reset it. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision 3 (`PreventBaseUIHandler()` on `EventArgs`; the cross-part consumed flag for EVD-EVT-27 replaces the nested-resolver flag of UBT-KEY-18), decisions/04-js-interop-boundary.md §3 (`decide()` with fallback "run" for button key handling, EVD-EVT-26; only synchronously completing consumer handlers can veto) and decisions/01-hosting-model-and-timing.md §Relaxation table (row "EVD-EVT-26 against composite/button key handling": on Server the key is handled regardless of a consumer veto, affecting UBT-KEY-03/04/06).
* **`event.target === event.currentTarget`.** React's synthetic `target` is the retargeted target (shadow-DOM host), which is what makes the shadow-root test pass. A port listening on the element must compare against the composed-path-retargeted target, not `composedPath()[0]`. Port: see decisions/04-js-interop-boundary.md §1 (keydown-to-click synthesis is JavaScript, so the comparison runs on the live DOM event).
* **`aria-disabled={false}`.** React stringifies boolean values of `aria-*` attributes, so `aria-disabled: false` renders as `aria-disabled="false"` (UBT-ARIA-01), whereas `disabled: false` on a `<button>` omits the attribute. A port must reproduce both renderings. Port: see decisions/04-js-interop-boundary.md §2 (these attributes are not in the JS-written list, so C# renders them, including in prerendered markup, UBT-DOM-11).
* **Dev-only effects.** The element-tag check runs in a `useEffect` guarded by `process.env.NODE_ENV !== 'production'`; the owner stack comes from `React.captureOwnerStack` when available (React 19.1+). The composite `disabled` correction runs in a layout effect *and* from the callback ref so it applies before paint and again when the element instance changes (test re-mounts with a new `key`). Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (post-commit, pre-paint mapping) and decisions/04-js-interop-boundary.md §1 (DOM property writes belong to the `button` module).
* **Stable callback ref.** `buttonRef` is a stable function identity (`useStableCallback`) so that parts can put it in a ref array without causing re-attachment every render. The port's DOM-handle registration should likewise tolerate being re-invoked with the same element. Port: see decisions/04-js-interop-boundary.md §3 (`attach(elementOrId, options)` returning a handle with `update`/`dispose`).
* **Timers in the repeater.** `useTimeout` / `useInterval` wrap `setTimeout` / `setInterval` with "clear before start" semantics and automatic clear on unmount; `start` on an already-started timer restarts it. The fake-timer tests advance exactly `50`, `400`, `60`, `60` ms, so the schedule must be `setTimeout(400)` followed by `setInterval(60)` (first repeat at 460 ms from the start of the sequence, measured from the end of the 50 ms touch settle for touch presses). Port: see decisions/04-js-interop-boundary.md §1 (press-and-hold timers live in the `button` JS module) and decisions/06-testing-harness.md §1 L3/L4 and §2 (module tests in jsdom; browser tests under `page.clock`, real Playwright touch/pen input).
* **Synthetic click vs `element.click()`.** `HTMLElement.click()` would also run activation behavior but always reports modifiers as unpressed and `detail` as 0; the mechanism constructs the event explicitly to carry modifiers (CHANGELOG 1.0.0-beta "Preserve modifier key properties in the change event (#3935)", "Handle modifier keys (#1529)"). Port: see decisions/04-js-interop-boundary.md §1 (keydown-to-click synthesis with modifiers is JavaScript).
* **Where `InteractionType` flows.** The enhanced click handler's classification is stored in popup stores (`openMethod`) and consumed by `FloatingFocusManager` and the popup parts; those are documented in `popup-store-and-handles.md` and `floating-and-positioning.md`. This document only fixes the classification algorithm. Port: see decisions/04-js-interop-boundary.md §1 (pointer-type classification is in the `button` JS module).

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| UBT-API-01 | MUST | Five options with stated defaults | 4 API |
| UBT-API-02 | MUST | `nativeButton`/`disabled`/`focusableWhenDisabled` map to options; mode is declared, not detected | 4 API |
| UBT-API-03 | MUST | `nativeButton` default `true` except listed parts (`false`) | 4 API |
| UBT-API-04 | MUST | `Menu.LinkItem` and combobox dismiss button fixed non-native | 4 API |
| UBT-STA-01 | MUST | Composite mode: explicit option else inferred from composite root ancestor | 4 STA |
| UBT-STA-02 | MUST | `disabled` is the only gate; parts fold `readOnly` etc. into it | 4 STA |
| UBT-DOM-01 | MUST | Native mode emits `type="button"` | 4 DOM |
| UBT-DOM-02 | MUST | Non-native mode emits `role="button"`, no `type` | 4 DOM |
| UBT-DOM-03 | MUST | Non-composite always emits `tabindex` = option (default 0) | 4 DOM |
| UBT-DOM-04 | MUST | Non-composite non-native disabled → `tabindex="-1"` unless focusable-when-disabled | 4 DOM |
| UBT-DOM-05 | MUST | Composite mode emits no `tabindex` | 4 DOM |
| UBT-DOM-06 | MUST | Native `disabled` attribute rules | 4 DOM |
| UBT-DOM-07 | MUST | Native focusable-when-disabled never emits `disabled` | 4 DOM |
| UBT-DOM-08 | MUST | Non-native never emits `disabled` | 4 DOM |
| UBT-DOM-09 | MUST | Input non-handler props override mechanism defaults | 4 DOM |
| UBT-DOM-10 | MUST | Composite DOM `disabled` property correction | 4 DOM |
| UBT-DOM-11 | MUST | Attributes present in initial/server markup | 4 DOM |
| UBT-DOM-12 | MUST | `Toolbar.Input` uses the same resolution (non-native, composite) | 4 DOM |
| UBT-ARIA-01 | MUST | Native focusable/composite: `aria-disabled` stringified boolean (incl. `"false"`) | 4 ARIA |
| UBT-ARIA-02 | MUST | Non-native: `aria-disabled="true"` iff disabled | 4 ARIA |
| UBT-ARIA-03 | MUST | Native non-focusable non-composite: no `aria-disabled` | 4 ARIA |
| UBT-ARIA-04 | MUST | Native composite `focusableWhenDisabled=false`: no `aria-disabled` | 4 ARIA |
| UBT-ARIA-05 | MUST | Native composite unspecified + disabled: both attributes | 4 ARIA |
| UBT-KEY-01 | MUST | Non-native Enter keydown → preventDefault + click | 4 KEY |
| UBT-KEY-02 | MUST | Non-native non-composite Space: keydown preventDefault, keyup click | 4 KEY |
| UBT-KEY-03 | MUST | Default-prevented keydown cancels Enter activation | 4 KEY |
| UBT-KEY-04 | MUST | Default-prevented keyup cancels Space activation | 4 KEY |
| UBT-KEY-05 | SHOULD | No keydown→keyup state; prevented Space keydown does not cancel keyup click | 4 KEY |
| UBT-KEY-06 | MUST | Internal-handler suppression in keydown/keyup prevents activation | 4 KEY |
| UBT-KEY-07 | MUST | Other keys never intercepted | 4 KEY |
| UBT-KEY-08 | MUST | Activation only when target is the element (shadow retargeting counts) | 4 KEY |
| UBT-KEY-09 | MUST | Native mode: no synthetic activation except composite Space | 4 KEY |
| UBT-KEY-10 | MUST | Non-native link: Enter not intercepted | 4 KEY |
| UBT-KEY-11 | MUST | Non-native non-composite link: Space keydown preventDefault, keyup click navigates | 4 KEY |
| UBT-KEY-12 | MUST | Composite Space keydown: preventDefault, click, suppression flag; keyup nothing | 4 KEY |
| UBT-KEY-13 | MUST | Composite Space already prevented + text-navigation role → no click | 4 KEY |
| UBT-KEY-14 | MUST | Composite Space already prevented + other role → click | 4 KEY |
| UBT-KEY-15 | MUST | Native composite `<button>`: Space keyup preventDefault, one click per press | 4 KEY |
| UBT-KEY-16 | MUST | Native composite submit/reset via Space keydown click | 4 KEY |
| UBT-KEY-17 | MUST | Native composite non-`<button>`: Space prevented, no click; Enter untouched | 4 KEY |
| UBT-KEY-18 | MUST | Nested application → single click | 4 KEY |
| UBT-KEY-19 | MUST | Disabled: consumer keydown/keyup not invoked, no activation | 4 KEY |
| UBT-KEY-20 | MUST | Disabled + focusable: all keys but Tab default-prevented, propagation kept | 4 KEY |
| UBT-KEY-21 | MUST | Tab never default-prevented | 4 KEY |
| UBT-KEY-22 | MUST | Filterable menu Enter activates highlighted item with synthetic click | 4 KEY |
| UBT-PTR-01 | MUST | Disabled click: preventDefault, consumer not called | 4 PTR |
| UBT-PTR-02 | MUST | Disabled mousedown: consumer not called, default kept | 4 PTR |
| UBT-PTR-03 | MUST | Disabled pointerdown: preventDefault, consumer not called | 4 PTR |
| UBT-PTR-04 | MUST | Other handlers pass through while disabled | 4 PTR |
| UBT-PTR-05 | MUST | Enabled: consumer click/mousedown/pointerdown called once | 4 PTR |
| UBT-PTR-06 | MUST | Hold: primary non-touch pointerdown → preventDefault, immediate tick, 400 ms then every 60 ms | 4 PTR |
| UBT-PTR-07 | MUST | `tick` false stops; release listener kept so `onStop` still fires | 4 PTR |
| UBT-PTR-08 | MUST | Prevented / non-primary / disabled pointerdown starts nothing | 4 PTR |
| UBT-PTR-09 | MUST | Touch: 50 ms settle, <3 moves and still pressed → hold and ignore compat click | 4 PTR |
| UBT-PTR-10 | MUST | Touch move > 8 px cancels; moves counted | 4 PTR |
| UBT-PTR-11 | MUST | Touch pointerup marks released before settle | 4 PTR |
| UBT-PTR-12 | MUST | Window pointerup ends press, `onStop` once per cycle | 4 PTR |
| UBT-PTR-13 | MUST | mouseleave stops, mouseenter while pressed restarts | 4 PTR |
| UBT-PTR-14 | MUST | Local mouseup fallback stops | 4 PTR |
| UBT-PTR-15 | MUST | contextmenu suppressed on window during hold | 4 PTR |
| UBT-PTR-16 | MUST | Becoming disabled stops and resets | 4 PTR |
| UBT-PTR-17 | MUST | `shouldSkipClick` rules | 4 PTR |
| UBT-PTR-18 | MUST | Unmount cleanup | 4 PTR |
| UBT-PTR-19 | MUST | touchstart/touchend flag suppresses compat mouse logic | 4 PTR |
| UBT-PTR-20 | MUST | Stepper focuses input only for non-touch pointers | 4 PTR |
| UBT-FOC-01 | MUST | Native disabled non-focusable: not focusable | 4 FOC |
| UBT-FOC-02 | MUST | Non-native disabled non-focusable: out of tab order, programmatically focusable | 4 FOC |
| UBT-FOC-03 | MUST | Focusable-when-disabled stays in tab order; focus/blur fire | 4 FOC |
| UBT-FOC-04 | MUST | Becoming disabled while focused keeps focus | 4 FOC |
| UBT-FOC-05 | MUST | Composite: non-native disabled keeps roving tabindex; native unspecified not focusable | 4 FOC |
| UBT-FOC-06 | MUST | Mechanism never moves focus | 4 FOC |
| UBT-EVT-01 | MUST | Five wrapped handlers; order relative to gate and activation | 4 EVT |
| UBT-EVT-02 | MUST | Focusable key suppression runs first, no stopPropagation | 4 EVT |
| UBT-EVT-03 | MUST | Resolver position in merge list decides what is gated | 4 EVT |
| UBT-EVT-04 | MUST | Synthetic click event shape and dispatch semantics | 4 EVT |
| UBT-EVT-05 | MUST | Keyboard synthetic clicks: `detail 0`, `pointerType ""` | 4 EVT |
| UBT-EVT-06 | MUST | Mouse-gesture synthetic clicks: `detail 1`, `pointerType "mouse"` | 4 EVT |
| UBT-EVT-07 | MUST | Interaction-type classification | 4 EVT |
| UBT-EVT-08 | MUST | Callback on pointerdown and click; once for keyboard | 4 EVT |
| UBT-EVT-09 | MUST | Checkbox/Switch/Radio forward root click to hidden input with modifiers | 4 EVT |
| UBT-EVT-10 | MUST | `tick` receives the sequence's trigger event | 4 EVT |
| UBT-FRM-01 | MUST | `type="button"` default; `submit`/`reset` override | 4 FRM |
| UBT-FRM-02 | MUST | Synthetic clicks run activation behavior once | 4 FRM |
| UBT-A11Y-01 | MUST | Keyboard activation without pointer; `aria-disabled` while reachable | 4 A11Y |
| UBT-ALG-01 | MUST | Keydown procedure | 5 |
| UBT-ALG-02 | MUST | Keyup procedure | 5 |
| UBT-ALG-03 | MUST | Attribute resolution for disabled/focusable | 5 |
| UBT-ALG-04 | MUST | Composite DOM `disabled` correction | 5 |
| UBT-ALG-05 | MUST | Synthetic click construction | 5 |
| UBT-ALG-06 | MUST | Interaction-type classification | 5 |
| UBT-ALG-07 | MUST | Press-and-hold state machine | 5 |
| UBT-ALG-08 | MUST | Attribute-disabled predicate | 5 |
| UBT-EDGE-01 | MUST | Dev error: native mode on non-`<button>` (verbatim) | 7 |
| UBT-EDGE-02 | MUST | Dev error: non-native mode on `<button>` (verbatim) | 7 |
| UBT-EDGE-03 | MUST | Logged once per message, dev only, re-checked when `native` changes | 7 |
| UBT-EDGE-04 | MUST | Never throws; missing element only disables checks | 7 |
| UBT-EDGE-05 | MUST | Behavior of misconfigured modes | 7 |
| UBT-EDGE-06 | MUST | No IME/composition guard | 7 |
| UBT-EDGE-07 | MUST | No platform branches in this slice | 7 |
| UBT-EDGE-08 | MUST | Explicit `composite: true` outside a composite root gets no `tabindex` | 7 |

## 10. Open questions

1. **`onPointerDown` and text selection.** The assignment anticipated focus-on-press and text-selection handling in the pointer handlers. In v1.9.0 `useButton` only default-prevents `pointerdown` while disabled (`useButton.ts:218-224`) and performs no focus management (UBT-FOC-06); `SELECT_NONE_STYLE` (`user-select: none`) lives in the number field stepper (`useNumberFieldStepperButton.ts:18-21`), not in the mechanism. Whether a port should additionally suppress selection on repeated clicks is a product decision; nothing in the tests requires it.
2. **"Composite items are focusable when disabled by default."** The JSDoc on `useFocusableWhenDisabled` (`utils/useFocusableWhenDisabled.ts:71-75`) states this, but UBT-ALG-03 shows it holds only for non-native items; a native composite item with `focusableWhenDisabled` unspecified gets the `disabled` attribute (UBT-ARIA-05, `Toggle` inside `ToggleGroup`). The tests (`ToggleGroup.test.tsx` "can disable the whole group") assert only `aria-disabled` and do not assert focusability either way. Documented as observed; flag for the toggle-group author.
   Resolved: toggle-group.md TGG-ARIA-05 documents the full grouped-toggle matrix (native: enabled → `aria-disabled="false"`, disabled → `disabled` + `aria-disabled="true"`, not focusable).
3. **Misconfigured non-native `<button>`.** With `native: false` on a real `<button>`, Enter keydown goes through the non-native path and dispatches a synthetic click *and* the browser dispatches its own click on keydown (buttons activate on Enter keydown natively), likely producing two clicks; the source only reports the misconfiguration (UBT-EDGE-02) and no test pins the resulting behavior. Treat as undefined.
   Second-pass note: the non-native path calls `preventDefault()` on the Enter keydown before dispatching (`useButton.ts:170-174`), which cancels a `<button>`'s native Enter activation, so a double click on Enter is unlikely; the residual uncertainty is Space, where the source's own comment (`useButton.ts:203-206`) assumes a prevented keydown cancels native Space activation. UBT-EDGE-05 now states the source path; still untested, so the browser outcome stays open.
4. **`aria-disabled="false"` on enabled elements.** UBT-ARIA-01 is asserted by `ToggleGroup.test.tsx` and follows from `useFocusableWhenDisabled.ts:42`, but the public docs never mention it. If the port's attribute layer drops `false` booleans for `aria-*`, this test translation will fail; confirm with the user whether parity is wanted here.
   Resolved (documentation side): toggle-group.md TGG-ARIA-05 / §16 Q7 and toggle.md TGL-ARIA-02 specify `aria-disabled="false"` on enabled grouped toggles as observed parity.
5. **`usePressAndHold` repeat restart on `mouseenter`.** `onMouseEnter` calls `startAutoChange`, which ticks immediately again (`usePressAndHold.ts:150,257`); dragging out and back in therefore produces an extra immediate step. This matches the test "fires once per press-release even after mouseleave/mouseenter during a hold" only with respect to `onStop`; the extra tick is not asserted. Documented in UBT-PTR-13 as observed.
6. **Shadow-root test is browser-only.** "can be activated with Enter when the keyboard event originates inside a shadow root" is skipped in jsdom; the retargeting rule (UBT-KEY-08) is derived from the real-browser run.
7. **Space keydown then keyup on a non-native element whose keydown handler called `preventBaseUIHandler()`.** The keydown returns before `preventDefault()`, so the page may scroll on Space, and the keyup (a separate event, not suppressed) still dispatches the click unless the consumer also suppresses keyup — the test "does not click non-composite buttons when keydown/keyup calls preventBaseUIHandler" suppresses both. Behavior when only keydown is suppressed is untested; UBT-KEY-06 describes the source as written.
