# 0050: Icons are found by a path rule in `ui.rda` and the patch archives; the newest copy wins, and an unreadable icon archive withholds every icon

- **Status:** Accepted
- **Date:** 2026-11-10
- **Area:** game-catalog
- **Source:** Issue #24
- **Supersedes:** —

## Context

Goods and region assets name an icon in `assets.xml` as `data/ui/fhd/<rest>.png`; the archives hold no `fhd` folder
but `data/ui/4k/<rest>_0.dds`, in `ui.rda` and the `zz_patchfiles_<n>` archives, and the same folders repeat across
them ([`game-files.md`](../areas/game-files.md), *Locations*). The Product Manager supplied five asset values, the
archive listing and four DDS files for #24, kept outside the repository; which copy the game loads is known only from
his statement that a higher `<n>` is newer. The catalog fails closed
([0047](0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)), but a player should
keep the names when an icon is odd. Icons stay on the player's machine
([0023](0023-game-names-and-icons-extracted-locally.md)); the disk cache is #25.

## Options considered

1. **Look up the named `.png` path as written** — no rule in the code; no archive holds such a path, so no icon.
2. **Search every archive for a file of the same name** — independent of folders; a name may repeat across folders
   and DLCs, and the archive order outside the patch archives is unknown.
3. **Map `data/ui/fhd/<rest>.png` to `data/ui/4k/<rest>_0.dds`, read only `ui.rda` and the patch archives, the
   highest `<n>` winning** — matches all five samples; rests on one installation and an unverified load order.
4. **On an unreadable patch archive, fall back to the next older copy** — more icons; may show an outdated icon without
   anyone noticing.
5. **Fail the whole catalog on an unreadable icon** — consistent with the names; one odd icon after a game update would
   cost the player every name.

## Decision

Option 3: icons are taken by the path rule from `ui.rda` and the patch archives, newest first, after the names are
published, and held in memory with the catalog. An icon archive that cannot be opened, or a duplicate patch number,
withholds every icon (not option 4), and so does a set of kept PNGs above a total bound; a single icon that is missing or unreadable leaves only its entry without an
icon, and no icon problem changes the names (not option 5). The rules are in the
[game-catalog](../areas/game-catalog.md) area document (*Icons*).

## Consequences

- Residual risks: the rule and the order rest on the Product Manager's samples and statement (game version
  3.0.1733841); a game update that renames the folders shows as *missing* icons, counted in the log, and the opt-in
  installation test reports copies that differ between archives. No region icon file was sampled.
- The total bound exists because the archive reader allows entries that share one image's data: many paths to one
  large image would otherwise keep many large PNGs for the whole run (Security's review). Withholding the whole set,
  rather than counting the rest *unreadable*, keeps the result independent of the read order.
- A failing listing of `maindata` has no error kind of its own: the survey's listing skips inaccessible folders and
  reports a missing `maindata` as no archives (*archive missing*), so only an exception reaches it, and the boundary
  catch reports it as *archive unreadable*, as the catalog does
  ([0047](0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)). An archive in an
  inaccessible folder is therefore not seen, and an older copy of its icons may win.
- An `IconFilename` with child elements is a malformed `assets.xml` like any other value and fails the catalog, as
  record 0047 says for the values it reads.
- Building icons adds a second listing of the icon archives and, with a UNC game folder, their reads over SMB at every
  start, until the cache of #25.
