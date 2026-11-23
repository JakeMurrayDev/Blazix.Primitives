; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
IOD1001 | Iodine.Headless | Error | A [PartState] type must be a sealed partial record class
IOD1002 | Iodine.Headless | Error | A [PartState] record must be sealed
IOD1003 | Iodine.Headless | Error | A [PartState] property must be projectable
IOD1004 | Iodine.Headless | Error | A [PartState] record and its containing types must be partial
IOD1005 | Iodine.Headless | Error | A [StoreState] type must be a sealed partial record class
IOD1006 | Iodine.Headless | Error | A [StoreState] record must be sealed
IOD1007 | Iodine.Headless | Error | A [StoreState] record and its containing types must be partial
IOD1008 | Iodine.Headless | Error | A [StoreState] record must implement IStoreState of itself
IOD1009 | Iodine.Headless | Error | A [StoreState] record must not declare Copy or ShallowEquals
IOD1010 | Iodine.Headless | Error | A [StoreState] record must not derive from another record
IOD1011 | Iodine.Headless | Warning | A [StoreState] member of a struct type should be comparable without boxing
