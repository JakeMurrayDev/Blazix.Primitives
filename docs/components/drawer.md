# Drawer

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/drawer/` |
| Tests | `packages/react/src/drawer/**/*.test.ts(x)` (11 files) + `root/DrawerRoot.spec.tsx` (type-level only). Drawer runs no popup/dialog shared suite; inherited Dialog behavior is evidenced by the Dialog test files cited in [dialog](dialog.md). |
| Docs page | `docs/src/app/(docs)/react/components/drawer/page.mdx` |
| Prefix | `DRW` |
| Status | approved |
| Revised | 2026-10-20 |
| Builds on | [dialog](dialog.md) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md) (swipe tracker §4.7.5), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [button-behavior](../architecture/button-behavior.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Drawer is a Dialog that slides in from an edge of the screen and can be dragged. It is built on the Dialog Root in *drawer mode* (`useRenderDialogRoot('drawer', …)`), so open state, triggers, handles, modality, focus trap, Escape/outside-press dismissal, nesting and the mount/exit lifecycle are exactly those of [dialog](dialog.md). Trigger, Portal, Title, Description and Close **are** the Dialog parts re-exported under Drawer names.

What Drawer adds:

* **Swipe to dismiss.** `Drawer.Viewport` feeds pointer and touch input on the popup into the shared swipe tracker of [popup-store-and-handles](../architecture/popup-store-and-handles.md) §4.7.5 (PSH-PTR-09…28, PSH-ALG-07…12). The popup follows the finger through CSS variables (`--drawer-swipe-movement-x/y`), is damped (square root) when dragged against the dismiss direction, and on release either snaps back or dismisses with reason `swipe` once it travelled more than half its size (minimum 10 px) or was flicked at ≥ 0.5 px/ms. A release-velocity–derived scalar `--drawer-swipe-strength` lets CSS shorten the exit transition. Touch input gets its own arbitration against scroll containers, cross-axis gestures, text selection, range inputs and `data-base-ui-swipe-ignore`.
* **Snap points** for vertical (bottom/top) sheets: `snapPoints`, controlled/uncontrolled `snapPoint`, `onSnapPointChange`, velocity-based skipping (or `snapToSequentialPoints`).
* **Swipe to open** with `Drawer.SwipeArea`, an invisible edge strip that opens the drawer by a drag in the opposite direction.
* **Nested drawer stack** styling: `--nested-drawers`, `data-nested-drawer-open`, `data-nested-drawer-swiping`, `--drawer-height`, `--drawer-frontmost-height`, and a parent that follows a nested drawer's swipe progress.
* **Indent effect**: `Drawer.Provider` tracks whether any drawer is open and publishes swipe progress to `Drawer.Indent` / `Drawer.IndentBackground`.
* **Android back gesture** via the platform `CloseWatcher` (reason `close-watcher`).
* **Virtual keyboard handling**: `Drawer.VirtualKeyboardProvider` keeps focused fields of a bottom sheet visible above a software keyboard.

Positioning is entirely consumer CSS. Base component: [dialog](dialog.md). No component derives from Drawer.

## 2. Anatomy

```
<Drawer.Provider>                     optional, 0..1          renders no element
  <Drawer.IndentBackground>           optional, 0..n          default element: div   (works without Provider)
  <Drawer.Indent>                     optional, 0..n          default element: div   (works without Provider)
    <Drawer.Root>                     required, 1             renders no element  (= Dialog Root, drawer mode)
      <Drawer.Trigger>                optional, 0..n          default element: button (= Dialog.Trigger; may be detached with `handle`)
      <Drawer.SwipeArea>              optional, 0..n          default element: div
      <Drawer.VirtualKeyboardProvider> optional, 0..1         renders no element (any depth under Root)
      <Drawer.Portal>                 required for Popup, 1   default element: div   (= Dialog.Portal)
        [internal backdrop]           automatic               div, while mounted && modal === true (DLG-STR-10)
        <Drawer.Backdrop>             optional, 0..n          default element: div   (anywhere under Root)
        <Drawer.Viewport>             expected, 0..1          default element: div   (wraps Dialog.Viewport)
          [focus guard] <Drawer.Popup> [focus guard]  required, 1   default element: div
            <Drawer.Content>          optional, 0..n          default element: div
            <Drawer.Title>            optional, 0..1          default element: h2    (= Dialog.Title)
            <Drawer.Description>      optional, 0..1          default element: p     (= Dialog.Description)
            <Drawer.Close>            optional, 0..n          default element: button (= Dialog.Close)
```

Structural-only parts: Content, Title, Description, IndentBackground (state attributes only). Viewport is not strictly required: without it the Popup renders but logs DRW-EDGE-01 and has no swipe handling and no touch scroll locking.

### 2.1 Adoption from base

- **DRW-STR-01** — MUST. Drawer satisfies every requirement of [dialog](dialog.md) (DLG-STR-01…13, DLG-API-01…27, DLG-STA-01…15, DLG-DOM-01…28, DLG-ARIA-01…12, DLG-KEY-01…11, DLG-PTR-01…19, DLG-FOC-01…18, DLG-DIS-01…10, DLG-EVT-01…10, DLG-ANI-01…10, DLG-EDGE-01…18), in drawer mode, with the substitutions below, except the IDs listed under *Not adopted* and *Overridden*. Server: relaxed, see §9.
  Source: `drawer/root/DrawerRoot.tsx:233-245`; `dialog/root/useRenderDialogRoot.tsx:35-43,98-102`; `drawer/trigger/DrawerTrigger.tsx:13`; `drawer/portal/DrawerPortal.tsx:13`; `drawer/title/DrawerTitle.tsx:12`; `drawer/description/DrawerDescription.tsx:12`; `drawer/close/DrawerClose.tsx:12`; `drawer/handle.ts:11-22`. Test: [L2] Dialog test files per [dialog](dialog.md) §13; `DrawerRoot.test.tsx` "exposes the attributes rendered by borrowed trigger and close parts", "supports detached triggers with handles", "supports imperative actions with handles", "attaches fresh root state when a handle-backed root remounts", "does not re-render inactive detached triggers when the popup opens and closes", "synchronizes trigger aria-controls with the popup id". (adopted DLG rows run at the layers tagged in dialog.md)

Substitutions:

| In dialog.md | Under Drawer |
|---|---|
| `Dialog.Root/Trigger/Portal/Backdrop/Viewport/Popup/Title/Description/Close` | `Drawer.Root/Trigger/Portal/Backdrop/Viewport/Popup/Title/Description/Close` |
| `Dialog.createHandle` / `Dialog.Handle` | `Drawer.createHandle` / `Drawer.Handle` (PSH §3.1: nominal subtype, identical runtime) |
| mode `'dialog'` | mode `'drawer'`: `modal`/`disablePointerDismissal` not forced, role `dialog`, the Root reports itself in `nestedOpenDrawerCount` (DLG-STA-05) |
| quoted messages (DLG-EDGE-01/02/03/04/05/06) | **unchanged, verbatim** — the Drawer parts are the Dialog parts, so e.g. a Drawer.Trigger without Root throws `Base UI: <Dialog.Trigger> must be used within <Dialog.Root> or provided with a handle.` and handle warnings name `DialogHandle` |
| `Dialog.Root.ChangeEventReason` | `Drawer.Root.ChangeEventReason` (DRW-EVT-01 adds `close-watcher`, `swipe`) |

*Not adopted* (no replacement):

| Base ID | Reason |
|---|---|
| DLG-STA-13 (Popup clause) | `nestedDialogOpen` is not a field of `Drawer.Popup.State` (it remains in `Drawer.Viewport.State`, see DRW-STA-01). |
| DLG-DOM-19 | `Drawer.Popup` has no `--nested-dialogs` variable; it has `--nested-drawers` (DRW-DOM-08). |
| DLG-DOM-22 | `Drawer.Popup` never renders `data-nested-dialog-open`. |
| DLG-DIS-10 (alert-mode clause) | Drawer never runs in alert mode. DLG-DIS-10's drawer-mode sentence (the mode flag only adds the `nestedOpenDrawerCount` report; Drawer's own parts add dismissals) is adopted and elaborated by DRW-DIS-01…05. |

*Overridden*:

| Base ID | Replaced by |
|---|---|
| DLG-STR-13 (namespace) | DRW-STR-02 |
| DLG-STA-11 (state objects) | DRW-STA-01 |
| DLG-API-20 (`initialFocus` default) | DRW-API-07, DRW-FOC-01 |
| DLG-FOC-01 (default initial focus target) | DRW-FOC-01, DRW-FOC-02 |
| DLG-DOM-15 (Viewport `data-nested-dialog-open`) | DRW-DOM-22 |
| DLG-EVT-02 (reason list) | DRW-EVT-01 |

- **DRW-STR-02** — MUST. The `Drawer` namespace exports `Backdrop`, `Close`, `Content`, `Description`, `Indent`, `IndentBackground`, `Popup`, `Portal`, `Provider`, `Root`, `SwipeArea`, `Title`, `Trigger`, `Viewport`, `VirtualKeyboardProvider`, `createHandle` and `Handle`; alongside it the attribute-name namespaces `DrawerBackdropCssVariables`, `DrawerBackdropDataAttributes`, `DrawerPopupCssVariables`, `DrawerPopupDataAttributes`, `DrawerSwipeAreaDataAttributes`, `DrawerViewportCssVariables`, `DrawerViewportDataAttributes`, `DrawerTriggerDataAttributes` (= Dialog's) and `DrawerCloseDataAttributes` (= Dialog's).
  Source: `drawer/index.parts.ts:1-16`; `drawer/index.ts:1-27`. Test: [L2] `DrawerRoot.test.tsx` "exposes the attributes rendered by borrowed trigger and close parts" (uses `DrawerTriggerDataAttributes`, `DrawerCloseDataAttributes`).
- **DRW-STR-03** — MUST. `Drawer.Trigger`, `Drawer.Portal`, `Drawer.Title`, `Drawer.Description` and `Drawer.Close` are the Dialog implementations (identical DOM, behavior and messages); only their type names differ. `Drawer.Backdrop`, `Drawer.Viewport` and `Drawer.Popup` are Drawer-specific implementations that read the same Dialog store.
  Source: `drawer/trigger/DrawerTrigger.tsx:13`; `drawer/portal/DrawerPortal.tsx:13`; `drawer/title/DrawerTitle.tsx:12`; `drawer/description/DrawerDescription.tsx:12`; `drawer/close/DrawerClose.tsx:12`; `drawer/backdrop/DrawerBackdrop.tsx:17-55`; `drawer/viewport/DrawerViewport.tsx:83-1062`; `drawer/popup/DrawerPopup.tsx:116-410`. Test: [L2] `DrawerRoot.test.tsx` "exposes the attributes rendered by borrowed trigger and close parts".
- **DRW-STR-04** — MUST. `Drawer.Viewport` renders the Dialog Viewport element (default `<div>`, inside the Portal; DLG-STR-06, DLG-DOM-11…14) with the Drawer gesture handlers merged onto it, and provides the swipe state its Popup reads.
  Source: `drawer/viewport/DrawerViewport.tsx:913-1061`. Test: [L2] `DrawerViewport.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **DRW-STR-05** — MUST. `Drawer.Popup` renders a `<div>` by default; like Dialog.Popup it requires a Portal ancestor (DLG-EDGE-03) and a Root ancestor (DLG-EDGE-02), and additionally reads the Drawer Root's shared state (DRW-EDGE-02).
  Source: `drawer/popup/DrawerPopup.tsx:122-155,358-394`. Test: [L2] `DrawerPopup.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **DRW-STR-06** — MUST. `Drawer.Content` renders a `<div>` by default anywhere under a Root (it requires the Dialog Root's shared state, DLG-EDGE-02); it has no behavior of its own beyond marking its subtree as text-selectable for mouse/pen (DRW-PTR-04).
  Source: `drawer/content/DrawerContent.tsx:14-26`. Test: [L2] `DrawerContent.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **DRW-STR-07** — MUST. `Drawer.SwipeArea` renders a `<div>` by default anywhere under a Root (requires both the Dialog and the Drawer Root shared state); any number may be rendered.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:80-95,444-490`. Test: [L2] `DrawerSwipeArea.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **DRW-STR-08** — MUST. `Drawer.Provider` renders no element; `Drawer.Indent` and `Drawer.IndentBackground` render a `<div>` by default and work without a Provider (then permanently inactive).
  Source: `drawer/provider/DrawerProvider.tsx:14-56`; `drawer/indent/DrawerIndent.tsx:27-88`; `drawer/indent-background/DrawerIndentBackground.tsx:23-42`. Test: [L2] `DrawerProvider.test.tsx` "allows indent parts to render without a provider".
- **DRW-STR-09** — MUST. `Drawer.VirtualKeyboardProvider` renders no element; it must be under a Root (DLG-EDGE-02) and acts only when a `Drawer.Viewport` element is registered (the Viewport is its measurement root).
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:82-97,721-725`. Test: [L2] `DrawerVirtualKeyboardProvider.test.tsx` (see §13).
- **DRW-STR-10** — MUST. *Drawer nesting* is determined by the nearest ancestor `Drawer.Root` in the component tree, independently of Dialog nesting (DLG-STR-12): a Drawer.Root inside another Drawer's subtree (also through an intermediate Dialog) reports its height, presence, swiping and swipe progress to that ancestor Drawer (DRW-STA-09…13). A Dialog or AlertDialog inside a drawer is a nested *dialog* but not a nested *drawer*.
  Source: `drawer/root/DrawerRoot.tsx:52-57`; `dialog/root/useDialogRoot.ts:109-123`. Test: [L2] `DrawerPopup.test.tsx` "does not treat dialogs inside nested drawers as nested drawers", "does not treat alert dialogs inside nested drawers as nested drawers".

## 3. Public API

### 3.1 `Drawer.Provider`

**Default element:** none. **Accepts `render`:** no. **`className`/`style` functions:** no.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | — | Content. | DRW-API-14 |

- **DRW-API-14** — MUST. `Drawer.Provider` accepts only `children` and provides, to descendants, the open-drawer registry (DRW-STA-14) and the visual-state store (DRW-STA-15).
  Source: `drawer/provider/DrawerProvider.tsx:14-56`. Test: [L2] `DrawerProvider.test.tsx` "stays active until every open drawer is closed or removed".

State object: `{}` (`Drawer.Provider.State`).

### 3.2 `Drawer.IndentBackground`

**Default element:** `<div>`. **Accepts `render`:** yes. **`className`/`style` functions:** yes. Props: none beyond `className`, `style`, `render` and forwarded `<div>` attributes.

| Field | Type | Meaning |
|---|---|---|
| `active` | `boolean` | Any drawer under the nearest Provider is open (`false` without Provider). |

| Attribute | Present when | Req | Writer |
|---|---|---|---|
| `data-active` | `active` | DRW-DOM-31 | C# |
| `data-inactive` | `!active` | DRW-DOM-31 | C# |

### 3.3 `Drawer.Indent`

**Default element:** `<div>`. **Accepts `render`:** yes. **`className`/`style` functions:** yes. Props: none beyond the common props. State: as §3.2 (`{ active }`).

| Attribute / variable | Present when / value | Req | Writer |
|---|---|---|---|
| `data-active` / `data-inactive` | as §3.2 | DRW-DOM-31 | C# |
| `--drawer-swipe-progress` | inline; `0` initially, then the Provider's visual swipe progress | DRW-DOM-32 | C# → JS |
| `--drawer-height` | inline; the Provider's visual frontmost height in px while > 0, else removed | DRW-DOM-32 | JS |

### 3.4 `Drawer.Root`

**Default element:** none. Inherits every prop of `Dialog.Root` (DLG-API-01…11, same defaults: `modal` `true`, `disablePointerDismissal` `false`, `defaultOpen` `false`, `defaultTriggerId` `null`) and adds:

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `swipeDirection` | `'up' \| 'down' \| 'left' \| 'right'` | `'down'` | Direction that dismisses the drawer. | DRW-API-01 |
| `snapPoints` | `(number \| string)[] \| undefined` | `undefined` | Snap heights for vertical drawers. | DRW-API-02 |
| `snapPoint` | `number \| string \| null \| undefined` | `undefined` (uncontrolled) | Consumer-owned active snap point. | DRW-API-03 |
| `defaultSnapPoint` | `number \| string \| null \| undefined` | `undefined` → `snapPoints[0] ?? null` | Initial active snap point when uncontrolled; also the value restored on close. | DRW-API-04 |
| `onSnapPointChange` | `(snapPoint, eventDetails: Drawer.Root.SnapPointChangeEventDetails) => void` | `undefined` | Snap point change request (cancelable). | DRW-API-05 |
| `snapToSequentialPoints` | `boolean` | `false` | Disables velocity-based snap skipping. | DRW-API-06 |
| `onOpenChange` | `(open, eventDetails: Drawer.Root.ChangeEventDetails) => void` | `undefined` | As DLG-API-04 with the reasons of DRW-EVT-01. | DLG-API-04 |

- **DRW-API-01** — MUST. `swipeDirection` defaults to `'down'` and accepts `'up'`, `'down'`, `'left'`, `'right'`. It selects the dismiss direction of the Viewport gesture (DRW-PTR-02), the default opening direction of SwipeArea (its opposite, DRW-API-09), the `data-swipe-direction` value of the Popup (DRW-DOM-05), and the gesture axis (`left`/`right` → horizontal, else vertical).
  Source: `drawer/root/DrawerRoot.tsx:44`; `drawer/viewport/DrawerViewport.tsx:120-123,167-177`; `drawer/popup/DrawerPopup.tsx:317`. Test: [L2] `DrawerPopup.test.tsx` "applies a negative snap point offset to upward drawers" (`data-swipe-direction="up"`); `DrawerRoot.test.tsx` "uses a size-based swipe threshold" (`right`). (L4 for the gesture-direction and axis clauses) Docs: components/drawer/page.mdx §Position.
- **DRW-API-02** — MUST. `snapPoints` is a list of snap heights: a number in `[0, 1]` (at most 1) is a fraction of the viewport height, a number greater than 1 is pixels, a string ending in `px` or `rem` is pixels or root-font-relative; any other value is ignored (DRW-STA-05). Snap points take effect only when the list is non-empty **and** `swipeDirection` is `'down'` or `'up'`; for `'left'`/`'right'` they are ignored for positioning and release decisions.
  Source: `drawer/root/DrawerRoot.tsx:46,320-326`; `drawer/root/useDrawerSnapPoints.ts:30-64`; `drawer/popup/DrawerPopup.tsx:327-328`; `drawer/viewport/DrawerViewport.tsx:502-505`. Test: [L1] `DrawerSnapPoints.test.tsx` (4 tests); `DrawerViewport.test.tsx` "uses regular horizontal dismissal when snap points are configured". (L4 for the horizontal-direction release clause and the `DrawerViewport.test.tsx` citation) Docs: components/drawer/page.mdx §Snap points.
- **DRW-API-03** — MUST. `snapPoint`, when not `undefined`, is the consumer-owned active snap point (a controlled `null` means "no snap point"); the component then only requests changes through `onSnapPointChange`. Switching between controlled and uncontrolled logs the shared controlled-state warning (UTL `useControlled`, `name: 'Drawer'`, `state: 'snapPoint'`).
  Source: `drawer/root/DrawerRoot.tsx:67-74,96-99`. Test: [L2] `DrawerRoot.test.tsx` "opens from a controlled null snap point", "dismisses from a controlled null snap point".
- **DRW-API-04** — MUST. The default snap point is `defaultSnapPoint` when it is not `undefined` (including `null`), otherwise `snapPoints[0]`, otherwise `null`. It seeds the uncontrolled value, replaces an invalid uncontrolled value (DRW-STA-02), and is the value requested when the drawer closes (DRW-STA-04).
  Source: `drawer/root/DrawerRoot.tsx:65-66,105-110,158-167`. Test: [L2] `DrawerRoot.test.tsx` "resets to the default snap point when provided", "resets the active snap point when closing".
- **DRW-API-05** — MUST. `onSnapPointChange(snapPoint, eventDetails)` is called before every snap point change is applied; `eventDetails` is a plain change-event details object (EVD-API-01, no `preventUnmountOnClose`), cancelable.
  Source: `drawer/root/DrawerRoot.tsx:79-94,375-376`. Test: [L2] `DrawerRoot.test.tsx` "provides event details when snap point changes", "honors canceled snap point changes and falls back from invalid uncontrolled values".
- **DRW-API-06** — MUST. `snapToSequentialPoints` defaults to `false`; when `true` the release target ignores the velocity projection and at most advances one snap point per flick (DRW-ALG-01 branch S).
  Source: `drawer/root/DrawerRoot.tsx:45`; `drawer/viewport/DrawerViewport.tsx:538-618`. Test: [L4] `DrawerRoot.test.tsx` "allows dragging past a snap point when snapToSequentialPoints is enabled" and the sequential tests listed in §13. Docs: components/drawer/page.mdx §Snap points.

#### State object

`Drawer.Root.State` is `{}`; no element, no attributes.

### 3.5 `Drawer.Trigger`

Identical to `Dialog.Trigger` (§3.2 of dialog.md: DLG-API-12…16, DLG-DOM-01…04, DLG-ARIA-01…03): props `handle` (a `Drawer.Handle`), `payload`, `id`, `nativeButton` (default `true`), `disabled` (default `false`, not in the outline); state `{ disabled, open }`; attributes `data-popup-open`, `data-disabled`, `data-base-ui-click-trigger`; `aria-haspopup="dialog"`.

### 3.6 `Drawer.SwipeArea`

**Default element:** `<div>`. **Accepts `render`:** yes. **`className`/`style` functions:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `disabled` | `boolean` | `false` | Disables swipe-to-open. | DRW-API-08 |
| `swipeDirection` | `'up' \| 'down' \| 'left' \| 'right' \| undefined` | opposite of Root `swipeDirection` | Direction that opens the drawer. | DRW-API-09 |
| `id` | `string` | generated | Rendered id and trigger-registry key (not in the outline). | DRW-API-10 |
| `className`, `style`, `render` | per RND | — | `style` merges after the component's `pointer-events`/`touch-action`. | — |
| other `<div>` attributes / handlers | | — | Forwarded; consumer handlers chain after the component's. | — |

**Consumed:** `disabled`, `swipeDirection`.

- **DRW-API-08** — MUST. `disabled` defaults to `false`; when `true` the area never starts a gesture, cancels a gesture in progress (DRW-PTR-31), renders `data-disabled` and `pointer-events: none`.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:88,130,381,411-428,453`. Test: [L2] `DrawerSwipeArea.test.tsx` "does not open the drawer when disabled", "cancels an active opening gesture when the swipe area becomes disabled". (L4 for the gesture clauses)
- **DRW-API-09** — MUST. The opening direction is the `swipeDirection` prop when given, otherwise the opposite of the Root's `swipeDirection` (`down`↔`up`, `left`↔`right`); the *dismiss direction* used for measurement is the opposite of the opening direction.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:63-68,128-129`. Test: [L2] `DrawerSwipeArea.test.tsx` "respects custom swipeDirection", "opens the drawer when swiped in the open direction" (default `down` → opens upward). (L4 for the dismiss-direction measurement clause)
- **DRW-API-10** — MUST. The SwipeArea renders `id` = the `id` prop or a generated id (omitted while no id is available) and registers its element in the Root's trigger registry under that id (PSH-STA-26), re-registering when the id changes and unregistering on unmount; it is not an `aria-*` trigger.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:110-119,446,487`; `utils/popups/popupStoreUtils.ts:139-170`. Test: [L2] `DrawerSwipeArea.test.tsx` "works before a generated swipe area id is available", "registers the swipe area once a generated id becomes available", "follows a swipe area id change".

State object (`Drawer.SwipeArea.State`):

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state of the drawer. |
| `swiping` | `boolean` | The area's own gesture is active. |
| `swipeDirection` | `'up' \| 'down' \| 'left' \| 'right'` | Resolved opening direction. |
| `disabled` | `boolean` | The `disabled` prop. |

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` | open / closed | DRW-DOM-25 | C# |
| `data-swiping` | `swiping` | DRW-DOM-26 | JS |
| `data-swipe-direction` | always, resolved opening direction | DRW-DOM-27 | C# |
| `data-disabled` | `disabled` | DRW-DOM-28 | C# |

### 3.7 `Drawer.VirtualKeyboardProvider`

**Default element:** none. Props: `children` only. State `{}`. Behavior: §6.3.1. CSS variable it writes: `--drawer-keyboard-inset` on the Viewport (DRW-DOM-23).

- **DRW-API-11** — MUST. `Drawer.VirtualKeyboardProvider` accepts only `children`; Drawers without it have none of the §6.3.1 behavior.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:82-83,730-732`; `drawer/viewport/DrawerViewport.tsx:138,790,999,1043,1048`. Test: [L2] `DrawerVirtualKeyboardProvider.test.tsx` (see §13). Docs: components/drawer/page.mdx §Virtual keyboard aware.

### 3.8 `Drawer.Portal`

Identical to `Dialog.Portal` (DLG-API-17/18, DLG-DOM-05/06): props `keepMounted` (default `false`), `container`.

### 3.9 `Drawer.Backdrop`

**Default element:** `<div>`. **Accepts `render`:** yes. **`className`/`style` functions:** yes. Props: `forceRender` (default `false`, DLG-API-19 semantics), common props. State `{ open, transitionStatus }` (DLG §3.4). Rendering rule: DLG-STR-05.

