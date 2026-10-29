using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The AlertDialog parts' diagnostics and their interchange with the Dialog parts. The context errors name the AlertDialog
/// parts (ADR 08 Amendments 2026-10-27, chosen by the user 2026-10-27: a dated deviation from ADL-EDGE-01, whose handle
/// warnings keep Dialog's wording, AlertDialogApiTests), each built from the family name the part passes, so a Dialog part
/// keeps Dialog's message under an AlertDialog root. ADL-STR-04's port titles: every part reads the nearest dialog-family
/// root, so the root's mode decides the behaviour, whichever family the part is from.
/// </summary>
public sealed class AlertDialogPartTests : IDisposable
{
    private const string MissingRoot = "Blazix: The `AlertDialogRoot` context is missing. AlertDialog parts must be placed within `<AlertDialogRoot>`.";

    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    public static TheoryData<string> RootedParts => ["Portal", "Backdrop", "Viewport", "Popup", "Title", "Description", "Close"];

    [Fact(DisplayName = "port: an AlertDialogTrigger with neither a root nor a handle throws a message that names the AlertDialog parts")]
    [Trait("Requirement", "ADL-EDGE-01")]
    [Trait("Requirement", "DLG-EDGE-01")]
    public void An_AlertDialogTrigger_with_neither_a_root_nor_a_handle_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<AlertDialogTrigger<NoPayload>>());
        Assert.Equal("Blazix: `<AlertDialogTrigger>` must be used within `<AlertDialogRoot>` or provided with a handle.", error.Message);
    }

    [Theory(DisplayName = "port: an AlertDialog part outside any root throws a message that names the AlertDialog root:")]
    [MemberData(nameof(RootedParts))]
    [Trait("Requirement", "ADL-EDGE-01")]
    [Trait("Requirement", "DLG-EDGE-02")]
    public void An_AlertDialog_part_outside_any_root_throws(string part)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render(Part(part)));
        Assert.Equal(MissingRoot, error.Message);
    }

    [Theory(DisplayName = "port: an AlertDialog viewport or popup outside the portal throws a message that names the AlertDialog portal:")]
    [InlineData("Viewport")]
    [InlineData("Popup")]
    [Trait("Requirement", "ADL-EDGE-01")]
    [Trait("Requirement", "DLG-EDGE-03")]
    public void An_AlertDialog_viewport_or_popup_outside_the_portal_throws(string part)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<AlertDialogRoot<NoPayload>>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, Part(part))));
        Assert.Equal("Blazix: `<AlertDialogPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "port: a part outside its portal names its own family's portal, whichever family's root encloses it")]
    [Trait("Requirement", "ADL-STR-04")]
    [Trait("Requirement", "ADL-EDGE-01")]
    [Trait("Requirement", "DLG-EDGE-03")]
    public void A_part_outside_its_portal_names_its_own_familys_portal()
    {
        InvalidOperationException alert = Assert.Throws<InvalidOperationException>(() => test.Context.Render<DialogRoot<NoPayload>>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, Part("Popup"))));
        Assert.Equal("Blazix: `<AlertDialogPortal>` is missing.", alert.Message);

        InvalidOperationException dialog = Assert.Throws<InvalidOperationException>(() => test.Context.Render<AlertDialogRoot<NoPayload>>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<DialogPopup>()));
        Assert.Equal("Blazix: `<DialogPortal>` is missing.", dialog.Message);
    }

    // Base UI's only evidence is ToolbarButton.test.tsx's comment that AlertDialog.Trigger is Dialog.Trigger; the port's parts
    // are distinct types, so the interchange is asserted here.
    [Fact(DisplayName = "port: a DialogPopup under an AlertDialogRoot renders role=\"alertdialog\", and an AlertDialogPopup under a DialogRoot renders role=\"dialog\"")]
    [Trait("Requirement", "ADL-STR-04")]
    [Trait("Requirement", "ADL-ARIA-01")]
    public void The_roots_mode_decides_the_popups_role()
    {
        IRenderedComponent<IComponent> alertRoot = test.Context.Render(builder =>
        {
            builder.OpenComponent<AlertDialogRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(AlertDialogRoot<NoPayload>.Open), true);
            builder.AddComponentParameter(2, nameof(AlertDialogRoot<NoPayload>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<DialogPortal>(0);
                content.AddComponentParameter(1, nameof(DialogPortal.ChildContent), (RenderFragment)(portal =>
                {
                    portal.OpenComponent<DialogPopup>(0);
                    portal.CloseComponent();
                }));
                content.CloseComponent();
            }));
            builder.CloseComponent();
        });
        Assert.Single(alertRoot.FindAll("[role=alertdialog]"));
        Assert.Empty(alertRoot.FindAll("[role=dialog]"));

        IRenderedComponent<IComponent> dialogRoot = test.Context.Render(builder =>
        {
            builder.OpenComponent<DialogRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(DialogRoot<NoPayload>.Open), true);
            builder.AddComponentParameter(2, nameof(DialogRoot<NoPayload>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<AlertDialogPortal>(0);
                content.AddComponentParameter(1, nameof(AlertDialogPortal.ChildContent), (RenderFragment)(portal =>
                {
                    portal.OpenComponent<AlertDialogPopup>(0);
                    portal.CloseComponent();
                }));
                content.CloseComponent();
            }));
            builder.CloseComponent();
        });
        Assert.Single(dialogRoot.FindAll("[role=dialog]"));
        Assert.Empty(dialogRoot.FindAll("[role=alertdialog]"));
    }

    [Fact(DisplayName = "port: an AlertDialogTrigger opens the DialogRoot that encloses it, and a DialogTrigger the AlertDialogRoot that encloses it")]
    [Trait("Requirement", "ADL-STR-04")]
    [Trait("Requirement", "ADL-API-05")]
    public async Task A_trigger_opens_the_root_that_encloses_it_whichever_family()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(builder =>
        {
            builder.OpenComponent<DialogRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(DialogRoot<NoPayload>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<AlertDialogTrigger<NoPayload>>(0);
                content.AddAttribute(1, "id", "alert-trigger");
                content.CloseComponent();
                content.OpenComponent<DialogPortal>(2);
                content.AddComponentParameter(3, nameof(DialogPortal.ChildContent), (RenderFragment)(portal =>
                {
                    portal.OpenComponent<DialogPopup>(0);
                    portal.AddAttribute(1, "data-testid", "dialog-popup");
                    portal.CloseComponent();
                }));
                content.CloseComponent();
            }));
            builder.CloseComponent();
            builder.OpenComponent<AlertDialogRoot<NoPayload>>(4);
            builder.AddComponentParameter(5, nameof(AlertDialogRoot<NoPayload>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<DialogTrigger<NoPayload>>(0);
                content.AddAttribute(1, "id", "dialog-trigger");
                content.CloseComponent();
                content.OpenComponent<AlertDialogPortal>(2);
                content.AddComponentParameter(3, nameof(AlertDialogPortal.ChildContent), (RenderFragment)(portal =>
                {
                    portal.OpenComponent<AlertDialogPopup>(0);
                    portal.AddAttribute(1, "data-testid", "alert-popup");
                    portal.CloseComponent();
                }));
                content.CloseComponent();
            }));
            builder.CloseComponent();
        });

        await test.PressAsync(cut, "alert-trigger", open: true);
        Assert.Equal("dialog", cut.Find("[data-testid=dialog-popup]").GetAttribute("role"));
        Assert.Equal("true", cut.Find("#alert-trigger").GetAttribute("aria-expanded"));

        await test.PressAsync(cut, "dialog-trigger", open: true);
        Assert.Equal("alertdialog", cut.Find("[data-testid=alert-popup]").GetAttribute("role"));
        Assert.Equal("true", cut.Find("#dialog-trigger").GetAttribute("aria-expanded"));
    }

    private static RenderFragment Part(string name) => builder =>
    {
        switch (name)
        {
            case "Portal":
                builder.OpenComponent<AlertDialogPortal>(0);
                break;
            case "Backdrop":
                builder.OpenComponent<AlertDialogBackdrop>(0);
                break;
            case "Viewport":
                builder.OpenComponent<AlertDialogViewport>(0);
                break;
            case "Popup":
                builder.OpenComponent<AlertDialogPopup>(0);
                break;
            case "Title":
                builder.OpenComponent<AlertDialogTitle>(0);
                break;
            case "Description":
                builder.OpenComponent<AlertDialogDescription>(0);
                break;
            default:
                builder.OpenComponent<AlertDialogClose>(0);
                break;
        }

        builder.CloseComponent();
    };
}
