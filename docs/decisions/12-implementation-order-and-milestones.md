# ADR 12 — Implementation order and milestones

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | the order in which the infrastructure and the components are built; the first vertical slice; the grouping of components into milestones; what each milestone delivers |
| Affects | no requirement IDs; ADR 04 §6 (Server first, applied per component), ADR 10 (a tag per milestone), ADR 13 (done per component), ADR 14 (which verification item blocks which milestone) |

## Context

The accepted ADRs imply pipeline (02) → store and `Controlled<T>` (05) → twins and JavaScript modules (04) → simple parts → popups, and ADR 04 §6 says Server first. The documents' pilots were Switch and Collapsible. Switch mentions Field or Form 52 times and its §13 tests render it inside `Field.Root` and `Form`, so a Switch slice drags in FFI (126 requirements), Field (139), Form (48) and the `form` JavaScript module. Toggle mentions Field once and needs only the pipeline, `Controlled<T>`, event details and the `button` module. Collapsible needs those plus the `transitions` and `dom` modules, handover at attach and three relaxation rows, which makes it the first real exercise of ADRs 01 and 04. RadioGroup cites the composite mechanism 33 times; Accordion three times. The popup families share FLT, PSH, `portal`, `focus-manager`, `interactions`, `scroll-lock` and `positioning`, so the first popup pays for all of them.

## Decision

**Foundations first, then slices grouped by shared mechanism (option M-A).**

| Milestone | Delivers | Why this grouping |
|---|---|---|
| M0 Foundations | repository and CI (ADRs 07–11); `PartBase<TState>`, pipeline, frame rewrite, `MergeProps`, ids, projection generator (ADR 02); store, `Controlled<T>`, details and reasons (ADR 05); twin base, initializer, esbuild, `dom` and `button` modules (ADR 04); fixture host, readiness markers, browser projects, red-check job, ownership and module-graph lints (ADR 11); the conformance suite on a sample part; allocation-budget tests (ADR 16); M0's verification items closed (ADR 14) | everything every part needs; done means L1–L3 green and an L4 smoke test green on both modes |
| M1 First slice | Collapsible, Separator, Button, Avatar, Progress, Meter; the `transitions` module (Toggle moved to M3, Amendments 2026-10-21) | the smallest parts that exercise the pipeline, requests, a JavaScript loop, handover at attach, relaxations and the optimistic convergent write |
| M2 Form family | FFI and the `form` module; Field, Fieldset, Form, Input, Switch, Checkbox, CheckboxGroup, Radio; the `composite` module and RadioGroup close the milestone | one shared mechanism, nine documents |
| M3 Composite | Toggle, ToggleGroup, Tabs, Toolbar, Accordion | the composite module's remaining consumers |
| M4 Popup core | `positioning` (bundled Floating UI, ADR 03), `interactions`, `focus-manager`, `portal`, `scroll-lock`; popup store and handles (PSH); Popover, Tooltip, PreviewCard, Dialog, AlertDialog | the shared popup layer, proven on the five simplest popups |
| M5 Lists | Menu, ContextMenu, Menubar, Select, Combobox, Autocomplete, NavigationMenu | list navigation over the popup layer |
| M6 Loops and the rest | Slider, ScrollArea, NumberField, OtpField, Drawer (`swipe`), Toast (`toast` module, `ToastManager`) | the component modules with their own frame-rate loops |

Rules: within a milestone a component is built and taken through L4 on Server first, then WebAssembly (ADR 04 §6), and is done only on both (ADR 13). Each milestone ends with a tag `v0.<n>.0-preview.1` (ADR 10); `1.0.0` closes M6. A verification item assigned to a milestone (ADR 14) is closed before that milestone's first component is declared done.

## Consequences

* M0 ships no component; the first implemented components arrive with M1.
* Switch, a documentation pilot, is implemented in M2 with its Field dependencies rather than first.
* The milestone table is the project's progress view; the documents' status column shows `implemented` per component (ADR 13), not an in-progress state.

## Verification items

* None beyond those ADR 14 assigns to M0 and M1.

## Considered options

* A strict vertical slice first (Switch end to end, Field and FFI included): about 500 requirements across four documents and two modules before any pipeline or twin mistake is found.
* All infrastructure and all JavaScript modules first, then all parts: months without a component on both modes, the Server-floor assumptions untested against real parts the longest.

## Amendments

**2026-10-21, M1 scope and the milestone-item rule (M1 planning; chosen by the user the same day).** (1) Toggle moves from M1 to M3. Sixteen of its requirement IDs, including all five of its `[L4]` rows (TGL-FOC-02/03, TGL-KEY-02/03/05), are testable only inside ToggleGroup or Toolbar over the `composite` module (M2), so under ADR 13 §1 Toggle could not be `implemented` in M1; it is built in M3 beside ToggleGroup, as the first part of that milestone. The M1 row now reads Collapsible, Separator, Button, Avatar, Progress, Meter and the `transitions` module; the M3 row reads Toggle, ToggleGroup, Tabs, Toolbar, Accordion. M1's first slice still exercises the pipeline, a request (`Controlled<bool>` on Collapsible), a JavaScript loop, handover at attach, relaxations and the convergent write. (2) The rule "A verification item assigned to a milestone (ADR 14) is closed before that milestone's first component is declared done" is read per item: an item that measures one component's row ([issue 2](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/2), the TRN-ANI-09 round trip, Collapsible) is closed before that component is declared done, not before the milestone's first component; an item that belongs to the milestone as a whole ([issue 20](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/20), the first tagged build) is closed before the milestone's tag. ADR 14 §1 carries the same note on the issue 2 row.

**2026-10-22, the M2 plan (M2 planning; chosen by the user the same day).** (1) The M2 row's families are built as eight tracks in five sessions: Field, Fieldset and Input with the FFI core and the `form` module (track 1) and Form (track 2) by one session in three pull requests; the `composite` module with `DirectionProvider` (track 3); Switch (track 4); Checkbox and CheckboxGroup (tracks 5 and 6, one session); Radio and RadioGroup (tracks 7 and 8, one session and one pull request, because 39 Radio requirement IDs cite only `RadioGroup.test.tsx`, so ADR 13 §1 would hold Radio as it held Toggle). Wave 1 is tracks 1 to 4; wave 2 is tracks 5 to 8, each after the tracks it builds on have merged. (2) `DirectionProvider` (UTL-I18N-01..04) joins the M2 row, built by track 3 in its own family folder, because RadioGroup's right-to-left rows render it and every M3 composite family needs it. (3) The `composite` module ships in M2 the 65 of CMP's 225 requirement IDs that RadioGroup reaches; the rows whose first consumer is in M3 or M5 are listed by milestone in the pull request, as the `transitions` pull request did in M1, and the keyed registry, the composite grid and `onLoop`, which no v1.9.0 component uses, are deferred by a dated note under CMP §10 Q1. (4) The Switch track (4) owns the two shared-path changes every hidden-input family needs, the after-element member of ADR 02 Amendments 2026-10-22 and the hidden-input bridge options of the `button` module (UBT-EVT-09), each in its own commit with its red test; Checkbox and Radio reuse them.
