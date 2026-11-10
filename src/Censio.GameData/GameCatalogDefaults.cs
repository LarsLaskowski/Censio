using System.Xml;

namespace Censio.GameData;

/// <summary>
/// Reads the template and property default files, each in one forward-only pass.
/// </summary>
internal static class GameCatalogDefaults
{
    #region Methods

    /// <summary>
    /// Reads the property defaults.
    /// </summary>
    /// <param name="stream">The property defaults file</param>
    /// <returns>The defaults</returns>
    internal static GameCatalogValues ReadPropertyDefaults(Stream stream)
    {
        using var reader = GameCatalogXml.Create(stream, "Properties");

        var names = new string[GameCatalogReader.MaxAssetDepth + 1];
        var defaults = new GameCatalogValues();
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

                    ReadValues(reader, isDefault, defaults);
                    more = reader.EOF == false;

                    continue;
                }
            }

            more = reader.Read();
        }

        return defaults;
    }

    /// <summary>
    /// Reads the facts each defining template states.
    /// </summary>
    /// <param name="stream">The templates file</param>
    /// <returns>The facts by template name</returns>
    internal static Dictionary<string, GameCatalogValues> ReadTemplates(Stream stream)
    {
        using var reader = GameCatalogXml.Create(stream, "Templates");

        var names = new string[GameCatalogReader.MaxAssetDepth + 1];
        var path = new string[4];
        var templates = new Dictionary<string, GameCatalogValues>(StringComparer.Ordinal);
        var more = reader.Read();

        while (more)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                GameCatalogXml.Enter(reader, names);

                if (string.Equals(reader.LocalName, "Template", StringComparison.Ordinal) && GameCatalogXml.HasParent(reader, names, "Group"))
                {
                    var (name, values) = ReadTemplate(reader, path);

                    if (name is not null && templates.TryAdd(name, values) == false)
                    {
                        throw GameCatalogXml.Malformed();
                    }

                    more = reader.EOF == false;

                    continue;
                }
            }

            more = reader.Read();
        }

        return templates;
    }

    /// <summary>
    /// Gets the name of the one field of a property that the catalog uses.
    /// </summary>
    /// <param name="property">The name of the property</param>
    /// <returns>The name of the field, or <c>null</c> when the catalog uses no field of the property</returns>
    private static string? FieldOf(string property)
    {
        return property switch
               {
                   "Product" => "AssociatedRegion",
                   "Building" => "AssociatedRegions",
                   "Session" => "Region",
                   "Region" => "RegionID",
                   _ => null
               };
    }

    /// <summary>
    /// Marks a property as present.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="property">The name of the property</param>
    private static void SetPresent(GameCatalogValues values, string property)
    {
        if (string.Equals(property, "Product", StringComparison.Ordinal))
        {
            values.HasProduct = true;
        }
        else if (string.Equals(property, "Building", StringComparison.Ordinal))
        {
            values.HasBuilding = true;
        }
        else if (string.Equals(property, "Session", StringComparison.Ordinal))
        {
            values.HasSession = true;
        }
        else
        {
            values.HasRegion = true;
        }
    }

    /// <summary>
    /// Stores the value of the field of a property.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="property">The name of the property</param>
    /// <param name="text">The text of the field</param>
    private static void SetValue(GameCatalogValues values, string property, string text)
    {
        if (string.Equals(property, "Product", StringComparison.Ordinal))
        {
            values.ProductRegions = text;
        }
        else if (string.Equals(property, "Building", StringComparison.Ordinal))
        {
            values.BuildingRegions = text;
        }
        else if (string.Equals(property, "Session", StringComparison.Ordinal))
        {
            values.SessionRegion = string.IsNullOrWhiteSpace(text) ? 0 : GameCatalogXml.ParseInt(text);
        }
        else
        {
            values.RegionId = text;
        }
    }

    /// <summary>
    /// Reads the parts of a <c>DefaultValues</c> or <c>DefaultContainerValues</c> element.
    /// </summary>
    /// <param name="reader">The reader, at the element</param>
    /// <param name="isDefault">Whether the element is a <c>DefaultValues</c> element</param>
    /// <param name="defaults">The defaults met so far in the file</param>
    private static void ReadValues(XmlReader reader, bool isDefault, GameCatalogValues defaults)
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

            if (reader.Depth - baseDepth == 1 && isDefault && FieldOf(reader.LocalName) is not null)
            {
                var property = reader.LocalName;

                if (IsPresent(defaults, property))
                {
                    throw GameCatalogXml.Malformed();
                }

                SetPresent(defaults, property);
                ReadDefault(reader, defaults, property);
            }
            else
            {
                SkipPart(reader);
            }
        }

        reader.Read();
    }

    /// <summary>
    /// Checks whether a property is marked as present.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="property">The name of the property</param>
    /// <returns><c>true</c> when the property is present</returns>
    private static bool IsPresent(GameCatalogValues values, string property)
    {
        return property switch
               {
                   "Product" => values.HasProduct,
                   "Building" => values.HasBuilding,
                   "Session" => values.HasSession,
                   _ => values.HasRegion
               };
    }

    /// <summary>
    /// Reads the field the catalog uses from a property default.
    /// </summary>
    /// <param name="reader">The reader, at the property element</param>
    /// <param name="defaults">The defaults</param>
    /// <param name="property">The name of the property</param>
    private static void ReadDefault(XmlReader reader, GameCatalogValues defaults, string property)
    {
        var baseDepth = reader.Depth;
        var count = 1;
        var field = FieldOf(property);

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

            if (reader.Depth - baseDepth == 1 && string.Equals(reader.LocalName, field, StringComparison.Ordinal))
            {
                SetValue(defaults, property, reader.ReadElementContentAsString());
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();
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
    /// <returns>The name, or <c>null</c> when the template has none, and the facts the template states</returns>
    private static (string? Name, GameCatalogValues Values) ReadTemplate(XmlReader reader, string[] path)
    {
        var baseDepth = reader.Depth;
        var count = 1;
        string? name = null;
        var values = new GameCatalogValues();

        if (reader.IsEmptyElement)
        {
            reader.Read();

            return (null, values);
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
            else if (relative == 3 && path[1] == "Properties" && FieldOf(path[2]) is { } field && string.Equals(path[3], field, StringComparison.Ordinal))
            {
                SetValue(values, path[2], reader.ReadElementContentAsString());
            }
            else
            {
                if (relative == 2 && path[1] == "Properties" && FieldOf(path[2]) is not null)
                {
                    SetPresent(values, path[2]);
                }

                reader.Read();
            }
        }

        reader.Read();

        return (name, values);
    }

    #endregion // Methods
}