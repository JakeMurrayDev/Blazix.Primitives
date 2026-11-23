using AngleSharp.Dom;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// L2 titles of the toast positioner and arrow (TST-STR-10, TST-STR-11, TST-POS-01, TST-POS-02, TST-ARIA-08, TST-ARIA-09,
/// TST-EDGE-01, TST-EDGE-03; ToastPositioner.test.tsx, ToastArrow.test.tsx). The placement the `positioning` module computes
/// is L4 (tests/browser/toast.positioner.spec.ts); here, what C# renders before the attach.
/// </summary>
public sealed class ToastPositionerTests : IDisposable
{
    private readonly ToastTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Toast.Provider>")]
    [Trait("Requirement", "TST-STR-10")]
    [Trait("Requirement", "TST-EDGE-01")]
    public void Throws_a_descriptive_error_when_rendered_outside_ToastProvider()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            test.Context.Render<ToastPositioner<NoPayload>>(p => p.Add(x => x.Toast, new ToastObject { Id = "t" })));

        Assert.Equal("Iodine: `ToastProviderContext` is missing. Toast parts must be used within `<ToastProvider>`.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Toast.Positioner>")]
    [Trait("Requirement", "TST-STR-11")]
    [Trait("Requirement", "TST-EDGE-03")]
    public void Throws_a_descriptive_error_when_rendered_outside_ToastPositioner()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ToastArrow>());

        Assert.Equal("Iodine: `ToastPositionerContext` is missing. `ToastPositioner` parts must be placed within `<ToastPositioner>`.", error.Message);
    }

    [Fact(DisplayName = "lets positioner props override the ones carried on the toast")]
    [Trait("Requirement", "TST-POS-02")]
    public void Lets_positioner_props_override_the_ones_carried_on_the_toast()
    {
        ToastObject toast = new() { Id = "t", Positioner = new ToastPositionerSettings { Side = Side.Bottom, Align = Align.End } };
        test.Context.RenderTree.Add<ToastProvider<NoPayload>>();

        IRenderedComponent<ToastPositioner<NoPayload>> cut = test.Context.Render<ToastPositioner<NoPayload>>(p => p
            .Add(x => x.Toast, toast)
            .Add(x => x.Side, Side.Left)
            .Add(x => x.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "positioner" }));

        IElement positioner = cut.Find("[data-testid=positioner]");
        Assert.Equal("left", positioner.GetAttribute("data-side"));
        Assert.Equal("end", positioner.GetAttribute("data-align"));
    }

    [Fact(DisplayName = "port: the positioner falls back to top and center, renders role presentation and the toast's index")]
    [Trait("Requirement", "TST-POS-01")]
    [Trait("Requirement", "TST-ARIA-09")]
    [Trait("Requirement", "TST-DOM-21")]
    public void The_positioner_falls_back_to_top_and_center_renders_role_presentation_and_the_toasts_index()
    {
        test.Context.RenderTree.Add<ToastProvider<NoPayload>>();

        IRenderedComponent<ToastPositioner<NoPayload>> cut = test.Context.Render<ToastPositioner<NoPayload>>(p => p
            .Add(x => x.Toast, new ToastObject { Id = "t" })
            .AddChildContent<ToastArrow>(a => a.Add(x => x.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "arrow" })));

        IElement positioner = cut.Find("[role=presentation]");
        Assert.Equal("top", positioner.GetAttribute("data-side"));
        Assert.Equal("center", positioner.GetAttribute("data-align"));
        Assert.Contains("--toast-index: -1", positioner.GetAttribute("style"), StringComparison.Ordinal);
        // TST-ARIA-08.
        Assert.Equal("true", cut.Find("[data-testid=arrow]").GetAttribute("aria-hidden"));
    }
}
