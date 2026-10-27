using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.ScrollLockFixtures;

/// <summary>
/// The test-only scroll lock at L4 for the <c>scroll-lock-*</c> fixtures: a <c>div</c> whose document is locked while
/// <see cref="Enabled"/>, through <see cref="PopupScrollLockBehavior"/> as the popup families compose it.
/// </summary>
public sealed class FixtureScrollLock : ComponentBase, IAsyncDisposable
{
    private PopupScrollLockBehavior? behavior;
    private ElementReference element;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter]
    public bool Enabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (behavior is not null)
        {
            await behavior.DisposeAsync();
            behavior = null;
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "data-scroll-lock-enabled", Enabled ? "true" : "false");
        builder.AddElementReferenceCapture(3, reference => element = reference);
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        behavior ??= new PopupScrollLockBehavior(JSRuntime);
        return behavior.ApplyAsync(element, Enabled);
    }
}
