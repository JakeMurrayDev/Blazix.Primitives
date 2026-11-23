using Blazix.Primitives.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators.Analyzers;

/// <summary>
/// The member-order analyzer (ADR 08 §5): members appear as constants, fields, constructors, private properties, parameter
/// properties, public properties, internal properties, lifecycle methods, dispose methods, public methods, internal
/// methods, private methods, nested types; a member after one of a later group is a build error naming both. The
/// placements ADR 08 leaves open (constructors, events, operators, explicit interface implementations, protected members,
/// nested types, <c>BuildRenderTree</c>) are the analyzer's, listed in its <c>CONTEXT.md</c>.
/// </summary>
public sealed class MemberOrderAnalyzerTests
{
    private static readonly ReferenceAssemblies s_withComponents = ReferenceAssemblies.Net.Net100.AddPackages(
        [new PackageIdentity("Microsoft.AspNetCore.Components", "10.0.0")]);

    // One member per group, in order: the template with NAME where the name goes, the name, and the group's name.
    private static readonly Dictionary<string, (string Template, string Name)> s_members = new()
    {
        ["constants"] = ("private const int NAME = 1;", "Limit"),
        ["fields"] = ("private int NAME;", "count"),
        ["constructors"] = ("public NAME() { }", "Part"),
        ["private properties"] = ("private int NAME { get; set; }", "Hidden"),
        ["parameter properties"] = ("[Parameter] public int NAME { get; set; }", "Value"),
        ["public properties"] = ("public int NAME => 0;", "Size"),
        ["internal properties"] = ("internal int NAME => 0;", "Depth"),
        ["lifecycle methods"] = ("protected override void NAME() { }", "OnInitialized"),
        ["dispose methods"] = ("public void NAME() { }", "Dispose"),
        ["public methods"] = ("public void NAME() { }", "Focus"),
        ["internal methods"] = ("internal void NAME() { }", "Sync"),
        ["private methods"] = ("private void NAME() { }", "Update"),
        ["nested types"] = ("private sealed class NAME { }", "Entry"),
    };

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task A_type_with_every_group_in_order_is_accepted()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            internal sealed class Store { }

            internal class Part : ComponentBase, IDisposable
            {
                private const int Limit = 1;
                public const string Tag = "div";

                private static readonly object s_gate = new();
                private int count;
                public event Action? Changed;

                static Part() { }

                public Part() { }

                ~Part() { }

                private int Hidden { get; set; }

                [Inject]
                private IServiceProvider Services { get; set; } = null!;

                [Parameter]
                public int Value { get; set; }

                [Parameter(CaptureUnmatchedValues = true)]
                public System.Collections.Generic.IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

                [CascadingParameter]
                private Store? Store { get; set; }

                public int Size => count;

                protected int Margin => Limit;

                internal int Depth => count;

                private protected int Inset => count;

                public override Task SetParametersAsync(ParameterView parameters) => base.SetParametersAsync(parameters);

                protected override void OnInitialized() { }

                protected override Task OnParametersSetAsync() => Task.CompletedTask;

                protected override bool ShouldRender() => true;

                protected override void BuildRenderTree(RenderTreeBuilder builder) { }

                protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;

                public void Dispose() { }

                public void Focus() { }

                protected virtual void AddAttributes() { }

                public static Part operator +(Part left, Part right) => left;

                public static implicit operator int(Part part) => part.count;

                internal void Sync() { }

                private protected void Reset() { }

                private void Update() { }

                private static int Clamp(int value) => value;

                private sealed class Entry { }

                private delegate void Callback();
            }
            """;

        await Verify(source);
    }

    [Theory]
    [Trait("Requirement", "ADR08-5")]
    [InlineData("constants", "fields")]
    [InlineData("fields", "constructors")]
    [InlineData("constructors", "private properties")]
    [InlineData("private properties", "parameter properties")]
    [InlineData("parameter properties", "public properties")]
    [InlineData("public properties", "internal properties")]
    [InlineData("internal properties", "lifecycle methods")]
    [InlineData("lifecycle methods", "dispose methods")]
    [InlineData("dispose methods", "public methods")]
    [InlineData("public methods", "internal methods")]
    [InlineData("internal methods", "private methods")]
    [InlineData("private methods", "nested types")]
    public async Task Two_adjacent_groups_out_of_order_are_an_error_on_the_earlier_groups_member(string earlier, string later)
    {
        (string earlierTemplate, string earlierName) = s_members[earlier];
        (string laterTemplate, string laterName) = s_members[later];
        string source = $$"""
            using Microsoft.AspNetCore.Components;

