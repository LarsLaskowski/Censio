# Companion profiles

## Scope

The profiles the Companion keeps: the list and its file, creating and selecting a profile in the settings window, which
profile the live messages carry, the links from figures to profiles and the notification that follows from them, and the
privacy of the figure name. A player who plays more than one playthrough and a tester who reads the server's pages by
profile rely on it. How a profile reaches the server (the hello per connection) is in [`live-sending.md`](live-sending.md);
the window itself in [`companion-settings.md`](companion-settings.md).

## Behavior

### The list and its file

- The Companion keeps its profiles and the figure links in the file `%LOCALAPPDATA%\Censio\profiles.json`, fixed like the
  settings file. The list always holds `Live` first, with the identity `b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94`, the
  same on every start and for every server, so a history under `Live` continues across restarts and updates; the other
  profiles follow in the order of their creation, each with an identity the Companion made and a name. One profile is
  active; at the first start, and while the file is missing, it is `Live`.
- A missing file is the list with `Live` alone, and is valid. The file is written in the background after a change
  (*Writing*) in every mode, also when no live data is sent.
- Profiles are created and selected only; they are neither renamed nor deleted in the Companion. A profile's history is
  deleted on the pages ([`history-deletion.md`](history-deletion.md)); the list of the Companion keeps the profile.

### Creating and selecting

- In the settings window `Profile` lists the profiles and selects the active one; `New profile…` shows a name box
  ([`companion-settings.md`](companion-settings.md), *Fields*). `Save` with another selected profile makes it the active
  one; `Save` with a name typed creates the profile, makes it the active one and ignores the drop-down. Nothing else is
  needed: no restart, in both modes.
- The name's rules, whose messages are in [`companion-settings.md`](companion-settings.md) (*Validation*): not blank, at
  most 64 characters (UTF-16 code units), well-formed text without a control character, different from every other
  name ignoring case, and fewer than 1,000 profiles. A name is kept as typed, without trimming.
- The messages the Companion takes from the pipe after the save carry the active profile's identity
  ([`contract.md`](contract.md)); messages waiting in the buffer keep the profile they were taken for and reach the server
  under that profile's name ([`live-sending.md`](live-sending.md), *Start and hello*). A profile whose name the list no
  longer knows is sent on the current connection, and the server creates it without a name.
- The pages list a profile under the name the hello gave; a profile made and used offline appears with its name after the
  next connection.

### Figure links

- A *figure* is the player figure, whose name the game sends with a session start ([`pipe.md`](pipe.md)); it names the figure, not the savegame. The Companion links each figure it
  sees to a profile: the active one at its session start, moved to the newly selected profile by a switch during the session.
- The link holds the lowercase hex SHA-256 of the figure name's UTF-8 bytes (64 characters), never the name. At most
  1,000 links are kept; a new one beyond that drops the oldest.
- Links are made only in a run that sends live data: forward or local mode with a valid setting, a normal run or `--record`
  ([`live-sending.md`](live-sending.md), *Switching on*). A replay, mode `Off`, an invalid server setting and an invalid
  command line make no link and show no notice. Neither does a profiles file that cannot be used (*Limits and errors*).

### Notice

- At a session start, with the active profile `B`:
  - a figure linked to another profile `A` shows the notification `This figure was last played with profile A. Click to
    open the settings and switch.`;
  - a figure not linked yet, while `B` has a figure already, shows `This figure is new to profile B. Click to open the
    settings to switch or create a profile.`;
  - a first figure on a profile without one is linked silently.
  The profile names are the only data in a notice. The text is German with the language `Deutsch`
  ([`companion-texts.md`](companion-texts.md)).
- The notification is a Windows notification of the tray icon with the title `Censio Companion`, shown for up to 10 seconds
  (the time asked of Windows). A click opens the settings window as the menu entry does.
- A notice never holds a tick: ticks go on to the active profile `B`. A notice that is ignored links the figure to `B`; a
  switch in the window during the session moves the link to the newly selected profile.
