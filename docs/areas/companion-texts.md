# Companion texts

## Scope

The language of the Companion's own texts: how it is chosen (the settings key `language`, else the Windows display
language), what follows it, what does not, and every text in English and German, so a tester who runs the Companion in
German can compare what they read. The tray menu, the tooltip, the settings window and the notifications are covered; the
diagnostics window, the log and the web pages are not. The window that sets the language is in
[`companion-settings.md`](companion-settings.md).

## Behavior

### Language

- The Companion's texts are English or German. The choice is the key `language` of the settings file ([`companion.md`](companion.md),
  *Settings file*): the text `en` or `de`, matched ignoring case. A wrong value (another text, a value that is not a
  text) or a key written twice is read as not set, and the Companion logs `Language setting invalid; the Windows display
  language is used` once at start ([`companion-log.md`](companion-log.md)).
- Without a valid choice the Windows display language decides: German when the display language is German in any region (`de`, `de-AT`,
  `de-CH`), otherwise English.
- The language is read once at start. Choosing `English` or `Deutsch` under `Language` in the settings window and saving
  switches the settings window, the tray menu, the tooltip and the notifications at once, without a restart, and writes
  `language` to the file ([`companion-settings.md`](companion-settings.md), *Writing the file*); a window that stays open shows its new
  language too. A save that does not touch `Language` keeps the key as it was and writes none when the file had none, so the Windows
  display language keeps deciding.
- Stay in English: the diagnostics window ([`companion-diagnostics.md`](companion-diagnostics.md)), the log lines
  ([`companion-log.md`](companion-log.md); the reason texts that also appear in the status are logged in English), the
  web pages ([`web-ui.md`](web-ui.md); they keep their own language switch), `Status:` and `Version <version>` of the menu,
  the product and the file names, and the setting keys and values (`mode`, `local`, `forward`) a player writes in the file.
- A text that is a name of the game or of the product (`Censio Companion`, `Ubisoft Connect`) is the same in both languages.

### Where the texts come from

- Every text the Companion shows lies in one table with an English and a German instance; a text that exists in one and
  not in the other does not build, and a test compares the two instances text by text ([`decisions/0113-companion-texts-in-english-and-german-from-one-table.md`](../decisions/0113-companion-texts-in-english-and-german-from-one-table.md)).
- A text with a value in it is a pattern: `{0}` is the first value and `{1}` the second, written in invariant culture, so
  numbers have no grouping and a time keeps its form (`HH:mm:ss`), also on a German Windows. Singular and plural are two patterns
  where English needs both (`1 island`, `1 Insel`).
- The tooltip is cut to 127 characters, the limit of the tray icon ([`companion.md`](companion.md), *Tray icon and menu*). The longest
  German tooltip of the case the tests build (a replay finished with 2,147,483,647 ticks sent and refused, and a last
  tick of 256 islands at the earliest time the Companion can show) is 126 characters.

## The texts

`{0}` and `{1}` stand for a number, a port, a time or a reason as the line names it. The German texts are the Companion's
wording; the Product Manager reads them in the manual test of the release.

#### Menu entries

| English | German |
| ------- | ------ |
| `Settings…` | `Einstellungen…` |
| `Open Censio` | `Censio öffnen` |
| `Open diagnostics` | `Diagnose öffnen` |
| `Start recording` | `Aufnahme starten` |
| `Stop recording` | `Aufnahme beenden` |
| `Open recordings folder` | `Aufnahmeordner öffnen` |
| `Open log folder` | `Protokollordner öffnen` |
| `Exit` | `Beenden` |

#### Tooltip and status

| English | German |
| ------- | ------ |
| `Censio Companion` | `Censio Companion` |
| `Waiting for game` | `Warte auf Spiel` |
| `Connected` | `Verbunden` |
| `Receiving` | `Empfange Daten` |
| `Pipe busy` | `Pipe belegt` |
| `Protocol mismatch` | `Protokollkonflikt` |
| `Error` | `Fehler` |
| ` (recording)` | ` (Aufnahme läuft)` |
| ` (recording failed)` | ` (Aufnahme fehlgeschlagen)` |
| `Censio Companion: Invalid command line` | `Censio Companion: Ungültige Befehlszeile` |
| `Invalid command line` | `Ungültige Befehlszeile` |
| `Last tick: none` | `Letzter Tick: keiner` |
| `Last tick: {0}, {1}` | `Letzter Tick: {0}, {1}` |
| `1 island` | `1 Insel` |
| `{0} islands` | `{0} Inseln` |

#### Replay

