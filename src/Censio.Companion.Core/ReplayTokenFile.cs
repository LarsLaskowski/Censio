using System.Security;
using System.Text;

namespace Censio.Companion.Core;

/// <summary>
/// Reads the token file of a replay that is sent to a server.
/// </summary>
public static class ReplayTokenFile
{
    #region Constants

    /// <summary>
    /// The largest size of a token file in bytes.
    /// </summary>
    public const int MaxFileBytes = 65536;

    /// <summary>
    /// The smallest length of a token in characters.
    /// </summary>
    private const int MinTokenLength = 32;

    /// <summary>
    /// The largest length of a token in characters.
    /// </summary>
    private const int MaxTokenLength = 1024;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the token from a file.
    /// </summary>
    /// <param name="path">The path of the token file</param>
    /// <returns>The result of reading the file</returns>
    public static ReplayTokenFileResult ReadFile(string path)
    {
        byte[] bytes;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            bytes = ReadAtMost(stream, MaxFileBytes + 1);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                                    or NotSupportedException or SecurityException)
        {
            return new ReplayTokenFileResult
                   {
                       Kind = ReplayTokenFileResultKind.CannotBeRead
                   };
        }

        var token = bytes.Length > MaxFileBytes ? null : Decode(bytes)?.Trim(' ', '\t', '\r', '\n');

        return token is not null && token.Length is >= MinTokenLength and <= MaxTokenLength && token.All(IsTokenCharacter)
                   ? new ReplayTokenFileResult
                     {
                         Kind = ReplayTokenFileResultKind.Read,
                         Token = token
                     }
                   : new ReplayTokenFileResult
                     {
                         Kind = ReplayTokenFileResultKind.NoValidToken
                     };
    }

    /// <summary>
    /// Reads up to a number of bytes from a stream.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="limit">The largest number of bytes</param>
    /// <returns>The bytes read</returns>
    private static byte[] ReadAtMost(Stream stream, int limit)
    {
        var buffer = new byte[limit];
        var total = 0;

        while (total < limit)
        {
            var read = stream.Read(buffer, total, limit - total);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return buffer[..total];
    }

    /// <summary>
    /// Decodes the bytes by their byte order mark, UTF-8 without one.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <returns>The text, or <c>null</c> if the bytes are not valid in their encoding</returns>
    private static string? Decode(byte[] bytes)
    {
        var offset = 0;
        Encoding encoding;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            offset = 3;
            encoding = new UTF8Encoding(false, true);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            offset = 2;
            encoding = new UnicodeEncoding(false, false, true);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            offset = 2;
            encoding = new UnicodeEncoding(true, false, true);
        }
        else
        {
            encoding = new UTF8Encoding(false, true);
        }

        try
        {
            return encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether a character is allowed in a token.
    /// </summary>
    /// <param name="character">The character</param>
    /// <returns><c>true</c> if the character is between <c>!</c> and <c>~</c></returns>
    private static bool IsTokenCharacter(char character)
    {
        return character is >= '!' and <= '~';
    }

    #endregion // Methods
}