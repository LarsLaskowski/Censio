namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="PageAddress" />.
/// </summary>
[TestClass]
public sealed class PageAddressTests
{
    #region Constants

    /// <summary>
    /// A profile identity in the form the addresses carry.
    /// </summary>
    private const string ProfileText = "0f8fad5b-d9cb-469f-a165-70867728950e";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the texts that are no profile identity: none, empty, short, other GUID forms, markup, surrounding spaces and a
    /// 2,000-character text.
    /// </summary>
    private static IEnumerable<object?[]> MalformedProfiles
    {
        get
        {
            yield return [null];
            yield return [string.Empty];
            yield return ["x"];
            yield return ["0f8fad5bd9cb469fa16570867728950e"];
            yield return ["{" + ProfileText + "}"];
            yield return ["(" + ProfileText + ")"];
            yield return [" " + ProfileText];
            yield return [ProfileText + " "];
            yield return [ProfileText[..35]];
            yield return [ProfileText + "0"];
            yield return ["0f8fad5b-d9cb-469f-a165-70867728950g"];
            yield return ["<b>"];
            yield return ["%3Cb%3E"];
            yield return [new string('a', 2000)];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A profile identity in its 36-character form parses to that identity.
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void PageAddressTryParseProfileAcceptsIdentity()
    {
        // Act
        var parsed = PageAddress.TryParseProfile(ProfileText, out var profileId);

        // Assert
        Assert.IsTrue(parsed, "The identity should parse");
        Assert.AreEqual(Guid.Parse(ProfileText), profileId, "The parsed identity should be the one given");
    }

    /// <summary>
    /// Any text that is not an identity in its 36-character form is refused.
    /// </summary>
    /// <param name="text">The text</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DynamicData(nameof(MalformedProfiles))]
    public void PageAddressTryParseProfileRefusesMalformedText(string? text)
    {
        // Act
        var parsed = PageAddress.TryParseProfile(text, out _);

        // Assert
        Assert.IsFalse(parsed, "A text that is no identity in its 36-character form should be refused");
    }

    /// <summary>
    /// A session GUID of digits with an optional leading minus parses over the whole range of a 32-bit integer.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="expected">The expected session GUID</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("3245", 3245)]
    [DataRow("0", 0)]
    [DataRow("-5", -5)]
    [DataRow("2147483647", int.MaxValue)]
    [DataRow("-2147483648", int.MinValue)]
    public void PageAddressTryParseSessionAcceptsInteger(string text, int expected)
    {
        // Act
        var parsed = PageAddress.TryParseSession(text, out var sessionGuid);

        // Assert
        Assert.IsTrue(parsed, $"The session GUID {text} should parse");
        Assert.AreEqual(expected, sessionGuid, "The parsed session GUID should be the one given");
    }

    /// <summary>
    /// A session text with a plus sign, spaces, separators, an exponent, digits of another script, beyond the range or
    /// without digits is refused.
    /// </summary>
    /// <param name="text">The text</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("-")]
    [DataRow("+5")]
    [DataRow(" 5")]
    [DataRow("5 ")]
    [DataRow("1,000")]
    [DataRow("1.0")]
    [DataRow("1e3")]
    [DataRow("0x10")]
    [DataRow("٣")]
    [DataRow("2147483648")]
    [DataRow("-2147483649")]
    [DataRow("%3Cb%3E")]
    public void PageAddressTryParseSessionRefusesMalformedText(string? text)
    {
        // Act
        var parsed = PageAddress.TryParseSession(text, out _);

        // Assert
        Assert.IsFalse(parsed, $"The session text {text} should be refused");
    }

    /// <summary>
    /// An island ID or area index of digits from 0 to 255 parses.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="expected">The expected value</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow("0", (byte)0)]
    [DataRow("10", (byte)10)]
    [DataRow("255", (byte)255)]
    public void PageAddressTryParseByteAcceptsByte(string text, byte expected)
    {
        // Act
        var parsed = PageAddress.TryParseByte(text, out var value);

        // Assert
        Assert.IsTrue(parsed, $"The byte {text} should parse");
        Assert.AreEqual(expected, value, "The parsed byte should be the one given");
    }

    /// <summary>
    /// A byte text with a sign, a space, beyond 255 or without digits is refused: the minus belongs to the session GUID
    /// only.
    /// </summary>
    /// <param name="text">The text</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("-1")]
    [DataRow("-0")]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("256")]
    [DataRow("1.5")]
    [DataRow("x")]
    public void PageAddressTryParseByteRefusesMalformedText(string? text)
    {
        // Act
        var parsed = PageAddress.TryParseByte(text, out _);

        // Assert
        Assert.IsFalse(parsed, $"The byte text {text} should be refused");
    }

    /// <summary>
    /// On an address that carries a profile, the Regions entry links to that profile's overview, query and fragment
    /// ignored.
    /// </summary>
    /// <param name="relativePath">The base-relative address after the profile identity</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("/regions")]
    [DataRow("/sessions/3245")]
    [DataRow("/islands/3245/1/0")]
    [DataRow("/regions?x=1#top")]
    public void PageAddressRegionsEntryLinkCarriesProfileOfAddress(string relativePath)
    {
        // Act
        var link = PageAddress.RegionsEntryLink($"profiles/{ProfileText}{relativePath}");

        // Assert
        Assert.AreEqual($"/profiles/{ProfileText}/regions", link, $"The Regions entry on profiles/<profile>{relativePath} should carry the profile");
    }

    /// <summary>
    /// On an address that carries no well-formed profile, the Regions entry links to the overview of the newest profile.
    /// </summary>
    /// <param name="relativePath">The base-relative address</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("")]
    [DataRow("regions")]
    [DataRow("profiles/x/regions")]
    [DataRow("profiles/")]
    [DataRow(null)]
    public void PageAddressRegionsEntryLinkWithoutProfileIsRegions(string? relativePath)
    {
        // Act
        var link = PageAddress.RegionsEntryLink(relativePath);

        // Assert
        Assert.AreEqual("/regions", link, $"The Regions entry on '{relativePath}' should lead to /regions");
    }

    #endregion // Methods
}