| English | German |
| ------- | ------ |
| `Replay: Connecting to server` | `Wiedergabe: Verbinde mit Server` |
| `Replay: {0}` | `Wiedergabe: {0}` |
| `Replay finished` | `Wiedergabe beendet` |
| `Replay finished: {0} tick sent, {1} refused` | `Wiedergabe beendet: {0} gesendet, {1} abgelehnt` |
| `Replay finished: {0} ticks sent, {1} refused` | `Wiedergabe beendet: {0} gesendet, {1} abgelehnt` |
| `Replay error: {0}` | `Wiedergabefehler: {0}` |
| `file not found` | `Datei nicht gefunden` |
| `not a recording` | `keine Aufnahme` |
| `unsupported version` | `Version nicht unterstützt` |
| `invalid line {0}` | `ungültige Zeile {0}` |
| `read failed` | `Lesen fehlgeschlagen` |
| `token file cannot be read` | `Token-Datei nicht lesbar` |
| `token file holds no valid token` | `Token-Datei ohne gültiges Token` |
| `server unreachable` | `Server nicht erreichbar` |
| `token refused` | `Token abgelehnt` |
| `another Companion is connected` | `anderer Companion verbunden` |
| `server does not support contract version 3` | `Server unterstützt Vertragsversion 3 nicht` |
| `server does not answer` | `Server antwortet nicht` |
| `server connection lost` | `Serververbindung verloren` |
| `invalid server reply` | `ungültige Serverantwort` |
| `local server not running` | `lokaler Server läuft nicht` |

#### Server and buffer lines

The pattern `{0} (profile {1})` ends every `Server:` line of a live run: `{0}` is the line above, `{1}` the whole name of the active profile, never cut; the longest line is 124 characters (German `Server: anderer Companion verbunden, neuer Versuch` with a name of 64).

| English | German |
| ------- | ------ |
| `Server: starting` | `Server: startet` |
| `Server: running at http://localhost:{0}/` | `Server: läuft unter http://localhost:{0}/` |
| `Server: port {0} in use` | `Server: Port {0} belegt` |
| `Server: failed` | `Server: fehlgeschlagen` |
| `Server: settings invalid` | `Server: Einstellungen ungültig` |
| `Server: connecting` | `Server: verbinde` |
| `Server: connected` | `Server: verbunden` |
| `Server: unreachable, retrying` | `Server: nicht erreichbar, neuer Versuch` |
| `Server: not answering, retrying` | `Server: antwortet nicht, neuer Versuch` |
| `Server: connection lost, retrying` | `Server: Verbindung verloren, neuer Versuch` |
| `Server: invalid reply, retrying` | `Server: ungültige Antwort, neuer Versuch` |
| `Server: another Companion is connected, retrying` | `Server: anderer Companion verbunden, neuer Versuch` |
| `Server: not connected` | `Server: nicht verbunden` |
| `Server: token refused` | `Server: Token abgelehnt` |
| `Server: does not support contract version 3` | `Server: unterstützt Vertragsversion 3 nicht` |
| `{0} (profile {1})` | `{0} (Profil {1})` |
| `Buffer: {0} waiting` | `Puffer: {0} wartend` |
| `, {0} dropped` | `, {0} verworfen` |
| `, not saved to disk` | `, nicht auf Datenträger gespeichert` |

#### Catalog and game lines

| English | German |
| ------- | ------ |
| `Catalog: reading` | `Katalog: wird gelesen` |
| `Catalog: checking` | `Katalog: wird geprüft` |
| `Catalog: extracting names` | `Katalog: Namen werden extrahiert` |
| `Catalog: extracting icons, {0} entries` | `Katalog: Symbole werden extrahiert, {0} Einträge` |
| `Catalog: {0} entries` | `Katalog: {0} Einträge` |
| `Catalog: {0} entries, icons failed` | `Katalog: {0} Einträge, Symbole fehlgeschlagen` |
| `Catalog: {0} entries, not cached` | `Katalog: {0} Einträge, nicht zwischengespeichert` |
| `Catalog: no game` | `Katalog: kein Spiel` |
| `Catalog: failed` | `Katalog: fehlgeschlagen` |
| `Game: searching` | `Spiel: wird gesucht` |
| `Game: build {0} ({1})` | `Spiel: Build {0} ({1})` |
| `unknown` | `unbekannt` |
| `Ubisoft Connect` | `Ubisoft Connect` |
| `settings file` | `Einstellungsdatei` |
| `Game: not found` | `Spiel: nicht gefunden` |
| `Game: folder in settings not found` | `Spiel: Ordner aus den Einstellungen nicht gefunden` |
| `Game: folder in settings is not a full path` | `Spiel: Ordner aus den Einstellungen ist kein vollständiger Pfad` |
| `Game: folder in settings is not an Anno 117 installation` | `Spiel: Ordner aus den Einstellungen ist keine Anno-117-Installation` |
| `Game: settings file invalid` | `Spiel: Einstellungsdatei ungültig` |

