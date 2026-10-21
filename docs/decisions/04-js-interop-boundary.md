# ADR 04 — JavaScript interop boundary

| | |
|---|---|
| Status | accepted (2026-10-17) |
| Project | Blazix.Primitives |
| Decides | which behaviour lives in JavaScript and how it is partitioned; which DOM attributes JavaScript writes; the C#/JS contract including decision points inside JS-owned loops; the JS toolchain and delivery; portals; the development-first render mode |
| Affects | FLT (all JS-resident interaction and positioning IDs), PSH-PTR-09..28 and PSH-ALG-07..12 (swipe), PSH focus guards and portal IDs, TRN-ANI-03..15, TRN-DOM-01/02/05/06/08/09, TRN-STA-04/05/07/09, CMP (all keyboard, focus, typeahead and registry-move IDs), UBT (all), UTL-DOM-01..12 and UTL-DOM-18..31, UTL-ALG-11, SLD-PTR-*, SLD-ANI-01..07, SCR-PTR-*, SCR-STA-06..18, SCR-ANI-*, DRW-PTR-*, DRW-ALG-*, NUM scrub-area IDs, TAB indicator IDs, SEL list-alignment and scroll-arrow IDs; EVD-API-05, EVD-EVT-11, EVD-EVT-26, EVD-DIS-01, SLD-PTR-01; RND-STA-01 for mirrored fields |

## Context

ADR 01 fixed the residency rule (anything that needs a browser API is JavaScript; a frame-rate loop is JavaScript wholesale) and ADR 03 made a bundler unavoidable. The old port placed browser-API code in 36 per-component modules (about 30,600 lines; `floating.js` 5,182) whose shared parts became unmaintainable; the user named composite, floating and roving as the spaghetti. Its JS was hand-minified and the runtime loaded the `.min.js`, so source edits were invisible until regenerated. Its portal moved the DOM node into the container after render and moved it back before disposal, and that worked on both render modes. Blazor's renderer diffs its own tree and never reads the DOM, so an attribute C# never renders is never touched by Blazor, but a change to a C#-rendered `style` string replaces the whole attribute. On WebAssembly a JS listener may invoke a `[JSInvokable]` method synchronously and read the result inside the dispatch; Server cannot.

## Decision

### 1. Partition: one module per mechanism, plus component loops (G2)

| Module | Owns | Doc |
|---|---|---|
| `positioning` | engine assembly, Base UI middleware, anchor tracking, output writes | FLT §5, ADR 03 |
| `interactions` | press-to-toggle, dismissal (outside press, Escape, reference press), focus-to-open, cursor anchoring, hover with safe polygon, delay group, floating tree | FLT §4.7, §4.9, §4.10, §5.3 |
| `focus-manager` | initial focus, modal trap, focus-out close, focus restoration, return focus, outside marking (`aria-hidden`/inert), tabbable queries | FLT §4.8, §4.5 |
| `portal` | container resolution, DOM move and restore, focus guards | FLT §4.4, PSH |
| `transitions` | animations-finished watcher (TRN-ALG-03), next-frame flips of `data-starting-style`/`data-ending-style`, `hidden="until-found"` and `beforematch` (TRN-ALG-07), completion notification | TRN |
| `composite` | roving focus, list and grid navigation, typeahead, registry move detection, per-key `preventDefault` | CMP |
| `button` | keydown-to-click synthesis with modifiers, pointer-type classification, disabled press suppression, focusable-when-disabled, press-and-hold timers | UBT |
| `scroll-lock` | document scroll lock | UTL §4.8, UTL-ALG-01/02 |
| `dom` | platform flags computed once, owner/shadow-DOM helpers, rect/computed-style reads, `ResizeObserver`/`IntersectionObserver` subscriptions used by PSH auto-resize and viewport, Collapsible height, Tabs indicator, Select alignment, Avatar image load | UTL §4.10, §4.11, §4.16 |
| `swipe` | the shared swipe tracker | PSH §4.7.5 |
| component modules `slider`, `scroll-area`, `drawer`, `number-field`, `tabs`, `select` | that component's ALG sections and loops, composed from the modules above | component docs |