- A notice stays pending until the session ends or a profile is selected or created. Each session start sets it (to none when the figure shows no notice, and with a profiles file that cannot be used); `ObserveSessionEnd`, a selection and a creation clear it. While it is pending the tray menu shows it as an enabled entry with the text of the notification, directly above `Settings…`; a click opens the settings window. Windows may hold the notification back while a game runs full screen (Focus assist, *Do not disturb*), so the entry keeps the notice. The tray icon shows its *notice* look meanwhile (blue square with a white `i`), unless the state is a problem; it is refreshed when the pending notice changes ([`companion.md`](companion.md)).
- The log says `Profile notice raised: <kind>` when the notice is raised, before it goes to the tray, and `Profile notice passed to Windows: <kind>` when the tray icon passed the notification to Windows (not when the icon is already disposed); the kind is `LastUsedWithOther` or `NewFigure`, never the profile name or the text ([`companion-log.md`](companion-log.md)).
- The notification and the click are not verified on a Windows machine yet (*Limits and errors*).

### Writing

- The file is written by a background writer, never by the tray thread: after each change it writes the whole state as
  `profiles.json.tmp` in the same folder, flushes it and moves it over the file. A failed write logs `Could not save the
  settings: <exception type>`, keeps the state in memory and tries again at the next change. At `Exit` the writer is
  given one second to write the latest state ([`companion.md`](companion.md), *Tray icon and menu*).
- The format is `{"version":1,"active":"<id>","profiles":[{"id":"<id>","name":"<name>"}],"figures":[{"figure":"<key>","profile":"<id>"}]}`,
  with the writer's non-ASCII and control characters escaped (`\uXXXX`). The largest file the writer produces is
  567,086 bytes (1,000 profiles of 64 characters each written as 6-byte escapes and 1,000 figures), under the reader's bound.

## Limits and errors

- The reader fails closed: a file it cannot use is read as `Live` alone, with no link made and no notice, and the
  window shows `The profiles file could not be read. Saving a profile replaces it.` The file is replaced only by a save in
  the window that creates or selects a profile, never by the figure links alone. The log gets `Profiles file invalid:
  <problem>` (Warning) with the problem `cannot be read`, `larger than 1 MiB` or `not of its format`, never a value.
- The file is invalid when it is larger than 1 MiB (1,048,576 bytes, judged on the raw bytes, one more read), cannot be read,
  is not JSON, is not an object, has a byte order mark, a comment or a trailing comma, or breaks the format: a missing,
  unknown or repeated property (compared case-sensitively on the unescaped name) at the root, in a profile or in a figure;
  a `version` other than the number 1 (`1.0` and `1e0` too); an `active`, `id` or `profile` that is not an identity in the
  `D` form (either letter case); a repeated identity; no profile, more than 1,000 profiles or no `Live` entry; the
  `active` or a figure's profile not in the list; a name that breaks the name rules or repeats another ignoring case, or
  that holds text that is not valid UTF-8; more than 1,000 figures; a figure key that is not 64 lowercase hex characters
  or that repeats.
- The Companion does not read the server's profile list: a profile of the server that was created elsewhere is not in the list.
- A lost or replaced file starts the list again with `Live`; the server keeps the profiles it knows.
- Not verified on Windows yet: that the notification appears as a Windows 11 notification, and that its click raises the
  event ([`decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md`](../decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md)).

### Privacy

- The figure name stays in the memory of the Companion's process: it is never logged, never sent, never written, and
  no notice quotes it ([`decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md`](../decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)).
  The file holds only the hash of each name; a hash of a known name can be recomputed, so the file is not shared.
- Profile names are the player's own text. They are in the notice, in the file and in the hello to the server the Companion
  is set to, in no log line.
- No type of the settings or profiles holds a generated text form that could show a token, a profile name or a figure.

## Related decisions

- [`decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md`](../decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md) — the list is kept locally, each hello names the next message's profile.
- [`decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md`](../decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md) — the profile is selected in the Companion, the figure name is only a hint.

## Not here

- The hello per profile, the reconnect and what is buffered: [`live-sending.md`](live-sending.md); the contract's profile fields: [`contract.md`](contract.md).
- The window, its fields and the settings file: [`companion-settings.md`](companion-settings.md); the texts of the notice in both languages: [`companion-texts.md`](companion-texts.md).
- How the server stores and lists profiles: [`server-state.md`](server-state.md), [`web-pages.md`](web-pages.md).
