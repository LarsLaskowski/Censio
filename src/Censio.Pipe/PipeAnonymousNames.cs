namespace Censio.Pipe;

/// <summary>
/// Maps the names of a recording to invented names.
/// </summary>
internal sealed class PipeAnonymousNames
{
    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325
    /// <summary>
    /// Adds the name of an island.
    /// </summary>
    /// <param name="name">The original name</param>
    public void AddIsland(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds the name of a figure.
    /// </summary>
    /// <param name="name">The original name</param>
    public void AddFigure(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Invents the names for every name added.
    /// </summary>
    public void Complete()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Looks up the invented name of an island.
    /// </summary>
    /// <param name="original">The original name</param>
    /// <param name="invented">The invented name</param>
    /// <returns><see langword="true" /> when the name was added</returns>
    public bool TryGetIsland(string original, out string invented)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Looks up the invented name of a figure.
    /// </summary>
    /// <param name="original">The original name</param>
    /// <param name="invented">The invented name</param>
    /// <returns><see langword="true" /> when the name was added</returns>
    public bool TryGetFigure(string original, out string invented)
    {
        throw new NotImplementedException();
    }

#pragma warning restore RH2003, S2325

    #endregion // Methods
}