# 0049: A region asset's missing region type comes from its template and the property defaults

- **Status:** Accepted
- **Date:** 2026-11-09
- **Area:** game-catalog
- **Source:** Issue #130
- **Supersedes:** —

## Context

The base game's region asset (3225) states no `RegionID` in `assets.xml`, unlike the region assets of the other
regions, so the catalog of [0047](0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)
gave its sessions no region type. The issue forbids a region type in the code. The Product Manager supplied the
installation's `templates.xml` and `properties.xml` (from `data/base/config/export/` in `config.rda`, kept outside the
repository). In them the `Region` template states a `Region` property without a `RegionID`, and the property defaults
(`DefaultValues`) of `properties.xml` state `Region/RegionID` once, in one property group. Each of the file's 1,057
property names has its default values in exactly one group, and a template mixes properties of several groups, so a
default belongs to the property, not to the group. A property group's `Name` follows its values in the file. Building
templates state `AssociatedRegions` values that differ from the `Building` property default, which only makes sense
if an asset's value overrides its template's and the template's overrides the property default.

## Options considered

1. **Asset value, else template value, else property default, for `RegionID` only** — the game's own layering, read
   from two small files (0.9 MB and 0.4 MB); a game update that states the value in the template is still read right.
2. **Asset value, else property default, without `templates.xml`** — one file less; would give the property default
   even where a later template states its own value.
3. **The same layering for every fact the catalog reads** (`AssociatedRegion(s)`, `Session/Region`, the `Building`
   section) — the files show defaults there too (the `Building` default lists region types, `SessionModerate` names a
   region asset); it changes kinds and region lists of goods and buildings far beyond the issue and needs its own
   checks against the installation: follow-up issue.
4. **Defaults keyed by the property group's name** (`Session`) — matches the group name the Product Manager found;
   the group name comes after its values and has nothing to do with the template's group, and a template takes
   properties from several groups.
5. **A region type written into the code** — rejected by the issue and by 0047.

## Decision

Option 1: a region asset's `RegionID` is the one it states, else the one its template's `Region` property states in
`templates.xml`, else the `Region` property default of `properties.xml`; the two files are read with the same
forward-only, bounded, fail-closed reader as the others. The rules are in the
[game-catalog](../areas/game-catalog.md) area document.

## Consequences

- The base game's sessions carry the region type the data gives (`Roman` on game version 3.0.1733841), and a new
  region still needs no code change.
- Two more required files: a missing or malformed `templates.xml` or `properties.xml` now fails the whole catalog.
- That a template value overrides the property default is inferred from the files' structure; no template states a
  `RegionID` on the Product Manager's installation, so this order is not shown by any real value.
- Goods, buildings and sessions still read only their own values; their template and property defaults are the
  follow-up issue of option 3.
