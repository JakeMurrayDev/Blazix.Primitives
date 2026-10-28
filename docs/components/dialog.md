# Dialog

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/dialog/` |
| Tests | `packages/react/src/dialog/**/*.test.tsx` (10 files) + `root/DialogRoot.spec.tsx` (type-level only) + the shared suite `test/dialogRootSharedTests.tsx` (run by Dialog and AlertDialog) |
| Docs page | `docs/src/app/(docs)/react/components/dialog/page.mdx` |
| Prefix | `DLG` |
| Status | approved |
| Revised | 2026-10-29 |
| Builds on | none |
| Derived by | [alert-dialog](alert-dialog.md) (`useRenderDialogRoot('alert-dialog', …)`), [drawer](drawer.md) (`useRenderDialogRoot('drawer', …)`) |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [button-behavior](../architecture/button-behavior.md), [shared-utilities](../architecture/shared-utilities.md), [floating-and-positioning](../architecture/floating-and-positioning.md) (owns `useDismiss`, `useClick`, `FloatingFocusManager`, `FloatingPortal`) |

## 1. Overview

Dialog is a popup that opens on top of the whole page rather than next to an anchor. A **Trigger** button (contained in the **Root** or detached and linked through a handle) opens it; the content lives in a **Popup** rendered through a **Portal** into `<body>` (or a consumer container), optionally wrapped in a scrollable **Viewport** and accompanied by a **Backdrop**. **Title** and **Description** label the popup through `aria-labelledby`/`aria-describedby`; **Close** buttons close it.

The `modal` prop selects one of three regimes. With `true` (the default) focus is trapped inside the popup, an invisible internal backdrop swallows pointer interaction with the rest of the page, everything outside the popup is `aria-hidden`, and document scroll is locked. With `'trap-focus'` focus is trapped and the outside is `aria-hidden`, but the page stays scrollable and clickable. With `false` nothing is trapped or hidden, and moving focus out of the popup closes it. Dismissal is by <kbd>Escape</kbd>, by pressing outside (the exact event and target rules depend on the regime and on whether a backdrop exists), by a Close part, by a close-type trigger press, or imperatively through `actionsRef` or a handle. Dialogs nest: a Root rendered inside another dialog-family Root reports itself to its parent, which exposes `data-nested-dialog-open` and the `--nested-dialogs` count, stops handling <kbd>Escape</kbd>/outside presses, and keeps only the outermost Backdrop rendered.

Everything in §4 STA, §6.4, and §7 about the open state, trigger association, handles, payloads and the open-change sequence is the shared popup model of [popup-store-and-handles](../architecture/popup-store-and-handles.md) (PSH); this document states only what Dialog configures. **AlertDialog** and **Drawer** reuse the whole Root (`useRenderDialogRoot`), the store (`DialogStore`) and the interaction layer (`DialogInteractions`) with a different *mode*; where the mode changes a behavior it is noted inline ("alert mode", "drawer mode") so that those documents can cite the IDs here. Drawer additionally replaces Dialog's Popup, Backdrop and Viewport with its own implementations (initial focus default, nested-count attributes and variables, backdrop inline styles); those differences are not marked inline here — see [drawer](drawer.md) §3.17.

## 2. Anatomy

```
<Dialog.Root>                       required, 1            renders no element
  <Dialog.Trigger>                  optional, 0..n         default element: button   (may also be rendered outside Root with `handle`)
  <Dialog.Backdrop>                 optional, 0..n         default element: div      (anywhere under Root; usually inside Portal)
  <Dialog.Portal>                   required for Popup, 1  default element: div      (portal node appended to the container)
    [internal backdrop]             automatic              div, while mounted && modal === true
    <Dialog.Viewport>               optional, 0..1         default element: div
      [focus guard] <Dialog.Popup> [focus guard]   Popup required, 1   default element: div
        <Dialog.Title>              optional, 0..1         default element: h2
        <Dialog.Description>        optional, 0..1         default element: p
        <Dialog.Close>              optional, 0..n         default element: button
```

Title, Description, Close and Backdrop locate the Root through ancestor-provided shared state, so they may appear at any depth under Root (Backdrop is commonly placed inside Portal so it is portaled with the popup, and the tests also render it directly under Root). Viewport and Popup additionally require a Portal ancestor. A Trigger with a `handle` prop may be rendered anywhere in the application, before or after its Root (PSH-STR-05).

- **DLG-STR-01** — MUST. `Dialog.Root` renders no element of its own; it owns the popup store (PSH-STR-01) and exposes it to descendants as ancestor-provided shared state. Its `children` MAY be a function receiving `{ payload }` (DLG-API-09).
  Source: `dialog/root/DialogRoot.tsx:16-20`; `dialog/root/useRenderDialogRoot.tsx:94-106`. Test: [L2] `DialogRoot.test.tsx` popupConformanceTests.
- **DLG-STR-02** — MUST. `Dialog.Trigger` renders a `<button>` by default and MUST be either a descendant of a `Dialog.Root` or given a `handle`; otherwise it throws DLG-EDGE-01. A `handle` takes precedence over an ancestor Root (PSH-STR-02).
  Source: `dialog/trigger/DialogTrigger.tsx:38-45,83`. Test: [L2] `DialogTrigger.test.tsx` "throws a descriptive error without a root or handle"; describeConformance (`refInstanceof: HTMLButtonElement`).
- **DLG-STR-03** — MUST. `Dialog.Portal` renders a `<div>` *portal node* appended to the resolved container and renders the Popup subtree inside it; `Dialog.Viewport` and `Dialog.Popup` MUST be descendants of a `Dialog.Portal` or they throw DLG-EDGE-03.
  Source: `dialog/portal/DialogPortal.tsx:33-42`; `dialog/portal/DialogPortalContext.ts:6-12`; `dialog/viewport/DialogViewport.tsx:22`; `dialog/popup/DialogPopup.tsx:45`; `floating-ui-react/components/FloatingPortal.tsx:125-141,270`. Test: [L2] `DialogPortal.test.tsx` "throws a descriptive error when a portaled part is rendered without <Dialog.Portal>"; describeConformance (`refInstanceof: HTMLDivElement`) (L4 for the container-append clause).
- **DLG-STR-04** — MUST. The Portal's container resolves to: the `container` prop (element, shadow root, or DOM handle to one) when given; otherwise the portal node of the nearest enclosing Portal (so a nested dialog's portal node is appended *inside* its parent dialog's portal node by default); otherwise `<body>`. A `container` of `null` renders nothing until a non-null container is supplied.
  Source: `dialog/portal/DialogPortal.tsx:35,56-57`; `floating-ui-react/components/FloatingPortal.tsx:93-123`. Test: [L4] `DialogRoot.test.tsx` "keeps the parent open on close (outer: $outer, nested: $nested)" (`undefined` = parent portal node, else body); "closing via intentional outside press with user backdrop (modal=true): works when portaled into a shadow DOM". Docs: CHANGELOG.md v1.0.0-beta.2 "Support `ShadowRoot` containers".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design points 9 and 10, ADR 04 Amendments 2026-10-28 and 2026-10-29, PR 144): the shadow-root titles run in `dialog.spec.ts` on both modes, on a closed shadow root a page script holds, since Blazor renders components into the document only (the `dialog-shadow` fixture). The twelve "outside press event with backdrops (shadow DOM)" titles and the four "closed shadow root" cases whose nested popup is outside the shadow root pass, with "still dismisses on a press outside the popup"; the five nested AlertDialog cases are open to M4 track 9. The six whose nested popup sits in the shadow root are port titles ("port: keeps the parent open when the nested dialog inside the shadow root closes on Escape and on an outside press"), by the deviation of design point 10.
  Port: deviation 2026-10-29 (design point 10 of M4 track 8, settled by the orchestrator 2026-10-29; ADR 04 Amendments 2026-10-29; the platform followed in [issue 146](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/146)): inside a portal container in a shadow root, C# event handlers do not fire, the parts' own (a Close's click) and the consumer's alike, because Blazor dispatches them from the document's event target, which is the shadow host for an event raised inside a shadow root; the modules' paths work there (a trigger's press, Escape, the outside press, focus management). A consumer who portals a dialog into a shadow root closes it through those paths, not through an `@onclick` inside it.
- **DLG-STR-05** — MUST. `Dialog.Backdrop` renders a `<div>` by default. When its Root is nested (DLG-STA-03) and `forceRender` is `false` it renders nothing at all; otherwise it renders whenever its Root is mounted in the tree, regardless of open state (it is hidden while unmounted, DLG-DOM-08).
  Source: `dialog/backdrop/DialogBackdrop.tsx:19,33-49`. Test: [L2] `DialogBackdrop.test.tsx` "renders only the root backdrop by default", "always renders by default when not nested", "renders only the root backdrop with multiple nesting levels", "always renders when true". Docs: components/dialog/page.mdx §Nested dialogs.
- **DLG-STR-06** — MUST. `Dialog.Viewport` renders a `<div>` by default, inside the Portal, wrapping the Popup; it is a positioning/scroll container with no behavior beyond the attributes in §5.3.
  Source: `dialog/viewport/DialogViewport.tsx:16-61`. Test: [L2] `DialogViewport.test.tsx` describeConformance; "renders only when the dialog is mounted by default" (viewport contains popup).
- **DLG-STR-07** — MUST. `Dialog.Popup` renders a `<div>` by default and is the element that receives focus management, the `dialog` role and dismissal handlers. Exactly one Popup is expected per Root: the store keeps a single popup element and a single popup id.
  Source: `dialog/popup/DialogPopup.tsx:62,71-95`; `utils/popups/store.ts:70,152-155`. Test: [L2] `DialogPopup.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
- **DLG-STR-08** — MUST. `Dialog.Title` renders an `<h2>` and `Dialog.Description` a `<p>` by default; each registers its id with the Root (DLG-STA-09). They are structural: no handlers, no state attributes. When more than one of a kind is mounted the most recently mounted one's id wins.
  Source: `dialog/title/DialogTitle.tsx:22-29`; `dialog/description/DialogDescription.tsx:22-29`; `packages/utils/src/store/ReactStore.ts` (`useSyncedValueWithCleanup`, UTL-STA-20). Test: [L2] `DialogTitle.test.tsx` / `DialogDescription.test.tsx` describeConformance (`refInstanceof: HTMLHeadingElement` / `HTMLParagraphElement`).
- **DLG-STR-09** — MUST. `Dialog.Close` renders a `<button>` by default, anywhere under Root; any number may be rendered.
  Source: `dialog/close/DialogClose.tsx:45-49`. Test: [L2] `DialogClose.test.tsx` describeConformance (`refInstanceof: HTMLButtonElement`, `button: true`).
- **DLG-STR-10** — MUST. While the dialog is mounted and `modal === true`, the Portal renders the internal backdrop (PSH-STR-09, PSH-DOM-14) as the first child of the portal node, before the focus guards and the Popup; it is not rendered for `modal: false` or `modal: 'trap-focus'`.
  Source: `dialog/portal/DialogPortal.tsx:36-38`; `utils/InternalBackdrop.tsx:18-34`. Test: [L2] `DialogRoot.test.tsx` "should render an internal backdrop when `true`" (popup's previous sibling's previous sibling has `role="presentation"`), "should not render an internal backdrop when `false`", "makes other interactive elements on the page inert when a modal dialog is open".
- **DLG-STR-11** — MUST. While mounted, the Popup element is wrapped by two focus guards (PSH-STR-08, PSH-DOM-13) rendered as its immediate previous and next siblings (inside the Viewport when one is used); they exist in every `modal` regime because the Popup is always inside a portal.
  Source: `dialog/popup/DialogPopup.tsx:97-110`; `floating-ui-react/components/FloatingFocusManager.tsx:1003-1053`. Test: [L2] `DialogRoot.test.tsx` "should render an internal backdrop when `true`" (`popup.previousElementSibling` is the guard).
- **DLG-STR-12** — MUST. A Root rendered anywhere inside another Dialog, AlertDialog or Drawer Root (all three provide the same ancestor shared state) is *nested*; nesting is determined by the component tree, not by DOM containment or portal containers.
  Source: `dialog/root/useRenderDialogRoot.tsx:41-43`; `dialog/root/DialogRootContext.ts:5-19`; `alert-dialog/root/AlertDialogRoot.tsx:14-16`; `drawer/root/DrawerRoot.tsx:233`. Test: [L2] `DialogPopup.test.tsx` "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent dialog" / "…parent alert dialog".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render an AlertDialog are open to M4 track 9 ([issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)); the row does not hold `implemented` until they run.
- **DLG-STR-13** — MUST. The namespace exports `Root`, `Trigger`, `Portal`, `Backdrop`, `Viewport`, `Popup`, `Title`, `Description`, `Close`, `createHandle` and `Handle`; the attribute-name namespaces `DialogTriggerDataAttributes`, `DialogBackdropDataAttributes`, `DialogViewportDataAttributes`, `DialogPopupDataAttributes`, `DialogPopupCssVariables`, `DialogCloseDataAttributes` are exported alongside.
  Source: `dialog/index.parts.ts:1-10`; `dialog/index.ts:1-18`. Test: none (outline `components/page.mdx` §Dialog).

## 3. Public API

### 3.1 `Dialog.Root`

**Default element:** none. **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no (`Dialog.Root.State` is empty).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `open` | `boolean \| undefined` | `undefined` (uncontrolled) | Consumer-owned open state (PSH-STA-02). | DLG-API-01 |
| `defaultOpen` | `boolean` | `false` | Initial open state when `open` is not supplied. | DLG-API-02 |
| `modal` | `boolean \| 'trap-focus'` | `true` | Modality regime (focus trap, outside pointer blocking, `aria-hidden`, scroll lock). | DLG-API-03 |
| `onOpenChange` | `(open: boolean, eventDetails: Dialog.Root.ChangeEventDetails) => void` | `undefined` | Open-state change request callback (§7). | DLG-API-04 |
| `onOpenChangeComplete` | `(open: boolean) => void` | `undefined` | Called after the enter/exit animations finish (§9). | DLG-API-05 |
| `disablePointerDismissal` | `boolean` | `false` | Disables outside-press dismissal, focus-out dismissal (`modal: false`), and the focus manager's focus-out handling (popup focus recovery, backdrop-press stack push) for every `modal` value. | DLG-API-06 |
| `actionsRef` | `RefObject<{ unmount(): void; close(): void } \| null>` | `undefined` | Imperative actions. | DLG-API-07 |
| `handle` | `Dialog.Handle<Payload> \| undefined` | `undefined` | Binds this Root to a handle (PSH §3.1). | DLG-API-08 |
| `children` | `ReactNode \| ({ payload }: { payload: Payload \| undefined }) => ReactNode` | — | Content, or a function receiving the active payload. | DLG-API-09 |
| `triggerId` | `string \| null \| undefined` | `undefined` | Consumer-owned active trigger id (PSH-STA-03). | DLG-API-10 |
| `defaultTriggerId` | `string \| null \| undefined` | `null` | Initial active trigger id (with `defaultOpen`). | DLG-API-11 |

No other props are accepted; nothing is forwarded to an element.

- **DLG-API-01** — MUST. `open`, when not `undefined`, is the consumer-owned open state and wins over internal state (PSH-STA-02, EVD-STA-01/04/06); the component then only requests changes through `onOpenChange`.
  Source: `dialog/root/useRenderDialogRoot.tsx:23,54,65`; `utils/popups/store.ts:150`. Test: [L2] `DialogRoot.test.tsx` popupConformanceTests "opens the popup with the `open` prop", "is ignored when `open={false}` is passed". Docs: components/dialog/page.mdx §State.
- **DLG-API-02** — MUST. `defaultOpen` defaults to `false` and seeds the internal open state once (PSH-STA-01); a Root mounted with `defaultOpen` is mounted immediately and plays no enter transition (PSH-ANI-02).
  Source: `dialog/root/useRenderDialogRoot.tsx:24,53`. Test: [L2] popupConformanceTests "opens initially and remains uncontrolled"; `dialogRootSharedTests.tsx` "closes the popup when the `close` method is called".
- **DLG-API-03** — MUST. `modal` defaults to `true` and accepts `true`, `false` and `'trap-focus'`. The regimes differ exactly as follows: focus trap (DLG-KEY-06) for `true` and `'trap-focus'`; `aria-hidden` on the outside (DLG-ARIA-11) for `true` and `'trap-focus'`; internal backdrop (DLG-STR-10) and document scroll lock (DLG-DOM-26) for `true` only; focus-out dismissal (DLG-FOC-14) for `false` only; outside-press event mode per DLG-PTR-05. In alert mode `modal` is forced to `true`.
  Source: `dialog/root/useRenderDialogRoot.tsx:28,36-37`; `dialog/root/DialogRoot.tsx:37-46`; `dialog/popup/DialogPopup.tsx:105`; `dialog/root/useDialogRoot.ts:38,71,87`. Test: [L4] `DialogRoot.test.tsx` "prop: modal" (4 tests); `DialogPopup.test.tsx` "keeps trap-focus tab cycling inside the popup…". Docs: CHANGELOG.md v1.0.0-alpha.8 "Add new `trap-focus` value to `modal` prop".
- **DLG-API-04** — MUST. `onOpenChange` has the signature and cancelation/ordering semantics of EVD-API-13 with the reasons of DLG-EVT-02 and the `preventUnmountOnClose()` extension (EVD-API-09).
  Source: `dialog/root/DialogRoot.tsx:50,98-111`; `dialog/store/DialogStore.ts:72-95`. Test: [L2] `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button".
- **DLG-API-05** — MUST. `onOpenChangeComplete` is called with `true` once the Popup's enter animations finish and with `false` when the popup subtree unmounts after the exit animations (DLG-EVT-08). Server: relaxed, see §9.
  Source: `dialog/root/useRenderDialogRoot.tsx:26,70`; `dialog/popup/DialogPopup.tsx:47-55`; `utils/popups/popupStoreUtils.ts:586-595`. Test: [L4] popupConformanceTests "prop: onOpenChangeComplete" (5 tests).
- **DLG-API-06** — MUST. `disablePointerDismissal` defaults to `false`. When `true`: outside presses never request a close (DLG-DIS-01); with `modal: false`, focus leaving the popup never requests a close (DLG-FOC-14); and, for every `modal` value, because it is passed to the focus manager as `closeOnFocusOut: false`, the `restoreFocus: 'popup'` recovery (DLG-FOC-15) and the backdrop-press push onto the previously-focused stack (DLG-FOC-16, second clause) do not happen. <kbd>Escape</kbd>, Close parts, trigger toggling and imperative closes are unaffected. In alert mode it is forced to `true`.
  Source: `dialog/root/useRenderDialogRoot.tsx:27,38`; `dialog/root/useDialogRoot.ts:67`; `dialog/popup/DialogPopup.tsx:102`; `floating-ui-react/components/FloatingFocusManager.tsx:424-427`. Test: [L4] `DialogRoot.test.tsx` "$outcome the dialog when clicking outside if disablePointerDismissal=$disablePointerDismissal" (3 rows). Docs: CHANGELOG.md v1.0.0-beta.5 "Replace `dismissible` with `disablePointerDismissal`".
- **DLG-API-07** — MUST. `actionsRef.current` is `{ unmount, close }`: `close()` requests a close with reason `imperative-action` and no trigger; `unmount()` ends the closing phase immediately (PSH-API-09, TRN-EVT-08).
  Source: `dialog/root/useRenderDialogRoot.tsx:80-87`; `dialog/root/DialogRoot.tsx:93-96`. Test: [L2] `dialogRootSharedTests.tsx` "unmounts the popup when the `unmount` method is called", "clears manual unmount state after the `unmount` method is called", "closes the popup when the `close` method is called".
- **DLG-API-08** — MUST. `handle` binds the Root's store to a `Dialog.Handle` for the Root's mounted lifetime (PSH-STR-03/04); a Root with a `handle` keeps its dismissal interactions mounted even while closed (PSH-STA-43).
  Source: `dialog/root/useRenderDialogRoot.tsx:30,92,96`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` detachedTriggersConformanceTests; "attaches and detaches a handle when the prop toggles on a live root".
- **DLG-API-09** — MUST. When `children` is a function it is called with `{ payload }`, where `payload` is the store payload (the active trigger's `payload`, the value given to `openWithPayload`, or `undefined`) (PSH-API-07, PSH-STA-32).
  Source: `dialog/root/useRenderDialogRoot.tsx:74,104`; `dialog/root/DialogRoot.tsx:79`. Test: [L2] `dialogRootSharedTests.tsx` "opens with a payload set programmatically"; `DialogRoot.detached-triggers.test.tsx` "keeps the payload reactive" (×2). Docs: components/dialog/page.mdx §Multiple triggers.
- **DLG-API-10** — MUST. `triggerId`, when not `undefined`, is the consumer-owned active trigger id (a controlled `null` means no active trigger) and is meant to be used with `open` (PSH-STA-03).
  Source: `dialog/root/useRenderDialogRoot.tsx:31,56,66`; `dialog/root/DialogRoot.tsx:80-85`. Test: [L2] detachedTriggersConformanceTests "marks the trigger matching a controlled `triggerId` as active", "allows controlling the open state and active trigger programmatically". Docs: components/dialog/page.mdx §Controlled mode with multiple triggers.
- **DLG-API-11** — MUST. `defaultTriggerId` defaults to `null` and seeds the internal active trigger id once (PSH-API-06, PSH-STA-01).
  Source: `dialog/root/useRenderDialogRoot.tsx:32,55`. Test: [L2] detachedTriggersConformanceTests "opens initially with the payload of a non-first `defaultTriggerId`"; `DialogRoot.test.tsx` "reports no trigger when closing with an initial trigger id that is not mounted".

#### State object (`Dialog.Root.State`)

Empty object; the Root renders no element.

#### Data attributes / CSS variables

None.

### 3.2 `Dialog.Trigger`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Dialog.Trigger.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `handle` | `Dialog.Handle<Payload> \| undefined` | `undefined` | Makes this a detached trigger (PSH §3.3). | DLG-API-12 |
| `payload` | `Payload \| undefined` | `undefined` | Value forwarded to the store when this trigger is active. | DLG-API-13 |
| `id` | `string \| undefined` | generated | Rendered `id` and trigger id (PSH-API-08). | DLG-API-14 |
| `nativeButton` | `boolean` | `true` | Whether the rendered element is a native `<button>` (UBT). | DLG-API-15 |
| `disabled` | `boolean` | `false` | Disables the trigger (UBT). Not listed in the API outline (§16 Q8). | DLG-API-16 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |
| any other `<button>` attribute / handler | | — | Forwarded; handlers chained per RND-EVT-02. | — |

**Consumed (not forwarded):** `handle`, `payload`, `nativeButton`, `disabled` (re-emitted by the button mechanism), `id` (re-emitted as the resolved id).

- **DLG-API-12** — MUST. `handle` defaults to `undefined`; when given, the Trigger reads the store the handle currently exposes (PSH-STR-02, PSH-STA-34/36) and registers itself in that store's trigger registry (PSH-STA-26).
  Source: `dialog/trigger/DialogTrigger.tsx:34,38-40,54-61`. Test: [L2] detachedTriggersConformanceTests (`detached` mode); `DialogRoot.detached-triggers.test.tsx` "keeps ARIA controls in sync when a detached handle is recreated while open".
  Port note 2026-10-28 (conventions §3.5): §13 maps detachedTriggersConformanceTests "registers a detached trigger declared after the root", "resolves a trigger still registered to the previous root during a transient overlap" and "keeps a detached trigger working $name" (×4) to this row, whose `Test:` line did not cite them; they run at [L4] through the suite on the `dialog-triggers` fixture.
- **DLG-API-13** — MUST. `payload` defaults to `undefined` and is the only trigger-owned value Dialog forwards to the store (PSH-STA-28/30); it is written when the trigger becomes active and whenever it changes while the trigger owns the mounted popup.
  Source: `dialog/trigger/DialogTrigger.tsx:33,54-61`; `utils/popups/popupStoreUtils.ts:382-391`. Test: [L2] detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it"; `DialogRoot.detached-triggers.test.tsx` "keeps the payload reactive" (×2).
- **DLG-API-14** — MUST. `id`, when given, is rendered verbatim and used as the trigger id; otherwise a generated id (RND-DOM-27) serves both roles.
  Source: `dialog/trigger/DialogTrigger.tsx:32,47,92`. Test: [L2] `DialogRoot.test.tsx` "keeps trigger ownership when another trigger mounts while open" (`id="trigger-1"`).
- **DLG-API-15** — MUST. `nativeButton` defaults to `true` and is passed as the `native` option of the button mechanism (UBT-API-02/03): `true` renders `type="button"`, `false` renders `role="button"` with keyboard activation emulation.
  Source: `dialog/trigger/DialogTrigger.tsx:31,63-66`. Test: [L2] `DialogTrigger.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`, RND-DOM-49); "custom element".
- **DLG-API-16** — MUST. `disabled` defaults to `false`; when `true` the button mechanism blocks activation (UBT-PTR-01, UBT-KEY-19) so the dialog does not open, the element is removed from the tab order (UBT-FOC-01/02) and `disabled: true` appears in the state (DLG-DOM-04).
  Source: `dialog/trigger/DialogTrigger.tsx:30,63-66,77`. Test: [L2] `DialogTrigger.test.tsx` "disables the dialog", "custom element".

#### State object (`Dialog.Trigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | The `disabled` prop. |
| `open` | `boolean` | `true` iff the dialog is open **and** this trigger is the active trigger (PSH-STA-08). |

Source: `dialog/trigger/DialogTrigger.tsx:76-79,128-137`.

- **DLG-STA-12** — MUST. `Trigger.State.open` is `isOpenedByTrigger(ownId)` (PSH-STA-08), not the dialog's open state: while the dialog is open through another trigger or without a trigger, every other trigger reports `open: false`.
  Source: `dialog/trigger/DialogTrigger.tsx:49,78`. Test: [L2] `DialogRoot.test.tsx` "keeps trigger ownership when another trigger mounts while open"; `DialogRoot.detached-triggers.test.tsx` "does not associate the only rendered trigger when opened with a payload".

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open` | `open === true`; empty value (PSH-DOM-01) | DLG-DOM-03 |
| `data-disabled` | `disabled === true`; empty value | DLG-DOM-04 |
| `data-base-ui-click-trigger` | always; empty value (internal marker, PSH-DOM-16; not in the outline) | DLG-DOM-02 |

#### CSS variables

None.

### 3.3 `Dialog.Portal`

**Default element:** `<div>` (the portal node). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Dialog.Portal.State`, empty).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the portal subtree in the DOM while closed. | DLG-API-17 |
| `container` | `HTMLElement \| ShadowRoot \| RefObject<HTMLElement \| ShadowRoot \| null> \| null \| undefined` | `undefined` | Element the portal node is appended to (DLG-STR-04). | DLG-API-18 |
| `className`, `style`, `render` | per RND | — | Applied to the portal node. | — |
| any other `<div>` attribute | | — | Forwarded to the portal node. | — |

**Consumed:** `keepMounted`, `container`.

- **DLG-API-17** — MUST. `keepMounted` defaults to `false`. With `false` the Portal renders nothing while the dialog is not mounted (closed and not in an exit transition); with `true` the portal node, Viewport and Popup stay in the DOM while closed, hidden and inaccessible (TRN-ANI-01, PSH-ANI-14).
  Source: `dialog/portal/DialogPortal.tsx:21,28-31,34`; `dialog/viewport/DialogViewport.tsx:42`. Test: [L2] `DialogPopup.test.tsx` "should keep the dialog mounted when keepMounted=true", "should not keep the dialog mounted when keepMounted=$name"; `DialogViewport.test.tsx` "stays mounted when used within a keepMounted portal". Docs: CHANGELOG.md v1.0.0-alpha.5 "Require `Portal` part" (`keepMounted` lives on the Portal only).
