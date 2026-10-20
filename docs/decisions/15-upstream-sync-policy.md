# ADR 15 — Upstream sync policy

| | |
|---|---|
| Status | accepted (2026-10-20) |
| Project | Blazix.Primitives |
| Decides | whether and when the Base UI pin moves after v1.9.0; what happens between re-pins; what a re-pin does to the documents, the decision records and the version |
| Affects | `docs/conventions.md` §1 (the pin); every `Source:` line (line numbers from the pinned commit); ADR 10 (the minor bump); ADR 13 (amendments on re-pin) |

## Context

Base UI has shipped ten minor releases in ten months (v1.0.0 on 2025-12-11 to v1.9.0 on 2026-10-09). The pin is one commit for all 47 documents, and almost every `Source:` line carries line numbers from it, thousands in total; moving the pin invalidates them unless a tool maps old ranges to new ones through the diff hunks and flags the ranges that fall inside changed code. The old port re-pinned monthly under a rubric (four verdict classes: skip as React-specific, transfer to C#, transfer to JavaScript, no-op with the sub-labels already-present or moot; the symptom-restatement rule; default-port; one disposition row per change and component), recorded in `JakeMurrayDev/Blazix.BaseUI` issues 144–157 and `docs/audits/METHODOLOGY.md`; it had no specification, so a re-pin touched code directly. Here an upstream change hits the documents first, and the documents are being implemented milestone by milestone (ADR 12).

## Decision

### 1. The pin is frozen at v1.9.0 until the port's 1.0.0 (option P1)

No re-pin during M0–M6. Each Base UI release gets a **triage note**, `docs/upstream/v1.N.md`, written within the month of the release: one row per changelog entry and affected document, with a verdict — *doc-impacting, queued* (the statement, level, tests or ownership of named IDs would change), *test-only*, *React-only* (the symptom restated in Base UI-neutral terms and why it cannot arise in the port), *already matches* — plus the upstream PR and a one-line symptom. A **targeted amendment** before the re-pin is allowed only for a fix the triage rates severe on an already implemented component; it follows ADR 13's rule and cites the upstream commit explicitly (`Source: upstream <sha>`) without moving the pin.

### 2. What a re-pin does, in order

1. Move `reference/base-ui` to the new tag and `reference/vendor` to the versions in its lockfile; update `docs/conventions.md` §1 and the README pin.
2. Run the citation-migration tool (built before the first re-pin) over every `Source:` line and review the ranges it flags as inside changed hunks.
3. Apply the queued triage verdicts as document amendments under ADR 13 §3, each citing the upstream PR; withdraw or append IDs, never renumber.
4. Re-run `docs/_tools/test_titles.py` per document for new test titles; add §13 rows, `Test:` lines with layer tags, and the tests at their layers.
5. Review ADR impact; an ADR amendment caused by upstream cites the upstream change.
6. Bump the port's minor (ADR 10); update `THIRD-PARTY-NOTICES.md` if a vendored version changed.

A document's status does not regress on re-pin; tests made red by amended requirements are tracked in the re-pin's issue until green.

### 3. The rubric

The old port's four verdict classes, the symptom-restatement rule, the default-port rule and the standard disposition row are kept, with one change: a verdict attaches to an (upstream change, document) pair, never to code. The methodology text is reproduced in `docs/upstream/README.md` when the first triage note is written, citing its origin.

## Consequences

* 1.0.0 ships against a Base UI release that may be several minors old; the queue is worked off in one larger pass afterwards.
* The citation-migration tool is a prerequisite of the first re-pin and needs commit history in `reference/base-ui` (a deeper clone than the shallow one used today).
* Consumers see which Base UI release a package tracks in its metadata (ADR 10).

## Verification items

* The citation-migration tool handles the shallow clone (fetch depth or a full clone) and maps ranges through renamed files; build and test it before the first re-pin.

## Considered options

* Re-pin at every milestone boundary (six documents passes with citation migration during implementation; approved documents change under the people implementing them).
* Monthly re-pin as the old port did (the highest churn, each cycle a documents amendment pass plus an ADR impact review, on a specification that is the project's main asset).
