using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// The attach-then-flush cycle every interactions behaviour shares (the CompositeRootBehavior shape): attach after the
// first render that has the element (by ElementReference, by the rendered id, or by token for a part without one), then
// send options only when they changed. The comparison runs before a record is built (ADR 16 rule 7), so an unchanged
// render allocates and sends nothing.
/// <summary>One part's handle on the <c>interactions</c> module: its attach, its option sends and its release.</summary>
internal sealed class InteractionsAttachment : IAsyncDisposable
{
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<InteractionsOptions>? handle;
    private InteractionsOptions? sentOptions;
    private bool attaching;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="InteractionsAttachment"/> class.</summary>
    /// <param name="twin">The part's own twin.</param>
    public InteractionsAttachment(InteractionsTwin twin)
    {
        Twin = twin;
    }

    /// <summary>Gets the part's twin.</summary>
    /// <value>The twin the attachment attaches through.</value>
    public InteractionsTwin Twin { get; }

    /// <summary>Gets the options last sent to the module.</summary>
    /// <value>The options, or <see langword="null"/> before the attach.</value>
    public InteractionsOptions? SentOptions => sentOptions;

    /// <summary>Gets a task that completes when the first attach succeeded.</summary>
    /// <value>A task that completes after the first interactive render has attached the handle.</value>
    public Task Attached => attachedSource.Task;

    /// <summary>Gets the number of option records sent since the attach, the attach's included.</summary>
    /// <value>The count, which an unchanged render does not move.</value>
    public int Sends { get; private set; }

    /// <inheritdoc />
    /// <remarks>Releases the handle and the twin. Idempotent.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DetachAsync();
        await Twin.DisposeAsync();
    }

    /// <summary>Attaches on the first call that can, then sends the options when they changed.</summary>
    /// <param name="target">Where to attach: an element, an element id or a token.</param>
    /// <param name="unchanged">Compares the part's inputs with the options last sent, before any record is built.</param>
    /// <param name="build">Builds the options from the part's inputs.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task SyncAsync(InteractionsTarget target, Func<InteractionsOptions, bool> unchanged, Func<InteractionsOptions> build)
    {
        if (disposed || attaching)
        {
            return;
        }

        if (handle is null)
        {
            InteractionsOptions options = build();
            attaching = true;
            try
            {
                handle = target switch
                {
                    { Element: { } element } => await Twin.AttachAsync(element, options),
                    { ElementId: { } id } => await Twin.AttachAsync(id, options),
                    { Token: { } token } => await Twin.AttachTokenAsync(token, options),
                    _ => null,
                };
            }
            finally
            {
                attaching = false;
            }

            if (handle is null)
            {
                return;
            }

            sentOptions = options;
            Sends++;
            attachedSource.TrySetResult();

            // A render while the attach was in flight returned early: send what changed meanwhile.
        }

        await FlushAsync(unchanged, build);
    }

    /// <summary>Releases the handle, so a later sync attaches again.</summary>
    /// <returns>A task that completes when the module released the handle.</returns>
    public async Task DetachAsync()
    {
        if (handle is { } released)
        {
            handle = null;
            sentOptions = null;
            await released.DisposeAsync();
        }
    }

    private async Task FlushAsync(Func<InteractionsOptions, bool> unchanged, Func<InteractionsOptions> build)
    {
        if (handle is null || (sentOptions is { } sent && unchanged(sent)))
        {
            return;
        }

        InteractionsOptions next = build();
        sentOptions = next;
        Sends++;
        await handle.UpdateAsync(next);
    }
}

/// <summary>Where an interactions handle attaches: exactly one of an element, an element id or a token.</summary>
/// <param name="Element">The element, when the part has a reference to it.</param>
/// <param name="ElementId">The id of the element, under a substitute callback.</param>
/// <param name="Token">The token of a part that renders no element.</param>
internal readonly record struct InteractionsTarget(ElementReference? Element, string? ElementId, string? Token);
