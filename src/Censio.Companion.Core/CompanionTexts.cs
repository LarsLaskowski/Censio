using System.Globalization;
using System.Text;

namespace Censio.Companion.Core;

/// <summary>
/// Every text the Companion shows, in one language.
/// </summary>
public sealed class CompanionTexts
{
    #region Properties

    /// <summary>
    /// Gets the English texts.
    /// </summary>
    public static CompanionTexts English { get; } = new()
                                                    {
                                                        Language = CompanionLanguage.English,
                                                        ToolTip = "Censio Companion",
                                                        ExitText = "Exit",
                                                        InvalidCommandLineToolTip = "Censio Companion: Invalid command line",
                                                        InvalidCommandLineStatusText = "Invalid command line",
                                                        StartRecordingText = "Start recording",
                                                        StopRecordingText = "Stop recording",
                                                        OpenRecordingsFolderText = "Open recordings folder",
                                                        OpenLogFolderText = "Open log folder",
                                                        OpenDiagnosticsText = "Open diagnostics",
                                                        StartWithWindowsText = "Start with Windows",
                                                        OpenCensioText = "Open Censio",
                                                        OpenSettingsText = "Settings…",
                                                        StateWaitingForGame = "Waiting for game",
                                                        StateConnected = "Connected",
                                                        StateReceiving = "Receiving",
                                                        StatePipeBusy = "Pipe busy",
                                                        StateProtocolMismatch = "Protocol mismatch",
                                                        StateError = "Error",
                                                        RecordingSuffix = " (recording)",
                                                        RecordingFailedSuffix = " (recording failed)",
                                                        ReplayErrorFormat = CompositeFormat.Parse("Replay error: {0}"),
                                                        ReplayConnecting = "Replay: Connecting to server",
                                                        ReplayFinished = "Replay finished",
                                                        ReplayFinishedOneTickFormat = CompositeFormat.Parse("Replay finished: {0} tick sent, {1} refused"),
                                                        ReplayFinishedTicksFormat = CompositeFormat.Parse("Replay finished: {0} ticks sent, {1} refused"),
                                                        ReplayStateFormat = CompositeFormat.Parse("Replay: {0}"),
                                                        LastTickNone = "Last tick: none",
                                                        LastTickFormat = CompositeFormat.Parse("Last tick: {0}, {1}"),
                                                        OneIsland = "1 island",
                                                        IslandsFormat = CompositeFormat.Parse("{0} islands"),
                                                        LocalServerStarting = "Server: starting",
                                                        LocalServerRunningFormat = CompositeFormat.Parse("Server: running at http://localhost:{0}/"),
                                                        LocalServerPortInUseFormat = CompositeFormat.Parse("Server: port {0} in use"),
                                                        ServerProfileFormat = CompositeFormat.Parse("{0} (profile {1})"),
                                                        LocalServerFailed = "Server: failed",
                                                        LocalServerSettingsInvalid = "Server: settings invalid",
                                                        BufferFormat = CompositeFormat.Parse("Buffer: {0} waiting"),
                                                        BufferDroppedFormat = CompositeFormat.Parse(", {0} dropped"),
                                                        BufferMemoryOnly = ", not saved to disk",
                                                        LiveSendConnecting = "Server: connecting",
                                                        LiveSendConnected = "Server: connected",
                                                        LiveSendUnreachableRetrying = "Server: unreachable, retrying",
                                                        LiveSendNoAnswerRetrying = "Server: not answering, retrying",
                                                        LiveSendConnectionLostRetrying = "Server: connection lost, retrying",
                                                        LiveSendInvalidReplyRetrying = "Server: invalid reply, retrying",
                                                        LiveSendCompanionConnectedRetrying = "Server: another Companion is connected, retrying",
                                                        LiveSendNotConnected = "Server: not connected",
                                                        LiveSendTokenRefused = "Server: token refused",
                                                        LiveSendVersionNotSupported = "Server: does not support contract version 2",
                                                        CatalogReading = "Catalog: reading",
                                                        CatalogReadyFormat = CompositeFormat.Parse("Catalog: {0} entries"),
                                                        CatalogNoGame = "Catalog: no game",
                                                        CatalogFailed = "Catalog: failed",
                                                        CatalogChecking = "Catalog: checking",
                                                        CatalogExtractingNames = "Catalog: extracting names",
                                                        CatalogExtractingIconsFormat = CompositeFormat.Parse("Catalog: extracting icons, {0} entries"),
                                                        CatalogIconsFailedFormat = CompositeFormat.Parse("Catalog: {0} entries, icons failed"),
                                                        CatalogNotCachedFormat = CompositeFormat.Parse("Catalog: {0} entries, not cached"),
                                                        GameSearching = "Game: searching",
                                                        GameFoundFormat = CompositeFormat.Parse("Game: build {0} ({1})"),
                                                        GameBuildUnknown = "unknown",
                                                        GameSourceUbisoftConnect = "Ubisoft Connect",
                                                        GameSourceSettingsFile = "settings file",
                                                        GameNotFound = "Game: not found",
                                                        GameSettingsFolderNotFound = "Game: folder in settings not found",
                                                        GameSettingsFolderNotFullPath = "Game: folder in settings is not a full path",
                                                        GameSettingsFolderNotInstallation = "Game: folder in settings is not an Anno 117 installation",
                                                        GameSettingsFileInvalid = "Game: settings file invalid",
                                                        ReplayReasonFileNotFound = "file not found",
                                                        ReplayReasonNotARecording = "not a recording",
                                                        ReplayReasonUnsupportedVersion = "unsupported version",
                                                        ReplayReasonInvalidLineFormat = CompositeFormat.Parse("invalid line {0}"),
                                                        ReplayReasonReadFailed = "read failed",
                                                        ReplaySendReasonTokenFileCannotBeRead = "token file cannot be read",
                                                        ReplaySendReasonTokenFileHoldsNoValidToken = "token file holds no valid token",
                                                        ReplaySendReasonServerUnreachable = "server unreachable",
                                                        ReplaySendReasonTokenRefused = "token refused",
                                                        ReplaySendReasonCompanionConnected = "another Companion is connected",
                                                        ReplaySendReasonVersionNotSupported = "server does not support contract version 2",
                                                        ReplaySendReasonNoAnswer = "server does not answer",
                                                        ReplaySendReasonConnectionLost = "server connection lost",
                                                        ReplaySendReasonInvalidReply = "invalid server reply",
                                                        ReplaySendReasonLocalServerNotRunning = "local server not running",
                                                        ProblemPortRange = "Enter a whole number from 1 to 65535.",
                                                        ProblemAddressMissing = "Enter the server address.",
                                                        ProblemAddressInvalid = "Enter a ws:// or wss:// address with a host, without user name, password or fragment.",
                                                        ProblemTokenInvalid = "The token must be 32 to 1024 visible ASCII characters (! to ~).",
                                                        ProblemHoursRange = "Enter a whole number from 1 to 168.",
                                                        ProblemMegabytesRange = "Enter a whole number from 1 to 10240.",
                                                        ProblemFolderNotFullPath = "Enter a full path, such as C:\\Games\\Anno 117.",
                                                        ProblemFolderMissing = "This folder does not exist.",
                                                        ProblemFolderNotInstallation = "This folder is not an Anno 117 installation.",
                                                        ProblemNameBlank = "Enter a name.",
                                                        ProblemNameTooLong = "Use at most 64 characters.",
                                                        ProblemNameInvalidCharacters = "The name holds characters that cannot be used.",
                                                        ProblemNameExists = "A profile of this name exists.",
                                                        ProblemProfilesFull = "No more profiles can be created.",
                                                        ProblemModeInvalid = "Choose a mode.",
                                                        ProblemWrittenTwice = "This setting is written more than once in the file.",
                                                        NoticeLastUsedWithOtherFormat = CompositeFormat.Parse("This figure was last played with profile {0}. Click to open the settings and switch."),
                                                        NoticeNewFigureFormat = CompositeFormat.Parse("This figure is new to profile {0}. Click to open the settings to switch or create a profile."),
                                                        OutcomeWriteFailed = "The settings could not be saved. Details are in the log.",
                                                        OutcomeChangedOutside = "The settings file was changed outside this window. Close the window and open it again.",
                                                        OutcomeRestartFailed = "The Companion could not restart. Exit and start it again to apply the changes.",
                                                        OutcomeAutostartFailed = "Start with Windows could not be changed.",
                                                        SettingsWindowTitle = "Censio Companion settings",
                                                        ModeLabel = "Mode",
                                                        ModeOffText = "Off",
                                                        ModeLocalText = "Local",
                                                        ModeForwardText = "Forward",
                                                        LocalPortLabel = "Local port",
                                                        ServerAddressLabel = "Server address",
                                                        ServerTokenLabel = "Server token",
                                                        BufferHoursLabel = "Buffer hours",
                                                        BufferMegabytesLabel = "Buffer megabytes",
                                                        GameFolderLabel = "Game folder",
                                                        BrowseText = "Browse…",
                                                        GameFolderHint = "Empty: find automatically",
                                                        ProfileLabel = "Profile",
                                                        NewProfileText = "New profile…",
                                                        NewProfileNameLabel = "Name of the new profile",
                                                        LanguageLabel = "Language",
                                                        SaveText = "Save",
                                                        CancelText = "Cancel",
                                                        FileProblemFormat = CompositeFormat.Parse("The settings file could not be read ({0}). Saving replaces it with the values shown."),
                                                        ProfilesFileInvalidText = "The profiles file could not be read. Saving a profile replaces it.",
                                                        WarningReplayRunning = "Saving ends the running replay.",
                                                        WarningRecordingRunning = "Saving ends the running recording.",
                                                        WarningBufferMemoryOnly = "Messages that could not be saved to disk are lost.",
                                                        OutcomeRestarting = "Saved. The Companion restarts to apply the changes."
                                                    };

