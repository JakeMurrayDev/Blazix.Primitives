# Alert Dialog

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/alert-dialog/` (2 own parts: `root/`, `trigger/`; plus `handle.ts`; every other part re-exported from `dialog/`) |
| Tests | `packages/react/src/alert-dialog/**/*.test.tsx` (1 file: `root/AlertDialogRoot.test.tsx`) + `root/AlertDialogRoot.spec.tsx` (type-level only) + the shared suite `test/dialogRootSharedTests.tsx`; AlertDialog cases in `dialog/root/DialogRoot.test.tsx`, `dialog/popup/DialogPopup.test.tsx`, `drawer/popup/DrawerPopup.test.tsx`, `toolbar/button/ToolbarButton.test.tsx`, `menu/root/MenuRoot.test.tsx`, `utils/popups/popupHandle.test.ts` |
| Docs page | `docs/src/app/(docs)/react/components/alert-dialog/page.mdx` |
| Prefix | `ADL` |
| Status | implemented |
| Revised | 2026-10-29 |
| Builds on | [dialog](dialog.md) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [button-behavior](../architecture/button-behavior.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [shared-utilities](../architecture/shared-utilities.md) (all inherited through Dialog) |

## 1. Overview

Alert Dialog is a [Dialog](dialog.md) for interruptions that need a response, such as "Discard draft?" confirmations. It looks and behaves like a Dialog. The differences are: the popup has `role="alertdialog"`; the dialog is always fully modal (focus trapped, outside content `aria-hidden`, document scroll locked, an invisible full-viewport internal backdrop catches presses); and pressing outside the popup never closes it, whatever the target or pointer type. The user closes it with an explicit action: a Close button, <kbd>Escape</kbd>, a press on the active trigger, or an imperative close from the consumer. Focus leaving the popup never closes it either.

The implementation is the Dialog implementation in *alert mode*. `AlertDialog.Root` calls the same root routine as `Dialog.Root` with mode `'alert-dialog'`. That mode forces `modal` to `true` and `disablePointerDismissal` to `true`, and sets the popup role to `alertdialog`. The `modal` and `disablePointerDismissal` parameters are removed from the public prop contract. `AlertDialog.Trigger` is the `Dialog.Trigger` component itself under a narrower type, and every other part is the Dialog part of the same name. This document therefore adopts the Dialog requirements wholesale (ADL-STR-02) and specifies individually only what alert mode changes, plus the AlertDialog-specific tests. The dialog.md author marked the alert-mode branches inline (DLG-API-03, DLG-API-06, DLG-ARIA-04, DLG-STA-05, DLG-FOC-15, DLG-FOC-16, DLG-DIS-01, DLG-DIS-10).

Base component: [dialog](dialog.md). Derived components: none.

## 2. Anatomy

```
<AlertDialog.Root>                    required, 1            renders no element           (own; Dialog root in alert mode)
  <AlertDialog.Trigger>               optional, 0..n         button                       (= Dialog.Trigger; may be outside Root with `handle`)
  <AlertDialog.Backdrop>              optional, 0..n         div                          = Dialog.Backdrop
  <AlertDialog.Portal>                required for Popup, 1  div (portal node)            = Dialog.Portal
    [internal backdrop]               automatic              div, whenever mounted (always modal)
    <AlertDialog.Viewport>            optional, 0..1         div                          = Dialog.Viewport
      [focus guard] <AlertDialog.Popup> [focus guard]        div, role="alertdialog"      = Dialog.Popup
        <AlertDialog.Title>           optional, 0..1         h2                           = Dialog.Title
        <AlertDialog.Description>     optional, 0..1         p                            = Dialog.Description
        <AlertDialog.Close>           optional, 0..n         button                       = Dialog.Close
