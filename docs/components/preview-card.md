# Preview Card

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/preview-card/` |
| Tests | `packages/react/src/preview-card/**/*.test.tsx` (9 files) + `root/PreviewCardRoot.spec.tsx`, `trigger/PreviewCardTrigger.spec.tsx`, `positioner/PreviewCardPositioner.spec.tsx` (type-level only) |
| Docs page | `docs/src/app/(docs)/react/components/preview-card/page.mdx` |
| Prefix | `PRV` |
| Status | approved |
| Revised | 2026-10-29 |
| Builds on | none (shares the popup store, hover/focus/dismiss interactions and positioning with [tooltip](tooltip.md) and [popover](popover.md), see §11) |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [event-details-and-reasons](../architecture/event-details-and-reasons.md), [popup-store-and-handles](../architecture/popup-store-and-handles.md), [floating-and-positioning](../architecture/floating-and-positioning.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Preview Card is a hover card for links: a **Trigger** rendered as an `<a>` element shows a floating **Popup** with a preview of the link's destination while the pointer rests on the link (after a 600 ms delay) or while the link has keyboard focus (`:focus-visible`), and hides it when the pointer leaves the link and the card (after a 300 ms delay), when the link loses focus, on Escape, or on a press outside. The card is positioned against the link by the shared anchor-positioning mechanism and, uniquely among Base UI popups, against the **individual line box** of a multi-line inline link that the pointer entered (§10). Because the card can be opened by several triggers (contained in the Root or *detached* and connected through a handle created by `PreviewCard.createHandle()`), the Root can render different content per trigger through a `payload`, and the optional **Viewport** part animates between the contents of two triggers.

The component is deliberately **not** exposed to assistive technology: the Trigger carries no `aria-expanded`, `aria-controls`, `aria-haspopup` or `aria-describedby`; the Popup has no `role` and is never focused automatically; touch pointers never open it. The official guidance (`docs/src/app/(docs)/react/components/preview-card/page.mdx:20-23`) is that the card is a visual progressive enhancement for sighted mouse and keyboard users and that everything in it must also be available at the link's destination. This is the main difference from [tooltip](tooltip.md), whose popup is a hoverable description of a `<button>` trigger (Tooltip adds delay groups, cursor tracking, `disabled` and `disableHoverablePopup`; Preview Card adds inline-line anchoring, a mandatory hoverable popup and link semantics), and from [popover](popover.md), which opens on press, manages focus and emits ARIA relationships.

What the user experiences: hover a link → after 600 ms the card appears next to the hovered line; move the pointer along a "safe polygon" into the card → it stays open; leave the card or the link → it closes 300 ms later; Tab to the link → after 600 ms the card appears; Tab away or press Escape → it closes; click the link → the browser navigates (the card neither opens nor closes on click); hover another link of the same card while it is open → the card moves to that link immediately, re-rendering its content for that link's payload and, with a Viewport, animating between the two contents.

## 2. Anatomy

```
<PreviewCard.Root>              required, 1 per card       renders no element
  <PreviewCard.Trigger>         optional, 0..n             default element: a      (may also be detached, see below)
  <PreviewCard.Portal>          required for a visible card, 0..1   default element: div (portal node)
    <PreviewCard.Backdrop>      optional, 0..1             default element: div
    <PreviewCard.Positioner>    required inside Portal, 1  default element: div
      <PreviewCard.Popup>       required inside Positioner, 1   default element: div
        <PreviewCard.Arrow>     optional, 0..1             default element: div
        <PreviewCard.Viewport>  optional, 0..1             default element: div
```

`PreviewCard.createHandle()` / `PreviewCard.Handle` are exported next to the parts (`preview-card/index.parts.ts:9-12`). A Trigger given a `handle` prop is a **detached trigger**: it may be rendered anywhere in the tree, before or after the Root, and binds to whichever Root currently uses that handle (PSH-STR-02/03/05). Triggers inside the Root with no `handle` are **contained triggers** and bind to the nearest ancestor Root. Both kinds may be mixed on one card.

The Backdrop is placed inside the Portal in the official anatomy (`page.mdx:32-43`) but only requires a Root; it is purely presentational. Positioner, Popup, Arrow and Viewport are only rendered while the card is mounted (or the Portal is `keepMounted`), because the Portal renders nothing otherwise (PRV-STR-03).

- **PRV-STR-01** — MUST. `PreviewCard.Root` renders no element of its own; it owns the popup store (PSH-STR-01), provides it to descendant parts, and renders its children (or the result of a function child, PRV-API-07). A Root that is not inside another `PreviewCard.Root` creates a floating-tree boundary for nested cards (FLT-STR-07); a Root inside another Root's subtree joins the outer tree instead of creating one.
  Source: `preview-card/root/PreviewCardRoot.tsx:86-92,117-129`; `floating-ui-react/components/FloatingTree.tsx:90-95`. Test: [L4] `PreviewCardRoot.test.tsx` "keeps the parent preview card open when clicking nested trigger" (nested Roots).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the nested-trigger click is the `interactions` module's press handling, so the title is browser-only; green in `tests/browser/preview-card.spec.ts` ("nested preview card interactions") on both render modes. The root rendering no element stays at L2 (`PreviewCardRootTests` "port: the root renders no element, and the popup renders no role, …").
- **PRV-STR-02** — MUST. `PreviewCard.Trigger` renders an `<a>` element by default and MUST either be a descendant of a `PreviewCard.Root` or receive a `handle`; otherwise rendering throws the error of PRV-EDGE-02.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:40-47,90`. Test: [L2] `PreviewCardTrigger.test.tsx` "throws without PreviewCard.Root or a handle"; describeConformance (`refInstanceof: HTMLAnchorElement`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardMisuseTests` "throws without PreviewCard.Root or a handle" and `PreviewCardConformanceTests` (the Trigger as an `a`).
- **PRV-STR-03** — MUST. `PreviewCard.Portal` renders a portal node `<div>` (PRV-DOM-04) **only while the card is mounted or `keepMounted` is set**; otherwise it renders nothing, so no Positioner/Popup/Arrow/Viewport/Backdrop placed inside it exists in the document while the card is closed and unmounted. It MUST be a descendant of a Root (PRV-EDGE-01).
  Source: `preview-card/portal/PreviewCardPortal.tsx:19-33`. Test: [L2] `PreviewCardRoot.test.tsx` "should close when the trigger is unhovered" (`queryByText('Content')` is `null` after close); `PreviewCardPortal.test.tsx` describeConformance (`keepMounted`, `refInstanceof: HTMLDivElement`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardRootTests` "should close when the trigger is unhovered" (the closed state; the exit and the removal are the `transitions` module's, green in `tests/browser/preview-card.spec.ts`'s trio) and `PreviewCardConformanceTests` (the Portal).
- **PRV-STR-04** — MUST. `PreviewCard.Positioner` renders a `<div>` by default and MUST be a descendant of a `PreviewCard.Portal` (PRV-EDGE-03) inside a Root (PRV-EDGE-01). Exactly one Positioner is expected per card: it publishes itself as the card's positioner element and floating element (PSH-STA-46).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:48-50,96-103`; `preview-card/portal/PreviewCardPortalContext.ts:6-12`. Test: [L2] `PreviewCardPositioner.test.tsx` "throws a descriptive error when rendered outside <PreviewCard.Portal>"; describeConformance (`refInstanceof: HTMLDivElement`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardMisuseTests` "throws a descriptive error when rendered outside <PreviewCard.Portal>" and `PreviewCardConformanceTests` (the Positioner).
- **PRV-STR-05** — MUST. `PreviewCard.Popup` renders a `<div>` by default and MUST be a descendant of a Root and of a Positioner (PRV-EDGE-01, PRV-EDGE-04); it publishes itself as the card's popup element, which is the element whose animations gate unmounting (§9).
  Source: `preview-card/popup/PreviewCardPopup.tsx:27-28,59-69`. Test: [L2] `PreviewCardPopup.test.tsx` "throws a descriptive error when rendered outside <PreviewCard.Root>", "throws a descriptive error when rendered outside <PreviewCard.Positioner>", "should render the children".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardMisuseTests` (both misuse titles) and `PreviewCardPopupPartsTests` "should render the children".
- **PRV-STR-06** — MUST. `PreviewCard.Arrow` renders a `<div>` by default and MUST be a descendant of a Root and a Positioner (PRV-EDGE-01, PRV-EDGE-04); it registers itself as the arrow element of the positioning session (FLT-DOM-10).
  Source: `preview-card/arrow/PreviewCardArrow.tsx:22-23,34-39`. Test: [L2] `PreviewCardArrow.test.tsx` describeConformance (`refInstanceof: HTMLDivElement`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardConformanceTests` (the Arrow).
- **PRV-STR-07** — MUST. `PreviewCard.Backdrop` renders a `<div>` by default and requires only a Root ancestor; it is structural (state attributes and fixed styles, PRV-DOM-18/19) and attaches no handlers.
  Source: `preview-card/backdrop/PreviewCardBackdrop.tsx:21-47`. Test: [L2] `PreviewCardBackdrop.test.tsx` describeConformance (rendered directly inside `Root open`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardConformanceTests` (the Backdrop inside an open Root).
- **PRV-STR-08** — MUST. `PreviewCard.Viewport` renders a `<div>` by default, MUST be a descendant of a Root and a Positioner (PRV-EDGE-01, PRV-EDGE-04), and wraps its consumer-supplied children in the morphing containers of PRV-DOM-20 (the consumer's `children` are consumed, not rendered directly). It is optional; omitting it only removes content transitions between triggers.
  Source: `preview-card/viewport/PreviewCardViewport.tsx:21-45`. Test: [L4] `PreviewCardViewport.test.tsx` "throws a descriptive error when rendered outside <PreviewCard.Positioner>"; `viewportConformanceTests.tsx` "renders children in the `current` container by default". Docs: `components/preview-card/page.mdx` §Viewport (line 221).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes. L2 keeps the misuse title (`PreviewCardMisuseTests`) and the Viewport's conformance suite.
- **PRV-STR-09** — MUST. Any number of Triggers (contained, detached, or mixed) MAY open one card; at most one is the **active trigger** at a time (PSH-STR-06), and it is the one the card is anchored to, whose `payload` the Root renders, and which carries `data-popup-open`.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:50-51,60-68`; `utils/popups/store.ts:189-206`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active" (`hover`, `focus`; contained and detached). Docs: `components/preview-card/page.mdx` §Multiple triggers.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 keeps the store's clause (`PreviewCardDetachedTriggersTests` "sets the payload of the active trigger and renders content based on it").
- **PRV-STR-10** — MUST. A `PreviewCard.Root` rendered inside another card's Popup is a nested card: its Positioner registers a child node of the outer card's node in the shared floating tree, so that Escape (PRV-DIS-02), outside press (PRV-DIS-05) and hover-leave (PRV-DIS-08) of the two cards are coordinated. Every part binds to its *nearest* Root.
  Source: `preview-card/root/PreviewCardRoot.tsx:120-128`; `preview-card/positioner/PreviewCardPositioner.tsx:50,107`; `floating-ui-react/components/FloatingTree.tsx:32-50`. Test: [L4] `PreviewCardRoot.test.tsx` "keeps the parent preview card open when hovering nested trigger", "parent popup closes as soon as the child popup closes".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): "keeps the parent preview card open when hovering nested trigger" is green in `tests/browser/preview-card.spec.ts` on both render modes; the tree join is a nested Root's session naming the outer one (`PreviewCardRootTests` "port: a nested root's session names the enclosing card's session as its parent", L2). "parent popup closes as soon as the child popup closes" is written and skipped, open to design point 11.6 (the floating hover handle's tree parent).
- **PRV-STR-11** — MUST. The namespace exports exactly: `Root`, `Portal`, `Trigger`, `Positioner`, `Popup`, `Arrow`, `Backdrop`, `Viewport`, `createHandle`, `Handle`, plus the data-attribute and CSS-variable name constants listed in `preview-card/index.ts:12-19`.
  Source: `preview-card/index.parts.ts:1-12`; `preview-card/index.ts:1-19`. Test: none (outline `components/page.mdx` "Preview Card" exports list).

## 3. Public API

### 3.1 `PreviewCard.Root`

**Default element:** none (renders no element). **Accepts `render` element substitution:** no. **Accepts `className`/`style` as function of state:** no (the Root has no element and no `className`/`style`/`render` props).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `defaultOpen` | `boolean` | `false` | Whether the card is initially open (component-owned open state). | PRV-API-01 |
| `open` | `boolean \| undefined` | `undefined` | Consumer-owned open state. | PRV-API-02 |
| `onOpenChange` | `(open: boolean, eventDetails: PreviewCard.Root.ChangeEventDetails) => void` | `undefined` | Called when the card wants to open or close (§7). | PRV-API-03 |
| `onOpenChangeComplete` | `(open: boolean) => void` | `undefined` | Called after the enter/exit animations complete (§9). | PRV-API-04 |
| `actionsRef` | `RefObject<PreviewCard.Root.Actions \| null>` | `undefined` | Receives `{ unmount(), close() }` (§9, §6.4). | PRV-API-05 |
| `handle` | `PreviewCard.Handle<Payload> \| undefined` | `undefined` | Binds the Root to a handle so detached triggers and imperative calls control it. | PRV-API-06 |
| `children` | `ReactNode \| ({ payload }: { payload: Payload \| undefined }) => ReactNode` | — | Content, or a function receiving the active trigger's payload. | PRV-API-07 |
| `triggerId` | `string \| null \| undefined` | `undefined` | Consumer-owned id of the active trigger (use with `open`). | PRV-API-08 |
| `defaultTriggerId` | `string \| null \| undefined` | `null` | Initial active trigger id (use with `defaultOpen`). | PRV-API-09 |

**Consumed (not forwarded to any element):** all of the above (there is no element).

- **PRV-API-01** — MUST. `defaultOpen` defaults to `false` and seeds the component-owned open state once; it is ignored when `open` is supplied (PSH-STA-01, EVD-STA-01/02).
  Source: `preview-card/root/PreviewCardRoot.tsx:24,38`. Test: [L4] `popupConformanceTests.tsx` "opens initially and remains uncontrolled", "is ignored when `open={false}` is passed", "is ignored when `open={true}` is passed, so Escape cannot close".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes. L2 keeps "is ignored when `open={true}` is passed, so Escape cannot close" (`PreviewCardRootTests`).
- **PRV-API-02** — MUST. `open`, when not `undefined`, is the effective open state (PSH-STA-02); the component then never changes the open state itself and only reports requests through `onOpenChange` (EVD-STA-04/06).
  Source: `preview-card/root/PreviewCardRoot.tsx:23,39,48`; `utils/popups/store.ts:150`. Test: [L4] `popupConformanceTests.tsx` "opens the popup with the `open` prop"; `PreviewCardRoot.test.tsx` "should call onOpenChange when the open state changes".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes. "should call onOpenChange when the open state changes" is green at L2 (`PreviewCardRootTests`) and in `tests/browser/preview-card.spec.ts`'s trio.
- **PRV-API-03** — MUST. `onOpenChange` is optional; the latest function supplied is the one invoked (it is stored in the store context, not captured at creation).
  Source: `preview-card/root/PreviewCardRoot.tsx:51`; `utils/popups/store.ts:139`. Test: [L2] `PreviewCardRoot.test.tsx` "closes the preview card when the `close` method is called".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardRootTests` "closes the preview card when the `close` method is called" (`CloseAsync`).
- **PRV-API-04** — MUST. `onOpenChangeComplete` is optional and follows TRN-API-04 (one boolean argument, no details).
  Source: `preview-card/root/PreviewCardRoot.tsx:52,153`; `preview-card/popup/PreviewCardPopup.tsx:37-45`. Test: [L4] `popupConformanceTests.tsx` "is called on open when there is no enter animation".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes.
- **PRV-API-05** — MUST. `actionsRef`, when supplied, receives an object `{ unmount, close }` where `unmount()` is the unmount action (TRN-API-05) and `close()` requests a close with reason `imperative-action` and no trigger (PSH-API-09). The ref MAY be absent.
  Source: `preview-card/root/PreviewCardRoot.tsx:72-79,155-161,186-189`. Test: [L2] `PreviewCardRoot.test.tsx` "unmounts the preview card when the `unmount` method is called", "closes the preview card when the `close` method is called".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardRootTests` "unmounts the preview card when the `unmount` method is called" and "closes the preview card when the `close` method is called" (`Unmount`, `CloseAsync`); the retained close's browser half (the exit held in the ending phase until `Unmount`) stays open to [issue 147](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/147), as Tooltip's TIP-EVT-04.
- **PRV-API-06** — MUST. `handle` (default `undefined`) attaches the Root's store to the handle for the Root's mounted lifetime (PSH-STR-03/04); while a handle is given the Root's interactions are mounted even when closed (PRV-STA-16).
  Source: `preview-card/root/PreviewCardRoot.tsx:28,84,88`; `utils/popups/popupStoreUtils.ts:103-115`. Test: [L4] `detachedTriggersConformanceTests.tsx` "ignores imperative calls while no root is attached and follows the root across remounts".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 keeps the same title on the store (`PreviewCardDetachedTriggersTests`).
