using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Rendering;

/// <summary>
/// <c>merge-props/mergeProps.test.ts</c> over the typed list (RND-ALG-02..06, RND-API-09/10, RND-DOM-19, RND-EVT-01..04/06/10..12).
/// Handlers are untyped <see cref="EventCallback"/>s with no receiver, invoked through <see cref="AttributeValue.InvokeAsync"/>
/// as Blazor's renderer would. Titles whose arguments Blazor cannot express (several handler arguments, a non-event
/// first argument) are relaxed by ADR 02 (RND-EVT-07/08) and are not translated.
/// </summary>
public sealed class MergePropsTests
{
    [Fact(DisplayName = "merges event handlers")]
    [Trait("Requirement", "RND-EVT-01")]
    public async Task Merges_event_handlers()
    {
        List<string> order = [];
        AttributeList theirProps = new();
        theirProps.Set("onclick", Handler<MouseEventArgs>(_ => order.Add("their-click")));
        theirProps.Set("onkeydown", Handler<KeyboardEventArgs>(_ => order.Add("their-keydown")));
        AttributeList ourProps = new();
        ourProps.Set("onclick", Handler<MouseEventArgs>(_ => order.Add("our-click")));
        ourProps.Set("onpaste", Handler<ClipboardEventArgs>(_ => order.Add("our-paste")));

        AttributeList mergedProps = MergeProps.Merge(ourProps, theirProps);

        await Invoke(mergedProps, "onclick", new MouseEventArgs());
        await Invoke(mergedProps, "onkeydown", new KeyboardEventArgs());
        await Invoke(mergedProps, "onpaste", new ClipboardEventArgs());

        Assert.Equal(["their-click", "our-click", "their-keydown", "our-paste"], order);
    }

