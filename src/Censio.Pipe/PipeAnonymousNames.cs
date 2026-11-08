using System.Globalization;

namespace Censio.Pipe;

/// <summary>
/// Maps the names of a recording to invented names.
/// </summary>
internal sealed class PipeAnonymousNames
{
    #region Fields

    /// <summary>
    /// The island names in the order of first appearance.
    /// </summary>
    private readonly List<string> _islands = [];

    /// <summary>
    /// The figure names in the order of first appearance.
    /// </summary>
    private readonly List<string> _figures = [];

    /// <summary>
    /// The original names of both kinds, compared ignoring case.
    /// </summary>
    private readonly HashSet<string> _originals = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The island names added, compared exactly.
    /// </summary>
    private readonly HashSet<string> _islandSet = new(StringComparer.Ordinal);

    /// <summary>
    /// The figure names added, compared exactly.
    /// </summary>
    private readonly HashSet<string> _figureSet = new(StringComparer.Ordinal);

    /// <summary>
    /// The invented names of the islands by original name.
    /// </summary>
    private readonly Dictionary<string, string> _inventedIslands = new(StringComparer.Ordinal);

    /// <summary>
    /// The invented names of the figures by original name.
    /// </summary>
    private readonly Dictionary<string, string> _inventedFigures = new(StringComparer.Ordinal);

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Adds the name of an island.
    /// </summary>
    /// <param name="name">The original name</param>
    public void AddIsland(string name)
    {
        Add(name, _islandSet, _islands);
    }

    /// <summary>
    /// Adds the name of a figure.
    /// </summary>
    /// <param name="name">The original name</param>
    public void AddFigure(string name)
    {
        Add(name, _figureSet, _figures);
    }

    /// <summary>
    /// Invents the names for every name added: numbers in the order of first appearance, skipping a number whose name
    /// equals an original name of either kind, ignoring case.
    /// </summary>
    public void Complete()
    {
        Invent("Island", _islands, _inventedIslands);
        Invent("Player", _figures, _inventedFigures);
    }

    /// <summary>
    /// Looks up the invented name of an island.
    /// </summary>
    /// <param name="original">The original name</param>
    /// <param name="invented">The invented name</param>
    /// <returns><see langword="true" /> when the name was added</returns>
    public bool TryGetIsland(string original, out string invented)
    {
        return TryGet(original, _inventedIslands, out invented);
    }

    /// <summary>
    /// Looks up the invented name of a figure.
    /// </summary>
    /// <param name="original">The original name</param>
    /// <param name="invented">The invented name</param>
    /// <returns><see langword="true" /> when the name was added</returns>
    public bool TryGetFigure(string original, out string invented)
    {
        return TryGet(original, _inventedFigures, out invented);
    }

    /// <summary>
    /// Looks up an invented name; an empty name stays empty.
    /// </summary>
    /// <param name="original">The original name</param>
    /// <param name="inventedNames">The invented names by original name</param>
    /// <param name="invented">The invented name</param>
    /// <returns><see langword="true" /> when the name was added</returns>
    private static bool TryGet(string original, Dictionary<string, string> inventedNames, out string invented)
    {
        if (original.Length == 0)
        {
            invented = string.Empty;

            return true;
        }

        return inventedNames.TryGetValue(original, out invented!);
    }

    /// <summary>
    /// Adds a name to a pool; an empty name takes no number.
    /// </summary>
    /// <param name="name">The original name</param>
    /// <param name="set">The names of the pool</param>
    /// <param name="list">The names of the pool in order</param>
    private void Add(string name, HashSet<string> set, List<string> list)
    {
        if (name.Length == 0)
        {
            return;
        }

        _originals.Add(name);

        if (set.Add(name))
        {
            list.Add(name);
        }
    }

    /// <summary>
    /// Invents the names of a pool.
    /// </summary>
    /// <param name="prefix">The prefix of the invented names</param>
    /// <param name="names">The original names in order</param>
    /// <param name="invented">The invented names by original name</param>
    private void Invent(string prefix, List<string> names, Dictionary<string, string> invented)
    {
        var number = 0;

        foreach (var name in names)
        {
            string candidate;

            do
            {
                number++;
                candidate = string.Create(CultureInfo.InvariantCulture, $"{prefix} {number}");
            }
            while (_originals.Contains(candidate));

            invented[name] = candidate;
        }
    }

    #endregion // Methods
}