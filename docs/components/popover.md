# Popover

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/popover/` |
| Tests | `packages/react/src/popover/**/*.test.tsx` (13 files) + `root/PopoverRoot.spec.tsx`, `positioner/PopoverPositioner.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/popover/page.mdx` |
| Prefix | `POP` |
| Status | approved |
| Revised | 2026-10-29 |
| Builds on | none |
| Derived by | none (Menu, PreviewCard and Tooltip share the same popup store and floating mechanisms but have their own documents) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [button-behavior](../architecture/button-behavior.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Popover is an anchored, dismissible popup with `role="dialog"`. A **Trigger** button opens it on press (and optionally on hover); the **Popup** is positioned against the trigger by a **Positioner** rendered inside a **Portal** (appended to `<body>` by default). The popup is non-modal by default: the page stays interactive, focus moves into the popup on open (keyboard/mouse) and returns to the trigger on close, tabbing past either end of the popup closes it and continues the page's tab order, and an outside press, <kbd>Escape</kbd> or focus leaving closes it. With `modal`, an internal backdrop blocks outside pointer interaction, page scroll is locked, and — only when a **Close** part is rendered inside the Popup — focus is trapped and the rest of the document is hidden from assistive technology.

What distinguishes Popover from its siblings: Dialog is centered and always modal-capable; Tooltip and PreviewCard open on hover/focus only and are never pressable; Menu opens a `role="menu"` list with roving focus. Popover is the general-purpose anchored dialog. It supports several triggers for one popup, both *contained* (inside the Root) and *detached* (anywhere in the tree, linked by a handle from `Popover.createHandle()`), each trigger carrying a `payload` that the Root's function-as-child receives; a **Viewport** part animates the content swap when the popup moves between triggers. **Title** and **Description** label and describe the dialog automatically; **Arrow** renders a positioned pointer; **Backdrop** is an optional consumer-styled overlay.

## 2. Anatomy

```
<Popover.Root>                      required, 1            renders no element
  <Popover.Trigger>                 optional, 0..n         default element: button   (also allowed outside Root with `handle`)
  <Popover.Portal>                  required for a popup, 0..1   default element: div (portal node)
    <Popover.Backdrop>              optional, 0..1         default element: div
    <Popover.Positioner>            required inside Portal, 1    default element: div
      <Popover.Popup>               required inside Positioner, 1   default element: div
        <Popover.Arrow>             optional, 0..1         default element: div
        <Popover.Viewport>          optional, 0..1         default element: div
          <Popover.Title>           optional, 0..1 effective   default element: h2
          <Popover.Description>     optional, 0..1 effective   default element: p
          <Popover.Close>           optional, 0..n         default element: button
```

Backdrop, Title, Description and Close only require an ancestor Root (they read the Root's shared state); they are usually placed as shown. Arrow and Viewport require an ancestor Positioner. Close parts rendered *outside* the Popup still close the popover but do not count toward the modal focus trap (POP-STA-10). A Root rendered inside another Popover's Popup shares the outer Root's floating tree (nested popovers coordinate dismissal, §6.4).

- **POP-STR-01** — MUST. `Popover.Root` renders no element of its own; it owns the popup store (one per Root instance, kept for the Root's lifetime; PSH-STR-01) and provides it to every descendant part.
  Source: `popover/root/PopoverRoot.tsx:22-45,88-94,103-113`. Test: [L2] `PopoverRoot.test.tsx` "should render the children".
- **POP-STR-02** — MUST. A Root that is not inside another Popover Root creates a floating tree boundary; a Root inside another Popover Root joins the outer tree (so nested popovers are tree children for dismissal and focus purposes).
  Source: `popover/root/PopoverRoot.tsx:103-113`; `floating-ui-react/components/FloatingTree.tsx:90-95`; see [floating-and-positioning](../architecture/floating-and-positioning.md) FLT-STR-07. Test: [L4] `PopoverRoot.test.tsx` "should close child popover when clicking parent popover".
- **POP-STR-03** — MUST. `Popover.Trigger` renders a `<button>` by default. It resolves its store from its `handle` prop when given, otherwise from the nearest ancestor Root (PSH-STR-02); when neither exists it throws POP-EDGE-01.
  Source: `popover/trigger/PopoverTrigger.tsx:49-56,132`. Test: [L2] `PopoverTrigger.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`), "throws a descriptive error when rendered without a root or a handle".
- **POP-STR-04** — MUST. `Popover.Portal` renders a portal node `<div>` appended to the resolved container (default `<body>`) and places its children inside it; the Portal and everything below it exist only while the popup is mounted or `keepMounted` is set (POP-ANI-03).
  Source: `popover/portal/PopoverPortal.tsx:19-33`; `floating-ui-react/components/FloatingPortal.tsx:93-152,163-179,270`. Test: [L2] `PopoverPortal.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`). (L4 for the container-placement clause)
- **POP-STR-05** — MUST. `Popover.Positioner` renders a `<div>` and MUST be a descendant of a `Popover.Portal` (else POP-EDGE-03) and of a Root (else POP-EDGE-02).
  Source: `popover/positioner/PopoverPositioner.tsx:53-54,117-124`; `popover/portal/PopoverPortalContext.ts:6-12`. Test: [L2] `PopoverPositioner.test.tsx` describeConformance, "throws a descriptive error when rendered outside <Popover.Portal>".
- **POP-STR-06** — MUST. `Popover.Popup` renders a `<div>` and MUST be a descendant of a `Popover.Positioner` (else POP-EDGE-04) and of a Root (else POP-EDGE-02).
  Source: `popover/popup/PopoverPopup.tsx:33-35,84-105`; `popover/positioner/PopoverPositionerContext.ts:19-27`. Test: [L2] `PopoverPopup.test.tsx` describeConformance, "throws a descriptive error when rendered outside <Popover.Root>", "throws a descriptive error when rendered outside <Popover.Positioner>".
- **POP-STR-07** — MUST. `Popover.Arrow` renders a `<div>` and MUST be a descendant of a Positioner (it reads the arrow placement from the Positioner's shared state).
  Source: `popover/arrow/PopoverArrow.tsx:22-38`. Test: [L2] `PopoverArrow.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **POP-STR-08** — MUST. `Popover.Backdrop` renders a `<div>` and requires only an ancestor Root; it MAY be rendered anywhere under the Root (inside or outside the Portal).
  Source: `popover/backdrop/PopoverBackdrop.tsx:22-50`. Test: [L2] `PopoverBackdrop.test.tsx` describeConformance (rendered directly under `Popover.Root open`).
- **POP-STR-09** — MUST. `Popover.Viewport` renders a `<div>` and MUST be a descendant of a Positioner (it reads the resolved `side`); it wraps its children in a `<div data-current>` container (PSH-STR-07).
  Source: `popover/viewport/PopoverViewport.tsx:23-45`; `utils/usePopupViewport.tsx:229-268`. Test: [L2] `PopoverViewport.test.tsx` describeConformance, viewportConformanceTests "renders children in the `current` container by default".
- **POP-STR-10** — MUST. `Popover.Title` renders an `<h2>` and `Popover.Description` a `<p>`; both require an ancestor Root. When several are mounted, the most recently mounted one's id is the one referenced by the Popup (the store holds a single id per kind).
  Source: `popover/title/PopoverTitle.tsx:20-29`; `popover/description/PopoverDescription.tsx:20-29`. Test: [L2] `PopoverTitle.test.tsx` describeConformance (`refInstanceof: HTMLHeadingElement`), `PopoverDescription.test.tsx` describeConformance (`refInstanceof: HTMLParagraphElement`).
- **POP-STR-11** — MUST. `Popover.Close` renders a `<button>` and requires an ancestor Root; it renders (and works) while the popover is closed, and MAY be rendered outside the Popup.
  Source: `popover/close/PopoverClose.tsx:36-50`. Test: [L2] `PopoverClose.test.tsx` describeConformance (`button: true`), "renders when popover is closed".
- **POP-STR-12** — MUST. Any number of Triggers (contained, detached, or mixed) MAY target one Root; at most one is the *active* trigger at a time (PSH-STR-05, PSH-STR-06).
  Source: `popover/root/PopoverRoot.tsx:83-91`; `utils/popups/popupStoreUtils.ts:323-394`. Test: [L2] `PopoverRoot.test.tsx` describe.for "when using multiple detached triggers"; `PopoverRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "opens with any trigger on click and marks only that trigger as active".
- **POP-STR-13** — MUST. While `modal === true`, the popup is mounted and the last applied open reason is not `trigger-hover`, the Positioner renders the internal backdrop `<div>` (PSH-STR-09, PSH-DOM-14) as the sibling immediately before the positioner element; otherwise no such sibling exists.
  Source: `popover/positioner/PopoverPositioner.tsx:98,128-130`. Test: [L2] `PopoverRoot.test.tsx` "should render an internal backdrop when `true`", "should not render an internal backdrop when `false`", "enables modal behavior after a hover-open is clicked".
  Port note (2026-10-29, M4 track 5; design point 3 of M4 track 8, settled by the orchestrator 2026-10-28, PR 144): the Positioner renders the backdrop in `BuildBeforeElement` through the shared writer `Popup/PopupInternalBackdrop.Render` (id `<floating id>-backdrop`, `inert` while closing); the `clip-path` cut-out is the `popup` module's positioner role (POP-DOM-22's Writer row). L2: the three titles above; L4 port title for the `clip-path` clause: "port: a modal popover cuts its trigger out of the internal backdrop".
- **POP-STR-14** — MUST. A Trigger renders one focus guard `<span>` immediately before and one immediately after its element (PSH-STR-10, PSH-DOM-13) iff the popup is open by that trigger and focus-manager-modal mode (POP-STA-10) is off; toggling the guards MUST NOT replace the trigger's DOM element.
  Source: `popover/trigger/PopoverTrigger.tsx:80,122-125,153-170`; `popover/store/PopoverStore.ts:67-68`. Test: [L2] `PopoverRoot.test.tsx` "should keep trigger focus guards when `true` without a close part", "should only render focus guards inside the popup when `true`"; `PopoverRoot.detached-triggers.test.tsx` "renders focus guards around the active detached trigger and reports it on close", "when the popup moves to another trigger".
- **POP-STR-15** — MUST. While the focus manager is enabled (POP-FOC-01), two focus guards with `data-type="inside"` are rendered as siblings immediately before and after the Popup element inside the Positioner.
  Source: `popover/popup/PopoverPopup.tsx:107-125`; `floating-ui-react/components/FloatingFocusManager.tsx:1003-1053`. Test: [L2] `PopoverRoot.test.tsx` "should only render focus guards inside the popup when `true`" (`[data-base-ui-focus-guard][data-type="inside"]` count is 2).

## 3. Public API

### 3.1 `Popover.Root`

**Default element:** none. **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no (the Root has no element; `Popover.Root.State` is an empty object).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `open` | `boolean \| undefined` | `undefined` (uncontrolled) | Consumer-owned open state. | POP-API-01 |
| `defaultOpen` | `boolean` | `false` | Initial open state when `open` is not supplied. | POP-API-02 |
| `onOpenChange` | `(open: boolean, eventDetails: Popover.Root.ChangeEventDetails) => void` | `undefined` | Called before every open/close request is applied (§7). | POP-API-03 |
| `onOpenChangeComplete` | `(open: boolean) => void` | `undefined` | Called after the enter/exit animation finishes (§9). | POP-API-04 |
| `actionsRef` | `RefObject<{ unmount(): void; close(): void } \| null>` | `undefined` | Imperative actions (§9, §7). | POP-API-05 |
| `modal` | `boolean \| 'trap-focus'` | `false` | `true`: internal backdrop, scroll lock, focus trap (with a Close part); `'trap-focus'`: focus trap only; `false`: non-modal. | POP-API-06 |
| `triggerId` | `string \| null \| undefined` | `undefined` | Consumer-owned active-trigger id, for use with `open`. | POP-API-07 |
| `defaultTriggerId` | `string \| null \| undefined` | `null` | Initial active-trigger id, for use with `defaultOpen`. | POP-API-08 |
| `handle` | `Popover.Handle<Payload> \| undefined` | `undefined` | Binds the Root to a handle so detached triggers and imperative calls control it. | POP-API-09 |
| `children` | `ReactNode \| ({ payload }: { payload: Payload \| undefined }) => ReactNode` | — | Content, or a function receiving the active trigger's payload. | POP-API-10 |

**Consumed (not forwarded to any element):** all of the above (there is no element).

- **POP-API-01** — MUST. `open`, when not `undefined`, is the effective open state and wins over the component-owned state (PSH-STA-02); trigger presses and dismissals then only report through `onOpenChange` and change nothing until the consumer renders a new `open` value.
  Source: `popover/root/PopoverRoot.tsx:29,42,47`; `utils/popups/store.ts:150`; see [popup-store-and-handles](../architecture/popup-store-and-handles.md) PSH-STA-02. Test: [L2] `PopoverRoot.test.tsx` "should call onChange when the open state changes"; popupConformanceTests "opens the popup with the `open` prop", "is ignored when `open={true}` is passed, so Escape cannot close".
- **POP-API-02** — MUST. `defaultOpen` defaults to `false` and seeds the component-owned open state once at Root creation; a Root that starts open is also `mounted` from its first render (PSH-STA-01) and plays no enter transition (POP-ANI-04).
  Source: `popover/root/PopoverRoot.tsx:30,41`; `popover/store/PopoverStore.ts:226-228`. Test: [L2] `PopoverRoot.test.tsx` "remains uncontrolled, so a trigger click closes it"; popupConformanceTests "opens initially and remains uncontrolled", "is ignored when `open={false}` is passed".
- **POP-API-03** — MUST. `onOpenChange` is optional; when absent, requests are applied directly (uncontrolled) or ignored (controlled).
  Source: `popover/root/PopoverRoot.tsx:31,54`; `popover/store/PopoverStore.ts:139`. Test: [L2] `PopoverRoot.test.tsx` "should close when the anchor is clicked twice" (no callback).
- **POP-API-04** — MUST. `onOpenChangeComplete` is optional and is called with `true` after the open transition completes and `false` after the close completes, per §9 (POP-ANI-08).
  Source: `popover/root/PopoverRoot.tsx:32,55`; `popover/popup/PopoverPopup.tsx:56-64`; `utils/popups/popupStoreUtils.ts:586-595`. Test: [L2] popupConformanceTests "prop: onOpenChangeComplete" (5 tests).
- **POP-API-05** — MUST. `actionsRef.current` is `{ unmount, close }`: `close()` requests a close with reason `imperative-action` and no trigger (PSH-DIS-03); `unmount()` ends the closing phase per POP-ANI-05 (PSH-API-09).
  Source: `popover/root/PopoverRoot.tsx:74-81`. Test: [L2] `PopoverRoot.test.tsx` "prop: actionsRef" (9 tests).
- **POP-API-06** — MUST. `modal` defaults to `false` and accepts `true` or `'trap-focus'`; its effects are: internal backdrop (POP-STR-13) and scroll lock (POP-PTR-19) for `true` only; focus trap and outside `aria-hidden` for `true` and `'trap-focus'` only when a Close part is mounted inside the Popup (POP-STA-10, POP-FOC-08); outside-press mode `sloppy` for mouse with `'trap-focus'` (POP-DIS-01). The value is read every render (a change takes effect immediately).
  Source: `popover/root/PopoverRoot.tsx:33,64-66,241-248`; `popover/popup/PopoverPopup.tsx:71-72`; `popover/positioner/PopoverPositioner.tsx:98-105,128-130`. Test: [L2] `PopoverRoot.test.tsx` "prop: modal" (6 tests); `PopoverClose.test.tsx` "enables modal focus management when `modal=%s` and close is rendered", "does not enable modal focus management when close is not rendered". (L4 for the scroll-lock, focus-trap, `aria-hidden` and outside-press clauses)
- **POP-API-07** — MUST. `triggerId`, when not `undefined` (including `null`), is the effective active-trigger id and wins over the internally tracked one (PSH-STA-03); the trigger whose `id` equals it is marked active (`data-popup-open`, `aria-expanded="true"`) and is the positioning anchor.
  Source: `popover/root/PopoverRoot.tsx:35,44,48`; `utils/popups/store.ts:148`. Test: [L2] detachedTriggersConformanceTests "marks the trigger matching a controlled `triggerId` as active", "allows controlling the open state and active trigger programmatically"; `PopoverRoot.detached-triggers.test.tsx` "keeps positioning correct when conditional triggers unmount and the tree remounts".
- **POP-API-08** — MUST. `defaultTriggerId` defaults to `null` and seeds the internally tracked active-trigger id once; with `defaultOpen` it selects which trigger's payload the initially open popup renders.
  Source: `popover/root/PopoverRoot.tsx:36,43`. Test: [L2] detachedTriggersConformanceTests "opens initially with the payload of a non-first `defaultTriggerId`"; `PopoverClose.test.tsx` "reports no trigger when the active trigger id has no mounted trigger".
- **POP-API-09** — MUST. `handle` attaches the Root's store to the handle for the Root's mounted lifetime (PSH-STR-03, PSH-STR-04); detached Triggers with the same handle register with and read from this store; a Root with a handle keeps its interaction handlers mounted even while closed (PSH-STA-43).
  Source: `popover/root/PopoverRoot.tsx:34,86,90`; `utils/popups/popupStoreUtils.ts:103-115`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "root ownership" (3 tests), "opens by trigger from a descendant layout effect on initial mount".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): "opens by trigger from a descendant layout effect on initial mount" is restated as `PopoverRootTests` "port: opens by trigger from a descendant's first render on initial mount" (L2): a descendant of the Root calls the handle's `OpenAsync` in its first `OnAfterRenderAsync`, Blazor's counterpart of a layout effect, and no "no root" warning is written.
- **POP-API-10** — MUST. When `children` is a function it is called with `{ payload }` where `payload` is the store's current payload (`undefined` when none; PSH-API-07, PSH-STA-32); otherwise `children` is rendered as-is.
  Source: `popover/root/PopoverRoot.tsx:52,92`. Test: [L2] detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it"; `PopoverRoot.spec.tsx` (payload type).

#### State object (`Popover.Root.State`)

Empty (`{}`). Source: `popover/root/PopoverRoot.tsx:132`.

#### Data attributes / CSS variables

None (no element).

### 3.2 `Popover.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Popover.Trigger.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Whether the rendered element is a native `<button>` (UBT). | POP-API-11 |
| `disabled` | `boolean` | `false` | Disables press and hover opening; exposed in state. (Native attribute reused as a prop; listed in the outline only as a data attribute.) | POP-API-12 |
| `handle` | `Popover.Handle<Payload>` | `undefined` | Makes this a detached trigger. | POP-API-13 |
| `payload` | `Payload` | `undefined` | Value exposed to the Root's function-as-child while this trigger is active. | POP-API-14 |
| `id` | `string` | generated `base-ui-<token>` | Rendered `id` and the trigger id used by `triggerId`/`handle.open()`. | POP-API-15 |
| `openOnHover` | `boolean` | `false` | Also open on mouse hover. | POP-API-16 |
| `delay` | `number` | `300` | Hover rest delay in ms (requires `openOnHover`). | POP-API-17 |
| `closeDelay` | `number` | `0` | Hover close delay in ms (requires `openOnHover`). | POP-API-18 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |
| any other `<button>` attribute / handler | | — | Forwarded; handlers are merged before the button mechanism's (RND). | — |

**Consumed (not forwarded to the element):** `nativeButton`, `handle`, `payload`, `openOnHover`, `delay`, `closeDelay`, `disabled` (re-emitted per UBT), `id` (re-emitted as the trigger id).

- **POP-API-11** — MUST. `nativeButton` defaults to `true` and is passed as the button mechanism's `native` option (UBT-API-02): `true` emits `type="button"`, `false` emits `role="button"` and keyboard activation emulation (UBT-KEY-01/02).
  Source: `popover/trigger/PopoverTrigger.tsx:39,107-110`; `internals/use-button/useButton.ts`; see [button-behavior](../architecture/button-behavior.md). Test: [L2] `PopoverTrigger.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`), "custom element", "should toggle closed with Enter or Space when rendering a <div>". (L4 for the keyboard-activation clause)
- **POP-API-12** — MUST. `disabled` defaults to `false`. When `true`: the press interaction is blocked by the button mechanism (native `disabled` attribute, or `aria-disabled="true"` + `tabindex="-1"` for non-native; UBT-DOM-04/06, UBT-ARIA-02), hover opening is disabled (POP-PTR-08), `data-disabled` is rendered (POP-DOM-03), and the value is forwarded to the store as `disabled` while this trigger is the active mounted trigger (POP-STA-05), which also disables the Popup's hover-close interaction.
  Source: `popover/trigger/PopoverTrigger.tsx:38,70-76,83,107-110,127-130`; `popover/popup/PopoverPopup.tsx:52,66`. Test: [L2] `PopoverTrigger.test.tsx` "disables the popover", "custom element", "does not open on hover when disabled". (L4 for the hover clauses)
- **POP-API-13** — MUST. `handle` (default `undefined`) makes the Trigger read from the handle's current store (its inert fallback store while no Root is attached, the attached Root's store afterwards; PSH-STA-34..37) and takes precedence over an ancestor Root (PSH-STR-02).
  Source: `popover/trigger/PopoverTrigger.tsx:40,49-51`; `utils/popups/usePopupHandleStore.ts:17-36`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "keeps registration on the attached store when a stale-ref component swaps its host node", "does not detach the consumer ref when the handle attaches to a root", detachedTriggersConformanceTests "registers a detached trigger declared after the root".
- **POP-API-14** — MUST. `payload` (default `undefined`) is written to the store's `payload` when this trigger becomes, or registers as, the active trigger while the popup is open, and whenever it changes while this trigger owns the mounted popup (PSH-STA-28..30); the store keeps the last payload after close (PSH-STA-32).
  Source: `popover/trigger/PopoverTrigger.tsx:41,66-76`; `utils/popups/popupStoreUtils.ts:323-394`. Test: [L2] detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it"; `PopoverTrigger.test.tsx` "hands ownership to a hovered sibling trigger when opened by mouse".
- **POP-API-15** — MUST. `id`, when given, is both the rendered `id` attribute and the trigger id; when absent a generated `base-ui-<token>` id (RND-DOM-27..29) serves both purposes (PSH-API-08).
  Source: `popover/trigger/PopoverTrigger.tsx:45,58,142`; `internals/useBaseUiId.ts:9-11`. Test: [L2] `PopoverClose.test.tsx` "keeps the trigger when closing with a tooltip trigger close button" (`trigger.id === 'trigger-1'`); popupConformanceTests "allows a custom `id` prop" (popup id).
- **POP-API-16** — MUST. `openOnHover` defaults to `false`. When `true` the trigger also opens on mouse hover (POP-PTR-06) and the popup closes on hover-out (POP-PTR-07/12); the value is forwarded to the store while this trigger owns the mounted popup so the Popup enables its own hover handlers.
  Source: `popover/trigger/PopoverTrigger.tsx:42,70-76,82-94`; `popover/popup/PopoverPopup.tsx:53,66`. Test: [L4] `PopoverRoot.test.tsx` "closes after hovering out of a popup opened by its trigger"; `PopoverTrigger.test.tsx` "should have the data-popup-open but not the data-pressed attribute when open by hover".
- **POP-API-17** — MUST. `delay` defaults to `300` (`OPEN_DELAY`) and is passed as the hover interaction's `restMs` (not as an open delay): with a non-zero value the popup opens once the mouse has rested over the trigger for `delay` ms (POP-PTR-06); with `0` it opens immediately on `mouseenter`.
  Source: `popover/trigger/PopoverTrigger.tsx:43,87`; `popover/utils/constants.ts:1`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:224-300,426-497`. Test: [L4] `PopoverRoot.test.tsx` "should open after delay with rest type by default"; `PopoverTrigger.test.tsx` "sticks when clicked before the hover delay completes".
- **POP-API-18** — MUST. `closeDelay` defaults to `0` and is passed as the hover interaction's close delay on the trigger and, while this trigger owns the mounted popup, to the Popup's hover-close interaction (POP-STA-05); a hover-out close is deferred by that many ms (POP-PTR-07).
  Source: `popover/trigger/PopoverTrigger.tsx:44,70-76,88-90`; `popover/popup/PopoverPopup.tsx:54,66`. Test: [L4] `PopoverRoot.test.tsx` "should close after delay".

#### State object (`Popover.Trigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | The trigger's `disabled` prop. |
| `open` | `boolean` | `true` iff the popup is open **and** this trigger is the active trigger (PSH-STA-08). |

Source: `popover/trigger/PopoverTrigger.tsx:127-130,179-188`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open` | `state.open === true`; empty value | POP-DOM-01 |
| `data-pressed` | `state.open === true` and the last applied open reason is `trigger-press` | POP-DOM-02 |
| `data-disabled` | `disabled === true` | POP-DOM-03 |

#### CSS variables

None.

### 3.3 `Popover.Portal`

**Default element:** `<div>` (the portal node). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Popover.Portal.State`, empty).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the portal subtree in the DOM while closed. | POP-API-19 |
| `container` | `HTMLElement \| ShadowRoot \| RefObject<HTMLElement \| ShadowRoot \| null> \| null` | `undefined` (→ `<body>`) | Parent element for the portal node. | POP-API-20 |
| `className`, `style`, `render`, other `<div>` attributes | | — | Applied to the portal node. | — |

**Consumed:** `keepMounted`, `container`.

- **POP-API-19** — MUST. `keepMounted` defaults to `false`. When `false` the Portal renders nothing while the popup is not mounted; when `true` the portal node, Positioner and Popup stay in the DOM while closed, with the Positioner carrying `hidden` (POP-DOM-13) and `pointer-events: none` (POP-DOM-14). The value is also passed to the positioning layer so anchor tracking keeps running while kept mounted (FLT-API-17).
  Source: `popover/portal/PopoverPortal.tsx:19-33`; `popover/positioner/PopoverPositioner.tsx:54,83`. Test: [L2] `PopoverRoot.test.tsx` "focuses the trigger after the popover is closed but not unmounted"; `PopoverPositioner.test.tsx` "remains anchored if keepMounted=true"; `PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted".
  Re-tag (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); ADR 13 §3): the three cited titles observe focus return, layout and an animation, so they are [L4]; they run in `popover.spec.ts`, both modes.
- **POP-API-20** — MUST. `container` resolves per FLT-API-13: `undefined` → the nearest enclosing portal node, else `<body>`; an element or shadow root → that node; a DOM handle → its current value; `null` → no portal node (nothing rendered).
  Source: `popover/portal/PopoverPortal.tsx:47-48`; `floating-ui-react/components/FloatingPortal.tsx:93-123`. Test: [L4] `PopoverRoot.test.tsx` "closing via outside press: works when clicking another element inside the same shadow root", "keeps the popover open when a nested menu opens via Enter using a shared container".
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Menu "keeps the popover open when a nested menu opens via Enter using a shared container"); the row's other titles run in `popover.spec.ts`, both modes.

#### State object (`Popover.Portal.State`)

Empty (`{}`). Source: `popover/portal/PopoverPortal.tsx:36`.

#### Data attributes / CSS variables

None of Popover's own (the portal node carries `data-base-ui-portal=""`, POP-DOM-09).

### 3.4 `Popover.Positioner`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Popover.Positioner.State`).

#### Props

All positioning props are the shared Positioner props of [floating-and-positioning](../architecture/floating-and-positioning.md) §3.1; only Popover's defaults are listed.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `anchor` | `Element \| VirtualElement \| (() => Element \| VirtualElement \| null) \| RefObject \| null` | active trigger element | Element to position against. | POP-POS-03, POP-POS-04 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | CSS position of the positioner. | POP-POS-01 |
| `side` | `'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-start' \| 'inline-end'` | `'bottom'` | Preferred side. | POP-POS-01 |
| `sideOffset` | `number \| OffsetFunction` | `0` | Gap to the anchor. | POP-POS-01 |
| `align` | `'start' \| 'center' \| 'end'` | `'center'` | Alignment along the side. | POP-POS-01 |
| `alignOffset` | `number \| OffsetFunction` | `0` | Shift along the align axis. | POP-POS-01 |
| `collisionBoundary` | `'clipping-ancestors' \| Element \| Element[] \| Rect` | `'clipping-ancestors'` | Boundary for collision handling. | POP-POS-01 |
| `collisionPadding` | `number \| Padding` | `5` | Padding from the boundary. | POP-POS-01 |
| `collisionAvoidance` | `{ side?, align?, fallbackAxisSide? }` | `{ fallbackAxisSide: 'end' }` | Collision strategy (`side`/`align` default to `'flip'`, FLT-API-10). | POP-POS-02 |
| `sticky` | `boolean` | `false` | Keep in view while anchor scrolls out. | POP-POS-01 |
| `arrowPadding` | `number` | `5` | Minimum arrow distance from the popup corners. | POP-POS-01 |
| `disableAnchorTracking` | `boolean` | `false` | Stop following the anchor on scroll/resize/layout shift. | POP-POS-05 |
| `className`, `style`, `render`, other `<div>` attributes | | — | Forwarded. Consumer `style` is merged over the positioning styles (RND-DOM-16). | — |

**Consumed:** every positioning prop above. `keepMounted` is **not** a Positioner prop (POP-EDGE-13; it lives on the Portal).

#### State object (`Popover.Positioner.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Resolved side after collision handling (logical: `inline-start`/`inline-end` are preserved when requested). |
| `align` | `Align` | Resolved alignment. |
| `anchorHidden` | `boolean` | The anchor is fully clipped/hidden (FLT hide middleware). |
| `instant` | `'dismiss' \| 'click' \| 'focus' \| 'trigger-change' \| undefined` | The store's public instant type (POP-STA-07). Declared as `string \| undefined`. |

