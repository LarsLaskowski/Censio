# Spec: Anonymiser for recordings and test fixtures

Source: Issue #20
Status: Approved by Lead

## Problem

The parser and tick assembler are tested only against hand-built frames. The #17 recordings show what the game really
sends, but hold private names and exact economic data, so they may not be committed until an anonymiser exists (record
[0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md)); none does.

## Scenarios

The anonymiser is a console program for contributors, run from the repository with the .NET SDK (open question 1). It
reads a recording of format version 1 ([`pipe-recording.md`](../../docs/areas/pipe-recording.md), *File format*) and
writes an anonymised copy in the same format.

- S1: Given a recording, when the user runs the anonymiser with it and a new output path, then it writes the output,
  prints `Anonymised <n> frames to <output>` and exits with code 0. Every island and figure name is invented, every
  economic value perturbed, every receive time shifted; everything else of every frame is unchanged.
- S2: Given the output of S1, when it is replayed with `--replay`, then the replay shows the same ticks, session events
  and version messages in the same order as the original's, with the same island keys, game times and close reasons.
- S3: Given a recording with a renamed island or two figures, when it is anonymised, then one original name always
  gets the same invented name in that file and two names get two invented names, so the rename and the change of
  figure stay visible.
- S4: Given the fixtures under `tests/` with their README, when the test suite runs (locally or in CI), then the
  fixture-based tests read each fixture through the message reader and the tick assembler and find the facts the
  README states for it (frame counts by type, ticks with their close reasons, session events, a rename).
- S5: Given a maintainer with a local deny list (names to deny and, per fixture, the path of its original recording),
  when the suite runs with the environment variable that points to it, then the deny-list test checks every fixture;
  without the variable the test is reported inconclusive, not passed.
- S6: Given a contributor, when they read `README.md` or the user guide, then they find how to run the anonymiser.

## Errors and edge cases

The anonymiser prints one line `Error: <reason>` and exits with code 1; no file exists under the output name afterwards.

- E1: The recording is refused for the reasons and with the texts a replay uses: `file not found`, `not a recording`,
  `unsupported version`, `invalid line <n>`, `read failed` ([`pipe-recording.md`](../../docs/areas/pipe-recording.md),
  *Limits and errors*). A file that differs between the two passes (N5) gives `read failed`; a receive time the shift
  (N2) would move before the earliest representable time gives `invalid line <n>`.
- E2: A frame the anonymiser cannot decode — an unknown type, a malformed message, any frame but `Version` before a
  `Version 2` or after another version — gives `frame cannot be anonymised in line <n>`: its bytes could hold a name,
  so it is never copied and never dropped silently (fail closed). An empty frame holds nothing and is copied.
- E3: The output exists: `output file exists` (never overwritten); it is the input: `output is the input`.
- E4: Output not writable: `write failed`.
- E5: Not exactly two arguments: `Usage: <command> <recording> <output>` and exit code 2.
- E6: An original name that equals an invented one still gets a different invented name: no invented name equals an
  original name of the file.
- E7: An empty name stays empty. Bytes after the last field of a frame are not copied.
- E8: Zero stays zero; NaN stays NaN (the standard NaN with its sign) and infinities stay. Map pairs are copied one by one as on the wire, a GUID
  that repeats included; only their amounts change (N3).
- E9: The deny list names a fixture whose original is missing or unreadable: the deny-list test fails and names the
  fixture.
- E10: A header-only recording gives a header-only output.

## Non-functional requirements

- N1: Names are replaced whole by invented ones — `Island <n>` for islands, `Player <n>` for figures, numbered from 1
  in the order of first appearance — never derived from the original text.
