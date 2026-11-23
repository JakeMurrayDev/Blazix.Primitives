using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// NUM-API-04, NUM-EVT-05, NUM-EVT-08..10: OnValueCommitted over track 1's ValueCommittedEventArgs<T>(T Value,
// GenericEventDetails Details) (ADR 08 Amendments 2026-11-18, decision 13 (a), the Details type confirmed by track 1). The
// titles of NumberFieldRoot.test.tsx and useNumberFieldStepperButton.test.tsx that need no typed text; the steppers' reports
// are the button twin's (NumberFieldTestContext). The typed-then-blurred titles are 6b's input role.
/// <summary>The number field's commit callback.</summary>
public sealed class NumberFieldCommitTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private readonly List<ValueCommittedEventArgs<double?>> commits = [];
    private bool cancel;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "fires when using increment/decrement buttons")]
    [Trait("Requirement", "NUM-API-04")]
    [Trait("Requirement", "NUM-EVT-05")]
    [Trait("Requirement", "NUM-PTR-01")]
    public async Task Fires_when_using_increment_decrement_buttons()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(0);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        Assert.Single(commits);
        Assert.Equal(1, commits[^1].Value);
        Assert.Equal("1", test.Text);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);
        Assert.Equal(2, commits.Count);
        Assert.Equal(0, commits[^1].Value);
        Assert.Equal("0", test.Text);
    }

    [Fact(DisplayName = "includes the correct reason for increment and decrement button presses")]
    [Trait("Requirement", "NUM-EVT-05")]
    [Trait("Requirement", "NUM-EVT-11")]
    public async Task Includes_the_correct_reason_for_increment_and_decrement_button_presses()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(0);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        Assert.Equal(EventReason.IncrementPress, commits[^1].Details.Reason);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);
        Assert.Equal(EventReason.DecrementPress, commits[^1].Details.Reason);
    }

    [Fact(DisplayName = "commits on release after a hold reaches the boundary")]
    [Trait("Requirement", "NUM-PTR-04")]
    [Trait("Requirement", "NUM-EVT-09")]
    public async Task Commits_on_release_after_a_hold_reaches_the_boundary()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(9, max: 10);
        NumberFieldIncrement increment = cut.FindComponent<NumberFieldIncrement>().Instance;

        await test.PressAsync(cut, increment);
        await test.TickAsync(cut, increment);
        Assert.Empty(commits);

        await test.ReleaseAsync(cut, increment);

        ValueCommittedEventArgs<double?> commit = Assert.Single(commits);
        Assert.Equal(10, commit.Value);
        Assert.Equal(EventReason.IncrementPress, commit.Details.Reason);
        Assert.Equal("pointerup", commit.Details.Event.Type);
    }

    [Fact(DisplayName = "does not commit a stale value when a synced step is canceled after an external change")]
    [Trait("Requirement", "NUM-EVT-10")]
    public async Task Does_not_commit_a_stale_value_when_a_synced_step_is_canceled_after_an_external_change()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(0, controlled: true);
        NumberFieldIncrement increment = cut.FindComponent<NumberFieldIncrement>().Instance;

        await test.ClickAsync(cut, increment);
        Assert.Single(commits);
        Assert.Equal(1, commits[^1].Value);

        cut.Render(p => p.Add(x => x.Value, 10));
        cancel = true;
        await test.ClickAsync(cut, increment);

        Assert.Single(commits);
    }

    [Fact(DisplayName = "does not fire when blurring an untouched empty field")]
    [Trait("Requirement", "NUM-EVT-08")]
    public async Task Does_not_fire_when_blurring_an_untouched_empty_field()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(null);

        await test.FocusAsync(cut);
        await test.BlurAsync(cut, string.Empty);
        Assert.Empty(commits);

        // A pending programmatic change makes the same blur commit the cleared value.
        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        commits.Clear();
        await test.BlurAsync(cut, string.Empty, revision: 2);

        ValueCommittedEventArgs<double?> commit = Assert.Single(commits);
        Assert.Null(commit.Value);
        Assert.Equal(EventReason.InputClear, commit.Details.Reason);
    }

    private IRenderedComponent<NumberFieldRoot> Render(double? value, double? max = null, bool controlled = false)
    {
        RenderFragment parts = b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            b.OpenComponent<NumberFieldIncrement>(1);
            b.CloseComponent();
            b.OpenComponent<NumberFieldDecrement>(2);
            b.CloseComponent();
        };

        return test.Context.Render<NumberFieldRoot>(p =>
        {
            p.Add(x => x.Culture, s_enUs);
            p.Add(x => x.Max, max);
            if (controlled)
            {
                p.Add(x => x.Value, value);
            }
            else
            {
                p.Add(x => x.DefaultValue, value);
            }

            p.Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) =>
            {
                if (cancel)
                {
                    e.Details.Cancel();
                }
            });
            p.Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double?> e) => commits.Add(e));
            p.Add(x => x.ChildContent, parts);
        });
    }
}
