# tests/Blazix.Primitives.Fixtures.Client

The fixture components, in a Blazor WebAssembly project so the host can render them in both interactive modes.

## Inputs

- `../../src/Blazix.Primitives` (from Track 3 on) and `Microsoft.AspNetCore.Components.WebAssembly` on the RC2 band.
- `docs/components/<family>.md` §13: the scenarios the browser tests need a page for.
- Decision record 06 §2: readiness markers, prerendering off by default.

## Job

Holds one fixture component per scenario, routed under the render-mode segment the host resolves. A fixture renders the library parts with the options a test needs and the readiness marker `data-interactive`. It carries no behaviour of its own that a test could mistake for the library's.

## Outputs

- The client assembly and its WebAssembly bundle, served by `../Blazix.Primitives.Fixtures`.
- The pages `tests/browser/<family>.spec.ts` navigates to.

## Human check

- A fixture needs no code the user of the library would not write.
- It renders identically in both render modes except where ADR 01's relaxation table says otherwise.
- A page exists for each `Test:` row tagged `[L4]` that needs one, and for no other reason.
