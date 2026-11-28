using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

namespace Iodine.Headless.Interop;

/// <summary>
/// The handle of one attached element: the JavaScript object the module returned from <c>attach</c>, with
/// <see cref="UpdateAsync"/> to send new options and the references they name, <see cref="ReadAsync"/> for a value the
/// module's handle answers, and an idempotent <see cref="DisposeAsync"/> to detach.
/// </summary>
/// <typeparam name="TOptions">The options record of the module.</typeparam>
internal sealed class TwinHandle<TOptions> : IAsyncDisposable
    where TOptions : TwinOptions
{
    private readonly Task<IJSObjectReference?> reference;
    private readonly JsonTypeInfo<TOptions> optionsTypeInfo;
    private readonly Dictionary<string, TwinHandle<TOptions>> owner;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TwinHandle{TOptions}"/> class.</summary>
    /// <param name="key">The element reference id, element id or token the handle is attached to.</param>
    /// <param name="reference">The pending JavaScript handle; <see langword="null"/> when the circuit disconnected before <c>attach</c> completed.</param>
    /// <param name="optionsTypeInfo">The source-generated type information of <typeparamref name="TOptions"/>.</param>
    /// <param name="owner">The twin's handles by key, from which the handle removes itself once when it is disposed.</param>
    internal TwinHandle(string key, Task<IJSObjectReference?> reference, JsonTypeInfo<TOptions> optionsTypeInfo, Dictionary<string, TwinHandle<TOptions>> owner)
    {
        Key = key;
        this.reference = reference;
        this.optionsTypeInfo = optionsTypeInfo;
        this.owner = owner;
    }

    /// <summary>Gets the element reference id, element id or token this handle is attached to.</summary>
    /// <value>The key the twin uses to make a repeated attach of the same element or token a no-op.</value>
    public string Key { get; }

    /// <summary>Gets a value indicating whether <see cref="DisposeAsync"/> has run.</summary>
    /// <value><see langword="true"/> once disposed; otherwise <see langword="false"/>.</value>
    public bool IsDisposed => disposed;

    // Issue 132 (design point 10-132a (b), chosen by the user 2026-11-28): a repeated attach made while the attach is in
    // flight awaits it, then sends its options when they differ from the options and references last sent in that window.
    /// <summary>Gets the task of the module's <c>attach</c>, which completes when the handle exists in JavaScript.</summary>
    internal Task Attached => reference;

    /// <summary>Gets or sets the options sent with <c>attach</c>, or by the last repeated attach that forwarded its own.</summary>
    internal TOptions? SentOptions { get; set; }

    /// <summary>Gets or sets the JSON of <see cref="SentOptions"/>, serialized only when a repeated attach compares with it.</summary>
    internal string? SentJson { get; set; }

    /// <summary>Gets or sets the references argument sent with those options, or <see langword="null"/> when none were sent.</summary>
    internal object[]? SentReferences { get; set; }

    /// <summary>Gets or sets the last forward of a repeated attach, which the next one waits for so the forwards keep call order.</summary>
    internal Task? LastForward { get; set; }

    // PR 427's CI (run 46175183428, menu.filter-submenu.spec.ts:507): a forward is older than any update the part sent after
    // the repeated attach that carries it, so it is dropped then.
    /// <summary>Gets the number of updates the part sent through <see cref="UpdateAsync"/>.</summary>
    internal int PartUpdates { get; private set; }

    /// <inheritdoc />
    /// <remarks>Calls the module's <c>dispose</c> and releases the JavaScript reference. Idempotent: a second call does nothing.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        owner.Remove(Key);

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

    // ADR 04 Amendments 2026-11-26 (issue 312, chosen by the user 2026-11-26): a read the handle answers with a JSON string,
    // deserialized by the source-generated context (ADR 16 rule 4), so the interop result is always a string.
    /// <summary>Calls a method of the module's handle that answers a JSON string, and deserializes the answer.</summary>
    /// <typeparam name="TResult">The type of the answer.</typeparam>
    /// <param name="identifier">The name of the handle's method.</param>
    /// <param name="resultTypeInfo">The source-generated type information of <typeparamref name="TResult"/>.</param>
    /// <returns>
    /// The answer, or <see langword="null"/> when the method answered <see langword="null"/>, once disposed, when a dispose
    /// overtakes the call, or once the circuit is gone.
    /// </returns>
    public async ValueTask<TResult?> ReadAsync<TResult>(string identifier, JsonTypeInfo<TResult> resultTypeInfo)
        where TResult : class
    {
        if (disposed)
        {
            return null;
        }

        IJSObjectReference? handle = await reference.ConfigureAwait(false);
        if (handle is null)
        {
            return null;
        }

        try
        {
            string? json = await handle.InvokeAsync<string?>(identifier).ConfigureAwait(false);
            return json is null ? null : JsonSerializer.Deserialize(json, resultTypeInfo);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser has already discarded the module and its handles.
            return null;
        }
        catch (Exception exception) when (disposed && exception is JSException or ObjectDisposedException)
        {
            // PR 380's verifier nit 4: a dispose overtook the read; the module's handle or the reference is gone.
            return null;
        }
    }

    /// <summary>Sends new options to the module's <c>update</c>. A no-op once disposed, once the circuit is gone, or when a dispose overtakes it.</summary>
    /// <param name="options">The options record to serialize and send.</param>
    /// <param name="references">
    /// The elements and script-held objects the options name by index, sent after the options string; or
    /// <see langword="null"/>, or an empty instance, to send none.
    /// </param>
    /// <returns>A task that completes when the module has applied the options.</returns>
    public ValueTask UpdateAsync(TOptions options, TwinReferences? references = null)
    {
        if (!disposed)
        {
            PartUpdates++;
        }

        return SendAsync(options, references);
    }

    /// <summary>Sends options to the module's <c>update</c> as <see cref="UpdateAsync"/> does, without counting it as the part's.</summary>
    /// <param name="options">The options record to serialize and send.</param>
    /// <param name="references">The elements and script-held objects the options name by index.</param>
    /// <returns>A task that completes when the module has applied the options.</returns>
    internal async ValueTask SendAsync(TOptions options, TwinReferences? references)
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
            // ADR 04 Amendments 2026-10-27: the references follow the options string only when there are some; Amendments
            // 2026-10-28: their capture ids follow them when one of them is an element.
            string json = JsonSerializer.Serialize(options, optionsTypeInfo);
            object[]? argument = references?.ToArgument();
            string?[]? captures = TwinReferences.CaptureIds(elementCaptureId: null, references);
            if (argument is null)
            {
                await handle.InvokeVoidAsync("update", json).ConfigureAwait(false);
            }
            else if (captures is null)
            {
                await handle.InvokeVoidAsync("update", json, argument).ConfigureAwait(false);
            }
            else
            {
                await handle.InvokeVoidAsync("update", json, argument, captures).ConfigureAwait(false);
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser has already discarded the module and its handles.
        }
        catch (Exception exception) when (disposed && exception is JSException or ObjectDisposedException)
        {
            // PR 380's verifier nit 4: a dispose overtook the update; the module's handle or the reference is gone.
        }
    }
}
