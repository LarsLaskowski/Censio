namespace Censio.Pipe.Tests;

/// <summary>
/// An <see cref="IPipeAnonymiserRandom" /> that returns scripted numbers in order and then one number for every further
/// draw.
/// </summary>
public sealed class ScriptedPipeAnonymiserRandom : IPipeAnonymiserRandom
{
    #region Fields

    /// <summary>
    /// The scripted numbers not drawn yet.
    /// </summary>
    private readonly Queue<double> _values;

    /// <summary>
    /// The number returned once the scripted numbers are drawn.
    /// </summary>
    private readonly double _then;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptedPipeAnonymiserRandom" /> class.
    /// </summary>
    /// <param name="then">The number returned once the scripted numbers are drawn, in <c>[0, 1)</c></param>
    /// <param name="values">The scripted numbers in the order they are drawn, each in <c>[0, 1)</c></param>
    public ScriptedPipeAnonymiserRandom(double then, params double[] values)
    {
        _then = then;
        _values = new Queue<double>(values);
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of draws so far.
    /// </summary>
    public int DrawCount { get; private set; }

    #endregion // Properties

    #region IPipeAnonymiserRandom

    /// <inheritdoc />
    public double NextDouble()
    {
        DrawCount++;

        return _values.TryDequeue(out var value) ? value : _then;
    }

    #endregion // IPipeAnonymiserRandom
}