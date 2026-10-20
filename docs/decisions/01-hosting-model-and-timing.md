# ADR 01 — Hosting model and timing

| | |
|---|---|
| Status | accepted (2026-10-17) |
| Project | Blazix.Primitives (unofficial Blazor port of MUI Base UI v1.9.0) |
| Decides | which Blazor render modes the port must satisfy; how the seven canonical timing terms of `conventions.md` §4 map onto Blazor; where browser-API-dependent code lives |
| Affects | every requirement whose statement uses a timing term; seeded list in §Relaxation table |

## Context

Base UI's behaviour is specified with seven timing terms (same commit; post-commit, pre-paint; after paint; next frame; microtask; macrotask; on animations finished; plus "committed synchronously"). Blazor has two interactive render modes with different timing, and `InteractiveAuto` runs the same component code in both:

* **WebAssembly.** Verified against `dotnet/aspnetcore` `main`: `WebAssemblyRenderer.UpdateDisplayAsync` hands the batch to `Blazor._internal.renderBatch` through a synchronous `[JSImport]` (JS applies it synchronously and never calls back into .NET during the batch) and returns a completed task; `Renderer.InvokeRenderCompletedCalls` then runs `OnAfterRender` synchronously. `Renderer.DispatchEventAsync` calls `ProcessPendingRender()` in its `finally`, so a synchronous event handler's render is in the DOM before the browser's event dispatch ends. `OnAfterRender` is therefore post-commit, pre-paint, and "committed synchronously" holds for synchronous handlers.
* **Server.** `RemoteRenderer.UpdateDisplayAsync` returns `pendingRender.CompletionSource.Task`, completed only when the client acknowledges the batch (`OnRenderCompletedAsync`); `OnAfterRender` awaits it. Every C# observation of the DOM is after paint plus a round trip; nothing in C# can affect the dispatching event. Synchronous JS-to-.NET invocation does not exist.

The earlier port (Blazix.BaseUI, 2025-12 to 2026-08) ran on both modes with browser-API-dependent code in JavaScript and C# mirroring state through asynchronous callbacks. The user judged its behaviour acceptable on both modes, including animations; its failure was elsewhere (no specification to build against, shared-utility structure, test strategy). That split is therefore kept.

## Decision

1. **Both interactive render modes are first-class targets. Server is the design floor.** No C# code may depend on pre-paint execution, same-task commit, or frame/microtask scheduling. Component code must not branch on render mode; the only mode-dependent capability is synchronous JS-to-.NET invoke, which may be used on WebAssembly solely for the conditional-prevent cases below and never to move state.
2. **Everything that needs a browser API lives in JavaScript. C# owns state, rendering and the consumer contract.** The boundary is drawn per *loop*, not per function (constraint C1): a loop that reads and writes browser APIs at frame rate (pointer drag, scroll-tracked positioning, animation watching, swipe arbitration) is JavaScript end to end, arithmetic included; everything else is C# calling JavaScript primitives that do one DOM thing each. Consequently every ALG-tagged requirement is implemented in exactly one language. Two implementations of one algorithm (the old port's duplicated `roundValueToStep`) are forbidden. Amended 2026-10-18: locale-dependent number formatting and parsing (`Intl.NumberFormat` in Meter, Progress and NumberField) is C# through .NET globalization, because the browser's `Intl` cannot be consulted synchronously during a Server render; the output is equivalent, not byte-identical (percent spacing, currency patterns), which is a port relaxation of MTR-I18N-01..03, PRG-I18N-01..03 and NumberField's formatting rows.
3. **C# never emulates frames or microtasks** (constraint C2): no `Task.Yield`, `Task.Delay(0)` or `SynchronizationContext.Post` standing in for `requestAnimationFrame` or `queueMicrotask`. Those terms are implemented in JavaScript or not at all. Amended 2026-10-18: a Base UI microtask whose purpose is "after every synchronous write of this commit" (PSH-STA-23's close-on-trigger-unmount check, FLD-FRM-17's re-commit) maps to the render-batch boundary, `OnAfterRender`, which is not emulation but the boundary doing the microtask's job; a microtask whose purpose is ordering against a browser event inside the same task stays JavaScript.
4. **Timing-term mapping** (normative for every document that uses the term):

