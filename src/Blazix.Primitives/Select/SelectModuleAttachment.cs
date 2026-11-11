using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The PopupModuleAttachment shape for the `select` module (ADR 04 §1's `select` row): one SelectTwin per part, attached
// after the render that created the part's element and after the portal's move; an unchanged options record sends nothing
// (ADR 16 rule 7); a render during the pending first attach awaits it; a part that leaves while its attach is in flight
// gets TwinBase's no-op handle (issue 132).
/// <summary>Attaches a select part's <c>select</c> handle and sends its options when they changed.</summary>
internal sealed class SelectModuleAttachment : IAsyncDisposable
{
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<SelectOptions>? handle;
    private SelectOptions? sent;
    private string?[] sentReferences = [];
    private Task? attaching;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SelectModuleAttachment"/> class.</summary>
    /// <param name="jsRuntime">The JavaScript runtime.</param>
    public SelectModuleAttachment(IJSRuntime jsRuntime)
    {
        Twin = new SelectTwin(jsRuntime);
    }

    /// <summary>Gets the part's twin, whose reports the part subscribes to.</summary>
    public SelectTwin Twin { get; }

    /// <summary>Gets a task that completes when the handle has attached the first time.</summary>
    public Task Attached => attached.Task;

    /// <summary>Gets the number of option records sent after the attach.</summary>
    public int Updates { get; private set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await Twin.DisposeAsync();
    }

    /// <summary>Attaches the handle to <paramref name="element"/>, or sends <paramref name="options"/> when they or the elements changed.</summary>
    /// <param name="element">The part's element.</param>
    /// <param name="options">The options of this render, without element indices.</param>
    /// <param name="trigger">The trigger's element, or <see langword="null"/>.</param>
    /// <param name="popup">The popup's element, or <see langword="null"/>.</param>
    /// <param name="list">The list's element, or <see langword="null"/>.</param>
    /// <param name="value">The value part's element, or <see langword="null"/>.</param>
    /// <param name="positioner">The positioner's element, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the attach or the update was sent.</returns>
    public async Task SyncAsync(ElementReference element, SelectOptions options, ElementReference? trigger = null, ElementReference? popup = null, ElementReference? list = null, ElementReference? value = null, ElementReference? positioner = null)
    {
        if (disposed)
        {
            return;
        }

        TwinReferences? references = null;
        SelectOptions indexed = options with
        {
            Trigger = Add(ref references, trigger),
            Popup = Add(ref references, popup),
            List = Add(ref references, list),
            Value = Add(ref references, value),
            Positioner = Add(ref references, positioner),
        };
        string?[] ids = [trigger?.Id, popup?.Id, list?.Id, value?.Id, positioner?.Id];
        if (handle is null)
        {
            attaching ??= AttachAsync(element, indexed, references, ids);
            await attaching;
            return;
        }

        if (indexed != sent || !Same(ids))
        {
            sent = indexed;
            sentReferences = ids;
            Updates++;
            await handle.UpdateAsync(indexed, references);
        }
    }

    private static int? Add(ref TwinReferences? references, ElementReference? element) =>
        element is { } reference ? (references ??= new TwinReferences()).Add(reference) : null;

    private bool Same(string?[] ids)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            if (i >= sentReferences.Length || !string.Equals(ids[i], sentReferences[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private async Task AttachAsync(ElementReference element, SelectOptions options, TwinReferences? references, string?[] ids)
    {
        sent = options;
        sentReferences = ids;
        handle = await Twin.AttachAsync(element, options, references);
        attaching = null;
        attached.TrySetResult();
    }
}
