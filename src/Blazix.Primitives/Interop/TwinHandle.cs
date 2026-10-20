using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

/// <summary>
/// The handle of one attached element: the JavaScript object the module returned from <c>attach</c>, with
/// <see cref="UpdateAsync"/> to send new options and an idempotent <see cref="DisposeAsync"/> to detach.
/// </summary>
/// <typeparam name="TOptions">The options record of the module.</typeparam>
internal sealed class TwinHandle<TOptions> : IAsyncDisposable
    where TOptions : TwinOptions
{
    private readonly Task<IJSObjectReference?> reference;
    private readonly JsonTypeInfo<TOptions> optionsTypeInfo;
    private readonly Action<TwinHandle<TOptions>> released;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TwinHandle{TOptions}"/> class.</summary>
    /// <param name="key">The element reference id or element id the handle is attached to.</param>
    /// <param name="reference">The pending JavaScript handle; <see langword="null"/> when the circuit disconnected before <c>attach</c> completed.</param>
    /// <param name="optionsTypeInfo">The source-generated type information of <typeparamref name="TOptions"/>.</param>
    /// <param name="released">Called once when the handle is disposed, so the twin forgets it.</param>
    internal TwinHandle(string key, Task<IJSObjectReference?> reference, JsonTypeInfo<TOptions> optionsTypeInfo, Action<TwinHandle<TOptions>> released)
    {
        Key = key;
        this.reference = reference;
        this.optionsTypeInfo = optionsTypeInfo;
        this.released = released;
    }

    /// <summary>Gets the element reference id or element id this handle is attached to.</summary>
    /// <value>The key the twin uses to make a repeated attach of the same element a no-op.</value>
    public string Key { get; }

    /// <summary>Gets a value indicating whether <see cref="DisposeAsync"/> has run.</summary>
    /// <value><see langword="true"/> once disposed; otherwise <see langword="false"/>.</value>
    public bool IsDisposed => disposed;

    /// <summary>Sends new options to the module's <c>update</c>. A no-op once disposed or once the circuit is gone.</summary>
    /// <param name="options">The options record to serialise and send.</param>
    /// <returns>A task that completes when the module has applied the options.</returns>
    public async ValueTask UpdateAsync(TOptions options)
    {
        if (disposed)
        {
            return;
        }

        IJSObjectReference? handle = await reference.ConfigureAwait(false);
        if (handle is null)
        {
            return;
        }

        try
        {
            await handle.InvokeVoidAsync("update", JsonSerializer.Serialize(options, optionsTypeInfo)).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser has already discarded the module and its handles.
        }
    }

    /// <inheritdoc />
    /// <remarks>Calls the module's <c>dispose</c> and releases the JavaScript reference. Idempotent: a second call does nothing.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        released(this);

        IJSObjectReference? handle = await reference.ConfigureAwait(false);
        if (handle is null)
        {
            return;
        }

        try
        {
            await handle.InvokeVoidAsync("dispose").ConfigureAwait(false);
            await handle.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser has already discarded the module and its handles.
        }
    }
}
