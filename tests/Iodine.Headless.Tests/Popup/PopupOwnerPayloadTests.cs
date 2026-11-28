using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Iodine.Headless.Tests;

/// <summary>
/// PSH-STA-30/31 (useTriggerDataForwarding, a layout effect: the commit that makes a trigger the owner writes its payload).
/// Issue 142, design point 10-142a (1), chosen by the user 2026-11-28: the store writes the new owner's recorded payload in
/// the dispatch of the write that makes it the owner, whichever path makes it so (a request's commit, the reconciliation's
/// claim, a controlled trigger id), so no trigger render, and no render acknowledgement on Server, comes between. The
/// triggers here never run their <c>OnAfterRenderAsync</c>, where the port wrote it before.
/// </summary>
public sealed class PopupOwnerPayloadTests
{
    [Fact(DisplayName = "port: a request's commit that hands the popup to another trigger writes that trigger's payload (issue 142)")]
    [Trait("Requirement", "PSH-STA-30")]
    [Trait("Requirement", "PSH-STA-31")]
    public async Task A_requests_commit_writes_the_new_owners_payload()
    {
        TestFamilyStore<string> store = new();
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        using PopupTriggerBehavior<TestFamilyStore<string>, TestFamilyStoreState<string>, string> first = Trigger(store, "trigger-1", "1");
        using PopupTriggerBehavior<TestFamilyStore<string>, TestFamilyStoreState<string>, string> second = Trigger(store, "trigger-2", "2");

        await store.RequestOpenAsync(true, Press("trigger-1"));
        Assert.Equal("1", store.Payload);
        int version = store.Popup.PayloadVersion;

        await store.RequestOpenAsync(true, Press("trigger-2"));

        Assert.Equal("trigger-2", store.Popup.EffectiveActiveTriggerId);
        Assert.Equal("2", store.Payload);
        Assert.Equal(version + 1, store.Popup.PayloadVersion);
    }

    [Fact(DisplayName = "port: the reconciliation's claim of the active trigger's new id writes that trigger's payload (issue 142)")]
    [Trait("Requirement", "PSH-STA-30")]
    [Trait("Requirement", "PSH-STA-20")]
    public async Task The_reconciliations_claim_writes_the_new_owners_payload()
    {
        TestFamilyStore<string> store = new();
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        object owner = new();
        using PopupTriggerBehavior<TestFamilyStore<string>, TestFamilyStoreState<string>, string> trigger = new(owner, "TestTrigger", () => { });
        trigger.Apply(store, null, "trigger-1", null, true, "1", "missing root");
        await store.RequestOpenAsync(true, Press("trigger-1"));
        Assert.Equal("1", store.Payload);

        // The trigger's id and payload change in one parameter set; the element stays, so the batch boundary's
        // reconciliation claims the new id for it (PSH-ALG-03).
        trigger.Apply(store, null, "trigger-1b", null, true, "1b", "missing root");
        await store.AtBatchBoundaryAsync();

        Assert.Equal("trigger-1b", store.Popup.EffectiveActiveTriggerId);
        Assert.Equal("1b", store.Payload);
    }

    [Fact(DisplayName = "port: a controlled trigger id that moves the open popup writes the new trigger's payload (issue 142)")]
    [Trait("Requirement", "PSH-STA-30")]
    [Trait("Requirement", "PSH-STA-31")]
    public void A_controlled_trigger_id_writes_the_new_owners_payload()
    {
        TestFamilyStore<string> store = new();
        SetControlled(store, "trigger-1");
        using PopupTriggerBehavior<TestFamilyStore<string>, TestFamilyStoreState<string>, string> first = Trigger(store, "trigger-1", "1");
        using PopupTriggerBehavior<TestFamilyStore<string>, TestFamilyStoreState<string>, string> second = Trigger(store, "trigger-2", "2");
        Assert.Equal("1", store.Payload);

        SetControlled(store, "trigger-2");

        Assert.Equal("trigger-2", store.Popup.EffectiveActiveTriggerId);
        Assert.Equal("2", store.Payload);
    }

    private static PopupTriggerBehavior<TestFamilyStore<string>, TestFamilyStoreState<string>, string> Trigger(TestFamilyStore<string> store, string id, string payload)
    {
        PopupTriggerBehavior<TestFamilyStore<string>, TestFamilyStoreState<string>, string> behavior = new(new object(), "TestTrigger", () => { });
        behavior.Apply(store, null, id, null, true, payload, "missing root");
        return behavior;
    }

    private static OpenChangeEventDetails Press(string triggerId) => new(EventReason.TriggerPress, trigger: new EventTrigger(triggerId, null));

    // The root's parameter set: Open and TriggerId controlled.
    private static void SetControlled(TestFamilyStore<string> store, string triggerId)
    {
        store.OpenState.SetParameters(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Open"] = true }), "Open", "DefaultOpen");
        store.TriggerIdState.SetParameters(ParameterView.FromDictionary(new Dictionary<string, object?> { ["TriggerId"] = triggerId }), "TriggerId", "DefaultTriggerId");
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
    }
}
