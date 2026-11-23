using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-API-34 at L2 (ComboboxRoot.test.tsx, describe "prop: isItemEqualToValue"): the comparator decides the selected state
// and the toggle of object values, takes the item first, and is never called with a missing value. The values are a
// reference type compared by reference by default, as Object.is compares Base UI's objects, so only the comparator matches
// two equal-looking instances.
/// <summary>ComboboxRoot's IsItemEqualToValue at L2.</summary>
public sealed class ComboboxRootIsItemEqualToValueTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "matches object values using the provided comparator")]
    [Trait("Requirement", "CMB-API-34")]
    public void Matches_object_values_using_the_provided_comparator()
    {
        IRenderedComponent<ComboboxTypedHarness<User>> cut = Render(new()
        {
            ["Items"] = Users(),
            ["Value"] = new User(2, "Bob"),
            ["ItemToStringLabel"] = (Func<User, string>)(item => item.Name),
            ["ItemToStringValue"] = (Func<User, string>)(item => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ["IsItemEqualToValue"] = (Func<User, User, bool>)((item, value) => item.Id == value.Id),
            ["DefaultOpen"] = true,
        }, withValue: true);

        Assert.Equal("Bob", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("true", cut.Option("Bob").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "properly deselects object values using the provided comparator")]
    [Trait("Requirement", "CMB-API-34")]
    public async Task Properly_deselects_object_values_using_the_provided_comparator()
    {
        IRenderedComponent<ComboboxTypedHarness<User>> cut = Render(new()
        {
            ["Items"] = Users(),
            ["DefaultValues"] = (IReadOnlyList<User>)[new User(2, "Bob")],
            ["ItemToStringLabel"] = (Func<User, string>)(item => item.Name),
            ["ItemToStringValue"] = (Func<User, string>)(item => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ["IsItemEqualToValue"] = (Func<User, User, bool>)((item, value) => item.Id == value.Id),
            ["DefaultOpen"] = true,
            ["Multiple"] = true,
        }, withValue: true);

        await cut.ClickOptionAsync("Bob");

        Assert.Equal("false", cut.Option("Bob").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "passes item as the first comparator argument in multiple mode")]
    [Trait("Requirement", "CMB-API-34")]
    public async Task Passes_item_as_the_first_comparator_argument_in_multiple_mode()
    {
        IRenderedComponent<ComboboxTypedHarness<User>> cut = Render(new()
        {
            ["Items"] = Users(),
            ["DefaultValues"] = (IReadOnlyList<User>)[new User(2, "Bob", "selected")],
            ["ItemToStringLabel"] = (Func<User, string>)(item => item.Name),
            ["ItemToStringValue"] = (Func<User, string>)(item => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ["IsItemEqualToValue"] = (Func<User, User, bool>)((item, value) => item.Id == value.Id && item.Source == "item" && value.Source == "selected"),
            ["DefaultOpen"] = true,
            ["Multiple"] = true,
        });
        Assert.Equal("true", cut.Option("Bob").GetAttribute("aria-selected"));

        await cut.ClickOptionAsync("Bob");

        Assert.Equal("false", cut.Option("Bob").GetAttribute("aria-selected"));
    }

    // Base UI's comparator throws on a null value; here it records every call, and the assertion is the same.
    [Fact(DisplayName = "does not call comparator with null when clearing the value")]
    [Trait("Requirement", "CMB-API-34")]
    public async Task Does_not_call_comparator_with_null_when_clearing_the_value()
    {
        IReadOnlyList<User> users = Users();
        List<(User? Item, User? Value)> calls = [];
        RenderFragment clear = builder =>
        {
            builder.OpenComponent<ComboboxClear>(0);
            builder.AddComponentParameter(1, "data-testid", "clear");
            builder.AddComponentParameter(2, nameof(ComboboxClear.KeepMounted), true);
            builder.CloseComponent();
        };
        IRenderedComponent<ComboboxTypedHarness<User>> cut = test.Context.Render<ComboboxTypedHarness<User>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = users,
                ["DefaultValue"] = users[0],
                ["ItemToStringLabel"] = (Func<User, string>)(item => item.Name),
                ["ItemToStringValue"] = (Func<User, string>)(item => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ["IsItemEqualToValue"] = (Func<User, User, bool>)((item, value) =>
                {
                    calls.Add((item, value));
                    return value is not null && item.Id == value.Id;
                }),
            })
            .Add(x => x.WithInput, false)
            .Add(x => x.WithTrigger, true)
            .Add(x => x.WithValue, true)
            .Add(x => x.ItemText, item => item.Name)
            .Add(x => x.Extra, clear));

        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click());

        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
        Assert.Contains("\"value\":\"\"", LastFormControlOptions(), StringComparison.Ordinal);
        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.NotNull(call.Value));
    }

    [Fact(DisplayName = "does not call comparator with undefined when items load asynchronously after opening")]
    [Trait("Requirement", "CMB-API-34")]
    public async Task Does_not_call_comparator_with_undefined_when_items_load_asynchronously_after_opening()
    {
        Country[] loaded = [new("ca", "Canada"), new("us", "United States")];
        List<(Country? Item, Country? Value)> calls = [];
        Dictionary<string, object?> Parameters(IReadOnlyList<Country>? items)
        {
            Dictionary<string, object?> parameters = new()
            {
                ["Value"] = loaded[0],
                ["InputValue"] = string.Empty,
                ["OnInputValueChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, _ => { }),
                ["ItemToStringLabel"] = (Func<Country, string>)(item => item.Label),
                ["IsItemEqualToValue"] = (Func<Country, Country, bool>)((item, value) =>
                {
                    calls.Add((item, value));
                    return item is not null && value is not null && item.Code == value.Code;
                }),
                ["Filter"] = null,
            };
            if (items is not null)
            {
                parameters["Items"] = items;
            }

            return parameters;
        }

        IRenderedComponent<ComboboxTypedHarness<Country>> cut = test.Context.Render<ComboboxTypedHarness<Country>>(p => p
            .Add(x => x.RootParameters, Parameters(null))
            .Add(x => x.ItemText, item => item.Label));
        await test.PressAsync(cut, "[data-testid=input]", "input-press");

        cut.Render(p => p.Add(x => x.RootParameters, Parameters(loaded)));
        await test.EchoAsync(cut);
        Assert.NotNull(cut.Option("Canada"));
        await test.HighlightAsync(cut, 0, ComboboxRootBatchCSteps.MouseMove);
        Assert.Equal("Canada", cut.HighlightedText(cut.Instance.Root!.Store));
        await test.HighlightAsync(cut, 1, ComboboxRootBatchCSteps.ArrowDown);

        Assert.Equal("United States", cut.HighlightedText(cut.Instance.Root!.Store));
        Assert.NotEmpty(calls);
        Assert.All(calls, call =>
        {
            Assert.NotNull(call.Item);
            Assert.NotNull(call.Value);
        });
    }

    private static IReadOnlyList<User> Users() => [new User(1, "Alice"), new User(2, "Bob")];

    private string LastFormControlOptions()
    {
        string? options = null;
        foreach (JSRuntimeInvocation attach in test.Form.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.FormHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options ?? string.Empty;
    }

    private IRenderedComponent<ComboboxTypedHarness<User>> Render(Dictionary<string, object?> root, bool withValue = false) =>
        test.Context.Render<ComboboxTypedHarness<User>>(p => p
            .Add(x => x.RootParameters, root)
            .Add(x => x.WithValue, withValue)
            .Add(x => x.ItemText, item => item.Name));

    // A class, not a record: Base UI's objects are equal only by reference unless the comparator says otherwise.
    private sealed class User(int id, string name, string source = "item")
    {
        public int Id { get; } = id;

        public string Name { get; } = name;

        public string Source { get; } = source;
    }

    private sealed class Country(string code, string label)
    {
        public string Code { get; } = code;

        public string Label { get; } = label;
    }
}
