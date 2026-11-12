namespace Censio.Contracts;

/// <summary>
/// The wire names of the members of an enumeration of the contract: the member name with a lowercase first letter.
/// </summary>
internal static class ContractEnumNames
{
    #region Methods

    /// <summary>
    /// Creates the UTF-8 wire names of the members of an enumeration, in the order of <see cref="Enum.GetValues{TEnum}" />.
    /// </summary>
    /// <typeparam name="T">The enumeration</typeparam>
    /// <returns>The names</returns>
    internal static byte[][] Create<T>()
        where T : struct, Enum
    {
        return [.. Enum.GetNames<T>().Select(name => System.Text.Encoding.UTF8.GetBytes(char.ToLowerInvariant(name[0]) + name[1..]))];
    }

    #endregion // Methods
}