```

Placement and cardinality rules are those of [dialog](dialog.md) §2 (DLG-STR-01…12). The docs anatomy (`components/alert-dialog/page.mdx` §Anatomy) shows Backdrop and Viewport inside the Portal.

- **ADL-STR-01** — MUST. The `AlertDialog` namespace exposes exactly: `Root` (own), `Trigger` (the Dialog trigger component under an AlertDialog-typed `handle`), `Portal`, `Backdrop`, `Viewport`, `Popup`, `Title`, `Description`, `Close` (re-exported Dialog parts), `createHandle` and `Handle` (own subclass of `Dialog.Handle`). Alongside the namespace, the attribute-name namespaces `AlertDialogBackdropDataAttributes`, `AlertDialogCloseDataAttributes`, `AlertDialogPopupCssVariables`, `AlertDialogPopupDataAttributes` and `AlertDialogViewportDataAttributes` are the Dialog enums under new names. `AlertDialogTriggerDataAttributes` is its own file with the same content as Dialog's, `{ disabled: 'data-disabled', popupOpen: 'data-popup-open' }`.
  Source: `alert-dialog/index.parts.ts:1-10`; `alert-dialog/index.ts:1-40`; `alert-dialog/trigger/AlertDialogTriggerDataAttributes.ts:1-10`; `alert-dialog/trigger/AlertDialogTrigger.tsx:13`. Test: none (outline `components/page.mdx` §Alert Dialog).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); ADR 08 Amendments 2026-10-27, chosen by the user 2026-10-27): the port's names are types, not aliases. `AlertDialogRoot<TPayload>` and `AlertDialogTrigger<TPayload>` have their own parameter lists; `AlertDialogPortal`, `AlertDialogBackdrop`, `AlertDialogViewport`, `AlertDialogPopup`, `AlertDialogTitle`, `AlertDialogDescription` and `AlertDialogClose` are sealed parts over Dialog's shared behaviours, each with its own state record (`AlertDialogTriggerState(bool Disabled, bool Open)`, `AlertDialogBackdropState(bool Open, TransitionStatus? TransitionStatus)`, `AlertDialogViewportState` and `AlertDialogPopupState(bool Open, TransitionStatus? TransitionStatus, bool Nested, bool NestedDialogOpen)`, `AlertDialogCloseState(bool Disabled)`) and attribute class, with Dialog's values under the new names (`AlertDialogTriggerDataAttributes` is `{ Disabled = "data-disabled", PopupOpen = "data-popup-open" }`, `AlertDialogPopupCssVars.NestedDialogs` is `"--nested-dialogs"`). `createHandle()` is `new AlertDialogHandle<TPayload>()`. Test: [L1] `AlertDialogApiTests`; [L2] the eight AlertDialog part suites (`AlertDialogConformanceTests`).
- **ADL-STR-03** — MUST. `AlertDialog.Root` renders no element. It runs the Dialog root routine with mode `'alert-dialog'` (DLG-STR-01) and provides the same ancestor-provided shared state as `Dialog.Root`. As a result an AlertDialog Root inside a Dialog, Drawer or AlertDialog Root is *nested* (DLG-STR-12, DLG-STA-03), and a Dialog/Drawer Root inside an AlertDialog Root is nested in it. The parent tracks the nested open count across families (DLG-STA-04).
  Source: `alert-dialog/root/AlertDialogRoot.tsx:14-16`; `dialog/root/useRenderDialogRoot.tsx:17-43,94-106`. Test: [L2] `DialogPopup.test.tsx` "increments for nested alert dialog and decrements on close (cross-type)", "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent alert dialog".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): the two titles run at [L2] in `AlertDialogNestingTests` (verbatim).
- **ADL-STR-04** — MUST. Because `AlertDialog.Trigger` and the re-exported parts are the Dialog components themselves, `Dialog.*` parts MAY be used inside an `AlertDialog.Root` (and `AlertDialog.*` parts inside a `Dialog.Root`) with identical behavior. Each part reads the nearest dialog-family Root, so behavior is decided by that Root's mode, not by the namespace the part was imported from.
  Source: `alert-dialog/index.parts.ts:2-9`; `alert-dialog/trigger/AlertDialogTrigger.tsx:13`; `dialog/root/DialogRootContext.ts:5-19`. Test: [L2] `ToolbarButton.test.tsx` "AlertDialog" block comment (`AlertDialog.Trigger is Dialog.Trigger`). Docs: none.
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): the port's AlertDialog parts are types of their own, so the interchange is asserted by port titles; Base UI's only evidence is the comment in `ToolbarButton.test.tsx`'s AlertDialog block. Test: [L2] `AlertDialogPartTests` "port: a DialogPopup under an AlertDialogRoot renders role=\"alertdialog\", and an AlertDialogPopup under a DialogRoot renders role=\"dialog\"", "port: an AlertDialogTrigger opens the DialogRoot that encloses it, and a DialogTrigger the AlertDialogRoot that encloses it", "port: a part outside its portal names its own family's portal, whichever family's root encloses it".

Structural-only parts are the same as Dialog's: Title, Description and Backdrop. Omitting optional parts has the same effect as in Dialog. Without a Trigger the alert dialog opens only through `open`/`defaultOpen` or a handle, as in the docs' "Open from a menu" demo.

### 2.1 Adoption from base

- **ADL-STR-02** — MUST. Every re-exported part, and the Dialog root/store/interaction machinery that `AlertDialog.Root` runs, satisfies every DLG requirement in [dialog](dialog.md), with the substitutions below. The exceptions are the DLG IDs under "Not adopted" (they describe regimes alert mode makes unreachable) and under "Overridden" (replaced by the ADL requirement named). The §13 matrix of this document cites adopted DLG IDs directly. Server: relaxed, see §9.
  Source: `alert-dialog/root/AlertDialogRoot.tsx:14-16`; `alert-dialog/index.parts.ts:1-10`; `dialog/root/useRenderDialogRoot.tsx:35-39`. Test: see §13.
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): §13's rows run in `alert-dialog.spec.ts` (the three shared suites' AlertDialog runs and `AlertDialogRoot.test.tsx`'s browser titles, both modes) and at [L2] in `AlertDialogRootTests`; the Dialog rows ADL-API-02 and ADL-DOM-01 name run as port titles under the AlertDialog parts (§13's port note).

Substitutions:

| Where DLG says | Read |
|---|---|
| `Dialog.<Part>` | `AlertDialog.<Part>` |
| `Dialog.Root` / "the Root" | `AlertDialog.Root` (mode `'alert-dialog'`) |
| `Dialog.Handle` / `Dialog.createHandle()` | `AlertDialog.Handle` / `AlertDialog.createHandle()` (ADL-API-06) |
| `Dialog.Root.ChangeEventDetails` / `…Reason` / `…Actions` / `…Props` / `…State` | `AlertDialog.Root.ChangeEventDetails` / `…Reason` / `…Actions` / `…Props` / `…State` (same shapes; Props without `modal`/`disablePointerDismissal`) |
| "the `modal` value" / `modal === true` / `modal !== false` | always `true` (ADL-API-02) |
| `disablePointerDismissal` | always `true` (ADL-API-03) |
| "`role="dialog"` (`"alertdialog"` in alert mode)" | `role="alertdialog"` (ADL-ARIA-01) |
| an outside press "dismissing per DLG-PTR-10" (in DLG-PTR-18) | an outside press requesting nothing (ADL-DIS-01) |
| diagnostic texts | unchanged: they still name `Dialog` (ADL-EDGE-01) |

Not adopted (unreachable in alert mode):

| DLG IDs | Why |
|---|---|
| DLG-KEY-08 | `modal: false` Tab-out and `focus-out` close |
| DLG-FOC-11 | Return focus after an `outside-press` close; such closes cannot occur |
| DLG-FOC-13 | `modal: false` outside focus guards and `tabindex` stashing |
| DLG-FOC-14 | Non-modal focus-out close |
| DLG-PTR-05, DLG-PTR-07…12, DLG-PTR-14…17 | Outside-press event mode and target qualification; the outside-press decision is always "no" before any of these rules matter (ADL-DIS-01). The `outsidePressEnabledRef` gate of DLG-PTR-05/11 is never closed for an AlertDialog (only `Drawer.SwipeArea` clears it), so it never changes the result |
| DLG-DIS-04, DLG-DIS-05 | Sibling-modal backdrop dismissal, and nested-popup backdrops not dismissing the dialog; both are vacuous because no outside press dismisses an AlertDialog |
| DLG-DIS-06 (outside-press clauses) | Its last clause still applies: closing a nested dialog inside a closed shadow root leaves the parent open |
| DLG-DIS-10 (drawer-mode sentence) | Describes the `'drawer'` mode flag; an AlertDialog runs mode `'alert-dialog'` (`isDrawer` false) and never reports itself as a drawer (ADL-STA-02) |

Overridden:

| Base ID | Replaced by |
|---|---|
| DLG-STR-13 | ADL-STR-01 |
| DLG-API-03 | ADL-API-02 |
| DLG-API-06 | ADL-API-03 |
| DLG-API-08, DLG-API-12 (handle type) | ADL-API-04, ADL-API-05 |
| DLG §3.10 (`createHandle`) | ADL-API-06 (members still DLG-API-24…27) |
| DLG-STA-01, DLG-STA-02 | ADL-STA-01 |
| DLG-ARIA-04 | ADL-ARIA-01 |
| DLG-PTR-06 | ADL-DIS-01 (the primary-button filter runs but has no observable effect) |
| DLG-FOC-15 (recovery clause; its final `disablePointerDismissal: true` sentence is the rule that applies) | ADL-FOC-03 |
| DLG-FOC-16 (backdrop-press clause) | ADL-FOC-04 |
| DLG-DIS-01 | ADL-DIS-01 (outside presses), ADL-FOC-02…04 (the `closeOnFocusOut: false` effects) |
| DLG-DIS-10 (alert-mode clause) | Restated by ADL-API-02/03 (forcing), ADL-DIS-01 (no outside-press close), ADL-FOC-02 (no focus-out close), ADL-DIS-02 (remaining close paths) and ADL-ARIA-01 (role); the drawer-mode sentence is not adopted (above) |
| DLG-EVT-02 | ADL-EVT-02 |
| DLG §3.11 types | §3.5 of this document |

## 3. Public API

### 3.1 `AlertDialog.Root`

**Default element:** none. **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no (`AlertDialog.Root.State` is empty).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `open` | `boolean \| undefined` | `undefined` | Consumer-owned open state. | DLG-API-01 |
| `defaultOpen` | `boolean` | `false` | Initial open state. | DLG-API-02 |
| `onOpenChange` | `(open: boolean, eventDetails: AlertDialog.Root.ChangeEventDetails) => void` | `undefined` | Open-state change request (§7). | ADL-EVT-01 |
| `onOpenChangeComplete` | `(open: boolean) => void` | `undefined` | After enter/exit animations. | DLG-API-05 |
| `actionsRef` | `RefObject<AlertDialog.Root.Actions \| null>` | `undefined` | `{ unmount, close }`. | DLG-API-07 |
| `handle` | `AlertDialog.Handle<Payload> \| undefined` | `undefined` | Binds the Root to a handle. | ADL-API-04 |
| `children` | node, or `({ payload }) => node` | — | Content or payload function. | DLG-API-09 |
| `triggerId` | `string \| null \| undefined` | `undefined` | Consumer-owned active trigger id. | DLG-API-10 |
| `defaultTriggerId` | `string \| null \| undefined` | `null` | Initial active trigger id. | DLG-API-11 |
| ~~`modal`~~ | — | forced `true` | Not part of the contract; a runtime value is ignored. | ADL-API-02 |
| ~~`disablePointerDismissal`~~ | — | forced `true` | Not part of the contract; a runtime value is ignored. | ADL-API-03 |

Nothing is forwarded to an element.

- **ADL-API-01** — MUST. `AlertDialog.Root` accepts exactly the `Dialog.Root` parameters minus `modal` and `disablePointerDismissal`. `onOpenChange` and `actionsRef` are re-typed to the AlertDialog detail/action types (same shapes) and `handle` to `AlertDialog.Handle`. Each remaining parameter keeps its Dialog default and semantics.
  Source: `alert-dialog/root/AlertDialogRoot.tsx:20-43`; `dialog/root/DialogRoot.tsx:24-91`. Test: [L2] `AlertDialogRoot.spec.tsx` (type-level); popupConformanceTests (`expectedPopupRole: 'alertdialog'`).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); plan §Settled, type-level rows as L1 reflection tests): the type-level clause runs at [L1], `AlertDialogApiTests` "port: AlertDialogRoot declares DialogRoot's parameters and actions except Modal and DisablePointerDismissal, with the same types and defaults"; popupConformanceTests runs at [L4] in `alert-dialog.spec.ts`, both modes.
- **ADL-API-02** — MUST. The effective modality of an AlertDialog is always `true`, whatever value (if any) reaches the root routine at run time, and no diagnostic is emitted for a supplied value. Therefore: focus is trapped (DLG-KEY-06/07, DLG-FOC-12); while open, everything outside is `aria-hidden` (DLG-ARIA-11); while mounted, the internal backdrop is rendered (DLG-STR-10, DLG-DOM-06, DLG-PTR-18); while open, document scroll is locked (DLG-DOM-26/27). `'trap-focus'` and `false` cannot be selected.
  Source: `dialog/root/useRenderDialogRoot.tsx:28,36-37`; `dialog/root/useDialogRoot.ts:87`; `dialog/popup/DialogPopup.tsx:105`; `dialog/portal/DialogPortal.tsx:36-38`. Test: [L2] `AlertDialogRoot.test.tsx` "enforces alert dialog state for handle-backed roots" (`data-modal="true"`), "makes other interactive elements on the page inert when a modal dialog is open" (internal backdrop present) (L4 for the focus-trap, outside `aria-hidden` and scroll-lock clauses).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); ADR 08 Amendments 2026-10-27, ADL §14): a dated deviation. `AlertDialogRoot`'s parameter list is strict, so a `Modal` value cannot be supplied (it does not compile), in place of "a runtime value is ignored, no diagnostic"; the store in `DialogMode.AlertDialog` forces modality at every parameter set. Test: [L2] `AlertDialogRootTests` "enforces alert dialog state for handle-backed roots" (a probe of the internal dialog context, `data-modal="true"`), "port: an alert dialog's session never dismisses on an outside press, and its focus manager is modal and never closes on focus-out", "port: an alert dialog renders its internal backdrop and locks page scroll whenever it is open, with or without a backdrop part"; [L4] "makes other interactive elements on the page inert when a modal dialog is open" and the forced-regime port titles (§13).
- **ADL-API-03** — MUST. The effective `disablePointerDismissal` of an AlertDialog is always `true`, whatever value reaches the root routine at run time, with no diagnostic. Outside presses never request a close (ADL-DIS-01), and the focus manager runs with focus-out closing disabled (ADL-FOC-02…04).
  Source: `dialog/root/useRenderDialogRoot.tsx:27,38`; `dialog/root/useDialogRoot.ts:67`; `dialog/popup/DialogPopup.tsx:102`. Test: [L2] `AlertDialogRoot.test.tsx` "enforces alert dialog state for handle-backed roots" (`data-disable-pointer-dismissal="true"`).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); ADR 08 Amendments 2026-10-27, ADL §14): a dated deviation, as ADL-API-02's: `DisablePointerDismissal` cannot be supplied. Test: [L2] `AlertDialogRootTests` "enforces alert dialog state for handle-backed roots" (`data-disable-pointer-dismissal="true"`), "port: an alert dialog's session never dismisses on an outside press, and its focus manager is modal and never closes on focus-out" (`outsidePress: false`, `closeOnFocusOut: false`).
- **ADL-API-04** — MUST. `handle` accepts an `AlertDialog.Handle`. At run time it behaves exactly as DLG-API-08 (binding for the Root's mounted lifetime; interactions kept mounted while closed, PSH-STA-43). The type rejects a `Dialog.Handle`, but nothing checks it at run time (ADL-EDGE-02).
  Source: `alert-dialog/root/AlertDialogRoot.tsx:37-42`; `dialog/root/useRenderDialogRoot.tsx:30,92,96`. Test: [L2] `AlertDialogRoot.spec.tsx` "Dialog handles cannot be used for alert dialogs" (`@ts-expect-error`); detachedTriggersConformanceTests (`createHandle: AlertDialog.createHandle`).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); plan §Settled): the type-level clause runs at [L1], `AlertDialogApiTests` "port: the AlertDialog parts take an AlertDialogHandle, which a DialogHandle is not, and the Dialog parts accept one"; detachedTriggersConformanceTests runs at [L4] with `new AlertDialogHandle<string>()`.

#### State object, data attributes, CSS variables

`AlertDialog.Root.State` is `{}`; no data attributes or CSS variables.

### 3.2 `AlertDialog.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (`AlertDialog.Trigger.State` = `Dialog.Trigger.State` = `{ disabled, open }`, DLG-STA-12).

Props, state, data attributes (`data-popup-open`, `data-disabled`, internal `data-base-ui-click-trigger`) and requirements are those of `Dialog.Trigger` ([dialog](dialog.md) §3.2: DLG-API-12…16, DLG-DOM-01…04, DLG-ARIA-01…03), except the `handle` type.

| Prop | Type | Default | Req |
|---|---|---|---|
| `handle` | `AlertDialog.Handle<Payload> \| undefined` | `undefined` | ADL-API-05 |
| `payload` | `Payload \| undefined` | `undefined` | DLG-API-13 |
| `id` | `string \| undefined` | generated | DLG-API-14 |
| `nativeButton` | `boolean` | `true` | DLG-API-15 |
| `disabled` | `boolean` | `false` (not in the outline, §16 Q6) | DLG-API-16 |
| `className`, `style`, `render` | per RND | — | — |

