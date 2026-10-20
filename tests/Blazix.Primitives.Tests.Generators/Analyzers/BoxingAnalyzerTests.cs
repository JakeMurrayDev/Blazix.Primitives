using Blazix.Primitives.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators.Analyzers;

/// <summary>
/// The boxing analyzer (ADR 05 §4 and its union-binding amendment of 2026-10-20; ADR 16 §C): a union never converts to
/// <c>object</c> or an interface; any other value type converts only into Blazor's own <c>object</c> contracts (the
/// allow-list); the boxing members of <c>System.Enum</c> are not called. One test per allowed site and per forbidden
/// conversion. The compilation references <c>Microsoft.AspNetCore.Components</c> 10.0
/// and <c>Microsoft.JSInterop</c> 10.0 on the .NET 10 reference assemblies; the union attribute and interface, which .NET 10 lacks, are declared in the source.
/// </summary>
public sealed class BoxingAnalyzerTests
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Runtime.CompilerServices;
        using System.Threading.Tasks;
        using Microsoft.AspNetCore.Components;
        using Microsoft.AspNetCore.Components.Rendering;
        using Microsoft.JSInterop;

        """;

    // System.Runtime.CompilerServices.UnionAttribute and IUnion ship in .NET 11; the tests compile against .NET 10.
    private const string UnionShim = """

        namespace System.Runtime.CompilerServices
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
            public sealed class UnionAttribute : Attribute { }

            public interface IUnion
            {
                object? Value { get; }
            }
        }

        [Union]
        public readonly struct Choice : IUnion
        {
            private readonly string? text;
            private readonly bool flag;

            public Choice(string value) { text = value; flag = false; }

            public Choice(bool value) { text = null; flag = value; }

            public object? Value => text;
        }

        [Union]
        public readonly struct Marked
        {
            public Marked(string value) { Value = value; }

            public object? Value { get; }
        }

        public readonly struct Plain(int value)
        {
            public int Number => value;
        }

        public readonly struct Labeled
        {
            public override string ToString() => "labeled";
        }

        public enum Reason { None, Press }

        [Flags]
        public enum Modes { None = 0, Hover = 1, Focus = 2 }
        """;

    private static readonly ReferenceAssemblies s_withComponents = ReferenceAssemblies.Net.Net100.AddPackages(
        [
            new PackageIdentity("Microsoft.AspNetCore.Components", "10.0.0"),
            new PackageIdentity("Microsoft.JSInterop", "10.0.0"),
        ]);

    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task Typed_overloads_generics_reference_types_and_enum_operators_are_accepted()
    {
        const string source = Usings + """
            internal static class Code
            {
                public static void Render(RenderTreeBuilder builder, bool disabled, string label, EventCallback onClick, Reason reason, Modes modes)
                {
                    builder.OpenElement(0, "button");
                    builder.AddAttribute(1, "disabled", disabled);
                    builder.AddAttribute(2, "aria-label", label);
                    builder.AddAttribute(3, "onclick", onClick);
                    builder.AddAttribute(4, "title", (object)label);
                    builder.CloseElement();

                    object text = label;
                    IComparable<int> generic = Echo<IComparable<int>>(null!);
                    bool same = reason == Reason.Press;
                    bool hovering = (modes & Modes.Hover) != 0;
                    string literal = reason switch { Reason.Press => "press", _ => "none" };
                    string interpolated = $"{disabled} {reason}";
                    int number = disabled.GetHashCode() + 1.CompareTo(2);
                }

                private static T Echo<T>(T value) => value;
            }
            """ + UnionShim;

        await Verify(source);
    }

    [Theory]
    [Trait("Requirement", "ADR05-4")]
    [InlineData("builder.AddAttribute(0, \"tabindex\", index);")]
    [InlineData("builder.AddAttribute(0, \"tabindex\", (object)index);")]
    [InlineData("builder.AddComponentParameter(0, \"Value\", index);")]
    [InlineData("builder.AddMultipleAttributes(0, pairs);")]
    [InlineData("_ = js.InvokeVoidAsync(\"focus\", index, flag);")]
    [InlineData("_ = js.InvokeAsync<int>(\"measure\", new object?[] { index });")]
    [InlineData("_ = module.InvokeAsync<int>(\"measure\", index);")]
    [InlineData("_ = module.InvokeVoidAsync(\"update\", flag);")]
    [InlineData("_ = inProcessModule.Invoke<int>(\"measure\", index);")]
    [InlineData("_ = inProcessRuntime.Invoke<int>(\"measure\", index);")]
    [InlineData("bag[\"tabindex\"] = index;")]
    [InlineData("bag.Add(\"tabindex\", index);")]
    [InlineData("readOnlyBag = new Dictionary<string, object> { [\"tabindex\"] = index };")]
    [InlineData("pair = new KeyValuePair<string, object>(\"tabindex\", index);")]
    public async Task A_value_type_converted_at_one_of_Blazors_object_contracts_is_accepted(string statement)
    {
        string source = Usings + $$"""
            internal static class Code
            {
                public static void Run(
                    RenderTreeBuilder builder,
                    IJSRuntime js,
                    IJSObjectReference module,
                    IJSInProcessObjectReference inProcessModule,
                    IJSInProcessRuntime inProcessRuntime,
                    Dictionary<string, object> bag,
                    int index,
                    bool flag,
                    ImmutableArray<KeyValuePair<string, object>> pairs)
                {
                    IReadOnlyDictionary<string, object> readOnlyBag;
                    KeyValuePair<string, object> pair;
                    {{statement}}
                }
            }
            """ + UnionShim;

        await Verify(source);
    }

    [Theory]
    [Trait("Requirement", "ADR05-4")]
    [InlineData("object boxed = {|#0:index|};", "index", "int", "object")]
    [InlineData("object boxed = {|#0:(object)index|};", "index", "int", "object")]
    [InlineData("IComparable comparable = {|#0:index|};", "index", "int", "IComparable")]
    [InlineData("IEquatable<int> equatable = {|#0:index|};", "index", "int", "IEquatable<int>")]
    [InlineData("object? boxed = {|#0:maybe|};", "maybe", "int?", "object")]
    [InlineData("ValueType boxed = {|#0:plain|};", "plain", "Plain", "ValueType")]
    [InlineData("Log({|#0:index|});", "index", "int", "object")]
    [InlineData("_ = string.Format(\"{0}\", {|#0:index|});", "index", "int", "object")]
    [InlineData("builder.AddContent(0, {|#0:index|});", "index", "int", "object")]
    [InlineData("IEnumerable<KeyValuePair<string, object>> sequence = {|#0:pairs|};", "pairs", "ImmutableArray<KeyValuePair<string, object>>", "IEnumerable<KeyValuePair<string, object>>")]
    [InlineData("numbered[1] = {|#0:index|};", "index", "int", "object")]
    [InlineData("_ = EventCallback.Empty.Equals({|#0:callback|});", "callback", "EventCallback", "object")]
    [InlineData("object boxed = {|#0:Pass(plain)|};", "Pass(plain)", "Plain", "object")]
    public async Task A_value_type_converted_to_object_or_an_interface_elsewhere_is_an_error(string statement, string operand, string type, string target)
    {
        string source = Usings + $$"""
            internal static class Code
            {
                public static void Run(
                    RenderTreeBuilder builder,
                    Dictionary<int, object> numbered,
                    int index,
                    int? maybe,
                    Plain plain,
                    EventCallback callback,
                    ImmutableArray<KeyValuePair<string, object>> pairs)
                {
                    {{statement}}
                }

                private static void Log(object value) { }

                private static Plain Pass(Plain value) => value;
            }
            """ + UnionShim;

        await Verify(source, ValueError(0, operand, type, target));
    }

    // The compiler turns `"a" + value` into a constrained `value.ToString()` call, not a box: a type that overrides
    // ToString allocates nothing; an enum or a struct without its own ToString is boxed by that call at run time.
    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task String_concatenation_of_a_value_type_with_its_own_ToString_is_accepted()
    {
        const string source = Usings + """
            internal static class Code
            {
                public static string Run(int index, bool flag, char letter, int? maybe, Labeled labeled, string text)
                {
                    text += index;
                    return "a" + index + flag + letter + maybe + labeled + text + index + "b";
                }
            }
            """ + UnionShim;

        await Verify(source);
    }

    [Theory]
    [Trait("Requirement", "ADR05-4")]
    [InlineData("_ = \"a\" + {|#0:reason|};", "reason", "Reason", "object")]
    [InlineData("_ = {|#0:reason|} + \"a\";", "reason", "Reason", "object")]
    [InlineData("_ = \"a\" + {|#0:maybeReason|};", "maybeReason", "Reason?", "object")]
    [InlineData("_ = \"a\" + {|#0:plain|};", "plain", "Plain", "object")]
    [InlineData("text += {|#0:reason|};", "reason", "Reason", "object")]
    public async Task String_concatenation_of_an_enum_or_a_struct_without_its_own_ToString_is_an_error(string statement, string operand, string type, string target)
    {
        string source = Usings + $$"""
            internal static class Code
            {
                public static void Run(Reason reason, Reason? maybeReason, Plain plain, string text)
                {
                    {{statement}}
                }
            }
            """ + UnionShim;

        await Verify(source, ValueError(0, operand, type, target));
    }

    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task A_struct_constrained_type_parameter_converted_to_object_is_an_error()
    {
        const string source = Usings + """
            internal static class Code
            {
                public static object Box<T>(T value)
                    where T : struct
                {
                    return {|#0:value|};
                }

                public static object Reference<T>(T value)
                    where T : class
                {
                    return value;
                }
            }
            """ + UnionShim;

        await Verify(source, ValueError(0, "value", "T", "object"));
    }

    [Theory]
    [Trait("Requirement", "ADR05-4")]
    [InlineData("object boxed = {|#0:choice|};", "choice", "Choice", "object")]
    [InlineData("object boxed = {|#0:(object)choice|};", "choice", "Choice", "object")]
    [InlineData("IUnion union = {|#0:choice|};", "choice", "Choice", "IUnion")]
    [InlineData("object? boxed = {|#0:maybeChoice|};", "maybeChoice", "Choice", "object")]
    [InlineData("object boxed = {|#0:marked|};", "marked", "Marked", "object")]
    [InlineData("builder.AddAttribute(0, \"data-choice\", {|#0:choice|});", "choice", "Choice", "object")]
    [InlineData("builder.AddComponentParameter(0, \"Value\", {|#0:choice|});", "choice", "Choice", "object")]
    [InlineData("bag[\"data-choice\"] = {|#0:choice|};", "choice", "Choice", "object")]
    [InlineData("_ = js.InvokeVoidAsync(\"apply\", {|#0:choice|});", "choice", "Choice", "object")]
    public async Task A_union_converted_to_object_or_an_interface_is_an_error_even_at_an_allowed_site(string statement, string operand, string union, string target)
    {
        string source = Usings + $$"""
            internal static class Code
            {
                public static void Run(RenderTreeBuilder builder, IJSRuntime js, Dictionary<string, object> bag, Choice choice, Choice? maybeChoice, Marked marked)
                {
                    {{statement}}
                }
            }
            """ + UnionShim;

        await Verify(source, UnionError(0, operand, union, target));
    }

    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task A_declared_union_converted_to_object_is_an_error()
    {
        const string source = """
            using System;

            public union Pet(string, Uri);

            internal static class Code
            {
                public static object Run(Pet pet) => {|#0:pet|};
            }

            namespace System.Runtime.CompilerServices
            {
                [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
                public sealed class UnionAttribute : Attribute { }

                public interface IUnion
                {
                    object? Value { get; }
                }
            }
            """;

        await Verify(source, languageVersion: LanguageVersion.Preview, skipSuppressionCheck: false, UnionError(0, "pet", "Pet", "object"));
    }

    [Theory]
    [Trait("Requirement", "ADR05-4")]
    [InlineData("_ = {|#0:reason.ToString()|};", "reason.ToString()", "Reason", "use a `switch` over its members instead")]
    [InlineData("_ = {|#0:reason.ToString(\"D\")|};", "reason.ToString(\"D\")", "Reason", "use a `switch` over its members instead")]
    [InlineData("_ = {|#0:modes.HasFlag(Modes.Hover)|};", "modes.HasFlag(Modes.Hover)", "Modes", "test the flag with `(value & flag) != 0` instead")]
    [InlineData("_ = {|#0:reason.Equals(Reason.Press)|};", "reason.Equals(Reason.Press)", "Reason", "compare with `==` instead")]
    [InlineData("_ = {|#0:Enum.GetValues(typeof(Reason))|};", "Enum.GetValues(typeof(Reason))", "Enum", "list the members it needs instead")]
    [InlineData("_ = {|#0:Enum.GetValues<Reason>()|};", "Enum.GetValues<Reason>()", "Reason", "list the members it needs instead")]
    [InlineData("_ = {|#0:Enum.GetNames(typeof(Reason))|};", "Enum.GetNames(typeof(Reason))", "Enum", "list the members it needs instead")]
    [InlineData("_ = {|#0:Enum.GetNames<Modes>()|};", "Enum.GetNames<Modes>()", "Modes", "list the members it needs instead")]
    public async Task A_boxing_member_of_Enum_is_an_error(string statement, string call, string enumName, string advice)
    {
        string source = Usings + $$"""
            internal static class Code
            {
                public static void Run(Reason reason, Modes modes)
                {
                    {{statement}}
                }
            }
            """ + UnionShim;

        await Verify(source, EnumError(0, call, enumName, advice));
    }

    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task A_pragma_suppresses_the_rule()
    {
        const string source = Usings + """
            internal static class Code
            {
                public static object Run(int index, bool flag)
                {
            #pragma warning disable BLZX2005 // the interface contract returns object
                    object first = index;
            #pragma warning restore BLZX2005
                    return {|#0:flag|};
                }
            }
            """ + UnionShim;

        // The testing SDK wraps the source in its own pragma to check suppression, which the source's restore would undo.
        await Verify(source, languageVersion: null, skipSuppressionCheck: true, ValueError(0, "flag", "bool", "object"));
    }

    private static DiagnosticResult UnionError(int location, string operand, string union, string target)
    {
        return Result(location, $"Blazix: `{operand}` converts the union `{union}` to `{target}`, which boxes it and hides its case; pass the case value instead");
    }

    private static DiagnosticResult ValueError(int location, string operand, string type, string target)
    {
        return Result(location, $"Blazix: `{operand}` boxes the `{type}` value into `{target}`; use a typed overload or a generic parameter instead");
    }

    private static DiagnosticResult EnumError(int location, string call, string enumName, string advice)
    {
        return Result(location, $"Blazix: `{call}` boxes the enum `{enumName}`; {advice}");
    }

    private static DiagnosticResult Result(int location, string message)
    {
        return new DiagnosticResult(BoxingAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithMessage(message);
    }

    private static Task Verify(string source, params DiagnosticResult[] expected) => Verify(source, languageVersion: null, skipSuppressionCheck: false, expected);

    private static async Task Verify(string source, LanguageVersion? languageVersion, bool skipSuppressionCheck, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<BoxingAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = s_withComponents,
        };
        if (languageVersion is { } version)
        {
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                CSharpParseOptions options = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
                return solution.WithProjectParseOptions(projectId, options.WithLanguageVersion(version));
            });
        }

        if (skipSuppressionCheck)
        {
            test.TestBehaviors |= TestBehaviors.SkipSuppressionCheck;
        }

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
