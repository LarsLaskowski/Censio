using Censio.Pipe;

namespace Censio.Anonymiser;

/// <summary>
/// The entry point of the anonymiser.
/// </summary>
public static class Program
{
    #region Methods

    /// <summary>
    /// Runs the anonymiser.
    /// </summary>
    /// <param name="args">The command line arguments</param>
    /// <returns>The exit code</returns>
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();

        void OnCancel(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += OnCancel;

        try
        {
            return await new AnonymiserCommand(Console.Out, Console.Error, new PipeAnonymiserRandom()).RunAsync(args, cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }

    #endregion // Methods
}