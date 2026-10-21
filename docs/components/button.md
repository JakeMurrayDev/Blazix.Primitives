# Button

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/button/` |
| Tests | `packages/react/src/button/**/*.test.tsx` (1 file) |
| Docs page | `docs/src/app/(docs)/react/components/button/page.mdx` |
| Prefix | `BTN` |
| Status | implemented |
| Revised | 2026-10-21 |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [button-behavior](../architecture/button-behavior.md) |

## 1. Overview

Button is the public, standalone form of the library's shared button mechanism ([UBT](../architecture/button-behavior.md)). It renders a native `<button>` by default and can be substituted with any other element (`render`) while keeping button semantics: when the consumer declares the substitute is not a `<button>` (`nativeButton={false}`) the component adds `role="button"`, a `tabindex`, and synthesizes clicks from <kbd>Enter</kbd> and <kbd>Space</kbd>. Its second distinguishing feature is `focusableWhenDisabled`: a disabled button that stays in the tab order and keeps focus (used for loading states), announcing its state with `aria-disabled` instead of the `disabled` attribute.

Everything about activation, disabled gating, focusability and the `disabled`/`aria-disabled`/`tabindex` attributes is defined by UBT; this document records only what Button configures (its defaults, the state object, the one data attribute, the merge position of the mechanism) and maps Button's tests to UBT IDs. Button does not emit `onClick` itself, has no change callback, and has no form value; a native Button is `type="button"` unless the consumer passes another `type`.

No component builds on `Button`; other button-like parts (`Toolbar.Button`, popup triggers, `Toggle`, …) apply the same mechanism directly, not through this component.

## 2. Anatomy

```
<Button>               required, 1          default element: button
```

- **BTN-STR-01** — MUST. `Button` MUST render exactly one host element — a `<button>` by default, or the substitute supplied through `render` — and no other elements; it has no sub-parts, requires no ancestor and provides no shared state to descendants.
  Source: `button/Button.tsx:13-42`. Test: [L2] `Button.test.tsx` conformance suite "Base UI component API"; "custom element: applies button semantics and dispatches real clicks from keyboard activation" (`tagName === 'SPAN'`). (L4 for the keyboard-activation citation)

## 3. Public API

### 3.1 `Button`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Ignore user interaction (UBT semantics). Not listed in the API outline (it is a native `<button>` attribute). | BTN-API-01 |
| `focusableWhenDisabled` | `boolean` | `false` | Keep the button focusable and in the tab order while disabled. | BTN-API-02 |
| `nativeButton` | `boolean` | `true` | Declares whether the rendered element is a native `<button>`; selects native vs non-native mode. | BTN-API-03 |
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) §3.0 | — | Standard element customization; functions receive the state object below. | BTN-API-04 |
| `ref` | DOM handle | — | Receives the host element. | BTN-API-04 |
| `type`, `form`, `name`, `value`, `tabIndex`, `role`, `aria-*`, … | native | — | Forwarded; same-named values override the mechanism's defaults (UBT-DOM-09). | BTN-API-05 |
| `onClick`, `onMouseDown`, `onPointerDown`, `onKeyDown`, `onKeyUp` | DOM handlers | — | Forwarded through the mechanism's disabled gate (UBT-EVT-01). | BTN-API-05 |
| *other handlers and attributes* | — | — | Forwarded unchanged (UBT-PTR-04). | BTN-API-05 |

**Consumed (not forwarded to the element):** `disabled`, `focusableWhenDisabled`, `nativeButton`, `className`, `style`, `render`.

- **BTN-API-01** — MUST. `disabled` MUST default to `false` and MUST be passed unchanged as the mechanism's `disabled` option; Button derives no disabled state from any ancestor.
  Source: `button/Button.tsx:20,27-31`, `internals/use-button/useButton.ts:16`; see UBT-API-02. Test: [L2] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable".
- **BTN-API-02** — MUST. `focusableWhenDisabled` MUST default to `false` and MUST always be passed to the mechanism as an explicit boolean (never "unspecified"), so the UBT-ALG-03 rows for `F = false` apply when the prop is omitted.
  Source: `button/Button.tsx:21,29`; see UBT-ALG-03, UBT-API-02. Test: [L2] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable" (omitted → `disabled` attribute), "native button: prevents interactions but remains focusable" (`true`).
- **BTN-API-03** — MUST. `nativeButton` MUST default to `true` and MUST be passed as the mechanism's `native` option; the mode is never inferred from the rendered element.
  Source: `button/Button.tsx:22,30`, `internals/types.ts:63-71`; see UBT-API-02, UBT-API-03. Test: [L2] `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation". Docs: `components/button/page.mdx` §Rendering as another tag. (L4 for the keyboard-activation citation)
- **BTN-API-04** — MUST. `className`, `style`, `render` and `ref` MUST behave per RND-API-01…RND-API-05 with the state object of §3.1 State object; the consumer DOM handle and the mechanism's own DOM handle MUST both receive the host element.
  Source: `button/Button.tsx:37-41`. Test: [L2] `Button.test.tsx` conformance suite "Base UI component API".