| Attribute / variable | Present when / value | Req | Writer |
|---|---|---|---|
| `role="presentation"`, `hidden`, `user-select: none` | as DLG-ARIA-08, DLG-DOM-07/08 | — | C# |
| inline `pointer-events: none` | effective open state is `false` | DRW-DOM-17 | C# → JS |
| `data-open` / `data-closed` / `data-starting-style` / `data-ending-style` | as DLG-DOM-09/10 | — | C# (`data-open`, `data-closed`); JS (`data-starting-style`, `data-ending-style`) |
| `data-swiping` | imperatively, while the Viewport or SwipeArea gesture is active | DRW-DOM-20 | JS |
| `data-swipe-dismiss` | imperatively, from swipe release until reset | DRW-DOM-07 | JS |
| `--drawer-swipe-progress` | inline `0`, then imperatively the dismiss progress | DRW-DOM-18 | C# → JS |
| `--drawer-swipe-strength` | inline, always `1` | DRW-DOM-19 | C# → JS |
| `--drawer-height` | imperatively, frontmost height while progress is shown | DRW-DOM-21 | JS |

### 3.10 `Drawer.Viewport`

**Default element:** `<div>` (the Dialog Viewport). **Accepts `render`:** yes. **`className`/`style` functions:** yes. No props of its own (`style` is forwarded since v1.5.0).

State (`Drawer.Viewport.State`) = Dialog Viewport state `{ open, transitionStatus, nested, nestedDialogOpen }`.

| Attribute / variable | Present when / value | Req | Writer |
|---|---|---|---|
| `role="presentation"`, `hidden`, `pointer-events: none` when closed | DLG-ARIA-09, DLG-DOM-12/13 | — | C# |
| `data-open` / `data-closed` / `data-starting-style` / `data-ending-style` / `data-nested` | DLG-DOM-14/15 | — | C# (`data-open`, `data-closed`, `data-nested`); JS (`data-starting-style`, `data-ending-style`) |
| `data-nested-dialog-open` | never | DRW-DOM-22 | C# |
| `--drawer-keyboard-inset` | while a VirtualKeyboardProvider is aligned | DRW-DOM-23 | C# |

### 3.11 `Drawer.Popup`

**Default element:** `<div>`. **Accepts `render`:** yes. **`className`/`style` functions:** yes.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `initialFocus` | as DLG-API-20 | `undefined` → the popup element | Element focused on open. | DRW-API-07 |
| `finalFocus` | as DLG-API-21 | `undefined` → `true` | Element focused on close. | DLG-API-21 |
| `id` | `string` | generated popup id | As DLG-DOM-16 (not in the outline). | — |
| common props | | | `style` merges after the component's inline variables and drag styles. | — |

- **DRW-API-07** — MUST. When `initialFocus` is `undefined` the Popup passes a DOM handle to its own element as the initial-focus target, so the popup element itself receives focus on open for every interaction type (DRW-FOC-01); every other value follows PSH-FOC-02/03 unchanged.
  Source: `drawer/popup/DrawerPopup.tsx:306,402`. Test: [L4] `DrawerPopup.test.tsx` "defaults initial focus to the popup element", "leaves focus on the trigger when initial focus is disabled".

State object (`Drawer.Popup.State`):

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `transitionStatus` | `TransitionStatus` | Root transition status. |
| `expanded` | `boolean` | Active snap point is exactly the number `1` (DRW-STA-16). |
| `nested` | `boolean` | Root is nested (DLG-STA-03). |
| `nestedDrawerOpen` | `boolean` | `nestedOpenDrawerCount > 0`. |
| `nestedDrawerSwiping` | `boolean` | A nested drawer is being swiped (DRW-STA-12). |
| `swipeDirection` | swipe direction | Root `swipeDirection`. |
| `swiping` | `boolean` | The Viewport gesture is active (`false` without Viewport). |

| Attribute | Present when / value | Req | Writer |
|---|---|---|---|
| `data-open` / `data-closed` / `data-starting-style` / `data-ending-style` | DLG-DOM-20 (plus DRW-ANI-02) | — | C# (`data-open`, `data-closed`); JS (`data-starting-style`, `data-ending-style`) |
| `data-nested` | `nested` (not in the outline) | DRW-DOM-01 | C# |
| `data-expanded` | `expanded` | DRW-DOM-02 | C# |
| `data-nested-drawer-open` | `nestedDrawerOpen` | DRW-DOM-03 | C# |
| `data-nested-drawer-swiping` | `nestedDrawerSwiping` | DRW-DOM-04 | C# |
| `data-swipe-direction` | always, `swipeDirection` | DRW-DOM-05 | C# |
| `data-swiping` | `swiping` (state), or imperatively by SwipeArea | DRW-DOM-06 | JS |
| `data-swipe-dismiss` | imperatively from swipe release | DRW-DOM-07 | JS |
| `role`, `tabindex="-1"`, `data-base-ui-focusable`, `hidden`, `aria-labelledby`, `aria-describedby`, `id` | DLG-DOM-16…18, DLG-ARIA-04…06 | — | C# |

| Variable | Value | Req | Writer |
|---|---|---|---|
| `--nested-drawers` | `nestedOpenDrawerCount` | DRW-DOM-08 | C# → JS |
| `--drawer-height` | measured height in px while a nested drawer is present or the popup is exiting | DRW-DOM-09 | JS |
| `--drawer-frontmost-height` | frontmost height in px when > 0 | DRW-DOM-10 | C# → JS |
| `--drawer-snap-point-offset` | active snap offset in px (negated for `up`), else `0px` | DRW-DOM-11 | C# → JS |
| `--drawer-swipe-movement-x` / `-y` | drag movement in px | DRW-DOM-12 | JS |
| `--drawer-swipe-strength` | release scalar in `(0, 1]`, else `1` | DRW-DOM-13 | JS |
| `--drawer-swipe-progress` | nested drawer swipe progress (not in the outline) | DRW-DOM-14 | JS |

### 3.12 `Drawer.Content`

**Default element:** `<div>`. **Accepts `render`:** yes. **`className`/`style` functions:** yes (state `{}`). Props: common props only. Attribute: `data-drawer-content=""` (internal marker, not in the outline; DRW-DOM-24).

- **DRW-API-13** — MUST. `Drawer.Content`, `Drawer.Indent`, `Drawer.IndentBackground` and `Drawer.Viewport` have no component-specific props: every attribute and handler other than `className`, `style` and `render` is forwarded to the element (consumer handlers on the Viewport chain with the gesture handlers per RND-EVT-01/02).
  Source: `drawer/content/DrawerContent.tsx:18,24`; `drawer/indent/DrawerIndent.tsx:31,84`; `drawer/indent-background/DrawerIndentBackground.tsx:27,39`; `drawer/viewport/DrawerViewport.tsx:87,919`. Test: [L2] describeConformance of Content and Viewport.

### 3.13 `Drawer.Title` / 3.14 `Drawer.Description` / 3.15 `Drawer.Close`

Identical to `Dialog.Title` (`<h2>`), `Dialog.Description` (`<p>`), `Dialog.Close` (`<button>`, `nativeButton`, `disabled`, `data-disabled`); DLG §3.7–3.9.

### 3.16 `Drawer.createHandle()` / `Drawer.Handle`

- **DRW-API-12** — MUST. `Drawer.createHandle<Payload>()` returns a new `Drawer.Handle<Payload>`, a nominal subtype of `Dialog.Handle` with identical members and behavior (`open(triggerId | null)`, `openWithPayload(payload)`, `close()`, `isOpen`; DLG-API-24…27, PSH §3.1); a Drawer handle is not type-assignable to a Dialog/AlertDialog `handle` prop and vice versa.
  Source: `drawer/handle.ts:11-22`. Test: [L2] `DrawerRoot.test.tsx` "supports detached triggers with handles", "supports imperative actions with handles", "attaches fresh root state when a handle-backed root remounts" (three `no root using this handle is mounted` warnings).

### 3.17 Base-component parameters overridden by this component

| Base part / prop | Base default or meaning | Under Drawer |
|---|---|---|
| `Popup.initialFocus` | `undefined` → touch: popup, else first tabbable | `undefined` → popup element always (DRW-API-07) |
| `Popup` state | `{ open, transitionStatus, nested, nestedDialogOpen }` | DRW-STA-01 fields |
| `Popup` `--nested-dialogs`, `data-nested-dialog-open` | rendered | not rendered; `--nested-drawers`, `data-nested-drawer-open` instead |
| `Viewport` `data-nested-dialog-open` | rendered when a nested dialog is open | never rendered (DRW-DOM-22) |
| `Backdrop` inline style | `user-select: none` | plus `pointer-events: none` while closed, `--drawer-swipe-progress: 0`, `--drawer-swipe-strength: 1` |
| Root `onOpenChange` reasons | DLG-EVT-02 | plus `close-watcher`, `swipe` (DRW-EVT-01) |

### 3.18 Shared types

- `Drawer.Root.ChangeEventReason` = `'trigger-press' | 'outside-press' | 'escape-key' | 'close-watcher' | 'close-press' | 'focus-out' | 'imperative-action' | 'swipe' | 'none'` (`drawer/root/DrawerRoot.tsx:356-365`; `internals/reason-parts.ts:1,25,41`).
- `Drawer.Root.ChangeEventDetails` = change-event details for those reasons plus `preventUnmountOnClose(): void` (`:367-371`). For reason `swipe` the `event` is a `PointerEvent | TouchEvent` (type-level: `DrawerRoot.spec.tsx`).
- `Drawer.Root.SnapPointChangeEventReason` = `Drawer.Root.ChangeEventReason`; `Drawer.Root.SnapPointChangeEventDetails` = plain change-event details (no `preventUnmountOnClose`) (`:373-376`).
- `Drawer.Root.SnapPoint` = `number | string` (`drawer/root/DrawerRootContext.ts:7`). `Drawer.Root.Actions` = `{ unmount(): void; close(): void }`.
- `Props`/`State` for every part (outline §Drawer Types); flat aliases are exported from `drawer/index.ts:3-17`.
- `DrawerRoot.spec.tsx` (type-level only): narrowing `details.reason === 'swipe'` types `details.event` as `PointerEvent | TouchEvent` (not `KeyboardEvent`); `'escape-key'` types it as `KeyboardEvent`.

- **DRW-STA-01** — MUST. The state objects exposed to `className`/`style`/`render` are exactly: Popup `{ open, transitionStatus, expanded, nested, nestedDrawerOpen, nestedDrawerSwiping, swipeDirection, swiping }`; Viewport `{ open, transitionStatus, nested, nestedDialogOpen }`; Backdrop `{ open, transitionStatus }`; SwipeArea `{ open, swiping, swipeDirection, disabled }`; Indent and IndentBackground `{ active }`; Content, Root, Provider, VirtualKeyboardProvider `{}`; Trigger, Portal, Title, Description, Close as DLG-STA-11. Server: relaxed, see §9.
  Source: `drawer/popup/DrawerPopup.tsx:310-319`; `dialog/viewport/DialogViewport.tsx:35-40`; `drawer/backdrop/DrawerBackdrop.tsx:30-33`; `drawer/swipe-area/DrawerSwipeArea.tsx:437-442`; `drawer/indent/DrawerIndent.tsx:71-73`; `drawer/indent-background/DrawerIndentBackground.tsx:32-34`; `drawer/content/DrawerContent.tsx:30`. Test: [L2] describeConformance of Popup, Viewport, Content, SwipeArea (RND-DOM-37/39).

## 4. State model

Open state, trigger association, payload, `mounted`, `transitionStatus`, modality, nesting counts and the open-change sequence are Dialog's (DLG §4, adopted by DRW-STR-01). The open-change callback the Dialog store invokes is Drawer's wrapper (DRW-STA-04), which calls the consumer's `onOpenChange` first. Drawer adds the snap-point state, the nested-drawer stack measurements, the Provider registry and the gesture state of §6.2.

### 4.1 Snap points

- **DRW-STA-02** — MUST. The *active snap point* exposed to the parts is: when `snapPoint` is controlled, its value as given (even if not in `snapPoints`); otherwise the internal value, except that when `snapPoints` is non-empty and the internal value is `null` or not an element of `snapPoints` (compared with `Object.is`), the default snap point (DRW-API-04) is used instead.
  Source: `drawer/root/DrawerRoot.tsx:96-113`. Test: [L2] `DrawerRoot.test.tsx` "honors canceled snap point changes and falls back from invalid uncontrolled values" (999 → stays `'300px'`).
- **DRW-STA-03** — MUST. Every snap point change request runs: (1) build details (reason `none`, no event, when the requester supplies none); (2) call `onSnapPointChange(next, details)`; (3) stop if `details.isCanceled`; (4) write the internal value. There is no equality guard: a request for the current value still calls `onSnapPointChange`.
  Source: `drawer/root/DrawerRoot.tsx:79-94`. Test: [L2] `DrawerRoot.test.tsx` "honors canceled snap point changes and falls back from invalid uncontrolled values", "resets to the default snap point when provided".
- **DRW-STA-04** — MUST. The Root's open-change wrapper calls the consumer's `onOpenChange(next, details)`; if the request was not canceled, `next` is `false` and `snapPoints` is non-empty, it then requests the default snap point (DRW-STA-03) with new details carrying the close's `reason`, `event` and `trigger` — synchronously, before the store commits the close and even when a controlled owner will not actually close. A canceled close leaves the snap point untouched.
  Source: `drawer/root/DrawerRoot.tsx:150-169`. Test: [L2] `DrawerRoot.test.tsx` "resets the active snap point when closing", "resets to the default snap point when provided", "provides event details when snap point changes" (`close-press`), "does not reset snap point when a close is canceled", "does not restore snap point when a controlled swipe close is accepted by the parent".
- **DRW-STA-05** — MUST. A snap point value resolves to a height in px (given viewport height `vh` and root font size `fs`): `null` when `vh` is not a finite positive number; a non-finite number → `null`; a number `≤ 1` → `clamp(n, 0, 1) × vh`; a number `> 1` → `n`; a string is trimmed, then ending in `px` → `parseFloat` (non-finite → `null`), ending in `rem` → `parseFloat × fs` (non-finite → `null`), anything else (`'50%'`, `'invalid'`) → `null`.
  Source: `drawer/root/useDrawerSnapPoints.ts:30-64`. Test: [L1] `DrawerSnapPoints.test.tsx` "resolves supported units, filters invalid values, clamps heights, and keeps the last duplicate", "uses fractional and pixel snap points and resolves null or invalid active values as closed"; `DrawerViewport.test.tsx` "falls back to regular dismissal when all snap points are invalid". (L4 for the `DrawerViewport.test.tsx` citation)
- **DRW-STA-06** — MUST. The *resolved snap point list* is empty when `snapPoints` is empty, `vh ≤ 0` or the measured popup height `ph ≤ 0`. Otherwise each value resolving to non-`null` becomes `{ value, height: clamp(h, 0, min(ph, vh)), offset: max(0, ph − height) }` in input order; when more than one remains, duplicates are removed by walking from the **last** entry to the first and dropping any whose height is within 1 px of an already kept height, keeping the original relative order of the survivors.
  Source: `drawer/root/useDrawerSnapPoints.ts:118-161`. Test: [L1] `DrawerSnapPoints.test.tsx` "resolves supported units, filters invalid values, clamps heights, and keeps the last duplicate" (`[NaN,'invalid','invalidpx','invalidrem',-1,'2rem','100px',200,200.5,800]` with `ph=300`, `vh=400`, `fs=20` → `-1:0/300, '2rem':40/260, '100px':100/200, 200.5:200.5/99.5, 800:300/0`), "produces no resolved snap point when the list is empty", "resolves a single snap point without a deduplication pass"; `DrawerViewport.test.tsx` "does not resolve snap points before the popup has a measurable height". (L4 for the `DrawerViewport.test.tsx` citation)
- **DRW-STA-07** — MUST. The *active snap offset* is `null` when the active snap point is `null`; else the offset of the resolved entry whose `value` is `Object.is`-equal to it; else, when the active value itself resolves (DRW-STA-05), the offset of the resolved entry whose height is closest to `clamp(resolved, 0, min(ph, vh))` (first wins on ties); else `null`.
  Source: `drawer/root/useDrawerSnapPoints.ts:163-195`. Test: [L1] `DrawerSnapPoints.test.tsx` "resolves supported units…" (active `200` → `99.5`), "uses fractional and pixel snap points and resolves null or invalid active values as closed" (`200` → 100; `null`/`'invalid'` → `null`).
- **DRW-STA-08** — MUST. `vh` is the Viewport element's `offsetHeight` when a Viewport is registered, otherwise `document.documentElement.clientHeight`; `fs` is the computed `font-size` of the document element when finite, initially 16. Both are measured post-commit, pre-paint whenever the Viewport element changes and again on every `ResizeObserver` notification of the Viewport (when available).
  Source: `drawer/root/useDrawerSnapPoints.ts:89-116`. Test: [L4] `DrawerSnapPoints.test.tsx` (`fontSize = '20px'` → `'2rem'` = 40).
- **DRW-STA-09** — MUST. `getSnapPointSwipeMovement(base, m)` = `m` when `base + m ≥ 0`, else `−√(−(base + m)) − base` (the overshoot past the fully open edge is square-root damped). `closestSnapPointIndex(values, t)` returns the index of the value with the smallest `|v − t|`, the first on ties, `−1` for an empty list.
  Source: `drawer/root/useDrawerSnapPoints.ts:21-28,69-82`. Test: [L3] `useDrawerSnapPoints.test.ts` "returns the raw movement when the drag does not overshoot the open edge", "returns the raw movement at the open edge (nextOffset === 0)", "damps the movement with a square root once the drag overshoots the open edge", "returns the closest value and keeps the first value on ties", "returns -1 when there are no values".
- **DRW-STA-16** — MUST. `expanded` is `true` iff the active snap point is the number `1` (strict equality; `'100%'` or a px value equal to the full height does not count).
  Source: `drawer/popup/DrawerPopup.tsx:314`. Test: none.
- **DRW-STA-17** — MUST. When at least two snap points are configured, the resolved list has at least two entries and the direction is vertical, the *snap range* is `{ minOffset, range }` from the two smallest resolved offsets (`range = second − first`), and the *resting snap progress* is `clamp((activeOffset − minOffset) / range, 0, 1)` (absent when the active offset is `null`).
  Source: `drawer/viewport/DrawerViewport.tsx:140-165`. Test: [L3] `DrawerViewport.test.tsx` "keeps the resting snap point progress when a press never starts a swipe" (backdrop `--drawer-swipe-progress` `1` at the lower snap point).

### 4.2 Nested drawer stack

- **DRW-STA-10** — MUST. While mounted, the Popup measures its own `offsetHeight` post-commit, pre-paint on mount, when the nested-drawer state changes, and on every `ResizeObserver` notification, and reports it to its Root as `popupHeight` with these rules: (a) a measurement is skipped while the last measured height `> 0`, the frontmost height exceeds it and the element is currently taller than it (stretched by a nested drawer); (b) while a nested drawer is present (DRW-STA-12) and a height was measured, the last height is re-reported unchanged; (c) otherwise a changed height is stored and reported. When the Popup is unmounted (`mounted` false) the height is reset to 0 and reported. A measurement queued after unmount is ignored.
  Source: `drawer/popup/DrawerPopup.tsx:161-242`; `drawer/root/DrawerRoot.tsx:115-121`. Test: [L4] `DrawerPopup.test.tsx` "keeps the last measured popup height while nested content stretches it", "ignores a queued measurement after the popup unmounts".
- **DRW-STA-11** — MUST. The Root's *frontmost height* is the own popup height when no nested drawer has reported a positive height; a positive height reported by the nested drawer replaces it (and marks a nested drawer as open); a report of 0 reverts it to the own popup height (when > 0). An open Popup reports its Root's frontmost height to the parent Drawer post-commit, pre-paint whenever it changes, and 0 when it closes or unmounts; heights include borders (`offsetHeight`).
  Source: `drawer/root/DrawerRoot.tsx:115-134`; `drawer/popup/DrawerPopup.tsx:271-281`. Test: [L2] `DrawerPopup.test.tsx` "includes border size in frontmost height CSS variable for nested drawers", "reports nested presence and frontmost height before passive effects". (L4 for the border-inclusive `offsetHeight` clause)
- **DRW-STA-12** — MUST. A nested Drawer reports *presence* (`open || transitionStatus === 'ending'`) to its parent post-commit, pre-paint whenever it changes and `false` on unmount; the parent's `hasNestedDrawer` is the last report. *Nested swiping*: the parent's `nestedSwiping` is the last value reported by a nested Drawer's Viewport (DRW-PTR-17) and is forwarded unchanged to the grandparent.
  Source: `drawer/popup/DrawerPopup.tsx:283-294`; `drawer/root/DrawerRoot.tsx:136-148`. Test: [L2] `DrawerPopup.test.tsx` "reports nested presence and frontmost height before passive effects"; `DrawerViewport.test.tsx` "does not mark nested drawers as swiping until movement passes the threshold". (L4 for the nested-swiping clause)
- **DRW-STA-13** — MUST. The Root keeps a *nested swipe progress* value (initially 0) outside the render cycle: a report replaces it with the reported number, or 0 when not finite; subscribers are notified only when the stored value changes; every report is forwarded to the grandparent unchanged.
  Source: `drawer/root/DrawerRoot.tsx:140-143,389-417`. Test: [L3] `DrawerRoot.test.tsx` "normalizes invalid nested swipe progress without notifying for duplicate values".
- **DRW-STA-18** — MUST. `nestedOpenDrawerCount` (DLG-STA-04/05) counts only Drawer descendants: a nested Dialog or AlertDialog contributes to `nestedOpenDialogCount` but never to `nestedOpenDrawerCount` of its own Drawer or any ancestor; a handle-backed nested drawer that mounts closed does not reset a parent's count.
  Source: `dialog/root/useDialogRoot.ts:91-123`. Test: [L2] `DrawerPopup.test.tsx` "does not treat dialogs inside nested drawers as nested drawers", "does not treat alert dialogs inside nested drawers as nested drawers", "keeps the parent nested state when a closed handle-backed nested drawer mounts", "clears parent nested drawer state as soon as a nested drawer closes".

### 4.3 Provider registry and visual state

- **DRW-STA-14** — MUST. Every Root under a `Drawer.Provider` registers itself post-commit, pre-paint with its effective open state whenever that changes, and unregisters (as closed) post-commit, pre-paint on unmount (before after-paint work). The Provider keeps the set of open Roots and `active = set non-empty`; redundant open/closed reports and removal of an unknown or already removed Root are no-ops; `active` changes only when emptiness changes; closed or removed Roots are not retained.
  Source: `drawer/root/DrawerRoot.tsx:218-231,419-443`; `drawer/provider/DrawerProvider.tsx:17-41`. Test: [L2] `DrawerProvider.test.tsx` "stays active until every open drawer is closed or removed", "ignores redundant registry updates without disturbing active state", "releases closed drawers once they are removed"; `DrawerRoot.test.tsx` "reports and unregisters $label before passive effects" (×3); `DrawerIndent.test.tsx`, `DrawerIndentBackground.test.tsx` "sets data-active when any drawer is open". Docs: CHANGELOG.md v1.9.0 "Avoid retaining unmounted drawers in `<Drawer.Provider>`".
- **DRW-STA-15** — MUST. The Provider's *visual state* is `{ swipeProgress, frontmostHeight }`, initially `{0, 0}`. An update may set either field; a non-finite value is stored as 0; listeners are notified only when at least one field changes. It is written by the Viewport (DRW-PTR-16) and SwipeArea (DRW-ALG-04) gestures and read by Indent (DRW-DOM-32).
  Source: `drawer/provider/DrawerProvider.tsx:71-116`. Test: [L3] `DrawerProvider.test.tsx` "synchronizes and restores visual state on Drawer.Indent".

### 4.4 Open/close with swipe

```
open (idle) ──drag──▶ open, swiping (data-swiping, movement vars follow finger)
    ▲                     │ release
    │ snap back           ├─ below threshold / reversed / canceled ─▶ open, idle (movement 0)
    │                     └─ dismiss ─▶ data-swipe-dismiss + data-ending-style (immediately, DRW-ANI-02)
    │                                   └─ onOpenChange(false,'swipe') ─▶ canceled ─▶ reset to open
    │                                                                 └─ accepted ─▶ closed, 'ending' ─▶ unmount (DLG-ANI-03)
closed ──SwipeArea drag ≥ 1 px──▶ open (reason swipe) ── release: commit or close(reason swipe)
```

## 5. Rendered DOM and ARIA

### 5.1 Popup element

- **DRW-DOM-01** — MUST. The Popup renders `data-open`/`data-closed`/`data-starting-style`/`data-ending-style` as DLG-DOM-20 and `data-nested=""` iff its Root is nested (default projection of the `nested` state field).
  Source: `drawer/popup/DrawerPopup.tsx:91-92,310-319,393`. Test: none (the outline omits `data-nested` for the Popup, §16).
- **DRW-DOM-02** — MUST. The Popup renders `data-expanded=""` iff `expanded` (DRW-STA-16).
  Source: `drawer/popup/DrawerPopup.tsx:93-95`; `drawer/popup/DrawerPopupDataAttributes.ts:22`. Test: none.
- **DRW-DOM-03** — MUST. The Popup renders `data-nested-drawer-open=""` iff `nestedOpenDrawerCount > 0`; it is removed in the same commit in which a nested drawer starts closing (its open state becomes `false`), before the nested exit animation finishes.
  Source: `drawer/popup/DrawerPopup.tsx:96-98,157`. Test: [L2] `DrawerPopup.test.tsx` "reports nested presence and frontmost height before passive effects", "keeps the parent nested state when a closed handle-backed nested drawer mounts", "clears parent nested drawer state as soon as a nested drawer closes", "clears parent nested drawer state when a nested drawer starts closing before unmount". Docs: components/drawer/page.mdx §Nested drawers.
