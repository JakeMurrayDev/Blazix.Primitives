using Blazix.Primitives.Interop;

namespace Blazix.Primitives;

// A part's after-render sync can run again while its first attach still awaits the module: the twin's attach is idempotent
// per element and hands back the in-flight handle without the newer options, so a sync that recorded them as sent lost
// them for good (found by the L4 port of the Chips, 2026-11-11). This keeps the attach once, lets every sync wait for it,
// and then sends the newest options when they differ from what the module has.
/// <summary>Attaches a part's handle once and keeps the module's options current, across an attach in flight.</summary>
/// <typeparam name="TTwin">The part's twin.</typeparam>
/// <typeparam name="TOptions">The handle's options record.</typeparam>
internal sealed class ComboboxHandleSync<TTwin, TOptions>
    where TTwin : TwinBase<TTwin, TOptions>
    where TOptions : TwinOptions
{
    private Task<TwinHandle<TOptions>>? attaching;

    private TOptions? sent;

    private TOptions? latest;

    /// <summary>Gets the handle once the attach completed, or <see langword="null"/>.</summary>
    public TwinHandle<TOptions>? Handle { get; private set; }

    /// <summary>Gets a task that completes when the handle is attached.</summary>
    public Task Attached => attaching ?? Task.CompletedTask;

    /// <summary>Forgets the handle, after the part released it, so the next sync attaches again.</summary>
    public void Reset()
    {
        Handle = null;
        attaching = null;
        sent = null;
        latest = null;
    }

    /// <summary>Attaches the handle to the element with the options, or sends them when they differ from the module's.</summary>
    /// <param name="twin">The part's twin.</param>
    /// <param name="target">The part's element.</param>
    /// <param name="options">The part's current options.</param>
    /// <param name="references">The elements the options name, sent with an update; or <see langword="null"/>.</param>
    /// <returns>A task that completes when the module has the newest options.</returns>
    public Task SyncAsync(TTwin twin, Microsoft.AspNetCore.Components.ElementReference target, TOptions options, TwinReferences? references = null)
    {
        if (Handle is null && attaching is null)
        {
            sent = options;
            attaching = twin.AttachAsync(target, options).AsTask();
        }

        return SyncCoreAsync(options, references);
    }

    /// <summary>Attaches the handle to the element with the id, or sends the options when they differ from the module's.</summary>
    /// <param name="twin">The part's twin.</param>
    /// <param name="elementId">The id of the part's element, under a substitute callback.</param>
    /// <param name="options">The part's current options.</param>
    /// <returns>A task that completes when the module has the newest options.</returns>
    public Task SyncAsync(TTwin twin, string elementId, TOptions options)
    {
        if (Handle is null && attaching is null)
        {
            sent = options;
            attaching = twin.AttachAsync(elementId, options).AsTask();
        }

        return SyncCoreAsync(options, null);
    }

    private async Task SyncCoreAsync(TOptions options, TwinReferences? references)
    {
        latest = options;
        if (Handle is null)
        {
            try
            {
                Handle = await attaching!;
            }
            catch
            {
                attaching = null;
                throw;
            }
        }

        if (latest is { } next && !EqualityComparer<TOptions>.Default.Equals(next, sent))
        {
            sent = next;
            await Handle.UpdateAsync(next, references);
        }
    }
}
