namespace Censio.Pipe.Tests;

/// <summary>
/// A read of <see cref="ScriptedPipeMessageSource" /> that stays pending until the test releases it.
/// </summary>
public sealed class HeldPipeRead
{
    #region Fields

    /// <summary>
    /// The completion of the read.
    /// </summary>
    private readonly TaskCompletionSource<PipeReadResult> _completion;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HeldPipeRead" /> class.
    /// </summary>
    /// <param name="completion">The completion of the read</param>
    internal HeldPipeRead(TaskCompletionSource<PipeReadResult> completion)
    {
        _completion = completion;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Completes the read; the code waiting for it continues on the calling thread.
    /// </summary>
    /// <param name="result">The result of the read</param>
    public void Release(PipeReadResult result)
    {
        _completion.SetResult(result);
    }

    #endregion // Methods
}