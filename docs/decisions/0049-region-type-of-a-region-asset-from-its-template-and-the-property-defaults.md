# 0049: An asset's missing catalog facts come from its template and the property defaults

- **Status:** Accepted
- **Date:** 2026-11-10
- **Area:** game-catalog
- **Source:** Issue #130, Issue #133
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

#130 resolved `RegionID` alone. The same files supply the other facts the catalog takes from an asset's values (#133):
the `Building` property default lists region types, building templates state their own `AssociatedRegions`, and the
template `SessionModerate` states a `Session/Region`. A building whose list sits in its template or the default was
*not stated*, and an asset whose template has the `Building` property but which states no `Building` section of its own
was no building. The catalog decides the kinds in the asset pass and reads the texts of the candidates only, so the
template facts must be known when the assets are read.

## Options considered

1. **Asset value, else template value, else property default, for `RegionID` only** — #130's first scope; leaves
   the region lists and kinds the game takes from templates and defaults unread, although the game layers every
   property the same way.
2. **Asset value, else property default, without `templates.xml`** — one file less; would give the property default
   even where a template states its own value.
3. **The same layering for every fact the catalog takes from an asset's values that a template or default states**
   (`RegionID`, `AssociatedRegion`, `AssociatedRegions`, `Session/Region`, and the `Building` property for the kind),
   with `properties.xml` and `templates.xml` read before the assets.
4. **Layering every value, `GUID`, `Text/OasisId` and `IsWorkforce` included** — a default `GUID` or text identifier
   would give every asset the same one; #130's sample showed template or default values of interest only for the facts
   of option 3, so the others stay the asset's own.
5. **Keep the pass order and decide kinds after the templates** — every asset with a template stays a candidate
   until the end and its texts are read; holds a large part of `assets.xml`'s assets in memory at every start.
6. **Defaults keyed by the property group's name** (`Session`) — the group name comes after its values and has
   nothing to do with the template's group, and a template takes properties from several groups.
7. **A region type written into the code** — rejected by the issue and by 0047.

## Decision

Option 3: a fact an asset does not state is taken from its template in `templates.xml` when that template has the
property, else from the property's default in `properties.xml`; a template's `Building` property makes its assets
buildings. The two files are read before the assets with the same forward-only, bounded, fail-closed reader as the
others. The rules are in the [game-catalog](../areas/game-catalog.md) area document.

## Consequences

- The base game's sessions carry the region type the data gives (`Roman` on game version 3.0.1733841), and a new
  region still needs no code change.
- Buildings and goods whose region list sits in a template or a default carry it, and assets that are buildings only
  through their template become entries; the opt-in installation test reports the change of kinds and region lists
  against a build from the assets' own values, as counts.
- One template or default value reaches many assets: it is split once and its list shared by reference, and its
  unknown names are counted once, so memory, time and the count grow with the files' content, not with the number of
  assets (Security's plan review of #133). Removing duplicate names instead would bound memory but not the repeated
  splitting, and would change lists the assets state themselves.
- Two more required files: a missing or malformed `templates.xml` or `properties.xml` fails the whole catalog, now
  before the assets are read.
- That a template value overrides the property default is inferred from the files' structure; no template states a
  `RegionID` on the Product Manager's installation. A present empty element stops the layering at any level, as an
  empty `AssociatedRegions` of an asset means *none*; a template that lists empty fields as placeholders would turn a
  default into *none*, which the installation test's report shows.
- `IsWorkforce` and the text identifier stay the asset's own; a game update that moves them into templates needs a
  change of the reader.