Source: `popover/positioner/PopoverPositioner.tsx:109-115,136-157`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / closed (exactly one) | POP-DOM-15 |
| `data-side` | resolved side: `top`, `bottom`, `left`, `right`, `inline-end`, `inline-start` | POP-DOM-16 |
| `data-align` | resolved align: `start`, `center`, `end` | POP-DOM-16 |
| `data-anchor-hidden` | `anchorHidden === true` | POP-DOM-17 |
| `data-instant` | `instant` string value (not in the outline, §16 Q4) | POP-DOM-18 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--anchor-width` / `--anchor-height` | Positioner inline style | anchor size in `px` | POP-DOM-20 |
| `--available-width` / `--available-height` | Positioner inline style | space to the viewport edge in `px` (or `max-content` during auto-resize measurement, PSH-ANI-12) | POP-DOM-20 |
| `--transform-origin` | Positioner inline style | origin on the anchor/arrow side (POP-POS-07) | POP-DOM-20 |
| `--positioner-width` / `--positioner-height` | Positioner inline style | measured popup size in `px`, only while a Viewport is mounted (PSH-DOM-11) | POP-DOM-20 |

### 3.5 `Popover.Popup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Popover.Popup.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `initialFocus` | `boolean \| RefObject<HTMLElement \| null> \| ((openType: InteractionType) => void \| boolean \| HTMLElement \| null)` | `undefined` | Element to focus on open. | POP-API-21 |
| `finalFocus` | `boolean \| RefObject<HTMLElement \| null> \| ((closeType: InteractionType) => void \| boolean \| HTMLElement \| null)` | `undefined` | Element to focus on close. | POP-API-22 |
| `className`, `style`, `render`, other `<div>` attributes / handlers | | — | Forwarded; a consumer `id` replaces the generated popup id (PSH-ARIA-03); consumer `role`/`tabIndex` override the defaults (RND-DOM-19). | — |

**Consumed:** `initialFocus`, `finalFocus`.

- **POP-API-21** — MUST. `initialFocus` defaults to `undefined`, which resolves to "the popup element itself when the open interaction type is `touch`, otherwise the default target (first tabbable descendant, else the popup)" (PSH-FOC-01); other values resolve per PSH-FOC-02/03 (`InteractionType` = `'mouse' | 'touch' | 'pen' | 'keyboard' | ''`).
  Source: `popover/popup/PopoverPopup.tsx:31,68-69,113,151-167`; `utils/popups/popupStoreUtils.ts:37-40`. Test: [L4] `PopoverPopup.test.tsx` popupFocusPropsTests "prop: initialFocus" (6 tests).
- **POP-API-22** — MUST. `finalFocus` defaults to `undefined`, which resolves to the focus manager's default return target (the active trigger element, or the element focused before a programmatic open; PSH-FOC-04, POP-FOC-05); other values resolve per PSH-FOC-04/05.
  Source: `popover/popup/PopoverPopup.tsx:31,114,168-181`. Test: [L4] `PopoverPopup.test.tsx` popupFocusPropsTests "prop: finalFocus" (5 tests).

#### State object (`Popover.Popup.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Resolved side (from the Positioner). |
| `align` | `Align` | Resolved alignment (from the Positioner). |
| `instant` | `'dismiss' \| 'click' \| 'focus' \| 'trigger-change' \| undefined` | Public instant type (POP-STA-07). |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | Derived transition status (POP-ANI-02). |

Source: `popover/popup/PopoverPopup.tsx:76-82,128-149`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / closed (exactly one) | POP-DOM-25 |
| `data-starting-style` / `data-ending-style` | `transitionStatus` `'starting'` / `'ending'` | POP-DOM-26 |
| `data-side` / `data-align` | resolved placement | POP-DOM-27 |
| `data-instant` | `instant` value: `click`, `dismiss`, `focus` or `trigger-change` | POP-DOM-28 |

The Popup state has no `anchorHidden` field, so `data-anchor-hidden` is never rendered on the Popup even though the mapping supports it.

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--popup-width` / `--popup-height` | Popup inline style, only while a Viewport is mounted | `auto`, or the previous/new content size in `px` during a content switch (PSH-DOM-11) | POP-DOM-30 |

### 3.6 `Popover.Arrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Popover.Arrow.State`). Consumer `style` is merged over the arrow position styles.

#### Props

Only `className`, `style`, `render` and native `<div>` attributes (forwarded; a consumer `aria-hidden` overrides POP-ARIA-07).

#### State object (`Popover.Arrow.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Resolved side. |
| `align` | `Align` | Resolved alignment. |
| `uncentered` | `boolean` | The arrow could not be centered on the anchor (FLT arrow `centerOffset !== 0`). |

Source: `popover/arrow/PopoverArrow.tsx:26-31,43-60`; `internals/useAnchorPositioning.ts:625`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / closed | POP-DOM-32 |
| `data-side` / `data-align` | resolved placement | POP-DOM-32 |
| `data-uncentered` | `uncentered === true` | POP-DOM-32 |

#### CSS variables

None.

### 3.7 `Popover.Backdrop`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Popover.Backdrop.State`). Consumer `style` is merged over the part's inline styles (POP-DOM-35).

#### Props

Only `className`, `style`, `render` and native `<div>` attributes.

#### State object (`Popover.Backdrop.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `transitionStatus` | `TransitionStatus` | Derived transition status (POP-ANI-02). |

Source: `popover/backdrop/PopoverBackdrop.tsx:29-32,55-64`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | open / closed | POP-DOM-36 |
| `data-starting-style` / `data-ending-style` | transition status | POP-DOM-36 |

#### CSS variables

None.

### 3.8 `Popover.Viewport`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Popover.Viewport.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | — | Content rendered inside the `[data-current]` container. | POP-API-23 |
| `className`, `style`, `render`, other `<div>` attributes | | — | Forwarded to the viewport element. | — |

- **POP-API-23** — MUST. `children` is rendered inside a `<div data-current>` child of the Viewport (never directly); during a content transition a `<div data-previous>` sibling holding a clone of the outgoing content precedes it (PSH-STR-07, PSH-DOM-09/10).
  Source: `popover/viewport/PopoverViewport.tsx:21,28-32,43`; `utils/usePopupViewport.tsx:229-268`. Test: [L2] viewportConformanceTests "renders children in the `current` container by default", "creates morphing containers during transitions".

#### State object (`Popover.Viewport.State`)

| Field | Type | Meaning |
|---|---|---|
| `activationDirection` | `string \| undefined` | Direction from the previous to the new trigger (PSH-DOM-07). |
| `transitioning` | `boolean` | A `[data-previous]` container is rendered. |
| `instant` | `'dismiss' \| 'click' \| 'focus' \| 'trigger-change' \| undefined` | Public instant type (POP-STA-07). |

Source: `popover/viewport/PopoverViewport.tsx:34-38,48-61`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-activation-direction` | while a direction exists; `"<left\|right\|> <down\|up\|>"` | POP-DOM-38 |
| `data-transitioning` | `transitioning === true` | POP-DOM-39 |
| `data-instant` | `instant` value | POP-DOM-40 |
| `data-current` / `data-previous` | on the child containers, not the Viewport element | POP-DOM-37 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--popup-width` / `--popup-height` | the `[data-previous]` container's inline style | popup size when the previous content was rendered, in `px` | POP-DOM-41 |

### 3.9 `Popover.Title`

**Default element:** `<h2>`. **Accepts `render` element substitution:** yes (typed for `h1`–`h6`). **Accepts `className`/`style` as function of state:** yes (state empty).

Props: `id` (optional; otherwise generated), `className`, `style`, `render`, native heading attributes. State: `{}`. Data attributes / CSS variables: none. Source: `popover/title/PopoverTitle.tsx:18-44`.

### 3.10 `Popover.Description`

**Default element:** `<p>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state empty).

Props: `id` (optional; otherwise generated), `className`, `style`, `render`, native `<p>` attributes. State: `{}`. Data attributes / CSS variables: none. Source: `popover/description/PopoverDescription.tsx:18-44`.

### 3.11 `Popover.Close`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state empty).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Native `<button>` vs. emulated (UBT). | POP-API-24 |
| `disabled` | `boolean` | `false` | Disables the button (native attribute reused as a prop; not in the outline, §16 Q5). | POP-API-25 |
| `className`, `style`, `render`, other `<button>` attributes / handlers | | — | Forwarded; consumer `onClick` runs before the close (RND handler order). | — |

- **POP-API-24** — MUST. `nativeButton` defaults to `true` and maps to the button mechanism's `native` option; `focusableWhenDisabled` is fixed to `false`.
  Source: `popover/close/PopoverClose.tsx:26,30-34`. Test: [L2] `PopoverClose.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`).
- **POP-API-25** — MUST. `disabled` defaults to `false`; when `true` the close button is disabled per UBT (native `disabled` attribute; `aria-disabled` + `tabindex="-1"` when non-native) and clicks do not close.
  Source: `popover/close/PopoverClose.tsx:25,30-34`. Test: none.
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven by `PopoverPartsTests` "port: a disabled close button does not close" (L2).

State: `{}`. Data attributes / CSS variables: none (the Close has no state-derived attributes). Source: `popover/close/PopoverClose.tsx:55-63`.

### 3.12 `Popover.createHandle()` and `Popover.Handle`

- **POP-API-26** — MUST. `Popover.createHandle<Payload>()` returns a new independent `Popover.Handle` (PSH-API-01) whose `open(triggerId: string)` opens the attached Root associated with the registered trigger of that id (reason `imperative-action`, `trigger` = that element; PSH-STA-38), `close()` closes it (PSH-DIS-03), and `isOpen` reads the attached Root's effective open state (`false` when none; PSH-API-05). `open()` with an id that matches no registered trigger throws POP-EDGE-05; calls while no Root is attached are ignored with the dev warnings of POP-EDGE-06.
  Source: `popover/store/PopoverHandle.ts:13-55`; `utils/popups/popupHandle.ts:213-285`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests "opens by trigger id with its payload and closes", "throws when opened with an unregistered trigger id", "ignores imperative calls while no root is attached and follows the root across remounts", "opens by trigger from a descendant layout effect on initial mount".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): see POP-API-09's note; the same port title proves the handle's `OpenAsync` from a descendant's first render.

### 3.13 Shared types

- `Popover.Root.ChangeEventReason` = `'trigger-hover' | 'trigger-focus' | 'trigger-press' | 'outside-press' | 'escape-key' | 'close-press' | 'focus-out' | 'imperative-action' | 'none'`. `trigger-focus` and `none` are declared but never emitted in v1.9.0 (POP-EVT-10). Source: `popover/root/PopoverRoot.tsx:208-217`; `internals/reason-parts.ts`.
- `Popover.Root.ChangeEventDetails` = the common change-event details (EVD-API-01) specialized to those reasons, plus `preventUnmountOnClose(): void` (EVD-EVT-21). Source: `popover/root/PopoverRoot.tsx:218-222`.
- `Popover.Root.Actions` = `{ unmount(): void; close(): void }`. Source: `popover/root/PopoverRoot.tsx:203-206`.
- `Popover.Root.Props<Payload>`, `Popover.Root.State`, `Popover.Trigger.Props<Payload>`/`State`, `Popover.Portal.Props`/`State`, `Popover.Positioner.Props`/`State`, `Popover.Popup.Props`/`State`, `Popover.Arrow.Props`/`State`, `Popover.Backdrop.Props`/`State`, `Popover.Title.Props`/`State`, `Popover.Description.Props`/`State`, `Popover.Close.Props`/`State`, `Popover.Viewport.Props`/`State`, `Popover.Handle<Payload>`; flat aliases (`PopoverRootProps`, …) and attribute-name namespaces `PopoverTriggerDataAttributes`, `PopoverPositionerCssVariables`, `PopoverPositionerDataAttributes`, `PopoverPopupCssVariables`, `PopoverPopupDataAttributes`, `PopoverArrowDataAttributes`, `PopoverBackdropDataAttributes`, `PopoverViewportCssVariables`, `PopoverViewportDataAttributes`. Source: `popover/index.ts:1-32`. Test: [L1] `PopoverRoot.spec.tsx`, `PopoverPositioner.spec.tsx` (type assertions only).
- `Side` = `'top' | 'bottom' | 'left' | 'right' | 'inline-end' | 'inline-start'`; `Align` = `'start' | 'center' | 'end'`; `TransitionStatus` = `'starting' | 'ending' | 'idle' | undefined`; `InteractionType` = `'mouse' | 'touch' | 'pen' | 'keyboard' | ''`. Source: `internals/useAnchorPositioning.ts:66-67`; `internals/useTransitionStatus.ts`; `packages/utils/src/useEnhancedClickHandler.ts:4`.

## 4. State model

Popover's state lives in one popup store per Root ([popup-store-and-handles](../architecture/popup-store-and-handles.md) §4.3 defines the shared fields: `open`/`openProp`, `mounted`, `transitionStatus`, `activeTriggerId`/`triggerIdProp`, `activeTriggerElement`, `payload`, `openedWithoutTrigger`, `triggerCount`, `preventUnmountingOnClose`, `popupElement`, `positionerElement`, the three interaction prop bags and the floating root state). This section states the Popover-specific fields and selectors and the ownership rules Popover relies on.

### 4.1 Open state and active trigger

- **POP-STA-01** — MUST. The effective open state is `open` (consumer-owned) when it is not `undefined`, otherwise the component-owned value seeded from `defaultOpen` (PSH-STA-02). Every reader of "open" in this document (`data-open`, `aria-expanded`, `state.open`, positioning, dismissal) uses the effective value.
  Source: `popover/root/PopoverRoot.tsx:39-50`; `utils/popups/store.ts:150,178`. Test: [L2] popupConformanceTests "opens the popup with the `open` prop", "opens initially and remains uncontrolled".
- **POP-STA-02** — MUST. The effective active-trigger id is `triggerId` when not `undefined` (a controlled `null` means "no active trigger"), otherwise the internally tracked id seeded from `defaultTriggerId` and updated by every applied open request that carries a trigger (PSH-STA-03, PSH-STA-13).
  Source: `popover/root/PopoverRoot.tsx:35-36,43-44,48`; `utils/popups/store.ts:148`; `utils/popups/popupStoreUtils.ts:203-212`. Test: [L2] detachedTriggersConformanceTests "marks the trigger matching a controlled `triggerId` as active"; `PopoverClose.test.tsx` "falls back to the active trigger element when the active trigger id is unregistered".
- **POP-STA-03** — MUST. A close request without a trigger leaves the active trigger id and element unchanged (so exit animations and focus return still target the trigger); an open request always (re)sets them to the request's trigger or `null` (PSH-STA-13).
  Source: `utils/popups/popupStoreUtils.ts:207-212`. Test: [L2] `PopoverPositioner.test.tsx` "remains anchored to the trigger when closing from a tooltip trigger close"; `PopoverClose.test.tsx` "keeps the trigger when closing with a tooltip trigger close button".
- **POP-STA-04** — MUST. Lone-trigger claim: when the popup is effectively open with no active trigger resolved and exactly one trigger is registered (and the open did not deliberately omit a trigger), that trigger becomes the active trigger post-commit, pre-paint (PSH-STA-18/19); Popover does **not** close when its active trigger unregisters (PSH-STA-22; `closeOnActiveTriggerUnmount` is off).
  Source: `popover/root/PopoverRoot.tsx:58`; `utils/popups/popupStoreUtils.ts:423-555`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "returns focus to the previous element when the trigger unmounts while open"; popupConformanceTests "has the `aria-controls` attribute on the trigger" (single trigger, controlled `open`).

### 4.2 Popover-specific store fields

| Field | Type | Initial | Written by |
|---|---|---|---|
| `disabled` | `boolean` | `false` | active trigger's `disabled` (POP-STA-05) |
| `openOnHover` | `boolean` | `false` | active trigger's `openOnHover` (POP-STA-05) |
| `closeDelay` | `number` | `0` | active trigger's `closeDelay` (POP-STA-05) |
| `modal` | `boolean \| 'trap-focus'` | `false` | Root `modal` prop, every render |
| `focusManagerModal` | `boolean` | `false` | Popup (POP-STA-10) |
| `openChangeReason` | `ChangeEventReason \| null` | `null` | every applied open change (POP-STA-06) |
| `instantType` | `'dismiss' \| 'click' \| 'focus' \| 'trigger-change' \| undefined` | `undefined` | every applied open change (POP-STA-07); trigger switch (POP-ANI-07) |
| `openMethod` | `InteractionType \| null` | `null` | trigger activation (POP-STA-08) |
| `titleElementId`, `descriptionElementId` | `string \| undefined` | `undefined` | Title / Description (POP-STA-09) |
| `adaptiveOrigin` | middleware \| `undefined` | `undefined` | Viewport mount/unmount (PSH-POS-02) |

Non-reactive context (not state): `stickIfOpen` (initially `true`) and its 500 ms timeout (POP-STA-11), the popup DOM handle, and the three focus-guard DOM handles (`beforeTriggerFocusGuardRef`, `triggerFocusTargetRef`, `beforeContentFocusGuardRef`).

Source: `popover/store/PopoverStore.ts:22-44,204-245`.

- **POP-STA-05** — MUST. While a Trigger owns the mounted popup (`isMountedByTrigger(ownId)`), its `payload`, `disabled`, `openOnHover` and `closeDelay` are written to the store post-commit, pre-paint whenever any of them changes, and when the trigger registers while already active (PSH-STA-28..30); the Popup reads `openOnHover`, `disabled` and `closeDelay` from the store, never from a Trigger directly.
  Source: `popover/trigger/PopoverTrigger.tsx:66-76`; `utils/popups/popupStoreUtils.ts:339-394`; `popover/popup/PopoverPopup.tsx:52-54,66`. Test: [L2] `PopoverRoot.test.tsx` "closes after hovering out of a popup opened by its trigger"; detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it".
- **POP-STA-06** — MUST. `openChangeReason` is set to the reason of every applied (non-canceled) open change, including switches between triggers, and is reset to `null` when the popup unmounts (POP-ANI-01). It drives `data-pressed` (POP-DOM-02), the hover-opened branches (POP-STR-13, POP-DOM-35, POP-FOC-01, POP-PTR-19) and `isTouchPressOpen` (POP-STA-12).
  Source: `popover/store/PopoverStore.ts:59,147-159`; `popover/root/PopoverRoot.tsx:59-62`. Test: [L2] `PopoverTrigger.test.tsx` "should have the data-popup-open but not the data-pressed attribute when open by hover"; `PopoverBackdrop.test.tsx` "sets `pointer-events: none` style on backdrop if opened by hover".
- **POP-STA-07** — MUST. After every applied open change, `instantType` is set to: `'click'` when the reason is `trigger-press` and the event's `detail` is `0` (keyboard or assistive-technology activation; open or close); otherwise `'dismiss'` when closing with reason `escape-key` (or a null reason); otherwise `'focus'` when the reason is `focus-out`; otherwise `undefined` (this includes pointer presses, hover, outside press, close press and imperative actions). Separately, a trigger switch sets `'trigger-change'` per POP-ANI-07. The *public* value (state objects, `data-instant`) reads `undefined` instead of `'trigger-change'` whenever the popup is closed (TRN-ANI-29).
  Source: `popover/store/PopoverStore.ts:54-57,114-119,174-182`; see EVD-EVT-17. Test: [L1] `PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a prop-driven close after a switch"; `PopoverTrigger.test.tsx` "should toggle closed with Enter or Space when rendering a <div>" (keyboard path).
- **POP-STA-08** — MUST. `openMethod` records the interaction type of the trigger activation that opened the popup (PSH-PTR-01/02: `mouse`, `touch`, `pen`, `keyboard`, or `''`; `''` becomes `touch` on iOS), is written only while the popup is closed at the time of the activation, and is reset to `null` post-commit, pre-paint whenever the effective open state becomes `false` and when the Root unmounts (PSH-STA-41).
  Source: `popover/trigger/PopoverTrigger.tsx:98-103`; `utils/useOpenInteractionType.ts:9-38`; `popover/root/PopoverRoot.tsx:57`; `utils/popups/popupStoreUtils.ts:633-652`. Test: [L2] `PopoverRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width"; `PopoverTrigger.test.tsx` "keeps ownership on the tapped trigger when a sibling trigger is hovered".
- **POP-STA-09** — MUST. A mounted Title / Description writes its resolved id (consumer `id` or generated) to `titleElementId` / `descriptionElementId` post-commit, pre-paint, updates it when the id changes, and clears it to `undefined` when it unmounts.
  Source: `popover/title/PopoverTitle.tsx:22-24`; `popover/description/PopoverDescription.tsx:22-24`. Test: [L2] `PopoverTitle.test.tsx` "labels the popup element with its id"; `PopoverDescription.test.tsx` "describes the popup element with its id".
- **POP-STA-10** — MUST. `focusManagerModal` is `true` iff `modal !== false` **and** at least one `Popover.Close` is mounted inside the Popup (PSH-FOC-08); the Popup writes it to the store every render so the Trigger's focus-guard decision (POP-STR-14) stays consistent with the Popup's focus trap (POP-FOC-08).
  Source: `popover/popup/PopoverPopup.tsx:37,71-72,123`; `utils/closePart.tsx:12-37`. Test: [L2] `PopoverClose.test.tsx` "enables modal focus management when `modal=%s` and close is rendered", "does not enable modal focus management when close is not rendered"; `PopoverRoot.test.tsx` "should keep trigger focus guards when `true` without a close part".
- **POP-STA-11** — MUST. The *stick-if-open* flag starts `true`; a hover-reason open change sets it `true` and arms a 500 ms (`PATIENT_CLICK_THRESHOLD`) timer that resets it to `false`; the timer is cancelled whenever the effective open state becomes `false`; the flag is set back to `true` when the popup unmounts. The flag is read only when a trigger is pressed (POP-PTR-04).
  Source: `popover/store/PopoverStore.ts:41-43,161-168,242`; `popover/root/PopoverRoot.tsx:59-62,68-72,127`; `internals/constants.ts:4`. Test: [L1] `PopoverTrigger.test.tsx` "closes the popover if the user clicks patiently", "sticks if the user clicks impatiently"; `PopoverRoot.detached-triggers.test.tsx` "keeps the popup open when the last hovered trigger is pressed".
- **POP-STA-12** — MUST. Derived selectors used by Triggers: `isPressOpenedByTrigger(id)` = `openChangeReason === 'trigger-press'` and `isOpenedByTrigger(id)`; `isTouchPressOpen(openOnHover)` = `openOnHover && openMethod === 'touch' && openChangeReason === 'trigger-press'`; `hasTriggerFocusGuards(id)` = `isOpenedByTrigger(id) && !focusManagerModal`.
  Source: `popover/store/PopoverStore.ts:60-68`. Test: [L1] `PopoverTrigger.test.tsx` "style hooks" (4 tests), "keeps ownership on the tapped trigger when a sibling trigger is hovered"; `PopoverRoot.test.tsx` "should only render focus guards inside the popup when `true`".

### 4.3 Part state objects

- **POP-STA-13** — MUST. The state objects exposed to `className`/`style`/`render` are exactly: Trigger `{ disabled, open }` (POP-STA-14); Positioner `{ open, side, align, anchorHidden, instant }`; Popup `{ open, side, align, instant, transitionStatus }`; Arrow `{ open, side, align, uncentered }`; Backdrop `{ open, transitionStatus }`; Viewport `{ activationDirection, transitioning, instant }`; Root, Portal, Title, Description and Close `{}`. Server: relaxed, see §9.
  Source: `popover/trigger/PopoverTrigger.tsx:127-130`; `popover/positioner/PopoverPositioner.tsx:109-115`; `popover/popup/PopoverPopup.tsx:76-82`; `popover/arrow/PopoverArrow.tsx:26-31`; `popover/backdrop/PopoverBackdrop.tsx:29-32`; `popover/viewport/PopoverViewport.tsx:34-38`. Test: [L2] `PopoverBackdrop.test.tsx` "does not start an entry phase when initially open (controlled=%s)" (reads `state.transitionStatus`); `PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers" (reads `state.instant`, `state.transitionStatus`). (L4 for the JS-mirrored `side`, `align`, `anchorHidden`, `uncentered`, `instant` and `transitionStatus` fields)
- **POP-STA-14** — MUST. A Trigger's `open` state is `true` iff the popup is effectively open **and** this trigger is the effective active trigger (PSH-STA-08); inactive triggers of an open popup have `open: false`.
  Source: `popover/trigger/PopoverTrigger.tsx:61,129`; `utils/popups/store.ts:157-161,200-201`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "when the popup moves to another trigger" (`aria-expanded`/`data-pressed` move to the new trigger).

Invariants: exactly one of `data-open`/`data-closed` on Positioner, Popup, Arrow and Backdrop at all times; at most one Trigger has `data-popup-open`; `data-pressed` implies `data-popup-open`; the internal backdrop exists only while `modal === true` and the popup is mounted and not hover-opened; `focusManagerModal` implies a Close part is mounted inside the Popup.

The open/close state diagram (mounted flag, transition status) is the shared popup lifecycle of [transitions-and-mounting](../architecture/transitions-and-mounting.md) §4.1/4.3 as configured in §9; it is not repeated here.

## 5. Rendered DOM and ARIA

### 5.1 Trigger element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | consumer `id` or generated `base-ui-<token>` | POP-DOM-04 | C# |
| `type="button"` / `role="button"` | native / non-native (UBT) | POP-DOM-06 | C# |
| `disabled` / `aria-disabled="true"` + `tabindex="-1"` | `disabled` native / non-native (UBT) | POP-DOM-07 | C# |
| `data-base-ui-click-trigger=""` | always | POP-DOM-05 | C# |
| `data-popup-open=""` | opened by this trigger | POP-DOM-01 | C# |
| `data-pressed=""` | opened by this trigger by `trigger-press` | POP-DOM-02 | C# |
| `data-disabled=""` | `disabled` | POP-DOM-03 | C# |
| `aria-haspopup`, `aria-expanded`, `aria-controls` | §5.9 | POP-ARIA-01..03 | C# |
| focus guards (siblings) | per POP-STR-14 | POP-DOM-08 | C# |

- **POP-DOM-01** — MUST. The Trigger element has `data-popup-open=""` iff its `open` state (POP-STA-14) is `true`; the attribute is absent otherwise (PSH-DOM-01).
  Source: `popover/trigger/PopoverTrigger.tsx:112-120`; `utils/popupStateMapping.ts:9-11,30-37`. Test: [L2] `PopoverTrigger.test.tsx` "should have the data-popup-open and data-pressed attributes when open by clicking"; `PopoverRoot.test.tsx` "unmounts on a normal close after preventUnmountOnClose and external reopen".
- **POP-DOM-02** — MUST. The Trigger element additionally has `data-pressed=""` iff `data-popup-open` is present **and** the last applied open reason is `trigger-press` (PSH-DOM-02); a hover open renders `data-popup-open` without `data-pressed`, and a press within the patient-click window after a hover open adds `data-pressed`.
  Source: `popover/trigger/PopoverTrigger.tsx:78,112-120`; `utils/popupStateMapping.ts:13-16,39-46`. Test: [L2] `PopoverTrigger.test.tsx` "should have the data-popup-open but not the data-pressed attribute when open by hover", "should have the data-popup-open and data-pressed attributes when open by click when `openOnHover=true`"; `PopoverRoot.detached-triggers.test.tsx` "when a press takes over a hover-opened popup".
- **POP-DOM-03** — MUST. The Trigger element has `data-disabled=""` iff its `disabled` prop is `true` (default state projection, RND-DOM-01).
  Source: `popover/trigger/PopoverTrigger.tsx:127-128`. Test: [L2] `PopoverTrigger.test.tsx` "disables the popover", "custom element".
- **POP-DOM-04** — MUST. The Trigger element's `id` is the trigger id (POP-API-15); a substitute element's own `id` wins per RND-DOM-32.
  Source: `popover/trigger/PopoverTrigger.tsx:58,142`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "renders focus guards around the active detached trigger and reports it on close" (`trigger.id`).
- **POP-DOM-05** — MUST. The Trigger element always carries `data-base-ui-click-trigger=""` (read by the focus manager to treat a pointer-down on the trigger as "inside", FLT-FOC-13).
  Source: `popover/trigger/PopoverTrigger.tsx:141`; `internals/constants.ts:7`. Test: [L2] `PopoverRoot.test.tsx` "closes as soon as focus leaves the popup on pointer down outside" (contrast: outside pointer-down).
- **POP-DOM-06** — MUST. With `nativeButton` `true` the element has `type="button"` (consumer `type` overrides); with `false` it has `role="button"` and `tabindex="0"` (UBT-DOM-01..03).
  Source: `popover/trigger/PopoverTrigger.tsx:107-110,148`. Test: [L2] `PopoverTrigger.test.tsx` "custom element" (`getByRole('button')` on a `<span>`).
- **POP-DOM-07** — MUST. With `disabled`, a native trigger has the `disabled` attribute and no `aria-disabled` (UBT-DOM-06, UBT-ARIA-03); a non-native trigger has `aria-disabled="true"` and `tabindex="-1"` and no `disabled` attribute (UBT-DOM-04/08, UBT-ARIA-02); in both cases the element is removed from the tab order.
  Source: `popover/trigger/PopoverTrigger.tsx:107-110`. Test: [L2] `PopoverTrigger.test.tsx` "disables the popover", "custom element".
- **POP-DOM-08** — MUST. The trigger focus guards (POP-STR-14) are `<span tabindex="0" data-base-ui-focus-guard="" aria-hidden="true">` elements with the visually-hidden style (PSH-DOM-13); the one before the trigger is the *pre-guard*, the one after it is the *focus target*.
  Source: `popover/trigger/PopoverTrigger.tsx:157-168`; `utils/FocusGuard.tsx:10-42`. Test: [L2] `PopoverRoot.test.tsx` "should keep trigger focus guards when `true` without a close part" (`previousElementSibling`/`nextElementSibling`).

### 5.2 Portal node

