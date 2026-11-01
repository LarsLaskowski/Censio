# 0013: The README is the quick start, the user guide the reference; unreleased states are built from source

- **Status:** Accepted
- **Date:** 2026-10-31
- **Area:** —
- **Source:** Issue #7
- **Supersedes:** —

## Context

Issue #7 asks for a README with which an external person can install and start the current state on their own,
and a user guide (`docs/USER_GUIDE.md`) for installation, first start, data, logs and problem reports, each in
full English and then full German. Until then both files held the same Docker and Companion text, so every change
had to be made four times (two files, two languages) and the README grew with every operator detail (NAS
ownership, TLS, port variables). Edge builds (#6) were closed as not planned and no release exists before
`v0.1.0` (milestone M1), so the state of `main` reaches a tester only from source. Every later milestone updates
the documentation, so where each kind of text lives decides how much each update touches.

## Options considered

1. **README as quick start, user guide as reference** — the README holds what Censio is, *Status*, how to get it
   (releases, no edge builds, building from source), the shortest install and start path of both parts, the
   configuration table and the licence; the guide holds the full installation detail, the first start, data and
   logs, and problem reports. Pro: the README stays readable and alone suffices for the usual path; a milestone
   updates *Status* in one file. Con: the short install path appears in both files and must be kept in step.
2. **Both files with the full text** (the state before #7). Pro: either file stands alone. Con: every change
   four times, and the README buries *Status* and the quick path under operator detail.
3. **README links to the guide for installation.** Pro: no duplicate text. Con: fails #7's criterion that the
   README alone is enough to install and start.
4. **Point testers at the CI artifact `companion-dry-run`** for an unreleased Companion. Con: it is a test fixture
   stamped `1.2.3`, kept 14 days and downloadable only with a GitHub login; it would show a version that was never
   released.

## Decision

Option 1. The README is the quick start and the only home of *Status*; `docs/USER_GUIDE.md` is the reference. A
state without a release is built from source, the Companion with the flags of the release build except the version.

## Consequences

- A milestone updates *Status* in the README in both languages; the first release also removes the "Not yet tried"
  notes about the published image and ZIP from the opening paragraph of each installation section.
- The quick install path exists in both files; a change to a command changes it in both files and both languages.
- Bringing edge builds back (#6) would add a way to get Censio to the README's *Getting Censio* section and
  revisit option 4's reasoning.
