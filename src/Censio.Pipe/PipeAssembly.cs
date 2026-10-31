using System.Reflection;

namespace Censio.Pipe;

/// <summary>
/// Gives access to the assembly of <c>Censio.Pipe</c>.
/// </summary>
public static class PipeAssembly
{
    #region Properties

    /// <summary>
    /// Gets the assembly of <c>Censio.Pipe</c>.
    /// </summary>
    public static Assembly Reference => typeof(PipeAssembly).Assembly;

    #endregion // Properties
}