Rules: a mechanism module never imports a component module; a component module contains only its own component's ALG-tagged behaviour and composes mechanism modules; each mechanism module maps to exactly one architecture doc's JS-resident requirements. Adding a module amends this ADR. Everything not listed is C#: the popup store, controlled state, event details, field and form infrastructure, timers whose result is state, and all parts. Composite keyboard navigation is deliberately JavaScript: it reads native `disabled`/`aria-disabled` and visibility, calls `focus()`, and must `preventDefault` only for keys it handles; C# receives active-index and highlight changes.

Each JavaScript module has one C# **twin** that owns the interop handle and exposes typed callbacks; parts talk to twins, never to `IJSRuntime` directly.

### 2. Attribute ownership: single writer, JavaScript for frame-timed values, C# mirroring (O2)

Every DOM attribute and style property on a part's host element has exactly one writer. The writer is JavaScript when the value changes at frame timing or is produced by a JS loop; otherwise it is C#. A per-part **ownership list** names the JS-owned attributes; the initial list:

| Part(s) | JS-owned |
|---|---|
| every `*.Positioner` | `style` (transform/top/left, `--anchor-*`, `--available-*`, `--transform-origin`), `data-side`, `data-align`, `data-anchor-hidden` |
| every `*.Arrow` | `style` (position), `data-side`, `data-align`, `data-uncentered` |
| every animated part (TRN-DOM-04 list) | `data-starting-style`, `data-ending-style`; popups additionally the `transition: none` of TRN-DOM-05 |
| `Collapsible.Panel`, `Accordion.Panel` | `hidden="until-found"`, measured `--collapsible-panel-height/width` |
| `Drawer.Popup`, `Drawer.Backdrop`, `Drawer.Indent*` | swipe movement variables, `data-swiping`, `data-swipe-dismiss`, `--drawer-swipe-progress`, `--drawer-height`, `--drawer-swipe-strength` |
| `ScrollArea.Thumb`, `ScrollArea.Viewport`, `ScrollArea.Root` | thumb `transform`, `--scroll-area-*` variables, `data-scrolling`, `data-hovering`, corner size variables |
| `Slider.Thumb`, `Slider.Indicator` | `--position`, `--start-position`, `--end-position`, `--relative-size`, inline `visibility` |
| `Tabs.Indicator` | `--active-tab-*`, `hidden` until measured |
| `Toast.Root` | swipe movement variables, `data-swiping`, `data-swipe-direction` |

Consequences of the rule: C# excludes JS-owned state fields from the `data-*` projection (the custom mapping returns nothing) but keeps the value in the state object through a twin callback fired only when the value changes, so `ClassValue`/`StyleValue` functions still see it; on Server that mirror lags one round trip, which is the RND-STA-01 relaxation for mirrored fields. On an element with JS-owned style properties C# never renders `style`: it passes its merged style string to the twin and JS composes `element.style`. Prerender emits no JS-owned attribute; the configured `side`/`align` are not rendered until the first positioning pass. One named exception, the **optimistic convergent write**: at exit-animation end JavaScript may set `hidden` or remove the element from view before C#'s unmount batch arrives, permitted only because C#'s next render is already determined and renders the same value.

### 3. Contract and decision points (U2)

* **C# to JS.** `attach(elementOrId, options) → handle` returning an `IJSObjectReference` per attached part with `update(options)` and `dispose()`; no module-level `WeakMap` registries. Options are plain records serialized to JSON. The host element is located by the part's generated id when no `ElementReference` exists (substitute callback, ADR 02).
* **JS to C#.** The twin exposes `[JSInvokable]` methods; JavaScript calls them through a `DotNetObjectReference`, one message per state change and never per frame. `details.event` (EVD) is a C# snapshot record (`Kind`, `Type`, key and modifier fields, `PointerType`, `Detail`, `Buttons`, a target descriptor, `DefaultPrevented`, `TimeStamp`); EVD-API-05's `instanceof` form becomes the `Kind` field, and the `base-ui` placeholder is `Type = "base-ui"`.
* **Decision points.** Where a C# handler could change the outcome of a JS-owned dispatch (Escape close: EVD-EVT-11, EVD-DIS-01; drag start: SLD-PTR-01's default-prevented clause; consumer `PreventBaseUIHandler()` in `onKeyDown` against composite or button handling: EVD-EVT-26; button activation), the module calls one helper, `decide(point, eventInfo, fallbackPolicy)`. On WebAssembly it invokes the twin synchronously, runs the C# chain, and returns the verdict; the requirement is met as written. Elsewhere it returns the fallback policy. Fallback policies: **prevent and stop** for Escape when the owning popup is the innermost open one; **start** for drags; **run** for composite and button handling. Only synchronously completing consumer handlers can veto; async handlers run but cannot veto (documented, same as React).

