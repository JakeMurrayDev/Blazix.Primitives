# src/Blazix.Primitives/js

The TypeScript side of the library: one module per row of ADR 04 §1, the JS initializer, and the shared helpers that implement the twin contract of ADR 04 §3. Built by esbuild from MSBuild (ADR 04 §4); `wwwroot/` is the output and is ignored by git.

## Inputs

- `docs/architecture/*.md`: each mechanism module implements the JS-resident requirements of exactly one architecture document; a component module holds one component's loops (ADR 04 §1, placement rule 2026-10-18).
- `docs/decisions/04-js-interop-boundary.md` §3 (contract, decision points) and §4 (toolchain); ADR 01 (what Server cannot do); ADR 11 §5 (module-graph lint).
- The Base UI source under `reference/` for the algorithms and the vitest titles.
- `node_modules` at the repository root (`package.json`: esbuild, typescript, vitest, `@floating-ui/dom` 1.8.0).

## Job

- `twin.ts`: what every module receives from `attach(elementOrId, optionsJson, dotNetRef)` and hands back (`{ update(optionsJson), dispose() }`); `createTwinChannel` gives a handle its `decide` and `report`; `resolveElement`, `parseOptions`, `applyConsumerStyle` (handover at attach). No module-level registries: a handle's state lives in its closure.
- `decide.ts`: `decide(twin, point, event, fallback)`. The render mode is detected from the runtime, per channel, on the first decision: one `Ping` through `invokeMethod`, which Blazor Server's dispatcher rejects synchronously and WebAssembly's answers. WebAssembly then runs the C# chain inside the dispatch; everywhere else the fallback policy is returned without a call.
- `event-info.ts`: the event snapshot. Its property names mirror `Events/EventSnapshot.cs` (Track 2), which is the one place that defines them; `kind` is the DOM interface name.
- `platform.ts`: UTL-ALG-11 as `detectPlatform(navigatorData, webkitBackdrop)`, and `platform`, the page's flags computed once (and frozen) when the chunk loads; the `dom` module owns and re-exports both.
- `dom.ts` (entry `js/dom`): the helpers other modules import: `ownerDocument`, `ownerWindow`, `isElement`, `isHTMLElement`, `activeElement`, `contains`, `closest`, `getTarget`; `getCssDimensions`, `getElementTransform`, `getPseudoElementBounds`, `isMouseWithinBounds`, `getElementAtPoint`, `isScrollable(X|Y)`, `findScrollableAncestor`, `findScrollableTouchTarget`, `normalizeScrollOffset`, `getMaxScrollOffset`, `valueToPercent`; `observeResize` and `observeIntersection` (no-ops where the observer does not exist); `markLiveRegion`, `probeImage`, `deriveImageStatus`. Its `attach` runs the primitives `DomTwin` turns on: `checked` (set on attach and on every update, never inside a native click, so a toggle C# accepts is not undone before its commit; `checkedRestores` only makes an unchanged value arrive; re-asserted one task after the owning form's `reset`, switch §16 Q1), `indeterminate`, the live-region marker (after the next paint: one animation frame, then one task; restored 200 ms later if unchanged; never on iOS) and the image status (one report per change; the synchronous pass of a cached image reports only its result; every fallback id in `hideWhenLoaded`, a space-separated list, gets `hidden` the moment the image loads; a kept-mounted `<img>` is re-derived in a `MutationObserver` callback when a render changes its `src`, `srcset`, `sizes`, `crossorigin` or `referrerpolicy`, and that derivation is reported even when it equals the last report, because the element's `load` and `error` events reach C# directly). The Floating UI DOM predicates Base UI imports are re-implemented here so this module stays out of the positioning chunk.
- `button.ts` (entry `js/button`): UBT-ALG-01/02 as Base UI writes them, with two port points. A veto from a JavaScript listener that ran before the module (an earlier listener on the element, one on a descendant, or a capture listener on an ancestor) is `event.defaultPrevented`, checked where Base UI checks it; a veto from C# is the verdict of `decide(twin, 'activate', event, 'run')`, asked only where the module is about to act (prevent a key or click). The nested-handler flag is a symbol on the event, set right before each synthetic click, so two handles on one element click once. Also: `dispatchClickWithModifiers`, `isTouchLikePointerType`, `isElementDisabled`, disabled press suppression (click and pointerdown prevented, mousedown left alone), the composite `disabled` property correction, pointer-type classification, the native-mode development check, the press-and-hold repeater whose ticks, release and single-action clicks are reports, the hidden-input bridge (`hiddenInput`, `readOnly`; `findHiddenInput` finds the checkbox or radio input after the element by position; the input's clicks are stopped, a pre-prevented one is ignored, a read-only one reverted, and each toggle asks `decide(twin, 'toggle', click, 'run')`: a refusal on WebAssembly is the input click's `preventDefault()`, and a toggle not decided synchronously is reported as `OnToggle` with the new `checked`), and the Enter suppression (`suppressEnter`) of Checkbox and Radio.
- `transitions.ts` (entry `js/transitions`): TRN as a mechanism module.
  - The page-global frame scheduler: `requestFrame`, `cancelFrame`, `createFrameSlot` and, for tests, `resetFrameScheduler`. Every request made in one task shares one native `requestAnimationFrame`, and cancelling is an O(1) slot.
  - `getFiniteAnimations`.
  - `createAnimationsWatcher(element, waitForStartingStyleRemoved, batch)`, Base UI's `useAnimationsFinished`:
    - the first inspection runs on the next frame, or in the microtask after `data-starting-style` is removed;
    - a canceled animation is re-inspected for its replacement;
    - an abort signal stops the callback, the restart and the observer;
    - `batch` runs the completions ready in one microtask in one flush;
    - a missing element never completes;
    - the callback runs synchronously when `getAnimations` is missing or `globalThis.BASE_UI_ANIMATIONS_DISABLED` is set, read at each wait.
  - `evaluateTransition(state, deferEndingState)`: the evaluation step, a pure function. Its C# twin is `Interop/TransitionStatusRules.Evaluate`; one case table, `test-support/transition-evaluation.cases.json`, is read by the L3 and the L1 tests.
  - `createTransitionMachine(initial, { enableIdleState, deferEndingState }, onCommit)`, Base UI's `useTransitionStatus`: the evaluation step, then the three frame steps, each re-armed only when its dependencies change. `onCommit` is the per-pass hook a component module adds its own pass to.
  - `createTransitionAttributes(element)`: the diff writer of the two attributes.
  - `createHiddenController(element)`: `hidden="until-found"` on a change to hidden, the boolean-to-literal rewrite before paint, and the convergent boolean `hidden` at exit end.
  - `observeOpenInput(element, selector, onChange)`: the open input read from the attributes C# renders, in the `MutationObserver` microtask after the render batch, before paint.
  - `createRevealController(element, { open, request })`: the `beforematch` listener with the `reveal` decision point (fallback `run`, `consumeSkip()` for the opening pass). The revert runs after a 0 ms task and a frame when the request ran inside the dispatch (WebAssembly), and after C#'s answer and a frame otherwise (Server).
  - These factories are what the `collapsible` component module composes with `dom`'s reads. That module is built by the Collapsible track: the orchestrator placed the measurement passes there under the placement rule. This module stays Collapsible-unaware.
  - Its `attach`: the animated part's handle.
    - It starts from the `status` and `mounted` C# rendered (the handover) and is from then on the only writer of `data-starting-style` and `data-ending-style`. Each is written only when its value changes, as a renderer diffs.
    - With `openSelector` set, the open input is read from the DOM after every batch. A late `update` cannot reopen a part a newer batch closed. Without it, the input is `update`'s `open`.
    - Reports:
      - `OnStatusChange`: status literal and mounted, once per change;
      - `OnTransitionComplete`: `open: false` when an exit finished, standing for the mounted and status change; `open: true` for an enter when `reportOpenComplete`;
      - the invocation `OnRevealRequest`: when no C# chain ran inside the dispatch; it resolves with whether the part is open after the request.
    - Exit end, in the completion pass: it removes `data-ending-style` and sets `hidden` on an element C# is about to remove or hide, which is the convergent write.
    - `hidden="until-found"` while hidden, the persisted `data-starting-style` while hidden, a `MutationObserver` that turns a boolean `hidden` C# renders into the literal before paint, and no `hidden` while open.
    - `unmountRequests` lets C# end an exit it learned of elsewhere; a request that arrives after a reopen is dropped. With `externalExit` no exit watcher is armed, so only such a request ends the exit.
    - The consumer style is applied only when `options.style` is set.
    - A repeated `attach` of the same element returns the live handle (a symbol on the element, not a registry); `dispose` is idempotent.
- `collapsible.ts` (entry `js/collapsible`, a component module): the Collapsible panel's measurement passes (Base UI's `useCollapsiblePanel`), composed from the `transitions` factories (the machine with its per-pass hook, the watchers, the transition attributes, the hidden controller, the open input, the reveal controller, the frame slots) and `dom`'s `ownerWindow`.
  - Per element, a session: animation type detection (`getAnimationType`, reported once per change with the mixed flag); the opening pass (measure and hold the size, the alignment reset until the next frame, the forced idle of a `'none'` or reveal open, the `0s` override of a reveal open kept until the next close); the closing passes (the cached size of a keyframe close, the held size, the zero-size and no-motion exits, the exit watcher one frame after `'ending'`); the open side's completion that clears the held size; the one-shot `animation-name: none`; the persisted starting style and `hidden="until-found"`; the convergent `hidden` at exit end.
  - The element's whole `style` is the module's from `attach`: the consumer's string (`options.style`), then its own declarations (the size variables, the suppression), then the temporary overrides, each restored to what the layers below say.
  - After a closing measurement it computes the element's style, so the closing transition starts from the held size (the ending flip lands in the next frame's callback, before that frame computes styles).
  - `update` re-resolves a disconnected element by `panelId`: a new element starts a fresh session from what C# rendered, and while no element exists an update whose status is `'ending'` ends the exit.
  - Reports: `OnForcedIdleChange`, `OnTransitionComplete` (`open: false`), `OnAnimationTypeChange`, and the reveal (`reveal` decision point, or the `OnRevealRequest` invocation).
