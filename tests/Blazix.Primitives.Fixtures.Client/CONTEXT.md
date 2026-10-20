# tests/Blazix.Primitives.Fixtures.Client

The fixture components and the harness services, in a Blazor WebAssembly project so the host can render them in both interactive modes.

## Inputs

- `../../src/Blazix.Primitives` (internals visible: the probe twin derives from `TwinBase`), `Microsoft.AspNetCore.Components.WebAssembly` and `Microsoft.Extensions.TimeProvider.Testing` on the pinned versions.
- `docs/components/<family>.md` §13: the scenarios the browser tests need a page for.
- Decision record 06 §2: readiness markers, prerendering off by default, clocks, diagnostics.

## Job

- `FixtureCatalog`: name → `FixtureEntry(Component, Prerender, Roots)`; the host resolves `/{mode}/{fixture}` through it.
- `FixtureContainer.razor`: the wrapper every fixture renders: `data-fixture`, `data-interactive`, `data-renderer`, `data-session`; binds the circuit's dispatcher to the session for diagnostics attribution and registers the session's object reference with `window.blazixFixture` on the first interactive render.
- `Harness/`: `FixtureSession` (scoped: token, `FakeTimeProvider`, captured diagnostics, `[JSInvokable] AdvanceTime` and `DiagnosticsJson`), `FixtureSessionRegistry` (singleton: by token for the endpoints, by renderer `SynchronizationContext` for the logger), `CapturingLoggerProvider`, `ActivationCounter`, `AddFixtureHarness`.
- `Probe/`: the harness canary: `ProbeTwin`, `ProbeOptions`/`ProbeClick`, `ProbeFixture.razor`; `js/probe.ts` is its module, built by the library's esbuild script into `wwwroot/js/` (ignored by git) and served at `/js/probe.js` (the client project is served at the site root).
- `Prerender/`, `Allocation/`, `KeepAlive/`: the fixtures of the M0 verification items (ADR-02/1, ADR-06/1, ADR-05/3, ADR-06/2).

## Outputs

- The client assembly and its WebAssembly bundle, served by `../Blazix.Primitives.Fixtures`.
- The pages `tests/browser/*.spec.ts` navigate to.

## Human check

- A fixture needs no code the user of the library would not write, except the harness canary.
- It renders identically in both render modes except where ADR 01's relaxation table says otherwise.
- A page exists for each `Test:` row tagged `[L4]` that needs one, for each verification item, and for no other reason.
