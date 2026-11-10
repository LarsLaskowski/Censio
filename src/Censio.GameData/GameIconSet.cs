namespace Censio.GameData;

/// <summary>
/// Holds the icons of the catalog's entries and the reasons others have none.
/// </summary>
public sealed class GameIconSet
{
    #region Fields

    /// <summary>
    /// The icons by asset GUID.
    /// </summary>
    private readonly IReadOnlyDictionary<int, GameIcon> _icons;

    /// <summary>
    /// The reasons assets have no icon, by asset GUID.
    /// </summary>
    private readonly IReadOnlyDictionary<int, GameIconProblem> _problems;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameIconSet" /> class.
    /// </summary>
    /// <param name="icons">The icons by asset GUID</param>
    /// <param name="problems">The reasons assets have no icon, by asset GUID</param>
    /// <param name="goodsWithIcon">The number of goods that have an icon</param>
    /// <param name="regionsWithIcon">The number of regions that have an icon</param>
    public GameIconSet(IReadOnlyDictionary<int, GameIcon> icons, IReadOnlyDictionary<int, GameIconProblem> problems, int goodsWithIcon, int regionsWithIcon)
    {
        _icons = icons;
        _problems = problems;
        GoodsWithIcon = goodsWithIcon;
        RegionsWithIcon = regionsWithIcon;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of goods that have an icon.
    /// </summary>
    public int GoodsWithIcon { get; }

    /// <summary>
    /// Gets the number of regions that have an icon.
    /// </summary>
    public int RegionsWithIcon { get; }

    /// <summary>
    /// Gets the bytes of the distinct PNG files together.
    /// </summary>
    public long PngBytes => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Finds the icon of an asset.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <returns>The icon, or <c>null</c> when the asset has none</returns>
    public GameIcon? Find(int assetGuid)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells why an asset has no icon.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <returns>The problem, or <see cref="GameIconProblem.None" /> when the asset has an icon</returns>
    public GameIconProblem Check(int assetGuid)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Counts the assets that have a problem.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The number of assets with the problem</returns>
    public int Count(GameIconProblem problem)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}