- `composite.ts` (entry `js/composite`, a mechanism module): the roving group of CMP, as RadioGroup reaches it.
  - The index helpers as CMP §5.1 writes them (`isIndexOutOfListBounds`, `isListIndexDisabled`, `isHiddenByStyles`, `isElementVisible`, `findNonDisabledListIndex`, `getMinListIndex`, `getMaxListIndex`, `getNextListIndex`), `isModifierKeySet`, `getFallbackIndex`, `scrollIntoViewIfNeeded`, `isNativeInput`, `ACTIVE_COMPOSITE_ITEM`, `COMPOSITE_KEYS`, `MODIFIER_KEYS`, and the decision-point names `NAVIGATE_POINT` and `SELECT_POINT`.
  - `createCompositeRegistry()`: the item registry keyed by element (`register(element, { index?, metadata? })`, `unregister`, `flush`, `indexOf`, `subscribe`, `dispose`). One rebuild per `flush` (CMP-ALG-17): document order with explicit slots reserved, disconnected elements and negative indexes left out, publication only on a change. Move detection observes the lowest common ancestor of adjacent automatic items, re-installed after each rebuild, and is a no-op without `MutationObserver`. Explicit indexes and metadata exist on this side only.
  - Its `attach`: the root's handle.
    - It resolves the items' ids from the root's document or shadow root on `attach` and on every `update`, so a replaced element is found again; one `update` that changes the items rebuilds once.
    - The first publication and every later one adjust the highlight (CMP-ALG-18); from `attach` it is the only writer of every item's `tabindex`, `"0"` on the highlighted index and `"-1"` elsewhere, in the task that moves the highlight, never on an item whose registration carries a consumer `tabindex`.
    - `keydown` on the root: CMP-ALG-14 without the grid branch and without `onLoop`. Nothing is prevented or stopped unless the index changes; the decision point `navigate` (fallback `run`) is asked only then, before the module acts; the focus moves one microtask later.
    - `focusin` on the root: the focused item's index becomes the highlight without scrolling; a native text input selects its value; with `selectOnNavigation` and a pending arrow, the item's next-sibling `<input>`, unless disabled or read-only, is clicked with `dispatchClickWithModifiers` after the decision point `select` (fallback `run`), and the flag clears.
    - A capture `keydown` sets the pending flag for any key starting with `Arrow`, with any modifiers. A `focusout` whose related target is outside the root (shadow-aware `contains`, or none) clears it and, when asked, reports `OnFocusLeave`.
    - `mousemove` focuses a hovered, enabled item only with `highlightItemOnHover`. Reports: `OnHighlightChange` (when asked, once per change), `OnFocusLeave`, and `OnKeyHandled` (with `reportHandledKeys`, a key it handled and stopped where no C# chain ran inside the dispatch). It writes no attribute on the root. Handlers read the options of the latest `update`. A repeated `attach` of the same element returns the live handle; `dispose` is idempotent.
  - Not built: grid, `onLoop`, the keyed registry (CMP §10 Q1), the keyboard relay, a consumer-owned highlight, `onMapChange`, labels and `guess`, list navigation, typeahead.
- `form.ts` (entry `js/form`): the field and form infrastructure's browser half (field half from M2 track 1).
  - Pure helpers: `getDefaultFormSubmitter`, `isEligibleInput` (`:disabled` and the form owner), `readNativeValidity` (the base state, with the field's own message read out), `applyOwnCustomValidity` (the own message and the custom message it displaced, kept on the element under a symbol; `\r\n` and `\r` become `\n`; never written to a barred input), `getControlById` (shadow-aware) and `discoverLabel`.
  - `attach` with `control` options: value tracking (one `input` report per change of the value), `blur`, `keydown` (Enter on an input always, other keys only with `reportKeys`), the Enter commit's 0 ms task when the form owner is the enclosing form, the focus redirect to `focusTarget`, the active-element read at attach and on re-enable, focusing an `autoFocus` control at attach when nothing has focus, and the value ownership from attach (`defaultValue` and the unedited live value; a controlled value re-asserted on `update`). An increased `revision` is answered with a `sync` report, and the custom message is applied from `update`.
  - `attach` with `label` options: the double-click guard on a native label, and on a non-native one `pointerdown` prevented and click-to-focus with `focusVisible`. The nested-control guard walks out of shadow roots. Each step first asks the decision point `label` when the consumer has a pointer handler, and the development mismatch report is sent at attach and when `native` changes.
  - A repeated `attach` of the same element returns the live handle (a symbol on the element).
- `initializer.ts` → `wwwroot/Blazix.Primitives.lib.module.js`: imports `platform` and registers custom event types (none yet).
- `build.mjs`: the esbuild step. One ESM entry per `BlazixJsEntry` item of the csproj into `wwwroot/`, shared code as `js/chunks/*`, `legalComments: 'inline'` so the Floating UI banner survives, sourcemaps and no minification in Debug.

## Traceability (the sources carry no specification references)

- `twin.ts`, `decide.ts`: ADR 04 §3 (contract, decision points, fallback policies), ADR 02 decision 1 (the substitute callback passes the generated id), ADR 04 Amendments 2026-10-18 (handover at attach), ADR 01 decision 1 (the synchronous invoke serves decisions only).
- `event-info.ts`: ADR 04 §3, EVD-API-04 (the placeholder) and EVD-API-05 (`kind`).
- `platform.ts`: UTL-ALG-11, UTL-EDGE-01..09 (shared-utilities.md §4.16).
- `dom.ts`: shared-utilities.md §4.10 (UTL-DOM-18..22), §4.11 (UTL-DOM-23..31, UTL-ALG-12..14), §4.13 (UTL-A11Y-01..04), §4.16; switch.md SWT-DOM-08, SWT-EVT-04; checkbox.md CHK-DOM-13; avatar.md AVA-ALG-01/02, AVA-STA-09..13, AVA-EVT-02; ADR 04 §1 dom row and its 2026-10-18, 2026-10-20 and 2026-10-22 amendments.
- `button.ts`: button-behavior.md in full (UBT-ALG-01..08, UBT-KEY, UBT-PTR, UBT-EVT-04..10, UBT-EDGE-01..07); ADR 04 §3 (decision points `activate` and `toggle`, fallback `run`; Amendments 2026-10-22), ADR 12 Amendments 2026-10-22 item 4 (the bridge) and Amendments 2026-10-18 (dispatch-time decisions are JavaScript's); ADR 01 decision 1 (a tick is a report).
- `transitions.ts`: transitions-and-mounting.md (TRN) and its consumers' rows.
  - TRN-ALG-01..03 and TRN-ALG-07, TRN-ANI-04..15, TRN-STA-03..11/13, TRN-DOM-01/02/08/09, TRN-EDGE-01..03/05/06/08, TRN-EVT-09.
  - ADR 04 §1 (transitions row), §2 (every animated part; the convergent write), §3 (decision point `reveal`, fallback `run`; open with the orchestrator) and Amendments 2026-10-18 (handover at attach).
  - ADR 01 relaxation table:
    - TRN-STA-04/05/07/09 met on both modes, because the flips are frame-timed here;
    - TRN-ANI-03/09/10 relaxed, with the removal landing about two round trips after the exit on Server and the convergent `hidden` set at exit end meanwhile;
    - RND-STA-01 for the mirrored `transitionStatus`.
  - ADR 06 §1 (the synchronous path at L3) and §2 (the `BASE_UI_ANIMATIONS_DISABLED` switch).
  - Port decisions of 2026-10-21, chosen by the user (ADR 01 and ADR 04 Amendments 2026-10-21):
    - The open input of an attached element is read from the applied batch through `openSelector` (TRN-STA-03..10 on Server; ADR 01 decision 4's "post-commit, pre-paint, DOM-only work" row). The evaluation step exists in both languages, pinned by the shared case table (ADR 01 decision 2 note).
    - `beforematch` is the decision point `reveal`, fallback `run`, whose Server revert waits for C#'s answer plus a frame (TRN-ANI-33/34).
    - ADR 01's relaxation table has the three rows added 2026-10-21: the same-commit attributes of an attached element, met on both modes; `Avatar.Image` without `keepMounted`; the reveal.
    - The measurement passes live in a `collapsible` component module (decided by the orchestrator under the placement rule).
- `collapsible.ts`: collapsible.md §9 (COL-ANI-01..30), COL-DOM-12..18, COL-STA-04/06, COL-EVT-04/05, COL-EDGE-03; ADR 04 §1 (the `collapsible` component module, Amendments 2026-10-21), §2 and Amendments 2026-10-18 (handover at attach); ADR 01 relaxation table (the rows of 2026-10-21 for an attached element, a new element, the reveal; TRN-ANI-03/09/10 with the convergent write).
- `composite.ts`: composite-and-list-navigation.md (CMP) as M2 reaches it (ADR 12 Amendments 2026-10-22 item 3): CMP-ALG-01..06, 14..19, CMP-STA-01..30, CMP-KEY-01..11, 15..17, CMP-PTR-01/02, CMP-DOM-01..03, CMP-I18N-01..03, 06, CMP-EDGE-06, 08, 12..14, 18; RDG-STA-06, RDG-KEY-03, RAD-FOC-05, RDG-FOC-04/05. ADR 01 decision 2 (the registry is JavaScript's); ADR 04 §1 (composite row), §3 (decision points `navigate` and `select`, fallback run, names chosen by the user 2026-10-22) and Amendments 2026-10-20 (the RadioGroup options) and 2026-10-22 (the items' `tabindex` from attach). The rtl titles keep `describe.skipIf(isJSDOM)` and run at L4 (`tests/browser/composite.spec.ts`).
- `form.ts`: field-and-form-infrastructure.md (FFI-ALG-02/04/08/10/16, FFI-FRM-08..10, FFI-STA-09, FFI-PTR-01..04, FFI-FOC-05, FFI-EDGE-02/04/06/07), field.md (FLD-API-15/16, FLD-DOM-11, FLD-KEY-01/04, FLD-FOC-04, FLD-PTR-01..04); ADR 04 §1 (form row) and Amendments 2026-10-18 and 2026-10-22 (the listener ownership, value ownership, the Enter task, the `label` decision point, autofocus at attach); ADR 01 Amendments 2026-10-22.
- `initializer.ts`, `build.mjs`: ADR 04 §4, ADR 07 (the Floating UI licence banner).

## Toolchain rules

- The csproj target `BlazixBuildJs` runs `node js/build.mjs` before the static-web-assets pipeline enumerates `wwwroot` (`BlazixRegisterJs`, `BeforeTargets="ResolveProjectStaticWebAssets"`), so generated files are packed. `node` is needed at build time only.
- `dotnet build -p:BlazixSkipJsBuild=true` skips the step and fails with a clear message if an output is missing. Verified locally with node off `PATH`:
  1. `dotnet build src/Blazix.Primitives -c Release` with node on `PATH` (produces `wwwroot/`).
  2. `PATH=$(echo "$PATH" | tr ':' '\n' | grep -v node | paste -sd: -) dotnet build src/Blazix.Primitives -c Release -p:BlazixSkipJsBuild=true` → succeeds.
  3. `rm -r src/Blazix.Primitives/wwwroot` then step 2 → fails with the `BlazixRegisterJs` error naming the missing output.
  The manual CI job `.github/workflows/skip-js-build.yml` (ADR 11 §1, [issue 9](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/9)) repeats this on a runner without Node from the `js-outputs` artifact of the latest `main` build: run it before every tagged release.
- Adding a module: a `<BlazixJsEntry Include="js/<module>.ts" Out="js/<module>" />` item, `js/<module>.test.ts` beside it (L3, vitest in jsdom, against `test-support/fake-twin.ts`, which records reports and decisions and behaves like Server or, with `synchronous: true`, like WebAssembly), and a `<Module>Twin` in `Interop/` with its options record in `BlazixJsonContext`.
- L3 titles are Base UI's where a Base UI test observes the module, kept verbatim; other tests carry the port's own titles. Browser-only Base UI tests keep their `it.skipIf(isJSDOM)` and run nowhere until a module-level browser runner exists.
- A module that owns no style property never calls `applyConsumerStyle`: `dom` and `button` ignore `style`; `transitions` applies it only when the part hands its style over (`options.style` set).
- A mechanism module never imports a component module (ADR 04 §1); `module-graph.lint.test.ts` (L3, ADR 11 §5) enforces it, and a component module imports only mechanism modules, the shared helpers and `@floating-ui/dom`. `module-graph.lint.ts` holds `moduleKinds`, the one list that names every module's kind: adding a module adds a row there (and amends ADR 04 §1), or the lint fails on the file it does not know. The two `*.lint.*` files and `test-support/` (the L3 fake twin) are not modules of the library.

## Outputs

- `wwwroot/Blazix.Primitives.lib.module.js` (the initializer) and `wwwroot/js/<module>.js` with `wwwroot/js/chunks/*`, served at `_content/Blazix.Primitives/...` and packed as static web assets.

## Human check

- Every `decide()` call names its point and its fallback policy from ADR 04 §3; no module moves state through a synchronous invoke (ADR 01 decision 1).
- Every module checks `event.defaultPrevented` where the Base UI source does (ADR 04 Amendments 2026-10-18).
- One message per state change, never per frame (`report`).