- **POP-DOM-09** — MUST. The portal node is `<div id="<generated>" data-base-ui-portal="">` appended to the resolved container (FLT-DOM-11); the Portal's `className`/`style`/`render` and other attributes apply to it; the Backdrop, Positioner and Popup render inside it.
  Source: `popover/portal/PopoverPortal.tsx:30-32`; `floating-ui-react/components/FloatingPortal.tsx:125-141,270`. Test: [L2] `PopoverPortal.test.tsx` describeConformance. (L4 for the container-placement clause)
- **POP-DOM-10** — MUST. The Portal renders nothing (no portal node, no children) while the popup is not mounted and `keepMounted` is `false` (TRN-ANI-01).
  Source: `popover/portal/PopoverPortal.tsx:24-27`. Test: [L2] `PopoverRoot.test.tsx` "should close when the anchor is clicked twice" (`queryByText('Content')` is `null` after close).
- **POP-DOM-11** — MUST. While the focus manager is enabled and not in modal mode (POP-FOC-01, POP-STA-10) and the popup is open, the Portal additionally renders, at the Portal's own position in the DOM (not inside the portal node): an outside focus guard (`data-type="outside"`), a `<span aria-owns="<portal node id>">` with the owner visually-hidden style and no `role`, the portaled subtree, and a second outside guard (FLT-DOM-14, FLT-ARIA-04).
  Source: `floating-ui-react/components/FloatingPortal.tsx:192-193,248-295`; `popover/portal/PopoverPortal.tsx:31` (no `portalOwnerRole`). Test: none in Popover (FLT `FloatingPortal.test.tsx`). Docs: CHANGELOG.md v1.2.0 "Fix missing `aria-owns` element (#3959)".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven on Popover by `popover.spec.ts` "port: an open non-modal popover renders the outside guards and the aria-owns owner at the portal's position" (L4, both modes).

### 5.3 Positioner element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `role="presentation"` | always | POP-DOM-12 | C# |
| `hidden` | `mounted === false` (kept-mounted only) | POP-DOM-13 | C# |
| inline `pointer-events: none` | effective open is `false` | POP-DOM-14 | C# → JS |
| `data-open` / `data-closed` | open / closed | POP-DOM-15 | C# |
| `data-side`, `data-align` | resolved placement | POP-DOM-16 | C# → JS |
| `data-anchor-hidden` | anchor hidden | POP-DOM-17 | JS |
| `data-instant` | public instant type | POP-DOM-18 | C# |
| inline `transition: none` | `transitionStatus === 'starting'` | POP-DOM-19 | JS |
| positioning styles and CSS variables | FLT | POP-DOM-20, POP-DOM-21 | C# → JS |
| internal backdrop sibling | POP-STR-13 | POP-DOM-22 | C# |
| internal backdrop `clip-path` | cut-out of the active trigger | POP-DOM-22 | JS |

- **POP-DOM-12** — MUST. The Positioner element has `role="presentation"` (consumer `role` overrides).
  Source: `utils/usePositioner.tsx:36`; see FLT-DOM-01. Test: [L2] `PopoverRoot.test.tsx` "uses intentional outside press with internal backdrop (modal=true)…" (`[role="presentation"]` query also matches the internal backdrop).
- **POP-DOM-13** — MUST. The Positioner element has the boolean `hidden` attribute iff the popup is not mounted (only reachable with `keepMounted`, TRN-DOM-06).
  Source: `popover/positioner/PopoverPositioner.tsx:122`; `utils/usePositioner.tsx:36`. Test: [L2] `PopoverRoot.test.tsx` "restores temporarily disabled focus before focusing a reopened keepMounted popover" (`data-open` absent while closed and retained).
- **POP-DOM-14** — MUST. The Positioner element has inline `pointer-events: none` iff the effective open state is `false` (so a closing or kept-mounted popup cannot be interacted with), merged before the consumer's `style`.
  Source: `popover/positioner/PopoverPositioner.tsx:123`; `utils/usePositioner.tsx:26-30`. Test: none (FLT `positionerConformanceTests` cover the mechanism).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): proven on Popover by `popover.spec.ts` "port: a kept-mounted positioner takes pointer-events none while closed, and drops it when opened" (L4, both modes).
- **POP-DOM-15** — MUST. The Positioner element has `data-open=""` when open and `data-closed=""` when closed, exactly one at all times (PSH-DOM-03).
  Source: `popover/positioner/PopoverPositioner.tsx:110`; `utils/popupStateMapping.ts:48-54`. Test: [L2] viewportConformanceTests "creates morphing containers after a kept-mounted popup closes and reopens".
- **POP-DOM-16** — MUST. The Positioner element has `data-side="<Side>"` and `data-align="<Align>"` equal to the resolved placement after collision handling (logical sides preserved, FLT-POS-10; PSH-DOM-06).
  Source: `popover/positioner/PopoverPositioner.tsx:111-112`; `internals/getStateAttributesProps.ts` default projection. Test: [L4] `PopoverPositioner.test.tsx` "points to the flipped edge when the alignment flips" (`data-align="end"`).
- **POP-DOM-17** — MUST. The Positioner element has `data-anchor-hidden=""` iff `anchorHidden` is `true` (FLT-POS-09; PSH-DOM-04).
  Source: `popover/positioner/PopoverPositioner.tsx:113`; `utils/popupStateMapping.ts:55-60`. Test: none in Popover (FLT positioner conformance).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven on Popover by `popover.spec.ts` "port: the positioner is marked anchor-hidden once its anchor scrolls out of view" (L4, both modes).
- **POP-DOM-18** — MUST. The Positioner element has `data-instant="<value>"` iff the public instant type (POP-STA-07) is defined (TRN-DOM-10).
  Source: `popover/positioner/PopoverPositioner.tsx:114`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a prop-driven close after a switch" (CSS `.positioner[data-instant]`).
  Port note 2026-10-28 (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `data-instant` is C#'s, projected from `PopoverPositionerState.Instant`; `trigger-change` is written by C# when the `popup` module reports that the Positioner's animations after a switch have finished (POP-ANI-07), so the Writer cell reads `C#`.
- **POP-DOM-19** — MUST. While `transitionStatus` is `'starting'` the Positioner element has inline `transition: none` (TRN-DOM-05), removed once the status leaves `'starting'`.
  Source: `popover/positioner/PopoverPositioner.tsx:119`; `utils/usePositioner.tsx:37`; `internals/getDisabledMountTransitionStyles.ts:5-9`. Test: none in Popover. Docs: CHANGELOG.md v1.0.0 "Fix mount transitions on `Positioner` in Firefox (#3424)".
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): no Popover port title. The Positioner's starting-phase `transition: none` is the positioning mechanism's, written by `PositionerBehavior` for every family before the consumer's style and proven by FLT-ANI-05's L2 port title in `PositionerBehaviorTests`; Popover only passes its transition status, and the starting frame lasts one animation frame, so an L4 observation would add a race and no fact.
- **POP-DOM-20** — MUST. The Positioner element's inline style carries `--anchor-width`, `--anchor-height`, `--available-width`, `--available-height` (FLT-DOM-05/06) and `--transform-origin` (FLT-DOM-07) on every positioning pass, and `--positioner-width`/`--positioner-height` while a Viewport is mounted (PSH-DOM-11).
  Source: `popover/positioner/PopoverPositionerCssVars.ts:5-37`; `internals/useAnchorPositioning.ts` (size and transform-origin middleware). Test: [L4] `PopoverPositioner.test.tsx` "transform origin" (19 tests); positionerConformanceTests "uses top/left positioning with Viewport".
- **POP-DOM-21** — MUST. The Positioner element's inline positioning styles are the shared ones (FLT-DOM-02..04): `position: fixed; top: 0; left: 0; opacity: 0` until first positioned, then `position: <positionMethod>` with transform-based coordinates, or `top`/`left`/`bottom`/`right` coordinates while a Viewport is mounted (adaptive origin, PSH-POS-02).
  Source: `popover/positioner/PopoverPositioner.tsx:69-87,118`. Test: [L4] positionerConformanceTests "uses transform positioning without Viewport", "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts".
- **POP-DOM-22** — MUST. The internal backdrop (POP-STR-13) is `<div role="presentation" data-base-ui-inert="">` with inline `position: fixed; inset: 0; user-select: none; -webkit-user-select: none` and a `clip-path` polygon that cuts out the active trigger's bounding rectangle (PSH-DOM-14, PSH-PTR-05); it carries the `inert` attribute while the effective open state is `false`.
  Source: `popover/positioner/PopoverPositioner.tsx:128-130`; `utils/InternalBackdrop.tsx:6-35`. Test: [L2] `PopoverRoot.test.tsx` "should render an internal backdrop when `true`". (L4 for the `clip-path` clause) Docs: CHANGELOG.md v1.0.0-beta.1 "Cut out internal backdrop to allow interacting with triggers (#2141)".
  Port note 2026-10-28 (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the markup is C#'s, the `clip-path` cut-out of the active trigger is the `popup` module's write (PSH-ALG-21), hence the `JS` row in §5.3.

### 5.4 Popup element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `role="dialog"` | always (consumer `role` overrides) | POP-ARIA-04 | C# |
| `id` | generated popup id, or consumer `id` | POP-DOM-23 | C# |
| `tabindex="-1"`, `data-base-ui-focusable=""` | always | POP-DOM-24 | C# |
| `aria-labelledby`, `aria-describedby` | Title / Description ids | POP-ARIA-05/06 | C# |
| `data-open` / `data-closed` | open / closed | POP-DOM-25 | C# |
| `data-starting-style` / `data-ending-style` | transition status | POP-DOM-26 | JS |
| `data-side`, `data-align` | resolved placement | POP-DOM-27 | C# |
| `data-instant` | public instant type | POP-DOM-28 | C# |
| inline `transition: none` | `'starting'` | POP-DOM-29 | JS |
| `--popup-width`, `--popup-height` | Viewport mounted | POP-DOM-30 | JS |
| inside focus guards (siblings) | POP-STR-15 | POP-DOM-31 | C# |

- **POP-DOM-23** — MUST. The Popup element's `id` is the Root's generated floating id unless the consumer supplies an `id`; the Trigger's `aria-controls` references whichever is rendered (PSH-STA-04, PSH-ARIA-03).
  Source: `popover/popup/PopoverPopup.tsx:51,90`; `utils/popups/store.ts:152-155`. Test: [L2] popupConformanceTests "allows a custom `id` prop", "has the `aria-controls` attribute on the trigger".
- **POP-DOM-24** — MUST. The Popup element has `tabindex="-1"` and `data-base-ui-focusable=""` (PSH-DOM-15); because it already carries an unmanaged `tabindex`, the focus manager's dialog `tabindex` management (FLT-DOM-17) never changes it — the popup stays out of the sequential tab order even when it has no tabbable content.
  Source: `popover/popup/PopoverPopup.tsx:92`; `utils/popups/popupStoreUtils.ts:27-30`; `floating-ui-react/components/FloatingFocusManager.tsx:114-120`. Test: [L2] `PopoverRoot.test.tsx` "does not loop focus between guards when tabbing out of the popup" (popup itself receives initial focus when it has no tabbable content).
- **POP-DOM-25** — MUST. The Popup element has `data-open=""` when open and `data-closed=""` when closed (PSH-DOM-03).
  Source: `popover/popup/PopoverPopup.tsx:77,104`; `utils/popupStateMapping.ts:63-70`. Test: [L2] `PopoverRoot.test.tsx` "closes a nested combobox popup when tabbing out of the popover" (`data-closed`); popupConformanceTests "opens on trigger click and sets `aria-expanded` on the trigger" (`data-open`).
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Combobox "closes a nested combobox popup when tabbing out of the popover"); the row's other titles run in `popover.spec.ts`, both modes.
- **POP-DOM-26** — MUST. The Popup element has `data-starting-style=""` while its `transitionStatus` is `'starting'` and `data-ending-style=""` while it is `'ending'` (TRN-DOM-01..03).
  Source: `popover/popup/PopoverPopup.tsx:81,104`; `internals/stateAttributesMapping.ts`. Test: [L4] `PopoverRoot.test.tsx` "reopens immediately when re-hovering the trigger during a hover close transition", "moves focus to the element following the trigger when tabbing forward from the trigger" (during the exit animation).
- **POP-DOM-27** — MUST. The Popup element has `data-side` and `data-align` equal to the Positioner's resolved placement (PSH-DOM-06).
  Source: `popover/popup/PopoverPopup.tsx:78-79`. Test: none in Popover (positioner conformance covers the values).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): proven on Popover by `popover.spec.ts` "port: the popup carries the positioner's data-side and data-align" (L4, both modes).
- **POP-DOM-28** — MUST. The Popup element has `data-instant="<value>"` iff the public instant type is defined; the value set is `click`, `dismiss`, `focus`, `trigger-change`.
  Source: `popover/popup/PopoverPopup.tsx:80`; `popover/popup/PopoverPopupDataAttributes.ts:29-33`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers" (and the five sibling tests), "does not restore the trigger-change instant after a reopen on the same trigger".
  Port note 2026-10-28 (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `data-instant` is C#'s, projected from `PopoverPopupState.Instant`, so the Writer cell reads `C#`.
- **POP-DOM-29** — MUST. While `transitionStatus` is `'starting'` the Popup element has inline `transition: none` (TRN-DOM-05).
  Source: `popover/popup/PopoverPopup.tsx:101`. Test: none.
  Port note (2026-10-29, M4 track 5; ADR 04 Amendments 2026-10-28, gap 13's option (b)): the Popup's `TransitionsTwin` sets `DisableTransitionWhileStarting`, so from the handover the `transitions` module writes the declaration on each starting frame and removes it on the clearing frame (TRN-DOM-05's port note); before attach C# renders it. L2 port title: `PopoverModuleWiringTests` "port: the popup's transitions handle asks for transition: none while starting".
- **POP-DOM-30** — MUST. While a Viewport is mounted, the Popup element's inline style carries `--popup-width`/`--popup-height` per PSH-DOM-11 (`auto` at rest, `px` sizes during a content switch) and the inline anchoring styles of PSH-DOM-12; without a Viewport neither is written.
  Source: `popover/popup/PopoverPopupCssVars.ts:4-8`; `utils/usePopupViewport.tsx:280-289`. Test: [L4] viewportConformanceTests "creates morphing containers during transitions", `it.each` side/direction cases (inline anchoring styles); detachedTriggersConformanceTests "does not leave an inline scale style on the popup after switching triggers".
  Port note 2026-10-28 (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the variables and the anchoring styles are the `popup` module's auto-resize layer of the `dom` style writer, so the Writer cell reads `JS`.
- **POP-DOM-31** — MUST. The inside focus guards (POP-STR-15) are `<span tabindex="0" data-base-ui-focus-guard="" data-type="inside" aria-hidden="true">` elements (FLT-DOM-13/15).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:1008-1052`. Test: [L2] `PopoverRoot.test.tsx` "should only render focus guards inside the popup when `true`".

### 5.5 Arrow element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `aria-hidden="true"` | always (consumer overrides) | POP-ARIA-07 | C# |
| `data-open` / `data-closed` | open / closed | POP-DOM-32 | C# |
| `data-side`, `data-align` | resolved placement | POP-DOM-32 | C# → JS |
| `data-uncentered` | arrow not centered on the anchor | POP-DOM-32 | C# → JS |
| `style` | arrow position | POP-DOM-32 | C# → JS |

- **POP-DOM-32** — MUST. The Arrow element has `data-open`/`data-closed` (exactly one), `data-side` and `data-align` (resolved placement), and `data-uncentered=""` iff the arrow could not be centered on the anchor (FLT-DOM-08 `arrowUncentered`); its inline style is the arrow position (`position: absolute` with `top`/`left`, FLT-DOM-09) merged before the consumer's `style`.
  Source: `popover/arrow/PopoverArrow.tsx:24-38`; `popover/arrow/PopoverArrowDataAttributes.ts:6-24`. Test: [L4] `PopoverArrow.test.tsx` describeConformance; `PopoverPositioner.test.tsx` "points to the arrow when present regardless of alignment".

### 5.6 Backdrop element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `role="presentation"` | always (consumer overrides) | POP-DOM-33 | C# |
| `hidden` | not mounted | POP-DOM-34 | C# |
| inline `user-select`, `pointer-events` | always; hover-opened | POP-DOM-35 | C# |
| `data-open` / `data-closed` | open / closed | POP-DOM-36 | C# |
| `data-starting-style` / `data-ending-style` | transition status | POP-DOM-36 | JS |

- **POP-DOM-33** — MUST. The Backdrop element has `role="presentation"` (consumer `role` overrides).
  Source: `popover/backdrop/PopoverBackdrop.tsx:39`. Test: [L2] `PopoverRoot.test.tsx` "uses intentional outside press with user backdrop (mouse): closes on click, not on mousedown".
- **POP-DOM-34** — MUST. The Backdrop element has the boolean `hidden` attribute iff the popup is not mounted.
  Source: `popover/backdrop/PopoverBackdrop.tsx:40`. Test: none.
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven by `PopoverPartsTests` "does not set `pointer-events: none` style on backdrop if opened by click" (L2), which also asserts the Backdrop's `hidden` while the popup is not mounted.
- **POP-DOM-35** — MUST. The Backdrop element's inline style has `user-select: none; -webkit-user-select: none` always, and `pointer-events: none` iff the last applied open reason is `trigger-hover` (so a hover-opened popover's backdrop does not swallow the pointer); the consumer's `style` is merged over these.
  Source: `popover/backdrop/PopoverBackdrop.tsx:41-45`. Test: [L2] `PopoverBackdrop.test.tsx` "sets `pointer-events: none` style on backdrop if opened by hover", "does not set `pointer-events: none` style on backdrop if opened by click". Docs: CHANGELOG.md v1.0.0-alpha.7 "Set `pointer-events: none` style on backdrops when hoverable (#1351)".
- **POP-DOM-36** — MUST. The Backdrop element has `data-open`/`data-closed` (exactly one) and `data-starting-style`/`data-ending-style` per its `transitionStatus` (PSH-DOM-05); it never has `data-anchor-hidden` (not in its state).
  Source: `popover/backdrop/PopoverBackdrop.tsx:29-32,49`; `popover/backdrop/PopoverBackdropDataAttributes.ts:6-18`. Test: [L2] `PopoverBackdrop.test.tsx` "does not start an entry phase when initially open (controlled=%s)". (L4 for the `data-starting-style`/`data-ending-style` clause)

### 5.7 Viewport element and containers

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `[data-current]` container | always, keyed by the active trigger and the late payload | POP-DOM-37 | C# |
| `[data-previous]` container | during a content transition | POP-DOM-37 | JS |
| `data-activation-direction` | while a direction exists | POP-DOM-38 | JS |
| `data-transitioning` | `[data-previous]` rendered | POP-DOM-39 | JS |
| `data-instant` | public instant type | POP-DOM-40 | C# |
| `--popup-width` / `--popup-height` on `[data-previous]` | the previous content's popup size | POP-DOM-41 | JS |

- **POP-DOM-37** — MUST. The Viewport element's children are `<div data-current>` (always) and, during a content transition, a preceding `<div data-previous inert style="position: absolute; …">` holding a clone of the outgoing content (PSH-STR-07, PSH-DOM-09/10).
  Source: `utils/usePopupViewport.tsx:229-268`. Test: [L4] viewportConformanceTests "renders children in the `current` container by default", "creates morphing containers during transitions".
- **POP-DOM-38** — MUST. The Viewport element has `data-activation-direction="<h> <v>"` only while a direction exists (`<h>` ∈ `left`, `right`, empty; `<v>` ∈ `down`, `up`, empty; computed from the previous and new trigger centers with a 5 px tolerance, PSH-DOM-07).
  Source: `popover/viewport/PopoverViewport.tsx:35,44`; `utils/usePopupViewport.tsx:21-30,310-343`. Test: [L4] viewportConformanceTests "sets `data-activation-direction` from the previous to the new trigger". Docs: `components/popover/page.mdx` §Animating the Popover › Content.
- **POP-DOM-39** — MUST. The Viewport element has `data-transitioning=""` iff a `[data-previous]` container is rendered (PSH-DOM-08).
  Source: `popover/viewport/PopoverViewport.tsx:36`; `utils/CommonViewportDataAttributes.ts`. Test: [L4] viewportConformanceTests "creates morphing containers during transitions".
- **POP-DOM-40** — MUST. The Viewport element has `data-instant="<value>"` iff the public instant type is defined.
  Source: `popover/viewport/PopoverViewport.tsx:26,37`. Test: none.
  Port note 2026-10-28 (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): `data-instant` is C#'s, projected from `PopoverViewportState.Instant`; §5.7's Writer table names the Viewport's other writers (the containers, the direction and `data-transitioning` are the `popup` module's).
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven by `PopoverModuleWiringTests` "port: the trigger-change report writes the instant on the positioner, the popup and the viewport while open" (L2).
- **POP-DOM-41** — MUST. The `[data-previous]` container's inline style carries `--popup-width`/`--popup-height` equal to the popup size when the previous content was rendered (PSH-DOM-09).
  Source: `popover/viewport/PopoverViewportCssVars.ts:6-12`; `utils/usePopupViewport.tsx:244-254`. Test: [L4] viewportConformanceTests "creates morphing containers during transitions" (`/^\d+(?:\.\d+)?px$/`).

### 5.8 Title, Description and Close elements

- **POP-DOM-42** — MUST. The Title element (`<h2>` by default) and the Description element (`<p>` by default) each carry an `id`: the consumer's `id` when given, else a generated `base-ui-<token>`; the same id is what the Popup references (POP-ARIA-05/06).
  Source: `popover/title/PopoverTitle.tsx:22,26-29`; `popover/description/PopoverDescription.tsx:22,26-29`. Test: [L2] `PopoverTitle.test.tsx` "labels the popup element with its id"; `PopoverDescription.test.tsx` "describes the popup element with its id".
- **POP-DOM-43** — MUST. The Close element renders per UBT with `type="button"` (native) or `role="button"` (non-native) and the `disabled` handling of POP-API-25; it has no `data-*` state attributes.
  Source: `popover/close/PopoverClose.tsx:30-50`. Test: [L2] `PopoverClose.test.tsx` describeConformance.

### 5.9 ARIA

- **POP-ARIA-01** — MUST. Every Trigger has `aria-haspopup="dialog"` regardless of open state (PSH-ARIA-04).
  Source: `popover/trigger/PopoverTrigger.tsx:143`. Test: none in Popover (popupConformanceTests runs without `expectedAriaHasPopupValue`, §16 Q6).
  Fidelity correction (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); conventions §3.5, no decision record): the suite defaults `expectedAriaHasPopupValue` to `expectedPopupRole` (`test/popupConformanceTests.tsx:19`), so Popover's run, with `expectedPopupRole: 'dialog'`, does test the value. Test: [L2] popupConformanceTests "has the `aria-haspopup` attribute on the trigger"; the port's L2 proof is `PopoverPartsTests.cs` "port: the trigger renders its ARIA attributes and the click-trigger marker" (every trigger), and the suite's title also runs at L4 in `popover.spec.ts`.
- **POP-ARIA-02** — MUST. Every Trigger has `aria-expanded="true"` iff its `open` state (POP-STA-14) is `true`, and `aria-expanded="false"` otherwise (PSH-ARIA-01); inactive triggers of an open popup report `"false"`.
  Source: `popover/trigger/PopoverTrigger.tsx:144`. Test: [L2] popupConformanceTests "opens on trigger click and sets `aria-expanded` on the trigger"; `PopoverTrigger.test.tsx` "keeps ownership on the tapped trigger when a sibling trigger is hovered"; `PopoverRoot.detached-triggers.test.tsx` "opens by trigger from a descendant layout effect on initial mount".
- **POP-ARIA-03** — MUST. A Trigger has `aria-controls="<popup id>"` iff it owns the open popup, or the popup is open with no active trigger resolved and this is the only registered trigger (PSH-STA-10, PSH-ARIA-02); otherwise the attribute is absent.
  Source: `popover/trigger/PopoverTrigger.tsx:62,145`; `utils/popups/store.ts:163-175,212-213`. Test: [L2] popupConformanceTests "has the `aria-controls` attribute on the trigger".
- **POP-ARIA-04** — MUST. The Popup element has `role="dialog"`.
  Source: `popover/popup/PopoverPopup.tsx:91`. Test: [L2] popupConformanceTests "has the dialog role on the popup"; `PopoverRoot.test.tsx` (every `getByRole('dialog')` query).
- **POP-ARIA-05** — MUST. The Popup element has `aria-labelledby="<Title id>"` while a Title is mounted (POP-STA-09) and no `aria-labelledby` otherwise; a consumer `aria-labelledby` on the Popup overrides it.
  Source: `popover/popup/PopoverPopup.tsx:44,93`. Test: [L2] `PopoverTitle.test.tsx` "labels the popup element with its id".
- **POP-ARIA-06** — MUST. The Popup element has `aria-describedby="<Description id>"` while a Description is mounted and no `aria-describedby` otherwise; a consumer value overrides it.
  Source: `popover/popup/PopoverPopup.tsx:45,94`. Test: [L2] `PopoverDescription.test.tsx` "describes the popup element with its id".
- **POP-ARIA-07** — MUST. The Arrow element has `aria-hidden="true"` unless the consumer overrides it.
  Source: `popover/arrow/PopoverArrow.tsx:36`. Test: none. Docs: CHANGELOG.md v1.0.0-alpha.5 "Apply `aria-hidden` to `Arrow` parts [#1196]".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): proven on Popover by `PopoverPartsTests` "port: the arrow is aria-hidden" (L2).
- **POP-ARIA-08** — MUST. The Positioner, the Backdrop and the internal backdrop have `role="presentation"` (POP-DOM-12, POP-DOM-33, POP-DOM-22).
  Source: `utils/usePositioner.tsx:36`; `popover/backdrop/PopoverBackdrop.tsx:39`; `utils/InternalBackdrop.tsx:21`. Test: [L2] `PopoverRoot.test.tsx` "should render an internal backdrop when `true`".
- **POP-ARIA-09** — MUST. While the popup is open and in focus-manager-modal mode (POP-STA-10), every element outside the popup's inside set (the positioner subtree, the portal node, the focus guards, the before-trigger guard, the active trigger and the trigger focus target) is marked `aria-hidden="true"` or `inert` for assistive technology (FLT-ARIA-01/03); in non-modal mode no `aria-hidden` is written (FLT-ARIA-02).
  Source: `popover/popup/PopoverPopup.tsx:108-122`; `floating-ui-react/components/FloatingFocusManager.tsx:619-677`. Test: [L4] `PopoverClose.test.tsx` "enables modal focus management when `modal=%s` and close is rendered", "does not enable modal focus management when close is not rendered"; `PopoverRoot.test.tsx` "closes a nested combobox popup when tabbing backward to the trigger" (trigger never `aria-hidden`).
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Combobox "closes a nested combobox popup when tabbing backward to the trigger"); the row's other titles run in `popover.spec.ts`, both modes.
- **POP-ARIA-10** — MUST. All focus guards (trigger, inside and outside) are `aria-hidden="true"` except under VoiceOver on WebKit, where they are `role="button"` without `aria-hidden` (PSH-ARIA-05).
  Source: `utils/FocusGuard.tsx:16-24,37`. Test: none in Popover.
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven on Popover by `popover.spec.ts` "port: every focus guard of an open popover is aria-hidden" (L4, both modes).

## 6. Interactions

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Enter</kbd> / <kbd>Space</kbd> | Trigger focused, enabled | Toggles the popup (press with `detail === 0` → `trigger-press`, instant `click`); native `<button>` via the browser's click, non-native via UBT emulation. | POP-KEY-01 |
| <kbd>Escape</kbd> | popup open; keydown anywhere in the document, on the trigger, or inside the popup | Closes with reason `escape-key`; `preventDefault` when not canceled; propagation stopped unless `allowPropagation()`. Ignored during IME composition. | POP-KEY-02, POP-KEY-03 |
| <kbd>Escape</kbd> | nested popover open inside this popover | Only the innermost open popover closes. | POP-KEY-04 |
| <kbd>Tab</kbd> | Trigger focused, popup open, non-modal | Focus moves into the popup (first tabbable, else the popup). | POP-KEY-05 |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | first tabbable inside popup (or popup itself), non-modal | Focus moves to the trigger; popup stays open. | POP-KEY-06 |
| <kbd>Tab</kbd> | last tabbable inside popup (or popup itself), non-modal | Popup closes (`focus-out`); focus moves to the tabbable after the trigger, skipping the popup. | POP-KEY-07 |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | Trigger focused, popup open, non-modal | Popup closes (`focus-out`); focus moves to the tabbable before the trigger. | POP-KEY-08 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | focus-manager-modal mode | Focus cycles inside the popup; with no tabbable content the keydown is prevented. | POP-KEY-09 |
| <kbd>Tab</kbd> | Trigger focused during the exit animation | Native tab order; focus moves to the element after the trigger; the closing popup is skipped. | POP-KEY-10 |
| <kbd>Tab</kbd> | no other tabbable element on the page | Popup closes once; focus ends on the trigger; no guard bouncing. | POP-KEY-11 |
| <kbd>ArrowUp/Down/Left/Right</kbd>, <kbd>Home</kbd>, <kbd>End</kbd> | keydown inside the popup, Root inside a `Toolbar.Root` | Propagation stopped (not prevented) so the toolbar's roving focus does not react; the key still acts inside the popup. | POP-KEY-12 |
| any other key | — | Not intercepted. | — |

- **POP-KEY-01** — MUST. <kbd>Enter</kbd> and <kbd>Space</kbd> on an enabled Trigger toggle the popup exactly like a click (POP-PTR-01/02): the resulting click has `detail === 0`, so the open change carries reason `trigger-press`, the open interaction type is `keyboard` (POP-STA-08) and the instant type is `click` (POP-STA-07). For a non-native trigger the activation is emulated per UBT-KEY-01/02 (Enter on keydown, Space on keyup).
  Source: `popover/trigger/PopoverTrigger.tsx:97,107-110,136-149`; `floating-ui-react/hooks/useClick.ts:187-222`; `packages/utils/src/useEnhancedClickHandler.ts:30-36`. Test: [L4] `PopoverTrigger.test.tsx` "should toggle closed with Enter or Space when rendering a <div>"; `PopoverRoot.test.tsx` "keeps the popover open when a nested menu opens via Enter using a shared container".
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Menu "keeps the popover open when a nested menu opens via Enter using a shared container"); the row's other titles run in `popover.spec.ts`, both modes.
- **POP-KEY-02** — MUST. While open, an <kbd>Escape</kbd> keydown observed on the owner document, on the Trigger, or inside the Popup requests a close with reason `escape-key` and the native keyboard event (FLT-KEY-01); if the request is not canceled, `preventDefault()` is called on the keydown; unless `allowPropagation()` was called in `onOpenChange`, `stopPropagation()` is called (EVD-EVT-11, EVD-DIS-01). Server: relaxed, see §9.
  Source: `popover/root/PopoverRoot.tsx:241-262`; `floating-ui-react/hooks/useDismiss.ts:201-232,719-724,775-792`. Test: [L4] `PopoverRoot.test.tsx` "rewires dismiss interactions after closing and reopening"; popupConformanceTests "is ignored when `open={true}` is passed, so Escape cannot close"; `PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted".
- **POP-KEY-03** — MUST. <kbd>Escape</kbd> is ignored while an IME composition is in progress (`isComposing`, or within the composition-settled window after `compositionend`; FLT-KEY-03).
  Source: `floating-ui-react/hooks/useDismiss.ts:208-214,316-335`. Test: none in Popover.
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): no Popover port title. The IME guard is the `interactions` module's dismiss region, shared by every family and proven by FLT-KEY-03's L3 titles (`interactions.test.ts`, from `useDismiss.test.tsx`); Popover adds no behaviour, and Playwright drives an IME composition only through CDP, whose `isComposing` on the closing Escape is not guaranteed across engines.
- **POP-KEY-04** — MUST. Popover does not let <kbd>Escape</kbd> bubble (`bubbles` unset → `escapeKey: false`, FLT-API-01): while a nested open popover (a tree child) exists, the parent ignores Escape (FLT-KEY-02); the child closes and stops propagation, so one keypress closes one level.
  Source: `popover/root/PopoverRoot.tsx:241-248` (no `bubbles`); `floating-ui-react/hooks/useDismiss.ts:139,169-178,216-218`. Test: none in Popover (FLT `useDismiss.test.tsx`). Docs: CHANGELOG.md v1.0.0-beta.2 "Fix unexpected close when nested inside two popovers (#2481)".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven on Popover by `popover.spec.ts` "port: Escape closes one nesting level at a time" (L4, both modes).
- **POP-KEY-05** — MUST. In non-modal mode with the popup open by a trigger, <kbd>Tab</kbd> from the trigger lands on the trigger's focus target guard, which forwards focus to the popup's before-content guard, which in turn focuses the next tabbable element in the document after itself — the first tabbable inside the Popup (or the Popup element when it has none); the popup stays open (PSH-KEY-01, FLT-KEY-06).
  Source: `popover/trigger/PopoverTrigger.tsx:165`; `utils/popups/useTriggerFocusGuards.ts:62-69`; `floating-ui-react/components/FloatingFocusManager.tsx:1012-1026`; `floating-ui-react/utils/tabbable.ts:198-217`. Test: [L4] `PopoverRoot.test.tsx` "moves focus to the trigger when tabbing backward from the open popup then to the popup when tabbing forward" (three placements).
- **POP-KEY-06** — MUST. In non-modal mode, <kbd>Shift</kbd>+<kbd>Tab</kbd> from the first tabbable inside the Popup (or from the Popup element) lands on the before-content guard, which focuses the active trigger element; the popup stays open and focus moving to the trigger is not treated as focus leaving (FLT-KEY-06 with `previousFocusableElement` = active trigger).
  Source: `popover/popup/PopoverPopup.tsx:117-119`; `floating-ui-react/components/FloatingFocusManager.tsx:1017-1024`. Test: [L4] `PopoverRoot.test.tsx` "moves focus to the trigger when tabbing backward from the open popup then to the popup when tabbing forward"; "closes a nested combobox popup when tabbing backward to the trigger".
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Combobox "closes a nested combobox popup when tabbing backward to the trigger"); the row's other titles run in `popover.spec.ts`, both modes.
- **POP-KEY-07** — MUST. In non-modal mode, <kbd>Tab</kbd> from the last tabbable inside the Popup (or from the Popup element) lands on the after-content guard, which focuses the trigger's focus target guard; because focus arrived from inside the positioner, the popup closes synchronously with reason `focus-out` and `trigger` = the guard element, and focus then moves to the next tabbable after the guard (or after the trigger) in the current tab order, skipping the positioner subtree (PSH-KEY-02, PSH-KEY-04). Server: relaxed, see §9.
  Source: `popover/popup/PopoverPopup.tsx:120`; `floating-ui-react/components/FloatingFocusManager.tsx:1034-1050`; `utils/popups/useTriggerFocusGuards.ts:38-69`. Test: [L4] `PopoverRoot.test.tsx` "moves focus to the element following the trigger, excluding the popup, when tabbing forward from the open popup" (two placements), "moves focus to the element following the trigger when tabbing forward from the open popup", "closes a nested combobox popup when tabbing out of the popover".
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Combobox "closes a nested combobox popup when tabbing out of the popover"); the row's other titles run in `popover.spec.ts`, both modes.
- **POP-KEY-08** — MUST. In non-modal mode, <kbd>Shift</kbd>+<kbd>Tab</kbd> from the trigger while the popup is open lands on the trigger's pre-guard, which closes the popup synchronously with reason `focus-out` (`trigger` = the guard) and focuses the previous tabbable before the guard/trigger, skipping the positioner subtree (PSH-KEY-03). Server: relaxed, see §9.
  Source: `popover/trigger/PopoverTrigger.tsx:160-163`; `utils/popups/useTriggerFocusGuards.ts:58-60`. Test: [L4] `PopoverRoot.test.tsx` "closes and moves focus before the trigger when tabbing backward from the focused trigger", "moves focus to the element preceding the trigger when tabbing backward from the trigger while open".
- **POP-KEY-09** — MUST. In focus-manager-modal mode (POP-STA-10), <kbd>Tab</kbd> from the last tabbable wraps to the first and <kbd>Shift</kbd>+<kbd>Tab</kbd> from the first wraps to the last (through the inside guards, next frame; FLT-KEY-05); when the Popup has no tabbable content, a <kbd>Tab</kbd> keydown while focus is inside the Popup is prevented and stopped (FLT-KEY-04); no trigger guards are rendered (POP-STR-14).
  Source: `popover/popup/PopoverPopup.tsx:111`; `floating-ui-react/components/FloatingFocusManager.tsx:343-363,1013-1016,1035-1037`. Test: [L4] `PopoverRoot.test.tsx` "should only render focus guards inside the popup when `true`"; `PopoverClose.test.tsx` "enables modal focus management when `modal=%s` and close is rendered".
- **POP-KEY-10** — MUST. While the popup is closing (exit animation running, `data-ending-style` present) the trigger guards are already gone (POP-STR-14 requires the popup to be open) and the positioner is `pointer-events: none`; <kbd>Tab</kbd> from the trigger therefore follows the native tab order to the element after the trigger, and focus stays there after the popup unmounts.
  Source: `popover/store/PopoverStore.ts:67-68`; `popover/positioner/PopoverPositioner.tsx:123`. Test: [L4] `PopoverRoot.test.tsx` "moves focus to the element following the trigger when tabbing forward from the trigger" (during the exit animation).
- **POP-KEY-11** — MUST. When the trigger is the only tabbable element on the page, <kbd>Tab</kbd> out of the popup and <kbd>Shift</kbd>+<kbd>Tab</kbd> out of the trigger each close the popup once, each focus guard receives focus at most once, and focus ends on the trigger (PSH-KEY-05).
  Source: `utils/popups/useTriggerFocusGuards.ts:51-55`; `floating-ui-react/utils/tabbable.ts:226-256`. Test: [L4] `PopoverRoot.test.tsx` "does not loop focus between guards when tabbing out of the popup", "does not loop focus between guards when shift-tabbing out of the trigger". Docs: CHANGELOG.md v1.9.0 "Fix focus guard loop when the trigger is the only tabbable element (#5733)".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): `useTriggerFocusGuards` closes with `flushSync` before it looks for the tabbable near the guard, so the trigger's guards and the portal's outside guards are gone by then. On WebAssembly the close commits inside the guard's decision and the same holds. On Server the close lands a round trip later, so the `popup` module resolves the destination from the trigger with the guards the close unmounts taken out of the tab order for that lookup only; without it the next guard took focus and bounced it back into the popup. The two titles run per trigger layout (Base UI's `describe.for`), skipped for multiple detached triggers as Base UI skips them.
