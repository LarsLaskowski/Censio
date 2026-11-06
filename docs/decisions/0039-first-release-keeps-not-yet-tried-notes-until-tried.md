# 0039: The "Not yet tried" notes about the published image and ZIP stay until someone has tried them

- **Status:** Accepted
- **Date:** 2026-11-06
- **Area:** —
- **Source:** The first release `v0.1.0`
- **Supersedes:** 0013 (amends only its consequence that the first release removes the "Not yet tried" notes about
  the published image and ZIP; 0013 stays unedited)

## Context

Record 0013 says the first release removes the "Not yet tried" notes about the published image and ZIP from the
opening paragraph of each installation section. The release `v0.1.0` is published and its workflow ran with all
jobs green, but the workflow only builds, checks and publishes: nobody has pulled and started the published image,
run the ZIP on a machine without .NET, or seen the SmartScreen dialog on the downloaded file. Removing the notes
with the release would state as tried what only the build pipeline has covered.

## Options considered

1. **Remove the notes with the release, as 0013 says** — the text matches the record; it claims a test that did not
   happen.
2. **Keep the notes until someone has tried the step, and drop the condition "until the release exists"** — the text
   stays true; the notes are removed in a later documentation change, once a person has run the step.

## Decision

Option 2. A note about the published image or ZIP is removed in the pull request that records a person having tried
that step, not by the release itself; until then it names the released version (`0.1.0`, `v0.1.0`) instead of
waiting for the release.

## Consequences

- The release does not change the notes beyond naming the released version; the README and the user guide keep the
  notes in English and German.
- The tester who tries the image, the ZIP or the SmartScreen steps reports it, and the pull request that removes
  the note says who tried it and on what.
