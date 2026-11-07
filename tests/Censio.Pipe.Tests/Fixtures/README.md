# Test fixtures

Anonymised recordings of the Anno 117 statistics pipe (recording format version 1, protocol version 2), read by the
fixture-based tests of `Censio.Pipe.Tests`. The Product Manager made them from the recordings of issue #17 with the
anonymiser of the pull request that added the fixtures:

```bash
dotnet run --project src/Censio.Anonymiser -c Release -- <recording> <fixture>.jsonl
```

Every name in them is invented (`Island <n>`, `Player <n>`), every economic value perturbed and every receive time
shifted by a random offset; the structure, the game times and the pauses are kept
([`pipe-recording.md`](../../../docs/areas/pipe-recording.md), *Anonymising*). No original recording, original name,
original file name or original time is in the repository, and none may be added: the raw recordings stay with the
Product Manager.

## Fixtures

| Fixture | Variants it shows |
| ------- | ----------------- |
| `long-session.jsonl` | a long session (about 1.5 hours of receive time, 31 ticks with data); islands of three regions (three session GUIDs); an island renamed during the session; a pause of about 18 minutes between two ticks; a session end before the first session start; a session end followed by a new session start of the same figure; ticks without data at the start and at the end |

## Facts the tests assert

`PipeMessageReaderFixturesGiveReadmeFrameCounts` (in `PipeMessageReaderTests`) replays each fixture through the
message reader, `PipeTickAssemblerFixturesGiveReadmeTicksAndEvents` (in `PipeTickAssemblerTests`) through the message
reader and the tick assembler; both expect the facts below and no problem of the file. A new fixture gets a row above,
its facts below and a `DataRow` in both tests.

`long-session.jsonl`:

- 795 lines: the header and 794 frames, all decoded: 1 `Version` (2), 2 session starts, 2 session ends, 789 island
  frames with 21,091 production entries, no error.
- Replayed: `Version`, `RepeatedSessionEnd` (the session end before the first session start), `SessionStart`,
  32 ticks, `SessionEnd`, `SessionStart`, 1 tick, end of stream.
- 33 ticks: 1 closed because the game time changed (the first, without data), 31 by the quiet period, 1 by the end of
  the stream (the last, without data); 2 ticks without data.
- 24 island keys, 1 of them renamed (it carries two names); the figure is `Player 1` in both session starts.

The regions and the pause are shown by the file but not asserted.

## Deny-list check

`FixtureDenyListTests` check the fixtures with `FixtureDenyList`:

- `FixtureDenyListFixturesHaveInventedNames` always runs: every non-empty island name of every fixture is
  `Island <n>`, every figure name `Player <n>`, `n` from 1 without leading zeros.
- `FixtureDenyListFixturesAgainstLocalDenyList` runs only when the environment variable `CENSIO_FIXTURE_DENY_LIST` holds
  the path of the local deny list; without it the test is reported inconclusive, not passed.

The deny list is a UTF-8 text file kept outside the repository (as `censio-fixture-deny-list.txt`); it is never
committed. Its lines:

- `deny <name>`: a name that may not occur. Everything after `deny ` is the name; it is matched against every
  non-empty name of the fixtures ignoring case, so an invented name equal to a denied one fails too.
- `original <fixture file name> <path>`: the original recording of a fixture; the path is absolute or relative to the
  deny list's folder. Every fixture needs one.
- Empty lines and lines starting with `#` are skipped; any other line fails the check.

With an `original` line, the fixture and its original are walked line by line, and every non-zero, finite economic
value of the original (generation, consumption, delta, perfect generation, perfect consumption, maintenance, income,
profit, summed and average productivity, and each workforce amount by GUID after the reader's merge) must differ from
the value at the same line, entry and field of the fixture. A missing or unreadable original fails and names the
fixture. The report holds counts, fixture names, line numbers and field names only, never a name, a value or a path.
