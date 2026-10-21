# Avatar

| | |
|---|---|
| Base UI version | 1.9.0 (`ad7ecda`) |
| Source | `packages/react/src/avatar/` |
| Tests | `packages/react/src/avatar/**/*.test.tsx` (3 files) |
| Docs page | `docs/src/app/(docs)/react/components/avatar/page.mdx` |
| Prefix | `AVA` |
| Status | implemented |
| Revised | 2026-10-21 |
| Builds on | none |
| Derived by | none |
| Depends on | [rendering-and-composition](../architecture/rendering-and-composition.md), [transitions-and-mounting](../architecture/transitions-and-mounting.md), [shared-utilities](../architecture/shared-utilities.md) |

## 1. Overview

Avatar shows a user's picture, with a fallback (initials, an icon) for when there is no picture or it has not loaded yet. `Avatar.Root` is a plain `<span>` container. `Avatar.Image` decides whether the picture can be shown and tracks an *image loading status* (`idle` → `loading` → `loaded` | `error`). `Avatar.Fallback` reads that status from the Root and renders while the image is not loaded, optionally only after a delay so that fast loads never flash the fallback.

`Avatar.Image` has two loading modes:

* **Preload mode** (default). The image is fetched by a detached, off-document image object (the *probe*). The `<img>` element is rendered only once the probe reports success, and it animates in and out with the shared transition attributes. Until then only the fallback is in the DOM.
* **Kept-mounted mode** (`keepMounted`). The `<img>` is rendered immediately and loads in place (this is what makes `loading="lazy"` and image-optimizer components work). The status is read from the rendered element itself. The image carries `data-loading`/`data-error` and is `aria-hidden` until it has loaded, so the fallback provides the accessible name in the meantime.

Avatar has no keyboard or pointer behavior, no form participation, and no change-event details. No other component builds on Avatar.

## 2. Anatomy

```
<Avatar.Root>            required, 1          default element: span
  <Avatar.Image>         optional, 0..1       default element: img   (absent from the DOM until loaded, unless keepMounted)
  <Avatar.Fallback>      optional, 0..n       default element: span  (absent from the DOM while the image is loaded)
```

Image and Fallback may appear in either order and at any depth inside Root. The docs recommend placing the Image after the Fallback when the two are stacked in kept-mounted mode, so the image paints on top (`components/avatar/page.mdx` §Stacking).

- **AVA-STR-01** — MUST. `Avatar.Root` MUST render exactly one host element (default `<span>`) and MUST provide the image loading status to every `Avatar.Image` and `Avatar.Fallback` in its subtree.
  Source: `avatar/root/AvatarRoot.tsx:14-41`. Test: [L2] `AvatarRoot.test.tsx` conformance suite "Base UI component API".
- **AVA-STR-02** — MUST. `Avatar.Image` and `Avatar.Fallback` MUST be rendered inside an `Avatar.Root`; rendering either outside one MUST throw the error in AVA-EDGE-01.
  Source: `avatar/root/AvatarRootContext.ts:12-19`, `avatar/image/AvatarImage.tsx:47`, `avatar/fallback/AvatarFallback.tsx:22`. Test: none.
- **AVA-STR-03** — MUST. When no `Avatar.Image` is present, the Root's status MUST remain `'idle'`, so a Fallback MUST render (subject to its delay, AVA-DOM-09) for as long as the Root exists.
  Source: `avatar/root/AvatarRoot.tsx:20`, `avatar/fallback/AvatarFallback.tsx:46`. Test: none directly (`AvatarFallback.test.tsx` "shows the fallback when the delay has elapsed" keeps the Root at `'idle'` through a stubbed Image). Docs: demo `avatar/demos/hero` (second Root has text content only).

Root and Fallback are structural: they render an element with state and forwarded attributes and attach no handlers. Image attaches `load`/`error` handlers only in kept-mounted mode.

## 3. Public API

### 3.1 `Avatar.Root`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `className`, `style`, `render` | see [RND](../architecture/rendering-and-composition.md) §3.0 | — | Functions receive the Root state object. | AVA-API-01 |
| `ref` | DOM handle | — | Receives the host element (`HTMLSpanElement` by default). | AVA-API-01 |
| *other `<span>` attributes and handlers* | — | — | Forwarded to the host element. | AVA-API-01 |

**Consumed (not forwarded to the element):** `className`, `style`, `render`.

- **AVA-API-01** — MUST. `className`, `style`, `render` and `ref` MUST behave per RND-API-01…RND-API-05 using the Root state object; every other parameter MUST be forwarded unchanged to the host element (the Root supplies no attributes of its own).
  Source: `avatar/root/AvatarRoot.tsx:18,34-39`. Test: [L2] `AvatarRoot.test.tsx` conformance suite "Base UI component API".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `imageLoadingStatus` | `ImageLoadingStatus` | The status most recently reported by an `Avatar.Image` (§4), `'idle'` initially and after the Image is removed. |

- **AVA-STA-01** — MUST. The Root state object MUST contain exactly `imageLoadingStatus`.
  Source: `avatar/root/AvatarRoot.tsx:22-24,46-51`. Test: none (type-level: `avatar/Avatar.spec.tsx:7-9`).

#### Data attributes

None — `imageLoadingStatus` is deliberately not projected (AVA-DOM-05).

#### CSS variables

None.

### 3.2 `Avatar.Image`

