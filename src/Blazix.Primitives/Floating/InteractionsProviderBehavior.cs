using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADR 04 Amendments 2026-10-27 (hover timers and the delay group; chosen by the user 2026-10-27): a tooltip provider
// renders no element, draws a provider token and cascades it; the delay group is one object per token in the module
// (FLT-ALG-23). Its tooltips' triggers carry the token as InteractionsHoverOptions.DelayGroupToken.
/// <summary>A tooltip provider's handle on the <c>interactions</c> module: the token its tooltips join and the group's delays.</summary>
internal sealed class InteractionsProviderBehavior : IAsyncDisposable
{
    private readonly InteractionsAttachment attachment;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="InteractionsProviderBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="idGenerator">The source of the provider token.</param>
    public InteractionsProviderBehavior(IJSRuntime jsRuntime, PartIdGenerator idGenerator)
    {
        Token = idGenerator.Next();
        attachment = new InteractionsAttachment(new InteractionsTwin(jsRuntime));
    }

    /// <summary>Gets the provider token its tooltips' triggers carry; it is never rendered.</summary>
    /// <value>A token drawn once for the provider's lifetime.</value>
    public string Token { get; }

    /// <summary>Gets or sets the group's open delay.</summary>
    /// <value>The delay in milliseconds, or <see langword="null"/> for none.</value>
    public int? DelayOpen { get; set; }

    /// <summary>Gets or sets the group's close delay.</summary>
    /// <value>The delay in milliseconds, or <see langword="null"/> for none.</value>
    public int? DelayClose { get; set; }

    /// <summary>Gets or sets how long the group stays in its instant phase after its last tooltip closed.</summary>
    /// <value>The time in milliseconds.</value>
    public int Timeout { get; set; }

    /// <summary>Gets the number of option records sent to the module since the attach, the attach's included.</summary>
    /// <value>The count, which an unchanged render does not move.</value>
    internal int Sends => attachment.Sends;

    /// <summary>Gets a task that completes when the handle is attached.</summary>
    /// <value>A task that completes after the first interactive render has attached the handle.</value>
    internal Task TwinAttached => attachment.Attached;

    /// <inheritdoc />
    /// <remarks>Releases the provider's handle and the twin. Idempotent.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await attachment.DisposeAsync();
    }

    /// <summary>Attaches the provider's handle after its first render, then sends the delays that changed.</summary>
    /// <returns>A task that completes when the module has the options.</returns>
    public Task AfterRenderAsync() => attachment.SyncAsync(new InteractionsTarget(null, null, Token), Unchanged, BuildOptions);

    private bool Unchanged(InteractionsOptions sent) =>
        sent.Provider is { } provider && provider.DelayOpen == DelayOpen && provider.DelayClose == DelayClose && provider.Timeout == Timeout;

    private InteractionsOptions BuildOptions() => new()
    {
        Provider = new InteractionsProviderOptions { Token = Token, DelayOpen = DelayOpen, DelayClose = DelayClose, Timeout = Timeout },
    };
}
