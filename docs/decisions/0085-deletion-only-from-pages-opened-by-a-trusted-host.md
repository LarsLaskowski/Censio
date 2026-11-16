# 0085: A profile's history can be deleted only from a page opened by IP address, as localhost or by a listed host name

- **Status:** Accepted
- **Date:** 2026-11-16
- **Area:** web-pages
- **Source:** Issue #35
- **Supersedes:** 0006 — amends its consequence that anyone who reaches the server can use every action the UI offers,
  for the deletion of a profile's history, the first state-changing action on a page.

## Context

The Product Manager chose a `Delete history` button with a confirmation dialog, without a login like every page, and
asked that a page of another site cannot start it. Record [0006](0006-server-plain-http-and-open-health.md) keeps host
filtering off and lets the Blazor hub check no `Origin`, so a DNS-rebinding page (another site's name made to resolve
to the server) is served the server's pages; it said the first state-changing action on a page revisits that choice.
In such an attack the browser sends the attacker's host name in `Host`, and an `Origin` that matches it. The pages'
root component receives its parameters inside a descriptor that ASP.NET Core protects with the data-protection key
ring (`ServerComponentDeserializer` unprotects it), so a client cannot change them.

## Options considered

1. **Check `Origin` against `Host` on the hub** — a rebinding page sends a matching pair; it stops nothing.
2. **Turn host filtering on by default** — stops rebinding for every page; every deployment reached by a name (a NAS
   name, a proxy's domain) breaks until the operator lists it, and record 0006 is reversed for reading as well.
3. **Ask for a secret in the dialog (the ingest token)** — stops rebinding; the token may be switched off, and typing
   it to delete is a burden the Product Manager did not ask for.
4. **Trust the host the page was opened by: an IP address or `localhost` always, a host name only when `AllowedHosts`
   lists it, carried to the page in the protected descriptor** (chosen) — an attacker's name is never trusted, the usual
   addresses work without setup, and a name works once the operator lists it.

## Decision

Option 4. The server decides when it renders a page whether its host may delete, passes that to the interactive page
in the protected root parameters, and the page offers no deletion otherwise. The rule and the texts are in
[`web-pages.md`](../areas/web-pages.md).

## Consequences

- Reading stays open to a rebinding page as record 0006 accepted; only the deletion is guarded.
- Behind a reverse proxy the rule sees the host the proxy sends: a proxy that replaces it with an address or a listed
  name (nginx's default `$proxy_host`) makes every page it serves trusted, so the proxy passes the client's host
  (`proxy_set_header Host $host;`) and answers only its own names.
- The guard relies on the framework's `frame-ancestors 'self'` for interactive pages, so another site cannot frame a
  trusted page and have it clicked; a test pins the header.
- An operator who reaches the server by a name lists it in `AllowedHosts`, which also turns ASP.NET Core's host
  filtering on for every request, so the list must hold every name and address used, `127.0.0.1` for the image's
  health check included.
- Anyone who reaches the server by its address can delete any profile's history; restricting who reaches the port stays
  the remedy (`SECURITY.md`).
- The guard rests on the data-protection key ring; a stolen key ring lets a client forge the parameter, as it already
  lets it forge the theme and language.
