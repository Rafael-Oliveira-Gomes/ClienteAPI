using GumpStudio.Application.Abstractions;
using GumpStudio.Domain.Common;
using GumpStudio.Domain.Entities;
using GumpStudio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GumpStudio.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TattooArtist> TattooArtists => Set<TattooArtist>();
    public DbSet<ArtistCompensation> ArtistCompensations => Set<ArtistCompensation>();
    public DbSet<ArtistStockAccess> ArtistStockAccesses => Set<ArtistStockAccess>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<TattooSession> TattooSessions => Set<TattooSession>();
    public DbSet<ExamDocument> ExamDocuments => Set<ExamDocument>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<StudioGoogleConnection> GoogleConnections => Set<StudioGoogleConnection>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // Enums legíveis no banco (texto).
        c.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<ArtistType>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<CompensationType>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<UnitOfMeasure>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<SessionStatus>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<CalendarSyncStatus>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<StockMovementType>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<TransactionType>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<DocumentStatus>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Ids são gerados no código (Guid.NewGuid): sem geração pelo banco. Evita o EF tratar
        // entidades novas, descobertas via navegação, como já existentes.
        foreach (var entity in b.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)).ToList())
            b.Entity(entity.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.GoogleSubject).HasMaxLength(100);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.TokenHash).HasMaxLength(100);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TattooArtist>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Cpf).HasMaxLength(14);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Compensations).WithOne().HasForeignKey(c => c.ArtistId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.StockAccesses).WithOne().HasForeignKey(a => a.ArtistId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Compensations).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.StockAccesses).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<ArtistCompensation>(e =>
        {
            e.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
            e.Property(x => x.DailyAmount).HasPrecision(18, 2);
            e.Property(x => x.ArtistPercentage).HasPrecision(5, 2);
        });

        b.Entity<ArtistStockAccess>(e =>
        {
            e.HasKey(x => new { x.ArtistId, x.StockId });
            e.HasOne<Stock>().WithMany().HasForeignKey(x => x.StockId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Client>(e =>
        {
            e.HasIndex(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Cpf).HasMaxLength(14);
        });

        b.Entity<TattooSession>(e =>
        {
            e.HasIndex(x => new { x.ArtistId, x.StartsAt });
            e.HasIndex(x => x.ClientId);
            e.Property(x => x.Value).HasPrecision(18, 2);
            e.Property(x => x.GoogleCalendarEventId).HasMaxLength(100);
            e.Property(x => x.CalendarSyncError).HasMaxLength(500);
            e.Property(x => x.ReferenceUrl).HasMaxLength(500);
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TattooArtist>().WithMany().HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ExamDocument>(e =>
        {
            e.HasIndex(x => x.ClientId);
            e.Property(x => x.Type).HasMaxLength(100);
            e.Property(x => x.StorageKey).HasMaxLength(300);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TattooSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();   // vários NULL são permitidos no PostgreSQL
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Code).HasMaxLength(60);
            e.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            e.Property(x => x.SalePrice).HasPrecision(18, 2);
            e.Property(x => x.MinimumStock).HasPrecision(18, 3);
        });

        b.Entity<Stock>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne<TattooArtist>().WithMany().HasForeignKey(x => x.OwnerArtistId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StockMovement>(e =>
        {
            e.HasIndex(x => new { x.StockId, x.ProductId });
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.ReversesMovementId);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.QuantityDelta).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
            e.Property(x => x.UnitSalePrice).HasPrecision(18, 4);
            e.Property(x => x.Reason).HasMaxLength(300);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.Origin).HasMaxLength(30);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Stock>().WithMany().HasForeignKey(x => x.StockId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TattooSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Invoice>(e =>
        {
            e.HasIndex(x => new { x.Supplier, x.Series, x.Number });
            e.Property(x => x.Number).HasMaxLength(60);
            e.Property(x => x.Series).HasMaxLength(10);
            e.Property(x => x.Supplier).HasMaxLength(200);
            e.Property(x => x.TotalValue).HasPrecision(18, 2);
            e.Property(x => x.FileStorageKey).HasMaxLength(300);
        });

        b.Entity<FinancialTransaction>(e =>
        {
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.SessionId);
            e.Property(x => x.Amount).HasPrecision(18, 2);   // negativo apenas em estornos
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Category).HasMaxLength(100);
        });

        b.Entity<StudioGoogleConnection>(e =>
        {
            e.Property(x => x.GoogleAccountEmail).HasMaxLength(254);
            e.Property(x => x.CalendarId).HasMaxLength(200);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndGuards();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditAndGuards();
        return base.SaveChanges();
    }

    private void ApplyAuditAndGuards()
    {
        var now = clock.GetUtcNow();
        var userId = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            // Seção 33: não existe UPDATE/DELETE físico de movimentações de estoque e financeiras.
            if ((entry.Entity is StockMovement or FinancialTransaction)
                && (entry.State is EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} é imutável: registre uma movimentação/lançamento de correção.");

            if (entry.Entity is not AuditableEntity audit) continue;

            if (entry.State == EntityState.Added)
            {
                audit.CreatedAt = now;
                audit.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                audit.UpdatedAt = now;
                audit.UpdatedBy = userId;
            }
        }
    }
}
