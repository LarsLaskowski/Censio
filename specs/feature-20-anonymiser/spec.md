# Spec: Anonymiser for recordings and test fixtures

Source: Issue #20
Status: Draft

## Problem

The parser and the tick assembler are tested only against hand-built frames (`PipeFrameBuilder`); the four reference
recordings of #17 show what the game really sends ([`docs/protocol.md`](../../docs/protocol.md)), but they hold the
player figure's name, island names and exact economic data, so they may not be committed (record
[0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md)). There is no tool to anonymise a recording
(`README.md`, *What `v0.1.0` cannot do*), so no real recording can become a fixture, and record 0029 requires that
tool before the first one is committed.

## Scenarios

The anonymiser is a console program for contributors, run from the repository with the .NET SDK (open question 1).
It reads a recording of format version 1 ([`pipe-recording.md`](../../docs/areas/pipe-recording.md), *File
format*) and writes an anonymised copy in the same format.

- S1: Given a recording, when the user runs the anonymiser with the recording and a new output path, then it writes
  the output file, prints `Anonymised <n> frames to <output>` and exits with code 0. In the output every island name
  and the player figure's name are invented, every economic value is perturbed, every receive time is shifted, and
  everything else of every frame is unchanged.
- S2: Given the output of S1, when it is replayed with `--replay`, then the replay shows the same ticks, session events
  and version messages in the same order as a replay of the original, with the same island keys, game times and close
  reasons; only names, economic values and receive times differ.
- S3: Given a recording in which one island is renamed, or two figures start sessions, when it is anonymised, then one
  original name always gets the same invented name in that file and two different names get two different invented
  names, so the rename and the change of figure remain visible.
- S4: Given the fixtures under `tests/` with their README, when the test suite runs (locally or in CI), then the
  fixture-based tests read each fixture through the message reader and the tick assembler and find the facts the
  README states for it (frame counts by type, ticks with their close reasons, session events, a rename).
- S5: Given a maintainer with a local deny list (the names to deny and, for each fixture, the path of its original
  recording), when the suite runs with the environment variable that points to the deny list, then the deny-list test
  checks every fixture against it; without the variable the test is reported inconclusive, not passed.
- S6: Given an external tester with the .NET SDK, when they read `README.md` or the user guide, then they find how to
  run the anonymiser, what it changes and what it keeps (English first, then German).

## Errors and edge cases

The anonymiser prints one line `Error: <reason>` and exits with code 1; no file exists under the output name
afterwards (a partly written output is removed).

- E1: The recording is refused for the reasons a replay refuses it, with the same texts: `file not found`, `not a
  recording`, `unsupported version`, `invalid line <n>`, `read failed` ([`pipe-recording.md`](../../docs/areas/pipe-recording.md),
  *Limits and errors*, *Line rules*).
- E2: A frame whose content the anonymiser cannot decode — an unknown type, a malformed message, any frame other than
  `Version` before a `Version 2` or after a `Version` other than 2 — gives `frame cannot be anonymised in line <n>`:
  its bytes could hold a name, so it is never copied (fail closed). An empty frame holds nothing and is copied.
- E3: The output path exists: `output file exists`; it is never overwritten. The output path names the input file:
  `output is the input`.
- E4: The output cannot be created or written (missing folder, access denied, disk full): `write failed`.
- E5: Wrong arguments (none, one, more than two): `Usage: <command> <recording> <output>` and exit code 2.
- E6: An island or figure name that equals an invented name of the same file (an island really named like an
  invented one) still gets a different invented name: no invented name equals any original name of the file.
- E7: An empty name stays empty. Bytes after the last field of a frame are not copied.
- E8: A value that is zero stays zero; NaN and infinities stay as they are (they carry no amount). A map that repeats
  a GUID is written once with the summed amount, as the reader delivers it ([`pipe.md`](../../docs/areas/pipe.md),
  *Message types*).
- E9: The deny list names a fixture whose original is missing, or cannot be read: the deny-list test fails and names
  the fixture.
- E10: A recording that holds only the header gives an output that holds only the (shifted) header.

## Non-functional requirements

- N1: Nothing original survives in the output except what S1 keeps: names are replaced whole by invented ones
  (`Island 1`, `Island 2`, … for islands and `Player 1`, … for figures, numbered in the order of first appearance),
  never derived from the original text.