- **BTN-API-05** — MUST. Every other parameter MUST be handed to the mechanism's props resolver as its input: the five handlers `onClick`, `onMouseDown`, `onPointerDown`, `onKeyDown`, `onKeyUp` are wrapped by the disabled gate and activation rules (UBT-EVT-01); every non-handler attribute (including `type`, `role`, `tabIndex`, `aria-disabled`) overrides the value the mechanism would emit (UBT-DOM-09); all other handlers pass through unchanged (UBT-PTR-04).
  Source: `button/Button.tsx:17-25,40`, `internals/use-button/useButton.ts:93-100,102-229`. Test: [L2] `Button.test.tsx` "native button: prevents interactions but remains focusable" (all four gated handlers), "native button: allows hover handlers while blocking activation". (L4 for the disabled-gate clause)

#### State object

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | The `disabled` prop (after its default). |

- **BTN-STA-01** — MUST. The state object exposed to `className`/`style`/`render` MUST contain exactly the field `disabled`, equal to the `disabled` prop (default `false`); `focusableWhenDisabled` and `nativeButton` do not affect it.
  Source: `button/Button.tsx:33-35,44-49`. Test: none (indirectly: `data-disabled` assertions in "prop: disabled" and "prop: focusableWhenDisabled").

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `""` while `disabled` is `true` (in every mode, including `focusableWhenDisabled`) | BTN-DOM-01 |

#### CSS variables

None.

### 3.2 Shared types

- `Button.Props` (`ButtonProps`) — `NativeButtonProps` (`nativeButton`, default `true`) plus the common parameters for a `<button>` default element plus `focusableWhenDisabled`. Source: `button/Button.tsx:51-58`, `internals/types.ts:63-71`.
- `Button.State` (`ButtonState`) — `{ disabled: boolean }`. Source: `button/Button.tsx:44-49`.
- `ButtonDataAttributes` — exported constant namespace with the single member `disabled = 'data-disabled'`. Source: `button/ButtonDataAttributes.ts:1-4`, `button/index.ts:5`.

No callback types exist; Button has no event-details callbacks (see [EVD](../architecture/event-details-and-reasons.md) §3.6).

## 4. State model

Button owns no state. The state object echoes the `disabled` prop (BTN-STA-01). Native vs non-native mode, composite mode (UBT-STA-01) and focusable-when-disabled are configuration, fixed by props on each render. There is no consumer-owned/component-owned pair and no lifecycle.

Composite mode is not a Button prop: it is inferred from whether the Button is rendered inside a composite root (UBT-STA-01). Button's own tests never place it in one; BTN-EDGE-02 records the consequence.

## 5. Rendered DOM and ARIA

### 5.1 Host element

Attribute resolution is UBT-ALG-03 with `F = focusableWhenDisabled` (always `true` or `false`, BTN-API-02) and `T = 0`. The table restates the outcome outside composite mode for Button's defaults.

| `nativeButton` | `focusableWhenDisabled` | `disabled` | `type` | `role` | `tabindex` | `disabled` attr | `aria-disabled` | `data-disabled` |
|---|---|---|---|---|---|---|---|---|
| `true` | `false` | `false` | `"button"` | — | `0` | — | — | — |
| `true` | `false` | `true` | `"button"` | — | `0` | present | — | `""` |
| `true` | `true` | `false` | `"button"` | — | `0` | — | `"false"` | — |
| `true` | `true` | `true` | `"button"` | — | `0` | — | `"true"` | `""` |
| `false` | `false` | `false` | — | `"button"` | `0` | — | — | — |
| `false` | `false` | `true` | — | `"button"` | `-1` | — | `"true"` | `""` |
| `false` | `true` | `false` | — | `"button"` | `0` | — | — | — |
| `false` | `true` | `true` | — | `"button"` | `0` | — | `"true"` | `""` |

Req: BTN-DOM-01…BTN-DOM-04, BTN-ARIA-01, BTN-ARIA-02.

- **BTN-DOM-01** — MUST. The host element MUST carry `data-disabled=""` exactly while `disabled` is `true`, independently of `nativeButton` and `focusableWhenDisabled` (default state-attribute projection, RND-DOM-01/RND-DOM-02).
  Source: `button/Button.tsx:33-41`, `internals/getStateAttributesProps.ts:24-25`. Test: [L2] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable", "custom element: applies aria-disabled and is not focusable", "native button: prevents interactions but remains focusable", "native button: allows hover handlers while blocking activation", "custom element: prevents interactions but remains focusable".
