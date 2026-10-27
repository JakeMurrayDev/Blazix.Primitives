# src/Blazix.Primitives

The library: every part of the port and the machinery the parts share. It is the only project that ships code to consumers.

## Inputs

- `docs/components/<family>.md` for each family folder; `docs/architecture/*.md` for `Rendering/`, `Store/`, `Events/`, `Interop/` and `js/`.
- Decision records 02 (pipeline), 04 (interop boundary), 05 (state store, event details), 08 (naming, member order), 16 (performance).
- The generator (`../Blazix.Primitives.Generators`) and the analyzers (`../Blazix.Primitives.Analyzers`), consumed as analyzers at compile time.
- `node_modules` at the repository root for the TypeScript build (esbuild, `@floating-ui/dom` 1.8.0); `node` at build time only (`BlazixSkipJsBuild=true` with prebuilt outputs skips it).

## Job

Implements the parts as C# classes in the namespace `Blazix.Primitives` (flat `FamilyPart` names, ADR 08), one folder per family (`<Family>/`, no contract file of their own: the component document is their contract), plus `Rendering/`, `Store/`, `Events/` and `Interop/` for the pipeline, the state store, event details and the twins. A mechanism's C# half with no owning family has a folder named after it, with its own `CONTEXT.md` and its architecture document as contract (ADR 09 Amendments 2026-10-22): `Composite/` holds the composite root and item behaviours (CMP); `Floating/` holds floating positioning's public types and the Positioner and Arrow behaviours (FLT; ADR 09 Amendments 2026-10-27), with the interaction and focus-manager behaviours of M4 tracks 3 and 4 beside them. `DirectionProvider/` is a family folder whose contract is shared-utilities.md §4.15.1. `js/<module>.ts` holds the TypeScript modules with their vitest tests (`js/<module>.test.ts`) beside them. The project sets `IsTrimmable` and `IsAotCompatible` (warnings as errors), so reflection fails the build. No `.razor` part files (ADR 02 decision 4).

## Outputs

- `Blazix.Primitives.dll` and the `Blazix.Primitives` NuGet package (library, generator as an analyzer asset, built JavaScript as static web assets, `LICENSE`, `THIRD-PARTY-NOTICES.md`), with `BaseUIVersion` metadata set to the pinned Base UI tag.
- `wwwroot/` is esbuild output (the initializer and `js/<module>.js`): ignored by git, rebuilt by the `BlazixBuildJs` target on every build (or `npm run build:js`), never edited; `js/CONTEXT.md` has the toolchain rules.
- Consumed by every project under `tests/`.

## Human check

- A new type sits in the right namespace and follows the member order of ADR 08 §5; private static fields are `s_camelCase`, other private fields carry no prefix.
- Nothing in the library uses reflection; the build is warning-free.
- A family folder changes together with its component document and its `tests/browser/<family>.spec.ts`.
- `BaseUIVersion` and the package description still name the Base UI pin in `docs/conventions.md` §1.
