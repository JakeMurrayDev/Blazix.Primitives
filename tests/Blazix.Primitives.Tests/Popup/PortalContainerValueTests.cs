using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="PortalContainerValue"/> at L1: the portal's <c>container</c> (FLT-API-13, PSH §6.1) as a hand-written union
/// of an element, a selector, a node a consumer's script holds, and <see cref="PortalContainerValue.Pending"/> for Base
/// UI's <c>null</c> (ADR 04 Amendments 2026-10-27, decision 5; ADR 08 Amendments 2026-10-27).
/// </summary>
public sealed class PortalContainerValueTests
{
    [Fact(DisplayName = "holds an element, a selector, a script-held node or the pending case, and reads each without the others")]
    public void Holds_an_element_a_selector_a_script_held_node_or_the_pending_case_and_reads_each_without_the_others()
    {
        ElementReference element = new("element-1");
        ScriptHeldNode node = new();

        PortalContainerValue byElement = element;
        PortalContainerValue bySelector = "#target";
        PortalContainerValue byNode = node;
        PortalContainerValue pending = PortalContainerValue.Pending;

        Assert.True(byElement.TryGetValue(out ElementReference heldElement));
        Assert.Equal("element-1", heldElement.Id);
        Assert.False(byElement.TryGetValue(out string? _));
        Assert.True(bySelector.TryGetValue(out string? selector));
        Assert.Equal("#target", selector);
        Assert.False(bySelector.TryGetValue(out IJSObjectReference? _));
        Assert.True(byNode.TryGetValue(out IJSObjectReference? heldNode));
        Assert.Same(node, heldNode);
        Assert.False(byNode.IsPending);
        Assert.True(pending.IsPending);
        Assert.True(pending.TryGetValue(out PortalContainerPending _));
        Assert.False(pending.TryGetValue(out ElementReference _));
        Assert.IsType<PortalContainerPending>(pending.Value);
        Assert.All([byElement, bySelector, byNode, pending], value => Assert.True(value.HasValue));
    }

    [Fact(DisplayName = "the default instance holds nothing and is not the pending case")]
    public void The_default_instance_holds_nothing_and_is_not_the_pending_case()
    {
        PortalContainerValue pending = PortalContainerValue.Pending;
        PortalContainerValue none = default;

        Assert.True(pending.HasValue);
        Assert.False(none.HasValue);
        Assert.False(none.IsPending);
        Assert.Null(none.Value);
        Assert.False(none.TryGetValue(out string? _));
    }

    [Fact(DisplayName = "a switch over the case types matches each case")]
    public void A_switch_over_the_case_types_matches_each_case()
    {
        static string Describe(PortalContainerValue container) => container switch
        {
            ElementReference e => "element " + e.Id,
            string s => "selector " + s,
            IJSObjectReference => "script-held node",
            PortalContainerPending => "pending",
        };

        Assert.Equal("element e", Describe(new ElementReference("e")));
        Assert.Equal("selector main", Describe("main"));
        Assert.Equal("script-held node", Describe(new PortalContainerValue(new ScriptHeldNode())));
        Assert.Equal("pending", Describe(PortalContainerValue.Pending));
    }

    /// <summary>A node a consumer's script holds, such as a closed shadow root.</summary>
    private sealed class ScriptHeldNode : IJSObjectReference
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}
