# Blazix.Primitives

An unofficial Blazor (C#) port of [MUI Base UI](https://base-ui.com/), the headless, accessible component library. Both interactive render modes (Server and WebAssembly) are first-class targets.

**Status:** specification complete; implementation not started. The port is built documentation-first: `docs/` holds 47 requirement documents (about 6,040 traceable requirements reverse-engineered from Base UI v1.9.0) and the decision records that fix how they map onto Blazor. Start at [docs/README.md](docs/README.md).

| | |
|---|---|
| Base UI pin | v1.9.0 (`ad7ecda`), see `docs/conventions.md` §1 |
| Target | .NET 11 / C# 15 (`net11.0`) |
| Package | `Blazix.Primitives` on NuGet after .NET 11 GA (decision record 10) |
| Decisions | [docs/decisions/](docs/decisions/) — 16 accepted records |
| Glossary | [CONTEXT.md](CONTEXT.md) |
| Plan | [docs/decisions/12-implementation-order-and-milestones.md](docs/decisions/12-implementation-order-and-milestones.md) |

## Working in this repository

Read [AGENTS.md](AGENTS.md) first: it says where everything lives and how documents are amended. After any edit under `docs/`, run the lint from the repository root:

```bash
python3 docs/_tools/check_docs.py
```

## Licence and acknowledgements

MIT, see [LICENSE](LICENSE). Base UI is © Material-UI SAS (MIT) and Floating UI is © Floating UI contributors (MIT); their notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). This project is not affiliated with or endorsed by MUI.
