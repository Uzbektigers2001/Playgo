# Playgo Backend — Audit Index

> Comprehensive audit of the Playgo .NET 8 Clean Architecture backend on branch `feature/01-audit`.
> Read top-to-bottom for a full picture; each document is also self-contained.

| # | Document | What it covers |
|---|----------|----------------|
| 01 | [Architecture Overview](./01-architecture-overview.md) | Tech stack, project layout, layer dependency diagram, full ER diagram, auth sequence, request pipeline, DI graph, persistence conventions. |
| 02 | [Endpoints Inventory](./02-endpoints-inventory.md) | Every controller action (38 endpoints) with auth, roles, DTOs, status codes, and the frontend page it serves. |
| 03 | [Issues & Technical Debt](./03-issues-and-technical-debt.md) | 5 categories (Security, Performance, Architecture, Missing Features, Code Quality) with file:line anchors, severity, and suggested fixes. |
| 04 | [Frontend Integration Gaps](./04-frontend-integration-gaps.md) | 41-row gap matrix between `playgo-frontend` (Next.js 14) expectations and current backend responses, with per-row solution strategy. |
| 05 | [Roadmap](./05-roadmap.md) | 5-phase plan mapped 1:1 to Prompts 2–10: goals, files, migrations, breaking-change flags, effort. |

## How to use this audit

- **Frontend engineer onboarding** → start with `02` and `04`.
- **Backend engineer picking up the next prompt** → `05` first, then drill into `03` for issues to address along the way.
- **Architect / tech lead reviewing risk** → `03` (Critical / High rows) and the breaking-change cells in `05`.

## Conventions

- Every claim is anchored to `path:line` (e.g. `src/Playgo.API/Program.cs:45`). Re-verify before acting — code may have moved since the audit commit.
- Severity scale used throughout: **Critical · High · Med · Low**.
- Mermaid diagrams render natively on GitHub.
