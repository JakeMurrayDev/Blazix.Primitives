using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the filter query's reset when a filterable menu closes (MNU-EVT-18, API-58; M5 track 12), over
/// <see cref="MenuFilterInputHost"/>.
/// </summary>
public sealed class MenuFilterCloseTests : BunitContext
{
    private readonly BunitJSModuleInterop module;
    private readonly BunitJSModuleInterop handle;

    public MenuFilterCloseTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule("./_content/Blazix.Primitives/js/menu-filter.js");
        handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "reports popup-close when a controlled close discards the query")]
    [Trait("Requirement", "MNU-EVT-18")]
    [Trait("Requirement", "MNU-API-58")]
    public void Reports_popup_close()
    {
        List<(string Value, EventReason Reason)> requests = [];
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p
            .Add(x => x.DefaultQuery, "ren")
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<string> args) => requests.Add((args.Value, args.Details.Reason))));

        cut.Render(p => p.Add(x => x.Open, false));

        cut.WaitForAssertion(() => Assert.Equal([("", EventReason.PopupClose)], requests));
    }

    [Fact(DisplayName = "port: a reopened menu starts from an empty query")]
    [Trait("Requirement", "MNU-EVT-18")]
    public void A_reopened_menu_starts_empty()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p.Add(x => x.DefaultQuery, "ren"));

        cut.Render(p => p.Add(x => x.Open, false));
        cut.Render(p => p.Add(x => x.Open, true));

        cut.WaitForAssertion(() => Assert.Equal("", cut.Find("input").GetAttribute("value")));
    }

    [Fact(DisplayName = "keeps the query when the popup-close reset is canceled")]
    [Trait("Requirement", "MNU-EVT-18")]
    [Trait("Requirement", "MNU-API-58")]
    public void Keeps_the_query_when_the_reset_is_canceled()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p
            .Add(x => x.DefaultQuery, "ren")
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<string> args) =>
            {
                if (args.Details.Reason == EventReason.PopupClose)
                {
                    args.Details.Cancel();
                }
            }));

        cut.Render(p => p.Add(x => x.Open, false));
        cut.Render(p => p.Add(x => x.Open, true));

        cut.WaitForAssertion(() => Assert.Equal("ren", cut.Find("input").GetAttribute("value")));
    }

    [Fact(DisplayName = "port: with the popup kept mounted, the reset is written to the input when the menu reopens")]
    [Trait("Requirement", "MNU-EVT-18")]
    public void A_kept_mounted_input_is_cleared_on_reopen()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p
            .Add(x => x.DefaultQuery, "ren")
            .Add(x => x.KeepMounted, true));

        cut.Render(p => p.Add(x => x.Open, false));
        cut.WaitForAssertion(() => Assert.Contains("\"open\":false", LastUpdate(), StringComparison.Ordinal));
        Assert.Contains("\"value\":\"ren\"", LastUpdate(), StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.Open, true));

        cut.WaitForAssertion(() =>
        {
            string update = LastUpdate();
            Assert.Contains("\"open\":true", update, StringComparison.Ordinal);
            Assert.Contains("\"value\":\"\"", update, StringComparison.Ordinal);
            Assert.Contains("\"valueRevision\":1", update, StringComparison.Ordinal);
        });
    }

    private string LastUpdate() =>
        (string)handle.Invocations["update"].Last(i => ((string)i.Arguments[0]!).Contains("\"part\":\"input\"", StringComparison.Ordinal)).Arguments[0]!;
}