    /// <summary>
    /// Gets the German texts.
    /// </summary>
    public static CompanionTexts German { get; } = new()
                                                   {
                                                       Language = CompanionLanguage.German,
                                                       ToolTip = "Censio Companion",
                                                       ExitText = "Beenden",
                                                       InvalidCommandLineToolTip = "Censio Companion: Ungültige Befehlszeile",
                                                       InvalidCommandLineStatusText = "Ungültige Befehlszeile",
                                                       StartRecordingText = "Aufnahme starten",
                                                       StopRecordingText = "Aufnahme beenden",
                                                       OpenRecordingsFolderText = "Aufnahmeordner öffnen",
                                                       OpenLogFolderText = "Protokollordner öffnen",
                                                       OpenDiagnosticsText = "Diagnose öffnen",
                                                       StartWithWindowsText = "Mit Windows starten",
                                                       OpenCensioText = "Censio öffnen",
                                                       OpenSettingsText = "Einstellungen…",
                                                       StateWaitingForGame = "Warte auf Spiel",
                                                       StateConnected = "Verbunden",
                                                       StateReceiving = "Empfange Daten",
                                                       StatePipeBusy = "Pipe belegt",
                                                       StateProtocolMismatch = "Protokollkonflikt",
                                                       StateError = "Fehler",
                                                       RecordingSuffix = " (Aufnahme läuft)",
                                                       RecordingFailedSuffix = " (Aufnahme fehlgeschlagen)",
                                                       ReplayErrorFormat = CompositeFormat.Parse("Wiedergabefehler: {0}"),
                                                       ReplayConnecting = "Wiedergabe: Verbinde mit Server",
                                                       ReplayFinished = "Wiedergabe beendet",
                                                       ReplayFinishedOneTickFormat = CompositeFormat.Parse("Wiedergabe beendet: {0} gesendet, {1} abgelehnt"),
                                                       ReplayFinishedTicksFormat = CompositeFormat.Parse("Wiedergabe beendet: {0} gesendet, {1} abgelehnt"),
                                                       ReplayStateFormat = CompositeFormat.Parse("Wiedergabe: {0}"),
                                                       LastTickNone = "Letzter Tick: keiner",
                                                       LastTickFormat = CompositeFormat.Parse("Letzter Tick: {0}, {1}"),
                                                       OneIsland = "1 Insel",
                                                       IslandsFormat = CompositeFormat.Parse("{0} Inseln"),
                                                       LocalServerStarting = "Server: startet",
                                                       LocalServerRunningFormat = CompositeFormat.Parse("Server: läuft unter http://localhost:{0}/"),
                                                       LocalServerPortInUseFormat = CompositeFormat.Parse("Server: Port {0} belegt"),
                                                       ServerProfileFormat = CompositeFormat.Parse("{0} (Profil {1})"),
                                                       LocalServerFailed = "Server: fehlgeschlagen",
                                                       LocalServerSettingsInvalid = "Server: Einstellungen ungültig",
                                                       BufferFormat = CompositeFormat.Parse("Puffer: {0} wartend"),
                                                       BufferDroppedFormat = CompositeFormat.Parse(", {0} verworfen"),
                                                       BufferMemoryOnly = ", nicht auf Datenträger gespeichert",
                                                       LiveSendConnecting = "Server: verbinde",
                                                       LiveSendConnected = "Server: verbunden",
                                                       LiveSendUnreachableRetrying = "Server: nicht erreichbar, neuer Versuch",
                                                       LiveSendNoAnswerRetrying = "Server: antwortet nicht, neuer Versuch",
                                                       LiveSendConnectionLostRetrying = "Server: Verbindung verloren, neuer Versuch",
                                                       LiveSendInvalidReplyRetrying = "Server: ungültige Antwort, neuer Versuch",
                                                       LiveSendCompanionConnectedRetrying = "Server: anderer Companion verbunden, neuer Versuch",
                                                       LiveSendNotConnected = "Server: nicht verbunden",
                                                       LiveSendTokenRefused = "Server: Token abgelehnt",
                                                       LiveSendVersionNotSupported = "Server: unterstützt Vertragsversion 2 nicht",
                                                       CatalogReading = "Katalog: wird gelesen",
                                                       CatalogReadyFormat = CompositeFormat.Parse("Katalog: {0} Einträge"),
                                                       CatalogNoGame = "Katalog: kein Spiel",
                                                       CatalogFailed = "Katalog: fehlgeschlagen",
                                                       CatalogChecking = "Katalog: wird geprüft",
                                                       CatalogExtractingNames = "Katalog: Namen werden extrahiert",
                                                       CatalogExtractingIconsFormat = CompositeFormat.Parse("Katalog: Symbole werden extrahiert, {0} Einträge"),
                                                       CatalogIconsFailedFormat = CompositeFormat.Parse("Katalog: {0} Einträge, Symbole fehlgeschlagen"),
                                                       CatalogNotCachedFormat = CompositeFormat.Parse("Katalog: {0} Einträge, nicht zwischengespeichert"),
                                                       GameSearching = "Spiel: wird gesucht",
                                                       GameFoundFormat = CompositeFormat.Parse("Spiel: Build {0} ({1})"),
                                                       GameBuildUnknown = "unbekannt",
                                                       GameSourceUbisoftConnect = "Ubisoft Connect",
                                                       GameSourceSettingsFile = "Einstellungsdatei",
                                                       GameNotFound = "Spiel: nicht gefunden",
                                                       GameSettingsFolderNotFound = "Spiel: Ordner aus den Einstellungen nicht gefunden",
                                                       GameSettingsFolderNotFullPath = "Spiel: Ordner aus den Einstellungen ist kein vollständiger Pfad",
                                                       GameSettingsFolderNotInstallation = "Spiel: Ordner aus den Einstellungen ist keine Anno-117-Installation",
                                                       GameSettingsFileInvalid = "Spiel: Einstellungsdatei ungültig",
                                                       ReplayReasonFileNotFound = "Datei nicht gefunden",
                                                       ReplayReasonNotARecording = "keine Aufnahme",
                                                       ReplayReasonUnsupportedVersion = "Version nicht unterstützt",
                                                       ReplayReasonInvalidLineFormat = CompositeFormat.Parse("ungültige Zeile {0}"),
                                                       ReplayReasonReadFailed = "Lesen fehlgeschlagen",
                                                       ReplaySendReasonTokenFileCannotBeRead = "Token-Datei nicht lesbar",
                                                       ReplaySendReasonTokenFileHoldsNoValidToken = "Token-Datei ohne gültiges Token",
                                                       ReplaySendReasonServerUnreachable = "Server nicht erreichbar",
                                                       ReplaySendReasonTokenRefused = "Token abgelehnt",
                                                       ReplaySendReasonCompanionConnected = "anderer Companion verbunden",
                                                       ReplaySendReasonVersionNotSupported = "Server unterstützt Vertragsversion 2 nicht",
                                                       ReplaySendReasonNoAnswer = "Server antwortet nicht",
                                                       ReplaySendReasonConnectionLost = "Serververbindung verloren",
                                                       ReplaySendReasonInvalidReply = "ungültige Serverantwort",
                                                       ReplaySendReasonLocalServerNotRunning = "lokaler Server läuft nicht",
                                                       ProblemPortRange = "Geben Sie eine ganze Zahl von 1 bis 65535 ein.",
                                                       ProblemAddressMissing = "Geben Sie die Serveradresse ein.",
                                                       ProblemAddressInvalid = "Geben Sie eine ws://- oder wss://-Adresse mit Host ein, ohne Benutzername, Kennwort oder Fragment.",
                                                       ProblemTokenInvalid = "Das Token muss aus 32 bis 1024 sichtbaren ASCII-Zeichen (! bis ~) bestehen.",
                                                       ProblemHoursRange = "Geben Sie eine ganze Zahl von 1 bis 168 ein.",
                                                       ProblemMegabytesRange = "Geben Sie eine ganze Zahl von 1 bis 10240 ein.",
                                                       ProblemFolderNotFullPath = "Geben Sie einen vollständigen Pfad ein, zum Beispiel C:\\Games\\Anno 117.",
                                                       ProblemFolderMissing = "Dieser Ordner existiert nicht.",
                                                       ProblemFolderNotInstallation = "Dieser Ordner ist keine Anno-117-Installation.",
                                                       ProblemNameBlank = "Geben Sie einen Namen ein.",
                                                       ProblemNameTooLong = "Verwenden Sie höchstens 64 Zeichen.",
                                                       ProblemNameInvalidCharacters = "Der Name enthält Zeichen, die nicht verwendet werden können.",
                                                       ProblemNameExists = "Ein Profil mit diesem Namen existiert bereits.",
                                                       ProblemProfilesFull = "Es können keine weiteren Profile angelegt werden.",
                                                       ProblemModeInvalid = "Wählen Sie einen Modus.",
                                                       ProblemWrittenTwice = "Diese Einstellung ist mehrfach in der Datei angegeben.",
                                                       NoticeLastUsedWithOtherFormat = CompositeFormat.Parse("Diese Figur wurde zuletzt mit dem Profil {0} gespielt. Klicken Sie, um die Einstellungen zu öffnen und zu wechseln."),
                                                       NoticeNewFigureFormat = CompositeFormat.Parse("Diese Figur ist für das Profil {0} neu. Klicken Sie, um die Einstellungen zu öffnen und zu wechseln oder ein Profil anzulegen."),
                                                       OutcomeWriteFailed = "Die Einstellungen konnten nicht gespeichert werden. Details stehen im Protokoll.",
                                                       OutcomeChangedOutside = "Die Einstellungsdatei wurde außerhalb dieses Fensters geändert. Schließen Sie das Fenster und öffnen Sie es erneut.",
                                                       OutcomeRestartFailed = "Der Companion konnte nicht neu starten. Beenden Sie ihn und starten Sie ihn erneut, um die Änderungen zu übernehmen.",
                                                       OutcomeAutostartFailed = "Der Start mit Windows konnte nicht geändert werden.",
                                                       SettingsWindowTitle = "Censio Companion Einstellungen",
                                                       ModeLabel = "Modus",
                                                       ModeOffText = "Aus",
                                                       ModeLocalText = "Lokal",
                                                       ModeForwardText = "Weiterleiten",
                                                       LocalPortLabel = "Lokaler Port",
                                                       ServerAddressLabel = "Serveradresse",
                                                       ServerTokenLabel = "Server-Token",
                                                       BufferHoursLabel = "Pufferdauer in Stunden",
                                                       BufferMegabytesLabel = "Puffergröße in Megabyte",
                                                       GameFolderLabel = "Spielordner",
                                                       BrowseText = "Durchsuchen…",
                                                       GameFolderHint = "Leer: automatisch suchen",
                                                       ProfileLabel = "Profil",
                                                       NewProfileText = "Neues Profil…",
                                                       NewProfileNameLabel = "Name des neuen Profils",
                                                       LanguageLabel = "Sprache",
                                                       SaveText = "Speichern",
                                                       CancelText = "Abbrechen",
                                                       FileProblemFormat = CompositeFormat.Parse("Die Einstellungsdatei konnte nicht gelesen werden ({0}). Beim Speichern wird sie durch die angezeigten Werte ersetzt."),
                                                       ProfilesFileInvalidText = "Die Profildatei konnte nicht gelesen werden. Beim Speichern eines Profils wird sie ersetzt.",
                                                       WarningReplayRunning = "Das Speichern beendet die laufende Wiedergabe.",
                                                       WarningRecordingRunning = "Das Speichern beendet die laufende Aufnahme.",
                                                       WarningBufferMemoryOnly = "Nachrichten, die nicht auf die Festplatte gespeichert werden konnten, gehen verloren.",
                                                       OutcomeRestarting = "Gespeichert. Der Companion startet neu, um die Änderungen zu übernehmen."
                                                   };

