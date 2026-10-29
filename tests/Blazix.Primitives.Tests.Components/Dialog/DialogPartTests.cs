using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DialogTrigger.test.tsx</c>, <c>DialogViewport.test.tsx</c> and <c>DialogPortal.test.tsx</c>:
/// the missing root (DLG-STR-02, EDGE-01), the disabled trigger (DLG-API-15/16, DOM-04, EDGE-09), the viewport's mounting
/// (DLG-STR-06, DOM-05, DOM-11, API-17, ANI-01), the missing portal (DLG-STR-03, EDGE-03) and EDGE-10 restated. Titles are
/// Base UI's unless marked "port:". A trigger's press is the <c>interactions</c> module's report on its twin.
/// </summary>
public sealed class DialogPartTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error without a root or handle")]
    [Trait("Requirement", "DLG-STR-02")]
    [Trait("Requirement", "DLG-EDGE-01")]
    public void Throws_a_descriptive_error_without_a_root_or_handle()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<DialogTrigger<NoPayload>>());
        Assert.Equal("Blazix: `<DialogTrigger>` must be used within `<DialogRoot>` or provided with a handle.", error.Message);
    }

    // At L2 the press's absence is the trigger's options: a disabled trigger configures no press-to-toggle, so the module
    // never decides one; the browser's blocked click and Tab order are the same title at L4.
    [Fact(DisplayName = "disables the dialog")]
    [Trait("Requirement", "DLG-API-16")]
    [Trait("Requirement", "DLG-DOM-04")]
    [Trait("Requirement", "DLG-EDGE-09")]
    public void Disables_the_dialog()
    {
        IRenderedComponent<DialogRoot<NoPayload>> cut = test.Context.Render<DialogRoot<NoPayload>>(p => p
            .Add(x => x.Modal, false)
            .AddChildContent<DialogTrigger<NoPayload>>(trigger => trigger.Add(x => x.Disabled, true)));

        IElement trigger = cut.Find("button");
        Assert.True(trigger.HasAttribute("disabled"));
        Assert.True(trigger.HasAttribute("data-disabled"));
        string options = Assert.Single(DialogTestContext.AttachOptions(test.InteractionsModule), o => o!.Contains("\"trigger\":", StringComparison.Ordinal))!;
        Assert.DoesNotContain("\"click\":", options, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "custom element")]
    [Trait("Requirement", "DLG-API-15")]
    [Trait("Requirement", "DLG-API-16")]
    [Trait("Requirement", "DLG-DOM-04")]
    [Trait("Requirement", "DLG-EDGE-09")]
    public void Custom_element()
    {
        RenderFragment span = builder =>
        {
            builder.OpenElement(0, "span");
            builder.CloseElement();
        };
        IRenderedComponent<DialogRoot<NoPayload>> cut = test.Context.Render<DialogRoot<NoPayload>>(p => p
            .Add(x => x.Modal, false)
            .AddChildContent<DialogTrigger<NoPayload>>(trigger => trigger
                .Add(x => x.Disabled, true)
                .Add(x => x.NativeButton, false)
                .Add(x => x.Render, span)));

        IElement trigger = cut.Find("[role=button]");
        Assert.Equal("SPAN", trigger.TagName);
        Assert.False(trigger.HasAttribute("disabled"));
        Assert.True(trigger.HasAttribute("data-disabled"));
        Assert.Equal("true", trigger.GetAttribute("aria-disabled"));
        Assert.Equal("-1", trigger.GetAttribute("tabindex"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "renders only when the dialog is mounted by default")]
    [Trait("Requirement", "DLG-STR-06")]
    [Trait("Requirement", "DLG-DOM-05")]
    [Trait("Requirement", "DLG-DOM-11")]
    [Trait("Requirement", "DLG-ANI-01")]
    public async Task Renders_only_when_the_dialog_is_mounted_by_default()
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.ControlledOpen, false)
            .Add(x => x.Modal, false)
            .Add(x => x.WithViewport, true));
        Assert.Empty(cut.FindAll("[data-testid=viewport]"));

        await test.PressAsync(cut, cut.Find("[data-testid=trigger]").Id!, open: true);

        IElement viewport = cut.Find("[data-testid=viewport]");
        Assert.NotNull(viewport.QuerySelector("[data-testid=popup]"));
    }

    [Fact(DisplayName = "stays mounted when used within a keepMounted portal")]
    [Trait("Requirement", "DLG-API-17")]
    [Trait("Requirement", "DLG-DOM-11")]
    [Trait("Requirement", "DLG-ANI-01")]
    public void Stays_mounted_when_used_within_a_keepMounted_portal()
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.ControlledOpen, true)
            .Add(x => x.Modal, false)
            .Add(x => x.WithTrigger, false)
            .Add(x => x.KeepMounted, true)
            .Add(x => x.WithViewport, true));
        Assert.Single(cut.FindAll("[data-testid=viewport]"));

        cut.Render(p => p.Add(x => x.ControlledOpen, false));

        Assert.Single(cut.FindAll("[data-testid=viewport]"));
    }

    [Fact(DisplayName = "throws a descriptive error when a portaled part is rendered without <Dialog.Portal>")]
    [Trait("Requirement", "DLG-STR-03")]
    [Trait("Requirement", "DLG-EDGE-03")]
    public void Throws_a_descriptive_error_when_a_portaled_part_is_rendered_without_the_portal()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<DialogRoot<NoPayload>>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<DialogViewport>()));
        Assert.Equal("Blazix: `<DialogPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "port: a child that renders after an awaited OnInitializedAsync inside the popup renders its fallback, then its content, without an update loop")]
    [Trait("Requirement", "DLG-EDGE-10")]
    public void A_child_rendered_after_an_awaited_initialization_renders_without_an_update_loop()
    {
        TaskCompletionSource loading = new();
        IRenderedComponent<DialogRoot<NoPayload>> cut = test.Context.Render<DialogRoot<NoPayload>>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<DialogPortal>(portal => portal.AddChildContent<DialogPopup>(popup => popup.AddChildContent<DeferredContent>(deferred => deferred
                .Add(x => x.Loading, loading.Task)
                .Add(x => x.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Greetings</p>")))))));
        Assert.Contains("Loading…", cut.Markup, StringComparison.Ordinal);
        int renders = cut.RenderCount;

        loading.SetResult();

        cut.WaitForAssertion(() => Assert.Contains("Greetings", cut.Markup, StringComparison.Ordinal));
        Assert.True(cut.RenderCount - renders < 5, $"the root rendered {cut.RenderCount - renders} times after the child loaded");
    }
}
