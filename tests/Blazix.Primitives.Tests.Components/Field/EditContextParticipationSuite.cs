using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Verification item 5 of ADR 05 (Amendments 2026-10-22, issue 76) at L2, per control: a control inside a cascaded
/// <see cref="EditContext"/> with its expression parameter notifies the context of every committed change, defaults its
/// name to the identifier's field name, feeds the context's messages for its identifier into the field's invalid state,
/// and is inert without the context or the expression. <see cref="FieldControl"/> and <see cref="Input"/> derive it.
/// </summary>
/// <typeparam name="TControl">The type of the control part.</typeparam>
public abstract class EditContextParticipationSuite<TControl> : IDisposable
    where TControl : class, IComponent
{
    private readonly FieldTestContext host = new();
    private readonly Person model = new();
    private readonly EditContext editContext;

    protected EditContextParticipationSuite()
    {
        editContext = new EditContext(model);
    }

    private BunitContext Context => host.Context;

    private FieldIdentifier Email => new(model, nameof(Person.Email));

    /// <inheritdoc />
    public void Dispose()
    {
        host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "notifies the cascaded EditContext of every committed change")]
    [Trait("Verifies", "ADR05-5")]
    public void Notifies_the_cascaded_EditContext_of_every_committed_change()
    {
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        IRenderedComponent<EditContextHost> cut = Render(inField: true);

        host.Module.Control.Input(cut, "a");
        host.Module.Control.Input(cut, "ab");

        Assert.Equal([Email, Email], changed);
        Assert.Equal("ab", model.Email);
        Assert.True(editContext.IsModified(Email));
    }

    [Fact(DisplayName = "notifies the cascaded EditContext outside a field")]
    [Trait("Verifies", "ADR05-5")]
    public void Notifies_the_cascaded_EditContext_outside_a_field()
    {
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;
        IRenderedComponent<EditContextHost> cut = Render(inField: false);

        host.Module.Control.Input(cut, "a");

        Assert.Equal(1, changes);
        Assert.Equal("Email", cut.Find("input").GetAttribute("name"));
    }

    [Fact(DisplayName = "defaults its name to the identifier's field name, and its own name wins")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "FLD-API-18")]
    public void Defaults_its_name_to_the_identifiers_field_name()
    {
        Assert.Equal("Email", Render(inField: true).Find("input").GetAttribute("name"));
        Assert.Equal("contact", Render(inField: true, name: "contact").FindAll("input")[^1].GetAttribute("name"));
    }

    [Fact(DisplayName = "maps the context's messages for its identifier into the field's invalid state")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "FFI-FRM-25")]
    [Trait("Requirement", "FFI-FRM-26")]
    public void Maps_the_contexts_messages_into_the_fields_invalid_state()
    {
        ValidationMessageStore store = new(editContext);
        IRenderedComponent<EditContextHost> cut = Render(inField: true);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-invalid"));

        cut.InvokeAsync(() =>
        {
            store.Add(Email, "The Email field is required.");
            editContext.NotifyValidationStateChanged();
        });
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-invalid"));
        Assert.Equal("true", cut.Find("input").GetAttribute("aria-invalid"));

        cut.InvokeAsync(() =>
        {
            store.Clear();
            editContext.NotifyValidationStateChanged();
        });
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-invalid"));
        Assert.False(cut.Find("input").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "ignores the context's messages for another identifier")]
    [Trait("Verifies", "ADR05-5")]
    public void Ignores_the_contexts_messages_for_another_identifier()
    {
        ValidationMessageStore store = new(editContext);
        IRenderedComponent<EditContextHost> cut = Render(inField: true);

        cut.InvokeAsync(() =>
        {
            store.Add(new FieldIdentifier(model, nameof(Person.Name)), "The Name field is required.");
            editContext.NotifyValidationStateChanged();
        });

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-invalid"));
    }

    [Fact(DisplayName = "is inert without a cascaded EditContext")]
    [Trait("Verifies", "ADR05-5")]
    public void Is_inert_without_a_cascaded_EditContext()
    {
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;
        IRenderedComponent<EditContextHost> cut = Render(inField: true, cascade: false);

        host.Module.Control.Input(cut, "a");

        Assert.Equal(0, changes);
        Assert.False(cut.Find("input").HasAttribute("name"));
    }

    [Fact(DisplayName = "is inert without the expression parameter")]
    [Trait("Verifies", "ADR05-5")]
    public void Is_inert_without_the_expression_parameter()
    {
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;
        ValidationMessageStore store = new(editContext);
        IRenderedComponent<EditContextHost> cut = Render(inField: true, expression: false);

        host.Module.Control.Input(cut, "a");
        cut.InvokeAsync(() =>
        {
            store.Add(Email, "The Email field is required.");
            editContext.NotifyValidationStateChanged();
        });

        Assert.Equal(0, changes);
        Assert.False(cut.Find("input").HasAttribute("name"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-invalid"));
    }

    private IRenderedComponent<EditContextHost> Render(bool inField, bool cascade = true, bool expression = true, string? name = null) =>
        Context.Render<EditContextHost>(p => p
            .Add(x => x.ControlType, typeof(TControl))
            .Add(x => x.Model, model)
            .Add(x => x.EditContext, cascade ? editContext : null)
            .Add(x => x.InField, inField)
            .Add(x => x.Expression, expression)
            .Add(x => x.Name, name));

}

/// <summary>The model the EditContext participation tests bind to.</summary>
public sealed class Person
{
    public string? Email { get; set; }

    public string? Name { get; set; }
}

/// <summary>
/// A consumer that binds a control to <see cref="Person.Email"/> as <c>@bind-Value</c> does: it applies the new value and
/// renders again, inside a cascaded <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/> when one is given.
/// </summary>
public sealed class EditContextHost : ComponentBase
{
    [Parameter]
    public Type ControlType { get; set; } = typeof(FieldControl);

    [Parameter]
    public Person Model { get; set; } = new();

    [Parameter]
    public EditContext? EditContext { get; set; }

    [Parameter]
    public bool InField { get; set; }

    [Parameter]
    public bool Expression { get; set; }

    [Parameter]
    public string? Name { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (EditContext is null)
        {
            Field(builder);
            return;
        }

        builder.OpenComponent<CascadingValue<EditContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<EditContext>.Value), EditContext);
        builder.AddComponentParameter(2, nameof(CascadingValue<EditContext>.ChildContent), (RenderFragment)Field);
        builder.CloseComponent();
    }

    private void Field(RenderTreeBuilder builder)
    {
        if (!InField)
        {
            Control(builder);
            return;
        }

        builder.OpenComponent<FieldRoot>(0);
        builder.AddAttribute(1, "data-testid", "root");
        builder.AddComponentParameter(2, nameof(FieldRoot.ChildContent), (RenderFragment)Control);
        builder.CloseComponent();
    }

    private void Control(RenderTreeBuilder builder)
    {
        builder.OpenComponent(0, ControlType);
        builder.AddComponentParameter(1, "Value", Model.Email);
        builder.AddComponentParameter(2, "ValueChanged", EventCallback.Factory.Create<string?>(this, value => Model.Email = value));
        if (Expression)
        {
            builder.AddComponentParameter(3, "ValueExpression", (Expression<Func<string?>>)(() => Model.Email));
        }

        if (Name is not null)
        {
            builder.AddComponentParameter(4, "Name", Name);
        }

        builder.CloseComponent();
    }
}

/// <summary>The EditContext participation suite over <see cref="FieldControl"/>.</summary>
public sealed class FieldControlEditContextTests : EditContextParticipationSuite<FieldControl>;

/// <summary>The EditContext participation suite over <see cref="Input"/>.</summary>
public sealed class InputEditContextTests : EditContextParticipationSuite<Input>;
