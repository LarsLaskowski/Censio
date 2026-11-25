using System.Globalization;

using Censio.Pipe;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionTexts" />.
/// </summary>
[TestClass]
public sealed class CompanionTextsTests
{
    #region Constants

    /// <summary>
    /// The local receive time of <see cref="_oneIslandTick" /> and <see cref="_twoIslandTick" /> as the last-tick text shows it.
    /// </summary>
    private const string TickTime = "00:30:00";

    /// <summary>
    /// The build of the found game installation.
    /// </summary>
    private const string Build = "12345";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The clock of the last-tick texts: 2026-11-02 18:26:03 in a local time zone at one hour.
    /// </summary>
    private static readonly FakeTimeProvider _clock = CreateClock();

    /// <summary>
    /// A tick of today's local date with one island.
    /// </summary>
    private static readonly PipeTickSummary _oneIslandTick = new()
                                                             {
                                                                 ReceiveTime = new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.Zero),
                                                                 IslandCount = 1
                                                             };

    /// <summary>
    /// A tick of today's local date with two islands.
    /// </summary>
    private static readonly PipeTickSummary _twoIslandTick = _oneIslandTick with
                                                                            {
                                                                                IslandCount = 2
                                                                            };

    /// <summary>
    /// Every status of the server connection of forward mode the menu has a text for.
    /// </summary>
    private static readonly LiveSendStatus[] _liveSends = [
                                                              CreateLiveSend(LiveSendState.Connecting, null),
                                                              CreateLiveSend(LiveSendState.Connected, null),
                                                              CreateLiveSend(LiveSendState.Retrying, ReplaySendProblemKind.ServerUnreachable),
                                                              CreateLiveSend(LiveSendState.Retrying, ReplaySendProblemKind.NoAnswer),
                                                              CreateLiveSend(LiveSendState.Retrying, ReplaySendProblemKind.ConnectionLost),
                                                              CreateLiveSend(LiveSendState.Retrying, ReplaySendProblemKind.InvalidReply),
                                                              CreateLiveSend(LiveSendState.Retrying, ReplaySendProblemKind.CompanionConnected),
                                                              CreateLiveSend(LiveSendState.Retrying, null),
                                                              CreateLiveSend(LiveSendState.Stopped, ReplaySendProblemKind.TokenRefused),
                                                              CreateLiveSend(LiveSendState.Stopped, ReplaySendProblemKind.VersionNotSupported),
                                                              CreateLiveSend(LiveSendState.Stopped, null)
                                                          ];

    /// <summary>
    /// Every text the Companion shows by language, by name: each formats the text, or the part of it that has its own
    /// wording, from the texts of one language. The version entry and the bare application name are left out: they read
    /// <c>Version</c> and <c>Censio Companion</c> in both languages.
    /// </summary>
    private static readonly Dictionary<string, Func<CompanionTexts, string?>> _texts = CreateTexts();

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the name of every text of <see cref="_texts" />.
    /// </summary>
    private static IEnumerable<object[]> TextNames => _texts.Keys.Select(name => new object[] { name });

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Every tray, window and notice text reads differently in German than in English (S9).
    /// </summary>
    /// <param name="name">The name of the text in <see cref="_texts" /></param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(TextNames))]
    public void CompanionTextsGermanDiffersFromEnglishInEveryText(string name)
    {
        // Arrange
        var format = _texts[name];
        var english = format(CompanionTexts.English);

        // Act
        var german = format(CompanionTexts.German);

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(german), $"The German text should not be empty: {name}");
        Assert.AreNotEqual(english, german, $"The German text should differ from the English one: {name}");
    }

    /// <summary>
    /// A German culture, of any region, asks for German; every other culture, the invariant one included, for English (S9).
    /// </summary>
    /// <param name="cultureName">The name of the culture, empty for the invariant culture</param>
    /// <param name="expected">The expected language</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("de-DE", CompanionLanguage.German)]
    [DataRow("de-AT", CompanionLanguage.German)]
    [DataRow("de-CH", CompanionLanguage.German)]
    [DataRow("de", CompanionLanguage.German)]
    [DataRow("en-US", CompanionLanguage.English)]
    [DataRow("en-GB", CompanionLanguage.English)]
    [DataRow("fr-FR", CompanionLanguage.English)]
    [DataRow("nl-NL", CompanionLanguage.English)]
    [DataRow("", CompanionLanguage.English, DisplayName = "Invariant culture")]
    public void CompanionTextsLanguageOfCultureGivesGermanOnlyForGerman(string cultureName, CompanionLanguage expected)
    {
        // Arrange
        var culture = CultureInfo.GetCultureInfo(cultureName);

        // Act
        var language = CompanionTexts.LanguageOf(culture);

        // Assert
        Assert.AreEqual(expected, language, $"The culture {cultureName} should ask for {expected}");
    }

    /// <summary>
    /// Each language gives its own texts (S9).
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void CompanionTextsForGivesTextsOfLanguage()
    {
        // Act
        var english = CompanionTexts.For(CompanionLanguage.English);
        var german = CompanionTexts.For(CompanionLanguage.German);

        // Assert
        Assert.AreSame(CompanionTexts.English, english, "English should give the English texts");
        Assert.AreSame(CompanionTexts.German, german, "German should give the German texts");
    }

    /// <summary>
    /// A value outside its enumeration throws <see cref="ArgumentOutOfRangeException" /> for the argument that holds it,
    /// in each method that picks a text by kind.
    /// </summary>
    /// <param name="method">The method: For, FormatProblem, FormatNotice or FormatOutcome</param>
    /// <param name="parameter">The name of the parameter the exception should name</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("For", "language")]
    [DataRow("FormatProblem", "kind")]
    [DataRow("FormatNotice", "notice")]
    [DataRow("FormatOutcome", "outcome")]
    public void CompanionTextsUndefinedValueThrows(string method, string parameter)
    {
        // Arrange
        var call = CallWithUndefinedValue(method);

        // Act
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(call, $"{method} should refuse an undefined value");

        // Assert
        Assert.AreEqual(parameter, exception.ParamName, $"{method} should name the parameter {parameter}");
    }

    /// <summary>
    /// A saved or refused outcome has no text of its own: the autostart failure's text when the autostart could
    /// not be changed, otherwise none.
    /// </summary>
    /// <param name="kind">The outcome kind</param>
    /// <param name="isAutostartFailed">Whether the autostart could not be changed</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(SettingsSaveKind.Saved, false)]
    [DataRow(SettingsSaveKind.Saved, true)]
    [DataRow(SettingsSaveKind.Restarting, true)]
    [DataRow(SettingsSaveKind.Refused, false)]
    [DataRow(SettingsSaveKind.Refused, true)]
    public void CompanionTextsFormatOutcomeWithoutOwnTextGivesAutostartTextOrNone(SettingsSaveKind kind, bool isAutostartFailed)
    {
        // Arrange
        var texts = CompanionTexts.English;
        var expected = isAutostartFailed ? texts.OutcomeAutostartFailed : string.Empty;

        // Act
        var text = texts.FormatOutcome(CreateOutcome(kind, isAutostartFailed));

        // Assert
        Assert.AreEqual(expected, text, $"The outcome {kind} should give the autostart text only when the autostart failed");
    }

    /// <summary>
    /// A restarting outcome gives the restart text, or the autostart failure's text when the autostart could not be
    /// changed (S4).
    /// </summary>
    /// <param name="isAutostartFailed">Whether the autostart could not be changed</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(false, DisplayName = "Autostart changed")]
    [DataRow(true, DisplayName = "Autostart failed")]
    public void CompanionTextsFormatOutcomeRestartingGivesRestartOrAutostartText(bool isAutostartFailed)
    {
        // Arrange
        var texts = CompanionTexts.English;
        var expected = isAutostartFailed ? texts.OutcomeAutostartFailed : texts.OutcomeRestarting;

        // Act
        var text = texts.FormatOutcome(CreateOutcome(SettingsSaveKind.Restarting, isAutostartFailed));

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(texts.OutcomeRestarting), "The restart text should not be empty");
        Assert.AreEqual(expected, text, "A restarting outcome should give the restart text unless the autostart failed");
    }

    /// <summary>
    /// The banner of an unreadable settings file names the reason the log gives, in each language (E7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionTextsFormatFileProblemNamesReason()
    {
        // Arrange
        const string reason = "not JSON";

        // Act
        var english = CompanionTexts.English.FormatFileProblem(reason);
        var german = CompanionTexts.German.FormatFileProblem(reason);

        // Assert
        Assert.AreEqual("The settings file could not be read (not JSON). Saving replaces it with the values shown.", english, "The English banner should name the reason");
        Assert.AreEqual("Die Einstellungsdatei konnte nicht gelesen werden (not JSON). Beim Speichern wird sie durch die angezeigten Werte ersetzt.", german, "The German banner should name the reason");
    }

    /// <summary>
    /// Creates the call of a method of the texts with a value outside its enumeration.
    /// </summary>
    /// <param name="method">The method: For, FormatProblem, FormatNotice or FormatOutcome</param>
    /// <returns>The call</returns>
    private static Action CallWithUndefinedValue(string method)
    {
        var texts = CompanionTexts.English;

        return method switch
               {
                   "For" => () => CompanionTexts.For((CompanionLanguage)99),
                   "FormatProblem" => () => texts.FormatProblem((SettingsProblemKind)99),
                   "FormatNotice" => () => texts.FormatNotice(CreateNotice((ProfileNoticeKind)99)),
                   _ => () => texts.FormatOutcome(CreateOutcome((SettingsSaveKind)99, false))
               };
    }

    /// <summary>
    /// Creates every text of <see cref="_texts" />.
    /// </summary>
    /// <returns>The texts by name</returns>
    private static Dictionary<string, Func<CompanionTexts, string?>> CreateTexts()
    {
        return CreateTrayTexts().Concat(CreateServerTexts())
                                .Concat(CreateGameTexts())
                                .Concat(CreateReplayTexts())
                                .Concat(CreateWindowTexts())
                                .ToDictionary(text => text.Name, text => text.Format);
    }

    /// <summary>
    /// Creates the texts of the tooltip, the status, the recording suffixes and the last tick.
    /// </summary>
    /// <returns>The texts</returns>
    private static IEnumerable<(string Name, Func<CompanionTexts, string?> Format)> CreateTrayTexts()
    {
        var states = Enum.GetValues<PipeConnectionState>()
                         .Select(state => Text($"Tooltip {state}", texts => TrayMenu.FormatToolTip(texts, state)));

        return states.Append(Text("Recording suffix", texts => RecordingSuffix(texts, PipeRecordingState.Recording)))
                     .Append(Text("Recording failed suffix", texts => RecordingSuffix(texts, PipeRecordingState.Failed)))
                     .Append(Text("Status text", texts => TrayMenu.FormatStatusText(texts, PipeConnectionState.Receiving, PipeRecordingState.Recording)))
                     .Append(Text("Invalid command line status", texts => TraySnapshot.ForInvalidCommandLine(texts).StatusText))
                     .Append(Text("Invalid command line tooltip", texts => TrayMenu.FormatSnapshotToolTip(texts, TraySnapshot.ForInvalidCommandLine(texts), _clock)))
                     .Append(Text("Last tick none", texts => TrayMenu.FormatLastTick(texts, null, _clock)))
                     .Append(Text("Last tick", texts => TrayMenu.FormatLastTick(texts, _twoIslandTick, _clock)))
                     .Append(Text("Last tick one island", texts => After(TrayMenu.FormatLastTick(texts, _oneIslandTick, _clock), TickTime)))
                     .Append(Text("Last tick islands", texts => After(TrayMenu.FormatLastTick(texts, _twoIslandTick, _clock), TickTime)));
    }

    /// <summary>
    /// Creates the texts of the local server, the server connection and the buffer.
    /// </summary>
    /// <returns>The texts</returns>
    private static IEnumerable<(string Name, Func<CompanionTexts, string?> Format)> CreateServerTexts()
    {
        var local = Enum.GetValues<LocalServerState>()
                        .Select(state => Text($"Local server {state}", texts => TrayMenu.FormatLocalServer(texts, CreateLocalServer(state))));
        var live = _liveSends.Select(status => Text($"Server {status.State} {status.Problem}", texts => TrayMenu.FormatLiveSend(texts, status)));

        return local.Concat(live)
                    .Append(Text("Buffer", texts => TrayMenu.FormatLiveBuffer(texts, CreateBuffer(0, false))))
                    .Append(Text("Buffer dropped", texts => After(TrayMenu.FormatLiveBuffer(texts, CreateBuffer(2, false)), TrayMenu.FormatLiveBuffer(texts, CreateBuffer(0, false)))))
                    .Append(Text("Buffer memory only", texts => After(TrayMenu.FormatLiveBuffer(texts, CreateBuffer(0, true)), TrayMenu.FormatLiveBuffer(texts, CreateBuffer(0, false)))));
    }

    /// <summary>
    /// Creates the texts of the game and the catalog.
    /// </summary>
    /// <returns>The texts</returns>
    private static IEnumerable<(string Name, Func<CompanionTexts, string?> Format)> CreateGameTexts()
    {
        var games = Enum.GetValues<GameInstallationStatusKind>()
                        .Select(kind => Text($"Game {kind}", texts => TrayMenu.FormatGame(texts, CreateGame(kind, GameInstallationSource.UbisoftConnect, Build))));
        var catalogs = Enum.GetValues<GameCatalogStatusKind>()
                           .Select(kind => Text($"Catalog {kind}", texts => TrayMenu.FormatCatalog(texts, CreateCatalog(kind))));

        return games.Concat(catalogs)
                    .Append(Text("Game from settings file", texts => After(TrayMenu.FormatGame(texts, CreateGame(GameInstallationStatusKind.Found, GameInstallationSource.SettingsFile, Build)), Build)))
                    .Append(Text("Game of unknown build", texts => TrayMenu.FormatGame(texts, CreateGame(GameInstallationStatusKind.Found, GameInstallationSource.UbisoftConnect, null))));
    }

    /// <summary>
    /// Creates the texts of a replay: its states, its end and the reasons of its problems.
    /// </summary>
    /// <returns>The texts</returns>
    private static IEnumerable<(string Name, Func<CompanionTexts, string?> Format)> CreateReplayTexts()
    {
        var states = new[] { PipeConnectionState.Connected, PipeConnectionState.Receiving, PipeConnectionState.ProtocolMismatch }.Select(state => Text($"Replay {state}", texts => TrayMenu.FormatReplayToolTip(texts, CreateReplay(state, false, false))));
        var reasons = Enum.GetValues<PipeReplayProblemKind>()
                          .Select(kind => Text($"Replay reason {kind}", texts => TrayMenu.FormatReplayReason(texts, CreateReplayProblem(kind))));
        var sendReasons = Enum.GetValues<ReplaySendProblemKind>()
                              .Select(kind => Text($"Replay send reason {kind}", texts => TrayMenu.FormatReplaySendReason(texts, kind)));

        return states.Concat(reasons)
                     .Concat(sendReasons)
                     .Append(Text("Replay connecting", texts => TrayMenu.FormatReplayToolTip(texts, CreateReplay(PipeConnectionState.Connected, true, false))))
                     .Append(Text("Replay finished", texts => TrayMenu.FormatReplayToolTip(texts, CreateReplay(PipeConnectionState.Receiving, false, true))))
                     .Append(Text("Replay finished one tick", texts => After(TrayMenu.FormatReplayToolTip(texts, CreateSentReplay(1)), ": 1")))
                     .Append(Text("Replay finished ticks", texts => After(TrayMenu.FormatReplayToolTip(texts, CreateSentReplay(2)), ": 2")))
                     .Append(Text("Replay error",
                                  texts => TrayMenu.FormatReplayStatusText(texts,
                                                                           CreateReplay(PipeConnectionState.Receiving, false, false) with
                                                                                                                                     {
                                                                                                                                         Problem = CreateReplayProblem(PipeReplayProblemKind.ReadFailed)
                                                                                                                                     })))
                     .Append(Text("Replay send error",
                                  texts => TrayMenu.FormatReplayStatusText(texts,
                                                                           CreateReplay(PipeConnectionState.Receiving, false, false) with
                                                                                                                                     {
                                                                                                                                         SendProblem = ReplaySendProblemKind.TokenRefused
                                                                                                                                     })));
    }

    /// <summary>
    /// Creates the texts of the settings entry, the settings window and the notices: every problem, both notices and every
    /// outcome with a text of its own.
    /// </summary>
    /// <returns>The texts</returns>
    private static IEnumerable<(string Name, Func<CompanionTexts, string?> Format)> CreateWindowTexts()
    {
        var problems = Enum.GetValues<SettingsProblemKind>()
                           .Select(kind => Text($"Problem {kind}", texts => texts.FormatProblem(kind)));
        var notices = Enum.GetValues<ProfileNoticeKind>()
                          .Select(kind => Text($"Notice {kind}", texts => texts.FormatNotice(CreateNotice(kind))));
        var outcomes = new[] { SettingsSaveKind.WriteFailed, SettingsSaveKind.ChangedOutside, SettingsSaveKind.RestartFailed, SettingsSaveKind.Restarting }.Select(kind => Text($"Outcome {kind}", texts => texts.FormatOutcome(CreateOutcome(kind, false))));

        return problems.Concat(notices)
                       .Concat(outcomes)
                       .Append(Text("Outcome autostart failed", texts => texts.FormatOutcome(CreateOutcome(SettingsSaveKind.Saved, true))))
                       .Append(Text("Settings entry", texts => texts.OpenSettingsText));
    }

    /// <summary>
    /// Pairs the name of a text with its formatter.
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="format">The formatter</param>
    /// <returns>The pair</returns>
    private static (string Name, Func<CompanionTexts, string?> Format) Text(string name, Func<CompanionTexts, string?> format)
    {
        return (name, format);
    }

    /// <summary>
    /// Gets the recording suffix of the tooltip of a receiving connection.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="recording">The recording state</param>
    /// <returns>The suffix</returns>
    private static string RecordingSuffix(CompanionTexts texts, PipeRecordingState recording)
    {
        return After(TrayMenu.FormatToolTip(texts, PipeConnectionState.Receiving, recording), TrayMenu.FormatToolTip(texts, PipeConnectionState.Receiving));
    }

    /// <summary>
    /// Gets the part of a text after the first occurrence of a marker, or the whole text when it holds no marker, so a
    /// suffix or a counted word is compared on its own.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="marker">The marker</param>
    /// <returns>The part after the marker</returns>
    private static string After(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);

        return index < 0 ? text : text[(index + marker.Length)..];
    }

    /// <summary>
    /// Creates a status of the server connection of forward mode.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="problem">The problem, if any</param>
    /// <returns>The status</returns>
    private static LiveSendStatus CreateLiveSend(LiveSendState state, ReplaySendProblemKind? problem)
    {
        return new LiveSendStatus
               {
                   State = state,
                   Problem = problem
               };
    }

    /// <summary>
    /// Creates a status of the local server at port 5117.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The status</returns>
    private static LocalServerStatus CreateLocalServer(LocalServerState state)
    {
        return new LocalServerStatus
               {
                   State = state,
                   Port = 5117
               };
    }

    /// <summary>
    /// Creates a status of the live buffer with three waiting messages.
    /// </summary>
    /// <param name="dropped">The number of dropped messages</param>
    /// <param name="isMemoryOnly">Whether the buffer is kept in memory only</param>
    /// <returns>The status</returns>
    private static LiveBufferStatus CreateBuffer(int dropped, bool isMemoryOnly)
    {
        return new LiveBufferStatus
               {
                   Waiting = 3,
                   Dropped = dropped,
                   IsMemoryOnly = isMemoryOnly
               };
    }

    /// <summary>
    /// Creates a status of the game installation search.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <param name="source">The source of a found installation</param>
    /// <param name="build">The build of a found installation</param>
    /// <returns>The status</returns>
    private static GameInstallationStatus CreateGame(GameInstallationStatusKind kind, GameInstallationSource source, string? build)
    {
        return new GameInstallationStatus
               {
                   Kind = kind,
                   Source = source,
                   Build = build
               };
    }

    /// <summary>
    /// Creates a status of the game catalog with two entries.
    /// </summary>
    /// <param name="kind">The status kind</param>
    /// <returns>The status</returns>
    private static GameCatalogStatus CreateCatalog(GameCatalogStatusKind kind)
    {
        return new GameCatalogStatus
               {
                   Kind = kind,
                   EntryCount = 2
               };
    }

    /// <summary>
    /// Creates a status of a replay.
    /// </summary>
    /// <param name="state">The state of the replayed connection</param>
    /// <param name="isConnecting">Whether the replay connects to a server</param>
    /// <param name="isFinished">Whether the replay finished</param>
    /// <returns>The status</returns>
    private static PipeReplayStatus CreateReplay(PipeConnectionState state, bool isConnecting, bool isFinished)
    {
        return new PipeReplayStatus
               {
                   State = state,
                   IsConnecting = isConnecting,
                   IsFinished = isFinished
               };
    }

    /// <summary>
    /// Creates a problem of a replay at line 7.
    /// </summary>
    /// <param name="kind">The problem kind</param>
    /// <returns>The problem</returns>
    private static PipeReplayProblem CreateReplayProblem(PipeReplayProblemKind kind)
    {
        return new PipeReplayProblem
               {
                   Kind = kind,
                   LineNumber = 7
               };
    }

    /// <summary>
    /// Creates the status of a replay that finished after sending its ticks.
    /// </summary>
    /// <param name="ticks">The number of ticks sent</param>
    /// <returns>The status</returns>
    private static PipeReplayStatus CreateSentReplay(int ticks)
    {
        return CreateReplay(PipeConnectionState.Receiving, false, true) with
                                                                        {
                                                                            Sent = new ReplaySendCounts
                                                                                   {
                                                                                       Ticks = ticks,
                                                                                       Refused = 0
                                                                                   }
                                                                        };
    }

    /// <summary>
    /// Creates a notice about the profile <c>B</c>.
    /// </summary>
    /// <param name="kind">The notice kind</param>
    /// <returns>The notice</returns>
    private static ProfileNotice CreateNotice(ProfileNoticeKind kind)
    {
        return new ProfileNotice
               {
                   Kind = kind,
                   ProfileName = "B"
               };
    }

    /// <summary>
    /// Creates an outcome of saving the settings.
    /// </summary>
    /// <param name="kind">The outcome kind</param>
    /// <param name="isAutostartFailed">Whether the autostart could not be changed</param>
    /// <returns>The outcome</returns>
    private static SettingsSaveOutcome CreateOutcome(SettingsSaveKind kind, bool isAutostartFailed)
    {
        return new SettingsSaveOutcome
               {
                   Kind = kind,
                   IsAutostartFailed = isAutostartFailed
               };
    }

    /// <summary>
    /// Creates the clock of the last-tick texts.
    /// </summary>
    /// <returns>The clock</returns>
    private static FakeTimeProvider CreateClock()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 17, 26, 3, TimeSpan.Zero));

        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Censio Test", TimeSpan.FromHours(1), "Censio Test", "Censio Test"));

        return clock;
    }

    #endregion // Methods
}