- **PRV-API-07** — MUST. When `children` is a function it is called with `{ payload }` where `payload` is the store's current payload (`undefined` when none) (PSH-API-07); otherwise `children` is rendered as-is.
  Source: `preview-card/root/PreviewCardRoot.tsx:57,90,168-172`. Test: [L4] `detachedTriggersConformanceTests.tsx` "sets the payload of the active trigger and renders content based on it"; `PreviewCardRoot.spec.tsx` (payload typed by the handle's type argument).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. The type clause is L1 (`PreviewCardStoreTests` "port: the handle carries the payload type of its root and triggers"); L2 keeps the same title (`PreviewCardDetachedTriggersTests`).
- **PRV-API-08** — MUST. `triggerId`, when not `undefined` (a controlled `null` meaning "no active trigger"), is the effective active trigger id (PSH-STA-03); it is meant to be used with `open` and is unnecessary when uncontrolled.
  Source: `preview-card/root/PreviewCardRoot.tsx:29,41,49,173-178`; `utils/popups/store.ts:148`. Test: [L4] `detachedTriggersConformanceTests.tsx` "marks the trigger matching a controlled `triggerId` as active", "allows controlling the open state and active trigger programmatically".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes.
- **PRV-API-09** — MUST. `defaultTriggerId` defaults to `null` and seeds the component-owned active trigger id (PSH-API-06); with `defaultOpen` it makes the card open initially anchored to that trigger with that trigger's payload.
  Source: `preview-card/root/PreviewCardRoot.tsx:30,40,179-183`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens initially with the payload of a non-first `defaultTriggerId`".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 keeps the same title (`PreviewCardDetachedTriggersTests`).

#### State object (`PreviewCard.Root.State`)

Empty (`{}`): the Root renders no element and exposes no state object.
Source: `preview-card/root/PreviewCardRoot.tsx:131,207`.

#### Data attributes

None (no element).

#### CSS variables

None.

### 3.2 `PreviewCard.Trigger`

**Default element:** `<a>`. **Accepts `render` element substitution:** yes (the substitute receives the native `<a>` attributes, including `href`, `PreviewCardTrigger.spec.tsx:5-10`). **Accepts `className`/`style` as function of state:** yes (state = `PreviewCard.Trigger.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `delay` | `number` | `600` | Milliseconds to wait before opening on hover or focus. | PRV-API-10 |
| `closeDelay` | `number` | `300` | Milliseconds to wait before closing after the pointer leaves. | PRV-API-11 |
| `handle` | `PreviewCard.Handle<Payload> \| undefined` | `undefined` | Makes this a detached trigger bound to the handle's current Root. | PRV-API-12 |
| `payload` | `Payload \| undefined` | `undefined` | Value exposed to the Root's function child while this trigger is active. | PRV-API-13 |
| `id` | `string \| undefined` | generated | Trigger id and rendered `id` attribute. | PRV-API-14 |
| `href` and every other `<a>` attribute / handler | | — | Forwarded unchanged; handlers are chained per RND-EVT-02. The outline omits `href`/`id`. | PRV-API-15 |
| `className`, `style`, `render` | per RND | — | See [rendering-and-composition](../architecture/rendering-and-composition.md). | — |

**Consumed (not forwarded to the element):** `delay`, `closeDelay`, `handle`, `payload`, `render`, `className`, `style`; `id` is re-emitted as the resolved trigger id.

- **PRV-API-10** — MUST. `delay` defaults to `600` (`OPEN_DELAY`) and is the open delay of both the hover interaction (PRV-PTR-01) and the focus interaction (PRV-KEY-01) of *this* trigger; different triggers of one card MAY have different delays.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:57,74,80,133-137`; `preview-card/utils/constants.ts:1`. Test: [L4] `PreviewCardRoot.test.tsx` "should open after delay with rest type by default", "should open when the trigger is hovered" (`OPEN_DELAY`). Docs: `CHANGELOG.md` v1.0.0-beta.5 "Move delay props to trigger".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes under a paused clock; the fallback is L1 (`PreviewCardStoreTests` "port: a trigger's open delay falls back to 600 ms and its close delay to 300 ms") and the options L2 (`PreviewCardTriggerTests`).
- **PRV-API-11** — MUST. `closeDelay` defaults to `300` (`CLOSE_DELAY`); it is the close delay of this trigger's hover-leave (PRV-PTR-04) and, while this trigger is the active trigger, the close delay used when the pointer leaves the card itself (PRV-STA-11, PRV-PTR-06).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:58,66,74,138-142`; `preview-card/utils/constants.ts:2`; `preview-card/popup/PreviewCardPopup.tsx:35,47-49`. Test: [L4] `PreviewCardRoot.test.tsx` "should close after delay", "should close when the trigger is unhovered" (`CLOSE_DELAY`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes under a paused clock; the fallback is L1 and the forwarded value L2 (`PreviewCardTriggerTests` "port: the active trigger forwards its close delay …").
- **PRV-API-12** — MUST. A Trigger's `handle`, when given, takes precedence over any ancestor Root: the Trigger reads the store currently exposed by the handle (the inert fallback store while no Root is attached, PSH-STA-34/37) and migrates its registration when the handle's store changes (PSH-STA-26/36).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:40-42`; `utils/popups/usePopupHandleStore.ts:17-36`. Test: [L4] `detachedTriggersConformanceTests.tsx` "registers a detached trigger declared after the root", "keeps a detached trigger working $name".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 adds "port: a detached trigger declared before the root registers with it, and keeps working when the root remounts" (`PreviewCardDetachedTriggersTests`).
- **PRV-API-13** — MUST. `payload` (default `undefined`) is forwarded to the store together with `closeDelay` when this trigger is, or becomes, the active trigger while the card is open or mounted (PSH-STA-28/29/30).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:60-68,129-132`; `utils/popups/popupStoreUtils.ts:339-364,382-391`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should open the preview card immediately when hovering another trigger" (`Content: 1` → `Content: 2`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the title is browser-only (`describe.skipIf(isJSDOM)`), green on `preview-card-detached` (`case=multiple-root`) on both render modes. L2 keeps the request sequence (`PreviewCardDetachedTriggersTests`, same title).
- **PRV-API-14** — MUST. `id`, when given, is both the rendered `id` attribute and the trigger id used by the registry, `triggerId`/`defaultTriggerId` and `handle.open()`; when absent a stable generated id of the form `base-ui-<token>` is used for both (PSH-API-08, RND-DOM-27).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:33,49,98`; `internals/useBaseUiId.ts:9-10`. Test: [L2] `PreviewCardRoot.detached-triggers.test.tsx` "when opened and closed imperatively" (`handle.open('trigger-1')`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 as "port: does not re-render inactive triggers when $name" (`PreviewCardDetachedTriggersTests`, four cases; a bystander's renders counted through its `ClassValue`).
- **PRV-API-15** — MUST. The Trigger adds no `href`, `tabindex`, `role` or `type` of its own: link semantics, focusability and native navigation come entirely from the consumer's `<a>` attributes (an `<a>` without `href` is neither a link nor focusable; the keyboard path then needs the consumer's `tabIndex`).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:90-102` (no such attributes in the part-supplied props). Test: [L2] `PreviewCardRoot.test.tsx` (every test uses `href="#"` and `getByRole('link')`); `PreviewCardPositioner.test.tsx` "positions the popup relative to the side-aligned rect when opened via focus" (`tabIndex={0}` without `href`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardTriggerTests` "port: renders an anchor with the consumer's href, its trigger id, and no type, role, tabindex or ARIA attribute of its own"; the `tabIndex={0}` title is green in `tests/browser/preview-card.spec.ts` on both render modes.

#### State object (`PreviewCard.Trigger.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | `true` iff the card is open **and** this trigger is the active trigger (PSH-STA-08). |

Source: `preview-card/trigger/PreviewCardTrigger.tsx:51,82,113-118`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-popup-open` | state `open === true`; empty value. Absent otherwise. | PRV-DOM-02 |

#### CSS variables

None.

### 3.3 `PreviewCard.Portal`

**Default element:** `<div>` (the portal node, PRV-DOM-04). **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `PreviewCard.Portal.State`, which is empty).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `keepMounted` | `boolean` | `false` | Keep the portal node and its subtree in the DOM while the card is closed. | PRV-API-16 |
| `container` | `HTMLElement \| ShadowRoot \| RefObject<HTMLElement \| ShadowRoot \| null> \| null \| undefined` | `undefined` → `document.body` | Element the portal node is appended to. | PRV-API-17 |
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | Applied to the portal node. | — |

**Consumed:** `keepMounted` (published to the Positioner through ancestor-provided shared state).

- **PRV-API-16** — MUST. `keepMounted` defaults to `false`. When `true` the Portal renders its subtree even while the card is not mounted (TRN-API-01/02); the Positioner then receives `hidden` (PRV-DOM-06) and the positioning session only runs while mounted (FLT-POS-12). The value is also what the Positioner reads as its private `keepMounted` positioning parameter (FLT-API-17).
  Source: `preview-card/portal/PreviewCardPortal.tsx:19,24,30,42-46`; `preview-card/positioner/PreviewCardPositioner.tsx:49,74`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should reposition to a different trigger when reopened with keepMounted=true"; `popupConformanceTests.tsx` "hides the kept-mounted popup once the exit animation finishes".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: both cited titles are browser-only: "should reposition to a different trigger when reopened with keepMounted=true" on `preview-card-detached` and the suite's on `preview-card-conformance`, green on both render modes. L2 adds "port: a kept-mounted card renders its closed positioner as hidden" (`PreviewCardPopupPartsTests`).
- **PRV-API-17** — MUST. `container` follows FLT-API-13: an element or shadow root (or a DOM handle to one) is used directly; `undefined` resolves to the nearest enclosing portal node, else `document.body`; `null` renders nothing until a container is supplied.
  Source: `preview-card/portal/PreviewCardPortal.tsx:31,47-51`; `floating-ui-react/components/FloatingPortal.tsx:93-123`. Test: [L4] `PreviewCardPositioner.test.tsx` "uses the hovered line with a custom anchor in a clipped keepMounted portal" (`container={portalContainer}`, `toContainElement`). Docs: `CHANGELOG.md` v1.0.0-beta.2 "Support `ShadowRoot` containers".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes (`preview-card-inline`, `case=custom-anchor`, the container given as a selector).

#### State object (`PreviewCard.Portal.State`)

Empty (`{}`). Source: `preview-card/portal/PreviewCardPortal.tsx:36`.

#### Data attributes

None from state; the portal node carries `data-base-ui-portal=""` and a generated `id` (PRV-DOM-04).

#### CSS variables

None.

### 3.4 `PreviewCard.Backdrop`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `PreviewCard.Backdrop.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | Forwarded; consumer `style` merges over the fixed styles of PRV-DOM-18. | — |

No part-specific props.

#### State object (`PreviewCard.Backdrop.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state of the card. |
| `transitionStatus` | `'starting' \| 'ending' \| 'idle' \| undefined` | Card transition status (TRN). |

Source: `preview-card/backdrop/PreviewCardBackdrop.tsx:22-29,52-61`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` | `open === true` | PRV-DOM-19 |
| `data-closed` | `open === false` | PRV-DOM-19 |
| `data-starting-style` | `transitionStatus === 'starting'` | PRV-DOM-19 |
| `data-ending-style` | `transitionStatus === 'ending'` | PRV-DOM-19 |

#### CSS variables

None.

### 3.5 `PreviewCard.Positioner`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `PreviewCard.Positioner.State`).

#### Props

All props are the shared positioner parameters specified in FLT §3.1; the table lists Preview Card's defaults.

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `anchor` | `Element \| null \| VirtualElement \| RefObject \| (() => Element \| VirtualElement \| null)` | `undefined` (active trigger) | Element to position against. | PRV-API-18 |
| `positionMethod` | `'absolute' \| 'fixed'` | `'absolute'` | CSS position strategy. | PRV-API-18 |
| `side` | `'top' \| 'bottom' \| 'left' \| 'right' \| 'inline-end' \| 'inline-start'` | `'bottom'` | Preferred side. | PRV-API-18 |
| `sideOffset` | `number \| OffsetFunction` | `0` | Distance from the anchor. | PRV-API-18 |
| `align` | `'start' \| 'center' \| 'end'` | `'center'` | Alignment on the side. | PRV-API-18 |
| `alignOffset` | `number \| OffsetFunction` | `0` | Offset along the alignment axis. | PRV-API-18 |
| `collisionBoundary` | `'clipping-ancestors' \| Element \| Element[] \| Rect` | `'clipping-ancestors'` | Confinement area. | PRV-API-18 |
| `collisionPadding` | `number \| Partial<{top,right,bottom,left}>` | `5` | Padding from the boundary. | PRV-API-18 |
| `arrowPadding` | `number` | `5` | Minimum arrow distance from the popup edges. | PRV-API-18 |
| `sticky` | `boolean` | `false` | Keep in viewport when the anchor scrolls away. | PRV-API-18 |
| `disableAnchorTracking` | `boolean` | `false` | Stop following anchor layout changes. | PRV-API-18 |
| `collisionAvoidance` | `CollisionAvoidance` | `{ fallbackAxisSide: 'end' }` (→ `side: 'flip'`, `align: 'flip'`) | Collision strategy. | PRV-API-18 |
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | Forwarded; consumer `style` merges over the positioning styles (PRV-DOM-11). | — |

There is **no** `keepMounted` prop on the Positioner (PRV-API-19).

- **PRV-API-18** — MUST. The Positioner's defaults are exactly: `positionMethod 'absolute'`, `side 'bottom'`, `align 'center'`, `sideOffset 0`, `alignOffset 0`, `collisionBoundary 'clipping-ancestors'`, `collisionPadding 5`, `arrowPadding 5`, `sticky false`, `disableAnchorTracking false`, `collisionAvoidance = { fallbackAxisSide: 'end' }` (the free-popup default, whose missing keys resolve to `side: 'flip'`, `align: 'flip'` per FLT-API-10); `anchor` defaults to the active trigger element.
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:29-46`; `internals/constants.ts:26-28`. Test: [L4] `positionerConformanceTests.tsx` "offsets the positioner when a number is specified" (`sideOffset`, `alignOffset`), "rests exactly at collisionPadding from the colliding edge". Docs: `components/page.mdx` "Preview Card - Positioner" props.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes. L2 adds the defaults sent to the module (`PreviewCardPopupPartsTests` "port: the positioner renders role presentation and PreviewCard's default side, …").
- **PRV-API-19** — MUST. `keepMounted` is not a Positioner prop (passing it is a type error); the Positioner derives it from the enclosing Portal (PRV-API-16).
  Source: `preview-card/positioner/PreviewCardPositioner.spec.tsx:3-4`; `preview-card/positioner/PreviewCardPositioner.tsx:49`. Test: [L1] `PreviewCardPositioner.spec.tsx` (type-level `@ts-expect-error`). Docs: `CHANGELOG.md` v1.0.0-alpha.5 "Require `Portal` part … The `keepMounted` prop was removed from the Positioner".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L1]: the type-level check is a reflection title at L1: `PreviewCardStoreTests` "port: PreviewCardPositioner declares no KeepMounted parameter, which lives on PreviewCardPortal".

#### State object (`PreviewCard.Positioner.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Rendered side, logical when a logical side was requested (FLT-POS-10, FLT-I18N-02). |
| `align` | `Align` | Rendered alignment. |
| `anchorHidden` | `boolean` | Anchor is clipped out of view (FLT-POS-09). |
| `instant` | `'dismiss' \| 'focus' \| undefined` | Instant marker (PRV-STA-10). |

Source: `preview-card/positioner/PreviewCardPositioner.tsx:88-94,112-133`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | `open` true / false | PRV-DOM-09 |
| `data-anchor-hidden` | `anchorHidden === true` | PRV-DOM-09 |
| `data-side` | rendered side: `top` \| `bottom` \| `left` \| `right` \| `inline-end` \| `inline-start` | PRV-DOM-09 |
| `data-align` | `start` \| `center` \| `end` | PRV-DOM-09 |
| `data-instant` | `dismiss` \| `focus` when `instant` is set (not in the outline, §16 Q3) | PRV-DOM-09 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--available-width`, `--available-height` | Positioner inline style | px available between anchor and boundary; seeded `100vw`/`100vh` before the first pass | PRV-DOM-10 |
| `--anchor-width`, `--anchor-height` | Positioner inline style | anchor rect size in px (device-pixel snapped) | PRV-DOM-10 |
| `--transform-origin` | Positioner inline style | origin point for scale animations | PRV-DOM-10 |
| `--positioner-width`, `--positioner-height` | Positioner inline style | measured positioner size, written only by the Viewport's auto-resize (PSH) | PRV-DOM-10 |

### 3.6 `PreviewCard.Popup`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `PreviewCard.Popup.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render`, other `<div>` attributes (incl. `id`, `tabIndex`) | per RND | — | Forwarded last, so a consumer `tabIndex` overrides the part's `-1` (PRV-DOM-12). | — |

No part-specific props.

#### State object (`PreviewCard.Popup.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Rendered side (from the Positioner). |
| `align` | `Align` | Rendered alignment (from the Positioner). |
| `instant` | `'dismiss' \| 'focus' \| undefined` | Instant marker (PRV-STA-10). |
| `transitionStatus` | `TransitionStatus` | Card transition status (TRN). |

Source: `preview-card/popup/PreviewCardPopup.tsx:51-57,74-95`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | `open` true / false | PRV-DOM-13 |
| `data-starting-style` / `data-ending-style` | `transitionStatus` `'starting'` / `'ending'` | PRV-DOM-13 |
| `data-side`, `data-align` | rendered side / align | PRV-DOM-13 |
| `data-instant` | `dismiss` \| `focus` when set (not in the outline, §16 Q3) | PRV-DOM-13 |

#### CSS variables

None set by the Popup itself (`--popup-width`/`--popup-height` are written on the Popup only by the Viewport's auto-resize, PSH).

### 3.7 `PreviewCard.Arrow`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `PreviewCard.Arrow.State`); a consumer `style` merges over the arrow position styles.

#### Props

No part-specific props; `className`, `style`, `render` and other `<div>` attributes per RND.

#### State object (`PreviewCard.Arrow.State`)

| Field | Type | Meaning |
|---|---|---|
| `open` | `boolean` | Effective open state. |
| `side` | `Side` | Rendered side. |
| `align` | `Align` | Rendered alignment. |
| `uncentered` | `boolean` | The arrow could not be centered on the anchor (FLT-DOM-08 `arrowUncentered`). |

Source: `preview-card/arrow/PreviewCardArrow.tsx:25-32,44-61`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-open` / `data-closed` | `open` true / false | PRV-DOM-17 |
| `data-side`, `data-align` | rendered side / align | PRV-DOM-17 |
| `data-uncentered` | `uncentered === true` | PRV-DOM-17 |

#### CSS variables

None.

### 3.8 `PreviewCard.Viewport`

**Default element:** `<div>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes (state = `PreviewCard.Viewport.State`).

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `children` | `ReactNode` | — | Content to render inside the transition containers. | PRV-API-20 |
| `className`, `style`, `render`, other `<div>` attributes | per RND | — | Forwarded. | — |

**Consumed:** `children` (re-rendered inside the `[data-current]` container).

- **PRV-API-20** — MUST. `children` is rendered inside the Viewport's current-content container and, during a content transition, a DOM clone of the previous content is rendered in a previous-content container (PRV-DOM-20); the consumer's children never become direct children of the Viewport element.
  Source: `preview-card/viewport/PreviewCardViewport.tsx:21,28-32,43`; `utils/usePopupViewport.tsx:229-268`. Test: [L4] `viewportConformanceTests.tsx` "renders children in the `current` container by default" (L4 for the transition-clone clause).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes.

#### State object (`PreviewCard.Viewport.State`)

| Field | Type | Meaning |
|---|---|---|
| `activationDirection` | `string \| undefined` | `"<left|right|''> <down|up|''>"` from the previous to the new trigger; `undefined` when no transition happened. |
| `transitioning` | `boolean` | A previous-content container is currently rendered. |
| `instant` | `'dismiss' \| 'focus' \| undefined` | Instant marker (PRV-STA-10). |

Source: `preview-card/viewport/PreviewCardViewport.tsx:34-38,48-61`.

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-activation-direction` | the `activationDirection` string when non-empty | PRV-DOM-21 |
| `data-transitioning` | `transitioning === true` | PRV-DOM-21 |
| `data-instant` | `dismiss` \| `focus` when set | PRV-DOM-21 |
| `data-current` / `data-previous` | on the Viewport's *child* containers, not on the Viewport | PRV-DOM-20 |

#### CSS variables

| Variable | Set on | Value | Req |
|---|---|---|---|
| `--popup-width`, `--popup-height` | the `[data-previous]` container | px size of the popup when the previous content was rendered | PRV-DOM-22 |

### 3.9 `PreviewCard.createHandle()` and `PreviewCard.Handle`

| Member | Signature | Req |
|---|---|---|
| `PreviewCard.createHandle<Payload>()` | `() => PreviewCard.Handle<Payload>` | PRV-API-21 |
| `handle.open(triggerId)` | `(triggerId: string) => void` | PRV-API-21, PRV-API-22 |
| `handle.close()` | `() => void` | PRV-API-21 |
| `handle.isOpen` | `readonly boolean` | PRV-API-21 |

- **PRV-API-21** — MUST. `createHandle()` returns a new independent handle (PSH-API-01). `open(triggerId)` opens the attached Root's card associated with the registered trigger `triggerId`, with reason `imperative-action` and that trigger element as `details.trigger` (PSH-STA-38); `close()` closes it with reason `imperative-action` and no trigger (PSH-API-04); `isOpen` is the attached Root's effective open state, `false` while none is attached (PSH-API-05). The type argument types every bound Trigger's `payload` and the Root function child's `payload`.
  Source: `preview-card/store/PreviewCardHandle.ts:13-55`; `utils/popups/popupHandle.ts:213-285`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens by trigger id with its payload and closes"; `PreviewCardRoot.spec.tsx` (`payload` typed `number | undefined`, invalid payload rejected).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. The type clause is L1 (`PreviewCardStoreTests`); L2 keeps the same title (`PreviewCardDetachedTriggersTests`).
- **PRV-API-22** — MUST. `handle.open(id)` with an id that matches no registered trigger (in any attached Root's registry or the fallback registry) MUST throw the error of PRV-EDGE-05 and leave the card closed; a Preview Card never opens without a trigger to anchor to.
  Source: `preview-card/store/PreviewCardHandle.ts:18` (`throwOnMissingTrigger` default `true`); `utils/popups/popupHandle.ts:241-249`. Test: [L4] `detachedTriggersConformanceTests.tsx` "throws when opened with an unregistered trigger id".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 keeps the same title with the port's message (`PreviewCardDetachedTriggersTests`).
- **PRV-API-23** — MUST. `open()`/`close()` called while no Root is attached to the handle are ignored (dev warning PRV-EDGE-06) and are not replayed when a Root attaches later; each Root mount starts from fresh closed state without payload (PSH-STA-40).
  Source: `utils/popups/popupHandle.ts:216-224,274-282`; `preview-card/root/PreviewCardRoot.tsx:34-46`. Test: [L4] `detachedTriggersConformanceTests.tsx` "ignores imperative calls while no root is attached and follows the root across remounts". Docs: `components/preview-card/page.mdx` §Detached triggers (lines 56-57); `CHANGELOG.md` v1.7.0 "Prevent `<PreviewCard.Root>` from reopening after remounting with a reused handle".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 keeps the same title (`PreviewCardDetachedTriggersTests`).

### 3.10 Shared types

* `PreviewCard.Root.ChangeEventReason` = `'trigger-hover' | 'trigger-focus' | 'trigger-press' | 'outside-press' | 'escape-key' | 'imperative-action' | 'none'` (`preview-card/root/PreviewCardRoot.tsx:191-198`). Which of these are actually emitted is specified in PRV-EVT-02/03.
* `PreviewCard.Root.ChangeEventDetails` = the change-event details of EVD §3.1 for that reason union plus `preventUnmountOnClose(): void` (EVD-API-09, TRN-API-06) (`PreviewCardRoot.tsx:200-204`).
* `PreviewCard.Root.Actions` = `{ unmount(): void; close(): void }` (`PreviewCardRoot.tsx:186-189`).
* `PreviewCard.Root.Props<Payload>`, `PreviewCard.Trigger.Props<Payload>` and the `.State`/`.Props` of every part are exported as listed in the outline (`components/page.mdx` "Preview Card" Types). `Side`, `Align`, `OffsetFunction`, `CollisionAvoidance`, `Boundary`, `Padding`, `VirtualElement` are the FLT §3.4 types reachable through `PreviewCard.Positioner.Props`.

## 4. State model

### 4.1 Store

The Root owns one popup store (PSH-STR-01) whose common fields and initial values are PSH §4.3.1. Preview Card adds three state fields and one context member:

| Field | Type | Initial | Meaning |
|---|---|---|---|
| `instantType` | `'dismiss' \| 'focus' \| undefined` | `undefined` | Instant marker exposed as `instant` on Positioner, Popup and Viewport (PRV-STA-10). |
| `adaptiveOrigin` | middleware \| `undefined` | `undefined` | Set while a Viewport is mounted (PRV-STA-15); switches the Positioner to edge-based coordinates (FLT-DOM-04). |
| `closeDelay` | `number` | `300` | Close delay used when the pointer leaves the card (PRV-STA-11). |
| context `inlineRectCoordsRef` | `{ x, y, lineIndex?, element } \| undefined` | `undefined` | Last hovered point on a multi-line trigger (PRV-STA-13). |

- **PRV-STA-01** — MUST. The store state is the PSH common state plus exactly `instantType`, `adaptiveOrigin` and `closeDelay` with the initial values above, and the store context is the PSH common context plus `inlineRectCoordsRef`. The inert fallback store used by detached triggers before a Root attaches has the same initial state (closed, `closeDelay 300`) and a live trigger registry (PSH-STA-37).
  Source: `preview-card/store/PreviewCardStore.ts:22-30,107-142`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L1 port title: `PreviewCardStoreTests` "port: the store starts closed with no instant type, the 300 ms close delay and no viewport".
- **PRV-STA-02** — MUST. Open state and active trigger id are each either consumer-owned (`open` / `triggerId` not `undefined`) or component-owned (`defaultOpen` / `defaultTriggerId`), following PSH-STA-02/03 and EVD-STA-01..06: a component-owned open state changes only through non-canceled requests (§7); a consumer-owned one changes only when the consumer changes the prop. Changing the prop never invokes `onOpenChange`.
  Source: `preview-card/root/PreviewCardRoot.tsx:34-49`; `utils/popups/store.ts:148-150`. Test: [L4] `PreviewCardRoot.test.tsx` "does not close after hovering out of a popup opened externally" (external open, `onOpenChange` is `setOpen`); `popupConformanceTests.tsx` "opens the popup with the `open` prop".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the external-open hover leave is the `interactions` module's, green in `tests/browser/preview-card.spec.ts`'s trio on both render modes; `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes.

### 4.2 Part state objects

- **PRV-STA-03** — MUST. The Root and the Portal have empty state objects (`{}`).
  Source: `preview-card/root/PreviewCardRoot.tsx:131`; `preview-card/portal/PreviewCardPortal.tsx:36`. Test: none.
- **PRV-STA-04** — MUST. The Trigger state is exactly `{ open }` where `open` is `isOpenedByTrigger(ownId)` (PSH-STA-08): `true` only while the card is open and this trigger is the active trigger. An inactive trigger of an open card has `open: false`.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:51,82`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active"; `PreviewCardRoot.detached-triggers.test.tsx` "when the popup moves to another trigger".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. "when the popup moves to another trigger" is green at L2 as a render-count title (`PreviewCardDetachedTriggersTests`).
- **PRV-STA-05** — MUST. The Positioner state is exactly `{ open, side, align, anchorHidden, instant }` with `side`/`align`/`anchorHidden` from the positioning session (FLT-DOM-08) and `instant` = store `instantType`. Server: relaxed, see §9.
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:56,88-94`. Test: [L4] `positionerConformanceTests.tsx` "reads $description inside the function" (`data-side`/`data-align` reflect the rendered values).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes; the mirrored side is asserted per mode in `tests/browser/preview-card.spec.ts` "port: the mirrored side follows the data-side the module writes" (§9).
- **PRV-STA-06** — MUST. The Popup state is exactly `{ open, side, align, instant, transitionStatus }`, with `side`/`align` read from the enclosing Positioner's session and `transitionStatus` the store's derived status (PSH-STA-05). Server: relaxed, see §9.
  Source: `preview-card/popup/PreviewCardPopup.tsx:28-32,51-57`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "opens immediately when entering trigger B during trigger A close transition" (`data-ending-style` on the popup).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes, the close transition lengthened from 50 ms to 1000 ms so a real pointer move lands inside it.
- **PRV-STA-07** — MUST. The Arrow state is exactly `{ open, side, align, uncentered }`. Server: relaxed, see §9.
  Source: `preview-card/arrow/PreviewCardArrow.tsx:27-32`. Test: none (describeConformance only).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 conformance (`PreviewCardConformanceTests`) and three L4 port titles on the arrow cases of `preview-card-positioner` ("port: the arrow is hidden from assistive technology and mirrors the resolved side", "… is marked uncentered …", "… is not marked uncentered …").
- **PRV-STA-08** — MUST. The Backdrop state is exactly `{ open, transitionStatus }`.
  Source: `preview-card/backdrop/PreviewCardBackdrop.tsx:26-29`. Test: none (describeConformance only).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 conformance (`PreviewCardConformanceTests`, the Backdrop) and `PreviewCardPopupPartsTests` "sets `pointer-events: none` style".
- **PRV-STA-09** — MUST. The Viewport state is exactly `{ activationDirection, transitioning, instant }` where the first two come from the shared viewport mechanism (PSH) and `instant` = store `instantType`.
  Source: `preview-card/viewport/PreviewCardViewport.tsx:26-38`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions" (`data-transitioning`, `data-activation-direction`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes.

### 4.3 Derived state

- **PRV-STA-10** — MUST. `instantType` is updated by every applied (non-canceled) open change per EVD-EVT-17: an open with reason `trigger-focus` sets `'focus'`; a close with reason `escape-key` (or the never-emitted `trigger-press`) sets `'dismiss'`; any change with reason `trigger-hover` sets `undefined`; changes with any other reason (`outside-press`, `trigger-focus` close, `imperative-action`, `none`) and consumer prop changes leave the previous value in place (see §16 Q4).
  Source: `utils/popups/popupStoreUtils.ts:265-269,296-302`; `preview-card/store/PreviewCardStore.ts:77`. Test: none (no Preview Card test asserts `data-instant`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L1 port titles: `PreviewCardStoreTests` "port: the instant type follows the reason table of EVD-EVT-17" and "port: a change with an unmapped reason keeps the stale instant type" (§16 Q4); L2: `PreviewCardRootTests` "port: a focus open renders data-instant="focus" and an Escape close renders dismiss on the popup and the positioner, but not the arrow".
- **PRV-STA-11** — MUST. The store's `closeDelay` is the `closeDelay` (after defaulting, PRV-API-11) of the active trigger: written when a trigger registers as, or is observed to be, the active trigger of an open or mounted card, and whenever the active trigger's `closeDelay` prop changes while it is mounted-by (PSH-STA-28/29/30). It is not reset on close, so a card opened without any trigger forwarding (controlled open with `triggerId: null`) keeps the last value (initially `300`).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:58,60-68`; `utils/popups/popupStoreUtils.ts:339-364,382-391`; `preview-card/popup/PreviewCardPopup.tsx:35`. Test: [L4] `PreviewCardRoot.test.tsx` "closes after hovering out of a popup opened by its trigger" (default), "should close after delay" (trigger `closeDelay: 100`, trigger leave path).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the Positioner-leave close and the trigger leave are the `interactions` module's, green in `tests/browser/preview-card.spec.ts`'s trio on both render modes. The forwarding is L1 (`PreviewCardStoreTests` "port: the forwarded close delay is written only when it changes and is not reset on close") and L2 (`PreviewCardTriggerTests`).
- **PRV-STA-12** — MUST. Post-commit, pre-paint of any commit in which the card is effectively open and no active trigger id is resolved, the store's `payload` is set to `undefined`; a function child therefore receives `payload: undefined` for a card opened without a trigger association.
  Source: `preview-card/root/PreviewCardRoot.tsx:64-70`. Test: none.
- **PRV-STA-13** — MUST. Hovered-line coordinates (`inlineRectCoordsRef`) are: (a) captured from `mouseenter`/`mousemove` on a trigger **only while that trigger is not the open card's active trigger** (`isOpenedByThisTrigger` false), as `{ x: clientX, y: clientY, lineIndex, element }` when the trigger has at least two line boxes, else cleared to `undefined`; (b) cleared on trigger `focus`; (c) captured at the moment a hover open with a trigger and a mouse event is applied, if the current coordinates are not already for that trigger; (d) cleared when the card subtree unmounts after a close. Consequently coordinates never change while the card stays open for the same trigger.
  Source: `utils/popups/inlineRect.ts:203-235`; `preview-card/trigger/PreviewCardTrigger.tsx:85-88,97`; `preview-card/store/PreviewCardStore.ts:75-98`; `preview-card/root/PreviewCardRoot.tsx:60-62`. Test: [L4] `PreviewCardPositioner.test.tsx` "uses the latest hovered line", "stays anchored to the opened line while already open", "clears hovered-line coords after close before controlled reopen of the same trigger", "clears hovered-line coords when opened via focus" (L4 for clause (d)). Docs: `CHANGELOG.md` v1.5.0 "Keep inline preview anchored while open".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L3] → [L4]: the cited titles measure layout; "uses the latest hovered line", "stays anchored to the opened line while already open" and "clears hovered-line coords when opened via focus" are green in `tests/browser/preview-card.spec.ts` on both render modes; "clears hovered-line coords after close before controlled reopen of the same trigger" is written and skipped, open to design point 11.2 (the hovered line cleared at the unmount). The recording is track 3's (`interactions.test.ts`, PSH-PTR-08/29) and the middleware L3 (`positioning.test.ts`, PSH-POS-03).
- **PRV-STA-14** — MUST. The Root enables close-on-active-trigger-unmount (PSH-STA-23/24/25): when the resolved active trigger unregisters while the card is open, a close with reason `none` is requested one microtask later unless a trigger with the same id re-registered in between (PRV-DIS-07). Server: relaxed, see §9.
  Source: `preview-card/root/PreviewCardRoot.tsx:59`; `utils/popups/popupStoreUtils.ts:524-546`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should close when the active trigger unmounts", "should close when the active detached trigger unmounts". Docs: `CHANGELOG.md` v1.5.0 "Close when active trigger unmounts".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardDetachedTriggersTests` "should close when the active trigger unmounts", "should close when the active detached trigger unmounts"; the close is C#'s, at the render-batch boundary) and per mode at L4 in `tests/browser/preview-card.spec.ts` "port: the trigger-unmount close lands a round trip after the trigger leaves on Server" (§9).
- **PRV-STA-15** — MUST. While a Viewport is mounted the store's `adaptiveOrigin` is the adaptive-origin middleware (FLT-ALG-12) and is cleared to `undefined` when the Viewport unmounts; the Positioner passes it to the positioning session.
  Source: `utils/usePopupViewport.tsx:112-117`; `preview-card/positioner/PreviewCardPositioner.tsx:57,77`. Test: [L4] `positionerConformanceTests.tsx` "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes.
- **PRV-STA-16** — MUST. The Root's interactions (the dismiss handlers that make up the trigger/popup attribute bags, PSH-STA-42) are mounted whenever the card is open, mounted, or the Root has a `handle` (PSH-STA-43); otherwise the bags are empty.
  Source: `preview-card/root/PreviewCardRoot.tsx:84,89,95-109`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardRootTests` "port: the session holds Escape and the outside press only while the card is open or mounted, or the root has a handle".
- **PRV-STA-17** — MUST. The attribute/handler bundle a trigger receives while it is neither the active trigger nor becoming one MUST be identical before and after the card opens, closes, or moves to another trigger (whether imperatively, by focus or by hover), so that bystander triggers are not rendered again: the element-substitution callback of bystander triggers is not invoked again; observed through bUnit render counts (L2).
  Source: `preview-card/root/PreviewCardRoot.tsx:81-84`; `preview-card/trigger/PreviewCardTrigger.tsx:84`; `utils/popups/store.ts:207-208`. Test: [L2] `PreviewCardRoot.detached-triggers.test.tsx` "when opened and closed imperatively", "when the popup moves to another trigger", "when focus opens and closes the popup", "when hover opens and closes the popup" (`bystander.renders === 0`). Docs: `CHANGELOG.md` v1.9.0 "Avoid re-rendering inactive triggers on open and close".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardDetachedTriggersTests` "port: does not re-render inactive triggers when $name" (four cases, 0 renders measured) and `PreviewCardRootTests` "port: an open and a close by hover render neither the root nor its content outside the popup".
- **PRV-STA-18** — MUST. A Root rendered with `defaultOpen` and a `defaultTriggerId` whose detached trigger registers only after the Root's first commit MUST stay open: the pending active trigger id is not treated as lost (PSH-STA-24), the trigger gets `data-popup-open` once it registers, `handle.isOpen` is `true`, the Popup has `data-open`, and `onOpenChange` is never called.
  Source: `utils/popups/popupStoreUtils.ts:430-431,484-490`; `preview-card/root/PreviewCardRoot.tsx:40,59`. Test: [L2] `PreviewCardRoot.detached-triggers.test.tsx` "keeps a default-open root open while a detached trigger migrates after the initial commit".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardDetachedTriggersTests` "keeps a default-open root open while a detached trigger migrates after the initial commit" (the trigger mounts in a later render).

### 4.4 Open/close state diagram

```
closed, unmounted ──(open request applied: hover timer, focus timer, handle.open, prop)──▶ open, mounted, status 'starting' (→ unset next frame)
open, mounted ──(close request applied)──▶ closed, mounted, status 'ending'
closed, mounted ──(popup animations finished, or actionsRef.unmount())──▶ closed, unmounted (status unset; onOpenChangeComplete(false))
closed, mounted ──(open request applied, e.g. re-hover during the exit)──▶ open, mounted (no 'starting'; 'ending' cleared next frame)
```

Mount gating, status timing and completion are TRN-STA-02/03/04/06/09/13 and TRN-ANI-03/18/19 as configured in §9. Invariants: at most one active trigger (PSH-STR-06); the card is never open without being mounted; `instant` and hovered-line coordinates are per-open-cycle data (PRV-STA-10/13).

## 5. Rendered DOM and ARIA

### 5.1 Trigger element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| `id` | the trigger id (consumer `id` or generated) | PRV-DOM-01 | C# |
| `data-popup-open` | `""` while the card is open and this trigger is active | PRV-DOM-02 | C# |
| consumer `href`, `tabindex`, `target`, … | forwarded unchanged | PRV-API-15 | C# |
| handlers | pointer-type recording, hover enter/leave, mouse move, focus/blur, keydown (Escape), pointerdown/click (no-op dismiss hooks) — see §6 | — | JS |

- **PRV-DOM-01** — MUST. The Trigger element's `id` attribute equals its trigger id (PRV-API-14); a consumer `id` is rendered verbatim, otherwise the generated id.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:49,98`. Test: [L2] `PreviewCardPositioner.test.tsx` "clears hovered-line coords after close before controlled reopen of the same trigger" (`eventDetails.trigger?.id === 'trigger'`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): the id is green at L2 (`PreviewCardTriggerTests` "port: renders an anchor …"); "clears hovered-line coords after close before controlled reopen of the same trigger" is written and skipped, open to design point 11.2 (the hovered line cleared at the unmount).
- **PRV-DOM-02** — MUST. `data-popup-open=""` is present on a Trigger exactly while its state `open` is `true` (PRV-STA-04) and absent otherwise; it disappears as soon as the card closes (the close request is applied), before the exit animation ends. No `data-pressed` is ever rendered (the mapping is the non-pressable trigger mapping).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:101`; `utils/popupStateMapping.ts:30-37`. Test: [L4] `PreviewCardRoot.test.tsx` "closes the preview card when the `close` method is called" (`not.toHaveAttribute('data-popup-open')`); `popupConformanceTests.tsx` "keeps the popup closed when the open change is canceled".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes. L2 keeps "closes the preview card when the `close` method is called" (`PreviewCardRootTests`) and adds "removes `data-popup-open` as soon as `open` becomes false" (`PreviewCardTriggerTests`).
- **PRV-DOM-03** — MUST. The Trigger renders no other state-derived attribute (no `data-disabled`, no `data-starting-style`/`data-ending-style`, no `data-trigger-disabled`), so every Preview Card trigger counts as an *enabled* trigger for the hover/focus/dismiss "moving to another trigger" checks (FLT `isTargetInsideEnabledTrigger`).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:82,101`; `floating-ui-react/utils/element.ts:10-31`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardTriggerTests` "port: renders an anchor …" (the trigger's attributes are exactly `data-popup-open`, `href` and `id`).

### 5.2 Portal node

- **PRV-DOM-04** — MUST. While rendered (PRV-STR-03) the Portal is a `<div id="<generated id>" data-base-ui-portal="">` appended to the resolved container (FLT-DOM-11, FLT-API-13), carrying the consumer's `className`/`style`/attributes, and containing the Portal's children. Preview Card uses the lite portal: no focus guards and no `aria-owns` owner are rendered around it (PSH-STR-11; contrast FLT-DOM-14).
  Source: `preview-card/portal/PreviewCardPortal.tsx:31`; `utils/FloatingPortalLite.tsx:15-38`; `floating-ui-react/components/FloatingPortal.tsx:49,125-141`. Test: [L2] `PreviewCardPortal.test.tsx` describeConformance (prop forwarding, `className`, `render` on the portal node) (L4 for the container-append clause).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardConformanceTests`, the Portal); the container-append clause is green in `tests/browser/preview-card.spec.ts` "uses the hovered line with a custom anchor in a clipped keepMounted portal".

### 5.3 Positioner element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"presentation"` | PRV-DOM-05 | C# |
| `hidden` | present while the card is not mounted (only observable with `keepMounted`) | PRV-DOM-06 | C# |
| inline `pointer-events: none` | while the card is not effectively open (closing, or kept mounted closed) | PRV-DOM-07 | C# → JS |
| inline `transition: none` | while `transitionStatus === 'starting'` | PRV-DOM-08 | C# → JS |
| `data-open`, `data-closed`, `data-anchor-hidden`, `data-side`, `data-align`, `data-instant` | per state (§3.5) | PRV-DOM-09 | C# (`data-open`, `data-closed`, `data-instant`); C# → JS (`data-side`, `data-align`); JS (`data-anchor-hidden`) |
| CSS variables | per §3.5 | PRV-DOM-10 | C# → JS |
| `position`, `top`/`left` or `transform`, `opacity` | positioning styles | PRV-DOM-11 | C# → JS |

- **PRV-DOM-05** — MUST. The Positioner renders `role="presentation"` (FLT-DOM-01).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:96`; `utils/usePositioner.tsx:36`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardPopupPartsTests` "port: the positioner renders role presentation and PreviewCard's default side, …".
- **PRV-DOM-06** — MUST. The Positioner carries the `hidden` attribute whenever the card is not mounted; with `keepMounted` it therefore appears hidden after the exit finishes and loses `hidden` when the card reopens (TRN-DOM-06).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:101`; `utils/usePositioner.tsx:36`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should reposition to a different trigger when reopened with keepMounted=true" (`toHaveAttribute('hidden')`); `PreviewCardPositioner.test.tsx` "clears hovered-line coords after close before controlled reopen of the same trigger".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: "should reposition to a different trigger when reopened with keepMounted=true" is browser-only, green on `preview-card-detached` on both render modes; "clears hovered-line coords after close before controlled reopen of the same trigger" is written and skipped, open to design point 11.2 (the hovered line cleared at the unmount). L2 adds "port: a kept-mounted card renders its closed positioner as hidden" (`PreviewCardPopupPartsTests`).
- **PRV-DOM-07** — MUST. The Positioner's inline style contains `pointer-events: none` whenever the effective open state is `false` (including the whole exit transition), and not otherwise; a closing card therefore cannot be re-entered with the pointer, and reopening during the exit happens only through the trigger (PRV-PTR-09).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:102`; `utils/usePositioner.tsx:28-30`. Test: none.
- **PRV-DOM-08** — MUST. While `transitionStatus === 'starting'` the Positioner's inline style contains `transition: none` (TRN-DOM-05, FLT-ANI-05); the Positioner carries no `data-starting-style`/`data-ending-style` (its state has no `transitionStatus`).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:98`; `utils/usePositioner.tsx:37`; `internals/getDisabledMountTransitionStyles.ts:5-9`. Test: none.
- **PRV-DOM-09** — MUST. The Positioner renders `data-open=""` while open and `data-closed=""` while closed (exactly one of the two), `data-anchor-hidden=""` iff `anchorHidden`, `data-side="<side>"` and `data-align="<align>"` with the rendered values, and `data-instant="<instant>"` iff `instant` is set (default projection of the `instant` field, RND-DOM-03; absent from the outline, §16 Q3).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:88-94,103`; `utils/popupStateMapping.ts:48-61`. Test: [L4] `PreviewCardPositioner.test.tsx` "uses the hovered line with a custom anchor in a clipped keepMounted portal" (`data-side="top"`); `positionerConformanceTests.tsx` "reads $description inside the function" (`data-side`/`data-align`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes; `data-instant` is C#'s, L2 (`PreviewCardRootTests` "port: a focus open renders data-instant=…").
- **PRV-DOM-10** — MUST. On every positioning pass while mounted the Positioner's inline style receives `--available-width`, `--available-height`, `--anchor-width`, `--anchor-height` and `--transform-origin` (FLT-DOM-05/06/07); `--available-*` are seeded to `100vw`/`100vh` before the first pass; `--positioner-width`/`--positioner-height` are written only by the Viewport's auto-resize (PSH) and are otherwise absent.
  Source: `internals/useAnchorPositioning.ts:360-380,447-450,534-535`; `utils/usePopupAutoResize.ts:223-224`. Test: [L4] `positionerConformanceTests.tsx` "rests exactly at collisionPadding from the colliding edge" (`max-height: var(--available-height)`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes.
- **PRV-DOM-11** — MUST. Until positioned, the Positioner is `position: fixed; top: 0; left: 0; opacity: 0` (FLT-DOM-02); once positioned it uses `position: <positionMethod>` with `transform: translate(<x>px, <y>px)` (FLT-DOM-03), or — while a Viewport is mounted — `top`/`bottom` and `left`/`right` edge coordinates chosen by the adaptive-origin middleware instead of a transform (FLT-DOM-04). Consumer `style` merges over these.
  Source: `internals/useAnchorPositioning.ts:505-541`; `preview-card/positioner/PreviewCardPositioner.tsx:97`. Test: [L4] `positionerConformanceTests.tsx` "uses transform positioning without Viewport", "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes.

### 5.4 Popup element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `tabindex` | `"-1"` (consumer `tabIndex` overrides) | PRV-DOM-12 | C# |
| `data-base-ui-focusable` | `""` | PRV-DOM-12 | C# |
| inline `transition: none` | while `'starting'` | PRV-DOM-14 | JS |
| `data-open`/`data-closed`, `data-starting-style`/`data-ending-style`, `data-side`, `data-align`, `data-instant` | per state (§3.6) | PRV-DOM-13 | C# (`data-open`, `data-closed`, `data-side`, `data-align`, `data-instant`); JS (`data-starting-style`, `data-ending-style`) |
| `role`, `id` | none unless supplied by the consumer | PRV-DOM-15 | C# |
| handlers | keydown (Escape) and inside-press markers from the dismiss mechanism (§6.4) | — | JS |

- **PRV-DOM-12** — MUST. The Popup renders `tabindex="-1"` and `data-base-ui-focusable=""` (the focusable-popup attributes, PSH §3.4): it can receive focus programmatically or by click but is not in the tab order.
  Source: `preview-card/popup/PreviewCardPopup.tsx:63`; `utils/popups/popupStoreUtils.ts:27-30`; `floating-ui-react/utils/constants.ts:1`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardRootTests` "port: the root renders no element, and the popup renders no role, a focusable tabindex, …".
- **PRV-DOM-13** — MUST. The Popup renders `data-open`/`data-closed` (exactly one), `data-starting-style`/`data-ending-style` per TRN-DOM-01/02/03, `data-side`/`data-align` with the Positioner's rendered values, and `data-instant` iff `instant` is set (§16 Q3). `data-anchor-hidden` is never rendered on the Popup (its state has no `anchorHidden`).
  Source: `preview-card/popup/PreviewCardPopup.tsx:51-57,68`; `utils/popupStateMapping.ts:63-70`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "still respects trigger B open delay after trigger A close transition finishes" (`data-ending-style`, `data-closed`, `data-open`); `popupConformanceTests.tsx` "is called on close once the exit animation finishes".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes (the close transition lengthened to 1000 ms) and in the popup suite.
- **PRV-DOM-14** — MUST. While `transitionStatus === 'starting'` the Popup's inline style contains `transition: none` (TRN-DOM-05).
  Source: `preview-card/popup/PreviewCardPopup.tsx:65`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardPopupPartsTests` "port: the popup renders transition: none while starting, and its transitions handle writes it from attach" (ADR 04 Amendments 2026-10-28, gap 13 of M4 track 5).
- **PRV-DOM-15** — MUST. The Popup renders no `role` and no generated `id`; only a consumer-supplied `id` appears (and is then the store's popup id, PSH-STA-04, which no Preview Card part references).
  Source: `preview-card/popup/PreviewCardPopup.tsx:59-69` (no `role`/`id` in the part-supplied props). Test: [L2] `popupConformanceTests.tsx` (configured without `expectedPopupRole`, so no role test runs).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardRootTests` "port: the root renders no element, and the popup renders no role, …" (no `role`, no generated `id`, no `aria-*`).

### 5.5 Arrow element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `aria-hidden` | `"true"` | PRV-ARIA-01 | C# |
| `data-open` / `data-closed` | open / closed | PRV-DOM-17 | C# |
| `style` | `position: absolute` + the arrow middleware's `top`/`left` + consumer style | PRV-DOM-16 | C# → JS |
| `data-side`, `data-align` | resolved placement | PRV-DOM-17 | C# → JS |
| `data-uncentered` | `""` iff the arrow cannot point at the anchor's center | PRV-DOM-17 | C# → JS |

Writer note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3): the table is added with the implementing pull request. As Tooltip's (§5.5 of tooltip.md), the Arrow has no twin of its own: its style and placement are written by the `positioning` module from the attach of the Positioner that carries its id, mirrored in `PreviewCardArrowState`.

- **PRV-DOM-16** — MUST. The Arrow's inline style is `position: absolute` plus `top`/`left` from the arrow middleware (FLT-DOM-09); consumer `style` merges over it. The Arrow element is the arrow registered with the positioning session (FLT-DOM-10, FLT-POS-08) with `arrowPadding` from the Positioner (default 5).
  Source: `preview-card/arrow/PreviewCardArrow.tsx:23,36-37`; `internals/useAnchorPositioning.ts:381-390,616-623`. Test: none (`PreviewCardArrow.test.tsx` describeConformance only).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardPopupPartsTests` "is hidden from assistive technology and mirrors the resolved side" (`position: absolute`, the arrow id sent to the module).
- **PRV-DOM-17** — MUST. The Arrow renders `data-open`/`data-closed` (exactly one), `data-side`, `data-align`, and `data-uncentered=""` iff `uncentered` (default projection of a boolean field); no `data-anchor-hidden` (not in its state).
  Source: `preview-card/arrow/PreviewCardArrow.tsx:27-32,38`; `utils/popupStateMapping.ts:48-61`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L4 port titles on `preview-card-positioner`'s arrow cases in `tests/browser/preview-card.spec.ts`, on both render modes.

### 5.6 Backdrop element

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `role` | `"presentation"` | PRV-DOM-18 | C# |
| `hidden` | not mounted | PRV-DOM-18 | C# |
| inline `pointer-events`, `user-select`, `-webkit-user-select` | always `none` | PRV-DOM-18 | C# |
| `data-open` / `data-closed` | open / closed | PRV-DOM-19 | C# |
| `data-starting-style` / `data-ending-style` | transition status | PRV-DOM-19 | JS |

Writer note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3): the table is added with the implementing pull request; the transition attributes are the `transitions` module's, mirrored in `PreviewCardBackdropState`, as Popover's Backdrop's are.

- **PRV-DOM-18** — MUST. The Backdrop renders `role="presentation"`, the `hidden` attribute while the card is not mounted, and the inline styles `pointer-events: none`, `user-select: none`, `-webkit-user-select: none` (consumer `style` merges over them). It is never interactive and never blocks presses or hover.
  Source: `preview-card/backdrop/PreviewCardBackdrop.tsx:34-43`. Test: [L2] `PreviewCardBackdrop.test.tsx` "sets `pointer-events: none` style". Docs: `CHANGELOG.md` v1.0.0-alpha.7 "Set `pointer-events: none` style on backdrops when hoverable".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardPopupPartsTests` "sets `pointer-events: none` style".
- **PRV-DOM-19** — MUST. The Backdrop renders `data-open`/`data-closed` (exactly one) and `data-starting-style`/`data-ending-style` per TRN-DOM-01/02/03; it never renders `data-anchor-hidden`, `data-side` or `data-align`.
  Source: `preview-card/backdrop/PreviewCardBackdrop.tsx:26-29,46`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2: `PreviewCardPopupPartsTests` "sets `pointer-events: none` style" also asserts `data-open` and no `data-side`.

### 5.7 Viewport element and its containers

| Attribute / style | Value / present when | Req | Writer |
|---|---|---|---|
| `[data-current]` container | always, keyed by the active trigger and the late payload | PRV-DOM-20 | C# |
| `[data-previous]` container | during a content transition | PRV-DOM-20 | JS |
| `data-activation-direction` | while a direction exists | PRV-DOM-21 | JS |
| `data-transitioning` | `[data-previous]` rendered | PRV-DOM-21 | JS |
| `data-instant` | `focus` \| `dismiss` when defined | PRV-DOM-21 | C# |
| `--popup-width` / `--popup-height` on `[data-previous]` | the previous content's popup size | PRV-DOM-22 | JS |

Writer note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27): the Viewport is Popover's and Tooltip's over the card's store (`PreviewCardViewport`, track 5's `popup` module). C# renders `[data-current]`; the `popup` module writes the previous container, the direction and `data-transitioning`, mirrored in `PreviewCardViewportState`.

