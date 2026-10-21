# src/Blazix.Primitives/Interop

The twins: the C# half of each JavaScript module (ADR 04 §3). Internal to the library (ADR 08 §1); parts talk to twins and never to `IJSRuntime`.

## Inputs

- `docs/decisions/04-js-interop-boundary.md` §3 (contract, decision points), ADR 01 (Server floor), ADR 06 §1 (twin contract tests), ADR 08 §3 (`<Module>Twin`), ADR 16 rule 4 (one `JsonSerializerContext`).
- `../js/twin.ts`, `../js/decide.ts`, `../js/event-info.ts`: the JavaScript side of the same contract.
- `../Events/EventSnapshot.cs` (Track 2): the event snapshot and its JSON names, which `../js/event-info.ts` mirrors.

## Job

- `TwinBase<TTwin, TOptions>`: lazy `import()` of the module on first use; `AttachAsync(ElementReference | string, options)` returning a `TwinHandle<TOptions>` per attached part, idempotent per element; the one `DotNetObjectReference<TTwin>` the module calls back through; `Ping` and `Decide` as `[JSInvokable]` methods; `DisposeAsync` that releases every handle, the reference and the module. A `JSDisconnectedException` (the Server circuit is gone) turns every call into a no-op rather than an exception in a part's lifecycle method.
- `TwinHandle<TOptions>`: `UpdateAsync(options)` and an idempotent `DisposeAsync()`; `update` after `dispose` is a no-op.
- `TwinOptions`: the base of every options record; `Style` is the consumer's merged style string, sent on `attach` and on every `update` (handover at attach).
- `DecisionPolicy` and its kebab-case literals (`run`, `stop`, `start`, `prevent-and-stop`).
- `ButtonTwin` (module `button`): `ButtonOptions` carries the mechanism's configuration (`Disabled`, `FocusableWhenDisabled` with `null` for unspecified, `Native`, `Composite` as the part resolved it, `PressAndHold` with the 400/60 ms and 8 px defaults, `ReportInteractionType`, and `DevelopmentChecks`, which defaults to the development warnings switch). `ActivationDecider` is the C# chain of the `activate` decision point: return `Stop` when a consumer handler called `PreventPartHandler()`. Events: `InteractionReported` (the literal `mouse`, `touch`, `pen`, `keyboard` or empty, with the event), `HoldTicked` (the event that started the sequence, every tick), `HoldStopped` (the release), `HoldClicked` (a click that performs the single action), `NativeButtonMismatched` (the declared mode). The part renders the button attributes itself; the module writes none.
- `DomTwin` (module `dom`): `DomOptions` turns primitives on per element: `Indeterminate` (the checkbox input's property, re-asserted inside a native click), `LiveRegion` (the announcement marker), `Image` (`ImageLoadOptions`: the source properties, `KeepMounted` to derive from the attached `<img>` instead of a probe, `HideWhenLoaded` for the fallback's id). Event: `ImageStatusChanged` with `loading`, `loaded` or `error`, once per change. The module's measuring and observing helpers have no twin: other modules import them.
- `TransitionsTwin` (module `transitions`).
  - `TransitionsOptions` holds:
    - the machine input `Open`;
    - the values the element was rendered with, read on `attach` only: `Mounted`, and `Status` as a `TransitionStatus?` travelling as the literal `starting`, `ending`, `idle` or nothing;
    - the machine options `EnableIdleState` and `DeferEndingState`, fixed per part;
    - `KeepMounted`, `HiddenUntilFound`, `HiddenWhenUnmounted` (a container is hidden at exit end; an indicator is not), `SuppressEndingStyle` (a kept element plays no exit), `ReportOpenComplete`, `RevealOnBeforeMatch`, `UnmountRequests` (a count C# raises to end an exit itself), and `ExternalExit` (no exit watcher: the exit ends only when `UnmountRequests` increases; the Collapsible Root and Trigger, whose exit ends with the panel's);
    - `OpenSelector`, the selector the element matches while rendered open (`[data-open]`, `[data-panel-open]`, `:not([aria-hidden])`): when set, the module reads the open input from the DOM after every render, before paint;
    - `Style`, applied only when set.
  - There is no `AnimateInitialOpen` field: C# renders the initial status and sends it as `Status`.
  - Events, each raised once per message:
    - `StatusChanged` (`TransitionStatusChange(TransitionStatus? Status, bool Mounted)`);
    - `TransitionCompleted` (`false` for an exit, after which the element is unmounted and its status unset; `true` for an enter).
  - The reveal:
    - `RevealDecider` (`Func<EventSnapshot, DecisionPolicy>`) is the C# chain of the decision point `reveal`: it requests the open and returns `Stop` when the request was canceled. It runs on WebAssembly only, inside the dispatch.
    - Elsewhere the module applies `run` and invokes `OnRevealRequest`, which returns `Task<bool>`: the answer of `RevealRequestHandler` (`Func<EventSnapshot, ValueTask<bool>>`), whether the part is open once the request ran. The module re-collapses the element a frame after a `false` answer.
- `CollapsibleTwin` (module `collapsible`, the Collapsible and Accordion panel's component module).
  - `CollapsibleOptions`: `Open`; `Mounted` and `Status` as the element was rendered (read when the module starts on an element); `KeepMounted`, `HiddenUntilFound`; `SuppressOpenAnimation` (the one-shot `animation-name: none` of an initially open or re-attached panel, read when the module starts on an element); `PanelId` (by which a withheld or replaced element is found again); `OpenSelector` (`[data-open]`); `Style`, the consumer's merged string, which the module composes the whole `style` from.
  - Events, each once per message: `ForcedIdleChanged` (`PanelForcedIdleChange`: the panel shows itself settled during a motion-skipping open), `ExitCompleted` (`OnTransitionComplete` with `open: false`: the exit ended, the module has hidden the element), `AnimationTypeChanged` (`PanelAnimationTypeChange(Type, Mixed)`).
  - The reveal is `TransitionsTwin`'s: `RevealDecider` on WebAssembly, `OnRevealRequest` answered by `RevealRequestHandler` elsewhere.
- `TransitionStatusRules` (`Initial(open, enableIdleState, animateInitialOpen)` and `Evaluate(open, mounted, status, deferEndingState)`, both returning `(bool Mounted, TransitionStatus? Status)`): the C# half of the machine. It covers a part's first render and the render that applies a new open state. The `transitions` module runs the same evaluation step for an attached element; `js/test-support/transition-evaluation.cases.json` tests both.
- `TransitionStatus` (`public enum TransitionStatus { Starting, Ending, Idle }`, namespace `Blazix.Primitives`, file `TransitionStatus.cs` beside its first consumer): a part declares `[property: MirroredField] TransitionStatus? TransitionStatus`, `null` being the unset status.
- A part owns its twin: a callback carries no element key, so one twin attached to several elements cannot tell their reports apart. `dom` and `button` own no style property, so their options leave `Style` unset and the part keeps rendering its style; `transitions` applies `Style` only when a part hands its style over.
- The shared enums these literals become (`InteractionType`, Avatar's `ImageLoadingStatus`) arrive with their first part; the twins pass the literals until then. `TransitionStatus` arrived with its twin (track 5).
- `BlazixJsonContext`: `EventSnapshot`, every options record and every callback payload. Options and payloads cross the boundary as JSON strings the twin serialises and deserialises itself, so the runtime's own serializer only ever sees strings, `ElementReference` and object references (trim- and AOT-safe without configuring the runtime's resolver).

## Traceability (the sources carry no specification references)

- `TwinBase`, `TwinHandle`: ADR 04 §3 (contract, decision points), ADR 06 Amendments 2026-10-18 rule 2 (idempotent attach and dispose), ADR 08 §3 (`<Module>Twin`), ADR 01 decision 1 (the synchronous invoke serves decisions only), ADR 02 decision 1 (attach by generated id under a substitute callback).
- `TwinOptions`: ADR 04 §3 and Amendments 2026-10-18 (handover at attach).
- `DecisionPolicy`: ADR 04 §3 and ADR 01's relaxation table (the fallback per decision point).
- `TransitionsTwin`, `TransitionsOptions`, the transitions payloads, `TransitionStatus`, `TransitionStatusRules`:
  - ADR 04 §1 (transitions row), §2 (mirrored `transitionStatus`; the convergent `hidden`), §3 (decision point `reveal`, fallback `run`, and its Server answer: Amendments 2026-10-21) and Amendments 2026-10-18 (handover at attach);
  - `OpenSelector` and `TransitionStatusRules`: ADR 04 Amendments 2026-10-21 (the open input of an attached part) and ADR 01 Amendments 2026-10-21 (decision 2 note: the evaluation step in both languages, one case table; the rows added that day) and decision 4 ("post-commit, pre-paint, DOM-only work");
  - ADR 08 §3 (Enums row; Amendments 2026-10-21);
  - TRN-API-07, TRN-STA-01, TRN-ANI-03, TRN-ALG-04/05/07, TRN-EVT-09, COL-EDGE-09;
  - ADR 01 relaxation table: TRN-ANI-03/09/10, RND-STA-01 for mirrored fields, TRN-STA-04/05/07/09 met on both modes.
- `CollapsibleTwin`, `CollapsibleOptions`, the collapsible payloads: ADR 04 §1 (the `collapsible` component module, Amendments 2026-10-21), §2 (the panel's JS-owned attributes and style, the convergent `hidden`), §3 (the decision point `reveal`) and Amendments 2026-10-18 (handover at attach); COL-ANI-01..30, COL-STA-04/06, COL-DOM-12..18, COL-EVT-04/05, COL-EDGE-03/09. `TransitionsOptions.ExternalExit`: COL-STA-05, COL-DOM-03/06.
- `BlazixJsonContext`: ADR 16 rule 4.
- `ButtonTwin`, `ButtonOptions`, the button payloads: ADR 04 §1 (button row) and §3 (button activation, fallback `run`); UBT-API-01, UBT-ALG-01..07, UBT-EVT-07/08/10, UBT-EDGE-01..03; ADR 01 relaxation table (UBT-KEY-03/04/06).
- `DomTwin`, `DomOptions`, `ImageLoadOptions`: ADR 04 §1 (dom row), Amendments 2026-10-18 (the Avatar convergent write) and 2026-10-20 (the `indeterminate` and live-region owners); CHK-DOM-13, UTL-A11Y-01..04, AVA-ALG-01/02, AVA-STA-09/12, AVA-EVT-02.

## Outputs

- One `<Module>Twin` per JavaScript module, exposing the module's callbacks as typed events for the parts.

## Human check

- A twin's `[JSInvokable]` callback does one state change per message and is never called per frame.
- A new options record or payload is listed in `BlazixJsonContext` and has an L2 twin contract test.
- `OnDecide` runs synchronous consumer handlers only; nothing awaits inside a decision (ADR 04 §3).
- A press-and-hold tick is a report, never a decision: its result is state, so C# stops a sequence by sending `Disabled` (ADR 01 decision 1).