- N2: The receive times (every frame line and the header's `started`) move earlier by one offset of at least 30 days,
  chosen at random on each run; every pause between two lines is kept to the 100 ns. Each run draws new random
  values, and neither the offset nor any perturbation factor is written or printed, so the output cannot be turned
  back with a seed.
- N3: The economic values are generation, consumption, delta, perfect generation, perfect consumption, maintenance,
  income, profit, summed and average productivity, and the workforce amounts. Each non-zero one differs from its
  original by at least 1 % and at most 20 % (an integer by at least 1) and keeps its sign. The relations
  [`docs/protocol.md`](../../docs/protocol.md) confirms still hold to float32 rounding: delta is generation minus
  consumption, profit is within 1 of income minus maintenance, average productivity is 100 times summed productivity
  divided by the building count.
- N4: Kept unchanged: line count and order, the frame type, `Version`, session end, session ID, island ID, area index,
  session GUID, game time, entry count and order, product GUIDs, building count, the building map, and the GUIDs and
  pair count of the workforce map.
- N5: The input is opened read-only and never changed; the anonymiser holds one line in memory at a time, whatever the
  file size, and runs on Linux and Windows.
- N6: The deny list and the original recordings are never committed; the deny-list test reports only counts, line
  numbers and field names, never a denied name or an original value.

## Acceptance criteria

- AC1: An anonymised recording is a recording of format version 1 with the input's line count and order, and its
  replay through the message reader and tick assembler gives the same items as the original's except names, economic
  values and receive times, which differ by one offset (S1, S2, N4, E7, E8, E10).
- AC2: Every non-empty island and figure name is replaced by an invented name; one original name gets one invented
  name per file, different names get different ones, and none equals an original name of the file (S3, N1, E6, E7).
- AC3: Every non-zero economic value differs from its original within the bounds of N3 and keeps its sign, zero, NaN
  and infinities are kept, and the relations of N3 hold (N3, E8).
- AC4: All receive times move earlier by one offset of at least 30 days with every pause kept; two runs on the same
  input give different offsets and values, and neither run writes or prints them (N2).
- AC5: Each error of E1 to E5 prints its text, exits with its code and leaves no file under the output name; the input
  is unchanged (E1–E5, N5).
- AC6: Fixtures made with the anonymiser from the #17 reference recordings lie under `tests/` with a README stating how
  they were produced and the facts each one shows; the fixture-based tests of the reader and the tick assembler check
  those facts and pass in CI (S4).
- AC7: The deny-list test is inconclusive without its environment variable; with it, it fails when a fixture holds a
  listed name in any string (ignoring case) or a non-zero economic value equal to the value at the same place in its
  original, or when E9 applies, and reports only what N6 allows (S5, E9, N6).
- AC8: `README.md` and the user guide describe the anonymiser — its command, what it changes and keeps, its errors —
  in English and then German, and the README no longer lists it as missing (S6).

## Out of scope

- Shipping the anonymiser in the Companion ZIP or as a Companion switch (open question 1).
- Anonymising game files, logs or screenshots, and cutting or merging recordings.
- Recordings of a format version other than 1 and frames of a protocol version other than 2 (E2).
- Producing the deny list automatically.

## Open questions

1. Who gets the anonymiser: a contributor tool in the repository only, also a program in the Companion ZIP, or a
   Companion switch? Escalated to the Product Manager; this draft assumes the contributor tool.
2. Is the game time a timestamp to shift? Answered: no. The issue's "timestamps shifted" are the receive times, which
   tell when the player played; the game time tells nothing about the player, and its values (19,000 after a load, the
   multiples of 100, the tick spacing) are what the fixtures should show ([`docs/protocol.md`](../../docs/protocol.md),
   *Timestamp*).
3. How can a test find an "unaltered economic value"? Answered: by place, not by value. A value-set deny list would
   fail on every perturbed value that by chance equals some other original value (small integers always do), so the
   local deny list names the original of each fixture and the test compares each value with the one at the same place
   (AC7), as `.squad/routing.md` (*Escalation to the Product Manager*) describes for a sample kept outside the
   repository.
4. Which values are economic? Answered: N3 and N4 list them: the amounts of money, goods and workforce; counts that make up the
   structure (building count, building map, entry count) are kept.
5. Who makes the fixtures? Answered: only the Product Manager holds the #17 recordings (issue #17, shared outside the
   repository); they run the anonymiser once it is built and hand in the outputs, the squad writes the README and the
   tests against them.

## Area documents

- [`pipe-recording.md`](../../docs/areas/pipe-recording.md): a section *Anonymising* — what is replaced, perturbed,
  shifted and kept, the guarantee that the replay matches, the errors with their texts, and that the deny list stays
  local; *Privacy* and *Not here* name the anonymiser instead of #20; the index row in `docs/areas/README.md` and the
  *Not here* of [`pipe.md`](../../docs/areas/pipe.md) follow.