- **PRV-DOM-20** — MUST. The Viewport renders its content inside `<div data-current>`; while a content transition is in progress (PRV-ANI-10) it renders, before it, `<div data-previous inert style="position: absolute; --popup-width: <w>px; --popup-height: <h>px">` containing a DOM clone of the previous content, with `data-ending-style=""` on the previous container and `data-starting-style=""` on the current container for the first frame (then swapped per PSH-STR-07 / `usePopupViewport`). The current container is remounted (new element) when the active trigger changes.
  Source: `utils/usePopupViewport.tsx:229-268`; `preview-card/viewport/PreviewCardViewport.tsx:28-32,43`. Test: [L4] `viewportConformanceTests.tsx` "renders children in the `current` container by default", "remounts the `current` container when the active trigger changes", "creates morphing containers during transitions", "keeps the latest transition active during rapid trigger changes" (L4 for the in-transition `[data-previous]` clause).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes.
- **PRV-DOM-21** — MUST. The Viewport element renders `data-activation-direction="<horizontal> <vertical>"` (tokens `left`/`right` and `up`/`down`, either possibly empty, PSH `getActivationDirection` with a 5 px tolerance) only after a trigger switch while open, `data-transitioning=""` while the previous container exists, and `data-instant="<instant>"` iff `instant` is set.
  Source: `utils/usePopupViewport.tsx:21-30,291-294,310-316`; `preview-card/viewport/PreviewCardViewport.tsx:34-38,44`. Test: [L4] `viewportConformanceTests.tsx` "sets `data-activation-direction` from the previous to the new trigger" (`['left','up']`), "creates morphing containers during transitions" (`['right','down']`, `data-transitioning`). Docs: `components/preview-card/page.mdx` §Content (lines 174-183).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes.
