using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for the merging of profiles in <see cref="CompanionProfiles" />: the merge itself, the queue it shares with the
/// deletions, the resolving of merged identities and the profiles file.
/// </summary>
[TestClass]
public sealed class CompanionProfilesMergeTests
{
    #region Constants

    /// <summary>
    /// The identity of the second profile of the test files.
    /// </summary>
    private const string SecondId = "0f8c2d3e-1111-4a2b-8c3d-00000000a002";

    /// <summary>
    /// An identity no profile of the test files has.
    /// </summary>
    private const string OtherId = "7d0e4c1b-2222-4f3a-9b4c-00000000b003";

    /// <summary>
    /// A third identity no profile of the test files has.
    /// </summary>
    private const string ThirdId = "3c1a9e7f-3333-4b5c-8d6e-00000000c004";

    /// <summary>
    /// A fourth identity no profile of the test files has.
    /// </summary>
    private const string FourthId = "5e2b8d4a-4444-4c6d-9e7f-00000000d005";

    /// <summary>
    /// The all-zero identity, which the contract refuses.
    /// </summary>
    private const string ZeroId = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// The name of the profile whose history is merged.
    /// </summary>
    private const string SourceName = "MARKER-Source";

    /// <summary>
    /// The name of another profile.
    /// </summary>
    private const string OtherName = "MARKER-Other";

    /// <summary>
    /// A figure whose session names the profile it was linked to.
    /// </summary>
    private const string FigureA = "MARKER-Figure-A";

    /// <summary>
    /// A figure linked to the merged profile.
    /// </summary>
    private const string FigureB = "MARKER-Figure-B";

    /// <summary>
    /// The line the merge logs.
    /// </summary>
    private const string MergedLine = "Profile merged";

    /// <summary>
    /// The start of the line of an invalid profiles file.
    /// </summary>
    private const string InvalidLinePrefix = "Profiles file invalid: ";

    /// <summary>
    /// The identity of the profile <c>Live</c>, as the file writes it.
    /// </summary>
    private const string LiveId = "b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94";

    /// <summary>
    /// A waiting deletion of the test files.
    /// </summary>
    private const string DeletionEntry = """{"id":"{third}","confirmed":false}""";

    /// <summary>
    /// A waiting merge of the test files.
    /// </summary>
    private const string MergeEntry = """{"id":"{other}","target":"{second}","confirmed":false}""";

    /// <summary>
    /// The deleted list of the valid test file: a deletion, then a merge.
    /// </summary>
    private const string BaseDeleted = $$"""[{{DeletionEntry}},{{MergeEntry}}]""";

    /// <summary>
    /// A valid version-2 file with a waiting deletion and a waiting merge.
    /// </summary>
    private const string BaseFile = $$"""{"version":2,"active":"{second}","profiles":[{"id":"{live}","name":"Live"},{"id":"{second}","name":"MARKER-Second"}],"figures":[],"deleted":{{BaseDeleted}}}""";

    /// <summary>
    /// The number of bytes by which a merge entry is longer than a deletion entry: <c>"target":"&lt;36&gt;",</c>.
    /// </summary>
    private const long TargetPropertyLength = 48;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-profiles-merge-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The logger of the profiles.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionProfilesMergeTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public CompanionProfilesMergeTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets the path of the profiles file in the temporary folder.
    /// </summary>
    private string ProfilesPath => Path.Combine(_root, "profiles.json");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the temporary folder.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// A merge removes the profile from the list, moves the figures linked to it to the target, clears a notice that named
    /// it, logs one line without a name or identity, raises the change of the removals once and leaves one merge waiting,
    /// whether the target is <c>Live</c>, the active profile or another profile (spec AC1).
    /// </summary>
    /// <param name="target">The target: live, active or other</param>
    /// <param name="active">The active profile at the merge: live or other</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("live", "other", DisplayName = "Target Live, another profile active")]
    [DataRow("other", "other", DisplayName = "Target the active profile")]
    [DataRow("other", "live", DisplayName = "Target another profile")]
    public void CompanionProfilesMergeMovesLinksAndRemovesSource(string target, string active)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var source = profiles.Create(SourceName).Id;
        var other = profiles.Create(OtherName).Id;
        var targetId = target == "live" ? CompanionProfile.Live.Id : other;