- **BTN-DOM-02** — MUST. In native mode the host element MUST carry `type="button"` unless the consumer supplies a `type` (so a Button does not submit an enclosing form by default); in non-native mode no `type` is added by the component.
  Source: `internals/use-button/useButton.ts:226`, `internals/useRenderElement.tsx:225-227`; see UBT-DOM-01, UBT-DOM-02, UBT-FRM-01, RND-STR-05. Test: [L2] `Button.test.tsx` conformance suite (default element). Docs: `components/button/page.mdx` §Usage guidelines ("`type="submit"` must be specified").
- **BTN-DOM-03** — MUST. Outside composite mode, the host element MUST carry `tabindex="0"` — including on a native `<button>` — except a disabled non-native Button with `focusableWhenDisabled={false}`, which carries `tabindex="-1"`. A consumer `tabIndex` overrides both.
  Source: `utils/useFocusableWhenDisabled.ts:30-36`; see UBT-DOM-03, UBT-DOM-04, UBT-DOM-09. Test: [L2] `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation" (`0`), "custom element: applies aria-disabled and is not focusable" (`-1`), "native button: prevents interactions but remains focusable" (`0`), "custom element: prevents interactions but remains focusable" (`0`).
- **BTN-DOM-04** — MUST. In native mode the boolean `disabled` attribute MUST be present exactly when `disabled` is `true` and `focusableWhenDisabled` is `false`; in non-native mode, or with `focusableWhenDisabled={true}`, the component MUST never emit a `disabled` attribute.
  Source: `utils/useFocusableWhenDisabled.ts:45-47`; see UBT-DOM-06, UBT-DOM-07, UBT-DOM-08. Test: [L2] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable", "custom element: applies aria-disabled and is not focusable", "native button: prevents interactions but remains focusable", "custom element: prevents interactions but remains focusable".
- **BTN-DOM-05** — MUST. Consumer attributes MUST be merged *before* the mechanism (`[consumer attributes, mechanism resolver]`), so that the resolver receives them as input: non-handler consumer attributes override the mechanism's values (UBT-DOM-09) and consumer `onClick`/`onMouseDown`/`onPointerDown`/`onKeyDown`/`onKeyUp` are subject to the disabled gate (UBT-EVT-03). A substitute element's own handlers are merged later (RND §2.1 row 5) and are therefore not gated.
  Source: `button/Button.tsx:37-41`; see UBT-EVT-03, RND-ALG-01. Test: [L2] `Button.test.tsx` "native button: prevents interactions but remains focusable", "custom element: applies button semantics and dispatches real clicks from keyboard activation" (substitute element's `onClick`/`onClickCapture` observe the synthetic click). (L4 for the disabled-gate clause and the synthetic-click citation)

### 5.2 ARIA

- **BTN-ARIA-01** — MUST. In non-native mode the host element MUST carry `role="button"` unless the consumer supplies a `role`; in native mode no `role` is added.
  Source: `internals/use-button/useButton.ts:226`; see UBT-DOM-02. Test: [L2] `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation", "custom link element: Space activates the link without scrolling the page" (`<a>` exposed as `button`).
- **BTN-ARIA-02** — MUST. `aria-disabled` MUST follow UBT-ALG-03 with Button's explicit `focusableWhenDisabled`: native + `focusableWhenDisabled={true}` → `aria-disabled` equals the stringified `disabled` (`"false"` on an enabled button, `"true"` on a disabled one); non-native → `aria-disabled="true"` exactly while disabled; native + `focusableWhenDisabled={false}` → never.
  Source: `utils/useFocusableWhenDisabled.ts:38-43`; see UBT-ARIA-01, UBT-ARIA-02, UBT-ARIA-03. Test: [L2] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable" (absent), "custom element: applies aria-disabled and is not focusable" (`"true"`), "native button: prevents interactions but remains focusable" (`"true"`), "keeps focus and suppresses interactions after becoming disabled" (`"true"` after the change).

Button generates no `id` and sets no labelling attributes; its accessible name comes from its content or consumer `aria-*` attributes. The docs recommend `aria-labelledby` pointing at the changing text for loading states (`components/button/page.mdx` §Loading states); that is a consumer concern.

## 6. Interactions