- **DRW-DOM-04** — MUST. The Popup renders `data-nested-drawer-swiping=""` iff `nestedSwiping` (DRW-STA-12).
  Source: `drawer/popup/DrawerPopup.tsx:99-101,316`. Test: [L4] `DrawerViewport.test.tsx` "does not mark nested drawers as swiping until movement passes the threshold", "clears nested swiping when a nested drawer swipe is reversed before release".
- **DRW-DOM-05** — MUST. The Popup always renders `data-swipe-direction="<swipeDirection>"`.
  Source: `drawer/popup/DrawerPopup.tsx:102-104`. Test: [L2] `DrawerPopup.test.tsx` "applies a negative snap point offset to upward drawers".
- **DRW-DOM-06** — MUST. The Popup renders `data-swiping=""` while the Viewport gesture is active (from the render after gesture start until the render after it ends); `Drawer.SwipeArea` additionally sets/removes the same attribute imperatively during its gesture (DRW-ALG-04, DRW-PTR-31).
  Source: `drawer/popup/DrawerPopup.tsx:105-107,158`; `drawer/swipe-area/DrawerSwipeArea.tsx:248,280`. Test: [L4] `DrawerViewport.test.tsx` "cleans up when the popup unmounts during an active swipe"; `DrawerSwipeArea.test.tsx` "cancels an active opening gesture when the swipe area becomes disabled".
- **DRW-DOM-07** — MUST. `data-swipe-dismiss=""` is toggled imperatively on both the Popup and the consumer Backdrop: set when a release commits to dismissal (DRW-PTR-12) and again after an accepted swipe close (DRW-PTR-14); removed when the swipe release is cleared (snap back, canceled or rejected dismissal, reopen — DRW-PTR-14/15, DRW-ANI-03).
  Source: `drawer/viewport/DrawerViewport.tsx:179-188,455,697`. Test: [L4] `DrawerRoot.test.tsx` "clears swipe-dismiss styles when swipe close is canceled", "does not dismiss a controlled drawer via swipe when open is always true"; `DrawerViewport.test.tsx` "clears kept-mounted swipe state before descendant layout effects on reopen".
- **DRW-DOM-08** — MUST. The Popup's inline style declares `--nested-drawers` = `nestedOpenDrawerCount` (`0` when none).
  Source: `drawer/popup/DrawerPopup.tsx:377`; `drawer/popup/DrawerPopupCssVars.ts:5`. Test: [L2] `DrawerPopup.test.tsx` "reports nested presence and frontmost height before passive effects" (`'1'`), "does not treat dialogs inside nested drawers as nested drawers" (never `'2'`). Docs: components/drawer/page.mdx §Stacking and animations.
- **DRW-DOM-09** — MUST. The Popup declares `--drawer-height: <popupHeight>px` only while the measured height is non-zero **and** (a nested drawer is present (DRW-STA-12) **or** its own `transitionStatus` is `'ending'`); otherwise the variable is not declared (consumer CSS falls back, e.g. `var(--drawer-height, auto)`). The value is in place in the same commit that adds `data-nested-drawer-open`, and stays while a nested drawer is exiting after `data-nested-drawer-open` was removed.
  Source: `drawer/popup/DrawerPopup.tsx:321-325,378`. Test: [L4] `DrawerPopup.test.tsx` "keeps a fixed height applied while a nested drawer closes", "restores a fixed height before nested state when reopening a nested drawer".
- **DRW-DOM-10** — MUST. The Popup declares `--drawer-frontmost-height: <frontmostHeight>px` when the Root's frontmost height (DRW-STA-11) is non-zero, else not at all.
  Source: `drawer/popup/DrawerPopup.tsx:381-383`. Test: [L2] `DrawerPopup.test.tsx` "includes border size in frontmost height CSS variable for nested drawers", "reports nested presence and frontmost height before passive effects" (`100px`).
- **DRW-DOM-11** — MUST. The Popup declares `--drawer-snap-point-offset`: when snap points apply (DRW-API-02) and the active snap offset (DRW-STA-07) is not `null`, `<offset>px` for `down` and `-<offset>px` for `up`; otherwise `0px`.
  Source: `drawer/popup/DrawerPopup.tsx:327-332,379-380`. Test: [L2] `DrawerPopup.test.tsx` "applies a negative snap point offset to upward drawers" (`-200px`); `DrawerSnapPoints.test.tsx` (`99.5px`, `0px` for `null`).
- **DRW-DOM-12** — MUST. The Popup's drag styles are the swipe tracker's (PSH-DOM-17 with movement variables `--drawer-swipe-movement-x/-y`, PSH-DOM-18), i.e. `0px`/`0px` at rest, inline `transition: none` and `transform: translate3d(…)` plus movement variables while swiping, the final delta kept after release. Exception for `swipeDirection: 'down'` with snap points: the inline `transform` is never set (neither in render nor during progress updates, which also remove any inline `transform`), and while swiping `--drawer-swipe-movement-y` = `getSnapPointSwipeMovement(activeOffset ?? 0, deltaY)` px (DRW-STA-09), so dragging above the fully-open edge is square-root damped. Without a Viewport the variables are not declared by the Popup.
  Source: `drawer/popup/DrawerPopup.tsx:334-356,375`; `drawer/viewport/DrawerViewport.tsx:398-408`; `utils/useSwipeDismiss.ts:969-994`. Test: [L4] `DrawerRoot.test.tsx` "damps active snap point overshoot after the swipe direction is established" (`−√150`, transform `''`); `DrawerViewport.test.tsx` "keeps damped snap-point styles when a duplicate-coordinate pointermove arrives" (`-10px`), "keeps tracking the finger when a claimed drag returns inside the slop" (`30px`), "keeps driving a claimed drag through a non-cancelable touchmove" (`50px`, `100px`, `150px`).
- **DRW-DOM-13** — MUST. The Popup declares `--drawer-swipe-strength` = the release scalar of DRW-ALG-02 when one is stored (finite and > 0), else `1`. The scalar is stored synchronously (committed synchronously) at the moment a release commits to dismissal and cleared when the swipe release is cleared.
  Source: `drawer/popup/DrawerPopup.tsx:159,384-387`; `drawer/viewport/DrawerViewport.tsx:125,184-188,459-472,887-895`. Test: none. Docs: components/drawer/page.mdx §Stacking and animations.
- **DRW-DOM-14** — MUST. The Popup declares `--drawer-swipe-progress: 0` inline and, post-commit, pre-paint and on every change of the Root's nested swipe progress (DRW-STA-13), sets it imperatively to that value (`0` when ≤ 0); on unmount it is reset to `0`.
  Source: `drawer/popup/DrawerPopup.tsx:244-269,376`. Test: [L4] `DrawerViewport.test.tsx` "clears nested swiping when a nested drawer swipe is reversed before release" (`'0'`), "clears nested swipe state after an unattributed snap point gesture", "clears nested progress before descendant layout effects when the child closes" (`'0.5'` → `'0'`).
- **DRW-DOM-15** — MUST. The Popup's inline style is composed in this order, later entries winning: the drag styles (DRW-DOM-12), then `--drawer-swipe-progress`, `--nested-drawers`, `--drawer-height`, `--drawer-snap-point-offset`, `--drawer-frontmost-height`, `--drawer-swipe-strength`, then the consumer `style`; variables whose value is undefined are omitted.
  Source: `drawer/popup/DrawerPopup.tsx:374-390`. Test: [L2] describeConformance (RND-DOM-16). (L4 for the drag-style ordering clause)
- **DRW-DOM-16** — MUST. The Popup renders `id` = the consumer `id` when given, else the generated popup id (DLG-DOM-16), plus `role`, `aria-labelledby`, `aria-describedby`, `tabindex="-1"`, `data-base-ui-focusable` and `hidden` (when not mounted) exactly as Dialog.Popup (DLG-DOM-17/18, DLG-ARIA-04…06), and merges the Root's dismissal handlers.
  Source: `drawer/popup/DrawerPopup.tsx:151,360-368`. Test: [L2] `DrawerRoot.test.tsx` "synchronizes trigger aria-controls with the popup id".

### 5.2 Backdrop element

- **DRW-DOM-17** — MUST. `Drawer.Backdrop` renders as Dialog's Backdrop (DLG-STR-05, DLG-DOM-07…10, DLG-ARIA-08) and additionally has inline `pointer-events: none` while the effective open state is `false` (so a closing or kept-mounted backdrop does not intercept presses).
  Source: `drawer/backdrop/DrawerBackdrop.tsx:35-54`. Test: none.
- **DRW-DOM-18** — MUST. The Backdrop declares `--drawer-swipe-progress: 0` inline; during gestures it is set imperatively to the dismiss progress (DRW-PTR-16), to the resting snap progress (DRW-PTR-16), or to `1 − openProgress` during a SwipeArea gesture (DRW-ALG-04), and reset to `0` on dismissal and cleanup.
  Source: `drawer/backdrop/DrawerBackdrop.tsx:47`; `drawer/viewport/DrawerViewport.tsx:218-233,642-646`; `drawer/swipe-area/DrawerSwipeArea.tsx:259,291`. Test: [L4] `DrawerViewport.test.tsx` "keeps the resting snap point progress when a press never starts a swipe", "publishes resting snap progress before descendant layout effects"; `DrawerSwipeArea.test.tsx` "synchronizes overshoot styles across the popup, backdrop, and provider indent".
- **DRW-DOM-19** — MUST. The Backdrop declares `--drawer-swipe-strength: 1` inline and never changes it.
  Source: `drawer/backdrop/DrawerBackdrop.tsx:48`. Test: none (see §16).
- **DRW-DOM-20** — MUST. The Backdrop's `data-swiping=""` is toggled imperatively: on by the Viewport gesture start and by SwipeArea movement, off at gesture end, SwipeArea cleanup, and Viewport teardown (DRW-ANI-06).
  Source: `drawer/viewport/DrawerViewport.tsx:346-353,1090-1092`; `drawer/swipe-area/DrawerSwipeArea.tsx:257,290`. Test: [L4] `DrawerViewport.test.tsx` "toggles data-swiping on the backdrop while swiping", "starts touch swipes from interactive elements", "clears the backdrop data-swiping attribute when the drawer unmounts mid-swipe"; `DrawerSwipeArea.test.tsx` "synchronizes overshoot styles across the popup, backdrop, and provider indent".
- **DRW-DOM-21** — MUST. While a gesture shows non-zero progress and the frontmost height is > 0, the Backdrop's inline `--drawer-height` is `<frontmostHeight>px`; otherwise the property is removed.
  Source: `drawer/viewport/DrawerViewport.tsx:228-232`; `drawer/swipe-area/DrawerSwipeArea.tsx:260-264,292`. Test: [L4] `DrawerSwipeArea.test.tsx` "synchronizes overshoot styles…" (`100px`), "omits frontmost height when an opening drawer has no measured height".

### 5.3 Viewport element

- **DRW-DOM-22** — MUST. `Drawer.Viewport` never renders `data-nested-dialog-open` (even when a consumer passes it); `data-nested` and the other attributes are as DLG-DOM-12…15.
  Source: `drawer/viewport/DrawerViewport.tsx:1052-1054`; `internals/useRenderElement.tsx:77-87`. Test: [L2] `DrawerViewport.test.tsx` "does not mark nested drawers as swiping until movement passes the threshold" (no `data-nested-dialog-open` on either viewport).
