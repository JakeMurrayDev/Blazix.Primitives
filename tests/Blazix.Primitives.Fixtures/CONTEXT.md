# tests/Blazix.Primitives.Fixtures

The fixture host: a Blazor Web App that serves every fixture page in both render modes for the L4 browser tests. The app for trying the port by hand is the sample application under `samples/`.

## Inputs

- `../Blazix.Primitives.Fixtures.Client` (the fixture components, the catalog and the harness services) and, through it, `../../src/Blazix.Primitives`.
- Decision records 01 (render modes), 06 §2 (browser harness rules) and 09 §1.
- `tests/browser/*.spec.ts`, which start it with `dotnet run` through Playwright's `webServer` and poll `/_fixture/ready`.

## Job

- `Components/Pages/FixturePage.razor`: the one route, `/{mode}/{fixture}`. The page is static; each root of the catalog entry is an interactive island with `InteractiveServerRenderMode` or `InteractiveWebAssemblyRenderMode` built from the segment, prerendering off unless the entry says otherwise. An unknown mode or fixture answers 404.
- `Program.cs`: the harness services (`AddFixtureHarness`: one `FixtureSession` per circuit with its `FakeTimeProvider` as `TimeProvider`, the capturing logger provider), the hub options (keep-alive 2 s, client timeout 10 s, mirrored by the initializer), and the test-only endpoints `GET /_fixture/ready` and `GET /_fixture/diagnostics/{token}`.
- `wwwroot/Blazix.Primitives.Fixtures.lib.module.js`: sets `data-blazix-started` on `<html>` in `afterWebStarted`; sets `BASE_UI_ANIMATIONS_DISABLED` unless `?animations=1`; defines `window.blazixFixture` (`register`, `advanceTime`, `diagnostics`), the per-page channel to the session's `[JSInvokable]` methods. It also defines `blazixFixtureDom` (`createClosedShadowRoot`, `createOpenShadowRoot`): the shadow roots a fixture's portal takes as its container, created after the first render and handed to C# as an IJSObjectReference.
- `Components/App.razor`: tags prerendered fixture nodes before Blazor starts (ADR-02/1).
- `Blazix.Primitives.Fixtures.csproj`: turns the library's development warnings on for the Server-rendered fixtures (`RuntimeHostConfigurationOption`), so their messages reach the captured diagnostics (first used by Collapsible, COL-EDGE-02/03).

## Outputs

- A running site on the port Playwright configures (`http://127.0.0.1:5197`); the readiness markers `data-interactive` and `data-blazix-started` are the only thing a test waits for.
- Not built or run by the `build-and-test` job; the browser jobs build it.

## Human check

- Every fixture container is `FixtureContainer`, so `data-interactive`, `data-renderer` and `data-session` are always there; no fixture needs a sleep.
- Test-only endpoints and hooks exist only in the host and the client, never in the library.
- A fixture is a component in the client project, not copy in the host.
