using System.Reflection;
using Blazix.Primitives.Tests.TestSupport;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// AlertDialog at L1: its parameter lists and handle by reflection (ADL-API-01..06, ADL-EDGE-02, PSH-API-03; the type-level
/// rows of <c>AlertDialogRoot.spec.tsx</c>, re-tagged to L1 reflection by the plan's §Settled), and its handle's
/// diagnostics (the AlertDialog rows of <c>popupHandle.test.ts</c>, whose warnings name <c>DialogHandle</c>: ADL-EDGE-01,
/// PSH-EDGE-02). As in the shared handle's titles, the handle warns through the logger of the root it last attached to, so
/// a root attaches and detaches first.
/// </summary>
public sealed class AlertDialogApiTests
{
    private static readonly string[] s_removedRootParameters = ["Modal", "DisablePointerDismissal"];

    [Fact(DisplayName = "port: AlertDialogRoot declares DialogRoot's parameters and actions except Modal and DisablePointerDismissal, with the same types and defaults")]
    [Trait("Requirement", "ADL-API-01")]
    [Trait("Requirement", "ADL-API-02")]
    [Trait("Requirement", "ADL-API-03")]
    public void AlertDialogRoot_declares_DialogRoots_parameters_except_Modal_and_DisablePointerDismissal()
    {
        Dictionary<string, PropertyInfo> dialog = Parameters(typeof(DialogRoot<int>));
        Dictionary<string, PropertyInfo> alert = Parameters(typeof(AlertDialogRoot<int>));

        Assert.Subset(dialog.Keys.ToHashSet(), s_removedRootParameters.ToHashSet());
        Assert.Equal(dialog.Keys.Except(s_removedRootParameters).Order(StringComparer.Ordinal), alert.Keys.Order(StringComparer.Ordinal));
        AssertSameTypesAndDefaults(dialog, alert, new DialogRoot<int>(), new AlertDialogRoot<int>());

        Assert.Equal(typeof(Task), typeof(AlertDialogRoot<int>).GetMethod("CloseAsync", Type.EmptyTypes)?.ReturnType);
        Assert.Equal(typeof(void), typeof(AlertDialogRoot<int>).GetMethod("Unmount", Type.EmptyTypes)?.ReturnType);
        Assert.True(typeof(AlertDialogRoot<int>).IsSealed);
    }

    [Fact(DisplayName = "port: AlertDialogTrigger declares DialogTrigger's parameters with the same types and defaults")]
    [Trait("Requirement", "ADL-API-05")]
    public void AlertDialogTrigger_declares_DialogTriggers_parameters()
    {
        Dictionary<string, PropertyInfo> dialog = Parameters(typeof(DialogTrigger<int>));
        Dictionary<string, PropertyInfo> alert = Parameters(typeof(AlertDialogTrigger<int>));

        Assert.Equal(dialog.Keys.Order(StringComparer.Ordinal), alert.Keys.Order(StringComparer.Ordinal));
        AssertSameTypesAndDefaults(dialog, alert, new DialogTrigger<int>(), new AlertDialogTrigger<int>());
        Assert.Equal(typeof(int), alert["Payload"].PropertyType);
        Assert.True(typeof(AlertDialogTrigger<int>).IsSealed);
    }

    // ADL §14: in a nominally typed language the families are separated at compile time, one way. C# rejects a DialogHandle on
    // the AlertDialog parts and accepts an AlertDialogHandle on the Dialog parts, so ADL-EDGE-02's run-time interchangeability
    // is unreachable from AlertDialog's side.
    [Fact(DisplayName = "port: the AlertDialog parts take an AlertDialogHandle, which a DialogHandle is not, and the Dialog parts accept one")]
    [Trait("Requirement", "ADL-API-04")]
    [Trait("Requirement", "ADL-API-05")]
    [Trait("Requirement", "ADL-EDGE-02")]
    public void The_AlertDialog_parts_take_an_AlertDialogHandle()
    {
        Assert.Equal(typeof(AlertDialogHandle<int>), typeof(AlertDialogRoot<int>).GetProperty("Handle")?.PropertyType);
        Assert.Equal(typeof(AlertDialogHandle<int>), typeof(AlertDialogTrigger<int>).GetProperty("Handle")?.PropertyType);
        Assert.False(typeof(AlertDialogHandle<int>).IsAssignableFrom(typeof(DialogHandle<int>)));

        Assert.Equal(typeof(DialogHandle<int>), typeof(DialogRoot<int>).GetProperty("Handle")?.PropertyType);
        Assert.Equal(typeof(DialogHandle<int>), typeof(DialogTrigger<int>).GetProperty("Handle")?.PropertyType);
        Assert.True(typeof(DialogHandle<int>).IsAssignableFrom(typeof(AlertDialogHandle<int>)));
    }

