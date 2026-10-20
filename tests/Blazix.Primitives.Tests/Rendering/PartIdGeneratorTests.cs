using Blazix.Primitives.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Rendering;

/// <summary>The generated ids (RND-ALG-11, RND-DOM-27, RND-DOM-31; ADR 02 decision 5), with the [L1] titles of <c>packages/utils/src/useId.test.tsx</c>.</summary>
public sealed class PartIdGeneratorTests
{
    [Fact(DisplayName = "can be prefixed")]
    [Trait("Requirement", "RND-DOM-27")]
    public void Can_be_prefixed()
    {
        PartIdGenerator generator = new();

        string id = generator.Next();

        Assert.Equal("base-ui-", id[..8]);
        Assert.Equal(PartIdGenerator.Prefix, id[..8]);
    }

    [Fact(DisplayName = "can be used in in IDREF attributes")]
    [Trait("Requirement", "RND-DOM-31")]
    public void Can_be_used_in_in_IDREF_attributes()
    {
        PartIdGenerator generator = new();

        string labelPartA = generator.Next();
        string labelPartB = generator.Next();
        string labelledBy = $"{labelPartA} {labelPartB}";

        Assert.NotEqual(labelPartA, labelPartB);
        Assert.DoesNotContain(' ', labelPartA);
        Assert.DoesNotContain(' ', labelPartB);
        Assert.Equal([labelPartA, labelPartB], labelledBy.Split(' '));
    }

    [Fact(DisplayName = "can be suffixed")]
    [Trait("Requirement", "RND-DOM-31")]
    public void Can_be_suffixed()
    {
        PartIdGenerator generator = new();

        string id = generator.Next();
        string labelId = $"{id}-label";

        Assert.NotEqual(id, labelId);
        Assert.StartsWith(id, labelId);
    }

    [Fact(DisplayName = "draws ids in call order from one, per generator")]
    [Trait("Requirement", "RND-DOM-29")]
    public void Draws_ids_in_call_order_from_one_per_generator()
    {
        PartIdGenerator first = new();
        PartIdGenerator second = new();

        Assert.Equal(["base-ui-1", "base-ui-2", "base-ui-3"], [first.Next(), first.Next(), first.Next()]);
        Assert.Equal("base-ui-1", second.Next());
    }

    [Fact(DisplayName = "AddBlazixPrimitives registers the generator per scope")]
    [Trait("Requirement", "RND-DOM-29")]
    public void AddBlazixPrimitives_registers_the_generator_per_scope()
    {
        ServiceCollection services = new();
        services.AddBlazixPrimitives();
        using ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope scopeA = provider.CreateScope();
        using IServiceScope scopeB = provider.CreateScope();
        PartIdGenerator a = scopeA.ServiceProvider.GetRequiredService<PartIdGenerator>();
        PartIdGenerator b = scopeB.ServiceProvider.GetRequiredService<PartIdGenerator>();

        Assert.NotSame(a, b);
        Assert.Same(a, scopeA.ServiceProvider.GetRequiredService<PartIdGenerator>());
        Assert.Equal("base-ui-1", a.Next());
        Assert.Equal("base-ui-1", b.Next());
    }
}
