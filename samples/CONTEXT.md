# samples

The sample application: a Blazor Web App with one page per ported family, in both render modes, for trying a milestone's families by hand. Decision record 09 (Amendments 2026-10-22) is the contract; this file says how it is built.

## Inputs

- `../src/Blazix.Primitives`, by project reference; the application registers it with `AddBlazixPrimitives()` in both hosts, as `tests/Blazix.Primitives.Fixtures*` does.
- The Base UI documentation's hero demos in the pinned checkout: `reference/base-ui/docs/src/app/(docs)/react/components/<family>/demos/hero/css-modules/` (`index.tsx` is the markup, `index.module.css` the styles) and the documentation's theme tokens in `reference/base-ui/docs/src/css/index.css`.
- `docs/components/<family>.md` and the XML documentation of the parts, for the parameter names the markup is translated to.

## Job

- `Blazix.Primitives.Demo/` (the host): `Components/Pages/DemoPage.razor` is the one route, `/{mode}/{family}`; the page is static (the navigation) and the family's demo is an interactive island with `InteractiveServerRenderMode(prerender: true)` or `InteractiveWebAssemblyRenderMode(prerender: true)` built from the segment, as `tests/Blazix.Primitives.Fixtures` resolves its mode but with prerendering on. `Components/Pages/Home.razor` lists every family with both links. `wwwroot/app.css` holds the documentation's theme tokens and the chrome around the demos. Enhanced navigation is off, so every link is a full page load and a mode always starts from scratch.
- `Blazix.Primitives.Demo.Client/` (the pages): `DemoCatalog.cs` is the list the host and the home page read; `Demos/<Family>Demo.razor` is one family's page and `Demos/<Family>Demo.razor.css` its stylesheet; `Demos/DemoFrame.razor` is the frame they share (heading, mode badge, the hero demo in the documentation's demo box, an optional "Try it" block of variants); `ModeBadge.razor` shows the mode the address asked for and the one the page runs in (`RendererInfo`).
- A page is the hero demo's markup translated to the port's parts (`Meter.Root` becomes `MeterRoot`, `className` becomes `class`, `data-*` attributes stay), wrapped in `<div class="DemoScope">` so the stylesheet has a scoped element to hang on. The stylesheet is the demo's CSS module with the class names kept; two changes are forced by Blazor CSS isolation and are described at the top of each file: the nesting is written out (isolation does not scope nested rules) and every selector starts with `::deep` (the parts render their own elements, which carry no scope attribute). Anything the documentation's demo does not have goes after a "Sample additions" comment or in `app.css`.
- To add a family when its milestone lands: copy the hero demo, add `Demos/<Family>Demo.razor` and `.razor.css`, add a `DemoEntry` to `DemoCatalog`, add a "Try it" block only if the family has behaviour worth trying by hand.

## Outputs

- A running site, `dotnet run --project samples/Blazix.Primitives.Demo` (`http://localhost:5290`). Nothing is packed or published: both projects have `IsPackable` false.
- A build in CI: `build-and-test` runs one `dotnet build` of the host and nothing else from this folder (`.github/CONTEXT.md`).

## Human check

- Every family of every milestone that has closed has a page, in both modes, and each page works in both: the hero demo does what the documentation's does, and a variant that needs interaction (a trigger, an image load, a button's states) responds on Server and on WebAssembly.
- The badge under each title reads `interactive` once the page loads; a page that stays `prerendered` has a mode that did not start.
- No file here references the specification (decision numbers, requirement IDs) in text a user reads in the app, and nothing under `src/` references this folder.
- A defect a page reveals stays visible on its page and is reported with a reproduction; the page is not changed to hide it.
