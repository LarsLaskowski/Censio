using System.Xml;

namespace Censio.GameData;

/// <summary>
/// Reads the four catalog files, each in one forward-only pass.
/// </summary>
internal static class GameCatalogFiles
{
    #region Methods

    /// <summary>
    /// Reads the names of the region types.
    /// </summary>
    /// <param name="stream">The data set file</param>
    /// <returns>The items of the data set named <c>Region</c></returns>
    internal static HashSet<string> ReadRegionTypes(Stream stream)
    {
        using var reader = GameCatalogXml.Create(stream, "DataSets");

        var names = new string[GameCatalogReader.MaxAssetDepth + 1];
        var regionSets = 0;
        HashSet<string> regionTypes = [];

        var more = reader.Read();

        while (more)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                GameCatalogXml.Enter(reader, names);

                if (string.Equals(reader.LocalName, "DataSet", StringComparison.Ordinal) && GameCatalogXml.HasParent(reader, names, "Group"))
                {
                    var (name, items) = ReadDataSet(reader);

                    if (string.Equals(name, "Region", StringComparison.Ordinal))
                    {
                        regionSets++;
                        regionTypes = items;
                    }

                    more = reader.EOF == false;

                    continue;
                }
            }

            more = reader.Read();
        }

        return regionSets == 1 ? regionTypes : throw GameCatalogXml.Malformed();
    }

    /// <summary>
    /// Reads the defining assets.
    /// </summary>
    /// <param name="stream">The asset file</param>
    /// <returns>What the pass collected</returns>
    internal static GameCatalogAssets ReadAssets(Stream stream)
    {
        using var reader = GameCatalogXml.Create(stream, "AssetList");

        var names = new string[GameCatalogReader.MaxAssetDepth + 1];
        var path = new string[4];
        var assets = new GameCatalogAssets();
        var more = reader.Read();

        while (more)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                GameCatalogXml.Enter(reader, names);

                if (string.Equals(reader.LocalName, "Asset", StringComparison.Ordinal) && GameCatalogXml.HasParent(reader, names, "Assets"))
                {
                    assets.Add(ReadAsset(reader, path));
                    more = reader.EOF == false;

                    continue;
                }
            }

            more = reader.Read();
        }

        return assets;
    }

    /// <summary>
    /// Reads the texts a catalog uses.
    /// </summary>
    /// <param name="stream">The text file</param>
    /// <param name="used">The text identifiers to keep</param>
    /// <returns>The texts</returns>
    internal static GameCatalogTexts ReadTexts(Stream stream, HashSet<long> used)
    {
        using var reader = GameCatalogXml.Create(stream, "TextExport");

        var names = new string[GameCatalogReader.MaxAssetDepth + 1];
        var texts = new GameCatalogTexts();
        var more = reader.Read();

        while (more)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                GameCatalogXml.Enter(reader, names);

                if (reader.Depth == 2 && string.Equals(reader.LocalName, "Text", StringComparison.Ordinal) && GameCatalogXml.HasParent(reader, names, "Texts"))
                {
                    ReadText(reader, used, texts);
                    more = reader.EOF == false;

                    continue;
                }
            }

            more = reader.Read();
        }

        return texts;
    }

    /// <summary>
    /// Reads one data set.
    /// </summary>
    /// <param name="reader">The reader, at the <c>DataSet</c> element</param>
    /// <returns>The name of the data set and the names of its items</returns>
    private static (string? Name, HashSet<string> Items) ReadDataSet(XmlReader reader)
    {
        var path = new string[4];
        var items = new HashSet<string>(StringComparer.Ordinal);
        string? name = null;
        var baseDepth = reader.Depth;
        var count = 1;

        if (reader.IsEmptyElement)
        {
            reader.Read();

            return (null, items);
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
            else if (relative == 3 && path[1] == "Items" && path[2] == "Item" && path[3] == "Name")
            {
                items.Add(reader.ReadElementContentAsString());
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();

        return (name, items);
    }

    /// <summary>
    /// Reads one defining asset.
    /// </summary>
    /// <param name="reader">The reader, at the <c>Asset</c> element</param>
    /// <param name="path">The names of the first levels of the asset</param>
    /// <returns>The facts of the asset</returns>
    private static GameCatalogAsset ReadAsset(XmlReader reader, string[] path)
    {
        var asset = new GameCatalogAsset();
        var baseDepth = reader.Depth;
        var count = 1;

        if (reader.IsEmptyElement)
        {
            reader.Read();

            return asset;
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

            if (ReadFact(reader, asset, path, relative) == false)
            {
                reader.Read();
            }
        }

        reader.Read();

        return asset;
    }

    /// <summary>
    /// Reads the fact an element of an asset states, if it states one.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <param name="asset">The asset</param>
    /// <param name="path">The names of the first levels of the asset</param>
    /// <param name="relative">The depth of the element below the asset element</param>
    /// <returns><c>true</c> when the element's content was read and the reader moved past it</returns>
    private static bool ReadFact(XmlReader reader, GameCatalogAsset asset, string[] path, int relative)
    {
        switch (relative)
        {
            case 1 when path[1] == "Template":
                {
                    asset.Template = reader.ReadElementContentAsString();

                    return true;
                }

            case 1 when path[1] == "BaseAssetGUID":
                {
                    asset.HasBaseAsset = true;

                    return false;
                }

            case 2 when path[1] == "Values" && path[2] == "Building":
                {
                    asset.HasBuildingSection = true;

                    return false;
                }

            case 3 when path[1] == "Values":
                {
                    return ReadValue(reader, asset, path[2], path[3]);
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>
    /// Reads a value of the <c>Values</c> element of an asset, if the catalog uses it.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <param name="asset">The asset</param>
    /// <param name="section">The name of the section</param>
    /// <param name="name">The name of the element</param>
    /// <returns><c>true</c> when the element's content was read and the reader moved past it</returns>
    private static bool ReadValue(XmlReader reader, GameCatalogAsset asset, string section, string name)
    {
        switch (section, name)
        {
            case ("Standard", "GUID"):
                {
                    asset.Guid = GameCatalogXml.ParseInt(reader.ReadElementContentAsString());

                    return true;
                }

            case ("Text", "OasisId"):
                {
                    asset.TextId = GameCatalogXml.ParseLong(reader.ReadElementContentAsString());

                    return true;
                }

            case ("Product", "IsWorkforce"):
                {
                    asset.IsWorkforce = string.Equals(reader.ReadElementContentAsString().Trim(), "1", StringComparison.Ordinal);

                    return true;
                }

            case ("Product", "AssociatedRegion"):
                {
                    asset.ProductRegions = reader.ReadElementContentAsString();

                    return true;
                }

            case ("Building", "AssociatedRegions"):
                {
                    asset.BuildingRegions = reader.ReadElementContentAsString();

                    return true;
                }

            case ("Session", "Region"):
                {
                    asset.SessionRegion = ReadOptionalGuid(reader);

                    return true;
                }

            case ("Region", "RegionID"):
                {
                    asset.RegionId = reader.ReadElementContentAsString();

                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>
    /// Reads the GUID an element names; an empty element names none.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <returns>The GUID, or <c>null</c> when the element is empty</returns>
    private static int? ReadOptionalGuid(XmlReader reader)
    {
        var text = reader.ReadElementContentAsString();

        return string.IsNullOrWhiteSpace(text) ? null : GameCatalogXml.ParseInt(text);
    }

    /// <summary>
    /// Reads one text entry and keeps it when the catalog uses it.
    /// </summary>
    /// <param name="reader">The reader, at the <c>Text</c> element</param>
    /// <param name="used">The text identifiers to keep</param>
    /// <param name="texts">The texts</param>
    private static void ReadText(XmlReader reader, HashSet<long> used, GameCatalogTexts texts)
    {
        var baseDepth = reader.Depth;
        var count = 1;
        long? id = null;
        var text = string.Empty;

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

            GameCatalogXml.CountElement(reader, baseDepth, ref count);

            if (reader.Depth - baseDepth == 1 && string.Equals(reader.LocalName, "LineId", StringComparison.Ordinal))
            {
                id = GameCatalogXml.ParseLong(reader.ReadElementContentAsString());
            }
            else if (reader.Depth - baseDepth == 1 && string.Equals(reader.LocalName, "Text", StringComparison.Ordinal))
            {
                text = reader.ReadElementContentAsString();
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();

        if (id is { } key && used.Contains(key))
        {
            texts.Add(key, text);
        }
    }

    #endregion // Methods
}