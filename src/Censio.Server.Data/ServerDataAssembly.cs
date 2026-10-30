using System.Reflection;

namespace Censio.Server.Data;

/// <summary>
/// Gives access to the assembly of <c>Censio.Server.Data</c>.
/// </summary>
public static class ServerDataAssembly
{
    #region Properties

    /// <summary>
    /// Gets the assembly of <c>Censio.Server.Data</c>.
    /// </summary>
    public static Assembly Reference => typeof(ServerDataAssembly).Assembly;

    #endregion // Properties
}