- **ADL-API-05** — MUST. `AlertDialog.Trigger` is the `Dialog.Trigger` component, so the rendered element, the state and the behavior are identical. Its `handle` parameter is typed `AlertDialog.Handle<Payload>`, and `payload` is typed by that handle's `Payload`. At run time the handle is used exactly as in DLG-API-12.
  Source: `alert-dialog/trigger/AlertDialogTrigger.tsx:13-35`; `dialog/trigger/DialogTrigger.tsx:22-61`. Test: [L2] `AlertDialogRoot.spec.tsx` (`payload={42}` accepted, `payload={'invalid'}` and a Dialog handle rejected); detachedTriggersConformanceTests.
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); plan §Settled): the type-level clauses run at [L1], `AlertDialogApiTests` "port: AlertDialogTrigger declares DialogTrigger's parameters with the same types and defaults" and the handle-type title; detachedTriggersConformanceTests runs at [L4].

### 3.3 Re-exported parts

`Portal`, `Backdrop`, `Viewport`, `Popup`, `Title`, `Description` and `Close` have the props, state objects, data attributes, CSS variables and requirements of [dialog](dialog.md) §3.3–§3.9 (ADL-STR-02). Dialog-specific notes:

- `AlertDialog.Backdrop` is optional. Without it the internal backdrop still covers the page whenever the dialog is mounted (ADL-API-02). Pressing either backdrop does nothing (ADL-DIS-01). The `forceRender` / nested rule DLG-STR-05 is unchanged.
- `AlertDialog.Popup`'s `initialFocus`/`finalFocus` keep the Dialog defaults (ADL-FOC-01).
- The outline omits the same native attributes for these parts as it does for Dialog (§16 Q6).

### 3.4 `AlertDialog.createHandle()` / `AlertDialog.Handle`

- **ADL-API-06** — MUST. `AlertDialog.createHandle<Payload>()` returns a new `AlertDialog.Handle<Payload>`, a subclass of `Dialog.Handle<Payload>` that adds no runtime members. Its `open(triggerId | null)`, `openWithPayload(payload)`, `close()` and `isOpen` behave exactly as DLG-API-24…27. Its detached-call diagnostics are the Dialog ones, naming `DialogHandle` (DLG-EDGE-04…06; ADL-EDGE-01).
  Source: `alert-dialog/handle.ts:11-22`; `dialog/store/DialogHandle.ts:15-74`. Test: [L2] `popupHandle.test.ts` "$component handle is closed and names itself in warnings while no root is attached" (AlertDialog row, `warningName: 'Dialog'`), "$component handle ignores openWithPayload() with a warning while detached" (AlertDialog row); `dialogRootSharedTests.tsx` "opens with a payload set programmatically". Docs: components/alert-dialog/page.mdx §Detached triggers.
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): `AlertDialogHandle<TPayload> : DialogHandle<TPayload>`, created with `new`, declares no member. Its warnings name `DialogHandle`: `DialogHandle`'s mode constructor passes `Dialog` in every mode and the mode only picks the fallback store (settled by the orchestrator 2026-10-29, ADR 04 Amendments 2026-10-29). Test: [L1] `AlertDialogApiTests` "port: AlertDialogHandle derives from DialogHandle, is created with new and declares no member of its own", and `popupHandle.test.ts`'s two AlertDialog rows at L1, as the shared handle's titles run ("AlertDialog handle is closed and names itself in warnings while no root is attached", "AlertDialog handle ignores openWithPayload() with a warning while detached"); dialogRootSharedTests "opens with a payload set programmatically" at [L4].

### 3.5 Base-component parameters overridden by this component

| Base part / prop | Base default or meaning | Under AlertDialog |
|---|---|---|
| `Dialog.Root` `modal` | `true`; also `false` / `'trap-focus'` | Removed from the type; always `true` (ADL-API-02) |
| `Dialog.Root` `disablePointerDismissal` | `false` | Removed from the type; always `true` (ADL-API-03) |
| `Dialog.Root` `handle` / `Dialog.Trigger` `handle` | `Dialog.Handle` | `AlertDialog.Handle` (ADL-API-04/05) |
| `Dialog.Popup` `role` | `"dialog"` | `"alertdialog"` (ADL-ARIA-01) |
| `Dialog.Popup` focus manager `closeOnFocusOut` | `!disablePointerDismissal` (`true` by default) | always `false` (ADL-FOC-02) |

### 3.6 Shared types

- `AlertDialog.Root.ChangeEventReason` = `Dialog.Root.ChangeEventReason` = `'trigger-press' | 'outside-press' | 'escape-key' | 'close-press' | 'focus-out' | 'imperative-action' | 'none'`. `'outside-press'`, `'focus-out'` and `'none'` are declared but never emitted (ADL-EVT-02). Source: `alert-dialog/root/AlertDialogRoot.tsx:47`; `dialog/root/DialogRoot.tsx:98-105`.
- `AlertDialog.Root.ChangeEventDetails` = cancelable change details (EVD-API-01) over that reason set plus `preventUnmountOnClose(): void` (EVD-API-09). Source: `alert-dialog/root/AlertDialogRoot.tsx:48-52`.
- `AlertDialog.Root.Actions` = `Dialog.Root.Actions` = `{ unmount(): void; close(): void }`. Source: `alert-dialog/root/AlertDialogRoot.tsx:45`.
- `AlertDialog.Root.State` = `{}`; `AlertDialog.Root.Props<Payload>`; `AlertDialog.Trigger.Props<Payload>` / `State` (`State` extends `Dialog.Trigger.State`). Flat aliases `AlertDialogBackdropProps`/`State`, `AlertDialogCloseProps`/`State`, `AlertDialogDescriptionProps`/`State`, `AlertDialogPopupProps`/`State`, `AlertDialogPortalProps`/`State`, `AlertDialogTitleProps`/`State` and `AlertDialogViewportProps`/`State` are the Dialog types. Source: `alert-dialog/root/AlertDialogRoot.tsx:18-60`; `alert-dialog/trigger/AlertDialogTrigger.tsx:15-35`; `alert-dialog/index.ts:3-33`.
- `AlertDialog.Handle<Payload>` is a class. `createHandle<Payload>()` types every trigger's `payload` and the Root function-child's `payload` (`Payload | undefined`). Test: [L1] `AlertDialogRoot.spec.tsx`.

## 4. State model

The open state, the trigger association, the payload, the controlled/uncontrolled rules, the open-change sequence and the state diagram are those of [dialog](dialog.md) §4 (DLG-STA-03…15, PSH §4.3, EVD §4.2), adopted unchanged.

- **ADL-STA-01** — MUST. An AlertDialog store is created with, and always holds, `modal: true`, `disablePointerDismissal: true` and `role: 'alertdialog'`. These three fields do not follow consumer props: they are re-applied from the forced values post-commit, pre-paint each time the Root's parameters are applied. This holds for contained, controlled and handle-backed Roots, and survives a Root that is unmounted while open and remounted on the same handle. Every other initial value is as in DLG-STA-01.
  Source: `dialog/root/useRenderDialogRoot.tsx:35-43,49-63,68`; `dialog/store/DialogStore.ts:113-135`. Test: [L2] `AlertDialogRoot.test.tsx` "enforces alert dialog state for handle-backed roots", "rewires the trigger and keeps pointer dismissal disabled after the root remounts while open".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): the store in `DialogMode.AlertDialog` re-applies the three values at every parameter set (`DialogStore.ApplyRootSettings`). Test: [L2] `AlertDialogRootTests` "enforces alert dialog state for handle-backed roots" (the probe), "rewires the trigger and keeps pointer dismissal disabled after the root remounts while open"; the backdrop presses of both titles at [L4] in `alert-dialog.spec.ts`, both modes.
- **ADL-STA-02** — MUST. A nested AlertDialog reports itself to its parent as a dialog, never as a drawer: open, it reports `(ownNestedOpenDialogCount + 1, ownNestedOpenDrawerCount + 0)` (DLG-STA-05). An AlertDialog opened inside a nested Drawer therefore does not raise any Drawer's nested-drawer count.
  Source: `dialog/root/useRenderDialogRoot.tsx:35,101`; `dialog/root/useDialogRoot.ts:109-123`. Test: [L2] `DrawerPopup.test.tsx` "does not treat alert dialogs inside nested drawers as nested drawers"; `DialogPopup.test.tsx` "increments for nested alert dialog and decrements on close (cross-type)".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); ADR 13 Amendments 2026-10-22): the Drawer clause (`DrawerPopup.test.tsx` "does not treat alert dialogs inside nested drawers as nested drawers") is open to M6 (Drawer) and does not hold the document's `implemented` status; the dialog clause runs at [L2] in `AlertDialogNestingTests`.

## 5. Rendered DOM and ARIA

### 5.1 Elements

Every element, attribute and data attribute is as in [dialog](dialog.md) §5.1–§5.7 (DLG-DOM-01…28), with modality fixed at `true`.

- **ADL-DOM-01** — MUST. Because modality is always `true` (ADL-API-02): the internal backdrop (`role="presentation"`, `data-base-ui-inert`, fixed full-viewport) is the first child of the portal node whenever the dialog is mounted, and is `inert` during the exit transition (DLG-STR-10, DLG-DOM-06). Document scroll is locked from the open commit until the close request (DLG-DOM-26/27). Both hold whether or not an `AlertDialog.Backdrop` is rendered.
  Source: `dialog/portal/DialogPortal.tsx:36-38`; `dialog/root/useDialogRoot.ts:87`. Test: [L2] `AlertDialogRoot.test.tsx` "makes other interactive elements on the page inert when a modal dialog is open", "does not close when the backdrop is clicked" (internal backdrop found by `role="presentation"`) (L4 for the exit-transition `inert` and scroll-lock clauses).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L2] `AlertDialogRootTests` "port: an alert dialog renders its internal backdrop and locks page scroll whenever it is open, with or without a backdrop part"; [L4] `alert-dialog.spec.ts` "makes other interactive elements on the page inert when a modal dialog is open", "does not close when the backdrop is clicked", "port: the internal backdrop is inert while the alert dialog animates out", "port: an alert dialog locks page scroll from its open until its close (backdrop part: false|true)", both modes.

Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); design point 2, settled by the orchestrator 2026-10-29, ADR 04 Amendments 2026-10-29; the M4 ownership rows of ADR 04 Amendments 2026-10-27): the three animated parts' transition attributes restate dialog.md's Writer cells here, because the ownership lint checks each document on its own and the port's AlertDialog state records mirror the transition status. Every other element row is dialog.md's (§5.1–§5.7).

### 5.1.1 Backdrop element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-starting-style` / `data-ending-style` | transition status | DLG-DOM-10 | JS |

### 5.1.2 Viewport element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-starting-style` / `data-ending-style` | transition status | DLG-DOM-14 | JS |

### 5.1.3 Popup element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `data-starting-style` / `data-ending-style` | transition status | DLG-DOM-20 | JS |

### 5.2 ARIA

