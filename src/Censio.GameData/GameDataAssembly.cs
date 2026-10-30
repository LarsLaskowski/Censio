using System.Reflection;

namespace Censio.GameData;

/// <summary>
/// Gives access to the assembly of <c>Censio.GameData</c>.
/// </summary>
public static class GameDataAssembly
{
    #region Properties

    /// <summary>
    /// Gets the assembly of <c>Censio.GameData</c>.
    /// </summary>
    public static Assembly Reference => typeof(GameDataAssembly).Assembly;

    #endregion // Properties
}