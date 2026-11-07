namespace Censio.Pipe;

/// <summary>
/// The random source of the anonymiser, based on the system's cryptographic generator.
/// </summary>
public sealed class PipeAnonymiserRandom : IPipeAnonymiserRandom
{
    #region IPipeAnonymiserRandom

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325
    /// <inheritdoc />
    public double NextDouble()
    {
        throw new NotImplementedException();
    }

#pragma warning restore RH2003, S2325

    #endregion // IPipeAnonymiserRandom
}