| Element | Attribute | Value | Req | Writer |
|---|---|---|---|---|
| Trigger | `aria-haspopup` | `"dialog"` (not `"alertdialog"`) | ADL-ARIA-02 | C# |
| Trigger | `aria-expanded`, `aria-controls` | as Dialog | DLG-ARIA-02/03, ADL-ARIA-04 | C# |
| Popup | `role` | `"alertdialog"` | ADL-ARIA-01 | C# |
| Popup | `aria-labelledby` / `aria-describedby` | Title / Description id | DLG-ARIA-05/06 | C# |
| Popup | `aria-modal` | never rendered | ADL-ARIA-01 | C# |
| outside elements | `aria-hidden="true"` | always while open | ADL-ARIA-03 | JS |

- **ADL-ARIA-01** — MUST. The Popup renders `role="alertdialog"`, taken from the store's `role` (ADL-STA-01). A consumer `role` still overrides it (DLG-DOM-28). No `aria-modal` attribute is rendered.
  Source: `dialog/root/useRenderDialogRoot.tsx:39`; `dialog/popup/DialogPopup.tsx:42,79`. Test: [L2] popupConformanceTests "has the alertdialog role on the popup"; `AlertDialogRoot.test.tsx` "ARIA attributes", "enforces alert dialog state for handle-backed roots" (`data-role="alertdialog"`); every `dialogRootSharedTests.tsx` test (queries by `popupRole: 'alertdialog'`).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L2] `AlertDialogRootTests` "ARIA attributes", "enforces alert dialog state for handle-backed roots"; popupConformanceTests "has the alertdialog role on the popup" and dialogRootSharedTests at [L4], both modes.
- **ADL-ARIA-02** — MUST. The Trigger renders `aria-haspopup="dialog"`, the same value as `Dialog.Trigger` (DLG-ARIA-01). Alert mode does not change it to `"alertdialog"`.
  Source: `dialog/trigger/DialogTrigger.tsx:93`. Test: [L2] popupConformanceTests "has the `aria-haspopup` attribute on the trigger" (`expectedAriaHasPopupValue: 'dialog'`).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): popupConformanceTests runs at [L4] (`expectedAriaHasPopupValue: 'dialog'`), both modes; [L2] `AlertDialogRootTests` "ARIA attributes" asserts it too.
- **ADL-ARIA-03** — MUST. While the AlertDialog is open, outside elements carry `aria-hidden="true"` per DLG-ARIA-11. Since modality is always `true` there is no configuration in which they stay exposed. `inert` is never set on them (DLG-ARIA-12).
  Source: `dialog/popup/DialogPopup.tsx:105`; `floating-ui-react/components/FloatingFocusManager.tsx:620-663`. Test: none directly (DLG-ARIA-11 evidence).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L4] `alert-dialog.spec.ts` "port: an open alert dialog hides the rest of the page from assistive technology except a live region, and marks nothing inert", both modes.
- **ADL-ARIA-04** — MUST. A Root rendered with `handle`, `defaultOpen` and `defaultTriggerId="<id>"` whose matching Trigger is mounted gives that Trigger `aria-expanded="true"` and `aria-controls="<popup id>"` after the initial client render. This holds whether the Trigger is inside the Root or a detached Trigger rendered *before* the Root, and `handle.isOpen` is `true` from that point.
  Source: `dialog/root/useRenderDialogRoot.tsx:49-63,96`; `dialog/trigger/DialogTrigger.tsx:49-50,94-95`; `utils/popups/popupStoreUtils.ts:343-363`. Test: [L2] `AlertDialogRoot.test.tsx` "synchronizes trigger ARIA attributes when initially open with a handle", "synchronizes detached trigger ARIA attributes when initially open with a handle".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): both titles run at [L2] in `AlertDialogRootTests` (verbatim).

## 6. Interactions

Trigger clicks, Close clicks and the focus manager follow [dialog](dialog.md) §6. The tables below list only what alert mode makes observable.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Escape</kbd> | open and topmost; focus anywhere | Close request, `escape-key` (not blocked by alert mode) | ADL-KEY-01 (DLG-KEY-01…05, DLG-KEY-11) |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | focus inside popup | Always wraps inside the popup; never leaves it, never closes | ADL-KEY-02 (DLG-KEY-06/07) |
| Arrow keys, <kbd>Home</kbd>, <kbd>End</kbd> | keydown inside popup | Propagation stopped unconditionally (counterpart of [toolbar](toolbar.md) TLB-KEY-15) | DLG-KEY-09 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | Trigger or Close focused | Activates (open / `close-press`) | DLG-KEY-10 |
| other keys | — | Not intercepted | — |

- **ADL-KEY-01** — MUST. Alert mode does not disable <kbd>Escape</kbd>. The Root passes `escapeKey: isTopmost` to the dismissal mechanism exactly as Dialog does, so an Escape keydown while the AlertDialog is open and topmost requests a close with `reason: 'escape-key'`. Default-prevention, propagation, IME, nesting and controlled-mode behavior are per DLG-KEY-01…05 and DLG-KEY-11. Server: relaxed, see §9.
  Source: `dialog/root/useDialogRoot.ts:84`; `floating-ui-react/hooks/useDismiss.ts:201-232`. Test: [L4] popupConformanceTests "opens initially and remains uncontrolled" (Escape closes), "is called with the new open state and the change details" (`escape-key`), "is ignored when `open={true}` is passed, so Escape cannot close"; `ToolbarButton.test.tsx` "AlertDialog › handles interactions".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L4] popupConformanceTests in `alert-dialog.spec.ts` and `ToolbarButton.test.tsx` "AlertDialog › handles interactions" in `toolbar.spec.ts`, both modes; §9's Escape row per mode: "port: allowPropagation lets the Escape keydown reach the window on WebAssembly, and the Server fallback stops it while the alert dialog closes", "port: a canceled Escape close of an alert dialog is not prevented on WebAssembly, and on Server the fallback prevented it before the cancel arrived".
- **ADL-KEY-02** — MUST. Because modality is always `true`, <kbd>Tab</kbd>/<kbd>Shift</kbd>+<kbd>Tab</kbd> inside the Popup always cycle within it (DLG-KEY-06), or are default-prevented when it has no tabbable content (DLG-KEY-07). No key sequence moves focus out of the Popup or produces a `focus-out` close.
  Source: `dialog/popup/DialogPopup.tsx:102,105`; `floating-ui-react/components/FloatingFocusManager.tsx:343-363,1003-1052`. Test: none in the AlertDialog suite (DLG-KEY-06 evidence).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L4] `alert-dialog.spec.ts` "port: Tab and Shift+Tab cycle inside an open alert dialog and never leave it", "port: Tab in an alert dialog with no tabbable content keeps focus on the popup", both modes.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| Click on a Trigger | closed / open (active) / open (inactive) | Open / close (toggle) / switch, `trigger-press` | DLG-PTR-01…04 |
| Click on Close | open | Close request, `close-press` | DLG-PTR-19 |
| Any press outside the Popup (internal backdrop, `AlertDialog.Backdrop`, Viewport, page), any pointer type or button | open | Nothing: `onOpenChange` is not called | ADL-DIS-01 |
| Press on the page behind | mounted | Lands on the internal backdrop instead (DLG-PTR-18) | ADL-DOM-01 |

With modality forced to `true`, the internal backdrop covers registered Triggers outside the Popup, so the active-trigger toggle close (DLG-PTR-02) is reachable only for a Trigger placed above the backdrop or inside the Popup, or for a programmatic `click()`. The shared suite closes an AlertDialog this way through `user.click(trigger)` (§13).

### 6.3 Focus management

- **ADL-FOC-01** — MUST. The Popup uses the Dialog focus configuration with modality `true` (DLG-FOC-01…10, DLG-FOC-12, DLG-FOC-17, DLG-FOC-18). On open, focus moves into the Popup: to the Popup element when opened by touch, otherwise to the first tabbable element inside it, falling back to the Popup element. `initialFocus`/`finalFocus` override this per DLG-API-20/21. On close, focus returns to the active trigger or the previously focused element (DLG-FOC-06). Alert mode does not pick a "least destructive" control or any other special initial target.
  Source: `dialog/popup/DialogPopup.tsx:57-58,98-107`; `utils/popups/popupStoreUtils.ts:37-40`. Test: [L4] `ToolbarButton.test.tsx` "AlertDialog › handles interactions" (Popup with no tabbable content focused on Enter-open; focus back on the trigger after Escape), "AlertDialog › prevents composite keydowns from escaping" (Popup focused after click-open); `MenuRoot.test.tsx` "keeps focus in a nested alert dialog popup when the pointer leaves the triggering menu item"; `DialogRoot.test.tsx` "returns focus into the dialog when a close confirmation opened by an outside press closes" (AlertDialog confirmation's Close focused, then focus returned into the parent).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); ADR 13 Amendments 2026-10-22): `MenuRoot.test.tsx` "keeps focus in a nested alert dialog popup when the pointer leaves the triggering menu item" is incidental to this row (M5); its other citations carry it: `toolbar.spec.ts`'s AlertDialog block and `dialog.spec.ts` "returns focus into the dialog when a close confirmation opened by an outside press closes", both modes.
- **ADL-FOC-02** — MUST. The focus manager runs with focus-out closing disabled (`closeOnFocusOut: false`). Focus leaving the Popup, its portal node or the trigger never requests a close, so `focus-out` is never emitted.
  Source: `dialog/popup/DialogPopup.tsx:102`; `floating-ui-react/components/FloatingFocusManager.tsx:424-427,566`; `floating-ui-react/components/FloatingPortal.tsx:283-288`. Test: none.
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): the popup's focus manager runs with `closeOnFocusOut: false` in alert mode. Test: [L2] `AlertDialogRootTests` "port: an alert dialog's session never dismisses on an outside press, and its focus manager is modal and never closes on focus-out".
- **ADL-FOC-03** — MUST. Because focus-out handling is disabled entirely, the Popup's `restoreFocus: 'popup'` recovery (DLG-FOC-15) does not run. When the focused element inside the Popup is removed or hidden and focus falls to `<body>`, focus is **not** moved back to the Popup and stays on `<body>` while the AlertDialog remains open.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:424-427,505-529`; `dialog/popup/DialogPopup.tsx:102,106`. Test: none (see §16 Q1).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): §16 Q1 mirrored, as the brief settles: the `focus-manager` module keeps Base UI's grouping and skips the whole focus-out effect when `closeOnFocusOut` is false, so the recovery does not run. Test: none, as in Base UI.
- **ADL-FOC-04** — MUST. For the same reason, an element inside the Popup that loses focus to nothing (e.g. on a backdrop press) is **not** pushed onto the shared previously-focused stack (the second clause of DLG-FOC-16). The push of the element focused before each open (first clause of DLG-FOC-16) still happens, as does the use of the stack for default return focus.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:424-427,452-456,794`. Test: none.
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): §16 Q1 mirrored, as for ADL-FOC-03. Test: none, as in Base UI.

### 6.4 Dismissal

- **ADL-DIS-01** — MUST. No outside press closes an AlertDialog and none calls `onOpenChange`. This covers a press on the internal backdrop, on an `AlertDialog.Backdrop`, on the Viewport or any other ancestor of the Popup, on page content, or in another shadow root; any pointer type, button or touch count; and every intentional/sloppy timing. The outside-press decision returns "no request" for every press once the `outsidePressEnabledRef` gate (always open here, DLG-PTR-05) and the primary-button and single-touch filters have run. The dialog stays open and `handle.isOpen` stays `true`.
  Source: `dialog/root/useDialogRoot.ts:42-83` (line 67: `isTopmost && !disablePointerDismissal` is always false); `dialog/root/useRenderDialogRoot.tsx:38`. Test: [L4] `AlertDialogRoot.test.tsx` "does not close when the backdrop is clicked", "enforces alert dialog state for handle-backed roots", "rewires the trigger and keeps pointer dismissal disabled after the root remounts while open".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): the press itself is the `interactions` module's decision, so the three titles run at [L4] in `alert-dialog.spec.ts` (verbatim, pressing the internal backdrop) with "port: a press on the page behind an open alert dialog lands on its backdrop and closes nothing (backdrop part: false|true)" (left and right button), both modes; at [L2] the session's `outsidePress: false` in "enforces alert dialog state for handle-backed roots", "rewires the trigger and keeps pointer dismissal disabled after the root remounts while open" and "port: an alert dialog's session never dismisses on an outside press, and its focus manager is modal and never closes on focus-out".
- **ADL-DIS-02** — MUST. The ways to close an open AlertDialog are exactly: <kbd>Escape</kbd> while topmost (ADL-KEY-01, `escape-key`); a click or keyboard activation of a Close part (DLG-PTR-19, `close-press`); a click on the active Trigger (DLG-PTR-02, `trigger-press`); `actionsRef.current.close()` or `handle.close()` (DLG-DIS-09, `imperative-action`); and, in controlled mode, the owner setting `open` to `false` (no callback). Each request except the controlled-prop change is cancelable (DLG-DIS-07). In controlled mode a declined close leaves the dialog open with the trigger's `data-popup-open` and `handle.isOpen` intact (DLG-STA-14, DLG-DIS-08). Server: relaxed, see §9.
  Source: `dialog/root/useDialogRoot.ts:84`; `dialog/close/DialogClose.tsx:39-43`; `floating-ui-react/hooks/useClick.ts:102-124`; `dialog/root/useRenderDialogRoot.tsx:80-87`; `dialog/store/DialogHandle.ts:64-66`. Test: [L2] `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button", "unmounts the popup when the `unmount` method is called" (trigger toggle close), "closes the popup when the `close` method is called", "opens with a payload set programmatically" (`handle.close()`); popupConformanceTests "is called with the new open state and the change details" (Escape); `AlertDialogRoot.test.tsx` "keeps the trigger data-popup-open attribute and handle.isOpen when a controlled close is vetoed" (L4 for the Escape clause, relaxed on Server).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L2] `AlertDialogRootTests` "keeps the trigger data-popup-open attribute and handle.isOpen when a controlled close is vetoed"; dialogRootSharedTests and popupConformanceTests at [L4], both modes; the Escape clause's relaxation per mode as ADL-KEY-01's note lists.