#### Settings window

| English | German |
| ------- | ------ |
| `Censio Companion settings` | `Censio Companion Einstellungen` |
| `Mode` | `Modus` |
| `Off` | `Aus` |
| `Local` | `Lokal` |
| `Forward` | `Weiterleiten` |
| `Local port` | `Lokaler Port` |
| `Server address` | `Serveradresse` |
| `Server token` | `Server-Token` |
| `Buffer hours` | `Pufferdauer in Stunden` |
| `Buffer megabytes` | `Puffergröße in Megabyte` |
| `Game folder` | `Spielordner` |
| `Browse…` | `Durchsuchen…` |
| `Empty: find automatically` | `Leer: automatisch suchen` |
| `Profile` | `Profil` |
| `New profile…` | `Neues Profil…` |
| `Name of the new profile` | `Name des neuen Profils` |
| `Rename profile…` | `Profil umbenennen…` |
| `Delete profile…` | `Profil löschen…` |
| `Merge profile…` | `Profil zusammenführen…` |
| `Start with Windows` | `Mit Windows starten` |
| `Language` | `Sprache` |
| `Save` | `Speichern` |
| `Cancel` | `Abbrechen` |

#### Profile renaming

`Rename profile…` is in the table above. `Cancel` of the dialog is `CancelText`.

| English | German |
| ------- | ------ |
| `Live cannot be renamed.` | `Live kann nicht umbenannt werden.` |
| `Rename profile` | `Profil umbenennen` |
| `New name of the profile {0}` | `Neuer Name des Profils {0}` |
| `Rename` | `Umbenennen` |

#### Profile deletion

`Delete profile…` is in the table above. `Cancel` of the dialog is `CancelText`.

| English | German |
| ------- | ------ |
| `Live and the active profile cannot be deleted.` | `Live und das aktive Profil können nicht gelöscht werden.` |
| `Delete profile?` | `Profil löschen?` |
| `The profile {0} is removed from this Companion, with the figures linked to it.` | `Das Profil {0} wird aus diesem Companion entfernt, mit den Figuren, die mit ihm verknüpft sind.` |
| `Also delete its history on the server` | `Auch seinen Verlauf auf dem Server löschen` |
| `1,000 changes already wait for the server.` | `Schon 1.000 Änderungen warten auf den Server.` |
| `Delete` | `Löschen` |
| `Deleted profiles whose history the server has not deleted yet: {0}` | `Gelöschte Profile, deren Verlauf der Server noch nicht gelöscht hat: {0}` |

#### Profile merging

`Merge profile…` is in the table above. `Cancel` of the dialog is `CancelText`. The queue-full text of the dialog `Delete profile?` (above) is the one shown in this dialog.

| English | German |
| ------- | ------ |
| `Live and the active profile cannot be merged into another.` | `Live und das aktive Profil können nicht in ein anderes Profil überführt werden.` |
| `Merge profile` | `Profil zusammenführen` |
| `Move the profile {0} into:` | `Das Profil {0} überführen in:` |
| `The profile {0} is removed from this Companion; its figures and its history on the server move to the profile chosen. This cannot be undone.` | `Das Profil {0} wird aus diesem Companion entfernt; seine Figuren und sein Verlauf auf dem Server gehen auf das gewählte Profil über. Das lässt sich nicht rückgängig machen.` |
| `Merge` | `Zusammenführen` |
| `Merged profiles whose history the server has not moved yet: {0}` | `Zusammengeführte Profile, deren Verlauf der Server noch nicht übernommen hat: {0}` |

#### Messages below the fields

