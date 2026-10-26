# ADR 06 — Testing harness

| | |
|---|---|
| Status | accepted (2026-10-18) |
| Project | Blazix.Primitives |
| Decides | how the per-component §13 coverage matrices become tests; which layer each kind of requirement is tested at; the browser harness rules for both render modes; the fate of the old parity harness |
| Affects | every `Test:` line in the docs (a layer tag is added when the docs are revised for implementation); RND-DOM-36..49 (bUnit); TRN-EDGE-01 (the animations-disabled switch becomes a harness requirement); ADR 01's relaxation table (per-mode assertions) |

## Context

Base UI's suite is 348 test files and about 5,475 titles, run by vitest in jsdom with a Playwright browser mode; 506 blocks are browser-only. Every title is mapped to requirement IDs in the component docs' §13, including eight shared suites with per-component options; UTL §8.3 lists the harness helpers the suite relies on (controllable clock, pointer sequences with `timeStamp`, clipboard, `FormData` capture, scroll-lock predicate, diagnostics matchers, the `BASE_UI_ANIMATIONS_DISABLED` flag).

Under ADR 04 all keyboard, pointer, focus, dismissal, animation and positioning behaviour is JavaScript. bUnit renders markup and dispatches Blazor handlers but has no DOM, no focus model and no JS. The old port's handoff records the consequence ("bUnit has no pointer or focus model; route it to Playwright rather than fake a green test"), that CI was lint-only, and that its screenshot parity harness could not observe events, keyboard, ARIA or timing; the user lost interest at frame-by-frame comparison. Its Playwright suite used a `data-interactive="@RendererInfo.IsInteractive"` marker on every fixture container, a WebAssembly timeout multiplier, and fixed `WaitForDelayAsync` sleeps; the user reports Playwright as finicky with WebAssembly, sometimes waiting on hydration far too long. Tooling available: bUnit 2.7, Microsoft.Playwright 1.57 (`page.clock`), `Microsoft.Extensions.TimeProvider.Testing`, vitest.

## Decision

### 1. Four layers; every §13 row runs at the lowest layer that can observe it (M1)

| Layer | Runner | What runs there |
|---|---|---|
| L1 | xUnit, no Blazor | store and selectors (UTL-STA, UTL-ALG-03/04), `MergeProps` and the pipeline's merge rules (RND-ALG-02..07), the generated projection, the id generator, event details and reasons (EVD-ALG-01/02/08/09), C# timers through `TimeProvider` with `FakeTimeProvider` |
| L2 | bUnit | every part: the conformance suite RND-DOM-36..49 (tag name for 41, non-null capture for 44/45), controlled/uncontrolled contract (EVD-STA), request sequences and cancelation for C#-dispatched events (click, change, input), form integration (FFI), rendered attributes and ARIA that are C#-owned (ADR 04 §2); **twin contract tests**: bUnit's JS interop mock records the `attach`/`update`/`dispose` calls and their options records, and tests drive the twin's `[JSInvokable]` callbacks directly to assert the C# reaction |
| L3 | vitest in jsdom (TypeScript, same toolchain as ADR 04 §4) | each JavaScript module in isolation against a fake twin: the floating-ui-react hook tests (`useDismiss`, `useClick`, `useFocus`, hover, `safePolygon`), `tabbable`, `markOthers`, scroll lock, the `hide` middleware, `scrollEdges`, the animations watcher's synchronous path (TRN-ANI-15), composite navigation and typeahead, the swipe tracker |
| L4 | Playwright (C#, xUnit) against a fixture host | every cross-boundary row: the shared popup, popup-list, detached-triggers, positioner, viewport, popup-focus-props and dialog-root suites; keyboard, focus, dismissal, positioning with pixel assertions, animations, gestures; each test runs once per render mode |

Rules: a test runs at the lowest layer that can observe the behaviour and never fakes a layer above it. Every translated test keeps Base UI's title (traceability to §13); the `Test:` lines in the docs gain a layer tag during the implementation revision. A new test is shown to fail with its fix reverted before it counts. A row whose ID is in ADR 01's relaxation table asserts the WebAssembly column as written and the Server column on Server.

### 2. Browser harness rules (both render modes)

* **One fixture host application** serving both interactive render modes from the same fixture components; the mode is a route segment. Fixtures render with **prerendering disabled** so there is never a prerendered-but-dead interval; the few requirements about prerender (RND-DOM-29, TRN-ANI-35, SLD-ANI-03) get dedicated prerendering fixtures.
* **Readiness is a marker, never a timeout.** (The old port waited for the fixture container to attach with a 60 s WebAssembly ceiling and `WaitUntil = Load`, then slept; that is the strategy being replaced.) Every fixture container renders `data-interactive="@RendererInfo.IsInteractive"`, and the host's JS initializer sets `data-blazix-started` on `<html>` in `afterWebStarted`. Navigation helpers wait for both attributes; no test waits for `networkidle` (Blazor Server's SignalR connection is never idle) and no test uses fixed sleeps.
* **Interactivity before input.** Blazor attaches event handlers only after the first interactive render batch, so a click before the marker is lost (the old port documented this). Helpers press nothing until the marker is present.
* **WebAssembly warm-up once per run.** The runtime download is paid once: the fixture run opens one WebAssembly page before the suite and keeps the browser context, so per-test navigations hit the cache. Per-mode `expect` timeouts are configured (WebAssembly larger) but exist as a ceiling, not as a wait strategy.
* **Clocks.** Browser timers run under `page.clock`; the `transitions` module exposes the `BASE_UI_ANIMATIONS_DISABLED` switch (TRN-EDGE-01) and the host sets it by default, with the "animations" tests clearing it; C# timers run under `TimeProvider` so Server-side delays are deterministic.
* **Pointer realism.** Gesture tests use real Playwright mouse/touch input (not synthetic `dispatchEvent`) so pointer capture, `buttons`, `pointerType` and `timeStamp` are genuine (UTL §8.3 `firePointer`).
* **Diagnostics.** Development warnings are asserted through a captured logger (L1/L2) or console capture (L4).

