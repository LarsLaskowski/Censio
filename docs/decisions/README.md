# Decision records

Why the code is the way it is. Each file records one decision — its context, the options considered, what
was chosen and the consequences — so that months later the reasoning is still available without the
pull request, the issue thread or the session that produced it.

A record explains the *why*. What holds today — formats, limits, error behavior, guarantees — is written down
once in the [area document](../areas/README.md) of the record's area, and the record links to it.
`docs/ARCHITECTURE.md` describes how the parts fit together. When a decision changes the architecture,
`ARCHITECTURE.md` is updated as well and links the record.

## Rules

- One decision per file: `NNNN-short-title.md` (four digits, next free number), created from
  [`_template.md`](_template.md). In the index row and in a `Superseded by NNNN` status the number may be
  written as a link, `[NNNN](NNNN-short-title.md)`.
- **The record states the decision in one to three sentences and does not restate behavior.** Context,
  *Options considered* and *Consequences* carry the reasoning; the rules of the resulting behavior (values,
  formats, limits, error texts) belong in the area document and are linked from the record.
- The `Area:` field names the record's area as listed in [`docs/areas/README.md`](../areas/README.md), or `—`
  for a record about the process or about no area. Once areas are defined, every record needs the field. For a
  released record it is part of the content: it changes only through a new record. A released record without
  the field gets its area from an `## Area assignment` table (`| Record | Area |`) in a new record; the
  released record itself stays unedited.
- Written by the squad Lead (see [`.claude/agents/squad-lead.md`](../../.claude/agents/squad-lead.md));
  anyone may add one for a change made outside the squad.
- Committed together with the change it explains.
- **Released or not decides how a record changes.** A record is *released* once the commit that added it is
  contained in a release tag (`v*`; check with `git tag --contains <commit>`, the commit is
  `git log --diff-filter=A --format=%H -- <file>`). Until the first release nothing is released.
- A record that is **not yet released** is edited in place, in the pull request that changes the decision
  (status stays `Proposed` or `Accepted`). Its history is Git and the pull request; there is no
  `Superseded by` chain for it. A record that no longer applies is deleted (numbers are never reused, gaps are
  fine) together with its index row and every link to it.
- A **released** record is **append-only**: it is never rewritten. A changed decision gets a new record that
  names the old one under *Supersedes*, and the old record's status becomes `Superseded by NNNN` (the only
  edit allowed). `Superseded by` is therefore only ever set on a released record.
- A new record that **amends** a released record without superseding it names the older one under
  *Supersedes* and says there, in one sentence, what it amends. The older record is **not edited** — not even
  with a pointer; a reader of the older record finds the amendment through the index and the new record.
- **One record per decision that has lasting weight, grouped by topic — not one per pull request.** Not for
  routine changes: a record is needed when a choice between real alternatives was made that shapes the
  architecture or behavior, a trade-off or limitation was accepted, or a documented guarantee was touched.
  Before adding a record, look for an unreleased record on the same topic and extend it instead. A review
  finding that was deliberately not fixed, a follow-up split, or a dependency change gets its own record only
  if it touches a guarantee or the architecture; otherwise it belongs in the pull request description or the
  follow-up issue, or as a sentence in the record of the topic it concerns.
- `.squad/tools/decision-check.py` (also run by `config-check.py`) checks the index, the links between records
  and that no released record was changed.

## Index

