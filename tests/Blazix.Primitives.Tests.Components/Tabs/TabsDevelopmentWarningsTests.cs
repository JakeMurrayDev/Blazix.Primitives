using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Blazix.Primitives.Tests.Components.TabsTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TAB-EDGE-03, TAB-EDGE-04 and TAB-EDGE-05 through the development warnings switch, which is on for the whole run
// (Button/DevelopmentWarningsSwitch). The root's messages are Controlled<T>'s (UTL-EDGE-22/23) with the name `Tabs`; the
// tab's are the button mechanism's (UBT-EDGE-01/02), reported by the `button` module at attach and driven here by hand.
/// <summary>The development messages of the tabs root and the tab.</summary>
public sealed class TabsDevelopmentWarningsTests : IDisposable
{
    private readonly TabsTestContext test = new();
    private readonly CapturingLogger<TabsRoot<int>> rootLogger = new();
    private readonly CapturingLogger<TabsTab<int>> tabLogger = new();

    public TabsDevelopmentWarningsTests()
    {
        test.Context.Services.AddSingleton<ILogger<TabsRoot<int>>>(rootLogger);
        test.Context.Services.AddSingleton<ILogger<TabsTab<int>>>(tabLogger);
    }

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "continues honoring an initially disabled explicit defaultValue after defaultValue changes")]
    [Trait("Requirement", "TAB-EDGE-04")]
    [Trait("Requirement", "TAB-STA-07")]
    public void Continues_honoring_an_initially_disabled_explicit_defaultValue_after_defaultValue_changes()
    {
        Microsoft.AspNetCore.Components.RenderFragment content = List(Tab(0, t => t.Add(x => x.Disabled, true)), Tab(1), Tab(2));
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(content));
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));

        cut.Render(p => p
            .Add(x => x.DefaultValue, 1)
            .AddChildContent(content));

        Assert.Single(rootLogger.Entries, e => e.Message.StartsWith(
            "A component is changing the default value state of an uncontrolled Tabs after being initialized.",
            StringComparison.Ordinal));
        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("false", tabs[1].GetAttribute("aria-selected"));
        Assert.Equal("false", tabs[2].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "port: a value that stops being supplied logs the controlled-state error once, with the name Tabs")]
    [Trait("Requirement", "TAB-EDGE-03")]
    public void Port_a_value_that_stops_being_supplied_logs_the_controlled_state_error_once()
    {
        Microsoft.AspNetCore.Components.RenderFragment content = List(Tab(0), Tab(1));
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 1)
            .AddChildContent(content));

        cut.Render(p => p.AddChildContent(content));
        cut.Render(p => p.AddChildContent(content));

        (LogLevel _, string message) = Assert.Single(rootLogger.Entries);
        Assert.StartsWith("A component is changing the controlled value state of Tabs to be uncontrolled.\n", message, StringComparison.Ordinal);
        Assert.Contains("Decide between using a controlled or uncontrolled Tabs element for the lifetime of the component.", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: a value supplied after the first render logs the controlled-state error once, with the name Tabs")]
    [Trait("Requirement", "TAB-EDGE-03")]
    public void Port_a_value_supplied_after_the_first_render_logs_the_controlled_state_error_once()
    {
        Microsoft.AspNetCore.Components.RenderFragment content = List(Tab(0), Tab(1));
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(content));

        cut.Render(p => p.Add(x => x.Value, 1).Add(x => x.DefaultValue, 0).AddChildContent(content));
        cut.Render(p => p.Add(x => x.Value, 0).Add(x => x.DefaultValue, 0).AddChildContent(content));

        (LogLevel _, string message) = Assert.Single(rootLogger.Entries);
        Assert.StartsWith("A component is changing the uncontrolled value state of Tabs to be controlled.\n", message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "port: a tab whose element does not match NativeButton logs the button mechanism's error once")]
    [Trait("Requirement", "TAB-EDGE-05")]
    [InlineData(true, "Blazix: A component that acts as a button expected a native `<button>` because the `NativeButton` parameter is `true`.")]
    [InlineData(false, "Blazix: A component that acts as a button expected a non-`<button>` because the `NativeButton` parameter is `false`.")]
    public async Task Port_a_tab_whose_element_does_not_match_NativeButton_logs_once(bool expectedNative, string start)
    {
        Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(List(Tab(0, t => t.Add(x => x.NativeButton, expectedNative)), Tab(1, t => t.Add(x => x.NativeButton, expectedNative)))));
        string payload = $$"""{"expectedNative":{{(expectedNative ? "true" : "false")}}}""";

        // Each tab's `button` handle runs the check at attach; the session's log writes the message once.
        await Context.Renderer.Dispatcher.InvokeAsync(() =>
        {
            test.ButtonTwin(0).OnNativeButtonMismatch(payload);
            test.ButtonTwin(1).OnNativeButtonMismatch(payload);
        });

        (LogLevel _, string message) = Assert.Single(tabLogger.Entries);
        Assert.StartsWith(start, message, StringComparison.Ordinal);
    }
}
