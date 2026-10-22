using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SwitchThumb.test.tsx and the state shape it provides (test context, lines 6-16).
/// <summary>The thumb: the root's state, and the error outside a root.</summary>
public sealed class SwitchThumbTests : IDisposable
{
    private readonly SwitchTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Switch.Root>")]
    [Trait("Requirement", "SWT-EDGE-01")]
    [Trait("Requirement", "SWT-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_SwitchRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<SwitchThumb>());

        Assert.Equal("Blazix: The `SwitchRoot` context is missing. Switch parts must be placed within `<SwitchRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: the state has exactly the nine fields, the same on the root and the thumb")]
    [Trait("Requirement", "SWT-STA-04")]
    [Trait("Requirement", "SWT-STA-08")]
    public void Port_the_state_has_exactly_the_nine_fields()
    {
        SwitchRootState? rootState = null;
        SwitchThumbState? thumbState = null;
        test.Context.Render<SwitchRoot>(p => p
            .Add(x => x.DefaultChecked, true)
            .Add(x => x.Required, true)
            .Add(x => x.ClassValue, (Func<SwitchRootState, string?>)(s => (rootState = s) is null ? null : null))
            .AddChildContent<SwitchThumb>(t => t.Add(x => x.ClassValue, (Func<SwitchThumbState, string?>)(s => (thumbState = s) is null ? null : null))));

        Assert.Equal(new SwitchRootState(Checked: true, Disabled: false, ReadOnly: false, Required: true, Touched: false, Dirty: false, Valid: null, Filled: false, Focused: false), rootState);
        Assert.Equal(new SwitchThumbState(Checked: true, Disabled: false, ReadOnly: false, Required: true, Touched: false, Dirty: false, Valid: null, Filled: false, Focused: false), thumbState);
        Assert.Equal(
            ["Checked", "Disabled", "ReadOnly", "Required", "Touched", "Dirty", "Valid", "Filled", "Focused"],
            typeof(SwitchThumbState).GetProperties().Where(p => p.DeclaringType == typeof(SwitchThumbState) && p.Name != "EqualityContract").Select(p => p.Name));
    }

    [Fact(DisplayName = "port: the thumbs follow a toggle in the same render as the root")]
    [Trait("Requirement", "SWT-DOM-06")]
    public async Task Port_the_thumbs_follow_a_toggle_in_the_same_render_as_the_root()
    {
        IRenderedComponent<SwitchRoot> cut = test.Context.Render<SwitchRoot>(p => p
            .AddChildContent((RenderFragment)(builder =>
            {
                builder.OpenComponent<SwitchThumb>(0);
                builder.AddComponentParameter(1, "data-testid", "a");
                builder.CloseComponent();
                builder.OpenElement(2, "i");
                builder.OpenComponent<SwitchThumb>(3);
                builder.AddComponentParameter(4, "data-testid", "b");
                builder.CloseComponent();
                builder.CloseElement();
            })));

        await test.ToggleAsync(cut, true);

        Assert.True(cut.Find("[data-testid=a]").HasAttribute("data-checked"));
        Assert.True(cut.Find("[data-testid=b]").HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "port: the data attribute names of both parts")]
    [Trait("Requirement", "SWT-DOM-02")]
    [Trait("Requirement", "SWT-DOM-06")]
    public void Port_the_data_attribute_names_of_both_parts()
    {
        string[] names = ["data-checked", "data-unchecked", "data-disabled", "data-readonly", "data-required", "data-valid", "data-invalid", "data-touched", "data-dirty", "data-filled", "data-focused"];

        Assert.Equal(names, Values(typeof(SwitchRootDataAttributes)));
        Assert.Equal(names, Values(typeof(SwitchThumbDataAttributes)));
    }

    private static IEnumerable<string> Values(Type type) => type.GetFields().Select(f => (string)f.GetRawConstantValue()!);
}
