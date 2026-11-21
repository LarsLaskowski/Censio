using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(Censio.Server.Tests.RecordingHostingStartup))]

namespace Censio.Server.Tests;

/// <summary>
/// A hosting startup of the test assembly that counts its runs. A host runs it only when its hosting startup setting
/// (<c>ASPNETCORE_HOSTINGSTARTUPASSEMBLIES</c>) names the test assembly and nothing prevents hosting startups, so the
/// other host tests are unaffected.
/// </summary>
public sealed class RecordingHostingStartup : IHostingStartup
{
    #region Fields

    /// <summary>
    /// The number of runs since the last reset.
    /// </summary>
    private static int _runs;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the number of runs since the last reset.
    /// </summary>
    public static int Runs => Volatile.Read(ref _runs);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Sets the number of runs back to zero.
    /// </summary>
    public static void Reset()
    {
        Interlocked.Exchange(ref _runs, 0);
    }

    #endregion // Methods

    #region IHostingStartup

    /// <inheritdoc />
    public void Configure(IWebHostBuilder builder)
    {
        Interlocked.Increment(ref _runs);
    }

    #endregion // IHostingStartup
}