All rows are UBT behavior with Button's configuration; the normative statements are the UBT IDs cited. Button adds no keyboard or pointer handling of its own.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> | native, enabled | Not intercepted; browser activates the `<button>`. | BTN-KEY-01 (UBT-KEY-09) |
| <kbd>Space</kbd> | native, enabled | Not intercepted; browser activates on keyup. | BTN-KEY-01 (UBT-KEY-09) |
| <kbd>Enter</kbd> | non-native, not a link, enabled | Keydown default prevented; one synthetic click with the key's modifier state. | BTN-KEY-01 (UBT-KEY-01, UBT-EVT-04) |
| <kbd>Space</kbd> | non-native, not a link, enabled | Keydown default prevented (no scroll), no click; keyup dispatches one synthetic click. | BTN-KEY-01 (UBT-KEY-02) |
| <kbd>Enter</kbd> | non-native `<a href>` | Not intercepted; native link activation. | BTN-KEY-01 (UBT-KEY-10) |
| <kbd>Space</kbd> | non-native `<a href>` | Keydown default prevented (no scroll); keyup synthetic click → `onClick` and navigation. | BTN-KEY-01 (UBT-KEY-11) |
| <kbd>Enter</kbd>/<kbd>Space</kbd> | consumer `onKeyDown` called `preventDefault()` (Enter), consumer `onKeyUp` called `preventDefault()` (Space), or either called `preventBaseUIHandler()` | Activation canceled per UBT. A default-prevented <kbd>Space</kbd> *keydown* alone does not cancel the keyup click. | BTN-KEY-01 (UBT-KEY-03, UBT-KEY-04, UBT-KEY-05, UBT-KEY-06) |
| any key | disabled | Consumer `onKeyDown`/`onKeyUp` not called; no activation. | BTN-KEY-02 (UBT-KEY-19) |
| any key except <kbd>Tab</kbd> | disabled + `focusableWhenDisabled` | Keydown default prevented; propagation not stopped. | BTN-KEY-02 (UBT-KEY-20) |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | any | Never intercepted. | BTN-KEY-02 (UBT-KEY-21) |
| other keys | enabled | Not intercepted; consumer handlers run. | BTN-KEY-01 (UBT-KEY-07) |
| modifiers held | any activation above | Modifier state copied onto the synthetic click. | BTN-KEY-01 (UBT-EVT-04) |

There are no orientation or RTL variants.

- **BTN-KEY-01** — MUST. With `disabled={false}`, keyboard activation MUST follow UBT-ALG-01 and UBT-ALG-02 with `native = nativeButton`, `composite` inferred (normally `false`, BTN-EDGE-02): in particular each <kbd>Enter</kbd> press (keydown) and each <kbd>Space</kbd> press (keyup) on a non-native Button MUST produce exactly one click that capture-phase, substitute-element, consumer and ancestor listeners each observe once, carrying the key event's `shiftKey`/`ctrlKey`/`altKey`/`metaKey`. Server: relaxed, see §9.
  Source: `button/Button.tsx:27-31`, `internals/use-button/useButton.ts:116-217`; see UBT-KEY-01, UBT-KEY-02, UBT-KEY-09, UBT-KEY-10, UBT-KEY-11, UBT-EVT-04. Test: [L4] `Button.test.tsx` "custom element: applies button semantics and dispatches real clicks from keyboard activation", "custom element: keyboard activation clicks carry modifier key state", "custom link element: Space activates the link without scrolling the page".
- **BTN-KEY-02** — MUST. With `disabled={true}`, the consumer's `onKeyDown`/`onKeyUp` MUST NOT be invoked and no activation MUST occur; with `focusableWhenDisabled={true}` additionally every non-<kbd>Tab</kbd> keydown MUST be default-prevented.
  Source: `internals/use-button/useButton.ts:117-119,178-180`, `utils/useFocusableWhenDisabled.ts:23-27`; see UBT-KEY-19, UBT-KEY-20, UBT-KEY-21. Test: [L4] `Button.test.tsx` "native button: prevents interactions but remains focusable", "custom element: prevents interactions but remains focusable", "keeps focus and suppresses interactions after becoming disabled".

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| click | enabled | Consumer `onClick` called once; native activation (submit/reset/link) runs. | BTN-PTR-01 (UBT-PTR-05) |
| click | disabled | Default prevented; consumer `onClick` not called. | BTN-PTR-02 (UBT-PTR-01) |
| mousedown | disabled | Consumer `onMouseDown` not called; default not prevented. | BTN-PTR-02 (UBT-PTR-02) |
| pointerdown | disabled | Default prevented; consumer `onPointerDown` not called. | BTN-PTR-02 (UBT-PTR-03) |
| hover / move / enter / leave / pointerup | disabled + `focusableWhenDisabled` | Consumer handlers still fire. | BTN-PTR-02 (UBT-PTR-04) |

Pointer type makes no difference; there is no press-and-hold, drag or long-press behavior.

- **BTN-PTR-01** — MUST. When enabled, the consumer's `onClick`, `onMouseDown` and `onPointerDown` MUST each be called exactly once per corresponding event.
  Source: `internals/use-button/useButton.ts:104-115,218-224`; see UBT-PTR-05. Test: [L2] `Button.test.tsx` "keeps focus and suppresses interactions after becoming disabled" (first click calls `onClick` once).
- **BTN-PTR-02** — MUST. When disabled, `click` and `pointerdown` MUST be default-prevented, the consumer's `onClick`, `onMouseDown` and `onPointerDown` MUST NOT be called, and handlers outside the five gated ones (e.g. `onMouseMove`) MUST still be called.
  Source: `internals/use-button/useButton.ts:104-115,218-224,228`; see UBT-PTR-01…UBT-PTR-04. Test: [L4] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable", "custom element: applies aria-disabled and is not focusable", "native button: prevents interactions but remains focusable", "native button: allows hover handlers while blocking activation", "custom element: prevents interactions but remains focusable".