### 3. The parity harness is retired as a gate

The translated suite is the oracle the requirements docs were written for. Pixel, geometry and animation-timeline diffing against React may return as an exploratory tool; it is never a doneness criterion.

## Consequences

* Node is required for L3 and for the L4 fixture host's JS build, in addition to .NET 11.
* L2 twin contract tests make the C#/JS boundary testable in isolation on both sides; a change to a twin's options record breaks L2 and L3 before L4.
* CI runs L1–L3 on every change and L4 per render mode; L4 must pass before a component doc moves from `reviewed` to `approved`-implemented.
* The §13 matrices gain a layer column at implementation time; rows that cannot be observed at any layer (none found so far) would be recorded as untestable rather than faked.
* Amended 2026-10-18: render-count requirements (POP-EDGE-09, DLG-EDGE-16, FLD-EDGE-13, CMB-EDGE-10, PRV-STA-17 and similar) are observed at L2 through bUnit's component render counts; under ADR 05 §1 a part re-renders only when one of its store selections changes, so they hold by construction and are kept at their documented level.

## Verification items

* `RendererInfo.IsInteractive` is `false` during prerender and `true` from the first interactive render on both modes (it was in the old port's fixtures; confirm on .NET 11). **Verified 2026-10-20 (both modes, SDK 11.0.100-rc.2.26504.105):** `tests/browser/harness.spec.ts` "@verifies ADR-06/1 RendererInfo.IsInteractive is false in prerender and true afterwards": the static response of the `prerender` fixture carries `data-interactive="false"` and both roots read `"true"` after activation. [issue 13](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/13).
* `page.clock` and Blazor Server coexist: SignalR heartbeats under a frozen clock must not drop the circuit (set the clock to auto-advance, not paused, for Server runs). **Verified 2026-10-20:** `tests/browser/harness.spec.ts` "@verifies ADR-06/2 the server project runs with page.clock installed and a circuit survives longer than the SignalR keep-alive". Every page of the suite runs under `page.clock.install()` left running (`tests/browser/helpers/test.ts`); the fixture host runs the hub with `KeepAliveInterval` 2 s and `ClientTimeoutInterval` 10 s, mirrored on the client by its JS initializer, and the `keep-alive` fixture keeps rendering real-time ticks past the timeout and then answers a click. [issue 14](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/14).
* bUnit's JS interop mock supports `IJSObjectReference` handles with `InvokeAsync` returning handles (needed for the `attach → handle` contract). **Verified 2026-10-20 (bUnit 2.12.5):** `tests/Blazix.Primitives.Tests.Components/Interop/TwinContractTests.cs` `Attach_imports_the_module_once_and_returns_a_handle_whose_update_and_dispose_are_recorded` (`[Trait("Verifies", "ADR06-3")]`): `SetupModule(path)` answers the `import`, `SetupModule("attach", matcher)` on that module answers `InvokeAsync<IJSObjectReference>("attach", …)` with a second recorded handle whose `update` and `dispose` invocations and arguments are asserted. [issue 15](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/15).

## Considered options

* Browser for everything behavioural, bUnit for markup only (slowest feedback, flakiness concentrated).
* bUnit-centric with mocked JS (the old port; vacuous tests).
* Keeping the parity harness as a gate (unobservable for events, keyboard, ARIA and timing; the point where the old effort stalled).

## Amendments

**2026-10-18, React-assuming requirement statements (second-pass finding; about fifty IDs listed in the second-pass hand-over note).** Handled in the implementation revision this ADR plans, with one rule per kind. (1) Render-count and render-timing wording ("on every render", "fewer than N re-renders", "same render", "first commit") is translated to Blazor terms ("on every render of the part"; counts observed through bUnit render counts at L2) and stays normative at its current level. (2) StrictMode double-invocation statements (CMB-EDGE-07, DLG-EDGE-07, COL-EDGE-09, COL-ANI-20/21, TAB-ANI-05, CMP-STA-10/20, CKG-STA-10) are restated as the twin-contract property they guarded: `attach` and `dispose` are idempotent and a repeated `attach` of the same element is a no-op, tested at L2. (3) Five framework-specific statements are withdrawn for the port and recorded in ADR 02's relaxation list, IDs kept in their indexes: NUM-EDGE-07 (hydration), POP-EDGE-08 (Fast Refresh, stale ref callback), CMB-STR-10 (owner-stack suffix), TAB-EVT-06 (Suspense), DLG-DOM-26 (rewritten without the hook name). Also decided the same day: CMP-EDGE-02 keeps its "reject in every build" clause, because Base UI's production placement loop never terminates for an over-wide grid item.


**2026-10-20, L4 runner (decided with ADR 09).** §1's L4 row reads *Playwright under the Node test runner (`@playwright/test`, TypeScript) against the fixture host* instead of *Playwright (C#, xUnit)*. Reasons: Base UI's titles, role queries and matchers translate nearly literally into TypeScript; the runner's projects, dependencies, `webServer`, sharding, `--repeat-each` and `--fail-on-flaky-tests` implement §2's rules and ADR 11's policies natively; the Node toolchain is already required for L3 and esbuild. Consequences for §2: the two render modes are two Playwright projects (`server`, `wasm`), the WebAssembly warm-up is a setup project the `wasm` project depends on, the fixture host is started by `webServer` (`dotnet run`) and reused locally, and C# `TimeProvider` control and diagnostic capture reach the host through test-only endpoints because the tests run in another process. L1 and L2 stay C#. The specs live in `tests/browser/` (ADR 09).

**2026-10-26, Tabs' pre-hydration rows withdrawn for the port (M3 planning; chosen by the user 2026-10-26).** Under rule 3 of the 2026-10-18 amendment and tabs §16 Q15 ("the pre-hydration script is replaced by the initial C# render plus the module on attach", decided 2026-10-18), TAB-API-11, TAB-DOM-34, TAB-DOM-35, TAB-DOM-36, TAB-DOM-37, TAB-DOM-38 and TAB-ANI-07 are withdrawn for the port under conventions §3.5 by the Tabs pull request, and `renderBeforeHydration` has no parameter: the prerendered Indicator stays hidden until attach (TAB-EDGE-09). Blazor replaces the prerendered subtree at activation (ADR 02 verification item 1), so a pre-hydration script's work would be discarded.