- **DLG-API-18** — MUST. `container` defaults to `undefined` and resolves per DLG-STR-04; shadow roots are accepted and dismissal keeps working inside and across them (DLG-DIS-06).
  Source: `dialog/portal/DialogPortal.tsx:56-57`; `floating-ui-react/components/FloatingPortal.tsx:104-107`. Test: [L4] `DialogRoot.test.tsx` "closing via outside press: works when clicking another element inside the same shadow root" and the three sibling shadow-root tests.
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design points 9 and 10, ADR 04 Amendments 2026-10-28 and 2026-10-29, PR 144): the shadow-root titles run in `dialog.spec.ts` on both modes, on a closed shadow root a page script holds, since Blazor renders components into the document only (the `dialog-shadow` fixture). The twelve "outside press event with backdrops (shadow DOM)" titles and the four "closed shadow root" cases whose nested popup is outside the shadow root pass, with "still dismisses on a press outside the popup"; the five nested AlertDialog cases are open to M4 track 9. The six whose nested popup sits in the shadow root are port titles ("port: keeps the parent open when the nested dialog inside the shadow root closes on Escape and on an outside press"), by the deviation of design point 10.
  Port: deviation 2026-10-29 (design point 10 of M4 track 8, settled by the orchestrator 2026-10-29; ADR 04 Amendments 2026-10-29; the platform followed in [issue 146](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/146)): inside a portal container in a shadow root, C# event handlers do not fire, the parts' own (a Close's click) and the consumer's alike, because Blazor dispatches them from the document's event target, which is the shadow host for an event raised inside a shadow root; the modules' paths work there (a trigger's press, Escape, the outside press, focus management). A consumer who portals a dialog into a shadow root closes it through those paths, not through an `@onclick` inside it.

#### State object (`Dialog.Portal.State`)

Empty object.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-base-ui-portal` | always on the portal node; empty value (internal marker, not in the outline) | DLG-DOM-05 |

#### CSS variables

None.

### 3.4 `Dialog.Backdrop`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Dialog.Backdrop.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `forceRender` | `boolean` | `false` | Render the backdrop even when the Root is nested. | DLG-API-19 |
| `className`, `style`, `render` | per RND | — | `style` is merged after the component's `user-select: none` (DLG-DOM-07). | — |
| any other `<div>` attribute | | — | Forwarded; a consumer `hidden`/`role` overrides the component's (RND-DOM-19). | — |

**Consumed:** `forceRender`.

- **DLG-API-19** — MUST. `forceRender` defaults to `false`; `true` makes a nested Root's Backdrop render like a top-level one (DLG-STR-05).
  Source: `dialog/backdrop/DialogBackdrop.tsx:19,48,52-58`. Test: [L2] `DialogBackdrop.test.tsx` "always renders when true". Docs: CHANGELOG.md v1.0.0-beta.2 "Add `forceRender` prop to `Backdrop` part".

#### State object (`Dialog.Backdrop.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | Root transition status (PSH-STA-05). |

Source: `dialog/backdrop/DialogBackdrop.tsx:28-31,60-69`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` | `open === true` | DLG-DOM-09 |
| `data-closed` | `open === false` | DLG-DOM-09 |
| `data-starting-style` | `transitionStatus === 'starting'` | DLG-DOM-10 |
| `data-ending-style` | `transitionStatus === 'ending'` | DLG-DOM-10 |

#### CSS variables

None.

### 3.5 `Dialog.Viewport`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Dialog.Viewport.State`). It has no props of its own; `children` are rendered inside.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | per RND | — | `style` is merged after the component's `pointer-events` (DLG-DOM-13). | — |
| any other `<div>` attribute | | — | Forwarded. | — |

#### State object (`Dialog.Viewport.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `transitionStatus` | `TransitionStatus` | Root transition status. |
| `nested` | `boolean` | The Root is nested (DLG-STA-03). |
| `nestedDialogOpen` | `boolean` | `nestedOpenDialogCount > 0` (DLG-STA-13). |

Source: `dialog/viewport/DialogViewport.tsx:35-40,63-80`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | `open` true / false | DLG-DOM-14 |
| `data-starting-style` / `data-ending-style` | transition status | DLG-DOM-14 |
| `data-nested` | `nested === true` | DLG-DOM-15 |
| `data-nested-dialog-open` | `nestedDialogOpen === true` | DLG-DOM-15 |

#### CSS variables

None.

### 3.6 `Dialog.Popup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Dialog.Popup.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `initialFocus` | `boolean \| RefObject<HTMLElement \| null> \| ((openType: InteractionType) => boolean \| HTMLElement \| null \| void)` | `undefined` (→ default resolver) | Element to focus when the dialog opens. | DLG-API-20 |
| `finalFocus` | same shape, `(closeType) => …` | `undefined` (→ `true`) | Element to focus when the dialog closes. | DLG-API-21 |
| `id` | `string` | generated popup id | Consumer id; also becomes the value of every trigger's `aria-controls` (PSH-ARIA-03). | DLG-DOM-16 |
| `className`, `style`, `render` | per RND | — | `style` is merged after the inline `--nested-dialogs` declaration (DLG-DOM-19). | — |
| any other `<div>` attribute / handler | | — | Forwarded; `onKeyDown` chains with the component's (DLG-KEY-09). | — |

**Consumed:** `initialFocus`, `finalFocus`.

`InteractionType` is `'mouse' | 'touch' | 'pen' | 'keyboard' | ''` (`packages/utils/src/useEnhancedClickHandler.ts:4`).

- **DLG-API-20** — MUST. `initialFocus` defaults to the shared default resolver (PSH-FOC-01: the popup element itself when the open interaction type is `touch`, otherwise the default target of PSH-FOC-02) and otherwise follows the value contract PSH-FOC-02/03 (function called once per open with the open interaction type; `false`/`undefined` result → no focus move; `true`/`null` → default; element or DOM handle → that element, falling back to the default when the handle is empty).
  Source: `dialog/popup/DialogPopup.tsx:26,57-58,103,125-129`; `utils/popups/popupStoreUtils.ts:37-40`; `floating-ui-react/components/FloatingFocusManager.tsx:692-725`. Test: [L4] `DialogPopup.test.tsx` popupFocusPropsTests (modal and non-modal) "prop: initialFocus" (6 tests); "focuses the popup itself rather than inner content when opened by touch". Docs: components/dialog/page.mdx §Custom focus management; CHANGELOG.md v1.0.0-beta.3 "Support `initialFocus` and `finalFocus` functions".
- **DLG-API-21** — MUST. `finalFocus` defaults to `undefined`, which behaves as `true`, and follows the value contract PSH-FOC-04/05 (function called once per close with the close interaction type; `false`/`undefined` result → no focus move; `true`/`null` → the default return target of DLG-FOC-06; element or DOM handle → that element, falling back to the default when empty).
  Source: `dialog/popup/DialogPopup.tsx:26,104,139-143`; `floating-ui-react/components/FloatingFocusManager.tsx:847-882`. Test: [L4] popupFocusPropsTests "prop: finalFocus" (5 tests); `DialogPopup.test.tsx` "respects finalFocus when initialFocus points outside the popup", "moves final focus to trigger if initialFocus points outside the popup and finalFocus is not specified".

#### State object (`Dialog.Popup.State`)

Same four fields as `Dialog.Viewport.State` (`open`, `transitionStatus`, `nested`, `nestedDialogOpen`).
Source: `dialog/popup/DialogPopup.tsx:64-69,146-163`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | `open` true / false | DLG-DOM-20 |
| `data-starting-style` / `data-ending-style` | transition status | DLG-DOM-20 |
| `data-nested` | `nested === true` | DLG-DOM-21 |
| `data-nested-dialog-open` | `nestedDialogOpen === true` | DLG-DOM-22 |
| `data-base-ui-focusable` | always; empty value (internal marker, PSH-DOM-15; not in the outline) | DLG-DOM-17 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--nested-dialogs` | Popup inline style | `nestedOpenDialogCount` as a number (`0` when none) | DLG-DOM-19 |

### 3.7 `Dialog.Title`

**Default element:** `<h2>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state empty). Props: `id` (consumer id used verbatim, otherwise generated; DLG-DOM-23), `className`, `style`, `render`, any `<h2>` attribute. No data attributes, no CSS variables.

### 3.8 `Dialog.Description`

**Default element:** `<p>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state empty). Props: `id` (DLG-DOM-23), `className`, `style`, `render`, any `<p>` attribute. No data attributes, no CSS variables.

### 3.9 `Dialog.Close`

**Default element:** `<button>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `Dialog.Close.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `nativeButton` | `boolean` | `true` | Native `<button>` vs. emulated button (UBT). | DLG-API-22 |
| `disabled` | `boolean` | `false` | Disables the button. Not listed in the API outline (§16 Q8). | DLG-API-23 |
| `className`, `style`, `render` | per RND | — | | — |
| any other `<button>` attribute / handler | | — | Forwarded; a consumer `onClick` runs before the component's and may veto it (RND-EVT-02/03). | — |

**Consumed:** `nativeButton`, `disabled` (re-emitted by the button mechanism).

- **DLG-API-22** — MUST. `nativeButton` defaults to `true` and is passed as the button mechanism's `native` option (UBT-API-02).
  Source: `dialog/close/DialogClose.tsx:25,32-35`. Test: [L2] `DialogClose.test.tsx` describeConformance (`button: true`, `testRenderPropWith: 'button'`); "custom element".
- **DLG-API-23** — MUST. `disabled` defaults to `false`; when `true` the button mechanism blocks the click (UBT-PTR-01) so no close is requested, and `disabled: true` is exposed in the state (DLG-DOM-24).
  Source: `dialog/close/DialogClose.tsx:24,32-37`. Test: [L2] `DialogClose.test.tsx` "disables the button", "custom element".

#### State object (`Dialog.Close.State`)

| Field | Type | Meaning |
|---|---|---|
| `disabled` | `boolean` | The `disabled` prop. |

Source: `dialog/close/DialogClose.tsx:37,55-60`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-disabled` | `disabled === true`; empty value | DLG-DOM-24 |

#### CSS variables

None.

### 3.10 `Dialog.createHandle()` / `Dialog.Handle`

`Dialog.createHandle<Payload>()` returns a new `Dialog.Handle<Payload>` (PSH-API-01). The handle is the shared popup handle of PSH §3.1 with the Dialog-family extensions:

| Member | Signature | Req |
|---|---|---|
| `open(triggerId)` | `(triggerId: string \| null) => void` | DLG-API-24 |
| `openWithPayload(payload)` | `(payload: Payload) => void` | DLG-API-25 |
| `close()` | `() => void` | DLG-API-26 |
| `isOpen` | `readonly boolean` | DLG-API-27 |

- **DLG-API-24** — MUST. `handle.open(id)` requests an open with reason `imperative-action` and the registered trigger element as `trigger` (PSH-EVT-06, PSH-STA-38); `open(null)` (or `''`) opens without a trigger (PSH-API-02, PSH-EDGE-05); an unmatched non-empty id opens *unassociated* with a dev warning instead of throwing (PSH-EDGE-04, DLG-EDGE-06). While no Root is attached the call is ignored with a dev warning (PSH-EDGE-02, DLG-EDGE-05).
  Source: `dialog/store/DialogHandle.ts:19-21,31-33`; `utils/popups/popupHandle.ts:213-263`. Test: [L2] detachedTriggersConformanceTests "opens by trigger id with its payload and closes", "opens unassociated with a warning when opened with an unregistered trigger id", "ignores imperative calls while no root is attached and follows the root across remounts"; `DialogRoot.detached-triggers.test.tsx` "associates the requested trigger when opened by id in the same commit a root attaches", "stops resolving a handle-A trigger after the root switches to handle B".
- **DLG-API-25** — MUST. `handle.openWithPayload(p)` sets the store payload to `p` and then requests a trigger-less open with reason `imperative-action` (PSH-STA-39), so `openedWithoutTrigger` is set and no registered trigger is associated (PSH-STA-19); while no Root is attached it is ignored with the dev warning DLG-EDGE-04.
  Source: `dialog/store/DialogHandle.ts:42-57`. Test: [L2] `dialogRootSharedTests.tsx` "opens with a payload set programmatically"; `DialogRoot.detached-triggers.test.tsx` "does not associate the only rendered trigger when opened with a payload", "does not let a trigger that mounts while open replace the programmatic payload", "ignores openWithPayload() after the root unmounts and keeps the remounted root without payload", "opens with a payload from a descendant layout effect on initial mount".
  Port note 2026-10-28 (design point 7 (a), settled by the orchestrator 2026-10-28): the descendant layout-effect titles run over a descendant's first `OnAfterRender` (DLG-EDGE-05's note).
- **DLG-API-26** — MUST. `handle.close()` requests a close with reason `imperative-action` and no trigger (PSH-API-04, PSH-DIS-03); ignored with a dev warning while no Root is attached.
  Source: `dialog/store/DialogHandle.ts:64-66`; `utils/popups/popupHandle.ts:271-285`. Test: [L2] detachedTriggersConformanceTests "opens by trigger id with its payload and closes"; `DialogRoot.detached-triggers.test.tsx` "closes from a descendant layout effect on an initially open root".
  Port note 2026-10-28 (design point 7 (a), settled by the orchestrator 2026-10-28): the descendant layout-effect titles run over a descendant's first `OnAfterRender` (DLG-EDGE-05's note).
- **DLG-API-27** — MUST. `handle.isOpen` is the effective open state of the attached Root's store, `false` while none is attached (PSH-API-05).
  Source: `dialog/store/DialogHandle.ts:71-73`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "keeps the server snapshot stable when the root closes before a delayed trigger hydrates", "keeps a handle-backed dialog open and controllable across handle swaps on a live root".

### 3.11 Shared types

- `Dialog.Root.ChangeEventReason` = `'trigger-press' | 'outside-press' | 'escape-key' | 'close-press' | 'focus-out' | 'imperative-action' | 'none'`. Source: `dialog/root/DialogRoot.tsx:98-105`; `internals/reason-parts.ts:1,3,7,9,21,22,39`. `'none'` is declared but never emitted by Dialog (§16 Q3).
- `Dialog.Root.ChangeEventDetails` = the change-event details of EVD-API-01 specialized to those reasons, plus `preventUnmountOnClose(): void` (EVD-API-09, EVD §3.7). Source: `dialog/root/DialogRoot.tsx:107-111`.
- `Dialog.Root.Actions` = `{ unmount(): void; close(): void }`. Source: `dialog/root/DialogRoot.tsx:93-96`.
- `Dialog.Root.Props<Payload>`, `Dialog.Root.State`, `Dialog.Trigger.Props<Payload>`/`State`, `Dialog.Portal.Props`/`State`, `Dialog.Backdrop.Props`/`State`, `Dialog.Viewport.Props`/`State`, `Dialog.Popup.Props`/`State`, `Dialog.Title.Props`/`State`, `Dialog.Description.Props`/`State`, `Dialog.Close.Props`/`State`; flat aliases (`DialogRootProps`, `DialogPopupState`, …) are exported from `dialog/index.ts:3-11`. `Dialog.Handle<Payload>` is a class; `createHandle<Payload>()` types the `payload` of every trigger using the handle and the `payload` the Root's function-as-child receives. Test: [L1] `DialogRoot.spec.tsx` (type assertions only: a `number`-typed handle accepts `payload={42}` and rejects `payload={'invalid'}`; the function child receives `number | undefined`).
- `TransitionStatus` = `'starting' | 'ending' | 'idle' | undefined` (`internals/useTransitionStatus.ts`).

## 4. State model

### 4.1 Open state and trigger association

The open state, the active trigger, the payload, `mounted` and `transitionStatus` are the shared popup store fields of PSH §4.3 (PSH-STA-01…46) and the controlled/uncontrolled contract of EVD §4.2. Dialog adds nothing to those rules except the open-change procedure of DLG-STA-15.

- **DLG-STA-14** — MUST. The effective open state is `open` when that prop is not `undefined`, otherwise the internal state seeded from `defaultOpen` (PSH-STA-02); the effective active trigger id is `triggerId` when not `undefined`, otherwise the internal id seeded from `defaultTriggerId` (PSH-STA-03). In controlled mode a declined close leaves the dialog open with its trigger association and trigger-less flag intact (EVD-STA-10).
  Source: `dialog/root/useRenderDialogRoot.tsx:49-66`; `utils/popups/store.ts:148-150`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "keeps the programmatic payload when a controlled dialog declines a close request"; popupConformanceTests "controlled mode".
- **DLG-STA-15** — MUST. Every open-state change request (trigger press, dismissal, Close part, handle, `actionsRef.close`) runs this sequence synchronously: (1) `preventUnmountOnClose` is attached to the details so that calling it writes the store's `preventUnmountingOnClose = true` immediately (EVD-EDGE-09); (2) for a close request whose details carry no `trigger` while an active trigger id exists, `details.trigger` is set to the active trigger element (`undefined` when that element is unknown) (EVD-EVT-16); (3) `onOpenChange(nextOpen, details)` is called; (4) if `details.isCanceled` the sequence stops (EVD-EVT-07); (5) the internal open-change notification is emitted (EVD-EVT-14); (6) the store commits `open`, the active trigger and the trigger-less flag per PSH-STA-12/13/14. Dialog never writes an instant-phase flag (EVD-EVT-17 does not apply).
  Source: `dialog/store/DialogStore.ts:72-95`; `utils/popups/popupStoreUtils.ts:189-226`. Test: [L2] `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button"; `DialogRoot.test.tsx` "emits a single internal openchange when closing on Escape", "does not emit internal openchange when an Escape close is canceled".

### 4.2 Dialog-specific store fields

The store (`DialogStore`) extends the shared popup store with the fields below. `modal`, `disablePointerDismissal`, `nested` and `role` are derived from props every render and synced post-commit (UTL-STA-21).

| Field | Type | Initial | Meaning |
|---|---|---|---|
| `modal` | `boolean \| 'trap-focus'` | `modal` prop (`true`) | Effective modality (forced `true` in alert mode). |
| `disablePointerDismissal` | `boolean` | prop (`false`) | Forced `true` in alert mode. |
| `role` | `'dialog' \| 'alertdialog'` | `'dialog'` | Popup role; `'alertdialog'` in alert mode. |
| `nested` | `boolean` | computed | DLG-STA-03. |
| `openMethod` | `InteractionType \| null` | `null` | Interaction type that opened the dialog (PSH-PTR-01/02, PSH-STA-41). |
| `nestedOpenDialogCount` | `number` | `0` | DLG-STA-04. |
| `nestedOpenDrawerCount` | `number` | `0` | Same for nested Drawers (reported alongside; consumed by Drawer, DRW). |
| `titleElementId` / `descriptionElementId` | `string \| undefined` | `undefined` | DLG-STA-09. |
| `popupElement` / `viewportElement` | `HTMLElement \| null` | `null` | Registered Popup / Viewport elements. |

Store context additions: `popupRef`, `backdropRef`, `internalBackdropRef` (DOM handles of the Popup, the consumer Backdrop and the internal backdrop), `outsidePressEnabledRef` (the outside-press gate of DLG-PTR-05: initially `true` and never written by Dialog or AlertDialog; only `Drawer.SwipeArea` sets it to `false`, from the start of a swipe-open gesture until the next `pointerdown` or press-less click, DRW-DIS-02), `onNestedDialogOpen` (callback children use to report, DLG-STA-05).

- **DLG-STA-01** — MUST. A freshly created Dialog store has `modal: true`, `disablePointerDismissal: false`, `role: 'dialog'`, `nested: false`, `openMethod: null`, `nestedOpenDialogCount: 0`, `nestedOpenDrawerCount: 0`, `titleElementId`/`descriptionElementId: undefined`, `viewportElement: null`, overridden by the Root's initial props (`open` ← `defaultOpen`, `openProp` ← `open`, `activeTriggerId` ← `defaultTriggerId`, `triggerIdProp` ← `triggerId`, and the four prop-derived fields), on top of the shared initial state PSH-STA-01.
  Source: `dialog/store/DialogStore.ts:113-135`; `dialog/root/useRenderDialogRoot.tsx:43,49-63`. Test: [L1] `DialogRoot.test.tsx` "reports nested drawer counts before passive effects" (constructs stores directly).
  Port note 2026-10-28 (ADR 06 Amendments 2026-10-18, rule (a); M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design point 7 (a), settled by the orchestrator 2026-10-28): restated at [L1] over the C# store, `DialogStoreTests` "port: a new dialog store is closed, modal, dismissible by a pointer, the dialog role, top level, with no nested dialog and no labels". The drawer clause (the drawer count and "reports nested drawer counts before passive effects") is open to Drawer (M6) and does not hold `implemented`.
- **DLG-STA-02** — MUST. `modal`, `disablePointerDismissal`, `nested` and `role` in the store follow the Root's props (after mode overrides) and update post-commit, pre-paint when a prop changes; dismissal and focus behavior reads the stored values at event time.
  Source: `dialog/root/useRenderDialogRoot.tsx:35-43,68`. Test: none.
  Port note 2026-10-28 (ADR 05 §1; ADR 01's UTL-STA-19..22 row; M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123), Deliverable 3): the store follows the Root's parameters in `OnParametersSet`, before the render that carries them, earlier than Base UI's post-commit sync, so no render observes a stale value.
- **DLG-STA-03** — MUST. `nested` is `true` iff the Root has an ancestor Dialog, AlertDialog or Drawer Root in the component tree (DLG-STR-12). It is independent of the "floating nested" flag the Root derives from an enclosing floating node (a Dialog inside a Menu popup is floating-nested but not `nested`).
  Source: `dialog/root/useRenderDialogRoot.tsx:41-43`; `utils/popups/popupStoreUtils.ts:76-77`. Test: [L2] `DialogPopup.test.tsx` "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent dialog".
- **DLG-STA-04** — MUST. `nestedOpenDialogCount` is the value most recently reported by a nested Root (DLG-STA-05): the depth of the chain of open dialogs below this one (`1` when a direct child is open, `2` when that child has an open child, …), `0` when no nested dialog has reported itself open. It counts Dialog and AlertDialog descendants alike; Drawers are reported through `nestedOpenDrawerCount` as well.
  Source: `dialog/root/useDialogRoot.ts:26-28,91-99,109-123`. Test: [L2] `DialogPopup.test.tsx` "provides the number of open nested dialogs as a CSS variable", "increments for nested alert dialog and decrements on close (cross-type)".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render an AlertDialog are open to M4 track 9 ([issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)); the row does not hold `implemented` until they run.
- **DLG-STA-05** — MUST. A nested Root reports to its parent post-commit, pre-paint (before any after-paint work): whenever it is open it reports `(ownNestedOpenDialogCount + 1, ownNestedOpenDrawerCount + (1 if drawer mode else 0))`; when it stops being open, or unmounts while open, it reports `(0, 0)`; a Root that is closed never reports (unmounting a closed nested Root leaves the parent's count unchanged).
  Source: `dialog/root/useDialogRoot.ts:109-123`. Test: [L2] `DialogRoot.test.tsx` "reports nested drawer counts before passive effects"; `DialogPopup.test.tsx` "decrements the count when an open nested dialog is unmounted", "does not change the count when a closed nested dialog is unmounted"; `DialogRoot.detached-triggers.test.tsx` "updates the parent nested state each time a handle-backed nested dialog opens and closes". Docs: CHANGELOG.md v1.0.0-alpha.5 "Don't call `onNestedDialogOpen` when unmounting a closed nested dialog".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the drawer clause ("reports nested drawer counts before passive effects", the drawer count) is open to M6 (Drawer); the row does not hold `implemented` until it runs.
- **DLG-STA-06** — MUST. A parent ignores nested reports while it is neither open nor mounted (so a handle-backed Root that keeps its interactions mounted while closed does not accumulate counts), and resets both counts to `0` post-commit, pre-paint once it is closed and unmounted.
  Source: `dialog/root/useDialogRoot.ts:91-106`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "resets nested state recorded while open when a handle-backed dialog closes", "keeps the parent nested state when a closed handle-backed nested dialog mounts", "opens a handle-backed dialog as topmost over a dialog that opened while it was closed".
- **DLG-STA-07** — MUST. A dialog is *topmost* iff its `nestedOpenDialogCount` is `0`. Only the topmost dialog handles <kbd>Escape</kbd> (DLG-KEY-04) and outside presses (DLG-PTR-12).
  Source: `dialog/root/useDialogRoot.ts:28,67,84`. Test: [L4] `DialogRoot.detached-triggers.test.tsx` "keeps the parent nested state when a closed handle-backed nested dialog mounts" (parent backdrop click ignored).
  Port note 2026-10-28 (ADR 04 Amendments 2026-10-28, a fidelity correction of the 2026-10-27 settlement; design point 8 (a), settled by the orchestrator 2026-10-28): the gate is the nested count, as stated. The Root sends its `interactions` session `EscapeKey = false` and `OutsidePress = false` while the count is above zero (and `OutsidePress = false` under `DisablePointerDismissal`) and does not send `Topmost`, the module's latest-open-popup gate, which differs where a later popup is not a nested dialog. Test: [L4] `dialog.spec.ts` "port: a press on the first modal dialog's own backdrop, outside a later non-nested modal dialog's confined backdrop, closes only the first", "port: an outside press that moves no focus closes both of two open non-nested non-modal dialogs" (red with `Topmost` sent); the third case, a Popover inside a modal Dialog, is open to Popover (M4 track 5). For a modal dialog the outside press closes it only when the target is its own backdrop (the internal one or the consumer's, DLG-PTR-10) or an element that contains its Popup, which is why the first of two non-nested modal dialogs closes on a press on its uncovered backdrop; a non-modal dialog with a count of zero closes on any outside press (`dialog/root/useDialogRoot.ts:28,67-80`).
- **DLG-STA-08** — MUST. `openMethod` is written by a Trigger on `pointerdown`/`click` while the dialog is closed with the interaction type of PSH-PTR-01/02 (`'touch'` substituted for an empty type on iOS), reset to `null` post-commit, pre-paint when the effective open state becomes `false` and when the Root unmounts (PSH-STA-41). Opens that do not go through a Trigger (handle, controlled prop, `defaultOpen`) leave it `null`.
  Source: `dialog/trigger/DialogTrigger.tsx:69-74`; `utils/useOpenInteractionType.ts:9-38`; `utils/popups/popupStoreUtils.ts:633-652`. Test: [L4] popupFocusPropsTests "passes the interaction type to a function, and does nothing when it returns undefined" (`mouse`, `keyboard`, `touch`).
- **DLG-STA-09** — MUST. `titleElementId` / `descriptionElementId` hold the rendered id of the mounted Title / Description part (consumer `id` or generated) and revert to `undefined` when that part unmounts; a replacement part (new id) overwrites the value.
  Source: `dialog/title/DialogTitle.tsx:22-24`; `dialog/description/DialogDescription.tsx:22-24`. Test: [L2] `DialogRoot.test.tsx` "keeps accessible names and descriptions in sync when label parts change".
- **DLG-STA-10** — MUST. The Popup element is registered as the store's `popupElement` (and as the floating element for dismissal and focus management, PSH-STA-46 with `treatPopupAsFloatingElement`), and the Viewport element as `viewportElement`, when attached; both are cleared on detach.
  Source: `dialog/popup/DialogPopup.tsx:62,93`; `dialog/viewport/DialogViewport.tsx:31,47`; `dialog/root/useRenderDialogRoot.tsx:49-63`. Test: [L2] `DialogRoot.test.tsx` "allows a custom `id` prop" (popupConformanceTests; `aria-controls` follows the popup element's id).
- **DLG-STA-11** — MUST. The state objects exposed to `className`/`style`/`render` contain exactly: Trigger `{ disabled, open }`; Backdrop `{ open, transitionStatus }`; Viewport and Popup `{ open, transitionStatus, nested, nestedDialogOpen }`; Close `{ disabled }`; Root, Portal, Title and Description `{}`. Server: relaxed, see §9.
  Source: `dialog/trigger/DialogTrigger.tsx:76-79`; `dialog/backdrop/DialogBackdrop.tsx:28-31`; `dialog/viewport/DialogViewport.tsx:35-40`; `dialog/popup/DialogPopup.tsx:64-69`; `dialog/close/DialogClose.tsx:37`; `dialog/root/DialogRoot.tsx:22`; `dialog/portal/DialogPortal.tsx:45`. Test: [L2] describeConformance of every part (RND-DOM-37/39) (L4 for the Server lag of the mirrored `transitionStatus`).
- **DLG-STA-13** — MUST. `nestedDialogOpen` in the Viewport and Popup state is `nestedOpenDialogCount > 0`.
  Source: `dialog/viewport/DialogViewport.tsx:33`; `dialog/popup/DialogPopup.tsx:60`. Test: [L2] `DialogPopup.test.tsx` "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent dialog".

### 4.3 Open/close state diagram

```
closed, unmounted ──(open request, not canceled)──▶ open, mounted, 'starting' → (next frame) idle
                                                        │
        ▲                                               │ close request (not canceled) / controlled open=false
        │                                               ▼
        └──(exit animations finished, or actionsRef.unmount())── closed, mounted, 'ending'
                                                        [kept until unmount() when preventUnmountOnClose() was called]
