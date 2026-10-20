# tests/Blazix.Primitives.Tests.Components

L2: bUnit, including the twin contract tests.

## Inputs

- `../../src/Blazix.Primitives`, and bUnit and `Microsoft.Extensions.TimeProvider.Testing` from `Directory.Packages.props`.
- The `Test:` lines tagged `[L2]` in `docs/components/*.md`; the conformance suite RND-DOM-36..49; decision record 06 §1.

## Job

Renders every part through a `BunitContext`: the conformance suite, the controlled and uncontrolled contract, request sequences and cancelation for C#-dispatched events, form integration, and the rendered attributes and ARIA that C# owns. Twin contract tests record the `attach`/`update`/`dispose` calls on bUnit's JS interop mock and drive the twin's `[JSInvokable]` callbacks directly. It has no DOM, no focus model and no JavaScript, so anything that needs one goes to L4 (a test never fakes a layer above it).

## Outputs

- A pass or fail per test through `dotnet test --project tests/Blazix.Primitives.Tests.Components`.
- Run by the `build-and-test` CI job on every change outside `docs/`.

## Human check

- A test that pretends to check focus, pointer or keyboard behaviour here is wrong: it belongs in `tests/browser`.
- Each twin options record has a contract test, so a change to it breaks L2 before L4.
- Render-count requirements are asserted through bUnit's render counts (ADR 06 §1 amendment).
