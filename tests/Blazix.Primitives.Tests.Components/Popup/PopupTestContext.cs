using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>A bUnit context with the library's services for the popup tests, and helpers to compose the test-only hosts.</summary>
internal sealed class PopupTestContext : IDisposable
{
    public PopupTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public BunitContext Context { get; } = new();

    public void Dispose() => Context.Dispose();

    /// <summary>Renders <paramref name="content"/> in a host whose render state the test changes and re-renders.</summary>
    public IRenderedComponent<PopupTestHost> Render(Action<RenderTreeBuilder, PopupTestHost> content, Dictionary<string, object?>? values = null) =>
        Context.Render<PopupTestHost>(p => p.Add(x => x.Content, content).Add(x => x.InitialValues, values));
}

/// <summary>A host that renders a test-supplied tree and re-renders it on request, so a test can add or remove parts in a batch.</summary>
internal sealed class PopupTestHost : ComponentBase
{
    [Parameter]
    public Action<RenderTreeBuilder, PopupTestHost>? Content { get; set; }

    [Parameter]
    public Dictionary<string, object?>? InitialValues { get; set; }

    public Dictionary<string, object?> Values { get; } = [];

    public Task SetAsync(string key, object? value)
    {
        Values[key] = value;
        return InvokeAsync(StateHasChanged);
    }

    public T? Get<T>(string key) => Values.TryGetValue(key, out object? value) ? (T?)value : default;

    protected override void OnInitialized()
    {
        if (InitialValues is not null)
        {
            foreach (KeyValuePair<string, object?> pair in InitialValues)
            {
                Values[pair.Key] = pair.Value;
            }
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => Content?.Invoke(builder, this);
}