    /// <summary>
    /// Gets the language of the texts.
    /// </summary>
    public required CompanionLanguage Language { get; init; }

    /// <summary>
    /// Gets the tooltip of the tray icon.
    /// </summary>
    public required string ToolTip { get; init; }

    /// <summary>
    /// Gets the text of the exit entry.
    /// </summary>
    public required string ExitText { get; init; }

    /// <summary>
    /// Gets the tooltip of the tray icon when the command line is invalid.
    /// </summary>
    public required string InvalidCommandLineToolTip { get; init; }

    /// <summary>
    /// Gets the status text when the command line is invalid.
    /// </summary>
    public required string InvalidCommandLineStatusText { get; init; }

    /// <summary>
    /// Gets the text of the entry that starts the recording.
    /// </summary>
    public required string StartRecordingText { get; init; }

    /// <summary>
    /// Gets the text of the entry that stops the recording.
    /// </summary>
    public required string StopRecordingText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the recordings folder.
    /// </summary>
    public required string OpenRecordingsFolderText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the log folder.
    /// </summary>
    public required string OpenLogFolderText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the diagnostics window.
    /// </summary>
    public required string OpenDiagnosticsText { get; init; }

    /// <summary>
    /// Gets the label of the box in the settings window that turns the start with Windows on or off.
    /// </summary>
    public required string StartWithWindowsText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens Censio in the browser.
    /// </summary>
    public required string OpenCensioText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the settings window.
    /// </summary>
    public required string OpenSettingsText { get; init; }

