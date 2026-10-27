using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The transitions plumbing at L2 on the popup host (TRN-API-04, TRN-STA-06, TRN-EVT-03, TRN-ANI-18; PSH-STA-44/45): the
/// test popup attaches its <c>transitions</c> handle, and each test reports what the module reports when no animation runs,
/// through the twin's own invokable methods. popupConformanceTests' titles verbatim where the clause is the store's.
/// </summary>
public sealed class PopupTransitionsTests : IDisposable
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/transitions.js";

    private readonly PopupTestContext test = new();
    private readonly BunitJSModuleInterop handle;
    private TestPopupRoot<string>? root;

    public PopupTransitionsTests()
    {
        BunitJSModuleInterop module = test.Context.JSInterop.SetupModule(ModulePath);
        handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Theory(DisplayName = "is not called on mount when closed")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "TRN-EVT-03")]
    public void Is_not_called_on_mount_when_closed(bool keepMounted)
    {
        List<bool> calls = [];
        RenderTree(new() { ["onOpenChangeComplete"] = (Action<bool>)calls.Add, ["keepMounted"] = keepMounted });

        Assert.Empty(calls);
    }

    [Fact(DisplayName = "is called on close when there is no exit animation")]
    [Trait("Requirement", "TRN-API-04")]
    [Trait("Requirement", "PSH-STA-45")]
    public async Task Is_called_on_close_when_there_is_no_exit_animation()
    {
        List<bool> calls = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["open"] = true, ["onOpenChangeComplete"] = (Action<bool>)calls.Add });
        await ReportCompletionAsync(cut, open: true);

        await cut.Instance.SetAsync("open", false);
        await ReportCompletionAsync(cut, open: false);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=\"popup\"]")));
        Assert.Equal([true, false], calls);
        Assert.False(root!.Store.Popup.Mounted);
        Assert.Null(root.Store.Popup.ActiveTriggerId);
        Assert.Null(root.Store.Popup.TransitionStatus);
    }

    [Fact(DisplayName = "port: the status becomes ending in the write that closes, reaches the transitions handle, and mirrors its reports")]
    [Trait("Requirement", "TRN-STA-06")]
    [Trait("Requirement", "PSH-STA-44")]
    public async Task The_status_becomes_ending_in_the_write_that_closes_and_mirrors_the_reports()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["open"] = true });
        await cut.InvokeAsync(() => Popup(cut).Transitions!.OnStatusChange("""{"status":"idle","mounted":true}"""));
        Assert.Equal(TransitionStatus.Idle, root!.Store.Popup.TransitionStatus);

        await cut.Instance.SetAsync("open", false);

        Assert.True(root.Store.Popup.Mounted);
        Assert.Equal(TransitionStatus.Ending, root.Store.Popup.DerivedTransitionStatus);
        cut.WaitForAssertion(() => Assert.Contains(handle.Invocations["update"], invocation => ((string)invocation.Arguments[0]!).Contains("\"status\":\"ending\"", StringComparison.Ordinal)));
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed"));
    }

    [Fact(DisplayName = "port: an exit completion that arrives after a reopen is ignored")]
    [Trait("Requirement", "TRN-ANI-18")]
    public async Task An_exit_completion_that_arrives_after_a_reopen_is_ignored()
    {
        List<bool> calls = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(new() { ["open"] = true, ["onOpenChangeComplete"] = (Action<bool>)calls.Add });
        await ReportCompletionAsync(cut, open: true);

        await cut.Instance.SetAsync("open", false);
        await cut.Instance.SetAsync("open", true);
        await ReportCompletionAsync(cut, open: false);

        Assert.True(root!.Store.Popup.Mounted);
        Assert.NotEmpty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal([true], calls);
    }

    private static TestPopupPopup<string> Popup(IRenderedComponent<PopupTestHost> cut) => cut.FindComponent<TestPopupPopup<string>>().Instance;

    // What the module reports when the side finished with no animation running.
    private static Task ReportCompletionAsync(IRenderedComponent<PopupTestHost> cut, bool open)
    {
        cut.WaitForAssertion(() => Assert.NotNull(Popup(cut).Transitions));
        return cut.InvokeAsync(() => Popup(cut).Transitions!.OnTransitionComplete(open ? """{"open":true}""" : """{"open":false}"""));
    }

    private IRenderedComponent<PopupTestHost> RenderTree(Dictionary<string, object?> values) => test.Render(
        (builder, host) =>
        {
            builder.OpenComponent<TestPopupRoot<string>>(0);
            if (host.Values.TryGetValue("open", out object? open))
            {
                builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.Open), (bool)open!);
            }

            if (host.Get<Action<bool>>("onOpenChangeComplete") is { } complete)
            {
                builder.AddComponentParameter(2, nameof(TestPopupRoot<string>.OnOpenChangeComplete), EventCallback.Factory.Create(this, complete));
            }

            bool keepMounted = host.Get<bool>("keepMounted");
            builder.AddComponentParameter(3, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<TestPopupPopup<string>>(0);
                content.AddComponentParameter(1, "data-testid", "popup");
                content.AddComponentParameter(2, nameof(TestPopupPopup<string>.AttachesTransitions), true);
                content.AddComponentParameter(3, nameof(TestPopupPopup<string>.KeepMounted), keepMounted);
                content.CloseComponent();
            }));
            builder.AddComponentReferenceCapture(4, reference => root = (TestPopupRoot<string>)reference);
            builder.CloseComponent();
        },
        values);
}
