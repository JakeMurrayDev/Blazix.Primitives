using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// The radio indicator's rows C# observes (issue 83): when the element is rendered, what C# renders at its creation (the
// handover of data-starting-style, ADR 04 Amendments 2026-10-18), the twin's options on attach and update, and the mirror of
// the module's status and completion reports, each one state change. The frames are the transitions module's
// (tests/browser/radio.spec.ts, RadioIndicator.test.tsx's titles).
/// <summary>The radio indicator: its presence, its state and its transitions twin.</summary>
public sealed class RadioIndicatorTests : IDisposable
{
    private const string IndicatorSelector = "[data-testid=indicator]";

    private static readonly string s_checkedAttach =
        """{"open":true,"mounted":true,"enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":false,"openSelector":"[data-checked]"}""";

    private readonly RadioTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: an indicator renders only while its radio is checked, without a starting phase at mount")]
    [Trait("Requirement", "RAD-ANI-01")]
    [Trait("Requirement", "RAD-ANI-02")]
    [Trait("Requirement", "TRN-STA-13")]
    public void Port_an_indicator_renders_only_while_its_radio_is_checked()
    {
        IRenderedComponent<RadioGroup<string>> cut = RenderGroup(defaultValue: "a");

        IElement indicator = Assert.Single(cut.FindAll(IndicatorSelector));
        Assert.False(indicator.HasAttribute("data-starting-style"));
        Assert.Equal(string.Empty, indicator.GetAttribute("data-checked"));
        Assert.Equal(s_checkedAttach, Assert.Single(test.TransitionsAttaches));
    }

    [Fact(DisplayName = "port: an indicator a selection mounts is created with data-starting-style and hands it over on attach")]
    [Trait("Requirement", "RAD-ANI-02")]
    [Trait("Requirement", "RAD-STA-04")]
    public async Task Port_an_indicator_a_selection_mounts_is_created_with_data_starting_style()
    {
        List<TransitionStatus?> statuses = [];
        IRenderedComponent<RadioGroup<string>> cut = RenderGroup(classValue: state =>
        {
            statuses.Add(state.TransitionStatus);
            return null;
        });
        Assert.Empty(cut.FindAll(IndicatorSelector));

        await test.ToggleAsync(cut, 0);

        IElement indicator = Assert.Single(cut.FindAll(IndicatorSelector));
        Assert.Equal(string.Empty, indicator.GetAttribute("data-starting-style"));
        Assert.Equal(
            """{"open":true,"mounted":true,"status":"starting","enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":false,"openSelector":"[data-checked]"}""",
            Assert.Single(test.TransitionsAttaches));
        Assert.Equal(TransitionStatus.Starting, statuses[^1]);

        // The module clears the attribute on the next frame and reports it; C# mirrors the status (RAD-STA-04's lag).
        int renders = statuses.Count;
        await test.ReportTransitionAsync(cut, null, mounted: true);
        Assert.Equal([null], statuses.Skip(renders));
    }

    [Fact(DisplayName = "port: an unchecked indicator stays through its exit and is removed by the completion report")]
    [Trait("Requirement", "RAD-ANI-03")]
    [Trait("Requirement", "RAD-ANI-01")]
    public async Task Port_an_unchecked_indicator_stays_through_its_exit()
    {
        List<TransitionStatus?> statuses = [];
        IRenderedComponent<RadioGroup<string>> cut = RenderGroup(defaultValue: "a", classValue: state =>
        {
            statuses.Add(state.TransitionStatus);
            return null;
        });

        await test.ToggleAsync(cut, 1);

        // Still in the document, unchecked (the module's open selector reads data-checked), with the ending status.
        IElement indicator = cut.Find(IndicatorSelector);
        Assert.Equal(string.Empty, indicator.GetAttribute("data-unchecked"));
        Assert.Equal(TransitionStatus.Ending, statuses[^1]);

        int renders = statuses.Count;
        await test.CompleteExitAsync(cut);

        Assert.Empty(cut.FindAll(IndicatorSelector));
        Assert.Single(test.TransitionsHandle.Invocations["dispose"]);
        Assert.Equal(renders, statuses.Count);
    }