    /// <summary>
    /// Gets the text of the connection state waiting for the game.
    /// </summary>
    public required string StateWaitingForGame { get; init; }

    /// <summary>
    /// Gets the text of the connection state connected.
    /// </summary>
    public required string StateConnected { get; init; }

    /// <summary>
    /// Gets the text of the connection state receiving.
    /// </summary>
    public required string StateReceiving { get; init; }

    /// <summary>
    /// Gets the text of the connection state pipe busy.
    /// </summary>
    public required string StatePipeBusy { get; init; }

    /// <summary>
    /// Gets the text of the connection state protocol mismatch.
    /// </summary>
    public required string StateProtocolMismatch { get; init; }

    /// <summary>
    /// Gets the text of the connection state error.
    /// </summary>
    public required string StateError { get; init; }

    /// <summary>
    /// Gets the suffix of the status while the recording runs.
    /// </summary>
    public required string RecordingSuffix { get; init; }

    /// <summary>
    /// Gets the suffix of the status when the recording failed.
    /// </summary>
    public required string RecordingFailedSuffix { get; init; }

    /// <summary>
    /// Gets the format of the status of a replay problem, with the reason.
    /// </summary>
    public required CompositeFormat ReplayErrorFormat { get; init; }

    /// <summary>
    /// Gets the status of a replay that connects to the server.
    /// </summary>
    public required string ReplayConnecting { get; init; }

