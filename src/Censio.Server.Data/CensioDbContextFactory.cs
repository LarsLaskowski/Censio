using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Censio.Server.Data;

/// <summary>
/// Creates the context for the design-time tools (<c>dotnet-ef migrations add</c>).
/// </summary>
internal sealed class CensioDbContextFactory : IDesignTimeDbContextFactory<CensioDbContext>
{
    #region IDesignTimeDbContextFactory

    /// <inheritdoc/>
    public CensioDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CensioDbContext>().UseSqlite("Data Source=design-time.db")
                                                                    .Options;

        return new CensioDbContext(options);
    }

    #endregion // IDesignTimeDbContextFactory
}