```

Mount/unmount timing and the transition statuses are TRN-STA-01…14 and TRN-ANI-18…26 configured as in §9.

## 5. Rendered DOM and ARIA

### 5.1 Trigger element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | consumer `id` or generated | DLG-DOM-01 | C# |
| `type="button"` / `role="button"` | native / non-native (UBT-DOM-01/02) | — | C# |
| `disabled` / `aria-disabled` | per UBT-DOM-06, UBT-ARIA-02 | — | C# |
| `data-base-ui-click-trigger=""` | always | DLG-DOM-02 | C# |
| `data-popup-open=""` | `Trigger.State.open` | DLG-DOM-03 | C# |
| `data-disabled=""` | `disabled` | DLG-DOM-04 | C# |
| `aria-haspopup`, `aria-expanded`, `aria-controls` | §5.6 | DLG-ARIA-01..03 | C# |

- **DLG-DOM-01** — MUST. The Trigger renders `id` equal to its trigger id (DLG-API-14).
  Source: `dialog/trigger/DialogTrigger.tsx:47,92`. Test: [L2] detachedTriggersConformanceTests "marks the trigger matching a controlled `triggerId` as active".
- **DLG-DOM-02** — MUST. The Trigger always renders `data-base-ui-click-trigger=""` (PSH-DOM-16), which the focus manager uses to suppress focus-out closing for the press that lands on a trigger (DLG-FOC-14).
  Source: `dialog/trigger/DialogTrigger.tsx:91`; `internals/constants.ts:6`; `floating-ui-react/components/FloatingFocusManager.tsx:389-396`. Test: none.
- **DLG-DOM-03** — MUST. `data-popup-open=""` is present iff `Trigger.State.open` is `true` (PSH-DOM-01); it is absent on inactive triggers of an open dialog and on every trigger of a trigger-less open.
  Source: `dialog/trigger/DialogTrigger.tsx:100`; `utils/popupStateMapping.ts:30-37`. Test: [L2] popupConformanceTests "opens on trigger click and sets `aria-expanded` on the trigger"; `DialogRoot.detached-triggers.test.tsx` "does not associate the only rendered trigger when opened with a payload", "hydrates a detached trigger from the stable fallback store".
- **DLG-DOM-04** — MUST. `data-disabled=""` is present iff `disabled` is `true`, for native and non-native triggers alike.
  Source: `dialog/trigger/DialogTrigger.tsx:77,100` (default projection, RND-DOM-01). Test: [L2] `DialogTrigger.test.tsx` "disables the dialog", "custom element".

### 5.2 Portal node and internal backdrop

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| portal node `id`, `data-base-ui-portal` | `mounted \|\| keepMounted` | DLG-DOM-05 | C# |
| portal node `style` | `display: none` until the portal module moves the node into its container, then the consumer's | DLG-DOM-05 | C# → JS |
| internal backdrop `role`, `data-base-ui-inert`, `style` | `mounted && modal === true` | DLG-DOM-06 | C# |
| internal backdrop `inert` | effective open state `false` | DLG-DOM-06 | C# |

Port note 2026-10-28 (ADR 04 Amendments 2026-10-27, the Writer table for the portal node of Dialog; M4 track 8): the table is added to this section. The portal node is rendered `display: none` until the `portal` module moves it, and the module owns its `style` from attach; the internal backdrop's markup is the shared `PopupInternalBackdrop` writer (ADR 04 Amendments 2026-10-28).

- **DLG-DOM-05** — MUST. The portal node is a `<div>` with a generated `id` and `data-base-ui-portal=""`, carrying the Portal's `className`/`style`/other attributes, appended to the resolved container; it exists iff `mounted || keepMounted`. The Backdrop is *not* automatically inside it (it renders where the consumer places it). Server: relaxed, see §9.
  Source: `dialog/portal/DialogPortal.tsx:28-41`; `floating-ui-react/components/FloatingPortal.tsx:125-134,138-141,270`. Test: [L2] `DialogPortal.test.tsx` describeConformance; `DialogViewport.test.tsx` "renders only when the dialog is mounted by default" (L4 for the container-append clause).
- **DLG-DOM-06** — MUST. The internal backdrop (`<div role="presentation" data-base-ui-inert="" style="position: fixed; inset: 0; user-select: none">`, PSH-DOM-14) is rendered iff `mounted && modal === true`, and carries the `inert` attribute iff the effective open state is `false` (i.e. during the exit transition), so that it stops intercepting pointer input as soon as the close is requested.
  Source: `dialog/portal/DialogPortal.tsx:36-38`; `utils/InternalBackdrop.tsx:18-34`; `packages/utils/src/inertValue.ts:3-9` (UTL-DOM-16). Test: [L4] `DialogRoot.test.tsx` "makes other interactive elements on the page inert when a modal dialog is open", "does not make other interactive elements on the page inert when a non-modal dialog is open". Docs: CHANGELOG.md v1.0.0-alpha.5 "Set `pointer-events` on `InternalBackdrop` based on `open` state", "Use internal backdrop for pointer modality".

### 5.3 Backdrop element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role="presentation"` | always | DLG-ARIA-08 | C# |
| `hidden` | `mounted === false` | DLG-DOM-08 | C# |
| inline `user-select: none; -webkit-user-select: none` | always (before consumer `style`) | DLG-DOM-07 | C# |
| `data-open` / `data-closed` | open / closed | DLG-DOM-09 | C# |
| `data-starting-style` / `data-ending-style` | transition status | DLG-DOM-10 | JS |

- **DLG-DOM-07** — MUST. The Backdrop has inline `user-select: none` and `-webkit-user-select: none`, merged before the consumer `style` (RND-DOM-16), so pressing it does not start a text selection.
  Source: `dialog/backdrop/DialogBackdrop.tsx:41-44`. Test: none. Docs: CHANGELOG.md v1.0.0-alpha.7 "Fix selection on outside press on Firefox with modal prop".
- **DLG-DOM-08** — MUST. The Backdrop has the `hidden` attribute iff `mounted` is `false` (closed and not exiting); the element itself stays in the DOM (DLG-STR-05).
  Source: `dialog/backdrop/DialogBackdrop.tsx:40`. Test: none.
- **DLG-DOM-09** — MUST. The Backdrop renders `data-open=""` iff the effective open state is `true` and `data-closed=""` otherwise (PSH-DOM-03).
  Source: `dialog/backdrop/DialogBackdrop.tsx:36`; `utils/popupStateMapping.ts:48-54,63-70`. Test: none.
- **DLG-DOM-10** — MUST. The Backdrop renders `data-starting-style=""` / `data-ending-style=""` per TRN-DOM-01/02 from the Root's transition status (PSH-DOM-05).
  Source: `dialog/backdrop/DialogBackdrop.tsx:26,36`. Test: none.

### 5.4 Viewport element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `role="presentation"` | always | DLG-ARIA-09 | C# |
| `hidden` | `mounted === false` (only reachable with `keepMounted`) | DLG-DOM-12 | C# |
| inline `pointer-events: none` | `open === false` | DLG-DOM-13 | C# |
| `data-open` / `data-closed`, `data-starting-style` / `data-ending-style` | as for Popup | DLG-DOM-14 | C# (`data-open`/`data-closed`); JS (`data-starting-style`/`data-ending-style`) |
| `data-nested`, `data-nested-dialog-open` | DLG-STA-03 / DLG-STA-13 | DLG-DOM-15 | C# |

- **DLG-DOM-11** — MUST. The Viewport element exists iff the Portal is kept mounted or the dialog is mounted (`keepMounted || mounted`, TRN-ANI-01), and contains its children (the Popup).
  Source: `dialog/viewport/DialogViewport.tsx:22,42,45`. Test: [L2] `DialogViewport.test.tsx` "renders only when the dialog is mounted by default", "stays mounted when used within a keepMounted portal".
- **DLG-DOM-12** — MUST. The Viewport has `hidden` iff `mounted` is `false`.
  Source: `dialog/viewport/DialogViewport.tsx:52`. Test: none.
- **DLG-DOM-13** — MUST. The Viewport has inline `pointer-events: none` iff the effective open state is `false` (so a closing or kept-mounted viewport does not intercept presses), merged before the consumer `style`.
  Source: `dialog/viewport/DialogViewport.tsx:53-55`. Test: none. Docs: CHANGELOG.md v1.6.0 "Fix positioning and viewport edge cases".
- **DLG-DOM-14** — MUST. The Viewport renders `data-open`/`data-closed` (PSH-DOM-03) and `data-starting-style`/`data-ending-style` (TRN-DOM-01/02) from its state.
  Source: `dialog/viewport/DialogViewport.tsx:48`; `dialog/utils/stateAttributesMapping.ts:11-22`. Test: none.
- **DLG-DOM-15** — MUST. The Viewport renders `data-nested=""` iff `nested` and `data-nested-dialog-open=""` iff `nestedDialogOpen`.
  Source: `dialog/utils/stateAttributesMapping.ts:19-21` (`nested` uses the default projection RND-DOM-01). Test: none (Popup equivalents are tested, DLG-DOM-21/22).

### 5.5 Popup element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | consumer `id`, else generated popup id | DLG-DOM-16 | C# |
| `role` | `"dialog"` (`"alertdialog"` in alert mode) | DLG-ARIA-04 | C# |
| `tabindex="-1"`, `data-base-ui-focusable=""` | always | DLG-DOM-17 | C# |
| `hidden` | `mounted === false` | DLG-DOM-18 | C# |
| `aria-labelledby` / `aria-describedby` | Title / Description id | DLG-ARIA-05/06 | C# |
| inline `--nested-dialogs: <n>` | always | DLG-DOM-19 | C# |
| `data-open` / `data-closed`, `data-starting-style` / `data-ending-style` | state | DLG-DOM-20 | C# (`data-open`/`data-closed`); JS (`data-starting-style`/`data-ending-style`) |
| `data-nested` | `nested` | DLG-DOM-21 | C# |
| `data-nested-dialog-open` | `nestedDialogOpen` | DLG-DOM-22 | C# |

- **DLG-DOM-16** — MUST. The Popup renders `id` equal to the generated popup id unless the consumer passes `id`, in which case the consumer value wins and becomes the popup id used for `aria-controls` (PSH-STA-04, PSH-ARIA-03).
  Source: `dialog/popup/DialogPopup.tsx:43,76,91`; `utils/popups/store.ts:152-155`. Test: [L2] popupConformanceTests "allows a custom `id` prop", "has the `aria-controls` attribute on the trigger".
- **DLG-DOM-17** — MUST. The Popup always renders `tabindex="-1"` and `data-base-ui-focusable=""` (PSH-DOM-15): it is programmatically focusable, never in the tab order, and identified as the focus-managed element.
  Source: `dialog/popup/DialogPopup.tsx:80`; `utils/popups/popupStoreUtils.ts:27-30`. Test: [L2] `DialogPopup.test.tsx` "focuses the popup itself rather than inner content when opened by touch"; `DialogRoot.test.tsx` "moves focus to the popup when a focused child is removed on pointerdown…".
- **DLG-DOM-18** — MUST. The Popup has `hidden` iff `mounted` is `false` (kept-mounted closed state, TRN-DOM-06), making it inaccessible.
  Source: `dialog/popup/DialogPopup.tsx:81`. Test: [L2] `DialogPopup.test.tsx` "should keep the dialog mounted when keepMounted=true" (`toBeInaccessible`).