- **POP-KEY-12** — MUST. When the Root is rendered inside a `Toolbar.Root`, a keydown inside the Popup whose key is one of <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>Home</kbd>, <kbd>End</kbd> has its propagation stopped (not its default prevented) so the toolbar's roving focus does not move; outside a toolbar these keys propagate normally (TLB-KEY-15).
  Source: `popover/popup/PopoverPopup.tsx:36,95-99`; `internals/composite/composite.ts:22`. Test: [L4] `PopoverPopup.test.tsx` "does not relay composite keys from the popup to the toolbar", "keeps composite keys working inside the popup content".

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| primary click on Trigger | closed | Opens (`trigger-press`, trigger = that element); open interaction type recorded. | POP-PTR-01 |
| primary click on active Trigger | open | Closes (`trigger-press`), unless the popup was hover-opened less than 500 ms ago (stays open, becomes press-owned). | POP-PTR-02, POP-PTR-04 |
| click on an inactive Trigger | open | Switches: `onOpenChange(true)` with the new trigger; no close. | POP-PTR-03 |
| click on Trigger before the hover rest delay elapses | hover pending | Opens by press; the pending hover open is discarded; later leaving does not close. | POP-PTR-05 |
| mouse enters Trigger (`openOnHover`) | closed; mouse/pen only | `delay` 0: opens on `mouseenter`; `delay` > 0: opens after the mouse rests for `delay` ms. | POP-PTR-06 |
| mouse leaves Trigger (hover-opened) | open | Safe-polygon tracking towards the popup; close (`trigger-hover`) after `closeDelay` ms once the cursor leaves the polygon. | POP-PTR-07 |
| mouse enters/leaves the Popup (positioner) | hover-opened | Enter cancels a pending close; leave closes after `closeDelay`. | POP-PTR-12 |
| mouse enters another `openOnHover` Trigger | open | Hands the popup over (immediate, no delay). | POP-PTR-11 |
| mouse leaves Trigger/Popup | opened by press, prop or handle | No close. | POP-PTR-10, POP-PTR-13 |
| touch tap on Trigger | any | Opens/closes by press; hover is never triggered by touch; after a touch press open, hovering sibling `openOnHover` triggers does nothing. | POP-PTR-09, POP-PTR-14 |
| re-enter Trigger during a hover close transition | closing | Reopens immediately on `mouseenter` (no rest delay). | POP-PTR-20 |
| non-primary button on Trigger | any | Ignored by the click interaction (native button semantics apply). | POP-PTR-15 |
| pointer on the internal backdrop (`modal: true`) | open | Outside elements are shielded; a click on the backdrop dismisses (POP-DIS-01); the trigger stays reachable through the cutout. | POP-PTR-18 |
| touch-opened `modal: true` popup | open | Page scroll locked only when the positioner spans the viewport width (±20 px). | POP-PTR-19 |
| click on `Popover.Close` | any | Closes with `close-press` and the active trigger as `trigger`. | POP-PTR-22 |

- **POP-PTR-01** — MUST. A primary-button `click` on an enabled Trigger while the popup is closed requests an open with reason `trigger-press`, the native event and `trigger` = the trigger element (FLT-PTR-03/04, default `event: 'click'`, `toggle: true`); the interaction type of the press is recorded as `openMethod` (POP-STA-08).
  Source: `popover/trigger/PopoverTrigger.tsx:96-103,136-139`; `floating-ui-react/hooks/useClick.ts:187-219`. Test: [L4] `PopoverRoot.test.tsx` "should close when the anchor is clicked twice"; popupConformanceTests "is called with the new open state and the change details".
- **POP-PTR-02** — MUST. A primary-button `click` on the *active* Trigger while the popup is open requests a close with reason `trigger-press` (toggle), except in the stick-if-open case of POP-PTR-04.
  Source: `floating-ui-react/hooks/useClick.ts:94-124`. Test: [L4] `PopoverRoot.test.tsx` "should close when the anchor is clicked twice", "remains uncontrolled, so a trigger click closes it".
- **POP-PTR-03** — MUST. A `click` on a Trigger that is **not** the active trigger while the popup is open requests `open = true` with that trigger (FLT-PTR-03; FLT-ALG-16: a press on a different trigger always opens it); the popup is not closed, `onOpenChange(false)` is not called, the active trigger, `payload` and anchor switch to the new trigger (PSH-STA-31), and the popup/positioner elements are reused.
  Source: `floating-ui-react/hooks/useClick.ts:100-105`; `utils/popups/popupStoreUtils.ts:189-226`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "when the popup moves to another trigger"; detachedTriggersConformanceTests "reuses the popup DOM nodes when switching triggers", "opens with any trigger on click and marks only that trigger as active".
- **POP-PTR-04** — MUST. *Patient click*: when the popup was opened by hover and a `click` on the active trigger happens while the stick-if-open flag is still `true` (less than 500 ms after the hover open; POP-STA-11), the click requests `open = true` again instead of closing (FLT-PTR-06): the popup stays open, its open event becomes click-like (so leaving no longer closes it, POP-PTR-10), `openChangeReason` becomes `trigger-press` (`data-pressed` appears) and, with `modal: true`, the internal backdrop appears. A click 500 ms or more after the hover open closes the popup.
  Source: `popover/trigger/PopoverTrigger.tsx:96-97`; `popover/store/PopoverStore.ts:161-168`; `floating-ui-react/hooks/useClick.ts:117-120`; `floating-ui-react/components/FloatingRootStore.ts:91-101`. Test: [L4] `PopoverTrigger.test.tsx` "closes the popover if the user clicks patiently", "sticks if the user clicks impatiently", "should keep the popover open when re-hovered and clicked within the patient threshold"; `PopoverRoot.test.tsx` "enables modal behavior after a hover-open is clicked"; `PopoverRoot.detached-triggers.test.tsx` "when a press takes over a hover-opened popup".
- **POP-PTR-05** — MUST. A `click` on an `openOnHover` trigger while a hover open is still pending (rest delay not elapsed) opens the popup by press; the pending rest-delay open is discarded (a delayed hover open never overrides a click-like open), the popup is press-owned (focus moves in, leaving does not close, focus returns to the trigger on close), and after a close via `Popover.Close` the trigger can be hover-opened again.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:468-485`; `floating-ui-react/components/FloatingRootStore.ts:91-101`. Test: [L4] `PopoverTrigger.test.tsx` "sticks when clicked before the hover delay completes"; `PopoverPopup.test.tsx` "returns focus to the trigger if opened by click before the hover delay completes"; `PopoverRoot.test.tsx` "reopens on hover after an impatient click is followed by a close button press". Docs: CHANGELOG.md v1.1.0 "Prevent disabling focus management when clicking trigger before hover delay completes (#3572)".
- **POP-PTR-06** — MUST. With `openOnHover` and the trigger enabled, the hover interaction is configured `mouseOnly: true`, `move: false`, `restMs: delay`, `delay: { close: closeDelay }`, `handleClose: safePolygon()` (no pointer-events blocking): with `delay === 0`, a `mouseenter` on the trigger requests an open with reason `trigger-hover` (trigger = that element) immediately; with `delay > 0`, `mouseenter` does nothing and the open is requested once a `mousemove` on the trigger has been followed by `delay` ms without further movement of 2 px² or more (FLT-PTR-12, FLT-PTR-16). A pointer whose recorded type is `touch` (or any non-mouse/pen type) never triggers hover opening (FLT-PTR-11).
  Source: `popover/trigger/PopoverTrigger.tsx:82-94`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:216-300,423-497`. Test: [L4] `PopoverRoot.test.tsx` "should open after delay with rest type by default"; `PopoverTrigger.test.tsx` "sticks if the user clicks impatiently" (`delay={0}`, opens on `mouseEnter`); popupConformanceTests via `openOnHover` variants.
- **POP-PTR-07** — MUST. When a hover-opened popup's trigger receives `mouseleave` (and the pointer is not moving onto another enabled trigger), a safe-polygon `mousemove` handler is installed on the document; when the cursor leaves the polygon between the trigger and the popup (or the popup itself), a close with reason `trigger-hover` is requested after `closeDelay` ms (immediately when `0`), provided the open is still hover-like and the trigger is still the active trigger (FLT-PTR-14/15, FLT-ALG-15).
  Source: `popover/trigger/PopoverTrigger.tsx:86-90`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:194-206,302-360`; `floating-ui-react/safePolygon.ts:89-`. Test: [L4] `PopoverRoot.test.tsx` "should close after delay", "closes after hovering out of a popup opened by its trigger"; `PopoverTrigger.test.tsx` "closes the popover if the user clicks patiently" (leaving afterwards does not reopen).
- **POP-PTR-08** — MUST. A `disabled` Trigger never opens on hover (the hover interaction is not enabled), in addition to never opening on press.
  Source: `popover/trigger/PopoverTrigger.tsx:83`. Test: [L4] `PopoverTrigger.test.tsx` "does not open on hover when disabled".
- **POP-PTR-09** — MUST. While the popup is open by a `trigger-press` whose interaction type was `touch` and the active trigger has `openOnHover`, the hover interaction of **every** `openOnHover` trigger of this popup is disabled (POP-STA-12 `isTouchPressOpen`), so a stray hover over a sibling trigger does not switch the content; when opened by mouse press, hovering a sibling trigger does switch (POP-PTR-11).
  Source: `popover/trigger/PopoverTrigger.tsx:79,83`; `popover/store/PopoverStore.ts:65-66`. Test: [L4] `PopoverTrigger.test.tsx` "keeps ownership on the tapped trigger when a sibling trigger is hovered", "hands ownership to a hovered sibling trigger when opened by mouse".
- **POP-PTR-10** — MUST. When the popup is *click-like open* (opened by press, or hover-opened then clicked within the patient window, or the user pressed a nested-popup trigger inside it; FLT-STA-06), `mouseleave` on the trigger or the popup does not close it and the safe-polygon handler, if any, is removed.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:302-306`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:229-233`. Test: [L4] `PopoverTrigger.test.tsx` "sticks if the user clicks impatiently" (leave after click); `PopoverRoot.test.tsx` "cleans up the safe polygon handler after a hover-opened popup becomes click-sticky".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); gap 16, ADR 04 Amendments 2026-10-29): the press records its click-like open event on its request (FLT-STA-03's port note), so on Server a leave right after the press no longer finds a hover open and the popup stays.
- **POP-PTR-11** — MUST. While open, `mouseenter` on an inactive `openOnHover` trigger requests `open = true` with that trigger immediately (no rest delay), handing the popup over (PSH-STA-31); leaving the previous trigger towards an enabled trigger does not start a close.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:254-283,317-319`. Test: [L4] `PopoverTrigger.test.tsx` "hands ownership to a hovered sibling trigger when opened by mouse"; `PopoverRoot.detached-triggers.test.tsx` "hands off hover between detached triggers when the rendered component retains a stale ref".
- **POP-PTR-12** — MUST. While the active trigger has `openOnHover` and is not disabled (store values, POP-STA-05), the positioner element (the floating element) handles `mouseenter` (cancels a pending hover close) and `mouseleave` (closes with `trigger-hover` after `closeDelay` when the open is hover-like and not click-like, unless the pointer moves to an enabled trigger or into a child popup; FLT-PTR-23/24).
  Source: `popover/popup/PopoverPopup.tsx:66`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:156-255`. Test: [L4] `PopoverRoot.test.tsx` "closes after hovering out of a popup opened by its trigger".
- **POP-PTR-13** — MUST. A popup that became open through the controlled `open` prop, `defaultOpen`, a handle or `actionsRef` (no hover open event recorded) does not close when the pointer leaves the trigger or the popup.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:73-75,230-232`; `floating-ui-react/hooks/useHoverShared.ts:70-72`. Test: [L4] `PopoverRoot.test.tsx` "does not close after hovering out of a popup opened externally", "does not close after hovering out of a popup opened without trigger hover". Docs: CHANGELOG.md v1.5.0 "Fix controlled hover leave close (#4893)".
- **POP-PTR-14** — MUST. A touch press (pointerdown with `pointerType: 'touch'` followed by click) opens and closes the popup like a mouse click; the open interaction type is `touch`, so the default initial focus target is the Popup element itself (POP-FOC-02) and, with `modal: true`, scroll locking follows POP-PTR-19.
  Source: `utils/useOpenInteractionType.ts:13-27`; `utils/popups/popupStoreUtils.ts:37-40`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "when a touch press opens and closes the popup"; `PopoverRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width".
- **POP-PTR-15** — MUST. The click interaction ignores `mousedown` entirely (default `event: 'click'`) and acts on `click` only; a non-primary button therefore never toggles the popup, and right-clicks inside the popup do not count as outside presses (FLT-PTR-02/03).
  Source: `floating-ui-react/hooks/useClick.ts:138-151`. Test: none in Popover. Docs: CHANGELOG.md v1.0.0-beta.2 "Fix outside click after right clicking in popup (#2508)".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): proven on Popover by `popover.spec.ts` "port: a right press neither toggles the popover nor counts as an outside press inside it" (L4, both modes).
- **POP-PTR-16** — MUST. `pointerdown`/`pointerenter` on a Trigger record the pointer type for the hover interaction (FLT-PTR-11) and `pointerdown` on a Trigger records the pending click's interaction type (PSH-PTR-01); a keydown on the trigger resets the click interaction's remembered pointer type (FLT-KEY-08).
  Source: `popover/trigger/PopoverTrigger.tsx:136-139`; `floating-ui-react/hooks/useClick.ts:127-137,220-222`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:419-425`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "dismisses a keyboard-reopened popup on a virtual outside click after a touch session".
- **POP-PTR-17** — MUST. A hover-reason open change is applied synchronously within the event (flushed before the handler returns; PSH-ANI-03, EVD-EVT-18), so a `mouseenter` with `delay: 0` leaves the popup observably open as soon as the event is dispatched. Server: relaxed, see §9.
  Source: `popover/store/PopoverStore.ts:161-172`. Test: [L4] `PopoverTrigger.test.tsx` "sticks if the user clicks impatiently" (`data-popup-open` right after `mouseEnter`).
- **POP-PTR-18** — MUST. With `modal: true` and a non-hover open, the internal backdrop (POP-DOM-22) blocks pointer interaction with everything outside the popup except the active trigger's rectangle (PSH-PTR-05/06); a press on the backdrop is an outside press (POP-DIS-01). With `modal: 'trap-focus'` or `false`, or when hover-opened, no backdrop exists and outside elements remain interactive.
  Source: `popover/positioner/PopoverPositioner.tsx:98,128-130`. Test: [L4] `PopoverRoot.test.tsx` "should render an internal backdrop when `true`", "uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown".
- **POP-PTR-19** — MUST. With `modal === true`, the popup open and not hover-opened, page scroll is locked (UTL scroll lock, reference = the active trigger) when the open interaction type is not `touch`; when it is `touch`, scroll is locked only if the positioner's `offsetWidth` is at least the viewport width minus 20 px (both > 0), measured post-commit, pre-paint (PSH-PTR-07).
  Source: `popover/positioner/PopoverPositioner.tsx:98-105`; `utils/useAnchoredPopupScrollLock.ts:11-43`. Test: [L4] `PopoverRoot.test.tsx` "applies scroll lock when a touch-opened popup covers the viewport width", "does not apply scroll lock when a touch-opened popup is narrower than the viewport". Docs: `popover/root/PopoverRoot.tsx:169` JSDoc.
- **POP-PTR-20** — MUST. While the popup is in its closing transition after a hover-initiated close (`transitionStatus === 'ending'`, `isClosing` option), a `mouseenter` on the same trigger (or any enabled trigger) requests `open = true` immediately, without waiting for the rest delay (FLT-PTR-12/13).
  Source: `popover/trigger/PopoverTrigger.tsx:92-93`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:258-283`. Test: [L4] `PopoverRoot.test.tsx` "reopens immediately when re-hovering the trigger during a hover close transition". Docs: CHANGELOG.md v1.9.0 "Keep transition state consistent when reopening a retained popup (#5738)".
- **POP-PTR-21** — MUST. Once a hover-opened popup becomes click-like (POP-PTR-04), the document `mousemove` safe-polygon listener installed by an earlier `mouseleave` is removed, and a later `mouseleave` of the popup does not close it.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:142-150,302-306`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:223-233`. Test: [L4] `PopoverRoot.test.tsx` "cleans up the safe polygon handler after a hover-opened popup becomes click-sticky".
- **POP-PTR-22** — MUST. A `click` on an enabled `Popover.Close` requests a close with reason `close-press` and the native event; the Close works whether or not the popup is open (a request while closed calls `onOpenChange(false)` and changes nothing).
  Source: `popover/close/PopoverClose.tsx:42-46`. Test: [L2] `PopoverClose.test.tsx` "should close popover when clicked", "renders when popover is closed"; `PopoverRoot.test.tsx` "focuses the trigger after the popover is closed but not unmounted".

### 6.3 Focus management

Popover configures the shared focus manager ([floating-and-positioning](../architecture/floating-and-positioning.md) §FOC) on the Popup with: `openInteractionType = openMethod`, `modal = focusManagerModal` (POP-STA-10), `disabled = !mounted || openChangeReason === 'trigger-hover'`, `initialFocus` (POP-API-21), `returnFocus = finalFocus` (POP-API-22; the default `true` is implicit), `restoreFocus: 'popup'`, `getInsideElements = [before-trigger guard]`, `previousFocusableElement = active trigger element` (when it is an HTML element), `nextFocusableElement = trigger focus target guard`, `beforeContentFocusGuardRef`, `closeOnFocusOut` default `true`. Source: `popover/popup/PopoverPopup.tsx:107-125`.

- **POP-FOC-01** — MUST. Focus management (initial focus, focus trap, outside marking, guards, focus return, focus-out closing) is active only while the popup is mounted **and** the last applied open reason is not `trigger-hover` (FLT-FOC-19); a hover-opened popup never moves focus. When a hover-opened popup is clicked (POP-PTR-04) the reason becomes `trigger-press` and focus management starts for that session.
  Source: `popover/popup/PopoverPopup.tsx:112`. Test: [L4] `PopoverRoot.test.tsx` "does not move focus to the popover when opened with hover", "does not change focus when opened with hover and closed"; `PopoverPopup.test.tsx` "returns focus to the trigger if opened by click before the hover delay completes".
- **POP-FOC-02** — MUST. On open (with management active), focus moves — in a microtask after the commit and then on the next animation frame (synchronously for a virtual/assistive-technology press; FLT-FOC-06, FLT-FOC-22) — to the first tabbable element inside the Popup, else the Popup element; when the open interaction type is `touch` the Popup element itself is focused (PSH-FOC-01); focus is not moved when it is already inside the Popup, when the popup closed meanwhile, or when `initialFocus` resolves to `false`/`undefined` (PSH-FOC-02/03).
  Source: `popover/popup/PopoverPopup.tsx:68-69,113`; `utils/popups/popupStoreUtils.ts:37-40`; `floating-ui-react/components/FloatingFocusManager.tsx:679-769`. Test: [L4] popupFocusPropsTests "focuses the first focusable element by default", "focuses the element of a ref", "does not move focus when `false`", "passes the interaction type to a function, and does nothing when it returns undefined", "focuses the right element when a function returns $name", "is not called again when the popup closes"; `PopoverRoot.test.tsx` "restores temporarily disabled focus before focusing a reopened keepMounted popover".
- **POP-FOC-03** — MUST. `initialFocus` as a function is called once per open with the open interaction type (`keyboard` for Enter/Space, `mouse`/`pen`/`touch` for pointer presses, `''` when unknown) and never on close (PSH-FOC-03).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:693-697`. Test: [L4] popupFocusPropsTests "passes the interaction type to a function, and does nothing when it returns undefined", "is not called again when the popup closes". Docs: CHANGELOG.md v1.0.0-beta.5 "Fix `initialFocus` as function being called on close (#2949)".
- **POP-FOC-04** — MUST. Tabbability inside a kept-mounted, closed, non-modal popup is temporarily removed when focus leaves the portal (FLT-FOC-17) and is restored before the next open's initial focus runs, so reopening a `keepMounted` popover focuses its first tabbable element again.
  Source: `floating-ui-react/components/FloatingPortal.tsx:196-234`. Test: [L4] `PopoverRoot.test.tsx` "restores temporarily disabled focus before focusing a reopened keepMounted popover". Docs: CHANGELOG.md v1.5.0 "Fix non-modal focus-out close and tabindex management (#5030)".
- **POP-FOC-05** — MUST. On close (with management active), focus returns in a microtask after the popup's focus manager is disabled or unmounts (FLT-FOC-07) to the `finalFocus` target (POP-API-22); the default is the active trigger element when it is connected, else the element focused before the open (for a programmatic open — `openMethod === null` — the previously focused element is preferred over the trigger, FLT-FOC-20), else the most recently recorded focused element; with a keyboard close `focusVisible: true` is requested (FLT-KEY-09). With a kept-mounted popup the return happens when the popup closes, not when it unmounts.
  Source: `popover/popup/PopoverPopup.tsx:110,114,117-119`; `floating-ui-react/components/FloatingFocusManager.tsx:776-952`. Test: [L4] popupFocusPropsTests "focuses the trigger by default", "focuses the element of a ref", "focuses the right element when a function returns $name", "does not move focus when `false`", "passes the close type to a function"; `PopoverRoot.test.tsx` "focuses the trigger after the popover is closed but not unmounted"; `PopoverRoot.detached-triggers.test.tsx` "returns focus to the active trigger when opening programmatically from body focus", "returns focus to the previous element when the trigger unmounts while open".
