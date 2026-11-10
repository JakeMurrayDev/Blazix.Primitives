using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.4..3.6 at L2: what C# renders for the chips, the order the composite module reports and C# sends back to the chips'
// handle, and the removals and the open the `combobox` module reports (its keys and focus moves are L3). Base UI's chip
// titles are browser interactions (L4); these are port titles over the requests C# runs.
/// <summary>Combobox's Chips, Chip and ChipRemove at L2.</summary>
public sealed class ComboboxChipsTests : IDisposable
{
    private static readonly string[] s_values = ["apple", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // ADR 04 Amendments 2026-11-10: as the input group's (CMB-PTR-08's preventBaseUIHandler clause).
    [Fact(DisplayName = "port: the chips' onmousedown runs once inside press, and its PreventPartHandler() vetoes the press")]
    [Trait("Requirement", "CMB-PTR-08")]
    public async Task The_chips_onmousedown_runs_once_inside_press()
    {
        int calls = 0;
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxChips>(0);
            builder.AddComponentParameter(1, "data-testid", "chips");
            builder.AddComponentParameter(2, "onmousedown", EventCallback.Factory.Create<MouseEventArgs>(this, e =>
            {
                calls++;
                e.PreventPartHandler();
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p.Add(x => x.Extra, (RenderFragment)Extra));
        Assert.Contains("\"decidesPress\":true", test.LastOptions(ComboboxTwin.ChipsPart), StringComparison.Ordinal);
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.ChipsPart);

        Assert.Equal("stop", await cut.InvokeAsync(() => twin.Decide(ComboboxTwin.PressPoint, ComboboxTestContext.Event(ComboboxTestContext.Click, "mousedown", 2))));
        await cut.InvokeAsync(() => cut.Find("[data-testid=chips]").MouseDown(new MouseEventArgs { Type = "mousedown", Detail = 1 }));
        Assert.Equal(1, calls);
        await cut.InvokeAsync(() => cut.Find("[data-testid=chips]").MouseDown(new MouseEventArgs { Type = "mousedown", Detail = 1 }));

        Assert.Equal(2, calls);
    }

    [Fact(DisplayName = "port: the chips container is a toolbar only while a value is selected")]
    [Trait("Requirement", "CMB-STR-09")]
    public void The_chips_container_is_a_toolbar_only_while_a_value_is_selected()
    {
        Assert.Equal("toolbar", Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)s_values }).Find("[data-testid=chips]").GetAttribute("role"));
        Assert.False(Render(new() { ["Multiple"] = true }, chips: []).Find("[data-testid=chips]").HasAttribute("role"));
    }

    [Fact(DisplayName = "port: a chip has tabindex -1, and aria-disabled and aria-readonly from the root")]
    [Trait("Requirement", "CMB-ARIA-17")]
    [Trait("Requirement", "CMB-STA-03")]
    public void A_chip_carries_the_roots_disabled_and_readonly_state()
    {
        IElement chip = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)s_values, ["Disabled"] = true, ["ReadOnly"] = true }).Find("[data-testid=chip-apple]");

        Assert.Equal("div", chip.TagName.ToLowerInvariant());
        Assert.Equal("-1", chip.GetAttribute("tabindex"));
        Assert.Equal("true", chip.GetAttribute("aria-disabled"));
        Assert.Equal("true", chip.GetAttribute("aria-readonly"));
        Assert.True(chip.HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "port: the chips' handle receives the chips' ids in the order the composite module reports")]
    [Trait("Requirement", "CMB-KEY-07")]
    public async Task The_chips_handle_receives_the_reported_order()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)s_values });
        string apple = cut.Find("[data-testid=chip-apple]").Id!;
        string cherry = cut.Find("[data-testid=chip-cherry]").Id!;

        await ReportOrderAsync(cut, cherry, apple);

        Assert.Contains($"\"chipIds\":[\"{cherry}\",\"{apple}\"]", ChipsOptions(), StringComparison.Ordinal);
        Assert.Contains($"\"chipsId\":\"{cut.Find("[data-testid=chips]").Id}\"", test.LastInputOptions(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: Backspace on a chip removes its value with the reason none")]
    [Trait("Requirement", "CMB-KEY-08")]
    public async Task Backspace_on_a_chip_removes_its_value()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)s_values });
        ComboboxTwin chips = test.TwinOf(ComboboxTwin.ChipsPart);

        await cut.InvokeAsync(() => chips.OnRemove($$"""{"index":1,"event":{{ComboboxTestContext.Key("Backspace")}}}"""));

        (IReadOnlyList<string> values, EventReason reason) = Assert.Single(cut.Instance.ValuesChanges);
        Assert.Equal(["apple"], values);
        Assert.Equal(EventReason.None, reason);
    }

    [Fact(DisplayName = "port: ArrowDown on a chip opens the popup with list-navigation")]
    [Trait("Requirement", "CMB-KEY-09")]
    public async Task ArrowDown_on_a_chip_opens_the_popup()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)s_values });
        ComboboxTwin chips = test.TwinOf(ComboboxTwin.ChipsPart);

        await cut.InvokeAsync(() => chips.OnOpenRequest(ComboboxTestContext.Key("ArrowDown")));

        (bool open, EventReason reason) = Assert.Single(cut.Instance.OpenChanges);
        Assert.True(open);
        Assert.Equal(EventReason.ListNavigation, reason);
    }

    [Fact(DisplayName = "port: a chip remove button's report removes its chip's value with chip-remove-press")]
    [Trait("Requirement", "CMB-PTR-10")]
    public async Task A_chip_remove_report_removes_the_value()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)s_values });
        await ReportOrderAsync(cut, cut.Find("[data-testid=chip-apple]").Id!, cut.Find("[data-testid=chip-cherry]").Id!);
        Assert.Contains(RemoveOptions(), options => options.Contains("\"index\":0", StringComparison.Ordinal));
        ComboboxTwin remove = RemoveTwins()[0];

        await cut.InvokeAsync(() => remove.OnRemove($$"""{"index":0,"event":{{ComboboxTestContext.Click}}}"""));

        (IReadOnlyList<string> values, EventReason reason) = Assert.Single(cut.Instance.ValuesChanges);
        Assert.Equal(["cherry"], values);
        Assert.Equal(EventReason.ChipRemovePress, reason);
    }

    [Fact(DisplayName = "port: on WebAssembly the remove point removes the chip's value and stops the event")]
    [Trait("Requirement", "CMB-PTR-10")]
    [Trait("Requirement", "CMB-KEY-10")]
    public async Task The_remove_point_removes_and_stops()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)s_values });
        await ReportOrderAsync(cut, cut.Find("[data-testid=chip-apple]").Id!, cut.Find("[data-testid=chip-cherry]").Id!);
        ComboboxTwin remove = RemoveTwins()[1];

        string verdict = await cut.InvokeAsync(() => remove.Decide(ComboboxTwin.RemovePoint, ComboboxTestContext.Click));

        Assert.Equal("stop", verdict);
        (IReadOnlyList<string> values, EventReason reason) = Assert.Single(cut.Instance.ValuesChanges);
        Assert.Equal(["apple"], values);
        Assert.Equal(EventReason.ChipRemovePress, reason);
    }

    [Fact(DisplayName = "port: a chip outside the chips container throws the missing chips error")]
    [Trait("Requirement", "CMB-STR-02")]
    public void A_chip_outside_the_chips_container_throws()
    {
        RenderFragment extra = builder =>
        {
            builder.OpenComponent<ComboboxChip>(0);
            builder.CloseComponent();
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ComboboxHarness>(p => p.Add(x => x.Extra, extra)));

        Assert.StartsWith("Blazix: `ComboboxChipsContext` is missing.", error.Message, StringComparison.Ordinal);
    }

    private Task ReportOrderAsync(IRenderedComponent<ComboboxHarness> cut, params string[] ids)
    {
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"registrationOnly\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        string payload = "{\"ids\":[" + string.Join(',', ids.Select(id => "\"" + id + "\"")) + "]}";
        return cut.InvokeAsync(() => twin.OnItemsChange(payload));
    }

    private string ChipsOptions() => Options("chips").Last();

    private List<string> RemoveOptions() => Options("chip-remove");

    private List<string> Options(string part)
    {
        string marker = "\"part\":\"" + part + "\"";
        List<string> options = [];
        foreach (JSRuntimeInvocation attach in test.Combobox.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal))
            {
                options.Add(json);
            }
        }

        foreach (JSRuntimeInvocation update in test.ComboboxHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains(marker, StringComparison.Ordinal))
            {
                options.Add(json);
            }
        }

        return options;
    }

    private List<ComboboxTwin> RemoveTwins()
    {
        List<ComboboxTwin> twins = [];
        foreach (JSRuntimeInvocation attach in test.Combobox.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"part\":\"chip-remove\"", StringComparison.Ordinal))
            {
                twins.Add(((DotNetObjectReference<ComboboxTwin>)attach.Arguments[2]!).Value);
            }
        }

        return twins;
    }

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, IReadOnlyList<string>? chips = null)
    {
        IReadOnlyList<string> values = chips ?? s_values;
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxChips>(0);
            builder.AddComponentParameter(1, "data-testid", "chips");
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(inner =>
            {
                foreach (string value in values)
                {
                    inner.OpenComponent<ComboboxChip>(0);
                    inner.SetKey(value);
                    inner.AddComponentParameter(1, "data-testid", "chip-" + value);
                    inner.AddComponentParameter(2, "ChildContent", (RenderFragment)(chip =>
                    {
                        chip.AddContent(0, value);
                        chip.OpenComponent<ComboboxChipRemove>(1);
                        chip.AddComponentParameter(2, "data-testid", "remove-" + value);
                        chip.CloseComponent();
                    }));
                    inner.CloseComponent();
                }
            }));
            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.Extra, (RenderFragment)Extra));
    }
}
