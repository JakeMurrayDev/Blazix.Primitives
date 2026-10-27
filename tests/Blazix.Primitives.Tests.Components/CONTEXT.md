# tests/Blazix.Primitives.Tests.Components

L2: bUnit, including the twin contract tests.

## Inputs

- `../../src/Blazix.Primitives`, and bUnit and `Microsoft.Extensions.TimeProvider.Testing` from `Directory.Packages.props`; the generators as an analyzer, so the project's own state records are projected (`[PartState]`) and given their `Copy` and `ShallowEquals` (`[StoreState]`) as a consumer's would be.
- The `Test:` lines tagged `[L2]` in `docs/components/*.md`; the conformance suite RND-DOM-36..49; decision record 06 §1.
- The project uses the Razor SDK so `.razor` test consumers (`Rendering/Consumers/`) exercise the consumer-facing forms: a substitute written as static markup, `<Render>` and `<RenderFunction Context="props">` child content.

## Job

Renders every part through a `BunitContext` (with `Services.AddBlazixPrimitives()`): the conformance suite, the controlled and uncontrolled contract, request sequences and cancelation for C#-dispatched events, form integration, and the rendered attributes and ARIA that C# owns. The conformance suite is `Rendering/Conformance/PartConformanceTests<TPart>`: a family derives from it, supplies `ConformanceOptions<TPart>` (tag name, substitution tag, button, wrapping) and inherits every RND-DOM-36..49 test with Base UI's title; `SampleRootConformanceTests` is the reference run on the sample part `SampleRoot`. Twin contract tests record the `attach`/`update`/`dispose` calls on bUnit's JS interop mock and drive the twin's `[JSInvokable]` callbacks directly. It has no DOM, no focus model and no JavaScript, so anything that needs one goes to L4 (a test never fakes a layer above it).

## Outputs

- A pass or fail per test through `dotnet test --project tests/Blazix.Primitives.Tests.Components`.
- Run by the `build-and-test` CI job on every change outside `docs/`.

## Human check

- A test that pretends to check focus, pointer or keyboard behaviour here is wrong: it belongs in `tests/browser`.
- Each twin options record has a contract test, so a change to it breaks L2 before L4.
- Render-count requirements are asserted through bUnit's render counts (ADR 06 §1 amendment).
- bUnit's markup shows one element-reference capture per element (the first written, the part's own); a second capture is asserted non-null, not located (ADR 06 §1: non-null capture for RND-DOM-44/45).
- An allocation budget (`Rendering/AllocationBudgetTests`, `Button/ButtonAllocationBudgetTests`, `Separator/SeparatorAllocationBudgetTests`, `Avatar/AvatarAllocationBudgetTests`, `Field/FieldAllocationBudgetTests`, `Form/FormAllocationBudgetTests`, `Floating/PositionerAllocationBudgetTests`: 6352 bytes for a positioner with an arrow, the envelope of 46 full runs, 2026-10-27) only tightens (ADR 16 rule 7). Every budget class, the `*RenderBudgetTests` too, is in the `AllocationBudgets` collection, which runs with parallelization disabled (`AllocationBudgetsCollection`; issue 112, ADR 16 Amendments 2026-10-27). A budget whose operation may hit bUnit's list doubling takes the least of five samples.
- The development warnings switch is on for the whole run (`Button/DevelopmentWarningsSwitch`, a module initializer), as in a Debug build of an app; a test that needs it off sets the option it reads explicitly.
- Family tests use the project's root namespace (and a `.razor` consumer in a family folder sets `@namespace`), because a namespace named after the family would hide the part's type.
- `Rendering/RenderPipelineSequenceTests` reads a part's frames through a `Renderer` of its own, because bUnit reaches them only through a protected member; its Razor consumers (`Rendering/Consumers/CaptureConsumer.razor`) expose their fragments so the test can compare the consumer's own sequence numbers with the re-emitted ones.