- **DRW-DOM-23** — MUST. A `Drawer.VirtualKeyboardProvider` writes `--drawer-keyboard-inset: <max(0, ceil(inset))>px` on the Viewport element during alignment (DRW-FOC-05) and removes it when the drawer closes or the provider unmounts; it is never present without the provider.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:241-246,328,580`; `drawer/viewport/DrawerViewportCssVars.ts:6`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "sets the keyboard inset CSS variable for a focused input without a scroll target" (`300px`), "computes the keyboard inset from a non-zero visual viewport offset" (`260px`).

### 5.4 Content element

- **DRW-DOM-24** — MUST. `Drawer.Content` renders `data-drawer-content=""` (internal marker used by DRW-PTR-04) and never `data-base-ui-swipe-ignore` or `data-swipe-ignore`.
  Source: `drawer/content/DrawerContent.tsx:22-25`; `drawer/content/drawerContentAttribute.ts:1`. Test: [L2] `DrawerContent.test.tsx` "does not add public swipe-ignore attributes".

### 5.5 SwipeArea element

- **DRW-DOM-25** — MUST. The SwipeArea renders `data-open=""` when the drawer is open and `data-closed=""` otherwise.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:49-51`. Test: [L2] `DrawerSwipeArea.test.tsx` "opens the drawer when swiped in the open direction".
- **DRW-DOM-26** — MUST. The SwipeArea renders `data-swiping=""` while its own gesture is active (tracker swiping state).
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:52-54,439`. Test: [L4] `DrawerSwipeArea.test.tsx` "applies data-swiping during an active swipe gesture".
- **DRW-DOM-27** — MUST. The SwipeArea always renders `data-swipe-direction="<opening direction>"`.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:55-57`. Test: none.
- **DRW-DOM-28** — MUST. The SwipeArea renders `data-disabled=""` iff `disabled`.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:58-60`. Test: [L2] `DrawerSwipeArea.test.tsx` "cancels an active opening gesture when the swipe area becomes disabled".
- **DRW-DOM-29** — MUST. The SwipeArea's inline style has `pointer-events: none` while it is not enabled (DRW-PTR-26) and `touch-action: pan-y` when its opening direction is horizontal, `pan-x` when vertical; consumer `style` merges after.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:70-72,452-455`. Test: none.
- **DRW-DOM-30** — MUST. The SwipeArea renders its `id` attribute only once an id is available (consumer `id`, or a generated id); while none is available it renders no `id` and still works (it is then registered when the id appears).
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:110-119,487`. Test: [L2] `DrawerSwipeArea.test.tsx` "works before a generated swipe area id is available", "registers the swipe area once a generated id becomes available".

### 5.6 Indent and IndentBackground elements

- **DRW-DOM-31** — MUST. Indent and IndentBackground render `data-active=""` when the nearest Provider is active and `data-inactive=""` otherwise (also without a Provider).
  Source: `drawer/indent/DrawerIndent.tsx:11-18`; `drawer/indent-background/DrawerIndentBackground.tsx:8-15`. Test: [L2] `DrawerIndent.test.tsx` "sets data-active when any drawer is open"; `DrawerIndentBackground.test.tsx` "sets data-active when any drawer is open"; `DrawerProvider.test.tsx` "allows indent parts to render without a provider".
- **DRW-DOM-32** — MUST. Indent declares `--drawer-swipe-progress: 0` inline; post-commit, pre-paint and on every Provider visual-state change it sets `--drawer-swipe-progress` to the visual `swipeProgress` (`0` when ≤ 0) and `--drawer-height` to `<frontmostHeight>px` when > 0 (removing it otherwise); on unmount (or Provider change) it resets progress to `0` and removes `--drawer-height`.
  Source: `drawer/indent/DrawerIndent.tsx:40-82`. Test: [L4] `DrawerProvider.test.tsx` "synchronizes and restores visual state on Drawer.Indent"; `DrawerViewport.test.tsx` "publishes and clears swipe progress through Drawer.Provider".

### 5.7 CSS variable registration

- **DRW-DOM-33** — SHOULD. The first time any Drawer Popup mounts in the page, where `CSS.registerProperty` exists, the variables `--drawer-swipe-movement-x`, `--drawer-swipe-movement-y`, `--drawer-snap-point-offset` are registered as `<length>`, `inherits: false`, initial `0px`, and `--drawer-swipe-progress` (initial `0`) and `--drawer-swipe-strength` (initial `1`) as `<number>`, `inherits: false`; registration errors (already registered) are ignored and registration is attempted once per page. Consequently these variables are visible only on the element that declares them, not on descendants.
  Source: `drawer/popup/DrawerPopup.tsx:28-89,229`. Test: none. Docs: CHANGELOG.md v1.3.0 "Disable inheritance for swipe CSS variables".

### 5.8 ARIA

- **DRW-ARIA-01** — MUST. The SwipeArea renders `role="presentation"` and `aria-hidden="true"`; it is not focusable and is not announced.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:450-451`. Test: none.
- **DRW-ARIA-02** — MUST. Because a SwipeArea is a registered trigger, a swipe-open makes the SwipeArea the active trigger (`details.trigger` = the SwipeArea), so every `Drawer.Trigger` then reports `aria-expanded="false"` and no `aria-controls` (DLG-ARIA-02/03), and the default return-focus target (DLG-FOC-06) is the SwipeArea element.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:302-305`; `utils/popups/popupStoreUtils.ts:203-212`. Test: none (see §16).
- **DRW-ARIA-03** — MUST. All other ARIA (Popup `role="dialog"`, `aria-labelledby`/`aria-describedby`, Trigger `aria-haspopup="dialog"`/`aria-expanded`/`aria-controls`, outside `aria-hidden`) is Dialog's (DLG-ARIA-01…12); Drawer adds no live region and no `aria-*` for snap points or swipe state.
  Source: `drawer/popup/DrawerPopup.tsx:362-368`. Test: [L2] `DrawerRoot.test.tsx` "synchronizes trigger aria-controls with the popup id", "supports imperative actions with handles".

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Escape</kbd> | drawer open and topmost | Close, reason `escape-key` | DLG-KEY-01…05 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | per `modal` | Focus trap / focus-out as Dialog | DLG-KEY-06…08 |
| Arrow keys, <kbd>Home</kbd>, <kbd>End</kbd> | keydown bubbling to the Popup | Propagation stopped | DLG-KEY-09 |
| <kbd>Enter</kbd>/<kbd>Space</kbd> | Trigger / Close | Activate | DLG-KEY-10 |
| any key | SwipeArea, snap points | No keyboard equivalent | DRW-KEY-01 |

- **DRW-KEY-01** — MUST. Drawer adds no key handling beyond Dialog's: the SwipeArea is not focusable (DRW-ARIA-01), and neither swipe-to-dismiss nor snap point changes have a keyboard equivalent (a keyboard user closes with <kbd>Escape</kbd> or a Close part). The Popup's composite-key propagation stop applies unchanged.
  Source: `drawer/popup/DrawerPopup.tsx:369-373`; `drawer/swipe-area/DrawerSwipeArea.tsx:449-485`. Test: [L4] `DrawerPopup.test.tsx` "stops composite navigation keys from escaping the popup" (ArrowDown stopped, `a` bubbles).

### 6.2 Pointer and touch

Two independent gestures exist: the **dismiss gesture** handled by `Drawer.Viewport` on the Popup (§6.2.1–6.2.5) and the **open gesture** of `Drawer.SwipeArea` (§6.2.6). Both use the shared swipe tracker (PSH §4.7.5); this section states Drawer's configuration and everything Drawer adds around it. Displacement along a direction is PSH's `disp(dir, Δ)` (`up: −Δy`, `down: +Δy`, `left: −Δx`, `right: +Δx`); *size along a direction* is `offsetWidth` for left/right and `offsetHeight` for up/down.

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| Mouse/pen press + drag on popup | open, mounted, no nested drawer open, not on an ignored target | Popup follows (damped against direction), backdrop/indent progress | DRW-PTR-01…09, DRW-PTR-16 |
| Release past threshold or fast | as above | Dismiss with reason `swipe` | DRW-PTR-10, DRW-PTR-12…14 |
| Release below threshold / reversed ≥ 10 px | as above | Snap back | DRW-PTR-10 |
| Release with snap points | vertical drawer | Settle on a snap point or dismiss | DRW-PTR-11, DRW-ALG-01 |
| Touch press + drag | popup, outside scrollers or at their dismiss edge | As mouse; native scroll arbitration | DRW-PTR-19…24, DRW-ALG-03 |
| Touch drag on a scroll container not at its dismiss edge | | Native scroll; no swipe | DRW-PTR-21 |
| Touch drag on `input[type=range]` | | Native; no swipe | DRW-PTR-19 |
| Two-finger touch move | | Left to the browser (pinch zoom) | DRW-PTR-21 |
| Press on `[data-base-ui-swipe-ignore]` | any value, mouse/pen | No swipe | DRW-PTR-04 |
| Touch on `[data-base-ui-swipe-ignore]` | no value / drawer-axis value | No swipe | DRW-PTR-19 |
| Touch on `[data-base-ui-swipe-ignore="<cross axis>"]` | | Arbitrated like a cross-axis scroller | DRW-PTR-19, DRW-ALG-03 |
| Mouse/pen press inside `Drawer.Content` | | No swipe; text selectable | DRW-PTR-04 |
| Press/drag on SwipeArea toward the open direction | closed (or own gesture active), not disabled | Opens after ≥ 1 px; popup follows; release commits or closes | DRW-PTR-25…32 |

#### 6.2.1 Dismiss gesture: tracker configuration and gating

- **DRW-PTR-01** — MUST. `Drawer.Viewport` configures the swipe tracker (PSH-PTR-09…28, PSH-ALG-07…12) with: tracked element = the Popup; `enabled = mounted && nestedOpenDrawerCount === 0`; `directions` per DRW-PTR-02; `swipeThreshold` per DRW-PTR-03; `ignoreSelectorWhenTouch: false`; `ignoreScrollableAncestors: true`; `trackDrag: true` (default); movement variables `--drawer-swipe-movement-x/-y`; `canStart` per DRW-PTR-06; and the callbacks of DRW-PTR-08, -10…17. The tracker's damping (PSH-ALG-08), reverse cancel (10 px, PSH-PTR-15), velocity (PSH-ALG-10), primary-button rules (PSH-PTR-09/20/21) and drag styles (PSH-DOM-17) apply unchanged.
  Source: `drawer/viewport/DrawerViewport.tsx:310-699`. Test: [L4] `DrawerViewport.test.tsx` "ends swipe drag when the primary mouse button is released mid-gesture", "cancels pointer drags and ignores compatibility touch pointer cancellation", "toggles data-swiping on the backdrop while swiping". Docs: CHANGELOG.md v1.6.0 "Commit swipe on primary-button release".
- **DRW-PTR-02** — MUST. The tracker's allowed directions are `[swipeDirection]`, except when snap points are non-empty and the direction is vertical: then `['down', 'up']` for `down` and `['up', 'down']` for `up` (both directions on the axis allowed, the dismiss direction first). The opposite direction of a single-direction drawer is damped (`sign(d)·√|d|`, PSH-ALG-08) and so is any movement on the other axis.
  Source: `drawer/viewport/DrawerViewport.tsx:167-177`. Test: [L4] `DrawerViewport.test.tsx` "keeps damped snap-point styles when a duplicate-coordinate pointermove arrives"; `DrawerRoot.test.tsx` "damps active snap point overshoot after the swipe direction is established".
- **DRW-PTR-03** — MUST. The dismiss threshold for direction `d` is `max(size along d × 0.5, 10)` px of the Popup (re-evaluated when the direction is detected, PSH-PTR-12, and again at release by DRW-PTR-10).
  Source: `drawer/viewport/DrawerViewport.tsx:45,354-356,1102-1108`. Test: [L4] `DrawerRoot.test.tsx` "uses a size-based swipe threshold" (width 200: 90 px keeps open, 120 px closes), "closes an uncontrolled drawer after a slow long drag".
- **DRW-PTR-04** — MUST. On the Viewport `pointerdown` (bubbling from any descendant): the pointer type is remembered (and, for `pen`, the next `touchstart` is marked to be ignored, DRW-PTR-19); then nothing further happens unless the drawer is open, mounted and has no open nested drawer; a press whose element at the press point (hit-tested in the Viewport's root node, UTL-DOM-27) is inside an element carrying `data-base-ui-swipe-ignore` (any value) or inside `Drawer.Content` does not start a gesture; a `pointerType: 'touch'` press is ignored (touch uses §6.2.2); otherwise the press is passed to the tracker.
  Source: `drawer/viewport/DrawerViewport.tsx:920-944,1094-1100`. Test: [L4] `DrawerViewport.test.tsx` "does not start non-touch swipes from Drawer.Content", "treats pen interactions on Drawer.Content as non-touch swipes", "does not start a mouse drag swipe from an SVG inside scrollable content", "does not start pointer swipes while a closed drawer remains mounted", "ignores compatibility touch pointer gestures with real displacement". Docs: components/drawer/page.mdx §Anatomy.
- **DRW-PTR-05** — MUST. Viewport `pointermove` events with `pointerType: 'touch'` are not passed to the tracker; `pointerup`/`pointercancel` clear the remembered pointer type and are passed to the tracker unless `pointerType` is `touch`.
  Source: `drawer/viewport/DrawerViewport.tsx:945-961,908-911`. Test: [L4] `DrawerViewport.test.tsx` "cancels pointer drags and ignores compatibility touch pointer cancellation", "ignores compatibility touch pointer gestures with real displacement".
- **DRW-PTR-06** — MUST. The tracker may start a gesture (`canStart`) only when: a Popup element exists; the element at the gesture point is inside the Popup, unless this is a touch gesture whose `touchstart` began inside the Popup (so a touch that started inside may activate after leaving it); for touch, no text selection is being adjusted in the Popup (DRW-PTR-21 rule (b)); and, for touch over a cross-axis gesture target, the gesture has been attributed to the drawer axis or allowed from a scroll edge (DRW-ALG-03). A refused start stays pending (PSH-PTR-09) and may start on a later move.
  Source: `drawer/viewport/DrawerViewport.tsx:357-390,1136-1151`. Test: [L4] `DrawerViewport.test.tsx` "does not jump when touch starts outside the popup and then enters it" (movement `0`), "dismisses when touch starts outside the popup, then continues swiping down inside it", "ignores swipe input until a popup is mounted", "preserves an expanded touch selection whose focus endpoint is in the popup", "does not start swiping when adjusting input selection handles" / "…textarea…" / "…contenteditable…" / "…regular text…".
- **DRW-PTR-07** — MUST. Because `ignoreSelectorWhenTouch` is `false`, a mouse/pen press on `button, a, input, select, textarea, label, [role="button"]` never starts a dismiss gesture (PSH-PTR-10), while a touch on those elements does.
  Source: `drawer/viewport/DrawerViewport.tsx:314`; `utils/useSwipeDismiss.ts:39`. Test: [L4] `DrawerViewport.test.tsx` "starts touch swipes from interactive elements". Docs: CHANGELOG.md v1.3.0 "Make `data-base-ui-swipe-ignore` explicit for touch interactions".
- **DRW-PTR-08** — MUST. When a non-touch gesture starts and the document selection is non-collapsed with its anchor or focus node (or that node's parent element) inside the Popup, all selection ranges are removed; a selection entirely outside the Popup, and any selection during a touch gesture start, is preserved.
  Source: `drawer/viewport/DrawerViewport.tsx:320-345`. Test: [L4] `DrawerViewport.test.tsx` "clears text selection on swipe start", "preserves text selection outside the popup when a swipe starts", "does not clear text selection on touch swipe start", "clears a selection whose endpoints are popup elements".
- **DRW-PTR-09** — MUST. The Viewport gesture never starts while the drawer is closed (also when kept mounted or during its exit), while a nested drawer is open, or before a Popup is mounted; a nested Drawer's own Viewport handles its gesture independently.
  Source: `drawer/viewport/DrawerViewport.tsx:311,924-926,971-974`. Test: [L4] `DrawerViewport.test.tsx` "does not start pointer swipes while a closed drawer remains mounted", "ignores swipe input until a popup is mounted".

#### 6.2.2 Dismiss gesture: touch path

- **DRW-PTR-19** — MUST. On the Viewport `touchstart`: (1) if the last `pointerdown` was `pen` and marked, the mark is cleared, touch state reset, and nothing else happens; (2) if the drawer is not open/mounted or a nested drawer is open, touch state is reset; (3) with no active touch nothing happens; (4) a touch whose composed path contains an `<input type="range">` resets touch state (no swipe); (5) a target (composed-path target, or the Viewport when it is not an element) outside the Viewport resets state and ignores the gesture; (6) the VirtualKeyboardProvider is notified (DRW-FOC-10); (7) when the element at the touch point is inside a `[data-base-ui-swipe-ignore]` element whose value is not the cross-axis value (`x` for vertical drawers, `y` for horizontal; an outer valueless ancestor wins over an inner axis value) the gesture is ignored; (8) otherwise touch state is recorded — start/last point, `startedInPopup`, the scrollable target on the drawer axis (UTL-DOM-29, from the event target), whether a cross-axis gesture target exists (a cross-axis scroller or a cross-axis `data-base-ui-swipe-ignore` value at the point), and `allowSwipe = false` when the drawer-axis scroller is not at its dismiss start edge (DRW-ALG-03), else undecided — and the event is passed to the tracker's `touchstart` (which may start the gesture immediately, setting `data-swiping` before any move).
  Source: `drawer/viewport/DrawerViewport.tsx:962-1041,1153-1159,1243-1270`. Test: [L4] `DrawerViewport.test.tsx` "starts touch swipes from interactive elements", "uses shadow-root hit testing for touch swipe targets", "uses the event target for non-keyboard touch scroll arbitration", "does not start touch swipes from elements with data-base-ui-swipe-ignore", "data-base-ui-swipe-ignore axis values" (8 tests), "ignores touchstart when there is no active touch", "falls back to the viewport when a touch event has no element target", "does not block touchmove on native range inputs", "does not block touchmove on slider thumb range inputs", "treats pen interactions on Drawer.Content as non-touch swipes". Docs: CHANGELOG.md v1.9.0 "Add `x` and `y` options to `data-base-ui-swipe-ignore`".
- **DRW-PTR-20** — MUST. While the drawer is open and mounted and a Viewport (or Popup) element exists, a `touchmove` listener is registered on the owner document in the capture phase with `passive: false`, and removed otherwise (so closed drawers add no non-passive touch listener). Touch moves reach the tracker only through this listener (the Viewport has no element-level touch-move handler).
  Source: `drawer/viewport/DrawerViewport.tsx:707-821`. Test: [L4] `DrawerViewport.test.tsx` "registers the non-passive document touchmove listener only while open". Docs: CHANGELOG.md v1.9.0 "Avoid touch event overhead while the drawer is closed"; v1.6.0 "Drive swipe gestures natively to stop per-frame re-rasterization".
- **DRW-PTR-21** — MUST. For each document `touchmove` (after notifying the VirtualKeyboardProvider, DRW-FOC-10), unless the current touch is being ignored or no touch state exists: (a) a move with exactly two touches is left untouched; (b) when the active element inside the Viewport is an `input`/`textarea` with a non-empty selection range, or the document selection is non-collapsed and touches the Viewport, or a nested drawer is open, the move is left untouched; (c) when DRW-ALG-03 yields, the move is left untouched; (d) when there is no drawer-axis scroll target, or it is the document element or body, the move is `preventDefault()`ed (if cancelable), its propagation stopped, and it is fed to the tracker; (e) when the recorded scroll target no longer overflows on the drawer axis, the move is `preventDefault()`ed (if cancelable) and propagation stopped, but not fed; (f) otherwise, when the drawer-axis movement since the last move is non-zero: if the swipe is not yet allowed, it becomes allowed (and the move is prevented) only when the move is cancelable and moving toward dismissal from the scroller's start edge, else it is marked not allowed; if already allowed, the move is prevented when cancelable; (g) when allowed, propagation is stopped and the move fed to the tracker — also for non-cancelable moves. The last touch position is then updated. Server: relaxed, see §9.
  Source: `drawer/viewport/DrawerViewport.tsx:717-804,1229-1266`. Test: [L4] `DrawerViewport.test.tsx` "prevents touchmove at scroll top when swiping down on scrollable content", "prevents touchmove at scroll bottom when swiping up on scrollable content", "prevents touchmove when a scrollable ancestor wraps the popup at the top", "prevents touchmove when there is no scroll container", "allows touchmove when scrolling down from scroll top", "does not start an opposite-direction swipe from scroll bottom for down drawers with snap points", "does not start an opposite-direction swipe from scroll right edge for right drawers", "starts swipe-to-dismiss after a scrollable container reaches the dismiss edge", "dismisses from a top-edge scroll container with a touch swipe down" (and the bottom/left/right-edge variants), "allows horizontal swipe dismiss from a vertical scroll container", "does not prevent native touch scrolling in portaled descendants", "leaves pinch-zoom touchmove to the browser", "prevents page scrolling for a non-overflowing touch scroll container", "keeps a claimed scroll-edge swipe moving through non-cancelable events", "does not claim a stationary touch in overflowing content", "starts a swipe away from the page scroll edge when the body is a scroll container". Docs: CHANGELOG.md v1.8.0 "Ignore the page scroller when starting a swipe"; v1.4.0 "Fix touch scroll in portaled popups".
- **DRW-PTR-22** — MUST. Cross-axis arbitration follows DRW-ALG-03 with slop 6 px and bias 2 px; a drag attributed to the drawer axis keeps being tracked even when it later returns inside the slop or arrives as a non-cancelable move.
  Source: `drawer/viewport/DrawerViewport.tsx:56-57,1172-1227`. Test: [L4] `DrawerViewport.test.tsx` "does not prevent a sub-slop first touchmove over cross-axis scrollable content", "claims the gesture once the drawer axis passes the slop over cross-axis scrollable content", "yields the gesture when the browser commits to a native cross-axis scroll", "keeps tracking the finger when a claimed drag returns inside the slop", "keeps driving a claimed drag through a non-cancelable touchmove", "does not lock vertical swipe after minor cross-axis jitter in down drawers", "does not hijack cross-axis gestures from mixed-axis scroll containers", "does not block vertical scrolling in right drawers when only vertical overflow exists" (and the left/down/up variants), "allows touch gestures on a portaled combobox popup without starting drawer swipe", "expands a collapsed sheet when the first move from a $target leaves the popup" (×3). Docs: CHANGELOG.md v1.7.0 "Fix cross-axis scroll blocked on iOS below the touchmove slop".
- **DRW-PTR-23** — MUST. `touchend` and `touchcancel` on the Viewport notify the VirtualKeyboardProvider, clear all touch state (including the pen mark and remembered pointer type) and are passed to the tracker; eligibility (allowed swipe, attribution) is never inherited by the next touch.
  Source: `drawer/viewport/DrawerViewport.tsx:902-906,1042-1051`. Test: [L4] `DrawerViewport.test.tsx` "expands a collapsed sheet when the first move from a $target leaves the popup" (second touch not swiping).
- **DRW-PTR-24** — MUST. Only `data-base-ui-swipe-ignore` is honored; the legacy `data-swipe-ignore` attribute (still honored by Toast) has no effect on Drawer.
  Source: `drawer/viewport/DrawerViewport.tsx:37-39,59-62`; `internals/constants.ts:8-12`. Test: [L4] `DrawerViewport.test.tsx` "still allows touch swipes from elements with legacy data-swipe-ignore".

#### 6.2.3 Dismiss gesture: release without snap points

- **DRW-PTR-10** — MUST. With no snap points, the release decision (returned to the tracker as its `onRelease` override, PSH-PTR-24) is: no intended direction → clear the swipe release and let the tracker decide by its threshold rule; displacement along the intended direction `≤ 0` → snap back; overall velocity along the direction (Δ / max(duration, 50 ms), PSH-PTR-23) `≥ 0.5` px/ms → dismiss; displacement `>` threshold (DRW-PTR-03) → dismiss; else snap back. A dismissal runs DRW-PTR-12 before the tracker's `onDismiss` (DRW-PTR-14); a snap back clears the swipe release.
  Source: `drawer/viewport/DrawerViewport.tsx:46,475-500`. Test: [L4] `DrawerRoot.test.tsx` "uses a size-based swipe threshold", "closes an uncontrolled drawer after a slow long drag"; `DrawerViewport.test.tsx` "uses regular horizontal dismissal when snap points are configured", "falls back to regular dismissal when all snap points are invalid", "dismisses from a top-edge scroll container with a touch swipe down".
- **DRW-PTR-12** — MUST. When a release commits to dismissal ("start swipe release"), synchronously and before the open-change request: the nested-swipe report is finished (DRW-PTR-17); `data-swipe-dismiss` is set on the Popup and Backdrop; the Popup's inline `transition` is removed; `data-ending-style=""` is set on the Popup imperatively; and the strength scalar (DRW-ALG-02) is stored and committed synchronously (DRW-DOM-13).
  Source: `drawer/viewport/DrawerViewport.tsx:451-473`. Test: [L4] `DrawerViewport.test.tsx` "does not start swipe dismissal when closing the snap point is canceled" (none of these attributes appear when the dismissal is rejected earlier).
- **DRW-PTR-13** — SHOULD. The strength scalar is computed per DRW-ALG-02 so that faster releases with less remaining distance yield a smaller `--drawer-swipe-strength` (shorter consumer exit transition).
  Source: `drawer/viewport/DrawerViewport.tsx:50-55,236-293`. Test: none.
- **DRW-PTR-14** — MUST. On the tracker's dismissal (`onDismiss`): the Provider visual state is set to `{0, 0}`; the Backdrop's `--drawer-swipe-progress` is set to `0` and its `--drawer-height` removed; a close is requested with reason `swipe` and the native release event (no trigger supplied, so the active trigger is back-filled, DLG-STA-15). If the request is canceled: a snap point remembered by DRW-ALG-01 (`pending`) is re-requested with reason `swipe`; the tracker is reset (movement back to `0px`); the swipe release is cleared (`data-swipe-dismiss` and the Popup's imperative `data-ending-style` removed, strength cleared). If the store is closed afterwards (accepted, uncontrolled or synchronously controlled): `data-swipe-dismiss` is set (again). Otherwise DRW-PTR-15 applies.
  Source: `drawer/viewport/DrawerViewport.tsx:639-698`. Test: [L4] `DrawerRoot.test.tsx` "clears swipe-dismiss styles when swipe close is canceled", "restores snap point and swipe offsets when swipe close is canceled", "does not restore snap point when a controlled swipe close is accepted by the parent", "finishes a controlled accepted dismissal while kept mounted"; `DrawerViewport.test.tsx` "dismisses when touch starts outside the popup, then continues swiping down inside it". Docs: CHANGELOG.md v1.8.0 "Respect canceled snap point dismissal".
- **DRW-PTR-15** — MUST. When a non-canceled swipe close leaves the store open (a controlled owner that has not, or not yet, set `open={false}`), the dismissal proceeds optimistically and is re-checked on the next frame: if the store is still open, the pending snap point is restored (reason `swipe`), the swipe release cleared and the tracker reset, leaving the drawer open with no `data-swipe-dismiss`/`data-ending-style`; if it closed in the meantime nothing more happens. An owner that closes asynchronously later closes the drawer without the swipe exit styles.
  Source: `drawer/viewport/DrawerViewport.tsx:666-694`. Test: [L4] `DrawerRoot.test.tsx` "does not dismiss a controlled drawer via swipe when open is always true", "restores a snap point when a controlled parent rejects dismissal". Docs: CHANGELOG.md v1.3.0 "Prevent swipe dismissal when component is controlled".

#### 6.2.4 Dismiss gesture: release with snap points

- **DRW-PTR-11** — MUST. With non-empty snap points: a horizontal `swipeDirection` clears the release and defers to the tracker (regular dismissal, DRW-PTR-10 not applied); a measured popup height of 0 → snap back (`false`); an empty resolved list → clear and defer to the tracker (regular threshold dismissal); otherwise the decision is DRW-ALG-01. Snap point changes caused by a release use reason `swipe` and the release event.
  Source: `drawer/viewport/DrawerViewport.tsx:502-638`. Test: [L4] `DrawerRoot.test.tsx` "allows dragging past a snap point when snapToSequentialPoints is enabled", "advances on a short flick with a trailing stationary sample: %s" (×2), "stays on the current snap point after repeated stationary samples", "advances to the next snap point on fast flicks when snapToSequentialPoints is enabled", "keeps the drawer open on low-velocity swipes near a snap point", "keeps the drawer open when the release velocity reverses during an upward swipe", "chooses the nearest sequential snap point on a slow drag", "advances a sequential snap point toward dismissal", "dismisses past the final sequential snap point", "dismisses when a slow snap-point drag ends closer to closed", "dismisses from upward snap points using upward velocity", "opens from a controlled null snap point", "dismisses from a controlled null snap point", "keeps the first sequential snap point on a fast opening flick", "keeps the current snap point after a sub-threshold drag", "dismisses a non-sequential snap point when closed is nearest"; `DrawerViewport.test.tsx` "uses regular horizontal dismissal when snap points are configured", "does not resolve snap points before the popup has a measurable height", "falls back to regular dismissal when all snap points are invalid", "does not dismiss from a fast swipe that was never attributed to the snap point axis", "navigates snap points from a drag that was never attributed to the swipe axis", "does not dismiss from an unattributed fast swipe with snapToSequentialPoints", "does not start swipe dismissal when closing the snap point is canceled", "settles on the nearest snap point when an unattributed drag ends past the last snap point" (×2 incl. sequential). Docs: CHANGELOG.md v1.8.0 "Ignore swipes without an attributed direction when using snap points".

#### 6.2.5 Progress publication and nested drawers

- **DRW-PTR-16** — MUST. On every tracker progress report, the Viewport computes `p` = the tracker progress (PSH-ALG-09) or, with a snap range (DRW-STA-17) and popup height > 0, DRW-ALG-05; then publishes: Backdrop `--drawer-swipe-progress` = `p` when the drawer is open, not nested and `p > 0`, else `0`; Backdrop `--drawer-height` per DRW-DOM-21; Provider visual state `{ swipeProgress: (open && !nested) ? p : 0, frontmostHeight: that > 0 ? frontmostHeight : 0 }`; and, while swiping and the drawer has a parent drawer, `p` (or 0 when closed) to the parent's nested swipe progress. A report while not swiping sends 0 to the parent and finishes the nested-swipe report. Additionally, post-commit, pre-paint whenever the snap state, open state or nesting changes and no gesture is active, the resting snap progress (0 when closed or nested) is published the same way (without notifying the parent).
  Source: `drawer/viewport/DrawerViewport.tsx:199-234,391-433,823-840`. Test: [L4] `DrawerViewport.test.tsx` "publishes and clears swipe progress through Drawer.Provider" (≈ 0.39 with popup height 100 and 40 px drag; `0` and `--drawer-height` removed at 0), "keeps the resting snap point progress when a press never starts a swipe", "publishes resting snap progress before descendant layout effects", "clears nested swipe state after an unattributed snap point gesture", "clears nested swipe state after an attributed drag settles on a snap point". Docs: components/drawer/page.mdx §Stacking and animations.
- **DRW-PTR-17** — MUST. A nested drawer reports *swiping* to its parent (DRW-STA-12) once per gesture, the first time a progress report during swiping has `|displacement along (intended ?? swipeDirection)| ≥ 10` px; it reports *not swiping* when the gesture ends without a parent progress channel, when a progress report while swiping drops to `≤ 0`, when a report arrives while not swiping, at swipe-release start, on settle (DRW-ALG-01) and on Viewport teardown — each "finish" sends `false` only if `true` was sent.
  Source: `drawer/viewport/DrawerViewport.tsx:45,190-197,295-308,346-353,394-396,426-430,454`. Test: [L4] `DrawerViewport.test.tsx` "does not mark nested drawers as swiping until movement passes the threshold", "clears nested swiping when a nested drawer swipe is reversed before release", "clears nested swipe state after an unattributed snap point gesture", "clears nested swipe state after an attributed drag settles on a snap point". Docs: CHANGELOG.md v1.4.0 "Fix nested swipe cancel state".
- **DRW-PTR-18** — MUST. While a drawer's popup is mounted and its own `nestedOpenDrawerCount > 0`, its Viewport gesture is disabled (both pointer and touch paths), so only the frontmost drawer of a stack can be swiped; a nested Dialog does not disable it.
  Source: `drawer/viewport/DrawerViewport.tsx:119,311,729,924,971`. Test: [L4] `DrawerViewport.test.tsx` "does not mark nested drawers as swiping until movement passes the threshold" (child swipes, parent reflects it).

#### 6.2.6 Swipe to open (`Drawer.SwipeArea`)

- **DRW-PTR-25** — MUST. The SwipeArea configures its own swipe tracker with: tracked element = the SwipeArea; `enabled` per DRW-PTR-26; `directions = [opening direction]`; `trackDrag: false` (no drag transform on the area, the press position is kept so a single-move flick counts, PSH-PTR-13); default threshold/ignore options; movement variables `--drawer-swipe-movement-x/-y` (inert because `trackDrag` is false); callbacks per DRW-PTR-28…31.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:325-396`. Test: [L4] `DrawerSwipeArea.test.tsx` "opens on a quick flick whose only move is already released (buttons: 0)", "commits a released-move quick flick exactly once when a real pointerup trails it", "opens on a quick flick that lands its whole travel in a single touch move", "does not open on an in-place press-release without movement", "does not open a horizontal drawer on a stationary pointer move".
- **DRW-PTR-26** — MUST. The SwipeArea is *enabled* iff not `disabled` and (the drawer is closed or its own gesture is active); while not enabled its tracker ignores input and it has `pointer-events: none` (so the area does not cover an open drawer).
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:130,453`. Test: [L4] `DrawerSwipeArea.test.tsx` "does not open the drawer when disabled".
- **DRW-PTR-27** — MUST. Pointer events with `pointerType: 'touch'` are ignored by the SwipeArea (touch input arrives as touch events, passed straight to the tracker); for other pointer types `pointerdown` is passed to the tracker and then, if cancelable, default-prevented (suppressing text selection and native drag); `pointermove/up/cancel` are passed to the tracker.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:456-486`. Test: [L4] `DrawerSwipeArea.test.tsx` "prevents default pointer down for non-touch swipes", "does not prevent a non-cancelable pointer start", "ignores compatibility touch pointer gestures with real displacement", "opens the drawer when swiped with touch events".
- **DRW-PTR-28** — MUST. On gesture start the SwipeArea disables outside-press dismissal (DRW-DIS-02), remembers the start event and marks itself active. On each progress report it records the deltas (whatever the direction). When the report's intended direction is the opening direction and either the displacement along it is ≥ 1 px or this gesture has already requested an open, it requests an open with reason `swipe`, the *start* event and the SwipeArea element as `trigger` if the drawer is closed (at most once per gesture, even if a controlled owner rejects it), and then applies the movement of DRW-ALG-04. Reports in other directions neither open nor move the popup.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:28,302-305,334-367`. Test: [L4] `DrawerSwipeArea.test.tsx` "opens the drawer when swiped in the open direction", "does not open when the swipe direction never locks to the open direction", "respects custom swipeDirection", "does not apply or close swipe state when a controlled parent rejects opening", "re-enables outside press dismissal after an interrupted swipe-open gesture" (open before release).
- **DRW-PTR-29** — MUST. While the drawer is open and mounted, the popup position during a swipe-open follows DRW-ALG-04 (written imperatively to the Popup, Backdrop and Provider), and it is re-applied post-commit, pre-paint after every DOM update while the gesture is active, so no update (including the one that opens the drawer) ever paints the popup at rest. While the SwipeArea drives the popup, the Viewport's open-time tracker reset (DRW-ANI-03) is skipped.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:211-273,402-409`; `drawer/viewport/DrawerViewport.tsx:856-867`. Test: [L4] `DrawerSwipeArea.test.tsx` "synchronizes overshoot styles across the popup, backdrop, and provider indent", "opens from the rendered transform distance in either horizontal direction", "omits frontmost height when an opening drawer has no measured height", "keeps the swipe-area movement on the popup when re-grabbed during close" (`120px`). Docs: CHANGELOG.md v1.7.0 "Fix popup flashing fully open for a frame on swipe area re-grab".
- **DRW-PTR-30** — MUST. On release: `threshold` = Popup size along the dismiss axis × 0.5 (40 px when there is no Popup or its size is 0); *commit* iff the intended direction is the opening direction, the area is not disabled, and (displacement ≥ threshold or release velocity along the opening direction ≥ 0.1 px/ms). On commit an open is requested (reason `swipe`, release event, SwipeArea as trigger) if the drawer is not already open; otherwise, if this gesture opened the drawer and it is still open, a close is requested with reason `swipe`. The tracker's own dismissal is always suppressed (the override returns `false`). Then DRW-PTR-31 cleanup runs.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:29-30,202-209,368-394`. Test: [L4] `DrawerSwipeArea.test.tsx` "uses a size-based swipe threshold by default" (height 200: 70 px stays closed, 120 px opens), "commits a released-move quick flick exactly once when a real pointerup trails it". Docs: CHANGELOG.md v1.7.0 "Fix unreliable swipe-to-open gestures".
- **DRW-PTR-31** — MUST. Gesture finish (release or tracker cancel, e.g. a non-primary button during the drag) resets the gesture state, arms the outside-press re-enable guard (DRW-DIS-02), zeroes the deltas and clears the swipe styles: removes the Popup's movement variables and `data-swiping`, restores its saved inline `transition`, removes the Backdrop's `data-swiping`, sets its `--drawer-swipe-progress` to `0` and removes `--drawer-height`, and sets the Provider visual state to `{0, 0}`. When the area becomes not enabled during a gesture (e.g. `disabled` turns `true`), the same cleanup runs post-commit, pre-paint (before after-paint work) and the tracker is reset.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:275-323,395,411-428`. Test: [L4] `DrawerSwipeArea.test.tsx` "cancels an active opening gesture when the swipe area becomes disabled", "re-enables outside press dismissal after an interrupted swipe-open gesture", "re-enables outside press dismissal after a context menu interrupts swipe-open", "synchronizes overshoot styles across the popup, backdrop, and provider indent" (post-release state).
- **DRW-PTR-32** — MUST. The SwipeArea's element is registered as a trigger of the Root (DRW-API-10), so a press on it is never an outside press (DLG-PTR-13) and a swipe-open associates the drawer with it (DRW-ARIA-02).
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:110-119,446`. Test: [L4] `DrawerSwipeArea.test.tsx` "follows a swipe area id change".

### 6.3 Focus management

Focus trapping, return focus, the focus-out rule and `finalFocus` are Dialog's (DLG-FOC-02…18).

- **DRW-FOC-01** — MUST. With `initialFocus` unset, opening a drawer (by any interaction type, including mouse and keyboard) focuses the Popup element itself, not its first tabbable descendant (subject to DLG-FOC-02/03 timing and skip rules).
  Source: `drawer/popup/DrawerPopup.tsx:306,402`; `floating-ui-react/components/FloatingFocusManager.tsx:718-722`. Test: [L4] `DrawerPopup.test.tsx` "defaults initial focus to the popup element".
