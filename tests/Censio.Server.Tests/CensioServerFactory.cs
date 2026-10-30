using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Censio.Server.Tests;

/// <summary>
/// Hosts the server in memory with a chosen environment and optional settings.
/// </summary>
public sealed class CensioServerFactory : WebApplicationFactory<Program>
{
    #region Fields

    /// <summary>
    /// The hosting environment the server starts in.
    /// </summary>
    private readonly string _environment;

    /// <summary>
    /// The settings added on top of the committed configuration.
    /// </summary>
    private readonly IReadOnlyDictionary<string, string?> _settings;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CensioServerFactory" /> class.
    /// </summary>
    /// <param name="environment">The hosting environment the server starts in</param>
    /// <param name="settings">The settings added on top of the committed configuration, or <c>null</c> for none</param>
    public CensioServerFactory(string environment, IReadOnlyDictionary<string, string?>? settings = null)
    {
        _environment = environment;
        _settings = settings ?? new Dictionary<string, string?>();
    }

    #endregion // Constructors

    #region WebApplicationFactory

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));
    }

    #endregion // WebApplicationFactory
}