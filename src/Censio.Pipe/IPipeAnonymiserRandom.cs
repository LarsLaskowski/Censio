namespace Censio.Pipe;

/// <summary>
/// The source of the random numbers the anonymiser draws.
/// </summary>
public interface IPipeAnonymiserRandom
{
    #region Methods

    /// <summary>
    /// Draws the next number.
    /// </summary>
    /// <returns>A number in <c>[0, 1)</c></returns>
    double NextDouble();

    #endregion // Methods
}