- **DLG-DOM-19** — MUST. The Popup's inline style declares `--nested-dialogs` equal to `nestedOpenDialogCount` (`0` when none), merged before the consumer `style`; it updates synchronously with the count (post-commit, pre-paint of the nested report).
  Source: `dialog/popup/DialogPopup.tsx:87-89`; `dialog/popup/DialogPopupCssVars.ts:5`. Test: [L2] `DialogPopup.test.tsx` "provides the number of open nested dialogs as a CSS variable", "decrements the count when an open nested dialog is unmounted"; `DialogRoot.detached-triggers.test.tsx` "updates the parent nested state each time a handle-backed nested dialog opens and closes".
  Port note 2026-10-28 (ADR 04 Amendments 2026-10-27, `--nested-dialogs` is C#'s, written in the commit path; ADR 16 Amendments 2026-10-27; M4 track 8): the Writer cell reads `C#`, corrected from `C# → JS`. A nested Root reports its count from its commit subscription and the parent's Popup renders the variable in the same batch on both modes, so no module writes it.

- **DLG-DOM-20** — MUST. The Popup renders `data-open=""`/`data-closed=""` from the effective open state and `data-starting-style=""`/`data-ending-style=""` from the transition status (PSH-DOM-03, TRN-DOM-01/02). `data-anchor-hidden` is never rendered (the state has no such field).
  Source: `dialog/popup/DialogPopup.tsx:94`; `dialog/utils/stateAttributesMapping.ts:11-22`. Test: [L2] `dialogRootSharedTests.tsx` "unmounts the popup when the `unmount` method is called" (`data-closed` while retained); `DialogRoot.test.tsx` "waits for the exit transition to finish before unmounting" (`[data-open]` styling) (L4 for the `data-starting-style`/`data-ending-style` clause).
- **DLG-DOM-21** — MUST. The Popup renders `data-nested=""` iff its Root is nested (DLG-STA-03).
  Source: `dialog/popup/DialogPopup.tsx:66,94`; `dialog/popup/DialogPopupDataAttributes.ts:22`. Test: [L2] `DialogPopup.test.tsx` "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent dialog" / "…parent alert dialog".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render an AlertDialog are open to M4 track 9 ([issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)); the row does not hold `implemented` until they run.
- **DLG-DOM-22** — MUST. The Popup renders `data-nested-dialog-open=""` iff `nestedOpenDialogCount > 0`.
  Source: `dialog/utils/stateAttributesMapping.ts:19-21`; `dialog/popup/DialogPopupDataAttributes.ts:26`. Test: [L2] `DialogPopup.test.tsx` "adds the `nested` and `nested-dialog-open` style hooks…"; `DialogRoot.detached-triggers.test.tsx` "resets nested state recorded while open when a handle-backed dialog closes". Docs: CHANGELOG.md v1.0.0-alpha.8 "Rename `data-has-nested-dialogs` to `data-nested-dialog-open`".

### 5.6 Title, Description and Close elements

- **DLG-DOM-23** — MUST. Title and Description render `id` equal to the consumer `id` when given, otherwise a generated id (RND-DOM-27); the same value is registered with the Root (DLG-STA-09).
  Source: `dialog/title/DialogTitle.tsx:22,28`; `dialog/description/DialogDescription.tsx:22,28`. Test: [L2] `DialogRoot.test.tsx` "ARIA attributes". Docs: CHANGELOG.md v1.0.0-alpha.6 "Fix missing `id`s on Title and Description".
- **DLG-DOM-24** — MUST. Close renders `data-disabled=""` iff `disabled`, plus the button mechanism's `type`/`role`/`disabled`/`aria-disabled` attributes (UBT-DOM-01/02/06, UBT-ARIA-02).
  Source: `dialog/close/DialogClose.tsx:37,45-49`; `dialog/close/DialogCloseDataAttributes.ts:4`. Test: [L2] `DialogClose.test.tsx` "disables the button" (`disabled` + `data-disabled`), "custom element" (`aria-disabled="true"`, no `disabled`).

### 5.7 Document-level side effects

- **DLG-DOM-25** — MUST. While the dialog is open (any `modal` value), every element outside the path from `<body>` to the portal node/Popup (excluding `<script>` elements and the focus guards) carries the marker attribute `data-base-ui-inert=""`; the markers are reference-counted across simultaneously open popups and removed when the last one closes. The dismissal logic uses them to ignore presses on elements injected after the dialog opened (DLG-PTR-15). Server: relaxed, see §9.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:620-663`; `floating-ui-react/utils/markOthers.ts:89-187`. Test: none directly (DLG-PTR-15).
- **DLG-DOM-26** — MUST. While the effective open state is `true` and `modal === true`, document scroll is locked per UTL-DOM-01…12 (the shared document scroll lock of UTL-ALG-01/02, enabled while `open && modal === true` with the Popup element as its reference element), targeting the Popup element's document; `'trap-focus'` and `false` never lock. The lock is deferred by a 0 ms task and hands over to/from third-party locks per UTL-DOM-02/03. Server: relaxed, see §9.
  Source: `dialog/root/useDialogRoot.ts:87`; `packages/utils/src/useScrollLock.ts`. Test: [L4] `DialogRoot.test.tsx` "keeps the page locked until it takes over, and unlocks on close" (×3 lockers), "locks immediately when an external <body> lock cannot affect the page".
  Port note 2026-10-28: the 0 ms deferral clause is pinned at [L3] by the `scroll-lock` module, `scroll-lock.test.ts` "port: the lock applies after a 0 ms timer once for nested holders, and releases after a 0 ms timer once the last holder releases" (M4 track 1); a page cannot tell a deferred lock from an `update` still in flight.
- **DLG-DOM-27** — MUST. The scroll lock is released when the effective open state becomes `false` (at close-request time), not when the exit transition ends; the internal backdrop becomes `inert` at the same moment (DLG-DOM-06).
  Source: `dialog/root/useDialogRoot.ts:87` (keyed on `open`, not `mounted`). Test: [L4] `DialogRoot.test.tsx` "keeps the page locked until it takes over, and unlocks on close" (unlocked after close).
- **DLG-DOM-28** — MUST. On every part a consumer-supplied attribute of the same name overrides the component's (RND-DOM-10/19), except that `className`/`style` merge (RND-DOM-14/16) and handlers chain (RND-EVT-01/02); in particular a consumer `role`, `hidden`, `aria-labelledby` or `aria-describedby` on the Popup replaces the component's value.
  Source: `dialog/popup/DialogPopup.tsx:73-92` (`elementProps` last). Test: [L2] describeConformance of every part (RND-DOM-36…48).

### 5.8 ARIA

| Element | Attribute | Value | Req | Writer |
|---|---|---|---|---|
| Trigger | `aria-haspopup` | `"dialog"` | DLG-ARIA-01 | C# |
| Trigger | `aria-expanded` | `"true"` iff `Trigger.State.open`, else `"false"` | DLG-ARIA-02 | C# |
| Trigger | `aria-controls` | popup id iff this trigger owns the open popup (or lone-trigger case), else absent | DLG-ARIA-03 | C# |
| Popup | `role` | `"dialog"` (alert mode: `"alertdialog"`) | DLG-ARIA-04 | C# |
| Popup | `aria-labelledby` | id of the mounted Title, else absent | DLG-ARIA-05 | C# |
| Popup | `aria-describedby` | id of the mounted Description, else absent | DLG-ARIA-06 | C# |
| Backdrop | `role` | `"presentation"` | DLG-ARIA-08 | C# |
| Viewport | `role` | `"presentation"` | DLG-ARIA-09 | C# |
| internal backdrop | `role` | `"presentation"` (PSH-ARIA-06) | DLG-ARIA-10 | C# |
| focus guards | `aria-hidden`; under VoiceOver `role` | `"true"`; under VoiceOver on WebKit `role="button"` without `aria-hidden` | PSH-ARIA-05 | C# (`aria-hidden`); JS (`role`, the VoiceOver swap after attach) |
| outside elements | `aria-hidden` | `"true"` while open and `modal !== false` | DLG-ARIA-11 | JS |

Port note 2026-10-28 (ADR 04 Amendments 2026-10-27, the VoiceOver swap as a JavaScript write after attach; M4 track 8): the focus guards' row was malformed (the value in the attribute column, no requirement) and named C# for the VoiceOver `role="button"`, which the `portal` and `focus-manager` modules write after attach; the row is corrected, and the ownership registry carries the `role` entry.

- **DLG-ARIA-01** — MUST. The Trigger renders `aria-haspopup="dialog"` (PSH-ARIA-04), in every mode.
  Source: `dialog/trigger/DialogTrigger.tsx:93`. Test: [L2] popupConformanceTests "has the `aria-haspopup` attribute on the trigger".
- **DLG-ARIA-02** — MUST. The Trigger renders `aria-expanded="true"` iff `isOpenedByTrigger(ownId)` (PSH-ARIA-01) and `"false"` otherwise — including while the dialog is open through another trigger, through a handle without a trigger, or before a server-rendered detached trigger hydrates.
  Source: `dialog/trigger/DialogTrigger.tsx:49,94`. Test: [L2] popupConformanceTests "opens on trigger click and sets `aria-expanded` on the trigger"; `DialogRoot.test.tsx` "keeps trigger ownership when another trigger mounts while open"; `DialogRoot.detached-triggers.test.tsx` "hydrates a detached trigger from the stable fallback store", "when the popup moves to another trigger".
- **DLG-ARIA-03** — MUST. The Trigger renders `aria-controls="<popup id>"` iff `triggerPopupId(ownId)` resolves (PSH-STA-10, PSH-ARIA-02): it owns the open popup, or it is the only registered trigger of an open popup that was not opened trigger-less; otherwise the attribute is absent.
  Source: `dialog/trigger/DialogTrigger.tsx:50,95`. Test: [L2] popupConformanceTests "has the `aria-controls` attribute on the trigger"; `DialogRoot.test.tsx` "keeps trigger ownership when another trigger mounts while open" (second trigger has no `aria-controls`); `DialogRoot.detached-triggers.test.tsx` "notifies persistent detached triggers after attaching fresh root state on remount".
- **DLG-ARIA-04** — MUST. The Popup renders `role="dialog"` (`"alertdialog"` in alert mode) from the store's `role`.
  Source: `dialog/popup/DialogPopup.tsx:42,79`; `dialog/root/useRenderDialogRoot.tsx:39`. Test: [L2] popupConformanceTests "has the dialog role on the popup"; `dialogRootSharedTests.tsx` (queries by `popupRole`).
- **DLG-ARIA-05** — MUST. The Popup renders `aria-labelledby` equal to the mounted Title's id and omits it when no Title is mounted; it follows Title replacement and removal (DLG-STA-09).
  Source: `dialog/popup/DialogPopup.tsx:40,77`. Test: [L2] `DialogRoot.test.tsx` "ARIA attributes", "keeps accessible names and descriptions in sync when label parts change".
- **DLG-ARIA-06** — MUST. The Popup renders `aria-describedby` equal to the mounted Description's id and omits it when none is mounted, following replacement and removal.
  Source: `dialog/popup/DialogPopup.tsx:30,78`. Test: [L2] `DialogRoot.test.tsx` "ARIA attributes", "keeps accessible names and descriptions in sync when label parts change".
- **DLG-ARIA-07** — MUST. Title and Description MAY be rendered anywhere under the Root (not necessarily inside the Popup) and still label it; a Title/Description rendered under a nested Root labels the nested dialog only.
  Source: `dialog/title/DialogTitle.tsx:20-24` (nearest Root's store). Test: [L2] `DialogRoot.test.tsx` "ARIA attributes" (parts inside Popup; placement elsewhere untested).
- **DLG-ARIA-08** — MUST. The Backdrop renders `role="presentation"` (consumer-overridable).
  Source: `dialog/backdrop/DialogBackdrop.tsx:39`. Test: [L2] `DialogBackdrop.test.tsx` "has role=\"presentation\"".
- **DLG-ARIA-09** — MUST. The Viewport renders `role="presentation"` (consumer-overridable).
  Source: `dialog/viewport/DialogViewport.tsx:51`. Test: none.
- **DLG-ARIA-10** — MUST. The internal backdrop renders `role="presentation"` (PSH-ARIA-06), which is how the tests locate it (`getByRole('presentation', { hidden: true })`).
  Source: `utils/InternalBackdrop.tsx:21`. Test: [L2] `DialogRoot.test.tsx` "calls onOpenChange with the reason for change when user clicks backdrop while the modal dialog is open".
- **DLG-ARIA-11** — MUST. While the dialog is open and `modal !== false`, every element outside the path to the Popup (and outside the portal node, the focus guards, and any nested portal nodes) that is not an `[aria-live]` element or an ancestor of one receives `aria-hidden="true"`; elements that already had a non-`"false"` `aria-hidden` keep it; the attributes are reference-counted across open modal popups and removed when the dialog closes (at `open === false`, before the exit transition ends). With `modal: false` nothing is hidden. Server: relaxed, see §9.
  Source: `dialog/popup/DialogPopup.tsx:105`; `floating-ui-react/components/FloatingFocusManager.tsx:620-663`; `floating-ui-react/utils/markOthers.ts:102-130,147-159`. Test: [L4] `DialogRoot.detached-triggers.test.tsx` "does not let a trigger that mounts while open replace the programmatic payload" (trigger found only with `hidden: true`); `DialogPopup.test.tsx` popupFocusPropsTests "modal" (comment: "marks the rest of the page inert").
  Port note 2026-10-28 (fidelity correction, conventions §3.5): the cited "marks the rest of the page inert" is a source comment (`dialog/popup/DialogPopup.test.tsx:63`), not a title. The row is proven by popupFocusPropsTests' `modal` run and by `dialog.spec.ts` "port: a kept-mounted modal popup moves focus, hides the outside and locks scroll in its opening batch on both modes" and the Server-gap titles.
- **DLG-ARIA-12** — MUST. Dialog never sets the `inert` attribute on outside elements; "pointer interactions on outside elements are disabled" (docs) is realized solely by the internal backdrop (DLG-DOM-06) and `aria-hidden` (DLG-ARIA-11).
  Source: `floating-ui-react/utils/markOthers.ts:13,132-143` (marker only); `dialog/portal/DialogPortal.tsx:36-38`. Test: none. Docs: components/dialog/page.mdx §API reference (`modal`).

## 6. Interactions

Dialog configures three shared mechanisms, all owned by [floating-and-positioning](../architecture/floating-and-positioning.md): the trigger click interaction (`useClick` with defaults: `event: 'click'`, `toggle: true`, `stickIfOpen: true`, reason `trigger-press`), the dismissal interaction (`useDismiss` with the options in §11) and the focus manager (`FloatingFocusManager` with the options in §11). The KEY, PTR and FOC requirements below are configuration requirements (conventions §3.1): each names the option Dialog passes (component source line) and cites the FLT/PSH IDs that specify the resulting behavior; the behavior is restated only as far as needed to make the Dialog outcome testable, and clauses the architecture docs do not cover (Dialog's own `outsidePress` predicate, nesting gate, event-mode getter, `closeOnFocusOut` gating) are stated here in full.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Escape</kbd> | dialog open and topmost; focus anywhere in the document (trigger, popup, body, outside element) | Close request with reason `escape-key`; keydown default prevented unless canceled; propagation stopped unless `allowPropagation()` | DLG-KEY-01, DLG-KEY-02 |
| <kbd>Escape</kbd> | during IME composition (or within 0 ms / 5 ms on WebKit after `compositionend`) | Ignored | DLG-KEY-03 |
| <kbd>Escape</kbd> | dialog has an open nested dialog | Ignored by this dialog (the nested one handles it) | DLG-KEY-04 |
| <kbd>Escape</kbd> | dialog nested in a Menu/Popover popup | Closes the dialog only | DLG-KEY-05 |
| <kbd>Escape</kbd> | controlled `open={true}` whose owner ignores `onOpenChange` | Stays open | DLG-KEY-11 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | `modal: true` or `'trap-focus'`, focus inside popup | Wraps within the popup's tabbable elements | DLG-KEY-06 |
| <kbd>Tab</kbd> / <kbd>Shift</kbd>+<kbd>Tab</kbd> | `modal: true` or `'trap-focus'`, popup has no tabbable content | Default prevented, focus stays on the popup | DLG-KEY-07 |
| <kbd>Tab</kbd> past last / <kbd>Shift</kbd>+<kbd>Tab</kbd> before first | `modal: false` | Focus leaves to the next/previous tabbable after/before the trigger; close with `focus-out` unless `disablePointerDismissal` | DLG-KEY-08 |
| <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>/<kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd>/<kbd>Home</kbd>/<kbd>End</kbd> | keydown anywhere inside the popup | Propagation stopped (not default-prevented) | DLG-KEY-09 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> | Trigger or Close focused | Native button activation (or UBT emulation for `nativeButton={false}`) produces the click of DLG-PTR-01/19 | DLG-KEY-10 |
| any other key | — | Not intercepted | — |

- **DLG-KEY-01** — MUST. The Root passes `escapeKey: isTopmost` to the dismissal mechanism (`useDismiss`) and publishes its reference prop bag as the props of *every* registered trigger (active and inactive, contained and detached) and its floating prop bag on the Popup; the observable result is per FLT-KEY-01 and FLT-DIS-01: while the dialog is open and topmost (DLG-STA-07), an <kbd>Escape</kbd> keydown anywhere in the Popup's document — on the popup, on any registered trigger, or on any other element — requests a close with `reason: 'escape-key'` and the native keydown as `event` (PSH-DIS-01). Dialog-specific: exactly one request is made per keypress even though the popup, the triggers and the document all listen (the first handler's propagation stop, DLG-KEY-02, keeps the keydown from reaching the document listener).
  Source: `dialog/root/useDialogRoot.ts:30,84,125-135`; `floating-ui-react/hooks/useDismiss.ts:201-232,719-724,775-782,784-786`; see FLT-KEY-01, FLT-DIS-01. Test: [L4] `DialogRoot.test.tsx` "rewires dismiss interactions after closing and reopening", "emits a single internal openchange when closing on Escape"; `DialogRoot.detached-triggers.test.tsx` "closes a nonmodal dialog on Escape from an inactive trigger inside a popover on every open", "closes a non-modal dialog with Escape from an inactive detached trigger"; popupFocusPropsTests "passes the close type to a function".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): "closes a nonmodal dialog on Escape from an inactive trigger inside a popover on every open" is open to Popover (M4 track 5, PR 136; §16 Q11); "closes a non-modal dialog with Escape from an inactive detached trigger" runs at [L4] in `dialog.spec.ts` on both modes.
- **DLG-KEY-02** — MUST. The Root leaves `bubbles` unset, so Escape does not bubble (FLT-API-01); the observable result is per FLT-KEY-01 and FLT-EVT-05: when the Escape close is not canceled the keydown's default is prevented and its propagation is stopped unless `allowPropagation()` was called (EVD-EVT-11, EVD-DIS-01); when canceled neither happens and no internal open-change notification is emitted (DLG-STA-15 step 4). Server: relaxed, see §9.
  Source: `dialog/root/useDialogRoot.ts:30-85` (no `bubbles`); `floating-ui-react/hooks/useDismiss.ts:220-231`; see FLT-KEY-01, FLT-EVT-05, FLT-API-01. Test: [L4] `DialogRoot.test.tsx` "does not emit internal openchange when an Escape close is canceled".
- **DLG-KEY-03** — MUST. Dialog's Escape handling (DLG-KEY-01) is subject to the dismissal mechanism's IME guard unchanged; the observable result is per FLT-KEY-03: an <kbd>Escape</kbd> keydown whose `isComposing` is `true`, or that arrives while a composition is in progress or within the settle delay after `compositionend` (0 ms; 5 ms on WebKit), is ignored.
  Source: `dialog/root/useDialogRoot.ts:84`; `floating-ui-react/hooks/useDismiss.ts:212-214,316-335`; see FLT-KEY-03. Test: none in the Dialog suite (see FLT-KEY-03).
- **DLG-KEY-04** — MUST. Because `escapeKey` is `isTopmost` (`nestedOpenDialogCount === 0`), a dialog with an open nested dialog passes `escapeKey: false` and therefore attaches no Escape handling at all (document listener removed, popup/trigger keydown handlers inert, per FLT-DIS-16), so the innermost open dialog is the only one that reacts (EVD-DIS-02); once the nested dialog closes the parent handles <kbd>Escape</kbd> again. This is Dialog's own nesting gate, independent of the floating-tree rule FLT-KEY-02.
  Source: `dialog/root/useDialogRoot.ts:28,84`; `floating-ui-react/hooks/useDismiss.ts:203,719`; see FLT-DIS-16. Test: [L4] `DialogRoot.detached-triggers.test.tsx` "opens a handle-backed dialog as topmost over a dialog that opened while it was closed" (Escape closes the help dialog, section stays).
- **DLG-KEY-05** — MUST. When a dialog is opened from inside a Menu (or other floating popup) and <kbd>Escape</kbd> is pressed, only the dialog closes: the propagation stop of DLG-KEY-02 (FLT-KEY-01 with non-bubbling Escape, FLT-API-01) keeps the keydown from reaching the enclosing popup's handlers, so it stays open.
  Source: `dialog/root/useDialogRoot.ts:30-85`; `floating-ui-react/hooks/useDismiss.ts:228-230`; see FLT-KEY-01. Test: [L4] `DialogRoot.test.tsx` "should not close the parent menu when Escape is pressed in a nested dialog". Docs: CHANGELOG.md v1.0.0-beta.7 "Fix nested dialog from closing on Shift+Tab" (related).
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render a Menu or a Select are open to M5 (Menu, Select); the row does not hold `implemented` until they run.
- **DLG-KEY-06** — MUST. The Popup passes `modal: modal !== false` to the focus manager, so with `modal: true` or `'trap-focus'` the manager is modal and renders its inside guards (FLT-DOM-15); the observable result is per FLT-KEY-05: <kbd>Tab</kbd> from the last tabbable element inside the Popup moves focus (next frame) to the first tabbable element, and <kbd>Shift</kbd>+<kbd>Tab</kbd> from the first moves to the last; focus never reaches elements outside the Popup. Dialog-specific evidence: the wrap holds when the content contains nested scroll containers or `display: contents` wrappers. Server: relaxed, see §9.
  Source: `dialog/popup/DialogPopup.tsx:105`; `floating-ui-react/components/FloatingFocusManager.tsx:1008-1052`; `floating-ui-react/utils/enqueueFocus.ts:12-37`; see FLT-DOM-15, FLT-KEY-05. Test: [L4] `DialogPopup.test.tsx` "keeps trap-focus tab cycling inside the popup when wrapped by a display: contents ancestor"; `DialogRoot.test.tsx` "keeps focus trapped when dialog content contains a non-scrollable scroll area".
  Port note 2026-10-28 (ADR 13 Amendments 2026-10-22; ScrollArea is a later milestone): "keeps focus trapped when dialog content contains a non-scrollable scroll area" is re-cited to `dialog.spec.ts` "port: keeps focus trapped when dialog content contains a non-scrollable scroll container".
- **DLG-KEY-07** — MUST. With `modal: true` or `'trap-focus'` (modal focus manager, DLG-KEY-06) the observable result is per FLT-KEY-04: a <kbd>Tab</kbd> keydown while focus is inside the Popup and the Popup contains no tabbable element has its default prevented and propagation stopped, so focus stays on the Popup.
  Source: `dialog/popup/DialogPopup.tsx:105`; `floating-ui-react/components/FloatingFocusManager.tsx:343-363`; see FLT-KEY-04. Test: none in the Dialog suite (see FLT-KEY-04).
- **DLG-KEY-08** — MUST. With `modal: false` the Popup passes `modal: false` and `closeOnFocusOut: !disablePointerDismissal` to the focus manager inside the Portal; the observable result is per FLT-DOM-14, FLT-KEY-06 and FLT-KEY-07: <kbd>Tab</kbd> from the last tabbable element of the Popup moves focus to the next tabbable element after the active trigger in document order (or the trigger itself when none), and <kbd>Shift</kbd>+<kbd>Tab</kbd> from the first moves to the previous tabbable before the trigger; in both cases a close with `reason: 'focus-out'` is requested when `disablePointerDismissal` is `false`, and return focus is suppressed (FLT-FOC-08). Conversely, <kbd>Tab</kbd> from the trigger enters the Popup's first tabbable element. Server: relaxed, see §9.
  Source: `dialog/popup/DialogPopup.tsx:102,105`; `floating-ui-react/components/FloatingFocusManager.tsx:1012-1026,1034-1050`; `floating-ui-react/components/FloatingPortal.tsx:192-193,252-292`; see FLT-DOM-14, FLT-KEY-06, FLT-KEY-07, FLT-FOC-08. Test: none in the Dialog suite (mechanism tested in FLT).
- **DLG-KEY-09** — MUST. A keydown for <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd>, <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd>, <kbd>Home</kbd> or <kbd>End</kbd> that bubbles to the Popup has its propagation stopped (default not prevented), so composite containers around the dialog (a Toolbar, Menu, or other roving-focus group that contains the trigger or the portal's tree position) do not move their highlight while the dialog is open. This happens unconditionally, including when the key was already handled inside the popup. See [toolbar](toolbar.md) TLB-KEY-15.
  Source: `dialog/popup/DialogPopup.tsx:82-86`; `internals/composite/composite.ts:13-18,22`. Test: [L4] `ToolbarButton.test.tsx` "Dialog › prevents composite keydowns from escaping", "AlertDialog › prevents composite keydowns from escaping" (none in the Dialog suite).
  Port note 2026-10-28: open to Popover (M4 track 5), whose `popup` module adds the composite-key stop the Popup consumes (`StopCompositeKeys`); the cited Toolbar titles pass without it because the Popup is portaled out of the toolbar, so the port title that fails without the stop is the Popup portaled into a `Container` inside a `ToolbarRoot`, added with the option.
- **DLG-KEY-10** — MUST. <kbd>Enter</kbd> and <kbd>Space</kbd> on a focused Trigger or Close activate it through the button mechanism (native activation, or UBT-KEY-01/02 for non-native elements), producing the same open/close request as a pointer click with a keyboard-generated click event; the recorded open interaction type is `'keyboard'`.
  Source: `dialog/trigger/DialogTrigger.tsx:63-66,98`; `dialog/close/DialogClose.tsx:32-35,48`. Test: [L4] popupFocusPropsTests "passes the interaction type to a function, and does nothing when it returns undefined" (`[Enter]` → `'keyboard'`); `DialogRoot.test.tsx` "returns focus into the dialog when a close confirmation opened by an outside press closes" (`[Enter]` on Close).
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render an AlertDialog are open to M4 track 9 ([issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)); the row does not hold `implemented` until they run.
- **DLG-KEY-11** — MUST. With a controlled `open={true}` whose owner does not update it, <kbd>Escape</kbd> calls `onOpenChange(false, …)` but the dialog stays open and keeps its attributes (PSH-DIS-02, EVD-STA-04).
  Source: `utils/popups/store.ts:150`. Test: [L4] popupConformanceTests "is ignored when `open={true}` is passed, so Escape cannot close".

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| Click on a Trigger | dialog closed | Open request, `trigger-press`, `trigger` = that trigger | DLG-PTR-01 |
| Click on the active Trigger | dialog open | Close request, `trigger-press` | DLG-PTR-02 |
| Click on an inactive Trigger | dialog open | Open request, `trigger-press`, switching the active trigger | DLG-PTR-03 |
| Outside press | see event-mode table | Close request, `outside-press` | DLG-PTR-05…17 |
| Secondary/middle button press outside | any | Ignored | DLG-PTR-06 |
| Multi-finger touch outside | any | Ignored | DLG-PTR-07 |
| Press on the internal backdrop / page | `modal: true` | Page does not receive the press (backdrop covers it) | DLG-PTR-18 |
| Click on Close | dialog open | Close request, `close-press` | DLG-PTR-19 |

- **DLG-PTR-01** — MUST. A `click` on a Trigger while the dialog is closed requests an open with `reason: 'trigger-press'`, the native click as `event` and the trigger element as `trigger` (PSH-EVT-02), regardless of pointer type; for touch there is no delay (`touchOpenDelay` 0). Server: relaxed, see §9.
  Source: `dialog/trigger/DialogTrigger.tsx:68,87`; `floating-ui-react/hooks/useClick.ts:60-68,187-219`. Test: [L2] popupConformanceTests "is called with the new open state and the change details"; `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button".
- **DLG-PTR-02** — MUST. A `click` on the *active* Trigger while the dialog is open requests a close with `reason: 'trigger-press'` (toggle behavior), with the trigger element as `trigger`. Server: relaxed, see §9.
  Source: `floating-ui-react/hooks/useClick.ts:102-124,203-219`. Test: [L2] `dialogRootSharedTests.tsx` "unmounts the popup when the `unmount` method is called" (second trigger click closes); detachedTriggersConformanceTests "opens with any trigger on click and marks only that trigger as active" (`closeWith`).
- **DLG-PTR-03** — MUST. A `click` on a Trigger that is *not* the active one while the dialog is open requests an open (`true`, `trigger-press`, that trigger), which re-associates the dialog with the new trigger and its payload without closing (PSH-STA-31).
  Source: `floating-ui-react/hooks/useClick.ts:100-105`. Test: [L2] detachedTriggersConformanceTests "reuses the popup DOM nodes when switching triggers".
- **DLG-PTR-04** — MUST. The Trigger's `pointerdown`/`click` record the open interaction type into `openMethod` only while the dialog is closed (PSH-PTR-01/02, DLG-STA-08); the value is later passed to `initialFocus`/`finalFocus` functions and selects the touch default of DLG-FOC-01.
  Source: `dialog/trigger/DialogTrigger.tsx:69-74,89`. Test: [L4] `DialogPopup.test.tsx` "focuses the popup itself rather than inner content when opened by touch".
- **DLG-PTR-05** — MUST. The Root passes an `outsidePressEvent` getter and an `outsidePress` predicate to the dismissal mechanism; the getter is resolved per press (FLT-API-02) and the resulting mode is applied per FLT-DIS-03, FLT-PTR-26/27/28. The getter returns: when the internal backdrop or a consumer Backdrop element exists → `intentional` for every pointer type (close on the outside `click` whose press began while open); otherwise → mouse/pen: `intentional` for `modal: false`, `sloppy` (close on `pointerdown`) for `modal: 'trap-focus'`; touch: always `sloppy` (DLG-PTR-08). (`modal: true` always has the internal backdrop while mounted, hence always `intentional`.) The predicate first returns `false` while the store's `outsidePressEnabledRef` is `false`; nothing in Dialog or AlertDialog writes it (it stays `true`), and only `Drawer.SwipeArea` clears it, from the start of a swipe-open gesture until the next `pointerdown` or press-less click (DRW-DIS-02). The predicate's remaining clauses are DLG-PTR-06/07/10/11/12 and its `false` result vetoes the close per FLT-DIS-09.
  Source: `dialog/root/useDialogRoot.ts:31-45`; `drawer/swipe-area/DrawerSwipeArea.tsx:134,153,433`; `floating-ui-react/hooks/useDismiss.ts:351-374,522-534`; see FLT-API-02, FLT-DIS-03, FLT-DIS-09, FLT-PTR-26, FLT-PTR-27, FLT-PTR-28. Test: [L4] `DialogRoot.test.tsx` "uses intentional outside press with user backdrop (mouse): closes on click, not on mousedown", "uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown", "closes a trap-focus dialog without a backdrop" (touch), "does not close on a right-button outside press" (trap-focus, pointerdown). Docs: CHANGELOG.md v1.0.0-beta.2 "Use `click` event for outside press dismissal", v1.7.0 "Fix touch outside-press dismissal without a backdrop".
- **DLG-PTR-06** — MUST. The Root's `outsidePress` predicate returns `false` for any event with `button !== 0`, vetoing the close per FLT-DIS-09: only primary-button presses can dismiss; an outside `pointerdown`/`mousedown`/`click` with another button is ignored (no request, no `onOpenChange` call).
  Source: `dialog/root/useDialogRoot.ts:49-51`; `floating-ui-react/hooks/useDismiss.ts:510-512,593`; see FLT-DIS-09. Test: [L4] `DialogRoot.test.tsx` "does not change open state on non-main button clicks", "does not close on a right-button outside press".
- **DLG-PTR-07** — MUST. The Root's `outsidePress` predicate accepts a touch event only as a single-finger tap (vetoing otherwise per FLT-DIS-09): on `touchstart`/`touchmove` exactly one touch point must be active; on `touchend` exactly one touch must have ended and none remain. Two-finger gestures, a touch ending while another is still down, and two touches lifted together are ignored.
  Source: `dialog/root/useDialogRoot.ts:52-64`; see FLT-DIS-09. Test: [L4] `DialogRoot.test.tsx` "does not close when another touch is still active on touchend", "does not close when two touches are lifted simultaneously on touchend", "does not close when multiple touches move past the drag dismissal threshold".
- **DLG-PTR-08** — MUST. When the getter of DLG-PTR-05 selects `sloppy` for touch, the observable result is per FLT-PTR-27: a `touchstart` outside starts a 1000 ms window; a `touchmove` farther than 5 px arms dismissal on `touchend`; farther than 10 px dismisses immediately; a `touchend` within the window dismisses iff armed; after 1000 ms neither `touchend` nor the compatibility `mousedown` dismisses. A plain tap without movement dismisses through the compatibility `mousedown` that follows `touchend` within the window (§16 Q6).
  Source: `dialog/root/useDialogRoot.ts:37-40`; `floating-ui-react/hooks/useDismiss.ts:536-563,599-605,664-712`; see FLT-PTR-27. Test: [L4] `DialogRoot.test.tsx` "closes a non-modal dialog without a backdrop", "closes a trap-focus dialog without a backdrop", "closes as soon as a single touch moves past the drag dismissal threshold".
  Port note 2026-10-28: Test: [L4] `dialog.spec.ts` "port: after the 1000 ms window neither a mousedown during the armed touch nor its lift closes the dialog" (the window advanced with `page.clock`). The compatibility-mousedown clause holds while the touch is down: `touchend` clears the touch state (`floating-ui-react/hooks/useDismiss.ts:704-711`), so a `mousedown` after the lift is judged as a new press.
- **DLG-PTR-09** — MUST. When the getter of DLG-PTR-05 selects `intentional`, the observable result is per FLT-PTR-28: an outside `click` dismisses only if a primary `pointerdown` was observed while the dialog was open (so the trailing click of the press that opened it is ignored), or the click is press-less/virtual (`detail === 0`, keyboard or assistive technology, `element.click()`).
  Source: `dialog/root/useDialogRoot.ts:31-34`; `floating-ui-react/hooks/useDismiss.ts:488-499,586-597`; see FLT-PTR-28. Test: [L4] `DialogRoot.test.tsx` "ignores a native click whose pointerdown opened the dialog" (Chromium only), "closing via outside press: works when clicking another element inside the same shadow root" (`fireEvent.click`, detail 0). Docs: CHANGELOG.md v1.8.0 "Ignore outside clicks from presses that began before open".
- **DLG-PTR-10** — MUST. For a modal dialog (`modal: true`, or `'trap-focus'` while a Backdrop element exists), the Root's `outsidePress` predicate (after the gate and filters of DLG-PTR-05…07, vetoing per FLT-DIS-09) accepts a press only when its target is the internal backdrop, the consumer Backdrop element, or an element that *contains* the Popup and is not a portal node (the Viewport or any consumer wrapper between the portal node and the Popup); presses on any other outside element (another dialog's backdrop, page content behind a non-covering backdrop) are ignored.
  Source: `dialog/root/useDialogRoot.ts:66-79`; see FLT-DIS-09. Test: [L4] `DialogRoot.test.tsx` "calls onOpenChange with the reason for change when user clicks backdrop while the modal dialog is open", "detects clicks on user backdrop", "dismisses non-nested dialogs one by one". Docs: CHANGELOG.md v1.0.0-alpha.6 "Support multiple non-nested modal backdrops".
- **DLG-PTR-11** — MUST. For `modal: false` (and `'trap-focus'` without a Backdrop) the Root's `outsidePress` predicate accepts any outside target once the `outsidePressEnabledRef` gate (DLG-PTR-05; only `Drawer.SwipeArea` closes it) and the filters of DLG-PTR-06/07/12 pass; the mechanism's own target checks DLG-PTR-13…17 still apply (FLT-ALG-17).
  Source: `dialog/root/useDialogRoot.ts:43-45,71,80`; see FLT-DIS-09, FLT-ALG-17. Test: [L4] `DialogRoot.test.tsx` "calls onOpenChange with the reason for change when user clicks outside while the non-modal dialog is open", "still dismisses on a press outside the popup" (closed shadow root).
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design points 9 and 10, ADR 04 Amendments 2026-10-28 and 2026-10-29, PR 144): the shadow-root titles run in `dialog.spec.ts` on both modes, on a closed shadow root a page script holds, since Blazor renders components into the document only (the `dialog-shadow` fixture). The twelve "outside press event with backdrops (shadow DOM)" titles and the four "closed shadow root" cases whose nested popup is outside the shadow root pass, with "still dismisses on a press outside the popup"; the five nested AlertDialog cases are open to M4 track 9. The six whose nested popup sits in the shadow root are port titles ("port: keeps the parent open when the nested dialog inside the shadow root closes on Escape and on an outside press"), by the deviation of design point 10.
- **DLG-PTR-12** — MUST. The Root's `outsidePress` predicate returns `false` while the dialog is not topmost (DLG-STA-07), vetoing per FLT-DIS-09: a dialog with an open nested dialog never dismisses on an outside press; the press is evaluated by the nested dialog instead.
  Source: `dialog/root/useDialogRoot.ts:67,82`; see FLT-DIS-09. Test: [L4] `DialogRoot.detached-triggers.test.tsx` "keeps the parent nested state when a closed handle-backed nested dialog mounts"; `DialogRoot.test.tsx` "does not dismiss previous modal dialog when clicking new modal dialog".
- **DLG-PTR-13** — MUST. Every Trigger registers in the store's trigger map, which the root store exposes to the dismissal mechanism (PSH-STA-26, FLT-STR-03); the observable result is per FLT-DIS-05: a press whose target is, or is inside, any trigger registered with the dialog (active or inactive) is not an outside press.
  Source: `dialog/trigger/DialogTrigger.tsx:54-61`; `floating-ui-react/hooks/useDismiss.ts:414-423`; see FLT-DIS-05. Test: [L4] detachedTriggersConformanceTests "opens with any trigger on click and marks only that trigger as active" (clicking another trigger while open switches instead of closing).
- **DLG-PTR-14** — MUST. Dialog's outside-press handling is subject to the mechanism's scrollbar check unchanged; the observable result is per FLT-DIS-07: a mouse press on the scrollbar area of a scrollable target (vertical: `offsetX > clientWidth`, or `offsetX <= offsetWidth − clientWidth` in RTL; horizontal: `offsetY > clientHeight`; `<html>`/`<body>` always count as scrollable) is not an outside press. Touch presses skip this check.
  Source: `dialog/root/useDialogRoot.ts:30`; `floating-ui-react/hooks/useDismiss.ts:450-482`; see FLT-DIS-07. Test: none in the Dialog suite (see FLT-DIS-07).