| English | German |
| ------- | ------ |
| `Enter a whole number from 1 to 65535.` | `Geben Sie eine ganze Zahl von 1 bis 65535 ein.` |
| `Enter the server address.` | `Geben Sie die Serveradresse ein.` |
| `Enter a ws:// or wss:// address with a host, without user name, password or fragment.` | `Geben Sie eine ws://- oder wss://-Adresse mit Host ein, ohne Benutzername, Kennwort oder Fragment.` |
| `The token must be 32 to 1024 visible ASCII characters (! to ~).` | `Das Token muss aus 32 bis 1024 sichtbaren ASCII-Zeichen (! bis ~) bestehen.` |
| `Enter a whole number from 1 to 168.` | `Geben Sie eine ganze Zahl von 1 bis 168 ein.` |
| `Enter a whole number from 1 to 10240.` | `Geben Sie eine ganze Zahl von 1 bis 10240 ein.` |
| `Enter a full path, such as C:\Games\Anno 117.` | `Geben Sie einen vollständigen Pfad ein, zum Beispiel C:\Games\Anno 117.` |
| `This folder does not exist.` | `Dieser Ordner existiert nicht.` |
| `This folder is not an Anno 117 installation.` | `Dieser Ordner ist keine Anno-117-Installation.` |
| `Enter a name.` | `Geben Sie einen Namen ein.` |
| `Use at most 64 characters.` | `Verwenden Sie höchstens 64 Zeichen.` |
| `The name holds characters that cannot be used.` | `Der Name enthält Zeichen, die nicht verwendet werden können.` |
| `A profile of this name exists.` | `Ein Profil mit diesem Namen existiert bereits.` |
| `No more profiles can be created.` | `Es können keine weiteren Profile angelegt werden.` |
| `Choose a mode.` | `Wählen Sie einen Modus.` |
| `This setting is written more than once in the file.` | `Diese Einstellung ist mehrfach in der Datei angegeben.` |

#### Banner, outcomes and notifications

| English | German |
| ------- | ------ |
| `The settings file could not be read ({0}). Saving replaces it with the values shown.` | `Die Einstellungsdatei konnte nicht gelesen werden ({0}). Beim Speichern wird sie durch die angezeigten Werte ersetzt.` |
| `The profiles file could not be read. Saving a profile replaces it.` | `Die Profildatei konnte nicht gelesen werden. Beim Speichern eines Profils wird sie ersetzt.` |
| `Saving ends the running replay.` | `Das Speichern beendet die laufende Wiedergabe.` |
| `Saving ends the running recording.` | `Das Speichern beendet die laufende Aufnahme.` |
| `Messages that could not be saved to disk are lost.` | `Nachrichten, die nicht auf die Festplatte gespeichert werden konnten, gehen verloren.` |
| `Saved. The Companion restarts to apply the changes.` | `Gespeichert. Der Companion startet neu, um die Änderungen zu übernehmen.` |
| `The settings could not be saved. Details are in the log.` | `Die Einstellungen konnten nicht gespeichert werden. Details stehen im Protokoll.` |
| `The settings file was changed outside this window. Close the window and open it again.` | `Die Einstellungsdatei wurde außerhalb dieses Fensters geändert. Schließen Sie das Fenster und öffnen Sie es erneut.` |
| `The Companion could not restart. Exit and start it again to apply the changes.` | `Der Companion konnte nicht neu starten. Beenden Sie ihn und starten Sie ihn erneut, um die Änderungen zu übernehmen.` |
| `Start with Windows could not be changed.` | `Der Start mit Windows konnte nicht geändert werden.` |
| `This figure was last played with profile {0}. Click to open the settings and switch.` | `Diese Figur wurde zuletzt mit dem Profil {0} gespielt. Klicken Sie, um die Einstellungen zu öffnen und zu wechseln.` |
| `This figure is new to profile {0}. Click to open the settings to switch or create a profile.` | `Diese Figur ist für das Profil {0} neu. Klicken Sie, um die Einstellungen zu öffnen und zu wechseln oder ein Profil anzulegen.` |

Both notice texts are also the text of the pending notice's entry in the tray menu ([`companion-profiles.md`](companion-profiles.md), *Notice*).

## Limits and errors

- A text of a language that is missing cannot occur: both instances are built whole. A value in a text that is not a number or
  a port (a reason) is one of the texts above, in the language of the text that shows it.
- A German text longer than the tray icon's limit is cut, never wrapped; the tests keep the longest one under it.
- The German wording is not checked by a native speaker yet.

## Related decisions

- [`decisions/0113-companion-texts-in-english-and-german-from-one-table.md`](../decisions/0113-companion-texts-in-english-and-german-from-one-table.md) — one table of texts with an English and a German instance, chosen by `language`.

## Not here

- The window, its fields and its validation: [`companion-settings.md`](companion-settings.md); the notice and when it shows: [`companion-profiles.md`](companion-profiles.md).
- What a menu line means and when it shows: [`companion.md`](companion.md), [`local-mode.md`](local-mode.md), [`live-sending.md`](live-sending.md), [`game-installation.md`](game-installation.md), [`replay-sending.md`](replay-sending.md).
- The log's lines (always English): [`companion-log.md`](companion-log.md); the diagnostics window: [`companion-diagnostics.md`](companion-diagnostics.md).