    [Fact(DisplayName = "port: AlertDialogHandle derives from DialogHandle, is created with new and declares no member of its own")]
    [Trait("Requirement", "ADL-API-06")]
    [Trait("Requirement", "ADL-EDGE-02")]
    [Trait("Requirement", "PSH-API-03")]
    public void AlertDialogHandle_derives_from_DialogHandle_and_declares_no_member()
    {
        Type type = typeof(AlertDialogHandle<int>);

        Assert.Equal(typeof(DialogHandle<int>), type.BaseType);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        MemberInfo[] declared = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal([".ctor"], declared.Select(member => member.Name));

        // OpenWithPayloadAsync is the Dialog family's (PSH-API-03), inherited.
        MethodInfo? openWithPayload = type.GetMethod("OpenWithPayloadAsync", BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(typeof(DialogHandle<int>), openWithPayload?.DeclaringType);
        Assert.Equal(typeof(int), Assert.Single(openWithPayload!.GetParameters()).ParameterType);
    }

    [Fact(DisplayName = "AlertDialog handle is closed and names itself in warnings while no root is attached")]
    [Trait("Requirement", "ADL-API-06")]
    [Trait("Requirement", "ADL-EDGE-01")]
    [Trait("Requirement", "PSH-EDGE-02")]
    public async Task AlertDialog_handle_is_closed_and_names_itself_in_warnings_while_no_root_is_attached()
    {
        CapturingLogger logger = new();
        AlertDialogHandle<NoPayload> handle = new();
        handle.AttachStore(new FakePopupStore<NoPayload>(logger)).Dispose();

        Assert.False(handle.IsOpen);
        await handle.OpenAsync();
        await handle.CloseAsync();

        Assert.False(handle.IsOpen);
        Assert.Equal(
            [
                "Blazix: `DialogHandle.OpenAsync()` was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.",
                "Blazix: `DialogHandle.CloseAsync()` was called while no root using this handle is mounted. The call was ignored.",
            ],
            logger.Messages);
    }

    [Fact(DisplayName = "AlertDialog handle ignores openWithPayload() with a warning while detached")]
    [Trait("Requirement", "ADL-EDGE-01")]
    [Trait("Requirement", "PSH-API-03")]
    [Trait("Requirement", "PSH-EDGE-02")]
    public async Task AlertDialog_handle_ignores_openWithPayload_with_a_warning_while_detached()
    {
        CapturingLogger logger = new();
        AlertDialogHandle<int> handle = new();
        handle.AttachStore(new FakePopupStore<int>(logger)).Dispose();

        await handle.OpenWithPayloadAsync(8);

        Assert.False(handle.IsOpen);
        Assert.Equal(
            ["Blazix: `DialogHandle.OpenWithPayloadAsync()` was called while no root using this handle is mounted. The call and its payload were ignored; mount a root with this handle before opening it imperatively."],
            logger.Messages);
    }

    private static Dictionary<string, PropertyInfo> Parameters(Type part) =>
        part.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

    // The Handle parameter is compared by the title on handle types; every other parameter keeps Dialog's type and default.
    private static void AssertSameTypesAndDefaults(Dictionary<string, PropertyInfo> dialog, Dictionary<string, PropertyInfo> alert, object dialogPart, object alertPart)
    {
        foreach ((string name, PropertyInfo property) in alert.Where(pair => pair.Key != "Handle"))
        {
            Assert.True(dialog[name].PropertyType == property.PropertyType, $"{name}: {property.PropertyType} where Dialog has {dialog[name].PropertyType}");
            Assert.True(Equals(dialog[name].GetValue(dialogPart), property.GetValue(alertPart)), $"{name}: the default differs from Dialog's");
        }
    }
}