            internal sealed class Part : ComponentBase
            {
                {{laterTemplate.Replace("NAME", laterName)}}

                {{earlierTemplate.Replace("NAME", "{|#0:" + earlierName + "|}")}}
            }
            """;

        await Verify(source, Error(0, earlierName, earlier, laterName, later));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task A_member_is_reported_against_the_first_member_of_a_later_group()
    {
        const string source = """
            internal sealed class Part
            {
                public void Focus() { }

                private int {|#0:Hidden|} => 0;

                internal void Sync() { }

                private int {|#1:count|};

                private const int {|#2:Limit|} = 1;
            }
            """;

        await Verify(
            source,
            Error(0, "Hidden", "private properties", "Focus", "public methods"),
            Error(1, "count", "fields", "Focus", "public methods"),
            Error(2, "Limit", "constants", "Focus", "public methods"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Protected_members_order_as_public_and_private_protected_members_as_internal()
    {
        const string source = """
            internal class Part
            {
                internal int Depth => 0;

                protected int {|#0:Margin|} => 0;

                private void Update() { }

                private protected void {|#1:Reset|}() { }

                protected internal void {|#2:Focus|}() { }
            }
            """;

        await Verify(
            source,
            Error(0, "Margin", "public properties", "Depth", "internal properties"),
            Error(1, "Reset", "internal methods", "Update", "private methods"),
            Error(2, "Focus", "public methods", "Update", "private methods"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task A_cascading_parameter_is_a_parameter_property_whatever_its_accessibility()
    {
        const string source = """
            using Microsoft.AspNetCore.Components;

            internal sealed class Store { }

            internal sealed class Part : ComponentBase
            {
                public int Size => 0;

                [CascadingParameter]
                private Store? {|#0:Store|} { get; set; }
            }
            """;

        await Verify(source, Error(0, "Store", "parameter properties", "Size", "public properties"));
    }

    [Theory]
    [Trait("Requirement", "ADR08-5")]
    [InlineData("public override Task SetParametersAsync(ParameterView parameters) => base.SetParametersAsync(parameters);", "SetParametersAsync")]
    [InlineData("protected override Task OnInitializedAsync() => Task.CompletedTask;", "OnInitializedAsync")]
    [InlineData("protected override void OnParametersSet() { }", "OnParametersSet")]
    [InlineData("protected override void OnAfterRender(bool firstRender) { }", "OnAfterRender")]
    [InlineData("protected override bool ShouldRender() => true;", "ShouldRender")]
    [InlineData("protected override void BuildRenderTree(RenderTreeBuilder builder) { }", "BuildRenderTree")]
    public async Task Every_lifecycle_method_comes_before_the_dispose_methods(string declaration, string name)
    {
        string source = $$"""
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            internal sealed class Part : ComponentBase
            {
                public void Dispose() { }

                {{declaration.Replace(" " + name + "(", " {|#0:" + name + "|}(")}}
            }
            """;

        await Verify(source, Error(0, name, "lifecycle methods", "Dispose", "dispose methods"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Every_dispose_overload_comes_before_the_public_methods()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;

            internal class Part : IDisposable, IAsyncDisposable
            {
                public void Focus() { }

                void IDisposable.{|#0:Dispose|}() { }

                protected virtual void {|#1:Dispose|}(bool disposing) { }

                public ValueTask {|#2:DisposeAsync|}() => DisposeAsyncCore();

                protected virtual ValueTask {|#3:DisposeAsyncCore|}() => default;
            }
            """;

        await Verify(
            source,
            Error(0, "IDisposable.Dispose", "dispose methods", "Focus", "public methods"),
            Error(1, "Dispose", "dispose methods", "Focus", "public methods"),
            Error(2, "DisposeAsync", "dispose methods", "Focus", "public methods"),
            Error(3, "DisposeAsyncCore", "dispose methods", "Focus", "public methods"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Explicit_interface_implementations_and_operators_order_as_public_members()
    {
        const string source = """
            using System;

            internal interface IHost
            {
                int Size { get; }

                void Attach();
            }

            internal sealed class Part : IHost, IEquatable<Part>
            {
                internal int Depth => 0;

                int IHost.{|#0:Size|} => 0;

                private void Update() { }

                void IHost.{|#1:Attach|}() { }

                bool IEquatable<Part>.{|#2:Equals|}(Part? other) => false;

                public static bool operator {|#3:==|}(Part? left, Part? right) => false;

                public static bool operator {|#4:!=|}(Part? left, Part? right) => true;

                public static implicit operator {|#5:int|}(Part part) => 0;

                public override bool {|#6:Equals|}(object? obj) => false;

                public override int {|#7:GetHashCode|}() => 0;
            }
            """;

        await Verify(
            source,
            Error(0, "IHost.Size", "public properties", "Depth", "internal properties"),
            Error(1, "IHost.Attach", "public methods", "Update", "private methods"),
            Error(2, "IEquatable<Part>.Equals", "public methods", "Update", "private methods"),
            Error(3, "operator ==", "public methods", "Update", "private methods"),
            Error(4, "operator !=", "public methods", "Update", "private methods"),
            Error(5, "implicit operator int", "public methods", "Update", "private methods"),
            Error(6, "Equals", "public methods", "Update", "private methods"),
            Error(7, "GetHashCode", "public methods", "Update", "private methods"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Constructors_and_events_take_their_places()
    {
        const string source = """
            using System;

            internal sealed class Part
            {
                public Part() { }

                public event Action? {|#0:Changed|};

                private int Hidden => 0;

                ~{|#1:Part|}() { }

                private void Update() { }

                static {|#2:Part|}() { }
            }
            """;

        await Verify(
            source,
            Error(0, "Changed", "fields", "Part", "constructors"),
            Error(1, "~Part", "constructors", "Hidden", "private properties"),
            Error(2, "static Part", "constructors", "Hidden", "private properties"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Structs_records_and_interfaces_are_checked_and_each_partial_declaration_on_its_own()
    {
        const string source = """
            internal readonly struct Pair
            {
                public int Sum() => 0;

                private readonly int {|#0:left|};
            }

            internal sealed record Entry(string Name)
            {
                public string Label() => Name;

                public int {|#1:Length|} => Name.Length;
            }

            internal interface IPart
            {
                void Focus();

                int {|#2:Size|} { get; }
            }

            internal sealed partial class Part
            {
                public void Focus() { }
            }

            internal sealed partial class Part
            {
                private int count;
            }
            """;

        await Verify(
            source,
            Error(0, "left", "fields", "Sum", "public methods"),
            Error(1, "Length", "public properties", "Label", "public methods"),
            Error(2, "Size", "public properties", "Focus", "public methods"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task A_pragma_suppresses_the_rule()
    {
        const string source = """
            internal sealed class Part
            {
                public void Focus() { }

            #pragma warning disable BLZX2003 // a field kept beside the method that owns it
                private int count;
            #pragma warning restore BLZX2003

                private int {|#0:depth|};
            }
            """;

        // The testing SDK wraps the source in its own pragma to check suppression, which the source's restore would undo.
        await Verify(source, skipSuppressionCheck: true, Error(0, "depth", "fields", "Focus", "public methods"));
    }

    private static DiagnosticResult Error(int location, string member, string group, string preceding, string precedingGroup)
    {
        return new DiagnosticResult(MemberOrderAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithArguments(member, group, preceding, precedingGroup);
    }

    private static Task Verify(string source, params DiagnosticResult[] expected) => Verify(source, skipSuppressionCheck: false, expected);

    private static async Task Verify(string source, bool skipSuppressionCheck, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<MemberOrderAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = s_withComponents,
        };
        if (skipSuppressionCheck)
        {
            test.TestBehaviors |= TestBehaviors.SkipSuppressionCheck;
        }

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
