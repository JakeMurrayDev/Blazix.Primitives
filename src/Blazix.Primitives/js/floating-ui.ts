/*! Floating UI: @floating-ui/core 1.8.0, @floating-ui/dom 1.8.0, @floating-ui/utils 0.2.12 | MIT License | Copyright (c) 2021-present Floating UI contributors | https://floating-ui.com | Full notice: THIRD-PARTY-NOTICES.md */
// The one importer of Floating UI (ADR 03; ADR 04 and ADR 07 Amendments 2026-10-27). The vendored dist files carry no
// legal comment, so the notice above is the port's own; esbuild's `legalComments: 'inline'` keeps it in the output that
// bundles the engine, which is js/positioning.js because `positioning` is this file's only importer (the module-graph
// lint pins both). `.github/scripts/assert-package.mjs` fails a pack whose js/positioning.js lacks it.
//
// Re-exported: the eight engine functions ADR 03 names, the `@floating-ui/utils` helpers Base UI's positioning layer
// imports (`arrow.ts`, `useAnchorPositioning.ts`, `adaptiveOriginMiddleware.ts`) and the engine's types.
export { autoUpdate, computePosition, detectOverflow, flip, limitShift, offset, shift, size } from '@floating-ui/dom';
export type {
  AutoUpdateOptions,
  Boundary,
  ComputePositionConfig,
  ComputePositionReturn,
  Derivable,
  Dimensions,
  ElementRects,
  Middleware,
  MiddlewareData,
  MiddlewareState,
  Padding,
  Placement,
  Rect,
  ReferenceElement,
  Side,
  Strategy,
  VirtualElement,
} from '@floating-ui/dom';
export { clamp, evaluate, getAlignment, getAlignmentAxis, getAxisLength, getPaddingObject, getSide, getSideAxis } from '@floating-ui/utils';
export type { Alignment, Axis, Length, SideObject } from '@floating-ui/utils';