- **PRV-DOM-22** — MUST. `--popup-width`/`--popup-height` are set on the `[data-previous]` container only, with the popup's measured size at the time the previous content was rendered (PSH auto-resize); they are absent otherwise.
  Source: `utils/usePopupViewport.tsx:244-254`; `preview-card/viewport/PreviewCardViewportCssVars.ts:6,12`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions" (`--popup-width`/`--popup-height` match `/^\d+(?:\.\d+)?px$/`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes.

### 5.8 ARIA

Preview Card emits **no** ARIA relationship between the trigger and the card.

- **PRV-ARIA-01** — MUST. The Arrow renders `aria-hidden="true"`.
  Source: `preview-card/arrow/PreviewCardArrow.tsx:37`. Test: none. Docs: `CHANGELOG.md` v1.0.0-alpha.5 "Apply `aria-hidden` to `Arrow` parts".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port title: `PreviewCardPopupPartsTests` "is hidden from assistive technology and mirrors the resolved side".
- **PRV-ARIA-02** — MUST. The Trigger renders no `aria-expanded`, `aria-controls`, `aria-haspopup`, `aria-describedby` or `role`: its accessible role is whatever the rendered `<a>` provides (`link` when `href` is set). The detached-triggers suite is run with `ariaExpanded: false` for this reason.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:90-102`; `PreviewCardRoot.detached-triggers.test.tsx:47`. Test: [L4] `detachedTriggersConformanceTests.tsx` "opens with any trigger on $interaction and marks only that trigger as active" (no `aria-*` assertions in this configuration). Docs: `components/preview-card/page.mdx` §Usage guidelines.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 adds `PreviewCardTriggerTests` "port: renders an anchor …" (no `aria-*`, no `role`).
- **PRV-ARIA-03** — MUST. The Popup renders no `role` and no `aria-*` attributes, and the Positioner/Backdrop are `role="presentation"`; the card's content is therefore reachable by assistive technology only as ordinary document content inside the portal node, with no announced relationship to the link (Tooltip, by contrast, has no `role` either but describes a `<button>`; Popover renders a labelled `role="dialog"`).
  Source: `preview-card/popup/PreviewCardPopup.tsx:59-69`; `utils/usePositioner.tsx:36`; `preview-card/backdrop/PreviewCardBackdrop.tsx:36`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L2 port titles: `PreviewCardRootTests` "port: the root renders no element, and the popup renders no role, …" and `PreviewCardPopupPartsTests` (the Positioner's and the Backdrop's `role="presentation"`).

## 6. Interactions

Preview Card has no keyboard or pointer logic of its own; it configures three shared interactions and the shared dismissal mechanism (§11): the trigger-side hover interaction (`mouseOnly`, no `move`, safe polygon, per-trigger delays), the popup-side hover interaction (store `closeDelay`), the focus interaction (`delay`), and dismissal with default options (`escapeKey: true`, `outsidePress: true`, `outsidePressEvent: 'sloppy'`, no `referencePress`, no `bubbles`). The requirements below state the resulting observable behavior with the component's values; the algorithms are FLT's.

### 6.1 Keyboard

| Key | Context | Behavior | Req |
|---|---|---|---|
| <kbd>Tab</kbd> / <kbd>Shift+Tab</kbd> onto a Trigger | focus arrives with `:focus-visible` | Opens after `delay` ms (reason `trigger-focus`), or immediately when `delay` is `0` or when the card is open from another trigger | PRV-KEY-01, PRV-KEY-02 |
| <kbd>Tab</kbd> / <kbd>Shift+Tab</kbd> away from a Trigger | card open or opening | Not intercepted; the resulting blur closes the card (reason `trigger-focus`) unless focus lands in the card or on another trigger | PRV-KEY-03, PRV-DIS-06 |
| <kbd>Escape</kbd> | document, Trigger or Popup keydown while open | Closes (reason `escape-key`); innermost open card only; `preventDefault` unless canceled | PRV-DIS-01, PRV-DIS-02 |
| focus again after an Escape close | same Trigger still focused | Ignored until the trigger blurs or the pointer leaves it; focusing another trigger opens | PRV-KEY-04 |
| <kbd>Enter</kbd> / <kbd>Space</kbd> on a Trigger | — | Not intercepted; native `<a>` activation (navigation) | PRV-KEY-05 |
| any key inside the Popup | — | Only Escape is handled; the Popup is not in the tab order (PRV-DOM-12) | — |

- **PRV-KEY-01** — MUST. When a Trigger receives focus and the focus is *visible* (PRV-A11Y-01) and not blocked (PRV-KEY-04, PRV-FOC-03), the card opens with reason `trigger-focus` and that trigger as `details.trigger`: synchronously when `delay` is `0`, otherwise after `delay` ms (default 600) provided focus was not blocked in the meantime. The timer is per trigger and is cleared on blur.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:80`; `floating-ui-react/hooks/useFocus.ts:126-184` (FLT-FOC-01, FLT-API-15). Test: [L4] `PreviewCardRoot.test.tsx` "should open when the trigger is focused" (`OPEN_DELAY`); `PreviewCardRoot.detached-triggers.test.tsx` "should open again after escape when focusing another trigger" (`delay={0}`); `viewportConformanceTests.tsx` (all tests, `openOn: 'focus'`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes: the trio's focus title (Base UI returns early in a browser, where its synthetic focus is not `:focus-visible`; Playwright's focus is) and the viewport suite; "should open again after escape when focusing another trigger" is green at L2 (`PreviewCardDetachedTriggersTests`).
- **PRV-KEY-02** — MUST. When the card is already open and focus moves from one Trigger directly to another Trigger of the same card (`relatedTarget` inside an enabled trigger), the second trigger opens the card **immediately**, ignoring its own `delay`, with reason `trigger-focus`; the card does not close in between, and its payload/anchor switch to the new trigger.
  Source: `floating-ui-react/hooks/useFocus.ts:151-168`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should switch immediately when focusing another trigger while open" (trigger 2 has `delay={2000}`); `detachedTriggersConformanceTests.tsx` "reuses the popup DOM nodes when switching triggers" (focus switch).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): the suite's title is green on both render modes; "should switch immediately when focusing another trigger while open" is green at L2 (`PreviewCardDetachedTriggersTests`).
- **PRV-KEY-03** — MUST. <kbd>Tab</kbd> is never intercepted by any part: there is no focus trap, no focus guard and no roving tabindex; the card's content is left out of the tab sequence unless the consumer makes elements inside it tabbable. Leaving the trigger by keyboard closes the card through the blur rule (PRV-DIS-06).
  Source: `preview-card/popup/PreviewCardPopup.tsx:59-69` (no focus manager); `preview-card/portal/PreviewCardPortal.tsx:31` (lite portal). Test: [L4] `PreviewCardRoot.test.tsx` "should close when the trigger is blurred". Docs: `components/preview-card/page.mdx` §Usage guidelines ("not touch, keyboard or screen reader navigable").
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes.
- **PRV-KEY-04** — MUST. After the card closes with reason `escape-key` (or `trigger-press`, never emitted) the Trigger that was the anchor is *blocked*: a `focus` event on that same element does not reopen the card (and a pending focus-open timer that fires while blocked does nothing) until the element blurs or the pointer leaves it; a `focus` on a different trigger clears the block and opens normally.
  Source: `floating-ui-react/hooks/useFocus.ts:95-114,123-135,170-173,185-186` (FLT-FOC-03). Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should open again after escape when focusing another trigger".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardDetachedTriggersTests` "should open again after escape when focusing another trigger".