**Default element:** `<img>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `src` | `string` | — | Image URL. Used by the probe (preload mode) and placed on the `<img>`. | AVA-API-02 |
| `srcSet` | `string` | — | Responsive source set; probe and `<img>`. | AVA-API-02 |
| `sizes` | `string` | — | Responsive sizes; probe and `<img>`. | AVA-API-02 |
| `crossOrigin` | `'anonymous' \| 'use-credentials' \| ''` | — | Forwarded to the `<img>`; also configures the probe. | AVA-API-03 |
| `referrerPolicy` | referrer-policy string | — | Forwarded to the `<img>`; also configures the probe. | AVA-API-03 |
| `keepMounted` | `boolean` | `false` | Render the `<img>` immediately and load in place instead of preloading. | AVA-API-04 |
| `onLoadingStatusChange` | `(status: ImageLoadingStatus) => void` | — | Called when the Image's status changes (§7). | AVA-API-05 |
| `alt` | `string` | `""` | Forwarded; default supplied by the rendering pipeline. | AVA-DOM-07 |
| `onLoad`, `onError` | DOM handlers | — | Forwarded; in kept-mounted mode they run before the internal handlers and may veto them. | AVA-STA-14 |
| `aria-hidden` | boolean/string | — | Forwarded; overrides the kept-mounted default (AVA-ARIA-01). | AVA-ARIA-01 |
| `className`, `style`, `render` | see RND §3.0 | — | Functions receive the Image state object. | AVA-API-06 |
| `ref` | DOM handle | — | Receives the `<img>` (`HTMLImageElement` by default). | AVA-API-06 |
| *other `<img>` attributes and handlers* (`loading`, `width`, `height`, `decoding`, …) | — | — | Forwarded to the `<img>`. | AVA-API-06 |

**Consumed (not forwarded to the element):** `keepMounted`, `onLoadingStatusChange`, `className`, `style`, `render`. (`src`, `srcSet`, `sizes` are re-applied in a fixed position, AVA-DOM-06.)

- **AVA-API-02** — MUST. `src`, `srcSet` and `sizes` MUST be applied both to the probe (preload mode, AVA-ALG-01) and to the rendered `<img>` (AVA-DOM-06). Either `src` or `srcSet` alone is sufficient for a load attempt.
  Source: `avatar/image/AvatarImage.tsx:41-52,155-165`, `avatar/image/useImageLoadingStatus.ts:27,50-58`. Test: [L2] `AvatarImage.test.tsx` "passes native image props to the rendered image", "shows the image when only srcSet is provided", "passes responsive image props to the loading probe".
- **AVA-API-03** — MUST. `crossOrigin` and `referrerPolicy` MUST be forwarded as the `crossorigin` and `referrerpolicy` attributes of the rendered `<img>` and, in preload mode, MUST also configure the probe before its source is set (AVA-ALG-01), so that the probe's request matches the one the `<img>` will make.
  Source: `avatar/image/AvatarImage.tsx:44,170`, `avatar/image/useImageLoadingStatus.ts:46-49`. Test: [L2] `AvatarImage.test.tsx` "passes native image props to the rendered image" (L3 for the probe-configuration order). Docs: CHANGELOG.md v1.0.0-alpha.7 "Support cross origin in useImageLoadingStatus".
  Port note (2026-10-21, M1 track 7, issue 64; ADR 08 §3): `CrossOrigin` and `ReferrerPolicy` are `string?` parameters forwarded verbatim (`null` renders no attribute and configures the probe without CORS). The empty literal of `crossOrigin` is a valid value, and ADR 08 §3 rejects sentinel members, so no enum is declared.
- **AVA-API-04** — MUST. `keepMounted` MUST default to `false` (preload mode). When `true`, the probe MUST NOT be created and the status MUST be derived from the rendered element (AVA-ALG-02).
  Source: `avatar/image/AvatarImage.tsx:36,48-52,62-100`. Test: [L2] `AvatarImage.test.tsx` "mounts the image while loading without preloading it", "loads the image without a detached preload". Docs: `components/avatar/page.mdx` §Optimized and lazy-loaded images; CHANGELOG.md v1.8.0 "Add `keepMounted` prop to `<Avatar.Image>`".
- **AVA-API-05** — MUST. `onLoadingStatusChange` MUST receive exactly one argument, the new `ImageLoadingStatus`; it receives no event or event-details object.
  Source: `avatar/image/AvatarImage.tsx:121-124,197`. Test: [L2] `AvatarImage.test.tsx` "fires when the image loads".
- **AVA-API-06** — MUST. `className`, `style`, `render` and `ref` MUST behave per RND-API-01…RND-API-05 using the Image state object; the consumer handle and the component's internal handle MUST both receive the `<img>`. All other `<img>` parameters MUST be forwarded to the element.
  Source: `avatar/image/AvatarImage.tsx:32-45,167-173`. Test: [L2] `AvatarImage.test.tsx` conformance suite "Base UI component API", "passes native image props to the rendered image".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `imageLoadingStatus` | `ImageLoadingStatus` | This Image's own status (not the Root's copy). |
| `transitionStatus` | `'starting' \| 'ending' \| undefined` | Enter/exit phase of the `<img>` (§9); never `'ending'` in kept-mounted mode. |

- **AVA-STA-02** — MUST. The Image state object MUST contain exactly `imageLoadingStatus` (the Image's own status) and `transitionStatus` (as adjusted by AVA-STA-07).
  Source: `avatar/image/AvatarImage.tsx:147-152,182-187`. Test: none (type-level: `avatar/Avatar.spec.tsx:21-26`).

#### Data attributes

| Attribute | Present when / value | Req |
|---|---|---|
| `data-starting-style` | `""` while `transitionStatus === 'starting'` | AVA-DOM-03 |
| `data-ending-style` | `""` while `transitionStatus === 'ending'` (preload mode only) | AVA-DOM-03 |
| `data-loading` | `""` while status is `'loading'` (kept-mounted mode only) | AVA-DOM-04 |
| `data-error` | `""` while status is `'error'` (kept-mounted mode only) | AVA-DOM-04 |

#### CSS variables

None.

### 3.3 `Avatar.Fallback`

**Default element:** `<span>`. **Accepts `render` element substitution:** yes. **Accepts `className`/`style` as function of state:** yes.

#### Props

| Prop | Type | Default | Description | Req |
|---|---|---|---|---|
| `delay` | `number` (ms) | `0` | How long after mounting to wait before the fallback may show. | AVA-API-07 |
| `className`, `style`, `render` | see RND §3.0 | — | Functions receive the Fallback state object. | AVA-API-08 |
| `ref` | DOM handle | — | Receives the host element (`HTMLSpanElement` by default). | AVA-API-08 |
| *other `<span>` attributes and handlers* (incl. `children`) | — | — | Forwarded. | AVA-API-08 |

**Consumed (not forwarded to the element):** `delay`, `className`, `style`, `render`.

- **AVA-API-07** — MUST. `delay` MUST default to `0` and is a duration in milliseconds; its effect is specified in AVA-DOM-09 and AVA-ANI-07.
  Source: `avatar/fallback/AvatarFallback.tsx:20,54-61`. Test: [L2] `AvatarFallback.test.tsx` "shows the fallback when the delay has elapsed", "shows the fallback immediately when delay is 0".
  Port note (2026-10-21, M1 track 7, issue 64; chosen by the user 2026-10-21, ADR 08 §3 note "durations are `Duration?`"): `Delay` is a `Duration?`, a union of `int` milliseconds and `TimeSpan`; `null` (omitted, the tests' `undefined`) and zero are a zero delay. A negative value follows AVA-EDGE-05.
- **AVA-API-08** — MUST. `className`, `style`, `render` and `ref` MUST behave per RND-API-01…RND-API-05 using the Fallback state object; every other parameter MUST be forwarded to the host element.
  Source: `avatar/fallback/AvatarFallback.tsx:20,41-47`. Test: [L2] `AvatarFallback.test.tsx` conformance suite "Base UI component API".

#### State object

| Field | Type | Meaning |
|---|---|---|
| `imageLoadingStatus` | `ImageLoadingStatus` | The Root's status. |

- **AVA-STA-03** — MUST. The Fallback state object MUST contain exactly `imageLoadingStatus`, equal to the Root's status.
  Source: `avatar/fallback/AvatarFallback.tsx:22,37-39,52`. Test: none (type-level: `avatar/Avatar.spec.tsx:28-33`).

#### Data attributes

None (AVA-DOM-05).

#### CSS variables

None.

### 3.4 Shared types

- `ImageLoadingStatus` = `'idle' | 'loading' | 'loaded' | 'error'`. Source: `avatar/root/AvatarRoot.tsx:44`.
- `Avatar.Root.State` / `AvatarRootState`, `Avatar.Image.State` / `AvatarImageState`, `Avatar.Fallback.State` / `AvatarFallbackState` — the state objects above. `AvatarImageState` and `AvatarFallbackState` extend `AvatarRootState`. Source: `avatar/root/AvatarRoot.tsx:46-58`, `avatar/image/AvatarImage.tsx:182-209`, `avatar/fallback/AvatarFallback.tsx:52-66`.
- `Avatar.Root.Props`, `Avatar.Image.Props`, `Avatar.Fallback.Props` — the prop tables above.
- `AvatarImageDataAttributes` — exported constant namespace: `loading = 'data-loading'`, `error = 'data-error'`, `startingStyle = 'data-starting-style'`, `endingStyle = 'data-ending-style'`. Source: `avatar/image/AvatarImageDataAttributes.ts:1-18`, `avatar/index.ts:7`. No data-attribute namespaces exist for Root or Fallback.
- `TransitionStatus` — see [TRN](../architecture/transitions-and-mounting.md) §3.3.

## 4. State model

Two copies of the status exist:

* the **Image status** — owned by each `Avatar.Image`, computed by the probe (preload mode) or from the rendered element (kept-mounted mode);
* the **Root status** — owned by `Avatar.Root`, a mirror of the Image status that the Fallback reads.

Neither is consumer-owned; there is no `status`/`defaultStatus` pair.

```
Image status:   idle ──(first evaluation, post-commit pre-paint)──► loading ──load──► loaded
                  │                                                    │  ▲             │
                  │                                                    └──error──► error │
                  └──(no src and no srcSet, preload mode)──────────────────────► error   │
                source/config change (AVA-STA-09/12) from any state ──► loading (or loaded/error if already complete)

