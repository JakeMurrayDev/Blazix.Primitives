# Blazix.Primitives

The Blazor (C#) port of MUI Base UI v1.9.0. The behavioural vocabulary of the library itself (parts, element substitution, state object, event details, the seven timing terms) is defined in `docs/conventions.md` and the `docs/architecture/*.md` §2 tables and is not repeated here. This glossary holds only the terms the port adds on top of Base UI.

## Language

**Render mode**:
One of Blazor's two interactive hosting models, Server or WebAssembly. Both are first-class targets; `InteractiveAuto` is not a third mode but a runtime switch between them.
_Avoid_: hosting model, platform, target

**Server floor**:
The design rule that C# code may not depend on anything Server cannot provide: pre-paint execution, same-task commit, frame or microtask scheduling.
_Avoid_: lowest common denominator, Server-first

**JS primitive**:
A JavaScript function that performs one browser-API operation on behalf of C# (capture a pointer, read a rect, observe an attribute) and contains no component behaviour.
_Avoid_: helper, interop module, shim

**Loop**:
A sequence of browser-API reads and writes repeated at frame rate (drag, scroll-tracked positioning, animation watching). The unit at which the C#/JS boundary is decided: a loop lives wholly in JavaScript.
_Avoid_: hot path, gesture code

**Relaxation table**:
The normative list, in ADR 01, of requirement IDs whose stated timing Server cannot meet, each with the Server-observable substitute.
_Avoid_: known issues, limitations, degraded mode

**Decision record (ADR)**:
A file under `docs/decisions/` recording one settled cross-cutting decision with the requirement IDs it affects.
_Avoid_: design doc, RFC

**Part base**:
The C# base class every library part derives from; it owns the common parameters and calls the pipeline.
_Avoid_: base component, RenderElement (that name is the public `useRender` wrapper)

**Frame rewrite**:
Rendering a consumer-supplied fragment into a scratch builder and re-emitting its root element with merged attributes; the port's `cloneElement`.
_Avoid_: fragment cloning, render-tree patching

**Veto**:
A consumer handler's call to `PreventPartHandler()` on the event, which stops the part's own handler for that dispatch; Base UI's `preventBaseUIHandler()`.
_Avoid_: PreventBaseUIHandler, cancel (that is the request's `Cancel()`)

**Consumed flag**:
A marker a part sets on its ancestor-provided context after handling a key event so the enclosing part, dispatched next, skips it. The port's cross-part form of the handler veto.
_Avoid_: event bubbling flag, handled flag

**Mechanism module**:
A JavaScript module that implements the JS-resident requirements of exactly one architecture doc (positioning, interactions, focus manager, portal, transitions, composite, button, scroll lock, dom, swipe).
_Avoid_: shared JS, utility module, floating.js

**Component module**:
A JavaScript module holding one component's own frame-rate loops, composed from mechanism modules and nothing else.
_Avoid_: component script

**Twin**:
The C# class paired with one JavaScript module; it owns the interop handle and exposes the module's callbacks as typed events. Parts talk to twins, never to `IJSRuntime`.
_Avoid_: interop service, JS wrapper, bridge

**Ownership list**:
The per-part table naming which DOM attributes and style properties JavaScript writes; every attribute has exactly one writer.
_Avoid_: imperative styles, JS-managed attributes

**Mirrored field**:
A state-object field whose DOM attribute is JavaScript-written and whose C# value arrives later through the twin; on Server it lags one round trip.
_Avoid_: synced state, shadow state

**Decision point**:
A place inside a JavaScript-owned dispatch where a consumer's C# handler could change the outcome; resolved synchronously on WebAssembly, by a fallback policy elsewhere.
_Avoid_: hook, interception point

**Optimistic convergent write**:
A DOM write JavaScript makes ahead of a C# render that is already determined to produce the same value; the one sanctioned exception to single ownership.
_Avoid_: optimistic update

**Request**:
A proposed value change that has not been committed: the details callback runs, may cancel it, and only then does the component commit (if uncontrolled) and fire the binding callback.
_Avoid_: change event, update

**Controlled / uncontrolled**:
Whether the consumer supplied the value parameter at first render (controlled: the component never commits) or only a default (uncontrolled: the component commits accepted requests). Fixed for the component's lifetime.
_Avoid_: bound / unbound, managed / unmanaged

**Details**:
The mutable object carried by a request: reason, event snapshot, trigger, cancel and allow-propagation.
_Avoid_: event args (that is the Blazor wrapper carrying value plus details), payload (that is the trigger's consumer value)

**Reason**:
One member of the single `EventReason` enum naming the cause of a request; its literal form is the kebab-case Base UI string.
_Avoid_: source, cause, trigger (that is the element), BaseUIEventReason (the Base UI type name)

**Layer (L1–L4)**:
Where a translated test runs: xUnit for pure C#, bUnit for parts, vitest for JavaScript modules, Playwright for cross-boundary behaviour. A test runs at the lowest layer that can observe it.
_Avoid_: tier, level

**Twin contract test**:
A bUnit test that records what C# asked a JavaScript module to do and drives the module's callbacks into C#, without any JavaScript running.
_Avoid_: interop mock test

**Readiness marker**:
The `data-interactive` attribute a fixture renders once Blazor is interactive, plus the `data-blazix-started` attribute the initializer sets; the only thing a browser test waits for.
_Avoid_: hydration wait, sleep

**Family**:
One Base UI component and all of its parts (Menu and `Menu.Root`, `Menu.Item`, …); the unit of a document, a source folder and a browser spec file.
_Avoid_: component (ambiguous with a Blazor component), module

**Handover at attach**:
The rule that C# renders a JS-owned attribute's initial value once and freezes it from the moment the twin's `attach` succeeds, JavaScript owning it afterwards.
_Avoid_: hydration, initial style

**Placement rule**:
The rule deciding which JavaScript module owns a browser-API behaviour: the mechanism module of its architecture doc, or a component module when the component has a loop, a same-task measurement, selection, clipboard or composition handling, or a keyboard model shared with a loop.
_Avoid_: module table (that is the list the rule produces)

**Red check**:
The CI job that runs every test a change adds against the base branch's source with the change's tests, and fails the change if any added test passes.
_Avoid_: TDD check, mutation test

**Quarantine**:
The state of an intermittently failing test: excluded from the gate by `test.fixme` with the `@quarantine` tag and a linked issue, for at most 14 days.
_Avoid_: skip, flaky list

**Triage note**:
The per-release file `docs/upstream/v1.N.md` that classifies each Base UI changelog entry against the documents without moving the pin.
_Avoid_: delta inventory, sweep

**Re-pin**:
Moving `reference/base-ui` to a newer Base UI tag and carrying the documents, citations, tests and decision records to it.
_Avoid_: upgrade, sync

**Spike**:
A throwaway project under `spikes/` that verifies one decision-record item and is deleted when the item closes.
_Avoid_: prototype

**Fixture host**:
The Blazor Web App under `tests/` that serves every fixture page in both render modes for the browser tests.
_Avoid_: demo app, test server

**Sample application**:
The Blazor Web App under `samples/` with one page per ported family, each in both render modes, for trying a milestone's families by hand; built in CI and never packed. Not a spike: it stays.
_Avoid_: showcase, demo app, fixture host

**Implemented**:
The document status set when its component meets the done definition: every §13 row green at its layer, L4 on both render modes.
_Avoid_: done, complete, shipped

