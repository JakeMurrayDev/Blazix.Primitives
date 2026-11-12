using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): AutocompleteRoot.test.tsx's manual unmount lifecycle titles at L2 (AUT-ANI-01, EVT-03, API-05's
// actions), the five that Combobox ports at L2 (ComboboxRootManualUnmountLifecycleTests, ComboboxContextTitlesTests); the
// other seven run at L4 (autocomplete.spec.ts). Base UI's tests disable animations, so a close is followed by the popup's exit
// report (the store's CompleteTransitionAsync), which ends a close the consumer did not hold.
/// <summary>Autocomplete's manual unmount lifecycle at L2.</summary>
public sealed class AutocompleteLifecycleTests : IDisposable
{
    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "ignores `unmount` while the popup is open")]
    [Trait("Requirement", "AUT-ANI-01")]
    public async Task Ignores_unmount_while_the_popup_is_open()
    {
        IRenderedComponent<AutocompleteLifecycleHost> cut = Render(open: true);
        string? listId = cut.Find("[data-testid=list]").Id;

        await cut.InvokeAsync(cut.Instance.Root!.Unmount);

        Assert.Equal(listId, cut.Find("[data-testid=list]").Id);
        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "unmounts when `close` and `unmount` are called in one batch")]
    [Trait("Requirement", "AUT-ANI-01")]
    [Trait("Requirement", "AUT-EVT-03")]
    public async Task Unmounts_when_close_and_unmount_are_called_in_one_batch()
    {
        IRenderedComponent<AutocompleteLifecycleHost> cut = Render(open: true);
        cut.Instance.OnOpen = PreventUnmountOnClose;
        AutocompleteRoot<string> root = cut.Instance.Root!;

        await cut.InvokeAsync(async () =>
        {
            await root.CloseAsync();
            root.Unmount();
        });

        Assert.Empty(cut.FindAll("[role=listbox]"));
        Assert.Single(cut.Instance.Completes, opened => !opened);
    }

    [Fact(DisplayName = "still unmounts on a later close after `unmount` was called while open")]
    [Trait("Requirement", "AUT-ANI-01")]
    public async Task Still_unmounts_on_a_later_close_after_unmount_was_called_while_open()
    {
        IRenderedComponent<AutocompleteLifecycleHost> cut = Render(open: true);
        AutocompleteRoot<string> root = cut.Instance.Root!;

        await cut.InvokeAsync(root.Unmount);
        Assert.NotEmpty(cut.FindAll("[data-testid=list]"));
        await cut.InvokeAsync(root.CloseAsync);
        await CompleteCloseAsync(cut);

        Assert.Empty(cut.FindAll("[data-testid=list]"));
    }

    [Fact(DisplayName = "still unmounts on a later close after `unmount` and a reopen in one batch")]
    [Trait("Requirement", "AUT-ANI-01")]
    [Trait("Requirement", "AUT-EVT-03")]
    public async Task Still_unmounts_on_a_later_close_after_unmount_and_a_reopen_in_one_batch()
    {
        bool reopenOnComplete = true;
        bool optOut = true;
        IRenderedComponent<AutocompleteLifecycleHost> cut = Render(open: true);
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
        AutocompleteRoot<string> root = cut.Instance.Root!;

        await cut.InvokeAsync(root.CloseAsync);
        await CompleteCloseAsync(cut);
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));

        await cut.InvokeAsync(root.Unmount);
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.Single(cut.Instance.Completes, opened => !opened);

        optOut = false;
        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());
        await CompleteCloseAsync(cut);

        Assert.Empty(cut.FindAll("[role=listbox]"));
        Assert.Equal(2, cut.Instance.Completes.Count(opened => !opened));
    }

    [Fact(DisplayName = "does not call `onOpenChange` when the `close` action is called while closed")]
    [Trait("Requirement", "AUT-API-05")]
    [Trait("Requirement", "AUT-EVT-03")]
    public async Task Does_not_call_on_open_change_when_the_close_action_is_called_while_closed()
    {
        IRenderedComponent<AutocompleteLifecycleHost> cut = Render(open: false);

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

    private static Task CompleteCloseAsync(IRenderedComponent<AutocompleteLifecycleHost> cut) =>
        cut.InvokeAsync(() => cut.Instance.Root!.Store.CompleteTransitionAsync(false));

    private IRenderedComponent<AutocompleteLifecycleHost> Render(bool open) =>
        test.Context.Render<AutocompleteLifecycleHost>(p => p.Add(x => x.InitialOpen, open));
}
