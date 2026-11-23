using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// Title and Description at L2: <c>toast/utils/useToastLabelPart.test.tsx</c>, which runs each title for both parts
/// (<c>describe.each</c>), every title verbatim under its part's name, and <c>ToastRoot.test.tsx</c>'s "keeps dynamic title
/// and description ids synchronized with mounted label parts". A substitute element's own children count, through the
/// pipeline's opt-in content gate (ADR 02 Amendments 2026-11-18, Toast DP11).
/// </summary>
public sealed class ToastLabelPartTests : IDisposable
{
    private readonly ToastTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "<Toast.Title /> throws a descriptive error when rendered outside <Toast.Root>")]
    [Trait("Requirement", "TST-STR-05")]
    [Trait("Requirement", "TST-EDGE-02")]
    public void Title_throws_a_descriptive_error_when_rendered_outside_ToastRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ToastProvider<NoPayload>>(p => p.AddChildContent<ToastTitle>()));

        Assert.Equal("Iodine: `ToastRootContext` is missing. Toast parts must be used within `<ToastRoot>`.", error.Message);
    }

    [Fact(DisplayName = "<Toast.Title /> renders the toast title by default")]
    [Trait("Requirement", "TST-API-15")]
    public void Title_renders_the_toast_title_by_default()
    {
        IRenderedComponent<ToastHost> cut = AddToastFromManager();

        Assert.Equal("title", cut.Find("[data-testid=title]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Title /> adds aria-labelledby to the root element")]
    [Trait("Requirement", "TST-ARIA-04")]
    public void Title_adds_aria_labelledby_to_the_root_element()
    {
        IRenderedComponent<ToastHost> cut = AddToastFromManager();

        string? id = cut.Find("[data-testid=title]").Id;
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, cut.Find("[data-testid=root]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "<Toast.Title /> does not render if it has no children")]
    [Trait("Requirement", "TST-DOM-16")]
    [Trait("Requirement", "TST-ARIA-04")]
    public void Title_does_not_render_if_it_has_no_children()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = null }));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("aria-labelledby"));
        Assert.Empty(cut.FindAll("[data-testid=title]"));
    }

    [Fact(DisplayName = "<Toast.Title /> renders a numeric zero child")]
    [Trait("Requirement", "TST-DOM-16")]
    [Trait("Requirement", "TST-API-15")]
    public void Title_renders_a_numeric_zero_child()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.Plain, children: builder => builder.AddContent(0, 0));

        Assert.Equal("0", cut.Find("[data-testid=title]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Title /> renders content passed through the render prop")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Title_renders_content_passed_through_the_render_prop()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.RenderProp);

        Assert.Equal("render prop content", cut.Find("div[id]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Title /> renders content passed through a render function")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Title_renders_content_passed_through_a_render_function()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.RenderFunction);

        Assert.Equal("render fn", cut.Find("div[id]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Title /> wires aria-labelledby to a part rendered through the render prop")]
    [Trait("Requirement", "TST-ARIA-04")]
    public void Title_wires_aria_labelledby_to_a_part_rendered_through_the_render_prop()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.RenderProp);

        Assert.Equal(cut.Find("div[id]").Id, cut.Find("[data-testid=root]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "<Toast.Title /> renders the toast title through a childless render prop")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Title_renders_the_toast_title_through_a_childless_render_prop()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.ChildlessRenderProp, new ToastObject { Id = "test", Title = "Toast content" });

        Assert.Equal("Toast content", cut.Find("[data-testid=part-render]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Title /> does not render a childless render prop when there is no content")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Title_does_not_render_a_childless_render_prop_when_there_is_no_content()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.ChildlessRenderProp);

        Assert.Empty(cut.FindAll("[data-testid=part-render]"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "<Toast.Title /> does not render when a render function returns no element")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Title_does_not_render_when_a_render_function_returns_no_element()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.EmptyRenderFunction);

        Assert.NotNull(cut.Find("[data-testid=root]"));
        Assert.Empty(cut.FindAll("[data-testid=part-render]"));
    }

    [Fact(DisplayName = "<Toast.Title /> clears aria-labelledby from the root when the content is removed")]
    [Trait("Requirement", "TST-DOM-16")]
    [Trait("Requirement", "TST-ARIA-04")]
    public void Title_clears_aria_labelledby_from_the_root_when_the_content_is_removed()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(false, ToastLabelForm.Plain, children: builder => builder.AddContent(0, "Toast content"), partId: "part");
        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("part", root.GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.Description, false).Add(x => x.Form, ToastLabelForm.Plain).Add(x => x.PartId, "part").Add(x => x.Children, (RenderFragment?)null));

        Assert.DoesNotContain("Toast content", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "<Toast.Title /> does not let an older part cleanup clear a newer part")]
    [Trait("Requirement", "TST-ARIA-10")]
    public void Title_does_not_let_an_older_part_cleanup_clear_a_newer_part()
    {
        IRenderedComponent<ToastLabelHost> cut = test.Context.Render<ToastLabelHost>(p => Parts(p, old: true, @new: false));
        Assert.Equal("old-part", cut.Find("[data-testid=root]").GetAttribute("aria-labelledby"));

        cut.Render(p => Parts(p, old: true, @new: true));
        Assert.Equal("new-part", cut.Find("[data-testid=root]").GetAttribute("aria-labelledby"));

        cut.Render(p => Parts(p, old: false, @new: true));
        Assert.Equal("new-part", cut.Find("[data-testid=root]").GetAttribute("aria-labelledby"));

        static void Parts(ComponentParameterCollectionBuilder<ToastLabelHost> p, bool old, bool @new) =>
            p.Add(x => x.Description, false).Add(x => x.Form, ToastLabelForm.OldAndNew).Add(x => x.ShowOld, old).Add(x => x.ShowNew, @new);
    }

    [Fact(DisplayName = "<Toast.Description /> throws a descriptive error when rendered outside <Toast.Root>")]
    [Trait("Requirement", "TST-STR-06")]
    [Trait("Requirement", "TST-EDGE-02")]
    public void Description_throws_a_descriptive_error_when_rendered_outside_ToastRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ToastProvider<NoPayload>>(p => p.AddChildContent<ToastDescription>()));

        Assert.Equal("Iodine: `ToastRootContext` is missing. Toast parts must be used within `<ToastRoot>`.", error.Message);
    }

    [Fact(DisplayName = "<Toast.Description /> renders the toast description by default")]
    [Trait("Requirement", "TST-API-15")]
    public void Description_renders_the_toast_description_by_default()
    {
        IRenderedComponent<ToastHost> cut = AddToastFromManager();

        Assert.Equal("description", cut.Find("[data-testid=description]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Description /> adds aria-describedby to the root element")]
    [Trait("Requirement", "TST-ARIA-05")]
    public void Description_adds_aria_describedby_to_the_root_element()
    {
        IRenderedComponent<ToastHost> cut = AddToastFromManager();

        string? id = cut.Find("[data-testid=description]").Id;
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, cut.Find("[data-testid=root]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "<Toast.Description /> does not render if it has no children")]
    [Trait("Requirement", "TST-DOM-16")]
    [Trait("Requirement", "TST-ARIA-05")]
    public void Description_does_not_render_if_it_has_no_children()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Description = null }));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll("[data-testid=description]"));
    }

    [Fact(DisplayName = "<Toast.Description /> renders a numeric zero child")]
    [Trait("Requirement", "TST-DOM-16")]
    [Trait("Requirement", "TST-API-15")]
    public void Description_renders_a_numeric_zero_child()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.Plain, children: builder => builder.AddContent(0, 0));

        Assert.Equal("0", cut.Find("[data-testid=description]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Description /> renders content passed through the render prop")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Description_renders_content_passed_through_the_render_prop()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.RenderProp);

        Assert.Equal("render prop content", cut.Find("div[id]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Description /> renders content passed through a render function")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Description_renders_content_passed_through_a_render_function()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.RenderFunction);

        Assert.Equal("render fn", cut.Find("div[id]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Description /> wires aria-describedby to a part rendered through the render prop")]
    [Trait("Requirement", "TST-ARIA-05")]
    public void Description_wires_aria_describedby_to_a_part_rendered_through_the_render_prop()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.RenderProp);

        Assert.Equal(cut.Find("div[id]").Id, cut.Find("[data-testid=root]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "<Toast.Description /> renders the toast description through a childless render prop")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Description_renders_the_toast_description_through_a_childless_render_prop()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.ChildlessRenderProp, new ToastObject { Id = "test", Description = "Toast content" });

        Assert.Equal("Toast content", cut.Find("[data-testid=part-render]").TextContent);
    }

    [Fact(DisplayName = "<Toast.Description /> does not render a childless render prop when there is no content")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Description_does_not_render_a_childless_render_prop_when_there_is_no_content()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.ChildlessRenderProp);

        Assert.Empty(cut.FindAll("[data-testid=part-render]"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "<Toast.Description /> does not render when a render function returns no element")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Description_does_not_render_when_a_render_function_returns_no_element()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.EmptyRenderFunction);

        Assert.NotNull(cut.Find("[data-testid=root]"));
        Assert.Empty(cut.FindAll("[data-testid=part-render]"));
    }

    [Fact(DisplayName = "<Toast.Description /> clears aria-describedby from the root when the content is removed")]
    [Trait("Requirement", "TST-DOM-16")]
    [Trait("Requirement", "TST-ARIA-05")]
    public void Description_clears_aria_describedby_from_the_root_when_the_content_is_removed()
    {
        IRenderedComponent<ToastLabelHost> cut = Label(true, ToastLabelForm.Plain, children: builder => builder.AddContent(0, "Toast content"), partId: "part");
        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("part", root.GetAttribute("aria-describedby"));

        cut.Render(p => p.Add(x => x.Description, true).Add(x => x.Form, ToastLabelForm.Plain).Add(x => x.PartId, "part").Add(x => x.Children, (RenderFragment?)null));

        Assert.DoesNotContain("Toast content", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "<Toast.Description /> does not let an older part cleanup clear a newer part")]
    [Trait("Requirement", "TST-ARIA-10")]
    public void Description_does_not_let_an_older_part_cleanup_clear_a_newer_part()
    {
        IRenderedComponent<ToastLabelHost> cut = test.Context.Render<ToastLabelHost>(p => Parts(p, old: true, @new: false));
        Assert.Equal("old-part", cut.Find("[data-testid=root]").GetAttribute("aria-describedby"));

        cut.Render(p => Parts(p, old: true, @new: true));
        Assert.Equal("new-part", cut.Find("[data-testid=root]").GetAttribute("aria-describedby"));

        cut.Render(p => Parts(p, old: false, @new: true));
        Assert.Equal("new-part", cut.Find("[data-testid=root]").GetAttribute("aria-describedby"));

        static void Parts(ComponentParameterCollectionBuilder<ToastLabelHost> p, bool old, bool @new) =>
            p.Add(x => x.Description, true).Add(x => x.Form, ToastLabelForm.OldAndNew).Add(x => x.ShowOld, old).Add(x => x.ShowNew, @new);
    }

    [Fact(DisplayName = "keeps dynamic title and description ids synchronized with mounted label parts")]
    [Trait("Requirement", "TST-API-15")]
    [Trait("Requirement", "TST-ARIA-04")]
    [Trait("Requirement", "TST-ARIA-05")]
    public void Keeps_dynamic_title_and_description_ids_synchronized_with_mounted_label_parts()
    {
        ToastObject toast = new() { Id = "toast", Title = "Toast title", Description = "Toast description" };
        IRenderedComponent<ToastLabelsHost> cut = test.Context.Render<ToastLabelsHost>(p => p.Add(x => x.Toast, toast).Add(x => x.Mode, "fallback"));
        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal(cut.Find("[data-testid=title]").Id, root.GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find("[data-testid=description]").Id, root.GetAttribute("aria-describedby"));

        cut.Render(p => p.Add(x => x.Toast, toast).Add(x => x.Mode, "explicit"));
        root = cut.Find("[data-testid=root]");
        Assert.Equal(cut.Find("[data-testid=title]").Id, root.GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find("[data-testid=description]").Id, root.GetAttribute("aria-describedby"));
        Assert.Equal("Explicit title", cut.Find("[data-testid=title]").TextContent);
        Assert.Equal("Explicit description", cut.Find("[data-testid=description]").TextContent);

        cut.Render(p => p.Add(x => x.Toast, toast).Add(x => x.Mode, "none"));
        root = cut.Find("[data-testid=root]");
        Assert.False(root.HasAttribute("aria-labelledby"));
        Assert.False(root.HasAttribute("aria-describedby"));

        cut.Render(p => p.Add(x => x.Toast, toast).Add(x => x.Mode, "restored"));
        root = cut.Find("[data-testid=root]");
        Assert.Equal(cut.Find("[data-testid=title]").Id, root.GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find("[data-testid=description]").Id, root.GetAttribute("aria-describedby"));
        Assert.Equal("Toast title", cut.Find("[data-testid=title]").TextContent);
        Assert.Equal("Toast description", cut.Find("[data-testid=description]").TextContent);
    }

    // useToastLabelPart.test.tsx's addToastFromManager: the List and a button that adds `{ title, description }`.
    private IRenderedComponent<ToastHost> AddToastFromManager()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title", Description = "description" }));
        return cut;
    }

    private IRenderedComponent<ToastLabelHost> Label(bool description, ToastLabelForm form, ToastObject? toast = null, RenderFragment? children = null, string? partId = null) =>
        test.Context.Render<ToastLabelHost>(p =>
        {
            p.Add(x => x.Description, description).Add(x => x.Form, form).Add(x => x.Children, children).Add(x => x.PartId, partId);
            if (toast is not null)
            {
                p.Add(x => x.Toast, toast);
            }
        });
}