        profiles.Select(source);
        profiles.ObserveSessionStart(FigureA);
        profiles.ObserveSessionEnd();
        profiles.ObserveSessionStart(FigureB);
        profiles.ObserveSessionEnd();
        profiles.Select(active == "live" ? CompanionProfile.Live.Id : other);

        var notice = profiles.ObserveSessionStart(FigureA);
        var activeBefore = profiles.Active.Id;
        var raised = 0;
        var logged = _logger.Entries.Count;

        profiles.DeletionsChanged += (_, _) => raised++;

        // Act
        profiles.Merge(source, targetId);

        // Assert
        var lines = _logger.Entries.Skip(logged).ToList();

        var pending = profiles.PendingNotice;
        var activeAfter = profiles.Active.Id;

        profiles.ObserveSessionEnd();
        profiles.Select(target == "live" ? other : CompanionProfile.Live.Id);

        var movedNotice = profiles.ObserveSessionStart(FigureB);

        Assert.IsNotNull(notice, "The session of the figure on Live should have set a notice naming the source, or the check of the cleared notice proves nothing");
        Assert.IsNull(pending, "The merge should clear the pending notice");
        Assert.DoesNotContain(source, profiles.Profiles.Select(profile => profile.Id).ToList(), "The merged profile should leave the list");
        Assert.AreEqual(activeBefore, activeAfter, "The active profile should stay");
        Assert.IsNull(profiles.NameOf(source), "The merged profile's name should be unknown");
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, profiles.NameOf(targetId) ?? string.Empty)], Describe([movedNotice]), "The figure linked to the merged profile should be linked to the target");
        Assert.AreSequenceEqual([(LogLevel.Information, MergedLine)], lines.Select(line => (line.Level, line.Message)).ToList(), "The merge should log one line without a name or identity");
        Assert.AreEqual(1, raised, "The change of the removals should be raised once");
        Assert.AreSequenceEqual([source], profiles.WaitingMerges.ToArray(), "One merge should wait");
        Assert.AreSequenceEqual([(source, (Guid?)targetId, false)], profiles.WaitingRemovals.Select(removal => (removal.Id, removal.Target, removal.IsConfirmed)).ToArray(), "The merge should wait as the only removal, with its target");
    }

    /// <summary>
    /// A merge of <c>Live</c>, of the active or an unknown profile, into an unknown profile or into itself is refused with
    /// an <see cref="ArgumentException" /> and changes nothing: no profile, no queue, no line and no raised change (spec
    /// AC1).
    /// </summary>
    /// <param name="source">The source: live, active, unknown or other</param>
    /// <param name="target">The target: other, unknown or source</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("live", "other", DisplayName = "Source Live")]
    [DataRow("active", "other", DisplayName = "Source the active profile")]
    [DataRow("unknown", "other", DisplayName = "Source unknown")]
    [DataRow("other", "unknown", DisplayName = "Target unknown")]
    [DataRow("other", "source", DisplayName = "Target equal to the source")]
    public void CompanionProfilesMergeRefusedThrowsAndChangesNothing(string source, string target)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var listed = profiles.Create(SourceName).Id;
        var other = profiles.Create(OtherName).Id;

        profiles.Select(source == "active" ? listed : CompanionProfile.Live.Id);

        var sourceId = source switch
                       {
                           "live" => CompanionProfile.Live.Id,
                           "unknown" => new Guid(OtherId),
                           _ => listed
                       };
        var targetId = target switch
                       {
                           "unknown" => new Guid(ThirdId),
                           "source" => sourceId,
                           _ => other
                       };
        var before = profiles.Profiles.Select(profile => profile.Id).ToArray();
        var raised = 0;
        var logged = _logger.Entries.Count;

        profiles.DeletionsChanged += (_, _) => raised++;

        // Act
        var exception = Assert.ThrowsExactly<ArgumentException>(() => profiles.Merge(sourceId, targetId), "The merge should be refused");

        // Assert
        Assert.IsNotNull(exception, "The refusal should carry an exception");
        Assert.AreSequenceEqual(before, profiles.Profiles.Select(profile => profile.Id).ToArray(), "The profiles should stay as they were");
        Assert.IsEmpty(profiles.WaitingMerges, "No merge should wait");
        Assert.IsEmpty(profiles.WaitingRemovals, "No removal should wait");
        Assert.HasCount(logged, _logger.Entries, "A refused merge should log nothing");
        Assert.AreEqual(0, raised, "A refused merge should raise no change");
    }

    /// <summary>
    /// A merge while 500 deletions and 500 merges wait is refused with an <see cref="InvalidOperationException" /> and
    /// changes nothing: the profile stays listed and the queue as it was (spec AC1, N2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionProfilesMergeWithFullQueueThrowsAndChangesNothing()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var half = CompanionProfiles.MaxDeletions / 2;

        for (var index = 0; index < half; index++)
        {
            var created = profiles.Create("Deleted " + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

            profiles.Select(CompanionProfile.Live.Id);
            profiles.Delete(created.Id, true);
        }

        MergeIntoLive(profiles, half);

        var next = profiles.Create(SourceName);

        profiles.Select(CompanionProfile.Live.Id);

        var raised = 0;

        profiles.DeletionsChanged += (_, _) => raised++;

        // Act
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => profiles.Merge(next.Id, CompanionProfile.Live.Id), "A merge should be refused while 1,000 changes wait");

        // Assert
        Assert.IsNotNull(exception, "The refusal should carry an exception");
        Assert.IsTrue(profiles.IsDeletionQueueFull, "The queue should be full");
        Assert.HasCount(CompanionProfiles.MaxDeletions, profiles.WaitingRemovals, "The waiting removals should stay");
        Assert.HasCount(half, profiles.WaitingMerges, "The waiting merges should stay");
        Assert.Contains(next.Id, profiles.Profiles.Select(profile => profile.Id).ToList(), "The profile should stay listed");
        Assert.AreEqual(0, raised, "A refused merge should raise no change");
    }

    /// <summary>
    /// A merge while 1,000 removals are kept but not all wait forgets the oldest confirmed removal, never a waiting one:
    /// the merge waits last and the kept entries stay 1,000 (spec AC1, N2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task CompanionProfilesMergeWithFullQueueDropsOldestConfirmed()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var waiting = MergeIntoLive(profiles, 1);
        var confirmed = MergeIntoLive(profiles, CompanionProfiles.MaxDeletions - 1);

        foreach (var id in confirmed)
        {
            profiles.ConfirmMerge(id);
        }

        var next = profiles.Create(SourceName);

        profiles.Select(CompanionProfile.Live.Id);

        // Act
        await RunUntilCancelledAsync(profiles, running => running.Merge(next.Id, CompanionProfile.Live.Id));

        // Assert
        var text = await File.ReadAllTextAsync(ProfilesPath, TestContext.CancellationToken);

        Assert.AreEqual(confirmed[0], profiles.ResolveProfile(confirmed[0]), "The oldest confirmed merge should be forgotten to make room");
        Assert.AreEqual(CompanionProfile.Live.Id, profiles.ResolveProfile(confirmed[1]), "The next confirmed merge should be kept");
        Assert.AreEqual(CompanionProfile.Live.Id, profiles.ResolveProfile(confirmed[^1]), "The newest confirmed merge should be kept");
        Assert.AreSequenceEqual([waiting[0], next.Id], profiles.WaitingMerges.ToArray(), "The older waiting merge should never be forgotten, and the new merge should wait last");
        Assert.AreEqual(CompanionProfiles.MaxDeletions, text.Split("\"confirmed\":").Length - 1, "The kept removals should stay at the bound");
    }

    /// <summary>
    /// The queue is full when 999 deletions and one merge wait: a merge counts like a deletion (spec AC1, E3).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionProfilesIsDeletionQueueFullCountsMerges()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        for (var index = 0; index < CompanionProfiles.MaxDeletions - 1; index++)
        {
            var created = profiles.Create("Deleted " + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

            profiles.Select(CompanionProfile.Live.Id);
            profiles.Delete(created.Id, true);
        }

        var isFullBefore = profiles.IsDeletionQueueFull;

        // Act
        MergeIntoLive(profiles, 1);

        // Assert
        Assert.IsFalse(isFullBefore, "999 deletions should not fill the queue");
        Assert.IsTrue(profiles.IsDeletionQueueFull, "999 deletions and one merge should fill the queue");
        Assert.HasCount(1, profiles.WaitingMerges, "The merge should wait");
    }

    /// <summary>
    /// A waiting merge is written to the file and the profiles loaded again hold it as waiting with its target; once the
    /// server confirmed it, the merge no longer waits after a load (spec AC3, N1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task CompanionProfilesMergeWaitsAcrossReload()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var source = Guid.Empty;
        var target = Guid.Empty;

        // Act
        await RunUntilCancelledAsync(profiles,
                                     running =>
                                     {
                                         source = running.Create(SourceName).Id;
                                         target = running.Create(OtherName).Id;
                                         running.Select(CompanionProfile.Live.Id);
                                         running.Merge(source, target);
                                     });

        // Assert
        var reloaded = CompanionProfiles.Load(ProfilesPath, new RecordingLogger());

        Assert.IsFalse(reloaded.IsFileInvalid, "The written file should be valid");
        Assert.AreSequenceEqual([source], reloaded.WaitingMerges.ToArray(), "The merge should wait after the load");
        Assert.AreSequenceEqual([(source, (Guid?)target, false)], reloaded.WaitingRemovals.Select(removal => (removal.Id, removal.Target, removal.IsConfirmed)).ToArray(), "The merge should wait with its target after the load");
        Assert.DoesNotContain(source, reloaded.Profiles.Select(profile => profile.Id).ToList(), "The merged profile should not be listed");

        await RunUntilCancelledAsync(reloaded, running => running.ConfirmMerge(source));

        var confirmed = CompanionProfiles.Load(ProfilesPath, new RecordingLogger());

        Assert.IsEmpty(confirmed.WaitingMerges, "A confirmed merge should not wait after the next load");
        Assert.AreEqual(target, confirmed.ResolveProfile(source), "A confirmed merge should still resolve to its target");
    }

    /// <summary>
    /// A merged identity resolves to the profile that holds its history now: itself when it was not merged, the target,
    /// the last target of a chain of two merges, also once the merge is confirmed, and a deleted target as it is, so the
    /// caller can drop the message (spec AC4).
    /// </summary>
    /// <param name="scenario">The scenario: none, merged, confirmed, chain or deleted</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("none", DisplayName = "Not merged")]
    [DataRow("merged", DisplayName = "Merged")]
    [DataRow("confirmed", DisplayName = "Merged and confirmed")]
    [DataRow("chain", DisplayName = "Chain of two")]
    [DataRow("deleted", DisplayName = "Target deleted")]
    public void CompanionProfilesResolveProfileFollowsMerges(string scenario)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var first = profiles.Create(SourceName).Id;
        var second = profiles.Create(OtherName).Id;
        var third = profiles.Create("MARKER-Third").Id;

        profiles.Select(CompanionProfile.Live.Id);

        var expected = first;

        if (scenario != "none")
        {
            profiles.Merge(first, second);

            expected = second;
        }

        if (scenario == "confirmed")
        {
            profiles.ConfirmMerge(first);
        }

        if (scenario == "chain")
        {
            profiles.Merge(second, third);

            expected = third;
        }

        if (scenario == "deleted")
        {
            profiles.Delete(second, true);
        }

        // Act
        var resolved = profiles.ResolveProfile(first);

        // Assert
        Assert.AreEqual(expected, resolved, $"The identity should resolve to the profile that holds the history: {scenario}");
        Assert.AreEqual(scenario == "deleted", profiles.IsDeleted(expected), $"Only a deleted target should be on the deleted list: {scenario}");
    }

    /// <summary>
    /// Deletions and merges, waiting and confirmed, interleaved, are written in the order they were made, a merge as
    /// <c>{"id":"…","target":"…","confirmed":false}</c>, and read back in that order (spec AC5, N2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task CompanionProfilesMergeWrittenAndReadBackInOrder()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var removed = new Guid[4];
        var target = Guid.Empty;

        // Act
        await RunUntilCancelledAsync(profiles,
                                     running =>
                                     {
                                         target = running.Create("MARKER-Target").Id;

                                         for (var index = 0; index < removed.Length; index++)
                                         {
                                             removed[index] = running.Create("Removed " + index.ToString(System.Globalization.CultureInfo.InvariantCulture)).Id;
                                         }

                                         running.Select(CompanionProfile.Live.Id);
                                         running.Delete(removed[0], true);
                                         running.Merge(removed[1], target);
                                         running.Delete(removed[2], true);
                                         running.Merge(removed[3], CompanionProfile.Live.Id);
                                         running.ConfirmDeletion(removed[0]);
                                         running.ConfirmMerge(removed[3]);
                                     });

        // Assert
        var text = await File.ReadAllTextAsync(ProfilesPath, TestContext.CancellationToken);
        var expected = $$"""{"version":2,"active":"{{LiveId}}","profiles":[{"id":"{{LiveId}}","name":"Live"},{"id":"{{target:D}}","name":"MARKER-Target"}],"figures":[],"deleted":[{"id":"{{removed[0]:D}}","confirmed":true},{"id":"{{removed[1]:D}}","target":"{{target:D}}","confirmed":false},{"id":"{{removed[2]:D}}","confirmed":false},{"id":"{{removed[3]:D}}","target":"{{LiveId}}","confirmed":true}]}""";
        var reloaded = CompanionProfiles.Load(ProfilesPath, new RecordingLogger());

        Assert.AreEqual(expected, text, "The file should hold the removals in the order they were made");
        Assert.IsFalse(reloaded.IsFileInvalid, "The written file should be valid");
        Assert.AreSequenceEqual([(removed[1], (Guid?)target, false), (removed[2], null, false)], reloaded.WaitingRemovals.Select(removal => (removal.Id, removal.Target, removal.IsConfirmed)).ToArray(), "The waiting removals should be read in order");
        Assert.AreSequenceEqual([removed[1]], reloaded.WaitingMerges.ToArray(), "The waiting merge should be read");
        Assert.AreSequenceEqual([removed[2]], reloaded.WaitingDeletions.ToArray(), "The waiting deletion should be read");
        Assert.AreEqual(target, reloaded.ResolveProfile(removed[1]), "The waiting merge should resolve to its target");
        Assert.AreEqual(CompanionProfile.Live.Id, reloaded.ResolveProfile(removed[3]), "The confirmed merge should resolve to its target");
    }

    /// <summary>
    /// A version-2 file whose merge entry breaks a rule is not of its format and fails closed: <c>Live</c> alone, nothing
    /// waiting, one warning that quotes no value of the file. Identities are compared as parsed values, so upper case and
    /// white space do not hide a target equal to its own identity (spec AC5, N1).
    /// </summary>
    /// <param name="replacement">The deleted list that replaces the valid one</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("""[{"id":"{third}","confirmed":false},{"id":"{other}","target":"{second}","confirmed":false,"extra":0}]""", DisplayName = "Unknown property")]
    [DataRow("""[{"id":"{third}","confirmed":false},{"id":"{other}","target":"{second}","target":"{second}","confirmed":false}]""", DisplayName = "Target twice")]
    [DataRow("""[{"id":"{third}","confirmed":false},{"id":"{other}","target":"{secondN}","confirmed":false}]""", DisplayName = "Target not of the form D")]
    [DataRow($$"""[{"id":"{third}","confirmed":false},{"id":"{other}","target":"{{ZeroId}}","confirmed":false}]""", DisplayName = "Target all zero")]
    [DataRow("""[{"id":"{third}","confirmed":false},{"id":"{other}","target":"{other}","confirmed":false}]""", DisplayName = "Target equal to its identity")]
    [DataRow("""[{"id":"{third}","confirmed":false},{"id":"{other}","target":"{otherUpper}","confirmed":false}]""", DisplayName = "Target the upper-case form of its identity")]
    [DataRow("""[{"id":"{third}","confirmed":false},{"id":"{other}","target":" {other} ","confirmed":false}]""", DisplayName = "Target padded with white space")]
    [DataRow("""[{"id":"{third}","confirmed":false},{"id":"{other}","target":"{third}","confirmed":false}]""", DisplayName = "Target an earlier deletion's identity")]
    [DataRow("""[{"id":"{other}","target":"{second}","confirmed":false},{"id":"{third}","target":"{other}","confirmed":false}]""", DisplayName = "Target an earlier merge's source")]
    [DataRow("""[{"id":"{live}","target":"{second}","confirmed":false}]""", DisplayName = "Source Live")]
    [DataRow("""[{"id":"{second}","target":"{live}","confirmed":false}]""", DisplayName = "Source listed")]
    [DataRow("""[{"id":"{other}","confirmed":false},{"id":"{other}","target":"{second}","confirmed":false}]""", DisplayName = "Source an earlier deletion")]
    [DataRow("""[{"id":"{other}","target":"{second}","confirmed":false},{"id":"{other}","confirmed":false}]""", DisplayName = "Source repeated as a deletion")]
    [DataRow("""[{"id":"{other}","target":"{third}","confirmed":false},{"id":"{third}","target":"{other}","confirmed":false}]""", DisplayName = "Loop of two")]
    [DataRow("""[{"id":"{other}","target":"{third}","confirmed":false},{"id":"{third}","target":"{fourth}","confirmed":false},{"id":"{fourth}","target":"{other}","confirmed":false}]""", DisplayName = "Loop of three")]
    public void CompanionProfilesLoadRejectsMalformedMerge(string replacement)
    {
        // Arrange
        var valid = CompanionProfiles.Load(WriteFile(Expand(BaseFile)), new RecordingLogger());

        Assert.IsFalse(valid.IsFileInvalid, "The valid file should be read, or no rejection below proves anything");
        Assert.AreSequenceEqual([new Guid(OtherId)], valid.WaitingMerges.ToArray(), "The valid file should hold its waiting merge");

        var path = WriteFile(Expand(BaseFile.Replace(BaseDeleted, replacement, StringComparison.Ordinal)));

        // Act
        var profiles = CompanionProfiles.Load(path, _logger);

        // Assert
        var lines = _logger.Entries.Select(entry => (entry.Level, entry.Message)).ToList();
        string[] values = [SecondId, OtherId, ThirdId, FourthId, LiveId, "MARKER"];

        Assert.IsTrue(profiles.IsFileInvalid, "The file should be invalid");
        Assert.AreSequenceEqual([CompanionProfile.Live.Id], profiles.Profiles.Select(profile => profile.Id).ToArray(), "Live should be the one profile");
        Assert.IsEmpty(profiles.WaitingRemovals, "A rejected file should leave nothing waiting");
        Assert.IsEmpty(profiles.WaitingMerges, "A rejected file should leave no merge waiting");
        Assert.HasCount(1, lines, "One line should be logged");
        Assert.AreEqual(LogLevel.Warning, lines[0].Level, "The line should be a warning");
        Assert.StartsWith(InvalidLinePrefix, lines[0].Message, "The line should name the invalid profiles file");
        Assert.IsEmpty(values.Where(value => lines[0].Message.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList(), "The line should quote no value of the file");
    }

    /// <summary>
    /// The largest file the writer can write with merges, 1,000 profiles of 32 escaped surrogate pairs, 1,000 figures and
    /// 1,000 waiting merges, is 678,718 bytes (the deletions' 630,718 plus 48 bytes of <c>"target":"…",</c> for each
    /// entry), within the reader's bound, and reads back valid (spec AC5, N1, N2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task CompanionProfilesLargestFileWithMergesUnderBound()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var names = Enumerable.Range(1, CompanionProfiles.MaxProfiles - 1).Select(WorstCaseName).ToArray();
        var figures = Enumerable.Range(0, CompanionProfiles.MaxFigures).Select(index => "Figure " + index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Guid[] merged = [];

        // Act
        await RunUntilCancelledAsync(profiles,
                                     running =>
                                     {
                                         merged = MergeIntoLive(running, CompanionProfiles.MaxDeletions);

                                         foreach (var name in names)
                                         {
                                             running.Create(name);
                                         }

                                         foreach (var figure in figures)
                                         {
                                             running.ObserveSessionStart(figure);
                                             running.ObserveSessionEnd();
                                         }
                                     });

        // Assert
        var reloaded = CompanionProfiles.Load(ProfilesPath, new RecordingLogger());
        var length = new FileInfo(ProfilesPath).Length;

        Assert.AreEqual(630718L + (CompanionProfiles.MaxDeletions * TargetPropertyLength), length, "The largest file should have the computed length");
        Assert.IsLessThan((long)CompanionProfiles.MaxFileLength, length, "The largest file should stay under the reader's bound");
        Assert.IsFalse(reloaded.IsFileInvalid, "The largest file should read back valid");
        Assert.HasCount(CompanionProfiles.MaxProfiles, reloaded.Profiles, "Every profile should read back");
        Assert.AreSequenceEqual(merged, reloaded.WaitingMerges.ToArray(), "Every waiting merge should read back in order");
    }

    /// <summary>
    /// Replaces the placeholders of a test text.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The text with the identities filled in</returns>
    private static string Expand(string text)
    {
        return text.Replace("{otherUpper}", OtherId.ToUpperInvariant(), StringComparison.Ordinal)
                   .Replace("{secondN}", SecondId.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal)
                   .Replace("{live}", LiveId, StringComparison.Ordinal)
                   .Replace("{second}", SecondId, StringComparison.Ordinal)
                   .Replace("{other}", OtherId, StringComparison.Ordinal)
                   .Replace("{third}", ThirdId, StringComparison.Ordinal)
                   .Replace("{fourth}", FourthId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gives a distinct name of 32 surrogate pairs, 64 code units, each escaped as two <c>\uXXXX</c> by the writer.
    /// </summary>
    /// <param name="index">The number of the name, from 1 to 999</param>
    /// <returns>The name</returns>
    private static string WorstCaseName(int index)
    {
        return char.ConvertFromUtf32(0x10000 + index) + string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x1F600), 31));
    }

    /// <summary>
    /// Describes notices as their kind and profile name.
    /// </summary>
    /// <param name="notices">The notices</param>
    /// <returns>The descriptions of the notices that are not <see langword="null" /></returns>
    private static (ProfileNoticeKind Kind, string ProfileName)[] Describe(IEnumerable<ProfileNotice?> notices)
    {
        return [.. notices.OfType<ProfileNotice>().Select(notice => (notice.Kind, notice.ProfileName))];
    }

    /// <summary>
    /// Creates profiles one by one, selects <c>Live</c> after each and merges it into <c>Live</c>, so each merge waits.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="count">The number of merges</param>
    /// <returns>The merged identities, oldest first</returns>
    private static Guid[] MergeIntoLive(CompanionProfiles profiles, int count)
    {
        var merged = new Guid[count];

        for (var index = 0; index < count; index++)
        {
            var created = profiles.Create("Merged " + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

            profiles.Select(CompanionProfile.Live.Id);
            profiles.Merge(created.Id, CompanionProfile.Live.Id);
            merged[index] = created.Id;
        }

        return merged;
    }

    /// <summary>
    /// Writes a test text to the profiles file.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The path</returns>
    private string WriteFile(string text)
    {
        File.WriteAllText(ProfilesPath, text);

        return ProfilesPath;
    }

    /// <summary>
    /// Starts the writer, makes a change, cancels the writer and waits until it completed.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="change">The change</param>
    /// <returns>A task that completes when the writer completed</returns>
    private async Task RunUntilCancelledAsync(CompanionProfiles profiles, Action<CompanionProfiles> change)
    {
        using var stop = new CancellationTokenSource();

        var run = profiles.RunAsync(stop.Token);

        change(profiles);

        await stop.CancelAsync();

        try
        {
            await run.WaitAsync(_guard, TestContext.CancellationToken);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // The writer may end with the cancellation.
        }
    }

    #endregion // Methods
}