### 6.3 Focus management

- **BTN-FOC-01** — MUST. A disabled Button with `focusableWhenDisabled={false}` MUST NOT be reachable with <kbd>Tab</kbd>: natively via the `disabled` attribute (native mode, UBT-FOC-01) or via `tabindex="-1"` (non-native mode, UBT-FOC-02).
  Source: `utils/useFocusableWhenDisabled.ts:33-35,45-47`. Test: [L4] `Button.test.tsx` "native button: uses the disabled attribute and is not focusable", "custom element: applies aria-disabled and is not focusable".
- **BTN-FOC-02** — MUST. A disabled Button with `focusableWhenDisabled={true}` MUST remain in the tab order and receive focus via <kbd>Tab</kbd> in both modes; a focused Button that becomes disabled MUST keep focus, and subsequent clicks, <kbd>Enter</kbd> and <kbd>Space</kbd> MUST do nothing.
  Source: `utils/useFocusableWhenDisabled.ts:30-47`, `internals/use-button/useButton.ts:104-110`; see UBT-FOC-03, UBT-FOC-04. Test: [L4] `Button.test.tsx` "native button: prevents interactions but remains focusable", "custom element: prevents interactions but remains focusable", "keeps focus and suppresses interactions after becoming disabled". Docs: `components/button/page.mdx` §Loading states.
- **BTN-FOC-03** — MUST. Button MUST NOT move focus itself (no focus on press, no blur when disabled).
  Source: see UBT-FOC-06. Test: [L4] `Button.test.tsx` "keeps focus and suppresses interactions after becoming disabled".

### 6.4 Dismissal

Not applicable — Button has no dismissable surface.

## 7. Events and callbacks

