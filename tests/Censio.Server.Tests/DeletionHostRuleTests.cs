namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="DeletionHostRule" />.
/// </summary>
[TestClass]
public sealed class DeletionHostRuleTests
{
    #region Methods

    /// <summary>
    /// A host is trusted when it is an IPv4 or IPv6 address (brackets kept) or <c>localhost</c> in any case, whatever
    /// <c>AllowedHosts</c> holds, or when <c>AllowedHosts</c>, neither empty nor <c>*</c>, lists it by name or by a
    /// <c>*.</c> suffix, ignoring case; any other form of a name, an empty and a missing host are not trusted, and a
    /// <c>*</c> among listed names trusts no other name.
    /// </summary>
    /// <param name="host">The host of the request</param>
    /// <param name="allowedHosts">The <c>AllowedHosts</c> setting</param>
    /// <param name="expected">Whether the host should be trusted</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("127.0.0.1", "*", true)]
    [DataRow("192.168.1.10", "*", true)]
    [DataRow("[::1]", "*", true)]
    [DataRow("[fe80::1]", "*", true)]
    [DataRow("localhost", "*", true)]
    [DataRow("LOCALHOST", "*", true)]
    [DataRow("127.0.0.1", null, true)]
    [DataRow("[::1]", "", true)]
    [DataRow("localhost", null, true)]
    [DataRow("192.168.1.10", "nas", true)]
    [DataRow("evil.example", "*", false)]
    [DataRow("localhost.", "*", false)]
    [DataRow("foo.localhost", "*", false)]
    [DataRow("192.168.1.10.nip.io", "*", false)]
    [DataRow("nas", "*", false)]
    [DataRow("", "*", false)]
    [DataRow(null, "*", false)]
    [DataRow("evil.example", null, false)]
    [DataRow("localhost.", null, false)]
    [DataRow("foo.localhost", null, false)]
    [DataRow("192.168.1.10.nip.io", null, false)]
    [DataRow("nas", null, false)]
    [DataRow("", null, false)]
    [DataRow(null, null, false)]
    [DataRow("evil.example", "", false)]
    [DataRow("localhost.", "", false)]
    [DataRow("foo.localhost", "", false)]
    [DataRow("192.168.1.10.nip.io", "", false)]
    [DataRow("nas", "", false)]
    [DataRow("", "", false)]
    [DataRow(null, "", false)]
    [DataRow("nas", "nas;127.0.0.1", true)]
    [DataRow("NAS", "nas;127.0.0.1", true)]
    [DataRow("censio.example.com", "*.example.com", true)]
    [DataRow("example.com", "*.example.com", false)]
    [DataRow("evil.example", "nas;127.0.0.1", false)]
    [DataRow("evil.example", "nas;*", false)]
    [DataRow("", "nas;*", false)]
    [DataRow(null, "nas;*", false)]
    public void DeletionHostRuleIsTrustedDecidesByHostForm(string? host, string? allowedHosts, bool expected)
    {
        // Act
        var trusted = DeletionHostRule.IsTrusted(host, allowedHosts);

        // Assert
        Assert.AreEqual(expected, trusted, $"The host '{host ?? "<null>"}' with AllowedHosts '{allowedHosts ?? "<unset>"}' should be decided by its form");
    }

    #endregion // Methods
}