| Canonical term | Implemented by | WebAssembly | Server |
|---|---|---|---|
| in the same commit | one render batch (attributes derived from one state object land together) | met | met |
| post-commit, pre-paint — DOM-only work (attribute/style writes, measurement) | the JS primitive that performed the write, or a JS `MutationObserver`/`ResizeObserver` callback on the applied batch | met | met |
| post-commit, pre-paint — C# state work (registry flush, value-changed side effects) | `OnAfterRender` | met | after paint plus one round trip |
| prop-to-store sync (UTL-STA-19..22) | `OnParametersSet` (ADR 05 §1; amended 2026-10-18) | met | met |
| after paint | `OnAfterRender` on Server; a JS `requestAnimationFrame` on WebAssembly when paint ordering matters | met | met |
| committed synchronously | `StateHasChanged` inside the synchronous part of the handler | met for synchronous handlers | not met (see table below) |
| next frame; microtask | JavaScript only | met | met in JS; C# learns the result a round trip later |
| macrotask / N ms | C# timers (`UTL-EVT-01..09` semantics) when the result is state; JS timers when the result is DOM | met | timer exact; DOM consequence lands a round trip later |
| on animations finished | JS watcher (`TRN-ALG-03`) notifying C# | met | notification, unmount render and its batch: about two round trips after the animation ends |

## Relaxation table

Normative. Each row is a requirement whose stated timing Server cannot meet with the mapping above. Component docs cite this table from §9 instead of restating it; the WebAssembly test of each row runs unchanged, the Server test asserts the substitute column. Rows marked **met on both modes** are kept so the mechanism that makes them met is not undone.

