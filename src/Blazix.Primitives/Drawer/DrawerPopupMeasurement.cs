using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// M6 track 8b slice 3 (issue 279), added 2026-11-21. The Popup's own `drawer` handle (role `popup`): the module measures the
// Popup and reports the height, which the Root records as its popup height (DRW-STA-10), and writes the Popup's
// `--drawer-height` (DOM-09) and nested `--drawer-swipe-progress` (DOM-14). Base UI's Popup measures itself whether or not a
// Viewport renders it, so the handle is the Popup's, not the Viewport's. The Popup also reports to its parent drawer: its
// Root's frontmost height while open, 0 once closed or gone (STA-11), and its presence, open or animating out (STA-12),
// after each render (Base UI's layout effects, DrawerPopup.tsx:271-294).
/// <summary>The Popup's drawer handle and its reports to the enclosing drawer.</summary>
internal sealed class DrawerPopupMeasurement : IAsyncDisposable
{
    private readonly DrawerTwin twin;
    private readonly IDialogRootContext root;
    private readonly DrawerRootContext drawer;
    private readonly IDisposable rootSubscription;
    private readonly IDisposable drawerSubscription;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private TwinHandle<DrawerOptions>? handle;
    private Task<TwinHandle<DrawerOptions>>? attaching;
    private DrawerOptions? sent;
    private ElementReference? element;
    private double reportedFrontmost;
    private bool reportedPresence;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="DrawerPopupMeasurement"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    /// <param name="root">The drawer's dialog store.</param>
    /// <param name="drawer">The drawer's own state.</param>
    /// <param name="invokeAsync">The part's dispatcher.</param>
    public DrawerPopupMeasurement(IJSRuntime jsRuntime, IDialogRootContext root, DrawerRootContext drawer, Func<Func<Task>, Task> invokeAsync)
    {
        this.root = root;
        this.drawer = drawer;
        this.invokeAsync = invokeAsync;
        twin = new DrawerTwin(jsRuntime);
        twin.PopupHeightChanged += height => _ = invokeAsync(() =>
        {
            drawer.ReportPopupHeight(height);
            return Task.CompletedTask;
        });
        rootSubscription = root.Subscribe(DialogFields.Open | DialogFields.Mounted | DialogFields.TransitionStatus | DialogFields.NestedOpenDrawerCount, SyncLater);
        drawerSubscription = drawer.Subscribe(DrawerFields.HasNestedDrawer | DrawerFields.FrontmostHeight, SyncLater);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        rootSubscription.Dispose();
        drawerSubscription.Dispose();
        if (reportedFrontmost != 0)
        {
            drawer.Parent?.ReportNestedFrontmostHeight(0);
        }

        if (reportedPresence)
        {
            drawer.Parent?.ReportNestedPresence(false);
        }

        await twin.DisposeAsync();
    }

    /// <summary>Attaches the handle to the rendered Popup, or sends what changed, and makes the reports to the parent.</summary>
    /// <param name="rendered">The Popup's element.</param>
    /// <returns>A task that completes when the handle has the options.</returns>
    public async Task SyncAsync(ElementReference rendered)
    {
        element = rendered;
        await SyncAsync();
    }

    private void SyncLater() => _ = invokeAsync(SyncAsync);

    private async Task SyncAsync()
    {
        if (disposed)
        {
            return;
        }

        ReportToParent();
        if (element is not { } target)
        {
            return;
        }

        bool ending = root.TransitionStatus == TransitionStatus.Ending;
        if (sent is { } last && last.Open == root.Open && last.Mounted == root.Mounted && last.HasNestedDrawer == drawer.HasNestedDrawer
            && last.Ending == ending && last.FrontmostHeight == drawer.FrontmostHeight && last.NestedOpenDrawerCount == root.NestedOpenDrawerCount)
        {
            // ADR 16: an unchanged render sends nothing and allocates nothing here.
            return;
        }

        DrawerOptions options = new()
        {
            Role = DrawerTwin.PopupRole,
            Open = root.Open,
            Mounted = root.Mounted,
            HasNestedDrawer = drawer.HasNestedDrawer,
            Ending = ending,
            FrontmostHeight = drawer.FrontmostHeight,
            NestedOpenDrawerCount = root.NestedOpenDrawerCount,
        };
        sent = options;
        if (handle is null)
        {
            if (attaching is not null)
            {
                return;
            }

            attaching = twin.AttachAsync(target, options).AsTask();
            handle = await attaching;

            // A change made while the attach was in flight.
            if (!ReferenceEquals(sent, options))
            {
                await handle.UpdateAsync(sent);
            }

            return;
        }

        await handle.UpdateAsync(options);
    }

    private void ReportToParent()
    {
        if (drawer.Parent is not { } parent)
        {
            return;
        }

        double frontmost = root.Open ? drawer.FrontmostHeight : 0;
        if (frontmost != reportedFrontmost)
        {
            reportedFrontmost = frontmost;
            parent.ReportNestedFrontmostHeight(frontmost);
        }

        bool present = root.Open || root.TransitionStatus == TransitionStatus.Ending;
        if (present != reportedPresence)
        {
            reportedPresence = present;
            parent.ReportNestedPresence(present);
        }
    }
}