    [Fact(DisplayName = "merges multiple event handlers")]
    [Trait("Requirement", "RND-EVT-02")]
    public async Task Merges_multiple_event_handlers()
    {
        List<string> log = [];

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("3"))),
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("2"))),
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("1"))));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());
        Assert.Equal(["1", "2", "3"], log);
    }

    [Fact(DisplayName = "merges undefined event handlers")]
    [Trait("Requirement", "RND-EVT-10")]
    public async Task Merges_undefined_event_handlers()
    {
        List<string> log = [];

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("3"))),
            With("onclick", default(AttributeValue)),
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("1"))));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());
        Assert.Equal(["1", "3"], log);
    }

    [Fact(DisplayName = "makes a lone synthetic event handler preventable")]
    [Trait("Requirement", "RND-EVT-06")]
    public async Task Makes_a_lone_synthetic_event_handler_preventable()
    {
        bool prevented = false;

        AttributeList mergedProps = MergeProps.Merge(
            new AttributeList(),
            With("onmousedown", Handler<MouseEventArgs>(e =>
            {
                e.PreventPartHandler();
                prevented = e.PartHandlerPrevented();
            })));

        await Invoke(mergedProps, "onmousedown", new MouseEventArgs());

        Assert.True(prevented);
    }

    [Fact(DisplayName = "makes a first-position synthetic event handler preventable")]
    [Trait("Requirement", "RND-EVT-06")]
    public async Task Makes_a_first_position_synthetic_event_handler_preventable()
    {
        bool prevented = false;

        AttributeList mergedProps = MergeProps.Merge(
            With("onmousedown", Handler<MouseEventArgs>(e =>
            {
                e.PreventPartHandler();
                prevented = e.PartHandlerPrevented();
            })),
            With("id", "test-button"));

        await Invoke(mergedProps, "onmousedown", new MouseEventArgs());

        Assert.True(prevented);
    }

    [Fact(DisplayName = "makes a first-position synthetic event handler preventable in mergePropsN")]
    [Trait("Requirement", "RND-API-10")]
    public async Task Makes_a_first_position_synthetic_event_handler_preventable_in_mergePropsN()
    {
        bool prevented = false;

        AttributeList mergedProps = MergeProps.Merge(
        [
            With("onmousedown", Handler<MouseEventArgs>(e =>
            {
                e.PreventPartHandler();
                prevented = e.PartHandlerPrevented();
            })),
            With("id", "test-button"),
        ]);

        await Invoke(mergedProps, "onmousedown", new MouseEventArgs());

        Assert.True(prevented);
    }

    [Fact(DisplayName = "makes a lone obscure synthetic event handler preventable")]
    [Trait("Requirement", "RND-EVT-06")]
    public async Task Makes_a_lone_obscure_synthetic_event_handler_preventable()
    {
        bool prevented = false;

        AttributeList mergedProps = MergeProps.Merge(
            new AttributeList(),
            With("oncontextmenu", Handler<MouseEventArgs>(e =>
            {
                e.PreventPartHandler();
                prevented = e.PartHandlerPrevented();
            })));

        await Invoke(mergedProps, "oncontextmenu", new MouseEventArgs());

        Assert.True(prevented);
    }

    [Fact(DisplayName = "merges styles")]
    [Trait("Requirement", "RND-ALG-06")]
    public void Merges_styles()
    {
        AttributeList theirProps = With("style", "color: red");
        AttributeList ourProps = With("style", "color: blue; background-color: blue");

        AttributeList mergedProps = MergeProps.Merge(ourProps, theirProps);

        Assert.Equal("color: red; background-color: blue;", Text(mergedProps, "style"));
    }

    [Fact(DisplayName = "merges styles with undefined")]
    [Trait("Requirement", "RND-API-09")]
    public void Merges_styles_with_undefined()
    {
        AttributeList theirProps = With("style", "color: red");
        AttributeList ourProps = new();

        AttributeList mergedProps = MergeProps.Merge(ourProps, theirProps);

        Assert.Equal("color: red", Text(mergedProps, "style"));
    }

    [Fact(DisplayName = "does not merge styles if both are undefined")]
    [Trait("Requirement", "RND-ALG-06")]
    public void Does_not_merge_styles_if_both_are_undefined()
    {
        AttributeList mergedProps = MergeProps.Merge(new AttributeList(), new AttributeList());

        Assert.False(mergedProps.Contains("style"));
    }

    [Fact(DisplayName = "merges classNames with rightmost first")]
    [Trait("Requirement", "RND-ALG-05")]
    public void Merges_classNames_with_rightmost_first()
    {
        AttributeList theirProps = With("class", "external-class");
        AttributeList ourProps = With("class", "internal-class");

        AttributeList mergedProps = MergeProps.Merge(ourProps, theirProps);

        Assert.Equal("external-class internal-class", Text(mergedProps, "class"));
    }

    [Fact(DisplayName = "merges multiple classNames")]
    [Trait("Requirement", "RND-ALG-05")]
    public void Merges_multiple_classNames()
    {
        AttributeList mergedProps = MergeProps.Merge(With("class", "class-1"), With("class", "class-2"), With("class", "class-3"));

        Assert.Equal("class-3 class-2 class-1", Text(mergedProps, "class"));
    }

    [Fact(DisplayName = "merges classNames with undefined")]
    [Trait("Requirement", "RND-ALG-05")]
    public void Merges_classNames_with_undefined()
    {
        AttributeList mergedProps = MergeProps.Merge(new AttributeList(), With("class", "external-class"));

        Assert.Equal("external-class", Text(mergedProps, "class"));
    }

    [Fact(DisplayName = "does not merge classNames if both are undefined")]
    [Trait("Requirement", "RND-ALG-05")]
    public void Does_not_merge_classNames_if_both_are_undefined()
    {
        AttributeList mergedProps = MergeProps.Merge(new AttributeList(), new AttributeList());

        Assert.False(mergedProps.Contains("class"));
    }

    [Fact(DisplayName = "does not prevent internal handler if event.preventBaseUIHandler() is not called")]
    [Trait("Requirement", "RND-EVT-03")]
    public async Task Does_not_prevent_internal_handler_if_PreventPartHandler_is_not_called()
    {
        bool ran = false;

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => ran = true)),
            With("onclick", Handler<MouseEventArgs>(_ => { })));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());

        Assert.True(ran);
    }

    [Fact(DisplayName = "prevents internal handler if event.preventBaseUIHandler() is called")]
    [Trait("Requirement", "RND-EVT-03")]
    public async Task Prevents_internal_handler_if_PreventPartHandler_is_called()
    {
        bool ran = false;

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => ran = true)),
            With("onclick", Handler<MouseEventArgs>(_ => ran = true)),
            With("onclick", Handler<MouseEventArgs>(e => e.PreventPartHandler())));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());

        Assert.False(ran);
    }

    [Fact(DisplayName = "prevents handlers merged after event.preventBaseUIHandler() is called")]
    [Trait("Requirement", "RND-EVT-03")]
    public async Task Prevents_handlers_merged_after_PreventPartHandler_is_called()
    {
        List<string> log = [];

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("2"))),
            With("onclick", Handler<MouseEventArgs>(e =>
            {
                e.PreventPartHandler();
                log.Add("1");
            })),
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("0"))));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());

        Assert.Equal(["0", "1"], log);
    }

    [Fact(DisplayName = "merges internal props so that the ones defined first override the ones defined later")]
    [Trait("Requirement", "RND-DOM-19")]
    public void Merges_internal_props_so_that_the_ones_defined_first_override_the_ones_defined_later()
    {
        // The title is Base UI's (RND §10 Q3); the assertion is that the later input wins.
        AttributeList mergedProps = MergeProps.Merge(With("title", "internal title 2"), With("title", "internal title 1"), new AttributeList());

        Assert.Equal("internal title 1", Text(mergedProps, "title"));
    }

    [Fact(DisplayName = "sets baseUIHandlerPrevented to true after calling preventBaseUIHandler()")]
    [Trait("Requirement", "RND-EVT-04")]
    public async Task Sets_PartHandlerPrevented_to_true_after_calling_PreventPartHandler()
    {
        bool? observedFlag = null;

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => { })),
            With("onclick", Handler<MouseEventArgs>(e =>
            {
                e.PreventPartHandler();
                observedFlag = e.PartHandlerPrevented();
            })));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());

        Assert.True(observedFlag);
    }

    [Fact(DisplayName = "the veto flag is absent before PreventPartHandler is called and scoped to the event instance")]
    [Trait("Requirement", "RND-EVT-04")]
    public async Task The_veto_flag_is_absent_before_PreventPartHandler_is_called_and_scoped_to_the_event_instance()
    {
        MouseEventArgs first = new();
        MouseEventArgs second = new();
        Assert.False(first.PartHandlerPrevented());

        AttributeList mergedProps = MergeProps.Merge(With("onclick", Handler<MouseEventArgs>(e => e.PreventPartHandler())), new AttributeList());
        await Invoke(mergedProps, "onclick", first);

        Assert.True(first.PartHandlerPrevented());
        Assert.False(second.PartHandlerPrevented());
    }

    [Fact(DisplayName = "a handler name is matched case-insensitively and emitted in lowercase")]
    [Trait("Requirement", "RND-ALG-13")]
    public async Task A_handler_name_is_matched_case_insensitively_and_emitted_in_lowercase()
    {
        // ADR 02 decision 3: a handler is any `on`-prefixed entry whose value is a callback or delegate, any casing.
        List<string> log = [];

        AttributeList mergedProps = MergeProps.Merge(
            With("onClick", Handler<MouseEventArgs>(_ => log.Add("ours"))),
            With("OnClick", Handler<MouseEventArgs>(_ => log.Add("theirs"))));

        Assert.True(mergedProps.Contains("onclick"));
        Assert.Single(mergedProps);
        await Invoke(mergedProps, "onclick", new MouseEventArgs());
        Assert.Equal(["theirs", "ours"], log);
    }

    [Fact(DisplayName = "an on-prefixed string attribute is an ordinary attribute, not a handler")]
    [Trait("Requirement", "RND-EVT-13")]
    public void An_on_prefixed_string_attribute_is_an_ordinary_attribute_not_a_handler()
    {
        AttributeList mergedProps = MergeProps.Merge(With("one", "first"), With("one", "second"));

        Assert.Equal("second", Text(mergedProps, "one"));
    }

    [Fact(DisplayName = "the dictionary form merges with the same rules and unpacks a boxed typed callback")]
    [Trait("Requirement", "RND-ALG-02")]
    public async Task The_dictionary_form_merges_with_the_same_rules_and_unpacks_a_boxed_typed_callback()
    {
        List<string> log = [];
        Dictionary<string, object> ours = new()
        {
            ["class"] = "internal-class",
            ["style"] = "color: blue; padding: 4px",
            ["onclick"] = EventCallback.Factory.Create<MouseEventArgs>(new Receiver(), _ => log.Add("ours")),
            ["title"] = "ours",
        };
        Dictionary<string, object> theirs = new()
        {
            ["class"] = "external-class",
            ["style"] = "color: red",
            ["onclick"] = EventCallback.Factory.Create<MouseEventArgs>(new Receiver(), _ => log.Add("theirs")),
            ["title"] = "theirs",
            ["data-count"] = 5,
        };

        Dictionary<string, object> merged = MergeProps.MergeDictionaries(ours, theirs);

        Assert.Equal("external-class internal-class", merged["class"]);
        Assert.Equal("color: red; padding: 4px;", merged["style"]);
        Assert.Equal("theirs", merged["title"]);
        Assert.Equal("5", merged["data-count"]);
        await AttributeValue.FromObject(merged["onclick"]).InvokeAsync(new MouseEventArgs());
        Assert.Equal(["theirs", "ours"], log);
    }

    [Fact(DisplayName = "calls the props getter with the props defined after it")]
    [Trait("Requirement", "RND-EVT-11")]
    public void Calls_the_props_getter_with_the_props_defined_after_it()
    {
        // The title is Base UI's (RND §10 Q3); the getter receives the inputs defined before it.
        int calls = 0;
        List<(string, string)> observedProps = [];
        AttributeGetter propsGetter = props =>
        {
            calls++;
            observedProps = Strings(props);
            return props;
        };

        MergeProps.Merge(With(("id", "2"), ("class", "test-class")), propsGetter, With(("id", "1"), ("role", "button")));

        Assert.Equal(1, calls);
        Assert.Equal([("id", "2"), ("class", "test-class")], observedProps);
    }

    [Fact(DisplayName = "calls the props getter with merged props defined after it")]
    [Trait("Requirement", "RND-EVT-11")]
    public void Calls_the_props_getter_with_merged_props_defined_after_it()
    {
        int calls = 0;
        List<(string, string)> observedProps = [];
        AttributeGetter propsGetter = props =>
        {
            calls++;
            observedProps = Strings(props);
            return props;
        };

        MergeProps.Merge(With(("role", "button"), ("class", "test-class")), With("role", "tab"), propsGetter, With("id", "one"));

        Assert.Equal(1, calls);
        Assert.Equal([("role", "tab"), ("class", "test-class")], observedProps);
    }

    [Fact(DisplayName = "calls the props getter with an empty object if no props are defined after it")]
    [Trait("Requirement", "RND-EVT-11")]
    public void Calls_the_props_getter_with_an_empty_object_if_no_props_are_defined_after_it()
    {
        int calls = 0;
        int observedCount = -1;
        AttributeGetter propsGetter = props =>
        {
            calls++;
            observedCount = props.Count;
            return props;
        };

        MergeProps.Merge(propsGetter, With("id", "1"));

        Assert.Equal(1, calls);
        Assert.Equal(0, observedCount);
    }

    [Fact(DisplayName = "does not mutate a reused object returned by the first props getter")]
    [Trait("Requirement", "RND-EVT-12")]
    public void Does_not_mutate_a_reused_object_returned_by_the_first_props_getter()
    {
        AttributeList shared = With("class", "base");

        AttributeList result = MergeProps.Merge((AttributeGetter)(_ => shared), With("class", "next"));

        Assert.Equal([("class", "next base")], Strings(result));
        Assert.Equal([("class", "base")], Strings(shared));
    }

    [Fact(DisplayName = "accepts the result of the props getter")]
    [Trait("Requirement", "RND-EVT-11")]
    public void Accepts_the_result_of_the_props_getter()
    {
        AttributeGetter propsGetter = _ => With("class", "test-class");

        AttributeList result = MergeProps.Merge(With(("id", "two"), ("role", "tab")), With("id", "one"), propsGetter);

        Assert.Equal([("class", "test-class")], Strings(result));
    }

    [Fact(DisplayName = "does not automatically prevent handlers that are manually called by getter handlers")]
    [Trait("Requirement", "RND-EVT-11")]
    public async Task Does_not_automatically_prevent_handlers_that_are_manually_called_by_getter_handlers()
    {
        List<string> log = [];

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("first-handler"))),
            (AttributeGetter)(props =>
            {
                AttributeValue previous = props.TryGetValue("onclick", out AttributeValue value) ? value : default;
                return With("onclick", Handler<MouseEventArgs>(async e =>
                {
                    e.PreventPartHandler();
                    log.Add("getter-handler");
                    // Manually calling the previous handler bypasses the automatic prevention.
                    await previous.InvokeAsync(new MouseEventArgs());
                }));
            }),
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("last-handler"))));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());

        Assert.Equal(["last-handler", "getter-handler", "first-handler"], log);
    }

    [Fact(DisplayName = "allows props getter handlers to check baseUIHandlerPrevented manually")]
    [Trait("Requirement", "RND-EVT-11")]
    public async Task Allows_props_getter_handlers_to_check_PartHandlerPrevented_manually()
    {
        List<string> log = [];

        AttributeList mergedProps = MergeProps.Merge(
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("first-handler"))),
            (AttributeGetter)(props =>
            {
                AttributeValue previous = props.TryGetValue("onclick", out AttributeValue value) ? value : default;
                return With("onclick", Handler<MouseEventArgs>(async e =>
                {
                    e.PreventPartHandler();
                    log.Add("getter-handler");
                    if (!e.PartHandlerPrevented())
                    {
                        await previous.InvokeAsync(new MouseEventArgs());
                    }
                }));
            }),
            With("onclick", Handler<MouseEventArgs>(_ => log.Add("last-handler"))));

        await Invoke(mergedProps, "onclick", new MouseEventArgs());

        Assert.Equal(["last-handler", "getter-handler"], log);
    }

    [Fact(DisplayName = "an empty input list yields an empty list and absent inputs are skipped")]
    [Trait("Requirement", "RND-API-10")]
    public void An_empty_input_list_yields_an_empty_list_and_absent_inputs_are_skipped()
    {
        Assert.Empty(MergeProps.Merge([]));
        Assert.Equal([("id", "x")], Strings(MergeProps.Merge(MergeInput.Absent, With("id", "x"), MergeInput.Absent)));
    }

    private static AttributeList With(string name, AttributeValue value)
    {
        AttributeList list = new();
        list.Set(name, value);
        return list;
    }

    private static AttributeList With(params (string Name, string Value)[] entries)
    {
        AttributeList list = new();
        foreach ((string name, string value) in entries)
        {
            list.Set(name, value);
        }

        return list;
    }

    private static EventCallback Handler<TArgs>(Action<TArgs> handler) => new(null, handler);

    private static EventCallback Handler<TArgs>(Func<TArgs, Task> handler) => new(null, handler);

    private static Task Invoke(AttributeList list, string name, EventArgs args)
    {
        Assert.True(list.TryGetValue(name, out AttributeValue value), $"{name} is absent");
        return value.InvokeAsync(args);
    }

    private static string Text(AttributeList list, string name)
    {
        Assert.True(list.TryGetValue(name, out AttributeValue value), $"{name} is absent");
        Assert.True(value.TryGetValue(out string text), $"{name} is not a string");
        return text;
    }

    private static List<(string, string)> Strings(AttributeList list)
    {
        List<(string, string)> pairs = [];
        foreach (AttributeEntry entry in list)
        {
            pairs.Add((entry.Name, entry.Value.TryGetValue(out string text) ? text : entry.Value.ToString()));
        }

        return pairs;
    }

    private sealed class Receiver : IHandleEvent
    {
        /// <inheritdoc />
        public Task HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);
    }
}
