# Samples

`Blazix.Primitives.Demo` is a Blazor Web App for trying the ported families by hand: one page per family, each porting the hero demo of the [Base UI documentation](https://base-ui.com/), in Blazor Server and in Blazor WebAssembly. It is built in CI and never packed. Its contract is [CONTEXT.md](CONTEXT.md).

## Run it

You need the .NET 11 SDK and Node, as for the rest of the repository (see the root [README](../README.md)). Run `npm ci` once at the repository root: the library's build runs esbuild from it and the sample's build runs the Tailwind CLI from it.

```bash
dotnet run --project samples/Blazix.Primitives.Demo
```

Open <http://localhost:5290>. The home page lists every family with both links.

| Family | Blazor Server | Blazor WebAssembly |
|---|---|---|
| Separator | <http://localhost:5290/server/separator> | <http://localhost:5290/wasm/separator> |
| Meter | <http://localhost:5290/server/meter> | <http://localhost:5290/wasm/meter> |
| Progress | <http://localhost:5290/server/progress> | <http://localhost:5290/wasm/progress> |
| Button | <http://localhost:5290/server/button> | <http://localhost:5290/wasm/button> |
| Avatar | <http://localhost:5290/server/avatar> | <http://localhost:5290/wasm/avatar> |
| Collapsible | <http://localhost:5290/server/collapsible> | <http://localhost:5290/wasm/collapsible> |

## What a page shows

- The mode is the first segment of the address, `server` or `wasm`. Every page prerenders first. The badge under the title shows the mode that was asked for and flips from *prerendered* to *interactive* once Blazor has taken the page over; the first WebAssembly visit downloads the runtime, later ones use the cache.
- **Hero demo:** the documentation's Tailwind demo, in the documentation's demo box (the page follows the system's colour scheme, as the documentation does).
- **Try it:** variants where the family has behaviour to try: a meter with a slider, a progress bar you can restart or make indeterminate, the button's states (disabled, focusable when disabled, loading, a span instead of a button), an avatar whose source you can change (the status is what `OnLoadingStatusChange` reports), a collapsible you can also drive from outside.
- The hero Avatar loads the documentation's photo from `images.unsplash.com`, so it needs a network connection; offline it shows the fallback initials, which is the fallback working. The "Try it" block uses a local image.
- In a Debug build (the default for `dotnet run`) the library's development warnings are on and go to the server's console output (Blazor Server) or the browser's console (WebAssembly).

## Layout

```
samples/
  Blazix.Primitives.Demo/          the host: routes, layout, the page chrome's styles (wwwroot/app.css), Program.cs
  Blazix.Primitives.Demo.Client/   the pages (Demos/<Family>Demo.razor), the catalog, the frame, Styles/tailwind.css
```