- **PRV-KEY-05** — MUST. <kbd>Enter</kbd>/<kbd>Space</kbd> on the Trigger are not handled: no key handler other than the Escape handler is attached, so a native link navigates and a non-link `<a>` does nothing; neither opens nor closes the card.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:93-99` (handlers: hover, focus, dismiss reference, inline-rect); `floating-ui-react/hooks/useDismiss.ts:775-782`. Test: none.

### 6.2 Pointer and touch

| Gesture | Context | Behavior | Req |
|---|---|---|---|
| pointer enters a Trigger | mouse/pen (or unknown pointer type), card closed | Open timer `delay` ms → open (`trigger-hover`); `delay: 0` → synchronous | PRV-PTR-01 |
| pointer enters a Trigger | touch pointer | Nothing | PRV-PTR-02 |
| pointer leaves the Trigger before the timer fires | card closed | Pending open cancelled | PRV-PTR-03 |
| pointer leaves the active Trigger | card open (hover-opened) | Safe-polygon tracking; close `closeDelay` ms after the pointer is judged to have left toward anywhere but the card (`trigger-hover`) | PRV-PTR-04 |
| pointer re-enters the Trigger | within `closeDelay` | Pending close cancelled; no callback | PRV-PTR-05 |
| pointer leaves the Positioner | card hover-opened | Close after store `closeDelay` (`trigger-hover`) | PRV-PTR-06 |
| pointer leaves the Positioner | card opened by focus, prop, `defaultOpen` or handle | Nothing | PRV-PTR-06 |
| pointer enters the Positioner | pending close | Close cancelled | PRV-PTR-07 |
| pointer enters another Trigger of the same card | card open | Opens on that trigger immediately (no delay), content/anchor switch | PRV-PTR-08 |
| pointer enters a Trigger during the hover-close exit transition | status `ending` after a hover close | Reopens immediately; after the exit finished, normal `delay` again | PRV-PTR-09 |
| click on the Trigger | any | Not intercepted: native navigation; no open/close | PRV-PTR-11 |
| mouse move over a multi-line Trigger | card closed | Records the hovered line for anchoring | PRV-PTR-12 |
| press outside | card open | Close (`outside-press`) per §6.4 | PRV-DIS-03..05 |

- **PRV-PTR-01** — MUST. A `mouseenter` on a Trigger whose last recorded pointer type is mouse-like (`mouse`, `pen`, `''` or none recorded; FLT `isMouseLikePointerType`) starts the trigger's open timer: after `delay` ms (default 600) the card opens with reason `trigger-hover`, the `mouseenter` event, and the trigger element, provided the card is still closed (or the trigger is not the active one). With `delay: 0` the open is requested synchronously in the `mouseenter` handler. `mousemove` alone never opens (`restMs` is 0 and `move` is off).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:70-78` (`mouseOnly: true`, `move: false`, `delay()`); `floating-ui-react/hooks/useHoverReferenceInteraction.ts:216-300,385-389,453-456` (FLT-PTR-12). Test: [L4] `PreviewCardRoot.test.tsx` "should open when the trigger is hovered", "should open after delay with rest type by default" (not open before 100 ms, open after); `popupConformanceTests.tsx` "is called with the new open state and the change details" (`delay: 0`, reason `trigger-hover`, `trigger`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes under a paused clock, and in the popup suite.
- **PRV-PTR-02** — MUST. The pointer type is recorded on every `pointerdown`/`pointerenter` of a Trigger; when it is `touch`, `mouseenter` and `mousemove` on the trigger do nothing (FLT-PTR-11). A touch user therefore cannot open the card by touching the link (a tap focuses the link without `:focus-visible`, so PRV-KEY-01 does not fire either).
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:219-222,419-425,434-436`. Test: none. Docs: `components/preview-card/page.mdx` §Usage guidelines.
- **PRV-PTR-03** — MUST. A `mouseleave` of the Trigger while the card is closed cancels its pending open timer (the shared open/close timer is cleared or replaced), so a quick pass over the link never opens the card.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:302-360` (FLT-PTR-14, `openChangeTimeout.clear()` at 323, or replacement by the close timer at 197). Test: none (see §16 Q5 for a side effect of this path).
- **PRV-PTR-04** — MUST. A `mouseleave` of the active Trigger while the card is hover-open (and `relatedTarget` is not another trigger of the card) starts safe-polygon tracking (FLT-ALG-15) with the leave point as the polygon apex, the Positioner as the floating element and the trigger as the reference: document `mousemove`s inside the polygon or the trough between trigger and card keep it open; once the pointer is judged to have left (outside the polygon, slow movement outside the rect, 40 ms without landing, …) the card closes **after `closeDelay` ms** (default 300) with reason `trigger-hover` and the leave event. In a layout where the leave point is not between trigger and card (e.g. leaving away from the card) the close is decided on the `mouseleave` itself and the delay starts immediately.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:73-74` (`handleClose: safePolygon()`); `floating-ui-react/hooks/useHoverReferenceInteraction.ts:194-206,321-350`; `floating-ui-react/safePolygon.ts:90-451`. Test: [L4] `PreviewCardRoot.test.tsx` "should close when the trigger is unhovered", "should close after delay" (`closeDelay: 100`: still open right after leave, closed after 100 ms), "closes when the trigger is unhovered after opening by default".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes under a paused clock.
- **PRV-PTR-05** — MUST. A `mouseenter` of the Trigger while a close is pending (within `closeDelay`) cancels the close; because the card is still open for this trigger no open request is issued and `onOpenChange` is not called again.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:217,274,289-299`. Test: [L4] `PreviewCardRoot.test.tsx` "does not call onOpenChange again when the trigger is re-hovered while open".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes.
- **PRV-PTR-06** — MUST. A `mouseleave` of the **Positioner** element (the floating element; the Popup is inside it) closes the card after the store's `closeDelay` ms (PRV-STA-11) with reason `trigger-hover` **only if the card was opened by a hover** (the recorded open event is a mouse event other than `mousedown`, FLT-STA-05) and the user has not pressed a nested-popup trigger inside it (FLT-STA-06); a card opened by focus, by the `open` prop, by `defaultOpen`, by a handle or by `actionsRef` is not closed by leaving it. Leaving toward a trigger of the card, into a nested open card, or while a safe-polygon handler is active defers to those paths instead.
  Source: `preview-card/popup/PreviewCardPopup.tsx:47-49`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:165-178,197-233,250`; `floating-ui-react/components/FloatingRootStore.ts:91-101`; `floating-ui-react/hooks/useHoverShared.ts:66-72` (FLT-PTR-24). Test: [L4] `PreviewCardRoot.test.tsx` "closes after hovering out of a popup opened by its trigger", "does not close after hovering out of a popup opened externally", "does not close after hovering out of a popup opened without trigger hover". Docs: `CHANGELOG.md` v1.5.0 "Fix controlled hover leave close".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes. With two detached triggers and no trigger id the externally opened card has no anchor and stays unpositioned (FLT-STA-09), so that case checks the content only, as Base UI's title does.
- **PRV-PTR-07** — MUST. A `mouseenter` of the Positioner cancels any pending hover close (from the trigger or the card) and any pending nested-close timer.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:190-195` (FLT-PTR-23). Test: [L4] `PreviewCardRoot.test.tsx` "keeps the parent open and re-opens the child when re-entering after partial close" (re-entering the parent positioner at `CLOSE_DELAY / 2`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes (`preview-card-nested`, each card below its trigger so the parent's own content stays uncovered).
- **PRV-PTR-08** — MUST. While the card is open, a `mouseenter` on a different Trigger of the same card (contained or detached) opens the card on that trigger **immediately**, ignoring that trigger's `delay`, with reason `trigger-hover` and the new trigger element; the card does not close in between, the active trigger, anchor and payload switch, and the Popup/Positioner elements are reused.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:254-283` (`isOverInactive && isOpen` → immediate); `preview-card/trigger/PreviewCardTrigger.tsx:76` (`isActiveTrigger`). Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should open the preview card immediately when hovering another trigger" (trigger 2 `delay={2000}`), "when hover opens and closes the popup"; `detachedTriggersConformanceTests.tsx` "sets the payload of the active trigger and renders content based on it" (hover).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes (`preview-card-detached`, `case=multiple-root`) and in the detached suite.
- **PRV-PTR-09** — MUST. While the card is closed but still mounted with `transitionStatus === 'ending'` **because of a hover close**, a `mouseenter` on any Trigger of the card (the same one or another) reopens it immediately, without delay, with reason `trigger-hover`; the Popup goes back to `data-open` without `data-closed` and without a new `'starting'` phase (TRN-STA-09). Once the exit has completed (status unset, `data-closed` only), a new hover honors the trigger's full `delay` again. The "is closing" test uses the store's transition status (`isClosing` option).
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:77` (`isClosing`); `floating-ui-react/hooks/useHoverReferenceInteraction.ts:170-181,258-283` (FLT-PTR-12/13). Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "opens immediately when entering trigger B during trigger A close transition", "still respects trigger B open delay after trigger A close transition finishes", "reopens immediately when re-hovering trigger A during its close transition", "respects open delay on later same-trigger hovers after close lifecycle finishes"; `PreviewCardRoot.test.tsx` "reopens on hover after Escape closes it" (after a non-hover close the normal delay applies). Docs: `CHANGELOG.md` v1.3.0 "Fix hover reopen race conditions across close transitions".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): the four titles are green in `tests/browser/preview-card.spec.ts` on both render modes, the close transition lengthened from 50 ms to 1000 ms and its start awaited on animation frames.
- **PRV-PTR-10** — MUST. An open or close with reason `trigger-hover` is committed synchronously inside the handler (EVD-EVT-18), so a consumer or test inspecting the DOM right after the `mouseenter`/timer callback sees the new `data-open`/`data-closed`. Server: relaxed, see §9.
  Source: `utils/popups/popupStoreUtils.ts:307-312`. Test: [L4] `PreviewCardRoot.test.tsx` "should close after delay" (synchronous assertions after `tick`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes; the Server relaxation is asserted per mode ("port: a hover open commits a round trip after the hover on Server and with none on WebAssembly", §9).
- **PRV-PTR-11** — MUST. A click (mouse, touch or keyboard-generated) on the Trigger is not handled by the component: the dismiss hooks attached to the trigger are inert (`referencePress` is never true), no handler calls `preventDefault`, and a mouse click's focus does not open the card because it is not `:focus-visible` (PRV-A11Y-01). The link navigates natively; the card neither opens nor closes on click (a click on the active trigger while the card is open is also not an outside press, FLT-DIS-05).
  Source: `preview-card/root/PreviewCardRoot.tsx:98,102-106`; `floating-ui-react/hooks/useDismiss.ts:122,187-199,775-782` (FLT-PTR-30, FLT-API-07). Test: [L4] `PreviewCardRoot.test.tsx` "keeps the parent preview card open when clicking nested trigger" (`fireEvent.click` on a trigger inside the parent popup: parent stays open).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes. The fixture's triggers carry no `href`: Base UI's `"#"` resolves against the host's `<base href="/">`, and the hash navigation a click starts re-renders the fixture.
- **PRV-PTR-12** — MUST. On `mouseenter` and every `mousemove` over a Trigger while that trigger is not the open card's active trigger, the hovered point is recorded (PRV-STA-13(a)) so that, when the delayed open fires, the card anchors to the **line box under the latest recorded point** of a multi-line inline trigger (PRV-POS-03), not to the first line or the bounding box; the capture is skipped when the trigger has fewer than two line boxes.
  Source: `utils/popups/inlineRect.ts:110-129,203-224`; `preview-card/trigger/PreviewCardTrigger.tsx:85-88`. Test: [L4] `PreviewCardPositioner.test.tsx` "positions the popup relative to the hovered line of a multiline trigger", "uses the latest hovered line", "keeps the popup aligned after page scroll".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes; the triggers carry `pointer-events: none` and the titles dispatch `mouseenter`/`mousemove` with client coordinates, as Base UI's do.
- **PRV-PTR-13** — MUST. While the card is open for a trigger, pointer movement over the trigger's other lines does not re-anchor it (coordinates are frozen, PRV-STA-13) even when positioning re-runs (resize, scroll); re-entering a different line *during the hover-close exit transition* (PRV-PTR-09) re-anchors the reopened card to the newly entered line, because the card is closed at that moment and the coordinates are captured again.
  Source: `utils/popups/inlineRect.ts:211-215`; `preview-card/store/PreviewCardStore.ts:81-96`. Test: [L4] `PreviewCardPositioner.test.tsx` "stays anchored to the opened line while already open", "re-anchors to a newly entered line while reopening during close transition".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): "stays anchored to the opened line while already open" is green in `tests/browser/preview-card.spec.ts` on both render modes (the module's offset functions cannot count passes, so the title waits two frames after the resize); "re-anchors to a newly entered line while reopening during close transition" is green too, through `PositioningOptions.PositionOnOpen`, which `PreviewCardPositioner` sends so the handle runs one pass when the card reopens while still mounted (design point 11.5, ADR 04 Amendments 2026-10-29; L3 `positioning.test.ts` "port: with positionOnOpen, a reopen while still mounted runs one pass before paint").
- **PRV-PTR-14** — MUST. When the card opens with no recorded hovered point for its anchor (focus open, controlled/`defaultOpen` open, `handle.open()`, or a controlled trigger switch that reuses the popup), a multi-line anchor is reduced to the **side-aligned line**: for `side` `bottom` the last line box (spanning from the first line's top to the last line's bottom horizontally limited to the last line), for `top` the first line box, for `left`/`right` the lines sharing the outermost edge on that side (FLT inline-rect selection, `getInlineReferenceRect`). Stale coordinates recorded for a *different* trigger element are ignored.
  Source: `utils/popups/inlineRect.ts:131-193,237-292`. Test: [L4] `PreviewCardPositioner.test.tsx` "positions the popup relative to the side-aligned rect when open is controlled", "positions the popup relative to the side-aligned rect when opened via focus", "clears hovered-line coords when opened via focus", "ignores stale hovered coords when a controlled trigger switch reuses the popup", "clears hovered-line coords after close before controlled reopen of the same trigger".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes; the no-point reduction follows the pinned source (PSH-ALG-16), design point 11.1 open on ADR 04's reading.
- **PRV-PTR-15** — MUST. Safe-polygon pointer-events blocking is **off** (`safePolygon()` with default options, `blockPointerEvents: false`): the component never writes `pointer-events` on the document body, the trigger or the card while hovering (contrast FLT-DOM-19); only the Positioner's own `pointer-events: none` while closed (PRV-DOM-07) applies.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:73`; `floating-ui-react/safePolygon.ts:91,445-448`; `floating-ui-react/hooks/useHoverFloatingInteraction.ts:99-105` (guard on `blockPointerEvents`). Test: none.
- **PRV-PTR-16** — MUST. A `pointerdown` inside the Positioner on an interactive element that is (or is inside) an element with `aria-haspopup` marks the card as "interacted inside": from then until it closes, leaving the card or the trigger with the pointer does not close it (FLT-STA-06, FLT-PTR-22); pressing a non-interactive element clears the mark.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:180-188,251`; `floating-ui-react/hooks/useHoverReferenceInteraction.ts:108-110,303-306`. Test: none.

### 6.3 Focus management

- **PRV-FOC-01** — MUST. Opening or closing the card never moves focus: no element is focused on open, there is no focus trap or `aria-hidden`/`inert` marking of the rest of the page, and nothing is refocused on close. The active element stays wherever the user put it (normally the trigger link).
  Source: `preview-card/popup/PreviewCardPopup.tsx:59-69`, `preview-card/positioner/PreviewCardPositioner.tsx:105-109` (no focus manager anywhere in `preview-card/`). Test: none (no Preview Card test asserts `document.activeElement`; the detached-triggers suite skips its focus-return check because no `Close` part is given).
- **PRV-FOC-02** — MUST. The Popup is programmatically focusable (`tabindex="-1"`, PRV-DOM-12); when focus moves from the trigger into the card (Popup or a focusable descendant), the trigger's blur does not close the card (PRV-DIS-06). Focus leaving the card to the outside is not observed by the component and does not close it (closing on `focusout` of the popup exists only in the focus manager, which Preview Card does not use).
  Source: `floating-ui-react/hooks/useFocus.ts:215-221`. Test: none.
- **PRV-FOC-03** — MUST. A window `blur` observed while the card is closed and the DOM anchor (active trigger) is the document's active element blocks the next `focus` on that element (so returning to the tab does not open the card); the block is lifted by a `mouseleave` or `blur` of the trigger or by focusing another trigger (FLT-FOC-02).
  Source: `floating-ui-react/hooks/useFocus.ts:56-93,117-135`. Test: none.
- **PRV-A11Y-01** — MUST. Focus opens the card only when the focus is *visible*: the focused element matches `:focus-visible` (so a mouse click that focuses the link does not open it, while keyboard focus does); in environments where `:focus-visible` cannot be evaluated the focus counts as visible; on Mac Safari with focus arriving from outside the document (`relatedTarget` null) the component instead uses its own modality tracking (last input was a key, or the target is typeable) (FLT-A11Y-06).
  Source: `floating-ui-react/hooks/useFocus.ts:23,80-91,137-149`; `floating-ui-react/utils/element.ts:71-82`. Test: [L4] `PreviewCardRoot.test.tsx` "should open when the trigger is focused" (JSDOM only, where `:focus-visible` is assumed); `PreviewCardPositioner.test.tsx` "positions the popup relative to the side-aligned rect when opened via focus" (`user.tab()` in a browser).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes.

### 6.4 Dismissal

Dismissal is the shared mechanism with its default options (FLT-API-01/02: `bubbles.escapeKey = false`, `bubbles.outsidePress = true`, `outsidePressEvent = 'sloppy'`). It is active only while the card is effectively open (document listeners) and, for the keydown handlers, while the Root's interactions are mounted (PRV-STA-16).

- **PRV-DIS-01** — MUST. While open, an <kbd>Escape</kbd> keydown observed on the owner document, on a Trigger, or on the Popup requests a close with reason `escape-key` and the keyboard event (no trigger); unless the request is canceled, `preventDefault()` is called on the keydown; unless `allowPropagation()` was called, `stopPropagation()` is called (EVD-DIS-01, EVD-EVT-11, FLT-KEY-01). Escape during an IME composition is ignored (FLT-KEY-03). After an Escape close the card can be reopened by hover with the normal delay (and by focus of a *different* trigger, PRV-KEY-04). Server: relaxed, see §9.
  Source: `preview-card/root/PreviewCardRoot.tsx:98`; `floating-ui-react/hooks/useDismiss.ts:201-232,718-724,775-790`; `utils/popups/popupStoreUtils.ts:268-269,298-299` (`instantType 'dismiss'`). Test: [L4] `popupConformanceTests.tsx` "is called with the new open state and the change details" (reason `escape-key`), "opens initially and remains uncontrolled", "is ignored when `open={true}` is passed, so Escape cannot close"; `PreviewCardRoot.test.tsx` "reopens on hover after Escape closes it"; `PreviewCardRoot.detached-triggers.test.tsx` "should open again after escape when focusing another trigger".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes; the cancel relaxation is asserted per mode ("port: a canceled Escape close keeps the card open on both modes and keeps the key's default only on WebAssembly", §9).
- **PRV-DIS-02** — MUST. With nested cards, an <kbd>Escape</kbd> closes only the innermost open card: an outer card whose floating-tree node has an open descendant does not request a close (FLT-KEY-02, EVD-DIS-02); the next Escape closes the outer one.
  Source: `floating-ui-react/hooks/useDismiss.ts:169-178,216-218`; `preview-card/root/PreviewCardRoot.tsx:120-128`. Test: none (behavior exercised by the shared Popover/Menu suites; nested Preview Card tests cover press and hover only).
- **PRV-DIS-03** — MUST. While open, a `pointerdown` of a non-touch pointer outside the card's own elements (Positioner subtree and active trigger) and outside every registered trigger closes the card immediately with reason `outside-press` and the pointer event, subject to the exclusions of PRV-DIS-05 (FLT-PTR-26, FLT-ALG-17). A `click` is never used as the outside-press signal in this mode.
  Source: `floating-ui-react/hooks/useDismiss.ts:120-121,351-374,387-534,727-728`. Test: none directly (`PreviewCardRoot.test.tsx` "keeps the parent preview card open when press starts in nested popup and ends outside" covers the negative case).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): the negative case is green in `tests/browser/preview-card.spec.ts` on both render modes: Base UI's synthetic `pointerdown` and `click` land in one task, so they are dispatched in one task (ADR 04 Amendments 2026-10-29, gap 15's withdrawal), a commented exception to the real-input rule.
- **PRV-DIS-04** — MUST. While open, a touch sequence outside closes the card with reason `outside-press` when: the touch moves more than 10 px (close on that `touchmove`), or ends after having moved more than 5 px (close on `touchend`), or the compatibility `mousedown` that follows a tap arrives within 1 s of the `touchstart` (close on that `mousedown`); a touch held for more than 1 s then released without those conditions does not close (FLT-PTR-27).
  Source: `floating-ui-react/hooks/useDismiss.ts:536-563,586-614,664-716,733-744`. Test: none.
- **PRV-DIS-05** — MUST. The following presses MUST NOT close the card: a press on or inside any registered Trigger of the card (switching triggers, FLT-DIS-05); a press inside the Positioner subtree or inside any nested open card's Positioner (FLT-DIS-08); a press whose target was injected into the page after the card opened (inert-marker check, FLT-DIS-06); a non-touch press on a scrollbar (FLT-DIS-07); a press that began inside the card and was released outside (the trailing `click` is ignored in sloppy mode, FLT-DIS-03/04); and, for an outer card, a press inside a nested card. Because `bubbles.outsidePress` defaults to `true`, an outside press closes **both** a nested card and its parent.
  Source: `floating-ui-react/hooks/useDismiss.ts:368-374,399-423,437-448,452-482,484-486,514-516`; `floating-ui-react/hooks/useDismiss.ts:139` (`normalizeProp`). Test: [L4] `PreviewCardRoot.test.tsx` "keeps the parent preview card open when clicking nested trigger", "keeps the parent preview card open when press starts in nested popup and ends outside".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes.
- **PRV-DIS-06** — MUST. When the active Trigger blurs, the card closes with reason `trigger-focus` and the blur event after a 0 ms macrotask, unless at that time: focus left the page (no `relatedTarget` and the trigger is still the active element); the new active element is inside the Positioner subtree or the trigger; or the next focused element is another registered trigger of the card (that trigger's own focus handling decides). A blur also clears any pending focus-open timer and the focus block of PRV-KEY-04 (FLT-FOC-04).
  Source: `floating-ui-react/hooks/useFocus.ts:185-233`. Test: [L4] `PreviewCardRoot.test.tsx` "should close when the trigger is blurred"; `PreviewCardRoot.detached-triggers.test.tsx` "when focus opens and closes the popup"; `detachedTriggersConformanceTests.tsx` "opens with any trigger on focus and marks only that trigger as active".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio and the detached suite on both render modes.
- **PRV-DIS-07** — MUST. When the active trigger element unregisters while the card is open (it unmounts or loses its id) the card requests a close one microtask later with reason `none`, a generic `base-ui` event and no trigger, unless a trigger with the same id registered in the meantime (PSH-STA-23/24, EVD-EVT-13). If the request is not canceled the active trigger is cleared and the remaining triggers show no `data-popup-open`; if it is canceled (controlled Root declining), the card stays open with its previous payload and ownership and no other trigger is claimed (PSH-STA-25). Server: relaxed, see §9.
  Source: `preview-card/root/PreviewCardRoot.tsx:59`; `utils/popups/popupStoreUtils.ts:524-546`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should close when the active trigger unmounts", "should close when the active detached trigger unmounts", "should remain open when the active trigger unmount close is canceled" (`reason: 'none'`, content stays `1`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): the three titles are green at L2 (`PreviewCardDetachedTriggersTests`; the close is C#'s) and the Server relaxation per mode at L4 ("port: the trigger-unmount close lands a round trip after the trigger leaves on Server", §9).
- **PRV-DIS-08** — MUST. Nested hover closing is synchronized through the floating tree: while a parent card has an open nested card, leaving the parent's Positioner with the pointer does not start the parent's close; instead the parent waits for the child's `floating.closed` event and then, if it has no other open descendants, closes itself after a 0 ms macrotask with reason `trigger-hover`, emitting its own `floating.closed` (FLT-PTR-24/25, FLT-EVT-03). Hovering a nested trigger inside the parent's Popup keeps the parent open (the pointer is still inside the parent's floating element), and a safe-polygon close of the parent is aborted while a nested card is open.
  Source: `floating-ui-react/hooks/useHoverFloatingInteraction.ts:161-163,197-200,235-245`; `floating-ui-react/safePolygon.ts:167-180`; `floating-ui-react/utils/nodes.ts:5-16`. Test: [L4] `PreviewCardRoot.test.tsx` "keeps the parent preview card open when hovering nested trigger", "parent popup closes as soon as the child popup closes", "keeps the parent open and re-opens the child when re-entering after partial close". Docs: `CHANGELOG.md` v1.2.0 "Fix nested hoverable popups".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): "keeps the parent preview card open when hovering nested trigger" and "keeps the parent open and re-opens the child when re-entering after partial close" are green in `tests/browser/preview-card.spec.ts` on both render modes; "parent popup closes as soon as the child popup closes" is written and skipped, open to design point 11.6 (the floating hover handle's tree parent).
- **PRV-DIS-09** — MUST. The card is NOT closed by: scrolling; a press on its trigger (no reference press); a window `blur`; a `focusout` of the card's content; or the pointer leaving a card that was not hover-opened (PRV-PTR-06).
  Source: `preview-card/root/PreviewCardRoot.tsx:98` (no options); `floating-ui-react/hooks/useDismiss.ts:117-125`. Test: [L4] `PreviewCardRoot.test.tsx` "does not close after hovering out of a popup opened externally".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts`'s trio on both render modes.
- **PRV-DIS-10** — MUST. A consumer-owned (controlled) card whose `onOpenChange(false, …)` handler does not apply the request stays open with its active trigger and payload unchanged (EVD-STA-10); none of the dismissal paths force it closed, and the trigger-unmount path (PRV-DIS-07) additionally keeps ownership when the request is canceled.
  Source: `utils/popups/popupStoreUtils.ts:273-277,533-542`. Test: [L2] `PreviewCardRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled"; `popupConformanceTests.tsx` "is ignored when `open={true}` is passed, so Escape cannot close".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardDetachedTriggersTests` "should remain open when the active trigger unmount close is canceled", `PreviewCardRootTests` "is ignored when `open={true}` is passed, so Escape cannot close") and in the popup suite on both render modes.
- **PRV-DIS-11** — MUST. `actionsRef.current.close()` and `handle.close()` request a close with reason `imperative-action`, a generic `base-ui` event and no trigger; `handle.close()` is a no-op (with the PRV-EDGE-06 warning) while no Root is attached.
  Source: `preview-card/root/PreviewCardRoot.tsx:76`; `preview-card/store/PreviewCardHandle.ts:38-40`; `utils/popups/popupHandle.ts:271-285`. Test: [L2] `PreviewCardRoot.test.tsx` "closes the preview card when the `close` method is called" (`reason: 'imperative-action'`); `detachedTriggersConformanceTests.tsx` "opens by trigger id with its payload and closes".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardRootTests` "closes the preview card when the `close` method is called" and `PreviewCardDetachedTriggersTests` "opens by trigger id with its payload and closes".

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onOpenChange` | `(open: boolean, details: PreviewCard.Root.ChangeEventDetails) => void` | Every open/close *request* made by the component (not by prop changes), before the state is updated | `trigger-hover`, `trigger-focus`, `outside-press`, `escape-key`, `imperative-action`, `none` (`trigger-press` declared only) | yes (`details.cancel()`); plus `details.preventUnmountOnClose()` |
| `onOpenChangeComplete` | `(open: boolean) => void` | After the enter animations of the Popup finish (`true`) / after the exit finishes and the subtree unmounts (`false`) | — | no |

- **PRV-EVT-01** — MUST. `onOpenChange` is invoked exactly once per open-change request, synchronously inside the originating handler/timer, before any internal state or DOM changes (EVD-EVT-02/03, EVD-ALG-04), with `details` created per EVD-ALG-01 and extended with `preventUnmountOnClose()` (EVD-API-09).
  Source: `preview-card/store/PreviewCardStore.ts:71-99`; `utils/popups/popupStoreUtils.ts:246-313`. Test: [L2] `popupConformanceTests.tsx` "is called with the new open state and the change details"; `PreviewCardRoot.test.tsx` "should call onOpenChange when the open state changes" (exactly two calls: `false` then `true` as the *previous* open values).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardRootTests` "should call onOpenChange when the open state changes", two calls observing `false` then `true`) and in `tests/browser/preview-card.spec.ts` on both render modes.
- **PRV-EVT-02** — MUST. The reasons emitted, and the user action producing each, are exactly:
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes; the close and unmount-cancel titles are green at L2.

  | Reason | Open / close | Produced by | `details.event` | `details.trigger` |
  |---|---|---|---|---|
  | `trigger-hover` | open | hover timer, immediate trigger switch, reopen during exit (PRV-PTR-01/08/09) | `MouseEvent` (`mouseenter`) | the trigger |
  | `trigger-hover` | close | trigger leave after safe polygon + `closeDelay`; Positioner leave + `closeDelay`; nested synchronized close (PRV-PTR-04/06, PRV-DIS-08) | `MouseEvent` (`mouseleave`/`mousemove`) | `undefined` |
  | `trigger-focus` | open | focus-visible focus after `delay`, or immediate switch (PRV-KEY-01/02) | `FocusEvent` | the trigger |
  | `trigger-focus` | close | blur after 0 ms (PRV-DIS-06) | `FocusEvent` | `undefined` |
  | `escape-key` | close | Escape (PRV-DIS-01) | `KeyboardEvent` | `undefined` |
  | `outside-press` | close | outside press (PRV-DIS-03/04) | `PointerEvent` / `MouseEvent` / `TouchEvent` | `undefined` |
  | `imperative-action` | open | `handle.open(id)` | generic `Event` (`type: 'base-ui'`) | the trigger with that id |
  | `imperative-action` | close | `handle.close()`, `actionsRef.current.close()` | generic `Event` | `undefined` |
  | `none` | close | active trigger unmount (PRV-DIS-07) | generic `Event` | `undefined` |

  Source: `preview-card/root/PreviewCardRoot.tsx:191-198`; emitters cited per row in §6; `internals/createBaseUIEventDetails.ts:126-157`. Test: [L4] `popupConformanceTests.tsx` "is called with the new open state and the change details" (`trigger-hover` + trigger; `escape-key`); `PreviewCardRoot.test.tsx` "closes the preview card when the `close` method is called" (`imperative-action`); `PreviewCardRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled" (`none`); `PreviewCardPositioner.test.tsx` "clears hovered-line coords when opened via focus" (`trigger-focus`).
- **PRV-EVT-03** — MUST. `trigger-press` is part of `PreviewCard.Root.ChangeEventReason` but is **never emitted**: the dismiss mechanism is configured without `referencePress`, no press handler calls `setOpen`, and no other path uses the reason. A port MUST keep the literal in the type for compatibility and MUST NOT emit it (EVD §10 Q3; §16 Q1).
  Source: `preview-card/root/PreviewCardRoot.tsx:98,194`; `floating-ui-react/hooks/useDismiss.ts:122,187-199`. Test: none.
- **PRV-EVT-04** — MUST. `details.trigger` is the opening trigger element for every open (hover, focus, `handle.open`) and `undefined` for every close (EVD-API-07); consequently a controlled consumer derives the active trigger id from `details.trigger?.id ?? null` on each callback, as the docs demonstrate.
  Source: `floating-ui-react/hooks/useHoverReferenceInteraction.ts:280,292,297`; `floating-ui-react/hooks/useFocus.ts:159-165,231`; `utils/popups/popupHandle.ts:259-262,284`. Test: [L4] `detachedTriggersConformanceTests.tsx` "allows controlling the open state and active trigger programmatically"; `PreviewCardPositioner.test.tsx` "ignores stale hovered coords when a controlled trigger switch reuses the popup". Docs: `components/preview-card/page.mdx` §Controlled mode with multiple triggers.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes; "ignores stale hovered coords when a controlled trigger switch reuses the popup" is green in `tests/browser/preview-card.spec.ts` on both render modes.
- **PRV-EVT-05** — MUST. `details.event` is the native event listed in PRV-EVT-02; for `imperative-action` and `none` it is a freshly created generic event with `type === 'base-ui'` (EVD-API-04/05).
  Source: `internals/createBaseUIEventDetails.ts:140`; `utils/popups/popupStoreUtils.ts:533`. Test: none.
- **PRV-EVT-06** — MUST. Calling `details.cancel()` inside `onOpenChange` stops the whole request (EVD-EVT-05/07): no internal open-change notification, no trigger re-association, no `instantType`/coordinate update, no state change; the Trigger keeps/lacks `data-popup-open` accordingly. For a consumer-owned card `cancel()` has no additional effect (EVD-EVT-06) beyond the trigger-unmount ownership rule (PRV-DIS-07).
  Source: `utils/popups/popupStoreUtils.ts:273-277`; `preview-card/store/PreviewCardStore.ts:77-98` (`onBeforeDispatch` runs only when not canceled). Test: [L2] `popupConformanceTests.tsx` "keeps the popup closed when the open change is canceled"; `PreviewCardRoot.detached-triggers.test.tsx` "should remain open when the active trigger unmount close is canceled".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardRootTests` "keeps the popup closed when the open change is canceled", `PreviewCardDetachedTriggersTests`) and in the popup suite on both render modes.
- **PRV-EVT-07** — MUST. Calling `details.preventUnmountOnClose()` inside `onOpenChange` for a non-canceled close keeps the card mounted (and the Positioner in the document) after the exit animation until `actionsRef.current.unmount()` is called (TRN-EVT-07, EVD-EVT-21); it has no effect on opens (PSH-STA-12).
  Source: `utils/popups/popupStoreUtils.ts:228-236,271,284-289`; `preview-card/root/PreviewCardRoot.tsx:72-79`. Test: [L2] `PreviewCardRoot.test.tsx` "unmounts the preview card when the `unmount` method is called"; `viewportConformanceTests.tsx` "does not restart a transition when the active trigger remounts while closed and retained (strict: $strict)". Docs: `CHANGELOG.md` v1.9.0 "Decouple automatic unmounting from `actionsRef`".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardRootTests`); the viewport suite's retained title is green on both render modes; the retained close's browser half (the exit held in the ending phase until `Unmount`) stays open to [issue 147](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/147), as Tooltip's TIP-EVT-04.
- **PRV-EVT-08** — MUST. `onOpenChange` MUST NOT be called when the consumer changes `open`/`triggerId`, when the card is initially open through `defaultOpen`, when a close/open timer is cancelled before firing (PRV-PTR-03/05/07), when the pointer re-enters the active trigger of an open card, or when the open state does not change because of a declined controlled request; it MUST be called for an open request that opens the card on a different trigger while already open (PRV-EVT-09).
  Source: `utils/popups/popupStoreUtils.ts:246-313` (only called from `setOpen`); `floating-ui-react/hooks/useHoverReferenceInteraction.ts:274,289-299`. Test: [L2] `PreviewCardRoot.test.tsx` "does not call onOpenChange again when the trigger is re-hovered while open" (one call), "should call onOpenChange when the open state changes" (two calls); `PreviewCardRoot.detached-triggers.test.tsx` "keeps a default-open root open while a detached trigger migrates after the initial commit" (`not.toHaveBeenCalled`) (L4 for the cancelled-timer clause).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardRootTests`, `PreviewCardDetachedTriggersTests`) and in `tests/browser/preview-card.spec.ts` on both render modes.
- **PRV-EVT-09** — MUST. Moving the open card to another trigger (hover PRV-PTR-08, focus PRV-KEY-02, `handle.open(otherId)`) invokes `onOpenChange(true, { trigger: <new trigger> })` once and never `onOpenChange(false, …)`; the previous trigger loses `data-popup-open` and the new one gains it in the same update (PSH-STA-31).
  Source: `utils/popups/popupStoreUtils.ts:203-212,283-305`. Test: [L4] `detachedTriggersConformanceTests.tsx` "does not leave an inline scale style on the popup after switching triggers" (`onOpenChange` calls contain no `false`); `PreviewCardRoot.detached-triggers.test.tsx` "when the popup moves to another trigger" (L4 for the hover and focus clauses).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes (`viewport: true` runs the inline-scale title). L2 keeps the render counts (`PreviewCardDetachedTriggersTests`).
- **PRV-EVT-10** — MUST. `onOpenChangeComplete(true)` fires once per open, on animations finished for the Popup element (after `data-starting-style` is removed), guarded by the card still being open; `onOpenChangeComplete(false)` fires once per close, from the unmount cleanup after the Popup's exit animations finish (or immediately when there are none, or when `actionsRef.unmount()` is called), after the active trigger is cleared and the hovered-line coordinates are reset; it is not called on a closed mount (TRN-EVT-01..06, PSH-STA-45).
  Source: `preview-card/popup/PreviewCardPopup.tsx:37-45`; `utils/popups/popupStoreUtils.ts:586-595`; `preview-card/root/PreviewCardRoot.tsx:60-62`. Test: [L4] `popupConformanceTests.tsx` "is not called on mount when closed", "is called on open when there is no enter animation", "is called on close when there is no exit animation", "is called on open once the enter animation finishes", "is called on close once the exit animation finishes".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes.
- **PRV-EVT-11** — MUST. `handle.open(id)` invokes `onOpenChange(true, { reason: 'imperative-action', trigger: <element with id> })` and, if not canceled, opens the card anchored to that trigger with that trigger's payload; calling it with the id of a different trigger while open switches triggers per PRV-EVT-09.
  Source: `preview-card/store/PreviewCardHandle.ts:29-31`; `utils/popups/popupHandle.ts:213-263`. Test: [L2] `detachedTriggersConformanceTests.tsx` "opens by trigger id with its payload and closes"; `PreviewCardRoot.detached-triggers.test.tsx` "when the popup moves to another trigger".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardDetachedTriggersTests`) and in the detached suite on both render modes.

## 8. Form integration

Not applicable — Preview Card renders no form control, has no `name`/`value`, and does not register with `Field`/`Form`.

## 9. Lifecycle, mounting and animation

Vocabulary: **post-commit, pre-paint**, **next frame**, **microtask**, **macrotask / N ms**, **on animations finished** as defined in conventions §4 and TRN §2. "Mounted" is the store's `mounted` flag (PSH §4.3.1); "status" is the derived `transitionStatus` (PSH-STA-05).

- **PRV-ANI-01** — MUST. The Portal subtree (Positioner, Popup, Arrow, Viewport, and a Backdrop placed inside the Portal) is rendered iff `mounted || keepMounted` (TRN-ANI-01); the store's `mounted` is seeded synchronously at Root creation (`true` for an initially open card, PSH-STA-44).
  Source: `preview-card/portal/PreviewCardPortal.tsx:22-27`; `utils/popups/popupStoreUtils.ts:598-605`. Test: [L2] `PreviewCardRoot.test.tsx` "should open when the trigger is hovered" / "should close when the trigger is unhovered"; `popupConformanceTests.tsx` "removes the popup when there is no exit animation defined".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): the closed state is green at L2 (`PreviewCardRootTests`); the removal is the `transitions` module's, green in `tests/browser/preview-card.spec.ts`'s trio and the popup suite on both render modes.
- **PRV-ANI-02** — MUST. The transition status machine is used with its defaults (`enableIdleState: false`, `deferEndingState: false`, `animateInitialOpen: false`, TRN §6.1): open while unmounted → mounted and `'starting'` in the same commit, cleared next frame (TRN-STA-03/04); close while mounted → `'ending'` in the closing commit (TRN-STA-06); an initially open card never enters `'starting'` (TRN-STA-13).
  Source: `preview-card/root/PreviewCardRoot.tsx:60`; `utils/popups/popupStoreUtils.ts:573-608`; `internals/useUnmountAfterClose.ts:59-64`. Test: [L4] `popupConformanceTests.tsx` "is called on open once the enter animation finishes" (animation runs after open); `PreviewCardRoot.detached-triggers.test.tsx` "opens immediately when entering trigger B during trigger A close transition" (`data-ending-style` appears on close).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in the popup suite and `tests/browser/preview-card.spec.ts` on both render modes.
- **PRV-ANI-03** — MUST. `data-starting-style`/`data-ending-style` are rendered on the Popup and the Backdrop (TRN-DOM-01/02) and on the Viewport's containers during a content morph (PRV-DOM-20); the Positioner and the Arrow never carry them, and the Trigger never carries them.
  Source: `preview-card/popup/PreviewCardPopup.tsx:68`; `preview-card/backdrop/PreviewCardBackdrop.tsx:46`; `preview-card/positioner/PreviewCardPositioner.tsx:88-94` (no `transitionStatus` in state). Test: [L4] `popupConformanceTests.tsx` "hides the kept-mounted popup once the exit animation finishes".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes.