| ID | Spec timing | Server substitute |
|---|---|---|
| TRN-ANI-03, TRN-ANI-09, TRN-ANI-10 | mounted flag cleared / completion callback committed in the same microtask as the exit animation ends | removal lands in the first render batch after the completion message, about two round trips later; the optimistic convergent write of ADR 04 §2 lets JS set `hidden` at animation end so the element is not visible in the meantime |
| EVD-EVT-18, PSH-ANI-03 | `trigger-hover` open committed synchronously | commit lands a round trip later; same-task animation queries do not see it |
| PSH-KEY-07 | focus-guard close committed synchronously | a round trip later |
| EVD-EVT-11, EVD-DIS-01, FLT-KEY-01 | Escape keydown `preventDefault()` / `stopPropagation()` decided by the consumer's `cancel()` / `allowPropagation()` inside the dispatch | the consumer's decision arrives a round trip after dispatch ends. JS applies the fallback policy of ADR 04 §3: prevent and stop whenever the owning popup is the innermost open one. WebAssembly meets the spec through the synchronous `decide()` path |
| SLD-PTR-01 (default-prevented clause), EVD-EVT-26 against composite/button key handling, drag-start vetoes generally | a consumer DOM handler in the same dispatch stops the part's JS-owned behaviour | on Server the loop starts (or the key is handled) regardless; WebAssembly honours synchronous consumer handlers through `decide()` (ADR 04 §3) |
| C#-origin consumer vetoes: UBT-KEY-03/04, OTP-FOC-08, OTP-KEY-13, OTP-EVT-16, OTP-PTR-02, OTP-KEY-01, OTP-KEY-19, NUM-KEY-11, NUM-EVT-14, NUM-PTR-02, NUM-FOC-04, SLD-API-22 and any similar consumer handler that must precede a JS-owned handler | a consumer `preventDefault()` in the same dispatch vetoes activation, slot logic or paste handling | met on both modes when the veto is issued from a consumer JavaScript listener (the modules check `defaultPrevented`); a C# consumer's veto is `PreventBaseUIHandler()`, synchronous on WebAssembly through `decide()`, a round trip late on Server (fallback "run"); Blazor event directives are rejected on parts (ADR 04 Amendments 2026-10-18) |
| PSH-STA-23, FLD-FRM-17 | check runs one microtask after the commit's synchronous writes | runs in `OnAfterRender`; on Server after paint plus a round trip, during which the popup stays open or the field keeps its pre-normalised value |
| NUM-STA-15 | scrub-area value changes committed synchronously | the `number-field` module moves the cursor and reports values per state change; the C# commit lands a round trip later on Server (added 2026-10-20) |
| AVA-EDGE-02, AVA-EDGE-03 | a cached image never shows the Fallback (status resolved before paint) | **met on both modes** by the optimistic convergent write of ADR 04 §2 extended to the Avatar load status (the `dom` module toggles Image/Fallback visibility on load; C# renders the same values when its state arrives); the status `data-*` attributes lag per the RND-STA-01 row |
| RND-STA-01 for mirrored fields (`side`, `align`, `anchorHidden`, `transitionStatus`, swipe/scroll flags per ADR 04 §2) and any JS-measured value that C# renders (e.g. `--toast-height`, TST-ANI-03; added 2026-10-20) | class/style functions and data attributes observe the same state in one pass | the data attribute is JS-written at frame timing; the C# state field (and so `ClassValue`/`StyleValue`) updates one round trip later |
| EVD-STA-07, CMP-STA-10, FFI registration (post-commit, pre-paint C# state work) | before paint | after paint plus a round trip; observable only as latency of dependent attribute updates |
| UTL-STA-19..22 (prop-to-store sync) | post-commit, pre-paint | **met on both modes**: runs in `OnParametersSet`, before the render that carries the new parameter, so no render observes a stale store (ADR 05 §1; earlier than Base UI, which needs one catch-up render). Amended 2026-10-18; this row replaces the earlier relaxation |
| TRN-STA-04, TRN-STA-05, TRN-STA-07, TRN-STA-09 | `data-starting-style` / `data-ending-style` flipped on the next frame | **met on both modes**: ADR 04 §2 makes the flip JS-owned with C# mirroring; listed so nobody moves the flip back to C# |
| Positioning (`FLT-POS-16`, `FLT-ALG-13`), slider drag (`SLD-PTR-04..11`), scroll-area thumb (`SCR-PTR-05`), drawer swipe (`DRW-PTR-21`, `DRW-ALG-01..05`) | frame-rate loops | met on both modes **because** C1 places the whole loop in JavaScript; listed so nobody moves them back to C# |
| JS-owned values that originate in C# state (ownership list: `indeterminate` on the Checkbox hidden input, consumer style after attach, and every `update`-carried value; added 2026-10-20) | written post-commit, pre-paint | the twin passes the value on `update`: pre-paint on WebAssembly (`OnAfterRender` runs before paint), one round trip later on Server; a native click on the Checkbox hidden input is re-asserted by the `dom` primitive inside the dispatch on both modes |
| CMB-KEY-10, CMB-PTR-10, CMB-EVT-09 (ChipRemove), CMB-KEY-05 (Escape while closed) (added 2026-10-20) | propagation stopped unless the consumer called `allowPropagation()` inside the dispatch | decision points of ADR 04 §3: `AllowPropagation()` is honoured synchronously on WebAssembly; on Server the fallback applies — **stop** for ChipRemove, and for the closed-popup Escape **stop when there is something to clear and the root is not inline** (Base UI's own rule minus the consumer) |
| CTX-EVT-02, CTX-PTR-01 (added 2026-10-20) | `onOpenChange` runs synchronously inside the `contextmenu` (or long-press) dispatch | the `context-menu` module prevents the native menu and records the anchor synchronously on both modes; the open request runs inside the dispatch on WebAssembly and a round trip later on Server, where a `Cancel()` can no longer restore the native menu |

## Consequences

* The JavaScript layer is larger than a WebAssembly-only port would need, and it is the only place where frame-rate behaviour exists. ADR 04 enumerates its modules and the C#/JS contract.
* Tests of timing requirements must run in a real browser against both modes (ADR 06); bUnit cannot observe any row of the mapping.
* `docs/README.md` must link this table, and every component doc's §9 cites it where its IDs appear.
* Static server-side rendering (prerender) is out of scope for behaviour but in scope for markup: generated ids must be identical between the prerendered and the interactive pass (RND-DOM-29). That mechanism is ADR 02's.

## Verification items

* `WebAssemblyRenderer.UpdateDisplayAsync` defers its completed task by one `WebAssemblyCallQueue` turn when `HasUnstartedWork` is true; confirm during implementation that this never crosses a browser task boundary (if it did, `OnAfterRender` could run after paint in that case).
* Measure the Server round-trip cost of the TRN-ANI-09 row on a realistic network before deciding whether ADR 04 adds the optimistic JS hide.

## Considered options

* **WebAssembly only.** Meets every timing term as written; rejected because it drops the common enterprise deployment and `InteractiveAuto`.
* **Both modes, Server best-effort.** Rejected because the Server half would be untested behaviour that `InteractiveAuto` users hit first.
* **Logic in C#, JS as thin primitives.** Rejected: on Server every per-event decision is a round trip late and the frame-rate loops lag visibly; `DRW-PTR-21` has no such form at all.

## Amendments

**2026-10-20, from the implementation revision (settles the ADR gaps listed in `_qa-notes.md` "Implementation revision"; decided in the grilling of ADRs 07–16).**

* Three relaxation rows added above: JS-owned values that originate in C# state (generic `update` row), the Combobox propagation decision points, and the ContextMenu open inside the `contextmenu` dispatch. SLD-FOC-04 and SLD-FRM-06 need no row: the `slider` module classifies each thumb blur by `relatedTarget` and reports one message, so the sibling-thumb rule is met on both modes and only the field-state latency of the existing "post-commit, pre-paint C# state work" row applies.
* **Intl rounding (NUM-STA-12).** The port implements the nine `Intl.NumberFormat` rounding modes (`ceil`, `floor`, `expand`, `trunc`, `halfCeil`, `halfFloor`, `halfExpand`, `halfTrunc`, `halfEven`) and `roundingIncrement` in C# as one ALG helper tested at L1 with Base UI's `validate.test.ts` titles, because `MidpointRounding` lacks `expand`, `halfCeil`, `halfFloor` and `halfTrunc`. Formatting itself stays "equivalent, not byte-identical" per the amendment of 2026-10-18 (.NET globalization).

