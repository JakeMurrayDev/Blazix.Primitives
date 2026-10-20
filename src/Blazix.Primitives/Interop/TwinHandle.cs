using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

/// <summary>
/// One attached part's handle (ADR 04 §3): the <see cref="IJSObjectReference"/> the module returned from
/// <c>attach</c>, with <see cref="UpdateAsync"/> and an idempotent <see cref="DisposeAsync"/>.
/// </summary>
internal sealed class TwinHandle<TOptions> : IAsyncDisposable
    where TOptions : TwinOptions
{
    private readonly Task<IJSObjectReference?> reference;
    private readonly JsonTypeInfo<TOptions> optionsTypeInfo;
    private readonly Action<TwinHandle<TOptions>> released;
    private bool disposed;

    internal TwinHandle(string key, Task<IJSObjectReference?> reference, JsonTypeInfo<TOptions> optionsTypeInfo, Action<TwinHandle<TOptions>> released)
    {
        Key = key;
        this.reference = reference;
        this.optionsTypeInfo = optionsTypeInfo;
        this.released = released;
    }

    /// <summary>The element reference id or element id this handle is attached to.</summary>
    public string Key { get; }

    public bool IsDisposed => disposed;

    /// <summary>Sends the new options to the module; a no-op once disposed or once the circuit is gone.</summary>
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

    /// <summary>Tells the module to detach and releases the JavaScript reference. Idempotent.</summary>
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
