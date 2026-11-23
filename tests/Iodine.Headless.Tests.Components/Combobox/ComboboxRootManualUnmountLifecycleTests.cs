using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `manual unmount lifecycle` titles at L2 (batch A), CMB-API-36's @ref actions (CloseAsync, Unmount;
// ADR 08 Amendments 2026-10-27). Base UI's `Popup` is the host with the consumer's open state (ControlOpen): the root is
// controlled by it and a change its callback does not cancel is written back. Base UI's tests disable animations, so a close
// is followed by the popup's exit report (CompleteCloseAsync), which ends a close the consumer did not hold.
/// <summary>Combobox's root at L2: the manual unmount lifecycle.</summary>
public sealed class ComboboxRootManualUnmountLifecycleTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "closes through the `close` action so `onOpenChange` can opt out")]
    [Trait("Requirement", "CMB-API-36")]
    public async Task Closes_through_the_close_action_so_onOpenChange_can_opt_out()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderPopup(open: true);
        cut.Instance.OnOpen = PreventUnmountOnClose;
        ComboboxRoot<string> root = cut.Instance.Root!;

        await cut.InvokeAsync(root.CloseAsync);
        await cut.CompleteCloseAsync();

        Assert.Equal([EventReason.ImperativeAction], cut.Instance.OpenChanges.Select(change => change.Reason));
        Assert.Equal("false", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
        Assert.True(cut.HasListbox());

        await cut.InvokeAsync(root.Unmount);

        Assert.False(cut.HasListbox());
    }

    [Fact(DisplayName = "unmounts when `close` and `unmount` are called in one batch")]
    [Trait("Requirement", "CMB-API-36")]
    [Trait("Requirement", "CMB-ANI-03")]
    public async Task Unmounts_when_close_and_unmount_are_called_in_one_batch()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderPopup(open: true);
        cut.Instance.OnOpen = PreventUnmountOnClose;
        ComboboxRoot<string> root = cut.Instance.Root!;

        await cut.InvokeAsync(async () =>
        {
            await root.CloseAsync();
            root.Unmount();
        });

        Assert.False(cut.HasListbox());
        Assert.Single(cut.Instance.Completes, opened => !opened);
    }

    [Fact(DisplayName = "still unmounts on a later close after `unmount` and a reopen in one batch")]
    [Trait("Requirement", "CMB-API-36")]
    public async Task Still_unmounts_on_a_later_close_after_unmount_and_a_reopen_in_one_batch()
    {
        bool reopenOnComplete = true;
        bool optOut = true;
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderPopup(open: true);
        cut.Instance.OnOpen = args =>
        {
            if (!args.Value && optOut)
            {
                ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
            }
        };

        // An exit-animation callback that reopens right after it unmounts.
        cut.Instance.OnComplete = opened =>
        {
            if (!opened && reopenOnComplete)
            {
                reopenOnComplete = false;
                cut.Instance.SetOpenState(true);
            }
        };
        ComboboxRoot<string> root = cut.Instance.Root!;

        await cut.InvokeAsync(root.CloseAsync);
        await cut.CompleteCloseAsync();
        Assert.True(cut.HasListbox());

        await cut.InvokeAsync(root.Unmount);
        Assert.True(cut.HasListbox());
        Assert.Single(cut.Instance.Completes, opened => !opened);

        optOut = false;
        await cut.ClickOptionAsync("Apple");
        await cut.CompleteCloseAsync();

        Assert.False(cut.HasListbox());
        Assert.Equal(2, cut.Instance.Completes.Count(opened => !opened));
    }

    [Fact(DisplayName = "does not call `onOpenChange` when the `close` action is called while closed")]
    [Trait("Requirement", "CMB-API-07")]
    public async Task Does_not_call_onOpenChange_when_the_close_action_is_called_while_closed()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderPopup(open: false);

        await cut.InvokeAsync(cut.Instance.Root!.CloseAsync);

        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Equal("false", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
    }

    private static void PreventUnmountOnClose(ValueChangeEventArgs<bool> args)
    {
        if (!args.Value)
        {
            ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
        }
    }

    // ComboboxRoot.test.tsx:309-339: a Root over the consumer's open state, an Input and one item, apple, labelled Apple.
    private IRenderedComponent<ComboboxRootBatchAHost> RenderPopup(bool open) =>
        test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.ControlOpen, true)
            .Add(x => x.InitialOpen, open)
            .Add(x => x.StaticItems, (IReadOnlyList<string>)["apple"])
            .Add(x => x.Labels, _ => "Apple"));
}