- **DLG-PTR-15** — MUST. Dialog's outside-press handling is subject to the mechanism's injected-element check, using the inert markers its focus manager applies (DLG-DOM-25); the observable result is per FLT-DIS-06: a press on an element that was injected into the document after the dialog opened (its top-level ancestor contains none of the `data-base-ui-inert` markers) is not an outside press, unless the target contains the Popup or is `<html>`/`<body>`. The internal backdrop carries the marker itself so it always qualifies (PSH-PTR-06).
  Source: `dialog/root/useDialogRoot.ts:30`; `floating-ui-react/hooks/useDismiss.ts:404-448`; `utils/InternalBackdrop.tsx:24`; see FLT-DIS-06, PSH-PTR-06. Test: [L4] `DialogRoot.test.tsx` "should not dismiss the dialog when dismissing outside a nested modal menu", "…nested select popup" (the nested popup's own backdrop is injected after the dialog opened and is pressed without closing the dialog).
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render a Menu or a Select are open to M5 (Menu, Select); the row does not hold `implemented` until they run.
- **DLG-PTR-16** — MUST. The Popup carries the dismissal mechanism's floating prop bag (capture-phase inside marking; published as in DLG-KEY-01), so in `intentional` mode the observable result is per FLT-PTR-29 (drag clause) and FLT-DIS-12: a primary press that starts inside the Popup and is released outside it (a drag out) suppresses exactly the next outside `click`; a fresh press that starts outside clears the suppression.
  Source: `dialog/root/useDialogRoot.ts:132`; `floating-ui-react/hooks/useDismiss.ts:239-258,387-397,500-507,616-662`; see FLT-PTR-29, FLT-DIS-12. Test: none in the Dialog suite (see FLT-PTR-29).
- **DLG-PTR-17** — MUST. Through the same floating prop bag, the observable result is per FLT-PTR-29 (prevented-press clause) and FLT-DIS-13: when a press that started inside the Popup had its `pointerdown`/`mousedown` default prevented (e.g. a scrub area that requests pointer lock), the next outside `click` is suppressed only for the next 0 ms task, so a later genuine outside click still dismisses.
  Source: `dialog/root/useDialogRoot.ts:132`; `floating-ui-react/hooks/useDismiss.ts:260-274,337-344,645-652`; see FLT-PTR-29, FLT-DIS-13. Test: [L4] `DialogRoot.test.tsx` "dismisses on first outside click after NumberField scrub interaction (pointer lock path)". Docs: CHANGELOG.md v1.0.0-rc.0 "Fix closing after pointer lock exit in Firefox".
  Port note 2026-10-28 (ADR 13 Amendments 2026-10-22; NumberField is a later milestone): the scrub title is re-cited to `dialog.spec.ts` "port: dismisses on the first outside click after a pointerdown inside the popup whose default was prevented" (the test prevents `pointerdown` and `mousedown` at the element, as the scrub area does).
- **DLG-PTR-18** — MUST. While `mounted && modal === true`, the internal backdrop covers the viewport beneath the Popup (PSH-PTR-05): pointer input on the page lands on the backdrop (dismissing per DLG-PTR-10) instead of on page content, and text cannot be selected through it; during the exit transition it is `inert` (DLG-DOM-06) and lets presses through.
  Source: `dialog/portal/DialogPortal.tsx:36-38`; `utils/InternalBackdrop.tsx:26-32`. Test: [L4] `DialogRoot.test.tsx` "should render an internal backdrop when `true`".
- **DLG-PTR-19** — MUST. A `click` on a Close part while the dialog is open requests a close with `reason: 'close-press'`, the native click as `event`, and (by DLG-STA-15 step 2) the active trigger element as `trigger`; while the dialog is closed (kept mounted) the click calls the consumer's `onClick` but requests nothing. A consumer `onClick` that calls `preventBaseUIHandler()` suppresses the request (EVD-EVT-26); a consumer `onClick` of `undefined` has no effect.
  Source: `dialog/close/DialogClose.tsx:30,39-43,48`. Test: [L2] `DialogClose.test.tsx` "closes the dialog when undefined is passed to the `onClick` prop", "does not close the dialog when the Base UI click handler is prevented", "does not request another close when clicked after the dialog has closed"; `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button".

### 6.3 Focus management

Timing vocabulary: *mount commit* = the commit in which `mounted` becomes `true`; *unmount commit* = the commit in which the popup subtree is removed (`mounted` becomes `false`, after the exit animations or `actionsRef.unmount()`).

- **DLG-FOC-01** — MUST. The Popup passes `initialFocus: initialFocus ?? createDefaultInitialFocus(popupRef)` and `openInteractionType: openMethod` to the focus manager; with the default resolver the observable result is per PSH-FOC-01/02 and FLT-FOC-06: when the open interaction type is `touch`, the Popup element itself; otherwise the first tabbable element inside the Popup (tabbable per FLT-FOC-14), falling back to the Popup element when there is none. Dialog-specific evidence: `display: contents` wrappers between the Portal and the Popup do not break the search. Server: relaxed, see §9.
  Source: `dialog/popup/DialogPopup.tsx:57-58,100,103`; `utils/popups/popupStoreUtils.ts:37-40`; `floating-ui-react/components/FloatingFocusManager.tsx:711-725`; `floating-ui-react/utils/tabbable.ts:181-196`; see PSH-FOC-01, PSH-FOC-02, FLT-FOC-06, FLT-FOC-14. Test: [L4] popupFocusPropsTests "focuses the first focusable element by default"; `DialogPopup.test.tsx` "focuses the popup itself rather than inner content when opened by touch", "keeps initial focus working when the popup is wrapped by a display: contents ancestor".
- **DLG-FOC-02** — MUST. The Popup passes `disabled: !mounted`, so the manager is enabled from the mount commit (FLT-FOC-19); initial focus timing is then per FLT-FOC-06 and FLT-FOC-22: applied once per open, the target resolved in a microtask after the mount commit and focused on the next frame (synchronously instead when the open was a virtual/assistive-technology press); not applied if the dialog has closed in the meantime or if focus already moved to another element inside the Popup.
  Source: `dialog/popup/DialogPopup.tsx:101`; `floating-ui-react/components/FloatingFocusManager.tsx:680-759`; `floating-ui-react/utils/enqueueFocus.ts:12-37`; see FLT-FOC-06, FLT-FOC-19, FLT-FOC-22. Test: [L4] popupFocusPropsTests "is not called again when the popup closes"; `DialogRoot.test.tsx` "moves focus to the popup when a focused child is removed on pointerdown…" (open with `defaultOpen`).
- **DLG-FOC-03** — MUST. Per FLT-FOC-06 and FLT-API-05, no initial focus move happens when the active element is already inside the Popup at open time, or when `initialFocus` resolves to `false`/`undefined`.
  Source: `dialog/popup/DialogPopup.tsx:103`; `floating-ui-react/components/FloatingFocusManager.tsx:700-708`; see FLT-FOC-06, FLT-API-05. Test: [L4] popupFocusPropsTests "does not move focus when `false`", "passes the interaction type to a function, and does nothing when it returns undefined".
- **DLG-FOC-04** — MUST. Per FLT-FOC-06, the initial focus call uses `preventScroll: true` only when the target is the Popup element itself (the floating focus element, since the Popup carries `data-base-ui-focusable`, FLT-FOC-05); focusing an inner element may scroll it into view.
  Source: `dialog/popup/DialogPopup.tsx:80`; `floating-ui-react/components/FloatingFocusManager.tsx:738`; see FLT-FOC-06, FLT-FOC-05. Test: none.
- **DLG-FOC-05** — MUST. Because the Popup passes `openInteractionType: openMethod` (DLG-STA-08), an `initialFocus` function receives, per FLT-API-05 and FLT-FOC-06, the open interaction type (`'mouse' | 'touch' | 'pen' | 'keyboard'`), or `''` when the dialog was opened without a trigger (handle, controlled prop, `defaultOpen`: `openMethod` is `null`); it is called exactly once per open and never on close (PSH-FOC-03).
  Source: `dialog/popup/DialogPopup.tsx:100`; `floating-ui-react/components/FloatingFocusManager.tsx:693-697`; see FLT-API-05, FLT-FOC-06. Test: [L4] popupFocusPropsTests "focuses the right element when a function returns $name" (×3), "passes the interaction type to a function…", "is not called again when the popup closes". Docs: CHANGELOG.md v1.0.0-beta.5 "Fix `initialFocus` as function being called on close".
- **DLG-FOC-06** — MUST. With `returnFocus: finalFocus` resolving to the default, the return target is per PSH-FOC-04, FLT-ALG-21 and FLT-FOC-20, where the DOM anchor is the active trigger element: the active trigger element if it is still connected, otherwise the element that was focused before the dialog opened (if connected and not `<body>`), otherwise the most recent still-connected entry of the shared previously-focused stack (DLG-FOC-16). When the dialog was opened without a trigger interaction (`openMethod === null` at mount: handle, controlled prop, `defaultOpen`) the order of the first two is reversed: previously-focused element first, then the active trigger.
  Source: `dialog/popup/DialogPopup.tsx:100,104`; `floating-ui-react/components/FloatingFocusManager.tsx:788-794,863-881`; see FLT-ALG-21, FLT-FOC-20. Test: [L4] popupFocusPropsTests "focuses the trigger by default"; `DialogPopup.test.tsx` "moves final focus to trigger if initialFocus points outside the popup and finalFocus is not specified"; `DialogRoot.test.tsx` "returns focus to the menu trigger when a detached dialog trigger unmounts" (trigger gone → previously focused menu trigger). Docs: CHANGELOG.md v1.6.0 "Fix programmatic focus return".
  Port note 2026-10-28 (ADR 13 Amendments 2026-10-22; Menu is M5): "returns focus to the menu trigger when a detached dialog trigger unmounts" is re-cited to `dialog.spec.ts` "port: returns focus to the panel toggle when a detached dialog trigger in the closed panel unmounted" (a plain toggled panel in the menu's place).
- **DLG-FOC-07** — MUST. Because the manager is disabled at `!mounted` (DLG-FOC-02), return focus is applied per FLT-FOC-07 and FLT-EDGE-07 in a microtask after the unmount commit (so after the exit animations, or immediately on `actionsRef.unmount()`); if the dialog reopens before that commit the pending return is cancelled.
  Source: `dialog/popup/DialogPopup.tsx:101`; `floating-ui-react/components/FloatingFocusManager.tsx:776-786,884-939`; see FLT-FOC-07, FLT-EDGE-07. Test: [L4] popupFocusPropsTests "focuses the element of a ref" (finalFocus); `DialogRoot.test.tsx` "returns focus into the dialog when a close confirmation opened by an outside press closes".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render an AlertDialog are open to M4 track 9 ([issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)); the row does not hold `implemented` until they run.
- **DLG-FOC-08** — MUST. Per FLT-FOC-07 and FLT-KEY-09, the return-focus call uses `preventScroll: true`, and `focusVisible: true` when the close interaction type is `keyboard`; the element focused is the return target if tabbable, else its first tabbable descendant, else the target itself.
  Source: `dialog/popup/DialogPopup.tsx:104`; `floating-ui-react/components/FloatingFocusManager.tsx:102-112,911,929-933`; see FLT-FOC-07, FLT-KEY-09. Test: none.
- **DLG-FOC-09** — MUST. The close interaction type passed to a `finalFocus` function (PSH-FOC-05) is classified per FLT-FOC-10 from the native event of the close: `keyboard` for keyboard events and keyboard-generated clicks (`detail === 0`, no `pointerType`); the pointer's `pointerType` for pointer/mouse events, falling back to the last interaction type recorded on `pointerdown` since the open; `touch` for touch events; `''` when there is no event (imperative close).
  Source: `dialog/popup/DialogPopup.tsx:104`; `floating-ui-react/components/FloatingFocusManager.tsx:50-76,312-313,386-387,796-799`; see FLT-FOC-10. Test: [L4] popupFocusPropsTests "passes the close type to a function" (`mouse` then `keyboard`), "focuses the right element when a function returns $name" (×3).
- **DLG-FOC-10** — MUST. Per FLT-FOC-07, FLT-FOC-08 and FLT-FOC-21, return focus is skipped when: `finalFocus` resolves to `false`/`undefined`; the close was a `focus-out` through a focus guard or the non-modal focus-out of DLG-FOC-14 (EVD-FOC-03); or `finalFocus` is boolean (not an explicit element/handle/function; Dialog passes no `explicitReturnFocus`) and focus has already moved to an element outside the dialog's tree that is not `<body>`. A skipped return still clears the suppression for the next close.
  Source: `dialog/popup/DialogPopup.tsx:104`; `floating-ui-react/components/FloatingFocusManager.tsx:565,801-808,913-927,937`; see FLT-FOC-07, FLT-FOC-08, FLT-FOC-21. Test: [L4] popupFocusPropsTests "does not move focus when `false`" (focus falls to `<body>` when the Close control is removed); `DialogPopup.test.tsx` "respects finalFocus when initialFocus points outside the popup".
- **DLG-FOC-11** — MUST. Per FLT-FOC-08, an `outside-press` close returns focus per DLG-FOC-06 when the browser supports `focus({ preventScroll })` (checked at close time) or the press was virtual, or the dialog is floating-nested (inside another floating node, DLG-STA-03 note); otherwise return focus is suppressed to avoid a scroll jump.
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:810-842`; see FLT-FOC-08. Test: [L4] `DialogRoot.test.tsx` "returns focus into the dialog when a close confirmation opened by an outside press closes" (indirect).
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render an AlertDialog are open to M4 track 9 ([issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)); the row does not hold `implemented` until they run.
- **DLG-FOC-12** — MUST. With `modal: true` or `'trap-focus'` the Popup passes `modal: true` to the focus manager; the observable result is per FLT-DOM-15 (inside guards, implementing DLG-KEY-06/07), FLT-DOM-14 (the Portal renders no outside guards) and FLT-ARIA-01 (the outside is `aria-hidden`, DLG-ARIA-11).
  Source: `dialog/popup/DialogPopup.tsx:105`; `floating-ui-react/components/FloatingPortal.tsx:192-193`; `floating-ui-react/components/FloatingFocusManager.tsx:1003-1004`; see FLT-DOM-14, FLT-DOM-15, FLT-ARIA-01. Test: [L4] `DialogRoot.test.tsx` "keeps focus trapped when dialog content contains a non-scrollable scroll area".
  Port note 2026-10-28 (ADR 13 Amendments 2026-10-22; ScrollArea is a later milestone): "keeps focus trapped when dialog content contains a non-scrollable scroll area" is re-cited to `dialog.spec.ts` "port: keeps focus trapped when dialog content contains a non-scrollable scroll container".
- **DLG-FOC-13** — MUST. With `modal: false` the Popup passes `modal: false` to a focus manager rendered inside `Dialog.Portal` (`FloatingPortal`); the observable result is per FLT-DOM-14, FLT-ARIA-04, FLT-FOC-17, FLT-FOC-18 and FLT-DOM-18: the Portal renders a pair of outside focus guards (plus a visually hidden `aria-owns` owner span referencing the portal node; Dialog leaves `portalOwnerRole` unset) at the Portal's position in the DOM while the dialog is open, so the portaled Popup takes its place in the tab order right after the Portal's position; while focus is outside the portal node the tabbable elements inside it are temporarily given `tabindex="-1"` (original value kept in `data-tabindex`) and restored when focus re-enters or the dialog reopens.
  Source: `dialog/popup/DialogPopup.tsx:105`; `dialog/portal/DialogPortal.tsx:33-42`; `floating-ui-react/components/FloatingPortal.tsx:192-234,252-292`; `floating-ui-react/utils/tabbable.ts:264-284`; see FLT-DOM-14, FLT-ARIA-04, FLT-FOC-17, FLT-FOC-18, FLT-DOM-18. Test: none in the Dialog suite.
- **DLG-FOC-14** — MUST. The Popup passes `closeOnFocusOut: !disablePointerDismissal` and `modal: modal !== false`; the observable result is per FLT-FOC-11, FLT-ALG-22 and FLT-FOC-13: with `modal: false` and `disablePointerDismissal: false`, when focus leaves the Popup (or the active trigger) for an element that is not the trigger, not inside the Popup/portal node, not inside any registered trigger, not a focus guard, and not inside a nested or ancestor floating element, a close with `reason: 'focus-out'` (the `focusout` event as `event`) is requested after a microtask, with return focus suppressed — unless the destination is the element that was focused before the dialog opened, or a `pointerdown` on a click trigger happened within the last 0 ms task (DLG-DOM-02). Modal regimes never close on focus-out. Server: relaxed, see §9.
  Source: `dialog/popup/DialogPopup.tsx:102,105`; `floating-ui-react/components/FloatingFocusManager.tsx:424-617,551-567`; see FLT-FOC-11, FLT-ALG-22, FLT-FOC-13. Test: none in the Dialog suite directly (`disablePointerDismissal` rows cover the pointer path). Docs: CHANGELOG.md v1.6.0 "Fix non-modal focus-out close and tabindex management".
- **DLG-FOC-15** — MUST. The Popup passes `restoreFocus: 'popup'`; the observable result is per FLT-FOC-12, with the additional Dialog-relevant condition that it runs only while `closeOnFocusOut` is `true` (i.e. `disablePointerDismissal` is `false` and not alert mode), because the manager's whole focus-out handling is skipped otherwise: when the focused element inside the Popup becomes not visible or is removed (focus falls to `<body>`), focus is moved back to the Popup element immediately and again on the next frame, without closing the dialog; a subsequent outside press still dismisses. With `disablePointerDismissal: true` (any `modal` value) focus stays on `<body>` (ADL-FOC-03).
  Source: `dialog/popup/DialogPopup.tsx:102,106`; `floating-ui-react/components/FloatingFocusManager.tsx:424-427,505-529`; see FLT-FOC-12. Test: [L4] `DialogRoot.test.tsx` "moves focus to the popup when a focused child is removed on pointerdown and outside press still dismisses". Docs: CHANGELOG.md v1.0.0-beta.3 "Restore focus to popup when focused element is removed".
- **DLG-FOC-16** — MUST. The shared previously-focused stack of FLT-FOC-09 / FLT-EDGE-06 (global, bounded to 20 entries, disconnected entries dropped) is fed by Dialog as follows: the element focused before each open is pushed at mount (every regime); additionally, only when the Popup's manager is modal (`modal !== false`) **and** `closeOnFocusOut` is `true` (`disablePointerDismissal` is `false`, not alert mode), an element inside the Popup that loses focus to `<body>` (e.g. on a backdrop press) is pushed. This lets a confirmation dialog opened while `<body>` is focused return focus into the dialog that was interrupted; a non-modal dialog's fields, and a dialog with `disablePointerDismissal`, never feed the second clause (ADL-FOC-04).
  Source: `dialog/popup/DialogPopup.tsx:102,105`; `floating-ui-react/components/FloatingFocusManager.tsx:78-100,424-427,452-456,794,873-875,999`; see FLT-FOC-09, FLT-EDGE-06. Test: [L4] `DialogRoot.test.tsx` "returns focus into the dialog when a close confirmation opened by an outside press closes", "does not leak a non-modal popup field into an unrelated dialog return focus". Docs: CHANGELOG.md v1.6.0 "Fix confirmation return focus".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render an AlertDialog are open to M4 track 9 ([issue 124](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/124)); the row does not hold `implemented` until they run.
- **DLG-FOC-17** — SHOULD. Dialog's focus manager applies the WebKit unmount guard unchanged; the observable result is per FLT-FOC-16: on WebKit, when the effective open state becomes `false` while a typeable element (input, textarea, contenteditable) inside the Popup has focus, that element is blurred post-commit, pre-paint (before the popup unmounts).
  Source: `dialog/popup/DialogPopup.tsx:98-107`; `floating-ui-react/components/FloatingFocusManager.tsx:957-970`; see FLT-FOC-16. Test: none.
- **DLG-FOC-18** — MUST. The Popup's `tabindex` is never rewritten by the focus manager (it always carries `tabindex="-1"` without `data-tabindex`, which disables the manager's dialog `tabindex` adjustment); a Popup with no tabbable content is therefore reachable only programmatically and by DLG-FOC-01's fallback (see §16 Q2).
  Source: `floating-ui-react/components/FloatingFocusManager.tsx:114-124,993-1001`; `dialog/popup/DialogPopup.tsx:80`. Test: none.

### 6.4 Dismissal

- **DLG-DIS-01** — MUST. `disablePointerDismissal: true` (or alert mode) makes every outside press a no-op for this dialog — `onOpenChange` is not called — and, through `closeOnFocusOut: false`, disables the focus-out close of DLG-FOC-14 and DLG-KEY-08 for `modal: false` (focus may still leave) and, for every `modal` value, the focus recovery of DLG-FOC-15 and the backdrop-press stack push of DLG-FOC-16.
  Source: `dialog/root/useDialogRoot.ts:67`; `dialog/popup/DialogPopup.tsx:102`; `floating-ui-react/components/FloatingFocusManager.tsx:424-427`. Test: [L4] `DialogRoot.test.tsx` "does not close the dialog when clicking outside if disablePointerDismissal=true".
- **DLG-DIS-02** — MUST. <kbd>Escape</kbd>, Close parts, trigger toggling and imperative closes are not affected by `disablePointerDismissal`.
  Source: `dialog/root/useDialogRoot.ts:84` (`escapeKey` depends only on `isTopmost`). Test: [L4] `DialogRoot.detached-triggers.test.tsx` "keeps a handle-backed dialog open and controllable across handle swaps on a live root" (`disablePointerDismissal` + `handle.close()`).
- **DLG-DIS-03** — MUST. Among nested dialogs, only the innermost open one (topmost, DLG-STA-07) reacts to <kbd>Escape</kbd> and outside presses; a press on the parent's Backdrop while a nested dialog is open closes nothing, and closing the nested dialog (by any means) leaves the parent open and restores its dismissal.
  Source: `dialog/root/useDialogRoot.ts:28,67,84`. Test: [L4] `DialogRoot.detached-triggers.test.tsx` "keeps the parent nested state when a closed handle-backed nested dialog mounts", "opens a handle-backed dialog as topmost over a dialog that opened while it was closed"; `DialogRoot.test.tsx` "keeps the parent open on close (outer: $outer, nested: $nested)" (×15).
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design points 9 and 10, ADR 04 Amendments 2026-10-28 and 2026-10-29, PR 144): the shadow-root titles run in `dialog.spec.ts` on both modes, on a closed shadow root a page script holds, since Blazor renders components into the document only (the `dialog-shadow` fixture). The twelve "outside press event with backdrops (shadow DOM)" titles and the four "closed shadow root" cases whose nested popup is outside the shadow root pass, with "still dismisses on a press outside the popup"; the five nested AlertDialog cases are open to M4 track 9. The six whose nested popup sits in the shadow root are port titles ("port: keeps the parent open when the nested dialog inside the shadow root closes on Escape and on an outside press"), by the deviation of design point 10.
- **DLG-DIS-04** — MUST. Modal dialogs that are open simultaneously without being nested (siblings) dismiss independently: a click on a backdrop closes only the dialog that owns that backdrop (DLG-PTR-10), so stacked sibling modals close one by one from the top.
  Source: `dialog/root/useDialogRoot.ts:71-79`. Test: [L4] `DialogRoot.test.tsx` "dismisses non-nested dialogs one by one", "does not dismiss previous modal dialog when clicking new modal dialog".
- **DLG-DIS-05** — MUST. Dismissing an anchored popup (Menu, Select, Popover…) rendered inside the dialog by pressing that popup's own backdrop does not dismiss the dialog (DLG-PTR-15); presses inside the dialog's Popup subtree never count as outside presses even when they land in a nested portal.
  Source: `floating-ui-react/hooks/useDismiss.ts:399-402,437-448,484-486,792-803`. Test: [L4] `DialogRoot.test.tsx` "should not dismiss the dialog when dismissing outside a nested modal menu", "should not dismiss the dialog when dismissing outside a nested select popup".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the titles that render a Menu or a Select are open to M5 (Menu, Select); the row does not hold `implemented` until they run.
- **DLG-DIS-06** — MUST. Outside-press dismissal works when the Portal container is an open or closed shadow root and when the press target is in a different root than the Popup (inside the same shadow root, outside it in the document, or in the document while the Popup is in the shadow root); closing a nested dialog inside a closed shadow root (by its Close part) leaves the parent open with no `onOpenChange` call on the parent.
  Source: `dialog/root/useDialogRoot.ts:66`; `floating-ui-react/hooks/useDismiss.ts:164-167,293-295,404-411,792` (composed-path targeting, UTL-DOM-20/22). Test: [L4] `DialogRoot.test.tsx` "closing via outside press: works when clicking another element inside the same shadow root", "…works when clicking outside the shadow root", "…works for a modal dialog when clicking outside the shadow root", "closing via intentional outside press with user backdrop (modal=true): works when portaled into a shadow DOM", "still dismisses on a press outside the popup", "keeps the parent open on close (outer: $outer, nested: $nested)". Docs: CHANGELOG.md v1.9.0 "Keep parent open when closing a nested dialog in a closed shadow root".
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design points 9 and 10, ADR 04 Amendments 2026-10-28 and 2026-10-29, PR 144): the shadow-root titles run in `dialog.spec.ts` on both modes, on a closed shadow root a page script holds, since Blazor renders components into the document only (the `dialog-shadow` fixture). The twelve "outside press event with backdrops (shadow DOM)" titles and the four "closed shadow root" cases whose nested popup is outside the shadow root pass, with "still dismisses on a press outside the popup"; the five nested AlertDialog cases are open to M4 track 9. The six whose nested popup sits in the shadow root are port titles ("port: keeps the parent open when the nested dialog inside the shadow root closes on Escape and on an outside press"), by the deviation of design point 10.
  Port: deviation 2026-10-29 (design point 10 of M4 track 8, settled by the orchestrator 2026-10-29; ADR 04 Amendments 2026-10-29; the platform followed in [issue 146](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/146)): inside a portal container in a shadow root, C# event handlers do not fire, the parts' own (a Close's click) and the consumer's alike, because Blazor dispatches them from the document's event target, which is the shadow host for an event raised inside a shadow root; the modules' paths work there (a trigger's press, Escape, the outside press, focus management). A consumer who portals a dialog into a shadow root closes it through those paths, not through an `@onclick` inside it.
- **DLG-DIS-07** — MUST. A close request canceled in `onOpenChange` leaves the dialog open, mounted and focused as before, emits no internal open-change notification, and (for `escape-key`) leaves the keydown's default and propagation untouched (EVD-EVT-05/07). Server: relaxed, see §9.
  Source: `dialog/store/DialogStore.ts:88-90`. Test: [L4] `DialogRoot.test.tsx` "does not emit internal openchange when an Escape close is canceled"; popupConformanceTests "keeps the popup closed when the open change is canceled" (open side).
- **DLG-DIS-08** — MUST. A dialog opened through a controlled `open={true}` that the owner never updates stays open through every dismissal path (PSH-DIS-02); a controlled owner that declines a close keeps the trigger-less flag and payload (EVD-STA-10).
  Source: `utils/popups/store.ts:150`; `utils/popups/popupStoreUtils.ts:445-457`. Test: [L4] popupConformanceTests "is ignored when `open={true}` is passed, so Escape cannot close"; `DialogRoot.detached-triggers.test.tsx` "keeps the programmatic payload when a controlled dialog declines a close request".
- **DLG-DIS-09** — MUST. `actionsRef.current.close()` and `handle.close()` request a close with `reason: 'imperative-action'`, a synthetic `base-ui` event, and the active trigger element as `trigger` when one exists (`undefined` otherwise) (PSH-DIS-03, DLG-STA-15 step 2).
  Source: `dialog/root/useRenderDialogRoot.tsx:84`; `dialog/store/DialogHandle.ts:64-66`; `dialog/store/DialogStore.ts:80-84`. Test: [L2] `dialogRootSharedTests.tsx` "closes the popup when the `close` method is called"; `DialogRoot.test.tsx` "reports no trigger when closing with an initial trigger id that is not mounted".
- **DLG-DIS-10** — MUST. In alert mode (`useRenderDialogRoot('alert-dialog', …)`) `modal` is forced to `true` and `disablePointerDismissal` to `true` regardless of props, so outside presses never dismiss and focus-out never closes; the remaining close paths are <kbd>Escape</kbd>, Close parts, a click on the active Trigger (DLG-PTR-02), imperative closes, and a controlled `open` change (ADL-DIS-02); the popup role is `alertdialog`. The `'drawer'` mode flag changes nothing in this section except reporting the Root in `nestedOpenDrawerCount` (DLG-STA-05); Drawer adds dismissal behavior through its own Root wrapper and parts (the `close-watcher` dismissal DRW-DIS-01, the SwipeArea outside-press guard DRW-DIS-02, the snap-point reset after a close DRW-STA-04), specified in [drawer](drawer.md).
  Source: `dialog/root/useRenderDialogRoot.tsx:35-39`; `dialog/root/useDialogRoot.ts:114`. Test: [L4] `AlertDialogRoot.test.tsx` "enforces alert dialog state for handle-backed roots", "does not close when the backdrop is clicked" (mapped in detail by [alert-dialog](alert-dialog.md)).

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onOpenChange` | `(open: boolean, details: Dialog.Root.ChangeEventDetails) => void` | On every open/close *request*, synchronously inside the originating input, before any state or DOM change (EVD-EVT-02) | DLG-EVT-02 | yes (`details.cancel()`) |
| `onOpenChangeComplete` | `(open: boolean) => void` | `true`: on animations finished of the Popup after an open; `false`: when the popup subtree unmounts after a close | — | no |

- **DLG-EVT-01** — MUST. `onOpenChange` is called exactly once per request with `(nextOpen, details)` where `details` has the shape of EVD-API-01 plus `preventUnmountOnClose()` (EVD-API-09); `details.event` is the native event of the originating interaction, or a synthetic `base-ui` event for imperative requests (EVD-API-04/05).
  Source: `dialog/store/DialogStore.ts:72-86`; `internals/createBaseUIEventDetails.ts:126-134`. Test: [L2] `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button"; popupConformanceTests "is called with the new open state and the change details".
- **DLG-EVT-02** — MUST. The reasons Dialog emits, and the user action producing each, are exactly:

  | Reason | `open` | Produced by |
  |---|---|---|
  | `trigger-press` | `true` / `false` | Click (any pointer type, or keyboard-generated click) on a Trigger (DLG-PTR-01/02/03) |
  | `outside-press` | `false` | Qualifying outside press (DLG-PTR-05…17) |
  | `escape-key` | `false` | <kbd>Escape</kbd> keydown (DLG-KEY-01) |
  | `close-press` | `false` | Click on a Close part (DLG-PTR-19) |
  | `focus-out` | `false` | `modal: false` only: focus leaving the popup (DLG-FOC-14) or tabbing through a portal guard (DLG-KEY-08) |
  | `imperative-action` | `true` / `false` | `handle.open(id)`, `handle.openWithPayload(p)` (open); `handle.close()`, `actionsRef.current.close()` (close) |
  | `none` | — | Declared in the type; never emitted by Dialog in v1.9.0 (§16 Q3) |

  Source: `dialog/root/DialogRoot.tsx:98-105`; `floating-ui-react/hooks/useClick.ts:67,83`; `floating-ui-react/hooks/useDismiss.ts:220,518`; `dialog/close/DialogClose.tsx:41`; `floating-ui-react/components/FloatingFocusManager.tsx:566`; `floating-ui-react/components/FloatingPortal.tsx:283-288`; `dialog/store/DialogHandle.ts:56`; `utils/popups/popupHandle.ts:259-262,284`; `dialog/root/useRenderDialogRoot.tsx:84`. Test: [L2] `DialogRoot.test.tsx` "calls onOpenChange with the reason for change when user clicks backdrop while the modal dialog is open" (`outside-press`), "emits a single internal openchange when closing on Escape" (`escape-key`); `dialogRootSharedTests.tsx` (`trigger-press`, `close-press`, `imperative-action`).
- **DLG-EVT-03** — MUST. `details.trigger` is: on open, the Trigger element that was clicked, or the registered element for `handle.open(id)`, or `undefined` for `open(null)`/`openWithPayload`/controlled opens; on close, the element given by the closing interaction (the active trigger for a trigger-press close) or, when the interaction supplies none and an active trigger id exists, the active trigger element back-filled by DLG-STA-15 (so Close/Escape/outside-press/imperative closes report the trigger that opened the dialog); `undefined` when there is no active trigger element (e.g. a `defaultTriggerId` that never mounted).
  Source: `dialog/store/DialogStore.ts:80-84`; `floating-ui-react/hooks/useClick.ts:83`. Test: [L2] `dialogRootSharedTests.tsx` "reports the trigger and reason when opened by a trigger and closed by a close button"; `DialogRoot.test.tsx` "reports no trigger when closing with an initial trigger id that is not mounted"; `dialogRootSharedTests.tsx` "opens with a payload set programmatically" (no trigger). Docs: components/dialog/page.mdx §Controlled mode with multiple triggers.
- **DLG-EVT-04** — MUST. Calling `details.cancel()` inside `onOpenChange` stops the request (EVD-EVT-05/07): uncontrolled state does not change, no internal notification is emitted, trigger association and the trigger-less flag are untouched. In controlled mode the owner must itself skip its state update (EVD-EVT-06).
  Source: `dialog/store/DialogStore.ts:88-90`. Test: [L2] `DialogRoot.test.tsx` "does not emit internal openchange when an Escape close is canceled"; popupConformanceTests "keeps the popup closed when the open change is canceled"; `DialogRoot.test.tsx` "returns focus into the dialog when a close confirmation opened by an outside press closes" (cancel + confirmation).
