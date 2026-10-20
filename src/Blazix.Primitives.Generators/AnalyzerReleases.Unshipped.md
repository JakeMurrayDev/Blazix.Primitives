; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BLZX1001 | Blazix.Primitives | Error | A [PartState] type must be a sealed partial record class
BLZX1002 | Blazix.Primitives | Error | A [PartState] record must be sealed
BLZX1003 | Blazix.Primitives | Error | A [PartState] property must be projectable
BLZX1004 | Blazix.Primitives | Error | A [PartState] record and its containing types must be partial
