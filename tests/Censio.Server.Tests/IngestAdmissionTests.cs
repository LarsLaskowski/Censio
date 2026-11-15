using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestAdmission" />.
/// </summary>
[TestClass]
public sealed class IngestAdmissionTests
{
    #region Methods

    /// <summary>
    /// An accepted hello reported after the connection's failed hello is ignored: the host's waiting place is left once.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void IngestAdmissionRecordAcceptedHelloAfterFailedHelloLeavesPlaceOnce()
    {
        // Arrange
        using var hosts = new IngestHosts(new FakeTimeProvider(), NullLogger.Instance);
        var address = IPAddress.Parse("198.51.100.1");
        var entered = Enumerable.Range(0, IngestHosts.MaxPendingHellosPerHost)
                                .Select(_ => hosts.TryEnterPendingHello(address))
                                .OfType<IngestHost>()
                                .ToList();
        var admission = new IngestAdmission(new IngestConnections(), hosts, entered[0], address);

        admission.RecordFailedHello();

        // Act
        admission.RecordAcceptedHello();

        // Assert
        Assert.HasCount(IngestHosts.MaxPendingHellosPerHost, entered, "The host should hold all its places");
        Assert.IsNotNull(hosts.TryEnterPendingHello(address), "The failed hello should free one place");
        Assert.IsNull(hosts.TryEnterPendingHello(address), "The ignored accepted hello should not free a second place");
    }

    #endregion // Methods
}