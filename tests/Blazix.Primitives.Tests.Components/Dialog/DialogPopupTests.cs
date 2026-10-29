using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// The stalling runtime below is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DialogPopup.test.tsx</c>: the missing root (DLG-EDGE-02), <c>keepMounted</c> (DLG-API-17,
/// DOM-18, ANI-01), the nested count (DLG-STA-04/05, DOM-19) and the style hooks (DLG-STR-12, STA-03, STA-13, DOM-21/22).
/// Titles are Base UI's. Base UI's nested-count titles read <c>getComputedStyle</c> in a browser; the inline declaration C#
/// writes in the commit path is the same value, observed here in the markup of the batch that changed the count. The
/// nested dialogs open and close through their controlled <c>Open</c> (the count's path is the commit, whatever the
/// request's origin); the AlertDialog titles are track 9's.
/// </summary>
public sealed class DialogPopupTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Dialog.Root>")]
    [Trait("Requirement", "DLG-EDGE-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<DialogPopup>());
        Assert.Equal("Blazix: The `DialogRoot` context is missing. Dialog parts must be placed within `<DialogRoot>`.", error.Message);
    }

    [Fact(DisplayName = "should keep the dialog mounted when keepMounted=true")]
    [Trait("Requirement", "DLG-API-17")]
    [Trait("Requirement", "DLG-DOM-18")]
    [Trait("Requirement", "DLG-ANI-01")]
    public void Should_keep_the_dialog_mounted_when_keepMounted_is_true()
    {
        IRenderedComponent<DialogHarness> cut = Closed(keepMounted: true);

        IElement popup = cut.Find("[role=dialog]");
        Assert.True(popup.HasAttribute("hidden"));
    }

    [Theory(DisplayName = "should not keep the dialog mounted when keepMounted=")]
    [InlineData("false")]
    [InlineData("undefined")]
    [Trait("Requirement", "DLG-API-17")]
    [Trait("Requirement", "DLG-ANI-01")]
    public void Should_not_keep_the_dialog_mounted_when_keepMounted_is(string name)
    {
        IRenderedComponent<DialogHarness> cut = Closed(keepMounted: name == "false" ? false : null);

        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "provides the number of open nested dialogs as a CSS variable")]
    [Trait("Requirement", "DLG-STA-04")]
    [Trait("Requirement", "DLG-DOM-19")]
    public void Provides_the_number_of_open_nested_dialogs_as_a_CSS_variable()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p
            .Add(x => x.Levels, 3)
            .Add(x => x.Level2Open, false)
            .Add(x => x.Level3Open, false));
        Assert.Equal("0", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level2Open, true));
        Assert.Equal("1", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level3Open, true));
        Assert.Equal("2", NestedDialogs(cut, 1));
        Assert.Equal("1", NestedDialogs(cut, 2));

        cut.Render(p => p.Add(x => x.Level3Open, false));
        Assert.Equal("1", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level2Open, false));
        Assert.Equal("0", NestedDialogs(cut, 1));
    }

    [Fact(DisplayName = "decrements the count when an open nested dialog is unmounted")]
    [Trait("Requirement", "DLG-STA-05")]
    [Trait("Requirement", "DLG-DOM-19")]
    public void Decrements_the_count_when_an_open_nested_dialog_is_unmounted()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 2));
        Assert.Equal("1", NestedDialogs(cut, 1));

        cut.Render(p => p.Add(x => x.Level2Mounted, false));

        Assert.Equal("0", NestedDialogs(cut, 1));
        Assert.False(cut.Find("[data-testid=level-1-popup]").HasAttribute("data-nested-dialog-open"));
    }

    [Fact(DisplayName = "does not change the count when a closed nested dialog is unmounted")]
    [Trait("Requirement", "DLG-STA-05")]
    public void Does_not_change_the_count_when_a_closed_nested_dialog_is_unmounted()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 2).Add(x => x.Level2Open, false));
        Assert.Equal("0", NestedDialogs(cut, 1));
        Assert.Single(cut.FindAll("[data-testid=level-2-trigger]"));

        cut.Render(p => p.Add(x => x.Level2Mounted, false));

        Assert.Empty(cut.FindAll("[data-testid=level-2-trigger]"));
        Assert.Equal("0", NestedDialogs(cut, 1));
    }

    [Fact(DisplayName = "adds the `nested` and `nested-dialog-open` style hooks if a dialog has a parent dialog")]
    [Trait("Requirement", "DLG-STR-12")]
    [Trait("Requirement", "DLG-STA-03")]
    [Trait("Requirement", "DLG-STA-13")]
    [Trait("Requirement", "DLG-DOM-21")]
    [Trait("Requirement", "DLG-DOM-22")]
    public void Adds_the_nested_and_nested_dialog_open_style_hooks_if_a_dialog_has_a_parent_dialog()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 3).Add(x => x.Level3Open, false));

        IElement parent = cut.Find("[data-testid=level-1-popup]");
        IElement nested = cut.Find("[data-testid=level-2-popup]");
        Assert.False(parent.HasAttribute("data-nested"));
        Assert.True(nested.HasAttribute("data-nested"));
        Assert.True(parent.HasAttribute("data-nested-dialog-open"));
        Assert.False(nested.HasAttribute("data-nested-dialog-open"));
    }

    private static string NestedDialogs(IRenderedComponent<DialogNestedHarness> cut, int level)
    {
        string style = cut.Find($"[data-testid=level-{level}-popup]").GetAttribute("style") ?? string.Empty;
        const string prefix = "--nested-dialogs: ";
        int start = style.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no --nested-dialogs in \"{style}\"");
        int end = style.IndexOf(';', start);
        return (end < 0 ? style[(start + prefix.Length)..] : style[(start + prefix.Length)..end]).Trim();
    }

    private IRenderedComponent<DialogHarness> Closed(bool? keepMounted) => test.Context.Render<DialogHarness>(p =>
    {
        p.Add(x => x.ControlledOpen, false).Add(x => x.Modal, false).Add(x => x.WithTrigger, false);
        if (keepMounted is { } value)
        {
            p.Add(x => x.KeepMounted, value);
        }
    });

    // DLG-KEY-09 (dialog/popup/DialogPopup.tsx:82-86): the Popup stops the composite keys unconditionally, through the
    // `popup` module's Popup role (PopupElementOptions.StopCompositeKeys, M4 track 5's name), so a dialog opened from a
    // toolbar item never moves the toolbar's focus.
    [Fact(DisplayName = "port: the popup attaches the popup module's Popup role and stops the composite keys")]
    [Trait("Requirement", "DLG-KEY-09")]
    public void The_popup_attaches_the_popup_modules_popup_role_and_stops_the_composite_keys()
    {
        test.Context.Render<DialogHarness>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.Modal, false));

        string options = Assert.Single(DialogTestContext.AttachOptions(test.PopupModule))!;
        Assert.Contains("\"popup\":{\"stopCompositeKeys\":true}", options, StringComparison.Ordinal);
    }

    // Issue 134's class on Server (run 40782584447): the popup's handles attach after its render (the Popup role and the
    // floating handle after the portal's move), each behind its module's import, so a close that unmounts the popup can
    // remove its element before an attach answers, and the module then finds no element for the reference. The part has
    // left, so the error is dropped, as a twin drops an attach its disposal overtook. The focus manager's dispose is held,
    // as a Server round trip holds it.
    [Fact(DisplayName = "port: a popup that unmounts before the popup module's attach answers drops the missing-element error")]
    [Trait("Requirement", "DLG-KEY-09")]
    public Task A_popup_that_unmounts_before_the_popup_modules_attach_answers_drops_the_missing_element_error() =>
        AssertUnmountDropsTheStalledAttachAsync("popup.js", optionsFragment: null, modal: false);

    [Fact(DisplayName = "port: a popup that unmounts before its floating handle's attach answers drops the missing-element error")]
    [Trait("Requirement", "DLG-ANI-07")]
    public Task A_popup_that_unmounts_before_its_floating_handles_attach_answers_drops_the_missing_element_error() =>
        AssertUnmountDropsTheStalledAttachAsync("interactions.js", "\"floating\":", modal: false);

    [Fact(DisplayName = "port: a modal popup that unmounts before its scroll lock's attach answers drops the missing-element error")]
    [Trait("Requirement", "DLG-ANI-07")]
    public Task A_modal_popup_that_unmounts_before_its_scroll_locks_attach_answers_drops_the_missing_element_error() =>
        AssertUnmountDropsTheStalledAttachAsync("scroll-lock.js", optionsFragment: null, modal: true);

    // Issue 149's residual (its comment of 2026-10-29): DialogTransitions created its twin after awaiting the portal's move
    // without a disposed check, so a popup, backdrop or viewport disposed while the move was pending leaked a twin.
    [Fact(DisplayName = "port: a dialog disposed while its portal's move is pending creates no transitions twin")]
    [Trait("Requirement", "DLG-ANI-07")]
    public async Task A_dialog_disposed_while_its_portals_move_is_pending_creates_no_transitions_twin()
    {
        HeldAttachRuntime runtime = new(test.Context.JSInterop.JSRuntime, "portal.js");
        test.Context.Services.AddSingleton<IJSRuntime>(runtime);
        test.Context.Render<DialogHarness>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.Modal, false));
        int before = runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal));

        await test.Context.DisposeComponentsAsync();
        runtime.Release();
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(before, runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal)));
    }

    private async Task AssertUnmountDropsTheStalledAttachAsync(string module, string? optionsFragment, bool modal)
    {
        StalledAttachRuntime runtime = new(test.Context.JSInterop.JSRuntime, module, optionsFragment);
        test.Context.Services.AddSingleton<IJSRuntime>(runtime);
        JSRuntimeInvocationHandler focusDispose = test.FocusManagerHandle.SetupVoid("dispose", _ => true);
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p.Add(x => x.ControlledOpen, true).Add(x => x.Modal, modal));
        Assert.Equal(1, runtime.Attaches);

        cut.Render(p => p.Add(x => x.ControlledOpen, false));
        await test.CompletePopupTransitionAsync(cut, open: false);
        Assert.Empty(cut.FindAll("[role=dialog]"));
        runtime.FailAttach(new JSException("Blazix: no element for the reference"));

        Task<Exception> unhandled = test.Context.Renderer.UnhandledException;
        if (await Task.WhenAny(unhandled, Task.Delay(TimeSpan.FromMilliseconds(500), Xunit.TestContext.Current.CancellationToken)) == unhandled)
        {
            Exception caught = await unhandled;
            Assert.Fail($"The renderer caught {caught.GetType().Name}: {caught.Message}");
        }

        focusDispose.SetVoidResult();
    }

    // One module's attach (those whose options hold the fragment, when given) waits until the test fails it; every other
    // call goes to bUnit's runtime and its module.
    private sealed class StalledAttachRuntime(IJSRuntime inner, string module, string? optionsFragment) : IJSRuntime
    {
        private readonly TaskCompletionSource<IJSObjectReference> attach = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Attaches { get; private set; }

        public void FailAttach(Exception exception) => attach.SetException(exception);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            ValueTask<TValue> invocation = inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
            return identifier == "import" && args is [string path] && path.EndsWith("/" + module, StringComparison.Ordinal)
                ? new ValueTask<TValue>(WrapAsync(invocation))
                : invocation;
        }

        private async Task<TValue> WrapAsync<TValue>(ValueTask<TValue> import) =>
            (TValue)(object)new StalledModule(this, (IJSObjectReference)(await import)!);

        private ValueTask<TValue>? Stall<TValue>(string identifier, object?[]? args)
        {
            if (identifier != "attach" || (optionsFragment is not null && !(args is [_, string options, ..] && options.Contains(optionsFragment, StringComparison.Ordinal))))
            {
                return null;
            }

            Attaches++;
            return new ValueTask<TValue>(attach.Task.ContinueWith(task => (TValue)task.GetAwaiter().GetResult(), TaskScheduler.Default));
        }

        private sealed class StalledModule(StalledAttachRuntime runtime, IJSObjectReference inner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
                runtime.Stall<TValue>(identifier, args) ?? inner.InvokeAsync<TValue>(identifier, cancellationToken, args);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}

#pragma warning restore BL0016