    /// <summary>
    /// Gets the status of a replay that finished without sending.
    /// </summary>
    public required string ReplayFinished { get; init; }

    /// <summary>
    /// Gets the format of the status of a replay that sent one tick, with the number of refused messages.
    /// </summary>
    public required CompositeFormat ReplayFinishedOneTickFormat { get; init; }

    /// <summary>
    /// Gets the format of the status of a replay that sent several ticks, with the number of refused messages.
    /// </summary>
    public required CompositeFormat ReplayFinishedTicksFormat { get; init; }

    /// <summary>
    /// Gets the format of the status of a running replay, with the connection state.
    /// </summary>
    public required CompositeFormat ReplayStateFormat { get; init; }

    /// <summary>
    /// Gets the text of the last tick when none was received.
    /// </summary>
    public required string LastTickNone { get; init; }

    /// <summary>
    /// Gets the format of the text of the last tick, with the time and the island count.
    /// </summary>
    public required CompositeFormat LastTickFormat { get; init; }

    /// <summary>
    /// Gets the island count of one island.
    /// </summary>
    public required string OneIsland { get; init; }

    /// <summary>
    /// Gets the format of the island count of several islands.
    /// </summary>
    public required CompositeFormat IslandsFormat { get; init; }

    /// <summary>
    /// Gets the text of the local server that starts.
    /// </summary>
    public required string LocalServerStarting { get; init; }

    /// <summary>
    /// Gets the format of the text of the running local server, with its port.
    /// </summary>
    public required CompositeFormat LocalServerRunningFormat { get; init; }

    /// <summary>
    /// Gets the format of the text of the local server whose port is in use, with the port.
    /// </summary>
    public required CompositeFormat LocalServerPortInUseFormat { get; init; }

    /// <summary>
    /// Gets the format of a text of the server line with the name of the active profile, with the text and the name.
    /// </summary>
    public required CompositeFormat ServerProfileFormat { get; init; }

    /// <summary>
    /// Gets the text of the local server that failed.
    /// </summary>
    public required string LocalServerFailed { get; init; }

    /// <summary>
    /// Gets the text of the local server whose settings are invalid.
    /// </summary>
    public required string LocalServerSettingsInvalid { get; init; }

    /// <summary>
    /// Gets the format of the text of the live buffer, with the number of waiting messages.
    /// </summary>
    public required CompositeFormat BufferFormat { get; init; }

    /// <summary>
    /// Gets the format of the part of the live buffer text that names the dropped messages.
    /// </summary>
    public required CompositeFormat BufferDroppedFormat { get; init; }

    /// <summary>
    /// Gets the part of the live buffer text that says the messages are not saved to disk.
    /// </summary>
    public required string BufferMemoryOnly { get; init; }

    /// <summary>
    /// Gets the text of the server connection that is being made.
    /// </summary>
    public required string LiveSendConnecting { get; init; }

    /// <summary>
    /// Gets the text of the server connection that is made.
    /// </summary>
    public required string LiveSendConnected { get; init; }

    /// <summary>
    /// Gets the text of the server connection that retries because the server is unreachable.
    /// </summary>
    public required string LiveSendUnreachableRetrying { get; init; }

    /// <summary>
    /// Gets the text of the server connection that retries because the server does not answer.
    /// </summary>
    public required string LiveSendNoAnswerRetrying { get; init; }

    /// <summary>
    /// Gets the text of the server connection that retries because the connection was lost.
    /// </summary>
    public required string LiveSendConnectionLostRetrying { get; init; }