Root status:    idle ──(Image status becomes non-idle)──► same value as Image ──(Image removed)──► idle
```

- **AVA-STA-04** — MUST. The Image status MUST start as `'idle'`; `'idle'` is never re-entered by an Image once it has left it (later re-evaluations go directly to `'loading'`, `'loaded'` or `'error'`).
  Source: `avatar/image/useImageLoadingStatus.ts:19,43,62`, `avatar/image/AvatarImage.tsx:77-83`. Test: [L2] `AvatarImage.test.tsx` "fires for cached image errors without emitting idle", "resets the status when the src prop changes" (`'loading'`, not `'idle'`).
- **AVA-STA-05** — MUST. The Root status MUST start as `'idle'` and, each time an Image's status changes to a value other than `'idle'`, MUST be set to that value post-commit, pre-paint in the same pass that calls `onLoadingStatusChange` (after the callback).
  Source: `avatar/root/AvatarRoot.tsx:20`, `avatar/image/AvatarImage.tsx:121-130`. Test: [L2] `AvatarFallback.test.tsx` "should not render the children if the image loaded", "should render the fallback if the image fails to load" (L4 for the pre-paint timing).
- **AVA-STA-06** — MUST. When an `Avatar.Image` is removed from the Root, the Root status MUST be reset to `'idle'` post-commit, pre-paint of the removing commit (so a Fallback reappears).
  Source: `avatar/image/AvatarImage.tsx:132-134`. Test: [L2] `AvatarFallback.test.tsx` "shows the fallback when a loaded image is unmounted".
- **AVA-STA-07** — MUST. In kept-mounted mode the Image's `transitionStatus` MUST be reported as unset whenever the underlying transition status is `'ending'`; `'starting'` is reported unchanged. In preload mode the transition status is reported unchanged. Server: relaxed, see §9.
  Source: `avatar/image/AvatarImage.tsx:149-151`. Test: [L2] `AvatarImage.test.tsx` "does not apply data-ending-style with keepMounted".

### 4.1 Preload mode (default)

- **AVA-STA-08** — MUST. In preload mode, when both `src` and `srcSet` are empty or absent, the Image status MUST become `'error'` post-commit, pre-paint without creating a probe.
  Source: `avatar/image/useImageLoadingStatus.ts:27-30`. Test: [L2] `AvatarImage.test.tsx` "applies data-ending-style before unmount" (`src` → `undefined` makes the loaded image leave); `AvatarFallback.test.tsx` "keeps fallback mounted and image unmounted while the image is loading" (mocked `error` for no `src`).
- **AVA-STA-09** — MUST. In preload mode the status MUST be (re-)evaluated with a fresh probe (AVA-ALG-01) post-commit, pre-paint after the first commit and after every commit in which any of `src`, `srcSet`, `sizes`, `crossOrigin`, `referrerPolicy` changed or `keepMounted` became `false`; results (`load`/`error`) delivered by a probe that has been superseded, or after the Image was removed, MUST be ignored.
  Source: `avatar/image/useImageLoadingStatus.ts:22-68` (`isMounted` guard at `:32-41,65-67`, dependency list at `:68`). Test: [L2] `AvatarImage.test.tsx` "fires when the image loads", "fires when the image errors" (L3 for the superseded-probe clause).
- **AVA-STA-10** — MUST. If the probe reports itself complete synchronously right after its source is assigned (cached and decoded image), the status MUST be set to `'loaded'` when its natural width is greater than 0 and to `'error'` otherwise, in the same pre-paint pass, without waiting for an event.
  Source: `avatar/image/useImageLoadingStatus.ts:60-63`. Test: [L3] `AvatarImage.test.tsx` "shows the image immediately for a cached src", "fires for cached image errors without emitting idle"; "does not flash fallback for a cached image during SSR hydration". Docs: CHANGELOG.md v1.4.0 "Fix flash when image is cached".

- **AVA-ALG-01** — MUST. The preload-mode evaluation MUST reproduce this procedure (it runs post-commit, pre-paint; see AVA-STA-09 for when):

```
evaluate(src, srcSet, sizes, crossOrigin, referrerPolicy):
  supersede the previous probe (its pending load/error results are ignored from now on)
  if src is empty/absent and srcSet is empty/absent:
      status := 'error'; return
  probe := new detached image object (not inserted in the document)
  status := 'loading'
  probe.onload  := () => if probe not superseded: status := 'loaded'
  probe.onerror := () => if probe not superseded: status := 'error'
  if referrerPolicy is non-empty: probe.referrerPolicy := referrerPolicy
  probe.crossOrigin := crossOrigin, or null (no CORS) when absent        -- always assigned
  if sizes  is non-empty: probe.sizes  := sizes
  if srcSet is non-empty: probe.srcset := srcSet
  if src    is non-empty: probe.src    := src                           -- source assigned last
  if probe.complete:                                                    -- synchronous fast path
      status := (probe.naturalWidth > 0) ? 'loaded' : 'error'
```

  Status writes made in the same synchronous pass collapse: only the last value is committed (AVA-EVT-02).
  Port note (2026-10-21, M1 track 7, issue 64; chosen by the user 2026-10-21, ADR 04 Amendments 2026-10-21): the probe runs in the `dom` module through the Image's own twin, attached to the Root's element because the `<img>` does not exist before `'loaded'`; under a Root substitute the Root draws a generated id for it, as Button does. A runtime `keepMounted` switch moves the twin between the Root's element and the `<img>`.
  Source: `avatar/image/useImageLoadingStatus.ts:22-68`. Test: [L3] `AvatarImage.test.tsx` "passes responsive image props to the loading probe" (probe `sizes`/`srcset`/`src`), "fires when the image loads", "fires when the image errors", "fires for cached image errors without emitting idle".

### 4.2 Kept-mounted mode

- **AVA-STA-11** — MUST. In kept-mounted mode no probe MUST be created at any time; the rendered `<img>` is the only request for the image.
  Source: `avatar/image/AvatarImage.tsx:48-52`, `avatar/image/useImageLoadingStatus.ts:23-25`. Test: [L2] `AvatarImage.test.tsx` "mounts the image while loading without preloading it", "loads the image without a detached preload".
- **AVA-STA-12** — MUST. In kept-mounted mode the status MUST be (re-)derived from the rendered element (AVA-ALG-02) post-commit, pre-paint after the first commit and after every commit in which any of `keepMounted`, `src`, `srcSet`, `sizes`, `crossOrigin`, `referrerPolicy` changed, and MUST be re-derived when the substitute element parameter (`Render`) changes (a substitute element whose own `src` differs therefore triggers re-derivation).
  Source: `avatar/image/AvatarImage.tsx:62-100`. Test: [L2] `AvatarImage.test.tsx` "resets the status when the src prop changes", "resets the status when a rendered image changes source", "resets the status when the source changes to an unloaded one", "preserves loaded status when the render element changes", "reports an error when there is no source".
  Port note (2026-10-21, M1 track 7, issue 64): the `dom` module derives on attach and then whenever a render changes the element's `src`, `srcset`, `sizes`, `crossorigin` or `referrerpolicy`, read in a `MutationObserver` callback on the applied batch (ADR 01 decision 4, "post-commit, pre-paint — DOM-only work"). This covers a substitute element whose own `src` changes without a re-derivation after every render; a render that changes none of them changes no status, which Base UI's re-derivation would re-set to the same value without a report (AVA-EVT-01). The module reports each such derivation, and C# drops a status equal to its own, because the element's `load` and `error` events reach C# without the module.
- **AVA-STA-13** — MUST. In kept-mounted mode, if the internal DOM handle holds no element when re-derivation runs (a substitute that does not forward the handle), the status MUST be left unchanged, so that a status already reported by the element's `load`/`error` events is not overwritten.
  Source: `avatar/image/AvatarImage.tsx:70-75`. Test: [L2] `AvatarImage.test.tsx` "keeps the status reported by an element that does not forward a ref".
  Port note (2026-10-21, M1 track 7, issue 64): under a substitute callback (`RenderFunction`) the part has no element (ADR 02's relaxation of RND-DOM-20..24), which is this requirement's "no element": nothing derives, and the element's events report through the handlers the callback forwards. The fixture page `avatar-render-function` repeats the title in both render modes.
- **AVA-STA-14** — MUST. In kept-mounted mode the `<img>`'s `load` event MUST set the status to `'loaded'` and its `error` event to `'error'`. The consumer's `onLoad`/`onError` MUST run first, exactly once per event; if the consumer calls `preventBaseUIHandler()` on the event, the status MUST NOT change. In preload mode no internal `load`/`error` handlers are attached (consumer handlers are forwarded unchanged).
  Source: `avatar/image/AvatarImage.tsx:102-119,170`, RND-EVT-02, RND-EVT-03. Test: [L4] `AvatarImage.test.tsx` "derives the status from the rendered element load event", "keeps the image mounted when it fails to load", "calls the user onError handler", "calls the user onLoad handler", "lets a user handler prevent the status update".

- **AVA-ALG-02** — MUST. The kept-mounted re-derivation MUST reproduce this procedure (it runs post-commit, pre-paint; see AVA-STA-12 for when):

```
derive():
  isInitial := (this is the first time derive() runs for this Image instance)
  el := the rendered <img> (internal DOM handle)
  if el is absent: return                                   -- AVA-STA-13
  if not el.complete:
      status := 'loading'; return
  status := (el.naturalWidth > 0) ? 'loaded' : 'error'      -- includes a source-less <img>, which is complete with width 0
  if status == 'loaded' and isInitial:
      mounted flag := true                                  -- skip 'starting', AVA-ANI-04
