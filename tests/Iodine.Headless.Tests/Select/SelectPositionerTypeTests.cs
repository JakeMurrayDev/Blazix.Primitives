using System.Reflection;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The Select's type-level check at L1 (select/positioner/SelectPositioner.spec.tsx; SEL-API-36): the positioner takes no
/// KeepMounted parameter, as the Tooltip, PreviewCard and Popover positioners do not; the scroll arrows' and the item
/// indicator's are read.
/// </summary>
public sealed class SelectPositionerTypeTests
{
    [Fact(DisplayName = "port: the positioner declares no KeepMounted parameter; the scroll arrows' and the item indicator's are read")]
    [Trait("Requirement", "SEL-API-36")]
    public void The_positioner_declares_no_KeepMounted_parameter()
    {
        Assert.NotNull(typeof(SelectScrollUpArrow).GetProperty(nameof(SelectScrollUpArrow.KeepMounted))?.GetCustomAttribute<ParameterAttribute>());
        Assert.NotNull(typeof(SelectScrollDownArrow).GetProperty(nameof(SelectScrollDownArrow.KeepMounted))?.GetCustomAttribute<ParameterAttribute>());
        Assert.NotNull(typeof(SelectItemIndicator).GetProperty(nameof(SelectItemIndicator.KeepMounted))?.GetCustomAttribute<ParameterAttribute>());

        Assert.Null(typeof(SelectPositioner).GetProperty("KeepMounted"));
    }
}