    /// <summary>
    /// Gets the text of the server connection that retries because the reply was invalid.
    /// </summary>
    public required string LiveSendInvalidReplyRetrying { get; init; }

    /// <summary>
    /// Gets the text of the server connection that retries because another Companion is connected.
    /// </summary>
    public required string LiveSendCompanionConnectedRetrying { get; init; }

    /// <summary>
    /// Gets the text of the server connection that is not made.
    /// </summary>
    public required string LiveSendNotConnected { get; init; }

    /// <summary>
    /// Gets the text of the server connection that stopped because the token was refused.
    /// </summary>
    public required string LiveSendTokenRefused { get; init; }

    /// <summary>
    /// Gets the text of the server connection that stopped because the server does not support the contract version.
    /// </summary>
    public required string LiveSendVersionNotSupported { get; init; }

    /// <summary>
    /// Gets the text of the catalog that is read.
    /// </summary>
    public required string CatalogReading { get; init; }

    /// <summary>
    /// Gets the format of the text of the catalog that is ready, with its entry count.
    /// </summary>
    public required CompositeFormat CatalogReadyFormat { get; init; }

    /// <summary>
    /// Gets the text of the catalog when there is no game.
    /// </summary>
    public required string CatalogNoGame { get; init; }

    /// <summary>
    /// Gets the text of the catalog that failed.
    /// </summary>
    public required string CatalogFailed { get; init; }

    /// <summary>
    /// Gets the text of the catalog whose cache is checked.
    /// </summary>
    public required string CatalogChecking { get; init; }

    /// <summary>
    /// Gets the text of the catalog whose names are extracted.
    /// </summary>
    public required string CatalogExtractingNames { get; init; }

    /// <summary>
    /// Gets the format of the text of the catalog whose icons are extracted, with its entry count.
    /// </summary>
    public required CompositeFormat CatalogExtractingIconsFormat { get; init; }

    /// <summary>
    /// Gets the format of the text of the catalog whose icons failed, with its entry count.
    /// </summary>
    public required CompositeFormat CatalogIconsFailedFormat { get; init; }

    /// <summary>
    /// Gets the format of the text of the catalog that was not cached, with its entry count.
    /// </summary>
    public required CompositeFormat CatalogNotCachedFormat { get; init; }

    /// <summary>
    /// Gets the text of the game installation search that runs.
    /// </summary>
    public required string GameSearching { get; init; }

    /// <summary>
    /// Gets the format of the text of the found game installation, with its build and its source.
    /// </summary>
    public required CompositeFormat GameFoundFormat { get; init; }

    /// <summary>
    /// Gets the build of a found game installation whose build is not known.
    /// </summary>
    public required string GameBuildUnknown { get; init; }

    /// <summary>
    /// Gets the source of a game installation found through Ubisoft Connect.
    /// </summary>
    public required string GameSourceUbisoftConnect { get; init; }

    /// <summary>
    /// Gets the source of a game installation found through the settings file.
    /// </summary>
    public required string GameSourceSettingsFile { get; init; }

    /// <summary>
    /// Gets the text of the game installation that was not found.
    /// </summary>
    public required string GameNotFound { get; init; }

    /// <summary>
    /// Gets the text of a game folder of the settings that does not exist.
    /// </summary>
    public required string GameSettingsFolderNotFound { get; init; }

    /// <summary>
    /// Gets the text of a game folder of the settings that is not a full path.
    /// </summary>
    public required string GameSettingsFolderNotFullPath { get; init; }

    /// <summary>
    /// Gets the text of a game folder of the settings that is not an installation.
    /// </summary>
    public required string GameSettingsFolderNotInstallation { get; init; }

    /// <summary>
    /// Gets the text of an invalid settings file.
    /// </summary>
    public required string GameSettingsFileInvalid { get; init; }

    /// <summary>
    /// Gets the reason of a replay whose file was not found.
    /// </summary>
    public required string ReplayReasonFileNotFound { get; init; }

    /// <summary>
    /// Gets the reason of a replay whose file is not a recording.
    /// </summary>
    public required string ReplayReasonNotARecording { get; init; }

    /// <summary>
    /// Gets the reason of a replay whose recording has an unsupported version.
    /// </summary>
    public required string ReplayReasonUnsupportedVersion { get; init; }

    /// <summary>
    /// Gets the format of the reason of a replay with an invalid line, with the line number.
    /// </summary>
    public required CompositeFormat ReplayReasonInvalidLineFormat { get; init; }

