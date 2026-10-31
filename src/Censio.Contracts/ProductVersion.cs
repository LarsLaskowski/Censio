using System.Reflection;

namespace Censio.Contracts;

/// <summary>
/// Reads the product version an assembly was built with.
/// </summary>
public static class ProductVersion
{
    #region Constants

    /// <summary>
    /// The version shown when an assembly carries no informational version.
    /// </summary>
    public const string Fallback = "0.0.0";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the informational version of an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to read</param>
    /// <returns>The informational version, or <see cref="Fallback" /> when the assembly carries none</returns>
    public static string Read(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(version) ? Fallback : version;
    }

    #endregion // Methods
}