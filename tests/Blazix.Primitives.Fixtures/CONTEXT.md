# tests/Blazix.Primitives.Fixtures

The fixture host: a Blazor Web App that serves every fixture page in both render modes for the L4 browser tests, and doubles as the showcase.

## Inputs

- `../Blazix.Primitives.Fixtures.Client` (the fixture components, WebAssembly-capable) and, from Track 3 on, `../../src/Blazix.Primitives`.
- Decision records 01 (render modes), 06 §2 (browser harness rules) and 09 §1.
- `tests/browser/*.spec.ts`, which start it with `dotnet run` through Playwright's `webServer` and wait on its readiness endpoint.

## Job

Hosts Server and WebAssembly interactivity from the same fixture components; the render mode is a route segment resolved to an `IComponentRenderMode` expression, prerendering is off except on the dedicated prerender fixtures. It carries the test-only endpoints (readiness, `TimeProvider` control, captured diagnostics) because the tests run in another process. Today it is the `dotnet new blazor --interactivity Auto --empty` template and nothing more.

## Outputs

- A running site on the port Playwright configures; the readiness markers `data-interactive` and `data-blazix-started` are the only thing a test waits for.
- Not built or run by the `build-and-test` job; the browser jobs build it.

## Human check

- Every fixture container renders `data-interactive="@RendererInfo.IsInteractive"`; no fixture needs a sleep.
- Test-only endpoints exist only in the host, never in the library.
- A fixture is a component in the client project, not copy in the host.
