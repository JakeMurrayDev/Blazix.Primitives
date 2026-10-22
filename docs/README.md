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
| architecture/composite-and-list-navigation.md | CMP | approved | 225 reqs; keyboard matrices §4.5-4.6, algorithms §5 | |
| architecture/floating-and-positioning.md | FLT | approved | 221 reqs; engine middleware in §5 | |
| architecture/popup-store-and-handles.md | PSH | approved | 208 reqs; shared popup props in §3 | |
| architecture/transitions-and-mounting.md | TRN | approved | 94 reqs; state machine in §4.1 | |
| architecture/field-and-form-infrastructure.md | FFI | approved | 126 reqs; control registration matrix in §6 | |
| architecture/button-behavior.md | UBT | approved | 100 reqs |
| architecture/shared-utilities.md | UTL | approved | 155 reqs; includes DirectionProvider, CSPProvider, useMediaQuery, store |
| components/accordion.md | ACC | approved | 101 reqs (opus) | builds on collapsible |
| components/alert-dialog.md | ADL | approved | 30 reqs + adoption (opus) | builds on dialog |
| components/autocomplete.md | AUT | approved | 83 reqs + adoption (opus) | builds on combobox |
| components/avatar.md | AVA | implemented | 58 reqs (opus) | |
| components/button.md | BTN | implemented | 29 reqs (opus) | |
| components/checkbox.md | CHK | approved | 151 reqs |
| components/checkbox-group.md | CKG | approved | 83 reqs |
| components/collapsible.md | COL | implemented | pilot; 104 reqs |
| components/combobox.md | CMB | approved | 247 reqs |
| components/context-menu.md | CTX | approved | 95 reqs (opus) | builds on menu |
| components/dialog.md | DLG | approved | 191 reqs |
| components/drawer.md | DRW | approved | 152 reqs (opus) | builds on dialog |
| components/field.md | FLD | approved | 143 reqs; owns normative Field.Control text |
| components/fieldset.md | FST | approved | 32 reqs | |
| components/form.md | FRM | approved | 48 reqs | |
| components/input.md | INP | approved | 19 reqs; alias of Field.Control | |
| components/menu.md | MNU | approved | 308 reqs | |
| components/menubar.md | MBR | approved | 91 reqs (opus) | builds on menu |
| components/meter.md | MTR | implemented | 52 reqs (opus) | |
| components/navigation-menu.md | NAV | approved | 208 reqs |
| components/number-field.md | NUM | approved | 177 reqs | |
| components/otp-field.md | OTP | approved | 169 reqs | |
| components/popover.md | POP | approved | 214 reqs |
| components/preview-card.md | PRV | approved | 158 reqs |
| components/progress.md | PRG | implemented | 55 reqs (opus) | |
| components/radio.md | RAD | approved | 99 reqs |
| components/radio-group.md | RDG | approved | 86 reqs |
| components/scroll-area.md | SCR | approved | 135 reqs | |
| components/select.md | SEL | approved | 332 reqs | |
| components/separator.md | SEP | implemented | 14 reqs (opus) | |
| components/slider.md | SLD | approved | 191 reqs | |
| components/switch.md | SWT | approved | pilot; 117 reqs |
| components/tabs.md | TAB | approved | 156 reqs | |
| components/toast.md | TST | approved | 186 reqs |
| components/toggle.md | TGL | approved | 57 reqs | |
| components/toggle-group.md | TGG | approved | 71 reqs | |
| components/toolbar.md | TLB | approved | 94 reqs | |
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
| [01](decisions/01-hosting-model-and-timing.md) | Hosting model and timing — both interactive render modes, Server as design floor, browser-API code in JS, timing-term mapping, Server relaxation table | accepted 2026-10-17; amended 2026-10-18 (microtask classes, store sync in `OnParametersSet`, formatting via .NET globalization, new relaxation rows) and 2026-10-20 (`update`-carried values, Combobox propagation points, ContextMenu dispatch, Intl rounding in C#); amended 2026-10-22 (label and legend ids publish in the creating batch) |
| [02](decisions/02-render-pipeline-and-attribute-merging.md) | Render pipeline, element substitution and attribute merging — both `render` forms via frame rewriting, two class/style channels, veto as `EventArgs` extension, `PartBase<TState>` over one static pipeline, scoped id counter | accepted 2026-10-17; amended 2026-10-17 by ADR 05, 2026-10-18 (Blazor event directives rejected, no `inputRef`, four React-only IDs withdrawn for the port) and 2026-10-20 by ADR 08 (`PreventPartHandler()`, `ClassValue`/`StyleValue`); amended 2026-10-20 after the union-binding spike (`Render`/`RenderFunction` as two parameters, function-typed `ClassValue`/`StyleValue`); amended 2026-10-20 (always-on errors); amended 2026-10-22 (`inputRef` withdrawn on the form controls; content after the host element) |
| [03](decisions/03-positioning-engine.md) | Positioning engine — bundle the vendored `@floating-ui/dom` 1.8.0, port only Base UI's middleware and pipeline assembly in JS | accepted 2026-10-17 |
| [04](decisions/04-js-interop-boundary.md) | JavaScript interop boundary — one JS module per mechanism plus component loops, single-writer attribute ownership with C# mirroring, twin contract with WebAssembly-only synchronous decision points, TypeScript + esbuild, DOM-move portals, Server-first development | accepted 2026-10-17; amended 2026-10-18 (handover at attach, module placement rule and five new modules, JavaScript-only dispatch-time decisions, Avatar convergent write, quirks) and 2026-10-20 (ownership rows for Checkbox `indeterminate`, Drawer, NavigationMenu; `dom`, `composite` and `slider` owners; Combobox and Radio decision points); amended 2026-10-20 (directive-key diagnostic always on); amended 2026-10-22 (JavaScript owns the roving `tabindex` and the hidden input's `checked` after attach) |
| [05](decisions/05-state-store-and-event-details.md) | State, store, controlled values and event details — port the store where Base UI uses it, `@bind`-compatible controlled values with a cancelable details callback first, one reason enum; project-wide no-boxing and .NET 11 / C# 15 union constraints (amends ADR 02) | accepted 2026-10-17; amended 2026-10-18 (controlled mode fixed for all parts, uncontrolled text-edit exception, `ToastManager`) and 2026-10-20 by ADR 08 (`EventReason`); amended 2026-10-20 after the union-binding spike (unions only for non-delegate alternatives; nullable union parameters; no union through `object`); amended 2026-10-20 (closed hierarchies not applied to the details types; the union feature checklist of issue 12; boxing through a component receiver corrected; `createSelector` deferred); amended 2026-10-20 (`Is<T>` on the WebAssembly interpreter: generated `ShallowEquals` avoids `EqualityComparer`); amended 2026-10-20 (no boxing and nullable union parameters enforced by `BLZX2005` and `BLZX2006`) |
| [06](decisions/06-testing-harness.md) | Testing harness — four layers (xUnit, bUnit, vitest, Playwright per render mode), lowest observable layer per §13 row, marker-based readiness and no sleeps in the browser, parity harness retired | accepted 2026-10-18; amended 2026-10-18 (render-count rows at L2, React-assuming statements handled in the implementation revision) and 2026-10-20 (L4 under the Node test runner, TypeScript specs, `webServer`) |
| [07](decisions/07-repository-hosting-and-licensing.md) | Repository, hosting and licensing — new folder `Blazix.Primitives`, git `main`, GitHub `JakeMurrayDev/Blazix.Primitives` private until the first package release, MIT plus third-party notices, `docs/` is the specification | accepted 2026-10-20; amended 2026-10-21 (banner check scheduled with M4) |
| [08](decisions/08-naming-and-namespaces.md) | Naming and namespaces — flat `FamilyPart` type names in `Blazix.Primitives`, `ClassValue`/`StyleValue`, companion-type rules, product-name rule (`EventReason`, `PreventPartHandler()`), eleven-group member order, no field prefix | accepted 2026-10-20; amended 2026-10-20 after the union-binding spike (`ClassValue`/`StyleValue` as functions, `RenderFunction`, nullable union parameters); `s_` prefix on private static fields (Track 0); amended 2026-10-20 (overriding and implementing members carry `<inheritdoc />`); amended 2026-10-20 (member order, field names and `<inheritdoc />` enforced by `BLZX2003`, IDE1006 with `BLZX2004`, and `BLZX2007`); amended 2026-10-21 (Meter and Progress `Format`/`Culture`); amended 2026-10-22 (M2 form-family parameter types; `Input` and `FieldControl` over one behaviour) |
| [09](decisions/09-solution-and-project-layout.md) | Solution and project layout — the repository tree, ICM entry and contract files, one test project per layer, separate generator and analyzer projects, TypeScript L4 specs, root Node toolchain, a sample application for manual verification per milestone | accepted 2026-10-20; amended 2026-10-20 (time control and diagnostics through a page hook); amended 2026-10-21 (`Utilities/` folder); amended 2026-10-22 (sample application replaces "no demo app"); amended 2026-10-22 (`Composite/`; the FFI core in `Field/`; `DirectionProvider/`) |
| [10](decisions/10-packaging-and-versioning.md) | Packaging and versioning — one package `Blazix.Primitives`, independent SemVer with the Base UI pin as metadata, `preview.N`/`rc.N`, MinVer tags, `net11.0` on the RC2 SDK, no nuget.org publish before .NET 11 GA | accepted 2026-10-20; amended 2026-10-20 (the `Microsoft.AspNetCore.Components.Web` package replaces the framework reference) |
| [11](decisions/11-continuous-integration.md) | Continuous integration — L1–L3 on every change, scoped L4 on pull requests and full L4 on `main`, nightly repeat runs, the red-check job, no-retry flakiness policy with quarantine, docs, ownership and module-graph lints | accepted 2026-10-20 |
| [12](decisions/12-implementation-order-and-milestones.md) | Implementation order and milestones — M0 foundations, M1 Toggle and Collapsible slice, M2 form family, M3 composite, M4 popup core, M5 lists, M6 loops; Server first per component; a preview tag per milestone | accepted 2026-10-20; amended 2026-10-21 (Toggle moved to M3; milestone verification items read per item); amended 2026-10-22 (the M2 plan: eight tracks, `DirectionProvider` in M2, the composite module's M2 scope) |
| [13](decisions/13-document-status-and-amendments.md) | Document status and amendment rule — the done definition, the `implemented` status, how an approved document is amended (`Revised` row, dated notes, ADR citation for port decisions) | accepted 2026-10-20; amended 2026-10-22 (rows whose cited tests render a later milestone's component) |
| [14](decisions/14-verification-plan.md) | Verification plan — every ADR verification item assigned to a milestone and a method, one GitHub issue each, tagged permanent tests, the RC2 SDK installed by the user | accepted 2026-10-20; amended 2026-10-21 (issue 16 to M4; issue 2 tied to Collapsible's done) |
| [15](decisions/15-upstream-sync-policy.md) | Upstream sync policy — the pin frozen at v1.9.0 until 1.0.0, a triage note per Base UI release, the re-pin procedure, the old rubric re-targeted to documents | accepted 2026-10-20 |
| [16](decisions/16-performance-practices.md) | Performance practices — trim/AOT analyzers, cached `EventCallback`s, literal sequence numbers, source-generated JSON contexts, memoised merging, allocation-budget tests; frame caching and `[JSImport]` deferred to measurements | accepted 2026-10-20 |