<!-- project:begin index -->
| #    | Title | Status | Date |
| ---- | ----- | ------ | ---- |
| [0001](0001-quality-gates-before-the-pull-request.md) | Quality gates before the pull request | Accepted | 2026-10-30 |
| [0002](0002-squad-working-records-off-main.md) | Squad working records stay off main, and product PRs never change the squad | Accepted | 2026-10-30 |
| [0003](0003-squash-merge-pull-requests.md) | Squash-merge pull requests | Accepted | 2026-10-30 |
| [0004](0004-companion-shell-and-linux-build.md) | Companion logic in a platform-neutral project; the Windows shell builds on Linux | Accepted | 2026-10-30 |
| [0005](0005-product-version-from-one-build-property.md) | The product version comes from one build property; the first release is 0.1.0, without edge builds | Accepted | 2026-10-30 |
| [0006](0006-server-plain-http-and-open-health.md) | The server serves plain HTTP; the web UI and the health check need no login | Accepted | 2026-10-30 |
| [0007](0007-server-web-assets-independent-of-restore-inputs.md) | The server project requests the Blazor web assets itself | Accepted | 2026-10-30 |
| [0008](0008-ci-secrets-scoped-to-the-steps-that-use-them.md) | CI secrets are scoped to the steps that use them | Accepted | 2026-10-31 |
| [0009](0009-server-image-runs-as-a-fixed-non-root-user.md) | The server image runs as a fixed non-root user; other UIDs come from `--user` | Accepted | 2026-10-31 |
| [0010](0010-server-image-base-pinned-by-amd64-manifest-digest.md) | The image bases are full-version amd64 tags pinned by digest; the `base.digest` label is checked in CI | Accepted | 2026-10-31 |
| [0011](0011-data-protection-key-ring-in-the-data-directory.md) | The data-protection key ring lies in the configured data directory; the server stops when it cannot write it | Accepted | 2026-10-31 |
| [0012](0012-release-pre-release-first-and-secret-free-builds.md) | Releases run as pre-release first; artifacts are built without secrets and promoted by digest | Accepted | 2026-10-31 |
| [0013](0013-readme-quick-start-user-guide-reference.md) | The README is the quick start, the user guide the reference; unreleased states are built from source | Accepted | 2026-10-31 |
| [0014](0014-product-name-censio.md) | The product is named Censio | Accepted | 2026-11-01 |
| [0015](0015-two-deliverables-and-local-mode.md) | Two deliverables, the Companion ZIP and the server image; the Companion hosts the server for local use | Accepted | 2026-11-01 |
| [0016](0016-csharp-dotnet-10-blazor-server-only.md) | C#, .NET 10 and Blazor Server only | Accepted | 2026-11-01 |
| [0017](0017-companion-tray-app-autostart-and-connect.md) | The Companion is a WPF tray application with optional autostart and an automatic connection | Accepted | 2026-11-01 |
| [0018](0018-pipe-parsed-in-companion-versioned-json-contract.md) | The Companion parses the pipe; the server receives only a versioned JSON contract | Accepted | 2026-11-01 |
| [0019](0019-websocket-transport-with-shared-token.md) | Companion and server talk over a WebSocket with a shared token, enabled by default | Accepted | 2026-11-01 |
| [0020](0020-one-companion-per-server-with-savegame-profiles.md) | One Companion per server; savegames are kept apart as profiles | Accepted | 2026-11-01 |
| [0021](0021-sqlite-history-with-retention.md) | SQLite from the start; full resolution for 7 days, then hourly averages without limit; deletion per profile | Accepted | 2026-11-01 |
| [0022](0022-chart-time-axis-real-or-game-time.md) | The chart time axis switches between real time and game time | Accepted | 2026-11-01 |
| [0023](0023-game-names-and-icons-extracted-locally.md) | Names and icons are extracted from the local installation, cached, re-extracted after updates and never distributed; German and English | Accepted | 2026-11-01 |
| [0024](0024-rda-reading-with-an-existing-nuget-library.md) | RDA archives are read with an existing NuGet library where one fits | Accepted | 2026-11-01 |
| [0025](0025-mudblazor-and-blazor-apexcharts-light-theme.md) | The web UI uses MudBlazor and Blazor-ApexCharts; the light theme is the default | Accepted | 2026-11-01 |
| [0026](0026-server-image-linux-amd64-on-docker-hub-no-compose.md) | The server image is published for linux/amd64 on Docker Hub; no compose file is shipped | Accepted | 2026-11-01 |
| [0027](0027-companion-zip-self-contained-single-file-unsigned.md) | The Companion ships as a self-contained single-file exe in a ZIP, without code signing | Accepted | 2026-11-01 |
| [0028](0028-mit-licence.md) | Censio is released under the MIT licence | Accepted | 2026-11-01 |
| [0029](0029-anonymised-test-fixtures-only.md) | Test fixtures from recordings and game files are anonymised | Accepted | 2026-11-01 |
| [0030](0030-documentation-language-rules.md) | Code and project records are in English; README and user guides hold English, then German, in one file | Accepted | 2026-11-01 |
| [0031](0031-bounded-local-tick-buffer-gaps-accepted.md) | The Companion buffers unsent ticks within a bound; beyond it gaps are accepted | Accepted | 2026-11-01 |
| [0032](0032-telegram-alerts-and-mobile-layout-later.md) | Telegram alerts and a mobile layout come later | Accepted | 2026-11-01 |
| [0033](0033-pipe-reader-strict-from-the-reference-source.md) | The pipe reader is strict, fails closed and is built from the reference source with synthetic frames | Accepted | 2026-11-01 |
| [0034](0034-ticks-assembled-by-close-rules-on-an-injected-clock.md) | Ticks are assembled by close rules and a quiet period on an injected clock, in a pure core with an async driver | Accepted | 2026-11-01 |
| [0035](0035-pipe-connection-states-retry-and-identification-level.md) | The pipe connection is a platform-neutral state machine over a Windows connector that checks the pipe exists first and connects at identification level | Accepted | 2026-11-01 |
| [0036](0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md) | Recordings hold the raw frames as JSON lines, written off the read path; replay feeds them back as a byte stream on a recorded clock | Proposed | 2026-11-01 |
<!-- project:end index -->