- **DRW-FOC-02** — MUST. `initialFocus={true}` (or a function returning `true`/`null`) focuses the first tabbable element inside the Popup, falling back to the Popup, for every interaction type (Drawer has no touch special case); `initialFocus={false}` leaves focus where it is.
  Source: `drawer/popup/DrawerPopup.tsx:306`; `floating-ui-react/components/FloatingFocusManager.tsx:699-722`. Test: [L4] `DrawerPopup.test.tsx` "leaves focus on the trigger when initial focus is disabled".

#### 6.3.1 Virtual keyboard provider

Terms: *keyboard input* = a contenteditable element (resolved to its editing host), a `<textarea>`, or an `<input>` of type `email`, `number`, `password`, `search`, `tel`, `text` or `url`, not matching `:disabled`; a `<label>` resolves to its `control` when that is a keyboard input. *Root* = the registered Viewport element. *Keyboard viewport* = the result of DRW-ALG-06.

- **DRW-FOC-03** — MUST. The provider is active only while the drawer is open and mounted and a Viewport element is registered; while active it listens (capture) on the document for `focus`, `focusin`, `focusout`, `pointerdown`, on the window for `scroll`, and — when `visualViewport` exists — on `visualViewport` `resize`/`scroll` and window `resize`; if a keyboard input inside the Root is already focused at activation, it is captured and an alignment scheduled. Without `visualViewport`, or while pinch-zoomed (`scale ≠ 1`), no keyboard is ever detected and the inset stays `0px`.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:176-207,527-569`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "captures an initially focused input without attempting an unnecessary page restore", "does not align inputs when visualViewport is unavailable", "does not add keyboard scroll slack while pinch-zoomed", "does not add keyboard scroll slack by default" (no provider).
- **DRW-FOC-04** — MUST. Keyboard detection follows DRW-ALG-06 (threshold 60 px, remembered keyboard visual height, `100svh` probe for layouts that already follow the keyboard).
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:24,203-239`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "does not treat a small visual viewport reduction as the software keyboard", "computes the keyboard inset from a non-zero visual viewport offset", describe "layout viewport resized for the keyboard (Chrome on iOS)" (7 tests).
- **DRW-FOC-05** — MUST. *Alignment* (DRW-ALG-07) sets `--drawer-keyboard-inset` on the Root to `max(0, ceil(innerHeight − keyboard.bottom))` px (0 when no keyboard, no tracked target, the target left the Root, or a nested dialog/drawer is open — "nested" here is `nestedOpenDialogCount > 0`), adds bottom *scroll slack* to the nearest vertical scroll ancestor of the field equal to its overlap with the keyboard, and scrolls that ancestor so the field is centered in the visible band (keyboard-clipped container minus a 16 px margin at each edge), using `behavior: 'smooth'` unless `prefers-reduced-motion: reduce` matches (`'auto'`).
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:25,162-169,301-403`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "compensates for keyboard overlap without adding extra spacing and centers the focused input", "sets the keyboard inset CSS variable for a focused input without a scroll target", "captures focusin targets through a shadow root", "adds scroll slack to a light-DOM scroller for an input inside a shadow root", "adds keyboard scroll slack to a potential scroll ancestor for textareas", "adds keyboard scroll slack to the scroll container, not an overflowing textarea", "centers a field focused without a tap inside the scrollable body", "clears inset and slack when the focused field is removed".
- **DRW-FOC-06** — MUST. Scroll slack: `slack = max(0, ceil(overlap))`; 0 restores any adjustment. Before the first adjustment of an element its inline `overflow-anchor`, `padding-bottom`, `scroll-padding-bottom` and computed padding/scroll-padding are snapshotted; it then gets `overflow-anchor: none`, `padding-bottom: slack + max(computedPaddingBottom, 16)` px and `scroll-padding-bottom: computedScrollPaddingBottom + 16` px. Adjusting a different element, or finding the adjusted element disconnected, first restores the previous one to its snapshotted inline values; restoration also happens on release, close and unmount.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:108-160`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "compensates for keyboard overlap…" (`240px` then `20px`), "tops the scroll container padding up to the visibility margin below the overlap" (`236px`), "restores the previous scroll slack when focus moves to a different scroll container", "restores slack from a removed scroll container before adjusting a new one", "restores keyboard scroll slack when the drawer closes". Docs: CHANGELOG.md v1.9.0 "Remove excess virtual keyboard scroll space".
- **DRW-FOC-07** — MUST. Focus events: `focusin` clears the programmatic-focus suppression, restores any preempted geometry, and either captures the new target (keyboard input inside the Root, not while a nested dialog is open) — scheduling an alignment next frame and, when a keyboard is detected, four delayed realign passes 150 ms apart — or releases the tracked target. `focusout` (unless suppressed by a programmatic tap focus) whose `relatedTarget` is a capturable keyboard input captures it, *preempts* its reveal when a keyboard is detected (presents it centered in the visible band by an inline `transition: none; opacity: 0; transform: translateY(…)` override that is restored on the following `focusin` or alignment), and schedules an alignment; otherwise it releases the target. Releasing clears the target, forgets the remembered keyboard height, cancels pending realign passes and schedules an alignment on the next frame (so the inset and slack are dropped one frame later, keeping a tapped item under the finger). A `focus` event on a preempted target re-focuses it with `preventScroll: true`. Visual/window viewport events schedule an alignment when a target is tracked or the active element is capturable.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:279-299,405-525`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "keeps the keyboard inset when focus moves directly to another keyboard input", "releases keyboard inset and scroll slack after focus leaves the field (pointerdown: %s)" (×2), "activates an item on the first tap while the keyboard is open", "overrides the incoming field geometry when focus moves without a tap while the keyboard is open", "does not blur and re-focus for focus changes while the keyboard is closed", "clears keyboard state when a tapped input redirects focus away", "clears keyboard state when a tapped input blurs itself". Docs: CHANGELOG.md v1.9.0 "Fix native focus scrolling on iOS 27"; v1.7.0 "Fix scroll handling when focus moves while the virtual keyboard is open".
- **DRW-FOC-08** — MUST. Alignment scheduling: at most one pending frame callback; an alignment whose computed destination differs (> 1 px) from the previous one, or that sees the layout viewport still resizing, re-checks on the next frame instead of scrolling, up to 60 consecutive checks, after which it scrolls anyway; once a scroll toward a destination is issued it is not re-issued while it has arrived (within 1 px) or is still progressing, and is re-issued when a later pass finds it stalled. A capture-phase `pointerdown` anywhere cancels the pending frame (when a target is tracked) and all delayed passes and forgets the scroll bookkeeping.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:30-34,359-403,546-557`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "defers the alignment scroll until the destination stops moving" (280, never 330), "eventually aligns when geometry never settles" (1 call after 65 frames), "does not restart a progressing or settled reduced-motion alignment scroll", "re-issues the alignment scroll when a delayed realign pass finds it stalled", "stops pending realignment once the user starts scrolling while geometry is changing", "realigns a tapped field in the scrollable body when the keyboard is already open for a field outside it".
- **DRW-FOC-09** — MUST. For `modal === true` only, while a keyboard input is tracked, no nested dialog is open and a keyboard is detected, any window scroll away from the position recorded at activation is undone immediately with `window.scrollTo({ left, top, behavior: 'instant' })` and an alignment is scheduled; non-modal and `'trap-focus'` drawers never restore the page scroll.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:248-272,319,538-544`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "restores the page scroll position when a modal drawer page scrolls while the keyboard is open", "does not restore the page scroll position for a non-modal drawer".
- **DRW-FOC-10** — MUST. *Tap to focus*: the Viewport forwards `touchstart`, every document `touchmove` (even moves the swipe claims), `touchend` and `touchcancel` to the provider. A `touchstart` records the point; a move > 10 px on either axis marks the gesture as a drag; `touchcancel` clears it. On `touchend`, when the drawer is open and mounted, no nested dialog is open, a start point exists and the gesture was not a drag, the lift target is resolved by DRW-ALG-08. Nothing is done (native tap preserved) when: resolution is *blocked*; the focus or click target is outside the Root; the page is pinch-zoomed; or the target is already focused while a keyboard is detected (or no `visualViewport` exists). Otherwise the `touchend` is default-prevented, the field is focused with `preventScroll: true` while its geometry is overridden off-screen (`opacity: 0; transform: translateY(-2000px); transition: none`, restored synchronously; an already-focused field is first blurred), a replacement `click` (`PointerEvent` when available, else `MouseEvent`; `bubbles`, `cancelable`, `detail: 1`, tap coordinates) is dispatched on the tapped element, and — when the tapped element is a label and its control still has focus — the control is focused again with `preventScroll: true`. The swipe gesture is unaffected (`data-base-ui-swipe-ignore` fields still focus).
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:35,595-719,846-895`; `drawer/viewport/DrawerViewport.tsx:790,999,1043,1048`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "focuses an unfocused keyboard input on touchend without page scroll", "preserves native taps on picker inputs", "preserves native taps that lift over a neighboring interactive element", "resolves the lift point through the shadow root when the drawer renders in one", "preserves native taps on disabled inputs", "…disabled textareas", "…a disabled labelled control", "focuses a keyboard input with data-base-ui-swipe-ignore without starting drawer swipe", "does not focus a keyboard input on touchend without touchstart", "does not focus a keyboard input after touchcancel", "does not focus a keyboard input while a nested drawer is open", "preserves native taps on an already-focused keyboard input", "refocuses an already-focused keyboard input when the keyboard viewport is closed", "preserves preventScroll after activating an %s label with no input focused" (×2), "preserves a focus change from a label click handler", "focuses a contentEditable element on touchend", "focuses the editing host when tapping a nested element inside a contentEditable", "probes nearby points to resolve a keyboard input retargeted on touchend", "does not focus a keyboard input when the touch moves past the tap threshold", "does not steal a tap that lands on an interactive element near a keyboard input", "preserves native taps on keyboard inputs while pinch-zoomed", "ignores a directly captured touch target while a closed drawer remains mounted", "does not focus a lift-point target outside the drawer", "falls back to the touch target when point lookup misses", "uses the remaining touch and MouseEvent fallback for keyboard taps", "ignores a keyboard tap whose native target is not an element", "ignores touchend without a lift coordinate", "preserves native taps while the keyboard is open and refocuses once it closes". Docs: CHANGELOG.md v1.9.0 "Preserve item taps when dismissing the virtual keyboard".
- **DRW-FOC-11** — MUST. When the drawer closes (or the provider unmounts or its Root changes), the tracked target is cleared, scroll slack restored, pending frame and realign timers cancelled, any preempted geometry restored, the `100svh` probe element removed, all listeners removed and `--drawer-keyboard-inset` removed from the Root.
  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:176-187,571-581`. Test: [L3] `DrawerVirtualKeyboardProvider.test.tsx` "restores keyboard scroll slack when the drawer closes".

### 6.4 Dismissal

Escape, outside press, focus-out, Close, trigger toggle and imperative closes are Dialog's (DLG-DIS-01…09, DLG-PTR-05…19). Drawer adds:

- **DRW-DIS-01** — MUST. On Android (platform detection `os.android`), while the drawer is open and topmost (`nestedOpenDialogCount === 0`) and the window provides `CloseWatcher`, a `CloseWatcher` is created after paint and destroyed when any condition ends or the Root unmounts. Its `cancel` event, while the drawer is open, requests a close with reason `close-watcher` and that event; when the event is not cancelable, `cancel()` on the details is a no-op (EVD-EVT-10) so the close always proceeds; when the request was canceled, `preventDefault()` is called on the event so the same watcher stays active (EVD-EVT-12). On other platforms no watcher is created.
  Source: `drawer/root/DrawerRoot.tsx:445-493`. Test: [L4] `DrawerRoot.test.tsx` "CloseWatcher" (5 tests, ×6 cases). Docs: CHANGELOG.md v1.9.0 "Handle CloseWatcher close requests from the cancel event".
- **DRW-DIS-02** — MUST. From the start of a SwipeArea gesture, outside-press dismissal of the Root is disabled (the outside-press predicate returns `false`). At gesture finish a guard is armed on the document (capture phase) that re-enables it on the next `pointerdown`, or on the next `click` that is press-less (`detail === 0`) or virtual — a physical `click` without its own `pointerdown` (the gesture's release click) is ignored and the guard keeps waiting. Because the guard runs in the capture phase, the re-enabling `pointerdown`/`click` itself can dismiss. Unmounting the SwipeArea removes the guard and re-enables dismissal.
  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:132-169,430-435`; `dialog/root/useDialogRoot.ts:43-45`. Test: [L4] `DrawerSwipeArea.test.tsx` "re-enables outside press dismissal after opening by swipe", "re-enables outside press dismissal for a touch outside press after opening by swipe", "does not dismiss from the $pointerType/$input click synthesized by the swipe-open release" (×3), "suppresses a trusted release click that arrives without a preceding pointerdown", "allows a later click-only activation to dismiss after a swipe", "allows a later pointerless virtual click to dismiss when the release has no click", "keeps the drawer open when the release click follows a mid-gesture disable", "cancels an active opening gesture when the swipe area becomes disabled", "opens on a quick flick whose only move is already released (buttons: 0)".
- **DRW-DIS-03** — MUST. Swipe dismissal is independent of `disablePointerDismissal` and of `modal`: a drawer with `disablePointerDismissal` can still be swiped closed (reason `swipe`), and a cancel in `onOpenChange` (DRW-PTR-14) is the only way to veto it.
  Source: `drawer/viewport/DrawerViewport.tsx:639-698` (no `disablePointerDismissal` check). Test: none.
- **DRW-DIS-04** — MUST. In a drawer stack only the frontmost drawer can be swiped (DRW-PTR-18); Escape and outside presses follow DLG-DIS-03 (topmost dialog-family Root, counting nested Dialogs too).
  Source: `drawer/viewport/DrawerViewport.tsx:311`; `dialog/root/useDialogRoot.ts:28,67,84`. Test: [L4] `DrawerViewport.test.tsx` "does not mark nested drawers as swiping until movement passes the threshold".
- **DRW-DIS-05** — MUST. Closing a drawer that has snap points by any path (Escape, outside press, Close, swipe, imperative, `close-watcher`) resets the snap point per DRW-STA-04, and a swipe-to-close from a snap point first requests the `null` snap point (DRW-ALG-01), which can veto the dismissal.
  Source: `drawer/root/DrawerRoot.tsx:150-169`; `drawer/viewport/DrawerViewport.tsx:556-571`. Test: [L4] `DrawerViewport.test.tsx` "does not start swipe dismissal when closing the snap point is canceled"; `DrawerRoot.test.tsx` "resets the active snap point when closing".

### 6.5 Gesture and keyboard algorithms

Notation: `disp(d, v)` as §6.2; `ph` = measured popup height (DRW-STA-10); `activeOffset` per DRW-STA-07; `closest(list, t)` = DRW-STA-09.

