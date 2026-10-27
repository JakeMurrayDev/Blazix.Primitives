# Base UI v1.9.0 — behavioral requirements

Requirements specifications derived from reverse-engineering the MUI Base UI React component library, written so the library can be re-implemented in another language and UI framework. The implementation project is **Blazix.Primitives**, an unofficial Blazor (C#) port; cross-cutting implementation decisions are recorded in [decisions/](decisions/) (see below). Each document describes observable behavior (DOM output, ARIA, keyboard and pointer interaction, callbacks, form integration, lifecycle) with traceable requirement IDs, in the spirit of the WAI-ARIA Authoring Practices pattern pages.

Start with [conventions.md](conventions.md) (scope, ID scheme, writing rules). Cross-document fixes queued for the QA pass live in [_qa-notes.md](_qa-notes.md). The source being analyzed is pinned at `reference/base-ui/` (tag `v1.9.0`, commit `ad7ecda`).

## Reading order

1. `architecture/rendering-and-composition.md` — how every part renders, element substitution, state attributes.
2. `architecture/event-details-and-reasons.md` — the callback contract used by every component.
3. `architecture/button-behavior.md`, `architecture/composite-and-list-navigation.md`, `architecture/floating-and-positioning.md`, `architecture/popup-store-and-handles.md`, `architecture/transitions-and-mounting.md`, `architecture/field-and-form-infrastructure.md`, `architecture/shared-utilities.md`.
4. Component documents, simplest first: `switch`, `collapsible`, then the rest.

## Status

| Document | Prefix | Status | Notes |
|---|---|---|---|
| architecture/rendering-and-composition.md | RND | approved | 123 reqs; conformance obligations in §4.9 |
| architecture/event-details-and-reasons.md | EVD | approved | 83 reqs; reason matrix in §4.3.7 | |
| architecture/composite-and-list-navigation.md | CMP | approved | 225 reqs; keyboard matrices §4.5-4.6, algorithms §5; revised 2026-10-27 (M2 track 3, M3 tracks 1, 5 and 6; the M5 rows listed in §10 items 12 to 15) | |
| architecture/floating-and-positioning.md | FLT | approved | 221 reqs; engine middleware in §5; revised 2026-10-27 (M4 track 1: the portal rows; M4 track 4: the focus manager; the rows open to tracks 1 and 3 and to M5 listed in §10 item 15) | |
| architecture/popup-store-and-handles.md | PSH | approved | 208 reqs; shared popup props in §3; revised 2026-10-27 (M4 track 1: the popup store, handles and suites; §10 item 12) | |
| architecture/transitions-and-mounting.md | TRN | approved | 94 reqs; state machine in §4.1; revised 2026-10-27 (M4 track 1: the popup host; §10 item 11) | |
| architecture/field-and-form-infrastructure.md | FFI | approved | 126 reqs; control registration matrix in §6; revised 2026-10-23 (M2 track 1, PRs 1a and 1c) | |
| architecture/button-behavior.md | UBT | approved | 100 reqs; revised 2026-10-27 (M3 tracks 2 and 4: composite mode, the input item option) |
| architecture/shared-utilities.md | UTL | approved | 155 reqs; includes DirectionProvider, CSPProvider, useMediaQuery, store; revised 2026-10-27 (M4 track 1: the scroll lock, §4.8) |
| components/accordion.md | ACC | implemented | 101 reqs (opus) | builds on collapsible |
| components/alert-dialog.md | ADL | approved | 30 reqs + adoption (opus) | builds on dialog |
| components/autocomplete.md | AUT | approved | 83 reqs + adoption (opus) | builds on combobox |
| components/avatar.md | AVA | implemented | 58 reqs (opus) | |
| components/button.md | BTN | implemented | 29 reqs (opus) | |
| components/checkbox.md | CHK | implemented | 151 reqs; revised 2026-10-23, M2 tracks 5–6 |
| components/checkbox-group.md | CKG | implemented | 83 reqs; revised 2026-10-27, M4 track 1 (proposed): the five portal titles of CKG-STR-02, CKG-FRM-03/06 and CKG-FOC-02 green at L4 on the test-only portal |
| components/collapsible.md | COL | implemented | pilot; 104 reqs |
| components/combobox.md | CMB | approved | 247 reqs |
| components/context-menu.md | CTX | approved | 95 reqs (opus) | builds on menu |
| components/dialog.md | DLG | approved | 191 reqs |
| components/drawer.md | DRW | approved | 152 reqs (opus) | builds on dialog |
| components/field.md | FLD | implemented | 143 reqs; owns normative Field.Control text; revised 2026-10-23, PR 1c; rows open to tracks 4–8 and M5/M6 listed in PR 1c |
| components/fieldset.md | FST | implemented | 32 reqs; revised 2026-10-23, PR 1c; rows open to tracks 5–8 and M5 listed in PR 1c | |
| components/form.md | FRM | implemented | 48 reqs; revised 2026-10-23, M2 track 2 and PR 1c; rows open to tracks 4–5 and M5/M6 listed in PR 1c | |
| components/input.md | INP | implemented | 19 reqs; alias of Field.Control; revised 2026-10-23, PR 1c; INP-STR-03 open to M6 | |
| components/menu.md | MNU | approved | 308 reqs | |
| components/menubar.md | MBR | approved | 91 reqs (opus) | builds on menu |
| components/meter.md | MTR | implemented | 52 reqs (opus) | |
| components/navigation-menu.md | NAV | approved | 208 reqs |
| components/number-field.md | NUM | approved | 177 reqs | |
| components/otp-field.md | OTP | approved | 169 reqs | |
| components/popover.md | POP | approved | 214 reqs |
| components/preview-card.md | PRV | approved | 158 reqs |
| components/progress.md | PRG | implemented | 55 reqs (opus) | |
| components/radio.md | RAD | implemented | 99 reqs; revised 2026-10-23, M2 track 7 |
| components/radio-group.md | RDG | implemented | 86 reqs; revised 2026-10-27, M4 track 1 (proposed): RDG-FRM-04 and RDG-STR-02's portal clause green at L4 on the test-only portal |
| components/scroll-area.md | SCR | approved | 135 reqs | |
| components/select.md | SEL | approved | 332 reqs | |
| components/separator.md | SEP | implemented | 14 reqs (opus) | |
| components/slider.md | SLD | approved | 191 reqs | |
| components/switch.md | SWT | implemented | pilot; 117 reqs; revised 2026-10-23, M2 track 4 (SWT-API-12 withdrawn) |
| components/tabs.md | TAB | implemented | 156 reqs (7 withdrawn for the port); revised 2026-10-27, M3 track 5; TAB-FOC-10's popup clause open to M4 | |
| components/toast.md | TST | approved | 186 reqs |
| components/toggle.md | TGL | implemented | 57 reqs; revised 2026-10-27, M3 tracks 2 and 4 | |
| components/toggle-group.md | TGG | implemented | 71 reqs; revised 2026-10-27, M3 tracks 3 and 4 | |
| components/toolbar.md | TLB | implemented | 94 reqs; revised 2026-10-27, M3 track 4; TLB-KEY-15, TLB-FOC-06 and the popup clauses open to M4 and M5; the non-native Switch shape open to issue 115 | |
| components/tooltip.md | TIP | approved | 169 reqs |

Out of scope for v1.9.0: `filter-dropdown`, `internals/temporal*` (not exported).

## Phases

1. **Scaffold** — conventions, templates, brief. Done.
2. **Architecture + pilot** — nine architecture docs and two pilot component docs written in parallel; pilot output used to refine the template.
3. **Components wave 1** — all independent components.
4. **Components wave 2** — derived components (accordion, alert-dialog, autocomplete, context-menu, drawer, menubar), citing their base docs.
5. **QA** — cross-document consistency, ID uniqueness, completeness against the API outline. Done 2026-10-14 for the author-flagged discrepancies; the full per-document second pass (status → `reviewed`) was done 2026-10-18 (see phase 5 status below).
6. **Sign-off** — user review; status → `approved`. No implementation before this. Done 2026-10-18, after the second pass (see phase 6 status below).

Phase 5 status (2026-10-14 ~05:00): QA reconciliation complete — every author-flagged cross-document discrepancy applied or declined with reason (see `_qa-notes.md`); lint clean. All 47 documents remain `draft`: the reconciliation pass resolved known discrepancies but was not a full second-pass review of each document. Next: the per-document second pass, then user sign-off (phase 6).

Phase 6 status (2026-10-18): a previous session marked all 47 documents `approved` without the second pass; the user withdrew that the same day. The second pass then ran the same day (one Opus 5.5 reviewer per document or small group: citation spot-checks weighted to KEY/PTR/FOC/EVT/ANI, completeness against the API outline and `*DataAttributes.ts` / `*CssVars.ts`, cross-reference resolution, §14/§8 pointers to the decision records, coverage of every test title via `_tools/test_titles.py`, §16/§10 re-verification), and every document is now `reviewed`; `_qa-notes.md` records the substantive corrections; the decisions the review raised were taken the same day and recorded as dated amendments in `decisions/01`, `02`, `04`, `05` and `06` (handover at attach, module placement rule and five new modules, JavaScript-only dispatch-time decisions, microtask mapping, store-sync timing, controlled-mode exceptions, untested quirks, React-assuming statements deferred to the implementation revision). The user approved all 47 documents the same evening (status → `approved`); implementation may begin under the decisions in `decisions/`, starting with the implementation revision of the documents planned in `decisions/06-testing-harness.md`. The implementation revision planned in `decisions/06-testing-harness.md` (layer tags on `Test:` lines, a `Writer` column per ADR 04 §2, relaxation-table citations per ADR 01) follows approval. No implementation before sign-off.

7. **Implementation** — per `decisions/12-implementation-order-and-milestones.md`, in the repository `Blazix.Primitives` (`decisions/07-repository-hosting-and-licensing.md`); a component moves to `implemented` under `decisions/13-document-status-and-amendments.md`.

Implementation revision (2026-10-20): per `decisions/06-testing-harness.md`, every document now carries the three marks of `conventions.md` §3.4 — a test-layer tag on every `Test:` line (`[L1]` xUnit, `[L2]` bUnit, `[L3]` vitest, `[L4]` Playwright; `docs/_tools/apply_layers.py`), a `Writer` column (`C#`, `JS`, `C# → JS`) on the attribute tables of every component doc, and a *Server relaxations* list naming each document's IDs in ADR 01's relaxation table — and the React-assuming statements are rewritten or marked withdrawn under ADR 06's three rules. Lint: 47 docs, 6,040 requirements, 0 untagged `Test:` lines. Open ADR gaps found during the revision are listed in `_qa-notes.md`.

## Decisions

Architecture decision records for the Blazor port live in `decisions/<nn>-<slug>.md`. They are written only after the user settles a decision in a grilling session; each names the requirement IDs it affects. Component docs cite them where a requirement's timing or mechanism depends on one.

| ADR | Title | Status |
|---|---|---|
| [01](decisions/01-hosting-model-and-timing.md) | Hosting model and timing — both interactive render modes, Server as design floor, browser-API code in JS, timing-term mapping, Server relaxation table | accepted 2026-10-17; amended 2026-10-18 (microtask classes, store sync in `OnParametersSet`, formatting via .NET globalization, new relaxation rows) and 2026-10-20 (`update`-carried values, Combobox propagation points, ContextMenu dispatch, Intl rounding in C#); amended 2026-10-22 (label and legend ids publish in the creating batch); amended 2026-10-23 (`Field.Error`'s close through `update`); amended 2026-10-26 (consumer key handlers between a composite item and its root); amended 2026-10-27 (the M4 popup rows: hover timers in JavaScript, the newly created popup's gap, the `press` and `guard` points, the relay) |
| [02](decisions/02-render-pipeline-and-attribute-merging.md) | Render pipeline, element substitution and attribute merging — both `render` forms via frame rewriting, two class/style channels, veto as `EventArgs` extension, `PartBase<TState>` over one static pipeline, scoped id counter | accepted 2026-10-17; amended 2026-10-17 by ADR 05, 2026-10-18 (Blazor event directives rejected, no `inputRef`, four React-only IDs withdrawn for the port) and 2026-10-20 by ADR 08 (`PreventPartHandler()`, `ClassValue`/`StyleValue`); amended 2026-10-20 after the union-binding spike (`Render`/`RenderFunction` as two parameters, function-typed `ClassValue`/`StyleValue`); amended 2026-10-20 (always-on errors); amended 2026-10-22 (`inputRef` withdrawn on the form controls; content after the host element); amended 2026-10-26 (a never-projected state field); amended 2026-10-27 (typed callbacks of a component substitute stay typed); amended 2026-10-27 (content before the host element: `BuildBeforeElement`; the opt-in host wrapper) |
| [03](decisions/03-positioning-engine.md) | Positioning engine — bundle the vendored `@floating-ui/dom` 1.8.0, port only Base UI's middleware and pipeline assembly in JS | accepted 2026-10-17 |
| [04](decisions/04-js-interop-boundary.md) | JavaScript interop boundary — one JS module per mechanism plus component loops, single-writer attribute ownership with C# mirroring, twin contract with WebAssembly-only synchronous decision points, TypeScript + esbuild, DOM-move portals, Server-first development | accepted 2026-10-17; amended 2026-10-18 (handover at attach, module placement rule and five new modules, JavaScript-only dispatch-time decisions, Avatar convergent write, quirks) and 2026-10-20 (ownership rows for Checkbox `indeterminate`, Drawer, NavigationMenu; `dom`, `composite` and `slider` owners; Combobox and Radio decision points); amended 2026-10-20 (directive-key diagnostic always on); amended 2026-10-22 (JavaScript owns the roving `tabindex` and the hidden input's `checked` after attach); amended 2026-10-26 (the M3 composite extensions, the `tabs` module's `select` point and `data-activation-direction`, composite button mode); amended 2026-10-27 (the M4 popup modules: the per-popup session and the open-change relay, the `popup` module, the `press` and `guard` points and the `Prevent` verdict, the portal's container and shape, the Floating UI wrapper, the layered-style writer, the M4 ownership rows, element-typed option values as interop arguments) |
| [05](decisions/05-state-store-and-event-details.md) | State, store, controlled values and event details — port the store where Base UI uses it, `@bind`-compatible controlled values with a cancelable details callback first, one reason enum; project-wide no-boxing and .NET 11 / C# 15 union constraints (amends ADR 02) | accepted 2026-10-17; amended 2026-10-18 (controlled mode fixed for all parts, uncontrolled text-edit exception, `ToastManager`) and 2026-10-20 by ADR 08 (`EventReason`); amended 2026-10-20 after the union-binding spike (unions only for non-delegate alternatives; nullable union parameters; no union through `object`); amended 2026-10-20 (closed hierarchies not applied to the details types; the union feature checklist of issue 12; boxing through a component receiver corrected; `createSelector` deferred); amended 2026-10-20 (`Is<T>` on the WebAssembly interpreter: generated `ShallowEquals` avoids `EqualityComparer`); amended 2026-10-20 (no boxing and nullable union parameters enforced by `BLZX2005` and `BLZX2006`); amended 2026-10-22 (the form controls participate in a cascaded `EditContext`; `Form` stays Base UI's form); amended 2026-10-27 (the popup store record, the payload, the inert fallback store, the open-change callback) |
| [06](decisions/06-testing-harness.md) | Testing harness — four layers (xUnit, bUnit, vitest, Playwright per render mode), lowest observable layer per §13 row, marker-based readiness and no sleeps in the browser, parity harness retired | accepted 2026-10-18; amended 2026-10-18 (render-count rows at L2, React-assuming statements handled in the implementation revision) and 2026-10-20 (L4 under the Node test runner, TypeScript specs, `webServer`); amended 2026-10-26 (Tabs' pre-hydration rows withdrawn) |
| [07](decisions/07-repository-hosting-and-licensing.md) | Repository, hosting and licensing — new folder `Blazix.Primitives`, git `main`, GitHub `JakeMurrayDev/Blazix.Primitives` private until the first package release, MIT plus third-party notices, `docs/` is the specification | accepted 2026-10-20; amended 2026-10-21 (banner check scheduled with M4); amended 2026-10-23 (public from 2026-10-23, the Actions allowance exhausted); amended 2026-10-27 (the Floating UI banner lives in `js/floating-ui.ts`, checked by `assert-package.mjs`) |
| [08](decisions/08-naming-and-namespaces.md) | Naming and namespaces — flat `FamilyPart` type names in `Blazix.Primitives`, `ClassValue`/`StyleValue`, companion-type rules, product-name rule (`EventReason`, `PreventPartHandler()`), eleven-group member order, no field prefix | accepted 2026-10-20; amended 2026-10-20 after the union-binding spike (`ClassValue`/`StyleValue` as functions, `RenderFunction`, nullable union parameters); `s_` prefix on private static fields (Track 0); amended 2026-10-20 (overriding and implementing members carry `<inheritdoc />`); amended 2026-10-20 (member order, field names and `<inheritdoc />` enforced by `BLZX2003`, IDE1006 with `BLZX2004`, and `BLZX2007`); amended 2026-10-21 (Meter and Progress `Format`/`Culture`); amended 2026-10-22 (M2 form-family parameter types; `Input` and `FieldControl` over one behaviour); amended 2026-10-23 (`FormErrors` and the Form members; `MatchValue`, `ValidityFlag` and `FieldValidityState`); amended 2026-10-26 (M3 composite-family parameter types); amended 2026-10-27 (M4 popup-family parameter types: handles, the payload type parameter, `Duration?` fallbacks, the unions and enums, AlertDialog as sealed parts) |
| [09](decisions/09-solution-and-project-layout.md) | Solution and project layout — the repository tree, ICM entry and contract files, one test project per layer, separate generator and analyzer projects, TypeScript L4 specs, root Node toolchain, a sample application for manual verification per milestone | accepted 2026-10-20; amended 2026-10-20 (time control and diagnostics through a page hook); amended 2026-10-21 (`Utilities/` folder); amended 2026-10-22 (sample application replaces "no demo app"); amended 2026-10-22 (`Composite/`; the FFI core in `Field/`; `DirectionProvider/`); amended 2026-10-27 (`Popup/`, `Floating/`, `tests/browser/suites/`, `js/floating-ui.ts`) |
| [10](decisions/10-packaging-and-versioning.md) | Packaging and versioning — one package `Blazix.Primitives`, independent SemVer with the Base UI pin as metadata, `preview.N`/`rc.N`, MinVer tags, `net11.0` on the RC2 SDK, no nuget.org publish before .NET 11 GA | accepted 2026-10-20; amended 2026-10-20 (the `Microsoft.AspNetCore.Components.Web` package replaces the framework reference) |
| [11](decisions/11-continuous-integration.md) | Continuous integration — L1–L3 on every change, scoped L4 on pull requests and full L4 on `main`, nightly repeat runs, the red-check job, no-retry flakiness policy with quarantine, docs, ownership and module-graph lints | accepted 2026-10-20 |
| [12](decisions/12-implementation-order-and-milestones.md) | Implementation order and milestones — M0 foundations, M1 Toggle and Collapsible slice, M2 form family, M3 composite, M4 popup core, M5 lists, M6 loops; Server first per component; a preview tag per milestone | accepted 2026-10-20; amended 2026-10-21 (Toggle moved to M3; milestone verification items read per item); amended 2026-10-22 (the M2 plan: eight tracks, `DirectionProvider` in M2, the composite module's M2 scope); amended 2026-10-25 (M2 delivered 2026-10-23; tag `v0.3.0-preview.1`); amended 2026-10-26 (the M3 plan: six tracks in four sessions); amended 2026-10-27 (M3 delivered 2026-10-27; tag `v0.4.0-preview.1`); amended 2026-10-27 (the M4 plan: nine tracks in seven sessions over two waves) |
| [13](decisions/13-document-status-and-amendments.md) | Document status and amendment rule — the done definition, the `implemented` status, how an approved document is amended (`Revised` row, dated notes, ADR citation for port decisions) | accepted 2026-10-20; amended 2026-10-22 (rows whose cited tests render a later milestone's component) |
| [14](decisions/14-verification-plan.md) | Verification plan — every ADR verification item assigned to a milestone and a method, one GitHub issue each, tagged permanent tests, the RC2 SDK installed by the user | accepted 2026-10-20; amended 2026-10-21 (issue 16 to M4; issue 2 tied to Collapsible's done); amended 2026-10-22 (the ADR 05 `EditContext` item assigned to M2); amended 2026-10-27 (issues 8, 16 and 26 assigned to M4 tracks 1, 2 and 3; the payload-generic Razor probe, issue 116) |
| [15](decisions/15-upstream-sync-policy.md) | Upstream sync policy — the pin frozen at v1.9.0 until 1.0.0, a triage note per Base UI release, the re-pin procedure, the old rubric re-targeted to documents | accepted 2026-10-20 |
| [16](decisions/16-performance-practices.md) | Performance practices — trim/AOT analyzers, cached `EventCallback`s, literal sequence numbers, source-generated JSON contexts, memoised merging, allocation-budget tests; frame caching and `[JSImport]` deferred to measurements | accepted 2026-10-20; amended 2026-10-27 (the M4 popup core: the payload's version counter, options equality; the `[JSImport]` item measured by M4 track 3) |
