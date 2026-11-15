# Plan: Remeasure the catalog read peak in contract.md with a reproducible catalog

Source: Issue #178
Tier: trivial — area documents only (`docs/areas/` rules out `docs`), no production, test or behavior change.
Security areas touched: none

## Problem / root cause

The catalog behind `contract.md:194,211` (158.8 MiB, "the largest read") came from a scratch program (`:181`) and its
shape is unstated, so it cannot be reproduced; `ingest.md:319` measured 185.9 MiB on another catalog.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Generator not kept, shape only "16 names" | `contract.md:181,194` | confirmed |
| 2 | The issue's #28 catalog and 185.9 MiB | `ingest.md:304-319` | confirmed |
| 3 | Per entry the model holds a names dictionary, a region-type list or `null`, an icon string; bounds 16,384 entries, 16 names, 32 region types | `CatalogDocumentEntry.cs:28,40,46`; `CatalogDocumentReader.cs:25-35` | confirmed |
| 4 | The rules allocate per tag and subtag (`Split('-')`, `string.All`) | `CatalogDocumentRules.cs:98,123-130` | confirmed |
| 5 | A document of 16,777,216 bytes fits the message: Base64 22,369,624 characters, bound 23,767,724 | `contract.md` *Bounds* | confirmed |
| 6 | Shape B or C below peaks above shape A and above 158.8 MiB | to verify: Dev's measurement, step 6 | unverified |
| 7 | Record 0060:39 ("the largest read is a catalog upload") stays true | to verify: every shape above the worst tick's 114.4 MiB, Dev, step 6 | unverified |

## Acceptance criteria

- AC1: contract.md, *Memory of a read*, describes every measured catalog exactly (each property per entry index, the
  string-length rule, the message `id`) with each document's byte count and SHA-256 and each message's byte count.
- AC2: Its catalog rows are measurements of shapes A, B and C (*Approach*) by the section's own method, replacing the
  158.8 MiB row; the output block is the run with the highest peak.
- AC3: The summary sentence names the highest peak across the shapes measured as the largest read, with its shape, and
  states that the peak depends on the catalog's shape, not only its size.
- AC4: ingest.md's comparison (`:330-332`) points to the shapes of contract.md and their highest peak instead of
  158.8 MiB, and does not grow the document.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: rebuild by the Tester | — |
| AC2 | check: Dev's run output, Reviewer | — |
| AC3 | check: Reviewer | — |
| AC4 | check: Reviewer | — |

## Verification without tests

Steps 4, 5 and the *Coverage gate* are not applicable.

- AC1: check: the Tester (mode `coverage`, after the Dev) rebuilds the three documents and messages from contract.md's
  text alone and compares byte counts and SHA-256; a mismatch goes to the Dev.
- AC2: check: the Dev hands back the output of every run; the Reviewer compares the table with it.
- AC3, AC4: check: the Reviewer reads both documents against the run output.

## Approach

Shapes, written by `CatalogDocumentWriter` and wrapped by `ContractWriter` as a `catalogUpload` with `id` `11111111-2222-4333-8444-555555555555`: `schemaVersion` 1, `gameVersion` `1.2.3.4`, 16,384
entries; entry `i` (0-based) has GUID `i + 1`, kind `good`, region GUID `null`, icon `i` as 64 lowercase hex digits. All
names and region types are `x` repeated `L` times, `L` the largest length that keeps the document within 16,777,216
bytes.

- A: 16 names, tags `aa` to `ap`, region types `null` (#28's shape, filled).
- B: as A plus 32 region types: the most strings the bounds admit.
- C: as B with each tag its two letters followed by `-a` sixteen times (34 characters, 17 subtags: fact 4).

The Dev measures each shape three times by the section's method and replaces the `gen catalog` lines, the 158.8 MiB
row (one row per shape: largest peak, read time as range), the output block and the summary; tick rows stay. Layout free for the Tester, approved in step 9.

## Options considered

- A generator kept as code (test helper or tool), the issue's first option: lost — every measurement in `docs/areas/`
  uses scratch code outside the repository; a description plus SHA-256 reproduces the bytes without unused code.
- Shape A alone: lost — fact 4: tag form and string count change the allocation.

## Affected projects and types

None.

## Signatures (for the Dev's skeleton)

None: no API.

## Test files

None. Existing call sites: none. Files no unit test can cover: none.

## Areas

- `contract.md`: *Memory of a read* (AC1 to AC3).
- `ingest.md`: *Memory of a catalog upload*, last paragraph (AC4).

## Documentation updates

- Dev: `docs/areas/contract.md`, *Memory of a read*, per *Approach*.
- Dev: `docs/areas/ingest.md:330-332`, per AC4.
- Sweep (`158.8`, `159 MiB`, `185.9`, `largest read`): `decisions/0060:39` stays (fact 7); `game-catalog-format.md`
  *Limits and errors* measures the catalog reader alone below the byte bound and stays.

## Architecture check

No guarantee touched; the 24 MiB message bound stands.

## Security considerations

- [x] No limits.
- [x] None.

## Decision records

- none: description over kept code has no lasting weight.

## Out of scope / follow-ups

- The session measurement of ingest.md.