- **DRW-ALG-01** — MUST. The implementation reproduces the following snap-point release decision exactly (vertical drawer, non-empty resolved list, `ph > 0`; `s = +1` for `down`, `−1` for `up`; `direction` is the tracker's intended direction, possibly undefined). Server: relaxed, see §9.

```
dragDelta ← s·Δy;  dragDir ← sign(dragDelta)
v ← s·releaseVelocityY
if dragDir ≠ 0 and |dragDelta| ≥ 10 and sign(v) ≠ 0 and sign(v) ≠ dragDir: v ← s·velocityY   // ignore touch reversals
current ← activeOffset ?? 0
dragTarget ← clamp(current + dragDelta, 0, ph)
velOffset ← |v| ≥ 0.5 ? clamp(v, −4, 4) × 300 : 0
target ← snapToSequentialPoints ? dragTarget : clamp(dragTarget + velOffset, 0, ph)
details ← { reason: 'swipe', event }

settleInPlace(): publish progress 0 (DRW-PTR-16, notifying parent); clear swipe release; return false
settle(p): request snap point p.value with details (DRW-STA-03); return settleInPlace()
close(fallback):
  if direction undefined: return settle(fallback)            // unattributed gestures never dismiss
  request snap point null with details
  if details.isCanceled: return settleInPlace()                // no exit styles appear
  pending ← active snap point before the gesture
  start swipe release in swipeDirection (DRW-PTR-12); return true   // tracker then dismisses → DRW-PTR-14

if snapToSequentialPoints:                                     // branch S
  ordered ← resolved sorted by offset ascending
  cur ← closest(ordered.offsets, current);  t ← ordered[closest(ordered.offsets, target)]
  eff ← target
  if dragDir ≠ 0 and sign(v) = dragDir and |v| ≥ 0.5:
    adj ← clamp(cur + dragDir, 0, n − 1)
    if adj ≠ cur:
      if (dragDir > 0 and target < ordered[adj].offset) or (dragDir < 0 and target > ordered[adj].offset):
        t ← ordered[adj]; eff ← ordered[adj].offset
    else if dragDir > 0: return close(t)                       // flick past the last point
  if |eff − ph| < |eff − t.offset|: return close(t)
  return settle(t)
else:
  c ← resolved[closest(resolved.offsets, target)]              // input order, first on ties
  if v ≥ 0.5 and dragDelta > 0: return close(c)
  if |target − ph| < |target − c.offset|: return close(c)
  return settle(c)
```

  Source: `drawer/viewport/DrawerViewport.tsx:517-637`. Test: [L3] as DRW-PTR-11.
- **DRW-ALG-02** — MUST. The implementation reproduces the following swipe-strength computation exactly (`d` = dismissing direction; `none` → `--drawer-swipe-strength: 1`). Server: relaxed, see §9.

```
size ← size of the Popup along d;  if size ≤ 0: none
base ← (d ∈ {up, down} and snapPoints non-empty) ? (activeOffset ?? 0) : 0
remaining ← max(0, size − (base + disp(d, Δ)));  if remaining ≤ 0: none
rv ← disp(d, releaseVelocity);  v ← |rv| > 0 ? rv : disp(d, velocity)
if v ≤ 0.2: none
v ← clamp(v, 0.2, 4)
duration ← clamp(remaining / v, 80, 360)                      // ms
strength ← 0.1 + (duration − 80) / 280 × 0.9                  // ∈ [0.1, 1]
```

  Source: `drawer/viewport/DrawerViewport.tsx:50-55,236-293`. Test: none.
- **DRW-ALG-03** — MUST. The implementation reproduces the following touch arbitration exactly (run per document `touchmove` before scroll handling; deltas measured from the touch start; *drawer axis* = vertical for up/down, horizontal for left/right). Server: relaxed, see §9.

```
yield(move):                       // true = leave the move to the browser / cross-axis target
  if preserveNativeCrossAxisScroll: return true
  if drawerAxisAttributed or allowSwipe = true or not hasCrossAxisGestureTarget: return false
  if not move.cancelable: preserveNativeCrossAxisScroll ← true; return true
  a ← |drawer-axis delta|;  c ← |cross-axis delta|
  if c ≥ 6 and c > a + 2: preserveNativeCrossAxisScroll ← true; return true
  if a ≥ 6: drawerAxisAttributed ← true; return false
  return true                      // not attributable yet: do not preventDefault

dismissFromStart(dir, axis) ← axis vertical ? dir = down : dir = right
atStartEdge(scroller) ← dismissFromStart ? offset ≤ 0 : offset ≥ max(0, scrollSize − clientSize)
canSwipeFromEdgeOnMove(scroller, delta) ← (dismissFromStart ? delta > 0 : delta < 0) and atStartEdge(scroller)
```

  Source: `drawer/viewport/DrawerViewport.tsx:1172-1270`. Test: [L3] as DRW-PTR-21/22.
- **DRW-ALG-04** — MUST. The implementation reproduces the following SwipeArea open movement exactly (`open` = opening direction, `dismiss` = its opposite). Server: relaxed, see §9.

```
require Popup element, store open and mounted
closed ← (once per gesture) size of Popup along dismiss axis; if ≤ 0: abort
         t ← computed transform translation on that axis; if finite and |t| > 0.5: closed ← min(closed, |t|)
d ← max(0, disp(open, Δ))
damped ← d > closed ? closed + √(d − closed) : d
movement ← (closed − damped) × (dismiss ∈ {left, up} ? −1 : +1)        // on dismiss axis; other axis 0
openProgress ← clamp(d / closed, 0, 1)
Popup:    --drawer-swipe-movement-x/y ← movement px / 0px; data-swiping; save inline transition once; transition ← none
Backdrop: data-swiping; --drawer-swipe-progress ← 1 − openProgress; --drawer-height per DRW-DOM-21 (openProgress > 0)
Provider: { swipeProgress: openProgress, frontmostHeight: openProgress > 0 ? frontmostHeight : 0 }
```

  Source: `drawer/swipe-area/DrawerSwipeArea.tsx:171-273`; `utils/getElementTransform.ts` (UTL-DOM-24). Test: [L3] `DrawerSwipeArea.test.tsx` "synchronizes overshoot styles across the popup, backdrop, and provider indent" (`−√150`, indent progress `1` at overshoot, `0` back at start), "opens from the rendered transform distance in either horizontal direction" (`−20`), "keeps the swipe-area movement on the popup when re-grabbed during close" (`120px`).
- **DRW-ALG-05** — MUST. While swiping with a snap range (DRW-STA-17) and `ph > 0`, the published progress is `clamp((clamp(base + Δy, 0, ph) − minOffset) / range, 0, 1)` with `base = activeOffset ?? minOffset` and the raw `Δy` (not direction-adjusted); when not swiping it is the resting snap progress when defined, else the tracker progress. Server: relaxed, see §9.
  Source: `drawer/viewport/DrawerViewport.tsx:410-424`. Test: [L3] `DrawerViewport.test.tsx` "keeps the resting snap point progress when a press never starts a swipe".
- **DRW-ALG-06** — MUST. The implementation reproduces the following keyboard-viewport detection exactly (`remembered` starts at −1 per activation and is reset to −1 on release).

```
if no visualViewport or visualViewport.scale ≠ 1: none
L ← innerHeight;  V ← visualViewport.height
if L − V > 60: remembered ← V
else if |V − remembered| > 60: remembered ← −1; none
probe ← (created once per activation) div{position:fixed; top:0; height:100svh; visibility:hidden} appended to body
follows ← probe.offsetHeight − V ≤ 60
top ← max(0, visualViewport.offsetTop)
return { top, bottom: follows ? L : min(L, top + V), resizing: follows and |L − V| ≥ 1 }
```

  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:203-237`. Test: [L3] as DRW-FOC-04. Docs: CHANGELOG.md v1.9.0 "Avoid excess keyboard spacing in Chrome on iOS".
- **DRW-ALG-07** — MUST. The implementation reproduces the following alignment exactly.

```
restore preempted geometry
if nested dialog open or no tracked target or target ∉ Root: inset ← 0; restore slack; stop
restore window scroll (DRW-FOC-09)
kv ← keyboard viewport;  if none: inset ← 0; restore slack; stop
inset ← innerHeight − kv.bottom                                         // DRW-DOM-23
sc ← nearest vertically scrollable ancestor from parent(target) (crossing shadow roots) within Root,
     else nearest with overflow intent (UTL-DOM-28/29);  if none: restore slack; stop
r ← sc rect;  slack(sc, max(0, r.bottom − kv.bottom))                  // DRW-FOC-06
max ← max(0, sc.scrollHeight − sc.clientHeight);  if max ≤ 0: stop
vt ← max(r.top, kv.top) + 16;  vb ← min(r.bottom, kv.bottom) − 16;  if vb ≤ vt: stop
dest ← round(clamp(sc.scrollTop + (t.top + t.bottom − vt − vb) / 2, 0, max))
settled ← sc = lastSc and not kv.resizing and |lastDest − dest| ≤ 1
if not settled:
  checks ← (sc = lastSc) ? checks + 1 : 1; lastSc ← sc; lastDest ← dest; observed ← −1
  if checks ≤ 60: request next frame → alignment; stop
else if observed ≥ 0:
  if |sc.scrollTop − dest| ≤ 1: stop
  if sc.scrollTop ≠ observed: observed ← sc.scrollTop; stop
lastSc ← sc; lastDest ← dest; checks ← 0; observed ← sc.scrollTop
sc.scrollTo({ top: dest, behavior: reducedMotion ? 'auto' : 'smooth' })
```

  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:301-403,897-909`. Test: [L3] as DRW-FOC-05/08.
- **DRW-ALG-08** — MUST. The implementation reproduces the following tap-target resolution exactly (`(x, y)` = `changedTouches[0] ?? touches[0]` of the `touchend`; hit testing in the Root's root node, UTL-DOM-27).

```
exact ← elementFromPoint(x, y)
if exact is an HTML element and resolveKeyboardInput(exact) = k: return { focus: k, click: exact }
if exact is interactive or exact is inside a <label>: return BLOCKED
for (dx, dy) in [(0, 16), (0, −16), (16, 0), (−16, 0)]:
  k ← resolveKeyboardInput(elementFromPoint(x + dx, y + dy)); if k: return { focus: k, click: k }
target ← composed target of the touchend; k ← resolveKeyboardInput(target)
return k ? { focus: k, click: target } : none
```

  Source: `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:35-45,632-657,739-844`. Test: [L3] as DRW-FOC-10.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onOpenChange` | `(open, details: Drawer.Root.ChangeEventDetails) => void` | Every open/close request (DLG-EVT-01), synchronously, before any state change | DRW-EVT-01 | yes |
| `onOpenChangeComplete` | `(open: boolean) => void` | As DLG-EVT-08 (Popup animations) | — | no |
| `onSnapPointChange` | `(snapPoint, details: Drawer.Root.SnapPointChangeEventDetails) => void` | Every snap point change request | DRW-EVT-03 | yes |

- **DRW-EVT-01** — MUST. The reasons `onOpenChange` receives, and the action producing each, are exactly:

  | Reason | `open` | Produced by |
  |---|---|---|
  | `trigger-press` | `true`/`false` | Trigger click (DLG-PTR-01…03) |
  | `outside-press` | `false` | Qualifying outside press (DLG-PTR-05…17), not during a SwipeArea gesture (DRW-DIS-02) |
  | `escape-key` | `false` | <kbd>Escape</kbd> (DLG-KEY-01) |
  | `close-watcher` | `false` | Android `CloseWatcher` `cancel` (DRW-DIS-01) |
  | `close-press` | `false` | Close part (DLG-PTR-19) |
  | `focus-out` | `false` | `modal: false` focus leaving (DLG-FOC-14, DLG-KEY-08) |
  | `imperative-action` | `true`/`false` | Handle `open`/`openWithPayload`/`close`, `actionsRef.close()` |
  | `swipe` | `false` | Viewport swipe dismissal (DRW-PTR-14); SwipeArea close after a non-committed opening gesture (DRW-PTR-30) |
  | `swipe` | `true` | SwipeArea opening (DRW-PTR-28, DRW-PTR-30) |
  | `none` | — | Declared; never emitted for open changes |

  Source: `drawer/root/DrawerRoot.tsx:356-365,465-468`; `drawer/viewport/DrawerViewport.tsx:648-652`; `drawer/swipe-area/DrawerSwipeArea.tsx:302-309`; plus the DLG-EVT-02 sources. Test: [L4] `DrawerViewport.test.tsx` "dismisses with a touch swipe down that starts on an SVG inside a top-edge scroll container" (`swipe`); `DrawerRoot.test.tsx` "closes when the watcher requests a close" (`close-watcher`); `DrawerSwipeArea.test.tsx` "does not apply or close swipe state when a controlled parent rejects opening" (`true`).
- **DRW-EVT-02** — MUST. For `swipe` requests: from the Viewport, `details.event` is the native release event (`PointerEvent` or `TouchEvent`, the final `pointermove` with `buttons: 0` when that ended the gesture) and no trigger is supplied (the active trigger is back-filled, DLG-STA-15); from the SwipeArea, the open request carries the gesture's *start* event when issued during the drag and the release event when issued at release, and `details.trigger` is the SwipeArea element.
  Source: `drawer/viewport/DrawerViewport.tsx:648-652`; `drawer/swipe-area/DrawerSwipeArea.tsx:302-309,363,385,388`. Test: [L1] `DrawerRoot.spec.tsx` (type-level `PointerEvent | TouchEvent`). (L4 for the runtime event and trigger clauses)
- **DRW-EVT-03** — MUST. `onSnapPointChange` receives: reason `swipe` with the release event when a release settles on a snap point, requests `null` before a swipe dismissal, or restores the pending snap point after a canceled/rejected dismissal (DRW-ALG-01, DRW-PTR-14/15); the close's reason, event and trigger when the drawer closes with snap points (DRW-STA-04); reason `none` for an internal request without details. It is called even when the value does not change, and a canceled request leaves the value unchanged.
  Source: `drawer/root/DrawerRoot.tsx:79-94,158-167`; `drawer/viewport/DrawerViewport.tsx:541,563,657,680-683`. Test: [L4] `DrawerRoot.test.tsx` "provides event details when snap point changes", "honors canceled snap point changes and falls back from invalid uncontrolled values"; `DrawerViewport.test.tsx` "expands a collapsed sheet when the first move from a $target leaves the popup" (`'200px'`, `swipe`), "does not start swipe dismissal when closing the snap point is canceled" (`null`).
- **DRW-EVT-04** — MUST. For a swipe dismissal the order is: tracker release → (snap points) `onSnapPointChange(null)` → swipe release attributes (DRW-PTR-12) → `onOpenChange(false, swipe)` → (snap points, not canceled) `onSnapPointChange(default, swipe)` (DRW-STA-04) → store commit → exit transition → `onOpenChangeComplete(false)`.
  Source: `drawer/viewport/DrawerViewport.tsx:556-571,639-698`; `drawer/root/DrawerRoot.tsx:150-169`. Test: [L4] `DrawerRoot.test.tsx` "does not restore snap point when a controlled swipe close is accepted by the parent" (ends at `'100px'`).

## 8. Form integration

Not applicable — Drawer has no form value or Field participation; VirtualKeyboardProvider only moves focus and scroll for fields placed inside the drawer.

## 9. Lifecycle, mounting and animation

Mounting, transition statuses, exit detection on the Popup element, `keepMounted`, `preventUnmountOnClose`/`actionsRef.unmount()` and `onOpenChangeComplete` are DLG-ANI-01…10 (the Drawer Popup registers the same `onOpenChangeComplete(true)` completion on its element). Timing terms per conventions §4.

- **DRW-ANI-01** — MUST. Exit completion is detected on the Popup element only; `data-ending-style` set imperatively by a swipe release (DRW-PTR-12) is the consumer's hook to start the exit transition before the close commits, and it is replaced by the store-driven `data-ending-style` once the close commits. Server: relaxed, see §9.
  Source: `drawer/popup/DrawerPopup.tsx:296-304`; `drawer/viewport/DrawerViewport.tsx:458`. Test: [L4] `DrawerRoot.test.tsx` "finishes a controlled accepted dismissal while kept mounted" (`data-closed` after one frame).
- **DRW-ANI-02** — MUST. A swipe dismissal that is canceled or rejected removes the imperatively set `data-ending-style` and `data-swipe-dismiss` and resets the movement variables to `0px`; a canceled `null` snap point request prevents them from ever being set.
  Source: `drawer/viewport/DrawerViewport.tsx:184-188,563-567,654-663,676-687`. Test: [L4] `DrawerRoot.test.tsx` "clears swipe-dismiss styles when swipe close is canceled", "restores snap point and swipe offsets when swipe close is canceled"; `DrawerViewport.test.tsx` "does not start swipe dismissal when closing the snap point is canceled".
- **DRW-ANI-03** — MUST. Post-commit, pre-paint whenever the drawer becomes open (including reopening a kept-mounted drawer), the Viewport resets the tracker (movement back to `0px`, unless a SwipeArea gesture is driving the popup) and clears the swipe release (`data-swipe-dismiss`, imperative `data-ending-style`, strength), before descendants' post-commit work.
  Source: `drawer/viewport/DrawerViewport.tsx:856-867`. Test: [L4] `DrawerViewport.test.tsx` "clears kept-mounted swipe state before descendant layout effects on reopen".
- **DRW-ANI-04** — MUST. Post-commit, pre-paint when a nested drawer closes (or its Viewport unmounts), it reports nested swipe progress 0 to its parent, so the parent's `--drawer-swipe-progress` is `0` before after-paint work.
  Source: `drawer/viewport/DrawerViewport.tsx:842-854`. Test: [L4] `DrawerViewport.test.tsx` "clears nested progress before descendant layout effects when the child closes".
- **DRW-ANI-05** — MUST. When the Viewport unmounts (including mid-swipe), post-commit pre-paint teardown sets the Provider visual state to `{0, 0}`, removes `data-swiping` from both the backdrop seen at setup and the current backdrop, and finishes any nested-swipe report; a Popup unmounting mid-gesture leaves no swiping state after the release and requests nothing.
  Source: `drawer/viewport/DrawerViewport.tsx:869-885,444-448`. Test: [L4] `DrawerViewport.test.tsx` "clears provider and backdrop swipe state during layout teardown", "clears the backdrop data-swiping attribute when the drawer unmounts mid-swipe", "cleans up when the popup unmounts during an active swipe".
- **DRW-ANI-06** — MUST. A nested drawer counts as *present* for its parent until its exit transition ends (DRW-STA-12), so the parent keeps `--drawer-height` (DRW-DOM-09) during the nested exit while `data-nested-drawer-open` and `--nested-drawers` already reflect the close; on reopening, `--drawer-height` is applied no later than `data-nested-drawer-open`.
  Source: `drawer/popup/DrawerPopup.tsx:283-294,321-325`. Test: [L4] `DrawerPopup.test.tsx` "keeps a fixed height applied while a nested drawer closes", "restores a fixed height before nested state when reopening a nested drawer", "clears parent nested drawer state when a nested drawer starts closing before unmount".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- DRW-PTR-21, DRW-ALG-01…05 — frame-rate loops (touch arbitration, snap release, strength, SwipeArea open movement, snap progress): met on both modes because the whole loop is in the `drawer` JavaScript module (constraint C1); listed so nobody moves it back to C#.
- DRW-STR-01 / DRW-ANI-01 (TRN-ANI-03, TRN-ANI-09, TRN-ANI-10, adopted through DLG-ANI) — unmount after the exit animation lands in the first render batch after the completion message, about two round trips later; JavaScript sets `hidden` at animation end (optimistic convergent write) so the popup is not visible meanwhile.
- DRW-STR-01 (TRN-STA-04, TRN-STA-05, TRN-STA-07, TRN-STA-09, adopted through DLG-ANI) — `data-starting-style`/`data-ending-style` flips: met on both modes (JS-owned, C# mirrors).
- DRW-STR-01 (EVD-EVT-11, EVD-DIS-01, FLT-KEY-01, adopted through DLG-KEY/DLG-DIS) — Escape `preventDefault()`/`stopPropagation()`: the consumer's `cancel()`/`allowPropagation()` decision arrives a round trip after dispatch; JavaScript prevents and stops whenever the drawer is the innermost open popup; WebAssembly meets the spec through `decide()`.
- DRW-STA-01 (RND-STA-01 for mirrored fields: `swiping`, `transitionStatus`) — the JS-written data attribute changes at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.

## 10. Positioning

Not applicable — like Dialog, Drawer is not anchored; placement is consumer CSS driven by the variables of §5 (the docs translate the popup by `--drawer-snap-point-offset + --drawer-swipe-movement-*`).

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Dialog Root, store, interactions | [dialog](dialog.md) | `useRenderDialogRoot('drawer', props)` with Drawer's `onOpenChange` wrapper (DRW-STA-04); `isDrawer` → nested drawer count; `outsidePressEnabledRef` toggled by SwipeArea |
| Swipe tracker (Viewport) | [popup-store-and-handles](../architecture/popup-store-and-handles.md) §4.7.5 | DRW-PTR-01 options; React touch-move not wired, native capture listener feeds `moveNative` |
| Swipe tracker (SwipeArea) | same | DRW-PTR-25 options (`trackDrag: false`) |
| Trigger registration | PSH §4.3.5 | SwipeArea `useTriggerRegistration(id, store)` (bare) |
| Focus manager | [floating-and-positioning](../architecture/floating-and-positioning.md) | As Dialog except `initialFocus: initialFocus ?? popupRef` |
| Open-change complete | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | Popup `useOpenChangeComplete({ open, ref: popupRef })` |
| Change details and reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | `swipe`, `close-watcher` (EVD-EVT-10/12); snap details via `createChangeEventDetails` |
| Controlled snap point | [shared-utilities](../architecture/shared-utilities.md) | `useControlled({ name: 'Drawer', state: 'snapPoint' })` |
| DOM helpers | [shared-utilities](../architecture/shared-utilities.md) | `getElementAtPoint` (UTL-DOM-27), `findScrollableTouchTarget` (UTL-DOM-29), `getElementTransform` (UTL-DOM-24), `contains`/`closest`/`getTarget` (UTL-DOM-20…22), `platform.os.android` |
| Rendering | [rendering-and-composition](../architecture/rendering-and-composition.md) | Popup mapping = popup + transition + `expanded`, `nestedDrawerOpen`, `nestedDrawerSwiping`, `swipeDirection`, `swiping`; Backdrop `popupTransitionStateMapping`; SwipeArea and Indent custom mappings |

## 12. Edge cases, invariants and dev warnings

- **DRW-EDGE-01** — SHOULD. Outside production, after paint when a `Drawer.Popup` is rendered without a `Drawer.Viewport` ancestor, it logs via `console.error` exactly: `Base UI: <Drawer.Popup> expected to be rendered within <Drawer.Viewport>. Omitting the viewport disables drawer swipe handling and touch scroll locking. Wrap <Drawer.Popup> in <Drawer.Viewport>.` followed by the owner component stack when the platform supplies one (nothing appended otherwise).
  Source: `drawer/popup/DrawerPopup.tsx:164-179`. Test: [L2] `DrawerPopup.test.tsx` "warns in development when not rendered within a viewport", "warns without relying on React owner-stack support". Docs: CHANGELOG.md v1.4.0 "Warn when a popup is missing `Viewport`".
- **DRW-EDGE-02** — MUST. Popup, Viewport, SwipeArea, and any internal consumer of the Drawer Root's shared state rendered outside a `Drawer.Root` throw `Base UI: DrawerRootContext is missing. Drawer parts must be placed within <Drawer.Root>.` (Popup, Viewport and SwipeArea first require the Dialog Root context and so throw DLG-EDGE-02 when no dialog-family Root exists at all.)
  Source: `drawer/root/DrawerRootContext.ts:102-114`. Test: [L2] `DrawerRoot.test.tsx` "throws a descriptive error when the root context is missing".
- **DRW-EDGE-03** — MUST. A Popup whose `render` produces no element does not throw (measurement and nested-progress synchronization tolerate a missing element).
  Source: `drawer/popup/DrawerPopup.tsx:181-186,224-227,245-249`. Test: [L2] `DrawerPopup.test.tsx` "does not throw when a popup render function produces no element".
- **DRW-EDGE-04** — MUST. With snap points but a popup of measured height 0, a release never dismisses and never changes the snap point; with only invalid snap points the regular threshold dismissal applies.
  Source: `drawer/viewport/DrawerViewport.tsx:507-515`. Test: [L4] `DrawerViewport.test.tsx` "does not resolve snap points before the popup has a measurable height", "falls back to regular dismissal when all snap points are invalid".
- **DRW-EDGE-05** — MUST. Hit testing for gesture targets and tap targets uses the root node of the Viewport (document or shadow root), so drawers portaled into a shadow root start gestures and resolve taps correctly; outside presses follow DLG-DIS-06.
  Source: `drawer/viewport/DrawerViewport.tsx:368,928-932,987-991`; `drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx:633`. Test: [L4] `DrawerViewport.test.tsx` "uses shadow-root hit testing for touch swipe targets", "starts a swipe inside a shadow root using real hit testing"; `DrawerVirtualKeyboardProvider.test.tsx` "resolves the lift point through the shadow root when the drawer renders in one". Docs: CHANGELOG.md v1.7.0 "Fix Shadow DOM swipe gestures".
- **DRW-EDGE-06** — MUST. Without `ResizeObserver` the popup and viewport heights are measured only at mount and on dependency changes; without `CSS.registerProperty` the variables are left unregistered (inheriting normally); without `CloseWatcher` no back-gesture handling exists.
  Source: `drawer/popup/DrawerPopup.tsx:47,232-234`; `drawer/root/useDrawerSnapPoints.ts:107`; `drawer/root/DrawerRoot.tsx:454-458`. Test: none.
- **DRW-EDGE-07** — MUST. Controlled-mode caveat: an `onOpenChange` that closes asynchronously makes a swipe dismissal revert after one frame (DRW-PTR-15); the later `open={false}` closes the drawer through the ordinary exit transition without swipe exit attributes.
  Source: `drawer/viewport/DrawerViewport.tsx:666-672` (comment). Test: none.

## 13. Behaviors evidenced by tests

Drawer runs `describeConformance` for Popup, Viewport, Content and SwipeArea (options below; RND-DOM-36…49) and no popup/dialog shared suite. Chromium-only tests are marked (C). `DrawerVirtualKeyboardProvider.test.tsx` is entirely Chromium-only.

### `root/DrawerRoot.test.tsx`

| Test | Requirements |
|---|---|
| "exposes the attributes rendered by borrowed trigger and close parts" | DRW-STR-01/02/03, DLG-DOM-03/04/24 |
| "uses a size-based swipe threshold" (C) | DRW-PTR-03, DRW-PTR-10, DRW-API-01 |
| "supports detached triggers with handles" / "supports imperative actions with handles" | DRW-STR-01, DRW-API-12, DLG-API-24…27, DLG-ARIA-02/03 |
| "does not re-render inactive detached triggers when the popup opens and closes" | DLG-EDGE-16 |
| "attaches fresh root state when a handle-backed root remounts" | DLG-EDGE-12, DLG-EDGE-04/05, DRW-API-12 |
| "synchronizes trigger aria-controls with the popup id" | DRW-DOM-16, DRW-ARIA-03 |
| "resets the active snap point when closing" / "resets to the default snap point when provided" | DRW-STA-04, DRW-API-04, DRW-STA-03 |
| "provides event details when snap point changes" | DRW-STA-04, DRW-EVT-03 |
| "does not reset snap point when a close is canceled" | DRW-STA-04 |
| "honors canceled snap point changes and falls back from invalid uncontrolled values" | DRW-STA-02, DRW-STA-03, DRW-EVT-03 |
| "normalizes invalid nested swipe progress without notifying for duplicate values" | DRW-STA-13 |
| "throws a descriptive error when the root context is missing" | DRW-EDGE-02 |
| "reports and unregisters $label before passive effects" (×3: controlled, default-open Strict Mode, React 17 id fallback) | DRW-STA-14 |
| "clears swipe-dismiss styles when swipe close is canceled" (C) | DRW-PTR-14, DRW-DOM-07, DRW-ANI-02 |
| "does not dismiss a controlled drawer via swipe when open is always true" (C) | DRW-PTR-15 |
| "does not restore snap point when a controlled swipe close is accepted by the parent" (C) | DRW-STA-04, DRW-PTR-14, DRW-EVT-04 |
| "restores snap point and swipe offsets when swipe close is canceled" (C) | DRW-PTR-14, DRW-ANI-02 |
| "damps active snap point overshoot after the swipe direction is established" (C) | DRW-DOM-12, DRW-STA-09 |
| "allows dragging past a snap point when snapToSequentialPoints is enabled" (C) | DRW-ALG-01 (S), DRW-API-06 |
| "advances on a short flick with a trailing stationary sample: %s" (C, ×2) / "stays on the current snap point after repeated stationary samples" (C) / "advances to the next snap point on fast flicks when snapToSequentialPoints is enabled" (C) | DRW-ALG-01 (S), PSH-ALG-10 |
| "keeps the drawer open on low-velocity swipes near a snap point" (C) / "keeps the drawer open when the release velocity reverses during an upward swipe" (C) | DRW-ALG-01 |
| "chooses the nearest sequential snap point on a slow drag" / "advances a sequential snap point toward dismissal" / "dismisses past the final sequential snap point" / "dismisses when a slow snap-point drag ends closer to closed" / "keeps the first sequential snap point on a fast opening flick" (C) | DRW-ALG-01 (S) |
| "dismisses from upward snap points using upward velocity" / "opens from a controlled null snap point" / "dismisses from a controlled null snap point" / "keeps the current snap point after a sub-threshold drag" / "dismisses a non-sequential snap point when closed is nearest" (C) | DRW-ALG-01, DRW-API-03 |
| "finishes a controlled accepted dismissal while kept mounted" (C) | DRW-PTR-14, DRW-ANI-01 |
| "restores a snap point when a controlled parent rejects dismissal" (C) | DRW-PTR-15, DRW-STA-04 |
| "closes an uncontrolled drawer after a slow long drag" (C) | DRW-PTR-10 |
| CloseWatcher: "closes when the watcher requests a close" / "keeps the same watcher active when a cancelable close request is canceled" / "closes on a close request that cannot be prevented even if onOpenChange cancels it" / "steps through content on back presses until one cannot be prevented (controlled: %s)" (×2) / "destroys the watcher on unmount" | DRW-DIS-01, DRW-EVT-01, EVD-EVT-10/12 |

### `root/DrawerSnapPoints.test.tsx` / `root/useDrawerSnapPoints.test.ts`

| Test | Requirements |
|---|---|
| "resolves supported units, filters invalid values, clamps heights, and keeps the last duplicate" | DRW-STA-05, -06, -07, -08, DRW-DOM-11 |
| "uses fractional and pixel snap points and resolves null or invalid active values as closed" | DRW-STA-05, -07, DRW-DOM-11 |
| "produces no resolved snap point when the list is empty" / "resolves a single snap point without a deduplication pass" | DRW-STA-06 |
| `getSnapPointSwipeMovement`: "returns the raw movement when the drag does not overshoot the open edge" / "returns the raw movement at the open edge (nextOffset === 0)" / "damps the movement with a square root once the drag overshoots the open edge" | DRW-STA-09, DRW-DOM-12 |
| `closestSnapPointIndex`: "returns the closest value and keeps the first value on ties" / "returns -1 when there are no values" | DRW-STA-09, DRW-ALG-01 (`closest`) |

### `root/DrawerRoot.spec.tsx` (type-level)

Swipe/Escape reason narrowing of `details.event` — §3.18, DRW-EVT-02.

### `popup/DrawerPopup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, rendered in `Root open > Portal > Viewport`) | RND-DOM-36…49, DRW-STR-05, DRW-STA-01 |
| "warns in development when not rendered within a viewport" / "warns without relying on React owner-stack support" | DRW-EDGE-01 |
| "defaults initial focus to the popup element" | DRW-FOC-01, DRW-API-07 |
| "leaves focus on the trigger when initial focus is disabled" | DRW-FOC-02 |
| "stops composite navigation keys from escaping the popup" | DRW-KEY-01, DLG-KEY-09 |
| "applies a negative snap point offset to upward drawers" | DRW-DOM-11, DRW-DOM-05 |
| "keeps the last measured popup height while nested content stretches it" / "ignores a queued measurement after the popup unmounts" | DRW-STA-10 |
| "does not throw when a popup render function produces no element" | DRW-EDGE-03 |
| "includes border size in frontmost height CSS variable for nested drawers" (C) | DRW-STA-11, DRW-DOM-10 |
| "reports nested presence and frontmost height before passive effects" | DRW-STA-11, -12, DRW-DOM-03, -08, -10 |
| "keeps the parent nested state when a closed handle-backed nested drawer mounts" | DRW-STA-18, DRW-DOM-03 |
| "does not treat dialogs inside nested drawers as nested drawers" / "does not treat alert dialogs inside nested drawers as nested drawers" | DRW-STA-18, DRW-STR-10, DRW-DOM-08 |
| "clears parent nested drawer state as soon as a nested drawer closes" / "clears parent nested drawer state when a nested drawer starts closing before unmount" (C) | DRW-DOM-03, DRW-ANI-06 |
| "keeps a fixed height applied while a nested drawer closes" (C) / "restores a fixed height before nested state when reopening a nested drawer" (C) | DRW-DOM-09, DRW-ANI-06 |

### `viewport/DrawerViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, in `Root open > Portal`) | RND-DOM-36…49, DRW-STR-04, DRW-API-13 |
| "registers the non-passive document touchmove listener only while open" | DRW-PTR-20 |
| "clears text selection on swipe start" / "preserves text selection outside the popup when a swipe starts" / "does not clear text selection on touch swipe start" / "clears a selection whose endpoints are popup elements" | DRW-PTR-08 |
| "starts touch swipes from interactive elements" | DRW-PTR-07, DRW-PTR-19, DRW-DOM-20 |
| "uses shadow-root hit testing for touch swipe targets" / "starts a swipe inside a shadow root using real hit testing" (C) | DRW-EDGE-05, DRW-PTR-19 |
| "clears the backdrop data-swiping attribute when the drawer unmounts mid-swipe" | DRW-ANI-05, DRW-DOM-20 |
| "uses the event target for non-keyboard touch scroll arbitration" | DRW-PTR-19 |
| "scrolls instead of swiping when a touch drag starts on an SVG inside scrolled content" / "dismisses with a touch swipe down that starts on an SVG inside a top-edge scroll container" | DRW-PTR-21, DRW-EVT-01 |
| "does not start a mouse drag swipe from an SVG inside scrollable content" | DRW-PTR-04, PSH-PTR-10 |
| "allows clicks on non-interactive elements without data-base-ui-swipe-ignore" | DRW-PTR-19 (no swipe on a tap) |
| "does not start touch swipes from elements with data-base-ui-swipe-ignore" | DRW-PTR-19 |
| data-base-ui-swipe-ignore axis values: "ignores %s touch swipes from elements with the drawer axis value "%s"" (×2) / "keeps an ancestor that ignores all directions in effect over a nested axis value" | DRW-PTR-19 |
| …"hands cross-axis drags in %s drawers to elements with the value "%s"" (×2) / "does not swipe or settle snap points while a %s cross-axis target handles the drag" (×2) / "still swipes %s drawers from elements with the cross-axis value "%s"" (×2) | DRW-PTR-19, DRW-PTR-22, DRW-ALG-03, DRW-PTR-06 |
| "does not prevent native touch scrolling in portaled descendants" / "allows touch gestures on a portaled combobox popup without starting drawer swipe" (C) | DRW-PTR-21, DRW-PTR-06 |
| "still allows touch swipes from elements with legacy data-swipe-ignore" | DRW-PTR-24 |
| "does not start non-touch swipes from Drawer.Content" / "treats pen interactions on Drawer.Content as non-touch swipes" | DRW-PTR-04, DRW-PTR-19 |
| "does not jump when touch starts outside the popup and then enters it" / "dismisses when touch starts outside the popup, then continues swiping down inside it" | DRW-PTR-06, DRW-PTR-14 |
| "does not mark nested drawers as swiping until movement passes the threshold" / "clears nested swiping when a nested drawer swipe is reversed before release" | DRW-PTR-17, DRW-DOM-04, DRW-DOM-14, DRW-DOM-22, DRW-PTR-18 |
| "prevents touchmove at scroll top when swiping down on scrollable content" / "prevents touchmove at scroll bottom when swiping up on scrollable content" / "prevents touchmove when a scrollable ancestor wraps the popup at the top" / "prevents touchmove when there is no scroll container" / "allows touchmove when scrolling down from scroll top" | DRW-PTR-21, DRW-ALG-03 |
| "starts a swipe away from the page scroll edge when the body is a scroll container" (C) | DRW-PTR-21 (page scroller excluded) |
| "does not block touchmove on native range inputs" / "does not block touchmove on slider thumb range inputs" | DRW-PTR-19 |
| "does not start swiping when adjusting input selection handles" / "does not start swiping when adjusting textarea selection handles" / "does not start swiping when adjusting contenteditable selection handles" / "does not start swiping when adjusting regular text selection handles" / "preserves an expanded touch selection whose focus endpoint is in the popup" | DRW-PTR-06, DRW-PTR-21 |
| "does not start an opposite-direction swipe from scroll bottom for down drawers with snap points" / "does not start an opposite-direction swipe from scroll right edge for right drawers" / "starts swipe-to-dismiss after a scrollable container reaches the dismiss edge" | DRW-PTR-21, DRW-ALG-03 |
| "keeps damped snap-point styles when a duplicate-coordinate pointermove arrives" | DRW-DOM-12, DRW-PTR-02 |
| "dismisses from a top-edge scroll container with a touch swipe down" / "dismisses from a bottom-edge scroll container with a touch swipe up" / "dismisses from a left-edge horizontal scroll container with a touch swipe right" / "dismisses from a right-edge horizontal scroll container with a touch swipe left" / "allows horizontal swipe dismiss from a vertical scroll container" | DRW-PTR-21, DRW-PTR-10, DRW-EVT-01 |
| "expands a collapsed sheet when the first move from a %s leaves the popup" (×3) | DRW-PTR-06, DRW-PTR-22, DRW-PTR-23, DRW-ALG-01, DRW-EVT-03 |
| "does not prevent a sub-slop first touchmove over cross-axis scrollable content" / "claims the gesture once the drawer axis passes the slop over cross-axis scrollable content" / "yields the gesture when the browser commits to a native cross-axis scroll" / "keeps tracking the finger when a claimed drag returns inside the slop" / "keeps driving a claimed drag through a non-cancelable touchmove" / "does not lock vertical swipe after minor cross-axis jitter in down drawers" | DRW-PTR-22, DRW-ALG-03, DRW-DOM-12 |
| "does not hijack cross-axis gestures from mixed-axis scroll containers" (C) / "does not block vertical scrolling in right drawers when only vertical overflow exists" (C) / "does not block vertical scrolling in left drawers when only vertical overflow exists" (C) / "does not block horizontal scrolling in down drawers when only horizontal overflow exists" (C) / "does not block horizontal scrolling in up drawers when only horizontal overflow exists" (C) | DRW-PTR-22, DRW-PTR-21 |
| "toggles data-swiping on the backdrop while swiping" | DRW-DOM-20, DRW-PTR-01 |
| "cancels pointer drags and ignores compatibility touch pointer cancellation" / "ignores compatibility touch pointer gestures with real displacement" | DRW-PTR-05, DRW-PTR-04, PSH-PTR-26 |
| "ignores touchstart when there is no active touch" / "falls back to the viewport when a touch event has no element target" | DRW-PTR-19 |
| "uses regular horizontal dismissal when snap points are configured" | DRW-API-02, DRW-PTR-11 |
| "does not dismiss from a fast swipe that was never attributed to the snap point axis" / "navigates snap points from a drag that was never attributed to the swipe axis" / "does not dismiss from an unattributed fast swipe with snapToSequentialPoints" / "settles on the nearest snap point when an unattributed drag ends past the last snap point" / "settles on the nearest snap point when an unattributed drag ends past the last snap point with snapToSequentialPoints" | DRW-ALG-01, DRW-ALG-01 (S) |
| "clears nested swipe state after an unattributed snap point gesture" / "clears nested swipe state after an attributed drag settles on a snap point" | DRW-PTR-17, DRW-PTR-16, DRW-ALG-01 |
| "does not start swipe dismissal when closing the snap point is canceled" | DRW-ALG-01, DRW-ANI-02, DRW-DIS-05 |
| "keeps the resting snap point progress when a press never starts a swipe" | DRW-STA-17, DRW-ALG-05, DRW-DOM-18 |
| "does not resolve snap points before the popup has a measurable height" / "falls back to regular dismissal when all snap points are invalid" | DRW-EDGE-04, DRW-STA-06 |
| "ends swipe drag when the primary mouse button is released mid-gesture" | DRW-PTR-01, PSH-PTR-20/21 |
| "ignores swipe input until a popup is mounted" / "does not start pointer swipes while a closed drawer remains mounted" | DRW-PTR-09 |
| "cleans up when the popup unmounts during an active swipe" | DRW-ANI-05 |
| "publishes and clears swipe progress through Drawer.Provider" | DRW-PTR-16, DRW-DOM-32 |
| "publishes resting snap progress before descendant layout effects" | DRW-PTR-16 |
| "clears kept-mounted swipe state before descendant layout effects on reopen" | DRW-ANI-03, DRW-DOM-07 |
| "clears nested progress before descendant layout effects when the child closes" | DRW-ANI-04, DRW-DOM-14 |
| "clears provider and backdrop swipe state during layout teardown" | DRW-ANI-05 |
| "leaves pinch-zoom touchmove to the browser" / "prevents page scrolling for a non-overflowing touch scroll container" / "keeps a claimed scroll-edge swipe moving through non-cancelable events" / "does not claim a stationary touch in overflowing content" | DRW-PTR-21 |

### `swipe-area/DrawerSwipeArea.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, in `Root`) | RND-DOM-36…49, DRW-STR-07 |
| "opens the drawer when swiped in the open direction" / "respects custom swipeDirection" / "opens the drawer when swiped with touch events" | DRW-PTR-28, DRW-PTR-30, DRW-API-09, DRW-DOM-25 |
| "does not open when the swipe direction never locks to the open direction" | DRW-PTR-28 |
| "prevents default pointer down for non-touch swipes" / "does not prevent a non-cancelable pointer start" | DRW-PTR-27 |
| "does not open the drawer when disabled" | DRW-API-08, DRW-PTR-26 |
| "synchronizes overshoot styles across the popup, backdrop, and provider indent" / "opens from the rendered transform distance in either horizontal direction" / "omits frontmost height when an opening drawer has no measured height" | DRW-ALG-04, DRW-PTR-29, DRW-PTR-31, DRW-DOM-18/20/21 |
| "does not apply or close swipe state when a controlled parent rejects opening" | DRW-PTR-28, DRW-ALG-04 |
| "cancels an active opening gesture when the swipe area becomes disabled" / "keeps the drawer open when the release click follows a mid-gesture disable" | DRW-PTR-31, DRW-DIS-02, DRW-DOM-28 |
| "ignores compatibility touch pointer gestures with real displacement" | DRW-PTR-27 |
| "works before a generated swipe area id is available" / "registers the swipe area once a generated id becomes available" / "follows a swipe area id change" | DRW-API-10, DRW-DOM-30, DRW-PTR-32 |
| "applies data-swiping during an active swipe gesture" | DRW-DOM-26 |
| "re-enables outside press dismissal after opening by swipe" / "re-enables outside press dismissal for a touch outside press after opening by swipe" / "does not dismiss from the $pointerType/$input click synthesized by the swipe-open release" (×3) / "suppresses a trusted release click that arrives without a preceding pointerdown" (C) / "allows a later click-only activation to dismiss after a swipe" (C) / "allows a later pointerless virtual click to dismiss when the release has no click" | DRW-DIS-02 |
| "re-enables outside press dismissal after an interrupted swipe-open gesture" / "re-enables outside press dismissal after a context menu interrupts swipe-open" | DRW-PTR-31, DRW-DIS-02 |
| "uses a size-based swipe threshold by default" (C) | DRW-PTR-30 |
| "keeps the swipe-area movement on the popup when re-grabbed during close" (C) | DRW-PTR-29, DRW-ANI-03 |
| "opens on a quick flick whose only move is already released (buttons: 0)" / "commits a released-move quick flick exactly once when a real pointerup trails it" / "opens on a quick flick that lands its whole travel in a single touch move" / "does not open on an in-place press-release without movement" / "does not open a horizontal drawer on a stationary pointer move" | DRW-PTR-25, DRW-PTR-28, DRW-PTR-30, PSH-PTR-13/21 |

### `content/DrawerContent.test.tsx`, `indent/DrawerIndent.test.tsx`, `indent-background/DrawerIndentBackground.test.tsx`, `provider/DrawerProvider.test.tsx`

| Test | Requirements |
|---|---|
| Content describeConformance (`refInstanceof: HTMLDivElement`, in `Root open > Portal > Viewport > Popup`) | RND-DOM-36…49, DRW-STR-06 |
| "does not add public swipe-ignore attributes" | DRW-DOM-24 |
| Indent / IndentBackground "sets data-active when any drawer is open" | DRW-DOM-31, DRW-STA-14 |
| "stays active until every open drawer is closed or removed" / "ignores redundant registry updates without disturbing active state" / "releases closed drawers once they are removed" (jsdom only) | DRW-STA-14, DRW-API-14 |
| "synchronizes and restores visual state on Drawer.Indent" | DRW-STA-15, DRW-DOM-32 |
| "allows indent parts to render without a provider" | DRW-STR-08, DRW-DOM-31 |

### `virtual-keyboard-provider/DrawerVirtualKeyboardProvider.test.tsx` (all C)

| Test | Requirements |
|---|---|
| "compensates for keyboard overlap without adding extra spacing and centers the focused input" / "tops the scroll container padding up to the visibility margin below the overlap" | DRW-FOC-05, DRW-FOC-06, DRW-ALG-07 |
| "adds scroll slack to a light-DOM scroller for an input inside a shadow root" / "adds keyboard scroll slack to a potential scroll ancestor for textareas" / "adds keyboard scroll slack to the scroll container, not an overflowing textarea" | DRW-ALG-07, DRW-FOC-06 |
| "sets the keyboard inset CSS variable for a focused input without a scroll target" / "captures focusin targets through a shadow root" / "computes the keyboard inset from a non-zero visual viewport offset" | DRW-DOM-23, DRW-FOC-05, DRW-ALG-06 |
| "keeps the keyboard inset when focus moves directly to another keyboard input" / "releases keyboard inset and scroll slack after focus leaves the field (pointerdown: %s)" (×2) / "activates an item on the first tap while the keyboard is open" | DRW-FOC-07, DRW-FOC-08 |
| "does not add keyboard scroll slack by default" | DRW-API-11, DRW-FOC-03 |
| "restores keyboard scroll slack when the drawer closes" | DRW-FOC-11, DRW-FOC-06 |
| "does not add keyboard scroll slack while pinch-zoomed" / "does not align inputs when visualViewport is unavailable" / "does not treat a small visual viewport reduction as the software keyboard" | DRW-FOC-03, DRW-ALG-06 |
| tap tests: "focuses an unfocused keyboard input on touchend without page scroll", "preserves native taps on picker inputs", "preserves native taps that lift over a neighboring interactive element", "resolves the lift point through the shadow root when the drawer renders in one", "preserves native taps on disabled inputs", "preserves native taps on disabled textareas", "preserves native taps on a disabled labelled control", "focuses a keyboard input with data-base-ui-swipe-ignore without starting drawer swipe", "does not focus a keyboard input on touchend without touchstart", "does not focus a keyboard input after touchcancel", "does not focus a keyboard input while a nested drawer is open", "preserves native taps on an already-focused keyboard input", "refocuses an already-focused keyboard input when the keyboard viewport is closed", "preserves preventScroll after activating an %s label with no input focused" (×2), "preserves a focus change from a label click handler", "focuses a contentEditable element on touchend", "focuses the editing host when tapping a nested element inside a contentEditable", "probes nearby points to resolve a keyboard input retargeted on touchend", "does not focus a keyboard input when the touch moves past the tap threshold", "does not steal a tap that lands on an interactive element near a keyboard input", "preserves native taps on keyboard inputs while pinch-zoomed", "ignores a directly captured touch target while a closed drawer remains mounted", "does not focus a lift-point target outside the drawer", "falls back to the touch target when point lookup misses", "uses the remaining touch and MouseEvent fallback for keyboard taps", "ignores a keyboard tap whose native target is not an element", "ignores touchend without a lift coordinate" | DRW-FOC-10, DRW-ALG-08 |
| "overrides the incoming field geometry when focus moves without a tap while the keyboard is open" / "does not blur and re-focus for focus changes while the keyboard is closed" | DRW-FOC-07 |
| "centers a field focused without a tap inside the scrollable body" / "realigns a tapped field in the scrollable body when the keyboard is already open for a field outside it" | DRW-FOC-07, DRW-FOC-08, DRW-ALG-07 |
| "does not restart a progressing or settled reduced-motion alignment scroll" / "re-issues the alignment scroll when a delayed realign pass finds it stalled" / "stops pending realignment once the user starts scrolling while geometry is changing" / "defers the alignment scroll until the destination stops moving" / "eventually aligns when geometry never settles" | DRW-FOC-08, DRW-ALG-07 |
| "clears keyboard state when a tapped input redirects focus away" / "clears keyboard state when a tapped input blurs itself" | DRW-FOC-07, DRW-FOC-10 |
| "restores the page scroll position when a modal drawer page scrolls while the keyboard is open" / "does not restore the page scroll position for a non-modal drawer" | DRW-FOC-09 |
| "restores the previous scroll slack when focus moves to a different scroll container" / "restores slack from a removed scroll container before adjusting a new one" / "clears inset and slack when the focused field is removed" | DRW-FOC-06, DRW-ALG-07 |
| "captures an initially focused input without attempting an unnecessary page restore" | DRW-FOC-03, DRW-FOC-09 |
| describe "layout viewport resized for the keyboard (Chrome on iOS)": "applies no keyboard inset or slack when the small viewport follows the keyboard", "aligns the focused field once the layout viewport has shrunk to the visual viewport", "scrolls the focused field once the layout viewport reaches the visual viewport", "waits for the layout viewport to grow to a shorter keyboard before scrolling", "does not treat a plain resize of both viewports as the software keyboard", "clears the keyboard inset and slack once the layout viewport catches up with the visual viewport", "preserves native taps while the keyboard is open and refocuses once it closes" | DRW-ALG-06, DRW-FOC-04, DRW-FOC-08, DRW-FOC-10 |

### Tests in other files that exercise this component

| File / test | Requirements |
|---|---|
| `dialog/root/DialogRoot.test.tsx` "reports nested drawer counts before passive effects" (constructs `DialogInteractions … isDrawer`) | DLG-STA-05, DRW-STA-18 |
| `utils/useSwipeDismiss.test.tsx` (all) | PSH-PTR-09…28 (configured by DRW-PTR-01/25) |

## 14. React-specific notes and porting considerations

Non-normative.

- **Imperative styles vs. render styles.** Drag movement, backdrop progress, `data-swiping` on the backdrop, `data-swipe-dismiss`, the swipe-release `data-ending-style`, nested progress and the Indent variables are written directly to `element.style`/attributes so that a drag does not re-render the tree. The same properties are also declared in render (`--drawer-swipe-progress: 0` etc.); React only re-applies a declared style when its *rendered* value changes, so imperative values survive re-renders. A port that re-applies the full style on every render must treat these as imperative-owned, or keep them in state. Port: see decisions/04-js-interop-boundary.md §2.
- **Native touchmove listener.** React's touch events are delegated; Drawer registers its own non-passive capture `touchmove` on the document and calls `stopPropagation()` to keep React (and its re-rasterization) out of the drag. Ports with direct DOM listeners can attach to the Viewport, but must keep capture-phase, `passive: false` and the "only while open" lifetime. Port: see decisions/04-js-interop-boundary.md §1 (the `drawer` component module owns the touch loop; ADR 01 constraint C1).
- **`flushSync` on swipe release.** The strength scalar is committed synchronously in the release handler so `--drawer-swipe-strength` is present in the same frame as the imperative `data-ending-style`. Port: see decisions/04-js-interop-boundary.md §2 (JS writes both `--drawer-swipe-strength` and `data-ending-style`, so no synchronous commit is needed).
- **Controlled rejection check.** DRW-PTR-15 relies on React flushing the parent's state update before the next animation frame; a port should check the effective open state after the consumer's handler has run (next frame is the observed contract). Port: see decisions/01-hosting-model-and-timing.md §Decision ("next frame" is JavaScript-only; on Server the rejection check sees C#'s `open` a round trip late).
- **Layout-effect ordering.** Many tests ("…before passive effects", "…before descendant layout effects") pin that registration, nested reports and cleanups happen post-commit, pre-paint and that the Viewport's reset runs before descendants'. React runs child layout effects before parents'; Drawer places these in parts that are ancestors/siblings deliberately. A port needs equivalent ordering. Port: see decisions/01-hosting-model-and-timing.md §Decision (DOM-only pre-paint work in JS is met on both modes; C# state work is after paint plus a round trip on Server, §Relaxation table).
- **External stores.** Nested swipe progress and the Provider visual state are tiny subscribable stores read without re-rendering (`useSyncExternalStore`-like); the Provider registry is a mutable `Set` with a state flag for `active`. Port: see decisions/05-state-store-and-event-details.md §1.
- **`CSS.registerProperty`.** A one-time module-level flag; `inherits: false` changes CSS semantics (descendants cannot read the variables).
- **React 17 `useId`.** SwipeArea registration re-runs when a generated id appears late (`useId` fallback); a port with synchronous ids can register at mount. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision (generated ids `base-ui-<n>`).
- **Parts that are Dialog parts.** `Drawer.Trigger` etc. are literally the Dialog components cast to Drawer types; their error messages mention Dialog.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| DRW-STR-01 | MUST | Drawer satisfies every requirement of [dialog](dialog.md) (DLG-STR-01…13, DLG-API-01…27, DLG-STA-01…15, DLG-DOM-01…28… | 2.1 |
| DRW-STR-02 | MUST | The `Drawer` namespace exports `Backdrop`, `Close`, `Content`, `Description`, `Indent`, `IndentBackground`, `Popup`,… | 2.1 |
| DRW-STR-03 | MUST | `Drawer.Trigger`, `Drawer.Portal`, `Drawer.Title`, `Drawer.Description` and `Drawer.Close` are the Dialog implementat… | 2.1 |
| DRW-STR-04 | MUST | `Drawer.Viewport` renders the Dialog Viewport element (default `<div>`, inside the Portal; DLG-STR-06, DLG-DOM-11…14)… | 2.1 |
| DRW-STR-05 | MUST | `Drawer.Popup` renders a `<div>` by default; like Dialog.Popup it requires a Portal ancestor (DLG-EDGE-03) and a Root… | 2.1 |
| DRW-STR-06 | MUST | `Drawer.Content` renders a `<div>` by default anywhere under a Root (it requires the Dialog Root's shared state, DLG-… | 2.1 |
| DRW-STR-07 | MUST | `Drawer.SwipeArea` renders a `<div>` by default anywhere under a Root (requires both the Dialog and the Drawer Root s… | 2.1 |
| DRW-STR-08 | MUST | `Drawer.Provider` renders no element; `Drawer.Indent` and `Drawer.IndentBackground` render a `<div>` by default and w… | 2.1 |
| DRW-STR-09 | MUST | `Drawer.VirtualKeyboardProvider` renders no element; it must be under a Root (DLG-EDGE-02) and acts only when a `Draw… | 2.1 |
| DRW-STR-10 | MUST | *Drawer nesting* is determined by the nearest ancestor `Drawer.Root` in the component tree, independently of Dialog n… | 2.1 |
| DRW-API-01 | MUST | `swipeDirection` defaults to `'down'` and accepts `'up'`, `'down'`, `'left'`, `'right'`. It selects the dismiss direc… | 3.4 |
| DRW-API-02 | MUST | `snapPoints` is a list of snap heights: a number in `[0, 1]` (at most 1) is a fraction of the viewport height, a numb… | 3.4 |
| DRW-API-03 | MUST | `snapPoint`, when not `undefined`, is the consumer-owned active snap point (a controlled `null` means "no snap point"… | 3.4 |
| DRW-API-04 | MUST | The default snap point is `defaultSnapPoint` when it is not `undefined` (including `null`), otherwise `snapPoints[0]`… | 3.4 |
| DRW-API-05 | MUST | `onSnapPointChange(snapPoint, eventDetails)` is called before every snap point change is applied; `eventDetails` is a… | 3.4 |
| DRW-API-06 | MUST | `snapToSequentialPoints` defaults to `false`; when `true` the release target ignores the velocity projection and at m… | 3.4 |
| DRW-API-07 | MUST | When `initialFocus` is `undefined` the Popup passes a DOM handle to its own element as the initial-focus target, so t… | 3.11 |
| DRW-API-08 | MUST | `disabled` defaults to `false`; when `true` the area never starts a gesture, cancels a gesture in progress (DRW-PTR-3… | 3.6 |
| DRW-API-09 | MUST | The opening direction is the `swipeDirection` prop when given, otherwise the opposite of the Root's `swipeDirection`… | 3.6 |
| DRW-API-10 | MUST | The SwipeArea renders `id` = the `id` prop or a generated id (omitted while no id is available) and registers its ele… | 3.6 |
| DRW-API-11 | MUST | `Drawer.VirtualKeyboardProvider` accepts only `children`; Drawers without it have none of the §6.3.1 behavior. | 3.7 |
| DRW-API-12 | MUST | `Drawer.createHandle<Payload>()` returns a new `Drawer.Handle<Payload>`, a nominal subtype of `Dialog.Handle` with id… | 3.16 |
| DRW-API-13 | MUST | `Drawer.Content`, `Drawer.Indent`, `Drawer.IndentBackground` and `Drawer.Viewport` have no component-specific props:… | 3.12 |
| DRW-API-14 | MUST | `Drawer.Provider` accepts only `children` and provides, to descendants, the open-drawer registry (DRW-STA-14) and the… | 3.1 |
| DRW-STA-01 | MUST | The state objects exposed to `className`/`style`/`render` are exactly: Popup `{ open, transitionStatus, expanded, nes… | 3.18 |
| DRW-STA-02 | MUST | The *active snap point* exposed to the parts is: when `snapPoint` is controlled, its value as given (even if not in `… | 4.1 |
| DRW-STA-03 | MUST | Every snap point change request runs: (1) build details (reason `none`, no event, when the requester supplies none);… | 4.1 |
| DRW-STA-04 | MUST | The Root's open-change wrapper calls the consumer's `onOpenChange(next, details)`; if the request was not canceled, `… | 4.1 |
| DRW-STA-05 | MUST | A snap point value resolves to a height in px (given viewport height `vh` and root font size `fs`): `null` when `vh`… | 4.1 |
| DRW-STA-06 | MUST | The *resolved snap point list* is empty when `snapPoints` is empty, `vh ≤ 0` or the measured popup height `ph ≤ 0`. O… | 4.1 |
| DRW-STA-07 | MUST | The *active snap offset* is `null` when the active snap point is `null`; else the offset of the resolved entry whose… | 4.1 |
| DRW-STA-08 | MUST | `vh` is the Viewport element's `offsetHeight` when a Viewport is registered, otherwise `document.documentElement.clie… | 4.1 |
| DRW-STA-09 | MUST | `getSnapPointSwipeMovement(base, m)` = `m` when `base + m ≥ 0`, else `−√(−(base + m)) − base` (the overshoot past the… | 4.1 |
| DRW-STA-10 | MUST | While mounted, the Popup measures its own `offsetHeight` post-commit, pre-paint on mount, when the nested-drawer stat… | 4.2 |
| DRW-STA-11 | MUST | The Root's *frontmost height* is the own popup height when no nested drawer has reported a positive height; a positiv… | 4.2 |
| DRW-STA-12 | MUST | A nested Drawer reports *presence* (`open \|\| transitionStatus === 'ending'`) to its parent post-commit, pre-paint w… | 4.2 |
| DRW-STA-13 | MUST | The Root keeps a *nested swipe progress* value (initially 0) outside the render cycle: a report replaces it with the… | 4.2 |
| DRW-STA-14 | MUST | Every Root under a `Drawer.Provider` registers itself post-commit, pre-paint with its effective open state whenever t… | 4.3 |
| DRW-STA-15 | MUST | The Provider's *visual state* is `{ swipeProgress, frontmostHeight }`, initially `{0, 0}`. An update may set either f… | 4.3 |
| DRW-STA-16 | MUST | `expanded` is `true` iff the active snap point is the number `1` (strict equality; `'100%'` or a px value equal to th… | 4.1 |
| DRW-STA-17 | MUST | When at least two snap points are configured, the resolved list has at least two entries and the direction is vertica… | 4.1 |
| DRW-STA-18 | MUST | `nestedOpenDrawerCount` (DLG-STA-04/05) counts only Drawer descendants: a nested Dialog or AlertDialog contributes to… | 4.2 |
| DRW-DOM-01 | MUST | The Popup renders `data-open`/`data-closed`/`data-starting-style`/`data-ending-style` as DLG-DOM-20 and `data-nested=… | 5.1 |
| DRW-DOM-02 | MUST | The Popup renders `data-expanded=""` iff `expanded` (DRW-STA-16). | 5.1 |
| DRW-DOM-03 | MUST | The Popup renders `data-nested-drawer-open=""` iff `nestedOpenDrawerCount > 0`; it is removed in the same commit in w… | 5.1 |
| DRW-DOM-04 | MUST | The Popup renders `data-nested-drawer-swiping=""` iff `nestedSwiping` (DRW-STA-12). | 5.1 |
| DRW-DOM-05 | MUST | The Popup always renders `data-swipe-direction="<swipeDirection>"`. | 5.1 |
| DRW-DOM-06 | MUST | The Popup renders `data-swiping=""` while the Viewport gesture is active (from the render after gesture start until t… | 5.1 |
| DRW-DOM-07 | MUST | `data-swipe-dismiss=""` is toggled imperatively on both the Popup and the consumer Backdrop: set when a release commi… | 5.1 |
| DRW-DOM-08 | MUST | The Popup's inline style declares `--nested-drawers` = `nestedOpenDrawerCount` (`0` when none). | 5.1 |
| DRW-DOM-09 | MUST | The Popup declares `--drawer-height: <popupHeight>px` only while the measured height is non-zero **and** (a nested dr… | 5.1 |
| DRW-DOM-10 | MUST | The Popup declares `--drawer-frontmost-height: <frontmostHeight>px` when the Root's frontmost height (DRW-STA-11) is… | 5.1 |
| DRW-DOM-11 | MUST | The Popup declares `--drawer-snap-point-offset`: when snap points apply (DRW-API-02) and the active snap offset (DRW-… | 5.1 |
| DRW-DOM-12 | MUST | The Popup's drag styles are the swipe tracker's (PSH-DOM-17 with movement variables `--drawer-swipe-movement-x/-y`, P… | 5.1 |
| DRW-DOM-13 | MUST | The Popup declares `--drawer-swipe-strength` = the release scalar of DRW-ALG-02 when one is stored (finite and > 0),… | 5.1 |
| DRW-DOM-14 | MUST | The Popup declares `--drawer-swipe-progress: 0` inline and, post-commit, pre-paint and on every change of the Root's… | 5.1 |
| DRW-DOM-15 | MUST | The Popup's inline style is composed in this order, later entries winning: the drag styles (DRW-DOM-12), then `--draw… | 5.1 |
| DRW-DOM-16 | MUST | The Popup renders `id` = the consumer `id` when given, else the generated popup id (DLG-DOM-16), plus `role`, `aria-l… | 5.1 |
| DRW-DOM-17 | MUST | `Drawer.Backdrop` renders as Dialog's Backdrop (DLG-STR-05, DLG-DOM-07…10, DLG-ARIA-08) and additionally has inline `… | 5.2 |
| DRW-DOM-18 | MUST | The Backdrop declares `--drawer-swipe-progress: 0` inline; during gestures it is set imperatively to the dismiss prog… | 5.2 |
| DRW-DOM-19 | MUST | The Backdrop declares `--drawer-swipe-strength: 1` inline and never changes it. | 5.2 |
| DRW-DOM-20 | MUST | The Backdrop's `data-swiping=""` is toggled imperatively: on by the Viewport gesture start and by SwipeArea movement,… | 5.2 |
| DRW-DOM-21 | MUST | While a gesture shows non-zero progress and the frontmost height is > 0, the Backdrop's inline `--drawer-height` is `… | 5.2 |
| DRW-DOM-22 | MUST | `Drawer.Viewport` never renders `data-nested-dialog-open` (even when a consumer passes it); `data-nested` and the oth… | 5.3 |
| DRW-DOM-23 | MUST | A `Drawer.VirtualKeyboardProvider` writes `--drawer-keyboard-inset: <max(0, ceil(inset))>px` on the Viewport element… | 5.3 |
| DRW-DOM-24 | MUST | `Drawer.Content` renders `data-drawer-content=""` (internal marker used by DRW-PTR-04) and never `data-base-ui-swipe-… | 5.4 |
| DRW-DOM-25 | MUST | The SwipeArea renders `data-open=""` when the drawer is open and `data-closed=""` otherwise. | 5.5 |
| DRW-DOM-26 | MUST | The SwipeArea renders `data-swiping=""` while its own gesture is active (tracker swiping state). | 5.5 |
| DRW-DOM-27 | MUST | The SwipeArea always renders `data-swipe-direction="<opening direction>"`. | 5.5 |
| DRW-DOM-28 | MUST | The SwipeArea renders `data-disabled=""` iff `disabled`. | 5.5 |
| DRW-DOM-29 | MUST | The SwipeArea's inline style has `pointer-events: none` while it is not enabled (DRW-PTR-26) and `touch-action: pan-y… | 5.5 |
| DRW-DOM-30 | MUST | The SwipeArea renders its `id` attribute only once an id is available (consumer `id`, or a generated id); while none… | 5.5 |
| DRW-DOM-31 | MUST | Indent and IndentBackground render `data-active=""` when the nearest Provider is active and `data-inactive=""` otherw… | 5.6 |
| DRW-DOM-32 | MUST | Indent declares `--drawer-swipe-progress: 0` inline; post-commit, pre-paint and on every Provider visual-state change… | 5.6 |
| DRW-DOM-33 | SHOULD | The first time any Drawer Popup mounts in the page, where `CSS.registerProperty` exists, the variables `--drawer-swip… | 5.7 |
| DRW-ARIA-01 | MUST | The SwipeArea renders `role="presentation"` and `aria-hidden="true"`; it is not focusable and is not announced. | 5.8 |
| DRW-ARIA-02 | MUST | Because a SwipeArea is a registered trigger, a swipe-open makes the SwipeArea the active trigger (`details.trigger` =… | 5.8 |
| DRW-ARIA-03 | MUST | All other ARIA (Popup `role="dialog"`, `aria-labelledby`/`aria-describedby`, Trigger `aria-haspopup="dialog"`/`aria-e… | 5.8 |
| DRW-KEY-01 | MUST | Drawer adds no key handling beyond Dialog's: the SwipeArea is not focusable (DRW-ARIA-01), and neither swipe-to-dismi… | 6.1 |
| DRW-PTR-01 | MUST | `Drawer.Viewport` configures the swipe tracker (PSH-PTR-09…28, PSH-ALG-07…12) with: tracked element = the Popup; `ena… | 6.2.1 |
| DRW-PTR-02 | MUST | The tracker's allowed directions are `[swipeDirection]`, except when snap points are non-empty and the direction is v… | 6.2.1 |
| DRW-PTR-03 | MUST | The dismiss threshold for direction `d` is `max(size along d × 0.5, 10)` px of the Popup (re-evaluated when the direc… | 6.2.1 |
| DRW-PTR-04 | MUST | On the Viewport `pointerdown` (bubbling from any descendant): the pointer type is remembered (and, for `pen`, the nex… | 6.2.1 |
| DRW-PTR-05 | MUST | Viewport `pointermove` events with `pointerType: 'touch'` are not passed to the tracker; `pointerup`/`pointercancel`… | 6.2.1 |
| DRW-PTR-06 | MUST | The tracker may start a gesture (`canStart`) only when: a Popup element exists; the element at the gesture point is i… | 6.2.1 |
| DRW-PTR-07 | MUST | Because `ignoreSelectorWhenTouch` is `false`, a mouse/pen press on `button, a, input, select, textarea, label, [role=… | 6.2.1 |
| DRW-PTR-08 | MUST | When a non-touch gesture starts and the document selection is non-collapsed with its anchor or focus node (or that no… | 6.2.1 |
| DRW-PTR-09 | MUST | The Viewport gesture never starts while the drawer is closed (also when kept mounted or during its exit), while a nes… | 6.2.1 |
| DRW-PTR-10 | MUST | With no snap points, the release decision (returned to the tracker as its `onRelease` override, PSH-PTR-24) is: no in… | 6.2.3 |
| DRW-PTR-11 | MUST | With non-empty snap points: a horizontal `swipeDirection` clears the release and defers to the tracker (regular dismi… | 6.2.4 |
| DRW-PTR-12 | MUST | When a release commits to dismissal ("start swipe release"), synchronously and before the open-change request: the ne… | 6.2.3 |
| DRW-PTR-13 | SHOULD | The strength scalar is computed per DRW-ALG-02 so that faster releases with less remaining distance yield a smaller `… | 6.2.3 |
| DRW-PTR-14 | MUST | On the tracker's dismissal (`onDismiss`): the Provider visual state is set to `{0, 0}`; the Backdrop's `--drawer-swip… | 6.2.3 |
| DRW-PTR-15 | MUST | When a non-canceled swipe close leaves the store open (a controlled owner that has not, or not yet, set `open={false}… | 6.2.3 |
| DRW-PTR-16 | MUST | On every tracker progress report, the Viewport computes `p` = the tracker progress (PSH-ALG-09) or, with a snap range… | 6.2.5 |
| DRW-PTR-17 | MUST | A nested drawer reports *swiping* to its parent (DRW-STA-12) once per gesture, the first time a progress report durin… | 6.2.5 |
| DRW-PTR-18 | MUST | While a drawer's popup is mounted and its own `nestedOpenDrawerCount > 0`, its Viewport gesture is disabled (both poi… | 6.2.5 |
| DRW-PTR-19 | MUST | On the Viewport `touchstart`: (1) if the last `pointerdown` was `pen` and marked, the mark is cleared, touch state re… | 6.2.2 |
| DRW-PTR-20 | MUST | While the drawer is open and mounted and a Viewport (or Popup) element exists, a `touchmove` listener is registered o… | 6.2.2 |
| DRW-PTR-21 | MUST | For each document `touchmove` (after notifying the VirtualKeyboardProvider, DRW-FOC-10), unless the current touch is… | 6.2.2 |
| DRW-PTR-22 | MUST | Cross-axis arbitration follows DRW-ALG-03 with slop 6 px and bias 2 px; a drag attributed to the drawer axis keeps be… | 6.2.2 |
| DRW-PTR-23 | MUST | `touchend` and `touchcancel` on the Viewport notify the VirtualKeyboardProvider, clear all touch state (including the… | 6.2.2 |
| DRW-PTR-24 | MUST | Only `data-base-ui-swipe-ignore` is honored; the legacy `data-swipe-ignore` attribute (still honored by Toast) has no… | 6.2.2 |
| DRW-PTR-25 | MUST | The SwipeArea configures its own swipe tracker with: tracked element = the SwipeArea; `enabled` per DRW-PTR-26; `dire… | 6.2.6 |
| DRW-PTR-26 | MUST | The SwipeArea is *enabled* iff not `disabled` and (the drawer is closed or its own gesture is active); while not enab… | 6.2.6 |
| DRW-PTR-27 | MUST | Pointer events with `pointerType: 'touch'` are ignored by the SwipeArea (touch input arrives as touch events, passed… | 6.2.6 |
| DRW-PTR-28 | MUST | On gesture start the SwipeArea disables outside-press dismissal (DRW-DIS-02), remembers the start event and marks its… | 6.2.6 |
| DRW-PTR-29 | MUST | While the drawer is open and mounted, the popup position during a swipe-open follows DRW-ALG-04 (written imperatively… | 6.2.6 |
| DRW-PTR-30 | MUST | On release: `threshold` = Popup size along the dismiss axis × 0.5 (40 px when there is no Popup or its size is 0); *c… | 6.2.6 |
| DRW-PTR-31 | MUST | Gesture finish (release or tracker cancel, e.g. a non-primary button during the drag) resets the gesture state, arms… | 6.2.6 |
| DRW-PTR-32 | MUST | The SwipeArea's element is registered as a trigger of the Root (DRW-API-10), so a press on it is never an outside pre… | 6.2.6 |
| DRW-FOC-01 | MUST | With `initialFocus` unset, opening a drawer (by any interaction type, including mouse and keyboard) focuses the Popup… | 6.3 |
| DRW-FOC-02 | MUST | `initialFocus={true}` (or a function returning `true`/`null`) focuses the first tabbable element inside the Popup, fa… | 6.3 |
| DRW-FOC-03 | MUST | The provider is active only while the drawer is open and mounted and a Viewport element is registered; while active i… | 6.3.1 |
| DRW-FOC-04 | MUST | Keyboard detection follows DRW-ALG-06 (threshold 60 px, remembered keyboard visual height, `100svh` probe for layouts… | 6.3.1 |
| DRW-FOC-05 | MUST | *Alignment* (DRW-ALG-07) sets `--drawer-keyboard-inset` on the Root to `max(0, ceil(innerHeight − keyboard.bottom))`… | 6.3.1 |
| DRW-FOC-06 | MUST | Scroll slack: `slack = max(0, ceil(overlap))`; 0 restores any adjustment. Before the first adjustment of an element i… | 6.3.1 |
| DRW-FOC-07 | MUST | Focus events: `focusin` clears the programmatic-focus suppression, restores any preempted geometry, and either captur… | 6.3.1 |
| DRW-FOC-08 | MUST | Alignment scheduling: at most one pending frame callback; an alignment whose computed destination differs (> 1 px) fr… | 6.3.1 |
| DRW-FOC-09 | MUST | For `modal === true` only, while a keyboard input is tracked, no nested dialog is open and a keyboard is detected, an… | 6.3.1 |
| DRW-FOC-10 | MUST | *Tap to focus*: the Viewport forwards `touchstart`, every document `touchmove` (even moves the swipe claims), `touche… | 6.3.1 |
| DRW-FOC-11 | MUST | When the drawer closes (or the provider unmounts or its Root changes), the tracked target is cleared, scroll slack re… | 6.3.1 |
| DRW-DIS-01 | MUST | On Android (platform detection `os.android`), while the drawer is open and topmost (`nestedOpenDialogCount === 0`) an… | 6.4 |
| DRW-DIS-02 | MUST | From the start of a SwipeArea gesture, outside-press dismissal of the Root is disabled (the outside-press predicate r… | 6.4 |
| DRW-DIS-03 | MUST | Swipe dismissal is independent of `disablePointerDismissal` and of `modal`: a drawer with `disablePointerDismissal` c… | 6.4 |
| DRW-DIS-04 | MUST | In a drawer stack only the frontmost drawer can be swiped (DRW-PTR-18); Escape and outside presses follow DLG-DIS-03… | 6.4 |
| DRW-DIS-05 | MUST | Closing a drawer that has snap points by any path (Escape, outside press, Close, swipe, imperative, `close-watcher`)… | 6.4 |
| DRW-EVT-01 | MUST | The reasons `onOpenChange` receives, and the action producing each, are exactly: | 7 |
| DRW-EVT-02 | MUST | For `swipe` requests: from the Viewport, `details.event` is the native release event (`PointerEvent` or `TouchEvent`,… | 7 |
| DRW-EVT-03 | MUST | `onSnapPointChange` receives: reason `swipe` with the release event when a release settles on a snap point, requests… | 7 |
| DRW-EVT-04 | MUST | For a swipe dismissal the order is: tracker release → (snap points) `onSnapPointChange(null)` → swipe release attribu… | 7 |
| DRW-ANI-01 | MUST | Exit completion is detected on the Popup element only; `data-ending-style` set imperatively by a swipe release (DRW-P… | 9 |
| DRW-ANI-02 | MUST | A swipe dismissal that is canceled or rejected removes the imperatively set `data-ending-style` and `data-swipe-dismi… | 9 |
| DRW-ANI-03 | MUST | Post-commit, pre-paint whenever the drawer becomes open (including reopening a kept-mounted drawer), the Viewport res… | 9 |
| DRW-ANI-04 | MUST | Post-commit, pre-paint when a nested drawer closes (or its Viewport unmounts), it reports nested swipe progress 0 to… | 9 |
| DRW-ANI-05 | MUST | When the Viewport unmounts (including mid-swipe), post-commit pre-paint teardown sets the Provider visual state to `{… | 9 |
| DRW-ANI-06 | MUST | A nested drawer counts as *present* for its parent until its exit transition ends (DRW-STA-12), so the parent keeps `… | 9 |
| DRW-EDGE-01 | SHOULD | Outside production, after paint when a `Drawer.Popup` is rendered without a `Drawer.Viewport` ancestor, it logs via `… | 12 |
| DRW-EDGE-02 | MUST | Popup, Viewport, SwipeArea, and any internal consumer of the Drawer Root's shared state rendered outside a `Drawer.Ro… | 12 |
| DRW-EDGE-03 | MUST | A Popup whose `render` produces no element does not throw (measurement and nested-progress synchronization tolerate a… | 12 |
| DRW-EDGE-04 | MUST | With snap points but a popup of measured height 0, a release never dismisses and never changes the snap point; with o… | 12 |
| DRW-EDGE-05 | MUST | Hit testing for gesture targets and tap targets uses the root node of the Viewport (document or shadow root), so draw… | 12 |
| DRW-EDGE-06 | MUST | Without `ResizeObserver` the popup and viewport heights are measured only at mount and on dependency changes; without… | 12 |
| DRW-EDGE-07 | MUST | Controlled-mode caveat: an `onOpenChange` that closes asynchronously makes a swipe dismissal revert after one frame (… | 12 |
| DRW-ALG-01 | MUST | The implementation reproduces the following snap-point release decision exactly (vertical drawer, non-empty resolved… | 6.5 |
| DRW-ALG-02 | MUST | The implementation reproduces the following swipe-strength computation exactly (`d` = dismissing direction; `none` →… | 6.5 |
| DRW-ALG-03 | MUST | The implementation reproduces the following touch arbitration exactly (run per document `touchmove` before scroll han… | 6.5 |
| DRW-ALG-04 | MUST | The implementation reproduces the following SwipeArea open movement exactly (`open` = opening direction, `dismiss` =… | 6.5 |
| DRW-ALG-05 | MUST | While swiping with a snap range (DRW-STA-17) and `ph > 0`, the published progress is `clamp((clamp(base + Δy, 0, ph)… | 6.5 |
| DRW-ALG-06 | MUST | The implementation reproduces the following keyboard-viewport detection exactly (`remembered` starts at −1 per activa… | 6.5 |
| DRW-ALG-07 | MUST | The implementation reproduces the following alignment exactly. | 6.5 |
| DRW-ALG-08 | MUST | The implementation reproduces the following tap-target resolution exactly (`(x, y)` = `changedTouches[0] ?? touches[0… | 6.5 |

Counts: STR 10, API 14, STA 18, DOM 33, ARIA 3, KEY 1, PTR 32, FOC 11, DIS 5, EVT 4, ANI 6, EDGE 7, ALG 8 — 152 requirements, no gaps per `DRW-AREA`. FRM, POS, A11Y and I18N emit no IDs (§8, §10; no live regions; no direction-dependent behavior — gesture directions are physical, not logical, and do not flip in RTL).

## 16. Open questions

1. **SwipeArea publishes inverted progress to the Indent.** During a swipe-open the Backdrop receives `1 − openProgress` but the Provider visual state (and so `Drawer.Indent`'s `--drawer-swipe-progress`) receives `openProgress` (`drawer/swipe-area/DrawerSwipeArea.tsx:243-244,259,267-270`), whereas the Viewport publishes *dismiss* progress to both (`drawer/viewport/DrawerViewport.tsx:213-227`). With the docs' indent CSS (`scale(0.98 + 0.02 × progress)`) a fully dragged-open sheet shows the page un-indented. The test "synchronizes overshoot styles…" pins `'1'` at overshoot. Intended?
2. **Backdrop `--drawer-swipe-strength` never changes.** The docs (§Stacking and animations) scale both `.DrawerPopup[data-ending-style]` and `.DrawerBackdrop[data-ending-style]` durations by `--drawer-swipe-strength`, but only the Popup's value is updated (`drawer/popup/DrawerPopup.tsx:384-387`); the Backdrop declares a constant `1` (`drawer/backdrop/DrawerBackdrop.tsx:48`) and the variable is registered `inherits: false`. The backdrop's exit therefore never speeds up.
3. **Snap progress uses raw `Δy` for `up` drawers.** DRW-ALG-05 adds `details.deltaY` to the base offset for both directions (`drawer/viewport/DrawerViewport.tsx:419-420`), but for `up` drawers dismissal moves the finger up (negative Δy) while offsets grow toward closed. Backdrop progress may move the wrong way while dragging an upward sheet; no test covers `up` + progress.
4. **SwipeArea becomes the active trigger.** A swipe-open passes the SwipeArea element as `trigger` (`drawer/swipe-area/DrawerSwipeArea.tsx:304`), so `Drawer.Trigger`s lose `aria-expanded="true"` and default return focus targets a non-focusable `aria-hidden` div (DRW-ARIA-02); focus then falls back per DLG-FOC-08 (focusing the div is a no-op). Unclear whether intended; untested.
5. **Unattributed release without snap points.** When the dismiss gesture never acquires an intended direction (e.g. a mostly cross-axis drag), the Viewport returns `undefined` (`:476-479`) and the tracker applies its own rule (first allowed direction whose displacement exceeds the snapshotted threshold, PSH-ALG-11), which can dismiss on the dismiss axis component of a diagonal drag. The snap-point branch explicitly refuses unattributed dismissals (`:557-562`); the regular branch does not. Confirm.
6. **`expanded` is `snapPoint === 1` only** (`drawer/popup/DrawerPopup.tsx:314`); equivalent values (`'100%'` is invalid anyway, a px value equal to the full height, or the top snap point when none equals 1) never set `data-expanded`. The outline describes it as "at the expanded (full-height) snap point".
7. **`'none'` reason.** Declared for `onOpenChange` but never emitted; for `onSnapPointChange` it is only emitted by internal callers that pass no details (none in v1.9.0 public flows; the test reaches it through internal shared state).
8. **VirtualKeyboardProvider "nested" uses the dialog count.** It disables itself when `nestedOpenDialogCount > 0` (`DrawerVirtualKeyboardProvider.tsx:90,97`), i.e. also for a nested non-drawer Dialog, while the Viewport gesture uses `nestedOpenDrawerCount`. Named `nestedDrawerOpen` in source; behavior documented as implemented (DRW-FOC-05/07/10).
9. **Outline discrepancies** (`components/page.mdx` §Drawer): Popup omits `data-nested` and `--drawer-swipe-progress`; Backdrop omits `data-swiping`, `data-swipe-dismiss`, `--drawer-swipe-strength`, `--drawer-height`; Indent/IndentBackground omit `data-active`/`data-inactive` and Indent's `--drawer-swipe-progress`/`--drawer-height`; Trigger and Close omit `disabled`; SwipeArea and Popup omit `id`; Content's `data-drawer-content` marker is omitted (internal). `data-swipe-dismiss` is listed for the Popup but is set imperatively, not from state. All listed props, attributes and types appear in this document.
10. **Hinted items not present in v1.9.0.** There is no `side` prop (only `swipeDirection`), no `Drawer.Handle` *part* (the outline's `Handle` is the imperative handle class), no `activeSnapPoint` prop (the prop is `snapPoint`), and no separate change-of-mind constant beyond the tracker's 10 px reverse cancel (PSH-PTR-15).
11. **Escape and CloseWatcher on Android.** Where a hardware/UA Escape also triggers a `CloseWatcher` close request, both the Escape handling (DLG-KEY-01) and the watcher (DRW-DIS-01) may request a close; the source comment (`DrawerRoot.tsx:446-447`) limits the watcher to Android to avoid this on desktop. Not tested.

### Base-doc discrepancies — to be fixed in `dialog.md`

1. **dialog.md §4.2 (store context, line 426)** says `outsidePressEnabledRef` is "always `true` for Dialog; Drawer flips it during swipes". Only `Drawer.SwipeArea` writes it (`drawer/swipe-area/DrawerSwipeArea.tsx:134,153,433`), from the start of a swipe-*open* gesture until the next `pointerdown`/press-less click; the Viewport's swipe-to-dismiss never touches it. The outside-press gate should also be named in DLG-PTR-05/11 (it is evaluated first in `dialog/root/useDialogRoot.ts:43-45`), not only in §11. Resolved (QA 2026-10-14): dialog.md §4.2, DLG-PTR-05 and DLG-PTR-11 now name the gate and its only writer.
2. **DLG-DIS-10** states "Drawer mode changes nothing in this section except reporting itself in `nestedOpenDrawerCount`". Drawer's Root additionally adds the `close-watcher` dismissal (DRW-DIS-01) and the SwipeArea outside-press guard (DRW-DIS-02), and wraps `onOpenChange` (snap point reset, DRW-STA-04). These come from Drawer parts rather than from `useRenderDialogRoot`'s mode flag, so the sentence is true of the mode flag only; suggest rewording to "the mode flag changes nothing … (Drawer adds behavior through its own parts, see drawer.md)". Resolved (QA 2026-10-14): DLG-DIS-10 reworded.
3. **dialog.md §1** says the author "marked Drawer differences inline as 'drawer mode'"; only DLG-STA-05 and DLG-DIS-10 do. Drawer's own Popup/Backdrop/Viewport implementations differ from Dialog's (initial focus default, `--nested-dialogs`, `data-nested-dialog-open`, backdrop `pointer-events`) — not contradictions of dialog.md, but the overview's claim that Drawer differences are noted inline is incomplete; a pointer to drawer.md §3.17 would help. Resolved (QA 2026-10-14): dialog.md §1 now points to §3.17.

Amended 2026-10-20 (ADR 04 Amendments 2026-10-20, ADR 13 §3): the Root's `data-swiping` Writer cell in §3 changed from `C#` to `JS` — every Drawer part's `data-swiping` is JavaScript-written because `swiping` is a mirrored field; DRW-DOM-26's statement is unchanged.