- **PRV-ANI-04** — MUST. After a close is applied the card stays mounted (Popup `data-closed` + `data-ending-style`, Positioner `pointer-events: none`) until the Popup element's finite animations finish (TRN-ANI-03..08, detected on the Popup element only), then — in the same microtask — the store clears the active trigger, sets `mounted = false`, resets the hovered-line coordinates, and calls `onOpenChangeComplete(false)` (PSH-STA-45, TRN-ANI-27); without `keepMounted` the Portal subtree is then removed. With no exit animation the unmount happens after the first inspection next frame (TRN-ANI-11) — or synchronously under the animations-disabled flag / without `getAnimations` (TRN-EDGE-01/02). Server: relaxed, see §9.
  Source: `utils/popups/popupStoreUtils.ts:579-596`; `preview-card/popup/PreviewCardPopup.tsx:61` (`popupRef`); `preview-card/root/PreviewCardRoot.tsx:60-62`. Test: [L4] `popupConformanceTests.tsx` "is called on close once the exit animation finishes", "removes the popup when there is no exit animation defined"; `PreviewCardRoot.detached-triggers.test.tsx` "still respects trigger B open delay after trigger A close transition finishes" (`data-ending-style` removed, then `data-closed`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in the popup suite and `tests/browser/preview-card.spec.ts` on both render modes; the Server relaxation is asserted per mode ("port: an animated close hides the popup at animation end and removes it a round trip later on Server", §9). The hovered-line reset at the unmount is design point 11.2.
- **PRV-ANI-05** — MUST. With `keepMounted`, after the exit completes the Positioner and Backdrop get `hidden` and keep their elements; on reopen the same elements are reused (TRN-API-02), the Positioner loses `hidden`, and a positioning update runs post-commit, pre-paint of the commit in which the card is both open and mounted, so a reopen on a different trigger is repositioned to that trigger before paint.
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:82-86,101`; `preview-card/backdrop/PreviewCardBackdrop.tsx:37`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should reposition to a different trigger when reopened with keepMounted=true"; `viewportConformanceTests.tsx` "creates morphing containers after a kept-mounted popup closes and reopens".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` and the viewport suite on both render modes. The pass on a reopen while mounted is the `positioning` handle's, asked for by `PositionOnOpen` and run from the applied batch's open observer, before paint on both modes (design point 11.5, ADR 04 Amendments 2026-10-29).
- **PRV-ANI-06** — MUST. `actionsRef.current.unmount()` is the unmount action (TRN-ANI-19..22): while closed and mounted it ends the closing phase immediately (same cleanup and `onOpenChangeComplete(false)` as PRV-ANI-04); it is a no-op when already unmounted; called while open it takes effect only if a close commits in the same batch. It is the only way to end the closing phase after `preventUnmountOnClose()` (PRV-EVT-07).
  Source: `preview-card/root/PreviewCardRoot.tsx:72-79`; `internals/useUnmountAfterClose.ts:86-116`. Test: [L2] `PreviewCardRoot.test.tsx` "unmounts the preview card when the `unmount` method is called" (positioner present after unhover, removed after `unmount()`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2 (`PreviewCardRootTests` "unmounts the preview card when the `unmount` method is called"); the retained close's browser half (the exit held in the ending phase until `Unmount`) stays open to [issue 147](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/147), as Tooltip's TIP-EVT-04.
- **PRV-ANI-07** — MUST. The instant marker (`data-instant` on Positioner, Popup and Viewport, PRV-STA-10) is the component's only animation hint: `focus` after a focus open (so CSS can skip the enter transition for keyboard users), `dismiss` after an Escape close, absent after hover changes. No inline animation suppression beyond TRN-DOM-05 is applied.
  Source: `utils/popups/popupStoreUtils.ts:296-302`; `preview-card/positioner/PreviewCardPositioner.tsx:93`; `preview-card/popup/PreviewCardPopup.tsx:55`; `preview-card/viewport/PreviewCardViewport.tsx:37`. Test: none.
- **PRV-ANI-08** — MUST. Reopening during the exit (PRV-PTR-09, or a controlled flip back to `open`) keeps the card mounted, removes `data-ending-style` next frame and does **not** pass through `'starting'` (TRN-STA-09); `onOpenChangeComplete(false)` is not called and `(true)` fires after the enter animations (TRN-EVT-05).
  Source: `utils/popups/store.ts:180-183`; `internals/useUnmountAfterClose.ts:118-127`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "reopens immediately when re-hovering trigger A during its close transition" (`data-open` within 200 ms, no `data-closed`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes, the close transition lengthened to 1000 ms.
- **PRV-ANI-09** — MUST. A card that is open on first render (`defaultOpen` or `open`) is mounted without an enter phase: no `data-starting-style`, no `transition: none` suppression, and `onOpenChangeComplete(true)` once (TRN-STA-13, TRN-EVT-04).
  Source: `utils/popups/popupStoreUtils.ts:585,598-605`; `internals/useUnmountAfterClose.ts:59-64`. Test: [L4] `popupConformanceTests.tsx` "opens initially and remains uncontrolled".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes.
- **PRV-ANI-10** — MUST. With a Viewport, when the active trigger changes while the card is open and mounted, the Viewport starts a content morph: the previous content's DOM clone appears in a `[data-previous]` container (inert, absolutely positioned, sized by `--popup-width/--popup-height`), the new content mounts in a fresh `[data-current]` container, `data-starting-style` is set on the current container for one frame and `data-ending-style` on the previous container from the next frame, `data-transitioning` and `data-activation-direction` are set on the Viewport, and the Positioner/Popup are measured and sized through PSH auto-resize (`--positioner-width/height`, `--popup-width/height`); on animations finished of the current container (waiting for the starting attribute to be removed) the previous container is removed and `data-transitioning` cleared. Rapid successive switches always show the latest content and keep the latest transition. The morph uses the Positioner's rendered `side` and the reading direction to compute offsets.
  Source: `preview-card/viewport/PreviewCardViewport.tsx:28-32`; `utils/usePopupViewport.tsx:101,137-146,156-179,187-205,229-268,280-289`. Test: [L4] `viewportConformanceTests.tsx` "creates morphing containers during transitions", "sets `data-activation-direction` from the previous to the new trigger", "shows the latest content after rapid trigger changes", "keeps the latest transition active during rapid trigger changes", "cleans up the transition when a lagging payload remounts the current container"; `detachedTriggersConformanceTests.tsx` "does not leave an inline scale style on the popup after switching triggers". Docs: `components/preview-card/page.mdx` §Animating the Preview Card.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes, and the detached suite's inline-scale title.
- **PRV-ANI-11** — MUST. A trigger that remounts (new element, same id) while the card is closed and retained (`keepMounted` or `preventUnmountOnClose`) MUST NOT start a Viewport morph: the Popup stays `data-closed`, the Viewport has no `data-transitioning` and no `[data-previous]` container exists.
  Source: `utils/usePopupViewport.tsx:88,150-154,156-179`. Test: [L4] `viewportConformanceTests.tsx` "does not restart a transition when the active trigger remounts while closed and retained (strict: $strict)". Docs: `CHANGELOG.md` v1.9.0 "Don't start a viewport transition when the trigger remounts while closed".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes.

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- PRV-STA-05 — row RND-STA-01 for mirrored fields: `side`, `align`, `anchorHidden` are JS-written at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.
- PRV-STA-06 — row RND-STA-01 for mirrored fields: `side`, `align`, `transitionStatus` are JS-written at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.
- PRV-STA-07 — row RND-STA-01 for mirrored fields: `side`, `align`, `uncentered` are JS-written at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later.
- PRV-STA-14 — row PSH-STA-23: the close-on-active-trigger-unmount check runs in `OnAfterRender`; on Server after paint plus a round trip, during which the card stays open.
- PRV-PTR-10 — row EVD-EVT-18, PSH-ANI-03: the `trigger-hover` commit lands a round trip later; same-task animation queries do not see it (the statement holds on WebAssembly only).
- PRV-DIS-01 — row EVD-EVT-11, EVD-DIS-01, FLT-KEY-01: the consumer's `cancel()` / `allowPropagation()` arrives a round trip after dispatch; JS prevents and stops Escape whenever this card is the innermost open popup (ADR 04 §3 fallback); WebAssembly meets the spec through `decide()`.
- PRV-DIS-07 — row PSH-STA-23: the close-on-active-trigger-unmount check runs in `OnAfterRender`; on Server after paint plus a round trip, during which the card stays open.
- PRV-ANI-04 — row TRN-ANI-03, TRN-ANI-09, TRN-ANI-10: the unmount cleanup lands in the first render batch after the completion message, about two round trips later; JS's optimistic convergent write sets `hidden` at animation end so the card is not visible meanwhile.

## 10. Positioning

Positioning is the shared anchor-positioning session (FLT §4) driven by the Positioner. Preview Card contributes the defaults of PRV-API-18, an **inline-rect middleware** that runs first in the pipeline, and the hover-coordinate capture that feeds it.

- **PRV-POS-01** — MUST. The Positioner passes to the positioning session: the consumer props with the defaults of PRV-API-18; `keepMounted` from the Portal; `mounted` from the store; its floating-tree `nodeId`; the store's `adaptiveOrigin`; and `inline = createInlineMiddleware(inlineRectCoordsRef)`. Collision handling is therefore `flip` on both axes with perpendicular fallback preferring the logical end side (FLT-POS-02..05), `offset` from `sideOffset`/`alignOffset` (FLT-POS-06), `size` writing the CSS variables (FLT-POS-07), the arrow middleware (FLT-POS-08), `transformOrigin`, `hide` (FLT-POS-09) and, when present, adaptive origin.
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:60-79`; `internals/useAnchorPositioning.ts:257-261,359-457` (FLT-POS-02, FLT-API-17). Test: [L4] `positionerConformanceTests.tsx` (all); `PreviewCardPositioner.test.tsx` "uses the hovered line with a custom anchor in a clipped keepMounted portal" (`data-side="top"` after a flip inside a `collisionBoundary` rect).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes; the custom-anchor title is green in `tests/browser/preview-card.spec.ts`. L2 adds the options sent (`PreviewCardPopupPartsTests` "port: the positioner renders role presentation and PreviewCard's default side, and sends its paddings and the hovered-line option").
- **PRV-POS-02** — MUST. The floating element is the **Positioner** element (not the Popup) and the default anchor is the active trigger element, both synced from the store (PSH-STA-46, FLT-STR-10); while the card is not mounted the session's elements are cleared so no positioning runs for a kept-mounted closed card (FLT-STA-09). Consequences: hover-leave detection of the card and outside-press containment use the Positioner's subtree.
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:100` (`positionerElement`); `utils/popups/popupStoreUtils.ts:81-88`; `internals/useAnchorPositioning.ts:459-470`. Test: [L4] `PreviewCardRoot.test.tsx` "closes after hovering out of a popup opened by its trigger" (events on the positioner); `detachedTriggersConformanceTests.tsx` "allows controlling the open state and active trigger programmatically" (positioner left edge equals the active trigger's).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the Positioner-leave close is the `interactions` module's floating handle, green in `tests/browser/preview-card.spec.ts`'s trio and the detached suite on both render modes. L2 adds "port: the positioner's floating handle joins the card's session with the hover leave and the tree" (`PreviewCardPopupPartsTests`).
- **PRV-POS-03** — MUST. Inline-line anchoring: before offsets and collision handling, when the anchor exposes `getClientRects()` and groups into **two or more line boxes** (rects sorted by top, merged while the next rect's top is within half the previous height), the measured anchor rect is replaced by: the line at the recorded `lineIndex` if still present; else the line containing the recorded point (2 px tolerance); else, when there are exactly two lines that do not overlap horizontally and a point is recorded, the full bounding box; else the side-aligned rect of PRV-PTR-14. Recorded coordinates are used only when they were captured for the anchor element itself (or its context element). When the replacement rect equals the measured rect nothing changes; otherwise the pipeline is reset with the new rects (at most 50 resets, FLT-EDGE-02). Anchors with fewer than two line boxes are positioned normally.
  Source: `utils/popups/inlineRect.ts:62-104,131-193,237-292`; `internals/useAnchorPositioning.ts:259-261`. Test: [L4] `PreviewCardPositioner.test.tsx` "positions the popup relative to the hovered line of a multiline trigger" (y = line bottom + `sideOffset` 5, x within the line), "uses the latest hovered line", "positions the popup relative to the side-aligned rect when open is controlled", "ignores stale hovered coords when a controlled trigger switch reuses the popup" (y 135 = second line bottom 130 + 5). Docs: `CHANGELOG.md` v1.4.0 "Integrate inline positioning"; v1.5.0 "Fix positioning and viewport edge cases".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes; the middleware is the `positioning` module's (PSH-POS-03, L3 `positioning.test.ts`, the eleven `inlineRect.test.ts` titles).
- **PRV-POS-04** — MUST. Anchor tracking (ancestor scroll, element resize, layout shift; FLT-POS-11, FLT-ALG-13) runs while the Positioner is mounted unless `disableAnchorTracking`; for a kept-mounted Portal it runs only while the card is mounted (FLT-POS-12). After a page scroll the card stays aligned to the same line box.
  Source: `internals/useAnchorPositioning.ts:472-503,581-586`. Test: [L4] `PreviewCardPositioner.test.tsx` "keeps the popup aligned after page scroll", "stays anchored to the opened line while already open" (a `resize` re-runs positioning without re-anchoring).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes.
- **PRV-POS-05** — MUST. In addition to anchor tracking, the Positioner forces a positioning update post-commit, pre-paint of every commit in which the card is open and mounted (so each open — including a reopen of a retained card on another trigger, and an immediate trigger switch — is positioned against the current anchor before paint).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:80-86`. Test: [L4] `PreviewCardRoot.detached-triggers.test.tsx` "should reposition to a different trigger when reopened with keepMounted=true" (L4 for the repositioned-before-paint clause).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: the title is browser-only, green on `preview-card-detached` on both render modes; a reopen on another trigger re-anchors from the new reference's tracking.
- **PRV-POS-06** — MUST. The rendered `side` reported in state/`data-side` is logical when the requested side was logical (`inline-start`/`inline-end`, FLT-I18N-02) and physical otherwise; `align` is the rendered alignment after flipping; both are also what `sideOffset`/`alignOffset` functions receive (FLT-API-12). `anchorHidden` is `true` iff the hide middleware reports the anchor clipped (FLT-POS-09).
  Source: `internals/useAnchorPositioning.ts:41-53,588-591,633-636`. Test: [L4] `positionerConformanceTests.tsx` "reads the latest side after a flip inside the function", "reads the latest align after a flip inside the function", "reads the logical side after a flip inside the function", "reads the logical side in RTL mode inside the function".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes.
- **PRV-POS-07** — MUST. The Arrow is positioned by the arrow middleware with `arrowPadding` (default 5) from the Popup's edges; `uncentered` is `true` iff the middleware's `centerOffset` is non-zero (the arrow could not sit at the anchor's center); without an Arrow part a stand-in element is measured so `--transform-origin` still points at the anchor (FLT-DOM-10). The shift limiter accounts for the arrow's size so the arrow never leaves the anchor (FLT-ALG-07).
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:69`; `internals/useAnchorPositioning.ts:317-334,381-390,625`. Test: none (`PreviewCardArrow.test.tsx` describeConformance only). Docs: `CHANGELOG.md` v1.0.0-beta.3 "Fix `limitShift` offset based on arrow size".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L4 port titles on `preview-card-positioner`'s arrow cases in `tests/browser/preview-card.spec.ts`, on both render modes.
- **PRV-POS-08** — MUST. While a Viewport is mounted the Positioner uses adaptive-origin coordinates (`top`/`bottom` + `left`/`right` instead of `transform`, FLT-DOM-04, FLT-ALG-12) so size transitions grow from the anchor-facing edge; mounting or unmounting the Viewport switches the positioning mode of the already-open card. The viewport's own containers follow the FLT-ALG-12 edge choice per `side` and direction (e.g. `side="top"` → `bottom: 0; left: 0` on the Popup).
  Source: `preview-card/viewport/PreviewCardViewport.tsx:28-32`; `utils/usePopupViewport.tsx:112-117`; `internals/useAnchorPositioning.ts:505,519-520`. Test: [L4] `positionerConformanceTests.tsx` "uses top/left positioning with Viewport", "updates positioning when Viewport mounts and unmounts"; `viewportConformanceTests.tsx` "anchors the popup for side=$side in $direction mode".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `positionerConformanceTests` (`viewport: true`) runs as a browser suite on `preview-card-positioner`, green on both render modes; `viewportConformanceTests` (`openOn: 'focus'`) runs as a browser suite on `preview-card-viewport`, green on both render modes.
- **PRV-POS-09** — MUST. A consumer `anchor` (element, DOM handle, function or virtual element) replaces the active trigger as the position anchor (FLT-API-11); inline-line anchoring still applies when that anchor is the hovered trigger element (coordinates are matched by element identity), and the card is repositioned when the anchor changes.
  Source: `preview-card/positioner/PreviewCardPositioner.tsx:61`; `utils/popups/inlineRect.ts:251-254`; `internals/useAnchorPositioning.ts:545-579`. Test: [L4] `PreviewCardPositioner.test.tsx` "uses the hovered line with a custom anchor in a clipped keepMounted portal" (`anchor={triggerRef}`, y = 55 = line top 100 − 40 − 5 after flipping to `top`).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green in `tests/browser/preview-card.spec.ts` on both render modes (the anchor is the trigger's `Element`, given after the first render as Base UI's ref is).
- **PRV-POS-10** — MUST. Until the first positioning pass of an open cycle the Positioner is invisible and fixed at the viewport origin (FLT-ANI-02, PRV-DOM-11); after it, `--transform-origin` reflects the arrow (or stand-in) position and the side offset so a `transform-origin: var(--transform-origin)` scale animation grows from the anchor (FLT-ALG-11). A function `sideOffset` is evaluated for the transform origin as well.
  Source: `internals/useAnchorPositioning.ts:391-454,511-541`. Test: none for Preview Card (`positionerConformanceTests.tsx` "offsets the positioner when a function is specified" covers the function evaluation). Docs: `CHANGELOG.md` v1.0.0-beta.3 "Fix `transform-origin` variable calculation when Positioner `sideOffset` is a function".

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| `useRenderElement` (every part) | [rendering-and-composition](../architecture/rendering-and-composition.md) §6.1 | Trigger: tag `a`, props `[hover bag, focus bag, dismiss trigger bag, inline-rect bag, { id }, consumer]`, mapping `triggerOpenStateMapping`; Positioner: through `usePositioner` (`popupStateMapping`); Popup: `[FOCUSABLE_POPUP_PROPS, popup bag, mount-transition styles, consumer]`, `popupTransitionStateMapping`; Arrow: `[{ style: arrowStyles, 'aria-hidden': true }, consumer]`, `popupStateMapping`; Backdrop: `[{ role, hidden, style }, consumer]`, `popupTransitionStateMapping`; Viewport: `[consumer, { children }]`, `popupViewportStateMapping`; Portal: lite portal node. |
| `useBaseUiId` | RND §6.3 | Trigger id (`id` prop override). |
| `createChangeEventDetails`, reasons | [event-details-and-reasons](../architecture/event-details-and-reasons.md) | Reason union of §3.10; `preventUnmountOnClose` extension; EVD-ALG-04 shared routine with `onBeforeDispatch` = hover-coordinate capture. |
| `usePopupRootStore` / `PreviewCardStore` | [popup-store-and-handles](../architecture/popup-store-and-handles.md) §4.3 | Store created with `{ open: defaultOpen, openProp: open, activeTriggerId: defaultTriggerId, triggerIdProp: triggerId }`; `treatPopupAsFloatingElement` false (floating element = Positioner). Extra state `instantType`, `adaptiveOrigin`, `closeDelay`; extra context `inlineRectCoordsRef`. |
| `applyPopupOpenChange` | PSH, EVD-ALG-04 | `onBeforeDispatch`: capture hovered-line coordinates on hover opens (PRV-STA-13(c)); no `extraState`. |
| `useImplicitActiveTrigger` | PSH §4.3.4 | `closeOnActiveTriggerUnmount: true`. |
| `useOpenStateTransitions` / `useUnmountAfterClose` / `useTransitionStatus` | PSH, [transitions-and-mounting](../architecture/transitions-and-mounting.md) §6.1, §6.4 | `onUnmount`: reset `inlineRectCoordsRef`; `animateInitialOpen` false. |
| `usePopupInteractionProps` | PSH-STA-42/43 | `activeTriggerProps = inactiveTriggerProps = dismiss.reference`, `popupProps = dismiss.floating`; mounted when `open \|\| mounted \|\| handle != null`. |
| `PopupHandleAttachment`, `BasePopupHandle` | PSH §3.1, §4.3.7 | `PreviewCardHandle`: component name `'PreviewCard'`, `throwOnMissingTrigger` default `true`, `open(triggerId: string)` (no `null`), no `openWithPayload`. |
| `useTriggerDataForwarding`, `useTriggerRegistration`, `usePopupHandleStore` | PSH §4.3.5 | Forwarded state `{ payload, closeDelay }`. |
| `useHoverReferenceInteraction` | [floating-and-positioning](../architecture/floating-and-positioning.md) FLT-PTR-11..19 | `mouseOnly: true`, `move: false`, `handleClose: safePolygon()` (default options, `blockPointerEvents` false), `delay: () => ({ open: delay ?? 600, close: closeDelay ?? 300 })`, `restMs` default 0, `triggerElementRef`, `isActiveTrigger: isTriggerActive(ownId)`, `isClosing: () => transitionStatus === 'ending'`; no `shouldOpen`, no `guardStaleOpen`. |
| `useHoverFloatingInteraction` | FLT-PTR-20..25 | `closeDelay: store.closeDelay`; always enabled. |
| `useFocus` | FLT-FOC-01..04, FLT-API-15 | `delay: delay ?? 600`; always enabled; `reference` bag applied to every trigger. |
| `useDismiss` | FLT-DIS-01..16, FLT-KEY-01..03 | Default options only: `escapeKey: true`, `outsidePress: true`, `outsidePressEvent: 'sloppy'`, `referencePress` never, `bubbles` undefined (`escapeKey: false`, `outsidePress: true`). |
| `FloatingTree`, `FloatingNode`, `useFloatingNodeId` | FLT-STR-06..09 | Root creates a tree unless inside another Preview Card Root; Positioner registers a node. |
| `useAnchorPositioning` + `usePositioner` | FLT §3.1, §4, FLT-API-17 | Defaults of PRV-API-18; `inline` middleware; `adaptiveOrigin` from store; `keepMounted` from Portal; `hidden: !mounted`, `inert: !open` (pointer-events). |
| `createInlineMiddleware`, `getInlineRectTriggerProps`, `updateInlineRectCoords` | PSH (`utils/popups/inlineRect.ts`) — behavior specified here (PRV-POS-03, PRV-STA-13) | Coordinates captured while the trigger is not the open card's active trigger; cleared on focus. |
| `FloatingPortalLite` / `useFloatingPortalNode` | PSH-STR-11, FLT-DOM-11, FLT-API-13 | `container`, `keepMounted` gate. |
| `usePopupViewport` / `usePopupAutoResize` | PSH (viewport) | `side` = Positioner's rendered side; children. |
| `useOpenChangeComplete` | TRN §6.3 | On the Popup element; `onComplete` → `onOpenChangeComplete(true)` when open. |
| `getDisabledMountTransitionStyles` | TRN §6.6 | Positioner and Popup. |
| `NullStore` | [shared-utilities](../architecture/shared-utilities.md) UTL-STA-34 | Fallback store for detached triggers. |

## 12. Edge cases, invariants and dev warnings

- **PRV-EDGE-01** — MUST. Rendering `Portal`, `Positioner`, `Popup`, `Arrow`, `Backdrop` or `Viewport` without a `PreviewCard.Root` ancestor throws `Error('Base UI: PreviewCardRootContext is missing. PreviewCard parts must be placed within <PreviewCard.Root>.')` (in every build).
  Source: `preview-card/root/PreviewCardContext.ts:11-22`. Test: [L2] `PreviewCardPopup.test.tsx` "throws a descriptive error when rendered outside <PreviewCard.Root>".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardMisuseTests`, every part below the Root.
- **PRV-EDGE-02** — MUST. Rendering a `Trigger` with neither a Root ancestor nor a `handle` throws `Error('Base UI: <PreviewCard.Trigger> must be either used within a <PreviewCard.Root> component or provided with a handle.')`.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:43-47`. Test: [L2] `PreviewCardTrigger.test.tsx` "throws without PreviewCard.Root or a handle".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardMisuseTests`.
- **PRV-EDGE-03** — MUST. Rendering a `Positioner` outside a `PreviewCard.Portal` throws `Error('Base UI: <PreviewCard.Portal> is missing.')`.
  Source: `preview-card/portal/PreviewCardPortalContext.ts:6-12`. Test: [L2] `PreviewCardPositioner.test.tsx` "throws a descriptive error when rendered outside <PreviewCard.Portal>".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardMisuseTests`.
- **PRV-EDGE-04** — MUST. Rendering `Popup`, `Arrow` or `Viewport` outside a `PreviewCard.Positioner` throws `Error('Base UI: PreviewCardPositionerContext is missing. PreviewCardPositioner parts must be placed within <PreviewCard.Positioner>.')`.
  Source: `preview-card/positioner/PreviewCardPositionerContext.ts:14-23`. Test: [L2] `PreviewCardPopup.test.tsx` "throws a descriptive error when rendered outside <PreviewCard.Positioner>"; `PreviewCardViewport.test.tsx` same title.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): green at L2: `PreviewCardMisuseTests` (the Popup, the Viewport and the Arrow).
- **PRV-EDGE-05** — MUST. `handle.open(id)` with an unregistered id throws, in every build, `Error('Base UI: PreviewCardHandle.open() was called with the trigger id "<id>", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted PreviewCard.Trigger that has this handle set on its "handle" prop.')` (shared message with the component name substituted).
  Source: `utils/popups/popupHandle.ts:241-249`; `preview-card/store/PreviewCardHandle.ts:18`. Test: [L4] `detachedTriggersConformanceTests.tsx` "throws when opened with an unregistered trigger id".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 keeps the same title with the port's message (`PreviewCardDetachedTriggersTests`).
- **PRV-EDGE-06** — SHOULD. In development, `handle.open()` / `handle.close()` with no attached Root log the PSH warnings with `PreviewCardHandle.open()` / `PreviewCardHandle.close()` substituted (`"… was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively."` / `"… The call was ignored."`); the calls do nothing.
  Source: `utils/popups/popupHandle.ts:216-224,274-282`. Test: [L4] `detachedTriggersConformanceTests.tsx` "ignores imperative calls while no root is attached and follows the root across remounts" (two warnings per pair of calls).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes. L2 keeps the same title (`PreviewCardDetachedTriggersTests`).
- **PRV-EDGE-07** — SHOULD. In development, when more than one mounted Root is attached to the same handle and the overlap persists past the next frame, the PSH overlap warning (`"Base UI: A handle is attached to more than one mounted root at the same time. …"`) is logged once; the most recently mounted Root controls the handle (PSH-STA-35).
  Source: `utils/popups/popupHandle.ts:153-173`. Test: [L4] `detachedTriggersConformanceTests.tsx` "resolves a trigger still registered to the previous root during a transient overlap" (no `"No trigger found"` warning; the overlap resolves within the commit).
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): `detachedTriggersConformanceTests` runs as a browser suite on `preview-card-triggers` (hover and focus, with a viewport), green on both render modes; L2 adds "port: warns when two roots stay attached to one handle at the next render-batch boundary" (`PreviewCardDetachedTriggersTests`).
- **PRV-EDGE-08** — SHOULD. In development, registering one trigger element under two different ids in the same registry throws `Error('Base UI: A trigger element cannot be registered under multiple IDs in PopupTriggerMap.')` (PSH-STA-33 registry).
  Source: `utils/popups/popupTriggerMap.ts:36-60`. Test: none.
- **PRV-EDGE-09** — SHOULD. In development, switching `open` or `triggerId` between `undefined` and a defined value after the first render logs the controlled-mode error of UTL-EDGE-25 (`useControlledProp` on `openProp` / `triggerIdProp`); the component keeps its first-render mode.
  Source: `preview-card/root/PreviewCardRoot.tsx:48-49`; `packages/utils/src/store/ReactStore.ts` (UTL-STA-22, UTL-EDGE-25). Test: none.
- **PRV-EDGE-10** — MUST. No part accepts a `disabled` prop and there is no `data-disabled`: a Trigger cannot be disabled short of removing it or its handlers; a consumer who must suppress opening does so through `onOpenChange` cancelation (PRV-EVT-06), which also keeps `data-popup-open` off.
  Source: `preview-card/trigger/PreviewCardTrigger.tsx:120-143`; `preview-card/root/PreviewCardRoot.tsx:133-184`. Test: [L4] `popupConformanceTests.tsx` "keeps the popup closed when the open change is canceled".
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 13 §3, the plan's settled rule "Layer tags and browser-only titles", settled by the orchestrator 2026-10-27): re-tagged [L2] → [L4]: `popupConformanceTests` runs as a browser suite (`tests/browser/suites/`) on `preview-card-conformance`, green on both render modes. L2 keeps "keeps the popup closed when the open change is canceled" (`PreviewCardRootTests`).
- **PRV-EDGE-11** — SHOULD. `instantType` persists across changes whose reason does not map to it (PRV-STA-10): e.g. after a focus open, an outside-press close and a later hover close both leave/clear it only as their own reasons dictate, so `data-instant="focus"` can remain on a closing card dismissed by an outside press. A port SHOULD reproduce this observed behavior rather than reset the marker on every close. SHOULD, not MUST: no test asserts it and §16 Q4 (shared with tooltip.md §16 Q13) records it as a suspected bug; if Base UI confirms it as a bug, this requirement is withdrawn.
  Source: `utils/popups/popupStoreUtils.ts:296-304`. Test: none.
  Port note (2026-10-29, M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): L1 port title: `PreviewCardStoreTests` "port: a change with an unmapped reason keeps the stale instant type" (§16 Q4, replicated).
- **PRV-EDGE-12** — MUST. Server rendering / hydration: the Root renders only its children on the server; a detached Trigger reads the handle's fallback store during server rendering and hydration (PSH-STA-34), so server markup never shows `data-popup-open`; no inline positioning script is emitted (contrast TRN-DOM-11 for Select/Toast). Nested Roots and detached triggers behave identically after hydration.
  Source: `utils/popups/usePopupHandleStore.ts:35`; `preview-card/root/PreviewCardRoot.tsx:86-92`. Test: none.
- **PRV-EDGE-13** — MUST. RTL: `side: 'inline-start'`/`'inline-end'` resolve by the reading direction from the nearest `DirectionProvider` (FLT-I18N-01) and the reported side stays logical (PRV-POS-06); the Viewport's edge choice follows the rendered physical side (PRV-POS-08). Nothing else in the component is direction-dependent.
  Source: `internals/useAnchorPositioning.ts:179-205`; `utils/usePopupViewport.tsx:78,287`. Test: [L4] `positionerConformanceTests.tsx` "reads the logical side in RTL mode inside the function"; `viewportConformanceTests.tsx` "anchors the popup for side=$side in $direction mode".

## 13. Behaviors evidenced by tests

Every test file is listed; shared suites are listed with the options Preview Card passes. The per-ID `Test:` lines are authoritative; this matrix is the derived view. All tests run with the animations-disabled flag (TRN-EDGE-01) unless a test clears it; browser-only tests are marked (browser).

### `root/PreviewCardRoot.test.tsx`

Fixtures: every `describe.for` block runs three times — contained trigger, one detached trigger, two detached triggers (`useRefWithInit(() => PreviewCard.createHandle())`) — so each row below also exercises PRV-STR-09 and PRV-API-12.

| Test | Requirements |
|---|---|
| `popupConformanceTests` (`triggerMouseAction: 'hover'`, no `expectedPopupRole`, `exitAnimation` default): "opens the popup with the `open` prop" | PRV-API-02, PRV-STA-02 |
| "opens initially and remains uncontrolled" | PRV-API-01, PRV-ANI-09, PRV-DIS-01 |
| "is ignored when `open={false}` is passed" / "is ignored when `open={true}` is passed, so Escape cannot close" | PRV-API-01, PRV-STA-02, PRV-DIS-10 |
| "is called with the new open state and the change details" | PRV-EVT-01, PRV-EVT-02, PRV-PTR-01, PRV-DIS-01 |
| "keeps the popup closed when the open change is canceled" | PRV-EVT-06, PRV-DOM-02, PRV-EDGE-10 |
| "is not called on mount when closed" / "is called on open when there is no enter animation" / "is called on close when there is no exit animation" / "is called on open once the enter animation finishes" / "is called on close once the exit animation finishes" (browser) | PRV-API-04, PRV-EVT-10, PRV-ANI-02, PRV-ANI-04 |
| "removes the popup when there is no exit animation defined" / "hides the kept-mounted popup once the exit animation finishes" (browser) | PRV-ANI-01, PRV-ANI-03, PRV-ANI-04, PRV-API-16 |
| "should open when the trigger is hovered" | PRV-PTR-01, PRV-API-10, PRV-STR-03 |
| "should close when the trigger is unhovered" | PRV-PTR-04, PRV-API-11, PRV-ANI-04 |
| "should open when the trigger is focused" (JSDOM only) | PRV-KEY-01, PRV-A11Y-01 |
| "should close when the trigger is blurred" | PRV-DIS-06, PRV-KEY-03 |
| "should call onOpenChange when the open state changes" | PRV-EVT-01, PRV-EVT-08, PRV-API-02 |
| "does not close after hovering out of a popup opened externally" | PRV-PTR-06, PRV-DIS-09, PRV-STA-02 |
| "closes after hovering out of a popup opened by its trigger" | PRV-PTR-06, PRV-STA-11, PRV-POS-02 |
| "does not call onOpenChange again when the trigger is re-hovered while open" | PRV-PTR-05, PRV-EVT-08 |
| "closes when the trigger is unhovered after opening by default" | PRV-PTR-04 |
| "does not close after hovering out of a popup opened without trigger hover" | PRV-PTR-06 |
| "should open after delay with rest type by default" | PRV-PTR-01, PRV-API-10 |
| "should close after delay" | PRV-API-11, PRV-PTR-04, PRV-PTR-10 |
| "reopens on hover after Escape closes it" | PRV-DIS-01, PRV-PTR-09 |
| "unmounts the preview card when the `unmount` method is called" (JSDOM only) | PRV-API-05, PRV-EVT-07, PRV-ANI-06 |
| "closes the preview card when the `close` method is called" (JSDOM only) | PRV-API-05, PRV-DIS-11, PRV-EVT-02, PRV-DOM-02 |
| "keeps the parent preview card open when clicking nested trigger" | PRV-STR-01, PRV-PTR-11, PRV-DIS-05 |
| "keeps the parent preview card open when press starts in nested popup and ends outside" | PRV-DIS-05 |
| "keeps the parent preview card open when hovering nested trigger" | PRV-STR-10, PRV-DIS-08 |
| "keeps the parent open and re-opens the child when re-entering after partial close" | PRV-PTR-07, PRV-DIS-08 |
| "parent popup closes as soon as the child popup closes" | PRV-DIS-08, PRV-STR-10 |

### `root/PreviewCardRoot.detached-triggers.test.tsx`

The suite's `multiple triggers` block runs once per mode (contained, detached), so each of its rows (from the first row through "does not leave an inline scale style on the popup after switching triggers") is 2 cases, except the first, which is 4.

| Test | Requirements |
|---|---|
| `detachedTriggersConformanceTests` (`openInteractions: ['hover', 'focus']`, `ariaExpanded: false`, `throwOnMissingTrigger: true`, `Viewport` given, no `Close`, `triggerProps: { href: '#', delay: 0, closeDelay: 0, style: { pointerEvents: 'none' } }`): "opens with any trigger on $interaction and marks only that trigger as active" (4 cases: `hover`, `focus` × contained, detached; browser) | PRV-STR-09, PRV-STA-04, PRV-DOM-02, PRV-ARIA-02, PRV-PTR-01, PRV-PTR-04, PRV-KEY-01, PRV-DIS-06 |
| "sets the payload of the active trigger and renders content based on it" (browser) | PRV-API-07, PRV-API-13, PRV-PTR-08 |
| "reuses the popup DOM nodes when switching triggers" (browser) | PRV-KEY-02, PRV-EVT-09 |
| "marks the trigger matching a controlled `triggerId` as active" (browser) | PRV-API-08 |
| "opens initially with the payload of a non-first `defaultTriggerId`" (browser) | PRV-API-09 |
| "allows controlling the open state and active trigger programmatically" (browser) | PRV-API-08, PRV-EVT-04, PRV-POS-02 |
| "does not leave an inline scale style on the popup after switching triggers" (browser) | PRV-EVT-09, PRV-ANI-10 |
| "opens by trigger id with its payload and closes" | PRV-API-21, PRV-EVT-11, PRV-DIS-11 |
| "throws when opened with an unregistered trigger id" | PRV-API-22, PRV-EDGE-05 |
| "ignores imperative calls while no root is attached and follows the root across remounts" | PRV-API-06, PRV-API-23, PRV-EDGE-06 |
| "registers a detached trigger declared after the root" | PRV-API-12 |
| "resolves a trigger still registered to the previous root during a transient overlap" | PRV-EDGE-07 (PSH-STR-04, PSH-STA-38) |
| "keeps a detached trigger working when wrappers are removed" / "… when wrappers are added" / "… during Fast Refresh-like handle recreation" / "… when reparented during Fast Refresh-like handle recreation" (browser) | PRV-API-12 (PSH-STR-01, PSH-STA-26) |
| "when opened and closed imperatively" | PRV-STA-17, PRV-API-14, PRV-API-21 |
| "when the popup moves to another trigger" | PRV-STA-17, PRV-EVT-09, PRV-EVT-11, PRV-STA-04 |
| "when focus opens and closes the popup" | PRV-STA-17, PRV-KEY-01, PRV-DIS-06 |
| "when hover opens and closes the popup" | PRV-STA-17, PRV-PTR-08, PRV-PTR-04 |
| "keeps a default-open root open while a detached trigger migrates after the initial commit" (browser) | PRV-STA-18, PRV-EVT-08 |
| "should open the preview card immediately when hovering another trigger" (browser) | PRV-PTR-08, PRV-API-13 |
| "should open again after escape when focusing another trigger" (browser) | PRV-KEY-04, PRV-DIS-01, PRV-KEY-01 |
| "should switch immediately when focusing another trigger while open" (browser) | PRV-KEY-02 |
| "should close when the active trigger unmounts" (browser) | PRV-DIS-07, PRV-STA-14 |
| "should remain open when the active trigger unmount close is canceled" (browser) | PRV-DIS-07, PRV-DIS-10, PRV-EVT-06, PRV-EVT-02 |
| "should reposition to a different trigger when reopened with keepMounted=true" (browser) | PRV-ANI-05, PRV-POS-05, PRV-DOM-06, PRV-API-16 |
| "should close when the active detached trigger unmounts" (browser) | PRV-DIS-07, PRV-STA-14 |
| "opens immediately when entering trigger B during trigger A close transition" (browser) | PRV-PTR-09, PRV-STA-06 |
| "still respects trigger B open delay after trigger A close transition finishes" (browser) | PRV-PTR-09, PRV-DOM-13, PRV-ANI-04 |
| "reopens immediately when re-hovering trigger A during its close transition" (browser) | PRV-PTR-09, PRV-ANI-08 |
| "respects open delay on later same-trigger hovers after close lifecycle finishes" (browser) | PRV-PTR-09 |

### `root/PreviewCardRoot.spec.tsx` (type-level)

| Check | Requirements |
|---|---|
| `createHandle<number>()` types the Root function child's `payload` as `number \| undefined` and rejects a string `payload` on a bound Trigger | PRV-API-07, PRV-API-21 |

### `trigger/PreviewCardTrigger.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLAnchorElement`, rendered inside `Root open`) | PRV-STR-02; RND-DOM-36..48 |
| "throws without PreviewCard.Root or a handle" | PRV-EDGE-02, PRV-STR-02 |

### `trigger/PreviewCardTrigger.spec.tsx` (type-level)

| Check | Requirements |
|---|---|
| the `render` callback receives `href` typed as the native `<a>` prop | PRV-API-15 |

### `portal/PreviewCardPortal.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`<Portal keepMounted>`, `refInstanceof: HTMLDivElement`, inside `Root open`) | PRV-STR-03, PRV-DOM-04, PRV-API-16; RND-DOM-36..48 |

### `positioner/PreviewCardPositioner.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Root open` + `Portal`) | PRV-STR-04; RND-DOM-36..48 |
| "throws a descriptive error when rendered outside <PreviewCard.Portal>" | PRV-EDGE-03 |
| `positionerConformanceTests` (`viewport: true`, trigger substituted with `<div>`) (browser): "offsets the positioner when a number is specified" (`sideOffset`, `alignOffset`) | PRV-API-18, PRV-POS-01 |
| "offsets the positioner when a function is specified" (`sideOffset`, `alignOffset`) | PRV-API-18, PRV-POS-10 |
| "reads the latest side after a flip inside the function" / "reads the latest align after a flip inside the function" / "reads the logical side after a flip inside the function" / "reads the logical side in RTL mode inside the function" (×2 props) | PRV-POS-06, PRV-STA-05, PRV-DOM-09, PRV-EDGE-13 |
| "rests exactly at collisionPadding from the colliding edge" | PRV-API-18, PRV-DOM-10 |
| "uses transform positioning without Viewport" | PRV-DOM-11 |
| "uses top/left positioning with Viewport" / "updates positioning when Viewport mounts and unmounts" | PRV-POS-08, PRV-STA-15, PRV-DOM-11 |
| "positions the popup relative to the hovered line of a multiline trigger" (browser) | PRV-POS-03, PRV-PTR-12 |
| "uses the latest hovered line" (browser, fake timers) | PRV-PTR-12, PRV-STA-13 |
| "keeps the popup aligned after page scroll" (browser) | PRV-POS-04, PRV-PTR-12 |
| "stays anchored to the opened line while already open" (browser) | PRV-PTR-13, PRV-STA-13, PRV-POS-04 |
| "re-anchors to a newly entered line while reopening during close transition" (browser) | PRV-PTR-13, PRV-PTR-09 |
| "positions the popup relative to the side-aligned rect when open is controlled" (browser) | PRV-PTR-14, PRV-POS-03 |
| "clears hovered-line coords after close before controlled reopen of the same trigger" (browser) | PRV-STA-13, PRV-PTR-14, PRV-DOM-06, PRV-DOM-01 |
| "ignores stale hovered coords when a controlled trigger switch reuses the popup" (browser) | PRV-PTR-14, PRV-POS-03, PRV-EVT-04 |
| "uses the hovered line with a custom anchor in a clipped keepMounted portal" (browser) | PRV-POS-09, PRV-API-17, PRV-DOM-09, PRV-POS-01 |
| "positions the popup relative to the side-aligned rect when opened via focus" (browser) | PRV-PTR-14, PRV-API-15, PRV-A11Y-01 |
| "clears hovered-line coords when opened via focus" (browser) | PRV-STA-13, PRV-PTR-14, PRV-EVT-02 |

### `positioner/PreviewCardPositioner.spec.tsx` (type-level)

| Check | Requirements |
|---|---|
| `<PreviewCard.Positioner keepMounted />` is a type error | PRV-API-19 |

### `popup/PreviewCardPopup.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Root open` + `Portal` + `Positioner`) | PRV-STR-05; RND-DOM-36..48 |
| "throws a descriptive error when rendered outside <PreviewCard.Root>" | PRV-EDGE-01 |
| "throws a descriptive error when rendered outside <PreviewCard.Positioner>" | PRV-EDGE-04 |
| "should render the children" | PRV-STR-05 |

### `arrow/PreviewCardArrow.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Root open` + `Portal` + `Positioner` + `Popup`) | PRV-STR-06; RND-DOM-36..48 |

### `backdrop/PreviewCardBackdrop.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, rendered directly inside `Root open`) | PRV-STR-07; RND-DOM-36..48 |
| "sets `pointer-events: none` style" | PRV-DOM-18 |

### `viewport/PreviewCardViewport.test.tsx`

| Test | Requirements |
|---|---|
| describeConformance (`refInstanceof: HTMLDivElement`, inside `Root open` + `Trigger` + `Portal` + `Positioner` + `Popup`) | PRV-STR-08; RND-DOM-36..48 |
| `viewportConformanceTests` (`openOn: 'focus'`, triggers `href="#" delay={0}`): "renders children in the `current` container by default" | PRV-STR-08, PRV-API-20, PRV-DOM-20, PRV-DOM-21 |
| "remounts the `current` container when the active trigger changes" | PRV-DOM-20, PRV-KEY-02 |
| "does not restart a transition when the active trigger remounts while closed and retained (strict: false)" / "(strict: true)" | PRV-ANI-11, PRV-EVT-07 |
| "anchors the popup for side=$side in $direction mode" (12 cases) | PRV-POS-08, PRV-EDGE-13 |
| "creates morphing containers during transitions" (browser) | PRV-ANI-10, PRV-DOM-20, PRV-DOM-21, PRV-DOM-22, PRV-STA-09 |
| "sets `data-activation-direction` from the previous to the new trigger" (browser) | PRV-DOM-21 |
| "creates morphing containers after a kept-mounted popup closes and reopens" (browser) | PRV-ANI-10, PRV-ANI-05 |
| "shows the latest content after rapid trigger changes" / "keeps the latest transition active during rapid trigger changes" (browser) | PRV-ANI-10, PRV-DOM-20 |
| "cleans up the transition when a lagging payload remounts the current container" (browser) | PRV-ANI-10 (PSH-STA-30) |
| "throws a descriptive error when rendered outside <PreviewCard.Positioner>" | PRV-EDGE-04 |

No requirement exists solely because of a test: every behavior the tests assert is already captured in §2–§12.

## 14. React-specific notes and porting considerations

Non-normative.

* **Store subscriptions and bystander triggers.** Each part subscribes to individual store selectors (`store.useState(key, arg)`), and the `triggerProps` selector returns the *same* object for every inactive trigger, which is how PRV-STA-17 ("bystander triggers do not re-render") is achieved. A port with a different reactivity model must still guarantee that an inactive trigger's attributes are value-identical across open/close/switch; the observable is the attribute set, not render counts. Port: see decisions/05-state-store-and-event-details.md §1 (selectors with arguments, one selector subscription per part as the re-render boundary).
* **Interactions as a child component.** `PreviewCardInteractions` is a separate render unit mounted only when `open || mounted || handle` so that `useDismiss` and the prop bags exist on demand (PSH-STA-43). The port equivalent is "dismiss handlers are attached iff that predicate holds"; the document-level listeners additionally require `open`. Port: see decisions/04-js-interop-boundary.md §1 (`interactions` module: dismissal, focus-to-open, hover with safe polygon, floating tree; the popup store stays C#).
* **`flushSync` on hover.** Hover opens/closes are committed synchronously (`ReactDOM.flushSync`, EVD-EVT-18) so `getAnimations()` sees the new attributes within the same task. A port needs an equivalent synchronous commit for hover changes only. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 ("committed synchronously") and §Relaxation table (EVD-EVT-18, PSH-ANI-03 row: on Server the hover commit lands a round trip later, so PRV-PTR-10's same-task observation holds on WebAssembly only).
* **Layout-effect timing.** The forced position update (PRV-POS-05), the payload reset (PRV-STA-12), trigger registration/migration (PSH-STA-26), the active-trigger reconciliation (PSH §4.3.4) and the floating-root sync (PSH-STA-46) are all "post-commit, pre-paint". Their relative order within a commit follows React's child-before-parent layout-effect order: a Trigger registers before the Root reconciles; the handle attachment (`PopupHandleAttachment`, rendered first inside the Root) runs before any descendant's layout effect (PSH-STR-04). The detached-triggers test "keeps a default-open root open while a detached trigger migrates after the initial commit" depends on the trigger-unmount microtask (PRV-DIS-07) running before `useSyncExternalStore`'s passive subscription migrates the trigger; PSH-STA-24's "pending id" rule is what keeps the card open, and a port must preserve that rule rather than the exact scheduling. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 (timing-term mapping) and §Relaxation table (UTL-STA-19..22 row: post-commit C# state work lands after paint on Server); the forced position update (PRV-POS-05) is positioning work in JavaScript, decisions/04-js-interop-boundary.md §1–§2.
* **`useSyncExternalStore` for handles.** Detached triggers read the handle's current store through an external-store subscription with the fallback store as the server snapshot (PRV-EDGE-12). Any mechanism that re-evaluates the trigger when the handle's attached store changes is acceptable. Port: see decisions/05-state-store-and-event-details.md §1 (`PopupHandle<TPayload>` with attached-store stack and `NullStore` fallback, PSH-STA-34..40).
* **Refs.** `triggerElementRef` (the trigger DOM handle given to the hover hook), `popupRef` (the Popup DOM handle watched for animations) and `inlineRectCoordsRef` (mutable, non-reactive coordinates) are plain mutable cells; `arrowRef` is assumed to be stable for the popup's lifetime (`useAnchorPositioning.ts:246-249`). Port: see decisions/02-render-pipeline-and-attribute-merging.md §Requirements relaxed or withdrawn for the port (RND-DOM-20..24: DOM handles); the Popup's animation watch is the `transitions` module, decisions/04-js-interop-boundary.md §1.
* **StrictMode.** Effects are idempotent: registration is a stable callback that ignores re-registration of the same `(store, id, element)`; the handle attachment stack tolerates detach/re-attach (PSH-STA-35); the viewport test runs in both strict and non-strict modes.
* **React 17.** Generated ids may be `undefined` on the first render under the `useId` polyfill; registration then happens from the layout effect once the id resolves (PSH-STA-26). Not normative for a port that has synchronous ids. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 5 (scoped `base-ui-<n>` generator; every part draws its id at first render).
* **Test harness.** The Base UI suite replaces `requestAnimationFrame` with a 0 ms timer under JSDOM, uses fake timers (`clock.withFakeTimers()`, `advanceReactClock`) for the delay tests, and `resetBrowserPointer` so a real cursor does not hover the fixture. Tests marked (browser) need real layout (`getClientRects`, `getAnimations`). Port: see decisions/06-testing-harness.md §1 (lowest observable layer per §13 row) and §2 ("Clocks": `page.clock`/`TimeProvider`, `BASE_UI_ANIMATIONS_DISABLED`; "Pointer realism").
* **Positioning, arrow and portal.** `useAnchorPositioning` drives `@floating-ui/react-dom` (re-rendering on position updates) with the inline-rect middleware first in the pipeline (PRV-POS-01/03); the Arrow's and Positioner's styles and `data-side`/`data-align`/`data-anchor-hidden`/`data-uncentered` come from that session; the Portal renders through `FloatingPortalLite` (`createPortal`). Port: see decisions/03-positioning-engine.md §Decision (vendored `@floating-ui/dom`, Base UI middleware and pipeline assembly in JavaScript) and decisions/04-js-interop-boundary.md §2 (Positioner and Arrow attributes are JS-owned and mirrored into C# state) and §5 (portal by DOM move). The ADRs do not say where the hovered-line coordinates (PRV-STA-13) live: they are captured by the trigger's hover handlers and by the store's `onBeforeDispatch` and read by the inline middleware.

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| PRV-STR-01 | MUST | Root renders no element; owns store; creates floating tree unless nested | 2 |
| PRV-STR-02 | MUST | Trigger is `<a>`; needs Root or handle | 2 |
| PRV-STR-03 | MUST | Portal renders only while mounted or keepMounted; needs Root | 2 |
| PRV-STR-04 | MUST | Positioner is `<div>`; needs Portal and Root | 2 |
| PRV-STR-05 | MUST | Popup is `<div>`; needs Root and Positioner | 2 |
| PRV-STR-06 | MUST | Arrow is `<div>`; needs Root and Positioner | 2 |
| PRV-STR-07 | MUST | Backdrop is `<div>`; needs Root only; structural | 2 |
| PRV-STR-08 | MUST | Viewport is `<div>`; wraps children in containers; optional | 2 |
| PRV-STR-09 | MUST | Many triggers, one active | 2 |
| PRV-STR-10 | MUST | Nested Roots share the floating tree | 2 |
| PRV-STR-11 | MUST | Namespace exports | 2 |
| PRV-API-01 | MUST | `defaultOpen` default false | 3.1 |
| PRV-API-02 | MUST | `open` controlled | 3.1 |
| PRV-API-03 | MUST | `onOpenChange` optional, latest used | 3.1 |
| PRV-API-04 | MUST | `onOpenChangeComplete(open)` | 3.1 |
| PRV-API-05 | MUST | `actionsRef` `{ unmount, close }` | 3.1 |
| PRV-API-06 | MUST | `handle` attaches store; interactions mounted | 3.1 |
| PRV-API-07 | MUST | function child receives `{ payload }` | 3.1 |
| PRV-API-08 | MUST | `triggerId` controlled active trigger | 3.1 |
| PRV-API-09 | MUST | `defaultTriggerId` default null | 3.1 |
| PRV-API-10 | MUST | Trigger `delay` default 600 (hover and focus) | 3.2 |
| PRV-API-11 | MUST | Trigger `closeDelay` default 300 (trigger leave and card leave) | 3.2 |
| PRV-API-12 | MUST | Trigger `handle` precedence and migration | 3.2 |
| PRV-API-13 | MUST | `payload` forwarded with `closeDelay` | 3.2 |
| PRV-API-14 | MUST | Trigger `id` = trigger id, generated fallback | 3.2 |
| PRV-API-15 | MUST | No `href`/`tabindex`/`role` added by the Trigger | 3.2 |
| PRV-API-16 | MUST | Portal `keepMounted` default false | 3.3 |
| PRV-API-17 | MUST | Portal `container` resolution | 3.3 |
| PRV-API-18 | MUST | Positioner defaults | 3.5 |
| PRV-API-19 | MUST | No `keepMounted` on Positioner | 3.5 |
| PRV-API-20 | MUST | Viewport `children` rendered in current container | 3.8 |
| PRV-API-21 | MUST | `createHandle`, `open(id)`, `close()`, `isOpen` | 3.9 |
| PRV-API-22 | MUST | `open(id)` throws for unregistered id | 3.9 |
| PRV-API-23 | MUST | Handle calls ignored without Root; fresh state per mount | 3.9 |
| PRV-STA-01 | MUST | Store adds `instantType`, `adaptiveOrigin`, `closeDelay`, coords ref | 4.1 |
| PRV-STA-02 | MUST | Controlled/uncontrolled open and triggerId | 4.1 |
| PRV-STA-03 | MUST | Root and Portal state empty | 4.2 |
| PRV-STA-04 | MUST | Trigger state `{ open }` = opened by this trigger | 4.2 |
| PRV-STA-05 | MUST | Positioner state fields | 4.2 |
| PRV-STA-06 | MUST | Popup state fields | 4.2 |
| PRV-STA-07 | MUST | Arrow state fields | 4.2 |
| PRV-STA-08 | MUST | Backdrop state fields | 4.2 |
| PRV-STA-09 | MUST | Viewport state fields | 4.2 |
| PRV-STA-10 | MUST | `instantType` transitions by reason | 4.3 |
| PRV-STA-11 | MUST | Store `closeDelay` = active trigger's | 4.3 |
| PRV-STA-12 | MUST | Payload cleared when open without active trigger | 4.3 |
| PRV-STA-13 | MUST | Hovered-line coordinates lifecycle | 4.3 |
| PRV-STA-14 | MUST | Close on active trigger unmount enabled | 4.3 |
| PRV-STA-15 | MUST | `adaptiveOrigin` set while Viewport mounted | 4.3 |
| PRV-STA-16 | MUST | Interactions mounted iff open, mounted or handle | 4.3 |
| PRV-STA-17 | MUST | Inactive triggers' attributes stable (no bystander re-render) | 4.3 |
| PRV-STA-18 | MUST | Default-open Root stays open while detached trigger migrates | 4.3 |
| PRV-DOM-01 | MUST | Trigger `id` attribute | 5.1 |
| PRV-DOM-02 | MUST | `data-popup-open` on active trigger while open | 5.1 |
| PRV-DOM-03 | MUST | No other trigger state attributes; always "enabled" | 5.1 |
| PRV-DOM-04 | MUST | Portal node `<div id data-base-ui-portal>`; no guards | 5.2 |
| PRV-DOM-05 | MUST | Positioner `role="presentation"` | 5.3 |
| PRV-DOM-06 | MUST | Positioner `hidden` while unmounted | 5.3 |
| PRV-DOM-07 | MUST | Positioner `pointer-events: none` while not open | 5.3 |
| PRV-DOM-08 | MUST | Positioner `transition: none` while starting; no transition attrs | 5.3 |
| PRV-DOM-09 | MUST | Positioner data attributes incl. `data-instant` | 5.3 |
| PRV-DOM-10 | MUST | Positioner CSS variables | 5.3 |
| PRV-DOM-11 | MUST | Positioner positioning styles | 5.3 |
| PRV-DOM-12 | MUST | Popup `tabindex=-1`, `data-base-ui-focusable` | 5.4 |
| PRV-DOM-13 | MUST | Popup data attributes | 5.4 |
| PRV-DOM-14 | MUST | Popup `transition: none` while starting | 5.4 |
| PRV-DOM-15 | MUST | Popup has no role and no generated id | 5.4 |
| PRV-DOM-16 | MUST | Arrow inline position styles; registered arrow | 5.5 |
| PRV-DOM-17 | MUST | Arrow data attributes incl. `data-uncentered` | 5.5 |
| PRV-DOM-18 | MUST | Backdrop role, hidden, fixed styles | 5.6 |
| PRV-DOM-19 | MUST | Backdrop data attributes | 5.6 |
| PRV-DOM-20 | MUST | Viewport current/previous containers | 5.7 |
| PRV-DOM-21 | MUST | Viewport data attributes | 5.7 |
| PRV-DOM-22 | MUST | `--popup-width/height` on previous container | 5.7 |
| PRV-ARIA-01 | MUST | Arrow `aria-hidden="true"` | 5.8 |
| PRV-ARIA-02 | MUST | Trigger has no ARIA popup attributes | 5.8 |
| PRV-ARIA-03 | MUST | Popup has no role; no relationship exposed | 5.8 |
| PRV-KEY-01 | MUST | Visible focus opens after `delay` | 6.1 |
| PRV-KEY-02 | MUST | Focus switch between triggers while open is immediate | 6.1 |
| PRV-KEY-03 | MUST | Tab not intercepted; no trap | 6.1 |
| PRV-KEY-04 | MUST | Focus blocked on same trigger after Escape | 6.1 |
| PRV-KEY-05 | MUST | Enter/Space not handled | 6.1 |
| PRV-PTR-01 | MUST | Mouse enter opens after `delay` | 6.2 |
| PRV-PTR-02 | MUST | Touch never opens by hover | 6.2 |
| PRV-PTR-03 | MUST | Leave before open cancels | 6.2 |
| PRV-PTR-04 | MUST | Trigger leave: safe polygon then close after `closeDelay` | 6.2 |
| PRV-PTR-05 | MUST | Re-enter cancels pending close silently | 6.2 |
| PRV-PTR-06 | MUST | Positioner leave closes only hover-opened cards | 6.2 |
| PRV-PTR-07 | MUST | Positioner enter cancels pending close | 6.2 |
| PRV-PTR-08 | MUST | Hovering another trigger while open switches immediately | 6.2 |
| PRV-PTR-09 | MUST | Re-enter during hover-close exit reopens immediately | 6.2 |
| PRV-PTR-10 | MUST | Hover changes committed synchronously | 6.2 |
| PRV-PTR-11 | MUST | Click not intercepted; native navigation | 6.2 |
| PRV-PTR-12 | MUST | Hovered line recorded while closed | 6.2 |
| PRV-PTR-13 | MUST | Anchor line frozen while open; re-anchor during exit | 6.2 |
| PRV-PTR-14 | MUST | Side-aligned line when no coordinates | 6.2 |
| PRV-PTR-15 | MUST | No pointer-events blocking by safe polygon | 6.2 |
| PRV-PTR-16 | MUST | Press on nested-popup trigger inside disables hover close | 6.2 |
| PRV-FOC-01 | MUST | Never moves focus; no trap | 6.3 |
| PRV-FOC-02 | MUST | Popup focusable; focus inside keeps open | 6.3 |
| PRV-FOC-03 | MUST | Window blur blocks next focus-open | 6.3 |
| PRV-A11Y-01 | MUST | Focus-visible heuristic | 6.3 |
| PRV-DIS-01 | MUST | Escape closes; preventDefault/stopPropagation rules | 6.4 |
| PRV-DIS-02 | MUST | Nested Escape: innermost only | 6.4 |
| PRV-DIS-03 | MUST | Outside pointerdown (non-touch) closes | 6.4 |
| PRV-DIS-04 | MUST | Outside touch rules | 6.4 |
| PRV-DIS-05 | MUST | Presses that never close; outside press bubbles | 6.4 |
| PRV-DIS-06 | MUST | Blur closes after 0 ms with exceptions | 6.4 |
| PRV-DIS-07 | MUST | Active trigger unmount closes (reason none) | 6.4 |
| PRV-DIS-08 | MUST | Nested hover closing synchronized | 6.4 |
| PRV-DIS-09 | MUST | What does not close | 6.4 |
| PRV-DIS-10 | MUST | Controlled decline keeps open | 6.4 |
| PRV-DIS-11 | MUST | Imperative close reason | 6.4 |
| PRV-EVT-01 | MUST | `onOpenChange` once per request, before state | 7 |
| PRV-EVT-02 | MUST | Reason / event / trigger table | 7 |
| PRV-EVT-03 | MUST | `trigger-press` declared, never emitted | 7 |
| PRV-EVT-04 | MUST | `details.trigger` on opens only | 7 |
| PRV-EVT-05 | MUST | `details.event` types | 7 |
| PRV-EVT-06 | MUST | `cancel()` semantics | 7 |
| PRV-EVT-07 | MUST | `preventUnmountOnClose()` | 7 |
| PRV-EVT-08 | MUST | When `onOpenChange` is not called | 7 |
| PRV-EVT-09 | MUST | Trigger switch: one `true`, no `false` | 7 |
| PRV-EVT-10 | MUST | `onOpenChangeComplete` timing | 7 |
| PRV-EVT-11 | MUST | `handle.open` callback and effect | 7 |
| PRV-ANI-01 | MUST | Portal subtree iff mounted or keepMounted | 9 |
| PRV-ANI-02 | MUST | Default status machine | 9 |
| PRV-ANI-03 | MUST | Transition attributes on Popup/Backdrop/Viewport containers only | 9 |
| PRV-ANI-04 | MUST | Exit kept mounted until Popup animations finish; cleanup order | 9 |
| PRV-ANI-05 | MUST | keepMounted hidden/reuse/reposition | 9 |
| PRV-ANI-06 | MUST | `actionsRef.unmount()` | 9 |
| PRV-ANI-07 | MUST | `data-instant` is the only animation hint | 9 |
| PRV-ANI-08 | MUST | Reopen during exit | 9 |
| PRV-ANI-09 | MUST | Initially open: no enter phase | 9 |
| PRV-ANI-10 | MUST | Viewport content morph | 9 |
| PRV-ANI-11 | MUST | No morph on trigger remount while closed | 9 |
| PRV-POS-01 | MUST | Positioning configuration passed | 10 |
| PRV-POS-02 | MUST | Floating element = Positioner; anchor = active trigger | 10 |
| PRV-POS-03 | MUST | Inline-line anchoring algorithm | 10 |
| PRV-POS-04 | MUST | Anchor tracking | 10 |
| PRV-POS-05 | MUST | Position update on each open commit | 10 |
| PRV-POS-06 | MUST | Logical side/align/anchorHidden outputs | 10 |
| PRV-POS-07 | MUST | Arrow positioning, `uncentered`, stand-in | 10 |
| PRV-POS-08 | MUST | Adaptive origin with Viewport | 10 |
| PRV-POS-09 | MUST | Custom `anchor` still inline-anchored | 10 |
| PRV-POS-10 | MUST | Invisible until positioned; `--transform-origin` | 10 |
| PRV-EDGE-01 | MUST | Root-context error (verbatim) | 12 |
| PRV-EDGE-02 | MUST | Trigger without Root/handle error (verbatim) | 12 |
| PRV-EDGE-03 | MUST | Portal-missing error (verbatim) | 12 |
| PRV-EDGE-04 | MUST | Positioner-context error (verbatim) | 12 |
| PRV-EDGE-05 | MUST | `handle.open` unregistered id error (verbatim) | 12 |
| PRV-EDGE-06 | SHOULD | Handle no-root warnings | 12 |
| PRV-EDGE-07 | SHOULD | Multi-root overlap warning | 12 |
| PRV-EDGE-08 | SHOULD | Duplicate registration error (dev) | 12 |
| PRV-EDGE-09 | SHOULD | Controlled-mode switch error | 12 |
| PRV-EDGE-10 | MUST | No `disabled`; cancel instead | 12 |
| PRV-EDGE-11 | SHOULD | `instantType` persists across unmapped reasons (suspected bug, §16 Q4) | 12 |
| PRV-EDGE-12 | MUST | Server rendering / hydration | 12 |
| PRV-EDGE-13 | MUST | RTL logical sides | 12 |

## 16. Open questions

1. **`trigger-press` is declared but has no emitter.** `preview-card/root/PreviewCardRoot.tsx:194` lists it in `ChangeEventReason`, but the dismiss mechanism is configured with no `referencePress` (`PreviewCardRoot.tsx:98`; default `alwaysFalse`, `floating-ui-react/hooks/useDismiss.ts:122,187-199`) and nothing else emits it. The shared open-change routine still maps a `trigger-press` close to `instantType 'dismiss'` (`utils/popups/popupStoreUtils.ts:268-269`), unreachable here. Documented as PRV-EVT-03 (keep the literal, never emit). Agrees with EVD §10 Q3. Re-verified (QA 2026-10-14) as the second reader EVD §10 Q3 asks for: the only `trigger-press` emitters are `floating-ui-react/hooks/useClick.ts` (not used by Preview Card) and the dismiss reference press (`useDismiss.ts:187-199`, gated by `referencePress`, which `PreviewCardRoot.tsx:98` leaves at its always-false default); the handle and Root actions use `imperative-action` (`PreviewCardRoot.tsx:76`, `utils/popups/popupHandle.ts:52,261,284`).
2. **Orchestrator hint vs source: `delay`/`closeDelay` are Trigger props, not Root props.** They moved in v1.0.0-beta.5 (`CHANGELOG.md` "Move delay props to trigger"); `PreviewCardRoot.tsx:133-184` has no such props. The document places them on the Trigger (PRV-API-10/11). The store's `closeDelay` is per active trigger (PRV-STA-11).
3. **Outline vs source.** `components/page.mdx` "Preview Card": (a) Positioner and Popup data attributes omit `data-instant`, which both render through the default projection of their `instant` state field (`PreviewCardPositioner.tsx:93`, `PreviewCardPopup.tsx:55`; only the Viewport's `data-instant` is listed). Documented as PRV-DOM-09/13. (b) Trigger props omit `id` and the native `<a>` attributes (`href`, …) that the component accepts and relies on (PRV-API-14/15). (c) `PreviewCard.Root.State` is exported as a type but is an empty interface (`PreviewCardRoot.tsx:131`). The outline is incomplete rather than the source wrong. Resolution 2026-10-29 (M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); chosen by the user 2026-10-27, ADR 04 Amendments 2026-10-27, as Tooltip's §16 Q2): every part's state carries `Instant`, and C# renders `data-instant` on the Positioner, the Popup and the Viewport through the default projection (`PreviewCardInstantType`), never on the Arrow (PRV-DOM-09/13/17; `PreviewCardRootTests` "port: a focus open renders data-instant=…").
4. **`instantType` is never reset by non-mapped reasons.** `utils/popups/popupStoreUtils.ts:296-302` only writes `instantType` for focus opens, press/Escape closes and hover changes; an `outside-press`, blur (`trigger-focus` close), `imperative-action` or `none` close leaves the previous value, so `data-instant="focus"` can persist on a card dismissed by an outside press and even on the *next* open if that open is not hover/focus (e.g. `handle.open`). Shared with [tooltip](tooltip.md) §16 Q13 (same `applyPopupOpenChange` path, TIP-STA-05); [popover](popover.md) §16 Q11 shares only the controlled-`open={false}` residue, because `PopoverStore.setOpen` rewrites `instantType` on every applied change. Consumer prop changes (controlled `open={false}`, which never reaches `setOpen`) leave the value in all three. Documented as PRV-STA-10/PRV-EDGE-11; confirm whether a port should reproduce the stale value or whether Base UI considers it a bug. Resolution 2026-10-29 (M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 04 Amendments 2026-10-18, an untested quirk without a requirement against it is mirrored): replicated. A change with an unmapped reason keeps the stale value (`PreviewCardStoreTests` "port: a change with an unmapped reason keeps the stale instant type").
5. **Suspected spurious close request on a first hover-leave before any open.** On `mouseleave` while closed, when no positioning session has ever run (no `keepMounted`, card never opened), `dataRef.current.floatingContext` is undefined (`floating-ui-react/hooks/useFloating.ts:113` is only reached once a Positioner renders), so `useHoverReferenceInteraction.ts:321-359` skips the safe-polygon branch and calls `closeWithDelay(event)`: the pending open timer is replaced by a 300 ms timer that calls `store.setOpen(false, trigger-hover)` on an already-closed card (`:194-206`). `applyPopupOpenChange` has no equality guard (`popupStoreUtils.ts:273`), so `onOpenChange(false, …)` fires for a card that never opened. Not asserted by any test; the observable intent (PRV-PTR-03: the open is cancelled) is documented, the extra callback is not made a requirement. Shared with [tooltip](tooltip.md) §16 Q14 and [popover](popover.md) §16 Q12 (with `openOnHover`), where the default close delay is 0, so the spurious close is immediate instead of after 300 ms; Tooltip additionally hits it on every hover-leave of a closed tooltip when no safe-polygon strategy is configured. Preview Card always configures `safePolygon()` (`PreviewCardTrigger.tsx:73`), so only the never-rendered-Positioner case applies. A port should cancel the pending open without emitting a close. Resolution 2026-10-29 (M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): resolved for every popup family by FLT §10 item 12 (ADR 04 Amendments 2026-10-27, chosen by the user 2026-10-27). A trigger `mouseleave` on a closed card cancels the pending open only and requests no close.
6. **`closeDelay` in the store is never reset** (`preview-card/store/PreviewCardStore.ts:127`; cleared nowhere). Only observable if a hover-opened card's active trigger changes to one that never forwards (controlled `triggerId` set to an id whose trigger is not mounted); in practice the Positioner-leave close uses the last forwarding trigger's `closeDelay`. Documented in PRV-STA-11. Resolution 2026-10-29 (M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122); ADR 04 Amendments 2026-10-18, mirrored): replicated. The forwarded close delay is written only when it changes and never reset (`PreviewCardStoreTests` "port: the forwarded close delay is written only when it changes and is not reset on close").
7. **Nested-tree detection is component-local.** `PreviewCardRoot.tsx:120` joins an existing floating tree only when inside another *Preview Card* Root; a Preview Card nested inside a Popover/Menu popup creates its own tree, so cross-component nested-dismissal coordination relies solely on the `nested` flag / parent node id from the floating-node context (`popupStoreUtils.ts:77`). Escape and outside-press interplay with a non-Preview-Card parent is therefore undefined by Preview Card tests (Popover/Menu docs may cover it). Port note 2026-10-29 (M4 track 7, [issue 122](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/122)): the port keeps the component-local join. A nested `PreviewCardRoot` names the enclosing card's session (`ParentToken`) only when it reads an outer `PreviewCardRootContext`, as Popover's Root does for Popover; a card inside another family's popup starts its own session tree.
8. **PRV-DIS-02 (nested Escape) and PRV-DIS-04 (touch outside press) have no Preview Card test**; both are shared-mechanism behaviors asserted in the dismiss hook's own tests (`floating-ui-react/hooks/useDismiss.test.tsx`, FLT). Listed so a port's test translation knows the gap.
9. **Backdrop placement.** The anatomy puts `Backdrop` inside `Portal`, but it only needs a Root (`PreviewCardBackdrop.tsx:21`; conformance renders it directly in `Root open`). A Backdrop placed outside the Portal stays in the document permanently with `hidden` while unmounted; inside the Portal it is removed with the subtree. Both are valid; the document describes the mechanism (PRV-STR-07, PRV-DOM-18) rather than mandating the placement.
10. **Portal context value is the raw `keepMounted` boolean** (`PreviewCardPortalContext.ts:4-12`); a Positioner nested under a *different* popup's portal (e.g. a Preview Card inside a Popover popup without its own `PreviewCard.Portal`) still throws PRV-EDGE-03 because the context is component-specific. Intentional per the v1.0.0-alpha.5 "Require `Portal` part" change; noted for ports that might share a portal context across components.
11. **Root `disabled` + Trigger `disabled={false}` double `onOpenChange` ([tooltip](tooltip.md) §16 Q6; [popover](popover.md) §16 Q13) — does not apply.** Preview Card has no `disabled` prop on any part (PRV-EDGE-10). Its hover and focus interactions are always enabled (`preview-card/trigger/PreviewCardTrigger.tsx:70-80`), and no Root effect reverts an open, so the open-then-`disabled`-close pair cannot occur.
12. **Substitute callbacks and owner-computed IDREFs (rendering-and-composition §10 Q8) — not applicable.** Preview Card computes no IDREF attributes: the Trigger has no `aria-controls`/`aria-describedby` (PRV-ARIA-02), the Popup has no `aria-labelledby`, and the lite portal renders no `aria-owns` owner (PRV-DOM-04). A substitute callback that drops `id` therefore cannot leave a dangling reference. Contrast [popover](popover.md) POP-EDGE-15.
