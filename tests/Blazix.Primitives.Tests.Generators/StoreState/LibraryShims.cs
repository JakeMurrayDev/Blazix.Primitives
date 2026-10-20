namespace Blazix.Primitives.Tests.Generators.StoreState;

/// <summary>
/// The library types the generated code references, as source: the generator tests compile against the testing SDK's
/// reference assemblies and never against the library, so the contract the generated code targets is written out here
/// and must match <c>src/Blazix.Primitives/Store</c> member for member.
/// </summary>
internal static class LibraryShims
{
    public const string Source = """
        #nullable enable
        namespace Blazix.Primitives.Store
        {
            [global::System.AttributeUsage(global::System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class StoreStateAttribute : global::System.Attribute
            {
            }

            public interface IStoreState<TSelf>
                where TSelf : class, IStoreState<TSelf>
            {
                static abstract TSelf Copy(TSelf state);

                static abstract bool ShallowEquals(TSelf left, TSelf right);
            }

            public static class Equality
            {
                public static bool Is<T>(T left, T right) => false;
            }

            public class Store<TState>
                where TState : class, IStoreState<TState>
            {
            }
        }
        """;
}