- **ADL-DIS-03** — MUST. Nesting precedence is unchanged (DLG-DIS-03, DLG-STA-07). An AlertDialog with an open nested dialog does not handle Escape. A nested AlertDialog is topmost over its parent, so Escape closes only the AlertDialog. Closing a nested AlertDialog by any means, including inside open or closed shadow roots and custom portal containers, leaves the parent open without calling the parent's `onOpenChange`. A nested AlertDialog's `Backdrop` renders only with `forceRender` (DLG-STR-05).
  Source: `dialog/root/useDialogRoot.ts:28,84,91-123`; `dialog/backdrop/DialogBackdrop.tsx:48`. Test: [L4] `DialogRoot.test.tsx` "nested alert dialog › keeps the parent open on close (outer: $outer, nested: $nested)" (×5 portal setups); `DialogPopup.test.tsx` "increments for nested alert dialog and decrements on close (cross-type)".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); ADR 04 Amendments 2026-10-29, design point 10 of M4 track 8): Test: [L4] `dialog.spec.ts` "keeps the parent open on close (outer: …, nested: …)" for the alert dialog kind where the nested popup is outside the shadow root (two setups), and, where it sits in the shadow root (C# handlers do not fire there, so its Close does not run), "port: keeps the parent open when the nested alert dialog inside the shadow root closes on Escape, and a press on its backdrop leaves it open (outer: …, nested: …)" (three setups), both modes; [L2] `AlertDialogNestingTests` "increments for nested alert dialog and decrements on close (cross-type)".

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onOpenChange` | `(open: boolean, details: AlertDialog.Root.ChangeEventDetails) => void` | On every open/close request, synchronously, before any state change (DLG-EVT-01, DLG-EVT-10) | ADL-EVT-02 | yes |
| `onOpenChangeComplete` | `(open: boolean) => void` | As DLG-EVT-08/09 | — | no |

- **ADL-EVT-01** — MUST. `onOpenChange` has the signature, call timing, ordering, `trigger` reporting, cancelation and `preventUnmountOnClose()` semantics of DLG-API-04 and DLG-EVT-01, DLG-EVT-03…10, with `details` typed as `AlertDialog.Root.ChangeEventDetails` and reasons limited to ADL-EVT-02. Server: relaxed, see §9.
  Source: `alert-dialog/root/AlertDialogRoot.tsx:24-28,48-52`; `dialog/store/DialogStore.ts:72-95`. Test: [L2] `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button"; popupConformanceTests "is called with the new open state and the change details", "keeps the popup closed when the open change is canceled" (L4 for the `preventUnmountOnClose()` and exit-animation unmount clauses, relaxed on Server).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L4] dialogRootSharedTests and popupConformanceTests in `alert-dialog.spec.ts`, both modes; §9's TRN-ANI-03 row per mode: "port: an animated alert dialog popup is hidden at its exit end, and removed with onOpenChangeComplete(false) in a later batch, a round trip later on Server". A retained close's `'ending'` clause inherits DLG-EVT-05's open note (issue 147, track 6's gap 18; ADR 04 Amendments 2026-10-29).
- **ADL-EVT-02** — MUST. The reasons an AlertDialog emits are exactly:
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): Test: [L4] dialogRootSharedTests and popupConformanceTests, both modes; [L2] "port: an alert dialog's session never dismisses on an outside press, and its focus manager is modal and never closes on focus-out" (no `outside-press` request); [L4] "does not close when the backdrop is clicked".

  | Reason | `open` | Produced by |
  |---|---|---|
  | `trigger-press` | `true` / `false` | Click (pointer or keyboard-generated) on a Trigger: open, active-trigger toggle close, inactive-trigger switch (DLG-PTR-01…03) |
  | `escape-key` | `false` | <kbd>Escape</kbd> while topmost (ADL-KEY-01) |
  | `close-press` | `false` | Click on a Close part (DLG-PTR-19) |
  | `imperative-action` | `true` / `false` | `handle.open(id)`, `handle.openWithPayload(p)`; `handle.close()`, `actionsRef.current.close()` |
  | `outside-press` | — | Declared in the type; never emitted (ADL-DIS-01) |
  | `focus-out` | — | Declared in the type; never emitted (ADL-FOC-02) |
  | `none` | — | Declared in the type; never emitted (as Dialog, DLG §16 Q3) |

  Source: `alert-dialog/root/AlertDialogRoot.tsx:47`; `dialog/root/DialogRoot.tsx:98-105`; `dialog/root/useDialogRoot.ts:67`; `dialog/popup/DialogPopup.tsx:102`; `dialog/root/useRenderDialogRoot.tsx:84`. Test: [L2] `dialogRootSharedTests.tsx` (`trigger-press`, `close-press`, `imperative-action`); popupConformanceTests "is called with the new open state and the change details" (`trigger-press`, `escape-key`); `AlertDialogRoot.test.tsx` "does not close when the backdrop is clicked" (no `outside-press`) (L4 for the `escape-key` row).

## 8. Form integration

Not applicable: like Dialog (§8 of dialog.md), AlertDialog has no form value and takes no part in Field/Form state.

## 9. Lifecycle, mounting and animation

Adopted unchanged from [dialog](dialog.md) §9 (DLG-ANI-01…10): mount/unmount, `keepMounted`, transition statuses, `onOpenChangeComplete`, `actionsRef.unmount()` and `preventUnmountOnClose()`. Because modality is `true`, the internal backdrop's lifetime (DLG-ANI-01) is "whenever mounted" (ADL-DOM-01). The interaction layer is mounted iff `open || mounted || handle != null` (DLG-ANI-09), and alert-mode values are enforced in it on every remount (ADL-STA-01).

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- ADL-STR-02, ADL-EVT-01 (TRN-ANI-03, TRN-ANI-09, TRN-ANI-10 row, adopted through DLG-API-05, DLG-EVT-08, DLG-ANI-03) — unmount and `onOpenChangeComplete(false)` land in the first render batch after the completion message, about two round trips after the exit animation ends; JS sets `hidden` at animation end meanwhile (optimistic convergent write).
- ADL-STR-02 (TRN-STA-04, TRN-STA-05, TRN-STA-07, TRN-STA-09 row, adopted through DLG-ANI-02) — met on both modes: the next-frame `data-starting-style`/`data-ending-style` flip is JS-owned with C# mirroring; listed so it is not moved back to C#.
- ADL-STR-02 (RND-STA-01 row for mirrored fields, adopted through DLG-STA-11) — `transitionStatus` reaches the C# state (and so `ClassValue`/`StyleValue`) one round trip after JS writes `data-starting-style`/`data-ending-style`.
- ADL-KEY-01, ADL-DIS-02 (EVD-EVT-11, EVD-DIS-01, FLT-KEY-01 row, adopted through DLG-KEY-02, DLG-DIS-07) — the consumer's `cancel()` / `allowPropagation()` arrives a round trip after dispatch; JS prevents and stops Escape whenever this AlertDialog is the innermost open popup (ADR 04 §3 fallback); WebAssembly meets the spec through `decide()`.
- PSH-KEY-07 row (DLG-KEY-08 in dialog) — not reached: DLG-KEY-08 is not adopted (§2.1) and an AlertDialog never closes on focus-out (ADL-FOC-02), so no focus-guard close exists to relax.
- ADL-STR-02 (the row of a popup element the opening render creates, adopted through dialog.md §9; ADR 01 Amendments 2026-10-28) — the portal move, the initial focus, the trap, the outside marking and the scroll lock run when the Popup's handles attach from `OnAfterRender`: before the first paint on WebAssembly, after paint plus a round trip on Server. A kept-mounted popup has no gap.
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): the row is added as dialog.md §9 has it. Test: [L4] `alert-dialog.spec.ts` "port: a newly created alert dialog popup moves focus in, hides the outside and locks scroll after its opening batch, a round trip later on Server", both modes.

## 10. Positioning

Not applicable: AlertDialog is not anchored, as Dialog is not (dialog.md §10).

## 11. Cross-cutting dependencies