- **POP-FOC-06** — MUST. Focus return is suppressed (FLT-FOC-08) when the close reason is `focus-out` with a focus guard as trigger (the guard already moved focus), when the close is a hover close from `mouseleave`, when focus has moved outside the popup tree to an element other than the return target before the close (default, non-explicit `finalFocus`), and on an outside press when the engine does not support `focus({ preventScroll })`; a virtual (assistive-technology) outside press and a nested popover's outside press still return focus.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:796-843,917-934`. Test: [L4] `PopoverRoot.test.tsx` "moves focus to the element following the trigger, excluding the popup, when tabbing forward from the open popup" (focus stays on the target), "does not change focus when opened with hover and closed".
- **POP-FOC-07** — MUST. `restoreFocus: 'popup'`: when the focused element inside the Popup is removed or becomes invisible (e.g. `display: none`) so that focus falls to `<body>`, the Popup element is focused immediately and again on the next frame (FLT-FOC-12); an outside press afterwards still dismisses.
  Source: `popover/popup/PopoverPopup.tsx:115`; `floating-ui-react/components/FloatingFocusManager.tsx:505-530`. Test: [L4] `PopoverPopup.test.tsx` "focuses the popup when the active element becomes display:none"; `PopoverRoot.test.tsx` "moves focus to the popup when a focused child is removed on pointerdown and outside press still dismisses". Docs: CHANGELOG.md v1.0.0-beta.2 "Restore focus to popup when focused element is removed (#2479)".
- **POP-FOC-08** — MUST. In focus-manager-modal mode (POP-STA-10; requires a Close part inside the Popup): focus is trapped inside the Popup (POP-KEY-09), every element outside the inside set is hidden from assistive technology (POP-ARIA-09), the trigger focus guards are not rendered, and focus-out closing is disabled (FLT-FOC-11 applies only to non-modal). With `modal: true` but no Close part, none of this applies (the popup behaves as non-modal for focus while still rendering the internal backdrop and locking scroll).
  Source: `popover/popup/PopoverPopup.tsx:71-72,111`; `floating-ui-react/components/FloatingFocusManager.tsx:551-567,619-677`. Test: [L4] `PopoverClose.test.tsx` "enables modal focus management when `modal=%s` and close is rendered", "does not enable modal focus management when close is not rendered"; `PopoverRoot.test.tsx` "should keep trigger focus guards when `true` without a close part", "when a modal popup closes from Popover.Close". Docs: CHANGELOG.md v1.3.0 "Trap focus when `<Popover.Close>` is rendered inside and `modal` is `true`… (#4084)"; `popover/root/PopoverRoot.tsx:171-176` JSDoc.
- **POP-FOC-09** — MUST. The trigger focus guards (POP-STR-14, PSH-FOC-07) exist only around the trigger that owns the open popup; when the popup moves to another trigger the guards move with it, and they are removed as soon as the popup is no longer open (before the exit animation ends).
  Source: `popover/store/PopoverStore.ts:67-68`; `popover/trigger/PopoverTrigger.tsx:157-170`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "when the popup moves to another trigger", "renders focus guards around the active detached trigger and reports it on close".
- **POP-FOC-10** — MUST. The before-trigger guard is reported to the focus manager as an inside element (PSH-FOC-09), so <kbd>Shift</kbd>+<kbd>Tab</kbd> from the trigger onto it is not treated as focus leaving before the guard's own close handler runs; the active trigger and the trigger focus target are likewise excluded from outside marking (POP-ARIA-09).
  Source: `popover/popup/PopoverPopup.tsx:116,120`; `floating-ui-react/components/FloatingFocusManager.tsx:338-340,462-473`. Test: [L4] `PopoverRoot.test.tsx` "does not loop focus between guards when shift-tabbing out of the trigger".
- **POP-FOC-11** — MUST. Nested popovers return focus in close order: closing an inner popover (opened programmatically from a button inside the outer popup) returns focus to that button, and closing the outer one afterwards returns focus to the outer trigger.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:79-99,776-952` (module-level previously-focused stack plus each manager's return-focus handling, FLT-FOC-09). Test: [L4] `PopoverRoot.test.tsx` "returns focus through nested programmatic popovers in close order". Docs: CHANGELOG.md v1.5.0 "Fix programmatic focus return (#4849)".
- **POP-FOC-12** — MUST. Tabbing out of a nested Menu rendered inside the Popup closes the menu and moves focus to the next tabbable element inside the Popup; the popover stays open and visible.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:475-499` (tree ancestors are related nodes). Test: [L4] `PopoverRoot.test.tsx` "moves focus to the next element when tabbing out of a nested menu inside the popover".
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Menu "moves focus to the next element when tabbing out of a nested menu inside the popover"); the row's other titles run in `popover.spec.ts`, both modes.
- **POP-FOC-13** — MUST. Tabbing out of the popover (forward or backward) while a nested Combobox popup is open inside it closes the nested popup as well as (forward) the popover; tabbing backward to the trigger keeps the popover open and the trigger is never `aria-hidden`.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:439-569`. Test: [L4] `PopoverRoot.test.tsx` "closes a nested combobox popup when tabbing out of the popover", "closes a nested combobox popup when tabbing backward to the trigger".
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Combobox "closes a nested combobox popup when tabbing out of the popover", "closes a nested combobox popup when tabbing backward to the trigger"); the row's other titles run in `popover.spec.ts`, both modes.

### 6.4 Dismissal

Popover configures the dismiss interaction ([floating-and-positioning](../architecture/floating-and-positioning.md) §DIS) on the Root with `outsidePressEvent = { mouse: modal === 'trap-focus' ? 'sloppy' : 'intentional', touch: 'sloppy' }`, defaults otherwise (`escapeKey: true`, `outsidePress: true`, `bubbles` unset, `referencePress` never); the interaction is mounted whenever the popup is open, mounted, or the Root has a handle (PSH-STA-43). Source: `popover/root/PopoverRoot.tsx:86,232-265`.

- **POP-DIS-01** — MUST. Outside-press mode is resolved per press from the pointer type (FLT-API-02): for mouse/pen (and unknown) presses it is `intentional` when `modal` is `false` or `true` — the popup closes on an outside `click` whose primary-button `pointerdown` happened while the popup was open (or a press-less/virtual click), **not** on `mousedown`/`pointerdown` (FLT-PTR-28) — and `sloppy` when `modal === 'trap-focus'` — closes on the outside `pointerdown` (FLT-PTR-26); for touch it is always `sloppy` — closes on `touchend` within 1 s of a `touchstart` that moved more than 5 px, or during a `touchmove` of more than 10 px (FLT-PTR-27).
  Source: `popover/root/PopoverRoot.tsx:241-248`; `floating-ui-react/hooks/useDismiss.ts:351-374,488-508,522-563,664-712`. Test: [L4] `PopoverRoot.test.tsx` "uses intentional outside press with user backdrop (mouse): closes on click, not on mousedown", "uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown"; `PopoverRoot.detached-triggers.test.tsx` "dismisses a keyboard-reopened popup on a virtual outside click after a touch session". Docs: CHANGELOG.md v1.0.0-beta.1 "Use `click` event for outside press dismissal (#2275)", v1.8.0 "Ignore outside clicks from presses that began before open (#5378)".
- **POP-DIS-02** — MUST. A surviving outside press requests a close with reason `outside-press`, the native event and `trigger === undefined` (FLT-DIS-11); this works across shadow roots (a click on another element inside the same shadow root, or outside the shadow root that hosts the portal).
  Source: `floating-ui-react/hooks/useDismiss.ts:404-412,518`. Test: [L4] `PopoverRoot.test.tsx` "closing via outside press: works when clicking another element inside the same shadow root", "closing via outside press: works when clicking outside the shadow root", "rewires dismiss interactions after closing and reopening".
- **POP-DIS-03** — MUST. A press on any registered trigger of this popover (or inside one) is not an outside press (FLT-DIS-05): pressing an inactive trigger switches (POP-PTR-03), pressing the active trigger toggles (POP-PTR-02).
  Source: `floating-ui-react/hooks/useDismiss.ts:414-423`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "when the popup moves to another trigger".
- **POP-DIS-04** — MUST. A press inside the popup's own elements, inside the floating element of an open nested popup (tree child), or a press that starts inside the popup and ends outside (drag-out; the next outside click is suppressed once) does not close the popup (FLT-DIS-08, FLT-PTR-29); scrolling a nested scrollable popup by touch does not close it.
  Source: `floating-ui-react/hooks/useDismiss.ts:376-385,484-486,616-662,784-804`. Test: [L4] `PopoverRoot.test.tsx` "keeps the parent popover open when press starts in nested popover and ends outside", "should not close popover when scrolling nested popup on touch", "keeps the popover open when a nested menu opens via pointer using a shared container". Docs: CHANGELOG.md v1.0.0-beta.5 "Fix swiping or scrolling on nested popup dismissing popover on touch (#3011)".
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Menu "keeps the popover open when a nested menu opens via pointer using a shared container"; Combobox "should not close popover when scrolling nested popup on touch"); the row's other titles run in `popover.spec.ts`, both modes.
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); gap 15, withdrawn by the orchestrator 2026-10-29, ADR 04 Amendments 2026-10-29): the nested drag-out holds only within a 0 ms window, in Base UI as in the port. Base UI counts only a press inside the dismissing popup's own floating DOM subtree as starting inside (`useDismiss.ts:239-258`, on purpose for nested portals); its title passes because the child's `pointerdown` reaches the parent's capture handlers, whose inside mark is cleared after 0 ms, and the outside click arrives in the same tick. A real drag outlasts that window and closes the parent in both. `popover.spec.ts` replays the same-tick sequence as dispatched events, both modes.
- **POP-DIS-05** — MUST. Clicking the parent popover's popup closes a nested child popover (the click is outside the child's elements) and leaves the parent open (the click is inside the parent's own element).
  Source: `floating-ui-react/hooks/useDismiss.ts:399-402,484-486,792-803`. Test: [L4] `PopoverRoot.test.tsx` "should close child popover when clicking parent popover".
- **POP-DIS-06** — MUST. Outside presses on elements injected after the popup opened (no inert marker in their root, FLT-DIS-06) and non-touch presses on scrollbars (FLT-DIS-07) do not close the popup; the internal backdrop carries the inert marker so a press on it does close it (PSH-PTR-06).
  Source: `floating-ui-react/hooks/useDismiss.ts:404-448,450-482`. Test: [L4] `PopoverRoot.test.tsx` "uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown".
- **POP-DIS-07** — MUST. In non-modal mode (focus management active), when focus leaves the Popup or the trigger to an unrelated element (not the trigger, not inside the popup/portal/guards/inside elements, not a registered trigger, not a nested or ancestor popup) and no pointer-down on a click-trigger is in progress, the popup closes with reason `focus-out` after a microtask and focus return is suppressed (FLT-FOC-11); a `pointerdown` outside followed by focus moving outside closes it this way even before the outside `click` arrives.
  Source: `popover/popup/PopoverPopup.tsx:107-125` (`closeOnFocusOut` default); `floating-ui-react/components/FloatingFocusManager.tsx:446-568`. Test: [L4] `PopoverRoot.test.tsx` "closes as soon as focus leaves the popup on pointer down outside". Docs: CHANGELOG.md v1.0.0-rc.0 "Close when pressing focusable element outside (#3380)".
- **POP-DIS-08** — MUST. <kbd>Escape</kbd> dismissal follows POP-KEY-02..04; hover-out dismissal follows POP-PTR-07/12; focus-guard dismissal follows POP-KEY-07/08; `Popover.Close` follows POP-PTR-22; `actionsRef.current.close()` and `handle.close()` follow POP-EVT-11.
  Source: as cited. Test: [L4] as cited.
- **POP-DIS-09** — MUST. Popover never closes because its active trigger unmounts (POP-STA-04); the popup stays open and anchored to the retained element, and its `aria-controls`/focus return fall back per POP-ARIA-03 and POP-FOC-05.
  Source: `popover/root/PopoverRoot.tsx:58`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "returns focus to the previous element when the trigger unmounts while open". (L4 for the anchoring and focus-return clauses)
- **POP-DIS-10** — MUST. With a controlled `open={true}` whose owner ignores `onOpenChange`, every dismissal (Escape, outside press, focus-out, Close) is reported but the popup stays open (PSH-DIS-02).
  Source: `popover/store/PopoverStore.ts:139-159`; `utils/popups/store.ts:150`. Test: [L4] popupConformanceTests "is ignored when `open={true}` is passed, so Escape cannot close".

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onOpenChange` | `(open: boolean, details: Popover.Root.ChangeEventDetails) => void` | synchronously inside the originating input, before any state/DOM change, once per request | see POP-EVT-02 | yes (`details.cancel()`) |
| `onOpenChangeComplete` | `(open: boolean) => void` | after the enter animation finishes (`true`) / after the exit animation finishes and the popup unmounts (`false`) | — | no |

- **POP-EVT-01** — MUST. `onOpenChange(nextOpen, details)` is invoked exactly once per open-state change request, synchronously inside the handling of the originating event, **before** the floating-root dispatch, the store update and any DOM reflecting the new state (PSH-EVT-01, EVD-EVT-02/03); `details` is a change-event details object (EVD-API-01) with `preventUnmountOnClose()` (EVD-EVT-21).
  Source: `popover/store/PopoverStore.ts:110-183`. Test: [L2] popupConformanceTests "is called with the new open state and the change details"; `PopoverRoot.test.tsx` "should call onChange when the open state changes".
- **POP-EVT-02** — MUST. The `reason` values Popover emits, and their sources, are exactly:

  | `reason` | Emitted by | `event` | `trigger` |
  |---|---|---|---|
  | `trigger-press` | Trigger click/Enter/Space (open, close, switch) | `MouseEvent`/`PointerEvent` (`detail === 0` for keyboard) | the trigger element |
  | `trigger-hover` | hover open (`mouseenter`/`mousemove`), hover close (`mouseleave`, safe polygon, popup leave) | `MouseEvent` | the trigger element (open); `undefined` (close) |
  | `outside-press` | outside click / pointerdown / touch (POP-DIS-01) | `MouseEvent`/`PointerEvent`/`TouchEvent` | `undefined` |
  | `escape-key` | <kbd>Escape</kbd> keydown | `KeyboardEvent` | `undefined` |
  | `close-press` | `Popover.Close` click | `MouseEvent`/`PointerEvent`/`KeyboardEvent` | the active trigger (back-filled, POP-EVT-06) |
  | `focus-out` | focus leaving in non-modal mode; trigger/portal focus guards | `FocusEvent` | the guard element (guards) or `undefined` |
  | `imperative-action` | `actionsRef.current.close()`, `handle.close()`, `handle.open(id)` | synthetic `base-ui` event (EVD-API-04) | `undefined` (close) / the registered trigger (open) |
  | `trigger-focus`, `none` | **never** (declared only; POP-EVT-10) | — | — |

  Source: `popover/root/PopoverRoot.tsx:208-217`; `floating-ui-react/hooks/useClick.ts:83`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:198,203,280,292,297,482`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:168,242`; `floating-ui-react/hooks/useDismiss.ts:220,518`; `popover/close/PopoverClose.tsx:44`; `utils/popups/useTriggerFocusGuards.ts:46`; `floating-ui-react/components/FloatingFocusManager.tsx:566`; `floating-ui-react/components/FloatingPortal.tsx:284-287`; `popover/root/PopoverRoot.tsx:78`; `utils/popups/popupHandle.ts:259-262,284`. Test: [L4] `PopoverRoot.test.tsx` "closing via outside press: works when clicking outside the shadow root" (`outside-press`, `trigger` undefined); `PopoverClose.test.tsx` "keeps the trigger when closing with a tooltip trigger close button" (`close-press`); `PopoverRoot.detached-triggers.test.tsx` "renders focus guards around the active detached trigger and reports it on close".
- **POP-EVT-03** — MUST. Calling `details.cancel()` inside `onOpenChange` stops the request entirely: no floating-root dispatch, no trigger re-association, no `openChangeReason`/`instantType` update, no state change (EVD-EVT-07); for a canceled Escape the keydown is not default-prevented (EVD-EVT-11). Server: relaxed, see §9.
  Source: `popover/store/PopoverStore.ts:139-143`. Test: [L2] `PopoverRoot.test.tsx` "onOpenChange cancel() prevents closing from a close press without changing the trigger"; popupConformanceTests "keeps the popup closed when the open change is canceled". (L4 for the Escape clause)
- **POP-EVT-04** — MUST. `details.preventUnmountOnClose()` called during `onOpenChange` for a non-canceled close keeps the popup mounted in its closing phase until `actionsRef.current.unmount()` (POP-ANI-06, PSH-EVT-05, EVD-EVT-21); called during an open request, or during a request that is then canceled, it has no effect, and it does not leak into a second close requested synchronously from the same callback.
  Source: `popover/store/PopoverStore.ts:121-123,147-159`; `utils/popups/popupStoreUtils.ts:189-236`. Test: [L2] `PopoverRoot.test.tsx` "preventUnmountOnClose()" › "does not leak from a canceled close into a synchronous second close", "unmounts on a normal close after a prevented close and initially open remount", "unmounts on a normal close after preventUnmountOnClose and external reopen".
- **POP-EVT-05** — MUST. `details.trigger` is the initiating trigger element for `trigger-press` and hover opens and for `handle.open(id)` (EVD-API-07), the focus guard element for guard-driven `focus-out` closes, and `undefined` for `outside-press`, `escape-key`, hover closes and imperative closes; a controlled consumer derives the new `triggerId` from `details.trigger?.id` (PSH-EVT-08).
  Source: `floating-ui-react/hooks/useClick.ts:83`; `utils/popups/useTriggerFocusGuards.ts:46`; `floating-ui-react/hooks/useDismiss.ts:220,518`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant when a controlled close commits late" (`openedTriggerIds`), "returns focus to the active trigger when opening programmatically from body focus"; `PopoverRoot.test.tsx` "closing via outside press: works when clicking outside the shadow root". Docs: `components/popover/page.mdx` §Controlled mode with multiple triggers.
- **POP-EVT-06** — MUST. For a `close-press` close whose details carry no trigger, `details.trigger` is back-filled **before** `onOpenChange` is called with the element registered under the effective active-trigger id, else the stored active trigger element, else left `undefined` (an active id that no mounted trigger carries) (PSH-EVT-04, EVD-EVT-16).
  Source: `popover/store/PopoverStore.ts:125-137`. Test: [L2] `PopoverClose.test.tsx` "keeps the trigger when closing with a tooltip trigger close button", "falls back to the active trigger element when the active trigger id is unregistered", "reports no trigger when the active trigger id has no mounted trigger". Docs: CHANGELOG.md v1.4.0 "Preserve active trigger on close press (#4741)".
- **POP-EVT-07** — MUST. `onOpenChangeComplete(true)` fires once per open after the Popup element's enter animation finishes (immediately, next frame + microtask, when there is none; TRN-EVT-01); `onOpenChangeComplete(false)` fires once per close from the unmount cleanup, after the exit animation finishes or when `unmount()` runs (TRN-EVT-02, PSH-STA-45); neither fires on mount while closed; a popup that mounts open fires `true` once (TRN-EVT-04). Server: relaxed, see §9.
  Source: `popover/popup/PopoverPopup.tsx:56-64`; `utils/popups/popupStoreUtils.ts:586-595`. Test: [L4] popupConformanceTests "prop: onOpenChangeComplete" (5 tests); `PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted"; `PopoverRoot.test.tsx` "does not repeat closing completion when `unmount` is called after the automatic unmount".
- **POP-EVT-08** — MUST. `onOpenChange` is not called on mount and not called when the consumer changes the controlled `open`/`triggerId` props (PSH-EVT-03); such prop changes do not update `openChangeReason`, `instantType` (except the clearing of POP-ANI-07) or the recorded open event.
  Source: `popover/root/PopoverRoot.tsx:47-48`. Test: [L2] `PopoverRoot.test.tsx` "does not close after hovering out of a popup opened externally"; `PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a prop-driven close after a switch".
- **POP-EVT-09** — MUST. When the popup moves between triggers while open (POP-PTR-03/11, `handle.open(otherId)`), `onOpenChange(true, details)` is called with the new trigger; `onOpenChange(false, …)` is **not** called for the hand-off.
  Source: `floating-ui-react/hooks/useClick.ts:100-105`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:278-283`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant when a controlled close commits late" (`openedTriggerIds` equals `['trigger-1', 'trigger-2']`).
- **POP-EVT-10** — MUST. The reasons `trigger-focus` and `none` are part of `Popover.Root.ChangeEventReason` but are never emitted in v1.9.0: no focus-open interaction is wired on the Trigger, and the active-trigger-unmount close is not enabled (§16 Q1).
  Source: `popover/root/PopoverRoot.tsx:58,210,217`; `popover/trigger/PopoverTrigger.tsx` (no `useFocus`). Test: none.
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): no port title. A reason that is never emitted has no observation; `EventReason` declares `TriggerFocus` and `None` for the type surface, and every emitted reason is asserted by the rows that emit it.
- **POP-EVT-11** — MUST. `actionsRef.current.close()` and `handle.close()` request a close with reason `imperative-action`, no event target and no trigger (PSH-DIS-03); `handle.open(id)` requests an open with reason `imperative-action` and `trigger` = the registered element (PSH-STA-38); all three are no-ops while no Root is attached (handle) or the Root is unmounted.
  Source: `popover/root/PopoverRoot.tsx:78`; `popover/store/PopoverHandle.ts:29-40`; `utils/popups/popupHandle.ts:213-285`. Test: [L2] `PopoverRoot.test.tsx` "closes the popover when the `close` method is called"; detachedTriggersConformanceTests "opens by trigger id with its payload and closes", "ignores imperative calls while no root is attached and follows the root across remounts".
- **POP-EVT-12** — MUST. A `trigger-hover` change is committed synchronously inside the event handler (so animation queries in the same task see the new state); other reasons may commit in the normal batched manner (PSH-ANI-03, EVD-EVT-18). Server: relaxed, see §9.
  Source: `popover/store/PopoverStore.ts:161-172`. Test: [L4] `PopoverRoot.test.tsx` "closes after hovering out of a popup opened by its trigger" (`queryByRole('dialog')` right after `mouseLeave`).

## 8. Form integration

Not applicable — Popover has no value, no `name`, and does not register with Field or Form.

## 9. Lifecycle, mounting and animation

Vocabulary: *post-commit, pre-paint*, *next frame*, *microtask* and *on animations finished* as defined in conventions §4. Popover uses the shared store-backed lifecycle of [transitions-and-mounting](../architecture/transitions-and-mounting.md) (`useUnmountAfterClose` through `useOpenStateTransitions`, default options: `animateInitialOpen` off) observing the **Popup element**; the completion watcher for `onOpenChangeComplete(true)` also observes the Popup element.

- **POP-ANI-01** — MUST. `mounted` becomes `true` synchronously with an applied open request and stays `true` after a close until the Popup element's finite animations finish (TRN-ANI-03, PSH-ANI-01); on unmount the store sets `activeTriggerId`/`activeTriggerElement` to `null`, `mounted` to `false`, `preventUnmountingOnClose` to `false`, then Popover's own cleanup sets the stick-if-open flag to `true` and `openChangeReason` to `null`, then `onOpenChangeComplete(false)` fires (TRN-ANI-27). Server: relaxed, see §9.
  Source: `popover/root/PopoverRoot.tsx:59-62`; `utils/popups/popupStoreUtils.ts:573-608`. Test: [L4] `PopoverRoot.test.tsx` "should close when the anchor is clicked twice"; `PopoverRoot.detached-triggers.test.tsx` "keeps the popup open when the last hovered trigger is pressed" (stick-if-open reset on unmount).
- **POP-ANI-02** — MUST. The transition status read by Popup and Backdrop is the store's derived status (PSH-STA-05, TRN-STA-14): `'starting'` whenever the popup is effectively open and not yet `mounted`, else the stored status — `'starting'` in the opening commit, unset on the next frame, `'ending'` in the closing commit, unset once unmounted (default machine, TRN-STA-03/04/06/08).
  Source: `utils/popups/store.ts:182-183`; `utils/popups/popupStoreUtils.ts:579-605`. Test: [L4] `PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted" (`[{open, 'starting'}, {open, undefined}]` per opening).
- **POP-ANI-03** — MUST. With `keepMounted` on the Portal, the portal node, Positioner, Popup and their contents stay in the DOM while closed (Positioner `hidden`, `pointer-events: none`, POP-DOM-13/14); the same Popup element is reused on reopen, and each reopen goes through `'starting'` exactly once (TRN-ANI-01, PSH-ANI-14).
  Source: `popover/portal/PopoverPortal.tsx:24-27`; `popover/positioner/PopoverPositioner.tsx:122-123`. Test: [L2] `PopoverPopupTransitionState.test.tsx` "starts each opening once when the popup is kept mounted" (`popup.isConnected`, same element); `PopoverRoot.test.tsx` "focuses the trigger after the popover is closed but not unmounted". (L4 for the `'starting'` clause)
- **POP-ANI-04** — MUST. A popup that is open on its first render (`defaultOpen`, controlled `open`) mounts already `mounted` and never enters `'starting'` (TRN-STA-13, PSH-ANI-02, PSH-STA-44); the Backdrop and Popup observe only `transitionStatus === undefined` during that mount.
  Source: `popover/store/PopoverStore.ts:226-228`; `utils/popups/popupStoreUtils.ts:598-603`. Test: [L2] `PopoverBackdrop.test.tsx` "does not start an entry phase when initially open (controlled=%s)"; `PopoverRoot.test.tsx` "ignores `unmount` while the popup is open" (no `data-starting-style`).
- **POP-ANI-05** — MUST. `actionsRef.current.unmount()`: while the popup is closed and still mounted it ends the closing phase immediately (TRN-ANI-22) and fires `onOpenChangeComplete(false)` once; while the popup is open it is deferred to the current batch's commit and dropped if the committed state is still open (TRN-ANI-20) — the popup stays open, no completion fires, and a later ordinary close still unmounts automatically; after an automatic unmount it is a no-op (TRN-ANI-19); two calls in one batch, or `close()` + `unmount()` in one batch, complete the close once (TRN-ANI-21).
  Source: `popover/root/PopoverRoot.tsx:74-81`; `internals/useUnmountAfterClose.ts`. Test: [L2] `PopoverRoot.test.tsx` "unmounts the popover when the `unmount` method is called", "does not repeat closing completion when `unmount` is called after the automatic unmount", "ignores `unmount` while the popup is open", "unmounts when `close` and `unmount` are called in one batch", "still unmounts on a later close after `unmount` was called while open", "still unmounts on a later close after `unmount` and a reopen in one batch", "completes closing once when `unmount` is called twice in one batch".
- **POP-ANI-06** — MUST. When `preventUnmountOnClose()` was called for the applied close, automatic unmounting is suspended: the popup stays mounted with `data-ending-style` and `aria-expanded="false"` on the trigger until `unmount()` is called (TRN-ANI-23); the opt-out belongs to that close cycle only — the next open (including a controlled reopen, or a Root remount with `defaultOpen`) clears it, so a later ordinary close unmounts automatically (TRN-ANI-24); an opt-out recorded for a controlled close that commits later (e.g. in a low-priority transition) survives until that close commits (TRN-ANI-26).
  Source: `popover/store/PopoverStore.ts:121-123,147-159`; `utils/popups/popupStoreUtils.ts:195-201`. Test: [L2] `PopoverRoot.test.tsx` "unmounts on a normal close after preventUnmountOnClose and external reopen", "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen", "keeps the opt-out when a controlled close is applied in a transition", "preventUnmountOnClose()" › "unmounts on a normal close after a prevented close and initially open remount". (L4 for the `data-ending-style` clause)
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): "keeps the opt-out when a controlled close is applied in a transition" is restated as `PopoverRootTests` "port: keeps the opt-out when a controlled close is applied after the callback returns" (L2, per trigger layout): React's `startTransition` commits the consumer's close in a later render, and Blazor has no transitions, so the consumer applies the close after its callback.
- **POP-ANI-07** — MUST. Trigger switch transition (TRN-ANI-28/29/31): when the popup is open and the active trigger element changes to a different element, `instantType` is cleared in that commit (so the Positioner may transition to the new anchor), and once the Positioner's finite animations finish (next-frame inspection) it is set to `'trigger-change'` provided the popup is still open; a close by any route (including a controlled `open={false}`) aborts a pending restoration and clears a stored `'trigger-change'`; a trigger element change while closed never schedules a restoration; the public value hides `'trigger-change'` while closed. Consequently `data-instant="trigger-change"` is never present during a close that follows a switch, nor after a reopen on the same trigger. Server: relaxed, see §9.
  Source: `popover/positioner/PopoverPositioner.tsx:89-96`; `internals/useTriggerSwitchTransition.ts:49-112`; `popover/store/PopoverStore.ts:54-57`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "does not apply the trigger-change instant to a hover close after switching triggers", "… to a non-hover close after switching triggers", "… after switching back to the original trigger", "does not restore the trigger-change instant after a reopen on the same trigger", "does not apply the trigger-change instant to a prop-driven close after a switch", "does not restore the trigger-change instant when a controlled close commits late", "does not restore the trigger-change instant after a hover close has started". Docs: CHANGELOG.md v1.9.0 "Fix hover-out animations when switching between detached triggers (#4261)".
- **POP-ANI-08** — MUST. `onOpenChangeComplete` timing is as in POP-EVT-07; with animations disabled (`BASE_UI_ANIMATIONS_DISABLED`, TRN-EDGE-01) both completions are synchronous.
  Source: `popover/popup/PopoverPopup.tsx:56-64`. Test: [L4] popupConformanceTests "is called on open once the enter animation finishes", "is called on close once the exit animation finishes".
