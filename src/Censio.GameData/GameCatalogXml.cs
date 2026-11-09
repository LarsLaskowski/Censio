using System.Globalization;
using System.Xml;

namespace Censio.GameData;

/// <summary>
/// Reads the XML of the catalog files forward-only and within bounds.
/// </summary>
internal static class GameCatalogXml
{
    #region Fields

    /// <summary>
    /// The settings of every reader: no document type, no resolver, no comments
    /// </summary>
    private static readonly XmlReaderSettings _settings = new()
                                                          {
                                                              DtdProcessing = DtdProcessing.Prohibit,
                                                              XmlResolver = null,
                                                              IgnoreComments = true,
                                                              IgnoreProcessingInstructions = true,
                                                              IgnoreWhitespace = true,
                                                              CloseInput = false
                                                          };

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Creates a reader and moves it to the root element.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="rootName">The expected name of the root element</param>
    /// <returns>The reader, at the root element</returns>
    internal static XmlReader Create(Stream stream, string rootName)
    {
        var reader = XmlReader.Create(stream, _settings);

        try
        {
            if (reader.MoveToContent() != XmlNodeType.Element || string.Equals(reader.LocalName, rootName, StringComparison.Ordinal) == false)
            {
                throw Malformed();
            }
        }
        catch
        {
            reader.Dispose();

            throw;
        }

        return reader;
    }

    /// <summary>
    /// Creates the exception of a file with the wrong structure.
    /// </summary>
    /// <returns>The exception, of the type the reader boundary maps to the malformed-file kind</returns>
    internal static XmlException Malformed()
    {
        return new XmlException();
    }

    /// <summary>
    /// Counts an element of a bounded part and checks the bounds.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <param name="baseDepth">The depth of the part's first element</param>
    /// <param name="count">The elements counted so far</param>
    internal static void CountElement(XmlReader reader, int baseDepth, ref int count)
    {
        count++;

        if (count > GameCatalogReader.MaxAssetElements || reader.Depth - baseDepth > GameCatalogReader.MaxAssetDepth)
        {
            throw Malformed();
        }
    }

    /// <summary>
    /// Records an element's name at its depth.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <param name="names">The names by depth</param>
    internal static void Enter(XmlReader reader, string[] names)
    {
        if (reader.Depth >= names.Length)
        {
            throw Malformed();
        }

        names[reader.Depth] = reader.LocalName;
    }

    /// <summary>
    /// Checks whether the element the reader is at has a parent of a name.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <param name="names">The names by depth</param>
    /// <param name="parent">The name of the parent</param>
    /// <returns><c>true</c> when the parent has the name</returns>
    internal static bool HasParent(XmlReader reader, string[] names, string parent)
    {
        return reader.Depth > 0 && string.Equals(names[reader.Depth - 1], parent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses a GUID.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The GUID</returns>
    internal static int ParseInt(string text)
    {
        return int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : throw Malformed();
    }

    /// <summary>
    /// Parses a text identifier.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The text identifier</returns>
    internal static long ParseLong(string text)
    {
        return long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : throw Malformed();
    }

    #endregion // Methods
}