using System.Reflection;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// ContextMenu at L1: its own parts' parameter lists by reflection against Menu's (CTX-API-01, 03..05, 07..13, CTX-EDGE-03,
/// the `none` rows' reflection titles of the brief), the parent context's fixed values (CTX-STR-03, STA-05, STA-10,
/// KEY-07, DIS-01, DOM-04), and how a menu resolves its parent around it (CTX-STR-07; CTX §16 Q15, design point 8 of M5
/// track 6).
/// </summary>
public sealed class ContextMenuApiTests
{
    private static readonly string[] s_removedRootParameters = ["Modal", "Handle", "TriggerId", "DefaultTriggerId", "PayloadContent", "CloseParentOnEsc"];

    [Fact(DisplayName = "port: ContextMenuRoot declares MenuRoot's parameters except Modal, Handle, TriggerId, DefaultTriggerId, PayloadContent and CloseParentOnEsc, with the same types and defaults")]
    [Trait("Requirement", "CTX-API-01")]
    [Trait("Requirement", "CTX-API-03")]
    [Trait("Requirement", "CTX-API-04")]
    [Trait("Requirement", "CTX-API-05")]
    [Trait("Requirement", "CTX-EDGE-03")]
    public void ContextMenuRoot_declares_MenuRoots_parameters_except_the_removed_ones()
    {
        Dictionary<string, PropertyInfo> menu = Parameters(typeof(MenuRoot<NoPayload>));
        Dictionary<string, PropertyInfo> contextMenu = Parameters(typeof(ContextMenuRoot));

        Assert.Subset(menu.Keys.ToHashSet(), s_removedRootParameters.ToHashSet());
        Assert.Equal(menu.Keys.Except(s_removedRootParameters).Order(StringComparer.Ordinal), contextMenu.Keys.Order(StringComparer.Ordinal));
        AssertSameTypesAndDefaults(menu, contextMenu, new MenuRoot<NoPayload>(), new ContextMenuRoot());

        Assert.False(typeof(ContextMenuRoot).IsGenericType);
        Assert.True(typeof(ContextMenuRoot).IsSealed);
        Assert.Equal(typeof(Task), typeof(ContextMenuRoot).GetMethod("CloseAsync", Type.EmptyTypes)?.ReturnType);
        Assert.Equal(typeof(void), typeof(ContextMenuRoot).GetMethod("Unmount", Type.EmptyTypes)?.ReturnType);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ContextMenuRoot)));
    }

    [Fact(DisplayName = "port: ContextMenuTrigger declares no parameter of its own and renders its state as ContextMenuTriggerState")]
    [Trait("Requirement", "CTX-API-06")]
    [Trait("Requirement", "CTX-STA-01")]
    public void ContextMenuTrigger_declares_no_parameter_of_its_own()
    {
        Assert.DoesNotContain(Parameters(typeof(ContextMenuTrigger)).Values, property => property.DeclaringType == typeof(ContextMenuTrigger));
        Assert.Equal(typeof(Blazix.Primitives.Rendering.PartBase<ContextMenuTriggerState>), typeof(ContextMenuTrigger).BaseType);
        Assert.True(typeof(ContextMenuTrigger).IsSealed);
        Assert.Equal(["Open"], typeof(ContextMenuTriggerState).GetConstructors().Single().GetParameters().Select(parameter => parameter.Name!));
        Assert.Equal("data-popup-open", ContextMenuTriggerDataAttributes.PopupOpen);
        Assert.Equal("data-pressed", ContextMenuTriggerDataAttributes.Pressed);
    }

    // Design point 4 (a) of M5 track 7: Base UI's positionMethod is `@ignore @deprecated` on ContextMenu.Positioner.
    [Fact(DisplayName = "port: ContextMenuPositioner declares MenuPositioner's parameters except PositionMethod, with the same types and defaults")]
    [Trait("Requirement", "CTX-API-07")]
    [Trait("Requirement", "CTX-API-08")]
    [Trait("Requirement", "CTX-API-09")]
    [Trait("Requirement", "CTX-API-10")]
    [Trait("Requirement", "CTX-API-11")]
    [Trait("Requirement", "CTX-API-12")]
    [Trait("Requirement", "CTX-API-13")]
    public void ContextMenuPositioner_declares_MenuPositioners_parameters_except_PositionMethod()
    {
        Dictionary<string, PropertyInfo> menu = Parameters(typeof(MenuPositioner));
        Dictionary<string, PropertyInfo> contextMenu = Parameters(typeof(ContextMenuPositioner));

        Assert.Equal(menu.Keys.Except(["PositionMethod"]).Order(StringComparer.Ordinal), contextMenu.Keys.Order(StringComparer.Ordinal));
        AssertSameTypesAndDefaults(menu, contextMenu, new MenuPositioner(), new ContextMenuPositioner());
        Assert.True(typeof(ContextMenuPositioner).IsSealed);
    }

    [Fact(DisplayName = "port: a context menu's parent context is a modal, enabled, vertical context-menu parent with no cut-out, no relay, the cross-orientation key kept open and a 500 ms grace")]
    [Trait("Requirement", "CTX-STR-03")]
    [Trait("Requirement", "CTX-STA-05")]
    [Trait("Requirement", "CTX-STA-10")]
    [Trait("Requirement", "CTX-DOM-04")]
    [Trait("Requirement", "CTX-KEY-07")]
    [Trait("Requirement", "CTX-DIS-01")]
    public void The_parent_context_has_the_fixed_values_of_a_context_menu()
    {
        ContextMenuRootContext context = new("base-ui-7");

        Assert.Equal(MenuParentKind.ContextMenu, context.Kind);
        Assert.Null(context.Enclosing);
        Assert.True(context.Modal);
        Assert.False(context.Disabled);
        Assert.Equal("base-ui-7", context.RootId);
        Assert.Equal(Orientation.Vertical, context.Orientation);
        Assert.False(context.HasSubmenuOpen);
        Assert.Null(context.CutoutElementId);
        Assert.Null(context.RelayTarget);
        Assert.True(context.KeepOpenOnCrossOrientationKey);
        Assert.Equal(500, context.OutsidePressGrace);
    }

    [Fact(DisplayName = "port: the parent context records the backdrops and tells its subscribers, until they unsubscribe")]
    [Trait("Requirement", "CTX-PTR-03")]
    [Trait("Requirement", "CTX-EDGE-05")]
    public void The_parent_context_records_the_backdrops_and_tells_its_subscribers()
    {
        ContextMenuRootContext context = new("base-ui-7");
        int calls = 0;
        int changes = 0;
        IDisposable subscription = context.Subscribe(() => calls++);
        context.ElementsChanged += () => changes++;
        ElementReference backdrop = new("backdrop");
        ElementReference internalBackdrop = new("internal");

        context.SetBackdropElement(backdrop);
        context.SetInternalBackdropElement(internalBackdrop);
        context.SetInternalBackdropElement(internalBackdrop);

        Assert.Equal(backdrop, context.BackdropElement);
        Assert.Equal(internalBackdrop, context.InternalBackdropElement);
        Assert.Equal(2, changes);
        subscription.Dispose();
        context.SetBackdropElement(null);
        Assert.Null(context.BackdropElement);
        Assert.Equal(3, changes);
        // The parent's values never change, so its subscribers are never told; the backdrops reach the module, not Menu.
        Assert.Equal(0, calls);
    }

    // CTX §16 Q15, replicated as Base UI's MenuRoot.tsx:94-122 behaves: a context menu inside a menubar resolves to the bar.
    [Fact(DisplayName = "port: a context menu's root resolves to its own context, or to the menubar enclosing it")]
    [Trait("Requirement", "CTX-STR-07")]
    [Trait("Requirement", "CTX-STR-03")]
    public void A_context_menus_root_resolves_to_its_context_or_the_enclosing_bar()
    {
        ContextMenuRootContext context = new("base-ui-7");
        MenuParent own = MenuParent.Resolve(isSubmenu: false, enclosingMenu: null, context);
        Assert.Equal(MenuParentKind.ContextMenu, own.Kind);
        Assert.Same(context, own.Context);

        TestBar bar = new();
        context.Enclosing = bar;
        MenuParent inBar = MenuParent.Resolve(isSubmenu: false, enclosingMenu: null, context);
        Assert.Equal(MenuParentKind.Menubar, inBar.Kind);
        Assert.Same(bar, inBar.Context);
        Assert.Same(context, MenuParent.Find(context, MenuParentKind.ContextMenu));
    }

    [Fact(DisplayName = "port: a plain MenuRoot inside a context menu has no parent, and a submenu inside it has the context menu's menu")]
    [Trait("Requirement", "CTX-STR-07")]
    public void A_plain_menu_inside_a_context_menu_is_top_level()
    {
        ContextMenuRootContext context = new("base-ui-7");
        MenuRootContext contextMenusMenu = new MenuStore<NoPayload>("popup", "token").CreateContext("ContextMenu");

        Assert.Equal(MenuParent.None, MenuParent.Resolve(isSubmenu: false, contextMenusMenu, context));
        MenuParent submenu = MenuParent.Resolve(isSubmenu: true, contextMenusMenu, context);
        Assert.Equal(MenuParentKind.Menu, submenu.Kind);
        Assert.Same(contextMenusMenu, submenu.Menu);
    }

    // The parameters the part declares: those it inherits from PartBase (ClassValue, RenderFunction and the others) are typed by
    // its own state record, as every part's are.
    private static Dictionary<string, PropertyInfo> Parameters(Type part) =>
        part.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

    private static void AssertSameTypesAndDefaults(Dictionary<string, PropertyInfo> menu, Dictionary<string, PropertyInfo> contextMenu, object menuPart, object contextMenuPart)
    {
        foreach ((string name, PropertyInfo property) in contextMenu)
        {
            Assert.True(menu[name].PropertyType == property.PropertyType, $"{name}: {property.PropertyType} where Menu has {menu[name].PropertyType}");
            Assert.True(Equals(menu[name].GetValue(menuPart), property.GetValue(contextMenuPart)), $"{name}: the default differs from Menu's");
        }
    }

    /// <summary>A test-only menubar context, the parent a context menu resolves to inside a bar.</summary>
    private sealed class TestBar : IMenuParentContext
    {
        public MenuParentKind Kind => MenuParentKind.Menubar;

        public IMenuParentContext? Enclosing => null;

        public bool Modal => true;

        public bool Disabled => false;

        public string? RootId => "bar";

        public Orientation Orientation => Orientation.Horizontal;

        public bool HasSubmenuOpen => false;

        public string? CutoutElementId => null;

        public ElementReference? RelayTarget => null;

        public bool KeepOpenOnCrossOrientationKey => false;

        public int OutsidePressGrace => 0;

        public void SetBackdropElement(ElementReference? element)
        {
        }

        public void SetInternalBackdropElement(ElementReference? element)
        {
        }

        public void OnMenuOpenChange(MenuRootContext menu, bool open, EventReason reason)
        {
        }

        public IDisposable Subscribe(Action changed) => new Unsubscriber();

        private sealed class Unsubscriber : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
