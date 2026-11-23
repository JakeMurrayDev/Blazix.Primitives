using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The bUnit context of the Toast tests: the library's services, a <see cref="FakeTimeProvider"/> registered as the
/// scope's clock (the providers' timers run on it), bUnit's JS interop mock in loose mode for the button and portal
/// modules, and an interactive renderer, so a provider subscribes to its manager (ADR 05 Amendments 2026-11-18 item 3).
/// </summary>
internal sealed class ToastTestContext : IDisposable
{
    public ToastTestContext(bool interactive = true)
    {
        Context.Services.AddIodineHeadless();
        Context.Services.AddSingleton<TimeProvider>(Clock);
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.Renderer.SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
    }

    public BunitContext Context { get; } = new();

    public FakeTimeProvider Clock { get; } = new();

    // The manager AddIodineHeadless registers for the scope, which a provider without a Manager parameter uses.
    public ToastManager Manager => Context.Services.GetRequiredService<ToastManager>();

    public void Dispose() => Context.Dispose();

    public IRenderedComponent<ToastHost> RenderHost(Action<ComponentParameterCollectionBuilder<ToastHost>>? parameters = null) =>
        Context.Render(parameters ?? (_ => { }));

    // Runs a manager call on the renderer's context, as a consumer's handler would.
    public void Run<TComponent>(IRenderedComponent<TComponent> cut, Action action)
        where TComponent : IComponent => cut.InvokeAsync(action).GetAwaiter().GetResult();

    public void Advance<TComponent>(IRenderedComponent<TComponent> cut, int milliseconds)
        where TComponent : IComponent => cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(milliseconds))).GetAwaiter().GetResult();

    // The styles the roots send to the `toast` module, in document order: after the attach the module writes the element's
    // style from them, and the rendered style stays the one of the attach (the handover).
    public static IReadOnlyList<string?> RootStyles(IRenderedComponent<ToastHost> cut) =>
        [.. cut.FindComponents<ToastRoot<NoPayload>>().Select(root => root.Instance.SentStyle)];

    public static IReadOnlyList<IElement> Roots<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent => cut.FindAll("[data-testid=root]");
}
