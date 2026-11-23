# ADR 17 — Product name and package identity

| | |
|---|---|
| Status | accepted (2026-11-23) |
| Project | Blazix.Primitives (Iodine.Headless from the rename pull request on) |
| Decides | the product name, the package id and root namespace, the repository layout of the product family, the reach and timing of the rename, the folder on disk, and when the NuGet prefix is reserved |
| Affects | no requirement IDs; amends ADR 07 (repository name), ADR 08 (root namespace), ADR 09 (project names), ADR 10 (package id) by the dated amendments the rename pull request writes |

## Context

An unrelated project, `HGCollier/Blazix` (created 2026-11-21, MIT, docs at blazix-ui.com), ports Base UI to Blazor under the same name. On 2026-11-23 its owner, `HGCollier`, held nine NuGet ids: `Blazix.Primitives` (versions 0.1.0 to 0.3.0, the id of ADR 10 decision 1), `Blazix.Cli`, `Blazix.Icons` and six `Blazix.Icons.*`. Its library uses the root namespace `Blazix.Primitives`, the same as ADR 08's. Nothing of this port is published (ADR 10). The `Blazix.*` prefix is not reserved, so either party's later `Blazix.*` ids stay open to the other, and the namespace clash means a consumer cannot reference both packages without aliases. The name appears 8,950 times in 2,197 of 2,624 tracked files and in 2,229 tracked paths, and in 18 `BLZX` diagnostic ids.

The user plans a product family (a stack) of packages with one prefix, the Base UI port first and later ports such as a motion library. A package id carrying "Base UI" was considered: MUI publishes no trademark policy (mui.com/legal has none, and neither `mui/material-ui` nor `mui/base-ui` has a TRADEMARK file, checked 2026-11-23), so whether MUI asserts the mark is unknown, and ADR 08 already kept the product name out of every API name. On 2026-11-23 no NuGet id began with `Iodine`, `Iodine`, `Iodine.Headless`, `Iodine.Primitives` and `Iodine.FramerMotion` were unused, and no GitHub repository pairs Iodine with Blazor (the repositories named iodine are a DNS tunnel in C, a Ruby web server and a JavaScript validation library).

No pull request was open on 2026-11-23; `main` was at d10252dd, with M6 in progress.

## Decision

1. **The product family is Iodine; this package is `Iodine.Headless`.** The package id and the root namespace are both `Iodine.Headless`; the static assets are served at `_content/Iodine.Headless/`. The name carries no third-party mark, so ADR 08's product-name rule stands, and "a port of Base UI" appears only in the package description and the README.
2. **One repository per package.** This repository becomes `JakeMurrayDev/Iodine.Headless`; later Iodine packages get repositories of their own. Code shared between packages, if any appears, becomes a small shared package then.
3. **Renamed now, in one pull request.** It renames live names only: project and solution files and their paths, namespaces, public members that carry the name, the `_content/` path, the `blazix-*` JavaScript, CSS and attribute names (to `iodine-*`), the `BLZX` diagnostic ids (to `IOD`, numbers unchanged), MSBuild properties such as `BlazixSkipJsBuild`, and every link to the repository (to `JakeMurrayDev/Iodine.Headless`). Dated history (ADR narratives, amendment notes, `_qa-notes.md`) stays as written under `conventions.md` §3.5, and each ADR whose decision names the old identity gets one dated amendment that cites this record. `Blazix.BaseUI`, the old port's repository, keeps its name. The docs lint and the full L1–L4 suite on both render modes pass before the merge; the GitHub repository is renamed after the merge.
   The mapping, applied in this order (as built by the rename pull request):

   | Old | New |
   |---|---|
   | `github.com/JakeMurrayDev/Blazix.Primitives` (every link, history included) | `github.com/JakeMurrayDev/Iodine.Headless` |
   | `Blazix.Primitives` (package, namespace, projects, paths, `_content/`) | `Iodine.Headless` |
   | `BlazixPrimitives` (for example `AddBlazixPrimitives`) | `IodineHeadless` |
   | `blazix-primitives` (the Node package name) | `iodine-headless` |
   | `BLAZIX_` (environment variables) | `IODINE_` |
   | `BLZX` (diagnostic ids) | `IOD` |
   | any other `Blazix` (the `Blazix: ` message prefix, `BlazixJsonContext`, `BlazixSkipJsBuild`) | `Iodine` |
   | any other `blazix` (`data-blazix-started`, `blazixFixture`) | `iodine` |

   `Blazix.BaseUI` is left as it is. Requirement documents are live text: each renamed one carries a dated note at the end of its open-questions section and a `Revised` row of 2026-11-23. Decision records 01–16 and `_qa-notes.md` keep their text, links aside, and each record gains a dated amendment pointing to this table.
4. **The folder on disk keeps its name until M6 is done.** `/Users/jakemurray/Repo/Blazix.Primitives` is renamed by the user after M6, with the sessions closed, followed by `git worktree repair` and a copy of the Claude memory folder to the new path key.
5. **The `Iodine.*` prefix is reserved at the first publish.** The user applies to NuGet once `Iodine.Headless` is on nuget.org; it is a release item beside issue 21.

## Consequences

* ADR 10 decision 1's package id, ADR 08's root namespace, ADR 09's project names and ADR 07 decision 2's repository name are superseded by the rename pull request's amendments, which cite this record.
* Issue 17 (ADR 08, MUI's trademark position) narrows to the wording of the package description and README, since no id or namespace carries the mark.
* Until the prefix is reserved, nothing stops another party from publishing an `Iodine.*` id; the risk is accepted for the time until the first publish.
* Commit messages, issues and pull requests before the rename keep the old name; GitHub redirects the old repository URL.

## Alternatives considered

* Keep the Blazix name with another package id (for example `Blazix.BaseUI`): the namespace clash and the shared, unreserved prefix remain, and searches for Blazix find the other project first.
* Ask `HGCollier` to rename or transfer, on the strength of `JakeMurrayDev/Blazix.BaseUI` being public since 2025-12: uncertain and slow, against a project that already has a domain, a docs site and six versions.
* `Ego` as the family name: `Ego` and `Ego.Primitives` were free, but six `Ego.*`/`EGO.*` ids from two other owners make the prefix unreservable for anyone.
* `Ego.BaseUI` (a package id with the Base UI name): MUI's position on the mark is unknown and an id cannot change once published.
* A monorepo `Iodine`: atomic cross-package changes and one CI, at the cost of restructuring the tag scheme, `docs/` and CI path filters while M6 is in flight.
* Renaming after M6: every M6 track adds occurrences, and the rename would land just before the release tag.
* Rewriting every occurrence, history included: it contradicts `conventions.md` §3.5, and commits and issues would still say Blazix.
