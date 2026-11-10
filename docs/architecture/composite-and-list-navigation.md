# Composite and list navigation

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/internals/composite/**` (`composite.ts`, `constants.ts`, `root/useCompositeRoot.ts`, `root/CompositeRoot.tsx`, `root/CompositeRootContext.ts`, `root/gridNavigation.ts`, `item/CompositeItem.tsx`, `item/useCompositeItem.ts`, `list/CompositeList.tsx`, `list/CompositeListContext.ts`, `list/useCompositeListItem.ts`), `internals/useItemRegistry.ts`, `floating-ui-react/utils/composite.ts`, `floating-ui-react/hooks/gridNavigation.ts`, `floating-ui-react/hooks/useListNavigation.ts`, `floating-ui-react/hooks/useTypeahead.ts`, `utils/getHighlightReason.ts` |
| Tests | `internals/composite/composite.test.ts`, `internals/composite/root/CompositeRoot.test.tsx`, `internals/composite/list/CompositeList.test.tsx`, `internals/useItemRegistry.test.tsx`, `floating-ui-react/utils/composite.test.ts`, `floating-ui-react/hooks/useListNavigation.test.tsx`, `floating-ui-react/hooks/useListNavigation.webkit.test.tsx`, `floating-ui-react/hooks/useTypeahead.test.tsx`, `packages/react/test/popupListConformanceTests.tsx` |
| Prefix | `CMP` |
| Status | approved |
| Revised | 2026-11-04 (M5 track 1) |
| Used by | accordion (item registration), autocomplete, combobox (list navigation, typeahead on trigger, chips/items registration, highlight reason), menu (list navigation, typeahead, items registration, trigger as composite item, keyboard relay from menubar), menubar (roving group), navigation-menu (roving group, links/triggers as items), radio-group / radio (roving group, active item marker), select (list navigation, typeahead, items registration), tabs (roving group, active item marker, panels registration), toggle-group / toggle (roving group), toolbar (roving group: buttons, links, inputs), otp-field (input registration), slider (thumb registration), button-behavior (`useButton` infers "composite item" mode from the roving-group context) |

## 1. Purpose

Base UI has two keyboard-navigation models that share one set of index helpers:

1. **Roving tabindex groups** ("composites"). A group of sibling controls that behaves as one tab stop: exactly one item has `tabindex="0"`, all others `tabindex="-1"`, and arrow keys move both focus and the tab stop between items. Used by Tabs, RadioGroup, ToggleGroup, Toolbar, Menubar, NavigationMenu and (through `useButton`) by every button rendered inside one of those groups. Source: `internals/composite/**`.
2. **List navigation inside popups**. A trigger (reference element) opens a floating element containing items; arrow keys move an *active index* across the items, either by moving real DOM focus to the item or by keeping focus on the trigger and pointing at the item with `aria-activedescendant` ("virtual focus"). Typeahead (typing an item's label) moves the active index too. Used by Menu, Select, Combobox and Autocomplete. Source: `floating-ui-react/hooks/useListNavigation.ts`, `useTypeahead.ts`.
   Fidelity note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159), from M5 track 5's fact sheet F, contradiction 2; conventions §3.5, not a decision): NavigationMenu was listed among the users of list navigation and typeahead, but no file under `navigation-menu/` imports `useListNavigation` or `useTypeahead`; NavigationMenu uses the roving group (item 1, and the "Used by" row: "roving group, links/triggers as items").

Both models rely on an **item registry** (`CompositeList`) that assigns a stable integer index to every item in DOM order, keeps an array of item elements and an array of item labels addressed by that index, and republishes when items mount, unmount or move. Index resolution (which item is "next", which indices are disabled, how a grid wraps) lives in `floating-ui-react/utils/composite.ts` and is shared verbatim by both models.

A port cannot skip this mechanism: every component doc for the components listed above states only *which options it passes* to these mechanisms (see §6) and defers all keyboard, focus and pointer behavior to this document. The shared conformance suite `test/popupListConformanceTests.tsx` is run against Menu, Select and Combobox and is translated into requirements here (§4) so that a port of any of those components can be checked against it.

## 2. Concepts and terminology

| Base UI / React name | Neutral term used here | Meaning |
|---|---|---|
| `CompositeRoot` / `useCompositeRoot` | **composite root** | The container of a roving-tabindex group. Owns the highlighted index and handles arrow/Home/End keys for the whole group. |
| `CompositeItem` / `useCompositeItem` | **composite item** | A control inside a composite root. Receives `tabindex` 0 or -1, reports focus and hover to the root. |
| `CompositeList` | **item registry** | Ancestor-provided shared state that assigns an index to each registered item element, maintains the *elements array* and the optional *labels array*, and publishes an *item map* when membership or order changes. `CompositeRoot` always renders one; popups render one directly. |
| `useCompositeListItem` | **item registration** | The per-item call that registers an element (with optional explicit index, label, text element and metadata) and receives its resolved index. |
| `elementsRef` / `listRef` | **elements array** | Array of item elements ordered by index; explicit indexes may leave `null`/empty slots. The list-navigation and typeahead mechanisms read it. |
| `labelsRef` | **labels array** | Array of `string | null` ordered by index; the typeahead reads it. |
| `onMapChange` map / `CompositeMetadata` | **item map** | `Map<element, metadata & { index }>` published after each registry change, ordered by index. |
| `highlightedIndex` (composite) | **highlighted index** | Index of the item that currently holds the single tab stop (`tabindex="0"`). Default `0`. |
| `data-composite-item-active` (`ACTIVE_COMPOSITE_ITEM`) | **active item marker** | Attribute a component puts on the item that should receive the initial tab stop (the selected tab, the checked radio). |
| `activeIndex` (list navigation) | **active index** | Index of the item currently highlighted inside a popup; `null` when nothing is highlighted. Owned by the consumer; the mechanism proposes changes through `onNavigate`. |
| `indexRef` | **cursor** | The mechanism's private copy of the active index (`-1` when none), from which the next key moves. |
| `selectedIndex` | **selected index** | Index of the item that is the popup's current value, if any; `null` otherwise. |
| `virtual` | **virtual focus** | Focus model where real DOM focus stays on the reference element and the active item is announced with `aria-activedescendant`. The opposite is **DOM focus** (the item element itself is focused). |
| reference / floating / item / trigger props | **element roles** | The four groups of attributes/handlers the list-navigation mechanism emits. *Reference*: the element that opens the popup and keeps focus under virtual focus (also receives the trigger handlers). *Floating*: the popup element. *Item*: each list item. *Trigger*: the opening element when it differs from the reference. |
| `floatingFocusElement` | **popup focus element** | The element inside the floating element marked with `data-base-ui-focusable`, or the floating element itself when none is marked. The element that receives focus when no item is highlighted. |
| `typeableComboboxReference` | **typeable combobox reference** | A reference element with `role="combobox"` that is a text-entry element (`input` other than hidden/disabled, `textarea`, or `contenteditable`). Home/End and the floating `aria-activedescendant` are suppressed for it. |
| `orientation` | **orientation** | `'vertical'` (Up/Down move), `'horizontal'` (Left/Right move) or `'both'`. Composite default `'both'`; list navigation default `'vertical'`. |
| forward key / backward key | **forward / backward** | The key that moves toward the end / start of the list for the current orientation and direction (§4.6 tables). |
| main-orientation key, cross-orientation open/close key | see §4.6 | Predicates on a key given orientation and direction, used by list navigation for nested popups. |
| `rtl` / `direction` | **direction** | `'ltr'` or `'rtl'`. Composite roots read it from the ancestor `DirectionProvider` (owned by [shared-utilities](shared-utilities.md)); list navigation takes a boolean `rtl` option. |
| `disabledIndices` | **disabled indices** | Either an array of indices or a predicate `(index) => boolean` naming items that navigation must skip. When *absent*, disabled state is read from the DOM (`disabled` attribute or `aria-disabled="true"`); when *present*, DOM `aria-disabled` is ignored but natively `:disabled` elements are still skipped. |
| `loopFocus` | **looping** | Whether stepping past either end wraps to the other end. Composite default `true`; list navigation default `false`. |
| `allowEscape` | **escaping** | List navigation only: stepping past either end clears the active index (`null`) instead of wrapping. Requires looping and virtual focus. |
| `grid` | **grid navigation** | A pluggable navigator that interprets Up/Down as row moves (and, for orientation `'both'`, Left/Right as moves confined to the row). Composite: `gridNavigation({ cols, dense, itemSizes })` with multi-cell items. Popups: `gridNavigation(...)` for uniform 1×1 cells. |
| cell map | **cell map** | Array mapping each 1×1 grid cell index to the item index occupying it (multi-cell items occupy several cells). |
| pointer modality / keyboard modality | **input modality** | Whether the last input was a pointer move or a key press. Controls scroll-into-view and pointer-leave resets. |
| `focusItemOnOpen: 'auto'` | **auto focus-on-open** | Highlight the first/last item when the popup was opened by keyboard or by a screen-reader synthesized click, but not by a real pointer. |
| typeahead buffer / session | **typeahead buffer** | The string of characters typed within `resetMs` (750 ms) of each other. A session starts when the buffer is empty. |
| `onLoop` | **loop hook** | Composite callback invoked when a step wraps; may substitute the destination index. |
| `relayKeyboardEvent` | **keyboard relay** | Function exposed by a composite root so that keyboard events originating outside its DOM subtree (a portaled menu opened from a menubar) can be fed to its key handler. |
| `useItemRegistry` | **keyed registry** | A simpler registry (`Map<key, item>`) that publishes one immutable snapshot per commit. In v1.9.0 only the unexported `filter-dropdown` uses it; documented for completeness. |

## 3. Public surface

None of the mechanisms in this document is exported from `packages/react/src/index.ts` (`CompositeRoot`, `CompositeItem`, `CompositeList`, `useListNavigation`, `useTypeahead`, `gridNavigation` are internal; `floating-ui-react` is not a public entry point; Source: `index.ts` contains no `composite` or `floating-ui-react` export). `DirectionProvider` / `useDirection` are owned by [shared-utilities](shared-utilities.md); their effect here is specified in §4.10 (I18N).

Three public props appear on many components and have their semantics defined by this mechanism. Component docs cite the IDs below and state only their own default and which mechanism (composite root or list navigation) receives the value.

| Prop | Type | Carried by | Semantics |
|---|---|---|---|
| `loopFocus` | `boolean` | Menubar, Tabs, ToggleGroup, Toolbar, RadioGroup, NavigationMenu (composite root `loopFocus`); Menu, Select, Combobox, Autocomplete (list navigation `loopFocus`) | CMP-API-01 |
| `orientation` | `'horizontal' \| 'vertical' \| 'both'` (`'both'` only where the component allows it) | Menubar, Tabs, ToggleGroup, Toolbar, RadioGroup, NavigationMenu (composite root `orientation`); Menu, Select, Combobox (list navigation `orientation`) | CMP-API-02 |
| `highlightItemOnHover` | `boolean` | Menubar (composite root `highlightItemOnHover`); Menu, Select, Combobox, Autocomplete (list navigation `focusItemOnHover`) | CMP-API-03 |

- **CMP-API-01** — MUST. When `loopFocus` is `true`, moving forward from the last navigable item wraps to the first navigable item and moving backward from the first wraps to the last; when `false`, the highlight stays on the end item (the key is still consumed in list navigation; in a composite the key is not consumed because the index does not change, see CMP-KEY-11).
  Source: `internals/composite/root/useCompositeRoot.ts:282-300`, `floating-ui-react/utils/composite.ts:66-77`. Test: [L3] `CompositeRoot.test.tsx` "does not loop or call onLoop when loopFocus is disabled"; `useListNavigation.test.tsx` "ArrowDown looping", "navigates down on ArrowDown" (L4 for the consumption clause). Docs: `CHANGELOG.md v1.1.0 "Add \`loopFocus\` prop (#3592)"`, `CHANGELOG.md v1.0.0-beta.5 "Rename \`loop\` to \`loopFocus\` (#3186)"`.
  Port note (2026-10-22, M2 track 3, issue 79; ADR 12 Amendments 2026-10-22 item 3): the composite clause (looping on, the RadioGroup default, and off) runs at L3; the `useListNavigation.test.tsx` titles are M5.
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList` (`LoopFocus`) by `tests/browser/tabs.spec.ts` "moves focus to the last tab without activating it if focus is on the first tab" (looping), "does not wrap focus past the first tab when `loopFocus` is false" and "does not wrap focus past the last tab when `loopFocus` is false", both modes.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): the M5 clause is green at L3: `js/composite-list.test.ts` "ArrowDown looping" and "navigates down on ArrowDown", the list's looping set by `CompositeListOptions.LoopFocus`. The L4 consumption clause is CMP-KEY-30's statement, green at L4 by the popup-list suite (CMP-KEY-30's note); a main key at a list end without looping is consumed at L3 in the grid's "does not wrap ArrowUp when loopFocus is false for virtualized rows" and at L4 in `tests/browser/composite-list-rows.spec.ts` "port: without loopFocus the main key at an end of the list is consumed: its default is prevented, it does not propagate and the highlight stays" (Select's options, both modes; PR 170's review, fix 4).
- **CMP-API-02** — MUST. `orientation` selects which arrow keys move the highlight: `'vertical'` → <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>; `'horizontal'` → <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> (swapped under RTL, §4.10); `'both'` → all four, with the vertical pair and the horizontal pair each acting as backward/forward on the same one-dimensional list unless grid navigation is configured. Keys outside the orientation are not handled by the mechanism (composite: not consumed; list navigation: ignored, except that under grid navigation vertical keys are always handled).
  Source: `internals/composite/root/useCompositeRoot.ts:223-226,267-272`, `floating-ui-react/hooks/useListNavigation.ts:41-63`. Test: [L3] `CompositeRoot.test.tsx` "sets default prevention to $prevented for $key with $orientation orientation"; `useListNavigation.test.tsx` "navigates down on ArrowRight", "navigates up on ArrowLeft" (L4 for the consumption clause).
  Port note (2026-10-22, M2 track 3, issue 79; ADR 12 Amendments 2026-10-22 item 3): the composite clause runs at L3 and L4 for the three orientations; the `useListNavigation.test.tsx` titles are M5. The option is the literal `orientation` (`horizontal`, `vertical`, `both`); the M3 roots pass a non-nullable `Orientation` with the family default; no M3 family defaults to `both`.
  Port note (2026-10-26, M3 track 1, issue 100; ADR 08 Amendments 2026-10-26, chosen by the user 2026-10-26): corrected. The earlier wording read "the M3 public props map `Orientation?` to it, `null` being `both`"; ToggleGroup, Toolbar and Tabs default to `Horizontal` and Accordion to `Vertical`, each passed as `CompositeLiterals.ToLiteral(Orientation)`. `CompositeLiterals.ToLiteral(null)` still gives `both`, which the test-only roots use; RadioGroup sets no orientation and sends the mechanism default `both` (CMP §10 Q11).
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsRoot` (`Orientation`, non-nullable, default `Horizontal`) by `tests/browser/tabs.spec.ts` "keyboard navigation when focus is on a tab" in a horizontal ltr, a horizontal rtl and a vertical tablist, both modes; `TabsListTests` "port: the list configures the composite root for tabs" at L2.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): the M5 clause is green at L3: `js/composite-list.test.ts` "navigates down on ArrowRight" and "navigates up on ArrowLeft", the list's orientation set by `CompositeListOptions.Orientation`. The L4 consumption clause is green on both modes: the main keys are consumed in CMP-KEY-30's suite titles (its note), and a key outside the orientation is left alone by the list in `tests/browser/composite-list-rows.spec.ts` "port: a keydown the portaled list does not stop is relayed to the composite root, which moves to its next item and stops it" (ArrowRight on a vertical list).
- **CMP-API-03** — MUST. When `highlightItemOnHover` is `true`, moving the pointer over an item makes it the highlighted/active item (composite: focuses it; list navigation: sets the active index and, under DOM focus, focuses it); when `false`, pointer movement over items has no effect on the highlight.
  Source: `internals/composite/item/useCompositeItem.ts:29-39`, `floating-ui-react/hooks/useListNavigation.ts:699-708`, `menubar/Menubar.tsx:89`, `menu/root/MenuRoot.tsx:544`, `select/root/SelectRoot.tsx:385`, `combobox/root/AriaCombobox.tsx:1427`. Test: [L3] `useListNavigation.test.tsx` "true - focuses item on hover and syncs the active index", "false - does not focus item on hover and does not sync the active index" (L4 for the focus clause). Docs: `CHANGELOG.md v1.2.0 "Fix \`highlightItemOnHover\` not being respected (#3868)"`.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): the list-navigation clause is green at L3: `js/composite-list.test.ts` "true - focuses item on hover and syncs the active index" and "false - does not focus item on hover and does not sync the active index" (`CompositeListOptions.FocusItemOnHover`); the composite clause is CMP-PTR-01's, green since M2. The focus clause is green at L4: `tests/browser/composite-list-rows.spec.ts` "true - focuses item on hover and syncs the active index" and "false - does not focus item on hover and does not sync the active index", both modes.

All other option names that component documents reference are tabulated in §6.

## 4. Behavioral requirements

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

* CMP-STA-10 — the registry flush (post-commit, pre-paint C# state work) lands after paint plus a round trip; observable only as latency of dependent attribute updates (`tabindex`, published indexes).
* EVD-EVT-26 as it applies to composite key handling (CMP-EVT-12) — on Server the composite handles the key regardless of a C# consumer's `PreventBaseUIHandler()` in `onKeyDown` (`decide()` fallback "run"); WebAssembly honours synchronous consumer handlers through `decide()` (ADR 04 §3). Amended 2026-10-23 (ADR 01 Amendments 2026-10-23, the user's choice of option (i) of ADR 04's note of 2026-10-22): on Server the consumer's C# `onkeydown` on the focused item and on the root then runs a round trip later, from the module's handled-key report, and an ancestor's never runs.
  The list handles join this row (added 2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 01 Amendments 2026-11-04): `navigate` asks before a list key's first effect; on WebAssembly the consumer's C# `onkeydown` runs inside the keydown and its veto keeps the highlight; on Server the module moves the highlight at once and the handler runs a leg later (158 ms at 75 ms one-way), so the veto cannot apply. The families add their IDs.
* CMP-STA-06, CMP-STA-07, CMP-STA-17 — row "EVD-STA-07, CMP-STA-10, FFI registration" (added 2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 01 Amendments 2026-11-04): an item a re-render adds, with its label and explicit index, reaches the module's order report one leg after the render on Server; on WebAssembly the report goes out in the render's own task.
* CMP-STA-31, CMP-STA-34, CMP-STA-35 — row "RND-STA-01 for mirrored fields" (added 2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 01 Amendments 2026-11-04): the module writes `data-highlighted` and the items' `tabindex` in the task that moves the highlight on both modes; each item's C# `Highlighted` mirror, and with it `ClassValue` and the render functions, trails them by the report's leg and C#'s render (157 ms at 75 ms one-way; 0–1 ms on WebAssembly).
* CMP-STA-32, CMP-STA-33, CMP-STA-40 — row "JS-owned values that originate in C# state" (added 2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 01 Amendments 2026-11-04): a C#-origin highlight and the selected index C# computes reach the module through `update`, one leg after C# makes them on Server.
* CMP-STA-37, CMP-KEY-21, CMP-FOC-09 — the row of a popup element the opening render creates (ADR 01 Amendments 2026-10-27; added 2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 01 Amendments 2026-11-04): an open by ArrowDown highlights the first item after the open's round trip, the portal's move and the floating handle's attach, about eight one-way legs on Server (795–857 ms at 75 ms one-way; 122–129 ms on WebAssembly).
* CMP-EVT-10 — a closed list's typeahead match (added 2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 01 Amendments 2026-11-04): it reaches C# one leg after the key on Server, and the family's value request starts from it; the families add their IDs.
* CMP-KEY-24, CMP-EVT-08 — row "C#-origin consumer vetoes" (added 2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 01 Amendments 2026-11-04): on Server a canceled Shift+Tab close still moves the focus; on WebAssembly the cancel lands inside the keydown and the focus stays in the list.

### 4.1 Structure (STR)

- **CMP-STR-01** — MUST. A composite group consists of exactly one composite root element and any number of composite items rendered anywhere in its subtree (not necessarily as direct children); every item registers with the nearest enclosing item registry, which the root provides.
  Source: `internals/composite/root/CompositeRoot.tsx:84-96`, `internals/composite/item/useCompositeItem.ts:14-17`. Test: [L4] `CompositeRoot.test.tsx` "updates the order of items when their containers are reordered".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 13 §3): re-tagged [L2] → [L4], because the cited title is browser-only in Base UI (`it.skipIf(isJSDOM)`). It runs in `tests/browser/composite.spec.ts` on the `composite-reorder` fixture, both modes, with the test-only root and items; the public-part proof is RadioGroup's (track 8).
  Port note (2026-10-27, M3 track 1, issue 100; design point 6b, not replicated, chosen by the user 2026-10-27 under ADR 04 Amendments 2026-10-18, the orchestrator's and track 1's recommendation): Base UI registers every item with the nearest `CompositeList`, and a `CompositeRoot` renders one. Two untested quirks follow. A composite item inside a list inside a root (a `Toolbar.Button` inside an Accordion inside a Toolbar) registers with the list while it reads its highlight from the root. A list item inside a root inside a list (a `Tabs.Panel` inside a Toolbar inside Tabs) registers with the root's list. The port registers a composite item with its root (`CompositeRootContext`) and a list item with its list (`CompositeListContext`, CMP-STR-03), so neither quirk is reproduced; no Base UI test covers either.
- **CMP-STR-02** — MUST. A composite item rendered without a composite root ancestor is an error: the implementation MUST fail with the message `Base UI: CompositeRootContext is missing. Composite parts must be placed within <Composite.Root>.` (see CMP-EDGE-01). An item *registration* (CMP-STR-03) without a registry is not an error.
  Source: `internals/composite/root/CompositeRootContext.ts:23-31`. Test: none.
- **CMP-STR-03** — MUST. An item registration that has no enclosing item registry is inert: it resolves to index `-1`, registers nothing, and unmounting it does not fail.
  Source: `internals/composite/list/CompositeListContext.ts:18-23`, `list/useCompositeListItem.ts:43-55`. Test: [L2] `CompositeList.test.tsx` "renders an item that is not wrapped in a list".
  Port note (2026-10-27, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26; design points 1, 2, 3 and 6, option (a), settled by the orchestrator 2026-10-26): the tag stays [L2], because the title asserts an inertness C# can observe. An item registration is `CompositeListItemBehavior`, composed into a list item part (TabsPanel, AccordionItem); its registry is `CompositeListBehavior` on the list root (TabsRoot, AccordionRoot), cascaded as `CompositeListContext`, a context of its own. Without a list the item is inert: index -1, nothing registered, and a dispose that does not throw. The title runs verbatim at L2 (`CompositeListTests`) on the test-only `TestCompositeListItem`. In a list, the item's index is -1 until the first report and in prerender (CMP-STA-15), then its position in the order the `composite` module reports from its registration-only mode (`CompositeOptions.RegistrationOnly`). That mode keeps the registry, its move detection and its shadow-root items, and installs no listener, writes no `tabindex` and keeps no highlight (L3 port titles "reports the order and leaves keys, focus and tabindex to the page" and "reports a move and an update, and resolves items inside a shadow root"; L4 port titles on the `composite-registration` fixture, both modes). Every list item renders an id, the consumer's or a generated one, which the module resolves. On Server the index arrives after paint plus a round trip (CMP-STA-10's row names the published indexes). The public-part proofs are TabsPanel's (track 5) and AccordionItem's (track 6).
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsPanel`, the first item of a registration-only list (E1): `TabsAutomaticSelectionTests` "sets the panel index data attribute" (`-1` until the Root's list reports, then `0`) and "port: data-index counts the rendered panels only, in document order" at L2. A Panel is the list's item only while it renders an element (TAB-DOM-17's note); the inert branch, an item with no list, has no Tabs counterpart, since a Panel outside a Root throws (TAB-EDGE-01).
- **CMP-STR-04** — MUST. Two registrations MAY target the same DOM element (an outer composite item whose element substitution is itself a composite item). The *outer* registration owns the element's entry in the registry: its metadata is what the item map publishes, and later updates of the inner registration alone MUST NOT take over the entry. Ordering rule: the outer item attaches its registration after the inner one, and the last registration for an element wins.
  Source: `internals/composite/item/CompositeItem.tsx:29-30`, `list/useCompositeListItem.ts:59-82`. Test: [L4] `CompositeRoot.test.tsx` "keeps outer metadata and focus navigation when items share a DOM node", "keeps the outer registration when only the inner item updates" (L2 for "keeps the outer registration when only the inner item updates", restated as the C# rule).
  Port note (2026-10-26, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26; ADR 13 §3): re-tagged [L2] → [L4]. The rule is C#'s: `CompositeRootBehavior` sends the first registration per id, and Blazor sets parameters parent before child, so the outer item's registration is the one sent; the inner item's registration, its flags and the `tabindex="-1"` the outer item put in its bag are never sent for that id, and the inner item's registration takes over when the outer one is removed. The statement's ordering rule ("the outer item attaches its registration after the inner one, and the last registration for an element wins") therefore reads, for the port, "the outer item registers first, and the first registration per id wins"; the outcome is the statement's. "keeps outer metadata and focus navigation when items share a DOM node" runs in `tests/browser/composite.spec.ts` on the `composite-nested` fixture (Base UI's `NestedItemsRoot`), both modes. "keeps the outer registration when only the inner item updates" is restated at L2 over the options sent (`CompositeOuterRegistrationTests`): a change to the inner item alone sends no `update`, and a change to the outer item sends its registration. The key path of option (i) on a shared element runs the outer item's consumer `onkeydown` once (two L2 port titles). The public-part proof is Toolbar's (track 4), a `Toolbar.Button` substituting a `Toggle`.
- **CMP-STR-05** — MUST. The composite root renders a `div` by default; the element tag is configurable (`tag`) and element substitution / `className` / `style` are supported through the shared rendering mechanism ([rendering-and-composition](rendering-and-composition.md)). The composite item likewise renders a `div` by default with a configurable `tag`.
  Source: `internals/composite/root/CompositeRoot.tsx:39,67-72`, `internals/composite/item/CompositeItem.tsx:21,27-33`. Test: none.
- **CMP-STR-06** — MUST. List navigation emits attributes and handlers for four element roles — *reference*, *floating*, *item* and *trigger* — and the *reference* role is the union of the `aria-activedescendant` attribute (CMP-ARIA-01) and the *trigger* handlers. When the mechanism is disabled (`enabled: false`) all four groups are empty.
  Source: `floating-ui-react/hooks/useListNavigation.ts:947-957`, `floating-ui-react/types.ts:146-151`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept: Base UI has no title, and port titles prove the row (the M2 "none (port L3)" precedent of PR 86). Each role is a handle (design point 1 (a) with its C# refinement (r1), settled by the orchestrator 2026-11-04): a `Reference`, `Trigger` or `Floating` handle per element (`CompositeListRole`, `CompositeRootBehavior.SetList`); the reference handle writes `aria-activedescendant` and runs the trigger handlers; the item role is the item's registration (`CompositeItemBehavior`), its handlers delegated on the floating handle. L3: `js/composite-list.test.ts` "port: the reference, trigger, floating and item roles act through their handles, and with enabled false none acts"; L2: `ListNavigationTests` "a list handle waits for the session's handle, then sends the session token, the list's options and its role" and `CompositeTwinContractTests.A_list_handle_sends_its_session_its_list_options_and_one_role_and_each_option_only_when_set`.
- **CMP-STR-07** — MUST. Typeahead emits the same two handlers (`keydown`, `blur`) for both the *reference* and the *floating* roles, and nothing when disabled.
  Source: `floating-ui-react/hooks/useTypeahead.ts:239-244`. Test: [L3] `useTypeahead.test.tsx` "matches when focus is within reference", "matches when focus is within floating".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "matches when focus is within reference" and "matches when focus is within floating". Typeahead is wholly the module's (Amendments 2026-11-03 (1)): the reference and floating handles run it on their elements' `keydown` and `blur`, over one buffer in the list's shared state.

### 4.2 State model (STA)

#### 4.2.1 Item registry

Explanatory: the registry keeps a map from element to registration `{ metadata, index | null, label, textElement }`. Whenever a registration is added, removed or replaced, a single rebuild is scheduled for the current commit and performed post-commit, pre-paint. The rebuild produces an ordered item list (CMP-ALG-17), rewrites the elements array and labels array in place, and publishes the item map to subscribers and to `onMapChange` only if membership, order, explicit index or metadata identity changed.

- **CMP-STA-01** — MUST. Each registered item resolves to a non-negative integer index. Items registered with an explicit `index ≥ 0` occupy that index. Items registered without an explicit index ("automatic" items) are numbered in document order, skipping any index reserved by an explicit item.
  Source: `internals/composite/list/CompositeList.tsx:228-273`. Test: [L3] `CompositeList.test.tsx` "registers explicitly indexed items in their index-addressed slots", "reserves explicit slots when assigning automatic indexes".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). Partial in M2: explicit indexes exist on the JavaScript side only (CMP-ALG-17 partial); both titles run.
- **CMP-STA-02** — MUST. Document order for automatic items is the DOM tree order of their elements (an element that precedes another in tree order, including an ancestor relative to a descendant, gets the lower index), evaluated across shadow boundaries only insofar as the elements share a document tree.
  Source: `internals/composite/list/CompositeList.tsx:256,299-312`. Test: [L3] `CompositeList.test.tsx` "updates indexes when a leaf item moves outside React".
- **CMP-STA-03** — MUST. Registrations whose element is not connected to the document at rebuild time are excluded from the item list (they get no index and no slot).
  Source: `internals/composite/list/CompositeList.tsx:235-238`. Test: [L3] `CompositeList.test.tsx` "excludes items detached outside React from the registry", "updates the registry when a mounted item stops rendering an element".
- **CMP-STA-04** — MUST. A registration with an explicit negative index is ignored: it gets no slot in the elements array and no entry in the item map.
  Source: `internals/composite/list/CompositeList.tsx:247-253`. Test: [L3] `CompositeList.test.tsx` "does not register negative explicit indexes".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3; the title runs in `src/Blazix.Primitives/js/composite.test.ts` against `createCompositeRegistry`. Implemented early: an explicit index exists on the JavaScript side only until a later component registers one (M5).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user): C#'s half, the explicit index a family sends (`CompositeItemBehavior.SetRegistration(…, index:)`, `CompositeItemRegistration.Index`), runs in `composite.test.ts` "reserves the explicit slots C# sends when assigning automatic indexes, and leaves out a negative one" and L2 `CompositeRegistrationFieldsTests` "an item registers its label, text element and explicit index only when set, and a label change sends one update".
- **CMP-STA-05** — MUST. After each rebuild the elements array contains exactly the registered connected elements at their indexes (length = highest index + 1; explicit indexes may leave empty slots), and the labels array (when provided) is aligned with it.
  Source: `internals/composite/list/CompositeList.tsx:61-88`. Test: [L3] `CompositeList.test.tsx` "only publishes maps that are aligned with the element registry", "cleans up refs on unmount".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). The labels array is M5 (CMP-STA-06/07), so the labels clauses are not asserted; the elements-array clauses of both titles run.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user): the labels clause is built (`CompositeRegistry.labels`, aligned with `elements` on every rebuild) and asserted by both titles in `src/Blazix.Primitives/js/composite.test.ts`.
- **CMP-STA-06** — MUST. The label of an item is resolved, in order: the registration's explicit `label` when it is not `undefined` (an explicit `null` means "no label" and MUST NOT fall back); otherwise the text content of the registration's text element when one is given; otherwise the item element's text content.
  Source: `internals/composite/list/CompositeList.tsx:77-82`. Test: [L3] `CompositeList.test.tsx` "resolves each label source".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user): green at L3, "resolves each label source" in `composite.test.ts` against the registry (`register(element, { label, textElement })`). C# sends the label's source by registration, each field only when set: `Label` (the empty string for an explicit "no label", which the module registers as `null`), `TextId` (the text element's id, resolved as the item's is); values stay in C# (L2 `CompositeRegistrationFieldsTests`).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): on Server an item a re-render adds, with its label and explicit index, reaches the module's order report one leg after the render (157 ms at 75 ms one-way, C# holding the order a further leg later), and on WebAssembly the report goes out in the render's own task (`tests/browser/composite-list-figures.spec.ts` "port: an item added by a re-render reaches the module's order report one leg after its render"; ADR 01 Amendments 2026-11-04, the "EVD-STA-07, CMP-STA-10, FFI registration" row). Server: see ADR 01.
- **CMP-STA-07** — MUST. Labels are re-resolved on every rebuild, so a label change on a mounted item (which re-registers it) updates the labels array, and unmounting an item drops its label slot.
  Source: `internals/composite/list/CompositeList.tsx:62-88`, `list/useCompositeListItem.ts:62-82`. Test: [L3] `CompositeList.test.tsx` "updates the label of a mounted item", "drops label slots for items that unmount".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user): re-tagged [L2] → [L3] (the registry is JavaScript's, ADR 01 decision 2, the precedent of §10 item 12); both titles run in `composite.test.ts` against the registry, and a label change from C# re-registers the item (`composite.test.ts` "re-registers an item when its explicit index changes or is removed" covers the re-registration path; L2 "… a label change sends one update").
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): on Server an item a re-render adds, with its label and explicit index, reaches the module's order report one leg after the render (157 ms at 75 ms one-way, C# holding the order a further leg later), and on WebAssembly the report goes out in the render's own task (`tests/browser/composite-list-figures.spec.ts` "port: an item added by a re-render reaches the module's order report one leg after its render"; ADR 01 Amendments 2026-11-04, the "EVD-STA-07, CMP-STA-10, FFI registration" row). Server: see ADR 01.
- **CMP-STA-08** — MUST. The published item map is ordered by index and maps each element to its metadata object merged with `{ index }`; items without metadata publish `{ index }` only.
  Source: `internals/composite/list/CompositeList.tsx:69-74`. Test: [L3] `CompositeList.test.tsx` "publishes item metadata alongside the index", "registers explicitly indexed items in their index-addressed slots".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). Partial in M2: metadata exists on the JavaScript side only; "publishes item metadata alongside the index" and "registers explicitly indexed items in their index-addressed slots" run.
  Port note (2026-10-27, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26): of the M3 metadata, only Toolbar's flags travel to JavaScript: `CompositeItemRegistration.Disabled` and `FocusableWhenDisabled`, sent only when set, from which the module derives `disabledIndices` (CMP-KEY-08). Tabs' metadata (`{ disabled, id, value }`, `TabsTab.tsx:66`) stays in C#: with `CompositeOptions.ReportItems` the module reports the items' ids in index order, and `CompositeRootBehavior` joins them to its registrations as `OrderedItems`, so a Tab's `value` never crosses (CMP-EVT-03; design points 1 and 3, option (a), settled by the orchestrator 2026-10-26). L2 titles "the items' flags travel in their registrations, and one flag change sends one update" and "joins the reported ids to its registrations in order, ignoring unknown ids, with one change event per report". The public-part proofs are Toolbar's (track 4) and Tabs' (track 5).
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList` (E2): the report's ids are joined to the Tabs by element id for the Root's registry (`{ value, disabled, id }`, TAB-STA-04/05): `TabsAutomaticSelectionTests` ("should select the third tab when first two tabs are disabled", "sets the aria-labelledby attribute on tab panels to the corresponding tab id", and the others) at L2, `tests/browser/tabs.spec.ts` "calls onValueChange when the selected tab is removed", both modes.
- **CMP-STA-09** — MUST. The item map is published (to item subscribers and to `onMapChange`) only when the rebuilt list differs from the previously published one in length, or in any position's index, element, explicit registration index, or metadata object identity. Rebuilds that change none of these (e.g. the consumer swapping the array objects it reads from) resync the arrays silently without publishing.
  Source: `internals/composite/list/CompositeList.tsx:147-171,173-186`. Test: [L3] `CompositeList.test.tsx` "syncs replacement refs without publishing an unchanged map", "keeps refs populated for items whose guessed index is already correct".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). Both titles are restated (dated, 2026-10-22): React replacing the `elementsRef` object and Strict Mode's replay of correctly guessed indexes (`guess` is M5, CMP-STA-16) become a rebuild whose registrations did not change, which re-syncs the elements array in place and publishes nothing.
- **CMP-STA-10** — MUST. Registrations and unregistrations that happen in the same render batch (including from nested state updates deep in the subtree and from items replacing their element) are collapsed into one rebuild and one publication at most, performed post-commit, pre-paint. `attach` and `dispose` of the part's JavaScript twin are idempotent; a repeated `attach` of the same element is a no-op. Server: see ADR 01 relaxation table.
  Source: `internals/composite/list/CompositeList.tsx:37-59,188-192`. Test: [L2] twin contract (repeated `attach`, `dispose` twice); `CompositeList.test.tsx` "updates refs when an item mounts from a nested state update", "updates refs when an item unmounts from a nested state update", "keeps syncing refs after replacing items under Strict Mode", "registers a replacement render target" (L4 for the post-commit, pre-paint timing on each render mode, ADR 01 relaxation table).
  Port note (2026-10-22, M2 track 3, issue 79; ADR 06 Amendments 2026-10-18 rule 2): at L2 on the test-only `TestCompositeRoot` and `TestCompositeItem`: one attach and one dispose, one `update` per render batch (a batch of items, a removal, nothing for an unchanged render), and the four React titles restated as `update` counts. The batch boundary is C#'s after-render; a batch that only removes items makes the root render from the item's dispose. At L4 the port title "a new item takes the tab stop in the task that inserts it on WebAssembly, and within a round trip on Server" asserts ADR 01's row per mode on the `composite-list` fixture. C# also sends an `update` when an item renders a new element under the same id, so the module resolves it again.
- **CMP-STA-11** — MUST. When registered elements are *moved* in the DOM outside the framework's control (a node is removed and re-inserted within one mutation batch), the registry MUST detect that a connected element now precedes a previously earlier connected element and rebuild asynchronously (after the mutation observer callback; "soon after" is acceptable, tests use `waitFor`). Additions or removals alone, and mutations touching unrelated nodes, MUST NOT trigger a rebuild or a publication.
  Source: `internals/composite/list/CompositeList.tsx:90-141,287-297`. Test: [L3] `CompositeList.test.tsx` "updates indexes when a leaf item moves outside React", "updates indexes when keyed groups reorder", "updates indexes when grouped items reorder alongside unrelated mutations", "ignores mutations for unrelated leaf nodes", "skips detached items while verifying order after a move". Docs: `CHANGELOG.md v1.0.0-beta.4 "Fix \`CompositeList\` not updating item order on reordering (#2675)"`.
- **CMP-STA-12** — SHOULD. Move detection observes child-list mutations on the lowest common ancestor of each pair of adjacent automatic items (each distinct ancestor observed once), is not installed when fewer than two automatic items exist, and is re-installed after every rebuild (so a list that grows from one item starts observing).
  Source: `internals/composite/list/CompositeList.tsx:94-97,129-140,275-285`. Test: [L3] `CompositeList.test.tsx` "observes each shared mutation root once", "observes reorders after the list grows from one item".
- **CMP-STA-13** — MUST. Items in a shadow tree (no common ancestor with the light-DOM items) still register and receive indexes; the absence of an observable common ancestor MUST NOT prevent registration.
  Source: `internals/composite/list/CompositeList.tsx:132-140,275-285`. Test: [L3] `CompositeList.test.tsx` "registers items that sit across a shadow boundary".
- **CMP-STA-14** — MUST. When the registry is torn down, the elements array and labels array are emptied.
  Source: `internals/composite/list/CompositeList.tsx:180-185`. Test: [L3] `CompositeList.test.tsx` "cleans up refs on unmount".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). Teardown is the registry's `dispose`, which empties the elements array in place; "cleans up refs on unmount" runs without its labels clause (M5).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user): the labels clause of "cleans up refs on unmount" is asserted (`dispose` empties `labels`).
- **CMP-STA-15** — MUST. An item's index before the first publication is `-1` (this is the value present in server-rendered output); after the first publication it is the resolved index.
  Source: `internals/composite/list/useCompositeListItem.ts:43-55,84-96`. Test: [L2] `CompositeList.test.tsx` "hydrates a server-rendered list without a mismatch under Strict Mode".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, ADR 06 Amendments 2026-10-18 rule 2): restated at L2: C# renders `tabindex="-1"` on every item on every render, in prerender and in either interactive mode, and never another value, which is the value before the module's first publication.
- **CMP-STA-16** — MAY. An automatic item MAY opt in to *guessing* its index from the order in which items first render (`guess: true`): the registry hands out `0, 1, 2, …` in the order items first render within one render batch, so flat lists rendered in DOM order get the correct index on the first render of the part without a corrective re-render; a wrong guess is corrected by the first publication. An explicitly indexed item MUST NOT consume a guess.
  Source: `internals/composite/list/useCompositeListItem.ts:39-55`, `list/CompositeList.tsx:85`. Test: [L3] `CompositeList.test.tsx` "assigns correct guessed indexes during the first render", "does not consume an index guess for an explicitly indexed item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): re-tagged [L2] → [L3] (design point 4 (a), settled by the orchestrator 2026-11-04; ADR 13 §3). React's first-render guess has no counterpart under registration by id, where an item's index is `-1` until the first publication (CMP-STA-15). What the guess buys still holds: the module's first rebuild reads the document order, whatever order C# registers in, so the first publication is correct and no second one follows. Both titles are restated through C#'s registrations as `js/composite.test.ts` "port: the first publication indexes automatic items in DOM order around an explicit index, with no second publication" and "port: an explicitly indexed item consumes no automatic slot".
- **CMP-STA-17** — MUST. Changing an item's explicit index, or removing it (switching to automatic), re-registers the item at the new slot and clears the old slot; the automatic index is then resolved on the next publication.
  Source: `internals/composite/list/useCompositeListItem.ts:62-82`. Test: [L3] `CompositeList.test.tsx` "re-registers an item when its explicit index changes or is removed", "resolves an automatic index when the equivalent explicit index is removed".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user): re-tagged [L2] → [L3] (the registry is JavaScript's, ADR 01 decision 2). Both titles are restated in `composite.test.ts` through C#'s registrations and the order report, "re-registers an item when its explicit index changes or is removed" (its last step is the equivalent automatic index, which reports nothing, the second title's case).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): on Server an item a re-render adds, with its label and explicit index, reaches the module's order report one leg after the render (157 ms at 75 ms one-way, C# holding the order a further leg later), and on WebAssembly the report goes out in the render's own task (`tests/browser/composite-list-figures.spec.ts` "port: an item added by a re-render reaches the module's order report one leg after its render"; ADR 01 Amendments 2026-11-04, the "EVD-STA-07, CMP-STA-10, FFI registration" row). Server: see ADR 01.
- **CMP-STA-18** — MUST. An automatic item whose index shifts because of other items (insertions before it) keeps its registration; only its published index changes.
  Source: `internals/composite/list/useCompositeListItem.ts:62-82,89-95`. Test: [L3] `CompositeList.test.tsx` "does not detach item refs when an index shifts".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). Restated (dated, 2026-10-22): what the React test watched through ref callbacks is the registration itself, which an index shift keeps while only the published index changes; C# registers an item once per id and an index shift sends no new registration.

#### 4.2.2 Keyed registry (`useItemRegistry`)

- **CMP-STA-19** — MUST. The keyed registry exposes `registerItem(key, item)` returning an unregister function, an immutable `items` snapshot, and the live mutable map. All registrations and unregistrations of one commit are collected into exactly one new snapshot published after that commit; snapshots are never mutated after publication.
  Source: `internals/useItemRegistry.ts:14-54`. Test: [L2] `useItemRegistry.test.tsx` "publishes one immutable snapshot for all registrations in a commit".
- **CMP-STA-20** — MUST. The registry tolerates register → unregister → register sequences of the same key within one render batch: the published snapshot contains every registered key with its latest value, and the update latch MUST NOT get stuck. `attach` and `dispose` of the part's JavaScript twin are idempotent; a repeated `attach` of the same element is a no-op.
  Source: `internals/useItemRegistry.ts:19-30,49-51`. Test: [L2] twin contract (repeated `attach`, `dispose` twice); `useItemRegistry.test.tsx` "publishes every registration under StrictMode double effects".
- **CMP-STA-21** — MUST. When the last key unregisters, an empty snapshot is published.
  Source: `internals/useItemRegistry.ts:36-39`. Test: [L2] `useItemRegistry.test.tsx` "publishes an empty snapshot once the last item unregisters".

#### 4.2.3 Composite highlighted index

- **CMP-STA-22** — MUST. The highlighted index is either owned by the consumer (`highlightedIndex` + `onHighlightedIndexChange`) or by the root (initial value `0`); when owned by the consumer, every change the root wants to make is reported through `onHighlightedIndexChange(index)` and the root renders whatever `highlightedIndex` it is given.
  Source: `internals/composite/root/useCompositeRoot.ts:90,100-108`. Test: [L3] `CompositeRoot.test.tsx` "controlled mode", "uncontrolled mode".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id. The root owns the highlight in M2; the consumer-owned clause and "controlled mode" wait for M3 (TAB-STA-19), "uncontrolled mode" runs at L3 and at L4.
  Port note (2026-10-27, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26; design points 4 and 5, option (a) both, settled by the orchestrator 2026-10-26): there is no consumer-owned highlight in the port. The highlight stays the module's from attach (ADR 04 Amendments 2026-10-22), and the one v1.9.0 consumer of the consumer-owned clause, TabsList (`TabsList.tsx:111-126`), gets its behaviour as the option `CompositeOptions.FollowActiveItem`. With it, the highlight follows the item marked `data-composite-item-active`, evaluated per item as `TabsTab.tsx:83-109`'s effect is. The marker and the items' `aria-disabled` are observed by a `MutationObserver` on the root, pre-paint on both modes; a marked item that is `aria-disabled="true"` in the applied batch is not followed, as a disabled Tab renders that and never DOM `disabled` (TAB-DOM-10); the follow is skipped once after a keydown on the item. "controlled mode" is restated (dated 2026-10-27) at L3 over the follow: the keys move the highlight as in a root-owned group, and a marker moved while focus is outside the root moves the tab stop. L3 port titles cover the follow with focus outside and inside, a disabled marked item, the skip after a keydown and a marked item that registers late. L4 port titles run on the `composite-follow` fixture, both modes. The public-part proof is TabsList's (track 5, TAB-STA-19, TAB-FOC-04..07).
  Port note (2026-10-27, M3 track 1, issue 100; design point 5, option (a), settled by the orchestrator 2026-10-26): the follow's disabled check reads the DOM where Base UI reads the Tab's `disabled` prop (`TabsTab.tsx:104-108`). The two agree for every Tab the port renders, because only a disabled Tab renders `aria-disabled="true"`. They diverge in one misuse shape no test covers: a consumer-supplied `aria-disabled="true"` on an enabled Tab also blocks the follow in the port. TAB-FOC-06's note (track 5) says the same.
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList` (E5, `FollowActiveItem`) by `tests/browser/tabs.spec.ts` "when focus is outside the tablist, highlight follows the new active tab (tabIndex=0 moves)", "puts the selected child in tab order", "does not set tabIndex=0 on disabled tabs when they are programmatically selected", "when focus is inside the tablist, highlight stays put on external change and arrow keys continue from the focused tab" and "keeps controlled async activation and focus aligned with activateOnFocus=%s", both modes; TAB-FOC-06 carries the dated divergence of the `aria-disabled` read.
- **CMP-STA-23** — MUST. On the first non-empty publication of the item map, if some item element carries the active item marker (`data-composite-item-active`), the highlighted index becomes that item's published index (its registry index, not its position in the map).
  Source: `internals/composite/root/useCompositeRoot.ts:137-150`. Test: [L3] `CompositeRoot.test.tsx` "uses an active item explicit index as the initial highlighted index", "moves the tab stop to the active item when the highlighted item is removed".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id. Restated (dated, 2026-10-22): an explicit index exists on the JavaScript side only, so in "uses an active item explicit index as the initial highlighted index" the marked item's registry index is its position; "moves the tab stop to the active item when the highlighted item is removed" runs as written.
- **CMP-STA-24** — MUST. On the first non-empty publication, if no item carries the marker and the current highlighted index is disabled (CMP-ALG-02 with the root's `disabledIndices`), the highlighted index becomes the first non-disabled index; if every item is disabled the highlighted index is kept.
  Source: `internals/composite/root/useCompositeRoot.ts:151-161`. Test: [L3] `CompositeRoot.test.tsx` "moves the initial tab stop to the first enabled item when the default item is disabled", "keeps the initial tab stop when all items are disabled".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id.
  Port note (2026-10-26, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26): the root's `disabledIndices` may be derived by the module from the items' flags (`CompositeOptions.DeriveDisabledIndices`, Toolbar's case), in the same pass as the publication, so the first publication's check already reads the derived indices; Base UI checks the previous render's array and corrects it a render later through CMP-STA-29, with the same outcome. L3 port title "moves the initial tab stop off a first item that its flags disable, in the first publication (CMP-STA-24)".
- **CMP-STA-25** — MUST. On the first non-empty publication the marked active item (if any) is scrolled into view within the root (CMP-ALG-19).
  Source: `internals/composite/root/useCompositeRoot.ts:163`. Test: none.
- **CMP-STA-26** — MUST. On every later publication the root re-locates the element that held the highlight: if it is still present at a different index, the highlighted index follows it; if it is present at the same index nothing changes.
  Source: `internals/composite/root/useCompositeRoot.ts:115-133`. Test: [L3] `CompositeRoot.test.tsx` "keeps the tab stop on the highlighted item when an earlier item is removed". Docs: `CHANGELOG.md v1.8.0 "Fix roving focus when items are added or removed (#5447)"`.
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id.
- **CMP-STA-27** — MUST. When the previously highlighted element is gone: if another, non-disabled element now occupies the same index it keeps the tab stop (no index change); otherwise the highlighted index becomes the fallback index (CMP-ALG-15: the first non-disabled element carrying the active marker, else the first non-disabled element, else `0`).
  Source: `internals/composite/root/useCompositeRoot.ts:121-130,345-365`. Test: [L3] `CompositeRoot.test.tsx` "moves the tab stop back into range when the highlighted item is removed", "moves the tab stop to the active item when the highlighted item is removed", "skips items that cannot hold the tab stop", "keeps the tab stop in range when no item can hold it".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id.
- **CMP-STA-28** — MUST. Publications of an empty item map are ignored (no index change).
  Source: `internals/composite/root/useCompositeRoot.ts:111-113`. Test: none.
- **CMP-STA-29** — MUST. When `disabledIndices` is provided and changes after the first publication, and the highlight is owned by the root (not the consumer), and the current highlighted index is now disabled, the highlighted index moves to the first non-disabled index (kept if all are disabled). This check runs post-commit, pre-paint.
  Source: `internals/composite/root/useCompositeRoot.ts:166-193`. Test: [L3] `CompositeRoot.test.tsx` "keeps outer metadata and focus navigation when items share a DOM node" (disabled indices derived from metadata after the first publication) (L4 for the cited title, whose shared elements need the C# rule of CMP-STR-04).
  Port note (2026-10-26, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26; ADR 13 §3): re-tagged [L2] → [L3]: the re-validation is the module's (ADR 01 decision 2; ADR 04 Amendments 2026-10-22). At L3 it runs as the port titles "moves a root-owned highlight off an index that a changed disabledIndices disables (CMP-STA-29)" and "moves the tab stop off an item that disabledIndices disables when focus lands on it (CMP-STA-29)" (M2, implemented early) and "moves the tab stop off the highlighted item when a flag change disables it after the first publication (CMP-STA-29)", over the indices the module derives from the items' flags (`CompositeOptions.DeriveDisabledIndices`); with derivation the re-validation also runs after each publication, as Base UI's effect does on its new derived array. The cited title runs at L4 on the `composite-nested` fixture, both modes, beside the port title "a runtime flag change moves the tab stop off an item that is no longer focusable" on `composite-flags`, where the flag is `update`-carried on Server (ADR 01's row "JS-owned values that originate in C# state"). The public-part proof is Toolbar's (track 4).
- **CMP-STA-30** — MUST. Setting the highlighted index from a keyboard move also scrolls the new item into view within the root (CMP-ALG-19); setting it from item focus does not scroll.
  Source: `internals/composite/root/useCompositeRoot.ts:101-108,310`, `item/useCompositeItem.ts:26-28`. Test: none.

#### 4.2.4 List navigation active index

- **CMP-STA-31** — MUST. The active index is owned by the consumer: the mechanism reads `activeIndex` (`number | null`) and proposes every change through `onNavigate(index | null, event?)`. The mechanism keeps a private cursor (`-1` = none) and reports `null` whenever the cursor is `-1`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:305,309-311`. Test: [L3] `useListNavigation.test.tsx` "true - onNavigate is called with `null` when escaped".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "true - onNavigate is called with `null` when escaped". The highlight is the module's from attach (Amendments 2026-11-03 (2)): the floating handle keeps the cursor and the highlight and reports each change as `CompositeListHighlight(Index, ItemId, Event, Revision)`, `null` for a cursor of `-1`; C# mirrors it (`ListNavigationBehavior.HighlightedIndex`, each item's `Highlighted`) and asks for its own highlights by revision (CMP-STA-32). Also "port: writes the highlighted item's tabindex and data-highlighted in the task that moves the focus, and reports it once" (L3), and `ListNavigationTests` "every item renders tabindex="-1" and no data-highlighted, and its Highlighted mirror follows the module's report" and "a highlight move renders exactly the two items whose mirror changed, at 100 items" (L2; counted through `ClassValue`, the two items render once each). On Server the mirror, and with it `ClassValue` and the render functions, trails the module's `data-highlighted` by the report's leg and C#'s render (157 ms at 75 ms one-way; 0–1 ms on WebAssembly; `tests/browser/composite-list-figures.spec.ts` "port: the Highlighted mirror trails the module's data-highlighted by the report's leg and C#'s render"; ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-STA-32** — MUST. If the consumer declines a proposed change (renders an `activeIndex` different from the cursor), the next key press re-synchronizes the cursor to the rendered `activeIndex` (when it is in bounds) before computing the move.
  Source: `floating-ui-react/hooks/useListNavigation.ts:587-597`. Test: none. Docs: `CHANGELOG.md v1.7.0 "Prevent ignored arrow keys from selecting on refocus (#5643)"` (entry at line 120 of the v1.9.0 section).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); port titles prove it. In the port the consumer's refusal is a C#-origin highlight (Amendments 2026-11-03 (2)): C# sends `CompositeHighlightRequest(Index, Revision)` through `update`, the module applies each revision once and ignores a stale echo, and the next key resynchronizes the cursor to the highlight before it steps. L3: `js/composite-list.test.ts` "port: a C# highlight request moves the highlight and the cursor, so the next key steps from it", "port: a C# highlight request whose revision was already applied is ignored, and a newer one applies" and "port: the next key resynchronizes the cursor to a C# highlight it could not follow while the highlight was out of bounds"; L2: `ListNavigationTests` "a C# highlight request goes out at once with a revision, and a report that answers an older revision is ignored". On Server the request reaches the module one leg after C# makes it (the item written at 166 ms and mirrored at 324 ms at 75 ms one-way; 9 ms and 25 ms on WebAssembly; `tests/browser/composite-list-figures.spec.ts` "port: a C#-origin highlight request reaches the module one leg after C# makes it"; ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-STA-33** — MUST. When the popup becomes open with its floating element mounted, the cursor is set to `selectedIndex ?? -1`; if the effective focus-on-open flag is truthy (CMP-STA-39) and `selectedIndex` is not `null`, `onNavigate(selectedIndex)` is emitted and the next focus/scroll pass is forced to scroll the item into view regardless of input modality.
  Source: `floating-ui-react/hooks/useListNavigation.ts:391-403`. Test: [L3] `useListNavigation.test.tsx` "scrolls the selected item into view on open"; `test/popupListConformanceTests.tsx` "reveals and highlights the selected item when opening", "opens on the selected item with %s" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "scrolls the selected item into view on open", the selected index being an `int` C# computes (`CompositeListOptions.SelectedIndex`). Green at L4, both modes: `tests/browser/composite-list-rows.spec.ts` "scrolls the selected item into view on open", and the popup-list suite's "reveals and highlights the selected item when opening" and "opens on the selected item with %s" (Select, Combobox; §4.14's port note). On Server the selected index reaches the module one leg after C# sends it (CMP-STA-32's figure, ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-STA-34** — MUST. When the popup closes or its floating element unmounts after having been mounted, the cursor becomes `-1` and `onNavigate(null)` is emitted (no event).
  Source: `floating-ui-react/hooks/useListNavigation.ts:404-410`. Test: [L3] `useListNavigation.test.tsx` "resets indexRef to -1 upon close"; `test/popupListConformanceTests.tsx` "starts fresh when reopened after Escape" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "resets indexRef to -1 upon close", on DOM built after the test file's `Autocomplete`, with the consumer's `onChange` played as C#. The L4 clause is green by the popup-list suite's "starts fresh when reopened after Escape" (Menu, Select, Combobox), both modes. The mirror of the cleared highlight lags as CMP-STA-31's does (ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-STA-35** — MUST. While open with the floating element mounted, whenever `activeIndex` is a non-null in-bounds index, the cursor is set to it and the item focus/scroll procedure (CMP-ALG-23) runs post-commit, pre-paint; the forced-scroll flag is then cleared.
  Source: `floating-ui-react/hooks/useListNavigation.ts:478-482`. Test: [L3] `useListNavigation.test.tsx` "navigates down on ArrowDown" (L4 for the focus clause).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "navigates down on ArrowDown". The item focus and the highlight's writes happen in the task that moves the highlight ("port: writes the highlighted item's tabindex and data-highlighted in the task that moves the focus, and reports it once"). The focus clause is green at L4: `tests/browser/composite-list-rows.spec.ts` "navigates down on ArrowDown", both modes. The mirror lags as CMP-STA-31's does (ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-STA-36** — MUST. While open, when `activeIndex` becomes `null` and `selectedIndex` is `null` and the floating element was already mounted, the cursor becomes `-1` and the focus procedure runs (which focuses nothing, but cancels any pending deferred focus).
  Source: `floating-ui-react/hooks/useListNavigation.ts:427-438`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as in Base UI: built in `js/composite.ts` (`syncActiveIndex`, after `useListNavigation.ts:427-438`), with no title.
- **CMP-STA-37** — MUST. *Initial sync on open.* When the popup has just opened (or its floating element has just mounted) with `activeIndex === null` and `selectedIndex === null`, and the effective focus-on-open flag is truthy, and either a navigation key was recorded on the trigger (CMP-KEY-21) or the effective flag is exactly `true`: the mechanism waits for the elements array to have a first element (CMP-ALG-24: check now, then after a microtask, then after the next frame; give up after the third check), then sets the cursor to the first non-disabled index — computed **without** `disabledIndices` so DOM-disabled items are skipped even when `disabledIndices` is `[]` — when the recorded key is absent, is a "to-end" key for `triggerOrientation` (CMP-ALG-20), or the list is nested; otherwise to the last non-disabled index. It then clears the recorded key and emits `onNavigate(index)`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:440-477`. Test: [L3] `useListNavigation.test.tsx` "opens on ArrowDown and focuses first item", "opens on ArrowUp and focuses last item", "skips disabled item on initial navigation", "focuses first non-disabled item in grid"; `test/popupListConformanceTests.tsx` "opens on the first item with ArrowDown", "opens on the last item with ArrowUp", "opens past a disabled first item" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "opens on ArrowDown and focuses first item", "opens on ArrowUp and focuses last item", "skips disabled item on initial navigation" and "focuses first non-disabled item in grid". A list handle attaches after its session's handle, so a list open from its first render reads the open at attach (`CompositeRootBehavior` awaits `ListNavigationBehavior.SessionReady`; L2 `ListNavigationTests` "a list handle waits for the session's handle, then sends the session token, the list's options and its role"). Green at L4 by the popup-list suite's "opens on the first item with ArrowDown", "opens on the last item with ArrowUp" and "opens past a disabled first item" (Menu, Select, Combobox), both modes; the attach order is pinned by `tests/browser/composite-list-rows.spec.ts` "port: with js/composite.js stalled the list opens on ArrowDown with its first item highlighted once both modules load", the same title with `js/interactions.js` stalled, and "port: with js/interactions.js stalled a list open from the start highlights its first item once the session attaches" (red without the attach-after-session rule, on both modes). On Server the open by ArrowDown highlights the first item about eight one-way legs after the key (795–857 ms at 75 ms one-way; 122–129 ms on WebAssembly; ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-STA-38** — MUST. The recorded trigger key is cleared whenever the popup is closed.
  Source: `floating-ui-react/hooks/useListNavigation.ts:378-381`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as in Base UI: built in `js/composite.ts` (`syncOpenAndMount`, after `useListNavigation.ts:378-381`), with no title.
- **CMP-STA-39** — MUST. *Effective focus-on-open flag.* It is `focusItemOnOpen` (`true`, `false` or `'auto'`) whenever the popup is closed or the option is not `'auto'`. While the option is `'auto'`: a `pointerdown` or `pointerenter` on the trigger resets it to `'auto'` and then sets it to `true` if the event is a virtual pointer event (CMP-ALG-28); a `mousedown` or `click` on the trigger that is a virtual click sets it to `true` under DOM focus and `false` under virtual focus. Consequently, with `'auto'`, the first item is highlighted on open only after a keyboard open (CMP-STA-37 via the recorded key) or a screen-reader synthesized press; a real pointer open starts with no highlight.
  Source: `floating-ui-react/hooks/useListNavigation.ts:304,382-387,827-839,925-928`. Test: [L3] `useListNavigation.test.tsx` "focuses the first item on click when true", "does not focus the first item on click when false"; `test/popupListConformanceTests.tsx` "starts without a highlight after a pointer opening" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "focuses the first item on click when true" and "does not focus the first item on click when false"; the `'auto'` branch's virtual press (CMP-ALG-28) by "port: a virtual click on the trigger opens with the first item highlighted, and a pointer click opens with none". The option is `CompositeListOptions.FocusItemOnOpen : bool?`, `null` for `'auto'`. The L4 clause is green by the popup-list suite's "starts without a highlight after a pointer opening" (Menu, Select, Combobox), both modes.
- **CMP-STA-40** — MUST. The `selectedIndex` changing while open MUST NOT move focus away from the reference element (the mechanism only re-derives the cursor on open).
  Source: `floating-ui-react/hooks/useListNavigation.ts:391-411`. Test: [L4] `useListNavigation.test.tsx` "selectedIndex changing does not steal focus".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): kept [L4] (the track's decision, ADR 13 §3). Green at L4: `tests/browser/composite-list-rows.spec.ts` "selectedIndex changing does not steal focus" on `composite-list-selected` (DOM built after `test/floating-ui-tests/ListboxFocus.tsx`), both modes. Base UI runs the title in jsdom, and it also runs at L3 in `js/composite-list.test.ts`. On Server the selected index C# sends reaches the module one leg later (CMP-STA-32's figure, ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-STA-41** — MUST. The mechanism publishes its `orientation` into the popup's shared context data so nested popups can read their parent's orientation when `parentOrientation` is not given.
  Source: `floating-ui-react/hooks/useListNavigation.ts:374-376,539-545`. Test: [L3] `useListNavigation.test.tsx` "keyboard navigation in nested menus with different orientation".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "keyboard navigation in nested menus with different orientation", one of Base UI's three jsdom-only nested titles (`it.skipIf(!isJSDOM)`), on DOM built after `test/floating-ui-tests/MenuOrientation.tsx` by the menu-tree harness (one session per menu, linked by the parent's token). The orientation is published as `orientation` in the session's `dataRef.current`; a nested list without `ParentOrientation` reads its parent session's.

#### 4.2.5 Typeahead state

- **CMP-STA-42** — MUST. Typeahead keeps a buffer string (initially empty), a *previous index* (initially `selectedIndex ?? activeIndex ?? -1`), a *match index* (initially `null`) and a reset timer.
  Source: `floating-ui-react/hooks/useTypeahead.ts:88-91`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as in Base UI: the buffer, the previous and match indexes and the reset timer are the list's shared state in the session's `dataRef` (design point 1 (a)), observed through CMP-KEY-38's titles.
- **CMP-STA-43** — MUST. The reset timer fires `resetMs` (default 750 ms) after the most recent accepted character; it empties the buffer, sets the previous index to the match index, and emits `onTyping(false)`. Each accepted character restarts the timer.
  Source: `floating-ui-react/hooks/useTypeahead.ts:81,183-187`, `packages/utils/src/useTimeout.ts:19-25`. Test: [L3] `useTypeahead.test.tsx` "onTyping is called with typing activity", "starts from the current activeIndex and correctly loops (strict: %s)"; `test/popupListConformanceTests.tsx` "starts a new typeahead search after a pause" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "starts from the current activeIndex and correctly loops (strict: %s)" (both, with fake timers), and "onTyping is called with typing activity" restated as "port: onTyping is called with typing activity (a typed character sets the typing flag, the reset clears it)". There is no `onTyping` report, since the typing flag stays in the module (Amendments 2026-11-03 (1)), so the title reads the flag through its one effect, an item's Space prevented while typing (design point 2 (c), settled by the orchestrator 2026-11-04). The reset timer is the module's, with `CompositeTypeaheadOptions.ResetMs` (Menu 500, Select and Combobox 750). The L4 clause is green by the popup-list suite's "starts a new typeahead search after a pause" (Menu and Select, with `page.clock`), both modes.
- **CMP-STA-44** — MUST. Whenever `open` or `selectedIndex` changes (and on first mount), post-commit, pre-paint, unless the popup is then closed with a non-null `selectedIndex` — that is, when the popup opens, when `selectedIndex` changes while open, when the popup closes while `selectedIndex` is `null`, or when `selectedIndex` becomes `null` while closed: the timer is cleared, the match index becomes `null`, and if the buffer was non-empty it is emptied and `onTyping(false)` is emitted. Closing while `selectedIndex` is not `null` leaves the typeahead state untouched.
  Source: `floating-ui-react/hooks/useTypeahead.ts:225-237`. Test: [L3] `useTypeahead.test.tsx` "onTyping is called when the popup closes without moving focus". Docs: `CHANGELOG.md v1.9.0 "Reset typeahead state when closing without moving focus (#5816)"`.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3, restated: "onTyping is called when the popup closes without moving focus" runs as `js/composite-typeahead.test.ts` "port: onTyping is called when the popup closes without moving focus (the close clears the typing flag)", read through the flag as under CMP-STA-43.
- **CMP-STA-45** — MUST. When focus leaves both the reference element and the floating element (blur whose related target is inside neither), the timer is cleared, the buffer emptied, the previous index set to the match index, and `onTyping(false)` emitted. Focus moving between the reference and the floating element keeps the session.
  Source: `floating-ui-react/hooks/useTypeahead.ts:206-223`. Test: [L3] `useTypeahead.test.tsx` "matches when focus is within floating". Docs: `CHANGELOG.md v1.2.0 "Reset typeahead on external blur (#2618)"`.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "matches when focus is within floating" (the focus moves from the reference to an item and the session continues).

### 4.3 Rendered DOM (DOM)

- **CMP-DOM-01** — MUST. Every composite item renders `tabindex="0"` when its index equals the highlighted index and `tabindex="-1"` otherwise, so that exactly one item (at most) is in the sequential tab order.
  Source: `internals/composite/item/useCompositeItem.ts:19,25`. Test: [L3] `CompositeRoot.test.tsx` "uncontrolled mode", "moves the initial tab stop to the first enabled item when the default item is disabled".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id. JavaScript writes the value: C# renders `tabindex="-1"` on every item on every render (CMP-STA-15, the handover at attach), and the `composite` module writes `"0"` on the highlighted item and `"-1"` elsewhere, in the task that moves the highlight, never over a consumer's `tabindex` (RAD-DOM-16). "uncontrolled mode" also runs at L4.
  Port note (2026-10-27, M4 track 4, [issue 120](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/120); ADR 04 Amendments 2026-10-27, the composite's precedence over a non-modal portal's `data-tabindex` stash, and the focus-manager track's design point 4, settled by the orchestrator 2026-10-27): while an item carries the stash (FLT-DOM-18), the module writes the item's roving value into `data-tabindex` instead of `tabindex`, so `enableFocusInside` restores exactly one tab stop (`composite.test.ts` "writes the roving value into data-tabindex while an item carries the stash").
- **CMP-DOM-02** — MUST. The active item marker is the attribute `data-composite-item-active` (value `""`) on an item element. Components set it on the item that should take the initial tab stop (Tabs: the active tab; Radio: the checked radio); the composite mechanism only reads it.
  Source: `internals/composite/constants.ts:1`, `tabs/tab/TabsTab.tsx:199`, `radio/root/RadioRoot.tsx:133`. Test: [L3] `CompositeRoot.test.tsx` "uses an active item explicit index as the initial highlighted index".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id. The active-item constant is `CompositeAttributes.ActiveItem` in C# and `ACTIVE_COMPOSITE_ITEM` in the module. The title is restated as under CMP-STA-23.
- **CMP-DOM-03** — MUST. The composite root MUST NOT add `aria-orientation` (or any other attribute) on its own; components add role and orientation attributes themselves.
  Source: `internals/composite/root/useCompositeRoot.ts:319-330`. Test: [L2] `CompositeRoot.test.tsx` "does not add aria-orientation when orientation is set".
- **CMP-DOM-04** — MUST. List navigation MUST NOT add `aria-orientation` to the floating element.
  Source: `floating-ui-react/hooks/useListNavigation.ts:768-802`. Test: [L3] `useListNavigation.test.tsx` "does not add role-dependent aria-orientation".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user); Amendments 2026-11-03 (2): re-tagged [L2] → [L3], the module being the writer of every attribute list navigation sets on the floating element. Green: `src/Blazix.Primitives/js/composite-list.test.ts` "does not add role-dependent aria-orientation" (positive first: the item is focused by ArrowRight, then no `aria-orientation`).
- **CMP-DOM-05** — MUST. The popup focus element is identified by the attribute `data-base-ui-focusable` (`FOCUSABLE_ATTRIBUTE`): the floating element itself when it carries the attribute, else its first descendant carrying it, else the floating element.
  Source: `floating-ui-react/utils/constants.ts:1`, `floating-ui-react/utils/element.ts:84-97`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as in Base UI. The module reads no `data-base-ui-focusable`: where Base UI reads the popup focus element (CMP-KEY-26, CMP-KEY-31, CMP-PTR-05's focus), the list handle uses the element its floating handle is attached to, the popup's list (design point 1 (a), settled by the orchestrator 2026-11-04). For Menu and Select that is the element Base UI resolves too, since one popup carries both list navigation's floating props and `FOCUSABLE_POPUP_PROPS` (`MenuRoot.tsx:676-705`, `SelectRoot.tsx:432-438`). Combobox puts the floating props on its List and the focusable props on its Popup (`AriaCombobox.tsx:1477-1485`): under its virtual focus the DOM-focus branches of CMP-KEY-31 and CMP-PTR-05 never run, and CMP-KEY-26's check reads the List where Base UI reads the Popup, which no keydown reaches while the focus stays on the input; track 10 confirms it on its parts. The attribute's lookup stays the `focus-manager` module's (`getFloatingFocusElement`).

### 4.4 ARIA (ARIA)

- **CMP-ARIA-01** — MUST. Under virtual focus, while the popup is open and `activeIndex` is not `null`, the *reference* element carries `aria-activedescendant="{id}-{activeIndex}"` where `id` is the `id` option; the consumer MUST therefore give each item the DOM id `{id}-{index}`. When closed, when `activeIndex` is `null`, or under DOM focus, the attribute is absent.
  Source: `floating-ui-react/hooks/useListNavigation.ts:758-766,947-952`. Test: [L3] `test/popupListConformanceTests.tsx` "closes on Escape without committing, and keeps focus on the trigger" (attribute absent after close), `expectFocusOnItem` / `expectNoHighlight` helpers (virtual focus model) (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): re-tagged [L2] → [L3] (ADR 13 §3): the module writes the attribute (Amendments 2026-11-03 (2); the ownership list's row for the list's reference, ADR 04 Amendments 2026-11-04), so it is observable in isolation; "(L4 for the popup-list suite rows)" stays. Green at L3: `js/composite-list.test.ts` "port: under virtual focus writes aria-activedescendant from the active item's own id on the reference and the floating element, and DOM focus stays on the reference", "port: aria-activedescendant is removed on close and absent while open with no active item" and "port: aria-activedescendant is never written under DOM focus, and leaving virtual focus removes the one written". Dated deviation on the id clause (Amendments 2026-11-03 (2), chosen by the user): items keep their stable generated ids and the module writes the active item's own id, so the port has neither the `{id}-{index}` scheme nor the `id` option. CMB-DOM-09's and AUT-DOM-03's deviations are tracks 10 and 11's. The L4 clause is green in the popup-list suite's Combobox configuration (virtual focus): "closes on Escape without committing, and keeps focus on the trigger" and every title that reads the highlight through `expectFocusOnItem` or `expectNoHighlight`, both modes (§4.14's port note).
- **CMP-ARIA-02** — MUST. The *floating* element carries the same `aria-activedescendant` attribute under the same conditions, except when the reference is a typeable combobox reference, in which case the floating element never carries it.
  Source: `floating-ui-react/hooks/useListNavigation.ts:770`, `floating-ui-react/utils/element.ts:64-69`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); proven at L3 by `js/composite-list.test.ts` "port: under virtual focus writes aria-activedescendant from the active item's own id on the reference and the floating element, and DOM focus stays on the reference" and "port: under virtual focus with a typeable combobox reference writes aria-activedescendant on the reference only", with CMP-ARIA-01's id deviation.
- **CMP-ARIA-03** — MUST. Disabled detection reads `aria-disabled="true"` (and the `disabled` attribute) from item elements only when `disabledIndices` is not provided (CMP-ALG-02).
  Source: `floating-ui-react/utils/composite.ts:550-553`. Test: [L3] `composite.test.ts` (floating-ui-react/utils) "always treats natively disabled elements as disabled, unlike aria-disabled ones".

### 4.5 Keyboard — composite roving groups (KEY)

The composite root handles `keydown` events that reach the root element (bubbling from any descendant, or relayed per CMP-KEY-14). Let *min* = first non-disabled index and *max* = last non-disabled index (CMP-ALG-05 with the root's `disabledIndices`), *F* = "forward step", *B* = "backward step" (CMP-ALG-14), and "—" = not handled: the key is not consumed (no `preventDefault`, no `stopPropagation`) and the highlighted index does not change.

| Key | `horizontal`, LTR | `horizontal`, RTL | `vertical` (LTR or RTL) | `both`, LTR | `both`, RTL |
|---|---|---|---|---|---|
| <kbd>ArrowRight</kbd> | F | B | — | F | B |
| <kbd>ArrowLeft</kbd> | B | F | — | B | F |
| <kbd>ArrowDown</kbd> | — | — | F | F | F |
| <kbd>ArrowUp</kbd> | — | — | B | B | B |
| <kbd>Home</kbd> | min when `enableHomeAndEndKeys`, else — | same | same | same | same |
| <kbd>End</kbd> | max when `enableHomeAndEndKeys`, else — | same | same | same | same |
| Any key above with <kbd>Shift</kbd>, <kbd>Control</kbd>, <kbd>Alt</kbd> or <kbd>Meta</kbd> held, unless that modifier is listed in `modifierKeys` | — | — | — | — | — |
| Any key above while the event target is an enabled text input/textarea and the caret is not at the edge in the key's direction, or a text selection exists, or <kbd>Shift</kbd> is held | — (native caret movement) | — | — | — | — |
| <kbd>PageUp</kbd>, <kbd>PageDown</kbd>, <kbd>Tab</kbd>, <kbd>Enter</kbd>, <kbd>Space</kbd>, <kbd>Escape</kbd>, character keys | — (native behavior; Space/Enter activation is specified by [button-behavior](button-behavior.md)) | — | — | — | — |

With grid navigation configured (`grid`), the same table applies except: <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd> are first resolved by the grid navigator as row moves **in every orientation** (CMP-ALG-12); <kbd>ArrowLeft</kbd>/<kbd>ArrowRight</kbd> are resolved by the grid navigator as moves confined to the current row only when `orientation` is `'both'`; for `'horizontal'` they fall through to the linear F/B step over the flat index order. When the grid navigator returns the current index, the linear rule for that key (if any) is applied as a fallback (so with `loopFocus` off, <kbd>ArrowDown</kbd> from a cell with no cell below moves to the next flat index; see §10 Q2).

- **CMP-KEY-01** — MUST. The composite root handles only the keys `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Home`, `End`; `Home`/`End` are handled only when `enableHomeAndEndKeys` is `true` (default `false`). All other keys are ignored by the root.
  Source: `internals/composite/composite.ts:13-22`, `internals/composite/root/useCompositeRoot.ts:84,207-210`. Test: [L3] `CompositeRoot.test.tsx` "Home key moves focus to the first item", "End key moves focus to the last item". Docs: `CHANGELOG.md v1.2.0 "Enable \`Home\`/\`End\` key navigation (#3971)"`.
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList` (Home and End on) by `tests/browser/tabs.spec.ts` "keyboard navigation when focus is on a tab", every case, both modes.
- **CMP-KEY-02** — MUST. A handled key is ignored when any of `Shift`, `Control`, `Alt`, `Meta` is held, except modifiers listed in `modifierKeys` (default none), which are allowed. The modifier state is read from the event's modifier-state query for each of those four keys.
  Source: `internals/composite/composite.ts:24-26`, `root/useCompositeRoot.ts:212-214,367-377`. Test: [L3] `CompositeRoot.test.tsx` "prevents arrow key navigation when any modifier key is pressed by default", "specifies allowed modifier keys that do not prevent arrow key navigation when pressed".
- **CMP-KEY-03** — MUST. The forward/backward mapping is: horizontal forward = `ArrowRight` (LTR) / `ArrowLeft` (RTL), horizontal backward = the other one; vertical forward = `ArrowDown`, vertical backward = `ArrowUp`. A key is a forward key when (orientation ≠ `'vertical'` and key = horizontal forward) or (orientation ≠ `'horizontal'` and key = `ArrowDown`); a backward key symmetrically.
  Source: `internals/composite/root/useCompositeRoot.ts:221-226,267-272`. Test: [L3] `CompositeRoot.test.tsx` "sets default prevention to $prevented for $key with $orientation orientation", rtl "horizontal orientation", rtl "both horizontal and vertical orientation".
  Port note (2026-10-22, M2 track 3, issue 79; js/CONTEXT.md): the rtl titles are browser-only in Base UI (`describe.skipIf(isJSDOM)`), so in `js/composite.test.ts` they keep the skip and run nowhere at L3; they run at L4 on the `composite-rtl` fixture, both modes, inside `<div dir="rtl">` and `DirectionProvider Direction="TextDirection.Rtl"`. The other titles run at L3 and L4.
- **CMP-KEY-04** — MUST. When the event's original target (first element of the composed path) is an enabled native text input (an `input` exposing a selection range, or a `textarea`), the key is ignored (native caret behavior) if: the selection start is unknown, or <kbd>Shift</kbd> is held, or a non-collapsed selection exists, or the key is not the backward key and the caret is not at the end of the value, or the key is not the forward key and the caret is not at the start of the value. Here "forward/backward key" are the orientation's primary keys (`ArrowDown`/`ArrowUp` for `'vertical'`, horizontal forward/backward otherwise).
  Source: `internals/composite/composite.ts:28-42`, `root/useCompositeRoot.ts:223-246`. Test: [L3] `CompositeRoot.test.tsx` "keeps native input behavior when the native target differs from the synthetic target".
- **CMP-KEY-05** — MUST. <kbd>Home</kbd> sets the candidate index to min and <kbd>End</kbd> to max (when enabled), regardless of orientation and direction, and regardless of grid navigation.
  Source: `internals/composite/root/useCompositeRoot.ts:274-280`. Test: [L3] `CompositeRoot.test.tsx` "Home key moves focus to the first item", "End key moves focus to the last item", grid "uniform 1x1 items", grid rtl "horizontal orientation".
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList` by `tests/browser/tabs.spec.ts` "when `activateOnFocus = false`, moves focus to the first tab without activating it" and the other Home and End titles, both modes.
- **CMP-KEY-06** — MUST. A forward key when the candidate index is still the current index and equals max and `loopFocus` is `true` sets the candidate to min, then (if a loop hook is configured) to the loop hook's return value `onLoop(event, currentIndex, min, elementsArray)`.
  Source: `internals/composite/root/useCompositeRoot.ts:282-287`. Test: [L3] `CompositeRoot.test.tsx` "calls onLoop and uses its return value".
  Port note (2026-10-22, M2 track 3, issue 79; CMP §10 Q1): "calls onLoop and uses its return value" needs `onLoop` (deferred); the forward wrap without a loop hook runs as the L3 port title "wraps a forward key from the last item to the first when looping (CMP-KEY-06)".
- **CMP-KEY-07** — MUST. A backward key when the candidate index is still the current index and equals min and `loopFocus` is `true` sets the candidate to max, then to the loop hook's return value if configured.
  Source: `internals/composite/root/useCompositeRoot.ts:288-292`. Test: [L3] `CompositeRoot.test.tsx` rtl "horizontal orientation" ("loop backward" step).
  Port note (2026-10-22, M2 track 3, issue 79; js/CONTEXT.md): the rtl titles are browser-only in Base UI (`describe.skipIf(isJSDOM)`), so in `js/composite.test.ts` they keep the skip and run nowhere at L3; they run at L4 on the `composite-rtl` fixture, both modes, inside `<div dir="rtl">` and `DirectionProvider Direction="TextDirection.Rtl"`. The backward wrap is the "loop backward" step of rtl "horizontal orientation".
- **CMP-KEY-08** — MUST. Otherwise a forward/backward key whose candidate index is still the current index sets the candidate to the adjacent non-disabled index in that direction (CMP-ALG-04 from the current index), which may be out of bounds.
  Source: `internals/composite/root/useCompositeRoot.ts:293-299`. Test: [L3] `CompositeRoot.test.tsx` "disables navigating item when their index is included", "allows navigating items disabled in the DOM when their index is excluded".
  Port note (2026-10-26, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26): the skipped indices may be derived by the module from the items' `disabled` and `focusableWhenDisabled` flags (`CompositeOptions.DeriveDisabledIndices`), Toolbar's computed `disabledIndices` (`ToolbarRoot.tsx:32-46`); without the option the flags travel and nothing is derived, so TabsList's `[]` keeps `aria-disabled` Tabs navigable (TAB-KEY-07). L3 port titles "skips an item that its flags disable when an arrow key moves the highlight (CMP-KEY-08)", "keeps an aria-disabled item navigable when its flags make it focusable when disabled" and "derives the indices only when asked: with deriveDisabledIndices a flagged item is skipped, under an explicit [] it is not"; L4 port title "a runtime flag change moves the tab stop off an item that is no longer focusable". The public-part proof is Toolbar's (track 4).
- **CMP-KEY-09** — MUST. When `loopFocus` is `false` and the step leaves the list (candidate out of bounds), nothing happens and the loop hook is not called.
  Source: `internals/composite/root/useCompositeRoot.ts:282-300,302`. Test: [L3] `CompositeRoot.test.tsx` "does not loop or call onLoop when loopFocus is disabled" (list and grid).
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList` by `tests/browser/tabs.spec.ts` "does not wrap focus past the first tab when `loopFocus` is false" and "does not wrap focus past the last tab when `loopFocus` is false", both modes.
- **CMP-KEY-10** — MUST. When the candidate index differs from the current index and is in bounds: the event's propagation is stopped if `stopEventPropagation` is `true`; the event's default is prevented if grid navigation is configured or the key is <kbd>Home</kbd>/<kbd>End</kbd> or a forward/backward key; the highlighted index is set to the candidate with scroll-into-view (CMP-STA-30); and the candidate's element is focused after a microtask (so that a focus manager's return-focus work that runs synchronously has completed).
  Source: `internals/composite/root/useCompositeRoot.ts:302-316`. Test: [L4] `CompositeRoot.test.tsx` "controlled mode", "sets default prevention to $prevented for $key with $orientation orientation" (`prevented: true` rows), grid "prevents default for grid navigation outside the configured orientation".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 13 Amendments 2026-10-22): M2 runs the six "sets default prevention to $prevented for $key with $orientation orientation" variants at L4 on the `composite-list` fixture, both modes, asserting the default prevented and the propagation stopped exactly for the `prevented: true` rows; "controlled mode"'s clauses run as "uncontrolled mode" (CMP-KEY-16); the grid title waits for a consumer (CMP §10 Q1).
  Port note (2026-10-23, M2 track 8, issue 84; ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `RadioGroup` by `tests/browser/radio-group.spec.ts` "port: a handled arrow is default-prevented and stopped, and an arrow with no other enabled radio is neither" (the handled half), both modes.
- **CMP-KEY-11** — MUST. When the candidate index equals the current index (key outside the orientation, end reached without looping, loop hook returning the current index, all other items disabled) or is out of bounds, the event is neither prevented nor stopped and no focus change occurs.
  Source: `internals/composite/root/useCompositeRoot.ts:302`. Test: [L4] `CompositeRoot.test.tsx` "sets default prevention to $prevented for $key with $orientation orientation" (`prevented: false` rows), "stays on the current item when onLoop returns prevIndex".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 13 Amendments 2026-10-22): the `prevented: false` variants run at L4 on the `composite-list` fixture, both modes; "stays on the current item when onLoop returns prevIndex" waits for a consumer of `onLoop` (CMP §10 Q1).
  Port note (2026-10-23, M2 track 8, issue 84; ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `RadioGroup` by `tests/browser/radio-group.spec.ts` "port: a handled arrow is default-prevented and stopped, and an arrow with no other enabled radio is neither" (the all-other-items-disabled clause), and "does not select on refocus after an ignored arrow (%s)", both modes.
- **CMP-KEY-12** — MUST. When `grid` is configured, the navigator (CMP-ALG-12) is consulted first with the current index, min, max, orientation, `loopFocus`, direction, `disabledIndices` and the loop hook; its result becomes the candidate before the <kbd>Home</kbd>/<kbd>End</kbd> and linear rules (CMP-KEY-05 to CMP-KEY-09) are applied.
  Source: `internals/composite/root/useCompositeRoot.ts:252-265`. Test: [L3] `CompositeRoot.test.tsx` grid "uniform 1x1 items", "skips disabled indices", "packs items into earlier gaps when dense", "calls onLoop while navigating through grid cells", "calls onLoop when looping vertically between rows", rtl "uses the forward edge when navigating from a spanning item".
- **CMP-KEY-13** — MUST. The loop hook receives `(event, previousIndex, proposedIndex, elementsArray)` and its return value replaces the proposed index; returning the previous index leaves the highlight in place (CMP-KEY-11). In grid navigation the hook is invoked by the grid algorithm with the same first three arguments (the elements array is supplied by the root wrapper).
  Source: `internals/composite/root/useCompositeRoot.ts:195-202,285-291`, `root/gridNavigation.ts:12-16`. Test: [L3] `CompositeRoot.test.tsx` "calls onLoop and uses its return value", "calls onLoop while navigating through grid cells", "stays on the current item when onLoop returns prevIndex".
- **CMP-KEY-14** — MUST. The composite root exposes a keyboard relay function (through its ancestor-provided shared state) that runs the same key handling for an event that did not bubble through the root element (used by a menu whose popup is portaled outside a menubar: the menu trigger forwards the popup's keydown events to the menubar's relay).
  Source: `internals/composite/root/useCompositeRoot.ts:338`, `root/CompositeRootContext.ts:8-14`, `menu/trigger/MenuTrigger.tsx:106`. Test: none (covered by menubar component tests).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (4), chosen by the user; design point 3 (a), settled by the orchestrator 2026-11-04): `Test: none` kept (Menubar's tests cover it in Base UI); built in `composite` and proven by port titles. A roving root publishes its key resolution under a symbol on its element, and `relayKeyboardEvent(rootElement, event)` runs it for a keydown that did not bubble through the root and stops the event when handled. A list whose `CompositeListOptions.RelayElement` (an index into the references array) names the root relays each keydown its floating handle did not stop (`MenuRoot.tsx:692-699`). Option (i)'s handlers for a relayed key are the bar's: its `navigate` point on WebAssembly, its handled-key report on Server. L3, on a test-only menubar whose menu is portaled outside it, under both `synchronous` settings: `js/composite-list.test.ts` "port: a keydown the list does not stop is relayed to the composite root its relayElement names, and a key it handles is not", "port: a nested close key that is the bar's main key closes the menu and is relayed, moving the bar to the previous item", "port: without relayElement a keydown the list does not stop leaves the bar unchanged" and "port: runs a composite root's key resolution for a keydown that did not bubble through it, stops it when handled, and returns false for an element that is no root". At L4, on `composite-list-relay` (a test-only menubar whose Edit list is portaled outside it), both modes: `tests/browser/composite-list-rows.spec.ts` "port: a keydown the portaled list does not stop is relayed to the composite root, which moves to its next item and stops it" and "port: a nested close key that is the bar's main key closes the menu and is relayed, moving the bar to the previous item". Menubar (track 8) is the consumer.
- **CMP-KEY-15** — MUST. `stopEventPropagation` defaults to `true` for the composite root part and to `false` (unset) for the bare root mechanism used directly by components.
  Source: `internals/composite/root/CompositeRoot.tsx:34`, `root/useCompositeRoot.ts:85`. Test: none.
- **CMP-KEY-16** — MUST. When the key moves the highlight, the previously focused item loses focus and the new item gains it; the focus event on the new item (CMP-PTR-02) re-confirms the highlighted index without scrolling.
  Source: `internals/composite/root/useCompositeRoot.ts:313-315`, `item/useCompositeItem.ts:26-28`. Test: [L4] `CompositeRoot.test.tsx` "uncontrolled mode".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 13 Amendments 2026-10-22): re-cited from "controlled mode", whose consumer-owned highlight is M3, to "uncontrolled mode" at L4 (`tests/browser/composite.spec.ts`, `composite-list` fixture, both modes): each move is recorded as the tab stop moving while the old item has focus, then the focus arriving on the item that holds it.
  Port note (2026-10-23, M2 track 8, issue 84; ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `RadioGroup` by `tests/browser/radio-group.spec.ts` "port: after an arrow key the focus and the tab stop move in the same task, on both modes" and "moves focus with arrow keys and Tab", both modes.
- **CMP-KEY-17** — MUST. Focusing (via the root's `focus` handler, i.e. focus arriving anywhere inside the root) a native text input selects its entire value, so that the first arrow key returns control to the textbox per CMP-KEY-04 before moving focus.
  Source: `internals/composite/root/useCompositeRoot.ts:321-328`. Test: [L4] `CompositeRoot.test.tsx` "keeps native input behavior when the native target differs from the synthetic target".

### 4.6 Keyboard — list navigation in popups (KEY)

List navigation attaches `keydown` handlers to the *trigger/reference* element and to the *floating* element (item keydowns bubble to the floating element). Behavior depends on four key predicates (CMP-ALG-20) evaluated for an orientation *o* and direction:

| Predicate | `vertical`, LTR | `vertical`, RTL | `horizontal`, LTR | `horizontal`, RTL | `both`, LTR | `both`, RTL |
|---|---|---|---|---|---|---|
| MAIN(o) — main-orientation keys | <kbd>ArrowUp</kbd>, <kbd>ArrowDown</kbd> | same | <kbd>ArrowLeft</kbd>, <kbd>ArrowRight</kbd> | same | all four arrows | all four arrows |
| TOEND(o) — "toward the end" keys | <kbd>ArrowDown</kbd> | <kbd>ArrowDown</kbd> | <kbd>ArrowRight</kbd> | <kbd>ArrowLeft</kbd> | <kbd>ArrowDown</kbd>, <kbd>ArrowRight</kbd> | <kbd>ArrowDown</kbd>, <kbd>ArrowLeft</kbd> |
| (TOEND always also includes) | <kbd>Enter</kbd>, <kbd>Space</kbd> (`' '`), empty key (`''`) | | | | | |
| XOPEN(o) — cross-orientation open key (nested lists) | <kbd>ArrowRight</kbd> | <kbd>ArrowLeft</kbd> | <kbd>ArrowDown</kbd> | <kbd>ArrowDown</kbd> | <kbd>ArrowRight</kbd>, <kbd>ArrowDown</kbd> | <kbd>ArrowLeft</kbd>, <kbd>ArrowDown</kbd> |
| XCLOSE(o) — cross-orientation close key, no grid | <kbd>ArrowLeft</kbd> | <kbd>ArrowRight</kbd> | <kbd>ArrowUp</kbd> | <kbd>ArrowUp</kbd> | <kbd>Escape</kbd> | <kbd>Escape</kbd> |
| XCLOSE(o) — with grid navigation | <kbd>ArrowLeft</kbd> | <kbd>ArrowRight</kbd> | <kbd>Escape</kbd> | <kbd>Escape</kbd> | <kbd>Escape</kbd> | <kbd>Escape</kbd> |

**Table A — trigger/reference `keydown` while the popup is closed** (not nested; *o* = `triggerOrientation`, which defaults to `orientation`). "open → first/last" means: open the popup with reason `list-navigation` and record the key, so that the initial sync (CMP-STA-37) highlights the first or last non-disabled item.

| Key | `vertical` | `horizontal`, LTR | `horizontal`, RTL | `both`, LTR | `both`, RTL |
|---|---|---|---|---|---|
| <kbd>ArrowDown</kbd> | open → first | — | — | open → first | open → first |
| <kbd>ArrowUp</kbd> | open → last | — | — | open → last | open → last |
| <kbd>ArrowRight</kbd> | — | open → first | open → last | open → first | open → last |
| <kbd>ArrowLeft</kbd> | — | open → last | open → first | open → last | open → first |
| <kbd>Enter</kbd>, <kbd>Space</kbd>, empty key | record key only (the popup is opened by the click/press mechanism of the component); if the popup opens, the first item is highlighted | same | same | same | same |
| any arrow when `openOnArrowKeyDown` is `false` | — (nothing recorded, nothing opened) | same | same | same | same |
| <kbd>Home</kbd>, <kbd>End</kbd>, <kbd>Escape</kbd>, <kbd>Tab</kbd>, characters | — | — | — | — | — |

**Table B — trigger/reference `keydown` while the popup is open** (not nested; *o* = `orientation`).

| Key | DOM focus (`virtual: false`) | Virtual focus (`virtual: true`) |
|---|---|---|
| MAIN(o) | consume the key; cursor := `selectedIndex` when not `null`; run the floating handling (Table C) from the cursor; then emit `onNavigate` again with the resulting index | run the floating handling (Table C) on the reference |
| <kbd>Home</kbd>, <kbd>End</kbd> | — | Table C (min/max, unless typeable combobox reference) |
| <kbd>Enter</kbd>, <kbd>Space</kbd>, empty key | record key; otherwise — | Table C (not handled there either: —) |
| others | — | Table C (—) |

**Table C — floating `keydown` (also item keydowns bubbling up); `commonOnKeyDown`.** Let *cur* = cursor (re-synchronized to `activeIndex` first, CMP-STA-32), *min*/*max* = first/last non-disabled index with the configured `disabledIndices`.

| Key | Condition | Effect |
|---|---|---|
| any | key code 229 (IME composition) | — (ignored) |
| any | popup closed and the event's current target is the popup focus element (closing animation) | — (ignored) |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | open, DOM focus, original target inside the popup focus element | consume; close with reason `focus-out` (cancelable); when not canceled, focus the return element (`nestedReturnFocusRef` element, else the reference) |
| <kbd>Shift</kbd>+<kbd>Tab</kbd> | original target outside the popup focus element (e.g. a dialog opened from the menu) | — |
| <kbd>Tab</kbd> (no Shift) | any | — (not intercepted; dismissal/focus management is specified in [floating-and-positioning](floating-and-positioning.md)) |
| XCLOSE(o) | `nested` | consume only if the key is not in MAIN(parent orientation); close with reason `list-navigation`; focus the return element |
| <kbd>Home</kbd> | reference is not a typeable combobox | consume; cursor := min; `onNavigate(min, event)` |
| <kbd>End</kbd> | reference is not a typeable combobox | consume; cursor := max; `onNavigate(max, event)` |
| <kbd>Home</kbd>, <kbd>End</kbd> | reference is a typeable combobox | — (caret movement in the input) |
| any | grid navigation configured | cursor := grid result when it differs (CMP-ALG-13; vertical arrows always, horizontal arrows only for `'both'`; those keys are consumed by the grid algorithm); if *o* is `'both'`, stop here |
| MAIN(o) | open, DOM focus, focus is inside the current target but not inside any item | consume; cursor := TOEND(o) ? min : max; `onNavigate`; if `activeIndex` already equals it, run the focus procedure directly |
| MAIN(o) | otherwise | consume; cursor := next index per CMP-ALG-06 with `decrement = !TOEND(o)`, `loopFocus`, `allowEscape`, `disabledIndices`, min, max (result may be `-1` → `null`); `onNavigate(index, event)` |
| non-MAIN arrows, <kbd>Escape</kbd> (non-nested), <kbd>Enter</kbd>, <kbd>Space</kbd>, <kbd>PageUp</kbd>/<kbd>PageDown</kbd>, characters | | — (activation, dismissal and typeahead are separate mechanisms) |

**Table D — nested list: trigger (submenu trigger) `keydown`** (`nested: true`; *p* = parent orientation = `parentOrientation` option, else the orientation published by the parent popup, CMP-STA-41).

| Key | Popup closed | Popup open |
|---|---|---|
| XOPEN(p) | consume; open with reason `list-navigation` (the first item is then highlighted because the recorded key is cleared for nested lists — CMP-STA-37 picks min) | consume; cursor := min (with `disabledIndices`); `onNavigate`; under virtual focus, focus the popup focus element |
| MAIN(p) and all other keys | — (the parent list handles them) | — (except: virtual focus and the current target is a text-entry element → Table C) |

- **CMP-KEY-18** — MUST. Any `keydown` on the trigger/reference or the floating element switches the input modality to keyboard (CMP-STA-39 is unaffected; this flag drives scroll-into-view and pointer-leave handling).
  Source: `floating-ui-react/hooks/useListNavigation.ts:552,845`. Test: [L3] `useListNavigation.webkit.test.tsx` "keeps keyboard modality when a stationary pointermove fires on the floating element".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "keeps keyboard modality when a stationary pointermove fires on the floating element", `useListNavigation.webkit.test.tsx`'s title, with `platform.engine.webkit` stubbed.
- **CMP-KEY-19** — MUST. While the popup is closed and `openOnArrowKeyDown` is `false`, any key whose name starts with `Arrow` is ignored entirely on the trigger (no key recorded, no open).
  Source: `floating-ui-react/hooks/useListNavigation.ts:866-870`. Test: [L3] `useListNavigation.test.tsx` "does not open on ArrowDown when false", "does not open on ArrowUp when false".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "does not open on ArrowDown when false" and "does not open on ArrowUp when false".
- **CMP-KEY-20** — MUST. Under virtual focus, while the popup is open, a trigger `keydown` is processed by the floating handling (Table C) when the list is not nested or the current target is a text-entry element; nothing else in Table A/B/D applies to that event.
  Source: `floating-ui-react/hooks/useListNavigation.ts:862-864`. Test: [L3] `useListNavigation.test.tsx` "allowEscape + virtual › when true", "resets indexRef to -1 upon close".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "allowEscape + virtual › when true" and "resets indexRef to -1 upon close"; the reference handle under virtual focus also in "port: under virtual focus writes aria-activedescendant from the active item's own id on the reference and the floating element, and DOM focus stays on the reference".
- **CMP-KEY-21** — MUST. A trigger `keydown` whose key is in MAIN(`orientation` when open, else `triggerOrientation`) or is <kbd>Enter</kbd> or has an empty trimmed name (<kbd>Space</kbd>, `''`) is a *navigation key*; its name is recorded for the initial sync (CMP-STA-37), except that for nested lists a key in MAIN(parent orientation) records `null`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:847-860,872-875`. Test: [L3] `useListNavigation.test.tsx` "opens on ArrowUp and focuses last item"; `test/popupListConformanceTests.tsx` "opens with %s without activating an item" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "opens on ArrowUp and focuses last item"; the recorded key lives in the list's shared state in the session's `dataRef` (design point 1 (a)). The L4 clause is green by the popup-list suite's "opens with %s without activating an item" (Menu and Select, Enter and Space), both modes. On Server the open's initial highlight is CMP-STA-37's figure (ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-KEY-22** — MUST. For a non-nested list, a trigger `keydown` in MAIN(o) (o as in CMP-KEY-21): sets the cursor to `selectedIndex` when not `null`; consumes the event (default prevented, propagation stopped); when closed and `openOnArrowKeyDown` is `true` (default) opens the popup with reason `list-navigation` and the trigger element as `trigger`; when open runs Table C and then emits `onNavigate(cursor, event)`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:816-825,895-911`. Test: [L3] `useListNavigation.test.tsx` "opens on ArrowDown and focuses first item", "navigates down on ArrowRight", "opens on ArrowDown when true", "opens on ArrowUp when true" (L4 for the consumption clause).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "opens on ArrowDown and focuses first item", "navigates down on ArrowRight", "opens on ArrowDown when true" and "opens on ArrowUp when true". The open goes to C# as `CompositeListOpenRequest(Open, Reason, Event, TriggerId)` with `list-navigation` and the trigger's id ("port: requests the open with the reason list-navigation and the trigger, and the leave close with focus-out"), and `open` is read from the session at event time (§8). The consumption clause is green at L4: `tests/browser/composite-list-rows.spec.ts` "port: an arrow key on the closed trigger is consumed: its default is prevented and it does not propagate", both modes.
  Port note (2026-11-10, settled by the orchestrator 2026-11-10; found by TLB-STR-05; ADR 04 Amendments 2026-11-10): this row is the one about the trigger's key handling, so it carries the disabled gate. A trigger `keydown` reaches none of this row's clauses, nor typeahead's (CMP-STR-07), while the trigger is disabled: Base UI's `useButton` returns from a disabled button's `onKeyDown` before the trigger's merged handlers run (`internals/use-button/useButton.ts:116-119`; `menu/trigger/MenuTrigger.tsx:261` merges `getButtonProps` last). The gate reads useButton's `disabled` parameter, so it also covers a trigger that is focusable when disabled, which renders `aria-disabled="true"` instead of the `disabled` attribute. The port's reference and trigger list handles listen independently of the trigger's `ButtonBehavior`, so their `keydown` listener in `js/composite.ts` returns first while its element is disabled per `isElementDisabled` (either attribute): no key is handled and no `navigate` decision is asked. The floating handle and the pointer handlers are unchanged. Green at L3: `js/composite-list.test.ts` "port: a disabled trigger's list handle ignores the arrow keys and typeahead, and opens on ArrowDown once enabled" and "port: an aria-disabled trigger's list handle, focusable when disabled, ignores the arrow keys and typeahead, and opens on ArrowDown once enabled". TLB-STR-05 found it: a disabled toolbar button substituting a menu trigger kept opening the menu on <kbd>ArrowDown</kbd> and <kbd>ArrowUp</kbd>.
- **CMP-KEY-23** — MUST. For a nested list, a trigger `keydown` in XOPEN(parent orientation) is consumed; when the popup is open the cursor becomes the first non-disabled index (with `disabledIndices`) and `onNavigate` is emitted (and under virtual focus the popup focus element is focused); when closed the popup is opened with reason `list-navigation`. Every other key on a nested trigger is left to the parent list (CMP-KEY-20 excepted).
  Source: `floating-ui-react/hooks/useListNavigation.ts:877-893`. Test: [L3] `useListNavigation.test.tsx` "focus management in nested lists", "keyboard navigation in nested menus lists", "keyboard navigation in nested menus with different orientation" (L4 for the consumption and focus clauses).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "focus management in nested lists", "keyboard navigation in nested menus lists" and "keyboard navigation in nested menus with different orientation", Base UI's three jsdom-only titles (`it.skipIf(!isJSDOM)`), on DOM built after `test/floating-ui-tests/Menu.tsx` and `MenuOrientation.tsx` by the menu-tree harness: one session per menu linked by the parent's token, each popup portaled outside its parent's. The consumption and focus clauses are green at L4 on `composite-list-nested` (the same menus, each submenu's session the parent's child through the test-only parent token), both modes: `tests/browser/composite-list-rows.spec.ts` "focus management in nested lists", "keyboard navigation in nested menus lists", "keyboard navigation in nested menus with different orientation" and "port: the cross-orientation open key on a submenu trigger is consumed and opens the submenu on its first item".
- **CMP-KEY-24** — MUST. A `keydown` of <kbd>Tab</kbd> with <kbd>Shift</kbd> on the floating element while open under DOM focus, whose original target is inside the popup focus element, is consumed and closes the popup with reason `focus-out`; unless the close was canceled through the event details, focus then moves to the return element (`nestedReturnFocusRef` element if set, else the reference element). If the original target is outside the popup focus element the event is not handled.
  Source: `floating-ui-react/hooks/useListNavigation.ts:771-792`. Test: [L4] `test/popupListConformanceTests.tsx` "closes on Shift+Tab without committing, and moves focus to the element before the popup" (DOM-focus suites expect the trigger focused).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: the popup-list suite's "closes on Shift+Tab without committing, and moves focus to the element before the popup" in the DOM-focus configurations (Menu and Select; §4.14's port note), both modes. The close is the list handle's `leave` decision point (Amendments 2026-11-03 (3); CMP-EVT-08's note): L3 `js/composite-list.test.ts` "port: requests the open with the reason list-navigation and the trigger, and the leave close with focus-out" and "port: on WebAssembly a canceled Shift+Tab close leaves the focus where it was"; L2 `ListNavigationTests` "on WebAssembly leave runs the Shift+Tab close inside the decision and answers stop when it was canceled". On Server a canceled close still moves the focus (`tests/browser/composite-list-figures.spec.ts` "port: leave: a canceled Shift+Tab close keeps the focus on WebAssembly and still moves it on Server"; ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-KEY-25** — MUST. The floating handling ignores `keydown` events whose legacy key code is 229 (in-progress IME composition).
  Source: `floating-ui-react/hooks/useListNavigation.ts:559-561`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); proven at L3 by `js/composite-list.test.ts` "port: the floating handling ignores a keydown whose legacy key code is 229, and handles the same key without it".
- **CMP-KEY-26** — MUST. The floating handling ignores `keydown` events while the popup is closed when the current target is the popup focus element (the popup is animating out), so the active index is not set to `0` on a closed popup.
  Source: `floating-ui-react/hooks/useListNavigation.ts:566-568`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as in Base UI: built in `js/composite.ts` (`commonKeyDown`: a closed list ignores a keydown whose current target is its floating handle's element, CMP-DOM-05's reading), with no title.
- **CMP-KEY-27** — MUST. For a nested list, a key in XCLOSE(orientation, direction, grid) closes the nested popup with reason `list-navigation` and focuses the return element; the event is consumed only when the key is not in MAIN(parent orientation), so a key that also navigates the parent reaches it. When the parent orientation is unknown (no `parentOrientation` option and no orientation published by a parent popup, CMP-STA-41), MAIN(parent orientation) is the `'both'` set (all four arrows), so an arrow close key is never consumed by the nested list; <kbd>Escape</kbd> is still consumed.
  Source: `floating-ui-react/hooks/useListNavigation.ts:41-54,539-545,570-585`. Test: [L3] `useListNavigation.test.tsx` "keyboard navigation in nested menus lists" (<kbd>Escape</kbd> closes the `'both'` submenu and focuses "Image"), "keyboard navigation in nested menus with different orientation" (<kbd>ArrowLeft</kbd> closes the vertical submenu of a horizontal menu).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): re-tagged [L4] → [L3] (ADR 13 §3): both cited titles are among Base UI's `it.skipIf(!isJSDOM)` titles (`useListNavigation.test.tsx:1426`, `:1463`), which run in jsdom only. They run verbatim in `js/composite-list.test.ts` "keyboard navigation in nested menus lists" and "keyboard navigation in nested menus with different orientation", on the menu-tree harness. React bubbles a portaled child popup's keydown through the React tree to the parent popup's handlers; the port hands a keydown the child's floating handle did not stop to the parent list's floating handle, so a close key that is the parent's main key also moves the parent (in the second title, ArrowLeft closes "Share" and the horizontal parent lands on "Copy as"). `CompositeListOptions.KeepOpenOnCrossOrientationKey` keeps a nested root menu open on the key, for ContextMenu (CTX-KEY-07, not replicated, ADR 04 Amendments 2026-10-18 as corrected 2026-11-04): "port: a nested list with keepOpenOnCrossOrientationKey stays open on its cross-orientation close key, which another nested list closes on". The cited titles also run at L4 on `composite-list-nested`, with the focus return across the portal: `tests/browser/composite-list-rows.spec.ts` "keyboard navigation in nested menus lists", "keyboard navigation in nested menus with different orientation", "port: a nested list's close key returns the focus across the portal to its submenu trigger, consumed when it is not the parent's main key" and "port: a nested list's close key that is the parent's main key closes the submenu and moves the parent list", both modes.
- **CMP-KEY-28** — MUST. Unless the reference is a typeable combobox reference, <kbd>Home</kbd> moves the cursor to min and <kbd>End</kbd> to max (computed with the configured `disabledIndices`), consuming the event and emitting `onNavigate`. For a typeable combobox reference both keys are left to the input.
  Source: `floating-ui-react/hooks/useListNavigation.ts:599-615`. Test: [L3] `useListNavigation.test.tsx` "Home or End key press is ignored for typeable combobox reference"; `test/popupListConformanceTests.tsx` "moves the highlight to the last and first items with End and Home", "moves to disabled ends with End and Home" (Menu and Select configure `disabledIndices` so that disabled end items remain reachable by Home/End) (L4 for the consumption clause and the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "Home or End key press is ignored for typeable combobox reference". The L4 clause is green on both modes: the consumption clause by `tests/browser/composite-list-rows.spec.ts` "Home or End key press is ignored for typeable combobox reference" and "port: Home and End on the open list are consumed and move to the first and last items", the suite rows by "moves the highlight to the last and first items with End and Home" and "moves to disabled ends with End and Home" (Menu and Select; §4.14's port note).
- **CMP-KEY-29** — MUST. When grid navigation is configured, the navigator (CMP-ALG-13) runs before the main-key rule with the cursor, orientation, `loopFocus`, direction, `disabledIndices`, min and max; when it returns a different in-bounds index the cursor is updated and `onNavigate(index, event)` emitted; when `orientation` is `'both'` no further rule runs for that key.
  Source: `floating-ui-react/hooks/useListNavigation.ts:617-642`. Test: [L3] `useListNavigation.test.tsx` "skips row and remains on same column when pressing ArrowDown", "does not leave row with "both" orientation while looping", "looping works on last row".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "skips row and remains on same column when pressing ArrowDown", "does not leave row with "both" orientation while looping" and "looping works on last row", on DOM built after `test/floating-ui-tests/Grid.tsx` and `ComplexGrid.tsx`, with the columns from `CompositeListOptions.Cols` (CMP-ALG-13).
- **CMP-KEY-30** — MUST. A key in MAIN(orientation) on the floating element is always consumed (default prevented, propagation stopped), including at a list end without looping and when the list is empty.
  Source: `floating-ui-react/hooks/useListNavigation.ts:644-645`. Test: [L4] `test/popupListConformanceTests.tsx` "prevents the default scrolling of navigation keys", "opens and handles keys without losing focus when the list is empty".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: the popup-list suite's "prevents the default scrolling of navigation keys" and "opens and handles keys without losing focus when the list is empty" (Menu, Select, Combobox; §4.14's port note), both modes.
- **CMP-KEY-31** — MUST. Under DOM focus, a MAIN key pressed while focus is inside the current target but on no item (e.g. on the popup container) sets the cursor to min for a TOEND key and to max otherwise, emits `onNavigate`, and — when `activeIndex` already equals that index — runs the focus procedure (CMP-ALG-23) immediately so focus still lands on the item.
  Source: `floating-ui-react/hooks/useListNavigation.ts:647-667`. Test: [L3] `test/popupListConformanceTests.tsx` "starts without a highlight after a pointer opening" (<kbd>ArrowDown</kbd> after a pointer open highlights item 0), "clears the highlight when the pointer leaves the list without leaving focus on an item" (L4 for the focus clause and the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3 through titles other than the cited ones, which are the popup-list suite's and run at L4 in the port (§4.14's port note). In jsdom the branch runs in `js/composite-list.test.ts` "focus management in nested lists" and "keyboard navigation in nested menus lists", whose first ArrowDown after a pointer open, with the root menu's popup focused and no item, highlights the first item; and in "port: aria-activedescendant is never written under DOM focus, and leaving virtual focus removes the one written", where a key on the open reference that holds the focus starts at the first item. The L4 clause is green by the popup-list suite's "starts without a highlight after a pointer opening" and "clears the highlight when the pointer leaves the list without leaving focus on an item" (Menu, Select, Combobox), both modes.
- **CMP-KEY-32** — MUST. Otherwise a MAIN key moves the cursor to the result of CMP-ALG-06 (`decrement` = key not in TOEND(orientation)) and emits `onNavigate(index or null, event)`; when the step wrapped, the next focus is deferred to the next frame instead of being applied synchronously (to let virtualized lists render the target).
  Source: `floating-ui-react/hooks/useListNavigation.ts:669-684`. Test: [L3] `useListNavigation.test.tsx` "navigates down on ArrowDown", "navigates up on ArrowUp", "ArrowDown looping", "ArrowUp looping", "indices are skipped in focus order", "skips items hidden with CSS in navigation", "skips visibility:hidden items in navigation".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "navigates down on ArrowDown", "navigates up on ArrowUp", "ArrowDown looping", "ArrowUp looping", "indices are skipped in focus order", "skips items hidden with CSS in navigation" and "skips visibility:hidden items in navigation".
- **CMP-KEY-33** — MUST. With `allowEscape` (requires `loopFocus` and virtual focus), a MAIN key at the first/last item moves the cursor to `-1` (`onNavigate(null)`), and the next MAIN key from outside the list enters at the far end (first item for a TOEND key, last item otherwise).
  Source: `floating-ui-react/utils/composite.ts:68-77`. Test: [L3] `useListNavigation.test.tsx` "allowEscape + virtual › when true", "true - onNavigate is called with `null` when escaped"; `test/popupListConformanceTests.tsx` "leaves the list past the first item" / "leaves the list past the last item" (Combobox suite, `listEnd: 'escape'`), "keeps a single item reachable" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "allowEscape + virtual › when true" and "true - onNavigate is called with `null` when escaped". The L4 clause is green in the popup-list suite's Combobox configuration (`listEnd: 'escape'`): "leaves the list past the first item", "leaves the list past the last item" and "keeps a single item reachable", both modes.
- **CMP-KEY-34** — MUST. Keys not listed in Tables A–D are not intercepted by list navigation: <kbd>Tab</kbd> moves focus natively (and dismissal is handled elsewhere), <kbd>Escape</kbd> on a non-nested list is left to the dismiss mechanism, <kbd>Enter</kbd>/<kbd>Space</kbd> activation is the item's responsibility, and printable characters are left to typeahead (§4.9) or the input.
  Source: `floating-ui-react/hooks/useListNavigation.ts:551-685,842-914`. Test: [L4] `test/popupListConformanceTests.tsx` "closes on Tab without committing, and moves focus past the trigger", "closes on Escape without committing, and keeps focus on the trigger".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: the popup-list suite's "closes on Tab without committing, and moves focus past the trigger" and "closes on Escape without committing, and keeps focus on the trigger" (Menu, Select, Combobox; §4.14's port note), both modes; list navigation leaves both keys to the dismissal mechanisms (FLT, PSH).

### 4.7 Pointer (PTR)

- **CMP-PTR-01** — MUST. When the composite root has `highlightItemOnHover` (default `false`), a `mousemove` over a composite item that is not the highlighted item and is not disabled (has no `disabled` attribute and `aria-disabled` is not `"true"`) focuses that item (which in turn makes it the highlighted item, CMP-PTR-02). Without the option, `mousemove` has no effect.
  Source: `internals/composite/item/useCompositeItem.ts:29-39`, `root/CompositeRoot.tsx:38`. Test: none (exercised by menubar tests). Docs: `CHANGELOG.md v1.2.0 "Fix \`highlightItemOnHover\` not being respected (#3868)"`.
- **CMP-PTR-02** — MUST. A composite item receiving focus (from any source: keyboard, pointer, programmatic) sets the highlighted index to its index without scrolling.
  Source: `internals/composite/item/useCompositeItem.ts:26-28`. Test: [L3] `CompositeRoot.test.tsx` "uncontrolled mode" (focusing item 1 then navigating).
- **CMP-PTR-03** — MUST. Under DOM focus, a `click` on a list item focuses the item without scrolling (WebKit does not focus buttons on click). Under virtual focus the click does not move focus.
  Source: `floating-ui-react/hooks/useListNavigation.ts:693-698`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as in Base UI: built in `js/composite.ts` (`itemClick`, delegated on the floating handle: under DOM focus a click focuses the item with `preventScroll`, under virtual focus it moves nothing), with no title.
- **CMP-PTR-04** — MUST. A `mousemove` on a list item (that is not a stationary WebKit event, CMP-PTR-08) marks subsequent focus as synchronous, clears the forced-scroll flag, and — when `focusItemOnHover` is `true` (default) and the popup is open — sets the cursor to the item's index and emits `onNavigate(index, event)` if either the cursor or `activeIndex` differed from it.
  Source: `floating-ui-react/hooks/useListNavigation.ts:527-537,699-708`. Test: [L3] `useListNavigation.test.tsx` "true - focuses item on hover and syncs the active index", "true - syncs an item on hover when activeIndex is null but selectedIndex matches", "false - does not focus item on hover and does not sync the active index"; `test/popupListConformanceTests.tsx` "highlights and focuses the hovered item", "lets the pointer take the highlight back after keyboard navigation" (L4 for the popup-list suite rows).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "true - focuses item on hover and syncs the active index", "true - syncs an item on hover when activeIndex is null but selectedIndex matches" and "false - does not focus item on hover and does not sync the active index"; the items' `mousemove` is delegated on the floating handle. The L4 clause is green on both modes: `tests/browser/composite-list-rows.spec.ts` "true - focuses item on hover and syncs the active index" and "false - does not focus item on hover and does not sync the active index", and the popup-list suite's "highlights and focuses the hovered item" and "lets the pointer take the highlight back after keyboard navigation" (Menu, Select, Combobox).
- **CMP-PTR-05** — MUST. A `pointerleave` on a list item is ignored when the popup is closed, when the input modality is keyboard, or when the pointer type is `touch`. Otherwise it marks subsequent focus as synchronous and, provided `focusItemOnHover` is `true`, the related target is not another list item, and `resetOnPointerLeave` is `true` (default): cancels any deferred item focus, sets the cursor to `-1`, emits `onNavigate(null, event)`, and under DOM focus — when the popup focus element contains the active element — focuses the popup focus element without scrolling.
  Source: `floating-ui-react/hooks/useListNavigation.ts:709-743`. Test: [L3] `useListNavigation.test.tsx` "cancels pending item focus when the pointer leaves before focus lands", "clears the active item when the pointer leaves a clipped container while still within the item bounds"; `useListNavigation.webkit.test.tsx` "keeps keyboard modality when a stationary pointermove fires on the floating element" (leave ignored under keyboard modality); `test/popupListConformanceTests.tsx` "clears the highlight when the pointer leaves the list without leaving focus on an item", "keeps the highlight when a touch scroll starts on the highlighted item" (L4 for the focus clause and the popup-list suite rows). Docs: `CHANGELOG.md v1.4.1 "Clear highlight on pointer leave when item is clipped by scroll container (#4604)"`.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3 for its jsdom titles: `js/composite-list.test.ts` "clears the active item when the pointer leaves a clipped container while still within the item bounds" and `useListNavigation.webkit.test.tsx`'s "keeps keyboard modality when a stationary pointermove fires on the floating element". The floating handle hears an item's `pointerleave` in the capture phase, since the event does not bubble. "cancels pending item focus when the pointer leaves before focus lands" is browser-only in Base UI (`it.skipIf(isJSDOM)`, `useListNavigation.test.tsx:744`) and is green under this row's "(L4 for the focus clause …)": `tests/browser/composite-list-rows.spec.ts` "cancels pending item focus when the pointer leaves before focus lands", both modes (CMP-ALG-31's re-tag). The suite rows are green by "clears the highlight when the pointer leaves the list without leaving focus on an item" and "keeps the highlight when a touch scroll starts on the highlighted item" (Menu, Select, Combobox; §4.14's port note).
- **CMP-PTR-06** — MUST. A `pointermove` on the floating element (that is not a stationary WebKit event) switches the input modality to pointer.
  Source: `floating-ui-react/hooks/useListNavigation.ts:796-801`. Test: [L3] `useListNavigation.webkit.test.tsx` "keeps keyboard modality when a stationary pointermove fires on the floating element".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "keeps keyboard modality when a stationary pointermove fires on the floating element".
- **CMP-PTR-07** — MUST. Trigger `pointerdown`, `pointerenter`, `mousedown` and `click` feed the focus-on-open resolution of CMP-STA-39 and have no other effect.
  Source: `floating-ui-react/hooks/useListNavigation.ts:827-839,925-928`. Test: [L3] `useListNavigation.test.tsx` "focuses the first item on click when true".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "focuses the first item on click when true", and "port: a virtual click on the trigger opens with the first item highlighted, and a pointer click opens with none".
- **CMP-PTR-08** — MUST. On WebKit engines (Safari, all iOS browsers; detected as support for `-webkit-backdrop-filter`), `mousemove`/`pointermove` events whose `movementX` and `movementY` are both `0` are ignored by CMP-PTR-04 and CMP-PTR-06, because WebKit emits such events when the list scrolls beneath a stationary pointer during keyboard navigation. Non-WebKit engines do not apply this filter.
  Source: `floating-ui-react/hooks/useListNavigation.ts:34-39`, `packages/utils/src/platform/engine.ts:7-8`. Test: [L3] `useListNavigation.webkit.test.tsx` "ignores stationary mousemove events fired when the list scrolls beneath the pointer", "keeps keyboard modality when a stationary pointermove fires on the floating element"; `test/popupListConformanceTests.tsx` "keeps the keyboard highlight while the list scrolls under a resting pointer" (L4 for the popup-list suite rows). Docs: `CHANGELOG.md v1.7.0 "Fix hovered item stealing highlight when the list scrolls in Safari (#5265)"`.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "ignores stationary mousemove events fired when the list scrolls beneath the pointer" and "keeps keyboard modality when a stationary pointermove fires on the floating element", with `platform.engine.webkit` stubbed (the platform module mocked, as the focus-manager tests do). The L4 clause is green by the popup-list suite's "keeps the keyboard highlight while the list scrolls under a resting pointer" (Menu, Select, Combobox), both modes.

### 4.8 Focus management (FOC)

- **CMP-FOC-01** — MUST. A composite group occupies exactly one position in the sequential tab order: tabbing into it lands on the item with `tabindex="0"` (the highlighted item), and tabbing again leaves the group.
  Source: `internals/composite/item/useCompositeItem.ts:25`. Test: [L4] port title "Tab lands on the item holding the tab stop and Tab leaves the group" (`tests/browser/composite.spec.ts`).
  Port note (2026-10-22, M2 track 3, issue 79; ADR 13 Amendments 2026-10-22): re-cited from "keeps outer metadata and focus navigation when items share a DOM node" (`user.tab()` lands on the first item), whose two registrations on one element are M3 (CMP-STR-04), to the port title, on the `composite-list` fixture, both modes.
  Port note (2026-10-23, M2 track 8, issue 84; ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `RadioGroup` by `tests/browser/radio-group.spec.ts` "port: Tab enters the group on the radio holding the tab stop and leaves it in one press" and "moves focus with arrow keys and Tab" (both directions), both modes.
- **CMP-FOC-02** — MUST. After a composite key move, DOM focus moves to the new highlighted item one microtask after the keydown (CMP-KEY-10), so focus and tab stop stay together.
  Source: `internals/composite/root/useCompositeRoot.ts:313-315`. Test: [L4] `CompositeRoot.test.tsx` "uncontrolled mode" (asserts after `flushMicrotasks`).
  Port note (2026-10-22, M2 track 3, issue 79; ADR 13 Amendments 2026-10-22): re-cited from "controlled mode" (a consumer-owned highlight, M3) to "uncontrolled mode", at L4 on the `composite-list` fixture, both modes, and at L3. In a browser a microtask checkpoint follows every event listener, so the module's microtask focus lands before any later listener runs; the L4 title witnesses the order by records the page keeps (the `tabindex` mutation record, then the `focusin`).
  Port note (2026-10-23, M2 track 8, issue 84; ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `RadioGroup` by `tests/browser/radio-group.spec.ts` "port: after an arrow key the focus and the tab stop move in the same task, on both modes", with 150 ms of added Server latency each way, so no round trip is involved.
- **CMP-FOC-03** — MUST. *List navigation focus procedure* (CMP-ALG-23). Under DOM focus, whenever the cursor is applied (CMP-STA-35, CMP-KEY-31), the item at the cursor is focused with `preventScroll: true`. The focus is applied synchronously when the "synchronous focus" flag is set (set by any keydown, item focus, non-stationary item mousemove, or pointerleave; cleared when the popup closes, when `activeIndex` becomes `null`, and after a wrapping step) and otherwise on the next frame. A deferred focus request replaces any earlier pending deferred focus request (process-wide, CMP-ALG-31).
  Source: `floating-ui-react/hooks/useListNavigation.ts:327-372,420,428,553,679,690,703,718`, `floating-ui-react/utils/enqueueFocus.ts:12-37`. Test: [L4] `useListNavigation.test.tsx` "navigates down on ArrowDown", "cancels pending item focus when the pointer leaves before focus lands"; `test/popupListConformanceTests.tsx` "moves focus along with the highlight".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4, both modes: `tests/browser/composite-list-rows.spec.ts` "navigates down on ArrowDown" and "cancels pending item focus when the pointer leaves before focus lands", and the popup-list suite's "moves focus along with the highlight" (Menu and Select; §4.14's port note). The deferred focus is the `focus-manager` module's `enqueueFocus` (CMP-ALG-31). With `js/focus-manager.js` stalled, the focus stays on the first item once the focus manager attaches (`tests/browser/composite-list-rows.spec.ts` "port: with js/focus-manager.js stalled the list opens on ArrowDown on its first item, and the focus stays there once the focus manager attaches"), and with `js/composite.js` stalled the focus manager re-picks its initial focus onto the item the list handle writes `tabindex="0"` on ("port: with js/composite.js stalled the focus manager re-picks its initial focus onto the item the list handle writes tabindex="0" on").
- **CMP-FOC-04** — MUST. In the same procedure, after focusing (synchronously if the flag is set, else on the next frame), the item is scrolled into view with `block: 'nearest', inline: 'nearest'` when the forced-scroll flag is set (selected item on open, CMP-STA-33) or the input modality is keyboard; pointer-driven highlights never scroll. If the item was absent when the procedure started but is present when the deferred step runs, that later item is focused and scrolled instead.
  Source: `floating-ui-react/hooks/useListNavigation.ts:347-371`. Test: [L4] `useListNavigation.test.tsx` "scrolls the selected item into view on open"; `test/popupListConformanceTests.tsx` "scrolls the highlighted item into view in both directions, only as far as needed", "does not scroll the list when hovering a partially visible item at either edge", "scrolls the last item into view when opening with ArrowUp".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4, both modes: `tests/browser/composite-list-rows.spec.ts` "scrolls the selected item into view on open", and the popup-list suite's "scrolls the highlighted item into view in both directions, only as far as needed", "does not scroll the list when hovering a partially visible item at either edge" and "scrolls the last item into view when opening with ArrowUp" (Menu, Select, Combobox; §4.14's port note).
- **CMP-FOC-05** — MUST. Under virtual focus the procedure never moves DOM focus (scroll-into-view still applies).
  Source: `floating-ui-react/hooks/useListNavigation.ts:331-338`. Test: [L4] `test/popupListConformanceTests.tsx` "moves focus along with the highlight" (virtual suite: trigger keeps focus, `aria-activedescendant` points at the item).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: the popup-list suite's "moves focus along with the highlight" in the Combobox configuration (virtual focus: the trigger keeps the focus and `aria-activedescendant` names the item; §4.14's port note), both modes; at L3, CMP-ARIA-01's virtual-focus port titles.
- **CMP-FOC-06** — MUST. Under DOM focus, when the reference element itself receives focus (the event target is the reference, not a descendant) while the popup is open, the cursor becomes `-1` and `onNavigate(null, event)` is emitted (the item highlight is cleared).
  Source: `floating-ui-react/hooks/useListNavigation.ts:915-924`. Test: [L3] `useListNavigation.test.tsx` "clears ordinary item focus when the open reference receives focus".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "clears ordinary item focus when the open reference receives focus".
- **CMP-FOC-07** — MUST. A list item receiving focus marks subsequent focus as synchronous and synchronizes the cursor/active index to that item (as CMP-PTR-04, without the hover option gate).
  Source: `floating-ui-react/hooks/useListNavigation.ts:689-692`. Test: [L3] `useListNavigation.test.tsx` "focuses previous item using ArrowLeft key, skipping disabled items" (programmatic focus of item 47 then navigation from it).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "focuses previous item using ArrowLeft key, skipping disabled items" (item 47 of the `Grid.tsx` DOM focused, then the key).
- **CMP-FOC-08** — MUST. When a nested popup's floating element unmounts (it closes) under DOM focus with a popup tree available, and the input modality is pointer, and the document's active element is inside none of the tree's popups, the parent popup's floating element is focused without scrolling so that arrow keys keep working after the pointer left the child.
  Source: `floating-ui-react/hooks/useListNavigation.ts:500-518`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as in Base UI: built in `js/composite.ts` (`syncOpenAndMount`, after `useListNavigation.ts:481-503`), the parent's floating element read through the session's parent, with no title. Menu's submenus (track 6) reach it.
- **CMP-FOC-09** — MUST. The initial sync on open (CMP-STA-37) checks for the first list element immediately, then after a microtask, then after the next frame, and gives up silently if the list is still empty.
  Source: `floating-ui-react/hooks/useListNavigation.ts:446-476`. Test: [L4] `test/popupListConformanceTests.tsx` "opens and handles keys without losing focus when the list is empty" (L4 for the popup-list suite row).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): re-tagged [L3] → [L4] (the track's decision, ADR 13 §3): the cited title is the popup-list suite's, which the port runs at L4 (§4.14's port note). Green at L4: "opens and handles keys without losing focus when the list is empty" (Menu, Select, Combobox), both modes. The three checks are built (`js/composite.ts`, `syncActiveIndex`: now, after a microtask, after the next frame); no jsdom title opens an empty list. On Server the open's initial highlight follows the floating handle's attach (CMP-STA-37's figure, ADR 01 Amendments 2026-11-04). Server: see ADR 01.

### 4.9 Keyboard — typeahead (KEY, continued)

Typeahead handles `keydown` on the reference and floating elements (CMP-STR-07). *Available* item: see CMP-ALG-25 `isItemAvailable`. *Buffer*, *previous index*, *match index*: CMP-STA-42.

| Key | Condition | Effect |
|---|---|---|
| single-character key (`key.length === 1`) with none of <kbd>Control</kbd>, <kbd>Meta</kbd>, <kbd>Alt</kbd> | labels array present | accepted: appended to the buffer; match attempted (CMP-KEY-38 to CMP-KEY-41) |
| <kbd>Space</kbd> | buffer non-empty | consumed; `onTyping(true)`; then accepted like any character |
| <kbd>Space</kbd> | buffer empty | accepted like any character but **not** consumed (so the component may use it to select/open); a failed match leaves the single space in the buffer until the timer |
| any accepted character except <kbd>Space</kbd> | popup open | consumed (default prevented, propagation stopped); `onTyping(true)` |
| any accepted character except <kbd>Space</kbd> | popup closed | not consumed; matched silently (`onMatch` may still fire) |
| multi-character keys (<kbd>Enter</kbd>, arrows, <kbd>Backspace</kbd>, …), or any key with <kbd>Control</kbd>/<kbd>Meta</kbd>/<kbd>Alt</kbd> | | not accepted: no buffer change, no match (<kbd>Shift</kbd> and Caps Lock are allowed: uppercase characters match case-insensitively); the pre-acceptance step CMP-KEY-36 still runs, so a modified <kbd>Space</kbd> with a non-empty buffer is consumed and a stale buffer emits `onTyping(false)` for these keys too |

- **CMP-KEY-35** — MUST. A `keydown` is accepted by typeahead only when the labels array exists, the key name has length 1, and none of <kbd>Control</kbd>, <kbd>Meta</kbd>, <kbd>Alt</kbd> is held.
  Source: `floating-ui-react/hooks/useTypeahead.ts:144-154`. Test: [L3] `useTypeahead.test.tsx` "capslock characters continue to match".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "capslock characters continue to match".
- **CMP-KEY-36** — MUST. Before the acceptance check, if the buffer is non-empty and the key is <kbd>Space</kbd>, the event is consumed and `onTyping(true)` emitted; and if the buffer is non-empty, does not start with a space, no longer matches any available label, and the key is not <kbd>Space</kbd>, `onTyping(false)` is emitted.
  Source: `floating-ui-react/hooks/useTypeahead.ts:132-142`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); proven at L3 by `js/composite-typeahead.test.ts` "port: Space while the buffer holds text is consumed and keeps the typing flag" (the first clause) and "port: a key that is not Space ends the typing flag once the buffer matches no label, and keeps it while it matches" (the second, read through the typing flag, since `onTyping` is not reported).
- **CMP-KEY-37** — MUST. When the popup is open and the accepted key is not <kbd>Space</kbd>, the event is consumed and `onTyping(true)` emitted.
  Source: `floating-ui-react/hooks/useTypeahead.ts:156-159`. Test: [L3] `useTypeahead.test.tsx` "onTyping is called with typing activity" (L4 for the consumption clause).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3, restated as under CMP-STA-43 ("port: onTyping is called with typing activity (a typed character sets the typing flag, the reset clears it)"). The `button` module's Space path reads the flag through `defaultPrevented` (design point 2 (c)): the floating handle prevents an item's Space while typing in a capture listener, before the item's own `button` listener, and `button.ts` is unchanged; `js/button.test.ts` "port: an item's Space while typing is typed, not activated, because the list handle prevents it before the item's own listener" pins the order. The consumption clause is green at L4: `tests/browser/composite-list-rows.spec.ts` "port: a typeahead character on the open list is consumed", both modes.
- **CMP-KEY-38** — MUST. A *session* starts when the buffer is empty at acceptance; its base index is `selectedIndex ?? activeIndex ?? -1`. A continuing session's base index is the previous index (the last match, or the value set by the timer/blur).
  Source: `floating-ui-react/hooks/useTypeahead.ts:162-165,192-193`. Test: [L3] `useTypeahead.test.tsx` "starts from the current activeIndex and correctly loops (strict: %s)".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "starts from the current activeIndex and correctly loops (strict: %s)", the `it.each` pair.
- **CMP-KEY-39** — MUST. *Rapid same-letter cycling.* If every available label has different first and second characters (case-insensitive) and the buffer equals the pressed key, the buffer is emptied and the base index becomes the match index before the key is appended, so repeated presses of one letter cycle through the items starting with it. If any available label starts with a doubled letter (e.g. "aaron"), repeated letters accumulate instead ("aa").
  Source: `floating-ui-react/hooks/useTypeahead.ts:167-180`. Test: [L3] `useTypeahead.test.tsx` "rapidly focuses list items when they start with the same letter", "bails out of rapid focus of first letter if the list contains a string that starts with two of the same letter", "does not let hidden double-letter items block rapid cycling with elementsRef".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "rapidly focuses list items when they start with the same letter", "bails out of rapid focus of first letter if the list contains a string that starts with two of the same letter" and "does not let hidden double-letter items block rapid cycling with elementsRef".
- **CMP-KEY-40** — MUST. After appending the key and restarting the reset timer (CMP-STA-43), the match is the first available index, scanning from base index + 1 and wrapping around the whole list, whose label starts with the buffer (both lower-cased with locale-insensitive lower-casing).
  Source: `floating-ui-react/hooks/useTypeahead.ts:111-128,182-195`. Test: [L3] `useTypeahead.test.tsx` "passes the matching keydown event to onMatch", "does not depend on locale-sensitive lowercasing".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "passes the matching keydown event to onMatch" and "does not depend on locale-sensitive lowercasing".
- **CMP-KEY-41** — MUST. On a match, `onMatch(index, event)` is emitted and the match index recorded. On no match, unless the key is <kbd>Space</kbd>, the buffer is emptied and `onTyping(false)` emitted (the timer keeps running).
  Source: `floating-ui-react/hooks/useTypeahead.ts:197-203`. Test: [L3] `useTypeahead.test.tsx` "starts from the current activeIndex and correctly loops (strict: %s)" (no match while the buffer is "toytoy" → no call).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "starts from the current activeIndex and correctly loops (strict: %s)".
- **CMP-KEY-42** — MUST. An item is unavailable for matching when its element (from `elementsRef`, when provided) is not visible (CMP-ALG-03) or matches `:disabled`, or when `disabledIndices` (array or predicate) marks the index. Without `elementsRef` and without `disabledIndices`, every item with a label is available.
  Source: `floating-ui-react/hooks/useTypeahead.ts:98-109`. Test: [L3] `useTypeahead.test.tsx` "skips hidden items when matching with elementsRef", "skips visibility:hidden items when matching with elementsRef". Docs: `CHANGELOG.md v1.6.0 "Skip disabled items in typeahead and fix multiple-mode serialization (#5025)"`.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "skips hidden items when matching with elementsRef" and "skips visibility:hidden items when matching with elementsRef". The skip is `CompositeTypeaheadOptions.Skip`: `element` (Menu's `elementsRef`: visibility and `:disabled`), `attribute` (Select's `isElementDisabled`), or none.
- **CMP-KEY-43** — MUST. The match callback receives the originating `keydown` event as its second argument.
  Source: `floating-ui-react/hooks/useTypeahead.ts:198`. Test: [L3] `useTypeahead.test.tsx` "passes the matching keydown event to onMatch".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "passes the matching keydown event to onMatch" (the highlight report carries the keydown's snapshot).

### 4.10 Direction and locale (I18N)

Explanatory: `DirectionProvider` (owned by [shared-utilities](shared-utilities.md)) supplies a `direction` of `'ltr'` or `'rtl'` to descendants; without a provider the direction is `'ltr'`. It does not set the `dir` attribute; the application does that itself (Docs: `utils/direction-provider/page.mdx` §Anatomy). The composite root reads this value directly. List navigation does not read it: each popup component converts the direction into the boolean `rtl` option.

- **CMP-I18N-01** — MUST. The composite root part resolves its direction from the nearest ancestor direction provider, defaulting to `'ltr'`; the bare root mechanism takes `direction` as a required parameter.
  Source: `internals/composite/root/CompositeRoot.tsx:43,62`, `internals/direction-context/DirectionContext.tsx:12-16`, `root/useCompositeRoot.ts:47`. Test: [L2] `CompositeRoot.test.tsx` rtl "horizontal orientation" (wrapped in `DirectionProvider direction="rtl"`).
  Port note (2026-10-22, M2 track 3, issue 79; UTL §4.15.1, ADR 16 rule 9): the root reads the internal context of the nearest `DirectionProvider`, cascaded with `IsFixed` and subscribed to, so a change of direction renders the root and sends one `update`; without a provider it reads `ltr`. The L2 title "horizontal orientation" asserts the direction the root sends; the arrow mapping is the module's (CMP-I18N-02).
- **CMP-I18N-02** — MUST. Under RTL, the composite's horizontal forward key is <kbd>ArrowLeft</kbd> and the horizontal backward key is <kbd>ArrowRight</kbd> (CMP-KEY-03), for both `'horizontal'` and `'both'`; looping is unaffected (forward from max wraps to min).
  Source: `internals/composite/root/useCompositeRoot.ts:221-224`. Test: [L3] `CompositeRoot.test.tsx` rtl "horizontal orientation", rtl "both horizontal and vertical orientation".
  Port note (2026-10-22, M2 track 3, issue 79; js/CONTEXT.md): the rtl titles are browser-only in Base UI (`describe.skipIf(isJSDOM)`), so in `js/composite.test.ts` they keep the skip and run nowhere at L3; they run at L4 on the `composite-rtl` fixture, both modes, inside `<div dir="rtl">` and `DirectionProvider Direction="TextDirection.Rtl"`.
- **CMP-I18N-03** — MUST. Direction does not affect <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>, <kbd>Home</kbd>/<kbd>End</kbd>, or the vertical axis of grid navigation.
  Source: `internals/composite/root/useCompositeRoot.ts:225-226,274-280`, `floating-ui-react/utils/composite.ts:142-147`. Test: [L3] `CompositeRoot.test.tsx` grid rtl "both horizontal and vertical orientation".
  Port note (2026-10-22, M2 track 3, issue 79; CMP §10 Q1): the cited title is grid rtl (no consumer); the list clause runs as the L3 port title "leaves ArrowUp, ArrowDown, Home and End unchanged under rtl (CMP-I18N-03)", in jsdom, because the module reads the direction from its options and never from the document (UTL-I18N-03).
- **CMP-I18N-04** — MUST. In list navigation, `rtl: true` swaps <kbd>ArrowLeft</kbd> and <kbd>ArrowRight</kbd> in the TOEND, XOPEN and XCLOSE predicates (§4.6 table) and in grid navigation; MAIN membership is unchanged.
  Source: `floating-ui-react/hooks/useListNavigation.ts:65-99`, `floating-ui-react/utils/composite.ts:338,373,416`. Test: [L3] `useListNavigation.test.tsx` rtl "navigates down on ArrowLeft", rtl "navigates up on ArrowRight", "with rtl true › focuses next item using ArrowLeft key, skipping disabled items".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` rtl "navigates down on ArrowLeft", rtl "navigates up on ArrowRight" and "with rtl true › focuses next item using ArrowLeft key, skipping disabled items"; the family sends `CompositeListOptions.Rtl` from its direction, as Base UI's components convert it.
- **CMP-I18N-05** — MUST. In composite grid navigation under RTL, the horizontal forward key (<kbd>ArrowLeft</kbd>) moves to the next cell in the row, and when the highlighted item spans several columns the move starts from the item's top-*end* corner (`tr` cell) so that a spanning item does not resolve back to itself; <kbd>ArrowRight</kbd> starts from the top-left corner.
  Source: `internals/composite/root/gridNavigation.ts:106-119`. Test: [L3] `CompositeRoot.test.tsx` grid rtl "uses the forward edge when navigating from a spanning item", grid rtl "horizontal orientation".
- **CMP-I18N-06** — MUST. When scrolling a composite item into view horizontally (CMP-ALG-19), RTL checks "overflow to the left" (start edge) before "overflow to the right", while LTR checks right before left; both use `scroll-margin-*` of the item and `scroll-padding-*` of the container.
  Source: `internals/composite/composite.ts:65-111`. Test: [L3] `composite.test.ts` (internals/composite) "uses the left scroll margin when checking left overflow in RTL".
- **CMP-I18N-07** — MUST. Typeahead compares labels using locale-insensitive lower-casing (the result for `"I"` is `"i"` regardless of the user's locale).
  Source: `floating-ui-react/hooks/useTypeahead.ts:117,122`. Test: [L3] `useTypeahead.test.tsx` "does not depend on locale-sensitive lowercasing".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "does not depend on locale-sensitive lowercasing".

### 4.11 Dismissal (DIS)

- **CMP-DIS-01** — MUST. A nested list closes itself (reason `list-navigation`) on its cross-orientation close key, returning focus to the `nestedReturnFocusRef` element or the reference (CMP-KEY-27). The close is not cancelable through the event details.
  Source: `floating-ui-react/hooks/useListNavigation.ts:570-585`. Test: [L3] `useListNavigation.test.tsx` "keyboard navigation in nested menus lists".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): re-tagged [L4] → [L3] (ADR 13 §3): the cited title is one of Base UI's `it.skipIf(!isJSDOM)` titles (`useListNavigation.test.tsx:1426`) and runs verbatim in `js/composite-list.test.ts` "keyboard navigation in nested menus lists" (Escape closes the `'both'` submenu and focuses "Image"). The close goes to C# as an open request with `list-navigation` and no trigger (CMP-EVT-07), and the focus returns in the keydown's task. The cited title also runs at L4 on `composite-list-nested`, with the focus return across the portal: `tests/browser/composite-list-rows.spec.ts` "keyboard navigation in nested menus lists" and "port: a nested list's close key returns the focus across the portal to its submenu trigger, consumed when it is not the parent's main key", both modes.
- **CMP-DIS-02** — MUST. A non-nested popup closes on <kbd>Shift</kbd>+<kbd>Tab</kbd> from inside its popup focus element (reason `focus-out`, cancelable) with focus returned to the reference (CMP-KEY-24). <kbd>Tab</kbd> without <kbd>Shift</kbd>, <kbd>Escape</kbd> and outside presses are dismissed by the mechanisms specified in [floating-and-positioning](floating-and-positioning.md) and [popup-store-and-handles](popup-store-and-handles.md); §4.14 restates the externally observable results the shared suite asserts.
  Source: `floating-ui-react/hooks/useListNavigation.ts:771-792`. Test: [L4] `test/popupListConformanceTests.tsx` "closes on Shift+Tab without committing, and moves focus to the element before the popup".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: the popup-list suite's "closes on Shift+Tab without committing, and moves focus to the element before the popup" (Menu, Select, Combobox; §4.14's port note), both modes. The close and its cancel are CMP-KEY-24's and CMP-EVT-08's (`leave`; on Server a canceled close still moves the focus). Server: see ADR 01 (the `leave` row of Amendments 2026-11-04: a canceled close still moves the focus).

### 4.12 Callbacks and reasons (EVT)

- **CMP-EVT-01** — MUST. Composite `onHighlightedIndexChange(index: number)` is invoked with the new index whenever the root moves the highlight (keyboard, item focus, registry adjustments CMP-STA-23 to CMP-STA-29). It MAY be invoked with an index equal to the current one (every item focus reports its index).
  Source: `internals/composite/root/useCompositeRoot.ts:101-108`, `item/useCompositeItem.ts:26-28`. Test: [L2] `CompositeRoot.test.tsx` "uses an active item explicit index as the initial highlighted index".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22): the highlight is JavaScript's; C# mirrors the module's report (`CompositeRootBehavior.HighlightedIndex`, `HighlightChanged`, once per change, sent only when the part asks, so RadioGroup pays no message). The cited title is restated at L2 as that mirror, and at L3 as the report of the marked item's index.
  Port note (2026-10-27, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26): TabsList's `onHighlightedIndexChange` (`setHighlightedTabIndex`, `TabsList.tsx:123`) has no C# counterpart. It fed TabsList's own consumer-owned highlight, which in the port is the module's follow (CMP-STA-22's port note). The mirror stays the only C# view of the highlight, sent only when a part asks.
- **CMP-EVT-02** — MUST. Composite `onLoop(event, prevIndex, nextIndex, elementsArray) => number` is invoked only when a step wraps (CMP-KEY-06, CMP-KEY-07, grid wraps in CMP-ALG-08); its return value is the destination index.
  Source: `internals/composite/root/useCompositeRoot.ts:37-44,195-202,285-291`. Test: [L3] `CompositeRoot.test.tsx` "calls onLoop and uses its return value", "calls onLoop when looping vertically between rows".
- **CMP-EVT-03** — MUST. Composite root `onMapChange(map)` is invoked with the item map on every publication (CMP-STA-09), before the root's own index adjustment runs.
  Source: `internals/composite/root/CompositeRoot.tsx:88-91`. Test: [L3] `CompositeRoot.test.tsx` "keeps the outer registration when only the inner item updates" (L2 for the cited title, restated as the C# rule of CMP-STR-04 and the join of the report).
  Port note (2026-10-27, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26; ADR 13 §3; design points 1 and 3, option (a), settled by the orchestrator 2026-10-26): re-tagged [L2] → [L3]. `onMapChange` is a report of ids. With `CompositeOptions.ReportItems`, the module reports `OnItemsChange` with the items' ids in index order on each publication whose id sequence changed: the first in `attach`, one in `update`, or an observed move. Its listener subscribes before the root's adjustment, so the report precedes the same publication's highlight report. L3 port titles: "reports the ids in index order on the first publication, an addition and a removal, only with reportItems", "reports an observed move, and nothing for a publication that keeps the id sequence" and "reports the ids before the root's own adjustment of the same publication". `CompositeRootBehavior` joins the ids to its registrations (`OrderedItems`, one `ItemsChanged` per report), and values stay in C#; one report is about 30 bytes per item (about 300 bytes for ten Tabs), once per publication that changed the order. The cited title is restated at L2: what Base UI's map publishes is what the root sends and joins, the outer item's registration (`CompositeOuterRegistrationTests`, `CompositeItemsReportTests`). The public-part proofs are TabsList's (track 5), and ToolbarRoot's if track 4 needs the report.
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList` (E2, `ReportItems`): the report drives the Root's automatic selection, `TabsAutomaticSelectionTests` at L2 (the report driven by hand) and `tests/browser/tabs.spec.ts` "calls onValueChange when the selected tab is removed" and "calls onValueChange when the selected tab becomes disabled", both modes.
- **CMP-EVT-04** — MUST. List navigation `onNavigate(index: number | null, event: Event | undefined)` is invoked with `null` when the cursor is `-1`. `event` is `undefined` for synchronizations not caused by a user event (open/close, CMP-STA-33/34/37), the `keydown` for keyboard moves, and the originating `focus`/`mousemove`/`pointerleave` event for pointer-driven changes.
  Source: `floating-ui-react/hooks/useListNavigation.ts:117-118,309-311,402,409,472,535,607,613,636,660,683,734,883,909,922`. Test: [L3] `useListNavigation.test.tsx` "true - onNavigate is called with `null` when escaped"; `menu/root/MenuRoot.tsx:536` (consumer maps the event through CMP-EVT-09).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "true - onNavigate is called with `null` when escaped". `onNavigate` is the report `CompositeListHighlight(Index, ItemId, Event, Revision)`: its event snapshot is the keydown's or the pointer event's ("port: writes the highlighted item's tabindex and data-highlighted in the task that moves the focus, and reports it once"), and `null` for a synchronization with no user event and for a C#-origin highlight ("port: a C# highlight request moves the highlight and the cursor, so the next key steps from it"); L2 `CompositeTwinContractTests.The_list_reports_raise_their_events_with_the_payloads`. Menu's mapping through CMP-EVT-09 (`MenuRoot.tsx:536`) is track 6's.
  Port note (2026-11-04, design point 9 (a) of M5 track 6, [issue 172](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/172); ADR 04 Amendments 2026-11-04, "the highlight report's label", settled by the orchestrator 2026-11-04): the module's report of `onNavigate` (`OnListHighlight`) also carries `label`, the highlighted item's label as the registry resolves it on every rebuild (its registered label, else its text, CMP-STA-06/07), sent only for a highlighted item that has one. Menu's `onItemHighlighted` reads it (MNU-EVT-10, `menu/root/MenuRoot.tsx:605-613`). Proven at L3 (`js/composite-list.test.ts` "port: the highlight report carries the highlighted item's label, its registered label else its text") and L2 ("port: the highlight report's label is read by its module name, and an absent one is null").
- **CMP-EVT-05** — MUST. A MAIN key on the trigger while the popup is open under DOM focus produces two `onNavigate` calls with the same index and event (one from the floating handling, one from the trigger handler).
  Source: `floating-ui-react/hooks/useListNavigation.ts:905,908-910`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, restated: both calls happen (Table C's, then the trigger handler's own), but the module reports the highlight once per change (`reportHighlight`; §8, one message per change), so C# receives one `OnListHighlight` for the key and the repeated call has no observer in the port. No title.
- **CMP-EVT-06** — MUST. Opening from a navigation key (Table A, Table D) calls the popup's open-change with `open = true` and event details `{ reason: 'list-navigation', event: <native keydown>, trigger: <the trigger element> }`.
  Source: `floating-ui-react/hooks/useListNavigation.ts:816-825`, `internals/reason-parts.ts:26`. Test: [L3] `useListNavigation.test.tsx` "opens on ArrowDown when true".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "opens on ArrowDown when true", and "port: requests the open with the reason list-navigation and the trigger, and the leave close with focus-out"; L2 `ListNavigationTests` "a list's open and close requests run the family's sequence with their reason and trigger, and a closed match raises Matched" and `CompositeTwinContractTests.The_list_reports_raise_their_events_with_the_payloads`. The request (`CompositeListOpenRequest`) reaches the family's store through `ListNavigationBehavior`'s request delegate.
- **CMP-EVT-07** — MUST. The nested close (CMP-DIS-01) calls open-change with `{ reason: 'list-navigation', event: <native keydown> }` and no trigger.
  Source: `floating-ui-react/hooks/useListNavigation.ts:577`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); proven at L3 by `js/composite-list.test.ts` "port: a nested list with keepOpenOnCrossOrientationKey stays open on its cross-orientation close key, which another nested list closes on" (the nested close requested with `list-navigation` and no trigger) and "port: a nested close key that is the bar's main key closes the menu and is relayed, moving the bar to the previous item", and at L2 by `ListNavigationTests` "a list's open and close requests run the family's sequence with their reason and trigger, and a closed match raises Matched".
- **CMP-EVT-08** — MUST. The <kbd>Shift</kbd>+<kbd>Tab</kbd> close (CMP-DIS-02) calls open-change with `{ reason: 'focus-out', event: <native keydown> }`; if the consumer cancels it, focus is not moved.
  Source: `floating-ui-react/hooks/useListNavigation.ts:783-789`, `internals/reason-parts.ts:23`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); port titles prove it. The close is the list handle's `leave` decision point (Amendments 2026-11-03 (3)): the module stops the Shift+Tab and asks; `Run` (not canceled) returns the focus and `Stop` (canceled) leaves it. L3: `js/composite-list.test.ts` "port: requests the open with the reason list-navigation and the trigger, and the leave close with focus-out" and "port: on WebAssembly a canceled Shift+Tab close leaves the focus where it was"; L2: `ListNavigationTests` "on WebAssembly leave runs the Shift+Tab close inside the decision and answers stop when it was canceled" and `CompositeTwinContractTests.Decide_on_leave_runs_the_leave_decider_and_answers_run_without_one`. On Server the fallback `run` requests the close and returns the focus, so a cancel there does not keep it: the focus reaches the trigger 2–3 ms after the key and the cancel reaches C# at 158 ms at 75 ms one-way, while on WebAssembly the cancel lands inside the keydown (18–19 ms) and the focus stays in the list (`tests/browser/composite-list-figures.spec.ts` "port: leave: a canceled Shift+Tab close keeps the focus on WebAssembly and still moves it on Server"; ADR 01 Amendments 2026-11-04, the "C#-origin consumer vetoes" row). Server: see ADR 01.
- **CMP-EVT-09** — MUST. `getHighlightReason(event)` maps an optional event to a highlight reason: `undefined`/`null` → `'none'`; an event whose `type` starts with `key` → `'keyboard'`; `type` starting with `mouse` or `pointer` → `'pointer'`; anything else (e.g. `focus`, `blur`, `input`) → `'none'`. Components use it to fill `onItemHighlighted` details.
  Source: `utils/getHighlightReason.ts:8-21`, `internals/reason-parts.ts:1,27,29`. Test: none. Docs: `CHANGELOG.md v1.9.0 "Pass the originating event to \`onItemHighlighted\`. Pointer events may be \`MouseEvent\` or \`PointerEvent\`; typing reports the reason \`none\` (#5838)"`.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title). The mapping is C#'s, as the track's brief allowed (fact sheet A §4.1 (c)): `HighlightReason.From(EventSnapshot?)` in `Events/` (EVD-ALG-11, since M1) returns `Keyboard` for `key*`, `Pointer` for `mouse*` and `pointer*`, and `None` otherwise or with no event. L1: `tests/Blazix.Primitives.Tests/Events/HighlightReasonTests.cs` "derives the highlight reason from the event type", "no event means no reason" and "reports a pointer highlight after keyboard navigation". The list's highlight report carries the snapshot it reads (CMP-EVT-04); the families fill `onItemHighlighted`'s details with it (tracks 6, 9, 10 and 11).
- **CMP-EVT-10** — MUST. Typeahead `onMatch(index, keydownEvent)` is invoked once per accepted character that produces a match (CMP-KEY-41).
  Source: `floating-ui-react/hooks/useTypeahead.ts:198`. Test: [L3] `useTypeahead.test.tsx` "passes the matching keydown event to onMatch".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "passes the matching keydown event to onMatch". A match on an open list is the highlight the module writes and reports with the keydown; a match on a closed list is the report `CompositeListMatch(Index, Event)` for the family's value request ("port: a match while the list is closed is reported for the family's value request, and moves no highlight"; L2 `ListNavigationTests` "a list's open and close requests run the family's sequence with their reason and trigger, and a closed match raises Matched"). The value request is the family's (Select, track 9). On Server the match reaches C# one leg after the key (160 ms at 75 ms one-way; 11–12 ms on WebAssembly; `tests/browser/composite-list-figures.spec.ts` "port: a closed list's typeahead match reaches C# one leg after the key"; ADR 01 Amendments 2026-11-04). Server: see ADR 01.
- **CMP-EVT-11** — MUST. Typeahead `onTyping(isTyping)` is invoked with `true` when a character is accepted while open (or a space continues a session) and with `false` when the buffer is reset (timer, blur, close, no match, stale buffer); consecutive calls MAY repeat the same value.
  Source: `floating-ui-react/hooks/useTypeahead.ts:135,141,158,186,202,222,235`. Test: [L3] `useTypeahead.test.tsx` "onTyping is called with typing activity", "onTyping is called when the popup closes without moving focus".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3, restated: both titles run through the typing flag, as under CMP-STA-43 and CMP-STA-44 (no `onTyping` report; Amendments 2026-11-03 (1), design point 2 (c)).
- **CMP-EVT-12** — MUST. Composite `onKeyDown` supplied by the consumer runs in addition to (merged with) the root's key handling through the shared prop-merging rules of [rendering-and-composition](rendering-and-composition.md). Server: see ADR 01 relaxation table.
  Source: `internals/composite/root/CompositeRoot.tsx:70,125`. Test: none.
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, decision point `navigate`, named by the user 2026-10-22): the root part declines the consumer's `onkeydown` and runs it from its own handler; on WebAssembly the `navigate` decision runs it inside the dispatch, where `PreventPartHandler()` stops the key handling. A key the module handles with `stopEventPropagation` never reaches Blazor's document-level dispatcher. Option (i) of ADR 04's note, chosen by the user 2026-10-23: the root and the items decline the consumer's `onkeydown`; `navigate` runs the focused item's, then the root's, on one shared event (either one's veto stops the key); on Server the module's handled-key report runs both a round trip late; an ancestor's never runs, as in Base UI (L2 `CompositeKeyHandlerTests`, L4 probe titles, §10 item 12).
  Port note (2026-10-23, M2 track 8, issue 84; ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `RadioGroup` by `tests/browser/radio-group.spec.ts` "port: a handled arrow runs the radio's and the group's C# onkeydown once each" (option (i): the radio's, then the group's), both modes; `RadioRootTests` "port: a consumer onkeydown runs once per key through one gate, and never while disabled" and `RadioGroupTests` "port: the group's consumer onkeydown is declined and run at the navigate decision" at L2.

### 4.13 Form, animation, positioning, other accessibility (FRM, ANI, POS, A11Y)

- FRM: Not applicable — these mechanisms carry no value and do not register with a field.
- ANI: Not applicable — the only transition-related behavior is CMP-KEY-26 (keys ignored while the popup is closed but its focus element still exists).
- POS: Not applicable — positioning is owned by [floating-and-positioning](floating-and-positioning.md).
- A11Y: Not applicable — screen-reader synthesized presses are covered by CMP-STA-39/CMP-ALG-28; no live regions or focus-visible heuristics exist here.

### 4.14 Popup list conformance suite

`packages/react/test/popupListConformanceTests.tsx` is run by Menu (`itemRole: 'menuitem'`, `listEnd: 'wrap'`, all other flags default), Select (`itemRole: 'option'`, `selectable: true`, `listEnd: 'stop'`) and Combobox (`itemRole: 'option'`, `focusModel: 'virtual'`, `homeEnd: false`, `selectable: true`, `spaceActivates: false`, `enterSpaceOpen: false`, `typeahead: false`, `modal: false`, `triggerClickCloses: false`, `listEnd: 'escape'`). The fixture renders a "Before" button, the component (trigger `data-testid="trigger"`, popup `data-testid="popup"`, scroller `data-testid="scroller"` 100 px tall with `overflow-y: auto`, twelve 30 px items "Item 0"…"Item 11"), and an "After" button. "Highlighted" means the item carries `data-highlighted`; at most one item may be highlighted (the helper throws otherwise). "Focus on item *n*" means: DOM-focus suites — item *n* has DOM focus; virtual suite — the trigger has focus and `aria-activedescendant` equals item *n*'s id. Each requirement below applies to every component that runs the suite with the stated flags. Source for all: `packages/react/test/popupListConformanceTests.tsx` (line ranges per item); consumers: `menu/root/MenuRoot.test.tsx:267-293`, `select/root/SelectRoot.test.tsx:389-419`, `combobox/root/ComboboxRoot.test.tsx:191-227`.

Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): the suite runs as `tests/browser/suites/popupListConformanceTests.ts` (titles verbatim, Base UI's options) from `tests/browser/composite-list.spec.ts`, in Base UI's three configurations, each with its family's list options as the configuration's query: Menu (44 titles) and Select (47) on the test-only DOM-focus host `composite-list-dom` (a button reference), Combobox (38) on the virtual-focus host `composite-list-virtual` (an input reference). It runs on Server, then on WebAssembly; every title passes on both modes, and none is listed open. The hosts (`FixtureListRoot`, `FixtureListTrigger`, `FixtureListPopup`, `FixtureListBox` and `FixtureListItem` on the M4 popup host; `tests/Blazix.Primitives.Fixtures.Client/CONTEXT.md`) compose the list behaviors a family composes (`ListNavigationBehavior`, `CompositeRootBehavior` in list mode, `CompositeItemBehavior`), with test-only stand-ins for the family's own parts: a selectable value with `aria-selected`, and the item's activation through the `button` module. Each row below names the titles that prove it and the configurations that run them. The families re-run the suite on their public parts and add their "also proven through" notes (ADR 13 Amendments 2026-10-22).

Pointer:

- **CMP-PTR-09** — MUST. After a pointer open, hovering item 2 highlights it and puts focus on it.
  Source: `test/popupListConformanceTests.tsx:232-242`. Test: [L4] "highlights and focuses the hovered item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "highlights and focuses the hovered item" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-PTR-10** — MUST. Hovering item 1 then moving the pointer to the "After" button outside the list clears the highlight (no `data-highlighted`, no `aria-activedescendant` in the virtual suite); focus then rests on the popup (DOM suites) or stays on the trigger (virtual); a subsequent <kbd>Enter</kbd> activates nothing and changes no value.
  Source: `test/popupListConformanceTests.tsx:244-264`. Test: [L4] "clears the highlight when the pointer leaves the list without leaving focus on an item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "clears the highlight when the pointer leaves the list without leaving focus on an item" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-PTR-11** — MUST. After keyboard navigation to item 3, hovering item 1 moves the highlight and focus to item 1.
  Source: `test/popupListConformanceTests.tsx:299-310`. Test: [L4] "lets the pointer take the highlight back after keyboard navigation".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "lets the pointer take the highlight back after keyboard navigation" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-PTR-12** — MUST. A `pointerdown` → `pointercancel` → `pointerleave` sequence with `pointerType: 'touch'` on the highlighted item (a touch scroll starting) leaves the highlight on that item.
  Source: `test/popupListConformanceTests.tsx:312-331`. Test: [L4] "keeps the highlight when a touch scroll starts on the highlighted item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "keeps the highlight when a touch scroll starts on the highlighted item" (Menu, Select, Combobox; the touch sequence dispatched in the page as Base UI's `fireEvent` dispatches it), both modes (§4.14's port note).
- **CMP-PTR-13** — MUST. An item rendered disabled has `aria-disabled="true"`; hovering it and pressing <kbd>Enter</kbd>, or clicking it, invokes neither the item's click handler nor the value change, leaves the popup open and does not mark it selected; clicking an enabled item afterwards invokes the click handler once (and the value change, in selectable suites).
  Source: `test/popupListConformanceTests.tsx:333-366`. Test: [L4] "does not activate a disabled item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "does not activate a disabled item" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-PTR-14** — MUST. A right-button press on the trigger does not open the popup; a right-button press on an item of an open popup neither closes it nor activates anything.
  Source: `test/popupListConformanceTests.tsx:368-382`. Test: [L4] "ignores right clicks on the trigger and on items".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "ignores right clicks on the trigger and on items" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-PTR-15** — MUST. (Real browser) With the pointer resting over item 1 after hovering it, keyboard navigation from item 1 to item 9 — which scrolls the list under the stationary pointer — ends with item 9 highlighted; boundary events or zero-movement `mousemove` events caused by the scroll do not steal the highlight.
  Source: `test/popupListConformanceTests.tsx:384-408`. Test: [L4] "keeps the keyboard highlight while the list scrolls under a resting pointer".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "keeps the keyboard highlight while the list scrolls under a resting pointer" (Menu, Select, Combobox; real pointer input over the 100 px scroller), both modes (§4.14's port note).
- **CMP-PTR-16** — MUST. (Real browser) Hovering a partially visible item at the bottom edge (item 3, 10 px visible) or at the top edge (item 1 after scrolling to 45 px) highlights it without changing the scroller's scroll position.
  Source: `test/popupListConformanceTests.tsx:410-438`. Test: [L4] "does not scroll the list when hovering a partially visible item at either edge".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "does not scroll the list when hovering a partially visible item at either edge" (Menu, Select, Combobox), both modes (§4.14's port note).

Opening:

- **CMP-FOC-10** — MUST. After a pointer open nothing is highlighted and focus is on the popup (DOM suites) or on the trigger (virtual); the first <kbd>ArrowDown</kbd> then highlights item 0 and puts focus on it.
  Source: `test/popupListConformanceTests.tsx:442-454`. Test: [L4] "starts without a highlight after a pointer opening".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "starts without a highlight after a pointer opening" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-44** — MUST. With the trigger focused and the popup closed, <kbd>ArrowDown</kbd> opens the popup with item 0 highlighted and focused; <kbd>ArrowUp</kbd> opens it with the last item (11) highlighted and focused, and (real browser) scrolled fully into view.
  Source: `test/popupListConformanceTests.tsx:456-492`. Test: [L4] "opens on the first item with ArrowDown", "opens on the last item with ArrowUp", "scrolls the last item into view when opening with ArrowUp".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "opens on the first item with ArrowDown", "opens on the last item with ArrowUp" and "scrolls the last item into view when opening with ArrowUp" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-45** — MUST. When item 0 is disabled, opening with <kbd>ArrowDown</kbd> highlights item 1.
  Source: `test/popupListConformanceTests.tsx:494-500`. Test: [L4] "opens past a disabled first item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "opens past a disabled first item" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-46** — MUST. (`enterSpaceOpen`, i.e. Menu and Select) <kbd>Enter</kbd> or <kbd>Space</kbd> on the focused trigger opens the popup with item 0 highlighted and focused, without activating any item or changing the value.
  Source: `test/popupListConformanceTests.tsx:502-519`. Test: [L4] "opens with %s without activating an item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "opens with %s without activating an item" for Enter and Space (Menu and Select, `enterSpaceOpen`), both modes (§4.14's port note).
- **CMP-STA-46** — MUST. After navigating to item 7 and closing with <kbd>Escape</kbd>, a pointer reopen starts with no highlight.
  Source: `test/popupListConformanceTests.tsx:521-531`. Test: [L4] "starts fresh when reopened after Escape".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "starts fresh when reopened after Escape" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-STA-47** — MUST. (`selectable`) With "Item 8" as the default value, opening with <kbd>ArrowDown</kbd> or <kbd>ArrowUp</kbd> highlights item 8; only item 8 has `aria-selected="true"` (others `"false"`); (real browser) a pointer open reveals item 8 fully within the scroller and highlights it.
  Source: `test/popupListConformanceTests.tsx:533-565`. Test: [L4] "opens on the selected item with %s", "marks only the selected item as selected", "reveals and highlights the selected item when opening".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "opens on the selected item with %s" for ArrowDown and ArrowUp, "marks only the selected item as selected" and "reveals and highlights the selected item when opening" (Select and Combobox, `selectable`), both modes (§4.14's port note).

Keyboard:

- **CMP-KEY-47** — MUST. After a keyboard open at index *s*, <kbd>ArrowDown</kbd> highlights *s*+1 and moves focus to it; <kbd>ArrowUp</kbd> returns to *s*.
  Source: `test/popupListConformanceTests.tsx:569-594`. Test: [L4] "moves the highlight one item at a time with the arrow keys", "moves focus along with the highlight".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "moves the highlight one item at a time with the arrow keys" and "moves focus along with the highlight" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-48** — MUST. After a pointer open and hovering item 4, <kbd>ArrowDown</kbd> highlights item 5 with focus on it; and <kbd>Enter</kbd> on a hovered item (6) activates exactly that item (click handler once; value change in selectable suites) and closes the popup.
  Source: `test/popupListConformanceTests.tsx:266-297`. Test: [L4] "continues keyboard navigation from the hovered item", "activates the hovered item with Enter".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "continues keyboard navigation from the hovered item" and "activates the hovered item with Enter" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-49** — MUST. <kbd>Enter</kbd> (and <kbd>Space</kbd> unless `spaceActivates: false`) on the highlighted item *s*+1 activates it exactly once, closes the popup, and returns focus to the trigger.
  Source: `test/popupListConformanceTests.tsx:596-617`. Test: [L4] "activates the highlighted item once with %s".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "activates the highlighted item once with %s" for Enter and Space (Menu and Select) and for Enter (Combobox, `spaceActivates: false`), both modes (§4.14's port note).
- **CMP-KEY-50** — MUST. Inside a `<form>` with a submit button, opening/closing with <kbd>Escape</kbd>, opening with <kbd>Enter</kbd> (where supported), and activating an item with <kbd>Enter</kbd> never submit the form.
  Source: `test/popupListConformanceTests.tsx:619-652`. Test: [L4] "does not submit a surrounding form".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "does not submit a surrounding form" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-51** — MUST. Arrow navigation does not skip a disabled item: with item 2 disabled, <kbd>ArrowDown</kbd> from item 1 highlights item 2 and <kbd>ArrowUp</kbd> returns to item 1 (disabled items are focusable-when-disabled; the components pass an empty `disabledIndices`).
  Source: `test/popupListConformanceTests.tsx:654-669`. Test: [L4] "stops on a disabled item while navigating".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "stops on a disabled item while navigating" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-52** — MUST. (`homeEnd`, i.e. Menu and Select) <kbd>End</kbd> highlights the last item and <kbd>Home</kbd> the first, even when those items are disabled; (real browser) each is scrolled fully into view.
  Source: `test/popupListConformanceTests.tsx:671-721`. Test: [L4] "moves the highlight to the last and first items with End and Home", "scrolls the last and first items into view with End and Home", "moves to disabled ends with End and Home".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "moves the highlight to the last and first items with End and Home", "scrolls the last and first items into view with End and Home" and "moves to disabled ends with End and Home" (Menu and Select, `homeEnd`), both modes (§4.14's port note).
- **CMP-FOC-11** — MUST. (Real browser) Keyboard navigation scrolls the highlighted item fully into view in both directions and only as far as needed: moving to an already visible item does not scroll, moving one item past the bottom edge scrolls by at most one item height (+1 px), and moving far up scrolls the target into view.
  Source: `test/popupListConformanceTests.tsx:723-753`. Test: [L4] "scrolls the highlighted item into view in both directions, only as far as needed".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "scrolls the highlighted item into view in both directions, only as far as needed" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-53** — MUST. (`typeahead`, i.e. Menu and Select) Typing "Item 9" after a keyboard open highlights item 9 and (real browser) reveals it; with items "Alpha", "Bravo", "Charlie", typing `b` highlights "Bravo", and typing `c` after a 1000 ms pause highlights "Charlie" (a new search, not "bc").
  Source: `test/popupListConformanceTests.tsx:755-791`. Test: [L4] "reveals an item reached by typing its label", "starts a new typeahead search after a pause".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "reveals an item reached by typing its label" and "starts a new typeahead search after a pause" (Menu and Select, `typeahead`, with Menu's 500 ms and Select's 750 ms reset as their options), both modes (§4.14's port note).
- **CMP-KEY-54** — MUST. With an item focused after a keyboard open, `keydown` of <kbd>ArrowDown</kbd>, <kbd>ArrowUp</kbd> and (where `homeEnd`) <kbd>End</kbd>, <kbd>Home</kbd> has its default prevented.
  Source: `test/popupListConformanceTests.tsx:794-805`. Test: [L4] "prevents the default scrolling of navigation keys".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "prevents the default scrolling of navigation keys" (Menu and Select with the arrows, End and Home; Combobox with the arrows), both modes (§4.14's port note).

List boundaries (`listEnd`: Menu `'wrap'`, Select `'stop'`, Combobox `'escape'`):

- **CMP-KEY-55** — MUST. From item 0 (keyboard open), <kbd>ArrowUp</kbd> highlights the last item (`wrap`), stays on item 0 (`stop`) or clears the highlight (`escape`); from the last item (opened with <kbd>ArrowUp</kbd>), <kbd>ArrowDown</kbd> highlights item 0 (`wrap`), stays (`stop`) or clears (`escape`). (Real browser, `wrap` only) wrapping to item 0 scrolls it fully into view.
  Source: `test/popupListConformanceTests.tsx:808-869`. Test: [L4] "wraps/stops/leaves the list past the first item", "… past the last item" (template `${pastEndVerb}`: one of 3 verbs per suite configuration, chosen by `listEnd`), "scrolls the first item into view when wrapping past the last item".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "wraps past the first item", "wraps past the last item" and "scrolls the first item into view when wrapping past the last item" (Menu, `listEnd: 'wrap'`), "stops past the first item" and "stops past the last item" (Select), "leaves the list past the first item" and "leaves the list past the last item" (Combobox), both modes (§4.14's port note).
- **CMP-KEY-56** — MUST. With a single item, <kbd>ArrowDown</kbd> after a keyboard open keeps it highlighted (`wrap`, `stop`) or clears then re-enters on the next <kbd>ArrowDown</kbd> (`escape`); <kbd>Enter</kbd> then activates it.
  Source: `test/popupListConformanceTests.tsx:871-890`. Test: [L4] "keeps a single item reachable".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "keeps a single item reachable" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-57** — MUST. With no items, <kbd>ArrowDown</kbd> on the trigger opens the popup, focus rests on the popup (DOM) or trigger (virtual), and <kbd>ArrowDown</kbd>, <kbd>ArrowUp</kbd>, <kbd>Enter</kbd> keep focus there.
  Source: `test/popupListConformanceTests.tsx:892-910`. Test: [L4] "opens and handles keys without losing focus when the list is empty".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "opens and handles keys without losing focus when the list is empty" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-KEY-58** — MUST. When every item is disabled, opening with <kbd>ArrowDown</kbd> then <kbd>ArrowDown</kbd>, <kbd>ArrowUp</kbd>, <kbd>Enter</kbd> activates nothing, and focus remains on the trigger, an item or the popup.
  Source: `test/popupListConformanceTests.tsx:912-924`. Test: [L4] "does not activate anything when every item is disabled".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "does not activate anything when every item is disabled" (Menu, Select, Combobox), both modes (§4.14's port note).

Dismissal (observable results; the mechanisms are owned by FLT/PSH):

- **CMP-DIS-03** — MUST. <kbd>Escape</kbd> after navigating closes without activating or changing the value, focuses the trigger, and leaves no `aria-activedescendant` on it.
  Source: `test/popupListConformanceTests.tsx:928-942`. Test: [L4] "closes on Escape without committing, and keeps focus on the trigger".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "closes on Escape without committing, and keeps focus on the trigger" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-DIS-04** — MUST. <kbd>Tab</kbd> closes without committing and focuses the "After" button; <kbd>Shift</kbd>+<kbd>Tab</kbd> closes without committing and focuses the trigger (DOM suites) or the "Before" button (virtual suite, where focus never left the trigger).
  Source: `test/popupListConformanceTests.tsx:944-973`. Test: [L4] "closes on Tab without committing, and moves focus past the trigger", "closes on Shift+Tab without committing, and moves focus to the element before the popup".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "closes on Tab without committing, and moves focus past the trigger" and "closes on Shift+Tab without committing, and moves focus to the element before the popup" (Menu, Select, Combobox), both modes (§4.14's port note).
- **CMP-DIS-05** — MUST. A press on the document body while an item is hovered closes without committing; (real browser) a real click on the "After" button closes the popup and reaches the button's click handler only for non-modal suites (`modal: false`, Combobox).
  Source: `test/popupListConformanceTests.tsx:975-1009`. Test: [L4] "closes on an outside press without committing", "closes on a real outside click …" (template: 2 variants, "that the page underneath does not receive" for `modal: true`, "that reaches the page underneath" for `modal: false`).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "closes on an outside press without committing" (Menu, Select, Combobox), "closes on a real outside click that the page underneath does not receive" (Menu and Select, `modal`) and "closes on a real outside click that reaches the page underneath" (Combobox, `modal: false`), both modes (§4.14's port note).
- **CMP-DIS-06** — MUST. (`triggerClickCloses`, i.e. Menu and Select) Clicking the trigger while open closes the popup.
  Source: `test/popupListConformanceTests.tsx:1011-1022`. Test: [L4] "closes when the trigger is clicked again".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "closes when the trigger is clicked again" (Menu and Select, `triggerClickCloses`), both modes (§4.14's port note).
- **CMP-DIS-07** — MUST. Inside an open dialog, <kbd>Escape</kbd> after a keyboard open closes only the popup (the dialog stays visible); a second <kbd>Escape</kbd> closes the dialog.
  Source: `test/popupListConformanceTests.tsx:1024-1045`. Test: [L4] "closes only itself on Escape inside a dialog".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L4: "closes only itself on Escape inside a dialog" (Menu, Select, Combobox; the list inside an open `DialogRoot`'s popup), both modes (§4.14's port note). The hosts give the list's session no parent token, so the outcome does not depend on the list's session being the Dialog's child, and the row does not wait for track 2's cascade.

## 5. Algorithms

All pseudo-code is language-neutral. `list` is the elements array (positions may be `null`/empty). "mod" is the JavaScript remainder operator (`%`, sign of the dividend; `-1 mod 3 = -1`). "consume event" = prevent default and stop propagation (`stopEvent`). `floor` is integer floor toward −∞ for the non-negative values used here.

### 5.1 Index helpers (`floating-ui-react/utils/composite.ts`)

- **CMP-ALG-01** — MUST. The implementation reproduces the following procedure exactly:

  ```
  isIndexOutOfListBounds(list, index):
    return index < 0 or index >= length(list)
  ```
  Source: `floating-ui-react/utils/composite.ts:15-17`. Test: [L3] `floating-ui-react/utils/composite.test.ts` "leaves the list at either end when escaping is allowed".

- **CMP-ALG-02** — MUST. The implementation reproduces the following procedure exactly (`disabledIndices` is absent, an array, or a predicate):

  ```
  isListIndexDisabled(list, index, disabledIndices):
    explicit := disabledIndices is a function ? disabledIndices(index)
              : (disabledIndices present and disabledIndices contains index)
    if explicit: return true
    element := list[index]
    if element absent: return false
    if not isElementVisible(element): return true
    if element matches ':disabled': return true            // natively disabled: always skipped
    return disabledIndices absent
           and (element has attribute "disabled" or attribute "aria-disabled" = "true")
  ```
  Source: `floating-ui-react/utils/composite.ts:520-554`. Test: [L3] `floating-ui-react/utils/composite.test.ts` "always treats natively disabled elements as disabled, unlike aria-disabled ones"; `useListNavigation.test.tsx` "skips disabled item on initial navigation". Docs: `CHANGELOG.md v1.7.0 "Always skip natively disabled items during list navigation (#5185)"`, `CHANGELOG.md v1.3.0 "Skip CSS-hidden items during keyboard navigation in composite widgets (#4195)"`.
  Port note (2026-10-22, M2 track 3, issue 79; ADR 12 Amendments 2026-10-22 item 3): the cited `floating-ui-react/utils/composite.test.ts` title runs at L3; "skips disabled item on initial navigation" (`useListNavigation.test.tsx`) is M5.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): the M5 title runs at L3: `js/composite-list.test.ts` "skips disabled item on initial navigation".

- **CMP-ALG-03** — MUST. The implementation reproduces the following procedure exactly:

  ```
  isHiddenByStyles(styles):
    return styles.visibility = "hidden" or styles.visibility = "collapse"

  isElementVisible(element, styles := computed style of element when element present else null):
    if element absent or element not connected or styles null or isHiddenByStyles(styles): return false
    if element supports checkVisibility(): return element.checkVisibility()   // default options
    return styles.display ≠ "none" and styles.display ≠ "contents"
  ```
  Source: `floating-ui-react/utils/composite.ts:556-573`. Test: [L3] `floating-ui-react/utils/composite.test.ts` "treats hidden visibility styles as hidden", "uses CSS visibility fallbacks when checkVisibility is unavailable" (`content-visibility: hidden` counts as visible in the fallback).

- **CMP-ALG-04** — MUST. The implementation reproduces the following procedure exactly:

  ```
  findNonDisabledListIndex(list, { startingIndex := -1, decrement := false, disabledIndices, amount := 1 }):
    index := startingIndex
    repeat:
      index := index + (decrement ? -amount : amount)
    while index ≥ 0 and index ≤ length(list) - 1 and isListIndexDisabled(list, index, disabledIndices)
    return index          // may be < 0 or ≥ length(list)
  ```
  Source: `floating-ui-react/utils/composite.ts:85-109`. Test: [L3] `floating-ui-react/utils/composite.test.ts` "skips disabled indices".

- **CMP-ALG-05** — MUST. The implementation reproduces the following procedure exactly (note the results are out of bounds — `length(list)` and `-1` respectively — when every item is disabled, and `0`/`-1` for an empty list):

  ```
  getMinListIndex(list, disabledIndices):
    return findNonDisabledListIndex(list, { disabledIndices })
  getMaxListIndex(list, disabledIndices):
    return findNonDisabledListIndex(list, { decrement: true, startingIndex: length(list), disabledIndices })
  ```
  Source: `floating-ui-react/utils/composite.ts:19-35`. Test: [L3] `floating-ui-react/utils/composite.test.ts` "steps to the adjacent index" (helper usage).

- **CMP-ALG-06** — MUST. The implementation reproduces the following procedure exactly:

  ```
  getNextListIndex(list, currentIndex, { decrement, loopFocus, allowEscape, disabledIndices, minIndex, maxIndex }):
    step() := findNonDisabledListIndex(list, { startingIndex: currentIndex, decrement, disabledIndices })
    wrapped := false
    if not loopFocus:
      index := decrement ? max(minIndex, step()) : min(maxIndex, step())
    else if (decrement ? currentIndex ≤ minIndex : currentIndex ≥ maxIndex):
      outside := decrement ? -1 : length(list)        // escaping is only possible from inside the list
      if allowEscape and currentIndex ≠ outside:
        index := -1
      else:
        index := decrement ? maxIndex : minIndex
        wrapped := true
    else:
      index := step()
    return { index: isIndexOutOfListBounds(list, index) ? -1 : index, wrapped }
  ```
  Source: `floating-ui-react/utils/composite.ts:54-83`. Test: [L3] `floating-ui-react/utils/composite.test.ts` "steps to the adjacent index", "wraps at either end when looping", "stays at either end without looping", "leaves the list at either end when escaping is allowed", "enters the list at the far end from outside it", "skips disabled indices".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 12 Amendments 2026-10-22 item 3): implemented early, green at L3 in M2 (the six `getNextListIndex` titles); its first consumer is list navigation (M5).

- **CMP-ALG-07** — MUST. The implementation reproduces the following procedure exactly:

  ```
  isDifferentGridRow(index, cols, prevRow):
    return floor(index / cols) ≠ prevRow
  ```
  Source: `floating-ui-react/utils/composite.ts:11-13`. Test: [L3] `useListNavigation.test.tsx` "does not leave row with "both" orientation while looping".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "does not leave row with "both" orientation while looping"; `composite` exports `isDifferentGridRow`.

- **CMP-ALG-08** — MUST. The implementation reproduces the following grid navigation procedure exactly. `list` is the (cell or item) element list, `event.key` the pressed key, `onLoop` optional.

  ```
  getGridNavigatedIndex(list, { event, orientation, loopFocus, onLoop, rtl, cols, disabledIndices,
                                minIndex, maxIndex, prevIndex, stopEvent := false }):
    nextIndex := prevIndex
    vertical := event.key = "ArrowUp" ? "up" : event.key = "ArrowDown" ? "down" : none

    if vertical ≠ none:
      // ---- row structure detection (only for vertical keys) ----
      rows := []; rowIndexMap := []; hasRoleRow := false; visibleItemCount := 0
      currentRowEl := null; currentRowIndex := -1
      for each (el, idx) in list:
        if el absent: continue
        visibleItemCount += 1
        rowEl := nearest ancestor-or-self of el matching [role="row"] (composed tree, crossing shadow roots)
        if rowEl present: hasRoleRow := true
        if rowEl ≠ currentRowEl or currentRowIndex = -1:
          currentRowEl := rowEl; currentRowIndex += 1; rows[currentRowIndex] := []
        append idx to rows[currentRowIndex]
        rowIndexMap[idx] := currentRowIndex
      hasDomRows := false; inferredDomCols := 0
      if hasRoleRow:
        for each row in rows:
          if length(row) > inferredDomCols: inferredDomCols := length(row)
          if length(row) ≠ cols: hasDomRows := true
      hasVirtualizedGaps := hasDomRows and visibleItemCount < length(list)
      verticalCols := inferredDomCols > 0 ? inferredDomCols : cols

      navigateVertically(direction):
        if not hasDomRows or prevIndex = -1: return undefined
        currentRow := rowIndexMap[prevIndex]; if currentRow undefined: return undefined
        colInRow := position of prevIndex in rows[currentRow]
        step := direction = "up" ? -1 : 1
        nextRow := currentRow + step
        repeat length(rows) times:
          if nextRow < 0 or nextRow ≥ length(rows):
            if not loopFocus or hasVirtualizedGaps: return undefined
            nextRow := nextRow < 0 ? length(rows) - 1 : 0
            if onLoop present:
              clampedCol := min(colInRow, length(rows[nextRow]) - 1)
              targetItemIndex := rows[nextRow][clampedCol] ?? rows[nextRow][0]
              returned := onLoop(event, prevIndex, targetItemIndex)
              nextRow := rowIndexMap[returned] ?? nextRow
          targetRow := rows[nextRow]
          for col from min(colInRow, length(targetRow) - 1) down to 0:
            candidate := targetRow[col]
            if not isListIndexDisabled(list, candidate, disabledIndices): return candidate
          nextRow := nextRow + step
        return undefined

      navigateVerticallyWithInferredRows(direction):
        if not hasVirtualizedGaps or prevIndex = -1: return undefined
        colInRow := prevIndex mod verticalCols
        rowStep := direction = "up" ? -verticalCols : verticalCols
        lastRowStart := maxIndex - (maxIndex mod verticalCols)
        rowCount := floor(maxIndex / verticalCols) + 1
        rowStart := prevIndex - colInRow + rowStep
        repeat rowCount times:
          if rowStart < 0 or rowStart > maxIndex:
            if not loopFocus: return undefined
            rowStart := rowStart < 0 ? lastRowStart : 0
          rowEnd := min(rowStart + verticalCols - 1, maxIndex)
          for candidate from min(rowStart + colInRow, rowEnd) down to rowStart:
            if not isListIndexDisabled(list, candidate, disabledIndices): return candidate
          rowStart := rowStart + rowStep
        return undefined

      if stopEvent: consume event
      candidate := navigateVertically(vertical) ?? navigateVerticallyWithInferredRows(vertical)
      if candidate defined:
        nextIndex := candidate
      else if prevIndex = -1:
        nextIndex := vertical = "up" ? maxIndex : minIndex
      else:
        nextIndex := findNonDisabledListIndex(list, { startingIndex: prevIndex, amount: verticalCols,
                                                     decrement: vertical = "up", disabledIndices })
        if loopFocus:
          if vertical = "up" and (prevIndex - verticalCols < minIndex or nextIndex < 0):
            col := prevIndex mod verticalCols; maxCol := maxIndex mod verticalCols
            offset := maxIndex - (maxCol - col)
            nextIndex := maxCol = col ? maxIndex : (maxCol > col ? offset : offset - verticalCols)
            if onLoop present: nextIndex := onLoop(event, prevIndex, nextIndex)
          if vertical = "down" and prevIndex + verticalCols > maxIndex:
            nextIndex := findNonDisabledListIndex(list, { startingIndex: (prevIndex mod verticalCols) - verticalCols,
                                                         amount: verticalCols, disabledIndices })
            if onLoop present: nextIndex := onLoop(event, prevIndex, nextIndex)
      if isIndexOutOfListBounds(list, nextIndex): nextIndex := prevIndex

    if orientation = "both":                       // horizontal keys stay on the current row
      prevRow := floor(prevIndex / cols)
      if event.key = (rtl ? "ArrowLeft" : "ArrowRight"):
        if stopEvent: consume event
        if prevIndex mod cols ≠ cols - 1:
          nextIndex := findNonDisabledListIndex(list, { startingIndex: prevIndex, disabledIndices })
          if loopFocus and isDifferentGridRow(nextIndex, cols, prevRow):
            nextIndex := findNonDisabledListIndex(list, { startingIndex: prevIndex - (prevIndex mod cols) - 1, disabledIndices })
            if onLoop present: nextIndex := onLoop(event, prevIndex, nextIndex)
        else if loopFocus:
          nextIndex := findNonDisabledListIndex(list, { startingIndex: prevIndex - (prevIndex mod cols) - 1, disabledIndices })
          if onLoop present: nextIndex := onLoop(event, prevIndex, nextIndex)
        if isDifferentGridRow(nextIndex, cols, prevRow): nextIndex := prevIndex
      if event.key = (rtl ? "ArrowRight" : "ArrowLeft"):
        if stopEvent: consume event
        if prevIndex mod cols ≠ 0:
          nextIndex := findNonDisabledListIndex(list, { startingIndex: prevIndex, decrement: true, disabledIndices })
          if loopFocus and isDifferentGridRow(nextIndex, cols, prevRow):
            nextIndex := findNonDisabledListIndex(list, { startingIndex: prevIndex + (cols - (prevIndex mod cols)),
                                                         decrement: true, disabledIndices })
            if onLoop present: nextIndex := onLoop(event, prevIndex, nextIndex)
        else if loopFocus:
          nextIndex := findNonDisabledListIndex(list, { startingIndex: prevIndex + (cols - (prevIndex mod cols)),
                                                       decrement: true, disabledIndices })
          if onLoop present: nextIndex := onLoop(event, prevIndex, nextIndex)
        if isDifferentGridRow(nextIndex, cols, prevRow): nextIndex := prevIndex
      lastRow := floor(maxIndex / cols) = prevRow
      if isIndexOutOfListBounds(list, nextIndex):
        if loopFocus and lastRow:
          nextIndex := event.key = (rtl ? "ArrowRight" : "ArrowLeft")
                         ? maxIndex
                         : findNonDisabledListIndex(list, { startingIndex: prevIndex - (prevIndex mod cols) - 1, disabledIndices })
          if onLoop present: nextIndex := onLoop(event, prevIndex, nextIndex)
        else:
          nextIndex := prevIndex
    return nextIndex
  ```
  Source: `floating-ui-react/utils/composite.ts:111-432`. Test: [L3] `useListNavigation.test.tsx` "skips row and remains on same column when pressing ArrowDown", "skips row and remains on same column when pressing ArrowUp", "loops on the same column with ArrowDown", "loops on the same column with ArrowUp", "does not leave row with "both" orientation while looping", "looping works on last row", "wraps ArrowUp to the last row in the full list for virtualized rows", "clamps ArrowUp to the last item in a partial last row for virtualized rows", "clamps ArrowDown into a partial last row for virtualized rows", "does not wrap ArrowUp when loopFocus is false for virtualized rows", "still clamps ArrowDown into a partial last row when loopFocus is false", "falls back left in a partial last row when the preferred candidate is disabled", "falls back left when the preferred candidate is hidden", "focuses next item using ArrowRight key, skipping disabled items" (5-column `Grid` fixture, disabled `[0,1,2,3,4,5,6,7,10,15,45,48]`), "with rtl $rtl › focuses next item using … key, skipping disabled items" (7-column `ComplexGrid` fixture, disabled `[0,1,2,3,4,5,6,9,14,23,35]`); `CompositeRoot.test.tsx` grid "uniform 1x1 items", "calls onLoop when looping vertically between rows".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: every `useListNavigation.test.tsx` title the row cites runs in `js/composite-list.test.ts`, on DOM built after `test/floating-ui-tests/Grid.tsx` (5 columns), `ComplexGrid.tsx` (7) and the test file's `VirtualizedGridRows`; where Base UI's fixtures wrap the navigator with their column count (`gridNavigationWithColumns.ts`), the port sends `CompositeListOptions.Cols`, and a windowed list's length as `ItemCount`. The two `CompositeRoot.test.tsx` titles, grid "uniform 1x1 items" and "calls onLoop when looping vertically between rows", exercise the composite grid and `onLoop`, which stay deferred (§10 item 1): they are re-cited to CMP-KEY-12 and CMP-EVT-02, which already cite them, and the popup grid's titles carry this row (the track's brief, Deliverable 9).

### 5.2 Grid cell maps and navigator wrappers

- **CMP-ALG-09** — MUST. The implementation reproduces the following procedure exactly (`sizes[i] = { width, height }` in cells):

  ```
  createGridCellMap(sizes, cols, dense):
    cellMap := []            // cell index → item index; unassigned cells are undefined
    startIndex := 0
    for each ({ width, height }, index) in sizes:
      if width > cols: raise CMP-EDGE-02 (development builds only)
      placed := false
      if dense: startIndex := 0
      while not placed:
        targetCells := []
        for i in 0 … width-1:
          for j in 0 … height-1:
            append startIndex + i + j × cols to targetCells
        if (startIndex mod cols) + width ≤ cols and every cell in targetCells is unassigned:
          for each cell in targetCells: cellMap[cell] := index
          placed := true
        else:
          startIndex += 1
    return cellMap with holes materialized as undefined (length = highest assigned cell + 1)
  ```
  Source: `floating-ui-react/utils/composite.ts:435-475`. Test: [L3] `CompositeRoot.test.tsx` grid "packs items into earlier gaps when dense", rtl "uses the forward edge when navigating from a spanning item".

- **CMP-ALG-10** — MUST. The implementation reproduces the following procedure exactly:

  ```
  getGridCellIndexOfCorner(index, sizes, cellMap, cols, corner):     // corner ∈ { tl, tr, bl, br }
    if index = -1: return -1
    firstCellIndex := first position in cellMap whose value = index (−1 if none)
    sizeItem := sizes[index]
    tl: return firstCellIndex
    tr: return sizeItem absent ? firstCellIndex : firstCellIndex + sizeItem.width - 1
    bl: return sizeItem absent ? firstCellIndex : firstCellIndex + (sizeItem.height - 1) × cols
    br: return last position in cellMap whose value = index
    otherwise: return -1
  ```
  Source: `floating-ui-react/utils/composite.ts:478-510`. Test: [L3] `CompositeRoot.test.tsx` rtl "uses the forward edge when navigating from a spanning item".

- **CMP-ALG-11** — MUST. The implementation reproduces the following procedure exactly:

  ```
  getGridCellIndices(indices, cellMap):
    return every cellIndex such that indices contains cellMap[cellIndex]
    // an undefined entry in `indices` therefore selects every unassigned cell
  ```
  Source: `floating-ui-react/utils/composite.ts:513-518`. Test: [L3] `CompositeRoot.test.tsx` grid "skips disabled indices".

- **CMP-ALG-12** — MUST. *Composite grid navigator.* `gridNavigation({ cols, dense := false, itemSizes })` returns a navigator that the composite root calls with its navigation state (CMP-KEY-12); the implementation reproduces it exactly:

  ```
  navigator({ disabledIndices, elements, event, highlightedIndex, loopFocus, maxIndex, minIndex, onLoop, orientation, rtl }):
    sizes := itemSizes ?? [ { width: 1, height: 1 } × length(elements) ]
    cellMap := createGridCellMap(sizes, cols, dense)
    minGridIndex := first cellIndex with cellMap[cellIndex] present
                    and not isListIndexDisabled(elements, cellMap[cellIndex], disabledIndices)   (−1 if none)
    maxGridIndex := last such cellIndex                                                           (−1 if none)
    cellElements := for each cellIndex: cellMap[cellIndex] present ? elements[cellMap[cellIndex]] : null
    itemDisabled := disabledIndices ?? [ for each index in elements: isListIndexDisabled(elements, index) ? index : undefined ]
    cellDisabled := getGridCellIndices([ …itemDisabled, undefined ], cellMap)   // gaps count as disabled
    corner := event.key = "ArrowDown" ? "bl" : event.key = (rtl ? "ArrowLeft" : "ArrowRight") ? "tr" : "tl"
    prevCell := getGridCellIndexOfCorner(highlightedIndex > maxIndex ? minIndex : highlightedIndex, sizes, cellMap, cols, corner)
    cell := getGridNavigatedIndex(cellElements, { event, orientation, loopFocus, onLoop, cols,
                                                  disabledIndices: cellDisabled, minIndex: minGridIndex,
                                                  maxIndex: maxGridIndex, prevIndex: prevCell, rtl })   // stopEvent false
    return cellMap[cell]        // the navigated cell is never a gap
  ```
  Note: `onLoop` is invoked by CMP-ALG-08 with **cell** indices; for uniform 1×1 grids these equal item indices (see §10 Q3).
  Source: `internals/composite/root/gridNavigation.ts:50-126`. Test: [L3] `CompositeRoot.test.tsx` grid "uniform 1x1 items", "skips disabled indices", "packs items into earlier gaps when dense", "calls onLoop while navigating through grid cells", rtl "both horizontal and vertical orientation".

- **CMP-ALG-13** — MUST. *Popup grid navigator* (uniform 1×1 cells). The implementation reproduces it exactly:

  ```
  gridNavigation(event, prevIndex, list, orientation, loopFocus, rtl, disabledIndices, minIndex, maxIndex, cols := 2):
    next := getGridNavigatedIndex(list, { event, orientation, loopFocus, rtl, cols, disabledIndices, minIndex, maxIndex,
                                          prevIndex: prevIndex > maxIndex ? minIndex : prevIndex, stopEvent: true })
    return isIndexOutOfListBounds(list, next) ? undefined : next
  ```
  Callers bind `cols` by wrapping this function; Combobox with `grid: true` passes it unwrapped, so `cols` is the default `2` and vertical moves follow `[role="row"]` rows when present (CMP-ALG-08); the test fixtures wrap it with 3, 5 and 7 columns (`test/floating-ui-tests/gridNavigationWithColumns.ts:15`).
  Source: `floating-ui-react/hooks/gridNavigation.ts:17-47`, `combobox/root/AriaCombobox.tsx:1432`. Test: [L3] `useListNavigation.test.tsx` "ArrowDown focuses first item", "grid navigation with changing list items", "grid navigation with disabled list items".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "ArrowDown focuses first item", "grid navigation with changing list items" and "grid navigation with disabled list items" (the last two on DOM built after `EmojiPicker.tsx`: a search input inside the popup as the reference, 3 columns). `cols` is 2 unless `CompositeListOptions.Cols` is sent, and the vertical moves follow `[role="row"]` rows where the DOM has them.

### 5.3 Composite root

- **CMP-ALG-14** — MUST. *Composite key resolution.* The implementation reproduces the following procedure exactly on every `keydown` reaching the root (or relayed to it):

  ```
  onKeyDown(event):
    isHomeOrEnd := event.key ∈ { "Home", "End" }
    if event.key ∉ { ArrowUp, ArrowDown, ArrowLeft, ArrowRight, Home, End }: return
    if isHomeOrEnd and not enableHomeAndEndKeys: return
    if isModifierKeySet(event, modifierKeys): return                      // CMP-ALG-16
    if root element absent: return
    isRtl := direction = "rtl"
    hForward := isRtl ? "ArrowLeft" : "ArrowRight"; hBackward := isRtl ? "ArrowRight" : "ArrowLeft"
    forwardKey := orientation = "vertical" ? "ArrowDown" : hForward
    backwardKey := orientation = "vertical" ? "ArrowUp" : hBackward
    target := first element of the event's composed path (else event.target)
    if target is a native text input/textarea and not disabled (no "disabled" attr, aria-disabled ≠ "true"):
      if target.selectionStart absent or event.shiftKey or selectionStart ≠ selectionEnd: return
      if event.key ≠ backwardKey and selectionStart < length(target.value): return
      if event.key ≠ forwardKey and selectionStart > 0: return
    next := highlightedIndex
    min := getMinListIndex(elements, disabledIndices); max := getMaxListIndex(elements, disabledIndices)
    if grid present:
      next := grid({ disabledIndices, elements, event, highlightedIndex, loopFocus, maxIndex: max, minIndex: min,
                     onLoop: wrappedOnLoop, orientation, rtl: isRtl })          // CMP-ALG-12
    isForward  := (orientation ≠ "vertical" and event.key = hForward)  or (orientation ≠ "horizontal" and event.key = "ArrowDown")
    isBackward := (orientation ≠ "vertical" and event.key = hBackward) or (orientation ≠ "horizontal" and event.key = "ArrowUp")
    if enableHomeAndEndKeys:
      if event.key = "Home": next := min
      else if event.key = "End": next := max
    if next = highlightedIndex and (isForward or isBackward):
      if loopFocus and next = max and isForward:
        next := min
        if onLoop present: next := onLoop(event, highlightedIndex, next, elements)
      else if loopFocus and next = min and isBackward:
        next := max
        if onLoop present: next := onLoop(event, highlightedIndex, next, elements)
      else:
        next := findNonDisabledListIndex(elements, { startingIndex: next, decrement: isBackward, disabledIndices })
    if next ≠ highlightedIndex and not isIndexOutOfListBounds(elements, next):
      if stopEventPropagation: stop propagation of event
      if grid present or isHomeOrEnd or isForward or isBackward: prevent default of event
      setHighlightedIndex(next, scrollIntoView: true)                     // CMP-STA-30
      after a microtask: focus elements[next] if present
  ```
  where `wrappedOnLoop(event, prev, next) := onLoop present ? onLoop(event, prev, next, elements) : next`.
  Source: `internals/composite/root/useCompositeRoot.ts:195-202,206-317`. Test: [L3] `CompositeRoot.test.tsx` "controlled mode", "uncontrolled mode", "Home key moves focus to the first item", "calls onLoop and uses its return value", "stays on the current item when onLoop returns prevIndex", "keeps native input behavior when the native target differs from the synthetic target", "prevents arrow key navigation when any modifier key is pressed by default".
  Port note (2026-10-22, M2 track 3, issue 79; CMP §10 Q1): transcribed without the grid branch and without `onLoop`; "uncontrolled mode", "Home key moves focus to the first item" and the native-input and modifier titles run at L3; "controlled mode" is M3 and the `onLoop` titles wait for a consumer.

- **CMP-ALG-15** — MUST. The implementation reproduces the following procedure exactly:

  ```
  getFallbackIndex(elements, disabledIndices):
    fallback := -1
    for index in 0 … length(elements) - 1:
      element := elements[index]
      if element absent or isListIndexDisabled(elements, index, disabledIndices): continue
      if element has attribute "data-composite-item-active": return index
      if fallback = -1: fallback := index
    return max(fallback, 0)
  ```
  Source: `internals/composite/root/useCompositeRoot.ts:345-365`. Test: [L3] `CompositeRoot.test.tsx` "moves the tab stop to the active item when the highlighted item is removed", "skips items that cannot hold the tab stop", "keeps the tab stop in range when no item can hold it".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id. The procedure is `getFallbackIndex` in `js/composite.ts`, also called by a port title.

- **CMP-ALG-16** — MUST. The implementation reproduces the following procedure exactly:

  ```
  isModifierKeySet(event, allowedModifiers):
    for key in [ "Shift", "Control", "Alt", "Meta" ]:
      if allowedModifiers contains key: continue
      if event.getModifierState(key): return true
    return false
  ```
  Source: `internals/composite/root/useCompositeRoot.ts:367-377`, `internals/composite/composite.ts:24-26`. Test: [L3] `CompositeRoot.test.tsx` "specifies allowed modifier keys that do not prevent arrow key navigation when pressed".

- **CMP-ALG-17** — MUST. *Registry rebuild.* The implementation reproduces the following procedure exactly whenever the registry is dirty (post-commit, pre-paint) or a move was detected:

  ```
  sortByDocumentPosition(a, b):
    if a.nextElementSibling = b: return -1
    if b.nextElementSibling = a: return 1
    return (a.compareDocumentPosition(b) has DOCUMENT_POSITION_FOLLOWING) ? -1 : 1

  snapshot(map):                                     // map: element → registration, in registration order
    reserved := ∅; items := []; automatic := []
    for each (node, registration) in map:
      if node not connected: continue
      item := { index: registration.index ?? -1, element: node, registration }
      if registration.index is null: append item to automatic
      else if registration.index ≥ 0: add registration.index to reserved; append item to items
    sort automatic with sortByDocumentPosition on elements
    nextAutomatic := 0
    for each item in automatic:
      while reserved contains nextAutomatic: nextAutomatic += 1
      item.index := nextAutomatic; append item to items; nextAutomatic += 1
    if reserved ≠ ∅: sort items by index ascending
    return items, [ element of each automatic item ]

  syncRefs(items):
    nextMap := empty ordered map
    truncate elements array and labels array (when present) to length 0
    for each item in items:
      nextMap[item.element] := { …(item.registration.metadata ?? {}), index: item.index }
      elements[item.index] := item.element
      if labels present:
        labels[item.index] := item.registration.label ≠ undefined ? item.registration.label
                              : (item.registration.textElement?.textContent ?? item.element.textContent)
    nextGuess := length(elements)
    return nextMap

  flush():
    (items, automaticElements) := snapshot(map)
    nextMap := syncRefs(items)
    changed := previousItems absent or length differs or exists position p where
               items[p].index ≠ previousItems[p].index or items[p].element ≠ previousItems[p].element
               or items[p].registration.index ≠ previousItems[p].registration.index
               or items[p].registration.metadata ≠ previousItems[p].registration.metadata (identity)
    observe(automaticElements)                      // CMP-STA-11/12
    previousItems := items; dirty := false
    if changed: notify every item subscriber with nextMap; call onMapChange(nextMap)
  ```
  Source: `internals/composite/list/CompositeList.tsx:61-88,143-171,228-273,299-312`. Test: [L3] `CompositeList.test.tsx` "reserves explicit slots when assigning automatic indexes", "registers explicitly indexed items in their index-addressed slots", "resolves each label source", "only publishes maps that are aligned with the element registry", "syncs replacement refs without publishing an unchanged map".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). Labels and `guess` (CMP-STA-06/07/16) are M5 and not built, so "resolves each label source" waits for M5; the other four titles run.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1), chosen by the user): labels are built; "resolves each label source" runs in `composite.test.ts`, so all five titles run.

- **CMP-ALG-18** — MUST. *Composite default-index adjustment on publication.* The implementation reproduces the following procedure exactly (`setHighlighted(i)` = CMP-EVT-01 without scrolling; `sortedElements` = the map's keys in map order):

  ```
  onMapChange(map):
    if size(map) = 0: return
    if defaultIndexSet:
      nextIndex := position of highlightedElement in elements (−1 if absent)
      if nextIndex = -1:
        replacement := elements[highlightedIndex]
        if replacement absent or isListIndexDisabled(elements, highlightedIndex, disabledIndices):
          setHighlighted(getFallbackIndex(elements, disabledIndices))
        else:
          highlightedElement := replacement
      else if nextIndex ≠ highlightedIndex:
        setHighlighted(nextIndex)
      return
    defaultIndexSet := true
    sortedElements := keys of map
    activeItem := first of sortedElements having attribute "data-composite-item-active", else null
    activeIndex := activeItem present ? (map[activeItem].index ?? -1) : -1
    if activeIndex ≠ -1:
      setHighlighted(activeIndex)
    else if isListIndexDisabled(sortedElements, highlightedIndex, disabledIndices):
      first := findNonDisabledListIndex(sortedElements, { disabledIndices })
      if not isIndexOutOfListBounds(sortedElements, first): setHighlighted(first)
    scrollIntoViewIfNeeded(rootElement, activeItem, direction, orientation)
  ```
  Note: the "disabled default" check indexes `sortedElements` by position, which differs from registry index only when explicit indexes leave gaps (§10 Q4).
  Source: `internals/composite/root/useCompositeRoot.ts:110-164`. Test: [L3] `CompositeRoot.test.tsx` "uses an active item explicit index as the initial highlighted index", "keeps the tab stop on the highlighted item when an earlier item is removed", "moves the tab stop back into range when the highlighted item is removed".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id.

- **CMP-ALG-19** — MUST. *Scroll a composite item into view.* The implementation reproduces the following procedure exactly (`offset(container, element, side)` sums `offsetLeft`/`offsetTop` up the offset-parent chain until the container is reached; `styles(el)` reads the eight `scroll-margin-*`/`scroll-padding-*` computed values as numbers, `0` when unparsable):

  ```
  scrollIntoViewIfNeeded(container, element, direction, orientation):
    if container absent or element absent or element lacks scrollTo: return
    targetX := container.scrollLeft; targetY := container.scrollTop
    overflowX := container.clientWidth < container.scrollWidth
    overflowY := container.clientHeight < container.scrollHeight
    if overflowX and orientation ≠ "vertical":
      left := offset(container, element, "left"); c := styles(container); e := styles(element)
      rightOverflow := left + element.offsetWidth + e.scrollMarginRight > container.scrollLeft + container.clientWidth - c.scrollPaddingRight
      leftOverflow  := left - e.scrollMarginLeft < container.scrollLeft + c.scrollPaddingLeft
      alignRight := left + element.offsetWidth + e.scrollMarginRight - container.clientWidth + c.scrollPaddingRight
      alignLeft  := left - e.scrollMarginLeft - c.scrollPaddingLeft
      if direction = "ltr":
        if rightOverflow: targetX := alignRight
        else if leftOverflow: targetX := alignLeft
      if direction = "rtl":
        if leftOverflow: targetX := alignLeft
        else if rightOverflow: targetX := alignRight
    if overflowY and orientation ≠ "horizontal":
      top := offset(container, element, "top"); c := styles(container); e := styles(element)
      if top - e.scrollMarginTop < container.scrollTop + c.scrollPaddingTop:
        targetY := top - e.scrollMarginTop - c.scrollPaddingTop
      else if top + element.offsetHeight + e.scrollMarginBottom > container.scrollTop + container.clientHeight - c.scrollPaddingBottom:
        targetY := top + element.offsetHeight + e.scrollMarginBottom - container.clientHeight + c.scrollPaddingBottom
    container.scrollTo({ left: targetX, top: targetY, behavior: "auto" })   // always called, even when unchanged
  ```
  Source: `internals/composite/composite.ts:44-174`. Test: [L3] `internals/composite/composite.test.ts` "uses the left scroll margin when checking left overflow in RTL".

### 5.4 List navigation

- **CMP-ALG-20** — MUST. *Key predicates.* The implementation reproduces the following procedures exactly:

  ```
  switchOn(orientation, vertical, horizontal):
    "vertical" → vertical; "horizontal" → horizontal; otherwise → vertical or horizontal

  isMainOrientationKey(key, o)            := switchOn(o, key ∈ {ArrowUp, ArrowDown}, key ∈ {ArrowLeft, ArrowRight})
  isMainOrientationToEndKey(key, o, rtl)  := switchOn(o, key = ArrowDown, key = (rtl ? ArrowLeft : ArrowRight))
                                             or key = "Enter" or key = " " or key = ""
  isCrossOrientationOpenKey(key, o, rtl)  := switchOn(o, key = (rtl ? ArrowLeft : ArrowRight), key = ArrowDown)
  isCrossOrientationCloseKey(key, o, rtl, grid):
    if o = "both" or (o = "horizontal" and grid): return key = "Escape"
    return switchOn(o, key = (rtl ? ArrowRight : ArrowLeft), key = ArrowUp)

  parentOrientation() := parentOrientation option ?? orientation published by the parent popup (CMP-STA-41) ?? absent
                         // absent takes the switchOn "otherwise" branch, i.e. behaves as "both"
  ```
  Source: `floating-ui-react/hooks/useListNavigation.ts:41-99`. Test: [L3] `useListNavigation.test.tsx` "keyboard navigation in nested menus lists", "keyboard navigation in nested menus with different orientation", rtl "navigates down on ArrowLeft".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "keyboard navigation in nested menus lists", "keyboard navigation in nested menus with different orientation" and rtl "navigates down on ArrowLeft". `isCrossOrientationCloseKey` and `isMainOrientationToEndKey` are exported for Menu's filter keydown (track 12; §6.8's note).

- **CMP-ALG-21** — MUST. *Floating key handling* (Table C). The implementation reproduces the following procedure exactly (`cursor`, flags and `navigate(event)` = `onNavigate(cursor = -1 ? null : cursor, event)` as in §4.2.4):

  ```
  commonOnKeyDown(event):
    pointerModality := false; syncFocus := true
    if event.which = 229: return
    if not open and event.currentTarget = popupFocusElement: return
    if nested and isCrossOrientationCloseKey(event.key, orientation, rtl, grid present):
      if not isMainOrientationKey(event.key, parentOrientation()): consume event
      setOpen(false, details(reason: "list-navigation", event: native event))
      returnEl := nestedReturnFocusElement ?? referenceElement; if returnEl is an HTML element: focus returnEl
      return
    if activeIndex present and activeIndex ≠ cursor and not isIndexOutOfListBounds(list, activeIndex): cursor := activeIndex
    current := cursor
    min := getMinListIndex(list, disabledIndices); max := getMaxListIndex(list, disabledIndices)
    if reference is not a typeable combobox:
      if event.key = "Home": consume event; cursor := min; navigate(event)
      if event.key = "End":  consume event; cursor := max; navigate(event)
    if grid present:
      index := grid(event, cursor, list, orientation, loopFocus, rtl, disabledIndices, min, max)   // CMP-ALG-13
      if index present and index ≠ cursor: cursor := index; navigate(event)
      if orientation = "both": return
    if isMainOrientationKey(event.key, orientation):
      consume event
      focused := active element of the current target's document (descending into shadow roots)
      if open and not virtual and currentTarget contains focused and no list item contains focused:
        cursor := isMainOrientationToEndKey(event.key, orientation, rtl) ? min : max
        navigate(event)
        if activeIndex = cursor: focusItem()                               // CMP-ALG-23
        return
      { index, wrapped } := getNextListIndex(list, current, { decrement: not isMainOrientationToEndKey(event.key, orientation, rtl),
                                                             loopFocus, allowEscape, disabledIndices, minIndex: min, maxIndex: max })
      if wrapped: syncFocus := false
      cursor := index
      navigate(event)
  ```
  Source: `floating-ui-react/hooks/useListNavigation.ts:551-685`. Test: see CMP-KEY-25 to CMP-KEY-33.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `see` kept: the procedure is `commonKeyDown` in `js/composite.ts`, proven through CMP-KEY-25 to CMP-KEY-33 as their notes say (CMP-KEY-26 has no title; CMP-KEY-30's titles are the popup-list suite's, green at L4).

- **CMP-ALG-22** — MUST. *Trigger key handling* (Tables A, B, D). The implementation reproduces the following procedure exactly:

  ```
  triggerOnKeyDown(event):
    currentOpen := open state read at event time
    pointerModality := false
    isArrowKey := event.key starts with "Arrow"
    isParentCrossOpenKey := isCrossOrientationOpenKey(event.key, parentOrientation(), rtl)
    isMainKey := isMainOrientationKey(event.key, currentOpen ? orientation : triggerOrientation)
    isNavigationKey := (nested ? isParentCrossOpenKey : isMainKey) or event.key = "Enter" or trim(event.key) = ""
    if virtual and currentOpen and (not nested or currentTarget is a text-entry element): commonOnKeyDown(event); return
    if not currentOpen and not openOnArrowKeyDown and isArrowKey: return
    if isNavigationKey:
      recordedKey := (nested and isMainOrientationKey(event.key, parentOrientation())) ? null : event.key
    if nested:
      if isParentCrossOpenKey:
        consume event
        if currentOpen:
          cursor := getMinListIndex(list, disabledIndices); navigate(event)
          if virtual: focus popupFocusElement
        else:
          setOpen(true, details(reason: "list-navigation", event: native event, trigger: currentTarget))
      return
    if isMainKey:
      if selectedIndex present: cursor := selectedIndex
      consume event
      if not currentOpen and openOnArrowKeyDown:
        setOpen(true, details(reason: "list-navigation", event: native event, trigger: currentTarget))
      else:
        commonOnKeyDown(event)
      if currentOpen: navigate(event)

  triggerOnFocus(event):
    if event.target ≠ event.currentTarget: return
    if open and not virtual: cursor := -1; navigate(event)
  ```
  Source: `floating-ui-react/hooks/useListNavigation.ts:815-929`. Test: see CMP-KEY-18 to CMP-KEY-23, CMP-FOC-06.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `see` kept: the procedure is `triggerKeyDown` and the trigger's focus handler in `js/composite.ts`, proven through CMP-KEY-18 to CMP-KEY-23 and CMP-FOC-06 as their notes say.

- **CMP-ALG-23** — MUST. *Item focus procedure.* The implementation reproduces the following procedure exactly:

  ```
  focusItem():
    cancel any pending deferred focus frame of this list
    runFocus(item) := if not virtual: pendingCancel := enqueueFocus(item, { sync: syncFocus, preventScroll: true })   // CMP-ALG-31
    initialItem := list[cursor]
    forced := forceScrollIntoView
    if initialItem present: runFocus(initialItem)
    later := syncFocus ? run immediately : run on the next frame
    later(():
      waited := list[cursor] ?? initialItem
      if waited absent: return
      if initialItem absent: runFocus(waited)
      if forced or not pointerModality: waited.scrollIntoView({ block: "nearest", inline: "nearest" }) if supported
    )
  ```
  Source: `floating-ui-react/hooks/useListNavigation.ts:327-372`. Test: see CMP-FOC-03, CMP-FOC-04.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `see` kept: the procedure is `focusItem` in `js/composite.ts`, which queues through the `focus-manager` module's `enqueueFocus`. CMP-FOC-03's and CMP-FOC-04's titles are green at L4 (their notes); the procedure's jsdom paths run under CMP-STA-35's and CMP-KEY-32's titles.
  Port note (2026-11-04, F1 of [PR 183](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/183); ADR 04 Amendments 2026-11-04, "the list module's open-focus mark", settled by the orchestrator 2026-11-04 as (b)): while the list is open, each item focus the procedure queues is marked first, under the session data slot `blazix.composite.openFocus`, as `{ element, origin }`: the item, and the element that had focus before the list first moved it this open (`null` for the body or an element inside the floating element). The close clears it. The focus manager reads the mark at the open's first arm (FLT-FOC-20's dated note): the port's handle can attach after this focus moved focus in, which Base UI's layout effect never sees. Test: [L3] `composite-list.test.ts` "port: the open focus marks the item it moved focus to and the element focus came from, until the close".

- **CMP-ALG-24** — MUST. *Wait for the list on open.* The implementation reproduces the following procedure exactly (invoked by CMP-STA-37):

  ```
  waitForListPopulated(runs := 0):
    if list[0] absent:
      if runs < 2:
        schedule := runs = 0 ? microtask : next frame
        schedule(waitForListPopulated(runs + 1))
      return
    cursor := (recordedKey is null or isMainOrientationToEndKey(recordedKey, triggerOrientation, rtl) or nested)
              ? getMinListIndex(list)      // no disabledIndices: DOM-disabled items skipped
              : getMaxListIndex(list)
    recordedKey := null
    navigate(no event)
  ```
  Source: `floating-ui-react/hooks/useListNavigation.ts:446-476`. Test: [L3] `useListNavigation.test.tsx` "opens on ArrowDown and focuses first item", "skips disabled item on initial navigation".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "opens on ArrowDown and focuses first item" and "skips disabled item on initial navigation".

### 5.5 Typeahead

- **CMP-ALG-25** — MUST. *Typeahead key handling.* The implementation reproduces the following procedure exactly (`buffer`, `prevIndex`, `matchIndex`, `timer` per CMP-STA-42; `labels` = labels array, `elements` = optional elements array):

  ```
  isItemAvailable(index):
    element := elements?[index]
    if (element present and not isElementVisible(element)) or element matches ':disabled': return false
    return disabledIndices absent or not isListIndexDisabled([], index, disabledIndices)   // explicit check only

  getMatchingIndex(labels, string, startIndex := 0):
    if length(labels) = 0: return -1
    start := ((startIndex mod length(labels)) + length(labels)) mod length(labels)
    lower := lowercase(string)                                   // locale-insensitive
    for offset in 0 … length(labels) - 1:
      index := (start + offset) mod length(labels)
      text := labels[index]
      if text present and lowercase(text) starts with lower and isItemAvailable(index): return index
    return -1

  onKeyDown(event):
    if length(buffer) > 0 and event.key = " ": consume event; onTyping(true)
    if length(buffer) > 0 and buffer[0] ≠ " ":
      if getMatchingIndex(labels, buffer) = -1 and event.key ≠ " ": onTyping(false)
    if labels absent or length(event.key) ≠ 1 or event.ctrlKey or event.metaKey or event.altKey: return
    if open and event.key ≠ " ": consume event; onTyping(true)
    isNewSession := buffer = ""
    if isNewSession: prevIndex := selectedIndex ?? activeIndex ?? -1
    allowRapid := for every (text, index) in labels:
                    (text present and isItemAvailable(index)) ? lowercase(text[0]) ≠ lowercase(text[1]) : true
    if allowRapid and buffer = event.key: buffer := ""; prevIndex := matchIndex
    buffer := buffer + event.key
    timer.start(resetMs, (): buffer := ""; prevIndex := matchIndex; onTyping(false))
    base := isNewSession ? (selectedIndex ?? activeIndex ?? -1) : prevIndex
    index := getMatchingIndex(labels, buffer, (base ?? 0) + 1)
    if index ≠ -1: onMatch(index, event); matchIndex := index
    else if event.key ≠ " ": buffer := ""; onTyping(false)
  ```
  Source: `floating-ui-react/hooks/useTypeahead.ts:93-204`. Test: [L3] `useTypeahead.test.tsx` "rapidly focuses list items when they start with the same letter", "bails out of rapid focus of first letter if the list contains a string that starts with two of the same letter", "starts from the current activeIndex and correctly loops (strict: %s)", "skips hidden items when matching with elementsRef".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-typeahead.test.ts` "rapidly focuses list items when they start with the same letter", "bails out of rapid focus of first letter if the list contains a string that starts with two of the same letter", "starts from the current activeIndex and correctly loops (strict: %s)" and "skips hidden items when matching with elementsRef".

- **CMP-ALG-26** — MUST. *Typeahead blur and reset.* The implementation reproduces the following procedures exactly:

  ```
  onBlur(event):
    next := event.relatedTarget
    if referenceElement contains next or floatingElement contains next (composed containment): return
    timer.clear(); buffer := ""; prevIndex := matchIndex; onTyping(false)

  on change of (open, selectedIndex):            // post-commit, pre-paint
    if not open and selectedIndex ≠ null: return
    timer.clear(); matchIndex := null
    if buffer ≠ "": buffer := ""; onTyping(false)
  ```
  Source: `floating-ui-react/hooks/useTypeahead.ts:206-237`. Test: [L3] `useTypeahead.test.tsx` "onTyping is called when the popup closes without moving focus".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3, restated as under CMP-STA-44 ("port: onTyping is called when the popup closes without moving focus (the close clears the typing flag)"); the blur keeps the session while the focus moves between the reference and the floating element ("matches when focus is within floating").

### 5.6 Small helpers

- **CMP-ALG-27** — MUST. The implementation reproduces the following procedure exactly:

  ```
  getHighlightReason(event):
    if event absent: return "none"
    if event.type starts with "key": return "keyboard"
    if event.type starts with "mouse" or event.type starts with "pointer": return "pointer"
    return "none"
  ```
  Source: `utils/getHighlightReason.ts:8-21`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept, as CMP-EVT-09: the procedure is `HighlightReason.From(EventSnapshot?)` (`Events/`, EVD-ALG-11), proven at L1 by `HighlightReasonTests` "derives the highlight reason from the event type" and "no event means no reason".

- **CMP-ALG-28** — MUST. *Virtual (assistive-technology) press detection*, used by CMP-STA-39. The implementation reproduces the following procedures exactly (`platform.os.android` and `platform.env.jsdom` are static user-agent flags, see [shared-utilities](shared-utilities.md)):

  ```
  isVirtualClick(event):                       // MouseEvent or PointerEvent
    if event.pointerType = "" and event.isTrusted: return true
    if android and event.pointerType present: return event.type = "click" and event.buttons = 1
    return event.detail = 0 and event.pointerType absent

  isVirtualPointerEvent(event):                // PointerEvent
    if jsdom: return false
    return (not android and event.width = 0 and event.height = 0)
        or (event.type = "pointerdown" and event.width = 1 and event.height = 1 and event.pressure = 0
            and event.detail = 0 and event.pointerType = "mouse" and (android or event.buttons = 0))
        or (event.width < 1 and event.height < 1 and event.pressure = 0 and event.detail = 0 and event.pointerType = "touch")
  ```
  Source: `floating-ui-react/utils/event.ts:13-48`. Test: none (also used by hooks owned by [floating-and-positioning](floating-and-positioning.md)).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title here). The procedures are `interactions`' `isVirtualClick` and `isVirtualPointerEvent`, which `composite` imports and does not copy; proven at L3 by `js/composite-list.test.ts` "port: a virtual click on the trigger opens with the first item highlighted, and a pointer click opens with none".

- **CMP-ALG-29** — MUST. The implementation reproduces the following procedure exactly:

  ```
  isStationaryWebKitPointer(event):
    return webkitEngine and event.movementX = 0 and event.movementY = 0
  ```
  Source: `floating-ui-react/hooks/useListNavigation.ts:37-39`. Test: [L3] `useListNavigation.webkit.test.tsx` (both tests).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "ignores stationary mousemove events fired when the list scrolls beneath the pointer" and "keeps keyboard modality when a stationary pointermove fires on the floating element"; the engine flag is `platform.engine.webkit` (`js/platform.ts`, re-exported by `dom`).

- **CMP-ALG-30** — MUST. *Keyed registry.* The implementation reproduces the following procedure exactly:

  ```
  registry := empty map; version := 0; updateScheduled := false
  scheduleUpdate(): if updateScheduled: return; updateScheduled := true; version += 1  (schedules one re-render)
  registerItem(key, item): registry[key] := item; scheduleUpdate(); return (): delete registry[key]; scheduleUpdate()
  items := copy of registry taken when version changes (immutable snapshot)
  post-commit, pre-paint after version changes: updateScheduled := false
  ```
  Source: `internals/useItemRegistry.ts:14-54`. Test: [L2] `useItemRegistry.test.tsx` (all three tests).

- **CMP-ALG-31** — MUST. *Deferred focus* (`enqueueFocus`, shared with [floating-and-positioning](floating-and-positioning.md)). The implementation reproduces the following procedure exactly; the pending frame id is a single process-wide slot:

  ```
  enqueueFocus(element, { preventScroll := false, sync := false, shouldFocus }):
    cancel the process-wide pending frame (if any)
    exec() := if shouldFocus present and not shouldFocus(): return; element?.focus({ preventScroll })
    if sync: exec(); return no-op cancel
    id := request next frame(exec); pending := id
    return cancel(): if pending = id: cancel frame id; pending := none
  ```
  Source: `floating-ui-react/utils/enqueueFocus.ts:12-37`. Test: [L4] `useListNavigation.test.tsx` "cancels pending item focus when the pointer leaves before focus lands".
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): re-tagged [L3] → [L4] (ADR 13 §3): the cited title is browser-only in Base UI (`it.skipIf(isJSDOM)`, `useListNavigation.test.tsx:744`), so it runs nowhere at L3; it is green at L4 under CMP-PTR-05's "(L4 for the focus clause …)": `tests/browser/composite-list-rows.spec.ts` "cancels pending item focus when the pointer leaves before focus lands", both modes. The procedure is the `focus-manager` module's `enqueueFocus` (one page-global slot), which `composite` imports; design point 6 (a) (settled by the orchestrator 2026-11-04) moved the visibility predicates into `dom`, so the import makes no cycle (ADR 04 §1).

## 6. Configuration surface used by components

Component docs reference these option names in their §11. Source for defaults: as cited per table.

### 6.1 Composite root part (`CompositeRoot`) — `internals/composite/root/CompositeRoot.tsx:101-131`, defaults at `:18-41`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `render`, `className`, `style`, `children` | per [rendering-and-composition](rendering-and-composition.md) | — | Element substitution and styling of the root element. |
| `tag` | element tag name | `'div'` | Default element of the root. |
| `props` | array of attribute objects or functions returning them | `[]` | Extra attribute groups merged onto the root (after the root's own `focus`/`keydown` handlers, before the remaining element props). |
| `refs` | array of DOM handles | `[]` | Additional DOM handles to attach to the root element. |
| `state`, `stateAttributesMapping` | per [rendering-and-composition](rendering-and-composition.md) | `{}` / none | State object and mapping for `data-*` attributes. |
| `orientation` | `'horizontal' \| 'vertical' \| 'both'` | `'both'` (mechanism default) | CMP-API-02. |
| `grid` | grid navigator (CMP-ALG-12) or absent | absent | Enables grid navigation. |
| `loopFocus` | `boolean` | `true` (mechanism default) | CMP-API-01. |
| `onLoop` | `(event, prevIndex, nextIndex, elementsArray) => number` | absent | CMP-EVT-02. |
| `highlightedIndex` | `number` | absent (root-owned, initial `0`) | Consumer-owned highlighted index (CMP-STA-22). |
| `onHighlightedIndexChange` | `(index: number) => void` | absent | CMP-EVT-01. |
| `enableHomeAndEndKeys` | `boolean` | `false` | CMP-KEY-01. |
| `onMapChange` | `(map) => void` | absent | CMP-EVT-03. |
| `onKeyDown` | keyboard handler | absent | CMP-EVT-12. |
| `stopEventPropagation` | `boolean` | `true` | CMP-KEY-10, CMP-KEY-15. |
| `rootRef` | DOM handle | absent | Receives the root element. |
| `disabledIndices` | `number[]` | absent | Indices that cannot hold the tab stop or be navigated to (CMP-ALG-02 semantics; the part accepts arrays only). |
| `modifierKeys` | subset of `['Shift','Control','Alt','Meta']` | `[]` | Modifiers that do not block navigation (CMP-KEY-02). |
| `highlightItemOnHover` | `boolean` | `false` | CMP-API-03 / CMP-PTR-01. |

### 6.2 Bare composite root mechanism (`useCompositeRoot`) — `internals/composite/root/useCompositeRoot.ts:33-88`

Same options as 6.1 minus the rendering ones, with these differences: `direction` (`'ltr' \| 'rtl'`) is **required**; `stopEventPropagation` defaults to unset (`false`); there is no `highlightItemOnHover` (the caller supplies it to the shared state itself). The mechanism returns: root attributes (`focus` and `keydown` handlers plus the root DOM handle), `highlightedIndex`, `onHighlightedIndexChange(index, shouldScrollIntoView := false)`, the elements array, `onMapChange` (to connect an item registry) and `relayKeyboardEvent` (CMP-KEY-14). Direct users: `tabs/tab/TabsTab.tsx`, `menu/trigger/MenuTrigger.tsx`, `internals/use-button/useButton.ts` (reads the shared state only).

### 6.3 Composite root shared state (`CompositeRootContext`) — `internals/composite/root/CompositeRootContext.ts:4-15`

| Field | Type | Meaning |
|---|---|---|
| `highlightedIndex` | `number` | Current highlighted index. |
| `onHighlightedIndexChange` | `(index, shouldScrollIntoView?) => void` | Sets the highlighted index; `true` scrolls per CMP-ALG-19. |
| `highlightItemOnHover` | `boolean` | CMP-PTR-01. |
| `relayKeyboardEvent` | `(keyboardEvent) => void` | CMP-KEY-14. |

### 6.4 Composite item part (`CompositeItem`) — `internals/composite/item/CompositeItem.tsx:38-49`, defaults `:12-23`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `render`, `className`, `style`, `children`, `state`, `stateAttributesMapping` | per [rendering-and-composition](rendering-and-composition.md) | — | Rendering of the item. |
| `tag` | element tag name | `'div'` | Default element. |
| `props` | array of attribute objects | `[]` | Merged after the item's own `tabindex`/`focus`/`mousemove` attributes. |
| `refs` | array of DOM handles | `[]` | Attached after the registration handle (CMP-STR-04). |
| `metadata` | any | absent | Published in the item map (CMP-STA-08). |

`useCompositeItem({ metadata })` (`item/useCompositeItem.ts:14-46`) returns the item attributes (`tabIndex`, `onFocus`, `onMouseMove`), the registration DOM handle and the resolved `index`.

### 6.5 Item registration (`useCompositeListItem`) — `internals/composite/list/useCompositeListItem.ts:6-22`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `index` | `number` | absent (automatic) | Explicit index (CMP-STA-01, CMP-STA-04, CMP-STA-17). |
| `guess` | `boolean` | `false` | Guess the initial index from creation order (CMP-STA-16). |
| `label` | `string \| null` | `undefined` | Label for the labels array (CMP-STA-06). |
| `textRef` | DOM handle | absent | Element whose text content is the label when `label` is `undefined`. |
| `metadata` | any | absent | Published metadata; keep the object identity stable to avoid re-registration. |

Returns `{ ref, index }` where `ref` is the registration DOM handle and `index` the resolved index (`-1` before publication). Users: accordion items, combobox chips/items, menu items (all kinds) and submenu triggers, OTP inputs, select items, slider thumbs, tabs panels.

### 6.6 Item registry (`CompositeList`) — `internals/composite/list/CompositeList.tsx:316-330`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `children` | content | — | Subtree containing the items. |
| `elementsRef` | mutable array handle | required | The elements array (CMP-STA-05); passed as `listRef` to list navigation. |
| `labelsRef` | mutable array handle | absent | The labels array; passed as `listRef` to typeahead. |
| `onMapChange` | `(map) => void` | absent | CMP-STA-09. |

Shared state fields (`CompositeListContextValue`, `list/CompositeListContext.ts:11-16`): `register(node, registration)`, `unregister(node)`, `subscribeMapChange(fn) => unsubscribe`, `nextIndexRef` (next guess).

### 6.7 Composite grid configuration (`gridNavigation(config)`) — `internals/composite/root/gridNavigation.ts:18-27`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `cols` | `number` | required | Columns of the grid. |
| `dense` | `boolean` | `false` | Backfill earlier gaps when placing multi-cell items (CMP-ALG-09). |
| `itemSizes` | `{ width, height }[]` | absent (all 1×1) | Cell span of each item, by index. |

### 6.8 List navigation (`useListNavigation(store, props)`) — `floating-ui-react/hooks/useListNavigation.ts:101-233`, defaults `:244-267`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `listRef` | mutable array handle | required | Elements array. |
| `activeIndex` | `number \| null` | required | Consumer-owned active index (CMP-STA-31). |
| `onNavigate` | `(index \| null, event?) => void` | no-op | CMP-EVT-04. |
| `enabled` | `boolean` | `true` | When `false`, no handlers or attributes are emitted (CMP-STR-06). |
| `selectedIndex` | `number \| null` | `null` | CMP-STA-33, CMP-KEY-22, CMP-KEY-38 (typeahead reads its own copy). |
| `focusItemOnOpen` | `boolean \| 'auto'` | `'auto'` | CMP-STA-39. |
| `focusItemOnHover` | `boolean` | `true` | CMP-PTR-04/05 (components map `highlightItemOnHover` to it). |
| `openOnArrowKeyDown` | `boolean` | `true` | CMP-KEY-19, CMP-KEY-22. |
| `disabledIndices` | `number[] \| (index) => boolean` | `undefined` | CMP-ALG-02; `[]` means "nothing explicitly disabled, only natively disabled/hidden items are skipped". |
| `allowEscape` | `boolean` | `false` | CMP-KEY-33 (requires `loopFocus` and `virtual`). |
| `loopFocus` | `boolean` | `false` | CMP-API-01. |
| `nested` | `boolean` | `false` | Nested-list semantics (Table D, CMP-KEY-23, CMP-KEY-27). |
| `parentOrientation` | orientation | absent (read from the parent popup, CMP-STA-41) | Orientation of the parent list for nested keys. |
| `rtl` | `boolean` | `false` | CMP-I18N-04. |
| `virtual` | `boolean` | `false` | Virtual focus (CMP-ARIA-01, CMP-FOC-05). |
| `orientation` | `'vertical' \| 'horizontal' \| 'both'` | `'vertical'` | CMP-API-02. |
| `triggerOrientation` | orientation | `orientation` | Orientation used for trigger keys while closed (Table A, CMP-STA-37). |
| `id` | `string` | absent | Prefix for `aria-activedescendant` ids (CMP-ARIA-01). |
| `resetOnPointerLeave` | `boolean` | `true` | CMP-PTR-05. |
| `externalTree` | popup tree store | absent | Tree used for nested-parent lookups when none is provided by an ancestor. |
| `nestedReturnFocusRef` | DOM handle | absent | Element to focus when a nested list closes or on <kbd>Shift</kbd>+<kbd>Tab</kbd> (CMP-KEY-24, CMP-KEY-27). |
| `grid` | popup grid navigator (CMP-ALG-13) or absent | absent | Enables grid navigation. |

Users: `combobox/root/AriaCombobox.tsx` (Combobox and Autocomplete), `menu/root/MenuRoot.tsx` and `menu/filter-root/*`, `select/root/SelectRoot.tsx`.

Fidelity note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); conventions §3.5): no file under `menu/filter-root/` calls `useListNavigation`; the filter root reaches the hook through `MenuRootInternal` with `virtualFocus`, and its keydown and highlight helpers import CMP-ALG-20's predicates (`isCrossOrientationCloseKey`, `isMainOrientationToEndKey`; `useMenuFilterKeyDown.ts:20`, `useMenuFilterPopup.ts:15`, `moveHighlightFrom.ts:8`), which the port's `composite` module exports for Menu's filter mode (track 12).

### 6.9 Typeahead (`useTypeahead(store, props)`) — `floating-ui-react/hooks/useTypeahead.ts:13-65`, defaults `:73-83`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `listRef` | mutable array handle of `string \| null` | required | Labels array. |
| `activeIndex` | `number \| null` | required | Session base (CMP-KEY-38). |
| `onMatch` | `(index, keydownEvent) => void` | absent | CMP-EVT-10. |
| `elementsRef` | mutable array handle | absent | Enables hidden/natively-disabled skipping (CMP-KEY-42). |
| `disabledIndices` | `number[] \| (index) => boolean` | absent | Explicitly unavailable indices (CMP-KEY-42). |
| `onTyping` | `(isTyping: boolean) => void` | absent | CMP-EVT-11. |
| `enabled` | `boolean` | `true` | CMP-STR-07. |
| `resetMs` | `number` (ms) | `750` | CMP-STA-43. Menu passes `TYPEAHEAD_RESET_MS` (500 ms, `menu/root/MenuRoot.tsx:561`); Select and Combobox pass nothing and get 750 ms. |
| `selectedIndex` | `number \| null` | `null` | Session base and reset gating (CMP-KEY-38, CMP-STA-44). |

Users: `menu/root/MenuRoot.tsx`, `select/root/SelectRoot.tsx`, `combobox/trigger/ComboboxTrigger.tsx`.

### 6.10 Keyed registry (`useItemRegistry()`) — `internals/useItemRegistry.ts:56-62`

Returns `{ items, registerItem, liveItems }` (CMP-STA-19). No options. Only consumer in v1.9.0: `filter-dropdown` (out of scope).

### 6.11 Constants

| Name | Value | Source |
|---|---|---|
| `ACTIVE_COMPOSITE_ITEM` | `'data-composite-item-active'` | `internals/composite/constants.ts:1` |
| `COMPOSITE_KEYS` | `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Home`, `End` | `internals/composite/composite.ts:22` |
| `MODIFIER_KEYS` | `Shift`, `Control`, `Alt`, `Meta` | `internals/composite/composite.ts:25` |
| `PAGE_UP`, `PAGE_DOWN` | `'PageUp'`, `'PageDown'` (declared, not handled by any mechanism here) | `internals/composite/composite.ts:19-20` |
| `FOCUSABLE_ATTRIBUTE` | `'data-base-ui-focusable'` | `floating-ui-react/utils/constants.ts:1` |
| `TYPEABLE_SELECTOR` | `input:not([type='hidden']):not([disabled]),[contenteditable]:not([contenteditable='false']),textarea:not([disabled])` | `floating-ui-react/utils/constants.ts:4-6` |
| Typeahead reset delay (hook default; Select, Combobox) | 750 ms | `floating-ui-react/hooks/useTypeahead.ts:81` |
| `TYPEAHEAD_RESET_MS` (Menu only) | 500 ms | `internals/constants.ts:3`; used at `menu/root/MenuRoot.tsx:561` |
| Popup grid default columns | 2 | `floating-ui-react/hooks/gridNavigation.ts:27` |

## 7. Edge cases and dev warnings

- **CMP-EDGE-01** — MUST. Reading the composite root shared state from a part that is not inside a composite root fails (in every build) with the error message `Base UI: CompositeRootContext is missing. Composite parts must be placed within <Composite.Root>.`; parts that read it optionally (`useButton`, menu trigger) get "absent" instead.
  Source: `internals/composite/root/CompositeRootContext.ts:21-31`, `internals/use-button/useButton.ts:25`, `menu/trigger/MenuTrigger.tsx:85`. Test: none.
- **CMP-EDGE-02** — SHOULD. In development builds, building a grid cell map with an item whose `width` exceeds `cols` throws `[Floating UI]: Invalid grid - item width at index ${index} is greater than grid columns` (`${index}` = the item's index). In production builds the placement loop never terminates for such an item, so a port MUST reject the configuration in every build (see §10 Q5).
  Source: `floating-ui-react/utils/composite.ts:439-447`. Test: none.
- **CMP-EDGE-03** — SHOULD. In development builds, list navigation with `allowEscape` and without `loopFocus` logs the warning `` `useListNavigation` looping must be enabled to allow escaping. ``.
  Source: `floating-ui-react/hooks/useListNavigation.ts:272-275`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); proven at L3 by `js/composite-list.test.ts` "port: warns where the options are set when allowEscape lacks loopFocus or virtual, and for a vertical grid": Base UI's text with the `Blazix: ` prefix, on the attach or the update that sets the options, as each Base UI render warns (`useListNavigation.ts:270-287`).
- **CMP-EDGE-04** — SHOULD. In development builds, list navigation with `allowEscape` and without `virtual` logs the warning `` `useListNavigation` must be virtual to allow escaping. ``.
  Source: `floating-ui-react/hooks/useListNavigation.ts:277-279`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept; proven at L3 by CMP-EDGE-03's port title.
- **CMP-EDGE-05** — SHOULD. In development builds, list navigation with `orientation: 'vertical'` and a grid navigator logs the warning `In grid list navigation mode, the `orientation` should be either "horizontal" or "both".` (emitted as two console arguments joined by a space).
  Source: `floating-ui-react/hooks/useListNavigation.ts:282-287`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept; proven at L3 by CMP-EDGE-03's port title, with the two console arguments Base UI passes.
- **CMP-EDGE-06** — MUST. When every composite item is disabled, the highlighted index stays in range (CMP-ALG-15 returns `0` at minimum) so the group regains a tab stop as soon as an item becomes enabled; min is then `length(elements)` and max `-1`, so <kbd>Home</kbd>/<kbd>End</kbd> and arrow keys change nothing.
  Source: `internals/composite/root/useCompositeRoot.ts:151-161,364`. Test: [L3] `CompositeRoot.test.tsx` "keeps the initial tab stop when all items are disabled", "keeps the tab stop in range when no item can hold it".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 04 Amendments 2026-10-22, chosen by the user: the highlighted index and the items' `tabindex` are JavaScript's from attach): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` through `attach` and `update`, observing the `tabindex` the module writes and its highlight report; each removal is what C# does, the render batch removing the element and then the root's `update` dropping its id.
- **CMP-EDGE-07** — MUST. With an empty elements array, list navigation computes min `0` and max `-1`: <kbd>Home</kbd> reports `onNavigate(0)` (no item exists, nothing is focused), <kbd>End</kbd> reports `null`, and arrow keys report `null`; focus stays where it was.
  Source: `floating-ui-react/utils/composite.ts:19-35,66-82`. Test: [L4] `test/popupListConformanceTests.tsx` "opens and handles keys without losing focus when the list is empty" (L4 for the focus clause and the popup-list suite row).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): the min `0` and max `-1` clause is green at L3 since M2 (`js/composite.test.ts` "returns out-of-bounds min and max when every item is disabled, and 0 and -1 for an empty list (CMP-ALG-05)"), and the list handle takes min and max from those helpers, so Home reports the highlight 0 with no item while End and the arrows report `null` (§10 Q7, replicated). Re-tagged [L3] → [L4] (the track's decision, ADR 13 §3): the cited title is the popup-list suite's, which the port runs at L4 (§4.14's port note); green at L4: "opens and handles keys without losing focus when the list is empty" (Menu, Select, Combobox), both modes, which presses the arrows and Enter on the empty list and keeps the focus. The min and max clause stays M2's L3 title above. No title presses Home or End on an empty list, in Base UI as here (§10 Q7).
- **CMP-EDGE-08** — MUST. The composite root ignores keys while its root element is not mounted.
  Source: `internals/composite/root/useCompositeRoot.ts:216-219`. Test: none.
- **CMP-EDGE-09** — MUST. When the composite highlighted index is consumer-owned, the "disabled indices changed" re-validation (CMP-STA-29) does not run; the consumer is responsible for keeping the index on an enabled item.
  Source: `internals/composite/root/useCompositeRoot.ts:173-179`. Test: [L3] port title "leaves the followed highlight on an item that a changed disabledIndices disables (CMP-EDGE-09)" (`src/Blazix.Primitives/js/composite.test.ts`).
  Port note (2026-10-27, M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26; ADR 13 §3): the consumer-owned highlight is the follow of CMP-STA-22's port note, so with `CompositeOptions.FollowActiveItem` the module's re-validation (CMP-STA-29) does not run. The row had no test; the port title asserts the follow first, then that a changed `disabledIndices` leaves the highlight where it is. The public-part proof is TabsList's (track 5).
  Port note (2026-10-27, M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `TabsList`: with the follow on, nothing re-validates the tab stop against a Tab that becomes disabled, so a disabled Tab selected by value leaves the tab stop where it was: `tests/browser/tabs.spec.ts` "does not set tabIndex=0 on disabled tabs when they are programmatically selected", both modes.
- **CMP-EDGE-10** — MUST. A typeahead session that starts with <kbd>Space</kbd> (empty buffer) is not consumed and leaves a single space in the buffer when nothing matches; the buffer is only cleared by the reset timer, blur, close, or a later non-space character that fails to match.
  Source: `floating-ui-react/hooks/useTypeahead.ts:132-142,156,200-203`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); replicated as the source behaves (ADR 12 Amendments 2026-11-03 (5), chosen by the user; §10 Q6), proven at L3 by `js/composite-typeahead.test.ts` "port: a typeahead session that starts with Space is not consumed and keeps the space in its buffer".
- **CMP-EDGE-11** — MUST. Typeahead treats a `null` label as never matching, and labels array entries beyond the elements array are matchable when no element exists for them.
  Source: `floating-ui-react/hooks/useTypeahead.ts:94-109,122`. Test: [L3] `CompositeList.test.tsx` "resolves each label source" (null label), `useTypeahead.test.tsx` "matches when focus is within reference" (no elements array).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite.test.ts` "resolves each label source" (the `null` label) and `js/composite-typeahead.test.ts` "matches when focus is within reference" (labels C# sends with no item rendered: `CompositeTypeaheadOptions.Labels`, Combobox's `items`).
- **CMP-EDGE-12** — MUST. Event targets, containment and ancestor matching are evaluated on the composed tree: the composite root reads the original target from the event's composed path (falling back to `target` after dispatch), and list navigation's "focus inside item/popup" checks and the grid's `[role="row"]` lookup cross shadow roots and slots.
  Source: `packages/utils/src/shadowDom.ts:3-82`, `internals/composite/root/useCompositeRoot.ts:228`, `floating-ui-react/utils/composite.ts:169`. Test: [L3] `CompositeRoot.test.tsx` "keeps native input behavior when the native target differs from the synthetic target", `CompositeList.test.tsx` "registers items that sit across a shadow boundary".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 12 Amendments 2026-10-22 item 3): the composite root's composed target and the registry's shadow clause run at L3; list navigation's and the grid's clauses are M5 or deferred.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): the list-navigation and grid clauses are built on `dom`'s composed-tree helpers (`getTarget`, `contains`, and `closest` for the grid's `[role="row"]` lookup) and resolve items through `composedPath()`. The helpers' shadow-root titles are UTL's (`js/dom.test.ts` "finds an ancestor across nested shadow roots from a non-Element node", "follows the composed tree through slots", "uses the native check and then walks out of shadow roots", "returns the composed-path target while the event is being dispatched"). Green at L3 (PR 170's review, fix 3): `src/Blazix.Primitives/js/composite-list.test.ts` "list navigation inside a shadow root (port)": "port: inside a shadow root the list resolves its items by id, opens on ArrowDown and moves the focus and the highlight", "port: inside a shadow root the main key with the focus on the popup itself starts at the boundary item", "port: inside a shadow root the grid moves vertically along its [role=\"row\"] rows", "port: the grid finds an item's [role=\"row\"] through its assigned slot, inside the row host's shadow root".
- **CMP-EDGE-13** — MUST. When mutation observation is unavailable in the host environment, the item registry still registers and orders items; only out-of-band DOM moves go undetected.
  Source: `internals/composite/list/CompositeList.tsx:94-97`. Test: none.
- **CMP-EDGE-14** — MUST. The registry never publishes an empty map while its subtree is temporarily withheld by an asynchronous boundary and then restored: after each restoration the elements array, labels array and item map contain all items in order.
  Source: `internals/composite/list/CompositeList.tsx:173-201`. Test: [L3] `CompositeList.test.tsx` "does not publish an empty registry when an outer boundary repeatedly suspends".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 01 decision 2, the registry is JavaScript's): re-tagged [L2] → [L3] under ADR 13 §3. The titles run in `src/Blazix.Primitives/js/composite.test.ts` against the module's registry, `createCompositeRegistry`, which is keyed by element and carries explicit indexes and metadata on the JavaScript side only; C# registers every item automatically, by id (ADR 04 Amendments 2026-10-22). Restated (dated, 2026-10-22): Blazor has no Suspense; the title runs over a subtree withheld from the document and restored, twice, while its items stay registered: the registry rebuilds only on an `update` or a detected move, so nothing is published while the items are away and every restoration finds all items in order.
- **CMP-EDGE-15** — MUST. A consumer-rendered `activeIndex` that is out of bounds is ignored by the cursor re-synchronization (CMP-STA-32) and by the item focus synchronization (CMP-STA-35).
  Source: `floating-ui-react/hooks/useListNavigation.ts:478,591-597`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); proven at L3 by `js/composite-list.test.ts` "port: the next key resynchronizes the cursor to a C# highlight it could not follow while the highlight was out of bounds" and "port: a C# highlight request out of bounds moves neither the focus nor the cursor, while one in bounds does" (in the port a consumer renders an out-of-bounds index as a C#-origin highlight, CMP-STA-32).
- **CMP-EDGE-16** — MUST. In grid navigation, a cursor of `-1` (nothing highlighted) with a vertical key moves to max for <kbd>ArrowUp</kbd> and min for <kbd>ArrowDown</kbd>; horizontal keys with cursor `-1` compute `-1 mod cols = -1`, which never equals `cols - 1` or `0`, so they step from `-1` (i.e. to the first enabled index for the forward key, and to an out-of-bounds index — then "unchanged" — for the backward key).
  Source: `floating-ui-react/utils/composite.ts:290-291,336-409`. Test: [L3] `useListNavigation.test.tsx` "ArrowDown focuses first item" (grid, pointer open then <kbd>ArrowDown</kbd> → first enabled item 8).
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): green at L3: `js/composite-list.test.ts` "ArrowDown focuses first item" (a pointer open on the `Grid.tsx` DOM, then ArrowDown lands on the first enabled item, 8).
- **CMP-EDGE-17** — MUST. The composite grid navigator is given `highlightedIndex > maxIndex ? minIndex : highlightedIndex` as the origin, so a stale highlighted index beyond the last enabled item navigates from the first enabled item.
  Source: `internals/composite/root/gridNavigation.ts:107`, `floating-ui-react/hooks/gridNavigation.ts:39`. Test: none.
  Port note (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(3), chosen by the user): `Test: none` kept (Base UI has no title); the popup grid's clause is proven at L3 by `js/composite-list.test.ts` "port: the grid navigator moves from the first enabled item when the cursor lies past the last enabled one"; the composite grid's clause stays deferred (§10 item 1).
- **CMP-EDGE-18** — MUST. The composite `stopEventPropagation`/`preventDefault` behavior applies only when the index changes (CMP-KEY-10/11); a composite therefore never swallows arrow keys it cannot act on, which lets an enclosing composite or page handle them.
  Source: `internals/composite/root/useCompositeRoot.ts:302-309`. Test: [L4] `CompositeRoot.test.tsx` "sets default prevention to $prevented for $key with $orientation orientation".
  Port note (2026-10-22, M2 track 3, issue 79; ADR 13 Amendments 2026-10-22): the six variants run at L4 on the `composite-list` fixture, both modes; a key the root cannot act on reaches a listener on the document.
  Port note (2026-10-23, M2 track 8, issue 84; ADR 13 Amendments 2026-10-22, the internal root is incidental): also proven through the public `RadioGroup` by `tests/browser/radio-group.spec.ts` "port: a handled arrow is default-prevented and stopped, and an arrow with no other enabled radio is neither": the ignored arrow reaches a listener on the document, both modes.

## 8. React-specific notes and porting considerations

Non-normative.

* **Registration through DOM-handle callbacks.** Items register in the callback that receives their element (a React callback ref), and the callback's identity changes with `index`, `label`, `textRef`, `metadata` so that React detaches and reattaches it (unregister + register) when those change. Ports should model this as "re-register when registration data changes" and keep the rule that the *last* registration for an element wins (CMP-STR-04): the outer item's handle is placed first in the handle list and therefore attaches last. Port: see decisions/04-js-interop-boundary.md §1 (`composite` module: registry move detection is JavaScript).
* **Post-commit, pre-paint timing.** The registry flush, the composite default-index adjustment and all list-navigation synchronizations run in layout effects. In a port they must run after the DOM for a state change is in place and before paint, and in a single pass per change batch (CMP-STA-10). Port: see decisions/01-hosting-model-and-timing.md §Relaxation table (CMP-STA-10: on Server the C# state work lands after paint plus a round trip) and §Decision item 2 (constraint C1).
* **Development double-invocation (StrictMode).** Several tests run under StrictMode, which replays effects (and not callback refs). The registry marks itself dirty on effect teardown so the replay rebuilds (`CompositeList.tsx:194-201`), and the keyed registry resets its latch in an effect. A port without this behavior simply has nothing to replay; the observable requirements are unchanged.
* **Stable callbacks and "latest value" handles.** `useStableCallback`/`useValueAsRef` give handlers a fixed identity that always reads the latest options (`open`, `disabledIndices`, `selectedIndex`, `resetOnPointerLeave`). Handlers in a port must read current option values at event time, not captured ones; CMP-KEY-22 explicitly reads the open state "at event time" (`store.select('open')`). Port: see decisions/04-js-interop-boundary.md §3 (the JS handlers read options pushed through `update(options)`).
* **Popup store.** `store.useState('open' | 'floatingElement' | 'domReferenceElement')`, `store.setOpen(open, details)` and `store.context.dataRef` belong to the floating root store ([floating-and-positioning](floating-and-positioning.md)). `dataRef.current.orientation` is the field CMP-STA-41 publishes; `useFloatingTree`/`useFloatingParentNodeId` (`floating-ui-react/components/FloatingTree.tsx:17-23`) give access to the parent popup's floating element and data. Port: see decisions/05-state-store-and-event-details.md §1.
* **Synthetic vs native events.** Handlers receive React synthetic events; `event.nativeEvent` is what event details carry (CMP-EVT-06/07/08), `event.which` is the legacy key code (CMP-KEY-25), and `getTarget` reads `composedPath()[0]` of the native event. The composite's `relayKeyboardEvent` expects the same synthetic shape. Port: see decisions/04-js-interop-boundary.md §3 (`details.event` becomes a snapshot record).
* **Animation frames.** `useAnimationFrame` wraps a process-wide frame scheduler that collapses multiple requests into one native `requestAnimationFrame` per frame; `enqueueFocus` keeps its own single pending native frame id. The observable contract is CMP-ALG-23/31 ("next frame", "a later request supersedes an earlier one"). Port: see decisions/01-hosting-model-and-timing.md §Decision item 3 (constraint C2: frames and microtasks are JavaScript only).
* **`item &&` in the focus procedure.** `focusItem` references the `item` props object declared later in the same function (`useListNavigation.ts:364`); at call time it is always defined, so the condition reduces to `forced || !pointerModality` (CMP-FOC-04).
* **`ElementProps` merging.** The four handler groups are merged onto elements by the consumer's prop-merging (`useInteractions`/`mergeProps`); in tests `useTestInteractions` does the same. The *reference* group spreads `trigger` after `aria-activedescendant`, so a component that renders a separate trigger spreads `trigger` on it and `reference` (without `trigger`) is not available separately — components choose which group each element gets. Port: see decisions/02-render-pipeline-and-attribute-merging.md §Decision item 3 (handler chaining and the `preventBaseUIHandler` veto); for consumer vetoes against composite key handling (EVD-EVT-26) see decisions/04-js-interop-boundary.md §3 (`decide()` with fallback policy "run": on Server the composite handles the key regardless; ADR 01 §Relaxation table).
* **`process.env.NODE_ENV !== 'production'`** gates CMP-EDGE-02 to CMP-EDGE-05.
* **`MutationObserver`** is used only for move detection (CMP-STA-11); a port may use any mechanism that detects reordering of registered elements after the fact, as long as additions/removals alone and unrelated mutations do not trigger a publication. Port: see decisions/04-js-interop-boundary.md §1.
* **Language placement.** Roving focus, list and grid navigation, typeahead, registry move detection and per-key `preventDefault` are one JavaScript loop end to end (they read native `disabled`/`aria-disabled` and visibility, call `focus()`, and decide `preventDefault` inside the dispatch); C# receives active-index and highlight changes (`onNavigate`, `onHighlightedIndexChange`, `onMatch`, `onTyping`) one message per change. Port: see decisions/04-js-interop-boundary.md §1 and decisions/01-hosting-model-and-timing.md §Decision item 2 (constraint C1).
* **Test layers.** The unit tests of this mechanism (`composite.test.ts`, `CompositeRoot.test.tsx`, `CompositeList.test.tsx`, `useListNavigation*.test.tsx`, `useTypeahead.test.tsx`) translate to module tests of the JavaScript `composite` module against a fake twin; the popup-list suite and other cross-boundary keyboard/focus rows run in the browser per render mode. Port: see decisions/06-testing-harness.md §1 (L3, L4).

## 9. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| CMP-API-01 | MUST | When `loopFocus` is `true`, moving forward from the last navigable item wraps to the first navigable item and moving … | §3 |
| CMP-API-02 | MUST | `orientation` selects which arrow keys move the highlight: `'vertical'` → <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>; … | §3 |
| CMP-API-03 | MUST | When `highlightItemOnHover` is `true`, moving the pointer over an item makes it the highlighted/active item (composit… | §3 |
| CMP-STR-01 | MUST | A composite group consists of exactly one composite root element and any number of composite items rendered anywhere … | §4.1 |
| CMP-STR-02 | MUST | A composite item rendered without a composite root ancestor is an error: the implementation MUST fail with the messag… | §4.1 |
| CMP-STR-03 | MUST | An item registration that has no enclosing item registry is inert: it resolves to index `-1`, registers nothing, and … | §4.1 |
| CMP-STR-04 | MUST | Two registrations MAY target the same DOM element (an outer composite item whose element substitution is itself a com… | §4.1 |
| CMP-STR-05 | MUST | The composite root renders a `div` by default; the element tag is configurable (`tag`) and element substitution / `cl… | §4.1 |
| CMP-STR-06 | MUST | List navigation emits attributes and handlers for four element roles — *reference*, *floating*, *item* and *trigger… | §4.1 |
| CMP-STR-07 | MUST | Typeahead emits the same two handlers (`keydown`, `blur`) for both the *reference* and the *floating* roles, and noth… | §4.1 |
| CMP-STA-01 | MUST | Each registered item resolves to a non-negative integer index. Items registered with an explicit `index ≥ 0` occupy… | §4.2.1 |
| CMP-STA-02 | MUST | Document order for automatic items is the DOM tree order of their elements (an element that precedes another in tree … | §4.2.1 |
| CMP-STA-03 | MUST | Registrations whose element is not connected to the document at rebuild time are excluded from the item list (they ge… | §4.2.1 |
| CMP-STA-04 | MUST | A registration with an explicit negative index is ignored: it gets no slot in the elements array and no entry in the … | §4.2.1 |
| CMP-STA-05 | MUST | After each rebuild the elements array contains exactly the registered connected elements at their indexes (length = h… | §4.2.1 |
| CMP-STA-06 | MUST | The label of an item is resolved, in order: the registration's explicit `label` when it is not `undefined` (an explic… | §4.2.1 |
| CMP-STA-07 | MUST | Labels are re-resolved on every rebuild, so a label change on a mounted item (which re-registers it) updates the labe… | §4.2.1 |
| CMP-STA-08 | MUST | The published item map is ordered by index and maps each element to its metadata object merged with `{ index }`; item… | §4.2.1 |
| CMP-STA-09 | MUST | The item map is published (to item subscribers and to `onMapChange`) only when the rebuilt list differs from the prev… | §4.2.1 |
| CMP-STA-10 | MUST | Registrations and unregistrations that happen in the same render batch (including from nested state updates deep in t… | §4.2.1 |
| CMP-STA-11 | MUST | When registered elements are *moved* in the DOM outside the framework's control (a node is removed and re-inserted wi… | §4.2.1 |
| CMP-STA-12 | SHOULD | Move detection observes child-list mutations on the lowest common ancestor of each pair of adjacent automatic items (… | §4.2.1 |
| CMP-STA-13 | MUST | Items in a shadow tree (no common ancestor with the light-DOM items) still register and receive indexes; the absence … | §4.2.1 |
| CMP-STA-14 | MUST | When the registry is torn down, the elements array and labels array are emptied. | §4.2.1 |
| CMP-STA-15 | MUST | An item's index before the first publication is `-1` (this is the value present in server-rendered output); after the… | §4.2.1 |
| CMP-STA-16 | MAY | An automatic item MAY opt in to *guessing* its index from the order in which items first render (`guess: true`): the… | §4.2.1 |
| CMP-STA-17 | MUST | Changing an item's explicit index, or removing it (switching to automatic), re-registers the item at the new slot and… | §4.2.1 |
| CMP-STA-18 | MUST | An automatic item whose index shifts because of other items (insertions before it) keeps its registration; only its p… | §4.2.1 |
| CMP-STA-19 | MUST | The keyed registry exposes `registerItem(key, item)` returning an unregister function, an immutable `items` snapshot,… | §4.2.2 |
| CMP-STA-20 | MUST | The registry tolerates register → unregister → register sequences of the same key within one render batch: the pu… | §4.2.2 |
| CMP-STA-21 | MUST | When the last key unregisters, an empty snapshot is published. | §4.2.2 |
| CMP-STA-22 | MUST | The highlighted index is either owned by the consumer (`highlightedIndex` + `onHighlightedIndexChange`) or by the roo… | §4.2.3 |
| CMP-STA-23 | MUST | On the first non-empty publication of the item map, if some item element carries the active item marker (`data-compos… | §4.2.3 |
| CMP-STA-24 | MUST | On the first non-empty publication, if no item carries the marker and the current highlighted index is disabled (CMP-… | §4.2.3 |
| CMP-STA-25 | MUST | On the first non-empty publication the marked active item (if any) is scrolled into view within the root (CMP-ALG-19). | §4.2.3 |
| CMP-STA-26 | MUST | On every later publication the root re-locates the element that held the highlight: if it is still present at a diffe… | §4.2.3 |
| CMP-STA-27 | MUST | When the previously highlighted element is gone: if another, non-disabled element now occupies the same index it keep… | §4.2.3 |
| CMP-STA-28 | MUST | Publications of an empty item map are ignored (no index change). | §4.2.3 |
| CMP-STA-29 | MUST | When `disabledIndices` is provided and changes after the first publication, and the highlight is owned by the root (n… | §4.2.3 |
| CMP-STA-30 | MUST | Setting the highlighted index from a keyboard move also scrolls the new item into view within the root (CMP-ALG-19); … | §4.2.3 |
| CMP-STA-31 | MUST | The active index is owned by the consumer: the mechanism reads `activeIndex` (`number \| null`) and proposes every ch… | §4.2.4 |
| CMP-STA-32 | MUST | If the consumer declines a proposed change (renders an `activeIndex` different from the cursor), the next key press r… | §4.2.4 |
| CMP-STA-33 | MUST | When the popup becomes open with its floating element mounted, the cursor is set to `selectedIndex ?? -1`; if the eff… | §4.2.4 |
| CMP-STA-34 | MUST | When the popup closes or its floating element unmounts after having been mounted, the cursor becomes `-1` and `onNavi… | §4.2.4 |
| CMP-STA-35 | MUST | While open with the floating element mounted, whenever `activeIndex` is a non-null in-bounds index, the cursor is set… | §4.2.4 |
| CMP-STA-36 | MUST | While open, when `activeIndex` becomes `null` and `selectedIndex` is `null` and the floating element was already moun… | §4.2.4 |
| CMP-STA-37 | MUST | *Initial sync on open.* When the popup has just opened (or its floating element has just mounted) with `activeIndex =… | §4.2.4 |
| CMP-STA-38 | MUST | The recorded trigger key is cleared whenever the popup is closed. | §4.2.4 |
| CMP-STA-39 | MUST | *Effective focus-on-open flag.* It is `focusItemOnOpen` (`true`, `false` or `'auto'`) whenever the popup is closed or… | §4.2.4 |
| CMP-STA-40 | MUST | The `selectedIndex` changing while open MUST NOT move focus away from the reference element (the mechanism only re-de… | §4.2.4 |
| CMP-STA-41 | MUST | The mechanism publishes its `orientation` into the popup's shared context data so nested popups can read their parent… | §4.2.4 |
| CMP-STA-42 | MUST | Typeahead keeps a buffer string (initially empty), a *previous index* (initially `selectedIndex ?? activeIndex ?? -1`… | §4.2.5 |
| CMP-STA-43 | MUST | The reset timer fires `resetMs` (default 750 ms) after the most recent accepted character; it empties the buffer, set… | §4.2.5 |
| CMP-STA-44 | MUST | Whenever `open` or `selectedIndex` changes, unless then closed with a non-null `selectedIndex`, the timer is cleared… | §4.2.5 |
| CMP-STA-45 | MUST | When focus leaves both the reference element and the floating element (blur whose related target is inside neither), … | §4.2.5 |
| CMP-DOM-01 | MUST | Every composite item renders `tabindex="0"` when its index equals the highlighted index and `tabindex="-1"` otherwise… | §4.3 |
| CMP-DOM-02 | MUST | The active item marker is the attribute `data-composite-item-active` (value `""`) on an item element. Components set … | §4.3 |
| CMP-DOM-03 | MUST | The composite root MUST NOT add `aria-orientation` (or any other attribute) on its own; components add role and orien… | §4.3 |
| CMP-DOM-04 | MUST | List navigation MUST NOT add `aria-orientation` to the floating element. | §4.3 |
| CMP-DOM-05 | MUST | The popup focus element is identified by the attribute `data-base-ui-focusable` (`FOCUSABLE_ATTRIBUTE`): the floating… | §4.3 |
| CMP-ARIA-01 | MUST | Under virtual focus, while the popup is open and `activeIndex` is not `null`, the *reference* element carries `aria-a… | §4.4 |
| CMP-ARIA-02 | MUST | The *floating* element carries the same `aria-activedescendant` attribute under the same conditions, except when the … | §4.4 |
| CMP-ARIA-03 | MUST | Disabled detection reads `aria-disabled="true"` (and the `disabled` attribute) from item elements only when `disabled… | §4.4 |
| CMP-KEY-01 | MUST | The composite root handles only the keys `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Home`, `End`; `Home`/`En… | §4.5 |
| CMP-KEY-02 | MUST | A handled key is ignored when any of `Shift`, `Control`, `Alt`, `Meta` is held, except modifiers listed in `modifierK… | §4.5 |
| CMP-KEY-03 | MUST | The forward/backward mapping is: horizontal forward = `ArrowRight` (LTR) / `ArrowLeft` (RTL), horizontal backward = t… | §4.5 |
| CMP-KEY-04 | MUST | When the event's original target (first element of the composed path) is an enabled native text input (an `input` exp… | §4.5 |
| CMP-KEY-05 | MUST | <kbd>Home</kbd> sets the candidate index to min and <kbd>End</kbd> to max (when enabled), regardless of orientation a… | §4.5 |
| CMP-KEY-06 | MUST | A forward key when the candidate index is still the current index and equals max and `loopFocus` is `true` sets the c… | §4.5 |
| CMP-KEY-07 | MUST | A backward key when the candidate index is still the current index and equals min and `loopFocus` is `true` sets the … | §4.5 |
| CMP-KEY-08 | MUST | Otherwise a forward/backward key whose candidate index is still the current index sets the candidate to the adjacent … | §4.5 |
| CMP-KEY-09 | MUST | When `loopFocus` is `false` and the step leaves the list (candidate out of bounds), nothing happens and the loop hook… | §4.5 |
| CMP-KEY-10 | MUST | When the candidate index differs from the current index and is in bounds: the event's propagation is stopped if `stop… | §4.5 |
| CMP-KEY-11 | MUST | When the candidate index equals the current index (key outside the orientation, end reached without looping, loop hoo… | §4.5 |
| CMP-KEY-12 | MUST | When `grid` is configured, the navigator (CMP-ALG-12) is consulted first with the current index, min, max, orientatio… | §4.5 |
| CMP-KEY-13 | MUST | The loop hook receives `(event, previousIndex, proposedIndex, elementsArray)` and its return value replaces the propo… | §4.5 |
| CMP-KEY-14 | MUST | The composite root exposes a keyboard relay function (through its ancestor-provided shared state) that runs the same … | §4.5 |
| CMP-KEY-15 | MUST | `stopEventPropagation` defaults to `true` for the composite root part and to `false` (unset) for the bare root mechan… | §4.5 |
| CMP-KEY-16 | MUST | When the key moves the highlight, the previously focused item loses focus and the new item gains it; the focus event … | §4.5 |
| CMP-KEY-17 | MUST | Focusing (via the root's `focus` handler, i.e. focus arriving anywhere inside the root) a native text input selects i… | §4.5 |
| CMP-KEY-18 | MUST | Any `keydown` on the trigger/reference or the floating element switches the input modality to keyboard (CMP-STA-39 is… | §4.6 |
| CMP-KEY-19 | MUST | While the popup is closed and `openOnArrowKeyDown` is `false`, any key whose name starts with `Arrow` is ignored enti… | §4.6 |
| CMP-KEY-20 | MUST | Under virtual focus, while the popup is open, a trigger `keydown` is processed by the floating handling (Table C) whe… | §4.6 |
| CMP-KEY-21 | MUST | A trigger `keydown` whose key is in MAIN(`orientation` when open, else `triggerOrientation`) or is <kbd>Enter</kbd> o… | §4.6 |
| CMP-KEY-22 | MUST | For a non-nested list, a trigger `keydown` in MAIN(o) (o as in CMP-KEY-21): sets the cursor to `selectedIndex` when n… | §4.6 |
| CMP-KEY-23 | MUST | For a nested list, a trigger `keydown` in XOPEN(parent orientation) is consumed; when the popup is open the cursor be… | §4.6 |
| CMP-KEY-24 | MUST | A `keydown` of <kbd>Tab</kbd> with <kbd>Shift</kbd> on the floating element while open under DOM focus, whose origina… | §4.6 |
| CMP-KEY-25 | MUST | The floating handling ignores `keydown` events whose legacy key code is 229 (in-progress IME composition). | §4.6 |
| CMP-KEY-26 | MUST | The floating handling ignores `keydown` events while the popup is closed when the current target is the popup focus e… | §4.6 |
| CMP-KEY-27 | MUST | For a nested list, a key in XCLOSE(orientation, direction, grid) closes the nested popup with reason `list-navigation… | §4.6 |
| CMP-KEY-28 | MUST | Unless the reference is a typeable combobox reference, <kbd>Home</kbd> moves the cursor to min and <kbd>End</kbd> to … | §4.6 |
| CMP-KEY-29 | MUST | When grid navigation is configured, the navigator (CMP-ALG-13) runs before the main-key rule with the cursor, orienta… | §4.6 |
| CMP-KEY-30 | MUST | A key in MAIN(orientation) on the floating element is always consumed (default prevented, propagation stopped), inclu… | §4.6 |
| CMP-KEY-31 | MUST | Under DOM focus, a MAIN key pressed while focus is inside the current target but on no item (e.g. on the popup contai… | §4.6 |
| CMP-KEY-32 | MUST | Otherwise a MAIN key moves the cursor to the result of CMP-ALG-06 (`decrement` = key not in TOEND(orientation)) and e… | §4.6 |
| CMP-KEY-33 | MUST | With `allowEscape` (requires `loopFocus` and virtual focus), a MAIN key at the first/last item moves the cursor to `-… | §4.6 |
| CMP-KEY-34 | MUST | Keys not listed in Tables A–D are not intercepted by list navigation: <kbd>Tab</kbd> moves focus natively (and dism… | §4.6 |
| CMP-PTR-01 | MUST | When the composite root has `highlightItemOnHover` (default `false`), a `mousemove` over a composite item that is not… | §4.7 |
| CMP-PTR-02 | MUST | A composite item receiving focus (from any source: keyboard, pointer, programmatic) sets the highlighted index to its… | §4.7 |
| CMP-PTR-03 | MUST | Under DOM focus, a `click` on a list item focuses the item without scrolling (WebKit does not focus buttons on click)… | §4.7 |
| CMP-PTR-04 | MUST | A `mousemove` on a list item (that is not a stationary WebKit event, CMP-PTR-08) marks subsequent focus as synchronou… | §4.7 |
| CMP-PTR-05 | MUST | A `pointerleave` on a list item is ignored when the popup is closed, when the input modality is keyboard, or when the… | §4.7 |
| CMP-PTR-06 | MUST | A `pointermove` on the floating element (that is not a stationary WebKit event) switches the input modality to pointer. | §4.7 |
| CMP-PTR-07 | MUST | Trigger `pointerdown`, `pointerenter`, `mousedown` and `click` feed the focus-on-open resolution of CMP-STA-39 and ha… | §4.7 |
| CMP-PTR-08 | MUST | On WebKit engines (Safari, all iOS browsers; detected as support for `-webkit-backdrop-filter`), `mousemove`/`pointer… | §4.7 |
| CMP-FOC-01 | MUST | A composite group occupies exactly one position in the sequential tab order: tabbing into it lands on the item with `… | §4.8 |
| CMP-FOC-02 | MUST | After a composite key move, DOM focus moves to the new highlighted item one microtask after the keydown (CMP-KEY-10),… | §4.8 |
| CMP-FOC-03 | MUST | *List navigation focus procedure* (CMP-ALG-23). Under DOM focus, whenever the cursor is applied (CMP-STA-35, CMP-KEY-… | §4.8 |
| CMP-FOC-04 | MUST | In the same procedure, after focusing (synchronously if the flag is set, else on the next frame), the item is scrolle… | §4.8 |
| CMP-FOC-05 | MUST | Under virtual focus the procedure never moves DOM focus (scroll-into-view still applies). | §4.8 |
| CMP-FOC-06 | MUST | Under DOM focus, when the reference element itself receives focus (the event target is the reference, not a descendan… | §4.8 |
| CMP-FOC-07 | MUST | A list item receiving focus marks subsequent focus as synchronous and synchronizes the cursor/active index to that it… | §4.8 |
| CMP-FOC-08 | MUST | When a nested popup's floating element unmounts (it closes) under DOM focus with a popup tree available, and the inpu… | §4.8 |
| CMP-FOC-09 | MUST | The initial sync on open (CMP-STA-37) checks for the first list element immediately, then after a microtask, then aft… | §4.8 |
| CMP-KEY-35 | MUST | A `keydown` is accepted by typeahead only when the labels array exists, the key name has length 1, and none of <kbd>C… | §4.9 |
| CMP-KEY-36 | MUST | Before the acceptance check, if the buffer is non-empty and the key is <kbd>Space</kbd>, the event is consumed and `o… | §4.9 |
| CMP-KEY-37 | MUST | When the popup is open and the accepted key is not <kbd>Space</kbd>, the event is consumed and `onTyping(true)` emitted. | §4.9 |
| CMP-KEY-38 | MUST | A *session* starts when the buffer is empty at acceptance; its base index is `selectedIndex ?? activeIndex ?? -1`. A … | §4.9 |
| CMP-KEY-39 | MUST | *Rapid same-letter cycling.* If every available label has different first and second characters (case-insensitive) an… | §4.9 |
| CMP-KEY-40 | MUST | After appending the key and restarting the reset timer (CMP-STA-43), the match is the first available index, scanning… | §4.9 |
| CMP-KEY-41 | MUST | On a match, `onMatch(index, event)` is emitted and the match index recorded. On no match, unless the key is <kbd>Spac… | §4.9 |
| CMP-KEY-42 | MUST | An item is unavailable for matching when its element (from `elementsRef`, when provided) is not visible (CMP-ALG-03) … | §4.9 |
| CMP-KEY-43 | MUST | The match callback receives the originating `keydown` event as its second argument. | §4.9 |
| CMP-I18N-01 | MUST | The composite root part resolves its direction from the nearest ancestor direction provider, defaulting to `'ltr'`; t… | §4.10 |
| CMP-I18N-02 | MUST | Under RTL, the composite's horizontal forward key is <kbd>ArrowLeft</kbd> and the horizontal backward key is <kbd>Arr… | §4.10 |
| CMP-I18N-03 | MUST | Direction does not affect <kbd>ArrowUp</kbd>/<kbd>ArrowDown</kbd>, <kbd>Home</kbd>/<kbd>End</kbd>, or the vertical ax… | §4.10 |
| CMP-I18N-04 | MUST | In list navigation, `rtl: true` swaps <kbd>ArrowLeft</kbd> and <kbd>ArrowRight</kbd> in the TOEND, XOPEN and XCLOSE p… | §4.10 |
| CMP-I18N-05 | MUST | In composite grid navigation under RTL, the horizontal forward key (<kbd>ArrowLeft</kbd>) moves to the next cell in t… | §4.10 |
| CMP-I18N-06 | MUST | When scrolling a composite item into view horizontally (CMP-ALG-19), RTL checks "overflow to the left" (start edge) b… | §4.10 |
| CMP-I18N-07 | MUST | Typeahead compares labels using locale-insensitive lower-casing (the result for `"I"` is `"i"` regardless of the user… | §4.10 |
| CMP-DIS-01 | MUST | A nested list closes itself (reason `list-navigation`) on its cross-orientation close key, returning focus to the `ne… | §4.11 |
| CMP-DIS-02 | MUST | A non-nested popup closes on <kbd>Shift</kbd>+<kbd>Tab</kbd> from inside its popup focus element (reason `focus-out`,… | §4.11 |
| CMP-EVT-01 | MUST | Composite `onHighlightedIndexChange(index: number)` is invoked with the new index whenever the root moves the highlig… | §4.12 |
| CMP-EVT-02 | MUST | Composite `onLoop(event, prevIndex, nextIndex, elementsArray) => number` is invoked only when a step wraps; its return value is the destination | §4.12 |
| CMP-EVT-03 | MUST | Composite root `onMapChange(map)` is invoked with the item map on every publication (CMP-STA-09), before the root's o… | §4.12 |
| CMP-EVT-04 | MUST | List navigation `onNavigate(index: number \| null, event: Event \| undefined)` is invoked with `null` when the cursor… | §4.12 |
| CMP-EVT-05 | MUST | A MAIN key on the trigger while the popup is open under DOM focus produces two `onNavigate` calls with the same index… | §4.12 |
| CMP-EVT-06 | MUST | Opening from a navigation key (Table A, Table D) calls the popup's open-change with `open = true` and event details `… | §4.12 |
| CMP-EVT-07 | MUST | The nested close (CMP-DIS-01) calls open-change with `{ reason: 'list-navigation', event: <native keydown> }` and no … | §4.12 |
| CMP-EVT-08 | MUST | The <kbd>Shift</kbd>+<kbd>Tab</kbd> close (CMP-DIS-02) calls open-change with `{ reason: 'focus-out', event: <native … | §4.12 |
| CMP-EVT-09 | MUST | `getHighlightReason(event)` maps an optional event to a highlight reason: `undefined`/`null` → `'none'`; an event w… | §4.12 |
| CMP-EVT-10 | MUST | Typeahead `onMatch(index, keydownEvent)` is invoked once per accepted character that produces a match (CMP-KEY-41). | §4.12 |
| CMP-EVT-11 | MUST | Typeahead `onTyping(isTyping)` is invoked with `true` when a character is accepted while open (or a space continues a… | §4.12 |
| CMP-EVT-12 | MUST | Composite `onKeyDown` supplied by the consumer runs in addition to (merged with) the root's key handling through the … | §4.12 |
| CMP-PTR-09 | MUST | After a pointer open, hovering item 2 highlights it and puts focus on it. | §4.14 |
| CMP-PTR-10 | MUST | Hovering item 1 then moving the pointer to the "After" button outside the list clears the highlight (no `data-highlig… | §4.14 |
| CMP-PTR-11 | MUST | After keyboard navigation to item 3, hovering item 1 moves the highlight and focus to item 1. | §4.14 |
| CMP-PTR-12 | MUST | A `pointerdown` → `pointercancel` → `pointerleave` sequence with `pointerType: 'touch'` on the highlighted item (… | §4.14 |
| CMP-PTR-13 | MUST | An item rendered disabled has `aria-disabled="true"`; hovering it and pressing <kbd>Enter</kbd>, or clicking it, invo… | §4.14 |
| CMP-PTR-14 | MUST | A right-button press on the trigger does not open the popup; a right-button press on an item of an open popup neither… | §4.14 |
| CMP-PTR-15 | MUST | (Real browser) With the pointer resting over item 1 after hovering it, keyboard navigation from item 1 to item 9 — … | §4.14 |
| CMP-PTR-16 | MUST | (Real browser) Hovering a partially visible item at the bottom edge (item 3, 10 px visible) or at the top edge (item … | §4.14 |
| CMP-FOC-10 | MUST | After a pointer open nothing is highlighted and focus is on the popup (DOM suites) or on the trigger (virtual); the f… | §4.14 |
| CMP-KEY-44 | MUST | With the trigger focused and the popup closed, <kbd>ArrowDown</kbd> opens the popup with item 0 highlighted and focus… | §4.14 |
| CMP-KEY-45 | MUST | When item 0 is disabled, opening with <kbd>ArrowDown</kbd> highlights item 1. | §4.14 |
| CMP-KEY-46 | MUST | (`enterSpaceOpen`, i.e. Menu and Select) <kbd>Enter</kbd> or <kbd>Space</kbd> on the focused trigger opens the popup … | §4.14 |
| CMP-STA-46 | MUST | After navigating to item 7 and closing with <kbd>Escape</kbd>, a pointer reopen starts with no highlight. | §4.14 |
| CMP-STA-47 | MUST | (`selectable`) With "Item 8" as the default value, opening with <kbd>ArrowDown</kbd> or <kbd>ArrowUp</kbd> highlights… | §4.14 |
| CMP-KEY-47 | MUST | After a keyboard open at index *s*, <kbd>ArrowDown</kbd> highlights *s*+1 and moves focus to it; <kbd>ArrowUp</kbd> r… | §4.14 |
| CMP-KEY-48 | MUST | After a pointer open and hovering item 4, <kbd>ArrowDown</kbd> highlights item 5 with focus on it; and <kbd>Enter</kb… | §4.14 |
| CMP-KEY-49 | MUST | <kbd>Enter</kbd> (and <kbd>Space</kbd> unless `spaceActivates: false`) on the highlighted item *s*+1 activates it exa… | §4.14 |
| CMP-KEY-50 | MUST | Inside a `<form>` with a submit button, opening/closing with <kbd>Escape</kbd>, opening with <kbd>Enter</kbd> (where … | §4.14 |
| CMP-KEY-51 | MUST | Arrow navigation does not skip a disabled item: with item 2 disabled, <kbd>ArrowDown</kbd> from item 1 highlights ite… | §4.14 |
| CMP-KEY-52 | MUST | (`homeEnd`, i.e. Menu and Select) <kbd>End</kbd> highlights the last item and <kbd>Home</kbd> the first, even when th… | §4.14 |
| CMP-FOC-11 | MUST | (Real browser) Keyboard navigation scrolls the highlighted item fully into view in both directions and only as far as… | §4.14 |
| CMP-KEY-53 | MUST | (`typeahead`, i.e. Menu and Select) Typing "Item 9" after a keyboard open highlights item 9 and (real browser) reveal… | §4.14 |
| CMP-KEY-54 | MUST | With an item focused after a keyboard open, `keydown` of <kbd>ArrowDown</kbd>, <kbd>ArrowUp</kbd> and (where `homeEnd… | §4.14 |
| CMP-KEY-55 | MUST | From item 0 (keyboard open), <kbd>ArrowUp</kbd> highlights the last item (`wrap`), stays on item 0 (`stop`) or clears… | §4.14 |
| CMP-KEY-56 | MUST | With a single item, <kbd>ArrowDown</kbd> after a keyboard open keeps it highlighted (`wrap`, `stop`) or clears then r… | §4.14 |
| CMP-KEY-57 | MUST | With no items, <kbd>ArrowDown</kbd> on the trigger opens the popup, focus rests on the popup (DOM) or trigger (virtua… | §4.14 |
| CMP-KEY-58 | MUST | When every item is disabled, opening with <kbd>ArrowDown</kbd> then <kbd>ArrowDown</kbd>, <kbd>ArrowUp</kbd>, <kbd>En… | §4.14 |
| CMP-DIS-03 | MUST | <kbd>Escape</kbd> after navigating closes without activating or changing the value, focuses the trigger, and leaves n… | §4.14 |
| CMP-DIS-04 | MUST | <kbd>Tab</kbd> closes without committing and focuses the "After" button; <kbd>Shift</kbd>+<kbd>Tab</kbd> closes witho… | §4.14 |
| CMP-DIS-05 | MUST | A press on the document body while an item is hovered closes without committing; (real browser) a real click on the "… | §4.14 |
| CMP-DIS-06 | MUST | (`triggerClickCloses`, i.e. Menu and Select) Clicking the trigger while open closes the popup. | §4.14 |
| CMP-DIS-07 | MUST | Inside an open dialog, <kbd>Escape</kbd> after a keyboard open closes only the popup (the dialog stays visible); a se… | §4.14 |
| CMP-ALG-01 | MUST | The implementation reproduces the following procedure exactly: | §5.1 |
| CMP-ALG-02 | MUST | The implementation reproduces the following procedure exactly (`disabledIndices` is absent, an array, or a predicate): | §5.1 |
| CMP-ALG-03 | MUST | The implementation reproduces the following procedure exactly: | §5.1 |
| CMP-ALG-04 | MUST | The implementation reproduces the following procedure exactly: | §5.1 |
| CMP-ALG-05 | MUST | The implementation reproduces the following procedure exactly (note the results are out of bounds — `length(list)` … | §5.1 |
| CMP-ALG-06 | MUST | The implementation reproduces the following procedure exactly: | §5.1 |
| CMP-ALG-07 | MUST | The implementation reproduces the following procedure exactly: | §5.1 |
| CMP-ALG-08 | MUST | The implementation reproduces the following grid navigation procedure exactly. `list` is the (cell or item) element l… | §5.1 |
| CMP-ALG-09 | MUST | The implementation reproduces the following procedure exactly (`sizes[i] = { width, height }` in cells): | §5.2 |
| CMP-ALG-10 | MUST | The implementation reproduces the following procedure exactly: | §5.2 |
| CMP-ALG-11 | MUST | The implementation reproduces the following procedure exactly: | §5.2 |
| CMP-ALG-12 | MUST | *Composite grid navigator.* `gridNavigation({ cols, dense := false, itemSizes })` returns a navigator that the compos… | §5.2 |
| CMP-ALG-13 | MUST | *Popup grid navigator* (uniform 1×1 cells). The implementation reproduces it exactly: | §5.2 |
| CMP-ALG-14 | MUST | *Composite key resolution.* The implementation reproduces the following procedure exactly on every `keydown` reaching… | §5.3 |
| CMP-ALG-15 | MUST | The implementation reproduces the following procedure exactly: | §5.3 |
| CMP-ALG-16 | MUST | The implementation reproduces the following procedure exactly: | §5.3 |
| CMP-ALG-17 | MUST | *Registry rebuild.* The implementation reproduces the following procedure exactly whenever the registry is dirty (pos… | §5.3 |
| CMP-ALG-18 | MUST | *Composite default-index adjustment on publication.* The implementation reproduces the following procedure exactly (`… | §5.3 |
| CMP-ALG-19 | MUST | *Scroll a composite item into view.* The implementation reproduces the following procedure exactly (`offset(container… | §5.3 |
| CMP-ALG-20 | MUST | *Key predicates.* The implementation reproduces the following procedures exactly: | §5.4 |
| CMP-ALG-21 | MUST | *Floating key handling* (Table C). The implementation reproduces the following procedure exactly (`cursor`, flags and… | §5.4 |
| CMP-ALG-22 | MUST | *Trigger key handling* (Tables A, B, D). The implementation reproduces the following procedure exactly: | §5.4 |
| CMP-ALG-23 | MUST | *Item focus procedure.* The implementation reproduces the following procedure exactly: | §5.4 |
| CMP-ALG-24 | MUST | *Wait for the list on open.* The implementation reproduces the following procedure exactly (invoked by CMP-STA-37): | §5.4 |
| CMP-ALG-25 | MUST | *Typeahead key handling.* The implementation reproduces the following procedure exactly (`buffer`, `prevIndex`, `matc… | §5.5 |
| CMP-ALG-26 | MUST | *Typeahead blur and reset.* The implementation reproduces the following procedures exactly: | §5.5 |
| CMP-ALG-27 | MUST | The implementation reproduces the following procedure exactly: | §5.6 |
| CMP-ALG-28 | MUST | *Virtual (assistive-technology) press detection*, used by CMP-STA-39. The implementation reproduces the following pro… | §5.6 |
| CMP-ALG-29 | MUST | The implementation reproduces the following procedure exactly: | §5.6 |
| CMP-ALG-30 | MUST | *Keyed registry.* The implementation reproduces the following procedure exactly: | §5.6 |
| CMP-ALG-31 | MUST | *Deferred focus* (`enqueueFocus`, shared with [floating-and-positioning](floating-and-positioning.md)). The implement… | §5.6 |
| CMP-EDGE-01 | MUST | Reading the composite root shared state from a part that is not inside a composite root fails (in every build) with t… | §7 |
| CMP-EDGE-02 | SHOULD | In development builds, building a grid cell map with an item whose `width` exceeds `cols` throws `[Floating UI]: Inva… | §7 |
| CMP-EDGE-03 | SHOULD | In development builds, list navigation with `allowEscape` and without `loopFocus` logs the warning `` `useListNavigat… | §7 |
| CMP-EDGE-04 | SHOULD | In development builds, list navigation with `allowEscape` and without `virtual` logs the warning `` `useListNavigatio… | §7 |
| CMP-EDGE-05 | SHOULD | In development builds, list navigation with `orientation: 'vertical'` and a grid navigator logs the warning `In grid … | §7 |
| CMP-EDGE-06 | MUST | When every composite item is disabled, the highlighted index stays in range (CMP-ALG-15 returns `0` at minimum) so th… | §7 |
| CMP-EDGE-07 | MUST | With an empty elements array, list navigation computes min `0` and max `-1`: <kbd>Home</kbd> reports `onNavigate(0)` … | §7 |
| CMP-EDGE-08 | MUST | The composite root ignores keys while its root element is not mounted. | §7 |
| CMP-EDGE-09 | MUST | When the composite highlighted index is consumer-owned, the "disabled indices changed" re-validation (CMP-STA-29) doe… | §7 |
| CMP-EDGE-10 | MUST | A typeahead session that starts with <kbd>Space</kbd> (empty buffer) is not consumed and leaves a single space in the… | §7 |
| CMP-EDGE-11 | MUST | Typeahead treats a `null` label as never matching, and labels array entries beyond the elements array are matchable w… | §7 |
| CMP-EDGE-12 | MUST | Event targets, containment and ancestor matching are evaluated on the composed tree: the composite root reads the ori… | §7 |
| CMP-EDGE-13 | MUST | When mutation observation is unavailable in the host environment, the item registry still registers and orders items;… | §7 |
| CMP-EDGE-14 | MUST | The registry never publishes an empty map while its subtree is temporarily withheld by an asynchronous boundary and t… | §7 |
| CMP-EDGE-15 | MUST | A consumer-rendered `activeIndex` that is out of bounds is ignored by the cursor re-synchronization (CMP-STA-32) and … | §7 |
| CMP-EDGE-16 | MUST | In grid navigation, a cursor of `-1` (nothing highlighted) with a vertical key moves to max for <kbd>ArrowUp</kbd> an… | §7 |
| CMP-EDGE-17 | MUST | The composite grid navigator is given `highlightedIndex > maxIndex ? minIndex : highlightedIndex` as the origin, so a… | §7 |
| CMP-EDGE-18 | MUST | The composite `stopEventPropagation`/`preventDefault` behavior applies only when the index changes (CMP-KEY-10/11); a… | §7 |

## 10. Open questions

1. **Keyed registry scope.** `internals/useItemRegistry.ts` is in this document's assignment, but its only consumer in v1.9.0 is `filter-dropdown` (out of scope per conventions §1). CMP-STA-19 to CMP-STA-21 and CMP-ALG-30 are specified for completeness; the port may defer them.
   Decided (2026-10-22, ADR 12 Amendments 2026-10-22 item 3; M2 track 3, issue 79): deferred, with the composite grid and `onLoop`, which no v1.9.0 component uses. Not built: the keyed registry (CMP-STA-19..21, CMP-ALG-30), the composite grid (CMP-KEY-12, CMP-ALG-09..12, CMP-I18N-05, CMP-EDGE-02) and `onLoop` (CMP-KEY-13, CMP-EVT-02). They are listed in the M2 pull request under "no consumer", and a later component that needs one adds it with its tests.
   Fidelity correction 2026-11-03 (ADR 12 Amendments 2026-11-03; conventions §1's dated note): the keyed registry has a v1.9.0 consumer. `Menu.FilterRoot` is exported (`menu/index.ts:30`, `menu/index.parts.ts:29`) and composes `filter-dropdown/root/FilterDropdownRoot.tsx`, which calls `useItemRegistry` (`:8`, `:64`; also `filter-dropdown/group/useFilterDropdownGroup.ts:46`). CMP-STA-19..21 and CMP-ALG-30 are therefore first consumed by Menu's filter mode (M5 track 12), which builds them or records why its C# state needs no registry, with a note on each row. The composite grid and `onLoop` stay deferred.
2. **Composite grid without looping falls through to a linear step.** With `grid` configured and `loopFocus: false`, when the grid navigator finds no cell below/above and returns the current index, `useCompositeRoot.ts:282-300` still applies the linear forward/backward rule for `'both'`/`'vertical'` (ArrowDown) and `'both'`/`'horizontal'` (ArrowRight), so <kbd>ArrowDown</kbd> from a cell with no cell beneath moves to the next flat index. The tests only cover the last item (where the flat step is out of bounds). Confirm whether this is intended; the spec transcribes it as-is.
   2026-10-22 (M2 track 3): not built; the composite grid is deferred (item 1).
3. **Loop hook indices in multi-cell composite grids.** `internals/composite/root/gridNavigation.ts:85-122` passes `onLoop` straight into `getGridNavigatedIndex` over the cell map, so `prevIndex`/`nextIndex` given to `onLoop` are *cell* indices when `itemSizes` has spans, while the fourth argument (`elementsRef`) is item-indexed. Undocumented; only 1×1 grids are tested.
   2026-10-22 (M2 track 3): not built; the composite grid and `onLoop` are deferred (item 1).
4. **Disabled-default check indexes map positions, not registry indexes.** `useCompositeRoot.ts:151-160` evaluates `isListIndexDisabled(sortedElements, highlightedIndex, …)` with the map's key array (positional), which differs from registry indexes only when explicit indexes leave gaps. No test exercises that combination.
   2026-10-22 (M2 track 3): holds as transcribed. `js/composite.ts` evaluates the first publication's disabled check over the map's keys, by position, as CMP-ALG-18 writes it; the positions equal the registry indexes whenever no explicit index leaves a gap, and C# registers no explicit index.
5. **Grid cell map with an over-wide item.** `floating-ui-react/utils/composite.ts:439-470` throws only in development; in production the `while (!itemPlaced)` loop cannot terminate for `width > cols`. CMP-EDGE-02 therefore requires ports to reject the configuration unconditionally. Decided (2026-10-18): CMP-EDGE-02 keeps its reject-in-every-build clause (ADR 06 Amendments).
   2026-10-22 (M2 track 3): not built; the composite grid is deferred (item 1), and CMP-EDGE-02 keeps its decision for when it is.
6. **Leading space in the typeahead buffer.** `useTypeahead.ts:132-142,200-203` leaves a lone `" "` in the buffer after a failed match (CMP-EDGE-10). Probably incidental; a port may want to clear it, but the spec records the observed behavior.
   2026-11-04 (M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 12 Amendments 2026-11-03 (5), chosen by the user: untested quirks are replicated as the source behaves): replicated. A leading Space is not consumed and stays in the buffer until the timer, a blur, a close or a failed non-space character clears it (CMP-EDGE-10): `js/composite-typeahead.test.ts` "port: a typeahead session that starts with Space is not consumed and keeps the space in its buffer".
7. **Empty list min index.** `getMinListIndex` returns `0` for an empty list (`findNonDisabledListIndex` from `-1`), so <kbd>Home</kbd> on an empty popup emits `onNavigate(0)` (CMP-EDGE-07) while <kbd>End</kbd> and arrows emit `null`. Harmless in Base UI (no item to focus); ports should match or document a deviation.
   2026-11-04 (M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 12 Amendments 2026-11-03 (5), chosen by the user: untested quirks are replicated as the source behaves): replicated. The list handle takes min and max from the M2 helpers (`js/composite.test.ts` "returns out-of-bounds min and max when every item is disabled, and 0 and -1 for an empty list (CMP-ALG-05)"), so Home on an empty popup reports the highlight 0 with no item while End and the arrows report `null` (CMP-EDGE-07), and the focus stays where it was: the popup-list suite's "opens and handles keys without losing focus when the list is empty" (CMP-KEY-57), green at L4 in the three configurations. No title presses Home on an empty list (CMP-EDGE-07's note).
8. **`focusItemOnOpen: false` and keyboard opens.** CMP-STA-37 is gated on the effective flag being truthy, so `false` suppresses the initial highlight even for <kbd>ArrowDown</kbd> opens (`useListNavigation.ts:441-445`). Component docs that pass `false` (if any) should state the resulting behavior explicitly.
   2026-11-04 (M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 12 Amendments 2026-11-03 (5), chosen by the user: untested quirks are replicated as the source behaves): replicated. The initial sync runs only while the effective flag is truthy (`js/composite.ts`, `syncActiveIndex`), so `false` suppresses the first-item highlight after a keyboard open too. `js/composite-list.test.ts` "does not focus the first item on click when false" opens with a click that `'auto'` would read as virtual (CMP-ALG-28) and highlights nothing; no title opens with ArrowDown under `false`. Combobox is the family that passes `false` (`AriaCombobox.tsx:1425-1426`, track 10) and states the result; Menu's filter mode passes `keyboardOpen` (`MenuRoot.tsx:527`), `false` only after a pointer open (track 12).
9. **Active item marker read only at first publication and on fallback.** Later changes of `data-composite-item-active` (CMP-DOM-02) do not move the tab stop by themselves (`useCompositeRoot.ts:115-135`); Tabs keeps the tab stop in sync through `highlightedIndex`/`onHighlightedIndexChange` (`tabs/list/TabsList.tsx:119-123`). RadioGroup passes neither (`radio-group/RadioGroup.tsx:250-264`); it relies on the `data-composite-item-active` marker at first publication (the checked radio) plus focus-driven highlighting thereafter (CMP-PTR-02). The TAB and RDG docs should state which mechanism they rely on.
   Resolved for RDG: radio-group.md RDG-FOC-01/02 (radio-group.md §16 Q1). Corrected 2026-10-14: an earlier wording said RadioGroup also synced via `highlightedIndex`.
   Resolved for TAB: tabs.md TAB-STA-19 (the List owns `highlightedIndex` and passes `onHighlightedIndexChange`).
   2026-10-22 (M2 track 3): as resolved for RDG. The module reads the marker at the first non-empty publication and on fallback only (CMP-ALG-18), and RadioGroup passes no highlight of its own, so the tab stop follows focus and keyboard moves.
   2026-10-27 (M3 track 1, issue 100; ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26): for TAB, the module also reads the marker after the first publication, under `CompositeOptions.FollowActiveItem`. It observes the marker with a `MutationObserver` on the root, and re-checks on each publication, highlight change and flag change, so a selection made while focus is outside the root moves the tab stop pre-paint on both modes (CMP-STA-22's port note; design points 4 and 5, option (a) both, settled by the orchestrator 2026-10-26). Without the option, reading only at the first publication and on fallback is unchanged.
10. **Disabled ends reachable by Home/End but skipped on open.** The conformance suite expects Menu/Select to open past a disabled first item yet reach disabled end items with <kbd>Home</kbd>/<kbd>End</kbd> and arrows (CMP-KEY-45, CMP-KEY-51, CMP-KEY-52). This follows from CMP-STA-37 omitting `disabledIndices` while CMP-KEY-28/32 use the components' `disabledIndices`; the MNU/SEL docs must cite the exact `disabledIndices` value they pass.
   Resolved: select.md SEL-KEY-20 (`disabledIndices: []` to list navigation, `select/root/SelectRoot.tsx:376`) and menu.md MNU-KEY-05 (`disabledIndices: []`, `menu/root/MenuRoot.tsx:534`).
11. **API outline cross-check.** None of the mechanisms here appear in `docs/src/app/(docs)/react/components/page.mdx`; the public props covered by §3 (`loopFocus`, `orientation`, `highlightItemOnHover`) appear on Accordion, Menu, Menubar, Select, Combobox, Autocomplete, Tabs, ToggleGroup, Toolbar, NavigationMenu entries. RadioGroup has **no** `orientation` or `loopFocus` prop: it renders the composite root without them, so the mechanism defaults apply — orientation `'both'` and looping on (`radio-group/RadioGroup.tsx:250-264`, `internals/composite/root/useCompositeRoot.ts:76-77`). Accordion's `loopFocus`/`orientation` are not implemented through these mechanisms (Accordion items only register with the item registry); the ACC doc owns them.
   2026-10-22 (M2 track 3): resolved for RadioGroup. `CompositeOptions` defaults to `orientation` `both` and `loopFocus` `true`, and the RadioGroup part (track 8) passes neither.
12. **Implementation revision of 2026-10-22 (M2 track 3, issue 79; ADR 12 Amendments 2026-10-22 item 3; ADR 13 Amendments 2026-10-22).** Dated 2026-10-22. The `composite` module (`src/Blazix.Primitives/js/composite.ts`), `CompositeTwin`, the `Composite/` behaviours and `DirectionProvider` ship the 65 IDs RadioGroup reaches. Amendments under conventions §3.5, each with its dated port note in the bullet:
   - re-tagged [L2] → [L3], the registry being JavaScript's (ADR 01 decision 2): CMP-STA-01, CMP-STA-04 (M5, implemented early), CMP-STA-05, CMP-STA-08, CMP-STA-09, CMP-STA-14, CMP-STA-18, CMP-ALG-17, CMP-EDGE-14;
   - re-tagged [L2] → [L3], the highlight and the items' `tabindex` being JavaScript's from attach (ADR 04 Amendments 2026-10-22): CMP-STA-22, CMP-STA-23, CMP-STA-24, CMP-STA-26, CMP-STA-27, CMP-ALG-15, CMP-ALG-18, CMP-EDGE-06, CMP-DOM-01, CMP-DOM-02;
   - re-tagged [L2] → [L4], the cited title being browser-only in Base UI: CMP-STR-01;
   - re-cited under ADR 13 Amendments 2026-10-22: CMP-KEY-16 and CMP-FOC-02 to "uncontrolled mode", CMP-FOC-01 to the port title "Tab lands on the item holding the tab stop and Tab leaves the group";
   - restated titles (React mechanics or a later shape), each noted in its bullet: CMP-STA-09's two, CMP-STA-10's four, CMP-STA-15's, CMP-STA-18's, CMP-STA-23's and CMP-DOM-02's, CMP-EDGE-14's, CMP-EVT-01's;
   - port notes on CMP-DOM-01 (JavaScript writes the value), CMP-I18N-01 (the `DirectionProvider` cascade), the rtl titles of CMP-KEY-03, CMP-KEY-07 and CMP-I18N-02 (skipped at L3, run at L4), CMP-I18N-03 and CMP-KEY-06 (port titles), CMP-EVT-12, the M5 clauses of CMP-API-01, CMP-API-02 and CMP-ALG-02, CMP-ALG-14 and CMP-EDGE-12;
   - implemented early, green at L3 or L4 under their first consumer's milestone: CMP-KEY-04, CMP-KEY-05, CMP-KEY-09, CMP-KEY-17, CMP-STA-29 (M3); CMP-PTR-01, CMP-ALG-06, CMP-STA-04 (M5);
   - internal guards that no M2 public part reaches, throwing `Blazix: ` messages: CMP-STR-02, CMP-EDGE-01.
   - No new Server relaxation is added. The decision points are named `navigate` and `select`, chosen by the user 2026-10-22 (ADR 04 Amendments 2026-10-22). A key the module handles with `stopEventPropagation` never reaches Blazor's document-level dispatcher, so without more no C# `onkeydown` on the root, an item or an ancestor would run for it on Server, and on WebAssembly only the root's, inside `navigate` (the L4 probe). The user chose option (i) of ADR 04's note on 2026-10-23: the focused item's and the root's handlers run inside `navigate` on WebAssembly and from the module's handled-key report on Server, and an ancestor's never runs. ADR 01's row "… EVD-EVT-26 against composite/button key handling …" gains that Server text (ADR 01 Amendments 2026-10-23).
13. **Implementation revision of 2026-10-23 (M2 track 8, issue 84; ADR 13 Amendments 2026-10-22).** Dated 2026-10-23. RadioGroup is the first public composite root, so the six M2-reachable `[L4]` rows whose `CompositeRoot.test.tsx` titles render the internal root (CMP-FOC-01, CMP-FOC-02, CMP-KEY-10, CMP-KEY-11, CMP-KEY-16, CMP-EDGE-18) and CMP-EVT-12 gain a port note naming the `radio-group.spec.ts` title that proves each through the public part, on both render modes. The clauses only an M3 or M5 option reaches (grid, `onLoop`, `loopFocus={false}`, `disabledIndices`) stay listed by milestone as item 12 lists them. No requirement, tag or Server relaxation changes; the document stays `approved`.
14. **Implementation revision of 2026-10-27 (M3 track 1, [issue 100](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/100); ADR 04 and ADR 12 Amendments 2026-10-26; ADR 13 Amendments 2026-10-22).** Dated 2026-10-27. The `composite` module, `CompositeTwin` and the `Composite/` behaviours gain the five M3 extensions the user chose on 2026-10-26, each with a dated note on ADR 04's composite row: the items' flags with derived indices (E3), the outer registration (E4), the items report (E2), the registration-only list (E1) and the active-item follow (E5). Every extension is internal, proven on the test-only hosts (`TestCompositeRoot`, `TestCompositeItem`, `TestCompositeList`, `TestCompositeListItem`; the `composite-nested`, `composite-flags`, `composite-follow` and `composite-registration` fixtures, both modes). The shapes of E1, E2 and E5 follow design points 1–6 of track 1, each option (a), settled by the orchestrator 2026-10-26 and recorded on ADR 04's row: one ordered-ids report (1), an id on every list item (2), self-contained reports joined on arrival (3), a `MutationObserver` for the follow (4), `aria-disabled` read from the applied batch (5), and a separate list context (6). Design point 6b, the two untested nearest-registry quirks, is not replicated, chosen by the user 2026-10-27 (CMP-STR-01's port note). Amendments under conventions §3.5, each with its dated port note in the bullet:
   - green on the test-only hosts at their layers: CMP-STR-03 [L2], CMP-STR-04 [L4] (L2 for the restated title), CMP-STA-29 [L3] (L4 for the cited title), CMP-EVT-03 [L3] (L2 for the restated title), CMP-EDGE-09 [L3] (it had no test); and the M3 clauses of the partial rows CMP-STA-08 (the flags; Tabs' metadata joined by id), CMP-STA-22 (the follow; "controlled mode" restated) and CMP-KEY-08 (the derived indices), with a note on CMP-STA-24;
   - re-tagged: CMP-STR-04 [L2] → [L4], CMP-STA-29 [L2] → [L3], CMP-EVT-03 [L2] → [L3]; CMP-STR-03 stays [L2];
   - corrected: CMP-API-02's port note (ADR 08 Amendments 2026-10-26, chosen by the user): the M3 roots pass a non-nullable `Orientation` with the family default, and no M3 family defaults to `both`;
   - port notes: CMP-STR-01 (design point 6b, not replicated, chosen by the user 2026-10-27), CMP-STA-08, CMP-STA-22 (with the dated divergence of the follow's `aria-disabled` read), CMP-STA-24, CMP-KEY-08, CMP-EVT-01 (TabsList's callback has no C# counterpart), CMP-EDGE-09, and item 9 above;
   - green at L3 since M2 (item 12): CMP-KEY-04, CMP-KEY-05, CMP-KEY-09; CMP-KEY-17's [L4] title waits for `ToolbarInput` (track 4);
   - awaiting a public part, by track (ADR 13 Amendments 2026-10-22: the internal root is incidental, and each family's pull request adds its "also proven through" note):
     - CMP-API-01 and CMP-API-02 on all three roots (ToggleGroup, Toolbar, Tabs);
     - ToggleGroup (tracks 2–3): CMP-KEY-01, CMP-KEY-05, CMP-KEY-09, CMP-I18N-03;
     - Toolbar (track 4): CMP-STR-04, CMP-STA-08, CMP-STA-29, CMP-KEY-04, CMP-KEY-08, CMP-KEY-09, CMP-KEY-17, CMP-EDGE-12, CMP-EVT-03;
     - Tabs (track 5): CMP-STR-03 (Panel), CMP-STA-08, CMP-STA-22, CMP-KEY-01, CMP-KEY-05, CMP-KEY-09, CMP-I18N-03, CMP-EVT-01, CMP-EVT-03, CMP-EDGE-09;
     - Accordion (track 6): CMP-STR-03 (Item);
   - no consumer: CMP-KEY-15's bare-mechanism branch, which no v1.9.0 component uses (the part default `true` is every root's);
   - no new Server relaxation: the flags are `update`-carried (ADR 01's row "JS-owned values that originate in C# state"), and the list's indexes arrive after paint plus a round trip, which CMP-STA-10's row already names ("published indexes"). The follow and the tab stops it moves are the module's, pre-paint on both modes. No new decision point: `navigate` and `select` stay the module's two.
   - The document stays `approved` (ADR 13 Amendments 2026-10-22); its README row is unchanged.
15. **Implementation revision of 2026-10-27 (M3 track 5, [issue 104](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/104); ADR 13 Amendments 2026-10-22).** Dated 2026-10-27. Tabs consumes E1, E2 and E5 through its public parts, and adds an "also proven through the public" note to CMP-API-01, CMP-API-02, CMP-KEY-01, CMP-KEY-05, CMP-KEY-09, CMP-STR-03 (the Panel as a registration-only list's item), CMP-STA-08, CMP-STA-22, CMP-EVT-03 and CMP-EDGE-09. CMP-I18N-03 and CMP-EVT-01 stay without a Tabs note: the Tabs titles do not assert that direction leaves the vertical keys and Home and End alone, and `TabsList` has no C# counterpart of `onHighlightedIndexChange`. No requirement, tag or Server relaxation changes; the document stays `approved`.

16. **The rows open to M5 (2026-11-03, restored by the M5 orchestrator from [PR 86](https://github.com/JakeMurrayDev/Blazix.Primitives/pull/86)'s "Deferred rows by milestone"; ADR 12 Amendments 2026-10-22 item 3 and 2026-11-03).** Items 12 to 15 record what M2 and M3 built; the by-milestone list of the rows left for M5 lived only in PR 86's description. 133 whole rows are open to M5, in three groups:
   - the popup-list suite, §4.14 (32, all [L4]): CMP-PTR-09..16, FOC-10, FOC-11, KEY-44..58, STA-46, STA-47, DIS-03..07;
   - typeahead (21): CMP-KEY-35..43, STA-42..45, STR-07, EVT-10, EVT-11, I18N-07, ALG-25, ALG-26, EDGE-10, EDGE-11;
   - list navigation and the rest (80): CMP-API-03, STR-06, STA-06, STA-07, STA-16, STA-17, STA-31..41, DOM-04, DOM-05, ARIA-01, ARIA-02, KEY-14, KEY-18..34, PTR-03..08, FOC-03..09, I18N-04, DIS-01, DIS-02, EVT-04..09, ALG-07, ALG-08, ALG-13, ALG-20..24, ALG-27, ALG-28, ALG-29, ALG-31, EDGE-03, EDGE-04, EDGE-05, EDGE-07, EDGE-15, EDGE-16, EDGE-17.
   Fidelity correction (2026-11-04, M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); conventions §3.5, not a decision): the third group listed 78 IDs for its "80"; CMP-EDGE-16 and CMP-EDGE-17 (the popup grid's edges, §7) were missing and are added, as the M5 fact sheet's count of the group has them.
   PR 86's third group also held CMP-STA-04, CMP-PTR-01 and CMP-ALG-06, implemented early and green at L3 since M2 (item 12). The M5 clauses of partial rows: the `useListNavigation.test.tsx` titles of CMP-API-01 ("ArrowDown looping", "navigates down on ArrowDown"), CMP-API-02 ("navigates down on ArrowRight", "navigates up on ArrowLeft") and CMP-ALG-02 ("skips disabled item on initial navigation"); the labels clauses of CMP-STA-05, STA-14 and ALG-17; list navigation's and the grid's clauses of CMP-EDGE-12. With no v1.9.0 consumer (item 1): the composite grid (CMP-KEY-12, ALG-09..12, I18N-05, EDGE-02) and `onLoop` (CMP-KEY-13, EVT-02); the keyed registry (CMP-STA-19..21, ALG-30) has one after all, Menu's filter mode (item 1's correction of 2026-11-03). M5 track 1 (list navigation, ADR 12 Amendments 2026-11-03) owns the 133 rows on its test-only hosts; the families add their "also proven through" notes. No requirement, tag or Server relaxation changes; the document stays `approved`.

17. **Implementation revision of 2026-11-04 (M5 track 1, [issue 159](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/159); ADR 04 Amendments 2026-11-03 (1)–(4), chosen by the user; ADR 12 Amendments 2026-11-03; ADR 13 Amendments 2026-10-22).** Dated 2026-11-04. The `composite` module's list half (`js/composite.ts`: the list handles on `attach` with `CompositeOptions.List`, typeahead, the popup grid, the nested hand-off and `relayKeyboardEvent`), `CompositeTwin` with `CompositeListOptions` and the list reports, and the store-agnostic C# half in `Composite/` (`ListNavigationBehavior`, `ListNavigationContext`, `CompositeListRole`, `CompositeRootBehavior.SetList`, and `CompositeItemBehavior`'s `Highlighted` mirror) ship list navigation; ADR 04 Amendments 2026-11-04 record them as built. Every part is internal (§3), so the proofs run on test-only hosts: at L2 `TestListRoot`, `TestListHandle` and `TestListItem`; at L3 the list and menu-tree harnesses (`js/test-support/`); at L4 the fixtures `composite-list-dom`, `composite-list-virtual`, `composite-list-nested`, `composite-list-selected`, `composite-list-relay` and `composite-list-measure`, on both modes (`tests/browser/composite-list.spec.ts`, `composite-list-rows.spec.ts`, `composite-list-figures.spec.ts`). The design points, recorded on ADR 04's row: 1 (a), one handle per element role, with its C# refinement (r1); 2 (c), the typing flag read through `defaultPrevented`; 3 (a), the relay through an element option and a symbol; 4 (a), CMP-STA-16 restated at L3; 6 (a), the visibility predicates moved into `dom`. All five were settled by the orchestrator 2026-11-04; the user was told and may overturn them. Design point 5 (b), the chunked order report, was chosen by the user 2026-11-04. Amendments under conventions §3.5, each with its dated port note in the bullet:
   - green at L4, the popup-list suite (32; §4.14's port note): CMP-PTR-09..16, FOC-10, FOC-11, KEY-44..58, STA-46, STA-47 and DIS-03..07, in Menu's (44 titles) and Select's (47) configurations on `composite-list-dom` and Combobox's (38) on `composite-list-virtual`. CMP-DIS-07 does not wait for track 2's cascade.
   - green at L4, the rows of the other groups tagged [L4] (11): CMP-KEY-24, KEY-30, KEY-34, FOC-05 and DIS-02 by the suite's titles; CMP-STA-40, FOC-03 and FOC-04 by `composite-list-rows.spec.ts` (FOC-03 and FOC-04 with the suite's titles too); CMP-ALG-31 (re-tagged); CMP-FOC-09 and EDGE-07 (re-tagged) by the suite's title.
   - green at L4, the "(L4 for …)" clauses of the rows green at L3: by the suite, those of CMP-STA-34, STA-37, STA-39, STA-43, ARIA-01, KEY-21, KEY-31, KEY-33 and PTR-08, and the suite parts of CMP-STA-33, KEY-28, PTR-04 and PTR-05; by `composite-list-rows.spec.ts`, those of CMP-API-03, STA-33, STA-35, KEY-22, KEY-23, KEY-28, KEY-37, PTR-04 and PTR-05. CMP-API-02's consumption clause is green through CMP-KEY-30's titles and the relay's; CMP-API-01's is CMP-KEY-30's statement, but no L4 title presses a main key at a list end without looping (its note).
   - green at L3, typeahead (18 of 21; `js/composite-typeahead.test.ts`): CMP-STR-07, STA-43..45, KEY-35, KEY-37..43, EVT-10, EVT-11, I18N-07, ALG-25, ALG-26 and EDGE-11. The two `onTyping` titles are restated through the typing flag (CMP-STA-43, STA-44, KEY-37, EVT-11, ALG-26).
   - green at L3, list navigation and the rest (44 of 80; `js/composite-list.test.ts`, `js/composite.test.ts`): CMP-API-03, STA-06, STA-07, STA-16, STA-17, STA-31, STA-33..35, STA-37, STA-39, STA-41, DOM-04, ARIA-01, KEY-18..23, KEY-27..29, KEY-31..33, PTR-04..08, FOC-06, FOC-07, I18N-04, DIS-01, EVT-04, EVT-06, ALG-07, ALG-08, ALG-13, ALG-20, ALG-24, ALG-29 and EDGE-16; CMP-KEY-31 through titles other than the cited suite's. CMP-KEY-27 and DIS-01 also run at L4, with the focus return across the portal (`composite-list-nested`). The M5 clauses of CMP-API-01, API-02 and ALG-02, and the labels clauses of CMP-STA-05, STA-14 and ALG-17, are green at L3 too.
   - `Test: none` kept, proven by port titles (the M2 "none (port L3)" precedent of PR 86): at L3 CMP-STR-06, STA-32, ARIA-02, KEY-14, KEY-25, KEY-36, EVT-07, EVT-08, ALG-28, EDGE-03..05, EDGE-10, EDGE-15 and EDGE-17 (CMP-KEY-14 also at L4 on `composite-list-relay`); at L1 CMP-EVT-09 and ALG-27 (EVD-ALG-11's `HighlightReason`).
   - `Test: none` kept with no title, built as in Base UI: CMP-STA-36, STA-38, STA-42, DOM-05 (the floating handle's element stands for the popup focus element), KEY-26, PTR-03, FOC-08 and EVT-05 (one report per change).
   - `see` rows: CMP-ALG-21, ALG-22 and ALG-23 follow the KEY and FOC rows that carry their tests.
   - re-tagged: CMP-STA-07, STA-17, DOM-04, ARIA-01 and STA-16 [L2] → [L3]; CMP-KEY-27 and DIS-01 [L4] → [L3] (their titles are `it.skipIf(!isJSDOM)`); CMP-ALG-31 [L3] → [L4] (its title is `it.skipIf(isJSDOM)`); CMP-FOC-09 and EDGE-07 [L3] → [L4] (their cited titles are the popup-list suite's), the track's decision under ADR 13 §3. CMP-STA-40 stays [L4], the track's decision, though its title also runs at L3.
   - dated deviation: CMP-ARIA-01's id clause (Amendments 2026-11-03 (2): items keep their generated ids, and the module writes the active item's own id).
   - replicated as the source behaves (ADR 12 Amendments 2026-11-03 (5)): Q6 (CMP-EDGE-10), Q7 (CMP-EDGE-07) and Q8 (CMP-STA-37), each with its dated line above.
   - re-cited: CMP-ALG-08's two `CompositeRoot.test.tsx` titles, to the deferred composite grid (CMP-KEY-12) and `onLoop` (CMP-EVT-02).
   - corrected, as noted 2026-11-04: item 16's third group (CMP-EDGE-16, EDGE-17) and §6.8's filter-root users, fidelity corrections with their `_qa-notes.md` line; ADR 04's 2026-10-18 sentence on CTX-KEY-07 (ADR 04 Amendments 2026-11-04).
   - the attach order (the track's brief, Deliverable 4; design point 1 (a)'s handles on one trigger), pinned on both modes by `composite-list-rows.spec.ts` "port: with js/composite.js stalled the list opens on ArrowDown with its first item highlighted once both modules load", the same title with `js/interactions.js` stalled, "port: with js/interactions.js stalled a list open from the start highlights its first item once the session attaches" (red without the attach-after-session rule, on both modes), "port: with js/composite.js stalled the focus manager re-picks its initial focus onto the item the list handle writes tabindex="0" on" and "port: with js/focus-manager.js stalled the list opens on ArrowDown on its first item, and the focus stays there once the focus manager attaches" (CMP-STA-37's and CMP-FOC-03's notes).
   - Server relaxations, with figures (ADR 01 Amendments 2026-11-04, measured by `composite-list-figures.spec.ts` with the circuit delayed 75 ms each way): §4's list names the rows: the `Highlighted` mirror (CMP-STA-31, STA-34, STA-35), C#-origin highlights and the selected index (CMP-STA-32, STA-33, STA-40), the open by ArrowDown with its initial highlight (CMP-STA-37, KEY-21, FOC-09), the registration and labels after a re-render (CMP-STA-06, STA-07, STA-17), a closed list's typeahead match (CMP-EVT-10), `navigate` on the list handles, and `leave` (CMP-KEY-24, EVT-08); each row's note ends "Server: see ADR 01". The list measurement fixture's messages and bytes per key are ADR 14's (Amendments 2026-11-03, its line of 2026-11-04).
   - green at L3 after PR 170's review: CMP-EDGE-12's list-navigation and grid clauses ("list navigation inside a shadow root (port)", four titles); open: CMP-EDGE-07's Home and End reports on an empty list have no title, in Base UI as here (§10 Q7).
   - also proven through, by family (ADR 13 Amendments 2026-10-22: the test-only hosts are incidental, and each family's pull request adds its notes):
     - Menu (track 6): the suite's Menu configuration on its parts; nested lists (CMP-STA-41, KEY-23, KEY-27, DIS-01, EVT-07, FOC-08); typeahead with its 500 ms reset and the element skip (CMP-KEY-42); the highlight's writer and mirror (CMP-STA-31..35); CMP-EVT-04 and EVT-09 through `onItemHighlighted`; `leave` (CMP-KEY-24, EVT-08).
     - ContextMenu (track 7): `KeepOpenOnCrossOrientationKey` (CMP-KEY-27; CTX-KEY-07).
     - Menubar (track 8): the keyboard relay (CMP-KEY-14) and the bar's nested menus (CMP-KEY-23, KEY-27, ALG-20).
     - Select (track 9): the suite's Select configuration; `SelectedIndex` (CMP-STA-33, STA-40, STA-47); the attribute skip (CMP-KEY-42); the closed-trigger match (CMP-EVT-10) and its ID in ADR 01's list rows.
     - Combobox (track 10): the suite's Combobox configuration; virtual focus (CMP-ARIA-01, ARIA-02, KEY-20, KEY-28's typeable branch, KEY-33, FOC-05) with CMB-DOM-09's id deviation; the popup grid (CMP-ALG-13); C#'s typeahead labels (CMP-EDGE-11); Q8's `false`; CMP-DOM-05's List and Popup split (its note).
     - Autocomplete (track 11): virtual focus with AUT-DOM-03's id deviation.
     - Menu's filter mode (track 12): CMP-ALG-20's exported predicates, and the keyed registry (CMP-STA-19..21, ALG-30; item 1).
   - The document stays `approved` (ADR 13 Amendments 2026-10-22); its `Revised` row reads 2026-11-04 (M5 track 1).