- **POP-ANI-09** — MUST. Reopening during the exit animation (hover re-entry, POP-PTR-20; or a press) keeps the same Popup element, removes `data-ending-style`, re-applies `data-open` without `data-closed`, and does not fire `onOpenChangeComplete(false)` for the cancelled close (TRN-STA-09, TRN-EVT-05).
  Source: `utils/popups/popupStoreUtils.ts:579-605`; `internals/useTransitionStatus.ts`. Test: [L4] `PopoverRoot.test.tsx` "reopens immediately when re-hovering the trigger during a hover close transition"; `PopoverRoot.detached-triggers.test.tsx` "does not restore the trigger-change instant after a reopen on the same trigger".
- **POP-ANI-10** — MUST. Viewport content transitions (PSH-ANI-05..11) run when the active trigger changes while open: a `[data-previous]` clone with `data-ending-style` and a remounted `[data-current]` with `data-starting-style` for one frame, cleaned up on animations finished; a trigger that remounts while the popup is closed and retained does not start a transition; auto-resize (PSH-DOM-11, PSH-ANI-12/13) measures on open and on each content-key change. Popover passes `openOn: 'click'` to the shared conformance suite and otherwise uses the shared behavior unchanged.
  Source: `popover/viewport/PopoverViewport.tsx:28-32`; `utils/usePopupViewport.tsx:75-297`. Test: [L4] viewportConformanceTests "remounts the `current` container when the active trigger changes", "does not restart a transition when the active trigger remounts while closed and retained (strict: $strict)", "creates morphing containers during transitions", "creates morphing containers after a kept-mounted popup closes and reopens", "shows the latest content after rapid trigger changes", "keeps the latest transition active during rapid trigger changes", "cleans up the transition when a lagging payload remounts the current container". Docs: CHANGELOG.md v1.9.0 "Don't start a viewport transition when the trigger remounts while closed (#5887)".
- **POP-ANI-11** — MUST. Interaction handlers (dismiss listeners and the trigger/popup prop bags) are mounted while the popup is open or mounted, and permanently while the Root has a `handle` (PSH-STA-42/43); after a close-and-unmount without a handle they are torn down and re-created on the next open, with identical behavior.
  Source: `popover/root/PopoverRoot.tsx:86,91,232-265`. Test: [L2] `PopoverRoot.test.tsx` "rewires dismiss interactions after closing and reopening".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- POP-ANI-01 — via TRN-ANI-03, TRN-ANI-09 (adopted by this section): the unmount commit (`mounted` cleared, store cleanup, `onOpenChangeComplete(false)`) lands in the first render batch after the `transitions` module's completion message, about two round trips after the exit animation ends; the optimistic convergent write lets JS set `hidden` at animation end so the element is not visible meanwhile.
- POP-EVT-07 — via TRN-ANI-09: `onOpenChangeComplete(false)` runs with that unmount commit, about two round trips after the exit animation ends, not in the same microtask.
- POP-PTR-17, POP-EVT-12 — EVD-EVT-18, PSH-ANI-03: the `trigger-hover` open commit lands a round trip later; same-task animation queries do not see it.
- POP-KEY-07, POP-KEY-08 — PSH-KEY-07: the focus-guard `focus-out` close is committed a round trip later instead of synchronously.
- POP-ANI-07 — ADR 01's mapping row "on animations finished" (added 2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); design point 7 of the track, settled by the orchestrator 2026-10-28): the `trigger-change` instant comes from the `popup` module's `OnTriggerChangeSettled` report once the Positioner's animations finish, so it lands a round trip after them; the clear on the switch itself is written at commit on both modes.
- POP-KEY-02, POP-EVT-03 (Escape clause) — EVD-EVT-11, EVD-DIS-01, FLT-KEY-01: the consumer's `cancel()` / `allowPropagation()` decision arrives a round trip after dispatch ends; JS applies the ADR 04 §3 fallback (prevent and stop whenever this popover is the innermost open popup); WebAssembly meets the spec through the synchronous `decide()` path.
- POP-STA-13 — RND-STA-01 for mirrored fields: `data-side`, `data-align`, `data-anchor-hidden`, `data-uncentered` and the transition attributes are JS-written at frame timing; the state objects' `side`, `align`, `anchorHidden`, `uncentered` and `transitionStatus` (and so `ClassValue`/`StyleValue`) update one round trip later.

## 10. Positioning

Popover delegates positioning to the shared anchored-positioning session ([floating-and-positioning](../architecture/floating-and-positioning.md) §POS) through the shared Positioner element (FLT-DOM-01..07). Component-specific configuration: `collisionAvoidance` default `{ fallbackAxisSide: 'end' }` (`POPUP_COLLISION_AVOIDANCE`), `collisionBoundary` default `'clipping-ancestors'`, `keepMounted` from the Portal, `nodeId` from the floating tree, `adaptiveOrigin` from the store (set while a Viewport is mounted), `mounted` from the store; everything else uses the shared defaults.

- **POP-POS-01** — MUST. Defaults: `side: 'bottom'`, `align: 'center'`, `sideOffset: 0`, `alignOffset: 0`, `positionMethod: 'absolute'`, `collisionBoundary: 'clipping-ancestors'`, `collisionPadding: 5`, `arrowPadding: 5`, `sticky: false`, `disableAnchorTracking: false`.
  Source: `popover/positioner/PopoverPositioner.tsx:32-51`; `internals/useAnchorPositioning.ts:142-153`. Test: [L4] `PopoverPositioner.test.tsx` "points to the anchor center for center alignment" (default placement `bottom`/`center`); positionerConformanceTests "rests exactly at collisionPadding from the colliding edge".
- **POP-POS-02** — MUST. `collisionAvoidance` defaults to `{ fallbackAxisSide: 'end' }`, so with the per-key defaults (FLT-API-10) the popup flips side and alignment and may fall back to the perpendicular axis ending at the `end` side; consumer values replace the object as a whole.
  Source: `popover/positioner/PopoverPositioner.tsx:49`; `internals/constants.ts:26-28`. Test: [L4] `PopoverPositioner.test.tsx` "points to the flipped edge when the alignment flips" (`collisionAvoidance={{ align: 'flip' }}`), "points to the anchor center when the popup is shifted to overlap the anchor" (`{ side: 'shift' }`). Docs: CHANGELOG.md v1.0.0-beta.0 "Add `collisionAvoidance` prop (#1849)".
- **POP-POS-03** — MUST. By default the position anchor is the active trigger element (PSH-POS-01, FLT-STR-10): switching triggers repositions the popup to the new trigger (positioner left edge within 1 px of an `align="start"` trigger's), a controlled `triggerId` selects the anchor, and the anchor is retained through the exit transition so a closing popup does not move.
  Source: `popover/positioner/PopoverPositioner.tsx:57-61,89`; `utils/popups/popupStoreUtils.ts:81-88`. Test: [L4] `PopoverRoot.detached-triggers.test.tsx` "keeps positioning correct when conditional triggers unmount and the tree remounts"; `PopoverPositioner.test.tsx` "remains anchored to the trigger when closing from a tooltip trigger close"; detachedTriggersConformanceTests "reuses the popup DOM nodes when switching triggers".
- **POP-POS-04** — MUST. An explicit `anchor` (element, virtual element, function or DOM handle; FLT-API-11) replaces the trigger as the position anchor; with `keepMounted` the custom anchor is observed for size changes.
  Source: `popover/positioner/PopoverPositioner.tsx:36,70`. Test: [L4] `PopoverPositioner.test.tsx` "observes a custom anchor for keepMounted auto-updates".
- **POP-POS-05** — MUST. Anchor tracking (ancestor scroll, element resize, layout shift; FLT-POS-11) is on by default and follows the trigger when it moves, with and without `keepMounted`; `disableAnchorTracking` stops the popup from following when an ancestor scrolls.
  Source: `popover/positioner/PopoverPositioner.tsx:48,82`. Test: [L4] `PopoverPositioner.test.tsx` "remains anchored if keepMounted=false", "remains anchored if keepMounted=true", "does not follow the anchor when its ancestor scrolls".
- **POP-POS-06** — MUST. `sideOffset`/`alignOffset` accept numbers or functions (FLT-API-12, FLT-POS-06); the positioner rests exactly `collisionPadding` from a colliding boundary edge (FLT-POS-15).
  Source: `popover/positioner/PopoverPositioner.tsx:41-46,76-80`. Test: [L4] positionerConformanceTests "offsets the positioner when a number is specified", "offsets the positioner when a function is specified", `it.each(callbackDataCases)`, "rests exactly at collisionPadding from the colliding edge". Docs: CHANGELOG.md v1.0.0-alpha.6 "Add `OffsetFunction` for `sideOffset` and `alignOffset` (#1223)".
- **POP-POS-07** — MUST. `--transform-origin` (FLT-DOM-07) on the Positioner is `<cross> <side>` for vertical sides and `<side> <cross>` for horizontal sides, where: `<cross>` is `0%`/`100%` for a `start`/`end`-aligned popup with no Arrow whose cross-axis shift is at most 1 px (for vertical sides `start`/`end` swap under the floating element's own RTL direction; an `alignOffset` keeps the aligned edge); otherwise it is the center of the arrow in `px` along the cross axis (the Arrow part when present, else the stand-in arrow of POP-POS-08), which equals the anchor's center for `center` alignment and for a popup shifted on the cross axis by more than 1 px; `<side>` is `calc(100% + <sideOffset>px)` for `top`/`left` and `-<sideOffset>px` (`0px` when the offset is 0) for `bottom`/`right`, except that for vertical sides with side-axis shifting enabled (`collisionAvoidance.side: 'shift'`, FLT-POS-05) and a main-axis shift larger than `sideOffset` it is the anchor's vertical center in `px` relative to the popup.
  Source: `internals/useAnchorPositioning.ts:392-452` (transform-origin middleware, FLT-DOM-07/FLT-ALG-11). Test: [L4] `PopoverPositioner.test.tsx` "transform origin" (19 tests: center/start/end, RTL variants, horizontal sides, sideOffset, alignOffset, narrow anchor with arrowPadding, flipped alignment, 1 px cross-axis shift, shifted start/end/horizontal alignment, arrow present, overlap). Docs: CHANGELOG.md v1.0.0-beta.2 "Fix `transform-origin` variable calculation when Positioner `sideOffset` is a function (#2511)".
- **POP-POS-08** — MUST. Without an Arrow part, `arrowPadding` MUST NOT displace the popup (a stand-in arrow element with padding 0 is used, FLT-DOM-10/FLT-POS-08); with an Arrow, the arrow is positioned by the arrow middleware with `padding = arrowPadding` and `uncentered` reports a non-zero center offset.
  Source: `popover/positioner/PopoverPositioner.tsx:47,78`; `popover/arrow/PopoverArrow.tsx:24-36`. Test: [L4] `PopoverPositioner.test.tsx` "does not let the virtual arrow displace the popup on a narrow anchor", "points to the arrow when present regardless of alignment".
- **POP-POS-09** — MUST. While a Viewport is mounted the store's `adaptiveOrigin` middleware is set (PSH-POS-02), switching the Positioner to `top`/`left`-style coordinates (FLT-DOM-04) and freezing `--positioner-width`/`--positioner-height` to the measured popup size (PSH-DOM-11); unmounting the Viewport reverts to transform positioning.
  Source: `popover/positioner/PopoverPositioner.tsx:67,86`; `utils/usePopupViewport.tsx:112-117`. Test: [L4] positionerConformanceTests "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts". Docs: `components/popover/page.mdx` §API reference › Viewport note.
- **POP-POS-10** — MUST. The Positioner registers a floating-tree node (`FloatingNode` with the session's `nodeId`) so nested popovers/menus rendered inside the Popup become tree children for dismissal (POP-DIS-04/05, POP-KEY-04) and focus (POP-FOC-12) purposes (FLT-STR-07/08).
  Source: `popover/positioner/PopoverPositioner.tsx:55,84,131`. Test: [L4] `PopoverRoot.test.tsx` "should close child popover when clicking parent popover", "keeps the parent popover open when press starts in nested popover and ends outside".
- **POP-I18N-01** — MUST. In RTL (ancestor `DirectionProvider direction="rtl"`, or `dir="rtl"` on the Positioner), `align="start"` places the popup at the anchor's right edge and `align="end"` at its left (FLT-I18N-01/02), `inline-start`/`inline-end` sides resolve to `right`/`left`, and `--transform-origin` uses `100%` for `start` and `0%` for `end`; the direction of the floating element itself is honored when it differs from the provider.
  Source: `popover/positioner/PopoverPositioner.tsx:69-87`; `internals/useAnchorPositioning.ts:200-205`. Test: [L4] `PopoverPositioner.test.tsx` "points to the popup logical start edge for start alignment in RTL", "uses the floating element direction for RTL alignment", "points to the popup logical end edge for end alignment in RTL". Docs: CHANGELOG.md v1.4.0 "Fix RTL component behavior (#4840)".

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Popup store, trigger registry, handles, payload, implicit active trigger | [popup-store-and-handles](../architecture/popup-store-and-handles.md) | `PopoverStore` adds the fields of §4.2; `createNullPopoverStore()` fallback with `setOpen` no-op; `useImplicitActiveTrigger(store)` with `closeOnActiveTriggerUnmount` off; `useOpenStateTransitions(open, store, onUnmount)` with Popover's cleanup (POP-ANI-01); handle component name `'Popover'`, `throwOnMissingTrigger: true` |
| Trigger data forwarding | PSH-STA-26..30 | forwards `{ payload, disabled, openOnHover, closeDelay }` |
| Trigger focus guards | PSH-STR-10, PSH-KEY-01..05, PSH-FOC-07/09 | rendered iff `isOpenedByTrigger && !focusManagerModal` |
| Open interaction type | PSH-PTR-01/02, PSH-STA-41 | `useOpenMethodTriggerProps` on every Trigger; written to the store |
| Internal backdrop | PSH-STR-09, PSH-DOM-14, PSH-PTR-05/06 | rendered iff `mounted && modal === true && openChangeReason !== 'trigger-hover'`, `cutout` = active trigger, `inert` while closed |
| Anchored scroll lock | PSH-PTR-07 | `enabled = open && modal === true && !hover-opened`, `touchOpen = openMethod === 'touch'` |
| Viewport / auto-resize | PSH-STR-07, PSH-DOM-07..12, PSH-ANI-05..13 | `usePopupViewport({ store, side, children })`; `popupViewportStateMapping` |
| Close-part counting | PSH-FOC-08 | `useClosePartCount` in Popup, `useClosePartRegistration` in Close |
| Click interaction (`useClick`) | [floating-and-positioning](../architecture/floating-and-positioning.md) FLT-PTR-01..06 | `stickIfOpen: () => store.context.stickIfOpen`; defaults otherwise (`event: 'click'`, `toggle: true`, `touchOpenDelay: 0`, reason `trigger-press`) |
| Hover interactions | FLT-PTR-11..25, FLT-ALG-15, FLT-ALG-24 | Trigger: `enabled: !disabled && openOnHover && !isTouchPressOpen`, `mouseOnly: true`, `move: false`, `handleClose: safePolygon()` (no `blockPointerEvents`), `restMs: delay`, `delay: { close: closeDelay }`, `triggerElementRef`, `isActiveTrigger: isTriggerActive`, `isClosing: () => transitionStatus === 'ending'`. Popup: `useHoverFloatingInteraction(floatingContext, { enabled: openOnHover && !disabled, closeDelay })` |
| Dismiss interaction (`useDismiss`) | FLT-DIS-01..16, FLT-KEY-01..03, FLT-PTR-26..29 | `outsidePressEvent: { mouse: modal === 'trap-focus' ? 'sloppy' : 'intentional', touch: 'sloppy' }`; prop bags routed as `activeTriggerProps = inactiveTriggerProps = dismiss.reference`, `popupProps = dismiss.floating` |
| Focus manager (`FloatingFocusManager`) | FLT-FOC-05..22, FLT-KEY-04..07, FLT-ARIA-01..03 | see §6.3 preamble |
| Portal (`FloatingPortal`) | FLT-API-13, FLT-DOM-11/12/14, FLT-ARIA-04 | `container`, no `portalOwnerRole` |
| Floating tree | FLT-STR-06..09 | `FloatingTree` at the outermost Root; `FloatingNode` in the Positioner |
| Anchored positioning (`useAnchorPositioning` + `usePositioner`) | FLT-API-08..12, FLT-DOM-01..10, FLT-POS-01..17 | §10 preamble; `hidden: !mounted`, `inert: !open` |
| Transition status, unmount-after-close, completion watcher, trigger-switch transition, mount-transition suppression | [transitions-and-mounting](../architecture/transitions-and-mounting.md) TRN-STA-14, TRN-ANI-18..31, TRN-EVT-01/02/04, TRN-DOM-05/10 | default options; completion watcher on the Popup element (`batch` off); `getDisabledMountTransitionStyles` on Positioner and Popup |
| Button mechanism | [button-behavior](../architecture/button-behavior.md) UBT-API-02 | Trigger: `{ disabled, native: nativeButton }`; Close: `{ disabled, native: nativeButton, focusableWhenDisabled: false }` |
| Rendering pipeline, state attributes, ids | [rendering-and-composition](../architecture/rendering-and-composition.md) | `useRenderElement` on every element part; `triggerOpenStateMapping`/`pressableTriggerOpenStateMapping` (Trigger), `popupStateMapping` (Positioner via `usePositioner`, Arrow), `popupTransitionStateMapping` (Popup, Backdrop), `popupViewportStateMapping` (Viewport); `useBaseUiId` for Trigger, Title, Description |
| Event details and reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) EVD-ALG-04, EVD-EVT-16/17/21 | Popover's own open-change sequence (`PopoverStore.setOpen`) |
| Timers, scroll lock, visually hidden, platform, `NullStore`, `inertValue` | [shared-utilities](../architecture/shared-utilities.md) UTL-EVT-01..03, UTL-DOM-01..12, UTL-DOM-13/15, UTL-STA-34 | `Timeout` for stick-if-open (500 ms); `useScrollLock` through the anchored scroll lock |

## 12. Edge cases, invariants and dev warnings

- **POP-EDGE-01** — MUST. A `Popover.Trigger` rendered with neither an ancestor `Popover.Root` nor a `handle` throws `Error("Base UI: <Popover.Trigger> must be either used within a <Popover.Root> component or provided with a handle.")` (all environments).
  Source: `popover/trigger/PopoverTrigger.tsx:52-56`. Test: [L2] `PopoverTrigger.test.tsx` "throws a descriptive error when rendered without a root or a handle".
- **POP-EDGE-02** — MUST. Any of Portal, Positioner, Popup, Arrow, Backdrop, Viewport, Title, Description or Close rendered outside a `Popover.Root` throws `Error("Base UI: PopoverRootContext is missing. Popover parts must be placed within <Popover.Root>.")`.
  Source: `popover/root/PopoverRootContext.ts:11-19`. Test: [L2] `PopoverPopup.test.tsx` "throws a descriptive error when rendered outside <Popover.Root>".
- **POP-EDGE-03** — MUST. A `Popover.Positioner` rendered outside a `Popover.Portal` throws `Error("Base UI: <Popover.Portal> is missing.")`.
  Source: `popover/portal/PopoverPortalContext.ts:6-12`. Test: [L2] `PopoverPositioner.test.tsx` "throws a descriptive error when rendered outside <Popover.Portal>".
- **POP-EDGE-04** — MUST. A `Popover.Popup`, `Popover.Arrow` or `Popover.Viewport` rendered outside a `Popover.Positioner` throws `Error("Base UI: PopoverPositionerContext is missing. PopoverPositioner parts must be placed within <Popover.Positioner>.")`.
  Source: `popover/positioner/PopoverPositionerContext.ts:19-27`. Test: [L2] `PopoverPopup.test.tsx` "throws a descriptive error when rendered outside <Popover.Positioner>".
- **POP-EDGE-05** — MUST. `handle.open(id)` with a Root attached but no trigger registered under `id` (searching every attached Root's registry newest-first, then the fallback registry; PSH-STA-38) throws `Error("Base UI: PopoverHandle.open() was called with the trigger id \"<id>\", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted Popover.Trigger that has this handle set on its \"handle\" prop.")` (all environments; `throwOnMissingTrigger: true`).
  Source: `popover/store/PopoverHandle.ts:18`; `utils/popups/popupHandle.ts:241-249`. Test: [L2] detachedTriggersConformanceTests "throws when opened with an unregistered trigger id".
- **POP-EDGE-06** — SHOULD. In development, `handle.open()` / `handle.close()` while no Root is attached log `console.warn("Base UI: PopoverHandle.open() was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.")` / `"Base UI: PopoverHandle.close() was called while no root using this handle is mounted. The call was ignored."` and do nothing; a handle attached to more than one mounted Root for longer than one frame logs `console.warn("Base UI: A handle is attached to more than one mounted root at the same time. The most recently mounted root takes over and the previous one stops being controlled by the handle. A handle should be used by a single root that stays mounted for the lifetime of the handle.")` (PSH-STA-35).
  Source: `utils/popups/popupHandle.ts:153-173,216-224,274-281`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "opens by trigger from a descendant layout effect on initial mount" (asserts the warning is *not* logged); detachedTriggersConformanceTests "ignores imperative calls while no root is attached and follows the root across remounts", "resolves a trigger still registered to the previous root during a transient overlap".
  Port note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): see POP-API-09's note; the port title asserts that no "no root using this handle is mounted" warning is written when a descendant opens the popover in its first render.
- **POP-EDGE-07** — MUST. A Root unmounted while open and remounted with the same handle starts closed with no payload; imperative calls made while it was unmounted are not replayed (PSH-STA-40); a remount with `defaultOpen` starts open and a later ordinary close unmounts normally (POP-ANI-06).
  Source: `popover/root/PopoverRoot.tsx:115-130`. Test: [L2] detachedTriggersConformanceTests "ignores imperative calls while no root is attached and follows the root across remounts"; `PopoverRoot.test.tsx` "preventUnmountOnClose()" › "unmounts on a normal close after a prevented close and initially open remount". Docs: CHANGELOG.md v1.7.0 "Prevent `<Popover.Root>` from reopening after remounting with a reused handle (#5149)".
- **POP-EDGE-08** — MUST. A detached Trigger keeps working when its DOM ancestors are added or removed, when it is reparented, and when the handle is recreated (Fast Refresh); its registration migrates between the fallback store and the Root store without detaching the consumer's DOM handle, and a substitute element whose ref callback is retained from the first render (stale ref) still registers on the attached store, including after its host node is swapped (PSH-STA-26/36). Port: withdrawn (ADR 02 Amendments).
  Source: `popover/trigger/PopoverTrigger.tsx:64-76,132-135`; `utils/popups/popupStoreUtils.ts:139-178,377-380`. Test: [L4] detachedTriggersConformanceTests "keeps a detached trigger working $name" (4 cases); `PopoverRoot.detached-triggers.test.tsx` "hands off hover between detached triggers when the rendered component retains a stale ref", "keeps registration on the attached store when a stale-ref component swaps its host node", "does not detach the consumer ref when the handle attaches to a root". Docs: CHANGELOG.md v1.8.0 "Fix detached trigger store migration (#5442)".
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): the three stale-ref titles ("hands off hover between detached triggers when the rendered component retains a stale ref", "keeps registration on the attached store when a stale-ref component swaps its host node", "does not detach the consumer ref when the handle attaches to a root") exercise the withdrawn clause, a React ref callback retained from the first render, and have no port counterpart; the row's other clauses are proven by detachedTriggersConformanceTests "keeps a detached trigger working $name".
- **POP-EDGE-09** — SHOULD. Opening, closing, switching triggers, hover-opening and closing through `Popover.Close` of a handle-bound popover SHOULD NOT cause inactive detached Triggers to re-render: inactive detached Triggers do not re-render, because the Root keeps its interactions mounted and their inputs do not change (PSH-STA-43); observed through bUnit render counts (L2). This is a quality-of-implementation property with no DOM-observable effect.
  Source: `popover/root/PopoverRoot.tsx:83-86`; `popover/store/PopoverStore.ts:63-68`. Test: [L2] `PopoverRoot.detached-triggers.test.tsx` "does not re-render inactive triggers" (6 tests).
- **POP-EDGE-10** — MUST. `Popover.Positioner` does not accept `keepMounted` (type error); the prop belongs to the Portal.
  Source: `popover/positioner/PopoverPositioner.tsx:159-162`. Test: [L1] `PopoverPositioner.spec.tsx`. Docs: CHANGELOG.md v1.0.0-alpha.5 "Require `Portal` part… The `keepMounted` prop was removed from the Positioner."
- **POP-EDGE-11** — MUST. A Root nested inside another Popover's Popup (or inside a Menu/Dialog popup) positions, dismisses and manages focus independently; the `nested` flag is set so an outside press on a nested popover does not suppress focus return (FLT-FOC-08) and the parent is not dismissed by presses inside the child (POP-DIS-04).
  Source: `popover/root/PopoverRoot.tsx:103-113`; `utils/popups/popupStoreUtils.ts:76-77`. Test: [L4] `PopoverRoot.test.tsx` "nested popup interactions" (4 tests), "nested menu interactions" (2 tests).
  Open to M5 (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): the titles that nest a family not yet ported wait for it (Menu "nested menu interactions"); the row's other titles run in `popover.spec.ts`, both modes.
- **POP-EDGE-12** — MUST. A Popup whose role is overridden by the consumer still keeps Popover's dismissal and focus behavior; a consumer `id` on the Popup changes `aria-controls` accordingly (PSH-ARIA-03); a Title/Description with a consumer `id` publishes that id.
  Source: `popover/popup/PopoverPopup.tsx:88-103`. Test: [L2] popupConformanceTests "allows a custom `id` prop". (L4 for the dismissal and focus clause)
- **POP-EDGE-13** — MUST. Server rendering / hydration: a detached Trigger renders against the handle's stable fallback store on the server and during hydration (so its markup is closed: `aria-expanded="false"`, no `data-popup-open`), and switches to the Root's store after hydration (PSH-STA-34); a Root that mounts open renders without an enter transition (TRN-ANI-35).
  Source: `utils/popups/usePopupHandleStore.ts:31-35`; `utils/popups/popupHandle.ts:126-128`. Test: none in Popover (PSH `popupHandle.test.ts`). Docs: CHANGELOG.md v1.0.0-beta.6 "Fix for rendering of Alert Dialog, Dialog, Menu, Popover, and Tooltip in React Server Components (#3241)".
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): no Popover port title. The fallback-store clause is the handle's (PSH-STA-34, L1 `popupHandle.test.ts`); React's server render and hydration have no counterpart in Blazor, where a prerendered part re-renders interactively from its parameters; the no-enter-transition clause is TRN-ANI-35's, proven at L4 on Avatar and Collapsible.
- **POP-EDGE-14** — MUST. With `modal: true` and a touch-opened popup, the internal backdrop still blocks outside taps while page scroll stays free unless the positioner is viewport-wide (POP-PTR-19); with `modal: true` on a hover-opened popup neither the backdrop nor the scroll lock applies until the popup becomes press-owned (POP-PTR-04).
  Source: `popover/positioner/PopoverPositioner.tsx:98-105,128-130`. Test: [L4] `PopoverRoot.test.tsx` "enables modal behavior after a hover-open is clicked", "does not apply scroll lock when a touch-opened popup is narrower than the viewport". Docs: CHANGELOG.md v1.0.0-rc.0 "Fix modal backdrop on touch (#3383)".
