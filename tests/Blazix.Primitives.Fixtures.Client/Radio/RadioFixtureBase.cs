using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.RadioFixtures;

/// <summary>
/// The base of the Radio and RadioGroup fixtures: renders <c>data-attached</c> once every tracked radio has its root,
/// hidden-input and field behaviors attached, every tracked group its composite behavior and every tracked indicator its
/// transitions twin (the specs wait for it before they click or press a key), and counts the sync button the specs click
/// as a barrier. A part is tracked by assigning it to <see cref="Radio"/>, <see cref="Group"/> or
/// <see cref="Indicator"/> with <c>@ref</c>, which a fixture may do from as many parts as it renders.
/// </summary>
public abstract class RadioFixtureBase : ComponentBase
{
    private readonly List<Func<Task>> attached = [];

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>Sets a radio of type <see cref="string"/> to track.</summary>
    protected RadioRoot<string>? Radio
    {
        set => Track(value, radio => radio.TwinAttached);
    }

    /// <summary>Sets a radio of type <see cref="int"/>? to track.</summary>
    protected RadioRoot<int?>? NumberRadio
    {
        set => Track(value, radio => radio.TwinAttached);
    }

    /// <summary>Sets a group of type <see cref="string"/> to track.</summary>
    protected RadioGroup<string>? Group
    {
        set => Track(value, group => group.TwinAttached);
    }

    /// <summary>Sets a group of type <see cref="int"/>? to track.</summary>
    protected RadioGroup<int?>? NumberGroup
    {
        set => Track(value, group => group.TwinAttached);
    }

    /// <summary>Sets an indicator rendered at the first interactive render to track.</summary>
    protected RadioIndicator? Indicator
    {
        set => Track(value, indicator => indicator.TwinAttached);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(attached.Select(task => task()));
        Attached = true;
        StateHasChanged();
    }

    private void Track<T>(T? part, Func<T, Task> twinAttached)
        where T : class
    {
        if (part is not null && !Attached)
        {
            attached.Add(() => twinAttached(part));
        }
    }
}
