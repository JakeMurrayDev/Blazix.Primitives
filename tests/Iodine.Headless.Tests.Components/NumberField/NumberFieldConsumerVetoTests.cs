using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// The input's consumer handlers through the decision points on WebAssembly (issue 277, 6b; design point 1 (a); NUM-KEY-11,
// NUM-EVT-14, NUM-API-25). The module decides a keydown at the input's root node and a blur in the target phase, while
// Blazor dispatches onkeydown (document, bubble) and onblur (document, capture) first: the part's own handler then runs the
// consumer and the decision reuses its verdict, so the consumer runs once and its PreventPartHandler() still vetoes. A
// paste or an edit the module decides first, and Blazor's dispatch skips it (the one-shot marker).
/// <summary>A consumer handler on the input runs once per event, whichever of Blazor's dispatch and the decision comes first.</summary>
public sealed class NumberFieldConsumerVetoTests : IDisposable
{
    private const string KeyJson = """{"kind":"KeyboardEvent","type":"keydown","key":"ArrowUp"}""";
    private const string BlurJson = """{"kind":"FocusEvent","type":"blur"}""";
    private const string PasteJson = """{"kind":"ClipboardEvent","type":"paste"}""";

    private readonly NumberFieldTestContext test = new();
    private int calls;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a keydown Blazor dispatched first runs the consumer once, and the decision keeps its veto")]
    [Trait("Requirement", "NUM-KEY-11")]
    [Trait("Requirement", "NUM-API-25")]
    public void A_keydown_Blazor_dispatched_first_runs_the_consumer_once()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render("onkeydown", veto: true);

        cut.Find("input[type=text]").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        string verdict = test.NumberFieldTwin().Decide("input-key", KeyJson);

        Assert.Equal(1, calls);
        Assert.Equal("stop", verdict);
    }

    [Fact(DisplayName = "port: a blur Blazor dispatched first runs the consumer once, and the decision runs when it did not veto")]
    [Trait("Requirement", "NUM-EVT-14")]
    public void A_blur_Blazor_dispatched_first_runs_the_consumer_once()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render("onblur", veto: false);

        cut.Find("input[type=text]").Blur();
        string verdict = test.NumberFieldTwin().Decide("input-blur", BlurJson);

        Assert.Equal(1, calls);
        Assert.Equal("run", verdict);
    }

    [Fact(DisplayName = "port: a paste the decision runs first is not run again by Blazor's dispatch")]
    [Trait("Requirement", "NUM-EVT-16")]
    public void A_paste_the_decision_runs_first_is_not_run_again()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render("onpaste", veto: true);

        string verdict = test.NumberFieldTwin().Decide("input-paste", PasteJson);
        cut.Find("input[type=text]").TriggerEvent("onpaste", new ClipboardEventArgs { Type = "paste" });
        // A later paste whose decision came first runs the consumer again: no stale verdict.
        string second = test.NumberFieldTwin().Decide("input-paste", PasteJson);

        Assert.Equal("stop", verdict);
        Assert.Equal("stop", second);
        Assert.Equal(2, calls);
    }

    private IRenderedComponent<NumberFieldRoot> Render(string eventName, bool veto)
    {
        Action<EventArgs> handler = e =>
        {
            calls++;
            if (veto)
            {
                e.PreventPartHandler();
            }
        };
        RenderFragment parts = b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.AddAttribute(1, eventName, eventName switch
            {
                "onkeydown" => (object)EventCallback.Factory.Create<KeyboardEventArgs>(this, e => handler(e)),
                "onblur" => EventCallback.Factory.Create<FocusEventArgs>(this, e => handler(e)),
                _ => EventCallback.Factory.Create<ClipboardEventArgs>(this, e => handler(e)),
            });
            b.CloseComponent();
        };
        return test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, CultureInfo.GetCultureInfo("en-US"))
            .Add(x => x.DefaultValue, 5)
            .Add(x => x.ChildContent, parts));
    }
}