Button defines no callback parameters of its own. DOM handlers are forwarded through the mechanism.

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onClick` | `(event) => void` | each click while enabled, including synthetic keyboard clicks (non-native) and native keyboard activation (native) | — | `preventDefault()` cancels native activation (submit, navigation) |
| `onKeyDown` / `onKeyUp` | `(event) => void` | key events while enabled, before the mechanism's activation logic | — | `preventDefault()` / `preventBaseUIHandler()` cancel activation (UBT-KEY-03/04/06) |
| `onMouseDown` / `onPointerDown` | `(event) => void` | while enabled | — | — |

- **BTN-EVT-01** — MUST. Synthetic clicks produced by keyboard activation in non-native mode MUST be real, bubbling, cancelable DOM `click` events (`detail` 0, `pointerType` `''`) dispatched on the host element, so that the browser's activation behavior for the element (for example link navigation on an `<a href>`) runs.
  Source: `utils/dispatchClickWithModifiers.ts:20-41`; see UBT-EVT-04, UBT-EVT-05, UBT-FRM-02. Test: [L4] `Button.test.tsx` "custom link element: Space activates the link without scrolling the page" (`location.hash` changes), "custom element: applies button semantics and dispatches real clicks from keyboard activation". Docs: CHANGELOG.md v1.7.0 "Fix keyboard click handling for custom elements".

## 8. Form integration

Button is not a Field control and registers nothing with `Field` or `Form`. Its only form interaction is the native button's own: `type`, `form`, `name` and `value` are forwarded as attributes (BTN-API-05), and with the default `type="button"` (BTN-DOM-02) it neither submits nor resets.

- **BTN-FRM-01** — MUST. A native Button MUST submit (or reset) its form only when the consumer passes `type="submit"` (or `"reset"`); keyboard and pointer activation then run the native submission exactly once, and a disabled Button (any mode) MUST NOT submit because its click is default-prevented or never dispatched.
  Source: `internals/use-button/useButton.ts:104-110,226`; see UBT-FRM-01, UBT-PTR-01. Test: none in `Button.test.tsx` (UBT tests "key: Space preserves native submit semantics on composite buttons"). Docs: `components/button/page.mdx` §Usage guidelines.

## 9. Lifecycle, mounting and animation

Not applicable — Button is always rendered and has no transition status. The only post-mount work is the mechanism's development-only element check (BTN-EDGE-01) and its composite `disabled` correction (UBT-DOM-10), which does not apply outside a composite root.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- BTN-KEY-01 — UBT-KEY-03/04 row: a consumer `preventDefault()` veto of <kbd>Enter</kbd> keydown / <kbd>Space</kbd> keyup activation is met on both modes when issued from a consumer JavaScript listener (the `button` module checks `defaultPrevented`); a C# consumer's veto is `PreventBaseUIHandler()`, synchronous on WebAssembly through `decide()`, a round trip late on Server (fallback "run"); Blazor event directives are rejected on parts (ADR 04 Amendments 2026-10-18). EVD-EVT-26 row against button key handling: on Server a C# consumer's `PreventBaseUIHandler()` arrives after dispatch ends, so the key is handled regardless; WebAssembly honours it through `decide()` (ADR 04 §3).

## 10. Positioning

Not applicable — not a floating part.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Button mechanism (`useButton`) | [UBT](../architecture/button-behavior.md) §6.2 | `{ disabled: disabled ?? false, focusableWhenDisabled: focusableWhenDisabled ?? false, native: nativeButton ?? true }`; `tabIndex` default `0`; `composite` inferred from a composite root ancestor (UBT-STA-01) |
| `useFocusableWhenDisabled` (via the mechanism) | UBT (UBT-ALG-03) | `F` always explicit (`true`/`false`); `T = 0` |
| Rendering pipeline (`useRenderElement`) | [RND](../architecture/rendering-and-composition.md) §6.1 | default tag `button` (RND-STR-05 adds `type="button"` too); state `{ disabled }`; no custom mapping; `props: [consumer attributes, mechanism resolver]` — one of the five parts whose consumer attributes come first; DOM handles: consumer handle + mechanism handle |
| `dispatchClickWithModifiers` (via the mechanism) | UBT (UBT-ALG-05) | keyboard activation only |

## 12. Edge cases, invariants and dev warnings

Button emits no messages of its own. Dev-only diagnostics come from the mechanism and are quoted in UBT.

- **BTN-EDGE-01** — SHOULD. In development builds, rendering a non-`<button>` element while `nativeButton` is `true`, or a `<button>` while `nativeButton` is `false`, SHOULD log the corresponding error of UBT-EDGE-01 / UBT-EDGE-02 once (UBT-EDGE-03). No parameter substitution applies (the messages name only the `nativeButton` prop).
  Source: `internals/use-button/useButton.ts:36-66`. Test: none in `Button.test.tsx` (UBT: `useButton.test.tsx` "errors if nativeButton=true but ref is not a button", "errors if nativeButton=false but ref is a button"). Docs: CHANGELOG.md v1.2.0 "Capture component stack for `nativeButton` error message".
- **BTN-EDGE-02** — MUST. When a Button is rendered inside a composite root (for example inside `Toolbar.Root`, `Tabs.List`, `ToggleGroup`, `RadioGroup`, `Menubar` or `Accordion.Root`), composite mode MUST be inferred (UBT-STA-01): the mechanism then emits no `tabindex` (UBT-DOM-05), <kbd>Space</kbd> activates on keydown (UBT-KEY-12), and, because Button passes `focusableWhenDisabled = false` explicitly when the prop is omitted, a disabled native Button gets the `disabled` attribute and no `aria-disabled` (UBT-ARIA-04).
  Source: `internals/use-button/useButton.ts:25-26`, `utils/useFocusableWhenDisabled.ts:15-16,45-47`, `button/Button.tsx:21,29`. Test: none.
- **BTN-EDGE-03** — MUST. A misconfigured Button (mode declaration not matching the rendered element) MUST behave as UBT-EDGE-05 describes; nothing in Button corrects it.
  Source: see UBT-EDGE-05. Test: none.
- **BTN-EDGE-04** — MUST. No behavior depends on writing direction or locale.
  Source: `button/Button.tsx:1-42` (no direction or locale input); see UBT I18N. Test: none.
- **BTN-EDGE-05** — MUST. All attributes of §5 MUST be present in server-rendered markup (no attribute is applied after mount).
  Source: see UBT-DOM-11. Test: none in `Button.test.tsx`.

Inherited dev checks (invalid `render` element error, uppercase-named render function warning) are RND-EDGE-01…RND-EDGE-03.

## 13. Behaviors evidenced by tests

### `Button.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLButtonElement`, `button: true`; `testRenderPropWith` default `div`, so substitution tests pass `nativeButton={false}` and the extra "with a non-native button" pass does not run) | BTN-DOM-06, BTN-API-04, BTN-DOM-02 |
| "custom link element: Space activates the link without scrolling the page" | BTN-KEY-01 (UBT-KEY-11), BTN-EVT-01 (UBT-FRM-02), BTN-ARIA-01 |
| "custom element: applies button semantics and dispatches real clicks from keyboard activation" | BTN-STR-01, BTN-API-03, BTN-ARIA-01 (UBT-DOM-02), BTN-DOM-03 (UBT-DOM-03), BTN-KEY-01 (UBT-KEY-01, UBT-KEY-02, UBT-EVT-04), BTN-DOM-05, BTN-EVT-01 |
| "custom element: keyboard activation clicks carry modifier key state" | BTN-KEY-01 (UBT-EVT-04) |
| "native button: uses the disabled attribute and is not focusable" | BTN-API-01, BTN-API-02, BTN-DOM-01, BTN-DOM-04 (UBT-DOM-06), BTN-ARIA-02 (UBT-ARIA-03), BTN-FOC-01 (UBT-FOC-01), BTN-PTR-02 |
| "custom element: applies aria-disabled and is not focusable" | BTN-DOM-01, BTN-DOM-03 (UBT-DOM-04), BTN-DOM-04 (UBT-DOM-08), BTN-ARIA-02 (UBT-ARIA-02), BTN-FOC-01 (UBT-FOC-02), BTN-PTR-02, BTN-KEY-02 |
| "native button: prevents interactions but remains focusable" | BTN-API-02, BTN-API-05, BTN-DOM-01, BTN-DOM-03, BTN-DOM-04 (UBT-DOM-07), BTN-DOM-05, BTN-ARIA-02 (UBT-ARIA-01), BTN-FOC-02 (UBT-FOC-03), BTN-KEY-02 (UBT-KEY-19), BTN-PTR-02 (UBT-PTR-01…03) |
| "native button: allows hover handlers while blocking activation" (browser only) | BTN-API-05, BTN-DOM-01, BTN-PTR-02 (UBT-PTR-04) |
| "keeps focus and suppresses interactions after becoming disabled" | BTN-FOC-02 (UBT-FOC-04), BTN-FOC-03, BTN-PTR-01, BTN-ARIA-02, BTN-KEY-02 |
| "custom element: prevents interactions but remains focusable" | BTN-DOM-01, BTN-DOM-03, BTN-DOM-04, BTN-ARIA-02, BTN-FOC-02, BTN-KEY-02, BTN-PTR-02 |

### Additional requirements introduced here

- **BTN-DOM-06** — MUST. Button MUST pass the shared conformance suite: RND-DOM-36…RND-DOM-48 with an `HTMLButtonElement` DOM handle for the default element, where every substitution test renders a `div` with `nativeButton={false}` (so RND-DOM-49 is satisfied by the same runs).
  Source: `test/describeConformance.tsx:55-76`, `test/conformanceTests/propForwarding.tsx:14-20`, `test/conformanceTests/renderProp.tsx:17-25`. Test: [L2] `Button.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **Mechanism as a merge-list function.** Button passes the mechanism's props resolver as the *last* entry of its attribute list, after the consumer's attributes. In the port, the consumer's attribute/handler bag must be handed to the button behavior as input (not merged around it), otherwise the disabled gate would not cover consumer handlers (UBT-EVT-03, BTN-DOM-05). Port: see decisions/04-js-interop-boundary.md §1 (`button` module) and §3 (`decide()` for button activation, fallback "run"); decisions/02-render-pipeline-and-attribute-merging.md §Decision 3 (handler chain and `PreventBaseUIHandler()`).
