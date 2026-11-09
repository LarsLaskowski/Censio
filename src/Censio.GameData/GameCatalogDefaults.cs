using System.Xml;

namespace Censio.GameData;

/// <summary>
/// Reads the template and property default files, each in one forward-only pass.
/// </summary>
internal static class GameCatalogDefaults
{
    #region Methods

    /// <summary>
    /// Reads the default region type of the <c>Region</c> property.
    /// </summary>
    /// <param name="stream">The property defaults file</param>
    /// <returns>The default <c>RegionID</c>, or <c>null</c> when none is stated</returns>
    internal static string? ReadDefaultRegionId(Stream stream)
    {
        using var reader = GameCatalogXml.Create(stream, "Properties");

        var names = new string[GameCatalogReader.MaxAssetDepth + 1];
        var regionSections = 0;
        string? regionId = null;
        var more = reader.Read();

        while (more)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                GameCatalogXml.Enter(reader, names);

                if ((string.Equals(reader.LocalName, "DefaultValues", StringComparison.Ordinal) || string.Equals(reader.LocalName, "DefaultContainerValues", StringComparison.Ordinal))
                    && GameCatalogXml.HasParent(reader, names, "Group"))
                {
                    var isDefault = string.Equals(reader.LocalName, "DefaultValues", StringComparison.Ordinal);

                    ReadValues(reader, isDefault, ref regionSections, ref regionId);
                    more = reader.EOF == false;

                    continue;
                }
            }

            more = reader.Read();
        }

        return regionId;
    }

    /// <summary>
    /// Reads the region type each template with a <c>Region</c> property states.
    /// </summary>
    /// <param name="stream">The templates file</param>
    /// <returns>The stated <c>RegionID</c>, or <c>null</c>, by template name</returns>
    internal static Dictionary<string, string?> ReadTemplateRegionIds(Stream stream)
    {
        using var reader = GameCatalogXml.Create(stream, "Templates");

        var names = new string[GameCatalogReader.MaxAssetDepth + 1];
        var path = new string[4];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var regionIds = new Dictionary<string, string?>(StringComparer.Ordinal);
        var more = reader.Read();

        while (more)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                GameCatalogXml.Enter(reader, names);

                if (string.Equals(reader.LocalName, "Template", StringComparison.Ordinal) && GameCatalogXml.HasParent(reader, names, "Group"))
                {
                    var (name, hasRegion, regionId) = ReadTemplate(reader, path);

                    if (name is not null)
                    {
                        if (seen.Add(name) == false)
                        {
                            throw GameCatalogXml.Malformed();
                        }

                        if (hasRegion)
                        {
                            regionIds.Add(name, regionId);
                        }
                    }

                    more = reader.EOF == false;

                    continue;
                }
            }

            more = reader.Read();
        }

        return regionIds;
    }

    /// <summary>
    /// Reads the parts of a <c>DefaultValues</c> or <c>DefaultContainerValues</c> element.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <param name="isDefault">Whether the element is a <c>DefaultValues</c> element</param>
    /// <param name="regionSections">The number of <c>Region</c> defaults met so far</param>
    /// <param name="regionId">The default region type</param>
    private static void ReadValues(XmlReader reader, bool isDefault, ref int regionSections, ref string? regionId)
    {
        var baseDepth = reader.Depth;

        if (reader.IsEmptyElement)
        {
            reader.Read();

            return;
        }

        reader.Read();

        while (reader.Depth > baseDepth)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();

                continue;
            }

            if (reader.Depth - baseDepth == 1 && isDefault && string.Equals(reader.LocalName, "Region", StringComparison.Ordinal))
            {
                regionSections++;

                if (regionSections > 1)
                {
                    throw GameCatalogXml.Malformed();
                }

                regionId = ReadRegionDefault(reader);
            }
            else
            {
                SkipPart(reader);
            }
        }

        reader.Read();
    }

    /// <summary>
    /// Reads the <c>Region</c> default.
    /// </summary>
    /// <param name="reader">The reader, at the <c>Region</c> element</param>
    /// <returns>The <c>RegionID</c> it states, or <c>null</c></returns>
    private static string? ReadRegionDefault(XmlReader reader)
    {
        var baseDepth = reader.Depth;
        var count = 1;
        string? regionId = null;

        if (reader.IsEmptyElement)
        {
            reader.Read();

            return null;
        }

        reader.Read();

        while (reader.Depth > baseDepth)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();

                continue;
            }

            GameCatalogXml.CountElement(reader, baseDepth, ref count);

            if (reader.Depth - baseDepth == 1 && string.Equals(reader.LocalName, "RegionID", StringComparison.Ordinal))
            {
                regionId = reader.ReadElementContentAsString();
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();

        return regionId;
    }

    /// <summary>
    /// Skips a part within the bounds of a part.
    /// </summary>
    /// <param name="reader">The reader, at the part's element</param>
    private static void SkipPart(XmlReader reader)
    {
        var baseDepth = reader.Depth;
        var count = 1;

        if (reader.IsEmptyElement)
        {
            reader.Read();

            return;
        }

        reader.Read();

        while (reader.Depth > baseDepth)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                GameCatalogXml.CountElement(reader, baseDepth, ref count);
            }

            reader.Read();
        }

        reader.Read();
    }

    /// <summary>
    /// Reads one defining template.
    /// </summary>
    /// <param name="reader">The reader, at the <c>Template</c> element</param>
    /// <param name="path">The names of the first levels of the template</param>
    /// <returns>The name, whether it has a <c>Region</c> property, and the <c>RegionID</c> that property states</returns>
    private static (string? Name, bool HasRegion, string? RegionId) ReadTemplate(XmlReader reader, string[] path)
    {
        var baseDepth = reader.Depth;
        var count = 1;
        string? name = null;
        var hasRegion = false;
        string? regionId = null;

        if (reader.IsEmptyElement)
        {
            reader.Read();

            return (null, false, null);
        }

        reader.Read();

        while (reader.Depth > baseDepth)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();

                continue;
            }

            GameCatalogXml.CountElement(reader, baseDepth, ref count);

            var relative = reader.Depth - baseDepth;

            if (relative < path.Length)
            {
                path[relative] = reader.LocalName;
            }

            if (relative == 1 && path[1] == "Name")
            {
                name = reader.ReadElementContentAsString();
            }
            else if (relative == 3 && path[1] == "Properties" && path[2] == "Region" && path[3] == "RegionID")
            {
                regionId = reader.ReadElementContentAsString();
            }
            else
            {
                hasRegion |= relative == 2 && path[1] == "Properties" && path[2] == "Region";
                reader.Read();
            }
        }

        reader.Read();

        return (name, hasRegion, regionId);
    }

    #endregion // Methods
}