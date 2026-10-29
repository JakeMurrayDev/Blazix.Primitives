using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

/// <summary>
/// The C# half of one JavaScript module. A twin imports its module lazily on first use, attaches parts to it
/// (<c>attach(elementOrId, options, self, references)</c> returning a handle per element), owns the single
/// <see cref="DotNetObjectReference{TValue}"/> the module calls back through, and answers the module's decision
/// points. Parts talk to twins and never to <see cref="IJSRuntime"/>.
/// </summary>
/// <typeparam name="TTwin">The concrete twin, whose public <c>[JSInvokable]</c> methods are the module's callbacks.</typeparam>
/// <typeparam name="TOptions">The module's options record, listed in <see cref="BlazixJsonContext"/>.</typeparam>
internal abstract class TwinBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TTwin, TOptions> : IAsyncDisposable
    where TTwin : TwinBase<TTwin, TOptions>
    where TOptions : TwinOptions
{
    private readonly IJSRuntime jsRuntime;
    private readonly string modulePath;
    private readonly JsonTypeInfo<TOptions> optionsTypeInfo;
    private readonly Dictionary<string, TwinHandle<TOptions>> handles = new(StringComparer.Ordinal);
    private DotNetObjectReference<TTwin>? selfReference;
    private Task<IJSObjectReference?>? module;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TwinBase{TTwin, TOptions}"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    /// <param name="modulePath">The module URL, for example <c>./_content/Blazix.Primitives/js/button.js</c>.</param>
    /// <param name="optionsTypeInfo">The entry of <typeparamref name="TOptions"/> in <see cref="BlazixJsonContext"/>.</param>
    protected TwinBase(IJSRuntime jsRuntime, string modulePath, JsonTypeInfo<TOptions> optionsTypeInfo)
    {
        this.jsRuntime = jsRuntime;
        this.modulePath = modulePath;
        this.optionsTypeInfo = optionsTypeInfo;
    }

    /// <inheritdoc />
    /// <remarks>Disposes every live handle, the module reference and the .NET object reference. Idempotent.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (TwinHandle<TOptions> handle in handles.Values.ToArray())
        {
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        selfReference?.Dispose();

        if (module is { IsCompletedSuccessfully: true, Result: { } imported })
        {
            try
            {
                await imported.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; nothing to release on the JavaScript side.
            }
        }
    }

    /// <summary>Returns the URL of a module that ships with the library.</summary>
    /// <param name="module">The module's file name without extension, for example <c>button</c>.</param>
    /// <returns><c>./_content/Blazix.Primitives/js/&lt;module&gt;.js</c>.</returns>
    protected static string LibraryModule(string module) => $"./_content/Blazix.Primitives/js/{module}.js";

    /// <summary>
    /// Attaches the module to an element. Attaching is idempotent per element: a repeated call for the same element
    /// returns the existing handle and sends nothing to JavaScript.
    /// </summary>
    /// <param name="element">The element to attach to.</param>
    /// <param name="options">The options sent with <c>attach</c>.</param>
    /// <param name="references">
    /// The elements and script-held objects the options name by index, sent after the twin's reference; or
    /// <see langword="null"/>, or an empty instance, to send none.
    /// </param>
    /// <returns>The handle for the element.</returns>
    /// <exception cref="ObjectDisposedException">The twin has been disposed.</exception>
    public ValueTask<TwinHandle<TOptions>> AttachAsync(ElementReference element, TOptions options, TwinReferences? references = null)
    {
        return AttachAsync(element.Id, element, element.Id, options, references);
    }

    /// <summary>
    /// Attaches the module to the element with the given id. Used when the part has no
    /// <see cref="ElementReference"/>, which is the case when a consumer substitutes the element through a callback.
    /// </summary>
    /// <param name="elementId">The id of the element in the document.</param>
    /// <param name="options">The options sent with <c>attach</c>.</param>
    /// <param name="references">
    /// The elements and script-held objects the options name by index, sent after the twin's reference; or
    /// <see langword="null"/>, or an empty instance, to send none.
    /// </param>
    /// <returns>The handle for the element.</returns>
    /// <exception cref="ObjectDisposedException">The twin has been disposed.</exception>
    public ValueTask<TwinHandle<TOptions>> AttachAsync(string elementId, TOptions options, TwinReferences? references = null)
    {
        return AttachAsync(elementId, elementId, elementCaptureId: null, options, references);
    }

    // ADR 04 Amendments 2026-10-27 (decision 3, the token form; chosen by the user 2026-10-27): a popup Root, a tooltip
    // provider or a floating tree renders no element, so its handle is keyed by a token C# draws and never renders.
    /// <summary>
    /// Attaches the module without an element, by a token the part draws and never renders: the handle of a part that
    /// renders no element of its own. JavaScript receives <see langword="null"/> as the element and reads the token from
    /// the options. Attaching is idempotent per token.
    /// </summary>
    /// <param name="token">The token, for example one drawn with <c>PartIdGenerator.Next()</c>.</param>
    /// <param name="options">The options sent with <c>attach</c>, which carry the token.</param>
    /// <param name="references">
    /// The elements and script-held objects the options name by index, sent after the twin's reference; or
    /// <see langword="null"/>, or an empty instance, to send none.
    /// </param>
    /// <returns>The handle for the token.</returns>
    /// <exception cref="ObjectDisposedException">The twin has been disposed.</exception>
    public ValueTask<TwinHandle<TOptions>> AttachTokenAsync(string token, TOptions options, TwinReferences? references = null)
    {
        return AttachAsync<string?>(token, null, elementCaptureId: null, options, references);
    }

    /// <summary>
    /// Answers the probe with which JavaScript detects, per handle and at run time, whether the runtime supports a
    /// synchronous call from JavaScript into .NET. Only WebAssembly ever completes this call; Blazor Server rejects
    /// synchronous calls before they reach .NET.
    /// </summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [JSInvokable]
    public bool Ping() => true;

    /// <summary>
    /// Resolves a decision point. JavaScript calls this synchronously on WebAssembly, inside the browser's event
    /// dispatch, with the snapshot of the event; the C# handler chain runs and its verdict is returned as a literal.
    /// </summary>
    /// <param name="point">The name of the decision point, for example <c>activate</c> or <c>escape</c>.</param>
    /// <param name="eventJson">The JSON of the <see cref="EventSnapshot"/> JavaScript took inside the dispatch.</param>
    /// <returns>The verdict as <c>run</c>, <c>stop</c>, <c>start</c>, <c>prevent-and-stop</c> or <c>prevent</c>.</returns>
    [JSInvokable]
    public string Decide(string point, string eventJson)
    {
        EventSnapshot snapshot = Deserialize(eventJson, BlazixJsonContext.Default.EventSnapshot);
        return OnDecide(point, snapshot).ToLiteral();
    }

    /// <summary>Runs the C# handler chain of one decision point and returns the verdict.</summary>
    /// <param name="point">The name of the decision point.</param>
    /// <param name="snapshot">The snapshot of the event being dispatched.</param>
    /// <returns>What the module should do with the dispatch.</returns>
    protected abstract DecisionPolicy OnDecide(string point, EventSnapshot snapshot);

    /// <summary>Parses a callback payload through its source-generated type information.</summary>
    /// <typeparam name="TPayload">The payload record.</typeparam>
    /// <param name="json">The JSON the module sent.</param>
    /// <param name="typeInfo">The entry of <typeparamref name="TPayload"/> in <see cref="BlazixJsonContext"/>.</param>
    /// <returns>The parsed payload.</returns>
    /// <exception cref="JsonException">The JSON is not valid or deserialises to <see langword="null"/>.</exception>
    protected static TPayload Deserialize<TPayload>(string json, JsonTypeInfo<TPayload> typeInfo)
    {
        return JsonSerializer.Deserialize(json, typeInfo) ?? throw new JsonException($"The payload '{typeof(TPayload).Name}' deserialised to null.");
    }

    // Generic over the target so an ElementReference reaches the interop argument array unboxed until that call.
    private async ValueTask<TwinHandle<TOptions>> AttachAsync<TTarget>(string key, TTarget elementOrId, string? elementCaptureId, TOptions options, TwinReferences? references)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (handles.TryGetValue(key, out TwinHandle<TOptions>? existing))
        {
            return existing;
        }

        Task<IJSObjectReference?> reference = AttachCoreAsync(elementOrId, options, references?.ToArgument(), TwinReferences.CaptureIds(elementCaptureId, references));
        TwinHandle<TOptions> handle = new(key, reference, optionsTypeInfo, released => handles.Remove(released.Key));
        handles[key] = handle;

        try
        {
            await reference.ConfigureAwait(false);
        }
        catch
        {
            handles.Remove(key);
            throw;
        }

        return handle;
    }

    // A JSDisconnectedException means the Server circuit is gone and the part is being torn down: the attach yields
    // a handle that is already a no-op rather than an exception in OnAfterRenderAsync (null = disconnected). ADR 04
    // Amendments 2026-10-29 (gap 17 of M4 track 5): so does an attach whose twin was disposed while it was in flight,
    // since the part that left took its element with it ("no element for the reference").
    private async Task<IJSObjectReference?> AttachCoreAsync<TTarget>(TTarget elementOrId, TOptions options, object[]? references, string?[]? captures)
    {
        IJSObjectReference? imported = await ImportAsync().ConfigureAwait(false);
        if (imported is null || disposed)
        {
            return null;
        }

        selfReference ??= DotNetObjectReference.Create((TTwin)this);

        try
        {
            // ADR 04 Amendments 2026-10-27: the references follow the twin's reference only when there are some. Amendments
            // 2026-10-28 (design point 9 of M4 track 8): the capture ids follow them whenever an element crosses, the
            // references argument then being null when there are none. An id or a token whose references hold no element sends
            // three arguments, or four with script-held references.
            string json = JsonSerializer.Serialize(options, optionsTypeInfo);
            if (captures is not null)
            {
                return await imported.InvokeAsync<IJSObjectReference>("attach", elementOrId, json, selfReference, references, captures).ConfigureAwait(false);
            }

            return references is null
                ? await imported.InvokeAsync<IJSObjectReference>("attach", elementOrId, json, selfReference).ConfigureAwait(false)
                : await imported.InvokeAsync<IJSObjectReference>("attach", elementOrId, json, selfReference, references).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (JSException) when (disposed)
        {
            return null;
        }
    }

    private Task<IJSObjectReference?> ImportAsync()
    {
        return module ??= ImportCoreAsync();
    }

    private async Task<IJSObjectReference?> ImportCoreAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }
}
