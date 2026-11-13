# Web pages

## Scope

The server's data pages: the region overview, the island list of a region, the island detail with its goods table, the
key figures, names and their fallbacks, the number and time formats, the row and profile limits, the live updates, the
as-of line and the banner, the addresses and their failures, the bounded reads of profiles, the icon route and what is
logged. Users of the web application rely on it; this document is filled in with the implementation.

## Behavior

## Limits and errors

## Related decisions

- [`decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md`](../decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md) — islands are grouped by the catalog region of their session.
- [`decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md`](../decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md) — pages keep only derived views and read unheld profiles one at a time.
- [`decisions/0076-icons-served-by-content-hash-after-a-png-header-check.md`](../decisions/0076-icons-served-by-content-hash-after-a-png-header-check.md) — icons are served by their content hash after a PNG header check.
- [`decisions/0077-page-address-carries-the-shown-profile.md`](../decisions/0077-page-address-carries-the-shown-profile.md) — the page address carries the shown profile.
- [`decisions/0078-times-shown-at-the-browsers-utc-offset.md`](../decisions/0078-times-shown-at-the-browsers-utc-offset.md) — times are shown at the browser's UTC offset for the instant.

## Not here

- The frame, the theme, the language and the profile switch's look: [`web-ui.md`](web-ui.md).
- The state the pages read, its memory and its followers: [`server-state.md`](server-state.md).