    [Fact(DisplayName = "port: a kept indicator is never removed or hidden, and its exit still reports the ending status")]
    [Trait("Requirement", "RAD-ANI-04")]
    [Trait("Requirement", "RAD-API-10")]
    [Trait("Requirement", "RAD-DOM-15")]
    public async Task Port_a_kept_indicator_is_never_removed()
    {
        List<TransitionStatus?> statuses = [];
        IRenderedComponent<RadioGroup<string>> cut = RenderGroup(defaultValue: "a", keepMounted: true, classValue: state =>
        {
            statuses.Add(state.TransitionStatus);
            return null;
        });

        await test.ToggleAsync(cut, 1);
        Assert.Equal(TransitionStatus.Ending, statuses[^1]);
        await test.CompleteExitAsync(cut);

        IElement indicator = Assert.Single(cut.FindAll(IndicatorSelector));
        Assert.Equal(string.Empty, indicator.GetAttribute("data-unchecked"));
        Assert.False(indicator.HasAttribute("hidden"));
        Assert.False(indicator.HasAttribute("inert"));
        Assert.False(indicator.HasAttribute("style"));
        Assert.Null(statuses[^1]);
        Assert.Empty(test.TransitionsHandle.Invocations["dispose"]);
    }

    [Fact(DisplayName = "port: a KeepMounted change is sent in an update")]
    [Trait("Requirement", "RAD-API-10")]
    public void Port_a_KeepMounted_change_is_sent_in_an_update()
    {
        IRenderedComponent<RadioGroup<string>> cut = RenderGroup(defaultValue: "a");

        cut.FindComponent<RadioIndicator>().Render(p => p.Add(x => x.KeepMounted, true));

        Assert.Contains("\"keepMounted\":true", Assert.Single(test.TransitionsUpdates), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the indicator carries its root's state attributes and none of its handlers, ARIA or id")]
    [Trait("Requirement", "RAD-STR-04")]
    [Trait("Requirement", "RAD-DOM-15")]
    [Trait("Requirement", "RAD-STA-04")]
    public void Port_the_indicator_carries_its_roots_state_attributes()
    {
        IRenderedComponent<RadioGroup<string>> cut = RenderGroup(defaultValue: "a", disabled: true);

        IElement indicator = cut.Find(IndicatorSelector);
        string[] names = [.. indicator.Attributes.Select(a => a.Name).Where(n => !n.StartsWith("blazor:", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        Assert.Equal(["data-checked", "data-disabled", "data-testid"], names);
    }

    private IRenderedComponent<RadioGroup<string>> RenderGroup(string? defaultValue = null, bool keepMounted = false, bool disabled = false, Func<RadioIndicatorState, string?>? classValue = null)
    {
        RenderFragment Radio(string value, int sequence) => builder =>
        {
            builder.OpenComponent<RadioRoot<string>>(sequence);
            builder.AddComponentParameter(sequence + 1, nameof(RadioRoot<string>.Value), value);
            builder.AddComponentParameter(sequence + 2, "ChildContent", (RenderFragment)(content =>
            {
                if (value != "a")
                {
                    return;
                }

                content.OpenComponent<RadioIndicator>(0);
                content.AddComponentParameter(1, nameof(RadioIndicator.KeepMounted), keepMounted);
                content.AddAttribute(2, "data-testid", "indicator");
                if (classValue is not null)
                {
                    content.AddComponentParameter(3, nameof(RadioIndicator.ClassValue), classValue);
                }

                content.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Context.Render<RadioGroup<string>>(p =>
        {
            if (defaultValue is not null)
            {
                p.Add(x => x.DefaultValue, defaultValue);
            }

            p.Add(x => x.Disabled, disabled);
            p.AddChildContent(builder =>
            {
                builder.AddContent(0, Radio("a", 10));
                builder.AddContent(1, Radio("b", 20));
            });
        });
    }
}