### 4. Toolchain and delivery (T1)

TypeScript sources inside the library project; esbuild runs from an MSBuild target before the C# build (`node` required at build time only) and emits one ESM entry per module of §1 into `wwwroot/js/` with shared chunks (floating-ui is a chunk fetched when the first positioned popup attaches). Outputs are not committed; CI fails if the JS build did not run. Twins lazy-import their module on first use. `Blazix.Primitives.lib.module.js` is the JS initializer: it computes the platform flags once (UTL-ALG-11) and registers any custom event types. JavaScript unit tests (the FLT/UTL algorithm tests that are pure JS) run under a JS test runner in the same toolchain (ADR 06).

### 5. Portals

The `portal` module moves the portal node into the resolved container (`body` by default; a selector, element or shadow root via `Container`) after the part's first render and moves it back to its original parent before Blazor removes it (dispose), the pattern the old port proved on both modes. Blazor's logical tree is untouched, so cascading values, parameters and Blazor event bubbling keep working; the node is rendered `display: none` until moved so nothing paints in the wrong place.

### 6. Development order

Implementation and the browser test suite target Server first and WebAssembly second, because Server is the design floor (ADR 01) and surfaces every timing dependency that WebAssembly hides.

## Consequences

* ADR 01's relaxation table is updated: the conditional TRN-STA-04/05/07/09 rows are met (JS-owned flips); the Escape rows carry the "prevent and stop" fallback; new rows record consumer vetoes of JS-owned loops on Server and the mirrored-field lag of RND-STA-01.
* The ownership list is lint-checkable against the projection mappings and must be kept in each component doc's §5 table (a `Writer` column) when the docs are revised for implementation.
* JS modules are reviewable against one architecture doc each; the JS line count is expected to be of the old port's order but partitioned.

## Verification items

* Server preserves the order between JS-to-.NET invocations and Blazor event dispatches on one circuit. **Verified 2026-10-20 (both modes, the twin base):** `tests/browser/harness.spec.ts` "@verifies ADR-04/1 Server preserves the order between JS-to-.NET invocations and Blazor event dispatches on one circuit": the probe module's element listener reports through the twin before Blazor's document-level dispatcher sees the same click, and five clicks arrive in C# strictly alternating. [issue 6](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/6).
* A render queued inside a synchronous invoke on WebAssembly flushes before the JS listener returns (otherwise `decide()` must not rely on rendered state). **Verified 2026-10-20 (WebAssembly):** `tests/browser/harness.spec.ts` "@verifies ADR-04/2 on WebAssembly a render queued inside a synchronous invoke flushes before the JS listener returns": the C# `Decide` handler calls `StateHasChanged`, and the module reads the new attribute value before its listener returns. On Server the same module takes the fallback path (`data-verdict-source="fallback"`), as §3 prescribes. [issue 7](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/7).
* Moving a rendered node and restoring it before disposal never produces a Blazor `insertBefore`/`removeChild` on a foreign parent when the portal node has siblings in its logical parent (the old port avoided this by rendering the portal node as the only child of its wrapper; keep that shape).
* The esbuild MSBuild integration works on a machine without `node` when `BlazixSkipJsBuild=true` and prebuilt outputs are present (packaging path). Verified locally 2026-10-20 with `node` off `PATH` (steps in `src/Blazix.Primitives/js/CONTEXT.md`); **Verified 2026-10-20 by the manual job `.github/workflows/skip-js-build.yml` (M0 Track 4), dispatched on `main` at 5c5b76d: run 39442087672, success.** With `PATH` reduced to `/usr/share/dotnet:/usr/bin:/bin` and `node`/`npm` asserted absent, `dotnet pack src/Blazix.Primitives -c Release -p:BlazixSkipJsBuild=true` built the package from the `js-outputs` artifact of the first `main` run (15 entries asserted by `assert-package.mjs`), and a build with the outputs removed failed with the `The JavaScript build output ... is missing` message. The job is run before every tagged release (ADR 14 §1). [Issue 9](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/9) closed.