- **DLG-EVT-05** — MUST. `details.preventUnmountOnClose()` writes the store's unmount-prevention flag the moment it is called (EVD-EDGE-09) — even if the same callback later cancels — and, for a non-canceled close, keeps the popup subtree mounted in the `'ending'` phase with `data-closed` until `actionsRef.current.unmount()` is called (TRN-ANI-23, EVD-EVT-21); the flag is cleared when the dialog next opens (TRN-ANI-24) and by the unmount itself, so the following close unmounts automatically again.
  Source: `dialog/store/DialogStore.ts:76-78`; `internals/useUnmountAfterClose.ts:71-76,118-127`; `utils/popups/popupStoreUtils.ts:586-595`. Test: [L2] `dialogRootSharedTests.tsx` "unmounts the popup when the `unmount` method is called", "clears manual unmount state after the `unmount` method is called".
  Port note 2026-10-28 (§16 Q1 resolved as observed; ADR 05 Amendments 2026-10-27): the call is recorded at once (the store's `UnmountPreventionAtCall`). Test: [L2] `DialogRootTests` "port: preventUnmountOnClose called in a canceled close keeps the next close mounted until unmount"; `OpenChangeEventDetails`' remark states the rule.
- **DLG-EVT-06** — MUST. After every non-canceled request and before the state commit, exactly one internal open-change notification `{ open, reason, nativeEvent, nested, triggerElement }` is emitted to the focus manager and dismissal logic (EVD-EVT-14), where `nested` is the floating-nested flag (DLG-STA-03 note), not `Dialog.Root`'s `nested`. Server: relaxed, see §9.
  Source: `dialog/store/DialogStore.ts:92`; `floating-ui-react/components/FloatingRootStore.ts:106-118`. Test: [L2] `DialogRoot.test.tsx` "emits a single internal openchange when closing on Escape".
- **DLG-EVT-07** — MUST. `onOpenChange` is not called on mount (including `defaultOpen`), not called when the owner changes the controlled `open`/`triggerId` props, and not called when a Trigger becomes the active trigger through the lone-trigger claim (PSH-EVT-03, PSH-STA-18).
  Source: `dialog/root/useRenderDialogRoot.tsx:65-66` (prop sync only). Test: [L2] popupConformanceTests "opens initially and remains uncontrolled"; `DialogRoot.test.tsx` "keeps trigger ownership when another trigger mounts while open".
- **DLG-EVT-08** — MUST. `onOpenChangeComplete(true)` is called once per open, on animations finished of the Popup element (immediately when it has no finite animations), only if the dialog is still open at that time; `onOpenChangeComplete(false)` is called once per close from the unmount cleanup (after the exit animations or `unmount()`), after the store has reset `mounted`, the active trigger and the unmount-prevention flag (PSH-STA-45, TRN-EVT-01…04); neither is called on a closed mount. Server: relaxed, see §9.
  Source: `dialog/popup/DialogPopup.tsx:47-55`; `utils/popups/popupStoreUtils.ts:586-595`; `internals/useOpenChangeComplete.tsx:9-28`. Test: [L4] popupConformanceTests "prop: onOpenChangeComplete" (5 tests); `dialogRootSharedTests.tsx` "closes the popup when the `close` method is called".
- **DLG-EVT-09** — MUST. A dialog dismissed while its enter animation is still running never calls `onOpenChangeComplete(true)`; only `(false)` is called after the exit. If the Popup's enter animation is replaced by a different one while running, `(true)` waits for the replacement to finish (TRN-ANI-07).
  Source: `dialog/popup/DialogPopup.tsx:50-54`; `internals/useAnimationsFinished.ts` (TRN-ALG-03). Test: [L4] `DialogRoot.test.tsx` "does not get called on open when dismissed during the enter animation", "waits for a restarted enter animation to finish".
- **DLG-EVT-10** — MUST. The order for a successful request is: `onOpenChange` → internal notification → store commit → (post-commit, pre-paint) `mounted`/`transitionStatus`/floating state sync, focus management scheduling → (later) `onOpenChangeComplete` (EVD-EVT-20).
  Source: `dialog/store/DialogStore.ts:86-94`; `utils/popups/popupStoreUtils.ts:573-608`. Test: [L2] `DialogRoot.test.tsx` "reports nested drawer counts before passive effects" (pre-paint ordering of nested reports).
  Port note 2026-10-29 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); ADR 13 Amendments 2026-10-22): the drawer clause ("reports nested drawer counts before passive effects", the drawer count) is open to M6 (Drawer); the row does not hold `implemented` until it runs.

## 8. Form integration

Not applicable — Dialog has no form value, no `name`, and does not participate in Field/Form state; a form rendered inside the Popup behaves natively (the docs' controlled-dialog example closes it from the form's submit handler).

## 9. Lifecycle, mounting and animation

Vocabulary: *mounted* = store `mounted` (PSH-ANI-01); *post-commit, pre-paint*, *next frame*, *on animations finished* as in conventions §4. Dialog configures the shared machinery as: `useUnmountAfterClose({ open, ref: popupRef, animateInitialOpen: undefined })` → `useTransitionStatus(open, enableIdleState: false, deferEndingState: false)` (TRN §6.1/6.4); `useOpenChangeComplete({ open, ref: popupRef })` in the Popup for the `true` completion.

- **DLG-ANI-01** — MUST. The portal node, Viewport, focus guards and Popup exist iff `mounted || keepMounted` (TRN-ANI-01); the internal backdrop iff `mounted && modal === true`; the Backdrop always (hidden while unmounted) unless nested without `forceRender`.
  Source: `dialog/portal/DialogPortal.tsx:28-38`; `dialog/viewport/DialogViewport.tsx:42`; `dialog/backdrop/DialogBackdrop.tsx:40,48`. Test: [L2] `DialogPopup.test.tsx` "prop: keepMounted" (3 tests); `DialogViewport.test.tsx` (2 tests); `DialogBackdrop.test.tsx` "always renders by default when not nested".
- **DLG-ANI-02** — MUST. Transition statuses follow TRN-STA-01…14 with the idle state disabled and the ending state not deferred: an open request on an unmounted dialog mounts it with `transitionStatus: 'starting'` in the same commit (`data-starting-style` on Popup, Viewport, Backdrop), cleared on the next frame; a close request sets `'ending'` in the closing commit (`data-ending-style`); a dialog mounted already open (`defaultOpen`, controlled `open` at mount, server-rendered) never shows `'starting'` (PSH-ANI-02, TRN-STA-13); reopening during the exit clears `'ending'` on the next frame without a new `'starting'` (TRN-STA-09). Server: met on both modes, see §9.
  Source: `utils/popups/popupStoreUtils.ts:573-606`; `internals/useUnmountAfterClose.ts:59-64`; `utils/popups/store.ts:180-183`. Test: [L4] `DialogRoot.test.tsx` "does not get called on open when dismissed during the enter animation" (`[data-open][data-starting-style]` styling); popupConformanceTests "animations".
- **DLG-ANI-03** — MUST. After a close the popup subtree stays mounted with `data-closed`/`data-ending-style` until all finite CSS animations and transitions *on the Popup element* have finished (TRN-ANI-03…07, detection on that element only); Backdrop and Viewport animations are not awaited. Without animations the unmount happens in the same task (TRN-ANI-15 when `getAnimations` is unavailable, else after the next-frame inspection TRN-ANI-11). Server: relaxed, see §9.
  Source: `utils/popups/popupStoreUtils.ts:579-596`; `internals/useUnmountAfterClose.ts:118-127`. Test: [L4] `DialogRoot.test.tsx` "waits for the exit transition to finish before unmounting" (`onTransitionEnd` once); popupConformanceTests "removes the popup when there is no exit animation defined", "is called on close when there is no exit animation".
- **DLG-ANI-04** — MUST. With `keepMounted`, after the exit finishes the Popup, Viewport and portal node remain in the DOM with `hidden` (DLG-DOM-12/18), `data-closed`, no transition attribute, and are inaccessible (PSH-ANI-14); a Close part inside remains clickable in the DOM sense but requests nothing (DLG-PTR-19); reopening restores the enter transition from `'starting'` (PSH-STA-44/TRN-STA-03) and re-wires dismissal.
  Source: `dialog/portal/DialogPortal.tsx:28`; `dialog/popup/DialogPopup.tsx:81`. Test: [L4] `DialogPopup.test.tsx` "should keep the dialog mounted when keepMounted=true"; `DialogClose.test.tsx` "does not request another close when clicked after the dialog has closed"; popupFocusPropsTests with `alwaysMounted` (not used by Dialog).
- **DLG-ANI-05** — MUST. `actionsRef.current.unmount()` ends the closing phase immediately (ignoring running animations, TRN-ANI-22), is a no-op when already unmounted (TRN-ANI-19), and when called while open takes effect only if a close commits in the same batch (TRN-ANI-20).
  Source: `dialog/root/useRenderDialogRoot.tsx:83`; `internals/useUnmountAfterClose.ts:102-116`. Test: [L2] `dialogRootSharedTests.tsx` "unmounts the popup when the `unmount` method is called", "clears manual unmount state after the `unmount` method is called".
- **DLG-ANI-06** — MUST. The unmount cleanup runs in this order: `activeTriggerId`/`activeTriggerElement` → `null`, `mounted` → `false`, `preventUnmountingOnClose` → `false`, then `onOpenChangeComplete(false)` (PSH-STA-45, TRN-ANI-27); after it every trigger reports `aria-expanded="false"` and no `aria-controls`.
  Source: `utils/popups/popupStoreUtils.ts:586-595`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "when the popup opens and closes" (`expectClosed`); detachedTriggersConformanceTests "opens with any trigger on click and marks only that trigger as active".
- **DLG-ANI-07** — MUST. The focus manager is enabled exactly while `mounted` (so initial focus runs after the mount commit and return focus after the unmount commit, DLG-FOC-02/07); the Escape and outside-press listeners are attached exactly while the effective open state is `true` (removed at close-request time, so nothing dismisses during the exit transition); the Trigger's click interaction is always attached.
  Source: `dialog/popup/DialogPopup.tsx:101`; `floating-ui-react/hooks/useDismiss.ts:297-308,718-746`; `dialog/trigger/DialogTrigger.tsx:68`. Test: [L4] `DialogRoot.test.tsx` "waits for the exit transition to finish before unmounting" (indirect).
- **DLG-ANI-08** — MUST. Reopening while the exit transition is running cancels the pending unmount: `onOpenChangeComplete(false)` is not called, the same elements are reused, and `onOpenChangeComplete(true)` follows after the enter animations (TRN-ANI-18, TRN-EVT-05).
  Source: `internals/useUnmountAfterClose.ts:118-127`; `internals/useOpenChangeComplete.tsx:15-27`. Test: none in the Dialog suite (TRN-owned).
- **DLG-ANI-09** — MUST. The Root's interaction layer (dismissal listeners, scroll lock, nesting reports, interaction prop bags) is mounted iff `open || mounted || handle != null` (PSH-STA-43) and is re-created on every reopen, so Escape and outside-press dismissal work again after each close/reopen cycle for contained, detached and multiple detached triggers alike; a handle-backed Root keeps it mounted while closed so inactive detached triggers receive stable attributes and are not re-rendered on open/close (DLG-EDGE-16).
  Source: `dialog/root/useRenderDialogRoot.tsx:89-103`; `dialog/root/useDialogRoot.ts:125-135`. Test: [L4] `DialogRoot.test.tsx` "rewires dismiss interactions after closing and reopening" (×3 modes); `DialogRoot.detached-triggers.test.tsx` "when the popup opens and closes", "when the popup moves to another trigger".
- **DLG-ANI-10** — MUST. When a dialog is closed *and* unmounted its nested counts are reset to `0` (DLG-STA-06); a handle-backed dialog that closes while its nested dialog stays open therefore reopens with `--nested-dialogs: 0` and no `data-nested-dialog-open` until the nested dialog reports again.
  Source: `dialog/root/useDialogRoot.ts:101-106`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "resets nested state recorded while open when a handle-backed dialog closes".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- DLG-API-05 — TRN-ANI-03/09/10 row: unmount and `onOpenChangeComplete(false)` land in the first render batch after the completion message, about two round trips after the exit animation ends; JS sets `hidden` at animation end meanwhile (optimistic convergent write).
- DLG-EVT-08 — TRN-ANI-03/09/10 row: unmount and `onOpenChangeComplete(false)` land in the first render batch after the completion message, about two round trips after the exit animation ends; JS sets `hidden` at animation end meanwhile (optimistic convergent write).
- DLG-ANI-03 — TRN-ANI-03/09/10 row: unmount and `onOpenChangeComplete(false)` land in the first render batch after the completion message, about two round trips after the exit animation ends; JS sets `hidden` at animation end meanwhile (optimistic convergent write).
- DLG-KEY-08 — PSH-KEY-07 row: the focus-guard (`focus-out`) close commits a round trip later.
- DLG-KEY-02 — EVD-EVT-11 / EVD-DIS-01 / FLT-KEY-01 row: the consumer's `cancel()` / `allowPropagation()` arrives a round trip after dispatch; JS prevents and stops Escape whenever this dialog is the innermost open popup (ADR 04 §3 fallback); WebAssembly meets the spec through `decide()`.
- DLG-DIS-07 — EVD-EVT-11 / EVD-DIS-01 / FLT-KEY-01 row: the consumer's `cancel()` / `allowPropagation()` arrives a round trip after dispatch; JS prevents and stops Escape whenever this dialog is the innermost open popup (ADR 04 §3 fallback); WebAssembly meets the spec through `decide()`.
- DLG-STA-11 — RND-STA-01 row for mirrored fields: `transitionStatus` reaches the C# state (and so `ClassValue`/`StyleValue`) one round trip after JS writes `data-starting-style`/`data-ending-style`.
- DLG-ANI-02 — TRN-STA-04/05/07/09 row, met on both modes: the next-frame `data-starting-style`/`data-ending-style` flip is JS-owned with C# mirroring; listed so it is not moved back to C#.
- DLG-PTR-01, DLG-PTR-02 — "C#-origin consumer vetoes" row (the `press` decision point; added 2026-10-28, ADR 01 Amendments 2026-10-28): on WebAssembly C# runs the consumer's declined `onclick` and the open or close request inside the press; on Server the `interactions` module reports the press and C# runs the handler, then the request, a round trip later. The veto holds on both modes.
- DLG-EVT-06 — "JS-owned values that originate in C# state" row (the open-change relay; added 2026-10-28): the modules' `openchange` listeners run from the `update` that carries the committed change, pre-paint on WebAssembly and a round trip later on Server; the Escape, outside-press and focus-out decisions are JavaScript's and are unaffected.
- DLG-FOC-14 — FLT-FOC-11 row (added 2026-10-28): the focus-out close is decided by the `focus-manager` module in the microtask after the `focusout` and committed by C# a round trip later on Server.
- DLG-EDGE-07 — PSH-STA-23 / PSH-EDGE-06 row (added 2026-10-28): the overlap check runs at the next render-batch boundary (`OnAfterRender`), on Server after paint plus a round trip.
- DLG-DOM-05, DLG-FOC-01, DLG-KEY-06, DLG-ARIA-11, DLG-DOM-25, DLG-DOM-26 — the row of a popup element the opening render creates (added 2026-10-28, ADR 01 Amendments 2026-10-28): the portal move, the initial focus, the focus trap, the outside marking and the scroll lock run when the Popup's handles attach from `OnAfterRender`: before the first paint on WebAssembly, after paint plus a round trip on Server (measured: median focus 13 ms on localhost, 177 ms at a 50 ms round trip, 444 ms at 150 ms). A kept-mounted popup has no gap on either mode (its options travel in the commit path).

## 10. Positioning

Not applicable — Dialog is not anchored: no Positioner part, no placement props, no positioning CSS variables; the Popup is the floating element only for dismissal and focus-management purposes (PSH-STA-46 with `treatPopupAsFloatingElement`), and layout is entirely consumer CSS (the docs' Viewport demos use fixed positioning and scrolling).

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Popup store, trigger registry, handles | [popup-store-and-handles](../architecture/popup-store-and-handles.md) | `DialogStore` adds the fields of §4.2; `usePopupRootStore(…, treatPopupAsFloatingElement: true)`; `useImplicitActiveTrigger(store)` with default `closeOnActiveTriggerUnmount: false`; `useTriggerDataForwarding` forwards `{ payload }` only; `BasePopupHandle('Dialog', throwOnMissingTrigger: false)`; `DialogHandle.openWithPayload` (PSH-STA-39) |
| Open-change sequence and reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | Own `setOpen` (DLG-STA-15): immediate `preventUnmountOnClose` write (EVD-EDGE-09), close-trigger back-fill (EVD-EVT-16), no instant-phase flag; reasons DLG-EVT-02 |
| Transition status / unmount after close | [transitions-and-mounting](../architecture/transitions-and-mounting.md) | `useOpenStateTransitions(open, store)` → `useUnmountAfterClose` on `popupRef`, `animateInitialOpen` unset; Popup `useOpenChangeComplete({ open, ref: popupRef })` for `(true)` |
| Button mechanism | [button-behavior](../architecture/button-behavior.md) | Trigger and Close: `useButton({ disabled, native: nativeButton })`, no `focusableWhenDisabled` |
| Open interaction type | [popup-store-and-handles](../architecture/popup-store-and-handles.md) §PTR | Trigger: `useOpenMethodTriggerProps(() => store.select('open'), set 'openMethod')`; Root: `usePopupRootSync` resets |
| Scroll lock | [shared-utilities](../architecture/shared-utilities.md) §4.8 | `useScrollLock(open && modal === true, popupElement)` |
| `inert` attribute value | [shared-utilities](../architecture/shared-utilities.md) UTL-DOM-16 | Internal backdrop `inert={inertValue(!open)}` |
| Rendering, ids, state attributes | [rendering-and-composition](../architecture/rendering-and-composition.md) | `useRenderElement` on every element part; Backdrop/Viewport `enabled` gating (RND-DOM-25); mappings: Trigger `triggerOpenStateMapping` from `utils/popupStateMapping.ts` (`data-popup-open`; not the same-named `utils/collapsibleOpenStateMapping.ts` export, which emits `data-panel-open`), Backdrop `popupTransitionStateMapping` (`utils/popupStateMapping.ts`), Popup/Viewport `dialogStateAttributesMapping` (`dialog/utils/stateAttributesMapping.ts` = popup + transition + `nestedDialogOpen`) |
| Trigger click interaction (`useClick`) | [floating-and-positioning](../architecture/floating-and-positioning.md) §6.3 | Defaults only: `event: 'click'`, `toggle: true`, `stickIfOpen: true`, `touchOpenDelay: 0`, reason `trigger-press` |
| Dismissal (`useDismiss`) | [floating-and-positioning](../architecture/floating-and-positioning.md) §6.4 | `outsidePressEvent()` per DLG-PTR-05; `outsidePress(event)` per DLG-PTR-05 (`outsidePressEnabledRef` gate, evaluated first) and DLG-PTR-06/07/10/11/12; `escapeKey: isTopmost`; `bubbles` unset (Escape does not bubble, outside press does); no `referencePress`; prop bags published as `activeTriggerProps = inactiveTriggerProps = dismiss.reference`, `popupProps = dismiss.floating` |
| Focus manager (`FloatingFocusManager`) | [floating-and-positioning](../architecture/floating-and-positioning.md) §6.10 | `context: floatingRootContext`, `openInteractionType: openMethod`, `disabled: !mounted`, `closeOnFocusOut: !disablePointerDismissal`, `initialFocus: initialFocus ?? createDefaultInitialFocus(popupRef)`, `returnFocus: finalFocus`, `modal: modal !== false`, `restoreFocus: 'popup'`; `explicitReturnFocus`, `nextFocusableElement`, `previousFocusableElement`, `getInsideElements` unset |
| Portal (`FloatingPortal`) | [floating-and-positioning](../architecture/floating-and-positioning.md) §6.11 | `container` forwarded; consumer props on the portal node; `portalOwnerRole` unset |
| Composite key set | [composite-and-list-navigation](../architecture/composite-and-list-navigation.md) | Popup stops propagation of `COMPOSITE_KEYS` (DLG-KEY-09) |

## 12. Edge cases, invariants and dev warnings

- **DLG-EDGE-01** — MUST. A `Dialog.Trigger` rendered with neither an ancestor `Dialog.Root` nor a `handle` throws (in every environment) the error `Base UI: <Dialog.Trigger> must be used within <Dialog.Root> or provided with a handle.` (PSH-EDGE-01).
  Source: `dialog/trigger/DialogTrigger.tsx:41-45`. Test: [L2] `DialogTrigger.test.tsx` "throws a descriptive error without a root or handle".
- **DLG-EDGE-02** — MUST. Any of Portal, Backdrop, Viewport, Popup, Title, Description or Close rendered without an ancestor `Dialog.Root` (or AlertDialog/Drawer Root) throws `Base UI: DialogRootContext is missing. Dialog parts must be placed within <Dialog.Root>.`
  Source: `dialog/root/DialogRootContext.ts:12-16`. Test: [L2] `DialogPopup.test.tsx` "throws a descriptive error when rendered outside <Dialog.Root>".
- **DLG-EDGE-03** — MUST. A Viewport or Popup rendered inside a Root but without a `Dialog.Portal` ancestor throws `Base UI: <Dialog.Portal> is missing.`
  Source: `dialog/portal/DialogPortalContext.ts:6-12`. Test: [L2] `DialogPortal.test.tsx` "throws a descriptive error when a portaled part is rendered without <Dialog.Portal>".
- **DLG-EDGE-04** — MUST. `handle.openWithPayload()` called while no Root is attached is ignored (no state change, no payload stored) and, outside production, logs the warning `Base UI: DialogHandle.openWithPayload() was called while no root using this handle is mounted. The call and its payload were ignored; mount a root with this handle before opening it imperatively.` — exactly once per call, and never in production.
  Source: `dialog/store/DialogHandle.ts:42-57`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "ignores openWithPayload() after the root unmounts and keeps the remounted root without payload", "does not warn for a detached payload open in production"; `utils/popups/popupHandle.test.ts` "$component handle ignores openWithPayload() with a warning while detached".
- **DLG-EDGE-05** — MUST. `handle.open()` / `handle.close()` while no Root is attached are ignored with the PSH-EDGE-02 dev warnings, with the component name substituted as `DialogHandle` (`Base UI: DialogHandle.open() was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.` / `Base UI: DialogHandle.close() was called while no root using this handle is mounted. The call was ignored.`). A call made from a descendant of the Root in the Root's own mount commit is *not* detached (PSH-STR-04).
  Source: `dialog/store/DialogHandle.ts:20`; `utils/popups/popupHandle.ts:216-223,274-280`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "attaches and detaches a handle when the prop toggles on a live root" (two warnings), "opens from a descendant layout effect on initial mount", "closes from a descendant layout effect on an initially open root" (no warning).
  Port note 2026-10-28 (ADR 06 Amendments 2026-10-18, rule (a); M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design point 7 (a), settled by the orchestrator 2026-10-28): a descendant's layout effect is its first `OnAfterRender`; a call from the Root's own first batch is not detached, because the Root attaches its handle at its own first `OnAfterRender`, before any descendant's (PSH-STR-04). "attaches and detaches a handle when the prop toggles on a live root" asserts one warning: the first toggle comes before any root attached (PSH-EDGE-02's port note).
- **DLG-EDGE-06** — MUST. `handle.open(id)` with a non-empty id that matches no registered trigger (searching every attached Root's registry newest-first, then the handle's fallback registry) opens the dialog *unassociated* and logs, outside production, `Base UI: DialogHandle.open: No trigger found with id "<id>". The popup will open, but the trigger will not be associated with it.` (PSH-EDGE-04); it never throws.
  Source: `dialog/store/DialogHandle.ts:20` (`throwOnMissingTrigger: false`); `utils/popups/popupHandle.ts:233-257`. Test: [L2] detachedTriggersConformanceTests "opens unassociated with a warning when opened with an unregistered trigger id"; `DialogRoot.detached-triggers.test.tsx` "stops resolving a handle-A trigger after the root switches to handle B".