As [dialog](dialog.md) §11, with these alert-mode values:

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Dialog root routine | [dialog](dialog.md) | `useRenderDialogRoot('alert-dialog', props)` → `modal = true`, `disablePointerDismissal = true`, `role = 'alertdialog'`, `isDrawer = false` |
| Dismissal (`useDismiss`) | [floating-and-positioning](../architecture/floating-and-positioning.md) | `outsidePress(event)` always returns `false` (`!disablePointerDismissal` is false); `escapeKey: isTopmost` unchanged |
| Focus manager (`FloatingFocusManager`) | [floating-and-positioning](../architecture/floating-and-positioning.md) | `modal: true`, `closeOnFocusOut: false` (disables FLT-FOC-11 and the whole focus-out effect, including FLT-FOC-12 restore and the backdrop clause of FLT-FOC-09), `restoreFocus: 'popup'` (inert, see ADL-FOC-03) |
| Scroll lock | [shared-utilities](../architecture/shared-utilities.md) | `useScrollLock(open, popupElement)` (modality always `true`) |
| Handles | [popup-store-and-handles](../architecture/popup-store-and-handles.md) | `AlertDialogHandle extends DialogHandle` (`BasePopupHandle(…, 'Dialog', throwOnMissingTrigger: false)`) |

## 12. Edge cases, invariants and dev warnings

AlertDialog adds no diagnostics of its own. Every thrown error and dev warning is the Dialog one, emitted from Dialog code, with Dialog names in the text.

- **ADL-EDGE-01** — MUST. The diagnostics reachable through AlertDialog parts and handles keep their Dialog wording verbatim. They are not renamed for AlertDialog.

  | Condition | Message | Base ID |
  |---|---|---|
  | `AlertDialog.Trigger` without a Root or `handle` (throws, all environments) | `Base UI: <Dialog.Trigger> must be used within <Dialog.Root> or provided with a handle.` | DLG-EDGE-01 |
  | Portal/Backdrop/Viewport/Popup/Title/Description/Close without a Root (throws) | `Base UI: DialogRootContext is missing. Dialog parts must be placed within <Dialog.Root>.` | DLG-EDGE-02 |
  | Viewport/Popup without a Portal (throws) | `Base UI: <Dialog.Portal> is missing.` | DLG-EDGE-03 |
  | `handle.openWithPayload()` while detached (dev warning) | `Base UI: DialogHandle.openWithPayload() was called while no root using this handle is mounted. The call and its payload were ignored; mount a root with this handle before opening it imperatively.` | DLG-EDGE-04 |
  | `handle.open()` / `handle.close()` while detached (dev warnings) | `Base UI: DialogHandle.open() was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.` / `Base UI: DialogHandle.close() was called while no root using this handle is mounted. The call was ignored.` | DLG-EDGE-05 |
  | `handle.open(id)` with an unregistered id (dev warning; opens unassociated) | `Base UI: DialogHandle.open: No trigger found with id "<id>". The popup will open, but the trigger will not be associated with it.` | DLG-EDGE-06 |
  | Two Roots attached to one handle after one frame (dev warning) | PSH-EDGE-06 | DLG-EDGE-07 |
  | One trigger element under two ids (dev error) | PSH-EDGE-07 | DLG-EDGE-08 |

  Source: `dialog/trigger/DialogTrigger.tsx:41-45`; `dialog/root/DialogRootContext.ts:12-16`; `dialog/portal/DialogPortalContext.ts:6-12`; `dialog/store/DialogHandle.ts:20,46-51`; `utils/popups/popupHandle.ts:216-223,250-256,274-280`; `alert-dialog/handle.ts:11-15`. Test: [L2] `popupHandle.test.ts` "$component handle is closed and names itself in warnings while no root is attached" (AlertDialog → `DialogHandle`), "$component handle ignores openWithPayload() with a warning while detached" (AlertDialog row, exact message); detachedTriggersConformanceTests "opens unassociated with a warning when opened with an unregistered trigger id", "ignores imperative calls while no root is attached and follows the root across remounts".
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); ADR 08 Amendments 2026-10-27, chosen by the user 2026-10-27; §16 Q8 resolved): a dated deviation for the context errors, which name the AlertDialog parts: ``Blazix: `<AlertDialogTrigger>` must be used within `<AlertDialogRoot>` or provided with a handle.``, ``Blazix: The `AlertDialogRoot` context is missing. AlertDialog parts must be placed within `<AlertDialogRoot>`.``, ``Blazix: `<AlertDialogPortal>` is missing.`` Each is built from the family name the part passes, so a Dialog part keeps Dialog's message under an AlertDialog root (the portal message took the root's family until this track; corrected with its red, `AlertDialogPartTests`). The handle warnings keep Dialog's wording, naming `DialogHandle` (ADL-API-06's note). Test: [L2] `AlertDialogPartTests` (the diagnostics titles); [L1] `AlertDialogApiTests` (the handle rows); detachedTriggersConformanceTests at [L4].
- **ADL-EDGE-02** — MUST. Handle families are separated only in the types. An `AlertDialog.Root`/`AlertDialog.Trigger` given a `Dialog.Handle` at run time works exactly as with an `AlertDialog.Handle`, because the class adds no runtime members and the Root reads only the handle's shared contract. Alert mode is decided by the Root, not by the handle.
  Source: `alert-dialog/handle.ts:11-15` (`declare private readonly __alertDialogBrand: never` is type-only); `dialog/root/useRenderDialogRoot.tsx:30,96`. Test: [L2] `AlertDialogRoot.spec.tsx` (type-level rejection only).
  Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124); §16 Q9 resolved by the compiler, ADL §14): C# rejects a `DialogHandle<T>` on `AlertDialogRoot<T>.Handle` and `AlertDialogTrigger<T>.Handle` at compile time and accepts an `AlertDialogHandle<T>` on the Dialog parts, so the run-time interchangeability this row states is unreachable from AlertDialog's side. Test: [L1] `AlertDialogApiTests` "port: the AlertDialog parts take an AlertDialogHandle, which a DialogHandle is not, and the Dialog parts accept one", "port: AlertDialogHandle derives from DialogHandle, is created with new and declares no member of its own".

Other edge cases (disabled Trigger/Close, Suspense outside the Portal, handle swaps, remounts, server rendering, trigger ownership, inactive-trigger re-render avoidance, reading direction, and the IDREF effects of element substitution on Title/Description/Popup ids) are DLG-EDGE-09…18, adopted. For DLG-EDGE-16 (SHOULD), the AlertDialog suite asserts the stronger "zero re-renders of an inactive detached trigger across two open/close cycles" ("does not re-render inactive detached triggers when the popup opens and closes"). DLG-EDGE-12 is evidenced for AlertDialog by "rewires the trigger and keeps pointer dismissal disabled after the root remounts while open": a Root unmounted while open remounts closed, and the detached trigger re-associates on the next click.

## 13. Behaviors evidenced by tests

### `root/AlertDialogRoot.test.tsx`

All tests run with `globalThis.BASE_UI_ANIMATIONS_DISABLED = true` unless the suite overrides it.

| Test | Requirements |
|---|---|
| popupConformanceTests (`triggerMouseAction: 'click'`, `expectedPopupRole: 'alertdialog'`, `expectedAriaHasPopupValue: 'dialog'`; `exitAnimation` default `true`, not `alwaysMounted`): "controlled mode › opens the popup with the `open` prop" | DLG-API-01, DLG-STA-14 |
| popupConformanceTests "prop: defaultOpen › opens initially and remains uncontrolled" (closes on Escape) / "is ignored when `open={false}` is passed" / "is ignored when `open={true}` is passed, so Escape cannot close" | DLG-API-02, DLG-API-01, ADL-KEY-01, DLG-KEY-11, DLG-DIS-08 |
| popupConformanceTests "prop: onOpenChange › is called with the new open state and the change details" (`trigger-press` then `escape-key`) / "keeps the popup closed when the open change is canceled" | ADL-EVT-01, ADL-EVT-02, ADL-KEY-01, DLG-PTR-01, DLG-EVT-04, DLG-DOM-03 |
| popupConformanceTests "ARIA attributes › has the alertdialog role on the popup" | ADL-ARIA-01 |
| popupConformanceTests "has the `aria-controls` attribute on the trigger" / "opens on trigger click and sets `aria-expanded` on the trigger" / "allows a custom `id` prop" | DLG-ARIA-02, DLG-ARIA-03, DLG-DOM-03, DLG-DOM-16, DLG-STA-10 |
| popupConformanceTests "has the `aria-haspopup` attribute on the trigger" | ADL-ARIA-02 |
| popupConformanceTests "prop: onOpenChangeComplete" (5 tests, browser only: "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation", "is called on open once the enter animation finishes", "is called on close once the exit animation finishes") | DLG-API-05, DLG-EVT-08, DLG-ANI-03 |
| popupConformanceTests "animations › removes the popup when there is no exit animation defined" / "hides the kept-mounted popup once the exit animation finishes" (browser only) | DLG-ANI-02, DLG-ANI-03, DLG-ANI-04, DLG-API-17 |
| dialogRootSharedTests (`parts: AlertDialog`, `popupRole: 'alertdialog'`); see the next table | — |
| detachedTriggersConformanceTests (`createHandle: AlertDialog.createHandle`, Root/Trigger/Portal/Popup/Close from `AlertDialog`, no Positioner, no Viewport, `openInteractions: ['click']`, `ariaExpanded: true`, `throwOnMissingTrigger: false`; close interactions use the Close part, never Escape or outside press): "$mode triggers › multiple triggers › opens with any trigger on click and marks only that trigger as active" (×2: contained, detached; browser only) | DLG-PTR-01, DLG-PTR-19, DLG-ARIA-02, DLG-ANI-06, ADL-API-05 |
| detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it" / "reuses the popup DOM nodes when switching triggers" / "marks the trigger matching a controlled `triggerId` as active" / "opens initially with the payload of a non-first `defaultTriggerId`" / "allows controlling the open state and active trigger programmatically" (×2 each: contained, detached) | DLG-API-13, DLG-PTR-03, DLG-API-10, DLG-API-11, DLG-DOM-01, DLG-API-09 |
| detachedTriggersConformanceTests "detached triggers › imperative actions on the handle › opens by trigger id with its payload and closes" / "opens unassociated with a warning when opened with an unregistered trigger id" | ADL-API-06, DLG-API-24, DLG-API-26, DLG-API-27, ADL-EDGE-01 |
| detachedTriggersConformanceTests "root ownership › ignores imperative calls while no root is attached and follows the root across remounts" / "registers a detached trigger declared after the root" / "resolves a trigger still registered to the previous root during a transient overlap" | ADL-API-06, DLG-API-12, ADL-EDGE-01, DLG-EDGE-12 |
| detachedTriggersConformanceTests "reparenting › keeps a detached trigger working $name" (×4: wrappers removed, wrappers added, Fast Refresh-like handle recreation, reparented during recreation; browser only) | DLG-API-12, DLG-EDGE-11 |
| "ARIA attributes" | ADL-ARIA-01, DLG-ARIA-05, DLG-ARIA-06, DLG-DOM-23 |
| "synchronizes trigger ARIA attributes when initially open with a handle" / "synchronizes detached trigger ARIA attributes when initially open with a handle" | ADL-ARIA-04, DLG-API-11, DLG-API-27 |
| "does not re-render inactive detached triggers when the popup opens and closes" | DLG-EDGE-16, DLG-ANI-09, DLG-PTR-19 |
| "renders a viewport" | DLG-STR-06, DLG-DOM-11 |
| "prop: onOpenChange › does not close when the backdrop is clicked" | ADL-DIS-01, ADL-DOM-01, DLG-ARIA-10 |
| "prop: onOpenChange › keeps the trigger data-popup-open attribute and handle.isOpen when a controlled close is vetoed" | ADL-DIS-02, DLG-STA-14, DLG-DOM-03, DLG-API-27 |
| "handle-backed roots › rewires the trigger and keeps pointer dismissal disabled after the root remounts while open" | ADL-STA-01, ADL-DIS-01, DLG-EDGE-12, DLG-ARIA-03 |
| "handle-backed roots › enforces alert dialog state for handle-backed roots" | ADL-STA-01, ADL-API-02, ADL-API-03, ADL-ARIA-01, ADL-DIS-01 |
| "modality › makes other interactive elements on the page inert when a modal dialog is open" (browser only; asserts the internal backdrop exists) | ADL-API-02, ADL-DOM-01, DLG-STR-10 |

