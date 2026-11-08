namespace Censio.Anonymiser.Tests;

/// <summary>
/// Tests for <see cref="Program" />.
/// </summary>
[TestClass]
public sealed class ProgramTests
{
    #region Methods

    /// <summary>
    /// The entry point without arguments prints the usage on standard error and returns the usage code.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task ProgramMainWithoutArgumentsReturnsUsageCode()
    {
        // Arrange
        using var error = new StringWriter();
        var previous = Console.Error;

        Console.SetError(error);

        // Act
        var code = await RunRestoringErrorAsync(previous);

        // Assert
        Assert.AreEqual(AnonymiserCommand.UsageCode, code, "Main without arguments should return the usage code");
        Assert.AreEqual("Usage: Censio.Anonymiser <recording> <output>" + Environment.NewLine, error.ToString(), "Main should print the usage on standard error");
    }

    /// <summary>
    /// Runs the entry point without arguments and puts the previous standard error back afterwards.
    /// </summary>
    /// <param name="previous">The standard error to restore</param>
    /// <returns>The exit code</returns>
    private static async Task<int> RunRestoringErrorAsync(TextWriter previous)
    {
        try
        {
            return await Program.Main([]);
        }
        finally
        {
            Console.SetError(previous);
        }
    }

    #endregion // Methods
}