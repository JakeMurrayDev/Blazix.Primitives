// netstandard2.0 has no IsExternalInit; records with init accessors need it (the generator never ships this type: it is internal).
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