### `test/dialogRootSharedTests.tsx` (run with `parts: AlertDialog`, `popupRole: 'alertdialog'`)

| Test | Requirements |
|---|---|
| "prop: onOpenChange › reports the trigger and reason when opened by a trigger and closed by a close button" | ADL-EVT-01, ADL-EVT-02, ADL-DIS-02, DLG-EVT-03, DLG-PTR-01, DLG-PTR-19, DLG-STA-15 |
| "prop: actionsRef › unmounts the popup when the `unmount` method is called" (second close is a click on the trigger) | ADL-DIS-02, DLG-PTR-02, DLG-API-07, DLG-EVT-05, DLG-ANI-05, DLG-DOM-20 |
| "prop: actionsRef › clears manual unmount state after the `unmount` method is called" | DLG-EVT-05, DLG-ANI-05 |
| "prop: actionsRef › closes the popup when the `close` method is called" | ADL-DIS-02, DLG-API-07, DLG-DIS-09, ADL-EVT-02 |
| "imperative actions on the handle › opens with a payload set programmatically" | ADL-API-06, DLG-API-25, DLG-API-26, DLG-API-09, DLG-ARIA-02 |

### `root/AlertDialogRoot.spec.tsx`

Type-level only. `AlertDialog.createHandle<number>()` types `AlertDialog.Trigger`'s `payload` (`42` accepted, `'invalid'` rejected) and the Root function-child's `payload` as `number | undefined`. A `Dialog.createHandle<number>()` handle is rejected by both `AlertDialog.Root` and `AlertDialog.Trigger`. Covered by ADL-API-04, ADL-API-05, §3.6.

### Tests in other files that exercise this component

| File / test | Requirements |
|---|---|
| `dialog/popup/DialogPopup.test.tsx` "increments for nested alert dialog and decrements on close (cross-type)" | ADL-STR-03, ADL-STA-02, ADL-DIS-03, DLG-STA-04, DLG-DOM-19 |
| `dialog/popup/DialogPopup.test.tsx` "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent alert dialog" | ADL-STR-03, DLG-STA-03, DLG-DOM-21, DLG-DOM-22 |
| `dialog/root/DialogRoot.test.tsx` "nested alert dialog › keeps the parent open on close (outer: $outer, nested: $nested)" (×5 portal setups; nested AlertDialog with its Backdrop, closed by its Close part) | ADL-DIS-03, DLG-DIS-06 (nested-close clause), DLG-STR-04 |
| `dialog/root/DialogRoot.test.tsx` "returns focus into the dialog when a close confirmation opened by an outside press closes" / "does not leak a non-modal popup field into an unrelated dialog return focus" (AlertDialog as the controlled confirmation) | ADL-FOC-01, DLG-FOC-06, DLG-FOC-16 |
| `drawer/popup/DrawerPopup.test.tsx` "does not treat alert dialogs inside nested drawers as nested drawers" | ADL-STA-02 |
| `toolbar/button/ToolbarButton.test.tsx` "AlertDialog › handles interactions" | ADL-FOC-01, ADL-KEY-01, DLG-KEY-10, DLG-FOC-06 |
| `toolbar/button/ToolbarButton.test.tsx` "AlertDialog › prevents composite keydowns from escaping" | DLG-KEY-09, ADL-FOC-01 |
| `menu/root/MenuRoot.test.tsx` "keeps focus in a nested alert dialog popup when the pointer leaves the triggering menu item" | ADL-FOC-01, ADL-STR-04 |
| `utils/popups/popupHandle.test.ts` "$component handle is closed and names itself in warnings while no root is attached" (AlertDialog row) / "$component handle ignores openWithPayload() with a warning while detached" (AlertDialog row) | ADL-API-06, ADL-EDGE-01, DLG-API-27 |

ADL-ARIA-04 exists only because of the two "synchronizes … initially open with a handle" tests. Every other row maps to requirements stated elsewhere in this document or adopted from dialog.md.

Port note 2026-10-29 (M4 track 9, [issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)): the layers.
- `AlertDialogRoot.test.tsx`'s own titles that need no browser run at [L2] in `AlertDialogRootTests`, verbatim. Those that press a backdrop check there what the session was told, and run again at [L4] with the press; "modality" runs at [L4].
- The three shared suites run at [L4] in `alert-dialog.spec.ts`, both modes, on the `alert-dialog-conformance`, `alert-dialog-shared` and `alert-dialog-triggers` fixtures.
- The Dialog rows ADL-API-02 and ADL-DOM-01 name run as port titles under the AlertDialog parts, in `alert-dialog.spec.ts`'s "the regime alert mode forces" describe, both modes:
  - DLG-KEY-06 and FOC-12: the Tab cycle.
  - DLG-KEY-07: no tabbable content.
  - DLG-ARIA-11 and ARIA-12: the outside `aria-hidden` beside a live region, and nothing inert.
  - DLG-DOM-26 and DOM-27: the scroll lock, with and without a backdrop part, and after three third-party lockers.
  - DLG-DOM-06: the internal backdrop inert while the popup animates out.
  - DLG-PTR-18: a press behind lands on the backdrop.
- The cross-file rows:
  - `DialogPopup.test.tsx`'s two run at [L2] in `AlertDialogNestingTests`.
  - `DialogRoot.test.tsx`'s run in `dialog.spec.ts` and `ToolbarButton.test.tsx`'s in `toolbar.spec.ts`.
  - `popupHandle.test.ts`'s run at [L1].
  - `DrawerPopup.test.tsx`'s is open to M6 (ADL-STA-02); `MenuRoot.test.tsx`'s is re-cited (ADL-FOC-01).

## 14. React-specific notes and porting considerations

Non-normative.

- **One implementation, a mode flag.** A port does not need a separate AlertDialog component tree. Instantiating the Dialog root with an "alert" mode that overrides three store values (`modal`, `disablePointerDismissal`, `role`) reproduces every observable behavior here. The remaining differences are type-level: removed props and branded handles. Port: see decisions/05-state-store-and-event-details.md §1 (store, prop-to-store sync in `OnParametersSet`).
- **Memoization.** `Dialog.Root` is wrapped in the memoizing `fastComponent` factory. `AlertDialog.Root` is a plain function component that calls the same routine (`alert-dialog/root/AlertDialogRoot.tsx:14-16` vs `dialog/root/DialogRoot.tsx:16-20`). The inactive-trigger stability test still passes because it depends on the Trigger (`fastComponentRef`) and on the stable interaction props, not on Root memoization. No observable behavior depends on it. Port: see decisions/05-state-store-and-event-details.md §1 (one selector subscription per part, `StateHasChanged` only when a selection changed).
- **Type-only branding.** `declare private readonly __alertDialogBrand: never` produces no runtime field. A port in a nominally typed language can model `AlertDialog.Handle` as a distinct type wrapping or extending the Dialog handle. Runtime interchangeability (ADL-EDGE-02) need not be preserved. Port: see decisions/05-state-store-and-event-details.md §1 (`PopupHandle<TPayload>` handles).
- **Removed props at run time.** `modal` and `disablePointerDismissal` are omitted from the TypeScript props but still destructured by the shared routine and then overridden. A port with strict parameter lists can simply not accept them.
- **Focus-out effect gating.** The focus-out, restore-focus and backdrop-stack logic lives in one effect that is skipped when `closeOnFocusOut` is false (`FloatingFocusManager.tsx:424-427`). ADL-FOC-03/04 follow from that grouping, not from a deliberate alert-dialog rule. A port that splits the effect would diverge (§16 Q1). Port: see decisions/04-js-interop-boundary.md §1 (`focus-manager` module: focus-out close, focus restoration, return focus).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| ADL-STR-01 | MUST | Namespace parts and attribute-name exports | 2 |
| ADL-STR-02 | MUST | Adopts DLG with substitutions, not-adopted and overridden lists | 2.1 |
| ADL-STR-03 | MUST | Root = Dialog root routine in alert mode; cross-family nesting | 2 |
| ADL-STR-04 | MUST | Parts are the Dialog components; interchangeable across namespaces | 2 |
| ADL-API-01 | MUST | Root props = Dialog's minus `modal`/`disablePointerDismissal`; retyped | 3.1 |
| ADL-API-02 | MUST | Modality forced `true`; runtime value ignored silently | 3.1 |
| ADL-API-03 | MUST | `disablePointerDismissal` forced `true`; runtime value ignored silently | 3.1 |
| ADL-API-04 | MUST | Root `handle` is an `AlertDialog.Handle`; runtime as DLG-API-08 | 3.1 |
| ADL-API-05 | MUST | Trigger is `Dialog.Trigger` with AlertDialog-typed `handle` | 3.2 |
| ADL-API-06 | MUST | `createHandle`/`Handle` = Dialog handle subclass; same members | 3.4 |
| ADL-STA-01 | MUST | Store always `modal: true`, `disablePointerDismissal: true`, `role: 'alertdialog'` | 4 |
| ADL-STA-02 | MUST | Nested AlertDialog reports as a dialog, never a drawer | 4 |
| ADL-DOM-01 | MUST | Internal backdrop whenever mounted; scroll lock while open | 5.1 |
| ADL-ARIA-01 | MUST | Popup `role="alertdialog"`; no `aria-modal` | 5.2 |
| ADL-ARIA-02 | MUST | Trigger `aria-haspopup="dialog"` | 5.2 |
| ADL-ARIA-03 | MUST | Outside `aria-hidden` always while open | 5.2 |
| ADL-ARIA-04 | MUST | Initially-open handle Root syncs trigger `aria-expanded`/`aria-controls` | 5.2 |
| ADL-KEY-01 | MUST | Escape still closes (`escape-key`) when topmost | 6.1 |
| ADL-KEY-02 | MUST | Tab always trapped; never leaves or closes | 6.1 |
| ADL-FOC-01 | MUST | Dialog initial/return focus defaults, modal | 6.3 |
| ADL-FOC-02 | MUST | Focus-out never closes | 6.3 |
| ADL-FOC-03 | MUST | No refocus of Popup when focused child is removed | 6.3 |
| ADL-FOC-04 | MUST | No backdrop-press push onto previously-focused stack | 6.3 |
| ADL-DIS-01 | MUST | No outside press ever closes or calls `onOpenChange` | 6.4 |
| ADL-DIS-02 | MUST | Exhaustive list of close paths | 6.4 |
| ADL-DIS-03 | MUST | Nesting precedence and nested Backdrop rule unchanged | 6.4 |
| ADL-EVT-01 | MUST | `onOpenChange` contract as Dialog with AlertDialog types | 7 |
| ADL-EVT-02 | MUST | Emitted reasons; `outside-press`/`focus-out`/`none` never | 7 |
| ADL-EDGE-01 | MUST | Diagnostics verbatim with Dialog names | 12 |
| ADL-EDGE-02 | MUST | Handle families separated only by type | 12 |

