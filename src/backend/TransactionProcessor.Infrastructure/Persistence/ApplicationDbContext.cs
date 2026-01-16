using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the application
/// Manages database connections and entity tracking
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Files uploaded for processing
    /// </summary>
    public DbSet<FileEntity> Files { get; set; } = null!;

    /// <summary>
    /// Transactions parsed from CNAB files
    /// </summary>
    public DbSet<Transaction> Transactions { get; set; } = null!;

    /// <summary>
    /// Configure entity models and relationships
    /// </summary>
    /// <param name="modelBuilder">Model builder for entity configuration</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // File entity configuration
        modelBuilder.Entity<FileEntity>(entity =>
        {
            entity.ToTable("Files");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();
            entity.Property(e => e.UploadedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.ProcessedAt)
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            // Index on UploadedAt for sorting
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Status);
        });

        // Transaction entity configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type)
                .IsRequired()
                .HasConversion<string>();
            entity.Property(e => e.Amount)
                .IsRequired()
                .HasPrecision(18, 2);
            entity.Property(e => e.OccurredAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            // Relationship: Transaction belongs to File
            entity.HasOne(e => e.File)
                .WithMany(f => f.Transactions)
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for query performance
            entity.HasIndex(e => e.FileId);
            entity.HasIndex(e => e.OccurredAt);
        });
    }

    /// <summary>
    /// Configure timestamps to be stored in UTC
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Configure timestamps to be stored in UTC (async)
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensure all DateTime values are stored as UTC
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType == typeof(DateTime))
                {
                    if (property.CurrentValue is DateTime dateTime && dateTime.Kind != DateTimeKind.Utc)
                    {
                        property.CurrentValue = dateTime.ToUniversalTime();
                    }
                }
            }
        }
    }
}
