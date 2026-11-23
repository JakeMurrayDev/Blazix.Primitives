using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxLabel.test.tsx (CMB-STR-10; PR 193 delta review B1): the Label's warning about an Input that is the form control is
// a development warning, so a production app (the development warnings switch off) does not run it.
/// <summary>Combobox's Label with the development warnings switch off, at L2.</summary>
[Collection(DevelopmentWarningsOffCollection.Name)]
public sealed class ComboboxLabelProductionTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "does not run the development warning in production")]
    [Trait("Requirement", "CMB-STR-10")]
    public void Does_not_run_the_development_warning_in_production()
    {
        CapturingLogger<ComboboxLabel> logger = new();
        test.Context.Services.AddSingleton<ILogger<ComboboxLabel>>(logger);
        AppContext.SetSwitch("Blazix.Primitives.DevelopmentWarnings", false);
        try
        {
            test.Context.Render<ComboboxHarness>(p => p.Add(x => x.Extra, (RenderFragment)(builder =>
            {
                builder.OpenComponent<ComboboxLabel>(0);
                builder.CloseComponent();
            })));
        }
        finally
        {
            AppContext.SetSwitch("Blazix.Primitives.DevelopmentWarnings", true);
        }

        Assert.Empty(logger.Entries);
    }
}
