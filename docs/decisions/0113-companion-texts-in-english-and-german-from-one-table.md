# 0113: The Companion's own texts come in English and German from one table, chosen by the key `language`

- **Status:** Proposed
- **Date:** 2026-11-25
- **Area:** companion
- **Source:** Issue #43
- **Supersedes:** —

## Context

Every Companion text is English (`TrayMenu`'s constants and format methods). The Product Manager chose that the
settings window, the tray menu, the tooltip and the notifications follow a language choice of the window, English or
German, defaulting to the Windows display language. The same reason texts also go into the log, which stays English
([companion-log](../areas/companion-log.md)).

## Options considered

1. **One text table type with an English and a German instance, passed to the tray and window code** — the log keeps
   the English instance; a test can compare both instances member by member.
2. **.NET resource files with satellite assemblies** — the standard tool; the one-file ZIP must embed them, the
   current UI culture would switch the log's texts too, and a missing German resource falls back silently.
3. **A lookup of each English text** — no new type; texts with numbers and names in them cannot be looked up.

## Decision

Option 1. `CompanionTexts` holds every Companion text in both languages; the choice is the settings key `language`
(`en` or `de`), and without it the Windows display language decides; the texts are listed in
[companion-texts](../areas/companion-texts.md).

## Consequences

- Every tray formatter takes the texts it writes with; the log and the diagnostics window keep the English texts.
- A text added later needs both languages, which a test checks.
