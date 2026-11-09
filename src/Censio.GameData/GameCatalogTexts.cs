namespace Censio.GameData;

/// <summary>
/// Holds the texts of one language that the catalog uses.
/// </summary>
internal sealed class GameCatalogTexts
{
    #region Properties

    /// <summary>
    /// Gets the texts by text identifier.
    /// </summary>
    internal Dictionary<long, string> Values { get; } = [];

    /// <summary>
    /// Gets the text identifiers that have two different texts.
    /// </summary>
    internal HashSet<long> Ambiguous { get; } = [];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Adds a text.
    /// </summary>
    /// <param name="id">The text identifier</param>
    /// <param name="text">The text</param>
    internal void Add(long id, string text)
    {
        if (Values.TryAdd(id, text) == false && string.Equals(Values[id], text, StringComparison.Ordinal) == false)
        {
            Ambiguous.Add(id);
        }
    }

    /// <summary>
    /// Gets the name of a text identifier.
    /// </summary>
    /// <param name="id">The text identifier, or <c>null</c></param>
    /// <returns>The name, or <c>null</c> when there is none</returns>
    internal string? NameOf(long? id)
    {
        if (id is { } key && Values.TryGetValue(key, out var text) && text.Length > 0)
        {
            return text;
        }

        return null;
    }

    #endregion // Methods
}