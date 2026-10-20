using System.Reflection;
using System.Text.Json;

namespace Blazix.Primitives.Tests;

/// <summary>
/// The build-level contracts of ADR 09 §3, ADR 10 §2 and ADR 16 rule 1. Reflection is fine in a test;
/// the rule against it binds the library.
/// </summary>
public sealed class AssemblyContractTests
{
    // Prefixes (case-insensitive) of packages that would turn L1 into a layer above it: the bUnit
    // renderer, the in-process web host, the browser driver and the WebAssembly client runtime.
    private static readonly string[] forbiddenPackagePrefixes =
    [
        "bunit",
        "Microsoft.AspNetCore.Mvc.Testing",
        "Microsoft.AspNetCore.Components.WebAssembly",
        "Microsoft.Playwright",
    ];

    private static readonly Assembly library = Assembly.Load("Blazix.Primitives");

    [Fact]
    public void Library_is_marked_trimmable()
    {
        Assert.Equal("True", MetadataValue(library, "IsTrimmable"));
    }

    [Fact]
    public void Library_is_marked_aot_compatible()
    {
        Assert.Equal("True", MetadataValue(library, "IsAotCompatible"));
    }

    [Fact]
    public void Library_records_the_Base_UI_version_it_ports()
    {
        Assert.Equal("v1.9.0", MetadataValue(library, "BaseUIVersion"));
    }

    [Fact]
    public void L1_test_assembly_depends_on_neither_bunit_nor_a_blazor_test_host()
    {
        string depsFile = Path.Combine(AppContext.BaseDirectory, "Blazix.Primitives.Tests.deps.json");
        using JsonDocument deps = JsonDocument.Parse(File.ReadAllText(depsFile));

        string[] libraries = deps.RootElement
            .GetProperty("libraries")
            .EnumerateObject()
            .Select(library => library.Name)
            .ToArray();

        string[] offenders = libraries
            .Where(name => forbiddenPackagePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Concat(libraries.Where(name => name.StartsWith("Blazix.Primitives.Fixtures", StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string? MetadataValue(Assembly assembly, string key)
    {
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)
            ?.Value;
    }
}
