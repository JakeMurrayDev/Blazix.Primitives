using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.AccordionFixtures;

/// <summary>
/// The base of the Accordion fixtures (tests/browser/accordion.spec.ts): renders <c>data-attached</c> once every listed
/// trigger and initially rendered panel has its browser-side behavior attached (the specs wait for it before they press
/// anything) and counts the sync button the specs click as a barrier.
/// </summary>
public abstract class AccordionFixtureBase : ComponentBase
{
    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>The triggers and initially rendered panels of the page, read after the first interactive render.</summary>
    protected abstract IEnumerable<object?> Parts { get; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(Parts.Select(part => part switch
        {
            AccordionTrigger<string> trigger => trigger.TwinAttached,
            AccordionPanel<string> panel => panel.TwinAttached,
            AccordionTrigger<int> trigger => trigger.TwinAttached,
            AccordionPanel<int> panel => panel.TwinAttached,
            AccordionRevealCase reveal => reveal.TwinAttached,
            _ => Task.CompletedTask,
        }));
        Attached = true;
        StateHasChanged();
    }

    /// <summary>The literal of a transition status, for a class function.</summary>
    protected static string Literal(TransitionStatus? status) => status switch
    {
        TransitionStatus.Starting => "starting",
        TransitionStatus.Ending => "ending",
        TransitionStatus.Idle => "idle",
        _ => "none",
    };
}