- **`aria-disabled="false"`.** React renders `aria-disabled={false}` as the string `"false"`; with `focusableWhenDisabled` on an enabled native Button that attribute is present. A port must not drop it (UBT-ARIA-01; §16 item 2). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Amendments (typed `AttributeValue` emission; only absent/`null` values are omitted, per §Requirements relaxed RND-DOM-19).
- **`tabindex="0"` on a native `<button>`.** Redundant but emitted (UBT-DOM-03); keep it for markup parity.
- **Ref type.** The forwarded handle is typed `HTMLElement` because the rendered element varies with `render`; the default is `HTMLButtonElement`. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24, RND-DOM-41).
- **Composite inference** reads an ancestor-provided value from the nearest composite root; a port needs the same ancestor lookup if it wants BTN-EDGE-02 parity. Port: see decisions/04-js-interop-boundary.md §1 (composite navigation is JavaScript; the `button` module's composite Space-on-keydown path runs there).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| BTN-STR-01 | MUST | one host element, default `button` | 2 |
| BTN-API-01 | MUST | `disabled` default false, passed to mechanism | 3.1 |
| BTN-API-02 | MUST | `focusableWhenDisabled` default false, always explicit | 3.1 |
| BTN-API-03 | MUST | `nativeButton` default true → `native` | 3.1 |
| BTN-API-04 | MUST | `className`/`style`/`render`/`ref` per RND; both handles attached | 3.1 |
| BTN-API-05 | MUST | other params into props resolver; gated handlers; overrides | 3.1 |
| BTN-STA-01 | MUST | state object is exactly `{ disabled }` | 3.1 |
| BTN-DOM-01 | MUST | `data-disabled` while disabled, every mode | 5.1 |
| BTN-DOM-02 | MUST | native: `type="button"` unless consumer type | 5.1 |
| BTN-DOM-03 | MUST | `tabindex` 0, or -1 when disabled non-native non-focusable | 5.1 |
| BTN-DOM-04 | MUST | `disabled` attr only native + disabled + not focusable | 5.1 |
| BTN-DOM-05 | MUST | consumer attrs merged before mechanism (gated) | 5.1 |
| BTN-DOM-06 | MUST | passes RND conformance suite | 13 |
| BTN-ARIA-01 | MUST | non-native `role="button"` | 5.2 |
| BTN-ARIA-02 | MUST | `aria-disabled` per UBT-ALG-03 with explicit F | 5.2 |
| BTN-KEY-01 | MUST | keyboard activation per UBT-ALG-01/02 | 6.1 |
| BTN-KEY-02 | MUST | disabled: key handlers suppressed; focusable: non-Tab prevented | 6.1 |
| BTN-PTR-01 | MUST | enabled: pointer handlers once each | 6.2 |
| BTN-PTR-02 | MUST | disabled: click/pointerdown prevented, gated handlers not called, others fire | 6.2 |
| BTN-FOC-01 | MUST | disabled non-focusable: not tabbable | 6.3 |
| BTN-FOC-02 | MUST | focusableWhenDisabled: tabbable, keeps focus, inert | 6.3 |
| BTN-FOC-03 | MUST | never moves focus | 6.3 |
| BTN-EVT-01 | MUST | synthetic clicks are real DOM clicks with activation behavior | 7 |
| BTN-FRM-01 | MUST | submits only with consumer `type`; disabled never submits | 8 |
| BTN-EDGE-01 | SHOULD | nativeButton mismatch dev errors (UBT-EDGE-01/02) | 12 |
| BTN-EDGE-02 | MUST | composite inference inside composite roots | 12 |
| BTN-EDGE-03 | MUST | misconfiguration per UBT-EDGE-05 | 12 |
| BTN-EDGE-04 | MUST | no RTL/locale dependence | 12 |
| BTN-EDGE-05 | MUST | attributes present in SSR markup | 12 |

## 16. Open questions

1. **Links rendered through Button.** `components/button/page.mdx` §Rendering links as buttons says links "should not be rendered as buttons through the `render` prop", yet `Button.test.tsx:17-40` ("custom link element: Space activates the link without scrolling the page") pins the behavior of exactly that configuration (UBT-KEY-10/11). Documented as supported behavior; the docs guidance is advisory.
2. **`aria-disabled="false"` on an enabled `focusableWhenDisabled` native Button.** Follows from `utils/useFocusableWhenDisabled.ts:38-43` (UBT-ARIA-01) but no Button test asserts the enabled case. Kept as MUST for parity (BTN-ARIA-02); see UBT §10 Q4.
3. **Outline omits `disabled`.** `components/page.mdx` (Button outline) lists `className, focusableWhenDisabled, nativeButton, render, style` but not `disabled`, which Button consumes (`button/Button.tsx:20`) and which drives `data-disabled`. Documented in §3.1.
4. **Composite inference for a standalone Button** (BTN-EDGE-02) is untested for `Button`; UBT §6.2 lists Button's composite as "ctx (none)". Whether placing a `Button` inside a composite root is supported usage is not stated in the docs.
5. **`type` on non-native Button.** `button/Button.spec.tsx:10` type-checks `<Button nativeButton={false} type="submit" />`; the `type` attribute is then forwarded onto the non-button element (UBT-DOM-09) with no effect on submission. Behavior is as written; flagged in case the port wants to drop it.
6. **Ids under a substitute callback or a component root (port).** §5.2 says Button generates no `id`. The port's `button` module is attached by element reference, and where the part has none, by the element's id (ADR 04 §3; ADR 02 decision 1). Decided (2026-10-21, M1 track 4): under `RenderFunction` Button draws a generated `base-ui-<n>` id at its first render and renders it, the consumer's `id` winning (RND-DOM-32). Under a `Render` whose root is a component, Button draws an id after its first interactive render, renders it, and attaches on the next render. In every other configuration Button renders no `id`, as Base UI does. No requirement changes.
7. **The disabled gate on consumer handlers (port reading of BTN-DOM-05, UBT-EVT-03).** The port's pipeline merges consumer attributes after the part's (`src/Blazix.Primitives/Rendering/CONTEXT.md`), so the consumer still wins every non-handler attribute (UBT-DOM-09). Decided (2026-10-21, M1 track 4; ADR 04 Amendments 2026-10-18 item 1): Button withholds the consumer's `onclick`, `onmousedown` and `onpointerdown` while `disabled`, so Blazor invokes none of them; the `button` module default-prevents `click` and `pointerdown`. Button always withholds the consumer's `onkeydown` and `onkeyup` and runs them from its own handler, gated on `disabled`. On WebAssembly the activation decision point runs the consumer's key handler inside the dispatch, so `PreventPartHandler()` cancels the activation (ADR 04 §3), and the part handler skips Blazor's dispatch of that same event, so the consumer's handler runs once. On Server the decision point falls back to "run" (§9). Every other handler, and every handler on a substitute element, is forwarded unchanged (UBT-PTR-04, RND §2.1 row 5). No requirement changes.
8. **Development messages in the port's words (BTN-EDGE-01).** UBT-EDGE-01/02 quote Base UI's text with its `Base UI: ` prefix. Decided (2026-10-21, M1 track 4; ADR 08 §4 product-name rule): the port logs the same two messages with the `Blazix: ` prefix, code in backticks, the parameter names `NativeButton` and `Render`, and "the library" for "Base UI". It logs them through the development warnings switch at `Error`, once per message per session (Server circuit or WebAssembly application, UBT-EDGE-03). No requirement changes.
