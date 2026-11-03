using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The value-shape probe's permanent evidence at L2 (ADR 08 Amendments 2026-11-03 (1), the probe's finding of 2026-11-04):
/// one Razor host binds both triples and an item on the test-only list roots, compiled by the Razor SDK as a consumer's
/// markup is, and the library's mismatch check rejects the triple <c>Multiple</c> does not choose.
/// </summary>
public sealed class ListValueBindingTests : IDisposable
{
    private readonly BunitContext context = new();

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: the list roots infer TValue from @bind-Value and @bind-Values, supply both expressions, and cascade it to their items")]
    [Trait("Requirement", "SEL-API-14")]
    public async Task The_list_roots_infer_TValue_from_both_binds_supply_both_expressions_and_cascade_it_to_their_items()
    {
        IRenderedComponent<ListValueBindingHost> cut = context.Render<ListValueBindingHost>();

        TestListRoot<string?> single = cut.Instance.SingleRoot!;
        TestListRoot<string> multiple = cut.Instance.MultipleRoot!;
        Assert.Equal("a", single.Value);
        Assert.NotNull(single.ValueExpression);
        Assert.Equal(["b"], multiple.Values!);
        Assert.NotNull(multiple.ValuesExpression);

        // The items two wrappers deep took the root's TValue and found it.
        Assert.Equal(2, cut.FindComponents<TestListItem<string?>>().Count(item => ReferenceEquals(item.Instance.Root, single)));
        Assert.Single(cut.FindComponents<TestListItem<string>>(), item => ReferenceEquals(item.Instance.Root, multiple));
        Assert.Equal(["a", "omitted", "b", "omitted", "0"], cut.FindAll("[data-list-item]").Select(element => element.GetAttribute("data-list-item")));
        Assert.False(cut.Instance.OmittedItem!.ValueSupplied);
        Assert.True(cut.Instance.ZeroItem!.ValueSupplied);

        // Both binds write the consumer's fields.
        await cut.InvokeAsync(() => single.ValueChanged.InvokeAsync("c"));
        await cut.InvokeAsync(() => multiple.ValuesChanged.InvokeAsync(["b", "d"]));
        Assert.Equal("c", cut.Instance.Selected);
        Assert.Equal(["b", "d"], cut.Instance.SelectedValues);
    }

    [Fact(DisplayName = "port: a one-way Values takes a List<T>, an array and a collection expression, each inferring TValue")]
    [Trait("Requirement", "SEL-API-14")]
    public void A_one_way_Values_takes_a_list_an_array_and_a_collection_expression()
    {
        IRenderedComponent<ListValueBindingHost> cut = context.Render<ListValueBindingHost>();

        Assert.Equal(["l"], cut.Instance.ListRoot!.Values!);
        Assert.Equal(["a1", "a2"], cut.Instance.ArrayRoot!.Values!);
        Assert.Equal(["x", "y"], cut.Instance.ExpressionRoot!.Values!);
    }

    [Fact(DisplayName = "port: a list root throws when Multiple is true and a single-value parameter is supplied")]
    [Trait("Requirement", "SEL-API-14")]
    public void A_list_root_throws_when_Multiple_is_true_and_a_single_value_parameter_is_supplied()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => context.Render<TestListRoot<string>>(parameters => parameters
            .Add(root => root.Multiple, true)
            .Add(root => root.Value, "a")));

        Assert.Equal("Blazix: `TestListRoot` received `Value` while `Multiple` is true. In multiple mode, use `Values`, `ValuesChanged` (or `@bind-Values`), `DefaultValues` and `OnValuesChange`.", exception.Message);
    }

    [Fact(DisplayName = "port: a list root throws when Multiple is false and a multiple-value parameter is supplied")]
    [Trait("Requirement", "SEL-API-14")]
    public void A_list_root_throws_when_Multiple_is_false_and_a_multiple_value_parameter_is_supplied()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => context.Render<TestListRoot<string>>(parameters => parameters
            .Add(root => root.OnValuesChange, EventCallback.Factory.Create<ValueChangeEventArgs<IReadOnlyList<string>>>(this, _ => { }))));

        Assert.Equal("Blazix: `TestListRoot` received `OnValuesChange` while `Multiple` is false. Set `Multiple=\"true\"`, or use `Value`, `ValueChanged` (or `@bind-Value`), `DefaultValue` and `OnValueChange` for one value.", exception.Message);
    }
}
