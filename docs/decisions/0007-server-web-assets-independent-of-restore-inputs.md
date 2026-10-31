# 0007: The server project requests the Blazor web assets itself

- **Status:** Proposed
- **Date:** 2026-10-30
- **Area:** server
- **Source:** Issue #2
- **Supersedes:** —

## Context

The .NET 10 SDK decides at restore time whether a web project gets the framework's Blazor scripts
(`_framework/blazor.web.js`): target `ResolveRequiredWebAssets` in `Microsoft.NET.Sdk.Web.ProjectSystem.targets`
sets `RequiresAspNetWebAssets` to `true` only when a `.razor` content item exists. A Docker build that copies the
project files, restores, then copies the sources (the layer-caching order of `.squad/stack.md`, *Image build
check*, and of the Dockerfile #4 adds) has no `.razor` file at restore time, so the image ships without the
script and the page stays non-interactive (Vandox#24). The restore-order reproduction showed exactly this for
`Censio.Server`; copying one `.razor` file before the restore fixed it.

## Options considered

1. **`<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` in `Censio.Server.csproj`** (chosen) — the project
   states its need, so every restore gives the same result whatever source files exist; the Dockerfile keeps
   copying only project files before the restore. Con: the property is the SDK's own, not a documented public
   switch, so a later SDK may rename it; the *Image build check* then fails closed (no `_framework/`).
2. **Copy `*.razor` before the restore** (reproduction and Dockerfile) — documented SDK behavior only. Cons: a
   Dockerfile `COPY` does not copy a recursive glob with its directories (only `COPY --parents` of a newer
   Dockerfile syntax), every `.razor` edit invalidates the cached restore layer, and every later Razor project
   must remember the rule.
3. **An empty `wwwroot` placeholder** — tried by the Dev: no effect, the condition reads `.razor` items only.

## Decision

`Censio.Server.csproj` sets `RequiresAspNetWebAssets` to `true`; the *Image build check* reproduction and the
Dockerfile keep copying only project and build files before `dotnet restore`.

## Consequences

The image's publish output equals the working tree's for the Blazor scripts without a Dockerfile rule about
source files. An SDK that drops the property is caught by the *Image build check* and, once #4 adds it, by the image build in CI; the fallback is option 2. A future Razor class library needs the same property.
