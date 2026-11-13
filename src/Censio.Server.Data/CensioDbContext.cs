using Microsoft.EntityFrameworkCore;

namespace Censio.Server.Data;

/// <summary>
/// The Entity Framework context of the server database.
/// </summary>
internal sealed class CensioDbContext : DbContext
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CensioDbContext"/> class.
    /// </summary>
    /// <param name="options">The context options</param>
    public CensioDbContext(DbContextOptions<CensioDbContext> options)
        : base(options)
    {
    }

    #endregion // Constructors

    #region DbContext

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileRow>(entity =>
                                        {
                                            entity.ToTable("Profiles");
                                            entity.HasKey(row => row.Id);
                                        });

        modelBuilder.Entity<TickRow>(entity =>
                                     {
                                         entity.ToTable("Ticks");
                                         entity.HasKey(row => row.Number);
                                         entity.Property(row => row.Number).ValueGeneratedOnAdd();
                                         entity.HasIndex(row => row.Id).IsUnique();
                                         entity.HasIndex(row => new
                                                                {
                                                                    row.ProfileId,
                                                                    row.ReceiveTicks,
                                                                    row.Number
                                                                });
                                     });

        modelBuilder.Entity<IslandRow>(entity =>
                                       {
                                           entity.ToTable("Islands");
                                           entity.HasKey(row => row.Id);
                                           entity.HasIndex(row => new
                                                                  {
                                                                      row.ProfileId,
                                                                      row.SessionGuid,
                                                                      row.IslandId,
                                                                      row.AreaIndex
                                                                  }).IsUnique();
                                       });

        modelBuilder.Entity<TickIslandRow>(entity =>
                                           {
                                               entity.ToTable("TickIslands");
                                               entity.HasKey(row => row.Id);
                                               entity.HasIndex(row => new
                                                                      {
                                                                          row.TickNumber,
                                                                          row.Position
                                                                      }).IsUnique();
                                           });

        modelBuilder.Entity<TickEntryRow>(entity =>
                                          {
                                              entity.ToTable("TickEntries");
                                              entity.HasKey(row => row.Id);
                                              entity.HasIndex(row => new
                                                                     {
                                                                         row.TickIslandId,
                                                                         row.Position
                                                                     }).IsUnique();
                                          });

        modelBuilder.Entity<TickMapPairRow>(entity =>
                                            {
                                                entity.ToTable("TickMapPairs");
                                                entity.HasKey(row => row.Id);
                                                entity.HasIndex(row => row.EntryId);
                                            });

        modelBuilder.Entity<CatalogRow>(entity =>
                                        {
                                            entity.ToTable("Catalog");
                                            entity.HasKey(row => row.Id);
                                            entity.Property(row => row.Id).ValueGeneratedNever();
                                        });

        modelBuilder.Entity<IconRow>(entity =>
                                     {
                                         entity.ToTable("Icons");
                                         entity.HasKey(row => row.Reference);
                                     });
    }

    #endregion // DbContext
}