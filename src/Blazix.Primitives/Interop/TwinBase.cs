using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

/// <summary>
/// The twin base (ADR 04 §3): the C# half of one JavaScript module. It imports the module lazily on first use,
/// attaches parts (<c>attach(elementOrId, options) → handle</c>), owns the <see cref="DotNetObjectReference{TValue}"/>
/// the module calls back through, and answers decision points. Parts talk to twins, never to <see cref="IJSRuntime"/>.
/// </summary>
/// <typeparam name="TTwin">The concrete twin (<c>&lt;Module&gt;Twin</c>, ADR 08 §3), whose public <c>[JSInvokable]</c> methods are the module's callbacks.</typeparam>
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

    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly app.</param>
    /// <param name="modulePath">The module URL, for example <c>./_content/Blazix.Primitives/js/button.js</c>.</param>
    /// <param name="optionsTypeInfo">The options record's entry in <see cref="BlazixJsonContext"/>.</param>
    protected TwinBase(IJSRuntime jsRuntime, string modulePath, JsonTypeInfo<TOptions> optionsTypeInfo)
    {
        this.jsRuntime = jsRuntime;
        this.modulePath = modulePath;
        this.optionsTypeInfo = optionsTypeInfo;
    }

    /// <summary>The URL of a library module: <c>./_content/Blazix.Primitives/js/&lt;module&gt;.js</c>.</summary>
    protected static string LibraryModule(string module) => $"./_content/Blazix.Primitives/js/{module}.js";

    /// <summary>
    /// Attaches the module to the part's element. Idempotent per element: a repeated attach returns the existing
    /// handle and sends nothing (ADR 06 Amendments 2026-10-18, rule 2).
    /// </summary>
    public ValueTask<TwinHandle<TOptions>> AttachAsync(ElementReference element, TOptions options)
    {
        return AttachAsync(element.Id, element, options);
    }

    /// <summary>Attaches by the part's generated id when no <see cref="ElementReference"/> exists (substitute callback, ADR 02).</summary>
    public ValueTask<TwinHandle<TOptions>> AttachAsync(string elementId, TOptions options)
    {
        return AttachAsync(elementId, elementId, options);
    }

    /// <summary>Disposes every live handle, the module reference and the .NET object reference. Idempotent.</summary>
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

    /// <summary>
    /// Answers the JavaScript probe that detects, per handle and at run time, whether this runtime supports a
    /// synchronous JS-to-.NET call (<c>js/decide.ts</c>). Only WebAssembly ever reaches this method.
    /// </summary>
    [JSInvokable]
    public bool Ping() => true;

    /// <summary>
    /// A decision point (ADR 04 §3): JavaScript calls this synchronously on WebAssembly with the event snapshot and
    /// receives the verdict as its literal. The C# chain runs inside the browser's dispatch.
    /// </summary>
    [JSInvokable]
    public string Decide(string point, string eventJson)
    {
        EventSnapshot snapshot = Deserialize(eventJson, BlazixJsonContext.Default.EventSnapshot);
        return OnDecide(point, snapshot).ToLiteral();
    }

    /// <summary>Runs the C# chain of one decision point and returns the verdict.</summary>
    protected abstract DecisionPolicy OnDecide(string point, EventSnapshot snapshot);

    /// <summary>Deserialises a callback payload through its source-generated type info; a <see langword="null"/> payload is an error.</summary>
    protected static TPayload Deserialize<TPayload>(string json, JsonTypeInfo<TPayload> typeInfo)
    {
        return JsonSerializer.Deserialize(json, typeInfo) ?? throw new JsonException($"The payload '{typeof(TPayload).Name}' deserialised to null.");
    }

    private async ValueTask<TwinHandle<TOptions>> AttachAsync(string key, object elementOrId, TOptions options)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (handles.TryGetValue(key, out TwinHandle<TOptions>? existing))
        {
            return existing;
        }

        Task<IJSObjectReference?> reference = AttachCoreAsync(elementOrId, options);
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
    // a handle that is already a no-op rather than an exception in OnAfterRenderAsync (null = disconnected).
    private async Task<IJSObjectReference?> AttachCoreAsync(object elementOrId, TOptions options)
    {
        IJSObjectReference? imported = await ImportAsync().ConfigureAwait(false);
        if (imported is null)
        {
            return null;
        }

        selfReference ??= DotNetObjectReference.Create((TTwin)this);

        try
        {
            return await imported.InvokeAsync<IJSObjectReference>("attach", elementOrId, JsonSerializer.Serialize(options, optionsTypeInfo), selfReference).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
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
