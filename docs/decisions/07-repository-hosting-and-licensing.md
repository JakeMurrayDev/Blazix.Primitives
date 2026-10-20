# ADR 07 — Repository, hosting and licensing

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | where the port lives (local folder, git, GitHub), what is committed and what stays ignored, the licence of the port and the attributions it ships |
| Affects | no requirement IDs; `docs/README.md` (project location), ADR 10 (package contents), the hand-over notes of every session |

## Context

The specification was written in `/Users/jakemurray/Repo/BaseUIPort`, a folder that is not a git repository (its `.gitignore` holds only `reference/`), with `.DS_Store` files in the root, `docs/` and `reference/`. Several sessions use that folder as their working directory, so renaming it in place breaks them; git and GitHub do not depend on the folder's name. `docs/` holds the 47 requirement documents, the decision records, the templates and the lint, cross-linked by relative paths that survive only when the tree moves as a unit. The old port's `docs/` mixed audits, a strategy document and a documentation-site project, which the user wants to avoid.

`JakeMurrayDev/Blazix.BaseUI` on GitHub is public, default branch `master`, 825 commits from 2025-12-15 to 2026-08-19, no tags, no package on NuGet; its issues 144–157 hold the upstream-sync strategy and rubric of the old port. No repository named `Blazix.Primitives` exists. The old port's licence is MIT, copyright 2026 Jake Alexander Murray. Base UI is MIT (Material-UI SAS, 2019); the documents quote its diagnostic messages and test titles and the port transcribes its algorithms. Floating UI core, dom and utils are MIT (Floating UI contributors) and ADR 03 bundles them verbatim into the port's JavaScript; their dist files carry no licence banner. Reselect's behaviour is ported from stated semantics (UTL-ALG-04), not copied. MIT requires the notice in every copy or substantial portion, which includes the NuGet package and the JavaScript bundle.

## Decision

1. **A new folder, seeded from this one.** The port lives in `/Users/jakemurray/Repo/Blazix.Primitives`, created by copying `docs/`, `CONTEXT.md`, `reference/` (still ignored) and the files below from `BaseUIPort` after the last decision record of this session is written, so nothing drifts. `git init` with default branch `main`; the first commit is the specification, the glossary, the licence files, the entry files and `.gitignore`. `BaseUIPort` is deleted by the user once the sessions that use it are closed; any edit made there after the copy must be copied again.
2. **GitHub `JakeMurrayDev/Blazix.Primitives`, private until the first package release** (ADR 10), public from then on. `Blazix.BaseUI` stays untouched for now and is archived, with a README pointer to the new repository, after the first package release; its sync-strategy tickets are cited by URL from ADR 15 and are not migrated.
3. **Committed:** `docs/` (the specification: requirement documents, decisions, templates, lint and tools), `CONTEXT.md`, `AGENTS.md` and `CLAUDE.md` (ADR 09), `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `.gitignore`, and from M0 the solution, projects and tests of ADR 09. **Ignored:** `reference/` (the pinned Base UI clone and vendored packages; re-created from the tag named in `docs/conventions.md` §1), `.DS_Store`, `bin/`, `obj/`, `node_modules/`, the esbuild output under `src/Blazix.Primitives/wwwroot/js/` (ADR 04 §4), Playwright reports and test results, IDE user files.
4. **Licence and attributions.** `LICENSE` is MIT, copyright 2026 Jake Alexander Murray. `THIRD-PARTY-NOTICES.md` holds the full MIT texts of Base UI (v1.9.0: behaviour, algorithms, diagnostic messages and test titles reproduced in the documents and the port) and of Floating UI (`@floating-ui/core` 1.8.0, `@floating-ui/dom` 1.8.0, `@floating-ui/utils` 0.2.12: bundled verbatim). Both files are packed into the NuGet package (`PackageLicenseExpression=MIT`, the notices as package content); esbuild writes a legal-comment banner naming Floating UI on the chunk that contains it; the README carries an acknowledgements paragraph. Standing rule: any source that is transcribed or vendored carries its notice; reselect is added only if its code is ever copied.
5. **`docs/` means the specification.** Nothing else goes in it: contributor material lives beside the code it describes (ADR 09), and a documentation site, if one is ever built, gets its own top-level folder. Moving the specification to `docs/spec/` later is one `git mv` with no link breakage and is not done before a second kind of documentation exists.

## Consequences

* Every later decision record names paths relative to the new folder; the shared notes folders of the current sessions keep pointing at `BaseUIPort` until it is deleted.
* A private repository has a monthly GitHub Actions minute allowance; ADR 11 shapes the workflows around it.
* The notices file and the pin move together (ADR 15).

## Verification items

* The esbuild `legalComments` setting keeps the Floating UI banner on the chunk after code splitting (ADR 04 §4); check on the first packaged build.

## Considered options

* `git init` in place now and rename the folder later (no drift, but a stale folder name for the life of the running sessions); rename or move first (kills the running sessions' working directories). The user chose the fresh folder.
* Rename `Blazix.BaseUI` to `Blazix.Primitives` and push the new history beside `master`: GitHub redirects the old URL, but readers meet 825 commits and 198 issues and PRs about a different architecture in one repository.
* Public from the first push: nothing in the repository needs hiding, but the user chose private until the first package release.
* One `LICENSE` with notices appended (GitHub's licence detection and NuGet's expression stop matching); notices only in the README (the package and the bundle would ship without the notice MIT requires).
