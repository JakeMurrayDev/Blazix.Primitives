# ADR 03 — Positioning engine

| | |
|---|---|
| Status | accepted (2026-10-17) |
| Project | Blazix.Primitives |
| Decides | where the anchored-positioning arithmetic comes from and what the port implements on top of it |
| Affects | FLT-ALG-01..08 (satisfied by dependency), FLT-ALG-09..14, FLT-POS-01..17, FLT-DOM-04..10, FLT-ANI-02..04, FLT-I18N-01..03; TRN-DOM-05; PSH `usePopupAutoResize`/`usePopupViewport` measurement writes |

## Context

Base UI delegates placement, collision handling, size and anchor tracking to `@floating-ui/dom` 1.8.0 (`@floating-ui/core` 1.8.0, `@floating-ui/utils` 0.2.12; all MIT, vendored under `reference/vendor/` at the versions in Base UI's lockfile) and adds about 1,100 lines of its own: the middleware pipeline assembly in `internals/useAnchorPositioning.ts`, an `arrow` fork that measures against the floating element (FLT-ALG-09), a `hide` replacement (FLT-ALG-10), `transformOrigin` (FLT-ALG-11), `adaptiveOrigin` (FLT-ALG-12) and the anchor-tracking configuration over `autoUpdate` (FLT-POS-11, FLT-ALG-13). `floating-and-positioning.md` §5.1 transcribes the engine so that a port may "reimplement or substitute an equivalent engine". The test suite asserts pixel results (`translate(10px, 36px)`; resting exactly at `collisionPadding`) and compares the `hide` replacement against the library's own `hide()`. ADR 01 (constraint C1) already places positioning in JavaScript as a frame-rate loop; `@floating-ui/react-dom` is React glue and is not needed. The ESM builds import their siblings as bare specifiers, so shipping them requires a bundler.

## Decision

**Bundle the vendored `@floating-ui/dom` engine into the port's JavaScript and port only Base UI's own layer (option E1).** The port's positioning module imports `computePosition`, `offset`, `flip`, `shift`, `limitShift`, `size`, `autoUpdate` and `detectOverflow` from the vendored 1.8.0 builds and implements, in JavaScript:

* the pipeline assembly and ordering rules FLT-POS-01..08 and FLT-ALG-14 (middleware list rebuilt when an input changes, otherwise kept stable);
* the four Base UI middleware FLT-ALG-09..12;
* anchor tracking per FLT-POS-11/12 and FLT-ALG-13, including the kept-mounted gating (TRN §6.7);
* the output writes FLT-DOM-04..10: `transform`/`top`/`left`, `--anchor-width/height`, `--available-width/height`, `--transform-origin`, the arrow styles, and the rendered `side`/`align`/`anchorHidden` values;
* the logical-side resolution FLT-I18N-01..03, reading the direction the C# side supplies (the `DirectionProvider` value) and the engine's own computed-style check where FLT-ALG-11 requires it.

FLT-ALG-01..08 are satisfied by the dependency and are not re-implemented or re-tested; §5.1 is review material for upgrades. The engine version is pinned to Base UI's lockfile and moves only when the reference pin moves.

## Consequences

* The library's JavaScript needs a real build step (bundling the three floating-ui packages with the port's modules). The toolchain, module layout and loading are fixed in ADR 04.
* The rendered `side`, `align` and `anchorHidden` values change at scroll time. Whether JavaScript writes `data-side`/`data-align`/`data-anchor-hidden` directly and C# mirrors the values into the state object (consumer `ClassValue` functions then lag one round trip on Server), or C# renders them from state (the flip is one round trip late on Server), is the attribute-ownership rule of ADR 04. The CSS variables and `transform` are JavaScript-written in either case.
* Prerender emits the requested `side`/`align`, no `transform`, and the seeded `--available-width: 100vw` / `--available-height: 100vh` (FLT-DOM-05); positioning starts on the first client pass. (C# renders these once; the positioning module owns the attributes from attach, per ADR 04 Amendments 2026-10-18.)
* Tests: `positionerConformanceTests` and `useAnchorPositioning.test.tsx` translate to browser tests (ADR 06); `hideMiddleware.test.ts` becomes a JavaScript unit test against the bundled engine.
* One MIT attribution (floating-ui) ships with the package.

## Considered options

* **Reimplement the engine from §5.1.** No dependency and no bundler, but the DOM platform layer (clipping rects, offset-parent and scale conversion, the `IntersectionObserver` layout-shift trick in `autoUpdate`, iframes, `position: sticky`) is where floating-ui's accumulated fixes live, §5.1 transcribes the core more fully than that layer, and the port's tests would catch only what they assert.
* **Reimplement in C#.** Excluded by ADR 01 C1 and unworkable on Server at scroll frequency.