    /// <summary>
    /// Gets the reason of a replay whose file could not be read.
    /// </summary>
    public required string ReplayReasonReadFailed { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the token file cannot be read.
    /// </summary>
    public required string ReplaySendReasonTokenFileCannotBeRead { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the token file holds no valid token.
    /// </summary>
    public required string ReplaySendReasonTokenFileHoldsNoValidToken { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the server is unreachable.
    /// </summary>
    public required string ReplaySendReasonServerUnreachable { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the token was refused.
    /// </summary>
    public required string ReplaySendReasonTokenRefused { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: another Companion is connected.
    /// </summary>
    public required string ReplaySendReasonCompanionConnected { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the server does not support the contract version.
    /// </summary>
    public required string ReplaySendReasonVersionNotSupported { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the server does not answer.
    /// </summary>
    public required string ReplaySendReasonNoAnswer { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the connection was lost.
    /// </summary>
    public required string ReplaySendReasonConnectionLost { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the reply was invalid.
    /// </summary>
    public required string ReplaySendReasonInvalidReply { get; init; }

    /// <summary>
    /// Gets the reason of a send problem: the local server is not running.
    /// </summary>
    public required string ReplaySendReasonLocalServerNotRunning { get; init; }

    /// <summary>
    /// Gets the message of a port that is not a whole number from 1 to 65535.
    /// </summary>
    public required string ProblemPortRange { get; init; }

    /// <summary>
    /// Gets the message of a missing server address.
    /// </summary>
    public required string ProblemAddressMissing { get; init; }

    /// <summary>
    /// Gets the message of an invalid server address.
    /// </summary>
    public required string ProblemAddressInvalid { get; init; }

    /// <summary>
    /// Gets the message of an invalid token.
    /// </summary>
    public required string ProblemTokenInvalid { get; init; }

    /// <summary>
    /// Gets the message of buffer hours that are not a whole number from 1 to 168.
    /// </summary>
    public required string ProblemHoursRange { get; init; }

    /// <summary>
    /// Gets the message of buffer megabytes that are not a whole number from 1 to 10240.
    /// </summary>
    public required string ProblemMegabytesRange { get; init; }

    /// <summary>
    /// Gets the message of a game folder that is not a full path.
    /// </summary>
    public required string ProblemFolderNotFullPath { get; init; }

    /// <summary>
    /// Gets the message of a game folder that does not exist.
    /// </summary>
    public required string ProblemFolderMissing { get; init; }

    /// <summary>
    /// Gets the message of a game folder that is not an installation.
    /// </summary>
    public required string ProblemFolderNotInstallation { get; init; }

    /// <summary>
    /// Gets the message of a blank profile name.
    /// </summary>
    public required string ProblemNameBlank { get; init; }

    /// <summary>
    /// Gets the message of a profile name that is too long.
    /// </summary>
    public required string ProblemNameTooLong { get; init; }

    /// <summary>
    /// Gets the message of a profile name with characters that cannot be used.
    /// </summary>
    public required string ProblemNameInvalidCharacters { get; init; }

    /// <summary>
    /// Gets the message of a profile name that exists.
    /// </summary>
    public required string ProblemNameExists { get; init; }

    /// <summary>
    /// Gets the message of a profile list that is full.
    /// </summary>
    public required string ProblemProfilesFull { get; init; }

    /// <summary>
    /// Gets the message of a setting that is written more than once in the file.
    /// </summary>
    public required string ProblemWrittenTwice { get; init; }

    /// <summary>
    /// Gets the message of a mode of the settings file that is no valid mode.
    /// </summary>
    public required string ProblemModeInvalid { get; init; }

    /// <summary>
    /// Gets the format of the notice of a figure last played with another profile, with the profile name.
    /// </summary>
    public required CompositeFormat NoticeLastUsedWithOtherFormat { get; init; }

    /// <summary>
    /// Gets the format of the notice of a figure that is new to the active profile, with the profile name.
    /// </summary>
    public required CompositeFormat NoticeNewFigureFormat { get; init; }

    /// <summary>
    /// Gets the message of settings that could not be written.
    /// </summary>
    public required string OutcomeWriteFailed { get; init; }

    /// <summary>
    /// Gets the message of a settings file that was changed outside the window.
    /// </summary>
    public required string OutcomeChangedOutside { get; init; }

    /// <summary>
    /// Gets the message of a Companion that could not restart.
    /// </summary>
    public required string OutcomeRestartFailed { get; init; }

    /// <summary>
    /// Gets the message of a start with Windows that could not be changed.
    /// </summary>
    public required string OutcomeAutostartFailed { get; init; }

    /// <summary>
    /// Gets the title of the settings window.
    /// </summary>
    public required string SettingsWindowTitle { get; init; }

    /// <summary>
    /// Gets the label of the mode of the settings window.
    /// </summary>
    public required string ModeLabel { get; init; }

    /// <summary>
    /// Gets the text of the mode that sends nothing.
    /// </summary>
    public required string ModeOffText { get; init; }

    /// <summary>
    /// Gets the text of the mode that runs the server in the Companion.
    /// </summary>
    public required string ModeLocalText { get; init; }

    /// <summary>
    /// Gets the text of the mode that forwards to a server.
    /// </summary>
    public required string ModeForwardText { get; init; }

    /// <summary>
    /// Gets the label of the local port.
    /// </summary>
    public required string LocalPortLabel { get; init; }

    /// <summary>
    /// Gets the label of the server address.
    /// </summary>
    public required string ServerAddressLabel { get; init; }

    /// <summary>
    /// Gets the label of the server token.
    /// </summary>
    public required string ServerTokenLabel { get; init; }

    /// <summary>
    /// Gets the label of the buffer hours.
    /// </summary>
    public required string BufferHoursLabel { get; init; }

    /// <summary>
    /// Gets the label of the buffer megabytes.
    /// </summary>
    public required string BufferMegabytesLabel { get; init; }

    /// <summary>
    /// Gets the label of the game folder.
    /// </summary>
    public required string GameFolderLabel { get; init; }

    /// <summary>
    /// Gets the text of the button that picks the game folder.
    /// </summary>
    public required string BrowseText { get; init; }

    /// <summary>
    /// Gets the hint of an empty game folder.
    /// </summary>
    public required string GameFolderHint { get; init; }

    /// <summary>
    /// Gets the label of the profile.
    /// </summary>
    public required string ProfileLabel { get; init; }

    /// <summary>
    /// Gets the text of the button that asks for a new profile.
    /// </summary>
    public required string NewProfileText { get; init; }

    /// <summary>
    /// Gets the label of the name of a new profile.
    /// </summary>
    public required string NewProfileNameLabel { get; init; }

    /// <summary>
    /// Gets the label of the language.
    /// </summary>
    public required string LanguageLabel { get; init; }

    /// <summary>
    /// Gets the text of the button that saves the settings.
    /// </summary>
    public required string SaveText { get; init; }

    /// <summary>
    /// Gets the text of the button that closes the settings window.
    /// </summary>
    public required string CancelText { get; init; }

    /// <summary>
    /// Gets the format of the banner of a settings file that could not be read, with the reason.
    /// </summary>
    public required CompositeFormat FileProblemFormat { get; init; }

    /// <summary>
    /// Gets the banner of a profiles file that could not be read.
    /// </summary>
    public required string ProfilesFileInvalidText { get; init; }

    /// <summary>
    /// Gets the warning that saving ends the running replay.
    /// </summary>
    public required string WarningReplayRunning { get; init; }

    /// <summary>
    /// Gets the warning that saving ends the running recording.
    /// </summary>
    public required string WarningRecordingRunning { get; init; }

    /// <summary>
    /// Gets the warning that the buffer is kept in memory only.
    /// </summary>
    public required string WarningBufferMemoryOnly { get; init; }

    /// <summary>
    /// Gets the message of saved settings that restart the Companion.
    /// </summary>
    public required string OutcomeRestarting { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets the texts of a language.
    /// </summary>
    /// <param name="language">The language</param>
    /// <returns>The texts</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="language" /> is not a defined value</exception>
    public static CompanionTexts For(CompanionLanguage language)
    {
        return language switch
               {
                   CompanionLanguage.English => English,
                   CompanionLanguage.German => German,
                   _ => throw new ArgumentOutOfRangeException(nameof(language), language, "The language is not defined")
               };
    }

    /// <summary>
    /// Gets the language a culture asks for.
    /// </summary>
    /// <param name="culture">The culture</param>
    /// <returns>German for a German culture, otherwise English</returns>
    public static CompanionLanguage LanguageOf(CultureInfo culture)
    {
        return string.Equals(culture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase)
                   ? CompanionLanguage.German
                   : CompanionLanguage.English;
    }

    /// <summary>
    /// Formats the text of a problem with a value of the settings window.
    /// </summary>
    /// <param name="kind">The problem kind</param>
    /// <returns>The text</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind" /> is not a defined value</exception>
    public string FormatProblem(SettingsProblemKind kind)
    {
        return kind switch
               {
                   SettingsProblemKind.PortRange => ProblemPortRange,
                   SettingsProblemKind.AddressMissing => ProblemAddressMissing,
                   SettingsProblemKind.AddressInvalid => ProblemAddressInvalid,
                   SettingsProblemKind.TokenInvalid => ProblemTokenInvalid,
                   SettingsProblemKind.HoursRange => ProblemHoursRange,
                   SettingsProblemKind.MegabytesRange => ProblemMegabytesRange,
                   SettingsProblemKind.FolderNotFullPath => ProblemFolderNotFullPath,
                   SettingsProblemKind.FolderMissing => ProblemFolderMissing,
                   SettingsProblemKind.FolderNotInstallation => ProblemFolderNotInstallation,
                   SettingsProblemKind.NameBlank => ProblemNameBlank,
                   SettingsProblemKind.NameTooLong => ProblemNameTooLong,
                   SettingsProblemKind.NameInvalidCharacters => ProblemNameInvalidCharacters,
                   SettingsProblemKind.NameExists => ProblemNameExists,
                   SettingsProblemKind.ProfilesFull => ProblemProfilesFull,
                   SettingsProblemKind.ModeInvalid => ProblemModeInvalid,
                   SettingsProblemKind.WrittenTwice => ProblemWrittenTwice,
                   _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The problem kind is not defined")
               };
    }

    /// <summary>
    /// Formats the text of a notice about the active profile.
    /// </summary>
    /// <param name="notice">The notice</param>
    /// <returns>The text</returns>
    /// <exception cref="ArgumentOutOfRangeException">The notice kind is not a defined value</exception>
    public string FormatNotice(ProfileNotice notice)
    {
        var format = notice.Kind switch
                     {
                         ProfileNoticeKind.LastUsedWithOther => NoticeLastUsedWithOtherFormat,
                         ProfileNoticeKind.NewFigure => NoticeNewFigureFormat,
                         _ => throw new ArgumentOutOfRangeException(nameof(notice), notice.Kind, "The notice kind is not defined")
                     };

        return string.Format(CultureInfo.InvariantCulture, format, notice.ProfileName);
    }

    /// <summary>
    /// Formats the banner of a settings file that could not be read.
    /// </summary>
    /// <param name="reason">The reason the log gives</param>
    /// <returns>The text</returns>
    public string FormatFileProblem(string reason)
    {
        return string.Format(CultureInfo.InvariantCulture, FileProblemFormat, reason);
    }

    /// <summary>
    /// Formats the text of the outcome of saving the settings.
    /// </summary>
    /// <param name="outcome">The outcome</param>
    /// <returns>The text, empty when the outcome has none of its own</returns>
    /// <exception cref="ArgumentOutOfRangeException">The outcome kind is not a defined value</exception>
    public string FormatOutcome(SettingsSaveOutcome outcome)
    {
        return outcome.Kind switch
               {
                   SettingsSaveKind.WriteFailed => OutcomeWriteFailed,
                   SettingsSaveKind.ChangedOutside => OutcomeChangedOutside,
                   SettingsSaveKind.RestartFailed => OutcomeRestartFailed,
                   SettingsSaveKind.Restarting => outcome.IsAutostartFailed ? OutcomeAutostartFailed : OutcomeRestarting,
                   SettingsSaveKind.Saved or SettingsSaveKind.Refused => outcome.IsAutostartFailed ? OutcomeAutostartFailed : string.Empty,
                   _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind, "The outcome kind is not defined")
               };
    }

    #endregion // Methods
}