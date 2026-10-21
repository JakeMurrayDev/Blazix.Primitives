# ADR 10 — Packaging and versioning

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | the NuGet package id and contents; the version scheme and its relation to the Base UI pin; prerelease labels; the policy for depending on a .NET 11 release candidate until GA; where packages are produced before nuget.org |
| Affects | no requirement IDs; ADR 05 §4 (target framework), ADR 07 (packed licence files), ADR 09 (what is packed), ADR 15 (what a re-pin does to the version) |

## Context

Nothing named Blazix exists on NuGet. .NET 11 is at RC2 (SDK `11.0.100-rc.2`, 2026-10-13) under a go-live licence; GA is expected in November 2026. C# 15 unions need the .NET 11 runtime (ADR 05), so the library cannot multi-target net10.0. The machine has only SDK 10.0.300 and 10.0.301 installed. Base UI releases roughly monthly (v1.9.0 on 2026-10-09). NuGet orders prerelease labels lexically (`alpha` < `beta` < `preview` < `rc`); `preview.N` is the .NET convention. MinVer derives the version from git tags (`v0.1.0`) with an automatic height suffix on untagged commits, so no version number lives in a file. ADR 04 §4 requires the JavaScript build at pack time and a `BlazixSkipJsBuild=true` path with prebuilt outputs.

## Decision

1. **One package, `Blazix.Primitives`**, containing the library, the generator as an analyzer asset (ADR 09), the built JavaScript under `staticwebassets`, `LICENSE` and `THIRD-PARTY-NOTICES.md` (ADR 07). The analyzers project is never packed.
2. **Independent SemVer (option V1).** `0.x.y-preview.N` while components are still being implemented; `1.0.0` when every component document reaches the `implemented` status (ADR 13). The Base UI pin is metadata, not part of the number: the package description says which Base UI release the package ports, an assembly-level `BaseUIVersion` attribute carries the tag, and the README states it. A re-pin (ADR 15) bumps the minor.
3. **Labels.** `preview.N` until every component is implemented, then `rc.N`, then the release. Each milestone of ADR 12 ends with a tag `v0.<n>.0-preview.1`.
4. **Versioning tool.** MinVer with the `v` tag prefix; a release is a tag and nothing else.
5. **SDK policy.** Target `net11.0` from the first commit on the RC2 SDK. `global.json` pins `11.0.100-rc.2.<build>` with `rollForward: latestFeature`, so GA flows in without an edit; package references are pinned to the RC2 band in `Directory.Packages.props` and bumped at GA. **No nuget.org publish before .NET 11 GA.** Until then every `main` build attaches the `dotnet pack` output as a workflow artifact (ADR 11); GitHub Packages is not used.

## Consequences

* The first public release coincides with or follows .NET 11 GA; the repository becomes public at that release (ADR 07).
* Consumers of a preview package need the RC2 (or later) SDK; the README says so.
* The pack job runs the JavaScript build (Node on the pack machine) and separately verifies the `BlazixSkipJsBuild` path (ADR 04 verification item, ADR 11).

## Verification items

* MinVer resolves the version correctly through a `global.json` that rolls forward from RC2 to GA; confirm on the first tagged build (M1). **Verified 2026-10-21 on the first tagged build:** with `global.json` pinning SDK 11.0.100-rc.2.26504.105 (`rollForward` as pinned) and the tags `v0.1.0-preview.1` (941c517) and `v0.2.0-preview.1` (7a687fa) pushed, the manual pack job `skip-js-build.yml` on `main` at 7a687fa (run 39635416192) produced `Blazix.Primitives.0.2.0-preview.1.nupkg`: MinVer read the tag at height 0 and the `v` prefix as configured. The roll-forward to GA is re-checked at the first GA build. [Issue 20](https://github.com/JakeMurrayDev/Blazix.Primitives/issues/20) closed.
* nuget.org accepts a `net11.0` package built with the RC2 SDK (go-live) at the time of the first publish; re-check at GA.

## Considered options

* Mirror Base UI's version (`1.9.0-blazix.N`, then `1.9.0`): a port-only fix needs a new label or misstates the pin, and the port's own compatibility promises get no number.
* `alpha`/`beta` labels (sort below `preview` and say nothing more); a hand-edited version property (drifts and conflicts).
* Multi-targeting net10.0 (excluded by the union types); waiting for GA before any code (RC2 is go-live and the gap is weeks).
* GitHub Packages for previews (authentication for no gain while private).

## Amendments

**2026-10-20, the library's framework dependency (M0 Track 3; chosen by the user the same day).** The library references the `Microsoft.AspNetCore.Components.Web` package on the RC2 band (`Directory.Packages.props`) instead of `FrameworkReference Microsoft.AspNetCore.App`: a Blazor WebAssembly project cannot reference a library that carries that framework reference (NETSDK1082, no runtime pack for `browser-wasm`), so the framework reference of the skeleton (Track 0) would have excluded every WebAssembly consumer. The package is the one runtime dependency of `Blazix.Primitives`; §5's rule that package references are pinned to the RC2 band and bumped at GA applies to it.
