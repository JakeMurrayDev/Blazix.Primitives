using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.FieldFixtures;

/// <summary>
/// The base of the Field, Fieldset, Input and Form fixtures (tests/browser/field.spec.ts, fieldset.spec.ts, input.spec.ts, form.spec.ts):
/// renders <c>data-attached</c> once every listed form, control and label has the form module attached (the specs wait for it
/// before they type or click) and counts the sync button the specs click as a barrier.
/// </summary>
public abstract class FieldFixtureBase : ComponentBase
{
    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>The controls and labels of the page, read after the first interactive render.</summary>
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
            FieldControl control => control.TwinAttached,
            Input input => input.TwinAttached,
            FieldLabel label => label.TwinAttached,
            Form form => form.TwinAttached,
            _ => Task.CompletedTask,
        }));
        Attached = true;
        StateHasChanged();
    }
}