- **POP-EDGE-15** — MUST. Owner-computed IDREFs behave as follows when a part is given an element-substitution callback (opaque; it is expected to apply the `id` it is handed, RND-DOM-32): the Popup's `aria-labelledby`/`aria-describedby` reference the id that `Popover.Title`/`Popover.Description` computed (consumer `id`, else generated) and published to the store, not the id actually rendered, so a callback that drops that `id` leaves the reference dangling; the Trigger's `aria-controls` uses the `id` read from the rendered Popup element (the floating id is used only while no Popup element is registered), so a callback that drops the Popup `id` makes `aria-controls` absent rather than dangling (§16 Q14).
  Source: `popover/title/PopoverTitle.tsx:22-28`; `popover/description/PopoverDescription.tsx:22-28`; `popover/popup/PopoverPopup.tsx:90-94`; `utils/popups/store.ts:151-154`; `internals/resolveRenderedId.ts:12-14`; see RND-DOM-32. Test: none.
  Note (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125); settled by the orchestrator 2026-10-29): no Popover port title. The owner-computed id rule is the pipeline's (RND-DOM-32, L2 `resolveRenderedId` titles); Popover's Title, Description and Popup consume it unchanged (POP-STR-10's conformance suites and the Title/Description id titles cover the parts).

## 13. Behaviors evidenced by tests

Coverage of every test by the requirements above. Shared suites are listed with the options Popover passes. No requirement exists only because of a test; every test maps to requirements already stated.

### `root/PopoverRoot.test.tsx`

Shared suite `popupConformanceTests({ triggerMouseAction: 'click', expectedPopupRole: 'dialog' })`:

| Test | Requirements |
|---|---|
| "opens the popup with the `open` prop" | POP-API-01, POP-STA-01 |
| "opens initially and remains uncontrolled" / "is ignored when `open={false}` is passed" | POP-API-02, POP-STA-01 |
| "is ignored when `open={true}` is passed, so Escape cannot close" | POP-API-01, POP-DIS-10, POP-KEY-02 |
| "is called with the new open state and the change details" | POP-EVT-01, POP-PTR-01 |
| "keeps the popup closed when the open change is canceled" | POP-EVT-03 |
| "has the dialog role on the popup" | POP-ARIA-04 |
| "has the `aria-controls` attribute on the trigger" | POP-ARIA-03, POP-STA-04, POP-DOM-23 |
| "opens on trigger click and sets `aria-expanded` on the trigger" | POP-ARIA-02, POP-DOM-25, POP-PTR-01 |
| "allows a custom `id` prop" | POP-DOM-23, POP-EDGE-12 |
| "is not called on mount when closed" / "is called on open when there is no enter animation" / "is called on close when there is no exit animation" / "is called on open once the enter animation finishes" / "is called on close once the exit animation finishes" | POP-API-04, POP-EVT-07, POP-ANI-08 |
| "removes the popup when there is no exit animation defined" | POP-DOM-10, POP-ANI-01 |

`describe.for` × {contained triggers, detached triggers, multiple detached triggers} (each title runs three times):

| Test | Requirements |
|---|---|
| "should render the children" | POP-STR-01, POP-STR-12 |
| "should close when the anchor is clicked twice" | POP-PTR-01, POP-PTR-02, POP-DOM-10, POP-ANI-01 |
| "rewires dismiss interactions after closing and reopening" | POP-ANI-11, POP-KEY-02, POP-DIS-02 |
| "should call onChange when the open state changes" | POP-API-01, POP-EVT-01 |
| "unmounts on a normal close after preventUnmountOnClose and external reopen" | POP-EVT-04, POP-ANI-06, POP-DOM-01 |
| "does not close after hovering out of a popup opened externally" | POP-PTR-13, POP-EVT-08 |
| "closes after hovering out of a popup opened by its trigger" | POP-PTR-07, POP-PTR-12, POP-API-16, POP-STA-05, POP-EVT-12 |
| "cleans up the safe polygon handler after a hover-opened popup becomes click-sticky" | POP-PTR-21, POP-PTR-10 |
| "keeps the popover open when a nested menu opens via Enter using a shared container" | POP-KEY-01, POP-EDGE-11, POP-API-20 |
| "keeps the popover open when a nested menu opens via pointer using a shared container" | POP-DIS-04, POP-EDGE-11 |
| "remains uncontrolled, so a trigger click closes it" | POP-API-02, POP-PTR-02 |
| "does not close after hovering out of a popup opened without trigger hover" | POP-PTR-13 |
| "should open after delay with rest type by default" | POP-API-17, POP-PTR-06 |
| "should close after delay" | POP-API-18, POP-PTR-07 |
| "reopens immediately when re-hovering the trigger during a hover close transition" | POP-PTR-20, POP-ANI-09, POP-DOM-26 |
| "onOpenChange cancel() prevents closing from a close press without changing the trigger" | POP-EVT-03, POP-EVT-06 |
| "unmounts on a later normal close after a preventUnmountOnClose cycle and reopen" | POP-EVT-04, POP-ANI-06 |
| "focuses the trigger after the popover is closed but not unmounted" | POP-API-19, POP-ANI-03, POP-FOC-05, POP-PTR-22 |
| "restores temporarily disabled focus before focusing a reopened keepMounted popover" | POP-FOC-04, POP-FOC-02, POP-DOM-13 |
| "does not move focus to the popover when opened with hover" | POP-FOC-01 |
| "does not change focus when opened with hover and closed" | POP-FOC-01, POP-FOC-06 |
| "moves focus to the element following the trigger, excluding the popup, when tabbing forward from the open popup" (×2 placements) | POP-KEY-07, POP-FOC-06 |
| "closes a nested combobox popup when tabbing out of the popover" | POP-KEY-07, POP-FOC-13, POP-DOM-25 |
| "closes a nested combobox popup when tabbing backward to the trigger" | POP-KEY-06, POP-FOC-13, POP-ARIA-09 |
| "moves focus to the trigger when tabbing backward from the open popup then to the popup when tabbing forward" (×3 placements) | POP-KEY-05, POP-KEY-06 |
| "closes and moves focus before the trigger when tabbing backward from the focused trigger" | POP-KEY-08 |
| "moves focus to the element following the trigger when tabbing forward from the open popup" | POP-KEY-07 |
| "moves focus to the element preceding the trigger when tabbing backward from the trigger while open" | POP-KEY-08 |
| "uses intentional outside press with user backdrop (mouse): closes on click, not on mousedown" | POP-DIS-01, POP-DOM-33 |
| "uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown" | POP-DIS-01, POP-DIS-06, POP-PTR-18, POP-DOM-12 |
| "closing via outside press: works when clicking another element inside the same shadow root" / "closing via outside press: works when clicking outside the shadow root" | POP-DIS-02, POP-EVT-02, POP-EVT-05, POP-API-20 |
| "closes as soon as focus leaves the popup on pointer down outside" | POP-DIS-07, POP-DOM-05 |
| "moves focus to the next element when tabbing out of a nested menu inside the popover" | POP-FOC-12 |
| "moves focus to the element following the trigger when tabbing forward from the trigger" (during the exit animation) | POP-KEY-10, POP-DOM-26 |
| "does not loop focus between guards when tabbing out of the popup" | POP-KEY-11, POP-DOM-24 |
| "does not loop focus between guards when shift-tabbing out of the trigger" | POP-KEY-11, POP-FOC-10 |
| "moves focus to the popup when a focused child is removed on pointerdown and outside press still dismisses" | POP-FOC-07 |
| "unmounts the popover when the `unmount` method is called" | POP-API-05, POP-ANI-05, POP-ANI-06 |
| "does not repeat closing completion when `unmount` is called after the automatic unmount" | POP-ANI-05, POP-EVT-07 |
| "keeps the opt-out when a controlled close is applied in a transition" | POP-ANI-06 |
| "ignores `unmount` while the popup is open" | POP-ANI-05, POP-ANI-04 |
| "unmounts when `close` and `unmount` are called in one batch" / "completes closing once when `unmount` is called twice in one batch" | POP-ANI-05 |
| "still unmounts on a later close after `unmount` was called while open" / "still unmounts on a later close after `unmount` and a reopen in one batch" | POP-ANI-05, POP-ANI-06 |
| "closes the popover when the `close` method is called" | POP-API-05, POP-EVT-11 |
| "should render an internal backdrop when `true`" | POP-STR-13, POP-DOM-22, POP-ARIA-08, POP-PTR-18 |
| "should only render focus guards inside the popup when `true`" | POP-STR-14, POP-STR-15, POP-DOM-31, POP-STA-12, POP-KEY-09 |
| "should keep trigger focus guards when `true` without a close part" | POP-STR-14, POP-DOM-08, POP-STA-10, POP-FOC-08, POP-KEY-07 |
| "should not render an internal backdrop when `false`" | POP-STR-13 |
| "enables modal behavior after a hover-open is clicked" | POP-PTR-04, POP-STR-13, POP-EDGE-14 |
| "reopens on hover after an impatient click is followed by a close button press" | POP-PTR-05 |
| "applies scroll lock when a touch-opened popup covers the viewport width" / "does not apply scroll lock when a touch-opened popup is narrower than the viewport" | POP-PTR-19, POP-PTR-14, POP-STA-08, POP-EDGE-14 |
| "returns focus through nested programmatic popovers in close order" | POP-FOC-11, POP-EDGE-11 |
| "keeps the parent popover open when press starts in nested popover and ends outside" | POP-DIS-04, POP-POS-10 |
| "should not close popover when scrolling nested popup on touch" | POP-DIS-04 |
| "should close child popover when clicking parent popover" | POP-DIS-05, POP-STR-02, POP-POS-10 |

Top-level `preventUnmountOnClose()`:

| Test | Requirements |
|---|---|
| "does not leak from a canceled close into a synchronous second close" | POP-EVT-04, POP-EVT-03 |
| "unmounts on a normal close after a prevented close and initially open remount" | POP-EVT-04, POP-ANI-06, POP-EDGE-07 |

### `root/PopoverRoot.detached-triggers.test.tsx`

| Test | Requirements |
|---|---|
| "opens by trigger from a descendant layout effect on initial mount" | POP-API-09, POP-API-26, POP-EDGE-06, POP-ARIA-02 |
| "hands off hover between detached triggers when the rendered component retains a stale ref" | POP-EDGE-08, POP-PTR-11 |
| "keeps registration on the attached store when a stale-ref component swaps its host node" | POP-EDGE-08, POP-API-26 |
| "does not detach the consumer ref when the handle attaches to a root" | POP-EDGE-08, POP-API-13 |
| detachedTriggersConformanceTests (`openInteractions: ['click']`, `ariaExpanded: true`, `throwOnMissingTrigger: true`): "opens with any trigger on click and marks only that trigger as active" (contained + detached) | POP-STR-12, POP-PTR-03, POP-ARIA-02 |
| "sets the payload of the active trigger and renders content based on it" | POP-API-10, POP-API-14, POP-STA-05 |
| "reuses the popup DOM nodes when switching triggers" | POP-PTR-03, POP-POS-03 |
| "marks the trigger matching a controlled `triggerId` as active" | POP-API-07, POP-STA-02 |
| "opens initially with the payload of a non-first `defaultTriggerId`" | POP-API-08 |
| "allows controlling the open state and active trigger programmatically" | POP-API-07, POP-EVT-05 |
| "does not leave an inline scale style on the popup after switching triggers" | POP-DOM-30 |
| "opens by trigger id with its payload and closes" | POP-API-26, POP-EVT-11 |
| "throws when opened with an unregistered trigger id" | POP-EDGE-05 |
| "ignores imperative calls while no root is attached and follows the root across remounts" | POP-API-26, POP-EDGE-06, POP-EDGE-07, POP-EVT-11 |
| "registers a detached trigger declared after the root" | POP-API-13, POP-STR-12 |
| "resolves a trigger still registered to the previous root during a transient overlap" | POP-API-26, POP-EDGE-06 |
| "keeps a detached trigger working $name" (4 cases) | POP-EDGE-08 |
| "when a click opens and closes the popup" / "when a touch press opens and closes the popup" / "when a modal popup closes from Popover.Close" / "when the popup moves to another trigger" / "when a hover opens the popup" / "when a press takes over a hover-opened popup" | POP-EDGE-09; additionally POP-PTR-14 (touch), POP-FOC-08 (modal Close), POP-STR-14 + POP-FOC-09 + POP-DOM-02 + POP-STA-14 + POP-DIS-03 (moves), POP-STA-11 (hover), POP-PTR-04 + POP-DOM-02 (press takes over) |
| "keeps the popup open when the last hovered trigger is pressed" | POP-STA-11, POP-ANI-01, POP-PTR-04 |
| "dismisses a keyboard-reopened popup on a virtual outside click after a touch session" | POP-DIS-01, POP-PTR-16 |
| "returns focus to the active trigger when opening programmatically from body focus" | POP-FOC-05, POP-EVT-05 |
| "returns focus to the previous element when the trigger unmounts while open" | POP-FOC-05, POP-DIS-09, POP-STA-04 |
| "does not apply the trigger-change instant to a hover close after switching triggers" / "does not apply the trigger-change instant to a non-hover close after switching triggers" / "does not apply the trigger-change instant after switching back to the original trigger" / "does not restore the trigger-change instant after a reopen on the same trigger" / "does not apply the trigger-change instant to a prop-driven close after a switch" / "does not restore the trigger-change instant when a controlled close commits late" / "does not restore the trigger-change instant after a hover close has started" | POP-ANI-07, POP-STA-07, POP-DOM-28, POP-DOM-18, POP-STA-13; the prop-driven case also POP-EVT-08; the late-commit case also POP-EVT-09 |
| "renders focus guards around the active detached trigger and reports it on close" | POP-STR-14, POP-FOC-09, POP-EVT-06, POP-DOM-04 |
| "keeps positioning correct when conditional triggers unmount and the tree remounts" | POP-POS-03, POP-API-07 |

### `trigger/PopoverTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`, inside `Popover.Root open`) | POP-STR-03, POP-API-11; RND/UBT conformance |
| "throws a descriptive error when rendered without a root or a handle" | POP-EDGE-01 |
| "disables the popover" | POP-API-12, POP-DOM-03, POP-DOM-07 |
| "custom element" | POP-API-11, POP-API-12, POP-DOM-06, POP-DOM-07 |
| "does not open on hover when disabled" | POP-PTR-08 |
| "keeps ownership on the tapped trigger when a sibling trigger is hovered" | POP-PTR-09, POP-STA-12, POP-STA-08, POP-ARIA-02 |
| "hands ownership to a hovered sibling trigger when opened by mouse" | POP-PTR-11, POP-API-14 |
| "should have the data-popup-open and data-pressed attributes when open by clicking" | POP-DOM-01, POP-DOM-02 |
| "should have the data-popup-open but not the data-pressed attribute when open by hover" | POP-DOM-02, POP-STA-06, POP-API-16 |
| "should not have the data-popup-open and data-pressed attributes when open by click when `openOnHover=true` and `delay=0`" | POP-DOM-01, POP-PTR-04 (title is historical; the test asserts `data-popup-open` is present) |
| "should have the data-popup-open and data-pressed attributes when open by click when `openOnHover=true`" | POP-DOM-02, POP-PTR-05 |
| "closes the popover if the user clicks patiently" | POP-PTR-04, POP-STA-11, POP-PTR-07 |
| "sticks if the user clicks impatiently" | POP-PTR-04, POP-STA-11, POP-PTR-06, POP-PTR-10, POP-PTR-17 |
| "sticks when clicked before the hover delay completes" | POP-PTR-05, POP-API-17 |
| "should keep the popover open when re-hovered and clicked within the patient threshold" | POP-PTR-04 |
| "should toggle closed with Enter or Space when rendering a <div>" | POP-KEY-01, POP-API-11, POP-STA-07 |

### `popup/PopoverPopup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside open Root/Portal/Positioner) | POP-STR-06; RND conformance |
| "throws a descriptive error when rendered outside <Popover.Root>" | POP-EDGE-02 |
| "throws a descriptive error when rendered outside <Popover.Positioner>" | POP-EDGE-04 |
| "should render the children" | POP-STR-06 |
| popupFocusPropsTests: "focuses the first focusable element by default" / "focuses the element of a ref" / "focuses the right element when a function returns $name" (3) / "does not move focus when `false`" / "passes the interaction type to a function, and does nothing when it returns undefined" / "is not called again when the popup closes" | POP-API-21, POP-FOC-02, POP-FOC-03 |
| popupFocusPropsTests: "focuses the trigger by default" / "focuses the element of a ref" / "focuses the right element when a function returns $name" (3) / "does not move focus when `false`" / "passes the close type to a function" | POP-API-22, POP-FOC-05 |
| "focuses the popup when the active element becomes display:none" | POP-FOC-07 |
| "returns focus to the trigger if opened by click before the hover delay completes" | POP-PTR-05, POP-FOC-01, POP-FOC-05 |
| "does not relay composite keys from the popup to the toolbar" / "keeps composite keys working inside the popup content" | POP-KEY-12 |

### `popup/PopoverPopupTransitionState.test.tsx`

| Test | Requirements |
|---|---|
| "starts each opening once when the popup is kept mounted" | POP-ANI-02, POP-ANI-03, POP-API-19, POP-EVT-07, POP-KEY-02 |

### `positioner/PopoverPositioner.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside open Root/Portal) | POP-STR-05; RND conformance |
| "throws a descriptive error when rendered outside <Popover.Portal>" | POP-EDGE-03 |
| positionerConformanceTests (`viewport: true`): "offsets the positioner when a number is specified" / "offsets the positioner when a function is specified" / `it.each(callbackDataCases)` / "rests exactly at collisionPadding from the colliding edge" | POP-POS-06, POP-POS-01 |
| positionerConformanceTests: "uses transform positioning without Viewport" / "uses top/left positioning with Viewport" / "updates positioning when Viewport mounts and unmounts" | POP-DOM-21, POP-POS-09 |
| "remains anchored if keepMounted=false" / "remains anchored if keepMounted=true" | POP-POS-05, POP-API-19 |
| "does not follow the anchor when its ancestor scrolls" | POP-POS-05 |
| "observes a custom anchor for keepMounted auto-updates" | POP-POS-04 |
| "remains anchored to the trigger when closing from a tooltip trigger close" | POP-POS-03, POP-STA-03 |
| "transform origin" › "points to the anchor center for center alignment" / "points to the popup start edge for start alignment" / "points to the popup end edge for end alignment" | POP-POS-07, POP-DOM-20 |
| "transform origin" › "points to the popup logical start edge for start alignment in RTL" / "points to the popup logical end edge for end alignment in RTL" / "uses the floating element direction for RTL alignment" | POP-POS-07, POP-I18N-01 |
| "transform origin" › "places the side coordinate first for horizontal sides" / "points to the popup end edge for horizontal end alignment" / "accounts for sideOffset" / "accounts for sideOffset on top side" / "keeps the popup start edge as the origin with alignOffset" | POP-POS-07, POP-POS-06 |
| "transform origin" › "does not let the virtual arrow displace the popup on a narrow anchor" | POP-POS-08, POP-POS-07 |
| "transform origin" › "points to the flipped edge when the alignment flips" | POP-POS-07, POP-DOM-16, POP-POS-02 |
| "transform origin" › "keeps the popup start edge as the origin for a 1px cross-axis shift" / "points to the anchor center when start alignment is shifted on the cross axis" / "points to the anchor center when end alignment is shifted on the cross axis" / "points to the anchor center when horizontal alignment is shifted on the cross axis" / "points to the anchor center when the popup is shifted to overlap the anchor" | POP-POS-07 |
| "transform origin" › "points to the arrow when present regardless of alignment" | POP-POS-07, POP-POS-08 |

### `arrow/PopoverArrow.test.tsx`, `portal/PopoverPortal.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (Arrow, `refInstanceof: HTMLDivElement`) | POP-STR-07, POP-DOM-32 |
| describeConformance (Portal, `refInstanceof: HTMLDivElement`, inside `Popover.Root open`) | POP-STR-04, POP-DOM-09 |

### `backdrop/PopoverBackdrop.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Popover.Root open`) | POP-STR-08 |
| "does not start an entry phase when initially open (controlled=%s)" (×2) | POP-ANI-04, POP-STA-13, POP-DOM-36 |
| "sets `pointer-events: none` style on backdrop if opened by hover" | POP-DOM-35, POP-STA-06 |
| "does not set `pointer-events: none` style on backdrop if opened by click" | POP-DOM-35 |

### `close/PopoverClose.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`) | POP-STR-11, POP-API-24, POP-DOM-43 |
| "renders when popover is closed" | POP-STR-11, POP-PTR-22 |
| "should close popover when clicked" | POP-PTR-22 |
| "keeps the trigger when closing with a tooltip trigger close button" | POP-EVT-06, POP-EVT-02, POP-API-15 |
| "falls back to the active trigger element when the active trigger id is unregistered" | POP-EVT-06, POP-STA-02 |
| "reports no trigger when the active trigger id has no mounted trigger" | POP-EVT-06, POP-API-08 |
| "enables modal focus management when `modal=%s` and close is rendered" (×2: `true`, `'trap-focus'`) | POP-API-06, POP-STA-10, POP-FOC-08, POP-ARIA-09, POP-KEY-09 |
| "does not enable modal focus management when close is not rendered" | POP-STA-10, POP-FOC-08, POP-ARIA-09 |

### `description/PopoverDescription.test.tsx`, `title/PopoverTitle.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (Description, `refInstanceof: HTMLParagraphElement`) / (Title, `refInstanceof: HTMLHeadingElement`) | POP-STR-10 |
| "describes the popup element with its id" | POP-ARIA-06, POP-STA-09, POP-DOM-42 |
| "labels the popup element with its id" | POP-ARIA-05, POP-STA-09, POP-DOM-42 |

### `viewport/PopoverViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside open Root/Portal/Positioner/Popup) | POP-STR-09 |
| viewportConformanceTests (`openOn: 'click'`): "renders children in the `current` container by default" | POP-API-23, POP-DOM-37 |
| "remounts the `current` container when the active trigger changes" | POP-ANI-10 |
| "does not restart a transition when the active trigger remounts while closed and retained (strict: $strict)" (×2) | POP-ANI-10, POP-EVT-04 |
| `it.each` side/direction cases (inline anchoring styles) | POP-DOM-30 |
| "creates morphing containers during transitions" | POP-DOM-37, POP-DOM-39, POP-DOM-41, POP-ANI-10 |
| "sets `data-activation-direction` from the previous to the new trigger" | POP-DOM-38 |
| "creates morphing containers after a kept-mounted popup closes and reopens" | POP-ANI-10, POP-DOM-15 |
| "shows the latest content after rapid trigger changes" / "keeps the latest transition active during rapid trigger changes" / "cleans up the transition when a lagging payload remounts the current container" | POP-ANI-10 |

### `root/PopoverRoot.spec.tsx`, `positioner/PopoverPositioner.spec.tsx`

Type-level only: `Popover.createHandle<number>()` types the `payload` of triggers and of the Root's function-as-child (`number | undefined`), a mismatched payload is a type error (POP-API-10, POP-API-14, §3.13); `Popover.Positioner` rejects `keepMounted` (POP-EDGE-10).


### Port titles (`tests/Blazix.Primitives.Tests/Popover/`, `tests/Blazix.Primitives.Tests.Components/Popover/`, `tests/browser/popover.spec.ts`)

Added 2026-10-29 (M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): the port's own titles, each with its layer and the requirements it proves.

| Test | Layer | Requirements |
|---|---|---|
| "port: the instant type follows the reason and the press's detail" | L1 | POP-STA-07 |
| "port: an applied open change writes its reason and instant type in one write, and a canceled one writes nothing" | L1 | POP-STA-06, POP-STA-07, POP-EVT-03 |
| "port: the close event is the one that closed the popover; a later close while closed keeps it" | L1 | POP-API-22 |
| "port: the stored trigger-change instant reads as none once closed, and is dropped when closed" | L1 | POP-STA-07, POP-ANI-07 |
| "port: the selectors of the triggers derive from the reason, the open method and the focus trap" | L1 | POP-STA-12 |
| "port: a close press without a trigger reports the registered active trigger" | L1 | POP-EVT-06 |
| "port: the positioner takes no KeepMounted parameter; the portal's is read" | L1 | POP-EDGE-10 |
| "port: the popup's focus manager takes Popover's props: not trapped, restoring to the popup, anchored at the active trigger" | L2 | POP-FOC-01, POP-STA-10 |
| "port: the focus manager traps focus only for a modal popover with a close part" | L2 | POP-STA-10, POP-FOC-02 |
| "port: a hover-opened popover disables the focus manager" | L2 | POP-FOC-08 |
| "port: a touch open focuses the popup itself by default" | L2 | POP-FOC-03 |
| "port: the initial focus resolver gets the open interaction type, and no target moves no focus" | L2 | POP-API-21 |
| "port: the final focus resolver gets the close interaction type and names the element focus returns to" | L2 | POP-API-22 |
| "port: the trigger asks the popup module to watch its guards while it owns the open, untrapped popover" | L2 | POP-STR-14, POP-FOC-04 |
| "port: a focused trigger guard closes the popover with focus-out, its event and the guard as the trigger" | L2 | POP-FOC-04, POP-EVT-05 |
| "port: the viewport mirrors the module's direction and transitioning flag in its state" | L2 | POP-DOM-38, POP-DOM-39, POP-STA-13 |
| "port: the trigger-change report writes the instant on the positioner, the popup and the viewport while open" | L2 | POP-ANI-07, POP-DOM-18, POP-DOM-28, POP-DOM-40 |
| "port: a keyboard press that moves the popover to another trigger clears the instant type in the render that applies it" | L2 | POP-ANI-07, POP-DOM-28 |
| "port: a controlled trigger id that moves the open popover clears the instant type in the render that applies it" | L2 | POP-ANI-07, POP-DOM-28 |
| "port: the positioner sends the cut-out and the anchored lock for a modal press, not for a hover, and the touch open" | L2 | POP-STR-13, POP-PTR-19 |
| "port: inside a toolbar the popup asks the module to stop the composite keys, outside it does not" | L2 | POP-KEY-12 |
| "port: the popup's transitions handle asks for transition: none while starting" | L2 | POP-DOM-29 |
| "port: a press within the patient-click window after a hover open adds data-pressed" | L2 | POP-DOM-02 |
| "port: the trigger renders its ARIA attributes and the click-trigger marker" | L2 | POP-ARIA-01, POP-ARIA-02, POP-ARIA-03, POP-DOM-04, POP-DOM-05, POP-DOM-06 |
| "port: a disabled close button does not close" | L2 | POP-API-25 |
| "port: the popup is a dialog with the floating id, an unmanaged tabindex and no label without a title" | L2 | POP-ARIA-04, POP-DOM-23, POP-DOM-24, POP-DOM-25 |
| "port: a detached trigger rendered before its root joins the root's session once the root attaches the handle" | L2 | POP-API-13, POP-STR-12 |
| "port: the arrow is aria-hidden" | L2 | POP-ARIA-07 |
| "port: a trigger that becomes active again writes its payload again, after a move or a reopen" | L2 | POP-API-14, POP-STA-05 |
| "port: the trigger focus guards render without replacing the trigger element, and leave with the close" | L2 | POP-STR-14 |
| "port: keeps the opt-out when a controlled close is applied after the callback returns (restates "keeps the opt-out when a controlled close is applied in a transition")" | L2 | POP-ANI-06, POP-EVT-04 |
| "port: opens by trigger from a descendant's first render on initial mount (restates "opens by trigger from a descendant layout effect on initial mount")" | L2 | POP-API-09, POP-API-26, POP-EDGE-06 |
| "port: a C# re-render keeps [data-previous] before [data-current], and a dispose removes both" | L4 | POP-API-23, POP-DOM-37 |
| "port: with js/$module.js stalled the trigger attaches its other handles first, and opens the popover once when the module arrives ($module: button, interactions, popup)" | L4 | issue 133's attach-order rule; POP-PTR-01 |
| "port: with js/popup.js stalled the popover opens and closes, and its handles attach once the module arrives" | L4 | issue 133's attach-order rule; POP-STA-01 |
| "port: with js/interactions.js stalled a default-open popover positions at its trigger once the session joins (FLT-STA-08)" | L4 | FLT-STA-08; POP-POS-01 |
| "port: with js/focus-manager.js stalled a default-open popover attaches its other handles first, and focus moves in and returns once the module arrives" | L4 | issue 133's attach-order rule; POP-FOC-02, POP-FOC-05 |
| "port: with js/portal.js stalled the portaled handles wait for the move, and focus moves in once the popup is in place" | L4 | issue 133's attach-order rule; POP-STR-04, POP-FOC-02 |
| "port: a reopened popup carries transition: none with data-starting-style and drops both on the clearing frame" | L4 | POP-DOM-29 |
| "port: a modal popover cuts its trigger out of the internal backdrop" | L4 | POP-DOM-22 |
| "port: a trigger mouseleave before the hover delay ends cancels the open and requests no close" | L4 | POP-PTR-06; §16 Q12 |
| "port: an Escape close that allows propagation calls onOpenChange once" | L4 | POP-KEY-02; §16 Q7 |
| "port: a real press on a focusable outside element closes once, with focus-out" | L4 | POP-DIS-07, POP-EVT-01 (gap 14; open on Server to PR 137's re-merge) |
| "port: an open non-modal popover renders the outside guards and the aria-owns owner at the portal's position" | L4 | POP-DOM-11 |
| "port: every focus guard of an open popover is aria-hidden" | L4 | POP-ARIA-10 |
| "port: a right press neither toggles the popover nor counts as an outside press inside it" | L4 | POP-PTR-15 |
| "port: Escape closes one nesting level at a time" | L4 | POP-KEY-04 |
| "port: a kept-mounted positioner takes pointer-events none while closed, and drops it when opened" | L4 | POP-DOM-14 |
| "port: the popup carries the positioner's data-side and data-align" | L4 | POP-DOM-27 |
| "port: the positioner is marked anchor-hidden once its anchor scrolls out of view" | L4 | POP-DOM-17 |
| "port: a popover closed right after it opens raises no renderer error" | L4 | ADR 04 Amendments 2026-10-29 (gap 17) |

## 14. React-specific notes and porting considerations

Non-normative.

- **Store, not component state.** All Popover state lives in a plain observable store created once per Root (`useRefWithInit`) and read through selector subscriptions; parts re-render only when their selected slice changes. A port needs one mutable state object per Root with change notification; "post-commit, pre-paint" writes (trigger data forwarding, implicit active trigger, `openMethod` reset, title/description ids, `focusManagerModal`, `modal`) correspond to React layout effects and should run after the DOM for a state change is in place but before paint. The `mounted` seed (`useRefWithInit(() => store.set('mounted', mounted))`) exists so that subscribers created in the same render as the Root never see an initially-open popup as "not yet mounted". Port: see decisions/05-state-store-and-event-details.md §1 (store, selector subscriptions, prop-to-store sync in `OnParametersSet`); timing of the post-commit, pre-paint writes per decisions/01-hosting-model-and-timing.md §Decision (timing-term mapping).
- **Synchronous flushes.** Two places force a synchronous commit: hover-reason open changes (`ReactDOM.flushSync` in `PopoverStore.setOpen`) so that `getAnimations()` in the same task sees the new attributes, and the trigger focus guards' close (`flushSync` in `useTriggerFocusGuards`) so that the tab-order destination is computed against the closed DOM. A port that applies state synchronously by default needs no special handling; one that batches must flush at these two points. Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (EVD-EVT-18/PSH-ANI-03 hover open, PSH-KEY-07 focus-guard close).
- **Interaction prop bags.** `useDismiss`, `useClick`, the hover hook and the open-method hook each return handler objects that are merged onto the trigger/popup elements through `useRenderElement` (handler chaining order: consumer handlers first, then Popover's, then the button mechanism's). The Root publishes the dismiss bags into the store (`activeTriggerProps`/`inactiveTriggerProps`/`popupProps`) so detached triggers, which are not React children of the Root, can pick them up; the bags are reset to empty objects when the interactions unmount. In a port the equivalent is "the Root installs the Escape/press handlers on every registered trigger and the popup". Port: see decisions/04-js-interop-boundary.md §1 (`interactions` module owns press-to-toggle, dismissal and hover) and decisions/02-render-pipeline-and-attribute-merging.md §Decision 3 (handler chaining and veto).
- **Fragment keying.** The Trigger wraps its element in a keyed fragment so that adding/removing the sibling focus guards does not remount the trigger element (`React.Fragment key={thisTriggerId}`); a port must likewise insert/remove the guard elements around an unchanged trigger node (POP-STR-14).
- **`inert` across React versions.** `inertValue()` renders the `inert` attribute as a boolean on React ≥ 19 and as the string `"true"` on older versions; the observable DOM is the same (attribute present).
- **Function-as-child.** `Popover.Root` accepts `children` as a function of `{ payload }`; the port's equivalent is a templated content slot receiving the active trigger's payload. Port: see decisions/05-state-store-and-event-details.md §1 (`PopupHandle<TPayload>`).
- **`actionsRef`** is a React ref populated with `{ unmount, close }` through `useImperativeHandle`; a port exposes the same two methods on the Root instance.
- **Floating tree detection.** Whether a Root is nested is decided at creation from the presence of an ancestor tree node (`useFloatingParentNodeId`), and the outermost Popover Root creates the tree; Popover checks only for an ancestor *Popover* root context when deciding whether to create a tree, so a Popover inside a Menu creates its own tree but is still `nested` (its parent node id comes from the Menu's `FloatingNode`). Dismissal/focus relationships across different popup families therefore depend on the tree the Positioner's node is registered in. Port: see decisions/04-js-interop-boundary.md §1 (floating tree in the `interactions` module).
- **Strict Mode / double invocation.** The trigger registration ref callback is stable and idempotent (re-registering the same element under the same id in the same store is a no-op), and the detached-trigger tests run with `strict: false` only where StrictMode's ref replay would mask a bug; a port without double invocation needs no equivalent.
- **Timers and frames.** Stick-if-open uses a 500 ms timer owned by the store context and disposed on Root unmount; hover rest/close delays, the outside-press 1 s touch window and the inside-marker 0 ms clear are timers owned by the shared hooks; initial focus is a microtask followed by an animation frame (`enqueueFocus`); the focus-return check is a microtask; the trigger-switch restoration waits on the positioner's animations (next-frame inspection). Port: see decisions/01-hosting-model-and-timing.md §Decision (items 3–4: C# never emulates frames/microtasks; timing-term mapping).
- **jsdom skips.** Tests marked `skipIf(isJSDOM)` (real Tab navigation, transitions, scroll lock, pointer removal, transform origin) run only in a real browser; a port's test translation needs a real layout engine for them. Port: see decisions/06-testing-harness.md §1 (layer L4, Playwright per render mode).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| POP-STR-01 | MUST | Root renders no element, owns the store | 2 |
| POP-STR-02 | MUST | Outermost Root creates the floating tree; nested Roots join it | 2 |
| POP-STR-03 | MUST | Trigger renders button; store from handle or ancestor Root | 2 |
| POP-STR-04 | MUST | Portal renders portal node in container; exists while mounted/keepMounted | 2 |
| POP-STR-05 | MUST | Positioner renders div; requires Portal and Root | 2 |
| POP-STR-06 | MUST | Popup renders div; requires Positioner and Root | 2 |
| POP-STR-07 | MUST | Arrow renders div; requires Positioner | 2 |
| POP-STR-08 | MUST | Backdrop renders div; requires Root only | 2 |
| POP-STR-09 | MUST | Viewport renders div with data-current container; requires Positioner | 2 |
| POP-STR-10 | MUST | Title h2 / Description p; last mounted id wins | 2 |
| POP-STR-11 | MUST | Close renders button; works while closed; may be outside Popup | 2 |
| POP-STR-12 | MUST | Any number of triggers; one active | 2 |
| POP-STR-13 | MUST | Internal backdrop sibling iff modal true, mounted, not hover-opened | 2 |
| POP-STR-14 | MUST | Trigger focus guards iff opened by trigger and not focus-modal; element preserved | 2 |
| POP-STR-15 | MUST | Inside focus guards around Popup while focus manager enabled | 2 |
| POP-API-01 | MUST | `open` controlled wins | 3.1 |
| POP-API-02 | MUST | `defaultOpen` default false; initially open is mounted, no enter transition | 3.1 |
| POP-API-03 | MUST | `onOpenChange` optional | 3.1 |
| POP-API-04 | MUST | `onOpenChangeComplete` optional | 3.1 |
| POP-API-05 | MUST | `actionsRef` `{ unmount, close }` | 3.1 |
| POP-API-06 | MUST | `modal` default false; effects of true / 'trap-focus' | 3.1 |
| POP-API-07 | MUST | `triggerId` controlled active trigger | 3.1 |
| POP-API-08 | MUST | `defaultTriggerId` default null | 3.1 |
| POP-API-09 | MUST | `handle` attaches store; interactions kept mounted | 3.1 |
| POP-API-10 | MUST | `children` function receives `{ payload }` | 3.1 |
| POP-API-11 | MUST | Trigger `nativeButton` default true | 3.2 |
| POP-API-12 | MUST | Trigger `disabled` default false; blocks press and hover; forwarded | 3.2 |
| POP-API-13 | MUST | Trigger `handle` → detached trigger | 3.2 |
| POP-API-14 | MUST | Trigger `payload` written when active | 3.2 |
| POP-API-15 | MUST | Trigger `id` = rendered id = trigger id | 3.2 |
| POP-API-16 | MUST | `openOnHover` default false | 3.2 |
| POP-API-17 | MUST | `delay` default 300 = rest delay | 3.2 |
| POP-API-18 | MUST | `closeDelay` default 0 | 3.2 |
| POP-API-19 | MUST | Portal `keepMounted` default false | 3.3 |
| POP-API-20 | MUST | Portal `container` resolution | 3.3 |
| POP-API-21 | MUST | Popup `initialFocus` default: touch → popup, else first tabbable | 3.5 |
| POP-API-22 | MUST | Popup `finalFocus` default: trigger / previously focused | 3.5 |
| POP-API-23 | MUST | Viewport `children` inside data-current container | 3.8 |
| POP-API-24 | MUST | Close `nativeButton` default true; focusableWhenDisabled false | 3.11 |
| POP-API-25 | MUST | Close `disabled` default false | 3.11 |
| POP-API-26 | MUST | `createHandle()`: open(id), close(), isOpen | 3.12 |
| POP-STA-01 | MUST | Effective open = openProp ?? open | 4.1 |
| POP-STA-02 | MUST | Effective active trigger id = triggerIdProp ?? internal | 4.1 |
| POP-STA-03 | MUST | Close without trigger keeps trigger; open resets | 4.1 |
| POP-STA-04 | MUST | Lone-trigger claim; no close on trigger unmount | 4.1 |
| POP-STA-05 | MUST | Active trigger forwards payload/disabled/openOnHover/closeDelay | 4.2 |
| POP-STA-06 | MUST | `openChangeReason` set per applied change; null on unmount | 4.2 |
| POP-STA-07 | MUST | `instantType` derivation (click / dismiss / focus / trigger-change) | 4.2 |
| POP-STA-08 | MUST | `openMethod` recorded on activation; reset when closed | 4.2 |
| POP-STA-09 | MUST | Title/Description ids registered with cleanup | 4.2 |
| POP-STA-10 | MUST | `focusManagerModal` = modal !== false && Close inside Popup | 4.2 |
| POP-STA-11 | MUST | Stick-if-open flag: 500 ms after hover open; reset rules | 4.2 |
| POP-STA-12 | MUST | Selectors isPressOpenedByTrigger / isTouchPressOpen / hasTriggerFocusGuards | 4.2 |
| POP-STA-13 | MUST | Part state object shapes | 4.3 |
| POP-STA-14 | MUST | Trigger `open` state = open && this trigger active | 4.3 |
| POP-DOM-01 | MUST | Trigger `data-popup-open` | 5.1 |
| POP-DOM-02 | MUST | Trigger `data-pressed` iff press-opened | 5.1 |
| POP-DOM-03 | MUST | Trigger `data-disabled` | 5.1 |
| POP-DOM-04 | MUST | Trigger `id` | 5.1 |
| POP-DOM-05 | MUST | Trigger `data-base-ui-click-trigger` | 5.1 |
| POP-DOM-06 | MUST | Trigger type=button / role=button | 5.1 |
| POP-DOM-07 | MUST | Trigger disabled attribute / aria-disabled + tabindex -1 | 5.1 |
| POP-DOM-08 | MUST | Trigger focus guard spans | 5.1 |
| POP-DOM-09 | MUST | Portal node div with data-base-ui-portal | 5.2 |
| POP-DOM-10 | MUST | Portal renders nothing while unmounted without keepMounted | 5.2 |
| POP-DOM-11 | MUST | Outside guards + aria-owns span (non-modal, open) | 5.2 |
| POP-DOM-12 | MUST | Positioner role=presentation | 5.3 |
| POP-DOM-13 | MUST | Positioner `hidden` iff not mounted | 5.3 |
| POP-DOM-14 | MUST | Positioner pointer-events none iff closed | 5.3 |
| POP-DOM-15 | MUST | Positioner data-open/data-closed | 5.3 |
| POP-DOM-16 | MUST | Positioner data-side/data-align | 5.3 |
| POP-DOM-17 | MUST | Positioner data-anchor-hidden | 5.3 |
| POP-DOM-18 | MUST | Positioner data-instant | 5.3 |
| POP-DOM-19 | MUST | Positioner transition none while starting | 5.3 |
| POP-DOM-20 | MUST | Positioner CSS variables | 5.3 |
| POP-DOM-21 | MUST | Positioner positioning styles (FLT) | 5.3 |
| POP-DOM-22 | MUST | Internal backdrop element and styles | 5.3 |
| POP-DOM-23 | MUST | Popup id = floating id or consumer id | 5.4 |
| POP-DOM-24 | MUST | Popup tabindex -1 + data-base-ui-focusable; unmanaged | 5.4 |
| POP-DOM-25 | MUST | Popup data-open/data-closed | 5.4 |
| POP-DOM-26 | MUST | Popup data-starting-style/data-ending-style | 5.4 |
| POP-DOM-27 | MUST | Popup data-side/data-align | 5.4 |
| POP-DOM-28 | MUST | Popup data-instant values | 5.4 |
| POP-DOM-29 | MUST | Popup transition none while starting | 5.4 |
| POP-DOM-30 | MUST | Popup --popup-width/height and anchoring styles with Viewport | 5.4 |
| POP-DOM-31 | MUST | Inside focus guard spans | 5.4 |
| POP-DOM-32 | MUST | Arrow data attributes and inline position | 5.5 |
| POP-DOM-33 | MUST | Backdrop role=presentation | 5.6 |
| POP-DOM-34 | MUST | Backdrop `hidden` iff not mounted | 5.6 |
| POP-DOM-35 | MUST | Backdrop user-select none; pointer-events none iff hover-opened | 5.6 |
| POP-DOM-36 | MUST | Backdrop data-open/closed/starting/ending | 5.6 |
| POP-DOM-37 | MUST | Viewport data-current / data-previous containers | 5.7 |
| POP-DOM-38 | MUST | Viewport data-activation-direction | 5.7 |
| POP-DOM-39 | MUST | Viewport data-transitioning | 5.7 |
| POP-DOM-40 | MUST | Viewport data-instant | 5.7 |
| POP-DOM-41 | MUST | Previous container --popup-width/height | 5.7 |
| POP-DOM-42 | MUST | Title/Description ids | 5.8 |
| POP-DOM-43 | MUST | Close element per UBT; no data attributes | 5.8 |
| POP-ARIA-01 | MUST | Trigger aria-haspopup="dialog" | 5.9 |
| POP-ARIA-02 | MUST | Trigger aria-expanded | 5.9 |
| POP-ARIA-03 | MUST | Trigger aria-controls | 5.9 |
| POP-ARIA-04 | MUST | Popup role=dialog | 5.9 |
| POP-ARIA-05 | MUST | Popup aria-labelledby from Title | 5.9 |
| POP-ARIA-06 | MUST | Popup aria-describedby from Description | 5.9 |
| POP-ARIA-07 | MUST | Arrow aria-hidden | 5.9 |
| POP-ARIA-08 | MUST | Positioner/Backdrop/internal backdrop role=presentation | 5.9 |
| POP-ARIA-09 | MUST | Outside aria-hidden/inert in focus-modal mode only | 5.9 |
| POP-ARIA-10 | MUST | Focus guards aria-hidden (VoiceOver exception) | 5.9 |
| POP-KEY-01 | MUST | Enter/Space toggle; detail 0 → instant click | 6.1 |
| POP-KEY-02 | MUST | Escape closes; preventDefault; stopPropagation | 6.1 |
| POP-KEY-03 | MUST | Escape ignored during IME composition | 6.1 |
| POP-KEY-04 | MUST | Escape does not bubble to parent popover | 6.1 |
| POP-KEY-05 | MUST | Tab from trigger into popup | 6.1 |
| POP-KEY-06 | MUST | Shift+Tab from first popup element to trigger, stays open | 6.1 |
| POP-KEY-07 | MUST | Tab from last popup element closes, focus after trigger | 6.1 |
| POP-KEY-08 | MUST | Shift+Tab from trigger closes, focus before trigger | 6.1 |
| POP-KEY-09 | MUST | Modal trap cycling; Tab prevented with no tabbables | 6.1 |
| POP-KEY-10 | MUST | Tab during exit animation follows native order | 6.1 |
| POP-KEY-11 | MUST | No guard loop when trigger is the only tabbable | 6.1 |
| POP-KEY-12 | MUST | Composite keys stop propagation inside a Toolbar only | 6.1 |
| POP-PTR-01 | MUST | Click opens with trigger-press; openMethod recorded | 6.2 |
| POP-PTR-02 | MUST | Click on active trigger closes (toggle) | 6.2 |
| POP-PTR-03 | MUST | Click on inactive trigger switches | 6.2 |
| POP-PTR-04 | MUST | Patient click (500 ms stick-if-open) | 6.2 |
| POP-PTR-05 | MUST | Click before hover rest delay opens by press | 6.2 |
| POP-PTR-06 | MUST | Hover open config: mouse-only, rest delay | 6.2 |
| POP-PTR-07 | MUST | Hover close via safe polygon after closeDelay | 6.2 |
| POP-PTR-08 | MUST | Disabled trigger never hover-opens | 6.2 |
| POP-PTR-09 | MUST | Touch-press open disables sibling hover hand-off | 6.2 |
| POP-PTR-10 | MUST | Click-like open does not close on leave | 6.2 |
| POP-PTR-11 | MUST | Hover on inactive trigger hands over immediately | 6.2 |
| POP-PTR-12 | MUST | Popup mouseenter/mouseleave handling when hover-enabled | 6.2 |
| POP-PTR-13 | MUST | Prop/handle-opened popup does not close on leave | 6.2 |
| POP-PTR-14 | MUST | Touch press opens/closes; openMethod touch | 6.2 |
| POP-PTR-15 | MUST | Click interaction ignores mousedown / non-primary | 6.2 |
| POP-PTR-16 | MUST | Pointer type recording; keydown resets | 6.2 |
| POP-PTR-17 | MUST | Hover change applied synchronously | 6.2 |
| POP-PTR-18 | MUST | Modal backdrop blocks outside pointer except trigger cutout | 6.2 |
| POP-PTR-19 | MUST | Scroll lock rules (modal true; touch width check) | 6.2 |
| POP-PTR-20 | MUST | Re-enter during hover close reopens immediately | 6.2 |
| POP-PTR-21 | MUST | Safe polygon listener removed once click-sticky | 6.2 |
| POP-PTR-22 | MUST | Close click → close-press | 6.2 |
| POP-FOC-01 | MUST | Focus management active iff mounted and not hover-opened | 6.3 |
| POP-FOC-02 | MUST | Initial focus target and timing | 6.3 |
| POP-FOC-03 | MUST | initialFocus function called once with interaction type | 6.3 |
| POP-FOC-04 | MUST | Tabbability restored before reopening keepMounted popup | 6.3 |
| POP-FOC-05 | MUST | Focus return target and timing | 6.3 |
| POP-FOC-06 | MUST | Focus return suppression cases | 6.3 |
| POP-FOC-07 | MUST | restoreFocus 'popup' when focused child removed | 6.3 |
| POP-FOC-08 | MUST | Focus trap only with modal and Close inside Popup | 6.3 |
| POP-FOC-09 | MUST | Trigger guards follow the active trigger; removed on close | 6.3 |
| POP-FOC-10 | MUST | Before-trigger guard and trigger are inside elements | 6.3 |
| POP-FOC-11 | MUST | Nested popovers return focus in close order | 6.3 |
| POP-FOC-12 | MUST | Tab out of nested menu stays inside popover | 6.3 |
| POP-FOC-13 | MUST | Tab out with nested combobox closes it | 6.3 |
| POP-DIS-01 | MUST | Outside-press mode per pointer type and modal | 6.4 |
| POP-DIS-02 | MUST | Outside press → outside-press, trigger undefined; shadow roots | 6.4 |
| POP-DIS-03 | MUST | Press on a registered trigger is not outside | 6.4 |
| POP-DIS-04 | MUST | Inside/nested/drag-out/touch-scroll presses do not close | 6.4 |
| POP-DIS-05 | MUST | Click on parent popup closes child popover | 6.4 |
| POP-DIS-06 | MUST | Injected elements and scrollbars ignored; internal backdrop closes | 6.4 |
| POP-DIS-07 | MUST | Non-modal focus-out close | 6.4 |
| POP-DIS-08 | MUST | Other dismissal routes cross-referenced | 6.4 |
| POP-DIS-09 | MUST | No close when active trigger unmounts | 6.4 |
| POP-DIS-10 | MUST | Controlled open ignores dismissal | 6.4 |
| POP-EVT-01 | MUST | onOpenChange once, synchronously, before state change | 7 |
| POP-EVT-02 | MUST | Reason values and sources | 7 |
| POP-EVT-03 | MUST | cancel() stops the request | 7 |
| POP-EVT-04 | MUST | preventUnmountOnClose semantics | 7 |
| POP-EVT-05 | MUST | details.trigger per reason | 7 |
| POP-EVT-06 | MUST | close-press trigger back-fill | 7 |
| POP-EVT-07 | MUST | onOpenChangeComplete timing | 7 |
| POP-EVT-08 | MUST | No callback on mount or controlled prop change | 7 |
| POP-EVT-09 | MUST | Trigger switch calls onOpenChange(true) only | 7 |
| POP-EVT-10 | MUST | trigger-focus and none never emitted | 7 |
| POP-EVT-11 | MUST | Imperative actions: reason imperative-action | 7 |
| POP-EVT-12 | MUST | Hover change committed synchronously | 7 |
| POP-ANI-01 | MUST | mounted lifecycle; unmount cleanup order | 9 |
| POP-ANI-02 | MUST | Derived transition status | 9 |
| POP-ANI-03 | MUST | keepMounted retains and reuses the Popup element | 9 |
| POP-ANI-04 | MUST | Initially open: no starting phase | 9 |
| POP-ANI-05 | MUST | unmount() action semantics | 9 |
| POP-ANI-06 | MUST | preventUnmountOnClose suspends unmounting per cycle | 9 |
| POP-ANI-07 | MUST | Trigger-switch instant 'trigger-change' rules | 9 |
| POP-ANI-08 | MUST | onOpenChangeComplete timing (animations disabled → sync) | 9 |
| POP-ANI-09 | MUST | Reopen during exit keeps element, no false completion | 9 |
| POP-ANI-10 | MUST | Viewport content transitions and auto-resize | 9 |
| POP-ANI-11 | MUST | Interactions mounted while open/mounted/handle | 9 |
| POP-POS-01 | MUST | Positioning defaults | 10 |
| POP-POS-02 | MUST | collisionAvoidance default fallbackAxisSide end | 10 |
| POP-POS-03 | MUST | Anchor = active trigger; retained through exit | 10 |
| POP-POS-04 | MUST | Explicit anchor replaces trigger | 10 |
| POP-POS-05 | MUST | Anchor tracking on; disableAnchorTracking | 10 |
| POP-POS-06 | MUST | Offsets and collisionPadding resting | 10 |
| POP-POS-07 | MUST | --transform-origin values | 10 |
| POP-POS-08 | MUST | Arrow padding only with Arrow; uncentered | 10 |
| POP-POS-09 | MUST | Viewport switches to adaptive origin positioning | 10 |
| POP-POS-10 | MUST | Positioner registers a floating-tree node | 10 |
| POP-I18N-01 | MUST | RTL alignment and transform-origin | 10 |
| POP-EDGE-01 | MUST | Trigger without Root/handle throws | 12 |
| POP-EDGE-02 | MUST | Parts outside Root throw | 12 |
| POP-EDGE-03 | MUST | Positioner outside Portal throws | 12 |
| POP-EDGE-04 | MUST | Popup/Arrow/Viewport outside Positioner throw | 12 |
| POP-EDGE-05 | MUST | handle.open with unregistered id throws | 12 |
| POP-EDGE-06 | SHOULD | Dev warnings: no root attached; multiple roots | 12 |
| POP-EDGE-07 | MUST | Remount with same handle starts fresh | 12 |
| POP-EDGE-08 | MUST | Detached trigger survives reparenting/stale refs/handle recreation | 12 |
| POP-EDGE-09 | SHOULD | Inactive detached triggers do not re-render | 12 |
| POP-EDGE-10 | MUST | Positioner rejects keepMounted | 12 |
| POP-EDGE-11 | MUST | Nested Roots independent; nested flag | 12 |
| POP-EDGE-12 | MUST | Consumer role/id overrides keep behavior | 12 |
| POP-EDGE-13 | MUST | Server rendering uses the fallback store | 12 |
| POP-EDGE-14 | MUST | modal true on touch / hover opens | 12 |
| POP-EDGE-15 | MUST | Substitute callbacks must apply the handed `id`; Title/Description IDREFs dangle, `aria-controls` drops | 12 |

## 16. Open questions

1. **`trigger-focus` and `none` are declared but never emitted.** `Popover.Root.ChangeEventReason` (`popover/root/PopoverRoot.tsx:210,217`) includes both, but no `useFocus` interaction is wired on `Popover.Trigger` and `useImplicitActiveTrigger` is called without `closeOnActiveTriggerUnmount` (`PopoverRoot.tsx:58`); confirmed against the EVD reason matrix (event-details-and-reasons §10 Q3). Documented as POP-EVT-10; a port should keep the literals in the type for compatibility but needs no emitting path. Re-verified (QA 2026-10-14), answering EVD §10 Q3's request for a second reader: `REASONS.triggerFocus` is created only by `floating-ui-react/hooks/useFocus.ts:162,178,231`, whose call sites are Tooltip, Preview Card and Menu triggers; the Popover-reachable emitters are `useClick` (`trigger-press`), hover (`trigger-hover`), `useDismiss` (`outside-press`/`escape-key`), the focus manager and trigger focus guards (`focus-out`, `utils/popups/useTriggerFocusGuards.ts:46`), `Popover.Close` (`close-press`) and the Root actions / handle (`imperative-action`, `popover/root/PopoverRoot.tsx:78`, `utils/popups/popupHandle.ts:52,261,284`). No detached-trigger path produces `trigger-focus` or `none`. Resolved: decisions/05-state-store-and-event-details.md §3 ports reasons as one `BaseUIEventReason` enum carrying every EVD §3.5 member, so both values stay in the type with no Popover emitting path.
2. **`isDismissClose` includes `reason == null`** (`popover/store/PopoverStore.ts:118-119`). No interaction passes a nullish reason in v1.9.0, so the `'dismiss'` instant type is reachable only through `escape-key`; stated that way in POP-STA-07. If a future caller passes no reason, `data-instant="dismiss"` would appear on close.
3. **Popup `tabindex` is never managed.** Because the Popup always renders `tabindex="-1"` without `data-tabindex`, the focus manager's dialog `tabindex` logic (`FloatingFocusManager.tsx:114-120`) returns early; a popup with no tabbable content is therefore never placed in the sequential tab order (POP-DOM-24), unlike the generic FLT-DOM-17 behavior. Intentional per the test "does not loop focus between guards when tabbing out of the popup", but worth confirming.
4. **Outline omissions.** `components/page.mdx` does not list `data-instant` on the Positioner (rendered, POP-DOM-18) nor `data-disabled` on the Trigger as a prop-driven attribute (it is listed as a data attribute but the `disabled` prop itself is absent from the Trigger prop list). Documented from the source.
5. **`Popover.Close` `disabled` prop** is accepted (`PopoverClose.tsx:25`) but absent from the outline and untested (POP-API-25).
6. **`aria-haspopup` untested.** `popupConformanceTests` is invoked without `expectedAriaHasPopupValue`, so the `dialog` value (POP-ARIA-01) has no test; the Dialog suite does pass it.
   Resolved (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): the suite defaults the value to `expectedPopupRole`, so it is tested; see POP-ARIA-01's fidelity correction.
7. **Escape with `allowPropagation()` may double-dispatch.** When the consumer calls `allowPropagation()` for an Escape close that originated inside the Popup, the popup's own keydown handler does not stop propagation and the document-level listener registered by the same hook (`useDismiss.ts:721`) also fires with a stale `open` closure, calling `setOpen(false)` a second time and therefore `onOpenChange` twice. Not covered by Popover tests; flagged for the FLT author (FLT-KEY-01).
   Resolved (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27), as floating-and-positioning §10 item 11 records. Popover's proof (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): popover.spec.ts "port: an Escape close that allows propagation calls onOpenChange once", both modes.
8. **Historical test title.** `PopoverTrigger.test.tsx` "should not have the data-popup-open and data-pressed attributes when open by click when `openOnHover=true` and `delay=0`" asserts that `data-popup-open` *is* present after hover-then-click; the title predates the current stick-if-open behavior. Mapped to POP-DOM-01/POP-PTR-04 by its assertions.
9. **Hover close with `closeDelay` from the popup side resolves the delay against the recorded pointer type** (`getDelay(closeDelayProp, 'close', instance.pointerType)`), so a touch-recorded pointer yields a 0 ms close delay (FLT-API-03). Since hover opening is mouse-only (POP-PTR-06) this is reachable only when a mouse-opened popup later sees a touch `pointerdown` on the trigger; documented only via the FLT citation.
10. **Positioner `instant` is typed `string | undefined`** while Popup/Viewport use the literal union; the runtime values are identical (POP-STA-13). Cosmetic.
11. **Stale `instantType` after closes with unmapped reasons — mostly does not apply (shared-bug check with [tooltip](tooltip.md) §16 Q13 and [preview-card](preview-card.md) §16 Q4).** Popover's store does not use the shared `applyPopupOpenChange`; `PopoverStore.setOpen` writes `instantType` after every applied (non-canceled) change (`popover/store/PopoverStore.ts:173-182`), `undefined` for every reason other than keyboard click / Escape / nullish / `focus-out` (POP-STA-07). Outside-press, `imperative-action` (`actionsRef.close`, `handle.close`, both routed through `setOpen`) and `close-press` closes therefore clear it, unlike Tooltip and Preview Card. The only residue Popover shares with them is a close made by the consumer through a controlled `open={false}` with no `setOpen` call: the previous `'click'`, `'dismiss'` or `'focus'` stays, because the selector (`PopoverStore.ts:54-57`) masks only `'trigger-change'` once closed. No test asserts it; not a requirement.
12. **Spurious `onOpenChange(false)` on a hover-leave before any Positioner has rendered (shared with [tooltip](tooltip.md) §16 Q14 and [preview-card](preview-card.md) §16 Q5).** Applies only with `openOnHover`, because the hover interaction is disabled otherwise (`popover/trigger/PopoverTrigger.tsx:83`). Popover always configures `safePolygon()` (`:86`), so it occurs only while `dataRef.current.floatingContext` is still undefined, i.e. no Positioner has ever rendered (never opened, no `keepMounted`; set at `floating-ui-react/hooks/useFloating.ts:113`, never cleared). In that state a `mouseleave` of the closed trigger skips the safe-polygon branch (`floating-ui-react/hooks/useHoverReferenceInteraction.ts:315,321-359`) and calls `closeWithDelay` (`:194-206`). With the default `closeDelay` 0 (POP-API-18), `store.setOpen(false, trigger-hover)` runs at once; with a positive `closeDelay` it runs after that many ms. `PopoverStore.setOpen` has no equality guard (`PopoverStore.ts:110-140`), so `onOpenChange(false, { reason: 'trigger-hover' })` fires for a popover that never opened, and the 500 ms stick-if-open window is armed (no visible effect while closed). Cancelling the pending rest-delay open (FLT-PTR-14) is intended. The extra callback is not asserted by any test and is not made a requirement. A port should cancel the pending open without emitting a close. Popover differs from Preview Card in timing: it closes immediately by default, where Preview Card waits its 300 ms default close delay.
   Resolved (2026-10-27, M4 track 3, [issue 119](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/119); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27), as floating-and-positioning §10 item 12 records. Popover's proof (2026-10-29, M4 track 5, [issue 125](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/125)): popover.spec.ts "port: a trigger mouseleave before the hover delay ends cancels the open and requests no close", both modes.
13. **Root `disabled` + Trigger `disabled={false}` double `onOpenChange` ([tooltip](tooltip.md) §16 Q6) — does not apply.** `Popover.Root` has no `disabled` prop. `disabled` exists only on each Trigger (POP-API-12; the store's `disabled` is the active trigger's forwarded value, POP-STA-05). There is no Root-level revert effect that could follow a trigger-initiated open with a `disabled` close.
14. **Substitute callbacks and owner-computed IDREFs (rendering-and-composition §10 Q8).** Recorded as POP-EDGE-15. The same dangling `aria-labelledby`/`aria-describedby` also arises when `Popover.Title`/`Popover.Description` are given a substitute *element* carrying its own `id`. The element's `id` wins on the host (RND-DOM-32 rule 1, `mergeProps(props, render.props)` at `internals/useRenderElement.tsx:171`), but the part publishes `useBaseUiId(elementProps.id)` (`PopoverTitle.tsx:22-24`). Only `menu/popup/MenuPopup.tsx` uses `resolveRenderedId`. No test covers either case; confirm whether the parts should publish the resolved id. The portal's `aria-owns` owner follows FLT-DOM-12.