## Considered options

* Per-component modules (old port); one monolithic module (same organisation, worse loading).
* C# renders all state-derived attributes (Server lag on every transition start and side flip; `style` collision unsolved); JS owns every attribute on popup elements (removes those parts from bUnit).
* Uniform asynchronous contract (simpler, forfeits WebAssembly fidelity that costs one helper).
* Plain JS without a bundler (excluded by floating-ui); single committed bundle (invisible-edit hazard, pay-for-everything loading).
* Portals through Blazor sections (`SectionOutlet`): requires a consumer-placed outlet, breaks cascading values unless re-provided, and cannot express an arbitrary `container` element.

## Amendments

**2026-10-18, handover at attach (settles the ADR 03 prerender conflict, COL-ANI-14 / ACC-ANI-03, TRN §10 Q9, collapsible §16 Q11, accordion §16 Q14; decided in the second-pass review discussion).**

Decision 2's single-writer rule is refined to *initial writer C#, steady-state writer JavaScript*. For every JS-owned attribute or style property, C# renders the initial value once, in prerender and in the first interactive render: the consumer's merged `style`, the disabled-transition declarations (`animation-name: none`), the seeded `--available-width`/`--available-height`, the requested `side`/`align`. From the moment the twin's `attach` succeeds, the value C# rendered is frozen in the part's render output (so Blazor, which diffs against its own previous output, never writes the attribute again) and JavaScript owns the attribute: it composes `element.style` from the consumer's merged style string, passed on `attach` and on every `update(options)`, plus its own declarations. Consumer `ClassValue`/`StyleValue` functions are unaffected: the part evaluates them on every render; before attach the result is rendered, after attach it travels in `update`. The ownership list gains an "initial value rendered by C#" column. The sentence "prerender emits no JS-owned attribute; the configured `side`/`align` are not rendered until the first positioning pass" is withdrawn; prerender emits the values ADR 03 lists. The optimistic convergent write is unchanged.

**2026-10-18, module placement rule and five new modules (settles the ownership gaps found by the second pass: otp-field §16, toast §16, checkbox §16 Q12, context-menu §16 Q14, navigation-menu §16 Q14, tooltip §14, FFI §10 Q9, checkbox-group §16, slider §16 Q15, tabs §16 Q15).**

Decision 1's table is governed by a placement rule instead of being a closed list: *a behaviour that needs a browser API lives in the mechanism module of its architecture doc; an architecture doc whose JS-resident requirements have no module gets one; a component whose requirements include a frame-rate loop, a same-task measurement, selection, clipboard or composition handling, or a keyboard model that shares an algorithm with a JS loop gets a component module.* Adding a module still amends this table.

| Module | Owns | Doc |
|---|---|---|
| `form` (mechanism) | native submit interception (always prevented, re-issued by C# after validation; FFI-EVT-01, FFI-FRM-04, FRM-EVT-01), validity read and custom validity messages, disabled / form-owner eligibility, native label lookup, focus-first-invalid with `select()`, the Enter-to-submit path of FFI-KEY-02, `getFormValue` DOM reads | FFI |
| `otp-field` (component) | per-slot keydown, paste, composition and input handling: selection ranges, value distribution, focus movement, DOM value restore; reports one value change per edit | otp-field |
| `toast` (component) | Toast's own swipe gesture and its movement variables (the shared `swipe` tracker is Drawer's) | toast |
| `context-menu` (component) | `contextmenu` prevention, the document suppressor, the 500 ms long-press timer, the one-shot document `mouseup`; reports the open request with the anchor point and pointer type | context-menu |
| `navigation-menu` (component) | the viewport/popup size pass (frames, ResizeObserver, MutationObserver, close-size freeze) and the Content move into the Viewport through the `portal` module's move primitive | navigation-menu |

