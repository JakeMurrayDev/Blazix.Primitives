using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DialogClose.test.tsx</c>: disabled (DLG-API-22/23, DOM-24, EDGE-09), the consumer's
/// <c>onclick</c> and its veto (DLG-PTR-19), and no request while closed. Titles are Base UI's. The trigger's press is the
/// <c>interactions</c> module's report, driven into the trigger's twin as the module sends it on Server.
/// </summary>
public sealed class DialogCloseTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "disables the button")]
    [Trait("Requirement", "DLG-API-23")]
    [Trait("Requirement", "DLG-DOM-24")]
    [Trait("Requirement", "DLG-EDGE-09")]
    public async Task Disables_the_button()
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p.Add(x => x.WithClose, true).Add(x => x.CloseDisabled, true));
        Assert.Empty(cut.Instance.OpenChanges);

        await test.PressAsync(cut, cut.Find("[data-testid=trigger]").Id!, open: true);
        Assert.Equal(["true:trigger-press"], cut.Instance.OpenChanges);

        IElement close = cut.Find("[data-testid=close]");
        Assert.True(close.HasAttribute("disabled"));
        Assert.True(close.HasAttribute("data-disabled"));
        await close.ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Single(cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "custom element")]
    [Trait("Requirement", "DLG-API-22")]
    [Trait("Requirement", "DLG-API-23")]
    [Trait("Requirement", "DLG-DOM-24")]
    [Trait("Requirement", "DLG-EDGE-09")]
    public async Task Custom_element()
    {
        RenderFragment span = builder =>
        {
            builder.OpenElement(0, "span");
            builder.CloseElement();
        };
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.WithClose, true)
            .Add(x => x.CloseDisabled, true)
            .Add(x => x.CloseNativeButton, false)
            .Add(x => x.CloseRender, span));

        await test.PressAsync(cut, cut.Find("[data-testid=trigger]").Id!, open: true);
        Assert.Equal(["true:trigger-press"], cut.Instance.OpenChanges);

        IElement close = cut.Find("[data-testid=close]");
        Assert.Equal("SPAN", close.TagName);
        Assert.False(close.HasAttribute("disabled"));
        Assert.True(close.HasAttribute("data-disabled"));
        Assert.Equal("true", close.GetAttribute("aria-disabled"));
        await close.ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Single(cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "closes the dialog when undefined is passed to the `onClick` prop")]
    [Trait("Requirement", "DLG-PTR-19")]
    public async Task Closes_the_dialog_when_undefined_is_passed_to_the_onClick_prop()
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.WithClose, true)
            .Add(x => x.CloseAttributes, new Dictionary<string, object> { ["onclick"] = default(EventCallback<MouseEventArgs>) }));

        await test.PressAsync(cut, cut.Find("[data-testid=trigger]").Id!, open: true);
        Assert.Equal(["true:trigger-press"], cut.Instance.OpenChanges);

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Equal(["true:trigger-press", "false:close-press"], cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "does not close the dialog when the Base UI click handler is prevented")]
    [Trait("Requirement", "DLG-PTR-19")]
    public async Task Does_not_close_the_dialog_when_the_Base_UI_click_handler_is_prevented()
    {
        Action<MouseEventArgs> prevent = e => e.PreventPartHandler();
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Modal, false)
            .Add(x => x.WithTrigger, false)
            .Add(x => x.WithClose, true)
            .Add(x => x.CloseAttributes, new Dictionary<string, object> { ["onclick"] = EventCallback.Factory.Create(this, prevent) }));

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.Empty(cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "does not request another close when clicked after the dialog has closed")]
    [Trait("Requirement", "DLG-PTR-19")]
    public async Task Does_not_request_another_close_when_clicked_after_the_dialog_has_closed()
    {
        int clicks = 0;
        Action<MouseEventArgs> count = _ => clicks++;
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.ControlledOpen, false)
            .Add(x => x.Modal, false)
            .Add(x => x.KeepMounted, true)
            .Add(x => x.WithTrigger, false)
            .Add(x => x.WithClose, true)
            .Add(x => x.CloseAttributes, new Dictionary<string, object> { ["onclick"] = EventCallback.Factory.Create(this, count) }));

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Equal(1, clicks);
        Assert.Empty(cut.Instance.OpenChanges);
    }
}
