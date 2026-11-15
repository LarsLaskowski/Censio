using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestHosts" />, on a fake clock.
/// </summary>
[TestClass]
public sealed class IngestHostsTests
{
    #region Constants

    /// <summary>
    /// The line logged when a host is blocked, as the level and the text.
    /// </summary>
    private const string BlockStartLine = "Warning: A host was refused 10 times within 60 seconds; its connections are refused for 60 seconds.";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The clock of the hosts.
    /// </summary>
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The logger of the hosts.
    /// </summary>
    private readonly ConcurrentRecordingLogger _logger = new();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// A host with 4 connections waiting for their hello gets no fifth place.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void IngestHostsTryEnterPendingHelloFifthOfOneHostReturnsNull()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");
        var entered = EnterPendingHellos(hosts, address, 4);

        // Act
        var fifth = hosts.TryEnterPendingHello(address);

        // Assert
        Assert.AreEqual(4, IngestHosts.MaxPendingHellosPerHost, "The bound should be 4 pending hellos per host");
        Assert.HasCount(4, entered, "Four pending hellos of the host should be counted");
        Assert.IsNull(fifth, "The fifth pending hello of the host should be refused");
    }

    /// <summary>
    /// While one host has 4 connections waiting, another host's connection gets a place.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void IngestHostsTryEnterPendingHelloOtherHostWhileFirstFullReturnsHost()
    {
        // Arrange
        using var hosts = CreateHosts();
        var entered = EnterPendingHellos(hosts, IPAddress.Parse("198.51.100.1"), 4);

        // Act
        var other = hosts.TryEnterPendingHello(IPAddress.Parse("198.51.100.2"));

        // Assert
        Assert.HasCount(4, entered, "The first host should hold four places");
        Assert.IsNotNull(other, "The other host's connection should get a place");
        Assert.DoesNotContain(other, entered, "The other host should have an entry of its own");
    }

    /// <summary>
    /// The 10th failed hello of a host within 60 seconds blocks the host.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void IngestHostsRecordFailedHelloTenthWithinWindowBlocksHost()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");
        var early = FailHellos(hosts, address, 5);

        _timeProvider.Advance(IngestHosts.FailureWindow - TimeSpan.FromSeconds(1));

        var late = FailHellos(hosts, address, 4);
        var tenth = EnterPendingHello(hosts, address);

        // Act
        hosts.RecordFailedHello(tenth);

        // Assert
        Assert.AreEqual(10, IngestHosts.MaxFailedHellos, "The bound should be 10 failed hellos");
        Assert.AreEqual(60, IngestHosts.FailureWindow.TotalSeconds, "The window should be 60 seconds");
        Assert.AreEqual(9, early + late, "Nine failed hellos should be recorded before the tenth");
        Assert.IsNull(hosts.TryEnterPendingHello(address), "The blocked host's next connection should be refused");
    }

    /// <summary>
    /// Ten failed hellos spread over more than 60 seconds do not block the host.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void IngestHostsRecordFailedHelloTenSpreadOverMoreThanWindowDoesNotBlock()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");
        var first = FailHellos(hosts, address, 1);

        _timeProvider.Advance(IngestHosts.FailureWindow);

        var later = FailHellos(hosts, address, 8);
        var tenth = EnterPendingHello(hosts, address);

        // Act
        hosts.RecordFailedHello(tenth);

        // Assert
        Assert.AreEqual(9, first + later, "Nine failed hellos should be recorded before the tenth");
        Assert.IsNotNull(hosts.TryEnterPendingHello(address), "A host whose first failed hello left the window should not be blocked");
        Assert.IsEmpty(_logger.Entries, "No block should be logged");
    }

    /// <summary>
    /// The 10th failed hello logs the start of the block once.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void IngestHostsRecordFailedHelloTenthLogsBlockStartOnce()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");
        var failed = FailHellos(hosts, address, 9);
        var linesBefore = Lines();
        var tenth = EnterPendingHello(hosts, address);

        // Act
        hosts.RecordFailedHello(tenth);

        // Assert
        Assert.AreEqual(9, failed, "Nine failed hellos should be recorded before the tenth");
        Assert.IsEmpty(linesBefore, "Nothing should be logged before the tenth failed hello");
        Assert.AreSequenceEqual(new[] { BlockStartLine }, Lines(), "The start of the block should be logged once, with the fixed text");
    }

    /// <summary>
    /// An upgrade refused during a block does not extend the block.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void IngestHostsTryEnterPendingHelloDuringBlockDoesNotExtendBlock()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");

        Block(hosts, address);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        var refused = RefuseUpgrades(hosts, address, 1);

        _timeProvider.Advance(IngestHosts.BlockDuration - TimeSpan.FromSeconds(30));

        // Act
        var host = hosts.TryEnterPendingHello(address);

        // Assert
        Assert.AreEqual(1, refused, "The upgrade during the block should be refused");
        Assert.IsNotNull(host, "The block should end 60 seconds after it started, whatever was refused during it");
    }

    /// <summary>
    /// After its block a host's connection gets a place again.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void IngestHostsTryEnterPendingHelloAfterBlockReturnsHost()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");

        Block(hosts, address);
        _timeProvider.Advance(IngestHosts.BlockDuration);

        // Act
        var host = hosts.TryEnterPendingHello(address);

        // Assert
        Assert.AreEqual(60, IngestHosts.BlockDuration.TotalSeconds, "The block should last 60 seconds");
        Assert.IsNotNull(host, "The host's connection should get a place after the block");
    }

    /// <summary>
    /// A hello that fails during a block is not counted toward the next block.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void IngestHostsRecordFailedHelloDuringBlockIsNotCounted()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");
        var waiting = EnterPendingHello(hosts, address);

        Block(hosts, address);
        _timeProvider.Advance(IngestHosts.BlockDuration - TimeSpan.FromSeconds(1));
        hosts.RecordFailedHello(waiting);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        var failedAfterBlock = FailHellos(hosts, address, 9);

        // Act
        var host = hosts.TryEnterPendingHello(address);

        // Assert
        Assert.AreEqual(9, failedAfterBlock, "Nine failed hellos should be recorded after the block");
        Assert.IsNotNull(host, "The hello that failed during the block should not count, so nine failures afterwards block nothing");
    }

    /// <summary>
    /// An accepted hello sets the host's count to zero and does not shorten a running block.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void IngestHostsRecordAcceptedHelloResetsCountKeepsBlock()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");
        var firstAccepted = EnterPendingHello(hosts, address);
        var secondAccepted = EnterPendingHello(hosts, address);
        var beforeReset = FailHellos(hosts, address, 9);

        hosts.RecordAcceptedHello(firstAccepted, address);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        var afterReset = FailHellos(hosts, address, 1);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        var blocking = FailHellos(hosts, address, 9);

        hosts.RecordAcceptedHello(secondAccepted, address);
        _timeProvider.Advance(TimeSpan.FromSeconds(55));

        // Act
        var host = hosts.TryEnterPendingHello(address);

        // Assert
        Assert.AreEqual(19, beforeReset + afterReset + blocking, "Every failed hello should get a place, as the accepted hello reset the count");
        Assert.IsNull(host, "The block started 20 seconds in by the reset count should still hold 75 seconds in, unshortened by the accepted hello");
    }

    /// <summary>
    /// A failed hello leaves the connection's waiting place.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void IngestHostsRecordFailedHelloLeavesWaitingPlace()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");
        var entered = EnterPendingHellos(hosts, address, 4);

        // Act
        hosts.RecordFailedHello(entered[0]);

        // Assert
        Assert.HasCount(4, entered, "The host should hold four places");
        Assert.IsNotNull(hosts.TryEnterPendingHello(address), "The place of the failed hello should be free again");
    }

    /// <summary>
    /// The end of a block is logged with the number of upgrades refused during it.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void IngestHostsBlockEndLogsRefusedUpgradeCount()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("198.51.100.1");

        Block(hosts, address);

        var refused = RefuseUpgrades(hosts, address, 3);

        // Act
        _timeProvider.Advance(IngestHosts.BlockDuration);

        // Assert
        Assert.AreEqual(3, refused, "Three upgrades should be refused during the block");
        Assert.AreSequenceEqual(new[] { BlockStartLine, "Information: A host's block ended; 3 connections were refused while it lasted." }, Lines(), "The end of the block should be logged with the refused upgrades");
    }

    /// <summary>
    /// The end of a block without refused upgrades is logged with zero.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void IngestHostsBlockEndWithoutRefusedUpgradesLogsZero()
    {
        // Arrange
        using var hosts = CreateHosts();

        Block(hosts, IPAddress.Parse("198.51.100.1"));

        // Act
        _timeProvider.Advance(IngestHosts.BlockDuration);

        // Assert
        Assert.AreSequenceEqual(new[] { BlockStartLine, "Information: A host's block ended; 0 connections were refused while it lasted." }, Lines(), "The end of the block should be logged with zero refused upgrades");
    }

    /// <summary>
    /// The block lines are the fixed texts, which name no address.
    /// </summary>
    /// <param name="address">The address of the blocked host</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("203.0.113.77")]
    [DataRow("2001:db8::77")]
    [DataRow("::ffff:203.0.113.77")]
    public void IngestHostsBlockLinesNameNoAddress(string address)
    {
        // Arrange
        using var hosts = CreateHosts();
        var blocked = IPAddress.Parse(address);

        Block(hosts, blocked);
        RefuseUpgrades(hosts, blocked, 2);

        // Act
        _timeProvider.Advance(IngestHosts.BlockDuration);

        // Assert
        Assert.AreSequenceEqual(new[] { BlockStartLine, "Information: A host's block ended; 2 connections were refused while it lasted." }, Lines(), "The block lines should be exactly the fixed texts");
    }

    /// <summary>
    /// Two IPv4 addresses are separate hosts.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void IngestHostsTryEnterPendingHelloTwoIpv4AddressesAreSeparateHosts()
    {
        // Arrange
        using var hosts = CreateHosts();
        var entered = EnterPendingHellos(hosts, IPAddress.Parse("192.0.2.1"), 4);

        // Act
        var other = hosts.TryEnterPendingHello(IPAddress.Parse("192.0.2.2"));

        // Assert
        Assert.HasCount(4, entered, "The first address should hold four places");
        Assert.IsNotNull(other, "The second IPv4 address should be a host of its own");
    }

    /// <summary>
    /// Two IPv6 addresses of one /64 are separate hosts.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void IngestHostsTryEnterPendingHelloTwoIpv6AddressesOfOnePrefixAreSeparateHosts()
    {
        // Arrange
        using var hosts = CreateHosts();
        var entered = EnterPendingHellos(hosts, IPAddress.Parse("2001:db8:1:2::1"), 4);

        // Act
        var other = hosts.TryEnterPendingHello(IPAddress.Parse("2001:db8:1:2::2"));

        // Assert
        Assert.HasCount(4, entered, "The first address should hold four places");
        Assert.IsNotNull(other, "The second IPv6 address of the same /64 should be a host of its own");
    }

    /// <summary>
    /// Connections without an address share one host, apart from the unspecified addresses.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void IngestHostsTryEnterPendingHelloWithoutAddressIsOneHost()
    {
        // Arrange
        using var hosts = CreateHosts();
        var entered = EnterPendingHellos(hosts, null, 4);

        // Act
        var fifth = hosts.TryEnterPendingHello(null);

        // Assert
        Assert.HasCount(4, entered, "Four connections without an address should be counted");
        Assert.IsNull(fifth, "The fifth connection without an address should be refused as the same host");
        Assert.IsNotNull(hosts.TryEnterPendingHello(IPAddress.Any), "The IPv4 unspecified address should be a host apart from no address");
        Assert.IsNotNull(hosts.TryEnterPendingHello(IPAddress.IPv6Any), "The IPv6 unspecified address should be a host apart from no address");
    }

    /// <summary>
    /// An IPv4-mapped IPv6 address and its IPv4 address are one host.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void IngestHostsTryEnterPendingHelloIpv4MappedAddressIsIpv4Host()
    {
        // Arrange
        using var hosts = CreateHosts();
        var entered = EnterPendingHellos(hosts, IPAddress.Parse("192.0.2.1"), 4);

        // Act
        var mapped = hosts.TryEnterPendingHello(IPAddress.Parse("::ffff:192.0.2.1"));

        // Assert
        Assert.HasCount(4, entered, "The IPv4 address should hold four places");
        Assert.IsNull(mapped, "The IPv4-mapped address should be refused as the same host");
    }

    /// <summary>
    /// With 1,024 hosts tracked, further hosts share one entry.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void IngestHostsTryEnterPendingHelloBeyondTrackedHostsSharesOneEntry()
    {
        // Arrange
        using var hosts = CreateHosts();
        var tracked = FillTrackedHosts(hosts, IngestHosts.MaxTrackedHosts);
        var first = hosts.TryEnterPendingHello(IPAddress.Parse("198.51.100.1"));

        // Act
        var second = hosts.TryEnterPendingHello(IPAddress.Parse("198.51.100.2"));

        // Assert
        Assert.AreEqual(1024, IngestHosts.MaxTrackedHosts, "The bound should be 1,024 tracked hosts");
        Assert.HasCount(1024, tracked, "Every tracked host should get a place");
        Assert.IsNotNull(first, "The first untracked host should get a place on the shared entry");
        Assert.AreNotSame(tracked[^1], first, "The first untracked host should not share the last tracked host's entry");
        Assert.AreSame(first, second, "Two untracked hosts should share one entry");
    }

    /// <summary>
    /// The host of the last accepted hello keeps an entry of its own while the table is full.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void IngestHostsTryEnterPendingHelloLastAcceptedHostKeepsOwnEntry()
    {
        // Arrange
        using var hosts = CreateHosts();
        var companion = IPAddress.Parse("192.0.2.10");

        hosts.RecordAcceptedHello(EnterPendingHello(hosts, companion), companion);

        var tracked = FillTrackedHosts(hosts, IngestHosts.MaxTrackedHosts);
        var shared = EnterPendingHellos(hosts, IPAddress.Parse("198.51.100.1"), 4);

        // Act
        var own = hosts.TryEnterPendingHello(companion);

        // Assert
        Assert.HasCount(1024, tracked, "Every tracked host should get a place");
        Assert.HasCount(4, shared, "The shared entry should hold four places");
        Assert.IsNotNull(own, "The host of the last accepted hello should get a place on its own entry");
        Assert.DoesNotContain(own, shared, "The host of the last accepted hello should not use the shared entry");
    }

    /// <summary>
    /// A host is forgotten 60 seconds after its last failed hello, which frees its entry for another host.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void IngestHostsTryEnterPendingHelloForgottenHostFreesEntry()
    {
        // Arrange
        using var hosts = CreateHosts();
        var failed = FailTrackedHosts(hosts, IngestHosts.MaxTrackedHosts);
        var shared = EnterPendingHellos(hosts, IPAddress.Parse("198.51.100.1"), 4);

        _timeProvider.Advance(IngestHosts.FailureWindow);

        // Act
        var host = hosts.TryEnterPendingHello(IPAddress.Parse("198.51.100.2"));

        // Assert
        Assert.AreEqual(1024, failed, "Every tracked host should record a failed hello");
        Assert.HasCount(4, shared, "The shared entry should hold four places");
        Assert.IsNotNull(host, "A new host should get the entry of a forgotten host");
    }

    /// <summary>
    /// A host with a connection waiting is not forgotten.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void IngestHostsTryEnterPendingHelloWaitingHostIsNotForgotten()
    {
        // Arrange
        using var hosts = CreateHosts();
        var address = IPAddress.Parse("192.0.2.10");
        var ownFailed = FailHellos(hosts, address, 1);
        var waiting = EnterPendingHello(hosts, address);
        var othersFailed = FailTrackedHosts(hosts, IngestHosts.MaxTrackedHosts - 1);

        _timeProvider.Advance(IngestHosts.FailureWindow);

        var newcomer = hosts.TryEnterPendingHello(IPAddress.Parse("198.51.100.1"));
        var further = EnterPendingHellos(hosts, address, 3);

        // Act
        var fifth = hosts.TryEnterPendingHello(address);

        // Assert
        Assert.AreEqual(1024, ownFailed + othersFailed, "Every tracked host should record a failed hello");
        Assert.IsNotNull(newcomer, "A new host should get the entry of a forgotten host");
        Assert.HasCount(3, further, "Three further connections of the waiting host should be counted");
        Assert.AreNotSame(waiting, newcomer, "The new host should not take the waiting host's entry");
        Assert.IsNull(fifth, "The waiting host's entry should still count its first connection");
    }

    /// <summary>
    /// An accepted hello on the shared entry gives its address an entry of its own and leaves the shared entry's failed
    /// hellos alone.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void IngestHostsRecordAcceptedHelloOnSharedEntryProtectsOwnAddress()
    {
        // Arrange
        using var hosts = CreateHosts();
        var companion = IPAddress.Parse("192.0.2.10");
        var untracked = IPAddress.Parse("198.51.100.1");
        var tracked = FillTrackedHosts(hosts, IngestHosts.MaxTrackedHosts);
        var sharedFailed = FailHellos(hosts, untracked, 9);
        var shared = EnterPendingHello(hosts, companion);

        hosts.RecordAcceptedHello(shared, companion);

        var tenthFailed = FailHellos(hosts, untracked, 1);

        // Act
        var own = hosts.TryEnterPendingHello(companion);

        // Assert
        Assert.HasCount(1024, tracked, "Every tracked host should get a place");
        Assert.AreEqual(10, sharedFailed + tenthFailed, "Ten failed hellos should be recorded on the shared entry");
        Assert.IsNotNull(own, "The accepted address should have an entry of its own, not the blocked shared one");
        Assert.AreNotSame(shared, own, "The accepted address should not use the shared entry");
        Assert.IsNull(hosts.TryEnterPendingHello(IPAddress.Parse("198.51.100.2")), "The shared entry should be blocked, its failed hellos untouched by the accepted hello");
    }

    /// <summary>
    /// Enters one pending hello that must get a place.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="address">The address</param>
    /// <returns>The host's entry</returns>
    private static IngestHost EnterPendingHello(IngestHosts hosts, IPAddress? address)
    {
        var host = hosts.TryEnterPendingHello(address);

        Assert.IsNotNull(host, "The pending hello of the arrangement should get a place");

        return host;
    }

    /// <summary>
    /// Enters pending hellos of one address until one is refused or the count is reached.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="address">The address</param>
    /// <param name="count">The number of pending hellos to enter</param>
    /// <returns>The entries the pending hellos were counted on</returns>
    private static List<IngestHost> EnterPendingHellos(IngestHosts hosts, IPAddress? address, int count)
    {
        var entered = new List<IngestHost>();

        while (entered.Count < count && hosts.TryEnterPendingHello(address) is { } host)
        {
            entered.Add(host);
        }

        return entered;
    }

    /// <summary>
    /// Records failed hellos of one address, each on a connection that got a place.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="address">The address</param>
    /// <param name="count">The number of failed hellos</param>
    /// <returns>The number of failed hellos recorded; a connection refused a place records none</returns>
    private static int FailHellos(IngestHosts hosts, IPAddress? address, int count)
    {
        var failed = 0;

        for (var attempt = 0; attempt < count; attempt++)
        {
            var host = hosts.TryEnterPendingHello(address);

            if (host is not null)
            {
                hosts.RecordFailedHello(host);
                failed++;
            }
        }

        return failed;
    }

    /// <summary>
    /// Blocks a host by <see cref="IngestHosts.MaxFailedHellos" /> failed hellos.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="address">The address</param>
    private static void Block(IngestHosts hosts, IPAddress? address)
    {
        var failed = FailHellos(hosts, address, IngestHosts.MaxFailedHellos);

        Assert.AreEqual(IngestHosts.MaxFailedHellos, failed, "Every failed hello of the arrangement should get a place");
    }

    /// <summary>
    /// Tries upgrades of one address and counts the refused ones.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="address">The address</param>
    /// <param name="count">The number of upgrades</param>
    /// <returns>The number of upgrades refused</returns>
    private static int RefuseUpgrades(IngestHosts hosts, IPAddress? address, int count)
    {
        return Enumerable.Range(0, count).Count(_ => hosts.TryEnterPendingHello(address) is null);
    }

    /// <summary>
    /// Gets the address of the tracked host with an index.
    /// </summary>
    /// <param name="index">The index, below 65,536</param>
    /// <returns>The address</returns>
    private static IPAddress TrackedAddress(int index)
    {
        return new IPAddress([10, 1, (byte)(index >> 8), (byte)(index & 0xFF)]);
    }

    /// <summary>
    /// Enters one waiting connection for each of a number of distinct hosts.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="count">The number of hosts</param>
    /// <returns>The entries of the hosts that got a place</returns>
    private static List<IngestHost> FillTrackedHosts(IngestHosts hosts, int count)
    {
        return Enumerable.Range(0, count)
                         .Select(index => hosts.TryEnterPendingHello(TrackedAddress(index)))
                         .OfType<IngestHost>()
                         .ToList();
    }

    /// <summary>
    /// Records one failed hello for each of a number of distinct hosts, so that none waits.
    /// </summary>
    /// <param name="hosts">The hosts</param>
    /// <param name="count">The number of hosts</param>
    /// <returns>The number of failed hellos recorded</returns>
    private static int FailTrackedHosts(IngestHosts hosts, int count)
    {
        return Enumerable.Range(0, count).Sum(index => FailHellos(hosts, TrackedAddress(index), 1));
    }

    /// <summary>
    /// Creates the hosts on the test's clock and logger.
    /// </summary>
    /// <returns>The hosts</returns>
    private IngestHosts CreateHosts()
    {
        return new IngestHosts(_timeProvider, _logger);
    }

    /// <summary>
    /// Gets the lines logged so far, each as the level and the text.
    /// </summary>
    /// <returns>The lines</returns>
    private List<string> Lines()
    {
        return _logger.Entries.Select(entry => $"{entry.Level}: {entry.Message}").ToList();
    }

    #endregion // Methods
}