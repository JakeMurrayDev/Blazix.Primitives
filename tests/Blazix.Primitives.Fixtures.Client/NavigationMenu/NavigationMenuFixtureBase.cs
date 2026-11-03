using System.Globalization;
using Blazix.Primitives.Fixtures.Client.PopupFixtures;

namespace Blazix.Primitives.Fixtures.Client.NavigationMenuFixtures;

/// <summary>
/// The base of the <c>navigation-menu-*</c> fixtures (M5 track 5; ADR 06 Amendments 2026-10-27): the popup suite base with
/// the Root's <c>OnValueChange</c> recorded as <c>value:reason</c> entries (<c>item-1:trigger-hover null:escape-key</c>,
/// a falsy value as <c>""</c>) for the container's <c>data-value-changes</c>, and the query's cancel and opt-out.
/// </summary>
public abstract class NavigationMenuFixtureBase : PopupSuiteFixtureBase
{
    private readonly List<string> valueChanges = [];

    /// <summary>Gets the recorded value changes as the container renders them.</summary>
    protected string ValueChanges => string.Join(' ', valueChanges);

    /// <summary>Records a value change; <c>?cancel=true</c> cancels it, <c>?retain=true</c> keeps a close mounted until Unmount.</summary>
    /// <typeparam name="TValue">The type of the Root's value.</typeparam>
    /// <param name="args">The change.</param>
    protected void RecordValueChange<TValue>(ValueChangeEventArgs<TValue?> args)
    {
        string value = args.Value is null ? "null" : Convert.ToString(args.Value, CultureInfo.InvariantCulture) is { Length: > 0 } text ? text : "\"\"";
        valueChanges.Add(value + ":" + args.Details.Reason.ToLiteral());
        if (QueryFlag("cancel") == true)
        {
            args.Details.Cancel();
        }

        if (args.Value is null && QueryFlag("retain") == true && args.Details is OpenChangeEventDetails details)
        {
            details.PreventUnmountOnClose();
        }

        StateHasChanged();
    }
}
