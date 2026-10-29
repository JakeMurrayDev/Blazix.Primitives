using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The misuse errors at L2 (PreviewCardPopup.test.tsx, PreviewCardTrigger.test.tsx, PreviewCardPositioner.test.tsx,
/// PreviewCardViewport.test.tsx; PRV-EDGE-01..04), with the port's prefix and code quoting (issue 122).
/// </summary>
public sealed class PreviewCardMisuseTests : IDisposable
{
    private const string RootMissing = "Blazix: `PreviewCardRootContext` is missing. PreviewCard parts must be placed within `<PreviewCardRoot>`.";
    private const string PositionerMissing = "Blazix: `PreviewCardPositionerContext` is missing. `PreviewCardPositioner` parts must be placed within `<PreviewCardPositioner>`.";

    private readonly BunitContext context = new();

    public PreviewCardMisuseTests()
    {
        context.Services.AddBlazixPrimitives();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <PreviewCard.Root>")]
    [Trait("Requirement", "PRV-EDGE-01")]
    [Trait("Requirement", "PRV-STR-09")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_root()
    {
        // Base UI's title renders the Popup; every part below the Root reads the same context.
        Assert.Equal(RootMissing, Assert.Throws<InvalidOperationException>(() => context.Render<PreviewCardPopup>()).Message);
        Assert.Equal(RootMissing, Assert.Throws<InvalidOperationException>(() => context.Render<PreviewCardPortal>()).Message);
        Assert.Equal(RootMissing, Assert.Throws<InvalidOperationException>(() => context.Render<PreviewCardPositioner>()).Message);
        Assert.Equal(RootMissing, Assert.Throws<InvalidOperationException>(() => context.Render<PreviewCardArrow>()).Message);
        Assert.Equal(RootMissing, Assert.Throws<InvalidOperationException>(() => context.Render<PreviewCardViewport>()).Message);
        Assert.Equal(RootMissing, Assert.Throws<InvalidOperationException>(() => context.Render<PreviewCardBackdrop>()).Message);
    }

    [Fact(DisplayName = "throws without PreviewCard.Root or a handle")]
    [Trait("Requirement", "PRV-EDGE-02")]
    public void Throws_without_the_root_or_a_handle()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<PreviewCardTrigger<NoPayload>>());

        Assert.Equal("Blazix: `<PreviewCardTrigger>` must be either used within a `<PreviewCardRoot>` component or provided with a handle.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <PreviewCard.Portal>")]
    [Trait("Requirement", "PRV-EDGE-03")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_portal()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RenderOpen(builder =>
        {
            builder.OpenComponent<PreviewCardPositioner>(0);
            builder.CloseComponent();
        }));

        Assert.Equal("Blazix: `<PreviewCardPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <PreviewCard.Positioner>")]
    [Trait("Requirement", "PRV-EDGE-04")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_positioner()
    {
        // PreviewCardPopup.test.tsx and PreviewCardViewport.test.tsx; the Arrow reads the same context.
        Assert.Equal(PositionerMissing, Assert.Throws<InvalidOperationException>(() => RenderOpen(Part<PreviewCardPopup>)).Message);
        Assert.Equal(PositionerMissing, Assert.Throws<InvalidOperationException>(() => RenderOpen(Part<PreviewCardViewport>)).Message);
        Assert.Equal(PositionerMissing, Assert.Throws<InvalidOperationException>(() => RenderOpen(Part<PreviewCardArrow>)).Message);
    }

    private static void Part<TPart>(RenderTreeBuilder builder)
        where TPart : IComponent
    {
        builder.OpenComponent<TPart>(0);
        builder.CloseComponent();
    }

    private IRenderedComponent<PreviewCardRoot<NoPayload>> RenderOpen(RenderFragment content) =>
        context.Render<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.ChildContent, content));
}
