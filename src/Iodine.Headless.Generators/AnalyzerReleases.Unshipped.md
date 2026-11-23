; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BLZX1001 | Blazix.Primitives | Error | A [PartState] type must be a sealed partial record class
BLZX1002 | Blazix.Primitives | Error | A [PartState] record must be sealed
BLZX1003 | Blazix.Primitives | Error | A [PartState] property must be projectable
BLZX1004 | Blazix.Primitives | Error | A [PartState] record and its containing types must be partial
BLZX1005 | Blazix.Primitives | Error | A [StoreState] type must be a sealed partial record class
BLZX1006 | Blazix.Primitives | Error | A [StoreState] record must be sealed
BLZX1007 | Blazix.Primitives | Error | A [StoreState] record and its containing types must be partial
BLZX1008 | Blazix.Primitives | Error | A [StoreState] record must implement IStoreState of itself
BLZX1009 | Blazix.Primitives | Error | A [StoreState] record must not declare Copy or ShallowEquals
BLZX1010 | Blazix.Primitives | Error | A [StoreState] record must not derive from another record
BLZX1011 | Blazix.Primitives | Warning | A [StoreState] member of a struct type should be comparable without boxing
