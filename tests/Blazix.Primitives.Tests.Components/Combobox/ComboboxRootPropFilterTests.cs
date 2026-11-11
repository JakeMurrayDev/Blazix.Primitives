using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: filter` titles cited at L2. Combobox.useFilter({ value }) is ComboboxFilter.Create(value),
// created again at each consumer render as the hook's memo follows `value`; the component's setValue is the harness's own
// render after OnValueChange (the callback's receiver), with the new value and filter in its parameters.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: filter</c> block.</summary>
public sealed class ComboboxRootPropFilterTests : IDisposable
{
    private static readonly Movie[] s_movies =
    [
        new(1, "Spirited Away", "Sen to Chihiro no Kamikakushi"),
        new(2, "My Neighbor Totoro", "Tonari no Totoro"),
        new(3, "Princess Mononoke", "Mononoke Hime"),
    ];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "resets filtered results after selecting when using a custom search stringifier")]
    [Trait("Requirement", "CMB-API-14")]
    public async Task Resets_filtered_results_after_selecting_when_using_a_custom_search_stringifier()
    {
        static string StringifyMovie(Movie? movie) => movie is null ? string.Empty : $"{movie.English} {movie.Romaji}";

        Dictionary<string, object?> parameters = new()
        {
            ["Items"] = (IReadOnlyList<Movie>)s_movies,
            ["ItemToStringLabel"] = (Func<Movie, string>)(movie => movie.English),
            ["OnItemHighlighted"] = default(EventCallback<ComboboxItemHighlightedEventArgs<Movie>>),
        };
        void SetValue(Movie? value)
        {
            ComboboxFilter<Movie> contains = ComboboxFilter.Create(value);
            parameters["Value"] = value;
            parameters["Filter"] = (Func<Movie, string, Func<Movie, string>?, bool>)((item, query, _) => item is not null && contains.Contains(item, query, StringifyMovie));
        }

        SetValue(null);
        IRenderedComponent<ComboboxTypedHarness<Movie>> cut = test.Context.Render<ComboboxTypedHarness<Movie>>(p => p
            .Add(x => x.RootParameters, parameters)
            .Add(x => x.ItemText, movie => movie.English));
        cut.Instance.OnValue = args => SetValue(args.Value);

        await cut.PressInputAsync(test);
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));

        await cut.TypeAsync(test, "tonari");
        Assert.False(cut.HasOption("Spirited Away"));

        await cut.ClickOptionAsync("My Neighbor Totoro");
        await cut.FinishExitAsync();
        Assert.Empty(cut.FindAll("[role=listbox]"));

        await cut.PressInputAsync(test);
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));

        Assert.True(cut.HasOption("Spirited Away"));
    }

    private sealed record Movie(int Id, string English, string Romaji);
}