Placements under constraint C1: Slider thumb keydown moves into the `slider` module next to the drag loop, so `roundValueToStep`, `getSliderValue` and `validateMinimumDistance` exist once; the module moves the thumb optimistically and reports the value change, and a cancel rolls back through `update` (the drag loop's existing semantics). The `tabs` module measures the two Tab rects on activation and reports `activationDirection` with the index. Nested tooltip-trigger detection (TIP-PTR-17..22) lives in `interactions`. The ownership list of Decision 2 gains rows for `OtpField` slot values (JS-written on edit; C#-rendered initially and on `update`), `ContextMenu` anchor point (JS), and `NavigationMenu.Viewport`/`Popup` size variables (`--popup-width/height` and the viewport variables; JS); the existing `Toast.Root` row is unchanged.

**2026-10-18, dispatch-time decisions are JavaScript-only; no Blazor event directives on parts (settles UBT-KEY-03/04, OTP-FOC-08, OTP-KEY-13, OTP-EVT-16, OTP-PTR-02, OTP-KEY-01, OTP-KEY-19 and the Radio Enter suppression).**

1. Anything that must happen inside the browser's event dispatch — `preventDefault()`, `stopPropagation()`, pointer capture, clipboard and selection reads, composition handling — is done only by the JavaScript modules. Parts register C# handlers only for events whose result is state; events a module owns reach C# as twin callbacks, never as a second Blazor handler on the same element.
2. Consumer vetoes have two channels, by origin. From JavaScript: a consumer listener on the part's element or a descendant that calls `preventDefault()` is honoured because every module checks `event.defaultPrevented` wherever the Base UI source does; this holds on both render modes and keeps UBT-KEY-03/04 and the OTP veto requirements as written (including the non-cancelable focus/blur cases, where the flag is set although nothing is cancelled, as React's synthetic event imitated). From C#: `PreventBaseUIHandler()`, carried by `decide()`; synchronous on WebAssembly, the "run" fallback on Server. The `otp-field` module's mousedown, keydown and paste join the decision-point list with that fallback.
3. Blazor's event directives (`@on…:preventDefault`, `@on…:stopPropagation`) are never used by library parts and are rejected on them: they are applied by Blazor's document-level dispatcher after the modules' element-level listeners have run, so a module cannot see them, and they are unconditional. In development the pipeline throws (a new RND-EDGE diagnostic, message adapted to Blazor terms) when a directive-generated key (`__internal_preventDefault_*`, `__internal_stopPropagation_*`) appears in a part's unmatched bag or in a substitute element's attribute frames (ADR 02 frame rewriting); the message names the two sanctioned channels.
4. Library-internal suppressions (Radio's Enter suppression through UBT-KEY-03) are options of the owning module, not vetoes.

Verified 2026-10-18 (SDK 10.0.301, throwaway Razor class library): `<Part @onclick:preventDefault @onkeydown:stopPropagation />` compiles to `AddEventPreventDefaultAttribute(1, "onclick", true)` and `AddEventStopPropagationAttribute(2, "onkeydown", true)` on the component frame, i.e. attribute frames that reach `CaptureUnmatchedValues` (the key is the `__internal_preventDefault_<event>` / `__internal_stopPropagation_<event>` name `RenderTreeBuilder` uses; confirm the literal in the first L2 test). Combining `@onclick="H"` with `@onclick:preventDefault` on a component is already a compile error (RZ10010, duplicate parameter `onclick`, matched case-insensitively). The development-time diagnostic of item 3 is therefore implementable as a key check on the unmatched bag and on substitute-element attribute frames.

**2026-10-18, convergent write for Avatar; hovered-line coordinates are JavaScript's (settles avatar §16 item 8, preview-card §14 PRV-STA-13).**

The optimistic convergent write of Decision 2 (JavaScript may set a value C#'s next render is already determined to render) is extended from exit-animation `hidden` to the Avatar load status: the `dom` module's image-load primitive toggles the Image's and the Fallback's visibility at load time, and C# renders the same values when `loadingStatus` arrives; AVA-EDGE-02/03 therefore hold on both render modes. PreviewCard's hovered-line coordinates (PRV-STA-13) are owned by JavaScript end to end: the `interactions` module records them from the Trigger's hover handlers and the `positioning` module's inline-rect middleware reads them; for an open with no recorded pointer position the positioning module derives the point from the trigger rect. C# never writes them. The microtask mapping for PSH-STA-23 and FLD-FRM-17 is in ADR 01 (constraint C2, amended 2026-10-18).

**2026-10-18, untested Base UI quirks (settles context-menu §16 Q13 / menu §16 Q9, menu §16 Q10 / menubar §16 Q3, tooltip §16 Q16).**

Not replicated: CTX-KEY-07 — the port's `context-menu` module does not pass the nested close key to a root context menu, so a root context menu never closes with reason `list-navigation`; the doc keeps the Base UI description at SHOULD. Replicated as the source does: a plain menu opened on hover resolves `initialFocus` like any other open and moves focus into the popup (menu §16 Q10, menubar §16 Q3); the Tooltip Popup-leave close delay uses the Trigger's `closeDelay ?? 0` rather than the Provider's value (tooltip §16 Q16, to be reported upstream).


**2026-10-20, from the implementation revision (settles the ADR 04 gaps listed in `_qa-notes.md` "Implementation revision"; decided in the grilling of ADRs 07–16).**

*Ownership list (Decision 2), new rows:*

| Part(s) | JS-owned | Note |
|---|---|---|
| `Checkbox.Root` hidden input | the `indeterminate` DOM property | a `dom` primitive sets it on `attach`/`update` and re-asserts it after a native click on the hidden input inside the dispatch (CHK-DOM-13) |
| every Drawer part (`Root`, `Popup`, `Backdrop`, `Indent*`, `SwipeArea`) | `data-swiping` | uniform: `swiping` is a mirrored field; the Root's row in `drawer.md` is corrected from `C#` to `JS` |
| `Drawer.Viewport` | `--drawer-keyboard-inset` | the `drawer` module follows `visualViewport`; C# holds no field for it |
| `NavigationMenu.Positioner` | `--positioner-width`, `--positioner-height` | written by the `navigation-menu` module's size pass beside `--popup-width/height` (NAV-DOM-29) |

*Module owners (Decision 1, under the placement rule):* the live-region marker of UTL-A11Y-01..04 (text-node mutation after paint, the 200 ms restore, the iOS exclusion) is one `dom` primitive with its own timer; the Radio focus-driven selection of RAD-FOC-05 is an option of the `composite` module (RadioGroup passes it): the pending-navigation flag is set by composite's arrow handling and consumed in its focus handler, which clicks the hidden input synchronously, so no Radio module exists; the `slider` module classifies each thumb blur by `relatedTarget` (to a sibling thumb or outside) and reports one message for SLD-FOC-04 and SLD-FRM-06, C# applying field focus, touched and blur-mode validation.

*Decision points (Decision 3), added:* ChipRemove activation (CMB-KEY-10, CMB-PTR-10, CMB-EVT-09) with fallback **stop**; the closed-popup Escape clear (CMB-KEY-05) with fallback **stop when there is something to clear and the root is not inline**, decided in JavaScript from the `update` options `hasValue` and `inline`; the Radio focus-driven selection with fallback **run** (a consumer JavaScript `preventDefault()` on the non-cancelable focus event is honoured as the amendment of 2026-10-18 states). The `context-menu` module prevents the native menu and records the anchor synchronously on both modes; the open request of CTX-EVT-02/CTX-PTR-01 runs inside the dispatch on WebAssembly only (ADR 01 row).

**2026-10-20, the directive-key diagnostic is always on (M0 Track 1, issue 31; chosen by the user the same day).** Item 3 of the 2026-10-18 amendment ("in development the pipeline throws") reads: the pipeline throws in every build when a `__internal_preventDefault_*` or `__internal_stopPropagation_*` key appears in a part's unmatched bag or in a substitute element's attribute frames. See ADR 02 Amendments 2026-10-20.

**2026-10-21, the event snapshot of a request a C# handler starts (Decision 3; M1 track 4, issue 61 Q3; option (b), chosen by the user the same day).** Decision 3's `details.event` is the snapshot JavaScript takes inside the dispatch. A request that a part's C# handler starts has no such snapshot: CollapsibleTrigger's and Toggle's `onclick`, which runs after the `button` module, and any later C#-dispatched key. Such a request builds its snapshot from Blazor's event arguments with the internal `EventSnapshot.FromEventArgs(MouseEventArgs)` / `FromEventArgs(KeyboardEventArgs)`.
- `Kind` follows the argument type: `PointerEventArgs` → `PointerEvent`, `WheelEventArgs` → `WheelEvent`, other mouse arguments → `MouseEvent`, `KeyboardEventArgs` → `KeyboardEvent`.
- `Type`, `Detail`, `Buttons`, `AltKey`/`CtrlKey`/`MetaKey`/`ShiftKey`, `PointerType`, `Key` and `Code` are copied.
- What the C#-built snapshot lacks: `Target` is `null`, `DefaultPrevented` is `false` and `TimeStamp` is `0`. `Kind` reads `MouseEvent` for a click Chromium delivers as a `PointerEvent`; `IsInstanceOf(EventKind.MouseEvent)` holds for both.
- The keyboard-versus-pointer `detail` and the modifiers survive, which is what the EVD tests read.

The rejected alternatives:
- (a) The twin reports the click's snapshot. That costs a second message per click on Server (ADR 16 rule 6) and needs fragile pairing with Blazor's dispatch.
- (c) The `base-ui` placeholder. It loses every field.

The inverse, `snapshot.ToKeyboardEventArgs()`, is what the `activate` decision point hands a consumer's C# key handler. Tested by `tests/Blazix.Primitives.Tests/Events/EventSnapshotFromEventArgsTests.cs`.

**2026-10-21, the open input of an attached animated part and the decision point `reveal` (Decision 3; M1 track 5, issue 62; option B and the `reveal` proposal, chosen by the user the same day).**

*Contract, the open input.*
- Besides `update(options)`, the `transitions` module reads a part's open state from the attributes C# renders on the part's element.
- The part names them with a selector in its options, `openSelector`: for example `[data-open]` on the Collapsible Root and Panel, `[data-panel-open]` on the Trigger, and `:not([aria-hidden])` on a kept-mounted `Avatar.Image`.
- The module reads the selector in a `MutationObserver` callback, which runs after the render batch is applied and before paint. With a selector set, the applied DOM is the input and `update`'s `open` is not read after `attach`, so a late `update` cannot reopen a part that a newer batch closed.
- Decision 2's single writer stands: JavaScript writes `data-starting-style` and `data-ending-style` from `attach`, and they land with the batch that opens or closes an already-attached element on both modes.
- C# renders the initial status under the handover and keeps the mounted flag's rules for a newly rendered element. ADR 01 Amendments 2026-10-21 record the evaluation step in both languages.

*Decision points, added.*
- `reveal`: a `beforematch` on a hidden-until-found panel (TRN-ALG-07, TRN-ANI-32..34, TRN-EVT-09), with fallback **run**.
- Inside the dispatch the module removes `data-starting-style` and arms the one-shot motion skip before it decides, and restores both on `stop`. On WebAssembly the C# request commits the open inside the decision, and the open render's `update` re-enters the module before `decide` returns, so it finds them already applied.
- On Server, `run` is returned without a call. The request is then an invocation of the twin's `OnRevealRequest`, whose `Task<bool>` result says whether the part is open once the request ran. A `false` answer, with no open arrived, reverts the panel to `hidden="until-found"` on the next frame. The answer is asynchronous, so ADR 01 decision 1 holds: no synchronous invoke moves state.
- ADR 01 relaxation table, row added 2026-10-21.