- N2: The receive times (every frame line and the header's `started`) move earlier by one offset per run, drawn at
  random between 30 and 365 days with a random part below one day, so neither the date nor the time of day survives;
  every pause between two lines is kept to the 100 ns. Neither the offset nor a perturbation factor is written or
  printed, and each run draws new ones. Fixture file names and the fixtures README never carry an original file name
  or time.
- N3: Economic values, per entry:
  - Generation, consumption, perfect generation, perfect consumption, income and summed productivity are multiplied
    by one factor per entry, drawn at random from 0.80 to 0.99 or 1.01 to 1.20.
  - Delta becomes generation minus consumption and average productivity 100 times summed productivity divided by the
    building count, each from the perturbed values (0 with a building count of 0); a non-zero one that would equal
    its original moves to the adjacent float.
  - Maintenance and every workforce amount change on their own, keeping their sign and never becoming 0: by exactly 1
    in magnitude when below 5, else by 1 % to 20 %, rounded, and at least 1.
  - Profit becomes an integer within 1 of the new income minus maintenance, on the same side of it as the original
    was; a non-zero profit never keeps its value, a zero one only while 0 is within 1; its sign may change.
- N4: Kept unchanged: line count and order, frame types, `Version`, session end, session ID, island ID, area index,
  session GUID, game time, entry count and order, product GUIDs, building count, the building map, and the workforce
  map's GUIDs, pair count and order.
- N5: The input is opened read-only and never changed. The anonymiser reads it twice — first to collect the distinct
  original names (memory bounded by those names), then to write — holding one line in memory at a time per pass,
  whatever the file size. It runs on Linux and Windows.
- N6: The deny list and the original recordings are never committed; the deny-list test reports only counts, line
  numbers and field names, never a denied name or an original value.

## Acceptance criteria

- AC1: An anonymised recording is a recording of format version 1 with the input's line count and order; its replay
  through the message reader and tick assembler gives the same items as the original's except names, economic values
  and receive times, which differ by the one offset (S1, S2, N4, E7, E8, E10).
- AC2: Every non-empty island and figure name is replaced as N1 says; one original name gets one invented name per
  file, different names different ones, and none equals an original name of the file (S3, N1, N5, E6, E7).
- AC3: For each entry one factor in the bounds of N3 relates every multiplied value to its original to float32
  rounding; delta, average productivity and profit are derived as N3 says; maintenance and workforce amounts follow
  N3's integer rule; zero, NaN and infinities are kept (N3, E8).
- AC4: All receive times move earlier by one offset of 30 to 365 days that is not a whole number of days, with every
  pause kept; two runs on one input give different offsets and factors, and neither run writes or prints them (N2).
- AC5: Each error of E1 to E5 prints its text, exits with its code and leaves no file under the output name; the
  input is unchanged (E1–E5, N5).
- AC6: The fixtures the Product Manager produced with the anonymiser from the #17 recordings lie under `tests/` with a
  README naming each, how it was produced and the facts it shows; the fixture-based tests of the reader and the tick
  assembler check those facts and pass in CI (S4).
- AC7: The deny-list test is inconclusive without its environment variable; with it, it fails when a fixture holds a
  non-empty name that is not an invented name of N1, or one equal to a deny entry ignoring case, or a non-zero
  economic value equal to the value at the same place in its original, or when E9 applies; it reports only what N6
  allows (S5, E9, N6).
- AC8: `README.md` and the user guide describe the anonymiser — command, what it changes and keeps, errors — in
  English, then German, and the README no longer lists it as missing (S6).

## Out of scope

- Shipping the anonymiser in the Companion ZIP or as a Companion switch (open question 1); the user guide's advice to
  keep a raw recording private until a maintainer asks for it stays.
- Anonymising game files, logs or screenshots; cutting or merging recordings; producing the deny list automatically.
- Recordings of a format version other than 1; frames other than `Version` after a protocol version other than 2 (E2).

## Open questions

1. Who gets the anonymiser? Answered: by the Product Manager, a contributor tool only — a console project in the
   repository, run with `dotnet run` and the .NET SDK, not in the release ZIP; testers keep handing raw recordings to
   a maintainer privately.
2. Is the game time a timestamp to shift? Answered: no. Receive times tell when the player played, game time does not,
   and its values are what fixtures should show ([`docs/protocol.md`](../../docs/protocol.md), *Timestamp*).
3. How does a test find an "unaltered economic value"? Answered: by place. A set of values would fail on chance
   equalities (small integers always), so the deny list names each fixture's original and the test compares the value
   at the same place (AC7), as `.squad/routing.md` (*Escalation to the Product Manager*) describes.
4. Which values are economic? Answered: those of N3; structural counts are kept (N4).
5. Who makes the fixtures? Answered: the Product Manager, who alone holds the #17 recordings, runs the built
   anonymiser and hands in the outputs; a recording refused by E2 is reported to the Lead and left out of the set.

## Area documents

- [`pipe-recording.md`](../../docs/areas/pipe-recording.md): a section *Anonymising* — what is replaced, perturbed,
  shifted and kept, that the replay matches, the errors with their texts, and that the deny list stays local;
  *Privacy* and *Not here* name the anonymiser instead of #20; the index row in `docs/areas/README.md` and the *Not
  here* of [`pipe.md`](../../docs/areas/pipe.md) follow.

## Challenge

1. Major, N3 unmeetable — accepted: one factor per entry for the float amounts, delta, average productivity and
   profit derived, a change of 1 for small integers, profit's own bounds waived.
2. E8 against N4 and AC7 — accepted: map pairs are copied as on the wire. Trailing bytes stay dropped: AC1 compares
   replayed items, not bytes.
3. Time shift — accepted: 30 to 365 days plus a part below one day (N2, AC4); no original name or time in fixtures.
4. Two passes — accepted (N5, E1).
5. A refused reference recording — accepted: AC6 names the fixtures produced (open question 5).
6. Name match — accepted with another rule: every non-empty fixture name must be an invented name and equal no deny
   entry (AC7), so no substring rule is needed and a short entry cannot fail on `Island <n>`.

## Amendments

| Criterion | Change | Reason | Step | Decided by |
| --------- | ------ | ------ | ---- | ---------- |
| AC3 | Profit's derivation in N3: an integer within 1 of the new income minus maintenance on the original's side, instead of the original plus the rounded change | The old rule reached 1.5 away on the Product Manager's real output (2 of 21,091 entries) | 6 (after the fixture pause) | Product Manager |
| AC3 | N3: a derived delta or average productivity, and any perturbed float, never keeps a non-zero original value (the adjacent float instead) | The Product Manager's deny-list run found one delta equal to its original (rounding of `gen' − cons'`) | 6 (after task 12) | Lead |