Counts: STR 4, API 6, STA 2, DOM 1, ARIA 4, KEY 2, FOC 4, DIS 3, EVT 2, EDGE 2: 30 requirements, no gaps per `ADL-AREA`. PTR, FRM, ANI, POS, A11Y and I18N emit no IDs. Pointer behavior is adopted (DLG-PTR-01…04, 13, 18, 19) or overridden by ADL-DIS-01; lifecycle is adopted (§9); §8 and §10 are not applicable. All other behavior is the adopted DLG requirement set (ADL-STR-02).

## 16. Open questions

1. **Focus recovery disabled as a side effect (ADL-FOC-03/04).** `FloatingFocusManager.tsx:424-427` returns early from the whole focus-out effect when `closeOnFocusOut` is false, and `DialogPopup.tsx:102` passes `!disablePointerDismissal`. In alert mode, and for any Dialog with `disablePointerDismissal`, removing the focused control inside the popup leaves focus on `<body>` (no `restoreFocus: 'popup'` recovery), and backdrop presses do not feed the previously-focused stack. This looks unintended: `restoreFocus="popup"` is passed unconditionally at `DialogPopup.tsx:106`. No test covers it for AlertDialog. Resolved 2026-10-29 (M4 track 9): mirrored, as the brief settles, ADL-FOC-03/04 being MUST rows.
2. **Backdrop press moves focus to `<body>`.** A press on the internal or consumer backdrop of an open AlertDialog requests nothing (ADL-DIS-01), but browsers normally blur the focused control to `<body>` when a non-focusable element is pressed, and nothing re-focuses the popup (Q1). A following <kbd>Tab</kbd> from `<body>` then goes through the browser's document order, where focus may land on `aria-hidden` page content before reaching the popup's guards. Behavior depends on the browser; untested. Filed 2026-10-29 as [issue 150](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/150) (M4 track 9): browser-dependent and untested; the port moves focus to `<body>` as Base UI does (checked by hand on `/server/alert-dialog` in Chromium).
3. **Active-trigger toggle close is mostly unreachable by real pointers.** With modality forced on, the internal backdrop covers Triggers outside the popup, and focus is trapped. `trigger-press` closes (DLG-PTR-02) happen in practice only for Triggers stacked above the backdrop, Triggers inside the popup, or programmatic clicks. The shared test performs one via `user.click(trigger)`, which does not hit-test overlapping elements. Noted 2026-10-29 (M4 track 9): the port behaves the same; the shared suite's fixture (`alert-dialog-shared`) stacks its trigger above the internal backdrop so a real click reaches it, where Base UI's `user.click` does not hit-test.
4. **Escape remains enabled.** The docs subtitle says an alert dialog "requires a user response to proceed", but <kbd>Escape</kbd> closes it (ADL-KEY-01), and there is no prop to disable that apart from canceling in `onOpenChange`. This matches the WAI-ARIA alertdialog pattern; noted because the assignment asked which dismissals are blocked. Only outside press and focus-out are blocked. Noted 2026-10-29 (M4 track 9): Escape closes an alert dialog, as the WAI-ARIA alertdialog pattern has it (ADL-KEY-01); a consumer that must block it cancels the change in `OnOpenChange`.
5. **Hints not in source.** There is no `dismissible` prop: Dialog replaced it with `disablePointerDismissal` in v1.0.0-beta.5, and AlertDialog has neither publicly. There is no `modal` prop on AlertDialog. Neither component renders `aria-modal`. Noted 2026-10-29 (M4 track 9): the port's `AlertDialogRoot` has neither parameter (ADL-API-02/03's notes) and renders no `aria-modal`.
6. **Outline discrepancies** (`components/page.mdx` §Alert Dialog). (a) As for Dialog (DLG §16 Q8), `disabled` is not listed for Trigger and Close, and `id` (native attribute with special handling) is not listed for Popup/Title/Description. Internal markers (`data-base-ui-click-trigger`, `data-base-ui-focusable`, `data-base-ui-portal`, `data-base-ui-inert`) are absent. (b) The Types list omits `AlertDialog.Handle` (it appears under Exports). (c) `types.md` documents `AlertDialog.Root.ChangeEventReason`/`ChangeEventDetails` with `outside-press`, `focus-out` and `none` variants that AlertDialog never emits (ADL-EVT-02). (d) Root prop descriptions are inherited Dialog JSDoc ("Whether the dialog is…"). The Trigger `id` description says "the DialogRoot `triggerId` prop". Everything else in the outline appears in this document. Noted 2026-10-29 (M4 track 9): the port documents the Trigger's and the Close's `Disabled` and the parts' `id` handling in their XML documentation; the reasons are `EventReason`'s, of which an alert dialog emits ADL-EVT-02's four.
7. **Docs page omissions.** The AlertDialog page has no "Usage guidelines", "State", "Nested dialogs", "Custom focus management" or scroll-container sections. Its "Close confirmation" example reuses the Dialog demo (`../dialog/demos/close-confirmation`), whose parent is a `Dialog` (the backdrop/Esc wording there refers to the parent Dialog, not the AlertDialog). Noted 2026-10-29 (M4 track 9): the sample application's Dialog page carries the close-confirmation block, whose parent is a Dialog, as the docs' example is.
8. **Diagnostics name Dialog** (ADL-EDGE-01). An `AlertDialog.Trigger` used outside a Root throws a message telling the user to use `<Dialog.Root>`. `popupHandle.test.ts` deliberately asserts `DialogHandle` for AlertDialog handles, so the wording is intended for handles. For the context errors it may not be. Resolved 2026-10-29 (M4 track 9; ADR 08 Amendments 2026-10-27, chosen by the user 2026-10-27): the context errors name the AlertDialog parts and the handle warnings keep `DialogHandle` (ADL-EDGE-01's note).
9. **One-directional handle typing** (ADL-EDGE-02). `AlertDialogHandle extends DialogHandle` with a type-only private brand. Under TypeScript's class compatibility rules an `AlertDialog.Handle` is probably still assignable to `Dialog.Root`'s `handle` prop (subtype of the base class). Only the reverse direction is asserted (`AlertDialogRoot.spec.tsx:32-42`). PSH §3 (`popup-store-and-handles.md:80`) states that a handle of one family "is not type-assignable to another's `handle` prop", which may hold only one way. This was not verified with the compiler. Resolved in part: `popup-store-and-handles.md:80` now states the one-directional rule; the compiler check remains open. Resolved 2026-10-29 (M4 track 9): by the compiler, one way (ADL-EDGE-02's note).

### Base-doc discrepancies — to be fixed in `dialog.md`

1. **DLG-FOC-15** states the `restoreFocus: 'popup'` recovery unconditionally. Per source it runs only when `closeOnFocusOut` is true, i.e. `disablePointerDismissal` is `false` and not in alert mode (`floating-ui-react/components/FloatingFocusManager.tsx:424-427`; `dialog/popup/DialogPopup.tsx:102`). Resolved (QA 2026-10-14): DLG-FOC-15 now states the gating.
2. **DLG-FOC-16** says the backdrop-press push happens "for `modal !== false` dialogs only". It additionally requires `closeOnFocusOut` (same lines; push at `:452-456`), so it does not happen with `disablePointerDismissal` or in alert mode. Resolved (QA 2026-10-14): DLG-FOC-16 now states the gating.
3. **DLG-API-06 / DLG-DIS-01** describe the scope of `disablePointerDismissal` as outside presses plus (for `modal: false`) focus-out closing. It also disables, for every `modal` value, the focus-recovery and stack behaviors of items 1–2. The statement "Escape, Close parts and imperative closes are unaffected" is correct. Resolved (QA 2026-10-14): DLG-API-06 and DLG-DIS-01 now include these effects.
4. **DLG-DIS-10** says that in alert mode "Escape/Close/imperative closes remain the only paths". The active-trigger toggle (`trigger-press`, DLG-PTR-02) also remains, and the shared suite exercises it for AlertDialog ("unmounts the popup when the `unmount` method is called"). So does a controlled `open` change. See ADL-DIS-02. Resolved (QA 2026-10-14): DLG-DIS-10 now lists all remaining paths.
5. **DLG-DIS-10's `Test:` line** cites the "nested alert dialog" rows and the cross-type count test as evidence of alert-mode forcing. Those tests exercise nesting. The forcing is asserted by `AlertDialogRoot.test.tsx` "enforces alert dialog state for handle-backed roots" and "does not close when the backdrop is clicked". Resolved (QA 2026-10-14): DLG-DIS-10's `Test:` now cites both.

Related discrepancies in architecture docs (routed for completeness):

- `event-details-and-reasons.md:515` ("`Dialog.Root.onOpenChange` (AlertDialog identical)") and the reason index at `:577` (`outside-press`) and `:590` (`focus-out`) list AlertDialog as an emitter of `outside-press` and `focus-out`. AlertDialog never emits either (ADL-EVT-02). Resolved: `event-details-and-reasons.md:517-521` now excludes AlertDialog from both, and the reason index (`:577`, `:590`) no longer lists it.
- `floating-and-positioning.md` FLT-FOC-09 (backdrop clause) and FLT-FOC-12 do not state that they are inactive when `closeOnFocusOut` is false (`FloatingFocusManager.tsx:424-427`). Resolved: FLT-FOC-09 (`floating-and-positioning.md:407`) and FLT-FOC-12 (`:416`) now state the gating, naming AlertDialog.
- `popup-store-and-handles.md:80`: handle cross-family assignability, see Q9. Resolved: PSH §3 now states the non-assignability is one-directional (a plain `Dialog` handle is rejected by the AlertDialog/Drawer props).