- **DLG-EDGE-07** — MUST. Attaching a second Root to a handle already attached defers a check by one frame and warns (dev only) with the PSH-EDGE-06 message only if more than one Root is still attached then; a clean handoff and a canceled overlap do not warn. `attach` and `dispose` of the part's JavaScript twin are idempotent; a repeated `attach` of the same element is a no-op, and re-attaching the same Root to the handle neither detaches it nor warns. Server: relaxed, see §9.
  Source: `utils/popups/popupHandle.ts:153-173`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "does not warn when one root replaces another during a route transition", "restores control to the previous root when a newer overlapping root detaches", "keeps the newer root attached after the older root unmounts"; twin contract (repeated `attach` is a no-op; replaces "keeps the root attached through Strict Mode effect replay").
  Port note 2026-10-28 (ADR 01's PSH-STA-23 / PSH-EDGE-06 row; design point 7 (a)): the four fake-timer titles run at [L2] over the render-batch-boundary check, `DialogDetachedTriggersTests` "port: does not warn when one root replaces another during a route transition, before the next render-batch boundary", "port: keeps the newer root attached after the older root unmounts before the next render-batch boundary", "port: restores control to the previous root when a newer overlapping root detaches before the next render-batch boundary"; Strict Mode's replay is "port: keeps the root attached when the root renders again".
- **DLG-EDGE-08** — MUST. Registering one trigger element under two different ids in the same registry throws the PSH-EDGE-07 error outside production (a Trigger whose `id` prop changes re-registers cleanly instead, PSH-STA-26).
  Source: `utils/popups/popupTriggerMap.ts:36-60`. Test: none in the Dialog suite.
- **DLG-EDGE-09** — MUST. A Trigger with `disabled` neither opens the dialog on click/keyboard nor receives focus via <kbd>Tab</kbd>, for native (`disabled` attribute) and non-native (`aria-disabled="true"`, `tabindex="-1"`) elements alike; a disabled Close never requests a close.
  Source: `dialog/trigger/DialogTrigger.tsx:63-66`; `dialog/close/DialogClose.tsx:32-35`. Test: [L2] `DialogTrigger.test.tsx` "disables the dialog", "custom element"; `DialogClose.test.tsx` "disables the button", "custom element" (L4 for the keyboard and <kbd>Tab</kbd> clause).
- **DLG-EDGE-10** — MUST. A lazily loaded child inside the Popup whose Suspense boundary is *outside* the Portal suspends and resolves without an update loop; the fallback renders, then the content.
  Source: `floating-ui-react/components/FloatingPortal.tsx:78-123` (container resolution does not re-run on suspend). Test: [L2] `DialogPortal.test.tsx` "should not throw \"Maximum update depth exceeded\" when Suspense boundary is outside Portal". Docs: CHANGELOG.md v1.1.0 "Fix `Maximum update depth exceeded` error with Suspense".
  Port note 2026-10-28 (ADR 06 Amendments 2026-10-18, rule (a); M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design point 7 (a)): a suspended child is a child that renders after an awaited `OnInitializedAsync` inside the Popup. Test: [L2] `DialogPartTests` "port: a child that renders after an awaited OnInitializedAsync inside the popup renders its fallback, then its content, without an update loop".
- **DLG-EDGE-11** — MUST. Changing the `handle` prop of a mounted Root (to a new handle, back to a previous one, or to/from `undefined`) re-attaches the *same* store without resetting open state, payload or trigger association (PSH-STR-01/03); the old handle reports `isOpen === false`, the new one the live state; detached triggers follow their own handle, so a trigger bound to the old handle is no longer resolvable by the Root's new handle.
  Source: `dialog/root/useRenderDialogRoot.tsx:45-63,96`; `utils/popups/popupStoreUtils.ts:103-115`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "keeps a handle-backed dialog open and controllable across handle swaps on a live root", "attaches a fresh root store when the handle prop changes to a previously dirtied handle", "attaches and detaches a handle when the prop toggles on a live root", "keeps ARIA controls in sync when a detached handle is recreated while open".
- **DLG-EDGE-12** — MUST. A Root unmounted while open (or while holding a payload) and remounted with the same handle starts closed with `payload === undefined`, `aria-expanded="false"` and no `aria-controls` on persistent detached triggers; imperative calls made while unmounted are not replayed; a later `handle.open(id)` re-associates a persistent trigger (PSH-STA-40). Remounting controlled after uncontrolled (or vice versa) does not log the controlled-mode warning.
  Source: `utils/popups/popupStoreUtils.ts:79`; `utils/popups/popupHandle.ts:213-224`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "attaches fresh root state when the root remounts after being unmounted while open", "resets stale payload when a handle-backed root remounts", "notifies persistent detached triggers after attaching fresh root state on remount", "resets stale controlled open state when a handle-backed root remounts uncontrolled", "does not warn when a handle-backed root remounts controlled after being uncontrolled", "associates an imperative open-by-id with a persistent detached trigger after the root remounts". Docs: CHANGELOG.md v1.7.0 "Prevent `<Dialog.Root>` from reopening after remounting with a reused handle".
  Port note 2026-10-28 (M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123)): "associates an imperative open-by-id with a persistent detached trigger after the root remounts" is skipped in `DialogDetachedTriggersTests` until PR 137 (track 6's design point 13.8) lands: `PopupTriggerBehavior` does not forward an unchanged payload again once the trigger has lost the popup, so the remounted root's store shows no payload. It runs once this branch takes `main` after PR 137.
- **DLG-EDGE-13** — MUST. Server rendering: a detached Trigger renders from the handle's inert fallback store (PSH-STA-34), i.e. `aria-expanded="false"` and no `data-popup-open`, even when the Root is server-rendered open with a matching `defaultTriggerId`; after hydration it reflects the live store. If the Root closes before a delayed (suspended) Trigger hydrates, the Trigger still hydrates with the closed snapshot.
  Source: `utils/popups/usePopupHandleStore.ts:17-36`; `utils/popups/popupHandle.ts:126-128`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "hydrates a detached trigger from the stable fallback store", "keeps the server snapshot stable when the root closes before a delayed trigger hydrates".
  Port note 2026-10-28 (ADR 06 Amendments 2026-10-18, rule (a); M4 track 8, [issue 123](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/123); design point 7 (a)): hydration is a detached trigger rendered from the handle's fallback store before its root attaches, as during prerender. Test: [L2] `DialogDetachedTriggersTests` "port: renders a detached trigger from the handle's fallback store until its root attaches", "port: renders a delayed detached trigger closed when the root closes before the trigger renders".
- **DLG-EDGE-14** — MUST. A replacement Root for the same handle that suspends (abandoned or pending transition render) never attaches its store: the current Root keeps control, its payload and `aria-expanded`. Port: withdrawn (chosen by the user 2026-10-28; ADR 02 Amendments): React suspends and abandons renders, Blazor has no suspended or abandoned render, so a replacement Root cannot attach its store from one; the current Root's control, payload and `aria-expanded` are covered by PSH-STA-34..40's attached-store stack.
  Source: `utils/popups/popupStoreUtils.ts:93-115` (attachment only on commit). Test: none (withdrawn; Base UI's `DialogRoot.detached-triggers.test.tsx` "does not attach a replacement root store from an abandoned transition render" has no Blazor counterpart).
- **DLG-EDGE-15** — MUST. A trigger that mounts while the dialog is open through another trigger does not take over ownership (PSH-STA-22): the first trigger keeps `aria-expanded="true"`/`aria-controls`, the new one gets `aria-expanded="false"` and no `aria-controls`.
  Source: `utils/popups/popupStoreUtils.ts:343-363,484-490`. Test: [L2] `DialogRoot.test.tsx` "keeps trigger ownership when another trigger mounts while open".
- **DLG-EDGE-16** — SHOULD. Opening, closing and moving the dialog between triggers of a handle-backed Root does not re-render the part of an inactive detached trigger (render count observed through bUnit (L2)); its rendered interaction attributes stay unchanged.
  Source: `dialog/root/useRenderDialogRoot.tsx:89-92`; `dialog/trigger/DialogTrigger.tsx:81`. Test: [L2] `DialogRoot.detached-triggers.test.tsx` "when the popup opens and closes", "when the popup moves to another trigger". Docs: CHANGELOG.md v1.9.0 "Avoid re-rendering inactive triggers on open and close".
  Port note 2026-10-28 (ADR 16 Amendments 2026-10-27, the [issue 110](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/110) rule; design point 7 (a)): the render counts are `ClassValue` counts. Test: [L2] `DialogDetachedTriggersTests` "when the popup opens and closes", "when the popup moves to another trigger"; `DialogAllocationBudgetTests` "port: opening, moving and closing a handle-backed dialog renders neither the root nor an inactive detached trigger" (open, move, close: the two active triggers render 2 and 2 times, the inactive trigger and the root 0).
- **DLG-EDGE-17** — MUST. Nothing in Dialog depends on reading direction except the RTL branch of the scrollbar check (DLG-PTR-14); there is no locale-dependent behavior.
  Source: `floating-ui-react/hooks/useDismiss.ts:464-475`. Test: none.
- **DLG-EDGE-18** — MUST. Element substitution that changes the `id` landing on Title, Description or Popup affects the IDREFs as follows. Title and Description register with the Root the id resolved from their own `id` parameter (or the generated id), not the id actually rendered; so a substitute callback that does not apply the `id` it is handed, or a substitute element that carries its own different `id` (which wins on the element, RND-DOM-32), leaves the Popup's `aria-labelledby` / `aria-describedby` referencing an id no element carries. Consumers are expected to apply the passed `id` (RND-DOM-32, RND §10 Q8). The Trigger's `aria-controls`, by contrast, is derived from the Popup element's rendered `id` (PSH-STA-04, PSH-ARIA-03): a Popup substitute element with its own `id` is followed, and a Popup rendered with no `id` leaves `aria-controls` absent rather than dangling.
  Source: `dialog/title/DialogTitle.tsx:22-29`; `dialog/description/DialogDescription.tsx:22-29`; `dialog/popup/DialogPopup.tsx:76-78,93`; `internals/useRenderElement.tsx:168,171,195`; `utils/popups/store.ts:152-155,212-213`; see RND-DOM-32. Test: none.

## 13. Behaviors evidenced by tests

Coverage of each test by the requirements above. Shared suites are listed with the options Dialog passes.

### `root/DialogRoot.test.tsx`

| Test | Requirements |
|---|---|
| popupConformanceTests (`triggerMouseAction: 'click'`, `expectedPopupRole: 'dialog'`): "opens the popup with the `open` prop" | DLG-API-01, DLG-STA-14 |
| popupConformanceTests "opens initially and remains uncontrolled" / "is ignored when `open={false}` is passed" / "is ignored when `open={true}` is passed, so Escape cannot close" | DLG-API-02, DLG-API-01, DLG-KEY-11, DLG-DIS-08 |
| popupConformanceTests "is called with the new open state and the change details" / "keeps the popup closed when the open change is canceled" | DLG-EVT-01, DLG-PTR-01, DLG-EVT-04 |
| popupConformanceTests "has the dialog role on the popup" / "has the `aria-controls` attribute on the trigger" / "opens on trigger click and sets `aria-expanded` on the trigger" / "has the `aria-haspopup` attribute on the trigger" / "allows a custom `id` prop" | DLG-ARIA-04, DLG-ARIA-03, DLG-ARIA-02, DLG-DOM-03, DLG-ARIA-01, DLG-DOM-16, DLG-STA-10 |
| popupConformanceTests "prop: onOpenChangeComplete" ("is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation", "is called on open once the enter animation finishes", "is called on close once the exit animation finishes") | DLG-API-05, DLG-EVT-08, DLG-ANI-03 |
| popupConformanceTests "animations" ("removes the popup when there is no exit animation defined", exit-animation case) | DLG-ANI-02, DLG-ANI-03 |
| "ignores a native click whose pointerdown opened the dialog" | DLG-PTR-09 |
| dialogRootSharedTests (`parts: Dialog`, `popupRole: 'dialog'`) — see below | — |
| "reports nested drawer counts before passive effects" | DLG-STA-01, DLG-STA-05, DLG-EVT-10 |
| "keeps trigger ownership when another trigger mounts while open" | DLG-EDGE-15, DLG-STA-12, DLG-ARIA-02, DLG-ARIA-03, DLG-API-14, DLG-EVT-07 |
| "rewires dismiss interactions after closing and reopening" (×3: contained / detached / multiple detached) | DLG-ANI-09, DLG-KEY-01, DLG-PTR-11 |
| "ARIA attributes" (×3) | DLG-ARIA-05, DLG-ARIA-06, DLG-DOM-23, DLG-ARIA-07 |
| "keeps accessible names and descriptions in sync when label parts change" (×3) | DLG-STA-09, DLG-ARIA-05, DLG-ARIA-06 |
| "reports no trigger when closing with an initial trigger id that is not mounted" (×3) | DLG-EVT-03, DLG-DIS-09, DLG-API-11 |
| "calls onOpenChange with the reason for change when user clicks backdrop while the modal dialog is open" (×3) | DLG-PTR-10, DLG-EVT-02, DLG-ARIA-10 |
| "calls onOpenChange with the reason for change when user clicks outside while the non-modal dialog is open" (×3) | DLG-PTR-11, DLG-EVT-02 |
| "detects clicks on user backdrop" (×3) | DLG-PTR-10 |
| "does not change open state on non-main button clicks" (×3) | DLG-PTR-06 |
| "emits a single internal openchange when closing on Escape" (×3) | DLG-KEY-01, DLG-EVT-06, DLG-STA-15 |
| "does not emit internal openchange when an Escape close is canceled" (×3) | DLG-KEY-02, DLG-DIS-07, DLG-EVT-04 |
| "$outcome the dialog when clicking outside if disablePointerDismissal=$disablePointerDismissal" (×3 rows × 3) | DLG-API-06, DLG-DIS-01 |
| "uses intentional outside press with user backdrop (mouse): closes on click, not on mousedown" (×3) | DLG-PTR-05 |
| "uses intentional outside press with internal backdrop (modal=true): closes on click, not on mousedown" (×3) | DLG-PTR-05 |
| "closing via intentional outside press with user backdrop (modal=true): works when portaled into a shadow DOM" (×3) | DLG-DIS-06, DLG-API-18, DLG-STR-04 |
| "closing via outside press: works when clicking another element inside the same shadow root" / "closing via outside press: works when clicking outside the shadow root" / "closing via outside press: works for a modal dialog when clicking outside the shadow root" (×3 each) | DLG-DIS-06, DLG-PTR-09, DLG-PTR-11 |
| "waits for the exit transition to finish before unmounting" (×3) | DLG-ANI-03, DLG-DOM-20, DLG-ANI-07 |
| "makes other interactive elements on the page inert when a modal dialog is open" / "does not make other interactive elements on the page inert when a non-modal dialog is open" (×3 each) | DLG-STR-10, DLG-DOM-06 |
| "should render an internal backdrop when `true`" / "should not render an internal backdrop when `false`" (×3 each) | DLG-STR-10, DLG-STR-11, DLG-PTR-18 |
| "does not dismiss previous modal dialog when clicking new modal dialog" (×3) | DLG-DIS-04, DLG-PTR-12 |
| "dismisses non-nested dialogs one by one" (×3) | DLG-DIS-04, DLG-PTR-10 |
| "should not dismiss the dialog when dismissing outside a nested modal menu" / "should not dismiss the dialog when dismissing outside a nested select popup" (×3 each) | DLG-DIS-05, DLG-PTR-15 |
| "should not close the parent menu when Escape is pressed in a nested dialog" (×3) | DLG-KEY-05, DLG-KEY-02 |
| "moves focus to the popup when a focused child is removed on pointerdown and outside press still dismisses" (×3) | DLG-FOC-15, DLG-DOM-17, DLG-FOC-02 |
| "dismisses on first outside click after NumberField scrub interaction (pointer lock path)" (×3) | DLG-PTR-17 |
| "waits for a restarted enter animation to finish" (×3) | DLG-EVT-09 |
| "does not get called on open when dismissed during the enter animation" (×3) | DLG-EVT-09, DLG-ANI-02 |
| "does not close on a right-button outside press" | DLG-PTR-06, DLG-PTR-05 |
| "keeps the parent open on close (outer: $outer, nested: $nested)" (3 kinds × 5 setups) | DLG-DIS-06, DLG-DIS-03, DLG-STR-04 |
| "still dismisses on a press outside the popup" | DLG-DIS-06, DLG-PTR-11 |
| "closes a non-modal dialog without a backdrop" / "closes a trap-focus dialog without a backdrop" | DLG-PTR-08, DLG-PTR-05 |
| "does not close when another touch is still active on touchend" / "does not close when two touches are lifted simultaneously on touchend" / "does not close when multiple touches move past the drag dismissal threshold" | DLG-PTR-07 |
| "closes as soon as a single touch moves past the drag dismissal threshold" | DLG-PTR-08 |
| "keeps the page locked until it takes over, and unlocks on close" (×3 lockers) | DLG-DOM-26, DLG-DOM-27 |
| "locks immediately when an external <body> lock cannot affect the page" | DLG-DOM-26 |
| "returns focus to the menu trigger when a detached dialog trigger unmounts" | DLG-FOC-06, DLG-FOC-16 |
| "keeps focus trapped when dialog content contains a non-scrollable scroll area" | DLG-KEY-06, DLG-FOC-12 |
| "returns focus into the dialog when a close confirmation opened by an outside press closes" | DLG-FOC-16, DLG-FOC-07, DLG-EVT-04, DLG-KEY-10 |
| "does not leak a non-modal popup field into an unrelated dialog return focus" | DLG-FOC-16 |

### `test/dialogRootSharedTests.tsx` (shared with AlertDialog; run with `parts: Dialog`, `popupRole: 'dialog'`)

| Test | Requirements |
|---|---|
| "reports the trigger and reason when opened by a trigger and closed by a close button" | DLG-API-04, DLG-EVT-01, DLG-EVT-02, DLG-EVT-03, DLG-PTR-01, DLG-PTR-19, DLG-STA-15 |
| "unmounts the popup when the `unmount` method is called" | DLG-API-07, DLG-EVT-05, DLG-ANI-05, DLG-DOM-20, DLG-PTR-02 |
| "clears manual unmount state after the `unmount` method is called" | DLG-EVT-05, DLG-ANI-05 |
| "closes the popup when the `close` method is called" | DLG-API-07, DLG-DIS-09, DLG-EVT-08, DLG-API-02 |
| "opens with a payload set programmatically" | DLG-API-25, DLG-API-26, DLG-API-09, DLG-EVT-03, DLG-ARIA-02 |

### `root/DialogRoot.detached-triggers.test.tsx`

| Test | Requirements |
|---|---|
| detachedTriggersConformanceTests (`openInteractions: ['click']`, `ariaExpanded: true`, `throwOnMissingTrigger: false`, with `Close`): "opens with any trigger on click and marks only that trigger as active" (contained + detached) | DLG-PTR-01, DLG-PTR-02, DLG-PTR-13, DLG-ARIA-02, DLG-ANI-06 |
| detachedTriggersConformanceTests "sets the payload of the active trigger and renders content based on it" / "reuses the popup DOM nodes when switching triggers" / "marks the trigger matching a controlled `triggerId` as active" / "opens initially with the payload of a non-first `defaultTriggerId`" / "allows controlling the open state and active trigger programmatically" | DLG-API-13, DLG-PTR-03, DLG-API-10, DLG-API-11, DLG-DOM-01 |
| detachedTriggersConformanceTests "opens by trigger id with its payload and closes" / "opens unassociated with a warning when opened with an unregistered trigger id" | DLG-API-24, DLG-API-26, DLG-EDGE-06 |
| detachedTriggersConformanceTests "ignores imperative calls while no root is attached and follows the root across remounts" / "registers a detached trigger declared after the root" / "resolves a trigger still registered to the previous root during a transient overlap" / "keeps a detached trigger working $name" (×4) | DLG-API-24, DLG-API-12, DLG-EDGE-05, DLG-EDGE-12 |
| "when the popup opens and closes" / "when the popup moves to another trigger" | DLG-EDGE-16, DLG-ANI-09, DLG-ANI-06, DLG-ARIA-02 |
| "closes a nonmodal dialog on Escape from an inactive trigger inside a popover on every open" | DLG-KEY-01, DLG-ANI-09 |
| "opens a handle-backed dialog as topmost over a dialog that opened while it was closed" | DLG-STA-06, DLG-KEY-04, DLG-DIS-03, DLG-DOM-19, DLG-DOM-22 |
| "resets nested state recorded while open when a handle-backed dialog closes" | DLG-STA-06, DLG-ANI-10, DLG-DOM-22 |
| "updates the parent nested state each time a handle-backed nested dialog opens and closes" | DLG-STA-05, DLG-DOM-19, DLG-DOM-22 |
| "keeps the parent nested state when a closed handle-backed nested dialog mounts" | DLG-STA-06, DLG-STA-07, DLG-PTR-12, DLG-DIS-03 |
| "hydrates a detached trigger from the stable fallback store" | DLG-EDGE-13, DLG-ARIA-02, DLG-DOM-03 |
| "keeps the server snapshot stable when the root closes before a delayed trigger hydrates" | DLG-EDGE-13, DLG-API-27 |
| "opens from a descendant layout effect on initial mount" / "opens with a payload from a descendant layout effect on initial mount" / "closes from a descendant layout effect on an initially open root" | DLG-EDGE-05, DLG-API-25, DLG-API-26 |
| "does not warn for a detached payload open in production" | DLG-EDGE-04 |
| "ignores openWithPayload() after the root unmounts and keeps the remounted root without payload" | DLG-EDGE-04, DLG-API-25, DLG-EDGE-12 |
| "does not attach a replacement root store from an abandoned transition render" | DLG-EDGE-14 (Port: withdrawn, chosen by the user 2026-10-28; ADR 02 Amendments) |
| "attaches a fresh root store when the handle prop changes to a previously dirtied handle" | DLG-EDGE-11, DLG-EDGE-12 |
| "keeps a handle-backed dialog open and controllable across handle swaps on a live root" | DLG-EDGE-11, DLG-API-27, DLG-DIS-02 |
| "attaches and detaches a handle when the prop toggles on a live root" | DLG-EDGE-11, DLG-EDGE-05, DLG-API-08 |
| "associates the requested trigger when opened by id in the same commit a root attaches" | DLG-API-24 |
| "does not warn when one root replaces another during a route transition" / "keeps the newer root attached after the older root unmounts" / "restores control to the previous root when a newer overlapping root detaches" / "keeps the root attached through Strict Mode effect replay" | DLG-EDGE-07 |
| "keeps the payload reactive" (multiple triggers within Root; multiple detached triggers) | DLG-API-13, DLG-API-09 |
| "attaches fresh root state when the root remounts after being unmounted while open" / "resets stale payload when a handle-backed root remounts" / "notifies persistent detached triggers after attaching fresh root state on remount" / "resets stale controlled open state when a handle-backed root remounts uncontrolled" / "does not warn when a handle-backed root remounts controlled after being uncontrolled" | DLG-EDGE-12, DLG-ARIA-03 |
| "keeps ARIA controls in sync when a detached handle is recreated while open" | DLG-EDGE-11, DLG-API-12, DLG-ARIA-03 |
| "closes a non-modal dialog with Escape from an inactive detached trigger" | DLG-KEY-01 |
| "associates an imperative open-by-id with a persistent detached trigger after the root remounts" | DLG-EDGE-12, DLG-API-24 |
| "stops resolving a handle-A trigger after the root switches to handle B" | DLG-EDGE-11, DLG-EDGE-06 |
| "does not associate the only rendered trigger when opened with a payload" | DLG-API-25, DLG-STA-12, DLG-DOM-03, DLG-ARIA-03 |
| "does not let a trigger that mounts while open replace the programmatic payload" | DLG-API-25, DLG-ARIA-11 |
| "keeps the programmatic payload when a controlled dialog declines a close request" | DLG-DIS-08, DLG-STA-14 |

Port note 2026-10-28 (fidelity correction, conventions §3.5; M4 track 8): the table cited detachedTriggersConformanceTests "does not leave an inline scale style on the popup after switching triggers", which the suite generates only when it is given a `Viewport` (`test/detachedTriggersConformanceTests.tsx:407`); Dialog's run passes none, so the title is not run for Dialog and is removed from the row.

### `popup/DialogPopup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Root open modal={false}` + Portal) | DLG-STR-07; RND-DOM-36…48 |
| "throws a descriptive error when rendered outside <Dialog.Root>" | DLG-EDGE-02 |
| "should keep the dialog mounted when keepMounted=true" / "should not keep the dialog mounted when keepMounted=$name" (×2) | DLG-API-17, DLG-DOM-18, DLG-ANI-04, DLG-ANI-01 |
| popupFocusPropsTests ("modal" and "non-modal"): "focuses the first focusable element by default" | DLG-FOC-01 |
| popupFocusPropsTests "focuses the element of a ref" (initialFocus) / "focuses the right element when a function returns $name" (×3) / "does not move focus when `false`" / "passes the interaction type to a function, and does nothing when it returns undefined" / "is not called again when the popup closes" | DLG-API-20, DLG-FOC-03, DLG-FOC-05, DLG-STA-08, DLG-KEY-10, DLG-FOC-02 |
| popupFocusPropsTests "focuses the trigger by default" / "focuses the element of a ref" (finalFocus) / "focuses the right element when a function returns $name" (×3) / "does not move focus when `false`" / "passes the close type to a function" | DLG-API-21, DLG-FOC-06, DLG-FOC-07, DLG-FOC-09, DLG-FOC-10, DLG-KEY-01 |
| "focuses the popup itself rather than inner content when opened by touch" | DLG-FOC-01, DLG-PTR-04, DLG-DOM-17 |
| "keeps initial focus working when the popup is wrapped by a display: contents ancestor" | DLG-FOC-01 |
| "keeps trap-focus tab cycling inside the popup when wrapped by a display: contents ancestor" | DLG-KEY-06, DLG-API-03 |
| "respects finalFocus when initialFocus points outside the popup" | DLG-API-21, DLG-FOC-10 |
| "moves final focus to trigger if initialFocus points outside the popup and finalFocus is not specified" | DLG-FOC-06 |
| "provides the number of open nested dialogs as a CSS variable" | DLG-DOM-19, DLG-STA-04 |
| "decrements the count when an open nested dialog is unmounted" | DLG-STA-05, DLG-DOM-19 |
| "does not change the count when a closed nested dialog is unmounted" | DLG-STA-05 |
| "increments for nested alert dialog and decrements on close (cross-type)" | DLG-STA-04 |
| "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent dialog" / "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent alert dialog" | DLG-STR-12, DLG-STA-03, DLG-STA-13, DLG-DOM-21, DLG-DOM-22 |

### `backdrop/DialogBackdrop.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, directly under `Root open modal={false}`) | DLG-STR-05; RND-DOM-36…48 |
| "has role=\"presentation\"" | DLG-ARIA-08 |
| "renders only the root backdrop by default" / "always renders by default when not nested" / "renders only the root backdrop with multiple nesting levels" | DLG-STR-05, DLG-ANI-01 |
| "always renders when true" | DLG-API-19 |

### `close/DialogClose.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`) | DLG-STR-09, DLG-API-22; RND-DOM-36…49 |
| "disables the button" / "custom element" | DLG-API-23, DLG-DOM-24, DLG-EDGE-09 |
| "closes the dialog when undefined is passed to the `onClick` prop" | DLG-PTR-19 |
| "does not close the dialog when the Base UI click handler is prevented" | DLG-PTR-19 |
| "does not request another close when clicked after the dialog has closed" | DLG-PTR-19, DLG-ANI-04 |

### `trigger/DialogTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLButtonElement`, `testRenderPropWith: 'button'`, `button: true`, under `Root open modal={false}`) | DLG-STR-02, DLG-API-15; RND-DOM-36…49 |
| "throws a descriptive error without a root or handle" | DLG-EDGE-01 |
| "disables the dialog" / "custom element" | DLG-API-16, DLG-DOM-04, DLG-EDGE-09 |

### `portal/DialogPortal.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, under `Root open`) | DLG-STR-03, DLG-DOM-05; RND-DOM-36…48 |
| "throws a descriptive error when a portaled part is rendered without <Dialog.Portal>" | DLG-EDGE-03 |
| "should not throw \"Maximum update depth exceeded\" when Suspense boundary is outside Portal" | DLG-EDGE-10 |

### `viewport/DialogViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside Portal next to a Popup) | DLG-STR-06; RND-DOM-36…48 |
| "renders only when the dialog is mounted by default" | DLG-DOM-11, DLG-STR-06 |
| "stays mounted when used within a keepMounted portal" | DLG-DOM-11, DLG-API-17 |

### `title/DialogTitle.test.tsx` / `description/DialogDescription.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLHeadingElement` / `HTMLParagraphElement`, inside Popup) | DLG-STR-08; RND-DOM-36…48 |

### `root/DialogRoot.spec.tsx`

Type-level only: `Dialog.createHandle<number>()` types `Dialog.Trigger`'s `payload` (`42` accepted, `'invalid'` rejected) and the function child's `payload` as `number | undefined`. Covered by §3.11.

No test in the suite required a requirement beyond those already stated in §3–§12; every row above maps to existing IDs.

## 14. React-specific notes and porting considerations

Non-normative.

- **Store and effects.** The Root owns a `ReactStore` (UTL §4.2) created once; props are synced into it post-commit (`useSyncedValues`, `useControlledProp`). Interaction behavior (`DialogInteractions`) is a child *component* rendered conditionally (`open || mounted || handle`), and its mounting order matters: `PopupHandleAttachment` is rendered first so its layout effect (handle attachment) runs before descendants' layout effects (PSH-STR-04). A port needs an equivalent "attach before children initialize" ordering guarantee. Port: see decisions/05-state-store-and-event-details.md §1 (store, prop sync in `OnParametersSet`, `PopupHandle<TPayload>` attached-store stack).
- **Nested reporting order.** Nested counts are propagated with layout effects (`useIsoLayoutEffect`), so a parent sees a child's open state post-commit, pre-paint; the test "reports nested drawer counts before passive effects" pins this. A nested Root inside a parent's Popup is a React descendant even though its DOM lives in its own portal node. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 and §Relaxation table (UTL-STA-19..22 row: post-commit C# state work lands after paint on Server); decisions/04-js-interop-boundary.md §5 (portal DOM move keeps the logical tree).
- **Portals and events.** `FloatingPortal` uses `ReactDOM.createPortal`; React synthetic events bubble through the *component* tree, so the Popup's `onKeyDown` (Escape, composite keys) sees keydowns from portaled content and `stopPropagation` on the synthetic event also stops the native event before it reaches the document listener — this is why one Escape press yields exactly one request even though both the popup handler and a document listener exist. A port must deduplicate explicitly. Port: see decisions/04-js-interop-boundary.md §5 (portals by DOM move), §1 (`interactions` module owns Escape/outside-press dismissal) and §3 (Escape `decide()` fallback).
- **`fastComponent` / `fastComponentRef`.** Root and Trigger are wrapped in a memoizing component factory (`@base-ui/utils/fastHooks`) to avoid re-rendering inactive triggers (DLG-EDGE-16); no observable behavior depends on it. Port: see decisions/05-state-store-and-event-details.md §1 (one selector subscription per part, `StateHasChanged` only when a selection changed).
- **`useSyncExternalStore` for handles.** Detached triggers read the handle's current store through `useSyncExternalStore` with the fallback store as the server snapshot (DLG-EDGE-13); the hydration tests exercise React-specific server/client mismatch rules. Port: see decisions/05-state-store-and-event-details.md §1 (detached triggers subscribe to the handle's pointer; `NullStore` fallback).
- **Suspense / transitions.** Handle attachment happens in a layout effect, never during render, so suspended or abandoned renders never attach (DLG-EDGE-14). Strict Mode double-invokes effects; the handle stack tolerates attach/detach/attach of the same store (PSH-STA-35). Port: see decisions/05-state-store-and-event-details.md §1 (attached-store stack, PSH-STA-34..40).
- **`inert` value.** `inertValue()` returns a string on React < 19 and a boolean on ≥ 19 to produce the same DOM attribute (UTL-DOM-16). Port: see decisions/04-js-interop-boundary.md §1 (`focus-manager` module owns outside marking, `aria-hidden`/inert, including the internal backdrop's marker).
- **Refs vs. elements.** `initialFocus`/`finalFocus` accept React `RefObject`s; a port should accept a late-bound element reference with the same "empty → default" fallback (PSH-FOC-02/04). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24: DOM handles become `Element`).
- **Timing primitives.** `enqueueFocus` uses a single module-level `requestAnimationFrame` slot (a new request cancels the previous), the dismiss hook uses 0 ms `Timeout`s and a 1000 ms touch window, and the focus manager uses `queueMicrotask` for focus-in/out decisions and for return focus; these map onto the canonical vocabulary of conventions §4. Port: see decisions/01-hosting-model-and-timing.md §Decision items 3–4 (frames and microtasks only in JavaScript); decisions/04-js-interop-boundary.md §1 (`focus-manager` and `interactions` modules).
- **Focus trap, scroll lock and exit animation.** Initial focus, the modal trap, focus-out close, `restoreFocus` and return focus (DLG-FOC-01…18, DLG-KEY-06…08) run in the shared focus manager; scroll lock (DLG-DOM-26/27) is the shared document lock; the exit wait and unmount (DLG-ANI-03, DLG-EVT-08) rest on the animations-finished watcher. Port: see decisions/04-js-interop-boundary.md §1 (`focus-manager`, `scroll-lock`, `transitions` modules) and §2 (optimistic convergent write of `hidden` at exit-animation end); decisions/01-hosting-model-and-timing.md §Relaxation table (TRN-ANI-03/09/10 row).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| DLG-STR-01 | MUST | Root renders no element; owns the store; function children | 2 |
| DLG-STR-02 | MUST | Trigger `<button>`; needs Root or handle | 2 |
| DLG-STR-03 | MUST | Portal renders the portal node; Viewport/Popup need it | 2 |
| DLG-STR-04 | MUST | Container resolution: prop → parent portal node → body; `null` waits | 2 |
| DLG-STR-05 | MUST | Backdrop `<div>`; nothing when nested unless `forceRender`; otherwise always rendered | 2 |
| DLG-STR-06 | MUST | Viewport `<div>` inside Portal wrapping Popup | 2 |
| DLG-STR-07 | MUST | Popup `<div>`; one per Root | 2 |
| DLG-STR-08 | MUST | Title `<h2>`, Description `<p>`; last mounted wins | 2 |
| DLG-STR-09 | MUST | Close `<button>`, 0..n | 2 |
| DLG-STR-10 | MUST | Internal backdrop first in portal node iff mounted && modal === true | 2 |
| DLG-STR-11 | MUST | Popup wrapped by two focus guards while mounted | 2 |
| DLG-STR-12 | MUST | Nested = ancestor dialog-family Root in component tree | 2 |
| DLG-STR-13 | MUST | Namespace and attribute-name exports | 2 |
| DLG-API-01 | MUST | `open` controlled | 3.1 |
| DLG-API-02 | MUST | `defaultOpen` default `false`; no enter transition | 3.1 |
| DLG-API-03 | MUST | `modal` values and what each regime enables | 3.1 |
| DLG-API-04 | MUST | `onOpenChange` signature/semantics | 3.1 |
| DLG-API-05 | MUST | `onOpenChangeComplete` | 3.1 |
| DLG-API-06 | MUST | `disablePointerDismissal` default `false`; scope (incl. `closeOnFocusOut: false` effects) | 3.1 |
| DLG-API-07 | MUST | `actionsRef` `{ unmount, close }` | 3.1 |
| DLG-API-08 | MUST | `handle` binds store; keeps interactions mounted | 3.1 |
| DLG-API-09 | MUST | Function children receive `{ payload }` | 3.1 |
| DLG-API-10 | MUST | `triggerId` controlled active trigger | 3.1 |
| DLG-API-11 | MUST | `defaultTriggerId` default `null` | 3.1 |
| DLG-API-12 | MUST | Trigger `handle` | 3.2 |
| DLG-API-13 | MUST | Trigger `payload` forwarded | 3.2 |
| DLG-API-14 | MUST | Trigger `id` = trigger id | 3.2 |
| DLG-API-15 | MUST | Trigger `nativeButton` default `true` | 3.2 |
| DLG-API-16 | MUST | Trigger `disabled` default `false` | 3.2 |
| DLG-API-17 | MUST | Portal `keepMounted` default `false` | 3.3 |
| DLG-API-18 | MUST | Portal `container` | 3.3 |
| DLG-API-19 | MUST | Backdrop `forceRender` default `false` | 3.4 |
| DLG-API-20 | MUST | Popup `initialFocus` default resolver and value contract | 3.6 |
| DLG-API-21 | MUST | Popup `finalFocus` default `true` and value contract | 3.6 |
| DLG-API-22 | MUST | Close `nativeButton` default `true` | 3.9 |
| DLG-API-23 | MUST | Close `disabled` default `false` | 3.9 |
| DLG-API-24 | MUST | `handle.open(id \| null)` semantics | 3.10 |
| DLG-API-25 | MUST | `handle.openWithPayload(p)` trigger-less open | 3.10 |
| DLG-API-26 | MUST | `handle.close()` | 3.10 |
| DLG-API-27 | MUST | `handle.isOpen` | 3.10 |
| DLG-STA-01 | MUST | Dialog store initial values | 4.2 |
| DLG-STA-02 | MUST | `modal`/`disablePointerDismissal`/`nested`/`role` synced from props | 4.2 |
| DLG-STA-03 | MUST | `nested` definition; distinct from floating-nested | 4.2 |
| DLG-STA-04 | MUST | `nestedOpenDialogCount` = depth of open chain below | 4.2 |
| DLG-STA-05 | MUST | Nested report values and timing | 4.2 |
| DLG-STA-06 | MUST | Reports ignored while closed & unmounted; reset | 4.2 |
| DLG-STA-07 | MUST | Topmost = count 0 | 4.2 |
| DLG-STA-08 | MUST | `openMethod` recording/reset | 4.2 |
| DLG-STA-09 | MUST | Title/Description ids in the store | 4.2 |
| DLG-STA-10 | MUST | Popup/Viewport element registration | 4.2 |
| DLG-STA-11 | MUST | Exact state object fields per part | 4.2 |
| DLG-STA-12 | MUST | `Trigger.State.open` = opened by this trigger | 3.2 |
| DLG-STA-13 | MUST | `nestedDialogOpen` = count > 0 | 4.2 |
| DLG-STA-14 | MUST | Effective open / active trigger id; declined close | 4.1 |
| DLG-STA-15 | MUST | Dialog open-change procedure | 4.1 |
| DLG-DOM-01 | MUST | Trigger `id` | 5.1 |
| DLG-DOM-02 | MUST | Trigger `data-base-ui-click-trigger` | 5.1 |
| DLG-DOM-03 | MUST | Trigger `data-popup-open` | 5.1 |
| DLG-DOM-04 | MUST | Trigger `data-disabled` | 5.1 |
| DLG-DOM-05 | MUST | Portal node attributes; exists iff mounted \|\| keepMounted | 5.2 |
| DLG-DOM-06 | MUST | Internal backdrop presence; `inert` while closing | 5.2 |
| DLG-DOM-07 | MUST | Backdrop `user-select: none` | 5.3 |
| DLG-DOM-08 | MUST | Backdrop `hidden` iff !mounted | 5.3 |
| DLG-DOM-09 | MUST | Backdrop `data-open`/`data-closed` | 5.3 |
| DLG-DOM-10 | MUST | Backdrop transition attributes | 5.3 |
| DLG-DOM-11 | MUST | Viewport exists iff keepMounted \|\| mounted | 5.4 |
| DLG-DOM-12 | MUST | Viewport `hidden` iff !mounted | 5.4 |
| DLG-DOM-13 | MUST | Viewport `pointer-events: none` iff !open | 5.4 |
| DLG-DOM-14 | MUST | Viewport open/closed/transition attributes | 5.4 |
| DLG-DOM-15 | MUST | Viewport `data-nested`/`data-nested-dialog-open` | 5.4 |
| DLG-DOM-16 | MUST | Popup `id` (consumer wins; used by `aria-controls`) | 5.5 |
| DLG-DOM-17 | MUST | Popup `tabindex="-1"` + `data-base-ui-focusable` | 5.5 |
| DLG-DOM-18 | MUST | Popup `hidden` iff !mounted | 5.5 |
| DLG-DOM-19 | MUST | Popup `--nested-dialogs` | 5.5 |
| DLG-DOM-20 | MUST | Popup open/closed/transition attributes; no `data-anchor-hidden` | 5.5 |
| DLG-DOM-21 | MUST | Popup `data-nested` | 5.5 |
| DLG-DOM-22 | MUST | Popup `data-nested-dialog-open` | 5.5 |
| DLG-DOM-23 | MUST | Title/Description `id` | 5.6 |
| DLG-DOM-24 | MUST | Close `data-disabled` + button attributes | 5.6 |
| DLG-DOM-25 | MUST | `data-base-ui-inert` markers on outside elements while open | 5.7 |
| DLG-DOM-26 | MUST | Scroll lock iff open && modal === true | 5.7 |
| DLG-DOM-27 | MUST | Scroll lock released at close request | 5.7 |
| DLG-DOM-28 | MUST | Consumer attribute precedence | 5.7 |
| DLG-ARIA-01 | MUST | Trigger `aria-haspopup="dialog"` | 5.8 |
| DLG-ARIA-02 | MUST | Trigger `aria-expanded` | 5.8 |
| DLG-ARIA-03 | MUST | Trigger `aria-controls` | 5.8 |
| DLG-ARIA-04 | MUST | Popup `role="dialog"` / `alertdialog` | 5.8 |
| DLG-ARIA-05 | MUST | Popup `aria-labelledby` from Title | 5.8 |
| DLG-ARIA-06 | MUST | Popup `aria-describedby` from Description | 5.8 |
| DLG-ARIA-07 | MUST | Title/Description anywhere under the Root | 5.8 |
| DLG-ARIA-08 | MUST | Backdrop `role="presentation"` | 5.8 |
| DLG-ARIA-09 | MUST | Viewport `role="presentation"` | 5.8 |
| DLG-ARIA-10 | MUST | Internal backdrop `role="presentation"` | 5.8 |
| DLG-ARIA-11 | MUST | `aria-hidden="true"` on outside while open and modal !== false | 5.8 |
| DLG-ARIA-12 | MUST | No `inert` on outside elements | 5.8 |
| DLG-KEY-01 | MUST | Escape closes topmost from anywhere; one request | 6.1 |
| DLG-KEY-02 | MUST | Escape preventDefault/stopPropagation unless canceled/allowed | 6.1 |
| DLG-KEY-03 | MUST | Escape ignored during IME composition | 6.1 |
| DLG-KEY-04 | MUST | No Escape handling while a nested dialog is open | 6.1 |
| DLG-KEY-05 | MUST | Escape in dialog inside Menu closes only the dialog | 6.1 |
| DLG-KEY-06 | MUST | Tab/Shift+Tab wrap inside popup (modal/trap-focus) | 6.1 |
| DLG-KEY-07 | MUST | Tab with no tabbables: prevented | 6.1 |
| DLG-KEY-08 | MUST | Non-modal Tab out: next/previous after trigger; `focus-out` close | 6.1 |
| DLG-KEY-09 | MUST | Composite keys stop propagation at the Popup | 6.1 |
| DLG-KEY-10 | MUST | Enter/Space activate Trigger/Close; keyboard type | 6.1 |
| DLG-KEY-11 | MUST | Controlled `open={true}` stays open on Escape | 6.1 |
| DLG-PTR-01 | MUST | Trigger click opens (`trigger-press`) | 6.2 |
| DLG-PTR-02 | MUST | Active trigger click closes | 6.2 |
| DLG-PTR-03 | MUST | Inactive trigger click switches | 6.2 |
| DLG-PTR-04 | MUST | Trigger records `openMethod` | 6.2 |
| DLG-PTR-05 | MUST | Outside-press event mode table | 6.2 |
| DLG-PTR-06 | MUST | Primary button only | 6.2 |
| DLG-PTR-07 | MUST | Single-finger touch only | 6.2 |
| DLG-PTR-08 | MUST | Sloppy touch sequence (5 px / 10 px / 1000 ms) | 6.2 |
| DLG-PTR-09 | MUST | Intentional: press must begin while open, or be virtual | 6.2 |
| DLG-PTR-10 | MUST | Modal outside targets: internal/consumer backdrop or popup ancestor | 6.2 |
| DLG-PTR-11 | MUST | Non-modal/trap-focus: any outside target | 6.2 |
| DLG-PTR-12 | MUST | Non-topmost ignores outside presses | 6.2 |
| DLG-PTR-13 | MUST | Presses on registered triggers are not outside | 6.2 |
| DLG-PTR-14 | MUST | Scrollbar presses ignored (RTL aware) | 6.2 |
| DLG-PTR-15 | MUST | Elements injected after open ignored | 6.2 |
| DLG-PTR-16 | MUST | Drag-out suppresses next outside click | 6.2 |
| DLG-PTR-17 | MUST | Prevented inside press: 0 ms suppression | 6.2 |
| DLG-PTR-18 | MUST | Internal backdrop blocks page pointer input | 6.2 |
| DLG-PTR-19 | MUST | Close click → `close-press` when open; veto; no-op when closed | 6.2 |
| DLG-FOC-01 | MUST | Default initial focus target | 6.3 |
| DLG-FOC-02 | MUST | Initial focus timing and guards | 6.3 |
| DLG-FOC-03 | MUST | No move when focus inside / `false` | 6.3 |
| DLG-FOC-04 | MUST | `preventScroll` only for popup target | 6.3 |
| DLG-FOC-05 | MUST | `initialFocus` function argument; once per open | 6.3 |
| DLG-FOC-06 | MUST | Default return target precedence | 6.3 |
| DLG-FOC-07 | MUST | Return focus after unmount commit (microtask) | 6.3 |
| DLG-FOC-08 | MUST | Return focus options; tabbable resolution | 6.3 |
| DLG-FOC-09 | MUST | Close interaction type derivation | 6.3 |
| DLG-FOC-10 | MUST | Return focus skip conditions | 6.3 |
| DLG-FOC-11 | MUST | Outside press returns focus (preventScroll support) | 6.3 |
| DLG-FOC-12 | MUST | Modal focus trap configuration | 6.3 |
| DLG-FOC-13 | MUST | Non-modal portal guards and tab-order disabling | 6.3 |
| DLG-FOC-14 | MUST | Non-modal focus-out close | 6.3 |
| DLG-FOC-15 | MUST | Focused child removed → focus popup (only with `closeOnFocusOut`) | 6.3 |
| DLG-FOC-16 | MUST | Shared previously-focused stack (blur-to-body push: modal + `closeOnFocusOut` only) | 6.3 |
| DLG-FOC-17 | SHOULD | WebKit blur of typeable element on close | 6.3 |
| DLG-FOC-18 | MUST | Popup `tabindex` never rewritten | 6.3 |
| DLG-DIS-01 | MUST | `disablePointerDismissal` scope (outside press, focus-out, focus recovery, stack push) | 6.4 |
| DLG-DIS-02 | MUST | Escape/Close/imperative unaffected | 6.4 |
| DLG-DIS-03 | MUST | Nested: topmost only | 6.4 |
| DLG-DIS-04 | MUST | Sibling modals dismiss independently | 6.4 |
| DLG-DIS-05 | MUST | Nested anchored popups don't dismiss the dialog | 6.4 |
| DLG-DIS-06 | MUST | Shadow DOM dismissal | 6.4 |
| DLG-DIS-07 | MUST | Canceled close leaves everything unchanged | 6.4 |
| DLG-DIS-08 | MUST | Controlled open stays open | 6.4 |
| DLG-DIS-09 | MUST | Imperative close reason/trigger | 6.4 |
| DLG-DIS-10 | MUST | Alert mode forced flags and remaining close paths; drawer mode flag | 6.4 |
| DLG-EVT-01 | MUST | `onOpenChange` call shape | 7 |
| DLG-EVT-02 | MUST | Reasons table | 7 |
| DLG-EVT-03 | MUST | `details.trigger` rules | 7 |
| DLG-EVT-04 | MUST | `cancel()` semantics | 7 |
| DLG-EVT-05 | MUST | `preventUnmountOnClose()` immediate write and effect | 7 |
| DLG-EVT-06 | MUST | One internal notification per non-canceled request | 7 |
| DLG-EVT-07 | MUST | Not called on mount / controlled prop change / lone claim | 7 |
| DLG-EVT-08 | MUST | `onOpenChangeComplete` timing | 7 |
| DLG-EVT-09 | MUST | No `(true)` when dismissed during enter; restarted animation awaited | 7 |
| DLG-EVT-10 | MUST | Ordering | 7 |
| DLG-ANI-01 | MUST | Mount gating per element | 9 |
| DLG-ANI-02 | MUST | Transition status configuration | 9 |
| DLG-ANI-03 | MUST | Exit waits for Popup animations only | 9 |
| DLG-ANI-04 | MUST | `keepMounted` closed state | 9 |
| DLG-ANI-05 | MUST | `unmount()` semantics | 9 |
| DLG-ANI-06 | MUST | Unmount cleanup order | 9 |
| DLG-ANI-07 | MUST | Focus manager while mounted; dismissal while open | 9 |
| DLG-ANI-08 | MUST | Reopen during exit | 9 |
| DLG-ANI-09 | MUST | Interaction layer mounting and re-wiring | 9 |
| DLG-ANI-10 | MUST | Nested counts reset on close+unmount | 9 |
| DLG-EDGE-01 | MUST | Trigger error (verbatim) | 12 |
| DLG-EDGE-02 | MUST | Root context error (verbatim) | 12 |
| DLG-EDGE-03 | MUST | Portal missing error (verbatim) | 12 |
| DLG-EDGE-04 | MUST | `openWithPayload` detached warning (verbatim) | 12 |
| DLG-EDGE-05 | MUST | `open`/`close` detached warnings (`DialogHandle`) | 12 |
| DLG-EDGE-06 | MUST | Unmatched trigger id: unassociated open + warning | 12 |
| DLG-EDGE-07 | MUST | Overlap warning deferred one frame | 12 |
| DLG-EDGE-08 | MUST | Duplicate-id registry error | 12 |
| DLG-EDGE-09 | MUST | Disabled Trigger/Close behavior | 12 |
| DLG-EDGE-10 | MUST | Suspense outside Portal | 12 |
| DLG-EDGE-11 | MUST | Handle swap keeps state | 12 |
| DLG-EDGE-12 | MUST | Remount starts fresh; no replay; no mode warning | 12 |
| DLG-EDGE-13 | MUST | Server rendering of detached triggers | 12 |
| DLG-EDGE-14 | MUST | Abandoned transition render never attaches. Port: withdrawn (chosen by the user 2026-10-28; ADR 02 Amendments) | 12 |
| DLG-EDGE-15 | MUST | Late trigger does not take over ownership | 12 |
| DLG-EDGE-16 | SHOULD | Inactive triggers not re-rendered | 12 |
| DLG-EDGE-17 | MUST | No I18N dependence beyond RTL scrollbar check | 12 |
| DLG-EDGE-18 | MUST | Substitution dropping/changing `id`: label IDREFs dangle; `aria-controls` follows rendered Popup id | 12 |

Counts: STR 13, API 27, STA 15, DOM 28, ARIA 12, KEY 11, PTR 19, FOC 18, DIS 10, EVT 10, ANI 10, EDGE 18 — 191 requirements, no gaps per `DLG-AREA`. FRM, POS, A11Y and I18N emit no IDs (§8, §10; A11Y: no live regions or screen-reader heuristics beyond PSH-ARIA-05; I18N: DLG-EDGE-17).

## 16. Open questions

1. **`preventUnmountOnClose()` survives a canceled close.** `dialog/store/DialogStore.ts:76-78` writes `preventUnmountingOnClose = true` as soon as the method is called; a subsequent `cancel()` in the same `onOpenChange` returns early (`:88-90`) without clearing it, and `internals/useUnmountAfterClose.ts:71-76` clears it only when `open` becomes `true` — which does not happen because the dialog never closed. The next non-prevented close therefore stays mounted until `unmount()`. EVD-EDGE-09 records the same observation. Intended? (Popover/Menu capture the flag per request instead, `utils/popups/popupStoreUtils.ts:228-236,271,288`.) Resolved 2026-10-28 (M4 track 8; ADR 05 Amendments 2026-10-27): reproduced as observed (DLG-EVT-05 as written). The dialog store records the call at once (`UnmountPreventionAtCall`), so a canceled close keeps the next close that is not canceled mounted until `Unmount()`; `OpenChangeEventDetails`' remark states the dialog family's rule beside Popover's per-request one. `DialogRootTests` "port: preventUnmountOnClose called in a canceled close keeps the next close mounted until unmount".
2. **Popup `tabindex` management is inert.** `FloatingFocusManager.handleTabIndex` (`:114-124`) returns early when the element has `tabindex` without `data-tabindex`; `Dialog.Popup` always renders `tabindex="-1"` (`dialog/popup/DialogPopup.tsx:80`), so the "no tabbable content → `tabindex="0"`" adjustment never applies to Dialog (DLG-FOC-18). CHANGELOG v1.6.0 "Fix non-modal focus-out close and tabindex management" suggests the management was meant to work; possibly only relevant to other popups. Resolved 2026-10-28 (M4 track 8): reproduced as observed. `DialogPopup` always renders `tabindex="-1"`, and the `focus-manager` module's tabindex management returns early for it as Base UI's does (DLG-FOC-18).
3. **`'none'` reason declared but unreachable.** `Dialog.Root.ChangeEventReason` includes `REASONS.none` (`dialog/root/DialogRoot.tsx:105`), but `useImplicitActiveTrigger` is called without `closeOnActiveTriggerUnmount` (`dialog/root/useRenderDialogRoot.tsx:77`), the only emitter of `none` (PSH-DIS-05). No Dialog path emits it. Resolved 2026-10-28 (M4 track 8): reproduced as observed. `EventReason.None` exists in the shared reasons, and no Dialog path reports it: the Root does not close when its active trigger unmounts.
4. **Nested count with two open sibling children.** `onNestedDialogOpen` is "last writer wins" (`dialog/root/useDialogRoot.ts:91-99`): if two nested Roots under the same parent are open at once (possible with handles or controlled props), the parent's `--nested-dialogs` reflects whichever reported last, and closing one of them reports `(0, 0)` while the other is still open. The docs describe the variable as "how many dialogs are nested within", but it is a depth, not a count. Resolved 2026-10-28 (M4 track 8; design point 1 (a), settled by the orchestrator 2026-10-28): reproduced as observed. `DialogRootContext.ReportNestedOpen(int dialogs, int drawers)` is last-writer-wins, so the value is a depth, not a count.
5. **Any ancestor of the Popup counts as a backdrop press.** `dialog/root/useDialogRoot.ts:77` treats a press on *any* element that contains the Popup (other than a portal node) as an outside press for modal dialogs — including consumer wrappers between Portal and Popup (e.g. the `display: contents` form in the tests). Documented as DLG-PTR-10; confirm this is the intended reading of "the dialog's owning backdrop". Resolved 2026-10-28 (M4 track 8; ADR 04 Amendments 2026-10-27): reproduced as observed. The `interactions` module treats a press on an element that contains the Popup and is not a portal node as a press on the dialog's own backdrop, beside the backdrop ids the Root sends (`OwnBackdropIds`).
6. **Touch tap without movement.** In `sloppy` touch mode a motionless tap dismisses only through the compatibility `mousedown` that browsers fire after `touchend` (`useDismiss.ts:599-605`), not through `touchend` itself (`dismissOnTouchEnd` requires > 5 px). Environments that suppress compatibility mouse events (e.g. after `preventDefault` on `touchend`) will not dismiss on a plain tap. Documented in DLG-PTR-08. Resolved 2026-10-28 (M4 track 8): reproduced as observed by the `interactions` module (FLT-PTR-27); the port's L4 tap moves about 6 px, and DLG-PTR-08's note records that the compatibility `mousedown` gate holds only while the touch is down.
7. **Backdrop is always in the DOM.** Unlike Portal/Popup, a non-nested Backdrop renders even while the dialog is closed and not kept mounted (`dialog/backdrop/DialogBackdrop.tsx:48` gates only on nesting), carrying `hidden`. Intentional (it needs no Portal context), but worth confirming given the docs' "Backdrops of the child dialogs won't be rendered" wording applies only to nesting. Resolved 2026-10-28 (M4 track 8): reproduced as observed. A non-nested `DialogBackdrop` renders while its root is in the tree, `hidden` while unmounted (DLG-STR-05, DLG-DOM-08).
8. **Outline discrepancies.** `components/page.mdx` §Dialog lists `nativeButton` but not `disabled` for Trigger and Close (both come from `NativeButtonProps`, `dialog/trigger/DialogTrigger.tsx:111`, `dialog/close/DialogClose.tsx:53`); it does not list `id` for Title/Description/Popup (native attributes with special handling, DLG-DOM-16/23); it omits the internal markers `data-base-ui-click-trigger`, `data-base-ui-focusable`, `data-base-ui-portal`, `data-base-ui-inert`. All types listed in the outline appear in §3.11. Resolved 2026-10-28 (M4 track 8): the port follows the source. `DialogTrigger` and `DialogClose` take `Disabled` and `NativeButton`; Title, Description and Popup honor a consumer `id`; the parts render the internal markers.
9. **Docs wording on `modal`.** The `modal` JSDoc says outside "pointer interactions … are disabled"; the implementation uses a full-screen internal backdrop plus `aria-hidden`, never `inert` (DLG-ARIA-12). A port must not apply `inert` or it will diverge (e.g. DLG-DIS-05's nested-popup backdrops rely on being clickable). Resolved 2026-10-28 (M4 track 8): reproduced as observed. The port applies no `inert` to the page; only the internal backdrop takes `inert`, while the dialog closes (DLG-DOM-06), and the outside is marked with `aria-hidden` (DLG-ARIA-11).
10. **`modal: 'trap-focus'` with a consumer Backdrop.** The event mode switches to `intentional` for mouse as soon as a Backdrop element exists (`dialog/root/useDialogRoot.ts:32-34`), while the docs describe `'trap-focus'` only in terms of focus and scroll. Documented in DLG-PTR-05; no test covers `'trap-focus'` + Backdrop. Resolved 2026-10-28 (M4 track 8): reproduced as observed. The Root sends the `intentional` press mode for mouse and touch whenever a backdrop exists, before the trap-focus mode is considered (DLG-PTR-05). Test: [L4] `dialog.spec.ts` "port: a trap-focus dialog with a backdrop closes on the backdrop's click, not on its mousedown".
11. **Escape with focus on an inactive trigger inside another popup** (`DialogRoot.detached-triggers.test.tsx:114-153`) relies on the trigger-level `onKeyDown` from `dismiss.trigger` being re-published on every reopen (DLG-ANI-09); the Popover's own Escape handling is skipped only because the Dialog's handler stops propagation first. Order-dependent on listener registration; flagged for the FLT author. Resolved 2026-10-28 (M4 track 8): the port does not depend on listener order. The `interactions` module marks each Escape keydown with the tokens of the sessions that handled it (the per-keydown mark, FLT §10 Q11), so the Dialog's session closes once and a session that already handled the keydown, the enclosing Popover's included, skips it. The title itself needs a Popover holding the inactive trigger and is open to Popover (M4 track 5); its sibling with a plain inactive detached trigger runs at [L4] ("closes a non-modal dialog with Escape from an inactive detached trigger").

12. **DLG-EDGE-14 for the port** (added 2026-10-28, M4 track 8, design point 7). React suspends and abandons renders, so Base UI guards a replacement Root's store attachment against an abandoned render; Blazor has no suspended or abandoned render. Resolved 2026-10-28: withdrawn for the port, chosen by the user (ADR 02 Amendments; conventions §3.5); the current Root's control, payload and `aria-expanded` are covered by PSH-STA-34..40's attached-store stack.

### Related architecture-doc discrepancies

- `popup-store-and-handles.md` PSH-STA-04 says the popup id falls back to `floatingId` when the popup element's `id` is empty. Source: `utils/popups/store.ts:152-155` uses `popupElement?.id ?? floatingId`, so an attached popup element with an empty `id` yields `''`, which then resolves to `undefined` (no fallback to `floatingId`). DLG-EDGE-18 relies on the source behavior (`aria-controls` absent when the rendered Popup has no id). Resolved: PSH-STA-04 now states the nullish (not empty-string) fallback (`popup-store-and-handles.md` PSH-STA-04).
- `floating-and-positioning.md` FLT-FOC-09 (backdrop-press clause) and FLT-FOC-12 omit that both run only while `closeOnFocusOut` is `true` (`FloatingFocusManager.tsx:424-427`); DLG-FOC-15/16 state the gating locally (already routed from alert-dialog.md). Resolved: FLT-FOC-09 and FLT-FOC-12 now state the `closeOnFocusOut` gating (`floating-and-positioning.md` FLT-FOC-09, FLT-FOC-12).