```

  Source: `avatar/image/AvatarImage.tsx:58,62-100`. Test: [L3] `AvatarImage.test.tsx` "reports an error when there is no source", "renders the image in the server HTML and resolves cached images on hydration", "resets the status when the source changes to an unloaded one", "does not replay the enter animation for a cached image on hydration".
  Port note (2026-10-21, M1 track 7, issue 64): the step "mounted flag := true" is C#'s: `AvatarImage` mounts an image whose first known status is `'loaded'` (from the derivation, or from a `load` event that reached C# before it on Server) and attaches its `transitions` twin as already mounted. At L3 that is the `transitions` module's "does not request a frame or write a transition attribute for an element attached open" (`js/transitions.test.ts`, track 5), so "does not replay the enter animation for a cached image on hydration" is not repeated in `js/dom.test.ts`, where it would pass before this track and fail the red check (ADR 11 §3); it runs at L4 in both modes (AVA-ANI-04).

## 5. Rendered DOM and ARIA

### 5.1 `Avatar.Root` element

| Attribute | Value | Req | Writer |
|---|---|---|---|
| (none of its own) | consumer attributes only | AVA-DOM-08 | C# |

- **AVA-DOM-08** — MUST. The Root element MUST carry no part-supplied attributes (no `role`, no ARIA, no data attributes); only consumer-supplied attributes appear.
  Source: `avatar/root/AvatarRoot.tsx:34-39`, `avatar/root/stateAttributesMapping.ts:1-3`. Test: [L2] `AvatarRoot.test.tsx` conformance suite "Base UI component API".
- **AVA-DOM-05** — MUST. No part (Root, Image, Fallback) MUST emit a data attribute for `imageLoadingStatus` (the custom mapping suppresses the default `data-imageloadingstatus` projection, RND-DOM-07).
  Source: `avatar/root/stateAttributesMapping.ts:1-3`, `avatar/image/AvatarImage.tsx:17-20`, `avatar/fallback/AvatarFallback.tsx:45`. Test: none.

### 5.2 `Avatar.Image` element

| Attribute | Value / present when | Req | Writer |
|---|---|---|---|
| element presence | preload: while the mounted flag is set; kept-mounted: always | AVA-DOM-01, AVA-DOM-02 | C# → JS (convergent write at load) |
| `alt` | consumer value, else `""` | AVA-DOM-07 | C# |
| `sizes`, `srcset`, `src` | consumer values, applied last, absent when not given | AVA-DOM-06 | C# |
| `crossorigin`, `referrerpolicy`, `loading`, … | forwarded | AVA-API-03, AVA-API-06 | C# |
| `data-starting-style` / `data-ending-style` | transition phase | AVA-DOM-03 | JS |
| `data-loading` / `data-error` | kept-mounted only | AVA-DOM-04 | C# |
| `aria-hidden` | kept-mounted: `"true"` until loaded | AVA-ARIA-01 | C# |

- **AVA-DOM-01** — MUST. In preload mode the `<img>` MUST exist in the DOM exactly while the transition mounted flag is set (TRN-ANI-01 with `keepMounted = false`): it MUST NOT exist while the status is `'idle'`, `'loading'` or `'error'` (other than during the exit phase of a previously loaded image), it MUST appear in the commit in which the status becomes `'loaded'`, and after the status leaves `'loaded'` it MUST remain (with `data-ending-style`) until its exit animations finish (AVA-ANI-03).
  Source: `avatar/image/AvatarImage.tsx:54-55,154,167-177`. Test: [L2] `AvatarFallback.test.tsx` "keeps fallback mounted and image unmounted while the image is loading", "keeps only one of image or fallback mounted when switching to image"; `AvatarImage.test.tsx` "triggers enter animation via data-starting-style when mounting", "applies data-ending-style before unmount" (L4 for the exit-animation clause and the load-time visibility of the convergent write).
- **AVA-DOM-02** — MUST. In kept-mounted mode the `<img>` MUST exist in the DOM from the first render, in every status (including server-rendered HTML), and MUST NOT receive `hidden` or any inline style from the component (TRN-DOM-07).
  Source: `avatar/image/AvatarImage.tsx:154,167-177`. Test: [L2] `AvatarImage.test.tsx` "mounts the image while loading without preloading it", "keeps the image mounted when it fails to load", "renders the image in the server HTML and resolves cached images on hydration".
- **AVA-DOM-03** — MUST. The `<img>` MUST carry `data-starting-style=""` while its reported transition status is `'starting'` and `data-ending-style=""` while it is `'ending'` (RND-DOM-11, TRN-DOM-01…03); because of AVA-STA-07, `data-ending-style` never appears in kept-mounted mode.
  Source: `avatar/image/AvatarImage.tsx:17-20,147-152`, `internals/stateAttributesMapping.ts:10-20`. Test: [L4] `AvatarImage.test.tsx` "triggers enter animation via data-starting-style when mounting", "applies data-ending-style before unmount", "does not apply data-ending-style with keepMounted". Docs: CHANGELOG.md v1.2.0 "Add transition attributes".
- **AVA-DOM-04** — MUST. In kept-mounted mode the `<img>` MUST carry `data-loading=""` exactly while the status is `'loading'` and `data-error=""` exactly while it is `'error'` (neither while `'idle'` or `'loaded'`). In preload mode neither attribute is ever emitted, including while the image animates out.
  Source: `avatar/image/AvatarImage.tsx:102-108`. Test: [L2] `AvatarImage.test.tsx` "marks the not-loaded states with data attributes", "keeps the image hidden from assistive technology after an error", "resets the status when the source changes to an unloaded one", "does not apply the not-loaded state attributes without keepMounted".
- **AVA-DOM-06** — MUST. `sizes`, `srcset` and `src` MUST be applied to the `<img>` after every other attribute, in the order `sizes`, `srcset`, `src` (so attributes that configure the request, such as `loading`, `crossorigin`, `referrerpolicy`, `sizes`, `srcset`, are in place before the browser starts fetching on `src`). When one of the three is not given, it MUST be absent from the attribute set entirely (not present with an empty/undefined value), so a substitute callback's own value is not overwritten.
  Source: `avatar/image/AvatarImage.tsx:38-43,155-165,170`. Test: [L2] `AvatarImage.test.tsx` "applies the source props after the ones configuring the request", "does not override source props in a render callback".
  Port note (2026-10-21, M1 track 7, issue 64): the pipeline places part attributes before the consumer's (`src/Blazix.Primitives/Rendering/CONTEXT.md`), so `AvatarImage` hands the pipeline the consumer's attributes with `sizes`, `srcset` and `src` appended, each only when its parameter is not `null`.
- **AVA-DOM-07** — MUST. When rendering the default `<img>` element and the consumer supplies no `alt`, the element MUST carry `alt=""` (RND-STR-06).
  Source: `internals/useRenderElement.tsx:229-231`. Test: none.

### 5.3 `Avatar.Fallback` element

- **AVA-DOM-09** — MUST. The Fallback element MUST exist in the DOM exactly while the Root status is not `'loaded'` AND (`delay === 0` OR the delay has elapsed per AVA-ANI-07). It has no transition phase: it appears and disappears in a single commit, with no `data-starting-style`/`data-ending-style`.
  Source: `avatar/fallback/AvatarFallback.tsx:23,41-47`. Test: [L2] `AvatarFallback.test.tsx` "should not render the children if the image loaded", "should render the fallback if the image fails to load", "shows the fallback when the delay has elapsed", "shows the fallback immediately when delay is 0", "keeps fallback mounted and image unmounted while the image is loading" (L4 for the load-time hide of the convergent write). Docs: CHANGELOG.md v1.3.0 "Remove fallback transition logic and prevent premature image display".
- **AVA-DOM-10** — MUST. The Fallback MUST carry no part-supplied attributes; only consumer attributes and children appear.
  Source: `avatar/fallback/AvatarFallback.tsx:41-47`. Test: [L2] `AvatarFallback.test.tsx` conformance suite "Base UI component API".
  Port note (2026-10-21, M1 track 7, issue 64; ADR 04 Amendments 2026-10-18; chosen by the user 2026-10-21): the Fallback renders one part-supplied attribute, a generated `id` (`base-ui-<n>`) when the consumer supplies none; a consumer `id` is used instead. It is how the `dom` module finds every Fallback of the Root to hide the moment the image loads (the optimistic convergent write of AVA-EDGE-02/03): the Root's context collects the Fallbacks' ids, and the Image sends them, separated by spaces, as `hideWhenLoaded`.

### 5.4 ARIA

- **AVA-ARIA-01** — MUST. In kept-mounted mode the `<img>` MUST carry `aria-hidden="true"` while the status is anything other than `'loaded'` (including `'idle'` in server HTML) and MUST NOT carry `aria-hidden` while `'loaded'`; a consumer-supplied `aria-hidden` (including `false`) MUST override this in every status. In preload mode no `aria-hidden` is added.
  Source: `avatar/image/AvatarImage.tsx:109-111,170`. Test: [L2] `AvatarImage.test.tsx` "hides the image from assistive technology until it loads", "keeps the image hidden from assistive technology after an error", "hides the image from assistive technology again when the source changes", "preserves an explicitly provided aria-hidden value", "renders the image in the server HTML and resolves cached images on hydration". Docs: `components/avatar/page.mdx` §Stacking.
- **AVA-ARIA-02** — MUST. No part MUST add a `role`, an accessible name or any other `aria-*` attribute beyond AVA-ARIA-01: the image's name is the consumer's `alt`, and the fallback's is its text content.
  Source: `avatar/root/AvatarRoot.tsx:34-39`, `avatar/image/AvatarImage.tsx:102-119`, `avatar/fallback/AvatarFallback.tsx:41-47`. Test: [L2] `AvatarImage.test.tsx` "hides the image from assistive technology until it loads" (`getByRole('img', { name: 'Jane Doe' })` from `alt`).

## 6. Interactions

### 6.1 Keyboard

Not applicable — no part handles keyboard input or is focusable.

### 6.2 Pointer and touch

Not applicable — no part handles pointer input. (The only handlers are the kept-mounted `<img>` `load`/`error` listeners, AVA-STA-14.)

### 6.3 Focus management

Not applicable — no part is focusable or moves focus.

### 6.4 Dismissal

Not applicable — no dismissable surface.

## 7. Events and callbacks

| Callback | Signature | Fires when | Reasons | Cancelable |
|---|---|---|---|---|
| `onLoadingStatusChange` (Image) | `(status: ImageLoadingStatus) => void` | post-commit, pre-paint after a commit in which the Image status changed to a non-`'idle'` value | — (no details) | no |
| `onLoad` / `onError` (Image) | DOM handlers | native `load`/`error` on the `<img>` | — | `preventBaseUIHandler()` skips the internal status update (kept-mounted) |

- **AVA-EVT-01** — MUST. `onLoadingStatusChange(status)` MUST be called post-commit, pre-paint after each commit in which the Image status changed, with the new status, before the Root status is updated (AVA-STA-05). It MUST NOT be called with `'idle'`, MUST NOT be called when the status is re-set to the value it already has, and MUST NOT be called when the Image is removed.
  Source: `avatar/image/AvatarImage.tsx:121-134`. Test: [L2] `AvatarImage.test.tsx` "fires when the image loads" (`['loading','loaded']`), "fires when the image errors" (`['loading','error']`), "preserves loaded status when the render element changes" (no call on unchanged status), "derives the status from the rendered element load event".
- **AVA-EVT-02** — MUST. Status values that are overwritten within the same synchronous evaluation pass MUST NOT be reported: a probe or element that is already complete reports only `'loaded'` or `'error'` (never a preceding `'loading'`), and a source-less Image reports only `'error'`.
  Source: `avatar/image/useImageLoadingStatus.ts:43,60-63`, `avatar/image/AvatarImage.tsx:77-83,126-130`. Test: [L3] `AvatarImage.test.tsx` "fires for cached image errors without emitting idle", "reports an error when there is no source" (`['error']`), "resets the status when the source changes to an unloaded one" (`['loaded','loading','error']` — the cached first source never reports `'loading'`).
- **AVA-EVT-03** — MUST. In kept-mounted mode with a substitute that does not forward the DOM handle, only event-driven statuses MUST be reported (no `'loading'` is ever reported, AVA-STA-13).
  Source: `avatar/image/AvatarImage.tsx:70-75,112-117`. Test: [L2] `AvatarImage.test.tsx` "keeps the status reported by an element that does not forward a ref" (`['loaded']`).

There are no `reason` values; Avatar uses no change-event details object ([EVD](../architecture/event-details-and-reasons.md) §3.6).

## 8. Form integration

Not applicable — Avatar is not a form control.

## 9. Lifecycle, mounting and animation

Timing terms are those of conventions §4. The `<img>`'s enter/exit lifecycle is the shared transition machine ([TRN](../architecture/transitions-and-mounting.md) TRN-ALG-01) driven by `open = (Image status === 'loaded')`, with `enableIdleState`, `deferEndingState` and `animateInitialOpen` all `false` (TRN §6.1). An exit-completion watcher (TRN-ALG-04) is enabled only while the status is not `'loaded'`; there is no `onOpenChangeComplete`.

- **AVA-ANI-01** — MUST. The Image's transition machine MUST be driven by `open = (Image status === 'loaded')` with all three machine options `false`; consequently the mounted flag starts `false` (the status starts `'idle'`) and every first appearance of the image, in either mode, goes through `'starting'` unless AVA-ANI-04 applies.
  Source: `avatar/image/AvatarImage.tsx:54-55`, `internals/useTransitionStatus.ts:17-42`. Test: [L4] `AvatarImage.test.tsx` "triggers enter animation via data-starting-style when mounting".
- **AVA-ANI-02** — MUST. The enter phase MUST NOT be awaited: no animation inspection runs while the status is `'loaded'`; `data-starting-style` is removed on the next frame after the appearing commit (TRN-STA-04). Server: relaxed, see §9.
  Source: `avatar/image/AvatarImage.tsx:136-145` (`enabled: !isVisible`). Test: [L4] `AvatarImage.test.tsx` "triggers enter animation via data-starting-style when mounting" (`getAnimations` never called).
- **AVA-ANI-03** — MUST. When the status leaves `'loaded'` while the image is mounted, the image MUST enter `'ending'` in the same commit (preload mode: visible as `data-ending-style`), and the mounted flag MUST be cleared on animations finished for the `<img>` (TRN-ALG-03, inspection on the next frame); in preload mode the `<img>` is then removed (TRN-ANI-03). A return to `'loaded'` before that aborts the wait (TRN-STA-09). Server: relaxed, see §9.
  Source: `avatar/image/AvatarImage.tsx:136-145,154`, `internals/useOpenChangeComplete.tsx:15-27`. Test: [L4] `AvatarImage.test.tsx` "applies data-ending-style before unmount".
- **AVA-ANI-04** — MUST. In kept-mounted mode, when the first re-derivation (AVA-ALG-02) of an Image instance finds the element already complete and loaded (cached image, or loaded before the client took over server HTML), the image MUST be shown without a `'starting'` phase (no `data-starting-style` at any point) — TRN-ANI-35.
  Source: `avatar/image/AvatarImage.tsx:85-89`. Test: [L4] `AvatarImage.test.tsx` "does not replay the enter animation for a cached image on hydration". Docs: `components/avatar/page.mdx` §Server rendering.
- **AVA-ANI-05** — MUST. In kept-mounted mode a status change away from `'loaded'` MUST NOT produce `data-ending-style` (AVA-STA-07); the element instead shows `data-loading`/`data-error` immediately in the same commit, and the mounted flag is still cleared on animations finished so that the next transition to `'loaded'` plays `'starting'`.
  Source: `avatar/image/AvatarImage.tsx:136-152`. Test: [L4] `AvatarImage.test.tsx` "does not apply data-ending-style with keepMounted".
- **AVA-ANI-06** — MUST. When the status becomes `'loaded'`, the Fallback MUST be removed post-commit, pre-paint of the commit that mounted the image (via AVA-STA-05), so that no painted frame contains both the newly appearing image and the Fallback.
  Source: `avatar/image/AvatarImage.tsx:126-130`, `avatar/fallback/AvatarFallback.tsx:46`. Test: [L4] `AvatarFallback.test.tsx` "keeps only one of image or fallback mounted when switching to image"; `AvatarImage.test.tsx` "does not flash fallback for a cached image during SSR hydration", "renders the image in the server HTML and resolves cached images on hydration".
- **AVA-ANI-07** — MUST. Fallback delay: on mount and whenever `delay` changes, after paint: if `delay > 0`, a timeout of `delay` ms MUST be (re)started (replacing any pending one, UTL-EVT-01) whose expiry marks the delay as elapsed; otherwise the delay MUST be marked elapsed immediately. Once elapsed it MUST stay elapsed for the lifetime of the Fallback instance (a later change to a positive `delay` MUST NOT hide a visible Fallback). The pending timeout MUST be cancelled on unmount. The delay is independent of the image status.
  Source: `avatar/fallback/AvatarFallback.tsx:23-35`, `packages/utils/src/useTimeout.ts:19-36`. Test: [L2] `AvatarFallback.test.tsx` "shows the fallback when the delay has elapsed", "shows the fallback when delay changes to 0", "keeps the fallback visible when delay changes from undefined to a number", "keeps the fallback visible across a number -> undefined -> number delay change".
- **AVA-ANI-08** — MUST. With `delay === 0` (including omitted) at first render, the Fallback MUST be rendered in the first commit (no timer, no after-paint wait); a change of `delay` to `0` MUST show the Fallback in the commit that applies the change.
  Source: `avatar/fallback/AvatarFallback.tsx:23,46`. Test: [L2] `AvatarFallback.test.tsx` "shows the fallback immediately when delay is 0", "shows the fallback when delay changes to 0". Docs: CHANGELOG.md v1.7.0 "Show `<Avatar.Fallback>` immediately when `delay={0}`".

**Server relaxations** (decisions/01-hosting-model-and-timing.md, relaxation table):

- AVA-EDGE-02, AVA-EDGE-03 — AVA-EDGE-02/03 row, **met on both modes** by the optimistic convergent write (ADR 04 Amendments): the `dom` module toggles Image and Fallback visibility at load, and C# renders the same values when `loadingStatus` arrives; the status `data-*` attributes (`data-loading`/`data-error`) lag per the RND-STA-01 row.
- AVA-ANI-03 — TRN-ANI-03/09/10 row (Avatar.Image is an animated part, TRN-DOM-04): the `<img>` removal after its exit animation lands in the first render batch after the `transitions` module's completion message, about two round trips later; JS sets `hidden` at animation end meanwhile (optimistic convergent write).
- AVA-ANI-02 — TRN-STA-04/05/07/09 row, met on both modes: the next-frame `data-starting-style`/`data-ending-style` flip is JS-owned with C# mirroring; listed so it is not moved back to C#.
- AVA-STA-07 — RND-STA-01 row for mirrored fields: `transitionStatus` reaches the C# state (and so `ClassValue`/`StyleValue`) one round trip after JS writes the transition attributes.

## 10. Positioning

Not applicable — no floating parts.

## 11. Cross-cutting dependencies

| Mechanism | Doc | Component-specific configuration |
|---|---|---|
| Rendering pipeline (Root) | [RND](../architecture/rendering-and-composition.md) §6.1 | tag `span`; state `{ imageLoadingStatus }`; mapping `imageLoadingStatus → null`; consumer attributes only |
| Rendering pipeline (Image) | RND §6.1 | tag `img` (default `alt=""`); state `{ imageLoadingStatus, transitionStatus }`; mapping `imageLoadingStatus → null` + `transitionStatusMapping`; `props: [kept-mounted status attributes/handlers, consumer attributes, source attributes]`; handles: consumer + internal; `enabled = keepMounted || mounted` |
| Rendering pipeline (Fallback) | RND §6.1 | tag `span`; state `{ imageLoadingStatus }` (Root's); mapping `imageLoadingStatus → null`; `enabled = rootStatus !== 'loaded' && (delay === 0 || delayPassed)` |
| Transition machine (`useTransitionStatus`) | [TRN](../architecture/transitions-and-mounting.md) §6.1 | `open = status === 'loaded'`; all options `false`; kept-mounted override AVA-STA-07 |
| Completion watcher (`useOpenChangeComplete`) | TRN §6.3 | `enabled = !isVisible`, `open = isVisible`, element = `<img>`, `onComplete: if !isVisible → mounted := false` |
| `Timeout` (`useTimeout`) | [UTL](../architecture/shared-utilities.md) UTL-EVT-01…03 | Fallback delay, `delay` ms |
| Stable callback (`useStableCallback`) | UTL | wraps `onLoadingStatusChange` + Root update so the latest consumer callback is used |
| post-commit, pre-paint hook (`useIsoLayoutEffect`) | UTL | probe evaluation, kept-mounted derivation, callback dispatch, Root reset; none run during server rendering |

## 12. Edge cases, invariants and dev warnings

Avatar emits no development warnings. Inherited dev checks (invalid `render` element, uppercase-named render function) are RND-EDGE-01…RND-EDGE-03.

- **AVA-EDGE-01** — MUST. Rendering `Avatar.Image` or `Avatar.Fallback` outside an `Avatar.Root` MUST throw an error with the message `Base UI: AvatarRootContext is missing. Avatar parts must be placed within <Avatar.Root>.` (in all builds).
  Source: `avatar/root/AvatarRootContext.ts:12-19`. Test: none.
- **AVA-EDGE-02** — MUST. Server rendering in preload mode: post-commit hooks do not run on the server, so server HTML MUST contain no `<img>` and MUST contain the Fallback if its `delay` is `0` (none if `delay > 0`). On the client's first pass over that HTML, the probe evaluation MUST run before the first paint, so a cached image appears and the Fallback is removed without a painted fallback frame. Server: relaxed, see §9.
  Source: `avatar/image/useImageLoadingStatus.ts:22`, `avatar/fallback/AvatarFallback.tsx:23`. Test: [L4] `AvatarImage.test.tsx` "does not flash fallback for a cached image during SSR hydration".
- **AVA-EDGE-03** — MUST. Server rendering in kept-mounted mode: server HTML MUST contain the `<img>` with its `src` and `aria-hidden="true"` (status `'idle'`, so no `data-loading`/`data-error`) and the Fallback (if `delay` is `0`); on the client's first pass the derivation MUST run before paint, so an image that finished loading before takeover is shown immediately, unhidden, without `data-starting-style`, and the Fallback is removed. Server: relaxed, see §9.
  Source: `avatar/image/AvatarImage.tsx:60-100,109-111`. Test: [L4] `AvatarImage.test.tsx` "renders the image in the server HTML and resolves cached images on hydration", "does not replay the enter animation for a cached image on hydration". Docs: `components/avatar/page.mdx` §Server rendering.
- **AVA-EDGE-04** — MUST. With more than one `Avatar.Image` in a Root, the Root status MUST be the value most recently reported by any of them, and removing any one MUST reset it to `'idle'` (AVA-STA-06) even if another Image is still loaded. Only one Image per Root is a supported configuration.
  Source: `avatar/image/AvatarImage.tsx:121-134`. Test: none.
- **AVA-EDGE-05** — MUST. A Fallback with a negative or non-numeric `delay` MUST be hidden in its first commit and shown after paint (the delay is treated as elapsed immediately but `delay !== 0`).
  Source: `avatar/fallback/AvatarFallback.tsx:23,26-33,46`. Test: none.
  Port note (2026-10-21, M1 track 7, issue 64): the non-numeric clause has no input in the port, whose `Delay` is a `Duration?` of `int` milliseconds or `TimeSpan` (AVA-API-07); it stays `Test: none` by type. The negative clause has the port's L2 test "shows a fallback with a negative delay right after its first render".
- **AVA-EDGE-06** — MUST. No behavior depends on writing direction or locale.
  Source: `avatar/` (no direction or locale input). Test: none.
- **AVA-EDGE-07** — MUST. Toggling `keepMounted` at runtime MUST switch the evaluation source: to `true` stops honoring any pending probe result and derives from the element (the first derivation after the switch counts as "initial" for AVA-ANI-04); to `false` starts a new probe evaluation (AVA-STA-09) and from then on the element is rendered only while the mounted flag is set (AVA-DOM-01), so an element whose status is not `'loaded'` and whose exit already completed disappears in that same commit.
  Source: `avatar/image/AvatarImage.tsx:48-52,62-100,154`, `avatar/image/useImageLoadingStatus.ts:22-25,68`. Test: none.

## 13. Behaviors evidenced by tests

### `AvatarRoot.test.tsx`

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: `refInstanceof: HTMLSpanElement`; `testRenderPropWith` default `div`) | AVA-DOM-11, AVA-API-01, AVA-STR-01, AVA-DOM-08 |

### `AvatarImage.test.tsx`

All jsdom tests replace the detached image constructor with a mock whose `complete`/`naturalWidth` become `true`/`100` when a source is assigned (cached behavior) unless the test installs a non-completing mock. Browser-only tests use the real constructor.

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: rendered inside `Avatar.Root`, `src="test.png"`, `refInstanceof: HTMLImageElement`) | AVA-DOM-11, AVA-API-06 |
| "passes native image props to the rendered image" | AVA-API-02, AVA-API-03, AVA-API-06 |
| "shows the image when only srcSet is provided" | AVA-API-02, AVA-STA-10, AVA-DOM-09 |
| "passes responsive image props to the loading probe" | AVA-API-02, AVA-ALG-01 |
| "fires when the image loads" / "fires when the image errors" | AVA-EVT-01, AVA-STA-09, AVA-ALG-01, AVA-API-05 |
| "fires for cached image errors without emitting idle" | AVA-EVT-02, AVA-STA-04, AVA-STA-10 |
| "mounts the image while loading without preloading it" | AVA-API-04, AVA-STA-11, AVA-DOM-02 |
| "derives the status from the rendered element load event" | AVA-STA-14, AVA-EVT-01 |
| "keeps the image mounted when it fails to load" | AVA-STA-14, AVA-DOM-02 |
| "calls the user onError handler" / "calls the user onLoad handler" | AVA-STA-14 |
| "lets a user handler prevent the status update" | AVA-STA-14 |
| "resets the status when a rendered image changes source" | AVA-STA-12 |
| "resets the status when the src prop changes" | AVA-STA-12, AVA-STA-04 |
| "renders the image in the server HTML and resolves cached images on hydration" (browser) | AVA-EDGE-03, AVA-ARIA-01, AVA-DOM-02, AVA-ANI-06, AVA-ALG-02 |
| "loads the image without a detached preload" (browser) | AVA-STA-11, AVA-API-04 |
| "reports an error when there is no source" (browser) | AVA-ALG-02, AVA-EVT-02, AVA-STA-12 |
| "preserves loaded status when the render element changes" (browser) | AVA-STA-12, AVA-EVT-01 |
| "hides the image from assistive technology until it loads" | AVA-ARIA-01, AVA-ARIA-02 |
| "keeps the image hidden from assistive technology after an error" | AVA-ARIA-01, AVA-DOM-04 |
| "hides the image from assistive technology again when the source changes" | AVA-ARIA-01, AVA-STA-12 |
| "preserves an explicitly provided aria-hidden value" | AVA-ARIA-01 |
| "marks the not-loaded states with data attributes" | AVA-DOM-04 |
| "does not override source props in a render callback" | AVA-DOM-06 |
| "applies the source props after the ones configuring the request" | AVA-DOM-06 |
| "keeps the status reported by an element that does not forward a ref" (browser) | AVA-STA-13, AVA-EVT-03 |
| "resets the status when the source changes to an unloaded one" (browser) | AVA-STA-12, AVA-ALG-02, AVA-DOM-04, AVA-EVT-02 |
| "triggers enter animation via data-starting-style when mounting" (browser) | AVA-DOM-01, AVA-DOM-03, AVA-ANI-01, AVA-ANI-02 |
| "applies data-ending-style before unmount" (browser) | AVA-DOM-01, AVA-DOM-03, AVA-ANI-03, AVA-STA-08 |
| "does not apply the not-loaded state attributes without keepMounted" (browser) | AVA-DOM-04 |
| "does not apply data-ending-style with keepMounted" (browser) | AVA-STA-07, AVA-DOM-03, AVA-ANI-05 |
| "does not replay the enter animation for a cached image on hydration" (browser) | AVA-ANI-04, AVA-EDGE-03, AVA-ALG-02 |
| "does not flash fallback for a cached image during SSR hydration" (browser) | AVA-EDGE-02, AVA-STA-10, AVA-ANI-06 |
| "shows the image immediately for a cached src" | AVA-STA-10 |

### `AvatarFallback.test.tsx`

All tests replace the Image's status computation with a stub returning a fixed status per `src` (default `'idle'`), so they exercise only the Root mirror and the Fallback.

| Test | Requirements |
|---|---|
| Conformance suite "Base UI component API" (options: rendered inside `Avatar.Root`, `refInstanceof: HTMLSpanElement`) | AVA-DOM-11, AVA-API-08, AVA-DOM-10 |
| "should not render the children if the image loaded" | AVA-STA-05, AVA-DOM-09 |
| "should render the fallback if the image fails to load" | AVA-STA-05, AVA-DOM-09 |
| "shows the fallback when a loaded image is unmounted" | AVA-STA-06 |
| "shows the fallback when the delay has elapsed" | AVA-ANI-07, AVA-API-07, AVA-STR-03, AVA-DOM-09 |
| "shows the fallback immediately when delay is 0" | AVA-ANI-08, AVA-DOM-09 |
| "shows the fallback when delay changes to 0" | AVA-ANI-08, AVA-ANI-07 |
| "keeps the fallback visible when delay changes from undefined to a number" | AVA-ANI-07 |
| "keeps the fallback visible across a number -> undefined -> number delay change" | AVA-ANI-07 |
| "keeps fallback mounted and image unmounted while the image is loading" | AVA-DOM-01, AVA-DOM-09, AVA-STA-08 |
| "keeps only one of image or fallback mounted when switching to image" (browser) | AVA-ANI-06, AVA-DOM-01 |

### Additional requirements introduced here

- **AVA-DOM-11** — MUST. All three parts MUST pass the shared conformance suite RND-DOM-36…RND-DOM-48 (default `div` substitute; DOM handle `HTMLSpanElement` for Root and Fallback, `HTMLImageElement` for Image when not substituted). Image is tested in a loaded state (cached `src`), Fallback in the `'idle'` state, both inside a Root.
  Source: `test/describeConformance.tsx:55-76`. Test: [L2] `AvatarRoot.test.tsx`, `AvatarImage.test.tsx`, `AvatarFallback.test.tsx` "Base UI component API".

## 14. React-specific notes and porting considerations

Non-normative.

- **The probe.** Preload mode uses `new window.Image()`, an off-document `HTMLImageElement`. A Blazor port needs a small script-interop helper that creates the probe, configures it in AVA-ALG-01 order, checks `complete`/`naturalWidth` synchronously and reports `load`/`error` back; a Rust/WASM port can create the element through the DOM bindings. The probe must be "superseded" (callbacks ignored) rather than aborted; browsers offer no portable abort. Port: see decisions/04-js-interop-boundary.md §1 (`dom` module: "Avatar image load" owns the probe and the `complete`/`naturalWidth` reads of AVA-ALG-01/02; the status itself is C# state).
- **Pre-paint evaluation.** The probe evaluation, the kept-mounted derivation, the callback dispatch and the Root mirror all run in layout effects (post-commit, pre-paint) and React flushes state set there before painting. That is what makes cached images appear with no fallback flash (AVA-ANI-06, AVA-EDGE-02). A framework that only offers "after render" hooks that run after paint (for example Blazor's `OnAfterRenderAsync` with interop) will paint one frame of the fallback for cached images; ports should document that or render the image's initial state from a synchronous check where possible. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 ("post-commit, pre-paint — C# state work" row: met on WebAssembly, after paint plus one round trip on Server); AVA-EDGE-02's no-flash clause is not in the Relaxation table (§16 item 8).
- **Batched status writes.** `setLoadingStatus('loading')` followed by `setLoadingStatus('loaded')` in the same effect commits only `'loaded'`, so `'loading'` is never reported for cached images (AVA-EVT-02). A port that dispatches callbacks on every write must coalesce. Port: see decisions/04-js-interop-boundary.md §3 (the JS twin sends one message per state change, so the synchronous fast path reports only the final status).
- **Server rendering.** Layout effects never run on the server, so the server always sees status `'idle'` (no `<img>` in preload mode; `aria-hidden` `<img>` in kept-mounted mode). The kept-mounted `isInitialCommit` flag exists so that an image the browser loaded before hydration does not replay its enter animation. Port: see decisions/04-js-interop-boundary.md §2 (`data-starting-style`/`data-ending-style` are JS-written; C# mirrors `transitionStatus`).
- **`render` in the dependency list.** A substitute element is a new object on every parent render, so the kept-mounted derivation re-runs on every parent render when `render` is an element. That is harmless (it reads the element and re-sets the same status) and is how a substitute's own `src` change is detected (AVA-STA-12). A port can instead re-derive after every render.
- **Attribute order.** The `sizes`/`srcSet`/`src` split exists because React 17/18 set attributes in object-key order and Safari/Firefox start fetching when `src` is set, ignoring `loading`/`srcset` set later. Any port that builds DOM attributes incrementally must set `src` last (AVA-DOM-06). Port: the `<img>` attributes, including `crossorigin`/`referrerpolicy`, are C#-rendered through the pipeline of decisions/02-render-pipeline-and-attribute-merging.md §Decision 4; the probe's configuration order belongs to the `dom` module (decisions/04-js-interop-boundary.md §1).
- **Stable callback.** `onLoadingStatusChange` is read through a stable trampoline, so changing the callback prop does not re-fire it.
- **Effect timing of the delay.** The Fallback delay timer starts in a passive effect (after paint), so the delay is measured from slightly after the first paint, not from render. Port: see decisions/01-hosting-model-and-timing.md §Decision item 4 ("macrotask / N ms" row: a C# timer, since the result is state).

## 15. Requirement index

| ID | Level | Statement (abbreviated) | Section |
|---|---|---|---|
| AVA-STR-01 | MUST | Root renders one `span`, provides status | 2 |
| AVA-STR-02 | MUST | Image/Fallback must be inside Root | 2 |
| AVA-STR-03 | MUST | no Image → Root stays `idle`, Fallback shows | 2 |
| AVA-API-01 | MUST | Root: RND params, all attrs forwarded | 3.1 |
| AVA-API-02 | MUST | `src`/`srcSet`/`sizes` to probe and `<img>` | 3.2 |
| AVA-API-03 | MUST | `crossOrigin`/`referrerPolicy` on `<img>` and probe | 3.2 |
| AVA-API-04 | MUST | `keepMounted` default false; no probe when true | 3.2 |
| AVA-API-05 | MUST | `onLoadingStatusChange(status)` single argument | 3.2 |
| AVA-API-06 | MUST | Image: RND params, both handles, attrs forwarded | 3.2 |
| AVA-API-07 | MUST | `delay` default 0 ms | 3.3 |
| AVA-API-08 | MUST | Fallback: RND params, attrs forwarded | 3.3 |
| AVA-STA-01 | MUST | Root state `{ imageLoadingStatus }` | 3.1 |
| AVA-STA-02 | MUST | Image state `{ imageLoadingStatus, transitionStatus }` | 3.2 |
| AVA-STA-03 | MUST | Fallback state `{ imageLoadingStatus }` (Root's) | 3.3 |
| AVA-STA-04 | MUST | Image status starts `idle`, never re-enters it | 4 |
| AVA-STA-05 | MUST | Root mirrors non-idle Image status pre-paint, after callback | 4 |
| AVA-STA-06 | MUST | Image removal resets Root to `idle` | 4 |
| AVA-STA-07 | MUST | kept-mounted reports `ending` as unset | 4 |
| AVA-STA-08 | MUST | preload: no src/srcSet → `error`, no probe | 4.1 |
| AVA-STA-09 | MUST | preload: re-evaluate on source/config change; stale results ignored | 4.1 |
| AVA-STA-10 | MUST | synchronous complete fast path by naturalWidth | 4.1 |
| AVA-STA-11 | MUST | kept-mounted: never a probe | 4.2 |
| AVA-STA-12 | MUST | kept-mounted: re-derive on source/config/substitute-element change | 4.2 |
| AVA-STA-13 | MUST | kept-mounted: no element → status untouched | 4.2 |
| AVA-STA-14 | MUST | kept-mounted: load/error events set status; consumer first, can veto | 4.2 |
| AVA-DOM-01 | MUST | preload: `<img>` present only while mounted flag | 5.2 |
| AVA-DOM-02 | MUST | kept-mounted: `<img>` always present, no hidden/style | 5.2 |
| AVA-DOM-03 | MUST | transition data attributes; no ending when kept-mounted | 5.2 |
| AVA-DOM-04 | MUST | `data-loading`/`data-error` kept-mounted only | 5.2 |
| AVA-DOM-05 | MUST | no `imageLoadingStatus` data attribute on any part | 5.1 |
| AVA-DOM-06 | MUST | `sizes`,`srcset`,`src` applied last; absent when undefined | 5.2 |
| AVA-DOM-07 | MUST | default `alt=""` | 5.2 |
| AVA-DOM-08 | MUST | Root has no part-supplied attributes | 5.1 |
| AVA-DOM-09 | MUST | Fallback present iff not loaded and delay satisfied | 5.3 |
| AVA-DOM-10 | MUST | Fallback has no part-supplied attributes | 5.3 |
| AVA-DOM-11 | MUST | all parts pass RND conformance suite | 13 |
| AVA-ARIA-01 | MUST | kept-mounted `aria-hidden="true"` until loaded; consumer wins | 5.4 |
| AVA-ARIA-02 | MUST | no roles/names added | 5.4 |
| AVA-EVT-01 | MUST | callback pre-paint per change, never `idle`, not on removal | 7 |
| AVA-EVT-02 | MUST | overwritten same-pass statuses not reported | 7 |
| AVA-EVT-03 | MUST | no-handle substitute: only event-driven statuses | 7 |
| AVA-ANI-01 | MUST | transition machine `open = loaded`, options false | 9 |
| AVA-ANI-02 | MUST | enter not awaited; starting cleared next frame | 9 |
| AVA-ANI-03 | MUST | exit: ending, unmount on animations finished | 9 |
| AVA-ANI-04 | MUST | kept-mounted initial cached load skips starting | 9 |
| AVA-ANI-05 | MUST | kept-mounted: no ending attr; data-loading/error instead | 9 |
| AVA-ANI-06 | MUST | Fallback removed pre-paint when image appears | 9 |
| AVA-ANI-07 | MUST | delay timer semantics; sticky once elapsed | 9 |
| AVA-ANI-08 | MUST | `delay=0` renders in first commit / on change | 9 |
| AVA-EDGE-01 | MUST | missing Root throws (message) | 12 |
| AVA-EDGE-02 | MUST | SSR preload mode markup and hydration | 12 |
| AVA-EDGE-03 | MUST | SSR kept-mounted markup and hydration | 12 |
| AVA-EDGE-04 | MUST | multiple Images: last report wins; removal resets | 12 |
| AVA-EDGE-05 | MUST | negative/NaN delay: hidden first commit, shown after paint | 12 |
| AVA-EDGE-06 | MUST | no RTL/locale dependence | 12 |
| AVA-EDGE-07 | MUST | runtime `keepMounted` toggle | 12 |
| AVA-ALG-01 | MUST | preload probe procedure | 4.1 |
| AVA-ALG-02 | MUST | kept-mounted derivation procedure | 4.2 |

## 16. Open questions

1. **Assignment hint "re-evaluation when `src` changes resets to `idle`".** It does not: re-evaluation goes straight to `'loading'` (or `'loaded'`/`'error'`), `avatar/image/useImageLoadingStatus.ts:43`. `'idle'` is only the initial value and the Root's value after the Image is removed (AVA-STA-04, AVA-STA-06).
2. **Preload-mode `src` change while loaded.** A new `src` sets status `'loading'`, so the currently displayed image plays its exit and is removed, and the Fallback reappears until the new image loads (`avatar/image/AvatarImage.tsx:54-55,136-145`). Not tested for preload mode; consistent with the kept-mounted tests. Flag if a cross-fade was intended.
3. **Default-mode cached images do play `'starting'`.** Only kept-mounted mode suppresses the enter phase for an already-loaded image (`AvatarImage.tsx:85-89`); in preload mode even a cached image or a hydrated page gets one frame of `data-starting-style` (AVA-ANI-01). TRN-ANI-35 describes only the kept-mounted case; the preload-mode hydration test ("does not flash fallback for a cached image during SSR hydration") asserts the fallback, not the attribute.
4. **Brief double exposure in preload mode on failure after success.** When a loaded image's status becomes `'error'`/`'loading'`, the Root status changes immediately (Fallback renders) while the `<img>` remains during its exit animation without `aria-hidden`; both are exposed to assistive technology for the duration of the exit. Kept-mounted mode avoids this with `aria-hidden` (AVA-ARIA-01). Not tested.
5. **Root reset with multiple Images** (AVA-EDGE-04) and the **negative `delay`** behavior (AVA-EDGE-05) follow from the code but are untested and undocumented.
6. **Outline completeness.** The API outline (`components/page.mdx` Avatar entry) lists types `Avatar.*.Props`/`State` but not `ImageLoadingStatus` or `AvatarImageDataAttributes`, both of which are exported (`avatar/index.ts:3-7`) and listed in `types.md`. It also omits the forwarded native `src`/`srcSet`/`sizes`/`crossOrigin`/`referrerPolicy` that Image treats specially.
7. **No `delay` on Image.** Only the Fallback is delayed; the image appears as soon as it loads. The CHANGELOG v1.3.0 entry "prevent premature image display" refers to the image not being rendered before load, not to a delay.
8. **Server timing of the cached-image path.** The probe/derivation, `onLoadingStatusChange` and the Root mirror run post-commit, pre-paint (`avatar/image/useImageLoadingStatus.ts:22`, `avatar/image/AvatarImage.tsx:62,126,132`). Under ADR 01 Decision item 4 this is C# state work fed by the `dom` module (ADR 04 §1), so on Server the first client paint over prerendered HTML always shows the Fallback for a cached image, contradicting the no-flash clauses of AVA-EDGE-02 and AVA-EDGE-03 (tested by `AvatarImage.test.tsx` "does not flash fallback for a cached image during SSR hydration"). ADR 01's Relaxation table has no Avatar row; needs a decision (add a relaxation row, or have the `dom` module hide the Fallback, which would break single-writer ownership). Decided (2026-10-18): the optimistic convergent write is extended to the Avatar load status, so AVA-EDGE-02/03 hold on both modes (ADR 04 Amendments; ADR 01 relaxation table row).
9. **Port points of the implementation (M1 track 7, issue 64), decided 2026-10-21.** (a) The Fallback locator for the convergent write against AVA-DOM-10: a generated id, the ids sent space-separated (chosen by the user; the alternatives were Blazor's element-reference attribute and a single id). (b) Where the Image's `dom` twin attaches in preload mode: the Root's element (chosen by the user; ADR 04 Amendments 2026-10-21). (c) The C# type of `crossOrigin`: `string?` (settled by ADR 08 §3). (d) The C# type of `delay`: `Duration?`, a union of `int` milliseconds and `TimeSpan` (chosen by the user; ADR 08 §3 note of 2026-10-21).
10. **Resolution of items 1–7 (2026-10-21, M1 track 7).** Items 1, 2, 3 and 7 are implemented as stated and tested: the Image never reports `'idle'` (L2 "fires for cached image errors without emitting idle", "resets the status when the src prop changes"); a preload `src` change plays the exit and the Fallback returns (L2 "does not apply the not-loaded state attributes without keepMounted", L4 "applies data-ending-style before unmount"); a preload image, cached or not, enters through `'starting'` (L4 "triggers enter animation via data-starting-style when mounting"). Item 4 is replicated as Base UI has it. Item 5: AVA-EDGE-04 and AVA-EDGE-05 have port tests (L2 "takes the latest report of several images and resets the root when one is removed", "shows a fallback with a negative delay right after its first render"; L1 `AvatarFallbackDelayTests`), as do AVA-STR-03, AVA-DOM-05, AVA-DOM-07, AVA-EDGE-01 and AVA-EDGE-07 (L2, the port's titles), whose `Test:` lines stay `none` because Base UI has no test to cite. Item 6 is informational: the port exports `ImageLoadingStatus` (ADR 08 §3 note of 2026-10-21) and `AvatarImageDataAttributes`.
