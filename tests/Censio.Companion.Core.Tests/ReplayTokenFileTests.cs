using System.Text;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="ReplayTokenFile" />.
/// </summary>
[TestClass]
public sealed class ReplayTokenFileTests
{
    #region Constants

    /// <summary>
    /// The name of the token file in the temporary folder.
    /// </summary>
    private const string FileName = "token.txt";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-token-file-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Methods

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
    /// A token file that is missing or is a folder cannot be read.
    /// </summary>
    /// <param name="name">The name below the temporary folder; empty for the folder itself</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("missing.txt")]
    [DataRow("")]
    public void ReplayTokenFileUnreadableGivesCannotBeRead(string name)
    {
        // Arrange
        Directory.CreateDirectory(_root);

        var path = Path.Combine(_root, name);

        // Act
        var result = ReplayTokenFile.ReadFile(path);

        // Assert
        Assert.AreEqual(ReplayTokenFileResultKind.CannotBeRead, result.Kind, $"The path '{name}' should not be readable as a token file");
        Assert.IsNull(result.Token, "A file that cannot be read should give no token");
    }

    /// <summary>
    /// A token file whose content is no valid token by the server's rules, also after decoding, holds no valid token.
    /// </summary>
    /// <param name="content">The kind of content, see <see cref="InvalidContent" /></param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("empty")]
    [DataRow("white space only")]
    [DataRow("31 characters")]
    [DataRow("1025 characters")]
    [DataRow("space inside")]
    [DataRow("tab inside")]
    [DataRow("delete character")]
    [DataRow("character above tilde")]
    [DataRow("65537 bytes")]
    [DataRow("invalid UTF-8")]
    [DataRow("UTF-16 without mark")]
    [DataRow("UTF-16 with mark and odd length")]
    public void ReplayTokenFileInvalidContentGivesNoValidToken(string content)
    {
        // Arrange
        var path = WriteTokenFile(InvalidContent(content));

        // Act
        var result = ReplayTokenFile.ReadFile(path);

        // Assert
        Assert.AreEqual(ReplayTokenFileResultKind.NoValidToken, result.Kind, $"A file of content '{content}' should hold no valid token");
        Assert.IsNull(result.Token, "A file without a valid token should give no token");
    }

    /// <summary>
    /// Space, tab, carriage return and line feed around the token are trimmed, from 32 to 1,024 characters and up to
    /// a file of 65,536 bytes.
    /// </summary>
    /// <param name="length">The length of the token</param>
    /// <param name="leading">The text before the token</param>
    /// <param name="trailing">The text after the token, repeated</param>
    /// <param name="repeat">How often the text after the token is repeated</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(32, "", "", 0)]
    [DataRow(1024, "", "", 0)]
    [DataRow(64, "", "\r\n", 1)]
    [DataRow(64, "", "\n", 1)]
    [DataRow(64, " \t\r\n", " \t\r\n", 3)]
    [DataRow(64, "\t", " ", 1)]
    [DataRow(32, "", " ", 65504)]
    public void ReplayTokenFileSurroundingWhiteSpaceIsTrimmed(int length, string leading, string trailing, int repeat)
    {
        // Arrange
        var token = Token(length);
        var text = $"{leading}{token}{string.Concat(Enumerable.Repeat(trailing, repeat))}";
        var path = WriteTokenFile(Encoding.ASCII.GetBytes(text));

        // Act
        var result = ReplayTokenFile.ReadFile(path);

        // Assert
        Assert.AreEqual(ReplayTokenFileResultKind.Read, result.Kind, $"A token of {length} characters in a file of {text.Length} bytes should be read");
        Assert.AreEqual(token, result.Token, "The token should be read without the surrounding white space");
    }

    /// <summary>
    /// A token in UTF-8 with a byte order mark and in UTF-16 little and big endian with theirs, as Notepad and Windows
    /// PowerShell write it, is read as the same token.
    /// </summary>
    /// <param name="encodingName">The name of the encoding</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("utf-8")]
    [DataRow("utf-16")]
    [DataRow("utf-16BE")]
    public void ReplayTokenFileByteOrderMarksGiveSameToken(string encodingName)
    {
        // Arrange
        var encoding = Encoding.GetEncoding(encodingName);
        var token = Token(64);
        var path = WriteTokenFile([.. encoding.GetPreamble(), .. encoding.GetBytes(token + "\r\n")]);

        // Act
        var result = ReplayTokenFile.ReadFile(path);

        // Assert
        Assert.AreEqual(ReplayTokenFileResultKind.Read, result.Kind, $"A token in {encodingName} with its byte order mark should be read");
        Assert.AreEqual(token, result.Token, $"The token in {encodingName} should be the same token");
    }

    /// <summary>
    /// Builds a token of visible ASCII characters, from '!' to '~' and again.
    /// </summary>
    /// <param name="length">The length</param>
    /// <returns>The token</returns>
    private static string Token(int length)
    {
        return string.Concat(Enumerable.Range(0, length).Select(index => (char)('!' + (index % 94))));
    }

    /// <summary>
    /// Builds the bytes of a token file that holds no valid token.
    /// </summary>
    /// <param name="content">The kind of content</param>
    /// <returns>The bytes</returns>
    private static byte[] InvalidContent(string content)
    {
        return content switch
               {
                   "empty" => [],
                   "white space only" => Encoding.ASCII.GetBytes(" \t\r\n \r\n"),
                   "31 characters" => Encoding.ASCII.GetBytes(Token(31)),
                   "1025 characters" => Encoding.ASCII.GetBytes(Token(1025)),
                   "space inside" => Encoding.ASCII.GetBytes($"{Token(32)} {Token(32)}"),
                   "tab inside" => Encoding.ASCII.GetBytes($"{Token(32)}\t{Token(32)}"),
                   "delete character" => Encoding.ASCII.GetBytes($"{Token(32)}{(char)0x7F}"),
                   "character above tilde" => Encoding.UTF8.GetBytes($"{Token(32)}{(char)0xE9}"),
                   "65537 bytes" => Encoding.ASCII.GetBytes($"{Token(32)}{new string(' ', ReplayTokenFile.MaxFileBytes + 1 - 32)}"),
                   "invalid UTF-8" => [.. Encoding.ASCII.GetBytes(Token(32)), 0xC3, 0x28, .. Encoding.ASCII.GetBytes(Token(32))],
                   "UTF-16 without mark" => Encoding.Unicode.GetBytes(Token(64)),
                   "UTF-16 with mark and odd length" => [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Token(64)), 0x41],
                   _ => throw new ArgumentOutOfRangeException(nameof(content), content, "Unknown content")
               };
    }

    /// <summary>
    /// Writes the token file into the temporary folder.
    /// </summary>
    /// <param name="bytes">The content</param>
    /// <returns>The path of the file</returns>
    private string WriteTokenFile(byte[] bytes)
    {
        Directory.CreateDirectory(_root);

        var path = Path.Combine(_root, FileName);

        File.WriteAllBytes(path, bytes);

        return path;
    }

    #endregion // Methods
}