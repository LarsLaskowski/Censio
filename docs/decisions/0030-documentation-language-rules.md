# 0030: Code and project records are in English; README and user guides hold English, then German, in one file

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8
- **Supersedes:** —

## Context

Contributors, tools and AI agents work best in English, and the code, the analyzers and the libraries are English.
Many Anno players are German-speaking, and an end user should be able to read the
documentation in their own language. Two languages kept in separate files drift apart unnoticed.

## Options considered

1. **English everywhere** — one text to maintain; German-speaking players get no documentation in their language.
2. **German and English files side by side (`README.de.md`)** — each file in one language; a change to one is easily
   forgotten in the other.
3. **English for code, comments, issues, specs, decision records, commits and pull requests; README and user guides
   with the full English text first and the full German text after it, in one file** — the developer side has one
   language, the user side both, and a change shows in one diff.

## Decision

Option 3, as written in `AGENTS.md` (*What this project is*, *Commit messages and pull requests*).

## Consequences

- Every change to `README.md` or a user guide changes both languages in the same pull request.
- The roles of README and user guide are [0013](0013-readme-quick-start-user-guide-reference.md).
